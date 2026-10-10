using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using TMPro;
using Unity.Profiling;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Developer tool (0.8 performance pass), off unless Dev.Bench is true: what the panel really costs in the game. On the bench key
    /// (Dev.BenchKey, F4) or a file named "bench-request" in BepInEx/Hearthwoven/local/ (looked for once a second; a number in it is the
    /// rounds), the panel opens if it is shut (that opening is timed: the first one in a game builds the book and runs its code for the
    /// first time) and:
    /// 1. flips through every chapter, page and view Dev.BenchRounds times. Round 1 finds the pages as it draws them (a chapter's list, a
    ///    page's views), so each page's first visit is measured as one; the later rounds flip through the same list and are the steady
    ///    state. Per page it times a rebuild in its phases (gather the input, build the model, the JSON check, the UI objects, then the
    ///    canvas and text meshes: the Unity side), the bytes allocated, whether a garbage collection ran inside it, the game's log lines
    ///    written meanwhile, the frame after it and the panel's late-frame work after it (the plate measured again), and a refresh with
    ///    nothing new (what the open book does every 2 s);
    /// 2. times the gather in its parts (what every refresh pays before the model), each part 20 times;
    /// 3. sits idle for IdleSeconds on the page you had open, measuring the panel's time and bytes per frame (the target: 0 B with the
    ///    book open and idle) and the game's own frames (frame time; the game's managed allocations per frame where Unity reports them);
    /// 4. writes BepInEx/Hearthwoven/local/bench-&lt;version&gt;.json (per page: all rounds as before, then "first" and "steady" apart; the
    ///    opening, the gather parts, the idle numbers, the worst game frame, the collections, the log lines) and logs one line, then returns
    ///    to the page you had open, or closes the book again.
    /// For a cold first visit of every page, press the key soon after loading the world, before opening the book. Reads only; the panel's
    /// own keys wait while it flips (as for the self-check walk). Hooked into PanelUi in four places: BindConfig, the top of Frame
    /// (BenchTick), the measured Render (PanelUi.cs) and LateUpdate (BenchLate).
    /// </summary>
    public partial class PanelUi
    {
        internal static ConfigEntry<bool> Bench;
        internal static ConfigEntry<KeyCode> BenchKey;
        internal static ConfigEntry<int> BenchRounds;
        const float IdleSeconds = 8f;
        const int GatherPartRuns = 20;
        static string BenchDir => Path.Combine(Paths.BepInExRootPath, Path.Combine("Hearthwoven", "local"));

        static void BindBenchConfig(ConfigFile config)
        {
            Bench = config.Bind("Dev", "Bench", false, "Developer tool: on the bench key the panel flips through every chapter, page and view a few times, then sits idle for a few seconds, and writes what each page cost (rebuild and refresh times p50/p95/max, its first visit apart from the later ones, bytes allocated, the worst game frame) to BepInEx/Hearthwoven/local/bench-<version>.json. Reads only. Off for players: with it off the key does nothing.");
            BenchKey = config.Bind("Dev", "BenchKey", KeyCode.F4, "Key that starts the bench (only with Bench on). F4: F1 and F5 to F10 belong to the console and the group's other mods, F11 to the panel snapshots, F12 to the self-check.");
            BenchRounds = config.Bind("Dev", "BenchRounds", 5, new ConfigDescription("How many times the bench flips through every page (more: steadier numbers, a longer run; two frames per page per round).", new AcceptableValueRange<int>(1, 30)));
        }

        static double Ms(long ticks) => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        // ---------- the measured render (PanelUi.Render calls in here while measuring) ----------

        /// <summary>The marks of one measured Render: 0 start, 1 input gathered, 2 model built, 3 JSON compared, 4 UI objects made, 5 canvas updated;
        /// the collections and log lines while it ran.</summary>
        sealed class RenderMarks
        {
            public readonly long[] T = new long[6], B = new long[6];
            public readonly long[] F = new long[3];   // inside Fill (0.8.1): the chrome done, the page's blocks made, the plate fitted
            public bool Drew; public int Gc, GcEnd, Logs, TextsMade, TextsReused, ImagesMade, ImagesReused;
            public bool GcHit => GcEnd != Gc;
            public void Mark(int i) { T[i] = PerfMeter.Now; B[i] = AllocClock.Now(); }
            public long Ticks(int from, int to) => T[from] == 0 || T[to] == 0 ? 0 : Math.Max(0, T[to] - T[from]);
            public int End => Drew && T[5] != 0 ? 5 : Drew ? 4 : 3;
            /// <summary>Fill's parts (ms): 0 the chrome (heading row, player row, tabs, list), 1 the page's blocks, 2 the plate fit; -1 not marked.</summary>
            public double FillMs(int part)
            {
                var from = part == 0 ? T[3] : F[part - 1]; var to = F[part];
                return from == 0 || to == 0 ? -1 : Math.Max(0, to - from) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            }
            /// <summary>Bytes between two marks; -1 when the runtime cannot tell (the heap fallback saw a collection, or a mark is missing).</summary>
            public long Bytes(int from, int to)
            {
                if (T[from] == 0 || T[to] == 0) return -1;
                if (!AllocClock.Exact && GcHit) return -1;
                return Math.Max(0, B[to] - B[from]);
            }
        }
        RenderMarks marks;                 // set while a measured Render runs; Draw marks its phases into it
        RenderMarks logsInto;              // the measured Render whose log lines are being counted (its whole span, the canvas too)
        Action<RenderMarks> benchRender;   // the bench's page while it flips (null otherwise)
        bool benchRunning, benchIdle, benchFlipping;
        long benchLateTicks;               // the panel's LateUpdate work since the last page was drawn (while the bench flips)
        readonly PerfMeter benchMeter = new PerfMeter();
        readonly Dictionary<string, int> benchLogTally = new Dictionary<string, int>();
        float nextBenchCheck;

        // the meter of this frame's panel work: the bench's while it sits idle, the self-check's otherwise (never during a page walk)
        PerfMeter FrameMeter() => benchIdle ? benchMeter : DevCheck.On && !snapping ? DevCheck.Perf : null;

        void MarkPhase(int i) { if (marks != null) marks.Mark(i); }
        void MarkFill(int i) { if (marks != null) marks.F[i] = PerfMeter.Now; }

        void MeasuredRender(bool force, PerfMeter meter, Action<RenderMarks> bench)
        {
            var m = marks = new RenderMarks { Gc = AllocClock.Collections(), TextsMade = -TextsMade, TextsReused = -TextsReused, ImagesMade = -ImagesMade, ImagesReused = -ImagesReused };
            if (bench != null) logsInto = m;
            m.Mark(0);
            try { Draw(force); }
            finally
            {
                marks = null;
                try
                {
                    if (m.Drew && m.T[4] != 0) { Canvas.ForceUpdateCanvases(); m.Mark(5); }   // the canvas and text meshes the new page needs, now instead of later this frame
                    else { if (m.T[3] == 0) m.Mark(3); }
                    m.GcEnd = AllocClock.Collections(); logsInto = null; m.TextsMade += TextsMade; m.TextsReused += TextsReused; m.ImagesMade += ImagesMade; m.ImagesReused += ImagesReused;
                    var end = m.End;
                    if (meter != null)
                    {
                        meter.AddRefresh(m.Ticks(0, end), m.Bytes(0, end));
                        if (m.Drew) meter.AddRebuild(m.Ticks(0, 1), m.Ticks(1, 2), m.Ticks(2, 3), m.Ticks(3, 4), m.Ticks(4, 5));
                    }
                    bench?.Invoke(m);
                }
                catch (Exception e) { logsInto = null; Debug.LogWarning("[Hearthwoven] bench measure: " + e.Message); }
            }
        }

        // the game's log while a measured page is drawn: how many lines, and which (the first run's TMP font warning came once per label)
        void BenchLog(string message, string stack, LogType type)
        {
            var m = logsInto; if (m == null) return;
            m.Logs++;
            var key = type + ": " + (message == null ? "" : message.Length > 110 ? message.Substring(0, 110) : message);
            benchLogTally.TryGetValue(key, out var n); benchLogTally[key] = n + 1;
        }

        /// <summary>From LateUpdate: while the bench flips, the panel's late-frame work (the plate measured again after a draw) is timed for
        /// the page just drawn; false otherwise (LateUpdate runs it as usual).</summary>
        bool BenchLate()
        {
            if (!benchFlipping) return false;
            var t = PerfMeter.Now;
            try { LateFrame(); } finally { benchLateTicks += PerfMeter.Now - t; }
            return true;
        }

        // ---------- the trigger (top of Frame) ----------

        /// <summary>Starts a bench run on the key or the request file; false always (the run owns the panel through `snapping` while it flips).</summary>
        bool BenchTick()
        {
            if (Bench == null || !Bench.Value) return false;
            if (benchRunning && !snapping && !benchIdle) { benchRunning = false; benchFlipping = false; Application.logMessageReceived -= BenchLog; }   // a run the watchdog stopped (PanelSnapshot.SnapshotTick)
            if (benchRunning) return false;
            try
            {
                string trigger = null; int rounds = BenchRounds.Value;
                if (BenchKey.Value != KeyCode.None && !Typing() && Key(BenchKey.Value)) trigger = "key";
                else if (Time.unscaledTime >= nextBenchCheck)
                {
                    nextBenchCheck = Time.unscaledTime + 1f;
                    var request = Path.Combine(BenchDir, "bench-request");
                    if (!File.Exists(request) || !CanSnap() || snapping) return false;
                    var text = File.ReadAllText(request).Trim();
                    File.Delete(request);
                    if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= 30) rounds = n;
                    trigger = "file";
                }
                if (trigger == null) return false;
                if (snapping) { Debug.Log("[Hearthwoven] bench: not now (a snapshot or self-check run is on)"); return false; }
                if (!CanSnap()) { Debug.Log("[Hearthwoven] bench: not now (the panel cannot open here)"); return false; }
                benchRunning = true; snapping = true; snapProgress = Time.unscaledTime;
                snapRun = StartCoroutine(BenchRun(trigger, rounds));
            }
            catch (Exception e) { benchRunning = false; snapping = false; Debug.LogWarning("[Hearthwoven] bench not started: " + e.Message); }
            return false;
        }

        // ---------- the run ----------

        /// <summary>One measured visit of a page: a rebuild's phases (ms), its bytes (-1 unknown), the frame after it, the late-frame work after it.</summary>
        sealed class Visit
        {
            public double Rebuild, Gather, Model, JsonCheck, Ui, Canvas, FrameAfter, Late, Chrome = -1, Blocks = -1, Fit = -1; public long Bytes = -1; public int Logs; public bool Gc, Drew;
            public Json Write(Json j, string key)
            {
                j.Key(key).Open().Num("rebuildMs", Rebuild).Num("gatherMs", Gather).Num("modelMs", Model).Num("jsonMs", JsonCheck).Num("uiMs", Ui).Num("canvasMs", Canvas)
                 .Num("uiChromeMs", Chrome).Num("uiBlocksMs", Blocks).Num("uiFitMs", Fit)
                 .Num("frameAfterMs", FrameAfter).Num("lateMs", Late).Num("rebuildBytes", Bytes).Num("logLines", Logs).Num("gc", Gc ? 1 : 0).Num("drew", Drew ? 1 : 0);
                return j.Close();
            }
        }

        sealed class BenchPage
        {
            public string Name, Chapter, Page, View; public int Objects = -1, Texts = -1, RefreshDrew, GcHits;
            public readonly BenchSeries Rebuild = new BenchSeries(), Gather = new BenchSeries(), Model = new BenchSeries(), JsonCheck = new BenchSeries(), Ui = new BenchSeries(), CanvasMs = new BenchSeries(),
                                        RebuildBytes = new BenchSeries(), Refresh = new BenchSeries(), RefreshBytes = new BenchSeries(), FrameAfter = new BenchSeries(), Late = new BenchSeries(), Logs = new BenchSeries(),
                                        UiChrome = new BenchSeries(), UiBlocks = new BenchSeries(), UiFit = new BenchSeries(),   // 0.8.1: uiMs in Fill's parts
                                        TextsMade = new BenchSeries(), TextsReused = new BenchSeries(), ImagesMade = new BenchSeries(), ImagesReused = new BenchSeries();   // made new, reused (Dev.UiReuse)
            // the steady state: the rounds after the first, without the ones a garbage collection ran into
            public readonly BenchSeries SteadyRebuild = new BenchSeries(), SteadyGather = new BenchSeries(), SteadyModel = new BenchSeries(), SteadyUi = new BenchSeries(), SteadyCanvas = new BenchSeries(), SteadyFrameAfter = new BenchSeries(), SteadyLate = new BenchSeries(),
                                        SteadyChrome = new BenchSeries(), SteadyBlocks = new BenchSeries(), SteadyFit = new BenchSeries();
            public Visit First, last;

            public void AddRebuild(RenderMarks m)
            {
                var end = m.End;
                var v = last = new Visit { Rebuild = Ms(m.Ticks(0, end)), Gather = Ms(m.Ticks(0, 1)), Model = Ms(m.Ticks(1, 2)), JsonCheck = Ms(m.Ticks(2, 3)), Logs = m.Logs, Gc = m.GcHit, Drew = m.Drew };
                Rebuild.Add(v.Rebuild); Gather.Add(v.Gather); Model.Add(v.Model); JsonCheck.Add(v.JsonCheck); Logs.Add(m.Logs);
                if (m.Drew)
                {
                    v.Ui = Ms(m.Ticks(3, 4)); v.Canvas = Ms(m.Ticks(4, 5)); Ui.Add(v.Ui); CanvasMs.Add(v.Canvas);
                    v.Chrome = m.FillMs(0); v.Blocks = m.FillMs(1); v.Fit = m.FillMs(2);
                    if (v.Chrome >= 0) UiChrome.Add(v.Chrome); if (v.Blocks >= 0) UiBlocks.Add(v.Blocks); if (v.Fit >= 0) UiFit.Add(v.Fit);
                    TextsMade.Add(m.TextsMade); TextsReused.Add(m.TextsReused); ImagesMade.Add(m.ImagesMade); ImagesReused.Add(m.ImagesReused);
                }
                v.Bytes = m.Bytes(0, end); if (v.Bytes >= 0) RebuildBytes.Add(v.Bytes);
                if (m.GcHit) GcHits++;
            }
            /// <summary>The frame after the rebuild and the late-frame work after it: the visit is complete (round 1 keeps it as the first visit).</summary>
            public void Settle(bool firstRound, double frameAfter, double late)
            {
                FrameAfter.Add(frameAfter); Late.Add(late);
                var v = last; last = null; if (v == null) return;
                v.FrameAfter = frameAfter; v.Late = late;
                if (firstRound) { if (First == null) First = v; return; }
                if (v.Gc) return;
                SteadyRebuild.Add(v.Rebuild); SteadyGather.Add(v.Gather); SteadyModel.Add(v.Model); SteadyFrameAfter.Add(frameAfter); SteadyLate.Add(late);
                if (v.Drew) { SteadyUi.Add(v.Ui); SteadyCanvas.Add(v.Canvas); if (v.Chrome >= 0) SteadyChrome.Add(v.Chrome); if (v.Blocks >= 0) SteadyBlocks.Add(v.Blocks); if (v.Fit >= 0) SteadyFit.Add(v.Fit); }
            }
            public void AddRefresh(RenderMarks m)
            {
                var end = m.End;
                Refresh.Add(m.Ticks(0, end) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                var b = m.Bytes(0, end); if (b >= 0) RefreshBytes.Add(b);
                if (m.Drew) RefreshDrew++;
            }
        }

        IEnumerator BenchRun(string trigger, int rounds)
        {
            var started = DateTime.Now; var clock = System.Diagnostics.Stopwatch.StartNew();
            string failure = null;
            var pages = new List<BenchPage>(); var byName = new Dictionary<string, BenchPage>();
            var gameFrames = new BenchSeries(); var gameAlloc = new BenchSeries(); float worst = 0f; string worstWhere = "";
            int gcStart = AllocClock.Collections(); int flips = 0;
            ProfilerRecorder gcInFrame = default; bool recorder = false;
            try { gcInFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"); recorder = gcInFrame.Valid; } catch { recorder = false; }
            benchLogTally.Clear();
            Application.logMessageReceived -= BenchLog; Application.logMessageReceived += BenchLog;
            bool everyone = state.Everyone, cold = !root, opened = false; double openMs = -1, openFrameMs = -1;
            string warm = PanelWarm.Line();
            try
            {
                snapSaved = SaveState();
                if (!open) { var t0 = PerfMeter.Now; Open(); openMs = Ms(PerfMeter.Now - t0); opened = true; }   // cold: the first opening in this game (the book built, its code run the first time)
                Debug.Log("[Hearthwoven] bench: started (" + trigger + "), " + rounds + " rounds; the panel's keys wait until it is done");
            }
            catch (Exception e) { failure = "start: " + e.Message; }
            yield return null;
            if (opened) openFrameMs = Time.unscaledDeltaTime * 1000.0;   // the frame that opened the book
            yield return null;

            // 1. every page, rounds times: a rebuild (Apply = Render(true)), the frame after it, a refresh with nothing new. Round 1 finds the
            // pages as it draws them: each chapter as it opens (its list), then each page as it stands (its views); later rounds take its list.
            var plan = new List<BenchShot>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var done = new HashSet<string>();
            var todo = PanelBenchWalk.Start(names);   // every chapter as it opens, then About (PanelBenchWalk.cs)
            RenderMarks drawnMarks = null;
            benchFlipping = true;
            for (int round = 0; failure == null && round < rounds; round++)
            {
                var first = round == 0;
                var walk = first ? todo : plan;
                for (int i = 0; failure == null && i < walk.Count; i++)
                {
                    var shot = walk[i];
                    snapProgress = Time.unscaledTime;
                    if (!open || !Player.m_localPlayer || Player.m_localPlayer.IsDead()) { failure = "the panel closed or the player died"; break; }
                    BenchPage page = null;
                    try
                    {
                        drawnMarks = null; benchRender = m => drawnMarks = m;
                        PanelBenchWalk.Apply(shot, state); Render(true); flips++;   // as the page walk's Apply
                        if (first)
                        {
                            if (view != null) shot = PanelBenchWalk.Learn(shot, view, todo, i, names, done);
                            if (shot.Name == null) shot.Name = Unique(names, Safe(shot.Chapter.ToString()) + (shot.Page != null ? "-" + Safe(shot.Page) : ""));
                            plan.Add(shot);
                        }
                        if (!byName.TryGetValue(shot.Name, out page))
                        {
                            page = new BenchPage { Name = shot.Name, Chapter = shot.About ? "About" : shot.Chapter.ToString(), Page = shot.Page ?? "", View = shot.View ?? "" };
                            byName[shot.Name] = page; pages.Add(page);
                        }
                        if (drawnMarks != null) page.AddRebuild(drawnMarks);
                    }
                    catch (Exception e) { failure = shot.Name + ": " + e.Message; }
                    finally { benchRender = null; benchLateTicks = 0; }
                    if (failure != null) break;
                    yield return null;   // the frame that draws the new page
                    var dt = Time.unscaledDeltaTime; var late = Ms(benchLateTicks);
                    if (dt > worst) { worst = dt; worstWhere = "after rebuilding " + shot.Name + (first ? " (its first visit)" : ""); }
                    try
                    {
                        if (page.Objects < 0 && root) { page.Objects = root.GetComponentsInChildren<RectTransform>(false).Length; page.Texts = root.GetComponentsInChildren<TMP_Text>(false).Length; }   // not timed
                        benchRender = page.AddRefresh; Render(false);   // what the open book does every 2 s: the same page, nothing new
                    }
                    catch (Exception e) { failure = shot.Name + " (refresh): " + e.Message; }
                    finally { benchRender = null; }
                    if (failure != null) break;
                    yield return null;
                    page.Settle(first, dt * 1000.0, late + Ms(benchLateTicks));   // the late work of both frames after the drawing (the plate is measured again over three)
                    dt = Time.unscaledDeltaTime; if (dt > worst) { worst = dt; worstWhere = "after refreshing " + shot.Name; }
                }
            }
            benchFlipping = false; benchLateTicks = 0;

            // 2. the gather in its parts: what every refresh and rebuild pay before the model, each part GatherPartRuns times
            var parts = new List<KeyValuePair<string, BenchSeries>>();
            if (failure == null && !SampleMode.On)
            {
                try
                {
                    void Part(string name, Action a)
                    {
                        var s = new BenchSeries();
                        for (int i = 0; i < GatherPartRuns; i++) { var t = PerfMeter.Now; a(); s.Add(Ms(PerfMeter.Now - t)); }
                        parts.Add(new KeyValuePair<string, BenchSeries>(name, s));
                    }
                    var self = Gather(); self.Fellows = FellowsOf(self);
                    Part("gather (all of PanelUi.Gather)", () => Gather());
                    Part("Plugin.PendingDay (today's live row)", () => Plugin.PendingDay());
                    Part("Plugin.DeedsNow (Recent's minute booked)", () => Plugin.DeedsNow());
                    Part("Plugin.EventsSinceInstall", () => { _ = Plugin.EventsSinceInstall; });
                    Part("Plugin.DamageSinceInstall + BiomeSinceInstall", () => { _ = Plugin.DamageSinceInstall; _ = Plugin.BiomeSinceInstall; });
                    Part("armour (ArmourSince + ArmourPending)", () => { _ = Plugin.ArmourSince; _ = Plugin.ArmourPending(); });
                    Part("BattleHooks.Into (the battle record)", () => BattleHooks.Into(new PanelInput()));
                    Part("KnownBiomes", () => KnownBiomes(Player.m_localPlayer));
                    Part("fellows (FellowsOf: their copies as inputs)", () => FellowsOf(self));
                    Part("feats noted (FeatsTracker.Note, DrawPage)", () => FeatsTracker.Note(self));
                }
                catch (Exception e) { parts.Add(new KeyValuePair<string, BenchSeries>("stopped: " + e.Message, new BenchSeries())); }
                yield return null;
            }

            // 3. idle on the page you had open: the panel's own per-frame cost with nothing to do
            string idleLine = null; int idleFrames = 0; bool overtaken = false;
            if (failure == null)
            {
                try
                {
                    var s = snapSaved;
                    state.Chapter = s.Chapter; state.Player = s.Player; state.TheyReceived = s.They; state.ShowAbout = s.About;
                    state.Page.Clear(); foreach (var kv in s.Page) state.Page[kv.Key] = kv.Value;
                    state.View.Clear(); foreach (var kv in s.View) state.View[kv.Key] = kv.Value;
                    Render(true);
                }
                catch (Exception e) { failure = "idle: " + e.Message; }
                yield return null;
                yield return null;   // the page drawn and settled (the plate is measured again over three frames)
                yield return null;
                yield return null;
                benchMeter.Reset(AllocClock.Collections());
                benchIdle = true; snapping = false;   // Frame runs as it does for a player: keys, the 2 s refresh, LateUpdate
                var until = Time.unscaledTime + IdleSeconds;
                while (failure == null && Time.unscaledTime < until)
                {
                    yield return null;
                    if (snapping) { overtaken = true; failure = "a snapshot or self-check run started while the bench sat idle"; break; }   // that run owns the panel now
                    if (!open) { failure = "the panel was closed while the bench sat idle"; break; }
                    var dt = Time.unscaledDeltaTime;
                    benchMeter.EndFrame(dt, true, AllocClock.Collections()); idleFrames++;
                    gameFrames.Add(dt * 1000.0); if (dt > worst) { worst = dt; worstWhere = "idle"; }
                    if (recorder) { try { gameAlloc.Add(gcInFrame.LastValue); } catch { recorder = false; } }
                }
                benchIdle = false; if (!overtaken) snapping = true;
                try { idleLine = benchMeter.Line(); } catch { }
            }
            try { if (gcInFrame.Valid) gcInFrame.Dispose(); } catch { }
            Application.logMessageReceived -= BenchLog;

            // 4. the file
            string path = null;
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                string F(double v) => v.ToString("0.00", inv);
                bool incremental = false; double sliceMs = -1; string tmpFont = "unknown";
                try { incremental = UnityEngine.Scripting.GarbageCollector.isIncremental; sliceMs = UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds / 1e6; } catch { }
                try { tmpFont = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset.name : "none (a text made without a font logs a warning)"; } catch { tmpFont = "no TMP settings"; }
                var j = new Json().Open().Str("version", Plugin.Version).Str("time", started.ToString("yyyy-MM-dd'T'HH:mm:ss")).Str("trigger", trigger).Num("rounds", rounds)
                    .Num("sampleData", SampleMode.On ? 1 : 0).Num("screenWidth", Screen.width).Num("screenHeight", Screen.height).Num("panelScale", Scale.Value)
                    .Str("allocSource", AllocClock.Source).Num("allocExact", AllocClock.Exact ? 1 : 0).Str("failure", failure ?? "")
                    .Num("everyone", everyone ? 1 : 0).Num("uiReuse", ReuseOn ? 1 : 0).Str("unity", Application.unityVersion).Num("gcIncremental", incremental ? 1 : 0).Num("gcSliceMs", sliceMs).Str("tmpDefaultFont", tmpFont)
                    .Str("warmBefore", warm).Str("warmNow", PanelWarm.Line())
                    .Str("what", "per page and round: rebuild = Render(true) (gather the input, build the model, the JSON check, UI = the objects made, canvas = layout and text meshes after it), refresh = Render(false) with nothing new (every 2 s while open), frameAfter = the game frame that drew it, late = the panel's LateUpdate work in the two frames after it; first = the page's first visit (round 1, found while drawing), steady = rounds 2 and later without the ones a garbage collection ran into; ms, bytes");
                j.Key("open").Open().Num("benchOpened", opened ? 1 : 0).Num("cold", cold && opened ? 1 : 0).Num("ms", openMs).Num("frameMs", openFrameMs)
                 .Str("what", "the bench's own opening of the book (Open: built on the first opening in this game, then the page you had drawn), and the frame it fell in; cold = the first opening since the game started").Close();
                j.Key("pages").OpenArr();
                var rebuildP95 = new BenchSeries(); var refreshP95 = new BenchSeries(); var uiP95 = new BenchSeries();
                var firstRebuild = new BenchSeries(); var steadyP50 = new BenchSeries(); var steadyP95 = new BenchSeries(); var firstExtra = new BenchSeries(); var lateP50 = new BenchSeries(); var logsPer = new BenchSeries();
                double worstFirst = 0; string worstFirstPage = ""; int gcHits = 0;
                foreach (var p in pages)
                {
                    j.Open().Str("name", p.Name).Str("chapter", p.Chapter).Str("page", p.Page).Str("view", p.View).Num("objects", p.Objects).Num("texts", p.Texts);
                    p.Rebuild.Write(j, "rebuildMs"); p.Gather.Write(j, "gatherMs"); p.Model.Write(j, "modelMs"); p.JsonCheck.Write(j, "jsonMs"); p.Ui.Write(j, "uiMs"); p.CanvasMs.Write(j, "canvasMs");
                    p.RebuildBytes.Write(j, "rebuildBytes"); p.Refresh.Write(j, "refreshMs"); p.RefreshBytes.Write(j, "refreshBytes"); j.Num("refreshDrew", p.RefreshDrew); p.FrameAfter.Write(j, "frameAfterMs");
                    p.UiChrome.Write(j, "uiChromeMs"); p.UiBlocks.Write(j, "uiBlocksMs"); p.UiFit.Write(j, "uiFitMs"); p.TextsMade.Write(j, "textsMade"); p.TextsReused.Write(j, "textsReused"); p.ImagesMade.Write(j, "imagesMade"); p.ImagesReused.Write(j, "imagesReused");
                    p.Late.Write(j, "lateMs"); p.Logs.Write(j, "logLines"); j.Num("gcHits", p.GcHits);
                    if (p.First != null) p.First.Write(j, "first");
                    j.Key("steady").Open(); p.SteadyRebuild.Write(j, "rebuildMs"); p.SteadyGather.Write(j, "gatherMs"); p.SteadyModel.Write(j, "modelMs"); p.SteadyUi.Write(j, "uiMs");
                    p.SteadyChrome.Write(j, "uiChromeMs"); p.SteadyBlocks.Write(j, "uiBlocksMs"); p.SteadyFit.Write(j, "uiFitMs");
                    p.SteadyCanvas.Write(j, "canvasMs"); p.SteadyFrameAfter.Write(j, "frameAfterMs"); p.SteadyLate.Write(j, "lateMs"); j.Close();
                    j.Close();
                    rebuildP95.Add(p.Rebuild.Percentile(95)); refreshP95.Add(p.Refresh.Percentile(95)); uiP95.Add(p.Ui.Percentile(95) + p.CanvasMs.Percentile(95));
                    lateP50.Add(p.Late.Percentile(50)); logsPer.Add(p.Logs.Percentile(50)); gcHits += p.GcHits;
                    if (p.SteadyRebuild.Count > 0) { steadyP50.Add(p.SteadyRebuild.Percentile(50)); steadyP95.Add(p.SteadyRebuild.Percentile(95)); }
                    if (p.First != null)
                    {
                        firstRebuild.Add(p.First.Rebuild);
                        if (p.SteadyRebuild.Count > 0) firstExtra.Add(p.First.Rebuild - p.SteadyRebuild.Percentile(50));
                        if (p.First.FrameAfter > worstFirst) { worstFirst = p.First.FrameAfter; worstFirstPage = p.Name; }
                    }
                }
                j.CloseArr();
                j.Key("gatherParts").Open().Str("what", "each part of what a refresh gathers before the model, run " + GatherPartRuns + " times in a row (warm): p50/p95/max ms; the gather's rest is the character's counters and skills by name and the input itself");
                foreach (var kv in parts) kv.Value.Write(j, kv.Key);
                j.Close();
                j.Key("idle").Open().Num("seconds", IdleSeconds).Num("frames", idleFrames).Num("idleFrames", benchMeter.IdleFrames)
                    .Num("bytesPerIdleFrameMean", benchMeter.IdleFrames > 0 ? benchMeter.IdleBytes / (double)benchMeter.IdleFrames : -1).Num("bytesPerIdleFrameMax", benchMeter.IdleFrames > 0 ? benchMeter.IdleBytesMax : -1)
                    .Num("panelMicrosPerFrame", benchMeter.Frames > 0 ? benchMeter.Micros(benchMeter.PanelTicks / (double)benchMeter.Frames) : 0).Num("panelMicrosMax", benchMeter.Micros(benchMeter.PanelMax))
                    .Num("refreshes", benchMeter.Refreshes).Num("unknownByteFrames", benchMeter.UnknownByteFrames);
                gameFrames.Write(j, "gameFrameMs");
                if (recorder && gameAlloc.Count > 0) gameAlloc.Write(j, "gameAllocBytesPerFrame"); else j.Str("gameAllocBytesPerFrame", "not reported by this Unity build");
                j.Str("line", idleLine ?? "").Close();
                j.Key("summary").Open().Num("pages", pages.Count).Num("flips", flips).Num("seconds", clock.Elapsed.TotalSeconds);
                rebuildP95.Write(j, "pageRebuildP95Ms"); uiP95.Write(j, "pageUiAndCanvasP95Ms"); refreshP95.Write(j, "pageRefreshP95Ms");
                firstRebuild.Write(j, "pageFirstRebuildMs"); steadyP50.Write(j, "pageSteadyRebuildP50Ms"); steadyP95.Write(j, "pageSteadyRebuildP95Ms"); firstExtra.Write(j, "pageFirstExtraMs");
                lateP50.Write(j, "pageLateP50Ms"); logsPer.Write(j, "pageLogLinesP50");
                j.Num("worstFirstFrameMs", worstFirst).Str("worstFirstFramePage", worstFirstPage).Num("gcHitRebuilds", gcHits);
                j.Num("worstGameFrameMs", worst * 1000.0).Str("worstGameFrameWhen", worstWhere).Num("gcCollections", AllocClock.Collections() - gcStart);
                j.Key("logLines").OpenArr();
                foreach (var kv in benchLogTally.OrderByDescending(kv => kv.Value).Take(8)) j.Open().Str("line", kv.Key).Num("count", kv.Value).Close();
                j.CloseArr();
                j.Close();
                j.Close();
                Directory.CreateDirectory(BenchDir);
                path = Path.Combine(BenchDir, "bench-" + Plugin.Version + ".json");
                AtomicFile.Write(path, j.ToString());
                var line = "[Hearthwoven] bench: " + pages.Count + " pages x " + rounds + " rounds in " + clock.Elapsed.TotalSeconds.ToString("0.0", inv) + " s; page rebuild p95 (median page) " +
                           F(rebuildP95.Percentile(50)) + " ms, worst page " + F(rebuildP95.Max) + " ms; steady p50 (median page) " + F(steadyP50.Percentile(50)) + " ms; first visit (median page) " +
                           F(firstRebuild.Percentile(50)) + " ms, worst first frame " + F(worstFirst) + " ms (" + worstFirstPage + ")" + (opened ? "; opening " + F(openMs) + " ms" + (cold ? " (cold)" : "") : "") +
                           "; log lines while drawing " + benchLogTally.Values.Sum() + "; idle " +
                           (benchMeter.IdleFrames > 0 ? PerfMeter.Bytes(benchMeter.IdleBytes / (double)benchMeter.IdleFrames) + "/frame" : "not measured") + "; worst game frame " +
                           (worst * 1000).ToString("0.0", inv) + " ms (" + worstWhere + ")" + (failure != null ? "; stopped: " + failure : "") + "; written to " + path;
                if (failure == null) Debug.Log(line); else Debug.LogWarning(line);
                if (DevCheck.On) DevCheck.Book.Say(failure == null ? "INFO" : "FAIL", "bench", line.Replace("[Hearthwoven] bench: ", ""));
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] bench: the file was not written (" + e.Message + ")" + (failure != null ? "; stopped: " + failure : "")); }
            benchRunning = false; benchIdle = false;
            if (overtaken) yield break;   // the other run restores the panel when it is done
            Restore(snapSaved);
            snapping = false; snapRun = null;
        }
    }
}
