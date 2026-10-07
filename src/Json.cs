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
        public static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        Json Sep() { if (needComma) b.Append(','); needComma = true; return this; }
        public Json Open() { Sep(); b.Append('{'); needComma = false; return this; }
        public Json Close() { b.Append('}'); needComma = true; return this; }
        public Json OpenArr() { Sep(); b.Append('['); needComma = false; return this; }
        public Json CloseArr() { b.Append(']'); needComma = true; return this; }
        public Json Key(string k) { Sep(); b.Append(Q(k)).Append(':'); needComma = false; return this; }
        public Json Str(string k, string v) { Key(k); b.Append(Q(v)); needComma = true; return this; }
        public Json Num(string k, double v) { Key(k); b.Append(F(v)); needComma = true; return this; }
        public Json Raw(string k, string raw) { Key(k); b.Append(raw); needComma = true; return this; }
        public Json Dict(string k, IEnumerable<KeyValuePair<string, float>> d)
        {
            Key(k); b.Append('{'); var first = true;
            foreach (var kv in d) { if (kv.Value == 0f) continue; if (!first) b.Append(','); b.Append(Q(kv.Key)).Append(':').Append(F(kv.Value)); first = false; }
            b.Append('}'); needComma = true; return this;
        }
        public override string ToString() => b.ToString();
    }
}
