using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Compare periods (0.8; work/hearthwoven-0.8/prototypes/PICKS.md section 3, pick B; work/hearthwoven-0.7/COMPARE-SCOPE.md). One Compare
    /// chip after a page's window chips pairs the chosen day window with the stretch just before it, rolling (Joost: 7 days against the 7 before,
    /// not calendar weeks): Today with yesterday, 7 days with the 7 before. The page is built a second time for the period before (BeforeInput:
    /// the same input seen from N days earlier, without this session's unsaved part, so InWindow sums that period's day rows) and the two views
    /// are merged: the hero numbers and every cap with a total get the number before and a change mark; every composition, ranking and list of
    /// amounts opens into rows on one scale (kind "comparerows": this period solid, the period before as a thin outline at its length, then now ·
    /// before · change). The mark never judges: ▲ and ▼ in the text colour, "new" when there was none before, "same" when equal.
    /// Greyed, with the reason its tag says, where nothing fair can be paired: the short windows, Session and All (the day history holds days, the
    /// session's minutes no earlier session), before the day history reaches both periods ("Compare opens on 22 Oct"), a window whose pair is
    /// older than the history's day rows (30 days: rows are kept per day for DayHistory.DayRows days), a fellow's book (they share no days).
    /// A comparison never mixes a game total with a count from install (the 0.7 "one recorded total" rule): both periods come from the day rows,
    /// and a block whose source differs between the two periods is left as it is.
    /// </summary>
    public static partial class PanelModel
    {
        public const string CompareId = "compare", CompareLabel = "Compare", CompareRowsKind = "comparerows";
        /// <summary>The click target of the Compare chip (Follow): on and off.</summary>
        public const string CompareTarget = "compare:toggle";
        /// <summary>The change mark's arrows and words (never green or red: the mark does not judge).</summary>
        public const string CompareUp = "▲ ", CompareDown = "▼ ", CompareNew = "new", CompareSame = "same", CompareUnderOne = "<1 %";
        public const string CompareNowHead = "now", CompareBeforeHead = "before";
        /// <summary>The bar colour of a row that is a thing, not an item or a damage type (a tree, a weapon, a foe): the ranking's gold.</summary>
        public const string CompareRowColour = "#e8a948";
        /// <summary>At most this many rows per block; the rest as "Other (n kinds)", as the bar form folds.</summary>
        public const int CompareMaxRows = 9;

        public const string CompareNeedsDay = "Compare needs a day window. Pick Today or 7 days to set it beside the one before.";
        public const string CompareOwnBook = "Compare works in your own book.";
        public const string CompareArmourLine = "What your armor stopped is shown for this period only.";
        /// <summary>0.8, with the Everyone chip (PICKS.md section 3: only Battle > Damage By player compares the group, from the fellows' damage
        /// dealt per day): the greyed chip's reason on every other group page.</summary>
        public const string CompareEveryoneOff = "Not while Everyone is on: fellow players share totals, not days, so there is no earlier stretch to set beside the group's. Battle > Damage compares the group, player by player.";
        /// <summary>Battle > Damage By foe while comparing: the damage per foe kind is compared, the "×N" and the marks are not.</summary>
        public const string CompareByFoeLine = "The ×N and the marks show with Compare off.";

        /// <summary>The two periods of a comparison and whether it can be shown (Why: the greyed chip's reason, for its tag).</summary>
        public sealed class ComparePeriods
        {
            public TimeWindow Window;
            public DateTime From, To, BeforeFrom, BeforeTo;
            public bool Open;
            public string Why;
            /// <summary>The first day it works (the dated reason); null when it works, or never will in this window.</summary>
            public DateTime? OpensOn;
        }

        /// <summary>The pages that offer Compare: the day-window pages with addable numbers (Deeds' pages but Overview and Recent, Battle Damage and
        /// Defence, Voyages Sailing and Cargo). Company > Together offers it under Damage dealt (its own window chips; TogetherCompared).</summary>
        public static bool CompareOffered(Chapter c, string page) =>
            c == Chapter.Deeds ? page != null && page != "overview" && page != RecentPageId && WindowsOf(c, page) != null
            : c == Chapter.Battle ? page == "damage" || page == "defense"
            : c == Chapter.Voyages && (page == "sailing" || page == "cargo");

        /// <summary>"the 7 days before", "yesterday": what the number before is, where it stands beside a number now.</summary>
        public static string BeforeLabel(TimeWindow w) => w == TimeWindow.Today ? "yesterday" : "the " + DaysOf(w) + " days before";

        /// <summary>"7 days against the 7 before", "today against yesterday".</summary>
        static string Against(TimeWindow w) => w == TimeWindow.Today ? "today against yesterday" : DaysOf(w) + " days against the " + DaysOf(w) + " before";

        /// <summary>A day range, short: "4 to 10 Oct", "27 Sep to 3 Oct"; one day: "10 Oct".</summary>
        public static string DayRange(DateTime from, DateTime to, DateTime today)
        {
            if (from.Date == to.Date) return ShortDate(from, today);
            var head = from.Year != to.Year || from.Year != today.Year ? ShortDate(from, today) : from.Month == to.Month ? from.Day.ToString(Inv) : from.ToString("d MMM", Inv);
            return head + " to " + ShortDate(to, today);
        }

        /// <summary>The plate's first line while comparing: "7 days, 4 to 10 Oct, against the 7 days before, 27 Sep to 3 Oct."</summary>
        public static string CompareLine(ComparePeriods p, DateTime today) =>
            p.Window == TimeWindow.Today ? "Today, " + ShortDate(p.To, today) + ", against yesterday, " + ShortDate(p.BeforeTo, today) + "."
            : DaysOf(p.Window) + " days, " + DayRange(p.From, p.To, today) + ", against the " + DaysOf(p.Window) + " days before, " + DayRange(p.BeforeFrom, p.BeforeTo, today) + ".";

        // ---------- the change mark ----------

        /// <summary>The change from before to now: "▲ 12 %", "▼ 30 %", "new" (nothing before), "same", "▲ &lt;1 %". Whole per cent,
        /// rounded half away from zero, of (now - before) / before.</summary>
        public static string ChangeMark(double now, double before)
        {
            if (double.IsNaN(now) || double.IsNaN(before)) return null;
            if (before <= 0) return now > 0 ? CompareNew : CompareSame;
            if (Math.Abs(now - before) < 1e-9) return CompareSame;
            var c = (now - before) / before * 100;
            var r = Math.Round(Math.Abs(c), MidpointRounding.AwayFromZero);
            return (c > 0 ? CompareUp : CompareDown) + (r < 1 ? CompareUnderOne : N(r) + " %");
        }

        /// <summary>The direction of a change mark: 1 up, -1 down, 0 none ("new", "same"), and its words without the arrow (the panel draws the
        /// arrow as a picture: the game's fonts may not hold the glyph).</summary>
        public static (int dir, string words) ChangeParts(string mark)
        {
            if (string.IsNullOrEmpty(mark)) return (0, "");
            if (mark.StartsWith(CompareUp, StringComparison.Ordinal)) return (1, mark.Substring(CompareUp.Length));
            if (mark.StartsWith(CompareDown, StringComparison.Ordinal)) return (-1, mark.Substring(CompareDown.Length));
            return (0, mark);
        }

        static readonly Regex HoursMinutes = new Regex(@"^(?:(\d+) hours?)?(?: ?(\d+) min)?$", RegexOptions.CultureInvariant);
        static readonly Regex CountUnit = new Regex(@"^([0-9\s   ,.'’]*[0-9])\s*(km|m|item-km|item-m)?$", RegexOptions.CultureInvariant);

        /// <summary>
        /// A shown amount as a number in its base unit, so two periods compare even when their units differ: a count ("1 670"), a distance in
        /// metres ("2.0 km" = 2000, "640 m"), cargo in item-metres ("576 item-km"), time in minutes ("1 hour 1 min" = 61, "under 1 min" = 0.5),
        /// "&lt;1" = 0.5, "" = 0. NaN when the text is not an amount (a word, a level). Kind says which unit family, so a distance never
        /// sets beside a count.
        /// </summary>
        public static double Amount(string text, out string kind)
        {
            kind = "count";
            if (text == null) return double.NaN;
            var t = text.Trim();
            if (t.Length == 0) return 0;
            if (t == LessThanOne) return 0.5;
            if (t == "under 1 min") { kind = "time"; return 0.5; }
            var hm = HoursMinutes.Match(t);
            if (hm.Success && (hm.Groups[1].Success || hm.Groups[2].Success))
            {
                kind = "time";
                return (hm.Groups[1].Success ? int.Parse(hm.Groups[1].Value, Inv) * 60 : 0) + (hm.Groups[2].Success ? int.Parse(hm.Groups[2].Value, Inv) : 0);
            }
            var m = CountUnit.Match(t);
            if (!m.Success) return double.NaN;
            var v = ParseCount(m.Groups[1].Value);
            switch (m.Groups[2].Success ? m.Groups[2].Value : "")
            {
                case "km": kind = "distance"; return v * 1000;
                case "m": kind = "distance"; return v;
                case "item-km": kind = "cargo"; return v * 1000;
                case "item-m": kind = "cargo"; return v;
                default: return v;
            }
        }
        public static double Amount(string text) => Amount(text, out _);

        /// <summary>Nothing, said in the unit of a shown amount: "0", "0.0 km", "0 min", "0 item-km".</summary>
        static string ZeroLike(string shown)
        {
            Amount(shown, out var kind);
            var t = (shown ?? "").Trim();
            if (kind == "time") return "0 min";
            if (kind == "distance") return t.EndsWith(" m", StringComparison.Ordinal) ? "0 m" : "0.0 km";
            if (kind == "cargo") return "0 " + t.Substring(t.LastIndexOf(' ') + 1);
            return "0";
        }

        /// <summary>A title without its plural, so "1 block" and "229 blocks" are one number of the page ("parries" = "parry", "pieces" = "piece").</summary>
        static string Loose(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            var words = title.ToLowerInvariant().Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                var w = words[i];
                if (w.EndsWith("ies") && w.Length > 4) w = w.Substring(0, w.Length - 3) + "y";
                else if (w.EndsWith("ches") || w.EndsWith("shes") || w.EndsWith("xes") || w.EndsWith("sses")) w = w.Substring(0, w.Length - 2);
                else if (w.EndsWith("s") && !w.EndsWith("ss") && w.Length > 2) w = w.Substring(0, w.Length - 1);
                words[i] = w;
            }
            return string.Join(" ", words);
        }

        // ---------- whether a page can compare ----------

        static Block TogetherDealtSwitch(Block together) =>
            (together?.Items ?? new List<Block>()).Any(c => c.Kind == "category" && c.Id == "dealt" && c.Selected) ? (together.Items ?? new List<Block>()).FirstOrDefault(c => c.Kind == "switch") : null;

        /// <summary>The first local day the day rows hold every day from (later rows were folded into weeks and months by DayHistory.Fold): a window
        /// never sums a stretch whose days are gone.</summary>
        static DateTime DayRowsFrom(DayHistory h, DateTime today)
        {
            var from = h.FirstDay(today);
            var cut = today.AddDays(1 - DayHistory.DayRows);
            if (cut > from) from = cut;
            foreach (var r in h.Rows)
            {
                if (r.IsDay) continue;
                var end = r.Period == DayHistory.BeforeKey ? DateTime.MinValue : r.Period[0] == 'w' ? r.Start.AddDays(7) : r.Start.AddMonths(1);
                if (end > from) from = end;
            }
            return from;
        }

        /// <summary>The counter a Deeds page reads that the history began keeping later (DayHistory.Began, as DeedsDayLines says it); null = none.</summary>
        static string CompareCounter(PanelInput input, Chapter c, string page)
        {
            if (c != Chapter.Deeds) return null;
            switch (page)
            {
                case "building": case "groundwork": return DayHistory.PlacedPrefix;
                case "cooking": return Placed(input, "feast").Values.Sum() > 0 ? DayHistory.PlacedPrefix : null;
                case "farming": case "fishing": return DayHistory.PickedPrefix;
                case "taming": return "CreatureTamed";
                default: return null;
            }
        }

        static ComparePeriods Greyed(ComparePeriods p, string why) { p.Open = false; p.Why = why; return p; }

        /// <summary>
        /// Whether this page compares and the two periods: null when the page offers no Compare. The window is the one the page shows; a day
        /// window the history does not reach yet (the page shows All) is the one asked for, so its reason can say the date.
        /// </summary>
        public static ComparePeriods CompareOf(PanelInput input, PanelState state, PanelView view)
        {
            if (input == null || state == null || view == null || state.ShowAbout) return null;
            Block together = view.Active == Chapter.Company && view.Page == "together" ? TogetherOf(view) : null, sw = TogetherDealtSwitch(together);
            if (sw == null && !CompareOffered(view.Active, view.Page)) return null;
            TimeWindow? w;
            if (sw != null)
            {
                // the window Together shows (a greyed day window pressed shows All): on All its window row holds its long caption ("All: recorded
                // from 1 October") and the chip would cut it, so Together offers Compare on its other windows only
                var shown = (sw.Items ?? new List<Block>()).FirstOrDefault(v => v.Selected)?.Id;
                w = Enum.TryParse(shown ?? "", out TimeWindow chosen) && Enum.IsDefined(typeof(TimeWindow), chosen) ? chosen : TimeWindow.SinceInstall;
                if (w == TimeWindow.SinceInstall) return null;
            }
            else
            {
                if (view.Windows.Count == 0) return null;
                w = view.ShownWindow;
                if (w == TimeWindow.SinceInstall && IsDayWindow(state.Window) && view.Windows.Any(c => c.Id == state.Window.ToString())) w = state.Window;   // a greyed day window pressed (B17)
            }
            var p = new ComparePeriods { Window = w ?? TimeWindow.SinceInstall };
            if (!input.IsSelf) return Greyed(p, Name(input) + " shares totals, not days, so there is no earlier stretch to set beside this one. " + CompareOwnBook);
            // 0.8 the Everyone chip: the group's page compares only on Battle > Damage, By player (each fellow's damage dealt per day)
            var group = ShowsEveryone(input, state, view.Page) || view.EveryoneOn;   // Meals draws its own group view (MealsModel.cs)
            if (group && !(view.Active == Chapter.Battle && view.Page == "damage")) return Greyed(p, CompareEveryoneOff);
            if (!w.HasValue || !IsDayWindow(w.Value)) return Greyed(p, CompareNeedsDay);
            var n = DaysOf(w.Value);
            var today = LocalToday(input);
            p.To = today; p.From = today.AddDays(1 - n); p.BeforeTo = p.From.AddDays(-1); p.BeforeFrom = p.From.AddDays(-n);
            if (2 * n > DayHistory.DayRows)   // the pair would reach past the day rows (they fold into weeks after DayRows days): never fair
                return Greyed(p, "Your history keeps single days for " + DayHistory.DayRows + " days, so " + Against(w.Value) + " cannot be shown. Pick Today or 7 days.");
            if (input.History == null) return Greyed(p, "Your day history is not loaded yet.");
            var first = input.History.FirstDay(today);
            var from = DayRowsFrom(input.History, today);
            var counter = CompareCounter(input, view.Active, view.Page);
            var kept = counter == null ? from : input.History.KeptFrom(counter, today);
            if (kept > from) from = kept;
            if (p.BeforeFrom < from)
            {
                p.OpensOn = from.AddDays(2 * n - 1);
                var since = from > first ? "This page is counted per day from " + ShortDate(from, today) : "Your history on this PC starts on " + ShortDate(first, today);
                return Greyed(p, "Compare opens on " + ShortDate(p.OpensOn.Value, today) + ". " + since + "; " + Against(w.Value) +
                                 (w == TimeWindow.Today ? " needs yesterday too." : " needs " + 2 * n + " full days."));
            }
            if (sw != null || group)   // Together, and Everyone's Battle > Damage: every fellow who shares carries their damage dealt per day (the last 30 days of their copy) back to the period before
            {
                var shortOf = FiresidePeople(input).Where(f => !f.Input.IsSelf).Where(f => f.Input.DealtByDay == null ||
                    Local(input, f.Input.LastRecordedUtc ?? f.Input.ReceivedUtc ?? input.NowUtc).Date.AddDays(-29) > p.BeforeFrom).Select(f => f.Name).ToList();
                if (shortOf.Count > 0)
                    return Greyed(p, "Compare needs each player's days back to " + ShortDate(p.BeforeFrom, today) + ": " + JoinNames(shortOf) + (shortOf.Count == 1 ? "'s copy does" : "'s copies do") + " not reach that far.");
            }
            p.Open = true;
            return p;
        }

        // ---------- the period before ----------

        /// <summary>
        /// Your input as it stood at the end of the period before a day window: the clock N days back (adjusted by the hour so its local day is
        /// exactly N days earlier across a clock change), and nothing of this session (the running session's unsaved part, its log, its armour
        /// minutes): InWindow then sums that period's day rows, every page reads it as it reads a day window.
        /// </summary>
        public static PanelInput BeforeInput(PanelInput input, TimeWindow w)
        {
            var n = DaysOf(w);
            var target = LocalToday(input).AddDays(-n);
            var c = input.ShallowCopy();
            c.NowUtc = input.NowUtc.AddDays(-n);
            for (int k = 0; k < 4 && LocalToday(c) != target; k++) c.NowUtc = c.NowUtc.AddHours(LocalToday(c) > target ? -1 : 1);
            c.Pending = null; c.ArmourPending = null; c.ArmourSession = null; c.ArmourMinutes = null;
            c.Log = new EventLog(); c.Session = new DamageTally(); c.SessionOnly = new SessionEvents(); c.Deeds = null;
            return c;
        }

        // one view of the period before is kept while the page asks for the same one (the book redraws every 2 s; that period's rows do not change)
        static string compareKey; static PanelView compareBefore; static DayHistory compareHistory;
        /// <summary>How many times the period before was built (Dev and the tests: once per page while nothing it reads changed).</summary>
        public static int CompareBeforeBuilds { get; private set; }

        static string CompareCacheKey(PanelInput input, PanelState state, PanelView view, ComparePeriods p)
        {
            var b = new StringBuilder();
            b.Append(view.Active).Append('|').Append(view.Page).Append('|').Append(state.Player).Append('|').Append(p.Window).Append('|').Append(DayHistory.DayKey(p.To))
             .Append('|').Append(input.History?.Rows.Count).Append('|').Append(input.PlayerName);
            foreach (var kv in state.View.OrderBy(kv => kv.Key, StringComparer.Ordinal)) b.Append('|').Append(kv.Key).Append('=').Append(kv.Value);
            foreach (var kv in state.Facets.OrderBy(kv => kv.Key, StringComparer.Ordinal)) b.Append('|').Append(kv.Key).Append('=').Append(string.Join(",", kv.Value.ToArray()));
            foreach (var f in state.OpenFilters.OrderBy(x => x, StringComparer.Ordinal)) b.Append("|open=").Append(f);
            b.Append('|').Append(state.FilterRow).Append(',').Append(state.FilterCursor);
            if (view.Active == Chapter.Company) foreach (var f in input.Fellows ?? new List<PanelInput>()) b.Append('|').Append(f.PlayerName).Append(f.LastRecordedUtc?.Ticks).Append(',').Append(f.ReceivedUtc?.Ticks);
            return b.ToString();
        }

        /// <summary>The page built for the period before (the same page, its views and filters as chosen now), kept while nothing it reads changed.</summary>
        static PanelView BeforeView(PanelInput input, PanelState state, PanelView view, ComparePeriods p)
        {
            var key = CompareCacheKey(input, state, view, p);
            if (compareBefore != null && key == compareKey && ReferenceEquals(compareHistory, input.History)) return compareBefore;
            var s = (PanelState)CopyOf.Invoke(state, null);
            s.Compare = false; s.CompareTip = false; s.WaitAt = null; s.WaitView = null;
            var before = BuildView(BeforeInput(input, p.Window), s);
            CompareBeforeBuilds++;
            compareKey = key; compareBefore = before; compareHistory = input.History;
            return before;
        }

        /// <summary>The last error a comparison ran into (Dev.SelfCheck lists it; null: none). The page then shows as it is without Compare.</summary>
        public static string CompareError { get; private set; }

        /// <summary>Build's last step: the comparison when the page has one; any error in it gives the plain page (built again), never a broken one.</summary>
        static PanelView CompareOrPlain(PanelInput input, PanelState state, PanelView view)
        {
            try { CompareFinish(input ?? new PanelInput(), state ?? new PanelState(), view); return view; }
            catch (Exception e) { CompareError = e.GetType().Name + ": " + e.Message; compareBefore = null; compareKey = null; return BuildView(input, state); }
        }

        // ---------- the merge ----------

        sealed class CompareCtx { public string BeforeLabel; public PanelInput Now, Before; public TimeWindow Window; }

        /// <summary>
        /// Build's last step on a page that offers Compare: the chip (view.Compare, or Together's own beside its window chips), its reason when
        /// greyed, and, while it is on and open, the page merged with the period before (the plate's first line names both periods).
        /// </summary>
        static void CompareFinish(PanelInput input, PanelState state, PanelView view)
        {
            var p = CompareOf(input, state, view);
            if (p == null) return;
            var on = state.Compare && p.Open;
            var chip = new Choice { Id = CompareId, Label = CompareLabel, Selected = on, Disabled = !p.Open };
            var sw = TogetherDealtSwitch(TogetherOf(view));
            if (sw != null && view.Active == Chapter.Company) sw.Chip = new Block { Kind = "chip", Id = CompareId, Title = CompareLabel, Selected = on, Tone = p.Open ? null : OffTone, Text = p.Open ? null : p.Why, Open = state.CompareTip && !p.Open };
            else { view.Compare = chip; view.CompareWhy = p.Open ? null : p.Why; view.CompareTip = state.CompareTip && !p.Open; }
            if (!on) return;
            var before = BeforeView(input, state, view, p);
            var x = new CompareCtx { BeforeLabel = BeforeLabel(p.Window), Now = input, Before = BeforeInput(input, p.Window), Window = p.Window };
            view.Comparing = true;
            BattleHeroes(view.Blocks, input, p.Window); var beforeBlocks = CloneTop(before.Blocks); BattleHeroes(beforeBlocks, x.Before, p.Window);
            CompareMerge(view.Blocks, beforeBlocks, x);
            // 0.8: the growth line steps aside while comparing (the hero row holds the number before instead; the compare rows say more)
            foreach (var h in Content(view).Where(b => b.Kind == "hero")) h.Items?.RemoveAll(i => i.Kind == SparkKind);
            var plate = PlateOf(view);
            if (plate != null)
            {
                var line = CompareLine(p, LocalToday(input));
                plate.Text = string.IsNullOrEmpty(plate.Text) ? line : line + "\n" + plate.Text;
                // nothing this period, something before: one line says what the period before held (the page's empty state stays)
                var empty = plate.Items.FirstOrDefault(b => b.Kind == "empty");
                var hero = Content(before).FirstOrDefault(b => b.Kind == "hero");
                if (empty != null && hero != null && !plate.Items.Any(b => b.Kind == "hero"))
                    plate.Items.Insert(plate.Items.IndexOf(empty) + 1, new Block { Kind = "note", Text = Cap(x.BeforeLabel) + ": " + hero.Value + " " + hero.Title + "." });
                if (input.History != null && input.History.Rows.Any(r => r.IsDay && r.Clipped && r.Start >= p.BeforeFrom && r.Start <= p.BeforeTo))
                    plate.Items.Add(new Block { Kind = "note", Text = "A very full day in " + x.BeforeLabel + " kept only its largest kinds: a few small ones may be missing there." });
            }
        }

        /// <summary>A shallow copy of the before view's top blocks down to the switches' views, so BattleHeroes can swap a block in them without
        /// touching the kept view.</summary>
        static List<Block> CloneTop(List<Block> blocks) => blocks?.Select(b => b == null ? null : IsBox(b) ? Shallow(b, CloneTop(b.Items)) : b).ToList();
        static Block Shallow(Block b, List<Block> items) { var c = (Block)CopyOf.Invoke(b, null); c.Items = items; return c; }

        /// <summary>Battle > Damage while comparing: each view's one-answer line ("Melee damage, the most of any weapon"; By foe's too) becomes the period's
        /// damage dealt as the hero (PICKS B: "3 120 damage dealt ▲ 29 %"), worked out from the window's rows as the type table does.</summary>
        static void BattleHeroes(List<Block> blocks, PanelInput input, TimeWindow w)
        {
            if (blocks == null) return;
            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i]; if (b == null) continue;
                if (IsBox(b)) { BattleHeroes(b.Items, input, w); continue; }
                if (b.Kind == "stat" && blocks.Any(o => o != null && (o.Kind == "dmgmix" || o.Kind == "damagegrid" || o.Kind == "dealtfoes")))
                {
                    var c = InWindow(input, w);
                    var dealt = c == null ? 0 : DealtRows(DamageSinceInstallRows(c)).Sum(r => (double)r.Amount);
                    blocks[i] = new Block { Kind = "hero", Value = NAtLeast(dealt), Title = "damage dealt", Src = b.Src, Source = b.Source };
                }
                else if (b.Kind == "guard")   // Defence: blocks and parries as the hero's two numbers
                    blocks[i] = new Block { Kind = "hero", Value = b.Value, Title = b.Title, Src = b.Src, Source = b.Source,
                                            Items = new List<Block> { new Block { Kind = "number", Value = b.Value2, Title = b.Text, Src = b.Src, Source = b.Source } } };
            }
        }

        static string KeyOf(Block b, string section, Dictionary<string, int> seen)
        {
            var k = b.Kind + "|" + (b.Kind == "hero" || b.Kind == "column" || b.Kind == "plate" ? "" : (b.Id ?? "") + "|" + Loose(b.Kind == "view" || b.Kind == "switch" ? "" : b.Title)) + "|" + Loose(section);
            seen.TryGetValue(k, out var n); seen[k] = n + 1;
            return k + "#" + n;
        }

        static Dictionary<string, Block> IndexOf(List<Block> blocks)
        {
            var d = new Dictionary<string, Block>(); var seen = new Dictionary<string, int>(); string section = null;
            foreach (var b in blocks ?? new List<Block>())
            {
                if (b == null) continue;
                if (b.Kind == "section") section = b.Title;
                d[KeyOf(b, section, seen)] = b;
            }
            return d;
        }

        /// <summary>The merge alone, as a compared page does it (the tests' way in): the period before's blocks into now's.</summary>
        public static void CompareBlocks(List<Block> now, List<Block> before, TimeWindow w) => CompareMerge(now, before, new CompareCtx { BeforeLabel = BeforeLabel(w), Window = w });

        /// <summary>Merges the period before into the blocks of now, in place: each block finds its twin by kind, id, title and the section it
        /// stands in (CompareBlock says what each kind becomes). A block with no twin had nothing in the period before.</summary>
        static void CompareMerge(List<Block> now, List<Block> before, CompareCtx x)
        {
            if (now == null) return;
            var twins = IndexOf(before); var seen = new Dictionary<string, int>(); string section = null;
            for (int i = 0; i < now.Count; i++)
            {
                var b = now[i]; if (b == null) continue;
                if (b.Kind == "section") section = b.Title;
                if (b.Kind == "dmgmix")   // By weapon: the weapons' bars become one row per weapon (their types: By type)
                {
                    var run = now.Skip(i).TakeWhile(o => o != null && o.Kind == "dmgmix").ToList();
                    var was = (before ?? new List<Block>()).Where(o => o != null && o.Kind == "dmgmix").ToList();
                    now.RemoveRange(i, run.Count);
                    now.Insert(i, RowsOf(new Block { Kind = "dmgmix", Text = ByWeapon, Src = b.Src, Source = b.Source, Items = run }, new Block { Kind = "dmgmix", Items = was }, x, RowLook.Weapon, head: ByWeapon));
                    continue;
                }
                var key = KeyOf(b, section, seen);
                twins.TryGetValue(key, out var p);
                now[i] = CompareBlock(b, p, x);
                if (b.Kind == "dealtfoes" && now[i].Kind == CompareRowsKind) { now.Insert(i + 1, new Block { Kind = "note", Text = CompareByFoeLine }); i++; }
                // a pair of half columns with compare rows in them goes full width, one after the other: four columns of numbers need the room
                if (now[i].Kind == "columns" && Unfold(now[i]) is List<Block> flat) { now.RemoveAt(i); now.InsertRange(i, flat); i += flat.Count - 1; }
            }
        }

        static List<Block> Unfold(Block columns)
        {
            var cols = (columns.Items ?? new List<Block>()).Where(c => c != null && c.Kind == "column").ToList();
            if (!cols.Any(c => (c.Items ?? new List<Block>()).Any(i => i != null && i.Kind == CompareRowsKind))) return null;
            return cols.SelectMany(c => c.Items ?? new List<Block>()).Where(i => i != null).ToList();
        }

        enum RowLook { Part, Thing, Type, Weapon, Person, Segment }

        static Block CompareBlock(Block b, Block p, CompareCtx x)
        {
            switch (b.Kind)
            {
                case "plate": case "column": case "columns": case "view":
                    CompareMerge(b.Items, p?.Items, x); return b;
                case "switch":
                    if (b.Items != null && b.Items.Any(v => v.Kind == "view" && v.Id == ArmourId && v.Selected))   // Your armour: its ledger began later, shown as it is
                    {
                        var view = b.Items.First(v => v.Id == ArmourId);
                        if (view.Items != null && !view.Items.Any(i => i.Kind == "note" && i.Text == CompareArmourLine)) view.Items.Insert(0, new Block { Kind = "note", Text = CompareArmourLine });
                        return b;
                    }
                    CompareMerge(b.Items, p?.Items, x); return b;
                case "hero":
                    Mark(b, p, x);
                    foreach (var n in (b.Items ?? new List<Block>()).Where(n => n.Kind == "number"))
                        Mark(n, p?.Items?.FirstOrDefault(q => q.Kind == "number" && Loose(q.Title) == Loose(n.Title)), x);
                    return b;
                case "section":
                    if (!string.IsNullOrEmpty(b.Value)) Mark(b, p, x);
                    return b;
                case "composition": return RowsOf(b, p, x, NumbersOnBar(b) ? RowLook.Type : RowLook.Part);
                case "ranking": case "counts": case "strip": case "rows": return RowsOf(b, p, x, RowLook.Thing);
                case "crew": return RowsOf(b, p, x, RowLook.Person);
                case "sources":   // 0.8: the rows carry no "×N", so the caption drops the date the counts run from (BattleFeedModel.CountsFrom)
                {
                    var rows = RowsOf(b, p, x, RowLook.Thing, total: true);
                    var at = rows.Kind == CompareRowsKind && rows.Note != null ? rows.Note.IndexOf("counts from ", StringComparison.Ordinal) : -1;
                    if (at >= 0) rows.Note = rows.Note.Substring(0, at).TrimEnd(' ', '·');
                    return rows;
                }
                case "dealtfoes": return RowsOf(b, p, x, RowLook.Thing, parts: q => (q.Items ?? new List<Block>()).Where(i => i.Kind == "source"));   // 0.8 By foe: the damage per foe kind (no cap total: it is the hero)
                case GroupRowsKind: return RowsOf(b, p, x, RowLook.Person, parts: q => (q.Items ?? new List<Block>()).Where(i => i.Kind == GroupMemberKind));   // 0.8 Everyone: a row per player
                case "journey": return RowsOf(b, p, x, RowLook.Segment, parts: j => (j.Items ?? new List<Block>()).SelectMany(l => l.Items ?? new List<Block>()));
                case "damagegrid": return RowsOf(b, p, x, RowLook.Type, parts: g => (g.Items ?? new List<Block>()).Where(t => t.Kind == "dmgtype"), head: ByType, keepCap: false);
                case "together": TogetherCompared(b, p, x); return b;
                case "filterbar":   // the filter's bars (By category, By type): compare rows beside them, which the filter draws in their place
                    b.Items?.RemoveAll(i => i.Kind == CompareRowsKind);
                    foreach (var bar in (b.Items ?? new List<Block>()).Where(i => i.Kind == "facetbar" && i.Tone != "hidden").ToList())
                    {
                        var twin = p?.Items?.FirstOrDefault(i => i.Kind == "facetbar" && i.Id == bar.Id);
                        var rows = RowsOf(bar, twin ?? new Block { Kind = "facetbar", Src = bar.Src }, x, RowLook.Part, keepCap: false);
                        if (rows.Kind != CompareRowsKind) continue;
                        rows.Id = bar.Id; rows.Text = bar.Title;
                        b.Items.Insert(b.Items.IndexOf(bar) + 1, rows);
                    }
                    return b;
                default: return b;
            }
        }

        /// <summary>A number of now gets the number before it and the change (Before, Change, BeforeLabel). Not when either side is not recorded,
        /// nor when the two come from different sources (never a game total beside a count from install); false then.</summary>
        static bool Mark(Block b, Block p, CompareCtx x)
        {
            if (b == null || b.Unrecorded || (p != null && p.Unrecorded) || string.IsNullOrEmpty(b.Value)) return false;
            if (p != null && !string.IsNullOrEmpty(b.Src) && !string.IsNullOrEmpty(p.Src) && b.Src != p.Src) return false;
            var now = Amount(b.Value, out var kn);
            var before = p == null || string.IsNullOrEmpty(p.Value) ? ZeroLike(b.Value) : p.Value;
            var then = Amount(before, out var kb);
            if (double.IsNaN(now) || double.IsNaN(then) || kn != kb) return false;
            b.Before = before; b.Change = ChangeMark(now, then); b.BeforeLabel = x.BeforeLabel;
            return true;
        }

        static string RowKey(Block part) => !string.IsNullOrEmpty(part.Id) ? part.Id : part.Title ?? "";

        /// <summary>
        /// A list of parts as compare rows on one scale (PICKS B): one row per part of either period, this period's order first, then what only the
        /// period before had (largest first); each row now, before and the change, Fraction = now / the block's largest, Fraction2 = before / the
        /// same. The cap keeps the block's title and total (with its own change) and its qualifier. A block whose numbers are not amounts, or that
        /// is not recorded, stays as it is.
        /// </summary>
        static Block RowsOf(Block b, Block p, CompareCtx x, RowLook look, Func<Block, IEnumerable<Block>> parts = null, string head = null, bool keepCap = true, bool total = false)
        {
            parts = parts ?? (q => q.Items ?? new List<Block>());
            var now = parts(b).Where(q => q != null).ToList();
            var was = p == null ? new List<Block>() : parts(p).Where(q => q != null).ToList();
            if (now.Count == 0 || now.Any(q => q.Unrecorded) || was.Any(q => q.Unrecorded)) return b;
            if (p != null && !string.IsNullOrEmpty(b.Src) && !string.IsNullOrEmpty(p.Src) && b.Src != p.Src) return b;
            string kind = null;
            bool Fits(Block q) { var v = Amount(q.Value, out var k); if (double.IsNaN(v)) return false; if (v > 0 || (q.Value ?? "").Trim().Length > 0) { if (kind == null) kind = k; else if (kind != k) return false; } return true; }
            if (!now.All(Fits) || !was.All(Fits)) return b;
            var byKey = was.GroupBy(RowKey).ToDictionary(g => g.Key, g => g.First());
            var keys = now.Select(RowKey).Distinct().ToList();
            keys.AddRange(was.Where(q => !keys.Contains(RowKey(q))).OrderByDescending(q => Amount(q.Value)).Select(RowKey).Distinct());
            var nowBy = now.GroupBy(RowKey).ToDictionary(g => g.Key, g => g.First());
            var rows = new List<(Block part, double n, double v, string nv, string bv)>();
            foreach (var k in keys)
            {
                nowBy.TryGetValue(k, out var a); byKey.TryGetValue(k, out var o);
                var shape = a ?? o;
                string nv = a != null && !string.IsNullOrEmpty(a.Value) ? a.Value : ZeroLike(o?.Value ?? a?.Value), bv = o != null && !string.IsNullOrEmpty(o.Value) ? o.Value : ZeroLike(nv);
                double n = Amount(nv), v = Amount(bv);
                if (n <= 0 && v <= 0) continue;
                rows.Add((shape, n, v, nv, bv));
            }
            if (rows.Count == 0) return b;
            if (rows.Count > CompareMaxRows)   // the smallest past the ninth as one quiet row, summed when the amounts are counts
            {
                var rest = rows.Skip(CompareMaxRows - 1).ToList(); rows = rows.Take(CompareMaxRows - 1).ToList();
                double rn = rest.Sum(r => r.n), rv = rest.Sum(r => r.v);
                var counts = kind == null || kind == "count";
                rows.Add((new Block { Id = FoldId, Title = "Other (" + rest.Count + " kinds)", Colour = BarOtherColour }, rn, rv, counts ? NAtLeast(rn) : "", counts ? NAtLeast(rv) : ""));
            }
            var max = rows.Max(r => Math.Max(r.n, r.v));
            var items = rows.Select(r =>
            {
                var q = r.part; var other = q.Id == FoldId;
                var colour = other ? BarOtherColour : look == RowLook.Person ? "person:" + q.Id : look == RowLook.Weapon || (look == RowLook.Thing && !(q.Tone == "type" && !string.IsNullOrEmpty(q.Colour))) ? CompareRowColour : !string.IsNullOrEmpty(q.Colour) ? q.Colour : CompareRowColour;
                var icon = other ? null : look == RowLook.Part || look == RowLook.Segment ? null : look == RowLook.Person ? "person:" + q.Id : q.Icon;
                return new Block
                {
                    Kind = "row", Id = q.Id, Title = q.Title, Icon = icon, Colour = colour, Pattern = look == RowLook.Part && !other ? q.Pattern : null, Tone = look == RowLook.Thing && q.Tone == "type" ? "type" : null,
                    Value = r.nv, Before = r.bv, Change = ChangeMark(r.n, r.v), Fraction = max > 0 ? (float)(r.n / max) : 0f, Fraction2 = max > 0 ? (float)(r.v / max) : 0f, Src = q.Src ?? b.Src, Source = q.Source ?? b.Source,
                };
            }).ToList();
            var cmp = new Block { Kind = CompareRowsKind, Id = b.Id, Icon = keepCap && b.Kind != "dmgmix" ? b.Icon : null, Title = keepCap ? b.Title : null, Value = keepCap ? b.Value : null, Note = keepCap ? (b.Kind == "sources" ? b.Text : b.Note) : null,
                                  Text = head ?? (keepCap ? null : b.Title), Src = b.Src, Source = b.Source, Items = items };
            if (total && keepCap && string.IsNullOrEmpty(cmp.Value) && kind != "time") cmp.Value = NAtLeast(rows.Sum(r => r.n));   // the foes' sum, as the cap's total
            if (!string.IsNullOrEmpty(cmp.Value) && keepCap)
            {
                var then = p?.Value;
                if (total && string.IsNullOrEmpty(then)) then = NAtLeast(rows.Sum(r => r.v));
                Mark(cmp, new Block { Value = then, Src = p?.Src }, x);
            }
            return cmp;
        }

        /// <summary>Company > Together under Damage dealt: the group's total gets its number before and the change, and the players' bar becomes
        /// one compare row per player in their colours (the together block carries them; CompanyUi draws them where the bar was). The share rows
        /// under it stay this period's.</summary>
        static void TogetherCompared(Block b, Block p, CompareCtx x)
        {
            var cat = (b.Items ?? new List<Block>()).FirstOrDefault(c => c.Kind == "category" && c.Id == "dealt" && c.Selected);
            if (cat == null) return;
            var was = (p?.Items ?? new List<Block>()).FirstOrDefault(c => c.Kind == "category" && c.Id == "dealt");
            Mark(cat, was, x);
            var rows = RowsOf(new Block { Kind = "people", Src = cat.Src, Source = cat.Source, Items = cat.Items }, was == null ? null : new Block { Kind = "people", Src = was.Src, Items = was.Items }, x, RowLook.Person, keepCap: false);
            if (rows.Kind != CompareRowsKind) return;
            rows.Id = "dealt";
            b.Items.RemoveAll(i => i.Kind == CompareRowsKind);
            b.Items.Insert(b.Items.IndexOf(cat) + 1, rows);
        }

        /// <summary>Every player-visible string the comparison adds (for AllText).</summary>
        static IEnumerable<string> CompareText(PanelView v) => new[] { v.Compare?.Label, v.CompareWhy };
    }
}
