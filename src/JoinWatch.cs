using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// Fellow players who just joined (0.7, SYNC-DESIGN.md item 1): the book shows a name from the game's own player list right
    /// away, before their first shared copy arrives, as a dimmed chip without numbers. A name counts as joining while it is on the
    /// list (spawned, not you), has no shared copy yet, and appeared less than Window seconds ago: long enough for their first copy
    /// (10 s after spawn) and the book's next request (30 s). Whoever was already there when you arrived did not just join. A
    /// player who never shares drops out when the window ends; leaving for longer than Absence and coming back starts it again (a death
    /// and respawn takes a player off the list for a few seconds: that is no new arrival). Pure C#: tested without the game.
    /// </summary>
    public class JoinWatch
    {
        public const double Window = 60, Absence = 30;
        readonly Dictionary<string, DateTime> firstSeen = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, DateTime> lastSeen = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> ever = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // every name seen spawned this connection (Recent, B33)
        bool seeded;

        /// <summary>The game's list now: <paramref name="online"/> = the other spawned players' names; <paramref name="listHasYou"/> = the
        /// list already shows you spawned (until then it is not the list of this world yet, and nothing is taken from it).</summary>
        public void Update(IEnumerable<string> online, bool listHasYou, DateTime utcNow)
        {
            if (!listHasYou) return;
            var now = new HashSet<string>((online ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)), StringComparer.OrdinalIgnoreCase);
            // off the list for longer than Absence: gone, a return is a new arrival (a death and respawn is shorter)
            foreach (var gone in firstSeen.Keys.Where(k => !now.Contains(k) && (utcNow - lastSeen[k]).TotalSeconds > Absence).ToList()) { firstSeen.Remove(gone); lastSeen.Remove(gone); }
            foreach (var n in now) { if (!firstSeen.ContainsKey(n)) firstSeen[n] = seeded ? utcNow : DateTime.MinValue; lastSeen[n] = utcNow; }   // there before you: not joining
            present.Clear(); present.UnionWith(now); ever.UnionWith(now);
            seeded = true;
        }

        /// <summary>The joining names, in the order they appeared. <paramref name="withCopy"/>: the names of the fellows whose copy is here.</summary>
        public List<string> Joining(IEnumerable<string> withCopy, string self, DateTime utcNow)
        {
            var have = new HashSet<string>(withCopy ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return firstSeen.Where(kv => present.Contains(kv.Key) && !have.Contains(kv.Key) && !string.Equals(kv.Key, self, StringComparison.OrdinalIgnoreCase) && (utcNow - kv.Value).TotalSeconds < Window)
                            .OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
        }

        /// <summary>Whether this name was in the world at any time this connection (null: the list was not read yet, so nobody knows).</summary>
        public bool? SeenThisConnection(string name) => !seeded ? (bool?)null : !string.IsNullOrEmpty(name) && ever.Contains(name);

        /// <summary>A new connection: the next list seeds again.</summary>
        public void Clear() { firstSeen.Clear(); lastSeen.Clear(); present.Clear(); ever.Clear(); seeded = false; }
    }
}
