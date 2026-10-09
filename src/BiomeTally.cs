using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// Damage and deaths per biome, folded: no time, no foe. Damage keys are "biome|dir|type" ("Swamp|dealt|slash", dir dealt or
    /// taken), Deaths is biome -> count. This is what Battle's "All" window needs to keep its biome strip across sessions (the
    /// event log has the biome but lives one session). Bounded by the biome list times two directions times the damage types
    /// (MaxKeys is a hard stop for a mod that invents biomes): the file stays small however long someone plays. Pure C#.
    /// </summary>
    public class BiomeTally
    {
        public const int MaxKeys = 600;
        public readonly Dictionary<string, float> Damage = new Dictionary<string, float>(), Deaths = new Dictionary<string, float>();

        public bool Empty => Damage.Count == 0 && Deaths.Count == 0;

        /// <summary>Counts up on every change made through this class, so a reader can keep a sum and rebuild it only when this moved.</summary>
        public int Version { get; private set; }

        /// <summary>One damage type of one hit, folded straight in (what EventLog.AddDamage does as the hits come in; the same totals FromLog
        /// would give from the whole log, without reading the log again).</summary>
        public void AddDamage(string biome, bool dealt, string type, float v) { Add(Damage, biome + (dealt ? "|dealt|" : "|taken|") + type, v); Version++; }

        /// <summary>One death in this biome.</summary>
        public void AddDeath(string biome) { Add(Deaths, biome ?? "None", 1); Version++; }

        /// <summary>What a session's event log holds, folded to biome, direction and type (the deaths by their biome).</summary>
        public static BiomeTally FromLog(EventLog log)
        {
            var t = new BiomeTally();
            if (log == null) return t;
            foreach (var kv in log.Damage)
            {
                var p = kv.Key.Split('|');   // time|biome|dir|foe|cause|type
                if (p.Length < 6) continue;
                Add(t.Damage, p[1] + "|" + p[2] + "|" + p[5], kv.Value);
            }
            foreach (var d in log.Deaths) Add(t.Deaths, d.Biome ?? "None", 1);
            return t;
        }

        static void Add(Dictionary<string, float> d, string key, float v)
        {
            if (!(v > 0) || float.IsInfinity(v)) return;   // NaN fails v > 0
            if (!d.ContainsKey(key) && d.Count >= MaxKeys) return;
            d.TryGetValue(key, out var o); d[key] = o + v;
        }

        public void AddAll(BiomeTally other)
        {
            if (other == null) return;
            foreach (var kv in other.Damage) Add(Damage, kv.Key, kv.Value);
            foreach (var kv in other.Deaths) Add(Deaths, kv.Key, kv.Value);
            Version++;
        }

        /// <summary>What <paramref name="now"/> holds beyond <paramref name="was"/> (null: nothing before), never below zero: what one save
        /// added to a session (DayHistory).</summary>
        public static BiomeTally Minus(BiomeTally now, BiomeTally was)
        {
            var d = new BiomeTally();
            if (now == null) return d;
            foreach (var kv in now.Damage) { float w = 0; was?.Damage.TryGetValue(kv.Key, out w); Add(d.Damage, kv.Key, kv.Value - w); }
            foreach (var kv in now.Deaths) { float w = 0; was?.Deaths.TryGetValue(kv.Key, out w); Add(d.Deaths, kv.Key, kv.Value - w); }
            return d;
        }

        public static BiomeTally Sum(params BiomeTally[] parts)
        {
            var s = new BiomeTally();
            foreach (var p in parts) s.AddAll(p);
            return s;
        }

        public void WriteTo(Json j, string key)
        {
            j.Key(key).Open().Dict("damage", Damage).Dict("deaths", Deaths).Close();
        }

        /// <summary>Fills this from what WriteTo wrote, parsed by MiniJson (null: nothing). Returns this.</summary>
        public BiomeTally ReadFrom(Dictionary<string, object> o)
        {
            if (o == null) return this;
            MiniJson.Into(MiniJson.Obj(o, "damage"), Damage); MiniJson.Into(MiniJson.Obj(o, "deaths"), Deaths);
            Version++;
            return this;
        }

        /// <summary>
        /// Keeps the sum of an earlier-sessions tally and a running one, and builds it again only when one of them has changed (a new object
        /// or a moved Version). The panel reads this every 2 seconds and every send reads it, while the hits come in far less often.
        /// The returned tally is shared: read it, never change it.
        /// </summary>
        public sealed class SumCache
        {
            BiomeTally before, running, sum; int beforeVersion, runningVersion;

            public BiomeTally Get(BiomeTally earlier, BiomeTally now)
            {
                if (sum == null || !ReferenceEquals(before, earlier) || !ReferenceEquals(running, now) || beforeVersion != (earlier?.Version ?? 0) || runningVersion != (now?.Version ?? 0))
                {
                    sum = Sum(earlier, now); before = earlier; running = now; beforeVersion = earlier?.Version ?? 0; runningVersion = now?.Version ?? 0;
                }
                return sum;
            }
        }
    }
}
