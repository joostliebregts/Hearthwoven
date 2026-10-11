// Fireside's drawing as geometry (fireside-route, 2026-10-09). Joost: "the arrows overlap because they curve too far around the fire; keep the
// fire, but route threads so they never cross or overlap; keep the numbers on the threads". For the sample world (your book and a fellow's)
// and for busy groups of four, five and six (gifts both ways on most pairs) the plan must hold: no two threads run along each other; threads
// cross only when their two pairs of players alternate around the ring (then once, at 30 degrees or more); nothing passes through the hearth,
// another shield or a name; every count sits on its own thread and touches no other count, shield, name, arrow head or the hearth.
// Near a shield (FireLayout.Hub) the threads of that shield meet, as threads tied to it. 0.8.5 (Joost in game, four players: "arrows are kinda
// wonky"): every arrow's tip on its receiver's rim along the curve's end, each way of a pair on its right, no count on a crossing, and in a group
// of four no count over the other way of its pair; Joost's evening of 10 Oct (FireFour, sample names) is one of the groups. Called from Program.cs.
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

    /// <summary>Joost's evening of 2026-10-10 (four players, "arrows are kinda wonky"), sample names: Rowan as you at the top, Edda right, Finch at the
    /// bottom, Tor left. You set out Mountains feasts for Edda (5) and Finch (1), gave Tor 6 servings and 29 kinds of gear; Edda set out a feast for
    /// you (6), Finch gave you gear (14), Tor fed you (27); and a few gifts among the fellows. The feast lines carry the list's longest words.</summary>
    internal static Block FireFour()
    {
        var names = new[] { "Rowan", "Edda", "Finch", "Tor" };
        var items = names.Select((x, i) => new Block { Kind = "player", Id = x, Title = i == 0 ? "You" : x, Text = i == 0 ? "You" : x, Icon = "person:" + x, Selected = i == 0 }).ToList();
        var gifts = new List<(string from, string to, string tone, int n, bool feast)> {
            ("Rowan", "Edda", "food", 5, true), ("Rowan", "Finch", "food", 1, true), ("Rowan", "Tor", "food", 6, false), ("Rowan", "Tor", "gear", 29, false),
            ("Edda", "Rowan", "food", 6, true), ("Finch", "Rowan", "gear", 14, false), ("Tor", "Rowan", "food", 27, false),
            ("Edda", "Finch", "food", 4, false), ("Tor", "Edda", "food", 8, false), ("Finch", "Tor", "gear", 3, false) };
        foreach (var (from, to, tone, n, feast) in gifts)
            items.Add(new Block { Kind = "gift", Id = from + ">" + to, Title = from, Text = to, Tone = tone, Value = n.ToString(), Count = n, Fraction = n / 29f,
                                  Selected = from == "Rowan" || to == "Rowan", Note = tone == "gear" ? "put to good use" : "enjoyed", Icon = tone == "gear" ? "item:AxeBronze" : feast ? "item:FeastMountains" : "item:CookedMeat",
                                  Items = feast ? new List<Block> { new Block { Kind = "giftline", Icon = "item:FeastMountains", Title = PanelModel.SetOutFeast(new[] { "FeastMountains" }), Value = n.ToString(), Note = "enjoyed" } } : null });
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
            // 0.8.5 (Joost in game: "two sit on top of crossings"): no count covers a point where two threads cross
            for (int a = 0; a < threads.Count; a++) for (int b = a + 1; b < threads.Count; b++)
                    foreach (var x in FireLayout.Crossings(plan.Geos[threads[a]].Pts, plan.Geos[threads[b]].Pts))
                        if (x.at.x > r.X0 && x.at.x < r.X1 && x.at.y > r.Y0 && x.at.y < r.Y1) bad.Add("count on a crossing: " + L(live[i].Gift) + " (" + L(threads[a]) + " x " + L(threads[b]) + ")");
        }
        // 0.8.5 (Joost in game, four players: "unclear which thread a number belongs to"): in a group of four a count never covers the other way of its
        // own pair (the two ways bow apart; a busier ring draws a crowded pair's threads closer together, so there it may)
        foreach (var p in players.Count <= 4 ? live : new List<FireLayout.Pill>())
        {
            var r = FireLayout.Rect.Around(p.At.x, p.At.y, p.Width / 2, p.H / 2);
            foreach (var o in threads.Where(o => o.Title == p.Gift.Text && o.Text == p.Gift.Title))
                if (FireLayout.SegmentsIn(plan.Geos[o].Pts, r)) bad.Add("count over the other way: " + L(p.Gift) + " on " + L(o));
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
            ("Joost's four (10 Oct)", FireFour()), ("a busy four", FireBusy(4, 11)), ("a busy five", FireBusy(5, 26)), ("a busy six", FireBusy(6, 69)) };
        foreach (var (name, giving) in cases)
        {
            if (giving == null) { Check(false, "fireside geometry, " + name + ": no giving block"); continue; }
            var sw = System.Diagnostics.Stopwatch.StartNew(); var plan = FireLayout.Build(giving, 180, 232); var ms = sw.ElapsedMilliseconds;
            var again = FireLayout.Build(new Block { Kind = "giving", Items = giving.Items.ToList() }, 180, 233);   // a fresh plan (a different key), moved 1 px down: the same choices (0.8.5: down, not right; a wider drawing moves the right-hand names by 2 px, so it is not the same drawing moved)
            var bad = FireProblems(plan, giving);
            var same = plan.Bundles.Select(b => b.Bend).SequenceEqual(again.Bundles.Select(b => b.Bend)) && plan.Pills.Select(p => p.U).SequenceEqual(again.Pills.Select(p => p.U));
            Check(bad.Count == 0 && same && plan.Pills.Count == giving.Items.Count(g => g.Kind == "gift" && g.Selected) && ms < 1500,
                  "fireside geometry, " + name + " (" + giving.Items.Count(i => i.Kind == "player") + " players, " + plan.Drawn.Count + " threads, " + plan.Pills.Count + " counts, " + ms + " ms): " +
                  "no thread runs along another, threads cross only where their pairs alternate (once, at 30 degrees or more), none through the hearth, a shield or a name; " +
                  "every count on its own thread touching nothing (no crossing either), none left off; the same plan every time" + (bad.Count > 0 ? ": " + string.Join("; ", bad.Take(6)) : "") + (same ? "" : " (not the same plan twice)"));
        }
        // beyond what the test promises: the heaviest load (you give and receive food and gear with every one of five fellow players) says what it still does
        var heavy = FireBusy(6, 48);
        var hp = FireLayout.Build(heavy, 180, 232); var hb = FireProblems(hp, heavy);
        System.Console.WriteLine("INFO fireside geometry, the busiest six (seed 48, " + hp.Drawn.Count + " threads): " + (hb.Count == 0 ? "clean" : hb.Count + " problems: " + string.Join("; ", hb.Take(4))));
        // 0.8.5 (Joost in game: "arrows are kinda wonky": heads ending short or past the shield, pointing sideways): every thread leaves its giver's rim
        // and its arrow's tip sits on its receiver's rim (FireLayout.Rim: the shield's radius and the gap), the head along the curve's end tangent (the
        // drawn line's last piece) and so into the shield, at most MaxOffRadial off its middle (in a group of four, 20 degrees: none points past
        // the middle); the line leaves its giver as near straight out of it
        var rimBad = new List<string>();
        foreach (var (name, giving) in cases)
        {
            if (giving == null) continue;
            var plan = FireLayout.Build(giving, 180, 232);
            foreach (var kv in plan.Geos)
            {
                var g = kv.Key; var t = kv.Value; var st = FireLayout.StrokeOf(t, g.Fraction); var to = plan.Pos[g.Text]; var from = plan.Pos[g.Title];
                double D((double x, double y) a, (double x, double y) b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
                if (Math.Abs(D(st.Tip, to) - FireLayout.Rim) > 1e-6) rimBad.Add(name + ": " + g.Title + ">" + g.Text + " tip " + D(st.Tip, to).ToString("0.0") + " px from the middle");
                if (Math.Abs(D(t.P0, from) - FireLayout.Rim) > 1e-6) rimBad.Add(name + ": " + g.Title + ">" + g.Text + " leaves " + D(t.P0, from).ToString("0.0") + " px from the middle");
                double Deg((double x, double y) a, (double x, double y) b) => Math.Acos(Math.Max(-1, Math.Min(1, (a.x * b.x + a.y * b.y) / (Math.Sqrt(a.x * a.x + a.y * a.y) * Math.Sqrt(b.x * b.x + b.y * b.y))))) * 180 / Math.PI;
                var head = (x: st.Tip.x - (st.Left.x + st.Right.x) / 2, y: st.Tip.y - (st.Left.y + st.Right.y) / 2);
                var n = t.Pts.Length; var tangent = (x: t.Pts[n - 1].x - t.Pts[n - 2].x, y: t.Pts[n - 1].y - t.Pts[n - 2].y);   // the curve's own last piece
                var into = (x: to.x - st.Tip.x, y: to.y - st.Tip.y); var last = st.Line[st.Line.Length - 2]; var piece = (x: st.Line[st.Line.Length - 1].x - last.x, y: st.Line[st.Line.Length - 1].y - last.y);
                if (Deg(head, tangent) > 2) rimBad.Add(name + ": " + g.Title + ">" + g.Text + " head " + Deg(head, tangent).ToString("0") + " degrees off the curve's end");
                if (Deg(head, piece) > 6) rimBad.Add(name + ": " + g.Title + ">" + g.Text + " head " + Deg(head, piece).ToString("0") + " degrees off the line's last piece");
                if (Deg(head, into) > (plan.Pos.Count <= 4 ? 20 : FireLayout.MaxOffRadial + 1)) rimBad.Add(name + ": " + g.Title + ">" + g.Text + " head " + Deg(head, into).ToString("0") + " degrees off the shield's middle");
                var lx = t.Pts[1].x - t.P0.x; var ly = t.Pts[1].y - t.P0.y; var cos = (lx * (t.P0.x - from.x) + ly * (t.P0.y - from.y)) / (Math.Sqrt(lx * lx + ly * ly) * FireLayout.Rim);
                if (cos < Math.Cos((FireLayout.MaxOffRadial + 2) * Math.PI / 180)) rimBad.Add(name + ": " + g.Title + ">" + g.Text + " leaves its giver askew");
            }
        }
        Check(rimBad.Count == 0, "fireside arrows: every thread leaves its giver's rim and every arrow's tip sits on its receiver's rim (" + FireLayout.Rim + " px from the middle), the head along the curve's end, into the shield" +
              (rimBad.Count > 0 ? ": " + string.Join("; ", rimBad.Take(6)) : ""));
        // the rule of the road: in every pair, each way keeps to its right (A > B on the right of A > B, so the two ways never touch); food and gear going
        // the same way are two close lanes, food on the inside; measured at each thread's middle, across the chord
        var roadBad = new List<string>();
        foreach (var (name, giving) in cases)
        {
            if (giving == null) continue;
            var plan = FireLayout.Build(giving, 180, 232);
            foreach (var B in plan.Bundles)
            {
                var a = plan.Pos[B.A]; var b = plan.Pos[B.B]; double dx = b.x - a.x, dy = b.y - a.y, d = Math.Sqrt(dx * dx + dy * dy), rx = dy / d, ry = -dx / d;
                double Side(Block g) { var m = plan.Geos[g].At(0.5); return (m.x - a.x) * rx + (m.y - a.y) * ry; }
                var fwd = B.Lanes.Where(g => g.Title == B.A).ToList(); var back = B.Lanes.Where(g => g.Title == B.B).ToList();
                if (fwd.Count > 0 && back.Count > 0 && fwd.Min(Side) <= back.Max(Side)) roadBad.Add(name + ": " + B.A + "/" + B.B + " the two ways are not each on their right");
                foreach (var way in new[] { (fwd, 1), (back, -1) })
                {
                    var food = way.Item1.FirstOrDefault(g => g.Tone == "food"); var gear = way.Item1.FirstOrDefault(g => g.Tone == "gear");
                    if (food != null && gear != null && (Side(gear) - Side(food)) * way.Item2 <= 0) roadBad.Add(name + ": " + food.Title + ">" + food.Text + " gear inside its food");
                }
            }
        }
        Check(roadBad.Count == 0, "fireside lanes: in every pair each way keeps to its right (the two ways never on one line), food and gear going one way side by side, food on the inside" +
              (roadBad.Count > 0 ? ": " + string.Join("; ", roadBad.Take(6)) : ""));
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
