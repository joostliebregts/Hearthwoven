using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Hearthwoven;
using Hearthwoven.Panel;
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
            typeof(ZDOMan), typeof(Character), typeof(Player), typeof(Humanoid), typeof(Humanoid), typeof(TreeBase), typeof(ZDOMan) };
        var methods = new[] { "Damage", "ApplyDamage", "BlockAttack", "Damage", "Damage", "Damage", "OnBought", "OnAddOre", "OnAddFuel", "EatFood",
            "RPC_EatConfirmation", "RaiseSkill", "RPC_RoutedRPC", "Deserialize", "RPC_ZDOData", "RPC_Damage", "OnDeath", "EquipItem", "Pickup", "Damage", "HandleDestroyedZDO" };
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

        // 4. routed damage (players without the mod): only when the server switches it on
        var hit = new HitData { m_skill = Skills.SkillType.Bows, m_hitType = HitData.HitType.PlayerHit }; hit.m_damage.m_pierce = 55f;
        int Lines(string prefix) { var f = Directory.GetFiles(root, prefix + "*.jsonl").FirstOrDefault(); if (f == null) return 0;
            using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var r = new StreamReader(fs)) return r.ReadToEnd().Split('\n').Count(l => l.Length > 0); }
        // Server.LogRoutedDamage is off by default (Joost, 2026-10-09): nothing is logged then; switched on, the fallback works as before
        var routedOn = (BepInEx.Configuration.ConfigEntry<bool>)AccessTools.Field(typeof(Plugin), "logRouted").GetValue(null);
        bool routedWas = routedOn.Value;
        void FlushRouted() => AccessTools.Field(typeof(Plugin), "routedWriter").GetValue(null)?.GetType().GetMethod("Flush").Invoke(AccessTools.Field(typeof(Plugin), "routedWriter").GetValue(null), null);
        routedOn.Value = false;
        int dOff = Lines("damage-routed-");
        Send(Routed("RPC_Damage", 987654321L, new object[] { hit }));
        FlushRouted();
        Check(Lines("damage-routed-") == dOff, "routed damage not logged while Server.LogRoutedDamage is off (the default)");
        routedOn.Value = true;
        int d0 = Lines("damage-routed-");
        Send(Routed("RPC_Damage", 987654321L, new object[] { hit }));
        FlushRouted();
        Check(Lines("damage-routed-") == d0 + 1, "routed damage logged as fallback when Server.LogRoutedDamage is on");
        routedOn.Value = routedWas;

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
        // 0.6 the server book: loaded at start, saved with the world, ships go in through the real path, births are seen once
        var hooks = AccessTools.TypeByName("Hearthwoven.ServerBookHooks");
        var book = AccessTools.Field(hooks, "Book")?.GetValue(null) as ServerBook;
        Check(book != null, "0.6 server book: started with the server");
        if (book != null)
        {
            var ship = Make("VikingShip", new Vector3(100, 30, 100), 1001L);
            int c7 = Lines("chests-");
            ClientSends(ship, Inv("IronScrap", 30, ""));
            int lotsAfterPut = book.LotsIn(ship.m_uid.ToString());
            ship.SetPosition(new Vector3(1100, 30, 100));
            ClientSends(ship, Inv());
            var t7 = NewLines(c7);
            Check(t7.Contains("\"kind\":\"ship\"") && lotsAfterPut == 1 && book.LotsIn(ship.m_uid.ToString()) == 0,
                  "0.6 cargo: a ship's put and take through ZDO.Deserialize go into the server book (one lot in, used up by the take) (" + lotsAfterPut + ")");
            ZDOMan.instance.DestroyZDO(ship);
            AccessTools.Method(hooks, "Save").Invoke(null, null);
            var bookFile = Directory.GetFiles(root, "server-book-*.json").FirstOrDefault();
            Check(bookFile != null && ServerBook.FromJson(File.ReadAllText(bookFile)) != null, "0.6 server book: saved with the world and reads back (" + (bookFile == null ? "no file" : Path.GetFileName(bookFile)) + ")");
            // a tamed young animal the server sees for the first time (its prefab is still 0 before the data arrives)
            ZDO Born(string prefab, bool tamed, Vector3 at)
            {
                var fresh = ZDOMan.instance.CreateNewZDO(at, 0);
                var copy = ZDOMan.instance.CreateNewZDO(at + new Vector3(0, 500, 0), prefab.GetStableHashCode()); copy.SetPrefab(prefab.GetStableHashCode());
                if (tamed) copy.Set(ZDOVars.s_tamed, true);
                var pkg = new ZPackage(); copy.Serialize(pkg); pkg.SetPos(0);
                fresh.Deserialize(pkg); ZDOMan.instance.DestroyZDO(copy);
                return fresh;
            }
            int b0 = Lines("births-");
            var piglet = Born("Boar_piggy", true, new Vector3(110, 30, 110));
            Check(Lines("births-") == b0 + 1, "0.6 born near: a tamed piglet appearing for the first time is one birth (births log line)");
            var wild = Born("Boar_piggy", false, new Vector3(112, 30, 110));
            var adult = Born("Boar", true, new Vector3(114, 30, 110));
            var again = ZDOMan.instance.CreateNewZDO(piglet.GetPosition() + new Vector3(0, 500, 0), piglet.GetPrefab()); again.SetPrefab(piglet.GetPrefab()); again.Set(ZDOVars.s_tamed, true);
            var pk = new ZPackage(); again.Serialize(pk); pk.SetPos(0); piglet.Deserialize(pk); ZDOMan.instance.DestroyZDO(again);
            Check(Lines("births-") == b0 + 1, "0.6 born near: a wild young one, a tamed adult and a later update of the same piglet are no births");
            foreach (var z in new[] { piglet, wild, adult }) ZDOMan.instance.DestroyZDO(z);
            var bpre = AccessTools.Method(AccessTools.Inner(hooks, "BornWatch"), "Prefix");
            var bpost = AccessTools.Method(AccessTools.Inner(hooks, "BornWatch"), "Postfix");
            var probe = ZDOMan.instance.CreateNewZDO(new Vector3(40, 30, 40), "Greyling".GetStableHashCode());
            var bsw = Stopwatch.StartNew();
            for (int i = 0; i < 20000; i++) { var a = new object[] { probe, false }; bpre.Invoke(null, a); bpost.Invoke(null, new object[] { probe, a[1] }); }
            double bornUs = bsw.Elapsed.TotalMilliseconds * 1000 / 20000;
            Logger.LogInfo($"KS-SELFTEST overhead: {bornUs:0.0} us per object update for the birth watcher (reflection calls included)");
            Check(bornUs < 20, "0.6 born near: watcher overhead small");
            ZDOMan.instance.DestroyZDO(probe);
        }
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

        // 7. panel vocabulary: every sprite decodes, and the four new block kinds draw without exceptions off screen (no
        // canvas, no player, no clicks: PanelProbe), leaving nothing behind and still no panel on the server
        var unloaded = PanelProbe.MissingSprites();
        Check(unloaded.Count == 0, "vocabulary sprites all load from the manifest" + (unloaded.Count > 0 ? " (missing: " + string.Join(", ", unloaded) + ")" : ""));
        Block Part(string title, string value, float share, string colour, string pattern) => new Block { Title = title, Value = value, Fraction = share, Colour = colour, Pattern = pattern, Src = "character" };
        Block Skill(string id, float level, float progress, bool practised) => new Block { Kind = "ladder", Id = id, Icon = "skill:" + id, Title = id, Value = ((int)level).ToString(), Level = level, Progress = progress, Practised = practised, Src = "character" };
        var blocks = new List<Block>
        {
            new Block { Kind = "composition", Title = "Wood brought in", Value = "1,775", Src = "character", Items = new List<Block> {
                Part("Wood", "1,240", 0.70f, "#9a6d42", "vocab:grain-wood"), Part("Fine wood", "310", 0.17f, "#d6c79a", "vocab:grain-finewood"),
                Part("Core wood", "180", 0.10f, "#6e4630", "vocab:grain-corewood"), Part("Ancient bark", "45", 0.03f, "#4d4438", "vocab:grain-ancientbark") } },
            new Block { Kind = "composition", Title = "What hurt you", Value = "514", Src = "pc", Items = new List<Block> {
                new Block { Icon = "damage:blunt", Title = "Blunt", Value = "171", Fraction = 0.33f, Colour = "#6b7076", Src = "pc" },
                new Block { Icon = "damage:poison", Title = "Poison", Value = "170", Fraction = 0.33f, Colour = "#5fbf3a", Src = "pc" },
                new Block { Icon = "damage:pierce", Title = "Pierce", Value = "1", Fraction = 0.002f, Colour = "#e8dcbc", Src = "pc" } } },
            new Block { Kind = "biomes", Src = "pc", Items = new List<Block> {
                new Block { Kind = "biome", Id = "Meadows", Title = "Meadows", Icon = "vocab:biome-meadows", Colour = "#7fa04a", Tone = "dark-text", Value = "410", Fraction = 0.33f, Value2 = "14", Fraction2 = 0.05f, Src = "pc",
                    Items = new List<Block> { new Block { Kind = "boss", Icon = "item:TrophyEikthyr", Title = "Eikthyr", Src = "character" } } },
                new Block { Kind = "biome", Id = "BlackForest", Title = "Black Forest", Icon = "vocab:biome-blackforest", Colour = "#3f6b4a", Tone = "light-text", Value = "1,240", Fraction = 1f, Value2 = "210", Fraction2 = 0.5f, Count = 1, Src = "pc", Selected = true },
                new Block { Kind = "biome", Id = "Ocean", Title = "Ocean", Icon = "vocab:biome-ocean", Colour = "#3d6f96", Tone = "light-text", Src = "pc" } } },
            new Block { Kind = "ladders", Items = new List<Block> {
                new Block { Kind = "group", Title = "Fight", Items = new List<Block> { Skill("Axes", 22, 0.55f, true), Skill("Blocking", 18, -1f, false) } },
                new Block { Kind = "group", Title = "Move", Items = new List<Block> { Skill("Run", 41, 0.15f, true) } } } },
            new Block { Kind = "ladder", Id = "WoodCutting", Icon = "skill:WoodCutting", Title = "Wood cutting", Value = "34", Value2 = "35", Level = 34, Progress = 0.62f, Practised = true, Src = "character",
                Items = new List<Block> { new Block { Kind = "practice", Value = "6.8", Title = "practised", Src = "pc", SinceInstall = true }, new Block { Kind = "link", Id = "Deeds/woodcutting", Title = "Woodcutting" } } },
        };
        // the page layout kinds (slice 3): a plate holding a hero, columns, a view switch and cards
        Block Num(string kind, string value, string title, string src, bool since = false) => new Block { Kind = kind, Value = value, Title = title, Src = src, SinceInstall = since };
        var hero = Num("hero", "410", "trees felled", "character"); hero.Items = new List<Block> { Num("number", "64", "axe hits", "pc", true) };
        blocks.Add(new Block { Kind = "plate", Title = "Woodcutting", Icon = "title:woodcutter", Pill = "Woodcutter", PillIcon = "title:woodcutter", Text = "Edda, last shared", Items = new List<Block> {
            hero, blocks[0],
            new Block { Kind = "columns", Items = new List<Block> {
                new Block { Kind = "column", Items = new List<Block> { blocks[1] } },
                new Block { Kind = "column", Items = new List<Block> { new Block { Kind = "section", Title = "Axe hits per tree" } } } } },
            new Block { Kind = "switch", Id = "Skills/overview/view", Items = new List<Block> {
                new Block { Kind = "view", Id = "levels", Title = "Levels", Selected = true, Items = new List<Block> { blocks[3] } },
                new Block { Kind = "view", Id = "practised", Title = "Practised" } } },
            new Block { Kind = "cards", Items = new List<Block> {
                new Block { Kind = "card", Id = "Deeds/woodcutting", Icon = "title:woodcutter", Title = "Woodcutter", Value = "103", Text = "trees felled", Src = "character",
                    Items = new List<Block> { Num("number", "462", "axe hits", "pc", true) } },
                new Block { Kind = "card", Id = "Deeds/mining", Icon = "title:miner", Title = "Stonebreaker", Value = "1,180", Text = "pickaxe hits", Src = "pc", SinceInstall = true } } } } });
        int placed = -1; string error = null;
        try { placed = PanelProbe.Draw(blocks); } catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
        Check(error == null && placed > 100, "panel vocabulary draws composition, biomes, ladders, ladder and the page layout (plate, hero, columns, switch, cards) without exceptions (" + (error ?? placed + " objects") + ")");
        // ch-battle: the Battle kinds (damage grid, weapon bars, foe table and chips, guard, sources, where you fell, deaths)
        // built by the model from a small log, then drawn off screen the same way
        var bLog = new Hearthwoven.EventLog(); var bNow = DateTime.UtcNow;
        HitData.DamageTypes Dt(float slash, float fire, float poison) { var d = new HitData.DamageTypes(); d.m_slash = slash; d.m_fire = fire; d.m_poison = poison; return d; }
        bLog.AddDamage(bNow.AddMinutes(-5), "Swamp", true, "Draugr", "Swords", Dt(120, 30, 0)); bLog.AddDamage(bNow.AddMinutes(-4), "Swamp", true, "Blob", "Bows", Dt(0, 40, 0));
        bLog.AddDamage(bNow.AddMinutes(-4), "Swamp", true, "Draugr", "ElementalMagic", Dt(0, 50, 0));
        bLog.AddDamage(bNow.AddSeconds(-6), "Swamp", false, "Draugr", "EnemyHit", Dt(60, 0, 25)); bLog.AddDeath(bNow, "Swamp", 1, 1);
        var bFoe = new PanelModel.FoeData { Trophy = "TrophyDraugr" }; bFoe.Modifiers["fire"] = "Resistant"; bFoe.Modifiers["poison"] = "Immune"; bFoe.Modifiers["slash"] = "Weak";
        var bInput = new PanelInput { NowUtc = bNow, Log = bLog, Foe = f => f == "Draugr" ? bFoe : null };
        var bRows = PanelModel.Damage(bLog, TimeWindow.Session, "", bNow); var bDeaths = PanelModel.Deaths(bLog, TimeWindow.Session, "", bNow);
        var battle = new List<Block> { PanelModel.DamageGrid(bRows), PanelModel.FoeTable(bInput, bRows), PanelModel.FoeTypes(bInput, bRows), PanelModel.ReceivedSources(bInput, bRows),
            PanelModel.DeathStrip(bInput, bDeaths), PanelModel.DeathList(bInput, bDeaths),
            new Block { Kind = "guard", Value = "312", Title = "blocks", Value2 = "58", Text = "parries", Fraction = 58f / 312f, Src = "pc" } };
        battle.AddRange(PanelModel.DamageMixes(bRows));
        int bPlaced = 0; string bError = battle.Any(b => b == null) ? "a Battle builder returned nothing" : null;
        if (bError == null) try { bPlaced = PanelProbe.Draw(battle); } catch (Exception e) { bError = e.GetType().Name + ": " + e.Message; }
        Check(bError == null && bPlaced > 100, "panel Battle kinds draw without exceptions (" + (bError ?? bPlaced + " objects") + ")");
        // Feats (Chapters/FeatsUi.cs): the sample's Feats page (grid, detail area), Known for and an owner-page band, built by the model, drawn off screen
        var fSample = PanelSample.Full(DateTime.UtcNow);
        var fPage = PanelModel.Build(fSample, new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = "earned" } });
        var fBlocks = new List<Block> { PanelModel.Content(fPage).FirstOrDefault(b => b.Kind == "feats"), PanelModel.Content(fPage).FirstOrDefault(b => b.Kind == "featdetail"),
            PanelModel.FeatsKnownFor(fSample), PanelModel.FeatBand(fSample, Chapter.Battle, "defense") };
        int fPlaced = 0; string fError = fBlocks.Any(b => b == null) ? "a Feats builder returned nothing" : null;
        if (fError == null) try { fPlaced = PanelProbe.Draw(fBlocks); } catch (Exception e) { fError = e.GetType().Name + ": " + e.Message; }
        Check(fError == null && fPlaced > 100, "panel Feats kinds (cards, detail area, Known for, band) draw without exceptions (" + (fError ?? fPlaced + " objects") + ")");
        var asked = PanelProbe.PressSwitch(new Block { Kind = "switch", Id = "Skills/overview/view", Items = new List<Block> {
            new Block { Kind = "view", Id = "levels", Title = "Levels", Selected = true, Items = new List<Block> { blocks[3] } },
            new Block { Kind = "view", Id = "practised", Title = "Practised" } } });
        Check(asked == PanelModel.ViewTarget + "Skills/overview/view=practised", "panel view switch: pressing the Practised chip asks for that view (" + (asked ?? "no chip reacted") + ")");
        Check(GameObject.Find("HearthwovenProbe") == null && GameObject.Find("Hearthwoven") == null, "vocabulary probe leaves nothing behind; still no panel on a dedicated server");
    }
}
