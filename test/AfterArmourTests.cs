// 0.8.5 damage dealt after the foe's armor (AfterArmour.cs): the game's own steps per hit, kept beside the before in the log (shared) and the
// dealt ledger (the file's "dealtAfter"); a log from an older Hearthwoven joined under it stays unmeasured, never "all stopped".
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;

static class AfterArmourTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        HitData.DamageTypes D(float slash = 0, float pierce = 0, float fire = 0, float poison = 0) => new HitData.DamageTypes { m_slash = slash, m_pierce = pierce, m_fire = fire, m_poison = poison };
        bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;

        // per hit: each type times the foe's modifier, as HitData.ApplyResistance; a world level adds the game's own armor (one ratio for the hit).
        // The brief's "after <= before" holds for a resistance and for none; a weakness (x1.25 to x2) takes more than you swung
        var mods = new HitData.DamageModifiers { m_slash = HitData.DamageModifier.Resistant, m_pierce = HitData.DamageModifier.Weak, m_poison = HitData.DamageModifier.Immune };
        var hit = D(slash: 40, pierce: 20, fire: 10, poison: 30);
        var a = AfterArmour.Of(hit, mods, 0, 100);
        var armored = AfterArmour.Of(hit, mods, 2, 100);   // world level 2: 200 armor on what is left after resistances (20 + 30 + 10 = 60)
        var game = AfterArmour.Of(hit, mods, 0, 100); game.ApplyArmor(200);
        Check(Near(a.m_slash, 20) && a.m_slash <= hit.m_slash && Near(a.m_pierce, 30) && a.m_pierce > hit.m_pierce && Near(a.m_fire, 10) && a.m_poison == 0 &&
              Near(armored.m_slash, game.m_slash) && armored.m_slash < a.m_slash && Near(armored.m_slash / armored.m_pierce, a.m_slash / a.m_pierce),
              "After: per hit each type times the foe's modifier (Resistant slash 40 -> 20, Weak pierce 20 -> 30: more than swung, normal fire as it was, Immune poison 0); a world level adds the game's armor, one ratio for the hit");

        // the log: the after under the before's keys from the first hit on; a JSON round trip (as shared and kept) keeps it; an older log
        // (no afterFrom) joined under it stays unmeasured, its keys never read as "nothing got through"
        var now = new DateTime(2026, 10, 12, 20, 30, 0, DateTimeKind.Utc);
        var log = new EventLog();
        log.AddDamage(now.AddMinutes(-5), "Swamp", true, "Draugr", "Swords", D(slash: 40, pierce: 10, poison: 30));
        log.AddAfter(now.AddMinutes(-5), "Swamp", "Draugr", "Swords", D(slash: 40, pierce: 10, poison: 30), AfterArmour.Of(D(slash: 40, pierce: 10, poison: 30), new HitData.DamageModifiers { m_slash = HitData.DamageModifier.Resistant, m_poison = HitData.DamageModifier.Immune }, 0, 100));
        log.AddDamage(now.AddMinutes(-5), "Swamp", true, "Player", "Swords", D(slash: 25));   // a duel: no after (their armor is not known)
        var key = EventLog.Bucket(now.AddMinutes(-5)) + "|Swamp|dealt|Draugr|Swords|"; var duel = EventLog.Bucket(now.AddMinutes(-5)) + "|Swamp|dealt|Player|Swords|slash";
        var j = new Json().Open(); log.WriteTo(j); var json = j.Close().ToString();
        var back = new EventLog().ReadFrom(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(json), "measuredLog"));
        var wire = MiniJson.Obj(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(json), "measuredLog"), "stopped");
        var older = new EventLog(); older.AddDamage(now.AddMinutes(-40), "Swamp", true, "Draugr", "Swords", D(slash: 50));
        var joined = EventLog.Join(older, back);
        var oldKey = EventLog.Bucket(now.AddMinutes(-40)) + "|Swamp|dealt|Draugr|Swords|slash";
        Check(log.After[key + "slash"] == 20 && log.After[key + "pierce"] == 10 && log.After[key + "poison"] == 0 && wire != null && wire.Count == 2 && !wire.ContainsKey(key + "pierce") &&
              back.After[key + "slash"] == 20 && back.After[key + "pierce"] == 10 && back.After[key + "poison"] == 0 && !back.Measured(duel.Substring(0, 17), "Player") && !back.After.ContainsKey(duel) &&
              joined.Measured(key + "slash") && !joined.Measured(oldKey) && !joined.After.ContainsKey(oldKey),
              "After in the log: under the before's keys (resisted slash 20, normal pierce 10, immune poison 0), shared as what was stopped where something was (2 of 3 keys), read back whole; a duel has none; an older log joined under it stays unmeasured");

        // the file: the dealt ledger as top-level "dealtAfter" (schema 7) with its own date, read back
        var t = new LocalTotals { PlayerId = 42 };
        t.DealtAfter.FromUtc = now.AddDays(-1);
        var s = new ArmourTally(); s.Add("Draugr", "Swords", default, D(slash: 40, poison: 30), AfterArmour.Of(D(slash: 40, poison: 30), mods, 0, 100));
        t.DealtAfter.Record("S1", s, now.ToLocalTime());
        var saved = t.ToJson(now); var root = (Dictionary<string, object>)MiniJson.Parse(saved); var file = LocalTotals.FromJson(saved);
        Check(MiniJson.Num(root, "schema") >= 7 && root.ContainsKey("dealtAfter") && file.DealtAfter.FromUtc == t.DealtAfter.FromUtc &&
              ArmourTally.Total(file.DealtAfter.BeforeSession("S2").Before) == 70 && ArmourTally.Total(file.DealtAfter.BeforeSession("S2").After) == 20 && !file.Extra.ContainsKey("dealtAfter"),
              "After in the file: the dealt ledger as top-level \"dealtAfter\" with its own date, before 70 and after 20 read back");
        return fails;
    }
}
