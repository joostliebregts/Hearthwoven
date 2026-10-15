using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Hearthwoven
{
    /// <summary>A weapon level's share at one whole skill level: its seconds of fighting and the damage swung in them (its pace; not all its damage, which the cell alone keeps).</summary>
    public struct LevelPart { public int Level; public float Sec, Swung; }

    /// <summary>
    /// What one weapon at one upgrade level did (0.8.6, DPS per named weapon): the seconds of fighting that ended on one of its hits
    /// (<see cref="Sec"/>), the damage swung in those seconds (<see cref="Swung"/>, the numerator of its DPS), all the damage of its hits
    /// (<see cref="All"/>, the number its row shows), and the lowest and highest level of its skill you had on those hits. Split by your whole skill
    /// level as well (<see cref="Parts"/>): the same three numbers for each skill level, so the trend shows (skill rises slowly: a few levels an evening).
    /// The parts a cell keeps are capped by the set that holds it (WeaponCells.PerMinuteLevels, PerDayLevels, PerLongLevels): the newest (highest) levels
    /// stay, the rest are in the totals only, so the totals always hold every hit.
    /// </summary>
    public sealed class WeaponCell
    {
        public float Sec, Swung, All;
        public int Lo = int.MaxValue, Hi = -1;      // the skill level range seen; Hi < 0: none
        public int PLo = int.MaxValue, PHi = -1;    // the same since the ledger's last save (the day a save goes to gets this range, not the whole session's)
        public bool HasLevel => Hi >= 0;
        static readonly LevelPart[] NoParts = new LevelPart[0];
        public LevelPart[] Parts = NoParts; public int PartCount;   // unsorted; SortedParts for reading

        public void Level(int level)
        {
            if (level < 0) return;
            if (level < Lo) Lo = level; if (level > Hi) Hi = level;
            if (level < PLo) PLo = level; if (level > PHi) PHi = level;
        }

        /// <summary>One hit's numbers at your skill level <paramref name="level"/>: its damage, and when it ended a stretch's seconds those and the damage swung in them.
        /// No allocation but the first hit at a new level (its part).</summary>
        public void Add(int level, float secs, float swung, float all, int levelCap)
        {
            var s = secs > 0f && Json.IsFinite(secs) && swung > 0f && Json.IsFinite(swung);
            All += all; if (s) { Sec += secs; Swung += swung; }
            Level(level);
            if (level >= 0) AddPart(level, s ? secs : 0f, s ? swung : 0f, levelCap);
        }

        public bool TryPart(int level, out LevelPart part) { var i = Find(level); part = i < 0 ? default(LevelPart) : Parts[i]; return i >= 0; }

        int Find(int level) { for (int i = 0; i < PartCount; i++) if (Parts[i].Level == level) return i; return -1; }

        void AddPart(int level, float sec, float swung, int cap)
        {
            var i = Find(level);
            if (i < 0)
            {
                if (PartCount < cap)
                {
                    if (PartCount == Parts.Length) Array.Resize(ref Parts, Math.Max(2, PartCount * 2));
                    i = PartCount++;
                }
                else   // full: a higher level takes the place of the lowest one (that one stays in the totals), a lower one is in the totals only
                {
                    int low = 0; for (int j = 1; j < PartCount; j++) if (Parts[j].Level < Parts[low].Level) low = j;
                    if (PartCount == 0 || level <= Parts[low].Level) return;
                    i = low;
                }
                Parts[i] = new LevelPart { Level = level };
            }
            Parts[i].Sec += sec; Parts[i].Swung += swung;
        }

        /// <summary>The parts from the lowest skill level to the highest.</summary>
        public LevelPart[] SortedParts()
        {
            var r = new LevelPart[PartCount]; Array.Copy(Parts, r, PartCount);
            Array.Sort(r, (x, y) => x.Level.CompareTo(y.Level));
            return r;
        }

        /// <summary>At most <paramref name="max"/> parts: the highest levels stay.</summary>
        public void ClipLevels(int max)
        {
            if (PartCount <= max) return;
            var sorted = SortedParts(); var keep = Math.Max(0, max);
            Array.Copy(sorted, sorted.Length - keep, Parts, 0, keep); PartCount = keep;
        }

        public void Merge(WeaponCell o, int levelCap = WeaponCells.MergeLevels)
        {
            Sec += o.Sec; Swung += o.Swung; All += o.All;
            if (o.HasLevel) { if (o.Lo < Lo) Lo = o.Lo; if (o.Hi > Hi) Hi = o.Hi; }
            for (int i = 0; i < o.PartCount; i++) AddPart(o.Parts[i].Level, o.Parts[i].Sec, o.Parts[i].Swung, levelCap);
        }

        public WeaponCell Copy()
        {
            var c = new WeaponCell { Sec = Sec, Swung = Swung, All = All, Lo = Lo, Hi = Hi, PartCount = PartCount };
            if (PartCount > 0) { c.Parts = new LevelPart[PartCount]; Array.Copy(Parts, c.Parts, PartCount); }
            return c;
        }

        /// <summary>"Swords 45", or "Swords 38–45" when it changed.</summary>
        public string SkillWords(string skill) => !HasLevel ? skill : Lo == Hi ? skill + " " + Lo : skill + " " + Lo + "–" + Hi;

        /// <summary>The most recent pace: the part at the highest skill level that has at least <paramref name="floor"/> seconds of fighting, and the part at the lowest
        /// such level (the same one when only one reaches the floor). Has false when no level reaches it (the window's own pace then, with the dash rule).</summary>
        public Trend Recent(float floor)
        {
            int best = -1, low = -1;
            for (int i = 0; i < PartCount; i++)
            {
                if (!(Parts[i].Sec >= floor) || !(Parts[i].Swung > 0f)) continue;
                if (best < 0 || Parts[i].Level > Parts[best].Level) best = i;
                if (low < 0 || Parts[i].Level < Parts[low].Level) low = i;
            }
            if (best < 0) return default(Trend);
            return new Trend { Has = true, Level = Parts[best].Level, Dps = Parts[best].Swung / Parts[best].Sec, LowLevel = Parts[low].Level, LowDps = Parts[low].Swung / Parts[low].Sec };
        }
    }

    /// <summary>A weapon level's most recent pace and the pace at the lowest skill level that reached the floor (WeaponCell.Recent).</summary>
    public struct Trend { public bool Has; public int Level, LowLevel; public double Dps, LowDps; }

    /// <summary>The key of a weapon cell: "prefab|quality|skill" ("SwordIron|3|Swords"); what is no named weapon or beyond the cap is "~other|0|skill".</summary>
    public static class WeaponKey
    {
        public const string Other = "~other";
        public static string Make(string prefab, int quality, string skill) => prefab + "|" + quality.ToString(CultureInfo.InvariantCulture) + "|" + skill;
        public static string OtherOf(string skill) => Other + "|0|" + skill;

        /// <summary>Splits a key (without a minute) into its three parts; false when it is not one.</summary>
        public static bool Split(string key, out string prefab, out int quality, out string skill)
        {
            prefab = skill = ""; quality = 0;
            if (key == null) return false;
            var a = key.IndexOf('|'); if (a <= 0) return false;
            var b = key.IndexOf('|', a + 1); if (b < 0 || b == key.Length - 1) return false;
            if (!int.TryParse(key.Substring(a + 1, b - a - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out quality)) return false;
            prefab = key.Substring(0, a); skill = key.Substring(b + 1);
            return prefab.Length > 0;
        }
    }

    /// <summary>
    /// Weapon cells under keys: in a log per minute ("2026-10-07T20:14Z|SwordIron|3|Swords", <c>timed</c>), in a ledger tally without a time
    /// ("SwordIron|3|Swords"). Recording a hit allocates nothing: the cells of the current minute (the epoch) are found by the identity of the
    /// weapon's prefab and skill strings, which the hooks keep once per item; only the first hit of a weapon in a minute makes its key. At most
    /// <c>cap</c> named weapons a minute (a day row, a session); the rest fall into the skill's "other" cell, so a key count can never run away.
    /// Pure C#, tested (test/WeaponTests.cs).
    /// </summary>
    public sealed class WeaponCells
    {
        public const int PerMinute = 12, PerDay = 12, PerSession = 40, PerLong = 40;
        /// <summary>Skill levels kept per weapon and upgrade level: 4 in a minute, 8 in a day row and in this session, 12 in a week, month or the earlier sessions; the highest stay.
        /// Merging keeps up to MergeLevels (a window's sum), the page shows the newest 12.</summary>
        public const int PerMinuteLevels = 4, PerDayLevels = 8, PerLongLevels = 12, MergeLevels = 32;
        /// <summary>The prefab name of your bare hands (no item in the world has one).</summary>
        public const string Fists = "Fists";
        public readonly Dictionary<string, WeaponCell> Map = new Dictionary<string, WeaponCell>();
        readonly bool timed;
        public WeaponCells(bool timed = false) { this.timed = timed; }
        public int Count => Map.Count;

        // the hit path's cache: this epoch's cells by the identity of (prefab, quality, skill)
        string[] cPrefab = new string[16], cSkill = new string[16]; int[] cQ = new int[16]; WeaponCell[] cCell = new WeaponCell[16];
        int cCount, cNamed; long cEpoch = long.MinValue;

        /// <summary>One hit of yours with a weapon: <paramref name="all"/> its damage, <paramref name="secs"/> the fighting time it ended (0 for the first hit of a stretch)
        /// with <paramref name="swung"/> the damage swung in them, <paramref name="level"/> your level in <paramref name="skill"/> then. No allocation but the
        /// key of a weapon's first hit in a minute.</summary>
        public void Hit(DateTime utc, string prefab, int quality, string skill, int level, float secs, float swung, float all, int cap = PerMinute, int levelCap = PerMinuteLevels)
        {
            if (!(all > 0f) || !Json.IsFinite(all) || string.IsNullOrEmpty(skill)) return;
            For(utc, prefab, quality, skill, cap).Add(level, secs, swung, all, levelCap);
        }

        WeaponCell For(DateTime utc, string prefab, int quality, string skill, int cap)
        {
            var epoch = timed ? utc.Ticks / TimeSpan.TicksPerMinute : 0;
            if (epoch != cEpoch) { cEpoch = epoch; cCount = 0; cNamed = 0; }
            for (int i = 0; i < cCount; i++)
                if (ReferenceEquals(cPrefab[i], prefab) && cQ[i] == quality && ReferenceEquals(cSkill[i], skill)) return cCell[i];
            var prefix = timed ? EventLog.Bucket(utc) + "|" : "";
            var key = string.IsNullOrEmpty(prefab) ? null : prefix + WeaponKey.Make(prefab, quality, skill);
            // a weapon already in the cells keeps its cell and is not counted again (a switch back is no new weapon): only a new named key counts toward the cap
            if (key == null || (!Map.ContainsKey(key) && cNamed >= cap)) key = prefix + WeaponKey.OtherOf(skill);
            else if (!Map.ContainsKey(key)) cNamed++;
            if (!Map.TryGetValue(key, out var cell)) Map[key] = cell = new WeaponCell();
            if (cCount == cPrefab.Length && cCount < 256)
            {
                Array.Resize(ref cPrefab, cCount * 2); Array.Resize(ref cSkill, cCount * 2); Array.Resize(ref cQ, cCount * 2); Array.Resize(ref cCell, cCount * 2);
            }
            if (cCount < cPrefab.Length) { cPrefab[cCount] = prefab; cSkill[cCount] = skill; cQ[cCount] = quality; cCell[cCount] = cell; cCount++; }
            return cell;
        }

        /// <summary>Forget the hit path's cache (cells were removed or moved by a fold or a clip).</summary>
        void Uncache() { cCount = 0; cNamed = 0; cEpoch = long.MinValue; }

        public void Merge(WeaponCells o)
        {
            if (o == null) return;
            foreach (var kv in o.Map) Merge(kv.Key, kv.Value);
        }

        public void Merge(string key, WeaponCell c)
        {
            if (!Map.TryGetValue(key, out var mine)) { Map[key] = c.Copy(); return; }
            mine.Merge(c);
        }

        /// <summary>The cells of a timed set whose minute is at or after <paramref name="lo"/> and before <paramref name="hi"/> (null: no end), as a new set.</summary>
        public WeaponCells Between(string lo, string hi)
        {
            var b = new WeaponCells(timed);
            foreach (var kv in Map)
            {
                var k = kv.Key;
                if (k.Length < 17 || string.CompareOrdinal(k, 0, lo, 0, 17) < 0 || (hi != null && string.CompareOrdinal(k, 0, hi, 0, 17) >= 0)) continue;
                b.Merge(k, kv.Value);
            }
            return b;
        }

        /// <summary>Folds the minutes older than <paramref name="cut"/> into ten-minute buckets (EventLog.FoldKeys' rule: the minute's last digit to 0).</summary>
        public void Fold(string cut)
        {
            List<KeyValuePair<string, WeaponCell>> move = null;
            foreach (var kv in Map)
            {
                var k = kv.Key;
                if (k.Length > 16 && k[15] != '0' && string.CompareOrdinal(k, 0, cut, 0, 17) < 0) (move ?? (move = new List<KeyValuePair<string, WeaponCell>>())).Add(kv);
            }
            if (move == null) return;
            foreach (var kv in move) { Map.Remove(kv.Key); Merge(kv.Key.Substring(0, 15) + "0" + kv.Key.Substring(16), kv.Value); }
            Uncache();
        }

        /// <summary>At most <paramref name="max"/> named weapon cells: the ones with the least damage fall into their skill's "other" cell.</summary>
        public void Clip(int max)
        {
            var named = Map.Where(kv => !IsOther(kv.Key)).OrderByDescending(kv => kv.Value.All).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            if (named.Count <= max) return;
            foreach (var kv in named.Skip(max))
            {
                Map.Remove(kv.Key);
                if (WeaponKey.Split(kv.Key, out _, out _, out var skill)) Merge(WeaponKey.OtherOf(skill), kv.Value);
            }
            Uncache();
        }

        /// <summary>At most <paramref name="max"/> skill levels in each cell (the highest stay; the totals keep every hit).</summary>
        public void ClipLevels(int max) { foreach (var c in Map.Values) c.ClipLevels(max); }

        public static bool IsOther(string key) => key != null && key.StartsWith(WeaponKey.Other + "|", StringComparison.Ordinal);

        /// <summary>The ledger's save went to a day row: the next save's range starts again.</summary>
        public void ResetPending() { foreach (var c in Map.Values) { c.PLo = int.MaxValue; c.PHi = -1; } }

        /// <summary>What <paramref name="now"/> holds beyond <paramref name="was"/> (null: nothing before), never below zero: what one save added. The range of a
        /// cell is the one seen since the last save (PLo, PHi).</summary>
        public static WeaponCells Minus(WeaponCells now, WeaponCells was)
        {
            var d = new WeaponCells();
            if (now == null) return d;
            foreach (var kv in now.Map)
            {
                WeaponCell w = null; was?.Map.TryGetValue(kv.Key, out w);
                var n = kv.Value;
                float s = n.Sec - (w?.Sec ?? 0f), sw = n.Swung - (w?.Swung ?? 0f), a = n.All - (w?.All ?? 0f);
                if (!(s > 0f) && !(sw > 0f) && !(a > 0f)) continue;
                var c = new WeaponCell { Sec = Math.Max(0f, s), Swung = Math.Max(0f, sw), All = Math.Max(0f, a) };
                for (int i = 0; i < n.PartCount; i++)   // each skill level's share since the save before
                {
                    LevelPart before = default(LevelPart); w?.TryPart(n.Parts[i].Level, out before);
                    float ps = n.Parts[i].Sec - before.Sec, pw = n.Parts[i].Swung - before.Swung;
                    if (!(ps > 0f) && !(pw > 0f)) continue;
                    if (c.PartCount == c.Parts.Length) Array.Resize(ref c.Parts, Math.Max(2, c.PartCount * 2));
                    c.Parts[c.PartCount++] = new LevelPart { Level = n.Parts[i].Level, Sec = Math.Max(0f, ps), Swung = Math.Max(0f, pw) };
                }
                if (c.PartCount > 0) { for (int i = 0; i < c.PartCount; i++) { if (c.Parts[i].Level < c.Lo) c.Lo = c.Parts[i].Level; if (c.Parts[i].Level > c.Hi) c.Hi = c.Parts[i].Level; } }
                else if (n.PHi >= 0) { c.Lo = n.PLo; c.Hi = n.PHi; } else if (n.HasLevel) { c.Lo = n.Lo; c.Hi = n.Hi; }
                d.Map[kv.Key] = c;
            }
            return d;
        }

        /// <summary>Everything in a set whose key starts with a minute inside the filter, summed per weapon key without the minute (a window's weapons from a log).</summary>
        public WeaponCells Window(Func<string, bool> minuteIn)
        {
            var r = new WeaponCells();
            foreach (var kv in Map)
            {
                var k = kv.Key; var cut = k.IndexOf('|');
                if (!timed || cut < 0) { r.Merge(k, kv.Value); continue; }
                if (minuteIn(k.Substring(0, cut))) r.Merge(k.Substring(cut + 1), kv.Value);
            }
            return r;
        }

        // ---------- JSON ----------
        // a cell is one string, "seconds (a tenth),swung,all,lowest level,highest level" ("12.3,540,600,38,45"; a level of -1: none seen): one value to a cell, so a live update
        // (LiveDelta compares values) sends a changed cell once, not five numbers under five long paths

        // after the totals ";level,seconds,swung" for each skill level, lowest first; left out when the cell has one level that holds every hit (a minute usually does)
        static string Cell(WeaponCell c, bool round)
        {
            double R1(double v) => round ? Math.Round(v, 1) : v; double R0(double v) => round ? Math.Round(v) : v;
            var sb = new StringBuilder("\"");
            sb.Append(Json.F(R1(c.Sec))).Append(',').Append(Json.F(R0(c.Swung))).Append(',').Append(Json.F(R0(c.All))).Append(',')
              .Append(c.HasLevel ? c.Lo : -1).Append(',').Append(c.HasLevel ? c.Hi : -1);
            var single = c.PartCount == 1 && c.Lo == c.Hi && c.Parts[0].Level == c.Lo && c.Parts[0].Sec == c.Sec && c.Parts[0].Swung == c.Swung;
            if (!single) foreach (var p in c.SortedParts()) sb.Append(';').Append(p.Level).Append(',').Append(Json.F(R1(p.Sec))).Append(',').Append(Json.F(R0(p.Swung)));
            return sb.Append('"').ToString();
        }

        /// <summary>An untimed set as an object { "prefab|q|skill": cell }; rounded for a day row. A cell with nothing in it is left out.</summary>
        public void Write(Json j, string key, bool round = true)
        {
            j.Key(key).Open();
            foreach (var kv in Map.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                if (kv.Value.All > 0f || kv.Value.Sec > 0f) j.Raw(kv.Key, Cell(kv.Value, round));
            j.Close();
        }

        /// <summary>A timed set as { "minute": { "prefab|q|skill": cell } }, the minutes in order (a live update then changes only the minutes that changed).</summary>
        public void WriteTimed(Json j, string key)
        {
            j.Key(key).Open();
            string minute = null;
            foreach (var kv in Map.Where(kv => kv.Key.Length > 17 && kv.Value.All > 0f).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var m = kv.Key.Substring(0, kv.Key.IndexOf('|'));
                if (m != minute) { if (minute != null) j.Close(); j.Key(m).Open(); minute = m; }
                j.Raw(kv.Key.Substring(m.Length + 1), Cell(kv.Value, true));
            }
            if (minute != null) j.Close();
            j.Close();
        }

        static bool Num(string t, out double v) => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && Json.IsFinite(v);

        static bool ReadCell(object o, out WeaponCell c)
        {
            c = null;
            if (!(o is string text)) return false;
            var segs = text.Split(';');
            var p = segs[0].Split(',');
            if (p.Length != 5 || segs.Length > MergeLevels + 1) return false;
            var v = new double[5];
            for (int i = 0; i < 5; i++) if (!Num(p[i], out v[i])) return false;
            double sec = v[0], sw = v[1], all = v[2], lo = v[3], hi = v[4];
            if (sec < 0 || sw < 0 || all < 0 || sec > 1e7 || sw > 1e9 || all > 1e9) return false;
            c = new WeaponCell { Sec = (float)sec, Swung = (float)sw, All = (float)all };
            if (hi >= 0 && lo >= 0 && lo <= hi && hi <= 9999) { c.Lo = (int)lo; c.Hi = (int)hi; }
            for (int k = 1; k < segs.Length; k++)
            {
                var q = segs[k].Split(',');
                if (q.Length != 3 || !Num(q[0], out var level) || !Num(q[1], out var ps) || !Num(q[2], out var pw)) continue;
                if (level < 0 || level > 9999 || level != Math.Floor(level) || ps < 0 || pw < 0 || ps > 1e7 || pw > 1e9) continue;
                if (c.PartCount == c.Parts.Length) Array.Resize(ref c.Parts, Math.Max(2, c.PartCount * 2));
                c.Parts[c.PartCount++] = new LevelPart { Level = (int)level, Sec = (float)ps, Swung = (float)pw };
            }
            if (segs.Length == 1 && c.HasLevel && c.Lo == c.Hi)   // one level held every hit: the part is the totals
                { c.Parts = new[] { new LevelPart { Level = c.Lo, Sec = c.Sec, Swung = c.Swung } }; c.PartCount = 1; }
            return true;
        }

        /// <summary>Reads what Write wrote: bounded, hostile keys and cells skipped.</summary>
        public static WeaponCells Read(Dictionary<string, object> o, int maxKeys = 400)
        {
            var r = new WeaponCells();
            if (o == null) return r;
            foreach (var kv in o)
            {
                if (r.Map.Count >= maxKeys) break;
                if (!WeaponKey.Split(kv.Key, out _, out _, out _) || !ReadCell(kv.Value, out var c)) continue;
                r.Map[kv.Key] = c;
            }
            return r;
        }

        /// <summary>Reads what WriteTimed wrote.</summary>
        public static WeaponCells ReadTimed(Dictionary<string, object> o, int maxKeys = 20000)
        {
            var r = new WeaponCells(true);
            if (o == null) return r;
            foreach (var minute in o)
            {
                if (minute.Key.Length < 17 || !(minute.Value is Dictionary<string, object> cells)) continue;
                foreach (var kv in cells)
                {
                    if (r.Map.Count >= maxKeys) return r;
                    if (!WeaponKey.Split(kv.Key, out _, out _, out _) || !ReadCell(kv.Value, out var c)) continue;
                    r.Map[minute.Key + "|" + kv.Key] = c;
                }
            }
            return r;
        }
    }
}
