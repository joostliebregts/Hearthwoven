// The self-check's frame-cost meter (src/PerfMeter.cs, Dev.SelfCheck): sums per frame, worst frame, book open share,
// refreshes, the one-minute window and the log line in invariant culture. Fake ticks: 1 tick = 1 µs.
using System;
using System.Globalization;
using System.Threading;
using Hearthwoven;

static class PerfTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        var m = new PerfMeter(1_000_000);   // 1 tick = 1 µs
        // frame 1: book closed, panel 10 µs (Update 6 + LateUpdate 4), two hooks 30 + 20 µs
        m.AddPanel(6); m.AddPanel(4); m.AddHook(30); m.AddHook(20);
        Check(!m.EndFrame(0.016, false), "perf: a frame closes without filling the window");
        // frame 2: book open, panel 200 µs, one refresh of 3 ms inside it, one hook 5 µs
        m.AddPanel(200); m.AddRefresh(3000); m.AddHook(5);
        m.EndFrame(0.020, true);
        // frame 3: book open, panel 100 µs, a second refresh of 1 ms, no hooks
        m.AddPanel(100); m.AddRefresh(1000);
        m.EndFrame(0.018, true);
        Check(m.Frames == 3 && m.OpenFrames == 2 && Math.Abs(m.Seconds - 0.054) < 1e-9, "perf: frames, open frames and real seconds counted");
        Check(m.PanelTicks == 310 && m.PanelMax == 200 && m.PanelOpenTicks == 300, "perf: panel time summed per frame, worst frame kept, open frames apart");
        Check(m.HookTicks == 55 && m.HookMax == 50 && m.HookCalls == 3, "perf: hook bodies summed per frame (max is the worst frame, not the worst call), calls counted");
        Check(m.Refreshes == 2 && m.RefreshTicks == 4000 && m.RefreshMax == 3000, "perf: refreshes averaged and their worst kept");
        Check(Math.Abs(m.WorstFrame - 0.020) < 1e-9, "perf: the game's worst frame time kept");

        var line = m.Line();
        Check(line.StartsWith("last 0 s, 3 frames (avg frame 18.0 ms, worst 20.0 ms): "), "perf: the line opens with the window and the game's frame time: " + line);
        Check(line.Contains("panel 103.3 µs/frame avg (max 200.0 µs; book open 67% of frames: 150.0 µs/frame open, 10.0 µs/frame closed)"), "perf: panel per frame, its max, open share and open vs closed");
        Check(line.Contains("page refresh 2.00 ms avg (max 3.00 ms, 2 refreshes)"), "perf: page refresh in ms with its max and count");
        Check(line.Contains("hooks 18.3 µs/frame avg (max 50.0 µs, 3 calls)"), "perf: hooks per frame with the worst frame and the call count");

        // invariant culture, whatever the PC's language (a Dutch PC writes 18,0 elsewhere)
        var was = Thread.CurrentThread.CurrentCulture;
        try { Thread.CurrentThread.CurrentCulture = new CultureInfo("nl-NL"); Check(m.Line() == line, "perf: the line is the same on a Dutch PC (invariant numbers)"); }
        finally { Thread.CurrentThread.CurrentCulture = was; }

        // a minute of real time fills the window; Reset empties it
        var w = new PerfMeter(1_000_000);
        bool full = false; int frames = 0;
        while (!full && frames < 10000) { w.AddPanel(1500); full = w.EndFrame(1 / 60.0, false); frames++; }
        Check(full && frames == 3600, "perf: the window is full after 60 s of frames (" + frames + " at 60 fps)");
        Check(w.Line().Contains("panel 1.50 ms/frame avg (max 1.50 ms; book open 0% of frames: 1.50 ms/frame closed)") && w.Line().Contains("page refresh: none"), "perf: ms past a millisecond; no refresh says so: " + w.Line());
        w.Reset();
        Check(w.Frames == 0 && w.Seconds == 0 && w.PanelTicks == 0 && w.HookCalls == 0 && w.Refreshes == 0 && w.WorstFrame == 0, "perf: Reset starts a fresh window");
        w.AddHook(7); w.Reset(); w.EndFrame(0.01, false);
        Check(w.HookTicks == 0 && w.HookMax == 0, "perf: Reset also drops the half-summed frame");

        // nonsense from the game never breaks the window
        var z = new PerfMeter(1_000_000);
        z.EndFrame(double.NaN, false); z.EndFrame(-1, true); z.AddRefresh(-5); z.AddPanel(-3);
        Check(z.Frames == 2 && z.Seconds == 0 && z.RefreshTicks == 0 && z.PanelTicks == 0 && !z.Line().Contains("NaN"), "perf: NaN, negative frame times and negative ticks count as 0");
        Check(new PerfMeter().Line().StartsWith("last 0 s, 0 frames"), "perf: an empty window still writes a line");

        // the self-check asks for it
        Check(Array.Exists(CheckBook.Expected, e => e.Key == "perf" && e.Value.Contains("open the book")), "perf: the self-check report waits for a perf line until one minute is played");
        return fails;
    }
}
