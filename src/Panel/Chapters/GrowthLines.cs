using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8 growth lines (work/hearthwoven-0.8 OVERNIGHT-BRIEF item 4): a small column per day, the last GrowthDays days, beside the hero of
    /// each Deeds page whose number the day history keeps (trees felled, stone and ore, dishes, pieces, groundwork, gear, planted, fish), and
    /// beside damage dealt on Battle > Overview. Read from the DayHistory rows the day windows read (one day's copy of the input per column,
    /// the page's own count of it), so a column is what Today showed that day. Honest when the history is short: only the days it holds are
    /// drawn (from DayHistory.KeptFrom, the counter's first kept day), never a 0 before them; a kept day with nothing is a flat mark. A fellow's
    /// copy shares no days, so their Deeds pages draw the line's place greyed ("no days shared"); damage dealt is the exception (their
    /// snapshot's dealtByDay, the last 30 days). Past days are worked out once per day row (Memo); only today is counted again on a build.
    /// </summary>
    public static partial class PanelModel
    {
        public const int GrowthDays = 14;
        public const string SparkKind = "spark", SparkDay = "day", NoDaysShared = "no days shared";

        /// <summary>A block's growth line (Kind "spark"): Title its caption, Tone OffTone when greyed (no Items then), Items one "day" per day, oldest first
        /// (Id the local day "yyyy-MM-dd", Value the count, Fraction its height against the line's largest day, Tone "zero" for a kept day with nothing).</summary>
        public static Block SparkOf(Block b) => b?.Items?.FirstOrDefault(i => i.Kind == SparkKind);

        /// <summary>What a Deeds page's hero counts, read from one day's copy of the input the way its day window reads it, and the counter whose first
        /// kept day starts the line (DayHistory.Began: the per-token families and stats a later version added; null = kept from the history's start).</summary>
        static readonly Dictionary<string, (string counter, Func<PanelInput, double> of)> DayMetrics = new Dictionary<string, (string, Func<PanelInput, double>)>
        {
            ["woodcutting"] = (null, d => TreesFelledByKind(d).Values.Sum()),
            ["mining"] = (null, d => PickedUpMeasured(d, "mining").Values.Sum()),
            ["cooking"] = (null, d => DishesCooked(d)),
            ["building"] = (DayHistory.PlacedPrefix, d => Placed(d, "built").Values.Sum()),
            ["groundwork"] = (DayHistory.PlacedPrefix, d => Placed(d, "ground").Values.Sum()),
            ["crafting"] = ("CraftWeapon", d => GearKinds.Sum(k => C(d, k.stat))),
            ["farming"] = (null, d => PlantedTotals(d).Values.Sum()),
            ["fishing"] = ("FishCaught", d => C(d, "FishCaught")),
        };

        /// <summary>The Deeds pages that carry a growth line.</summary>
        public static IEnumerable<string> GrowthPages => DayMetrics.Keys;

        // past days' values, worked out once per day row (a row before today never changes; a new row object, after a load or a fold, is counted anew)
        static readonly Dictionary<(string page, string day), (DayHistory.Row row, double v)> growthMemo = new Dictionary<(string, string), (DayHistory.Row, double)>();

        /// <summary>One day's copy of the input (as InWindow and DeedsWindow build a day window's, for one row): the row's events and game counters,
        /// the pieces placed that day, the items made; nothing else is read by the day metrics.</summary>
        static PanelInput DayCopy(PanelInput input, DayHistory.Row row, DateTime day, bool pieces)
        {
            var c = input.ShallowCopy();
            c.Events = row.Events; c.Character = row.Game; c.ItemsCrafted = row.Events.Made; c.ItemsPickedUp = row.Events.PickedUp;
            c.PiecesPlaced = pieces ? DayHistory.Family(row.Game, DayHistory.PlacedPrefix) : null;
            c.Baseline = null; c.ExactAtBaseline = null; c.BaselineAt = null; c.Book = null;
            c.Window = new DayView { Window = TimeWindow.Today, From = day, To = day };
            return c;
        }

        /// <summary>The days a growth line draws: from the counter's first kept day (or GrowthDays back, the later) to today; empty when fewer than two.</summary>
        static List<DateTime> GrowthSpan(PanelInput input, string counter)
        {
            var today = LocalToday(input);
            var from = input.History.KeptFrom(counter, today);
            var back = today.AddDays(1 - GrowthDays);
            if (from < back) from = back;
            var days = new List<DateTime>();
            for (var d = from; d <= today; d = d.AddDays(1)) days.Add(d);
            return days.Count >= 2 ? days : new List<DateTime>();
        }

        /// <summary>A line of daily values (oldest first) as its block: the caption says the span ("last 14 days", or "since 3 Oct" when shorter).</summary>
        static Block SparkBlock(PanelInput input, List<DateTime> days, IList<double> values)
        {
            var max = values.DefaultIfEmpty(0).Max();
            var today = LocalToday(input);
            var spark = new Block { Kind = SparkKind, Title = days.Count >= GrowthDays ? "last " + GrowthDays + " days" : "since " + ShortDate(days[0], today), Items = new List<Block>() };
            for (int k = 0; k < days.Count; k++)
            {
                var v = values[k];
                spark.Items.Add(new Block { Kind = SparkDay, Id = DayHistory.DayKey(days[k]), Value = v > 0 ? NAtLeast(v) : "0", Fraction = max > 0 ? (float)(v / max) : 0, Tone = v > 0 ? null : "zero" });
            }
            return spark;
        }

        /// <summary>A Deeds page's growth line on your own book (null without a day history or with under two days).</summary>
        static Block DeedsGrowth(PanelInput input, string page, (string counter, Func<PanelInput, double> of) m)
        {
            if (input.History == null) return null;
            var days = GrowthSpan(input, m.counter);
            if (days.Count == 0) return null;
            var today = LocalToday(input);
            var byDay = input.History.Rows.Where(r => r.IsDay).ToDictionary(r => r.Period, r => r);
            var pieces = m.counter == DayHistory.PlacedPrefix;
            var values = new List<double>(days.Count);
            foreach (var d in days)
            {
                var key = DayHistory.DayKey(d);
                byDay.TryGetValue(key, out var row);
                if (d == today)
                {
                    // today: the saved row and what the session counted since the last save (as the Today window); merged only when both hold something
                    var pending = input.Pending != null && !input.Pending.Empty ? input.Pending : null;
                    DayHistory.Row now;
                    if (pending == null) now = row ?? new DayHistory.Row { Period = key };
                    else if (row == null) now = pending;
                    else { now = new DayHistory.Row { Period = key }; now.AddAll(row); now.AddAll(pending); }
                    values.Add(GrowthSafe(() => m.of(DayCopy(input, now, d, pieces))));
                    continue;
                }
                if (row == null) { values.Add(0); continue; }   // a kept day without a row: nothing done that day
                var memoKey = (page, key);   // a pair, not a joined string: nothing made per day on a rebuild
                if (growthMemo.TryGetValue(memoKey, out var hit) && ReferenceEquals(hit.row, row)) { values.Add(hit.v); continue; }
                var v = GrowthSafe(() => m.of(DayCopy(input, row, d, pieces)));
                if (growthMemo.Count > 1024) growthMemo.Clear();
                growthMemo[memoKey] = (row, v); values.Add(v);
            }
            return SparkBlock(input, days, values);
        }

        static double GrowthSafe(Func<double> f) { try { var v = f(); return double.IsNaN(v) || double.IsInfinity(v) ? 0 : Math.Max(0, v); } catch { return 0; } }

        /// <summary>Damage dealt per day (Battle > Overview): your day history (the eight battle types, before the foe's armour, as the day windows),
        /// or a fellow's shared dealtByDay from its first shared day.</summary>
        static Block DealtGrowth(PanelInput input)
        {
            var today = LocalToday(input);
            if (input.IsSelf)
            {
                if (input.History == null) return null;
                var days = GrowthSpan(input, null);
                if (days.Count == 0) return null;
                var byDay = input.History.Rows.Where(r => r.IsDay).ToDictionary(r => r.Period, r => r);
                var values = days.Select(d =>
                {
                    byDay.TryGetValue(DayHistory.DayKey(d), out var row);
                    var v = row != null ? DayHistory.DealtOf(row.Damage) : 0;
                    if (d == today && input.Pending != null) v += DayHistory.DealtOf(input.Pending.Damage);
                    return v;
                }).ToList();
                return SparkBlock(input, days, values);
            }
            if (input.DealtByDay == null || input.DealtByDay.Count == 0) return null;
            var shared = new Dictionary<DateTime, double>();
            foreach (var kv in input.DealtByDay)
                if (DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", Inv, System.Globalization.DateTimeStyles.None, out var d) && kv.Value > 0) shared[d.Date] = kv.Value;
            if (shared.Count == 0) return null;
            var first = shared.Keys.Min(); var back = today.AddDays(1 - GrowthDays);
            var span = new List<DateTime>();
            for (var d = first < back ? back : first; d <= today; d = d.AddDays(1)) span.Add(d);
            if (span.Count < 2) return null;
            return SparkBlock(input, span, span.Select(d => shared.TryGetValue(d, out var v) ? v : 0).ToList());
        }

        /// <summary>Build's last step: the page's growth line beside its hero (Deeds) or beside damage dealt on the biome strip (Battle > Overview, all biomes);
        /// on a fellow's Deeds page its place greyed, "no days shared".</summary>
        static void GrowthLine(PanelInput input, PanelView view)
        {
            if (view == null || view.ShowAbout || input == null) return;
            if (view.Active == Chapter.Deeds && view.Page != null && DayMetrics.TryGetValue(view.Page, out var m))
            {
                var hero = Content(view).FirstOrDefault(b => b.Kind == "hero");
                if (hero == null || SparkOf(hero) != null) return;
                var spark = input.IsSelf ? DeedsGrowth(input, view.Page, m) : new Block { Kind = SparkKind, Tone = OffTone, Title = NoDaysShared };
                if (spark != null) (hero.Items ?? (hero.Items = new List<Block>())).Add(spark);
                return;
            }
            if (view.Active == Chapter.Battle && view.Page == "overview")
            {
                // the line counts every biome: with biomes chosen (the legend says "in the Swamp") it would not be the legend's number, so none
                var strip = Content(view).FirstOrDefault(b => b.Kind == "biomes" && !string.IsNullOrEmpty(b.Value) && string.IsNullOrEmpty(b.Title));
                if (strip == null || SparkOf(strip) != null) return;
                var spark = DealtGrowth(input);
                if (spark != null) (strip.Items ?? (strip.Items = new List<Block>())).Add(spark);
            }
        }
    }
}
