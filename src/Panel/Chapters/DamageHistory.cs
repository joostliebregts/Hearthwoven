using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9.5 draft, Damage history (Joost 19 Oct, on Battle > Damage > By weapon: "I would love to see more clearly the damage I do over time with
    /// melee or bow"): the Deeds History (Chapters/DeedHistory.cs) built for Battle > Damage, the same form and the same rules, no new chart. Opened
    /// as a deed's History is: from the growth line beside the page's one answer ("49 920 Melee damage, the most of any weapon"), its gold
    /// "History" word (GrowthLines.cs DamageHistoryHook). The kind rows of By weapon get no second click: their click is the fold (h095-kindtoggle).
    ///
    /// What backs it, and from when:
    /// - Damage dealt per weapon kind per day: your day history (DayHistory rows: damage by foe, skill and type, the skill read as its kind,
    ///   DayHistory.WeaponKind; the eight battle types, before the foe's armor, as By weapon), from the history's first day (0.6.2 on this PC), the
    ///   last DayRows days as days, older ones as weeks and then months (Weeks and Months greyed with the day they open, as on Deeds).
    /// - DPS per kind per day: the dealt ledger's fighting seconds (ArmourBook day rows, ArmourTally.Swing, SwingClock), from the first 0.8.6 hit
    ///   (SwingFromUtc). A day before that has no DPS column at all and says why on hover: not recorded is not zero.
    /// - A friend's book: what their copy shares per day, the last 30 days: damage by kind and type (dayDealt, 0.9) and their weapons with their
    ///   fighting seconds (dayWeapons, 0.8.6). Days only: weeks and months are not shared (greyed, one line says so). A copy that shares no split
    ///   has no History (the hook is not drawn).
    /// - Everyone: none (the hook is your own and one friend's book only). A column group per player would set players' damage side by side per
    ///   day, and DPS never stands in a row of players (PaceModel.cs); the group's damage per day is the Company's Together.
    ///
    /// The form: chips for the weapon (All weapons, then each kind with damage), Days / Weeks / Months, the section, the large growth line (a column
    /// per day). All weapons: each column split by kind in By type's kind colours (CategoryColour), the kinds' bar under it as the key, each kind's
    /// damage and DPS that day on hover. One kind: its columns in its colour, and under them its DPS per day as a second chart of the same form, on
    /// the same columns, so a day's damage and its pace stand one above the other.
    /// </summary>
    public static partial class PanelModel
    {
        public const string DamageHistoryPage = HistoryPagePrefix + "damage";
        public const string DamageHistoryHeading = "Damage history", AllWeaponsChip = "All weapons", AllWeaponsId = "all";
        /// <summary>The tone of a History column with no number to draw (a DPS day before fighting time was recorded, or with too little of it):
        /// nothing drawn, its hover says why. Drawn by GrowthLinesUi.SparkLarge and sparkLarge() in panel-preview.html.</summary>
        public const string SparkNoneTone = "none";
        /// <summary>The one-answer line of By weapon that carries the History link (BattleModel.BattleDamage's TopLine; never the group's "together" line).</summary>
        public const string MostOfAnyWeapon = " damage, the most of any weapon";
        /// <summary>The days a friend's copy shares (GroupShare: dayDealt and dayWeapons).</summary>
        public const int SharedDays = 30;

        public static bool IsDamageHistoryPage(string page) => page == DamageHistoryPage;

        /// <summary>Whether this book has a Damage history: your own with its day history, a friend's whose copy splits its days by weapon kind.</summary>
        public static bool DamageHistoryOpen(PanelInput input) => input != null && (input.IsSelf ? input.History != null : input.DayDealt != null && input.DayDealt.Count > 0);

        /// <summary>One column of the history: its days, damage per kind, and fighting seconds and damage swung in them per kind (null: not recorded).</summary>
        sealed class DamagePeriod
        {
            public string Key; public DateTime From, To;
            public readonly Dictionary<string, double> Kinds = new Dictionary<string, double>();
            public Dictionary<string, (double seconds, double swung)> Swing;
            public double Total => Kinds.Values.Sum();
        }

        // a dealt key "foe|skill|type" (or a shared "|kind|type") as its weapon kind and type
        static bool KindOfDealt(string key, out string kind, out string type)
        {
            kind = type = null;
            var last = key.LastIndexOf('|'); if (last < 0) return false;
            var mid = last > 0 ? key.LastIndexOf('|', last - 1) : -1;
            type = key.Substring(last + 1);
            if (Array.IndexOf(BattleTypes, type) < 0) return false;
            kind = DayHistory.WeaponKind(mid >= 0 ? key.Substring(mid + 1, last - mid - 1) : key.Substring(0, last));   // a shared "bow|pierce" carries the kind itself
            return true;
        }

        static void AddDealt(DamagePeriod p, DamageTally t)
        {
            foreach (var kv in t?.Dealt ?? new Dictionary<string, float>())
                if (kv.Value > 0 && KindOfDealt(kv.Key, out var kind, out _)) Bump(p.Kinds, kind, kv.Value);
        }

        static void AddSwing(DamagePeriod p, ArmourTally t)
        {
            if (t == null) return;
            if (p.Swing == null) p.Swing = SwingClock.Kinds.ToDictionary(k => k, k => (0.0, 0.0));
            foreach (var k in SwingClock.Kinds) { var (s, d) = t.SwingOf(k); var (s0, d0) = p.Swing[k]; p.Swing[k] = (s0 + s, d0 + d); }
        }

        /// <summary>Your history's columns in one size: the day history's periods (DeedHistory.HistoryPeriods, today with the running session's part),
        /// each with the dealt ledger's fighting time where it was recorded (from the period's first day on or after SwingFromUtc).</summary>
        static List<DamagePeriod> OwnDamagePeriods(PanelInput input, string size)
        {
            var list = new List<DamagePeriod>();
            var today = LocalToday(input);
            var swingFrom = SwingFromDay(input);
            var book = input.DealtArmourBook;
            foreach (var hp in HistoryPeriods(input, null, size))
            {
                var p = new DamagePeriod { Key = hp.Key, From = hp.From, To = hp.To };
                AddDealt(p, hp.Row.Damage);
                if (swingFrom.HasValue && hp.To >= swingFrom.Value)
                {
                    AddSwing(p, new ArmourTally());   // recorded: zero seconds is a day without fighting, not a day without a record
                    foreach (var r in book?.Days ?? new List<(string Period, ArmourTally Tally)>())
                    {
                        if (r.Period == DayHistory.BeforeKey) continue;
                        var start = DayHistory.StartOf(r.Period);
                        if (start >= hp.From && start <= hp.To) AddSwing(p, r.Tally);
                    }
                    if (today >= hp.From && today <= hp.To) AddSwing(p, book != null ? input.DealtArmourPending : input.DealtArmourSession);
                }
                list.Add(p);
            }
            return list;
        }

        /// <summary>The local day your fighting time began to be recorded (the dealt ledger's SwingFromUtc, PanelInput.SwingFromUtc); null = not yet.</summary>
        static DateTime? SwingFromDay(PanelInput input)
        {
            var from = input.SwingFromUtc ?? (input.DealtArmourBook != null && input.DealtArmourBook.SwingFromUtc > DateTime.MinValue ? input.DealtArmourBook.SwingFromUtc : (DateTime?)null);
            return from.HasValue && from.Value > DateTime.MinValue ? Local(input, from.Value).Date : (DateTime?)null;
        }

        /// <summary>A friend's columns: the days their copy shares (dayDealt), from the first shared day (at most SharedDays back) to today; their
        /// fighting seconds per kind from the weapons they share per day (dayWeapons), from its first shared day.</summary>
        static List<DamagePeriod> SharedDamagePeriods(PanelInput input, out DateTime? swingFrom)
        {
            swingFrom = null;
            var list = new List<DamagePeriod>();
            var today = LocalToday(input);
            var dealt = new Dictionary<DateTime, Dictionary<string, float>>();
            foreach (var kv in input.DayDealt ?? new Dictionary<string, Dictionary<string, float>>())
                if (DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", Inv, System.Globalization.DateTimeStyles.None, out var d)) dealt[d.Date] = kv.Value;
            var weapons = new Dictionary<DateTime, WeaponCells>();
            foreach (var kv in input.DayWeapons ?? new Dictionary<string, WeaponCells>())
                if (DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", Inv, System.Globalization.DateTimeStyles.None, out var d)) weapons[d.Date] = kv.Value;
            if (dealt.Count == 0) return list;
            var back = today.AddDays(1 - SharedDays);
            var first = dealt.Keys.Min(); if (first < back) first = back;
            if (weapons.Count > 0) { var w = weapons.Keys.Min(); swingFrom = w < back ? back : w; }
            for (var d = first; d <= today; d = d.AddDays(1))
            {
                var p = new DamagePeriod { Key = DayHistory.DayKey(d), From = d, To = d };
                if (dealt.TryGetValue(d, out var parts))
                    foreach (var kv in parts ?? new Dictionary<string, float>())
                        if (kv.Value > 0 && KindOfDealt(kv.Key, out var kind, out _)) Bump(p.Kinds, kind, kv.Value);
                if (swingFrom.HasValue && d >= swingFrom.Value)
                {
                    p.Swing = SwingClock.Kinds.ToDictionary(k => k, k => (0.0, 0.0));
                    if (weapons.TryGetValue(d, out var cells))
                        foreach (var kv in cells.Map)
                            if (WeaponKey.Split(kv.Key, out _, out _, out var skill))
                            {
                                var k = SwingClock.Kinds[SwingClock.KindOf(skill)]; var (s, sw) = p.Swing[k];
                                p.Swing[k] = (s + kv.Value.Sec, sw + kv.Value.Swung);
                            }
                }
                list.Add(p);
            }
            return list;
        }

        // ---------- the page ----------

        static void DamageHistoryView(PanelInput input, PanelState state, PanelView view)
        {
            state = state ?? new PanelState();
            view.Recorded = true;
            view.Heading = DamageHistoryHeading; view.HeadingSource = null;
            view.Scope = RecordedScope(input);
            if (!DamageHistoryOpen(input))
            {
                view.Blocks.Add(Empty(NotYetRecorded, input != null && !input.IsSelf ? Name(input) + "'s copy does not share damage per day yet." : "Damage history fills in day by day once Hearthwoven has saved a day."));
                Plate(view, "ui:chapter-battle", input != null && !input.IsSelf ? RecordedScope(input) : null);
                return;
            }
            var page = DamageHistoryPage;
            var kindKey = state.Chapter + "/" + page + "/kind"; var sizeKey = state.Chapter + "/" + page + "/period";
            // the kinds with damage in what the book keeps (all days, weeks and months): a chip each after All weapons
            var whole = input.IsSelf ? OwnDamagePeriods(input, HistoryMonths).Concat(OwnDamagePeriods(input, HistoryDays)) : SharedDamagePeriods(input, out _);
            var kinds = WeaponKinds.Where(k => whole.Any(p => p.Kinds.TryGetValue(k, out var v) && v > 0)).ToList();
            var chips = new List<(string id, string title)> { (AllWeaponsId, AllWeaponsChip) };
            chips.AddRange(kinds.Select(k => (k, WeaponName(k))));
            var pick = state.View.TryGetValue(kindKey, out var pk) && chips.Any(c => c.id == pk) ? pk : AllWeaponsId;
            var views = chips.Select(c => new Block { Kind = "view", Id = c.id, Title = c.title, Selected = c.id == pick, Items = c.id == pick ? DamageHistorySizes(input, state, c.id, sizeKey) : null }).ToList();
            view.Blocks.Add(new Block { Kind = "switch", Id = kindKey, Items = views });
            if (pick != AllWeaponsId) AboutLines(view, (DpsAboutTitle, Voice(input, DpsAbout(input, TimeWindow.SinceInstall))));
            Plate(view, "ui:chapter-battle");   // a friend's: whose copy is in the scope and the sections ("Recorded on Edda's PC"); Weeks and Months say "not shared" themselves
        }

        // Days, Weeks, Months (your own book: as a deed's History; a friend's: days only, the others greyed) and the chosen size's charts
        static List<Block> DamageHistorySizes(PanelInput input, PanelState state, string kind, string sizeKey)
        {
            var today = LocalToday(input);
            var sizes = new[] { (id: HistoryDays, title: "Days"), (id: HistoryWeeks, title: "Weeks"), (id: HistoryMonths, title: "Months") };
            var open = sizes.ToDictionary(s => s.id, s => s.id == HistoryDays ? (true, today) : input.IsSelf ? HistorySizeOpen(input, s.id) : (false, DateTime.MinValue));
            var pick = state.View.TryGetValue(sizeKey, out var p) && open.TryGetValue(p, out var o) && o.Item1 ? p : HistoryDays;
            var items = sizes.Select(s =>
            {
                var (isOpen, opens) = open[s.id];
                var title = isOpen ? s.title : opens > DateTime.MinValue ? s.title + " from " + ShortDate(opens, today) : s.title + ", not shared";
                return new Block { Kind = "view", Id = s.id, Title = title, Selected = s.id == pick, Tone = isOpen ? null : OffTone, Items = s.id == pick ? DamageHistoryCharts(input, kind, s.id, today) : null };
            }).ToList();
            return new List<Block> { new Block { Kind = "switch", Id = sizeKey, Items = items } };
        }

        static string KindColour(string kind) => CategoryColour(WeaponName(kind));
        static string Per(string size) => size == HistoryDays ? "per day" : size == HistoryWeeks ? "per week" : "per month";

        /// <summary>The charts of one weapon choice in one size, in order: the damage section and chart (All weapons: its kinds' key under it), then
        /// (one kind) its DPS section and chart on the same columns.</summary>
        static List<Block> DamageHistoryCharts(PanelInput input, string kind, string size, DateTime today)
        {
            var blocks = new List<Block>();
            DateTime? swingFrom = null;
            var periods = input.IsSelf ? OwnDamagePeriods(input, size) : SharedDamagePeriods(input, out swingFrom);
            if (input.IsSelf) swingFrom = SwingFromDay(input);
            if (periods.Count == 0) { blocks.Add(Empty(NotYetRecorded)); return blocks; }
            var all = kind == AllWeaponsId;
            var label = all ? "Damage dealt" : WeaponName(kind) + " damage";
            var values = periods.Select(x => all ? x.Total : (x.Kinds.TryGetValue(kind, out var v) ? v : 0)).ToList();
            var total = values.Sum(); var max = values.DefaultIfEmpty(0).Max();
            var since = input.IsSelf ? NoonUtc(input, periods[0].From) : (DateTime?)null;   // your numbers are the day history's, from its first column on; a friend's say whose copy in the plate
            var head = new Block { Kind = "section", Title = label + " " + Per(size), Value = total > 0 ? N(total) : null, Src = SrcPc, Source = TagOfSrc(SrcPc) };
            DateFrom(head, since);
            blocks.Add(head);
            var chart = new Block { Kind = SparkKind, Tone = SparkLargeTone, Title = label + " " + Per(size), Value = total > 0 ? N(total) : "0", Src = SrcPc, Source = TagOfSrc(SrcPc), Items = new List<Block>() };
            var shown = WeaponKinds.Where(k => periods.Any(x => x.Kinds.TryGetValue(k, out var v) && v > 0)).ToList();
            for (int k = 0; k < periods.Count; k++)
            {
                var p = periods[k]; var v = values[k];
                List<Block> parts = null;
                if (v > 0)
                {
                    // All weapons: the kinds stacked in By weapon's order (melee at the foot); one kind: its colour, one part
                    var ks = all ? shown : new List<string> { kind };
                    parts = ks.Where(x => p.Kinds.TryGetValue(x, out var kv) && kv > 0).Select(x => new Block { Id = x, Colour = KindColour(x), Fraction = (float)(p.Kinds[x] / v), Src = SrcPc }).ToList();
                }
                chart.Items.Add(new Block
                {
                    Kind = SparkDay, Id = p.Key, Src = SrcPc, Source = TagOfSrc(SrcPc), Value = v > 0 ? Math.Max(1, Math.Round(v)).ToString(Inv) : "0", Fraction = max > 0 ? (float)(v / max) : 0, Tone = v > 0 ? null : "zero",   // the plain number (the hover says it in words): the chart's own scale
                    Title = DamageDayWords(p, size, today, all ? null : kind, swingFrom), Items = parts, Note = AxisWord(k, periods.Count, p.From, size, today),
                });
            }
            DateFrom(chart, since);
            blocks.Add(chart);
            if (all)
            {
                var sums = new Dictionary<string, double>();
                foreach (var p in periods) foreach (var kv in p.Kinds) if (kv.Value > 0) Bump(sums, kv.Key, kv.Value);
                var n = periods.Count;
                var key = Composition("By weapon, these " + n + " " + (size == HistoryDays ? Label1(n, "day", "days") : size == HistoryWeeks ? Label1(n, "week", "weeks") : Label1(n, "month", "months")),
                                      sums, WeaponName, k => (KindColour(k), null), SrcPc, icon: k => "vocab:weapon-" + k);
                if (key != null) { DateFrom(key, since); blocks.Add(key); }
                if (periods.Any(x => x.Swing != null)) blocks.Add(new Block { Kind = "note", Text = "Point at a day for each weapon's damage and DPS that day. Choose a weapon above for its DPS per " + (size == HistoryDays ? "day" : size == HistoryWeeks ? "week" : "month") + "." });
                return blocks;
            }
            blocks.AddRange(DpsChart(input, kind, size, periods, swingFrom, today));
            return blocks;
        }

        /// <summary>One kind's DPS per period on the damage chart's columns: its pace (swung over seconds, SwingClock.Pace) as tall as the period against
        /// the largest; no column where the pace is not known (before fighting time was recorded, no fighting, under SwingClock.Floor seconds), its hover
        /// says which. The section's number is the pace of all the shown periods together.</summary>
        static List<Block> DpsChart(PanelInput input, string kind, string size, List<DamagePeriod> periods, DateTime? swingFrom, DateTime today)
        {
            var blocks = new List<Block>();
            var title = WeaponName(kind) + " DPS " + Per(size);
            var paces = periods.Select(p => p.Swing != null && p.Swing.TryGetValue(kind, out var s) ? SwingClock.Pace(s.swung, s.seconds) : -1).ToList();
            double secs = 0, swung = 0;
            foreach (var p in periods) if (p.Swing != null && p.Swing.TryGetValue(kind, out var s)) { secs += s.seconds; swung += s.swung; }
            var overall = SwingClock.Words(SwingClock.Pace(swung, secs));
            var head = new Block { Kind = "section", Title = title, Value = overall.Length > 0 ? overall : null, Src = SrcPc, Source = TagOfSrc(SrcPc) };
            var from = swingFrom.HasValue && input.IsSelf ? NoonUtc(input, swingFrom.Value > periods[0].From ? swingFrom.Value : periods[0].From) : (DateTime?)null;
            DateFrom(head, from);
            blocks.Add(head);
            if (!paces.Any(x => x >= 0))
            {
                blocks.Add(new Block { Kind = "note", Text = !swingFrom.HasValue ? "Fighting time is not recorded yet, so there is no DPS to show." : "Not enough " + WeaponName(kind).ToLowerInvariant() + " fighting in these " + (size == HistoryDays ? "days" : size == HistoryWeeks ? "weeks" : "months") + " for a DPS: it shows after " + (int)SwingClock.Floor + " seconds of fighting." });
                return blocks;
            }
            var max = paces.Max();
            var chart = new Block { Kind = SparkKind, Tone = SparkLargeTone, Title = title, Value = overall, Src = SrcPc, Source = TagOfSrc(SrcPc), Items = new List<Block>() };
            for (int k = 0; k < periods.Count; k++)
            {
                var p = periods[k]; var pace = paces[k];
                var known = pace >= 0;
                chart.Items.Add(new Block
                {
                    Kind = SparkDay, Id = p.Key, Src = SrcPc, Source = TagOfSrc(SrcPc), Value = known ? SwingClock.Words(pace).Replace(" DPS", "") : "", Fraction = known && max > 0 ? (float)(pace / max) : 0,
                    Tone = known ? null : SparkNoneTone, Title = DpsDayWords(p, size, today, kind, swingFrom), Note = AxisWord(k, periods.Count, p.From, size, today),
                    Items = known ? new List<Block> { new Block { Id = kind, Colour = KindColour(kind), Fraction = 1f, Src = SrcPc } } : null,
                });
            }
            DateFrom(chart, from);
            blocks.Add(chart);
            return blocks;
        }

        /// <summary>A damage column's hover: the period, its damage, and each kind's part with its DPS that period where it is known
        /// ("Mon 5 Oct · 4 210 damage · Melee 3 100, 38 DPS · Bow 1 110, 21 DPS"); one kind: that kind's damage and DPS.</summary>
        static string DamageDayWords(DamagePeriod p, string size, DateTime today, string kind, DateTime? swingFrom)
        {
            var words = PeriodWords(p.From, size, today);
            string Pace(string k) => p.Swing != null && p.Swing.TryGetValue(k, out var s) ? SwingClock.Words(SwingClock.Pace(s.swung, s.seconds)) : "";
            if (kind != null)
            {
                p.Kinds.TryGetValue(kind, out var v);
                if (!(v > 0)) return words + " · no " + WeaponName(kind).ToLowerInvariant() + " damage";
                var pace = Pace(kind);
                return words + " · " + NAtLeast(v) + " " + WeaponName(kind).ToLowerInvariant() + " damage" + (pace.Length > 0 ? " · " + pace : "");
            }
            if (!(p.Total > 0)) return words + " · no damage dealt";
            var parts = WeaponKinds.Where(k => p.Kinds.TryGetValue(k, out var x) && x > 0).Select(k => { var pace = Pace(k); return WeaponName(k) + " " + NAtLeast(p.Kinds[k]) + (pace.Length > 0 ? ", " + pace : ""); });
            return words + " · " + NAtLeast(p.Total) + " damage · " + string.Join(" · ", parts);
        }

        /// <summary>A DPS column's hover: the pace and the fighting time behind it, or why there is none (not recorded yet, no fighting, too little).</summary>
        static string DpsDayWords(DamagePeriod p, string size, DateTime today, string kind, DateTime? swingFrom)
        {
            var words = PeriodWords(p.From, size, today);
            if (p.Swing == null) return words + " · fighting time not recorded" + (swingFrom.HasValue ? " before " + ShortDate(swingFrom.Value, today) : " yet");
            p.Swing.TryGetValue(kind, out var s);
            if (!(s.seconds > 0)) return words + " · no " + WeaponName(kind).ToLowerInvariant() + " fighting";
            var pace = SwingClock.Words(SwingClock.Pace(s.swung, s.seconds));
            return pace.Length == 0 ? words + " · " + About(s.seconds) + " of fighting, too little for a DPS" : words + " · " + pace + " over " + About(s.seconds) + " of fighting";
        }

        /// <summary>The History link on Battle > Damage (Build's last step, GrowthLine): By weapon's one answer gets the damage growth line (the Overview's,
        /// DealtGrowth) with the gold History word, as a Deeds hero does; a click opens Damage history. Your own book and a friend's whose copy splits
        /// its days; never Everyone (its line says "together") nor a page without a growth line.</summary>
        static void DamageHistoryHook(PanelInput input, PanelView view)
        {
            if (!DamageHistoryOpen(input)) return;
            var top = Content(view).FirstOrDefault(b => b.Kind == "stat" && (b.Title ?? "").EndsWith(MostOfAnyWeapon, StringComparison.Ordinal));
            if (top == null || SparkOf(top) != null) return;
            var spark = DealtGrowth(input);
            if (spark == null) return;
            spark.Id = "Battle/" + DamageHistoryPage;
            (top.Items ?? (top.Items = new List<Block>())).Add(spark);
        }
    }
}
