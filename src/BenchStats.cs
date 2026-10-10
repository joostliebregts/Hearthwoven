using System;
using System.Collections.Generic;

namespace Hearthwoven
{
    /// <summary>
    /// One measured series for the page benches (0.8 performance pass): the offline bench in the panel test harness
    /// (test-panel --bench) and Dev.Bench in game (Panel/PanelBench.cs) both keep their numbers here and write them the same
    /// way: p50, p95 and max (nearest rank on the sorted values), the mean and the count, numbers in invariant culture
    /// (Json.F). Pure C#, never touches the game. NaN and infinities are not added.
    /// </summary>
    public sealed class BenchSeries
    {
        readonly List<double> values = new List<double>();
        double[] sorted;

        public int Count => values.Count;

        public void Add(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return;
            values.Add(v); sorted = null;
        }

        double[] Sorted()
        {
            if (sorted != null) return sorted;
            sorted = values.ToArray(); Array.Sort(sorted);
            return sorted;
        }

        /// <summary>The p-th percentile by nearest rank (p in 0..100): the smallest value with at least p % of the values at or below it; 0 when empty.</summary>
        public double Percentile(double p)
        {
            var s = Sorted(); if (s.Length == 0) return 0;
            var rank = (int)Math.Ceiling(Math.Max(0, Math.Min(100, p)) / 100.0 * s.Length);
            return s[Math.Max(0, Math.Min(s.Length - 1, rank - 1))];
        }

        public double Max { get { var s = Sorted(); return s.Length == 0 ? 0 : s[s.Length - 1]; } }
        public double Mean { get { if (values.Count == 0) return 0; double t = 0; foreach (var v in values) t += v; return t / values.Count; } }
        public double Sum { get { double t = 0; foreach (var v in values) t += v; return t; } }

        /// <summary>{"p50":..,"p95":..,"max":..,"mean":..,"n":..} under <paramref name="key"/>.</summary>
        public Json Write(Json j, string key) =>
            j.Key(key).Open().Num("p50", Percentile(50)).Num("p95", Percentile(95)).Num("max", Max).Num("mean", Mean).Num("n", Count).Close();
    }
}
