using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The battle record for the Battle pages (0.8, BACKLOG.md "Battle detail"; the recorder: src/BattleRecord.cs). Pure C#: what a page reads,
    /// no blocks. The pages build on it: Damage by foe ("×N", the pips: defeated filled, got away a ring), Defence (the same "×N" after a
    /// foe's name: the foes of the kind that hit you) and the battle feed (Log, Cards, Timeline over the same fights).
    ///
    /// Windows as the rest of Battle: 10 min .. 3 h and Session from this session's recorder (a foe counts when you hit it in the window,
    /// by the minute of your last hit, as the damage buckets), Today, 7 days and 30 days from the foes kept on this PC (FoeBook day rows
    /// plus the running session's unsaved part), All = since recording began (FoesFrom: "Recorded from"). A fellow's book: Session and All
    /// as they shared them (FoeShare), the short windows and days not (null). The damage per kind is the window's own damage rows, the very
    /// rows the Damage and Defence pages show, so a bar and its count never disagree about the window.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>One foe kind in a window: how many separate foes and what became of them, and the damage both ways by type.</summary>
        public sealed class FoeKindRow
        {
            public string Kind;                 // the foe's prefab ("Troll"), the key of every Battle page
            /// <summary>Whether the window has counts at all (false: a fellow's short window, an older fellow's copy): then no "×N", not "×0".</summary>
            public bool Counted;
            /// <summary>"×N" on the Damage page: separate foes of the kind you fought in the window (FoeCounts.Count.Separate).</summary>
            public int Count;
            public int Defeated, DefeatedWith, GotAway, Fighting;
            /// <summary>"×N" on the Defence page: separate foes of the kind that hit you in the window.</summary>
            public int HitYou;
            /// <summary>Damage type -> amount: Dealt in the eight battle types (as Damage), Received after your armour (as Defence's Received from,
            /// with raw damage under "damage").</summary>
            public readonly Dictionary<string, double> Dealt = new Dictionary<string, double>(), Received = new Dictionary<string, double>();
            public double DealtTotal, ReceivedTotal;
        }

        /// <summary>When the foes began to be recorded (the "Recorded from" date of the counts; yourself: this PC, a fellow: theirs); null = not known.</summary>
        public static DateTime? FoesFrom(PanelInput input) => input?.FoesFromUtc;

        /// <summary>
        /// The foes per kind in a window, or null when the window has none recorded (a fellow's short or day window; your day window before the
        /// day history; no recorder). Fighting is the running session's (a foe still in the fight is neither defeated nor got away).
        /// </summary>
        public static FoeCounts FoeCountsIn(PanelInput input, TimeWindow w)
        {
            if (input == null) return null;
            if (!input.IsSelf)
            {
                if (w == TimeWindow.Session && FellowFoesBeforeYou(input)) return null;   // REVIEW-08 #2: their tally would reach back before your session
                return w == TimeWindow.Session ? input.FoesSession : w == TimeWindow.SinceInstall ? input.FoesSince : null;
            }
            var live = input.Battle?.Counts(null);
            if (IsShortWindow(w)) return w == TimeWindow.Session ? live : input.Battle?.Counts(Cutoff(w, input.NowUtc));
            if (w == TimeWindow.SinceInstall) return WithFighting(input.FoesSince ?? live, live);
            if (input.FoeBook == null || !DayOpen(input, w)) return null;
            var sum = input.FoeBook.Sum(DayFrom(input, w), LocalToday(input));
            if (input.FoesPending != null) sum.AddAll(input.FoesPending);   // the running session since the last save: always today
            return WithFighting(sum, live);
        }

        /// <summary>
        /// REVIEW-08 #2 (as REVIEW-07 #2): a fellow's Session counts are one tally of their whole connection, with no times, while on their book
        /// Session starts at yours (SessionFrom). The counts are shown only when everything their log holds lies inside your session (its first
        /// bucket reaches into it: their connection began after yours, or they did nothing before you came in). Otherwise no "×N" and no marks,
        /// and one line says the counts show in All (FellowCountsAll). False on your own book and in every other window.
        /// </summary>
        public static bool FellowFoesBeforeYou(PanelInput input)
        {
            var from = SessionFrom(input);
            if (!from.HasValue || input.FoesSession == null) return false;
            return !input.FirstRecordedUtc.HasValue || input.FirstRecordedUtc.Value.AddMinutes(SpanOf(input)) <= from.Value;
        }

        /// <summary>The line where a fellow's Session leaves their counts out (FellowFoesBeforeYou).</summary>
        public static string FellowCountsAll(PanelInput input) => Name(input) + " was playing before you came in, so the counts per foe show in All.";

        /// <summary>A copy of saved counts with the running session's foes still in a fight (never saved) put back, so GotAway leaves them out.</summary>
        static FoeCounts WithFighting(FoeCounts saved, FoeCounts live)
        {
            if (saved == null) return null;
            var c = FoeCounts.Sum(saved);
            foreach (var kv in c.Kinds) kv.Value.Fighting = 0;
            if (live != null) foreach (var kv in live.Kinds) if (kv.Value.Fighting > 0) c.Of(kv.Key).Fighting = kv.Value.Fighting;
            return c;
        }

        /// <summary>
        /// Every foe kind of a window, with its count and its damage both ways: the kinds you fought or that hit you (counted), and the kinds the
        /// window's damage rows have without a count (damage from before the foes were recorded: Counted true, Count 0; a player in a duel).
        /// Most damage dealt first, then most received, then by name.
        /// </summary>
        public static List<FoeKindRow> FoeKinds(PanelInput input, TimeWindow w)
        {
            var rows = new Dictionary<string, FoeKindRow>(StringComparer.Ordinal);
            if (input == null) return new List<FoeKindRow>();
            var counts = FoeCountsIn(input, w);
            FoeKindRow Row(string k) { if (!rows.TryGetValue(k, out var r)) rows[k] = r = new FoeKindRow { Kind = k, Counted = counts != null }; return r; }
            if (counts != null)
                foreach (var kv in counts.Kinds)
                {
                    var c = kv.Value; if (c.Empty) continue;
                    var r = Row(kv.Key);
                    r.Count = c.Separate; r.Defeated = c.Defeated; r.DefeatedWith = c.With; r.GotAway = c.GotAway; r.Fighting = c.Fighting; r.HitYou = c.HitYou;
                }
            foreach (var d in WindowDamageRows(input, w))
            {
                if (d.Amount <= 0 || string.IsNullOrEmpty(d.Other)) continue;
                if (d.Dir == "dealt" && BattleTypes.Contains(d.Type)) { var r = Row(d.Other); Bump(r.Dealt, d.Type, d.Amount); r.DealtTotal += d.Amount; }
                else if (d.Dir == "taken" && ReceivedTypes.Contains(d.Type)) { var r = Row(d.Other); Bump(r.Received, d.Type, d.Amount); r.ReceivedTotal += d.Amount; }
            }
            return rows.Values.OrderByDescending(r => r.DealtTotal).ThenByDescending(r => r.ReceivedTotal).ThenBy(r => r.Kind, StringComparer.Ordinal).ToList();
        }

        /// <summary>The damage rows of a window as the Damage and Defence pages read them: All the since-install totals, a day window its copy's rows,
        /// a short window and Session the log's buckets.</summary>
        static List<DamageRow> WindowDamageRows(PanelInput input, TimeWindow w)
        {
            if (w == TimeWindow.SinceInstall) return DamageSinceInstallRows(input);
            if (IsDayWindow(w)) { var c = InWindow(input, w); return c == null ? new List<DamageRow>() : DamageSinceInstallRows(c); }
            return Damage(input.Log, w, "", input.NowUtc, SessionFrom(input));   // a fellow's Session from yours, as the pages (B33)
        }

        /// <summary>The battle feed: your fights this session, newest first, each with its foes newest first (at most BattleRecorder.MaxFeed foes
        /// over all fights). Your own book only (a fellow's copy carries no feed): null. <paramref name="maxFights"/> above 0: only that many.</summary>
        public static List<FoeFight> BattleFeed(PanelInput input, int maxFights = 0)
        {
            if (input == null || !input.IsSelf || input.Battle == null) return null;
            var all = input.Battle.Fights();
            return maxFights > 0 && all.Count > maxFights ? all.Take(maxFights).ToList() : all;
        }
    }
}
