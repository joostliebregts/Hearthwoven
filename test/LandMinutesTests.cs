// 0.9 arrows track (work/hearthwoven-0.9/DESIGN.md "Best arrows for these foes"): the land you are in per minute, recorded beside the deed log
// (DeedLog.Lands): one per land per minute, both lands of a crossing minute, folded to ten minutes with the buckets, bounded by MaxKeys, the
// pickups of a minute carry its land (PickedUpIn), the window's minutes per land (LandMinutes), kept across a restart in the minutes file
// (KeptMinutes "lands") and a minutes file from before 0.9 still read. The deed buckets themselves are unchanged. Fictional times and names.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hearthwoven;

static class LandMinutesTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var start = new DateTime(2026, 10, 18, 18, 0, 0, DateTimeKind.Utc);

        // ---- an evening: 30 min in the Meadows, then the Ashlands; feathers in both, grausten only there; the crossing minute holds both ----
        var ev = new SessionEvents(); var game = new Dictionary<string, float>(); var log = new DeedLog();
        log.Tick(start, ev, () => game);
        for (int m = 0; m < 300; m++)
        {
            var t = start.AddMinutes(m);
            log.Tick(t, ev, () => game);
            for (int f = 0; f < 60; f += 10) log.NoteLand(t.AddSeconds(f), m < 30 ? "Meadows" : "AshLands");   // every frame (here every 10 s): one key per minute
            if (m == 30) log.NoteLand(t.AddSeconds(5), "Meadows");   // the crossing: back once, the same minute
            if (m == 10) SessionEvents.Add(ev.PickedUp, "$item_feathers", 2);
            if (m == 290) { SessionEvents.Add(ev.PickedUp, "$item_feathers", 4); SessionEvents.Add(ev.PickedUp, "$item_grausten", 3); }
            if (m == 40) SessionEvents.Add(ev.PickedUp, "$item_grausten", 5);
        }
        var now = start.AddMinutes(300);
        log.Flush(now, ev, game);
        Check(log.Lands.Values.All(v => v >= 1f) && log.Lands.Keys.All(k => k.Length > 18 && k[17] == '|'),
              "L keys: \"<minute>|<land>\" -> minutes, beside the buckets (" + log.Lands.Count + " keys)");
        var all = log.LandMinutes(null);
        Check(all.Count == 2 && Math.Abs(all["Meadows"] - 31) < 1e-3 && Math.Abs(all["AshLands"] - 270) < 1e-3,
              "L minutes: a minute counts once per land however often it is noted; the crossing minute counts for both (Meadows 31, Ashlands 270: " + string.Join(", ", all.Select(kv => kv.Key + " " + kv.Value)) + ")");
        var folded = log.Lands.Keys.Count(k => k.CompareTo(EventLog.Bucket(now.AddMinutes(-EventLog.FoldAfterMinutes - 1))) < 0);
        Check(folded > 0 && log.Lands.Keys.Where(k => k.CompareTo(EventLog.Bucket(now.AddMinutes(-EventLog.FoldAfterMinutes - 1))) < 0).All(k => k[15] == '0') && log.Lands.Values.Max() <= 20f,
              "L fold: older than " + EventLog.FoldAfterMinutes + " min the land minutes fold to ten-minute keys with the buckets, summing their minutes (" + folded + " folded keys, at most " + log.Lands.Values.Max() + ")");
        var ash = log.PickedUpIn(null, "AshLands"); var home = log.PickedUpIn(null, "Meadows");
        Check(ash.TryGetValue("$item_grausten", out var g) && Math.Abs(g - 8) < 1e-3 && ash.TryGetValue("$item_feathers", out var fa) && Math.Abs(fa - 4) < 1e-3 &&
              home.TryGetValue("$item_feathers", out var fh) && Math.Abs(fh - 2) < 1e-3 && !home.ContainsKey("$item_grausten"),
              "L pickups carry their minute's land, a folded pickup its ten minutes' land (Ashlands: 8 grausten, 4 feathers; Meadows: 2 feathers)");
        var hour = log.LandMinutes(now.AddHours(-1));
        Check(hour.Count == 1 && hour.ContainsKey("AshLands") && log.PickedUpIn(now.AddHours(-1), "AshLands").Sum(kv => kv.Value) == 7 && log.PickedUpIn(now.AddHours(-1), "Meadows").Count == 0,
              "L window: the last hour is the Ashlands only, its 7 pickups");
        var grew = log.Grew(null);
        Check(grew[DeedLog.PickedUp].Sum(kv => kv.Value) == 14 && !grew.ContainsKey("land") && !log.Buckets.Keys.Any(k => k.Contains("AshLands")),
              "L the deed buckets are untouched: no land family, no land in their keys, every pickup still counted once (14)");
        log.NoteLand(now, "None"); log.NoteLand(now, "");
        Check(!log.Lands.Keys.Any(k => k.EndsWith("|None") || k.EndsWith("|")), "L no land (None, empty) is never noted");
        var cap = new DeedLog();
        for (int m = 0; m < DeedLog.MaxKeys + 50; m++) cap.NoteLand(start.AddMinutes(m), "Meadows");
        Check(cap.Lands.Count == DeedLog.MaxKeys, "L bounded: at most " + DeedLog.MaxKeys + " land keys, as the buckets");

        // ---- kept across a restart (KeptMinutes "lands"), read again within the 3 h window; a file from before 0.9 reads ----
        var kept = KeptMinutes.Take(now, new EventLog(), new ArmourLog(), log.Buckets, log.Earlier).WithLands(log.Lands, log.EarlierLands);
        kept.World = "Midgard-1"; kept.PlayerId = "7"; kept.Name = "Rowan";
        var json = kept.ToJson(now);
        var back = KeptMinutes.FromJson(json);
        Check(back != null && back.Lands.Count == kept.Lands.Count && kept.Lands.Count > 0 && kept.Lands.Keys.All(k => string.CompareOrdinal(k, 0, EventLog.Bucket(kept.FromUtc), 0, 17) >= 0),
              "L kept: the minutes file keeps the land minutes the 3 h window reaches (" + kept.Lands.Count + " keys) and reads them back");
        var dir = Path.Combine(Path.GetTempPath(), "hw-lands-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "7-Rowan.minutes.json"); File.WriteAllText(path, json);
        var loaded = KeptMinutes.Load(path, "Midgard-1", 7, "Rowan", now.AddMinutes(5), now.AddMinutes(4), out var problem);
        var next = new DeedLog(); foreach (var kv in loaded.Deeds) next.Earlier[kv.Key] = kv.Value; foreach (var kv in loaded.Lands) next.EarlierLands[kv.Key] = kv.Value;
        Check(problem == null && next.LandMinutes(now.AddHours(-1)).ContainsKey("AshLands") && next.PickedUpIn(now.AddHours(-1), "AshLands").Sum(kv => kv.Value) == 7 && next.LandMinutes(null).Count == 0,
              "L restart: the next connection's last hour still knows its Ashlands minutes and pickups (kept), Session none of them");
        var before = KeptMinutes.Take(now, new EventLog(), new ArmourLog(), log.Buckets, log.Earlier);   // as 0.8.6 wrote it: no land minutes
        var old = before.ToJson(now);
        Check(!old.Contains("\"lands\"") && KeptMinutes.FromJson(old) != null && KeptMinutes.FromJson(old).Lands.Count == 0 && KeptMinutes.FromJson(old).Deeds.Count == kept.Deeds.Count,
              "L a minutes file without lands (before 0.9) reads as before, and writes no lands key");
        Check(!kept.SharedJson().Contains("AshLands"), "L never shared: the shared copy's kept log carries no land minutes");
        try { Directory.Delete(dir, true); } catch { }
        return fails;
    }
}
