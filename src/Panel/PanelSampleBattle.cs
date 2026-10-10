using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The sample's battle record (0.8, BattleRecord.cs), worked out from the very log its Damage and Defence pages read, so the feed and the
    /// "×N" never contradict the bars: every hit of the session's log is replayed through the real recorder, split over separate creatures of
    /// its kind by how much damage one of them takes (Takes), and most of them defeated a moment after their last hit (every fifth survives,
    /// every fifth after that is defeated with a fellow player of the sample). The book on this PC: recorded from an evening two days ago (as the
    /// armour sample), the session split over its two local days, saved up to now. Pure C#; fictional players only.
    /// </summary>
    public static class BattleSample
    {
        /// <summary>About what one creature of a kind takes before it falls in the sample (fictional, the order of the game's health).</summary>
        static readonly Dictionary<string, float> Takes = new Dictionary<string, float>
        {
            ["Boar"] = 60, ["Neck"] = 80, ["Deer"] = 40, ["Greydwarf"] = 120, ["Greydwarf_Elite"] = 300, ["Greydwarf_Shaman"] = 120, ["Troll"] = 500,
            ["Skeleton"] = 100, ["Draugr"] = 200, ["Draugr_Elite"] = 400, ["Blob"] = 100, ["Leech"] = 60, ["Wraith"] = 300, ["Goblin"] = 250,
        };
        static readonly HashSet<string> NotCreatures = new HashSet<string>(Enum.GetNames(typeof(HitData.HitType)));   // Freezing, Fall, Burning ... are causes, not foes

        sealed class One { public FoeId Id; public string Kind; public float Dealt; public DateTime LastDealt = DateTime.MinValue, Last; public int Index; }
        struct Ev { public DateTime At; public bool Dealt; public string Kind, Biome; public HitData.DamageTypes D; public One Foe; public bool Kill; }

        /// <summary>Fills the book's battle record: Battle (this session), FoeBook, FoesSince and FoesFromUtc, from its log. <paramref name="near"/>:
        /// the players near you in the last two fights (0.8 Last fight; null: nobody).</summary>
        public static void Session(PanelInput p, DateTime now, string[] fellows, string[] near = null)
        {
            if (p?.Log == null) return;
            var grouped = new Dictionary<(string time, string biome, string dir, string foe), HitData.DamageTypes>();
            foreach (var kv in p.Log.Damage)
            {
                var k = kv.Key.Split('|');   // time|biome|dir|foe|cause|type
                if (k.Length < 6 || NotCreatures.Contains(k[3]) || Array.IndexOf(BattleRecorder.Types, k[5]) < 0 || kv.Value <= 0) continue;
                var key = (k[0], k[1], k[2], k[3]);
                grouped.TryGetValue(key, out var d); grouped[key] = Add(d, k[5], kv.Value);
            }
            // the hits in time order, spread over their minute (one bucket holds a minute of hits)
            var evs = new List<Ev>();
            foreach (var bucket in grouped.GroupBy(g => g.Key.time).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                if (!DateTime.TryParseExact(bucket.Key, "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
                int i = 0;
                foreach (var g in bucket.OrderBy(g => g.Key.dir == "dealt" ? 0 : 1).ThenBy(g => g.Key.foe, StringComparer.Ordinal))
                    evs.Add(new Ev { At = t.AddSeconds(5 + 9 * (i++ % 6)), Dealt = g.Key.dir == "dealt", Kind = g.Key.foe, Biome = g.Key.biome, D = g.Value });
            }
            evs = evs.OrderBy(e => e.At).ToList();
            // which creature each hit belongs to: the kind's current one until it has taken what it takes; a foe's hit on you goes to the kind's
            // creature of the same fight, else to one that only hit you
            var current = new Dictionary<string, One>(); var all = new List<One>(); uint next = 1;
            for (int i = 0; i < evs.Count; i++)
            {
                var e = evs[i];
                current.TryGetValue(e.Kind, out var c);
                var take = Takes.TryGetValue(e.Kind, out var tk) ? tk : 150f;
                bool sameFight = c != null && e.At - c.Last <= BattleRecorder.FightGap;
                if (c == null || !sameFight || (e.Dealt && c.Dealt >= take)) { c = new One { Id = new FoeId(p.PlayerId, next++), Kind = e.Kind, Index = all.Count }; all.Add(c); current[e.Kind] = c; }
                if (e.Dealt) { c.Dealt += Total(e.D); c.LastDealt = e.At; }
                c.Last = e.At;
                e.Foe = c; evs[i] = e;
            }
            // the kills: every fifth creature survives, the one two after it falls with a fellow player; one that only hit you is never yours
            foreach (var c in all)
                if (c.LastDealt > DateTime.MinValue && c.Index % 5 != 3) evs.Add(new Ev { At = c.Last.AddSeconds(2), Foe = c, Kill = true });
            var rec = new BattleRecorder();
            // 0.8 Last fight: who was near you. In the last fight the first two fellow players stood by you (a sample every 2 s of play), a third
            // came by for a moment only (under the 10 s that put a player in a fight); in the fight before it the first one alone
            var ordered = evs.OrderBy(x => x.At).ThenBy(x => x.Kill ? 1 : 0).ToList();
            DateTime lastStart = DateTime.MinValue, prevStart = DateTime.MinValue;
            for (int i = 0; i < ordered.Count; i++) if (!ordered[i].Kill && (i == 0 || ordered[i].At - ordered[i - 1].At > BattleRecorder.FightGap)) { prevStart = lastStart; lastStart = ordered[i].At; }
            DateTime nearAt = DateTime.MinValue; int glimpse = 0;
            foreach (var e in ordered)
            {
                var token = "$enemy_" + e.Foe.Kind.ToLowerInvariant();
                if (e.Kill)
                {
                    var with = fellows != null && fellows.Length > 0 && e.Foe.Index % 5 == 0 ? new[] { fellows[(e.Foe.Index / 5) % fellows.Length] } : FoeEncounter.NoNames;
                    if (with.Length > 0) rec.Attackers(e.Foe.Id, 2, with);
                    rec.Killed(e.At, token, 1 + with.Length);
                    rec.Destroyed(e.At.AddSeconds(0.5), e.Foe.Id);
                    continue;
                }
                if (e.Dealt) rec.Dealt(e.At, e.Foe.Id, e.Kind, token, e.Biome, e.D);
                else rec.Received(e.At, e.Foe.Id, e.Kind, token, e.Biome, e.D);
                if (near != null && near.Length > 0 && prevStart > DateTime.MinValue && e.At >= prevStart && e.At - nearAt >= TimeSpan.FromSeconds(BattleRecorder.NearEvery))
                {
                    var last = e.At >= lastStart;
                    rec.Near(e.At, near[0], BattleRecorder.NearEvery * 2);
                    if (last && near.Length > 1) rec.Near(e.At, near[1], BattleRecorder.NearEvery * 2);
                    if (last && near.Length > 2 && glimpse++ < 1) rec.Near(e.At, near[2], BattleRecorder.NearEvery);
                    nearAt = e.At;
                }
            }
            rec.Tick(now);
            p.Battle = rec;

            // the book on this PC: recorded from an evening two days ago, the session over its two local days, saved up to now
            Func<DateTime, DateTime> local = t => p.ToLocal != null ? p.ToLocal(t) : t.ToLocalTime();
            var today = local(now).Date; var fromDay = today.AddDays(-2);
            var offset = local(now) - now;
            var from = DateTime.SpecifyKind(fromDay.AddHours(19) - offset, DateTimeKind.Utc);
            var session = rec.Counts(null);
            var sinceMidnight = rec.Counts(DateTime.SpecifyKind(today - offset, DateTimeKind.Utc));
            var book = new FoeBook { FromUtc = from, LastSession = p.SessionId ?? "s1", Last = FoeCounts.Minus(session, null) };
            var before = FoeCounts.Minus(session, sinceMidnight);
            if (!before.Empty) book.Days.Add((DayHistory.DayKey(today.AddDays(-1)), before));
            if (!sinceMidnight.Empty) book.Days.Add((DayHistory.DayKey(today), FoeCounts.Minus(sinceMidnight, null)));
            p.FoeBook = book; p.FoesSince = book.Sum(fromDay, today); p.FoesPending = null; p.FoesFromUtc = from;
        }

        /// <summary>What a book's record puts in its shared copy (FoeShare), as Plugin does: this session and since recording began.</summary>
        public static FoeShare ShareOf(PanelInput own) =>
            own?.Battle == null ? null : new FoeShare { Session = own.Battle.Counts(null), Since = own.FoesSince, FromUtc = own.FoesFromUtc };

        static float Total(HitData.DamageTypes d) => d.m_blunt + d.m_slash + d.m_pierce + d.m_fire + d.m_frost + d.m_lightning + d.m_poison + d.m_spirit;

        static HitData.DamageTypes Add(HitData.DamageTypes d, string type, float v)
        {
            switch (type)
            {
                case "blunt": d.m_blunt += v; break; case "slash": d.m_slash += v; break; case "pierce": d.m_pierce += v; break; case "fire": d.m_fire += v; break;
                case "frost": d.m_frost += v; break; case "lightning": d.m_lightning += v; break; case "poison": d.m_poison += v; break; case "spirit": d.m_spirit += v; break;
            }
            return d;
        }
    }
}
