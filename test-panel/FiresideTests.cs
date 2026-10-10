// Fireside's drawing as geometry (fireside-route, 2026-10-09). Joost: "the arrows overlap because they curve too far around the fire; keep the
// fire, but route threads so they never cross or overlap; keep the numbers on the threads". For the sample world (your book and a fellow's)
// and for busy groups of four, five and six (gifts both ways on most pairs) the plan must hold: no two threads run along each other; threads
// cross only when their two pairs of players alternate around the ring (then once, at 30 degrees or more); nothing passes through the hearth,
// another shield or a name; every count sits on its own thread and touches no other count, shield, name, arrow head or the hearth.
// Near a shield (FireLayout.Hub) the threads of that shield meet, as threads tied to it. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static partial class Program
{
    /// <summary>A busy group of n (Rowan first): food both ways on most pairs, one way on some, gear one way on about a third; deterministic
    /// (the same generator as the prototype the layout was tuned with). The owner's gifts are Selected.</summary>
    internal static Block FireBusy(int n, int seed)
    {
        var names = new[] { "Rowan", "Edda", "Finch", "Tor", "Sigrun", "Halvar" }.Take(n).ToArray(); long s = seed;
        double Rnd() { s = (s * 9301 + 49297) % 233280; return s / 233280.0; }
        var items = names.Select((x, i) => new Block { Kind = "player", Id = x, Title = i == 0 ? "You" : x, Icon = "person:" + x, Selected = i == 0 }).ToList();
        void G(string from, string to, string tone, int v, bool sel) => items.Add(new Block { Kind = "gift", Id = from + ">" + to, Title = from, Text = to, Tone = tone, Value = v.ToString(), Count = v, Fraction = Math.Min(1f, v / 15f), Selected = sel, Note = tone == "gear" ? "put to good use" : "enjoyed", Icon = "item:Bread" });
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                var r = Rnd(); var sel = i == 0;
                if (r < 0.7) { G(names[i], names[j], "food", 1 + (int)Math.Floor(Rnd() * 18), sel); G(names[j], names[i], "food", 1 + (int)Math.Floor(Rnd() * 18), sel); }
                else if (r < 0.9) G(names[j], names[i], "food", 1 + (int)Math.Floor(Rnd() * 18), sel);
                if (Rnd() < 0.35) G(names[i], names[j], "gear", 1 + (int)Math.Floor(Rnd() * 4), sel);
            }
        return new Block { Kind = "giving", Items = items };
    }

    /// <summary>Everything wrong with a Fireside plan, one line each (empty = clean).</summary>
    internal static List<string> FireProblems(FireLayout.Plan plan, Block giving)
    {
        var bad = new List<string>();
        var players = giving.Items.Where(i => i.Kind == "player").ToList(); var idx = players.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var threads = plan.Drawn.Where(g => plan.Geos.ContainsKey(g)).ToList();
        if (threads.Count != plan.Drawn.Count) bad.Add((plan.Drawn.Count - threads.Count) + " gifts without a thread");
        string L(Block g) => g.Title + ">" + g.Text + "/" + g.Tone;
        double Hyp(double x, double y) => Math.Sqrt(x * x + y * y);
        for (int i = 0; i < threads.Count; i++)
            for (int j = i + 1; j < threads.Count; j++)
            {
                Block A = threads[i], B = threads[j]; var ga = plan.Geos[A]; var gb = plan.Geos[B];
                var shared = new[] { A.Title, A.Text }.Where(x => x == B.Title || x == B.Text).ToList(); var hubs = shared.Select(x => plan.Pos[x]).ToList();
                var xs = FireLayout.Crossings(ga.Pts, gb.Pts).Where(x => !hubs.Any(h => Hyp(x.at.x - h.x, x.at.y - h.y) < FireLayout.Hub)).ToList();
                int i1 = Math.Min(idx[A.Title], idx[A.Text]), j1 = Math.Max(idx[A.Title], idx[A.Text]), i2 = Math.Min(idx[B.Title], idx[B.Text]), j2 = Math.Max(idx[B.Title], idx[B.Text]);
                var alternate = shared.Count == 0 && ((i1 < i2 && i2 < j1) != (i1 < j2 && j2 < j1));
                var run = FireLayout.RunTogetherPx(ga.Pts, gb.Pts, hubs);
                if (run > FireLayout.RunTogether) bad.Add("run together " + Math.Round(run) + " px: " + L(A) + " / " + L(B));
                if (!alternate && xs.Count > 0) bad.Add("cross where they need not: " + L(A) + " / " + L(B));
                if (alternate && xs.Count > 1) bad.Add("cross " + xs.Count + " times: " + L(A) + " / " + L(B));
                foreach (var x in xs) if (x.deg < FireLayout.MinCrossDeg) bad.Add("cross at " + Math.Round(x.deg) + " degrees: " + L(A) + " / " + L(B));
            }
        var centre = (x: plan.Cx, y: -plan.Cy);
        foreach (var g in threads)
        {
            var pts = plan.Geos[g].Pts;
            if (pts.Any(p => Hyp(p.x - centre.x, p.y - centre.y) < FireLayout.Keep)) bad.Add("through the hearth: " + L(g));
            foreach (var q in players)
            {
                if (q.Id != g.Title && q.Id != g.Text && pts.Any(p => Hyp(p.x - plan.Pos[q.Id].x, p.y - plan.Pos[q.Id].y) < FireLayout.ShieldD / 2 + 4)) bad.Add("over " + q.Id + "'s shield: " + L(g));
                var r = plan.NameRect[q.Id]; if (pts.Any(p => p.x > r.X0 && p.x < r.X1 && p.y > r.Y0 && p.y < r.Y1)) bad.Add("over " + q.Id + "'s name: " + L(g));
            }
        }
        var live = plan.Pills.Where(p => !p.Dropped).ToList();
        var arrows = threads.Select(g => FireLayout.ArrowBox(plan.Geos[g])).ToList();   // each arrowhead as drawn (0.7: its size follows the count)
        for (int i = 0; i < live.Count; i++)
        {
            var r = FireLayout.Rect.Around(live[i].At.x, live[i].At.y, live[i].Width / 2, live[i].H / 2);
            var on = live[i].Thread.At(live[i].U); if (Hyp(on.x - live[i].At.x, on.y - live[i].At.y) > 0.01) bad.Add("count off its thread: " + L(live[i].Gift));
            if (plan.Fixed.Any(f => FireLayout.Overlap(r, f) > 0)) bad.Add("count on a shield, name or the hearth: " + L(live[i].Gift));
            if (arrows.Any(a => FireLayout.Overlap(r, a) > 0)) bad.Add("count on an arrow head: " + L(live[i].Gift));
            for (int j = i + 1; j < live.Count; j++) if (FireLayout.Overlap(r, FireLayout.Rect.Around(live[j].At.x, live[j].At.y, live[j].Width / 2, live[j].H / 2)) > 0) bad.Add("counts touch: " + L(live[i].Gift) + " / " + L(live[j].Gift));
        }
        foreach (var p in plan.Pills.Where(p => p.Dropped)) bad.Add("count left off the drawing: " + L(p.Gift));
        return bad;
    }

    internal static void FiresideChecks(DateTime now, Action<bool, string> Check)
    {
        Block Giving(PanelInput who, string player)
        {
            var st = new PanelState { Chapter = Chapter.Company, Player = player }; st.Page[Chapter.Company] = "fireside";
            return PanelModel.Content(PanelModel.Build(who, st)).FirstOrDefault(b => b.Kind == "giving");
        }
        var world = PanelSample.Full(now); var books = FullDump.Books(world).ToDictionary(b => b.who, b => b.book);
        var cases = new List<(string name, Block giving)> { ("your book", Giving(world, null)), ("Edda's book", Giving(books["Edda"], "Edda")),
            ("a busy four", FireBusy(4, 11)), ("a busy five", FireBusy(5, 26)), ("a busy six", FireBusy(6, 69)) };
        foreach (var (name, giving) in cases)
        {
            if (giving == null) { Check(false, "fireside geometry, " + name + ": no giving block"); continue; }
            var sw = System.Diagnostics.Stopwatch.StartNew(); var plan = FireLayout.Build(giving, 180, 232); var ms = sw.ElapsedMilliseconds;
            var again = FireLayout.Build(new Block { Kind = "giving", Items = giving.Items.ToList() }, 181, 232);   // a fresh plan (a different key), moved 1 px: the same choices
            var bad = FireProblems(plan, giving);
            var same = plan.Bundles.Select(b => b.Opt).SequenceEqual(again.Bundles.Select(b => b.Opt)) && plan.Pills.Select(p => p.U).SequenceEqual(again.Pills.Select(p => p.U));
            Check(bad.Count == 0 && same && plan.Pills.Count == giving.Items.Count(g => g.Kind == "gift" && g.Selected) && ms < 1500,
                  "fireside geometry, " + name + " (" + giving.Items.Count(i => i.Kind == "player") + " players, " + plan.Drawn.Count + " threads, " + plan.Pills.Count + " counts, " + ms + " ms): " +
                  "no thread runs along another, threads cross only where their pairs alternate (once, at 30 degrees or more), none through the hearth, a shield or a name; " +
                  "every count on its own thread touching nothing, none left off; the same plan every time" + (bad.Count > 0 ? ": " + string.Join("; ", bad.Take(6)) : "") + (same ? "" : " (not the same plan twice)"));
        }
        // beyond what the test promises: the heaviest load (you give and receive food and gear with every one of five fellow players) says what it still does
        var heavy = FireBusy(6, 48);
        var hp = FireLayout.Build(heavy, 180, 232); var hb = FireProblems(hp, heavy);
        System.Console.WriteLine("INFO fireside geometry, the busiest six (seed 48, " + hp.Drawn.Count + " threads): " + (hb.Count == 0 ? "clean" : hb.Count + " problems: " + string.Join("; ", hb.Take(4))));
        // the arrows: each along its arc's true end tangent (the baked arc is a parabola)
        var sp = FireLayout.Build(cases[0].giving, 180, 232);
        Check(sp.Geos.Values.All(t => Math.Abs(t.EndAngleDeg - t.AngleDeg - (t.BendRight ? 1 : -1) * Math.Atan(4 * (FireLayout.Arcs[t.Arc] - 8) / 240) * 180 / Math.PI) < 1e-9) &&
              sp.Geos.Values.Max(t => t.Arc) <= 1, "fireside: in the sample world no thread needs the deep arc; every arrow follows its arc's end tangent");
        // 0.7 (Joost: "why is the line so thick and the arrow so small?"): a thin line that grows with the count, 1.5 to 5 px, the arrowhead 3 times its width;
        // the head stays inside the box PlacePills keeps clear of counts (FireProblems checks no count sits on it)
        var strokeBad = new List<string>();
        foreach (var (name, giving) in cases)
        {
            if (giving == null) continue;
            var plan = FireLayout.Build(giving, 180, 232);
            var drawn = plan.Geos.Select(kv => (gift: kv.Key, geo: kv.Value, st: FireLayout.StrokeOf(kv.Value, kv.Key.Fraction))).OrderBy(x => x.gift.Fraction).ToList();
            for (int i = 0; i < drawn.Count; i++)
            {
                var (gift, geo, st) = drawn[i];
                if (st.Width < 1.5 - 1e-9 || st.Width > 5 + 1e-9) strokeBad.Add(name + ": width " + st.Width.ToString("0.00"));
                if (i > 0 && st.Width < drawn[i - 1].st.Width - 1e-9) strokeBad.Add(name + ": a larger count drew a thinner line");
                if (Math.Abs(st.ArrowLen - 3 * st.Width) > 1e-9) strokeBad.Add(name + ": arrowhead not 3 times the line");
                var box = FireLayout.ArrowBox(geo);
                if (new[] { st.Tip, st.Left, st.Right }.Any(p => p.x < box.X0 || p.x > box.X1 || p.y < box.Y0 || p.y > box.Y1)) strokeBad.Add(name + ": " + gift.Title + ">" + gift.Text + " arrowhead outside the box the counts keep clear");
                var end = st.Line[st.Line.Length - 1]; var mid = ((st.Left.x + st.Right.x) / 2, (st.Left.y + st.Right.y) / 2);
                if (Math.Abs(end.x - mid.Item1) > 1e-6 || Math.Abs(end.y - mid.Item2) > 1e-6) strokeBad.Add(name + ": the line does not stop at the arrowhead's base");
            }
            if (drawn.Count > 0 && drawn[drawn.Count - 1].gift.Fraction >= 1 && Math.Abs(drawn[drawn.Count - 1].st.Width - 5) > 1e-6) strokeBad.Add(name + ": the largest count is not 5 px");
        }
        Check(strokeBad.Count == 0, "fireside threads: line 1.5 to 5 px growing with the count (the largest 5), arrowhead 3 times the width, inside the box the counts keep clear, the line stopping at its base" +
              (strokeBad.Count > 0 ? ": " + string.Join("; ", strokeBad.Distinct().Take(6)) : ""));
    }
}
