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
    /// Client: asks when the panel opens (at most every 30 s) and keeps the replies in Group (name -> snapshot JSON).
    /// </summary>
    public static class GroupShare
    {
        public const string RequestRpc = "Hearthwoven_GroupRequest", ReplyRpc = "Hearthwoven_Group",
                            StateRpc = "Hearthwoven_ShareState", ListRpc = "Hearthwoven_GroupList";
        public const int MaxQueue = 600, MaxAgeDays = 14;
        public static readonly Dictionary<string, string> Group = new Dictionary<string, string>();
        public static float LastReply = -1f;
        internal static Func<bool> Sharing = () => true;

        static float nextRequest;
        static readonly Fragments.Assembler assembler = new Fragments.Assembler();
        class Pending { public long Target; public ZPackage Pkg; }
        static readonly Queue<Pending> outbox = new Queue<Pending>();
        static readonly Dictionary<long, float> lastServed = new Dictionary<long, float>();
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
        internal static void Store(long playerId, string json)
        {
            if (playerId == 0) return;   // unknown player: never shared
            var file = Path.Combine(Dir, playerId + ".share.json");
            if (Shares(json)) File.WriteAllText(file, SharedCopy(json), Encoding.UTF8);
            else Unshare(playerId);
        }

        static void Unshare(long playerId)
        {
            var file = Path.Combine(Dir, playerId + ".share.json");
            if (File.Exists(file)) File.Delete(file);
        }

        /// <summary>Client: tell the server whether you share, on every spawn and when the setting changes. Separate from
        /// the stats themselves, so switching sharing off works even with SendStats off (INTEGRITY S3).</summary>
        public static void SendShareState()
        {
            try
            {
                if (ZNet.instance == null || ZNet.instance.IsServer() || ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.InvokeRoutedRPC(StateRpc, Sharing());
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] share state: " + e.Message); }
        }

        static void OnState(long sender, bool sharing)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || sharing) return;   // sharing on: the next snapshot brings the copy
            var peer = ZNet.instance.GetPeer(sender);
            if (peer != null) Unshare(peer.m_playerID);
        }

        /// <summary>Client: the server's current list of sharers; anyone else is forgotten (they stopped sharing).</summary>
        static void OnList(long sender, ZPackage pkg)
        {
            try
            {
                if (ZNet.instance == null || ZNet.instance.IsServer()) return;
                int n = pkg.ReadInt();
                var keep = new HashSet<string>();
                for (int i = 0; i < n; i++) keep.Add(pkg.ReadString());
                foreach (var name in new List<string>(Group.Keys)) if (!keep.Contains(name)) Group.Remove(name);
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] group list: " + e.Message); }
        }

        // ---------- client ----------

        /// <summary>Ask the server for fellow players' stats. Rate-limited; does nothing if you do not share.</summary>
        public static void Request()
        {
            try
            {
                if (!Sharing() || ZNet.instance == null || ZNet.instance.IsServer() || ZRoutedRpc.instance == null) return;
                if (Time.realtimeSinceStartup < nextRequest) return;
                nextRequest = Time.realtimeSinceStartup + 30f;
                ZRoutedRpc.instance.InvokeRoutedRPC(RequestRpc, new ZPackage());
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
                var name = Transport.Field(json, "name");
                if (name.Length > 0) { Group[name] = json; LastReply = Time.realtimeSinceStartup; }
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] group reply: " + e.Message); }
        }

        /// <summary>Client: forget what was received (on logout, or when sharing is switched off).</summary>
        internal static void Clear() { Group.Clear(); nextRequest = 0f; }

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
                var own = Path.Combine(Dir, peer.m_playerID + ".share.json");
                if (!File.Exists(own)) return;   // you see others only while you share yourself
                foreach (var q in outbox) if (q.Target == sender) return;   // still sending your previous answer
                if (outbox.Count > MaxQueue) return;                         // busy: ask again later (client retries every 30 s)
                var names = new List<string>();
                var files = new List<string>();
                foreach (var file in Directory.GetFiles(Dir, "*.share.json"))
                {
                    if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(own), StringComparison.Ordinal)) continue;
                    if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(file)).TotalDays > MaxAgeDays) continue;   // not seen for two weeks: no longer shown
                    files.Add(file);
                }
                var texts = new List<string>();
                foreach (var file in files) { var txt = File.ReadAllText(file, Encoding.UTF8); texts.Add(txt); names.Add(Transport.Field(txt, "name")); }
                var list = new ZPackage(); list.Write(names.Count); foreach (var nm in names) list.Write(nm);
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, ListRpc, list);
                foreach (var text in texts)
                {
                    var parts = Fragments.Split(Transport.Pack(text));
                    var msgId = Transport.Field(text, "playerId") + "-" + DateTime.UtcNow.Ticks;
                    for (int i = 0; i < parts.Count; i++)
                    {
                        var p = new ZPackage(); p.Write(msgId); p.Write(i); p.Write(parts.Count); p.Write(parts[i]);
                        outbox.Enqueue(new Pending { Target = sender, Pkg = p });
                    }
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
    }
}
