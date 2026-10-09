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

        public void AddPanel(long ticks) { if (ticks > 0) panelNow += ticks; }
        public void AddHook(long ticks) { if (ticks > 0) hooksNow += ticks; HookCalls++; }
        public void AddRefresh(long ticks)
        {
            if (ticks < 0) ticks = 0;
            Refreshes++; RefreshTicks += ticks;
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
            panelNow = hooksNow = 0;
            return Seconds >= WindowSeconds - 1e-6;   // summed floats: 3600 frames of 1/60 s are a minute
        }

        public void Reset()
        {
            panelNow = hooksNow = 0;
            Frames = OpenFrames = Refreshes = 0; Seconds = WorstFrame = 0;
            PanelTicks = PanelMax = PanelOpenTicks = HookTicks = HookMax = HookCalls = RefreshTicks = RefreshMax = 0;
        }

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
            return text;
        }

        // microseconds below a millisecond, else milliseconds: "35.2 µs", "1.84 ms"
        string Time(double ticks)
        {
            var us = Micros(ticks);
            return us < 1000 ? F(us, "0.0") + " µs" : F(us / 1000, "0.00") + " ms";
        }
    }
}
