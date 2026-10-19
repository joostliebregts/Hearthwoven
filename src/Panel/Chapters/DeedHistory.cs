using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 History (work/hearthwoven-0.9/DESIGN.md section 2 "History", board History; Joost 18 Oct: "Seems good.", "Let's do A and B"): every Deeds page
    /// with a growth line (GrowthLines.cs) gets a History page, opened from the hero's growth line (its click; no heading chip). It is that growth line's
    /// own model drawn large, with the same rules: only the days the day history holds (from the counter's first kept day, DayHistory.KeptFrom; the
    /// day rows of the last DayHistory.DayRows days), never a 0 before them, a kept day with nothing a flat mark. Chips for the hero's numbers (the
    /// page's growth number first, its second number where the day history keeps it: Crafting's upgrades, Woodcutting's axe hits). Days now; Weeks and
    /// Months greyed with the date they open (from DayRows days of history on, older days are kept as weeks, from DayRows + 7 x WeekRows as months)
    /// and drawn the same way once they hold rows. Option A: a small gold tick over a day with a first (record 3, the page's own kind of thing) or a
    /// feat of this page earned that day. Option B: each column split by kind in the page's By kind colours, the kinds' bar of the shown days under
    /// it as the key (the book's one bar form, Composition). Hover says the day; a click opens that day's page (DayPagePrefix: the page as its day
    /// window, one day). Under it a Firsts island in the row form: what you first made or placed, newest first, recorded from 0.9, and one line on
    /// what came before. Your own book only: a fellow's copy has no day history here.
    /// Block shape of the chart: Kind SparkKind, Tone SparkLargeTone, Title its caption, Value the total, PartLink the click's stem (a day's target is
    /// PartLink + its Id), Items one "day" per column: Id the day key ("2026-10-13", or a week's or month's period), Value, Fraction (height), Tone
    /// "zero" for an empty day, Title the hover words, Count the ticks and Text their words, Items its parts by kind (Id, Colour, Fraction of the day).
    /// </summary>
    public static partial class PanelModel
    {
        public const string HistoryPagePrefix = "history:", DayPagePrefix = "day:", SparkLargeTone = "large";
        public const string HistoryDays = "days", HistoryWeeks = "weeks", HistoryMonths = "months";
        public const string FirstsTitle = "Firsts", HistoryWord = "History";
        /// <summary>The most first rows the Firsts island lists (newest first); the rest say how many.</summary>
        public const int FirstsTop = 12;

        public static string HistoryPageOf(string deedPage) => HistoryPagePrefix + deedPage;
        public static bool IsHistoryPage(string page) => page != null && page.StartsWith(HistoryPagePrefix, StringComparison.Ordinal);
        /// <summary>The deed page a History page belongs to ("history:crafting" -> "crafting"); null for any other page.</summary>
        public static string HistoryParent(string page) => IsHistoryPage(page) ? page.Substring(HistoryPagePrefix.Length) : null;
        /// <summary>A day's own page: the deed page in that one day ("day:crafting:2026-10-13").</summary>
        public static string DayPageOf(string deedPage, DateTime day) => DayPagePrefix + deedPage + ":" + DayHistory.DayKey(day);
        public static bool TryDayPage(string page, out string parent, out DateTime day)
        {
            parent = null; day = DateTime.MinValue;
            if (page == null || !page.StartsWith(DayPagePrefix, StringComparison.Ordinal)) return false;
            var rest = page.Substring(DayPagePrefix.Length); var at = rest.LastIndexOf(':');
            if (at <= 0 || !DateTime.TryParseExact(rest.Substring(at + 1), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return false;
            parent = rest.Substring(0, at);
            return true;
        }
        /// <summary>The deed page a History page or a day's page belongs to (the left list lights it, Back leads past it); null for any other page.</summary>
        public static string DeedSubParent(string page) => HistoryParent(page) ?? (TryDayPage(page, out var p, out _) ? p : null);

        // ---------- what each page's history counts ----------

        /// <summary>One number a History page can show: its chip, its words per day, the counter whose first kept day starts it, its count of one
        /// day's copy of the input (as the growth line and the day windows read it), and its parts by kind (option B; null = one colour).</summary>
        sealed class HistoryMetric
        {
            public string Id, Chip, One, Many, Counter;
            public Func<PanelInput, double> Of;
            public Func<PanelInput, IDictionary<string, double>> Parts;
            public Func<PanelInput, string, string> Label;
            public Func<PanelInput, Func<string, (string colour, string pattern)>> Look;
            public Func<PanelInput, Func<string, string>> Tint;
            public Func<string, string> Icon;
        }

        static IDictionary<string, double> GearParts(PanelInput d) => GearKinds.Where(k => C(d, k.stat) > 0).ToDictionary(k => k.id, k => C(d, k.stat));
        static IDictionary<string, double> PieceTabs(PanelInput d)
        {
            var tabs = new Dictionary<string, double>();
            foreach (var kv in Placed(d, "built")) Bump(tabs, Ask(d.PieceTab, kv.Key) ?? MaterialOther, kv.Value);
            return tabs;
        }
        static IDictionary<string, double> FishParts(PanelInput d) => (d.Harvested ?? new Dictionary<string, float>()).Where(kv => kv.Key.StartsWith("$") && kv.Value > 0).ToDictionary(kv => kv.Key, kv => (double)kv.Value);

        static readonly Dictionary<string, HistoryMetric[]> HistoryMetrics = new Dictionary<string, HistoryMetric[]>
        {
            ["woodcutting"] = new[] {
                new HistoryMetric { Id = "felled", Chip = "Trees felled", One = "tree felled", Many = "trees felled", Of = d => TreesFelledByKind(d).Values.Sum(), Parts = TreesFelledByKind, Label = (i, k) => k, Icon = TreeIcon },
                new HistoryMetric { Id = "hits", Chip = "Axe hits", One = "axe hit", Many = "axe hits", Of = d => M(d, e => e.ChopHits) } },
            ["mining"] = new[] {
                new HistoryMetric { Id = "ore", Chip = "Stone and ore", One = "stone, ore brought in", Many = "stone, ore brought in", Of = d => PickedUpMeasured(d, "mining").Values.Sum(), Parts = d => PickedUpMeasured(d, "mining"), Tint = ItemTint },
                new HistoryMetric { Id = "hits", Chip = "Pickaxe hits", One = "pickaxe hit", Many = "pickaxe hits", Of = d => M(d, e => e.PickaxeHits) } },
            ["cooking"] = new[] {
                new HistoryMetric { Id = "dishes", Chip = "Dishes cooked", One = "dish cooked", Many = "dishes cooked", Of = DishesCooked, Parts = d => Crafted(d, "food"), Tint = ItemTint },
                new HistoryMetric { Id = "feasts", Chip = "Feasts set out", One = "feast set out", Many = "feasts set out", Counter = DayHistory.PlacedPrefix, Of = d => Placed(d, "feast").Values.Sum(), Icon = k => "piece:" + k } },
            ["building"] = new[] {
                new HistoryMetric { Id = "pieces", Chip = "Pieces built", One = "piece built", Many = "pieces built", Counter = DayHistory.PlacedPrefix, Of = d => Placed(d, "built").Values.Sum(), Parts = PieceTabs, Label = (i, k) => k, Icon = k => null } },
            ["groundwork"] = new[] {
                new HistoryMetric { Id = "strokes", Chip = "Groundwork strokes", One = "groundwork stroke", Many = "groundwork strokes", Counter = DayHistory.PlacedPrefix, Of = d => Placed(d, "ground").Values.Sum(), Parts = d => Placed(d, "ground"), Look = i => GroundOf, Icon = k => "piece:" + k } },
            ["crafting"] = new[] {
                new HistoryMetric { Id = "gear", Chip = "Gear crafted", One = "gear crafted", Many = "gear crafted", Counter = "CraftWeapon", Of = d => GearKinds.Sum(k => C(d, k.stat)), Parts = GearParts, Label = (i, k) => GearKinds.First(g => g.id == k).title, Icon = k => null },
                new HistoryMetric { Id = "upgrades", Chip = "Upgrades made", One = "upgrade made", Many = "upgrades made", Counter = "CraftWeapon", Of = d => C(d, "Upgrades") } },
            ["farming"] = new[] {
                new HistoryMetric { Id = "planted", Chip = "Planted", One = "planted", Many = "planted", Of = d => PlantedTotals(d).Values.Sum(), Parts = d => PlantedTotals(d), Icon = k => "piece:" + k },
                new HistoryMetric { Id = "picked", Chip = "Picked", One = "picked", Many = "picked", Of = d => C(d, "HarvestCrop") } },
            ["fishing"] = new[] {
                new HistoryMetric { Id = "caught", Chip = "Fish caught", One = "fish caught", Many = "fish caught", Counter = "FishCaught", Of = d => C(d, "FishCaught"), Parts = FishParts, Tint = ItemTint },
                new HistoryMetric { Id = "hooked", Chip = "Hooked", One = "hooked", Many = "hooked", Counter = "FishCaught", Of = d => C(d, "FishHooked") } },
        };

        /// <summary>The Deeds pages with a History page (the pages with a growth line).</summary>
        public static IEnumerable<string> HistoryPages => HistoryMetrics.Keys;

        /// <summary>One period's copy of the input (as GrowthLines.DayCopy, plus the picks per token for Fishing's kinds): the row's events and game
        /// counters, the pieces placed and things picked that period, the items made.</summary>
        static PanelInput PeriodCopy(PanelInput input, DayHistory.Row row, DateTime from, DateTime to)
        {
            var c = input.ShallowCopy();
            c.Events = row.Events; c.Character = row.Game.Where(kv => !kv.Key.Contains("|")).ToDictionary(kv => kv.Key, kv => kv.Value);
            c.ItemsCrafted = row.Events.Made; c.ItemsPickedUp = row.Events.PickedUp;
            c.PiecesPlaced = DayHistory.Family(row.Game, DayHistory.PlacedPrefix); c.Harvested = DayHistory.Family(row.Game, DayHistory.PickedPrefix);
            c.Baseline = null; c.ExactAtBaseline = null; c.BaselineAt = null; c.Book = null;
            c.Window = new DayView { Window = TimeWindow.Today, From = from, To = to, Clipped = row.Clipped, Whole = input };
            return c;
        }

        /// <summary>A period of the history: its key, its first and last day, its row (the saved row(s), today with the running session's part).</summary>
        sealed class HistoryPeriod { public string Key; public DateTime From, To; public DayHistory.Row Row; }

        /// <summary>The periods a History view draws, oldest first. Days: from the counter's first kept day (or DayRows back, the later) to today, a
        /// kept day without a row empty. Weeks and months: the rows of that size and every later row added into its week (Monday) or month.</summary>
        static List<HistoryPeriod> HistoryPeriods(PanelInput input, string counter, string size)
        {
            var today = LocalToday(input); var h = input.History; var list = new List<HistoryPeriod>();
            if (size == HistoryDays)
            {
                var from = h.KeptFrom(counter, today); var back = today.AddDays(1 - DayHistory.DayRows);
                if (from < back) from = back;
                var byDay = h.Rows.Where(r => r.IsDay).GroupBy(r => r.Period).ToDictionary(g => g.Key, g => g.First());
                for (var d = from; d <= today; d = d.AddDays(1))
                {
                    byDay.TryGetValue(DayHistory.DayKey(d), out var row);
                    var now = new DayHistory.Row { Period = DayHistory.DayKey(d) };
                    if (row != null) now.AddAll(row);
                    if (d == today && input.Pending != null && !input.Pending.Empty) now.AddAll(input.Pending);
                    list.Add(new HistoryPeriod { Key = now.Period, From = d, To = d, Row = now });
                }
                return list;
            }
            var weeks = size == HistoryWeeks;
            DateTime StartOfBucket(DateTime day) => weeks ? day.AddDays(-(((int)day.DayOfWeek + 6) % 7)) : new DateTime(day.Year, day.Month, 1);
            var buckets = new SortedDictionary<DateTime, DayHistory.Row>();
            foreach (var r in h.Rows)
            {
                if (r.Period == DayHistory.BeforeKey) continue;
                var isMonth = r.Period.Length > 0 && r.Period[0] == 'm'; var isWeek = r.Period.Length > 0 && r.Period[0] == 'w';
                if (weeks && isMonth) continue;   // a month older than the weeks kept: not a week
                var at = StartOfBucket(r.Start.Date);
                if (!buckets.TryGetValue(at, out var b)) buckets[at] = b = new DayHistory.Row { Period = (weeks ? "w" : "m") + DayHistory.DayKey(at) };
                b.AddAll(r);
            }
            if (input.Pending != null && !input.Pending.Empty)
            {
                var at = StartOfBucket(today);
                if (!buckets.TryGetValue(at, out var b)) buckets[at] = b = new DayHistory.Row { Period = (weeks ? "w" : "m") + DayHistory.DayKey(at) };
                b.AddAll(input.Pending);
            }
            foreach (var kv in buckets) list.Add(new HistoryPeriod { Key = kv.Value.Period, From = kv.Key, To = weeks ? kv.Key.AddDays(6) : kv.Key.AddMonths(1).AddDays(-1), Row = kv.Value });
            return list;
        }

        /// <summary>When weeks (or months) open: the history holds a row of that size (true), else the local day the first one will (DayRows days of
        /// history on for weeks, DayRows + 7 x WeekRows for months).</summary>
        static (bool open, DateTime opens) HistorySizeOpen(PanelInput input, string size)
        {
            var h = input.History; var first = h.FirstDay(LocalToday(input));
            var has = size == HistoryWeeks ? h.Rows.Any(r => r.Period.StartsWith("w", StringComparison.Ordinal) || r.Period.StartsWith("m", StringComparison.Ordinal) || r.Period == DayHistory.BeforeKey)
                                           : h.Rows.Any(r => r.Period.StartsWith("m", StringComparison.Ordinal) || r.Period == DayHistory.BeforeKey);
            return (has, first.AddDays(size == HistoryWeeks ? DayHistory.DayRows : DayHistory.DayRows + 7 * DayHistory.WeekRows));
        }

        /// <summary>"Mon 13 Oct" (a day), "Week of 5 Oct", "October" (a month): what the hover says first.</summary>
        public static string PeriodWords(DateTime from, string size, DateTime today) =>
            size == HistoryWeeks ? "Week of " + ShortDate(from, today) : size == HistoryMonths ? from.ToString(from.Year == today.Year ? "MMMM" : "MMMM yyyy", Inv) : from.ToString("ddd ", Inv) + ShortDate(from, today);

        static readonly string[] TierMarks = { "", " I", " II", " III" };

        /// <summary>Option A: what happened on these days that the page cares about: a first of the page's own kind (record 3) and a feat of this page earned
        /// then, in words ("First: Iron Sword", "Feat: Arms for the Hall"), oldest first.</summary>
        static List<string> HistoryMarks(PanelInput input, string page, DateTime from, DateTime to)
        {
            var marks = new List<string>();
            foreach (var kv in (input.Firsts ?? new Dictionary<string, DateTime>()).Where(kv => kv.Value.Date >= from && kv.Value.Date <= to && FirstOfPage(input, page, kv.Key)).OrderBy(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
                marks.Add("First recorded: " + Who(input, kv.Key));
            if (input.Feats != null)
                foreach (var def in FeatDefs.Where(d => d.Chapter == Chapter.Deeds && d.Page == page && !d.Group))
                    if (input.Feats.Earned.TryGetValue(def.Id, out var tiers))
                        for (int t = 0; t < tiers.Count; t++)
                        {
                            var m = tiers[t];
                            if (m.Utc <= DateTime.MinValue || m.Before || m.Noticed) continue;   // a tier with no day it happened on (before Hearthwoven counted, or noticed later) gets no tick
                            var day = Local(input, m.Utc).Date;
                            if (day >= from && day <= to) marks.Add("Feat: " + def.Name + (def.Tiers.Length > 1 && t + 1 < TierMarks.Length ? TierMarks[t + 1] : ""));
                        }
            return marks;
        }

        /// <summary>A first that belongs to this page: gear on Crafting, a dish on Cooking, a piece on Building, a planting on Farming (record 3 holds
        /// items made and pieces placed; the gathering pages have none).</summary>
        static bool FirstOfPage(PanelInput input, string page, string token)
        {
            // record 3 keeps items and pieces in one book: a piece is what the game data knows as one, or by its name token ("$piece_"), never an
            // item the piece rules would call built (PieceKindByName's fallback)
            string Piece() => Ask(input.PieceKind, token) ?? ((token ?? "").StartsWith("$piece_", StringComparison.Ordinal) ? PieceKindByName(token) : null);
            switch (page)
            {
                case "crafting": return ItemKind(input, token) == "gear";
                case "cooking": return ItemKind(input, token) == "food";
                case "building": return Piece() == "built";
                case "groundwork": return Piece() == "ground";
                case "farming": return Piece() == "planted";
                default: return false;
            }
        }

        // ---------- the page ----------

        static void DeedHistoryPage(PanelInput input, PanelState state, PanelView view, string parent)
        {
            state = state ?? new PanelState();
            view.Recorded = true;
            var label = DeedsList.FirstOrDefault(p => p.id == parent).label ?? Cap(parent);
            var icon = DeedsList.FirstOrDefault(p => p.id == parent).icon;
            view.Heading = label + " history";
            view.Scope = RecordedScope(input);
            if (!HistoryMetrics.TryGetValue(parent, out var metrics) || input == null || !input.IsSelf || input.History == null)
            {
                view.Blocks.Add(Empty(NotYetRecorded, input != null && !input.IsSelf ? "History is kept on each player's own PC." : "History fills in day by day once Hearthwoven has saved a day."));
                Plate(view, icon, RecordedScope(input));
                return;
            }
            var today = LocalToday(input);
            var page = HistoryPageOf(parent);
            var metricKey = state.Chapter + "/" + page + "/metric"; var sizeKey = state.Chapter + "/" + page + "/period";
            var pickMetric = state.View.TryGetValue(metricKey, out var pm) && metrics.Any(m => m.Id == pm) ? pm : metrics[0].Id;
            var views = new List<Block>();
            foreach (var m in metrics)
            {
                var on = m.Id == pickMetric;
                views.Add(new Block { Kind = "view", Id = m.Id, Title = m.Chip, Selected = on, Items = on ? HistoryViews(input, state, parent, m, sizeKey, today) : null });
            }
            view.Blocks.Add(new Block { Kind = "switch", Id = metricKey, Items = views });
            foreach (var b in FirstsIsland(input, parent)) view.Blocks.Add(b);
            Plate(view, icon, RecordedScope(input));
        }

        // the period switch (Days, Weeks, Months: a size not open yet greyed with its date) and the chosen period's chart, its kinds' key under it
        static List<Block> HistoryViews(PanelInput input, PanelState state, string parent, HistoryMetric m, string sizeKey, DateTime today)
        {
            var sizes = new[] { (id: HistoryDays, title: "Days"), (id: HistoryWeeks, title: "Weeks"), (id: HistoryMonths, title: "Months") };
            var open = sizes.ToDictionary(s => s.id, s => s.id == HistoryDays ? (true, today) : HistorySizeOpen(input, s.id));
            var pick = state.View.TryGetValue(sizeKey, out var p) && open.TryGetValue(p, out var o) && o.Item1 ? p : HistoryDays;
            var items = new List<Block>();
            foreach (var s in sizes)
            {
                var (isOpen, opens) = open[s.id];
                var on = s.id == pick;
                items.Add(new Block
                {
                    Kind = "view", Id = s.id, Title = isOpen ? s.title : s.title + " from " + ShortDate(opens, today), Selected = on, Tone = isOpen ? null : OffTone,
                    Items = on ? HistoryChart(input, parent, m, s.id, today) : null,
                });
            }
            return new List<Block> { new Block { Kind = "switch", Id = sizeKey, Items = items } };
        }

        /// <summary>The History chart of one number in one period size, with its section and (option B) its kinds' key; the blocks in order.</summary>
        static List<Block> HistoryChart(PanelInput input, string parent, HistoryMetric m, string size, DateTime today)
        {
            var blocks = new List<Block>();
            var periods = HistoryPeriods(input, m.Counter, size);
            if (periods.Count == 0) { blocks.Add(Empty(NotYetRecorded)); return blocks; }
            var copies = periods.Select(x => PeriodCopy(input, x.Row, x.From, x.To)).ToList();
            var values = copies.Select(c => GrowthSafe(() => m.Of(c))).ToList();
            var total = values.Sum(); var max = values.DefaultIfEmpty(0).Max();
            var per = size == HistoryDays ? "per day" : size == HistoryWeeks ? "per week" : "per month";
            var head = new Block { Kind = "section", Title = m.Chip + " " + per, Value = total > 0 ? N(total) : null, Src = SrcPc, Source = TagOfSrc(SrcPc) };
            var since = NoonUtc(input, periods[0].From);   // the chart's numbers are the day history's, from its first column on
            DateFrom(head, since);
            blocks.Add(head);
            // option B: the kinds of the shown periods, the page's own bar over them as the key, each column split in the same colours
            Block key = null; Dictionary<string, string> colours = null; HashSet<string> named = null;
            if (m.Parts != null)
            {
                var kinds = new Dictionary<string, double>();
                var partsPer = copies.Select(c => { try { return m.Parts(c) ?? new Dictionary<string, double>(); } catch { return new Dictionary<string, double>(); } }).ToList();
                foreach (var d in partsPer) foreach (var kv in d) if (kv.Value > 0) Bump(kinds, kv.Key, kv.Value);
                if (kinds.Count > 1)
                {
                    Func<string, string> labelOf = k => m.Label != null ? m.Label(input, k) : Who(input, k);
                    key = Composition("By kind, these " + periods.Count + " " + (size == HistoryDays ? Label1(periods.Count, "day", "days") : size == HistoryWeeks ? Label1(periods.Count, "week", "weeks") : Label1(periods.Count, "month", "months")),
                                      kinds, k => labelOf(k), m.Look?.Invoke(input) ?? (k => (null, null)), SrcPc, icon: m.Icon ?? (k => "item:" + k), tint: m.Tint?.Invoke(input));
                    if (key != null)
                    {
                        colours = key.Items.Where(i => i.Id != FoldId).ToDictionary(i => i.Id, i => i.Colour);
                        named = new HashSet<string>(colours.Keys);
                    }
                }
                // the parts per column, in the key's order, the rest folded
                if (colours != null)
                {
                    var order = key.Items.Select(i => i.Id).ToList();
                    blocks.Add(Chart(input, parent, m, size, periods, values, max, total, today, (k, day) =>
                    {
                        var d = partsPer[k]; var sum = d.Values.Where(v => v > 0).Sum();
                        if (sum <= 0) return null;
                        var parts = new List<Block>();
                        foreach (var id in order.Where(x => x != FoldId)) if (d.TryGetValue(id, out var v) && v > 0) parts.Add(new Block { Id = id, Colour = colours[id], Fraction = (float)(v / sum), Src = SrcPc });   // a part inherits its day's source (TagSources): dated with it
                        var rest = d.Where(kv => kv.Value > 0 && !named.Contains(kv.Key)).Sum(kv => kv.Value);
                        if (rest > 0) parts.Add(new Block { Id = FoldId, Colour = BarOtherColour, Fraction = (float)(rest / sum), Src = SrcPc });
                        return parts;
                    }));
                    DateFrom(key, since); DateFrom(blocks[blocks.Count - 1], since);
                    blocks.Add(key);
                    return blocks;
                }
            }
            blocks.Add(Chart(input, parent, m, size, periods, values, max, total, today, null));
            DateFrom(blocks[blocks.Count - 1], since);
            return blocks;
        }

        // the chart block itself (see the class summary for its shape)
        static Block Chart(PanelInput input, string parent, HistoryMetric m, string size, List<HistoryPeriod> periods, List<double> values, double max, double total, DateTime today,
                           Func<int, DateTime, List<Block>> parts)
        {
            var chart = new Block
            {
                Kind = SparkKind, Tone = SparkLargeTone, Title = m.Chip + (size == HistoryDays ? " per day" : size == HistoryWeeks ? " per week" : " per month"), Value = total > 0 ? N(total) : "0",
                PartLink = size == HistoryDays ? "Deeds/" + DayPagePrefix + parent + ":" : null, Src = SrcPc, Source = TagOfSrc(SrcPc), Items = new List<Block>(),
            };
            for (int k = 0; k < periods.Count; k++)
            {
                var p = periods[k]; var v = values[k];
                var marks = HistoryMarks(input, parent, p.From, p.To);
                var words = PeriodWords(p.From, size, today) + " · " + (v > 0 ? NAtLeast(v) + " " + Label1(v, m.One, m.Many) : "nothing") + (marks.Count > 0 ? " · " + string.Join(" · ", marks) : "");
                chart.Items.Add(new Block
                {
                    Kind = SparkDay, Id = p.Key, Src = SrcPc, Source = TagOfSrc(SrcPc), Value = v > 0 ? NAtLeast(v) : "0", Fraction = max > 0 ? (float)(v / max) : 0, Tone = v > 0 ? null : "zero",
                    Title = words, Count = marks.Count, Text = marks.Count > 0 ? string.Join(" · ", marks) : null, Items = v > 0 ? parts?.Invoke(k, p.From) : null,
                    Note = AxisWord(k, periods.Count, p.From, size, today),
                });
            }
            return chart;
        }

        /// <summary>The date under a column (its Note): the first, every week's (fourth week's, third month's) after it while it keeps apart from the
        /// last, and the last ("today" for today); null under the others.</summary>
        public static string AxisWord(int k, int count, DateTime from, string size, DateTime today)
        {
            var every = size == HistoryDays ? 7 : size == HistoryWeeks ? 4 : 3;
            string Word() => size == HistoryMonths ? from.ToString(from.Year == today.Year ? "MMM" : "MMM yyyy", Inv) : ShortDate(from, today);
            if (k == count - 1) return size == HistoryDays && from == today ? "today" : Word();
            return k % every == 0 && count - 1 - k >= (every + 1) / 2 ? Word() : null;
        }

        /// <summary>A local day as a UTC instant whose local day it is (its noon), for the "Recorded from" words, which read UTC.</summary>
        static DateTime NoonUtc(PanelInput input, DateTime localDay)
        {
            var guess = DateTime.SpecifyKind(localDay.Date.AddHours(12), DateTimeKind.Utc);
            var shift = Local(input, guess) - guess;
            return DateTime.SpecifyKind(localDay.Date.AddHours(12) - shift, DateTimeKind.Utc);
        }

        /// <summary>The click target of a History chart's column: that day's page; null for a week or a month, or a chart that does not open days.</summary>
        public static string HistoryDayTarget(Block chart, Block day) => chart?.PartLink == null || day == null || string.IsNullOrEmpty(day.Id) ? null : chart.PartLink + day.Id;

        // ---------- the Firsts island ----------

        /// <summary>
        /// The Firsts island (rows form, REUSE.md: a rows block with an icon and the date in Value's place): what you first made or placed of this page's
        /// kind, newest first, recorded from 0.9 on this PC (record 3, FirstsFrom); at most FirstsTop rows, the rest said in a line. What came before has
        /// no date: "6 more first made before 2 November: the game counts them, not when." None on a page whose things are not made or placed.
        /// </summary>
        static List<Block> FirstsIsland(PanelInput input, string page)
        {
            var blocks = new List<Block>();
            if (input?.Firsts == null || !input.IsSelf || !new[] { "crafting", "cooking", "building", "groundwork", "farming" }.Contains(page)) return blocks;
            var today = LocalToday(input);
            var mine = input.Firsts.Where(kv => FirstOfPage(input, page, kv.Key)).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            var verb = page == "building" || page == "groundwork" || page == "farming" ? "placed" : "made";
            var earlier = EarlierThings(input, page).Where(t => !input.Firsts.ContainsKey(t)).Count();
            if (mine.Count == 0 && earlier == 0) return blocks;
            var head = new Block { Kind = "section", Title = FirstsTitle, Value = mine.Count > 0 ? N(mine.Count) : null, Src = SrcPc, Source = TagOfSrc(SrcPc) };
            DateFrom(head, input.FirstsFrom);
            blocks.Add(head);
            var icon = page == "crafting" || page == "cooking" ? "item:" : "piece:";
            if (mine.Count > 0)
            {
                var list = new Block
                {
                    Kind = "rows", Src = SrcPc, Source = TagOfSrc(SrcPc),
                    Items = mine.Take(FirstsTop).Select(kv => new Block { Id = kv.Key, Icon = icon + kv.Key, Title = Who(input, kv.Key), Value = "first recorded " + ShortDate(kv.Value.Date, today), Src = SrcPc, Source = TagOfSrc(SrcPc) }).ToList(),
                    Note = mine.Count > FirstsTop ? N(mine.Count - FirstsTop) + " more since " + (input.FirstsFrom.HasValue ? RecordDate(input, input.FirstsFrom.Value) : "then") : null,
                };
                DateFrom(list, input.FirstsFrom);
                blocks.Add(list);
            }
            else blocks.Add(new Block { Kind = "note", Text = "Each thing you " + verb + " that this PC never saw before shows here, first recorded, with its day." });
            if (earlier > 0 && input.FirstsFrom.HasValue)
                blocks.Add(new Block { Kind = "note", Text = N(earlier) + " " + (mine.Count > 0 ? "more " : "") + Label1(earlier, "kind", "kinds") + " first " + verb + " before " + RecordDate(input, input.FirstsFrom.Value) + ": the game counts them, not when." });
            return blocks;
        }

        // the page's kinds of things the character has made or placed in all (the game's per-token counters): the firsts before the record began are these
        static IEnumerable<string> EarlierThings(PanelInput input, string page)
        {
            switch (page)
            {
                case "crafting": return Crafted(input, "gear").Keys;
                case "cooking": return Crafted(input, "food").Keys;
                case "building": return Placed(input, "built").Keys;
                case "groundwork": return Placed(input, "ground").Keys;
                case "farming": return Placed(input, "planted").Keys;
                default: return Enumerable.Empty<string>();
            }
        }

        // ---------- a day's own page (a History column's click) ----------

        /// <summary>One day's copy of your input (as InWindow builds a day window's, for that one day; today with the running session's part); null when
        /// the history does not hold that day (a fellow's book, before the history began, a day folded into a week, a day to come).</summary>
        public static PanelInput OneDayInput(PanelInput input, DateTime day)
        {
            if (input == null || !input.IsSelf || input.History == null) return null;
            var today = LocalToday(input); day = day.Date;
            if (day > today || day < input.History.FirstDay(today) || day < today.AddDays(1 - DayHistory.DayRows)) return null;
            var row = input.History.Sum(day, day);
            if (day == today && input.Pending != null) row.AddAll(input.Pending);
            var c = input.ShallowCopy();
            c.Events = row.Events; c.DamageSinceInstall = row.Damage; c.BiomeSinceInstall = row.Biome; c.BiomeFromUtc = null;
            c.Character = row.Game.Where(kv => !kv.Key.Contains("|")).ToDictionary(kv => kv.Key, kv => kv.Value);
            c.PiecesPlaced = DayHistory.Family(row.Game, DayHistory.PlacedPrefix); c.Harvested = DayHistory.Family(row.Game, DayHistory.PickedPrefix);
            c.EnemyKills = null; c.ItemsPickedUp = new Dictionary<string, float>(row.Events.PickedUp); c.ItemsCrafted = new Dictionary<string, float>(row.Events.Made);
            c.Baseline = null; c.ExactAtBaseline = null; c.BaselineAt = null; c.Book = null;
            c.Window = new DayView { Window = TimeWindow.Today, From = day, To = day, Clipped = row.Clipped, Whole = input };
            c.DealtArmourSince = null;
            return c;
        }

        /// <summary>A Deeds page in one day (opened from its History): the page's own code on that day's copy, as a day window draws it, its heading the day
        /// ("Crafting, Mon 13 Oct"); no window chips (the day is the page). A day the history no longer holds says so.</summary>
        static void DeedsOneDay(PanelInput input, PanelState state, PanelView view, string parent, DateTime day)
        {
            var label = DeedsList.FirstOrDefault(p => p.id == parent).label ?? Cap(parent);
            var icon = DeedsList.FirstOrDefault(p => p.id == parent).icon;
            var today = input == null ? day : LocalToday(input);
            var words = PeriodWords(day, HistoryDays, today);
            var one = OneDayInput(input, day);
            if (one == null)
            {
                view.Recorded = true; view.Heading = label + ", " + words;
                view.Blocks.Add(Empty("This day is not kept", "Days are kept for " + DayHistory.DayRows + " days, then as weeks. Open History for what is kept."));
                Plate(view, icon, RecordedScope(input));
                return;
            }
            view.Windowed = true;   // the numbers are the day's, as a day window's: no "Recorded from" on them
            if (parent == "woodcutting" || parent == "mining") DeedsDay(one, parent, view, TimeWindow.Today);
            else if (DeedsPage(one, parent, view, state)) DeedsDayLines(input, one, parent, view, TimeWindow.Today);
            view.Heading = label + ", " + words;
            var plate = PlateOf(view);
            if (plate != null)
            {
                plate.Title = view.Heading;
                foreach (var b in plate.Items ?? new List<Block>())
                {
                    if (b?.Kind == "note" && b.Text != null) b.Text = b.Text.Replace(": choose All to see ", ": " + label + " in All shows ");   // no window chips on a day's page
                    if (b?.Kind == "empty" && b.Text == DayEmptyLine)   // no window chips here: the other days are in History
                    {
                        if (day != today && b.Title == "Nothing today") b.Title = "Nothing on " + words;
                        b.Text = "The other days are in " + label + " history.";
                    }
                }
            }
        }
    }
}
