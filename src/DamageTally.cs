using System.Collections.Generic;

namespace Hearthwoven
{
    /// <summary>Damage this client dealt and took this session, summed per (target or source, skill, damage type).</summary>
    public class DamageTally
    {
        public readonly Dictionary<string, float> Dealt = new Dictionary<string, float>();   // "Troll|Axes|slash" -> sum
        public readonly Dictionary<string, float> Taken = new Dictionary<string, float>();   // "Troll|EnemyHit|blunt" -> sum
        public int HitsDealt, HitsTaken;

        static void Add(Dictionary<string, float> d, string key, float v) { if (v == 0f) return; d.TryGetValue(key, out var o); d[key] = o + v; }

        public void AddDealt(string target, string skill, HitData.DamageTypes dt) { HitsDealt++; Each(dt, (t, v) => Add(Dealt, target + "|" + skill + "|" + t, v)); }
        public void AddTaken(string source, string cause, HitData.DamageTypes dt) { HitsTaken++; Each(dt, (t, v) => Add(Taken, source + "|" + cause + "|" + t, v)); }

        static void Each(HitData.DamageTypes d, System.Action<string, float> f)
        {
            f("blunt", d.m_blunt); f("slash", d.m_slash); f("pierce", d.m_pierce); f("chop", d.m_chop); f("pickaxe", d.m_pickaxe);
            f("fire", d.m_fire); f("frost", d.m_frost); f("lightning", d.m_lightning); f("poison", d.m_poison); f("spirit", d.m_spirit); f("damage", d.m_damage);
        }

        public void WriteTo(Json j)
        {
            j.Key("damageThisSession").Open().Num("hitsDealt", HitsDealt).Num("hitsTaken", HitsTaken).Dict("dealt", Dealt).Dict("taken", Taken).Close();
        }
    }
}
