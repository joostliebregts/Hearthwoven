using System.Collections.Generic;

namespace Hearthwoven
{
    /// <summary>Damage this client dealt and took this session, summed per (target or source, skill, damage type).</summary>
    public class DamageTally
    {
        public readonly Dictionary<string, float> Dealt = new Dictionary<string, float>();   // "Troll|Axes|slash" -> sum
        public readonly Dictionary<string, float> Taken = new Dictionary<string, float>();   // "Troll|EnemyHit|blunt" -> sum
        public int HitsDealt, HitsTaken;

        static void Add(Dictionary<string, float> d, string key, float v) { if (v == 0f || !Json.IsFinite(v)) return; d.TryGetValue(key, out var o); d[key] = o + v; }

        public void AddDealt(string target, string skill, HitData.DamageTypes dt) { HitsDealt++; AddTypes(Dealt, target, skill, dt); }
        public void AddTaken(string source, string cause, HitData.DamageTypes dt) { HitsTaken++; AddTypes(Taken, source, cause, dt); }

        // A hit usually carries one or two of the eleven damage types: the key "who|how|type" is only built for a type with damage,
        // and nothing is allocated for the others (no closure either). The sums are the same as adding every type.
        static void AddTypes(Dictionary<string, float> d, string who, string how, HitData.DamageTypes t)
        {
            One(d, who, how, "blunt", t.m_blunt); One(d, who, how, "slash", t.m_slash); One(d, who, how, "pierce", t.m_pierce); One(d, who, how, "chop", t.m_chop);
            One(d, who, how, "pickaxe", t.m_pickaxe); One(d, who, how, "fire", t.m_fire); One(d, who, how, "frost", t.m_frost);
            One(d, who, how, "lightning", t.m_lightning); One(d, who, how, "poison", t.m_poison); One(d, who, how, "spirit", t.m_spirit); One(d, who, how, "damage", t.m_damage);
        }

        static void One(Dictionary<string, float> d, string who, string how, string type, float v) { if (v != 0f) Add(d, who + "|" + how + "|" + type, v); }

        public void WriteTo(Json j, string key = "damageThisSession")
        {
            j.Key(key).Open().Num("hitsDealt", HitsDealt).Num("hitsTaken", HitsTaken).Dict("dealt", Dealt).Dict("taken", Taken).Close();
        }

        /// <summary>Fills this from what WriteTo wrote, parsed by MiniJson (null: nothing). Returns this.</summary>
        public DamageTally ReadFrom(Dictionary<string, object> o)
        {
            if (o == null) return this;
            MiniJson.Into(MiniJson.Obj(o, "dealt"), Dealt); MiniJson.Into(MiniJson.Obj(o, "taken"), Taken);
            HitsDealt = (int)MiniJson.Num(o, "hitsDealt"); HitsTaken = (int)MiniJson.Num(o, "hitsTaken");
            return this;
        }

        /// <summary>Adds another tally's counts to this one (null: nothing).</summary>
        public void AddAll(DamageTally other)
        {
            if (other == null) return;
            HitsDealt += other.HitsDealt; HitsTaken += other.HitsTaken;
            foreach (var kv in other.Dealt) Add(Dealt, kv.Key, kv.Value);
            foreach (var kv in other.Taken) Add(Taken, kv.Key, kv.Value);
        }

        /// <summary>What <paramref name="now"/> holds beyond <paramref name="was"/> (null: nothing before), never below zero: what one save
        /// added to a session (DayHistory).</summary>
        public static DamageTally Minus(DamageTally now, DamageTally was)
        {
            var d = new DamageTally();
            if (now == null) return d;
            d.HitsDealt = System.Math.Max(0, now.HitsDealt - (was?.HitsDealt ?? 0)); d.HitsTaken = System.Math.Max(0, now.HitsTaken - (was?.HitsTaken ?? 0));
            foreach (var kv in now.Dealt) { float w = 0; was?.Dealt.TryGetValue(kv.Key, out w); if (kv.Value - w > 0) Add(d.Dealt, kv.Key, kv.Value - w); }
            foreach (var kv in now.Taken) { float w = 0; was?.Taken.TryGetValue(kv.Key, out w); if (kv.Value - w > 0) Add(d.Taken, kv.Key, kv.Value - w); }
            return d;
        }

        /// <summary>A new tally with the counts of all parts added up (nulls skipped); the parts stay unchanged.</summary>
        public static DamageTally Sum(params DamageTally[] parts)
        {
            var s = new DamageTally();
            foreach (var p in parts) s.AddAll(p);
            return s;
        }
    }
}
