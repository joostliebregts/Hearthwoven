using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// Server only, no client mod needed: who put what into which chest, cart or ship, and who took it out.
    /// Every container update a player's PC sends (ZDOMan.RPC_ZDOData -> ZDO.Deserialize) is compared with the server's
    /// own previous copy, so a server restart misses nothing and a repeated update counts nothing twice.
    /// Who: the PC that sent the change; with MultiUserChest, the request that passed the server names the real mover.
    /// We only read other mods' messages, never change or block them. Rules and tests: INTEGRITY.md.
    /// </summary>
    public static class ChestWatch
    {
        static readonly int ItemsHash = StringExtensionMethods.GetStableHashCode("items");
        static readonly int CreatorHash = StringExtensionMethods.GetStableHashCode("creator");
        static readonly Dictionary<int, string> MucMethods = new Dictionary<int, string>();
        static readonly Dictionary<ZDOID, List<ChestLedger.Claim>> pending = new Dictionary<ZDOID, List<ChestLedger.Claim>>();
        static readonly Dictionary<int, string> kinds = new Dictionary<int, string>();
        static long currentSender;   // the peer whose ZDO data is being applied right now (main thread only)
        static readonly int InUseHash = StringExtensionMethods.GetStableHashCode("InUse");
        static Dictionary<ZDOID, long> deadZDOs => ZDOMan.instance == null ? null : deadField?.GetValue(ZDOMan.instance) as Dictionary<ZDOID, long>;
        static readonly System.Reflection.FieldInfo deadField = AccessTools.Field(typeof(ZDOMan), "m_deadZDOs");

        /// <summary>Markers in the chest log: a world save, and a server start. Events after the last save that are followed
        /// by a start without a save in between were rolled back with the world (INTEGRITY C12); the companion drops them.</summary>
        internal static void Marker(string what)
        {
            try
            {
                var dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "Hearthwoven");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "chests-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".jsonl"),
                    new Json().Open().Str("t", DateTime.UtcNow.ToString("o")).Str("marker", what).Close() + "\n");
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] marker: " + e.Message); }
        }

        [HarmonyPatch(typeof(ZNet), "SaveWorld")]
        static class Saved { static void Postfix() { if (IsServer) Marker("world-saved"); } }

        static bool started;
        [HarmonyPatch(typeof(ZNet), "Start")]
        static class Started { static void Postfix() { if (IsServer && !started) { started = true; Marker("server-start"); } } }

        static ChestWatch()
        {
            foreach (var m in new[] { "MUC_RequestItemAdd", "MUC_RequestItemRemove", "MUC_RequestItemConsume", "MUC_RequestItemDrop" })
            {
                MucMethods[StringExtensionMethods.GetStableHashCode(m)] = m;
                MucMethods[StringExtensionMethods.GetStableHashCode(m + "Response")] = m + "Response";
            }
        }

        static bool IsServer => ZNet.instance != null && ZNet.instance.IsServer();

        /// <summary>Called for every routed RPC passing the server (Plugin.RoutedPatch). Remembers shared-chest requests.</summary>
        internal static void OnRouted(ZRoutedRpc.RoutedRPCData d)
        {
            if (d == null || !MucMethods.TryGetValue(d.m_methodHash, out var method)) return;
            var p = d.m_parameters; p.SetPos(0);
            var container = p.ReadZDOID();
            var request = p.ReadPackage();
            pending.TryGetValue(container, out var open);
            if (method.EndsWith("Response")) { ChestLedger.ApplyMucResponse(method, request, d.m_targetPeerID, open); return; }
            var claims = ChestLedger.ReadMucRequest(method, request, d.m_senderPeerID, Time.time);
            if (claims.Count == 0) return;
            if (!pending.TryGetValue(container, out var list)) pending[container] = list = new List<ChestLedger.Claim>();
            list.AddRange(claims);
            if (list.Count > 200) list.RemoveRange(0, list.Count - 200);
        }

        static string Kind(int prefabHash)
        {
            if (kinds.TryGetValue(prefabHash, out var k)) return k;
            var go = ZNetScene.instance?.GetPrefab(prefabHash);
            var name = go == null ? "" : go.name;
            k = go == null ? "chest" : go.GetComponent<TombStone>() ? "grave" : go.GetComponent<Vagon>() ? "cart" : go.GetComponentInChildren<Ship>() ? "ship"
              : name.StartsWith("CargoCrate") ? "crate"                         // cargo of a sunk ship or broken cart (Container.m_destroyedLootPrefab)
              : name.IndexOf("feeder", StringComparison.OrdinalIgnoreCase) >= 0 ? "feeder"   // a chest a mod empties by itself (SmelterUpgrades' Stoker's chest)
              : "chest";
            return kinds[prefabHash] = k;
        }

        [HarmonyPatch(typeof(ZDOMan), "RPC_ZDOData")]
        static class Sender
        {
            static void Prefix(ZRpc rpc)
            {
                currentSender = 0;
                if (!IsServer) return;
                foreach (var peer in ZNet.instance.GetPeers()) if (peer.m_rpc == rpc) { currentSender = peer.m_uid; break; }
            }
            static void Postfix() => currentSender = 0;
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
        static class Watch
        {
            // The server's own copy before the new data overwrites it (ZDOExtraData.Add replaces the array, so the old one stays intact).
            static void Prefix(ZDO __instance, out byte[] __state)
            {
                __state = null;
                try { if (IsServer) __state = ZDOExtraData.GetByteArray(__instance.m_uid, ItemsHash); } catch { }
            }

            static void Postfix(ZDO __instance, byte[] __state)
            {
                try
                {
                    if (!IsServer) return;
                    var bytes = ZDOExtraData.GetByteArray(__instance.m_uid, ItemsHash);
                    if (bytes == null || ReferenceEquals(bytes, __state)) return;
                    if (__state != null && Same(bytes, __state)) return;   // the common case: nothing changed, no parsing
                    if (deadZDOs != null && deadZDOs.ContainsKey(__instance.m_uid)) return;   // a removed object briefly revived by a late update
                    var kind = Kind(__instance.GetPrefab());
                    long owner = __instance.GetOwner(), sender = currentSender != 0 ? currentSender : owner;
                    pending.TryGetValue(__instance.m_uid, out var claims);
                    if (claims != null) ChestLedger.Expire(claims, Time.time);
                    var builder = ZDOExtraData.GetLong(__instance.m_uid, CreatorHash, 0L);
                    var before = __state == null ? new List<ChestLedger.Slot>() : ChestLedger.Parse(__state);
                    bool inUse = ZDOExtraData.GetInt(__instance.m_uid, InUseHash, 0) != 0;
                    var events = ChestLedger.Attribute(before, ChestLedger.Parse(bytes), kind, __state == null, builder != 0L, claims, sender, owner, inUse);
                    if (claims != null && claims.Count == 0) pending.Remove(__instance.m_uid);
                    if (events.Count == 0) return;
                    Write(__instance, kind, builder, events);
                }
                catch (Exception e) { Debug.LogWarning("[Hearthwoven] chest: " + e.Message); }
            }
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static string ItemName(int hash)
        {
            var prefab = ObjectDB.instance?.GetItemPrefab(hash);
            return prefab != null ? prefab.name : "#" + hash;
        }

        static void Write(ZDO zdo, string kind, long builder, List<ChestLedger.Event> events)
        {
            var container = ZNetScene.instance?.GetPrefab(zdo.GetPrefab());
            var pos = zdo.GetPosition();
            var t = DateTime.UtcNow.ToString("o");
            var lines = new System.Text.StringBuilder();
            foreach (var e in events)
            {
                var peer = ZNet.instance.GetPeer(e.Peer);
                var parts = e.Key.Split('|');
                lines.Append(new Json().Open().Str("t", t).Str("player", peer?.m_playerName ?? "?").Num("playerId", peer?.m_playerID ?? 0)
                    .Str("via", e.Via).Str("container", container ? container.name : "#" + zdo.GetPrefab()).Str("kind", kind)
                    .Str("containerId", zdo.m_uid.ToString()).Num("containerBuilder", builder).Num("x", pos.x).Num("z", pos.z)
                    .Str("item", ItemName(int.Parse(parts[0]))).Str("maker", parts[1]).Num("quality", int.Parse(parts[2]))
                    .Str("action", e.Action).Num("count", e.Count).Close()).Append('\n');
            }
            var dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "Hearthwoven");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "chests-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".jsonl"), lines.ToString());
        }
    }
}
