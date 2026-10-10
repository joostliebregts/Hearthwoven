// 0.7 redesign, page group G2 Deeds make: Cooking, Building, Groundwork, Crafting
// (work/hearthwoven-0.7/REDESIGN-RULES.md part 5). One test per page that captures the page's rule (pattern: RecordedGatherTests).
// Registered in Program.cs. The since-install twins of these pages are gone (rule B): the game's counter is the number, its
// earlier part and the part since the first run live in the About box.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RecordedMakeTests
{
    public static int Run(PanelInput input, PanelInput edda, DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Show(PanelInput i, string page, Action<PanelState> more = null) { var s = new PanelState { Page = { [Chapter.Deeds] = page } }; more?.Invoke(s); return PanelModel.Build(i, s); }
        int Incomplete(PanelView v) => PanelModel.AllText(v).Count(t => t == PanelModel.EarlierIncomplete);
        var numbersOn = new Action<PanelState>(s => s.ShowNumbers = true);

        // ---------- Cooking (rules A, B.4, K): dishes cooked is the sum, the skill beside the hero, the bar's parts are sums ----------
        var realBook = RecordedTests.RealBook();
        if (realBook == null) Check(false, "cooking 0.7, the real book: the fixture loads");
        else
        {
            var bc = Show(realBook, "cooking"); var bcItems = PanelModel.Content(bc);
            var bh = bcItems.First(b => b.Kind == "hero");
            var bbox = Show(realBook, "cooking", numbersOn).AboutNumbers;
            Check(bc.Recorded && bh.Value == "259" && bh.Src == PanelModel.SrcCharacter && bh.Note == null && PanelModel.AboutText(bc).Contains(PanelModel.EarlierIncomplete) &&
                  Incomplete(bc) == 1 && PanelModel.SkillOf(bh) != null && PanelModel.SkillOf(bh).Id == "Skills/Cooking" &&
                  !bcItems.Any(b => b.Kind == "zone" || b.Kind == "ladders") && bcItems.Single(b => b.Kind == "composition").Items.All(p => p.Fraction2 == 0) &&
                  bcItems.Single(b => b.Kind == "composition").Note == null,
                  "cooking 0.7, the real book: 259 dishes cooked, the character's whole count (rule A) with \"Earlier counts may be incomplete.\" in About these numbers (0.8 layout D+); the Cooking skill beside the hero; the dish bar its sums, no faded share, no zone, no ladder strip");
            Check(bbox != null && bbox.Items.Count >= 3 && bbox.Items[0].Title == "Before 8 October" && bbox.Items[0].Text.StartsWith("The game's own count: 259 dishes.") &&
                  bbox.Items[1].Title == "From 8 October" && bbox.Items[2].Title == PanelModel.DetailsLabel,
                  "cooking 0.7, the real book: About these numbers says before 8 October the game's own 259 dishes (what the station owner's grill missed), from 8 October what Hearthwoven counted, the details line");
        }
        var cook7 = Show(PanelSample.Full(now), "cooking", s => s.Window = TimeWindow.SevenDays);
        Check(cook7.Windowed && cook7.AboutNumbers == null && Incomplete(cook7) == 0 && !PanelModel.Content(cook7).Any(b => b.Kind == "ladders") &&
              PanelModel.SkillOf(PanelModel.Content(cook7).First(b => b.Kind == "hero")) != null,
              "cooking 0.7, 7 days: no box, no \"Earlier counts\" line, the skill beside the hero, no skill strip (rules W.1, K.5)");
        var cookEdda = Show(edda, "cooking");
        Check(cookEdda.Recorded && cookEdda.AboutNumbers == null && Incomplete(cookEdda) == 0 && PanelModel.PlateOf(cookEdda).Text == null && cookEdda.StripNote.StartsWith("Edda") && !cookEdda.StripNote.Contains("since install"),
              "cooking 0.7, Edda's book: the game's count (0.8 layout D+: \"Earlier counts may be incomplete.\" lives only in your About these numbers); whose copy and when at the strip's right end (0.8 layout D+), no box");

        // ---------- Building (rule B, C): the game's count is the hero, the repairs their own section, the box names the split ----------
        var twin = DeedsTwinTests.Twin(input);
        var building = Show(twin, "building");
        var bItems = PanelModel.Content(building);
        var bhero = bItems.First(b => b.Kind == "hero");
        var repairs = bItems.FirstOrDefault(b => b.Kind == "section" && b.Title == "Pieces repaired");
        Check(building.Recorded && !bItems.Any(b => b.Kind == "zone") && bhero.Value == "866" && bhero.Title == "pieces built" && bhero.Src == PanelModel.SrcCharacter && bhero.Note == null &&
              repairs != null && repairs.Value == "9" && repairs.Src == PanelModel.SrcPc && repairs.RecordedFrom != null && repairs.RecordedFrom.StartsWith("Recorded from ") &&
              !PanelModel.AllText(building).Any(t => t.Contains("since install") || t.Contains("pieces built since")),
              "building 0.7: 866 pieces built, the game's whole count (rule B: no twin, no since-install hero); \"Pieces repaired\" 9 its own section \"Recorded from ... · this PC\" (rule C)");
        var buildingBox = Show(twin, "building", numbersOn).AboutNumbers;
        Check(buildingBox != null && buildingBox.Items[0].Text == "The game's own count of placements: 740." && buildingBox.Items[1].Text.StartsWith("Still the game's own count: 126 more.") &&
              buildingBox.Items[2].Text.Contains("Repairs are counted by Hearthwoven from "),
              "building 0.7: About these numbers: before 1 October the game's own 740 placements, still the game's own count 126 more, repairs counted by Hearthwoven from the install");
        var buildingEdda = Show(edda, "building");
        Check(buildingEdda.AboutNumbers == null && !PanelModel.AllText(buildingEdda).Any(t => t.Contains("you") && t.Contains("counts again")),
              "building 0.7, Edda's book: no box, the placement note says nothing to you");

        // ---------- Groundwork (rule B): the game's strokes, the box with its split ----------
        var ground = Show(twin, "groundwork");
        var gItems = PanelModel.Content(ground);
        var gh = gItems.First(b => b.Kind == "hero");
        Check(ground.Recorded && !gItems.Any(b => b.Kind == "zone" || b.Kind == "ranking") && PanelModel.ParseCount(gh.Value) == 1023 && gh.Title == "groundwork strokes" && gh.Src == PanelModel.SrcCharacter && gh.Note == null &&
              gItems.Single(b => b.Kind == "composition").Items.All(p => p.Fraction2 == 0),
              "groundwork 0.7: 1 023 strokes, the game's whole count (rule B), the bar its sums with no faded share and no zone or ranking since install");
        var groundBox = Show(twin, "groundwork", numbersOn).AboutNumbers;
        Check(groundBox != null && groundBox.Items[0].Text.StartsWith("The game's own count of strokes: 940.") && groundBox.Items[1].Text == "Still the game's own count: 83 more." &&
              groundBox.Items[2].Text.StartsWith("A stroke is one use of the hoe"),
              "groundwork 0.7: About these numbers: the game's own 940 strokes before the split, still the game's own 83 more, what a stroke is");

        // ---------- Crafting (rules B, K, E): gear and upgrades are the game's counts, the skill beside the hero, the box names both dates when they differ ----------
        var crafting = Show(twin, "crafting");
        var cItems = PanelModel.Content(crafting);
        var cHero = cItems.First(b => b.Kind == "hero");
        Check(crafting.Recorded && !cItems.Any(b => b.Kind == "zone") && cHero.Value == "20" && cHero.Title == "gear crafted" && cHero.Items.Any(n => n.Kind == "number" && n.Value == "11") && !cItems.Any(b => b.Kind == "note" && b.Text == PanelModel.FadedKeyTwin),
              "crafting 0.7: 20 gear crafted and 11 upgrades, the game's counts (rule B), no since-install key");
        var realCraft = realBook == null ? null : PanelModel.Content(Show(realBook, "crafting")).First(b => b.Kind == "hero");
        Check(realCraft != null && PanelModel.SkillOf(realCraft)?.Id == "Skills/Crafting" && PanelModel.Content(Show(realBook, "crafting")).First(b => b.Kind == "hero").Value == "57",
              "crafting 0.7, the real book: 57 gear crafted, the Crafting skill beside the hero (rule K)");
        var craftBox = Show(twin, "crafting", numbersOn).AboutNumbers;
        Check(craftBox != null && craftBox.Items[0].Text == "The game's own count: 14 pieces of gear, 8 upgrades." && craftBox.Items[1].Text.StartsWith("Still the game's own count: 6 more gear and 3 more upgrades.") &&
              craftBox.Items[2].Text == "A batch (a stack of arrows) counts once, as the game books it.",
              "crafting 0.7: About these numbers: the game's own 14 gear and 8 upgrades before the split, still the game's own 6 more gear and 3 more upgrades after it");
        var craftEdda = Show(edda, "crafting");
        Check(craftEdda.AboutNumbers == null && !PanelModel.AllText(craftEdda).Any(t => t.Contains("since install")),
              "crafting 0.7, Edda's book: no box, nothing says \"since install\"");

        return fails;
    }
}
