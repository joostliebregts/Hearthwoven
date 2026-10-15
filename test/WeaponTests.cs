// 0.8.6 DPS per named weapon (WeaponLog.cs): per hit the weapon you hold (its prefab name), its upgrade level and your level in its skill; per minute in the log and
// in the dealt ledger, per weapon and level: fighting seconds, the damage swung in them, all its damage, the lowest and highest skill level. One sample with two
// weapons, two levels of one of them, a skill that went up and a long gap; the cap on keys; what is saved and shared; the cost per hit; the bytes added to the copy.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;

static class WeaponTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        HitData.DamageTypes D(float slash = 0, float pierce = 0) => new HitData.DamageTypes { m_slash = slash, m_pierce = pierce };
        var t0 = new DateTime(2026, 10, 12, 20, 30, 0, DateTimeKind.Utc);
        // the strings the hooks keep once per item and per skill: a hit finds its cell by their identity
        string iron = "SwordIron", bowPrefab = "BowFineWood", swords = "Swords", bows = "Bows";

        var clock = new SwingClock(); var log = new EventLog(); var ledger = new ArmourTally();
        void Hit(double at, string prefab, int quality, string skill, int level, float amount)
        {
            var utc = t0.AddSeconds(at); var d = skill == bows ? D(pierce: amount) : D(slash: amount);
            var kind = SwingClock.KindOf(skill); var secs = clock.Hit(kind, utc); var dmg = SwingClock.SwungOf(d); var swung = secs > 0 ? dmg : 0f;
            log.AddDamage(utc, "Swamp", true, "Troll", skill, d);
            if (secs > 0) { log.AddSwing(utc, kind, secs, dmg); ledger.AddSwing(kind, secs, dmg); }
            log.AddWeapon(utc, prefab, quality, skill, level, secs, swung, dmg); ledger.AddWeapon(prefab, quality, skill, level, secs, swung, dmg);
        }
        // the sword at upgrade level 2 with the skill at 20, then 21 on the fourth hit; at level 3 from the fifth on; an arrow between; a 30 s pause; two more sword hits at skill 22
        Hit(0, iron, 2, swords, 20, 100); Hit(1, iron, 2, swords, 20, 100); Hit(2, iron, 2, swords, 20, 100); Hit(2.5, bowPrefab, 1, bows, 31, 50); Hit(3, iron, 2, swords, 21, 100);
        Hit(4, iron, 3, swords, 21, 120); Hit(4.5, bowPrefab, 1, bows, 31, 50); Hit(5, iron, 3, swords, 21, 120); Hit(35, iron, 3, swords, 22, 120); Hit(36, iron, 3, swords, 22, 120);
        // iron q2: hits at 0 1 2 3 -> 3 s (the first hit starts the clock), 300 swung of 400; q3: 4 5 35 36 -> 1 + 1 + 0 (after the pause) + 1 = 3 s, 360 swung of 480; the arrows: 2 s, 50 swung of 100
        WeaponCell Of(WeaponCells c, string key) => c.Map.TryGetValue(key, out var x) ? x : null;
        var ledgerCells = ledger.Weapons; var q2 = Of(ledgerCells, "SwordIron|2|Swords"); var q3 = Of(ledgerCells, "SwordIron|3|Swords"); var arrows = Of(ledgerCells, "BowFineWood|1|Bows");
        bool Is(WeaponCell c, double sec, double swung, double all, int lo, int hi) => c != null && Math.Abs(c.Sec - sec) < 0.001 && Math.Abs(c.Swung - swung) < 0.001 && Math.Abs(c.All - all) < 0.001 && c.Lo == lo && c.Hi == hi;
        Check(ledgerCells.Count == 3 && Is(q2, 3, 300, 400, 20, 21) && Is(q3, 3, 360, 480, 21, 22) && Is(arrows, 2, 50, 100, 31, 31),
              "per hit the weapon, its upgrade level and your skill level: the sword at level 2 has 3 s of fighting, 300 swung of 400, the skill 20 to 21 (it went up); at level 3 3 s (the 30 s pause not counted), 360 of 480, 21 to 22; the arrows 2 s, 50 of 100, 31");
        // the same per whole skill level: the seconds and the damage go to the level they happened at (the skill went up inside the level 2 sword's fight, and between the level 3's stretches)
        bool Lv(WeaponCell c, int level, double sec, double swung) => c != null && c.TryPart(level, out var x) && Math.Abs(x.Sec - sec) < 0.001 && Math.Abs(x.Swung - swung) < 0.001;
        Check(q2.PartCount == 2 && Lv(q2, 20, 2, 200) && Lv(q2, 21, 1, 100) && q3.PartCount == 2 && Lv(q3, 21, 2, 240) && Lv(q3, 22, 1, 120) && arrows.PartCount == 1 && Lv(arrows, 31, 2, 50),
              "per skill level: the level 2 sword has 2 s and 300 damage at skill 20 and 1 s and 100 at 21 (the skill went up inside the fight); the level 3 sword 2 s at 21 and 1 s at 22; each level's seconds and swung damage add up to the cell's");
        // the most recent pace: the highest skill level with 5 s of fighting, against the lowest such level; a newest level under the floor falls back to the one before it; none reaches it: no trend
        WeaponCell Cell(params (int level, double sec, double swung)[] ps) { var c = new WeaponCell(); foreach (var (l, sc, sw) in ps) { c.Add(l, (float)sc, (float)sw, (float)sw, 12); } return c; }
        var rising = Cell((38, 20, 600), (39, 10, 330), (41, 3, 150)).Recent(SwingClock.Floor); var onlyOne = Cell((38, 20, 600), (41, 3, 150)).Recent(SwingClock.Floor);
        var none = Cell((38, 4, 120), (39, 3, 99)).Recent(SwingClock.Floor); var two = Cell((40, 6, 120), (38, 20, 600), (39, 10, 330), (45, 5, 250)).Recent(SwingClock.Floor);
        Check(rising.Has && rising.Level == 39 && Math.Abs(rising.Dps - 33) < 0.001 && rising.LowLevel == 38 && Math.Abs(rising.LowDps - 30) < 0.001 && onlyOne.Has && onlyOne.Level == 38 && onlyOne.LowLevel == 38 && !none.Has &&
              two.Level == 45 && Math.Abs(two.Dps - 50) < 0.001 && two.LowLevel == 38 && Math.Abs(two.LowDps - 30) < 0.001,
              "the most recent pace: levels 38 (30 DPS), 39 (33) and 41 (3 s: under the floor) give level 39 against level 38; one level over the floor gives itself and no mark; none over it gives none; the highest level wins, the lowest to compare, whatever order they came in");
        // the cap on levels: a minute keeps its 4 newest levels (the rest only in its totals), a day row 8, a week 12; a lower level than all of a full set is in the totals only
        var five = new WeaponCell(); for (int l = 30; l < 35; l++) five.Add(l, 1f, 10f, 10f, WeaponCells.PerMinuteLevels); five.Add(10, 1f, 10f, 10f, WeaponCells.PerMinuteLevels);
        var wide = new WeaponCell(); for (int l = 1; l <= 30; l++) wide.Add(l, 1f, 10f, 10f, WeaponCells.MergeLevels); var clipped = wide.Copy(); clipped.ClipLevels(WeaponCells.PerDayLevels);
        Check(five.PartCount == 4 && five.SortedParts().Select(x => x.Level).SequenceEqual(new[] { 31, 32, 33, 34 }) && Math.Abs(five.All - 60) < 0.001 && five.Lo == 10 && five.Hi == 34 &&
              clipped.PartCount == 8 && clipped.SortedParts().First().Level == 23 && clipped.SortedParts().Last().Level == 30 && Math.Abs(clipped.All - 300) < 0.001 && wide.PartCount == 30,
              "levels kept: a minute's cell keeps its " + WeaponCells.PerMinuteLevels + " newest skill levels (a sixth hit at a lower level is in the totals only), a day row " + WeaponCells.PerDayLevels + ", a week or month " + WeaponCells.PerLongLevels + "; the totals always hold every hit");
        var logCells = log.Weapons.Window(_ => true);
        bool SameParts(WeaponCell x, WeaponCell y) { var a = x.SortedParts(); var b = y.SortedParts(); return a.Length == b.Length && a.Zip(b, (u, v) => u.Level == v.Level && Math.Abs(u.Sec - v.Sec) < 0.001 && Math.Abs(u.Swung - v.Swung) < 0.001).All(t => t); }
        bool Same(WeaponCells a, WeaponCells b) => a.Count == b.Count && a.Map.All(kv => b.Map.TryGetValue(kv.Key, out var o) && Math.Abs(o.Sec - kv.Value.Sec) < 0.001 && Math.Abs(o.Swung - kv.Value.Swung) < 0.001 && Math.Abs(o.All - kv.Value.All) < 0.001 && o.Lo == kv.Value.Lo && o.Hi == kv.Value.Hi && SameParts(o, kv.Value));
        var kindSeconds = ledger.SwingOf("melee").seconds + ledger.SwingOf("bow").seconds;
        Check(Same(logCells, ledgerCells) && log.Weapons.Map.Keys.All(k => k.StartsWith("2026-10-12T20:30Z|", StringComparison.Ordinal)) && Math.Abs(kindSeconds - ledgerCells.Map.Values.Sum(c => c.Sec)) < 0.001,
              "the log's minute and the ledger hold the same cells; the weapons' seconds add up to the kinds' seconds");

        // the cap: the 13th weapon in a minute falls into its skill's other cell, and so does a hit with no named weapon (an arrow after you switched)
        var cap = new EventLog(); var names = Enumerable.Range(0, 14).Select(i => "Axe" + i).ToArray();
        foreach (var n in names) cap.AddWeapon(t0, n, 1, swords, 10, 1f, 10f, 10f);
        cap.AddWeapon(t0, null, 0, bows, 10, 0f, 0f, 10f);
        cap.AddWeapon(t0.AddMinutes(1), names[13], 1, swords, 10, 1f, 10f, 10f);   // the next minute starts again: this one is named
        var other = Of(cap.Weapons.Window(m => m.EndsWith("20:30Z")), "~other|0|Swords");
        Check(cap.Weapons.Map.Keys.Count(k => k.StartsWith("2026-10-12T20:30Z|Axe", StringComparison.Ordinal)) == WeaponCells.PerMinute && Is(other, 2, 20, 20, 10, 10) &&
              Of(cap.Weapons.Window(m => m.EndsWith("20:30Z")), "~other|0|Bows") != null && cap.Weapons.Map.ContainsKey("2026-10-12T20:31Z|Axe13|1|Swords"),
              "at most " + WeaponCells.PerMinute + " named weapons a minute: the 13th and 14th fall into the skill's other cell with an unnamed hit's own, and the next minute is named again");

        // the cap counts weapons, not switches: 45 alternations of a sword and a bow, three hits each, with a new string for each prefab name at every hit (Unity makes
        // one each time it is read); the session's cap is PerSession, so both keep all their hits and no hit falls into an other cell
        var alt = new WeaponCells();
        for (int i = 0; i < 45; i++)
            for (int h = 0; h < 3; h++)
            {
                alt.Hit(t0, new string(iron.ToCharArray()), 3, swords, 20, 1f, 10f, 10f, WeaponCells.PerSession);
                alt.Hit(t0, new string(bowPrefab.ToCharArray()), 2, bows, 31, 1f, 10f, 10f, WeaponCells.PerSession);
            }
        Check(alt.Map.Count == 2 && !alt.Map.ContainsKey(WeaponKey.OtherOf(swords)) && !alt.Map.ContainsKey(WeaponKey.OtherOf(bows)) &&
              Math.Abs(alt.Map["SwordIron|3|Swords"].All - 1350) < 0.001 && Math.Abs(alt.Map["BowFineWood|2|Bows"].All - 1350) < 0.001,
              "the cap counts weapons, not switches: 45 alternations of a sword and a bow with new name strings at every hit: both keep all 135 hits (1350 damage each), no other cell");

        // saved and shared: the log's minutes in your kept minutes, the shared copy's measuredLog (with the weapons: on purpose), the ledger's book and its day row
        var kept = KeptMinutes.Take(t0.AddMinutes(2), log, null);
        var back = KeptMinutes.FromJson(kept.ToJson(t0.AddMinutes(2))).Log;
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats();
        var book = new ArmourBook { FromUtc = t0.AddDays(-1), SwingFromUtc = t0 }; book.Record("S1", ledger, t0.ToLocalTime());
        var today = t0.ToLocalTime().Date;
        var dayWeapons = book.DayWeapons(today, 30, book.Pending("S1", ledger));
        var copy = GroupShare.SharedCopy(Snapshot.Build("0.8.6", 7, "Rowan", stats, new Snapshot.SkillInfo[0], "Midgard", new DamageTally(), "s1", new SessionEvents(), log, true, dayWeapons: dayWeapons));
        var theirs = Hearthwoven.Panel.PanelInput.FromSnapshot(copy);
        var bj = new Json().Open(); book.WriteTo(bj, "armour"); var bookBack = ArmourBook.ReadFrom(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(bj.Close().ToString()), "armour"));
        var dayRow = bookBack.Days.Count == 1 ? bookBack.Days[0].Tally.Weapons : new WeaponCells();
        var sharedDay = theirs.DayWeapons != null && theirs.DayWeapons.Count == 1 ? theirs.DayWeapons.Values.First() : new WeaponCells();
        Check(Same(back.Weapons.Window(_ => true), logCells) && theirs.Log.WeaponsKept && Same(theirs.Log.Weapons.Window(_ => true), logCells) && !copy.Contains("\"swing\"") &&
              Same(bookBack.Last.Weapons, ledgerCells) && dayRow.Count == 3 && Is(Of(dayRow, "SwordIron|3|Swords"), 3, 360, 480, 21, 22) && Same(sharedDay, dayRow),
              "saved and shared: your kept minutes and the copy's measuredLog hold the log's cells whole (the kind-level fighting time still stays on your PC); the ledger's book keeps the session and its day row, and the copy's dayWeapons is that day row");
        // an older copy has no weapons and is never read as having none
        var older = Hearthwoven.Panel.PanelInput.FromSnapshot(GroupShare.SharedCopy(Snapshot.Build("0.8.5", 7, "Rowan", stats, new Snapshot.SkillInfo[0], "Midgard", new DamageTally(), "s1", new SessionEvents(), new EventLog { WeaponsKept = false }, true)));
        var hostile = Hearthwoven.Panel.PanelInput.FromSnapshot("{\"name\":\"Tor\",\"measuredLog\":{\"weapons\":{\"2026-10-12T20:30Z\":{\"SwordIron|2|Swords\":\"1e99,5,6,1,2\",\"x\":\"1,2,3,4,5\",\"Axe|q|Swords\":\"1,2,3,4,5\",\"Bad|2|Swords\":\"x\",\"Odd|2|Swords\":[1,2,3,4,5]}}},\"dayWeapons\":{\"nonsense\":{},\"2026-10-12\":{\"SwordIron|2|Swords\":\"3,300,400,20,21\",\"Axe|2|Swords\":\"3,300,-4,20,21\"}}}");
        Check(!older.Log.WeaponsKept && older.DayWeapons == null && hostile.Log.WeaponsKept && hostile.Log.Weapons.Count == 0 && hostile.DayWeapons != null && hostile.DayWeapons.Keys.SequenceEqual(new[] { "2026-10-12" }) && hostile.DayWeapons["2026-10-12"].Count == 1,
              "a copy from before 0.8.6 has no weapons (not read as none); hostile cells (huge, negative, not numbers, not keys) and days are skipped");

        // a cell with one level that holds every hit is written without its part (a minute usually is); several levels carry theirs; hostile parts are skipped
        var oneCell = new WeaponCells(); oneCell.Hit(t0, iron, 2, swords, 20, 1f, 100f, 100f, 12, 4); oneCell.Hit(t0, iron, 3, swords, 20, 1f, 100f, 100f, 12, 4); oneCell.Hit(t0.AddSeconds(5), iron, 3, swords, 21, 1f, 100f, 100f, 12, 4);
        var oj = new Json().Open(); oneCell.Write(oj, "w"); var ot = oj.Close().ToString(); var oneBack = WeaponCells.Read(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(ot), "w"));
        var rough = WeaponCells.Read(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse("{\"w\":{\"A|1|Swords\":\"3,300,400,20,21;20,2,200;21,-1,5;x,1,1;99999,1,1;21,1,100\"}}"), "w"));
        Check(Same(oneBack, oneCell) && oneBack.Map["SwordIron|2|Swords"].PartCount == 1 && !ot.Contains("\"SwordIron|2|Swords\":\"1,100,100,20,20;") && ot.Contains("\"SwordIron|3|Swords\":\"2,200,200,20,21;20,1,100;21,") && ot.Split(';').Length == 3 &&
              rough.Map["A|1|Swords"].PartCount == 2 && rough.Map["A|1|Swords"].Lo == 20 && rough.Map["A|1|Swords"].Hi == 21,
              "written compactly: a cell with one skill level has no part of its own (read back as the totals), a cell with two carries both; a part with a negative number, a level that is no number or a level past 9999 is skipped when read");

        // the skill range of a day is the one seen since the last save, not the whole session's
        var split = new ArmourTally(); var splitBook = new ArmourBook(); var lap = t0.ToLocalTime();
        split.AddWeapon(iron, 2, swords, 20, 1f, 100f, 100f); splitBook.Record("S2", split, lap);
        split.AddWeapon(iron, 2, swords, 25, 1f, 100f, 100f); splitBook.Record("S2", split, lap.AddMinutes(10));
        var days = splitBook.Days[0].Tally.Weapons.Map["SwordIron|2|Swords"];
        Check(splitBook.Days.Count == 1 && days.Lo == 20 && days.Hi == 25 && Math.Abs(days.All - 200) < 0.001 && split.Weapons.Map["SwordIron|2|Swords"].PHi == -1,
              "a save gives its day the weapon's damage and the skill range seen since the save before it (20 then 25 across two saves: 20 to 25), and the next range starts again");

        // the cost per hit: no object per hit within a minute (a weapon's cell is found by the identity of its strings); a weapon's first hit in a minute makes its key
        var hot = new SwingClock(); var hotLog = new EventLog(); var hotLedger = new ArmourTally();
        void Swing(DateTime u) { var s0 = hot.Hit(0, u); hotLog.AddWeapon(u, iron, 3, swords, 40, s0, s0 > 0 ? 40f : 0f, 40f); hotLedger.AddWeapon(iron, 3, swords, 40, s0, s0 > 0 ? 40f : 0f, 40f); }
        for (int i = 0; i < 5000; i++) Swing(t0.AddSeconds(i % 500 * 0.1));   // warm
        const int N = 400000; long before = GC.GetAllocatedBytesForCurrentThread(); var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < N; i++) Swing(t0.AddSeconds(i % 500 * 0.1));
        var ns = sw.Elapsed.TotalMilliseconds * 1e6 / N; var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)N;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int m = 0; m < 600; m++) { Swing(t0.AddMinutes(100 + m)); Swing(t0.AddMinutes(100 + m).AddSeconds(1)); }
        var perNewMinute = (GC.GetAllocatedBytesForCurrentThread() - before) / 600.0;
        System.Console.WriteLine("MEASURED weapon cells, per hit: " + ns.ToString("0") + " ns and " + bytes.ToString("0.00") + " bytes allocated within a minute; " + perNewMinute.ToString("0") + " bytes for each new minute (the log's key and cell)");
        Check(bytes < 0.01 && perNewMinute < 1000, "no object per hit: " + bytes.ToString("0.00") + " bytes allocated per hit by the log's and the ledger's weapon cells; " + perNewMinute.ToString("0") + " bytes for each new minute");

        // the shared size: a busy evening (three hours, a fight in 100 of its minutes, three weapons at two levels in a minute at most, the skill rising) and a month (30 days, 8 weapons at 2 levels
        // a day, the cap 12): the copy with and without the weapons, raw and packed
        var busy = new EventLog(); var rnd = new Random(7); var skill = 30;
        string[] prefabs = { "AxeIron", "SwordIron", "BowFineWood", "MaceSilver", "StaffFireball", "SpearBronze" }; string[] skillNames = { "Axes", "Swords", "Bows", "Clubs", "ElementalMagic", "Spears" };
        var weaponStrings = prefabs.Select(p => new string(p.ToCharArray())).ToArray(); var skillStrings = skillNames.Select(p => new string(p.ToCharArray())).ToArray();
        for (int m = 0; m < 180; m++)
        {
            var at = t0.AddMinutes(m); if (m % 9 >= 5) continue;   // a fight in about 100 of the 180 minutes
            if (m % 20 == 0) skill++;
            for (int i = 0; i < 3; i++)
            {
                var w = (m / 7 + i) % prefabs.Length;
                busy.AddDamage(at, "Swamp", true, "Draugr", skillNames[w], D(slash: 80));   // folds the log as the hook does
                for (int h = 0; h < 6; h++) { var swing = (float)Math.Round(55 + rnd.NextDouble() * 50); busy.AddWeapon(at.AddSeconds(h * 2 + i), weaponStrings[w], 1 + (m / 40 + i) % 2, skillStrings[w], skill + i, h == 0 ? 0f : (float)(1 + rnd.NextDouble() * 2.5), h == 0 ? 0f : swing, swing); }
            }
        }
        var evening = new Json().Open(); busy.WriteTo(evening, "measuredLog"); var eveningText = evening.Close().ToString();
        var eveningNo = new Json().Open(); var noWeapons = new EventLog(); foreach (var kv in busy.Damage) noWeapons.Damage[kv.Key] = kv.Value; foreach (var kv in busy.Hits) noWeapons.Hits[kv.Key] = kv.Value;
        noWeapons.WriteTo(eveningNo, "measuredLog"); var eveningNoText = eveningNo.Close().ToString();
        var weaponsOnly = new Json().Open(); busy.Weapons.WriteTimed(weaponsOnly, "weapons"); var weaponsText = weaponsOnly.Close().ToString();
        // a typical evening: an hour of fighting in 60 of its minutes, one or two weapons a minute at one level, the skill steady
        var typical = new EventLog(); var typicalNo = new EventLog();
        for (int m = 0; m < 90; m++)
        {
            if (m % 3 == 2) continue; var at = t0.AddMinutes(m);
            for (int i = 0; i < 1 + m % 2; i++)
            {
                var w = (m / 30 + i) % 3;
                typical.AddDamage(at, "Swamp", true, "Draugr", skillNames[w], D(slash: 80)); typicalNo.AddDamage(at, "Swamp", true, "Draugr", skillNames[w], D(slash: 80));
                for (int h = 0; h < 6; h++) { var swing = (float)Math.Round(55 + rnd.NextDouble() * 50); typical.AddWeapon(at.AddSeconds(h * 2 + i), weaponStrings[w], 2, skillStrings[w], 34, h == 0 ? 0f : (float)(1 + rnd.NextDouble() * 2.5), h == 0 ? 0f : swing, swing); }
            }
        }
        var typicalJson = new Json().Open(); typical.WriteTo(typicalJson, "measuredLog"); var typicalText = typicalJson.Close().ToString();
        var typicalNoJson = new Json().Open(); typicalNo.WriteTo(typicalNoJson, "measuredLog"); var typicalNoText = typicalNoJson.Close().ToString();
        WeaponCells DayOf(int d, int weapons, int levels)   // a day's weapons: each at one upgrade level, the skill having risen over `levels` levels that day
        {
            var cells = new WeaponCells();
            for (int i = 0; i < weapons; i++)
            {
                var cell = new WeaponCell { Lo = 30 + d / 3, Hi = 30 + d / 3 + levels - 1 };
                for (int k = 0; k < levels; k++)
                {
                    var part = new LevelPart { Level = 30 + d / 3 + k, Sec = (float)Math.Round(100 + rnd.NextDouble() * 130, 1), Swung = rnd.Next(3600, 6500) };
                    if (cell.PartCount == cell.Parts.Length) Array.Resize(ref cell.Parts, 4);
                    cell.Parts[cell.PartCount++] = part; cell.Sec += part.Sec; cell.Swung += part.Swung; cell.All += part.Swung + rnd.Next(100, 900);
                }
                cells.Map[WeaponKey.Make(prefabs[i % prefabs.Length] + (i / prefabs.Length), 1 + i % 2, skillNames[i % prefabs.Length])] = cell;
            }
            return cells;
        }
        var monthDays = new Dictionary<string, WeaponCells>(); var typicalMonth = new Dictionary<string, WeaponCells>();
        for (int d = 0; d < 30; d++)
        {
            monthDays[DayHistory.DayKey(today.AddDays(d - 29))] = DayOf(d, 8, 3);   // the busy month: 8 weapons a day (the day row's cap is 12), the skill up 3 levels in each
            if (d % 2 == 0) typicalMonth[DayHistory.DayKey(today.AddDays(d - 29))] = DayOf(d, 4, 2);   // a typical one: 15 days played, 4 weapons, 2 levels
        }
        var monthJson = new Json().Open(); foreach (var kv in monthDays) kv.Value.Write(monthJson, kv.Key); var monthText = monthJson.Close().ToString();
        var typMonthJson = new Json().Open(); foreach (var kv in typicalMonth) kv.Value.Write(typMonthJson, kv.Key); var typMonthText = typMonthJson.Close().ToString();
        int Packed(string s) => Transport.Pack(s).Length;
        System.Console.WriteLine("INFO share: weapons add " + weaponsText.Length + " bytes of JSON (" + (Packed(eveningText) - Packed(eveningNoText)) + " packed) to a busy evening (" + busy.Weapons.Count + " minute cells, " +
                                 eveningNoText.Length + " bytes without), " + (Packed(typicalText) - Packed(typicalNoText)) + " packed to a typical one (" + typical.Weapons.Count + " cells) and " + typMonthText.Length + " bytes of JSON (" + Packed(typMonthText) + " packed) for a typical month (15 days, 4 weapons, 2 skill levels each) and " + monthText.Length + " bytes of JSON (" + Packed(monthText) + " packed) for a busy one (30 days, 8 weapons, 3 skill levels each)");
        Check(weaponsText.Length < 24000 && Packed(eveningText) - Packed(eveningNoText) < 6000 && monthText.Length < 25000 && Packed(monthText) < 8000 && Packed(typMonthText) < 2500,
              "the shared size stays small: a busy evening adds " + weaponsText.Length + " bytes of JSON (" + (Packed(eveningText) - Packed(eveningNoText)) + " packed), a typical month " + typMonthText.Length + " (" + Packed(typMonthText) + " packed), a busy one " + monthText.Length + " (" + Packed(monthText) + " packed)");

        // the live update (every 10 s, one message): two minutes into a busy fight on the busy evening's copy, base + the latest update reads back the weapons as sent, only the weapon cells that changed travel
        var skillInfos = new Snapshot.SkillInfo[0];
        string Full(EventLog l, IDictionary<string, WeaponCells> dw) => GroupShare.SharedCopy(Snapshot.Build("0.8.6", 42, "Edda", stats, skillInfos, "Midgard", new DamageTally(), "s1", new SessionEvents(), l, true, copyId: "c1", dayWeapons: dw));
        var baseShared = Full(busy, monthDays); var made = new List<LiveDelta.Made>(); var refused = new List<string>(); var liveNow = t0.AddMinutes(200);
        for (int step = 1; step <= 12; step++)
        {
            for (int h = 0; h < 5; h++) busy.AddWeapon(liveNow.AddSeconds(step * 10 + h), weaponStrings[0], 2, skillStrings[0], skill, 1.5f, 80f, 80f);   // one weapon, five hits every 10 s
            busy.AddWeapon(liveNow.AddSeconds(step * 10 + 5), weaponStrings[1], 1, skillStrings[1], skill, 1.5f, 70f, 70f);
            monthDays[DayHistory.DayKey(today)].Map["AxeIron0|1|Axes"] = new WeaponCell { Sec = 300 + step * 12, Swung = 11000 + step * 400, All = 12500 + step * 450, Lo = 31, Hi = 31, Parts = new[] { new LevelPart { Level = 31, Sec = 300 + step * 12, Swung = 11000 + step * 400 } }, PartCount = 1 };
            var m = LiveDelta.Make(baseShared, Full(busy, monthDays), "c1", step); made.Add(m);
            if (m.Text == null || !ServerIntake.IsJsonObject(m.Text) || !LiveDelta.Read(m.Text, out _, out _) || LiveDelta.Private(m.Text) || LiveDelta.Identity(m.Text) || m.TooBig) refused.Add("step " + step + (m.TooBig ? " too big" : " refused"));
        }
        var lastMade = made.Last();
        var setPaths = (MiniJson.Obj(MiniJson.Parse(lastMade.Text) as Dictionary<string, object>, "set") ?? new Dictionary<string, object>()).Keys.ToList();
        var applied = LiveDelta.Apply(FellowIds.WithPlatform(baseShared, "76561198000000042"), lastMade.Text);
        var read = applied == null ? null : Hearthwoven.Panel.PanelInput.FromSnapshot(applied);
        var sentDays = Hearthwoven.Panel.PanelInput.FromSnapshot(Full(busy, monthDays));
        System.Console.WriteLine("  live: the update since the last full copy, two minutes into a busy fight: " + string.Join(", ", made.Select(x => x.Packed?.Length ?? 0)) + " bytes packed (limit " + LiveDelta.MaxPacked + "); " + setPaths.Count + " paths set, " + lastMade.Text.Length + " bytes of JSON");
        Check(refused.Count == 0 && made.Max(x => x.Packed?.Length ?? 0) <= LiveDelta.MaxPacked && setPaths.Count > 0 && setPaths.All(x => x.StartsWith("/measuredLog/weapons/") || x.StartsWith("/dayWeapons/") || x.StartsWith("/measuredLog/damage/") || x.StartsWith("/measuredLog/hits/")) &&
              read != null && Same(read.Log.Weapons.Window(_ => true), sentDays.Log.Weapons.Window(_ => true)) && read.DayWeapons != null && sentDays.DayWeapons != null && read.DayWeapons.Count == sentDays.DayWeapons.Count && read.DayWeapons.All(d => Same(d.Value, sentDays.DayWeapons[d.Key])),
              "live: each update of a busy fight is one message the 0.8.1 server keeps (its JSON object, base and seq, nothing private, no identity key; at most " + made.Max(x => x.Packed?.Length ?? 0) + " bytes packed), only changed cells travel, and base + the latest update reads back the weapons as sent" + (refused.Count > 0 ? ": " + string.Join(", ", refused) : ""));
        return fails;
    }
}
