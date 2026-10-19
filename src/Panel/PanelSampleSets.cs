using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 Armor sets (Chapters/ArmorSets.cs): the sample's armor, as GameData reads it from the game: vanilla-like pieces of every land with their
    /// recipes (station, level, ingredients), the game's set names where the game has them, a cast the Deep North hardens, and one mod's set
    /// (Mithril, a bar the game data does not know by name). Fictional numbers; the recipes follow the reference tables. Every sample book reads it.
    /// </summary>
    public static class SetsSample
    {
        static ArmorPiece P(string token, string name, string slot, string station, int level, string set, params (string token, string name, int n)[] cost) =>
            new ArmorPiece
            {
                Token = token, Prefab = name.Replace(" ", "").Replace("'", ""), Name = name, Slot = slot, SetName = set ?? "", SetSize = set == null ? 0 : 3,
                Station = station, StationName = station == null ? null : StationNames.TryGetValue(station, out var sn) ? sn : station, StationLevel = level, HasRecipe = true,
                Recipe = cost.Select(c => new ArmorCost { Token = c.token, Name = c.name, Amount = c.n, Refined = Refined.Contains(c.token) }).ToList(),
            };

        static readonly Dictionary<string, string> StationNames = new Dictionary<string, string>
        {
            ["piece_workbench"] = "Workbench", ["forge"] = "Forge", ["blackforge"] = "Black Forge", ["piece_magetable"] = "Galdr Table",
        };
        static readonly HashSet<string> Refined = new HashSet<string> { "$item_bronze", "$item_iron", "$item_silver", "$item_blackmetal", "$item_flametal", "$item_eitr", "$item_linenthread", "$item_mithrilbar" };

        const string Wb = "piece_workbench", Fg = "forge", Bf = "blackforge", Gt = "piece_magetable";
        static readonly (string, string, int) Scraps = ("$item_leatherscraps", "Leather Scraps", 5), Hide6 = ("$item_deerhide", "Deer Hide", 6), Hide2 = ("$item_deerhide", "Deer Hide", 2);

        public static readonly List<ArmorPiece> Pieces = new List<ArmorPiece>
        {
            // Meadows
            P("$item_chest_rags", "Rag Tunic", "Chest", Wb, 1, null, Scraps), P("$item_legs_rags", "Rag Trousers", "Legs", Wb, 1, null, Scraps),
            P("$item_helmet_leather", "Leather Helmet", "Helmet", Wb, 2, null, Hide6), P("$item_chest_leather", "Leather Tunic", "Chest", Wb, 2, null, Hide6),
            P("$item_legs_leather", "Leather Trousers", "Legs", Wb, 2, null, Hide6), P("$item_cape_deerhide", "Deer Hide Cape", "Shoulder", Wb, 2, null, ("$item_deerhide", "Deer Hide", 4), ("$item_bonefragments", "Bone Fragments", 5)),
            // Black Forest
            P("$item_helmet_bronze", "Bronze Helmet", "Helmet", Fg, 1, null, ("$item_bronze", "Bronze", 5), Hide2), P("$item_chest_bronze", "Bronze Plate Tunic", "Chest", Fg, 1, null, ("$item_bronze", "Bronze", 5), Hide2),
            P("$item_legs_bronze", "Bronze Plate Leggings", "Legs", Fg, 1, null, ("$item_bronze", "Bronze", 5), Hide2),
            P("$item_helmet_trollleather", "Troll Leather Hood", "Helmet", Wb, 2, "troll", ("$item_trollhide", "Troll Hide", 5), ("$item_bonefragments", "Bone Fragments", 3)),
            P("$item_chest_trollleather", "Troll Leather Tunic", "Chest", Wb, 2, "troll", ("$item_trollhide", "Troll Hide", 5), ("$item_bonefragments", "Bone Fragments", 3)),
            P("$item_legs_trollleather", "Troll Leather Trousers", "Legs", Wb, 2, "troll", ("$item_trollhide", "Troll Hide", 5), ("$item_bonefragments", "Bone Fragments", 3)),
            P("$item_cape_trollhide", "Troll Hide Cape", "Shoulder", Wb, 2, "troll", ("$item_trollhide", "Troll Hide", 10), ("$item_bonefragments", "Bone Fragments", 10)),
            // Swamp
            P("$item_helmet_iron", "Iron Helmet", "Helmet", Fg, 1, null, ("$item_iron", "Iron", 20), Hide2), P("$item_chest_iron", "Iron Scale Mail", "Chest", Fg, 2, null, ("$item_iron", "Iron", 20), Hide2),
            P("$item_legs_iron", "Iron Greaves", "Legs", Fg, 2, null, ("$item_iron", "Iron", 20), Hide2),
            // Mountains: the Drake Helmet at a lower forge level than the wolf hide, the Wolf Fur Cape made at the workbench (it joins them)
            P("$item_helmet_drake", "Drake Helmet", "Helmet", Fg, 1, null, ("$item_silver", "Silver", 20), ("$item_wolfpelt", "Wolf Pelt", 2), ("$item_trophy_hatchling", "Drake Trophy", 2)),
            P("$item_chest_wolf", "Wolf Hide Chestpiece", "Chest", Fg, 2, null, ("$item_silver", "Silver", 20), ("$item_wolfpelt", "Wolf Pelt", 2), ("$item_trophy_hatchling", "Drake Trophy", 2)),
            P("$item_legs_wolf", "Wolf Hide Trousers", "Legs", Fg, 2, null, ("$item_silver", "Silver", 20), ("$item_wolfpelt", "Wolf Pelt", 2), ("$item_trophy_hatchling", "Drake Trophy", 2)),
            P("$item_cape_wolf", "Wolf Fur Cape", "Shoulder", Wb, 4, null, ("$item_silver", "Silver", 4), ("$item_wolfpelt", "Wolf Pelt", 6), ("$item_trophy_wolf", "Wolf Trophy", 1)),
            P("$item_helmet_fenris", "Fenris Hood", "Helmet", Wb, 4, "fenris", ("$item_wolfhairbundle", "Fenris Hair", 20), ("$item_wolfpelt", "Wolf Pelt", 2), ("$item_trophy_cultist", "Cultist Trophy", 1)),
            P("$item_chest_fenris", "Fenris Coat", "Chest", Wb, 4, "fenris", ("$item_wolfhairbundle", "Fenris Hair", 20), ("$item_wolfpelt", "Wolf Pelt", 2), ("$item_trophy_cultist", "Cultist Trophy", 1)),
            P("$item_legs_fenris", "Fenris Leggings", "Legs", Wb, 4, "fenris", ("$item_wolfhairbundle", "Fenris Hair", 20), ("$item_wolfpelt", "Wolf Pelt", 2), ("$item_trophy_cultist", "Cultist Trophy", 1)),
            // a mod's set: its own bar, which no vanilla name knows (it keeps its material's name)
            P("$item_helmet_mithril", "Mithril Helm", "Helmet", Fg, 3, null, ("$item_mithrilbar", "Mithril", 12), ("$item_wolfpelt", "Wolf Pelt", 2)),
            P("$item_chest_mithril", "Mithril Hauberk", "Chest", Fg, 3, null, ("$item_mithrilbar", "Mithril", 16), ("$item_wolfpelt", "Wolf Pelt", 2)),
            // Plains
            P("$item_helmet_padded", "Padded Helmet", "Helmet", Fg, 1, null, ("$item_iron", "Iron", 10), ("$item_linenthread", "Linen Thread", 15)),
            P("$item_chest_pcuirass", "Padded Cuirass", "Chest", Fg, 2, null, ("$item_iron", "Iron", 10), ("$item_linenthread", "Linen Thread", 15)),
            P("$item_legs_pgreaves", "Padded Greaves", "Legs", Fg, 2, null, ("$item_iron", "Iron", 10), ("$item_linenthread", "Linen Thread", 15)),
            // Mistlands
            P("$item_helmet_carapace", "Carapace Helmet", "Helmet", Bf, 1, null, ("$item_carapace", "Carapace", 16), ("$item_scalehide", "Scale Hide", 3), ("$item_mandible", "Mandible", 2), ("$item_eitr", "Refined Eitr", 4)),
            P("$item_chest_carapace", "Carapace Breastplate", "Chest", Bf, 1, null, ("$item_carapace", "Carapace", 16), ("$item_scalehide", "Scale Hide", 3), ("$item_mandible", "Mandible", 2), ("$item_eitr", "Refined Eitr", 4)),
            P("$item_legs_carapace", "Carapace Greaves", "Legs", Bf, 1, null, ("$item_carapace", "Carapace", 16), ("$item_scalehide", "Scale Hide", 3), ("$item_mandible", "Mandible", 2), ("$item_eitr", "Refined Eitr", 4)),
            P("$item_helmet_mage", "Eitr-weave Hood", "Helmet", Gt, 1, "mage", ("$item_linenthread", "Linen Thread", 16), ("$item_eitr", "Refined Eitr", 15), ("$item_iron", "Iron", 2)),
            P("$item_chest_mage", "Eitr-weave Robe", "Chest", Gt, 1, "mage", ("$item_linenthread", "Linen Thread", 16), ("$item_eitr", "Refined Eitr", 15), ("$item_iron", "Iron", 2)),
            P("$item_legs_mage", "Eitr-weave Trousers", "Legs", Gt, 1, "mage", ("$item_linenthread", "Linen Thread", 16), ("$item_eitr", "Refined Eitr", 15), ("$item_iron", "Iron", 2)),
            // Ashlands
            P("$item_helmet_flametal", "Flametal Helmet", "Helmet", Bf, 3, null, ("$item_flametal", "Flametal", 16), ("$item_askhide", "Asksvin Hide", 3), ("$item_charredbone", "Charred Bone", 2), ("$item_eitr", "Refined Eitr", 4)),
            P("$item_chest_flametal", "Flametal Breastplate", "Chest", Bf, 3, null, ("$item_flametal", "Flametal", 16), ("$item_askhide", "Asksvin Hide", 3), ("$item_charredbone", "Charred Bone", 2), ("$item_eitr", "Refined Eitr", 4)),
            P("$item_legs_flametal", "Flametal Greaves", "Legs", Bf, 3, null, ("$item_flametal", "Flametal", 16), ("$item_askhide", "Asksvin Hide", 3), ("$item_charredbone", "Charred Bone", 2), ("$item_eitr", "Refined Eitr", 4)),
            // Deep North: hardened from a cast (the cast's recipe stands for the piece)
            Cast(P("$item_helmet_heavy_deepnorth", "Helmet of the Protector", "Helmet", Bf, 4, "protector", ("$item_bloodgold", "Bloodgold", 20), ("$item_moosehide", "Moose Hide", 4)), "$item_helmet_heavy_gold_uncooked"),
            Cast(P("$item_chest_heavy_deepnorth", "Breastplate of the Protector", "Chest", Bf, 4, "protector", ("$item_bloodgold", "Bloodgold", 20), ("$item_moosehide", "Moose Hide", 8)), "$item_chest_heavy_gold_uncooked"),
            // no recipe at all (Hildir sells it): never in a set
            new ArmorPiece { Token = "$item_helmet_hildir1", Name = "Hildir's Brass Hat", Slot = "Helmet", HasRecipe = false },
        };

        static ArmorPiece Cast(ArmorPiece p, string cast) { p.MadeAs = cast; return p; }

        static readonly Dictionary<string, ArmorPiece> ByToken = Pieces.ToDictionary(p => p.Token);
        /// <summary>The sample armor's item type (GameData.ItemType for these tokens); null = not one of them.</summary>
        public static string TypeOf(string token) => token != null && ByToken.TryGetValue(token, out var p) ? p.Slot : null;
        /// <summary>The sample armor's names and the names of what they cost (the game's localized names).</summary>
        public static string NameOf(string token)
        {
            if (token == null) return null;
            if (ByToken.TryGetValue(token, out var p)) return p.Name;
            return Pieces.SelectMany(x => x.Recipe).FirstOrDefault(c => c.Token == token)?.Name;
        }

        /// <summary>The armor recipes the sample's Rowan knows: the Meadows, the Black Forest and the Swamp's iron.</summary>
        public static readonly HashSet<string> Known = new HashSet<string>(Pieces.Where(p => p.Token.Contains("rags") || p.Token.Contains("leather") || p.Token.Contains("deerhide") || p.Token.Contains("bronze") || p.Token.Contains("troll") || p.Token.Contains("iron")).Select(p => p.Token));

        /// <summary>
        /// The Armor sets previews (KsPanelTest --dump preview-deeds-crafting-sets*): the sample world with Rowan's armor made as each state asks.
        ///   "yours"    the Leather set in full, Bronze two of three, Troll leather two of four, the Rag set begun, Iron known and not begun;
        ///   "land"     every set of the Meadows in full ("MEADOWS · every set made");
        ///   "new"      a new character: the Meadows' recipes known, nothing made yet (every tile dashed);
        ///   "everyone" "yours", with Edda's and Tor's armor as their copies share it and Finch's copy from 0.8.6 (it does not say).
        /// </summary>
        public static PanelInput World(DateTime now, string state)
        {
            var w = PanelSample.Full(now);
            var crafted = new Dictionary<string, float>(w.ItemsCrafted ?? new Dictionary<string, float>());
            void Made(params (string token, float n)[] made) { foreach (var m in made) crafted[m.token] = m.n; }
            var known = new HashSet<string>(Known);
            switch (state)
            {
                case "land":
                    Made(("$item_chest_rags", 1), ("$item_legs_rags", 1), ("$item_helmet_leather", 2), ("$item_chest_leather", 2), ("$item_legs_leather", 1), ("$item_cape_deerhide", 1), ("$item_helmet_bronze", 1));
                    break;
                case "new":
                    crafted = new Dictionary<string, float>(crafted.Where(kv => TypeOf(kv.Key) == null).ToDictionary(kv => kv.Key, kv => kv.Value));
                    known = new HashSet<string>(Pieces.Where(p => p.Token.Contains("rags") || p.Token.Contains("_leather") || p.Token.Contains("deerhide")).Select(p => p.Token));
                    break;
                default:
                    Made(("$item_chest_rags", 1), ("$item_helmet_leather", 2), ("$item_chest_leather", 2), ("$item_legs_leather", 1), ("$item_cape_deerhide", 1),
                         ("$item_helmet_bronze", 1), ("$item_chest_bronze", 1), ("$item_helmet_trollleather", 1), ("$item_chest_trollleather", 1));
                    break;
            }
            w.ItemsCrafted = crafted;
            var recipes = w.RecipeKnown;
            w.RecipeKnown = t => known.Contains(t ?? "") || (TypeOf(t) == null && recipes != null && recipes(t));
            if (state == "everyone")
                foreach (var f in w.Fellows ?? new List<PanelInput>())
                {
                    if (f.PlayerName == SampleWorld.Edda) f.ArmorMade = new HashSet<string> { "$item_helmet_leather", "$item_chest_leather", "$item_legs_leather", "$item_cape_deerhide", "$item_helmet_bronze", "$item_chest_bronze", "$item_legs_bronze" };
                    else if (f.PlayerName == SampleWorld.Tor) f.ArmorMade = new HashSet<string> { "$item_chest_rags", "$item_legs_rags", "$item_helmet_leather", "$item_chest_leather", "$item_legs_leather", "$item_cape_deerhide", "$item_helmet_iron", "$item_chest_iron" };
                    else { f.ArmorMade = null; f.ModVersion = "0.8.6"; }
                }
            return w;
        }
    }
}
