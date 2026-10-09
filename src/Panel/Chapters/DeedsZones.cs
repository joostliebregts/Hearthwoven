using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The shared helper behind the Deeds twins (Joost 2026-10-09: every Deeds page has the two zones Woodcutting and Mining
    /// have, "Your character" = the game's count and "Since install, this PC" = Hearthwoven's).
    ///
    /// Where the game's own counter is complete (building pieces and groundwork, crafting, fishing, harvest, taming care),
    /// "since install" is that counter now minus what it stood at when Hearthwoven first ran (LocalTotals.Baseline, taken once
    /// per kind in Plugin.LoadLocal; LocalTotals.Since is the arithmetic). Where Hearthwoven counts exactly itself (repairs,
    /// planted, axe and pickaxe hits) the page keeps that. The layers of one count: before = the baseline (faded), since =
    /// now minus baseline (solid); before + since = the game's counter now, so a since-install number never exceeds the
    /// character's total. No baseline of a kind (a fellow's copy, totals not loaded) = no twin number, nothing guessed.
    ///
    /// A counter baselined later than the install (an older install on the first run of the version that baselines it) is
    /// said in the zone line: Began sets PanelView.EmberFrom, ZonesModel.EmberZone words it. Cooking (cooking-06) uses the same
    /// helpers: CounterLayers over the item-craft counter, Began with its kind.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>True when this is your own book and totals hold a baseline of that kind: the page can show a since-install twin.</summary>
        public static bool HasBaseline(PanelInput i, string kind) =>
            i != null && i.IsSelf && i.Baseline != null && kind != null && i.Baseline.TryGetValue(kind, out var b) && b != null;

        /// <summary>Since install of one game counter: its value now (<paramref name="gameNow"/>) minus the baseline; null = no baseline.</summary>
        public static double? SinceInstall(PanelInput i, string kind, string token, double gameNow) =>
            HasBaseline(i, kind) ? LocalTotals.Since(i.Baseline, kind, token, gameNow) : null;

        /// <summary>One of the game's stats in its two layers (before install faded, since install solid); null = no twin for it.</summary>
        public static (double before, double since)? StatLayers(PanelInput i, string stat)
        {
            var now = C(i, stat);
            var since = SinceInstall(i, LocalTotals.StatsKind, stat, now);
            return since.HasValue ? (now - since.Value, since.Value) : ((double, double)?)null;
        }

        /// <summary>Several stats added up, each in its layers; null when none has a twin.</summary>
        public static (double before, double since)? StatLayers(PanelInput i, params string[] stats)
        {
            var parts = stats.Select(s => StatLayers(i, s)).ToList();
            if (parts.Any(p => !p.HasValue)) return null;
            return (parts.Sum(p => p.Value.before), parts.Sum(p => p.Value.since));
        }

        /// <summary>
        /// A per-token game counter (item crafts, pieces placed, picked plants) in its two layers, for the tokens
        /// <paramref name="keep"/> accepts that the counter holds now; null = no baseline of that kind (no twin). before + since
        /// = the counter now, per token and in total.
        /// </summary>
        public static Dictionary<string, (double before, double since)> CounterLayers(PanelInput i, string kind, IDictionary<string, float> counter, Func<string, bool> keep = null)
        {
            if (!HasBaseline(i, kind)) return null;
            var d = new Dictionary<string, (double before, double since)>();
            foreach (var kv in counter ?? new Dictionary<string, float>())
            {
                if (kv.Value <= 0 || (keep != null && !keep(kv.Key))) continue;
                var since = LocalTotals.Since(i.Baseline, kind, kv.Key, kv.Value) ?? 0;
                d[kv.Key] = (kv.Value - since, since);
            }
            return d;
        }

        /// <summary>The since-install parts of <see cref="CounterLayers"/> alone, the nonzero ones, for a ranking or a bar.</summary>
        public static Dictionary<string, double> SinceOnly(Dictionary<string, (double before, double since)> layers) =>
            (layers ?? new Dictionary<string, (double before, double since)>()).Where(kv => kv.Value.since > 0).ToDictionary(kv => kv.Key, kv => kv.Value.since);

        /// <summary>
        /// A layered number on a block: both layers when the count has some of each (faded + solid, the number itself stays
        /// their sum). Only one layer: a plain number. Returns true when it layered the block.
        /// </summary>
        public static bool Layer(Block b, double before, double since)
        {
            if (b == null || before <= 0 || since <= 0) return false;
            b.Faded = N(before); b.Solid = N(since);
            return true;
        }

        /// <summary>
        /// Says on the page which counts began later than the install: the latest date among the kinds this page's twin reads
        /// (LocalTotals.BaselineAt). A kind without a date counts from the install. The ember zone's line then reads "Counting since
        /// 9 Oct, when Hearthwoven started these counts."
        /// </summary>
        public static void Began(PanelView view, PanelInput i, params string[] kinds)
        {
            if (view == null || i?.BaselineAt == null) return;
            DateTime? latest = null;
            foreach (var k in kinds) if (i.BaselineAt.TryGetValue(k, out var at) && (!latest.HasValue || at > latest.Value)) latest = at;
            if (latest.HasValue) view.EmberFrom = latest;
        }

        /// <summary>The ember zone's own number of a page: a hero of since-install numbers, each with the pc mark. Numbers of 0 drop out.</summary>
        static Block SinceHero(params (double v, string one, string many)[] numbers) =>
            Hero(numbers.Select(n => (n.v > 0 ? N(n.v) : null, Label1(n.v, n.one, n.many), SrcPc, (string)null)).ToArray());
    }
}
