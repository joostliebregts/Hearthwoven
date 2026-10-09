using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

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
                            StateRpc = "Hearthwoven_ShareState", ListRpc = "Hearthwoven_GroupList";
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
        internal static Func<bool> Sharing = () => true;

        static float nextRequest;
        static readonly Fragments.Assembler assembler = new Fragments.Assembler();
        class Pending { public long Target; public ZPackage Pkg; }
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
            if (Shares(json)) AtomicFile.Write(file, SharedCopy(json));   // written whole: a crash never leaves a truncated copy
            else Unshare(playerKey);
        }

        static void Unshare(string playerKey)
        {
            if (string.IsNullOrEmpty(playerKey) || playerKey == "0") return;
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
            serve.Forget(sender);   // its copies were cleared: the next answer sends them all again
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
                var keep = ReadList(pkg, out var books, out var keys);
                Fellows.KeepOnly(keys, keep);   // by key from a server with the identity fix, else by name (an older server)
                ReadBooks(books);
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] group list: " + e.Message); }
        }

        /// <summary>The list as the server writes it: the sharers' names, then (0.6) the tag and the packed book. A reader before 0.6
        /// reads the names and stops; this one reads on when there is more. <paramref name="books"/> = the book's JSON, null without one.</summary>
        public static HashSet<string> ReadList(ZPackage pkg, out string books) => ReadList(pkg, out books, out _);

        /// <summary>The list with its tagged parts after the names: the book (BooksTag) and the fellow keys (KeysTag, null from a
        /// server without them). An unknown tag ends the reading.</summary>
        public static HashSet<string> ReadList(ZPackage pkg, out string books, out List<string> keys)
        {
            int n = pkg.ReadInt();
            var names = new HashSet<string>();
            for (int i = 0; i < n; i++) names.Add(pkg.ReadString());
            books = null; keys = null;
            while (pkg.GetPos() < pkg.Size())
            {
                var tag = pkg.ReadString();
                if (tag == BooksTag) books = Transport.Unpack(pkg.ReadByteArray());
                else if (tag == KeysTag) { int k = pkg.ReadInt(); keys = new List<string>(); for (int i = 0; i < k; i++) keys.Add(pkg.ReadString()); }
                else break;
            }
            return names;
        }

        /// <summary>Server: the list for one answer (ReadList reads it). <paramref name="keys"/>: each name's fellow key, after the book.</summary>
        public static ZPackage WriteList(IList<string> names, string booksJson, IList<string> keys = null)
        {
            var list = new ZPackage(); list.Write(names.Count); foreach (var nm in names) list.Write(nm);
            if (booksJson != null) { list.Write(BooksTag); list.Write(Transport.Pack(booksJson)); }
            if (keys != null) { list.Write(KeysTag); list.Write(keys.Count); foreach (var k in keys) list.Write(k ?? ""); }
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
                if (Fellows.Receive(json) != null) LastReply = Time.realtimeSinceStartup;
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] group reply: " + e.Message); }
        }

        /// <summary>Client: forget what was received (on logout, or when sharing is switched off).</summary>
        internal static void Clear() { Fellows.Clear(); Books.Clear(); BooksByKey.Clear(); OwnBook = null; nextRequest = 0f; }

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
                var answer = serve.Plan(sender, sources, now, file => File.Exists(file) ? File.ReadAllText(file, Encoding.UTF8) : null,
                                        text => Fragments.Split(Transport.Pack(text)), text => Transport.Field(text, "name"),
                                        text => Transport.Field(text, "playerId") + "-" + DateTime.UtcNow.Ticks, FellowIds.KeyOf);
                // 0.6: the server's book for you and each sharer, after the names (an older client stops reading there), then each
                // name's fellow key (two players with one name both show; an older client stops before it)
                var list = WriteList(answer.Names, ServerBookHooks.Shared(PeerIdentity.Key(peer), answer.Keys, answer.Names, answer.Fellows), answer.Fellows);
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
            if (outbox.Count == 0 || now < nextFragment || ZRoutedRpc.instance == null) return;
            var next = outbox.Dequeue();
            if (ZNet.instance?.GetPeer(next.Target) != null) ZRoutedRpc.instance.InvokeRoutedRPC(next.Target, ReplyRpc, next.Pkg);
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
                string text; try { text = File.ReadAllText(file, Encoding.UTF8); } catch { continue; }
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
                var key = Fellows.Receive(s.Value);
                if (key == null) continue;
                fileKeys.Add(s.Key); names.Add(Fellows.NameOf(key)); keys.Add(key);
            }
            Fellows.KeepOnly(keys, names);
            ReadBooks(books?.Invoke(fileKeys, names, keys));
        }

        static void RequestLocal()
        {
            var profile = Game.instance?.GetPlayerProfile();
            if (profile == null) return;
            var ownKey = PeerIdentity.KeyOf(profile.GetPlayerID(), "", 0);
            HostAnswer(HostSources(Dir, ownKey, DateTime.UtcNow), (fileKeys, names, keys) => ServerBookHooks.Shared(ownKey, fileKeys, names, keys));
            LastReply = Time.realtimeSinceStartup;
        }
    }
}
