using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>One ingredient of an item's recipe, as the game's ObjectDB gives it (GameData reads it at runtime).</summary>
    public struct Ingredient
    {
        public string Token;      // the ingredient's name token ("$item_bronze")
        public string Name;       // its display name, for a material this file does not know ("Mithril")
        public bool Refined;      // a smelter, kiln or refinery makes it: a bar
    }

    /// <summary>
    /// Crafting's filter (Deeds > Crafting, design G): the page's gear as a filter bar over two rows, then the filtered gear
    /// as the item grid. Only what the game's own data says about an item, so modded gear works as well:
    ///   Kind          = the game's item type (the same one the per-kind tiles count): Weapons, Armour, Tools, Trinkets.
    ///   Main material = the recipe's ingredient that best marks the tier (MainMaterial below). It says "by main material",
    ///                   never "made from only"; an item without a recipe the game knows is "Other".
    /// </summary>
    public static partial class PanelModel
    {
        public const string CraftFilter = "Deeds/crafting/gear", MaterialOther = "Other";

        /// <summary>The materials that always have a chip, in tier order. Later ones (Carapace, Flametal, a mod's bar) join when
        /// the character has crafted something of them; "Other" when something has no material.</summary>
        public static readonly string[] BaseMaterials = { "Wood", "Leather", "Bronze", "Iron", "Silver", "Black metal" };

        // each material's own colour in the linked bar (the kinds' bar has none of these: no blue means two things)
        static readonly Dictionary<string, string> GearMaterialColours = new Dictionary<string, string>
        {
            ["Wood"] = "#8a5a34", ["Leather"] = "#c2a072", ["Bronze"] = "#b87a3a", ["Iron"] = "#7d8793", ["Silver"] = "#d6dde4", ["Black metal"] = "#2e3136",
            ["Carapace"] = "#5f8a74", ["Flametal"] = "#c2552a", [MaterialOther] = "#6f6a60",
        };
        const string ModMaterialColour = "#9a8f7a";
        static string GearMaterialColour(string m) => GearMaterialColours.TryGetValue(m ?? "", out var c) ? c : ModMaterialColour;

        // the rank a material has in the tier order (higher marks the item); a mod's own bar ranks above every vanilla one
        static readonly string[] MaterialTiers = { "Wood", "Leather", "Bronze", "Iron", "Silver", "Black metal", "Carapace", "Flametal" };

        /// <summary>
        /// What an ingredient stands for as a material: (label, rank), or (null, -1) for one that marks no tier (resin, flint,
        /// feathers, coal, nails of an unknown kind...). By the vanilla names first; a bar some smelter makes that no name
        /// above knows (a mod's) is its own material under its display name, above every vanilla one.
        /// </summary>
        public static (string material, int rank) MaterialOfIngredient(Ingredient i)
        {
            var t = (i.Token ?? "").ToLowerInvariant();
            int R(string m) => Array.IndexOf(MaterialTiers, m);
            if (VanillaWood.Contains(i.Token ?? "")) return ("Wood", R("Wood"));
            if (t.Contains("hide") || t.Contains("pelt") || t.Contains("leather")) return ("Leather", R("Leather"));
            if (t.Contains("blackmetal")) return ("Black metal", R("Black metal"));
            if (t.Contains("flametal")) return ("Flametal", R("Flametal"));
            if (t.Contains("carapace")) return ("Carapace", R("Carapace"));
            if (t.Contains("silver")) return ("Silver", R("Silver"));
            if (t.Contains("iron")) return ("Iron", R("Iron"));
            if (t.Contains("bronze") || t == "$item_copper" || t == "$item_tin") return ("Bronze", R("Bronze"));
            if (i.Refined && !string.IsNullOrWhiteSpace(i.Name)) return (i.Name.Trim(), MaterialTiers.Length);
            return (null, -1);
        }

        /// <summary>
        /// An item's main material from its recipe: the ingredient that best marks the tier, that is the highest in the tier
        /// order (wood, leather, bronze, iron, silver, black metal, later biomes' materials, a mod's own bar). A Bronze Axe
        /// (bronze, wood) is Bronze, a Wolf Armour (wolf pelt, silver) Silver, a Troll armour (troll hide, leather scraps)
        /// Leather. null = no ingredient marks a tier ("Other").
        /// </summary>
        public static string MainMaterial(IEnumerable<Ingredient> recipe)
        {
            string best = null; var rank = -1;
            foreach (var i in recipe ?? Enumerable.Empty<Ingredient>())
            {
                var (m, r) = MaterialOfIngredient(i);
                if (m != null && r > rank) { best = m; rank = r; }
            }
            return best;
        }

        /// <summary>The game's craft counter an item type belongs to (the per-kind tile), or -1 (an item type that is no gear).</summary>
        static int GearKindIndex(string itemType)
        {
            if (string.IsNullOrEmpty(itemType)) return -1;
            if (itemType == "Utility") itemType = "Chest";   // belts and the like count with the armour
            for (int k = 0; k < GearKinds.Length; k++) if (GearKinds[k].types.Contains(itemType)) return k;
            return -1;
        }

        /// <summary>A piece of gear with the values the filter tests (its kind and main material, "Other" when the game data does not say).</summary>
        static FacetItem GearFacetItem(PanelInput input, string key, double weight)
        {
            var it = new FacetItem { Key = key, Weight = weight };
            var k = GearKindIndex(Ask(input.ItemType, key));
            if (k >= 0) it.Values["kind"] = GearKinds[k].title.ToLowerInvariant();
            it.Values["material"] = Ask(input.MainMaterial, key) ?? MaterialOther;
            return it;
        }

        /// <summary>The page's gear as a filter bar and the items that pass it; null without any gear.</summary>
        static FacetResult GearFilter(PanelInput input, PanelState state, Dictionary<string, double> gear)
        {
            if (gear.Count == 0) return null;
            var items = new List<FacetItem>();
            foreach (var kv in gear.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)) items.Add(GearFacetItem(input, kv.Key, kv.Value));
            var materials = new List<string>(BaseMaterials);
            foreach (var m in items.Select(i => i.Values["material"]).Distinct().Where(m => m != MaterialOther && !materials.Contains(m))
                                   .OrderBy(m => { var r = Array.IndexOf(MaterialTiers, m); return r < 0 ? 99 : r; }).ThenBy(m => m, StringComparer.OrdinalIgnoreCase)) materials.Add(m);
            if (items.Any(i => i.Values["material"] == MaterialOther)) materials.Add(MaterialOther);
            var defs = new List<FacetDef>
            {
                new FacetDef { Id = "kind", Title = "Kind", BarTitle = "By kind", Options = GearKinds.Select(g => new FacetOption { Id = g.title.ToLowerInvariant(), Label = g.title, Colour = g.colour }).ToList() },
                new FacetDef { Id = "material", Title = "Main material", Sub = "by main material", BarTitle = "By main material",
                               Options = materials.Select(m => new FacetOption { Id = m, Label = m, Colour = GearMaterialColour(m) }).ToList() },
            };
            return Facets(state, CraftFilter, defs, items, "crafted", SrcCharacter, "item", "items");
        }
    }
}
