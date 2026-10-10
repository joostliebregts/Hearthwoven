// US spelling guard (0.8.1, Joost: "use US English spelling: armor, not armour"; it is also Valheim's own UI): no string literal in
// src/ holds a British form (armour, defence, colour, grey, metre, honour, practise, centre, labelled, recognise, ...). A player reads these strings: page
// titles, chips, notes, About texts, hover tags, config descriptions. What stays British is listed below with its reason: an id or a
// saved field name (changing it would break a player's saved choices or file), a file name, a developer log line. Camel-case identifiers
// (DealtAfterArmour) and prefab names (palisadefence) have no word edge before the British part, so the word list does not see them.
// The mirror's rendered text is checked in render-check.mjs (the same words); comments are not scanned, only literals.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

static class UsSpellingTests
{
    // Valheim's own names (Greydwarf, Greyling) are not forms of "grey"; "metres?" without a leading \b also finds "item-metres" and "kilometre".
    static readonly Regex British = new Regex(
        @"\b(armour|defence|offence|colour|grey(?!dwarf|ling)|honour|favour|behaviour|neighbour|labour|flavour|harbour|centre|litre)|metres?\b|\b(practis|travell|cancell|labell|modell|levell|fuell|signall|marvell|counsell|jewell)(e|ed|es|er|ers|ing)\b|\b(recogni|organi|summari|customi|categori|normali|visuali|prioriti|minimi|maximi|reali|apologi|speciali|utili|synchroni|optimi|finali|initiali|locali|saniti|authori|personali|standardi|harmoni|fertili|analy|emphasi|memori|capitali|digiti|stabili)s(e|ed|es|ing)\b|\b\w+isation\b|\b(licence|programme|whilst|amongst|learnt)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Literals that are not text a player reads, exact, per file. Each one must still be found: a stale entry fails the check.</summary>
    static readonly (string file, string literal, string why)[] Exact =
    {
        ("src/LocalTotals.cs", "armour", "the saved field of the local totals file (0.7 schema 4); an older reader keeps it"),
        ("src/ArmourTally.cs", "armour", "the saved field the ledger writes under (the default key)"),
        ("src/Plugin.cs", "armour ledger not recorded: ", "a developer log line"),
        ("src/Panel/PanelModel.cs", "practised", "the Skills view id (saved in a player's choices) and the preview data field"),
        ("src/Panel/PanelModel.cs", "colour", "a field of the preview data the HTML mirror reads"),
        ("src/Panel/PanelWarm.cs", " ms, colours ", "a developer log line"),
        ("src/Panel/PanelReuse.cs", " colour=", "a field name in the UI reuse check's structure fingerprint (compared in the process, never drawn)"),
        ("src/Panel/PanelUi.cs", "armour", "a block kind id"),
        ("src/Panel/Chapters/BattleUi.cs", "armour", "a block kind id"),
        ("src/Panel/Chapters/BattleUi.cs", "armourrow", "a block kind id"),
        ("src/Panel/Chapters/ArmourModel.cs", "armour", "a block kind id and the Defense view id (saved in a player's choices)"),
        ("src/Panel/Chapters/ArmourModel.cs", "armourrow", "a block kind id"),
        ("src/Panel/Chapters/DeedsModel.cs", "armour", "the Kind chip id of the crafting filter (saved in a player's choices); its label reads Armor"),
        ("src/Panel/Chapters/BattleModel.cs", "vocab:defence-built", "a picture file name"),
    };

    /// <summary>Whole files of developer-only text: the self-check lines (HW-CHECK in the log, Dev.SelfCheck only), the hook log lines, the bench.</summary>
    static readonly string[] DevFiles = { "src/ArmourHooks.cs", "src/CheckBook.cs", "src/DevCheck.cs", "src/Panel/PanelBench.cs" };

    static string SrcDir()
    {
        var src = AppContext.BaseDirectory;
        while (src != null && !File.Exists(Path.Combine(src, "src", "Plugin.cs"))) src = Path.GetDirectoryName(src);
        return src;
    }

    /// <summary>The text of every string literal in C# source, comments left out and interpolation holes left out (they are code), with its line.</summary>
    public static List<(int line, string text)> Literals(string code)
    {
        var found = new List<(int, string)>(); int i = 0, n = code.Length, line = 1;
        while (i < n)
        {
            char c = code[i];
            if (c == '\n') { line++; i++; continue; }
            if (c == '/' && i + 1 < n && code[i + 1] == '/') { while (i < n && code[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < n && code[i + 1] == '*') { i += 2; while (i + 1 < n && !(code[i] == '*' && code[i + 1] == '/')) { if (code[i] == '\n') line++; i++; } i += 2; continue; }
            if (c == '\'') { i++; if (i < n && code[i] == '\\') i++; i++; while (i < n && code[i] != '\'' && code[i] != '\n') i++; i++; continue; }   // a char literal
            bool verbatim = false, interp = false; int j = i;
            while (j < n && (code[j] == '$' || code[j] == '@')) { if (code[j] == '@') verbatim = true; else interp = true; j++; }
            if (j >= n || code[j] != '"' || j - i > 2) { i++; continue; }
            var text = new StringBuilder(); int start = line; i = j + 1;
            while (i < n)
            {
                char d = code[i];
                if (d == '\n') { if (!verbatim) break; line++; text.Append(d); i++; continue; }
                if (d == '"') { if (verbatim && i + 1 < n && code[i + 1] == '"') { text.Append('"'); i += 2; continue; } i++; break; }
                if (d == '\\' && !verbatim) { text.Append(i + 1 < n ? code[i + 1] : ' '); i += 2; continue; }
                if (interp && d == '{')
                {
                    if (i + 1 < n && code[i + 1] == '{') { text.Append('{'); i += 2; continue; }
                    int depth = 1; i++; text.Append(' ');
                    while (i < n && depth > 0)
                    {
                        char h = code[i];
                        if (h == '{') depth++; else if (h == '}') depth--;
                        else if (h == '"') { i++; while (i < n && code[i] != '"' && code[i] != '\n') { if (code[i] == '\\') i++; i++; } }   // a string inside the hole
                        if (h == '\n') line++;
                        i++;
                    }
                    continue;
                }
                text.Append(d); i++;
            }
            found.Add((start, text.ToString()));
        }
        return found;
    }

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // the scan itself: a literal is found, a comment and an interpolation hole are not, a verbatim literal and a char literal do not confuse it
        var probe = Literals("var a = \"colour\"; // \"armour\"\nvar b = $\"{Colour} and {x(\"grey\")} metres\";\nvar c = @\"a \"\"b\"\" centre\"; var d = '\"'; /* \"defence\" */ var e = \"honour\\\"\";");
        var probeText = probe.Select(p => Regex.Replace(p.text, @"\s+", " ").Trim()).ToList();
        Check(probeText.SequenceEqual(new[] { "colour", "and metres", "a \"b\" centre", "honour\"" }),
              "us spelling: the scan reads string literals only (not comments, not interpolation holes): " + string.Join(" | ", probeText));
        Check(British.IsMatch("Defence") && British.IsMatch("item-metres") && British.IsMatch("greyed out") && British.IsMatch("Practised") && !British.IsMatch("Greydwarf") && !British.IsMatch("Defense") && !British.IsMatch("armor") && !British.IsMatch("practiced") && !British.IsMatch("items"),
              "us spelling: the word list finds the British forms and leaves the US ones and Valheim's own names (Greydwarf) alone");

        var src = SrcDir();
        Check(src != null, "us spelling: the mod's source folder is found");
        if (src == null) return fails;
        var used = new HashSet<(string, string)>(); var hits = new List<string>(); int files = 0, literals = 0, logs = 0, ids = 0, dev = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(src, "src"), "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(src, file).Replace('\\', '/'); files++;
            foreach (var (line, text) in Literals(File.ReadAllText(file)))
            {
                literals++;
                if (!British.IsMatch(text)) continue;
                if (DevFiles.Contains(rel)) { dev++; continue; }
                if (text.StartsWith("[Hearthwoven]", StringComparison.Ordinal)) { logs++; continue; }   // a developer log line
                if (Exact.Any(x => x.file == rel && x.literal == text)) { ids++; used.Add((rel, text)); continue; }
                if (rel == "src/Panel/RichText.cs" && text.StartsWith("</?(?:b|i|u|s|color|colour|", StringComparison.Ordinal)) { ids++; continue; }   // the tag names a pattern strips
                hits.Add(rel + ":" + line + " \"" + (text.Length > 60 ? text.Substring(0, 60) + "..." : text) + "\"");
            }
        }
        Check(hits.Count == 0, "us spelling: no string literal in src/ holds a British form (" + files + " files, " + literals + " literals; " + ids + " ids and field names, " + logs + " log lines, " + dev +
                               " developer-check lines allowed, each with its reason in test-panel/UsSpellingTests.cs)" + (hits.Count > 0 ? ": " + hits.Count + ", e.g. " + string.Join(" | ", hits.Take(5)) : ""));
        var stale = Exact.Where(x => !used.Contains((x.file, x.literal))).Select(x => x.file + " \"" + x.literal + "\"").ToList();
        Check(stale.Count == 0, "us spelling: every allowed id is still in the source (a stale entry hides nothing but also rots the list)" + (stale.Count > 0 ? ": " + string.Join(" | ", stale) : ""));
        return fails;
    }
}
