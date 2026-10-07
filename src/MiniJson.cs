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

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        const int MaxDepth = 64;   // shared copies come from other PCs: a deep nesting must not overflow the stack

        static bool At(string s, int i, string word) => string.CompareOrdinal(s, i, word, 0, word.Length) == 0;

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
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
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
