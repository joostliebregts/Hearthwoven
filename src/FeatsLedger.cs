using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// The moment a feat tier was earned (Feats, ACHIEVEMENTS-06 section 4): the date, and the biome under the player's feet
    /// when the sample crossed the line. Coarse on purpose: never x/z. Three honest kinds of moment:
    /// exact (Utc and Biome set: Hearthwoven saw it happen), Before (the line was already crossed when Hearthwoven first counted:
    /// "before 8 Oct") and Noticed (a feat derived from fellow players' copies: the latest it can have happened, "by 9 Oct").
    /// </summary>
    public struct FeatMoment
    {
        public DateTime Utc;          // DateTime.MinValue = not known
        public string Biome, Place;   // Heightmap.Biome name; Place a coarse word ("aboard") or null. Place is never shared.
        public bool Before, Noticed;
        public bool Known => Before || Utc > DateTime.MinValue;
    }

    /// <summary>
    /// A best of its kind (Heavy Keel's biggest load, Long Lead's longest lead): the number, the day and the biome under the player's
    /// feet when it was set, and for an animal which kind. Never a position.
    /// </summary>
    public struct BestMark
    {
        public double Value; public DateTime Utc; public string Biome, What;
    }

    /// <summary>
    /// What the Feats system keeps on the player's own PC, inside LocalTotals (top-level "feats"): which feat tiers were earned
    /// and when, the small counters the hooks add to (shield hits near a fellow, damage stopped, the best parry streak), whether
    /// the first look at the character's old counts is done (Primed) and how many earned tiers the player has seen on the
    /// Feats page (the gold dot). Bounded however long someone plays: at most MaxFeats feats with MaxTiers moments each and
    /// MaxCounts counters. Pure C# (no Unity calls), unit-tested.
    ///
    /// Shared with fellow players in the snapshot under "feats" as id -> {tier, utc, biome} (the highest tier only; never a
    /// place, never a counter, so fellows see what was earned and when, not how far along someone is).
    /// A fellow's copy read back (ReadShared) has Earned lists as long as the tier, only the last moment real.
    /// </summary>
    public class FeatsLedger
    {
        public const int MaxFeats = 64, MaxTiers = 3, MaxCounts = 32, MaxBests = 8;
        public bool Primed;
        public int Seen;
        public readonly Dictionary<string, List<FeatMoment>> Earned = new Dictionary<string, List<FeatMoment>>();
        public readonly Dictionary<string, double> Counts = new Dictionary<string, double>();
        /// <summary>Bests that are not sums (cargoBestVoyage: metal and ore items aboard over 2 km; ledBestMeters: metres of the longest lead):
        /// the number also sits in Counts under the same key, so a feat reads it like any counter. These add the day, the biome and the animal.
        /// Shared with fellow players in the snapshot under "featBests" (never a place).</summary>
        public readonly Dictionary<string, BestMark> Bests = new Dictionary<string, BestMark>();

        public int Tier(string id) => id != null && Earned.TryGetValue(id, out var l) ? l.Count : 0;

        /// <summary>The moment of a tier (1..3); 0 = the highest earned. null = not earned.</summary>
        public FeatMoment? Moment(string id, int tier = 0)
        {
            if (id == null || !Earned.TryGetValue(id, out var l) || l.Count == 0) return null;
            var at = tier <= 0 ? l.Count : tier;
            return at >= 1 && at <= l.Count ? l[at - 1] : (FeatMoment?)null;
        }

        public int TiersEarned => Earned.Values.Sum(l => l.Count);
        /// <summary>Earned tiers the player has not seen on the Feats page yet (the gold dot).</summary>
        public int Unseen => Math.Max(0, TiersEarned - Seen);
        public void MarkSeen() { Seen = TiersEarned; }

        /// <summary>Records every tier up to <paramref name="tier"/> that is not there yet, each with the moment; true when anything was added.</summary>
        public bool Earn(string id, int tier, FeatMoment moment)
        {
            if (string.IsNullOrEmpty(id) || tier < 1) return false;
            tier = Math.Min(tier, MaxTiers);
            if (!Earned.TryGetValue(id, out var l))
            {
                if (Earned.Count >= MaxFeats) return false;
                Earned[id] = l = new List<FeatMoment>();
            }
            var added = false;
            while (l.Count < tier) { l.Add(moment); added = true; }
            return added;
        }

        public double Count(string key) => key != null && Counts.TryGetValue(key, out var v) ? v : 0;
        public void Add(string key, double v)
        {
            if (string.IsNullOrEmpty(key) || v == 0 || !Json.IsFinite(v)) return;
            if (!Counts.ContainsKey(key) && Counts.Count >= MaxCounts) return;
            Counts[key] = Count(key) + v;
        }
        public void Max(string key, double v)
        {
            if (string.IsNullOrEmpty(key) || !Json.IsFinite(v) || v <= Count(key)) return;
            if (!Counts.ContainsKey(key) && Counts.Count >= MaxCounts) return;
            Counts[key] = v;
        }

        /// <summary>
        /// A new best of <paramref name="key"/>: kept (with its day, biome and animal) only when it beats the one before. Bounded like the
        /// counters. True when it was kept.
        /// </summary>
        public bool NoteBest(string key, double value, DateTime utc, string biome, string what = null)
        {
            if (string.IsNullOrEmpty(key) || !(value > Count(key))) return false;
            if (!Bests.ContainsKey(key) && Bests.Count >= MaxBests) return false;
            if (!Counts.ContainsKey(key) && Counts.Count >= MaxCounts) return false;
            Counts[key] = value;
            Bests[key] = new BestMark { Value = value, Utc = utc, Biome = string.IsNullOrEmpty(biome) ? null : biome, What = string.IsNullOrEmpty(what) ? null : what };
            return true;
        }

        public bool IsEmpty => !Primed && Earned.Count == 0 && Counts.Count == 0 && Bests.Count == 0 && Seen == 0;

        static string Iso(DateTime d) => d.ToString("o", CultureInfo.InvariantCulture);
        static DateTime ParseUtc(string s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : DateTime.MinValue;

        static void WriteMoment(Json j, FeatMoment m, bool place)
        {
            j.Open();
            if (m.Utc > DateTime.MinValue) j.Str("utc", Iso(m.Utc));
            if (!string.IsNullOrEmpty(m.Biome)) j.Str("biome", m.Biome);
            if (place && !string.IsNullOrEmpty(m.Place)) j.Str("place", m.Place);
            if (m.Before) j.Num("before", 1);
            if (m.Noticed) j.Num("noticed", 1);
            j.Close();
        }

        static FeatMoment ReadMoment(Dictionary<string, object> o) => new FeatMoment
        {
            Utc = ParseUtc(MiniJson.Str(o, "utc")), Biome = NullIfEmpty(MiniJson.Str(o, "biome")), Place = NullIfEmpty(MiniJson.Str(o, "place")),
            Before = MiniJson.Num(o, "before") > 0, Noticed = MiniJson.Num(o, "noticed") > 0,
        };
        static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

        /// <summary>The local file's form (LocalTotals, top-level "feats"): {"primed":1,"seen":n,"earned":{id:[moment,..]},"counts":{key:n}}.</summary>
        public void WriteTo(Json j, string key = "feats")
        {
            j.Key(key).Open().Num("primed", Primed ? 1 : 0).Num("seen", Seen);
            j.Key("earned").Open();
            foreach (var kv in Earned.Take(MaxFeats))
            {
                j.Key(kv.Key).OpenArr();
                foreach (var m in kv.Value.Take(MaxTiers)) WriteMoment(j, m, true);
                j.CloseArr();
            }
            j.Close();
            j.Key("counts").Open();
            foreach (var kv in Counts.Take(MaxCounts)) j.Num(kv.Key, kv.Value);
            j.Close();
            WriteBests(j, "bests");
            j.Close();
        }

        void WriteBests(Json j, string key)
        {
            if (Bests.Count == 0) return;
            j.Key(key).Open();
            foreach (var kv in Bests.Take(MaxBests))
            {
                j.Key(kv.Key).Open().Num("v", kv.Value.Value);
                if (kv.Value.Utc > DateTime.MinValue) j.Str("utc", Iso(kv.Value.Utc));
                if (!string.IsNullOrEmpty(kv.Value.Biome)) j.Str("biome", kv.Value.Biome);
                if (!string.IsNullOrEmpty(kv.Value.What)) j.Str("what", kv.Value.What);
                j.Close();
            }
            j.Close();
        }

        void ReadBests(Dictionary<string, object> o)
        {
            if (o == null) return;
            foreach (var kv in o)
            {
                if (Bests.Count >= MaxBests) break;
                if (!(kv.Value is Dictionary<string, object> e)) continue;
                var v = MiniJson.Num(e, "v");
                if (v > 0) Bests[kv.Key] = new BestMark { Value = v, Utc = ParseUtc(MiniJson.Str(e, "utc")), Biome = NullIfEmpty(MiniJson.Str(e, "biome")), What = NullIfEmpty(MiniJson.Str(e, "what")) };
            }
        }

        /// <summary>The shared form of the bests (Snapshot, key "featBests"): key -> {v, utc, biome, what}. Nothing when there are none.</summary>
        public void WriteSharedBests(Json j, string key = "featBests")
        {
            if (Bests.Count > 0) { WriteBests(j, key); return; }
            j.Key(key).Open().Close();   // an empty object still says this sender counts them (a best may simply not exist yet)
        }

        /// <summary>True for a fellow's ledger whose sender shares the bests (it counts them, even when it has none yet).</summary>
        public bool BestsShared;

        /// <summary>A fellow's shared "featBests" read back into this ledger (their numbers, no counters).</summary>
        public void ReadSharedBests(Dictionary<string, object> o) { if (o == null) return; BestsShared = true; ReadBests(o); }

        public static FeatsLedger ReadFrom(Dictionary<string, object> o)
        {
            var l = new FeatsLedger();
            if (o == null) return l;
            l.Primed = MiniJson.Num(o, "primed") > 0; l.Seen = (int)MiniJson.Num(o, "seen");
            var earned = MiniJson.Obj(o, "earned");
            if (earned != null)
                foreach (var kv in earned)
                {
                    if (l.Earned.Count >= MaxFeats) break;
                    if (!(kv.Value is List<object> list)) continue;
                    var moments = new List<FeatMoment>();
                    foreach (var m in list.OfType<Dictionary<string, object>>().Take(MaxTiers)) moments.Add(ReadMoment(m));
                    if (moments.Count > 0) l.Earned[kv.Key] = moments;
                }
            var counts = MiniJson.Obj(o, "counts");
            if (counts != null)
                foreach (var kv in counts)
                    if (kv.Value is double v && l.Counts.Count < MaxCounts) l.Counts[kv.Key] = v;
            l.ReadBests(MiniJson.Obj(o, "bests"));
            return l;
        }

        /// <summary>The shared form (Snapshot, key "feats"): id -> {tier, utc, biome} of the highest tier. Nothing when nothing is earned.</summary>
        public void WriteShared(Json j, string key = "feats")
        {
            if (Earned.Count == 0) return;
            j.Key(key).Open();
            foreach (var kv in Earned.Take(MaxFeats))
            {
                if (kv.Value.Count == 0) continue;
                var m = kv.Value[kv.Value.Count - 1];
                j.Key(kv.Key).Open().Num("tier", kv.Value.Count);
                if (m.Utc > DateTime.MinValue) j.Str("utc", Iso(m.Utc));
                if (!string.IsNullOrEmpty(m.Biome)) j.Str("biome", m.Biome);
                if (m.Before) j.Num("before", 1);
                if (m.Noticed) j.Num("noticed", 1);
                j.Close();
            }
            j.Close();
        }

        /// <summary>A fellow's shared "feats" read back: Earned lists as long as the tier, the earlier tiers' moments not known.</summary>
        public static FeatsLedger ReadShared(Dictionary<string, object> o)
        {
            var l = new FeatsLedger();
            if (o == null) return l;
            foreach (var kv in o)
            {
                if (l.Earned.Count >= MaxFeats) break;
                if (!(kv.Value is Dictionary<string, object> e)) continue;
                var tier = (int)Math.Max(0, Math.Min(MaxTiers, MiniJson.Num(e, "tier")));
                if (tier < 1) continue;
                var list = new List<FeatMoment>();
                for (int k = 1; k < tier; k++) list.Add(new FeatMoment());
                list.Add(ReadMoment(e));
                l.Earned[kv.Key] = list;
            }
            return l;
        }
    }

    /// <summary>
    /// The small counters the feat hooks add to (ClientHooks-style, FeatsHooks.cs), kept apart from the hooks so the rules are
    /// plain C# and tested. Counted on the player's own PC, since install, in the FeatsLedger's Counts.
    /// </summary>
    public static class FeatsCounters
    {
        public const string BlocksNear = "shieldHitsNear", Stopped = "damageStopped", BestStreak = "parryStreakBest", BossParries = "bossParries";
        /// <summary>Parries in a row since the last hit that landed on you (this session; a new connection starts again at 0).</summary>
        public static int Streak;
        /// <summary>A fellow player this close (metres) makes a held block count for the shield wall.</summary>
        public const float NearMetres = 15f;

        /// <summary>
        /// One block that held (Humanoid.BlockAttack returned true and the guard was not broken). <paramref name="stopped"/> = the
        /// blockable damage before the block minus what was left after it (before your armour). A parry (the game's own rule) adds to
        /// the streak and, when the attacker is a boss, to the boss parries.
        /// </summary>
        public static void OnBlock(FeatsLedger l, bool parry, bool bossAttacker, bool fellowNear, float stopped)
        {
            if (l == null) return;
            if (fellowNear) l.Add(BlocksNear, 1);
            if (stopped > 0f) l.Add(Stopped, stopped);
            if (!parry) return;
            Streak++;
            l.Max(BestStreak, Streak);
            if (bossAttacker) l.Add(BossParries, 1);
        }

        /// <summary>A hit that landed on you (damage after the block above zero, from something that attacked): the streak is over.
        /// Burning and poison ticks carry no attacker and do not break it.</summary>
        public static void OnHurt(bool hadAttacker, float damage)
        {
            if (hadAttacker && damage > 0.5f) Streak = 0;
        }
    }
}
