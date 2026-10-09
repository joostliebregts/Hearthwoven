using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hearthwoven
{
    /// <summary>
    /// Measured since install, per session, with WHEN and WHERE: damage in 10-minute buckets per biome, enemy, cause and
    /// damage type (dealt and taken), and every death with its biome, killer and the damage of its last 30 seconds (TimelineSeconds).
    /// Absolute per session like everything measured: a resend replaces, never adds. Pure C#, unit-tested.
    /// </summary>
    public class EventLog
    {
        public const int BucketMinutes = 1, MaxDeaths = 200, MaxBuckets = 4000;
        /// <summary>Buckets are one minute, so Battle's "last 10 minutes" is a ten-minute window (a bucket counts when any part of it lies
        /// in the window: at most a minute more). Buckets older than FoldAfterMinutes (past every window but This session) are folded
        /// into ten-minute buckets (Fold), which keeps the log, and what is shared, near the size it had.</summary>
        public const int FoldAfterMinutes = 190, FoldedMinutes = 10;
        /// <summary>The bucket length of the keys of a copy read from a fellow player (an older sender: 10, as its JSON says); this PC's own: BucketMinutes.</summary>
        public int Span = BucketMinutes;
        string lastFold = "";
        /// <summary>A death keeps the hits you received in its last TimelineSeconds, with their time, at most MaxTimelineHits
        /// (the latest); the queue of recent hits is bounded by MaxRecent.</summary>
        public const int TimelineSeconds = 30, MaxTimelineHits = 40, MaxRecent = 400;
        // "2026-10-07T20:10Z|Swamp|taken|Draugr|EnemyHit|slash" -> damage
        public readonly Dictionary<string, float> Damage = new Dictionary<string, float>();
        // same key without the type -> number of hits
        public readonly Dictionary<string, float> Hits = new Dictionary<string, float>();
        public readonly List<Death> Deaths = new List<Death>();
        /// <summary>Damage and deaths folded per biome, kept up to date as hits and deaths come in: always what BiomeTally.FromLog(this) gives
        /// for a log filled through AddDamage and AddDeath (the full pass over every key stays as the reference, and for a log read from a snapshot).</summary>
        public readonly BiomeTally Biome = new BiomeTally();
        public int DeathsNotListed;   // beyond MaxDeaths: counted, never silently lost
        readonly Queue<Recent> recent = new Queue<Recent>();

        public class Death
        {
            public DateTime Time; public string Biome, Killer, Cause; public float X, Z;
            public readonly Dictionary<string, float> Last10s = new Dictionary<string, float>();   // "source|cause|type" -> damage
            /// <summary>How the damage built up: every hit received in the last 30 s, oldest first, one entry per damage type
            /// of a hit (Ago = seconds before the death). Empty for a death recorded before this was kept.</summary>
            public readonly List<Hit> Timeline = new List<Hit>();
        }
        public class Hit { public float Ago, Amount; public string Source, Cause, Type; }
        struct Recent { public DateTime T; public string Source, Cause; public HitData.DamageTypes D; }

        public static string Bucket(DateTime utc)
        {
            var t = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / BucketMinutes * BucketMinutes, 0, DateTimeKind.Utc);
            return t.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) + "Z";
        }

        /// <summary>Folds the buckets older than FoldAfterMinutes into ten-minute buckets (the minute's last digit to 0), once per minute.</summary>
        void Fold(DateTime utc)
        {
            var now = Bucket(utc); if (now == lastFold) return;
            lastFold = now;
            var cut = Bucket(utc.AddMinutes(-FoldAfterMinutes));
            FoldKeys(Damage, cut); FoldKeys(Hits, cut);
        }

        static void FoldKeys(Dictionary<string, float> d, string cut)
        {
            List<KeyValuePair<string, float>> move = null;
            foreach (var kv in d)
            {
                var k = kv.Key;   // "2026-10-07T20:14Z|...": the minute's last digit is at 15
                if (k.Length > 16 && k[15] != '0' && string.CompareOrdinal(k, 0, cut, 0, 17) < 0) (move ?? (move = new List<KeyValuePair<string, float>>())).Add(kv);
            }
            if (move == null) return;
            foreach (var kv in move)
            {
                d.Remove(kv.Key);
                var folded = kv.Key.Substring(0, 15) + "0" + kv.Key.Substring(16);
                d.TryGetValue(folded, out var o); d[folded] = o + kv.Value;
            }
        }

        public void AddDamage(DateTime utc, string biome, bool dealt, string other, string cause, HitData.DamageTypes d)
        {
            if (!dealt && DamageTally.TotalOf(d) <= 0f) return;   // a hit fully absorbed (a ward) is no hit received, as in DamageTally (review 0.6.5: the windows still counted it)
            Fold(utc);
            var head = Bucket(utc) + "|" + (biome ?? "None") + "|" + (dealt ? "dealt" : "taken") + "|" + (other ?? "?") + "|" + (cause ?? "?");
            if (!Hits.ContainsKey(head) && Hits.Count >= MaxBuckets) head = Bucket(utc) + "|" + (biome ?? "None") + "|" + (dealt ? "dealt" : "taken") + "|other|other";
            Hits.TryGetValue(head, out var h); Hits[head] = h + 1;
            var biomeName = biome ?? "None";
            Each(d, (type, v) => { var k = head + "|" + type; Damage.TryGetValue(k, out var o); Damage[k] = o + v; Biome.AddDamage(biomeName, dealt, type, v); });
            if (!dealt)
            {
                recent.Enqueue(new Recent { T = utc, Source = other, Cause = cause, D = d });
                while (recent.Count > 0 && ((utc - recent.Peek().T).TotalSeconds > TimelineSeconds || recent.Count > MaxRecent)) recent.Dequeue();
            }
        }

        public Death AddDeath(DateTime utc, string biome, float x, float z)
        {
            var death = new Death { Time = utc, Biome = biome ?? "None", X = x, Z = z };
            Recent last = default; bool any = false;
            var hits = new List<Recent>();
            foreach (var r in recent)
            {
                if ((utc - r.T).TotalSeconds > TimelineSeconds) continue;
                hits.Add(r);
                if ((utc - r.T).TotalSeconds > 10) continue;
                Each(r.D, (type, v) => { var k = r.Source + "|" + r.Cause + "|" + type; death.Last10s.TryGetValue(k, out var o); death.Last10s[k] = o + v; });
                last = r; any = true;
            }
            for (int i = Math.Max(0, hits.Count - MaxTimelineHits); i < hits.Count; i++)
            {
                var r = hits[i]; var ago = (float)Math.Max(0, (utc - r.T).TotalSeconds);
                Each(r.D, (type, v) => death.Timeline.Add(new Hit { Ago = ago, Source = r.Source ?? "?", Cause = r.Cause ?? "?", Type = type, Amount = v }));
            }
            death.Killer = any ? last.Source : "unknown";
            death.Cause = any ? last.Cause : "unknown";
            recent.Clear();
            if (Deaths.Count < MaxDeaths) { Deaths.Add(death); Biome.AddDeath(death.Biome); } else DeathsNotListed++;
            return death;
        }

        static void Each(HitData.DamageTypes d, Action<string, float> f)
        {
            void A(string k, float v) { if (v != 0f && Json.IsFinite(v)) f(k, v); }
            A("blunt", d.m_blunt); A("slash", d.m_slash); A("pierce", d.m_pierce); A("chop", d.m_chop); A("pickaxe", d.m_pickaxe);
            A("fire", d.m_fire); A("frost", d.m_frost); A("lightning", d.m_lightning); A("poison", d.m_poison); A("spirit", d.m_spirit); A("damage", d.m_damage);
        }

        public void WriteTo(Json j)
        {
            j.Key("measuredLog").Open().Num("bucketMinutes", BucketMinutes).Num("deathsNotListed", DeathsNotListed).Dict("damage", Damage).Dict("hits", Hits);
            j.Key("deaths").OpenArr();
            foreach (var d in Deaths)
            {
                j.Open().Str("t", d.Time.ToString("o", CultureInfo.InvariantCulture)).Str("biome", d.Biome).Str("killer", d.Killer).Str("cause", d.Cause)
                 .Num("x", d.X).Num("z", d.Z).Dict("last10s", d.Last10s);
                j.Key("timeline").OpenArr();
                foreach (var h in d.Timeline) j.Open().Num("ago", Math.Round(h.Ago, 2)).Str("source", h.Source).Str("cause", h.Cause).Str("type", h.Type).Num("amount", h.Amount).Close();
                j.CloseArr().Close();
            }
            j.CloseArr().Close();
        }
    }
}
