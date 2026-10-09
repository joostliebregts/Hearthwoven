using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// Heavy Keel's arithmetic (0.6, ACHIEVEMENTS-06 section 3.1: "100 metal or ore items aboard over 2 km"), pure C# so the tests run
    /// it without the game. CargoHooks.SampleShip (every 10 s, at the helm) hands it the metal and ore count that lay in the ship's box
    /// and the metres the ship moved since the last sample.
    ///
    /// A voyage starts with the first sample at the helm and goes on while you steer the same ship. Leaving the helm pauses it: it ends
    /// when you have been away for more than AwaySeconds (5 minutes), when you steer another ship, or when the connection starts again
    /// (a new ship object). Docking to unload and casting off again within five minutes is the same voyage.
    ///
    /// The best load of a voyage is the biggest number L such that the ship moved at least LineMetres (2 km) in all with L or more
    /// metal and ore aboard. Those 2 km need not be in one piece; unloading half-way lowers L for the rest. A step of 0 or less, or a
    /// jump (Cargo.JumpMax and over: a teleport, a ship carried in a bottle), adds nothing. Metres are the straight line between two
    /// samples, so a winding route is longer than counted: a floor, never an overcount.
    /// </summary>
    public class CargoVoyage
    {
        /// <summary>The ledger counter (and shared best) Heavy Keel reads.</summary>
        public const string BestKey = "cargoBestVoyage";
        /// <summary>The feat's distance: a load counts once the ship has moved this far with it aboard.</summary>
        public const double LineMetres = 2000;
        /// <summary>Away from the helm for longer than this (seconds) ends the voyage.</summary>
        public const double AwaySeconds = 300;
        /// <summary>At most this many different loads are remembered in one voyage (a load changes only when cargo is added or taken out).</summary>
        public const int MaxLoads = 2048;

        /// <summary>The items that count as metal or ore: ores, scrap, and bars (not nails, tools or gear). The game's own item names.</summary>
        public static readonly HashSet<string> MetalOre = new HashSet<string>
        {
            "$item_copperore", "$item_copperscrap", "$item_copper", "$item_tinore", "$item_tin", "$item_bronzescrap", "$item_bronze",
            "$item_ironscrap", "$item_ironore", "$item_iron", "$item_silverore", "$item_silver",
            "$item_blackmetalscrap", "$item_blackmetal", "$item_flametalore", "$item_flametalorenew", "$item_flametal", "$item_flametalnew",
        };

        /// <summary>How many metal and ore items lie in the box (<paramref name="aboard"/>: item token to amount).</summary>
        public static int MetalOreCount(IDictionary<string, int> aboard) =>
            aboard == null ? 0 : aboard.Where(kv => kv.Value > 0 && MetalOre.Contains(kv.Key)).Sum(kv => kv.Value);

        object ship; bool active; double lastAtHelm;
        readonly Dictionary<int, double> metresWith = new Dictionary<int, double>();   // load -> metres moved with exactly that many aboard

        /// <summary>True while a voyage is on (the helm is held, or has been left for at most AwaySeconds).</summary>
        public bool Active => active;
        /// <summary>The metres moved with some metal or ore aboard in the voyage so far.</summary>
        public double LoadedMetres => metresWith.Values.Sum();

        /// <summary>The best load of the voyage so far (0 = none carried for 2 km yet).</summary>
        public int Best
        {
            get
            {
                double sum = 0;
                foreach (var load in metresWith.Keys.OrderByDescending(k => k))
                {
                    sum += metresWith[load];
                    if (sum >= LineMetres) return load;
                }
                return 0;
            }
        }

        /// <summary>Forgets the voyage (the connection ended).</summary>
        public void Reset() { ship = null; active = false; metresWith.Clear(); }

        /// <summary>
        /// One sample. <paramref name="atHelm"/> = the ship whose helm you hold now (null: you do not hold a helm). <paramref name="load"/> = metal
        /// and ore aboard now, <paramref name="metres"/> = the metres the ship moved since the last sample at the helm (0 when this is the first,
        /// or when the box could not be read: the voyage goes on, nothing is counted).
        /// </summary>
        public void Step(double now, object atHelm, int load, double metres)
        {
            if (atHelm == null) { if (active && now - lastAtHelm > AwaySeconds) Reset(); return; }
            if (active && (!ReferenceEquals(ship, atHelm) || now - lastAtHelm > AwaySeconds)) Reset();
            if (!active) { active = true; ship = atHelm; metres = 0; }   // the step that ended before this voyage began is not part of it
            lastAtHelm = now;
            if (load <= 0 || !(metres > 0) || metres >= Cargo.JumpMax) return;
            if (!metresWith.ContainsKey(load) && metresWith.Count >= MaxLoads) return;
            metresWith.TryGetValue(load, out var m); metresWith[load] = m + metres;
        }
    }
}
