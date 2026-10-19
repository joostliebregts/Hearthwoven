using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9.2 Deeds > Meals, "Plan a meal" (Joost: "I actually would love to be able to create my own meals theoretically ... it shows you the meals
    /// from the current biome that you're in ... and then you can sort of click them and see the stamina, the HP and the eitr together ... and it
    /// should also include all the feasts"; he approved the web prototype, "with in-game icons and it needs to fit the Hearthwoven UI grammar").
    /// An island on your own book between Meals by dish and Your combinations (0.9.5: its own page, below), in the book's own forms:
    ///
    ///   the meal    Your combinations' row form (sources, Tone MealPlanTone, its row Tone ComboTone, Id MealPlanId): the three slots as the row's dish pictures (an empty slot a
    ///               dashed square, Tone PlanEmptyTone; a filled one a click that empties it, Link), the bar the three foods' values summed in the
    ///               boosts' colours on one scale (the best three foods of the lands shown together = the full track), the line their values
    ///               ("Health 160") and "first wears off after 25 min" (the shortest of their burn times); no food yet: one line in the bar's place
    ///   the lands   a section per land (Id MealPlanId; "Ashlands", "where you are now"; "Mistlands", "before"; "Plains", "the one before") with its
    ///               foods as item tiles (itemgrid, Tone MealPlanTone): the game's picture, the name, a small bar of one serving on one scale for
    ///               the planner and its numbers ("15 · 45 · 25m"), What to use's status word ("can make", "made", "owned") or, not known yet,
    ///               the tile quiet (Tone "unknown"), still a click. A food in a slot is Selected; the keys' cursor is Cursor.
    ///
    /// Which lands: the land you are exploring (the furthest land of the land minutes this PC noted, DeedLog.Lands and the kept ones; with none,
    /// the furthest land you found, FoundBiomes), then the two lands before it in the game's progression that you have been in. A food's land
    /// comes from the game's data (FoodLand): a feast by its own land, else the latest land any of its own name, its recipe's ingredients and what
    /// a station makes it from point to (the food tokens' small table FoodLandKeys, then Armor sets' ingredient table), a cauldron dish at least
    /// the Black Forest. A food no land points to (a mod's) shows under "Other" only when you have eaten it. Every feast is a food with values.
    ///
    /// The slots live in PanelState.View (MealPlanKey, "Prefab,Prefab,Prefab", the oldest first): view state for this session, recorded nowhere.
    /// A click on a food puts it in the next slot, a fourth replaces the oldest, a click on a food already in (or on its slot) takes it out; every
    /// click is a plain view link (Follow), worked out here. A row of Your combinations is a click that loads its foods. On the page A/D move a
    /// cursor over the foods and Enter picks or drops the cursor's food (as on Foes). Values only from the game's data (PanelInput.Foods:
    /// m_food, m_foodStamina, m_foodEitr, m_foodBurnTime); the sample has sample values. Pure C#: test-panel/MealPlanTests.cs.
    ///
    /// 0.9.3 (Joost in game): the planner shuts (MealPlanOpenKey, kept between sessions; shut by default: the heading, a click with the opens mark, and
    /// the meal's row); open, a Best for pill row (Any · Health · Stamina · Eitr: sorts and filters, MealPlanBestKey) and compact tiles with the damage
    /// bar (MealPlanUi.cs). Only what the game lets you eat (IsEdible: a Consumable with m_food above 0), never an uncooked pie or raw meat.
    ///
    /// 0.9.5 (Joost in game): the planner left Meals for a page of its own, Deeds > Meal plan (MealPlanPage), with its meal pinned under the strip while the
    /// foods scroll; no heading and no open or shut any more. Meals keeps a strip chip to it; a combination opens it with its foods in the slots.
    ///
    /// 0.9.6 (Joost, the pinned meal: "let's ensure the food text doesn't need 2 lines. We know what we clicked. Icons only would be good. And then the bar
    /// all at one row height"): the pinned meal is one row (DamageRowsUi.PlanMealBar): the slots as pictures only (each dish's Title is its hover), the bar
    /// and its legend centred on their height, the island at the book's least padding; nothing picked, the empty track and PlanPick under it.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>One food of the game's data: its token and prefab, one serving's values, whether it is a feast, and what says where it comes from
        /// (From: its recipe's ingredients, what a station makes it from, and theirs one step on; Station: the recipe's crafting station's prefab).</summary>
        public sealed class FoodData
        {
            /// <summary>Type: the item's type as the game names it (ItemDrop.ItemData.ItemType, "Consumable", "Material"); null = not known (the sample's dishes).</summary>
            public string Token, Prefab, Station, Type;
            public float Health, Stamina, Eitr, Seconds;
            public bool Feast;
            public List<string> From = new List<string>();
            public double Total => Math.Max(0, Health) + Math.Max(0, Stamina) + Math.Max(0, Eitr);
        }

        public const string MealPlanTitle = "Plan a meal";
        public const string MealPlanId = "mealplan", MealPlanTone = "mealplan", PlanEmptyTone = "planempty", PlanUnknownTone = "unknown";
        /// <summary>The state key of the slots ("Prefab,Prefab,Prefab", the oldest first) and of the keys' cursor (a food's prefab).</summary>
        public const string MealPlanKey = "Deeds/meals/plan", MealPlanCursorKey = "Deeds/meals/plancursor";
        public const int MealPlanSlots = 3;
        public const string PlanPick = "Pick foods below";   // 0.9.6: in the pinned meal's legend place, under the empty track
        public const string PlanHere = "where you are now", PlanExplored = "where you explore", PlanFurthest = "the furthest you have been", PlanBefore = "before", PlanBeforeThat = "the one before";
        public const string PlanOtherLand = "Other", PlanOtherWords = "foods you have eaten that no land claims";
        public const string PlanCanMake = "can make", PlanKeyLine = "[A/D] Food", PlanPickKeyLine = "[Enter] Pick";

        // ---------- 0.9.3 (Joost in game: "lets make this more dense. and add a filter for primary stats (hp, stamina, eitr) to filter or sort. And make
        // the bargraph thicker. like in our damage graphs") ----------

        // ---------- 0.9.5 its own page (Joost in game, scrolling through the food tiles: "We should have an always visible anchored 'health/stamina/eitr'
        // bar when scrolling over this 'plan your meal' thing. Maybe we should make a 'mealplan' tab on the left menu btw.. separate it from meals.") ----------

        /// <summary>
        /// Deeds > Meal plan: the planner's own page, right under Meals in the Deeds list (your own book only: a fellow's list has no Meal plan). Its
        /// meal (the three slots, the summed bar with its numbers, "first wears off after 25 min") is the page's first island, pinned under the strip
        /// with it (Island.Pin, StickyHeadUi.PinStrip), so the bar stays in sight while the foods scroll under it; Best for, the marks' line and the
        /// lands' foods are the island under it. No window chips (the foods are the game's data, not a count), as Armor sets. Meals keeps a strip chip
        /// to it (MealPlanLink, the headlinks' grammar), and a row of Your combinations opens it with its foods in the slots (PlanLoadTarget).
        /// </summary>
        public const string MealPlanPageId = "mealplan", MealPlanLabel = "Meal plan", MealPlanIcon = "vocab:feat-scene-secondhelping";
        /// <summary>The click that opens Meal plan with these foods in the slots ("plan:A,B,C"): PanelModel.Follow.</summary>
        public const string PlanLoadTarget = "plan:";
        /// <summary>About these numbers on Meal plan: what the bar measures, where the values come from.</summary>
        public const string PlanAboutBarLabel = "The bar", PlanAboutValuesLabel = "The values";
        public const string PlanAboutBar = "A full bar is the best three foods of the lands shown here together. Each food's own bar is on one scale for the page: the biggest serving fills it.";
        public const string PlanAboutValues = "Each food's health, stamina and eitr and how long it lasts are the game's own food data; nothing here is counted or recorded. The slots last for this session.";
        public const string PlanNoFoods = "No food data yet", PlanNoFoodsLine = "The planner shows once you are in a world and the game has loaded its foods.";

        /// <summary>Meals' strip chip to Meal plan (a headlink, as Crafting's Sets).</summary>
        static Block MealPlanLink() => new Block { Kind = "headlink", Id = "Deeds/" + MealPlanPageId, Title = MealPlanTitle, Icon = MealPlanIcon };

        /// <summary>Whether this book has a Meal plan: your own (a fellow's copy carries no planner).</summary>
        public static bool HasMealPlan(PanelInput input) => input == null || input.IsSelf;

        /// <summary>Whether the game's food data is known here (a food to plan with): Meals' chip and a combination's click need it.</summary>
        static bool HasFoods(PanelInput input) => input != null && FoodsOf(input).Count > 0;

        /// <summary>
        /// Best for (a sort control in the book's pill row, Kind "sort", SortUi): Any (the default: strongest first) · Health · Stamina · Eitr. A boost
        /// shows only the foods where it is the largest of the three (a tie counts for both), strongest in it first, and puts its part first in their
        /// bars; a click on the chosen pill goes back to Any. View state for the session (PanelState.View, MealPlanBestKey), recorded nowhere.
        /// </summary>
        public const string MealPlanBestKey = "Deeds/meals/planbest", PlanBestCaption = "Best for", PlanAny = "any";
        static readonly (string id, string label)[] PlanBests = { (PlanAny, "Any"), ("health", "Health"), ("stamina", "Stamina"), ("eitr", "Eitr") };
        /// <summary>The key under the chips (Joost in game, 0.9.8: "why not show the actual icons there?"): a note of Tone PlanMarksTone whose Items
        /// are the tile's own marks, each drawn as on a tile (MealPlanUi.PlanMarksKey) and followed by its meaning: the gold dot (Note GearMade) made
        /// or owned, the ring (Note PlanCanMake) can make, a faded picture (Tone PlanUnknownTone, Icon a food of the page) not known yet.</summary>
        public const string PlanMarksTone = "planmarks", PlanMarkMade = "made or owned", PlanMarkCanMake = "can make", PlanMarkUnknown = "not known yet";
        /// <summary>Best for left no food in any land: one line in their place.</summary>
        public static string PlanNoneFor(string label) => "No food of these lands gives most " + label.ToLowerInvariant() + ".";

        /// <summary>The chosen Best for: health, stamina, eitr or any.</summary>
        public static string PlanBestChoice(PanelState s) => s != null && s.View.TryGetValue(MealPlanBestKey, out var v) && PlanBests.Any(b => b.id == v) ? v : PlanAny;

        /// <summary>
        /// The game's own rule for a food you can eat (assembly_valheim, Humanoid.CanConsumeItem and Player.ConsumeItem): only an item of type
        /// Consumable can be consumed, and only one with m_food above 0 is eaten as a food (Player.EatFood); a mead (no food value) is not. An uncooked
        /// pie or raw meat is a Material: left out, whatever values its data carries. Type not known (the sample): the food value alone decides.
        /// </summary>
        public static bool IsEdible(FoodData f) => f != null && (f.Type == null || f.Type == "Consumable") && f.Health > 0;

        /// <summary>A food's value for a boost (health, stamina, eitr); its total for any.</summary>
        static double BoostOf(FoodData f, string best) => best == "health" ? f.Health : best == "stamina" ? f.Stamina : best == "eitr" ? f.Eitr : f.Total;

        /// <summary>Whether <paramref name="best"/> is the largest of a food's three values (any: every food).</summary>
        public static bool BestFor(FoodData f, string best)
        {
            if (f == null) return false;
            if (best == null || best == PlanAny) return true;
            var v = BoostOf(f, best);
            return v > 0 && v >= f.Health && v >= f.Stamina && v >= f.Eitr;
        }

        /// <summary>A land's foods in the planner's order: with a boost chosen only the foods it is best for, strongest in it first; then (and with Any)
        /// the strongest overall, then by name.</summary>
        public static List<FoodData> PlanOrder(IEnumerable<FoodData> foods, string best, Func<FoodData, string> name) =>
            (foods ?? new FoodData[0]).Where(f => BestFor(f, best)).OrderByDescending(f => best == PlanAny ? 0 : BoostOf(f, best)).ThenByDescending(f => f.Total)
                                      .ThenBy(f => name(f), StringComparer.Ordinal).ToList();

        // ---------- the land of a food ----------

        // the land a food token points to, by its vanilla name; the latest land first, so "mushroomyellow" is the Black Forest and not the Meadows'
        // "mushroom". Checked before Armor sets' ingredient table (LandOfIngredient), which knows the hides, metals and creature parts.
        static readonly (int land, string[] keys)[] FoodLandKeys =
        {
            (6, new[] { "vineberr", "smokepuff", "fiddlehead", "volture", "asksvin", "bonemaw" }),
            (5, new[] { "hare", "bug_meat", "jotunpuff", "magecap", "royaljelly", "seeker" }),
            (4, new[] { "cloudberr", "barley", "chicken", "lox", "bread" }),
            (3, new[] { "onion", "wolf", "freezegland", "freeze_gland", "serpent" }),
            (2, new[] { "turnip", "entrails", "bloodbag", "ooze", "pukeberr" }),
            (1, new[] { "carrot", "blueberr", "thistle", "mushroomyellow" }),
            (0, new[] { "raspberr", "honey", "mushroom", "boar", "deer", "neck", "fish" }),
        };

        /// <summary>The land one token (a food's own, an ingredient's) points to, an index of ArmorLands; -1 when its name marks none.</summary>
        public static int LandOfFoodToken(string token)
        {
            var t = (token ?? "").ToLowerInvariant();
            if (t.Length == 0) return -1;
            foreach (var e in FoodLandKeys) foreach (var k in e.keys) if (t.Contains(k)) return e.land;
            return LandOfIngredient(t);
        }

        /// <summary>A feast's land by its prefab ("FeastMountains"): an index of ArmorLands, -1 when the name says none (the Ocean's feast: its ingredients say).</summary>
        public static int FeastLand(string prefab)
        {
            var p = (prefab ?? "").ToLowerInvariant();
            if (!p.StartsWith("feast")) return -1;
            var land = p.Substring(5).TrimStart('_');
            switch (land)
            {
                case "meadows": return 0; case "blackforest": return 1; case "swamps": case "swamp": return 2; case "mountains": case "mountain": return 3;
                case "plains": return 4; case "mistlands": return 5; case "ashlands": return 6; default: return -1;
            }
        }

        /// <summary>
        /// A food's land (an index of ArmorLands; -1 = no land points to it, a mod's food): a feast by its own land; else the latest land its own
        /// token and prefab, its recipe's ingredients and what a station makes it from point to; a dish of the cauldron at least the Black Forest
        /// (the cauldron is built there), when anything points to a land at all.
        /// </summary>
        public static int FoodLand(FoodData f)
        {
            if (f == null) return -1;
            if (f.Feast) { var fl = FeastLand(f.Prefab); if (fl >= 0) return fl; }
            var land = new[] { f.Token, f.Prefab }.Concat(f.From ?? new List<string>()).Select(LandOfFoodToken).DefaultIfEmpty(-1).Max();
            if (land >= 0 && (f.Station ?? "").ToLowerInvariant().Contains("cauldron")) land = Math.Max(land, 1);
            return land;
        }

        // ---------- which lands ----------

        /// <summary>A biome key of the game ("Mountain", "AshLands") as an index of ArmorLands; -1 for the Ocean, none or a land not on the way.</summary>
        static int LandIndex(string biome) => Array.IndexOf(ArmorLands, BiomeName(biome));

        /// <summary>
        /// The lands the planner shows, in order, with their words: the land you are exploring (the furthest land of the land minutes this PC noted
        /// this session and kept from earlier ones: "where you are now" when your latest minute is there, else "where you explore"; with no minutes the
        /// furthest land you found, "the furthest you have been"), then the two lands before it in the game's progression that you have been in
        /// ("before", "the one before"). No land known at all: the Meadows.
        /// </summary>
        public static List<(int land, string words)> PlanLands(PanelInput input)
        {
            var been = new HashSet<int>(FoundBiomes(input).Select(LandIndex).Where(i => i >= 0));
            var minutes = new List<(string at, int land)>();
            void Read(Dictionary<string, float> lands)
            {
                if (lands == null) return;
                foreach (var kv in lands)
                {
                    var bar = kv.Key.IndexOf('|'); if (bar < 0 || kv.Value <= 0) continue;
                    var l = LandIndex(kv.Key.Substring(bar + 1)); if (l >= 0) minutes.Add((kv.Key.Substring(0, bar), l));
                }
            }
            if (input?.Deeds != null) { Read(input.Deeds.Lands); Read(input.Deeds.EarlierLands); }
            foreach (var m in minutes) been.Add(m.land);
            int anchor; string words;
            if (minutes.Count > 0)
            {
                anchor = minutes.Max(m => m.land);
                var latest = minutes.OrderByDescending(m => m.at, StringComparer.Ordinal).ThenByDescending(m => m.land).First().land;
                words = latest == anchor ? PlanHere : PlanExplored;
            }
            else if (been.Count > 0) { anchor = been.Max(); words = PlanFurthest; }
            else { anchor = 0; words = PlanFurthest; }
            var list = new List<(int, string)> { (anchor, words) };
            var before = been.Where(l => l < anchor).OrderByDescending(l => l).Take(2).ToList();
            for (int k = 0; k < before.Count; k++) list.Add((before[k], k == 0 ? PlanBefore : PlanBeforeThat));
            return list;
        }

        // ---------- the slots ----------

        /// <summary>The slots as the state holds them: prefabs, the oldest first, at most MealPlanSlots, no food twice, empty parts left out.</summary>
        public static List<string> PlanSlots(PanelState s)
        {
            var raw = s != null && s.View.TryGetValue(MealPlanKey, out var v) ? v : null;
            return (raw ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).Take(MealPlanSlots).ToList();
        }

        /// <summary>The slots after a click on <paramref name="food"/>: already in, it comes out; else it goes in the next slot, and with every slot
        /// full it replaces the oldest. As the state's string ("A,B,C").</summary>
        public static string PlanAfter(IList<string> slots, string food)
        {
            var list = (slots ?? new List<string>()).Where(x => !string.IsNullOrEmpty(x)).ToList();
            if (string.IsNullOrEmpty(food)) return string.Join(",", list.ToArray());
            if (list.Contains(food)) list.Remove(food);
            else { list.Add(food); while (list.Count > MealPlanSlots) list.RemoveAt(0); }
            return string.Join(",", list.ToArray());
        }

        /// <summary>The click that opens Meal plan with these foods in the slots (a row of Your combinations): at most MealPlanSlots, in their order
        /// (PlanLoadTarget, worked out by Follow: the slots set, then the page opened as a headlink opens one, so Back leads to Meals).</summary>
        public static string PlanLoadLink(IEnumerable<string> foods) =>
            PlanLoadTarget + string.Join(",", (foods ?? new string[0]).Where(f => !string.IsNullOrEmpty(f)).Distinct(StringComparer.Ordinal).Take(MealPlanSlots).ToArray());

        /// <summary>A click on a food or a filled slot: the slots after it (PlanAfter).</summary>
        static string PlanLink(IList<string> slots, string food) => ViewTarget + MealPlanKey + "=" + PlanAfter(slots, food);

        /// <summary>How long until the first of these foods wears off, in words: "first wears off after 25 min" ("1 h 10 min" from an hour); null for none.</summary>
        public static string PlanWearsOff(IEnumerable<float> seconds)
        {
            var known = (seconds ?? new float[0]).Where(x => !float.IsNaN(x) && x > 0).ToList();
            if (known.Count == 0) return null;
            var m = (int)Math.Round(known.Min() / 60.0, MidpointRounding.AwayFromZero); if (m < 1) m = 1;
            return (known.Count == 1 ? "wears off after " : "first wears off after ") + (m < 60 ? m + " min" : m / 60 + " h" + (m % 60 > 0 ? " " + m % 60 + " min" : ""));
        }

        /// <summary>The best total of three different foods of <paramref name="foods"/> (fewer foods: all of them): the planner's full track.</summary>
        public static double PlanBest(IEnumerable<FoodData> foods) =>
            (foods ?? new FoodData[0]).Where(f => f != null).GroupBy(f => f.Prefab).Select(g => g.First().Total).OrderByDescending(t => t).Take(MealPlanSlots).Sum();

        // ---------- status ----------

        /// <summary>What you know of a food, as What to use's status word (GearStatus): in your bag or picked up "owned", made "made", its recipe
        /// known "can make"; null = not known yet (the tile is quiet, still a click).</summary>
        public static string FoodStatus(PanelInput input, FoodData f)
        {
            if (input == null || f == null) return null;
            bool Ask(Func<string, bool> q, string t) { try { return q != null && !string.IsNullOrEmpty(t) && q(t); } catch { return false; } }
            if (Ask(input.Owned, f.Token)) return GearOwned;
            if (Has(input.ItemsCrafted, f.Token)) return GearMade;
            if (Has(input.ItemsPickedUp, f.Token) || Has(input.Events?.PickedUp, f.Token)) return GearOwned;
            if (Ask(input.RecipeKnown, f.Token)) return PlanCanMake;
            return null;
        }

        // ---------- the island ----------

        /// <summary>The food data as the input gives it (PanelInput.Foods), each prefab once, only foods with a value; empty when not known.</summary>
        static List<FoodData> FoodsOf(PanelInput input)
        {
            IList<FoodData> all = null;
            try { all = input?.Foods?.Invoke(); } catch { }
            return (all ?? new List<FoodData>()).Where(f => f != null && !string.IsNullOrEmpty(f.Prefab) && IsEdible(f) && f.Total > 0 && !double.IsNaN(f.Total) && !double.IsInfinity(f.Total))
                                                .GroupBy(f => f.Prefab, StringComparer.Ordinal).Select(g => g.First()).ToList();
        }

        /// <summary>The foods this book ate (item prefabs), from its meals since install, its feast servings and its combinations.</summary>
        static HashSet<string> EatenFoods(PanelInput input)
        {
            var eaten = new HashSet<string>(StringComparer.Ordinal);
            var ev = input?.Events;
            if (ev != null)
            {
                foreach (var kv in ev.AteFoodMadeBy) if (kv.Value > 0) eaten.Add(Split2(kv.Key)[1]);
                foreach (var kv in ev.AteFromFeastOf) if (kv.Value > 0) eaten.Add(Split2(kv.Key)[1]);
            }
            if (input?.MealCombos != null) foreach (var c in input.MealCombos.Combos.Values) foreach (var f in c.Foods ?? new string[0]) eaten.Add(f);
            return eaten;
        }

        /// <summary>
        /// The planner's blocks on your own book (see the class summary): the meal's row (pinned under the strip, Islands), then Best for, the marks'
        /// line and each land's foods. None on a fellow's book or when the game's food data is not known.
        /// </summary>
        public static List<Block> MealPlanBlocks(PanelInput input, PanelState state)
        {
            var blocks = new List<Block>();
            if (input == null || !input.IsSelf) return blocks;
            var foods = FoodsOf(input);
            if (foods.Count == 0) return blocks;
            var byPrefab = foods.ToDictionary(f => f.Prefab, StringComparer.Ordinal);
            var eaten = EatenFoods(input);
            var lands = PlanLands(input);
            var groups = new List<(string title, string words, List<FoodData> foods)>();
            var landOf = foods.ToDictionary(f => f.Prefab, FoodLand, StringComparer.Ordinal);
            var bestFor = PlanBestChoice(state);
            string Name(FoodData f) => DishTitle(input, f.Prefab);
            foreach (var (land, words) in lands)
            {
                var here = PlanOrder(foods.Where(f => landOf[f.Prefab] == land), PlanAny, Name);
                if (here.Count > 0) groups.Add((LandName(land), words, here));
            }
            var other = PlanOrder(foods.Where(f => landOf[f.Prefab] < 0 && eaten.Contains(f.Prefab)), PlanAny, Name);
            if (other.Count > 0) groups.Add((PlanOtherLand, PlanOtherWords, other));
            var shown = groups.SelectMany(g => g.foods).ToList();   // every food of the shown lands, whatever Best for shows: the one scale
            // the slots: only foods the game's data knows (a combination's mod food that is gone, a hand-made state: left out)
            var slots = PlanSlots(state).Where(byPrefab.ContainsKey).ToList();
            var best = Math.Max(PlanBest(shown), 0);
            var picked = slots.Select(p => byPrefab[p]).ToList();
            double h = picked.Sum(f => Math.Max(0, f.Health)), s = picked.Sum(f => Math.Max(0, f.Stamina)), e = picked.Sum(f => Math.Max(0, f.Eitr)), total = h + s + e;
            var scale = Math.Max(best, total);

            var dishes = new List<Block>();
            for (int k = 0; k < MealPlanSlots; k++)
            {
                if (k < picked.Count)
                    dishes.Add(new Block { Kind = ComboDishKind, Id = picked[k].Prefab, Icon = "item:" + picked[k].Prefab, Title = DishTitle(input, picked[k].Prefab), Link = PlanLink(slots, picked[k].Prefab) });
                else dishes.Add(new Block { Kind = ComboDishKind, Id = "", Icon = "", Title = "", Tone = PlanEmptyTone });
            }
            var row = new Block
            {
                Kind = "source", Id = MealPlanId, Tone = ComboTone, Title = string.Join(", ", picked.Select(f => DishTitle(input, f.Prefab)).ToArray()), Icon = "",
                Value = "", Fraction = scale > 0 ? (float)(total / scale) : 0, Fraction2 = scale > 0 ? (float)(total / scale) : 0,
                Text = picked.Count == 0 ? PlanPick : PlanWearsOff(picked.Select(f => f.Seconds)),
                Items = dishes.Concat(ComboParts((float)h, (float)s, (float)e)).ToList(),
            };
            blocks.Add(new Block { Kind = "sources", Id = MealPlanId, Tone = MealPlanTone, Title = "", Items = new List<Block> { row } });   // 0.9.5: its scale's line in About these numbers (the pinned meal kept short)

            // Best for: the book's pill row (SortUi); the chosen pill's click goes back to Any
            blocks.Add(new Block
            {
                Kind = "sort", Id = MealPlanBestKey, Title = PlanBestCaption,
                // a boost's pill carries its part's colour (the bars' Health, Stamina, Eitr): a swatch before the word (SortUi, PanelUi.Chip)
                Items = PlanBests.Select(b => new Block { Kind = "view", Id = b.id, Title = b.label, Selected = b.id == bestFor, Colour = b.id == PlanAny ? null : CategoryColour(b.id),
                                                          Link = b.id == bestFor && b.id != PlanAny ? ViewTarget + MealPlanBestKey + "=" + PlanAny : null }).ToList(),
            });
            // the key of the tiles' marks: the marks themselves, each with its meaning; the faded sample a food of the page not known yet (else the first)
            var sample = shown.FirstOrDefault(f => FoodStatus(input, f) == null) ?? shown.FirstOrDefault();
            blocks.Add(new Block
            {
                Kind = "note", Id = MealPlanId, Tone = PlanMarksTone,
                Items = new List<Block>
                {
                    new Block { Kind = "mark", Note = GearMade, Title = PlanMarkMade },
                    new Block { Kind = "mark", Note = PlanCanMake, Title = PlanMarkCanMake },
                    new Block { Kind = "mark", Tone = PlanUnknownTone, Icon = sample == null ? "" : "item:" + sample.Prefab, Title = PlanMarkUnknown },
                },
            });

            // the foods: one scale for every tile of the planner (the biggest serving of the shown lands the full track), the damage bars' form
            var most = shown.Select(f => f.Total).DefaultIfEmpty(0).Max();
            var any = false;
            foreach (var g in groups)
            {
                var these = PlanOrder(g.foods, bestFor, Name);
                if (these.Count == 0) continue;
                any = true;
                blocks.Add(new Block { Kind = "section", Id = MealPlanId, Title = g.title, Text = g.words });
                blocks.Add(new Block
                {
                    Kind = "itemgrid", Id = MealPlanId, Tone = MealPlanTone,
                    Items = these.Select(f =>
                    {
                        var status = FoodStatus(input, f);
                        return new Block
                        {
                            Kind = "food", Id = f.Prefab, Icon = "item:" + f.Prefab, Title = DishTitle(input, f.Prefab), Value = ServingWords(f), Text = ServingTime(f.Seconds),
                            Fraction2 = most > 0 ? (float)Math.Min(1, f.Total / most) : 0,
                            Note = status, Tone = status == null ? PlanUnknownTone : null, Selected = slots.Contains(f.Prefab), Link = PlanLink(slots, f.Prefab),
                            Items = TileParts(f, bestFor),
                        };
                    }).ToList(),
                });
            }
            if (!any) blocks.Add(new Block { Kind = "note", Id = MealPlanId, Text = PlanNoneFor(PlanBests.First(b => b.id == bestFor).label) });
            return blocks;
        }

        /// <summary>A tile's bar parts: one per boost the food has, as long as its value, in Your combinations' colours, with its number inside (the damage
        /// bars' form, not a combination's numberless part); the chosen boost first and marked (Selected).</summary>
        public static List<Block> TileParts(FoodData f, string best)
        {
            var parts = ComboParts(f.Health, f.Stamina, f.Eitr);
            foreach (var p in parts) { p.Tone = null; p.Selected = p.Id == best; }
            return parts.OrderByDescending(p => p.Selected).ToList();   // a stable sort: the others keep health, stamina, eitr
        }

        /// <summary>One serving's numbers in words, for the tile's hover and the dump ("55 · 55", eitr only when there is some).</summary>
        static string ServingWords(FoodData f)
        {
            var said = new List<string> { ComboValue(f.Health), ComboValue(f.Stamina) };
            if (f.Eitr > 0) said.Add(ComboValue(f.Eitr));
            return string.Join(" · ", said.ToArray());
        }

        // ---------- the page ----------

        /// <summary>Deeds > Meal plan (see MealPlanPageId): the planner's blocks (the meal first, pinned under the strip), About these numbers (what the bar
        /// measures, where the values come from). No window chips (WindowsOf: none), no hero: the meal is what the page is.</summary>
        static void MealPlanPage(PanelInput input, PanelState state, PanelView view)
        {
            view.Heading = MealPlanLabel;
            var blocks = MealPlanBlocks(input, state);
            if (blocks.Count == 0) view.Blocks.Add(Empty(PlanNoFoods, PlanNoFoodsLine));
            else view.Blocks.AddRange(blocks);
            if (input != null && input.IsSelf)
                view.AboutNumbers = new Block
                {
                    Kind = "aboutnumbers", Id = NumbersTarget, Title = AboutNumbersTitle,
                    Items = new List<Block> { new Block { Kind = "aboutline", Title = PlanAboutBarLabel, Text = PlanAboutBar },
                                              new Block { Kind = "aboutline", Title = PlanAboutValuesLabel, Text = PlanAboutValues } },
                };
            Plate(view, MealPlanIcon);
        }

        /// <summary>Follow's step for PlanLoadTarget ("plan:A,B,C"): the slots set to those foods and Meal plan opened (as a headlink opens a page, so the
        /// strip's Back leads to where the click came from). false: another target.</summary>
        static bool FollowPlanLoad(PanelState s, string target)
        {
            if (target == null || !target.StartsWith(PlanLoadTarget, StringComparison.Ordinal)) return false;
            s.View[MealPlanKey] = target.Substring(PlanLoadTarget.Length);
            s.View.Remove(MealPlanCursorKey);
            Jump(s, Chapter.Deeds + "/" + MealPlanPageId);
            return true;
        }

        // ---------- the keys (as on Foes: Chapters/FoesKeys.cs) ----------

        /// <summary>The planner's food tiles of the page on screen, in order; none on any other page.</summary>
        public static List<Block> VisiblePlanFoods(PanelView v)
        {
            if (v == null || v.ShowAbout || v.Active != Chapter.Deeds || v.Page != MealPlanPageId) return new List<Block>();
            return Content(v).Where(b => b.Kind == "itemgrid" && b.Tone == MealPlanTone).SelectMany(b => b.Items ?? new List<Block>()).Where(i => i.Kind == "food").ToList();
        }

        static string PlanCursor(PanelState s) => s != null && s.View.TryGetValue(MealPlanCursorKey, out var c) ? c : null;

        /// <summary>A/D on Meal plan: the cursor moves to the next food (wraps); the first press puts it on the first food (A: the last). false: no foods
        /// shown (A/D turn the chapters then).</summary>
        public static bool StepPlanFood(PanelState s, PanelView v, int d)
        {
            var stops = VisiblePlanFoods(v);
            if (stops.Count == 0) return false;
            var at = stops.FindIndex(f => f.Id == PlanCursor(s));
            s.View[MealPlanCursorKey] = stops[at >= 0 ? ((at + d) % stops.Count + stops.Count) % stops.Count : d < 0 ? stops.Count - 1 : 0].Id;
            return true;
        }

        /// <summary>Enter, Space or the pad's A on Meal plan: on the cursor's food it puts it in a slot, or takes it out when it is in; with no cursor yet
        /// (or on a food Best for hides) the cursor goes to the first food. false: no foods shown.</summary>
        public static bool TogglePlanFood(PanelState s, PanelView v)
        {
            var stops = VisiblePlanFoods(v);
            if (stops.Count == 0) return false;
            var stop = stops.FirstOrDefault(f => f.Id == PlanCursor(s));
            if (stop == null) { s.View[MealPlanCursorKey] = stops[0].Id; return true; }
            s.View[MealPlanKey] = PlanAfter(PlanSlots(s), stop.Id);
            return true;
        }

        /// <summary>Build's last step on Meal plan: the cursor's food is marked (Cursor) and the key line says what A/D and Enter do (Q/E turn the chapters).</summary>
        static void MealPlanFinish(PanelState state, PanelView view)
        {
            var stops = VisiblePlanFoods(view);
            if (stops.Count == 0) return;
            var cursor = PlanCursor(state);
            if (!string.IsNullOrEmpty(cursor)) foreach (var f in stops) if (f.Id == cursor) f.Cursor = true;
            if (state.FilterRow >= 0) return;
            var at = view.Keys.IndexOf("[Q/E·A/D] Chapter");
            if (at >= 0) view.Keys[at] = "[Q/E] Chapter";
            var page = view.Keys.IndexOf("[W/S] Page");
            view.Keys.InsertRange(page >= 0 ? page + 1 : view.Keys.Count, new[] { PlanKeyLine, PlanPickKeyLine });
        }
    }
}
