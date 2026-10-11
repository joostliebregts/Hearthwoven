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
                                   IEnumerable<SkillInfo> skills, string world, DamageTally damage, string session = "", SessionEvents events = null, EventLog log = null, bool share = false,
                                   SessionEvents eventsSinceInstall = null, DamageTally damageSinceInstall = null, BiomeTally biomeSinceInstall = null, System.DateTime? biomeFromUtc = null, FeatsLedger feats = null,
                                   IDictionary<string, float> dealtByDay = null, IEnumerable<string> knownBiomes = null, string copyId = null, FoeShare foes = null, FeedShare feed = null,
                                   IDictionary<string, Dictionary<string, float>> dayDealt = null, string logBefore = null,
                                   IDictionary<string, Dictionary<string, Dictionary<string, float>>> dayDeeds = null)
        {
            // Absolute values only: a resend after a reconnect or crash replaces, never adds.
            var j = new Json().Open()
                .Str("mod", modVersion).Num("playerId", playerId).Str("name", playerName).Str("world", world ?? "").Str("session", session ?? "").Raw("share", share ? "true" : "false");
            // 0.7: which full copy this is (live updates name it as their base, LiveSync.cs); an older reader ignores the key
            if (!string.IsNullOrEmpty(copyId)) j.Str("copyId", copyId);
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
            // since install on the sender's PC (LocalTotals + this session), so fellows' pages do not drop back to zero
            // when they reconnect (SOURCES.md fix 3); an older reader ignores these keys, an older sender leaves them out
            if (eventsSinceInstall != null) eventsSinceInstall.WriteTo(j, "measuredSinceInstall");
            if (damageSinceInstall != null) damageSinceInstall.WriteTo(j, "damageSinceInstall");
            // per biome since install (Battle's All overview for a fellow's book): bounded by the biome list, a few hundred numbers at most
            if (biomeSinceInstall != null)
            {
                j.Key("biomeSinceInstall").Open();
                if (biomeFromUtc.HasValue) j.Str("from", biomeFromUtc.Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
                j.Dict("damage", biomeSinceInstall.Damage).Dict("deaths", biomeSinceInstall.Deaths).Close();
            }
            // the feats earned so far, id -> {tier, utc, biome} (never a place or a counter); an older reader ignores the key
            feats?.WriteShared(j);
            // your damage dealt per local day, the last 30 days (HISTORY-06.md: the only part of the day history that is shared, under 1 KB),
            // so a fellow's Together can show your Today, 7 days and 30 days; an older reader ignores the key
            if (dealtByDay != null && dealtByDay.Count > 0) j.Dict("dealtByDay", dealtByDay);
            // 0.9: the same days split by weapon kind and battle type ("melee|slash", DayHistory.DayDealt), so the group's By weapon and By type work on
            // Today, 7 days and 30 days; written whenever the day history is loaded, also empty, so a reader can tell "nothing dealt" from an older
            // Hearthwoven. Nested per day, so a live update carries only today's parts that changed. An older reader ignores the key
            if (dayDealt != null)
            {
                j.Key("dayDealt").Open();
                foreach (var kv in dayDealt) if (kv.Value != null && kv.Value.Count > 0) j.Dict(kv.Key, kv.Value);
                j.Close();
            }
            // 0.8.5: your Deeds per local day, the last 30 days: per family the parts each Deeds page's hero and first bar show (PanelModel.DayDeeds:
            // wood and ore brought in, pieces built per hammer tab, dishes, plantings, fish, the game's craft, farm, fish and taming counters),
            // so a fellow's Deeds pages and the group's work on Today, 7 days and 30 days. Written whenever the day history is loaded, also empty,
            // so a reader can tell "nothing done" from an older Hearthwoven; nested per day, so a live update carries only today's amounts that
            // changed (and a part name new today). Each family's part names stand once, a day holds its amounts in that order. An older reader
            // ignores the key
            Panel.PanelModel.WriteDayDeeds(j, dayDeeds);
            // the bests behind some feats (Heavy Keel's load, Long Lead's lead): number, day and biome, never a place
            feats?.WriteSharedBests(j);
            // the biomes this character found (0.7: the group feats' gate counts fellows' lands too; GROUP-FEATS.md 1.3): the game's own record mapped
            // to Heightmap.Biome names, at most the nine of the journey. The server keeps and relays a copy whole (GroupShare.Store writes the JSON
            // as sent, less death positions and worlds; GroupServe packs the file's text), so no server change; an older reader ignores the key
            var biomes = knownBiomes?.Where(b => !string.IsNullOrEmpty(b)).Distinct().Take(16).ToList();
            if (biomes != null && biomes.Count > 0) j.Raw("knownBiomes", "[" + string.Join(",", biomes.Select(Json.Q).ToArray()) + "]");
            // 0.8: the foes you fought per kind and what became of them, this session and since recording began (FoeShare: compact, at most 60 kinds
            // each). An older reader ignores the key
            foes?.WriteTo(j);
            // 0.8.2: the battle feed itself, its newest 100 foes, names only of players who share (FeedShare); an older reader ignores the key
            feed?.WriteTo(j);
            // 0.8.2: your kept minutes of earlier connections in this world (KeptMinutes.SharedJson, written once per connection), beside measuredLog,
            // which stays this connection's (the server's sessions and the report add measuredLog up per session); fellows join them for the short
            // windows (FellowTrail.Joined). An older reader ignores the key; the server keeps it as sent, less death positions
            if (!string.IsNullOrEmpty(logBefore)) j.Raw(KeptMinutes.SharedKey, logBefore);
            return j.Close().ToString();
        }
    }
}
