using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hearthwoven
{
    /// <summary>
    /// The deeds of this session per minute (0.7, Deeds > Recent): what you picked up, made, planted, felled, hit with the axe and the
    /// pickaxe, the skill practice, and from the game's own counters the pieces placed, plants and fish picked, foes defeated per kind and a
    /// few totals (kills, boss kills, fish caught, creatures tamed). Like the damage buckets of EventLog: one-minute buckets, folded into
    /// ten-minute buckets once they are older than EventLog.FoldAfterMinutes, this session only, never saved or shared.
    ///
    /// One source of truth: a bucket is a TIME SPLIT of the session tally, never a second count. Nothing is recorded in the hooks: at each
    /// minute change (Tick, from Plugin.Update) and when the panel reads it (Flush), the growth of the session tallies since the last flush
    /// is booked into that minute. So the buckets of a family add up to its session tally exactly: SessionEvents' families (Plugin.Events,
    /// which start at zero with the session), and the game's counters' growth since the first flush of the session (the session's mark).
    /// What the session tallies do not count, the buckets cannot hold. Cost: nothing per event; per frame one comparison of the minute;
    /// per flush one pass over the families' keys (DeedLogTests measures it).
    ///
    /// Keys: "2026-10-09T20:14Z|family|token" (the time part as EventLog's, so EventLog.FoldKeys folds them). Bounded: one minute keeps at
    /// most MaxKeysPerBucket tokens by name (the largest; the rest of a family goes to its OtherToken, one per family, so every family's sum
    /// stays exact), the log at most MaxKeys named keys (past it, a new token goes to its family's OtherToken). Pure C#, unit-tested (test/DeedLogTests.cs).
    /// </summary>
    public class DeedLog
    {
        public const int MaxKeysPerBucket = 40, MaxKeys = 4000;
        /// <summary>The token that holds what a capped bucket could not keep by name: counted, never lost ("a few small kinds").</summary>
        public const string OtherToken = "~other";

        /// <summary>The families taken from SessionEvents (their JSON names, SessionEvents.Named).</summary>
        public const string PickedUp = "pickedUp", Made = "made", Planted = "planted", Felled = "treesFelled", ChopHits = "chopHits", PickaxeHits = "pickaxeHits", Skills = "skillPractice";
        /// <summary>0.8: materials recovered from pieces that came down (Building's short windows).</summary>
        public const string Recovered = "recovered";
        public static readonly string[] EventFamilies = { PickedUp, Made, Planted, Felled, ChopHits, PickaxeHits, Skills, Recovered };
        static readonly HashSet<string> EventSet = new HashSet<string>(EventFamilies);

        /// <summary>The families taken from the game's counters (GameKeys): pieces placed (built, groundwork, planted, feasts: PieceKind
        /// tells them apart), plants and fish picked (m_pickableStats), foes defeated per kind (m_enemyStats table 0), and Counters.</summary>
        public const string Placed = "placed", Picked = "picked", Kills = "kills", Counters = "counters";
        public static readonly string[] GameFamilies = { Placed, Picked, Kills, Counters };
        /// <summary>The game's whole-number counters kept under Counters (PlayerStatType names).</summary>
        public static readonly string[] CounterStats = { "EnemyKills", "BossKills", "FishCaught", "CreatureTamed" };
        public static readonly string[] AllFamilies = { PickedUp, Made, Planted, Felled, ChopHits, PickaxeHits, Skills, Recovered, Placed, Picked, Kills, Counters };

        /// <summary>"2026-10-09T20:14Z|family|token" -> amount booked in that minute (ten minutes once folded).</summary>
        public readonly Dictionary<string, float> Buckets = new Dictionary<string, float>();
        /// <summary>Whether a bucket was capped (a few small kinds sit in OtherToken).</summary>
        public bool Clipped;

        // what was booked so far: SessionEvents per "family|token" (from zero), the game's counters as they stood at the last flush
        readonly Dictionary<string, float> markEvents = new Dictionary<string, float>();
        Dictionary<string, float> markGame, startGame;
        long lastMinute;          // the minute of the last Tick (DateTime ticks / TicksPerMinute); 0 = not ticked yet
        string foldCut = "";      // buckets before this one are folded
        string lastFold = "";

        /// <summary>The game's counters as they stood at this session's first flush ("family|token" -> value); null = no profile seen yet.
        /// The game families' session tally is their counter now minus this.</summary>
        public IDictionary<string, float> GameAtStart => startGame;

        /// <summary>
        /// Called every frame while playing: when the minute changed since the last call, the growth since the last flush is booked into the
        /// minute that ended (and folding runs). The first call takes the game's mark. <paramref name="game"/> is asked only then (GameKeys).
        /// </summary>
        public void Tick(DateTime utc, SessionEvents events, Func<IDictionary<string, float>> game)
        {
            var m = utc.Ticks / TimeSpan.TicksPerMinute;
            if (m == lastMinute) return;
            var ended = lastMinute == 0 ? utc : new DateTime(lastMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
            lastMinute = m;
            Flush(ended, events, game?.Invoke());
            Fold(utc);
        }

        /// <summary>Books what the session tallies grew by since the last flush into the minute of <paramref name="bucketUtc"/>. The panel calls it
        /// with now before it reads, so the running minute is in. <paramref name="game"/> null (no profile): only SessionEvents' families.</summary>
        public void Flush(DateTime bucketUtc, SessionEvents events, IDictionary<string, float> game)
        {
            var bucket = EventLog.Bucket(bucketUtc);
            List<KeyValuePair<string, float>> grew = null;
            if (events != null)
                foreach (var fam in events.Named())
                {
                    if (!EventSet.Contains(fam.Key)) continue;
                    var head = fam.Key + "|";
                    foreach (var kv in fam.Value)
                    {
                        var k = KeyJoin.Of(head, kv.Key);   // 0.8.1: joined once, not on every refresh
                        markEvents.TryGetValue(k, out var was);
                        if (kv.Value == was) continue;
                        markEvents[k] = kv.Value;
                        (grew ?? (grew = new List<KeyValuePair<string, float>>())).Add(new KeyValuePair<string, float>(k, kv.Value - was));
                    }
                }
            if (game != null)
            {
                if (markGame == null) { markGame = new Dictionary<string, float>(game); startGame = new Dictionary<string, float>(game); }   // the session's mark: what was there before is not this session's
                else
                    foreach (var kv in game)
                    {
                        if (!markGame.TryGetValue(kv.Key, out var was)) { was = 0f; startGame[kv.Key] = 0f; }   // a token new this session stood at 0
                        if (kv.Value == was) continue;
                        markGame[kv.Key] = kv.Value;
                        (grew ?? (grew = new List<KeyValuePair<string, float>>())).Add(new KeyValuePair<string, float>(kv.Key, kv.Value - was));
                    }
            }
            if (grew != null) Book(bucket, grew);
        }

        void Book(string bucket, List<KeyValuePair<string, float>> grew)
        {
            // the bucket's named keys as they are, then the new ones largest first: past the caps a family's small kinds go to its OtherToken
            int named = 0;
            if (bucket == countedBucket) named = countedKeys;   // the flushes of one minute: counted once
            else foreach (var k in Buckets.Keys) if (k.Length > bucket.Length && k[bucket.Length] == '|' && string.CompareOrdinal(k, 0, bucket, 0, bucket.Length) == 0 && !k.EndsWith(OtherToken, StringComparison.Ordinal)) named++;
            grew.Sort((a, b) => Math.Abs(b.Value).CompareTo(Math.Abs(a.Value)));
            foreach (var g in grew)
            {
                var key = bucket + "|" + g.Key;
                if (!Buckets.ContainsKey(key))
                {
                    if (named >= MaxKeysPerBucket || Buckets.Count >= MaxKeys) { key = bucket + "|" + g.Key.Substring(0, g.Key.IndexOf('|')) + "|" + OtherToken; Clipped = true; }
                    else named++;
                }
                Buckets.TryGetValue(key, out var o); Buckets[key] = o + g.Value;
            }
            countedBucket = bucket; countedKeys = named;
        }
        string countedBucket; int countedKeys;

        void Fold(DateTime utc)
        {
            var now = EventLog.Bucket(utc); if (now == lastFold) return;
            lastFold = now;
            foldCut = EventLog.Bucket(utc.AddMinutes(-EventLog.FoldAfterMinutes));
            EventLog.FoldKeys(Buckets, foldCut);
            countedBucket = null;
        }

        /// <summary>The minutes a bucket covers: ten for a folded one (older than the fold, on a whole ten minutes), else one.</summary>
        int SpanOf(string key) => key.Length > 16 && key[15] == '0' && string.CompareOrdinal(key, 0, foldCut, 0, 17) < 0 ? EventLog.FoldedMinutes : EventLog.BucketMinutes;

        /// <summary>
        /// What grew from <paramref name="fromUtc"/> on (null: the whole session), per family and token, only what grew (a sum above 0). A
        /// bucket counts when any part of it lies after <paramref name="fromUtc"/> (at most a minute more; ten for a folded one, which only
        /// windows longer than EventLog.FoldAfterMinutes reach).
        /// </summary>
        // a bucket key's parts, read once per key (the keys never change; bounded like Panel.LogKeys)
        sealed class KeyParts { public bool HasTime; public DateTime Time; public string Family, Token; }
        static readonly Dictionary<string, KeyParts> keyParts = new Dictionary<string, KeyParts>(StringComparer.Ordinal);
        static KeyParts KeyOf(string k)
        {
            lock (keyParts)
            {
                if (keyParts.TryGetValue(k, out var p)) return p;
                p = new KeyParts();
                if (k.Length >= 17) p.HasTime = DateTime.TryParseExact(k.Substring(0, 17), "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out p.Time);
                var bar = k.Length > 18 ? k.IndexOf('|', 18) : -1;
                if (bar >= 0) { p.Family = k.Substring(18, bar - 18); p.Token = k.Substring(bar + 1); }
                if (keyParts.Count >= 60000) keyParts.Clear();
                keyParts[k] = p;
                return p;
            }
        }

        public Dictionary<string, Dictionary<string, float>> Grew(DateTime? fromUtc)
        {
            var sums = new Dictionary<string, Dictionary<string, float>>();
            foreach (var kv in Buckets)
            {
                var k = kv.Key;
                var p = KeyOf(k);   // 0.8: the key's minute, family and token read once, not on every build (the panel asks every 2 s while open)
                if (fromUtc.HasValue)
                {
                    if (!p.HasTime) continue;
                    if (p.Time.AddMinutes(SpanOf(k)) <= fromUtc.Value) continue;
                }
                if (p.Family == null) continue;
                var fam = p.Family; var token = p.Token;
                if (!sums.TryGetValue(fam, out var d)) sums[fam] = d = new Dictionary<string, float>();
                d.TryGetValue(token, out var o); d[token] = o + kv.Value;
            }
            foreach (var d in sums.Values) foreach (var t in new List<string>(d.Keys)) if (d[t] <= 0f) d.Remove(t);
            foreach (var f in new List<string>(sums.Keys)) if (sums[f].Count == 0) sums.Remove(f);
            return sums;
        }

        /// <summary>
        /// The game's counters this log reads, as "family|token" (Placed, Picked, Kills, Counters) from the profile's per-token tables:
        /// pieces placed, plants and fish picked, kills per foe (table 0) and the CounterStats. Pure: the caller hands over the tables.
        /// </summary>
        public static Dictionary<string, float> GameKeys(IDictionary<string, float> stats, IDictionary<string, float> placed, IDictionary<string, float> picked, IDictionary<string, float> kills)
        {
            var d = new Dictionary<string, float>();
            if (stats != null) foreach (var s in CounterStats) if (stats.TryGetValue(s, out var v)) d[KeyJoin.Of(Counters + "|", s)] = v;   // the keys joined once (KeyJoin, 0.8.1)
            if (placed != null) foreach (var kv in placed) d[KeyJoin.Of(Placed + "|", kv.Key)] = kv.Value;
            if (picked != null) foreach (var kv in picked) d[KeyJoin.Of(Picked + "|", kv.Key)] = kv.Value;
            if (kills != null) foreach (var kv in kills) d[KeyJoin.Of(Kills + "|", kv.Key)] = kv.Value;
            return d;
        }
    }
}
