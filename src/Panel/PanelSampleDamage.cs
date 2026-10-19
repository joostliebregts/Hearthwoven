using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9.5 draft, Damage history (Chapters/DamageHistory.cs): the sample world with thirty days of day history (fictional). Rowan's own days before
    /// the sample's week are damage only, an evening of melee most days, the bow on two days in three and more of it as the month goes on, a staff
    /// picked up in the last ten days, a few days without play; fighting time (0.8.6) recorded from twelve days back, so the DPS charts begin
    /// part way. Edda's copy shares thirty days of damage by kind and type (dayDealt) and fourteen of her weapons with their seconds (dayWeapons).
    /// </summary>
    public static class DamageHistorySample
    {
        public const int MonthDays = 30, SwingDays = 12;

        /// <summary>"month": the thirty days above; anything else: the sample world as it is (its week of history, fighting time from two days back).</summary>
        public static PanelInput World(DateTime now, string variant)
        {
            var p = PanelSample.Full(now);
            if (variant != "month") return p;
            var today = PanelModel.LocalToday(p);
            Month(p, today);
            Swing(p, today);
            Edda(p, today);
            return p;
        }

        static bool Rest(int k) => k % 6 == 4 || (k >= 13 && k <= 15);

        // the damage of day k (0 = the oldest of the month) per kind
        static (double melee, double bow, double magic) DayOf(int k) =>
            Rest(k) ? (0, 0, 0) : (1800 + (k * 397) % 2200, k % 3 == 1 ? 0 : (400 + (k * 211) % 1300) * (1 + k / 30.0), k >= 20 && k % 2 == 0 ? 300 + k * 13 : 0);

        static void Month(PanelInput p, DateTime today)
        {
            var h = p.History; if (h == null) return;
            var first = today.AddDays(1 - MonthDays);
            var start = h.FirstDay(today);
            var added = new List<DayHistory.Row>();
            for (var d = first; d < start; d = d.AddDays(1))
            {
                var k = (int)(d - first).TotalDays;
                var (melee, bow, magic) = DayOf(k);
                if (melee + bow + magic <= 0) continue;   // a day without play has no row
                var row = new DayHistory.Row { Period = DayHistory.DayKey(d) };
                void Put(string key, double v) { if (v >= 1) row.Damage.Dealt[key] = (float)Math.Round(v); }
                Put("Greydwarf|Swords|slash", melee * 0.6); Put("Skeleton|Clubs|blunt", melee * 0.4);
                Put("Deer|Bows|pierce", bow * 0.85); Put("Greydwarf|Bows|fire", bow * 0.15);
                Put("Draugr|ElementalMagic|fire", magic);
                row.Damage.HitsDealt = (int)((melee + bow + magic) / 30);
                added.Add(row);
            }
            var at = h.Rows.FindIndex(r => r.IsDay); if (at < 0) at = h.Rows.Count;
            h.Rows.InsertRange(at, added);
            h.From = first;
        }

        /// <summary>A row's damage per weapon kind (as By weapon reads it).</summary>
        static Dictionary<string, double> Kinds(DayHistory.Row row)
        {
            var d = new Dictionary<string, double>();
            foreach (var kv in row.Damage.Dealt)
            {
                var parts = kv.Key.Split('|'); if (parts.Length != 3 || kv.Value <= 0) continue;
                var k = DayHistory.WeaponKind(parts[1]); d.TryGetValue(k, out var o); d[k] = o + kv.Value;
            }
            return d;
        }

        // fighting time from SwingDays back: each played day before the ledger's own rows gets its kinds' seconds at a pace that moves a little day to day
        static void Swing(PanelInput p, DateTime today)
        {
            var book = p.DealtArmourBook; if (book == null || p.History == null) return;
            var from = today.AddDays(1 - SwingDays);
            var bookFirst = book.Days.Where(r => r.Period.Length == 10).Select(r => DayHistory.StartOf(r.Period)).DefaultIfEmpty(today.AddDays(1)).Min();
            var add = new List<(string, ArmourTally)>();
            foreach (var row in p.History.Rows.Where(r => r.IsDay && r.Start >= from && r.Start < bookFirst).OrderBy(r => r.Start))
            {
                var k = (int)(row.Start - from).TotalDays;
                var t = new ArmourTally();
                foreach (var kv in Kinds(row))
                {
                    var pace = kv.Key == "bow" ? 18 + (k * 5) % 9 : kv.Key == "magic" ? 24 + k % 6 : 32 + (k * 7) % 14;
                    var swung = kv.Value * 0.9;
                    t.AddSwing(SwingClock.KindOf(kv.Key), (float)(swung / pace), (float)swung);
                }
                add.Add((row.Period, t));
            }
            book.Days.InsertRange(0, add);
            var utc = DateTime.SpecifyKind(from.AddHours(19), DateTimeKind.Utc);
            book.SwingFromUtc = utc; p.SwingFromUtc = utc;
        }

        // Edda's copy: thirty days of damage by kind and type, mostly the bow; her weapons with their seconds for the last fourteen
        static void Edda(PanelInput p, DateTime today)
        {
            var e = p.Fellows?.FirstOrDefault(f => f.PlayerName == "Edda"); if (e == null) return;
            e.DayDealt = new Dictionary<string, Dictionary<string, float>>();
            e.DayWeapons = new Dictionary<string, WeaponCells>();
            var first = today.AddDays(1 - MonthDays);
            for (int k = 0; k < MonthDays; k++)
            {
                if (k % 5 == 2) continue;
                var day = DayHistory.DayKey(first.AddDays(k));
                float bow = 900 + (k * 173) % 1400, melee = k % 4 == 0 ? 0 : 300 + (k * 89) % 600;
                e.DayDealt[day] = new Dictionary<string, float> { ["bow|pierce"] = (float)Math.Round(bow * 0.9), ["bow|frost"] = (float)Math.Round(bow * 0.1) };
                if (melee > 0) e.DayDealt[day]["melee|pierce"] = melee;
                if (k < MonthDays - 14) continue;
                var cells = new WeaponCells();
                cells.Map[WeaponKey.Make("BowHuntsman", 2, "Bows")] = new WeaponCell { Sec = bow * 0.9f / (22 + k % 7), Swung = bow * 0.9f, All = bow };
                if (melee > 0) cells.Map[WeaponKey.Make("SpearBronze", 2, "Spears")] = new WeaponCell { Sec = melee * 0.9f / 30, Swung = melee * 0.9f, All = melee };
                e.DayWeapons[day] = cells;
            }
        }
    }
}
