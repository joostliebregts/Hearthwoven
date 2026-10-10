using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// The game side of SalvageWatch (0.8, Salvage.cs): which drops in the world are a piece's materials, so ClientHooks.Pickup books them
    /// as recovered and never as brought in. Read only: it remembers network ids, nothing in the world changes (no ZDO write, no flag).
    ///   DropBack (Piece.DropResources, on the PC that hosts the piece): the drops it makes for a piece a player built are remembered exactly.
    ///     Woke collects them as they wake; the finalizer names them by their network id once the game has set them up.
    ///   Woke (ItemDrop.Awake): a drop made inside DropBack is collected; a fresh drop another PC made is handed to the watch (Appeared).
    ///   CameDown (ZDOMan.HandleDestroyedZDO, which every PC that knows the object runs): a piece a player built that another PC brought
    ///     down, with the materials its prefab drops as items (WearNTear pieces without a loot crate; a crate is a chest, never a pickup).
    /// Only on a PC someone plays on (a client, a listen host, singleplayer), dead or alive: never on a dedicated server or in the menu.
    /// </summary>
    static class SalvageHooks
    {
        internal static readonly SalvageWatch<ZDOID> Watch = new SalvageWatch<ZDOID>();
        static int dropping;   // > 0 inside Piece.DropResources of a piece a player built, on a PC someone plays on
        static readonly List<ItemDrop> made = new List<ItemDrop>();   // the drops woken inside it, named in its finalizer
        const int MaxMade = 256;
        static bool Playing => ZNet.instance != null && !ZNet.instance.IsDedicated();   // not Player.m_localPlayer: it is null while you lie dead

        /// <summary>Whether a drop is a piece's materials (ClientHooks.Pickup, inside its guard).</summary>
        internal static bool FromPiece(GameObject go)
        {
            var zdo = go != null ? go.GetComponent<ZNetView>()?.GetZDO() : null;
            return zdo != null && Watch.Recovered(zdo.m_uid, Time.time);
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
        static class DropBack
        {
            static void Prefix(Piece __instance, out bool __state)
            {
                __state = false;
                try
                {
                    if (!Playing || __instance == null || !__instance.IsPlacedByPlayer()) return;   // a ruin's materials are found, not recovered
                    if (dropping == 0) made.Clear();
                    dropping++; __state = true;
                }
                catch (Exception e) { __state = false; HookGuard.Fail(e, "SalvageHooks.DropBack.Prefix"); }
            }
            static void Finalizer(bool __state)   // runs even if the game's method throws
            {
                try
                {
                    if (!__state) return;
                    if (dropping > 0) dropping--;
                    if (dropping > 0) return;
                    var now = Time.time;
                    foreach (var d in made)
                    {
                        var zdo = d != null ? d.GetComponent<ZNetView>()?.GetZDO() : null;
                        if (zdo != null) Watch.Created(zdo.m_uid, now);
                    }
                    made.Clear();
                }
                catch (Exception e) { HookGuard.Fail(e, "SalvageHooks.DropBack.Finalizer"); }
            }
        }

        [HarmonyPatch(typeof(ItemDrop), "Awake")]
        static class Woke
        {
            static void Postfix(ItemDrop __instance)
            {
                try
                {
                    if (__instance == null || !Playing) return;
                    if (dropping > 0) { if (made.Count < MaxMade) made.Add(__instance); return; }
                    var zdo = __instance.GetComponent<ZNetView>()?.GetZDO();
                    if (zdo == null || ZDOMan.instance == null || zdo.m_uid.UserID == ZDOMan.GetSessionID()) return;   // made here outside DropResources: not a piece's
                    var data = __instance.m_itemData;
                    if (data?.m_shared == null || data.m_pickedUp) return;   // someone held it: never a piece's fresh drop
                    var p = __instance.transform.position;
                    Watch.Appeared(zdo.m_uid, data.m_shared.m_name, data.m_stack, p.x, p.y, p.z,
                                   zdo.GetLong(ZDOVars.s_spawnTime, 0L) / (double)TimeSpan.TicksPerSecond, ZNet.instance.GetTimeSeconds(), Time.time);
                }
                catch (Exception e) { HookGuard.Fail(e, "SalvageHooks.Woke.Postfix"); }
            }
        }

        // prefab hash -> the materials a piece of it drops as items and how high above it; null = none (not a built piece, a loot crate)
        sealed class Salvageable { public float Height; public List<KeyValuePair<string, int>> Materials; }
        static readonly Dictionary<int, Salvageable> byPrefab = new Dictionary<int, Salvageable>();

        static Salvageable Of(int prefab)
        {
            if (byPrefab.TryGetValue(prefab, out var s)) return s;
            var go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            if (go == null) return null;   // not known (yet): asked again next time
            var piece = go.GetComponent<Piece>();
            if (piece != null && go.GetComponent<WearNTear>() != null && piece.m_destroyedLootPrefab == null && piece.m_resources != null)
            {
                var list = new List<KeyValuePair<string, int>>();
                foreach (var r in piece.m_resources)
                {
                    var name = r != null && r.m_recover && r.m_resItem != null ? r.m_resItem.m_itemData?.m_shared?.m_name : null;
                    if (!string.IsNullOrEmpty(name) && r.m_amount > 0) list.Add(new KeyValuePair<string, int>(name, r.m_amount));
                }
                if (list.Count > 0) s = new Salvageable { Height = piece.m_returnResourceHeightOffset, Materials = list };
            }
            byPrefab[prefab] = s;
            return s;
        }

        /// <summary>Every destroyed network object passes here: one lookup, then only a built piece another PC hosted.</summary>
        [HarmonyPatch(typeof(ZDOMan), "HandleDestroyedZDO")]
        static class CameDown
        {
            static void Prefix(ZDOID uid)
            {
                try
                {
                    if (!Playing) return;
                    var zdo = ZDOMan.instance?.GetZDO(uid);
                    if (zdo == null || zdo.IsOwner()) return;   // hosted here: DropResources ran here (DropBack, exact)
                    var prefab = zdo.GetPrefab(); if (prefab == 0) return;
                    var s = Of(prefab); if (s == null) return;
                    if (zdo.GetLong(ZDOVars.s_creator, 0L) == 0L) return;   // the world's own piece (a ruin): its materials are found, not recovered
                    var p = zdo.GetPosition();
                    Watch.CameDown(p.x, p.y + s.Height, p.z, s.Materials, ZNet.instance.GetTimeSeconds(), Time.time);
                }
                catch (Exception e) { HookGuard.Fail(e, "SalvageHooks.CameDown.Prefix"); }
            }
        }
    }
}
