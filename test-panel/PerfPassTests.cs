// The 0.8 performance pass: what keeps the open book cheap, and the numbers the benches write. One check per behaviour that matters:
// the idle frame works nothing out again (KeyLineCache), the faster JSON writer writes the same bytes, the log key cache stays bounded,
// the frame-cost meter's bytes and rebuild phases, the bench's percentiles, the allocation counter this runtime has.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Hearthwoven;
using Hearthwoven.Panel;

static class PerfPassTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // the key line: an idle frame takes the line it has; a new page, the wheel key or another width work it out again
        var cache = new KeyLineCache(); var keys = new List<string> { "[Q/E·A/D] Chapter", "[W/S] Page", "[Backspace] Back" }; int measured = 0;
        bool Fits(string line) { measured++; return line.Length < 200; }
        var first = cache.Line(keys, false, 1052f, Fits);
        for (int f = 0; f < 600; f++) cache.Line(keys, false, 1052f, Fits);   // ten seconds of frames, nothing changed
        var idleComputed = cache.Computed; var idleMeasured = measured;
        var wheel = cache.Line(keys, true, 1052f, Fits); var narrow = cache.Line(keys, true, 876f, Fits); var page = cache.Line(new List<string>(keys), true, 876f, Fits);
        Check(idleComputed == 1 && idleMeasured == 1 && first == PanelModel.KeyLine(keys, null) && cache.Computed == 4 && wheel.EndsWith(PanelModel.ScrollKey) && narrow == wheel && page == wheel,
              "idle frame: the key line is worked out once while nothing changes (600 frames: 1 time, 1 measure), again for the wheel key, a new width and a new page");

        // the JSON writer appends in place and writes exactly what Q and F wrote (the previews and the snapshots stay byte for byte)
        string[] texts = { "", "plain", "quote \" and \\ back", "line\nbreak\ttab\u0001", "Edda's é — \U0001F525" };
        double[] nums = { 0, -0.0, 1, -12, 0.1234, 2.5, 1e14, 999999999999999, 1e15, 12345678901234567, double.NaN, double.PositiveInfinity, 0.0005, -3.0000001, 7f / 3f };
        var j = new Json().Open(); var want = new System.Text.StringBuilder("{"); var firstKey = true;
        foreach (var t in texts) { j.Str(t, t); want.Append(firstKey ? "" : ",").Append(Json.Q(t)).Append(':').Append(Json.Q(t)); firstKey = false; }
        for (int i = 0; i < nums.Length; i++) { j.Num("n" + i, nums[i]); want.Append(',').Append(Json.Q("n" + i)).Append(':').Append(Json.F(nums[i])); }
        j.Dict("d", new[] { new KeyValuePair<string, float>("a\"", 3f), new KeyValuePair<string, float>("b", 0f), new KeyValuePair<string, float>("c", 0.25f) });
        want.Append(",\"d\":{").Append(Json.Q("a\"")).Append(":3,").Append(Json.Q("c")).Append(':').Append(Json.F(0.25f)).Append('}').Append('}');
        Check(j.Close().ToString() == want.ToString(), "json: strings, escapes, whole and broken numbers, NaN and the dictionary written as Q and F write them");

        // the log key cache: a key split once and found again; never more than MaxKept keys
        var k1 = LogKeys.Of("2026-10-07T20:10Z|Swamp|dealt|Draugr|Axes|slash");
        Check(ReferenceEquals(k1, LogKeys.Of("2026-10-07T20:10Z|Swamp|dealt|Draugr|Axes|slash")) && k1.HasTime && k1.Time == new DateTime(2026, 10, 7, 20, 10, 0, DateTimeKind.Utc) && k1.P.Length == 6 && k1.P[3] == "Draugr" &&
              !LogKeys.Of("Swamp|dealt|slash").HasTime, "log keys: a key is split and its minute read once, then found again; a key without a minute says so");
        for (int i = 0; i < LogKeys.MaxKept + 10; i++) LogKeys.Of("k" + i);
        Check(LogKeys.Count <= LogKeys.MaxKept, "log keys: the cache never holds more than " + LogKeys.MaxKept + " keys (" + LogKeys.Count + ")");

        // the frame-cost meter's bytes: idle frames apart from frames with a refresh, unknown spans apart, rebuild phases, collections
        var m = new PerfMeter(1_000_000);   // 1 tick = 1 µs
        m.Reset(10);
        for (int f = 0; f < 3; f++) { m.AddPanel(20, 0); m.AddPanel(10, 0); m.EndFrame(0.016, true, 10); }   // open and idle: 0 B
        m.AddPanel(900, 4096); m.AddRefresh(800, 4000); m.AddRebuild(100, 300, 50, 250, 100); m.EndFrame(0.030, true, 11);   // a refresh that drew the page
        m.AddPanel(20, -1); m.EndFrame(0.016, true, 12);   // a span the runtime could not tell
        m.AddPanel(20, 512); m.EndFrame(0.016, false, 12);  // book shut
        var line = m.Line();
        Check(m.IdleFrames == 3 && m.IdleBytes == 0 && m.ByteFrames == 5 && m.PanelBytesMax == 4096 && m.UnknownByteFrames == 1 && m.Collections == 2 && m.Rebuilds == 1 && m.UiMax == 350,
              "perf: idle frames counted apart (3 at 0 B), the refresh frame is not idle, an unknown span is left out, 2 collections");
        Check(line.Contains("book open and idle 0 B/frame avg (max 0 B, 3 frames)") && line.Contains("page refresh 3.9 KB avg") && line.Contains("1 rebuild: gather 0.10 ms, model 0.30 ms, json 0.05 ms, UI 0.25 ms, canvas 0.10 ms avg (UI + canvas max 0.35 ms)") &&
              line.Contains("(1 frame not known") && line.Contains("2 garbage collections") && line.StartsWith("last 0 s, 6 frames"), "perf: the line adds bytes, the rebuild's phases and the collections after the times: " + line);
        Check(!new PerfMeter(1_000_000).Line().Contains("allocations"), "perf: nothing measured, no allocation part in the line");

        // the bench's numbers: nearest-rank percentiles, written in invariant culture
        var s = new BenchSeries(); for (int i = 1; i <= 20; i++) s.Add(i); s.Add(double.NaN);
        var was = Thread.CurrentThread.CurrentCulture; string written;
        try { Thread.CurrentThread.CurrentCulture = new CultureInfo("nl-NL"); written = s.Write(new Json().Open(), "ms").Close().ToString(); }
        finally { Thread.CurrentThread.CurrentCulture = was; }
        Check(s.Count == 20 && s.Percentile(50) == 10 && s.Percentile(95) == 19 && s.Max == 20 && written == "{\"ms\":{\"p50\":10,\"p95\":19,\"max\":20,\"mean\":10.5,\"n\":20}}" && new BenchSeries().Percentile(95) == 0,
              "bench: p50 10, p95 19, max 20 of 1..20 (nearest rank, NaN left out), written with a point on a Dutch PC: " + written);

        // a fellow's live update (REVIEW-07 finding 8): applied onto the parsed copy it gives the JSON the text gave, and leaves the parsed copy as
        // it was; on the worker it ends where the old at-once path ended (the latest update shown, the trail and the arrival time noted), and the
        // panel and the trail share the one read of it
        GroupShare.Clear();
        var at0 = new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc);
        var fellow = new BenchFellow(at0);
        var key = GroupShare.TakeFull(fellow.Stored, at0);
        var (s1, d1) = fellow.Next(); var (s2, d2) = fellow.Next();
        var tree = LiveDelta.Parse(fellow.Stored); var untouched = LiveDelta.Write(tree);
        var want2 = LiveDelta.Apply(fellow.Stored, d2);
        Check(LiveDelta.Apply(tree, d1) == LiveDelta.Apply(fellow.Stored, d1) && LiveDelta.Apply(tree, d2) == want2 && want2 != fellow.Stored && LiveDelta.Write(tree) == untouched,
              "live: an update applied onto the parsed copy writes what applying it onto the text writes, and the parsed copy stays as it was");
        var reads = FellowCopies.Reads;
        GroupShare.TakeLive(key, "s1-1", s1, d1, at0.AddSeconds(10)); GroupShare.TakeLive(key, "s1-1", s2, d2, at0.AddSeconds(20));
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (GroupShare.LivePending > 0 && waited.ElapsedMilliseconds < 5000) { GroupShare.DrainLive(); Thread.Sleep(2); }
        GroupShare.DrainLive();
        var shown = GroupShare.Group.TryGetValue(key ?? "", out var g) ? g : null;
        Check(key != null && shown == want2 && GroupShare.ReceivedAt(key) == at0.AddSeconds(20) && GroupShare.Trails.Of(key)?.LastUtc == at0.AddSeconds(20) &&
              FellowCopies.Of(key, shown) != null && FellowCopies.Reads == reads,
              "live: on the worker the latest update is shown (the overtaken one dropped), the trail and the arrival time noted, and the panel reads no copy again (" + (FellowCopies.Reads - reads) + " reads)");
        // REVIEW-08 #4: the marks saved at each send interval take that same read too, and the name in the copy's text, not the panel's label
        var read1 = FellowCopies.Of(key ?? "", shown ?? ""); var ownName = read1?.PlayerName; if (read1 != null) read1.PlayerName = ownName + " (2)";
        var marks = new FellowMarks(); var readsBefore = FellowCopies.Reads;
        var marked = marks.Update(GroupShare.Group, null, at0.AddMinutes(2));
        Check(marked && key != null && marks.Latest.TryGetValue(key, out var mk) && mk.Name == ownName && !string.IsNullOrEmpty(ownName) && FellowCopies.Reads == readsBefore,
              "marks: the fellow marks take the copy the worker already read (" + (FellowCopies.Reads - readsBefore) + " reads), named as the copy names itself (" + ownName + "), not by the panel's label");
        if (read1 != null) read1.PlayerName = ownName;
        GroupShare.Clear();

        // the allocation counter: this runtime counts per thread, and sees an allocation
        var span = AllocClock.Span.Start(); var big = new byte[100_000]; var seen = span.Bytes(); GC.KeepAlive(big);
        Check(AllocClock.Exact && AllocClock.Source == "GC.GetAllocatedBytesForCurrentThread" && seen >= 100_000, "alloc clock: on .NET the per-thread counter, which sees a 100 KB array (" + seen + " B)");

        // 0.8.1 Dev.Bench: round 1 finds the pages while drawing them (PanelBenchWalk) and visits exactly what the page walk plans
        // (PanelSnapshot.Plan: each chapter's list, each page's views or toggle sides), each once, under the plan's names; your own book and Everyone
        var when = new DateTime(2026, 10, 7, 22, 30, 0, DateTimeKind.Utc);
        var world = PanelSample.Full(when); FullDump.Books(world);
        foreach (var everyone in new[] { false, true })
        {
            var st = new PanelState { Everyone = everyone }; var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var done = new HashSet<string>();
            var todo = PanelBenchWalk.Start(names); var found = new List<string>();
            for (int i = 0; i < todo.Count; i++) { PanelBenchWalk.Apply(todo[i], st); found.Add(PanelBenchWalk.Learn(todo[i], PanelModel.Build(world, st), todo, i, names, done).Name); }
            var plan = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Chapter c in Enum.GetValues(typeof(Chapter)))
            {
                var opened = PanelModel.Build(world, new PanelState { Chapter = c, Everyone = everyone });
                foreach (var pg in opened.List.Count > 0 ? opened.List.ConvertAll(l => l.Id) : new List<string> { null })
                {
                    var probe = new PanelState { Chapter = c, Everyone = everyone }; if (pg != null) probe.Page[c] = pg;
                    var v = PanelModel.Build(world, probe); var stem = PanelBenchWalk.Safe(c.ToString()) + (pg != null ? "-" + PanelBenchWalk.Safe(pg) : "");
                    var sw = PanelModel.Content(v).Find(x => x.Kind == "switch" && x.Items != null && x.Items.Count > 1);
                    if (sw != null) foreach (var o in sw.Items) plan.Add(stem + "-" + PanelBenchWalk.Safe(o.Id));
                    else if (v.Toggle.Count > 1) foreach (var t in v.Toggle) plan.Add(stem + "-" + PanelBenchWalk.Safe(t.Id));
                    else plan.Add(stem);
                }
            }
            plan.Add("about");
            var distinct = new HashSet<string>(found, StringComparer.OrdinalIgnoreCase);
            Check(distinct.Count == found.Count && distinct.SetEquals(plan),
                  "bench round 1" + (everyone ? " (Everyone on)" : "") + ": the pages found while drawing are the page walk's " + plan.Count + ", each once (" + found.Count + " drawn" +
                  (distinct.SetEquals(plan) ? "" : "; missing " + string.Join(",", System.Linq.Enumerable.Except(plan, distinct)) + "; extra " + string.Join(",", System.Linq.Enumerable.Except(distinct, plan))) + ")");
        }

        // 0.8.1 Dev.UiReuse (PanelReuse.cs): a page turn inside a chapter keeps the tabs, the list and the player row (their keys stay; the chosen
        // tab and row are restyled), another chapter keeps the tabs but draws its list new, and the Everyone chip turned on draws the player row new
        var fellows = new List<string>(); foreach (var f in world.Fellows) fellows.Add(f.PlayerName);
        PanelView Drawn(Chapter c, string page, bool everyone = false)
        {
            var st = new PanelState { Chapter = c, Everyone = everyone }; if (page != null) st.Page[c] = page;
            var v = PanelModel.Build(world, st); PanelModel.AddPlayers(v, world.PlayerName, fellows, st.Player, true); return v;
        }
        var cooking = Drawn(Chapter.Deeds, "cooking"); var crafting = Drawn(Chapter.Deeds, "crafting"); var battle = Drawn(Chapter.Battle, null); var together = Drawn(Chapter.Deeds, "crafting", everyone: true);
        var (rowH, gap) = PanelModel.ListRows(cooking.List.Count, 468);
        Check(ChromeKeys.Tabs(cooking) == ChromeKeys.Tabs(crafting) && ChromeKeys.List(cooking, rowH, gap) == ChromeKeys.List(crafting, rowH, gap) &&
              ChromeKeys.Players(cooking, 0, 5) == ChromeKeys.Players(crafting, 0, 5) && cooking.List.Find(x => x.Selected)?.Id == "cooking" && crafting.List.Find(x => x.Selected)?.Id == "crafting",
              "reuse: Deeds > Cooking to Crafting keeps the tabs, the list and the player row (only the chosen row moves)");
        Check(ChromeKeys.Tabs(cooking) == ChromeKeys.Tabs(battle) && ChromeKeys.List(cooking, rowH, gap) != ChromeKeys.List(battle, rowH, gap),
              "reuse: another chapter keeps the tabs (restyled) and draws its list new");
        Check(ChromeKeys.Players(crafting, 0, 5) != ChromeKeys.Players(together, 0, 5), "reuse: the Everyone chip turned on draws the player row new");
        // the in-game parity line's comparison (Dev.SelfCheck ui-reuse): the same drawings pass; a kept object with a child left over names it
        var drawnA = new List<string> { "/Panel#0 children=3 active=1 parts=Image;", "/Panel#0/Text#1 children=0 active=1 parts=TextMeshProUGUI; text=\"Cooking\"" };
        var drawnB = new List<string> { "/Panel#0 children=3 active=1 parts=Image;", "/Panel#0/Text#1 children=1 active=1 parts=TextMeshProUGUI; text=\"Cooking\"" };
        var parity = UiParity.Compare(drawnB, drawnA);
        Check(UiParity.Compare(drawnA, new List<string>(drawnA)) == null && parity != null && parity.StartsWith("/Panel#0/Text#1: children=1 (fresh children=0)") &&
              UiParity.Compare(drawnA, drawnA.GetRange(0, 1)) != null, "ui-reuse parity: the same drawings pass; a left-over child is named with both counts (" + parity + "); a missing object fails");

        // 0.8.1 review 8: the 2 s refresh's change test (ToJson) builds no island plan (a pure function of the blocks it already holds); the dump's JSON carries it
        var wood = PanelModel.Build(world, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = "woodcutting" } });
        Check(!PanelModel.ToJson(wood).Contains("\"islands\"") && PanelModel.ToJson(wood, islands: true).Contains("\"islands\":[{\"span\""),
              "change test: the refresh's page text carries no island plan; the preview dump's does");
        return fails;
    }
}
