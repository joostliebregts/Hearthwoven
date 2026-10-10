using System;
using System.Diagnostics;
using System.Globalization;

namespace Hearthwoven
{
    /// <summary>
    /// What Hearthwoven costs per frame on this PC, measured only while Dev.SelfCheck is on (DevCheck.PerfTick logs one
    /// "HW-CHECK INFO perf: ..." line a minute). Pure C#, so the tests run it without the game: the callers hand it Stopwatch
    /// ticks and close every frame with EndFrame. Three buckets: the panel's own per-frame work (PanelUi Update + LateUpdate, open
    /// or closed), one rebuild of the open page (Render: model build + UI draw), and the client hook bodies (ClientHooks,
    /// CargoHooks and FeatsHooks Safe). Per frame it keeps the average and the worst frame; per refresh the average and the worst.
    /// Only sums and counts; it never touches the game.
    /// 0.8 (performance pass): also the bytes the panel allocates per frame (AllocClock: per thread where the runtime can tell, else
    /// the heap's growth), on its own for the frames with the book open and idle (no refresh in them: the target is 0 B), the bytes of
    /// a refresh, the phases of a page rebuild (gather the input, build the model, the JSON check, the UI objects, the canvas and text
    /// meshes after it: the Unity side) and the garbage collections in the window. Dev.Bench (Panel/PanelBench.cs) uses the same calls.
    /// </summary>
    public class PerfMeter
    {
        public const double WindowSeconds = 60;
        public static long Now => Stopwatch.GetTimestamp();

        readonly double ticksPerSecond;
        public PerfMeter() : this(Stopwatch.Frequency) { }
        public PerfMeter(long ticksPerSecond) { this.ticksPerSecond = ticksPerSecond > 0 ? ticksPerSecond : 1; }

        // this frame, until EndFrame
        long panelNow, hooksNow;
        // the window
        public int Frames { get; private set; }
        public double Seconds { get; private set; }
        public double WorstFrame { get; private set; }
        public int OpenFrames { get; private set; }
        public long PanelTicks { get; private set; }
        public long PanelMax { get; private set; }
        public long PanelOpenTicks { get; private set; }
        public long HookTicks { get; private set; }
        public long HookMax { get; private set; }
        public long HookCalls { get; private set; }
        public int Refreshes { get; private set; }
        public long RefreshTicks { get; private set; }
        public long RefreshMax { get; private set; }
        // 0.8: allocations (bytes < 0: the runtime could not tell for that stretch), refresh bytes, rebuild phases, collections
        long panelBytesNow; bool bytesNow, bytesUnknownNow, refreshedNow;
        public int ByteFrames { get; private set; }
        public long PanelBytes { get; private set; }
        public long PanelBytesMax { get; private set; }
        public int IdleFrames { get; private set; }
        public long IdleBytes { get; private set; }
        public long IdleBytesMax { get; private set; }
        public int UnknownByteFrames { get; private set; }
        public int RefreshesWithBytes { get; private set; }
        public long RefreshBytes { get; private set; }
        public int Rebuilds { get; private set; }
        public long GatherTicks { get; private set; }
        public long ModelTicks { get; private set; }
        public long JsonTicks { get; private set; }
        public long FillTicks { get; private set; }
        public long CanvasTicks { get; private set; }
        public long UiMax { get; private set; }
        int firstCollections = -1, lastCollections = -1;
        public int Collections => firstCollections < 0 ? 0 : lastCollections - firstCollections;

        public void AddPanel(long ticks) { if (ticks > 0) panelNow += ticks; }
        /// <summary>A panel body's time and the bytes it allocated (-1: not known for this stretch).</summary>
        public void AddPanel(long ticks, long bytes)
        {
            AddPanel(ticks);
            if (bytes < 0) bytesUnknownNow = true; else { bytesNow = true; panelBytesNow += bytes; }
        }
        /// <summary>One refresh of the open page (the whole Render) and the bytes it allocated (-1: unknown).</summary>
        public void AddRefresh(long ticks, long bytes)
        {
            AddRefresh(ticks);
            if (bytes >= 0) { RefreshesWithBytes++; RefreshBytes += bytes; }
        }
        /// <summary>A rebuild's phases (ticks): the input gathered, the model built, the JSON check, the UI objects made (Fill) and the canvas
        /// and text meshes brought up to date after it (the Unity side).</summary>
        public void AddRebuild(long gather, long model, long json, long fill, long canvas)
        {
            Rebuilds++;
            GatherTicks += Math.Max(0, gather); ModelTicks += Math.Max(0, model); JsonTicks += Math.Max(0, json);
            FillTicks += Math.Max(0, fill); CanvasTicks += Math.Max(0, canvas);
            var ui = Math.Max(0, fill) + Math.Max(0, canvas); if (ui > UiMax) UiMax = ui;
        }
        /// <summary>Closes one frame like EndFrame and notes the garbage collections so far (AllocClock.Collections).</summary>
        public bool EndFrame(double frameSeconds, bool bookOpen, int collections)
        {
            if (collections >= 0) { if (firstCollections < 0) firstCollections = collections; lastCollections = collections; }
            return EndFrame(frameSeconds, bookOpen);
        }
        public void AddHook(long ticks) { if (ticks > 0) hooksNow += ticks; HookCalls++; }
        public void AddRefresh(long ticks)
        {
            if (ticks < 0) ticks = 0;
            Refreshes++; RefreshTicks += ticks; refreshedNow = true;
            if (ticks > RefreshMax) RefreshMax = ticks;
        }

        /// <summary>Closes one frame (<paramref name="frameSeconds"/>: the game's own unscaled frame time); true once the window holds 60 s.</summary>
        public bool EndFrame(double frameSeconds, bool bookOpen)
        {
            if (double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds) || frameSeconds < 0) frameSeconds = 0;
            Frames++; Seconds += frameSeconds;
            if (frameSeconds > WorstFrame) WorstFrame = frameSeconds;
            PanelTicks += panelNow; if (panelNow > PanelMax) PanelMax = panelNow;
            if (bookOpen) { OpenFrames++; PanelOpenTicks += panelNow; }
            HookTicks += hooksNow; if (hooksNow > HookMax) HookMax = hooksNow;
            if (bytesUnknownNow) UnknownByteFrames++;
            else if (bytesNow)
            {
                ByteFrames++; PanelBytes += panelBytesNow; if (panelBytesNow > PanelBytesMax) PanelBytesMax = panelBytesNow;
                if (bookOpen && !refreshedNow) { IdleFrames++; IdleBytes += panelBytesNow; if (panelBytesNow > IdleBytesMax) IdleBytesMax = panelBytesNow; }
            }
            panelNow = hooksNow = 0; panelBytesNow = 0; bytesNow = bytesUnknownNow = refreshedNow = false;
            return Seconds >= WindowSeconds - 1e-6;   // summed floats: 3600 frames of 1/60 s are a minute
        }

        public void Reset()
        {
            panelNow = hooksNow = 0;
            Frames = OpenFrames = Refreshes = 0; Seconds = WorstFrame = 0;
            PanelTicks = PanelMax = PanelOpenTicks = HookTicks = HookMax = HookCalls = RefreshTicks = RefreshMax = 0;
            panelBytesNow = 0; bytesNow = bytesUnknownNow = refreshedNow = false;
            ByteFrames = IdleFrames = UnknownByteFrames = RefreshesWithBytes = Rebuilds = 0;
            PanelBytes = PanelBytesMax = IdleBytes = IdleBytesMax = RefreshBytes = 0;
            GatherTicks = ModelTicks = JsonTicks = FillTicks = CanvasTicks = UiMax = 0;
            firstCollections = lastCollections = lastCollections >= 0 ? lastCollections : -1;   // the next window counts from here
        }

        /// <summary>Reset, with the garbage collections so far as the new window's start.</summary>
        public void Reset(int collections) { Reset(); firstCollections = lastCollections = collections; }

        public double Micros(double ticks) => ticks * 1e6 / ticksPerSecond;
        public double Millis(double ticks) => ticks * 1e3 / ticksPerSecond;

        static string F(double v, string format) => v.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// The window in one line, numbers in invariant culture, e.g. "last 60 s, 3600 frames (avg frame 16.7 ms, worst 41.2 ms):
        /// panel 35.2 µs/frame avg (max 1.80 ms; book open 25% of frames: 120.4 µs/frame open, 6.9 µs/frame closed), page refresh
        /// 4.10 ms avg (max 9.30 ms, 8 refreshes), hooks 12.0 µs/frame avg (max 310.0 µs, 5120 calls)".
        /// </summary>
        public string Line()
        {
            var n = Math.Max(1, Frames);
            var closed = Frames - OpenFrames;
            var text = "last " + F(Seconds, "0") + " s, " + Frames + " frames (avg frame " + F(Seconds * 1000 / n, "0.0") + " ms, worst " + F(WorstFrame * 1000, "0.0") + " ms): " +
                "panel " + Time(PanelTicks / (double)n) + "/frame avg (max " + Time(PanelMax) + "; book open " + F(100.0 * OpenFrames / n, "0") + "% of frames" +
                (OpenFrames > 0 ? ": " + Time(PanelOpenTicks / (double)OpenFrames) + "/frame open" : "") +
                (closed > 0 ? (OpenFrames > 0 ? ", " : ": ") + Time((PanelTicks - PanelOpenTicks) / (double)closed) + "/frame closed" : "") + "), " +
                (Refreshes > 0 ? "page refresh " + F(Millis(RefreshTicks / (double)Refreshes), "0.00") + " ms avg (max " + F(Millis(RefreshMax), "0.00") + " ms, " + Refreshes + " refreshes), "
                               : "page refresh: none (the book was not open or showed nothing new), ") +
                "hooks " + Time(HookTicks / (double)n) + "/frame avg (max " + Time(HookMax) + ", " + HookCalls + " calls)";
            return text + AllocLine();
        }

        /// <summary>
        /// 0.8: what the window allocated and what a rebuild cost on the Unity side, after the time part of Line; empty when nothing was
        /// measured. E.g. "; allocations (GC.GetTotalAllocatedBytes): book open and idle 0 B/frame avg (max 0 B, 3400 frames), panel 1.2 KB/frame
        /// avg (max 96.0 KB), page refresh 120.0 KB avg; 4 rebuilds: gather 0.40 ms, model 1.20 ms, json 0.30 ms, UI 6.10 ms, canvas 2.30 ms avg
        /// (UI + canvas max 12.0 ms); 3 garbage collections".
        /// </summary>
        public string AllocLine()
        {
            var parts = "";
            if (ByteFrames > 0 || UnknownByteFrames > 0)
            {
                parts += "; allocations (" + AllocClock.Source + "): " +
                    (IdleFrames > 0 ? "book open and idle " + Bytes(IdleBytes / (double)IdleFrames) + "/frame avg (max " + Bytes(IdleBytesMax) + ", " + IdleFrames + " frames), " : "book open and idle: no such frame, ") +
                    (ByteFrames > 0 ? "panel " + Bytes(PanelBytes / (double)ByteFrames) + "/frame avg (max " + Bytes(PanelBytesMax) + ")" : "panel: not known") +
                    (UnknownByteFrames > 0 ? " (" + UnknownByteFrames + (UnknownByteFrames == 1 ? " frame" : " frames") + " not known: a collection ran in it)" : "") +
                    (RefreshesWithBytes > 0 ? ", page refresh " + Bytes(RefreshBytes / (double)RefreshesWithBytes) + " avg" : "");
            }
            if (Rebuilds > 0)
            {
                var r = (double)Rebuilds;
                parts += "; " + Rebuilds + (Rebuilds == 1 ? " rebuild" : " rebuilds") + ": gather " + Ms(GatherTicks / r) + ", model " + Ms(ModelTicks / r) + ", json " + Ms(JsonTicks / r) +
                         ", UI " + Ms(FillTicks / r) + ", canvas " + Ms(CanvasTicks / r) + " avg (UI + canvas max " + Ms(UiMax) + ")";
            }
            if (firstCollections >= 0 && (ByteFrames > 0 || UnknownByteFrames > 0 || Rebuilds > 0)) parts += "; " + Collections + (Collections == 1 ? " garbage collection" : " garbage collections");
            return parts;
        }

        string Ms(double ticks) => F(Millis(ticks), "0.00") + " ms";

        /// <summary>"0 B", "512 B", "1.2 KB", "3.40 MB" (invariant).</summary>
        public static string Bytes(double b)
        {
            if (double.IsNaN(b) || b < 0) b = 0;
            return b < 1024 ? F(Math.Round(b), "0") + " B" : b < 1024 * 1024 ? F(b / 1024, "0.0") + " KB" : F(b / (1024 * 1024), "0.00") + " MB";
        }

        // microseconds below a millisecond, else milliseconds: "35.2 µs", "1.84 ms"
        string Time(double ticks)
        {
            var us = Micros(ticks);
            return us < 1000 ? F(us, "0.0") + " µs" : F(us / 1000, "0.00") + " ms";
        }
    }
}
