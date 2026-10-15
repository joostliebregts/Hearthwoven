using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// The armour ledger (0.7, work/hearthwoven-0.7/ARMOUR-SCOPE.md): hits on your own character as they reached your armour and as
    /// they left it, measured at the game's own steps on this PC (ArmourHooks). Three stages per "source|cause|type", summed:
    /// Incoming (before your resistances, after a block and your ward), Before (after resistances = before armour) and After (after
    /// armour). Only the eight battle types (armour never acts on falls, chop or pickaxe damage).
    ///
    /// A ledger of its own: never added to DamageTally, the event log, the biome tally or what is shared. Its "after armour" sits earlier
    /// in the game's pipeline than "Received from" (before the world's damage setting, with fire, spirit and poison as the blow sets
    /// them), so the two are never summed or shown as one number. Pure C# (no Unity calls), unit-tested.
    /// </summary>
    public class ArmourTally
    {
        public static readonly string[] Types = { "blunt", "slash", "pierce", "fire", "frost", "lightning", "poison", "spirit" };
        public readonly Dictionary<string, float> Incoming = new Dictionary<string, float>();   // "Troll|EnemyHit|blunt" -> sum
        public readonly Dictionary<string, float> Before = new Dictionary<string, float>();
        public readonly Dictionary<string, float> After = new Dictionary<string, float>();
        /// <summary>Hits that reached your armour with damage (before armour above zero).</summary>
        public int Hits;
        /// <summary>A day row reached its key cap: its smallest keys were left out.</summary>
        public bool Clipped;
        /// <summary>0.8.6, the dealt ledger only (DPS, SwingClock): per weapon kind the seconds of fighting ("melee|s") and the damage swung in them
        /// ("melee|d", the hits after the first of each stretch), under SwingClock.SecondsKeys and DamageKeys. At most six keys, so a day row's key
        /// cap (Clip) leaves them alone. Never shared.</summary>
        public readonly Dictionary<string, float> Swing = new Dictionary<string, float>();

        /// <summary>0.8.6, the dealt ledger only (DPS per named weapon): per weapon at its upgrade level ("SwordIron|3|Swords") the seconds of fighting, the damage swung in
        /// them, all its damage and the range of your skill level. A session holds at most WeaponCells.PerSession named weapons, a day row PerDay (Clip); the rest fall
        /// into the skill's "other" cell. Your own book reads all windows from it; the last 30 days' day rows are shared (dayWeapons).</summary>
        public readonly WeaponCells Weapons = new WeaponCells();

        public bool Empty => Hits == 0 && Incoming.Count == 0 && Before.Count == 0 && After.Count == 0 && Swing.Count == 0 && Weapons.Count == 0;

        /// <summary>A hit of yours with a weapon, in the ledger (the log's twin, EventLog.AddWeapon): every hit adds its damage and the skill level; a hit that ended a
        /// stretch's seconds adds those and the damage swung in them.</summary>
        public void AddWeapon(string prefab, int quality, string skill, int level, float seconds, float swung, float all) =>
            Weapons.Hit(DateTime.MinValue, prefab, quality, skill, level, seconds, swung, all, WeaponCells.PerSession, WeaponCells.PerDayLevels);

        /// <summary>A hit's fighting time (0.8.6): <paramref name="seconds"/> and the <paramref name="damage"/> swung in them, under a weapon kind
        /// (SwingClock.Kinds index). Nothing for a hit that starts a stretch (no seconds).</summary>
        public void AddSwing(int kind, float seconds, float damage)
        {
            if (!(seconds > 0f) || !(damage > 0f) || !Json.IsFinite(seconds) || !Json.IsFinite(damage)) return;
            Add(Swing, SwingClock.SecondsKeys[kind], seconds); Add(Swing, SwingClock.DamageKeys[kind], damage);
        }

        /// <summary>The seconds and the damage of a weapon kind ("melee", "bow", "magic").</summary>
        public (double seconds, double damage) SwingOf(string kind)
        {
            Swing.TryGetValue(kind + "|s", out var s); Swing.TryGetValue(kind + "|d", out var d);
            return (s, d);
        }
        IEnumerable<Dictionary<string, float>> Maps() { yield return Incoming; yield return Before; yield return After; }

        static void Add(Dictionary<string, float> d, string key, float v) { if (v == 0f || !Json.IsFinite(v)) return; d.TryGetValue(key, out var o); d[key] = o + v; }

        /// <summary>The eight battle types of a hit, in Types order.</summary>
        public static float[] Of(HitData.DamageTypes t) => new[] { t.m_blunt, t.m_slash, t.m_pierce, t.m_fire, t.m_frost, t.m_lightning, t.m_poison, t.m_spirit };
        static float SumOf(float[] v) { float s = 0; foreach (var x in v) if (x > 0 && Json.IsFinite(x)) s += x; return s; }

        /// <summary>One hit on your armour: the three stages of the same blow. Nothing is kept when all three are zero (a ward or a parry
        /// took it all); a hit counts when anything reached the armour. True when something was kept.</summary>
        public bool Add(string source, string cause, HitData.DamageTypes incoming, HitData.DamageTypes before, HitData.DamageTypes after)
        {
            var i = Of(incoming); var b = Of(before); var a = Of(after);
            if (SumOf(i) <= 0 && SumOf(b) <= 0 && SumOf(a) <= 0) return false;
            var head = (source ?? "?") + "|" + (cause ?? "?") + "|";
            for (int k = 0; k < Types.Length; k++)
            {
                if (i[k] > 0) Add(Incoming, head + Types[k], i[k]);
                if (b[k] > 0) Add(Before, head + Types[k], b[k]);
                if (a[k] > 0) Add(After, head + Types[k], a[k]);
            }
            if (SumOf(b) > 0) Hits++;
            return true;
        }

        /// <summary>A stage's sum (every key; a key's last part is the type).</summary>
        public static double Total(Dictionary<string, float> d) { double s = 0; foreach (var v in d.Values) if (v > 0) s += v; return s; }

        public void AddAll(ArmourTally o)
        {
            if (o == null) return;
            Hits += o.Hits; Clipped |= o.Clipped;
            foreach (var kv in o.Incoming) Add(Incoming, kv.Key, kv.Value);
            foreach (var kv in o.Before) Add(Before, kv.Key, kv.Value);
            foreach (var kv in o.After) Add(After, kv.Key, kv.Value);
            foreach (var kv in o.Swing) Add(Swing, kv.Key, kv.Value);
            Weapons.Merge(o.Weapons);
        }

        public static ArmourTally Sum(params ArmourTally[] parts) { var s = new ArmourTally(); foreach (var p in parts) s.AddAll(p); return s; }

        /// <summary>What <paramref name="now"/> holds beyond <paramref name="was"/> (null: nothing before), never below zero: what one save added.</summary>
        public static ArmourTally Minus(ArmourTally now, ArmourTally was)
        {
            var d = new ArmourTally();
            if (now == null) return d;
            d.Hits = Math.Max(0, now.Hits - (was?.Hits ?? 0));
            void M(Dictionary<string, float> n, Dictionary<string, float> w, Dictionary<string, float> into)
            {
                foreach (var kv in n) { float o = 0; w?.TryGetValue(kv.Key, out o); if (kv.Value - o > 0) Add(into, kv.Key, kv.Value - o); }
            }
            M(now.Incoming, was?.Incoming, d.Incoming); M(now.Before, was?.Before, d.Before); M(now.After, was?.After, d.After); M(now.Swing, was?.Swing, d.Swing);
            d.Weapons.Merge(WeaponCells.Minus(now.Weapons, was?.Weapons));
            return d;
        }

        public int Keys => Incoming.Count + Before.Count + After.Count;

        /// <summary>At most <paramref name="max"/> keys over the three stages: the smallest go (as DayHistory.Row.Clip).</summary>
        public void Clip(int max)
        {
            if (Keys <= max) return;
            var all = Maps().SelectMany(m => m.Select(kv => (m, kv.Key, kv.Value))).OrderBy(e => e.Value).ThenBy(e => e.Key, StringComparer.Ordinal).Take(Keys - max).ToList();
            foreach (var e in all) e.m.Remove(e.Key);
            Clipped = true;
        }

        static IEnumerable<KeyValuePair<string, float>> Rounded(Dictionary<string, float> d, bool round) =>
            round ? d.Where(kv => Math.Round(kv.Value, 1) != 0).Select(kv => new KeyValuePair<string, float>(kv.Key, (float)Math.Round(kv.Value, 1))) : d;

        /// <summary>The fields inside an already opened object: hits, incoming, before, after (a day row rounds to tenths).</summary>
        public void WriteFields(Json j, bool round = false)
        {
            if (Hits != 0) j.Num("hits", Hits);
            if (Clipped) j.Num("clipped", 1);
            if (Incoming.Count > 0) j.Dict("incoming", Rounded(Incoming, round));
            if (Before.Count > 0) j.Dict("before", Rounded(Before, round));
            if (After.Count > 0) j.Dict("after", Rounded(After, round));
            if (Swing.Count > 0) j.Dict("swing", Rounded(Swing, round));   // 0.8.6: an older Hearthwoven ignores the key
            if (Weapons.Count > 0) Weapons.Write(j, "weapons", round);
        }

        public void WriteTo(Json j, string key) { j.Key(key).Open(); WriteFields(j); j.Close(); }

        public ArmourTally ReadFrom(Dictionary<string, object> o)
        {
            if (o == null) return this;
            Hits = (int)MiniJson.Num(o, "hits"); Clipped = MiniJson.Num(o, "clipped") > 0;
            MiniJson.Into(MiniJson.Obj(o, "incoming"), Incoming); MiniJson.Into(MiniJson.Obj(o, "before"), Before); MiniJson.Into(MiniJson.Obj(o, "after"), After);
            MiniJson.Into(MiniJson.Obj(o, "swing"), Swing);
            foreach (var kv in WeaponCells.Read(MiniJson.Obj(o, "weapons")).Map) Weapons.Map[kv.Key] = kv.Value;
            return this;
        }
    }

    /// <summary>
    /// The armour ledger of this session in one-minute buckets, for Battle's short windows (10 min .. 3 h): "bucket|stage|type" -> sum and
    /// "bucket" -> hits. No source, no biome (small); folded to ten-minute buckets after EventLog.FoldAfterMinutes like the event log. Kept
    /// apart from EventLog on purpose: the log is shared in the snapshot and older fellows' panels read every direction they find in it.
    /// </summary>
    public class ArmourLog
    {
        public readonly Dictionary<string, float> Damage = new Dictionary<string, float>();
        public readonly Dictionary<string, float> Hits = new Dictionary<string, float>();
        string lastFold = "";

        public void Add(DateTime utc, HitData.DamageTypes incoming, HitData.DamageTypes before, HitData.DamageTypes after)
        {
            var bucket = EventLog.Bucket(utc);
            if (bucket != lastFold) { lastFold = bucket; var cut = EventLog.Bucket(utc.AddMinutes(-EventLog.FoldAfterMinutes)); EventLog.FoldKeys(Damage, cut); EventLog.FoldKeys(Hits, cut); }
            var i = ArmourTally.Of(incoming); var b = ArmourTally.Of(before); var a = ArmourTally.Of(after);
            bool any = false, reached = false;
            for (int k = 0; k < ArmourTally.Types.Length; k++)
            {
                if (One(bucket, "incoming", k, i[k])) any = true;
                if (One(bucket, "before", k, b[k])) { any = true; reached = true; }
                if (One(bucket, "after", k, a[k])) any = true;
            }
            if (any && reached) { Hits.TryGetValue(bucket, out var h); Hits[bucket] = h + 1; }
        }

        bool One(string bucket, string stage, int k, float v)
        {
            if (!(v > 0) || !Json.IsFinite(v)) return false;
            var key = bucket + "|" + stage + "|" + ArmourTally.Types[k];
            Damage.TryGetValue(key, out var o); Damage[key] = o + v;
            return true;
        }

        /// <summary>The buckets that reach past <paramref name="cutoffUtc"/> (null: all), as a tally with an empty source and cause ("||slash").</summary>
        public ArmourTally Since(DateTime? cutoffUtc)
        {
            var t = new ArmourTally();
            bool In(string bucket) => !cutoffUtc.HasValue || (DateTime.TryParseExact(bucket, "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) && at.AddMinutes(EventLog.BucketMinutes) > cutoffUtc.Value);
            foreach (var kv in Damage)
            {
                var p = kv.Key.Split('|'); if (p.Length != 3 || !In(p[0])) continue;
                var into = p[1] == "incoming" ? t.Incoming : p[1] == "before" ? t.Before : p[1] == "after" ? t.After : null;
                if (into == null) continue;
                var key = "||" + p[2]; into.TryGetValue(key, out var o); into[key] = o + kv.Value;
            }
            foreach (var kv in Hits) if (In(kv.Key)) t.Hits += (int)kv.Value;
            return t;
        }
    }

    /// <summary>
    /// The armour ledger kept on this PC (LocalTotals top-level "armour", schema 4): when it began (From: the first run of a version that records
    /// armour, the "Recorded from" date), the earlier sessions folded, the latest saved session under its own id (an older Hearthwoven can
    /// rewrite the rest of the file while keeping this key as it is, so it never shares LocalTotals' session id), and its own day rows, outside
    /// "history" (0.6.1 and 0.6.2 rewrite history rows with only the fields they know). Day rows fold to weeks and months by DayHistory's rule.
    /// JSON: {"from":"ISO","previous":{..},"lastSession":{"id":"..",..},"days":[{"p":"2026-10-09","hits":..,"incoming":{..},"before":{..},"after":{..}}]}.
    /// </summary>
    public class ArmourBook
    {
        /// <summary>When armour began to be recorded for this character on this PC; DateTime.MinValue = not yet (set on load, Plugin.LoadLocal).</summary>
        public DateTime FromUtc = DateTime.MinValue;
        /// <summary>0.8.6 (DPS): when your fighting time began to be recorded on this PC (the first hit of a version that records it); MinValue = not yet.
        /// Only the dealt ledger's book has it: the "from" date of a weapon's pace on All.</summary>
        public DateTime SwingFromUtc = DateTime.MinValue;
        public ArmourTally Previous = new ArmourTally();
        public string LastSession = "";
        public ArmourTally Last = new ArmourTally();
        /// <summary>Oldest first: "before", months, weeks, days (DayHistory's period keys).</summary>
        public readonly List<(string Period, ArmourTally Tally)> Days = new List<(string, ArmourTally)>();

        public bool IsEmpty => FromUtc == DateTime.MinValue && SwingFromUtc == DateTime.MinValue && Previous.Empty && Last.Empty && LastSession.Length == 0 && Days.Count == 0;

        /// <summary>Everything recorded before the session <paramref name="current"/> (the latest saved one unless it is that session).</summary>
        public ArmourTally BeforeSession(string current) => ArmourTally.Sum(Previous, LastSession == current ? null : Last);

        /// <summary>What the running session holds beyond its last save: the live part of today.</summary>
        public ArmourTally Pending(string session, ArmourTally now) => ArmourTally.Minus(now, !string.IsNullOrEmpty(session) && session == LastSession ? Last : null);

        /// <summary>Records a session's ledger as it is now (as LocalTotals.Record): the difference to its last save goes to the day of
        /// <paramref name="localNow"/>; a new session id first folds the latest saved one into Previous.</summary>
        public void Record(string session, ArmourTally now, DateTime localNow)
        {
            if (string.IsNullOrEmpty(session) || now == null) return;
            var same = session == LastSession;
            var diff = ArmourTally.Minus(now, same ? Last : null);
            if (!diff.Empty) AddDay(localNow.Date, diff);
            if (!same) { if (LastSession.Length > 0) { Previous.AddAll(Last); Previous.Weapons.Clip(WeaponCells.PerLong); Previous.Weapons.ClipLevels(WeaponCells.PerLongLevels); } LastSession = session; }
            Last = ArmourTally.Sum(now);
            now.Weapons.ResetPending();   // 0.8.6: the next save's skill range starts here
        }

        void AddDay(DateTime day, ArmourTally diff)
        {
            var key = DayHistory.DayKey(day);
            var last = Days.Count > 0 ? Days[Days.Count - 1] : default;
            ArmourTally row;
            if (Days.Count > 0 && (last.Period == key || DayHistory.StartOf(last.Period) > day)) row = last.Tally;   // the clock went back: never a row out of order
            else { row = new ArmourTally(); Days.Add((key, row)); }
            row.AddAll(diff); row.Clip(DayHistory.MaxRowKeys); row.Weapons.Clip(WeaponCells.PerDay); row.Weapons.ClipLevels(WeaponCells.PerDayLevels);   // 0.8.6: a day row keeps at most PerDay named weapons
            Fold(day);
        }

        /// <summary>Folds old day rows by DayHistory's rule: days for DayRows days, then weeks, months, "before". Every sum is kept.</summary>
        public void Fold(DateTime today)
        {
            today = today.Date;
            var dayCut = today.AddDays(-(DayHistory.DayRows - 1));
            var weekCut = DayHistory.Monday(dayCut).AddDays(-7 * DayHistory.WeekRows);
            var monthCut = new DateTime(weekCut.Year, weekCut.Month, 1).AddMonths(-DayHistory.MonthRows);
            string Target(string period)
            {
                if (period == DayHistory.BeforeKey) return period;
                var start = DayHistory.StartOf(period);
                var kind = period.Length == 10 ? 'd' : period[0];
                if (kind == 'd') { if (start >= dayCut) return period; kind = 'w'; start = DayHistory.Monday(start); }
                if (kind == 'w') { if (start >= weekCut) return DayHistory.WeekKey(start); kind = 'm'; start = new DateTime(start.Year, start.Month, 1); }
                if (kind == 'm' && start >= monthCut) return DayHistory.MonthKey(start);
                return DayHistory.BeforeKey;
            }
            if (Days.All(r => Target(r.Period) == r.Period)) return;
            var merged = new Dictionary<string, ArmourTally>(); var order = new List<string>();
            foreach (var r in Days)
            {
                var t = Target(r.Period);
                if (!merged.TryGetValue(t, out var into)) { merged[t] = into = new ArmourTally(); order.Add(t); }
                into.AddAll(r.Tally);
            }
            foreach (var kv in merged) { kv.Value.Clip(DayHistory.CapOf(kv.Key)); kv.Value.Weapons.Clip(WeaponCells.PerLong); kv.Value.Weapons.ClipLevels(WeaponCells.PerLongLevels); }
            Days.Clear();
            Days.AddRange(order.Select(k => (k, merged[k])).OrderBy(r => DayHistory.StartOf(r.k)));
        }

        /// <summary>The day rows from <paramref name="fromDay"/> to <paramref name="toDay"/> (local days, both included) added up.</summary>
        public ArmourTally Sum(DateTime fromDay, DateTime toDay)
        {
            var s = new ArmourTally();
            foreach (var r in Days) { var start = DayHistory.StartOf(r.Period); if (r.Period.Length == 10 && start >= fromDay.Date && start <= toDay.Date) s.AddAll(r.Tally); }
            return s;
        }

        /// <summary>0.8.6: the weapons of the last <paramref name="days"/> local days up to <paramref name="today"/>, per day key ("2026-10-11"), from the day rows plus the
        /// running session's unsaved part (<paramref name="pending"/>, today's); at most WeaponCells.PerDay named weapons a day. The shared copy's "dayWeapons".</summary>
        public Dictionary<string, WeaponCells> DayWeapons(DateTime today, int days, ArmourTally pending)
        {
            var from = today.Date.AddDays(1 - days); var d = new Dictionary<string, WeaponCells>();
            void Put(string key, WeaponCells w)
            {
                if (w == null || w.Count == 0) return;
                if (!d.TryGetValue(key, out var into)) d[key] = into = new WeaponCells();
                into.Merge(w);
            }
            foreach (var r in Days) { var start = DayHistory.StartOf(r.Period); if (r.Period.Length == 10 && start >= from && start <= today.Date) Put(r.Period, r.Tally.Weapons); }
            Put(DayHistory.DayKey(today), pending?.Weapons);
            foreach (var w in d.Values) { w.Clip(WeaponCells.PerDay); w.ClipLevels(WeaponCells.PerDayLevels); }
            return d;
        }

        public void WriteTo(Json j, string key = "armour")
        {
            j.Key(key).Open();
            if (FromUtc > DateTime.MinValue) j.Str("from", FromUtc.ToString("o", CultureInfo.InvariantCulture));
            if (SwingFromUtc > DateTime.MinValue) j.Str("swingFrom", SwingFromUtc.ToString("o", CultureInfo.InvariantCulture));
            Previous.WriteTo(j, "previous");
            j.Key("lastSession").Open().Str("id", LastSession); Last.WriteFields(j); j.Close();
            j.Key("days").OpenArr();
            foreach (var r in Days) { j.Open().Str("p", r.Period); r.Tally.WriteFields(j, true); j.Close(); }
            j.CloseArr().Close();
        }

        /// <summary>Reads what WriteTo wrote; null or missing: an empty book (From set later, on load). Rows out of order or unreadable are dropped.</summary>
        public static ArmourBook ReadFrom(Dictionary<string, object> o)
        {
            var b = new ArmourBook();
            if (o == null) return b;
            if (DateTime.TryParse(MiniJson.Str(o, "from"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var from)) b.FromUtc = from.ToUniversalTime();
            if (DateTime.TryParse(MiniJson.Str(o, "swingFrom"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var sf)) b.SwingFromUtc = sf.ToUniversalTime();
            b.Previous.ReadFrom(MiniJson.Obj(o, "previous"));
            var last = MiniJson.Obj(o, "lastSession");
            b.LastSession = MiniJson.Str(last, "id"); b.Last.ReadFrom(last);
            if (o.TryGetValue("days", out var ds) && ds is List<object> rows)
                foreach (var x in rows.OfType<Dictionary<string, object>>())
                {
                    var p = MiniJson.Str(x, "p");
                    if (p != DayHistory.BeforeKey && DayHistory.StartOf(p) == DateTime.MinValue) continue;
                    var t = new ArmourTally().ReadFrom(x);
                    if (b.Days.Count > 0 && DayHistory.StartOf(b.Days[b.Days.Count - 1].Period) > DayHistory.StartOf(p)) continue;   // out of order: never trusted
                    if (b.Days.Count > 0 && b.Days[b.Days.Count - 1].Period == p) { b.Days[b.Days.Count - 1].Tally.AddAll(t); continue; }
                    b.Days.Add((p, t));
                }
            return b;
        }
    }
}
