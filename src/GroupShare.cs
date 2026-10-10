using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;
using Hearthwoven.Panel;

namespace Hearthwoven
{
    /// <summary>
    /// See your fellow players' stats, and share yours (Joost, 2026-10-07): one reciprocal choice, ON by default.
    /// Server: keeps the latest snapshot of every player who shares (players/&lt;id&gt;.share.json, death positions removed)
    ///         and sends them ONLY to a player who asks and shares too. It never sends anything unasked, so a player
    ///         without the mod receives nothing and is never kicked.
    /// Client: asks when the panel opens (at most every 30 s) and keeps the replies in Fellows (FellowIds: keyed by platform id,
    /// shown by name; Group = key -> snapshot JSON).
    /// 0.6: the server keeps each copy packed and sends a requester only the copies that changed since it was last served
    /// (GroupServe; every copy again each 5 min). After the names, the list carries the server's book (ServerBookHooks: cargo
    /// loaded and unloaded, born near) for the requester and each sharer; an older client stops reading after the names.
    /// </summary>
    public static class GroupShare
    {
        public const string RequestRpc = "Hearthwoven_GroupRequest", ReplyRpc = "Hearthwoven_Group",
                            StateRpc = "Hearthwoven_ShareState", ListRpc = "Hearthwoven_GroupList",
                            // 0.7 live updates (SYNC-DESIGN.md item 4): new names, so an older peer never receives one (an unknown routed RPC is dropped unread)
                            LiveRpc = "Hearthwoven_Live", LiveRequestRpc = "Hearthwoven_LiveRequest", LiveDeltaRpc = "Hearthwoven_LiveDelta";
        /// <summary>After the keys in the list (0.7): this server keeps live updates. A 0.6.0 reader stops after the book, a 0.6.5 one at this unknown tag.</summary>
        public const string LiveTag = "hw-live-1";
        /// <summary>How often a 0.7 client sends its live update while playing and asks for the fellows' while the book is open (seconds).</summary>
        public const float LiveEvery = 10f;
        public const int MaxQueue = 600, MaxAgeDays = 14;
        /// <summary>Client: the fellows received, keyed by who they are (platform id, else profile id, else name; FellowIds).</summary>
        public static readonly FellowIds Fellows = new FellowIds();
        /// <summary>Client: fellow key -> shared copy (snapshot JSON).</summary>
        public static IDictionary<string, string> Group => Fellows.Copies;
        /// <summary>Client: the server's book for each fellow (name -> numbers) and for yourself; empty from a server before 0.6.</summary>
        public static readonly Dictionary<string, ServerBook.Shared> Books = new Dictionary<string, ServerBook.Shared>();
        /// <summary>Client: the same book keyed by fellow key ("byKey", a server with the identity fix): two fellows with one name each keep theirs.</summary>
        public static readonly Dictionary<string, ServerBook.Shared> BooksByKey = new Dictionary<string, ServerBook.Shared>();
        /// <summary>After the book in the list: the fellow key of each name, in the same order. An older reader stops before it.</summary>
        public const string KeysTag = "hw-keys-1";
        public static ServerBook.Shared OwnBook;
        /// <summary>The tag in front of the book in the list (after the names): a reader that knows it reads on, any other stops.</summary>
        public const string BooksTag = "hw-book-1";
        public static float LastReply = -1f;
        /// <summary>Client: when each fellow's copy arrived with new content (a resend of the same copy keeps the time): FellowMarks' "seen", Recent's "to".</summary>
        public static readonly Dictionary<string, DateTime> ReceivedUtc = new Dictionary<string, DateTime>();
        public static DateTime? ReceivedAt(string key) => key != null && ReceivedUtc.TryGetValue(key, out var t) ? t : (DateTime?)null;
        internal static Func<bool> Sharing = () => true;
        /// <summary>Client: a group list came from this server this connection (it runs Hearthwoven and answers you).</summary>
        public static bool ListSeen;
        /// <summary>Client: who just joined (0.7, SYNC-DESIGN.md item 1), from the game's own player list.</summary>
        public static readonly JoinWatch Joins = new JoinWatch();
        /// <summary>Client: this server announced live updates in its list (LiveTag) this connection; never true on a server before 0.7.</summary>
        public static bool ServerLive;
        /// <summary>Server: every sharer's latest live update, in memory (LiveSync.cs).</summary>
        internal static readonly LiveStore Live = new LiveStore();
        /// <summary>Client: the fellows' full copies and latest live updates; Group shows base + update.</summary>
        internal static readonly LiveFellows LiveIn = new LiveFellows();
        /// <summary>Client: when each fellow's copies reached this PC this connection (Deeds > Recent counts a fellow only inside your session, B33).</summary>
        public static readonly FellowTrails Trails = new FellowTrails();
        /// <summary>Client: the fellows shown from this PC's cache (FellowCache, B23) until the server's fresh copy replaces them: key -> when it was received.</summary>
        public static readonly Dictionary<string, DateTime> Cached = new Dictionary<string, DateTime>();
        public static bool IsCached(string key) => key != null && Cached.ContainsKey(key);
        /// <summary>Client: this world's cache folder (FellowCache), set on spawn; null = none (a hosted world, singleplayer, not sharing).</summary>
        static string cacheDir;
        static readonly object cacheGate = new object();
        static int cacheGeneration;   // bumped when the cache is forgotten: a write queued before that never lands
        static readonly ServerIntake.OnceEach liveWarned = new ServerIntake.OnceEach();
        static float nextLiveRequest;

        static float nextRequest;
        static readonly Fragments.Assembler assembler = new Fragments.Assembler();
        class Pending { public long Target; public ZPackage Pkg; public string Rpc; public Action Sent; }
        static readonly Queue<Pending> outbox = new Queue<Pending>();
        static readonly Dictionary<long, float> lastServed = new Dictionary<long, float>();
        static readonly GroupServe serve = new GroupServe();
        static float nextFragment;
        static readonly Regex DeathPos = new Regex(",\"x\":[-0-9.eE]+,\"z\":[-0-9.eE]+", RegexOptions.Compiled);
        static readonly Regex Worlds = new Regex(",\"secondsPerWorld\":\\{[^{}]*\\}", RegexOptions.Compiled);

        internal static void Register(ZRoutedRpc r)
        {
            r.Register<ZPackage>(RequestRpc, OnRequest);
            r.Register<ZPackage>(ReplyRpc, OnReply);
            r.Register<bool>(StateRpc, OnState);
            r.Register<ZPackage>(ListRpc, OnList);
            r.Register<ZPackage>(LiveRpc, OnLive);
            r.Register<ZPackage>(LiveRequestRpc, OnLiveRequest);
            r.Register<ZPackage>(LiveDeltaRpc, OnLiveDelta);
        }

        static string Dir => Path.Combine(Paths.BepInExRootPath, "Hearthwoven", "players");

        /// <summary>The copy others may see: same snapshot without the places where you died and the worlds you played.</summary>
        public static string SharedCopy(string snapshotJson) => Worlds.Replace(DeathPos.Replace(snapshotJson, ""), "");

        public static bool Shares(string snapshotJson) => snapshotJson.Contains("\"share\":true");

        /// <summary>Server, on every complete snapshot: keep or remove the shared copy.</summary>
        internal static void Store(string playerKey, string json)
        {
            if (string.IsNullOrEmpty(playerKey) || playerKey == "0") return;   // unknown player: never shared
            var file = Path.Combine(Dir, playerKey + ".share.json");
            if (Shares(json)) { AtomicFile.Write(file, SharedCopy(json)); Live.Full(playerKey, LiveFellows.CopyIdOf(json), FellowIds.KeyOf(json)); }   // written whole: a crash never leaves a truncated copy; the new base of live updates
            else Unshare(playerKey);
        }

        /// <summary>A shared copy as stored (Store, AtomicFile): the file, else its .bak (the copy before) when the file is broken; null when
        /// neither is one JSON object (0.8, RESILIENCE item 3: a truncated copy is never served).</summary>
        internal static string ReadShare(string file)
        {
            foreach (var f in new[] { file, file + AtomicFile.BackupSuffix })
            {
                try { if (File.Exists(f)) { var t = File.ReadAllText(f, Encoding.UTF8); if (ServerIntake.IsJsonObject(t)) return t; } }
                catch { }
            }
            return null;
        }

        static void Unshare(string playerKey)
        {
            if (string.IsNullOrEmpty(playerKey) || playerKey == "0") return;
            Live.Unshare(playerKey);   // no live update of theirs is kept or served
            var file = Path.Combine(Dir, playerKey + ".share.json");
            if (File.Exists(file)) File.Delete(file);
            if (File.Exists(file + AtomicFile.BackupSuffix)) File.Delete(file + AtomicFile.BackupSuffix);   // its backup copy goes too (S3)
        }

        /// <summary>Client: tell the server whether you share, on every spawn and when the setting changes. Separate from
        /// the stats themselves, so switching sharing off works even with SendStats off (INTEGRITY S3).</summary>
        public static void SendShareState()
        {
            try
            {
                if (Panel.SampleMode.Quiet("share state")) return;   // Dev.SampleData: no server messages
                if (ZNet.instance == null || ZNet.instance.IsServer() || ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.InvokeRoutedRPC(StateRpc, Sharing());
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] share state: " + e.Message); }
        }

        static void OnState(long sender, bool sharing)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || sharing) return;   // sharing on: the next snapshot brings the copy
            serve.Forget(sender); Live.Forget(sender);   // its copies were cleared: the next answer sends them all again
            var peer = ZNet.instance.GetPeer(sender);
            // no id yet (character ZDO not synced, no snapshot this session): the next snapshot says share:false and unshares
            if (peer != null && PeerIdentity.Id(peer).HasValue) Unshare(PeerIdentity.Key(peer));
        }

        /// <summary>Client: the server's current list of sharers; anyone else is forgotten (they stopped sharing).</summary>
        static void OnList(long sender, ZPackage pkg)
        {
            try
            {
                if (ZNet.instance == null || ZNet.instance.IsServer()) return;
                var keep = ReadList(pkg, out var books, out var keys, out var live);
                ListSeen = true; ServerLive = live;
                Fellows.KeepOnly(keys, keep);
                LiveIn.KeepOnly((ICollection<string>)keys ?? Group.Keys);   // the list's own keys: a fellow just listed keeps an update that waits for its copy   // by key from a server with the identity fix, else by name (an older server)
                Trails.KeepOnly(Group.Keys); FellowCopies.KeepOnly(Group.Keys);
                foreach (var gone in Cached.Keys.Where(k => !Group.ContainsKey(k)).ToList()) { Cached.Remove(gone); ReceivedUtc.Remove(gone); }
                var keepFiles = new List<string>(Group.Keys);   // a fellow who stopped sharing leaves this PC's cache too
                QueueCache(dir => FellowCache.KeepOnly(dir, keepFiles));
                ReadBooks(books);
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] group list: " + e.Message); }
        }

        /// <summary>The list as the server writes it: the sharers' names, then (0.6) the tag and the packed book. A reader before 0.6
        /// reads the names and stops; this one reads on when there is more. <paramref name="books"/> = the book's JSON, null without one.</summary>
        public static HashSet<string> ReadList(ZPackage pkg, out string books) => ReadList(pkg, out books, out _);

        /// <summary>The list with its tagged parts after the names: the book (BooksTag) and the fellow keys (KeysTag, null from a
        /// server without them). An unknown tag ends the reading.</summary>
        public static HashSet<string> ReadList(ZPackage pkg, out string books, out List<string> keys) => ReadList(pkg, out books, out keys, out _);

        /// <summary>The list with its tagged parts; <paramref name="live"/>: the server keeps live updates (LiveTag, 0.7).</summary>
        public static HashSet<string> ReadList(ZPackage pkg, out string books, out List<string> keys, out bool live)
        {
            int n = pkg.ReadInt();
            var names = new HashSet<string>();
            for (int i = 0; i < n; i++) names.Add(pkg.ReadString());
            books = null; keys = null; live = false;
            while (pkg.GetPos() < pkg.Size())
            {
                var tag = pkg.ReadString();
                if (tag == BooksTag) books = Transport.Unpack(pkg.ReadByteArray());
                else if (tag == KeysTag) { int k = pkg.ReadInt(); keys = new List<string>(); for (int i = 0; i < k; i++) keys.Add(pkg.ReadString()); }
                else if (tag == LiveTag) live = pkg.ReadInt() >= LiveDelta.Version;
                else break;
            }
            return names;
        }

        /// <summary>Server: the list for one answer (ReadList reads it). <paramref name="keys"/>: each name's fellow key, after the book;
        /// <paramref name="live"/>: the live tag last (0.7), so every older reader has stopped before it.</summary>
        public static ZPackage WriteList(IList<string> names, string booksJson, IList<string> keys = null, bool live = false)
        {
            var list = new ZPackage(); list.Write(names.Count); foreach (var nm in names) list.Write(nm);
            if (booksJson != null) { list.Write(BooksTag); list.Write(Transport.Pack(booksJson)); }
            if (keys != null) { list.Write(KeysTag); list.Write(keys.Count); foreach (var k in keys) list.Write(k ?? ""); }
            if (live) { list.Write(LiveTag); list.Write(LiveDelta.Version); }
            return list;
        }

        /// <summary>Client: the server's book from a list's tail (null: a server before 0.6, so none).</summary>
        public static void ReadBooks(string json)
        {
            Books.Clear(); BooksByKey.Clear(); OwnBook = null;
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) return;
            OwnBook = ServerBook.Shared.Read(MiniJson.Obj(root, "self"));
            foreach (var kv in MiniJson.Obj(root, "players") ?? new Dictionary<string, object>())
            {
                var b = ServerBook.Shared.Read(kv.Value as Dictionary<string, object>);
                if (b != null) Books[kv.Key] = b;
            }
            foreach (var kv in MiniJson.Obj(root, "byKey") ?? new Dictionary<string, object>())
            {
                var b = ServerBook.Shared.Read(kv.Value as Dictionary<string, object>);
                if (b != null) BooksByKey[kv.Key] = b;
            }
        }

        /// <summary>A fellow's part of the server's book, or null.</summary>
        public static ServerBook.Shared BookOf(string name) => name != null && Books.TryGetValue(name, out var b) ? b : null;

        /// <summary>A fellow's part of the server's book by key; by name only from a server that sends no keys.</summary>
        public static ServerBook.Shared BookOf(string key, string name) =>
            key != null && BooksByKey.TryGetValue(key, out var b) ? b : BooksByKey.Count == 0 ? BookOf(name) : null;

        // ---------- client ----------

        /// <summary>
        /// The quiet group request while playing (0.7, Deeds > Recent and Since you were away), on spawn and once per send interval, the book
        /// open or not: only when you share (the same rule as the open book: a player who does not share never pulls others' copies) and only
        /// in the world (spawned, a local player). Request itself keeps its 30 s limit, the server and sample-mode checks.
        /// </summary>
        public static bool AskInBackground(bool sharing, bool spawned, bool localPlayer) => sharing && spawned && localPlayer;

        /// <summary>Ask the server for fellow players' stats. Rate-limited; does nothing if you do not share.</summary>
        public static void Request()
        {
            try
            {
                if (Panel.SampleMode.Quiet("group request")) return;   // Dev.SampleData: the sample brings its own fellows
                if (!Sharing() || ZNet.instance == null || ZRoutedRpc.instance == null) return;
                if (ZNet.instance.IsServer() && !IsHost()) return;   // a dedicated server or singleplayer: nobody to ask
                if (Time.realtimeSinceStartup < nextRequest) return;
                nextRequest = Time.realtimeSinceStartup + 30f;
                if (IsHost()) RequestLocal();   // you host the world: the group is on this PC
                else ZRoutedRpc.instance.InvokeRoutedRPC(RequestRpc, new ZPackage());
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] group request: " + e.Message); }
        }

        static void OnReply(long sender, ZPackage pkg)
        {
            try
            {
                if (ZNet.instance == null || ZNet.instance.IsServer() || !Sharing()) return;
                var msgId = pkg.ReadString(); int index = pkg.ReadInt(), count = pkg.ReadInt();
                var whole = assembler.Add(sender, msgId, index, count, pkg.ReadByteArray());
                if (whole == null) return;
                var json = Transport.Unpack(whole);
                if (DevCheck.On) DevCheck.SyncIn(false, whole.Length);
                var key = TakeFull(json, DateTime.UtcNow);
                if (key != null)
                {
                    LastReply = Time.realtimeSinceStartup;
                    var shown = Group[key]; var at = ReceivedUtc[key];
                    QueueCache(dir => FellowCache.Save(dir, key, shown, at));   // B23: kept on this PC for the next login (full copies only, never per live update)
                }
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] group reply: " + e.Message); }
        }

        /// <summary>
        /// Client: a full copy from the server (or the host's own store), received at <paramref name="utc"/>: shown with its live update when one
        /// for it is here, replacing a copy from this PC's cache; the trail notes it. Returns the fellow key, or null for a copy without one.
        /// </summary>
        public static string TakeFull(string json, DateTime utc)
        {
            var fellow = FellowIds.KeyOf(json) ?? "";
            Group.TryGetValue(fellow, out var was);
            var shown = LiveIn.OnFull(fellow, json);   // 0.7: with its live update applied when one for this copy is here
            var key = Fellows.Receive(shown);
            if (key == null) return null;
            var fromCache = Cached.Remove(key);
            if (fromCache || !string.Equals(was, shown, StringComparison.Ordinal) || !ReceivedUtc.ContainsKey(key)) { ReceivedUtc[key] = utc; Trails.Add(key, shown, utc); }   // a resend of the same copy is no news
            return key;
        }

        /// <summary>
        /// Client (B23): the cached copies of this world, shown at once until the server's fresh ones arrive (TakeFull replaces them). Only while
        /// you share, and only for fellows who have no copy here yet. Returns how many were shown.
        /// </summary>
        public static int ShowCached(IEnumerable<FellowCache.Entry> entries)
        {
            if (!Sharing() || entries == null) return 0;
            int n = 0;
            foreach (var e in entries)
            {
                if (e == null || Group.ContainsKey(e.Key)) continue;
                var key = Fellows.Receive(e.Json);
                if (key == null) continue;
                Cached[key] = e.ReceivedUtc; ReceivedUtc[key] = e.ReceivedUtc; n++;
            }
            return n;
        }

        /// <summary>Client, on spawn (B23): this world's cache folder, and its copies shown when no list came yet this connection.</summary>
        internal static void LoadCached(string hearthwovenDir)
        {
            try
            {
                if (Panel.SampleMode.Quiet("fellow cache") || !Sharing() || ZNet.instance == null || ZNet.instance.IsServer() || ZNet.World == null) return;
                var dir = Path.Combine(FellowCache.Root(hearthwovenDir), FellowCache.ScopeKey(ZNet.instance.GetWorldName(), ZNet.instance.GetWorldUID()));
                var fresh = cacheDir != dir;
                cacheDir = dir;
                if (fresh && !ListSeen) ShowCached(FellowCache.Load(dir, DateTime.UtcNow));   // a respawn keeps what is shown; once the server answered, its list rules
                if (fresh) { var root = FellowCache.Root(hearthwovenDir); QueueCache(_ => FellowCache.SweepAll(root, DateTime.UtcNow)); }   // other worlds' old copies age out too (REVIEW-07 #5)
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] fellow cache: " + e.Message); }
        }

        /// <summary>Client: you stopped sharing: every cached fellow of every world is deleted, and nothing queued before writes again.</summary>
        internal static void ForgetCache(string hearthwovenDir)
        {
            try { lock (cacheGate) { cacheGeneration++; FellowCache.ForgetAll(FellowCache.Root(hearthwovenDir)); } }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] fellow cache not deleted: " + e.Message); }
        }

        // the cache jobs off the main thread, in the order they were queued (REVIEW-07 #13: a Save from an earlier copy never lands after the
        // KeepOnly of a later list): one queue, drained by one thread-pool worker at a time; never after ForgetCache
        static readonly System.Collections.Generic.Queue<(Action<string> job, string dir, int gen)> cacheJobs = new System.Collections.Generic.Queue<(Action<string>, string, int)>();
        static bool cacheDraining;
        static void QueueCache(Action<string> job)
        {
            var dir = cacheDir; if (dir == null || !Sharing()) return;
            lock (cacheJobs)
            {
                int gen; lock (cacheGate) gen = cacheGeneration;
                cacheJobs.Enqueue((job, dir, gen));
                if (cacheDraining) return;
                cacheDraining = true;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                while (true)
                {
                    (Action<string> job, string dir, int gen) next;
                    lock (cacheJobs) { if (cacheJobs.Count == 0) { cacheDraining = false; return; } next = cacheJobs.Dequeue(); }
                    try { lock (cacheGate) { if (next.gen == cacheGeneration) next.job(next.dir); } }
                    catch (Exception e) { Debug.LogWarning("[Hearthwoven] fellow cache not written: " + e.Message); }
                }
            });
        }

        /// <summary>Client: forget what was received (on logout, or when sharing is switched off).</summary>
        public static void Clear() { liveGeneration++; FellowCopies.Clear(); Cached.Clear(); Trails.Clear(); cacheDir = null; ReceivedUtc.Clear(); Fellows.Clear(); Books.Clear(); BooksByKey.Clear(); OwnBook = null; nextRequest = 0f; ListSeen = false; Joins.Clear(); ServerLive = false; LiveIn.Clear(); nextLiveRequest = 0f; }

        /// <summary>Client, every couple of seconds while playing: the game's player list into Joins (the other spawned players by name;
        /// whether it shows you spawned yet).</summary>
        internal static void WatchJoins()
        {
            var net = ZNet.instance;
            if (net == null || ZNet.IsSinglePlayer) return;
            var mine = net.LocalPlayerCharacterID;
            bool hasYou = false; var names = new List<string>();
            foreach (var p in net.GetPlayerList())
            {
                if (p.m_characterID.IsNone()) continue;   // still loading: not in the world yet
                if (p.m_characterID == mine) { hasYou = true; continue; }
                names.Add(p.m_name);
            }
            Joins.Update(names, hasYou && !mine.IsNone(), DateTime.UtcNow);
        }

        /// <summary>The fellows who just joined and have no copy here yet (dimmed chips, no numbers): only while you share and this server
        /// has answered you (it runs Hearthwoven), so the chip's "stats follow" can come true.</summary>
        public static List<string> Joining(string self) =>
            Sharing() && ListSeen ? Joins.Joining(Fellows.Copies.Keys.Select(Fellows.NameOf), self, DateTime.UtcNow) : new List<string>();

        // ---------- server ----------

        static void OnRequest(long sender, ZPackage pkg)
        {
            try
            {
                if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
                var now = Time.realtimeSinceStartup;
                if (lastServed.TryGetValue(sender, out var t) && now - t < 20f) return;
                lastServed[sender] = now;
                var peer = ZNet.instance.GetPeer(sender);
                if (peer == null) return;
                if (!PeerIdentity.Id(peer).HasValue) return;   // not known yet: no own share file can match
                var own = Path.Combine(Dir, PeerIdentity.Key(peer) + ".share.json");
                if (!File.Exists(own)) return;   // you see others only while you share yourself
                foreach (var q in outbox) if (q.Target == sender) return;   // still sending your previous answer
                if (outbox.Count > MaxQueue) return;                         // busy: ask again later (client retries every 30 s)
                var sources = new List<GroupServe.Source>();
                foreach (var file in Directory.GetFiles(Dir, "*.share.json"))
                {
                    if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(own), StringComparison.Ordinal)) continue;
                    var info = new FileInfo(file);
                    if ((DateTime.UtcNow - info.LastWriteTimeUtc).TotalDays > MaxAgeDays) continue;   // not seen for two weeks: no longer shown
                    sources.Add(new GroupServe.Source { Path = file, Key = Path.GetFileName(file).Replace(".share.json", ""), Stamp = info.LastWriteTimeUtc.Ticks + ":" + info.Length });
                }
                serve.Prune(id => ZNet.instance.GetPeer(id) != null);
                foreach (var gone in new List<long>(lastServed.Keys)) if (ZNet.instance.GetPeer(gone) == null) lastServed.Remove(gone);   // bounded by the connected peers
                // each copy read and packed once per version; this requester gets only what changed since it was last served (GroupServe)
                var answer = serve.Plan(sender, sources, now, ReadShare,
                                        text => Fragments.Split(Transport.Pack(text)), text => Transport.Field(text, "name"),
                                        text => Transport.Field(text, "playerId") + "-" + DateTime.UtcNow.Ticks, FellowIds.KeyOf);
                // 0.6: the server's book for you and each sharer, after the names (an older client stops reading there), then each
                // name's fellow key (two players with one name both show; an older client stops before it)
                var list = WriteList(answer.Names, ServerBookHooks.Shared(PeerIdentity.Key(peer), answer.Keys, answer.Names, answer.Fellows), answer.Fellows, live: true);
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, ListRpc, list);
                foreach (var copy in answer.Send)
                    for (int i = 0; i < copy.Parts.Count; i++)
                    {
                        var p = new ZPackage(); p.Write(copy.MsgId); p.Write(i); p.Write(copy.Parts.Count); p.Write(copy.Parts[i]);
                        outbox.Enqueue(new Pending { Target = sender, Pkg = p });
                    }
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] group serve: " + e.Message); }
        }

        /// <summary>Server: one fragment every 100 ms, never a burst.</summary>
        internal static void Tick(float now)
        {
            if (!liveDone.IsEmpty) DrainLive();   // 0.8: the live updates applied on the worker (client)
            if (outbox.Count == 0 || now < nextFragment || ZRoutedRpc.instance == null) return;
            var next = outbox.Dequeue();
            if (ZNet.instance?.GetPeer(next.Target) != null) { ZRoutedRpc.instance.InvokeRoutedRPC(next.Target, next.Rpc ?? ReplyRpc, next.Pkg); next.Sent?.Invoke(); }
            nextFragment = now + 0.1f;
        }
        // ---------- a world hosted by a player (no dedicated server) ----------

        /// <summary>This PC is the server and plays too: a world hosted from the game ("Start server" on), not singleplayer.</summary>
        public static bool IsHost() => ZNet.instance != null && ZNet.instance.IsServer() && !ZNet.instance.IsDedicated() && !ZNet.IsSinglePlayer;

        /// <summary>Host: your own snapshot, kept the way the server keeps a player's (shared copy under your profile id, with your
        /// platform id), so the players who join see you, and you count as sharing for your own group.</summary>
        internal static void StoreOwn(long profileId, string json)
        {
            try
            {
                if (profileId == 0) return;
                Directory.CreateDirectory(Dir);
                Store(PeerIdentity.KeyOf(profileId, "", 0), FellowIds.WithPlatform(json, LocalPlatform()));
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] host share: " + e.Message); }
        }

        // the host's own platform id as the game gives it ("Steam_..."), read through the game's own serializer (no platform assembly needed)
        static string LocalPlatform()
        {
            try
            {
                var p = new ZPackage(); var u = UserInfo.GetLocalUser(); u.Serialize(ref p);
                p.SetPos(0); p.ReadString(); return p.ReadString();
            }
            catch { return ""; }
        }

        /// <summary>Host: the shared copies in <paramref name="dir"/> except your own, as (file key, text); null when you do not share
        /// (no own copy: you see others only while you share, as on a dedicated server). Copies older than MaxAgeDays are left out.</summary>
        public static List<KeyValuePair<string, string>> HostSources(string dir, string ownKey, DateTime utcNow)
        {
            var own = Path.Combine(dir, ownKey + ".share.json");
            if (!Directory.Exists(dir) || !File.Exists(own)) return null;
            var list = new List<KeyValuePair<string, string>>();
            foreach (var file in Directory.GetFiles(dir, "*.share.json"))
            {
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(own), StringComparison.Ordinal)) continue;
                if ((utcNow - File.GetLastWriteTimeUtc(file)).TotalDays > MaxAgeDays) continue;
                var text = ReadShare(file); if (text == null) continue;
                list.Add(new KeyValuePair<string, string>(Path.GetFileName(file).Replace(".share.json", ""), text));
            }
            return list;
        }

        /// <summary>Host: the answer a client would get, applied here: every copy received, the rest forgotten, the book read.
        /// <paramref name="books"/> (file keys, names, fellow keys) gives the book's JSON. Null sources: you do not share, so nobody.</summary>
        public static void HostAnswer(IList<KeyValuePair<string, string>> sources, Func<IList<string>, IList<string>, IList<string>, string> books)
        {
            var fileKeys = new List<string>(); var names = new List<string>(); var keys = new List<string>();
            foreach (var s in sources ?? new List<KeyValuePair<string, string>>())
            {
                var key = TakeFull(s.Value, DateTime.UtcNow);   // 0.7: with the live update for this copy, when one is here
                if (key == null) continue;
                fileKeys.Add(s.Key); names.Add(Fellows.NameOf(key)); keys.Add(key);
            }
            Fellows.KeepOnly(keys, names); Trails.KeepOnly(Group.Keys); FellowCopies.KeepOnly(Group.Keys);
            ReadBooks(books?.Invoke(fileKeys, names, keys));
        }

        static void RequestLocal()
        {
            var profile = Game.instance?.GetPlayerProfile();
            if (profile == null) return;
            var ownKey = PeerIdentity.KeyOf(profile.GetPlayerID(), "", 0);
            var sources = HostSources(Dir, ownKey, DateTime.UtcNow);
            HostAnswer(sources, (fileKeys, names, keys) => ServerBookHooks.Shared(ownKey, fileKeys, names, keys));
            LastReply = Time.realtimeSinceStartup; ListSeen = sources != null; ServerLive = true;   // you host: this PC keeps the live updates
        }

        // ---------- live updates (0.7, SYNC-DESIGN.md item 4) ----------

        /// <summary>Client, every LiveEvery seconds while the book is open: the fellows' latest live updates. Only while you share and this
        /// server announced them; a hosted world reads its own store.</summary>
        public static void LiveRequest()
        {
            try
            {
                if (Panel.SampleMode.Quiet("live request")) return;
                if (!Sharing() || !ServerLive || ZNet.instance == null || ZRoutedRpc.instance == null) return;
                if (ZNet.instance.IsServer() && !IsHost()) return;
                if (Time.realtimeSinceStartup < nextLiveRequest) return;
                nextLiveRequest = Time.realtimeSinceStartup + LiveEvery;
                if (IsHost()) LiveLocal();
                else ZRoutedRpc.instance.InvokeRoutedRPC(LiveRequestRpc, new ZPackage());
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] live request: " + e.Message); }
        }

        /// <summary>Client: one fellow's live update from the server. Shown at once when its full copy is here (base + update), else it waits for it.</summary>
        static void OnLiveDelta(long sender, ZPackage pkg)
        {
            try
            {
                if (ZNet.instance == null || ZNet.instance.IsServer() || !Sharing()) return;
                var fellow = pkg.ReadString(); var baseId = pkg.ReadString(); var seq = pkg.ReadLong(); var data = pkg.ReadByteArray();
                if (DevCheck.On) DevCheck.SyncIn(true, data?.Length ?? 0);
                var text = ServerIntake.Gunzip(data, LiveDelta.MaxJson, out _);
                if (text != null) ShowLive(fellow, baseId, seq, text);
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] live update: " + e.Message); }
        }

        static void ShowLive(string fellow, string baseId, long seq, string text) => TakeLive(fellow, baseId, seq, text, DateTime.UtcNow);   // 0.8: applied and read on a worker

        // ---------- 0.8 (REVIEW-07 finding 8): a live update applied and read off the main thread ----------
        // Before, each fellow's update cost about 2 MB of garbage and a few milliseconds on the main thread (the 26 KB copy parsed again to apply
        // it, read again for the trail, and again by the panel). Now the copy is parsed once per full copy (LiveFellows.TreeOf), the update is
        // applied onto it (copy on write) and the result read once (FellowCopies), on one worker thread in arrival order; the main thread only
        // takes the finished copies (DrainLive, every frame from Tick) and drops one a newer update or a new full copy overtook.

        sealed class LiveJob
        {
            public string Fellow, BaseId, Text, Shown, Error; public long Seq; public object Tree; public DateTime Utc; public int Generation;
            public PanelInput Copy; public Dictionary<string, float> Flat; public bool SinceInstall;
            public void Run()
            {
                Shown = LiveFellows.SameFellow(Fellow, LiveDelta.Apply(Tree, Text));
                if (Shown == null) return;
                Copy = PanelInput.FromSnapshot(Shown);
                if (Copy != null) { Flat = FellowTrail.Flat(Copy); SinceInstall = Copy.SharedSinceInstall; }
            }
        }
        static readonly System.Collections.Concurrent.ConcurrentQueue<LiveJob> liveTodo = new System.Collections.Concurrent.ConcurrentQueue<LiveJob>(), liveDone = new System.Collections.Concurrent.ConcurrentQueue<LiveJob>();
        static int liveWorking, liveGeneration;
        static bool liveWarnedJob, liveWarnedShow;

        /// <summary>Client: one fellow's live update (seq <paramref name="seq"/> on full copy <paramref name="baseId"/>) that reached this PC at
        /// <paramref name="utc"/>: applied and read on a worker thread, shown by DrainLive. Nothing happens when it is older than the one shown
        /// or its full copy is not here yet (it waits for it, LiveFellows).</summary>
        public static void TakeLive(string fellow, string baseId, long seq, string text, DateTime utc)
        {
            if (!LiveIn.Accept(fellow, baseId, seq, text, out var tree)) return;
            liveTodo.Enqueue(new LiveJob { Fellow = fellow, BaseId = baseId, Seq = seq, Text = text, Tree = tree, Utc = utc, Generation = liveGeneration });
            if (System.Threading.Interlocked.CompareExchange(ref liveWorking, 1, 0) == 0) System.Threading.ThreadPool.QueueUserWorkItem(_ => LiveWork());
        }

        // one worker at a time, the jobs in the order they came
        static void LiveWork()
        {
            while (true)
            {
                while (liveTodo.TryDequeue(out var job))
                {
                    try { job.Run(); } catch (Exception e) { job.Error = e.Message; }
                    liveDone.Enqueue(job);
                }
                System.Threading.Interlocked.Exchange(ref liveWorking, 0);
                if (liveTodo.IsEmpty || System.Threading.Interlocked.CompareExchange(ref liveWorking, 1, 0) != 0) return;   // a job came after the last look: keep going
            }
        }

        /// <summary>Main thread: the live updates the worker finished, shown in the order they came (what ShowLive did at once before 0.8).
        /// Returns how many finished jobs were taken.</summary>
        public static int DrainLive()
        {
            int n = 0;
            while (liveDone.TryDequeue(out var job))
            {
                n++;
                if (job.Generation != liveGeneration) continue;   // from before a Clear (another connection)
                if (job.Error != null) { if (!liveWarnedJob) Debug.LogWarning("[Hearthwoven] live update not applied: " + job.Error); liveWarnedJob = true; continue; }
                // REVIEW-08 #3: each job in its own try, as OnLiveDelta had it before the worker: a throw here loses that update only, never
                // the rest of Plugin.Update (Deeds.Tick, the battle record, the outbox) for the frame
                try
                {
                    if (job.Shown == null || !LiveIn.Current(job.Fellow, job.BaseId, job.Seq)) continue;   // a newer update or a new full copy came meanwhile
                    if (!Group.TryGetValue(job.Fellow, out var was) || string.Equals(was, job.Shown, StringComparison.Ordinal)) continue;
                    var key = Fellows.Receive(job.Shown);
                    if (key == null) continue;
                    ReceivedUtc[key] = job.Utc;   // the copy grew: Recent and the marks see it
                    if (job.Copy != null) { FellowCopies.Put(key, job.Shown, job.Copy); Trails.Add(key, job.Flat, job.SinceInstall, job.Utc); }
                }
                catch (Exception e) { if (!liveWarnedShow) Debug.LogWarning("[Hearthwoven] live update not shown: " + e.Message); liveWarnedShow = true; }
            }
            return n;
        }

        /// <summary>Live updates taken but not shown yet (queued, on the worker, or finished and waiting for DrainLive).</summary>
        public static int LivePending => liveTodo.Count + liveDone.Count + System.Threading.Volatile.Read(ref liveWorking);

        /// <summary>Host: the live updates in this PC's own store, applied to the book (no message).</summary>
        static void LiveLocal()
        {
            var profile = Game.instance?.GetPlayerProfile();
            var own = profile != null ? PeerIdentity.KeyOf(profile.GetPlayerID(), "", 0) : null;
            foreach (var e in Live.All.ToList())
            {
                if (e.Key == own) continue;
                var text = ServerIntake.Gunzip(e.Packed, LiveDelta.MaxJson, out _);
                if (text != null) ShowLive(e.Fellow, e.Base, e.Seq, text);
            }
        }

        /// <summary>Host: your own live update goes into this PC's store, as your full copy does (StoreOwn).</summary>
        internal static string LiveOwn(long profileId, string baseId, long seq, byte[] packed)
        {
            if (profileId == 0) return "no profile";
            return Live.Offer(PeerIdentity.KeyOf(profileId, "", 0), ZNet.GetUID(), baseId, seq, packed, Time.realtimeSinceStartup);
        }

        // a peer still here; on a hosted world the host itself counts (its own updates sit in the store under its own id)
        static bool Connected(long id) => id == ZNet.GetUID() || ZNet.instance?.GetPeer(id) != null;

        /// <summary>Server: a player's live update. Kept in memory (LiveStore) when it passes every check; nothing is written to disk.</summary>
        static void OnLive(long sender, ZPackage pkg)
        {
            try
            {
                if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
                if (pkg.ReadInt() != LiveDelta.Version) return;   // a later version's update: not ours to read
                var data = pkg.ReadByteArray();
                var peer = ZNet.instance.GetPeer(sender);
                if (peer == null || !PeerIdentity.Id(peer).HasValue) return;   // not known yet: its full copy comes first anyway
                Live.Prune(Connected); liveWarned.Prune(Connected);
                if (!Live.MayOffer(sender, Time.realtimeSinceStartup))   // before any gunzip or parse: a flood costs a dictionary lookup each
                {
                    if (liveWarned.First(sender, "live-rate")) Debug.LogWarning($"[Hearthwoven] live updates from peer {sender} arrive more often than one per {LiveStore.MinGap:0} s; the extra ones are ignored (logged once per connection)");
                    return;
                }
                var text = data != null && data.Length <= LiveDelta.MaxPacked ? ServerIntake.Gunzip(data, LiveDelta.MaxJson, out var why) : null;
                if (text == null || !ServerIntake.IsJsonObject(text) || !LiveDelta.Read(text, out var baseId, out var seq) || LiveDelta.Private(text) || LiveDelta.Identity(text))
                {
                    if (liveWarned.First(sender, "live-shape")) Debug.LogWarning($"[Hearthwoven] live update from peer {sender} dropped: not a live update this server keeps (logged once per connection)");
                    return;
                }
                var refused = Live.Offer(PeerIdentity.Key(peer), sender, baseId, seq, data, Time.realtimeSinceStartup);
                if (refused == "too often" && liveWarned.First(sender, "live-rate")) Debug.LogWarning($"[Hearthwoven] live updates from peer {sender} arrive more often than one per {LiveStore.MinGap:0} s; the extra ones are ignored (logged once per connection)");
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] live intake: " + e.Message); }
        }

        /// <summary>Server: the fellows' latest live updates this requester has not had yet, one message each through the outbox. Only to a
        /// requester who shares (its own copy is here), at most once per LiveStore.MinGap.</summary>
        static void OnLiveRequest(long sender, ZPackage pkg)
        {
            try
            {
                if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
                var peer = ZNet.instance.GetPeer(sender);
                if (peer == null || !PeerIdentity.Id(peer).HasValue) return;
                var ownKey = PeerIdentity.Key(peer);
                if (!File.Exists(Path.Combine(Dir, ownKey + ".share.json"))) return;   // you see others only while you share yourself
                if (!Live.MayAsk(sender, Time.realtimeSinceStartup)) return;
                foreach (var q in outbox) if (q.Target == sender) return;   // still sending your previous answer
                if (outbox.Count > MaxQueue) return;
                Live.Prune(Connected);
                foreach (var e in Live.For(sender, ownKey))
                {
                    var p = new ZPackage(); p.Write(e.Fellow ?? ""); p.Write(e.Base); p.Write(e.Seq); p.Write(e.Packed);
                    var entry = e;
                    outbox.Enqueue(new Pending { Target = sender, Pkg = p, Rpc = LiveDeltaRpc, Sent = () => Live.Served(sender, entry) });   // marked when it went out
                }
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] live serve: " + e.Message); }
        }
    }
}
