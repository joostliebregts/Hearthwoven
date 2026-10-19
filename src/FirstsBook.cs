using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// 0.9, record 3 (work/hearthwoven-0.9/DESIGN.md section 1): for each item you made and each piece you placed for the first time, the local day
    /// it happened (and its minute, UTC, for the feed's moments), from the day 0.9 is installed. Before: every token you had already made or placed
    /// when recording began (the game's own item-craft and pieces-placed counters, and what Hearthwoven had counted as made), so a thing made before
    /// 0.9 is never called a first. One mark per token, kept for good, bounded (MaxMarks, MaxBefore). Kept in the local totals file (LocalTotals,
    /// top-level "firsts"), never shared. Read from the per-minute deeds (DeedLog buckets: "made" and "placed"), so nothing is counted in a hook.
    /// Pure C#, tested in test/RecordTests.cs.
    /// </summary>
    public sealed class FirstsBook
    {
        public const int MaxMarks = 4000, MaxBefore = 6000;
        /// <summary>Known: the character already knew it (its recipe or the material) when the known set was seeded, so it may have been made
        /// before 0.9: shown on your own book as "first recorded", never sent in a friend's feed block (DeedMoment.Known).</summary>
        public sealed class Mark { public DateTime Day, Utc; public bool Known; }

        /// <summary>Review fix (0.9): whether the character's known recipes and materials (Player.m_knownRecipes, m_knownMaterial) were added to
        /// Before. A long-time player knows the sausage recipe and has made sausages at a station that never booked them to their item-craft
        /// counter: without this, the next sausage was called a first.</summary>
        public bool KnownSeeded;

        /// <summary>Once (KnownSeeded): every token the character knows goes to Before (no new first for it), and a mark already given to one of
        /// them becomes Known. True when it ran (the caller saves and flags the kept deeds' moments: KeptDeeds.FlagKnownFirsts).</summary>
        public bool SeedKnown(IEnumerable<string> known)
        {
            if (KnownSeeded || !Begun || known == null) return false;
            KnownSeeded = true;
            foreach (var t in known)
            {
                if (string.IsNullOrEmpty(t)) continue;
                AddBefore(t);
                if (Marks.TryGetValue(t, out var m)) m.Known = true;
            }
            return true;
        }

        /// <summary>True when <paramref name="token"/>'s first is from before-0.9 knowledge (a Known mark): never shared.</summary>
        public bool FromKnowledge(string token) => token != null && Marks.TryGetValue(token, out var m) && m.Known;

        public DateTime FromUtc = DateTime.MinValue;
        /// <summary>token -> its first day (local date) and minute (UTC).</summary>
        public readonly Dictionary<string, Mark> Marks = new Dictionary<string, Mark>(StringComparer.Ordinal);
        /// <summary>The tokens already made or placed when recording began.</summary>
        public readonly HashSet<string> Before = new HashSet<string>(StringComparer.Ordinal);
        public bool BeforeClipped;

        public bool IsEmpty => FromUtc == DateTime.MinValue && Marks.Count == 0 && Before.Count == 0;
        public bool Begun => FromUtc > DateTime.MinValue;

        /// <summary>Recording begins (once): every token in <paramref name="already"/> with a count above 0 was made or placed before. True when it
        /// began just now (the caller saves).</summary>
        public bool Begin(DateTime nowUtc, params IEnumerable<KeyValuePair<string, float>>[] already)
        {
            if (Begun) return false;
            FromUtc = nowUtc;
            foreach (var d in already ?? new IEnumerable<KeyValuePair<string, float>>[0])
                if (d != null) foreach (var kv in d) if (kv.Value > 0 && !string.IsNullOrEmpty(kv.Key)) AddBefore(kv.Key);
            return true;
        }
        void AddBefore(string t) { if (Before.Count < MaxBefore) Before.Add(t); else BeforeClipped = true; }

        /// <summary>True when <paramref name="token"/> is a first you made or placed (it has a mark).</summary>
        public bool IsFirst(string token) => token != null && Marks.ContainsKey(token);

        /// <summary>
        /// Notes every first in the per-minute deeds <paramref name="buckets"/> ("2026-10-09T20:14Z|family|token" -> amount, DeedLog's keys): a
        /// token of the families made or placed that grew, that was not made or placed before recording began and has no mark yet, gets the
        /// earliest minute it grew in (its local day by <paramref name="toLocal"/>; null: the PC's time zone). Only after recording began, only
        /// minutes from then on. Returns how many were added.
        /// </summary>
        public int Note(IEnumerable<KeyValuePair<string, float>> buckets, Func<DateTime, DateTime> toLocal = null)
        {
            if (!Begun || buckets == null) return 0;
            var found = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            foreach (var kv in buckets)
            {
                if (kv.Value <= 0) continue;
                if (!KeptDeeds.ParseKey(kv.Key, out var utc, out var fam, out var token)) continue;
                if (fam != DeedLog.Made && fam != DeedLog.Placed) continue;
                if (token == DeedLog.OtherToken || Marks.ContainsKey(token) || Before.Contains(token) || utc.AddMinutes(1) <= FromUtc) continue;
                if (!found.TryGetValue(token, out var was) || utc < was) found[token] = utc;
            }
            int added = 0;
            foreach (var kv in found.OrderBy(x => x.Value))
            {
                if (Marks.Count >= MaxMarks) break;
                var local = toLocal != null ? toLocal(kv.Value) : kv.Value.ToLocalTime();
                Marks[kv.Key] = new Mark { Day = local.Date, Utc = kv.Value }; added++;
            }
            return added;
        }

        /// <summary>token -> first local day (what the panel reads, PanelInput.Firsts).</summary>
        public Dictionary<string, DateTime> Days() => Marks.ToDictionary(kv => kv.Key, kv => kv.Value.Day, StringComparer.Ordinal);

        // ---------- the file (LocalTotals, top-level "firsts") ----------

        /// <summary>{"from":"..","marks":{token:{"d":"2026-10-18","u":"2026-10-18T20:15:00Z"}},"before":[token,..],"beforeClipped":1}</summary>
        public void WriteTo(Json j, string key = "firsts")
        {
            j.Key(key).Open();
            if (Begun) j.Str("from", FromUtc.ToString("o", CultureInfo.InvariantCulture));
            j.Key("marks").Open();
            foreach (var kv in Marks.Take(MaxMarks)) { j.Key(kv.Key).Open().Str("d", UpgradeLog.Day(kv.Value.Day)).Str("u", UpgradeLog.Iso(kv.Value.Utc)); if (kv.Value.Known) j.Num("k", 1); j.Close(); }
            j.Close();
            j.Key("before").OpenArr();
            foreach (var t in Before.Take(MaxBefore)) j.Val(t);
            j.CloseArr();
            if (BeforeClipped) j.Num("beforeClipped", 1);
            if (KnownSeeded) j.Num("knownSeeded", 1);
            j.Close();
        }

        /// <summary>Reads what WriteTo wrote, bounded; null or not an object: an empty book (recording begins at the next load).</summary>
        public static FirstsBook ReadFrom(Dictionary<string, object> o)
        {
            var b = new FirstsBook();
            if (o == null) return b;
            b.FromUtc = UpgradeLog.Time(MiniJson.Str(o, "from"));
            var marks = MiniJson.Obj(o, "marks");
            if (marks != null)
                foreach (var kv in marks)
                {
                    if (b.Marks.Count >= MaxMarks) break;
                    if (!(kv.Value is Dictionary<string, object> m) || kv.Key.Length == 0) continue;
                    var day = UpgradeLog.Time(MiniJson.Str(m, "d")); var utc = UpgradeLog.Time(MiniJson.Str(m, "u"));
                    if (day > DateTime.MinValue) b.Marks[kv.Key] = new Mark { Day = day.Date, Utc = utc > DateTime.MinValue ? utc : day, Known = MiniJson.Num(m, "k") > 0 };
                }
            if (o.TryGetValue("before", out var bf) && bf is List<object> before)
                foreach (var t in before.OfType<string>()) { if (t.Length > 0) b.AddBefore(t); }
            if (MiniJson.Num(o, "beforeClipped") > 0) b.BeforeClipped = true;
            b.KnownSeeded = MiniJson.Num(o, "knownSeeded") > 0;
            return b;
        }
    }
}
