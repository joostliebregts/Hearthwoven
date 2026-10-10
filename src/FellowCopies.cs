using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven.Panel;

namespace Hearthwoven
{
    /// <summary>
    /// Each fellow's copy read once (0.8 performance pass, REVIEW-07 finding 8): the trail (FellowTrails), the panel (PanelUi.Fellow) and the
    /// worker that applies live updates (GroupShare.TakeLive) share one PanelInput per copy text. Before, every live update was read twice on
    /// the main thread (the trail, then the panel on its next refresh: about 1.2 MB of garbage for a 26 KB copy). By fellow key, the latest copy
    /// only; bounded by the group. The panel writes its labels into the shared PanelInput (as it did into its own cache); the trail reads
    /// only the counts, which nothing changes after the copy is read. Locked: the worker puts, the main thread reads.
    /// </summary>
    public static class FellowCopies
    {
        static readonly Dictionary<string, KeyValuePair<string, PanelInput>> read = new Dictionary<string, KeyValuePair<string, PanelInput>>();
        static readonly object gate = new object();

        /// <summary>Times a copy was read (PanelInput.FromSnapshot) here; the tests' and the bench's count.</summary>
        public static int Reads { get; private set; }

        /// <summary>The copy <paramref name="json"/> of <paramref name="key"/> read (once per copy text); null when it cannot be read.</summary>
        public static PanelInput Of(string key, string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            if (key != null) lock (gate) if (read.TryGetValue(key, out var hit) && string.Equals(hit.Key, json, StringComparison.Ordinal)) return hit.Value;   // the server resends unchanged copies
            var copy = PanelInput.FromSnapshot(json);   // outside the lock: a few milliseconds for a big copy
            lock (gate) { Reads++; if (key != null) read[key] = new KeyValuePair<string, PanelInput>(json, copy); }
            return copy;
        }

        /// <summary>A copy already read elsewhere (the live-update worker): kept for the panel and the trail.</summary>
        public static void Put(string key, string json, PanelInput copy)
        {
            if (key == null || json == null || copy == null) return;
            lock (gate) read[key] = new KeyValuePair<string, PanelInput>(json, copy);
        }

        /// <summary>Only the fellows still there are kept.</summary>
        public static void KeepOnly(ICollection<string> keys) { lock (gate) foreach (var k in read.Keys.Where(k => keys == null || !keys.Contains(k)).ToList()) read.Remove(k); }
        public static void Clear() { lock (gate) read.Clear(); }
        public static int Count { get { lock (gate) return read.Count; } }
    }
}
