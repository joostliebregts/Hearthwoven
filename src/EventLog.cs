using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hearthwoven
{
    /// <summary>
    /// Measured since install, per session, with WHEN and WHERE: damage in 10-minute buckets per biome, enemy, cause and
    /// damage type (dealt and taken), and every death with its biome, killer and the damage of its last 10 seconds.
    /// Absolute per session like everything measured: a resend replaces, never adds. Pure C#, unit-tested.
    /// </summary>
    public class EventLog
    {
        public const int BucketMinutes = 10, MaxDeaths = 200, MaxBuckets = 4000;
        // "2026-10-07T20:10Z|Swamp|taken|Draugr|EnemyHit|slash" -> damage
        public readonly Dictionary<string, float> Damage = new Dictionary<string, float>();
        // same key without the type -> number of hits
        public readonly Dictionary<string, float> Hits = new Dictionary<string, float>();
        public readonly List<Death> Deaths = new List<Death>();
        public int DeathsNotListed;   // beyond MaxDeaths: counted, never silently lost
        readonly Queue<Recent> recent = new Queue<Recent>();

        public class Death
        {
            public DateTime Time; public string Biome, Killer, Cause; public float X, Z;
            public readonly Dictionary<string, float> Last10s = new Dictionary<string, float>();   // "source|cause|type" -> damage
        }
        struct Recent { public DateTime T; public string Source, Cause; public HitData.DamageTypes D; }

        public static string Bucket(DateTime utc)
        {
            var t = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / BucketMinutes * BucketMinutes, 0, DateTimeKind.Utc);
            return t.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) + "Z";
        }

        public void AddDamage(DateTime utc, string biome, bool dealt, string other, string cause, HitData.DamageTypes d)
        {
            var head = Bucket(utc) + "|" + (biome ?? "None") + "|" + (dealt ? "dealt" : "taken") + "|" + (other ?? "?") + "|" + (cause ?? "?");
            if (!Hits.ContainsKey(head) && Hits.Count >= MaxBuckets) head = Bucket(utc) + "|" + (biome ?? "None") + "|" + (dealt ? "dealt" : "taken") + "|other|other";
            Hits.TryGetValue(head, out var h); Hits[head] = h + 1;
            Each(d, (type, v) => { var k = head + "|" + type; Damage.TryGetValue(k, out var o); Damage[k] = o + v; });
            if (!dealt)
            {
                recent.Enqueue(new Recent { T = utc, Source = other, Cause = cause, D = d });
                while (recent.Count > 0 && (utc - recent.Peek().T).TotalSeconds > 10) recent.Dequeue();
            }
        }

        public Death AddDeath(DateTime utc, string biome, float x, float z)
        {
            var death = new Death { Time = utc, Biome = biome ?? "None", X = x, Z = z };
            Recent last = default; bool any = false;
            foreach (var r in recent)
            {
                if ((utc - r.T).TotalSeconds > 10) continue;
                Each(r.D, (type, v) => { var k = r.Source + "|" + r.Cause + "|" + type; death.Last10s.TryGetValue(k, out var o); death.Last10s[k] = o + v; });
                last = r; any = true;
            }
            death.Killer = any ? last.Source : "unknown";
            death.Cause = any ? last.Cause : "unknown";
            recent.Clear();
            if (Deaths.Count < MaxDeaths) Deaths.Add(death); else DeathsNotListed++;
            return death;
        }

        static void Each(HitData.DamageTypes d, Action<string, float> f)
        {
            void A(string k, float v) { if (v != 0f) f(k, v); }
            A("blunt", d.m_blunt); A("slash", d.m_slash); A("pierce", d.m_pierce); A("chop", d.m_chop); A("pickaxe", d.m_pickaxe);
            A("fire", d.m_fire); A("frost", d.m_frost); A("lightning", d.m_lightning); A("poison", d.m_poison); A("spirit", d.m_spirit); A("damage", d.m_damage);
        }

        public void WriteTo(Json j)
        {
            j.Key("measuredLog").Open().Num("bucketMinutes", BucketMinutes).Num("deathsNotListed", DeathsNotListed).Dict("damage", Damage).Dict("hits", Hits);
            j.Key("deaths").OpenArr();
            foreach (var d in Deaths)
                j.Open().Str("t", d.Time.ToString("o", CultureInfo.InvariantCulture)).Str("biome", d.Biome).Str("killer", d.Killer).Str("cause", d.Cause)
                 .Num("x", d.X).Num("z", d.Z).Dict("last10s", d.Last10s).Close();
            j.CloseArr().Close();
        }
    }
}
