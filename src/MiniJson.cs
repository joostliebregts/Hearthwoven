using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hearthwoven
{
    /// <summary>
    /// Minimal JSON reader for snapshots shared by other players (no Newtonsoft: other mods ship their own copies).
    /// Objects -> Dictionary&lt;string, object&gt;, arrays -> List&lt;object&gt;, numbers -> double. Returns null on bad input.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            int i = 0;
            try { var v = Value(s, ref i, 0); return v; } catch { return null; }
        }

        // reading helpers for what Json wrote: a missing or mistyped field reads as empty, never throws
        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
        public static double Num(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is double x ? x : 0;
        public static string Str(Dictionary<string, object> d, string k, string otherwise = "") => d != null && d.TryGetValue(k, out var v) && v is string x ? x : otherwise;
        /// <summary>Copies the numbers of a parsed object into a tally (sets, does not add); null: nothing.</summary>
        public static void Into(Dictionary<string, object> from, IDictionary<string, float> to)
        {
            if (from == null) return;
            foreach (var kv in from) if (kv.Value is double v && Json.IsFinite((float)v)) to[kv.Key] = (float)v;   // beyond float range: skipped
        }

        /// <summary>A parsed value written back as JSON (objects, arrays, strings, numbers, true/false/null), so a file can
        /// carry keys this version does not know through a rewrite unchanged (LocalTotals.Extra). Numbers keep full precision.</summary>
        public static string Write(object v)
        {
            var b = new StringBuilder(); Write(b, v, 0); return b.ToString();
        }

        static void Write(StringBuilder b, object v, int depth)
        {
            if (depth > MaxDepth) { b.Append("null"); return; }
            switch (v)
            {
                case null: b.Append("null"); break;
                case string s: b.Append(Json.Q(s)); break;
                case bool x: b.Append(x ? "true" : "false"); break;
                case double d: b.Append(Json.IsFinite(d) ? d.ToString("R", CultureInfo.InvariantCulture) : "0"); break;
                case Dictionary<string, object> o:
                    b.Append('{'); var first = true;
                    foreach (var kv in o) { if (!first) b.Append(','); first = false; b.Append(Json.Q(kv.Key)).Append(':'); Write(b, kv.Value, depth + 1); }
                    b.Append('}'); break;
                case List<object> l:
                    b.Append('[');
                    for (int i = 0; i < l.Count; i++) { if (i > 0) b.Append(','); Write(b, l[i], depth + 1); }
                    b.Append(']'); break;
                default: b.Append("null"); break;
            }
        }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        const int MaxDepth = 64;   // shared copies come from other PCs: a deep nesting must not overflow the stack

        static bool At(string s, int i, string word) => string.CompareOrdinal(s, i, word, 0, word.Length) == 0;

        // longest first: "-Infinity" before "-"; the infinity sign is what .NET Core writes for Infinity
        static readonly string[] NonFinite = { "-Infinity", "+Infinity", "Infinity", "-NaN", "NaN", "-\u221E", "\u221E" };

        static bool WellFormed(string t)
        {
            int i = 0, digits = 0;
            if (i < t.Length && (t[i] == '-' || t[i] == '+')) i++;
            while (i < t.Length && char.IsDigit(t[i])) { i++; digits++; }
            if (i < t.Length && t[i] == '.') { i++; while (i < t.Length && char.IsDigit(t[i])) { i++; digits++; } }
            if (digits == 0) return false;
            if (i < t.Length && (t[i] == 'e' || t[i] == 'E'))
            {
                i++; if (i < t.Length && (t[i] == '-' || t[i] == '+')) i++;
                int exp = 0; while (i < t.Length && char.IsDigit(t[i])) { i++; exp++; }
                if (exp == 0) return false;
            }
            return i == t.Length;
        }

        static object Value(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw new System.FormatException("too deep");
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i); var k = Str(s, ref i); Ws(s, ref i); i++;   // ':'
                    d[k] = Value(s, ref i, depth + 1); Ws(s, ref i);
                    if (s[i++] == '}') return d;
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i, depth + 1)); Ws(s, ref i);
                    if (s[i++] == ']') return l;
                }
            }
            if (c == '"') return Str(s, ref i);
            if (At(s, i, "true")) { i += 4; return true; }
            if (At(s, i, "false")) { i += 5; return false; }
            if (At(s, i, "null")) { i += 4; return null; }
            // not JSON, but older Hearthwoven files hold them (Json.F wrote NaN/Infinity before 0.6.1): read as 0, never fail the file
            foreach (var word in NonFinite) if (At(s, i, word)) { i += word.Length; return 0d; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            var token = s.Substring(start, i - start);
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return Json.IsFinite(n) ? n : 0d;
            if (WellFormed(token)) return 0d;   // a well-formed number out of double's range (1e999): 0, not a failed file
            throw new System.FormatException("not a number: " + token);
        }

        static string Str(string s, ref int i)
        {
            var b = new StringBuilder(); i++;   // opening quote
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': b.Append('\n'); break;
                        case 't': b.Append('\t'); break;
                        case 'r': b.Append('\r'); break;
                        case 'b': b.Append('\b'); break;
                        case 'f': b.Append('\f'); break;
                        case 'u': b.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber)); i += 4; break;
                        default: b.Append(s[i]); break;
                    }
                    i++;
                }
                else b.Append(s[i++]);
            }
            i++;
            return b.ToString();
        }
    }
}
