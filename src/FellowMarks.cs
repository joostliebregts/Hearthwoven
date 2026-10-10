using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hearthwoven.Panel;

namespace Hearthwoven
{
    /// <summary>
    /// "Since last time" for fellow players (0.7, LAST-EVENING-SCOPE.md section 2): per fellow, the running totals of their shared copy as
    /// this PC last saw it, so the next session can say what grew in between (their copy now minus this mark). A fellow's copy carries
    /// running totals (measuredSinceInstall, the game's counters), so the difference is right however many sessions they played meanwhile.
    ///
    /// Two sets: Before = the marks as loaded when this session began (what the page compares with, frozen for the session); Latest = Before
    /// with every copy received this session laid over it (Update), saved, and so the next session's Before. Only the deed families of
    /// DeedLog are kept, flat as "family|token" (the same names RecentModel reads): the event families only from a copy that shares its
    /// since-install totals (an older copy's measured tallies are one session, which does not difference), the game's counters always.
    ///
    /// Its own file next to the character's totals: local/&lt;playerId&gt;-&lt;name&gt;.fellows.json (PathFor), written atomically with a
    /// .bak (AtomicFile), never part of LocalTotals. Bounded: MaxFellows fellows (the longest unseen go), MaxKeysPerFamily per family per
    /// fellow (the largest). About 5-15 KB per fellow (DeedLogTests). Pure C#, unit-tested.
    /// JSON: {"version":1,"fellows":{"&lt;fellow key&gt;":{"name":"..","session":"..","seen":"ISO","since":true,"story":true,"v":{"family|token":n}}}}.
    /// Beside DeedLog's families a mark keeps StoryFamilies (the gifts and deaths Since you were away tells).
    /// 0.8 (RESILIENCE item 4): keys a newer Hearthwoven wrote (top level and inside a fellow's mark) are kept and written back as read;
    /// a file of a newer version is read for nothing and never saved over (ReadOnly). A broken file falls back to its .bak (Load says so).
    /// </summary>
    public class FellowMarks
    {
        public const int Version = 1, MaxFellows = 40, MaxKeysPerFamily = 300;

        public class Mark
        {
            public string Name = "", Session = "";
            /// <summary>When this PC received this copy (its content first arrived); the "from" of since last time.</summary>
            public DateTime SeenUtc;
            /// <summary>The copy carried since-install measured totals (0.5+): the event families can be differenced.</summary>
            public bool SinceInstall;
            /// <summary>The mark keeps the StoryFamilies too (a mark from before they were kept has none: their growth is not known, never "all of it").</summary>
            public bool Story;
            /// <summary>"family|token" -> running total (DeedLog's families).</summary>
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
            /// <summary>Keys of this mark a newer version wrote, kept as read (written back while the mark is not replaced by a new copy).</summary>
            public readonly Dictionary<string, object> Extra = new Dictionary<string, object>();
        }

        static readonly HashSet<string> KnownTop = new HashSet<string> { "version", "fellows" };
        static readonly HashSet<string> KnownMark = new HashSet<string> { "name", "session", "seen", "since", "story", "v" };
        /// <summary>Top-level keys a newer version wrote, kept as read.</summary>
        public readonly Dictionary<string, object> Extra = new Dictionary<string, object>();
        /// <summary>The file is from a newer Hearthwoven: nothing in it was read, and Save leaves it as it is.</summary>
        public bool ReadOnly;

        public readonly Dictionary<string, Mark> Before = new Dictionary<string, Mark>();
        public readonly Dictionary<string, Mark> Latest = new Dictionary<string, Mark>();
        readonly Dictionary<string, string> taken = new Dictionary<string, string>();   // fellow key -> the copy's JSON its Latest mark was made from

        public Mark BeforeOf(string key) => key != null && Before.TryGetValue(key, out var m) ? m : null;

        public static string PathFor(string totalsPath) =>
            string.IsNullOrEmpty(totalsPath) ? null : (totalsPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? totalsPath.Substring(0, totalsPath.Length - 5) : totalsPath) + ".fellows.json";

        /// <summary>The mark of one fellow copy (a shared snapshot, PanelInput.FromSnapshot); null when it cannot be read.</summary>
        public static Mark Of(PanelInput copy, DateTime seenUtc) => Of(copy, copy?.PlayerName, seenUtc);

        /// <summary>The mark of a copy the panel shares (FellowCopies), named by <paramref name="name"/>: the name in the copy's own text, since the
        /// panel writes its label over the shared PanelInput's PlayerName.</summary>
        public static Mark Of(PanelInput copy, string name, DateTime seenUtc)
        {
            if (copy == null) return null;
            var m = new Mark { Name = name ?? "", Session = copy.SessionId ?? "", SeenUtc = seenUtc, SinceInstall = copy.SharedSinceInstall, Story = true };
            foreach (var kv in Families(copy).Concat(Story(copy))) foreach (var t in Clip(kv.Value)) m.Values[kv.Key + "|" + t.Key] = t.Value;
            return m;
        }

        /// <summary>The families Since you were away tells beside DeedLog's (0.7, AwayModel.cs): whose food a fellow enjoyed, whose gear they put to
        /// good use, and their deaths (in "battle"). Since-install copies only (a one-session tally does not difference). Not part of Families, so
        /// Recent never reads them.</summary>
        public static readonly string[] StoryFamilies = { "ateFoodMadeBy", "ateFromFeastOf", "ateFromFeastAt", "equippedGearMadeBy", "battle" };   // ateFromFeastAt (0.8): which feast, so its maker gets the credit

        /// <summary>A copy's running totals of the StoryFamilies (family -> token -> value); empty for a copy without since-install totals.</summary>
        public static Dictionary<string, Dictionary<string, float>> Story(PanelInput copy)
        {
            var d = new Dictionary<string, Dictionary<string, float>>();
            if (copy?.Events == null || !copy.SharedSinceInstall) return d;
            foreach (var fam in copy.Events.Named()) if (Array.IndexOf(StoryFamilies, fam.Key) >= 0 && fam.Value.Count > 0) d[fam.Key] = new Dictionary<string, float>(fam.Value);
            return d;
        }

        /// <summary>A copy's running totals per DeedLog family (token -> value): the event families only when they are since install.</summary>
        public static Dictionary<string, Dictionary<string, float>> Families(PanelInput copy)
        {
            var d = new Dictionary<string, Dictionary<string, float>>();
            if (copy.SharedSinceInstall && copy.Events != null)
                foreach (var fam in copy.Events.Named()) if (Array.IndexOf(DeedLog.EventFamilies, fam.Key) >= 0 && fam.Value.Count > 0) d[fam.Key] = new Dictionary<string, float>(fam.Value);
            var game = DeedLog.GameKeys(copy.Character, copy.PiecesPlaced, copy.Harvested, copy.EnemyKills);
            foreach (var kv in game)
            {
                var bar = kv.Key.IndexOf('|'); var fam = kv.Key.Substring(0, bar);
                if (!d.TryGetValue(fam, out var f)) d[fam] = f = new Dictionary<string, float>();
                f[kv.Key.Substring(bar + 1)] = kv.Value;
            }
            return d;
        }

        static IEnumerable<KeyValuePair<string, float>> Clip(Dictionary<string, float> d) =>
            d.Count <= MaxKeysPerFamily ? (IEnumerable<KeyValuePair<string, float>>)d : d.OrderByDescending(kv => kv.Value).Take(MaxKeysPerFamily);

        /// <summary>Lays every copy received this session over Latest (only a copy whose text changed since its mark was made is looked at again, and
        /// through FellowCopies, so a copy the live worker or the panel already read is not parsed again on the main thread: REVIEW-08 #4).
        /// <paramref name="received"/>: when each copy arrived (GroupShare.ReceivedUtc); missing: <paramref name="nowUtc"/>. Returns whether a mark changed.</summary>
        public bool Update(IDictionary<string, string> copies, IDictionary<string, DateTime> received, DateTime nowUtc)
        {
            var changed = false;
            if (copies == null) return false;
            foreach (var kv in copies)
            {
                if (taken.TryGetValue(kv.Key, out var was) && string.Equals(was, kv.Value, StringComparison.Ordinal)) continue;
                taken[kv.Key] = kv.Value;
                var when = received != null && received.TryGetValue(kv.Key, out var r) ? r : nowUtc;
                var m = Of(FellowCopies.Of(kv.Key, kv.Value), Transport.Field(kv.Value, "name"), when);
                if (m != null) { Latest[kv.Key] = m; changed = true; }
            }
            foreach (var k in Latest.OrderByDescending(kv => kv.Value.SeenUtc).Skip(MaxFellows).Select(kv => kv.Key).ToList()) Latest.Remove(k);
            return changed;
        }

        public string ToJson()
        {
            var j = new Json().Open().Num("version", Version);
            j.Key("fellows").Open();
            foreach (var kv in Latest)
            {
                var m = kv.Value;
                j.Key(kv.Key).Open().Str("name", m.Name).Str("session", m.Session).Str("seen", m.SeenUtc.ToString("o", CultureInfo.InvariantCulture)).Raw("since", m.SinceInstall ? "true" : "false").Raw("story", m.Story ? "true" : "false").Dict("v", m.Values);
                foreach (var x in m.Extra) if (!KnownMark.Contains(x.Key)) j.Raw(x.Key, MiniJson.Write(x.Value));   // a newer version's data, kept as read
                j.Close();
            }
            j.Close();
            foreach (var x in Extra) if (!KnownTop.Contains(x.Key)) j.Raw(x.Key, MiniJson.Write(x.Value));
            return j.Close().ToString();
        }

        /// <summary>Reads what ToJson wrote into Before and Latest; a file it cannot read (or a newer version): empty marks.</summary>
        public static FellowMarks FromJson(string json)
        {
            var f = new FellowMarks();
            if (!(MiniJson.Parse(json ?? "") is Dictionary<string, object> root) || MiniJson.Num(root, "version") < 1) return f;
            if (MiniJson.Num(root, "version") > Version) { f.ReadOnly = true; return f; }   // a newer format: left as it is, never saved over
            foreach (var x in root) if (!KnownTop.Contains(x.Key)) f.Extra[x.Key] = x.Value;
            var all = MiniJson.Obj(root, "fellows");
            if (all == null) return f;
            foreach (var kv in all)
            {
                if (!(kv.Value is Dictionary<string, object> o)) continue;
                DateTime.TryParse(MiniJson.Str(o, "seen"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var seen);
                var m = new Mark { Name = MiniJson.Str(o, "name"), Session = MiniJson.Str(o, "session"), SeenUtc = seen.ToUniversalTime(), SinceInstall = o.TryGetValue("since", out var s) && s is bool b && b, Story = o.TryGetValue("story", out var st) && st is bool sb && sb };
                MiniJson.Into(MiniJson.Obj(o, "v"), m.Values);
                foreach (var x in o) if (!KnownMark.Contains(x.Key)) m.Extra[x.Key] = x.Value;
                f.Before[kv.Key] = m; f.Latest[kv.Key] = m;
            }
            return f;
        }

        public static FellowMarks Load(string path) => Load(path, out _);

        /// <summary>
        /// The marks at <paramref name="path"/>, else its .bak (the save before) when the main file is missing or broken: the broken one is
        /// set aside (AtomicFile.SetAside, never deleted) and <paramref name="problem"/> names both. A file that cannot be opened, or one
        /// of a newer version: ReadOnly marks (nothing saved over it this session). Never throws.
        /// </summary>
        public static FellowMarks Load(string path, out string problem)
        {
            problem = null;
            if (path == null) return new FellowMarks();
            try
            {
                var text = AtomicFile.ReadWithBackup(path, t => MiniJson.Parse(t) is Dictionary<string, object>, out var fromBackup);
                var f = FromJson(text);
                if (f.ReadOnly) problem = "fellow marks " + path + " are from a newer Hearthwoven; left as they are (not saved over)";
                else if (fromBackup)
                {
                    var aside = AtomicFile.SetAside(path);
                    problem = "fellow marks " + path + (aside != null ? " were unreadable (set aside as " + aside + ")" : " were missing") + "; restored from the backup " + path + AtomicFile.BackupSuffix;
                }
                else if (text != null && !(MiniJson.Parse(text) is Dictionary<string, object>))
                {
                    var aside = AtomicFile.SetAside(path);
                    problem = "fellow marks " + path + " are unreadable" + (aside != null ? " (set aside as " + aside + ")" : "") + " and have no usable backup; since last time starts again";
                }
                return f;
            }
            catch (Exception e) { problem = "fellow marks " + path + " could not be opened, not saving over them: " + e.Message; return new FellowMarks { ReadOnly = true }; }
        }

        public void Save(string path) { if (path != null && !ReadOnly) AtomicFile.Write(path, ToJson()); }
    }
}
