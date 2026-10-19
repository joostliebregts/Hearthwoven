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
        public const int MaxFeats = 160, MaxTiers = 3, MaxCounts = 32, MaxBests = 8;   // 0.9.2 Together batch 3: 118 feats in the table (128 left too little room), room to grow
        public bool Primed;
        /// <summary>Earned tiers seen: Seen of the player's own feats (on Earned), SeenGroup of the group's feats (on Together). Each page
        /// answers its own gold dot, so opening Earned never clears a group tier's dot.</summary>
        public int Seen, SeenGroup;
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
        /// <summary>Earned tiers of the group's feats (group = true) or the player's own (false); isGroup says which ids are group feats (the panel's FeatDefs).</summary>
        public int TiersEarnedOf(Func<string, bool> isGroup, bool group) => Earned.Where(kv => isGroup(kv.Key) == group).Sum(kv => kv.Value.Count);
        /// <summary>Earned tiers not seen yet on their own page (the gold dot): Earned for the player's own, Together for the group's. 0.9 (New feats):
        /// read from the tiers opened (SeenTiers) once that record exists; before it (a file not yet loaded by 0.9, a test ledger) from the old counts.</summary>
        public int UnseenOf(Func<string, bool> isGroup, bool group) => SeenTiers != null && isGroup != null ? UnseenTiers(isGroup, group).Count : CountedUnseenOf(isGroup, group);
        /// <summary>The 0.8.6 rule: earned tiers of a side less the count seen (Seen, SeenGroup). The migration reads it.</summary>
        int CountedUnseenOf(Func<string, bool> isGroup, bool group) => Math.Max(0, TiersEarnedOf(isGroup, group) - (group ? SeenGroup : Seen));
        /// <summary>Marks one side seen: own (Earned was opened) or group (Together was opened). 0.9: every earned tier of that side goes into SeenTiers too.</summary>
        public void MarkSeenOf(Func<string, bool> isGroup, bool group)
        {
            var n = TiersEarnedOf(isGroup, group); if (group) SeenGroup = n; else Seen = n;
            if (SeenTiers != null) foreach (var kv in Earned.Where(kv => isGroup(kv.Key) == group)) for (int t = 1; t <= kv.Value.Count; t++) SeenTiers.Add(TierKey(kv.Key, t));
        }

        // ---------- 0.9, record 4: feats seen per tier (work/hearthwoven-0.9/DESIGN.md section 1) ----------

        /// <summary>0.9: the earned tiers you opened, as "featId:tier" (TierKey); at most one per earned tier (MaxFeats x MaxTiers). null: not
        /// recorded (a fellow's copy, or your file before its first load with 0.9: MigrateSeen fills it). Kept in the local totals at top level
        /// ("featsSeen", LocalTotals), so an older Hearthwoven keeps it; the old Seen and SeenGroup counts stay beside it and follow it.</summary>
        public HashSet<string> SeenTiers;
        public static string TierKey(string id, int tier) => id + ":" + tier.ToString(CultureInfo.InvariantCulture);

        /// <summary>Whether a tier was opened; true when not recorded (null SeenTiers: nothing is called new without a record).</summary>
        public bool IsSeen(string id, int tier) => SeenTiers == null || SeenTiers.Contains(TierKey(id, tier));

        /// <summary>
        /// 0.9's first load (SeenTiers null): the tiers earned before it become seen, except the N most recently earned of each side, N = that
        /// side's old UnseenOf count (Earned's and Together's gold dots), when their earned dates are known (an exact moment, not "before" or
        /// "noticed"); a tier without a date is seen. Nothing unseen when no date exists. True when it ran (the caller saves).
        /// </summary>
        public bool MigrateSeen(Func<string, bool> isGroup)
        {
            if (SeenTiers != null || isGroup == null) return false;
            var counted = new[] { CountedUnseenOf(isGroup, false), CountedUnseenOf(isGroup, true) };   // the old counts, before the set exists
            SeenTiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in new[] { false, true })
            {
                var n = counted[group ? 1 : 0];
                var dated = new List<(string key, DateTime utc)>();
                foreach (var kv in Earned.Where(kv => isGroup(kv.Key) == group))
                    for (int t = 1; t <= kv.Value.Count; t++)
                    {
                        var m = kv.Value[t - 1];
                        if (m.Utc > DateTime.MinValue && !m.Before && !m.Noticed) dated.Add((TierKey(kv.Key, t), m.Utc));
                        else SeenTiers.Add(TierKey(kv.Key, t));
                    }
                var unseen = new HashSet<string>(dated.OrderByDescending(d => d.utc).ThenBy(d => d.key, StringComparer.Ordinal).Take(n).Select(d => d.key));
                foreach (var d in dated) if (!unseen.Contains(d.key)) SeenTiers.Add(d.key);
            }
            return true;
        }

        /// <summary>One tier opened (its card): into SeenTiers, and the side's old count follows (Seen or SeenGroup = the seen tiers of that side),
        /// so an older Hearthwoven's gold dot agrees. True when it was new.</summary>
        public bool MarkTierSeen(string id, int tier, Func<string, bool> isGroup)
        {
            if (SeenTiers == null || string.IsNullOrEmpty(id) || tier < 1 || Tier(id) < tier) return false;
            if (!SeenTiers.Add(TierKey(id, tier))) return false;
            if (isGroup != null) { var group = isGroup(id); var n = SeenCountOf(isGroup, group); if (group) SeenGroup = n; else Seen = n; }
            return true;
        }

        int SeenCountOf(Func<string, bool> isGroup, bool group) =>
            Earned.Where(kv => isGroup(kv.Key) == group).Sum(kv => Enumerable.Range(1, kv.Value.Count).Count(t => SeenTiers.Contains(TierKey(kv.Key, t))));

        /// <summary>The earned tiers of one side not opened yet, newest first (by their earned moment; undated last): (id, tier, moment). Empty when
        /// not recorded.</summary>
        public List<(string id, int tier, FeatMoment moment)> UnseenTiers(Func<string, bool> isGroup, bool group)
        {
            var list = new List<(string, int, FeatMoment)>();
            if (SeenTiers == null || isGroup == null) return list;
            foreach (var kv in Earned.Where(kv => isGroup(kv.Key) == group))
                for (int t = 1; t <= kv.Value.Count; t++) if (!SeenTiers.Contains(TierKey(kv.Key, t))) list.Add((kv.Key, t, kv.Value[t - 1]));
            return list.OrderByDescending(x => x.Item3.Utc).ThenBy(x => x.Item1, StringComparer.Ordinal).ThenByDescending(x => x.Item2).ToList();
        }

        /// <summary>SeenTiers as stored: only tiers still earned, at most MaxFeats x MaxTiers.</summary>
        public void WriteSeenTiers(Json j, string key = "featsSeen")
        {
            if (SeenTiers == null) return;
            j.Key(key).OpenArr();
            foreach (var k in SeenTiers.Where(StillEarned).OrderBy(k => k, StringComparer.Ordinal).Take(MaxFeats * MaxTiers)) j.Val(k);
            j.CloseArr();
        }
        bool StillEarned(string key)
        {
            var i = key.LastIndexOf(':');
            return i > 0 && int.TryParse(key.Substring(i + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) && t >= 1 && Tier(key.Substring(0, i)) >= t;
        }

        /// <summary>The stored SeenTiers read back (bounded, only tiers this ledger has earned); null when the file has none (MigrateSeen runs).</summary>
        public void ReadSeenTiers(object o)
        {
            if (!(o is List<object> list)) return;
            SeenTiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k in list.OfType<string>().Take(MaxFeats * MaxTiers * 2)) if (StillEarned(k)) SeenTiers.Add(k);
        }
        /// <summary>Marks both sides seen (samples and tests).</summary>
        public void MarkSeen(Func<string, bool> isGroup) { MarkSeenOf(isGroup, false); MarkSeenOf(isGroup, true); }

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

        public bool IsEmpty => !Primed && Earned.Count == 0 && Counts.Count == 0 && Bests.Count == 0 && Seen == 0 && SeenGroup == 0;

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

        /// <summary>The local file's form (LocalTotals, top-level "feats"): {"primed":1,"seen":n,"seenGroup":n,"earned":{id:[moment,..]},"counts":{key:n}}.</summary>
        public void WriteTo(Json j, string key = "feats")
        {
            j.Key(key).Open().Num("primed", Primed ? 1 : 0).Num("seen", Seen);
            if (SeenGroup > 0) j.Num("seenGroup", SeenGroup);
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
            l.Primed = MiniJson.Num(o, "primed") > 0; l.Seen = (int)MiniJson.Num(o, "seen"); l.SeenGroup = (int)MiniJson.Num(o, "seenGroup");
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
        /// <summary>0.9 more feats: the best run of dishes taken off unburnt, the longest stint at the helm with a full crew (seconds), the most held
        /// blocks near a fellow in one fight you did not fall in, altars that answered you, repairs on a fellow's pieces, raids you hit a foe in (and
        /// the last raid's key, so one raid counts once).</summary>
        public const string GrillBest = "grillStreakBest", CrewBest = "crewBestSeconds", HeldLineBest = "heldLineBest", Summons = "altarSummons",
                            RepairsOthers = "repairsOthers", Raids = "raidsFought", RaidLast = "raidLast";
        /// <summary>0.9.1 feats, batch 2: the longest stint at the helm through a storm (seconds), the most lands set foot in in one session, the farthest
        /// from the world's middle (metres: how far, never where), the metres ridden in a saddle.</summary>
        public const string StormBest = "stormBestSeconds", LandsBest = "landsOneSession", FarBest = "farthestMeters", Ridden = "riddenMeters";
        /// <summary>This session's storm stint and the lands set foot in (a new connection starts them again: NewSession).</summary>
        public static float StormStint;
        public static readonly HashSet<string> SessionLands = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Fellow players aboard (besides you at the helm) that make a whole crew.</summary>
        public const int CrewAboard = 3;
        /// <summary>This session's runs: dishes off unburnt in a row, seconds at the helm with a whole crew, held blocks near a fellow in the fight on
        /// now and whether you fell in it.</summary>
        public static int GrillRun, LineRun; public static float CrewStint; public static bool LineFell;
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
            if (fellowNear) { l.Add(BlocksNear, 1); LineRun++; }   // 0.9: Held the Line, this fight's run
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

        /// <summary>A dish you took off a cooking station (ClientHooks.TakeOffAmount): a burnt one ends the run, an unburnt one adds to it (Steady at the Grill).</summary>
        public static void OnTakenOff(FeatsLedger l, bool burnt)
        {
            if (l == null) return;
            if (burnt) { GrillRun = 0; return; }
            GrillRun++; l.Max(GrillBest, GrillRun);
        }

        /// <summary>One look at the ship (every 10 s, ClientHooks.SampleVoyage): at the helm with CrewAboard or more fellow players aboard the stint
        /// grows by <paramref name="seconds"/>, anything else ends it (Whole Crew).</summary>
        public static void OnCrewSample(FeatsLedger l, bool atHelm, int othersAboard, float seconds)
        {
            if (l == null) return;
            if (!atHelm || othersAboard < CrewAboard || !(seconds > 0f)) { CrewStint = 0f; return; }
            CrewStint += seconds; l.Max(CrewBest, CrewStint);
        }

        /// <summary>You fell (ClientHooks.Died): the fight on now does not hold the line.</summary>
        public static void OnFell() => LineFell = true;

        /// <summary>No fight on (BattleHooks.Tick, the battle record's rule: a minute with no hit): the fight's held blocks near a fellow count for Held the
        /// Line when you did not fall in it; then a new fight begins.</summary>
        public static void OnFightOver(FeatsLedger l)
        {
            if (l != null && LineRun > 0 && !LineFell) l.Max(HeldLineBest, LineRun);
            LineRun = 0; LineFell = false;
        }

        /// <summary>An altar answered your offering (Before the Altar).</summary>
        public static void OnSummoned(FeatsLedger l) => l?.Add(Summons, 1);

        /// <summary>A repair with your hammer (Mender of Others): it counts when the piece has a builder and it is not you.</summary>
        public static void OnRepaired(FeatsLedger l, long creator, long you)
        {
            if (l != null && creator != 0L && creator != you) l.Add(RepairsOthers, 1);
        }

        /// <summary>A raid's key: the game's event name and where it was set (its centre, whole metres): one raid, one key.</summary>
        public static string RaidKey(string name, float x, float z) =>
            string.IsNullOrEmpty(name) ? null : name + "|" + Math.Round(x).ToString(CultureInfo.InvariantCulture) + "|" + Math.Round(z).ToString(CultureInfo.InvariantCulture);

        /// <summary>Your hit landed on a foe while a raid was on around you (Raid Breaker): each raid once, by its key (kept as a number in the ledger,
        /// so a restart in the middle of a raid does not count it again). True when the raid is new.</summary>
        public static bool OnRaidHit(FeatsLedger l, string key)
        {
            if (l == null || string.IsNullOrEmpty(key)) return false;
            var h = KeyHash(key);
            if (l.Count(RaidLast) == h) return false;
            if (!l.Counts.ContainsKey(RaidLast) && l.Counts.Count >= FeatsLedger.MaxCounts) return false;
            l.Counts[RaidLast] = h; l.Add(Raids, 1);
            return true;
        }

        /// <summary>A new connection (Plugin's NewConnection): this session's runs start again: the parry streak, the storm stint, the lands of One Long Road.</summary>
        public static void NewSession() { Streak = 0; StormStint = 0f; SessionLands.Clear(); }

        /// <summary>The game's weather is a storm by its own name ("ThunderStorm", "SnowStorm", "Ashlands_storm", "Mistlands_thunder").</summary>
        public static bool IsStorm(string environment) => !string.IsNullOrEmpty(environment) &&
            (environment.IndexOf("storm", StringComparison.OrdinalIgnoreCase) >= 0 || environment.IndexOf("thunder", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>One look (every 10 s, FeatsHooks.SampleWorld): at the helm in a storm the stint grows by <paramref name="seconds"/>, anything else ends it
        /// (Through the Storm).</summary>
        public static void OnStormSample(FeatsLedger l, bool atHelm, bool storm, float seconds)
        {
            if (l == null) return;
            if (!atHelm || !storm || !(seconds > 0f)) { StormStint = 0f; return; }
            StormStint += seconds; l.Max(StormBest, StormStint);
        }

        /// <summary>The land under your feet (One Long Road): each land once this session; the sea and no land at all are not lands here.</summary>
        public static void OnLand(FeatsLedger l, string biome)
        {
            if (l == null || string.IsNullOrEmpty(biome) || biome == "None" || biome == "Ocean") return;
            if (SessionLands.Add(biome)) l.Max(LandsBest, SessionLands.Count);
        }

        /// <summary>No reading beyond this many metres from the middle is a place in a vanilla world (its edge is at 10.5 km): left out.</summary>
        public const float WorldEdgeMetres = 12000f;
        /// <summary>How far you stand from the world's middle (Far Horizon): the farthest, whole metres; where is never kept.</summary>
        public static void OnFar(FeatsLedger l, float x, float z)
        {
            if (l == null) return;
            var d = Math.Sqrt((double)x * x + (double)z * z);
            if (double.IsNaN(d) || double.IsInfinity(d) || d > WorldEdgeMetres) return;
            l.Max(FarBest, Math.Floor(d));
        }

        /// <summary>A step longer than this between two looks 10 s apart is a teleport or a reload, not a ride.</summary>
        public const float RideStepMax = 200f;
        /// <summary>Metres ridden in a saddle since the last look (Saddled).</summary>
        public static void OnRide(FeatsLedger l, float metres)
        {
            if (l == null || !(metres > 0f) || metres > RideStepMax) return;
            l.Add(Ridden, metres);
        }

        /// <summary>A stable number for a key (FNV-1a, 32 bits, never 0).</summary>
        public static double KeyHash(string key)
        {
            uint h = 2166136261;
            foreach (var c in key) { h ^= c; h *= 16777619; }
            return (double)h + 1;
        }
    }
}
