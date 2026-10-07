using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hearthwoven
{
    /// <summary>One routed hit, already resolved to plain values. Pure C#, so it can be tested outside the game.</summary>
    public class DamageRecord
    {
        public string Time, Sender, Attacker, Target, Skill, Cause, StatusEffect;
        public float X, Z, SkillLevel;
        public int ItemLevel;
        public readonly Dictionary<string, float> Damage = new Dictionary<string, float>();

        static string Q(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string F(float f) => f.ToString("0.##", CultureInfo.InvariantCulture);

        public string ToJson()
        {
            var b = new StringBuilder("{");
            b.Append("\"t\":").Append(Q(Time)).Append(",\"sender\":").Append(Q(Sender))
             .Append(",\"attacker\":").Append(Q(Attacker)).Append(",\"target\":").Append(Q(Target))
             .Append(",\"skill\":").Append(Q(Skill)).Append(",\"cause\":").Append(Q(Cause))
             .Append(",\"status\":").Append(Q(StatusEffect))
             .Append(",\"skillLevel\":").Append(F(SkillLevel)).Append(",\"itemLevel\":").Append(ItemLevel)
             .Append(",\"x\":").Append(F(X)).Append(",\"z\":").Append(F(Z)).Append(",\"damage\":{");
            var first = true;
            foreach (var kv in Damage)
            {
                if (!first) b.Append(',');
                b.Append(Q(kv.Key)).Append(':').Append(F(kv.Value));
                first = false;
            }
            return b.Append("}}").ToString();
        }
    }
}
