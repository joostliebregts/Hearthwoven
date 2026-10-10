// The day history (HISTORY-06.md): recording what each save adds (no double count, a new session, midnight, the game counters' mark),
// the live part of the running session, folding (35 days -> weeks -> months -> before, every sum kept), the size budget (a heavy year,
// the worst case), clipping, the file (a 0.6.1 file without history loads and starts one; history and unknown keys survive a rewrite;
// rows out of order are not trusted) and the shared damage dealt per day. Fictional ids; files in a fresh temp folder.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Hearthwoven;

static class HistoryTests
{
    const long Id = 2718281828L;
    static readonly DateTime Day1 = new DateTime(2026, 10, 9, 20, 0, 0);   // local evenings

    static SessionEvents Ev(float chops, int blocks = 0) { var e = new SessionEvents { Blocks = blocks }; SessionEvents.Add(e.ChopHits, "Beech1", chops); return e; }
    static DamageTally Dmg(float slash) { var d = new DamageTally(); if (slash > 0) d.Dealt["Troll|Swords|slash"] = slash; d.HitsDealt = (int)(slash / 10); return d; }
    static Dictionary<string, float> Game(float sail, float kills = 0) => new Dictionary<string, float> { ["DistanceSail"] = sail, ["EnemyKills"] = kills, ["BuildClusterDefense"] = 40 };
    static float Chops(SessionEvents e) => e.ChopHits.TryGetValue("Beech1", out var v) ? v : 0;
    static float Get(Dictionary<string, float> d, string k) => d.TryGetValue(k, out var v) ? v : 0;

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- recording: differences, never totals twice ----------
        var t = new LocalTotals { PlayerId = Id };
        t.Record("S1", Dmg(100), Ev(5, 2), null, Game(1000, 10), Day1);
        var h = t.History;
        Check(h.From == Day1.Date && h.Rows.Count == 1 && h.Rows[0].Period == "2026-10-09" && Chops(h.Rows[0].Events) == 5 && h.Rows[0].Events.Blocks == 2 && h.Rows[0].Game.Count == 0 &&
              Get(h.GameMark, "DistanceSail") == 1000 && !h.GameMark.ContainsKey("BuildClusterDefense"),
              "H record: the first save starts the history on its local day; the game counters only set the mark (nothing booked before it); a most-at-once counter is never kept");
        t.Record("S1", Dmg(160), Ev(8, 3), null, Game(1500, 12), Day1.AddMinutes(5));
        Check(h.Rows.Count == 1 && Chops(h.Rows[0].Events) == 8 && h.Rows[0].Events.Blocks == 3 && Get(h.Rows[0].Damage.Dealt, "Troll|Swords|slash") == 160 &&
              Get(h.Rows[0].Game, "DistanceSail") == 500 && Get(h.Rows[0].Game, "EnemyKills") == 2,
              "H record: the same session saved again adds only what it grew by (8 chops, not 13); the game counters by their growth since the mark");
        t.Record("S2", Dmg(40), Ev(2), null, Game(1600, 12), Day1.AddHours(4).AddMinutes(5));   // 00:05 the next day, a new session
        Check(h.Rows.Count == 2 && h.Rows[1].Period == "2026-10-10" && Chops(h.Rows[1].Events) == 2 && Get(h.Rows[1].Damage.Dealt, "Troll|Swords|slash") == 40 && Get(h.Rows[1].Game, "DistanceSail") == 100,
              "H record: a new session after midnight books on the new day, all of it (it was not recorded before)");
        Check(h.Rows.Sum(r => Chops(r.Events)) == Chops(t.EventsBefore("S3")) && h.Rows.Sum(r => Get(r.Damage.Dealt, "Troll|Swords|slash")) == Get(t.DamageBefore("S3").Dealt, "Troll|Swords|slash"),
              "H record: the day rows add up to the since-install totals (history from the first save)");
        t.Record("S2", Dmg(40), Ev(2), null, Game(1400, 12), Day1.AddHours(5));
        Check(h.Rows[1].Game.Values.All(v => v > 0) && Get(h.Rows[1].Game, "DistanceSail") == 100 && Get(h.GameMark, "DistanceSail") == 1400,
              "H record: a game counter that went down (an older copy of the character played elsewhere) books nothing; the mark follows it");
        t.Record("S2", Dmg(40), Ev(2), null, null, Day1.AddHours(6));
        Check(Get(h.GameMark, "DistanceSail") == 1400, "H record: no profile at the moment of a save: nothing booked, the mark stays");
        var back = new LocalTotals { PlayerId = Id }; back.Record("A", null, Ev(1), null, null, Day1); back.Record("A", null, Ev(3), null, null, Day1.AddDays(-2));
        Check(back.History.Rows.Count == 1 && Chops(back.History.Rows[0].Events) == 3, "H record: a clock that went back books on the newest row, never a row out of order");
        var noDay = new LocalTotals { PlayerId = Id }; noDay.Record("A", null, Ev(1));
        Check(noDay.History.Rows.Count == 0 && !noDay.ToJson(Day1).Contains("\"history\""), "H record: a caller without a local time (older code paths) records no history, and the file carries none");

        // ---------- the live part ----------
        var live = t.Pending("S2", Dmg(70), Ev(6), new BiomeTally(), Game(1450, 13), Day1.AddHours(6));
        Check(live.Period == "2026-10-10" && Chops(live.Events) == 4 && Get(live.Damage.Dealt, "Troll|Swords|slash") == 30 && Get(live.Game, "DistanceSail") == 50 && Get(live.Game, "EnemyKills") == 1 &&
              Get(h.GameMark, "DistanceSail") == 1400 && Chops(h.Rows[1].Events) == 2,
              "H live: what the running session counted since the last save, as today's row; nothing recorded, the mark unmoved");
        var liveNew = t.Pending("S9", Dmg(10), Ev(1), null, null, Day1.AddHours(6));
        Check(Chops(liveNew.Events) == 1 && Get(liveNew.Damage.Dealt, "Troll|Swords|slash") == 10, "H live: a session not saved yet counts whole");

        // ---------- folding ----------
        var f = new DayHistory();
        var today = new DateTime(2027, 12, 31);
        var start = today.AddDays(-1099);
        for (var d = start; d <= today; d = d.AddDays(1))
        {
            f.Add(d.AddHours(20), Ev(1, 1), Dmg(10), null, null);
        }
        var dayRows = f.Rows.Count(r => r.IsDay); var weekRows = f.Rows.Count(r => r.Period.StartsWith("w")); var monthRows = f.Rows.Count(r => r.Period.StartsWith("m")); var before = f.Rows.Count(r => r.Period == DayHistory.BeforeKey);
        Check(dayRows == DayHistory.DayRows && weekRows <= DayHistory.WeekRows + 1 && monthRows <= DayHistory.MonthRows + 1 && before == 1 && f.Rows.Count <= 88,
              "H fold: 1 100 play days keep " + dayRows + " day rows, " + weekRows + " weeks, " + monthRows + " months and one row before (at most 88)");
        Check(f.Rows.Sum(r => Chops(r.Events)) == 1100 && f.Rows.Sum(r => r.Events.Blocks) == 1100 && Math.Abs(f.Rows.Sum(r => Get(r.Damage.Dealt, "Troll|Swords|slash")) - 11000) < 0.01 && f.Rows.Sum(r => r.Damage.HitsDealt) == 1100,
              "H fold: every sum is kept through the folds (1 100 chops, blocks and hits, 11 000 damage)");
        Check(f.Rows.Zip(f.Rows.Skip(1), (a, b) => a.Start <= b.Start).All(x => x) && f.Rows.Last().Period == "2027-12-31" && f.Rows.First(r => r.IsDay).Period == DayHistory.DayKey(today.AddDays(1 - DayHistory.DayRows)),
              "H fold: oldest first; the day rows are exactly the last 35 days");
        var w30 = f.Sum(today.AddDays(-29), today); var w7 = f.Sum(today.AddDays(-6), today); var w1 = f.Sum(today, today);
        Check(Chops(w30.Events) == 30 && Chops(w7.Events) == 7 && Chops(w1.Events) == 1, "H windows: 30 days, 7 days and today read 30, 7 and 1 day rows");
        var weeks = f.Rows.Where(r => r.Period.StartsWith("w")).ToList();
        Check(weeks.All(r => DayHistory.Monday(r.Start) == r.Start) && weeks.Take(weeks.Count - 1).All(r => Chops(r.Events) == 7),
              "H fold: a week row starts on a Monday and holds its seven days");

        // ---------- clipping ----------
        var big = new DayHistory.Row { Period = "2026-10-09" };
        for (int k = 0; k < 500; k++) SessionEvents.Add(big.Events.PickedUp, "$item_mod_" + k, k + 1);
        big.Clip();
        Check(big.Keys == DayHistory.MaxRowKeys && big.Clipped && big.Events.PickedUp.ContainsKey("$item_mod_499") && !big.Events.PickedUp.ContainsKey("$item_mod_0"),
              "H clip: a day row keeps at most 250 keys, the largest, and says it was clipped");

        // ---------- size: a heavy year, and the worst case ----------
        var rng = new Random(7);
        var year = new LocalTotals { PlayerId = Id, Name = "Rowan" };
        var day0 = new DateTime(2026, 1, 5, 20, 0, 0);
        string[] pool = Enumerable.Range(0, 400).Select(k => "token" + k).ToArray();
        // 0.7: a big builder's per-token game counters (pieces placed, plants and fish picked): 300 piece kinds and 80 picked kinds,
        // modded names, some of them growing every evening; the rows keep their growth and the mark holds every kind
        string[] pieces = Enumerable.Range(0, 300).Select(k => "$piece_modded_building_piece_" + k).ToArray(), picks = Enumerable.Range(0, 80).Select(k => "Modded_Pickable_Plant_" + k).ToArray();
        var placedNow = new Dictionary<string, float>(); var pickedNow = new Dictionary<string, float>();
        var session = 0;
        for (var d = day0; d < day0.AddDays(365); d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Tuesday && d.DayOfWeek != DayOfWeek.Friday && d.DayOfWeek != DayOfWeek.Saturday) continue;   // three evenings a week
            var ev = new SessionEvents { Blocks = rng.Next(50, 300), Parries = rng.Next(5, 60) };
            var dmg = new DamageTally { HitsDealt = rng.Next(100, 900), HitsTaken = rng.Next(50, 400) };
            foreach (var k in pool.OrderBy(_ => rng.Next()).Take(rng.Next(150, 220)))
            {
                var bucket = rng.Next(6);
                if (bucket == 0) SessionEvents.Add(ev.PickedUp, "$item_" + k, rng.Next(1, 400));
                else if (bucket == 1) SessionEvents.Add(ev.Made, "$item_" + k, rng.Next(1, 30));
                else if (bucket == 2) dmg.Dealt[k + "|Swords|slash"] = rng.Next(10, 4000);
                else if (bucket == 3) dmg.Taken[k + "|EnemyHit|blunt"] = rng.Next(10, 2000);
                else if (bucket == 4) SessionEvents.Add(ev.ChopHits, k, rng.Next(1, 200));
                else SessionEvents.Add(ev.SkillPractice, k, rng.Next(1, 200) + 0.37f);
            }
            foreach (var k in pieces.OrderBy(_ => rng.Next()).Take(rng.Next(20, 60))) SessionEvents.Add(placedNow, k, rng.Next(1, 80));
            foreach (var k in picks.OrderBy(_ => rng.Next()).Take(rng.Next(10, 30))) SessionEvents.Add(pickedNow, k, rng.Next(1, 100));
            var gameNow = new Dictionary<string, float> { ["DistanceSail"] = session * 4000f, ["EnemyKills"] = session * 30f, ["CreatureTamed"] = session };
            foreach (var kv in placedNow) gameNow[DayHistory.PlacedPrefix + kv.Key] = kv.Value;
            foreach (var kv in pickedNow) gameNow[DayHistory.PickedPrefix + kv.Key] = kv.Value;
            year.Record("Y" + (session++), dmg, ev, null, gameNow, d);
        }
        var clock = Stopwatch.StartNew();
        var yearJson = year.ToJson(day0.AddDays(365));
        var write = clock.Elapsed.TotalMilliseconds; clock.Restart();
        var yearBack = LocalTotals.FromJson(yearJson);
        var read = clock.Elapsed.TotalMilliseconds;
        var historyBytes = yearJson.Length - new LocalTotals { PlayerId = Id, Name = "Rowan", PreviousEvents = year.PreviousEvents, PreviousDamage = year.PreviousDamage, LastEvents = year.LastEvents, LastDamage = year.LastDamage, Sessions = year.Sessions, LastSession = year.LastSession }.ToJson(day0).Length;
        var markKeys = year.History.GameMark.Count; var familyKeys = year.History.Rows.Sum(r => r.Game.Keys.Count(k => k.Contains("|")));
        System.Console.WriteLine($"INFO size: a heavy year (156 evenings, 150-220 kinds each; 0.7: 300 piece kinds and 80 picked kinds): history {historyBytes / 1024} KB in a {yearJson.Length / 1024} KB file, {year.History.Rows.Count} rows, {familyKeys} per-token keys in the rows, {markKeys} keys in the mark, {year.History.Rows.Count(r => r.IsDay && r.Clipped)} of {year.History.Rows.Count(r => r.IsDay)} day rows clipped; write {write:0.0} ms, read {read:0.0} ms");
        Check(year.History.GameMark.Keys.Count(k => k.StartsWith(DayHistory.PlacedPrefix) && k.Length > DayHistory.PlacedPrefix.Length) == 300 && familyKeys > 0,
              "H size: the heavy year's mark holds every piece kind and its rows the per-token growth (the size below includes both)");
        Check(historyBytes < 250 * 1024 && year.History.Rows.Count <= 88, "H size: a heavy year of play keeps the history under 250 KB (" + historyBytes / 1024 + " KB)");
        Check(yearBack != null && yearBack.History.Rows.Count == year.History.Rows.Count && yearBack.History.Rows.Zip(year.History.Rows, (a, b) => a.Period == b.Period && Math.Abs(Chops(a.Events) - Chops(b.Events)) < 0.01 && a.Damage.HitsDealt == b.Damage.HitsDealt).All(x => x),
              "H size: the year's history reads back row for row");
        var worst = new DayHistory { From = new DateTime(2020, 1, 1) };
        var wd = new DateTime(2026, 10, 9);
        foreach (var period in new[] { DayHistory.BeforeKey }.Concat(Enumerable.Range(0, DayHistory.MonthRows).Select(k => "m" + wd.AddMonths(-40 + k).ToString("yyyy-MM")))
                                 .Concat(Enumerable.Range(0, DayHistory.WeekRows).Select(k => DayHistory.WeekKey(wd.AddDays(-7 * (40 - k)))))
                                 .Concat(Enumerable.Range(0, DayHistory.DayRows).Select(k => DayHistory.DayKey(wd.AddDays(-34 + k)))))
        {
            var r = new DayHistory.Row { Period = period };
            for (int k = 0; k < DayHistory.CapOf(period); k++) r.Damage.Dealt["Some Modded Creature Name " + k + "|BloodMagic|lightning"] = 123456.7f + k;
            worst.Rows.Add(r);
        }
        // 0.7: and the mark of a big builder with modded names: 300 piece kinds and 80 picked kinds, beside the counters
        foreach (var k in pieces) worst.GameMark[DayHistory.PlacedPrefix + k + "_with_a_long_modded_suffix"] = 123456.7f;
        foreach (var k in picks) worst.GameMark[DayHistory.PickedPrefix + k + "_with_a_long_modded_suffix"] = 123456.7f;
        foreach (var st in DayHistory.GameStats) worst.GameMark[st] = 1234567.8f;
        var wj = new Json().Open(); worst.WriteTo(wj); var worstBytes = wj.Close().ToString().Length;
        var mj = new Json().Open().Dict("mark", worst.GameMark).Close().ToString().Length;
        System.Console.WriteLine($"INFO size: the worst case (86 full rows of long keys: 250 a day, 120 a week, 80 a month; a mark of {worst.GameMark.Count} keys, {mj / 1024} KB): {worstBytes / 1024} KB");
        Check(worstBytes < 1024 * 1024, "H size: the worst case (every row full, long modded names) stays under 1 MB (" + worstBytes / 1024 + " KB)");
        var shared = year.History.DealtByDay(day0.AddDays(364));
        var sharedJson = new Json().Open().Dict("dealtByDay", shared).Close().ToString();
        Check(shared.Count <= 30 && sharedJson.Length < 1024, "H share: the damage dealt per day of the last 30 days is under 1 KB (" + sharedJson.Length + " bytes, " + shared.Count + " days)");
        var rows30 = year.History.Rows.Where(r => r.IsDay && r.Start >= day0.AddDays(364 - 29)).ToList();
        var j30 = new Json().Open(); var tmp = new DayHistory(); tmp.Rows.AddRange(rows30); tmp.WriteTo(j30); var json30 = j30.Close().ToString();
        var packed = Transport.Pack(json30).Length;
        System.Console.WriteLine($"INFO share: the full rows of the last 30 days would be {json30.Length / 1024} KB of JSON, {packed / 1024} KB packed ({(packed + 2999) / 3000} parts of 3 000 bytes), sent to every fellow every 5 minutes; only dealtByDay is shared");
        Check(packed > sharedJson.Length * 10, "H share: the full rows cost far more than the one number per day that Together needs (" + packed + " vs " + sharedJson.Length + " bytes)");

        // ---------- the shared snapshot with a year of history: only the damage dealt per day is added ----------
        var lastDay = day0.AddDays(364);
        string Snap(IDictionary<string, float> byDay) => Snapshot.Build("0.6.1", Id, "Rowan", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", year.LastDamage, "s1", year.LastEvents, new EventLog(), true,
                                                                       SessionEvents.Sum(year.PreviousEvents, year.LastEvents), DamageTally.Sum(year.PreviousDamage, year.LastDamage), null, null, null, byDay);
        int without = Transport.Pack(Snap(null)).Length, with = Transport.Pack(Snap(year.History.DealtByDay(lastDay))).Length;
        System.Console.WriteLine($"INFO share: a year-old heavy player's snapshot is {without / 1024} KB packed without dealtByDay, {with / 1024} KB with it (+{with - without} bytes); res-server's cap is 600 KB packed (200 parts of 3 000 bytes)");
        Check(with - without < 1024 && with < 600 * 1024 / 4, "H share: with a year of history the snapshot grows by under 1 KB packed (+" + (with - without) + " bytes) and stays far under the 600 KB cap (" + with / 1024 + " KB)");

        // ---------- the file ----------
        var dir = Path.Combine(Path.GetTempPath(), "hw-history-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "local", Id + "-Rowan.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var old = new LocalTotals { PlayerId = Id, Name = "Rowan", FirstRunUtc = Day1.ToUniversalTime() }; old.Record("S1", Dmg(10), Ev(4));
            var oldJson = old.ToJson(Day1).Replace("\"schema\":" + LocalTotals.Schema, "\"schema\":2");   // as 0.6.1 wrote it, plus a key from a newer version
            File.WriteAllText(path, oldJson.Substring(0, oldJson.Length - 1) + ",\"fromTheFuture\":{\"x\":1}}");
            var loaded = LocalTotals.Load(path, Id, out var problem);
            Check(loaded != null && problem == null && loaded.History.Rows.Count == 0 && loaded.History.From == DateTime.MinValue, "H file: a 0.6.1 file (schema 2, no history) loads; its history is empty");
            loaded.Record("S1", Dmg(25), Ev(9), null, Game(300), Day1.AddMinutes(10));
            loaded.Save(path, Day1.ToUniversalTime());
            var text = File.ReadAllText(path);
            var again = LocalTotals.Load(path, Id, out _);
            Check(text.Contains("\"schema\":" + LocalTotals.Schema) && text.Contains("\"history\":{\"from\":\"2026-10-09\"") && text.Contains("\"fromTheFuture\":{\"x\":1}") &&
                  again.History.Rows.Count == 1 && Chops(again.History.Rows[0].Events) == 5 && Get(again.History.GameMark, "DistanceSail") == 300,
                  "H file: the first save starts the history from the session's growth since the old save (5 chops), keeps the newer version's unknown key, and reads back");
            var zero = new DayHistory(); zero.GameGrowth(new Dictionary<string, float> { ["DistanceSail"] = 0 }, true);
            var zj = new Json().Open(); zero.WriteTo(zj); var zBack = DayHistory.ReadFrom(MiniJson.Parse(zj.Close().ToString()) is Dictionary<string, object> zr ? MiniJson.Obj(zr, "history") : null);
            Check(zBack.GameMark.Count > 0 && zBack.GameGrowth(new Dictionary<string, float> { ["DistanceSail"] = 50 }, false)["DistanceSail"] == 50,
                  "H file: a mark of zeros is still a mark after a reload (a new character's first 50 m are booked, not lost)");
            // 0.7: a counter a later version keeps (CreatureTamed) or a per-token family (pieces placed) on a history that already runs: its first save books
            // nothing (never a lifetime count on one day) and says from which day it is kept; a token new to a kept family stood at 0; it all reads back
            var older = new DayHistory(); older.Add(Day1, null, null, null, new Dictionary<string, float> { ["DistanceSail"] = 100 });
            older.GameMark.Remove(DayHistory.PlacedPrefix); older.GameMark.Remove(DayHistory.PickedPrefix);   // as a 0.6 mark: no families
            var day2 = Day1.AddDays(1);
            older.Add(day2, null, null, null, new Dictionary<string, float> { ["DistanceSail"] = 150, ["CreatureTamed"] = 12, [DayHistory.PlacedPrefix + "$piece_woodwall"] = 900 });
            var b2 = older.Rows.Last();
            older.Add(day2.AddMinutes(5), null, null, null, new Dictionary<string, float> { ["DistanceSail"] = 150, ["CreatureTamed"] = 13, [DayHistory.PlacedPrefix + "$piece_woodwall"] = 904, [DayHistory.PlacedPrefix + "$piece_stonewall"] = 2 });
            var oj = new Json().Open(); older.WriteTo(oj); var oBack = DayHistory.ReadFrom(MiniJson.Parse(oj.Close().ToString()) is Dictionary<string, object> or ? MiniJson.Obj(or, "history") : null);
            Check(Get(b2.Game, "DistanceSail") == 50 && Get(b2.Game, "CreatureTamed") == 1 && Get(b2.Game, DayHistory.PlacedPrefix + "$piece_woodwall") == 4 && Get(b2.Game, DayHistory.PlacedPrefix + "$piece_stonewall") == 2 &&
                  older.KeptFrom("CreatureTamed", day2) == day2.Date && older.KeptFrom(DayHistory.PlacedPrefix, day2) == day2.Date && older.KeptFrom("DistanceSail", day2) == Day1.Date &&
                  oBack.KeptFrom(DayHistory.PlacedPrefix, day2) == day2.Date && oBack.GameMark.ContainsKey(DayHistory.PlacedPrefix),
                  "H record: a counter kept from a later version books nothing on its first save (not 12 tamed, not 900 walls on one day), then its growth; a new piece kind from 0; from when it is kept reads back");
            var messy = "{\"from\":\"2026-10-09\",\"rows\":[{\"p\":\"2026-10-10\",\"ev\":{\"chopHits\":{\"Beech1\":2}}},{\"p\":\"2026-10-09\",\"ev\":{\"chopHits\":{\"Beech1\":9}}},{\"p\":\"nonsense\"},{\"p\":\"2026-10-11\",\"ev\":{\"chopHits\":{\"Beech1\":1}}}]}";
            var m = DayHistory.ReadFrom(MiniJson.Parse(messy) as Dictionary<string, object>);
            Check(m.Rows.Select(r => r.Period).SequenceEqual(new[] { "2026-10-10", "2026-10-11" }), "H file: a row out of order or with an unreadable period is not trusted; the rest stays");
            Check(DayHistory.ReadFrom(null).Rows.Count == 0 && DayHistory.ReadFrom(new Dictionary<string, object> { ["rows"] = "x" }).Rows.Count == 0, "H file: a missing or odd history reads as empty, never an error");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        // ---------- REVIEW-07 #4: 0.7 -> 0.6.5 -> 0.7 keeps the 0.7-only day counters honest ----------
        {
            var g1 = new Dictionary<string, float> { ["DistanceSail"] = 100, [DayHistory.PlacedPrefix + "$piece_woodwall"] = 10, ["CreatureTamed"] = 2 };
            var v7 = new LocalTotals { PlayerId = Id, Name = "Rowan" };
            v7.Record("S1", null, Ev(1), null, g1, Day1);
            v7.Record("S1", null, Ev(1), null, new Dictionary<string, float>(g1) { [DayHistory.PlacedPrefix + "$piece_woodwall"] = 12 }, Day1.AddMinutes(30));
            v7.LastEvents.ChestFed["charcoal_kiln|$item_wood"] = 40; v7.Feats.SeenGroup = 3;
            var json7 = v7.ToJson(Day1.ToUniversalTime());
            // what 0.6.5 writes back a day later: it drops "began" inside history, "chestFed" inside measured and "seenGroup" inside feats, keeps
            // the 0.7-only marks frozen (it never moves them) and the unknown top-level keys as they were
            var root = (Dictionary<string, object>)MiniJson.Parse(json7);
            ((Dictionary<string, object>)root["history"]).Remove("began");
            ((Dictionary<string, object>)((Dictionary<string, object>)((Dictionary<string, object>)root["totals"])["lastSession"])["measured"]).Remove("chestFed");
            if (root.TryGetValue("feats", out var fo) && fo is Dictionary<string, object> fd) fd.Remove("seenGroup");
            root["saved"] = Day1.AddDays(1).ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture);
            var back7 = LocalTotals.FromJson(MiniJson.Write(root));
            var day4 = Day1.AddDays(3);   // 300 walls built in the 0.6.5 days; 0.7 again
            back7.Record("S3", null, Ev(1), null, new Dictionary<string, float>(g1) { [DayHistory.PlacedPrefix + "$piece_woodwall"] = 312, ["CreatureTamed"] = 5 }, day4);
            var row4 = back7.History.Rows.Last();
            Check(Get(row4.Game, DayHistory.PlacedPrefix + "$piece_woodwall") == 0 && Get(row4.Game, "CreatureTamed") == 0 &&
                  back7.History.KeptFrom(DayHistory.PlacedPrefix, day4) == day4.Date && back7.History.KeptFrom("CreatureTamed", day4) == day4.Date &&
                  (back7.LastEvents.ChestFed.TryGetValue("charcoal_kiln|$item_wood", out var cf) ? cf : back7.PreviousEvents.ChestFed.TryGetValue("charcoal_kiln|$item_wood", out var pf) ? pf : 0) == 40 && back7.Feats.SeenGroup == 3,
                  "H downgrade: after a 0.6.5 save in between, the walls and tames of the 0.6.5 days do not land on one 0.7 day, the windows count them from that day, and Stoker feeding and seen group tiers come back" +
                  " [walls " + Get(row4.Game, DayHistory.PlacedPrefix + "$piece_woodwall") + " tamed " + Get(row4.Game, "CreatureTamed") + " kept " + back7.History.KeptFrom(DayHistory.PlacedPrefix, day4).ToString("MM-dd") + "/" + back7.History.KeptFrom("CreatureTamed", day4).ToString("MM-dd") +
                  " chest " + (back7.LastEvents.ChestFed.Count + back7.PreviousEvents.ChestFed.Count) + " seen " + back7.Feats.SeenGroup + "]");
        }

        // ---------- shared per day ----------
        var sh = new DayHistory();
        sh.Add(Day1, null, Dmg(100), null, null);
        var tool = new DamageTally(); tool.Dealt["Beech1|WoodCutting|chop"] = 500; sh.Add(Day1.AddDays(1), null, tool, null, null);
        var pend = new DayHistory.Row { Period = DayHistory.DayKey(Day1.AddDays(1).Date) }; pend.Damage.Dealt["Neck|Bows|pierce"] = 20;
        var by = sh.DealtByDay(Day1.AddDays(1), 30, pend);
        Check(by.Count == 2 && by["2026-10-09"] == 100 && by["2026-10-10"] == 20, "H share: damage dealt per day, the eight battle types only (tool damage is not battle), today with the live part");
        return fails;
    }
}
