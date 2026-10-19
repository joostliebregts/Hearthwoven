using System;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// 0.9, record 1: the moment you upgrade an item at a crafting station (InventoryGui.DoCrafting with an item to upgrade). Read only: the prefix
    /// notes the very item and its level, the postfix reads what the game put in its slot (UpgradeJudge: a higher level of the same item is an
    /// upgrade; the old item still in the bag, a broken or a lowered one is not), the station you stand at and its level, and books it in the
    /// UpgradeLog (Plugin.Upgraded). The game's own Upgrades counter is not touched; the item-craft counter (m_itemCraftStats) never counts an
    /// upgrade (DoCrafting books IncrementStatItemCraft only when no item is upgraded: work/hearthwoven-0.9/TRACK-record.md).
    /// </summary>
    static class UpgradeHooks
    {
        static readonly GameField<InventoryGui, ItemDrop.ItemData> upgradeItem = new GameField<InventoryGui, ItemDrop.ItemData>("m_craftUpgradeItem");
        static readonly GameField<InventoryGui, Recipe> craftRecipe = new GameField<InventoryGui, Recipe>("m_craftRecipe");

        internal sealed class Before { public ItemDrop.ItemData Item; public string Token; public int Level; public Vector2i Slot; }

        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        static class Upgrade
        {
            static void Prefix(InventoryGui __instance, Player player, out Before __state)
            {
                Before b = null;
                HookGuard.Run(() =>
                {
                    if (__instance == null || player == null || player != Player.m_localPlayer) return;
                    var item = upgradeItem.Of(__instance);
                    var recipe = craftRecipe.Of(__instance);
                    if (item == null || recipe == null || recipe.m_item == null) return;
                    b = new Before { Item = item, Token = recipe.m_item.m_itemData?.m_shared?.m_name, Level = item.m_quality, Slot = item.m_gridPos };
                });
                __state = b;
            }

            static void Postfix(Before __state) => HookGuard.Run(() =>   // only the hook's own state: the player is the local one (the prefix checked)
            {
                var player = Player.m_localPlayer;
                if (__state == null || player == null) return;
                var inv = player.GetInventory(); if (inv == null) return;
                var now = inv.GetItemAt(__state.Slot.x, __state.Slot.y);
                var to = UpgradeJudge.NewLevel(inv.ContainsItem(__state.Item), now?.m_shared?.m_name, now?.m_quality ?? 0, __state.Token, __state.Level);
                if (to == null) return;
                var station = player.GetCurrentCraftingStation();
                Plugin.Upgraded(new UpgradeMoment
                {
                    Token = __state.Token, From = __state.Level, To = to.Value, Utc = DateTime.UtcNow,
                    Station = station ? Utils.GetPrefabName(station.gameObject) : "", StationLevel = station ? station.GetLevel() : 0,
                });
            });
        }
    }
}
