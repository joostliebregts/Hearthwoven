// Tests for the Deeds chapter pages (src/Panel/Chapters/DeedsModel.cs): Cooking, Building, Crafting, Farming, Fishing,
// Taming in the approved round-2/4 forms. Called from Program.cs with the sample evening; Rich() adds the counters the
// sample leaves at zero (fish, taming, crops) for these pages and their previews.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Hearthwoven;
using Hearthwoven.Panel;

static class DeedsTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelView Show(PanelInput i, string page) => PanelModel.Build(i, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = page } });
    static Block Find(PanelView v, string kind, Func<Block, bool> where = null) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind && (where == null || where(b)));
    static Block After(PanelView v, string section) => PanelModel.Content(v).SkipWhile(b => !(b.Kind == "section" && b.Title == section)).Skip(1).FirstOrDefault(b => b.Kind != "sort");   // the list under a heading, past its sort control (SortModel.cs)
    static Block Head(PanelView v, string section) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "section" && b.Title == section);

    internal static readonly Dictionary<string, string> Names = PanelSample.DeedsNames;   // src/Panel/PanelSample.cs

    /// <summary>The sample evening with the counters these pages read: fish (hooked, caught, lost, quality tiers, per kind),
    /// taming, crops planted and harvested (CropOf as GameData derives it from Plant -> Pickable), gear item types.</summary>
    public static PanelInput Rich(PanelInput s) => PanelSample.DeedsRich(s);   // src/Panel/PanelSample.cs

    /// <summary>The rich sample at the size of a long-played character (Joost's live Farming, 2026-10-08: 6,399 crops,
    /// 2,478 mushrooms): for the preview that checks large numbers and bars fit.</summary>
    public static PanelInput Big(PanelInput s)
    {
        var b = Rich(s);
        foreach (var kv in new Dictionary<string, float> { ["HarvestCrop"] = 6399, ["HarvestMushroom"] = 2478, ["HarvestBerry"] = 608, ["BeesHarvested"] = 56, ["SapHarvested"] = 3 }) b.Character[kv.Key] = kv.Value;
        b.PiecesPlaced = new Dictionary<string, float>(b.PiecesPlaced) { ["$piece_sapling_barley"] = 3120, ["$piece_sapling_turnip"] = 1406, ["$piece_sapling_carrot"] = 2210 };
        b.Harvested = new Dictionary<string, float>(b.Harvested) { ["Barley"] = 3960, ["Turnip"] = 1290, ["Carrot"] = 1041 };   // 6,291 of the 6,399 crops picked
        return b;
    }

    /// <summary>
    /// Joost's live case (2026-10-08, "Flax 16 planted / 1\u00A0694 harvested"), on the rich sample: Hearthwoven first ran when the
    /// game's planted counter stood at 16 barley, 60 carrot, 12 beech (the baseline, faded); since then it counted every
    /// plant: a PlantEasily grid of 171 barley and 20 turnips set in one frame (solid). The game's counter has since grown
    /// to 72 barley / 64 turnip (one per click): never added.
    /// </summary>
    public static PanelInput Layered(PanelInput s)
    {
        var l = Rich(s);
        var ev = SessionEvents.Sum(s.Events);
        SessionEvents.Add(ev.Planted, "$piece_sapling_barley", 171);
        for (int i = 0; i < 20; i++) SessionEvents.CountPlanting(ev.Planted, "$piece_sapling_turnip", true, 0, 5L, 5L);
        l.Events = ev;
        l.Baseline = new Dictionary<string, Dictionary<string, float>>
        {
            [PanelModel.PlacedBaseline] = new Dictionary<string, float> { ["$piece_sapling_barley"] = 16, ["$piece_sapling_carrot"] = 60, ["$piece_sapling_beech"] = 12, ["$piece_woodwall"] = 300 },
            ["pickedUp"] = new Dictionary<string, float> { ["$item_wood"] = 900 },
        };
        return l;
    }

    /// <summary>
    /// Cooking counted on your own PC (SOURCES.md: the game books a cooking station's dish to the station's OWNER), on the
    /// rich sample: Hearthwoven first ran when the game's craft counter stood at 14 bread, 118 carrot soup, 28 fish wraps
    /// (the baseline, faded); since then Rowan made 12 bread (a fellow's oven: the game booked it to them) and took 9 cooked
    /// meat off Edda's grill (also booked to Edda), and an iron sword (gear, not a dish). The game counter has since grown to
    /// 20 bread (Tor's loaves from Rowan's oven): never added. Fellows: Edda (2 Queen's Jam "made by Rowan", which Rowan
    /// never made) and Tor (5 cooked meat by Rowan, and 3 grilled neck tails cooked by Tor on Rowan's grill).
    /// </summary>
    public static PanelInput Cooked(PanelInput s, PanelInput edda)
    {
        var c = Rich(s);
        var ev = SessionEvents.Sum(s.Events);
        for (int i = 0; i < 12; i++) SessionEvents.CountMade(ev.Made, "$item_bread", 1, false, true);
        SessionEvents.CountTakenOff(ev.Made, "$item_cookedmeat", false, new object[] { null, 7 });
        SessionEvents.CountTakenOff(ev.Made, "$item_cookedmeat", false, new object[] { null, 2 });
        SessionEvents.CountMade(ev.Made, "$item_sword_iron", 1, false, true);
        c.Events = ev;
        c.Baseline = new Dictionary<string, Dictionary<string, float>>
        {
            [PanelModel.CraftedBaseline] = new Dictionary<string, float> { ["$item_bread"] = 14, ["$item_carrotsoup"] = 118, ["$item_fishwraps"] = 28, ["$item_sword_iron"] = 2 },
        };
        c.StationDish = t => t == "$item_bread" || t == "$item_cookedmeat" || t == "$item_necktailgrilled";
        var kind = c.ItemKind; c.ItemKind = t => t == "$item_cookedmeat" || t == "$item_necktailgrilled" || t == "$item_queensjam" ? "food" : kind(t);
        var name = c.DisplayName;
        c.DisplayName = t => t == "$item_cookedmeat" || t == "CookedMeat" ? "Cooked Meat" : t == "NeckTailGrilled" ? "Grilled Neck Tail" : t == "QueensJam" ? "Queens Jam" : name(t);
        c.ItemToken = p => p == "CookedMeat" ? "$item_cookedmeat" : p == "NeckTailGrilled" ? "$item_necktailgrilled" : p == "QueensJam" ? "$item_queensjam" : p == "Bread" ? "$item_bread" : null;
        var torEv = new SessionEvents();
        SessionEvents.Add(torEv.AteFoodMadeBy, "Rowan|CookedMeat", 5); SessionEvents.Add(torEv.AteFoodMadeBy, "Rowan|NeckTailGrilled", 3);
        c.Fellows = new List<PanelInput> { edda, new PanelInput { PlayerName = "Tor", IsSelf = false, Events = torEv } };
        return c;
    }

    public static int Run(PanelInput input, PanelInput edda)
    {
        fails = 0;
        var keep = input.Fellows; var keepE = edda.Fellows;
        input.Fellows = new List<PanelInput> { edda }; edda.Fellows = new List<PanelInput> { input };
        var rich = Rich(input);

        // ---------- Cooking: dishes cooked, dishes by kind (composition), the kitchen ledger, who enjoyed your food (a bar per fellow) ----------
        var cooking = Show(rich, "cooking");
        var hero = Find(cooking, "hero");
        Check(cooking.Heading == "Cooking" && hero?.Value == "166" && hero.Title == "dishes cooked" && hero.Src == "character" && PanelModel.PlateOf(cooking)?.Icon == "title:cook", "deeds cooking: on the plate, the hero number is the game's dishes cooked (CraftFood + CraftGrill)");
        Check(Find(cooking, "columns") == null && Head(cooking, "Who enjoyed your food")?.Value == "2", "deeds cooking: the dishes and who enjoyed them, each the full width, the total on the section head");
        var dishes = Find(cooking, "composition");
        Check(dishes?.Title == "Dishes by kind" && dishes.Value == "166" && dishes.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Carrot Soup=118", "Fish Wraps=28", "Bread=20" }) &&
              dishes.Items.All(i => i.Icon.StartsWith("item:") && i.Src == "character") && Math.Abs(dishes.Items.Sum(i => i.Fraction) - 1) < 1e-4 && dishes.Note == null,
              "deeds cooking: the dishes by kind as one composition bar with the game's pictures, most first, adding up to the hero (118 + 28 + 20 = 166)");
        var ledger = After(cooking, "Where it was cooked");
        Check(ledger?.Kind == "ledger" && ledger.Src == "character" && ledger.Items.Select(r => r.Title + "=" + r.Value + "/" + string.Join(",", r.Items.Select(c => c.Title + " " + c.Value))).SequenceEqual(new[] {
                  "Cauldron and prep table=146/Carrot Soup 118,Fish Wraps 28", "Cooking stations and oven=20/Bread 20" }) && ledger.Items[0].Note == "dishes" && ledger.Items[1].Text == "booked to whoever takes it off" &&
              ledger.Items.All(r => r.Items.All(c => c.Kind == "chip" && c.Icon.StartsWith("item:"))) && ledger.Items.Sum(r => double.Parse(r.Value)) == double.Parse(dishes.Value) && ledger.Note == PanelModel.StationsNote,
              "deeds cooking: the kitchen ledger, a row per kind of cooking with its dishes as chips and its total, the rows adding up to the bar, one line on the stations not counted apart");
        Check(PanelModel.AllText(cooking).Count(t => t.Contains("not counted apart")) == 1, "deeds cooking: the stations line is said once");
        var chefs = Rich(input); chefs.StationDish = null;
        Check(After(Show(chefs, "cooking"), "Where it was cooked").Items.Single().Title == "Cauldron and prep table" && After(Show(chefs, "cooking"), "Where it was cooked").Note == null,
              "deeds cooking: without station data every dish sits in the cauldron row and the stations line is left out");
        var many = Rich(input); var lots = new Dictionary<string, float>(); var kinds2 = new Dictionary<string, string>();
        for (int k = 0; k < 30; k++) { lots["$item_dish" + k] = 100 - k; kinds2["$item_dish" + k] = "food"; }
        many.ItemsCrafted = lots; many.ItemKind = t => kinds2.TryGetValue(t, out var kk) ? kk : null;
        var manyBar = Find(Show(many, "cooking"), "composition");
        Check(manyBar.Items.Count == PanelModel.BarMaxParts && manyBar.Items.Last().Id == PanelModel.FoldId && manyBar.Items.Last().Title == "Other (23 kinds)" && manyBar.Items.Last().Items.Count == 23 &&
              After(Show(many, "cooking"), "Where it was cooked").Items.Single().Items.Count == 30,
              "deeds cooking: the ledger keeps every dish kind (the page scrolls); the bar shows the seven largest and folds the rest into \"Other (23 kinds)\" (0.6.5: at most eight parts the eye can tell apart)");
        var manyHero = Find(Show(many, "cooking"), "hero");
        // 0.7 rule A.3: no baseline (the sample) puts "Earlier counts may be incomplete." in the hero's own slot; the discrepancy line stands under the bar
        var manyNote = PanelModel.Content(Show(many, "cooking")).FirstOrDefault(b => b.Kind == "note" && (b.Text ?? "").StartsWith("the dishes add up to "));
        Check(hero.Note == null && PanelModel.AboutText(cooking).Contains(PanelModel.EarlierIncomplete) && hero.Src == "character" && manyHero.Note == null && manyNote != null && manyNote.Text.EndsWith("the headline is your character's own count"),
              "deeds cooking: the sample's dishes (118 + 28 + 20) add up to the headline (146 + 20 grill = 166) with the one line on the earlier counts (in About these numbers, 0.8 layout D+); when the game's counters and the per-dish counts differ, a line under the bar says so and names its source (polish-06, rule A.3)");
        var axis = After(cooking, "Who enjoyed your food");
        var row = axis?.Items.Single();
        Check(axis?.Kind == "ranking" && axis.Src == "fellows" && row.Id == "Edda" && row.Icon == "person:Edda" && row.Title == "Edda" && row.Value == "2" && Math.Abs(row.Fraction - 1f) < 1e-6 && row.Colour == null && axis.Items.Count == 1,
              "deeds cooking: who enjoyed your food, one bar per fellow (the UI paints it in their player colour), the count at its end");
        Check(!PanelModel.Content(cooking).Any(b => b.Kind == "tiles" || b.Kind == "rows"), "deeds cooking: no dots or rows under the dishes, no doubled eater list");
        // feast servings ride on the same bar, crimson part (Fraction2), as their own chip with the feast's game sprite
        var feaster = new SessionEvents(); SessionEvents.Add(feaster.AteFoodMadeBy, "Rowan|Bread", 3); SessionEvents.Add(feaster.AteFromFeastOf, "5|FeastMeadows", 1);
        var tor = new PanelInput { PlayerName = "Tor", IsSelf = false, Events = feaster };
        rich.PlayerId = 5; rich.Fellows = new List<PanelInput> { edda, tor };
        var axis2 = PanelModel.FoodAxis(rich);
        var torRow = axis2.Items.Single(r => r.Id == "Tor");
        Check(axis2.Items.Select(r => r.Id).SequenceEqual(new[] { "Edda", "Tor" }) && torRow.Value == "4" && Math.Abs(torRow.Fraction2 - 0.25f) < 1e-6 &&
              torRow.Items.Select(c => c.Icon + " " + c.Value).SequenceEqual(new[] { "item:Bread × 3", "item:FeastMeadows × 1" }), "deeds cooking: eaters alphabetical, feast servings as the crimson share and a chip of their own");
        rich.Fellows = input.Fellows; rich.PlayerId = 0;
        var noFellows = Rich(input); noFellows.Fellows = null;
        Check(Find(Show(noFellows, "cooking"), "axis") == null && !PanelModel.Content(Show(noFellows, "cooking")).Any(b => b.Kind == "ranking") && Find(Show(noFellows, "cooking"), "note") != null, "deeds cooking: without fellow records no bars, one line saying when they appear");

        // ---------- Cooking counted on your own PC: the game counter at first run faded + every dish you made since solid ----------
        var cookedIn = Cooked(input, edda);
        var cooked = Show(cookedIn, "cooking");
        var ch = Find(cooked, "hero");
        Check(ch.Value == "181" && ch.Src == "character" && ch.Note == null && PanelModel.AboutText(cooked).Contains(PanelModel.EarlierIncomplete) && ch.Title == "dishes cooked",
              "deeds cooking layered: hero = 181 dishes cooked (rule A: 160 the game counted before the first run + 21 made since, 12 bread and 9 cooked meat off a fellow's grill), the character's whole count, \"Earlier counts may be incomplete.\" in About these numbers (0.8 layout D+); the game counter's later growth (bread 20) is not added: " + ch.Value + " " + ch.Note);
        var cg = Find(cooked, "composition");
        Check(cg.Note == null && cg.Value == "181" && cg.Items.Select(i => i.Title + " " + i.Value + " " + i.Src).SequenceEqual(new[] { "Carrot Soup 118 character", "Fish Wraps 28 character", "Bread 26 character", "Cooked Meat 9 character" }) &&
              cg.Items.All(i => i.Fraction2 == 0),
              "deeds cooking layered: the bar per dish, each kind its sum (bread 26 = 14 + 12), no faded share and no key (rule A.2); cooked meat taken off a fellow's grill counts for you (9); no gear: " +
              string.Join(" | ", cg.Items.Select(i => i.Title + " " + i.Value + " " + i.Fraction2 + " " + i.Src)));
        var cl = After(cooked, "Where it was cooked");
        Check(cl.Items.Select(r => r.Title + "=" + r.Value + "/" + string.Join(",", r.Items.Select(c => c.Title + " " + c.Value))).SequenceEqual(new[] { "Cauldron and prep table=146/Carrot Soup 118,Fish Wraps 28", "Cooking stations and oven=35/Bread 26,Cooked Meat 9" }),
              "deeds cooking layered: the ledger's rows add up to the hero (146 + 35 = 181), the stations row holds what the stations hand out (oven bread, cooked meat)");
        var cax = After(cooked, "Who enjoyed your food");
        Check(cax?.Kind == "ranking" && cax.Items.Count == 1 && cax.Items[0].Id == "Tor" && cax.Items[0].Value == "5" && cax.Items[0].Icon == "person:Tor" && Head(cooked, "Who enjoyed your food").Value == "5",
              "deeds cooking layered: who enjoyed your food counts only dishes you made (5 cooked meat); neck tails Tor grilled on your grill and jam you never made are not yours");
        var ccard = Find(PanelModel.Build(cookedIn, new PanelState { Chapter = Chapter.Deeds }), "cards").Items.Single(c => c.Title == "Hearth Cook");
        Check(ccard.Value == "181" && ccard.Items[0].Value == "5", "deeds overview: the Hearth Cook card has the same dishes cooked and enjoyed as the Cooking page");
        var cookCopy = Cooked(input, edda); cookCopy.IsSelf = false;
        Check(Find(Show(cookCopy, "cooking"), "hero").Value == "166" && PanelModel.MadeOf(cookCopy) == null,
              "deeds cooking layered: a fellow's copy shows the game's counters, no layers, no cap");
        var fresh = Cooked(input, edda); fresh.Baseline[PanelModel.CraftedBaseline] = new Dictionary<string, float>();
        var fh = Find(Show(fresh, "cooking"), "hero");
        Check(fh.Value == "21" && fh.Src == "character" && fh.Note == null, "deeds cooking layered: a character that started with Hearthwoven: its 21 dishes are the sum, no earlier part, no note (rule A)");
        var olderInstall = Cooked(input, edda); olderInstall.ExactAtBaseline = new Dictionary<string, Dictionary<string, float>> { [PanelModel.CraftedBaseline] = new Dictionary<string, float> { ["$item_bread"] = 2 } };
        Check(Find(Show(olderInstall, "cooking"), "hero").Value == "179", "deeds cooking layered: what was counted exactly when the baseline was taken is not counted twice (160 + 19 = 179)");
        // the cap shared out in proportion: two eaters of 3 bread each, the maker made 4 (2 + 2) or 5 (3 + 2, the tie by name)
        var two = new List<(string who, Dictionary<string, double> dishes)> { ("Ana", new Dictionary<string, double> { ["Bread"] = 3 }), ("Bo", new Dictionary<string, double> { ["Bread"] = 3, ["Jam"] = 1 }) };
        string Shares(Func<string, double?> m) => string.Join(" ", PanelModel.CapToMade(two, m).Select(e => e.who + ":" + string.Join(",", e.dishes.OrderBy(kv => kv.Key).Select(kv => kv.Key + kv.Value))));
        Check(Shares(d => d == "Bread" ? 4 : (double?)null) == "Ana:Bread2 Bo:Bread2,Jam1" && Shares(d => d == "Bread" ? 5 : (double?)null) == "Ana:Bread3 Bo:Bread2,Jam1" &&
              Shares(d => 0) == "Ana: Bo:" && Shares(null) == "Ana:Bread3 Bo:Bread3,Jam1" && Shares(d => 100) == "Ana:Bread3 Bo:Bread3,Jam1",
              "deeds cooking: servings never exceed what the maker made of a dish, shared out in proportion; unknown (no count) leaves them as recorded: " + Shares(d => d == "Bread" ? 5 : (double?)null));

        // ---------- Building ----------
        var building = Show(rich, "building");
        var pieces = After(building, "Every piece");
        Check(Find(building, "hero").Value == "866" && pieces.Kind == "itemgrid" && pieces.Items.Select(i => i.Icon).SequenceEqual(new[] { "piece:$piece_woodwall", "piece:$piece_woodfloor2x2", "piece:$piece_sharpstakes" }),
              "deeds building: pieces built as an item grid with the piece pictures, the full list, the total in its head");
        var ground = Find(Show(rich, "groundwork"), "composition");
        Check(ground.Items.All(i => i.Pattern != null && i.Pattern.StartsWith("vocab:grain-ground-")), "deeds building: groundwork with a pattern per kind");
        Check(!PanelModel.AllText(building).Any(t => t.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0), "deeds building: no material bar (the mod has no material data)");
        var repairs = After(building, "Pieces repaired"); var repairHead = Head(building, "Pieces repaired");
        Check(repairs != null && repairHead.Value == "9" && repairs.Src == "pc" && (repairHead.RecordedFrom ?? "").StartsWith("Recorded") && !PanelModel.AllText(building).Any(t => t.Contains("since install")), "deeds building: repairs counted on this PC are their own section, \"Recorded ...\"; no since install anywhere on the page (rule C)");
        // 0.8 (v08-salvage): materials picked up from pieces that came down, their own section; Mining's brought in never reads them
        var hall = PanelSample.Full(input.NowUtc); var hallBuilding = Show(hall, "building");
        var back = After(hallBuilding, PanelModel.RecoveredTitle); var backHead = Head(hallBuilding, PanelModel.RecoveredTitle);
        var oneKind = PanelSample.Full(input.NowUtc); oneKind.Events.Recovered.Clear(); oneKind.Events.Recovered["$item_stone"] = 500;
        var oneBack = After(Show(oneKind, "building"), PanelModel.RecoveredTitle);
        Check(backHead?.Value == "80" && back?.Kind == "composition" && back.Value == null && back.Items.Select(i => i.Id + "=" + i.Value).SequenceEqual(new[] { "$item_wood=48", "$item_stone=24", "$item_roundlog=8" }) &&
              (backHead.RecordedFrom ?? "").StartsWith("Recorded from") && oneBack?.Kind == "itemgrid" && oneBack.Items.Single().Value == "500" &&
              PanelModel.BroughtInTotal(oneKind, "mining") == PanelModel.BroughtInTotal(hall, "mining"),
              "deeds building: materials recovered are their own section (total, the bar form per item, recorded from their own date); one kind is one tile; Mining's brought in leaves them out");

        // ---------- Crafting ----------
        var crafting = Show(rich, "crafting");
        Check(Find(crafting, "hero")?.Value == "20" && Find(crafting, "hero").Title == "gear crafted" && Find(crafting, "hero").Items.Single().Value == "11" && Find(crafting, "hero").Items.Single().Title == "upgrades made",
              "deeds crafting: gear crafted (the game's four gear counters) and upgrades");
        var kinds = PanelModel.FilterOf(crafting).Items.First(b => b.Kind == "facet" && b.Id == "kind");
        Check(kinds.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Weapons=13", "Armor=2", "Tools=4", "Trinkets=1" }) && Find(crafting, "counts") == null,
              "deeds crafting: the kind counts are the filter's chips (13 + 2 + 4 + 1, a shield with the weapons as the game counts it), the per-kind tiles are gone");
        var perItem = Find(crafting, "itemgrid");
        Check(perItem?.Kind == "itemgrid" && perItem.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Bronze Axe=6", "Wood Shield=4", "Hammer=2", "Leather Helmet=2", "Iron Sword=2",
              "Crude Bow=1", "Cultivator=1", "Hoe=1", "Bronze Health Trinket=1" }), "deeds crafting: all gear made, per item, as an item grid (no food, no arrows), trinkets included");
        Check(perItem.Items.Sum(i => int.Parse(i.Value)) == kinds.Items.Sum(i => int.Parse(i.Value)) && perItem.Items.Sum(i => int.Parse(i.Value)) == 20,
              "fix2 5 crafting: the per-item list adds up to the kinds (9 + 6 + 4 + 1 = 20)");
        var used = After(crafting, "Put to good use by");
        Check(used?.Kind == "people" && used.Items.Single().Title == "Edda" && used.Items.Single().Items.Single().Title == "Bronze Axe" && used.Items.Single().Items.Single().Icon == "item:AxeBronze",
              "deeds crafting: fellows who put your gear to good use, by name and picture");
        Check(!PanelModel.AllText(crafting).Any(t => t.Contains("smelter") || t.Contains("Carrot")), "deeds crafting: no smelters (Hall) and no food (Cooking)");

        // ---------- Farming ----------
        var farming = Show(rich, "farming");
        Check(Find(farming, "hero")?.Value == "208" && Find(farming, "hero").Title == "planted" && Find(farming, "hero").Items.Single().Value == "312" && Find(farming, "hero").Items.Single().Title == "picked", "deeds farming: planted and picked, the two big numbers");
        Check(Find(farming, "note") == null,
              "deeds farming: no baseline (a fellow's copy, totals not loaded): the game's planted counter alone; no sentence row");
        var crops = After(farming, "Crops");
        Check(crops?.Kind == "cropgrid" && crops.Items.Select(i => i.Title + " " + i.Value + "/" + i.Value2 + " " + i.Text).SequenceEqual(new[] { "Barley 66/72 planted", "Turnip 58/64 planted", "Carrot 41/60 planted", "Other plants 147/ " }),
              "deeds farming: every crop as a tile, most picked first, and the plants you never planted as the list's last tile (fix4: 66 + 58 + 41 + 147 = the 312 picked)");
        Check(crops.Items.Sum(i => PanelModel.ParseCount(i.Items.Single(p => p.Tone == PanelModel.PickedWord).Value)) == 312 && crops.Items.Last().Id == PanelModel.OtherPlantsId && crops.Items.Last().Items.Count == 1,
              "deeds farming: the tiles' picked numbers add up to the hero's 312 (Other plants carries picked alone)");
        Check(crops.Text == PanelModel.PickedScope && crops.Items.Where(i => i.Id != PanelModel.OtherPlantsId).All(i => i.Items.Select(p => p.Title).SequenceEqual(new[] { "planted", "picked" })) &&
              crops.Items[0].Items.Select(p => p.Value + " " + p.Title + " " + p.Fraction.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).SequenceEqual(new[] { "72 planted 1.00", "66 picked 0.92" }) &&
              crops.Items[2].Items[0].Fraction > crops.Items[2].Items[1].Fraction && crops.Items.Where(i => i.Id != PanelModel.OtherPlantsId).All(i => i.Items[0].Colour == PanelModel.PlantedColour && i.Items[1].Colour == PanelModel.PickedColour),
              "diff-05 farming: each crop tile carries two thin paired bars with their numbers (planted tan, picked green, the crop's own scale); what picked includes in the legend line");
        Check(After(farming, "Also planted")?.Items.Single().Title == "Beech Sapling", "deeds farming: plantings that are no crop (trees) apart, never as a crop");
        Check(After(farming, PanelModel.AlsoHarvestedTitle)?.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Berries=512", "Mushrooms=84", "Honey=36", "Sap=22" }) == true,
              "deeds farming: the other harvests in one strip, named as counted apart from the picked crops (vines left out at zero; Other plants moved into the crop list)");
        Check(!PanelModel.AllText(farming).Any(t => t.Contains("Flint")), "deeds farming: other pickables (flint) are not crops");
        Check(!PanelModel.AllText(farming).Any(t => t.IndexOf("harvested", StringComparison.OrdinalIgnoreCase) >= 0 && t != PanelModel.AlsoHarvestedTitle), "deeds farming: crops say picked, never harvested");

        // planted in two layers (the game's counter at install + every plant counted since): 0.7 rule A (REDESIGN-RULES.md part 1) says the sum is the number,
        // one "Earlier counts may be incomplete." in the hero's slot, and the crop tiles carry sums only (no faded share, no key)
        var layeredIn = Layered(input);
        var layered = Show(layeredIn, "farming");
        var lh = Find(layered, "hero");
        Check(lh.Value == "279" && lh.Note == null && PanelModel.AboutText(layered).Contains(PanelModel.EarlierIncomplete) && lh.Src == "character" && lh.Title == "planted" && lh.Items.Single().Value == "312" && lh.Items.Single().Title == "picked",
              "deeds farming layered (0.7 rule A): hero planted = 88 at install + 191 counted since = 279, one \"Earlier counts may be incomplete.\" in About these numbers (0.8 layout D+); the game counter's later growth (72 barley, 64 turnip) is not added: " + lh.Value);
        var lc = After(layered, "Crops");
        Check(lc.Note == null && lc.Items.All(i => i.Items.All(p => p.Fraction2 == 0)),
              "deeds farming layered (0.7 rule A): no faded shares and no legend key on the crop tiles, the sums only");
        Check(lc.Items.Select(i => i.Title + " " + i.Value + "/" + i.Value2).SequenceEqual(new[] { "Barley 66/187", "Turnip 58/20", "Carrot 41/60", "Other plants 147/" }),
              "deeds farming layered (0.7 rule A): per crop the planted sum (barley 187 = 16 before install + 171 since), a crop counted only since (turnip 20) or only before (carrot 60) alike: " +
              string.Join(" | ", lc.Items.Select(i => i.Title + " " + i.Value + "/" + i.Value2)));
        Check(After(layered, "Also planted")?.Items.Single().Value == "12" && !PanelModel.AllText(layered).Any(t => t.Contains("Wood Wall") || t.Contains("woodwall")),
              "deeds farming layered: a tree planted before install stays in Also planted; built pieces in the baseline are not plantings");
        Check(PanelModel.Content(layered).Count(b => b.Kind == "note") == 0, "deeds farming layered: no sentence rows, the legend says it");
        var allSince = Layered(input); allSince.Baseline[PanelModel.PlacedBaseline] = new Dictionary<string, float>();
        var ash = Find(Show(allSince, "farming"), "hero");
        Check(ash.Value == "191" && ash.Note == null && ash.Src == "character" && ash.Title == "planted",
              "deeds farming layered (0.7 rule A): a character that started with Hearthwoven: every plant counted exactly, one number with no earlier count, no note");
        var fellowCopy = Layered(input); fellowCopy.IsSelf = false;
        Check(Find(Show(fellowCopy, "farming"), "hero").Value == "208", "deeds farming layered: a fellow's copy shows the game's counter, no layers");
        Check(!System.Text.RegularExpressions.Regex.IsMatch(PanelModel.ToJson(layered), "\"faded\":\"[0-9]"), "deeds farming layered (0.7): the preview JSON carries no faded parts (rule A: sums only)");
        var lcard = Find(PanelModel.Build(layeredIn, new PanelState { Chapter = Chapter.Deeds }), "cards").Items.Single(c => c.Title == "Fieldkeeper");
        Check(lcard.Value == "312" && lcard.Text == "crops picked" && lcard.Items[0].Value == "279", "deeds overview: the Fieldkeeper card says crops picked and the planted total");

        // ---------- Fishing ----------
        var fishing = Show(rich, "fishing");
        var hcl = Find(fishing, "ranking");   // the three rows, no "Hooked, caught, lost" heading (J)
        Check(hcl.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Hooked=402", "Caught=251", "Got away=97" }) && hcl.Items[0].Fraction == 1f && hcl.Items.All(i => i.Colour != null && i.Icon == ""),
              "deeds fishing: hooked, caught and lost on one scale, in that order");
        var q = After(fishing, "Caught by quality, 6 best");
        Check(q.Kind == "grades" && q.Items.Select(i => i.Value).SequenceEqual(new[] { "104", "71", "44", "22", "9", "1" }) && q.Items.All(i => i.Icon == "item:$animal_fish1"), "deeds fishing: caught by quality 1..6, the most caught fish as the picture");
        var per = After(fishing, "Fish caught");
        Check(per.Kind == "itemgrid" && per.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Perch=88", "Pike=61", "Tuna=60", "Giant Herring=42" }) && per.Value == null && PanelModel.Content(fishing).First(b => b.Kind == "section" && b.Title == "Fish caught").Value == "251" && Find(Show(rich, "taming"), "counts").Tone == "framed", "deeds fishing: caught per kind from the game's fish record, its total in the section title (Fish caught 251: the named kinds, which add up to the 251 caught; no remainder tile); Taming draws thin framed tiles");
        Check(PanelModel.Content(Show(input, "fishing")).All(b => b.Kind == "empty" || b.Kind == "plate"), "deeds fishing: nothing caught, nothing drawn");
        // fish-held: the sample's kinds add up to its Caught row (both are booked at the same reeled-in catch), so the page never shows two numbers
        // that disagree; real data that differs shows the kinds' own total in the section title, never Caught's
        var hclRows = Find(fishing, "ranking"); var caughtRow = hclRows?.Items.FirstOrDefault(i => i.Title == "Caught")?.Value;
        Check(caughtRow != null && per.Items.Sum(i => double.Parse(i.Value.Replace(" ", ""), System.Globalization.CultureInfo.InvariantCulture)).ToString(System.Globalization.CultureInfo.InvariantCulture) == caughtRow.Replace(" ", "") &&
              PanelModel.Content(fishing).First(b => b.Kind == "section" && b.Title == "Fish caught").Value == caughtRow,
              "fish-held fishing: the sample's fish by kind add up to Caught (" + caughtRow + "), the section title the same number");
        var odd = Rich(input); odd.Harvested = new Dictionary<string, float>(odd.Harvested); odd.Harvested.Remove("$animal_fish3");
        var oddV = Show(odd, "fishing");
        Check(PanelModel.Content(oddV).First(b => b.Kind == "section" && b.Title == "Fish caught").Value == "191" && Find(oddV, "ranking").Items.First(i => i.Title == "Caught").Value == "251" &&
              !PanelModel.Content(oddV).Any(b => (b.Title ?? "").StartsWith("Other") || (b.Items ?? new List<Block>()).Any(i => (i.Title ?? "").StartsWith("Other"))),
              "fish-held fishing: when the kinds record holds fewer fish than Caught (191 of 251), the section says the kinds' own total; no remainder is made up");
        Check(Find(fishing, "columns")?.Items.Count == 2, "deeds fishing: counts and quality left, per kind right");
        // live-polish: Joost's own book in game (ingame-0.6rc/deeds-fishing.json and his local totals, 9 Oct): FishHooked 20, FishLost 8, no
        // FishCaught and no fish in the picked-plants record (the game books a catch only when the line is reeled in), yet 6 fish picked up
        // ($animal_fish5 4, $animal_fish1 1, $animal_fish7 1). The reader is right (PlayerStatType.FishCaught exists, FishingFloat.Catch is its
        // only writer); the page now shows the fish that came in another way and says what Other holds
        var joost = new PanelInput { PlayerName = "Astrid", IsSelf = true, NowUtc = input.NowUtc, Character = new Dictionary<string, float> { ["FishHooked"] = 20, ["FishLost"] = 8 },
                                     ItemsPickedUp = new Dictionary<string, float> { ["$animal_fish5"] = 4, ["$animal_fish1"] = 1, ["$animal_fish7"] = 1, ["$item_fish_raw"] = 57 }, Harvested = new Dictionary<string, float>() };
        var jf = Show(joost, "fishing"); var jRows = Find(jf, "ranking"); var jHeld = After(jf, PanelModel.FishHeldTitle);
        Check(jRows != null && jRows.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Hooked=20", "Caught=0", "Got away=8" }) &&
              !PanelModel.Content(jf).Any(b => (b.Text ?? "").StartsWith("Other") || (b.Title ?? "").StartsWith("Other") || (b.Items ?? new List<Block>()).Any(i => (i.Title ?? "").StartsWith("Other"))),
              "fish-held fishing (Joost's values): the game's three counters as booked, hooked 20, caught 0, got away 8; no Other row, tile or note (a remainder proves nothing)");
        Check(jHeld != null && jHeld.Kind == "itemgrid" && jHeld.Items.Select(i => i.Id + "=" + i.Value).SequenceEqual(new[] { "$animal_fish5=4", "$animal_fish1=1", "$animal_fish7=1" }) &&
              PanelModel.Content(jf).First(b => b.Kind == "section" && b.Title == PanelModel.FishHeldTitle).Value == "6" && !PanelModel.Content(jf).Any(b => b.Kind == "section" && b.Title == "Fish caught") &&
              PanelModel.Content(jf).Any(b => b.Kind == "note" && b.Text == PanelModel.FishHeldNote),
              "live-polish fishing (Joost's values): the 6 fish picked up show as their own group (raw fish meat is not a fish), never added to caught");
        Check(After(fishing, PanelModel.FishHeldTitle) == null, "live-polish fishing: when every fish picked up was reeled in, no picked-up group (the sample's catches hold them all)");

        // ---------- Taming ----------
        var taming = Show(rich, "taming");
        var three = Find(taming, "counts");
        Check(three.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Tamed=6", "Petted=41", "Commands given=19" }) && three.Items.Select(i => i.Icon).SequenceEqual(new[] { "vocab:tame-tamed", "vocab:tame-petted", "vocab:tame-command" }) && three.Src == "character",
              "deeds taming: the game's three counters as tiles (no per-creature count exists), Codex's care pictures");
        Check(Find(taming, "band") == null && PanelModel.AllText(taming).Count(t => t.Contains("Cared for a creature")) == 1 && PanelModel.BandTitles(Find(taming, "featband")).Single().Text.StartsWith("Cared for a creature"),
              "deeds taming: no title band (it repeated Petted and Commands given); the strip says Beastkeeper's reason once (B18)");

        var big = After(Show(Big(input), "farming"), "Crops");
        Check(big.Items.Where(i => i.Id != PanelModel.OtherPlantsId).Select(i => i.Value + "/" + i.Value2).SequenceEqual(new[] { "3\u00A0960/3\u00A0120", "1\u00A0290/1\u00A0406", "1\u00A0041/2\u00A0210" }),
              "deeds farming: large numbers keep their separators (the tile and the preview check that they fit)");

        // ---------- across the six pages ----------
        var views = new[] { "cooking", "building", "groundwork", "crafting", "farming", "fishing", "taming" }.Select(p => Show(rich, p)).ToList();
        var text = views.SelectMany(PanelModel.AllText).Where(t => !string.IsNullOrEmpty(t)).ToList();
        var bad = text.FirstOrDefault(t => new[] { "picked up", "enemy", "ate ", "used", "took", "since this character" }.Any(w => t.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) || t.Contains('—') || t.Contains('–') || t.StartsWith("$"));
        Check(bad == null, "deeds words: word list kept, no raw tokens, no dashes" + (bad != null ? ": " + bad : ""));
        var valid = true;
        foreach (var v in views) try { var d = JsonDocument.Parse(PanelModel.ToJson(v)); if (!PanelModel.ToJson(v).Contains("\"columns\"")) valid = false; } catch { valid = false; }
        Check(valid, "deeds: preview JSON valid, the new fields included");
        var unmarkedDeed = views.SelectMany(PanelModel.Content).FirstOrDefault(b => !(b.Kind == "section" || b.Kind == "note" || b.Kind == "sort" || b.Kind == "aboutnumbers" || PanelModel.IsBox(b) || PanelModel.IsFeatKind(b.Kind) || b.Src != null));
        Check(unmarkedDeed == null, "deeds: every block with a number carries its source mark" + (unmarkedDeed != null ? ": " + unmarkedDeed.Kind + " '" + (unmarkedDeed.Title ?? unmarkedDeed.Text) + "'" : ""));
        Check(views.All(v => PanelModel.PlateOf(v) != null), "deeds: every page sits on the plate");

        // ---------- Overview (r4over-deeds-a): a card per deed, short labels, then the other names earned ----------
        var over = PanelModel.Build(rich, new PanelState { Chapter = Chapter.Deeds });
        var cards = Find(over, "cards");
        // Earned and Unsung (Joost 2026-10-08): the overview's view switch; Unsung shows the names not earned yet, dimmed,
        // with the title's own description and no number (the mod has no progress to measure)
        var young = new PanelInput { Character = new Dictionary<string, float> { ["Tree"] = 12, ["CraftFood"] = 3 }, SkillLevels = new Dictionary<string, float>(), SkillProgress = new Dictionary<string, float>(), Events = new SessionEvents(), Log = new EventLog() };
        var youngOver = PanelModel.Build(young, new PanelState { Chapter = Chapter.Deeds });   // a young character: two names earned
        var sw = Find(youngOver, "switch");
        Check(sw != null && sw.Items.Select(v => v.Title).SequenceEqual(new[] { "Earned", "Unsung" }) && sw.Items[0].Selected, "deeds overview: Earned (default) and Unsung views");
        var unsungView = PanelModel.Build(young, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = "overview" }, View = { [sw.Id] = "unsung" } });
        var unsung = Find(unsungView, "cards");
        var earnedIds = new HashSet<string>(PanelModel.Titles(young).Select(x => x.Id).Concat(PanelModel.SagaTitles.Where(x => Find(youngOver, "cards").Items.Any(c => c.Title == x.Title)).Select(x => x.Id)));   // a deed with a card is not unsung
        Check(unsung != null && unsung.Items.Count == PanelModel.SagaTitles.Length - earnedIds.Count && unsung.Items.All(c => c.Tone == "unsung" && string.IsNullOrEmpty(c.Value) && (c.Items == null || c.Items.Count == 0)) &&
              unsung.Items.All(c => PanelModel.SagaTitles.Any(x => x.Title == c.Title && PanelModel.FirstStep(x) == c.Text && !earnedIds.Contains(x.Id))),
              "deeds overview, Unsung: every name not earned yet, its description from the title table, no number");
        Check(Find(unsungView, "strip") == null && Find(youngOver, "cards").Items.All(c => c.Tone != "unsung") && earnedIds.Count >= 2, "deeds overview: Earned keeps the cards and other names; Unsung only the unearned");
        var allSw = Find(over, "switch");
        var allUnsung = PanelModel.Build(rich, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = "overview" }, View = { ["Deeds/overview/view"] = "unsung" } });
        Check(allSw != null && allSw.Items.Select(v => v.Title).SequenceEqual(new[] { "Earned", "Unsung" }) && over.Keys.Contains("[F] View") &&
              (PanelModel.Titles(rich).Count < PanelModel.SagaTitles.Length || Find(allUnsung, "note")?.Text == PanelModel.EveryNameEarned),
              "deeds overview: the Earned/Unsung switch always shows (F flips it); every name earned: Unsung says so");
        Check(over.Heading == "Deeds" && cards.Items.Select(c => c.Title + " " + c.Value + " " + c.Text + " / " + c.Items.FirstOrDefault()?.Value + " " + c.Items.FirstOrDefault()?.Title).SequenceEqual(new[] {
                "Hearth Cook 166 dishes cooked / 2 enjoyed by fellows", "Hallwright 866 pieces built / 1\u00A0023 groundwork strokes", "Forgekeeper 20 gear crafted / 11 upgrades made",
                "Woodcutter 410 trees felled /  ", "Stonebreaker 2\u00A0134 stone, ore brought in /  ", "Fieldkeeper 312 crops picked / 208 planted",
                "Tidecatcher 251 fish caught / 402 hooked", "Beastkeeper 60 petted and commanded / 6 tamed" }),
              "deeds overview: one card per deed, the big number, a short label and one short second line when it is a class A, B or D number (0.7, hard case 13): " + string.Join(" | ", cards.Items.Select(c => c.Title + " " + c.Value + " " + c.Text + " / " + c.Items?.FirstOrDefault()?.Value + " " + c.Items?.FirstOrDefault()?.Title)));
        Check(cards.Items.All(c => c.Text.Length <= 22 && (c.Items == null || c.Items.Count == 0 || c.Items[0].Title.Length <= 18)) && cards.Items.All(c => c.Id.StartsWith("Deeds/")),
              "deeds overview: card labels short enough to never be cut off, each card opens its deed page");
        var woodCard = cards.Items.Single(c => c.Title == "Woodcutter");
        Check((woodCard.Items == null || woodCard.Items.Count == 0) && over.Recorded,
              "deeds overview 0.7: no zones; the class C axe hits drop off the Woodcutter card (hard case 13)");
        var others = After(over, "Other titles");
        Check(others?.Kind == "strip" && others.Items.All(t => !t.Id.StartsWith("Deeds/") && t.Value == "") && others.Items.Single(t => t.Title == "Shieldbearer").Text == "Battle" && others.Items.Single(t => t.Title == "Helmskeeper").Id == "Voyages/sailing",
              "deeds overview: the other chapters' names earned in one line, each with its chapter, no numbers");
        Check(PanelModel.Content(PanelModel.Build(new PanelInput { PlayerName = "Nobody" }, new PanelState())).Any(b => b.Kind == "empty"), "deeds overview: no deeds, the empty line");

        // ---------- the harvested record travels in a shared snapshot ----------
        var st = new PlayerProfile.PlayerStats[1]; st[0] = new PlayerProfile.PlayerStats();
        st[0][PlayerStatType.FishCaught] = 3f; st[0].m_pickableStats["Barley"] = 12f; st[0].m_pickableStats["$animal_fish1"] = 3f;
        var json = GroupShare.SharedCopy(Snapshot.Build("0.2.0", 9L, "Sif", st, new Snapshot.SkillInfo[0], "Midgard", new DamageTally(), "s1", new SessionEvents(), new EventLog(), share: true));
        var sif = PanelInput.FromSnapshot(json);
        Check(sif.Harvested != null && sif.Harvested["Barley"] == 12f && sif.Harvested["$animal_fish1"] == 3f, "deeds snapshot: the harvested record (crops, fish per kind) comes back from a shared copy");

        input.Fellows = keep; edda.Fellows = keepE;
        return fails;
    }
}
