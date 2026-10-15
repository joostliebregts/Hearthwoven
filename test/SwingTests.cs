// 0.8.6 damage per second (SwingClock.cs): seconds of fighting, not wall clock. One sample fight with a long gap in it, kept the way the hooks keep it
// (the weapon's clock, the log's minutes, the dealt ledger, the recorder's fight), and read back from every file it is saved in.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;

static class SwingTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        HitData.DamageTypes D(float slash = 0, float pierce = 0) => new HitData.DamageTypes { m_slash = slash, m_pierce = pierce };
        var t0 = new DateTime(2026, 10, 12, 20, 30, 0, DateTimeKind.Utc);

        // a fight: four sword hits a second apart, 10 s of nothing, four more; one arrow in the middle of the first four (its own clock)
        var clock = new SwingClock(); var log = new EventLog(); var ledger = new ArmourTally(); var rec = new BattleRecorder();
        var troll = new FoeId(7, 1);
        void Hit(double at, string skill, float amount)
        {
            var utc = t0.AddSeconds(at); var d = skill == "Bows" ? D(pierce: amount) : D(slash: amount);
            var kind = SwingClock.KindOf(skill); var secs = clock.Hit(kind, utc);
            log.AddDamage(utc, "Swamp", true, "Troll", skill, d);
            if (secs > 0) { log.AddSwing(utc, kind, secs, SwingClock.SwungOf(d)); ledger.AddSwing(kind, secs, SwingClock.SwungOf(d)); }
            rec.Dealt(utc, troll, "Troll", "$enemy_troll", "Swamp", d);
        }
        Hit(0, "Swords", 100); Hit(1, "Swords", 100); Hit(2, "Bows", 50); Hit(3, "Swords", 100); Hit(4, "Swords", 100);
        foreach (var at in new[] { 14.0, 15, 16, 20 }) Hit(at, "Swords", 100);   // 10 s later; the last one 4 s after the one before: exactly the gap, it counts
        // sword: 1 + 2 + 1 = 4 s in the first stretch, then 1 + 1 + 4 = 6 s in the second, the first hit of each stretch counted for no time: 10 s, 6 hits of 100.
        // The fight's clock is every weapon's: the arrow at 2 s keeps its stretch going (1 s each way) and its 50 is swung in it: 10 s, 650
        var (secs, dmg) = ledger.SwingOf("melee"); var bow = ledger.SwingOf("bow");
        var fight = rec.Fights()[0];
        var perMinute = log.Swing.Where(kv => kv.Key.EndsWith("|melee|s")).Sum(kv => kv.Value);
        Check(Math.Abs(secs - 10) < 0.001 && dmg == 600 && Math.Abs(perMinute - secs) < 0.001 && log.Swing.Where(kv => kv.Key.EndsWith("|melee|d")).Sum(kv => kv.Value) == 600 &&
              bow.seconds == 0 && bow.damage == 0 && Math.Abs(fight.SwingSeconds - 10) < 0.001 && fight.SwingDamage == 650,
              "the rule on a sample fight with a 10 s gap: sword fighting time 10 s (not the 20 s of the clock), 600 damage swung in it; the arrow starts its own clock and adds nothing; the fight's clock (any weapon) is 10 s too, 650 damage");
        // pace: 60 DPS for the sword, a dash under SwingClock.Floor seconds
        Check(Math.Abs(SwingClock.Pace(dmg, secs) - 60) < 0.001 && SwingClock.Words(SwingClock.Pace(dmg, secs)) == "60 DPS" && SwingClock.Pace(300, 4.9) < 0 && SwingClock.Words(-1) == "" && SwingClock.Pace(300, 5) == 60,
              "pace: 600 damage in 10 s is 60 DPS; under 5 s of fighting there is no pace (a dash on the page)");

        // saved and read back: the log's minutes in your kept minutes (the local file, whole), the shared copy's measuredLog without the fighting time (it stays on your PC), the ledger's book (the file), the kept fight and the shared copy (0.8.6: a friend's DPS in their Last fight)
        var keptMinutes = KeptMinutes.Take(t0.AddMinutes(2), log, null);
        var back = KeptMinutes.FromJson(keptMinutes.ToJson(t0.AddMinutes(2))).Log;
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats();
        var copy = GroupShare.SharedCopy(Snapshot.Build("0.8.6", 7, "Rowan", stats, new Snapshot.SkillInfo[0], "Midgard", new DamageTally(), "s1", new SessionEvents(), log, true));
        var measured = MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(copy), "measuredLog");
        var between = log.Between(t0, t0.AddMinutes(1));
        var book = new ArmourBook { FromUtc = t0.AddDays(-1), SwingFromUtc = t0 };
        book.Record("S1", ledger, t0.ToLocalTime());
        var bj = new Json().Open(); book.WriteTo(bj, "armour"); var bookBack = ArmourBook.ReadFrom(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(bj.Close().ToString()), "armour"));
        var day = bookBack.Days.Count == 1 ? bookBack.Days[0].Tally.SwingOf("melee") : (0, 0);
        Check(back.Swing.Count == log.Swing.Count && back.Swing.All(kv => Math.Abs(log.Swing[kv.Key] - kv.Value) < 0.001) && between.Swing.Count == log.Swing.Count &&
              Math.Abs(bookBack.Last.SwingOf("melee").seconds - 10) < 0.001 && Math.Abs(day.Item1 - 10) < 0.2 && bookBack.SwingFromUtc == t0,
              "saved: your kept minutes read the log's " + back.Swing.Count + " swing keys back whole and cut by minute; the ledger's book keeps the sword's 10 s in the session and in its day row, and the day the count began");
        Check(measured != null && measured.ContainsKey("damage") && !measured.ContainsKey("swing") && !copy.Contains("\"swing\""),
              "the shared copy's measuredLog keeps its damage and leaves the fighting time out (it stays on your PC)");

        var feed = FeedShare.From(rec, _ => false); var shared = new Json().Open(); feed.WriteTo(shared, "battleFeed", hits: false); var sharedText = shared.Close().ToString();
        var kept = new Json().Open(); feed.WriteTo(kept, "battleFeed", hits: true); var keptText = kept.Close().ToString();
        var keptBack = FeedShare.ReadFrom(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(keptText), "battleFeed"));
        var sharedBack = FeedShare.ReadFrom(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(sharedText), "battleFeed"));
        Check(sharedBack.Fights.Count == 1 && Math.Abs(sharedBack.Fights[0].SwingSeconds - 10) < 0.001 && sharedBack.Fights[0].SwingDamage == 650 &&
              keptBack.Fights.Count == 1 && Math.Abs(keptBack.Fights[0].SwingSeconds - 10) < 0.001 && keptBack.Fights[0].SwingDamage == 650,
              "a fight's own fighting time is in your kept fights and (0.8.6, a friend's Last fight shows their DPS on their book) in the copy shared with fellow players");

        // the cost per hit (the 0.8.1 speed rules: no object per hit): the weapon's clock, the log's minute and the ledger. Within one minute nothing is
        // allocated; the minute's six keys are made once a minute (a hit a second: about 8 bytes a hit over the minute)
        var hot = new SwingClock(); var hotLog = new EventLog(); var hotLedger = new ArmourTally();
        void Swing(DateTime u) { var s0 = hot.Hit(0, u); hotLog.AddSwing(u, 0, s0, 40); hotLedger.AddSwing(0, s0, 40); }
        for (int i = 0; i < 5000; i++) Swing(t0.AddSeconds(i % 500 * 0.1));   // warm
        const int N = 400000; long before = GC.GetAllocatedBytesForCurrentThread(); var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < N; i++) Swing(t0.AddSeconds(i % 500 * 0.1));   // 50 s of the minute 20:30, again and again
        var ns = sw.Elapsed.TotalMilliseconds * 1e6 / N; var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)N;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int m = 0; m < 600; m++) { Swing(t0.AddMinutes(100 + m)); Swing(t0.AddMinutes(100 + m).AddSeconds(1)); }   // two hits a second apart in each of 600 new minutes
        var perNewMinute = (GC.GetAllocatedBytesForCurrentThread() - before) / 600.0;
        System.Console.WriteLine("MEASURED swing, per hit: " + ns.ToString("0") + " ns and " + bytes.ToString("0.00") + " bytes allocated within a minute; " + perNewMinute.ToString("0") + " bytes for each new minute (its six keys and the log's two entries)");
        Check(bytes < 0.01 && perNewMinute < 2000, "no object per hit: " + bytes.ToString("0.00") + " bytes allocated per hit by the weapon's clock, the log and the ledger; " + perNewMinute.ToString("0") + " bytes for each new minute");
        return fails;
    }
}
