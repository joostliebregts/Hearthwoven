using System;
using System.Collections.Generic;

namespace Hearthwoven
{
    /// <summary>
    /// Materials back from a piece that came down (0.8, v08-salvage): which drops in the world are a piece's materials, so picking them up
    /// counts as recovered and never as brought in (ClientHooks.Pickup). A piece that comes down (taken down with the hammer, broken by a foe,
    /// worn away) drops its recoverable materials as fresh items (Piece.DropResources): to the game they look like any drop from a tree or a
    /// rock, never held by anyone, so without this every dismantled stone wall counted again as stone brought in.
    ///
    /// Piece.DropResources runs on the PC that hosts the piece (WearNTear.Remove sends RPC_Remove to its owner), which is often not the PC of
    /// the player who took it down. Two ways in, one rule:
    ///   - Created: the drops made inside DropResources on this PC. Exact.
    ///   - CameDown + Appeared: another PC took a piece down. This PC sees the piece's network object destroyed (ZDOMan.HandleDestroyedZDO,
    ///     which every PC that knows it runs) and, before or after that, the drops it made arrive. A drop counts as that piece's when it is one
    ///     of its materials with some of the amount left, it was made within <see cref="Match"/> seconds of the piece coming down (both on the
    ///     game's clock: the drop's own spawn time, which its maker stamps), and it sits within <see cref="Reach"/> metres sideways of the
    ///     spot where the piece drops them, no higher than <see cref="Above"/> and no lower than <see cref="Below"/> metres from it (they fall).
    /// Only pieces a player built (the game's creator is set): the world's own pieces (ruins) give materials found for the first time,
    /// which stay brought in. A remembered drop is forgotten after <see cref="Keep"/> seconds; everything is bounded. Read only: it remembers
    /// ids, it never writes to the world. Pure C# (no Unity calls; in the game the id is the drop's ZDOID), unit-tested.
    /// </summary>
    public class SalvageWatch<TId>
    {
        public const float Keep = 600f;      // seconds a recovered drop (and a spot whose drops have not all shown up) is remembered
        public const double Match = 10;      // the most seconds, on the game's clock, between a piece coming down and its drops being made
        public const float Reach = 4f, Above = 3f, Below = 40f;   // metres from the spot where the piece drops its materials
        const float Pending = 30f;           // seconds a drop that came before its piece's destroy waits for it
        const int MaxIds = 2048, MaxSpots = 256, MaxPending = 128;

        class Spot { public float X, Y, Z, At; public double Net; public Dictionary<string, int> Left; }
        class Drop { public TId Id; public string Item; public int Stack; public float X, Y, Z, At; public double Net; }

        readonly Dictionary<TId, float> recovered = new Dictionary<TId, float>();
        readonly List<Spot> spots = new List<Spot>();
        readonly List<Drop> pending = new List<Drop>();

        public int Tracked => recovered.Count;

        /// <summary>A drop this PC made inside Piece.DropResources of a piece a player built, at <paramref name="now"/> (real seconds).</summary>
        public void Created(TId id, float now)
        {
            if (recovered.Count >= MaxIds) Prune(now);
            recovered[id] = now;
        }

        /// <summary>
        /// Another PC brought down a piece a player built: <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/> where it drops its
        /// materials, <paramref name="materials"/> the item tokens and the most of each it can drop, <paramref name="net"/> the game's clock
        /// (seconds) when this PC heard of it. Drops that arrived first are claimed now; the rest of the amount waits for the drops still to come.
        /// </summary>
        public void CameDown(float x, float y, float z, IEnumerable<KeyValuePair<string, int>> materials, double net, float now)
        {
            if (materials == null) return;
            var s = new Spot { X = x, Y = y, Z = z, Net = net, At = now, Left = new Dictionary<string, int>() };
            foreach (var m in materials) if (!string.IsNullOrEmpty(m.Key) && m.Value > 0) { s.Left.TryGetValue(m.Key, out var had); s.Left[m.Key] = had + m.Value; }
            if (s.Left.Count == 0) return;
            Expire(now);
            for (int i = 0; i < pending.Count && s.Left.Count > 0;)
            {
                var d = pending[i];
                if (Fits(s, d.Item, d.X, d.Y, d.Z, d.Net) >= 0) { Take(s, d.Item, d.Stack); Created(d.Id, now); pending.RemoveAt(i); }
                else i++;
            }
            if (s.Left.Count == 0) return;
            if (spots.Count >= MaxSpots) spots.RemoveAt(0);
            spots.Add(s);
        }

        /// <summary>
        /// A drop another PC made showed up on this PC: its item token, stack, position, the game's clock when it was made
        /// (<paramref name="spawnNet"/>, 0 = not known) and now (<paramref name="netNow"/>). True when it is a piece's material (remembered as
        /// recovered). A fresh drop no spot claims waits a little for its piece's destroy, which can arrive after it.
        /// </summary>
        public bool Appeared(TId id, string item, int stack, float x, float y, float z, double spawnNet, double netNow, float now)
        {
            if (string.IsNullOrEmpty(item) || spawnNet <= 0) return false;
            if (recovered.ContainsKey(id)) return true;
            Expire(now);
            Spot best = null; var bestD = float.MaxValue;
            foreach (var s in spots) { var dd = Fits(s, item, x, y, z, spawnNet); if (dd >= 0 && dd < bestD) { best = s; bestD = dd; } }
            if (best != null)
            {
                Take(best, item, stack); Created(id, now);
                if (best.Left.Count == 0) spots.Remove(best);
                return true;
            }
            if (Math.Abs(netNow - spawnNet) <= Match)
            {
                if (pending.Count >= MaxPending) pending.RemoveAt(0);
                pending.Add(new Drop { Id = id, Item = item, Stack = stack, X = x, Y = y, Z = z, Net = spawnNet, At = now });
            }
            return false;
        }

        /// <summary>Whether a drop is a piece's material (remembered, not yet forgotten).</summary>
        public bool Recovered(TId id, float now) => recovered.TryGetValue(id, out var at) && now - at <= Keep && now >= at;

        // the squared distance sideways when the drop fits the spot (its material, its time, its place), else -1
        static float Fits(Spot s, string item, float x, float y, float z, double net)
        {
            if (!s.Left.ContainsKey(item) || Math.Abs(net - s.Net) > Match) return -1;
            var up = y - s.Y; if (up > Above || up < -Below) return -1;
            float dx = x - s.X, dz = z - s.Z, d = dx * dx + dz * dz;
            return d <= Reach * Reach ? d : -1;
        }

        static void Take(Spot s, string item, int stack)
        {
            var left = s.Left[item] - Math.Max(1, stack);
            if (left > 0) s.Left[item] = left; else s.Left.Remove(item);
        }

        void Expire(float now)
        {
            spots.RemoveAll(s => now - s.At > Keep || now < s.At);
            pending.RemoveAll(d => now - d.At > Pending || now < d.At);
        }

        void Prune(float now)
        {
            var old = new List<TId>();
            foreach (var kv in recovered) if (now - kv.Value > Keep || now < kv.Value) old.Add(kv.Key);
            foreach (var k in old) recovered.Remove(k);
            if (recovered.Count < MaxIds) return;
            // still full (a whole hall taken down within ten minutes): the oldest half goes
            var byAge = new List<KeyValuePair<TId, float>>(recovered);
            byAge.Sort((a, b) => a.Value.CompareTo(b.Value));
            for (int i = 0; i < byAge.Count / 2; i++) recovered.Remove(byAge[i].Key);
        }
    }
}
