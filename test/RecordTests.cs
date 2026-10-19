// 0.9, the record track (work/hearthwoven-0.9/DESIGN.md section 1, TRACK-record.md): the four new records on your PC, without the game.
// U: upgrade moments (the hook's rule, the 2 000 bound and the fold per item, the file); FI: firsts (what counts as a first, the earliest minute,
// the local day); DS: deed stretches (closed by 10 quiet minutes, counts as a time split of the minutes, moments, the 30-day store per world, a busy
// evening's size); FS: feats seen per tier (the first-run migration from the old counts, marking, the file). Fictional player: Rowan.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Hearthwoven;

static class RecordTests
{
    const long Id = 4242424242L;
    const string World = "Midgard-123456789";
    static readonly DateTime T0 = new DateTime(2026, 10, 18, 19, 0, 0, DateTimeKind.Utc);   // 21:00 on a PC at UTC+2

    static string K(DateTime utc, string fam, string token) => EventLog.Bucket(utc) + "|" + fam + "|" + token;
    static string Kind(string t) => t.Contains("sword") || t.Contains("axe") || t.Contains("helmet") ? "gear" : t.Contains("stew") || t.Contains("meat") ? "food" : "other";
    static string Piece(string t) => t.Contains("feast") ? "feast" : t.Contains("hoe") ? "ground" : "built";
    static string Gather(string t) => t.Contains("wood") ? "wood" : t.Contains("ore") || t.Contains("stone") ? "mining" : null;

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- U: upgrade moments ----------
        Check(UpgradeJudge.NewLevel(false, "$item_sword_iron", 3, "$item_sword_iron", 2) == 3, "U1 the same item one level higher in its slot: an upgrade to level 3");
        Check(UpgradeJudge.NewLevel(true, "$item_sword_iron", 2, "$item_sword_iron", 2) == null, "U1 the old item still in the bag (a check failed, nothing crafted): no upgrade");
        Check(UpgradeJudge.NewLevel(false, null, 0, "$item_sword_iron", 2) == null && UpgradeJudge.NewLevel(false, "$item_wood", 1, "$item_sword_iron", 2) == null,
              "U1 the slot empty or holding something else (an upgrader broke the item): no upgrade");
        Check(UpgradeJudge.NewLevel(false, "$item_sword_iron", 1, "$item_sword_iron", 2) == null && UpgradeJudge.NewLevel(false, "$item_sword_iron", 2, "$item_sword_iron", 2) == null,
              "U1 an upgrader's failure (a level lost) or the same level: no upgrade");
        Check(UpgradeJudge.NewLevel(false, "", 3, "", 2) == null, "U1 no item token: nothing");

        var ul = new UpgradeLog { FromUtc = T0 };
        ul.Add(new UpgradeMoment { Token = "$item_sword_iron", From = 1, To = 2, Station = "forge", StationLevel = 3, Utc = T0.AddMinutes(5) });
        ul.Add(new UpgradeMoment { Token = "$item_helmet_iron", From = 2, To = 3, Station = "forge", StationLevel = 4, Utc = T0.AddMinutes(9) });
        ul.Add(new UpgradeMoment { Token = "$item_axe_bronze", From = 1, To = 2, Station = "forge", StationLevel = 2, Utc = T0.AddMinutes(7) });   // booked late: still in time order
        ul.Add(new UpgradeMoment { Token = "$item_axe_bronze", From = 2, To = 2, Utc = T0.AddMinutes(8) });   // not higher: ignored
        Check(ul.Recent.Select(m => m.Token).SequenceEqual(new[] { "$item_helmet_iron", "$item_axe_bronze", "$item_sword_iron" }) && ul.Total == 3,
              "U2 upgrades newest first, in time order, one that is not higher left out (3 kept)");

        var big = new UpgradeLog { FromUtc = T0 };
        for (int i = 0; i < 2600; i++) big.Add(new UpgradeMoment { Token = i % 4 == 0 ? "$item_sword_iron" : "$item_tool_" + (i % 3), From = 1 + i % 3, To = 2 + i % 3, Station = "forge", StationLevel = 3, Utc = T0.AddDays(-30).AddMinutes(i * 15) });
        var oldest600 = Enumerable.Range(0, 600).Select(i => (tok: i % 4 == 0 ? "$item_sword_iron" : "$item_tool_" + (i % 3), to: 2 + i % 3)).ToList();
        var foldSword = big.Folded.FirstOrDefault(f => f.Token == "$item_sword_iron");
        Check(big.Recent.Count == UpgradeLog.MaxRecent && big.Total == 2600 && foldSword != null && foldSword.Count == oldest600.Count(x => x.tok == "$item_sword_iron") &&
              foldSword.MaxLevel == oldest600.Where(x => x.tok == "$item_sword_iron").Max(x => x.to) && foldSword.First == T0.AddDays(-30).Date,
              "U3 past 2 000 the oldest fold into one line per item: count exact (" + big.Total + " in all), highest level, first day");
        var per = big.PerItem();
        Check(per.Sum(p => p.Count) == 2600 && per.First(p => p.Token == "$item_sword_iron").Count == 650, "U3 per item (whole and folded joined): 650 sword upgrades, 2 600 in all");
        var ulBack = UpgradeLog.ReadFrom(Obj(Wrap(j => big.WriteTo(j)), "upgrades"));
        Check(ulBack.Recent.Count == 2000 && ulBack.Total == 2600 && ulBack.FromUtc == T0 && ulBack.Recent[0].Station == "forge" && ulBack.Recent[0].StationLevel == 3 &&
              ulBack.Recent[0].Utc == big.Recent[0].Utc && ulBack.Folded.Count == big.Folded.Count, "U4 the log round-trips through JSON (whole, folded, station and its level, time to the second)");
        var junk = (Dictionary<string, object>)MiniJson.Parse("{\"recent\":[{\"t\":\"\",\"a\":1,\"b\":2,\"u\":\"2026-10-18T20:00:00Z\"},{\"t\":\"$x\",\"a\":3,\"b\":2,\"u\":\"2026-10-18T20:00:00Z\"},{\"t\":\"$y\",\"a\":1,\"b\":2}],\"folded\":[{\"t\":\"$z\",\"n\":0}]}");
        Check(UpgradeLog.ReadFrom(junk).Total == 0, "U4 unreadable entries (no token, not higher, no time, no count) are skipped");
        System.Console.WriteLine("     upgrades: 2 000 whole + folds = " + Wrap(j => big.WriteTo(j)).Length / 1024 + " KB in the totals file");

        // ---------- FI: firsts ----------
        var fb = new FirstsBook();
        Check(fb.Note(new Dictionary<string, float> { [K(T0, "made", "$item_axe_bronze")] = 1 }) == 0, "FI1 nothing is a first before recording begins");
        Check(fb.Begin(T0, new Dictionary<string, float> { ["$item_club"] = 3, ["$item_torch"] = 0 }, new Dictionary<string, float> { ["$piece_woodwall"] = 40 }) && !fb.Begin(T0.AddDays(1)),
              "FI1 recording begins once");
        var mins = new Dictionary<string, float>
        {
            [K(T0.AddMinutes(12), "made", "$item_axe_bronze")] = 1, [K(T0.AddMinutes(3), "made", "$item_axe_bronze")] = 1,   // its earliest minute wins
            [K(T0.AddMinutes(4), "made", "$item_club")] = 1,             // made before 0.9 (the game's counter): no first
            [K(T0.AddMinutes(5), "made", "$item_torch")] = 1,            // stood at 0 in the game's counter: a first
            [K(T0.AddMinutes(6), "placed", "$piece_woodwall")] = 2,      // placed before: no first
            [K(T0.AddMinutes(7), "placed", "$piece_bench")] = 1,
            [K(T0.AddMinutes(8), "pickedUp", "$item_wood")] = 10,        // another family: no first
            [K(T0.AddMinutes(9), "made", DeedLog.OtherToken)] = 3,       // the capped rest: no first
            [K(T0.AddMinutes(-30), "made", "$item_spear")] = 1,          // a minute before recording began
        };
        Check(fb.Note(mins, t => t.AddHours(2)) == 3 && fb.Marks["$item_axe_bronze"].Utc == T0.AddMinutes(3) && fb.IsFirst("$item_torch") && fb.IsFirst("$piece_bench") &&
              !fb.IsFirst("$item_club") && !fb.IsFirst("$piece_woodwall") && !fb.IsFirst("$item_wood") && !fb.IsFirst("$item_spear"),
              "FI2 a first: made or placed, not before 0.9, its earliest minute (3 firsts: bronze axe at 19:03, torch, bench)");
        var late = new FirstsBook(); late.Begin(T0);
        late.Note(new Dictionary<string, float> { [K(new DateTime(2026, 10, 18, 22, 30, 0, DateTimeKind.Utc), "made", "$item_stew")] = 1 }, t => t.AddHours(2));
        Check(late.Days()["$item_stew"] == new DateTime(2026, 10, 19), "FI3 the first's day is the local day (22:30 UTC is 00:30 the next day at UTC+2)");
        Check(fb.Note(mins, t => t.AddHours(2)) == 0 && fb.Marks["$item_axe_bronze"].Utc == T0.AddMinutes(3), "FI3 one mark per token, kept: noting again changes nothing");
        var fbBack = FirstsBook.ReadFrom(Obj(Wrap(j => fb.WriteTo(j)), "firsts"));
        Check(fbBack.FromUtc == T0 && fbBack.Marks.Count == 3 && fbBack.Before.SetEquals(new[] { "$item_club", "$piece_woodwall" }) && fbBack.Marks["$item_torch"].Utc == T0.AddMinutes(5) &&
              fbBack.Days()["$item_torch"] == T0.AddHours(2).Date, "FI4 firsts round-trip through JSON (marks with day and minute, what was there before)");
        var many = new FirstsBook(); many.Begin(T0);
        var flood = new Dictionary<string, float>(); for (int i = 0; i < 5000; i++) flood[K(T0.AddMinutes(1 + i % 100), "made", "$item_mod" + i)] = 1;
        many.Note(flood);
        Check(many.Marks.Count == FirstsBook.MaxMarks, "FI5 bounded: at most " + FirstsBook.MaxMarks + " firsts (a mod with 5 000 items)");

        // ---------- DS: deed stretches ----------
        Check(DeedStretches.FamilyOf("made", "$item_sword_iron", Kind, Piece, Gather) == "crafting" && DeedStretches.FamilyOf("made", "$item_stew", Kind, Piece, Gather) == "cooking" &&
              DeedStretches.FamilyOf("placed", "$piece_feast_meadows", Kind, Piece, Gather) == "cooking" && DeedStretches.FamilyOf("placed", "$piece_hoe", Kind, Piece, Gather) == "groundwork" &&
              DeedStretches.FamilyOf("pickedUp", "$item_wood", Kind, Piece, Gather) == "woodcutting" && DeedStretches.FamilyOf("pickedUp", "$item_flint", Kind, Piece, Gather) == "broughtin" &&
              DeedStretches.FamilyOf("picked", "$animal_fish1", Kind, Piece, Gather) == "fishing" && DeedStretches.FamilyOf("picked", "Barley", Kind, Piece, Gather) == "farming" &&
              DeedStretches.FamilyOf("kills", "$enemy_troll", Kind, Piece, Gather) == null && DeedStretches.FamilyOf("counters", "EnemyKills", Kind, Piece, Gather) == null &&
              DeedStretches.FamilyOf("counters", "FishCaught", Kind, Piece, Gather) == null && DeedStretches.FamilyOf("skillPractice", "Swords", Kind, Piece, Gather) == "practice" &&
              DeedStretches.FamilyOf("counters", "CreatureTamed", Kind, Piece, Gather) == "taming" && DeedStretches.FamilyOf("counters", "DistanceSail", Kind, Piece, Gather) == "sailing" &&
              DeedStretches.FamilyOf("counters", DeedLog.CargoTotal, Kind, Piece, Gather) == "sailing",
              "DS1 each deed goes to its family's stretches (made: Crafting or Cooking; a feast set out: Cooking; 0.9 all-feed: other pickups Brought in, skill practice, tamed, sailed and cargo; kills and the kill and fish counters: none, the fights and Fishing's fish say them)");
        var b = new Dictionary<string, float>
        {
            // crafting 19:05..19:08, quiet 9 minutes, 19:18 (same stretch), quiet 12 minutes, 19:31 (a new one)
            [K(T0.AddMinutes(5), "made", "$item_sword_iron")] = 1, [K(T0.AddMinutes(6), "made", "$item_bronzenails")] = 20, [K(T0.AddMinutes(8), "made", "$item_helmet_iron")] = 1,
            [K(T0.AddMinutes(18), "made", "$item_bronzenails")] = 10,
            [K(T0.AddMinutes(31), "made", "$item_axe_bronze")] = 2,
            // cooking at the same time: its own stretch; a feast set out
            [K(T0.AddMinutes(6), "made", "$item_stew")] = 3, [K(T0.AddMinutes(7), "placed", "$piece_feast_meadows")] = 1,
            // building
            [K(T0.AddMinutes(40), "placed", "$piece_woodwall")] = 30, [K(T0.AddMinutes(41), "placed", "$piece_bench")] = 1,
            // not a deed feed
            [K(T0.AddMinutes(10), "kills", "$enemy_troll")] = 1, [K(T0.AddMinutes(10), "skillPractice", "Swords")] = 4,
        };
        var ups = new[] { new UpgradeMoment { Token = "$item_sword_iron", From = 1, To = 2, Station = "forge", StationLevel = 3, Utc = T0.AddMinutes(8).AddSeconds(20) },
                          new UpgradeMoment { Token = "$item_shield_wood", From = 2, To = 3, Station = "piece_workbench", StationLevel = 2, Utc = T0.AddMinutes(55) } };   // an upgrade alone: a stretch of its own
        var firsts = new FirstsBook(); firsts.Begin(T0.AddMinutes(-1)); firsts.Note(b);
        var ds = DeedStretches.Build(World, "s1", T0, T0.AddMinutes(70), b, ups, firsts, Kind, Piece, Gather);
        var craft = ds.Stretches.Where(s => s.Family == "crafting").OrderBy(s => s.FromUtc).ToList();
        Check(craft.Count == 3 && craft[0].FromUtc == T0.AddMinutes(5) && craft[0].ToUtc == T0.AddMinutes(19) && craft[1].FromUtc == T0.AddMinutes(31) && craft[2].FromUtc == T0.AddMinutes(55) &&
              ds.Stretches.Count(s => s.Family == "cooking") == 1 && ds.Stretches.Count(s => s.Family == "building") == 1 && ds.Stretches.All(s => s.Family != null),
              "DS2 a stretch = the minutes a family grew, closed by 10 quiet minutes (9 quiet: one stretch, 12: a new one); families apart; an upgrade alone is a crafting stretch");
        Check(craft[0].Counts["made|$item_bronzenails"] == 30 && craft[0].Counts["made|$item_sword_iron"] == 1 && ds.Stretches.Sum(s => s.Counts.Values.Sum()) == b.Where(kv => !kv.Key.Contains("kills")).Sum(kv => kv.Value),
              "DS3 counts are a time split of the minutes: every deed in a feed counted once, in its stretch (30 nails over two bursts of one stretch)");
        var m0 = craft[0].Moments;
        Check(m0.Count(x => x.Kind == "made") == 2 && m0.Any(x => x.Kind == "upgraded" && x.Token == "$item_sword_iron" && x.From == 1 && x.To == 2 && x.Station == "forge" && x.StationLevel == 3) &&
              m0.Count(x => x.Kind == "first") == 3 && !m0.Any(x => x.Token == "$item_bronzenails" && x.Kind == "made") && m0[0].Utc >= m0[m0.Count - 1].Utc,
              "DS4 moments: gear made (not materials), the upgrade with its levels and station, the firsts of the stretch; newest first");
        var cook = ds.Stretches.First(s => s.Family == "cooking");
        Check(cook.Moments.Any(x => x.Kind == "feast" && x.Token == "$piece_feast_meadows") && !cook.Moments.Any(x => x.Kind == "made"), "DS4 a feast set out is a Cooking moment; a dish is counted, not a moment");
        var wide = new Dictionary<string, float>();
        for (int i = 0; i < 60; i++) wide[K(T0.AddMinutes(1 + i % 5), "placed", "$piece_kind" + i)] = 1 + i;
        wide[K(T0.AddMinutes(2), "placed", DeedLog.OtherToken)] = 7;
        var wd = DeedStretches.Build(World, "s2", T0, T0.AddMinutes(30), wide, null, null, Kind, Piece, Gather).Stretches.Single();
        Check(wd.Counts.Count == DeedStretches.MaxCounts + 1 && wd.Counts["placed|~other"] == Enumerable.Range(0, 20).Sum(i => 1 + i) + 7 && wd.Counts.Values.Sum() == wide.Values.Sum() &&
              wd.Counts.ContainsKey("placed|$piece_kind59"), "DS5 a stretch keeps its largest 40 kinds by name, the rest in ~other: its sum stays exact");
        var fold = new Dictionary<string, float> { [K(T0.AddMinutes(20), "chopHits", "Beech1")] = 40, [K(T0.AddMinutes(34), "chopHits", "Beech1")] = 10 };
        var fs1 = DeedStretches.Build(World, "s3", T0, T0.AddMinutes(20 + EventLog.FoldAfterMinutes + 30), fold, null, null, Kind, Piece, Gather);
        Check(fs1.Stretches.Count == 1 && fs1.Stretches[0].ToUtc == T0.AddMinutes(35), "DS6 a folded ten-minute bucket covers its ten minutes (19:20 folded, then 19:34: one stretch, 4 quiet minutes)");

        var sjson = Wrap(j => { j.Key("x"); ds.WriteTo(j); });
        var back = DeedSession.ReadFrom((Dictionary<string, object>)((Dictionary<string, object>)MiniJson.Parse(sjson))["x"]);
        Check(back != null && back.Stretches.Count == ds.Stretches.Count && back.Stretches.Zip(ds.Stretches, (x, y) => x.Family == y.Family && x.FromUtc == y.FromUtc && x.ToUtc == y.ToUtc &&
              x.Counts.Count == y.Counts.Count && x.Moments.Count == y.Moments.Count).All(v => v) && back.Stretches.SelectMany(s => s.Moments).Any(x => x.Station == "forge" && x.To == 2),
              "DS7 a session round-trips through its JSON object (the form a block can reuse): families, times, counts, moments");
        Check(DeedSession.ReadFrom((Dictionary<string, object>)MiniJson.Parse("{\"id\":\"\",\"from\":\"2026-10-18T19:00:00Z\",\"to\":\"2026-10-18T20:00:00Z\"}")) == null &&
              DeedSession.ReadFrom((Dictionary<string, object>)MiniJson.Parse("{\"id\":\"a\",\"from\":\"2026-10-18T19:00:00Z\",\"to\":\"2026-10-18T20:00:00Z\",\"s\":[{\"f\":\"\",\"a\":0,\"b\":60},{\"f\":\"crafting\",\"a\":60,\"b\":0},{\"f\":\"crafting\",\"a\":0,\"b\":60,\"c\":{\"bad\":3,\"made|$x\":-1,\"made|$y\":2}}]}")).Stretches.Single().Counts.Count == 1,
              "DS7 the reader refuses a session without id and skips a stretch without family, one ending before it starts, and counts that are not \"family|token\" above 0");

        // the store: 30 days per world, other worlds and characters apart
        var dir = Path.Combine(Path.GetTempPath(), "hw-deeds-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var path = KeptDeeds.PathFor(LocalTotals.PathFor(dir, Id, "Rowan"));
        var kd = KeptDeeds.Load(path, Id, "Rowan", out var problem);
        Check(kd.Sessions.Count == 0 && problem == null && path.EndsWith("-Rowan.deeds.json"), "DS8 no file: an empty book, the file beside the totals (<id>-<name>.deeds.json)");
        var now = T0.AddDays(40);
        DeedSession Sess(string world, string id, DateTime at) => DeedStretches.Build(world, id, at, at.AddHours(1), new Dictionary<string, float> { [K(at.AddMinutes(5), "made", "$item_sword_iron")] = 1 }, null, null, Kind, Piece, Gather);
        for (int d = 39; d >= 0; d--) kd.Record(Sess(World, "d" + d, now.AddDays(-d).AddHours(-2)), now.AddDays(-d));
        kd.Record(Sess("Ashlands-987", "x1", now.AddHours(-30)), now);
        kd.Record(Sess(World, "empty", now.AddHours(-1)).Also(s => s.Stretches.Clear()), now);
        var earlier = kd.Earlier(World, "d0", now);
        Check(earlier.Count == 29 && earlier.All(s => s.ToUtc >= now - KeptDeeds.MaxAge) && earlier[0].Id == "d1" && kd.Earlier("Ashlands-987", "", now).Count == 1 && earlier.All(s => s.Id != "empty"),
              "DS8 per world, newest first, only stretches of the last 30 days (29 earlier evenings), another world apart, a session without a stretch not kept");
        kd.Record(Sess(World, "d0", now.AddHours(-2)).Also(s => s.Stretches[0].Counts["made|$item_sword_iron"] = 5), now);
        Check(kd.Sessions.Count(s => s.Id == "d0") == 1 && kd.Sessions.First(s => s.Id == "d0").Stretches[0].Counts["made|$item_sword_iron"] == 5, "DS8 a later save of a session replaces its record");
        for (int w = 0; w < 6; w++) kd.Record(Sess("World" + w, "w" + w, now.AddMinutes(w)), now.AddMinutes(w));
        Check(kd.Sessions.Select(s => s.World).Distinct().Count() == KeptDeeds.MaxWorlds && kd.Sessions.Any(s => s.World == "World5") && !kd.Sessions.Any(s => s.World == World),
              "DS8 at most 4 worlds, the ones played last");
        kd.PlayerId = Id.ToString(); kd.Name = "Rowan";
        kd.Save(path, now);
        var kdBack = KeptDeeds.Load(path, Id, "Rowan", out problem);
        Check(problem == null && kdBack.Sessions.Count == kd.Sessions.Count && KeptDeeds.Load(path, Id, "Edda", out _).Sessions.Count == 0, "DS9 the file round-trips; another character's file is not read");
        File.WriteAllText(path, "{ not json");
        var fromBak = KeptDeeds.Load(path, Id, "Rowan", out problem);
        Check(fromBak.Sessions.Count == 0 || problem == null, "DS9 an unreadable file never throws (its .bak or an empty book)");

        // a busy evening: 3 hours, five families, 30 upgrades, 12 firsts; and 30 such evenings in the file
        var busy = new Dictionary<string, float>(); var rnd = new Random(7);
        for (int m = 0; m < 180; m++)
        {
            var t = T0.AddMinutes(m);
            if (m % 50 < 30) for (int k = 0; k < 6; k++) busy[K(t, "placed", "$piece_kind" + rnd.Next(45))] = rnd.Next(1, 6);
            if (m % 40 < 15) for (int k = 0; k < 3; k++) busy[K(t, "made", (k == 0 ? "$item_sword_" : "$item_mat") + rnd.Next(25))] = rnd.Next(1, 4);
            if (m % 60 < 20) { busy[K(t, "chopHits", "Beech" + rnd.Next(5))] = rnd.Next(5, 20); busy[K(t, "pickedUp", "$item_wood")] = rnd.Next(5, 30); }
            if (m % 70 < 10) busy[K(t, "made", "$item_stew" + rnd.Next(4))] = rnd.Next(1, 5);
            if (m % 90 < 12) busy[K(t, "pickaxeHits", "rock" + rnd.Next(4))] = rnd.Next(4, 12);
        }
        var busyUps = Enumerable.Range(0, 30).Select(i => new UpgradeMoment { Token = "$item_sword_" + i % 8, From = 1 + i % 3, To = 2 + i % 3, Station = "forge", StationLevel = 4, Utc = T0.AddMinutes(i * 6 + 1) }).ToList();
        var busyFirsts = new FirstsBook(); busyFirsts.Begin(T0.AddDays(-1)); busyFirsts.Note(busy.Where(kv => kv.Key.Contains("$item_sword_") || kv.Key.Contains("$piece_kind1")).Take(12));
        var sw = Stopwatch.StartNew();
        var evening = DeedStretches.Build(World, "busy", T0, T0.AddHours(3), busy, busyUps, busyFirsts, Kind, Piece, Gather);
        var buildMs = sw.Elapsed.TotalMilliseconds;
        var eveningJson = Wrap(j => { j.Key("x"); evening.WriteTo(j); });
        var month = new KeptDeeds { PlayerId = Id.ToString(), Name = "Rowan" };
        for (int d = 0; d < 30; d++) { var e = DeedSession.ReadFrom((Dictionary<string, object>)((Dictionary<string, object>)MiniJson.Parse(eveningJson))["x"]); e.Id = "e" + d; e.FromUtc = e.FromUtc.AddDays(-d); e.ToUtc = e.ToUtc.AddDays(-d); foreach (var st in e.Stretches) { st.FromUtc = st.FromUtc.AddDays(-d); st.ToUtc = st.ToUtc.AddDays(-d); } month.Record(e, T0.AddHours(4)); }
        var monthJson = month.ToJson(T0);
        sw.Restart(); var monthBack = KeptDeeds.FromJson(monthJson); var loadMs = sw.Elapsed.TotalMilliseconds;
        System.Console.WriteLine("     a busy evening (3 h, " + busy.Count + " minute keys, 30 upgrades): " + evening.Stretches.Count + " stretches, " + evening.Stretches.Sum(s => s.Moments.Count) + " moments, " +
                          eveningJson.Length + " bytes JSON (" + Transport.Pack(eveningJson).Length + " packed), built in " + buildMs.ToString("0.0") + " ms; 30 such evenings: " +
                          monthJson.Length / 1024 + " KB file, read in " + loadMs.ToString("0") + " ms");
        Check(evening.Stretches.Count > 3 && eveningJson.Length < 48 * 1024 && monthBack.Sessions.Count == 30 && monthJson.Length < 1536 * 1024,
              "DS10 a busy evening stays small (under 48 KB, a block's size) and 30 of them read back whole (under 1.5 MB)");

        // ---------- FS: feats seen per tier ----------
        Func<string, bool> isGroup = id => id.StartsWith("g_");
        FeatMoment At(int day) => new FeatMoment { Utc = T0.AddDays(day), Biome = "Meadows" };
        var fl = new FeatsLedger();
        fl.Earn("a", 2, At(-10)); fl.Earned["a"][1] = At(-2);        // a:1 ten days ago, a:2 two days ago
        fl.Earn("b", 1, At(-1));                                       // b:1 yesterday
        fl.Earn("c", 1, new FeatMoment { Before = true });             // earned before Hearthwoven counted: no date
        fl.Earn("g_x", 1, At(-3)); fl.Earn("g_y", 1, At(-4));
        fl.Seen = 2;                                                   // old count: 4 own tiers, 2 seen -> 2 unseen
        fl.SeenGroup = 1;                                              // 2 group tiers, 1 seen -> 1 unseen
        Check(fl.SeenTiers == null && fl.IsSeen("b", 1), "FS1 not recorded yet (a file before 0.9): nothing is called new");
        Check(fl.MigrateSeen(isGroup) && !fl.MigrateSeen(isGroup), "FS1 the migration runs once");
        Check(fl.UnseenTiers(isGroup, false).Select(x => x.id + ":" + x.tier).SequenceEqual(new[] { "b:1", "a:2" }) && fl.UnseenTiers(isGroup, true).Select(x => x.id + ":" + x.tier).SequenceEqual(new[] { "g_x:1" }) &&
              fl.IsSeen("a", 1) && fl.IsSeen("c", 1) && fl.UnseenOf(isGroup, false) == 2,
              "FS1 first run: the N most recently earned tiers stay New (N = the old count: own 2, group 1), newest first; older and undated ones are seen");
        var nodates = new FeatsLedger(); nodates.Earn("a", 2, new FeatMoment { Before = true }); nodates.Earn("b", 1, new FeatMoment { Noticed = true, Utc = T0 });
        nodates.MigrateSeen(isGroup);
        Check(nodates.UnseenTiers(isGroup, false).Count == 0 && nodates.SeenTiers.Count == 3, "FS1 no earned dates: none New");
        Check(fl.MarkTierSeen("b", 1, isGroup) && !fl.MarkTierSeen("b", 1, isGroup) && !fl.MarkTierSeen("b", 2, isGroup) && fl.Seen == 3 && fl.UnseenOf(isGroup, false) == 1,
              "FS2 opening a card marks that tier seen (once; a tier not earned never), and the old count follows (an older Hearthwoven's dot agrees)");
        fl.Earn("b", 2, At(0));
        Check(!fl.IsSeen("b", 2) && fl.UnseenTiers(isGroup, false)[0].id == "b" && fl.UnseenTiers(isGroup, false)[0].tier == 2, "FS2 a tier earned later is New, first in the list");
        fl.MarkSeenOf(isGroup, true);
        Check(fl.IsSeen("g_x", 1) && fl.UnseenTiers(isGroup, true).Count == 0 && fl.UnseenTiers(isGroup, false).Count == 2, "FS2 opening Together (the 0.8.6 rule) marks every group tier seen, the own ones stay");
        var tot = new LocalTotals { PlayerId = Id, Name = "Rowan", Feats = fl, Upgrades = ul, Firsts = fb };
        var totBack = LocalTotals.FromJson(tot.ToJson(T0));
        Check(totBack.Feats.SeenTiers.SetEquals(fl.SeenTiers) && totBack.Feats.Seen == fl.Seen && totBack.Upgrades.Total == 3 && totBack.Firsts.Marks.Count == 3 && totBack.Extra.Count == 0,
              "FS3 the feats seen, upgrades and firsts round-trip through the totals file (top-level keys, known to this reader)");
        var older = LocalTotals.FromJson(tot.ToJson(T0).Replace("\"featsSeen\"", "\"featsSeenX\""));
        Check(older.Feats.SeenTiers == null, "FS3 a file without featsSeen (saved by an older Hearthwoven) runs the migration again: not recorded until then");
        var stale = new LocalTotals { PlayerId = Id, Feats = new FeatsLedger() }; stale.Feats.Earn("a", 1, At(0)); stale.Feats.SeenTiers = new HashSet<string> { "a:1", "a:3", "gone:1", "junk" };
        Check(LocalTotals.FromJson(stale.ToJson(T0)).Feats.SeenTiers.SetEquals(new[] { "a:1" }), "FS3 only tiers still earned are kept (one per earned tier)");

        // ---------- PI: the panel's fields (DESIGN.md section 3): a fellow's copy carries none of the four records (nothing new leaves the PC) ----------
        var fellow = Hearthwoven.Panel.PanelInput.FromSnapshot("{\"name\":\"Edda\",\"playerId\":7,\"upgrades\":{\"recent\":[]},\"firsts\":{},\"featsSeen\":[\"a:1\"]}");
        Check(fellow != null && fellow.Upgrades == null && fellow.UpgradesFrom == null && fellow.Firsts == null && fellow.FirstsFrom == null && fellow.FeatsSeen == null && fellow.DeedStretches == null,
              "PI a fellow's copy: Upgrades, Firsts, FeatsSeen and DeedStretches are null (not recorded on that book), whatever the copy holds");

        // ---------- MC: meal combinations (0.9, Meals' Your combinations; src/MealCombos.cs) ----------
        Check(MealCombos.KeyOf(new[] { "QueensJam", "CarrotSoup", "Honey" }) == "CarrotSoup+Honey+QueensJam" && MealCombos.KeyOf(new[] { "Honey", "Honey", "" , null }) == "Honey"
              && MealCombos.KeyOf(new string[0]) == null && MealCombos.KeyOf(new[] { "A+B" }) == "A_B" && MealCombos.KeyOf(new[] { "D", "C", "B", "A" }) == "A+B+C",
              "MC1 a combination is its foods sorted, each once, at most three; a '+' in a name cannot split it; no food: none");
        var day0 = new DateTime(2026, 10, 18);
        var mc = new MealCombos { FromUtc = T0 };
        Check(!mc.Add(new string[0], 1, 1, 0, day0) && mc.Total == 0, "MC1 a meal with no food on: nothing");
        mc.Add(new[] { "CarrotSoup", "Honey" }, 23, 80, 0, day0); mc.Add(new[] { "Honey", "CarrotSoup" }, 23, 80, 0, day0.AddDays(1)); mc.Add(new[] { "Bread" }, 23, 70, 0, day0.AddDays(1));
        var soup = mc.Combos["CarrotSoup+Honey"];
        Check(mc.Combos.Count == 2 && soup.Count == 2 && soup.First == day0 && soup.Last == day0.AddDays(1) && soup.Days[day0] == 1 && soup.Days[day0.AddDays(1)] == 1 && soup.Health == 23 && soup.Stamina == 80 && mc.Total == 3,
              "MC2 per combination its meals, first and last day, the meals per day and the food values summed");
        var b1 = mc.Between(day0.AddDays(1), day0.AddDays(1));
        Check(b1.Count == 2 && b1["CarrotSoup+Honey"] == 1 && b1["Bread"] == 1 && mc.Between(day0.AddDays(5), day0.AddDays(9)).Count == 0, "MC2 a day window counts its days only");
        mc.Add(new[] { "Bread" }, 23, 70, 0, day0.AddDays(MealCombos.KeepDays + 3));
        Check(mc.Combos["Bread"].Days.Count == 1 && mc.Combos["Bread"].Count == 2 && mc.Combos["Bread"].First == day0.AddDays(1), "MC3 the days older than " + MealCombos.KeepDays + " fall away; the count and first day stay");
        var capMc = new MealCombos { FromUtc = T0 };
        for (int k = 0; k < MealCombos.MaxCombos + 20; k++) capMc.Add(new[] { "Food" + k }, 5, 5, 0, day0);
        Check(capMc.Combos.Count == MealCombos.MaxCombos && capMc.Combos.ContainsKey(DeedLog.OtherToken) && capMc.Combos[DeedLog.OtherToken].Count == 21 && capMc.Total == MealCombos.MaxCombos + 20 && capMc.Combos[DeedLog.OtherToken].Foods.Length == 0,
              "MC3 bounded: at most " + MealCombos.MaxCombos + " combinations, the rest one other line; every meal still counted");
        var mcBack = MealCombos.ReadFrom(Obj(Wrap(j => mc.WriteTo(j)), "mealCombos"));
        Check(mcBack.FromUtc == T0 && mcBack.Total == mc.Total && mcBack.Combos["CarrotSoup+Honey"].Days.Count == 2 && mcBack.Combos["Bread"].Stamina == 70 && mcBack.Combos["Bread"].First == day0.AddDays(1),
              "MC4 the file round-trips (start, counts, days, food values)");
        var capBack = MealCombos.ReadFrom(Obj(Wrap(j => capMc.WriteTo(j)), "mealCombos"));
        Check(capBack.Combos.Count == MealCombos.MaxCombos && capBack.Total == capMc.Total, "MC4 the capped record reads back whole, its other line too");
        var odd = MealCombos.ReadFrom((Dictionary<string, object>)MiniJson.Parse("{\"from\":\"x\",\"combos\":[{\"k\":\"B+A\",\"n\":3,\"first\":\"2026-10-18\",\"last\":\"2026-10-18\"},{\"k\":\"A\",\"n\":-2,\"first\":\"2026-10-18\",\"last\":\"2026-10-18\"},{\"k\":\"C\",\"n\":2,\"first\":\"nope\",\"last\":\"2026-10-18\"},{\"k\":\"D\",\"n\":1,\"first\":\"2026-10-18\",\"last\":\"2026-10-18\",\"h\":-5,\"days\":{\"2026-10-18\":1,\"bad\":4}}]}"));
        Check(!odd.Begun && odd.Combos.Count == 1 && odd.Combos["D"].Health == 0 && odd.Combos["D"].Days.Count == 1 && MealCombos.ReadFrom(null).IsEmpty,
              "MC4 anything unreadable is skipped (a key this writer would not make, no meals, no day, a negative value), never an error");
        var totMc = LocalTotals.FromJson(new LocalTotals { PlayerId = Id, Name = "Rowan", Combos = mc }.ToJson(T0));
        Check(totMc.Combos.Total == mc.Total && totMc.Extra.Count == 0 && new LocalTotals { PlayerId = Id }.ToJson(T0).IndexOf("mealCombos", StringComparison.Ordinal) < 0,
              "MC5 kept in the totals file under top-level mealCombos (known to this reader), left out while empty");
        var fellowMc = Hearthwoven.Panel.PanelInput.FromSnapshot("{\"name\":\"Edda\",\"playerId\":7,\"mealCombos\":{\"combos\":[]}}");
        Check(fellowMc != null && fellowMc.MealCombos == null, "MC5 a fellow's copy carries no combinations (never shared)");

        // ---------- RV: review fixes ----------
        // RV1 a long-time player cooks a sausage: made at stations for years (never in the item-craft counter, made before Hearthwoven counted), the
        // recipe known: not a first
        var known = new[] { "$item_sausage", "$item_cookedmeat", "$piece_cookingstation", "$item_wood" };
        var veteran = new FirstsBook(); veteran.Begin(T0, new Dictionary<string, float> { ["$item_club"] = 1 });
        Check(veteran.SeedKnown(known) && !veteran.SeedKnown(known) && veteran.KnownSeeded, "RV1 the known recipes and materials are seeded once");
        var sausage = new Dictionary<string, float> { [K(T0.AddMinutes(5), "made", "$item_sausage")] = 4, [K(T0.AddMinutes(6), "made", "$item_sword_iron")] = 1 };
        Check(veteran.Note(sausage) == 1 && !veteran.IsFirst("$item_sausage") && veteran.IsFirst("$item_sword_iron") && !veteran.FromKnowledge("$item_sword_iron"),
              "RV1 a long-time player cooking a sausage (recipe known, never in the craft counter): no first; a sword never known is one");
        // a book that gave the sausage a first before the seed (the 0.8.91 test build): kept as "first recorded" on your book, never in a block
        var early = new FirstsBook(); early.Begin(T0); early.Note(sausage);
        var earlySession = DeedStretches.Build(World, "e1", T0, T0.AddMinutes(30), sausage, null, early, Kind, Piece, Gather);
        var keptEarly = new KeptDeeds(); keptEarly.Record(earlySession, T0.AddMinutes(30));
        early.SeedKnown(known);
        Check(early.FromKnowledge("$item_sausage") && early.IsFirst("$item_sausage") && keptEarly.FlagKnownFirsts(early) && !keptEarly.FlagKnownFirsts(early) &&
              keptEarly.Sessions[0].Stretches.SelectMany(s => s.Moments).Single(m => m.Kind == "first" && m.Token == "$item_sausage").Known,
              "RV1 a first given before the seed stays (first recorded) and is flagged as from before-0.9 knowledge, in the kept sessions too");
        var rebuilt = DeedStretches.Build(World, "e1", T0, T0.AddMinutes(30), sausage, null, early, Kind, Piece, Gather);
        var block = DeedBlockPart.Clean(rebuilt);
        var blockBack = DeedSession.ReadFrom(Obj(Wrap(j => { j.Key("x"); keptEarly.Sessions[0].WriteTo(j); }), "x"));
        Check(rebuilt.Stretches.SelectMany(s => s.Moments).Any(m => m.Kind == "first" && m.Token == "$item_sausage" && m.Known) &&
              !block.Stretches.SelectMany(s => s.Moments).Any(m => m.Kind == "first" && m.Token == "$item_sausage") &&
              block.Stretches.SelectMany(s => s.Moments).Any(m => m.Kind == "first" && m.Token == "$item_sword_iron") &&
              !DeedBlockPart.Clean(blockBack).Stretches.SelectMany(s => s.Moments).Any(m => m.Kind == "first" && m.Token == "$item_sausage"),
              "RV1 a first from before-0.9 knowledge never goes into a friend's feed block (also after a save and load); a real first does");
        var earlyBack = FirstsBook.ReadFrom(Obj(Wrap(j => early.WriteTo(j)), "firsts"));
        Check(earlyBack.KnownSeeded && earlyBack.FromKnowledge("$item_sausage") && !earlyBack.FromKnowledge("$item_sword_iron"), "RV1 the seed and the flag round-trip through the file");

        // RV2 a 0.8.6 kept-minutes file has no armor minutes: joined under this session's log, the armor is not kept for the joined span
        var noArmor = new EventLog { ArmorKept = false }; var withArmor = new EventLog { ArmorKept = true };
        Check(!EventLog.Join(noArmor, withArmor).ArmorKept && EventLog.Join(withArmor, new EventLog { ArmorKept = true }).ArmorKept && !EventLog.Join(withArmor, noArmor).ArmorKept,
              "RV2 joined logs keep armor only when both parts kept it (a 0.8.6 kept-minutes file has none)");

        // RV3 past 2 000 folded items the "other" line takes the last slot and survives the file
        var wideUps = new UpgradeLog { FromUtc = T0 };
        for (int i = 0; i < 4500; i++) wideUps.Add(new UpgradeMoment { Token = "$item_mod" + i, From = 1, To = 2 + i % 3, Utc = T0.AddMinutes(i) });
        var wideBack = UpgradeLog.ReadFrom(Obj(Wrap(j => wideUps.WriteTo(j)), "upgrades"));
        var otherLine = wideBack.Folded.FirstOrDefault(f => f.Token == DeedLog.OtherToken);
        Check(wideUps.Folded.Count == UpgradeLog.MaxFolded && wideUps.Total == 4500 && wideBack.Total == 4500 && otherLine != null && otherLine.Count == 2500 - (UpgradeLog.MaxFolded - 1) &&
              wideBack.Folded.Count == UpgradeLog.MaxFolded, "RV3 2 500 items folded: 1 999 named lines and one other line of " + (otherLine?.Count ?? 0) + ", all 4 500 upgrades back after save and load");

        try { Directory.Delete(dir, true); } catch { }
        return fails;
    }

    static Dictionary<string, object> Obj(string json, string key) => MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(json), key);
    static string Wrap(Action<Json> body) { var j = new Json().Open(); body(j); return j.Close().ToString(); }
    static T Also<T>(this T x, Action<T> a) { a(x); return x; }
}
