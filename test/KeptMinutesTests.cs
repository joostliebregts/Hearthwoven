// 0.8.2 (Joost, right after restarting the game to install a build: Battle > Damage in 3 h said "Nothing in the last 3 hours" though he had
// fought all evening): every connection started its minute logs from zero. KeptMinutes keeps their last minutes beside the local totals and the
// next connection in the same world joins them for the short windows. Here: the file across a restart (log, deeds, armour), another world or
// character never joins, minutes past the 3 h window are dropped, a same-minute reconnect keeps Session clean, and the shared copy carries them
// beside measuredLog (which stays the connection's). Fictional ids and names; every file lives in a fresh temp folder.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class KeptMinutesTests
{
    const long Id = 4242424242L;
    static readonly DateTime Now = new DateTime(2026, 10, 10, 22, 17, 30, DateTimeKind.Utc);

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        HitData.DamageTypes D(float slash) => new HitData.DamageTypes { m_slash = slash };
        var dir = Path.Combine(Path.GetTempPath(), "hw-kept-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var path = KeptMinutes.PathFor(LocalTotals.PathFor(dir, Id, "Rowan"));
        const string world = "Midgard-123456789";
        float Dealt(EventLog l) => l.Damage.Where(kv => kv.Key.Contains("|dealt|")).Sum(kv => kv.Value);

        // ---------- the evening before the restart: a fight 40 min ago, one 175 min ago (still in 3 h), one 200 min ago (past it) ----------
        var restart = Now.AddMinutes(-5);   // the game restarted 5 min ago: the old connection's last save, then the new connection
        var first = new EventLog();
        first.AddDamage(Now.AddMinutes(-200), "Swamp", true, "Draugr", "Swords", D(300));
        first.AddDamage(Now.AddMinutes(-175), "Swamp", true, "Draugr", "Swords", D(120));
        first.AddDamage(Now.AddMinutes(-40), "Mistlands", true, "Seeker", "Swords", D(500));
        first.AddDamage(Now.AddMinutes(-40).AddSeconds(5), "Mistlands", false, "Seeker", "EnemyHit", D(80));
        first.AddDeath(Now.AddMinutes(-39), "Mistlands", 10f, 20f);
        first.AddGuard(Now.AddMinutes(-41), true);
        var deeds = new Dictionary<string, float> { [EventLog.Bucket(Now.AddMinutes(-30)) + "|pickedUp|Wood"] = 12, [EventLog.Bucket(Now.AddMinutes(-230)) + "|pickedUp|Stone"] = 4 };
        var armour = new ArmourLog(); armour.Add(Now.AddMinutes(-40), D(80), D(80), D(30));
        var taken = KeptMinutes.Take(restart, first, armour, deeds);
        taken.World = world; taken.PlayerId = Id.ToString(); taken.Name = "Rowan";
        taken.Save(path, restart);
        var kept = KeptMinutes.Load(path, world, Id, "Rowan", Now, restart.AddSeconds(30), out var problem);
        Check(problem == null && kept != null && Dealt(kept.Log) == 620 && kept.Log.Deaths.Count == 1 && kept.Log.Guard.Count == 2 && kept.Deeds.Count == 1 && kept.Armour.Damage.Count > 0 &&
              path.EndsWith("-Rowan.minutes.json") && File.Exists(path),
              "K kept across a restart: the fights 40 and 175 min ago (" + (kept == null ? "none" : Dealt(kept.Log).ToString()) + " dealt), the death, the block and parry, the deeds and the armour minutes, beside the totals file");
        Check(kept != null && kept.Log.Damage.Keys.All(k => string.CompareOrdinal(k, 0, EventLog.Bucket(KeptMinutes.From(Now)), 0, 17) >= 0) && !kept.Deeds.Keys.Any(k => k.Contains("Stone")) &&
              KeptMinutes.From(Now) <= Now.AddMinutes(-181) && KeptMinutes.From(Now).Minute % 10 == 0,
              "K the fight 200 min ago and the deeds 230 min ago are dropped (past every short window); kept from " + KeptMinutes.From(Now).ToString("HH:mm") + ", on a whole ten minutes, before the 3 h window's start");

        // ---------- never another world or character; a same-minute reconnect leaves that minute out, so Session never holds a kept one ----------
        Check(KeptMinutes.Load(path, "Ashlands-987", Id, "Rowan", Now, restart, out _) == null && KeptMinutes.Load(path, world, Id, "Tor", Now, restart, out _) == null &&
              KeptMinutes.Load(path, world, Id + 1, "Rowan", Now, restart, out _) == null && File.Exists(path),
              "K another world, another name or another player id: nothing joined, the file left as it is");
        var late = new EventLog(); late.AddDamage(restart.AddSeconds(-20), "Plains", true, "Fuling", "Swords", D(70)); late.AddDamage(restart.AddMinutes(-3), "Plains", true, "Fuling", "Swords", D(30));
        var lt = KeptMinutes.Take(restart, late, null); lt.World = world; lt.PlayerId = Id.ToString(); lt.Name = "Rowan"; lt.Save(path, restart);
        var reconnect = new DateTime(restart.Year, restart.Month, restart.Day, restart.Hour, restart.Minute, 0, DateTimeKind.Utc).AddSeconds(50);   // back in the same minute as the last hit
        var quick = KeptMinutes.Load(path, world, Id, "Rowan", Now, reconnect, out _);
        Check(quick != null && Dealt(quick.Log) == 30, "K a reconnect within the minute of the last hit: that minute is left out (" + (quick == null ? 0 : Dealt(quick.Log)) + " of 100 kept), so no kept minute ends after Session's start");

        // ---------- the shared copy: "logBefore" beside measuredLog, which stays this connection's; no death positions; read back apart ----------
        var now = new EventLog(); now.AddDamage(Now.AddMinutes(-2), "Mistlands", true, "Tick", "Swords", D(40));
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats();
        var json = GroupShare.SharedCopy(Snapshot.Build("0.8.2", Id, "Rowan", stats, new Snapshot.SkillInfo[0], "Midgard", new DamageTally(), "s2", new SessionEvents(), now, true, logBefore: kept?.SharedJson()));
        var back = PanelInput.FromSnapshot(json);
        var plain = Snapshot.Build("0.8.2", Id, "Rowan", stats, new Snapshot.SkillInfo[0], "Midgard", new DamageTally(), "s2", new SessionEvents(), now, true);
        Check(back != null && Dealt(back.Log) == 40 && back.LogBefore != null && Dealt(back.LogBefore) == 620 && back.LogBeforeFromUtc == kept.FromUtc && !json.Contains("\"x\":10") &&
              back.LogBefore.Deaths.Count == 1 && back.LogBefore.Deaths[0].X == 0 && back.FirstRecordedUtc == Minute(Now.AddMinutes(-2)) &&
              !plain.Contains(KeptMinutes.SharedKey) && PanelInput.FromSnapshot(plain).LogBefore == null,
              "K shared: measuredLog holds this connection's 40 only (the server and the report add it up per session), logBefore the kept 620 with its from, the death without its position; " +
              "the recorded span is this connection's; nothing kept, no logBefore");

        // ---------- the size: a heavy 5 h evening, a hit every 10 s on four kinds in two biomes; the first copy after the restart vs the last before ----------
        var heavy = new EventLog(); var start = restart.AddHours(-5); var kinds = new[] { "Seeker", "Gjall", "Tick", "SeekerBrute" };
        for (int s = 0; s < 5 * 3600; s += 10)
        {
            var at = start.AddSeconds(s);
            heavy.AddDamage(at, s % 600 < 300 ? "Mistlands" : "Plains", true, kinds[(s / 10) % 4], s % 20 == 0 ? "Swords" : "Bows", D(30 + s % 7));
            if (s % 30 == 0) heavy.AddDamage(at, s % 600 < 300 ? "Mistlands" : "Plains", false, kinds[(s / 30) % 4], "EnemyHit", D(12));
        }
        string Copy(EventLog l, string session, string before = null) => GroupShare.SharedCopy(Snapshot.Build("0.8.2", Id, "Rowan", stats, new Snapshot.SkillInfo[0], "Midgard", new DamageTally(), session, new SessionEvents(), l, true, copyId: session + "-1", logBefore: before));
        var lastBefore = Copy(heavy, "s1");
        var hk = KeptMinutes.Take(restart, heavy, null); hk.World = world; hk.PlayerId = Id.ToString(); hk.Name = "Rowan"; hk.Save(path, restart);
        var hkept = KeptMinutes.Load(path, world, Id, "Rowan", Now, restart.AddSeconds(30), out _);
        var fresh = new EventLog(); fresh.AddDamage(Now.AddMinutes(-1), "Mistlands", true, "Tick", "Swords", D(40));
        var firstAfter = Copy(fresh, "s2", hkept.SharedJson());
        int packedBefore = Transport.Pack(lastBefore).Length, packedAfter = Transport.Pack(firstAfter).Length;
        Check(hkept.Log.Hits.Count < heavy.Hits.Count && firstAfter.Length <= lastBefore.Length && packedAfter <= packedBefore,
              "K size: the first copy after the restart is " + firstAfter.Length / 1024 + " KB (" + packedAfter / 1024 + " KB packed), the last one before it " + lastBefore.Length / 1024 + " KB (" +
              packedBefore / 1024 + " KB packed): the kept minutes are the last " + KeptMinutes.KeepMinutes + " min of the same log, never more");
        // the live updates that follow: the kept part never changes, so an update carries only what grew; once forgotten, one small removal
        fresh.AddDamage(Now, "Mistlands", true, "Tick", "Swords", D(15));
        var grew = LiveDelta.Make(firstAfter, Copy(fresh, "s2", hkept.SharedJson()), "s2-1", 1);
        var dropped = LiveDelta.Make(firstAfter, Copy(fresh, "s2"), "s2-1", 2);
        Check(!grew.TooBig && !grew.Text.Contains(KeptMinutes.SharedKey) && grew.Text.Length < 600 && !dropped.TooBig && dropped.Text.Contains("\"/" + KeptMinutes.SharedKey + "\"") && dropped.Packed.Length < 1000,
              "K live: an update after the restart carries only the new hit (" + grew.Text.Length + " bytes, no kept minute); dropping the kept minutes 3 h later is one small removal (" + dropped.Packed.Length + " bytes packed)");

        // ---------- Deeds > Recent: a window with a start reaches the kept deeds, the whole session (Session, since last time) never ----------
        var dl = new DeedLog();
        foreach (var kv in kept?.Deeds ?? new Dictionary<string, float>()) dl.Earlier[kv.Key] = kv.Value;
        var ev = new SessionEvents(); SessionEvents.Add(ev.PickedUp, "Wood", 3); dl.Flush(Now, ev, null);
        float Wood(Dictionary<string, Dictionary<string, float>> g) => g.TryGetValue("pickedUp", out var p) && p.TryGetValue("Wood", out var w) ? w : 0f;
        Check(Wood(dl.Grew(Now.AddHours(-1))) == 15 && Wood(dl.Grew(Now.AddMinutes(-10))) == 3 && Wood(dl.Grew(null)) == 3,
              "K deeds: 1 h counts the 12 wood kept from before the restart and this session's 3 (" + Wood(dl.Grew(Now.AddHours(-1))) + "), 10 min and Session only this session's 3");

        try { Directory.Delete(dir, true); } catch { }
        return fails;
    }

    static DateTime? Minute(DateTime t) => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Utc);
}
