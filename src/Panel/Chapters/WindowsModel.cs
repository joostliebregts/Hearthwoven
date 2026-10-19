using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// One window set for the whole panel (HISTORY-06.md): the event log's short windows (10 min, 30 min, 1 h, 3 h, Session), the day
    /// history's day windows (Today, Yesterday, 3 days, 7 days, 30 days; 0.9.1: Yesterday and 3 days) and All (the since-install totals with your character's own counters). One chosen
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
        /// row of it was clipped (MaxRowKeys) so a list may miss its smallest kinds. Shared (0.8.5): a fellow's day window from what their copy
        /// shares per day (FellowDeedsWindow: family -> part -> amount), so a page leaves out the lists those parts cannot fill; null otherwise.
        /// Whole (0.8.5): the book the window was cut from (its fellows, its all-time records), for a list a page draws from other players'
        /// days (Cooking's Who enjoyed, FoodBookIn); null when not known.</summary>
        public class DayView { public TimeWindow Window; public DateTime From, To; public bool Clipped; public Dictionary<string, Dictionary<string, double>> Shared; public PanelInput Whole; public bool Short => IsShortWindow(Window); }

        public static readonly TimeWindow[] ShortWindows = { TimeWindow.LastTenMinutes, TimeWindow.LastThirtyMinutes, TimeWindow.LastHour, TimeWindow.LastThreeHours, TimeWindow.Session };
        public static readonly TimeWindow[] DayWindows = { TimeWindow.Today, TimeWindow.Yesterday, TimeWindow.ThreeDays, TimeWindow.SevenDays, TimeWindow.ThirtyDays };
        /// <summary>Every window, in chip order.</summary>
        public static readonly TimeWindow[] AllWindows = ShortWindows.Concat(DayWindows).Concat(new[] { TimeWindow.SinceInstall }).ToArray();
        /// <summary>The pages with only day windows and All (their data has days, not minutes).</summary>
        public static readonly TimeWindow[] DayAndAll = DayWindows.Concat(new[] { TimeWindow.SinceInstall }).ToArray();

        /// <summary>The Tone of a view in a switch that cannot be chosen (Together's day window before the history): drawn greyed, inert, skipped by the keys.</summary>
        public const string OffTone = "off";

        public static bool IsDayWindow(TimeWindow w) => w == TimeWindow.Today || w == TimeWindow.Yesterday || w == TimeWindow.ThreeDays || w == TimeWindow.SevenDays || w == TimeWindow.ThirtyDays;
        public static bool IsShortWindow(TimeWindow w) => !IsDayWindow(w) && w != TimeWindow.SinceInstall;
        public static int DaysOf(TimeWindow w) => w == TimeWindow.Today || w == TimeWindow.Yesterday ? 1 : w == TimeWindow.ThreeDays ? 3 : w == TimeWindow.SevenDays ? 7 : w == TimeWindow.ThirtyDays ? 30 : 0;
        /// <summary>0.9.1: a day window of one whole day (Today, Yesterday): said as the day ("today", "yesterday"), not "in the last ...".</summary>
        public static bool IsOneDay(TimeWindow w) => w == TimeWindow.Today || w == TimeWindow.Yesterday;
        /// <summary>0.9.1: the day window ends today (every one but Yesterday), so the running session's unsaved part belongs in it.</summary>
        public static bool HoldsToday(TimeWindow w) => IsDayWindow(w) && w != TimeWindow.Yesterday;
        /// <summary>A window in words after "Nothing": "today", "yesterday", "in the last 3 days".</summary>
        public static string InWindowWords(TimeWindow w) => w == TimeWindow.Today ? "today" : w == TimeWindow.Yesterday ? "yesterday" : "in the " + WindowLabel(w).ToLowerInvariant();
        /// <summary>0.9.1: Yesterday in the heading, with its day in the reader's own calendar: "yesterday, Sunday 18 Oct".</summary>
        public static string YesterdayWords(PanelInput input) => YesterdayHead + LocalToday(input).AddDays(-1).ToString("dddd d MMM", Inv);

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
            if (chapter == Chapter.Deeds && IsDeedFeedPage(page)) return AllWindows;   // 0.9: a deed feed, as Battle's (Chapters/DeedFeedModel.cs)
            if (chapter == Chapter.Deeds && DeedSubParent(page) != null) return null;   // 0.9: a deed's History (its own Days, Weeks, Months) and one day of it (the day is the page): no window chips
            if (chapter == Chapter.Deeds && page == RecentPageId) return RecentWindows;   // Deeds > Recent: Battle's whole set (B33, RecentPage.cs)
            if (chapter == Chapter.Deeds && Array.IndexOf(DeedsShortPages, page) >= 0) return AllWindows;   // 0.7: the short windows too, from this session's DeedLog (DeedsShort)
            if (chapter == Chapter.Deeds && page != "overview" && page != SetsPageId && page != MealPlanPageId) return DayAndAll;   // 0.9.5: Meal plan is the game's food data, no count (Chapters/MealPlanModel.cs)   // 0.9: Armor sets counts the character's whole life (Chapters/ArmorSets.cs)   // 0.7: every Deeds page but the Overview, whose cards are the titles (earned by lifetime counts)
            return null;
        }

        /// <summary>Today as the player's calendar says it (the day history's rows are local days).</summary>
        public static DateTime LocalToday(PanelInput input) => Local(input, input.NowUtc).Date;

        /// <summary>The first local day of a day window: today for Today, yesterday for Yesterday, two days back for 3 days, six for 7 days.</summary>
        public static DateTime DayFrom(PanelInput input, TimeWindow w) => DayTo(input, w).AddDays(1 - Math.Max(1, DaysOf(w)));
        /// <summary>0.9.1: the last local day of a day window: yesterday for Yesterday (midnight to midnight, the reader's own clock), else today.</summary>
        public static DateTime DayTo(PanelInput input, TimeWindow w) => w == TimeWindow.Yesterday ? LocalToday(input).AddDays(-1) : LocalToday(input);

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
            return input.History.FirstDay(LocalToday(input)).AddDays((LocalToday(input) - DayFrom(input, w)).Days);   // 7 days: six days after; Yesterday: the day after
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

        /// <summary>
        /// A Deeds window on this book. Yours: a short one (10 min .. Session) from your DeedLog, the day windows and All as WindowOpen. A fellow's
        /// (0.8.5): Session from what reached this PC from them while you played (their trail, as Recent and Everyone read it), 10 min to 3 h while
        /// their copy is live (FellowLive, Battle's rule), the day windows when their copy shares Deeds days (FellowDeedsOpen), All as before.
        /// </summary>
        public static bool DeedsWindowOpen(PanelInput input, TimeWindow w)
        {
            if (input == null) return false;
            if (input.IsSelf) return IsShortWindow(w) ? true : WindowOpen(input, w);
            if (w == TimeWindow.Session) return true;
            if (IsShortWindow(w)) return FellowLive(input);
            return IsDayWindow(w) ? FellowDeedsOpen(input, w) : WindowOpen(input, w);
        }

        /// <summary>
        /// Why a fellow's Deeds window chips are greyed, in one line (as Battle's SharedWindowsLine): 10 min to 3 h while they do not play
        /// (FellowShortWhy), their days when their copy shares none (an older Hearthwoven: OlderLine), All when
        /// their copy carries no totals. <paramref name="page"/>: the Deeds page (Meals and Taming have no short windows); null when nothing
        /// is greyed.
        /// </summary>
        public static string DeedsWindowsLine(PanelInput input, string page = null)
        {
            if (input == null || input.IsSelf) return null;
            var parts = new List<string>();
            var offered = page == null ? AllWindows : WindowsOf(Chapter.Deeds, page) ?? DayAndAll;
            if (offered.Any(w => IsShortWindow(w) && w != TimeWindow.Session)) parts.Add(FellowShortWhy(input));
            if (input.DayDeeds == null) parts.Add(OlderThan(input, NeedsDayDeeds) ? OlderLine(input, "their days fill in") : Name(input) + "'s days are not shared.");
            if (!input.SharedSinceInstall) parts.Add(Name(input) + "'s totals are not shared.");
            var line = string.Join(" ", parts.Where(p => p != null).ToArray());
            return line.Length == 0 ? null : line;
        }

        /// <summary>A window of Voyages > Sailing or Cargo on this book: yours as WindowOpen; a fellow's day windows (0.8.5) when their copy shares
        /// voyage days, read as their Everyone row reads them (VoyageDays); All as before.</summary>
        public static bool VoyagesWindowOpen(PanelInput input, TimeWindow w) =>
            input != null && (input.IsSelf || !IsDayWindow(w) ? WindowOpen(input, w) : input.DayVoyages && input.DayDeeds != null);

        /// <summary>Why a fellow's Sailing and Cargo window chips are greyed, in one line (as DeedsWindowsLine): their days when their copy shares no
        /// voyage days (an older Hearthwoven or the 0.8.5 test build: OlderLine, never "sailed nothing"), All when it carries no totals; null when
        /// nothing is greyed.</summary>
        public static string VoyagesWindowsLine(PanelInput input)
        {
            if (input == null || input.IsSelf) return null;
            var parts = new List<string>();
            if (!VoyagesWindowOpen(input, TimeWindow.Today))
                parts.Add(input.DayDeeds != null || OlderThan(input, NeedsDayDeeds) ? OlderLine(input, "their days fill in") : Name(input) + "'s days are not shared.");
            if (!input.SharedSinceInstall) parts.Add(Name(input) + "'s totals are not shared.");
            return parts.Count == 0 ? null : string.Join(" ", parts.ToArray());
        }

        /// <summary>
        /// A copy of your input that holds a day window instead of the totals: Events, DamageSinceInstall, BiomeSinceInstall and the game's
        /// counters (Character, PiecesPlaced, Harvested) are the window's rows added up (with the running session's unsaved part on Today), ItemsPickedUp the
        /// window's pickups (so no "before install" layer appears), no baselines, no server book. null when the window cannot be shown.
        /// </summary>
        public static PanelInput InWindow(PanelInput input, TimeWindow w)
        {
            if (!IsDayWindow(w) || !DayOpen(input, w)) return null;
            var from = DayFrom(input, w); var to = DayTo(input, w);
            var row = input.History.Sum(from, to);
            if (input.Pending != null && HoldsToday(w)) row.AddAll(input.Pending);   // the running session since the last save: always today (never Yesterday)
            var c = input.ShallowCopy();
            c.Events = row.Events; c.DamageSinceInstall = row.Damage; c.BiomeSinceInstall = row.Biome; c.BiomeFromUtc = null;
            c.Character = row.Game.Where(kv => !kv.Key.Contains("|")).ToDictionary(kv => kv.Key, kv => kv.Value);
            c.PiecesPlaced = DayHistory.Family(row.Game, DayHistory.PlacedPrefix); c.Harvested = DayHistory.Family(row.Game, DayHistory.PickedPrefix);   // 0.7: the per-token counters' growth
            c.EnemyKills = null;   // the kills per foe (the bosses on the strip) have no days: only the game's kill counter's growth is kept
            c.ItemsPickedUp = new Dictionary<string, float>(row.Events.PickedUp);
            c.Baseline = null; c.ExactAtBaseline = null; c.BaselineAt = null; c.Book = null;
            c.Window = new DayView { Window = w, From = from, To = to, Clipped = row.Clipped, Whole = input };
            c.DealtArmourSince = DealtLedgerIn(input, w);   // 0.8.5: the dealt ledger's same days (DamageSinceInstallRows reads it beside the day's damage)
            return c;
        }

        /// <summary>
        /// 0.9: a fellow's damage dealt in a day window from what their copy shares per day ("dayDealt": weapon kind and type per local day, the
        /// last 30 days), as the rows the Damage page reads: no foe (a day is not split by foe), the weapon kind as the row's cause (WeaponOf takes
        /// it as itself). The window is the viewer's calendar (<paramref name="viewer"/>: your book, or the fellow's book read on this PC), as
        /// Together reads their dealtByDay. null when their copy shares no split (a Hearthwoven before 0.9).
        /// </summary>
        public static List<DamageRow> FellowDayRows(PanelInput p, TimeWindow w, PanelInput viewer)
        {
            if (p?.DayDealt == null || !IsDayWindow(w)) return null;
            DateTime from = DayFrom(viewer, w), to = DayTo(viewer, w);
            var sum = new Dictionary<string, double>();
            foreach (var day in p.DayDealt)
                if (DateTime.TryParseExact(day.Key, "yyyy-MM-dd", Inv, System.Globalization.DateTimeStyles.None, out var d) && d >= from && d <= to)
                    foreach (var kv in day.Value ?? new Dictionary<string, float>()) if (kv.Value > 0) { sum.TryGetValue(kv.Key, out var o); sum[kv.Key] = o + kv.Value; }
            var rows = new List<DamageRow>();
            foreach (var kv in sum.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var k = kv.Key.Split('|');
                if (k.Length == 2) rows.Add(new DamageRow { Biome = "", Dir = "dealt", Other = "", Cause = k[0], Type = k[1], Amount = (float)kv.Value });
            }
            return rows;
        }

        /// <summary>A fellow's book in a day window (0.9, Battle > Damage only): a copy whose damage since install is the window's shared parts
        /// (FellowDayRows), read the way a day window of your own is (InWindow); null when their copy shares no split.</summary>
        public static PanelInput FellowInWindow(PanelInput input, TimeWindow w)
        {
            var rows = FellowDayRows(input, w, input);
            if (rows == null) return null;
            var t = new DamageTally();
            foreach (var r in rows) t.Dealt["|" + r.Cause + "|" + r.Type] = r.Amount;
            var c = input.ShallowCopy();
            c.DamageSinceInstall = t; c.BiomeSinceInstall = null; c.BiomeFromUtc = null;
            c.Window = new DayView { Window = w, From = DayFrom(input, w), To = DayTo(input, w) };
            return c;
        }

        /// <summary>A fellow's day window is open on their book: Battle > Damage, when their copy splits its days (0.9, FellowDayRows).</summary>
        public static bool FellowDaysOpen(PanelInput input, TimeWindow w) => input != null && !input.IsSelf && IsDayWindow(w) && input.DayDealt != null;

        /// <summary>
        /// 0.9 (Joost: Today's numbers looked unrelated to Session's): the one line of a day window that shows fellow players' numbers, once per
        /// page. A fellow's day is their whole local day as they shared it, also the part before you came online; Session counts from your start
        /// (WindowFellowSession). <paramref name="who"/>: one fellow's name on their own book; null = the group ("each player's").
        /// </summary>
        public static string DayWindowLine(TimeWindow w, string who = null)
        {
            var whose = who == null ? "each player's" : who + "'s";
            return w == TimeWindow.Today ? "Today counts " + whose + " whole day, also before you came online."
                 : w == TimeWindow.Yesterday ? "Yesterday counts " + whose + " whole day, also when you were not online."
                 : WindowShort(w) + " counts " + whose + " whole days, also when you were not online.";
        }
        /// <summary>The version that first shares days by weapon kind and type (OlderLine names it when a copy has only the day totals).</summary>
        public const string NeedsDayTypes = "0.9.0";

        /// <summary>The start of a window in UTC (a day window: local midnight of its first day; a short window: Cutoff; Session and All: null).</summary>
        public static DateTime? WindowStartUtc(PanelInput input, TimeWindow w)
        {
            if (!IsDayWindow(w)) return Cutoff(w, input.NowUtc);
            var localMidnight = DayFrom(input, w);
            var offset = Local(input, input.NowUtc) - input.NowUtc;   // the player's offset now (a day window is about calendar days, not exact hours)
            return DateTime.SpecifyKind(localMidnight - offset, DateTimeKind.Utc);
        }

        /// <summary>0.9.1: where a day window ends in UTC: Yesterday at this morning's local midnight; null for the windows that run to now.</summary>
        public static DateTime? WindowEndUtc(PanelInput input, TimeWindow w)
        {
            if (w != TimeWindow.Yesterday || input == null) return null;
            var offset = Local(input, input.NowUtc) - input.NowUtc;
            return DateTime.SpecifyKind(LocalToday(input) - offset, DateTimeKind.Utc);
        }
        /// <summary>0.9.1: a moment lies before the window's end (always, but for Yesterday).</summary>
        public static bool BeforeEnd(PanelInput input, TimeWindow w, DateTime utc) => !(WindowEndUtc(input, w) is DateTime end) || utc < end;

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

        /// <summary>
        /// 0.8.2: your held blocks and parries in a window of this session's log (EventLog.Guard, a minute each), as Defence reads them: a minute
        /// counts when any part of it lies in the window, Session from <paramref name="sessionFrom"/> (a fellow's book: your session's start).
        /// Null when the log keeps no minutes of them (a copy from before 0.8.2: their blocks are one untimed tally).
        /// </summary>
        public static SessionEvents GuardIn(EventLog log, TimeWindow w, DateTime nowUtc, DateTime? sessionFrom = null)
        {
            if (log == null || !log.GuardKept) return null;
            var ev = new SessionEvents();
            foreach (var kv in log.Guard)
            {
                var p = LogKeys.Of(kv.Key).P;   // minute|block or minute|parry
                if (p.Length < 2 || !InFilter(p[0], "", w, "", nowUtc, log.Span, sessionFrom)) continue;
                if (p[1] == EventLog.GuardBlock) ev.Blocks += (int)kv.Value; else if (p[1] == EventLog.GuardParry) ev.Parries += (int)kv.Value;
            }
            return ev;
        }

        /// <summary>A window with nothing in it on a page that counts days (Voyages, Deeds): says the window and where more is.</summary>
        static Block DayEmpty(PanelInput input, TimeWindow w) =>
            Empty(w == TimeWindow.Session ? NothingYet + " this session" : "Nothing " + InWindowWords(w), DayEmptyLine);
        public const string DayEmptyLine = "Choose a longer window, or All for everything.";

        /// <summary>The line of a page that leaves something out in a day window, because that thing has no days.</summary>
        /// <summary>
        /// 0.8.2 (I; Joost's friend on 0.6.5, whose Battle pages showed parts empty without a reason): what a fellow's page needs from their
        /// Hearthwoven, part by part, the version that first sent what the part reads (0.7 and 0.8.0 were never released alone: in the wild an
        /// older copy is 0.6.5 or 0.8.1):
        ///   NeedsLive (0.7): 10 min to 3 h on Battle (live updates, FellowShortWhy); NeedsFoeCounts (0.8.0): the "×N" counts and marks on Damage >
        ///   By foe and Defense; NeedsFeed (0.8.2): Battle > Feed and Last fight; NeedsGuard (0.8.2): blocks and parries in a fellow's Session.
        /// A page whose part is missing for that reason says it once, in one form (OlderLine). Foes, Overview and Deaths read only the minute log
        /// and the totals, which 0.6.5 already sends: they need nothing newer.
        /// </summary>
        public const string NeedsLive = "0.7.0", NeedsFoeCounts = "0.8.0", NeedsFeed = "0.8.2", NeedsGuard = "0.8.2";

        /// <summary>A fellow's copy comes from a Hearthwoven older than <paramref name="need"/> (a copy that names no version, or one that cannot be read,
        /// is not called older).</summary>
        public static bool OlderThan(PanelInput i, string need) => i != null && !i.IsSelf && VersionOf(i.ModVersion) is System.Version v && VersionOf(need) is System.Version n && v < n;
        static System.Version VersionOf(string s)
        {
            var digits = new string((s ?? "").TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()).Trim('.');   // "0.8.2-rc1" -> "0.8.2"
            return System.Version.TryParse(digits.Contains(".") ? digits : digits + ".0", out var v) ? v : null;
        }

        /// <summary>The one line a fellow's page says when part of it waits for their Hearthwoven (0.8.2, I): "Tor's Hearthwoven is 0.6.5: this page
        /// fills in once they update." <paramref name="fills"/>: what fills in, with its verb ("the ×N counts fill in").</summary>
        public static string OlderLine(PanelInput i, string fills = "this page fills in") =>
            Name(i) + "'s Hearthwoven is " + (string.IsNullOrEmpty(i?.ModVersion) ? "older" : i.ModVersion) + ": " + fills + " once they update.";

        /// <summary>A fellow's Session on Defense when their copy keeps no minutes of their blocks (GuardIn null: a Hearthwoven before 0.8.2).</summary>
        public static string FellowBlocksAll(PanelInput i) => OlderLine(i, "blocks and parries here fill in");
        public const string NoDaysServerBook = "The server's cargo book has no days: choose All to see it.",
                            ClippedLine = "A very full day kept only its largest kinds: a few small ones may be missing here.",
                            ClippedMinuteLine = "A very busy minute kept only its largest kinds: a few small ones may be missing here.";
        /// <summary>The clipped line of a window: a day row's or (a short window) a minute's.</summary>
        public static string ClippedLineOf(DayView w) => w != null && w.Short ? ClippedMinuteLine : ClippedLine;
    }
}
