using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// The day history (HISTORY-06.md): one compact row per play day of what that day added, so the panel can answer "the last
    /// 7 days" between the session's event log (minutes, this session only) and the since-install totals (All). A row holds the
    /// same four shapes the totals use, as differences: SessionEvents, DamageTally, BiomeTally, and the game's own character
    /// counters that only grow (GameStats). LocalTotals.Record adds what each save added to the row of the local calendar day of
    /// the save; the game counters by their difference to a stored mark (GameMark).
    ///
    /// Bounded: day rows for the last DayRows days, older days fold into week rows (Monday start), weeks older than WeekRows weeks
    /// into month rows (the month of the week's Monday), months older than MonthRows into one "before" row; a day row keeps at most
    /// MaxRowKeys keys, a week MaxWeekKeys, a month or "before" MaxMonthKeys (the largest, then Clipped). Sums are kept by every fold. Pure C# (no Unity calls), unit-tested.
    /// JSON (LocalTotals top-level "history"): {"from":"2026-10-09","mark":{..},"rows":[{"p":"2026-10-09","ev":{..},"dmg":{..},"bio":{..},"game":{..}}]}.
    /// </summary>
    public class DayHistory
    {
        public const int DayRows = 35, WeekRows = 26, MonthRows = 24;
        /// <summary>The most keys a row keeps: a day the most (the windows read days), a folded week or month fewer (they only keep the
        /// shape of older play for growth lines): the largest stay, the row says it was clipped. The size budget rests on these.</summary>
        public const int MaxRowKeys = 250, MaxWeekKeys = 120, MaxMonthKeys = 80;
        public static int CapOf(string period) => string.IsNullOrEmpty(period) || period == BeforeKey || period[0] == 'm' ? MaxMonthKeys : period[0] == 'w' ? MaxWeekKeys : MaxRowKeys;
        public const string BeforeKey = "before";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The game's character counters a row keeps (PlayerStatType names): only counters that grow, never one that holds a
        /// "most at once" (BuildClusterDefense), whose difference means nothing. The windowed pages read these.</summary>
        public static readonly string[] GameStats = new[]
        {
            "DistanceSail", "DistanceSailHelm", "LeviathanSink", "LavaLeviathanSink", "DistanceWalk", "DistanceRun", "DistanceAir", "Jumps",
            "EnemyKills", "BossKills", "EnemyHits", "PlayerHits", "Deaths", "HitsTakenEnemies", "HitsTakenPlayers", "TrapArmed", "TurretAmmoAdded",
            "Tree", "MineHits", "CraftFood", "CraftGrill", "TimeInBase", "TimeOutOfBase", "PortalsUsed",
        }.Concat(LocalTotals.StatsTokens).Distinct().ToArray();

        /// <summary>The local calendar day the history began (the first save of a version that keeps it); DateTime.MinValue = no row yet.</summary>
        public DateTime From = DateTime.MinValue;
        /// <summary>The game counters as they stood at the last save (GameStats only): the next save books the difference.</summary>
        public readonly Dictionary<string, float> GameMark = new Dictionary<string, float>();
        /// <summary>Oldest first: "before", months ("m2026-10"), weeks ("w2026-10-05", the Monday), days ("2026-10-09").</summary>
        public readonly List<Row> Rows = new List<Row>();

        public class Row
        {
            public string Period = "";
            public SessionEvents Events = new SessionEvents();
            public DamageTally Damage = new DamageTally();
            public BiomeTally Biome = new BiomeTally();
            public readonly Dictionary<string, float> Game = new Dictionary<string, float>();
            /// <summary>The row reached MaxRowKeys: its smallest keys were left out (the pages say so).</summary>
            public bool Clipped;

            public bool IsDay => Period.Length == 10 && Period[4] == '-';
            public DateTime Start => StartOf(Period);

            public void AddAll(Row o)
            {
                if (o == null) return;
                Events.AddAll(o.Events); Damage.AddAll(o.Damage); Biome.AddAll(o.Biome);
                foreach (var kv in o.Game) SessionEvents.Add(Game, kv.Key, kv.Value);
                Clipped |= o.Clipped;
            }

            /// <summary>Every key of the row, with the map it lives in (for the key count and Clip).</summary>
            IEnumerable<(Dictionary<string, float> map, string key, float v)> Entries()
            {
                foreach (var m in Events.Named()) foreach (var kv in m.Value) yield return (m.Value, kv.Key, kv.Value);
                foreach (var kv in Damage.Dealt) yield return (Damage.Dealt, kv.Key, kv.Value);
                foreach (var kv in Damage.Taken) yield return (Damage.Taken, kv.Key, kv.Value);
                foreach (var kv in Biome.Damage) yield return (Biome.Damage, kv.Key, kv.Value);
                foreach (var kv in Biome.Deaths) yield return (Biome.Deaths, kv.Key, kv.Value);
                foreach (var kv in Game) yield return (Game, kv.Key, kv.Value);
            }
            public int Keys => Entries().Count();

            /// <summary>At most MaxRowKeys keys: the smallest go (a mod that invents thousands of item names cannot grow the file).</summary>
            public void Clip(int max = MaxRowKeys)
            {
                var all = Entries().ToList();
                if (all.Count <= max) return;
                foreach (var e in all.OrderBy(e => e.v).ThenBy(e => e.key, StringComparer.Ordinal).Take(all.Count - max).ToList()) e.map.Remove(e.key);
                Clipped = true;
            }

            public bool Empty => Events.Blocks == 0 && Events.Parries == 0 && Damage.HitsDealt == 0 && Damage.HitsTaken == 0 && !Entries().Any();
        }

        // ---------- periods ----------

        public static string DayKey(DateTime day) => day.ToString("yyyy-MM-dd", Inv);
        public static string WeekKey(DateTime day) => "w" + DayKey(Monday(day));
        public static string MonthKey(DateTime day) => "m" + day.ToString("yyyy-MM", Inv);
        public static DateTime Monday(DateTime day) { day = day.Date; return day.AddDays(-(((int)day.DayOfWeek + 6) % 7)); }

        /// <summary>The first day a period covers ("before": DateTime.MinValue; unreadable: MinValue too).</summary>
        public static DateTime StartOf(string period)
        {
            if (string.IsNullOrEmpty(period) || period == BeforeKey) return DateTime.MinValue;
            if (period[0] == 'w' && DateTime.TryParseExact(period.Substring(1), "yyyy-MM-dd", Inv, DateTimeStyles.None, out var w)) return w;
            if (period[0] == 'm' && DateTime.TryParseExact(period.Substring(1), "yyyy-MM", Inv, DateTimeStyles.None, out var m)) return m;
            return DateTime.TryParseExact(period, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var d) ? d : DateTime.MinValue;
        }

        // ---------- recording ----------

        /// <summary>
        /// The game counters' growth since the last save (GameStats): now minus the mark; <paramref name="move"/> moves the mark to now.
        /// The very first save (no mark yet) books nothing: the history starts there. A counter that went down (an older copy of the
        /// character played elsewhere) books nothing; the mark follows it. null (no profile at that moment): nothing, the mark stays.
        /// </summary>
        public Dictionary<string, float> GameGrowth(IDictionary<string, float> gameNow, bool move)
        {
            var d = new Dictionary<string, float>();
            if (gameNow == null) return d;
            var started = GameMark.Count > 0;
            foreach (var s in GameStats)
            {
                if (!gameNow.TryGetValue(s, out var v) || !Json.IsFinite(v)) continue;
                if (started) { GameMark.TryGetValue(s, out var m); if (v > m) d[s] = v - m; }
                if (move) GameMark[s] = v;
            }
            if (move && !started && GameMark.Count == 0) GameMark["_"] = 0;   // all counters still at zero: the history has started all the same
            return d;
        }

        /// <summary>Adds what one save added (differences, never totals) to the row of <paramref name="localNow"/>'s calendar day,
        /// then folds the old rows. A day before the newest row (the clock went back) books on the newest row.</summary>
        public void Add(DateTime localNow, SessionEvents events, DamageTally damage, BiomeTally biome, IDictionary<string, float> gameNow)
        {
            var day = localNow.Date;
            if (From == DateTime.MinValue) From = day;
            var game = GameGrowth(gameNow, true);
            var row = RowFor(day);
            row.Events.AddAll(events); row.Damage.AddAll(damage); row.Biome.AddAll(biome);
            foreach (var kv in game) SessionEvents.Add(row.Game, kv.Key, kv.Value);
            row.Clip();
            Fold(day);
        }

        Row RowFor(DateTime day)
        {
            var last = Rows.Count > 0 ? Rows[Rows.Count - 1] : null;
            var key = DayKey(day);
            if (last != null && last.Period == key) return last;
            if (last != null && last.Start > day) return last;   // the clock went back: never a row out of order
            var row = new Row { Period = key };
            Rows.Add(row);
            return row;
        }

        /// <summary>Folds the rows that are too old for their resolution (see the class summary). Every sum is kept.</summary>
        public void Fold(DateTime today)
        {
            today = today.Date;
            var dayCut = today.AddDays(-(DayRows - 1));
            var weekCut = Monday(dayCut).AddDays(-7 * WeekRows);
            var monthCut = new DateTime(weekCut.Year, weekCut.Month, 1).AddMonths(-MonthRows);
            string Target(Row r)
            {
                var start = r.Start;
                if (r.Period == BeforeKey) return BeforeKey;
                var kind = r.IsDay ? 'd' : r.Period[0];
                if (kind == 'd') { if (start >= dayCut) return r.Period; kind = 'w'; start = Monday(start); }
                if (kind == 'w') { if (start >= weekCut) return WeekKey(start); kind = 'm'; start = new DateTime(start.Year, start.Month, 1); }
                if (kind == 'm' && start >= monthCut) return MonthKey(start);
                return BeforeKey;
            }
            if (Rows.All(r => Target(r) == r.Period)) return;
            var merged = new Dictionary<string, Row>(); var order = new List<string>();
            foreach (var r in Rows)
            {
                var t = Target(r);
                if (!merged.TryGetValue(t, out var into)) { into = t == r.Period ? r : new Row { Period = t }; merged[t] = into; order.Add(t); if (into == r) continue; }
                into.AddAll(r);
            }
            foreach (var r in merged.Values) r.Clip(CapOf(r.Period));
            Rows.Clear();
            Rows.AddRange(order.Select(k => merged[k]).OrderBy(r => r.Start));
        }

        // ---------- reading ----------

        /// <summary>The day rows from <paramref name="fromDay"/> to <paramref name="toDay"/> (local calendar days, both included) added up.</summary>
        public Row Sum(DateTime fromDay, DateTime toDay)
        {
            var sum = new Row { Period = DayKey(toDay) };
            foreach (var r in Rows) if (r.IsDay && r.Start >= fromDay.Date && r.Start <= toDay.Date) sum.AddAll(r);
            return sum;
        }

        /// <summary>The first day the history can answer for: From, or <paramref name="today"/> while there is no row yet (the next
        /// save starts it, and the panel adds what the session counted meanwhile).</summary>
        public DateTime FirstDay(DateTime today) => From == DateTime.MinValue ? today.Date : From;

        // ---------- JSON ----------

        static void Dict(Json j, string key, IDictionary<string, float> d)
        {
            if (d == null || !d.Values.Any(v => Math.Round(v, 1) != 0)) return;
            j.Dict(key, d.Select(kv => new KeyValuePair<string, float>(kv.Key, (float)Math.Round(kv.Value, 1))));
        }

        public void WriteTo(Json j, string key = "history")
        {
            j.Key(key).Open();
            if (From > DateTime.MinValue) j.Str("from", DayKey(From));
            j.Dict("mark", GameMark.Select(kv => new KeyValuePair<string, float>(kv.Key, kv.Value == 0 ? float.Epsilon : kv.Value)));   // a zero mark is still a mark
            j.Key("rows").OpenArr();
            foreach (var r in Rows)
            {
                j.Open().Str("p", r.Period);
                if (r.Clipped) j.Num("clipped", 1);
                var ev = r.Events;
                if (ev.Blocks != 0 || ev.Parries != 0 || ev.Named().Any(m => m.Value.Count > 0))
                {
                    j.Key("ev").Open();
                    if (ev.Blocks != 0) j.Num("blocks", ev.Blocks);
                    if (ev.Parries != 0) j.Num("parries", ev.Parries);
                    foreach (var m in ev.Named()) Dict(j, m.Key, m.Value);
                    j.Close();
                }
                if (r.Damage.HitsDealt != 0 || r.Damage.HitsTaken != 0 || r.Damage.Dealt.Count > 0 || r.Damage.Taken.Count > 0)
                {
                    j.Key("dmg").Open();
                    if (r.Damage.HitsDealt != 0) j.Num("hitsDealt", r.Damage.HitsDealt);
                    if (r.Damage.HitsTaken != 0) j.Num("hitsTaken", r.Damage.HitsTaken);
                    Dict(j, "dealt", r.Damage.Dealt); Dict(j, "taken", r.Damage.Taken);
                    j.Close();
                }
                if (!r.Biome.Empty) { j.Key("bio").Open(); Dict(j, "damage", r.Biome.Damage); Dict(j, "deaths", r.Biome.Deaths); j.Close(); }
                Dict(j, "game", r.Game);
                j.Close();
            }
            j.CloseArr().Close();
        }

        /// <summary>Reads what WriteTo wrote (null or missing: an empty history, which starts at the next save). Rows out of order or
        /// unreadable periods are dropped; then the rows are folded for <paramref name="today"/> when given.</summary>
        public static DayHistory ReadFrom(Dictionary<string, object> o)
        {
            var h = new DayHistory();
            if (o == null) return h;
            if (DateTime.TryParseExact(MiniJson.Str(o, "from"), "yyyy-MM-dd", Inv, DateTimeStyles.None, out var from)) h.From = from;
            var mark = MiniJson.Obj(o, "mark");
            if (mark != null) foreach (var kv in mark) if (kv.Value is double v && Json.IsFinite(v)) h.GameMark[kv.Key] = v < 1e-30 ? 0f : (float)v;
            if (o.TryGetValue("rows", out var rs) && rs is List<object> rows)
                foreach (var x in rows.OfType<Dictionary<string, object>>())
                {
                    var p = MiniJson.Str(x, "p");
                    if (p != BeforeKey && StartOf(p) == DateTime.MinValue) continue;
                    var r = new Row { Period = p, Clipped = MiniJson.Num(x, "clipped") > 0 };
                    r.Events.ReadFrom(MiniJson.Obj(x, "ev")); r.Damage.ReadFrom(MiniJson.Obj(x, "dmg")); r.Biome.ReadFrom(MiniJson.Obj(x, "bio"));
                    MiniJson.Into(MiniJson.Obj(x, "game"), r.Game);
                    if (h.Rows.Count > 0 && h.Rows[h.Rows.Count - 1].Start > r.Start) continue;   // out of order: never trusted
                    if (h.Rows.Count > 0 && h.Rows[h.Rows.Count - 1].Period == r.Period) { h.Rows[h.Rows.Count - 1].AddAll(r); continue; }
                    h.Rows.Add(r);
                }
            if (h.From == DateTime.MinValue && h.Rows.Count > 0) h.From = h.Rows.Where(r => r.IsDay).Select(r => r.Start).DefaultIfEmpty(DateTime.MinValue).First();
            return h;
        }

        /// <summary>Your damage dealt per day (the eight battle types, before the foe's armour), the last <paramref name="days"/> days:
        /// what the snapshot shares ("dealtByDay", under 1 KB), so a fellow's Together can show your day windows.</summary>
        public Dictionary<string, float> DealtByDay(DateTime today, int days = 30, Row pending = null)
        {
            var d = new Dictionary<string, float>();
            var from = today.Date.AddDays(1 - days);
            foreach (var r in Rows.Where(r => r.IsDay && r.Start >= from).Concat(pending != null ? new[] { pending } : new Row[0]))
            {
                var v = DealtOf(r.Damage);
                if (v > 0) { var k = r.IsDay ? r.Period : DayKey(today); d.TryGetValue(k, out var o); d[k] = (float)Math.Round(o + v); }
            }
            return d;
        }

        static readonly string[] BattleTypes = { "blunt", "slash", "pierce", "fire", "frost", "lightning", "poison", "spirit" };
        /// <summary>Damage dealt in a tally, the eight battle types only (tool damage is never battle).</summary>
        public static double DealtOf(DamageTally t)
        {
            double s = 0;
            foreach (var kv in t?.Dealt ?? new Dictionary<string, float>())
            {
                var type = kv.Key.Substring(kv.Key.LastIndexOf('|') + 1);
                if (kv.Value > 0 && Array.IndexOf(BattleTypes, type) >= 0) s += kv.Value;
            }
            return s;
        }
    }
}
