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
        public const string Guid = "com.joostliebregts.hearthwoven", Version = "0.8.1", RpcName = "Hearthwoven_Profile";
        static ConfigEntry<bool> sendStats, logRouted, shareWithGroup;
        static ConfigEntry<float> intervalMinutes;
        static ConfigEntry<int> compressAfterDays, intervalLayout;
        // Measured this session. A session = one connection to one server; reset when the next connection starts.
        internal static DamageTally Session = new DamageTally();
        internal static SessionEvents Events = new SessionEvents();
        internal static EventLog Log = new EventLog();
        /// <summary>How much your armour helped this session (ArmourHooks, 0.7): its own ledger, never part of Session or Log, never shared.</summary>
        internal static ArmourTally Armour = new ArmourTally();
        internal static ArmourLog ArmourMinutes = new ArmourLog();
        /// <summary>Everything recorded on your armour on this PC (the book's earlier sessions + this one), the book itself (day rows), what this
        /// session added since the last save, and when recording began (the "Recorded from" date). Totals not loaded: this session only, no date.</summary>
        internal static ArmourTally ArmourSince => Totals != null ? ArmourTally.Sum(Totals.Armour.BeforeSession(SessionId), Armour) : Armour;
        internal static ArmourBook ArmourBook => Totals?.Armour;
        internal static ArmourTally ArmourPending() => Totals?.Armour.Pending(SessionId, Armour);
        internal static DateTime? ArmourFromUtc => Totals != null && Totals.Armour.FromUtc > DateTime.MinValue ? Totals.Armour.FromUtc : (DateTime?)null;
        /// <summary>The battle record (0.8, BattleRecord.cs): this session's foes and fights; the foes kept on this PC (the book's earlier sessions + this
        /// session), what this session added since the last save, and when recording began. Battle.DealtAfterArmour (dev): your damage after the foe's
        /// armour this session (BattleHooks), its own ledger, never saved or shared.</summary>
        internal static BattleRecorder Battle = new BattleRecorder();
        internal static FoeBook FoeBook => Totals?.Foes;
        internal static FoeCounts FoesSince => Totals != null ? FoeCounts.Sum(Totals.Foes.BeforeSession(SessionId), Battle.Counts(null)) : Battle.Counts(null);
        internal static FoeCounts FoesPending() => Totals?.Foes.Pending(SessionId, Battle.Counts(null));
        internal static DateTime? FoesFromUtc => Totals != null && Totals.Foes.FromUtc > DateTime.MinValue ? Totals.Foes.FromUtc : (DateTime?)null;
        internal static ArmourTally DealtArmour = new ArmourTally();
        internal static ArmourLog DealtArmourMinutes = new ArmourLog();
        /// <summary>The foe counts in your shared copy (FoeShare): this session and since recording began, compact; the feed stays here.</summary>
        static FoeShare FoeShareNow() => new FoeShare { Session = Battle.Counts(null), Since = Totals != null ? FoesSince : null, FromUtc = FoesFromUtc };
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
        /// <summary>When each counter group began on this PC (LocalTotals.Starts: cargo, led, born, feats); null = not loaded.</summary>
        internal static System.Collections.Generic.Dictionary<string, DateTime> Starts => Totals?.Starts;
        /// <summary>The day history of this character (LocalTotals.History, HISTORY-06.md); null = not loaded.</summary>
        internal static DayHistory History => Totals?.History;
        /// <summary>What the running session counted since the last save, as today's row (LocalTotals.Pending): the panel adds it to the
        /// day history so Today is live. null = totals not loaded.</summary>
        internal static DayHistory.Row PendingDay()
        {
            try { return Totals?.Pending(SessionId, Session, Events, Log.Biome, GameStatsNow(), DateTime.Now); }
            catch (Exception e) { LogOnce("day history (live part) not read: " + e.Message); return null; }
        }
        /// <summary>The game's counters of the local character by name (PlayerStatType -> value, and the per-token families DayHistory keeps), for the day history; null = no profile now.</summary>
        static System.Collections.Generic.Dictionary<string, float> GameStatsNow()
        {
            var all = Game.instance?.GetPlayerProfile()?.m_playerStats;
            if (all == null || all.Length == 0 || all[0]?.m_stats == null) return null;
            var d = new System.Collections.Generic.Dictionary<string, float>();
            foreach (var kv in all[0].m_stats) d[GameNames.Of(kv.Key)] = kv.Value;   // names worked out once (GameNames)
            // 0.7: the per-token counters the Deeds day windows read (pieces placed, plants and fish picked), under their family prefix
            if (all[0].m_piecesPlacedStats != null) foreach (var kv in all[0].m_piecesPlacedStats) d[KeyJoin.Of(DayHistory.PlacedPrefix, kv.Key)] = kv.Value;   // joined once (KeyJoin)
            if (all[0].m_pickableStats != null) foreach (var kv in all[0].m_pickableStats) d[KeyJoin.Of(DayHistory.PickedPrefix, kv.Key)] = kv.Value;
            return d;
        }
        /// <summary>Deeds > Recent (0.7, DeedLog.cs): this session's deeds per minute, a time split of Events and of the game's counters' growth.</summary>
        internal static DeedLog Deeds = new DeedLog();
        static readonly Func<System.Collections.Generic.IDictionary<string, float>> deedGame = DeedGame;
        /// <summary>When your previous session on this PC ended (the totals file's last save, read before this session saved); null = none here.</summary>
        internal static DateTime? PreviousSessionEndUtc;
        /// <summary>Fellow players as this PC last saw them (FellowMarks.cs): Before = up to the previous session, for "since last time".</summary>
        internal static FellowMarks Marks;
        static string marksPath, recentFor;
        /// <summary>The deeds log with the running minute booked (the panel reads it, at most every refresh).</summary>
        internal static DeedLog DeedsNow()
        {
            try { Deeds.Flush(DateTime.UtcNow, Events, DeedGame()); } catch (Exception e) { LogOnce("recent deeds not read: " + e.Message); }
            return Deeds;
        }
        /// <summary>The game's counters DeedLog reads (DeedLog.GameKeys); null = no profile now.</summary>
        static System.Collections.Generic.IDictionary<string, float> DeedGame()
        {
            var all = Game.instance?.GetPlayerProfile()?.m_playerStats;
            if (all == null || all.Length == 0 || all[0]?.m_stats == null) return null;
            var s = all[0];
            var stats = new System.Collections.Generic.Dictionary<string, float>();
            foreach (var (name, t) in DeedCounters())
                if (s.m_stats.TryGetValue(t, out var v)) stats[name] = v;
            return DeedLog.GameKeys(stats, s.m_piecesPlacedStats, s.m_pickableStats, s.m_enemyStats != null && s.m_enemyStats.Length > 0 ? s.m_enemyStats[0] : null);
        }
        // DeedLog.CounterStats as the game's counters, parsed once (0.8.1: Enum.TryParse on every refresh); a name this game does not have is left out, as before
        static System.Collections.Generic.List<(string name, PlayerStatType stat)> deedCounters;
        static System.Collections.Generic.List<(string name, PlayerStatType stat)> DeedCounters()
        {
            if (deedCounters != null) return deedCounters;
            var list = new System.Collections.Generic.List<(string name, PlayerStatType stat)>();
            foreach (var name in DeedLog.CounterStats) if (Enum.TryParse<PlayerStatType>(name, out var t)) list.Add((name, t));
            return deedCounters = list;
        }
        /// <summary>Once per session, on the first spawn after the totals loaded: when the previous session ended, and the fellows' marks.</summary>
        static void RecentOnSpawn()
        {
            if (recentFor == SessionId) return;
            recentFor = SessionId;
            PreviousSessionEndUtc = Totals != null && Totals.LastSession.Length > 0 && Totals.LastSession != SessionId && Totals.SavedUtc > DateTime.MinValue ? Totals.SavedUtc : (DateTime?)null;
            marksPath = totalsPath != null ? FellowMarks.PathFor(totalsPath) : null;
            string problem = null;
            Marks = marksPath != null ? FellowMarks.Load(marksPath, out problem) : new FellowMarks();
            if (problem != null) LogOnce(problem);   // one line when the file was restored from its backup (RESILIENCE item 5)
        }
        /// <summary>The quiet group request (GroupShare.AskInBackground): on spawn and once per send interval, only when you share.</summary>
        static void AskGroup()
        {
            try { if (GroupShare.AskInBackground(shareWithGroup.Value, nextSend > 0f, Player.m_localPlayer != null)) GroupShare.Request(); }
            catch (Exception e) { LogOnce("group request (background) failed: " + e.Message); }
        }
        static void SaveMarks()
        {
            if (Marks == null || marksPath == null || Panel.SampleMode.Quiet("fellow marks") || !shareWithGroup.Value) return;   // not sharing: no fellow's numbers kept (REVIEW-07 #5)
            if (Marks.Update(GroupShare.Group, GroupShare.ReceivedUtc, DateTime.UtcNow)) Marks.Save(marksPath);
        }
        static float nextSend = -1f, nextVoyage, nextJoinWatch, lastFullSend = -100f;
        /// <summary>The first copy after a spawn (0.7, SYNC-DESIGN.md item 2): 10 s after it (was 30 s, "shortly after spawning", no other reason
        /// recorded; the local totals are loaded before, in the same hook), but never within the server's RateGate gap (20 s) of the previous
        /// copy of this connection, which it would refuse with a warning (a death and quick respawn right after an interval send).</summary>
        public const float FirstSendDelay = 10f, MinSendGap = (float)ServerIntake.RateGate.MinGap + 1f;
        public static float FirstSendAt(float now, float lastSend) => Math.Max(now + FirstSendDelay, lastSend + MinSendGap);
        static string SessionId = NewSessionId();
        // Live updates (0.7, SYNC-DESIGN.md item 4): the last full copy sent (its shared form and copyId: the base), the last update's
        // seq, and the one diff in work on a worker thread (parse, diff and gzip off the main thread; the snapshot itself is built here)
        static int copyCount;
        static string liveBase, liveBaseId;
        static long liveSeq;
        static float nextLive;
        static bool liveBusy;
        class LiveJob { public string Id; public long Seq; public LiveDelta.Made Made; }
        static volatile LiveJob liveDone;
        static string NewSessionId() => System.Guid.NewGuid().ToString("N").Substring(0, 12);
        /// <summary>This connection's session id (Since you were away opens by itself once per session, PanelModel.OpenAway).</summary>
        internal static string CurrentSession => SessionId;
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
            intervalMinutes = Config.Bind("Client", "IntervalMinutes", 2f, "How often (minutes) to send your stats while playing, at least 1; also 10 s after you spawn and when you log out.");
            intervalLayout = Config.Bind("Client", "IntervalLayout", 0, "Internal: which send interval default this config was moved to (1: 2 minutes, the old default 5 moved once). Do not edit.");
            var interval = MigrateInterval(intervalLayout.Value, intervalMinutes.Value);   // once per config (0.7, SYNC-DESIGN.md item 3)
            if (interval.layout != intervalLayout.Value || interval.minutes != intervalMinutes.Value) { intervalMinutes.Value = interval.minutes; intervalLayout.Value = interval.layout; }
            shareWithGroup = Config.Bind("Client", "ShareWithGroup", true, "Do you want to see other players' stats? Then you'll share yours as well. Off: you see only your own, and nobody sees yours.");
            GroupShare.Sharing = () => shareWithGroup.Value;
            if (!shareWithGroup.Value) GroupShare.ForgetCache(Path.Combine(Paths.BepInExRootPath, "Hearthwoven"));   // switched off in the file while the game was closed: keep nobody's copy (B23)
            shareWithGroup.SettingChanged += (_, __) => { if (!shareWithGroup.Value) { GroupShare.Clear(); GroupShare.ForgetCache(Path.Combine(Paths.BepInExRootPath, "Hearthwoven")); if (Marks != null) Marks = new FellowMarks(); } GroupShare.SendShareState(); SendNow("share-changed"); };   // not sharing: no fellow's copy stays on this PC (B23)
            DevCheck.Bind(Config, Logger);   // Dev.SelfCheck: the in-game self-check, off for players (DevCheck.cs)
            BattleHooks.Bind(Config);        // Battle.DealtAfterArmour (dev, off): read before the hooks are patched
            logRouted = Config.Bind("Server", "LogRoutedDamage", false, "Server: log routed damage (cross-owner hits) to damage-routed-<day>.jsonl, a fallback for players without the mod. Off by default (Joost, 2026-10-09).");
            compressAfterDays = Config.Bind("Server", "CompressLogsAfterDays", 30, "Server: daily logs (chests, births, routed damage, received) older than this many days are gzipped in place (<name>.jsonl.gz), never deleted. 0 = never; at least 2.");
            // Patch each hook on its own: if a future game update breaks one, only that measurement stops.
            var harmony = patcher = new Harmony(Guid);
            int ok = 0, failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0 || StokerHooks.Later(type)) continue;
                try { harmony.CreateClassProcessor(type).Patch(); ok++; }
                catch (Exception e) { failed++; Logger.LogWarning("hook " + type.Name + " skipped: " + (e.InnerException ?? e).Message); }
            }
            HooksOk = ok; HooksFailed = failed;
            Logger.LogInfo($"Hearthwoven {Version} loaded: {ok} hooks active, {failed} skipped");
        }

        /// <summary>
        /// One-time interval migration (0.7, SYNC-DESIGN.md item 3), as PanelUi.MigrateFilterKey: the default went from 5 to 2 minutes. A config at
        /// layout 0 still holding 5 held the old default, not a choice: it moves to 2. Any other value stays, and from layout 1 on everything
        /// stays, so a player who sets 5 again keeps it. Returns the layout and the minutes to store.
        /// </summary>
        public static (int layout, float minutes) MigrateInterval(int layout, float minutes)
        {
            if (layout >= 1) return (layout, minutes);
            return (1, minutes == 5f ? 2f : minutes);
        }

        static Harmony patcher;
        static bool laterPatched;

        void Update()
        {
            if (!laterPatched) { laterPatched = true; StokerHooks.Patch(patcher, Logger); }   // every plugin has loaded by the first frame (another mod's hooks)
            var now = Time.realtimeSinceStartup;
            if (routedWriter != null && now - lastFlush > 10f) { routedWriter.Flush(); lastFlush = now; }
            if (compressReport != null) ReportCompression();
            if (nextSend > 0f && now >= nextJoinWatch)   // who just joined (0.7): the game's player list, read every 2 s (a short list)
            {
                nextJoinWatch = now + 2f;
                try { GroupShare.WatchJoins(); } catch (Exception e) { LogOnce("player list not read: " + e.Message); }
            }
            if (nextSend > 0f && now >= nextVoyage) { ClientHooks.SampleVoyage(10f); ClientHooks.SampleCart(); nextVoyage = now + 10f; Panel.FeatsTracker.Tick(now); }
            if (nextSend > 0f && now >= nextSend) { SaveLocal(); SendNow("interval"); nextSend = now + Mathf.Max(1f, intervalMinutes.Value) * 60f; AskGroup(); }
            if (nextSend > 0f && now >= nextLive)   // live updates every 10 s (0.7): only what grew since the last full copy
            {
                nextLive = now + GroupShare.LiveEvery;
                try { LiveTick(); } catch (Exception e) { liveBusy = false; LogOnce("live update not started: " + e.Message); }
            }
            if (liveDone != null) LiveTake();
            // one fragment every 250 ms, never a burst
            GroupShare.Tick(now);
            if (nextSend > 0f) { try { Deeds.Tick(DateTime.UtcNow, Events, deedGame); } catch (Exception e) { LogOnce("recent deeds not booked: " + e.Message); } }   // Deeds > Recent: books the minute that ended (one comparison per frame); never stops Update (REVIEW-07 #10)
            if (nextSend > 0f) BattleHooks.Tick(now);   // the battle record: fights end, kills settle, once a second (one comparison per frame)
            if (DevCheck.On) { DevCheck.Tick(); DevCheck.PerfTick(); DevCheck.SyncTick(); }   // Dev.SelfCheck: the filter-key watch, the frame cost (one line a minute)
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
                var copyId = SessionId + "-" + (++copyCount);
                var json = BuildJson(player, profile, copyId);
                if (host)
                {
                    GroupShare.StoreOwn(profile.GetPlayerID(), json);   // the players who join see you; share off removes the copy
                    SetLiveBase(json, copyId);   // stored here: the base of the live updates (a logout that is cancelled keeps them in step)
                    if (check) DevCheck.Final("INFO", reason, "kept on this PC: you host the world, so your copy is in the group here");
                    return;
                }
                var packedCopy = Transport.Pack(json);
                if (DevCheck.On) DevCheck.SyncOut(false, packedCopy.Length);
                var parts = Fragments.Split(packedCopy);
                var msgId = SessionId + "-" + DateTime.UtcNow.Ticks;
                Outbox.Clear();   // a newer snapshot replaces an unsent older one
                lastFullSend = Time.realtimeSinceStartup;
                for (int i = 0; i < parts.Count; i++)
                {
                    var pkg = new ZPackage();
                    pkg.Write(reason); pkg.Write(msgId); pkg.Write(i); pkg.Write(parts.Count); pkg.Write(parts[i]);
                    Outbox.Enqueue(pkg);
                }
                if (!final) SetLiveBase(json, copyId);   // queued: the base of the live updates that follow (0.7)
                nextSend = NotInsideGap(nextSend, lastFullSend);   // the next interval copy never lands inside the server's 20 s gate (it would be refused)
                if (final)
                {
                    int queued = Outbox.Count, sent = 0;
                    if (queued <= MaxFinalParts)
                        while (Outbox.Count > 0 && clock.ElapsedMilliseconds < FinalBudgetMs) { ZRoutedRpc.instance.InvokeRoutedRPC(RpcName, Outbox.Dequeue()); sent++; }
                    Outbox.Clear();   // whatever is left could not go out in time, and would be gone with the connection
                    lastFinal = Time.realtimeSinceStartup;
                    if (sent == queued) SetLiveBase(json, copyId);   // it all went out: a cancelled logout plays on with the server's base
                    if (sent == queued) log?.LogInfo($"final snapshot sent ({reason}): {sent} parts, {json.Length} bytes of JSON, {clock.ElapsedMilliseconds} ms");
                    else log?.LogWarning($"final snapshot ({reason}) not sent in full: {sent} of {queued} parts in {clock.ElapsedMilliseconds} ms (never delays leaving the game)");
                    if (check) DevCheck.Final(sent == queued ? "PASS" : "FAIL", reason, $"sent {sent} of {queued} parts, {json.Length} bytes of JSON, in {clock.ElapsedMilliseconds} ms");
                }
            }
            catch (Exception e) { log?.LogWarning((IsFinal(reason) ? "final send failed: " : "send failed: ") + e.Message); }
        }

        static void SetLiveBase(string json, string copyId) { liveBase = GroupShare.SharedCopy(json); liveBaseId = copyId; liveSeq = 0; }

        /// <summary>After any full copy (a share change, an anchor), the next one is never due within the server's RateGate gap of it: a refused
        /// interval copy would leave the server on the old base and every live update dropped until the interval after (0.7 review).</summary>
        public static float NotInsideGap(float next, float lastSend) => next > 0f ? Math.Max(next, lastSend + MinSendGap) : next;

        /// <summary>The snapshot as it stands now (the full copy, and each live update's "now" with the base's copyId).</summary>
        static string BuildJson(Player player, PlayerProfile profile, string copyId)
        {
            var skills = player.GetSkills()?.GetSkillList()?.Select(s => new Snapshot.SkillInfo
                { Name = s.m_info.m_skill.ToString(), Level = s.m_level, Accumulator = s.m_accumulator }) ?? Enumerable.Empty<Snapshot.SkillInfo>();
            return Snapshot.Build(Version, profile.GetPlayerID(), profile.GetName(), profile.m_playerStats, skills,
                                  ZNet.instance.GetWorldName(), Session, SessionId, Events, Log, shareWithGroup.Value,
                                  EventsSinceInstall, DamageSinceInstall, BiomeSinceInstall, BiomeFromUtc, FeatsLedger,
                                  Totals?.History.DealtByDay(DateTime.Now, 30, PendingDay()),   // your damage dealt per day (HISTORY-06: under 1 KB), for fellows' Together
                                  Panel.PanelUi.KnownBiomes(player),   // the biomes you found: the group feats wait for a land until someone who shares found it
                                  copyId, FoeShareNow());   // 0.8: the foes you fought per kind and what became of them (the feed stays here)
        }

        /// <summary>
        /// Every 10 s while playing (0.7, SYNC-DESIGN.md item 4): only while you share, SendStats is on, this server keeps live updates and a full
        /// copy went out this connection (the base). Builds the snapshot here (game state), then diffs it against the base on a worker thread.
        /// </summary>
        static void LiveTick()
        {
            if (liveBusy || liveBaseId == null || !shareWithGroup.Value || !sendStats.Value || !GroupShare.ServerLive || Panel.SampleMode.On) return;
            if (Outbox.Count > 0) return;   // the full copy is still going out: an update on it would reach the server first
            if (ZNet.instance == null || ZRoutedRpc.instance == null || (ZNet.instance.IsServer() && !GroupShare.IsHost())) return;
            var player = Player.m_localPlayer; var profile = Game.instance?.GetPlayerProfile();
            if (player == null || profile == null) return;
            var json = BuildJson(player, profile, liveBaseId);
            string baseText = liveBase, id = liveBaseId; long seq = liveSeq + 1;
            liveBusy = true;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                LiveDelta.Made made;
                try { made = LiveDelta.Make(baseText, GroupShare.SharedCopy(json), id, seq); }
                catch (Exception e) { made = new LiveDelta.Made { Error = e.Message }; }
                liveDone = new LiveJob { Id = id, Seq = seq, Made = made };
            });
        }

        /// <summary>The worker's result, on the main thread: sent (or kept, when you host); too big: a full copy instead, the new base.</summary>
        static void LiveTake()
        {
            var done = liveDone; liveDone = null; liveBusy = false;
            try
            {
                if (done == null || done.Id != liveBaseId) return;   // a full copy went out meanwhile: this update was for the one before
                var made = done.Made;
                if (made.Error != null) { LogOnce("live update not made: " + made.Error); return; }
                if (made.Empty) return;   // nothing grew
                if (!shareWithGroup.Value || !sendStats.Value) return;   // switched off while the worker was busy: nothing leaves this PC
                if (made.TooBig) { if (Time.realtimeSinceStartup - lastFullSend >= MinSendGap) SendNow("anchor"); return; }
                if (ZNet.instance == null || ZRoutedRpc.instance == null) return;
                liveSeq = done.Seq;
                if (GroupShare.IsHost())
                {
                    var profile = Game.instance?.GetPlayerProfile();
                    if (profile != null) GroupShare.LiveOwn(profile.GetPlayerID(), done.Id, done.Seq, made.Packed);
                }
                else
                {
                    var pkg = new ZPackage(); pkg.Write(LiveDelta.Version); pkg.Write(made.Packed);
                    ZRoutedRpc.instance.InvokeRoutedRPC(GroupShare.LiveRpc, pkg);
                }
                if (DevCheck.On) DevCheck.SyncOut(true, made.Packed.Length);
            }
            catch (Exception e) { LogOnce("live update not sent: " + e.Message); }
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
                Totals = LocalTotals.LoadFor(Path.Combine(Paths.BepInExRootPath, "Hearthwoven"), id, name, gameNow, () => OtherCharacters(profile, id), () => OtherNames(profile, id), out totalsPath, out var problem);
                if (problem != null) LogOnce(problem);
                if (Totals != null && Totals.Armour.FromUtc == DateTime.MinValue) Totals.Armour.FromUtc = DateTime.UtcNow;   // the armour ledger records from the first run of a version that has it
                if (Totals != null && Totals.Foes.FromUtc == DateTime.MinValue) Totals.Foes.FromUtc = DateTime.UtcNow;     // the foes too (0.8)
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
                if (Totals.FillStarts(DateTime.UtcNow)) took = true;   // the counter groups' start dates (schema 4 "starts"), once
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

        /// <summary>The names of every OTHER character on this PC with the same player id (0.8: a renamed character adopts its earlier file
        /// only when no character of that name is still here). Asked only when such a file exists; null = could not be listed.</summary>
        static System.Collections.Generic.List<string> OtherNames(PlayerProfile current, long id)
        {
            try
            {
                var list = new System.Collections.Generic.List<string>();
                foreach (var p in SaveSystem.GetAllPlayerProfiles())
                    if (p != null && p.GetPlayerID() == id && p.GetFilename() != current.GetFilename()) list.Add(p.GetName() ?? "");
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
            try
            {
                if (Totals != null && totalsPath != null)
                {
                    Totals.Record(SessionId, Session, Events, Log, GameStatsNow(), DateTime.Now);
                    // REVIEW-08 #12: each side ledger in its own try, so a throw in one never skips the whole local-totals save
                    try { Totals.Armour.Record(SessionId, Armour, DateTime.Now); } catch (Exception e) { LogOnce("armour ledger not recorded: " + e.Message); }
                    try { Totals.Foes.Record(SessionId, Battle.Counts(null), DateTime.Now); } catch (Exception e) { LogOnce("foes ledger not recorded: " + e.Message); }
                    Totals.Save(totalsPath, DateTime.UtcNow);
                }
            }
            catch (Exception e) { log?.LogWarning("local totals not saved: " + e.Message); }
            try { SaveMarks(); } catch (Exception e) { LogOnce("fellow marks not saved: " + e.Message); }
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
                GroupShare.Clear(); nextSend = -1f; lastFullSend = -100f; FeatsCounters.Streak = 0;
                liveBase = liveBaseId = null; liveSeq = 0; liveBusy = false; liveDone = null;   // live updates start from this connection's first full copy
                Armour = new ArmourTally(); ArmourMinutes = new ArmourLog();   // the armour ledger starts over with the session too
                Battle = new BattleRecorder(); DealtArmour = new ArmourTally(); DealtArmourMinutes = new ArmourLog();   // the battle record too (0.8)
                Deeds = new DeedLog(); PreviousSessionEndUtc = null; Marks = null; marksPath = null;   // Deeds > Recent: per session; set again on spawn (RecentOnSpawn)
            });
        }

        [HarmonyPatch(typeof(Player), "OnSpawned")]
        static class OnSpawn
        {
            static void Postfix(Player __instance) => HookGuard.Run(() =>
            {
                if (__instance != Player.m_localPlayer) return;
                LoadLocal();                                   // since-install totals of this character (P7)
                try { RecentOnSpawn(); } catch (Exception e) { LogOnce("recent (since last time) not loaded: " + e.Message); }
                nextSend = FirstSendAt(Time.realtimeSinceStartup, lastFullSend);   // first snapshot 10 s after spawning (0.7; was 30 s)
                GroupShare.LoadCached(Path.Combine(Paths.BepInExRootPath, "Hearthwoven"));   // B23: the fellows as they last shared them, until the server's fresh copies come
                AskGroup();                                    // fellows' copies for Recent and Since you were away, the book open or not
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
