// richtext-fix (0.6.1 in game: a Feats card showed "<b><color=#C27A4A>I</color></…" and the detail area "Earned 8 Oct <i><color=#B9A688>(or earlier)</color></i>",
// because PanelUi.Label turns rich text off and three Feats labels got tags all the same). The rule now, checked here:
// - the model hands the UI parts, never markup: no string the model produces holds a tag, on any page of the sample world, the samples,
//   the Feats fixture or the real-data fixture (Joost's own book), whatever the block kind;
// - the UI writes tags only through Rich (src/Panel/RichText.cs), puts them only on labels made by RichLabel, SetRich or BtRich, and those
//   turn rich text on through PanelRich.On(kind): the static map kind -> rich text. Every kind they name is in the map, every kind in the
//   map is drawn somewhere, and the HTML bridge reads the same map (window.PANEL_RICH) and names the same kinds.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hearthwoven;
using Hearthwoven.Panel;

static class RichTextTests
{
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => (bs ?? new List<Block>()).Where(b => b != null).SelectMany(b => new[] { b }.Concat(Every(b.Items)));

    /// <summary>Every string reachable from a model object, with the kind of the nearest block around it and the field it sits in.</summary>
    static void Walk(object o, string kind, string field, List<(string kind, string field, string text)> found, HashSet<object> seen, int depth)
    {
        if (o == null || depth > 16) return;
        if (o is string s) { found.Add((kind, field, s)); return; }
        var t = o.GetType();
        if (t.IsPrimitive || t.IsEnum || o is DateTime || o is decimal || o is Delegate) return;
        if (!t.IsValueType && !seen.Add(o)) return;
        if (o is IDictionary d) { foreach (DictionaryEntry e in d) { Walk(e.Key, kind, field + ".key", found, seen, depth + 1); Walk(e.Value, kind, field, found, seen, depth + 1); } return; }
        if (o is IEnumerable list) { foreach (var x in list) Walk(x, kind, field, found, seen, depth + 1); return; }
        if (t.Namespace == null || !t.Namespace.StartsWith("Hearthwoven")) return;
        if (o is Block b) kind = b.Kind ?? kind;
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance)) Walk(f.GetValue(o), kind, f.Name, found, seen, depth + 1);
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            object v; try { v = p.GetValue(o); } catch { continue; }
            Walk(v, kind, p.Name, found, seen, depth + 1);
        }
    }

    static string SrcDir()
    {
        var src = AppContext.BaseDirectory;
        while (src != null && !File.Exists(Path.Combine(src, "src", "Plugin.cs"))) src = Path.GetDirectoryName(src);
        return src;
    }

    /// <summary>The string literal given as the second argument of the call whose '(' is at <paramref name="open"/> (null when it is not a literal).</summary>
    static string SecondArgLiteral(string line, int open)
    {
        int depth = 0;
        for (int i = open; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"') { var end = line.IndexOf('"', i + 1); if (end < 0) return null; i = end; continue; }
            if (c == '(') depth++;
            else if (c == ')') { depth--; if (depth == 0) return null; }
            else if (c == ',' && depth == 1)
            {
                var m = Regex.Match(line.Substring(i + 1), "^\\s*\"([a-z]+)\"");
                return m.Success ? m.Groups[1].Value : null;
            }
        }
        return null;
    }

    public static int Run(IEnumerable<(string name, PanelInput inp)> samples)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- Rich: the only maker of tags ----------
        var card = Rich.Plain("Drover") + " " + Rich.Plain("II").Ink("#C27A4A").Bold();
        Check(card.Markup == "Drover <b><color=#C27A4A>II</color></b>" && card.Shown == "Drover II", "rich: a card's tier numeral is a tag pair the UI writes from the model's parts, shown as 'Drover II': " + card.Markup);
        var moment = Rich.Plain("Earned 8 Oct") + " " + Rich.Plain("(or earlier)").Ink("#B9A688").Italic();
        Check(moment.Markup == "Earned 8 Oct <i><color=#B9A688>(or earlier)</color></i>" && moment.Shown == "Earned 8 Oct (or earlier)", "rich: the detail's moment with its quiet '(or earlier)', shown without tags");
        Check(Rich.Plain("<3 Astrid").Markup == "<noparse><3 Astrid</noparse>" && Rich.Plain("a</noparse><b>x").Shown == "a<b>x" && Rich.Plain("<b>").Shown == "<b>",
              "rich: model text with a '<' stays literal (noparse, and a closing noparse inside it cannot end that early)");
        Check(Rich.Plain("x").Ink("red").Markup == "x" && Rich.Plain("x").Ink("#12345").Markup == "x" && Rich.Plain("x").Ink("C27A4A").Markup == "<color=#C27A4A>x</color>" && Rich.Plain("x").Ink("#c27a4a\"><b").Markup == "x",
              "rich: a colour is a hex colour or nothing (no way to slip a tag in through a colour)");
        Check(("a" + Rich.Empty + "b").Markup == "ab" && Rich.Empty.IsEmpty && Rich.Plain(null).IsEmpty && Rich.Plain("14").Sized(14).Markup == "<size=14>14</size>", "rich: plain joins plain, empty is empty, a size in invariant digits");

        // ---------- the matcher the test and the in-game self-check share ----------
        Check(PanelRich.HasMarkup("Drover <b><color=#C27A4A>I</color></…") && PanelRich.HasMarkup("Earned 8 Oct <i><color=#B9A688>(or earlier)</color></i>") &&
              PanelRich.HasMarkup("<size=14>x") && PanelRich.HasMarkup("<#fff>x") && PanelRich.HasMarkup("x</i>") && PanelRich.FirstTag("Drover <b><color=#C27A4A>I") == "<b",
              "markup: the 0.6.1 strings (cut inside a tag too), a size, a short colour and a lone closing tag are found");
        Check(!PanelRich.HasMarkup("<3 Astrid") && !PanelRich.HasMarkup("a < b") && !PanelRich.HasMarkup("<Boar>") && !PanelRich.HasMarkup("<1%") && !PanelRich.HasMarkup("Drover II") && !PanelRich.HasMarkup(null),
              "markup: a heart, a comparison, a word in brackets, a number and plain words are not tags");
        Check(PanelRich.On("feats") && PanelRich.On("featdetail") && PanelRich.On("featband") && !PanelRich.On("rows") && !PanelRich.On("knownfor") && !PanelRich.On(null),
              "map: rich text on for the Feats card, detail and band, off for a plain kind (rows, Known for)");

        // ---------- every string the model produces ----------
        var books = new List<(string name, PanelInput inp)>(samples);
        foreach (var (who, book) in FullDump.Books(PanelSample.Full(FeatsTests.Now))) books.Add(("world " + who, book));
        books.Add(("feats fixture", FeatsTests.Full()));
        var real = FeatsTests.Joost();
        Check(real != null, "markup: the real-data fixture loads (test-panel/fixtures/sample-0.6rc-localtotals.json)");
        if (real != null) books.Add(("real data", real));
        var pages = PanelTextTests.Pages(books);
        // the Feats pages once per card chosen too (A/D, a hover): the detail area shows each card's own block
        foreach (var (name, inp) in books)
            foreach (var page in new[] { "earned", "unsung", "together" })
            {
                var v0 = PanelModel.Build(inp, new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = page } });
                foreach (var c in PanelModel.Content(v0).Where(b => b.Kind == "feats").SelectMany(b => b.Items ?? new List<Block>()))
                    pages.Add((name + ": Feats > " + page + " (" + c.Id + ")", PanelModel.Build(inp, new PanelState { Chapter = Chapter.Feats, FeatSel = c.Id, Page = { [Chapter.Feats] = page } })));
            }
        var hits = new List<string>(); int strings = 0; var kinds = new HashSet<string>();
        foreach (var (where, v) in pages)
        {
            var found = new List<(string kind, string field, string text)>();
            Walk(v, "view", "", found, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
            foreach (var (kind, field, text) in found)
            {
                strings++; kinds.Add(kind);
                if (PanelRich.HasMarkup(text)) hits.Add(where + " > " + kind + "." + field + " (rich text " + (PanelRich.On(kind) ? "on, but the UI keeps model text literal" : "off") + "): " + text);
            }
        }
        Check(strings > 20000 && kinds.Contains("feats") && kinds.Contains("featdetail") && kinds.Contains("featband") && kinds.Contains("moment"),
              "markup: the walk reads every string of " + pages.Count + " pages (" + strings + " strings, " + kinds.Count + " block kinds, the Feats cards, detail, moment and band among them)");
        Check(hits.Count == 0, "markup: no string the model produces holds a tag, in any block kind: a kind with rich text off would show it as letters, a kind with it on gets " +
                               "its tags from the UI and model text literal (sample world, samples, Feats fixture, real data)" + (hits.Count > 0 ? ": " + hits.Count + ", e.g. " + string.Join(" | ", hits.Take(3)) : ""));

        // ---------- the UI source: tags only through Rich, rich text only through the map ----------
        var src = SrcDir();
        Check(src != null, "source: the mod's source folder is found");
        if (src == null) return fails;
        var literal = new Regex("\"(?:[^\"\\\\\\n]|\\\\.)*\"");
        var call = new Regex(@"\b(RichLabel|SetRich|BtRich)\(");
        var tagLiterals = new List<string>(); var badSwitch = new List<string>(); var noKind = new List<string>(); var markupUse = new List<string>();
        var named = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(src, "src", "Panel"), "*.cs", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != "RichText.cs").OrderBy(f => f, StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(file); var rel = Path.GetRelativePath(src, file);
            for (int n = 0; n < lines.Length; n++)
            {
                var line = lines[n]; var code = line.TrimStart();
                if (code.StartsWith("//")) continue;
                var at = rel + ":" + (n + 1);
                foreach (Match m in literal.Matches(line)) if (PanelRich.HasMarkup(m.Value)) tagLiterals.Add(at + " " + m.Value);
                var sw = Regex.Match(line, @"\.richText\s*=\s*([^;]+);");
                if (sw.Success && sw.Groups[1].Value.Trim() != "false" && !sw.Groups[1].Value.Contains("PanelRich.On(")) badSwitch.Add(at + " " + sw.Value);
                if (line.Contains(".Markup")) markupUse.Add(at);
                if (code.StartsWith("static ") || code.StartsWith("internal static ")) continue;   // the helpers' own definitions
                foreach (Match m in call.Matches(line))
                {
                    var kind = SecondArgLiteral(line, m.Index + m.Length - 1);
                    if (kind == null) noKind.Add(at + " " + m.Value); else named.Add(kind);
                }
            }
        }
        Check(tagLiterals.Count == 0, "source: no tag written in the UI outside Rich (src/Panel/RichText.cs)" + (tagLiterals.Count > 0 ? ": " + string.Join(" | ", tagLiterals.Take(4)) : ""));
        Check(badSwitch.Count == 0, "source: rich text is never switched on by hand: only false (PanelUi.Label) or PanelRich.On(kind)" + (badSwitch.Count > 0 ? ": " + string.Join(" | ", badSwitch.Take(4)) : ""));
        Check(markupUse.Count == 3, "source: a Rich string reaches a label only inside RichLabel, SetRich and BtRich (3 uses of .Markup): " + string.Join(", ", markupUse));
        Check(noKind.Count == 0, "source: every RichLabel, SetRich and BtRich call names its block kind as a literal" + (noKind.Count > 0 ? ": " + string.Join(" | ", noKind.Take(4)) : ""));
        var unlisted = named.Except(PanelRich.Kinds).ToList(); var unused = PanelRich.Kinds.Except(named).ToList();
        Check(unlisted.Count == 0 && unused.Count == 0, "map: the kinds the UI draws rich labels for are exactly PanelRich.Kinds (" + string.Join(", ", named.OrderBy(k => k)) + ")" +
              (unlisted.Count > 0 ? "; not in the map: " + string.Join(", ", unlisted) : "") + (unused.Count > 0 ? "; in the map, drawn nowhere: " + string.Join(", ", unused) : ""));

        // ---------- the HTML bridge reads the same map ----------
        var html = File.ReadAllText(Path.Combine(src, "src", "Panel", "preview", "panel-preview.html"));
        var bridge = Regex.Matches(html, "\\brich\\(\"([a-z]+)\"|\\blayered\\([^()\\n]*?,\\s*\"([a-z]+)\"\\)").Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).ToHashSet();
        Check(html.Contains("window.PANEL_RICH") && bridge.SetEquals(PanelRich.Kinds), "bridge: panel-preview.html draws markup only through rich(kind, ...) on the map from preview-data.js, for the same kinds (" + string.Join(", ", bridge.OrderBy(k => k)) + ")");
        return fails;
    }
}
