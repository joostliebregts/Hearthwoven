using System;
using System.Collections.Generic;

namespace Hearthwoven
{
    /// <summary>
    /// The arithmetic of "cargo carried" and "born in your care" (0.6), pure C# so the tests run it without the game.
    /// ClientHooks / CargoHooks read the game and call these.
    ///
    /// Cargo: while you hold a ship's helm or pull a cart, every sample (10 s) reads what lies in that ship's or cart's
    /// container and adds count x metres moved since the last sample to SessionEvents.CargoMeters (item-metres, per item),
    /// and the metres themselves to CargoStretch (per item: the distance travelled with some of it aboard). The two give an
    /// honest average: item-metres / stretch = how many of that item were aboard on average over that distance. Metres are
    /// the straight line between two samples, so a winding route is longer than counted: a floor, never an overcount.
    /// Counts are floats like every tally here: exact to about a billion item-metres, far beyond a long game.
    /// </summary>
    public static class Cargo
    {
        /// <summary>At most this many kinds of item (and of creature) per tally, so the saved file stays small however many mods add items.</summary>
        public const int MaxKinds = 150;
        /// <summary>A step this long between two samples (10 s) is a jump (teleport, a ship carried in a bottle), not a drive: it counts for nothing.</summary>
        public const float JumpMax = 250f;
        /// <summary>A newborn counts as born in your care within this many metres of you (Joost's rule).</summary>
        public const float CareRange = 40f;

        /// <summary>
        /// One sample: <paramref name="aboard"/> (item token -> amount in the container now) moved <paramref name="metres"/>.
        /// Adds amount x metres to <paramref name="itemMetres"/> and the metres to <paramref name="stretch"/> for every item
        /// aboard. A step of 0 or less, or a jump (JumpMax and over), adds nothing. Returns the item-metres added.
        /// </summary>
        public static double Add(Dictionary<string, float> itemMetres, Dictionary<string, float> stretch, IDictionary<string, int> aboard, double metres)
        {
            if (itemMetres == null || stretch == null || aboard == null || !(metres > 0) || metres >= JumpMax) return 0;
            double added = 0;
            foreach (var kv in aboard)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value <= 0) continue;
                if (!itemMetres.ContainsKey(kv.Key) && itemMetres.Count >= MaxKinds) continue;
                var im = kv.Value * metres;
                SessionEvents.Add(itemMetres, kv.Key, (float)im);
                SessionEvents.Add(stretch, kv.Key, (float)metres);
                added += im;
            }
            return added;
        }

        /// <summary>The average amount of an item that was aboard over the distance it travelled with you: item-metres / stretch (0 = unknown).</summary>
        public static double AverageAboard(double itemMetres, double stretchMetres) => itemMetres > 0 && stretchMetres > 0 ? itemMetres / stretchMetres : 0;

        /// <summary>
        /// A newborn counts once (ClientHooks: Character.SetTamed inside Procreation.Procreate or EggGrow.GrowUpdate, which only
        /// run on the PC that hosts the animals): it must be tamed, born inside one of those two (never the grow-up of a young
        /// animal, which is Growup.GrowUpdate), and within CareRange of you. Returns whether it counted.
        /// </summary>
        public static bool CountBirth(Dictionary<string, float> born, string creature, bool tamed, bool insideBirth, double metresFromYou)
        {
            if (born == null || string.IsNullOrEmpty(creature) || !tamed || !insideBirth || !(metresFromYou <= CareRange)) return false;
            if (!born.ContainsKey(creature) && born.Count >= MaxKinds) return false;
            SessionEvents.Add(born, creature);
            return true;
        }
    }
}
