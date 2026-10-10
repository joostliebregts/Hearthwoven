// 0.7 redesign, page group G3 Deeds field: Farming, Fishing, Taming
// (work/hearthwoven-0.7/REDESIGN-RULES.md part 5). One test per page that captures the page's rule
// (pattern: RecordedGatherTests.Woodcutting). Registered in Program.cs; never edit Program.cs from a page agent.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RecordedFieldTests
{
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));

    public static int Run(PanelInput input, PanelInput edda, DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Show(PanelInput i, string page, Action<PanelState> more = null) { var s = new PanelState { Page = { [Chapter.Deeds] = page } }; more?.Invoke(s); return PanelModel.Build(i, s); }
        int Incomplete(PanelView v) => PanelModel.AllText(v).Count(t => t == PanelModel.EarlierIncomplete);
        Block After(List<Block> items, string title) { int i = items.FindIndex(b => b.Kind == "section" && b.Title == title); return i < 0 || i + 1 >= items.Count ? null : items[i + 1]; }
        // the game's counters with their baselines (Twins: planted since the first run, 46 fish caught since, 42 crops picked since)
        var twin = DeedsTwinTests.Twin(input);

        // ---------- Farming (rule A planted, rule B picked, rule K skill, the box with its split dates) ----------
        var layeredIn = DeedsTests.Layered(input);   // 88 planted at the first run (faded), 191 counted since (solid); picked is the game's counter
        var farm = Show(layeredIn, "farming");
        var fitems = PanelModel.PlateOf(farm).Items;
        var fhero = fitems.First(b => b.Kind == "hero");
        var cropGrid = fitems.Single(b => b.Kind == "cropgrid");
        var barley = cropGrid.Items.Single(i => i.Id == "Barley");
        var alsoPlanted = After(fitems, "Also planted");
        double plantedAll = PanelModel.ParseCount(fhero.Value);
        double plantedByTile = cropGrid.Items.Where(i => i.Id != PanelModel.OtherPlantsId).Sum(i => PanelModel.ParseCount(i.Items[0].Value));
        double plantedAlso = (alsoPlanted?.Items ?? new List<Block>()).Sum(i => PanelModel.ParseCount(i.Value));
#pragma warning disable CS0618   // the retired layer fields: asserted empty
        Check(farm.Recorded && !fitems.Any(b => b.Kind == "zone" || b.Kind == "ladders") && fhero.Title == "planted" && fhero.Value == "279" && fhero.Src == PanelModel.SrcCharacter && fhero.Note == null && PanelModel.AboutText(farm).Contains(PanelModel.EarlierIncomplete) && fhero.Items.First(n => n.Kind == "number").Value == "312" && fhero.Items.First(n => n.Kind == "number").Title == "picked" &&
              fhero.Items.First(n => n.Kind == "number").Src == PanelModel.SrcCharacter && Incomplete(farm) == 1,
              "farming 0.7: planted is one number, 279 (88 counted at the first run + 191 since), \"Earlier counts may be incomplete.\" in About these numbers (0.8 layout D+); picked 312 is the game's own counter");
        Check(plantedAll == plantedByTile + plantedAlso,
              "farming 0.7: the planted total is the crop tiles' planted plus the plantings that are no crop (" + plantedByTile + " + " + plantedAlso + " = " + plantedAll + "), with no faded or solid part anywhere");
#pragma warning restore CS0618
        Check(barley.Items.Count == 2 && barley.Items[0].Value == "187" && barley.Items.All(p => p.Fraction2 == 0) && cropGrid.Note == null,
              "farming 0.7: a crop tile holds the planted sum (barley 187 = 16 + 171) and the picked game counter, no faded share and no legend key");
        // the box on the twin (dated baselines): before the planting's date the game's own count, from it what Hearthwoven counted, and picked is the game's own count
        var farmBox = PanelModel.PlateOf(Show(twin, "farming", s => s.ShowNumbers = true)).Items.Single(b => b.Kind == "aboutnumbers");
        Check(farmBox.Items.Count >= 3 && farmBox.Items[0].Title.StartsWith("Before ") && farmBox.Items[0].Text.StartsWith("The game's own count: ") && farmBox.Items[0].Text.Contains(" plants. It booked one planting per click") &&
              farmBox.Items[1].Title.StartsWith("From ") && farmBox.Items[1].Text.Contains("Hearthwoven counted every plant you put in the ground, ") &&
              farmBox.Items[1].Text.ToLowerInvariant().Contains("picked is still the game's own count, 42 more.") && farmBox.Items[2].Title == PanelModel.DetailsLabel,
              "farming 0.7: About these numbers: before the planting's date the game's own plants, from then what Hearthwoven counted and picked still the game's own count (42 more); Y opens it");
        // the skill beside the hero: the real book holds all six skills (the sample world has no Farming level)
        var realBook = RecordedTests.RealBook();
        if (realBook == null) Check(false, "farming 0.7, the real book: the fixture loads");
        else
        {
            var bh = PanelModel.Content(Show(realBook, "farming")).First(b => b.Kind == "hero");
            Check(bh.Value == "162" && PanelModel.SkillOf(bh)?.Id == "Skills/Farming" && PanelModel.SkillOf(bh)?.Value != null,
                  "farming 0.7, the real book: the hero's planted is 162 with the Farming skill in its row (rule K)");
        }
        var week = Show(PanelSample.Full(now), "farming", s => s.Window = TimeWindow.SevenDays);
        Check(week.Recorded && week.Windowed && week.AboutNumbers == null && Incomplete(week) == 0 && !Every(PanelModel.Content(week)).Any(b => !string.IsNullOrEmpty(b.RecordedFrom)),
              "farming 0.7, 7 days: the window's numbers; no label, no \"Earlier counts\", no box (rules W.1, A.6)");
        var eddaFarm = Show(edda, "farming");
        Check(eddaFarm.Recorded && eddaFarm.AboutNumbers == null && !(eddaFarm.Scope ?? "").Contains("since install") && (eddaFarm.Scope ?? "").Contains("Edda"),
              "farming 0.7, Edda's book: her scope names her and says no since install; no box (a fellow's copy has no dates)");

        // ---------- Fishing (rule B counters, the skill beside the hero, rule A fish picked up, the box) ----------
        var fishIn = DeedsTests.Rich(input);
        var fishItems = PanelModel.PlateOf(Show(fishIn, "fishing")).Items;
        var fishHero = fishItems.First(b => b.Kind == "hero");
        Check(!fishItems.Any(b => b.Kind == "zone" || b.Kind == "ladders") && fishHero.Title == "fish caught" && fishHero.Value == "251" && fishHero.Src == PanelModel.SrcCharacter &&
              fishHero.Items.First(n => n.Kind == "number").Title == "hooked" && PanelModel.SkillOf(fishHero)?.Id == "Skills/Fishing" && PanelModel.SkillOf(fishHero)?.Value == "15",
              "fishing 0.7: the hero is the game's count of fish caught (251) and hooked, the Fishing skill (level 15) in its row; no strip, no zone");
        var fishBox = PanelModel.PlateOf(Show(twin, "fishing", s => s.ShowNumbers = true)).Items.Single(b => b.Kind == "aboutnumbers");
        Check(fishBox.Items.Count == 3 && fishBox.Items[0].Text == "The game's own count: 205 fish caught." && fishBox.Items[1].Text == "Still the game's own count: 46 more fish caught." &&
              fishBox.Items[2].Text.StartsWith("Only a fish reeled in counts as caught."),
              "fishing 0.7: About these numbers: the game's own catches before the split date, the rest still the game's own count (a complete counter: no Hearthwoven number)");
        // fish picked up (rule A, hard case 10): 3 counted by the game before Hearthwoven first ran + 1 counted since = 4 for fish5; fish1 and raw fish meat are not in its baseline
        var ev = new SessionEvents(); SessionEvents.Add(ev.PickedUp, "$animal_fish5", 1f);
        var heldIn = new PanelInput { PlayerName = "Astrid", IsSelf = true, NowUtc = input.NowUtc, Character = new Dictionary<string, float>(),
                                      ItemsPickedUp = new Dictionary<string, float> { ["$animal_fish5"] = 4, ["$animal_fish1"] = 1, ["$item_fish_raw"] = 57 },
                                      Baseline = new Dictionary<string, Dictionary<string, float>> { ["pickedUp"] = new Dictionary<string, float> { ["$animal_fish5"] = 3 } },
                                      Harvested = new Dictionary<string, float>(), Events = ev };
        var held = PanelModel.PlateOf(Show(heldIn, "fishing")).Items;
        var heldSection = held.FirstOrDefault(b => b.Kind == "section" && b.Title == PanelModel.FishHeldTitle);
        var heldGrid = held.FirstOrDefault(b => b.Kind == "itemgrid" && b.Items.Any(i => i.Id == "$animal_fish5"));
        Check(heldSection != null && heldSection.Value == "4" && heldSection.Text == null && heldSection.Src == PanelModel.SrcCharacter &&
              heldGrid != null && heldGrid.Items.Count == 1 && heldGrid.Items[0].Value == "4" && !heldGrid.Items.Any(i => i.Id == "$item_fish_raw"),
              "fishing 0.7, fish picked up (rule A, hard case 10): 3 before the first run + 1 since = 4, its own section (0.8 layout D+: \"Earlier counts may be incomplete.\" in About these numbers); raw fish meat is not a fish");
        var fishWeek = Show(PanelSample.Full(now), "fishing", s => s.Window = TimeWindow.SevenDays);
        Check(fishWeek.Windowed && fishWeek.AboutNumbers == null && Incomplete(fishWeek) == 0 && !Every(PanelModel.Content(fishWeek)).Any(b => !string.IsNullOrEmpty(b.RecordedFrom)),
              "fishing 0.7, 7 days: no label, no \"Earlier counts\", no box (rules W.1)");

        // ---------- Taming (rule B and rule D: tamed is the owner's and carries its note; rule C6 groups with their own dates; rule S server label; the box) ----------
        var taming = Show(twin, "taming");
        var tItems = PanelModel.PlateOf(taming).Items;
        var tamingBox = PanelModel.PlateOf(Show(twin, "taming", s => s.ShowNumbers = true)).Items.Single(b => b.Kind == "aboutnumbers");
        Check(taming.Recorded && !tItems.Any(b => b.Kind == "hero" && PanelModel.SkillOf(b) != null) && !Every(tItems).Any(b => b.Kind == "ladders") &&
              tamingBox.Items.Count >= 3 && tamingBox.Items[0].Text.StartsWith("The game's own count of tames, pets and commands.") && tamingBox.Items[2].Text == "Born near you is counted by the server; near is not bred.",
              "taming 0.7: no skill (the game has none for taming), no zone; About these numbers says the game's own tames, pets and commands, and that near is not bred");
        var dated = PanelSample.Full(now);   // the sample world: installed 3 October, its born and led groups dated by their own starts
        var born = Every(PanelModel.PlateOf(Show(dated, "taming")).Items).FirstOrDefault(b => b.Kind == "section" && b.Title == PanelModel.BornTitle);
        Check(born != null && born.From.HasValue && (born.RecordedFrom ?? "").StartsWith("Recorded from ") && born.RecordedFrom.EndsWith(" · this PC"),
              "taming 0.7: born in your care is class C6: its own start date, \"Recorded from ... · this PC\" on its heading");
        var bornNear = Every(PanelModel.PlateOf(Show(dated, "taming")).Items).FirstOrDefault(b => b.Kind == "section" && b.Title == PanelModel.BornNearTitle);
        Check(bornNear == null || (bornNear.RecordedFrom ?? "").StartsWith("Recorded by the server"),
              "taming 0.7: born near you is class S: \"Recorded by the server\" on its heading, never \"this PC\"");
        var led = Every(PanelModel.PlateOf(Show(dated, "taming")).Items).FirstOrDefault(b => b.Kind == "section" && b.Title == PanelModel.LedTitle);
        Check(led == null || ((led.RecordedFrom ?? "").StartsWith("Recorded from ") && led.From.HasValue),
              "taming 0.7: animals led is class C6 with its own start date on its heading");
        var eddaTaming = Show(edda, "taming");
        Check(!(eddaTaming.Scope ?? "").Contains("since install") && eddaTaming.AboutNumbers == null && !PanelModel.AllText(eddaTaming).Any(t => t.Contains("since install")),
              "taming 0.7, Edda's book: her scope without since install, no box");
        return fails;
    }
}
