using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Hearthwoven;
using UnityEngine;

// TEST SERVER ONLY. Never install on a live server. Exercises the server side of Hearthwoven in the real runtime.
[BepInPlugin("com.joostliebregts.hearthwoven.selftest", "Hearthwoven.SelfTest", "0.2.0")]
[BepInDependency("com.joostliebregts.hearthwoven")]
public class SelfTest : BaseUnityPlugin
{
    bool done; float readyAt = -1f; int pass, fail;
    void Check(bool ok, string what) { if (ok) pass++; else fail++; Logger.LogInfo((ok ? "KS-SELFTEST PASS " : "KS-SELFTEST FAIL ") + what); }

    void Update()
    {
        if (done || ZNet.instance == null || !ZNet.instance.IsServer() || ZRoutedRpc.instance == null || ZNetScene.instance == null || ZDOMan.instance == null || ObjectDB.instance == null) return;
        if (readyAt < 0) { readyAt = Time.realtimeSinceStartup; return; }
        if (Time.realtimeSinceStartup - readyAt < 15f) return;
        done = true;
        try { Run(); } catch (Exception e) { fail++; Logger.LogError("KS-SELFTEST FAIL exception " + e); }
        Logger.LogInfo($"KS-SELFTEST DONE pass={pass} fail={fail}");
    }

    ZPackage Routed(string method, long target, object[] args)
    {
        var d = new ZRoutedRpc.RoutedRPCData { m_msgID = DateTime.UtcNow.Ticks, m_senderPeerID = 555L, m_targetPeerID = target,
            m_targetZDO = ZDOID.None, m_methodHash = StringExtensionMethods.GetStableHashCode(method) };
        ZRpc.Serialize(args, ref d.m_parameters);
        var pkg = new ZPackage(); d.Serialize(pkg); pkg.SetPos(0); return pkg;
    }

    void Run()
    {
        var root = Path.Combine(Paths.BepInExRootPath, "Hearthwoven");
        var routed = AccessTools.Method(typeof(ZRoutedRpc), "RPC_RoutedRPC");
        long serverId = (long)AccessTools.Field(typeof(ZRoutedRpc), "m_id").GetValue(ZRoutedRpc.instance);
        bool Send(ZPackage p) { try { routed.Invoke(ZRoutedRpc.instance, new object[] { null, p }); return true; } catch (Exception e) { Logger.LogInfo("KS-SELFTEST threw " + (e.InnerException ?? e).GetType().Name); return false; } }

        // 1. every hook attached
        // (no tuples: the game's Mono runtime has no System.ValueTuple)
        var types = new Type[] { typeof(Character), typeof(Character), typeof(Humanoid), typeof(MineRock5), typeof(MineRock), typeof(Destructible), typeof(Trader),
            typeof(Smelter), typeof(Smelter), typeof(Player), typeof(Feast), typeof(Skills), typeof(ZRoutedRpc), typeof(ZDO),
            typeof(ZDOMan), typeof(Character), typeof(Player), typeof(Humanoid), typeof(Humanoid) };
        var methods = new[] { "Damage", "ApplyDamage", "BlockAttack", "Damage", "Damage", "Damage", "OnBought", "OnAddOre", "OnAddFuel", "EatFood",
            "RPC_EatConfirmation", "RaiseSkill", "RPC_RoutedRPC", "Deserialize", "RPC_ZDOData", "RPC_Damage", "OnDeath", "EquipItem", "Pickup" };
        var missing = Enumerable.Range(0, types.Length).Where(i => { var m = AccessTools.Method(types[i], methods[i]); var info = m == null ? null : Harmony.GetPatchInfo(m);
            return info == null || !info.Owners.Contains(Plugin.Guid); }).Select(i => types[i].Name + "." + methods[i]).ToList();
        Check(missing.Count == 0, "all hook targets attached" + (missing.Count > 0 ? " (missing: " + string.Join(", ", missing) + ")" : ""));
        var ctor = AccessTools.Constructor(typeof(ZRoutedRpc), new[] { typeof(bool) });
        Check(Harmony.GetPatchInfo(ctor)?.Owners.Contains(Plugin.Guid) == true, "RPC registration hook on every new ZRoutedRpc");

        // 2. a fragmented profile from a 'player' reaches the server and is written
        var stats = new PlayerProfile.PlayerStats[10]; stats[0] = new PlayerProfile.PlayerStats();
        stats[0][PlayerStatType.DistanceSailHelm] = 9000f; stats[0].m_itemPickupStats["IronScrap"] = 812f;
        var big = string.Join("", Enumerable.Range(0, 300).Select(i => Guid.NewGuid().ToString("N")));   // random, so gzip cannot shrink it below one fragment
        stats[0].m_itemCraftStats[big] = 1f;
        var json = Snapshot.Build(Plugin.Version, 4242L, "SelfTestViking", stats, new Snapshot.SkillInfo[0], "KdlTest", new DamageTally(), "sess", new SessionEvents());
        var parts = Fragments.Split(Transport.Pack(json));
        var playersDir = Path.Combine(root, "players");
        // filed under the player's id, never under 0: the game's peer.m_playerID is 0 on a dedicated server (PeerIdentity);
        // this sender is no real peer, so the id comes from the snapshot itself
        var file = Path.Combine(playersDir, "4242.json");
        var zero = Path.Combine(playersDir, "0.json");
        if (File.Exists(file)) File.Delete(file);
        if (File.Exists(zero)) File.Delete(zero);
        for (int i = 0; i < parts.Count; i++)
        {
            var p = new ZPackage(); p.Write("selftest"); p.Write("msg1"); p.Write(i); p.Write(parts.Count); p.Write(parts[i]);
            Send(Routed(Plugin.RpcName, serverId, new object[] { p }));
            if (i < parts.Count - 1) Check(!File.Exists(file) || i > 0, "nothing written before the last fragment (" + (i + 1) + "/" + parts.Count + ")");
        }
        Check(parts.Count >= 2, $"profile was split ({parts.Count} fragments)");
        var written = File.Exists(file) ? File.ReadAllText(file) : "";
        Check(written.Contains("\"SelfTestViking\"") && written.Contains("DistanceSailHelm") && written.Contains("\"reason\":\"selftest\""), "server wrote the reassembled profile");
        Check(!File.Exists(zero) && !Directory.Exists(Path.Combine(playersDir, "0")) && written.Contains("\"playerKey\":\"4242\""), "profile filed under the player id (4242), no 0.json");
        Check(File.Exists(file) && File.ReadAllBytes(file).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }) == false, "profile file has no UTF-8 BOM");

        // 3. a server without the mod: an unknown routed RPC is ignored without error
        Check(Send(Routed("Hearthwoven_NotRegisteredHere", serverId, new object[] { new ZPackage() })), "unknown RPC (server without the mod) is ignored without error");

        // 4. routed damage is still logged (players without the mod)
        var hit = new HitData { m_skill = Skills.SkillType.Bows, m_hitType = HitData.HitType.PlayerHit }; hit.m_damage.m_pierce = 55f;
        int Lines(string prefix) { var f = Directory.GetFiles(root, prefix + "*.jsonl").FirstOrDefault(); if (f == null) return 0;
            using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var r = new StreamReader(fs)) return r.ReadToEnd().Split('\n').Count(l => l.Length > 0); }
        int d0 = Lines("damage-routed-");
        Send(Routed("RPC_Damage", 987654321L, new object[] { hit }));
        AccessTools.Field(typeof(Plugin), "routedWriter").GetValue(null)?.GetType().GetMethod("Flush").Invoke(AccessTools.Field(typeof(Plugin), "routedWriter").GetValue(null), null);
        Check(Lines("damage-routed-") == d0 + 1, "routed damage logged as fallback");

        // 5. chest watcher through the REAL path: a player's PC sends a container update -> ZDO.Deserialize (our prefix + postfix)
        byte[] Inv(params object[] flat)   // prefab, stack, maker, prefab, stack, maker, ...
        {
            var p = new ZPackage(); p.Write(109); p.Write((ushort)(flat.Length / 3));
            for (int k = 0; k < flat.Length; k += 3)
            {
                var prefab = (string)flat[k]; var stack = (int)flat[k + 1]; var maker = (string)flat[k + 2];
                p.Write(10000); p.Write((byte)(k / 3)); p.Write((byte)0); p.Write((byte)0);
                p.Write((byte)(0x40 | (stack > 1 ? 8 : 0) | (maker != "" ? 0x20 : 0)));
                if (stack > 1) p.Write((ushort)stack);
                if (maker != "") { p.Write(1L); p.Write(maker); }
                p.Write(prefab.GetStableHashCode()); p.Write((byte)0);
            }
            return p.GetArray();
        }
        ZDO Make(string prefab, Vector3 at, long creator) { var z = ZDOMan.instance.CreateNewZDO(at, prefab.GetStableHashCode()); z.SetPrefab(prefab.GetStableHashCode()); if (creator != 0) z.Set(ZDOVars.s_creator, creator); return z; }
        // what a client would send: a copy of the server's object with the new item list, serialized by the game itself
        void ClientSends(ZDO server, byte[] items)
        {
            var copy = ZDOMan.instance.CreateNewZDO(server.GetPosition() + new Vector3(0, 500, 0), server.GetPrefab());
            copy.SetPrefab(server.GetPrefab());
            var creator = server.GetLong(ZDOVars.s_creator, 0L); if (creator != 0) copy.Set(ZDOVars.s_creator, creator);
            copy.Set(ZDOVars.s_items, items);
            var pkg = new ZPackage(); copy.Serialize(pkg); pkg.SetPos(0);
            server.Deserialize(pkg);
            ZDOMan.instance.DestroyZDO(copy);
        }
        string ChestText() { var f = Directory.GetFiles(root, "chests-*.jsonl").FirstOrDefault(); if (f == null) return "";
            using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var r = new StreamReader(fs)) return r.ReadToEnd(); }
        string NewLines(int from) => string.Join("\n", ChestText().Split('\n').Where(l => l.Length > 0).Skip(from));

        // C1 restart: the chest exists in the save (set directly, as ZDO.Load does) and the first update after the start is counted
        var chest = Make("piece_chest_wood", new Vector3(20, 30, 20), 1001L);
        chest.Set(ZDOVars.s_items, Inv("FishWraps", 10, "Rowan", "IronScrap", 30, ""));
        int c0 = Lines("chests-");
        ClientSends(chest, Inv("FishWraps", 7, "Rowan", "IronScrap", 30, "", "IronScrap", 20, ""));
        var t1 = NewLines(c0);
        Check(Lines("chests-") == c0 + 2 && t1.Contains("\"item\":\"FishWraps\"") && t1.Contains("\"maker\":\"Rowan\"") && t1.Contains("\"action\":\"take\"") && t1.Contains("\"count\":3")
              && t1.Contains("\"action\":\"put\"") && t1.Contains("\"count\":20") && t1.Contains("piece_chest_wood") && t1.Contains("\"kind\":\"chest\""),
              "C1 first change after a (re)start is counted through ZDO.Deserialize: 3 fish wraps taken, 20 iron put in");
        // C2 the same update again counts nothing
        int c1 = Lines("chests-");
        ClientSends(chest, Inv("FishWraps", 7, "Rowan", "IronScrap", 30, "", "IronScrap", 20, ""));
        Check(Lines("chests-") == c1, "C2 a repeated update counts nothing twice");
        // C8 shared chest: a MultiUserChest request from another player passes the server, then the manager's update arrives
        var req = new ZPackage(); req.Write(7); req.Write(new Vector2i(-1, -1));
        req.Write(true); req.Write("Wood"); req.Write(15); req.Write(100f); req.Write(new Vector2i(0, 0)); req.Write(1); req.Write(0); req.Write(0L); req.Write(""); req.Write(0); req.Write(false); req.Write(false); req.Write(0);
        req.Write(true);
        Send(Routed("MUC_RequestItemAdd", 999L, new object[] { chest.m_uid, req }));
        int c2 = Lines("chests-");
        ClientSends(chest, Inv("FishWraps", 7, "Rowan", "IronScrap", 30, "", "IronScrap", 20, "", "Wood", 15, ""));
        var t2 = NewLines(c2);
        Check(Lines("chests-") == c2 + 1 && t2.Contains("\"via\":\"shared-chest\"") && t2.Contains("\"item\":\"Wood\"") && t2.Contains("\"count\":15"),
              "C8 shared chest: the friend's request names who put 15 wood in (" + t2 + ")");
        // C5 world chest (no creator) filling with loot on first open: loot-spawned
        var world = Make("TreasureChest_meadows", new Vector3(40, 30, 40), 0L);
        int c3 = Lines("chests-");
        ClientSends(world, Inv("Coins", 25, ""));
        Check(NewLines(c3).Contains("\"action\":\"loot-spawned\"") && !NewLines(c3).Contains("\"action\":\"put\""), "C5 dungeon loot on first open is loot-spawned, not a put");
        // C6 tombstone: death drop and pickup are grave events
        var grave = Make("Player_tombstone", new Vector3(50, 30, 50), 0L);
        int c4 = Lines("chests-");
        ClientSends(grave, Inv("IronScrap", 30, "", "SwordIron", 1, "Edda"));
        ClientSends(grave, Inv("SwordIron", 1, "Edda"));
        var t4 = NewLines(c4);
        Check(t4.Contains("\"kind\":\"grave\"") && t4.Contains("\"action\":\"grave-drop\"") && t4.Contains("\"action\":\"grave-take\"") && !t4.Contains("\"action\":\"put\""),
              "C6 tombstone: death drops and pickups are grave events, never chest gifts");

        // C9 feeder: SmelterUpgrades' Stoker's chest emptied by its mod while nobody has it open: auto-feed, unattended
        var feederPrefab = ZNetScene.instance.GetPrefab("smx_feeder_chest");
        if (feederPrefab != null)
        {
            var feeder = Make("smx_feeder_chest", new Vector3(60, 30, 60), 1001L);
            feeder.Set(ZDOVars.s_items, Inv("Coal", 50, ""));
            int c5 = Lines("chests-");
            ClientSends(feeder, Inv("Coal", 49, ""));
            var t5 = NewLines(c5);
            Check(t5.Contains("\"kind\":\"feeder\"") && t5.Contains("\"action\":\"auto-feed\"") && t5.Contains("\"via\":\"unattended\""), "C9 Stoker's chest feeding itself is auto-feed, unattended (" + t5 + ")");
            ZDOMan.instance.DestroyZDO(feeder);
        }
        else Check(false, "C9 smx_feeder_chest prefab not found (SmelterUpgrades missing on the test server?)");
        // C10 cargo crate: salvage, never stocking
        var crate = Make("CargoCrate", new Vector3(70, 30, 70), 0L);
        int c6 = Lines("chests-");
        ClientSends(crate, Inv("IronScrap", 60, ""));
        ClientSends(crate, Inv("IronScrap", 10, ""));
        var t6 = NewLines(c6);
        Check(t6.Contains("\"kind\":\"crate\"") && t6.Contains("\"action\":\"salvage-drop\"") && t6.Contains("\"action\":\"salvage-take\"") && !t6.Contains("\"action\":\"take\""), "C10 cargo crate: salvage-drop and salvage-take");
        ZDOMan.instance.DestroyZDO(crate);
        // C12 markers: the server start is in the log; a save adds world-saved
        Check(ChestText().Contains("\"marker\":\"server-start\""), "C12 server-start marker written");
        AccessTools.Method(typeof(ChestWatch), "Marker").Invoke(null, new object[] { "world-saved" });
        Check(ChestText().Contains("\"marker\":\"world-saved\""), "C12 world-saved marker written");
        // panel: inert on a dedicated server (no UI object created)
        Check(GameObject.Find("Hearthwoven") == null, "panel creates nothing on a dedicated server");

        // 6. overhead in the server runtime: our prefix + postfix per ZDO update (direct calls, no Deserialize work)
        var pre = AccessTools.Method(AccessTools.Inner(typeof(ChestWatch), "Watch"), "Prefix");
        var post = AccessTools.Method(AccessTools.Inner(typeof(ChestWatch), "Watch"), "Postfix");
        var plain = ZDOMan.instance.CreateNewZDO(new Vector3(30, 30, 30), "Greyling".GetStableHashCode());
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 20000; i++) { var a = new object[] { plain, null }; pre.Invoke(null, a); post.Invoke(null, new object[] { plain, a[1] }); }
        double plainUs = sw.Elapsed.TotalMilliseconds * 1000 / 20000; sw.Restart();
        for (int i = 0; i < 5000; i++) { var a = new object[] { chest, null }; pre.Invoke(null, a); post.Invoke(null, new object[] { chest, a[1] }); }
        double chestUs = sw.Elapsed.TotalMilliseconds * 1000 / 5000;
        Logger.LogInfo($"KS-SELFTEST overhead: {plainUs:0.0} us per ordinary object update, {chestUs:0.0} us per unchanged chest update");
        Check(plainUs < 20 && chestUs < 200, "chest watcher overhead small");
        foreach (var z in new[] { chest, world, grave, plain }) ZDOMan.instance.DestroyZDO(z);
    }
}
