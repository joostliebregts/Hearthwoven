using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Cooking's filter (Deeds > Cooking, 0.6.5; Joost 2026-10-09: "Why don't we have filters on cooking?"): the dishes as the same
    /// filter bar Crafting and Building use (FacetModel.cs), over two rows, then the dish bar and the kitchen ledger of the dishes
    /// that pass. Only what the game's own data says about a dish, so a mod's dishes land where they belong:
    ///   Type       = how the game makes it (DishTypeOf): Meals (made in the crafting window, cauldron or prep table, with food value),
    ///                Grilled and Baked (what a cooking station hands out; Baked = a station that burns fuel, the oven), Feasts (what a
    ///                feast is set out from), Uncooked (what goes onto a cooking station: dough, a raw pie), Mead bases (what a
    ///                fermenter takes). A dish the data cannot place is "Other", never its bare id.
    ///   Main boost = the biggest of the dish's health, stamina and eitr, the game's own food values (DishBoostOf); "Balanced" when
    ///                the two biggest are close; "Other" = no food value (a mead base, a dough) or not known.
    /// The counts are the page's own unit, dishes cooked. It filters the dishes you made; who enjoyed your food is counted on your
    /// fellows' PCs per serving and stays whole, with one line that says so.
    /// </summary>
    public static partial class PanelModel
    {
        public const string CookFilter = "Deeds/cooking/dishes", DishOther = "other";

        /// <summary>The fold test of the collapsed bar (FacetModel.Facets): the page's dish bar sits between the filter and the ledger,
        /// its head and thin track count above the list (px at scale 1), so the linked bars show only when the filter is opened.</summary>
        public const double CookAbove = FilterAbove + 48;

        /// <summary>The line under "Who enjoyed" while a filter is on: those servings are not narrowed by it.</summary>
        public static string EnjoyedWhole(string whose) => "Shows all " + whose + " food, not the filter above.";

        // id, words, colour in the linked bar, always a chip (the rest join when a dish of them was made). The types keep clear of the
        // boosts' red, yellow and blue (no colour means two things in one bar pair)
        static readonly (string id, string label, string colour, bool always)[] DishTypes =
        {
            ("meal", "Meals", "#8a7a4a", true), ("grilled", "Grilled", "#a0603a", true), ("baked", "Baked", "#c9a46a", true),
            ("feast", "Feasts", "#8c6e9a", false), ("uncooked", "Uncooked", "#b8ae96", false), ("meadbase", "Mead bases", "#6f8f5a", false),
            (DishOther, "Other", "#6f6a60", false),
        };

        // the game's own food colours: health red, stamina yellow, eitr blue
        static readonly (string id, string label, string colour, bool always)[] DishBoosts =
        {
            ("health", "Health", "#b84a44", true), ("stamina", "Stamina", "#d1a73a", true), ("eitr", "Eitr", "#5a78c8", true),
            ("balanced", "Balanced", "#5f8a8a", false), (DishOther, "Other", "#6f6a60", false),
        };

        /// <summary>
        /// A dish's type from what the game's data says about it (GameData reads it): set out as a feast; handed out by a cooking
        /// station (baked when only fuel-burning stations, the oven, hand it out, grilled otherwise); put onto a cooking station
        /// (uncooked); taken by a fermenter (a mead base); made in the crafting window with food value (a meal). null = none of
        /// these ("Other": a brew the data cannot place, an item the game does not know).
        /// </summary>
        public static string DishTypeOf(bool feast, bool grilled, bool baked, bool uncooked, bool meadBase, bool food)
        {
            if (feast) return "feast";
            if (grilled) return "grilled";
            if (baked) return "baked";
            if (uncooked) return "uncooked";
            if (meadBase) return "meadbase";
            return food ? "meal" : null;
        }

        /// <summary>
        /// A dish's main boost from the game's food values (ItemData.m_food, m_foodStamina, m_foodEitr): the biggest of the three;
        /// "balanced" when the second is at least 85 % of it (a jerky: 23 health, 23 stamina). null = no food value.
        /// </summary>
        public static string DishBoostOf(float health, float stamina, float eitr)
        {
            var v = new[] { ("health", health), ("stamina", stamina), ("eitr", eitr) }.OrderByDescending(x => x.Item2).ToArray();
            if (v[0].Item2 <= 0) return null;
            return v[1].Item2 >= 0.85f * v[0].Item2 ? "balanced" : v[0].Item1;
        }

        /// <summary>A dish with the values the filter tests (its type and main boost; "other" when the game data does not say).</summary>
        static FacetItem DishFacetItem(PanelInput input, string key, double weight)
        {
            var it = new FacetItem { Key = key, Weight = weight };
            var type = Ask(input.DishType, key); var boost = Ask(input.DishBoost, key);
            it.Values["type"] = DishTypes.Any(t => t.id == type) ? type : DishOther;
            it.Values["boost"] = DishBoosts.Any(b => b.id == boost) ? boost : DishOther;
            return it;
        }

        /// <summary>The page's dishes as a filter bar and the dishes that pass it; null without any dish.</summary>
        static FacetResult DishFilter(PanelInput input, PanelState state, IDictionary<string, double> dishes, string src)
        {
            var items = dishes.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                              .Select(kv => DishFacetItem(input, kv.Key, kv.Value)).ToList();
            if (items.Count == 0) return null;
            FacetDef Row(string id, string title, string sub, string barTitle, (string id, string label, string colour, bool always)[] options) => new FacetDef
            {
                Id = id, Title = title, Sub = sub, BarTitle = barTitle,
                Options = options.Where(o => o.always || items.Any(i => i.Values[id] == o.id)).Select(o => new FacetOption { Id = o.id, Label = o.label, Colour = o.colour }).ToList(),
            };
            var defs = new List<FacetDef>
            {
                Row("type", "Type", null, "By type", DishTypes),
                Row("boost", "Main boost", "by food value", "By main boost", DishBoosts),
            };
            return Facets(state, CookFilter, defs, items, "cooked", src, "dish", "dishes", "No filter: every dish", CookAbove);
        }
    }
}
