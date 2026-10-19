using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9.1 feats, batch 2 (work/hearthwoven-0.9/FEAT-CATALOG-2.md; Joost: "I just want more feats in-game and adjust as we go", and of Steady at the
    /// Grill and Whole Crew: "I like those kind of, they're very unique"). Moments and streaks first, the roles deeper (the weapon styles get a family of
    /// their own, the Armsman; a crossbow, a summoner, a boss fight held on the shield), the thin families filled. Each counts only what a record holds;
    /// the four that need a new look (Through the Storm, One Long Road, Far Horizon, Saddled) are counted by FeatsHooks.SampleWorld into the ledger from
    /// the day this version first ran on the PC (LocalTotals.StartFeats091).
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>The feats the batch-2 look counts (FeatsCounters, FeatsHooks.SampleWorld): their counts began on the first load of 0.9.1's feats
        /// (LocalTotals.StartFeats091).</summary>
        static readonly HashSet<string> SinceV091 = new HashSet<string> { "throughstorm", "longroad", "farhorizon", "saddled" };

        // ---------- the records the batch reads ----------

        /// <summary>Different kinds of a record's keys with a count, kept by <paramref name="kind"/> (null: not counted).</summary>
        static double KindsOf(IDictionary<string, float> d, Func<string, string> kind) =>
            d == null ? 0 : d.Where(kv => kv.Value > 0).Select(kv => kind(kv.Key)).Where(k => !string.IsNullOrEmpty(k)).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        /// <summary>The fellow players whose name a record of yours keys first ("maker|thing", or the name alone), never you.</summary>
        static double OthersIn(PanelInput i, IDictionary<string, float> d) =>
            KindsOf(d, k => { var who = Split2(k)[0]; return who.Length == 0 || SameName(who, i?.PlayerName) ? null : who; });

        /// <summary>Mead bases you brewed, each kind once (the character's record and Hearthwoven's count since, the Mead Hall's tokens).</summary>
        static double MeadKinds(PanelInput i) =>
            (i?.ItemsCrafted ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).Select(kv => kv.Key)
                .Concat((i?.Events?.Made ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).Select(kv => kv.Key))
                .Where(IsMeadBase).Distinct(StringComparer.Ordinal).Count();

        /// <summary>Kinds of feast you set out ("crafter|feast|feast id", SetOutFeastMadeBy: the placed feast's prefab is the kind).</summary>
        static double FeastKinds(PanelInput i) => KindsOf(i?.Events?.SetOutFeastMadeBy, k => { var p = k.Split('|'); return p.Length >= 2 ? p[1] : null; });

        /// <summary>The most held blocks of yours in one boss fight that was won (BossLog: your part of each attempt).</summary>
        static double BossBlocksBest(PanelInput i) => BossesWon(i).Select(a => a.Players.Where(p => p.Self).Select(p => (double)p.Blocks).DefaultIfEmpty(0).Max()).DefaultIfEmpty(0).Max();

        /// <summary>The most players in one boss fight won: those who share by name and the others counted (BossAttempt.Unnamed).</summary>
        static double BossCrowdBest(PanelInput i) => BossesWon(i).Select(a => (double)(a.Players.Count + Math.Max(0, a.Unnamed))).DefaultIfEmpty(0).Max();

        /// <summary>The most damage you dealt with one weapon, every upgrade level of it together (the dealt ledger's weapons: "prefab|quality|skill"); bare
        /// hands and the hits the ledger could not tie to a weapon left out. Your own book only.</summary>
        static double OneWeaponBest(PanelInput i)
        {
            if (i == null || !i.IsSelf) return 0;
            var w = (i.DealtArmourSince ?? i.DealtArmourSession)?.Weapons; if (w == null) return 0;
            var per = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var kv in w.Map)
            {
                if (!WeaponKey.Split(kv.Key, out var prefab, out _, out _) || string.IsNullOrEmpty(prefab) || prefab == WeaponKey.Other || prefab == WeaponCells.Fists) continue;
                per[prefab] = (per.TryGetValue(prefab, out var s) ? s : 0) + Math.Max(0, kv.Value.All);
            }
            return per.Count == 0 ? 0 : per.Values.Max();
        }

        /// <summary>The most things you made or placed for the first time on one local day (the firsts record). Your own book only.</summary>
        static double FirstsBestDay(PanelInput i) =>
            i?.Firsts == null || !i.IsSelf ? 0 : i.Firsts.GroupBy(kv => kv.Value.Date).Select(g => (double)g.Count()).DefaultIfEmpty(0).Max();

        /// <summary>The game's own ships and the cart, by their prefab names (the repairs record keys a piece by its prefab).</summary>
        public static readonly HashSet<string> ShipPrefabs = new HashSet<string>(StringComparer.Ordinal) { "Raft", "Karve", "VikingShip", "VikingShip_Ashlands", "Cart" };
        public static readonly HashSet<string> StoneTokens = new HashSet<string>(StringComparer.Ordinal) { "$item_stone", "$item_blackmarble", "$item_grausten" };
        /// <summary>The Forsaken's powers the game counts a use of (PlayerStatType.UsePower*; the Deep North's has no power yet).</summary>
        static readonly string[] PowerStats = { "UsePowerEikthyr", "UsePowerElder", "UsePowerBonemass", "UsePowerModer", "UsePowerYagluth", "UsePowerQueen", "UsePowerAshlands" };

        /// <summary>The biggest fish you caught, in stars (PlayerStatType.FishCaughtTier0..6 by the fish's quality; a quality 1 fish has no star).</summary>
        static double FishStars(PanelInput i)
        {
            for (int q = 6; q >= 2; q--) if (C(i, "FishCaughtTier" + q) > 0) return q - 1;
            return 0;
        }

        /// <summary>Seconds you sailed under the helm of someone else (One Crew: each member's own part).</summary>
        static double UnderOthersHelm(PanelInput p) =>
            (p?.Events?.SailedUnderHelmOf ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0 && kv.Key.Length > 0 && !SameName(kv.Key, p.PlayerName)).Sum(kv => (double)kv.Value);

        static FeatDef Style(string id, string name, string honours, string skill, string what, string caveat, string picture, double[] tiers) =>
            new FeatDef(id, name, "melee", Chapter.Battle, "damage", honours, tiers,
                t => "Deal " + N(t) + " damage with " + what + ".",
                i => DealtWhere(i, s => s == skill, null),
                CountedPc, caveat, brief: t => N(t) + " with " + what, unit: "damage") { Picture = picture };

        // ---------- the batch's rows (tune here) ----------

        static IEnumerable<FeatDef> MoreFeatDefs2() => new[]
        {
            // ----- Hearth Cook -----
            new FeatDef("everydraught", "Every Draught", "cook", Chapter.Deeds, "cooking", "A cellar with a cask of every kind.", new[] { 3.0, 6.0, 10.0 },
                t => "Brew " + N(t) + " different kinds of mead base.",
                i => MeadKinds(i), CountedCharacter, "Each kind once, however often {you} brewed it.", retro: true,
                brief: t => N(t) + " kinds of mead", unit: "kind|kinds") { Picture = "item:$item_meadbasehealth|title:cook" },
            new FeatDef("everyfeast", "Feasts of Every Land", "cook", Chapter.Deeds, "cooking", "A table laid for every land.", new[] { 2.0, 4.0, 7.0 },
                t => "Set out " + N(t) + " different kinds of feast.",
                i => FeastKinds(i), CountedPc, "Each kind once. Feasts set out count from the day this PC began recording them (0.8).",
                brief: t => N(t) + " kinds of feast", unit: "kind|kinds") { Picture = "item:$item_feastmeadows|title:cook" },
            // ----- Helmskeeper -----
            new FeatDef("crewmate", "Crewmate", "sailor", Chapter.Voyages, "sailing", "A seat on many ships, and trust in many hands.", new[] { 2.0, 3.0, 5.0 },
                t => "Sail under the helm of " + N(t) + " different fellow players.",
                i => OthersIn(i, i?.Events?.SailedUnderHelmOf), CountedPc, "Anyone at the helm counts, with Hearthwoven or without.",
                brief: t => N(t) + " helms sailed under", unit: "fellow player|fellow players"),
            new FeatDef("throughstorm", "Through the Storm", "sailor", Chapter.Voyages, "sailing", "Hand on the helm while the sky broke.", new[] { 2.0, 5.0, 10.0 },
                t => "Steer through a storm for " + N(t) + " minutes in one go.",
                i => FeatCount(i, FeatsCounters.StormBest) / 60.0, CountedPc, "Looked at every 10 seconds. A storm is the game's own weather of that name.",
                brief: t => N(t) + " min in a storm", unit: "minutes", decimals: true),
            // ----- Shieldbearer -----
            new FeatDef("beforeforsaken", "Before the Forsaken", "defender", Chapter.Battle, "defense", "The shield held while the old power raged.", new[] { 10.0, 25.0, 60.0 },
                t => "Hold " + N(t) + " blows on {your} shield in one boss fight that was won.",
                i => BossBlocksBest(i), CountedPc, "From the boss fights this PC keeps, the newest 50 in each world. A tier once reached stays.",
                brief: t => N(t) + " held, one boss", unit: "blows held"),
            // ----- Battlehand -----
            new FeatDef("oldcompanion", "Old Companion", "fighter", Chapter.Battle, "damage", "One weapon, carried through it all.", new[] { 10000.0, 50000.0, 200000.0 },
                t => "Deal " + N(t) + " damage with one weapon.",
                i => OneWeaponBest(i), CountedPc, "Every upgrade of the same weapon counts as one. Bare hands are not a weapon here.",
                brief: t => N(t) + " by one weapon", unit: "damage"),
            // ----- Armsman: the weapon styles (Joost: "role specific ones") -----
            Style("axes", "Bearded Edge", "The axe that bites deep.", "Axes", "axes", "Axes and battleaxes in a fight; felling trees is not counted.", "item:$item_axe_bronze|vocab:weapon-melee", new[] { 2000.0, 10000.0, 50000.0 }),
            Style("maces", "Mace and Maul", "Blunt work, done well.", "Clubs", "maces", "Clubs, maces and sledges.", "item:$item_mace_bronze|vocab:dmg-blunt", new[] { 2000.0, 10000.0, 50000.0 }),
            Style("spears", "Spear-Hand", "Thrust and throw.", "Spears", "spears", "A thrown spear counts too.", "item:$item_spear_bronze|vocab:dmg-pierce", new[] { 2000.0, 10000.0, 50000.0 }),
            Style("polearms", "Atgeir's Sweep", "Room cleared in one wide arc.", "Polearms", "atgeirs", null, "item:$item_atgeir_bronze|vocab:weapon-melee", new[] { 2000.0, 10000.0, 50000.0 }),
            Style("knives", "Quiet Knife", "Close, quick and unseen.", "Knives", "knives", null, "item:$item_knife_copper|vocab:dmg-slash", new[] { 1000.0, 5000.0, 25000.0 }),
            Style("fists", "Bare-Knuckled", "Nothing between the fist and the foe.", "Unarmed", "fists", "Bare fists and claws.", "item:$item_fistweapon_bjorn|vocab:dmg-blunt", new[] { 1000.0, 5000.0, 25000.0 }),
            Style("swords", "Sword-Sworn", "A blade kept close and kept keen.", "Swords", "swords", null, "item:$item_sword_bronze|vocab:dmg-slash", new[] { 2000.0, 10000.0, 50000.0 }),
            // ----- Bowhand -----
            new FeatDef("heavybolt", "Heavy Bolt", "archer", Chapter.Battle, "damage", "The crank turned, the bolt flew true.", new[] { 2000.0, 10000.0, 50000.0 },
                t => "Deal " + N(t) + " damage with crossbows.",
                i => DealtWhere(i, s => s == "Crossbows", null), CountedPc, "Damage as {your} bolts carried it, before the foe's armor.",
                brief: t => N(t) + " with crossbows", unit: "damage") { Picture = "item:$item_crossbow_arbalest|vocab:dmg-pierce" },
            // ----- Staffbearer -----
            new FeatDef("raisedead", "Raised the Dead", "mage", Chapter.Battle, "damage", "Bones that answer the staff.", new[] { 10.0, 50.0, 200.0 },
                t => "Raise " + N(t) + " skeletons to fight beside {them}.",
                i => C(i, "SkeletonSummons"), CountedCharacter, "As the game counts it: each skeleton the staff calls up.", retro: true,
                brief: t => N(t) + " skeletons raised", unit: "skeleton|skeletons") { Picture = "item:$item_staffskeleton|vocab:dmg-spirit" },
            // ----- Bossbane -----
            new FeatDef("forsakengifts", "Gifts of the Forsaken", "bossbane", Chapter.Battle, FeedPage, "Each old power's gift, taken up.", new[] { 2.0, 4.0, 7.0 },
                t => "Use the powers of " + N(t) + " different Forsaken.",
                i => PowerStats.Count(s => C(i, s) > 0), CountedCharacter, "A power counts once called on, not when it is only set at a stone.", retro: true,
                brief: t => N(t) + " powers called on", unit: "power|powers") { Picture = "vocab:boss-ring" },
            new FeatDef("wholehall", "The Whole Hall", "bossbane", Chapter.Battle, FeedPage, "Every hand raised against the Forsaken.", new[] { 3.0, 5.0, 8.0 },
                t => "Win a boss fight with " + N(t) + " players in it.",
                i => BossCrowdBest(i), CountedPc, "Players who do not share are counted, never named. From the boss fights this PC keeps, the newest 50 in each world.",
                brief: t => N(t) + " in one boss fight", unit: "player|players"),
            // ----- Trailfinder -----
            new FeatDef("longroad", "One Long Road", "explorer", Chapter.Voyages, "onfoot", "Many lands between one fire and the next.", new[] { 3.0, 4.0, 5.0 },
                t => "Set foot in " + N(t) + " lands in one session.",
                i => FeatCount(i, FeatsCounters.LandsBest), CountedPc, "The land under {your} feet, looked at every 10 seconds; the sea is not a land here. A session ends when {you} leave the world.",
                brief: t => N(t) + " lands in a session", unit: "land|lands") { Picture = "vocab:compass-rose" },
            // ----- Mapmaker -----
            new FeatDef("farhorizon", "Far Horizon", "mapmaker", Chapter.Voyages, "maps", "Where the map runs thin.", new[] { 3.0, 6.0, 9.0 },
                t => "Stand " + N(t) + " km from the middle of the world.",
                i => FeatCount(i, FeatsCounters.FarBest) / 1000.0, CountedPc, "How far, never where: looked at every 10 seconds, measured from the world's middle.",
                brief: t => N(t) + " km from the middle", unit: "km", decimals: true) { Picture = "vocab:compass-home" },
            // ----- Hallwright -----
            new FeatDef("dayfirsts", "A Day of Firsts", "builder", Chapter.Deeds, "building", "Something new at every turn.", new[] { 5.0, 15.0, 30.0 },
                t => "Make or place " + N(t) + " things for the first time in one day.",
                i => FirstsBestDay(i), CountedPc, "Things {you} made or placed for the first time, from the day this PC began recording firsts.",
                brief: t => N(t) + " firsts in a day", unit: "first|firsts"),
            new FeatDef("festive", "Festive Hall", "builder", Chapter.Deeds, "building", "The hall dressed for the season.", new[] { 1.0, 5.0, 15.0 },
                t => t <= 1 ? "Have one of {your} seasonal decorations standing." : "Have " + N(t) + " of {your} seasonal decorations standing around {them} at once.",
                i => C(i, "BuildClusterSeasonal"), CountedCharacter, "As the game counts it: the most of {your} own seasonal pieces around {them} at once. They can be built only in their season.", retro: true,
                brief: t => N(t) + " seasonal pieces", unit: "seasonal piece|seasonal pieces"),
            // ----- Mender -----
            new FeatDef("shipwright", "Shipwright's Mend", "mender", Chapter.Deeds, "building", "Hull and wheel kept whole.", new[] { 5.0, 25.0, 100.0 },
                t => "Repair ships and carts " + N(t) + " times with the hammer.",
                i => (i?.Events?.Repairs ?? new Dictionary<string, float>()).Where(kv => ShipPrefabs.Contains(kv.Key)).Sum(kv => (double)kv.Value),
                CountedPc, "The game's own ships and the cart; a mod's boat is not known by name.",
                brief: t => N(t) + " hull repairs", unit: "repair|repairs") { Picture = "vocab:list-sailing" },
            // ----- Wallwarden -----
            new FeatDef("snare", "Set the Snare", "wallwarden", Chapter.Deeds, "building", "Teeth hidden in the ground.", new[] { 5.0, 25.0, 100.0 },
                t => "Arm " + N(t) + " traps.",
                i => C(i, "TrapArmed"), CountedCharacter, null, retro: true,
                brief: t => N(t) + " traps armed", unit: "trap|traps"),
            // ----- Woodcutter -----
            new FeatDef("everygrove", "Every Grove", "woodcutter", Chapter.Deeds, "woodcutting", "Beech to scorched ash, every tree knows the axe.", new[] { 3.0, 5.0, 7.0 },
                t => "Fell " + N(t) + " different kinds of tree.",
                i => KindsOf(i?.Events?.Felled, k => TreeKind(i, k)), CountedPc, "Trees {your} axe felled, each kind once.",
                brief: t => N(t) + " kinds of tree", unit: "kind|kinds") { Picture = "vocab:tree-oak" },
            // ----- Stonebreaker -----
            new FeatDef("stonewalls", "Stone for the Walls", "miner", Chapter.Deeds, "mining", "The walls begin in the quarry.", new[] { 1000.0, 5000.0, 20000.0 },
                t => "Bring in " + N(t) + " stone: stone, black marble and grausten.",
                i => BroughtInOf(i, "mining", StoneTokens), CountedCharacter, "Stone from pieces taken down never counts.", retro: true,
                brief: t => N(t) + " stone gathered", unit: "stone") { Picture = "vocab:rock-stone" },
            // ----- Beastkeeper -----
            new FeatDef("saddled", "Saddled", "tamer", Chapter.Deeds, "taming", "Riding high on a beast raised by hand.", new[] { 1.0, 5.0, 20.0 },
                t => "Ride a tamed animal " + N(t) + " km in all.",
                i => FeatCount(i, FeatsCounters.Ridden) / 1000.0, CountedPc, "Looked at every 10 seconds while {you} sit in the saddle.",
                brief: t => N(t) + " km in the saddle", unit: "km", decimals: true) { Picture = "item:$item_saddlelox|title:tamer" },
            // ----- Tidecatcher -----
            new FeatDef("fishtotell", "A Fish to Tell Of", "fisher", Chapter.Deeds, "fishing", "The one that did not get away.", new[] { 1.0, 2.0, 3.0 },
                t => t <= 1 ? "Catch a fish with a star." : "Catch a fish with " + N(t) + " stars.",
                i => FishStars(i), CountedCharacter, "Stars as the game gives a fish its size; a fish with no star counts none.", retro: true,
                brief: t => (t <= 1 ? "a fish with a star" : "a " + N(t) + "-star fish"), unit: "star|stars"),
            // ----- Waymate -----
            new FeatDef("fedbymany", "Fed by Many Hands", "helper", Chapter.Deeds, "cooking", "Fed by many, thankful to all.", new[] { 2.0, 4.0, 6.0 },
                t => "Enjoy food made by " + N(t) + " different fellow players.",
                i => OthersIn(i, i?.Events?.AteFoodMadeBy), CountedPc, "A dish carries its maker's name as the game writes it.",
                brief: t => N(t) + " cooks fed {them}", unit: "fellow player|fellow players") { Picture = "vocab:list-food-shared" },
            new FeatDef("wornwork", "Dressed by the Hall", "helper", Chapter.Deeds, "crafting", "Armed by a fellow's hand.", new[] { 2.0, 3.0, 5.0 },
                t => "Put on gear made by " + N(t) + " different fellow players.",
                i => OthersIn(i, i?.Events?.EquippedGearMadeBy), CountedPc, "It counts putting gear on, as Arms for the Hall does.",
                brief: t => N(t) + " makers worn", unit: "fellow player|fellow players") { Picture = "vocab:list-gear-shared" },
        };

        // ---------- the group's (Together) ----------

        static IEnumerable<FeatDef> MoreGroupFeatDefs2() => new[]
        {
            // every member who shares has sailed under another's helm; 2 or more members (as One Hearth: never "everyone", only those who share)
            new FeatDef("onecrew", "One Crew", "sailor", Chapter.Feats, FeatsTogetherId, "Everyone who shares has sailed under another's hand.", new[] { 1.0 },
                t => "Every player who shares has sailed under another player's helm.",
                i => { var m = GroupMembers(i); return m.Count >= 2 && m.All(p => UnderOthersHelm(p) > 0) ? 1 : 0; },
                CountedGroup, "Every fellow player who shares, never everyone: it needs two or more who share.", exact: false, derived: true,
                brief: t => "Each sailed as crew", unit: "", group: true)
            { Part = p => Math.Floor(UnderOthersHelm(p) / 60.0), Helped = "Minutes under another's helm", Overlaps = true, Picture = "vocab:list-sailing" },   // minutes: most stints are short
        };
    }
}
