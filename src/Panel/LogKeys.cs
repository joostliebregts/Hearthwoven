using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The measured log's keys split once (0.8 performance pass). Every Battle page read every key of the log on every build:
    /// "2026-10-07T20:10Z|Swamp|dealt|Draugr|Axes|slash".Split('|') and a date parse per key, several times per build, every 2 s
    /// while the book is open (bench: 1.7 MB and 2.4 ms per build of a Battle page after a six-hour session). A key never changes
    /// once written and the log keeps its keys, so its parts and its minute are kept here and the next build only looks them up.
    /// Bounded (the cache is emptied past MaxKept keys, so fellows' logs and long sessions never grow it without end); main-thread
    /// use, locked anyway. Pure C#.
    /// </summary>
    public static class LogKeys
    {
        public sealed class Parts
        {
            /// <summary>The key's parts between the '|', as string.Split gives them.</summary>
            public readonly string[] P;
            /// <summary>The first part is a log minute ("yyyy-MM-ddTHH:mmZ"); Time is that minute in UTC.</summary>
            public readonly bool HasTime;
            public readonly DateTime Time;

            internal Parts(string key)
            {
                P = (key ?? "").Split('|');
                HasTime = DateTime.TryParseExact(P[0], "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out Time);
            }
        }

        public const int MaxKept = 60000;
        static readonly Dictionary<string, Parts> kept = new Dictionary<string, Parts>(StringComparer.Ordinal);
        static readonly object gate = new object();

        /// <summary>The key's parts (split and parsed the first time, looked up after).</summary>
        public static Parts Of(string key)
        {
            key = key ?? "";
            lock (gate)
            {
                if (kept.TryGetValue(key, out var p)) return p;
                if (kept.Count >= MaxKept) kept.Clear();
                p = new Parts(key);
                kept[key] = p;
                return p;
            }
        }

        static readonly Dictionary<string, (bool ok, DateTime utc)> minutes = new Dictionary<string, (bool, DateTime)>(StringComparer.Ordinal);

        /// <summary>A log minute ("yyyy-MM-ddTHH:mmZ", the first part of a key) as UTC, parsed once per string (PanelModel.TryBucket).</summary>
        public static bool Minute(string iso, out DateTime utc)
        {
            iso = iso ?? "";
            lock (gate)
            {
                if (!minutes.TryGetValue(iso, out var hit))
                {
                    var ok = DateTime.TryParseExact(iso, "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t);
                    if (minutes.Count >= MaxKept) minutes.Clear();
                    minutes[iso] = hit = (ok, t);
                }
                utc = hit.utc;
                return hit.ok;
            }
        }

        /// <summary>How many keys are kept now (the tests' bound check).</summary>
        public static int Count { get { lock (gate) return kept.Count; } }
    }
}
