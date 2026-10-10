// Deeds > Recent (0.7): the deeds per minute (DeedLog) are a time split of the session tallies, folding and capping keep every sum, a window
// returns only what grew inside it, the cost per frame and per minute stays tiny; and Since last time (FellowMarks, RecentModel). Fictional ids.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class DeedLogTests
{
    static readonly DateTime T0 = new DateTime(2026, 10, 9, 18, 0, 0, DateTimeKind.Utc);

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        float Fam(Dictionary<string, Dictionary<string, float>> g, string f) => g.TryGetValue(f, out var d) ? d.Values.Sum() : 0f;
        bool Near(double a, double b) => Math.Abs(a - b) <= 0.01 + Math.Abs(b) * 1e-5;

        // ---------- a heavy 6-hour session: every family's buckets add up to its session tally, before and after the folds ----------
        var rnd = new Random(7);
        var ev = new SessionEvents();
        var game = new Dictionary<string, float> { ["counters|EnemyKills"] = 500, ["placed|$piece_woodwall"] = 900, ["kills|$enemy_greydwarf"] = 300 };
        var startGame = new Dictionary<string, float>(game);
        var log = new DeedLog();
        int frames = 0;
        bool Sums()
        {
            var all = log.Grew(null);
            foreach (var f in DeedLog.EventFamilies)
            {
                var tally = ev.Named().First(n => n.Key == f).Value.Values.Sum();
                if (!Near(Fam(all, f), tally)) { System.Console.WriteLine("   " + f + ": buckets " + Fam(all, f) + " vs tally " + tally); return false; }
            }
            foreach (var f in DeedLog.GameFamilies)
            {
                var grown = game.Where(kv => kv.Key.StartsWith(f + "|")).Sum(kv => kv.Value - (startGame.TryGetValue(kv.Key, out var s) ? s : 0f));
                if (!Near(Fam(all, f), grown)) { System.Console.WriteLine("   " + f + ": buckets " + Fam(all, f) + " vs growth " + grown); return false; }
            }
            return true;
        }
        var t = T0; bool sumsBeforeFold = true;
        for (int sec = 0; sec < 6 * 3600; sec += 3)
        {
            t = T0.AddSeconds(sec);
            if (rnd.Next(4) == 0) SessionEvents.Add(ev.PickedUp, "$item_t" + rnd.Next(60), rnd.Next(1, 5));
            if (rnd.Next(6) == 0) SessionEvents.Add(ev.ChopHits, "Beech" + rnd.Next(3));
            if (rnd.Next(30) == 0) SessionEvents.Add(ev.Felled, "Beech" + rnd.Next(3));
            if (rnd.Next(20) == 0) SessionEvents.Add(ev.Made, "$item_food" + rnd.Next(8), 2);
            if (rnd.Next(10) == 0) SessionEvents.Add(ev.SkillPractice, "WoodCutting", 0.37f);
            if (rnd.Next(12) == 0) { var pk = "placed|$piece_p" + rnd.Next(40); game[pk] = (game.TryGetValue(pk, out var p) ? p : 0) + 1; }
            if (rnd.Next(40) == 0) { game["counters|EnemyKills"] += 1; game["kills|$enemy_greydwarf"] += 1; }
            log.Tick(t, ev, () => game); frames++;
            if (sec == 3 * 3600) sumsBeforeFold = Sums();
        }
        log.Flush(t, ev, game);
        Check(sumsBeforeFold, "R sums: after 3 h every family's minute buckets add up to its session tally (SessionEvents) or its game counter's growth this session");
        var folded = log.Buckets.Keys.Count(k => string.CompareOrdinal(k, 0, EventLog.Bucket(t.AddMinutes(-EventLog.FoldAfterMinutes)), 0, 17) < 0 && k[15] != '0');
        Check(Sums() && folded == 0, "R fold: after 6 h (minutes older than " + EventLog.FoldAfterMinutes + " min folded to ten) every sum still equals its tally; no unfolded old minute left");
        Check(log.GameAtStart != null && log.GameAtStart["counters|EnemyKills"] == 500, "R mark: the game's counters before the session are not this session's (the first flush only takes the mark)");

        // ---------- caps keep the largest, and the family's sum ----------
        var capEv = new SessionEvents(); var cap = new DeedLog();
        for (int i = 1; i <= 100; i++) SessionEvents.Add(capEv.PickedUp, "$item_k" + i, i);
        cap.Flush(T0, capEv, null);
        var minute = cap.Buckets.Where(kv => kv.Key.StartsWith(EventLog.Bucket(T0) + "|")).ToList();
        var named = minute.Where(kv => !kv.Key.EndsWith(DeedLog.OtherToken)).Select(kv => kv.Value).ToList();
        Check(minute.Count == DeedLog.MaxKeysPerBucket + 1 && named.Count == DeedLog.MaxKeysPerBucket && named.Min() == 100 - DeedLog.MaxKeysPerBucket + 1 && cap.Clipped && Near(minute.Sum(kv => kv.Value), 5050),
              "R cap: a minute with 100 kinds keeps " + DeedLog.MaxKeysPerBucket + " by name, the largest (smallest kept " + named.Min() + "), the rest under " + DeedLog.OtherToken + "; the sum stays 5050");

        // ---------- a window returns only what grew inside it ----------
        var wEv = new SessionEvents(); var wLog = new DeedLog(); var now = T0.AddHours(2);
        wLog.Tick(now.AddMinutes(-50), wEv, null);
        SessionEvents.Add(wEv.PickedUp, "$item_wood", 10); wLog.Tick(now.AddMinutes(-40), wEv, null);   // booked at -50
        SessionEvents.Add(wEv.PickedUp, "$item_wood", 5); wLog.Tick(now.AddMinutes(-20), wEv, null);    // booked at -40
        SessionEvents.Add(wEv.PickedUp, "$item_stone", 2); wLog.Tick(now.AddMinutes(-5), wEv, null);    // booked at -20
        SessionEvents.Add(wEv.Felled, "Oak1", 1); wLog.Flush(now, wEv, null);                           // the running minute
        var self = new PanelInput { IsSelf = true, NowUtc = now, SessionStartUtc = now.AddMinutes(-55), Deeds = wLog };
        var r10 = PanelModel.RecentOf(self, TimeWindow.LastTenMinutes); var r30 = PanelModel.RecentOf(self, TimeWindow.LastThirtyMinutes);
        var rh = PanelModel.RecentOf(self, TimeWindow.LastHour); var rs = PanelModel.RecentOf(self, TimeWindow.Session);
        Check(r10.Grew.Count == 1 && r10.Total(DeedLog.Felled) == 1 && r30.Total(DeedLog.PickedUp) == 2 && r30.Total(DeedLog.Felled) == 1 &&
              rh.Total(DeedLog.PickedUp) == 17 && rs.Total(DeedLog.PickedUp) == 17 && rs.FromUtc == self.SessionStartUtc && r10.FromUtc == now.AddMinutes(-10),
              "R window: 10 min holds only the tree felled now, 30 min the stone too, 1 h and Session all 17 picked up");

        // the day windows read the day history (a row 9 days back is outside 7 days); kills per foe have no days
        var hist = new DayHistory(); var localNow = DateTime.Now;
        SessionEvents Wood(float n) { var e = new SessionEvents(); SessionEvents.Add(e.PickedUp, "$item_wood", n); return e; }
        hist.Add(localNow.AddDays(-9), Wood(5), new DamageTally(), null, new Dictionary<string, float> { ["placed|$piece_woodwall"] = 10, ["EnemyKills"] = 1 });
        hist.Add(localNow.AddDays(-3), Wood(7), new DamageTally(), null, new Dictionary<string, float> { ["placed|$piece_woodwall"] = 14, ["EnemyKills"] = 3 });
        var r7 = PanelModel.RecentOf(new PanelInput { IsSelf = true, NowUtc = DateTime.UtcNow, History = hist, Deeds = wLog }, TimeWindow.SevenDays);
        Check(r7.Shown && r7.Total(DeedLog.PickedUp) == 7 && r7.Total(DeedLog.Placed) == 4 && r7.Grew[DeedLog.Counters]["EnemyKills"] == 2 && r7.NotRecorded.Contains(DeedLog.Kills),
              "R window: 7 days reads the day rows inside it (7 wood, 4 walls, 2 kills; the row of 9 days ago left out); kills per foe: not recorded per day");

        // ---------- cost ----------
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1_000_000; i++) log.Tick(t, ev, () => game);
        var perFrameNs = sw.Elapsed.TotalMilliseconds * 1e6 / 1_000_000;
        var heavyEv = new SessionEvents(); var heavyGame = new Dictionary<string, float>(); var heavy = new DeedLog();
        for (int i = 0; i < 300; i++) { SessionEvents.Add(heavyEv.PickedUp, "$item_h" + i, 1); heavyGame["placed|$piece_h" + i] = 1; }
        heavy.Flush(T0, heavyEv, heavyGame);
        sw.Restart();
        for (int m = 1; m <= 600; m++)
        {
            for (int i = 0; i < 20; i++) { SessionEvents.Add(heavyEv.PickedUp, "$item_h" + ((m * 7 + i) % 300), 1); heavyGame["placed|$piece_h" + ((m * 3 + i) % 300)] += 1; }
            heavy.Tick(T0.AddMinutes(m), heavyEv, () => heavyGame);
        }
        var perMinuteMs = sw.Elapsed.TotalMilliseconds / 600;
        var bytes = log.Buckets.Sum(kv => 2 * kv.Key.Length + 20 + 40);   // string chars + header, value, dictionary entry (approximate)
        Check(perFrameNs < 200 && perMinuteMs < 2 && log.Buckets.Count <= DeedLog.MaxKeys,
              "R cost: per frame " + perFrameNs.ToString("0") + " ns (one minute comparison); per minute flush over 600 kinds " + perMinuteMs.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms; nothing per event; the heavy 6 h session holds " +
              log.Buckets.Count + " keys, about " + (bytes / 1024) + " KB (cap " + DeedLog.MaxKeys + " keys)");

        // ---------- since last time: yours ----------
        var back = new PanelInput { IsSelf = true, NowUtc = now, SessionStartUtc = now.AddMinutes(-55), PreviousSessionEndUtc = now.AddDays(-2), Deeds = wLog };
        var mine = PanelModel.SinceLastTime(back);
        Check(mine.Shown && mine.FromUtc == back.PreviousSessionEndUtc && mine.Total(DeedLog.PickedUp) == 17 && PanelModel.AwayFor(back) == TimeSpan.FromDays(2) - TimeSpan.FromMinutes(55) &&
              !PanelModel.SinceLastTime(self).Shown && PanelModel.AwayFor(self) == null,
              "R since last time (yours): this session from your previous session's end; away = previous end to this session's start; the first session here: not shown, away unknown");

        // ---------- since last time: a fellow, through the marks file ----------
        string Copy(float wood, float walls, float kills, bool since, string session)
        {
            var st = new PlayerProfile.PlayerStats[1]; st[0] = new PlayerProfile.PlayerStats();
            st[0][PlayerStatType.EnemyKills] = kills; st[0].m_piecesPlacedStats["$piece_woodwall"] = walls; st[0].m_enemyStats[0]["$enemy_troll"] = kills;
            var e = new SessionEvents(); SessionEvents.Add(e.PickedUp, "$item_wood", wood);
            return GroupShare.SharedCopy(Snapshot.Build("0.7.0", 42L, "Tor", st, new Snapshot.SkillInfo[0], "W", new DamageTally(), session, e, new EventLog(), true, since ? e : null));
        }
        var marks = new FellowMarks();
        marks.Update(new Dictionary<string, string> { ["steam:42"] = Copy(100, 20, 3, true, "s1") }, new Dictionary<string, DateTime> { ["steam:42"] = T0 }, T0);
        var reloaded = FellowMarks.FromJson(marks.ToJson());   // the next session loads it as Before
        var tor = PanelInput.FromSnapshot(Copy(160, 26, 5, true, "s3")); tor.SeenBefore = reloaded.BeforeOf("steam:42"); tor.NowUtc = now;
        var since = PanelModel.SinceLastTime(tor);
        Check(since.Shown && since.FromUtc == T0 && since.Total(DeedLog.PickedUp) == 60 && since.Total(DeedLog.Placed) == 6 && since.Grew[DeedLog.Kills]["$enemy_troll"] == 2 && since.Total(DeedLog.Counters) == 2,
              "R since last time (fellow): their copy now minus the mark your PC kept (through the file): 60 wood, 6 walls, 2 trolls, however many sessions in between");
        var restarted = PanelInput.FromSnapshot(Copy(30, 26, 5, true, "s4")); restarted.SeenBefore = tor.SeenBefore;
        var older = PanelInput.FromSnapshot(Copy(160, 26, 5, false, "s4")); older.SeenBefore = tor.SeenBefore;
        var first = PanelInput.FromSnapshot(Copy(160, 26, 5, true, "s4"));
        var rr = PanelModel.SinceLastTime(restarted); var ro = PanelModel.SinceLastTime(older);
        Check(rr.Restarted.Contains(DeedLog.PickedUp) && !rr.Grew.ContainsKey(DeedLog.PickedUp) && rr.Total(DeedLog.Placed) == 6 &&
              ro.NotRecorded.Contains(DeedLog.PickedUp) && ro.Total(DeedLog.Placed) == 6 && !PanelModel.SinceLastTime(first).Shown,
              "R since last time (fellow): a count that went down is 'restarted', never negative; a copy without since-install totals: those not recorded; never seen before: not shown");
        // B33 (Joost 2026-10-10): Session is YOUR session's span. Tor's copy that reached you at login holds what he did before you came in
        // (160 wood, 26 walls): never counted. What reached you after it is (15 wood, 2 walls, a troll); a fellow who was not in the world
        // during your session and grew nothing says so; the short windows need timed copies (live updates).
        var start = now.AddMinutes(-60);
        tor.ViewerSessionStartUtc = start; tor.OnThisSession = true;
        tor.Trail = new FellowTrail(PanelInput.FromSnapshot(Copy(160, 26, 5, true, "s3")), start.AddSeconds(15));
        tor.Trail.Add(PanelInput.FromSnapshot(Copy(175, 28, 6, true, "s3")), now.AddMinutes(-2));
        var fs = PanelModel.RecentOf(tor, TimeWindow.Session); var f10 = PanelModel.RecentOf(tor, TimeWindow.LastTenMinutes);
        var away = PanelInput.FromSnapshot(Copy(160, 26, 5, true, "s2")); away.NowUtc = now; away.ViewerSessionStartUtc = start; away.OnThisSession = false;
        away.Trail = new FellowTrail(PanelInput.FromSnapshot(Copy(160, 26, 5, true, "s2")), start.AddSeconds(15));
        var fa = PanelModel.RecentOf(away, TimeWindow.Session);
        tor.Timed = true; var t10 = PanelModel.RecentOf(tor, TimeWindow.LastTenMinutes); var t1 = PanelModel.RecentOf(tor, TimeWindow.LastHour);
        Check(fs.Shown && fs.Total(DeedLog.PickedUp) == 15 && fs.Total(DeedLog.Placed) == 2 && fs.Grew[DeedLog.Kills]["$enemy_troll"] == 1 && !f10.Shown &&
              !fa.Shown && fa.Row == PanelModel.RecentNotOn && fa.Why == PanelModel.FellowNotOn(away) && t10.Total(DeedLog.PickedUp) == 15 && t1.Total(DeedLog.PickedUp) == 15,
              "R group (B33): a fellow's copy from before your session start is not counted in Session (15 of 175 wood); a fellow not on during your session says so; 10 min only with timed copies");
        var bigMarks = new FellowMarks();
        var bigCopies = new Dictionary<string, string>();
        for (int i = 0; i < 4; i++)
        {
            var st = new PlayerProfile.PlayerStats[1]; st[0] = new PlayerProfile.PlayerStats();
            var e = new SessionEvents();
            for (int k = 0; k < 400; k++) { st[0].m_piecesPlacedStats["$piece_heavy" + k] = k + 1; SessionEvents.Add(e.PickedUp, "$item_heavy" + k, k + 1); }
            for (int k = 0; k < 60; k++) st[0].m_enemyStats[0]["$enemy_e" + k] = k + 1;
            st[0][PlayerStatType.EnemyKills] = 1830;
            bigCopies["p" + i] = GroupShare.SharedCopy(Snapshot.Build("0.7.0", 100L + i, "F" + i, st, new Snapshot.SkillInfo[0], "W", new DamageTally(), "s", e, new EventLog(), true, e));
        }
        bigMarks.Update(bigCopies, null, T0);
        var perFellow = bigMarks.ToJson().Length / 4;
        Check(perFellow < 40_000 && bigMarks.Latest.Values.All(m => m.Values.Keys.Count(k => k.StartsWith("placed|")) == FellowMarks.MaxKeysPerFamily),
              "R marks size: a heavy fellow (400 kinds placed, 400 picked up, 60 foes) keeps " + FellowMarks.MaxKeysPerFamily + " per family, about " + (perFellow / 1024) + " KB in the marks file");
        Check(GroupShare.AskInBackground(true, true, true) && !GroupShare.AskInBackground(false, true, true) && !GroupShare.AskInBackground(true, false, true) && !GroupShare.AskInBackground(true, true, false),
              "R group request in the background: only when you share, and only spawned in the world (never in the main menu or before spawn)");
        return fails;
    }
}
