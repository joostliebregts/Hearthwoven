using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The geometry of the Fireside drawing (Company > Fireside), kept free of Unity so PanelUi draws from it and the tests can check it.
    /// The HTML preview (preview/panel-preview.html, fireLayout) is a line-for-line port with the same constants; the dump writes this
    /// plan beside the preview data and render-check.mjs compares the two (fireside-route, 2026-10-09).
    ///
    /// Joost (fix4-rest review): "the arrows overlap because they curve too far around the fire; keep the fire, but route threads so they
    /// never cross or overlap; keep the numbers on the threads". So: the players stand on a ring around the hearth; every PAIR of players is
    /// one bundle, and all the gifts between them run side by side in lanes (each way on its own right), along one gentle baked arc that
    /// bends away from the hearth. For every bundle a few shapes are possible (shallow, medium, deep; outward or inward; lanes centred or all
    /// on the bending side; the two ways apart or nested); they are chosen together so that no thread runs along another, threads of pairs
    /// that share a player never cross, threads of two pairs cross only where they must (the four players alternate around the ring),
    /// there once and at 30 degrees or more, and nothing passes through the hearth, another shield or a name. Near a shield (Hub) the
    /// threads of that shield meet, as threads tied to it. Each count is a small dark pill ON its own thread, placed where it touches no
    /// other pill, shield, name, arrow head or the hearth.
    /// y is up and the origin is the top left of the drawing, as PanelUi has it (so the players' y is negative).
    /// </summary>
    public static class FireLayout
    {
        public const double Ring = 140, ShieldD = 40, PillH = 24, PillCompactH = 20, Start = 26, End = 30, Keep = 32, Hearth = 80, HearthX = 22, HearthY = 27,
                            LaneGap = 11, Hub = 40, NameH = 22, RunTogether = 18, MinCrossDeg = 30;
        public const int Steps = 32;
        /// <summary>The three baked arcs (ui-language/README.md): the midpoint depth in the 256 x 96 sprite, chord at y 8. The arc is a parabola.</summary>
        public static readonly double[] Arcs = { 30, 54, 84 };

        public struct Rect { public double X0, Y0, X1, Y1; public Rect(double x0, double y0, double x1, double y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; } public static Rect Around(double x, double y, double hw, double hh) => new Rect(x - hw, y - hh, x + hw, y + hh); }
        public static double Overlap(Rect a, Rect b) { var w = Math.Min(a.X1, b.X1) - Math.Max(a.X0, b.X0); var h = Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0); return w > 0 && h > 0 ? w * h : 0; }
        static double Hyp(double x, double y) => Math.Sqrt(x * x + y * y);
        public static double NameWidth(string t) => Math.Ceiling((t ?? "").Length * 9.0) + 8;
        public static double PillWidth(string text) => Math.Max(26, Math.Ceiling((text ?? "").Length * 10.6) + 12);

        /// <summary>Where each player stands: even angles from the top, the owner (first) at the top, around the hearth at (cx, -cy).</summary>
        public static Dictionary<string, (double x, double y)> Positions(IList<string> ids, double cx, double cy)
        {
            var pos = new Dictionary<string, (double x, double y)>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ids.Count; i++)
            {
                var a = Math.PI / 2 - i * 2 * Math.PI / ids.Count;
                pos[ids[i]] = (cx + Ring * Math.Cos(a), -cy + Ring * Math.Sin(a));
            }
            return pos;
        }

        /// <summary>One thread: from a's shield to b's in its lane (px to the right of travel), drawn as baked arc Arc bending right or left.</summary>
        public class Geo
        {
            public (double x, double y) Dir, Right, P0, P1, Mid; public double Len, Scale, AngleDeg, EndAngleDeg, Depth, Lane; public int Arc; public bool BendRight;
            public (double x, double y)[] Pts;
            /// <summary>The gift's count as a share of the page's largest (Block.Fraction), set once the thread is chosen: it sets the line's width and arrowhead.</summary>
            public double Fraction;
            /// <summary>The point of the thread at u (0 = where it leaves the sender's shield, 1 = where the arrow lands), on the baked parabola.</summary>
            public (double x, double y) At(double u) { var k = Depth * 4 * u * (1 - u); return (P0.x + (P1.x - P0.x) * u + Right.x * k, P0.y + (P1.y - P0.y) * u + Right.y * k); }
        }

        public static Geo Thread((double x, double y) a, (double x, double y) b, double lane, int arc, bool bendRight)
        {
            var dx = b.x - a.x; var dy = b.y - a.y; var d = Hyp(dx, dy); if (d < 1e-6) return null; dx /= d; dy /= d;
            double rx = dy, ry = -dx, s0 = Math.Sqrt(Math.Max(64, Start * Start - lane * lane)), s1 = Math.Sqrt(Math.Max(64, End * End - lane * lane));
            var g = new Geo { Dir = (dx, dy), Right = (rx, ry), Lane = lane, Arc = arc, BendRight = bendRight };
            g.P0 = (a.x + dx * s0 + rx * lane, a.y + dy * s0 + ry * lane); g.P1 = (b.x - dx * s1 + rx * lane, b.y - dy * s1 + ry * lane);
            g.Len = Hyp(g.P1.x - g.P0.x, g.P1.y - g.P0.y); if (g.Len < 20) return null;
            g.Mid = ((g.P0.x + g.P1.x) / 2, (g.P0.y + g.P1.y) / 2); g.Scale = g.Len / 240; g.AngleDeg = Math.Atan2(dy, dx) * 180 / Math.PI;
            g.Depth = (Arcs[arc] - 8) * g.Scale * (bendRight ? 1 : -1);
            g.EndAngleDeg = g.AngleDeg + Math.Atan(4 * (Arcs[arc] - 8) / 240) * 180 / Math.PI * (bendRight ? 1 : -1);   // the parabola's end tangent
            g.Pts = new (double x, double y)[Steps + 1]; for (int k = 0; k <= Steps; k++) g.Pts[k] = g.At(k / (double)Steps);
            return g;
        }

        /// <summary>
        /// How a thread is drawn (0.7, Joost: "why is the line so thick and the arrow so small?"; variant A of bar-prototypes/index-v4.html): one thin flat line
        /// along the arc, its width scaled by the count (LineMin for the smallest, LineMax for the page's largest; square root, so a 2 next to a 40 is still
        /// seen), and an arrowhead ArrowPerLine times the line's width long, so it grows with the line. The head's tip is where it always was (4 px past P1,
        /// inside the arrow box PlacePills keeps clear); the line stops at the head's base.
        /// </summary>
        public const double LineMin = 1.5, LineMax = 5, ArrowPerLine = 3;
        public static double LineWidth(double fraction) => LineMin + (LineMax - LineMin) * Math.Sqrt(Math.Max(0, Math.Min(1, fraction)));
        public class Stroke { public double Width, ArrowLen; public (double x, double y)[] Line; public (double x, double y) Tip, Left, Right; }
        public static Stroke StrokeOf(Geo g, double fraction)
        {
            var w = LineWidth(fraction); var len = ArrowPerLine * w; var half = 1.3 * w + 1;
            var a = g.EndAngleDeg * Math.PI / 180; double ex = Math.Cos(a), ey = Math.Sin(a);
            var tip = (x: g.P1.x + g.Dir.x * 4, y: g.P1.y + g.Dir.y * 4); var bas = (x: tip.x - ex * len, y: tip.y - ey * len);
            var line = g.Pts.Where(p => Hyp(p.x - tip.x, p.y - tip.y) > len + 0.5).ToList(); line.Add(bas);
            return new Stroke { Width = w, ArrowLen = len, Line = line.ToArray(), Tip = tip, Left = (bas.x - ey * half, bas.y + ex * half), Right = (bas.x + ey * half, bas.y - ex * half) };
        }

        /// <summary>The box a thread's arrowhead fills (its three points, 1 px around): no count may sit on it (PlacePills).</summary>
        public static Rect ArrowBox(Geo g)
        {
            var s = StrokeOf(g, g.Fraction); var xs = new[] { s.Tip.x, s.Left.x, s.Right.x }; var ys = new[] { s.Tip.y, s.Left.y, s.Right.y };
            return new Rect(xs.Min() - 1, ys.Min() - 1, xs.Max() + 1, ys.Max() + 1);
        }

        static double Cr((double x, double y) o, (double x, double y) p, (double x, double y) q) => (p.x - o.x) * (q.y - o.y) - (p.y - o.y) * (q.x - o.x);

        /// <summary>Where two polylines cross: the point and the angle (degrees, 0..90) at each.</summary>
        public static List<((double x, double y) at, double deg)> Crossings(IList<(double x, double y)> pa, IList<(double x, double y)> pb)
        {
            var outp = new List<((double x, double y), double)>();
            for (int i = 0; i + 1 < pa.Count; i++)
                for (int j = 0; j + 1 < pb.Count; j++)
                {
                    var a = pa[i]; var b = pa[i + 1]; var c = pb[j]; var d = pb[j + 1];
                    if (Math.Max(a.x, b.x) < Math.Min(c.x, d.x) || Math.Max(c.x, d.x) < Math.Min(a.x, b.x) || Math.Max(a.y, b.y) < Math.Min(c.y, d.y) || Math.Max(c.y, d.y) < Math.Min(a.y, b.y)) continue;
                    double d1 = Cr(c, d, a), d2 = Cr(c, d, b), d3 = Cr(a, b, c), d4 = Cr(a, b, d);
                    if (d1 * d2 < 0 && d3 * d4 < 0)
                    {
                        double t = d1 / (d1 - d2), ux = b.x - a.x, uy = b.y - a.y, vx = d.x - c.x, vy = d.y - c.y;
                        var den = Hyp(ux, uy) * Hyp(vx, vy); var cos = Math.Abs(ux * vx + uy * vy) / (den == 0 ? 1 : den);
                        outp.Add(((a.x + ux * t, a.y + uy * t), Math.Acos(Math.Min(1, cos)) * 180 / Math.PI));
                    }
                }
            return outp;
        }

        static double SegDist((double x, double y) p, (double x, double y) a, (double x, double y) b)
        {
            double vx = b.x - a.x, vy = b.y - a.y, l = vx * vx + vy * vy; var t = l != 0 ? ((p.x - a.x) * vx + (p.y - a.y) * vy) / l : 0; t = Math.Max(0, Math.Min(1, t));
            return Hyp(p.x - a.x - vx * t, p.y - a.y - vy * t);
        }
        static double PolyDist((double x, double y) p, IList<(double x, double y)> pts) { var m = double.PositiveInfinity; for (int i = 0; i + 1 < pts.Count; i++) m = Math.Min(m, SegDist(p, pts[i], pts[i + 1])); return m; }

        /// <summary>How far two threads run together: the longest stretch of pa (px) within 4 px of pb, outside the hubs.</summary>
        public static double RunTogetherPx(IList<(double x, double y)> pa, IList<(double x, double y)> pb, IList<(double x, double y)> hubs)
        {
            double run = 0, best = 0;
            for (int i = 0; i < pa.Count; i++)
            {
                var p = pa[i]; var free = !hubs.Any(h => Hyp(p.x - h.x, p.y - h.y) < Hub);
                if (free && PolyDist(p, pb) < 4) { if (i > 0) run += Hyp(p.x - pa[i - 1].x, p.y - pa[i - 1].y); best = Math.Max(best, run); } else run = 0;
            }
            return best;
        }

        /// <summary>The count that hangs on a thread: its text, width and height (compact when there was no room for the full pill), where on the thread it sits (0..1).</summary>
        public class Pill { public Geo Thread; public Block Gift; public string Text; public double Prefer = 0.5, Width, H = PillH, U; public bool Compact, Dropped; public (double x, double y) At; }

        /// <summary>A pair of players and the gifts between them, lanes left to right as seen going from A (the earlier player) to B.</summary>
        public class Bundle
        {
            public string A, B; public int Ia, Ib, I, Outward, Opt, PillsFwd, PillsBack; public bool TwoWay; public double Len;
            public List<Block> Gifts = new List<Block>(), Order; public double[] Offs; public List<int> Opts = new List<int>();
            internal (double x, double y) Pa, Pb; internal Dictionary<int, List<Geo>> Cache = new Dictionary<int, List<Geo>>(); internal Dictionary<int, double> OwnC = new Dictionary<int, double>();

            /// <summary>The lanes for an option: 0..11 all lanes on one arc (o % 3) bending outward or inward (o / 3 odd), centred on the chord or (o >= 6) all on the
            /// bending side of it; 12, 13 the two ways apart, each bending to its own right (shallow, medium); 14, 15 the two ways nested, both bending outward
            /// (inward), the way on the bending side on the medium arc and the other on the shallow one.</summary>
            public List<Geo> Lanes(int o)
            {
                if (Cache.TryGetValue(o, out var hit)) return hit;
                var m = Order.Count; var list = new List<Geo>();
                for (int k = 0; k < m; k++)
                {
                    var g = Order[k]; var fwd = g.Title == A;
                    var sideR = o < 12 ? (((o / 3) & 1) == 1 ? -Outward : Outward) : o < 14 ? (fwd ? 1 : -1) : (o == 14 ? Outward : -Outward);   // + bends to the right of A > B
                    var arc = o < 12 ? o % 3 : o < 14 ? o - 12 : ((fwd ? 1 : -1) == sideR ? 1 : 0);
                    var off = Offs[k] + (o >= 6 && o < 12 ? sideR * (m - 1) / 2.0 * LaneGap : 0);
                    var geo = fwd ? Thread(Pa, Pb, off, arc, sideR > 0) : Thread(Pb, Pa, -off, arc, sideR < 0);
                    if (geo != null) list.Add(geo);
                }
                return Cache[o] = list;
            }
        }

        /// <summary>Everything the drawing needs, from a giving block (PanelUi draws it, the tests check it).</summary>
        public class Plan
        {
            public Dictionary<string, (double x, double y)> Pos; public Dictionary<string, Rect> NameRect = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
            public List<Block> Drawn; public List<Bundle> Bundles = new List<Bundle>(); public Dictionary<Block, Geo> Geos = new Dictionary<Block, Geo>();
            public List<Pill> Pills = new List<Pill>(); public Dictionary<Pill, Block> GiftOf = new Dictionary<Pill, Block>(); public List<Rect> Fixed;
            /// <summary>Counts that found no free place even compact and are left off the drawing (their rows in the list say them).</summary>
            public int Stuck;
            public double Cx, Cy;
        }

        static readonly Dictionary<string, Plan> cache = new Dictionary<string, Plan>();

        /// <summary>The plan for a "giving" block: the players around the hearth at (cx, -cy), a thread per gift, a pill for each of YOUR gifts' counts.
        /// Worked out once per distinct block (the choice is a small search), then kept.</summary>
        public static Plan Build(Block giving, double cx, double cy)
        {
            var items = giving.Items ?? new List<Block>();
            var key = cx + "," + cy + "|" + string.Join(";", items.Select(i => i.Kind + ":" + i.Id + ":" + i.Title + ":" + i.Text + ":" + i.Tone + ":" + i.Value + ":" + (i.Selected ? 1 : 0)));
            lock (cache) if (cache.TryGetValue(key, out var hit)) return hit;
            var plan = Make(items, cx, cy);
            lock (cache) { if (cache.Count > 64) cache.Clear(); cache[key] = plan; }
            return plan;
        }

        static Plan Make(List<Block> items, double cx, double cy)
        {
            var players = items.Where(i => i.Kind == "player").ToList(); var gifts = items.Where(i => i.Kind == "gift").ToList();
            int n = players.Count; var centre = (x: cx, y: -cy);
            var plan = new Plan { Pos = Positions(players.Select(p => p.Id).ToList(), cx, cy), Cx = cx, Cy = cy };
            var pos = plan.Pos; var idx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); for (int i = 0; i < n; i++) idx[players[i].Id] = i;
            // the names: under the shield in the lower half, over it otherwise; a player on the side has the name pushed outward (the threads leave inward)
            var fw = 2 * cx;
            foreach (var p in players)
            {
                var at = pos[p.Id]; var below = -at.y > cy + 10; var w = NameWidth(string.IsNullOrEmpty(p.Title) ? p.Id : p.Title);
                var top = below ? at.y - ShieldD / 2 - 2 - NameH : at.y + ShieldD / 2 + 2;
                var nx = at.x + (at.x - cx) / Ring * w / 2; nx = Math.Max(w / 2, Math.Min(fw - w / 2, nx));
                plan.NameRect[p.Id] = new Rect(nx - w / 2, top, nx + w / 2, top + NameH);
            }
            plan.Drawn = gifts.Where(g => g.Title != g.Text && g.Title != null && g.Text != null && pos.ContainsKey(g.Title) && pos.ContainsKey(g.Text)).ToList();
            // the bundles, in the players' order
            var byKey = new Dictionary<string, Bundle>();
            foreach (var g in plan.Drawn)
            {
                int i = idx[g.Title], j = idx[g.Text]; var a = i < j ? g.Title : g.Text; var b = i < j ? g.Text : g.Title; var k = a + "|" + b;
                if (!byKey.TryGetValue(k, out var bu)) { bu = new Bundle { A = a, B = b, Ia = idx[a], Ib = idx[b] }; byKey[k] = bu; plan.Bundles.Add(bu); }
                bu.Gifts.Add(g);
            }
            plan.Bundles = plan.Bundles.OrderBy(x => x.Ia).ThenBy(x => x.Ib).ToList();
            var bundles = plan.Bundles; int nb = bundles.Count;
            for (int bi = 0; bi < nb; bi++)
            {
                var B = bundles[bi]; B.I = bi;
                var fwdG = B.Gifts.Where(g => g.Title == B.A).OrderBy(g => g.Tone ?? "", StringComparer.Ordinal).ToList();
                var back = B.Gifts.Where(g => g.Title == B.B).OrderBy(g => g.Tone ?? "", StringComparer.Ordinal).ToList(); back.Reverse();
                B.Order = back.Concat(fwdG).ToList(); var m = B.Order.Count;
                B.Offs = Enumerable.Range(0, m).Select(k => (k - (m - 1) / 2.0) * LaneGap).ToArray();
                B.Pa = pos[B.A]; B.Pb = pos[B.B];
                double dx = B.Pb.x - B.Pa.x, dy = B.Pb.y - B.Pa.y, d = Hyp(dx, dy), rx = dy / d, ry = -dx / d;
                var side = ((B.Pa.x + B.Pb.x) / 2 - centre.x) * rx + ((B.Pa.y + B.Pb.y) / 2 - centre.y) * ry;
                B.Outward = Math.Abs(side) < 1 ? 1 : side > 0 ? 1 : -1;   // bend away from the hearth; a chord through it bends to its right
                B.TwoWay = fwdG.Count > 0 && back.Count > 0; B.PillsFwd = fwdG.Count(g => g.Selected); B.PillsBack = back.Count(g => g.Selected);
                B.Len = d - Start - End;
                for (int o = 0; o < 12; o++) if (o < 6 || m > 1) B.Opts.Add(o);
                if (B.TwoWay) B.Opts.AddRange(new[] { 12, 13, 14, 15 });
            }

            double Own(Bundle B, int o)
            {
                if (B.OwnC.TryGetValue(o, out var hit)) return hit;
                double c = o < 12 ? (o % 3) * 40 + ((o / 3) & 1) * 5 + (o >= 6 ? 8 : 0) : o < 14 ? 10 + (o - 12) * 40 : 25 + (o - 14) * 5;
                // room for the counts: a lane-bundle holds about one count per 36 px of its middle; apart, each way holds its own
                double room = 0.7 * B.Len / 36, need = o < 12 ? B.PillsFwd + B.PillsBack : Math.Max(B.PillsFwd, B.PillsBack);
                if (need > room) c += 200 * (need - room);
                foreach (var L in B.Lanes(o))
                    foreach (var p in L.Pts)
                    {
                        if (Hyp(p.x - centre.x, p.y - centre.y) < Keep) c += 1e6;
                        foreach (var q in players) if (q.Id != B.A && q.Id != B.B && Hyp(p.x - pos[q.Id].x, p.y - pos[q.Id].y) < ShieldD / 2 + 4) c += 1e6;
                        foreach (var q in players) { var r = plan.NameRect[q.Id]; if (p.x > r.X0 - 2 && p.x < r.X1 + 2 && p.y > r.Y0 - 2 && p.y < r.Y1 + 2) c += 1e5; }
                    }
                return B.OwnC[o] = c;
            }
            var pairMemo = new Dictionary<long, double>();
            double Pair(Bundle B1, int o1, Bundle B2, int o2)
            {
                if (B1.I > B2.I) return Pair(B2, o2, B1, o1);
                long key = ((long)B1.I * 4096 + B2.I) * 256 + o1 * 16 + o2; if (pairMemo.TryGetValue(key, out var hit)) return hit;
                var shared = new[] { B1.A, B1.B }.Where(x => x == B2.A || x == B2.B).ToList(); var hubs = shared.Select(x => pos[x]).ToList();
                var inter = shared.Count == 0 && ((B1.Ia < B2.Ia && B2.Ia < B1.Ib) != (B1.Ia < B2.Ib && B2.Ib < B1.Ib));
                double c = 0;
                foreach (var L1 in B1.Lanes(o1))
                    foreach (var L2 in B2.Lanes(o2))
                    {
                        var xs = Crossings(L1.Pts, L2.Pts).Where(x => !hubs.Any(h => Hyp(x.at.x - h.x, x.at.y - h.y) < Hub)).ToList();
                        if (inter) { c += Math.Abs(xs.Count - 1) * 1e6; foreach (var x in xs) if (x.deg < MinCrossDeg) c += 1e5 + (MinCrossDeg - x.deg) * 1000; }
                        else c += xs.Count * 1e6;
                        if (RunTogetherPx(L1.Pts, L2.Pts, hubs) > RunTogether) c += 1e6;
                    }
                pairMemo[key] = c; return c;
            }
            double With1(int[] opt, int i, int o) { var s = Own(bundles[i], o); for (int j = 0; j < nb; j++) if (j != i) s += Pair(bundles[i], o, bundles[j], opt[j]); return s; }
            double TotalOf(int[] opt) { double s = 0; for (int i = 0; i < nb; i++) { s += Own(bundles[i], opt[i]); for (int j = i + 1; j < nb; j++) s += Pair(bundles[i], opt[i], bundles[j], opt[j]); } return s; }
            // choose: each bundle in turn takes its best option given the others, until nothing changes; then any two bundles still in conflict try every
            // pair of options together; again until nothing changes. From two starts (every bundle shallow and outward, and the same with the chords
            // through the hearth bending the other way); the cheaper wins.
            int[] Solve(int[] opt)
            {
                for (int round = 0; round < 6; round++)
                {
                    var changed = false;
                    for (int pass = 0; pass < 8; pass++)
                    {
                        var moved = false;
                        for (int i = 0; i < nb; i++)
                        {
                            int best = opt[i]; var bc = With1(opt, i, opt[i]);
                            foreach (var o in bundles[i].Opts) { var c = With1(opt, i, o); if (c < bc - 1e-6) { bc = c; best = o; } }
                            if (best != opt[i]) { opt[i] = best; moved = changed = true; }
                        }
                        if (!moved) break;
                    }
                    for (int i = 0; i < nb; i++)
                        for (int j = i + 1; j < nb; j++)
                        {
                            if (Pair(bundles[i], opt[i], bundles[j], opt[j]) < 1e5) continue;
                            double Rest(int oi, int oj)
                            {
                                var s = Own(bundles[i], oi) + Own(bundles[j], oj) + Pair(bundles[i], oi, bundles[j], oj);
                                for (int k = 0; k < nb; k++) if (k != i && k != j) s += Pair(bundles[i], oi, bundles[k], opt[k]) + Pair(bundles[j], oj, bundles[k], opt[k]);
                                return s;
                            }
                            int bi = opt[i], bj = opt[j]; var bc = Rest(bi, bj);
                            foreach (var oi in bundles[i].Opts) foreach (var oj in bundles[j].Opts) { var c = Rest(oi, oj); if (c < bc - 1e-6) { bc = c; bi = oi; bj = oj; } }
                            if (bi != opt[i] || bj != opt[j]) { opt[i] = bi; opt[j] = bj; changed = true; }
                        }
                    if (!changed) break;
                }
                return opt;
            }
            var startA = Solve(new int[nb]); var startB = Solve(bundles.Select(B => B.Outward == 1 && Math.Abs(B.Ib - B.Ia) * 2 == n ? 3 : 0).ToArray());
            var chosen = TotalOf(startB) < TotalOf(startA) - 1e-6 ? startB : startA;
            for (int bi = 0; bi < nb; bi++)
            {
                var B = bundles[bi]; B.Opt = chosen[bi]; var lanes = B.Lanes(B.Opt);
                for (int k = 0; k < B.Order.Count && k < lanes.Count; k++) { plan.Geos[B.Order[k]] = lanes[k]; lanes[k].Fraction = B.Order[k].Fraction; }   // the count sets the line and its arrowhead (StrokeOf)
            }
            // the counts of your gifts; where each would like to sit: the counts of one pair spread evenly along it, in lane order, so neighbouring lanes never want the same spot
            foreach (var B in bundles)
            {
                var mine = B.Order.Where(g => g.Selected && plan.Geos.ContainsKey(g)).ToList(); var m = mine.Count; var step = Math.Min(0.22, 64 / Math.Max(1, B.Len));
                for (int k = 0; k < m; k++)
                {
                    var g = mine[k]; var text = g.Value ?? ""; var along = (k - (m - 1) / 2.0) * step;
                    plan.Pills.Add(new Pill { Thread = plan.Geos[g], Gift = g, Text = text, Prefer = 0.5 + (g.Title == B.A ? along : -along), Width = PillWidth(text) });
                }
            }
            plan.Pills = plan.Pills.OrderBy(p => plan.Drawn.IndexOf(p.Gift)).ToList();
            foreach (var p in plan.Pills) plan.GiftOf[p] = p.Gift;
            plan.Fixed = new List<Rect> { Rect.Around(cx, -cy, HearthX, HearthY) };
            foreach (var p in players) { var at = pos[p.Id]; plan.Fixed.Add(Rect.Around(at.x, at.y, ShieldD / 2 + 2, ShieldD / 2 + 2)); plan.Fixed.Add(plan.NameRect[p.Id]); }
            plan.Stuck = PlacePills(plan.Pills, plan.Fixed, plan.Drawn.Where(g => plan.Geos.ContainsKey(g)).Select(g => plan.Geos[g]).ToList());
            return plan;
        }

        /// <summary>
        /// Every count's place along its own thread: u 0.1..0.9 in steps of 0.02; nothing may touch a shield, a name, the hearth, an arrow head or another count
        /// (cost 1 000 000 plus the overlap); of the free places the one nearest where it would like to sit and off other threads. The shortest threads choose
        /// first, six passes. A count with no free place is made compact (16 px digits on a 20 px pill) and all are placed again; one that still finds none is
        /// left off the drawing (its row in the list beside it says it). Returns how many were left off.
        /// </summary>
        public static int PlacePills(IList<Pill> pills, IList<Rect> fixedRects, IList<Geo> threads)
        {
            int n = pills.Count; if (n == 0) return 0;
            var arrows = threads.Select(ArrowBox).ToList();   // the arrow head's own box
            var us = Enumerable.Range(0, 41).Select(k => 0.1 + 0.02 * k).ToArray(); var chosen = Enumerable.Repeat(-1, n).ToArray();
            foreach (var p in pills) { p.H = PillH; p.Compact = false; p.Dropped = false; }
            Rect RectAt(int i, double u) { var p = pills[i].Thread.At(u); return Rect.Around(p.x, p.y, pills[i].Width / 2, pills[i].H / 2); }
            double Cost(int i, int k)
            {
                var u = us[k]; var r = RectAt(i, u); double hard = 0;
                foreach (var f in fixedRects) hard += Overlap(r, f);
                foreach (var a in arrows) hard += Overlap(r, a);
                for (int j = 0; j < n; j++) if (j != i && chosen[j] >= 0 && !pills[j].Dropped) { var o = RectAt(j, us[chosen[j]]); hard += Overlap(r, new Rect(o.X0 - 3, o.Y0 - 3, o.X1 + 3, o.Y1 + 3)); }
                var soft = Math.Abs(u - pills[i].Prefer) * 120;
                foreach (var t in threads)
                {
                    if (ReferenceEquals(t, pills[i].Thread)) continue;
                    foreach (var q in t.Pts) if (q.x >= r.X0 - 3 && q.x <= r.X1 + 3 && q.y >= r.Y0 - 3 && q.y <= r.Y1 + 3) { soft += 30; break; }
                }
                return (hard > 0 ? 1000000 + hard : 0) + soft;
            }
            var order = Enumerable.Range(0, n).OrderBy(i => pills[i].Thread.Len).ThenBy(i => i).ToList();   // the shortest threads choose first: they have the fewest places
            void Passes()
            {
                for (int pass = 0; pass < 6; pass++)
                    foreach (var i in order)
                    {
                        if (pills[i].Dropped) continue;
                        int best = -1; var bc = double.MaxValue;
                        for (int k = 0; k < us.Length; k++) { var c = Cost(i, k); if (c < bc) { bc = c; best = k; } }
                        chosen[i] = best;
                    }
            }
            List<int> StuckOnes() => order.Where(i => !pills[i].Dropped && Cost(i, chosen[i]) >= 1000000).ToList();
            Passes();
            var st = StuckOnes();
            if (st.Count > 0) { foreach (var i in st) { var p = pills[i]; p.Compact = true; p.H = PillCompactH; p.Width = Math.Max(22, Math.Ceiling(p.Text.Length * 8.9) + 10); } Passes(); st = StuckOnes(); }
            foreach (var i in st) if (Cost(i, chosen[i]) >= 1000000) pills[i].Dropped = true;   // one by one: leaving one off may free another
            int dropped = 0;
            for (int i = 0; i < n; i++) { pills[i].U = us[chosen[i]]; pills[i].At = pills[i].Thread.At(pills[i].U); if (pills[i].Dropped) dropped++; }
            return dropped;
        }

        /// <summary>The plan as JSON for the preview bridge (render-check.mjs compares it with the HTML port's own plan).</summary>
        public static string ToJson(Plan plan)
        {
            string N(double v) => Math.Round(v, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
            var threads = plan.Drawn.Where(g => plan.Geos.ContainsKey(g)).Select(g => { var t = plan.Geos[g];
                return "{\"gift\":\"" + Esc(g.Title + ">" + g.Text + "/" + g.Tone) + "\",\"arc\":" + t.Arc + ",\"bend\":" + (t.BendRight ? 1 : 0) + ",\"p0\":[" + N(t.P0.x) + "," + N(t.P0.y) + "],\"p1\":[" + N(t.P1.x) + "," + N(t.P1.y) + "],\"w\":" + N(LineWidth(g.Fraction)) + "}"; });
            var pills = plan.Pills.Select(p => "{\"gift\":\"" + Esc(p.Gift.Title + ">" + p.Gift.Text + "/" + p.Gift.Tone) + "\",\"u\":" + N(p.U) + ",\"compact\":" + (p.Compact ? 1 : 0) + ",\"dropped\":" + (p.Dropped ? 1 : 0) + "}");
            return "{\"threads\":[" + string.Join(",", threads) + "],\"pills\":[" + string.Join(",", pills) + "]}";
        }
    }
}
