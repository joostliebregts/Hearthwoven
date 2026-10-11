// 0.8.2 (Joost in game on Deeds > Cooking: a bar part filtered the page, its legend item "Meals 557 78 %" did not, and Dishes by kind did
// not filter at all): one rule for the whole book. A bar's part and its legend item are one click (PanelModel.PartTarget, made once per
// row in BarFormUi.BarWithList and the preview's barForm; render-check.mjs holds the drawn pairs to it), and on every page with a filter
// every bar either filters or is on the list below with the reason it cannot. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class LegendFilterTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    /// <summary>The bars of a page with a filter that cannot map to it, and why (the audit, 0.8.2). A new bar on such a page lands here or filters.</summary>
    static readonly Dictionary<string, string> CannotMap = new Dictionary<string, string>
    {
        ["Deeds/building: Materials recovered"] = "materials picked up from pieces that came down, not pieces: no tab or main material",
        ["Battle/overview: What hurt you"] = "damage types; the page filters by biome, and the biome tiles are that filter",
    };

    static PanelView Page(PanelInput who, Chapter c, string page, PanelState st = null)
    {
        st = st ?? new PanelState(); st.Chapter = c; st.Page[c] = page;
        return PanelModel.Build(who, st);
    }

    // every bar in the one bar form (a filter's linked bars, the page's compositions), named by its title or, untitled, by the section above it
    static List<(string name, Block bar)> Bars(PanelView v)
    {
        var res = new List<(string, Block)>(); string section = null;
        foreach (var b in PanelModel.Content(v))
        {
            if (b.Kind == "section") section = b.Title;
            if (b.Kind == "filterbar") foreach (var f in (b.Items ?? new List<Block>()).Where(i => i.Kind == "facetbar")) res.Add((f.Title, f));
            if (b.Kind == "composition") res.Add((string.IsNullOrEmpty(b.Title) ? section : Words(b.Title), b));
        }
        return res;
    }
    static string Words(string title) => title.StartsWith("What hurt", StringComparison.Ordinal) ? "What hurt you" : title;   // "What hurt you in the Swamp"

    public static int Run(DateTime now)
    {
        fails = 0;
        var world = PanelSample.Full(now);

        // ---- every page with a filter: each of its bars filters, or cannot map and says why ----
        var unlisted = new List<string>(); var seen = new HashSet<string>(); var pages = 0; var bars = 0; var parts = 0; var off = new List<string>();
        foreach (Chapter c in Enum.GetValues(typeof(Chapter)))
            foreach (var page in Page(world, c, null).List.Select(x => x.Id).Distinct().ToList())
            {
                var v = Page(world, c, page);
                var filter = PanelModel.FilterOf(v);
                if (filter == null) continue;
                pages++;
                foreach (var (name, bar) in Bars(v))
                {
                    var key = c + "/" + page + ": " + name; seen.Add(key);
                    if (bar.PartLink == null) { if (!CannotMap.ContainsKey(key)) unlisted.Add(key); continue; }
                    bars++;
                    // each part drawn (not the fold): its click chooses it, shown chosen on the part and on its legend item; a second click unchooses it
                    foreach (var row in PanelModel.BarFormOf(bar.Items ?? new List<Block>()).Rows.Where(r => r.Part.Id != PanelModel.FoldId))
                    {
                        parts++;
                        var target = PanelModel.PartTarget(bar, row.Part);
                        var st = new PanelState();
                        if (target == null || !PanelModel.FollowFacet(st, target)) { off.Add(key + " '" + row.Title + "' has no filter target"); continue; }
                        var after = Bars(Page(world, c, page, st)).FirstOrDefault(x => x.name == name).bar;
                        var form = after == null ? null : PanelModel.BarFormOf(after.Items ?? new List<Block>());
                        var lit = form?.Rows.Where(r => r.Selected).Select(r => r.Part.Id).ToList() ?? new List<string>();
                        if (!(PanelModel.FilterOn(st, filter.Id) && after.Items.Any(p => p.Id == row.Part.Id && p.Selected) && lit.SequenceEqual(new[] { row.Part.Id })))
                            off.Add(key + " '" + row.Title + "': chosen " + PanelModel.FilterOn(st, filter.Id) + ", lit in the legend: " + string.Join(",", lit));
                        PanelModel.FollowFacet(st, target);
                        if (PanelModel.FilterOn(st, filter.Id)) off.Add(key + " '" + row.Title + "': a second click leaves it chosen");
                    }
                }
            }
        var gone = CannotMap.Keys.Where(k => !seen.Contains(k)).ToList();
        Check(unlisted.Count == 0 && gone.Count == 0 && pages >= 7,
              "legend filter: on all " + pages + " pages with a filter every bar filters or cannot map and says why (" + bars + " filter, " + CannotMap.Count + " cannot)" +
              (unlisted.Count > 0 ? "; not filtering and not listed: " + string.Join("; ", unlisted) : "") + (gone.Count > 0 ? "; listed but gone: " + string.Join("; ", gone) : ""));
        foreach (var o in off.Take(10)) System.Console.WriteLine("  off: " + o);
        Check(off.Count == 0 && parts > 20, "legend filter: each of the " + parts + " parts of those bars chooses itself on a click, chosen on its part and its legend item alike, and unchooses on a second (" + off.Count + " off)");
        return fails;
    }
}
