using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Hearthwoven
{
    /// <summary>
    /// Server: what a snapshot must pass before it is stored (RESILIENCE-06 item 5, A9 and A11). Pure C# (no Unity calls), so
    /// the tests run it without the game.
    /// - Size: at most MaxPackedBytes of gzip (200 parts of Fragments.Size, the Assembler's own limits) and at most MaxJsonBytes
    ///   once unpacked. Gunzip reads in blocks and stops at the limit, so a gzip bomb never inflates in memory.
    /// - Shape: one JSON object and nothing after it (IsJsonObject, a strict linear reader with a depth limit), so a broken
    ///   snapshot never makes players/*.json unreadable for kstats. NaN and Infinity pass as numbers (older clients write them;
    ///   MiniJson and Python read them).
    /// - Rate: RateGate, at most one snapshot per sender per MinGap seconds; a logout, quit or share change may come sooner, at
    ///   most once per MinGap and never within MinGapEarly of the last one. The rest is ignored, logged once per connection.
    /// </summary>
    public static class ServerIntake
    {
        public const int MaxPackedBytes = 200 * Fragments.Size;    // 600 000 bytes: the Assembler's 200 parts of 3 000
        public const int MaxJsonBytes = 4 * 1024 * 1024;           // a real 0.6 snapshot is 26 KB (8 KB packed), the heaviest test one 0.7 MB (unit G3)
        public const int MaxDepth = 64;

        /// <summary>The UTF-8 text inside <paramref name="data"/>, or null (with <paramref name="why"/>) when it is too big packed or
        /// unpacked, or not gzip. Never holds more than <paramref name="maxBytes"/> plus one block in memory.</summary>
        public static string Gunzip(byte[] data, int maxBytes, out string why)
        {
            why = null;
            if (data == null || data.Length == 0) { why = "empty"; return null; }
            if (data.Length > MaxPackedBytes) { why = $"{data.Length} bytes packed is over the limit of {MaxPackedBytes}"; return null; }
            try
            {
                using (var gz = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
                using (var outp = new MemoryStream())
                {
                    var buf = new byte[16384];
                    int n;
                    while ((n = gz.Read(buf, 0, buf.Length)) > 0)
                    {
                        if (outp.Length + n > maxBytes) { why = $"over {maxBytes} bytes unpacked (stopped reading there)"; return null; }
                        outp.Write(buf, 0, n);
                    }
                    return new UTF8Encoding(false, true).GetString(outp.GetBuffer(), 0, (int)outp.Length);
                }
            }
            catch (Exception e) { why = "not readable as gzip text: " + e.GetType().Name; return null; }
        }

        /// <summary>True when <paramref name="s"/> is exactly one JSON object (whitespace around it allowed), nested at most MaxDepth.</summary>
        public static bool IsJsonObject(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int i = 0;
            Ws(s, ref i);
            if (i >= s.Length || s[i] != '{') return false;
            if (!Value(s, ref i, 0)) return false;
            Ws(s, ref i);
            return i == s.Length;
        }

        static void Ws(string s, ref int i) { while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++; }

        static bool Value(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) return false;
            Ws(s, ref i);
            if (i >= s.Length) return false;
            char c = s[i];
            if (c == '{' || c == '[')
            {
                char close = c == '{' ? '}' : ']';
                i++; Ws(s, ref i);
                if (i < s.Length && s[i] == close) { i++; return true; }
                while (true)
                {
                    if (c == '{')
                    {
                        Ws(s, ref i);
                        if (i >= s.Length || s[i] != '"' || !Str(s, ref i)) return false;
                        Ws(s, ref i);
                        if (i >= s.Length || s[i] != ':') return false;
                        i++;
                    }
                    if (!Value(s, ref i, depth + 1)) return false;
                    Ws(s, ref i);
                    if (i >= s.Length) return false;
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == close) { i++; return true; }
                    return false;
                }
            }
            if (c == '"') return Str(s, ref i);
            foreach (var word in Words) if (string.CompareOrdinal(s, i, word, 0, word.Length) == 0) { i += word.Length; return true; }
            return Number(s, ref i);
        }

        static readonly string[] Words = { "true", "false", "null", "NaN", "Infinity", "-Infinity" };

        static bool Str(string s, ref int i)
        {
            i++;   // opening quote
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"') { i++; return true; }
                if (c < ' ') return false;
                if (c == '\\')
                {
                    if (++i >= s.Length) return false;
                    if (s[i] == 'u')
                    {
                        if (i + 4 >= s.Length) return false;
                        for (int k = 1; k <= 4; k++) if (Uri.IsHexDigit(s[i + k]) == false) return false;
                        i += 4;
                    }
                    else if ("\"\\/bfnrt".IndexOf(s[i]) < 0) return false;
                }
                i++;
            }
            return false;
        }

        static bool Number(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && s[i] == '-') i++;
            int digits = 0;
            while (i < s.Length && char.IsDigit(s[i])) { i++; digits++; }
            if (digits == 0) return false;
            if (i < s.Length && s[i] == '.') { i++; int f = 0; while (i < s.Length && char.IsDigit(s[i])) { i++; f++; } if (f == 0) return false; }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++; if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                int e = 0; while (i < s.Length && char.IsDigit(s[i])) { i++; e++; }
                if (e == 0) return false;
            }
            return i > start;
        }

        /// <summary>Per sender: at most one snapshot per MinGap seconds (logout, quit and share changes may come sooner, see the class).</summary>
        public class RateGate
        {
            public const double MinGap = 20, MinGapEarly = 2;
            readonly Dictionary<long, double> last = new Dictionary<long, double>(), lastEarly = new Dictionary<long, double>();
            readonly HashSet<long> warned = new HashSet<long>();
            public int Refused { get; private set; }

            public static bool MayComeEarly(string reason) => reason == "logout" || reason == "quit" || reason == "share-changed";

            /// <summary>True when this snapshot may be stored. <paramref name="firstRefusal"/>: refused, and the first time for this
            /// sender (log it then, never again for this connection).</summary>
            public bool Allow(long sender, string reason, double now, out bool firstRefusal)
            {
                firstRefusal = false;
                if (!last.TryGetValue(sender, out var t) || now - t >= MinGap || now < t) { last[sender] = now; return true; }
                if (MayComeEarly(reason) && now - t >= MinGapEarly && (!lastEarly.TryGetValue(sender, out var e) || now - e >= MinGap || now < e))
                {
                    last[sender] = now; lastEarly[sender] = now; return true;
                }
                Refused++;
                firstRefusal = warned.Add(sender);
                return false;
            }

            /// <summary>Forgets senders that are no longer connected (bounded by the connected peers).</summary>
            public void Prune(Func<long, bool> connected)
            {
                foreach (var s in last.Keys.Where(k => !connected(k)).ToList()) { last.Remove(s); lastEarly.Remove(s); warned.Remove(s); }
            }

            public int Senders => last.Count;
        }

        /// <summary>One warning per sender and kind of problem (a bad or hostile client must not flood the server log).</summary>
        public class OnceEach
        {
            readonly HashSet<string> seen = new HashSet<string>();
            public bool First(long sender, string what) => seen.Count < 10000 && seen.Add(sender + "|" + what);
            public void Prune(Func<long, bool> connected) => seen.RemoveWhere(k => !connected(long.Parse(k.Substring(0, k.IndexOf('|')), System.Globalization.CultureInfo.InvariantCulture)));
        }
    }
}
