// 0.9 armor-share (Joost, 18 Oct: "Yes, add friends' armour view (What hurt you / What your armor stopped on a friend's Defense page) to 0.9"):
// the armour ledger travels in the shared copy as totals per damage type since it began ("armorSince", its start date) and as minutes in the log
// ("armor" in measuredLog and logBefore, EventLog.Armor), the way blocks and parries travel. Round trip, bounds, the cut and join of the minutes,
// an older copy (no key: not known, never zero), the server's live-update checks and the size of a busy evening.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;

static class ArmorShareTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        HitData.DamageTypes D(float slash = 0, float blunt = 0, float poison = 0, float fall = 0) => new HitData.DamageTypes { m_slash = slash, m_blunt = blunt, m_poison = poison, m_damage = fall };
        var t0 = new DateTime(2026, 10, 18, 19, 0, 0, DateTimeKind.Utc);

        // ---------- the log's armor minutes: per minute and type, what reached the armor and what got through ----------
        var log = new EventLog();
        log.AddArmor(t0, D(slash: 50, poison: 10, fall: 7), D(slash: 30, poison: 6, fall: 7));
        log.AddArmor(t0.AddSeconds(20), D(slash: 10), D(slash: 4));
        log.AddArmor(t0.AddMinutes(1), D(), D());   // a ward or a block took it all: nothing reached the armor
        var m0 = EventLog.Bucket(t0);
        Check(log.Armor.Count == 4 && log.Armor[m0 + "|b|slash"] == 60 && log.Armor[m0 + "|a|slash"] == 34 && log.Armor[m0 + "|b|poison"] == 10 && log.Armor[m0 + "|a|poison"] == 6 &&
              !log.Armor.Keys.Any(k => k.EndsWith("|damage")) && log.Damage.Count == 0 && log.Hits.Count == 0,
              "AV minutes: a hit's two numbers per battle type under its minute (falls never meet armor); a hit that reached nothing adds nothing; Damage and Hits untouched");

        // ---------- the copy: totals per type with the start date, the minutes in measuredLog; read back exactly ----------
        var ledger = new ArmourTally();
        ledger.Add("Troll", "EnemyHit", D(blunt: 120), D(blunt: 120), D(blunt: 66.66f));
        ledger.Add("Draugr", "EnemyHit", D(slash: 50, poison: 10), D(slash: 50, poison: 10), D(slash: 30, poison: 6));
        ledger.Add("Draugr_Ranged", "EnemyHit", D(slash: 10), D(slash: 10), D(slash: 4));
        var from = new DateTime(2026, 10, 6, 18, 30, 0, DateTimeKind.Utc);
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats();
        string Copy(ArmourTally since, EventLog l, DateTime? f) =>
            GroupShare.SharedCopy(Snapshot.Build("0.9.0", 4242, "Edda", stats, new Snapshot.SkillInfo[0], "Midgard", new DamageTally(), "s1", new SessionEvents(), l, true, armorSince: since, armorFromUtc: f));
        var json = Copy(ledger, log, from);
        var root = (Dictionary<string, object>)MiniJson.Parse(json);
        var shared = MiniJson.Obj(root, ArmourTally.SharedKey);
        var back = ArmourTally.ReadShared(shared, out var backFrom);
        var copy = Hearthwoven.Panel.PanelInput.FromSnapshot(json);
        var sharedText = MiniJson.Write(shared);
        Check(back != null && backFrom == from && back.Hits == 3 && back.Before.Count == 3 && back.Before["||blunt"] == 120 && back.Before["||slash"] == 60 && back.After["||slash"] == 34 &&
              Math.Abs(back.After["||blunt"] - 66.7f) < 0.001 && back.Before.Keys.All(k => k.StartsWith("||")) && !sharedText.Contains("Troll") && !sharedText.Contains("Draugr") && !sharedText.Contains("EnemyHit") &&
              copy != null && copy.ArmourFromUtc == from && copy.ArmourSince != null && ArmourTally.Total(copy.ArmourSince.Before) == 190 &&
              copy.Log.ArmorKept && copy.Log.Armor.Count == 4 && copy.Log.Armor[m0 + "|a|slash"] == 34,
              "AV copy: \"armorSince\" carries the start date and per damage type what reached the armor and what got through (to tenths) and the hits, no foe or cause; " +
              "the log's minutes travel in measuredLog; PanelInput reads both back (" + sharedText + ")");

        var empty = Copy(new ArmourTally(), new EventLog(), from);
        var emptyCopy = Hearthwoven.Panel.PanelInput.FromSnapshot(empty);
        Check(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(empty), ArmourTally.SharedKey) != null && emptyCopy.ArmourSince != null && emptyCopy.ArmourSince.Empty && emptyCopy.Log.ArmorKept,
              "AV empty: a ledger with nothing in it is still written, so a reader tells \"nothing reached their armor\" from an older Hearthwoven");

        // ---------- an older copy (before 0.9): no key, so not known (null), never read as nothing stopped ----------
        var older = (Dictionary<string, object>)MiniJson.Parse(json);
        older.Remove(ArmourTally.SharedKey); ((Dictionary<string, object>)older["measuredLog"]).Remove("armor");
        var olderCopy = Hearthwoven.Panel.PanelInput.FromSnapshot(MiniJson.Write(older));
        Check(olderCopy != null && olderCopy.ArmourSince == null && olderCopy.ArmourFromUtc == null && !olderCopy.Log.ArmorKept && olderCopy.Log.Armor.Count == 0,
              "AV older copy: no \"armorSince\" and no \"armor\" minutes read as not shared (null, ArmorKept false), never as zero");

        // ---------- bounds: the reader keeps only well-formed keys of the eight types; the log keeps at most MaxArmorKeys, the newest ----------
        var junk = new Dictionary<string, object>
        {
            ["armorSince"] = new Dictionary<string, object> { ["from"] = "2026-10-06T18:30:00Z", ["before"] = new Dictionary<string, object> { ["slash"] = 5.0, ["chop"] = 9.0, ["Troll|EnemyHit|blunt"] = 3.0, ["poison"] = -4.0, ["fire"] = double.PositiveInfinity }, ["after"] = new Dictionary<string, object> { ["slash"] = 2.0 } },
        };
        var junkBack = ArmourTally.ReadShared(MiniJson.Obj(junk, "armorSince"), out _);
        var junkLog = new EventLog().ReadFrom(new Dictionary<string, object> { ["armor"] = new Dictionary<string, object> { [m0 + "|b|slash"] = 5.0, [m0 + "|x|slash"] = 1.0, [m0 + "|b|chop"] = 1.0, ["nonsense"] = 2.0, [m0 + "|a|slash"] = -1.0 } });
        Check(junkBack.Before.Count == 1 && junkBack.Before["||slash"] == 5 && junkBack.After["||slash"] == 2 && junkLog.ArmorKept && junkLog.Armor.Count == 1 && junkLog.Armor[m0 + "|b|slash"] == 5,
              "AV bounds (reader): only the eight battle types with a finite amount above 0, only \"minute|b|type\" and \"minute|a|type\" keys: anything else in a copy is left out");

        var day = new EventLog();
        var dayStart = t0.AddHours(-20);
        for (int m = 0; m < 24 * 60; m++)   // a whole day of hits in all eight types, every minute: the worst a session can hold
            day.AddArmor(dayStart.AddMinutes(m), new HitData.DamageTypes { m_blunt = 1, m_slash = 1, m_pierce = 1, m_fire = 1, m_frost = 1, m_lightning = 1, m_poison = 1, m_spirit = 1 },
                         new HitData.DamageTypes { m_blunt = 1, m_slash = 1, m_pierce = 1, m_fire = 1, m_frost = 1, m_lightning = 1, m_poison = 1, m_spirit = 1 });
        var newest = EventLog.Bucket(dayStart.AddMinutes(24 * 60 - 1));
        Check(day.Armor.Count <= EventLog.MaxArmorKeys && !day.ArmorClipped && day.Armor.ContainsKey(newest + "|b|spirit"),
              "AV bounds (day): a whole day of hits in all eight types every minute stays under the bound (" + day.Armor.Count + " keys of " + EventLog.MaxArmorKeys + ", the minutes past 190 folded to tens)");
        var full = new EventLog();
        for (int m = 0; full.Armor.Count < EventLog.MaxArmorKeys; m++) full.Armor[EventLog.Bucket(dayStart.AddMinutes(-m * 10)) + "|b|slash"] = 1;   // a log at the bound, oldest first to go
        var oldest = full.Armor.Keys.Min(StringComparer.Ordinal);
        full.AddArmor(t0, D(slash: 9), D(slash: 3));
        Check(full.Armor.Count <= EventLog.MaxArmorKeys && full.ArmorClipped && !full.Armor.ContainsKey(oldest) && full.Armor[m0 + "|b|slash"] == 9,
              "AV bounds (full): past MaxArmorKeys the oldest minute goes, the newest hit is kept, and the log says it was clipped (written as \"armorClipped\")");
        var clippedBack = new EventLog().ReadFrom(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(Json(full)), "measuredLog"));
        Check(clippedBack.ArmorClipped, "AV bounds: a clipped log reads back as clipped");

        // ---------- the window cut: kept minutes, joins and folds carry Armor like Guard ----------
        var connect = t0.AddMinutes(30);
        var earlier = new EventLog();
        earlier.AddArmor(t0.AddMinutes(5), D(slash: 20), D(slash: 8)); earlier.AddGuard(t0.AddMinutes(5), false);
        var kept = KeptMinutes.Take(connect, earlier, null);
        var keptBack = KeptMinutes.FromJson(kept.ToJson(connect));
        var sharedKept = new EventLog().ReadFrom((Dictionary<string, object>)MiniJson.Parse(kept.SharedJson()));
        var later = new EventLog(); later.AddArmor(connect.AddMinutes(2), D(slash: 40), D(slash: 10));
        var joined = EventLog.Join(sharedKept, later);
        var cut = joined.Between(connect, DateTime.MaxValue);
        Check(!kept.Empty && keptBack.Log.Armor[EventLog.Bucket(t0.AddMinutes(5)) + "|b|slash"] == 20 && sharedKept.ArmorKept && sharedKept.Armor.Count == 2 &&
              joined.Armor.Count == 4 && cut.Armor.Count == 2 && cut.Armor.Keys.All(k => string.CompareOrdinal(k, EventLog.Bucket(connect)) >= 0),
              "AV cut: the armor minutes are kept across a restart (the local minutes file), shared as logBefore, joined under the next connection's log and cut by Between like Guard");
        var oldEarlier = new EventLog().ReadFrom(new Dictionary<string, object> { ["guard"] = new Dictionary<string, object>() });   // an earlier connection on an older Hearthwoven
        Check(!oldEarlier.ArmorKept && !EventLog.Join(oldEarlier, later).ArmorKept && !EventLog.Join(later, oldEarlier).ArmorKept && EventLog.Join(later, later).ArmorKept,
              "AV join: the joined log keeps armor minutes only when both parts kept them (review fix: a 0.8.6 kept-minutes file has none, so its span is not armor-free)");
        var fold = new EventLog();
        fold.AddArmor(t0.AddMinutes(3), D(slash: 5), D(slash: 2));
        fold.AddArmor(t0.AddMinutes(3 + EventLog.FoldAfterMinutes + 5), D(slash: 1), D(slash: 1));   // the first minute is now past the fold
        Check(fold.Armor.ContainsKey(EventLog.Bucket(t0) + "|b|slash") && !fold.Armor.ContainsKey(EventLog.Bucket(t0.AddMinutes(3)) + "|b|slash") && fold.Armor[EventLog.Bucket(t0) + "|b|slash"] == 5,
              "AV fold: minutes older than " + EventLog.FoldAfterMinutes + " fold into their ten minutes with the rest of the log, every amount kept");

        // ---------- size and the server: a busy evening's copy, its live updates through the server's checks ----------
        var busy = new EventLog(); var busyLedger = new ArmourTally();
        var rnd = new Random(7);
        for (int s = 0; s < 4 * 60 * 6; s++)   // four hours, a hit every 10 s in two or three types
        {
            var b = new HitData.DamageTypes { m_slash = (float)rnd.NextDouble() * 40, m_blunt = s % 3 == 0 ? (float)rnd.NextDouble() * 60 : 0, m_poison = s % 5 == 0 ? (float)rnd.NextDouble() * 15 : 0 };
            var a = new HitData.DamageTypes { m_slash = b.m_slash * 0.6f, m_blunt = b.m_blunt * 0.5f, m_poison = b.m_poison * 0.8f };
            busy.AddArmor(dayStart.AddSeconds(10 * s), b, a); busyLedger.Add("Draugr", "EnemyHit", b, b, a);
        }
        int Packed(string j) => Transport.Pack(j).Length;
        var withArmor = Copy(busyLedger, busy, from);
        var noArmor = Copy(null, StripArmor(busy), null);
        int add = Packed(withArmor) - Packed(noArmor);
        System.Console.WriteLine($"INFO armor-share: a busy four-hour evening adds {add} bytes packed to the shared copy ({busy.Armor.Count} minute keys, " +
                                 $"{withArmor.Length - noArmor.Length} bytes of JSON); armorSince alone is {MiniJson.Write(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(withArmor), ArmourTally.SharedKey)).Length} bytes of JSON");
        Check(add > 0 && add <= 8 * 1024, "AV size: a busy four-hour evening's armor (a hit every 10 s) adds at most 8 KB packed to the copy, under the 12 KB a busy evening may pack before a second encoding is due (DESIGN-feed-blocks.md) (+" + add + ")");
        var baseShared = withArmor;
        busy.AddArmor(dayStart.AddHours(4).AddSeconds(5), D(slash: 12), D(slash: 7)); busyLedger.Add("Draugr", "EnemyHit", D(slash: 12), D(slash: 12), D(slash: 7));
        var delta = LiveDelta.Make(baseShared, Copy(busyLedger, busy, from), "s1-1", 1);
        Check(delta.Text != null && !delta.TooBig && ServerIntake.IsJsonObject(delta.Text) && LiveDelta.Read(delta.Text, out _, out _) && !LiveDelta.Private(delta.Text) && !LiveDelta.Identity(delta.Text) &&
              delta.Text.Contains("armorSince") && delta.Text.Contains("/measuredLog/armor/"),
              "AV live: a new hit on the armor goes out as a small live update the server takes (" + delta.Packed?.Length + " bytes packed): this minute's key and the totals");
        return fails;
    }

    static string Json(EventLog l) { var j = new Json().Open(); l.WriteTo(j); return j.Close().ToString(); }

    static EventLog StripArmor(EventLog l) { var c = l.Between(DateTime.MinValue, DateTime.MaxValue); c.Armor.Clear(); c.ArmorKept = false; return c; }
}
