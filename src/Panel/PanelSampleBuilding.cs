using System;
using System.Collections.Generic;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// SAMPLE data (fictional Rowan) for Deeds > Building's filter (BuildingFacets.cs): the hall Rowan raised, piece by piece, with
    /// the hammer tab and main material the game's data would give each. The three pieces of the plain evening (Wood Wall, Wood Floor
    /// 2x2, Sharp Stakes) keep their counts; the rest joins them in PanelSample.Full only, so the plain samples of the older tests stay
    /// as they were. Each piece is in the placed-pieces counter now and, mostly, in its baseline (the counter when Hearthwoven first
    /// ran), never above it, so Together, the overview card and the since-install zone read the same pieces. Applying it twice changes nothing.
    /// </summary>
    public static class BuildingSample
    {
        // token, the game's name, hammer tab, main material, placed now, placed at install (the baseline)
        static readonly (string token, string name, string tab, string material, float now, float atInstall)[] Pieces =
        {
            ("$piece_woodwall", "Wood Wall", "Building", "Wood", 520, 440), ("$piece_woodfloor2x2", "Wood Floor 2x2", "Building", "Wood", 310, 270),
            ("$piece_woodbeam", "Wood Beam", "Building", "Wood", 96, 80), ("$piece_woodpole", "Wood Pole", "Building", "Wood", 64, 52),
            ("$piece_woodstair", "Wood Stairs", "Building", "Wood", 22, 22), ("$piece_woodgate", "Wood Gate", "Building", "Wood", 5, 5),
            ("$piece_darkwood_wall", "Core Wood Wall", "Building", "Core wood", 88, 60), ("$piece_darkwood_beam", "Core Wood Beam", "Building", "Core wood", 40, 30),
            ("$piece_darkwood_roof", "Core Wood Roof 45°", "Building", "Core wood", 36, 36), ("$piece_darkwood_floor", "Core Wood Floor", "Building", "Core wood", 30, 30),
            ("$piece_irongate", "Iron Gate", "Building", "Iron", 2, 2),
            ("$piece_stonewall1x1", "Stone Wall 1x1", "Stonecutter", "Stone", 140, 120), ("$piece_stonefloor2x2", "Stone Floor 2x2", "Stonecutter", "Stone", 90, 90),
            ("$piece_stonearch", "Stone Arch", "Stonecutter", "Stone", 24, 24), ("$piece_stonepillar", "Stone Pillar", "Stonecutter", "Stone", 18, 18),
            ("$piece_workbench", "Workbench", "Crafting", "Wood", 2, 2), ("$piece_forge", "Forge", "Crafting", "Bronze", 1, 1), ("$piece_cauldron", "Cauldron", "Crafting", "Iron", 1, 1),
            ("$piece_sharpstakes", "Sharp Stakes", "Misc", "Wood", 36, 30), ("$piece_portal_wood", "Portal", "Misc", "Fine wood", 2, 2),
            ("$piece_bed", "Bed", "Furniture", "Fine wood", 4, 4), ("$piece_throne", "Throne", "Furniture", "Fine wood", 1, 1), ("$piece_table", "Table", "Furniture", "Wood", 5, 5),
            ("$piece_chair", "Chair", "Furniture", "Wood", 14, 12), ("$piece_bench", "Bench", "Furniture", "Wood", 7, 7), ("$piece_chest", "Chest", "Furniture", "Iron", 12, 9),
            ("$piece_brazierceiling", "Hanging Brazier", "Furniture", "Iron", 6, 6),
        };

        static readonly Dictionary<string, string> Tabs = new Dictionary<string, string>(), Materials = new Dictionary<string, string>();

        /// <summary>Rowan's hall in the placed-pieces counter and its baseline, with the names, tabs and materials the panel reads.</summary>
        public static void Hall(PanelInput full)
        {
            var baseline = full.Baseline != null && full.Baseline.TryGetValue(LocalTotals.PlacedKind, out var b) ? b : null;
            foreach (var p in Pieces)
            {
                if (!PanelSample.Names.ContainsKey(p.token)) PanelSample.Names[p.token] = p.name;
                Tabs[p.token] = p.tab; Materials[p.token] = p.material;
                full.PiecesPlaced[p.token] = p.now;
                if (baseline != null && p.atInstall > 0) baseline[p.token] = Math.Min(p.atInstall, p.now);
            }
            full.PieceTab = t => t != null && Tabs.TryGetValue(t, out var tab) ? tab : null;
            full.PieceMaterial = t => t != null && Materials.TryGetValue(t, out var m) ? m : null;
        }
    }
}
