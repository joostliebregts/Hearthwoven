using System.Collections.Generic;

namespace Hearthwoven
{
    /// <summary>
    /// Trees you felled, counted on your own PC (ClientHooks.ChopTree and TreeGone). The game books a felled tree only
    /// inside TreeBase.RPC_Damage, which runs on the PC that hosts the area and only for that PC's own player: a tree you
    /// fell where a fellow's PC hosts the area is counted by nobody (SOURCES.md, Woodcutting).
    ///
    /// The rule: a tree counts as felled by you when your own axe hit should bring it down and the tree is then really
    /// destroyed. "Should bring it down" is the game's own sum (TreeBase.RPC_Damage): the hit's damage after the tree's
    /// resistances, with a tool good enough for it, is at least the tree's health. That health is the lower of what the
    /// tree's shared state says (synced from the host, it can lag a hit behind) and what your own previous hits left of it.
    /// "Really destroyed": the tree's network object is destroyed (ZDOMan.HandleDestroyedZDO, which reaches every PC in the
    /// area, the host's own included) within <see cref="Window"/> seconds of that hit. Walking away only unloads a tree, it
    /// is not destroyed, so it never counts. Like the game, only the blow that fells it counts: a tree two players chop
    /// together is yours when your hit was the last. A fellow's hit your PC has not heard of yet can, rarely, make both of
    /// you count it, or neither.
    ///
    /// Pure C# (no Unity calls; in the game the key is the tree's ZDOID), unit-tested.
    /// </summary>
    public class TreeFalls<TKey>
    {
        public const float Window = 5f;   // seconds from the felling hit to the destroy (one round trip through the host)
        const float Forget = 120f;        // a tree not hit for this long is forgotten
        class Tree { public string Prefab; public float At, HealthLeft; public bool Felling; }
        readonly Dictionary<TKey, Tree> trees = new Dictionary<TKey, Tree>();

        public int Tracked => trees.Count;

        /// <summary>
        /// One of your axe hits on a tree at time <paramref name="now"/>: <paramref name="health"/> = the tree's health in its
        /// shared state, <paramref name="damage"/> = your hit's damage after the tree's resistances (a tool too weak for the
        /// tree: do not call). Returns whether this hit should fell it.
        /// </summary>
        public bool Hit(TKey tree, string prefab, float health, float damage, float now)
        {
            if (damage <= 0f) return false;
            if (trees.Count > 64) Prune(now);
            if (trees.TryGetValue(tree, out var t) && now - t.At <= Forget && t.HealthLeft < health) health = t.HealthLeft;
            var left = health - damage;
            trees[tree] = new Tree { Prefab = prefab, At = now, HealthLeft = left, Felling = left <= 0.001f };
            return left <= 0.001f;
        }

        /// <summary>A tree's network object was destroyed at <paramref name="now"/>: its prefab when your hit felled it (count
        /// it), else null. The tree is forgotten either way.</summary>
        public string Destroyed(TKey tree, float now)
        {
            if (!trees.TryGetValue(tree, out var t)) return null;
            trees.Remove(tree);
            return t.Felling && now - t.At >= 0f && now - t.At <= Window ? t.Prefab : null;
        }

        void Prune(float now)
        {
            var old = new List<TKey>();
            foreach (var kv in trees) if (now - kv.Value.At > Forget) old.Add(kv.Key);
            foreach (var k in old) trees.Remove(k);
        }
    }
}
