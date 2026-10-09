// 0.6 server-side tests: the server's book (src/ServerBook.cs: cargo loaded and unloaded, born near; persistence; the rebuild
// from the chest logs with C12) and the group answer (src/GroupServe.cs: each copy packed once, only changed copies sent),
// with a measurement of the group answer before and after. Fictional players and ids; the real chest log is read only when
// it is on this machine (HW_REAL_CHESTS or ~/ValheimReport/hearthwoven), and only totals are printed, never names.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Hearthwoven;

static class ServerBookTests
{
    static readonly DateTime T0 = new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc);

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        double Sent(ServerBook b, string who, string item) => b.Of(who) != null && b.Of(who).Sent.TryGetValue(item, out var v) ? v : 0;
        double Deliv(ServerBook b, string who, string item) => b.Of(who) != null && b.Of(who).Delivered.TryGetValue(item, out var v) ? v : 0;

        // ---------- cargo ----------
        var b1 = new ServerBook();
        b1.Cargo("ship1", "ship", "put", "sender", "111", "IronScrap", 30, 0, 0, T0);
        var leg = b1.Cargo("ship1", "ship", "take", "sender", "222", "IronScrap", 30, 1000, 0, T0.AddMinutes(20));
        Check(leg == 30000 && Sent(b1, "111", "IronScrap") == 30000 && Deliv(b1, "222", "IronScrap") == 30000 && Deliv(b1, "111", "IronScrap") == 0 && Sent(b1, "222", "IronScrap") == 0,
              "SB cargo: 30 scrap loaded by one player, unloaded 1 km away by another: 30 000 item-metres sent to the loader, delivered to the unloader, never summed");
        Check(b1.LotsIn("ship1") == 0 && b1.CargoFrom == T0, "SB cargo: a lot taken out whole is gone; the book begins at its first ship or cart event");

        var b2 = new ServerBook();
        b2.Cargo("ship1", "ship", "put", "sender", "111", "IronScrap", 10, 0, 0, T0);
        b2.Cargo("ship1", "ship", "put", "shared-chest", "333", "IronScrap", 10, 0, 500, T0);
        b2.Cargo("ship1", "ship", "take", "sender", "222", "IronScrap", 15, 0, 1000, T0);
        Check(Sent(b2, "111", "IronScrap") == 10000 && Sent(b2, "333", "IronScrap") == 2500 && Deliv(b2, "222", "IronScrap") == 12500 && b2.LotsIn("ship1") == 1,
              "SB cargo: first in, first out: a take of 15 uses the oldest lot (10 x 1 000 m) and 5 of the next (5 x 500 m); 5 stay aboard");

        var b3 = new ServerBook();
        foreach (var k in new[] { "chest", "grave", "crate", "feeder" })
        {
            b3.Cargo("c-" + k, k, "put", "sender", "111", "Wood", 50, 0, 0, T0);
            b3.Cargo("c-" + k, k, "take", "sender", "222", "Wood", 50, 900, 0, T0);
        }
        b3.Cargo("ship9", "ship", "loot-spawned", "sender", "111", "Coins", 5, 0, 0, T0); b3.Cargo("ship9", "ship", "take", "sender", "222", "Coins", 5, 900, 0, T0);
        Check(b3.Players == 0 && b3.Carriers == 0, "SB cargo: chests, graves, crates and feeders never enter the book (storage, not transport), nor anything but put and take");

        var b4 = new ServerBook();
        b4.Cargo("cart1", "cart", "put", "unattended", "111", "Wood", 36, 0, 0, T0);
        b4.Cargo("cart1", "cart", "take", "sender", "222", "Wood", 36, 100, 0, T0);
        b4.Cargo("cart1", "cart", "put", "sender", "111", "Stone", 20, 0, 0, T0);
        b4.Cargo("cart1", "cart", "take", "unattended", "222", "Stone", 20, 0, 50, T0);
        Check(Sent(b4, "111", "Wood") == 0 && Deliv(b4, "222", "Wood") == 3600 && Sent(b4, "111", "Stone") == 1000 && Deliv(b4, "222", "Stone") == 0,
              "SB cargo: a put or take with nobody at the cart (unattended) credits nobody for that side; the other side keeps its credit");

        var b5 = new ServerBook();
        Check(b5.Cargo("ship2", "ship", "take", "sender", "222", "IronScrap", 30, 500, 0, T0) == 0 && b5.Players == 0 && b5.Carriers == 0,
              "SB cargo: a take with nothing put in before (loaded before the book began) books nothing: a floor, never an overcount");
        b5.Cargo("ship2", "ship", "put", "sender", "111", "IronScrap", 30, 0, 0, T0);
        Check(b5.Cargo("ship2", "ship", "take", "sender", "222", "Wood", 30, 500, 0, T0) == 0 && b5.LotsIn("ship2") == 1, "SB cargo: a take of another item leaves the iron's lot alone");
        b5.Cargo("ship2", "ship", "take", "sender", "222", "IronScrap", 50, 500, 0, T0);
        Check(Deliv(b5, "222", "IronScrap") == 15000 && b5.LotsIn("ship2") == 0, "SB cargo: a take larger than what was put in books only what was put in (30 of 50)");
        b5.Cargo("ship2", "ship", "put", "sender", "444", "Copper", 10, 0, 0, T0); b5.Cargo("ship2", "ship", "take", "sender", "444", "Copper", 10, 0, 2000, T0);
        Check(Sent(b5, "444", "Copper") == 20000 && Deliv(b5, "444", "Copper") == 20000, "SB cargo: loading and unloading yourself is both loaded and unloaded, two numbers, never one sum");
        b5.Cargo("ship2", "ship", "put", "sender", "", "Copper", 10, 0, 0, T0); b5.Cargo("ship2", "ship", "take", "sender", null, "Copper", 10, 0, 2000, T0);
        Check(b5.Players == 3, "SB cargo: a player without an id credits nobody");

        var b6 = new ServerBook();
        b6.Cargo("ship3", "ship", "put", "sender", "111", "IronScrap", 30, 10, 10, T0); b6.Cargo("ship3", "ship", "put", "sender", "111", "IronScrap", 12, 12, 11, T0);
        Check(b6.LotsIn("ship3") == 1, "SB cargo: two puts of one item by one player at one spot are one lot (a stack split in two)");
        b6.Cargo("ship3", "ship", "put", "sender", "111", "IronScrap", 5, 400, 0, T0);
        Check(b6.LotsIn("ship3") == 2, "SB cargo: a put somewhere else starts a new lot");

        // bounded
        var b7 = new ServerBook();
        for (int i = 0; i < 1000; i++) b7.Cargo("ship", "ship", "put", "sender", "111", "Wood", 1, i * 10, 0, T0);
        Check(b7.LotsIn("ship") == ServerBook.MaxLots, "SB bounded: at most " + ServerBook.MaxLots + " lots per ship or cart (the oldest goes first)");
        for (int i = 0; i < 400; i++) b7.Cargo("cart" + i, "cart", "put", "sender", "111", "Wood", 1, 0, 0, T0);
        Check(b7.Carriers == ServerBook.MaxCarriers && b7.LotsIn("cart399") == 1 && b7.LotsIn("cart0") == 0, "SB bounded: at most " + ServerBook.MaxCarriers + " ships and carts (the one untouched longest goes first)");
        for (int i = 0; i < 300; i++) { b7.Cargo("k", "ship", "put", "sender", "111", "Item" + i, 1, 0, 0, T0); b7.Cargo("k", "ship", "take", "sender", "222", "Item" + i, 1, 100, 0, T0); }
        Check(b7.Of("111").Sent.Count == ServerBook.MaxKinds && b7.Of("222").Delivered.Count == ServerBook.MaxKinds, "SB bounded: at most " + ServerBook.MaxKinds + " items per player and table");
        for (int i = 0; i < 700; i++) b7.Birth("Boar_piggy", new[] { "p" + i });
        Check(b7.Players == ServerBook.MaxPlayers, "SB bounded: at most " + ServerBook.MaxPlayers + " players");

        // ---------- born near ----------
        var players = new List<KeyValuePair<string, float[]>> {
            new KeyValuePair<string, float[]>("111", new[] { 0f, 30f, 0f }), new KeyValuePair<string, float[]>("222", new[] { 39f, 30f, 0f }),
            new KeyValuePair<string, float[]>("333", new[] { 41f, 30f, 0f }), new KeyValuePair<string, float[]>("444", new[] { 0f, 75f, 0f }),
            new KeyValuePair<string, float[]>("", new[] { 0f, 30f, 0f }) };
        var near = ServerBook.Near(players, 0, 30, 0);
        Check(near.SequenceEqual(new[] { "111", "222" }), "SB born: near = within 40 m in three dimensions (39 m yes, 41 m and 45 m above no); a player without an id is never near");
        var b8 = new ServerBook();
        b8.Birth("Boar_piggy", near); b8.Birth("Boar_piggy", new[] { "111", "111" }); b8.Birth("", near); b8.Birth("Wolf_cub", new string[0]);
        Check(b8.Of("111").Born["Boar_piggy"] == 2 && b8.Of("222").Born["Boar_piggy"] == 1 && b8.Of("333") == null && b8.Players == 2,
              "SB born: one birth for each player near it, once each; nobody near or no creature: nothing");

        // ---------- what a player's book shares ----------
        var shared = b2.SharedJson("111", item => item == "IronScrap" ? "$item_ironscrap" : item);
        var back = ServerBook.Shared.Read(MiniJson.Parse(shared) as Dictionary<string, object>);
        Check(shared.Contains("\"cargoSent\":{\"$item_ironscrap\":10000}") && back != null && back.Sent["$item_ironscrap"] == 10000 && back.Delivered.Count == 0 && back.CargoFrom == T0,
              "SB shared: a player's part names items by the game's token and says when the book began; read back the same");
        Check(b2.SharedJson("nobody") == null && b8.SharedJson("111").Contains("\"bornNear\":{\"Boar_piggy\":2}"), "SB shared: nothing for a player the book has nothing for (no ghosts); births per creature");
        var many = new ServerBook(); for (int i = 0; i < 60; i++) { many.Cargo("s", "ship", "put", "sender", "1", "I" + i, i + 1, 0, 0, T0); many.Cargo("s", "ship", "take", "sender", "2", "I" + i, i + 1, 10, 0, T0); }
        var manyBack = ServerBook.Shared.Read(MiniJson.Parse(many.SharedJson("1")) as Dictionary<string, object>);
        Check(manyBack.Sent.Count == ServerBook.SharedKinds && manyBack.Sent.ContainsKey("I59") && !manyBack.Sent.ContainsKey("I0"), "SB shared: only the " + ServerBook.SharedKinds + " largest items of each table travel");
        Check(ServerBook.Shared.Read(null) == null && ServerBook.Shared.Read(new Dictionary<string, object>()) == null && ServerBook.Shared.Read(MiniJson.Parse("{\"cargoSent\":{\"x\":\"bad\"}}") as Dictionary<string, object>) == null,
              "SB shared: a missing or broken part reads as none");

        // ---------- restart-safe ----------
        var b9 = new ServerBook { BornFrom = T0 };
        b9.Cargo("ship1", "ship", "put", "sender", "111", "IronScrap", 30, 0, 0, T0);
        b9.Cargo("ship1", "ship", "put", "unattended", null, "Wood", 10, 5, 5, T0);
        b9.Birth("Boar_piggy", new[] { "111" });
        var saved = b9.ToJson();
        var b10 = ServerBook.FromJson(saved);
        Check(b10 != null && b10.ToJson() == saved && b10.LotsIn("ship1") == 2 && b10.BornFrom == T0 && b10.CargoFrom == T0, "SB restart: the saved book reads back exactly (lots, totals, dates)");
        b10.Cargo("ship1", "ship", "take", "sender", "222", "IronScrap", 30, 0, 2000, T0);
        Check(Sent(b10, "111", "IronScrap") == 60000 && Deliv(b10, "222", "IronScrap") == 60000, "SB restart: cargo loaded before a restart is credited when it comes out after it");
        Check(ServerBook.FromJson(saved.Replace("\"version\":1", "\"version\":99")) == null && ServerBook.FromJson("not json") == null && ServerBook.FromJson("") == null,
              "SB restart: a newer format or a broken file is not read (the server sets it aside, never overwrites it)");

        // ---------- the rebuild from the chest logs (C12) ----------
        string L(string t, string action, string who, int count, double x, string via = "sender") =>
            "{\"t\":\"" + t + "\",\"player\":\"P\",\"playerId\":" + (who == null ? "0" : who) + ",\"playerKey\":\"" + (who ?? "Steam_1") + "\",\"via\":\"" + via +
            "\",\"container\":\"VikingShip\",\"kind\":\"ship\",\"containerId\":\"9:1\",\"x\":" + x + ",\"z\":0,\"item\":\"IronScrap\",\"maker\":\"\",\"quality\":1,\"action\":\"" + action + "\",\"count\":" + count + "}";
        string M(string t, string m) => "{\"t\":\"" + t + "\",\"marker\":\"" + m + "\"}";
        var log = new List<string> {
            M("2026-10-08T13:00:00Z", "server-start"),
            L("2026-10-08T13:10:00Z", "put", "111", 30, 0),
            "{\"t\":\"2026-10-08T13:11:00Z\",\"player\":\"P\",\"kind\":\"chest\",\"containerId\":\"1:1\",\"action\":\"put\",\"item\":\"Wood\",\"count\":5}",
            M("2026-10-08T13:30:00Z", "world-saved"),
            L("2026-10-08T13:40:00Z", "take", "222", 30, 1000),   // rolled back: a restart follows without a save
            M("2026-10-08T13:50:00Z", "server-start"),
            L("2026-10-08T14:00:00Z", "take", "333", 30, 2000),
            "not json",
            M("2026-10-08T14:30:00Z", "world-saved"),
            L("2026-10-08T14:40:00Z", "put", null, 5, 0),           // after the last save: the world this server loads does not have it
        };
        var b11 = new ServerBook();
        var r = b11.Replay(log);
        Check(r.Applied == 2 && r.RolledBack == 2 && r.Unreadable == 1 && Deliv(b11, "222", "IronScrap") == 0 && Deliv(b11, "333", "IronScrap") == 60000 && Sent(b11, "111", "IronScrap") == 60000 && b11.LotsIn("9:1") == 0,
              "SB rebuild: the chest logs replayed with C12: a take a restart rolled back is not booked, the take after it is; lines after the last save are dropped; chests and broken lines ignored");

        // the real log of the 8 Oct iron haul, when it is on this machine (totals only, never names)
        var dir = Environment.GetEnvironmentVariable("HW_REAL_CHESTS") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ValheimReport", "hearthwoven");
        var files = Directory.Exists(dir) ? Directory.GetFiles(dir, "chests-*.jsonl").OrderBy(f => f, StringComparer.Ordinal).ToList() : new List<string>();
        if (files.Count > 0)
        {
            var real = new ServerBook();
            var clock = Stopwatch.StartNew();
            var rr = real.Replay(files.SelectMany(f => File.ReadLines(f)));
            clock.Stop();
            var json = real.ToJson();
            var sentIron = new[] { "IronScrap" }.Sum(i => RealSum(real, json, "sent", i)); var delivIron = RealSum(real, json, "delivered", "IronScrap");
            System.Console.WriteLine($"MEASURED real chest log ({files.Count} files, {rr.Lines} lines): {rr.Applied} ship and cart lines applied, {rr.RolledBack} rolled back, replay {clock.Elapsed.TotalMilliseconds:0} ms, book {json.Length} bytes; iron scrap sent {sentIron / 1000:0.0} item-km, delivered {delivIron / 1000:0.0} item-km");
            if (files.Any(f => f.EndsWith("chests-20261008.jsonl")))
                Check(Math.Abs(sentIron - 1951700) < 10000 && Math.Abs(delivIron - 1951800) < 10000 && real.Carriers <= ServerBook.MaxCarriers,
                      "SB real log: the 8 Oct iron haul replays to about 1 952 item-km loaded and the same delivered (FEASIBILITY-06 measured 1.95 million item-metres)");
            var reread = ServerBook.FromJson(json);
            Check(reread != null && reread.ToJson() == json, "SB real log: the rebuilt book saves and reads back exactly");
        }
        else System.Console.WriteLine("SKIP SB real log: no chests-*.jsonl in " + dir);

        // ---------- the list on the wire: old and new clients, old and new servers ----------
        var booksJson = "{\"self\":" + b2.SharedJson("111") + ",\"players\":{\"Edda\":" + b2.SharedJson("222") + "}}";
        var wire = GroupShare.WriteList(new[] { "Edda", "Tor" }, booksJson).GetArray();
        var oldReader = new ZPackage(wire); int count = oldReader.ReadInt(); var oldNames = new List<string>(); for (int i = 0; i < count; i++) oldNames.Add(oldReader.ReadString());
        Check(oldNames.SequenceEqual(new[] { "Edda", "Tor" }), "WIRE a client before 0.6 reads the names exactly as before; the book after them is never read (no protocol change)");
        var newNames = GroupShare.ReadList(new ZPackage(wire), out var gotBooks);
        Check(newNames.SetEquals(new[] { "Edda", "Tor" }) && gotBooks == booksJson, "WIRE a 0.6 client reads the names, then the server book behind its tag");
        var oldServer = new ZPackage(); oldServer.Write(1); oldServer.Write("Edda");
        Check(GroupShare.ReadList(new ZPackage(oldServer.GetArray()), out var noBooks).SetEquals(new[] { "Edda" }) && noBooks == null, "WIRE a server before 0.6 (names only): read as before, no book");
        GroupShare.ReadBooks(booksJson);
        Check(GroupShare.OwnBook != null && GroupShare.OwnBook.Sent["IronScrap"] == 10000 && GroupShare.BookOf("Edda")?.Delivered["IronScrap"] == 12500 && GroupShare.BookOf("Tor") == null,
              "WIRE the book read on the client: your own part and each fellow's by name; a fellow with nothing has none");
        GroupShare.ReadBooks(null);
        Check(GroupShare.OwnBook == null && GroupShare.Books.Count == 0, "WIRE a list without a book clears what an earlier one said (no stale numbers)");
        var full = new ServerBook();
        for (int pl = 0; pl < 9; pl++) for (int i = 0; i < 60; i++)
        { full.Cargo("s" + pl, "ship", "put", "sender", "p" + pl, "$item_longitemname_" + i, 30, 0, 0, T0); full.Cargo("s" + pl, "ship", "take", "sender", "p" + pl, "$item_longitemname_" + i, 30, 1234.5 + i, 0, T0); full.Birth("Creature_young_" + i, new[] { "p" + pl }); }
        var worst = "{\"self\":" + full.SharedJson("p0") + ",\"players\":{" + string.Join(",", Enumerable.Range(1, 8).Select(pl => "\"Fellow" + pl + "\":" + full.SharedJson("p" + pl))) + "}}";
        var worstSize = GroupShare.WriteList(Enumerable.Range(1, 8).Select(pl => "Fellow" + pl).ToList(), worst).Size();
        System.Console.WriteLine($"MEASURED list with the book, worst case (you + 8 fellows, {ServerBook.SharedKinds} items in each of three tables): {worst.Length / 1024.0:0.0} KB JSON, {worstSize / 1024.0:0.0} KB on the wire");
        Check(worstSize < 16384, "WIRE the list with the book stays one modest message even in the worst case (under 16 KB)");

        // ---------- the group answer: each copy packed once, only what changed ----------
        var tmp = Path.Combine(Path.GetTempPath(), "hw-groupserve-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tmp);
        try
        {
            var realShare = Path.Combine(dir, "players");
            var sample = Directory.Exists(realShare) ? Directory.GetFiles(realShare, "*.share.json").Select(File.ReadAllText).FirstOrDefault() : null;
            sample = sample ?? "{\"mod\":\"0.5.0\",\"playerId\":1,\"name\":\"X\",\"stats\":[" + string.Join(",", Enumerable.Range(0, 800).Select(i => "{\"k" + i + "\":" + (i * 7919 % 1000) + "}")) + "]}";
            string Copy(int i, int version) => sample.Replace("\"playerId\":", "\"v\":" + version + ",\"playerId\":").Replace("\"name\":\"", "\"name\":\"Fellow" + i);
            var paths = Enumerable.Range(0, 8).Select(i => Path.Combine(tmp, (1000 + i) + ".share.json")).ToList();
            var versions = new int[8];
            void Write(int i) { File.WriteAllText(paths[i], Copy(i, versions[i])); File.SetLastWriteTimeUtc(paths[i], T0.AddSeconds(versions[i] * 10 + i)); }
            for (int i = 0; i < 8; i++) Write(i);
            List<GroupServe.Source> Sources() => paths.Where(File.Exists).Select(p => { var fi = new FileInfo(p); return new GroupServe.Source { Path = p, Key = Path.GetFileName(p).Replace(".share.json", ""), Stamp = fi.LastWriteTimeUtc.Ticks + ":" + fi.Length }; }).ToList();
            var serve = new GroupServe();
            GroupServe.Answer Ask(long peer, float now) => serve.Plan(peer, Sources(), now, File.ReadAllText, t => Fragments.Split(Transport.Pack(t)), t => Transport.Field(t, "name"), t => "m" + t.Length);
            var a1 = Ask(7, 0);
            Check(a1.Send.Count == 8 && a1.Names.Count == 8 && serve.Packed == 8, "GS first answer: every fellow's copy, each read and packed once");
            var a2 = Ask(7, 30);
            Check(a2.Send.Count == 0 && a2.Names.Count == 8 && a2.Names.SequenceEqual(a1.Names) && serve.Packed == 8, "GS the next answer: nothing unchanged is sent again; the names still go out (leavers are forgotten as before)");
            versions[3]++; Write(3);
            var a3 = Ask(7, 60);
            Check(a3.Send.Count == 1 && a3.Send[0].Path == paths[3] && serve.Packed == 9, "GS a fellow's new copy: only that one is packed and sent");
            var a4 = Ask(8, 61);
            Check(a4.Send.Count == 8 && serve.Packed == 9, "GS another player's first answer: every copy, from the packed copies (nothing read or packed again)");
            serve.Forget(7);
            Check(Ask(7, 90).Send.Count == 8, "GS a player who switched sharing off and on again gets every copy again");
            Check(Ask(7, 120).Send.Count == 0 && Ask(7, 120 + GroupServe.FullEvery).Send.Count == 8, "GS every copy again once every " + GroupServe.FullEvery + " s (covers a lost fragment)");
            File.Delete(paths[5]);
            var gone = serve.Plan(7, Sources(), 999, File.ReadAllText, t => Fragments.Split(Transport.Pack(t)), t => Transport.Field(t, "name"), t => "m");
            Check(gone.Names.Count == 7 && !gone.Names.Any(n => n.StartsWith("Fellow5")), "GS a copy no longer shared is left out of the names");
            serve.Prune(p => p == 7);
            Check(serve.Requesters == 1, "GS requesters no longer connected are forgotten (bounded)");
            Write(5);

            // measured: 10 minutes with the panel open (a request every 30 s), 8 fellows, each sending a new copy every 5 minutes
            int requests = 20; long oldFragments = 0, newFragments = 0, oldBytes = 0, newBytes = 0;
            for (int i = 0; i < 8; i++) { versions[i] = 100; Write(i); }
            var serve2 = new GroupServe();
            double oldMs = 0, newMs = 0;
            for (int q = 0; q < requests; q++)
            {
                float now = q * 30f;
                for (int i = 0; i < 8; i++) if (q > 0 && (q * 30 + i * 37) % 300 < 30) { versions[i]++; Write(i); }   // each fellow: a new copy every 5 min, staggered
                var sw = Stopwatch.StartNew();
                foreach (var p in paths) { var parts = Fragments.Split(Transport.Pack(File.ReadAllText(p))); oldFragments += parts.Count; oldBytes += parts.Sum(x => x.Length); }   // 0.5: read, gzip and split all, every request
                sw.Stop(); oldMs += sw.Elapsed.TotalMilliseconds;
                sw = Stopwatch.StartNew();
                var ans = serve2.Plan(1, Sources(), now, File.ReadAllText, t => Fragments.Split(Transport.Pack(t)), t => Transport.Field(t, "name"), t => "m");
                sw.Stop(); newMs += sw.Elapsed.TotalMilliseconds;
                foreach (var c in ans.Send) { newFragments += c.Parts.Count; newBytes += c.Parts.Sum(x => x.Length); }
            }
            System.Console.WriteLine($"MEASURED group answers, 8 fellows ({sample.Length / 1024.0:0.0} KB copies), {requests} requests over 10 min: before {oldFragments} fragments / {oldBytes / 1024.0:0} KB, {oldMs / requests:0.00} ms per request; after {newFragments} fragments / {newBytes / 1024.0:0} KB, {newMs / requests:0.00} ms per request ({100.0 * (oldBytes - newBytes) / oldBytes:0}% less traffic)");
            Check(newFragments * 3 < oldFragments && newMs < oldMs, "GS measured: the group answers send under a third of the fragments and cost less server time");
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        return fails;
    }

    // the sum of one table over every player in a saved book (names never printed)
    static double RealSum(ServerBook book, string json, string table, string item)
    {
        double sum = 0;
        var players = MiniJson.Obj(MiniJson.Parse(json) as Dictionary<string, object>, "players");
        foreach (var kv in players ?? new Dictionary<string, object>()) sum += MiniJson.Num(MiniJson.Obj(kv.Value as Dictionary<string, object>, table), item);
        return sum;
    }
}
