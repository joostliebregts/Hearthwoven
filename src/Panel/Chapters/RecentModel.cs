using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The read API of Deeds > Recent and of Since you were away (0.7; no page here, the pages read these). "What grew" per DeedLog family
    /// (pickedUp, made, planted, treesFelled, chopHits, pickaxeHits, skillPractice, placed, picked, kills, counters), token -> amount, only
    /// what grew. Your own: the short windows and Session from this session's DeedLog (minute buckets), the day windows from the day history
    /// (InWindow, the running session's unsaved part included), All from the since-install tallies. A fellow's (B33, Joost 2026-10-10: "Session"
    /// is YOUR session's span): Session and the short windows from what reached this PC from them while you played (FellowTrail), the short
    /// ones only when their copies come timed (live updates); All from their shared since-install totals; days are not shared. Since last time
    /// from their copy now minus the mark this PC kept (FellowMarks).
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>What grew in one window, for one player.</summary>
        public class RecentView
        {
            /// <summary>The window; null = Since last time.</summary>
            public TimeWindow? Window;
            /// <summary>False: this window cannot be told for this player; Why says so in a line for the player.</summary>
            public bool Shown = true;
            public string Why;
            /// <summary>Not shown, said on the player's own row of The group instead of "Not recorded" (and no Why line above the rows).</summary>
            public string Row;
            /// <summary>From when to when (UTC); From null = the start of the session (when not known).</summary>
            public DateTime? FromUtc;
            public DateTime ToUtc;
            /// <summary>family -> token -> amount that grew (above 0). A family with nothing is absent.</summary>
            public readonly Dictionary<string, Dictionary<string, float>> Grew = new Dictionary<string, Dictionary<string, float>>();
            /// <summary>Families this view cannot tell (not 0: not recorded), e.g. kills per foe in a day window, a fellow's game counters in Session.</summary>
            public readonly List<string> NotRecorded = new List<string>();
            /// <summary>Families whose running total went down (a fellow's count restarted: reinstall, another PC): left out, never negative.</summary>
            public readonly List<string> Restarted = new List<string>();
            /// <summary>A list may miss its smallest kinds (a capped minute, a clipped day row or mark); DeedLog.OtherToken holds the rest.</summary>
            public bool Clipped;
            public float Total(string family) => Grew.TryGetValue(family, out var d) ? d.Values.Sum() : 0f;
            /// <summary>Shown and nothing grew: "nothing" is recorded, so it is true.</summary>
            public bool Nothing => Shown && Grew.Count == 0;
        }

        /// <summary>The chips of Deeds > Recent: Battle's whole set (B33, Joost 2026-10-10; "Since last time" is the Since you were away page's, not a chip here).</summary>
        public static TimeWindow[] RecentWindows => AllWindows;   // a property: AllWindows is set in another part of this class (WindowsModel.cs)

        /// <summary>The lines a fellow's window says when it cannot be told (B33).</summary>
        public const string FellowNoDays = "Other players' days are not shared: choose Session or All for them.", RecentNotOn = "not on this session";
        // a fellow's book never says "you" (CoherenceWorld): the start of your session is said as its time, "since 21:40"
        public static string FellowNotTimed(PanelInput f) => f != null && f.ServerLive ? Name(f) + "'s Hearthwoven is older: choose Session."   // the server is live, their copy is not
                                                                                   : Name(f) + "'s numbers arrive only every few minutes on this server: choose Session.";
        public static string FellowNotOn(PanelInput f) => Name(f) + " has not played " + SinceViewer(f) + ".";
        public static string FellowWaiting(PanelInput f) => "Waiting for " + Name(f) + "'s numbers from the server.";
        /// <summary>"since 21:40": when the viewer's session began, in the viewer's clock (a fellow's Session counts from there).</summary>
        public static string SinceViewer(PanelInput f) => f?.ViewerSessionStartUtc != null ? "since " + Local(f, f.ViewerSessionStartUtc.Value).ToString("HH:mm", Inv) : "since this session began";

        /// <summary>Whether a Recent window can be chosen on this book: yours as every page (WindowOpen); a fellow's Session, their All when they
        /// share since-install totals, the short windows when their copies come timed (B33); never their days.</summary>
        public static bool RecentOpen(PanelInput input, TimeWindow w) =>
            input != null && (input.IsSelf ? WindowOpen(input, w) : w == TimeWindow.Session || (w == TimeWindow.SinceInstall && input.SharedSinceInstall) || (IsShortWindow(w) && input.Timed));

        /// <summary>What grew in a window: your own book (input.IsSelf), else RecentOfFellow.</summary>
        public static RecentView RecentOf(PanelInput input, TimeWindow w)
        {
            if (input == null) return new RecentView { Window = w, Shown = false, Why = "Nothing to show." };
            if (!input.IsSelf) return RecentOfFellow(input, w);
            var v = new RecentView { Window = w, ToUtc = input.NowUtc };
            if (IsDayWindow(w))
            {
                var c = InWindow(input, w);
                if (c == null) { v.Shown = false; v.Why = WaitLine(input, w) ?? "Your days are not recorded on this PC yet."; return v; }
                v.FromUtc = WindowStartUtc(input, w); v.Clipped = c.Window != null && c.Window.Clipped;
                foreach (var fam in c.Events.Named()) if (Array.IndexOf(DeedLog.EventFamilies, fam.Key) >= 0) Put(v, fam.Key, fam.Value);
                Put(v, DeedLog.Placed, c.PiecesPlaced); Put(v, DeedLog.Picked, c.Harvested);
                Put(v, DeedLog.Counters, c.Character?.Where(kv => Array.IndexOf(DeedLog.CounterStats, kv.Key) >= 0));
                v.NotRecorded.Add(DeedLog.Kills);   // kills per foe have no days (only the kill counter's growth, under counters)
                return v;
            }
            if (w == TimeWindow.SinceInstall) return RecentSinceInstall(input, v);
            if (input.Deeds == null) { v.Shown = false; v.Why = "Recorded from your next session on."; return v; }
            var cutoff = Cutoff(w, input.NowUtc);
            v.FromUtc = cutoff ?? input.SessionStartUtc;
            foreach (var kv in input.Deeds.Grew(cutoff)) Put(v, kv.Key, kv.Value);
            v.Clipped = input.Deeds.Clipped;
            return v;
        }

        /// <summary>
        /// All on your own book: what Hearthwoven counted since install (the since-install tallies), the pieces and plants beside them from the
        /// game's counters minus their baseline (when it was taken); the kills have no since-install count per foe (Not recorded, never 0).
        /// </summary>
        static RecentView RecentSinceInstall(PanelInput input, RecentView v)
        {
            if (input.Events == null) { v.Shown = false; v.Why = NothingYet; return v; }
            v.FromUtc = input.InstalledUtc;
            foreach (var fam in input.Events.Named()) if (Array.IndexOf(DeedLog.EventFamilies, fam.Key) >= 0) Put(v, fam.Key, fam.Value);
            void Game(string family, IDictionary<string, float> now, string kind)
            {
                if (now == null || input.Baseline == null || !input.Baseline.TryGetValue(kind, out var at) || at == null) { v.NotRecorded.Add(family); return; }
                Put(v, family, now.Select(kv => new KeyValuePair<string, float>(kv.Key, kv.Value - (at.TryGetValue(kv.Key, out var b) ? b : 0f))));
            }
            Game(DeedLog.Placed, input.PiecesPlaced, LocalTotals.PlacedKind);
            Game(DeedLog.Picked, input.Harvested, LocalTotals.PickablesKind);
            v.NotRecorded.Add(DeedLog.Kills); v.NotRecorded.Add(DeedLog.Counters);
            return v;
        }

        /// <summary>
        /// What grew for a fellow player (B33). Session: from your session's start (ViewerSessionStartUtc) to their latest copy, as it reached this
        /// PC (FellowTrail: what they did before you came in is never counted; a fellow who was not in the world during your session says so).
        /// The short windows the same, only when their copies come timed (live updates every 10 s; full copies every few minutes are too coarse
        /// for 10 minutes). All: their shared since-install totals. The day windows: not shared. Since last time via SinceLastTimeOfFellow.
        /// </summary>
        public static RecentView RecentOfFellow(PanelInput fellow, TimeWindow w)
        {
            var v = new RecentView { Window = w, ToUtc = fellow?.Trail?.LastUtc ?? fellow?.ReceivedUtc ?? fellow?.NowUtc ?? DateTime.UtcNow };
            if (fellow == null) { v.Shown = false; v.Why = "Nothing to show."; return v; }
            if (IsDayWindow(w)) { v.Shown = false; v.Why = FellowNoDays; return v; }
            if (w == TimeWindow.SinceInstall)
            {
                if (!fellow.SharedSinceInstall || fellow.Events == null) { v.Shown = false; v.Why = Name(fellow) + "'s Hearthwoven is older: it shares no totals."; return v; }
                foreach (var fam in fellow.Events.Named()) if (Array.IndexOf(DeedLog.EventFamilies, fam.Key) >= 0) Put(v, fam.Key, fam.Value);
                v.NotRecorded.AddRange(DeedLog.GameFamilies);   // their game counters run from the character's start, not from install
                return v;
            }
            if (w != TimeWindow.Session && !fellow.Timed) { v.Shown = false; v.Why = FellowNotTimed(fellow); return v; }
            var trail = fellow.Trail;
            if (trail == null) { v.Shown = false; v.Why = FellowWaiting(fellow); return v; }
            var from = w == TimeWindow.Session ? fellow.ViewerSessionStartUtc : Cutoff(w, fellow.NowUtc);
            foreach (var kv in trail.Grew(from, v.Restarted)) Put(v, kv.Key, kv.Value);
            v.FromUtc = from.HasValue && from.Value > trail.FirstUtc ? from.Value : trail.FirstUtc;
            if (v.Grew.Count == 0 && fellow.OnThisSession == false) { v.Shown = false; v.Why = FellowNotOn(fellow); v.Row = RecentNotOn; }   // never as if they had been there
            return v;
        }

        /// <summary>
        /// Yours since your previous session on this PC ended (PreviousSessionEndUtc): on this PC nothing is recorded between sessions, so
        /// this is this session so far, with that end as its start. Not shown on the first session here.
        /// </summary>
        public static RecentView SinceLastTime(PanelInput input)
        {
            if (input != null && !input.IsSelf) return SinceLastTimeOfFellow(input);
            var v = RecentOf(input, TimeWindow.Session);
            v.Window = null;
            if (input?.PreviousSessionEndUtc == null) { v.Grew.Clear(); v.Shown = false; v.Why = "Your first session with Hearthwoven on this PC: from now on."; return v; }
            v.FromUtc = input.PreviousSessionEndUtc;
            return v;
        }

        /// <summary>
        /// A fellow's since last time: their copy now minus their mark as this PC last saw it before this session (FellowMarks.Before), per
        /// token, never below 0. The event families need since-install totals on both sides (else NotRecorded); a family whose sum went down
        /// is Restarted and left out; a token missing from a clipped mark family is left out (Clipped). From = when the mark's copy arrived.
        /// </summary>
        public static RecentView SinceLastTimeOfFellow(PanelInput fellow)
        {
            var v = new RecentView { Window = null, ToUtc = fellow?.ReceivedUtc ?? fellow?.NowUtc ?? DateTime.UtcNow };
            var mark = fellow?.SeenBefore;
            if (fellow == null || mark == null) { v.Shown = false; v.Why = fellow == null ? "Nothing to show." : "Your PC sees " + fellow.PlayerName + "'s record for the first time: from now on."; return v; }
            v.FromUtc = mark.SeenUtc;
            var was = new Dictionary<string, Dictionary<string, float>>();
            foreach (var kv in mark.Values)
            {
                var bar = kv.Key.IndexOf('|'); if (bar <= 0) continue;
                var fam = kv.Key.Substring(0, bar);
                if (!was.TryGetValue(fam, out var d)) was[fam] = d = new Dictionary<string, float>();
                d[kv.Key.Substring(bar + 1)] = kv.Value;
            }
            if (!(mark.SinceInstall && fellow.SharedSinceInstall)) v.NotRecorded.AddRange(DeedLog.EventFamilies);   // a copy without since-install totals: one session, no difference
            foreach (var kv in FellowMarks.Families(fellow))
            {
                if (v.NotRecorded.Contains(kv.Key)) continue;
                was.TryGetValue(kv.Key, out var before);
                if (before != null && kv.Value.Values.Sum() < before.Values.Sum() - 0.001f) { v.Restarted.Add(kv.Key); continue; }
                var clipped = before != null && before.Count >= FellowMarks.MaxKeysPerFamily;
                var grew = new Dictionary<string, float>();
                foreach (var t in kv.Value)
                {
                    float b = 0f;
                    if (before != null && !before.TryGetValue(t.Key, out b) && clipped) { v.Clipped = true; continue; }   // not in a clipped mark: its growth is not known
                    if (t.Value - b > 0f) grew[t.Key] = t.Value - b;
                }
                Put(v, kv.Key, grew);
            }
            return v;
        }

        /// <summary>How long you were away: from your previous session's end on this PC to this session's start; null = not known (the first
        /// session here, or the session start not seen yet). Since you were away opens by itself when this passes its threshold.</summary>
        public static TimeSpan? AwayFor(PanelInput input)
        {
            if (input == null || !input.IsSelf || !input.PreviousSessionEndUtc.HasValue || !input.SessionStartUtc.HasValue) return null;
            var away = input.SessionStartUtc.Value - input.PreviousSessionEndUtc.Value;
            return away < TimeSpan.Zero ? TimeSpan.Zero : away;
        }

        static void Put(RecentView v, string family, IEnumerable<KeyValuePair<string, float>> amounts)
        {
            if (amounts == null) return;
            Dictionary<string, float> d = null;
            foreach (var kv in amounts)
            {
                if (!(kv.Value > 0f)) continue;
                if (d == null && !v.Grew.TryGetValue(family, out d)) v.Grew[family] = d = new Dictionary<string, float>();
                d.TryGetValue(kv.Key, out var o); d[kv.Key] = o + kv.Value;
            }
        }
    }
}
