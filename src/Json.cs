using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hearthwoven
{
    /// <summary>Tiny JSON writer: no dependency on Newtonsoft (other mods ship their own copies).</summary>
    public class Json
    {
        readonly StringBuilder b = new StringBuilder();
        bool needComma;
        public static string Q(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
        /// <summary>A number as JSON. NaN and +/-Infinity are not JSON (and MiniJson used to reject the whole file for one):
        /// they are written as 0, so one odd value from a hook or another mod never costs a file (RESILIENCE-06 item 2).</summary>
        public static string F(double v) => IsFinite(v) ? v.ToString("0.###", CultureInfo.InvariantCulture) : "0";
        public static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        // 0.8 performance pass: strings and whole numbers go straight into the builder (Q and F made a builder, a string or two per
        // value: a 27 KB page model cost 600 KB of garbage per ToJson, every 2 s while the book is open). The bytes written are the
        // same as Q and F write; a number that is not a whole one, or 0, still goes through F.
        static readonly string[] Escaped = MakeEscaped();
        static string[] MakeEscaped() { var e = new string[32]; for (int c = 0; c < 32; c++) e[c] = "\\u" + c.ToString("x4"); return e; }
        internal static void AppendQ(StringBuilder sb, string s)   // also LiveDelta.Write (0.8)
        {
            sb.Append('"');
            if (s != null)
            {
                int run = 0;   // copy plain stretches whole
                for (int i = 0; i < s.Length; i++)
                {
                    var c = s[i];
                    if (c != '"' && c != '\\' && c >= 32) continue;
                    if (i > run) sb.Append(s, run, i - run);
                    if (c < 32) sb.Append(Escaped[c]); else sb.Append('\\').Append(c);
                    run = i + 1;
                }
                if (s.Length > run) sb.Append(s, run, s.Length - run);
            }
            sb.Append('"');
        }
        static void AppendF(StringBuilder sb, double v)
        {
            if (v != 0 && v > -1e15 && v < 1e15 && v == Math.Floor(v)) { AppendWhole(sb, (long)v); return; }   // "0.###" of a whole number under 15 digits is its digits
            sb.Append(F(v));
        }
        [ThreadStatic] static char[] digits;
        static void AppendWhole(StringBuilder sb, long n)
        {
            var d = digits ?? (digits = new char[20]);
            int i = d.Length; bool neg = n < 0; ulong u = neg ? (ulong)(-n) : (ulong)n;
            do { d[--i] = (char)('0' + (int)(u % 10)); u /= 10; } while (u > 0);
            if (neg) d[--i] = '-';
            sb.Append(d, i, d.Length - i);
        }
        Json Sep() { if (needComma) b.Append(','); needComma = true; return this; }
        public Json Open() { Sep(); b.Append('{'); needComma = false; return this; }
        public Json Close() { b.Append('}'); needComma = true; return this; }
        public Json OpenArr() { Sep(); b.Append('['); needComma = false; return this; }
        public Json CloseArr() { b.Append(']'); needComma = true; return this; }
        public Json Key(string k) { Sep(); AppendQ(b, k); b.Append(':'); needComma = false; return this; }
        public Json Str(string k, string v) { Key(k); AppendQ(b, v); needComma = true; return this; }
        public Json Num(string k, double v) { Key(k); AppendF(b, v); needComma = true; return this; }
        public Json Raw(string k, string raw) { Key(k); b.Append(raw); needComma = true; return this; }
        public Json Dict(string k, IEnumerable<KeyValuePair<string, float>> d)
        {
            Key(k); b.Append('{'); var first = true;
            foreach (var kv in d) { if (kv.Value == 0f || !IsFinite(kv.Value)) continue; if (!first) b.Append(','); AppendQ(b, kv.Key); b.Append(':'); AppendF(b, kv.Value); first = false; }
            b.Append('}'); needComma = true; return this;
        }
        public override string ToString() => b.ToString();
    }
}
