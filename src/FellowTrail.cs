using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven.Panel;

namespace Hearthwoven
{
    /// <summary>
    /// What a fellow did while you played (0.7, FEEDBACK B33): Deeds > Recent counts a fellow only inside YOUR session's span. A shared copy
    /// carries running totals, not times, so the times are when this PC received each copy this connection: a trail of samples of their
    /// running totals (FellowMarks.Families: the event families since install, else their one-session tally; the game's counters always).
    /// A window's growth = the latest sample minus the sample at the window's start (the latest one at or before it; none: the first sample
    /// this connection, so what they did before you came in is never counted). A copy loaded from this PC's cache (FellowCache) is no sample:
    /// only what the server sent this connection is timed.
    ///
    /// Bounded: a sample keeps only the values that differ from the first one (a session touches few kinds), and older samples are thinned
    /// (the last one per minute for 35 minutes, per 5 minutes up to 65, per 15 minutes up to 3 h 20; older ones go, the first stays), so a
    /// fellow live-updating every 10 s for hours holds about 50 small samples. Pure C#: tested without the game (RecentPageTests).
    /// </summary>
    public class FellowTrail
    {
        class Sample { public DateTime Utc; public Dictionary<string, float> Diff; }

        readonly Dictionary<string, float> first = new Dictionary<string, float>();   // "family|token" -> running total, the first sample
        readonly List<Sample> later = new List<Sample>();
        public DateTime FirstUtc { get; private set; }
        public DateTime LastUtc => later.Count > 0 ? later[later.Count - 1].Utc : FirstUtc;
        /// <summary>The first sample's copy carried since-install totals (the event families are running totals across their sessions).</summary>
        public bool SinceInstall { get; private set; }
        /// <summary>Something they count grew since the first sample this connection.</summary>
        public bool Moved => later.Any(s => s.Diff.Count > 0);

        /// <summary>A trail starting with one copy as it was received at <paramref name="utc"/>.</summary>
        public FellowTrail(PanelInput copy, DateTime utc) : this(Flat(copy), copy?.SharedSinceInstall ?? false, utc) { }

        /// <summary>A trail starting with running totals as Flat gives them (the sample world builds its fellows' evenings this way).</summary>
        public FellowTrail(Dictionary<string, float> flat, bool sinceInstall, DateTime utc)
        {
            FirstUtc = utc; SinceInstall = sinceInstall;
            foreach (var kv in flat ?? new Dictionary<string, float>()) first[kv.Key] = kv.Value;
        }

        /// <summary>The running totals of one copy, flat as "family|token": FellowMarks.Families, and for a copy without since-install totals
        /// its one-session tally (differences within one of their sessions; a new session of theirs restarts it, which Grew leaves out).</summary>
        public static Dictionary<string, float> Flat(PanelInput copy)
        {
            var d = new Dictionary<string, float>();
            if (copy == null) return d;
            var fams = FellowMarks.Families(copy);
            if (!copy.SharedSinceInstall && copy.SessionOnly != null)
                foreach (var fam in copy.SessionOnly.Named()) if (Array.IndexOf(DeedLog.EventFamilies, fam.Key) >= 0 && fam.Value.Count > 0) fams[fam.Key] = new Dictionary<string, float>(fam.Value);
            foreach (var f in fams) foreach (var t in f.Value) d[f.Key + "|" + t.Key] = t.Value;
            return d;
        }

        /// <summary>A copy received at <paramref name="utc"/> (later than the previous one).</summary>
        public void Add(PanelInput copy, DateTime utc) { if (copy != null) Add(Flat(copy), utc); }

        /// <summary>Running totals (as Flat gives them) received at <paramref name="utc"/>.</summary>
        public void Add(Dictionary<string, float> flat, DateTime utc)
        {
            if (flat == null || utc < LastUtc) return;
            var diff = new Dictionary<string, float>();
            foreach (var kv in flat) if (!first.TryGetValue(kv.Key, out var f) || Math.Abs(f - kv.Value) > 0.0001f) diff[kv.Key] = kv.Value;
            var last = later.Count > 0 ? later[later.Count - 1].Diff : null;
            if (last != null && last.Count == diff.Count && diff.All(kv => last.TryGetValue(kv.Key, out var v) && v == kv.Value)) return;   // the same totals: nothing new to time
            if (last == null && diff.Count == 0) return;
            later.Add(new Sample { Utc = utc, Diff = diff });
            Thin(utc);
        }

        // the last sample per bucket, the bucket wider the older it is; the newest always stays
        void Thin(DateTime now)
        {
            var keep = new List<Sample>(); var seen = new HashSet<long>();
            for (int i = later.Count - 1; i >= 0; i--)
            {
                var s = later[i];
                if (i == later.Count - 1) { keep.Add(s); seen.Add(Bucket(s.Utc, now)); continue; }
                var b = Bucket(s.Utc, now);
                if (b == long.MinValue || !seen.Add(b)) continue;
                keep.Add(s);
            }
            keep.Reverse();
            later.Clear(); later.AddRange(keep);
        }

        static long Bucket(DateTime utc, DateTime now)
        {
            var age = (now - utc).TotalMinutes;
            long width = age <= 35 ? 1 : age <= 65 ? 5 : age <= 200 ? 15 : 0;
            if (width == 0) return long.MinValue;   // older than any window needs: gone (the first sample stays for Session)
            return (utc.Ticks / TimeSpan.TicksPerMinute / width) * 4 + (width == 1 ? 1 : width == 5 ? 2 : 3);   // one bucket per width and slot, never shared
        }

        /// <summary>The value of a key in a sample (null = the first).</summary>
        float ValueIn(Sample s, string key) => s != null && s.Diff.TryGetValue(key, out var v) ? v : first.TryGetValue(key, out var f) ? f : 0f;

        /// <summary>
        /// What grew from <paramref name="fromUtc"/> to the latest sample, per family (token -> amount, only above 0): the base is the latest
        /// sample at or before fromUtc, else the first sample. A family whose sum went down (they restarted a count) is left out and named in
        /// <paramref name="restarted"/>.
        /// </summary>
        public Dictionary<string, Dictionary<string, float>> Grew(DateTime? fromUtc, List<string> restarted)
        {
            Sample baseline = null;
            if (fromUtc.HasValue) foreach (var s in later) if (s.Utc <= fromUtc.Value) baseline = s;
            var latest = later.Count > 0 ? later[later.Count - 1] : null;
            var keys = new HashSet<string>();
            if (latest != null) keys.UnionWith(latest.Diff.Keys);
            if (baseline != null) keys.UnionWith(baseline.Diff.Keys);
            var grew = new Dictionary<string, Dictionary<string, float>>(); var sums = new Dictionary<string, float>();
            foreach (var k in keys)
            {
                var bar = k.IndexOf('|'); if (bar <= 0) continue;
                var fam = k.Substring(0, bar);
                var d = ValueIn(latest, k) - ValueIn(baseline, k);
                sums.TryGetValue(fam, out var sum); sums[fam] = sum + d;
                if (d <= 0f) continue;
                if (!grew.TryGetValue(fam, out var g)) grew[fam] = g = new Dictionary<string, float>();
                g[k.Substring(bar + 1)] = d;
            }
            foreach (var kv in sums) if (kv.Value < -0.001f) { grew.Remove(kv.Key); restarted?.Add(kv.Key); }
            return grew;
        }
    }

    /// <summary>Client: each fellow's trail this connection, by fellow key (GroupShare.Trails); cleared with the connection.</summary>
    public class FellowTrails
    {
        readonly Dictionary<string, FellowTrail> trails = new Dictionary<string, FellowTrail>();
        public FellowTrail Of(string key) => key != null && trails.TryGetValue(key, out var t) ? t : null;

        /// <summary>A copy the server sent (or its live update applied), received at <paramref name="utc"/>.</summary>
        public void Add(string key, string json, DateTime utc)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(json)) return;
            var copy = FellowCopies.Of(key, json);   // 0.8: read once, the panel reads the same (FellowCopies.cs)
            if (copy == null) return;
            if (trails.TryGetValue(key, out var t)) t.Add(copy, utc); else trails[key] = new FellowTrail(copy, utc);
        }

        /// <summary>A copy's running totals already flattened (FellowTrail.Flat, on the live-update worker: GroupShare.TakeLive).</summary>
        public void Add(string key, Dictionary<string, float> flat, bool sinceInstall, DateTime utc)
        {
            if (string.IsNullOrEmpty(key) || flat == null) return;
            if (trails.TryGetValue(key, out var t)) t.Add(flat, utc); else trails[key] = new FellowTrail(flat, sinceInstall, utc);
        }

        public void KeepOnly(ICollection<string> keys) { foreach (var k in trails.Keys.Where(k => !keys.Contains(k)).ToList()) trails.Remove(k); }
        public void Clear() => trails.Clear();
    }
}
