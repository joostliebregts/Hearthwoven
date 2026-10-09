using System;
using System.Collections.Generic;

namespace Hearthwoven
{
    /// <summary>
    /// Drover and Long Lead's arithmetic (0.6, ACHIEVEMENTS-06 section 3.5), pure C# so the tests run it without the game.
    /// ClientHooks.SampleLed (every 10 s) lists the tamed animals that follow the local player and that this PC owns (the game runs
    /// an animal's AI only on the PC that owns it: the owner trap, so the panel says "while your PC hosted them"), and calls Sample.
    ///
    /// Led metres: for every follower seen at the previous sample (and not lost for more than BreakSeconds) the straight-line distance
    /// to where it is now, added to LedMeters under its kind (a Wolf and a Boar led 1 km each: 2 km in all, the feat's "sum over animals").
    /// A step over StepMax metres is a jump (teleport, a ship carried in a bottle) and counts for nothing; the animal's lead goes on.
    /// A winding path is longer than the straight line between samples: a floor, never an overcount. Fast sailing can pass StepMax in
    /// 10 s; such steps are skipped, so on a quick ship the lead is shorter than the real one.
    ///
    /// A lead (stint) is one animal's continuous follow: it ends when the animal has not been seen following for more than BreakSeconds
    /// (told to stay, lost, out of reach, the connection ended). Best is the longest lead so far, in metres, with the animal's kind.
    /// </summary>
    public class LedTracker
    {
        /// <summary>A step longer than this (metres) between two 10 s samples is a jump and counts for nothing.</summary>
        public const float StepMax = 100f;
        /// <summary>Not seen following for longer than this (seconds) ends a lead.</summary>
        public const double BreakSeconds = 60;
        /// <summary>At most this many animals are tracked at once.</summary>
        public const int MaxStints = 64;

        /// <summary>The ledger counters (and shared bests) Drover and Long Lead read: the sum over animals is the "ledMeters" tally.</summary>
        public const string BestKey = "ledBestMeters";

        public struct Follower { public string Id, Kind; public float X, Y, Z; }

        class Stint { public double LastSeen; public float X, Y, Z; public double Metres; public string Kind; }
        readonly Dictionary<string, Stint> stints = new Dictionary<string, Stint>();

        /// <summary>The longest single lead so far, metres (0 = none), and the kind of animal.</summary>
        public double BestMetres; public string BestKind;
        /// <summary>The metres counted by this tracker in all.</summary>
        public double Total;
        /// <summary>How many animals are being led now.</summary>
        public int Leading => stints.Count;

        public void Reset() { stints.Clear(); }

        /// <summary>
        /// One sample at time <paramref name="now"/> (seconds): the animals following the player now. Adds the metres to
        /// <paramref name="ledMeters"/> (kind to metres; at most Cargo.MaxKinds kinds) and returns the metres added.
        /// </summary>
        public double Sample(double now, IEnumerable<Follower> followers, Dictionary<string, float> ledMeters)
        {
            double added = 0;
            foreach (var f in followers ?? new Follower[0])
            {
                if (string.IsNullOrEmpty(f.Id) || string.IsNullOrEmpty(f.Kind)) continue;
                if (!stints.TryGetValue(f.Id, out var s) || now - s.LastSeen > BreakSeconds)
                {
                    if (s == null && stints.Count >= MaxStints) continue;
                    stints[f.Id] = new Stint { LastSeen = now, X = f.X, Y = f.Y, Z = f.Z, Kind = f.Kind };   // a new lead: nothing to count yet
                    continue;
                }
                var dx = f.X - s.X; var dy = f.Y - s.Y; var dz = f.Z - s.Z;
                var d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                s.LastSeen = now; s.X = f.X; s.Y = f.Y; s.Z = f.Z;
                if (!(d > 0) || d > StepMax) continue;
                if (ledMeters != null && (ledMeters.ContainsKey(f.Kind) || ledMeters.Count < Cargo.MaxKinds)) SessionEvents.Add(ledMeters, f.Kind, (float)d);
                s.Metres += d; added += d; Total += d;
                if (s.Metres > BestMetres) { BestMetres = s.Metres; BestKind = s.Kind; }
            }
            var gone = new List<string>();
            foreach (var kv in stints) if (now - kv.Value.LastSeen > BreakSeconds) gone.Add(kv.Key);
            foreach (var id in gone) stints.Remove(id);
            return added;
        }
    }
}
