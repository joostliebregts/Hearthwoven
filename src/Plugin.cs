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
        public const string Guid = "com.joostliebregts.hearthwoven", Version = "0.6.2", RpcName = "Hearthwoven_Profile";
        static ConfigEntry<bool> sendStats, logRouted, shareWithGroup;
        static ConfigEntry<float> intervalMinutes;
        static ConfigEntry<int> compressAfterDays;
        // Measured this session. A session = one connection to one server; reset when the next connection starts.
        internal static DamageTally Session = new DamageTally();
        internal static SessionEvents Events = new SessionEvents();
        internal static EventLog Log = new EventLog();
        // Since install on this PC (INTEGRITY P7): the running total per character (LocalTotals), loaded on spawn, saved
        // with every send interval and on logout. The panel shows EventsBefore + Events; the event log stays per session.
        static LocalTotals Totals;
        internal static DateTime? InstalledUtc => Totals != null && Totals.FirstRunUtc > DateTime.MinValue ? Totals.FirstRunUtc : (DateTime?)null;
        /// <summary>The feats earned on this PC and the hook counters (saved with the local totals, shared in the snapshot); null until the totals load.</summary>
        internal static FeatsLedger FeatsLedger => Totals?.Feats;
        static string totalsPath, totalsFor;
        internal static SessionEvents EventsBefore = new SessionEvents();
        internal static SessionEvents EventsSinceInstall => SessionEvents.Sum(EventsBefore, Events);
        internal static DamageTally DamageBefore = new DamageTally();
        internal static DamageTally DamageSinceInstall => DamageTally.Sum(DamageBefore, Session);
        /// <summary>Damage and deaths per biome since install (BiomeTally): earlier sessions folded + this session's log. BiomeFromUtc: per biome counts from then.</summary>
        internal static BiomeTally BiomeBefore = new BiomeTally();
        internal static BiomeTally BiomeSinceInstall => biomeSum.Get(BiomeBefore, Log.Biome);   // the log folds per biome as hits come in; shared: read only
        static readonly BiomeTally.SumCache biomeSum = new BiomeTally.SumCache();
        internal static DateTime? BiomeFromUtc => Totals != null && Totals.BiomeFromUtc > Totals.FirstRunUtc.AddMinutes(10) ? Totals.BiomeFromUtc : (DateTime?)null;   // only when later than the install: an older file
        /// <summary>The game's counters when Hearthwoven first ran for this character (LocalTotals.Baseline); null = not loaded.</summary>
        internal static System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, float>> Baseline => Totals?.Baseline;
        /// <summary>What Hearthwoven had counted exactly when the baseline was taken (LocalTotals.ExactAtBaseline); null = not loaded.</summary>
        internal static System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, float>> ExactAtBaseline => Totals?.ExactAtBaseline;
        /// <summary>When each baseline kind was taken (LocalTotals.BaselineAt); null = not loaded.</summary>
        internal static System.Collections.Generic.Dictionary<string, DateTime> BaselineAt => Totals?.BaselineAt;
        /// <summary>The day history of this character (LocalTotals.History, HISTORY-06.md); null = not loaded.</summary>
        internal static DayHistory History => Totals?.History;
        /// <summary>What the running session counted since the last save, as today's row (LocalTotals.Pending): the panel adds it to the
        /// day history so Today is live. null = totals not loaded.</summary>
        internal static DayHistory.Row PendingDay()
        {
            try { return Totals?.Pending(SessionId, Session, Events, Log.Biome, GameStatsNow(), DateTime.Now); }
            catch (Exception e) { LogOnce("day history (live part) not read: " + e.Message); return null; }
        }
        /// <summary>The game's counters of the local character by name (PlayerStatType -> value), for the day history; null = no profile now.</summary>
        static System.Collections.Generic.Dictionary<string, float> GameStatsNow()
        {
            var all = Game.instance?.GetPlayerProfile()?.m_playerStats;
            if (all == null || all.Length == 0 || all[0]?.m_stats == null) return null;
            var d = new System.Collections.Generic.Dictionary<string, float>();
            foreach (var kv in all[0].m_stats) d[kv.Key.ToString()] = kv.Value;
            return d;
        }
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
        internal static int HooksOk, HooksFailed;   // for the self-check report (DevCheck)

        void Awake()
        {
            log = Logger;
            sendStats = Config.Bind("Client", "SendStats", true, "Send your character statistics, skills and damage to the server you play on.");
            intervalMinutes = Config.Bind("Client", "IntervalMinutes", 5f, "How often (minutes) to send while playing; also on spawn and logout.");
            shareWithGroup = Config.Bind("Client", "ShareWithGroup", true, "Do you want to see other players' stats? Then you'll share yours as well. Off: you see only your own, and nobody sees yours.");
            GroupShare.Sharing = () => shareWithGroup.Value;
            shareWithGroup.SettingChanged += (_, __) => { if (!shareWithGroup.Value) GroupShare.Clear(); GroupShare.SendShareState(); SendNow("share-changed"); };
            DevCheck.Bind(Config, Logger);   // Dev.SelfCheck: the in-game self-check, off for players (DevCheck.cs)
            logRouted = Config.Bind("Server", "LogRoutedDamage", false, "Server: log routed damage (cross-owner hits) to damage-routed-<day>.jsonl, a fallback for players without the mod. Off by default (Joost, 2026-10-09).");
            compressAfterDays = Config.Bind("Server", "CompressLogsAfterDays", 30, "Server: daily logs (chests, births, routed damage, received) older than this many days are gzipped in place (<name>.jsonl.gz), never deleted. 0 = never; at least 2.");
            // Patch each hook on its own: if a future game update breaks one, only that measurement stops.
            var harmony = new Harmony(Guid);
            int ok = 0, failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try { harmony.CreateClassProcessor(type).Patch(); ok++; }
                catch (Exception e) { failed++; Logger.LogWarning("hook " + type.Name + " skipped: " + (e.InnerException ?? e).Message); }
            }
            HooksOk = ok; HooksFailed = failed;
            Logger.LogInfo($"Hearthwoven {Version} loaded: {ok} hooks active, {failed} skipped");
        }

        void Update()
        {
            var now = Time.realtimeSinceStartup;
            if (routedWriter != null && now - lastFlush > 10f) { routedWriter.Flush(); lastFlush = now; }
            if (compressReport != null) ReportCompression();
            if (nextSend > 0f && now >= nextVoyage) { ClientHooks.SampleVoyage(10f); ClientHooks.SampleCart(); nextVoyage = now + 10f; Panel.FeatsTracker.Tick(now); }
            if (nextSend > 0f && now >= nextSend) { SaveLocal(); SendNow("interval"); nextSend = now + Mathf.Max(1f, intervalMinutes.Value) * 60f; }
            // one fragment every 250 ms, never a burst
            GroupShare.Tick(now);
            if (DevCheck.On) { DevCheck.Tick(); DevCheck.PerfTick(); }   // Dev.SelfCheck: the filter-key watch, the frame cost (one line a minute)
            if (Outbox.Count > 0 && now >= nextFragment && ZRoutedRpc.instance != null && !Panel.SampleMode.On) { ZRoutedRpc.instance.InvokeRoutedRPC(RpcName, Outbox.Dequeue()); nextFragment = now + 0.25f; }
        }

        void OnDestroy() { routedWriter?.Flush(); routedWriter?.Dispose(); routedWriter = null; }
        // Closing the game window without logging out: the local totals, and the session's last snapshot if the game's own
        // shutdown (OnShutdown, which runs first) has not already sent it.
        void OnApplicationQuit() { SaveLocal(); SendNow("quit"); }

        // ---------- client ----------

        // The final snapshot (logout, or the game shutting down) is flushed at once: a closing connection sends no queued
        // fragments later. Time-boxed so leaving the game is never held up; skipped when there is no connection to send on.
        const int MaxFinalParts = 40;       // a heavy 6 h session is about 6 parts; more than this is not sent (never a long block)
        const long FinalBudgetMs = 250;
        static float lastFinal = -100f;
        static bool IsFinal(string reason) => reason == "logout" || reason == "quit";

        internal static void SendNow(string reason)
        {
            try
            {
                if (Panel.SampleMode.Quiet("your stats snapshot")) { if (IsFinal(reason) && DevCheck.On) DevCheck.Final("INFO", reason, "not sent: Dev.SampleData is on"); Outbox.Clear(); return; }   // Dev.SampleData: nothing leaves this PC
                bool final = IsFinal(reason);
                bool check = final && DevCheck.On;   // Dev.SelfCheck: say why a last snapshot was not sent
                var clock = System.Diagnostics.Stopwatch.StartNew();
                bool host = GroupShare.IsHost();   // a world hosted from the game: your snapshot stays on this PC, in the group (E2)
                if (!sendStats.Value || ZNet.instance == null || (ZNet.instance.IsServer() && !host) || ZRoutedRpc.instance == null)
                {
                    if (check) DevCheck.Final("INFO", reason, "not sent: " + (!sendStats.Value ? "Client.SendStats is off" : ZNet.instance == null ? "no connection" : ZNet.instance.IsServer() ? "this PC is the server (nothing to send to)" : "no RPC"));
                    return;
                }
                if (final && !host)
                {
                    if (ZNet.instance.HaveStopped) { if (check) DevCheck.Final(lastFinal > 0f && Time.realtimeSinceStartup - lastFinal < 60f ? "INFO" : "WARN", reason, "not sent: the game had already closed the connection" + (lastFinal > 0f && Time.realtimeSinceStartup - lastFinal < 60f ? " (the logout snapshot went out before it)" : "")); return; }   // the game has already closed the connection
                    if (reason == "quit" && Time.realtimeSinceStartup - lastFinal < 5f) { if (check) DevCheck.Final("INFO", reason, "skipped: the logout snapshot went out " + (Time.realtimeSinceStartup - lastFinal).ToString("0.0") + " s ago (no double send)"); return; }   // logout just sent it
                    if (ZNet.instance.GetConnectedPeers().Count == 0) { if (check) DevCheck.Final("WARN", reason, "not sent: no connected peer"); return; }   // no connection: skip
                }
                var player = Player.m_localPlayer;
                var profile = Game.instance?.GetPlayerProfile();
                if (player == null || profile == null) { if (check) DevCheck.Final("WARN", reason, "not sent: no player or profile at this moment"); return; }
                var skills = player.GetSkills()?.GetSkillList()?.Select(s => new Snapshot.SkillInfo
                    { Name = s.m_info.m_skill.ToString(), Level = s.m_level, Accumulator = s.m_accumulator }) ?? Enumerable.Empty<Snapshot.SkillInfo>();
                var json = Snapshot.Build(Version, profile.GetPlayerID(), profile.GetName(), profile.m_playerStats, skills,
                                          ZNet.instance.GetWorldName(), Session, SessionId, Events, Log, shareWithGroup.Value,
                                          EventsSinceInstall, DamageSinceInstall, BiomeSinceInstall, BiomeFromUtc, FeatsLedger,
                                          Totals?.History.DealtByDay(DateTime.Now, 30, PendingDay()));   // your damage dealt per day (HISTORY-06: under 1 KB), for fellows' Together
                if (host)
                {
                    GroupShare.StoreOwn(profile.GetPlayerID(), json);   // the players who join see you; share off removes the copy
                    if (check) DevCheck.Final("INFO", reason, "kept on this PC: you host the world, so your copy is in the group here");
                    return;
                }
                var parts = Fragments.Split(Transport.Pack(json));
                var msgId = SessionId + "-" + DateTime.UtcNow.Ticks;
                Outbox.Clear();   // a newer snapshot replaces an unsent older one
                for (int i = 0; i < parts.Count; i++)
                {
                    var pkg = new ZPackage();
                    pkg.Write(reason); pkg.Write(msgId); pkg.Write(i); pkg.Write(parts.Count); pkg.Write(parts[i]);
                    Outbox.Enqueue(pkg);
                }
                if (final)
                {
                    int queued = Outbox.Count, sent = 0;
                    if (queued <= MaxFinalParts)
                        while (Outbox.Count > 0 && clock.ElapsedMilliseconds < FinalBudgetMs) { ZRoutedRpc.instance.InvokeRoutedRPC(RpcName, Outbox.Dequeue()); sent++; }
                    Outbox.Clear();   // whatever is left could not go out in time, and would be gone with the connection
                    lastFinal = Time.realtimeSinceStartup;
                    if (sent == queued) log?.LogInfo($"final snapshot sent ({reason}): {sent} parts, {json.Length} bytes of JSON, {clock.ElapsedMilliseconds} ms");
                    else log?.LogWarning($"final snapshot ({reason}) not sent in full: {sent} of {queued} parts in {clock.ElapsedMilliseconds} ms (never delays leaving the game)");
                    if (check) DevCheck.Final(sent == queued ? "PASS" : "FAIL", reason, $"sent {sent} of {queued} parts, {json.Length} bytes of JSON, in {clock.ElapsedMilliseconds} ms");
                }
            }
            catch (Exception e) { log?.LogWarning((IsFinal(reason) ? "final send failed: " : "send failed: ") + e.Message); }
        }

        // The local since-install totals of the character that just spawned (P7). A respawn in the same session keeps what
        // is loaded; the file's own session id keeps this session from being added twice. One file per character: player id +
        // name (LocalTotals.LoadFor, which also migrates a file from before 0.6.1 when exactly one character fits it).
        static void LoadLocal()
        {
            try
            {
                var profile = Game.instance?.GetPlayerProfile();
                if (profile == null) return;
                var id = profile.GetPlayerID(); var name = profile.GetName() ?? "";
                if (totalsFor == id + "|" + name + "|" + SessionId) return;
                totalsFor = id + "|" + name + "|" + SessionId;
                var gameNow = GameCounters(profile);
                Totals = LocalTotals.LoadFor(Path.Combine(Paths.BepInExRootPath, "Hearthwoven"), id, name, gameNow, () => OtherCharacters(profile, id), out totalsPath, out var problem);
                if (problem != null) LogOnce(problem);
                EventsBefore = Totals?.EventsBefore(SessionId) ?? new SessionEvents();
                DamageBefore = Totals?.DamageBefore(SessionId) ?? new DamageTally();
                BiomeBefore = Totals?.BiomeBefore(SessionId) ?? new BiomeTally();
                // the game's own counters as they stand now, once per character and kind (an older install: on the first load of
                // the version that baselines that kind); saved at once so a crash before the first send cannot move them later.
                // pickedUp and battle carry what Hearthwoven had counted exactly by then (the other kinds started at zero with it).
                if (Totals == null || gameNow == null) return;
                var took = false;
                foreach (var kind in BaselineKinds)
                    if (gameNow.TryGetValue(kind, out var counter) &&
                        Totals.TakeBaseline(kind, counter, kind == "pickedUp" ? EventsSinceInstall.PickedUp : kind == LocalTotals.BattleKind ? EventsSinceInstall.Battle : null))
                        took = true;
                if (took) Totals.Save(totalsPath, DateTime.UtcNow);
            }
            catch (Exception e) { Totals = null; log?.LogWarning("local totals not loaded: " + e.Message); }
        }

        // the baseline kinds, in the order they were introduced
        static readonly string[] BaselineKinds = { "pickedUp", "piecesPlaced", "itemsCrafted", "treesFelled", LocalTotals.StatsKind, LocalTotals.PickablesKind, LocalTotals.BattleKind };

        /// <summary>A character's game counters per baseline kind (kind -> token -> count): what TakeBaseline stores, and what
        /// LoadFor checks a file from before 0.6.1 against. pickedUp: pickups; piecesPlaced: placed pieces and plantings;
        /// itemsCrafted: items and dishes; treesFelled; stats: the Deeds counters the game keeps complete; pickables: picked
        /// plants and fish; battle: hits and deaths. null = no stats.</summary>
        static System.Collections.Generic.IDictionary<string, System.Collections.Generic.IDictionary<string, float>> GameCounters(PlayerProfile profile)
        {
            var all = profile?.m_playerStats;
            if (all == null || all.Length == 0 || all[0] == null) return null;
            var s = all[0];
            var d = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IDictionary<string, float>>
            {
                ["pickedUp"] = s.m_itemPickupStats, ["piecesPlaced"] = s.m_piecesPlacedStats, ["itemsCrafted"] = s.m_itemCraftStats,
                [LocalTotals.PickablesKind] = s.m_pickableStats,
            };
            if (s.m_stats != null)
            {
                d["treesFelled"] = new System.Collections.Generic.Dictionary<string, float> { ["Tree"] = s.m_stats.TryGetValue(PlayerStatType.Tree, out var felled) ? felled : 0f };
                d[LocalTotals.StatsKind] = s.m_stats.Where(kv => LocalTotals.StatsTokens.Contains(kv.Key.ToString())).ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
                d[LocalTotals.BattleKind] = s.m_stats.Where(kv => LocalTotals.BattleTokens.Contains(kv.Key.ToString())).ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
            }
            return d;
        }

        /// <summary>The game counters of every OTHER character on this PC with the same player id (a copied .fch), read from
        /// the character files; asked only when a file from before 0.6.1 is about to be migrated. null = could not be listed.</summary>
        static System.Collections.Generic.List<System.Collections.Generic.IDictionary<string, System.Collections.Generic.IDictionary<string, float>>> OtherCharacters(PlayerProfile current, long id)
        {
            try
            {
                var list = new System.Collections.Generic.List<System.Collections.Generic.IDictionary<string, System.Collections.Generic.IDictionary<string, float>>>();
                foreach (var p in SaveSystem.GetAllPlayerProfiles())
                    if (p != null && p.GetPlayerID() == id && p.GetFilename() != current.GetFilename())
                        list.Add(GameCounters(p) ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IDictionary<string, float>>());
                return list;
            }
            catch (Exception e) { log?.LogWarning("could not list the characters on this PC: " + e.Message); return null; }
        }

        // a problem with the local files is logged once per game run, not on every respawn or reconnect
        static readonly System.Collections.Generic.HashSet<string> logged = new System.Collections.Generic.HashSet<string>();
        static void LogOnce(string message) { if (logged.Add(message)) log?.LogWarning(message); }

        // previous sessions + this session as it is now, under this session's id (a second save replaces the first)
        static void SaveLocal()
        {
            try { if (Totals != null && totalsPath != null) { Totals.Record(SessionId, Session, Events, Log, GameStatsNow(), DateTime.Now); Totals.Save(totalsPath, DateTime.UtcNow); } }
            catch (Exception e) { log?.LogWarning("local totals not saved: " + e.Message); }
        }

        // Every session builds a new ZRoutedRpc; register on each new one exactly once (mods that registered once went
        // "deaf" after a rejoin, and registering twice on the same instance throws).
        [HarmonyPatch(typeof(ZRoutedRpc), MethodType.Constructor, typeof(bool))]
        static class RegisterRpc
        {
            static void Postfix(ZRoutedRpc __instance) => HookGuard.Run(() => { __instance.Register<ZPackage>(RpcName, OnProfile); GroupShare.Register(__instance); });
        }

        // A session = one connection: every new connection (another server, a reconnect after a lost link) starts from
        // zero under a new session id, so nothing carries over twice. Hooked on the connection, not on Logout, which can be
        // cancelled and is not called when the link drops (INTEGRITY P3).
        [HarmonyPatch(typeof(ZNet), "Awake")]
        static class NewConnection
        {
            static void Postfix() => HookGuard.Run(() =>
            {
                Session = new DamageTally(); Events = new SessionEvents(); Log = new EventLog(); SessionId = NewSessionId();
                Totals = null; totalsPath = totalsFor = null; EventsBefore = new SessionEvents(); DamageBefore = new DamageTally(); BiomeBefore = new BiomeTally();   // loaded again on spawn (P7)
                GroupShare.Clear(); nextSend = -1f; FeatsCounters.Streak = 0;
            });
        }

        [HarmonyPatch(typeof(Player), "OnSpawned")]
        static class OnSpawn
        {
            static void Postfix(Player __instance) => HookGuard.Run(() =>
            {
                if (__instance != Player.m_localPlayer) return;
                LoadLocal();                                   // since-install totals of this character (P7)
                nextSend = Time.realtimeSinceStartup + 30f;   // first snapshot shortly after spawning
                GroupShare.SendShareState();                   // tell the server whether you share, even if SendStats is off
            });
        }

        [HarmonyPatch(typeof(Game), "Logout")]
        static class OnLogout
        {
            static void Prefix() => HookGuard.Run(() =>
            {
                SaveLocal();         // the local since-install totals (P7)
                SendNow("logout");   // flushed now; the reset happens when the next connection starts (a logout can be cancelled)
                HookGuard.EndSession();   // one line: which hooks failed this session and how often
            });
        }

        // The game's own shutdown: logout to the menu (after it) and closing the window both end in Game.Shutdown, which closes the
        // connection. Our send goes in front of it, so the session's last minutes reach the server. A prefix that never skips the
        // game's method; an exception here is caught inside SendNow.
        [HarmonyPatch(typeof(Game), "Shutdown")]
        static class OnShutdown
        {
            static void Prefix() => HookGuard.Run(() => { SaveLocal(); SendNow("quit"); HookGuard.EndSession(); });
        }

        // ---------- server ----------

        // What a snapshot must pass before it is stored (ServerIntake, RESILIENCE-06 item 5): bounded parts and messages (Assembler),
        // at most one snapshot per sender per 20 s (RateGate), a bounded gunzip, one JSON object. Each problem is logged once per sender.
        static readonly ServerIntake.RateGate Gate = new ServerIntake.RateGate();
        static readonly ServerIntake.OnceEach Warned = new ServerIntake.OnceEach();

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
                if (Assembler.Rejected != rejected && Warned.First(sender, "parts"))
                    log?.LogWarning($"snapshot from peer {sender} rejected: part {index} of {count} is over the limit ({Fragments.Assembler.MaxParts} parts of {Fragments.Size} bytes); logged once per connection (never silent: INTEGRITY P5)");
                if (whole == null) return;   // waiting for more fragments
                Gate.Prune(id => ZNet.instance.GetPeer(id) != null); Warned.Prune(id => ZNet.instance.GetPeer(id) != null);
                if (!Gate.Allow(sender, reason, Time.realtimeSinceStartup, out var firstRefusal))
                {
                    if (firstRefusal) log?.LogWarning($"snapshots from peer {sender} arrive more often than one per {ServerIntake.RateGate.MinGap:0} s; the extra ones are ignored (logged once per connection)");
                    return;
                }
                var json = ServerIntake.Gunzip(whole, ServerIntake.MaxJsonBytes, out var why);
                if (json != null && !ServerIntake.IsJsonObject(json)) { json = null; why = "not one JSON object"; }
                if (json == null)
                {
                    if (Warned.First(sender, "shape")) log?.LogWarning($"snapshot from peer {sender} dropped: {why} (logged once per connection)");
                    return;
                }
                var peer = ZNet.instance.GetPeer(sender);
                // who sent it: the game's peer.m_playerID is always 0 on a dedicated server (PeerIdentity), so the key comes
                // from the character's ZDO, else the playerId this snapshot carries, bound to this peer
                PeerIdentity.Bind(sender, PeerIdentity.SnapshotPlayerId(json));
                var key = peer != null ? PeerIdentity.Key(peer) : PeerIdentity.KeyOf(PeerIdentity.IdOf(0, 0, PeerIdentity.SnapshotPlayerId(json)), "", sender);
                var resolved = peer != null ? PeerIdentity.Id(peer) : null;
                var envelope = new Json().Open()
                    .Str("received", DateTime.UtcNow.ToString("o")).Str("reason", reason)
                    .Str("peerName", peer?.m_playerName ?? "").Num("peerPlayerId", peer?.m_playerID ?? 0)
                    .Str("playerKey", key).Str("platform", peer?.m_socket?.GetHostName() ?? "")
                    .Raw("profile", json).Close().ToString();
                var dir = Path.Combine(Paths.BepInExRootPath, "Hearthwoven", "players");
                Directory.CreateDirectory(dir);
                // every server JSON file is written whole (AtomicFile: temp, flushed, replaced, the copy before kept as .bak),
                // so a crash or a full disk never leaves a truncated file
                AtomicFile.Write(Path.Combine(dir, key + ".json"), envelope);
                // Measured values are absolute per session: keep the latest copy of EVERY session, so the sum over
                // sessions is the measured total (players/<id>.json alone would lose earlier sessions).
                var session = Transport.SafeName(Transport.SessionOf(json));
                if (session.Length > 0)
                    AtomicFile.Write(Path.Combine(dir, key, "sessions", session + ".json"), envelope);
                // shared only under a real player id, never a platform fallback; the copy carries the platform id so fellows are told apart
                if (resolved.HasValue) GroupShare.Store(key, FellowIds.WithPlatform(json, peer?.m_socket?.GetHostName()));
                File.AppendAllText(Path.Combine(dir, "received-" + DailyLogs.Day(DateTime.UtcNow) + ".jsonl"),
                                   new Json().Open().Str("t", DateTime.UtcNow.ToString("o")).Num("playerId", resolved ?? 0).Str("playerKey", key).Str("name", peer?.m_playerName ?? "")
                                             .Str("reason", reason).Num("bytes", json.Length).Close() + "\n");
            }
            catch (Exception e) { log?.LogWarning("receive failed: " + e.Message); }
        }

        // ---------- old daily logs: compressed, never deleted (Joost, 2026-10-09) ----------

        static string compressedDay;
        static volatile DailyLogs.Report compressReport;

        /// <summary>Server start and every world save: once per UTC day, gzip the daily logs older than Server.CompressLogsAfterDays,
        /// on a worker thread (DailyLogs.Gate keeps a book rebuild from reading a day while it is being compressed).</summary>
        internal static void CompressOldLogs()
        {
            try
            {
                int days = compressAfterDays?.Value ?? 0;
                var today = DailyLogs.Day(DateTime.UtcNow);
                if (days <= 0 || compressedDay == today || ZNet.instance == null || !ZNet.instance.IsServer()) return;
                compressedDay = today;
                var root = Path.Combine(Paths.BepInExRootPath, "Hearthwoven");
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { compressReport = DailyLogs.Compress(new[] { root, Path.Combine(root, "players") }, DateTime.UtcNow, days); }
                    catch (Exception e) { var r = new DailyLogs.Report(); r.Failed++; r.Problems.Add(e.Message); compressReport = r; }
                });
            }
            catch (Exception e) { log?.LogWarning("log compression not started: " + e.Message); }
        }

        static void ReportCompression()
        {
            var r = compressReport; compressReport = null;
            if (r == null || r.Compressed + r.Kept + r.Failed == 0) return;
            log?.LogInfo($"old daily logs compressed: {r.Compressed} files, {r.BytesBefore / 1024} KB -> {r.BytesAfter / 1024} KB; {r.Kept} kept as they were, {r.Failed} failed (originals kept)");
            foreach (var p in r.Problems.Take(5)) log?.LogWarning("log compression: " + p);
        }

        static void WriteRouted(string line)
        {
            var day = DailyLogs.Day(DateTime.UtcNow);
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
                catch (Exception e) { HookGuard.Fail(e, "Plugin.RoutedPatch.Prefix"); }
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
        /// <summary>The text Pack packed; bounded (ServerIntake.Gunzip): over the limits or not gzip throws InvalidDataException.</summary>
        public static string Unpack(byte[] data) =>
            ServerIntake.Gunzip(data, ServerIntake.MaxJsonBytes, out var why) ?? throw new InvalidDataException("unpack refused: " + why);
    }
}
