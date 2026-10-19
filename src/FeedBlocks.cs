using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Hearthwoven
{
    /// <summary>
    /// 0.8.6 (a friend's older feed, work/hearthwoven-0.8.6/DESIGN-feed-blocks.md): a friend's feed reaches back 30 days. Their own PC cuts each of its
    /// closed sessions (KeptFights) into one block and sends it to the server once; the server stores it, lists it in the group list and serves it to
    /// a friend who asks for it, one at a time, as Show older foes reaches it. Pure C# with files: tested without the game (test/FeedBlockTests.cs).
    ///
    /// A block is one closed session's battle feed in the shared copy's form (FeedShare): no hits, no foe network ids, no fellow's part, no place; names
    /// only of players who shared when it was saved, anyone else counted. The server never stores what it received: it stores what it read back and
    /// wrote again (FeedBlock.Canonical), so no other key is ever stored or served.
    /// </summary>
    public sealed class BlockRow
    {
        public string Id = "";
        public DateTime FromUtc, ToUtc;
        public int Foes;
        /// <summary>0.9 (deed feeds): the deed stretches the block holds (its head's "deeds"); 0 for a block of fights only.</summary>
        public int Stretches;
    }

    /// <summary>One closed session's feed: <c>{"v":1,"id":"3f9a0c1e2b7d","from":"ISO","to":"ISO","battleFeed":{FeedShare, no hits, no ids}}</c>.</summary>
    public sealed class FeedBlock
    {
        public const int Version = 1;
        public static readonly Regex IdPattern = new Regex("^[0-9a-f]{12}$", RegexOptions.Compiled);
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string Id = "";
        public DateTime FromUtc, ToUtc;
        public FeedShare Feed;
        /// <summary>The block as a kept session (one side, yours): every fight closed and tied to it, as the feed draws an earlier session of your own.</summary>
        public KeptSession Session;
        /// <summary>The foes read (the entries), not a number the sender claims.</summary>
        public int Foes => Feed == null ? 0 : Feed.Fights.Sum(f => f.Entries.Count);
        /// <summary>0.9 (deed feeds, work/hearthwoven-0.9/TRACK-feeds.md): the same closed session's deed stretches (KeptDeeds), cleaned and bounded as
        /// DeedBlockPart reads them; null when the block carries none (a block of fights only, as 0.8.6 wrote them).</summary>
        public DeedSession Deeds;
        /// <summary>The deed stretches read, not a number the sender claims.</summary>
        public int Stretches => Deeds?.Stretches.Count ?? 0;

        /// <summary>A closed session as the block JSON (your side only: your fights and falls); null when it holds none or its id is not one.</summary>
        public static string Write(KeptSession s) => Write(s, null);

        /// <summary>
        /// 0.9: a closed session as one block: its fights (your side only, as 0.8.6) and its deed stretches (<paramref name="deeds"/>, the same session id,
        /// at most <paramref name="maxStretches"/> newest ones, cleaned as DeedBlockPart reads them). Either part may be missing; null when both are
        /// empty or the id is not one. A block with fights only is written exactly as 0.8.6 wrote it.
        /// </summary>
        public static string Write(KeptSession s, DeedSession deeds, int maxStretches = DeedBlockPart.MaxStretches)
        {
            var mine = s?.Mine;
            var hasFights = mine != null && mine.Fights.Count > 0;
            var part = DeedBlockPart.Clean(deeds, maxStretches);
            var hasDeeds = part != null && part.Stretches.Count > 0;
            if (hasFights && hasDeeds && deeds.Id != s.Id) hasDeeds = false;   // never another session's stretches under this id
            var id = hasFights ? s.Id : deeds?.Id;
            if ((!hasFights && !hasDeeds) || !IdPattern.IsMatch(id ?? "")) return null;
            FeedShare feed = null;
            if (hasFights)
            {
                feed = new FeedShare();
                feed.Fights.AddRange(mine.Fights);
                feed.Falls.AddRange(mine.Falls ?? new List<Fall>());
                feed.T0 = mine.Fights.Select(f => f.StartUtc).Concat(feed.Falls.Select(f => f.Utc)).Min();   // every time in it is whole seconds from here
            }
            DateTime from = hasFights ? s.FromUtc : part.FromUtc, to = hasFights ? s.ToUtc : part.ToUtc;
            if (hasDeeds && hasFights) { if (part.FromUtc < from) from = part.FromUtc; if (part.ToUtc > to) to = part.ToUtc; }
            if (hasDeeds) part.Id = id;
            return Compose(id, from, to, feed, hasDeeds ? part : null);
        }

        /// <summary>What the server stores and serves: the block as it was read, written again. An unknown key, a position or a hit count is gone.</summary>
        public static string Canonical(FeedBlock b) => Compose(b.Id, b.FromUtc, b.ToUtc, b.Feed != null && b.Feed.Fights.Count > 0 ? b.Feed : null, b.Stretches > 0 ? b.Deeds : null);

        static string Compose(string id, DateTime from, DateTime to, FeedShare feed, DeedSession deeds = null)
        {
            var j = new Json().Open().Num("v", Version).Str("id", id).Str("from", from.ToUniversalTime().ToString("o", Inv)).Str("to", to.ToUniversalTime().ToString("o", Inv));
            feed?.WriteTo(j, "battleFeed", hits: false, ids: false);
            if (deeds != null) { j.Key(DeedBlockPart.Key); deeds.WriteTo(j); }   // 0.9: after the fights; a 0.8.6 reader takes what it knows and leaves the rest
            return j.Close().ToString();
        }

        /// <summary>The first line of a stored block: what the index needs without reading the block (0.9: "deeds", its stretches, when it has any).</summary>
        public static string Head(FeedBlock b)
        {
            var j = new Json().Open().Num("v", Version).Str("id", b.Id).Str("from", b.FromUtc.ToString("o", Inv)).Str("to", b.ToUtc.ToString("o", Inv)).Num("foes", b.Foes);
            if (b.Stretches > 0) j.Num("deeds", b.Stretches);
            return j.Close().ToString();
        }

        /// <summary>Reads block JSON; null when it is not one: version 1, an id of 12 hex characters, <c>to</c> not before <c>from</c>, a feed with fights.
        /// Bounded as FeedShare.ReadFrom (200 foes, names capped); boss fights and runs are dropped. Every fight comes back closed (KeptFights.CloseFights).</summary>
        public static FeedBlock Read(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root) || MiniJson.Num(root, "v") != Version) return null;
            var id = MiniJson.Str(root, "id");
            if (!IdPattern.IsMatch(id)) return null;
            if (!DateTime.TryParse(MiniJson.Str(root, "from"), Inv, DateTimeStyles.RoundtripKind, out var from) || !DateTime.TryParse(MiniJson.Str(root, "to"), Inv, DateTimeStyles.RoundtripKind, out var to)) return null;
            from = from.ToUniversalTime(); to = to.ToUniversalTime();
            if (to < from) return null;
            var battle = MiniJson.Obj(root, "battleFeed");
            var feed = battle == null ? null : FeedShare.ReadFrom(battle);
            var deeds = DeedBlockPart.Read(MiniJson.Obj(root, DeedBlockPart.Key), id);   // 0.9: the session's deed stretches, cleaned and bounded
            if ((feed == null || feed.Fights.Count == 0) && deeds == null) return null;
            if (feed == null) feed = new FeedShare();
            feed.Bosses.Clear(); feed.Runs.Clear();
            var session = new KeptSession { Id = id, FromUtc = from, ToUtc = to };
            session.Sides.Add(new FightSide { Name = "", Self = true, Fights = feed.Fights, Falls = feed.Falls });
            KeptFights.CloseFights(session);
            return new FeedBlock { Id = id, FromUtc = from, ToUtc = to, Feed = feed, Session = session, Deeds = deeds };
        }

        public BlockRow Row => new BlockRow { Id = Id, FromUtc = FromUtc, ToUtc = ToUtc, Foes = Foes, Stretches = Stretches };
    }

    /// <summary>
    /// Server: the blocks it holds, one world at a time (the server's own). players/&lt;owner&gt;/feed/&lt;world&gt;/&lt;id&gt;.json, two lines: the head
    /// (<c>{"v":1,"id":..,"from":..,"to":..,"foes":n}</c>) and the canonical block. Written once (AtomicFile), never replaced. The index lives in memory
    /// and is rebuilt from the heads on first use, so a restart needs nothing else.
    /// Bounded per owner and world, as the friend's own book (KeptFights): none that ended more than 30 days ago, the newest MaxBlocks, the newest whose
    /// foes add up to at most MaxFoes. What falls out is deleted. Sharing off deletes every world's blocks of that owner (Forget).
    /// </summary>
    public sealed class BlockStore
    {
        /// <summary>0.9: the deed stretches one owner's blocks of a world hold together at most (picked: a month of busy evenings is about 400).</summary>
        public const int MaxStretches = 2000;
        public const int MaxBlocks = 60, MaxFoes = KeptFights.MaxFoes, MaxBlockPacked = Uploads.MaxParts * Fragments.Size, MaxBlockJson = 128 * 1024, MaxFutureMinutes = 5, ShareDays = GroupShare.MaxAgeDays;
        public static readonly TimeSpan MaxAge = KeptFights.MaxAge;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly string playersDir, world;
        readonly Dictionary<string, List<BlockRow>> rows = new Dictionary<string, List<BlockRow>>(StringComparer.Ordinal);   // owner -> newest first
        readonly Dictionary<string, string> ownerOf = new Dictionary<string, string>(StringComparer.Ordinal);                // id -> owner
        bool loaded;

        public BlockStore(string playersDir, string world) { this.playersDir = playersDir; this.world = world ?? ""; }

        public string World => world;
        /// <summary>How many blocks are held in this world (all owners).</summary>
        public int Count { get { Load(DateTime.UtcNow); return ownerOf.Count; } }

        static bool SafeName(string s) => !string.IsNullOrEmpty(s) && s.Length <= 120 && s.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !s.StartsWith(".", StringComparison.Ordinal) && !s.Contains("..");

        string FolderOf(string owner) => Path.Combine(playersDir, owner, "feed", world);
        string FileOf(string owner, string id) => Path.Combine(FolderOf(owner), id + ".json");

        /// <summary>The server's world as a folder name (FellowCache.ScopeKey of its name and id).</summary>
        public static string WorldKey(string name, long uid) => FellowCache.ScopeKey(name, uid);

        // ---------- the index ----------

        void Load(DateTime now)
        {
            if (loaded) return;
            loaded = true;
            if (string.IsNullOrEmpty(playersDir) || !Directory.Exists(playersDir) || !SafeName(world)) return;
            foreach (var ownerDir in Directory.GetDirectories(playersDir))
            {
                var owner = Path.GetFileName(ownerDir);
                var folder = Path.Combine(ownerDir, "feed", world);
                if (!SafeName(owner) || !Directory.Exists(folder)) continue;
                var list = new List<BlockRow>();
                foreach (var file in Directory.GetFiles(folder, "*.json"))
                {
                    var row = file.EndsWith(".json", StringComparison.Ordinal) ? ReadHead(file) : null;
                    if (row == null || row.Id != Path.GetFileNameWithoutExtension(file) || ownerOf.ContainsKey(row.Id)) { Delete(file); continue; }
                    list.Add(row); ownerOf[row.Id] = owner;
                }
                if (list.Count == 0) continue;
                rows[owner] = list.OrderByDescending(r => r.ToUtc).ToList();
                Trim(owner, now);
            }
        }

        /// <summary>The head line of a stored block (null when the file is unreadable or its head is not one).</summary>
        public static BlockRow ReadHead(string file)
        {
            try
            {
                string line;
                using (var r = new StreamReader(file, new UTF8Encoding(false, true))) line = r.ReadLine();
                if (string.IsNullOrEmpty(line) || line.Length > 400 || !(MiniJson.Parse(line) is Dictionary<string, object> h) || MiniJson.Num(h, "v") != FeedBlock.Version) return null;
                var id = MiniJson.Str(h, "id");
                if (!FeedBlock.IdPattern.IsMatch(id)) return null;
                if (!DateTime.TryParse(MiniJson.Str(h, "from"), Inv, DateTimeStyles.RoundtripKind, out var from) || !DateTime.TryParse(MiniJson.Str(h, "to"), Inv, DateTimeStyles.RoundtripKind, out var to)) return null;
                var foes = (int)Math.Max(0, Math.Min(FeedShare.MaxRead, MiniJson.Num(h, "foes")));
                var stretches = (int)Math.Max(0, Math.Min(DeedBlockPart.MaxStretches, MiniJson.Num(h, "deeds")));   // 0.9; a 0.8.6 head has none
                return new BlockRow { Id = id, FromUtc = from.ToUniversalTime(), ToUtc = to.ToUniversalTime(), Foes = foes, Stretches = stretches };
            }
            catch { return null; }
        }

        static void Delete(string file)
        {
            try { foreach (var f in new[] { file, file + AtomicFile.BackupSuffix, file + AtomicFile.TempSuffix }) if (File.Exists(f)) File.Delete(f); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        // the bounds for one owner; returns the ids that fell out (their files are deleted)
        List<string> Trim(string owner, DateTime now)
        {
            var gone = new List<string>();
            if (!rows.TryGetValue(owner, out var list)) return gone;
            list.Sort((a, b) => b.ToUtc.CompareTo(a.ToUtc));
            var cut = now - MaxAge; int foes = 0, stretches = 0; var keep = new List<BlockRow>(); var full = false;
            foreach (var r in list)
            {
                full = full || r.ToUtc < cut || keep.Count >= MaxBlocks || foes + r.Foes > MaxFoes || stretches + r.Stretches > MaxStretches;   // the newest while they fit, then none older (no gap)
                if (full) { gone.Add(r.Id); continue; }
                keep.Add(r); foes += r.Foes; stretches += r.Stretches;
            }
            foreach (var id in gone) { Delete(FileOf(owner, id)); ownerOf.Remove(id); }
            if (keep.Count == 0) rows.Remove(owner); else rows[owner] = keep;
            return gone;
        }

        // ---------- put ----------

        /// <summary>
        /// A block the owner uploaded, packed (gzip of the block JSON): checked in this order, each before the next costs anything: the packed size
        /// (MaxBlockPacked), the unpacked size (MaxBlockJson, read in blocks, never inflated past it), one JSON object, then Put. Returns null when it
        /// was stored, else why not.
        /// </summary>
        public string PutPacked(string owner, byte[] packed, DateTime now, string currentSession)
        {
            if (packed == null || packed.Length == 0) return "empty";
            if (packed.Length > MaxBlockPacked) return "packed over " + MaxBlockPacked + " bytes";
            var text = ServerIntake.Gunzip(packed, MaxBlockJson, out var why);
            if (text == null) return "unpacked: " + why;
            if (!ServerIntake.IsJsonObject(text)) return "not one JSON object";
            var block = FeedBlock.Read(text);
            return block == null ? "not a block" : Put(owner, block, now, currentSession);
        }

        /// <summary>Stores a block read from an upload under <paramref name="owner"/> (the uploader's own key, never one from the message). Returns null
        /// when it was stored, else why not: its id is the owner's running session, an id held by anyone, to older than 30 days or more than 5
        /// minutes ahead, empty, or outside the bounds.</summary>
        public string Put(string owner, FeedBlock block, DateTime now, string currentSession)
        {
            if (block == null || !SafeName(owner) || !SafeName(world)) return "no owner";
            if (!FeedBlock.IdPattern.IsMatch(block.Id)) return "bad id";
            if (!string.IsNullOrEmpty(currentSession) && block.Id == currentSession) return "the running session";
            if (block.ToUtc < now - MaxAge) return "older than 30 days";
            if (block.ToUtc > now.AddMinutes(MaxFutureMinutes)) return "in the future";
            if (block.Foes == 0 && block.Stretches == 0) return "empty";   // 0.9: a session of deeds without a fight is a block too
            Load(now);
            if (ownerOf.ContainsKey(block.Id)) return "held";
            var text = FeedBlock.Head(block) + "\n" + FeedBlock.Canonical(block);
            AtomicFile.Write(FileOf(owner, block.Id), text);
            if (!rows.TryGetValue(owner, out var list)) rows[owner] = list = new List<BlockRow>();
            list.Add(block.Row); ownerOf[block.Id] = owner;
            return Trim(owner, now).Contains(block.Id) ? "outside the bounds" : null;
        }

        // ---------- read ----------

        /// <summary>The owner's blocks in this world, newest first (their ids are what the owner need not upload again).</summary>
        public List<BlockRow> Rows(string owner, DateTime now)
        {
            Load(now);
            return owner != null && rows.TryGetValue(owner, out var l) ? l.Where(r => r.ToUtc >= now - MaxAge).ToList() : new List<BlockRow>();
        }

        // held by the owner and not older than MaxAge (the bound Put and Trim keep)
        bool Fresh(string owner, string id, DateTime now) => rows.TryGetValue(owner, out var l) && l.Any(r => r.Id == id && r.ToUtc >= now - MaxAge);

        /// <summary>Whether the owner's shared copy is served now: its share file is here and was written within ShareDays (the group answer's own rule).</summary>
        public bool Shares(string owner, DateTime now)
        {
            if (!SafeName(owner)) return false;
            var file = Path.Combine(playersDir, owner + ".share.json");
            return File.Exists(file) && (now - File.GetLastWriteTimeUtc(file)).TotalDays <= ShareDays;
        }

        /// <summary>The owner's blocks as a friend sees them: none while their copy is not served.</summary>
        public List<BlockRow> Listed(string owner, DateTime now) => Shares(owner, now) ? Rows(owner, now) : new List<BlockRow>();

        /// <summary>The canonical block <paramref name="id"/> for <paramref name="requester"/>, or null (gone): not held, older than 30 days, its owner's copy not
        /// served, or the requester's own.</summary>
        public string Serve(string requester, string id, DateTime now)
        {
            Load(now);
            if (!FeedBlock.IdPattern.IsMatch(id ?? "") || !ownerOf.TryGetValue(id, out var owner) || owner == requester || !Shares(owner, now) || !Fresh(owner, id, now)) return null;
            try
            {
                var text = AtomicFile.ReadWithBackup(FileOf(owner, id), t => t.IndexOf('\n') > 0 && ServerIntake.IsJsonObject(t.Substring(t.IndexOf('\n') + 1)), out _);
                var nl = text?.IndexOf('\n') ?? -1;
                return nl > 0 ? text.Substring(nl + 1) : null;
            }
            catch (IOException) { return null; } catch (UnauthorizedAccessException) { return null; }
        }

        // ---------- forget and sweep ----------

        /// <summary>Sharing off: every world's blocks of this owner are deleted.</summary>
        public void Forget(string owner)
        {
            if (!SafeName(owner)) return;
            Load(DateTime.UtcNow);
            if (rows.TryGetValue(owner, out var list)) { foreach (var r in list) ownerOf.Remove(r.Id); rows.Remove(owner); }
            try { var dir = Path.Combine(playersDir, owner, "feed"); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        /// <summary>Once per server start: every world's blocks of every owner that ended more than 30 days ago (or whose head is unreadable) are deleted,
        /// and the folders left empty. A world the server no longer plays ages out this way.</summary>
        public static int SweepAll(string playersDir, DateTime now)
        {
            int n = 0;
            if (string.IsNullOrEmpty(playersDir) || !Directory.Exists(playersDir)) return 0;
            foreach (var ownerDir in Directory.GetDirectories(playersDir))
            {
                var feed = Path.Combine(ownerDir, "feed");
                if (!Directory.Exists(feed)) continue;
                try
                {
                    foreach (var worldDir in Directory.GetDirectories(feed))
                    {
                        foreach (var file in Directory.GetFiles(worldDir))
                        {
                            var row = file.EndsWith(".json", StringComparison.Ordinal) ? ReadHead(file) : null;
                            if (row == null || row.ToUtc < now - MaxAge) { Delete(file); n++; }
                        }
                        if (Directory.GetFileSystemEntries(worldDir).Length == 0) Directory.Delete(worldDir);
                    }
                    if (Directory.GetFileSystemEntries(feed).Length == 0) Directory.Delete(feed);
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            return n;
        }
    }

    /// <summary>Server: the gaps between a sender's uploads and a requester's asks, in seconds, checked before any work is done. A refused one does not move the clock.</summary>
    public sealed class BlockGate
    {
        public const double UpGap = 10, AskGap = 2;
        readonly Dictionary<long, double> up = new Dictionary<long, double>(), ask = new Dictionary<long, double>();

        public bool MayUpload(long sender, double now) => May(up, sender, now, UpGap);
        public bool MayAsk(long requester, double now) => May(ask, requester, now, AskGap);

        static bool May(Dictionary<long, double> last, long who, double now, double gap)
        {
            if (last.TryGetValue(who, out var t) && now >= t && now - t < gap) return false;
            last[who] = now; return true;
        }

        /// <summary>Forgets who is no longer connected (bounded by the connected peers).</summary>
        public void Prune(Func<long, bool> connected)
        {
            foreach (var k in up.Keys.Where(k => !connected(k)).ToList()) up.Remove(k);
            foreach (var k in ask.Keys.Where(k => !connected(k)).ToList()) ask.Remove(k);
        }
    }

    /// <summary>
    /// Server: the answers waiting to go out, one part per tick to everyone (GroupShare.Tick, 100 ms). A friend's block (AddBlock: one per requester, a few parts) takes
    /// turns with the rest (Add: group answers, live updates) instead of waiting behind every part queued before it: while both wait, a block part goes, then one of the
    /// rest, and so on. The pace and the cap are unchanged (Count counts both), the rest keeps its order and gets at least every other part (0.8.6 test build 5: a block
    /// reached the PC 124 ms after a fellow's copy it had queued behind).
    /// </summary>
    public sealed class AnswerQueue<T>
    {
        readonly Queue<T> rest = new Queue<T>(), blocks = new Queue<T>();
        bool blockLast;

        public int Count => rest.Count + blocks.Count;
        public IEnumerable<T> Rest => rest;
        public IEnumerable<T> Blocks => blocks;
        public void Add(T item) => rest.Enqueue(item);
        public void AddBlock(T item) => blocks.Enqueue(item);

        public bool TryNext(out T item)
        {
            var block = blocks.Count > 0 && (rest.Count == 0 || !blockLast);
            if (!block && rest.Count == 0) { item = default(T); return false; }
            item = block ? blocks.Dequeue() : rest.Dequeue(); blockLast = block;
            return true;
        }
    }

    /// <summary>
    /// The index the server puts in the group list after the live tag (<c>hw-blocks-1</c>), for one requester: its own held ids (what it need not upload)
    /// and each listed sharer's rows, newest first, keyed by the fellow key the list already uses.
    /// <c>{"v":1,"mine":["3f9a0c1e2b7d"],"of":{"Steam_123#77":[["5be1c0d9a4f2",1728497112,1728510003,150]]}}</c> (id, from, to in unix seconds, foes).
    /// </summary>
    public sealed class BlockIndex
    {
        public HashSet<string> Mine = new HashSet<string>(StringComparer.Ordinal);
        public Dictionary<string, List<BlockRow>> Of = new Dictionary<string, List<BlockRow>>(StringComparer.Ordinal);

        public static string Write(IEnumerable<string> mine, IEnumerable<KeyValuePair<string, List<BlockRow>>> of)
        {
            var b = new StringBuilder("{\"v\":1,\"mine\":[");
            b.Append(string.Join(",", (mine ?? Enumerable.Empty<string>()).Take(BlockStore.MaxBlocks).Select(Json.Q)));
            b.Append("],\"of\":{");
            bool first = true;
            foreach (var kv in of ?? Enumerable.Empty<KeyValuePair<string, List<BlockRow>>>())
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value == null || kv.Value.Count == 0) continue;
                if (!first) b.Append(',');
                first = false;
                b.Append(Json.Q(kv.Key)).Append(":[");
                b.Append(string.Join(",", kv.Value.Take(BlockStore.MaxBlocks).Select(r => "[" + Json.Q(r.Id) + "," + Unix(r.FromUtc) + "," + Unix(r.ToUtc) + "," + r.Foes.ToString(CultureInfo.InvariantCulture) + "]")));
                b.Append(']');
            }
            return b.Append("}}").ToString();
        }

        static string Unix(DateTime t) => new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        /// <summary>Reads an index; null when it is not one. Bounded: at most FellowCache.MaxFellows fellows, MaxBlocks rows each, MaxBlocks own ids; an id that
        /// is not 12 hex characters is skipped; times clipped to 1970..2100 and foes to FeedShare.MaxRead.</summary>
        public static BlockIndex Read(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root) || MiniJson.Num(root, "v") != 1) return null;
            var ix = new BlockIndex();
            if (root.TryGetValue("mine", out var m) && m is List<object> mine)
                foreach (var id in mine.OfType<string>().Where(i => FeedBlock.IdPattern.IsMatch(i)).Take(BlockStore.MaxBlocks)) ix.Mine.Add(id);
            foreach (var kv in (MiniJson.Obj(root, "of") ?? new Dictionary<string, object>()).Take(FellowCache.MaxFellows))
            {
                if (kv.Key.Length == 0 || kv.Key.Length > 200 || !(kv.Value is List<object> list)) continue;
                var rows = new List<BlockRow>(); var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var o in list.OfType<List<object>>().Take(BlockStore.MaxBlocks))
                {
                    if (o.Count < 4 || !(o[0] is string id) || !FeedBlock.IdPattern.IsMatch(id) || !(o[1] is double f) || !(o[2] is double t) || !(o[3] is double n) || !seen.Add(id)) continue;
                    var from = At(f); var to = At(t);
                    rows.Add(new BlockRow { Id = id, FromUtc = from, ToUtc = to < from ? from : to, Foes = (int)Math.Max(0, Math.Min(FeedShare.MaxRead, Json.IsFinite(n) ? n : 0)) });
                }
                if (rows.Count > 0) ix.Of[kv.Key] = rows;
            }
            return ix;
        }

        /// <summary>0.9: the rows one index lists: the fights index the sessions with foes (as 0.8.6: a reader of that version must never ask for a block of
        /// deeds alone), the deeds index (<paramref name="deeds"/>) the sessions with deed stretches, each written with its stretches in the count's place.</summary>
        public static List<BlockRow> Part(IEnumerable<BlockRow> rows, bool deeds) =>
            (rows ?? Enumerable.Empty<BlockRow>()).Where(r => r != null && (deeds ? r.Stretches > 0 : r.Foes > 0))
                .Select(r => deeds ? new BlockRow { Id = r.Id, FromUtc = r.FromUtc, ToUtc = r.ToUtc, Foes = r.Stretches, Stretches = r.Stretches } : r).ToList();

        /// <summary>0.9: the deeds index's rows as read (the count is the stretches): Stretches set, Foes 0. Null in, null out.</summary>
        public static Dictionary<string, List<BlockRow>> DeedRows(IDictionary<string, List<BlockRow>> of) =>
            of?.ToDictionary(kv => kv.Key, kv => kv.Value.Select(r => new BlockRow { Id = r.Id, FromUtc = r.FromUtc, ToUtc = r.ToUtc, Stretches = Math.Min(DeedBlockPart.MaxStretches, r.Foes) }).ToList(), StringComparer.Ordinal);

        /// <summary>Both indexes' rows per fellow, each session once: what the cache on this PC may keep (BlockCache.KeepOnly).</summary>
        public static Dictionary<string, List<BlockRow>> Joined(IDictionary<string, List<BlockRow>> a, IDictionary<string, List<BlockRow>> b)
        {
            var all = new Dictionary<string, List<BlockRow>>(StringComparer.Ordinal);
            foreach (var src in new[] { a, b })
                foreach (var kv in src ?? new Dictionary<string, List<BlockRow>>())
                {
                    if (!all.TryGetValue(kv.Key, out var l)) all[kv.Key] = l = new List<BlockRow>();
                    foreach (var r in kv.Value) if (!l.Any(x => x.Id == r.Id)) l.Add(r);
                }
            return all;
        }

        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        static DateTime At(double secs) => Epoch.AddSeconds(Json.IsFinite(secs) ? Math.Max(0, Math.Min(4102444800.0, Math.Round(secs))) : 0);
    }

    /// <summary>Client: which of your closed sessions go to the server as blocks, and how they are packed.</summary>
    public static class Uploads
    {
        /// <summary>Seconds between two blocks (over the server's 10 s gap, BlockGate.UpGap).</summary>
        public const double UpEvery = 12;
        /// <summary>The most parts of one block: BlockStore.MaxBlockPacked is exactly this many parts of Fragments.Size bytes.</summary>
        public const int MaxParts = 16;

        /// <summary>
        /// The sessions to upload next, newest first: <paramref name="earlier"/> (KeptFights.Earlier: never the running connection's) that ended within the
        /// last 30 days, are not in <paramref name="mine"/> (the server holds them) and not in <paramref name="tried"/> (sent or skipped this connection).
        /// </summary>
        public static List<KeptSession> Plan(IEnumerable<KeptSession> earlier, ISet<string> mine, ISet<string> tried, DateTime now) =>
            (earlier ?? Enumerable.Empty<KeptSession>())
                .Where(s => s != null && s.Mine != null && s.Fights.Count > 0 && FeedBlock.IdPattern.IsMatch(s.Id ?? "") && s.ToUtc >= now - BlockStore.MaxAge &&
                            (mine == null || !mine.Contains(s.Id)) && (tried == null || !tried.Contains(s.Id)))
                .OrderByDescending(s => s.ToUtc).ToList();

        /// <summary>The block as it travels (gzip of its JSON); null when the session holds nothing to send or it packs over BlockStore.MaxBlockPacked.</summary>
        public static byte[] Pack(KeptSession s, out string problem)
        {
            problem = null;
            var json = FeedBlock.Write(s);
            if (json == null) { problem = "nothing to send"; return null; }
            var packed = Transport.Pack(json);
            if (packed.Length > BlockStore.MaxBlockPacked) { problem = packed.Length + " bytes packed is over " + BlockStore.MaxBlockPacked; return null; }
            return packed;
        }

        /// <summary>0.9: one closed session as a block can carry it: its fights (KeptFights), its deed stretches (KeptDeeds), or both (the same session id).</summary>
        public sealed class Source
        {
            public string Id = "";
            public DateTime FromUtc, ToUtc;
            public KeptSession Fights;
            public DeedSession Deeds;
            public bool HasFights => Fights?.Mine != null && Fights.Fights.Count > 0;
            public bool HasDeeds => Deeds != null && Deeds.Stretches.Count > 0;
        }

        /// <summary>
        /// 0.9 (deed feeds): the sessions to upload next, newest first, from your kept fights and (<paramref name="deeds"/>, only where the server keeps deed
        /// feeds: null otherwise) your kept deed stretches, joined by session id: every closed session that holds either, ended within the last 30 days,
        /// is not held by the server (<paramref name="mine"/>) and was not tried this connection (<paramref name="tried"/>).
        /// </summary>
        public static List<Source> Plan(IEnumerable<KeptSession> earlier, IEnumerable<DeedSession> deeds, ISet<string> mine, ISet<string> tried, DateTime now)
        {
            var byId = new Dictionary<string, Source>(StringComparer.Ordinal);
            foreach (var s in earlier ?? Enumerable.Empty<KeptSession>())
            {
                if (s?.Id == null || byId.ContainsKey(s.Id)) continue;
                byId[s.Id] = new Source { Id = s.Id, FromUtc = s.FromUtc, ToUtc = s.ToUtc, Fights = s };
            }
            foreach (var d in deeds ?? Enumerable.Empty<DeedSession>())
            {
                if (d?.Id == null || d.Live) continue;
                if (!byId.TryGetValue(d.Id, out var src)) byId[d.Id] = src = new Source { Id = d.Id, FromUtc = d.FromUtc, ToUtc = d.ToUtc };
                if (src.Deeds != null) continue;
                src.Deeds = d;
                if (d.FromUtc < src.FromUtc) src.FromUtc = d.FromUtc;
                if (d.ToUtc > src.ToUtc) src.ToUtc = d.ToUtc;
            }
            return byId.Values.Where(s => (s.HasFights || s.HasDeeds) && FeedBlock.IdPattern.IsMatch(s.Id) && s.ToUtc >= now - BlockStore.MaxAge &&
                                          (mine == null || !mine.Contains(s.Id)) && (tried == null || !tried.Contains(s.Id)))
                              .OrderByDescending(s => s.ToUtc).ToList();
        }

        /// <summary>The steps a block's deed part shrinks by when the whole block would pack over the bounds: the newest stretches stay, and at the last step the
        /// block goes without them (its fights alone, as 0.8.6).</summary>
        public static readonly int[] DeedSteps = { DeedBlockPart.MaxStretches, 50, 25, 12, 0 };

        /// <summary>0.9: the block of <paramref name="s"/> as it travels: its fights and as many of its newest deed stretches as fit BlockStore.MaxBlockJson and
        /// MaxBlockPacked (DeedSteps); null when it holds nothing to send or does not fit even without its deeds.</summary>
        public static byte[] Pack(Source s, out string problem)
        {
            problem = null;
            if (s == null) { problem = "nothing to send"; return null; }
            foreach (var max in DeedSteps)
            {
                var json = FeedBlock.Write(s.Fights, max > 0 ? s.Deeds : null, Math.Max(1, max));
                if (json == null) { if (max > 0 && !s.HasFights) continue; problem = problem ?? "nothing to send"; return null; }
                if (json.Length > BlockStore.MaxBlockJson) { problem = json.Length + " bytes is over " + BlockStore.MaxBlockJson; continue; }
                var packed = Transport.Pack(json);
                if (packed.Length > BlockStore.MaxBlockPacked) { problem = packed.Length + " bytes packed is over " + BlockStore.MaxBlockPacked; continue; }
                problem = null;
                return packed;
            }
            return null;
        }
    }

    /// <summary>
    /// 0.9 (deed feeds, work/hearthwoven-0.9/TRACK-feeds.md): the deed stretches of one closed session as a block carries them, under "deedFeed" after the
    /// fights. Written by the owner's PC from its KeptDeeds (DeedSession.WriteTo), read by the server and by a friend's PC through Read, which keeps only
    /// what the feed draws: the deed families (the eight with a page feed, and 0.9 all-feed's taming, practice, brought in and sailing), DeedLog's kinds, the four moment kinds, names of a sane length, at most MaxStretches stretches (the
    /// newest), counts and moments as DeedSession reads them. No world name, no position, no other player. The server stores what Read gave, written again.
    /// </summary>
    public static class DeedBlockPart
    {
        public const string Key = "deedFeed";
        /// <summary>The stretches one block carries at most (the newest; a busy evening is about 13).</summary>
        public const int MaxStretches = 100;
        /// <summary>The longest "kind|token" key, token or station name kept.</summary>
        public const int MaxName = 120;
        static readonly HashSet<string> Families = new HashSet<string>(StringComparer.Ordinal)
        {
            DeedStretches.Crafting, DeedStretches.Cooking, DeedStretches.Building, DeedStretches.Groundwork, DeedStretches.Farming,
            DeedStretches.Fishing, DeedStretches.Woodcutting, DeedStretches.Mining,
            DeedStretches.Taming, DeedStretches.Practice, DeedStretches.BroughtIn, DeedStretches.Sailing,   // 0.9 all-feed: the mixed feed's other deeds
        };
        static readonly HashSet<string> Kinds = new HashSet<string>(DeedLog.AllFamilies, StringComparer.Ordinal);
        static readonly HashSet<string> MomentKinds = new HashSet<string>(StringComparer.Ordinal) { DeedMoment.Made, DeedMoment.Upgraded, DeedMoment.Feast, DeedMoment.First };

        /// <summary>A clean copy for a block: no world, never live, the newest <paramref name="max"/> stretches of the known families, every count and moment
        /// whose names pass; null when <paramref name="s"/> is null.</summary>
        public static DeedSession Clean(DeedSession s, int max = MaxStretches)
        {
            if (s == null) return null;
            var c = new DeedSession { World = "", Id = s.Id ?? "", FromUtc = s.FromUtc, ToUtc = s.ToUtc };
            foreach (var st in s.Stretches.Where(x => x != null && Families.Contains(x.Family ?? "") && x.ToUtc >= x.FromUtc).OrderByDescending(x => x.ToUtc).Take(Math.Max(0, Math.Min(MaxStretches, max))))
            {
                var n = new DeedStretch { Family = st.Family, FromUtc = st.FromUtc, ToUtc = st.ToUtc, MomentsLeft = Math.Max(0, Math.Min(100000, st.MomentsLeft)) };
                foreach (var kv in st.Counts)
                    if (kv.Key != null && kv.Key.Length <= MaxName && Kinds.Contains(DeedStretch.KindOf(kv.Key)) && DeedStretch.TokenOf(kv.Key).Length > 0 && kv.Value > 0 && Json.IsFinite(kv.Value))
                        n.Counts[kv.Key] = kv.Value;
                foreach (var m in st.Moments)
                    if (m != null && MomentKinds.Contains(m.Kind ?? "") && !string.IsNullOrEmpty(m.Token) && m.Token.Length <= MaxName && m.Count > 0 && !(m.Kind == DeedMoment.First && m.Known))   // a first from before-0.9 knowledge is never shared
                        n.Moments.Add(new DeedMoment { Kind = m.Kind, Token = m.Token, Utc = m.Utc, Count = m.Count, From = Math.Max(0, Math.Min(99, m.From)), To = Math.Max(0, Math.Min(99, m.To)),
                                                       Station = m.Station != null && m.Station.Length <= MaxName ? m.Station : null, StationLevel = Math.Max(0, Math.Min(99, m.StationLevel)) });
                c.Stretches.Add(n);
            }
            if (s.FromUtc > DateTime.MinValue && c.Stretches.Count > 0)
            {
                var first = c.Stretches.Min(x => x.FromUtc);
                if (first < c.FromUtc) c.FromUtc = first;   // every time is written from the session's start (DeedSession.WriteTo): none before it
            }
            return c;
        }

        /// <summary>The deed part of a block as this reader keeps it (Clean of DeedSession.ReadFrom, its id the block's); null when there is none or it holds no stretch.</summary>
        public static DeedSession Read(Dictionary<string, object> o, string id)
        {
            if (o == null) return null;
            var s = Clean(DeedSession.ReadFrom(o));
            if (s == null || s.Stretches.Count == 0) return null;
            s.Id = id ?? s.Id;
            return s;
        }
    }
    /// <summary>
    /// Client: the blocks of friends' sessions kept on this PC beside the fellow cache (FellowCache): <c>&lt;world&gt;/blocks/&lt;fellow&gt;/&lt;id&gt;.json</c>, the
    /// same two lines as the server's file. Only blocks the server's latest list names (KeepOnly); gone with the fellow cache when you stop sharing
    /// (FellowCache.ForgetAll deletes the root); never sent on. FellowCache.Load and KeepOnly read top-level files only, so this folder does not disturb them.
    /// </summary>
    public static class BlockCache
    {
        public static string FolderOf(string worldDir, string fellowKey) =>
            Path.Combine(worldDir, "blocks", Path.GetFileNameWithoutExtension(FellowCache.FileName(fellowKey)));

        static string FileOf(string worldDir, string fellowKey, string id) => Path.Combine(FolderOf(worldDir, fellowKey), id + ".json");

        /// <summary>Keeps one block (two lines: its head and the canonical block).</summary>
        public static void Save(string worldDir, string fellowKey, FeedBlock block)
        {
            if (string.IsNullOrEmpty(worldDir) || string.IsNullOrEmpty(fellowKey) || block == null || !FeedBlock.IdPattern.IsMatch(block.Id)) return;
            AtomicFile.Write(FileOf(worldDir, fellowKey, block.Id), FeedBlock.Head(block) + "\n" + FeedBlock.Canonical(block));
        }

        /// <summary>The canonical block text kept for <paramref name="id"/> (its .bak tried when the file is broken); null when there is none or it is not whole.</summary>
        public static string Load(string worldDir, string fellowKey, string id)
        {
            if (string.IsNullOrEmpty(worldDir) || string.IsNullOrEmpty(fellowKey) || !FeedBlock.IdPattern.IsMatch(id ?? "")) return null;
            try
            {
                var text = AtomicFile.ReadWithBackup(FileOf(worldDir, fellowKey, id), t => { var nl = t.IndexOf('\n'); return nl > 0 && ServerIntake.IsJsonObject(t.Substring(nl + 1)); }, out _);
                var cut = text?.IndexOf('\n') ?? -1;
                return cut > 0 ? text.Substring(cut + 1) : null;
            }
            catch (IOException) { return null; } catch (UnauthorizedAccessException) { return null; }
        }

        /// <summary>The list names these: every fellow folder not in <paramref name="rowsByFellow"/> goes, and in the others every file whose id is not in that fellow's rows.</summary>
        public static void KeepOnly(string worldDir, IDictionary<string, List<BlockRow>> rowsByFellow)
        {
            var root = string.IsNullOrEmpty(worldDir) ? null : Path.Combine(worldDir, "blocks");
            if (root == null || !Directory.Exists(root)) return;
            var keep = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var kv in rowsByFellow ?? new Dictionary<string, List<BlockRow>>())
                keep[Path.GetFileName(FolderOf(worldDir, kv.Key))] = new HashSet<string>(kv.Value.Select(r => r.Id), StringComparer.Ordinal);
            try
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    if (!keep.TryGetValue(Path.GetFileName(dir), out var ids)) { Directory.Delete(dir, true); continue; }
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        var name = Path.GetFileName(file);
                        var id = name.EndsWith(".json", StringComparison.Ordinal) ? name.Substring(0, name.Length - 5) : null;
                        if (id == null || !ids.Contains(id)) File.Delete(file);
                    }
                    if (Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
                }
                if (Directory.GetFileSystemEntries(root).Length == 0) Directory.Delete(root);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        /// <summary>Ages out blocks that ended more than 30 days ago (or whose head is unreadable) in one world's folder. Run with FellowCache.SweepAll.</summary>
        public static void Sweep(string worldDir, DateTime utcNow)
        {
            var root = string.IsNullOrEmpty(worldDir) ? null : Path.Combine(worldDir, "blocks");
            if (root == null || !Directory.Exists(root)) return;
            try
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        var row = file.EndsWith(".json", StringComparison.Ordinal) ? BlockStore.ReadHead(file) : null;
                        if (row == null || row.ToUtc < utcNow - BlockStore.MaxAge) File.Delete(file);
                    }
                    if (Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
                }
                if (Directory.GetFileSystemEntries(root).Length == 0) Directory.Delete(root);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Client: the friends' sessions the feed can show (GroupShare.Blocks). Rows come from the server's latest list (TakeRows); a block is asked for
    /// only when the feed wants it (Want), one at a time: from memory, else from this PC's cache (read on the cache queue), else from the server.
    /// States: NotAsked, Loading (a cache read or an ask is out, AskTimeout seconds), Ready, Failed (Retry), Gone (the server no longer holds it: its row is
    /// hidden until the next list). Version changes whenever something the feed shows changes, so a page built from it is built again.
    /// Memory: at most MaxMemory parsed blocks, the least recently used goes (it is still in the cache). Pure C#: the sending and the file reading are delegates.
    /// </summary>
    public sealed class FellowBlocks
    {
        public enum State { NotAsked, Loading, Ready, Failed, Gone }
        /// <summary>Every row a friend can have (BlockStore.MaxBlocks): a feed paging through all of them never finds an earlier one gone from memory.</summary>
        public const int MaxMemory = BlockStore.MaxBlocks;
        public const double AskTimeout = 30;
        /// <summary>The least time between two asks: the server's gap (BlockGate.AskGap) and half a second more. A closer ask is held and sent once the gap has passed.</summary>
        public const double AskSpacing = BlockGate.AskGap + 0.5;

        sealed class Entry { public State State; public FeedBlock Block; public double AskedAt, WantedAt; public long Used; }
        readonly Dictionary<string, List<BlockRow>> rows = new Dictionary<string, List<BlockRow>>(StringComparer.Ordinal);
        /// <summary>0.9: the friends' sessions with deed stretches (the list's deeds index, BlockRow.Stretches set), by fellow key, newest first.</summary>
        readonly Dictionary<string, List<BlockRow>> deedRows = new Dictionary<string, List<BlockRow>>(StringComparer.Ordinal);
        readonly Dictionary<string, string> fellowOf = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<string, string>> cached = new System.Collections.Concurrent.ConcurrentQueue<KeyValuePair<string, string>>();
        string inFlight, held;
        double lastAskAt = double.NegativeInfinity;
        long used;

        public int Version { get; private set; }
        /// <summary>Sends the ask for a block id to the server (null: nowhere to ask). A hosted world answers at once (Arrived or Gone).</summary>
        public Action<string> Ask;
        /// <summary>Queues a read of this PC's cached block (fellow key, id); the read ends in Cached(id, text). False: no cache here, so the ask goes out at once.</summary>
        public Func<string, string, bool> ReadCache;
        /// <summary>Something the feed shows changed (a block arrived, failed or went): the page is built again soon.</summary>
        public Action Changed;

        void Bump() { Version++; Changed?.Invoke(); }

        /// <summary>The friend's rows, newest first, without the ones the server said are gone and <paramref name="except"/> (the session their running copy is).</summary>
        public List<BlockRow> RowsOf(string fellowKey, string except = null) => RowsIn(rows, fellowKey, except);

        /// <summary>0.9: the friend's sessions with deed stretches, newest first, as RowsOf (Stretches set).</summary>
        public List<BlockRow> DeedRowsOf(string fellowKey, string except = null) => RowsIn(deedRows, fellowKey, except);

        List<BlockRow> RowsIn(Dictionary<string, List<BlockRow>> from, string fellowKey, string except) =>
            fellowKey != null && from.TryGetValue(fellowKey, out var l) ? l.Where(r => r.Id != except && !(entries.TryGetValue(r.Id, out var e) && e.State == State.Gone)).ToList() : new List<BlockRow>();

        /// <summary>0.9: the block's deed stretches (a session of that friend), or null while it is not Ready or holds none.</summary>
        public DeedSession DeedsOf(string id)
        {
            if (!entries.TryGetValue(id ?? "", out var e) || e.State != State.Ready || e.Block?.Deeds == null) return null;
            e.Used = ++used;
            return e.Block.Deeds;
        }

        /// <summary>The whole seconds block <paramref name="id"/> has been on its way, from when the feed wanted it (a held ask and the cache read included); -1 when it is
        /// not on its way. The feed's "Getting ..." line counts them (PanelModel.FetchWaited). No allocation: read every frame while that line shows.</summary>
        public int WaitedSeconds(string id, double now) =>
            id != null && entries.TryGetValue(id, out var e) && e.State == State.Loading ? (int)Math.Max(0, Math.Floor(now - e.WantedAt)) : -1;
        /// <summary>When the feed wanted block <paramref name="id"/> (the clock Want was given), or NaN when it is not on its way: the round trip in the log.</summary>
        public double WantedAt(string id) => id != null && entries.TryGetValue(id, out var e) && e.State == State.Loading ? e.WantedAt : double.NaN;
        public State StateOf(string id) => entries.TryGetValue(id ?? "", out var e) ? e.State : State.NotAsked;
        public string FellowOf(string id) => fellowOf.TryGetValue(id ?? "", out var f) ? f : null;
        /// <summary>True while the server's answer to this id is awaited (an answer for any other id is ignored).</summary>
        public bool Expecting(string id) => id != null && inFlight == id;

        /// <summary>The block as a kept session of that friend (World and the side's name set), or null while it is not Ready.</summary>
        public KeptSession SessionOf(string id, string world, string name)
        {
            if (!entries.TryGetValue(id ?? "", out var e) || e.State != State.Ready) return null;
            e.Used = ++used; e.Block.Session.World = world ?? ""; e.Block.Session.Sides[0].Name = name ?? "";
            return e.Block.Session;
        }

        /// <summary>The server's latest index (BlockIndex.Of, by fellow key). Blocks no row names leave memory; a row the server had called gone is shown again.</summary>
        public void TakeRows(IDictionary<string, List<BlockRow>> byFellow, IDictionary<string, List<BlockRow>> deedsByFellow = null)
        {
            string Sig() => string.Join("|", rows.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + ":" + string.Join(",", k.Value.Select(r => r.Id))))
                            + "#" + string.Join("|", deedRows.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + ":" + string.Join(",", k.Value.Select(r => r.Id))));
            var was = Sig();
            rows.Clear(); deedRows.Clear(); fellowOf.Clear();
            foreach (var kv in byFellow ?? new Dictionary<string, List<BlockRow>>())
            {
                rows[kv.Key] = kv.Value.OrderByDescending(r => r.ToUtc).ToList();
                foreach (var r in kv.Value) fellowOf[r.Id] = kv.Key;
            }
            foreach (var kv in deedsByFellow ?? new Dictionary<string, List<BlockRow>>())   // 0.9: the deeds index; a session may be in both (one block)
            {
                deedRows[kv.Key] = kv.Value.OrderByDescending(r => r.ToUtc).ToList();
                foreach (var r in kv.Value) if (!fellowOf.ContainsKey(r.Id)) fellowOf[r.Id] = kv.Key;
            }
            foreach (var id in entries.Keys.Where(i => !fellowOf.ContainsKey(i)).ToList()) entries.Remove(id);
            if (inFlight != null && !entries.ContainsKey(inFlight)) inFlight = null;
            var gone = entries.Where(kv => kv.Value.State == State.Gone).Select(kv => kv.Key).ToList();
            foreach (var id in gone) entries.Remove(id);
            var now = Sig();
            if (now != was || gone.Count > 0) Bump();
        }

        /// <summary>The state of block <paramref name="id"/> now, starting to fetch it when it is not here: Ready from memory; else a cache read (Loading); else the ask
        /// (Loading, AskTimeout seconds, then Failed). One fetch at a time: another id stays NotAsked until the first ends. Cheap to call on every build.</summary>
        public State Want(string id, double now)
        {
            if (!fellowOf.TryGetValue(id ?? "", out var fellow)) return State.NotAsked;
            Expire(now);
            if (!entries.TryGetValue(id, out var e)) entries[id] = e = new Entry { State = State.NotAsked };
            if (e.State == State.Ready) { e.Used = ++used; return e.State; }
            if (e.State != State.NotAsked || inFlight != null) return e.State;
            e.State = State.Loading; e.AskedAt = e.WantedAt = now; inFlight = id;
            Bump();   // the page says "Getting ..." at once
            if (ReadCache == null || !ReadCache(fellow, id)) Send(id, e, now);
            return e.State;
        }

        // an ask sooner than AskSpacing after the last one is held (the server drops a closer one silently), never dropped: Drain sends it
        void Send(string id, Entry e, double now)
        {
            if (Ask == null) { Fail(id); return; }
            if (now - lastAskAt < AskSpacing) { held = id; return; }
            e.AskedAt = now; lastAskAt = now;
            Ask(id);
        }

        /// <summary>A cache read ended (any thread): the block's text, or null when this PC has none.</summary>
        public void Cached(string id, string text) => cached.Enqueue(new KeyValuePair<string, string>(id, text));

        /// <summary>Main thread, every frame: cache reads that ended become a Ready block or the ask; an ask over AskTimeout seconds old fails.</summary>
        public void Drain(double now)
        {
            if (held != null && now - lastAskAt >= AskSpacing)
            {
                var id = held; held = null;
                if (entries.TryGetValue(id, out var h) && h.State == State.Loading && inFlight == id) Send(id, h, now);
            }
            while (cached.TryDequeue(out var c))
            {
                if (!entries.TryGetValue(c.Key, out var e) || e.State != State.Loading || inFlight != c.Key) continue;   // forgotten or answered meanwhile
                var block = c.Value == null ? null : FeedBlock.Read(c.Value);
                if (block != null && block.Id == c.Key) Ready(c.Key, e, block);
                else Send(c.Key, e, now);
            }
            Expire(now);
        }

        void Expire(double now)
        {
            if (inFlight != null && entries.TryGetValue(inFlight, out var e) && e.State == State.Loading && (now - e.AskedAt > AskTimeout || now < e.AskedAt)) Fail(inFlight);
        }

        /// <summary>The server's answer: the canonical block text of <paramref name="id"/>. Returns the block when it was the one asked for and reads, else null (it fails).</summary>
        public FeedBlock Arrived(string id, string text)
        {
            if (!Expecting(id) || !entries.TryGetValue(id, out var e)) return null;
            var block = FeedBlock.Read(text ?? "");
            if (block == null || block.Id != id) { Fail(id); return null; }
            Ready(id, e, block);
            return block;
        }

        void Ready(string id, Entry e, FeedBlock block)
        {
            e.State = State.Ready; e.Block = block; e.Used = ++used; inFlight = null;
            var ready = entries.Where(kv => kv.Value.State == State.Ready).ToList();
            if (ready.Count > MaxMemory) { var oldest = ready.OrderBy(kv => kv.Value.Used).First(); oldest.Value.State = State.NotAsked; oldest.Value.Block = null; }
            Bump();
        }

        /// <summary>The server does not hold it (any more): its row is hidden until the next list.</summary>
        public void Gone(string id)
        {
            if (!Expecting(id) || !entries.TryGetValue(id, out var e)) return;
            e.State = State.Gone; e.Block = null; inFlight = null; Bump();
        }

        /// <summary>The ask cannot be sent, or got no usable answer: a block that is Loading fails (Try again).</summary>
        public void Fail(string id)
        {
            if (entries.TryGetValue(id, out var e) && e.State == State.Loading) { e.State = State.Failed; if (inFlight == id) inFlight = null; Bump(); }
        }

        /// <summary>Try again after a failure: back to NotAsked, and fetched.</summary>
        public State Retry(string id, double now)
        {
            if (entries.TryGetValue(id ?? "", out var e) && e.State == State.Failed) { e.State = State.NotAsked; Bump(); }
            return Want(id, now);
        }

        /// <summary>Everything forgotten (a new connection, sharing off).</summary>
        public void Clear()
        {
            rows.Clear(); deedRows.Clear(); fellowOf.Clear(); entries.Clear(); inFlight = null; held = null;
            while (cached.TryDequeue(out _)) { }
            Version++;
        }
    }
}
