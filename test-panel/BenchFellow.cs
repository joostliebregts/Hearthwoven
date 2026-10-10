// A fellow the size of the live server's real copy (about 26 KB shared: 109 counters, 251 kinds picked up, 385 pieces, 99 crafts, 28 skills,
// 90 minutes of log), playing 10 s at a time, as test/LiveSyncTests.cs builds it; fictional (Edda). For the fellow-update bench (Bench.cs)
// and its test (PerfPassTests.cs): the stored full copy as the server relays it, and each 10 s update as the sender makes it.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;

sealed class BenchFellow
{
    readonly PlayerProfile.PlayerStats[] stats = new PlayerProfile.PlayerStats[1];
    readonly List<Snapshot.SkillInfo> skills;
    readonly SessionEvents ev = new SessionEvents(), evBefore = new SessionEvents();
    readonly DamageTally dmg = new DamageTally(), dmgBefore = new DamageTally();
    readonly EventLog log = new EventLog();
    readonly DateTime t0;
    readonly HitData.DamageTypes hit = new HitData.DamageTypes { m_slash = 18.5f, m_fire = 4f };
    const long Id = 9007199254740993L;
    public readonly string BaseShared, Stored, Key;
    int round;

    public BenchFellow(DateTime start)
    {
        t0 = start;
        var rnd = new Random(11);
        stats[0] = new PlayerProfile.PlayerStats();
        foreach (PlayerStatType t in Enum.GetValues(typeof(PlayerStatType))) stats[0][t] = (float)Math.Round(rnd.NextDouble() * 5000, 1);
        for (int i = 0; i < 251; i++) stats[0].m_itemPickupStats["$item_kind" + i] = rnd.Next(1, 900);
        for (int i = 0; i < 99; i++) stats[0].m_itemCraftStats["$item_made" + i] = rnd.Next(1, 60);
        for (int i = 0; i < 27; i++) { stats[0].m_pickableStats["Pickable_" + i] = rnd.Next(1, 300); stats[0].m_foodEatenStats["Food" + i] = rnd.Next(1, 90); }
        for (int i = 0; i < 385; i++) stats[0].m_piecesPlacedStats["$piece_kind" + i] = rnd.Next(1, 400);
        for (int i = 0; i < 40; i++) stats[0].m_enemyStats[0]["$enemy_" + i] = rnd.Next(1, 200);
        stats[0].m_knownWorlds["Midgard"] = 3600f * 90;
        skills = Enumerable.Range(0, 28).Select(i => new Snapshot.SkillInfo { Name = "Skill" + i, Level = (float)Math.Round(rnd.NextDouble() * 70, 3), Accumulator = (float)Math.Round(rnd.NextDouble() * 50, 2) }).ToList();
        for (int i = 0; i < 60; i++) { evBefore.PickedUp["$item_kind" + i] = rnd.Next(1, 500); evBefore.Made["$item_made" + i] = rnd.Next(1, 30); evBefore.ChopHits["Beech" + (i % 6)] = rnd.Next(10, 900); }
        for (int m = 0; m < 90; m++) { log.AddDamage(t0.AddMinutes(-90 + m), "BlackForest", true, "Greydwarf", "Axes", hit); dmg.AddDealt("Greydwarf", "Axes", hit); ev.PickedUp["$item_kind" + (m % 20)] = m + 1; }
        var full = Full("s1-1");
        BaseShared = GroupShare.SharedCopy(full);
        Stored = FellowIds.WithPlatform(BaseShared, "76561198000000078");   // as the server keeps and relays it (GroupShare.Store)
        Key = FellowIds.KeyOf(Stored);
    }

    string Full(string copyId) => Snapshot.Build("0.8.0", Id, "Edda", stats, skills, "Midgard", dmg, "s1", ev, log, true,
                                                 SessionEvents.Sum(evBefore, ev), DamageTally.Sum(dmgBefore, dmg), null, null, null, null, new[] { "Meadows", "BlackForest" }, copyId);

    /// <summary>Ten more seconds of play, then the update the sender makes (cumulative since the full copy, seq = the round).</summary>
    public (long seq, string text) Next()
    {
        round++;
        stats[0][PlayerStatType.DistanceWalk] += 31.4f; stats[0][PlayerStatType.DistanceRun] += 22.75f; stats[0][PlayerStatType.Jumps] += 2;
        stats[0].m_itemPickupStats["$item_kind3"] += 4; ev.PickedUp["$item_kind3"] = ev.PickedUp.TryGetValue("$item_kind3", out var w) ? w + 4 : 4;
        for (int h = 0; h < 3; h++) { log.AddDamage(t0.AddSeconds(round * 10 + h), "BlackForest", true, "Troll", "Axes", hit); dmg.AddDealt("Troll", "Axes", hit); }
        skills[4] = new Snapshot.SkillInfo { Name = "Skill4", Level = skills[4].Level, Accumulator = skills[4].Accumulator + 0.85f };
        var made = LiveDelta.Make(BaseShared, GroupShare.SharedCopy(Full("s1-1")), "s1-1", round);
        return (round, made.Text);
    }
}
