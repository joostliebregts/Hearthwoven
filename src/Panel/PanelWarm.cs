using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// What the book loads the first time it needs something, done beforehand (0.8.1 performance pass). Joost's in-game bench showed one-off
    /// costs on a page's first visit that later visits do not pay (Deeds > Cooking with Everyone: 15 ms more in its model once, 26 ms more in
    /// its UI). Three loads work that way: the game data the pages group by (every prefab read once, on the book's first opening), the panel's
    /// own pictures (PNGs decoded and sent to the GPU when a page first shows them) and the items' own colours for the composition bars (a GPU
    /// read per new item that waits for the GPU: the likeliest cause of Cooking's model spike). Here, while the book is shut and the player has
    /// been in the world a moment, a little per frame:
    /// 0. the look (PanelLook.Resolve: the game's fonts found, the drawn shapes made), one frame;
    /// 1. the game data (GameData.Warm): one frame, the one longer step, soon after the spawn's own heavy frames;
    /// 2. the panel's pictures (the UI kit, the vocabulary, the title emblems): a few decoded per frame;
    /// 3. the item colours: asked of the GPU without waiting (PanelLook.WarmColours), a few per frame, answered a frame or two later;
    /// 4. the book's code (0.8.1): the game's runtime compiles a method the first time it runs, so the first opening and each page's first visit
    ///    paid for compiling the book's code as it went; every method of the book (Hearthwoven.Panel, the developer tools left out) is compiled
    ///    here instead, without running it: MethodHandle.GetFunctionPointer, which makes Mono compile it (RuntimeHelpers.PrepareMethod is
    ///    most likely a no-op there, REVIEW-081). Each frame until CodeMsPerFrame has passed: at most that plus the one method under way.
    /// Nothing changes what the book shows: the same tables fill earlier, and anything not warm yet is loaded as before when a page needs it.
    /// Off with the panel (Panel.Enabled false). Called from PanelUi.Frame while the book is shut; once done it costs one comparison.
    /// </summary>
    static class PanelWarm
    {
        const float DelaySeconds = 1f;          // after the local player appears: past the spawn's own heaviest frames
        const float GiveUpSeconds = 30f;        // a GPU answer that never comes does not keep the warm-up asking every frame
        const int PicturesPerFrame = 3, ColoursPerFrame = 8;
        const double CodeMsPerFrame = 2.0;
        enum Stage { Look, GameData, Pictures, Colours, Code, Done }
        static Stage stage;
        static float startAt = -1f, begunAt = -1f;
        static List<string> pictures; static int nextPicture;
        static double gameDataMs, picturesMs, coloursMs, codeMs, worstStepMs; static int steps;
        static List<System.Reflection.MethodBase> code; static int nextCode, codeFailed;
        static double slowestMethodMs; static string slowestMethod = "";

        public static bool Done => stage == Stage.Done;

        /// <summary>One small step; nothing before the player is in the world, nothing once done.</summary>
        public static void Step()
        {
            if (stage == Stage.Done) return;
            if (!Player.m_localPlayer || !ObjectDB.instance || ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0 || !ZNetScene.instance) { startAt = -1f; return; }
            if (startAt < 0f) { startAt = Time.unscaledTime + DelaySeconds; return; }
            if (Time.unscaledTime < startAt) return;
            if (begunAt < 0f) begunAt = Time.unscaledTime;
            var t0 = PerfMeter.Now;
            var at = stage;
            try
            {
                switch (stage)
                {
                    case Stage.Look:
                        PanelLook.Resolve(); stage = Stage.GameData;   // the game's fonts found and the drawn shapes made (the book's first opening did it)
                        break;
                    case Stage.GameData:
                        GameData.Warm(); stage = Stage.Pictures;   // read or not (nothing there to read), the book reads it again when it opens
                        break;
                    case Stage.Pictures:
                        if (pictures == null) pictures = PanelLook.OwnPictures();
                        for (int i = 0; i < PicturesPerFrame && nextPicture < pictures.Count; i++) PanelLook.Icon(pictures[nextPicture++]);
                        if (nextPicture >= pictures.Count) stage = Stage.Colours;
                        break;
                    case Stage.Colours:
                        if (PanelLook.WarmColours(ColoursPerFrame) || Time.unscaledTime - begunAt > GiveUpSeconds) stage = Stage.Code;
                        break;
                    case Stage.Code:
                        if (code == null) code = BookCode();
                        var until = t0 + (long)(CodeMsPerFrame / 1000.0 * System.Diagnostics.Stopwatch.Frequency);
                        do
                        {
                            if (nextCode >= code.Count) { stage = Stage.Done; code = null; break; }
                            var m0 = PerfMeter.Now;
                            try { code[nextCode].MethodHandle.GetFunctionPointer(); }
                            catch { codeFailed++; }   // a method the runtime cannot compile ahead stays as it was: compiled when it first runs
                            var mms = (PerfMeter.Now - m0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                            if (mms > slowestMethodMs) { slowestMethodMs = mms; slowestMethod = code[nextCode].DeclaringType?.Name + "." + code[nextCode].Name; }
                            nextCode++;
                        } while (PerfMeter.Now < until);
                        break;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel warm-up stopped (the book loads the rest when it needs it): " + e.Message); stage = Stage.Done; }
            var ms = (PerfMeter.Now - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (at == Stage.Look || at == Stage.GameData) gameDataMs += ms; else if (at == Stage.Pictures) picturesMs += ms; else if (at == Stage.Colours) coloursMs += ms; else codeMs += ms;
            steps++; if (ms > worstStepMs) worstStepMs = ms;
            if (stage == Stage.Done) Debug.Log("[Hearthwoven] panel warm-up done: " + Line());
        }

        // every method and constructor of the book's own types (Hearthwoven.Panel, the compiler's closures and iterators with them) that can be
        // compiled without running: not abstract or external, not generic and not on a generic type, no type initializer
        static List<System.Reflection.MethodBase> BookCode()
        {
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
            var list = new List<System.Reflection.MethodBase>();
            Type[] types;
            try { types = typeof(PanelWarm).Assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
            foreach (var t in types)
            {
                if (t == null || t.Namespace != "Hearthwoven.Panel" || t.ContainsGenericParameters || t.IsInterface) continue;
                if (DevOnly(t)) continue;
                foreach (var m in t.GetMethods(all))
                    if (!m.IsAbstract && !m.ContainsGenericParameters && HasBody(m) && !(t == typeof(PanelUi) && DevMethods.Contains(m.Name))) list.Add(m);
                foreach (var c in t.GetConstructors(all))
                    if (!c.IsStatic && HasBody(c)) list.Add(c);
            }
            return list;
        }

        // the developer tools a player's book never runs: Dev.SampleData's world, Dev.Bench (PanelBench, PanelBenchWalk), the panel snapshots
        // (PanelSnapshot) and the self-check (PanelCheck); their own types, their PanelUi methods by name, their iterators
        static readonly HashSet<string> DevTypes = new HashSet<string> { "PanelBenchWalk", "BenchShot", "PanelPng", "PanelProbe", "BenchPage", "RenderMarks", "Visit", "Saved", "Shot", "ConfigEntryKey" };
        static readonly HashSet<string> DevMethods = new HashSet<string>
        {
            "AddRebuild", "AddRefresh", "BenchLate", "BenchLog", "BenchRun", "BenchTick", "BindBenchConfig", "MeasuredRender",
            "AboutPlan", "Apply", "BindSnapshotConfig", "CanSnap", "Capture", "Model", "Plan", "Probe", "Restore", "SaveState", "Shows", "SnapshotRun", "SnapshotTick", "Wanted",
            "CheckRun", "CheckTick", "FilterKeyHeld", "FocusNow", "KeyClashCheck", "KeyReport", "Marks", "PageName",
        };
        static bool DevOnly(Type t)
        {
            var name = t.FullName ?? t.Name;
            if (name.Contains("Sample") || DevTypes.Contains(t.Name)) return true;
            for (var outer = t.DeclaringType; outer != null; outer = outer.DeclaringType) if (DevTypes.Contains(outer.Name)) return true;
            return name.Contains("<BenchRun>") || name.Contains("<SnapshotRun>") || name.Contains("<CheckRun>") || name.Contains("<MeasuredRender>");
        }

        static bool HasBody(System.Reflection.MethodBase m) =>
            (m.MethodImplementationFlags & (System.Reflection.MethodImplAttributes.InternalCall | System.Reflection.MethodImplAttributes.Runtime)) == 0 &&
            (m.Attributes & System.Reflection.MethodAttributes.PinvokeImpl) == 0;

        /// <summary>What the warm-up did and what it cost on the main thread (Dev.Bench's header and the log).</summary>
        public static string Line()
        {
            string F(double v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            return (stage == Stage.Done ? "done" : begunAt < 0f ? "not begun" : "at " + stage) + "; look and game data " + F(gameDataMs) + " ms, " + nextPicture + " pictures " + F(picturesMs) +
                   " ms, colours " + PanelLook.ColourWarmth + " (" + F(coloursMs) + " ms), code " + nextCode + " methods" + (codeFailed > 0 ? " (" + codeFailed + " left to run time)" : "") +
                   " (" + F(codeMs) + " ms, slowest " + F(slowestMethodMs) + " ms: " + slowestMethod + "); " + steps + " frames, worst " + F(worstStepMs) + " ms" +
                   (begunAt >= 0f ? ", begun " + F(Time.unscaledTime - begunAt) + " s ago" : "");
        }
    }
}
