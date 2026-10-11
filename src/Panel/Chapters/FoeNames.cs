using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8.5: one row per foe by the game's name (WISHLIST, found by v082-rocks: Draugr and the ranged Draugr, the Skeleton kinds, Goblin and
    /// Goblin archer, Greydwarf and the frozen Greydwarf were separate rows with one label). The game gives several creature prefabs one name;
    /// the book keeps recording per prefab (the log, the battle record, the day rows, the fights: no migration) and groups when a page reads:
    /// every place a foe is a row or a filter keys it by FoeGroup, the prefab that stands for every creature of its name (the main prefab).
    /// Its name, trophy and multipliers are the row's; where the kinds struck take damage differently, the open foe says so (KinDiffers). The
    /// join of one creature's hits across PCs (FoeId) is not touched. The names a page takes from the game lose their color tags here (Named,
    /// through PanelRich.Untagged).
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>
        /// The key of a foe's row: the main prefab of every creature the game gives this one's name (PanelInput.FoeKin), else the prefab itself
        /// (no other creature has its name, the game data is not loaded, or it is a cause and no creature: a fall, smoke). 0.8.5: also the kind
        /// of a kill token ("$enemy_babyseeker": the game counts kills by the creature's name token, Character.m_name), so a page that has only
        /// the token joins it to the foe's row: the game's own data first (GameData.FoeKin knows every creature's token), else the one alias
        /// table (KillTokens), else the kind the token spells ("$enemy_troll" -> "Troll").
        /// </summary>
        public static string FoeGroup(PanelInput input, string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            var k = input?.FoeKin == null ? null : Ask(input.FoeKin, key);
            if (!string.IsNullOrEmpty(k)) return k;
            if (key[0] != '$') return key;
            var kind = TokenKinds.TryGetValue(key, out var p) ? p : Spelled(key);
            return kind == null ? key : FoeGroup(input, kind);
        }

        /// <summary>
        /// 0.8.5 (the daily report found it: "Also defeated: Babyseeker 124" while its Seeker Brood row had no x N): the one table of creature
        /// kinds whose kill token says something else than their prefab, prefab -> the game's token. Checked on 11 Oct against the players' own
        /// copies: each kind's defeats in Hearthwoven's record beside the game's kill table (SeekerBrood 123 of $enemy_babyseeker 178, TentaRoot_wild
        /// 85 of $enemy_root 87, Dverger 6 of $enemy_dvergr 26, DvergerMage 1 of $enemy_dvergr_mage 17, Greydwarf_Elite 3 of $enemy_greydwarfbrute 16;
        /// the game counts since the character began); Hatchling ($enemy_drake) and Brenna ($enemy_skeletonfire) by the report's table and the game's
        /// English names; Zil and Thungr by their names and trophies only (TrophyGoblinBruteBrosShaman is Zil's), not yet in a player's counts.
        /// The book's own x N, the feed and the fights never need it: a hit and the kill message both carry the creature's own token
        /// (BattleRecorder.Killed). Kinds of one name (Draugr_Ranged, GoblinArcher) share their main kind's token and are not listed.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> KillTokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SeekerBrood"] = "$enemy_babyseeker", ["TentaRoot_wild"] = "$enemy_root", ["Dverger"] = "$enemy_dvergr", ["DvergerMage"] = "$enemy_dvergr_mage",
            ["Greydwarf_Elite"] = "$enemy_greydwarfbrute", ["Hatchling"] = "$enemy_drake", ["Skeleton_Hildir"] = "$enemy_skeletonfire",
            ["GoblinShaman_Hildir"] = "$enemy_goblin_hildir", ["GoblinBruteBros"] = "$enemy_goblinbrute_hildir",
        };
        static readonly Dictionary<string, string> TokenKinds = KillTokens.GroupBy(kv => kv.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).OrderBy(x => x.Length).ThenBy(x => x, StringComparer.Ordinal).First(), StringComparer.Ordinal);

        // the kind a kill token spells when nothing else names it: "$enemy_troll" -> "Troll" (the game's own prefabs mostly are that word)
        static string Spelled(string token)
        {
            const string Head = "$enemy_";
            if (!token.StartsWith(Head, StringComparison.Ordinal) || token.Length == Head.Length) return null;
            return char.ToUpperInvariant(token[Head.Length]) + token.Substring(Head.Length + 1);
        }

        /// <summary>
        /// The kin map, from the game's creatures (prefab, the name it shows, its name token): every prefab whose name another prefab shares -> the
        /// main prefab of that name, the shortest prefab name, then the first by ordinal ("Draugr" for "Draugr" and "Draugr_Ranged", "Skeleton" for
        /// the Skeleton kinds); and every token -> the main prefab of its creatures (the game's own join of a kill to its kind, 0.8.5). A creature
        /// alone with its name, or with no name the game knows ("[...]", "$..."), keeps its prefab: it is its own row. Names compare as shown
        /// (PanelRich.Untagged), so "&lt;color=orange&gt;Brenna&lt;/color&gt; " and "Brenna" are one name. GameData reads it once from ZNetScene.
        /// </summary>
        public static Dictionary<string, string> FoeKinOf(IEnumerable<(string prefab, string name, string token)> creatures)
        {
            var kin = new Dictionary<string, string>(StringComparer.Ordinal);
            var list = (creatures ?? Enumerable.Empty<(string, string, string)>()).Where(c => !string.IsNullOrEmpty(c.prefab)).ToList();
            string Main(IEnumerable<string> prefabs) => prefabs.OrderBy(p => p.Length).ThenBy(p => p, StringComparer.Ordinal).First();
            var named = list.Select(c => (c.prefab, name: PanelRich.Untagged(c.name))).Where(c => !string.IsNullOrEmpty(c.name) && !c.name.StartsWith("[") && !c.name.StartsWith("$"));
            foreach (var g in named.GroupBy(c => c.name, StringComparer.Ordinal))
            {
                var prefabs = g.Select(c => c.prefab).Distinct(StringComparer.Ordinal).ToList();
                if (prefabs.Count < 2) continue;
                var main = Main(prefabs);
                foreach (var p in prefabs) kin[p] = main;
            }
            foreach (var g in list.Where(c => !string.IsNullOrEmpty(c.token) && c.token[0] == '$').GroupBy(c => c.token, StringComparer.Ordinal))
            {
                var main = Main(g.Select(c => c.prefab).Distinct(StringComparer.Ordinal));
                kin[g.Key] = kin.TryGetValue(main, out var m) ? m : main;
            }
            return kin;
        }

        /// <summary>The kin map from (prefab, the name it shows) alone, without tokens (the samples).</summary>
        public static Dictionary<string, string> FoeKinOf(IEnumerable<KeyValuePair<string, string>> creatures) =>
            FoeKinOf((creatures ?? Enumerable.Empty<KeyValuePair<string, string>>()).Select(c => (c.Key, c.Value, (string)null)));

        /// <summary>The game's name for a prefab or token as the pages show it (PanelInput.DisplayName, its color tags off: PanelRich.Untagged); null when
        /// it gives none.</summary>
        static string Named(PanelInput i, string key) => PanelRich.Untagged(Ask(i?.DisplayName, key));

        /// <summary>Counts per foe kind keyed as the rows are (FoeGroup): the counts of the kinds of one name added up. The same object when no
        /// kind changes key (no copy on a book without kin).</summary>
        static FoeCounts ByFoeName(PanelInput input, FoeCounts counts)
        {
            if (counts == null || input?.FoeKin == null || counts.Kinds.Keys.All(k => FoeGroup(input, k) == k)) return counts;
            var grouped = new FoeCounts();
            foreach (var kv in counts.Kinds) grouped.Of(FoeGroup(input, kv.Key)).Add(kv.Value);
            return grouped;
        }

        /// <summary>
        /// A fight's foe rows by the game's name (FoeGroup), in the fight's order: the kinds of one name are one row (a boss row never with a
        /// row that is no boss), its Prefab the main one, its counts and every player's parts added up. A kind alone with its name is the fight's
        /// own row. The fight (FightView.Foes, its sums) is not changed.
        /// </summary>
        public static List<FightFoe> FoeRowsOf(PanelInput input, FightView f)
        {
            var rows = new List<FightFoe>();
            if (f == null) return rows;
            foreach (var g in f.Foes.GroupBy(x => (key: FoeGroup(input, x.Prefab), x.Boss)))
            {
                var list = g.ToList();
                if (list.Count == 1 && list[0].Prefab == g.Key.key) { rows.Add(list[0]); continue; }
                var row = new FightFoe { Prefab = g.Key.key, Boss = g.Key.Boss, Count = 0 };
                foreach (var x in list)
                {
                    row.Count += x.Count; row.Defeated += x.Defeated;
                    foreach (var kv in x.By)
                    {
                        if (!row.By.TryGetValue(kv.Key, out var part)) row.By[kv.Key] = part = new Hearthwoven.Panel.FightPart();   // the fight's part (FightView.cs), not Last fight's PanelModel.FightPart
                        foreach (var d in kv.Value.Dealt) { part.Dealt.TryGetValue(d.Key, out var o); part.Dealt[d.Key] = o + d.Value; }
                        foreach (var d in kv.Value.Received) { part.Received.TryGetValue(d.Key, out var o); part.Received[d.Key] = o + d.Value; }
                    }
                }
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>The fight's boss row by the game's name (FoeRowsOf), or null.</summary>
        static FightFoe BossRowOf(PanelInput input, FightView f) => FoeRowsOf(input, f).FirstOrDefault(x => x.Boss);

        /// <summary>
        /// The open foe's line when the kinds of its name you struck take damage differently (a mod's frozen Greydwarf beside the game's own): the
        /// multipliers and the ranking are the main kind's (FoeGroup), and this says so. Null when they agree, or the game knows none of the others.
        /// </summary>
        public static string KinDiffers(PanelInput input, string key, IEnumerable<string> struck)
        {
            var main = FoeOf(input, key);
            if (main == null) return null;
            foreach (var p in struck.Where(p => p != key).Distinct(StringComparer.Ordinal))
            {
                var other = FoeOf(input, p);
                if (other != null && BattleTypes.Any(t => Multiplier(main.Modifiers.TryGetValue(t, out var a) ? a : null) != Multiplier(other.Modifiers.TryGetValue(t, out var b) ? b : null)))
                    return KinDiffersLine(Who(input, key));
            }
            return null;
        }

        /// <summary>What the open foe says when the kinds of its name take damage differently (KinDiffers).</summary>
        public static string KinDiffersLine(string name) => "Not every " + name + " takes damage the same way. These multipliers are those of the main kind.";
    }
}
