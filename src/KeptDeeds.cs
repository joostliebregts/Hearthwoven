using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>One moment in a deed stretch (0.9): an item of gear made ("made"), an upgrade ("upgraded", with its levels and station), a feast
    /// set out ("feast"), or a first ("first": an item made or a piece placed for the first time, FirstsBook). Utc: its minute (an upgrade: its
    /// second). Count: how many in that minute (1 for an upgrade or a first).</summary>
    public sealed class DeedMoment
    {
        public const string Made = "made", Upgraded = "upgraded", Feast = "feast", First = "first";
        public string Kind = "", Token = "";
        public DateTime Utc;
        public int Count = 1;
        /// <summary>An upgrade only: its level before and after, the station (prefab) and its level.</summary>
        public int From, To, StationLevel;
        public string Station;
        /// <summary>A first only: from before-0.9 knowledge (FirstsBook.Mark.Known: the recipe or material was known): shown as "first recorded" on your own
        /// book, never sent in a feed block (DeedBlockPart.Clean drops it). JSON "kb":1.</summary>
        public bool Known;
    }

    /// <summary>
    /// One stretch of one deed family (0.9): the minutes that family grew, closed by StretchGap quiet minutes. Family: the Deeds page it belongs to
    /// (DeedStretches.FamilyOf: "crafting", "cooking", "building", ...). Counts: what it did, per "deedfamily|token" as DeedLog keeps them
    /// ("made|$item_axe_bronze": 2, "chopHits|Beech1": 40): the largest MaxCounts by name, the rest of each deed family in its "deedfamily|~other",
    /// so every deed family's sum stays exact. Moments: newest first, at most MaxMoments (MomentsLeft counts the rest).
    /// </summary>
    public sealed class DeedStretch
    {
        public string Family = "";
        public DateTime FromUtc, ToUtc;
        public readonly Dictionary<string, float> Counts = new Dictionary<string, float>(StringComparer.Ordinal);
        public readonly List<DeedMoment> Moments = new List<DeedMoment>();
        public int MomentsLeft;
        /// <summary>The token of a Counts key ("made|$item_x" -> "$item_x").</summary>
        public static string TokenOf(string key) { var i = key?.IndexOf('|') ?? -1; return i < 0 ? key : key.Substring(i + 1); }
        /// <summary>The deed family of a Counts key ("made|$item_x" -> "made", DeedLog's family names).</summary>
        public static string KindOf(string key) { var i = key?.IndexOf('|') ?? -1; return i < 0 ? "" : key.Substring(0, i); }
    }

    /// <summary>One session's deed stretches in one world (0.9, record 2): the session id, when it began and when it was last saved, its stretches
    /// newest first. Live: the running session, built for the panel (never in the file).</summary>
    public sealed class DeedSession
    {
        public string World = "", Id = "";
        public DateTime FromUtc, ToUtc;
        public readonly List<DeedStretch> Stretches = new List<DeedStretch>();
        public bool Live;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// The session in a JSON object (the file's form, and the form a block for friends can reuse in the feeds track): times in whole seconds
        /// from the session's start ("t0"), short keys. {"world","id","from","to","s":[{"f":family,"a":sec,"b":sec,"c":{key:n},"m":[{"k":kind,
        /// "t":token,"a":sec,"n":count,"x":from,"y":to,"st":station,"sl":level}],"ml":left}]}. Bounded as the reader reads it.
        /// </summary>
        public void WriteTo(Json j)
        {
            j.Open().Str("world", World).Str("id", Id).Str("from", FromUtc.ToString("o", Inv)).Str("to", ToUtc.ToString("o", Inv));
            j.Key("s").OpenArr();
            foreach (var s in Stretches.Take(DeedStretches.MaxStretches))
            {
                j.Open().Str("f", s.Family).Num("a", Sec(s.FromUtc)).Num("b", Sec(s.ToUtc));
                j.Dict("c", s.Counts.Take(DeedStretches.MaxCounts + 16).Select(kv => new KeyValuePair<string, float>(kv.Key, (float)Math.Round(kv.Value, 2))));
                if (s.Moments.Count > 0)
                {
                    j.Key("m").OpenArr();
                    foreach (var m in s.Moments.Take(DeedStretches.MaxMoments))
                    {
                        j.Open().Str("k", m.Kind).Str("t", m.Token).Num("a", Sec(m.Utc));
                        if (m.Count != 1) j.Num("n", m.Count);
                        if (m.Known) j.Num("kb", 1);
                        if (m.Kind == DeedMoment.Upgraded) { j.Num("x", m.From).Num("y", m.To); if (!string.IsNullOrEmpty(m.Station)) j.Str("st", m.Station); if (m.StationLevel > 0) j.Num("sl", m.StationLevel); }
                        j.Close();
                    }
                    j.CloseArr();
                }
                if (s.MomentsLeft > 0) j.Num("ml", s.MomentsLeft);
                j.Close();
            }
            j.CloseArr().Close();
        }
        long Sec(DateTime t) => (long)Math.Round((t - FromUtc).TotalSeconds);

        /// <summary>Reads what WriteTo wrote, bounded (MaxStretches stretches, MaxCounts + 16 counts and MaxMoments moments each, families and kinds
        /// as written); null when it is not a session (no id or times).</summary>
        public static DeedSession ReadFrom(Dictionary<string, object> o)
        {
            if (o == null) return null;
            var s = new DeedSession { World = MiniJson.Str(o, "world"), Id = MiniJson.Str(o, "id"), FromUtc = UpgradeLog.Time(MiniJson.Str(o, "from")), ToUtc = UpgradeLog.Time(MiniJson.Str(o, "to")) };
            if (s.Id.Length == 0 || s.FromUtc == DateTime.MinValue || s.ToUtc < s.FromUtc) return null;
            DateTime At(Dictionary<string, object> d, string k) { var v = MiniJson.Num(d, k); return v >= 0 && v < 3e7 ? s.FromUtc.AddSeconds(v) : s.FromUtc; }
            if (o.TryGetValue("s", out var so) && so is List<object> list)
                foreach (var x in list.OfType<Dictionary<string, object>>().Take(DeedStretches.MaxStretches))
                {
                    var st = new DeedStretch { Family = MiniJson.Str(x, "f"), FromUtc = At(x, "a"), ToUtc = At(x, "b") };
                    if (st.Family.Length == 0 || st.ToUtc < st.FromUtc) continue;
                    var c = MiniJson.Obj(x, "c");
                    if (c != null) foreach (var kv in c.Take(DeedStretches.MaxCounts + 16)) if (kv.Value is double v && v > 0 && Json.IsFinite(v) && kv.Key.IndexOf('|') > 0) st.Counts[kv.Key] = (float)v;
                    if (x.TryGetValue("m", out var mo) && mo is List<object> ms)
                        foreach (var e in ms.OfType<Dictionary<string, object>>().Take(DeedStretches.MaxMoments))
                        {
                            var m = new DeedMoment { Kind = MiniJson.Str(e, "k"), Token = MiniJson.Str(e, "t"), Utc = At(e, "a"), Count = e.ContainsKey("n") ? (int)MiniJson.Num(e, "n") : 1,
                                                     From = (int)MiniJson.Num(e, "x"), To = (int)MiniJson.Num(e, "y"), Station = MiniJson.Str(e, "st", null), StationLevel = (int)MiniJson.Num(e, "sl"), Known = MiniJson.Num(e, "kb") > 0 };
                            if (m.Kind.Length > 0 && m.Token.Length > 0 && m.Count > 0) st.Moments.Add(m);
                        }
                    st.MomentsLeft = (int)Math.Max(0, MiniJson.Num(x, "ml"));
                    s.Stretches.Add(st);
                }
            s.Stretches.Sort((a, b) => b.ToUtc.CompareTo(a.ToUtc));
            return s;
        }
    }

    /// <summary>
    /// 0.9, record 2: a session's deed stretches built from its per-minute deeds (DeedLog), its upgrades (UpgradeLog) and its firsts (FirstsBook).
    /// Pure: the caller hands over the buckets and the game data that sorts a token (item kind, piece kind, gather kind). Nothing is counted
    /// here: a stretch's counts are a time split of the session's buckets, so they add up to what the buckets hold in its minutes.
    /// </summary>
    public static class DeedStretches
    {
        /// <summary>Quiet minutes that close a stretch (Joost, 18 Oct 2026: "Yes sure").</summary>
        public const int StretchGap = 10;
        /// <summary>Bounds per stretch and session (the counts copy DeedLog's 40 per bucket).</summary>
        public const int MaxCounts = DeedLog.MaxKeysPerBucket, MaxMoments = 60, MaxStretches = 200;

        public const string Crafting = "crafting", Cooking = "cooking", Building = "building", Groundwork = "groundwork", Farming = "farming",
                            Fishing = "fishing", Woodcutting = "woodcutting", Mining = "mining";
        /// <summary>0.9 all-feed (Joost: "a feed for all of their deeds"): the deed families with no Deeds page feed of their own, shown in the mixed feed
        /// only: creatures tamed, skills practiced, what you brought in that is neither wood nor stone and ore, and sailing (km sailed and cargo carried).</summary>
        public const string Taming = "taming", Practice = "practice", BroughtIn = "broughtin", Sailing = "sailing";

        /// <summary>
        /// The deed family a deed belongs to (its stretch), or null (in no stretch: kills and the kill and fish counters, which the fights and Fishing's
        /// fish already say). made: a dish (item kind "food") Cooking, anything else Crafting; placed: built Building, ground Groundwork, a feast Cooking
        /// (set out), planted pieces left to Planted (Farming); planted: Farming; picked: a fish token ("$...") Fishing, else Farming; pickedUp: wood
        /// Woodcutting, stone and ore Mining, anything else BroughtIn (0.9 all-feed); treesFelled and chopHits Woodcutting; pickaxeHits Mining;
        /// recovered Building; 0.9 all-feed: skillPractice Practice, the counters CreatureTamed Taming, DistanceSail and the cargo total Sailing.
        /// </summary>
        public static string FamilyOf(string deedFamily, string token, Func<string, string> itemKind, Func<string, string> pieceKind, Func<string, string> gatherKind)
        {
            switch (deedFamily)
            {
                case DeedLog.Made: return Ask(itemKind, token) == "food" ? Cooking : Crafting;
                case DeedLog.Placed:
                    switch (Ask(pieceKind, token)) { case "built": return Building; case "ground": return Groundwork; case "feast": return Cooking; default: return null; }
                case DeedLog.Planted: return Farming;
                case DeedLog.Picked: return token != null && token.StartsWith("$", StringComparison.Ordinal) ? Fishing : Farming;
                case DeedLog.PickedUp: { var g = Ask(gatherKind, token); return g == "wood" ? Woodcutting : g == "mining" ? Mining : BroughtIn; }
                case DeedLog.Felled: case DeedLog.ChopHits: return Woodcutting;
                case DeedLog.PickaxeHits: return Mining;
                case DeedLog.Recovered: return Building;
                case DeedLog.Skills: return Practice;
                case DeedLog.Counters: return token == "CreatureTamed" ? Taming : token == "DistanceSail" || token == DeedLog.CargoTotal ? Sailing : null;
                default: return null;
            }
        }
        static string Ask(Func<string, string> f, string t) { try { return f?.Invoke(t); } catch { return null; } }

        struct Entry { public string Family, Key, Deed, Token; public DateTime From, To; public float Value; }

        /// <summary>
        /// The stretches of one session: every bucket of <paramref name="buckets"/> (DeedLog's keys; a folded one, older than
        /// EventLog.FoldAfterMinutes at <paramref name="nowUtc"/> and on a whole ten minutes, covers ten minutes) goes to its family (FamilyOf);
        /// <paramref name="upgrades"/> in the session count as Crafting minutes; a family's minutes form one stretch until StretchGap quiet minutes
        /// pass. Moments: gear made (made, item kind "gear"), feasts set out (placed, piece kind "feast"), upgrades and firsts (a token of the
        /// stretch whose first minute, <paramref name="firsts"/>, lies in it). Stretches newest first, at most MaxStretches (the newest stay).
        /// </summary>
        public static DeedSession Build(string world, string id, DateTime fromUtc, DateTime nowUtc, IEnumerable<KeyValuePair<string, float>> buckets,
                                        IEnumerable<UpgradeMoment> upgrades, FirstsBook firsts,
                                        Func<string, string> itemKind, Func<string, string> pieceKind, Func<string, string> gatherKind)
        {
            var s = new DeedSession { World = world ?? "", Id = id ?? "", FromUtc = fromUtc, ToUtc = nowUtc };
            var foldCut = EventLog.Bucket(nowUtc.AddMinutes(-EventLog.FoldAfterMinutes));
            var entries = new List<Entry>();
            var famCache = new Dictionary<string, string>(StringComparer.Ordinal);
            if (buckets != null)
                foreach (var kv in buckets)
                {
                    if (kv.Value <= 0 || !Json.IsFinite(kv.Value)) continue;
                    if (!KeptDeeds.ParseKey(kv.Key, out var utc, out var deed, out var token)) continue;
                    var fk = deed + "|" + token;
                    if (!famCache.TryGetValue(fk, out var fam)) famCache[fk] = fam = FamilyOf(deed, token, itemKind, pieceKind, gatherKind);
                    if (fam == null) continue;
                    var folded = utc.Minute % EventLog.FoldedMinutes == 0 && string.CompareOrdinal(kv.Key, 0, foldCut, 0, 17) < 0;
                    entries.Add(new Entry { Family = fam, Key = fk, Deed = deed, Token = token, From = utc, To = utc.AddMinutes(folded ? EventLog.FoldedMinutes : EventLog.BucketMinutes), Value = kv.Value });
                }
            var ups = (upgrades ?? Enumerable.Empty<UpgradeMoment>()).Where(u => u != null && u.Utc >= fromUtc && u.Utc <= nowUtc).ToList();
            foreach (var u in ups)
            {
                var m = new DateTime(u.Utc.Ticks - u.Utc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
                entries.Add(new Entry { Family = Crafting, Key = null, From = m, To = m.AddMinutes(1) });
            }
            foreach (var fam in entries.GroupBy(e => e.Family))
            {
                DeedStretch cur = null; var curTokens = new HashSet<string>(StringComparer.Ordinal);
                var sums = new Dictionary<string, float>(StringComparer.Ordinal);
                void Close()
                {
                    if (cur == null) return;
                    Finish(cur, sums, curTokens, ups, firsts, itemKind, pieceKind);
                    s.Stretches.Add(cur); cur = null; sums.Clear(); curTokens.Clear();
                }
                foreach (var e in fam.OrderBy(e => e.From))
                {
                    if (cur != null && e.From >= cur.ToUtc.AddMinutes(StretchGap)) Close();
                    if (cur == null) cur = new DeedStretch { Family = fam.Key, FromUtc = e.From, ToUtc = e.To };
                    if (e.To > cur.ToUtc) cur.ToUtc = e.To;
                    if (e.Key == null) continue;
                    sums.TryGetValue(e.Key, out var o); sums[e.Key] = o + e.Value;
                    curTokens.Add(e.Token);
                    if (e.Deed == DeedLog.Made && Ask(itemKind, e.Token) == "gear" && e.Token != DeedLog.OtherToken)
                        cur.Moments.Add(new DeedMoment { Kind = DeedMoment.Made, Token = e.Token, Utc = e.From, Count = Math.Max(1, (int)Math.Round(e.Value)) });
                    else if (e.Deed == DeedLog.Placed && Ask(pieceKind, e.Token) == "feast")
                        cur.Moments.Add(new DeedMoment { Kind = DeedMoment.Feast, Token = e.Token, Utc = e.From, Count = Math.Max(1, (int)Math.Round(e.Value)) });
                }
                Close();
            }
            s.Stretches.Sort((a, b) => b.ToUtc != a.ToUtc ? b.ToUtc.CompareTo(a.ToUtc) : string.CompareOrdinal(a.Family, b.Family));
            if (s.Stretches.Count > MaxStretches) s.Stretches.RemoveRange(MaxStretches, s.Stretches.Count - MaxStretches);
            return s;
        }

        static void Finish(DeedStretch st, Dictionary<string, float> sums, HashSet<string> tokens, List<UpgradeMoment> ups, FirstsBook firsts,
                           Func<string, string> itemKind, Func<string, string> pieceKind)
        {
            // counts: the largest MaxCounts by name; the rest of each deed family in its "~other" (every family's sum stays exact)
            var named = sums.Where(kv => DeedStretch.TokenOf(kv.Key) != DeedLog.OtherToken).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            foreach (var kv in named.Take(MaxCounts)) st.Counts[kv.Key] = kv.Value;
            foreach (var kv in sums.Where(kv => !st.Counts.ContainsKey(kv.Key)).ToList())   // listed first: the "~other" keys below must not hide a bucket's own "~other"
            {
                var other = DeedStretch.KindOf(kv.Key) + "|" + DeedLog.OtherToken;
                st.Counts.TryGetValue(other, out var o); st.Counts[other] = o + kv.Value;
            }
            if (st.Family == Crafting)
                foreach (var u in ups.Where(u => u.Utc >= st.FromUtc && u.Utc < st.ToUtc))
                    st.Moments.Add(new DeedMoment { Kind = DeedMoment.Upgraded, Token = u.Token, Utc = u.Utc, From = u.From, To = u.To, Station = string.IsNullOrEmpty(u.Station) ? null : u.Station, StationLevel = u.StationLevel });
            if (firsts != null)
                foreach (var t in tokens)
                    if (firsts.Marks.TryGetValue(t, out var mark) && mark.Utc >= st.FromUtc && mark.Utc < st.ToUtc)
                        st.Moments.Add(new DeedMoment { Kind = DeedMoment.First, Token = t, Utc = mark.Utc, Known = mark.Known });
            st.Moments.Sort((a, b) => b.Utc != a.Utc ? b.Utc.CompareTo(a.Utc) : string.CompareOrdinal(a.Kind + a.Token, b.Kind + b.Token));
            if (st.Moments.Count > MaxMoments) { st.MomentsLeft = st.Moments.Count - MaxMoments; st.Moments.RemoveRange(MaxMoments, st.Moments.Count - MaxMoments); }
        }
    }

    /// <summary>
    /// 0.9, record 2 kept (work/hearthwoven-0.9/DESIGN.md section 1): your deed stretches of earlier sessions, per world, on this PC, as KeptFights
    /// keeps your fights: local/&lt;playerId&gt;-&lt;name&gt;.deeds.json beside the character's totals file, written whole (AtomicFile, one .bak) at
    /// every local save once this session has a stretch. Bounded per world: stretches that ended within MaxAge (30 days), at most MaxSessions
    /// sessions; at most MaxWorlds worlds, the ones played last; each session as DeedSession reads it (MaxStretches, counts and moments capped).
    /// Never shared in this track (the feeds track sends one closed session as a block: DeedSession.WriteTo / ReadFrom). Pure C# with files,
    /// tested in test/RecordTests.cs.
    /// </summary>
    public sealed class KeptDeeds
    {
        public const int Version = 1, MaxWorlds = 4, MaxSessions = 120;
        public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string PlayerId = "", Name = "";
        /// <summary>Every world's sessions, newest first (by their last save).</summary>
        public readonly List<DeedSession> Sessions = new List<DeedSession>();

        /// <summary>A DeedLog key "2026-10-09T20:14Z|family|token" in its parts; false when it is not one.</summary>
        public static bool ParseKey(string key, out DateTime utc, out string family, out string token)
        {
            utc = DateTime.MinValue; family = token = null;
            if (key == null || key.Length < 20 || key[16] != 'Z' || key[17] != '|') return false;
            var bar = key.IndexOf('|', 18);
            if (bar < 0) return false;
            if (!DateTime.TryParseExact(key.Substring(0, 17), "yyyy-MM-dd'T'HH:mm'Z'", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc)) return false;
            family = key.Substring(18, bar - 18); token = key.Substring(bar + 1);
            return family.Length > 0 && token.Length > 0;
        }

        /// <summary>The file beside a character's totals file (LocalTotals.PathFor): "&lt;id&gt;-&lt;name&gt;.deeds.json".</summary>
        public static string PathFor(string totalsPath) =>
            (totalsPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? totalsPath.Substring(0, totalsPath.Length - 5) : totalsPath) + ".deeds.json";

        /// <summary>A session's record in place of its earlier one (the same world and id), then everything trimmed to the bounds. A session with no
        /// stretch is not kept.</summary>
        public void Record(DeedSession s, DateTime nowUtc)
        {
            if (s == null) return;
            Sessions.RemoveAll(x => x.World == s.World && x.Id == s.Id);
            s.Live = false;
            if (s.Stretches.Count > 0) Sessions.Insert(0, s);
            Trim(nowUtc);
        }

        /// <summary>The earlier sessions of <paramref name="world"/> (every one but <paramref name="current"/>), newest first, trimmed at <paramref name="nowUtc"/>.</summary>
        public List<DeedSession> Earlier(string world, string current, DateTime nowUtc)
        {
            Trim(nowUtc);
            return Sessions.Where(s => s.World == (world ?? "") && s.Id != current).ToList();
        }

        void Trim(DateTime nowUtc)
        {
            var cut = nowUtc - MaxAge;
            foreach (var s in Sessions) s.Stretches.RemoveAll(x => x.ToUtc < cut);
            Sessions.RemoveAll(s => s.Stretches.Count == 0);
            Sessions.Sort((a, b) => b.ToUtc.CompareTo(a.ToUtc));
            var perWorld = new Dictionary<string, int>();
            Sessions.RemoveAll(s => { perWorld.TryGetValue(s.World, out var n); perWorld[s.World] = n + 1; return n >= MaxSessions; });
            var worlds = Sessions.Select(s => s.World).Distinct().Take(MaxWorlds).ToList();   // newest first: the worlds played last
            Sessions.RemoveAll(s => !worlds.Contains(s.World));
        }

        /// <summary>Review fix (0.9): every kept first moment whose mark is from before-0.9 knowledge (FirstsBook.FromKnowledge) flagged Known, so a
        /// session saved before the known set was seeded never sends it in a block. True when any changed (the caller saves).</summary>
        public bool FlagKnownFirsts(FirstsBook firsts)
        {
            if (firsts == null) return false;
            var changed = false;
            foreach (var m in Sessions.SelectMany(s => s.Stretches).SelectMany(st => st.Moments))
                if (m.Kind == DeedMoment.First && !m.Known && firsts.FromKnowledge(m.Token)) { m.Known = true; changed = true; }
            return changed;
        }

        // ---------- the file ----------

        public string ToJson(DateTime savedUtc)
        {
            var j = new Json().Open().Num("version", Version).Str("playerId", PlayerId).Str("name", Name).Str("saved", savedUtc.ToString("o", Inv));
            j.Key("sessions").OpenArr();
            foreach (var s in Sessions) s.WriteTo(j);
            return j.CloseArr().Close().ToString();
        }

        /// <summary>Reads what ToJson wrote; null when it is not a deeds file of a version this mod reads.</summary>
        public static KeptDeeds FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root) || MiniJson.Num(root, "version") < 1 || MiniJson.Num(root, "version") > Version) return null;
            var k = new KeptDeeds { PlayerId = MiniJson.Str(root, "playerId"), Name = MiniJson.Str(root, "name") };
            if (root.TryGetValue("sessions", out var ss) && ss is List<object> list)
                foreach (var x in list.OfType<Dictionary<string, object>>().Take(MaxWorlds * MaxSessions))
                {
                    var s = DeedSession.ReadFrom(x);
                    if (s != null && s.Stretches.Count > 0 && !k.Sessions.Any(o => o.World == s.World && o.Id == s.Id)) k.Sessions.Add(s);
                }
            k.Sessions.Sort((a, b) => b.ToUtc.CompareTo(a.ToUtc));
            return k;
        }

        /// <summary>Written whole with one backup (AtomicFile); nothing while Dev.SampleData is on.</summary>
        public void Save(string path, DateTime savedUtc)
        {
            if (Panel.SampleMode.Quiet("kept deeds")) return;
            AtomicFile.Write(path, ToJson(savedUtc));
        }

        /// <summary>The deed stretches kept at <paramref name="path"/> for this character (its .bak tried when the file is unreadable). No file,
        /// another character's, or one this version cannot read (<paramref name="problem"/> says so): an empty book for this character.</summary>
        public static KeptDeeds Load(string path, long playerId, string name, out string problem)
        {
            problem = null;
            var empty = new KeptDeeds { PlayerId = playerId.ToString(Inv), Name = name ?? "" };
            if (path == null || (!File.Exists(path) && !File.Exists(path + AtomicFile.BackupSuffix))) return empty;
            KeptDeeds k = null;
            try { AtomicFile.ReadWithBackup(path, t => (k = FromJson(t)) != null, out _); }
            catch (Exception e) { problem = "kept deeds " + path + " could not be read: " + e.Message; return empty; }
            if (k == null) { problem = "kept deeds " + path + " are unreadable: the deed feeds start with this session"; return empty; }
            return k.PlayerId == empty.PlayerId && k.Name == empty.Name ? k : empty;
        }
    }
}
