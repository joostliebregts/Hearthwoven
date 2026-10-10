// 0.7 redesign, page group G4 Overview and Feats: Deeds Overview (Earned, Unsung), Feats
// (work/hearthwoven-0.7/REDESIGN-RULES.md part 5). One test per page that captures the page's rule (pattern: RecordedGatherTests.Run).
// Registered in Program.cs; the G4 page agent owns this file.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RecordedOverviewTests
{
    static IEnumerable<Block> Walk(IEnumerable<Block> blocks)
    {
        foreach (var b in blocks ?? Enumerable.Empty<Block>())
        {
            yield return b;
            foreach (var x in Walk(b.Items)) yield return x;
        }
    }

    public static int Run(PanelInput input, PanelInput edda, DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Show(PanelInput i, Chapter ch, Action<PanelState> more = null) { var s = new PanelState { Chapter = ch }; more?.Invoke(s); return PanelModel.Build(i, s); }

        // ---------- Deeds overview (hard cases 13, 14 and 16; rule A.3 and the box): one number per card, one "Earlier counts" line, the box ----------
        var over = Show(input, Chapter.Deeds);
        var cards = Walk(over.Blocks).Where(b => b.Kind == "card").ToList();
        var wood = cards.Single(c => c.Title == "Woodcutter");
        var stone = cards.Single(c => c.Title == "Stonebreaker");
        var open = Show(input, Chapter.Deeds, s => s.ShowNumbers = true);
        var box = Walk(open.Blocks).SingleOrDefault(b => b.Kind == "aboutnumbers");
        Check(over.Recorded && !Walk(over.Blocks).Any(b => b.Kind == "zone") && over.Scope == null && cards.Count >= 2 &&
              wood.Value == "410" && (wood.Items == null || wood.Items.Count == 0) && wood.Src == PanelModel.SrcCharacter &&
              stone.Src == PanelModel.SrcCharacter && (stone.Items == null || stone.Items.Count == 0) &&
              PanelModel.AllText(over).Count(t => t == PanelModel.EarlierIncomplete) == 1 &&
              !PanelModel.AllText(over).Any(t => t.Contains("since install") || t.Contains("before install") || t.Contains("faded") || t.Contains("in all ·")),
              "overview 0.7: one number per card (410 trees felled, 2 134 stone and ore, both your character's), no class C line, no zone, no scope line, one \"Earlier counts may be incomplete.\"");
        Check(box != null && box.Items.Count >= 3 && box.Items[0].Title.StartsWith("Before ") && box.Items[1].Title.StartsWith("From ") && box.Items[2].Title == "Additional details" &&
              box.Items[1].Text.StartsWith("Hearthwoven also counted every tree, pickup, planting and dish on this PC.") && over.Keys.Contains("[Y] Numbers"),
              "overview 0.7, About these numbers: Before, From and Additional details lines; Y opens it on the Deeds overview");
        var eddaOver = Show(edda, Chapter.Deeds);
        Check(eddaOver.Scope != null && eddaOver.Scope.StartsWith("Edda") && !eddaOver.Scope.Contains("since install") && eddaOver.AboutNumbers == null,
              "overview 0.7, Edda's book: \"Edda, as of ...\" as her scope (rule E), no box");

        // ---------- Feats (hard case 15): the date words come from the feat's own start: the 0.6 feats from the feats group, the rest from the install ----------
        var inst = now.AddDays(-6).AddHours(16);
        var mine = new PanelInput { IsSelf = true, PlayerName = "Rowan", InstalledUtc = inst, NowUtc = now, ToLocal = t => t,
                                    Starts = new Dictionary<string, DateTime> { [LocalTotals.StartFeats] = inst.AddDays(2) } };
        var keptFires = PanelModel.FeatById("keptfires"); var shield = PanelModel.FeatById("shieldwall");
        var old = PanelModel.FeatMomentWords(mine, new FeatMoment { Before = true, Utc = inst.AddDays(-30) }, PanelModel.CountFrom(mine, "shieldwall"));
        Check(PanelModel.CountFrom(mine, "keptfires") == inst && PanelModel.CountFrom(mine, "shieldwall") == inst.AddDays(2) && PanelModel.CountFrom(mine, "turnedblades") == inst &&
              PanelModel.FeatCounted(mine, keptFires) == "Recorded from " + PanelModel.RecordDate(mine, inst) + " · this PC" &&
              PanelModel.FeatCounted(mine, shield) == "Recorded from " + PanelModel.RecordDate(mine, inst.AddDays(2)) + " · this PC" &&
              old.text == "Earned before " + PanelModel.RecordDate(mine, inst.AddDays(2)) && old.note == "(Hearthwoven counts from " + PanelModel.RecordDate(mine, inst.AddDays(2)) + ")",
              "feats 0.7: Kept the Fires counted from the install date, Shield Wall from the feats group's start; \"Earned before <that date>\", never \"before install\"");
        var feats = Show(input, Chapter.Feats, s => s.Page[Chapter.Feats] = PanelModel.FeatsPageId);
        Check(!PanelModel.AllText(feats).Any(t => t.Contains("before install") || t.Contains("Since install") || t.Contains("since 8")),
              "feats 0.7: the Feats page says no \"before install\" and no \"Since install\" in its text");
        return fails;
    }
}
