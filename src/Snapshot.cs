using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// Everything the character file already counts, as JSON: retroactive to the character's creation.
    /// Pure C# (no Unity calls), so it is unit-tested outside the game.
    /// </summary>
    public static class Snapshot
    {
        public struct SkillInfo { public string Name; public float Level, Accumulator; }

        public static string Build(string modVersion, long playerId, string playerName, PlayerProfile.PlayerStats[] stats,
                                   IEnumerable<SkillInfo> skills, string world, DamageTally damage, string session = "", SessionEvents events = null, EventLog log = null, bool share = false)
        {
            // Absolute values only: a resend after a reconnect or crash replaces, never adds.
            var j = new Json().Open()
                .Str("mod", modVersion).Num("playerId", playerId).Str("name", playerName).Str("world", world ?? "").Str("session", session ?? "").Raw("share", share ? "true" : "false");
            j.Key("stats").OpenArr();
            // Only slot 0: the raw totals. The other slots are per difficulty and overlap, so adding them double-counts
            // (lesson from DudeWhatAreMyStats, see README credits).
            for (int i = 0; i < 1 && i < stats.Length; i++)
            {
                var s = stats[i];
                if (s == null || s.m_stats.Values.All(v => v == 0f)) continue;
                j.Open().Num("index", i)
                    .Dict("counters", s.m_stats.Select(kv => new KeyValuePair<string, float>(kv.Key.ToString(), kv.Value)))
                    .Dict("secondsPerWorld", s.m_knownWorlds)
                    .Dict("itemsPickedUp", s.m_itemPickupStats)
                    .Dict("itemsCrafted", s.m_itemCraftStats)
                    .Dict("harvested", s.m_pickableStats)
                    .Dict("foodEaten", s.m_foodEatenStats)
                    .Dict("piecesPlaced", s.m_piecesPlacedStats);
                j.Key("enemyKills").OpenArr();
                for (int k = 0; k < s.m_enemyStats.Length; k++) j.Open().Num("table", k).Dict("kills", s.m_enemyStats[k] ?? new Dictionary<string, float>()).Close();
                j.CloseArr().Close();
            }
            j.CloseArr();
            j.Key("skills").OpenArr();
            foreach (var sk in skills) j.Open().Str("skill", sk.Name).Num("level", sk.Level).Num("progress", sk.Accumulator).Close();
            j.CloseArr();
            // "profile" = the game's own counters since the character was made; "measured*" = Hearthwoven since install.
            if (damage != null) damage.WriteTo(j);
            if (events != null) events.WriteTo(j);
            if (log != null) log.WriteTo(j);
            return j.Close().ToString();
        }
    }
}
