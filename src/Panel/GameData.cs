using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// What the game's own data says about the tokens in the character's counters, so the panel groups them the way the
    /// game does, mods included. Read once, after ObjectDB and ZNetScene are loaded:
    /// - items: the item type (the same switch the game uses for CraftWeapon/CraftFood..., PanelModel.ItemKindOf);
    /// - gathering: what felled logs split into is wood, what a pickaxe-only rock, ore vein or pile drops is mining
    ///   (PanelModel.GatherKindOf); what any smelter, kiln or refinery makes (Coal, Iron, ...) is never gathered, even when
    ///   some drop table also holds it (B12). The standing tree's own drops are left out: they are resin, cones and seeds,
    ///   not wood. PanelModel checks the vanilla names first;
    /// - pieces: TerrainOp/TerrainModifier = groundwork, Plant = planted, Feast = feast, the rest built.
    /// Reads only. An unknown token gets null: PanelModel keeps such an item out of gear and food, leaves such a pickup out
    /// of wood and mining, and counts such a piece as built unless it carries a vanilla groundwork name.
    /// </summary>
    static class GameData
    {
        static readonly Dictionary<string, string> items = new Dictionary<string, string>(), gather = new Dictionary<string, string>(), pieces = new Dictionary<string, string>();
        static readonly Dictionary<string, string> crops = new Dictionary<string, string>(), types = new Dictionary<string, string>();   // Deeds: Farming, Crafting
        static readonly Dictionary<string, string> tokens = new Dictionary<string, string>();   // Deeds: Cooking (item prefab -> token)
        static readonly HashSet<string> stationDishes = new HashSet<string>();   // Deeds: Cooking (what cooking stations hand out, by item token)
        static readonly Dictionary<string, string> materials = new Dictionary<string, string>();   // Deeds: Crafting (item token -> main material)
        static readonly Dictionary<string, string> pieceTabs = new Dictionary<string, string>(), tabLabels = new Dictionary<string, string>(), pieceMats = new Dictionary<string, string>();   // Deeds: Building (piece token -> category; category -> the hammer tab's label; piece token -> main material)
        static readonly Dictionary<string, Sprite> pieceIcons = new Dictionary<string, Sprite>();
        static bool ready;

        public static string ItemKind(string token) { Ensure(); return token != null && items.TryGetValue(token, out var k) ? k : null; }
        public static string GatherKind(string token) { Ensure(); return token != null && gather.TryGetValue(token, out var k) ? k : null; }
        public static string PieceKind(string token) { Ensure(); return token != null && pieces.TryGetValue(token, out var k) ? k : null; }
        /// <summary>A planted piece's harvest: the item prefab its grown plant's Pickable yields ("Barley"); null = not a crop.</summary>
        public static string CropOf(string token) { Ensure(); return token != null && crops.TryGetValue(token, out var k) ? k : null; }
        /// <summary>The game's item type of an item token ("OneHandedWeapon"); null = not known.</summary>
        public static string ItemType(string token) { Ensure(); return token != null && types.TryGetValue(token, out var k) ? k : null; }
        /// <summary>True when a cooking station (grill, iron cooking station, oven, any mod's CookingStation) hands this dish out: the game books it as CraftGrill, to whoever takes it off. False = made in the crafting window, or not known.</summary>
        public static bool StationDish(string token) { Ensure(); return token != null && stationDishes.Contains(token); }
        /// <summary>An item's main material from its recipe ("Bronze"): the ingredient that best marks the tier (PanelModel.MainMaterial); null = no recipe or no ingredient that marks a tier.</summary>
        public static string MainMaterial(string token) { Ensure(); return token != null && materials.TryGetValue(token, out var k) ? k : null; }
        /// <summary>The hammer tab a piece sits on ("Furniture"): the label its piece table gives that category, else the category's own name; null = not known (Piece.m_category).</summary>
        public static string PieceTab(string token)
        {
            Ensure();
            if (token == null || !pieceTabs.TryGetValue(token, out var cat)) return null;
            return tabLabels.TryGetValue(cat, out var label) ? label : PanelModel.TabNameOf(cat);
        }
        /// <summary>A piece's main material from its build resources ("Fine wood"), judged by PanelModel.PieceMainMaterial; null = no resource marks one.</summary>
        public static string PieceMaterial(string token) { Ensure(); return token != null && pieceMats.TryGetValue(token, out var k) ? k : null; }
        /// <summary>An item prefab's token ("CookedMeat" -> "$item_cookedmeat"): what eaters record against what the craft counter books.</summary>
        public static string ItemToken(string prefab) { Ensure(); return prefab != null && tokens.TryGetValue(prefab, out var k) ? k : null; }
        /// <summary>The game's own sprite of a piece by its name token ("$piece_woodwall"), as the build menu shows it.</summary>
        public static Sprite PieceIcon(string token) { Ensure(); return token != null && pieceIcons.TryGetValue(token, out var s) ? s : null; }

        // the game's own word for a material the panel does not know by name (a mod's bar)
        static string LocalizedName(string token) { try { return Localization.instance != null ? Localization.instance.Localize(token) : token; } catch { return token; } }

        static void Ensure()
        {
            if (ready || !ObjectDB.instance || ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0 || !ZNetScene.instance) return;
            ready = true;
            try
            {
                var pieceObjs = new Dictionary<string, Piece>();   // the first Piece of each name, for its build resources (judged once the smelters are known)
                void LearnTabs(PieceTable table)
                {
                    if (table == null || table.m_categories == null) return;
                    for (int i = 0; i < table.m_categories.Count; i++)
                    {
                        var label = table.m_categoryLabels != null && i < table.m_categoryLabels.Count ? table.m_categoryLabels[i] : null;
                        var key = table.m_categories[i].ToString();
                        if (!string.IsNullOrWhiteSpace(label) && !tabLabels.ContainsKey(key)) tabLabels[key] = LocalizedName(label);
                    }
                }
                void Piece(GameObject go)
                {
                    var p = go ? go.GetComponent<Piece>() : null;
                    if (!p || string.IsNullOrEmpty(p.m_name)) return;
                    if (!pieceTabs.ContainsKey(p.m_name)) pieceTabs[p.m_name] = p.m_category.ToString();
                    if (!pieceObjs.ContainsKey(p.m_name)) pieceObjs[p.m_name] = p;
                    var kind = PanelModel.PieceKindOf(go.GetComponent<TerrainOp>() || go.GetComponent<TerrainModifier>(), go.GetComponent<Plant>(), go.GetComponent<Feast>());
                    if (!pieces.ContainsKey(p.m_name) || pieces[p.m_name] == "built") pieces[p.m_name] = kind;
                    if (p.m_icon && !pieceIcons.ContainsKey(p.m_name)) pieceIcons[p.m_name] = p.m_icon;
                    var plant = go.GetComponent<Plant>();
                    if (plant && plant.m_grownPrefabs != null && !crops.ContainsKey(p.m_name))
                        foreach (var grown in plant.m_grownPrefabs)
                        {
                            var pick = grown ? grown.GetComponent<Pickable>() : null;
                            if (pick && pick.m_itemPrefab) { crops[p.m_name] = pick.m_itemPrefab.name; break; }
                        }
                }
                foreach (var go in ObjectDB.instance.m_items)
                {
                    var shared = go ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                    if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;
                    if (!items.ContainsKey(shared.m_name)) items[shared.m_name] = PanelModel.ItemKindOf(shared.m_itemType.ToString(), shared.m_name);
                    if (!types.ContainsKey(shared.m_name)) types[shared.m_name] = shared.m_itemType.ToString();
                    if (!tokens.ContainsKey(go.name)) tokens[go.name] = shared.m_name;
                    LearnTabs(shared.m_buildPieces);
                    if (shared.m_buildPieces?.m_pieces != null) foreach (var p in shared.m_buildPieces.m_pieces) Piece(p);   // hammer, hoe, cultivator, feast tray, mods' tools
                }
                var drops = new List<KeyValuePair<DropTable, string>>();
                var refined = new HashSet<string>();   // what smelters, kilns and refineries make
                void Gather(DropTable table, string component, HitData.DamageModifiers mods)
                {
                    var kind = PanelModel.GatherKindOf(component, mods.m_chop.ToString(), mods.m_pickaxe.ToString());
                    if (kind != null && table != null) drops.Add(new KeyValuePair<DropTable, string>(table, kind));
                }
                foreach (var go in ZNetScene.instance.m_prefabs)
                {
                    if (!go) continue;
                    Piece(go);
                    var log = go.GetComponent<TreeLog>(); if (log) Gather(log.m_dropWhenDestroyed, "TreeLog", log.m_damages);
                    var rock = go.GetComponent<MineRock>(); if (rock) Gather(rock.m_dropItems, "MineRock", rock.m_damageModifiers);
                    var rock5 = go.GetComponent<MineRock5>(); if (rock5) Gather(rock5.m_dropItems, "MineRock5", rock5.m_damageModifiers);
                    var destructible = go.GetComponent<Destructible>(); var dropper = go.GetComponent<DropOnDestroyed>();
                    if (destructible && dropper) Gather(dropper.m_dropWhenDestroyed, "Destructible", destructible.m_damages);
                    var cooker = go.GetComponent<CookingStation>();
                    if (cooker && cooker.m_conversion != null)
                        foreach (var c in cooker.m_conversion) { var dish = c?.m_to?.m_itemData?.m_shared?.m_name; if (!string.IsNullOrEmpty(dish)) stationDishes.Add(dish); }
                    var smelter = go.GetComponent<Smelter>();
                    if (smelter && smelter.m_conversion != null)
                        foreach (var c in smelter.m_conversion) { var made = c?.m_to?.m_itemData?.m_shared?.m_name; if (!string.IsNullOrEmpty(made)) refined.Add(made); }
                }
                foreach (var d in drops)
                    foreach (var dd in d.Key?.m_drops ?? new List<DropTable.DropData>())
                    {
                        var shared = dd.m_item ? dd.m_item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                        if (shared == null || string.IsNullOrEmpty(shared.m_name) || gather.ContainsKey(shared.m_name) || refined.Contains(shared.m_name)) continue;
                        gather[shared.m_name] = d.Value;
                    }
                // main material: every recipe's ingredients (ObjectDB.m_recipes), judged by PanelModel.MainMaterial; a bar is an ingredient some smelter makes
                if (ObjectDB.instance.m_recipes != null)
                    foreach (var r in ObjectDB.instance.m_recipes)
                    {
                        var made = r && r.m_item ? r.m_item.m_itemData?.m_shared?.m_name : null;
                        if (string.IsNullOrEmpty(made) || materials.ContainsKey(made) || r.m_resources == null) continue;
                        var recipe = new List<Ingredient>();
                        foreach (var req in r.m_resources)
                        {
                            var shared = req != null && req.m_resItem ? req.m_resItem.m_itemData?.m_shared : null;
                            if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;
                            recipe.Add(new Ingredient { Token = shared.m_name, Refined = refined.Contains(shared.m_name), Name = refined.Contains(shared.m_name) ? LocalizedName(shared.m_name) : null });
                        }
                        var m = PanelModel.MainMaterial(recipe);
                        if (m != null) materials[made] = m;
                    }
                // pieces: the same for what a piece costs to build (Piece.m_resources); a resource only counts as a new material when the game calls it one
                foreach (var kv in pieceObjs)
                {
                    var recipe = new List<PieceIngredient>();
                    foreach (var req in kv.Value.m_resources ?? new Piece.Requirement[0])
                    {
                        var shared = req != null && req.m_resItem ? req.m_resItem.m_itemData?.m_shared : null;
                        if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;
                        recipe.Add(new PieceIngredient { Token = shared.m_name, Amount = req.m_amount, Refined = refined.Contains(shared.m_name), Material = shared.m_itemType == ItemDrop.ItemData.ItemType.Material, Name = LocalizedName(shared.m_name) });
                    }
                    var m = PanelModel.PieceMainMaterial(recipe);
                    if (m != null) pieceMats[kv.Key] = m;
                }
                Debug.Log("[Hearthwoven] panel game data: " + items.Count + " items, " + pieces.Count + " pieces, " + gather.Count + " gathered kinds");
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel game data: " + e.Message); }
        }
    }
}
