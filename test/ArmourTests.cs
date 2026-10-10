// The armour ledger (0.7, work/hearthwoven-0.7/ARMOUR-SCOPE.md): three stages per hit, a save that repeats adds nothing twice, a new session folds,
// the day rows and the file (schema 4, its own top-level key: never inside "history", which 0.6.1 and 0.6.2 rewrite), the short windows' buckets.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;

static class ArmourTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        HitData.DamageTypes D(float slash, float poison = 0, float fall = 0) => new HitData.DamageTypes { m_slash = slash, m_poison = poison, m_damage = fall };

        // one blow: before resistances 50 slash + 20 poison, before armour 50 + 10 (a poison mead), after armour 30 + 6; a fall is never armour's
        var s = new ArmourTally();
        s.Add("Draugr", "EnemyHit", D(50, 20, 5), D(50, 10, 5), D(30, 6, 5));
        var wardAte = s.Add("Draugr", "EnemyHit", D(0), D(0), D(0));   // the ward took it all before the armour
        Check(s.Hits == 1 && !wardAte && s.Incoming["Draugr|EnemyHit|poison"] == 20 && s.Before["Draugr|EnemyHit|poison"] == 10 && s.After["Draugr|EnemyHit|slash"] == 30 &&
              ArmourTally.Total(s.Before) == 60 && ArmourTally.Total(s.After) == 36 && !s.Before.Keys.Any(k => k.EndsWith("|damage")),
              "A ledger: three stages per source, cause and type; raw damage (falls) left out; a hit with nothing left (a ward) is not kept");

        // the book on this PC: a save that repeats adds only the growth, a new session folds the last one, the day rows add up to the sessions
        var t = new LocalTotals { PlayerId = 42 }; var day = new DateTime(2026, 10, 12, 20, 0, 0);
        t.Armour.FromUtc = new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);
        t.Armour.Record("S1", s, day);
        var s2 = ArmourTally.Sum(s); s2.Add("Troll", "EnemyHit", D(80), D(80), D(40));
        t.Armour.Record("S1", s2, day.AddMinutes(5));
        t.Armour.Record("S2", s, day.AddDays(1));
        var all = ArmourTally.Sum(t.Armour.BeforeSession("S3"));
        Check(t.Armour.Days.Count == 2 && ArmourTally.Total(t.Armour.Days[0].Tally.Before) == 140 && ArmourTally.Total(t.Armour.Days[1].Tally.Before) == 60 &&
              ArmourTally.Total(all.Before) == 200 && all.Hits == 3 && ArmourTally.Total(t.Armour.BeforeSession("S2").Before) == 140 &&
              ArmourTally.Total(t.Armour.Pending("S2", s2).Before) == 80,
              "A book: the same session saved twice adds only its growth; a new session folds the last; day rows add up; Pending is today's unsaved part");

        // the file: schema 4, "armour" top-level with its own start date, nothing of it inside a history row; it reads back exactly
        t.Record("S2", new DamageTally(), new SessionEvents(), null, new Dictionary<string, float> { ["DistanceSail"] = 1 }, day.AddDays(1));
        var json = t.ToJson(DateTime.UtcNow);
        var root = (Dictionary<string, object>)MiniJson.Parse(json);
        var history = MiniJson.Write(root["history"]);
        var back = LocalTotals.FromJson(json);
        Check(MiniJson.Num(root, "schema") >= 4 && root.ContainsKey("armour") && !history.Contains("before") && !history.Contains("incoming") &&
              back.Armour.FromUtc == t.Armour.FromUtc && back.Armour.LastSession == "S2" && back.Armour.Days.Count == 2 &&
              ArmourTally.Total(ArmourTally.Sum(back.Armour.BeforeSession("S3")).Before) == 200 && back.Extra.Count == 0,
              "A file: schema 4 with a top-level \"armour\" (its own date, sessions and day rows), never inside \"history\" (0.6.1/0.6.2 rewrite those rows); reads back exactly");
        var fresh = LocalTotals.FromJson(new LocalTotals { PlayerId = 7 }.ToJson(DateTime.UtcNow));
        Check(!new LocalTotals { PlayerId = 7 }.ToJson(DateTime.UtcNow).Contains("\"armour\"") && fresh.Armour.IsEmpty && fresh.Armour.FromUtc == DateTime.MinValue,
              "A file: an empty ledger writes no key; a file without one reads as empty, its date unset (set on load: the first run of 0.7)");

        // the short windows: one-minute buckets, cut at the window's start
        var log = new ArmourLog(); var now = new DateTime(2026, 10, 12, 20, 30, 0, DateTimeKind.Utc);
        log.Add(now.AddMinutes(-25), D(40), D(40), D(20)); log.Add(now.AddMinutes(-3), D(10), D(10), D(6));
        var ten = log.Since(now.AddMinutes(-10)); var whole = log.Since(null);
        Check(ArmourTally.Total(ten.Before) == 10 && ten.Hits == 1 && ArmourTally.Total(whole.After) == 26 && whole.Hits == 2 && ten.Before.ContainsKey("||slash"),
              "A minutes: the last 10 minutes hold only the hit 3 minutes ago; the buckets keep no source");
        return fails;
    }
}
