using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The game's own counters split at the baseline Hearthwoven took when it first read them (LocalTotals.Baseline, once per kind in
    /// Plugin.LoadLocal; LocalTotals.Since is the arithmetic): "before" = the counter at the baseline, "since" = now minus that, so
    /// before + since = the counter now and a count since install never exceeds the character's total. No baseline of a kind (a
    /// fellow's copy, totals not loaded) = null, nothing guessed. The Deeds pages read these for their "earlier counts" and their day
    /// windows (once the zones' helpers, DeedsZones.cs, removed with the zones in 0.7).
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>True when this is your own book and totals hold a baseline of that kind.</summary>
        public static bool HasBaseline(PanelInput i, string kind) =>
            i != null && i.IsSelf && i.Baseline != null && kind != null && i.Baseline.TryGetValue(kind, out var b) && b != null;

        /// <summary>Since install of one game counter: its value now (<paramref name="gameNow"/>) minus the baseline; null = no baseline.</summary>
        public static double? SinceInstall(PanelInput i, string kind, string token, double gameNow) =>
            HasBaseline(i, kind) ? LocalTotals.Since(i.Baseline, kind, token, gameNow) : null;

        /// <summary>One of the game's stats in its two layers (before the baseline, since it); null = no baseline.</summary>
        public static (double before, double since)? StatLayers(PanelInput i, string stat)
        {
            var now = C(i, stat);
            var since = SinceInstall(i, LocalTotals.StatsKind, stat, now);
            return since.HasValue ? (now - since.Value, since.Value) : ((double, double)?)null;
        }

        /// <summary>Several stats added up, each in its layers; null when one has no baseline.</summary>
        public static (double before, double since)? StatLayers(PanelInput i, params string[] stats)
        {
            var parts = stats.Select(s => StatLayers(i, s)).ToList();
            if (parts.Any(p => !p.HasValue)) return null;
            return (parts.Sum(p => p.Value.before), parts.Sum(p => p.Value.since));
        }

        /// <summary>
        /// A per-token game counter (item crafts, pieces placed, picked plants) in its two layers, for the tokens
        /// <paramref name="keep"/> accepts that the counter holds now; null = no baseline of that kind. before + since
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
    }
}
