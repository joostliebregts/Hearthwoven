using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// Hearthwoven: how every player contributes, in their own play style.
    /// Client: sends the character's own statistics (retroactive to character creation), skills and the damage it dealt
    ///         and took this session to the server: on spawn, every few minutes and on logout.
    /// Server: writes them to BepInEx/Hearthwoven/players/, and logs routed RPC_Damage (cross-owner hits) for players
    ///         without the mod. Nothing in the game changes; a client without the mod notices nothing and nobody is kicked.
    /// Inspiration and credits: see README.md.
    /// </summary>
    [BepInPlugin(Guid, "Hearthwoven", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.joostliebregts.hearthwoven", Version = "0.2.0", RpcName = "Hearthwoven_Profile";
        static ConfigEntry<bool> sendStats, logRouted, shareWithGroup;
        static ConfigEntry<float> intervalMinutes;
        // Measured this session. A session = one connection to one server; reset after the logout send.
        internal static DamageTally Session = new DamageTally();
        internal static SessionEvents Events = new SessionEvents();
        internal static EventLog Log = new EventLog();
        static float nextSend = -1f, nextVoyage;
        static string SessionId = NewSessionId();
        static string NewSessionId() => System.Guid.NewGuid().ToString("N").Substring(0, 12);
        static readonly Fragments.Assembler Assembler = new Fragments.Assembler();
        static readonly System.Collections.Generic.Queue<ZPackage> Outbox = new System.Collections.Generic.Queue<ZPackage>();
        static float nextFragment;
        static StreamWriter routedWriter;
        static string routedDay;
        static float lastFlush;
        static BepInEx.Logging.ManualLogSource log;

        void Awake()
        {
            log = Logger;
            sendStats = Config.Bind("Client", "SendStats", true, "Send your character statistics, skills and damage to the server you play on.");
            intervalMinutes = Config.Bind("Client", "IntervalMinutes", 5f, "How often (minutes) to send while playing; also on spawn and logout.");
            shareWithGroup = Config.Bind("Client", "ShareWithGroup", true, "Do you want to see other players' stats? Then you'll share yours as well. Off: you see only your own, and nobody sees yours.");
            GroupShare.Sharing = () => shareWithGroup.Value;
            shareWithGroup.SettingChanged += (_, __) => { if (!shareWithGroup.Value) GroupShare.Clear(); GroupShare.SendShareState(); SendNow("share-changed"); };
            logRouted = Config.Bind("Server", "LogRoutedDamage", true, "Server: log routed damage (cross-owner hits) as a fallback for players without the mod.");
            // Patch each hook on its own: if a future game update breaks one, only that measurement stops.
            var harmony = new Harmony(Guid);
            int ok = 0, failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try { harmony.CreateClassProcessor(type).Patch(); ok++; }
                catch (Exception e) { failed++; Logger.LogWarning("hook " + type.Name + " skipped: " + (e.InnerException ?? e).Message); }
            }
            Logger.LogInfo($"Hearthwoven {Version} loaded: {ok} hooks active, {failed} skipped");
        }

        void Update()
        {
            var now = Time.realtimeSinceStartup;
            if (routedWriter != null && now - lastFlush > 10f) { routedWriter.Flush(); lastFlush = now; }
            if (nextSend > 0f && now >= nextVoyage) { ClientHooks.SampleVoyage(10f); ClientHooks.SampleCart(); nextVoyage = now + 10f; }
            if (nextSend > 0f && now >= nextSend) { SendNow("interval"); nextSend = now + Mathf.Max(1f, intervalMinutes.Value) * 60f; }
            // one fragment every 250 ms, never a burst
            GroupShare.Tick(now);
            if (Outbox.Count > 0 && now >= nextFragment && ZRoutedRpc.instance != null) { ZRoutedRpc.instance.InvokeRoutedRPC(RpcName, Outbox.Dequeue()); nextFragment = now + 0.25f; }
        }

        void OnDestroy() { routedWriter?.Flush(); routedWriter?.Dispose(); routedWriter = null; }

        // ---------- client ----------

        internal static void SendNow(string reason)
        {
            try
            {
                if (!sendStats.Value || ZNet.instance == null || ZNet.instance.IsServer() || ZRoutedRpc.instance == null) return;
                var player = Player.m_localPlayer;
                var profile = Game.instance?.GetPlayerProfile();
                if (player == null || profile == null) return;
                var skills = player.GetSkills()?.GetSkillList()?.Select(s => new Snapshot.SkillInfo
                    { Name = s.m_info.m_skill.ToString(), Level = s.m_level, Accumulator = s.m_accumulator }) ?? Enumerable.Empty<Snapshot.SkillInfo>();
                var json = Snapshot.Build(Version, profile.GetPlayerID(), profile.GetName(), profile.m_playerStats, skills,
                                          ZNet.instance.GetWorldName(), Session, SessionId, Events, Log, shareWithGroup.Value);
                var parts = Fragments.Split(Transport.Pack(json));
                var msgId = SessionId + "-" + DateTime.UtcNow.Ticks;
                Outbox.Clear();   // a newer snapshot replaces an unsent older one
                for (int i = 0; i < parts.Count; i++)
                {
                    var pkg = new ZPackage();
                    pkg.Write(reason); pkg.Write(msgId); pkg.Write(i); pkg.Write(parts.Count); pkg.Write(parts[i]);
                    Outbox.Enqueue(pkg);
                }
                if (reason == "logout") while (Outbox.Count > 0) ZRoutedRpc.instance.InvokeRoutedRPC(RpcName, Outbox.Dequeue());
            }
            catch (Exception e) { log?.LogWarning("send failed: " + e.Message); }
        }

        // Every session builds a new ZRoutedRpc; register on each new one exactly once (mods that registered once went
        // "deaf" after a rejoin, and registering twice on the same instance throws).
        [HarmonyPatch(typeof(ZRoutedRpc), MethodType.Constructor, typeof(bool))]
        static class RegisterRpc
        {
            static void Postfix(ZRoutedRpc __instance) { __instance.Register<ZPackage>(RpcName, OnProfile); GroupShare.Register(__instance); }
        }

        // A session = one connection: every new connection (another server, a reconnect after a lost link) starts from
        // zero under a new session id, so nothing carries over twice. Hooked on the connection, not on Logout, which can be
        // cancelled and is not called when the link drops (INTEGRITY P3).
        [HarmonyPatch(typeof(ZNet), "Awake")]
        static class NewConnection
        {
            static void Postfix()
            {
                Session = new DamageTally(); Events = new SessionEvents(); Log = new EventLog(); SessionId = NewSessionId();
                GroupShare.Clear(); nextSend = -1f;
            }
        }

        [HarmonyPatch(typeof(Player), "OnSpawned")]
        static class OnSpawn
        {
            static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                nextSend = Time.realtimeSinceStartup + 30f;   // first snapshot shortly after spawning
                GroupShare.SendShareState();                   // tell the server whether you share, even if SendStats is off
            }
        }

        [HarmonyPatch(typeof(Game), "Logout")]
        static class OnLogout
        {
            static void Prefix()
            {
                SendNow("logout");   // flushed now; the reset happens when the next connection starts (a logout can be cancelled)
            }
        }

        // ---------- server ----------

        static void OnProfile(long sender, ZPackage pkg)
        {
            try
            {
                if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
                var reason = pkg.ReadString();
                var msgId = pkg.ReadString();
                int index = pkg.ReadInt(), count = pkg.ReadInt();
                int rejected = Assembler.Rejected;
                var whole = Assembler.Add(sender, msgId, index, count, pkg.ReadByteArray());
                if (Assembler.Rejected != rejected) log?.LogWarning($"snapshot from peer {sender} rejected: {count} parts is over the limit (never silent: INTEGRITY P5)");
                if (whole == null) return;   // waiting for more fragments
                var json = Transport.Unpack(whole);
                var peer = ZNet.instance.GetPeer(sender);
                var envelope = new Json().Open()
                    .Str("received", DateTime.UtcNow.ToString("o")).Str("reason", reason)
                    .Str("peerName", peer?.m_playerName ?? "").Num("peerPlayerId", peer?.m_playerID ?? 0)
                    .Str("platform", peer?.m_socket?.GetHostName() ?? "")
                    .Raw("profile", json).Close().ToString();
                var dir = Path.Combine(Paths.BepInExRootPath, "Hearthwoven", "players");
                Directory.CreateDirectory(dir);
                var id = peer?.m_playerID ?? 0;
                File.WriteAllText(Path.Combine(dir, id + ".json"), envelope, Encoding.UTF8);
                // Measured values are absolute per session: keep the latest copy of EVERY session, so the sum over
                // sessions is the measured total (players/<id>.json alone would lose earlier sessions).
                var session = Transport.SafeName(Transport.SessionOf(json));
                if (session.Length > 0)
                {
                    var sdir = Path.Combine(dir, id.ToString(), "sessions");
                    Directory.CreateDirectory(sdir);
                    File.WriteAllText(Path.Combine(sdir, session + ".json"), envelope, Encoding.UTF8);
                }
                GroupShare.Store(id, json);
                File.AppendAllText(Path.Combine(dir, "received-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".jsonl"),
                                   new Json().Open().Str("t", DateTime.UtcNow.ToString("o")).Num("playerId", id).Str("name", peer?.m_playerName ?? "")
                                             .Str("reason", reason).Num("bytes", json.Length).Close() + "\n");
            }
            catch (Exception e) { log?.LogWarning("receive failed: " + e.Message); }
        }

        static void WriteRouted(string line)
        {
            var day = DateTime.UtcNow.ToString("yyyyMMdd");
            if (routedWriter == null || routedDay != day)
            {
                routedWriter?.Dispose();
                var dir = Path.Combine(Paths.BepInExRootPath, "Hearthwoven");
                Directory.CreateDirectory(dir);
                routedWriter = new StreamWriter(Path.Combine(dir, "damage-routed-" + day + ".jsonl"), true);
                routedDay = day;
            }
            routedWriter.WriteLine(line);
        }

        internal static string Describe(ZDOID id)
        {
            if (id.IsNone()) return null;
            var zdo = ZDOMan.instance?.GetZDO(id);
            if (zdo == null) return "?";
            var player = zdo.GetString(ZDOVars.s_playerName);
            if (!string.IsNullOrEmpty(player)) return player;
            var prefab = ZNetScene.instance?.GetPrefab(zdo.GetPrefab());
            return prefab != null ? prefab.name : "#" + zdo.GetPrefab();
        }

        // Reads the raw routed message first (before NPS's owner router), never changes or skips anything.
        [HarmonyPatch(typeof(ZRoutedRpc), "RPC_RoutedRPC")]
        static class RoutedPatch
        {
            [HarmonyPriority(Priority.First)]
            static void Prefix(ZPackage pkg)
            {
                try
                {
                    if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
                    var hit = RoutedDamage.TryRead(pkg, out var data);
                    ChestWatch.OnRouted(data);
                    if (hit == null || !logRouted.Value) return;
                    var r = new DamageRecord
                    {
                        Time = DateTime.UtcNow.ToString("o"),
                        Sender = ZNet.instance.GetPeer(data.m_senderPeerID)?.m_playerName,
                        Attacker = Describe(hit.m_attacker), Target = Describe(data.m_targetZDO),
                        Skill = hit.m_skill.ToString(), Cause = hit.m_hitType.ToString(),
                        StatusEffect = hit.m_statusEffectHash == 0 ? null : ObjectDB.instance?.GetStatusEffect(hit.m_statusEffectHash)?.name,
                        SkillLevel = hit.m_skillLevel, ItemLevel = hit.m_itemLevel, X = hit.m_point.x, Z = hit.m_point.z,
                    };
                    RoutedDamage.FillDamage(r, hit.m_damage);
                    WriteRouted(r.ToJson());
                }
                catch (Exception e) { Debug.LogWarning("[Hearthwoven] " + e.Message); }
            }
        }
    }

    public static class Transport
    {
        public static string SessionOf(string json) => Field(json, "session");

        /// <summary>A top-level string field written by Json.Str (first occurrence), or "".</summary>
        public static string Field(string json, string name)
        {
            var key = "\"" + name + "\":\"";
            var i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return "";
            i += key.Length; var b = new StringBuilder();
            for (; i < json.Length && json[i] != '"'; i++) { if (json[i] == '\\' && i + 1 < json.Length) i++; b.Append(json[i]); }
            return b.ToString();
        }

        public static string SafeName(string s) => new string((s ?? "").Where(c => char.IsLetterOrDigit(c) || c == '-').Take(40).ToArray());

        public static byte[] Pack(string s)
        {
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionMode.Compress)) { var b = Encoding.UTF8.GetBytes(s); gz.Write(b, 0, b.Length); }
                return ms.ToArray();
            }
        }
        public static string Unpack(byte[] data)
        {
            using (var gz = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
            using (var r = new StreamReader(gz, Encoding.UTF8)) return r.ReadToEnd();
        }
    }
}
