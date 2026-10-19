using System;
using System.Collections.Generic;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 Meals' Your combinations (Chapters/MealCombosModel.cs): Rowan's combinations, recorded on this PC from four days before the sample's today
    /// (so 7 days says from when it counts them). The game's food values as the game gives them (Carrot Soup 15 health and 45 stamina, Fish Wraps 70
    /// and 23, ...), summed per combination; three of the five most eaten lean on stamina. Variants for the previews and tests: "empty" (recording
    /// began an hour ago, nothing yet) and "notrecorded" (no record on this book). Fictional numbers.
    /// </summary>
    public static class MealCombosSample
    {
        // the game's food values (health, stamina, eitr) of the sample's dishes
        static readonly Dictionary<string, (float h, float s, float e)> Food = new Dictionary<string, (float, float, float)>
        {
            ["CarrotSoup"] = (15, 45, 0), ["QueensJam"] = (14, 40, 0), ["Honey"] = (8, 35, 0), ["Bread"] = (23, 70, 0), ["Raspberry"] = (7, 20, 0),
            ["CookedMeat"] = (30, 10, 0), ["Sausages"] = (55, 18, 0), ["FishWraps"] = (70, 23, 0), ["FeastMeadows"] = (35, 35, 0),
        };

        /// <summary>Rowan's record: (foods, meals per day back from today: index 0 = today).</summary>
        static readonly (string[] foods, int[] perDay)[] Rowan =
        {
            (new[] { "CarrotSoup", "QueensJam", "Honey" }, new[] { 2, 2, 0, 3 }),
            (new[] { "Bread", "CarrotSoup", "Raspberry" }, new[] { 1, 0, 2, 0, 2 }),
            (new[] { "CookedMeat", "Sausages", "FishWraps" }, new[] { 0, 2, 2 }),
            (new[] { "Bread", "CarrotSoup", "Honey" }, new[] { 0, 0, 0, 3 }),
            (new[] { "CarrotSoup", "FishWraps", "Sausages" }, new[] { 1, 0, 0, 0, 2 }),
            (new[] { "FeastMeadows", "QueensJam" }, new[] { 0, 0, 2 }),
            (new[] { "Raspberry" }, new[] { 0, 0, 0, 0, 1 }),
        };

        /// <summary>The record for <paramref name="p"/> (Rowan's own book) in a variant: null (default) the full record, "empty", "notrecorded" (null).</summary>
        public static MealCombos For(PanelInput p, string variant = null)
        {
            if (variant == "notrecorded") return null;
            var today = PanelModel.LocalToday(p);
            if (variant == "empty") return new MealCombos { FromUtc = p.NowUtc.AddHours(-1) };
            var rec = new MealCombos { FromUtc = p.NowUtc.Date.AddDays(-4).AddHours(14) };
            foreach (var (foods, perDay) in Rowan)
            {
                float h = 0, s = 0, e = 0;
                foreach (var f in foods) { var v = Food[f]; h += v.h; s += v.s; e += v.e; }
                for (int back = perDay.Length - 1; back >= 0; back--)
                    for (int k = 0; k < perDay[back]; k++) rec.Add(foods, h, s, e, today.AddDays(-back));
            }
            return rec;
        }
    }

    /// <summary>
    /// 0.9.2 Meals' Plan a meal (Chapters/MealPlanModel.cs): the foods of the sample's game data, Meadows to Ashlands, with sample values (the
    /// dishes Rowan ate keep the values Meals by dish and Your combinations use), each food's recipe ingredients or what a station makes it from
    /// (so the land of a food is worked out as in game), the feasts, and a mod's cake no land claims. Known: the food recipes Rowan's character
    /// knows (the "can make" mark). Fictional numbers.
    /// </summary>
    public static class MealPlanSample
    {
        const string Cauldron = "piece_cauldron";
        static PanelModel.FoodData F(string prefab, string token, float h, float s, float e, float minutes, string station, params string[] from) =>
            new PanelModel.FoodData { Prefab = prefab, Token = token, Health = h, Stamina = s, Eitr = e, Seconds = minutes * 60, Station = station, Feast = prefab.StartsWith("Feast"), From = new List<string>(from) };

        static PanelModel.FoodData Raw(PanelModel.FoodData f) { f.Type = "Material"; return f; }

        public static readonly List<PanelModel.FoodData> Foods = new List<PanelModel.FoodData>
        {
            // Meadows
            F("Raspberry", "$item_raspberries", 7, 20, 0, 15, null), F("Honey", "$item_honey", 8, 35, 0, 15, null), F("Mushroom", "$item_mushroomcommon", 15, 15, 0, 15, null),
            F("CookedMeat", "$item_boar_meat_cooked", 30, 10, 0, 20, null, "$item_boar_meat"), F("NeckTailGrilled", "$item_necktailgrilled", 25, 8, 0, 20, null, "$item_necktail"),
            F("FeastMeadows", "$item_feastmeadows", 35, 35, 0, 50, null),
            // Black Forest
            F("Blueberries", "$item_blueberries", 8, 25, 0, 15, null), F("MushroomYellow", "$item_mushroomyellow", 10, 30, 0, 10, null),
            F("CarrotSoup", "$item_carrotsoup", 15, 45, 0, 25, Cauldron, "$item_mushroomcommon", "$item_carrot"),
            F("QueensJam", "$item_queensjam", 14, 40, 0, 20, Cauldron, "$item_raspberries", "$item_blueberries"),
            F("DeerStew", "$item_deerstew", 45, 15, 0, 25, Cauldron, "$item_blueberries", "$item_carrot", "$item_deer_meat_cooked"),
            F("BoarJerky", "$item_boarjerky", 23, 23, 0, 30, Cauldron, "$item_boar_meat", "$item_honey"),
            F("MincedMeatSauce", "$item_mincedmeatsauce", 40, 13, 0, 25, Cauldron, "$item_boar_meat", "$item_necktail", "$item_carrot"),
            F("FeastBlackforest", "$item_feastblackforest", 35, 35, 0, 50, null),
            // Swamp
            F("TurnipStew", "$item_turnipstew", 18, 55, 0, 25, Cauldron, "$item_boar_meat", "$item_turnip"),
            F("Sausages", "$item_sausages", 55, 18, 0, 30, Cauldron, "$item_entrails", "$item_boar_meat", "$item_thistle"),
            F("BlackSoup", "$item_blacksoup", 50, 17, 0, 20, Cauldron, "$item_bloodbag", "$item_honey", "$item_turnip"),
            F("FeastSwamps", "$item_feastswamps", 45, 45, 0, 50, null),
            // Mountains
            F("OnionSoup", "$item_onionsoup", 20, 60, 0, 30, Cauldron, "$item_onion"),
            F("WolfMeatSkewer", "$item_wolf_skewer", 65, 21, 0, 30, Cauldron, "$item_wolf_meat", "$item_mushroomcommon", "$item_onion"),
            F("Eyescream", "$item_eyescream", 21, 65, 0, 25, Cauldron, "$item_greydwarfeye", "$item_freezegland"),
            F("SerpentStew", "$item_serpentstew", 80, 26, 0, 30, Cauldron, "$item_mushroomcommon", "$item_serpentmeatcooked", "$item_honey"),
            F("WolfJerky", "$item_wolfjerky", 33, 33, 0, 30, Cauldron, "$item_wolf_meat", "$item_honey"),
            F("FeastMountains", "$item_feastmountains", 55, 55, 0, 50, null),
            // Plains
            F("Bread", "$item_bread", 23, 70, 0, 30, null, "$item_breaddough", "$item_barleyflour"),
            F("FishWraps", "$item_fishwraps", 70, 23, 0, 30, Cauldron, "$item_fish_cooked", "$item_barleyflour"),
            F("LoxPie", "$item_loxpie", 75, 24, 0, 30, null, "$item_loxpie_uncooked", "$item_loxmeat"),
            F("Cloudberry", "$item_cloudberries", 13, 40, 0, 15, null), F("FeastPlains", "$item_feastplains", 60, 60, 0, 50, null),
            // Mistlands
            F("SeekerAspic", "$item_seekeraspic", 28, 85, 0, 30, null, "$item_seekermeat", "$item_magecap"),
            F("MisthareSupreme", "$item_mistharesupreme", 85, 28, 0, 30, null, "$item_hare_meat", "$item_mushroomjotunpuffs"),
            F("YggdrasilPorridge", "$item_yggdrasilporridge", 27, 13, 80, 30, null, "$item_sap", "$item_magecap"),
            F("MagicallyStuffedShroom", "$item_magicallystuffedmushroom", 25, 12, 75, 30, null, "$item_magecap", "$item_royaljelly"),
            F("FeastMistlands", "$item_feastmistlands", 65, 65, 20, 50, null),
            // Ashlands
            F("MarinatedGreens", "$item_marinatedgreens", 35, 35, 80, 30, null, "$item_fiddleheadfern", "$item_vineberry"),
            F("PiquantPie", "$item_piquantpie", 105, 35, 0, 40, null, "$item_piquantpie_uncooked", "$item_asksvin_meat"),
            F("ScorchingMedley", "$item_scorchingmedley", 32, 95, 32, 30, null, "$item_fiddleheadfern", "$item_smokepuff"),
            F("SizzlingBerryBroth", "$item_sizzlingberrybroth", 28, 42, 90, 30, null, "$item_vineberry", "$item_smokepuff"),
            F("FeastAshlands", "$item_feastashlands", 75, 75, 35, 50, null),
            // 0.9.3: what the game does not let you eat, whatever values its data carries (an uncooked pie, raw meat: Materials): never in the planner
            Raw(F("PiquantPieUncooked", "$item_piquantpie_uncooked", 105, 35, 0, 40, null, "$item_asksvin_meat")), Raw(F("AsksvinTail", "$item_asksvin_tail", 30, 10, 0, 20, null)),
            // a mod's food: no land claims it (shown under Other only once eaten)
            F("HwSampleCake", "$item_hwsamplecake", 30, 30, 0, 20, null),
        };

        /// <summary>The food recipes Rowan's character knows (the "can make" mark).</summary>
        public static readonly HashSet<string> Known = new HashSet<string>
        {
            "$item_carrotsoup", "$item_queensjam", "$item_boarjerky", "$item_deerstew", "$item_turnipstew", "$item_sausages", "$item_onionsoup", "$item_wolf_skewer", "$item_feastmountains",
        };
    }
}
