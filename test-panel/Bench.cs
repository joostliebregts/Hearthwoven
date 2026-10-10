// The offline page bench (0.8 performance pass): test-panel -- --bench <dir> [--label before] [--n 50]. Builds every chapter, page and
// view of the sample world (PanelSample.Full: Rowan's own book, then Edda's as Rowan receives it; the About pages) N times, as the panel's
// refresh does it in game (PanelUi.Draw: PanelModel.Build + AddPlayers, then ToJson for the "nothing new" check), and writes per page:
// the model build time p50/p95/max, the JSON time, the whole refresh, the bytes allocated (GC.GetAllocatedBytesForCurrentThread), the
// GC counts, and whether the page's JSON stays the same when nothing changed (twice in a row; with the clock 2 s and 60 s on), which is
// what lets the idle refresh skip the UI rebuild. Output: <dir>/bench-offline-<label>.json, a short table on the console.
// Also: Heavy (Rowan after a six-hour session: how a page's cost grows with play), the open book's idle frame (its pure per-frame work, the
// way before 0.8 and now) and the feats tick. --probe <page prefix> prints a page's allocations by type instead (test-panel/BenchProbe.cs).
// This measures .NET on this Mac, not the game's Mono: Dev.Bench (src/Panel/PanelBench.cs) measures the real thing in game.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Hearthwoven;
using Hearthwoven.Panel;

sealed class KeyFitter { public bool Fits(string line) => line.Length < 400; }   // the key line's measure, as PanelUi.KeysFit (TMP) does it, by length

static class PanelBenchOffline
{
    sealed class Shot { public string Name, Book, Player, View; public Chapter Chapter; public string Page; public bool About; public string AboutPage, Switch; public bool? They; public bool Everyone; }

    static PanelState StateOf(Shot s)
    {
        var st = new PanelState { Chapter = s.Chapter, Player = s.Player ?? "", ShowAbout = s.About, Everyone = s.Everyone };
        if (s.About) st.AboutPage = s.AboutPage;
        if (s.Page != null) st.Page[s.Chapter] = s.Page;
        if (s.Switch != null) st.View[s.Switch] = s.View;
        if (s.They.HasValue) st.TheyReceived = s.They.Value;
        return st;
    }

    // every page and view, as the in-game page walk plans it (PanelSnapshot.Plan): each list entry, each view of its switch or both sides of its toggle
    // everyone: the Everyone chip on (Group: only the pages that show the group's view, the way Joost's in-game bench of 0.8.0 ran)
    static List<Shot> Plan(string bookName, PanelInput book, string player, bool everyone = false)
    {
        var shots = new List<Shot>();
        foreach (Chapter c in Enum.GetValues(typeof(Chapter)))
        {
            var first = PanelModel.Build(book, new PanelState { Chapter = c, Player = player, Everyone = everyone });
            var pages = first.List.Count > 0 ? first.List.Select(l => l.Id).ToList() : new List<string> { null };
            foreach (var page in pages)
            {
                var probe = new PanelState { Chapter = c, Player = player, Everyone = everyone }; if (page != null) probe.Page[c] = page;
                var v = PanelModel.Build(book, probe);
                if (everyone && !v.EveryoneOn) continue;
                var name = bookName + "/" + c + (page != null ? "/" + page : "");
                var sw = PanelModel.Content(v).FirstOrDefault(b => b.Kind == "switch" && b.Items != null && b.Items.Count > 1);
                if (sw != null) foreach (var o in sw.Items) shots.Add(new Shot { Name = name + "/" + o.Id, Book = bookName, Player = player, Chapter = c, Page = page, Switch = sw.Id, View = o.Id, Everyone = everyone });
                else if (v.Toggle.Count > 1) foreach (var t in v.Toggle) shots.Add(new Shot { Name = name + "/" + t.Id, Book = bookName, Player = player, Chapter = c, Page = page, They = t.Id == "they", View = t.Id, Everyone = everyone });
                else shots.Add(new Shot { Name = name, Book = bookName, Player = player, Chapter = c, Page = page, Everyone = everyone });
            }
        }
        if (player == "" && !everyone) foreach (var (id, _) in PanelModel.AboutList) shots.Add(new Shot { Name = bookName + "/About/" + id, Book = bookName, Player = "", About = true, AboutPage = id });
        return shots;
    }

    // A long evening on top of the sample world: six hours of fighting, a hit or four every minute across biomes, foes and weapons, and
    // six hours of deeds per minute (Deeds > Recent), so the bench shows how a page's cost grows with a session as Joost's group plays it
    static PanelInput Heavy(DateTime now)
    {
        var w = PanelSample.Full(now);
        string[] biomes = { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains" }, foes = { "Greyling", "Troll", "Draugr", "Wolf", "Fuling", "Deathsquito" }, weapons = { "Axes", "Swords", "Bows", "Spears", "Clubs" }, types = { "slash", "pierce", "blunt", "fire", "frost", "poison" };
        for (int m = 360; m > 0; m--)
        {
            var t = now.AddMinutes(-m);
            for (int k = 0; k < 3; k++)
            {
                var d = new HitData.DamageTypes();
                switch (types[(m + k) % types.Length]) { case "slash": d.m_slash = 20 + k; break; case "pierce": d.m_pierce = 15 + k; break; case "blunt": d.m_blunt = 25 + k; break; case "fire": d.m_fire = 8 + k; break; case "frost": d.m_frost = 6 + k; break; default: d.m_poison = 5 + k; break; }
                w.Log.AddDamage(t.AddSeconds(k * 10), biomes[(m / 40) % biomes.Length], true, foes[(m + k) % foes.Length], weapons[(m / 7 + k) % weapons.Length], d);
            }
            w.Log.AddDamage(t.AddSeconds(45), biomes[(m / 40) % biomes.Length], false, foes[m % foes.Length], "EnemyHit", new HitData.DamageTypes { m_blunt = 12 });
        }
        if (w.Deeds != null)
            for (int m = 360; m > 0; m--)
            {
                var t = now.AddMinutes(-m);
                var at = m > EventLog.FoldAfterMinutes ? EventLog.Bucket(t.AddMinutes(-(t.Minute % 10))) : EventLog.Bucket(t);
                void Book(string family, string token, float v) { var key = at + "|" + family + "|" + token; w.Deeds.Buckets.TryGetValue(key, out var o); w.Deeds.Buckets[key] = o + v; }
                Book(DeedLog.PickedUp, "$item_wood", 6); Book(DeedLog.PickedUp, "$item_stone", 3); Book(DeedLog.Felled, "Beech", 1); Book(DeedLog.ChopHits, "Beech", 9);
                Book(DeedLog.Skills, "WoodCutting", 0.4f); if (m % 3 == 0) Book(DeedLog.Made, "$item_bread", 1); if (m % 5 == 0) Book(DeedLog.Kills, "$enemy_greyling", 1);
            }
        return w;
    }

    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    // the first place two page JSONs differ, a little of each side around it
    static string FirstChange(string a, string b)
    {
        int i = 0, n = Math.Min(a.Length, b.Length);
        while (i < n && a[i] == b[i]) i++;
        int from = Math.Max(0, i - 60);
        string Cut(string s) => s.Substring(from, Math.Min(s.Length - from, 140));
        return Cut(a) + "  =>  " + Cut(b);
    }

    static string Arg(string[] args, string name, string fallback) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }

    public static int Run(string[] args, DateTime now)
    {
        var dir = Arg(args, "--bench", ".");
        var label = Arg(args, "--label", "run");
        var n = int.TryParse(Arg(args, "--n", "50"), out var nn) && nn > 0 ? nn : 50;
        const int warm = 3;

        var world = PanelSample.Full(now);
        var books = FullDump.Books(world);   // Rowan's own book with the group around it; each fellow's as the copy Rowan receives
        var group = world.Fellows.Select(f => f.PlayerName).ToList();
        var benched = new[] { "Rowan", "Edda" };   // your own book, and one fellow's (every chapter): the two ways a page is built; then Heavy
        var shots = new List<(Shot shot, PanelInput book)>();
        foreach (var (who, book) in books.Where(b => benched.Contains(b.who)))
            foreach (var s in Plan(who, book, who == "Rowan" ? "" : who)) shots.Add((s, book));
        var own = books.First(b => b.who == "Rowan").book;   // Group: Rowan's book with the Everyone chip on (0.8), every page with a group view
        foreach (var s in Plan("Group", own, "", everyone: true)) shots.Add((s, own));
        var heavy = Heavy(now); var heavyBooks = FullDump.Books(heavy);   // Rowan after a six-hour session (Heavy): your own pages only (the fellows wired as in game)
        foreach (var s in Plan("Heavy", heavy, "")) shots.Add((s, heavy));

        var probeAt = Array.IndexOf(args, "--probe");
        if (probeAt >= 0 && probeAt + 1 < args.Length)
        {
            foreach (var (shot, book) in shots.Where(x => x.shot.Name.StartsWith(args[probeAt + 1])))
                AllocProbe.Run(book, StateOf(shot), shot.Name, 40, (b, st) => { var v = PanelModel.Build(b, st); PanelModel.AddPlayers(v, world.PlayerName, group, st.Player, true); return PanelModel.ToJson(v); });
            return 0;
        }
        void Shift(TimeSpan by) { foreach (var (_, b) in books) b.NowUtc = b.NowUtc + by; foreach (var (_, b) in heavyBooks) b.NowUtc = b.NowUtc + by; }   // every book's clock (fellows read Rowan's clock in game)

        var j = new Json().Open().Str("label", label).Str("kind", "offline").Str("runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription)
            .Str("machine", Environment.MachineName).Num("processors", Environment.ProcessorCount).Num("n", n).Num("warmup", warm)
            .Str("what", "per page: PanelModel.Build + AddPlayers (build), PanelModel.ToJson (json), both (refresh: the idle 2 s refresh in game, without Gather and Fill); bytes = GC.GetAllocatedBytesForCurrentThread");
        j.Key("pages").OpenArr();
        var allBuild = new BenchSeries(); var allRefresh = new BenchSeries(); var allBytes = new BenchSeries(); var pageP95 = new BenchSeries();
        var rows = new List<(string name, double p50, double p95, double max, double kb, double jsonMs, bool same, bool same2, bool same60)>();
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        var walk = Stopwatch.StartNew();
        foreach (var (shot, book) in shots)
        {
            var st = StateOf(shot);   // one state per page, kept across builds as the panel keeps its own
            string Refresh() { var v = PanelModel.Build(book, st); PanelModel.AddPlayers(v, world.PlayerName, group, st.Player, true); return PanelModel.ToJson(v); }
            string first = null;
            for (int w = 0; w < warm; w++) first = Refresh();
            var build = new BenchSeries(); var json = new BenchSeries(); var refresh = new BenchSeries(); var bytes = new BenchSeries(); var jsonBytes = new BenchSeries();
            bool same = true; int len = 0;
            int p0 = GC.CollectionCount(0), p1 = GC.CollectionCount(1), p2 = GC.CollectionCount(2);
            for (int i = 0; i < n; i++)
            {
                var a0 = GC.GetAllocatedBytesForCurrentThread(); var t0 = Stopwatch.GetTimestamp();
                var v = PanelModel.Build(book, st); PanelModel.AddPlayers(v, world.PlayerName, group, st.Player, true);
                var t1 = Stopwatch.GetTimestamp(); var a1 = GC.GetAllocatedBytesForCurrentThread();
                var text = PanelModel.ToJson(v); var unchanged = text == first;
                var t2 = Stopwatch.GetTimestamp(); var a2 = GC.GetAllocatedBytesForCurrentThread();
                build.Add(Ms(t1 - t0)); json.Add(Ms(t2 - t1)); refresh.Add(Ms(t2 - t0)); bytes.Add(a1 - a0); jsonBytes.Add(a2 - a1);
                same &= unchanged; len = text.Length;
            }
            int q0 = GC.CollectionCount(0) - p0, q1 = GC.CollectionCount(1) - p1, q2 = GC.CollectionCount(2) - p2;
            // the clock moves on while the book sits open: is the page still the same (the idle refresh then rebuilds nothing)?
            Shift(TimeSpan.FromSeconds(2)); var after2 = Refresh(); Shift(TimeSpan.FromSeconds(58)); var after60 = Refresh(); Shift(TimeSpan.FromSeconds(-60));
            bool same2 = after2 == first, same60 = after60 == first;
            j.Open().Str("name", shot.Name).Str("book", shot.Book).Str("chapter", shot.About ? "About" : shot.Chapter.ToString()).Str("page", shot.About ? shot.AboutPage : shot.Page ?? "").Str("view", shot.View ?? "");
            if (!same60) j.Str("change60", FirstChange(first, after60));   // what a minute changes on the page: the open book rebuilds its UI for it
            build.Write(j, "buildMs"); json.Write(j, "jsonMs"); refresh.Write(j, "refreshMs"); bytes.Write(j, "buildBytes"); jsonBytes.Write(j, "jsonBytes");
            j.Num("jsonLength", len).Key("gc").Open().Num("gen0", q0).Num("gen1", q1).Num("gen2", q2).Close()
             .Num("sameTwice", same ? 1 : 0).Num("sameAfter2s", same2 ? 1 : 0).Num("sameAfter60s", same60 ? 1 : 0).Close();
            allBuild.Add(build.Percentile(50)); allRefresh.Add(refresh.Percentile(50)); allBytes.Add(bytes.Mean); pageP95.Add(build.Percentile(95));
            rows.Add((shot.Name, build.Percentile(50), build.Percentile(95), build.Max, bytes.Mean / 1024.0, json.Percentile(50), same, same2, same60));
        }
        walk.Stop();
        j.CloseArr();
        // the open book's idle frame, the pure part of it (PanelUi.Frame + LateFrame without Unity): before 0.8 every frame asked FilterAnyOpen
        // of the whole view and worked the key line out again (a LINQ chain, a new list, a joined string, a delegate); since 0.8 the filter bar
        // is found once per view and the key line comes from KeyLineCache. Bytes per frame over 600 frames on each page, both ways.
        var idleOld = new BenchSeries(); var idleNew = new BenchSeries(); var scrollersLike = new List<object> { new object(), new object(), new object() };
        foreach (var (shot, book) in shots)
        {
            var st = StateOf(shot); var v = PanelModel.Build(book, st); var baseKeys = v.Keys.ToList(); var fitter = new KeyFitter();
            const int frames = 600;
            var a0 = GC.GetAllocatedBytesForCurrentThread();
            for (int f = 0; f < frames; f++)
            {
                PanelModel.FilterAnyOpen(st, v);
                var more = scrollersLike.Any(o => o == null);
                PanelModel.KeyLine(more ? baseKeys.Concat(new[] { PanelModel.ScrollKey }) : baseKeys, new Func<string, bool>(fitter.Fits));   // KeysFit as a method group: a delegate per frame
            }
            var a1 = GC.GetAllocatedBytesForCurrentThread();
            var bar = PanelModel.FilterOf(v); var cache = new KeyLineCache(); Func<string, bool> fits = fitter.Fits;
            for (int f = 0; f < frames; f++)
            {
                PanelModel.FilterAnyOpen(st, bar);
                var more = false; for (int i = 0; i < scrollersLike.Count && !more; i++) more = scrollersLike[i] == null;
                cache.Line(baseKeys, more, 1052f, fits);
            }
            var a2 = GC.GetAllocatedBytesForCurrentThread();
            idleOld.Add((a1 - a0) / (double)frames); idleNew.Add((a2 - a1) / (double)frames);
        }
        j.Key("idleFrame").Open().Str("what", "bytes per frame of the open book's pure per-frame work (FilterAnyOpen, the key line), 600 frames per page; old = before 0.8, new = 0.8 (the first frame works the line out once)");
        idleOld.Write(j, "oldBytesPerFrame"); idleNew.Write(j, "newBytesPerFrame"); j.Close();

        // the feats tick (FeatsTracker.Tick: every 10 s while playing, the book open or shut): your feats against your numbers now
        PanelModel.EvaluateFeats(world, now, "Meadows", null);   // the first look primes the ledger
        var feats = new BenchSeries(); var featBytes = new BenchSeries();
        for (int i = 0; i < n; i++)
        {
            var a0 = GC.GetAllocatedBytesForCurrentThread(); var t0 = Stopwatch.GetTimestamp();
            PanelModel.EvaluateFeats(world, now, "Meadows", null);
            feats.Add(Ms(Stopwatch.GetTimestamp() - t0)); featBytes.Add(GC.GetAllocatedBytesForCurrentThread() - a0);
        }
        j.Key("featsTick").Open(); feats.Write(j, "ms"); featBytes.Write(j, "bytes"); j.Close();

        // Dev.UiReuse (0.8.1, PanelReuse.cs): flipping through your own book in the in-game bench's order (chapter by chapter, page by page, view by
        // view), how many objects of the chrome each page turn keeps instead of making them again. Objects counted the way Tab, Entry and the
        // player row make them (a tab 3 + its icon + its dot; a list row 2 + an icon's 2 + a dot; a chip 2 + an icon's 2 + the colour key; the
        // Everyone chip about 10); the milliseconds at the in-game fit of 58 µs per object (Joost's PC, 0.8.0): an estimate, Dev.Bench measures.
        {
            var flip = shots.Where(x => x.shot.Book == "Rowan").Select(x => x.shot).ToList();
            int Tabs(PanelView v) => v.Chapters.Sum(c => 3 + (c.Icon != null ? 1 : 0) + (c.Dot ? 1 : 0));
            int Rows(PanelView v) => v.List.Sum(c => 2 + (!string.IsNullOrEmpty(c.Icon) ? 2 : 0) + (c.Dot ? 1 : 0));
            int Chips(PanelView v) => v.Players.Sum(c => 2 + (!string.IsNullOrEmpty(c.Icon) ? 2 : 0) + (v.EveryoneOn ? 1 : 0)) + (v.EveryoneChip ? 10 : 0);
            PanelView DrawnView(Shot sh) { var st = StateOf(sh); var v = PanelModel.Build(world, st); PanelModel.AddPlayers(v, world.PlayerName, group, st.Player, true); return v; }
            var kept = new BenchSeries(); var chrome = new BenchSeries(); int turns = 0, keptTabs = 0, keptList = 0, keptPlayers = 0;
            PanelView last = null;
            foreach (var sh in flip)
            {
                var v = DrawnView(sh);
                var (rh, gp) = PanelModel.ListRows(v.List.Count, 468);
                if (last != null)
                {
                    var (lh, lg) = PanelModel.ListRows(last.List.Count, 468);
                    int k = 0; turns++;
                    if (ChromeKeys.Tabs(v) == ChromeKeys.Tabs(last)) { k += Tabs(v); keptTabs++; }
                    if (ChromeKeys.List(v, rh, gp) == ChromeKeys.List(last, lh, lg)) { k += Rows(v); keptList++; }
                    if (ChromeKeys.Players(v, 0, v.EveryoneChip ? 3 : 5) == ChromeKeys.Players(last, 0, last.EveryoneChip ? 3 : 5)) { k += Chips(v); keptPlayers++; }
                    kept.Add(k); chrome.Add(Tabs(v) + Rows(v) + Chips(v));
                }
                last = v;
            }
            j.Key("reuse").Open().Str("what", "your own book flipped in the in-game bench's order: per page turn, the chrome objects (tabs, list, player row) Dev.UiReuse keeps; ms at 58 us per object (the in-game fit, an estimate)")
             .Num("turns", turns).Num("tabsKept", keptTabs).Num("listKept", keptList).Num("playersKept", keptPlayers);
            kept.Write(j, "objectsKept"); chrome.Write(j, "chromeObjects"); j.Num("estimatedMsSavedMean", kept.Mean * 0.058).Close();
            System.Console.WriteLine("reuse (estimate): " + turns + " page turns; tabs kept " + keptTabs + ", list kept " + keptList + ", player row kept " + keptPlayers + "; " +
                                     F(kept.Mean) + " of " + F(chrome.Mean) + " chrome objects kept per turn (median " + F(kept.Percentile(50)) + "), about " + F(kept.Mean * 0.058) + " ms at 58 us per object");
        }

        // the game's per-token counters as keys (DeedLog.GameKeys: Deeds > Recent's minute, every 2 s refresh with the book open): before 0.8.1
        // each key joined anew ("placed|" + token), since then joined once (KeyJoin). A character with 300 kinds of pieces placed, 60 picked, 40 foes.
        var placed = Enumerable.Range(0, 300).ToDictionary(i => "$piece_kind" + i, i => (float)i); var picked = Enumerable.Range(0, 60).ToDictionary(i => "Pickable_" + i, i => 1f);
        var killed = Enumerable.Range(0, 40).ToDictionary(i => "$enemy_" + i, i => 2f); var counters = new Dictionary<string, float> { ["EnemyKills"] = 9, ["FishCaught"] = 3 };
        Dictionary<string, float> OldKeys()
        {
            var d = new Dictionary<string, float>();
            foreach (var c in DeedLog.CounterStats) if (counters.TryGetValue(c, out var v)) d[DeedLog.Counters + "|" + c] = v;
            foreach (var kv in placed) d[DeedLog.Placed + "|" + kv.Key] = kv.Value;
            foreach (var kv in picked) d[DeedLog.Picked + "|" + kv.Key] = kv.Value;
            foreach (var kv in killed) d[DeedLog.Kills + "|" + kv.Key] = kv.Value;
            return d;
        }
        var keysSame = OldKeys().OrderBy(kv => kv.Key).SequenceEqual(DeedLog.GameKeys(counters, placed, picked, killed).OrderBy(kv => kv.Key));
        var oldKeyBytes = new BenchSeries(); var newKeyBytes = new BenchSeries(); var oldKeyMs = new BenchSeries(); var newKeyMs = new BenchSeries();
        for (int i = 0; i < n; i++)
        {
            var a0 = GC.GetAllocatedBytesForCurrentThread(); var t0 = Stopwatch.GetTimestamp(); OldKeys();
            var t1 = Stopwatch.GetTimestamp(); var a1 = GC.GetAllocatedBytesForCurrentThread(); DeedLog.GameKeys(counters, placed, picked, killed);
            var t2 = Stopwatch.GetTimestamp(); var a2 = GC.GetAllocatedBytesForCurrentThread();
            oldKeyBytes.Add(a1 - a0); newKeyBytes.Add(a2 - a1); oldKeyMs.Add(Ms(t1 - t0)); newKeyMs.Add(Ms(t2 - t1));
        }
        j.Key("deedKeys").Open().Num("sameKeys", keysSame ? 1 : 0).Str("what", "DeedLog.GameKeys per refresh (300 pieces, 60 picked, 40 foes): old = each key joined anew, new = KeyJoin (0.8.1)");
        oldKeyBytes.Write(j, "oldBytes"); newKeyBytes.Write(j, "newBytes"); oldKeyMs.Write(j, "oldMs"); newKeyMs.Write(j, "newMs"); j.Close();
        System.Console.WriteLine("deed keys per refresh (400 tokens): before " + F(oldKeyBytes.Percentile(50) / 1024) + " KB " + F(oldKeyMs.Percentile(50)) + " ms, now " + F(newKeyBytes.Percentile(50) / 1024) + " KB " + F(newKeyMs.Percentile(50)) + " ms; same keys " + keysSame);

        // a fellow's live update (REVIEW-07 finding 8; a copy the size of the live server's real one, BenchFellow.cs): the old path, everything on
        // the main thread as 0.7 did it per update: apply onto the copy's text (parsed again), read it for the trail, read it again for the panel
        var bf = new BenchFellow(now); var updates = new List<(long seq, string text)>(); for (int i = 0; i < 14; i++) updates.Add(bf.Next());
        var oldMs = new BenchSeries(); var oldBytes = new BenchSeries();
        for (int i = 0; i < updates.Count; i++)
        {
            var a0 = GC.GetAllocatedBytesForCurrentThread(); var t0 = Stopwatch.GetTimestamp();
            var shown = LiveDelta.Apply(bf.Stored, updates[i].text);
            var copy = PanelInput.FromSnapshot(shown); FellowTrail.Flat(copy);   // FellowTrails.Add
            PanelInput.FromSnapshot(shown);                                       // the panel's next refresh (PanelUi.Fellow)
            var t1 = Stopwatch.GetTimestamp(); var a1 = GC.GetAllocatedBytesForCurrentThread();
            if (i >= 2) { oldMs.Add(Ms(t1 - t0)); oldBytes.Add(a1 - a0); }   // the first two warm up
        }
        j.Key("fellowUpdateOldPath").Open().Num("copyBytes", bf.Stored.Length).Str("what", "per live update, main thread: LiveDelta.Apply on the copy's text, PanelInput.FromSnapshot + FellowTrail.Flat (trail), PanelInput.FromSnapshot (panel)");
        oldMs.Write(j, "mainMs"); oldBytes.Write(j, "mainBytes"); j.Close();
        System.Console.WriteLine("fellow update, old path (" + (bf.Stored.Length / 1024.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " KB copy): main thread " + F(oldMs.Percentile(50)) + " ms, " + F(oldBytes.Mean / 1024) + " KB per update");
        // 0.8 path (this code only): the copy parsed once, the update applied onto it and read on the worker (GroupShare.TakeLive), the main thread
        // takes the result (DrainLive) and the panel finds it read (FellowCopies). Main-thread time and bytes apart from the worker's (all threads).
        GroupShare.Clear();
        var fkey = GroupShare.TakeFull(bf.Stored, now);
        var newMs = new BenchSeries(); var newBytes = new BenchSeries(); var allBytes2 = new BenchSeries(); var doneMs = new BenchSeries();
        for (int i = 0; i < updates.Count; i++)
        {
            var at = now.AddSeconds(10 * (i + 1));
            var all0 = GC.GetTotalAllocatedBytes(true); var a0 = GC.GetAllocatedBytesForCurrentThread(); var t0 = Stopwatch.GetTimestamp();
            GroupShare.TakeLive(fkey, "s1-1", updates[i].seq, updates[i].text, at);
            var t1 = Stopwatch.GetTimestamp(); var a1 = GC.GetAllocatedBytesForCurrentThread();
            while (GroupShare.LivePending > 0 && !(GroupShare.LivePending == 1 && GroupShare.DrainLive() > 0)) Thread.SpinWait(50);
            var t2 = Stopwatch.GetTimestamp(); var a2 = GC.GetAllocatedBytesForCurrentThread();
            GroupShare.DrainLive();
            var shown = GroupShare.Group[fkey]; FellowCopies.Of(fkey, shown);   // the panel's next refresh
            var t3 = Stopwatch.GetTimestamp(); var a3 = GC.GetAllocatedBytesForCurrentThread(); var all1 = GC.GetTotalAllocatedBytes(true);
            if (i >= 2) { newMs.Add(Ms((t1 - t0) + (t3 - t2))); newBytes.Add((a1 - a0) + (a3 - a2)); allBytes2.Add(all1 - all0 - (a2 - a1)); doneMs.Add(Ms(t2 - t0)); }
        }
        GroupShare.Clear();
        j.Key("fellowUpdateNewPath").Open().Num("copyBytes", bf.Stored.Length).Str("what", "per live update: main thread = TakeLive + DrainLive + the panel's FellowCopies.Of; allBytes = every thread (the worker's apply and read included); doneMs = until the worker finished");
        newMs.Write(j, "mainMs"); newBytes.Write(j, "mainBytes"); allBytes2.Write(j, "allBytes"); doneMs.Write(j, "doneMs"); j.Close();
        System.Console.WriteLine("fellow update, 0.8 path: main thread " + F(newMs.Percentile(50)) + " ms, " + F(newBytes.Mean / 1024) + " KB per update; all threads " + F(allBytes2.Mean / 1024) + " KB; on screen after " + F(doneMs.Percentile(50)) + " ms");
        var unstable = rows.Where(r => !r.same).Select(r => r.name).ToList();
        var moves2 = rows.Where(r => !r.same2).Select(r => r.name).ToList();
        var moves60 = rows.Where(r => !r.same60).Select(r => r.name).ToList();
        j.Key("summary").Open().Num("pages", rows.Count).Num("walkSeconds", walk.Elapsed.TotalSeconds);
        pageP95.Write(j, "pageBuildP95Ms");   // over pages: each page's own p95
        allBuild.Write(j, "pageBuildP50Ms"); allRefresh.Write(j, "pageRefreshP50Ms"); allBytes.Write(j, "pageBuildBytesMean");
        j.Num("pagesOver2msP95", rows.Count(r => r.p95 > 2)).Key("gc").Open().Num("gen0", GC.CollectionCount(0) - g0).Num("gen1", GC.CollectionCount(1) - g1).Num("gen2", GC.CollectionCount(2) - g2).Close();
        j.Raw("notSameTwice", "[" + string.Join(",", unstable.Select(Json.Q)) + "]").Raw("changesIn2s", "[" + string.Join(",", moves2.Select(Json.Q)) + "]").Raw("changesIn60s", "[" + string.Join(",", moves60.Select(Json.Q)) + "]");
        j.Close();
        j.Close();
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, "bench-offline-" + label + ".json");
        System.IO.File.WriteAllText(path, j.ToString());

        string F(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        System.Console.WriteLine("bench " + label + ": " + rows.Count + " pages x " + n + " builds in " + F(walk.Elapsed.TotalSeconds) + " s (" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + ")");
        System.Console.WriteLine("page build p95 over pages: p50 " + F(pageP95.Percentile(50)) + " ms, p95 " + F(pageP95.Percentile(95)) + " ms, max " + F(pageP95.Max) + " ms; " + rows.Count(r => r.p95 > 2) + " pages over 2 ms; bytes per build mean " + F(allBytes.Mean / 1024) + " KB (max page " + F(allBytes.Max / 1024) + " KB)");
        System.Console.WriteLine("not the same twice: " + unstable.Count + (unstable.Count > 0 ? " (" + string.Join(", ", unstable.Take(8)) + ")" : "") + "; changes when the clock moves 2 s: " + moves2.Count + (moves2.Count > 0 ? " (" + string.Join(", ", moves2.Take(8)) + ")" : "") + "; 60 s: " + moves60.Count);
        System.Console.WriteLine("idle frame (pure part, 600 frames per page): before 0.8 " + F(idleOld.Mean) + " B/frame avg (max page " + F(idleOld.Max) + "), now " + F(idleNew.Mean) + " B/frame avg (max page " + F(idleNew.Max) + ")");
        System.Console.WriteLine("feats tick (every 10 s while playing): p50 " + F(feats.Percentile(50)) + " ms, p95 " + F(feats.Percentile(95)) + " ms, " + F(featBytes.Mean / 1024) + " KB");
        System.Console.WriteLine("slowest pages (build p50 / p95 / max ms, KB per build, json p50 ms):");
        foreach (var r in rows.OrderByDescending(r => r.p95).Take(15)) System.Console.WriteLine("  " + r.name.PadRight(44) + F(r.p50).PadLeft(7) + F(r.p95).PadLeft(7) + F(r.max).PadLeft(7) + F(r.kb).PadLeft(9) + F(r.jsonMs).PadLeft(7));
        System.Console.WriteLine("wrote " + path);
        return 0;
    }
}
