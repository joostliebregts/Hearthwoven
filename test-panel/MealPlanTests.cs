// Tests for Plan a meal (src/Panel/Chapters/MealPlanModel.cs, 0.9.2; 0.9.5 its own page, Deeds > Meal plan): the land of a food from the game's data,
// which lands show and in what order, the slots (fill, empty, the fourth replaces the oldest), one scale (the best three foods of the shown lands = the
// full track), the first to wear off, feasts in, unknown foods left out, the "can make" mark, a combination opening the page with its foods in the
// slots, the keys, the page in the Deeds list, its meal pinned under the strip, and Meals without the planner.
// On Rowan's sample (src/Panel/PanelSampleMeals.cs MealPlanSample). Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using Hearthwoven;
using Hearthwoven.Panel;

static class MealPlanTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    // Deeds > Meal plan (0.9.5: the planner's own page)
    static PanelState State(string plan = null, string best = null) => On(PanelModel.MealPlanPageId, plan, best);
    static PanelState On(string page, string plan = null, string best = null)
    {
        var st = new PanelState { Chapter = Chapter.Deeds }; st.Page[Chapter.Deeds] = page;
        if (plan != null) st.View[PanelModel.MealPlanKey] = plan;
        if (best != null) st.View[PanelModel.MealPlanBestKey] = best;
        return st;
    }
    static Block Chips(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "sort" && b.Id == PanelModel.MealPlanBestKey);
    static List<Block> Grids(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "itemgrid" && b.Tone == PanelModel.MealPlanTone).ToList();
    static List<Block> Lands(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "section" && b.Id == PanelModel.MealPlanId && b.Title != PanelModel.MealPlanTitle).ToList();
    static Block Row(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "sources" && b.Id == PanelModel.MealPlanId)?.Items?.FirstOrDefault();
    static List<Block> Foods(PanelView v) => Grids(v).SelectMany(g => g.Items).ToList();
    static string SourceOf(string dir, string file)
    {
        var root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "src", "Plugin.cs"))) root = Path.GetDirectoryName(root);
        var p = root == null ? null : Path.Combine(root, "src", "Panel", dir, file);
        return p != null && File.Exists(p) ? File.ReadAllText(p) : "";
    }
    // a method's text from its signature to the next blank line
    static string Body(string src, string signature)
    {
        var i = src.IndexOf(signature, StringComparison.Ordinal); if (i < 0) return "";
        var end = src.IndexOf("\n\n", i, StringComparison.Ordinal); return end < 0 ? src.Substring(i) : src.Substring(i, end - i);
    }
    static PanelModel.FoodData Food(string prefab) => MealPlanSample.Foods.First(f => f.Prefab == prefab);

    public static int Run(DateTime now)
    {
        fails = 0;

        // ---------- the land of a food ----------
        var lands = new Dictionary<string, int> { ["Raspberry"] = 0, ["Mushroom"] = 0, ["CookedMeat"] = 0, ["MushroomYellow"] = 1, ["CarrotSoup"] = 1, ["BoarJerky"] = 1, ["Sausages"] = 2,
                                                  ["Eyescream"] = 3, ["SerpentStew"] = 3, ["Bread"] = 4, ["FishWraps"] = 4, ["SeekerAspic"] = 5, ["MisthareSupreme"] = 5, ["MarinatedGreens"] = 6,
                                                  ["FeastMeadows"] = 0, ["FeastMountains"] = 3, ["FeastAshlands"] = 6, ["HwSampleCake"] = -1 };
        var wrong = lands.Where(kv => PanelModel.FoodLand(Food(kv.Key)) != kv.Value).Select(kv => kv.Key + "=" + PanelModel.FoodLand(Food(kv.Key))).ToList();
        Check(wrong.Count == 0, "land: from the food's own name, its ingredients and what a station makes it from, the latest land; a feast by its own land; a mod's cake none (" + string.Join(" ", wrong) + ")");
        Check(PanelModel.FoodLand(Food("BoarJerky")) == 1 && PanelModel.LandOfFoodToken("$item_boarjerky") == 0,
              "land: a cauldron dish is the Black Forest at least (Boar Jerky: boar and honey say the Meadows)");
        Check(PanelModel.FoodLand(new PanelModel.FoodData { Prefab = "FeastOceans", Token = "$item_feastoceans", Feast = true, From = new List<string> { "$item_serpentmeat" } }) == 3
              && PanelModel.FoodLand(new PanelModel.FoodData { Prefab = "ModStew", Token = "$item_modstew", Station = "piece_cauldron" }) == -1,
              "land: the Ocean's feast by its ingredients (serpent: the Mountains); a mod's cauldron dish nothing points to stays without a land");

        // ---------- which lands ----------
        PanelInput With(string[] known, params (int minute, string land)[] minutes)
        {
            var i = PanelSample.Full(now); i.Log = null; i.KnownBiomes = known.ToList();
            var log = new DeedLog(); foreach (var (m, l) in minutes) log.NoteLand(now.AddMinutes(-120 + m), l);
            i.Deeds = log; return i;
        }
        string Say(List<(int land, string words)> l) => string.Join(" | ", l.Select(x => PanelModel.LandName(x.land) + ", " + x.words));
        var all7 = new[] { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "Ocean" };
        var ash = PanelModel.PlanLands(With(all7, (0, "Meadows"), (30, "AshLands"), (60, "AshLands")));
        Check(Say(ash) == "Ashlands, where you are now | Mistlands, before | Plains, the one before", "lands: where you are now, then the two before it (" + Say(ash) + ")");
        var home = PanelModel.PlanLands(With(all7, (0, "AshLands"), (50, "Meadows")));
        Check(Say(home) == "Ashlands, where you explore | Mistlands, before | Plains, the one before", "lands: back home in the Meadows, the land you explore still leads (" + Say(home) + ")");
        var gap = PanelModel.PlanLands(With(new[] { "Meadows", "Swamp", "Mountain", "Ocean" }, (10, "Mountain")));
        Check(Say(gap) == "Mountains, where you are now | Swamp, before | Meadows, the one before", "lands: only lands you have been in, in the game's order (no Black Forest: skipped) (" + Say(gap) + ")");
        var none = PanelModel.PlanLands(With(new[] { "Meadows", "BlackForest", "Swamp", "Ocean" }));
        Check(Say(none) == "Swamp, the furthest you have been | Black Forest, before | Meadows, the one before", "lands: no land minutes, the furthest land found (" + Say(none) + ")");
        var first = PanelModel.PlanLands(With(new[] { "Meadows" }, (5, "Meadows")));
        Check(Say(first) == "Meadows, where you are now", "lands: a first evening, the Meadows alone (" + Say(first) + ")");

        // ---------- the slots ----------
        var a = PanelModel.PlanAfter(new List<string>(), "A"); var ab = PanelModel.PlanAfter(a.Split(','), "B"); var abc = PanelModel.PlanAfter(ab.Split(','), "C");
        var bcd = PanelModel.PlanAfter(abc.Split(','), "D"); var ac = PanelModel.PlanAfter(abc.Split(','), "B");
        Check(a == "A" && ab == "A,B" && abc == "A,B,C" && bcd == "B,C,D" && ac == "A,C", "slots: a click fills the next slot, a fourth replaces the oldest, a food already in comes out (" + string.Join(" ", new[] { abc, bcd, ac }) + ")");
        Check(PanelModel.PlanSlots(State("A,,A,B,C,D")).SequenceEqual(new[] { "A", "B", "C" }) && PanelModel.PlanSlots(State()).Count == 0, "slots: the state read as three foods at most, none twice, empty parts left out");

        // ---------- Rowan's page ----------
        var rowan = PanelSample.Full(now);
        var empty = PanelModel.Build(rowan, State());
        var list = empty.List.Select(l => l.Id).ToList();
        Check(empty.Page == PanelModel.MealPlanPageId && list.IndexOf(PanelModel.MealPlanPageId) == list.IndexOf(PanelModel.MealsPageId) + 1
              && empty.List.First(l => l.Id == PanelModel.MealPlanPageId).Label == PanelModel.MealPlanLabel && empty.List.First(l => l.Id == PanelModel.MealPlanPageId).Icon == PanelModel.MealPlanIcon,
              "page: Meal plan stands in the Deeds list right under Meals, with its own icon (" + string.Join(" ", list) + ")");
        Check(empty.Windows.Count == 0 && empty.Heading == PanelModel.MealPlanLabel && !PanelModel.Content(empty).Any(b => b.Kind == "section" && b.Title == PanelModel.MealPlanTitle),
              "page: no window chips (the foods are the game's data, as Armor sets), no heading of its own over the meal");
        var meals = PanelModel.Build(rowan, On(PanelModel.MealsPageId));
        var chip = PanelModel.PlateOf(meals).Items.FirstOrDefault(b => b.Kind == "headlink" && b.Id == "Deeds/" + PanelModel.MealPlanPageId);
        Check(!PanelModel.Content(meals).Any(b => b.Id == PanelModel.MealPlanId || b.Id == PanelModel.MealPlanBestKey || (b.Kind == "itemgrid" && b.Tone == PanelModel.MealPlanTone))
              && chip != null && chip.Tone == PanelModel.StripLinkTone && chip.Title == PanelModel.MealPlanTitle && PanelModel.StripLinks(PanelModel.PlateOf(meals)).Contains(chip),
              "Meals: no planner island any more; a chip in its strip (the headlinks' grammar, as Crafting's Sets) opens Meal plan");
        var chipSt = On(PanelModel.MealsPageId); var chipFrom = PanelModel.Build(rowan, chipSt); PanelModel.Visited(chipSt, chipFrom.Active, chipFrom.Page); PanelModel.Follow(chipSt, chip.Id);
        Check(chipSt.PageOf(Chapter.Deeds) == PanelModel.MealPlanPageId && PanelModel.Build(rowan, chipSt).Back != null, "Meals: the chip opens Meal plan, its strip's Back leads back to Meals");
        var row = Row(empty);
        Check(row != null && row.Tone == PanelModel.ComboTone && row.Items.Count(i => i.Kind == PanelModel.ComboDishKind && i.Tone == PanelModel.PlanEmptyTone) == 3 && row.Text == PanelModel.PlanPick
              && !row.Items.Any(i => i.Kind == "part"), "empty: Your combinations' row form, three empty slots, no bar, one line in its place (" + row?.Text + ")");
        var shownLands = Lands(empty).Select(l => l.Title + ", " + l.Text).ToList();
        var expected = PanelModel.PlanLands(rowan).Select(x => PanelModel.LandName(x.land) + ", " + x.words).ToList();
        Check(shownLands.SequenceEqual(expected) && shownLands.Count == 3, "page: a heading per land with its words (" + string.Join(" | ", shownLands) + ")");
        var landIdx = PanelModel.PlanLands(rowan).Select(x => x.land).ToList();
        var foods = Foods(empty);
        Check(foods.All(f => landIdx.Contains(PanelModel.FoodLand(Food(f.Id)))) && foods.Count == MealPlanSample.Foods.Count(f => PanelModel.IsEdible(f) && landIdx.Contains(PanelModel.FoodLand(f))),
              "page: every food of the shown lands, none of the others (" + foods.Count + " foods)");
        var feasts = MealPlanSample.Foods.Where(f => f.Feast && landIdx.Contains(PanelModel.FoodLand(f))).Select(f => f.Prefab).ToList();
        Check(feasts.Count == 3 && feasts.All(p => foods.Any(f => f.Id == p)), "feasts: every feast of the shown lands is a food to pick (" + string.Join(", ", feasts) + ")");
        Check(Grids(empty).All(g => g.Items.Select(i => Food(i.Id).Total).SequenceEqual(g.Items.Select(i => Food(i.Id).Total).OrderByDescending(t => t))),
              "page: a land's foods strongest first");
        var soup = foods.FirstOrDefault(f => f.Id == "DeerStew"); var made = foods.FirstOrDefault(f => f.Id == "CarrotSoup"); var yellow = foods.FirstOrDefault(f => f.Id == "MushroomYellow");
        Check(soup != null && soup.Note == PanelModel.PlanCanMake && soup.Tone == null && made?.Note == PanelModel.GearMade && yellow != null && yellow.Note == null && yellow.Tone == PanelModel.PlanUnknownTone && yellow.Link != null,
              "can make: a food whose recipe you know says \"can make\", one you made \"made\" (What to use's mark); one not known yet is quiet, still a click (" + string.Join(", ", foods.Select(f => f.Id + ":" + (f.Note ?? f.Tone))) + ")");
        // 0.9.8 (Joost in game: "why not show the actual icons there?"): the key under Best for is the tiles' own marks, each with its meaning, not words
        var key = PanelModel.Content(empty).FirstOrDefault(b => b.Kind == "note" && b.Tone == PanelModel.PlanMarksTone);
        var keyItems = key?.Items ?? new List<Block>();
        Check(key != null && string.IsNullOrEmpty(key.Text) && keyItems.Count == 3
              && keyItems[0].Note == PanelModel.GearMade && keyItems[0].Title == PanelModel.PlanMarkMade
              && keyItems[1].Note == PanelModel.PlanCanMake && keyItems[1].Title == PanelModel.PlanMarkCanMake
              && keyItems[2].Tone == PanelModel.PlanUnknownTone && keyItems[2].Title == PanelModel.PlanMarkUnknown && foods.Any(f => f.Tone == PanelModel.PlanUnknownTone && keyItems[2].Icon == f.Icon)
              && !PanelModel.Content(empty).Any(b => (b.Text ?? "").Contains("gold dot")),
              "marks key: the gold dot (made or owned), the ring (can make) and a faded picture of a food not known yet (" + string.Join(" · ", keyItems.Select(k => (k.Note ?? k.Tone) + " " + k.Title)) + "), no \"a gold dot\" sentence");
        var chipCols = Chips(empty)?.Items?.Select(c => c.Id + "=" + c.Colour).ToList() ?? new List<string>();
        Check(Chips(empty).Items.All(c => c.Id == PanelModel.PlanAny ? c.Colour == null : c.Colour == PanelModel.CategoryColour(c.Id)),
              "Best for: Health, Stamina and Eitr carry their bars' colours (a swatch on the pill), Any none (" + string.Join(" ", chipCols) + ")");
        // the drawing: the key and the tiles draw the marks through one call (MealPlanUi.PlanMark), the book's round sprites (BattleFeedUi.MarkImg)
        var src = SourceOf("Chapters", "MealPlanUi.cs"); var feed = SourceOf("Chapters", "BattleFeedUi.cs");
        var keyBody = Body(src, "static void PlanMarksKey("); var markBody = Body(src, "static void PlanMark(");
        Check(keyBody.Contains("PlanMark(") && !keyBody.Contains("gold dot") && Regex.IsMatch(markBody, @"MarkImg\(") && !markBody.Contains("RoundedPart") && !markBody.Contains("BtHollow")
              && Regex.IsMatch(feed, @"case ""made"": sprite = PanelLook\.Circle") && Regex.IsMatch(feed, @"case PanelModel\.PipOpen:.*sprite = PanelLook\.Ring"),
              "marks drawing: the key draws the tiles' marks with the same call (PlanMark), round: the made dot PanelLook.Circle, the can-make ring PanelLook.Ring (no rounded square, no four-edge box)");

        Check(made.Value == "15 · 45" && made.Text == "25m" && made.Items.Count(i => i.Kind == "part") == 2 && made.Items.All(i => i.Tone != PanelModel.ComboTone),
              "a tile: one serving's numbers (its hover) and how long it lasts, its bar's parts carry their numbers inside (not a combination's numberless parts) (" + made.Value + " / " + made.Text + ")");
        var most = foods.Max(f => Food(f.Id).Total);
        Check(foods.All(f => Math.Abs(f.Fraction2 - Food(f.Id).Total / most) < 1e-4), "a tile: its small bar on one scale for the planner");

        // ---------- three picked, a feast among them ----------
        var three = PanelModel.Build(rowan, State("WolfMeatSkewer,Eyescream,FeastMountains"));
        var r3 = Row(three); var parts = r3?.Items?.Where(i => i.Kind == "part").ToList() ?? new List<Block>();
        var shown = Foods(three).Select(f => Food(f.Id)).ToList();
        var best = shown.Select(f => f.Total).OrderByDescending(t => t).Take(3).Sum();
        double total = new[] { "WolfMeatSkewer", "Eyescream", "FeastMountains" }.Sum(p => Food(p).Total);
        Check(r3 != null && r3.Items.Where(i => i.Kind == PanelModel.ComboDishKind).Select(i => i.Icon).SequenceEqual(new[] { "item:WolfMeatSkewer", "item:Eyescream", "item:FeastMountains" }),
              "three: the slots hold the picked foods in order, a feast among them");
        Check(parts.Select(p => p.Value).SequenceEqual(new[] { "141", "141" }) && Math.Abs(r3.Fraction - total / best) < 1e-4,
              "one scale: the sums in game units (" + string.Join(" ", parts.Select(p => p.Title + " " + p.Value)) + "), the bar against the best three of the shown lands (" + total + " of " + best + ")");
        Check(r3.Text == "first wears off after 25 min", "duration: the shortest burn time of the three (" + r3.Text + ")");
        Check(r3.Items.Where(i => i.Kind == PanelModel.ComboDishKind).Select(i => i.Title).SequenceEqual(new[] { "Wolf Meat Skewer", "Eyescream", "Mountains feast" })
              && row.Items.Where(i => i.Kind == PanelModel.ComboDishKind).All(i => string.IsNullOrEmpty(i.Title)) && row.Text == "Pick foods below",
              "pinned (0.9.6): each filled slot carries its food's name for its hover (no names under the slots), an empty slot none; nothing picked: \"Pick foods below\"");
        Check(PanelModel.PlanWearsOff(new[] { 4200f, 5000f }) == "first wears off after 1 h 10 min" && PanelModel.PlanWearsOff(new[] { 900f }) == "wears off after 15 min" && PanelModel.PlanWearsOff(new float[0]) == null,
              "duration: an hour or more in hours and minutes, one food \"wears off\", none no words");
        var bestThree = shown.OrderByDescending(f => f.Total).Take(3).Select(f => f.Prefab).ToList();
        var full = Row(PanelModel.Build(rowan, State(string.Join(",", bestThree))));
        Check(Math.Abs(full.Fraction - 1f) < 1e-4, "one scale: the best three foods of the shown lands fill the track (" + string.Join(", ", bestThree) + ")");
        var picked = Foods(three).Where(f => f.Selected).Select(f => f.Id).OrderBy(x => x).ToList();
        Check(picked.SequenceEqual(new[] { "Eyescream", "FeastMountains", "WolfMeatSkewer" }), "three: the picked foods' tiles are marked (Selected)");
        var skewerSlot = r3.Items.First(i => i.Id == "WolfMeatSkewer");
        var st = State("WolfMeatSkewer,Eyescream,FeastMountains"); PanelModel.Follow(st, skewerSlot.Link);
        Check(PanelModel.PlanSlots(st).SequenceEqual(new[] { "Eyescream", "FeastMountains" }), "slots: a click on a filled slot empties it");
        var tile = Foods(three).First(f => f.Id == "OnionSoup"); PanelModel.Follow(st = State("WolfMeatSkewer,Eyescream,FeastMountains"), tile.Link);
        Check(PanelModel.PlanSlots(st).SequenceEqual(new[] { "Eyescream", "FeastMountains", "OnionSoup" }), "slots: a fourth food replaces the oldest");

        // ---------- unknown foods ----------
        var unknown = Row(PanelModel.Build(rowan, State("NoSuchFood,CarrotSoup")));
        Check(unknown.Items.Count(i => i.Kind == PanelModel.ComboDishKind && i.Tone != PanelModel.PlanEmptyTone) == 1 && unknown.Title == "Carrot Soup", "unknown: a food the game's data does not know is left out of the slots");
        Check(!Foods(empty).Any(f => f.Id == "HwSampleCake") && !Lands(empty).Any(l => l.Title == PanelModel.PlanOtherLand), "unknown: a mod's food no land claims is not shown when you never ate it");
        var ate = PanelSample.Full(now); SessionEvents.Add(ate.Events.AteFoodMadeBy, "Rowan|HwSampleCake", 1);
        var other = PanelModel.Build(ate, State());
        Check(Lands(other).LastOrDefault()?.Title == PanelModel.PlanOtherLand && Grids(other).Last().Items.Select(i => i.Id).SequenceEqual(new[] { "HwSampleCake" }),
              "unknown: eaten, it stands under Other, after the lands");
        var noData = PanelSample.Full(now); noData.Foods = null;
        Check(!PanelModel.Content(PanelModel.Build(noData, State())).Any(b => b.Id == PanelModel.MealPlanId), "unknown: no food data, no planner");

        // ---------- a combination opens Meal plan with its foods ----------
        var load = On(PanelModel.MealsPageId, "Honey");
        var loadFrom = PanelModel.Build(rowan, load); PanelModel.Visited(load, loadFrom.Active, loadFrom.Page);
        var combo = PanelModel.Content(loadFrom).First(b => b.Kind == "sources" && b.Tone == PanelModel.ComboTone && b.Id != PanelModel.MealPlanId).Items.First();
        PanelModel.Follow(load, combo.Link);
        var loadedV = PanelModel.Build(rowan, load); var loaded = Row(loadedV);
        Check(combo.Link != null && load.Chapter == Chapter.Deeds && load.PageOf(Chapter.Deeds) == PanelModel.MealPlanPageId && loadedV.Page == PanelModel.MealPlanPageId
              && PanelModel.PlanSlots(load).SequenceEqual(new[] { "CarrotSoup", "Honey", "QueensJam" }) && loaded.Title == "Carrot Soup, Honey, Queen's Jam" && loadedV.Back != null,
              "load: a row of Your combinations on Meals opens Meal plan with its foods in the slots, Back leads to Meals (" + loaded?.Title + ")");

        // ---------- the keys, the islands, the fellow ----------
        var ks = State(); var v = PanelModel.Build(rowan, ks);
        Check(PanelModel.StepPlanFood(ks, v, 1) && ks.View[PanelModel.MealPlanCursorKey] == Foods(v)[0].Id && PanelModel.StepPlanFood(ks, v, 1) && ks.View[PanelModel.MealPlanCursorKey] == Foods(v)[1].Id
              && PanelModel.StepPlanFood(ks, v, -1) && PanelModel.StepPlanFood(ks, v, -1) && ks.View[PanelModel.MealPlanCursorKey] == Foods(v).Last().Id,
              "keys: A/D move the cursor over the foods, the first press to the first (wraps)");
        Check(PanelModel.TogglePlanFood(ks, v) && PanelModel.PlanSlots(ks).SequenceEqual(new[] { Foods(v).Last().Id }) && PanelModel.TogglePlanFood(ks, v) && PanelModel.PlanSlots(ks).Count == 0,
              "keys: Enter puts the cursor's food in a slot, again takes it out");
        var vk = PanelModel.Build(rowan, ks);
        Check(Foods(vk).Count(f => f.Cursor) == 1 && vk.Keys.Contains(PanelModel.PlanKeyLine) && vk.Keys.Contains(PanelModel.PlanPickKeyLine) && vk.Keys.Contains("[Q/E] Chapter"), "keys: the cursor's food marked, the key line says A/D and Enter");
        var fresh = State(); var noCursor = PanelModel.Build(rowan, fresh);
        Check(PanelModel.TogglePlanFood(fresh, noCursor) && fresh.View[PanelModel.MealPlanCursorKey] == Foods(noCursor)[0].Id && PanelModel.PlanSlots(fresh).Count == 0,
              "keys: Enter with no cursor yet puts it on the first food, picks nothing");
        var plate = PanelModel.PlateOf(empty); var islands = PanelModel.Islands(plate);
        Check(plate.Items.FirstOrDefault()?.Kind == "featband" && islands.Count == 2 && islands[0].Pin && islands[0].Parts.Count == 1 && islands[0].Parts[0].Block.Kind == "sources" && islands[0].Parts[0].Block.Id == PanelModel.MealPlanId
              && !islands[1].Pin && islands[1].Parts.Count(p => p.Block.Id == PanelModel.MealPlanId || p.Block.Id == PanelModel.MealPlanBestKey) == 2 + 2 * Lands(empty).Count,
              "pinned: the strip leads the plate, the meal (slots, summed bar, its numbers, wears off) is the first island and pinned with it; Best for, its line and the lands' foods one island under it");
        Check(PanelModel.ToJson(empty, true).Contains("\"span\":\"wide\",\"pin\":1"), "pinned: the dump marks the pinned island for the preview");
        Check(!PanelModel.Islands(PanelModel.PlateOf(meals)).Any(i => i.Pin), "pinned: no island on Meals is pinned");
        var three3 = PanelModel.Islands(PanelModel.PlateOf(PanelModel.Build(rowan, State("WolfMeatSkewer,Eyescream,FeastMountains"))))[0].Parts[0].Block.Items[0];
        Check(three3.Items.Count(i => i.Kind == "part") == 2 && three3.Text == "first wears off after 25 min" && three3.Items.Count(i => i.Kind == PanelModel.ComboDishKind) == 3,
              "pinned: the pinned island holds the three slots, the bar's parts with their real numbers and the first to wear off");
        Check(!PanelModel.RememberedViews.Any(k => k.StartsWith("Deeds/meals/plan", StringComparison.Ordinal)), "prefs: no open or shut kept any more (the planner is a page)");
        Check(!PanelPrefs.ToJson(State(best: "eitr")).Contains(PanelModel.MealPlanBestKey), "Best for: view state for the session, not kept in the prefs");

        // ---------- 0.9.3: what you can eat (the game: a Consumable with m_food above 0) ----------
        PanelModel.FoodData T(string type, float h, float s = 10) => new PanelModel.FoodData { Prefab = "X", Token = "$x", Type = type, Health = h, Stamina = s };
        Check(PanelModel.IsEdible(T("Consumable", 30)) && !PanelModel.IsEdible(T("Material", 30)) && !PanelModel.IsEdible(T("Consumable", 0, 40)) && PanelModel.IsEdible(T(null, 30)),
              "edible: a Consumable with a food value; a Material (an uncooked pie, raw meat) never, a mead (no food value) never; the sample's type-less dishes by their value");
        var ash7 = With(all7, (0, "Meadows"), (30, "AshLands"), (60, "AshLands"));
        var ashFoods = Foods(PanelModel.Build(ash7, State())).Select(f => f.Id).ToList();
        Check(ashFoods.Contains("PiquantPie") && !ashFoods.Contains("PiquantPieUncooked") && !ashFoods.Contains("AsksvinTail") && ashFoods.Contains("MarinatedGreens"),
              "edible: the Ashlands' cooked Piquant Pie is there; the uncooked pie and the raw Asksvin Tail (Materials with values) are not (" + string.Join(", ", ashFoods) + ")");
        var extra = With(all7, (0, "Meadows"), (30, "AshLands"));
        extra.Foods = () => MealPlanSample.Foods.Concat(new[] { new PanelModel.FoodData { Prefab = "RoastedCrustPieUncooked", Token = "$item_roastedcrustpie_uncooked", Type = "Material", Health = 75, Stamina = 25, Seconds = 1800, From = new List<string> { "$item_asksvin_meat" } },
                                                                new PanelModel.FoodData { Prefab = "RawVoltureMeat", Token = "$item_volture_meat", Type = "Material", Health = 20, Stamina = 20, Seconds = 1200 } }).ToList();
        Check(!Foods(PanelModel.Build(extra, State())).Any(f => f.Id == "RoastedCrustPieUncooked" || f.Id == "RawVoltureMeat"), "edible: an uncooked pie token and a raw meat token from the game's data are left out");

        // ---------- 0.9.3: Best for (sort + filter, one control), one scale ----------
        var anyV = PanelModel.Build(ash7, State()); var eitrSt = State(best: "eitr"); var eitrV = PanelModel.Build(ash7, eitrSt);
        var chips = Chips(eitrV);
        Check(chips != null && string.Join(" ", chips.Items.Select(c => (c.Selected ? "*" : "") + c.Title)) == "Any Health Stamina *Eitr" && chips.Title == PanelModel.PlanBestCaption,
              "Best for: the book's pill row, Any · Health · Stamina · Eitr, the chosen one filled");
        var eitrFoods = Foods(eitrV).Select(f => Food(f.Id)).ToList();
        Check(eitrFoods.Count > 0 && eitrFoods.All(f => f.Eitr >= f.Health && f.Eitr >= f.Stamina) && eitrFoods.Count < Foods(anyV).Count
              && !Foods(anyV).Select(f => Food(f.Id)).Any(f => f.Eitr >= f.Health && f.Eitr >= f.Stamina && f.Eitr > 0 && !eitrFoods.Contains(f)),
              "Best for: Eitr shows exactly the foods whose largest value is eitr (" + string.Join(", ", eitrFoods.Select(f => f.Prefab)) + ")");
        Check(Grids(eitrV).All(g => g.Items.Select(i => Food(i.Id).Eitr).SequenceEqual(g.Items.Select(i => Food(i.Id).Eitr).OrderByDescending(x => x))),
              "Best for: each land's foods most eitr first");
        Check(Foods(eitrV).All(f => f.Items[0].Id == "eitr" && f.Items[0].Selected) && Foods(anyV).All(f => f.Items[0].Id == "health" && !f.Items.Any(p => p.Selected)),
              "Best for: the chosen boost's part first in every bar and marked; with Any the bars keep health, stamina, eitr");
        var mostAsh = Foods(anyV).Max(f => Food(f.Id).Total);
        Check(Foods(anyV).Concat(Foods(eitrV)).All(f => Math.Abs(f.Fraction2 - Food(f.Id).Total / mostAsh) < 1e-4) && Foods(anyV).Concat(Foods(eitrV)).Count(f => Math.Abs(f.Fraction2 - 1) < 1e-4) >= 1,
              "one scale: every tile against the biggest serving of the shown lands, the same whatever Best for shows");
        var back = chips.Items.First(c => c.Selected); PanelModel.Follow(eitrSt, PanelModel.SortLink(chips, back));
        var health = chips.Items.First(c => c.Id == "health"); var hs = State(best: "eitr"); PanelModel.Follow(hs, PanelModel.SortLink(chips, health));
        Check(PanelModel.PlanBestChoice(eitrSt) == PanelModel.PlanAny && PanelModel.PlanBestChoice(hs) == "health", "Best for: a click on the chosen pill goes back to Any, another pill picks its boost");
        var none2 = PanelModel.Build(rowan, State(best: "eitr"));
        Check(Grids(none2).Count == 0 && PanelModel.Content(none2).Any(b => b.Kind == "note" && b.Text == PanelModel.PlanNoneFor("Eitr")),
              "Best for: no food of the shown lands for it, one line in their place (" + PanelModel.PlanNoneFor("Eitr") + ")");
        var torBook = PanelSample.Full(now); torBook.IsSelf = false; var tor = PanelModel.Build(torBook, State());
        Check(!PanelModel.Content(tor).Any(b => b.Id == PanelModel.MealPlanId) && !tor.List.Any(l => l.Id == PanelModel.MealPlanPageId) && tor.Page != PanelModel.MealPlanPageId
              && !PanelModel.PlateOf(PanelModel.Build(torBook, On(PanelModel.MealsPageId))).Items.Any(b => b.Kind == "headlink" && b.Id == "Deeds/" + PanelModel.MealPlanPageId),
              "fellow: no Meal plan in a fellow's list, no planner, no chip to it on their Meals");
        var json = PanelModel.ToJson(three);
        Check(json.Contains("\"link\":\"view:Deeds/meals/plan=") && !PanelModel.ToJson(PanelModel.Build(noData, State())).Contains("\"link\":"), "dump: a click's target written as link, only when set");
        return fails;
    }

}
