using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The geometry of the Fireside drawing (Company > Fireside), kept free of Unity so PanelUi draws from it and the tests can check it.
    /// The preview draws the same plan: the dump writes it beside the page data (ToJson) and panel-preview.html draws exactly that (0.8.5).
    ///
    /// 0.8.5 (Joost in game, four players: "arrows are kinda wonky": heads short of the shield or past it, threads bunched near You, counts
    /// between threads, a count on a crossing). One rule, like a road. The players stand on a ring around the hearth; every PAIR of players is
    /// one corridor that bends away from the hearth by the same angle (BaseBend; more where the hearth is in the way, less where it would pass
    /// near a third shield or over a name; a pair straight across the hearth bends to its quieter side). In it every thread keeps to its right:
    /// A > B and B > A leave and arrive side by side and bow apart in the middle (WaySplit), so they never touch and a count on one stays off
    /// the other; food and gear going the same way are two close lanes, food on the inside. Each thread is one smooth curve (a cubic) from its
    /// own port on the giver's rim to the arrow's tip on the receiver's rim (Rim: the shield's radius and Gap), the head along the curve's end
    /// into the shield. Around each shield the pairs keep the order of the players they lead to, clear of the name, and are spread (the least
    /// movement) until no two lines or heads touch; each pair's ports lie side by side. Each count is a small dark pill ON its own thread, at
    /// its middle; the counts going one way step along it in lane order, and any count is nudged along its thread off another count, a shield,
    /// a name, an arrow head, the hearth, a crossing or the other way of its pair. A busy ring (five or six, gifts both ways on most pairs)
    /// draws a crowded pair's threads closer together (Tight) before it bends them less.
    /// y is up and the origin is the top left of the drawing, as PanelUi has it (so the players' y is negative).
    /// </summary>
    public static class FireLayout
    {
        public const double Ring = 140, ShieldD = 40, Gap = 3, PillH = 24, PillCompactH = 20, Keep = 32, Hearth = 80, HearthX = 22, HearthY = 27,
                            Hub = 40, NameH = 22, RunTogether = 18, MinCrossDeg = 30, BaseBend = 26, BendStep = 6, MaxBend = 80, MinBend = 8;
        /// <summary>Where a thread leaves its giver and where its arrow's tip touches its receiver: this far from the shield's middle.</summary>
        public const double Rim = ShieldD / 2 + Gap;
        /// <summary>How far a pair's threads keep from a third player's middle: its shield, the heads arriving there and room for a count on the thread.</summary>
        public const double Clear = ShieldD / 2 + 16;
        /// <summary>The room between neighbouring ports on a shield (px, beyond the lines' and heads' own half widths): food and gear going the
        /// same way, the two ways of one pair, two pairs.</summary>
        public const double SameWay = 3, OtherWay = 3, OtherPair = 5;
        /// <summary>The two ways of a pair bow apart: each leaves and arrives a little to its own right, so that its middle lies WaySplit px further
        /// to its right (a count on one stays off the other); at most MaxSplit degrees.</summary>
        public const double WaySplit = 6, MaxSplit = 9;
        /// <summary>A thread leaves and arrives at most MaxOffRadial degrees off its port's radius, so every head points into its shield (its line
        /// passes the middle closer than the shield's edge). In a group of four it is under 20.</summary>
        public const double MaxOffRadial = 50;
        /// <summary>How far round a shield its ports may go from the way in to the hearth (degrees: never behind it).</summary>
        public const double Front = 100;
        /// <summary>The ports of a pair lie side by side round the rim; a thread leaves (and arrives) Radial of the way from the way its pair wants to
        /// go (its bend, where the pairs beside it pushed its ports off) to its own port's radius: straight out of the shield at 1, along its
        /// corridor at 0, so a pair's threads neither fan out nor run in parallel.</summary>
        public const double Radial = 0.5;
        /// <summary>A pair in the way of another (near a third shield, over a name, crossing or running along a thread it need not) first draws its threads
        /// closer together, in Tightest steps (less bowed apart, less fanned out), and only then bends less.</summary>
        public const int Tightest = 3;
        /// <summary>How far a shield's ports keep from its name (px, round the name's box).</summary>
        public const double NameMargin = 3;
        /// <summary>A thread's last stretch is straight, along its end tangent, for HeadRoom px: room for the longest head (ArrowPerLine x LineMax) and 2,
        /// so the line runs into its head without a kink.</summary>
        public const double HeadRoom = 17;
        public const int Steps = 48;

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

        /// <summary>One thread as drawn: one curve from its port on the giver's rim (P0, at angle PhiFrom round the giver) to the arrow's tip on the
        /// receiver's rim (P3, at PhiTo). Pts is the line, At(u) the point at the share u of its length, EndDir the way it arrives.</summary>
        public class Geo
        {
            public (double x, double y) P0, P3, EndDir; public double PhiFrom, PhiTo, Len;
            public (double x, double y)[] Pts; internal double[] Cum;
            /// <summary>The gift's count as a share of the page's largest (Block.Fraction), set once the thread is chosen: it sets the line's width and arrowhead.</summary>
            public double Fraction;
            /// <summary>The point of the thread at u (0 = where it leaves the giver's rim, 1 = the arrow's tip on the receiver's rim), by length along it.</summary>
            public (double x, double y) At(double u)
            {
                var s = Math.Max(0, Math.Min(1, u)) * Len; int i = 1;
                while (i < Pts.Length - 1 && Cum[i] < s) i++;
                var seg = Cum[i] - Cum[i - 1]; var t = seg > 1e-12 ? (s - Cum[i - 1]) / seg : 0;
                return (Pts[i - 1].x + (Pts[i].x - Pts[i - 1].x) * t, Pts[i - 1].y + (Pts[i].y - Pts[i - 1].y) * t);
            }
        }

        static (double x, double y) Cubic((double x, double y) a, (double x, double y) b, (double x, double y) c, (double x, double y) d, double u)
        {
            double v = 1 - u, k0 = v * v * v, k1 = 3 * v * v * u, k2 = 3 * v * u * u, k3 = u * u * u;
            return (k0 * a.x + k1 * b.x + k2 * c.x + k3 * d.x, k0 * a.y + k1 * b.y + k2 * c.y + k3 * d.y);
        }

        /// <summary>One thread: a cubic from the port at phiFrom on the giver's rim to the one at phiTo on the receiver's, leaving along dirFrom and
        /// arriving along dirTo (about out of and into the shield: Radial, MaxOffRadial), both turned a little to its own right when the pair has
        /// two ways (so its middle lies split px to the right: the two ways bow apart). Each handle is as long as a circle's arc of that turn needs
        /// (chord / 3 cos^2 of half the turn), at most 0.6 of the chord, so a thread is an even bend with no kink; its last HeadRoom px run
        /// straight along the head.</summary>
        public static Geo Lane((double x, double y) giver, double phiFrom, double dirFrom, (double x, double y) receiver, double phiTo, double dirTo, double split)
        {
            var g = new Geo { PhiFrom = phiFrom, PhiTo = phiTo, P0 = (giver.x + Math.Cos(phiFrom) * Rim, giver.y + Math.Sin(phiFrom) * Rim), P3 = (receiver.x + Math.Cos(phiTo) * Rim, receiver.y + Math.Sin(phiTo) * Rim) };
            double cx = g.P3.x - g.P0.x, cy = g.P3.y - g.P0.y, c = Hyp(cx, cy);
            double Handle(double th, double dx, double dy)
            {
                var turn = Math.Acos(Math.Max(-1, Math.Min(1, (Math.Cos(th) * dx + Math.Sin(th) * dy) / c)));
                var k = Math.Cos(turn / 2); return Math.Min(0.6 * c, c / (3 * k * k));
            }
            // the turn that moves the middle split px: three quarters of a handle's sideways step (the cubic's middle is (P0 + 3 Q1 + 3 Q2 + P3) / 8)
            var turnR = split <= 0 ? 0 : Math.Min(MaxSplit * Math.PI / 180, Math.Asin(Math.Min(1, split / (0.75 * Math.Max(1, 0.5 * (Handle(dirFrom, cx, cy) + Handle(dirTo, -cx, -cy)))))));
            double Off(double a) => Math.Max(-MaxOffRadial * Math.PI / 180, Math.Min(MaxOffRadial * Math.PI / 180, Wrap(a)));
            double a0 = phiFrom + Off(dirFrom - turnR - phiFrom), a1 = phiTo + Off(dirTo + turnR - phiTo);   // to its right: clockwise as it leaves, and as it arrives
            var e = (x: g.P3.x + Math.Cos(a1) * HeadRoom, y: g.P3.y + Math.Sin(a1) * HeadRoom);   // where the straight last stretch begins
            double h0 = Handle(a0, cx, cy), h1 = Math.Max(0, Handle(a1, -cx, -cy) - HeadRoom);
            var q1 = (g.P0.x + Math.Cos(a0) * h0, g.P0.y + Math.Sin(a0) * h0); var q2 = (e.x + Math.Cos(a1) * h1, e.y + Math.Sin(a1) * h1);
            g.EndDir = (-Math.Cos(a1), -Math.Sin(a1));
            g.Pts = new (double x, double y)[Steps + 2]; for (int k = 0; k <= Steps; k++) g.Pts[k] = Cubic(g.P0, q1, q2, e, k / (double)Steps);
            g.Pts[Steps + 1] = g.P3;
            g.Cum = new double[g.Pts.Length];
            for (int k = 1; k < g.Pts.Length; k++) g.Cum[k] = g.Cum[k - 1] + Hyp(g.Pts[k].x - g.Pts[k - 1].x, g.Pts[k].y - g.Pts[k - 1].y);
            g.Len = g.Cum[g.Pts.Length - 1];
            return g;
        }

        /// <summary>
        /// How a thread is drawn (0.7, Joost: "why is the line so thick and the arrow so small?"; variant A of bar-prototypes/index-v4.html): one thin flat line
        /// along the curve, its width scaled by the count (LineMin for the smallest, LineMax for the page's largest; square root, so a 2 next to a 40 is still
        /// seen), and an arrowhead ArrowPerLine times the line's width long, so it grows with the line. The head's tip is the thread's end on the receiver's
        /// rim (0.8.5), the head along the end tangent; the line stops at the head's base.
        /// </summary>
        public const double LineMin = 1.5, LineMax = 5, ArrowPerLine = 3;
        public static double LineWidth(double fraction) => LineMin + (LineMax - LineMin) * Math.Sqrt(Math.Max(0, Math.Min(1, fraction)));
        public static double HeadHalf(double width) => 1.3 * width + 1;
        public class Stroke { public double Width, ArrowLen; public (double x, double y)[] Line; public (double x, double y) Tip, Left, Right; }
        public static Stroke StrokeOf(Geo g, double fraction)
        {
            var w = LineWidth(fraction); var len = ArrowPerLine * w; var half = HeadHalf(w);
            var (ex, ey) = g.EndDir;
            var tip = g.P3; var bas = (x: tip.x - ex * len, y: tip.y - ey * len);
            var line = g.Pts.Where(p => Hyp(p.x - tip.x, p.y - tip.y) > len + 0.5).ToList(); line.Add(bas);
            return new Stroke { Width = w, ArrowLen = len, Line = line.ToArray(), Tip = tip, Left = (bas.x - ey * half, bas.y + ex * half), Right = (bas.x + ey * half, bas.y - ex * half) };
        }

        /// <summary>The box a thread's arrowhead fills (its three points, 1 px around): no count may sit on it (PlacePills).</summary>
        public static Rect ArrowBox(Geo g)
        {
            var s = StrokeOf(g, g.Fraction); var xs = new[] { s.Tip.x, s.Left.x, s.Right.x }; var ys = new[] { s.Tip.y, s.Left.y, s.Right.y };
            return new Rect(xs.Min() - 1, ys.Min() - 1, xs.Max() + 1, ys.Max() + 1);
        }

        /// <summary>Whether a polyline passes through a box (a point of it inside, or a piece of it through it).</summary>
        public static bool SegmentsIn(IList<(double x, double y)> pts, Rect r)
        {
            bool In((double x, double y) p) => p.x > r.X0 && p.x < r.X1 && p.y > r.Y0 && p.y < r.Y1;
            var corners = new[] { (r.X0, r.Y0), (r.X1, r.Y0), (r.X1, r.Y1), (r.X0, r.Y1) };
            for (int i = 0; i < pts.Count; i++)
            {
                if (In(pts[i])) return true;
                if (i + 1 < pts.Count) for (int k = 0; k < 4; k++) if (Crossings(new[] { pts[i], pts[i + 1] }, new[] { corners[k], corners[(k + 1) % 4] }).Count > 0) return true;
            }
            return false;
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
        public class Pill { public Geo Thread; public Block Gift; public string Text; public double Prefer = 0.5, Width, H = PillH, U; public bool Compact, Dropped; public (double x, double y) At;
                            /// <summary>The threads of its pair going the other way: a count keeps off them where it can.</summary>
                            public List<Geo> Others = new List<Geo>(); }

        /// <summary>A pair of players and the gifts between them (a corridor): A the earlier player, Side +1 when it bends to the right of A > B (away
        /// from the hearth), Lanes left to right as seen going from A to B (B > A gear, B > A food, A > B food, A > B gear: each way keeps to its
        /// right, food on the inside), Bend the angle (degrees) it leaves A and reaches B off the chord before the pairs around a shield are spread
        /// (ThA, ThB: the middle of its ports there).</summary>
        public class Bundle
        {
            public string A, B; public int Ia, Ib, I, Side, Tight; public bool TwoWay; public double Len, Out, ThA, ThB, Bend = BaseBend;
            public List<Block> Lanes = new List<Block>();
        }

        /// <summary>Everything the drawing needs, from a giving block (PanelUi draws it, the tests check it).</summary>
        public class Plan
        {
            public Dictionary<string, (double x, double y)> Pos; public Dictionary<string, Rect> NameRect = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
            public List<Block> Drawn; public List<Bundle> Bundles = new List<Bundle>(); public Dictionary<Block, Geo> Geos = new Dictionary<Block, Geo>();
            public List<Pill> Pills = new List<Pill>(); public Dictionary<Pill, Block> GiftOf = new Dictionary<Pill, Block>(); public List<Rect> Fixed;
            /// <summary>Where two threads cross (outside the hubs too): no count sits on one.</summary>
            public List<(double x, double y)> Crossed = new List<(double x, double y)>();
            /// <summary>Counts that found no free place even compact and are left off the drawing (their rows in the list say them).</summary>
            public int Stuck;
            public double Cx, Cy;
        }

        static readonly Dictionary<string, Plan> cache = new Dictionary<string, Plan>();

        /// <summary>The plan for a "giving" block: the players around the hearth at (cx, -cy), a thread per gift, a pill for each of YOUR gifts' counts.
        /// Worked out once per distinct block, then kept.</summary>
        public static Plan Build(Block giving, double cx, double cy)
        {
            var items = giving.Items ?? new List<Block>();
            var key = KeyOf(items, cx, cy);
            lock (cache) if (cache.TryGetValue(key, out var hit)) return hit;
            var plan = Make(items, cx, cy, null);
            KeepPlan(key, plan);
            return plan;
        }

        static string KeyOf(List<Block> items, double cx, double cy) =>
            cx + "," + cy + "|" + string.Join(";", items.Select(i => i.Kind + ":" + i.Id + ":" + i.Title + ":" + i.Text + ":" + i.Tone + ":" + i.Value + ":" + i.Fraction + ":" + (i.Selected ? 1 : 0)));

        static void KeepPlan(string key, Plan plan) { lock (cache) { if (cache.Count > 64) cache.Clear(); cache[key] = plan; } }

        // ---------- the open book's way (0.9, Joost in game on 0.8.91: "when I open the company tab for the first time ... it sort of freezes the game") ----------

        /// <summary>Make's steps, said on the wait line while a worker works the plan out: the players and their corridors, every thread's ports,
        /// the bends, the counts.</summary>
        public const int Stages = 4;
        const int StepPorts = 2, StepBends = 3, StepCounts = 4;

        /// <summary>A plan on its way: the step the worker is on (1 .. Stages, never 0), and Done once it is kept (or failed: the book then works it
        /// out itself on its next drawing, as before 0.9).</summary>
        public sealed class Pending
        {
            volatile int stage = 1; volatile bool done;
            public int Stage => stage;
            public bool Done => done;
            internal void At(int s) { if (s > stage && s <= Stages) stage = s; }
            internal void Finish() => done = true;
        }

        static readonly Dictionary<string, Pending> working = new Dictionary<string, Pending>();
        static readonly HashSet<string> failed = new HashSet<string>();

        /// <summary>For the open book (CompanyUi.Giving): a plan already worked out comes back at once; otherwise null, and a worker thread works it
        /// out (Make is Unity-free) while the page shows the wait line; <paramref name="wait"/> says how far it is and when it is done (the book then
        /// draws the page again, PanelUi.FireTick). The geometry took 10 ms for four players and 0.5 to 0.8 s for a busy six or a group of twelve
        /// on the Mini (more in the game's Mono), all on the frame that drew Fireside first: the freeze Joost saw. A plan the worker could not make
        /// is made here, on the caller, as before.</summary>
        public static Plan Request(Block giving, double cx, double cy, out Pending wait)
        {
            wait = null;
            var items = (giving?.Items ?? new List<Block>()).ToList();   // the worker's own list: the drawing's blocks are never changed after the build
            var key = KeyOf(items, cx, cy);
            bool here;
            lock (cache)
            {
                if (cache.TryGetValue(key, out var hit)) return hit;
                if (working.TryGetValue(key, out wait)) return null;
                here = failed.Contains(key);
                if (!here) { wait = new Pending(); working[key] = wait; }
            }
            if (here) { var plan = Make(items, cx, cy, null); KeepPlan(key, plan); return plan; }
            var w = wait;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { KeepPlan(key, Make(items, cx, cy, w)); }
                catch { lock (cache) failed.Add(key); }
                finally { lock (cache) working.Remove(key); w.Finish(); }
            });
            return null;
        }

        /// <summary>Whether the plan for this block is worked out already (a cache hit: drawing it costs nothing more).</summary>
        public static bool Ready(Block giving, double cx, double cy)
        {
            var key = KeyOf(giving?.Items ?? new List<Block>(), cx, cy);
            lock (cache) return cache.ContainsKey(key);
        }

        /// <summary>One thread's end on a shield, while the ports are laid out.</summary>
        class Port { public Block Gift; public Bundle B; public bool Arrives; public double W, Phi, Dir; }

        static double Wrap(double a) { while (a > Math.PI) a -= 2 * Math.PI; while (a <= -Math.PI) a += 2 * Math.PI; return a; }

        /// <summary>The least angle between two neighbouring ports (radians): their lines' half widths (a head's half width where it arrives) and the
        /// room between them, 4 px off the rim where two lines leave (they part from there), at the head's base where a head is (the heads point
        /// about at the middle, so they are widest there).</summary>
        static double PortGap(Port p, Port q)
        {
            double Ext(Port x) => x.Arrives ? HeadHalf(x.W) : x.W / 2;
            var room = p.B != q.B ? OtherPair : p.Gift.Title == q.Gift.Title ? SameWay : OtherWay;
            var heads = new[] { p, q }.Where(x => x.Arrives).Select(x => ArrowPerLine * x.W).ToList();
            return (Ext(p) + Ext(q) + room) / (Rim + (heads.Count > 0 ? heads.Min() : 4));
        }

        /// <summary>The least squared movement that keeps every gap (pool adjacent violators): y[i+1] - y[i] >= gaps[i], and all of them within lo..hi
        /// (uniform bounds on the pooled values: clamping the unbounded answer is the bounded one). A shield with less room than its gaps need has
        /// them all made smaller alike, so nothing goes under a name or behind the shield.</summary>
        static double[] Spread(double[] ideal, double[] gaps, double lo = double.NegativeInfinity, double hi = double.PositiveInfinity)
        {
            int n = ideal.Length; var G = new double[n]; for (int i = 1; i < n; i++) G[i] = G[i - 1] + gaps[i - 1];
            if (n > 1 && G[n - 1] > hi - lo) { var k = (hi - lo) / G[n - 1]; for (int i = 1; i < n; i++) G[i] *= k; }
            var val = new List<double>(); var wt = new List<int>();
            for (int i = 0; i < n; i++)
            {
                val.Add(ideal[i] - G[i]); wt.Add(1);
                while (val.Count > 1 && val[val.Count - 2] > val[val.Count - 1])
                {
                    int k = val.Count - 1; var w = wt[k - 1] + wt[k]; var v = (val[k - 1] * wt[k - 1] + val[k] * wt[k]) / w;
                    val.RemoveAt(k); wt.RemoveAt(k); val[k - 1] = v; wt[k - 1] = w;
                }
            }
            var y = new double[n]; int at = 0; var top = hi - G[n - 1];
            for (int b = 0; b < val.Count; b++) for (int k = 0; k < wt[b]; k++, at++) y[at] = Math.Max(lo, Math.Min(top, val[b])) + G[at];
            return y;
        }

        static Plan Make(List<Block> items, double cx, double cy, Pending progress)
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
            // the corridors, in the players' order; in each, the lanes left to right as seen going from A to B
            var byKey = new Dictionary<string, Bundle>();
            foreach (var g in plan.Drawn)
            {
                int i = idx[g.Title], j = idx[g.Text]; var a = i < j ? g.Title : g.Text; var b = i < j ? g.Text : g.Title; var k = a + "|" + b;
                if (!byKey.TryGetValue(k, out var bu)) { bu = new Bundle { A = a, B = b, Ia = idx[a], Ib = idx[b] }; byKey[k] = bu; plan.Bundles.Add(bu); }
                bu.Lanes.Add(g);
            }
            plan.Bundles = plan.Bundles.OrderBy(x => x.Ia).ThenBy(x => x.Ib).ToList();
            var bundles = plan.Bundles;
            int Tone(Block g) => g.Tone == "gear" ? 1 : 0;   // food on the inside (next to the other way), gear outside
            for (int bi = 0; bi < bundles.Count; bi++)
            {
                var B = bundles[bi]; B.I = bi;
                var fwd = B.Lanes.Where(g => g.Title == B.A).OrderBy(Tone).ToList(); var back = B.Lanes.Where(g => g.Title == B.B).OrderByDescending(Tone).ToList();
                B.Lanes = back.Concat(fwd).ToList(); B.TwoWay = fwd.Count > 0 && back.Count > 0;
                var Pa = pos[B.A]; var Pb = pos[B.B];
                double dx = Pb.x - Pa.x, dy = Pb.y - Pa.y, d = Hyp(dx, dy), rx = dy / d, ry = -dx / d;
                var side = ((Pa.x + Pb.x) / 2 - centre.x) * rx + ((Pa.y + Pb.y) / 2 - centre.y) * ry;
                B.Side = Math.Abs(side) < 1 ? 0 : side > 0 ? 1 : -1;   // bend away from the hearth; a chord through it: below
                B.Len = d - 2 * Rim; B.Out = Math.Abs(side);   // how far its chord passes from the hearth
            }

            // a pair straight across the hearth bends to its quieter side: the side whose players have fewer gifts with its two players (ties: to its right)
            var across = new HashSet<Bundle>(bundles.Where(B => B.Side == 0));
            foreach (var B in across)
            {
                var Pa = pos[B.A]; var Pb = pos[B.B]; double dx = Pb.x - Pa.x, dy = Pb.y - Pa.y;
                int On(string id) { var q = pos[id]; var c = (q.x - Pa.x) * dy - (q.y - Pa.y) * dx; return c > 1 ? 1 : c < -1 ? -1 : 0; }   // +1: to the right of A > B
                int Busy(int side) => plan.Drawn.Count(g => (g.Title == B.A || g.Title == B.B || g.Text == B.A || g.Text == B.B) && On(g.Title == B.A || g.Title == B.B ? g.Text : g.Title) == side);
                B.Side = Busy(-1) < Busy(1) ? -1 : 1;
            }
            // around each shield the ports keep out of the angle its name covers (NameMargin px around it), and in front (Front): lo..hi, relative to the way in to the hearth
            var room = new Dictionary<string, (double lo, double hi)>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in players)
            {
                var at = pos[p.Id]; var inward = Math.Atan2(centre.y - at.y, centre.x - at.x); var r = plan.NameRect[p.Id];
                // the name's angle, measured round the way to its own middle (a name reaching behind the shield wraps past half a turn)
                var mid = Wrap(Math.Atan2((r.Y0 + r.Y1) / 2 - at.y, (r.X0 + r.X1) / 2 - at.x) - inward);
                var ds = new[] { (r.X0 - NameMargin, r.Y0 - NameMargin), (r.X1 + NameMargin, r.Y0 - NameMargin), (r.X0 - NameMargin, r.Y1 + NameMargin), (r.X1 + NameMargin, r.Y1 + NameMargin) }
                    .Select(c => Wrap(Math.Atan2(c.Item2 - at.y, c.Item1 - at.x) - inward - mid)).ToList();
                double from = mid + ds.Min(), to = mid + ds.Max(), lo = -Front * Math.PI / 180, hi = Front * Math.PI / 180;
                if (mid > 0) { hi = Math.Min(hi, from); lo = Math.Max(lo, to - 2 * Math.PI); } else { lo = Math.Max(lo, to); hi = Math.Min(hi, from + 2 * Math.PI); }
                room[p.Id] = (lo, hi);
            }
            progress?.At(StepPorts);
            // every thread's two ports
            var ends = new Dictionary<Block, (Port from, Port to)>(); var pairOf = new Dictionary<Block, Bundle>();
            foreach (var B in bundles)
                foreach (var g in B.Lanes)
                {
                    var w = LineWidth(g.Fraction); pairOf[g] = B;
                    ends[g] = (new Port { Gift = g, B = B, Arrives = false, W = w }, new Port { Gift = g, B = B, Arrives = true, W = w });
                }
            // a pair's ports on one shield, in rising angle (right of A > B: clockwise at A, so the lanes reversed there), and where each sits from the
            // pair's middle (radians): their gaps (PortGap) laid end to end, centred on it
            (List<Port> ports, double[] at, double span) PortsAt(Bundle B, string id)
            {
                var ps = (B.A == id ? Enumerable.Reverse(B.Lanes) : B.Lanes).Select(g => g.Title == id ? ends[g].from : ends[g].to).ToList();
                var at = new double[ps.Count]; for (int k = 1; k < ps.Count; k++) at[k] = at[k - 1] + PortGap(ps[k - 1], ps[k]);
                var span = at[ps.Count - 1]; for (int k = 0; k < ps.Count; k++) at[k] -= span / 2;
                return (ps, at, span);
            }
            void Lay()
            {
                // the corridors: each pair leaves its two shields Bend off the chord toward its bending side; around every shield the pairs keep
                // their order and are spread (the least movement) until their ports lie side by side on the rim; each port then sits at its place
                // in its pair, so a thread leaves (and arrives) about along its corridor
                foreach (var p in players)
                {
                    var at = pos[p.Id]; var inward = Math.Atan2(centre.y - at.y, centre.x - at.x);
                    var mine = bundles.Where(B => B.A == p.Id || B.B == p.Id).Select(B =>
                    {
                        var isA = B.A == p.Id; var other = pos[isA ? B.B : B.A]; var chord = Math.Atan2(other.y - at.y, other.x - at.x);
                        return (B, isA, along: Wrap(chord - inward), ideal: Wrap(chord + (isA ? -B.Side : B.Side) * B.Bend * Math.PI / 180 - inward), ports: PortsAt(B, p.Id));   // A: toward the bending side (right = clockwise); B: mirrored
                    }).OrderBy(x => x.along).ThenBy(x => x.B.I).ToList();   // in the order of the players they lead to: a bend spreads the pairs, never swaps two
                    if (mine.Count == 0) continue;
                    var gaps = new double[mine.Count - 1];
                    for (int k = 0; k + 1 < mine.Count; k++) gaps[k] = mine[k].ports.span / 2 + PortGap(mine[k].ports.ports.Last(), mine[k + 1].ports.ports[0]) + mine[k + 1].ports.span / 2;
                    var y = Spread(mine.Select(x => x.ideal).ToArray(), gaps, room[p.Id].lo + mine[0].ports.span / 2, room[p.Id].hi - mine[mine.Count - 1].ports.span / 2);
                    var fit = gaps.Sum() + mine[0].ports.span / 2 + mine[mine.Count - 1].ports.span / 2 > room[p.Id].hi - room[p.Id].lo ? (room[p.Id].hi - room[p.Id].lo) / (gaps.Sum() + mine[0].ports.span / 2 + mine[mine.Count - 1].ports.span / 2) : 1;   // a crowded shield: all made smaller alike
                    for (int k = 0; k < mine.Count; k++)
                    {
                        var x = mine[k]; if (x.isA) x.B.ThA = inward + y[k]; else x.B.ThB = inward + y[k];
                        for (int j = 0; j < x.ports.ports.Count; j++) { var phi = y[k] + x.ports.at[j] * fit; x.ports.ports[j].Phi = inward + phi; var radial = Radial * (1 - x.B.Tight / (double)Tightest); x.ports.ports[j].Dir = inward + radial * phi + (1 - radial) * x.ideal; }   // toward the bend it wanted, where the pairs beside it pushed it off
                    }
                }
                foreach (var g in plan.Drawn) { var B = pairOf[g]; plan.Geos[g] = Lane(pos[g.Title], ends[g].from.Phi, ends[g].from.Dir, pos[g.Text], ends[g].to.Phi, ends[g].to.Dir, B.TwoWay ? WaySplit * (1 - B.Tight / (double)Tightest) : 0); }
            }
            progress?.At(StepBends);
            // the bend: BaseBend for every pair, then each pair in trouble moves (Trouble): one whose thread passes the hearth bends further (1); one that
            // passes near a third shield (Clear) or over a name, or crosses a pair it must cross (their players alternate round the ring) at under
            // MinCrossDeg and 2, first draws its threads closer together (Tight) and then bends less (-1); two pairs that cross or run together where
            // they need not part: the one whose chord passes further from the hearth bends more (2), the other less (-2)
            bool Alternate(Bundle p, Bundle q) => p.A != q.A && p.A != q.B && p.B != q.A && p.B != q.B && ((p.Ia < q.Ia && q.Ia < p.Ib) != (p.Ia < q.Ib && q.Ib < p.Ib));
            int Trouble(Bundle B)
            {
                if (B.Lanes.Any(g => plan.Geos[g].Pts.Any(p => Hyp(p.x - centre.x, p.y - centre.y) < Keep))) return 1;   // the hearth first: it never gives way
                foreach (var g in B.Lanes)
                    foreach (var p in plan.Geos[g].Pts)
                    {
                        foreach (var q in players)
                        {
                            if (q.Id == B.A || q.Id == B.B) continue;
                            if (Hyp(p.x - pos[q.Id].x, p.y - pos[q.Id].y) < Clear) return -1;
                        }
                        foreach (var q in players) { var r = plan.NameRect[q.Id]; if (p.x > r.X0 - 2 && p.x < r.X1 + 2 && p.y > r.Y0 - 2 && p.y < r.Y1 + 2) return -1; }
                    }
                foreach (var B2 in bundles)
                {
                    if (B2 == B) continue;
                    if (Alternate(B, B2)) { if (B.Lanes.Any(g => B2.Lanes.Any(h => Crossings(plan.Geos[g].Pts, plan.Geos[h].Pts).Any(x => x.deg < MinCrossDeg + 2)))) return -1; continue; }
                    // two pairs that need not cross (they share a player, or their players do not alternate) crossing or running together: they part, the
                    // one whose chord passes further from the hearth bending more, the other less
                    var hubs = new[] { B.A, B.B }.Where(x => x == B2.A || x == B2.B).Select(x => pos[x]).ToList();
                    if (B.Lanes.Any(g => B2.Lanes.Any(h => RunTogetherPx(plan.Geos[g].Pts, plan.Geos[h].Pts, hubs) > RunTogether - 4 ||
                                                         Crossings(plan.Geos[g].Pts, plan.Geos[h].Pts).Any(x => !hubs.Any(c => Hyp(x.at.x - c.x, x.at.y - c.y) < Hub))))) return B.Out > B2.Out + 1 ? 2 : -2;
                }
                return 0;
            }
            void Bends()
            {
                for (int round = 0; round < 10 + Tightest; round++)
                {
                    Lay(); var moved = false;
                    foreach (var B in bundles)
                    {
                        // the hearth: bend further; in the way of another pair, a shield or a name: first draw the threads closer together, then bend (less, or
                        // for the outer of two pairs that clash, more)
                        var t = Trouble(B); var tighten = t != 0 && B.Tight < Tightest && (t != 1 || B.Bend >= MaxBend);
                        var to = tighten || t == 0 ? B.Bend : t > 0 ? Math.Min(MaxBend, B.Bend + BendStep) : Math.Max(MinBend, B.Bend - BendStep);
                        if (tighten) { B.Tight++; moved = true; }
                        if (to != B.Bend) { B.Bend = to; moved = true; }
                    }
                    if (!moved) break;
                }
                Lay();
            }
            void Again() { foreach (var B in bundles) { B.Bend = BaseBend; B.Tight = 0; } Bends(); }
            int Troubled() => bundles.Count(B => Trouble(B) != 0);
            Bends();
            // a pair straight across the hearth still in trouble on its quieter side (boxed in by its neighbours) tries the other side, one pair at a time;
            // it keeps the side that leaves fewer pairs in trouble
            var left = Troubled(); var laid = true;
            foreach (var B in bundles.Where(across.Contains))
            {
                if (left == 0) break;
                B.Side = -B.Side; Again(); var now = Troubled();
                if (now < left) { left = now; laid = true; } else { B.Side = -B.Side; laid = false; }
            }
            if (!laid) Again();
            foreach (var g in plan.Drawn) plan.Geos[g].Fraction = g.Fraction;   // the count sets the line and its arrowhead (StrokeOf)
            var drawnGeos = plan.Drawn.Select(g => plan.Geos[g]).ToList();
            for (int i = 0; i < drawnGeos.Count; i++) for (int j = i + 1; j < drawnGeos.Count; j++) plan.Crossed.AddRange(Crossings(drawnGeos[i].Pts, drawnGeos[j].Pts).Select(x => x.at));
            progress?.At(StepCounts);
            // the counts of your gifts at their threads' middles (the two ways have bowed apart there); the counts going one way step along it in lane
            // order, so food and gear side by side never want the same spot
            foreach (var B in bundles)
                foreach (var way in B.Lanes.Where(g => g.Selected).GroupBy(g => g.Title))
                {
                    var mine = way.ToList(); var m = mine.Count; var step = Math.Min(0.2, 40 / Math.Max(1, B.Len));
                    for (int k = 0; k < m; k++)
                    {
                        var g = mine[k]; var text = g.Value ?? ""; var along = (k - (m - 1) / 2.0) * step;
                        var prefer = Math.Floor((0.5 + (g.Title == B.A ? along : -along) - 0.1) / 0.02 + 0.5) * 0.02 + 0.1;   // on the grid PlacePills tries: one nearest place, never a tie
                        plan.Pills.Add(new Pill { Thread = plan.Geos[g], Gift = g, Text = text, Prefer = prefer, Width = PillWidth(text), Others = B.Lanes.Where(o => o.Title != g.Title).Select(o => plan.Geos[o]).ToList() });
                    }
                }
            plan.Pills = plan.Pills.OrderBy(p => plan.Drawn.IndexOf(p.Gift)).ToList();
            foreach (var p in plan.Pills) plan.GiftOf[p] = p.Gift;
            plan.Fixed = new List<Rect> { Rect.Around(cx, -cy, HearthX, HearthY) };
            foreach (var p in players) { var at = pos[p.Id]; plan.Fixed.Add(Rect.Around(at.x, at.y, ShieldD / 2 + 2, ShieldD / 2 + 2)); plan.Fixed.Add(plan.NameRect[p.Id]); }
            plan.Stuck = PlacePills(plan.Pills, plan.Fixed, drawnGeos, plan.Crossed);
            return plan;
        }

        /// <summary>
        /// Every count's place along its own thread: u 0.1..0.9 in steps of 0.02; nothing may touch a shield, a name, the hearth, a crossing, an arrow head or
        /// another count (cost 1 000 000 plus the overlap); of the free places the one nearest where it would like to sit, off other threads and a little away
        /// from crossings. The counts with the fewest free places choose first, six passes; any still without a place then choose first, up to three times.
        /// A count with no free place is made compact (16 px digits on a 20 px pill) and all are placed again; one that still finds none is left off the
        /// drawing (its row in the list beside it says it). Returns how many were left off.
        /// </summary>
        public static int PlacePills(IList<Pill> pills, IList<Rect> fixedRects, IList<Geo> threads, IList<(double x, double y)> crossed = null)
        {
            int n = pills.Count; if (n == 0) return 0;
            crossed = crossed ?? new List<(double x, double y)>();
            fixedRects = fixedRects.Concat(crossed.Select(x => Rect.Around(x.x, x.y, 1, 1))).ToList();   // a crossing is never under a count; one within 5 px of it costs (below)
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
                foreach (var x in crossed) if (x.x > r.X0 - 5 && x.x < r.X1 + 5 && x.y > r.Y0 - 5 && x.y < r.Y1 + 5) soft += 40;
                foreach (var t in threads)
                {
                    if (ReferenceEquals(t, pills[i].Thread)) continue;
                    foreach (var q in t.Pts) if (q.x >= r.X0 - 3 && q.x <= r.X1 + 3 && q.y >= r.Y0 - 3 && q.y <= r.Y1 + 3) { soft += 30; break; }
                }
                foreach (var t in pills[i].Others) if (SegmentsIn(t.Pts, r)) soft += 400;   // over the other way of its own pair: whose count is it?
                return (hard > 0 ? 1000000 + hard : 0) + soft;
            }
            // the counts with the fewest free places (no shield, name, hearth, crossing or arrow head) choose first; then the shortest threads
            int Free(int i) => us.Count(u => { var r = RectAt(i, u); return !fixedRects.Any(f => Overlap(r, f) > 0) && !arrows.Any(a => Overlap(r, a) > 0); });
            var free = Enumerable.Range(0, n).Select(Free).ToArray();
            var order = Enumerable.Range(0, n).OrderBy(i => free[i]).ThenBy(i => pills[i].Thread.Len).ThenBy(i => i).ToList();   // (a count still stuck after the passes moves to the front)
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
            for (int again = 0; again < 3 && st.Count > 0; again++)   // a count still without a place chooses first next time, all placed again
            {
                order = st.Concat(order.Where(i => !st.Contains(i))).ToList(); for (int i = 0; i < n; i++) chosen[i] = -1;
                Passes(); st = StuckOnes();
            }
            if (st.Count > 0) { foreach (var i in st) { var p = pills[i]; p.Compact = true; p.H = PillCompactH; p.Width = Math.Max(22, Math.Ceiling(p.Text.Length * 8.9) + 10); } Passes(); st = StuckOnes(); }
            foreach (var i in st) if (Cost(i, chosen[i]) >= 1000000) pills[i].Dropped = true;   // one by one: leaving one off may free another
            int dropped = 0;
            for (int i = 0; i < n; i++) { pills[i].U = us[chosen[i]]; pills[i].At = pills[i].Thread.At(pills[i].U); if (pills[i].Dropped) dropped++; }
            return dropped;
        }

        /// <summary>The drawing as JSON for the preview (0.8.5): the dump writes it beside the page data and panel-preview.html draws exactly this, so the game
        /// and the preview share one geometry (no port to keep in step). Every thread as StrokeOf draws it (the line, the head's three points, the width),
        /// every count where PlacePills put it, every shield and its name's box; y is up, as here.</summary>
        public static string ToJson(Plan plan)
        {
            string N(double v) => Math.Round(v, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string P((double x, double y) p) => "[" + N(p.x) + "," + N(p.y) + "]";
            string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
            string Key(Block g) => Esc(g.Title + ">" + g.Text + "/" + g.Tone);
            var players = plan.Pos.Select(kv => { var r = plan.NameRect[kv.Key]; return "\"" + Esc(kv.Key) + "\":{\"at\":" + P(kv.Value) + ",\"name\":[" + N(r.X0) + "," + N(r.Y0) + "," + N(r.X1) + "," + N(r.Y1) + "]}"; });
            var threads = plan.Drawn.Where(g => plan.Geos.ContainsKey(g)).Select(g => { var s = StrokeOf(plan.Geos[g], g.Fraction);
                return "{\"gift\":\"" + Key(g) + "\",\"from\":\"" + Esc(g.Title) + "\",\"to\":\"" + Esc(g.Text) + "\",\"tone\":\"" + Esc(g.Tone) + "\",\"mine\":" + (g.Selected ? 1 : 0) +
                       ",\"w\":" + N(s.Width) + ",\"line\":[" + string.Join(",", s.Line.Select(P)) + "],\"head\":[" + P(s.Tip) + "," + P(s.Left) + "," + P(s.Right) + "]}"; });
            var pills = plan.Pills.Select(p => "{\"gift\":\"" + Key(p.Gift) + "\",\"text\":\"" + Esc(p.Text) + "\",\"tone\":\"" + Esc(p.Gift.Tone) + "\",\"at\":" + P(p.At) + ",\"w\":" + N(p.Width) + ",\"h\":" + N(p.H) +
                                               ",\"compact\":" + (p.Compact ? 1 : 0) + ",\"dropped\":" + (p.Dropped ? 1 : 0) + "}");
            return "{\"cx\":" + N(plan.Cx) + ",\"cy\":" + N(plan.Cy) + ",\"rim\":" + N(Rim) + ",\"players\":{" + string.Join(",", players) + "},\"threads\":[" + string.Join(",", threads) + "],\"pills\":[" + string.Join(",", pills) + "]}";
        }
    }
}
