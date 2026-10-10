using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// One window set for the whole panel (HISTORY-06.md): the event log's short windows (10 min, 30 min, 1 h, 3 h, Session), the day
    /// history's day windows (Today, 7 days, 30 days) and All (the since-install totals with your character's own counters). One chosen
    /// window (PanelState.Window) for every page; each page offers only the windows its data has (WindowsOf), and a page that does not
    /// offer the chosen one shows All while the choice stays for the pages that do. A day window that begins before the history did is
    /// greyed; pressing it chooses it, the page shows All and one line says from when it works (WaitLine).
    /// A day window's numbers come from a copy of the input (InWindow) whose tallies are the window's rows: the page code reads it the
    /// way it reads the since-install totals, so a page needs no second implementation; what has no days (a "most at once" counter,
    /// the faded "before install" layer, a record, the server's book) is left out by the page in a day window.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>What a day window's copy of the input holds (PanelInput.Window): the window, its first and last local day, and whether a
        /// row of it was clipped (MaxRowKeys) so a list may miss its smallest kinds.</summary>
        public class DayView { public TimeWindow Window; public DateTime From, To; public bool Clipped; public bool Short => IsShortWindow(Window); }

        public static readonly TimeWindow[] ShortWindows = { TimeWindow.LastTenMinutes, TimeWindow.LastThirtyMinutes, TimeWindow.LastHour, TimeWindow.LastThreeHours, TimeWindow.Session };
        public static readonly TimeWindow[] DayWindows = { TimeWindow.Today, TimeWindow.SevenDays, TimeWindow.ThirtyDays };
        /// <summary>Every window, in chip order.</summary>
        public static readonly TimeWindow[] AllWindows = ShortWindows.Concat(DayWindows).Concat(new[] { TimeWindow.SinceInstall }).ToArray();
        /// <summary>The pages with only day windows and All (their data has days, not minutes).</summary>
        public static readonly TimeWindow[] DayAndAll = DayWindows.Concat(new[] { TimeWindow.SinceInstall }).ToArray();

        /// <summary>The Tone of a view in a switch that cannot be chosen (Together's day window before the history): drawn greyed, inert, skipped by the keys.</summary>
        public const string OffTone = "off";

        public static bool IsDayWindow(TimeWindow w) => w == TimeWindow.Today || w == TimeWindow.SevenDays || w == TimeWindow.ThirtyDays;
        public static bool IsShortWindow(TimeWindow w) => !IsDayWindow(w) && w != TimeWindow.SinceInstall;
        public static int DaysOf(TimeWindow w) => w == TimeWindow.Today ? 1 : w == TimeWindow.SevenDays ? 7 : w == TimeWindow.ThirtyDays ? 30 : 0;

        /// <summary>
        /// The Deeds pages whose main numbers this session's DeedLog holds per minute, so they also offer the short windows (10 min .. Session,
        /// DeedsShort). Taming is not one: petted, commands, led and born have no minutes (only the tame counter has).
        /// </summary>
        public static readonly string[] DeedsShortPages = { "cooking", "building", "groundwork", "crafting", "woodcutting", "mining", "farming", "fishing" };

        /// <summary>The windows a page offers, in chip order; null = the page has no window chips.</summary>
        public static TimeWindow[] WindowsOf(Chapter chapter, string page)
        {
            if (chapter == Chapter.Battle && (page == "overview" || page == "damage" || page == "deaths" || page == "foes" || page == "defense")) return AllWindows;
            if (chapter == Chapter.Voyages && (page == "sailing" || page == "cargo")) return DayAndAll;
            if (chapter == Chapter.Deeds && page == RecentPageId) return RecentWindows;   // Deeds > Recent: Battle's whole set (B33, RecentPage.cs)
            if (chapter == Chapter.Deeds && Array.IndexOf(DeedsShortPages, page) >= 0) return AllWindows;   // 0.7: the short windows too, from this session's DeedLog (DeedsShort)
            if (chapter == Chapter.Deeds && page != "overview") return DayAndAll;   // 0.7: every Deeds page but the Overview, whose cards are the titles (earned by lifetime counts)
            return null;
        }

        /// <summary>Today as the player's calendar says it (the day history's rows are local days).</summary>
        public static DateTime LocalToday(PanelInput input) => Local(input, input.NowUtc).Date;

        /// <summary>The first local day of a day window: today for Today, six days back for 7 days.</summary>
        public static DateTime DayFrom(PanelInput input, TimeWindow w) => LocalToday(input).AddDays(1 - Math.Max(1, DaysOf(w)));

        /// <summary>A day window can be shown: your own book with a day history that reaches back to its first day (a fellow's copy carries no rows).</summary>
        public static bool DayOpen(PanelInput input, TimeWindow w) =>
            input != null && input.IsSelf && input.History != null && DayFrom(input, w) >= input.History.FirstDay(LocalToday(input));

        /// <summary>Whether a window can be chosen on this book: the log's windows and All as before (WindowShared), a day window when DayOpen.</summary>
        public static bool WindowOpen(PanelInput input, TimeWindow w) => IsDayWindow(w) ? DayOpen(input, w) : WindowShared(input, w);

        /// <summary>The first day a day window works on your own book (its first day is the day history's first); null when it already works,
        /// or when there is no day history to wait for (a fellow's copy).</summary>
        public static DateTime? DayOpensOn(PanelInput input, TimeWindow w)
        {
            if (!IsDayWindow(w) || input == null || !input.IsSelf || input.History == null || DayOpen(input, w)) return null;
            return input.History.FirstDay(LocalToday(input)).AddDays(DaysOf(w) - 1);
        }

        /// <summary>
        /// "7 days works from 15 Oct: Hearthwoven started counting days on 9 Oct." The one line a greyed day chip of your own book says when
        /// it is pressed (the page then shows All); null when the window works or does not wait for the day history. B17 (Joost 2026-10-09):
        /// a standing "Day history since 9 Oct: 7 days opens on 15 Oct" on every windowed page explained the mechanism and was not understood;
        /// a date on the chip itself did not fit the nine chips of the heading row (it cut the page heading).
        /// </summary>
        public static string WaitLine(PanelInput input, TimeWindow w)
        {
            var opens = DayOpensOn(input, w);
            if (!opens.HasValue) return null;
            var today = LocalToday(input);
            return WindowShort(w) + " works from " + ShortDate(opens.Value, today) + ": Hearthwoven started counting days on " + ShortDate(input.History.FirstDay(today), today) + ".";
        }

        /// <summary>
        /// B17 review: a pressed greyed day window held for the whole session (every windowed page showed All with its line, Battle lost its
        /// Session). Now it holds on the page where it was pressed: the first build there notes the page (WaitAt); a build of any other page,
        /// or the book opening again (ForgetHistory), returns Window to the last chosen window that was open, and Together's switch to its own.
        /// </summary>
        public static void SettleWait(PanelState s, PanelPlace place, PanelInput input)
        {
            if (s.WaitAt != null && !s.WaitAt.Same(place)) EndWait(s);
            if (input == null || !input.IsSelf) return;
            if (WindowOpen(input, s.Window)) s.LastWorking = s.Window;
            else if (s.WaitAt == null && DayOpensOn(input, s.Window).HasValue) s.WaitAt = place;
        }

        /// <summary>Back to the last windows that worked (Window, Together's switch) and no line waiting.</summary>
        public static void EndWait(PanelState s)
        {
            if (s.WaitAt != null || s.WaitView != null)
            {
                if (s.WaitAt != null && IsDayWindow(s.Window) && s.Window != s.LastWorking) s.Window = s.LastWorking;
                if (s.WaitView != null) { if (s.LastWorkingView.TryGetValue(s.WaitView, out var lw)) s.View[s.WaitView] = lw; else s.View.Remove(s.WaitView); }
            }
            s.WaitAt = null; s.WaitView = null;
        }

        /// <summary>
        /// The window chips of a page (view.Windows, view.HasFilters) and the window it shows: the chosen one when the page offers it and it
        /// is open, else All. A window the page offers but cannot show stays in the row, greyed (a fellow's book; a day before the history).
        /// </summary>
        public static TimeWindow WindowChips(PanelInput input, PanelState state, PanelView view, TimeWindow[] offered, TimeWindow? chosen = null, Func<PanelInput, TimeWindow, bool> open = null)
        {
            open = open ?? WindowOpen;
            var want = chosen ?? state.Window;   // the Deeds pages pass All until the player chose a window (PanelState.WindowPicked)
            var shown = offered.Contains(want) && open(input, want) ? want : TimeWindow.SinceInstall;
            view.HasFilters = true; view.ShownWindow = shown;
            foreach (var w in offered) view.Windows.Add(new Choice { Id = w.ToString(), Label = WindowShort(w), Selected = w == shown, Disabled = !open(input, w), Waits = DayOpensOn(input, w).HasValue });
            return shown;
        }

        /// <summary>A Deeds window on this book: a short one (10 min .. Session) needs your own DeedLog, so a fellow's copy shows it greyed (their
        /// Session is not their deeds per minute); the day windows and All as WindowOpen.</summary>
        public static bool DeedsWindowOpen(PanelInput input, TimeWindow w) => IsShortWindow(w) ? input != null && input.IsSelf : WindowOpen(input, w);

        /// <summary>Why a fellow's Deeds window chips are greyed, in one line (as Battle's SharedWindowsLine).</summary>
        public static string DeedsWindowsLine(PanelInput input) => Name(input) + " shares totals only, not days or minutes";

        /// <summary>
        /// A copy of your input that holds a day window instead of the totals: Events, DamageSinceInstall, BiomeSinceInstall and the game's
        /// counters (Character, PiecesPlaced, Harvested) are the window's rows added up (with the running session's unsaved part on Today), ItemsPickedUp the
        /// window's pickups (so no "before install" layer appears), no baselines, no server book. null when the window cannot be shown.
        /// </summary>
        public static PanelInput InWindow(PanelInput input, TimeWindow w)
        {
            if (!IsDayWindow(w) || !DayOpen(input, w)) return null;
            var today = LocalToday(input); var from = DayFrom(input, w);
            var row = input.History.Sum(from, today);
            if (input.Pending != null) row.AddAll(input.Pending);   // the running session since the last save: always today
            var c = input.ShallowCopy();
            c.Events = row.Events; c.DamageSinceInstall = row.Damage; c.BiomeSinceInstall = row.Biome; c.BiomeFromUtc = null;
            c.Character = row.Game.Where(kv => !kv.Key.Contains("|")).ToDictionary(kv => kv.Key, kv => kv.Value);
            c.PiecesPlaced = DayHistory.Family(row.Game, DayHistory.PlacedPrefix); c.Harvested = DayHistory.Family(row.Game, DayHistory.PickedPrefix);   // 0.7: the per-token counters' growth
            c.EnemyKills = null;   // the kills per foe (the bosses on the strip) have no days: only the game's kill counter's growth is kept
            c.ItemsPickedUp = new Dictionary<string, float>(row.Events.PickedUp);
            c.Baseline = null; c.ExactAtBaseline = null; c.BaselineAt = null; c.Book = null;
            c.Window = new DayView { Window = w, From = from, To = today, Clipped = row.Clipped };
            return c;
        }

        /// <summary>The start of a window in UTC (a day window: local midnight of its first day; a short window: Cutoff; Session and All: null).</summary>
        public static DateTime? WindowStartUtc(PanelInput input, TimeWindow w)
        {
            if (!IsDayWindow(w)) return Cutoff(w, input.NowUtc);
            var localMidnight = DayFrom(input, w);
            var offset = Local(input, input.NowUtc) - input.NowUtc;   // the player's offset now (a day window is about calendar days, not exact hours)
            return DateTime.SpecifyKind(localMidnight - offset, DateTimeKind.Utc);
        }

        /// <summary>Your hits in a window of this session's log, dealt or received (the log counts hits per bucket, without the type).</summary>
        public static double HitsIn(EventLog log, TimeWindow w, DateTime nowUtc, bool dealt, DateTime? sessionFrom = null)
        {
            if (log == null) return 0;
            double n = 0; var dir = dealt ? "dealt" : "taken";
            foreach (var kv in log.Hits)
            {
                var p = LogKeys.Of(kv.Key).P;   // time|biome|dir|foe|cause (0.8: split once, LogKeys.cs)
                if (p.Length < 5 || p[2] != dir || !InFilter(p[0], p[1], w, "", nowUtc, log.Span, sessionFrom)) continue;
                n += kv.Value;
            }
            return n;
        }

        /// <summary>A window with nothing in it on a page that counts days (Voyages, Deeds): says the window and where more is.</summary>
        static Block DayEmpty(PanelInput input, TimeWindow w) =>
            Empty(w == TimeWindow.Session ? NothingYet + " this session" : "Nothing " + (w == TimeWindow.Today ? "today" : "in the " + WindowLabel(w).ToLowerInvariant()), DayEmptyLine);
        public const string DayEmptyLine = "Choose a longer window, or All for everything.";

        /// <summary>The line of a page that leaves something out in a day window, because that thing has no days.</summary>
        public const string NoDaysServerBook = "The server's cargo book has no days: choose All to see it.",
                            NoDaysBlocks = "Blocks and parries show in Session and longer windows.",
                            FellowBlocksAll = "Other players' blocks and parries show in All.",
                            ClippedLine = "A very full day kept only its largest kinds: a few small ones may be missing here.",
                            ClippedMinuteLine = "A very busy minute kept only its largest kinds: a few small ones may be missing here.";
        /// <summary>The clipped line of a window: a day row's or (a short window) a minute's.</summary>
        public static string ClippedLineOf(DayView w) => w != null && w.Short ? ClippedMinuteLine : ClippedLine;
    }
}
