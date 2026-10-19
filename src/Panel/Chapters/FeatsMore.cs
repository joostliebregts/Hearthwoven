using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 more feats (work/hearthwoven-0.9/FEAT-CATALOG.md, Joost 18 Oct: "I just want more feats in-game and adjust as we go"): the catalog's
    /// RECORDED feats, the client-side NEEDS NEW ones (counted by FeatsHooks into the ledger from the day this version first ran, LocalTotals.StartFeats09),
    /// and a few per role (Joost: "archery/ranged (x-bow and bow), magic stuff, tanky stuff"): Bowhand, Staffbearer, and more for the Shieldbearer.
    /// Each counts only what a record holds; where a record does not hold what the catalog first said, the rule says what it does hold.
    /// FeatDefs orders every personal feat by its family (InFamilyOrder), the table's own feats first within a family.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>Personal feats in family order, each family's own order kept (a stable sort).</summary>
        static IEnumerable<FeatDef> InFamilyOrder(IEnumerable<FeatDef> defs)
        {
            var order = new[] { "cook", "smith", "hauler", "sailor", "defender", "fighter", "melee", "archer", "mage", "bossbane", "delver", "explorer", "mapmaker",
                                "builder", "mender", "wallwarden", "woodcutter", "miner", "farmer", "tamer", "fisher", "helper" };   // local: static fields of partial files load in no set order
            int at(FeatDef d) { var k = Array.IndexOf(order, d.Family); return k < 0 ? order.Length : k; }
            return defs.Select((d, k) => (d, k)).OrderBy(x => at(x.d)).ThenBy(x => x.k).Select(x => x.d);
        }

        /// <summary>The feats the new hooks count (FeatsCounters, FeatsHooks): their counts on this PC began on the first load of the version that
        /// brought them (LocalTotals.StartFeats09), not with the install.</summary>
        static readonly HashSet<string> SinceV09 = new HashSet<string> { "steadygrill", "wholecrew", "heldtheline", "altar", "menderothers", "raidbreaker" };

        /// <summary>A personal feat of one land (Cold Iron in the Ashes, Tapped the Root) waits off the page, unnamed, until the group found that land, as the
        /// group's land feats do (GroupVisible); once a tier is earned it stays.</summary>
        public static bool OwnVisible(FeatDef d, PanelInput i) => d.Group || d.Biome == null || FeatTier(d, i) > 0 || GroupFound(i).Contains(d.Biome);

        // ---------- the records the new feats read ----------

        static readonly string[] RangedSkills = { "Bows", "Crossbows" }, MagicSkills = { "ElementalMagic", "BloodMagic" };
        /// <summary>The eight battle types (ArmourTally.Types): Every Edge's set.</summary>
        static readonly string[] FeatBattleTypes = { "blunt", "slash", "pierce", "fire", "frost", "lightning", "poison", "spirit" };

        /// <summary>Damage you dealt since install ("foe|skill|type" in DamageSinceInstall.Dealt), summed where the skill and type are kept.</summary>
        static double DealtWhere(PanelInput i, Func<string, bool> skill, Func<string, bool> type)
        {
            var d = i?.DamageSinceInstall?.Dealt; if (d == null) return 0;
            double s = 0;
            foreach (var kv in d)
            {
                var p = kv.Key.Split('|'); if (p.Length < 3) continue;
                if ((skill == null || skill(p[1])) && (type == null || type(p[2]))) s += Math.Max(0, kv.Value);
            }
            return s;
        }

        /// <summary>How many of <paramref name="types"/> you dealt at least <paramref name="least"/> of, with the skills kept (all skills: null).</summary>
        static double TypesDealt(PanelInput i, Func<string, bool> skill, IEnumerable<string> types, double least) =>
            types.Count(t => DealtWhere(i, skill, x => x == t) >= least);

        /// <summary>Your hits after the foe's resistances and armor that hit a weakness: per "foe|skill|type" of the dealt ledger, what got through where
        /// it is more than what you swung (the foe took more than the blow carried). Your own book only (the ledger is never shared).</summary>
        static double WeakDealt(PanelInput i, Func<string, bool> skill)
        {
            if (i == null || !i.IsSelf) return 0;
            var t = i.DealtArmourSince ?? i.DealtArmourSession; if (t == null) return 0;
            double s = 0;
            foreach (var kv in t.After)
            {
                var p = kv.Key.Split('|'); if (p.Length < 3 || (skill != null && !skill(p[1]))) continue;
                var swung = t.Incoming.TryGetValue(kv.Key, out var v) ? v : 0;
                if (kv.Value > swung + 0.5f) s += kv.Value;
            }
            return s;
        }

        /// <summary>What your armor stopped (the armour ledger: before armor less after armor), since install; your own book only.</summary>
        static double ArmorStopped(PanelInput i)
        {
            if (i == null || !i.IsSelf) return 0;
            var t = i.ArmourSince ?? i.ArmourSession; if (t == null) return 0;
            return Math.Max(0, t.Before.Values.Sum(v => (double)v) - t.After.Values.Sum(v => (double)v));
        }

        /// <summary>Times fellow players ate food, or put on gear, that carries your name (their shared tallies, "maker|thing").</summary>
        static double FellowsUsedMine(PanelInput i, Func<SessionEvents, Dictionary<string, float>> pick) =>
            FellowsOf(i).Sum(f => pick(f.Events).Where(kv => kv.Value > 0 && SameName(Split2(kv.Key)[0], i.PlayerName)).Sum(kv => (double)kv.Value));

        /// <summary>The brought-in count (the character's record before install plus Hearthwoven's exact count since, BroughtIn) of a kind's items, kept by token.</summary>
        static double BroughtInOf(PanelInput i, string kind, ICollection<string> tokens) =>
            BroughtIn(i, kind).Where(kv => tokens.Contains(kv.Key)).Sum(kv => kv.Value.before + kv.Value.exact);

        public static readonly HashSet<string> WoodTokens = new HashSet<string>(StringComparer.Ordinal)
            { "$item_wood", "$item_finewood", "$item_roundlog", "$item_elderbark", "$item_yggdrasilwood", "$item_blackwood" };
        /// <summary>Ore as the rock gives it (reference/item-names.json): never the bars (they come out of a smelter).</summary>
        public static readonly HashSet<string> OreTokens = new HashSet<string>(StringComparer.Ordinal)
            { "$item_copperore", "$item_tinore", "$item_ironscrap", "$item_ironore", "$item_silverore", "$item_blackmetalscrap", "$item_flametalore" };
        public static readonly HashSet<string> PortalTokens = new HashSet<string>(StringComparer.Ordinal) { "$piece_portal", "$piece_portal_stone" };
        public static bool IsAmmo(string token) => token != null && (token.StartsWith("$item_arrow_", StringComparison.Ordinal) || token.StartsWith("$item_bolt_", StringComparison.Ordinal));

        /// <summary>Arrows and bolts you made: the crafts in your record and Hearthwoven's count since (both count crafts), each craft what its recipe makes.</summary>
        static double AmmoMade(PanelInput i)
        {
            if (i == null) return 0;
            var tokens = new HashSet<string>((i.ItemsCrafted ?? new Dictionary<string, float>()).Keys.Concat(i.Events?.Made.Keys ?? Enumerable.Empty<string>()).Where(IsAmmo));
            return tokens.Sum(t => RecordAndSince(i, LocalTotals.CraftedKind, i.ItemsCrafted, i.Events?.Made, t) * YieldOf(i, t));
        }

        /// <summary>Different items you upgraded to their highest level (the upgrade record: each item's highest level reached, its top level from the game's data).</summary>
        static double ItemsHoned(PanelInput i)
        {
            var log = i?.Upgrades; if (log == null || !i.IsSelf) return 0;
            var best = new Dictionary<string, int>(StringComparer.Ordinal);
            void Note(string t, int to) { if (string.IsNullOrEmpty(t)) return; best[t] = Math.Max(best.TryGetValue(t, out var b) ? b : 0, to); }
            foreach (var m in log.Recent) Note(m.Token, m.To);
            foreach (var f in log.Folded) Note(f.Token, f.MaxLevel);
            int Top(string t) { try { return i.MaxQuality?.Invoke(t) ?? 0; } catch { return 0; } }
            return best.Count(kv => { var top = Top(kv.Key); return top > 1 && kv.Value >= top; });
        }

        /// <summary>The boss fights won this PC keeps (BossLog: the newest 50 per world).</summary>
        static IEnumerable<BossAttempt> BossesWon(PanelInput i) => i?.BossLog == null || !i.IsSelf ? Enumerable.Empty<BossAttempt>() : i.BossLog.Attempts.Where(a => a.Outcome == BossOutcome.Won);

        /// <summary>The dungeon runs of this world you were in, ended (the fight book's runs, from the run record).</summary>
        static IEnumerable<FightView> RunsEnded(PanelInput i) => i?.Fights == null || !i.IsSelf ? Enumerable.Empty<FightView>() :
            i.Fights.Fights.Where(f => f.Kind == FightKind.Run && !f.IsLive && f.YouWereIn);

        /// <summary>The crops among what you planted (each crop once; tree saplings left out): the planted layers by piece, their crop from the game's data.</summary>
        static double CropsPlanted(PanelInput i)
        {
            var crops = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in PlantedTotals(i))
            {
                if (kv.Value <= 0) continue;
                string crop = null; try { crop = i.CropOf?.Invoke(kv.Key); } catch { crop = null; }
                if (!string.IsNullOrEmpty(crop)) crops.Add(crop);
            }
            return crops.Count;
        }

        static double FishKinds(PanelInput i) => (i?.Harvested ?? new Dictionary<string, float>()).Count(kv => kv.Value > 0 && kv.Key.StartsWith("$animal_fish", StringComparison.Ordinal));
        static double LandsWalked(PanelInput i) => i?.KnownBiomes == null ? 0 : i.KnownBiomes.Count(b => !string.IsNullOrEmpty(b) && b != "None");

        static bool IsRanged(string skill) => Array.IndexOf(RangedSkills, skill) >= 0;
        static bool IsMagic(string skill) => Array.IndexOf(MagicSkills, skill) >= 0;

        // ---------- the table's new rows (tune here) ----------

        static IEnumerable<FeatDef> MoreFeatDefs() => new[]
        {
            // ----- Hearth Cook -----
            new FeatDef("secondhelping", "Second Helping", "cook", Chapter.Deeds, "cooking", "Coming back for more of what {you} made.", new[] { 25.0, 100.0, 400.0 },
                t => "Fellow players enjoyed food {you} made " + N(t) + " times.",
                i => FellowsUsedMine(i, e => e.AteFoodMadeBy),
                CountedFellows, "Only fellow players who share are seen. It counts servings; Full Table counts people.", exact: false, derived: true,
                brief: t => N(t) + " servings enjoyed", unit: "servings"),
            new FeatDef("steadygrill", "Steady at the Grill", "cook", Chapter.Deeds, "cooking", "Not one dish left to burn.", new[] { 50.0 },
                t => "Take " + N(t) + " dishes in a row off a cooking station with none burnt.",
                i => FeatCount(i, FeatsCounters.GrillBest),
                CountedPc, "Only dishes {you} take off. A dish someone else takes off does not break the run.", brief: t => N(t) + " in a row, unburnt", unit: "dishes"),
            // ----- Forgekeeper -----
            new FeatDef("trustededge", "Trusted Edge", "smith", Chapter.Deeds, "crafting", "Gear the hall reaches for again and again.", new[] { 25.0, 100.0, 400.0 },
                t => "Fellow players put on gear {you} made " + N(t) + " times.",
                i => FellowsUsedMine(i, e => e.EquippedGearMadeBy),
                CountedFellows, "Only fellow players who share are seen. It counts putting gear on, not time worn.", exact: false, derived: true,
                brief: t => N(t) + " times put on", unit: "times"),
            new FeatDef("honed", "Honed to the Last", "smith", Chapter.Deeds, "crafting", "Brought to its finest, nothing left to add.", new[] { 1.0, 5.0, 15.0 },
                t => t <= 1 ? "Upgrade one item to its highest level." : "Upgrade " + N(t) + " different items to their highest level.",
                i => ItemsHoned(i),                CountedPc, "Upgrades count from the day this PC began recording them. A mod that raises an item's top level moves the line.",
                brief: t => N(t) + (t <= 1 ? " item at its best" : " items at their best"), unit: "item|items"),
            // Joost on the board: "should go up to 2500". The game books a craft once however many it makes, so each craft counts what its recipe makes
            new FeatDef("arrowsforall", "Arrows for All", "smith", Chapter.Deeds, "crafting", "Quivers filled before the fight.", new[] { 100.0, 500.0, 2500.0 },
                t => "Make " + N(t) + " arrows and bolts.",
                i => AmmoMade(i),                CountedCharacter, "Each craft counts what its recipe makes, as this PC's game data says (one where it does not know).", retro: true,
                brief: t => N(t) + " arrows and bolts", unit: "arrows and bolts"),
            // ----- Storekeeper -----
            new FeatDef("cartroad", "Cart Road", "hauler", Chapter.Voyages, "cargo", "The cart's wheels know the way home.", new[] { 5.0, 25.0, 100.0 },
                t => "Pull a cart " + N(t) + " km in all.",
                i => M(i, e => e.CartMeters) / 1000.0,
                CountedPc, "It counts distance, not load: Ore Road counts the load.", brief: t => N(t) + " km with a cart", unit: "km", decimals: true),
            // ----- Helmskeeper -----
            new FeatDef("wholecrew", "Whole Crew", "sailor", Chapter.Voyages, "sailing", "Every bench full, one hand on the helm.", new[] { 10.0 },
                t => "Steer with 3 fellow players aboard for " + N(t) + " minutes in one go.",
                i => FeatCount(i, FeatsCounters.CrewBest) / 60.0,
                CountedPc, "Looked at every 10 seconds. Fellow players without Hearthwoven count too: they are aboard.",
                brief: t => N(t) + " min, 3 aboard", unit: "minutes", decimals: true),
            // ----- Shieldbearer (the tank's role, Joost: "tanky stuff"): what was held and stopped, never what was taken -----
            new FeatDef("heldtheline", "Held the Line", "defender", Chapter.Battle, "defense", "One fight, the line never broke.", new[] { 20.0 },
                t => "Hold " + N(t) + " blows on {your} shield in one fight with a fellow player near, and stay standing.",
                i => FeatCount(i, FeatsCounters.HeldLineBest),
                CountedPc, "A fight ends after a minute with no hit, the battle record's rule. A fellow player without Hearthwoven counts as near.",
                brief: t => N(t) + " held in one fight", unit: "blows held"),
            new FeatDef("braced", "Braced", "defender", Chapter.Battle, "defense", "Shield up, feet set.", new[] { 500.0, 2500.0, 10000.0 },
                t => "Hold " + N(t) + " blows on {your} shield.",
                i => i.Events == null ? 0 : i.Events.Blocks,
                CountedPc, "Held blocks as the game counts them, parries among them.", brief: t => N(t) + " blows held", unit: "blows held"),
            new FeatDef("ironhide", "Iron Hide", "defender", Chapter.Battle, "defense", "The blows that never reached the skin.", new[] { 2000.0, 10000.0, 50000.0 },
                t => "Let {your} armor stop " + N(t) + " damage.",
                i => ArmorStopped(i),                CountedPc, "What {your} armor stopped, never what got through.", brief: t => N(t) + " armor stopped", unit: "damage"),
            // ----- Battlehand -----
            new FeatDef("shoulder", "Shoulder to Shoulder", "fighter", Chapter.Battle, "foes", "No foe faced alone.", new[] { 25.0, 100.0, 400.0 },
                t => "Defeat " + N(t) + " foes together with a fellow player.",
                i => FoeCountsIn(i, TimeWindow.SinceInstall)?.Total(c => c.With) ?? 0,
                CountedPc, "Who struck is the game's own list, so a fellow player without Hearthwoven counts too. It never says who did more.",
                brief: t => N(t) + " foes with a fellow", unit: "foe|foes"),
            new FeatDef("weakspot", "Struck Where It Hurts", "fighter", Chapter.Battle, "damage", "The right edge for the foe.", new[] { 2000.0, 10000.0, 50000.0 },
                t => "Deal " + N(t) + " damage of a kind the foe is weak to.",
                i => WeakDealt(i, null),
                CountedPc, "A foe's armor can hide a weak spot, so this may read low. Only hits whose armor was counted.", brief: t => N(t) + " to weak spots", unit: "damage"),
            new FeatDef("everyedge", "Every Edge", "fighter", Chapter.Battle, "damage", "Blade, arrow, fire and frost: whatever the fight asks.", new[] { 4.0, 6.0, 8.0 },
                t => "Deal 500 or more damage of each of " + N(t) + " damage types.",
                i => TypesDealt(i, null, FeatBattleTypes, 500),
                CountedPc, "The eight battle types: blunt, slash, pierce, fire, frost, lightning, poison and spirit.", brief: t => N(t) + " damage types", unit: "damage type|damage types"),
            new FeatDef("spiritdead", "Spirit for the Dead", "fighter", Chapter.Battle, "damage", "Silver and spirit against the restless dead.", new[] { 1000.0, 5000.0, 20000.0 },
                t => "Deal " + N(t) + " spirit damage.",
                i => DealtWhere(i, null, x => x == "spirit"),
                CountedPc, null, brief: t => N(t) + " spirit damage", unit: "spirit damage"),
            new FeatDef("coldiron", "Cold Iron in the Ashes", "fighter", Chapter.Battle, "damage", "Frost carried into the burning land.", new[] { 2000.0, 10000.0, 40000.0 },
                t => "Deal " + N(t) + " frost damage in the Ashlands.",
                i => i.BiomeSinceInstall != null && i.BiomeSinceInstall.Damage.TryGetValue("AshLands|dealt|frost", out var v) ? v : 0,
                CountedPc, null, brief: t => N(t) + " frost, Ashlands", unit: "frost damage") { Biome = "AshLands" },
            // ----- Bowhand (Joost: "archery/ranged (x-bow and bow)") -----
            new FeatDef("longreach", "Long Reach", "archer", Chapter.Battle, "damage", "The fight ended before it came close.", new[] { 2000.0, 10000.0, 50000.0 },
                t => "Deal " + N(t) + " damage with bows and crossbows.",
                i => DealtWhere(i, IsRanged, null),
                CountedPc, "Damage as {your} blows carried it, before the foe's armor.", brief: t => N(t) + " from afar", unit: "damage")
                { Picture = "item:$item_bow_finewood|vocab:dmg-pierce" },
            new FeatDef("loosed", "String Never Slack", "archer", Chapter.Battle, "damage", "Draw, loose, draw again.", new[] { 250.0, 1000.0, 5000.0 },
                t => "Loose " + N(t) + " arrows from a bow.",
                i => C(i, "ArrowsShot"),
                CountedCharacter, "The game counts shots from a bow; a crossbow's bolts are not in its count.", retro: true,
                brief: t => N(t) + " arrows loosed", unit: "arrow|arrows") { Picture = "item:$item_arrow_wood|vocab:dmg-pierce" },
            new FeatDef("fletchings", "Many Fletchings", "archer", Chapter.Battle, "damage", "Fire, frost and poison on the wind.", new[] { 2.0, 3.0, 5.0 },
                t => "Deal 250 or more damage of each of " + N(t) + " damage types with bows and crossbows.",
                i => TypesDealt(i, IsRanged, FeatBattleTypes, 250),
                CountedPc, "An arrow's head sets its type: fire, frost, poison, spirit and the rest.", brief: t => N(t) + " types from afar", unit: "damage type|damage types")
                { Picture = "item:$item_arrow_fire|vocab:dmg-fire" },
            // ----- Staffbearer (Joost: "magic stuff") -----
            new FeatDef("eitrunbound", "Eitr Unbound", "mage", Chapter.Battle, "damage", "Power drawn and sent where it was needed.", new[] { 2000.0, 10000.0, 50000.0 },
                t => "Deal " + N(t) + " damage with staffs.",
                i => DealtWhere(i, IsMagic, null),
                CountedPc, "Elemental and blood magic. A summoned skeleton's blows are its own, not counted here.", brief: t => N(t) + " with staffs", unit: "damage")
                { Picture = "item:$item_stafffireball|vocab:dmg-fire" },
            new FeatDef("threestorms", "Fire, Frost and Storm", "mage", Chapter.Battle, "damage", "Every element answered the call.", new[] { 1.0, 2.0, 3.0 },
                t => t <= 1 ? "Deal 1,000 or more fire, frost or lightning damage with staffs." : "Deal 1,000 or more damage with staffs in each of " + N(t) + " of fire, frost and lightning.",
                i => TypesDealt(i, IsMagic, new[] { "fire", "frost", "lightning" }, 1000),
                CountedPc, null, brief: t => N(t) + (t <= 1 ? " element" : " elements") + " by staff", unit: "element|elements")
                { Picture = "item:$item_stafficeshards|vocab:dmg-frost" },
            new FeatDef("rightspell", "The Right Spell", "mage", Chapter.Battle, "damage", "The element the foe feared most.", new[] { 1000.0, 5000.0, 20000.0 },
                t => "Deal " + N(t) + " damage with staffs of a kind the foe is weak to.",
                i => WeakDealt(i, IsMagic),
                CountedPc, "A foe's armor can hide a weak spot, so this may read low. Only hits whose armor was counted.", brief: t => N(t) + " to weaknesses", unit: "damage")
                { Picture = "item:$item_staff_lightning|vocab:dmg-lightning" },
            // ----- Bossbane -----
            new FeatDef("sidebyside", "Side by Side", "bossbane", Chapter.Battle, FeedPage, "The Forsaken fall to many hands.", new[] { 3.0, 10.0, 25.0 },
                t => "Win " + N(t) + " boss fights with fellow players.",
                i => C(i, "BossKillMultiplayer"),
                CountedCharacter, "As the game counts it: a boss defeated with more than one player in the fight. It never names who struck last.", retro: true,
                brief: t => N(t) + " won with fellows", unit: "boss fight|boss fights"),
            new FeatDef("altar", "Before the Altar", "bossbane", Chapter.Battle, FeedPage, "The offering laid, the call answered.", new[] { 1.0 },
                t => "Call a Forsaken at its altar.",
                i => FeatCount(i, FeatsCounters.Summons),
                CountedPc, "Calling is not winning: it counts when the altar answers {you}.", brief: t => "Call a Forsaken", unit: "Forsaken called"),
            new FeatDef("facedtogether", "Forsaken Faced Together", "bossbane", Chapter.Battle, FeedPage, "Each of the old powers, met as a group.", new[] { 1.0, 3.0, 6.0 },
                t => t <= 1 ? "Win a boss fight with a fellow player in it." : "Win boss fights against " + N(t) + " different Forsaken with a fellow player in each.",
                i => BossesWon(i).Where(a => a.Players.Count > 1 || a.Unnamed > 0).Select(a => a.Kind).Where(k => !string.IsNullOrEmpty(k)).Distinct().Count(),
                CountedPc, "From the boss fights this PC keeps, the newest 50 in each world. A tier once reached stays.",
                brief: t => N(t) + " Forsaken, together", unit: "Forsaken"),
            new FeatDef("notonefell", "Not One Fell", "bossbane", Chapter.Battle, FeedPage, "Every one of us walked away.", new[] { 1.0 },
                t => "Win a boss fight with no one who shares falling.",
                i => BossesWon(i).Any(a => a.Players.Count > 0 && a.Players.All(p => p.Deaths == 0)) ? 1 : 0,
                CountedPc, "Only players who share are seen: no one who shares fell.", brief: t => "A boss won, none fell", unit: "boss fight|boss fights"),
            // ----- Delver (no title: dungeon runs have their own record) -----
            new FeatDef("intodark", "Into the Dark", "delver", Chapter.Voyages, "onfoot", "Down the steps, torch first.", new[] { 10.0, 50.0, 150.0 },
                t => "Enter a crypt, cave or mine " + N(t) + " times.",
                i => C(i, "PortalDungeonIn"),
                CountedCharacter, "Stepping out and back in counts again: the game counts doors, not runs.", retro: true,
                brief: t => N(t) + " times inside", unit: "time|times"),
            new FeatDef("deephalls", "Treasure of the Deep Halls", "delver", Chapter.Voyages, "onfoot", "What the dead kept, brought into the light.", new[] { 10.0, 40.0, 120.0 },
                t => "Open " + N(t) + " chests below ground for the first time.",
                i => C(i, "TreasureDungeonFound"),
                CountedCharacter, "Each chest once, the first time {you} open it.", retro: true,
                brief: t => N(t) + " chests below", unit: "chest|chests"),
            new FeatDef("hallsbelow", "Halls Below", "delver", Chapter.Battle, FeedPage, "Many kinds of dark, all of them walked.", new[] { 2.0, 4.0, 6.0 },
                t => "Run through " + N(t) + " different kinds of place below ground.",
                i => RunsEnded(i).Select(r => r.Place).Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                CountedPc, "From the runs this world's record keeps, the newest 50. A tier once reached stays.",
                brief: t => N(t) + " kinds of place", unit: "kind|kinds"),
            new FeatDef("cameback", "Came Back Out Together", "delver", Chapter.Battle, FeedPage, "In together, out together.", new[] { 1.0, 5.0, 15.0 },
                t => t <= 1 ? "Run through a place below ground with a fellow player, and no one who shares falls." : "Run through " + N(t) + " places below ground with a fellow player, and no one who shares falls.",
                i => RunsEnded(i).Count(r => r.Players.Any(p => !p.You) && r.Players.All(p => p.Deaths.Count == 0)),
                CountedPc, "Only fellow players who share are seen inside. From the runs this world's record keeps, the newest 50.",
                brief: t => N(t) + (t <= 1 ? " run" : " runs") + ", none fell", unit: "run|runs"),
            // ----- Trailfinder -----
            new FeatDef("manylands", "Many Lands Walked", "explorer", Chapter.Voyages, "onfoot", "From the meadows to the burning south.", new[] { 3.0, 5.0, 8.0 },
                t => "Set foot in " + N(t) + " lands.",
                i => LandsWalked(i),                CountedCharacter, "The Deep North cannot be entered in the game yet, so eight is every land.", retro: true,
                brief: t => N(t) + " lands walked", unit: "land|lands"),
            new FeatDef("portalraiser", "Portal-Raiser", "explorer", Chapter.Voyages, "onfoot", "A doorway home for everyone.", new[] { 2.0, 6.0, 15.0 },
                t => "Raise " + N(t) + " portals.",
                i => (i.PiecesPlaced ?? new Dictionary<string, float>()).Where(kv => PortalTokens.Contains(kv.Key)).Sum(kv => (double)kv.Value),
                CountedCharacter, "A portal taken down still counts.", retro: true,
                brief: t => N(t) + " portals raised", unit: "portal|portals") { Picture = "vocab:portal-mark" },
            // ----- Mapmaker -----
            new FeatDef("mapgiver", "Map-Giver", "mapmaker", Chapter.Voyages, "maps", "What {you} saw, written down for all.", new[] { 10.0, 25.0, 75.0 },
                t => "Write {your} map to a map table " + N(t) + " times.",
                i => M(i, e => e.MapShared),
                CountedPc, "It counts writes, not new map area.", brief: t => N(t) + " maps shared", unit: "time|times"),
            // ----- Hallwright -----
            new FeatDef("groundbreaker", "Groundbreaker", "builder", Chapter.Deeds, "groundwork", "Level ground under every hall.", new[] { 500.0, 2000.0, 8000.0 },
                t => "Make " + N(t) + " groundwork strokes.",
                i => Placed(i, "ground").Values.Sum(),
                CountedCharacter, "Leveling, raising, paving and the cultivator, as Groundwork counts them.", retro: true,
                brief: t => N(t) + " strokes", unit: "stroke|strokes") { Picture = "vocab:ground-lower" },
            // ----- Mender -----
            new FeatDef("keptstanding", "Kept It Standing", "mender", Chapter.Deeds, "building", "Quiet work that keeps the roof on.", new[] { 100.0, 500.0, 2000.0 },
                t => "Repair " + N(t) + " pieces with the hammer.",
                i => M(i, e => e.Repairs),
                CountedPc, null, brief: t => N(t) + " repairs", unit: "repair|repairs"),
            new FeatDef("menderothers", "Mender of Others", "mender", Chapter.Deeds, "building", "Mending what a fellow built.", new[] { 25.0, 100.0, 400.0 },
                t => "Repair " + N(t) + " pieces a fellow player built.",
                i => FeatCount(i, FeatsCounters.RepairsOthers),
                CountedPc, "Pieces with no builder, the world's own, do not count.", brief: t => N(t) + " mended for others", unit: "repair|repairs"),
            // ----- Wallwarden -----
            new FeatDef("ballista", "Loaded the Ballista", "wallwarden", Chapter.Deeds, "building", "The walls answer back.", new[] { 50.0, 250.0, 1000.0 },
                t => "Load " + N(t) + " bolts and stones into turrets.",
                i => C(i, "TurretAmmoAdded"),
                CountedCharacter, null, retro: true, brief: t => N(t) + " loaded", unit: "bolts and stones"),
            new FeatDef("raidbreaker", "Raid Breaker", "wallwarden", Chapter.Deeds, "building", "When the horns sounded, {you} were there.", new[] { 1.0, 5.0, 15.0 },
                t => t <= 1 ? "Hit a foe while a raid is on around {them}." : "Hit a foe in " + N(t) + " raids.",
                i => FeatCount(i, FeatsCounters.Raids),
                CountedPc, "Counted when {your} hit lands while the game's raid is on around {them}. A raid fought across a reconnect may count twice.",
                brief: t => N(t) + (t <= 1 ? " raid" : " raids") + " fought", unit: "raid|raids"),
            // ----- Woodcutter, Stonebreaker -----
            new FeatDef("timber", "Timber for the Hall", "woodcutter", Chapter.Deeds, "woodcutting", "Every beam begins as a tree.", new[] { 2000.0, 10000.0, 40000.0 },
                t => "Bring in " + N(t) + " wood: wood, fine wood, core wood, ancient bark, Yggdrasil wood and ashwood.",
                i => BroughtInOf(i, "wood", WoodTokens),
                CountedCharacter, "Wood from pieces taken down never counts.", retro: true,
                brief: t => N(t) + " wood brought in", unit: "wood"),
            new FeatDef("deepore", "Ore from the Deep", "miner", Chapter.Deeds, "mining", "Stone split, ore carried out.", new[] { 500.0, 2500.0, 10000.0 },
                t => "Bring in " + N(t) + " ore: copper, tin, iron, silver, black metal and flametal ore.",
                i => BroughtInOf(i, "mining", OreTokens),
                CountedCharacter, "Ore as the rock gives it, never bars.", retro: true,
                brief: t => N(t) + " ore brought in", unit: "ore"),
            // ----- Fieldkeeper -----
            new FeatDef("seedsower", "Seed-Sower", "farmer", Chapter.Deeds, "farming", "Rows in the ground, bread to come.", new[] { 500.0, 2000.0, 10000.0 },
                t => "Put " + N(t) + " plants in the ground.",
                i => M(i, e => e.Planted),
                CountedPc, "Every plant of a planted row counts.", brief: t => N(t) + " plants sown", unit: "plant|plants"),
            new FeatDef("everyfield", "Every Field", "farmer", Chapter.Deeds, "farming", "Carrots to jotun puffs.", new[] { 3.0, 6.0, 9.0 },
                t => "Plant " + N(t) + " different crops.",
                i => CropsPlanted(i),                CountedCharacter, "Tree saplings are left out.", retro: true, brief: t => N(t) + " different crops", unit: "crop|crops"),
            new FeatDef("tappedroot", "Tapped the Root", "farmer", Chapter.Deeds, "farming", "Sap from the ancient roots of the mist.", new[] { 20.0, 100.0, 400.0 },
                t => "Harvest " + N(t) + " sap.",
                i => C(i, "SapHarvested"),
                CountedCharacter, null, retro: true, brief: t => N(t) + " sap", unit: "sap") { Biome = "Mistlands", Picture = "item:$item_sap|title:farmer" },
            // ----- Tidecatcher -----
            new FeatDef("fishhall", "Fish for the Hall", "fisher", Chapter.Deeds, "fishing", "A full line, a full pot.", new[] { 25.0, 100.0, 400.0 },
                t => "Catch " + N(t) + " fish on the line.",
                i => C(i, "FishCaught"),
                CountedCharacter, "Only fish caught on the line count.", retro: true, brief: t => N(t) + " fish caught", unit: "fish"),
            new FeatDef("everywater", "Waters of Every Land", "fisher", Chapter.Deeds, "fishing", "A line in every water.", new[] { 3.0, 6.0, 9.0 },
                t => "Catch " + N(t) + " different kinds of fish.",
                i => FishKinds(i),                CountedCharacter, null, retro: true, brief: t => N(t) + " kinds of fish", unit: "kind|kinds"),
        };

        // ---------- the group's new six (Together) ----------

        static double DishesFromOthers(PanelInput p) => (p?.Events?.AteFoodMadeBy ?? new Dictionary<string, float>())
            .Where(kv => kv.Value > 0 && !string.IsNullOrEmpty(Split2(kv.Key)[0]) && !SameName(Split2(kv.Key)[0], p.PlayerName)).Sum(kv => (double)kv.Value);
        static double BornHere(PanelInput p) => (p?.Events?.BornInCare ?? new Dictionary<string, float>()).Values.Sum(v => (double)v);
        static double Bars(PanelInput p, string token) => RecordAndSince(p, "pickedUp", p.ItemsPickedUp, p.Events?.PickedUp, token);

        static IEnumerable<FeatDef> MoreGroupFeatDefs() => new[]
        {
            new FeatDef("silvervein", "Silver Vein", "smith", Chapter.Feats, FeatsTogetherId, "Silver from the cold heights.", new[] { 100.0, 400.0, 1500.0 },
                t => "Together, take " + N(t) + " silver bars from the smelters.",
                i => GroupSum(i, p => Bars(p, "$item_silver")),
                CountedGroup, "A bar counts once, for whoever takes it first. " + BooksDiffer, exact: false, derived: true,
                brief: t => N(t) + " silver bars", unit: "silver bars", group: true)
            {
                Biome = "Mountain", Picture = "item:$item_silver|title:smith", Part = p => Bars(p, "$item_silver"), Helped = "Who smelted",
                Veil = new FeatVeil
                {
                    Name = "What the Mountain Hides", Honours = "The cold heights hide something worth the climb.", Lifts = new[] { "$item_silverore" },
                    Caveat = "Each counts once, for whoever takes it first.",
                    Rule = t => "Together, bring home " + N(t) + " of what the mountain hides.", Brief = t => N(t) + " from the heights",
                },
            },
            new FeatDef("blackmetal", "Black Metal", "smith", Chapter.Feats, FeatsTogetherId, "Black metal won from the fulings' fields.", new[] { 100.0, 400.0, 1500.0 },
                t => "Together, take " + N(t) + " black metal bars from the smelters.",
                i => GroupSum(i, p => Bars(p, "$item_blackmetal")),
                CountedGroup, "A bar counts once, for whoever takes it first. " + BooksDiffer, exact: false, derived: true,
                brief: t => N(t) + " black metal bars", unit: "black metal bars", group: true)
            {
                Biome = "Plains", Picture = "item:$item_blackmetal|title:smith", Part = p => Bars(p, "$item_blackmetal"), Helped = "Who smelted",
                Veil = new FeatVeil
                {
                    Name = "What the Plains Bury", Honours = "The wide fields bury something worth the fight.", Lifts = new[] { "$item_blackmetalscrap" },
                    Caveat = "Each counts once, for whoever takes it first.",
                    Rule = t => "Together, bring home " + N(t) + " of what the plains bury.", Brief = t => N(t) + " from the plains",
                },
            },
            new FeatDef("eitr", "Eitr Refined", "smith", Chapter.Feats, FeatsTogetherId, "Power drawn from the mist.", new[] { 50.0, 200.0, 600.0 },
                t => "Together, take " + N(t) + " refined eitr from the refinery.",
                i => GroupSum(i, p => Bars(p, "$item_eitr")),
                CountedGroup, "Eitr counts once, for whoever takes it first. " + BooksDiffer, exact: false, derived: true,
                brief: t => N(t) + " refined eitr", unit: "refined eitr", group: true)
            {
                Biome = "Mistlands", Picture = "item:$item_eitr|title:smith", Part = p => Bars(p, "$item_eitr"), Helped = "Who refined",
                Veil = new FeatVeil
                {
                    Name = "What the Mist Guards", Honours = "The mist guards something worth the dark.", Lifts = new[] { "$item_sap", "$item_softtissue" },
                    Caveat = "Each counts once, for whoever takes it first.",
                    Rule = t => "Together, bring home " + N(t) + " of what the mist guards.", Brief = t => N(t) + " from the mist",
                },
            },
            new FeatDef("flametal", "Flametal", "smith", Chapter.Feats, FeatsTogetherId, "Metal forged from the burning land.", new[] { 50.0, 200.0, 600.0 },
                t => "Together, take " + N(t) + " flametal bars from the smelters.",
                i => GroupSum(i, p => Bars(p, "$item_flametal")),
                CountedGroup, "A bar counts once, for whoever takes it first. " + BooksDiffer, exact: false, derived: true,
                brief: t => N(t) + " flametal bars", unit: "flametal bars", group: true)
            {
                Biome = "AshLands", Picture = "item:$item_flametal|title:smith", Part = p => Bars(p, "$item_flametal"), Helped = "Who smelted",
                Veil = new FeatVeil
                {
                    Name = "What the Ashes Hold", Honours = "The burning land holds something worth the heat.", Lifts = new[] { "$item_flametalore" },
                    Caveat = "Each counts once, for whoever takes it first.",
                    Rule = t => "Together, bring home " + N(t) + " of what the ashes hold.", Brief = t => N(t) + " from the ashes",
                },
            },
            // every member who shares has eaten food another member made; 2 or more members, else not yet (never "everyone": only those who share)
            new FeatDef("onehearth", "One Hearth", "cook", Chapter.Feats, FeatsTogetherId, "Everyone who shares has eaten from another's hand.", new[] { 1.0 },
                t => "Every player who shares has enjoyed food another player made.",
                i => { var m = GroupMembers(i); return m.Count >= 2 && m.All(p => DishesFromOthers(p) > 0) ? 1 : 0; },
                CountedGroup, "Every fellow player who shares, never everyone: it needs two or more who share.", exact: false, derived: true,
                brief: t => "Each fed by another", unit: "", group: true)
            { Part = DishesFromOthers, Helped = "Fed by others", Overlaps = true },
            new FeatDef("herdgrows", "The Herd Grows", "tamer", Chapter.Feats, FeatsTogetherId, "Pens full, and fuller each season.", new[] { 25.0, 100.0, 300.0 },
                t => "Together, see " + N(t) + " tamed young born.",
                i => GroupSum(i, BornHere),
                CountedGroup, "A birth counts on the PC that hosted the parents; one hosted by a PC without Hearthwoven is missed.", exact: false, derived: true,
                brief: t => N(t) + " young born", unit: "young born", group: true)
            { Part = BornHere, Helped = "Hosted by" },
        };
    }
}
