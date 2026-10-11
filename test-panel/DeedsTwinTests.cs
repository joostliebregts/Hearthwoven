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

        // Building, Groundwork and Crafting (0.7 rule B, REDESIGN-RULES.md part 5 G2): the game's counter is the number and the since-install
        // twins are gone; their asserts moved to test-panel/RecordedMakeTests.cs (one per page)

        // ---------- the twin pages in a fellow's hands: their copy carries no baseline dates ----------
        // 0.7: all six twin pages are migrated (G2: building, groundwork, crafting; G3: farming, fishing, taming); your own twin's words are in the
        // text sweep (PanelTextTests, the twin among its samples) and its sums in RecordedMakeTests.cs and RecordedFieldTests.cs. A fellow's
        // copy with baselines taken still follows the every-page rule for a fellow's book (PropertyTests): no dates, no box, never "since install"
        var theirs = Twin(input); theirs.IsSelf = false; theirs.PlayerName = "Edda";
        var twinFaults = new[] { "building", "groundwork", "crafting", "farming", "fishing", "taming" }
            .SelectMany(page => PropertyTests.Fellow(new PropertyTests.Page("Edda", false, "Deeds/" + page, null, Show(theirs, page), null)).Concat(Says(Show(theirs, page), "Counting since") ? new[] { "Counting since" } : new string[0]).Select(f => page + ": " + f)).ToList();
        Check(twinFaults.Count == 0, "twins (0.7): a fellow's six twin pages say nothing about counting since your install, carry no dates and no box, and name Edda's book" + (twinFaults.Count > 0 ? ": " + twinFaults[0] : ""));

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
