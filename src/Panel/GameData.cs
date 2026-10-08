using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// What the game's own data says about the tokens in the character's counters, so the panel groups them the way the
    /// game does, mods included. Read once, after ObjectDB and ZNetScene are loaded:
    /// - items: the item type (the same switch the game uses for CraftWeapon/CraftFood..., PanelModel.ItemKindOf);
    /// - gathering: what felled logs split into is wood, what rocks and ore veins drop is mining. The standing tree's own
    ///   drops are left out: they are resin, cones and seeds, not wood. PanelModel checks the vanilla names first;
    /// - pieces: TerrainOp/TerrainModifier = groundwork, Plant = planted, Feast = feast, the rest built.
    /// Reads only. An unknown token gets null: PanelModel keeps such an item out of gear and food, leaves such a pickup out
    /// of wood and mining, and counts such a piece as built unless it carries a vanilla groundwork name.
    /// </summary>
    static class GameData
    {
        static readonly Dictionary<string, string> items = new Dictionary<string, string>(), gather = new Dictionary<string, string>(), pieces = new Dictionary<string, string>();
        static bool ready;

        public static string ItemKind(string token) { Ensure(); return token != null && items.TryGetValue(token, out var k) ? k : null; }
        public static string GatherKind(string token) { Ensure(); return token != null && gather.TryGetValue(token, out var k) ? k : null; }
        public static string PieceKind(string token) { Ensure(); return token != null && pieces.TryGetValue(token, out var k) ? k : null; }

        static void Ensure()
        {
            if (ready || !ObjectDB.instance || ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0 || !ZNetScene.instance) return;
            ready = true;
            try
            {
                void Piece(GameObject go)
                {
                    var p = go ? go.GetComponent<Piece>() : null;
                    if (!p || string.IsNullOrEmpty(p.m_name)) return;
                    var kind = PanelModel.PieceKindOf(go.GetComponent<TerrainOp>() || go.GetComponent<TerrainModifier>(), go.GetComponent<Plant>(), go.GetComponent<Feast>());
                    if (!pieces.ContainsKey(p.m_name) || pieces[p.m_name] == "built") pieces[p.m_name] = kind;
                }
                foreach (var go in ObjectDB.instance.m_items)
                {
                    var shared = go ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                    if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;
                    if (!items.ContainsKey(shared.m_name)) items[shared.m_name] = PanelModel.ItemKindOf(shared.m_itemType.ToString(), shared.m_name);
                    if (shared.m_buildPieces?.m_pieces != null) foreach (var p in shared.m_buildPieces.m_pieces) Piece(p);   // hammer, hoe, cultivator, feast tray, mods' tools
                }
                var drops = new List<KeyValuePair<DropTable, string>>();
                foreach (var go in ZNetScene.instance.m_prefabs)
                {
                    if (!go) continue;
                    Piece(go);
                    var log = go.GetComponent<TreeLog>(); if (log) drops.Add(new KeyValuePair<DropTable, string>(log.m_dropWhenDestroyed, "wood"));
                    var rock = go.GetComponent<MineRock>(); if (rock) drops.Add(new KeyValuePair<DropTable, string>(rock.m_dropItems, "mining"));
                    var rock5 = go.GetComponent<MineRock5>(); if (rock5) drops.Add(new KeyValuePair<DropTable, string>(rock5.m_dropItems, "mining"));
                }
                foreach (var d in drops)
                    foreach (var dd in d.Key?.m_drops ?? new List<DropTable.DropData>())
                    {
                        var shared = dd.m_item ? dd.m_item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                        if (shared == null || string.IsNullOrEmpty(shared.m_name) || gather.ContainsKey(shared.m_name)) continue;
                        gather[shared.m_name] = d.Value;
                    }
                Debug.Log("[Hearthwoven] panel game data: " + items.Count + " items, " + pieces.Count + " pieces, " + gather.Count + " gathered kinds");
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel game data: " + e.Message); }
        }
    }
}
