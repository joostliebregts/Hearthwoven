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
    /// 0.8.5 (work/hearthwoven-0.8.5/proto/4-compare, A and C; Joost: "I don't actually know what it's showing me and how I compare to something
    /// else"): the page's title says the pair ("Woodcutting, today against yesterday"), the strip a one-line legend drawn as the bars are ("solid
    /// bar: today", "thin outline: yesterday"), and a greyed chip in a day window says when it opens ("Compare opens 14 Oct") with its reason as one
    /// line in the strip. The chip opens a small menu of what to compare against, kept per page (PanelState.CompareWith, PanelPrefs): the stretch
    /// just before (yesterday, the 7 before), the same stretch earlier (the same day last week; the same 7 days 4 weeks back, as far as the day rows
    /// reach), or your best (the day, or the 7 days, with the most of the page's hero number in your day history), and Off. An option the history
    /// does not reach yet is greyed with its reason; a chosen option that is greyed shows the stretch just before until it opens.
    /// </summary>
    public static partial class PanelModel
    {
        public const string CompareId = "compare", CompareLabel = "Compare", CompareRowsKind = "comparerows";
        /// <summary>The click target of Together's Compare chip (Follow): on and off.</summary>
        public const string CompareTarget = "compare:toggle";
        /// <summary>0.8.5: the click target of the heading's Compare chip (Follow): its menu opens and closes.</summary>
        public const string CompareMenuTarget = "compare:menu";
        /// <summary>0.8.5: a click outside the open menu (Follow): the menu closes, nothing else changes (never opens it, unlike the chip).</summary>
        public const string CompareCloseTarget = "compare:close";
        /// <summary>0.8.5: a menu option's click target: this, the page ("Deeds/woodcutting"), "=", the option's id.</summary>
        public const string ComparePickTarget = "compare:pick=";
        /// <summary>0.8.5: what a page compares against: the stretch just before (the default), the same stretch earlier, your best; Off ends it.</summary>
        public const string CompareLastId = "last", CompareSameId = "same", CompareBestId = "best", CompareOffId = "off";
        /// <summary>0.8.5: the menu under the chip (PanelView.CompareMenu) and its rows.</summary>
        public const string CompareMenuKind = "comparemenu", CompareOptionKind = "compareoption", CompareOffLabel = "Off";
        /// <summary>0.8.5: the legend in the strip, drawn as the bars are: "solid bar: today", "thin outline: yesterday".</summary>
        public const string CompareSolidWords = "solid bar:", CompareOutlineWords = "thin outline:";
        /// <summary>0.8.5: the key line's entry while the menu is open (W/S walk its options, Enter picks one).</summary>
        public const string CompareMenuKeys = "[W/S·Enter] Choose";
        /// <summary>The change mark's arrows and words (never green or red: the mark does not judge).</summary>
        public const string CompareUp = "▲ ", CompareDown = "▼ ", CompareNew = "new", CompareSame = "same", CompareUnderOne = "<1 %";
        /// <summary>The bar colour of a row that is a thing, not an item or a damage type (a tree, a weapon, a foe): the ranking's gold.</summary>
        public const string CompareRowColour = "#e8a948";
        /// <summary>At most this many rows per block; the rest as "Other (n kinds)", as the bar form folds.</summary>
        public const int CompareMaxRows = 9;

        /// <summary>The greyed chip's reason on All, Session and the short windows (Joost, 0.8.1: "needs a day window" confused; plain words now).
        /// 0.8.5: one short line, so its tag under the chip covers little.</summary>
        public const string CompareNeedsDay = "Compare works on Today and 7 days.";
        /// <summary>0.9.1: Yesterday has no pair of its own; Today against yesterday is the pair.</summary>
        public const string CompareYesterday = "Choose Today to compare it with yesterday.";
        public const string CompareOwnBook = "Compare works in your own book.";
        public const string CompareArmourLine = "What your armor stopped is shown for this period only.";
        /// <summary>0.8, with the Everyone chip (PICKS.md section 3: only Battle > Damage By player compares the group, from the fellows' damage
        /// dealt per day): the greyed chip's reason on every other group page. 0.8.5: one line, as the strip shows it.</summary>
        public const string CompareEveryoneOff = "With Everyone on, only Battle > Damage compares: fellow players share totals, not days.";
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
            /// <summary>0.8.5: what it compares against (CompareLastId, CompareSameId, CompareBestId); on CompareOf's answer, every option of the
            /// menu with its own periods (null while the chip is greyed: there is no menu then, or when not asked for). Short: a greyed option's
            /// reason in the menu, where its dates would stand ("opens 15 Oct").</summary>
            public string With = CompareLastId;
            public List<ComparePeriods> Options;
            public string Short;
            /// <summary>0.8.5: a greyed chip's reason as the strip's one line, short enough for it beside the strip's right end (the chip says the
            /// date itself: "Needs 14 full days; your history starts on 1 Oct."); null: none.</summary>
            public string Line;
            /// <summary>How many days the period before ends before this one does: 1 for yesterday, 7 for the same day last week.</summary>
            public int Back => (To - BeforeTo).Days;
        }

        /// <summary>The pages that offer Compare: the day-window pages with addable numbers (Deeds' pages but Overview and Recent, Battle Damage and
        /// Defence, Voyages Sailing and Cargo). Company > Together offers it under Damage dealt (its own window chips; TogetherCompared).</summary>
        public static bool CompareOffered(Chapter c, string page) =>
            c == Chapter.Deeds ? page != null && page != "overview" && page != RecentPageId && !IsDeedFeedPage(page) && WindowsOf(c, page) != null   // 0.9: a deed feed lists, it adds nothing up
            : c == Chapter.Battle ? page == "damage" || page == "defense"
            : c == Chapter.Voyages && (page == "sailing" || page == "cargo");

        /// <summary>"yesterday", "the 7 before": what the number before is, where it stands beside a number now, and the compare rows' column head
        /// over it. 0.8.1 (Joost): the heads name the periods, not "now" and "before".</summary>
        public static string BeforeLabel(TimeWindow w) => CompareWords(w, CompareLastId).label;

        /// <summary>
        /// 0.8.5: an option's words. Menu: its row in the menu ("The same day last week"); Title: the page's title after "against" ("your best
        /// day"); Label: after a number before ("from 20 on your best day", "135 on your best day"); Head: the compare rows' column head.
        /// </summary>
        public static (string menu, string title, string label, string head) CompareWords(TimeWindow w, string with)
        {
            if (w == TimeWindow.Today)
                switch (with)
                {
                    case CompareSameId: return ("The same day last week", "the same day last week", "on the same day last week", "last week");
                    case CompareBestId: return ("Your best day", "your best day", "on your best day", "best day");
                    default: return ("Yesterday", "yesterday", "yesterday", "yesterday");
                }
            var n = DaysOf(w);
            switch (with)
            {
                case CompareSameId: return ("The same " + n + " days last month", "the same " + n + " days last month", "in the same " + n + " days last month", "last month");
                case CompareBestId: return ("Your best " + n + " days", "your best " + n + " days", "in your best " + n + " days", "best " + n + " days");
                default: return ("The " + n + " before", "the " + n + " before", "the " + n + " before", "the " + n + " before");
            }
        }

        /// <summary>0.8.5: the period before, short: "Wed 7 Oct" for a day, "4 to 10 Sep" for more.</summary>
        public static string CompareDates(ComparePeriods p, DateTime today) =>
            p.BeforeFrom == p.BeforeTo ? p.BeforeTo.ToString("ddd ", Inv) + ShortDate(p.BeforeTo, today) : DayRange(p.BeforeFrom, p.BeforeTo, today);

        /// <summary>0.8.5: the legend's thin outline in words: "yesterday", "the 7 before"; an earlier stretch with its dates ("your best day, Sun 4 Oct").</summary>
        public static string CompareThenWords(ComparePeriods p, DateTime today) =>
            p.With == CompareLastId ? CompareWords(p.Window, p.With).title : CompareWords(p.Window, p.With).title + ", " + CompareDates(p, today);

        /// <summary>"today", "these 7 days": the compare rows' column head over the numbers now.</summary>
        public static string NowLabel(TimeWindow w) => w == TimeWindow.Today ? "today" : "these " + DaysOf(w) + " days";

        /// <summary>A hero number's comparison as one line after it (Joost, 0.8.1: "new" sat over "0 yesterday" like a label): a change mark leads,
        /// "▲ 12 % from 86 yesterday"; "new" and "same" follow the number before, "0 yesterday · new". MarkFirst says where the mark goes.</summary>
        public static (bool markFirst, string words) HeroCompare(Block n)
        {
            var then = n.Before + (string.IsNullOrEmpty(n.BeforeLabel) ? "" : " " + n.BeforeLabel);
            return ChangeParts(n.Change).dir != 0 ? (true, "from " + then) : (false, then + " ·");
        }

        /// <summary>"7 days against the 7 before", "today against yesterday".</summary>
        static string Against(TimeWindow w) => w == TimeWindow.Today ? "today against yesterday" : DaysOf(w) + " days against the " + DaysOf(w) + " before";

        /// <summary>0.8.5: the pair in the page's title, after the page's name and window: "today against yesterday", "last 7 days against your best 7 days".</summary>
        // 0.9.1: the same stretch earlier says its short head in the title ("today against last week", "last 7 days against last month"): with eleven
        // window chips the full words did not fit beside them; the strip's legend still names it in full with its dates (CompareThenWords).
        public static string ComparePair(string window, ComparePeriods p) => window + " against " + (p.With == CompareSameId ? CompareWords(p.Window, p.With).head : CompareWords(p.Window, p.With).title);

        /// <summary>A day range, short: "4 to 10 Oct", "27 Sep to 3 Oct"; one day: "10 Oct".</summary>
        public static string DayRange(DateTime from, DateTime to, DateTime today)
        {
            if (from.Date == to.Date) return ShortDate(from, today);
            var head = from.Year != to.Year || from.Year != today.Year ? ShortDate(from, today) : from.Month == to.Month ? from.Day.ToString(Inv) : from.ToString("d MMM", Inv);
            return head + " to " + ShortDate(to, today);
        }

        /// <summary>The plate's first line while comparing: "7 days, 4 to 10 Oct, against the 7 days before, 27 Sep to 3 Oct." (0.8.5: "against your
        /// best day, 4 Oct"; About these numbers holds it, under "Compared").</summary>
        public static string CompareLine(ComparePeriods p, DateTime today)
        {
            var title = p.With == CompareLastId && p.Window != TimeWindow.Today ? "the " + DaysOf(p.Window) + " days before" : CompareWords(p.Window, p.With).title;
            return p.Window == TimeWindow.Today ? "Today, " + ShortDate(p.To, today) + ", against " + title + ", " + ShortDate(p.BeforeTo, today) + "."
                : DaysOf(p.Window) + " days, " + DayRange(p.From, p.To, today) + ", against " + title + ", " + DayRange(p.BeforeFrom, p.BeforeTo, today) + ".";
        }

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

        static ComparePeriods Greyed(ComparePeriods p, string why, string line = null) { p.Open = false; p.Why = why; p.Line = line; return p; }

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
                // the window Together shows (a greyed day window pressed shows All): Compare needs a period before it, so not on All
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
            if (!input.IsSelf) return Greyed(p, (input.DayDeeds != null ? Name(input) + "'s copy shares a few numbers per day, not the day history Compare reads. "   // 0.8.5: their Deeds pages have days too
                                                 : input.DayDealt != null ? Name(input) + "'s copy shares damage dealt per day, not the day history Compare reads. "   // 0.9: their Damage has days now
                                                                         : Name(input) + " shares totals, not days, so there is no earlier stretch to set beside this one. ") + CompareOwnBook, CompareOwnBook);
            // 0.8 the Everyone chip: the group's page compares only on Battle > Damage, By player (each fellow's damage dealt per day)
            var group = ShowsEveryone(input, state, view.Page) || view.EveryoneOn;   // Meals draws its own group view (MealsModel.cs)
            if (group && !(view.Active == Chapter.Battle && view.Page == "damage")) return Greyed(p, CompareEveryoneOff, "With Everyone on, only Battle > Damage compares.");
            if (!w.HasValue || !IsDayWindow(w.Value)) return Greyed(p, CompareNeedsDay);
            if (w == TimeWindow.Yesterday) return Greyed(p, CompareYesterday, CompareYesterday);   // 0.9.1
            var n = DaysOf(w.Value);
            var today = LocalToday(input);
            p.To = today; p.From = today.AddDays(1 - n); p.BeforeTo = p.From.AddDays(-1); p.BeforeFrom = p.From.AddDays(-n);
            if (2 * n > DayHistory.DayRows)   // the pair would reach past the day rows (they fold into weeks after DayRows days): never fair
                return Greyed(p, "Your history keeps single days for " + DayHistory.DayRows + " days, so " + Against(w.Value) + " cannot be shown. Pick Today or 7 days.",
                              "Single days are kept for " + DayHistory.DayRows + " days: pick Today or 7 days.");
            if (input.History == null) return Greyed(p, "Your day history is not loaded yet.", "Your day history is not loaded yet.");
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
                                 (w == TimeWindow.Today ? " needs yesterday too." : " needs " + 2 * n + " full days."),
                              (w == TimeWindow.Today ? "Needs yesterday too" : "Needs " + 2 * n + " full days") + "; " + (from > first ? "this page counts days from " : "your history starts on ") + ShortDate(from, today) + ".");
            }
            if (sw != null || group)   // Together, and Everyone's Battle > Damage: every fellow who shares carries their damage dealt per day (the last 30 days of their copy) back to the period before
            {
                var shortOf = ShortOf(input, p.BeforeFrom);
                if (shortOf.Count > 0)
                    return Greyed(p, "Compare needs each player's days back to " + ShortDate(p.BeforeFrom, today) + ": " + JoinNames(shortOf) + (shortOf.Count == 1 ? "'s copy does" : "'s copies do") + " not reach that far.",
                                  JoinNames(shortOf) + (shortOf.Count == 1 ? "'s copy does" : "'s copies do") + " not reach back to " + ShortDate(p.BeforeFrom, today) + ".");
            }
            p.Open = true;
            return sw != null ? p : Choose(input, state, view, p, from, group);   // Together's own chip turns on and off; the heading's chip has the menu
        }

        // the fellows whose shared damage per day (the last 30 days of their copy) does not reach back to a day
        static List<string> ShortOf(PanelInput input, DateTime from) => FiresidePeople(input).Where(f => !f.Input.IsSelf).Where(f => f.Input.DealtByDay == null ||
            Local(input, f.Input.LastRecordedUtc ?? f.Input.ReceivedUtc ?? input.NowUtc).Date.AddDays(-29) > from).Select(f => f.Name).ToList();

        /// <summary>0.8.5: the page whose comparison choice this is ("Deeds/woodcutting"), as PanelState.CompareWith keeps it.</summary>
        public static string ComparePageOf(PanelView view) => view.Active + "/" + view.Page;

        /// <summary>0.8.5: what this page compares against as chosen (CompareLastId when nothing was chosen).</summary>
        public static string CompareChoice(PanelState s, PanelView view) => s.CompareWith.TryGetValue(ComparePageOf(view), out var w) && Array.IndexOf(CompareChoices, w) >= 0 ? w : CompareLastId;
        static readonly string[] CompareChoices = { CompareLastId, CompareSameId, CompareBestId };

        // an option's two periods: the period now as the default's, the period before ending back days before it ends
        static ComparePeriods OptionOf(ComparePeriods last, string with, int back)
        {
            var n = DaysOf(last.Window);
            var o = new ComparePeriods { Window = last.Window, From = last.From, To = last.To, With = with, Open = true };
            o.BeforeTo = last.To.AddDays(-back); o.BeforeFrom = o.BeforeTo.AddDays(1 - n);
            return o;
        }

        /// <summary>
        /// 0.8.5: the page's comparison as chosen (the default when its choice is greyed) with every option of the menu: the stretch just before,
        /// the same stretch earlier (a day: 7 days back; 7 days: 28 days back, so the weekdays match and the day rows still hold it), and your best.
        /// The best is worked out only when it is chosen or the menu is open (it builds the page once per day of the history: BestOf).
        /// </summary>
        static ComparePeriods Choose(PanelInput input, PanelState state, PanelView view, ComparePeriods last, DateTime from, bool group)
        {
            var n = DaysOf(last.Window); var today = last.To;
            var asked = CompareChoice(state, view);
            var same = OptionOf(last, CompareSameId, n == 1 ? 7 : 28);
            if (same.BeforeFrom < from)
            {
                same.Open = false; same.OpensOn = from.AddDays(same.Back + n - 1);
                same.Short = "opens " + ShortDate(same.OpensOn.Value, today);
                same.Why = CompareWords(last.Window, CompareSameId).menu + " opens on " + ShortDate(same.OpensOn.Value, today) + ": your day history does not reach " + ShortDate(same.BeforeFrom, today) + " yet.";
            }
            else if (group && ShortOf(input, same.BeforeFrom) is List<string> shortOf && shortOf.Count > 0)
            {
                same.Open = false; same.Short = "copies too short";
                same.Why = "Compare needs each player's days back to " + ShortDate(same.BeforeFrom, today) + ": " + JoinNames(shortOf) + (shortOf.Count == 1 ? "'s copy does" : "'s copies do") + " not reach that far.";
            }
            ComparePeriods best = null;
            if (group) { best = OptionOf(last, CompareBestId, n); best.Open = false; best.Short = "your own book only"; best.Why = "Your best day is read from your own day history, not the group's."; }
            else if (asked == CompareBestId || state.CompareMenu) best = BestOf(input, state, view, last, from);
            var options = new List<ComparePeriods> { last, same };
            if (best != null) options.Add(best);
            var pick = options.FirstOrDefault(o => o.With == asked && o.Open) ?? last;
            pick.Options = options;
            return pick;
        }

        /// <summary>
        /// 0.8.5: your best stretch before this one: the n days (one, or seven) with the most of the page's main number (its hero's: "trees felled")
        /// in your day history, ending before this period begins; the latest on a tie. A day the hero's first number is missing on (no tree felled,
        /// only axe hits) counts 0: the same number is read on every day. Greyed when no such stretch counted any yet.
        /// </summary>
        static ComparePeriods BestOf(PanelInput input, PanelState state, PanelView view, ComparePeriods last, DateTime from)
        {
            var n = DaysOf(last.Window); var today = last.To;
            var o = OptionOf(last, CompareBestId, n);
            var counts = DayCounts(input, state, view, from, today.AddDays(-1));   // every day before today: Today and 7 days share them
            // the main number: the one most days lead their hero with (a day without a tree felled leads with its axe hits: Hero drops an empty
            // first number), else the page's own now; the same number on every day
            var main = counts.Values.Select(d => d.Keys.FirstOrDefault()).Where(k => k != null).GroupBy(k => k).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key).FirstOrDefault()
                       ?? HeroNumbers(view, input, last.Window).Select(x => x.key).FirstOrDefault();
            double top = 0; DateTime? end = null;
            for (var e = from.AddDays(n - 1); e <= today.AddDays(-n); e = e.AddDays(1))
            {
                double sum = 0;
                for (int k = 0; k < n; k++) if (main != null && counts.TryGetValue(e.AddDays(-k), out var day) && day.TryGetValue(main, out var v)) sum += v;
                if (sum > 0 && sum >= top) { top = sum; end = e; }
            }
            if (end == null)
            {
                o.Open = false; o.Short = "none yet";
                o.Why = "No earlier " + (n == 1 ? "day" : n + " days") + " in your day history counted any " + (main ?? "of this page's number") + " yet.";
                return o;
            }
            o.BeforeTo = end.Value; o.BeforeFrom = end.Value.AddDays(1 - n);
            return o;
        }

        // the page's hero numbers on each day from one day to another (your day history, each day built as Today seen from that day), by their
        // title without its plural ("tree felled"), kept while the page, its choices and the history stay the same: the days before today do not change
        static string dayCountsKey; static Dictionary<DateTime, Dictionary<string, double>> dayCounts; static DayHistory dayCountsHistory;
        /// <summary>0.8.5: how many single days were built to find a best day (Dev and the tests: once per page while nothing it reads changed).</summary>
        public static int CompareDayBuilds { get; private set; }

        static Dictionary<DateTime, Dictionary<string, double>> DayCounts(PanelInput input, PanelState state, PanelView view, DateTime from, DateTime to)
        {
            var key = CompareCacheKey(input, state, view, TimeWindow.Today, to) + "|days|" + DayHistory.DayKey(from);
            if (dayCounts != null && key == dayCountsKey && ReferenceEquals(dayCountsHistory, input.History)) return dayCounts;
            var s = (PanelState)CopyOf.Invoke(state, null);
            s.Compare = false; s.CompareTip = false; s.CompareMenu = false; s.WaitAt = null; s.WaitView = null; s.Window = TimeWindow.Today; s.WindowPicked = true;
            var today = LocalToday(input);
            var d = new Dictionary<DateTime, Dictionary<string, double>>();
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var at = BeforeInput(input, TimeWindow.Today, (today - day).Days);
                var numbers = new Dictionary<string, double>();
                foreach (var (title, amount) in HeroNumbers(BuildView(at, s), at, TimeWindow.Today)) if (!numbers.ContainsKey(title)) numbers[title] = amount;
                d[day] = numbers;
                CompareDayBuilds++;
            }
            dayCountsKey = key; dayCounts = d; dayCountsHistory = input.History;
            return d;
        }

        /// <summary>0.8.5: the page's hero numbers in order, each by its title without the plural ("tree felled") with its amount (Battle > Damage:
        /// the damage dealt its comparison shows as the hero); none when the page has no hero, a number not recorded or not an amount left out.</summary>
        static IEnumerable<(string key, double amount)> HeroNumbers(PanelView v, PanelInput at, TimeWindow w)
        {
            var blocks = CloneTop(v.Blocks); BattleHeroes(blocks, at, w);
            var hero = Shown(blocks).FirstOrDefault(b => b != null && b.Kind == "hero");
            if (hero == null) yield break;
            foreach (var x in new[] { hero }.Concat((hero.Items ?? new List<Block>()).Where(i => i.Kind == "number")))
            {
                if (x.Unrecorded || string.IsNullOrEmpty(x.Value) || string.IsNullOrEmpty(x.Title)) continue;
                var a = Amount(x.Value);
                if (!double.IsNaN(a) && a > 0) yield return (Loose(x.Title), a);
            }
        }

        /// <summary>The day each option of the page's menu compares with, worked out as the menu shows them (the tests' way in): option id to its
        /// period before, or null when greyed. The menu need not be open.</summary>
        public static Dictionary<string, (DateTime from, DateTime to)?> CompareOptionPeriods(PanelInput input, PanelState state)
        {
            var s = (PanelState)CopyOf.Invoke(state, null); s.CompareMenu = true;
            var view = BuildView(input, s);
            var p = CompareOf(input, s, view);
            var d = new Dictionary<string, (DateTime, DateTime)?>();
            foreach (var o in p?.Options ?? new List<ComparePeriods>()) d[o.With] = o.Open ? (o.BeforeFrom, o.BeforeTo) : ((DateTime, DateTime)?)null;
            return d;
        }

        // ---------- the period before ----------

        /// <summary>
        /// Your input as it stood at the end of the period before a day window: the clock N days back (adjusted by the hour so its local day is
        /// exactly N days earlier across a clock change), and nothing of this session (the running session's unsaved part, its log, its armour
        /// minutes): InWindow then sums that period's day rows, every page reads it as it reads a day window.
        /// </summary>
        public static PanelInput BeforeInput(PanelInput input, TimeWindow w) => BeforeInput(input, w, DaysOf(w));

        /// <summary>0.8.5: the same, seen from <paramref name="back"/> days back (the period before ends that many days before today: 7 for the same
        /// day last week, any number for your best day).</summary>
        public static PanelInput BeforeInput(PanelInput input, TimeWindow w, int back)
        {
            var n = back;
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

        static string CompareCacheKey(PanelInput input, PanelState state, PanelView view, ComparePeriods p) => CompareCacheKey(input, state, view, p.Window, p.BeforeTo) + "|" + DayHistory.DayKey(p.To);

        static string CompareCacheKey(PanelInput input, PanelState state, PanelView view, TimeWindow w, DateTime day)
        {
            var b = new StringBuilder();
            b.Append(view.Active).Append('|').Append(view.Page).Append('|').Append(state.Player).Append('|').Append(w).Append('|').Append(DayHistory.DayKey(day))
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
            s.Compare = false; s.CompareTip = false; s.CompareMenu = false; s.WaitAt = null; s.WaitView = null;
            var before = BuildView(BeforeInput(input, p.Window, p.Back), s);
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

        sealed class CompareCtx { public string BeforeLabel, BeforeHead; public PanelInput Now, Before; public TimeWindow Window; }

        /// <summary>
        /// Build's last step on a page that offers Compare: the chip (view.Compare, or Together's own beside its window chips), its reason when
        /// greyed, and, while it is on and open, the page merged with the period before (the plate's first line names both periods).
        /// </summary>
        static void CompareFinish(PanelInput input, PanelState state, PanelView view)
        {
            // 0.8.5: the menu belongs to the page it was opened on: another page (a chapter key, the list) shuts it
            if (state.CompareMenu && state.Here != null && (state.Here.Chapter != view.Active || state.Here.Page != view.Page || state.Here.About || (state.Here.Player ?? "") != (state.Player ?? ""))) { state.CompareMenu = false; state.CompareCursor = -1; }
            var p = CompareOf(input, state, view);
            if (p == null || !p.Open) { state.CompareMenu = false; state.CompareCursor = -1; }   // and a page or window where Compare cannot open
            if (p == null) return;
            var on = state.Compare && p.Open;
            var today = LocalToday(input);
            var sw = TogetherDealtSwitch(TogetherOf(view));
            if (sw != null && view.Active == Chapter.Company) sw.Chip = new Block { Kind = "chip", Id = CompareId, Title = CompareLabel, Selected = on, Tone = p.Open ? null : OffTone, Text = p.Open ? null : p.Why, Open = state.CompareTip && !p.Open };
            else
            {
                // 0.8.5 (A): greyed in a day window, the chip says when it opens and its reason is one line in the strip (CompareNote), no tag over the
                // page; on All and the session's windows the short reason stays the chip's tag, under it while the pointer is on it
                // (a tag before the title, Everyone's or a fellow's book, leaves the heading no room for the longer chip: the date leads the line then)
                var dayShown = view.ShownWindow.HasValue && IsDayWindow(view.ShownWindow.Value);
                var dated = !p.Open && p.OpensOn.HasValue && dayShown; var tagged = view.EveryoneOn || !input.IsSelf;
                var opens = dated ? "opens " + ShortDate(p.OpensOn.Value, today) : null;
                view.Compare = new Choice { Id = CompareId, Label = dated && !tagged ? CompareLabel + " " + opens : CompareLabel, Selected = on, Disabled = !p.Open };
                view.CompareWhy = p.Open ? null : p.Why;
                view.CompareNote = !p.Open && dayShown && p.Line != null ? (dated && tagged ? CompareLabel + " " + opens + ": " + char.ToLowerInvariant(p.Line[0]) + p.Line.Substring(1) : p.Line) : null;
                view.CompareTip = state.CompareTip && !p.Open && view.CompareNote == null;
                if (p.Open) CompareKeys(state, view, p, today);
            }
            if (!on) return;
            var before = BeforeView(input, state, view, p);
            var words = CompareWords(p.Window, p.With);
            var x = new CompareCtx { BeforeLabel = words.label, BeforeHead = words.head, Now = input, Before = BeforeInput(input, p.Window, p.Back), Window = p.Window };
            // 0.8.5 (A): the pair in the page's title where the title says its window (Everyone's page leaves it to the chips: its tag takes the
            // room, PICKS: the heading never truncates), the legend in the strip
            if (view.Compare != null && !string.IsNullOrEmpty(view.HeadingWindow)) view.HeadingWindow = ComparePair(view.HeadingWindow, p);
            view.CompareNow = NowLabel(p.Window); view.CompareThen = CompareThenWords(p, today);
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
                    plate.Items.Add(new Block { Kind = "note", Text = "A very full day in " + words.title + " kept only its largest kinds: a few small ones may be missing there." });
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
        public static void CompareBlocks(List<Block> now, List<Block> before, TimeWindow w) => CompareMerge(now, before, new CompareCtx { BeforeLabel = BeforeLabel(w), BeforeHead = BeforeLabel(w), Window = w });

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
            if (rows.Count > CompareMaxRows && look != RowLook.Person)   // the smallest past the ninth as one quiet row, summed when the amounts are counts; people never fold (0.8.2: everyone shows)
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
                                  Text = head ?? (keepCap ? null : b.Title), Src = b.Src, Source = b.Source, Items = items,
                                  NowHead = NowLabel(x.Window), BeforeHead = x.BeforeHead ?? x.BeforeLabel };   // the column heads name the periods ("today", "yesterday"; 0.8.5: "best day")
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
        static IEnumerable<string> CompareText(PanelView v) => new[] { v.Compare?.Label, v.CompareWhy, v.CompareNote, v.CompareNow, v.CompareThen }
            .Concat(v.CompareMenu == null ? new string[0] : new[] { v.CompareMenu.Title }.Concat(v.CompareMenu.Items.SelectMany(o => new[] { o.Title, o.Value, o.Text })));

        // ---------- 0.8.5: the menu, its keys ----------

        /// <summary>The menu's link for an option of a page: ComparePickTarget, the page, "=", the option.</summary>
        public static string ComparePickLink(string page, string option) => ComparePickTarget + page + "=" + option;

        // the chip's key in the key line ("[J] Compare"), and while the menu is open its rows (CompareMenu) and the keys that walk them
        static void CompareKeys(PanelState state, PanelView view, ComparePeriods p, DateTime today)
        {
            if (!string.IsNullOrEmpty(state.CompareKey) && !state.ShowAbout)
            {
                var back = view.Keys.IndexOf("[Backspace] Back");
                var key = "[" + state.CompareKey + "] " + CompareLabel;
                if (back >= 0) view.Keys.Insert(back, key); else view.Keys.Add(key);
            }
            if (!state.CompareMenu || p.Options == null) return;
            var page = ComparePageOf(view);
            var on = state.Compare;
            var items = p.Options.Select(o => new Block
            {
                Kind = CompareOptionKind, Id = o.With, Title = CompareWords(o.Window, o.With).menu, Value = o.Open ? CompareDates(o, today) : null,
                Selected = on && o.With == p.With, Tone = o.Open ? null : OffTone, Text = o.Open ? null : o.Short, Note = o.Open ? ComparePickLink(page, o.With) : null,
            }).ToList();
            items.Add(new Block { Kind = CompareOptionKind, Id = CompareOffId, Title = CompareOffLabel, Selected = !on, Note = ComparePickLink(page, CompareOffId) });
            if (state.CompareCursor == -2) state.CompareCursor = Math.Max(0, items.FindIndex(i => i.Selected));   // opened by its key: on the option chosen now
            var cursor = state.CompareCursor >= 0 && state.CompareCursor < items.Count && items[state.CompareCursor].Tone != OffTone ? state.CompareCursor : -1;
            if (cursor >= 0) items[cursor].Open = true;   // the keys' place in the menu
            view.CompareMenu = new Block { Kind = CompareMenuKind, Title = "Compare " + NowLabel(p.Window) + " against", Items = items };
            var at = view.Keys.IndexOf("[W/S] Page");
            if (at >= 0) view.Keys[at] = CompareMenuKeys; else view.Keys.Insert(Math.Min(1, view.Keys.Count), CompareMenuKeys);
        }

        /// <summary>0.8.5: an option chosen in the menu (ComparePickTarget): Off turns Compare off; any other turns it on, kept for its page (the
        /// stretch just before is the default, kept as no entry). The menu closes.</summary>
        public static void ComparePick(PanelState s, string pageAndOption)
        {
            var eq = (pageAndOption ?? "").LastIndexOf('=');
            s.CompareMenu = false; s.CompareCursor = -1;
            if (eq <= 0) return;
            string page = pageAndOption.Substring(0, eq), option = pageAndOption.Substring(eq + 1);
            if (option == CompareOffId) { s.Compare = false; return; }
            if (Array.IndexOf(CompareChoices, option) < 0) return;
            s.Compare = true;
            if (option == CompareLastId) s.CompareWith.Remove(page); else s.CompareWith[page] = option;
        }

        /// <summary>0.8.5: the compare key (and the pad's X): the menu opens on the option the page compares with now, or closes. false when the page
        /// has no open Compare chip.</summary>
        public static bool CompareKeyPressed(PanelState s, PanelView v)
        {
            if (v?.Compare == null || v.Compare.Disabled) return false;
            if (s.CompareMenu) { s.CompareMenu = false; s.CompareCursor = -1; return true; }
            s.CompareMenu = true; s.CompareTip = false;
            s.CompareCursor = -2;   // the next build puts it on the chosen option (CompareMenuFocus)
            return true;
        }

        /// <summary>0.8.5: W/S in the open menu: the next option that can be chosen (greyed ones are skipped), round. false when the menu is shut.</summary>
        public static bool CompareMenuStep(PanelState s, PanelView v, int d)
        {
            var items = v?.CompareMenu?.Items; if (!s.CompareMenu || items == null || items.Count == 0) return false;
            var at = items.FindIndex(i => i.Open); if (at < 0) at = items.FindIndex(i => i.Selected);
            for (int k = 1; k <= items.Count; k++) { var i = ((at + d * k) % items.Count + items.Count) % items.Count; if (items[i].Tone != OffTone) { s.CompareCursor = i; return true; } }
            return false;
        }

        /// <summary>0.8.5: Enter in the open menu: the option under the keys is chosen (ComparePick). false when there is none.</summary>
        public static bool CompareMenuChoose(PanelState s, PanelView v)
        {
            var item = v?.CompareMenu?.Items?.FirstOrDefault(i => i.Open) ?? v?.CompareMenu?.Items?.FirstOrDefault(i => i.Selected);
            if (!s.CompareMenu || item?.Note == null) return false;
            ComparePick(s, item.Note.Substring(ComparePickTarget.Length));
            return true;
        }
    }
}
