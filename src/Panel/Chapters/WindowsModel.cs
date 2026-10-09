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
    /// greyed, and one line on the plate says from when the history runs and when the window opens.
    /// A day window's numbers come from a copy of the input (InWindow) whose tallies are the window's rows: the page code reads it the
    /// way it reads the since-install totals, so a page needs no second implementation; what has no days (a "most at once" counter,
    /// the faded "before install" layer, a record, the server's book) is left out by the page in a day window.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>What a day window's copy of the input holds (PanelInput.Window): the window, its first and last local day, and whether a
        /// row of it was clipped (MaxRowKeys) so a list may miss its smallest kinds.</summary>
        public class DayView { public TimeWindow Window; public DateTime From, To; public bool Clipped; }

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

        /// <summary>The windows a page offers, in chip order; null = the page has no window chips.</summary>
        public static TimeWindow[] WindowsOf(Chapter chapter, string page)
        {
            if (chapter == Chapter.Battle && (page == "overview" || page == "damage" || page == "deaths" || page == "foes" || page == "defense")) return AllWindows;
            if (chapter == Chapter.Voyages && (page == "sailing" || page == "cargo")) return DayAndAll;
            if (chapter == Chapter.Deeds && (page == "woodcutting" || page == "mining")) return DayAndAll;
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

        /// <summary>"Day history since 3 Oct: 30 days opens on 1 Nov." The one line under greyed day chips on your own book; null when none is greyed.</summary>
        public static string HistoryLine(PanelInput input, IEnumerable<TimeWindow> offered)
        {
            if (input == null || !input.IsSelf || input.History == null) return null;
            var greyed = offered.Where(w => IsDayWindow(w) && !DayOpen(input, w)).ToList();
            if (greyed.Count == 0) return null;
            var today = LocalToday(input); var first = input.History.FirstDay(today);
            var opens = string.Join(", ", greyed.Select(w => WindowShort(w) + " opens on " + ZoneDate(first.AddDays(DaysOf(w) - 1), today)).ToArray());
            return "Day history since " + ZoneDate(first, today) + ": " + opens + ".";
        }

        /// <summary>
        /// The window chips of a page (view.Windows, view.HasFilters) and the window it shows: the chosen one when the page offers it and it
        /// is open, else All. A window the page offers but cannot show stays in the row, greyed (a fellow's book; a day before the history).
        /// </summary>
        public static TimeWindow WindowChips(PanelInput input, PanelState state, PanelView view, TimeWindow[] offered)
        {
            var shown = offered.Contains(state.Window) && WindowOpen(input, state.Window) ? state.Window : TimeWindow.SinceInstall;
            view.HasFilters = true; view.ShownWindow = shown;
            foreach (var w in offered) view.Windows.Add(new Choice { Id = w.ToString(), Label = WindowShort(w), Selected = w == shown, Disabled = !WindowOpen(input, w) });
            return shown;
        }

        /// <summary>
        /// A copy of your input that holds a day window instead of the totals: Events, DamageSinceInstall, BiomeSinceInstall and the game's
        /// counters (Character) are the window's rows added up (with the running session's unsaved part on Today), ItemsPickedUp the
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
            c.Character = new Dictionary<string, float>(row.Game);
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
        public static double HitsIn(EventLog log, TimeWindow w, DateTime nowUtc, bool dealt)
        {
            if (log == null) return 0;
            double n = 0; var dir = dealt ? "dealt" : "taken";
            foreach (var kv in log.Hits)
            {
                var p = kv.Key.Split('|');   // time|biome|dir|foe|cause
                if (p.Length < 5 || p[2] != dir || !InFilter(p[0], p[1], w, "", nowUtc, log.Span)) continue;
                n += kv.Value;
            }
            return n;
        }

        /// <summary>A window with nothing in it on a page that counts days (Voyages, Deeds): says the window and where more is.</summary>
        static Block DayEmpty(PanelInput input, TimeWindow w) =>
            Empty("Nothing " + (w == TimeWindow.Today ? "today" : "in the " + WindowLabel(w).ToLowerInvariant()), "Choose a longer window, or All for everything.");

        /// <summary>The line of a page that leaves something out in a day window, because that thing has no days.</summary>
        public const string NoDaysServerBook = "The server's cargo book has no days: choose All to see it.",
                            NoDaysBlocks = "Blocks and parries are counted per session and per day: choose Session or longer.",
                            ClippedLine = "A very full day kept only its largest kinds: a few small ones may be missing here.";
    }
}
