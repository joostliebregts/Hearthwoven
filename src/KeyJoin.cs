using System.Collections.Generic;

namespace Hearthwoven
{
    /// <summary>
    /// Keys made of a head and a token ("placed|$piece_woodwall", "Made|$item_bread"), each joined once (0.8.1 performance pass). The open
    /// book's 2 s refresh reads the game's per-token counters for the day history and Deeds > Recent and joined the same few hundred keys
    /// anew every time; the strings are the same, only made once. Bounded per head (a head past MaxPerHead tokens starts over, as LogKeys
    /// does) and locked: the day history's save reads the counters too.
    /// </summary>
    internal static class KeyJoin
    {
        public const int MaxPerHead = 20000;
        static readonly Dictionary<string, Dictionary<string, string>> joined = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>head + tail, the same string every time.</summary>
        public static string Of(string head, string tail)
        {
            if (head == null || tail == null) return head + tail;
            lock (joined)
            {
                if (!joined.TryGetValue(head, out var d)) joined[head] = d = new Dictionary<string, string>();
                if (d.TryGetValue(tail, out var s)) return s;
                if (d.Count >= MaxPerHead) d.Clear();
                return d[tail] = head + tail;
            }
        }
    }
}
