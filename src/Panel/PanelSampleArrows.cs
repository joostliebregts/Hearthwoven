using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// SAMPLE for the 0.9 arrows track (Chapters/BestArrows.cs): what the sample arrows' recipes ask (close to the game's own, fictional where the
    /// game has no such arrow: the Bloodgold Arrow is the board's invented one), two Ashlands arrows, three Ashlands foes, and Rowan's Ashlands
    /// evening with the land per minute (DeedLog.Lands): Joost's case of 18 Oct, Fallen Valkyrie with Charred Arrow 82 = Silver Arrow 82, the
    /// Bloodgold Arrow 100 not known yet, its materials brought in there. Pure sample data: the test runners and the previews read it.
    /// </summary>
    public static class ArrowsSample
    {
        public const string Valkyrie = "FallenValkyrie", Asksvin = "Asksvin", Charred = "Charred_Melee";
        public const string Warlock = "Charred_Mage", Morgen = "Morgen", Volture = "Volture", LavaBlob = "BlobLava", Marksman = "Charred_Archer", Twitcher = "Charred_Twitcher";
        public const string Land = "AshLands", Home = "Meadows";

        /// <summary>The shown names this sample adds (prefabs and tokens), read before the world's own.</summary>
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["ArrowCharred"] = "Charred Arrow", ["ArrowBloodgold"] = "Bloodgold Arrow", ["$item_arrow_charred"] = "Charred Arrow", ["$item_arrow_bloodgold"] = "Bloodgold Arrow",
            [Valkyrie] = "Fallen Valkyrie", [Asksvin] = "Asksvin", [Charred] = "Charred Warrior",
            ["$item_wood"] = "Wood", ["$item_feathers"] = "Feathers", ["$item_resin"] = "Resin", ["$item_obsidian"] = "Obsidian", ["$item_freezegland"] = "Freeze Gland",
            ["$item_needle"] = "Needle", ["$item_ooze"] = "Ooze", ["$item_silver"] = "Silver", ["$item_carapace"] = "Carapace", ["$item_ashwood"] = "Ashwood",
            ["$item_grausten"] = "Grausten", ["$item_flametal"] = "Flametal",
            [Warlock] = "Charred Warlock", [Morgen] = "Morgen", [Volture] = "Volture", [LavaBlob] = "Lava Blob", [Marksman] = "Charred Marksman", [Twitcher] = "Charred Twitcher",
            ["ArrowBlackmetal"] = "Blackmetal Arrow", ["$item_arrow_blackmetal"] = "Blackmetal Arrow", ["$item_blackmetal"] = "Black Metal", ["$item_charredbone"] = "Charred Bone",
            ["$item_bonefragments"] = "Bone Fragments", ["$item_iron"] = "Iron", ["$item_bronze"] = "Bronze", ["$item_finewood"] = "Fine Wood", ["$item_leatherscraps"] = "Leather Scraps",
            ["$item_trollhide"] = "Troll Hide", ["$item_surtlingcore"] = "Surtling Core",
        };

        // what each arrow's recipe asks (token -> ingredient, amount)
        static readonly Dictionary<string, (string token, int n)[]> Costs = new Dictionary<string, (string token, int n)[]>
        {
            ["$item_arrow_wood"] = new[] { ("$item_wood", 8), ("$item_feathers", 2) },
            ["$item_arrow_fire"] = new[] { ("$item_wood", 8), ("$item_resin", 8), ("$item_feathers", 2) },
            ["$item_arrow_frost"] = new[] { ("$item_wood", 8), ("$item_obsidian", 4), ("$item_freezegland", 1), ("$item_feathers", 2) },
            ["$item_arrow_needle"] = new[] { ("$item_needle", 4), ("$item_feathers", 2) },
            ["$item_arrow_poison"] = new[] { ("$item_wood", 8), ("$item_obsidian", 4), ("$item_ooze", 2), ("$item_feathers", 2) },
            ["$item_arrow_silver"] = new[] { ("$item_wood", 8), ("$item_silver", 1), ("$item_feathers", 2) },
            ["$item_arrow_carapace"] = new[] { ("$item_carapace", 4), ("$item_feathers", 2) },
            ["$item_arrow_charred"] = new[] { ("$item_charredbone", 4), ("$item_feathers", 2), ("$item_ashwood", 8) },
            ["$item_arrow_blackmetal"] = new[] { ("$item_wood", 8), ("$item_blackmetal", 2), ("$item_feathers", 2) },
            // the sample bolts and weapons (PanelSample.SampleGear), close to the game's recipes
            ["$item_bolt_bone"] = new[] { ("$item_bonefragments", 8), ("$item_feathers", 1) },
            ["$item_bolt_iron"] = new[] { ("$item_wood", 8), ("$item_iron", 1), ("$item_feathers", 1) },
            ["$item_bolt_blackmetal"] = new[] { ("$item_wood", 8), ("$item_blackmetal", 2), ("$item_feathers", 1) },
            ["$item_bolt_carapace"] = new[] { ("$item_carapace", 4), ("$item_feathers", 1) },
            ["$item_sword_bronze"] = new[] { ("$item_bronze", 8), ("$item_wood", 2), ("$item_leatherscraps", 2) },
            ["$item_sword_iron"] = new[] { ("$item_iron", 20), ("$item_wood", 2), ("$item_leatherscraps", 3) },
            ["$item_sword_blackmetal"] = new[] { ("$item_blackmetal", 20), ("$item_finewood", 2), ("$item_leatherscraps", 3) },
            ["$item_mace_bronze"] = new[] { ("$item_bronze", 8), ("$item_wood", 4) },
            ["$item_mace_iron"] = new[] { ("$item_iron", 20), ("$item_wood", 4) },
            ["$item_atgeir_iron"] = new[] { ("$item_iron", 30), ("$item_wood", 10) },
            ["$item_spear_bronze"] = new[] { ("$item_bronze", 6), ("$item_wood", 5) },
            ["$item_axe_iron"] = new[] { ("$item_iron", 20), ("$item_wood", 10) },
            ["$item_bow_finewood"] = new[] { ("$item_finewood", 10), ("$item_bronze", 2) },
            ["$item_crossbow_arbalest"] = new[] { ("$item_blackmetal", 10), ("$item_finewood", 10), ("$item_iron", 4) },
            ["$item_staff_fireball"] = new[] { ("$item_surtlingcore", 10), ("$item_trollhide", 2) },
            ["$item_arrow_bloodgold"] = new[] { ("$item_grausten", 2), ("$item_flametal", 1), ("$item_feathers", 2) },
        };

        /// <summary>The recipe of a sample arrow in the armor sets' ingredient form; empty for an arrow the sample gives none.</summary>
        public static List<ArmorCost> CostOf(string token) =>
            token != null && Costs.TryGetValue(token, out var c) ? c.Select(x => new ArmorCost { Token = x.token, Name = Names.TryGetValue(x.token, out var n) ? n : null, Amount = x.n }).ToList() : new List<ArmorCost>();

        static PanelModel.ArrowData A(string prefab, string token, params (string t, float v)[] dmg)
        {
            var a = new PanelModel.ArrowData { Prefab = prefab, Token = token, Cost = CostOf(token) };
            foreach (var (t, v) in dmg) a.Damage[t] = v;
            return a;
        }

        /// <summary>The sample arrows and two of the Ashlands: Charred (52 pierce, 30 fire) and Bloodgold (70 pierce, 30 fire).</summary>
        public static IList<PanelModel.ArrowData> Arrows() =>
            PanelSample.SampleArrows().Concat(new[] { A("ArrowCharred", "$item_arrow_charred", ("pierce", 52), ("fire", 30)), A("ArrowBloodgold", "$item_arrow_bloodgold", ("pierce", 70), ("fire", 30)),
                                                      A("ArrowBlackmetal", "$item_arrow_blackmetal", ("blunt", 52), ("pierce", 26)) }).ToList();

        /// <summary>The three Ashlands foes (fictional modifiers): the Fallen Valkyrie takes spirit at x1.5 (so Silver 52 + 20 x 1.5 = 82 = Charred 52 + 30),
        /// the Asksvin all at x1, the Charred Warrior fire at x0.5 and spirit at x1.5.</summary>
        public static PanelModel.FoeData Foe(string prefab)
        {
            var f = new PanelModel.FoeData();
            foreach (var t in PanelModel.BattleTypes) f.Modifiers[t] = "Normal";
            switch (prefab)
            {
                case Valkyrie: f.Modifiers["spirit"] = "Weak"; f.Trophy = "TrophyFallenValkyrie"; return f;
                case Asksvin: f.Trophy = "TrophyAsksvin"; return f;
                case Charred: f.Modifiers["fire"] = "Resistant"; f.Modifiers["spirit"] = "Weak"; f.Trophy = "TrophyCharredMelee"; return f;
                // What to use's evening (the multipliers of board 7's prototype, Joost's field guide): four more, and two for more foes than columns
                case Warlock: f.Modifiers["pierce"] = "Resistant"; f.Modifiers["frost"] = "Resistant"; f.Modifiers["spirit"] = "Weak"; f.Modifiers["poison"] = "Immune"; f.Trophy = "TrophyCharredMage"; return f;
                case Morgen: f.Modifiers["pierce"] = "Resistant"; f.Modifiers["blunt"] = "Resistant"; f.Modifiers["fire"] = "Resistant"; f.Trophy = "TrophyMorgen"; return f;
                case Volture: f.Modifiers["frost"] = "Weak"; f.Trophy = "TrophyVolture"; return f;
                case LavaBlob: f.Modifiers["pierce"] = "Resistant"; f.Modifiers["blunt"] = "Weak"; f.Modifiers["frost"] = "Weak"; f.Modifiers["fire"] = "Immune"; f.Trophy = "TrophyBlobLava"; return f;
                case Marksman: f.Modifiers["fire"] = "Resistant"; f.Modifiers["spirit"] = "Weak"; f.Trophy = "TrophyCharredArcher"; return f;
                case Twitcher: f.Modifiers["slash"] = "Weak"; f.Modifiers["fire"] = "Resistant"; f.Trophy = "TrophyCharredTwitcher"; return f;
                default: return null;
            }
        }

        /// <summary>The recipes the sample character knows here: the world's, and the Charred and Silver Arrows (not the Bloodgold Arrow).</summary>
        public static readonly HashSet<string> Known = new HashSet<string> { "$item_arrow_charred", "$item_arrow_silver", "$item_arrow_blackmetal" };

        /// <summary>
        /// A DeedLog recorded the way the game records it (DeedLog.Tick each minute, NoteLand in it), from <paramref name="start"/> to <paramref name="now"/>:
        /// each stretch (minutes from start) is spent in its land, its pickups spread one per minute over it.
        /// </summary>
        public static DeedLog Log(DateTime start, DateTime now, params (int from, int to, string land, (string token, int n)[] pickups)[] stretches)
        {
            var ev = new SessionEvents(); var game = new Dictionary<string, float>(); var log = new DeedLog();
            log.Tick(start, ev, () => game);
            var total = (int)Math.Floor((now - start).TotalMinutes);
            for (int m = 0; m <= total; m++)
            {
                log.Tick(start.AddMinutes(m), ev, () => game);
                foreach (var s in stretches.Where(s => m >= s.from && m < s.to))
                {
                    log.NoteLand(start.AddMinutes(m).AddSeconds(30), s.land);
                    var span = s.to - s.from;
                    foreach (var (token, n) in s.pickups)
                        for (int k = 0; k < n; k++) if (s.from + (k * span) / n == m) SessionEvents.Add(ev.PickedUp, token, 1);
                }
            }
            log.Flush(now, ev, game);
            return log;
        }

        /// <summary>
        /// Rowan's Ashlands evening on the sample world: home in the Meadows for the session's first half hour, then the Ashlands; hits on the
        /// Fallen Valkyrie (most), an Asksvin and a Charred Warrior, the last fight on the Valkyrie alone; Grausten, Flametal, Ashwood and Feathers
        /// brought in there. The battle record replayed from the log (Last fight).
        /// </summary>
        public static PanelInput World(DateTime now, bool many = false)
        {
            var w = PanelSample.Full(now);
            var start = w.SessionStartUtc ?? now.AddHours(-2);
            var names = w.DisplayName; w.DisplayName = t => t != null && Names.TryGetValue(t, out var n) ? n : names?.Invoke(t);
            var foe = w.Foe; w.Foe = p => Foe(p) ?? foe?.Invoke(p);
            w.Arrows = Arrows;
            var recipes = w.RecipeKnown; w.RecipeKnown = t => Known.Contains(t ?? "") || (recipes != null && recipes(t));
            var log = new EventLog();
            void Hit(int minutesAgo, string who, string skill, params (string t, float v)[] d)
            {
                var dt = new HitData.DamageTypes();
                foreach (var (t, v) in d) switch (t) { case "pierce": dt.m_pierce += v; break; case "slash": dt.m_slash += v; break; case "fire": dt.m_fire += v; break; case "blunt": dt.m_blunt += v; break; }
                log.AddDamage(now.AddMinutes(-minutesAgo), Land, true, who, skill, dt);
            }
            Hit(52, Valkyrie, "Bows", ("pierce", 160)); Hit(51, Valkyrie, "Swords", ("slash", 240)); Hit(50, Valkyrie, "Bows", ("pierce", 82));
            Hit(40, Asksvin, "Bows", ("pierce", 120)); Hit(39, Asksvin, "Swords", ("slash", 90));
            Hit(30, Charred, "Swords", ("slash", 150)); Hit(29, Charred, "Bows", ("pierce", 60));
            Hit(12, Charred, "Swords", ("slash", 70));
            Hit(46, Warlock, "Bows", ("pierce", 90)); Hit(45, Morgen, "Swords", ("slash", 140)); Hit(44, Volture, "Bows", ("pierce", 70)); Hit(35, LavaBlob, "Clubs", ("blunt", 60));
            if (many) { Hit(25, Marksman, "Bows", ("pierce", 40)); Hit(24, Twitcher, "Swords", ("slash", 30)); Hit(23, Charred, "Bows", ("pierce", 20)); }
            Hit(7, Valkyrie, "Bows", ("pierce", 164)); Hit(6, Valkyrie, "Swords", ("slash", 120));
            w.Log = log; w.LogBefore = null; w.LogSessionFromUtc = null; w.LogBeforeFromUtc = null;
            w.Session = SampleWorld.TallyOf(log); w.DamageSinceInstall = PanelSample.SinceInstall(log);
            BattleSample.Session(w, now, new string[0]);
            var total = (int)Math.Floor((now - start).TotalMinutes);
            w.Deeds = Log(start, now,
                (0, 30, Home, new[] { ("$item_wood", 30), ("$item_feathers", 2) }),
                (30, total + 1, Land, new[] { ("$item_grausten", 14), ("$item_flametal", 3), ("$item_ashwood", 20), ("$item_feathers", 6), ("$item_wood", 20), ("$item_obsidian", 6),
                                              ("$item_freezegland", 2), ("$item_silver", 2), ("$item_charredbone", 6) }));
            var owned = w.Owned; w.Owned = t => t == "$item_arrow_silver" || (owned != null && owned(t));   // a stack of Silver Arrows in the bag
            return w;
        }
    }
}
