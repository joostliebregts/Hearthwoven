using System.Collections.Generic;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// The game's enum names, each worked out once (0.8.1 performance pass). The panel reads the character's counters (PlayerStatType),
    /// skills (Skills.SkillType) and its own keys (KeyCode) by name on every refresh, and Mono's Enum.ToString looks the name up by
    /// reflection each time: a few hundred calls per refresh, in the game's gather of about 1.15 ms (Dev.Bench, Joost's PC). The text is
    /// exactly ToString's, a value without a name (a mod's) included: that is its number, kept the same way. Keyed by the value as an int,
    /// so a lookup boxes nothing; locked, because the snapshot and the day history also read counters off the panel's path.
    /// </summary>
    internal static class GameNames
    {
        static readonly Dictionary<int, string> stats = new Dictionary<int, string>(), skills = new Dictionary<int, string>(), keys = new Dictionary<int, string>();

        public static string Of(PlayerStatType t) { lock (stats) { if (!stats.TryGetValue((int)t, out var s)) stats[(int)t] = s = t.ToString(); return s; } }
        public static string Of(Skills.SkillType t) { lock (skills) { if (!skills.TryGetValue((int)t, out var s)) skills[(int)t] = s = t.ToString(); return s; } }
        public static string Of(KeyCode k) { lock (keys) { if (!keys.TryGetValue((int)k, out var s)) keys[(int)k] = s = k.ToString(); return s; } }
    }
}
