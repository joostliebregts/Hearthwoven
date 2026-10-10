// Panel-wide text checks over every page of the sample models (integrate-05): every left-list label, chapter tab, chip
// and card line fits its box at its font size (so a label never ships cut off with "..."), and no "at least" anywhere.
// Box widths and font sizes are PanelUi's (Entry, Tab, Chip, Cards). One character is counted as 0.56 em: calibrated on
// the one cut-off label seen in game ("Drawn from the hall", 22 px in the 232 px list row), not measured per glyph. The
// cards' small mostly lowercase lines count 0.5 em with "since install" as 66 px, as they measure in the preview.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven.Panel;

static class PanelTextTests
{
    const float Em = 0.56f, SmallEm = 0.5f, Since = 66 + 6;
    const float ListW = 190, TabW = (1180f - 140 - 5 * 8) / 6, PlateColumn = 840, CardGap = 10;   // chrome A2

    static int Max(float width, float font, float em = Em) => (int)Math.Floor(width / (em * font));

    /// <summary>Every page (and every view of a view switch) of each sample.</summary>
    public static List<(string where, PanelView v)> Pages(IEnumerable<(string name, PanelInput inp)> samples)
    {
        var all = new List<(string, PanelView)>();
        foreach (var (name, inp) in samples) all.Add((name + ": About", PanelModel.Build(inp, new PanelState { ShowAbout = true })));
        foreach (var (name, inp) in samples)
            foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
                foreach (var l in PanelModel.Build(inp, new PanelState { Chapter = ch }).List)
                    foreach (var they in new[] { true, false })
                    {
                        var st = new PanelState { Chapter = ch, Page = { [ch] = l.Id }, TheyReceived = they };
                        var v = PanelModel.Build(inp, st);
                        var where = name + ": " + v.Chapters.FirstOrDefault(c => c.Selected)?.Label + " > " + l.Label;
                        all.Add((where, v));
                        foreach (var sw in PanelModel.Content(v).Where(b => b.Kind == "switch"))
                            foreach (var view in (sw.Items ?? new List<Block>()).Where(x => !x.Selected))
                            {
                                var st2 = new PanelState { Chapter = ch, Page = { [ch] = l.Id }, TheyReceived = they };
                                st2.View[sw.Id] = view.Id;
                                all.Add((where + " (" + view.Title + ")", PanelModel.Build(inp, st2)));
                            }
                    }
        return all;
    }

    public static int Run(IEnumerable<(string name, PanelInput inp)> samples)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var pages = Pages(samples);
        var tooLong = new List<string>();
        void Fit(string where, string box, string text, int max)
        {
            if (!string.IsNullOrEmpty(text) && text.Length > max) tooLong.Add($"{where} > {box}: \"{text}\" = {text.Length} chars, max {max}");
        }
        foreach (var (where, v) in pages)
        {
            // the left list (PanelUi.Entry, stretch): 22 px in 68 px rows, 18 px in 48 px rows when the list has more than 5
            var compact = v.List.Count > 5;
            foreach (var c in v.List)
            {
                var icon = !string.IsNullOrEmpty(c.Icon);
                // chrome A2: 40 px rows, 18 px, a 22 px line icon 10 px in and 10 px before the label
                Fit(where, "list", c.Label, Max(ListW - 4 - (icon ? 10 + 22 + 10 : 20) - 16, 18));
            }
            // the footer key line (PanelUi: 1052 px at 15 px, the keys six spaces apart): PanelModel.KeyLine leaves out the least needed keys
            // (KeysToDrop) while it is too wide; with the character estimate standing in for the drawn width, every other key must still fit
            Fit(where, "footer keys", PanelModel.KeyLine(v.Keys, l => l.Length <= Max(1052, 15)), Max(1052, 15));
            // chapter tabs (PanelUi.Tab): six across the 1116 px row, 20 px
            foreach (var c in v.Chapters) Fit(where, "tab", c.Label, Max(TabW - 20 - 26 - 8, 18));   // A2: the icon left of the label, 18 px
            foreach (var b in PanelModel.Content(v))
            {
                // chips (PanelUi.Chip, 14 px, as wide as their text): a view switch's and Together's; one must fit the plate
                if (b.Kind == "switch") foreach (var x in b.Items ?? new List<Block>()) Fit(where, "chip", x.Title, Max(PlateColumn / 2 - 22, 14));
                if (b.Kind == "together") foreach (var x in (b.Items ?? new List<Block>()).Where(i => i.Kind == "category")) Fit(where, "chip", x.Title, Max(PlateColumn / 2 - 22, 14));
                // item tiles (DeedsUi.ItemGrid, fix2 6): the name at 12 px right of a 32 px picture, two short lines at most, in
                // the narrowest place a grid sits (a half column of the plate: two tiles)
                if (b.Kind == "itemgrid")
                {
                    var half = (PlateColumn - 34) / 2; var per = PanelModel.ItemTilesPerLine(half);
                    var tw = (float)Math.Floor((half - PanelModel.ItemTileGap * (per - 1)) / per) - (8 + 32 + 8) - 6;
                    foreach (var x in b.Items ?? new List<Block>()) Fit(where, "item tile name (two lines)", x.Title, 2 * Max(tw, 12) - 4);   // a word break loses about two characters a line
                }
                if (b.Kind != "cards") continue;
                // cards (PanelUi.Cards): four across the plate; title 16 px after a 24 px icon, label 15 px, second line 14 + 13 px
                var w = (float)Math.Floor((PlateColumn - CardGap * 3) / 4) - 24;
                foreach (var c in b.Items ?? new List<Block>())
                {
                    Fit(where, "card title", c.Title, Max(w - (string.IsNullOrEmpty(c.Icon) ? 0 : 32), 16));
                    if (c.Tone == "unsung") Fit(where, "card description (three lines)", c.Text, 3 * Max(w, 15, SmallEm) - 6);   // wraps; a word break loses about two characters a line
                    else Fit(where, "card label", c.Text, Max(w - (!string.IsNullOrEmpty(c.RecordedFrom) ? Since : 0), 15, SmallEm));
                    var sub = (c.Items ?? new List<Block>()).FirstOrDefault();
                    if (sub != null)
                    {
                        var room = w - (!string.IsNullOrEmpty(sub.RecordedFrom) ? Since : 0) - 6 - (sub.Value ?? "").Length * SmallEm * 14;
                        Fit(where, "card second line", sub.Title, Max(room, 13, SmallEm));
                    }
                }
            }
        }
        var distinct = tooLong.Distinct().ToList();
        foreach (var t in distinct.Take(20)) System.Console.WriteLine("    " + t);
        Check(PanelModel.ItemTilesPerLine(PlateColumn) == 4 && PanelModel.ItemTilesPerLine((PlateColumn - 34) / 2) == 2, "item tiles: four across the plate, two in a half column (fix2 6)");
        Check(distinct.Count == 0, $"fit: every list label, chapter tab, chip, card line and item tile name fits its box ({pages.Count} pages and views)" + (distinct.Count > 0 ? ": " + distinct[0] : ""));

        // 0.7 redesign (REDESIGN-RULES.md part 0): a migrated page (view.Recorded) says no "since install", "before install", "faded" or "in all ·"
        var retired = new[] { "since install", "before install", "faded", "in all ·" };
        var oldWords = pages.Where(p => p.v.Recorded).SelectMany(p => PanelModel.AllText(p.v).Concat(new[] { p.v.HeadingRecordedFrom, p.v.HeadingWindow }).Concat(p.v.Keys)
                                  .Where(t => t != null && retired.Any(r => t.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0)).Select(t => p.where + ": \"" + t + "\"")).Distinct().ToList();
        Check(oldWords.Count == 0, "0.7 wording: no \"since install\", \"before install\", \"faded\" or \"in all ·\" on a migrated page (" + pages.Count(p => p.v.Recorded) + " pages and views)" + (oldWords.Count > 0 ? ": " + oldWords[0] : ""));
        // the class sweep (0.7 merge): every page of every chapter in every window, your book and the fellows' (an older sender's last session
        // too, and the preview world's books), with what sits around the page (scope, plate line, heading window, window chips, keys)
        var sweepWorld = PanelSample.Full(new DateTime(2026, 10, 9, 18, 0, 0, DateTimeKind.Utc));
        var swept = new List<string>(); int sweptPages = 0;
        foreach (var (name, inp) in samples.Concat(FullDump.Books(sweepWorld).Select(b => ("world " + b.who, b.book))))
            foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
                foreach (var l in PanelModel.Build(inp, new PanelState { Chapter = ch }).List)
                    foreach (TimeWindow tw in Enum.GetValues(typeof(TimeWindow)))
                    {
                        var v = PanelModel.Build(inp, new PanelState { Chapter = ch, Page = { [ch] = l.Id }, Window = tw }); sweptPages++;
                        swept.AddRange(PanelModel.AllText(v).Concat(new[] { v.HeadingRecordedFrom, v.HeadingWindow, v.Scope, PanelModel.PlateOf(v)?.Text }).Concat(v.Keys).Concat(v.Windows.Select(w => w.Label))
                                       .Where(t => t != null && retired.Take(2).Any(r => t.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0)).Select(t => name + " " + ch + "/" + l.Id + " " + tw + ": \"" + t + "\""));
                    }
        swept = swept.Distinct().ToList();
        Check(swept.Count == 0, "0.7 wording sweep: no \"since install\" or \"before install\" on any page in any window, own or fellow book (" + sweptPages + " views)" + (swept.Count > 0 ? ": " + swept[0] : ""));

        var atLeast = pages.Where(p => PanelModel.AllText(p.v).Any(t => t.IndexOf("at least", StringComparison.OrdinalIgnoreCase) >= 0)).Select(p => p.where).Distinct().ToList();
        Check(atLeast.Count == 0, "wording: no \"at least\" on any page or view" + (atLeast.Count > 0 ? ": " + atLeast[0] : ""));
        return fails;
    }
}
