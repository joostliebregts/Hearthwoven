using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>One resource of a piece's build cost, as the game's data gives it (GameData reads it at runtime).</summary>
    public struct PieceIngredient
    {
        public string Token;      // the resource's name token ("$item_finewood")
        public string Name;       // its display name, for a material this file does not know ("Clay")
        public int Amount;        // how many the piece costs
        public bool Refined;      // a smelter, kiln or refinery makes it: a bar
        public bool Material;     // the game calls the item a material (not a trophy, food or tool)
    }

    /// <summary>
    /// Building's filter (Deeds > Building, FILTERING-06): the pieces built as the same filter bar Crafting uses (FacetModel.cs), over
    /// two rows, then the pieces that pass as the item grid. Only what the game's own data says about a piece, so modded pieces work:
    ///   Category      = the hammer tab the piece sits on (Piece.m_category, named by its piece table): Misc, Crafting, Building,
    ///                   Stonecutter, Furniture, and whatever a mod adds.
    ///   Main material = the build resource that best marks the tier (PieceMainMaterial below). It says "by main material", never
    ///                   "made only of"; a piece the game data cannot place is "Other".
    /// The counts are the page's own unit, pieces built (the game's placed-pieces counter; a piece you move counts again).
    /// </summary>
    public static partial class PanelModel
    {
        public const string BuildFilter = "Deeds/building/pieces";

        /// <summary>The tabs that always have a chip, in the hammer's order. Others (Feasts, a mod's own) join when something was built from them.</summary>
        public static readonly string[] BaseTabs = { "Misc", "Crafting", "Building", "Stonecutter", "Furniture" };

        /// <summary>The materials that always have a chip, in tier order. Later ones (Black marble, a mod's own) join when something was built from them; "Other" when something has none.</summary>
        public static readonly string[] BasePieceMaterials = { "Wood", "Stone", "Core wood", "Fine wood", "Bronze", "Iron" };

        // the rank a material has in the tier order (the highest marks the piece); a mod's own bar ranks above every vanilla one
        static readonly string[] PieceTiers = { "Wood", "Stone", "Core wood", "Fine wood", "Ancient bark", "Bronze", "Iron", "Silver", "Black metal", "Black marble", "Yggdrasil wood", "Grausten", "Ashwood", "Flametal" };

        // resources that never mark a tier: fasteners, fuel, glue, trim, and what monsters drop (a Wood Gate with iron nails is still wood)
        static readonly string[] PieceMarkless = { "nails", "bolt", "coal", "resin", "flint", "feather", "leatherscraps", "hide", "pelt", "chain", "core", "eye", "bone", "thistle", "honey", "entrails", "ymirremains" };

        static readonly Dictionary<string, string> PieceTabColours = new Dictionary<string, string>
        {
            ["Misc"] = "#6fa39b", ["Crafting"] = "#a8707a", ["Building"] = "#8c6e9a", ["Stonecutter"] = "#7d9a6a", ["Furniture"] = "#5f8fbf", ["Other"] = "#6f6a60",
        };
        const string ModTabColour = "#a89fb8";
        static string PieceTabColour(string t) => PieceTabColours.TryGetValue(t ?? "", out var c) ? c : ModTabColour;

        // each material's own colour in the linked bar; the tabs' bar shares none of these (no colour means two things on the page)
        static readonly Dictionary<string, string> PieceMaterialColours = new Dictionary<string, string>
        {
            ["Wood"] = "#8a5a34", ["Stone"] = "#a39d8e", ["Core wood"] = "#5e3c22", ["Fine wood"] = "#c9a15f", ["Ancient bark"] = "#7a6a3a", ["Bronze"] = "#b87a3a", ["Iron"] = "#7d8793",
            ["Silver"] = "#d6dde4", ["Black metal"] = "#2e3136", ["Black marble"] = "#4a4f5c", ["Yggdrasil wood"] = "#b08f5a", ["Grausten"] = "#8a8f8c", ["Ashwood"] = "#4a403a", ["Flametal"] = "#c2552a",
            [MaterialOther] = "#6f6a60",
        };
        static string PieceMaterialColour(string m) => PieceMaterialColours.TryGetValue(m ?? "", out var c) ? c : ModMaterialColour;

        /// <summary>The game's name for a piece category when its piece table gives no label (GameData): the Building tab is two categories in the game's enum.</summary>
        public static string TabNameOf(string category)
        {
            switch (category)
            {
                case "BuildingWorkbench": return "Building";
                case "BuildingStonecutter": return "Stonecutter";
                case "DeepNorth": return "Deep North";
                default: return string.IsNullOrEmpty(category) ? null : category;
            }
        }

        /// <summary>What a resource stands for as a material: (label, rank), or (null, -1) for one that marks no tier (iron nails, resin, flint, hides...).</summary>
        public static (string material, int rank) MaterialOfPieceIngredient(PieceIngredient i)
        {
            var t = (i.Token ?? "").ToLowerInvariant();
            if (PieceMarkless.Any(w => t.Contains(w))) return (null, -1);
            string m = null;
            switch (t)
            {
                case "$item_wood": m = "Wood"; break;
                case "$item_stone": m = "Stone"; break;
                case "$item_roundlog": m = "Core wood"; break;
                case "$item_finewood": m = "Fine wood"; break;
                case "$item_elderbark": m = "Ancient bark"; break;
                case "$item_bronze": case "$item_copper": case "$item_tin": m = "Bronze"; break;
                case "$item_iron": m = "Iron"; break;
                case "$item_silver": m = "Silver"; break;
                case "$item_blackmetal": m = "Black metal"; break;
                case "$item_blackmarble": m = "Black marble"; break;
                case "$item_yggdrasilwood": m = "Yggdrasil wood"; break;
                case "$item_grausten": m = "Grausten"; break;
                case "$item_ashwood": m = "Ashwood"; break;
                case "$item_flametal": m = "Flametal"; break;
            }
            if (m != null) return (m, Array.IndexOf(PieceTiers, m));
            if (i.Refined && !string.IsNullOrWhiteSpace(i.Name)) return (i.Name.Trim(), PieceTiers.Length);   // a bar some smelter makes: a mod's own material, above every vanilla one
            return (null, -1);
        }

        /// <summary>
        /// A piece's main material from what it costs to build: the resource that best marks the tier, that is the highest in the
        /// tier order (a Wood Gate with iron nails is Wood; a Chest of wood and iron is Iron; a Portal of fine wood, eyes and cores is
        /// Fine wood). With no resource in the order, the material a mod's piece costs most of ("Clay"). null = nothing marks one ("Other").
        /// </summary>
        public static string PieceMainMaterial(IEnumerable<PieceIngredient> recipe)
        {
            string best = null; var rank = -1; PieceIngredient? unknown = null;
            foreach (var i in recipe ?? Enumerable.Empty<PieceIngredient>())
            {
                var (m, r) = MaterialOfPieceIngredient(i);
                if (m != null && r > rank) { best = m; rank = r; }
                else if (m == null && i.Material && !string.IsNullOrWhiteSpace(i.Name) && !PieceMarkless.Any(w => (i.Token ?? "").ToLowerInvariant().Contains(w)) && (unknown == null || i.Amount > unknown.Value.Amount)) unknown = i;
            }
            return best ?? unknown?.Name.Trim();
        }

        /// <summary>A piece with the values the filter tests (its hammer tab and main material, "Other" when the game data does not say).</summary>
        static FacetItem PieceFacetItem(PanelInput input, string key, double weight)
        {
            var it = new FacetItem { Key = key, Weight = weight };
            it.Values["tab"] = Ask(input.PieceTab, key) ?? MaterialOther; it.Values["material"] = Ask(input.PieceMaterial, key) ?? MaterialOther;
            return it;
        }

        /// <summary>The page's pieces as a filter bar and the pieces that pass it; null when the game data names neither a tab nor a material for any of them.</summary>
        static FacetResult PieceFilter(PanelInput input, PanelState state, Dictionary<string, double> built)
        {
            if (built.Count == 0) return null;
            var items = new List<FacetItem>();
            foreach (var kv in built.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)) items.Add(PieceFacetItem(input, kv.Key, kv.Value));
            if (!items.Any(i => i.Values["tab"] != MaterialOther || i.Values["material"] != MaterialOther)) return null;   // nothing known (a copy without the game's data): the plain grid
            var tabs = new List<string>(BaseTabs);
            foreach (var t in items.Select(i => i.Values["tab"]).Distinct().Where(t => t != MaterialOther && !tabs.Contains(t)).OrderBy(t => t, StringComparer.OrdinalIgnoreCase)) tabs.Add(t);
            if (items.Any(i => i.Values["tab"] == MaterialOther)) tabs.Add(MaterialOther);
            var materials = new List<string>(BasePieceMaterials);
            foreach (var m in items.Select(i => i.Values["material"]).Distinct().Where(m => m != MaterialOther && !materials.Contains(m))
                                   .OrderBy(m => { var r = Array.IndexOf(PieceTiers, m); return r < 0 ? 99 : r; }).ThenBy(m => m, StringComparer.OrdinalIgnoreCase)) materials.Add(m);
            if (items.Any(i => i.Values["material"] == MaterialOther)) materials.Add(MaterialOther);
            var defs = new List<FacetDef>
            {
                new FacetDef { Id = "tab", Title = "Category", Sub = "the hammer's tab", BarTitle = "By category", Options = tabs.Select(t => new FacetOption { Id = t, Label = t, Colour = PieceTabColour(t) }).ToList() },
                new FacetDef { Id = "material", Title = "Main material", Sub = "by main material", BarTitle = "By main material",
                               Options = materials.Select(m => new FacetOption { Id = m, Label = m, Colour = PieceMaterialColour(m) }).ToList() },
            };
            return Facets(state, BuildFilter, defs, items, "built", SrcCharacter, "piece", "pieces", "No filter: every piece");
        }
    }
}
