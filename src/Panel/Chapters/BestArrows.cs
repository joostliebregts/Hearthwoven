using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 arrows track (work/hearthwoven-0.9/DESIGN.md section 2, "Best arrows for these foes" and "Best arrow tie"; board 6, Arrows.dc.html).
    ///
    /// The Best arrow tie: Foes' Best arrow column named none when two arrows did the same damage to a foe (Joost in game, 18 Oct: Fallen
    /// Valkyrie, Charred Arrow 82 = Silver Arrow 82, so his own book showed no Best arrow while a friend's, with fewer arrows, showed Frost Arrow).
    /// Now a tie is named: two as "Charred or Silver Arrow" (hover: TieHover), three or more as "3 arrows tied" (hover: TieHover and their names).
    ///
    /// What you brought in (Gathered): the pickups of a window in the land where you fought (FoughtIn: most of your damage; DeedLog.Lands, the
    /// land per minute), read by the What to use view's "You can make" (Chapters/WhatToUse.cs). Pure C#: test-panel/ArrowsTests.cs, WhatToUseTests.cs.
    /// </summary>
    public static partial class PanelModel
    {
        // ---------- the tie ----------

        public const string TieTone = "tie", TieHover = "They do the same damage to this foe", TiedWord = "tied";

        /// <summary>The arrows with the highest damage against a foe after its modifiers (BestArrow's score): one when there is a best, two or more
        /// on a tie, none for an unknown foe or fewer than two arrows known (no choice to show).</summary>
        public static List<ArrowData> TopArrows(FoeData foe, IList<ArrowData> known)
        {
            if (foe == null || known == null || known.Count < 2) return new List<ArrowData>();
            double Score(ArrowData a) => a.Damage.Sum(kv => kv.Value * Multiplier(foe.Modifiers.TryGetValue(kv.Key, out var m) ? m : "Normal"));
            var ranked = known.Select(a => (a, s: Score(a))).OrderByDescending(x => x.s).ThenBy(x => x.a.Prefab, StringComparer.Ordinal).ToList();
            return ranked.Where(x => x.s > ranked[0].s - 1e-3).Select(x => x.a).ToList();
        }

        /// <summary>Two tied arrows in one name, their shared last word said once ("Charred Arrow", "Silver Arrow": "Charred or Silver Arrow"; else
        /// "A or B"); three or more: "3 arrows tied".</summary>
        public static string TiedName(IList<string> names)
        {
            if (names == null || names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            if (names.Count > 2) return names.Count + " arrows " + TiedWord;
            string a = names[0], b = names[1];
            int sa = a.LastIndexOf(' '), sb = b.LastIndexOf(' ');
            return sa > 0 && sb > 0 && string.Equals(a.Substring(sa), b.Substring(sb), StringComparison.Ordinal) ? a.Substring(0, sa) + " or " + b : a + " or " + b;
        }

        /// <summary>
        /// Foes' Best arrow cell (Kind "arrow") for a foe: the best one (Id, Icon, Title), or on a tie Tone TieTone, Title TiedName of their names
        /// (by name), Text the hover's words (TieHover; three or more: and their names), no Icon (the name starts where it stands), Items Kind "item" per tied arrow
        /// (Id, Icon, Title). null when there is no choice (TopArrows).
        /// </summary>
        public static Block ArrowCell(PanelInput input, FoeData foe, IList<ArrowData> known)
        {
            var top = TopArrows(foe, known).Select(a => (a, name: Who(input, a.Prefab))).OrderBy(x => x.name, StringComparer.Ordinal).ToList();
            if (top.Count == 0) return null;
            var first = top[0].a;
            var cell = new Block { Kind = "arrow", Id = first.Prefab, Icon = "item:" + first.Prefab, Title = top[0].name };
            if (top.Count == 1) return cell;
            var names = top.Select(x => x.name).ToList();
            cell.Tone = TieTone; cell.Title = TiedName(names); cell.Icon = null;   // no one arrow's picture for two: the name takes the icon's room
            cell.Text = names.Count == 2 ? TieHover : TieHover + ": " + string.Join(", ", names.ToArray());
            cell.Items = top.Select(x => new Block { Kind = "item", Id = x.a.Prefab, Icon = "item:" + x.a.Prefab, Title = x.name }).ToList();
            return cell;
        }

        // ---------- what you brought in ----------

        public const string GatheredNotRecorded = "Recorded from your next session on.";


        /// <summary>What was brought in for the island's right side: the land (a biome id; null = the window has no land minutes), the window's words
        /// (GatheredWhen: "this session", "last hour", "today", "all recorded"), the pickups (item token -> amount; null = not recorded on this book).</summary>
        public sealed class Gathered { public string Land, When; public IDictionary<string, float> Items; }

        /// <summary>
        /// What you brought in during a window, in the land where you fought the page's foes: the land of most of your damage in <paramref name="rows"/>
        /// (the damage log keeps the biome per hit; so a long evening at home base never wins over a short fight out there), its pickups those of the
        /// minutes you were in that land (DeedLog.Lands). 10 min .. Session: this session's minute log (from a time on with the kept minutes). No
        /// land (a day window or All, whose rows keep none; or no land minutes there yet): every pickup of the window: its days' (InWindow or
        /// <paramref name="src"/>), every pickup since install (Events), or the minute log's.
        /// </summary>
        public static Gathered GatheredIn(PanelInput input, PanelInput src, TimeWindow w, IEnumerable<DamageRow> rows)
        {
            var g = new Gathered { When = GatheredWhen(w) };
            if (IsDayWindow(w)) { var c = src?.Window != null ? src : InWindow(input, w); g.Items = c?.ItemsPickedUp; return g; }
            if (w == TimeWindow.SinceInstall) { g.Items = input.Events?.PickedUp; return g; }
            if (input.Deeds == null) return g;
            return FromLog(input.Deeds, Cutoff(w, input.NowUtc), FoughtIn(rows), g);
        }

        /// <summary>Last fight's: this session's pickups, in the fight's land (the land of most of your damage in the log's minutes of the fight).</summary>
        public static Gathered GatheredForFight(PanelInput input, DateTime fromUtc, DateTime toUtc)
        {
            var g = new Gathered { When = GatheredWhen(TimeWindow.Session) };
            if (input.Deeds == null) return g;
            var rows = input.Log == null ? new List<DamageRow>() : Damage(input.Log, TimeWindow.Session, "", input.NowUtc, SessionFrom(input))
                .Where(r => r.Bucket >= fromUtc.AddMinutes(-1) && r.Bucket <= toUtc).ToList();
            return FromLog(input.Deeds, null, FoughtIn(rows), g);
        }

        /// <summary>The land where most of your damage in the rows was dealt; a tie: the later in journey order; null when no row carries a land.</summary>
        public static string FoughtIn(IEnumerable<DamageRow> rows)
        {
            var by = DealtRows(rows ?? new List<DamageRow>()).Where(r => !string.IsNullOrEmpty(r.Biome) && r.Biome != "None")
                                                          .GroupBy(r => r.Biome).Select(gr => (land: gr.Key, v: gr.Sum(r => (double)r.Amount))).ToList();
            return by.Count == 0 ? null : by.OrderByDescending(x => x.v).ThenByDescending(x => BiomeRank(x.land)).ThenBy(x => x.land, StringComparer.Ordinal).First().land;
        }

        static Gathered FromLog(DeedLog log, DateTime? from, string land, Gathered g)
        {
            // the fought land's minutes picked up there; a land without noted minutes (a log from before 0.9) counts as none
            if (!string.IsNullOrEmpty(land) && log.LandMinutes(from).ContainsKey(land)) { g.Land = land; g.Items = log.PickedUpIn(from, land); return g; }
            g.Items = log.Grew(from).TryGetValue(DeedLog.PickedUp, out var all) ? all : new Dictionary<string, float>();
            return g;
        }

        /// <summary>The window as the right side's count says it, short enough to share the island's head with its title ("Black Forest, last 3 hours").</summary>
        public static string GatheredWhen(TimeWindow w) =>
            w == TimeWindow.LastTenMinutes ? "last 10 min" : w == TimeWindow.LastThirtyMinutes ? "last 30 min" : w == TimeWindow.SinceInstall ? "all recorded" : WindowLabel(w).ToLowerInvariant();

    }
}
