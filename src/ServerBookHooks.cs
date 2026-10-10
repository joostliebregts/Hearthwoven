using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// Server only (0.6): keeps ServerBook (the arithmetic, tested without the game) and hands each player's part to GroupShare.
    /// Cargo: ChestWatch passes every ship and cart line as it writes it. Births: a ZDO the server sees for the first time (a new
    /// object a player's PC created and sent; its prefab is still 0 before the data arrives, ZDOMan.RPC_ZDOData -> CreateNewZDO)
    /// that is a young animal (Growup + Character) and tamed. Saved with every world save (BepInEx/Hearthwoven/server-book-&lt;world&gt;.&lt;id&gt;.json,
    /// AtomicFile: a flushed temp file moved over the old one, which stays as .bak), loaded at server start (the .bak when the file
    /// is unreadable); a dedicated server without a book rebuilds the cargo once from that world's chest logs (ServerBook.Replay).
    /// Only reads the game, never changes it.
    /// </summary>
    static class ServerBookHooks
    {
        internal static ServerBook Book;
        static string path, world;
        static readonly Dictionary<string, string> tokens = new Dictionary<string, string>();
        static readonly Dictionary<int, bool> young = new Dictionary<int, bool>();
        static readonly HashSet<ZDOID> counted = new HashSet<ZDOID>();
        static readonly Queue<ZDOID> countedOrder = new Queue<ZDOID>();

        static bool IsServer => ZNet.instance != null && ZNet.instance.IsServer();

        /// <summary>The running world's id (World.m_uid) as text, the same on the server and every client; null when there is no world.</summary>
        internal static string WorldUid()
        {
            try { var w = ZNet.World; return w != null && w.m_uid != 0 ? w.m_uid.ToString(System.Globalization.CultureInfo.InvariantCulture) : null; }
            catch { return null; }
        }
        static string Root => Path.Combine(BepInEx.Paths.BepInExRootPath, "Hearthwoven");

        /// <summary>Server start (ChestWatch.Started, after the server-start marker): load this world's book, or begin one.</summary>
        internal static void Start()
        {
            try
            {
                if (!IsServer) return;
                var w = ZNet.instance.GetWorldName() ?? "";
                var uid = WorldUid();
                if (Book != null && w + "|" + uid == world) return;
                world = w + "|" + uid;
                Book = null; path = null;   // another world: nothing of the one before is credited or saved, even if what follows fails
                Directory.CreateDirectory(Root);
                path = ServerBook.BookPath(Root, w, uid, out var adopted);   // 0.8: per world id, so a world made again under the same name starts its own
                if (adopted != null) Debug.Log("[Hearthwoven] " + adopted);
                try { Book = ServerBook.Load(path, out var problem); if (problem != null) Debug.LogWarning("[Hearthwoven] " + problem); }
                catch (Exception e)
                {
                    // the file is there but cannot be opened (locked, no rights): never saved over this run; counting starts afresh in memory
                    Book = new ServerBook { BornFrom = DateTime.UtcNow };
                    Debug.LogWarning("[Hearthwoven] server book could not be opened (" + e.Message + "); not saved this run, so the file stays as it is");
                    path = null;
                    return;
                }
                if (Book != null) Debug.Log("[Hearthwoven] server book loaded: " + Book.Carriers + " ships and carts with cargo, " + Book.Players + " players");
                if (Book == null)
                {
                    Book = new ServerBook { BornFrom = DateTime.UtcNow };
                    if (ZNet.instance.IsDedicated())   // a PC hosting a world starts from now
                    {
                        var r = Book.ReplayLogs(Root, w, out var files, uid);
                        Debug.Log($"[Hearthwoven] server book of world {w} rebuilt from {files} chest logs: {r.Lines} lines, {r.Applied} ship and cart lines applied, {r.RolledBack} rolled back with the world, {r.OtherWorld} of other worlds left out, {r.Unreadable} unreadable");
                    }
                    Save();
                }
            }
            catch (Exception e) { Book = Book ?? new ServerBook { BornFrom = DateTime.UtcNow }; Debug.LogWarning("[Hearthwoven] server book: " + e.Message + (path == null ? "; not saved this run" : "")); }
        }

        /// <summary>With every world save (ChestWatch.Saved): the book as it is now, so a restart without a save rolls it back with the world.</summary>
        internal static void Save()
        {
            try
            {
                if (!IsServer || Book == null || path == null) return;
                AtomicFile.Write(path, Book.ToJson());   // temp, flushed, replaced; the save before stays as .bak
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] server book not saved: " + e.Message); }
        }

        /// <summary>One chest-log event (ChestWatch.Write): ships and carts go into the book.</summary>
        internal static void Chest(string kind, string container, string action, string via, string playerKey, string item, int count, float x, float z)
        {
            try { if (Book != null && ServerBook.IsCarrier(kind)) Book.Cargo(container, kind, action, via, playerKey, item, count, x, z, DateTime.UtcNow); }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] server book cargo: " + e.Message); }
        }

        /// <summary>An item prefab ("IronScrap") as the game's token ("$item_ironscrap"), the key the client's own cargo uses; else the prefab.</summary>
        static string Token(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return prefab;
            if (tokens.TryGetValue(prefab, out var t)) return t;
            try { t = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name; } catch { t = null; }
            if (string.IsNullOrEmpty(t)) return prefab;   // not known yet: not cached, asked again next time
            return tokens[prefab] = t;
        }

        /// <summary>
        /// For GroupShare's answer: {"self":{...},"players":{name:{...}}}, the requester's own numbers and each listed sharer's
        /// (ServerBook.SharedJson); "{}" when the book has nothing for any of them. With <paramref name="fellows"/> (FellowIds keys, one per
        /// name) the same numbers again under "byKey", so two fellows with one name each keep their own (an older reader ignores it).
        /// </summary>
        internal static string Shared(string ownKey, IList<string> keys, IList<string> names, IList<string> fellows = null)
        {
            if (Book == null) return "{}";
            var b = new System.Text.StringBuilder("{");
            var own = ownKey == null ? null : Book.SharedJson(ownKey, Token);
            if (own != null) b.Append("\"self\":").Append(own);
            var players = new List<string>(); var byKey = new List<string>();
            for (int i = 0; i < keys.Count && i < names.Count; i++)
            {
                var p = Book.SharedJson(keys[i], Token);
                if (p != null && !string.IsNullOrEmpty(names[i])) players.Add(Json.Q(names[i]) + ":" + p);
                if (p != null && fellows != null && i < fellows.Count && !string.IsNullOrEmpty(fellows[i])) byKey.Add(Json.Q(fellows[i]) + ":" + p);
            }
            if (players.Count > 0) b.Append(own != null ? "," : "").Append("\"players\":{").Append(string.Join(",", players)).Append('}');
            if (byKey.Count > 0) b.Append(own != null || players.Count > 0 ? "," : "").Append("\"byKey\":{").Append(string.Join(",", byKey)).Append('}');
            return b.Append('}').ToString();
        }

        // ---------- born near ----------

        static bool Young(int prefab)
        {
            if (young.TryGetValue(prefab, out var y)) return y;
            var go = ZNetScene.instance?.GetPrefab(prefab);
            if (go == null) return false;   // not known yet: asked again next time
            return young[prefab] = go.GetComponent<Growup>() != null && go.GetComponent<Character>() != null;
        }

        static Dictionary<ZDOID, long> deadZDOs => ZDOMan.instance == null ? null : deadField?.GetValue(ZDOMan.instance) as Dictionary<ZDOID, long>;
        static readonly System.Reflection.FieldInfo deadField = AccessTools.Field(typeof(ZDOMan), "m_deadZDOs");

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
        static class BornWatch
        {
            // before the data: a ZDO the server has never had carries prefab 0 (ZDO.Valid sets it), every known one its real prefab
            static void Prefix(ZDO __instance, out bool __state)
            {
                __state = false;
                try { __state = __instance != null && __instance.GetPrefab() == 0 && IsServer; }
                catch (Exception e) { __state = false; HookGuard.Fail(e, "ServerBookHooks.BornWatch.Prefix"); }
            }

            static void Postfix(ZDO __instance, bool __state)
            {
                if (!__state) return;
                try
                {
                    if (Book == null || !Young(__instance.GetPrefab()) || !__instance.GetBool(ZDOVars.s_tamed)) return;   // wild young are not tamed
                    var id = __instance.m_uid;
                    if (counted.Contains(id) || (deadZDOs != null && deadZDOs.ContainsKey(id))) return;   // once; never a removed one revived
                    counted.Add(id); countedOrder.Enqueue(id);
                    if (countedOrder.Count > 2000) counted.Remove(countedOrder.Dequeue());
                    var pos = __instance.GetPosition();
                    var peers = new List<KeyValuePair<string, float[]>>();
                    foreach (var peer in ZNet.instance.GetPeers())
                        if (PeerIdentity.Id(peer).HasValue) peers.Add(new KeyValuePair<string, float[]>(PeerIdentity.Key(peer), new[] { peer.m_refPos.x, peer.m_refPos.y, peer.m_refPos.z }));
                    var near = ServerBook.Near(peers, pos.x, pos.y, pos.z);
                    var creature = ZNetScene.instance?.GetPrefab(__instance.GetPrefab())?.name ?? "#" + __instance.GetPrefab();
                    Book.Birth(creature, near);
                    // one line per birth, so the count can be checked afterwards (never silent)
                    File.AppendAllText(Path.Combine(Root, "births-" + DailyLogs.Day(DateTime.UtcNow) + ".jsonl"),
                        new Json().Open().Str("t", DateTime.UtcNow.ToString("o")).Str("creature", creature).Str("id", id.ToString()).Num("x", pos.x).Num("z", pos.z)
                                  .Str("near", string.Join(",", near)).Close() + "\n");
                }
                catch (Exception e) { HookGuard.Fail(e, "ServerBookHooks.BornWatch.Postfix"); }
            }
        }
    }
}
