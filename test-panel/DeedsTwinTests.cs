// Deeds twins (src/Panel/Chapters/DeedsBaseline.cs and the six pages): the game's complete counters split at the baseline Hearthwoven
// took when it first ran (the zones that once showed the two parts left in 0.7: one recorded total, "Earlier counts may be incomplete"
// where it applies). Sample: PanelSample.Twins on the rich sample.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class DeedsTwinTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static PanelView Show(PanelInput i, string page) => PanelModel.Build(i, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = page } });
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
    static Block Zone(PanelView v, string id) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "zone" && b.Id == id);
    static double Num(string s) => double.Parse((s ?? "0").Replace(" ", "").Replace(" ", "").Replace(",", ""), CultureInfo.InvariantCulture);

    /// <summary>The sample with the baselines taken six days ago (PanelSample.Twins), on top of the rich sample.</summary>
    public static PanelInput Twin(PanelInput input, bool startedLater = false) => PanelSample.Twins(DeedsTests.Rich(input), startedLater);

    public static int Run(PanelInput input, PanelInput edda)
    {
        fails = 0;
        var keep = input.Fellows; var keepE = edda.Fellows;
        input.Fellows = new List<PanelInput> { edda }; edda.Fellows = new List<PanelInput> { input };
        var twin = Twin(input);

        // Building, Groundwork and Crafting (0.7 rule B, REDESIGN-RULES.md part 5 G2): the game's counter is the number and the since-install
        // twins are gone; their asserts moved to test-panel/RecordedMakeTests.cs (one per page)

        // ---------- every twin page: since install adds up to at most the character's, in both zones, on the sample; a fellow has none ----------
        // 0.7: all six twin pages are migrated (G2: building, groundwork, crafting; G3: farming, fishing, taming): no since-install zone, the
        // page is the recorded page; the per-page sums live in RecordedMakeTests.cs and RecordedFieldTests.cs
        foreach (var page in new[] { "building", "groundwork", "crafting", "farming", "fishing", "taming" })
        {
            var mine = Show(twin, page);
            Check(mine.Recorded && Zone(mine, "pc") == null && Zone(mine, "character") == null && !PanelModel.AllText(mine).Any(t => t.Contains("since install")),
                  page + " twin (0.7): the recorded page, no since-install zone and no \"since install\" anywhere");
            var theirs = Twin(input); theirs.IsSelf = false; theirs.PlayerName = "Edda";
            var tv = Show(theirs, page);
            Check(!PanelModel.AllText(tv).Any(t => t.Contains("Counting since") || t.Contains("since install")) && Zone(tv, "pc") == null && tv.AboutNumbers == null,
                  page + " twin (0.7): a fellow's page says nothing about counting since your install and has no About these numbers box");
        }

        // ---------- the in-game sample (Dev.SampleData) carries the twins too ----------
        var full = PanelSample.Full(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));
        foreach (var page in new[] { "building", "groundwork", "crafting", "farming", "fishing", "taming" })
        {
            var v = Show(full, page);
            Check(v.Recorded && Zone(v, "character") == null && Zone(v, "pc") == null && PanelModel.Content(v).Any(b => b.Kind == "hero" || b.Kind == "section"), "sample (0.7): Deeds > " + page + " is the recorded page with its numbers (hero or sections), no zones");
        }

        input.Fellows = keep; edda.Fellows = keepE;
        return fails;
    }
}
