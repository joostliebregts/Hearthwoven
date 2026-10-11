// 0.8.5 the book's layout numbers in one table (src/Panel/PanelLayout.cs; work/hearthwoven-0.8.5/LAYOUT-TABLE-PLAN.md): every spacing, size,
// padding, gap, radius, text size and bar height has one name. The preview gets the same values as preview/layout.css, written here on every
// --dump and checked on every run. The ratchet counts the numbers still written out, in the game's drawing (the *Ui.cs files and
// FireLayout.cs) and in the HTML mirror (panel-preview.html), so their counts can only go down.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hearthwoven.Panel;

static class LayoutTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static string SrcDir()
    {
        var src = AppContext.BaseDirectory;
        while (src != null && !File.Exists(Path.Combine(src, "src", "Plugin.cs"))) src = Path.GetDirectoryName(src);
        return src;
    }

    /// <summary>The files that draw the book: every *Ui.cs under src/Panel (the sample tag's PanelSampleUi.cs aside) and the Fireside's solver.</summary>
    public static IEnumerable<string> UiFiles(string src) =>
        Directory.GetFiles(Path.Combine(src, "src", "Panel"), "*.cs", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith("Ui.cs", StringComparison.Ordinal) && !f.EndsWith("PanelSampleUi.cs", StringComparison.Ordinal)) || f.EndsWith("FireLayout.cs", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal);

    /// <summary>C# code without its comments and the insides of its string literals (an interpolation hole is code, but a size is never written there).</summary>
    public static string Code(string cs)
    {
        cs = Regex.Replace(cs, @"/\*.*?\*/", m => new string('\n', m.Value.Count(c => c == '\n')), RegexOptions.Singleline);
        cs = Regex.Replace(cs, @"@?\$?""(?:[^""\\\n]|\\.)*""", "\"\"");
        cs = Regex.Replace(cs, @"'(?:[^'\\\n]|\\.)'", "' '");
        return Regex.Replace(cs, @"//[^\n]*", "");
    }

    /// <summary>The ratchet's counts: the layout numbers each file still writes out (BareNumbers; the mirror: BarePx). Lowered by each step.</summary>
    static readonly Dictionary<string, int> Written = new Dictionary<string, int>
    {
        ["BarFormUi.cs"] = 8,
        ["Chapters/AboutUi.cs"] = 71,
        ["Chapters/AwayUi.cs"] = 7,
        ["Chapters/AxisUi.cs"] = 60,
        ["Chapters/BattleFeedUi.cs"] = 88,
        ["Chapters/BattleUi.cs"] = 185,
        ["Chapters/CompanyUi.cs"] = 123,
        ["Chapters/CompareUi.cs"] = 66,
        ["Chapters/CropsUi.cs"] = 39,
        ["Chapters/DamageRowsUi.cs"] = 21,
        ["Chapters/DeedsUi.cs"] = 136,
        ["Chapters/EveryoneUi.cs"] = 67,
        ["Chapters/FeatsUi.cs"] = 173,
        ["Chapters/FightUi.cs"] = 157,
        ["Chapters/FireLayout.cs"] = 88,
        ["Chapters/FoesKeysUi.cs"] = 4,
        ["Chapters/GrowthLinesUi.cs"] = 2,
        ["Chapters/RecentUi.cs"] = 16,
        ["Chapters/SkillsBesideUi.cs"] = 2,
        ["Chapters/VoyagesHallUi.cs"] = 95,
        ["FacetUi.cs"] = 59,
        ["HoverUi.cs"] = 1,
        ["IslandsUi.cs"] = 15,
        ["PanelUi.cs"] = 478,
        ["RecordedUi.cs"] = 26,
        ["SortUi.cs"] = 1,
        ["preview/panel-preview.html"] = 1098,
    };

    static readonly Regex Number = new Regex(@"(?<![\w.])(\d+(?:\.\d+)?)f?\b(?!\.\d)");

    /// <summary>The numbers a UI file writes out in its code: every literal but 0 and 1 and a 2 that halves or doubles (h / 2, 2 * pad), outside the
    /// lines that name a value (const, static readonly).</summary>
    public static int BareNumbers(string cs) =>
        Code(cs).Split('\n').Where(l => !Regex.IsMatch(l, @"\bconst\b|\bstatic readonly\b"))
            .Sum(l => Number.Matches(l).Cast<Match>().Count(m => m.Groups[1].Value != "0" && m.Groups[1].Value != "1" &&
                !(m.Groups[1].Value == "2" && (Regex.IsMatch(l.Substring(0, m.Index), @"/\s*$") || Regex.IsMatch(l.Substring(m.Index + m.Length), @"^f?\s*\*")))));

    /// <summary>The px values the mirror writes out (its CSS and the styles its script writes), but a hairline's 1px.</summary>
    public static int BarePx(string html) => Regex.Matches(html, @"(?<![\w.#-])\d+(?:\.\d+)?px\b").Cast<Match>().Count(m => m.Value != "0px" && m.Value != "1px");

    /// <summary>Where the mirror writes a text size out as a number: a font-size with a px value, a script hole that works one out from a
    /// literal, a canvas font that measures at one. Each comes from the table: var(--text-...) in CSS, TX or layoutPx in the script.</summary>
    public static List<string> MirrorTextSizes(string html)
    {
        var hits = new List<string>();
        var lines = html.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            foreach (Match m in Regex.Matches(lines[i], @"font-size:|\.font\s*=\s*"))
            {
                // the value: up to a ; or a quote, a script hole ${...} taken whole
                var at = m.Index + m.Length; var v = new System.Text.StringBuilder(); var holes = new List<string>();
                if (m.Value.StartsWith(".font", StringComparison.Ordinal) && at < lines[i].Length) at++;   // past the string's opening quote
                while (at < lines[i].Length && !";\"'`}\n".Contains(lines[i][at]))
                {
                    if (lines[i][at] == '$' && at + 1 < lines[i].Length && lines[i][at + 1] == '{')
                    {
                        int depth = 0, from = at + 2;
                        for (at++; at < lines[i].Length; at++) { if (lines[i][at] == '{') depth++; else if (lines[i][at] == '}' && --depth == 0) break; }
                        holes.Add(lines[i].Substring(from, Math.Max(0, at - from))); v.Append("${}"); at++; continue;
                    }
                    v.Append(lines[i][at++]);
                }
                if (Regex.IsMatch(v.ToString(), @"(?<![\w.$-])\d+(?:\.\d+)?px") || holes.Any(h => Regex.IsMatch(Regex.Replace(h, @"""[^""]*""|'[^']*'", "\"\""), @"(?<![\w.])(?:[2-9]|\d{2,})\b(?!\.\d)")))   // a quoted "700 " is a weight
                    hits.Add("panel-preview.html:" + (i + 1) + " " + m.Value + v);
            }
        return hits;
    }

    /// <summary>The mirror's CSS rules: selector (comments left out) and body, in order.</summary>
    public static List<(string selector, string body)> Rules(string html)
    {
        var a = html.IndexOf("<style>", StringComparison.Ordinal); var b = html.IndexOf("</style>", StringComparison.Ordinal);
        var css = Regex.Replace(html.Substring(a + 7, b - a - 7), @"/\*.*?\*/", "", RegexOptions.Singleline);
        return Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}").Cast<Match>().Select(m => (Regex.Replace(m.Groups[1].Value.Trim(), @"\s+", " "), m.Groups[2].Value)).ToList();
    }

    /// <summary>The mirror's parts on the table (step 2): each of these declarations reads a token, var(--...), never a number of its own.</summary>
    static readonly (string rule, string property)[] MirrorOnTable =
    {
        (".panel", "width"), (".panel", "height"), (".chapters", "left"), (".chapters", "right"), (".chapters", "top"), (".chapters", "height"), (".chapters", "gap"), (".tab", "height"),
        (".listtitle", "left"), (".listtitle", "top"), (".listtitle", "width"), (".listtitle", "height"), (".list", "left"), (".list", "top"), (".list", "width"), (".list", "bottom"), (".list", "gap"),
        (".list .entry", "height"), (".content", "left"), (".content", "right"), (".content", "top"), (".content", "bottom"), (".keys", "left"), (".keys", "right"), (".keys", "bottom"), (".keys", "height"),
        (".kc", "width"), (".kc", "height"), (".heading", "height"), (".scope", "margin-top"), (".scope", "height"), (".blocks", "top"), (".hrow", "height"), (".hrow .hr", "gap"),
        (".platebg", "top"), (".plate", "left"), (".plate", "right"), (".plate", "top"), (".plate", "bottom"), (".plate", "padding"), (".plate", "gap"), (".plate.full", "padding-top"), (".plate.full", "gap"),
        (".isl", "gap"), (".isl", "padding"), (".isl", "border-radius"), (".isl.hero-isl", "padding"), (".isl-pair", "gap"), (".isl-plain", "gap"),
        (".hn", "gap"), (".hn .num", "font-size"), (".hn .what", "gap"), (".hn .what", "font-size"), (".hn.second .num", "font-size"), (".hn.second .what", "font-size"), (".hero .rule", "margin"),
        (".bf-bar", "height"), (".bf-bar.thin", "height"), (".bf-bar.tall", "height"), (".bf-list", "gap"), (".bf-list", "margin-top"), (".bf-seg .num", "font-size"),
        (".bf-tab", "column-gap"), (".bf-tab", "row-gap"), (".bf-tab", "line-height"), (".bf-tab", "font-size"), (".bf-tab .nm", "gap"), (".bf-tab .sw", "width"), (".bf-tab .sw", "height"),
        (".bf-tab .val", "font-size"), (".bf-tab .pc", "font-size"),
        (".chip", "height"), (".chip", "padding"), (".chip", "font-size"), (".chip.icon", "padding-left"), (".hr .chip.win", "padding"), (".sortrow", "gap"), (".sortrow", "height"),
        (".plate .section, .plate .cmp-h", "font-size"), (".plate .section .tot, .plate .cmp-h .tot", "font-size"),
        (".fb-lab", "width"), (".fb-chips", "gap"), (".fb-chip", "height"), (".cols", "gap"), (".cols > .col", "gap"),
        (".fcard.sel::after, .fb-chip.cur::after, .b2-col.cur .b2-tile::after", "inset"), (".bt-foerow.cur::after", "inset"),
        (".feats", "gap"), (".fgrid", "max-height"), (".fcard", "height"), (".plate.full .fgrid", "min-height"),
        // 0.8.5 the top row, the tab's inside, the page without a plate, the kitchen ledger's line boxes, the cargo sum
        (".logo", "left"), (".logo", "top"), (".logo", "height"), (".owner", "left"), (".owner", "top"), (".owner", "height"),
        (".players", "right"), (".players", "top"), (".players", "height"), (".players", "gap"), (".tab", "gap"), (".tab img", "width"), (".tab img", "height"),
        (".blocks", "gap"), (".lg-nm b", "line-height"), (".lg-nm span", "line-height"),
        (".eqn", "font-size"), (".eqn .g, .eqn .r", "gap"), (".eqn .r", "gap"), (".sum", "gap"), (".sum .cap", "font-size"), (".sum .eqn", "gap"), (".sum .m", "width"), (".sum .m", "height"),
        (".sumrows", "gap"), (".sumrow", "height"), (".sumrow", "gap"), (".sumrow", "padding"), (".sumrow .nm", "font-size"), (".sumrow .eqn", "gap"), (".sumrow .eqn .m", "width"),
        (".sumrow .eqn .m", "height"), (".sumrows .tot", "gap"), (".sumrows .tot", "padding-right"), (".sumrows .tot", "font-size"), (".sumrows .tot b", "font-size"),
    };

    /// <summary>The script's layout constants on the table: none of these is a number of its own (each reads layoutPx).</summary>
    static readonly (string pattern, string what)[] MirrorScriptOnTable =
    {
        (@"const BF = \{[^}]*\b(?:bar|thin|tall|gap|minPart|toList|row|rowGap|colGap|after): \d", "the bar form's measures (BF)"),
        (@"const LEGEND = \{[^}]*\bgap: \d", "the legend grid's gap (LEGEND.gap)"),
        (@"\bCOLUMN = \d", "the content column (COLUMN)"), (@"const ISLE = \{[^}]*: \d", "the island's measures (ISLE)"), (@"\bPLATE_W = \d", "the plate's width (PLATE_W)"),
        (@"\(6 \+ BF\.gap\)", "the least part (BF.minPart)"),
    };

    /// <summary>The text calls of the game's drawing and the argument that is their size: Label's third, RichLabel's fourth, Since's second,
    /// BtText's seventh; and a size set straight on a text (.fontSize =).</summary>
    static readonly Dictionary<string, int> SizeArg = new Dictionary<string, int> { ["Label"] = 2, ["RichLabel"] = 3, ["Since"] = 1, ["BtText"] = 6 };

    /// <summary>Where the game's drawing writes a text size out as a number: a literal in a text call's size, or set straight on a text. Each comes
    /// from the table (PanelLayout.Text, or a part's own size such as PanelLayout.Hero.Second).</summary>
    public static List<string> UiTextSizes(string file, string cs)
    {
        var hits = new List<string>(); var code = Code(cs);
        bool Written(string expr) => Regex.IsMatch(expr, @"(?<![\w.])(?:[2-9]|\d{2,})(?:\.\d+)?f?\b");
        foreach (Match m in Regex.Matches(code, @"(?<![\w.])(Label|RichLabel|Since|BtText)\("))
        {
            var before = code.Substring(code.LastIndexOf('\n', Math.Max(0, m.Index - 1)) + 1, m.Index - code.LastIndexOf('\n', Math.Max(0, m.Index - 1)) - 1);
            if (Regex.IsMatch(before, @"\b(static|TextMeshProUGUI|void)\b")) continue;   // the helper's own declaration
            var args = new List<string>(); int depth = 0, from = m.Index + m.Length, i = from;
            for (; i < code.Length; i++)
            {
                var c = code[i];
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') { if (depth-- == 0) { args.Add(code.Substring(from, i - from)); break; } }
                else if (c == ',' && depth == 0) { args.Add(code.Substring(from, i - from)); from = i + 1; }
            }
            var k = SizeArg[m.Groups[1].Value];
            if (args.Count > k && Written(args[k])) hits.Add(file + ":" + (code.Take(m.Index).Count(c => c == '\n') + 1) + " " + m.Groups[1].Value + "(..., " + args[k].Trim() + ", ...)");
        }
        foreach (Match m in Regex.Matches(code, @"\.fontSize\s*=(?!=)\s*([^;]+);"))
            if (Written(m.Groups[1].Value)) hits.Add(file + ":" + (code.Take(m.Index).Count(c => c == '\n') + 1) + " fontSize = " + m.Groups[1].Value.Trim());
        return hits;
    }

    /// <summary>The game's parts on the table (step 3): each of these names, kept in its file so the drawing code reads as before, is defined from
    /// PanelLayout and never holds a number of its own.</summary>
    static readonly (string file, string[] names)[] UiOnTable =
    {
        ("PanelUi.cs", new[] { "W", "H", "Inset", "ListW", "HeadTop", "ListTop", "Foot", "TabTop", "TabH", "TabInset", "RowH", "KeyRoom", "Wide", "PlateColumn", "PlateTop", "PlateInset",
                               "PlatePadX", "PlatePadTop", "PlatePadBottom", "FullPadTop", "PlateGap", "FullGap", "WindowPad", "HeroSize", "HeroLabel", "HeroSecond", "HeroSecondLabel", "HeroGap", "ColumnGap" }),
        ("IslandsUi.cs", new[] { "IslandPad", "IslandGap", "IslandInner", "IslandHeroTop", "IslandHeroBottom", "NarrowColumn" }),
        ("BarFormUi.cs", new[] { "BarListName", "BarListText", "BarListShare", "BarListCellGap", "BarSwatch", "BarSwatchGap" }),
        ("FacetUi.cs", new[] { "FacetLabelW", "FacetChipH", "FacetText" }),
        ("BarForm.cs", new[] { "BarFormBar", "BarFormThin", "BarFormTall", "BarFormGap", "BarFormToList", "BarFormRow", "BarFormRowGap", "BarFormColumnGap", "BarFormCellGap", "BarFormAfter", "LegendGap" }),
        ("Chapters/FeatsModel.cs", new[] { "FeatsGridRoom" }),
        ("Chapters/FeatsUi.cs", new[] { "FeatH", "FeatGap", "FeatRowGap" }),
    };

    /// <summary>A token's CSS name: its class and field in kebab case (Hero.SecondLabel is --hero-second-label).</summary>
    public static string CssName(string cls, string field) =>
        "--" + Regex.Replace(cls + "-" + field, @"(?<=[a-z0-9])(?=[A-Z])", "-").ToLowerInvariant();

    /// <summary>Every value of the table, in its order: (class, field, value), computed ones (Feats.GridRoom, a property) included and marked.</summary>
    public static List<(string cls, string field, float value, bool computed)> Tokens()
    {
        var all = new List<(string, string, float, bool)>();
        foreach (var t in typeof(PanelLayout).GetNestedTypes(BindingFlags.Public))
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(f => f.FieldType == typeof(float)))
                all.Add((t.Name, f.Name, (float)f.GetValue(null), false));
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(p => p.PropertyType == typeof(float)))
                all.Add((t.Name, p.Name, (float)p.GetValue(null), true));
        }
        return all;
    }

    /// <summary>preview/layout.js: the same table for the layout playground (layout-playground.html), [class, field, px, worked out from others].</summary>
    public static string Js() =>
        "// Generated from src/Panel/PanelLayout.cs by KsPanelTest --dump (test-panel/LayoutTests.cs) for layout-playground.html. Do not edit: change the table.\n" +
        "window.PANEL_LAYOUT = [\n" + string.Join(",\n", Tokens().Select(t => "  [\"" + t.cls + "\", \"" + t.field + "\", " + t.value.ToString("0.##", CultureInfo.InvariantCulture) + (t.computed ? ", true" : "") + "]")) + "\n];\n";

    /// <summary>preview/layout.css: one custom property per value of the table, in px.</summary>
    public static string Css()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("/* Generated from src/Panel/PanelLayout.cs by KsPanelTest --dump (test-panel/LayoutTests.cs). Do not edit: change the table. */\n:root {\n");
        foreach (var g in Tokens().GroupBy(x => x.cls))
        {
            sb.Append("  /* ").Append(g.Key).Append(" */\n");
            foreach (var (cls, field, value, _) in g) sb.Append("  ").Append(CssName(cls, field)).Append(": ").Append(value.ToString("0.##", CultureInfo.InvariantCulture)).Append("px;\n");
        }
        return sb.Append("}\n").ToString();
    }

    /// <param name="dumpDir">The --dump folder: layout.css is written there first.</param>
    public static int Run(string dumpDir = null)
    {
        fails = 0;
        var src = SrcDir();
        if (src == null) { Check(false, "layout: the mod's source folder is found"); return fails; }
        var css = Css(); var js = Js();
        if (dumpDir != null) { File.WriteAllText(Path.Combine(dumpDir, "layout.css"), css); File.WriteAllText(Path.Combine(dumpDir, "layout.js"), js); }
        var cssPath = Path.Combine(src, "src", "Panel", "preview", "layout.css"); var jsPath = Path.Combine(src, "src", "Panel", "preview", "layout.js");
        var tokens = Tokens();
        var dup = tokens.GroupBy(x => CssName(x.cls, x.field)).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Check(File.Exists(cssPath) && File.ReadAllText(cssPath) == css && File.Exists(jsPath) && File.ReadAllText(jsPath) == js && dup.Count == 0,
              "layout: preview/layout.css (and the playground's layout.js) is the table as it is now (" + tokens.Count + " values in " + tokens.Select(x => x.cls).Distinct().Count() + " parts; run KsPanelTest --dump after changing PanelLayout.cs)" +
              (dup.Count > 0 ? "; one CSS name for two values: " + string.Join(", ", dup) : ""));
        var html = File.ReadAllText(Path.Combine(src, "src", "Panel", "preview", "panel-preview.html"));
        // the mirror on the table (step 2): no text size written out as a number; the shared parts' declarations read their tokens
        var sizes = MirrorTextSizes(html);
        Check(sizes.Count == 0, "layout: the mirror writes no text size out as a number (every font-size a var(--...) of the table, the script's sizes from TX and layoutPx)" +
                                (sizes.Count > 0 ? ": " + sizes.Count + ", e.g. " + string.Join(" | ", sizes.Take(5)) : ""));
        var rules = Rules(html);
        var offTable = MirrorOnTable.Where(x => !rules.Where(r => r.selector == x.rule).SelectMany(r => Regex.Matches(r.body, @"(?<![\w-])" + Regex.Escape(x.property) + @"\s*:\s*([^;]+)").Cast<Match>())
                                                     .Select(m => m.Groups[1].Value).DefaultIfEmpty("").Last().Contains("var(--")).Select(x => x.rule + " { " + x.property + " }").ToList();
        var scriptOff = MirrorScriptOnTable.Where(x => Regex.IsMatch(html, x.pattern)).Select(x => x.what).ToList();
        Check(offTable.Count == 0 && scriptOff.Count == 0, "layout: the mirror's frame, page head, plate, islands, hero, bar form, legend, chips, sort row, filter bar, columns, focus ring and Feats grid read the table (" +
                                                           MirrorOnTable.Length + " declarations, " + MirrorScriptOnTable.Length + " script constants)" +
                                                           (offTable.Count + scriptOff.Count > 0 ? ": not on it: " + string.Join(", ", offTable.Concat(scriptOff)) : ""));
        // the game's drawing on the table (step 3): no text size written out as a number; the shared parts' names read PanelLayout
        var panel = Path.Combine(src, "src", "Panel");
        var uiSizes = UiFiles(src).SelectMany(f => UiTextSizes(Path.GetRelativePath(panel, f).Replace('\\', '/'), File.ReadAllText(f))).ToList();
        Check(uiSizes.Count == 0, "layout: the game's drawing writes no text size out as a number (Label, RichLabel, Since, BtText, fontSize: PanelLayout.Text or a part's own size)" +
                                  (uiSizes.Count > 0 ? ": " + uiSizes.Count + ", e.g. " + string.Join(" | ", uiSizes.Take(5)) : ""));
        var uiOff = UiOnTable.SelectMany(x => x.names.Where(n => Regex.IsMatch(Code(File.ReadAllText(Path.Combine(panel, x.file))), @"\b" + n + @"\s*=\s*-?\d")).Select(n => x.file + " " + n)).ToList();
        var below = Tokens().Where(t => t.cls == "Text" && t.value < PanelLayout.Text.Floor).Select(t => t.field).ToList();
        Check(uiOff.Count == 0 && below.Count == 0, "layout: the game's frame, page head, plate, islands, hero, bar form, legend, chips, columns, filter bar and Feats grid read PanelLayout (" +
                                                    UiOnTable.Sum(x => x.names.Length) + " names), and no text size of the table is under the floor" +
                                                    (uiOff.Count > 0 ? "; a number of its own: " + string.Join(", ", uiOff) : "") + (below.Count > 0 ? "; under the floor: " + string.Join(", ", below) : ""));
        // the ratchet: no file writes out more numbers than it did when the table last took some in; lower a count here when a step moves more
        var now = UiFiles(src).Select(f => (file: Path.GetRelativePath(Path.Combine(src, "src", "Panel"), f).Replace('\\', '/'), n: BareNumbers(File.ReadAllText(f))))
            .Append((file: "preview/panel-preview.html", n: BarePx(html))).ToList();
        var over = now.Where(u => u.n > (Written.TryGetValue(u.file, out var max) ? max : 0)).Select(u => u.file + " " + u.n + " (allowed " + (Written.TryGetValue(u.file, out var m) ? m : 0) + ")").ToList();
        var under = now.Where(u => Written.TryGetValue(u.file, out var max) && u.n < max).Select(u => u.file + " " + u.n).ToList();
        Check(over.Count == 0, "layout ratchet: no file writes out more layout numbers than before (the game's drawing " + now.Where(u => u.file.EndsWith(".cs")).Sum(u => u.n) + " in " + (now.Count - 1) +
                               " files, the mirror " + BarePx(html) + " px values)" + (over.Count > 0 ? ": " + string.Join(", ", over) + "; take the value from PanelLayout (src/Panel/PanelLayout.cs), " +
                               "or for a page's own geometry name it in a constant at the top of its file" : "") + (under.Count > 0 ? "; lower the count in LayoutTests.Written: " + string.Join(", ", under) : ""));
        return fails;
    }
}
