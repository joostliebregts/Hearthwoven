using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// One earlier session's fights in one world, kept on this PC (KeptFights): the session's id, when its connection began and when it was last
    /// saved, and the records of its fights. Yours first (Self: your encounters as your shared feed carries them, with your hits), then each fellow
    /// player's part of the fights you were in together, as their shared copy held it at that save. Joined again (GroupFights.Join), they give the
    /// same group fight the pages drew while it was live.
    /// </summary>
    public sealed class KeptSession
    {
        public string World = "", Id = "";
        public DateTime FromUtc, ToUtc;
        public readonly List<FightSide> Sides = new List<FightSide>();
        public FightSide Mine => Sides.FirstOrDefault(s => s.Self);
        /// <summary>Your fights in it, newest first (none: empty).</summary>
        public List<FoeFight> Fights => Mine?.Fights ?? new List<FoeFight>();
    }

    /// <summary>
    /// 0.8.5 (Joost in game, after a restart: Battle > Last fight said "No fight yet this session"; "last fight should show your last fight, but
    /// indicate that it was x hours/days ago. Same for the feed."): your recent fights kept across a restart, per world and character, as
    /// KeptMinutes keeps the last minutes. Battle > Last fight shows the newest even from an earlier session, the feed lists the earlier sessions'
    /// fights under this session's.
    ///
    /// Where: local/&lt;playerId&gt;-&lt;name&gt;.fights.json beside the character's totals file, written whole (AtomicFile, one .bak) at every
    /// local save once this session has a fight. Each session is one block with its world (FellowCache.ScopeKey): a session of another world is
    /// never shown and stays in the file for that world (at most MaxWorlds worlds, the ones played last).
    ///
    /// Bounded per world: your newest whole fights up to MaxFoes foes (BattleRecorder.MaxFeed, what the recorder itself keeps of one session, so
    /// what is kept never outweighs a live session and the feed's filter reaches about as far back as within a session), none that ended more than
    /// MaxAge ago (the 7 days of the longest recent window: a week-old fight is history, which Damage and Foes keep as day rows). A fellow's
    /// fights are kept only beside one of yours. What your copy shares is what is kept (FeedShare.From): names only of players who share, anyone
    /// else counted as another player; no place. Pure C# with files: tested without the game (test/KeptFightsTests.cs).
    /// </summary>
    public sealed class KeptFights
    {
        public const int Version = 1;
        /// <summary>Your foes kept per world (whole fights, newest first). At most FeedShare.MaxRead, which is what a feed reads back.</summary>
        public const int MaxFoes = BattleRecorder.MaxFeed, MaxWorlds = 4;
        public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string PlayerId = "", Name = "";
        /// <summary>Every world's sessions, newest first (by their last save).</summary>
        public readonly List<KeptSession> Sessions = new List<KeptSession>();

        /// <summary>The file beside a character's totals file (LocalTotals.PathFor): "&lt;id&gt;-&lt;name&gt;.fights.json".</summary>
        public static string PathFor(string totalsPath) =>
            (totalsPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? totalsPath.Substring(0, totalsPath.Length - 5) : totalsPath) + ".fights.json";

        /// <summary>
        /// This session's record now: your newest MaxFoes foes of <paramref name="rec"/> as your shared feed carries them (<paramref name="shares"/>:
        /// whether a player shares; anyone else is another player), your falls in those fights, and each fellow's fights that join one of yours
        /// (GroupFights.Join over <paramref name="fellows"/>, their shared feeds as this PC holds them now).
        /// </summary>
        public static KeptSession Take(string world, string id, DateTime fromUtc, DateTime nowUtc, BattleRecorder rec, string me, Func<string, bool> shares, IEnumerable<FightSide> fellows)
        {
            var s = new KeptSession { World = world ?? "", Id = id ?? "", FromUtc = fromUtc, ToUtc = nowUtc };
            var feed = FeedShare.From(rec, shares, MaxFoes);
            var numbers = new HashSet<int>(feed.Fights.Select(f => f.Number));
            var mine = new FightSide { Name = me ?? "", Self = true, Fights = feed.Fights, Falls = (rec?.Falls() ?? new List<Fall>()).Where(f => numbers.Contains(f.Fight)).ToList() };
            s.Sides.Add(mine);
            var others = (fellows ?? Enumerable.Empty<FightSide>()).Where(x => x != null && !x.Self).ToList();
            if (others.Count == 0 || mine.Fights.Count == 0) return s;
            var sides = new List<FightSide> { mine }; sides.AddRange(others);
            var theirs = new Dictionary<FightSide, HashSet<FoeFight>>();
            foreach (var g in GroupFights.Join(sides, nowUtc))
            {
                if (!g.Players.Any(p => p.Self)) continue;
                foreach (var p in g.Players.Where(p => !p.Self))
                {
                    var side = others.FirstOrDefault(x => x.Key == p.Key && x.Name == p.Name);
                    if (side == null) continue;
                    if (!theirs.TryGetValue(side, out var set)) theirs[side] = set = new HashSet<FoeFight>();
                    foreach (var f in p.Fights) set.Add(f);
                }
            }
            foreach (var side in others.Where(theirs.ContainsKey))
            {
                var kept = side.Fights.Where(theirs[side].Contains).ToList();
                var nums = new HashSet<int>(kept.Select(f => f.Number));
                s.Sides.Add(new FightSide { Name = side.Name, Key = side.Key, ReceivedUtc = side.ReceivedUtc, Fights = kept, Falls = (side.Falls ?? new List<Fall>()).Where(f => nums.Contains(f.Fight)).ToList() });
            }
            return s;
        }

        /// <summary>A session's record in place of its earlier one (the same world and id), then everything trimmed to the bounds.</summary>
        public void Record(KeptSession s, DateTime nowUtc)
        {
            if (s == null) return;
            Sessions.RemoveAll(x => x.World == s.World && x.Id == s.Id);
            Sessions.Insert(0, s);
            Trim(nowUtc);
        }

        /// <summary>The earlier sessions of <paramref name="world"/> (every one but <paramref name="current"/>), newest first, trimmed to the bounds at
        /// <paramref name="nowUtc"/>: what Last fight and the feed show of them.</summary>
        public List<KeptSession> Earlier(string world, string current, DateTime nowUtc)
        {
            Trim(nowUtc);
            return Sessions.Where(s => s.World == (world ?? "") && s.Id != current).ToList();
        }

        /// <summary>0.8.5 (review, B23): sharing switched off: every fellow's part of every world's sessions goes; your own fights stay. Returns
        /// whether any went.</summary>
        public bool ForgetFellows() => Sessions.Sum(s => s.Sides.RemoveAll(x => !x.Self)) > 0;

        /// <summary>
        /// The bounds, per world: your newest whole fights while they fit in MaxFoes foes and ended within MaxAge, then none older (no gap); a fellow's
        /// fight only while it meets one of yours that stays (within BattleRecorder.FightGap: what joined it); a session with none of yours left
        /// goes; at most MaxWorlds worlds, the ones played last.
        /// </summary>
        void Trim(DateTime nowUtc)
        {
            var cut = nowUtc - MaxAge;
            Sessions.Sort((a, b) => b.ToUtc.CompareTo(a.ToUtc));
            foreach (var world in Sessions.Select(x => x.World).Distinct().ToList())
            {
                int foes = 0; var full = false;
                foreach (var s in Sessions.Where(x => x.World == world))
                {
                    var mine = s.Mine;
                    if (mine == null) { s.Sides.Clear(); continue; }
                    var keep = new List<FoeFight>();
                    foreach (var f in mine.Fights.OrderByDescending(f => f.EndUtc))
                    {
                        full = full || f.EndUtc < cut || foes + f.Foes > MaxFoes;
                        if (full) break;
                        keep.Add(f); foes += f.Foes;
                    }
                    if (keep.Count == mine.Fights.Count) continue;
                    var numbers = new HashSet<int>(keep.Select(f => f.Number));
                    mine.Fights = keep; mine.Falls = (mine.Falls ?? new List<Fall>()).Where(x => numbers.Contains(x.Fight)).ToList();
                    foreach (var side in s.Sides.Where(x => !x.Self))
                    {
                        side.Fights = side.Fights.Where(f => keep.Any(k => f.StartUtc <= k.EndUtc + BattleRecorder.FightGap && k.StartUtc <= f.EndUtc + BattleRecorder.FightGap)).ToList();
                        var theirs = new HashSet<int>(side.Fights.Select(f => f.Number));
                        side.Falls = (side.Falls ?? new List<Fall>()).Where(x => theirs.Contains(x.Fight)).ToList();
                    }
                    s.Sides.RemoveAll(x => !x.Self && x.Fights.Count == 0);
                }
            }
            Sessions.RemoveAll(s => s.Fights.Count == 0);
            var worlds = Sessions.Select(s => s.World).Distinct().Take(MaxWorlds).ToList();   // newest first: the worlds played last
            Sessions.RemoveAll(s => !worlds.Contains(s.World));
        }

        // ---------- the file ----------

        public string ToJson(DateTime savedUtc)
        {
            var j = new Json().Open().Num("version", Version).Str("playerId", PlayerId).Str("name", Name).Str("saved", savedUtc.ToString("o", Inv));
            j.Key("sessions").OpenArr();
            foreach (var s in Sessions)
            {
                j.Open().Str("world", s.World).Str("id", s.Id).Str("from", s.FromUtc.ToString("o", Inv)).Str("to", s.ToUtc.ToString("o", Inv));
                j.Key("sides").OpenArr();
                foreach (var side in s.Sides.Where(x => x.Fights.Count > 0))
                {
                    j.Open().Str("n", side.Name ?? "");
                    if (side.Self) j.Num("me", 1);
                    if (!string.IsNullOrEmpty(side.Key)) j.Str("k", side.Key);
                    if (side.ReceivedUtc.HasValue) j.Str("at", side.ReceivedUtc.Value.ToString("o", Inv));
                    var feed = new FeedShare();
                    feed.Fights.AddRange(side.Fights); feed.Falls.AddRange(side.Falls ?? new List<Fall>());
                    feed.T0 = side.Fights.Select(f => f.StartUtc).Concat(feed.Falls.Select(f => f.Utc)).Min();   // every time in it is whole seconds from here
                    feed.WriteTo(j, "feed", hits: side.Self);
                    j.Close();
                }
                j.CloseArr().Close();
            }
            return j.CloseArr().Close().ToString();
        }

        /// <summary>Reads what ToJson wrote; null when it is not a fights file of a version this mod reads. Every fight comes back ended: none open,
        /// a foe still in the fight at the save got away when the session ended (AwayHow.YouLeft: "survived").</summary>
        public static KeptFights FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root) || MiniJson.Num(root, "version") < 1 || MiniJson.Num(root, "version") > Version) return null;
            var k = new KeptFights { PlayerId = MiniJson.Str(root, "playerId"), Name = MiniJson.Str(root, "name") };
            if (!(root.TryGetValue("sessions", out var ss) && ss is List<object> list)) return k;
            foreach (var x in list.OfType<Dictionary<string, object>>())
            {
                var s = new KeptSession { World = MiniJson.Str(x, "world"), Id = MiniJson.Str(x, "id"), FromUtc = Time(MiniJson.Str(x, "from")), ToUtc = Time(MiniJson.Str(x, "to")) };
                if (x.TryGetValue("sides", out var sd) && sd is List<object> sides)
                    foreach (var o in sides.OfType<Dictionary<string, object>>())
                    {
                        var feed = FeedShare.ReadFrom(MiniJson.Obj(o, "feed"));
                        if (feed == null || feed.Fights.Count == 0) continue;
                        var side = new FightSide { Name = MiniJson.Str(o, "n"), Self = MiniJson.Num(o, "me") > 0, Key = MiniJson.Str(o, "k", null), Fights = feed.Fights, Falls = feed.Falls };
                        if (o.ContainsKey("at")) side.ReceivedUtc = Time(MiniJson.Str(o, "at"));
                        if (side.Self && s.Mine != null) continue;   // one record of yours a session
                        foreach (var f in side.Fights)
                        {
                            f.Open = false; f.Kept = s;
                            foreach (var e in f.Entries) if (e.Outcome == FoeOutcome.Fighting) { e.Outcome = FoeOutcome.GotAway; e.Away = AwayHow.YouLeft; }
                        }
                        s.Sides.Add(side);
                    }
                if (s.Mine != null) k.Sessions.Add(s);
            }
            k.Sessions.Sort((a, b) => b.ToUtc.CompareTo(a.ToUtc));
            return k;
        }

        static DateTime Time(string s) => DateTime.TryParse(s, Inv, DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : DateTime.MinValue;

        /// <summary>Written whole with one backup (AtomicFile); nothing while Dev.SampleData is on.</summary>
        public void Save(string path, DateTime savedUtc)
        {
            if (Panel.SampleMode.Quiet("kept fights")) return;
            AtomicFile.Write(path, ToJson(savedUtc));
        }

        /// <summary>
        /// The fights kept at <paramref name="path"/> for this character (its .bak tried when the file is unreadable), parsed once. No file, another
        /// character's (a copied or renamed file: the next save replaces it) or one this version cannot read (<paramref name="problem"/> says so): an
        /// empty book for this character.
        /// </summary>
        public static KeptFights Load(string path, long playerId, string name, out string problem)
        {
            problem = null;
            var empty = new KeptFights { PlayerId = playerId.ToString(Inv), Name = name ?? "" };
            if (path == null || (!File.Exists(path) && !File.Exists(path + AtomicFile.BackupSuffix))) return empty;
            KeptFights k = null;
            try { AtomicFile.ReadWithBackup(path, t => (k = FromJson(t)) != null, out _); }
            catch (Exception e) { problem = "kept fights " + path + " could not be read: " + e.Message; return empty; }
            if (k == null) { problem = "kept fights " + path + " are unreadable: Last fight and the feed start with this session"; return empty; }
            return k.PlayerId == empty.PlayerId && k.Name == empty.Name ? k : empty;
        }
    }
}
