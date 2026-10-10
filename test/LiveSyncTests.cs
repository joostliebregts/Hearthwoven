// Live updates (0.7, work/hearthwoven-0.7/SYNC-DESIGN.md item 4): a realistic 10 s update and its packed size; the latest update applied
// on the last full copy gives the numbers of the next full copy (as the server stores and relays them); the server keeps only what it may.
// Fictional players and counts.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hearthwoven;

static class LiveSyncTests
{
    static bool RenameRefused(string key, string stored, string rename)
    {
        var f = new LiveFellows(); f.OnFull(key, stored);
        return f.OnDelta(key, "s1-1", 1, rename) == null;
    }

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- a character the size of a real one (the live server's copy: 109 counters, 251 kinds picked up, 385 pieces, 99 crafts) ----------
        var rnd = new Random(11);
        var stats = new PlayerProfile.PlayerStats[1];
        stats[0] = new PlayerProfile.PlayerStats();
        foreach (PlayerStatType t in Enum.GetValues(typeof(PlayerStatType))) stats[0][t] = (float)Math.Round(rnd.NextDouble() * 5000, 1);
        for (int i = 0; i < 251; i++) stats[0].m_itemPickupStats["$item_kind" + i] = rnd.Next(1, 900);
        for (int i = 0; i < 99; i++) stats[0].m_itemCraftStats["$item_made" + i] = rnd.Next(1, 60);
        for (int i = 0; i < 27; i++) { stats[0].m_pickableStats["Pickable_" + i] = rnd.Next(1, 300); stats[0].m_foodEatenStats["Food" + i] = rnd.Next(1, 90); }
        for (int i = 0; i < 385; i++) stats[0].m_piecesPlacedStats["$piece_kind" + i] = rnd.Next(1, 400);
        for (int i = 0; i < 40; i++) stats[0].m_enemyStats[0]["$enemy_" + i] = rnd.Next(1, 200);
        stats[0].m_knownWorlds["Midgard"] = 3600f * 90;
        var skills = Enumerable.Range(0, 28).Select(i => new Snapshot.SkillInfo { Name = "Skill" + i, Level = (float)Math.Round(rnd.NextDouble() * 70, 3), Accumulator = (float)Math.Round(rnd.NextDouble() * 50, 2) }).ToList();
        var ev = new SessionEvents(); var evBefore = new SessionEvents(); var dmg = new DamageTally(); var dmgBefore = new DamageTally(); var log = new EventLog();
        for (int i = 0; i < 60; i++) { evBefore.PickedUp["$item_kind" + i] = rnd.Next(1, 500); evBefore.Made["$item_made" + i] = rnd.Next(1, 30); evBefore.ChopHits["Beech" + (i % 6)] = rnd.Next(10, 900); }
        var t0 = new DateTime(2026, 10, 9, 20, 0, 0, DateTimeKind.Utc);
        var hit = new HitData.DamageTypes { m_slash = 18.5f, m_fire = 4f };
        for (int m = 0; m < 90; m++) { log.AddDamage(t0.AddMinutes(-90 + m), "BlackForest", true, "Greydwarf", "Axes", hit); dmg.AddDealt("Greydwarf", "Axes", hit); ev.PickedUp["$item_kind" + (m % 20)] = m + 1; }
        const long Id = 9007199254740993L;   // above 2^53: the copy keeps the digits Snapshot wrote, and the update never rewrites them
        string Full(string copyId) => Snapshot.Build("0.7.0", Id, "Åsa", stats, skills, "Midgard", dmg, "s1", ev, log, true,
                                                     SessionEvents.Sum(evBefore, ev), DamageTally.Sum(dmgBefore, dmg), null, null, null, null, new[] { "Meadows", "BlackForest" }, copyId);
        string Stored(string json) => FellowIds.WithPlatform(GroupShare.SharedCopy(json), "76561198000000077");   // as the server keeps and relays it (GroupShare.Store)

        // ---------- 10 s of play ----------
        void TenSeconds(int round)
        {
            stats[0][PlayerStatType.DistanceWalk] += 31.4f; stats[0][PlayerStatType.DistanceRun] += 22.75f; stats[0][PlayerStatType.DistanceTraveled] += 54.15f; stats[0][PlayerStatType.Jumps] += 2;
            stats[0].m_knownWorlds["Midgard"] += 10f;                                   // seconds per world: private, never in an update
            stats[0].m_itemPickupStats["$item_kind3"] += 4; ev.PickedUp["$item_kind3"] = ev.PickedUp.TryGetValue("$item_kind3", out var w) ? w + 4 : 4;
            for (int h = 0; h < 3; h++) { var at = t0.AddSeconds(round * 10 + h); log.AddDamage(at, "BlackForest", true, "Troll", "Axes", hit); dmg.AddDealt("Troll", "Axes", hit); }
            skills[4] = new Snapshot.SkillInfo { Name = "Skill4", Level = skills[4].Level, Accumulator = skills[4].Accumulator + 0.85f };
            if (round == 2) skills.Add(new Snapshot.SkillInfo { Name = "Fishing", Level = 1, Accumulator = 0.2f });   // a skill new this session: the array grows
        }

        var baseFull = Full("s1-1");
        var baseShared = GroupShare.SharedCopy(baseFull);
        TenSeconds(1);
        var watch = Stopwatch.StartNew();
        var now1 = Full("s1-1");
        var buildMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var d1 = LiveDelta.Make(baseShared, GroupShare.SharedCopy(now1), "s1-1", 1);
        var diffMs = watch.Elapsed.TotalMilliseconds;
        var fullPacked = Transport.Pack(baseFull).Length;
        Check(!d1.Empty && !d1.TooBig && d1.Packed.Length < 1000 && d1.Packed.Length * 10 < fullPacked && !LiveDelta.Private(d1.Text) && !d1.Text.Contains("secondsPerWorld"),
              $"live: a realistic 10 s update is {d1.Packed.Length} bytes packed ({d1.Text.Length} bytes of JSON, {((LiveDelta.Parse(d1.Text) as Dictionary<string, object>)["set"] as Dictionary<string, object>).Count} values) " +
              $"against {fullPacked} bytes for the full copy ({baseFull.Length} bytes of JSON); built in {buildMs:0.0} ms, diffed and packed in {diffMs:0.0} ms; nothing private in it");

        // ---------- the fellow: base + the latest update = the next full copy ----------
        TenSeconds(2);
        var d2 = LiveDelta.Make(baseShared, GroupShare.SharedCopy(Full("s1-1")), "s1-1", 2);   // cumulative: since the full copy, not since d1
        var fellows = new LiveFellows();
        var stored = Stored(baseFull);
        var key = FellowIds.KeyOf(stored);
        var early = fellows.OnDelta(key, "s1-1", 2, d2.Text);              // the update can arrive before its full copy: it waits
        var shown = fellows.OnFull(key, stored);                            // ...and is applied the moment the copy comes
        var late = fellows.OnDelta(key, "s1-1", 1, d1.Text);               // the older update, late: ignored
        var next = Stored(Full("s1-2"));                                    // the next full copy, built at the same moment
        bool SameNumbers(string a, string b)
        {
            var ta = LiveDelta.Parse(a) as Dictionary<string, object>; var tb = LiveDelta.Parse(b) as Dictionary<string, object>;
            if (ta == null || tb == null) return false;
            ta.Remove("copyId"); tb.Remove("copyId");
            return LiveDelta.Write(ta) == LiveDelta.Write(tb);
        }
        Check(early == null && late == null && shown != stored && SameNumbers(shown, next) && FellowIds.KeyOf(shown) == key && PeerIdentity.SnapshotPlayerId(shown) == PeerIdentity.SnapshotPlayerId(stored) &&
              !SameNumbers(stored, next),
              "live: the latest update on the last full copy gives exactly the next full copy (every number, a grown skill list, the same fellow key and player id, its digits kept as written); " +
              "an update before its copy waits for it, an older one is ignored");
        for (int r = 3; r <= 12; r++) TenSeconds(r);                           // the rest of the 2 minutes until the next full copy
        var d12 = LiveDelta.Make(baseShared, GroupShare.SharedCopy(Full("s1-1")), "s1-1", 12);
        Check(!d12.TooBig && d12.Packed.Length <= LiveDelta.MaxPacked,
              $"live: the last update before the next full copy (2 minutes of growth) is {d12.Packed.Length} bytes packed, one part (limit {LiveDelta.MaxPacked})");
        var resent = fellows.OnFull(key, stored);
        Check(resent == shown, "live: the server resending the same full copy keeps the update applied (no numbers jumping back)");

        // ---------- the server keeps only what it may ----------
        var store = new LiveStore();
        var sk = "1001";
        Check(store.Offer(sk, 7, "s1-1", 1, d1.Packed, 0) == "not shared", "server: no shared copy here, no update kept");
        store.Full(sk, LiveFellows.CopyIdOf(stored), key);
        var kept = store.Offer(sk, 7, "s1-1", 1, d1.Packed, 0);
        var tooOften = store.Offer(sk, 7, "s1-1", 2, d2.Packed, 3);
        var older = store.Offer(sk, 7, "s1-1", 1, d1.Packed, 10);
        var otherCopy = store.Offer(sk, 7, "s0-9", 3, d2.Packed, 20);
        var tooBig = store.Offer(sk, 7, "s1-1", 4, new byte[LiveDelta.MaxPacked + 1], 30);
        var newer = store.Offer(sk, 7, "s1-1", 2, d2.Packed, 40);
        var gate = new LiveStore();
        Check(gate.MayOffer(7, 0) && !gate.MayOffer(7, 5) && gate.MayOffer(9, 5) && gate.MayOffer(7, 8.5),
              "server: one live message per sender per 8 s gets past the gate, before any unpacking (a flood costs a lookup each)");
        var first = store.For(42, "2002"); var notSent = store.For(42, "2002").Count; store.Served(42, first.FirstOrDefault());
        var again = store.For(42, "2002"); var own = store.For(7, sk);
        Check(kept == null && tooOften == "too often" && older == "older" && otherCopy == "other copy" && tooBig == "too big" && newer == null &&
              first.Count == 1 && first[0].Seq == 2 && first[0].Fellow == key && notSent == 1 && again.Count == 0 && own.Count == 0 && store.Count == 1,
              "server: one update per player in memory (the latest, on its stored copy), at most one per 8 s and one part; each requester gets it once it went out (not before: one dropped on the way comes again), never its own");
        store.Full(sk, "s1-2", key);
        var afterFull = store.Count;
        store.Offer(sk, 7, "s1-2", 1, d1.Packed, 60); store.Unshare(sk);
        var afterUnshare = store.Count + (store.Offer(sk, 7, "s1-2", 2, d1.Packed, 80) == "not shared" ? 0 : 1);
        store.Full(sk, "s1-2", key); store.Offer(sk, 7, "s1-2", 3, d1.Packed, 100); store.Served(42, store.For(42, "2002").FirstOrDefault()); store.Prune(p => p != 7 && p != 42);
        Check(afterFull == 0 && afterUnshare == 0 && store.Count == 0 && store.Requesters == 0,
              "server: a new full copy drops the old update, unsharing drops everything, a peer that left leaves nothing behind (bounded by who is connected)");
        var leak = "{\"live\":1,\"base\":\"s1-1\",\"seq\":1,\"set\":{\"/measuredLog/deaths/0/x\":12.5,\"/stats/0/secondsPerWorld/Midgard\":9},\"del\":[]}";
        var leakValue = "{\"live\":1,\"base\":\"s1-1\",\"seq\":1,\"set\":{\"/measuredLog/deaths\":[{\"t\":\"x\",\"x\":1,\"z\":2}]},\"del\":[]}";
        var rename = "{\"live\":1,\"base\":\"s1-1\",\"seq\":9,\"set\":{\"/platformId\":\"Steam_1\",\"/stats/0/counters/Jumps\":1},\"del\":[]}";
        var reid = "{\"live\":1,\"base\":\"s1-1\",\"seq\":9,\"set\":{\"/playerId\":5},\"del\":[\"/name\"]}";
        Check(LiveDelta.Identity(rename) && LiveDelta.Identity(reid) && !LiveDelta.Identity(d2.Text) && LiveDelta.Apply(stored, rename) == null &&
              RenameRefused(key, stored, rename),
              "identity: an update that names another player (platform id, player id, name, copy id) is refused by the server and never applied");
        Check(LiveDelta.Private(leak) && LiveDelta.Private(leakValue) && !LiveDelta.Private(d2.Text) && LiveDelta.Read(d2.Text, out var rb, out var rs) && rb == "s1-1" && rs == 2,
              "server: an update with a death position or the worlds played is refused (in a path or inside a value)");

        // ---------- the announcement: only a 0.7 server says it keeps live updates; the names, book and keys read as before ----------
        var withTag = GroupShare.WriteList(new[] { "Edda" }, "{}", new[] { "Steam_1#5" }, live: true); withTag.SetPos(0);
        var names = GroupShare.ReadList(withTag, out var books, out var keys, out var live);
        var without = GroupShare.WriteList(new[] { "Edda" }, "{}", new[] { "Steam_1#5" }); without.SetPos(0);
        GroupShare.ReadList(without, out _, out _, out var live0);
        Check(live && !live0 && names.Single() == "Edda" && books == "{}" && keys.Single() == "Steam_1#5",
              "list: the live tag comes last, after the book and the keys (an older reader stops before it); without it a 0.7 client sends no live updates");
        return fails;
    }
}
