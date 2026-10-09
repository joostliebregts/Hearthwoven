using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The fictional sample the panel's tests and previews are built on (players Rowan, Edda, Finch and Tor), moved here from
    /// test-panel so the game can show it too: with Dev.SampleData on (SampleMode), the panel draws Full() instead of the
    /// real player's data, for screenshots that show every page filled without anyone's real name. Pure C#: no game or
    /// Unity calls, so the tests build exactly what the game shows (PanelUi swaps the stand-in lookups for the game's own).
    /// The pieces keep their test names: Evening (Program.Sample), Voyager, DeedsRich, Battle, Fellows (CompanyFellows).
    /// </summary>
    public static class PanelSample
    {
        static HitData.DamageTypes D(string type, float v) => DT((type, v));

        static HitData.DamageTypes DT(params (string type, float v)[] parts)
        {
            var d = new HitData.DamageTypes();
            foreach (var (t, v) in parts)
                switch (t)
                {
                    case "blunt": d.m_blunt += v; break; case "slash": d.m_slash += v; break; case "pierce": d.m_pierce += v; break;
                    case "fire": d.m_fire += v; break; case "frost": d.m_frost += v; break; case "lightning": d.m_lightning += v; break;
                    case "poison": d.m_poison += v; break; case "spirit": d.m_spirit += v; break; case "chop": d.m_chop += v; break;
                }
            return d;
        }

        // The game's own English names for the sample's tokens and prefabs (reference/item-names.json, the game's
        // localization): the stand-in for PanelUi's Localized outside the game. Voyager and Fellows add their own names.
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["$item_wood"] = "Wood", ["$item_finewood"] = "Finewood", ["$item_roundlog"] = "Corewood", ["$item_elderbark"] = "Ancient Bark",
            ["$item_stone"] = "Stone", ["$item_copperore"] = "Copper Ore", ["$item_tinore"] = "Tin Ore", ["$item_silverore"] = "Silver Ore",
            ["$item_raspberries"] = "Raspberries", ["$item_resin"] = "Resin", ["$item_bread"] = "Bread", ["$item_fishwraps"] = "Fish Wraps",
            ["$item_carrotsoup"] = "Carrot Soup", ["$item_sword_iron"] = "Iron Sword", ["$item_shield_wood"] = "Wood Shield", ["$item_axe_bronze"] = "Bronze Axe",
            ["$item_bow"] = "Crude Bow", ["$item_helmet_leather"] = "Leather Helmet", ["$item_hammer"] = "Hammer", ["$item_hoe"] = "Hoe", ["$item_cultivator"] = "Cultivator",
            ["$item_trinketbronzehealth"] = "Bronze Health Trinket",
            ["$item_arrow_wood"] = "Wood Arrow", ["$piece_woodwall"] = "Wood Wall", ["$piece_woodfloor2x2"] = "Wood Floor 2x2", ["$piece_sharpstakes"] = "Sharp Stakes",
            ["$piece_levelground"] = "Level Ground", ["$piece_raise"] = "Raise Ground", ["$piece_pavedroad"] = "Paved Road", ["$piece_sapling_carrot"] = "Carrot",
            ["$enemy_greydwarf"] = "Greydwarf", ["$enemy_troll"] = "Troll", ["$enemy_draugr"] = "Draugr", ["$enemy_eikthyr"] = "Eikthyr", ["$enemy_gdking"] = "The Elder",
            ["$enemy_bonemass"] = "Bonemass", ["$enemy_dragon"] = "Moder",
            // prefab keys (the measured records), as Localized resolves them through the prefab's token
            ["Bread"] = "Bread", ["FishWraps"] = "Fish Wraps", ["SerpentStew"] = "Serpent Stew", ["QueensJam"] = "Queen's Jam", ["Raspberry"] = "Raspberries",
            ["SwordIron"] = "Iron Sword", ["ArmorIronChest"] = "Iron Scale Mail", ["AxeBronze"] = "Bronze Axe", ["CopperOre"] = "Copper Ore",
            ["YmirRemains"] = "Ymir Flesh", ["BeltStrength"] = "Megingjord", ["rock4_copper"] = "Copper Deposit", ["woodwall"] = "Wood Wall",
            ["piece_cartographytable"] = "Cartography Table", ["BlobElite"] = "Oozer", ["Blob"] = "Blob", ["Greyling"] = "Greyling", ["Troll"] = "Troll", ["Draugr"] = "Draugr",
        };

        static string Name(string key) => key != null && Names.TryGetValue(key, out var n) ? n : null;

        /// <summary>A sample evening for Rowan (fictional players), built through the mod's own recorders (test-panel's Sample()).</summary>
        public static PanelInput Evening(DateTime now)
        {
            var log = new EventLog();
            var t0 = now.AddHours(-3.5);
            // Meadows warm-up 3.5 h ago, Black Forest 2 h ago, Swamp in the last hour
            for (int i = 0; i < 6; i++) log.AddDamage(t0.AddMinutes(i), "Meadows", true, "Greyling", "Axes", D("slash", 18));
            for (int i = 0; i < 10; i++) log.AddDamage(now.AddHours(-2).AddMinutes(i), "BlackForest", true, "Troll", "Axes", D("slash", 42));
            for (int i = 0; i < 4; i++) log.AddDamage(now.AddHours(-2).AddMinutes(i), "BlackForest", false, "Troll", "EnemyHit", D("blunt", 55));
            for (int i = 0; i < 14; i++) log.AddDamage(now.AddMinutes(-50 + i), "Swamp", true, "Draugr", "Swords", D("slash", 31));
            for (int i = 0; i < 6; i++) log.AddDamage(now.AddMinutes(-50 + i), "Swamp", false, "Draugr", "EnemyHit", D("slash", 24));
            // two falls in the Swamp to Blob poison, one in the Black Forest to a Troll
            log.AddDamage(now.AddMinutes(-40), "Swamp", false, "Blob", "EnemyHit", D("poison", 30));
            log.AddDamage(now.AddMinutes(-40).AddSeconds(4), "Swamp", false, "Blob", "Poisoned", D("poison", 22));
            log.AddDeath(now.AddMinutes(-40).AddSeconds(6), "Swamp", 10, 20);
            log.AddDamage(now.AddMinutes(-12), "Swamp", false, "BlobElite", "EnemyHit", D("poison", 45));
            log.AddDamage(now.AddMinutes(-12).AddSeconds(3), "Swamp", false, "Blob", "Poisoned", D("poison", 25));
            log.AddDeath(now.AddMinutes(-12).AddSeconds(5), "Swamp", 12, 22);
            log.AddDamage(now.AddHours(-2).AddMinutes(5), "BlackForest", false, "Troll", "EnemyHit", D("blunt", 90));
            log.AddDeath(now.AddHours(-2).AddMinutes(5).AddSeconds(1), "BlackForest", 5, 5);

            var ev = new SessionEvents { Blocks = 55, Parries = 27 };
            SessionEvents.Add(ev.AteFoodMadeBy, "Edda|Bread", 3); SessionEvents.Add(ev.AteFoodMadeBy, "Edda|FishWraps", 2);
            SessionEvents.Add(ev.AteFoodMadeBy, "Tor|SerpentStew", 1); SessionEvents.Add(ev.AteFoodMadeBy, "Rowan|QueensJam", 2);
            SessionEvents.Add(ev.AteFoodMadeBy, "unknown|Raspberry", 4);
            SessionEvents.Add(ev.EquippedGearMadeBy, "Tor|SwordIron", 1); SessionEvents.Add(ev.EquippedGearMadeBy, "Tor|ArmorIronChest", 1);
            SessionEvents.Add(ev.SailedWith, "Edda", 1260); SessionEvents.Add(ev.SailedWith, "Finch", 600);
            SessionEvents.Add(ev.SailedUnderHelmOf, "Edda", 900);
            SessionEvents.Add(ev.AteFromFeastOf, "42|FeastMeadows", 2);
            SessionEvents.Add(ev.AteFromFeastOf, "77|FeastBlackforest", 1);
            SessionEvents.Add(ev.PickaxeHits, "rock4_copper", 120); SessionEvents.Add(ev.Spent, "Haldor", 350);
            SessionEvents.Add(ev.SkillPractice, "Axes", 14.5f); SessionEvents.Add(ev.SkillPractice, "Blocking", 9.2f);
            SessionEvents.Add(ev.SkillPractice, "Run", 6.1f); SessionEvents.Add(ev.SkillPractice, "Swords", 4.4f);
            SessionEvents.Add(ev.ChopHits, "Beech1", 64); SessionEvents.Add(ev.Repairs, "woodwall", 9); SessionEvents.Add(ev.MapShared, "piece_cartographytable", 2);
            SessionEvents.Add(ev.CartMeters, "Cart", 840); SessionEvents.Add(ev.SmelterAdded, "smelter|CopperOre", 30); SessionEvents.Add(ev.SmelterAdded, "smelter|fuel", 12);
            SessionEvents.Add(ev.Bought, "Haldor|YmirRemains", 3); SessionEvents.Add(ev.Bought, "Haldor|BeltStrength", 1);

            var tally = new DamageTally();
            tally.AddDealt("Troll", "Axes", D("slash", 420)); tally.AddTaken("Blob", "EnemyHit", D("poison", 122));
            return new PanelInput
            {
                PlayerName = "Rowan", NowUtc = now, SessionStartUtc = now.AddHours(-3.6), ToLocal = t => t.AddHours(2),
                CharacterMade = now.Date.AddDays(-22), InstalledUtc = now.Date.AddDays(-6).AddHours(16),   // the zones' two dates (15 Sep, 1 Oct at the tests' clock)
                Character = new Dictionary<string, float>
                {
                    ["DistanceTraveled"] = 184200f, ["HarvestCrop"] = 312f, ["CraftFood"] = 146f, ["CraftWeapon"] = 9f, ["CraftArmor"] = 6f,
                    ["DistanceSailHelm"] = 6600f, ["Builds"] = 1204f, ["EnemyKills"] = 860f, ["BossKills"] = 3f, ["FishCaught"] = 0f,
                    ["Tree"] = 410f, ["BuildClusterDefense"] = 40f, ["TrapArmed"] = 2f, ["CraftGrill"] = 20f,
                    ["CraftTool"] = 4f, ["Upgrades"] = 11f, ["MineHits"] = 650f, ["EnemyHits"] = 5400f, ["HitsTakenEnemies"] = 1300f, ["PlayerHits"] = 3f,
                },
                // as the game books them: per craft action (a batch of arrows counts once), food from the grill +1 per piece
                // the gear per item adds up to the gear counters above, so screenshots are believable (fix2 5): weapons 2 + 6 + 1 = 9,
                // armour 4 + 2 = 6, tools 2 + 1 + 1 = 4 (DeedsRich adds the one trinket)
                ItemsCrafted = new Dictionary<string, float> { ["$item_bread"] = 20, ["$item_fishwraps"] = 28, ["$item_sword_iron"] = 2, ["$item_carrotsoup"] = 118,
                    ["$item_shield_wood"] = 4, ["$item_axe_bronze"] = 6, ["$item_bow"] = 1, ["$item_helmet_leather"] = 2, ["$item_hammer"] = 2, ["$item_hoe"] = 1, ["$item_cultivator"] = 1,
                    ["$item_arrow_wood"] = 12, ["$item_modthing"] = 3 },
                PiecesPlaced = new Dictionary<string, float> { ["$piece_woodwall"] = 520, ["$piece_woodfloor2x2"] = 310, ["$piece_sharpstakes"] = 36,
                    ["$piece_levelground"] = 850, ["$piece_raise"] = 133, ["$piece_pavedroad"] = 40, ["$piece_sapling_carrot"] = 60, ["$piece_feast_meadows"] = 2 },
                ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 2400, ["$item_finewood"] = 310, ["$item_roundlog"] = 520, ["$item_elderbark"] = 75,
                    ["$item_stone"] = 1800, ["$item_copperore"] = 240, ["$item_tinore"] = 90, ["$item_raspberries"] = 40 },
                // what PanelUi derives from the game data at runtime (item type, drop tables, piece components); null = unknown
                ItemKind = t => new Dictionary<string, string> { ["$item_bread"] = "food", ["$item_fishwraps"] = "food", ["$item_carrotsoup"] = "food", ["$item_sword_iron"] = "gear",
                    ["$item_shield_wood"] = "gear", ["$item_axe_bronze"] = "gear", ["$item_bow"] = "gear", ["$item_helmet_leather"] = "gear", ["$item_hammer"] = "gear", ["$item_hoe"] = "gear",
                    ["$item_cultivator"] = "gear", ["$item_trinketbronzehealth"] = "gear", ["$item_arrow_wood"] = "other" }.TryGetValue(t, out var k) ? k : null,
                StationDish = t => t == "$item_bread",   // the oven hands bread out; the cauldron and prep table make the rest (146 + 20 = the 166 dishes above)
                PieceKind = t => t == "$piece_raise" ? "ground" : t == "$piece_sapling_carrot" ? "planted" : t == "$piece_feast_meadows" ? "feast" : null,
                EnemyKills = new Dictionary<string, float> { ["$enemy_greydwarf"] = 410, ["$enemy_troll"] = 12, ["$enemy_draugr"] = 96, ["$enemy_eikthyr"] = 2, ["$enemy_gdking"] = 1, ["$enemy_bonemass"] = 0 },
                SkillLevels = new Dictionary<string, float> { ["Axes"] = 38.4f, ["Blocking"] = 42.7f, ["Run"] = 55f, ["Swords"] = 21f, ["Cooking"] = 18f, ["WoodCutting"] = 34f, ["Pickaxes"] = 22f, ["Fishing"] = 15f, ["Jump"] = 28f, ["Swim"] = 9f },
                SkillProgress = new Dictionary<string, float> { ["Axes"] = 0.62f, ["Blocking"] = 0.3f, ["Run"] = 0.15f, ["Swords"] = 0.4f, ["Cooking"] = 0.9f, ["WoodCutting"] = 0.55f, ["Pickaxes"] = 0.2f, ["Fishing"] = 0.7f, ["Jump"] = 0.7f, ["Swim"] = 0.8f },
                Session = tally, Events = ev, Log = log,
                PlayerNames = new Dictionary<long, string> { [42] = "Edda" },
                // what PanelUi's Localized returns in game (English), the stand-in for the game's localization
                DisplayName = Name,
            };
        }

        static readonly Dictionary<string, string> VoyageNames = new Dictionary<string, string>
        {
            ["FishingBait"] = "Fishing Bait", ["HelmetHat1"] = "Hat", ["TinOre"] = "Tin Ore", ["Wood"] = "Wood", ["BlackMetalScrap"] = "Black Metal Scrap",
            ["Coal"] = "Coal", ["smelter"] = "Smelter", ["blastfurnace"] = "Blast Furnace", ["charcoal_kiln"] = "Charcoal Kiln",
        };

        /// <summary>The sample evening plus the numbers the Voyages and Hall pages show (fictional, after the prototypes).</summary>
        public static PanelInput Voyager(PanelInput i)
        {
            foreach (var kv in new Dictionary<string, float>
            {
                ["DistanceWalk"] = 39100, ["DistanceRun"] = 9100, ["DistanceSail"] = 21500, ["DistanceAir"] = 2600, ["Jumps"] = 412,
                ["TimeInBase"] = 22200, ["TimeOutOfBase"] = 42000, ["LeviathanSink"] = 2,
                // seconds beyond the far edge, as the game books them (its x-axis names are mirrored: ExploreWest is the east)
                ["ExploreNorth"] = 840, ["ExploreSouth"] = 180, ["ExploreWest"] = 1320, ["ExploreEast"] = 540,
                ["TreasureBuriedFound"] = 4, ["TreasureDungeonFound"] = 3, ["TreasureLocationFound"] = 6, ["PortalDungeonIn"] = 9, ["PortalsUsed"] = 37,
            }) i.Character[kv.Key] = kv.Value;
            // Jump 28 and Swim 9 are in the base sample now (Skills > Overview shows what On foot shows)
            i.KnownBiomes = new List<string> { "Swamp", "Meadows", "Ocean", "BlackForest", "Mountain" };   // the game's names, in any order
            var ev = i.Events;
            SessionEvents.Add(ev.SailedWith, "Tor", 480); SessionEvents.Add(ev.SailedUnderHelmOf, "Tor", 300);
            SessionEvents.Add(ev.Spent, "Hildir", 300); SessionEvents.Add(ev.Bought, "Haldor|FishingBait", 20); SessionEvents.Add(ev.Bought, "Hildir|HelmetHat1", 1);
            SessionEvents.Add(ev.SmelterAdded, "smelter|TinOre", 20); SessionEvents.Add(ev.SmelterAdded, "charcoal_kiln|Wood", 40);
            SessionEvents.Add(ev.SmelterAdded, "blastfurnace|BlackMetalScrap", 10); SessionEvents.Add(ev.SmelterAdded, "blastfurnace|fuel", 15);
            CargoSample.Cargo(ev, "Rowan");   // 0.6: cargo carried (Voyages > Sailing)
            i.Book = CargoSample.Book("Rowan");   // 0.6: the server's book (cargo loaded and unloaded, born near)
            foreach (var kv in VoyageNames) Names[kv.Key] = kv.Value;
            return i;
        }

        /// <summary>The fellow players of the Company sample: their shared copies as PanelUi receives them (IsSelf false).</summary>
        public static List<PanelInput> Fellows(DateTime now)
        {
            PanelInput Fellow(string name, long id, Action<SessionEvents> ev, Dictionary<string, float> counters, Dictionary<string, float> picked, float dealt, params (int minutesAgo, float amount)[] fights)
            {
                var e = new SessionEvents(); ev(e);
                var tally = new DamageTally(); var d = new HitData.DamageTypes { m_slash = dealt }; if (dealt > 0) tally.AddDealt("Troll", "Swords", d);
                // their shared log of the session (what GroupShare carries as measuredLog): the fights that add up to what they dealt
                var log = new EventLog(); foreach (var f in fights) log.AddDamage(now.AddMinutes(-f.minutesAgo), "Swamp", true, "Draugr", "Swords", new HitData.DamageTypes { m_slash = f.amount });
                return new PanelInput { PlayerName = name, PlayerId = id, IsSelf = false, NowUtc = now, Events = e, Character = counters, ItemsPickedUp = picked, Session = tally, Log = log,
                                        DisplayName = Name, LastRecordedUtc = fights.Length > 0 ? now.AddMinutes(-fights.Min(f => f.minutesAgo)) : now.AddMinutes(-20), ToLocal = t => t.AddHours(2) };
            }
            foreach (var kv in new Dictionary<string, string> { ["PickaxeAntler"] = "Antler Pickaxe", ["CarrotSoup"] = "Carrot Soup", ["CookedMeat"] = "Cooked Boar Meat",
                         ["FeastMeadows"] = "Whole Roasted Meadow Boar", ["FeastBlackforest"] = "Black Forest Buffet Platter", ["ArmorLeatherChest"] = "Leather Tunic" })
                if (!Names.ContainsKey(kv.Key)) Names[kv.Key] = kv.Value;
            var fellows = new List<PanelInput>
            {
                Fellow("Edda", 77, e =>
                {
                    SessionEvents.Add(e.AteFoodMadeBy, "Rowan|QueensJam", 2); SessionEvents.Add(e.AteFoodMadeBy, "Edda|Bread", 5);   // her own bread is no gift
                    SessionEvents.Add(e.AteFoodMadeBy, "Tor|CookedMeat", 3); SessionEvents.Add(e.EquippedGearMadeBy, "Rowan|AxeBronze", 2);
                }, new Dictionary<string, float> { ["CraftFood"] = 210, ["DistanceSail"] = 12000 }, new Dictionary<string, float> { ["$item_wood"] = 1000, ["$item_stone"] = 300 }, 300, (150, 100f), (6, 200f)),
                Fellow("Tor", 88, e =>
                {
                    SessionEvents.Add(e.AteFoodMadeBy, "Rowan|CarrotSoup", 6); SessionEvents.Add(e.AteFoodMadeBy, "Rowan|QueensJam", 2);
                    SessionEvents.Add(e.AteFromFeastOf, "11|FeastMeadows", 3); SessionEvents.Add(e.EquippedGearMadeBy, "Rowan|PickaxeAntler", 1);
                }, new Dictionary<string, float> { ["CraftFood"] = 40, ["BuiltPieces"] = 90 }, new Dictionary<string, float> { ["$item_wood"] = 500 }, 0),
                Fellow("Finch", 99, e =>
                {
                    SessionEvents.Add(e.AteFoodMadeBy, "Tor|CookedMeat", 5); SessionEvents.Add(e.EquippedGearMadeBy, "Edda|ArmorLeatherChest", 1);
                    SessionEvents.Add(e.AteFoodMadeBy, "Gunn|Bread", 4);   // Gunn does not share: left out
                }, new Dictionary<string, float> { ["DistanceSail"] = 3000 }, new Dictionary<string, float> { ["$item_copperore"] = 120 }, 80, (9, 80f)),
            };
            foreach (var f in fellows) { CargoSample.Cargo(f.Events, f.PlayerName); CargoSample.Born(f.Events, f.PlayerName); LedSample.Led(f.Events, f.PlayerName); f.Book = CargoSample.Book(f.PlayerName); }   // 0.6: Edda and Tor haul, Tor's piglets
            return fellows;
        }

        public static readonly Dictionary<string, string> DeedsNames = new Dictionary<string, string>
        {
            ["Barley"] = "Barley", ["Carrot"] = "Carrot", ["Turnip"] = "Turnip", ["$piece_sapling_beech"] = "Beech Sapling",
            ["$animal_fish1"] = "Perch", ["$animal_fish2"] = "Pike", ["$animal_fish3"] = "Tuna", ["$animal_fish6"] = "Giant Herring", ["FeastMeadows"] = "Whole Roasted Meadow Boar",
        };

        // the game's item type of the sample's gear (GameData.ItemType: shared.m_itemType)
        static readonly Dictionary<string, string> GearTypes = new Dictionary<string, string>
        {
            ["$item_sword_iron"] = "OneHandedWeapon", ["$item_axe_bronze"] = "OneHandedWeapon", ["$item_bow"] = "Bow", ["$item_shield_wood"] = "Shield",
            ["$item_helmet_leather"] = "Helmet", ["$item_hammer"] = "Tool", ["$item_hoe"] = "Tool", ["$item_cultivator"] = "Tool", ["$item_trinketbronzehealth"] = "Trinket",
        };

        // the main material of the sample's gear (GameData: the recipe's ingredient that marks the tier)
        static readonly Dictionary<string, string> GearMaterials = new Dictionary<string, string>
        {
            ["$item_sword_iron"] = "Iron", ["$item_axe_bronze"] = "Bronze", ["$item_bow"] = "Wood", ["$item_shield_wood"] = "Wood",
            ["$item_helmet_leather"] = "Leather", ["$item_hammer"] = "Wood", ["$item_hoe"] = "Wood", ["$item_cultivator"] = "Wood", ["$item_trinketbronzehealth"] = "Bronze",
        };

        /// <summary>The sample evening with the counters the Deeds pages read: fish (hooked, caught, lost, quality tiers, per kind),
        /// taming, crops planted and harvested (CropOf as GameData derives it from Plant -> Pickable), gear item types.</summary>
        public static PanelInput DeedsRich(PanelInput s)
        {
            var ch = new Dictionary<string, float>(s.Character)
            {
                ["FishHooked"] = 402, ["FishCaught"] = 251, ["FishLost"] = 97, ["FishCaughtTier1"] = 104, ["FishCaughtTier2"] = 71, ["FishCaughtTier3"] = 44,
                ["FishCaughtTier4"] = 22, ["FishCaughtTier5"] = 9, ["FishCaughtTier6"] = 1, ["CreatureTamed"] = 6, ["TamedPetting"] = 41, ["TamedCommand"] = 19,
                ["HarvestBerry"] = 512, ["HarvestMushroom"] = 84, ["BeesHarvested"] = 36, ["SapHarvested"] = 22, ["CraftTrinket"] = 1,
            };
            var placed = new Dictionary<string, float>(s.PiecesPlaced) { ["$piece_sapling_barley"] = 72, ["$piece_sapling_turnip"] = 64, ["$piece_sapling_beech"] = 12 };
            CargoSample.Born(s.Events, s.PlayerName);   // 0.6: born in your care (Deeds > Taming)
            LedSample.Led(s.Events, s.PlayerName);      // 0.6: animals led (Deeds > Taming)
            var oldName = s.DisplayName; var oldKind = s.PieceKind;
            return new PanelInput
            {
                PlayerName = s.PlayerName, PlayerId = s.PlayerId, NowUtc = s.NowUtc, SessionStartUtc = s.SessionStartUtc, ToLocal = s.ToLocal, IsSelf = s.IsSelf,
                Character = ch, ItemsCrafted = new Dictionary<string, float>(s.ItemsCrafted) { ["$item_trinketbronzehealth"] = 1 }, PiecesPlaced = placed, ItemsPickedUp = s.ItemsPickedUp, EnemyKills = s.EnemyKills,
                SkillLevels = s.SkillLevels, SkillProgress = s.SkillProgress, ItemKind = s.ItemKind, StationDish = s.StationDish, GatherKind = s.GatherKind,
                PieceKind = t => t.StartsWith("$piece_sapling_") ? "planted" : oldKind(t),
                Session = s.Session, Events = s.Events, Log = s.Log, Fellows = s.Fellows, PlayerNames = s.PlayerNames, Book = s.Book ?? CargoSample.Book(s.PlayerName),   // 0.6: born near (Deeds > Taming)
                DisplayName = t => DeedsNames.TryGetValue(t, out var n) ? n : oldName(t),
                Harvested = new Dictionary<string, float> { ["Carrot"] = 41, ["Barley"] = 66, ["Turnip"] = 58, ["Flint"] = 30, ["$animal_fish1"] = 88, ["$animal_fish2"] = 61, ["$animal_fish3"] = 60, ["$animal_fish6"] = 42 },   // fish-held: the kinds add up to the 251 caught
                CropOf = t => t == "$piece_sapling_carrot" ? "Carrot" : t == "$piece_sapling_barley" ? "Barley" : t == "$piece_sapling_turnip" ? "Turnip" : null,
                ItemType = t => GearTypes.TryGetValue(t, out var type) ? type : null,
                MainMaterial = t => GearMaterials.TryGetValue(t, out var m) ? m : null,
            };
        }

        /// <summary>
        /// Deeds twins on the rich sample (Joost 2026-10-09): the game's counters as they stood when Hearthwoven first ran for
        /// this character six days ago (the baselines, every one at or below the counter now), so each Deeds page shows both zones:
        /// your character's count (the counter now) and since install (now minus baseline). Food stays at the counter now (Cooking
        /// is laid out from its own baseline: nothing of it is since install). Planted: the game's counter at first run, then 191
        /// plants counted exactly since (a PlantEasily grid of 171 barley and 20 turnips). Dates: every kind taken at the install;
        /// <paramref name="startedLater"/> = the stats and picked-plant baselines were first read after an update, two days ago.
        /// </summary>
        public static PanelInput Twins(PanelInput rich, bool startedLater = false)
        {
            var t = rich;
            var installed = t.InstalledUtc ?? t.NowUtc.Date.AddDays(-6).AddHours(16);   // as the evening: 1 Oct at the tests' clock
            t.InstalledUtc = installed; t.CharacterMade = t.CharacterMade ?? t.NowUtc.Date.AddDays(-22);
            t.Baseline = new Dictionary<string, Dictionary<string, float>>(t.Baseline ?? new Dictionary<string, Dictionary<string, float>>())
            {
                [LocalTotals.StatsKind] = new Dictionary<string, float>
                {
                    ["CraftWeapon"] = 7, ["CraftArmor"] = 4, ["CraftTool"] = 3, ["Upgrades"] = 8,
                    ["FishHooked"] = 330, ["FishCaught"] = 205, ["FishLost"] = 80,
                    ["FishCaughtTier1"] = 88, ["FishCaughtTier2"] = 60, ["FishCaughtTier3"] = 34, ["FishCaughtTier4"] = 17, ["FishCaughtTier5"] = 6,
                    ["HarvestCrop"] = 270, ["HarvestBerry"] = 470, ["HarvestMushroom"] = 70, ["BeesHarvested"] = 36, ["SapHarvested"] = 20,
                    ["TamedPetting"] = 34, ["TamedCommand"] = 16,
                },
                [LocalTotals.PickablesKind] = new Dictionary<string, float>
                {
                    ["Carrot"] = 30, ["Barley"] = 41, ["Turnip"] = 52, ["Flint"] = 30, ["$animal_fish1"] = 68, ["$animal_fish2"] = 49, ["$animal_fish3"] = 60, ["$animal_fish6"] = 28,
                },
                [LocalTotals.PlacedKind] = new Dictionary<string, float>
                {
                    ["$piece_woodwall"] = 440, ["$piece_woodfloor2x2"] = 270, ["$piece_sharpstakes"] = 30,
                    ["$piece_levelground"] = 780, ["$piece_raise"] = 120, ["$piece_pavedroad"] = 40,
                    ["$piece_sapling_barley"] = 40, ["$piece_sapling_carrot"] = 60, ["$piece_sapling_turnip"] = 50, ["$piece_sapling_beech"] = 12,
                },
                [LocalTotals.CraftedKind] = new Dictionary<string, float>
                {
                    ["$item_bread"] = 40, ["$item_fishwraps"] = 22, ["$item_carrotsoup"] = 118,
                    ["$item_sword_iron"] = 1, ["$item_shield_wood"] = 3, ["$item_axe_bronze"] = 5, ["$item_bow"] = 1, ["$item_helmet_leather"] = 1,
                    ["$item_hammer"] = 2, ["$item_hoe"] = 1, ["$item_arrow_wood"] = 12,
                },
            };
            t.ExactAtBaseline = new Dictionary<string, Dictionary<string, float>>(t.ExactAtBaseline ?? new Dictionary<string, Dictionary<string, float>>());
            foreach (var kind in new[] { LocalTotals.StatsKind, LocalTotals.PickablesKind, LocalTotals.PlacedKind, LocalTotals.CraftedKind }) t.ExactAtBaseline[kind] = new Dictionary<string, float>();
            var later = t.NowUtc.AddDays(-2);
            t.BaselineAt = new Dictionary<string, DateTime>
            {
                [LocalTotals.StatsKind] = startedLater ? later : installed, [LocalTotals.PickablesKind] = startedLater ? later : installed,
                [LocalTotals.PlacedKind] = installed, [LocalTotals.CraftedKind] = installed,
            };
            // planted exactly since install, one per plant (Hearthwoven's own count, not a baseline difference)
            var ev = SessionEvents.Sum(t.Events);
            SessionEvents.Add(ev.Planted, "$piece_sapling_barley", 171); SessionEvents.Add(ev.Planted, "$piece_sapling_turnip", 20);
            t.Events = ev;
            return t;
        }

        public static readonly Dictionary<string, string> BattleNames = new Dictionary<string, string>
        {
            ["Troll"] = "Troll", ["Greydwarf"] = "Greydwarf", ["Greydwarf_Elite"] = "Greydwarf Brute", ["Draugr"] = "Draugr", ["Boar"] = "Boar", ["Neck"] = "Neck",
            ["Leech"] = "Leech", ["Blob"] = "Blob", ["ArrowWood"] = "Wood Arrow", ["ArrowFire"] = "Fire Arrow", ["ArrowFrost"] = "Frost Arrow",
            ["ArrowNeedle"] = "Needle Arrow", ["ArrowPoison"] = "Poison Arrow",
        };

        // SAMPLE creature data (w = Weak, r = Resistant, i = Immune), the prototype's table: in game it comes from the prefabs
        static readonly Dictionary<string, (string trophy, string mods)> BattleFoes = new Dictionary<string, (string, string)>
        {
            ["Troll"] = ("TrophyFrostTroll", "pierce:w blunt:r spirit:i"), ["Greydwarf"] = ("TrophyGreydwarf", "fire:w poison:r spirit:i"),
            ["Greydwarf_Elite"] = ("TrophyGreydwarfBrute", "fire:w poison:r spirit:i"), ["Draugr"] = ("TrophyDraugr", "fire:r poison:i"),
            ["Leech"] = ("TrophyLeech", "poison:r fire:i spirit:i"), ["Blob"] = ("TrophyBlob", "blunt:w frost:w lightning:w slash:r pierce:r fire:r poison:i"),
            ["Boar"] = ("TrophyBoar", "spirit:i"), ["Neck"] = ("TrophyNeck", "fire:w poison:r spirit:i"),
        };

        public static PanelModel.FoeData SampleFoe(string prefab)
        {
            if (prefab == null || !BattleFoes.TryGetValue(prefab, out var f)) return null;
            var data = new PanelModel.FoeData { Trophy = f.trophy };
            foreach (var t in PanelModel.BattleTypes) data.Modifiers[t] = "Normal";
            foreach (var m in f.mods.Split(' ')) { var p = m.Split(':'); data.Modifiers[p[0]] = p[1] == "w" ? "Weak" : p[1] == "r" ? "Resistant" : "Immune"; }
            return data;
        }

        /// <summary>The arrow recipes the sample character knows (wood to carapace; not silver).</summary>
        public static readonly HashSet<string> SampleRecipes = new HashSet<string> { "$item_arrow_wood", "$item_arrow_fire", "$item_arrow_frost", "$item_arrow_needle", "$item_arrow_poison", "$item_arrow_carapace" };

        // SAMPLE arrows (damage close to the game's own)
        public static IList<PanelModel.ArrowData> SampleArrows()
        {
            PanelModel.ArrowData A(string prefab, string token, params (string t, float v)[] dmg)
            {
                var a = new PanelModel.ArrowData { Prefab = prefab, Token = token };
                foreach (var (t, v) in dmg) a.Damage[t] = v;
                return a;
            }
            return new List<PanelModel.ArrowData>
            {
                A("ArrowWood", "$item_arrow_wood", ("pierce", 22)), A("ArrowFire", "$item_arrow_fire", ("pierce", 11), ("fire", 22)),
                A("ArrowFrost", "$item_arrow_frost", ("pierce", 26), ("frost", 52)), A("ArrowNeedle", "$item_arrow_needle", ("pierce", 62)),
                A("ArrowPoison", "$item_arrow_poison", ("pierce", 26), ("poison", 26)), A("ArrowSilver", "$item_arrow_silver", ("pierce", 52), ("spirit", 20)),
                A("ArrowCarapace", "$item_arrow_carapace", ("pierce", 72)),
            };
        }

        /// <summary>A Battle evening close to the prototypes: melee, bow and magic across the eight types, six sources of hurt,
        /// three falls (14:31 Black Forest, 15:02 and 15:40 Swamp, local time, at the default clock), 312 blocks of which 58 parries.</summary>
        public static PanelInput Battle() => Battle(new DateTime(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc));   // 16:00 local

        public static PanelInput Battle(DateTime now)
        {
            var log = new EventLog();
            var t = now.AddHours(-2);
            void Hit(int min, string biome, string foe, string skill, params (string, float)[] d) => log.AddDamage(t.AddMinutes(min), biome, true, foe, skill, DT(d));
            void Hurt(DateTime at, string biome, string foe, params (string, float)[] d) => log.AddDamage(at, biome, false, foe, "EnemyHit", DT(d));
            Hit(0, "Meadows", "Boar", "Clubs", ("blunt", 100)); Hit(1, "Meadows", "Boar", "Bows", ("pierce", 60), ("poison", 30)); Hit(2, "Meadows", "Boar", "Knives", ("slash", 30));
            Hit(3, "Meadows", "Neck", "Axes", ("slash", 120)); Hit(4, "Meadows", "Neck", "Swords", ("fire", 30)); Hit(4, "Meadows", "Neck", "Spears", ("lightning", 40));
            Hit(10, "BlackForest", "Troll", "Clubs", ("blunt", 120)); Hit(11, "BlackForest", "Troll", "Spears", ("pierce", 330)); Hit(12, "BlackForest", "Troll", "Bows", ("pierce", 70));
            Hit(12, "BlackForest", "Troll", "Axes", ("chop", 50));   // tool damage: never in Battle
            Hit(13, "BlackForest", "Greydwarf", "Swords", ("slash", 250)); Hit(14, "BlackForest", "Greydwarf", "Bows", ("fire", 70)); Hit(15, "BlackForest", "Greydwarf", "ElementalMagic", ("fire", 60), ("frost", 30));
            Hit(15, "BlackForest", "Greydwarf", "Clubs", ("blunt", 40));
            Hit(16, "BlackForest", "Greydwarf_Elite", "Axes", ("slash", 180)); Hit(17, "BlackForest", "Greydwarf_Elite", "Swords", ("fire", 60)); Hit(17, "BlackForest", "Greydwarf_Elite", "Bows", ("pierce", 30));
            Hit(40, "Swamp", "Draugr", "Swords", ("slash", 50)); Hit(41, "Swamp", "Draugr", "ElementalMagic", ("frost", 30), ("fire", 60)); Hit(42, "Swamp", "Draugr", "Clubs", ("blunt", 180), ("frost", 50));
            Hit(43, "Swamp", "Draugr", "Bows", ("frost", 40), ("poison", 30));
            Hit(44, "Swamp", "Blob", "Clubs", ("poison", 50)); Hit(45, "Swamp", "Blob", "ElementalMagic", ("lightning", 50), ("spirit", 60)); Hit(46, "Swamp", "Leech", "Bows", ("pierce", 30));
            // received, after your armour, and the three falls
            Hurt(now.AddMinutes(-150), "Meadows", "Neck", ("slash", 14));
            Hurt(now.AddMinutes(-110), "BlackForest", "Greydwarf", ("slash", 39));
            Hurt(now.AddMinutes(-89).AddSeconds(-8), "BlackForest", "Troll", ("blunt", 90)); Hurt(now.AddMinutes(-89).AddSeconds(-2), "BlackForest", "Troll", ("blunt", 81));
            log.AddDeath(now.AddMinutes(-89), "BlackForest", 5, 5);   // 14:31
            Hurt(now.AddMinutes(-70), "Swamp", "Draugr", ("slash", 17), ("pierce", 17));
            Hurt(now.AddMinutes(-58).AddSeconds(-6), "Swamp", "Draugr", ("slash", 74)); Hurt(now.AddMinutes(-58).AddSeconds(-3), "Swamp", "Draugr", ("poison", 52));
            log.AddDeath(now.AddMinutes(-58), "Swamp", 10, 20);   // 15:02
            Hurt(now.AddMinutes(-35), "Swamp", "Blob", ("poison", 30));
            Hurt(now.AddMinutes(-20).AddSeconds(-7), "Swamp", "Leech", ("poison", 88)); Hurt(now.AddMinutes(-20).AddSeconds(-1), "Swamp", "Leech", ("pierce", 12));
            log.AddDeath(now.AddMinutes(-20), "Swamp", 12, 22);   // 15:40
            return new PanelInput
            {
                PlayerName = "Rowan", NowUtc = now, SessionStartUtc = now.AddHours(-3), ToLocal = x => x.AddHours(2), Log = log,
                SkillLevels = new Dictionary<string, float> { ["Swords"] = 21f, ["Axes"] = 38.4f, ["Clubs"] = 16f, ["Spears"] = 27f, ["Knives"] = 9f, ["Bows"] = 31f },
                SkillProgress = new Dictionary<string, float> { ["Swords"] = 0.4f, ["Axes"] = 0.62f, ["Clubs"] = 0.1f, ["Spears"] = 0.8f, ["Knives"] = 0.5f, ["Bows"] = 0.25f },
                Events = new SessionEvents { Blocks = 312, Parries = 58 }, Session = new DamageTally(),
                Character = new Dictionary<string, float> { ["EnemyKills"] = 214f, ["BuildClusterDefense"] = 12f },
                ItemsCrafted = new Dictionary<string, float> { ["$item_arrow_wood"] = 10, ["$item_arrow_fire"] = 4, ["$item_arrow_frost"] = 3, ["$item_arrow_needle"] = 2 },
                ItemsPickedUp = new Dictionary<string, float> { ["$item_arrow_poison"] = 20 },
                DisplayName = p => BattleNames.TryGetValue(p, out var n) ? n : null,
                Foe = SampleFoe, Arrows = SampleArrows,
            };
        }

        /// <summary>Damage since install as Foes and Defense read it: a log's whole evening plus two earlier evenings in the Black Forest.</summary>
        /// <summary>Damage and deaths per biome since install, as SinceInstall: the log's evening plus the two earlier evenings in the Black Forest (one fall there).</summary>
        public static BiomeTally BiomeSinceInstall(EventLog log)
        {
            var t = BiomeTally.FromLog(log);
            void Add(string k, float v) { t.Damage.TryGetValue(k, out var o); t.Damage[k] = o + v; }
            Add("BlackForest|dealt|pierce", 1400); Add("BlackForest|dealt|slash", 900); Add("BlackForest|taken|blunt", 420);
            t.Deaths.TryGetValue("BlackForest", out var fell); t.Deaths["BlackForest"] = fell + 1;   // one fall on an earlier evening: since install is more than this session
            return t;
        }

        public static DamageTally SinceInstall(EventLog log)
        {
            var since = new DamageTally();
            foreach (var kv in log.Damage)
            {
                var k = kv.Key.Split('|'); var into = k[2] == "dealt" ? since.Dealt : since.Taken; var key = k[3] + "|" + k[4] + "|" + k[5];
                into.TryGetValue(key, out var o); into[key] = o + kv.Value;
            }
            since.AddDealt("Troll", "Spears", DT(("pierce", 1400))); since.AddDealt("Greydwarf", "Swords", DT(("slash", 900))); since.AddTaken("Troll", "EnemyHit", DT(("blunt", 420)));
            return since;
        }

        /// <summary>
        /// Every chapter filled at once, for the panel in game with Dev.SampleData (screenshots): the evening, the voyager's
        /// numbers, the rich Deeds counters, the Battle evening and the three fellows who share, all at one clock. Both kinds of
        /// number are in it: your character's counts (since the character was made) and Hearthwoven's own (since install).
        /// The lookups are the tests' stand-ins; PanelUi swaps in the game's own data. Built fresh on every call.
        /// </summary>
        public static PanelInput Full(DateTime now) => SampleWorld.Build(now);

        /// <summary>
        /// The previous Full: the older samples layered by hand (Evening, Voyager, DeedsRich, Twins, Coherent, Battle, FeatsSample). It is no
        /// longer what the previews or Dev.SampleData show (SampleWorld is, one world behind every page); it stays as the fixture of the
        /// Feats tests, which assert a hand-set ledger (tiers, moments, waiting feats) the derived world does not have.
        /// </summary>
        public static PanelInput FeatsFixture(DateTime now)
        {
            var evening = Voyager(Evening(now));
            // what Hearthwoven counted exactly since install (Woodcutting and Mining), beside the character's counts
            foreach (var (item, n) in new[] { ("$item_wood", 140f), ("$item_finewood", 12f), ("$item_roundlog", 30f), ("$item_stone", 220f), ("$item_copperore", 18f), ("$item_tinore", 9f) })
                SessionEvents.Add(evening.Events.PickedUp, item, n);
            var full = Twins(DeedsRich(evening));   // the baselines six days ago: every Deeds page in both zones (PanelSample.Twins)
            full.KnownBiomes = evening.KnownBiomes;
            Coherent(full);
            BuildingSample.Hall(full);   // Rowan's hall: the pieces with their hammer tabs and materials (Deeds > Building's filter; PanelSampleBuilding.cs)
            // the Battle evening (all eight damage types, six sources of hurt, three falls) at the same clock
            var battle = Battle(now);
            full.Log = battle.Log;
            full.Events.Blocks = battle.Events.Blocks; full.Events.Parries = battle.Events.Parries;
            full.DamageSinceInstall = SinceInstall(battle.Log);
            foreach (var kv in battle.ItemsCrafted) full.ItemsCrafted[kv.Key] = Math.Max(kv.Value, full.ItemsCrafted.TryGetValue(kv.Key, out var c) ? c : 0f);
            foreach (var kv in battle.ItemsPickedUp) full.ItemsPickedUp[kv.Key] = kv.Value;
            full.Foe = SampleFoe; full.Arrows = SampleArrows; full.RecipeKnown = t => SampleRecipes.Contains(t);   // the arrows a Mistlands character knows
            var deedsName = full.DisplayName;
            full.DisplayName = t => t != null && BattleNames.TryGetValue(t, out var n) ? n : deedsName(t);
            // the Company: Edda, Tor and Finch share; feasts name Rowan by this id
            full.PlayerId = 11;
            full.Fellows = Fellows(now);
            foreach (var f in full.Fellows) f.ViewerName = full.PlayerName;
            FeatsSample.Apply(full, now);   // the sample's feats, a mix of earned and unsung (FeatsSample.cs)
            return full;
        }

        /// <summary>
        /// One story for every number the Deeds and Company pages show (fix-deeds, 2026-10-09: the reviewer failed pages on a
        /// character that said 410 trees on one page and 411 on the next). Applied on top of Twins(DeedsRich(...)):
        /// the baselines are the game's counters when Hearthwoven first ran, each at or below the counter now; trees felled
        /// 380 at install + 30 counted since = the 410 of the character; wood and ore brought in = the baseline + what Hearthwoven
        /// counted since; fish hooked = caught + got away + 14 the game books as neither; the fish by kind add up to the fish caught;
        /// the crops picked add up to the crops picked in all, with the 20 plants of no crop you planted ("other plants").
        /// test-panel/CoherenceTests.cs checks it.
        /// </summary>
        public static void Coherent(PanelInput f)
        {
            var stats = f.Baseline[LocalTotals.StatsKind]; var picks = f.Baseline[LocalTotals.PickablesKind]; var cooked = f.Baseline[LocalTotals.CraftedKind];
            // fishing: 362 hooked = 251 caught + 97 got away + 14 neither; since install 70 = 46 + 17 + 7
            f.Character["FishHooked"] = 362; stats["FishHooked"] = 292;
            // the fish by kind add up to the 251 caught (Tuna was none of the catches since install)
            f.Harvested["$animal_fish3"] = 60; picks["$animal_fish3"] = 60;
            // farming: crops picked 312 = 155 barley + 71 carrot + 66 turnip + 20 other plants (wild, a fellow's field); since install 25 + 11 + 6 = 42
            f.Harvested["Barley"] = 155; f.Harvested["Carrot"] = 71; f.Harvested["Turnip"] = 66;
            picks["Barley"] = 130; picks["Carrot"] = 60; picks["Turnip"] = 60;
            // cooking: the baseline is the counter now (nothing of the food is since install): 118 + 28 + 20 = the 166 dishes
            cooked["$item_bread"] = 20; cooked["$item_fishwraps"] = 28; cooked["$item_carrotsoup"] = 118;
            // woodcutting and mining: the game's pickup and trees-felled counters at install (faded), what Hearthwoven counted since on top
            f.Baseline["pickedUp"] = new Dictionary<string, float> { ["$item_wood"] = 1900, ["$item_finewood"] = 260, ["$item_roundlog"] = 480, ["$item_elderbark"] = 60,
                                                                    ["$item_stone"] = 1500, ["$item_copperore"] = 200, ["$item_tinore"] = 90 };
            f.Baseline[PanelModel.TreesBaseline] = new Dictionary<string, float> { ["Tree"] = 380 };
            SessionEvents.Add(f.Events.Felled, "Beech1", 17); SessionEvents.Add(f.Events.Felled, "FirTree", 9); SessionEvents.Add(f.Events.Felled, "Birch2", 4);   // 30 trees: 380 + 30 = the 410
            SessionEvents.Add(f.Events.ChopHits, "Beech1_log", 20); SessionEvents.Add(f.Events.ChopHits, "beech_log_half", 9); SessionEvents.Add(f.Events.ChopHits, "FirTree", 41);
            f.DisplayName = Chain(f.DisplayName, t => t == "$animal_fish3" ? "Tuna" : null);
        }

        static Func<string, string> Chain(Func<string, string> first, Func<string, string> then) => t => then(t) ?? first?.Invoke(t);

        /// <summary>Everyone in the sample by name: Rowan and the three fellows who share.</summary>
        public static readonly string[] Players = { "Rowan", "Edda", "Finch", "Tor" };
    }

    /// <summary>
    /// The sample as the panel in game reads it (PanelSampleUi): yourself, and your fellows kept in their own list. The panel
    /// rewires self.Fellows on every render (PanelUi.Render clears it first, then fills it from the fellows), so the fellows
    /// must never be read back from self.Fellows: that list was emptied, and the Company and the player chips came out
    /// empty in game (Joost's first snapshot run, 2026-10-08) while the tests, which build Full directly, passed.
    /// </summary>
    public sealed class SampleSource
    {
        public readonly PanelInput Self;
        public readonly IReadOnlyList<PanelInput> Fellows;

        public SampleSource(DateTime now)
        {
            Self = PanelSample.Full(now);
            Fellows = Self.Fellows.ToList().AsReadOnly();
        }

        /// <summary>The fellows' names, for the player chips (Edda, Tor, Finch).</summary>
        public IEnumerable<string> Names => Fellows.Select(f => f.PlayerName);

        /// <summary>A fellow by name, wired as PanelUi.Fellow wires a received copy; null for anyone else.</summary>
        public PanelInput Fellow(string name, PanelInput viewer)
        {
            var other = string.IsNullOrEmpty(name) ? null : Fellows.FirstOrDefault(f => f.PlayerName == name);
            if (other != null && viewer != null) { other.NowUtc = viewer.NowUtc; other.ViewerName = viewer.PlayerName; other.PlayerNames = viewer.PlayerNames; }
            return other;
        }
    }

    /// <summary>
    /// Dev.SampleData (bound by PanelUi): while on, the panel shows PanelSample.Full instead of the real player's data, and
    /// nothing is saved or shared: no LocalTotals writes, no snapshot or share state sent, no group request. What the mod
    /// measures meanwhile stays in memory, untouched; the files on disk and the server never see the sample.
    /// </summary>
    public static class SampleMode
    {
        /// <summary>Where the switch is read (PanelUi: the config entry); tests set it directly.</summary>
        public static Func<bool> Check = () => false;
        /// <summary>Log line sink (PanelUi: Debug.Log); none outside the game.</summary>
        public static Action<string> Log;
        static readonly HashSet<string> logged = new HashSet<string>();

        public static bool On { get { try { return Check != null && Check(); } catch { return false; } } }

        /// <summary>What was held back so far (one entry per kind), for the log and the tests.</summary>
        public static ICollection<string> Quieted { get { lock (logged) return logged.ToList(); } }

        /// <summary>True when sample mode is on and <paramref name="what"/> must not happen (a write or a message); says so in
        /// the log once per kind per game run.</summary>
        public static bool Quiet(string what)
        {
            if (!On) return false;
            lock (logged) if (logged.Add(what ?? "")) try { Log?.Invoke("[Hearthwoven] Dev.SampleData is on: " + what + " not saved or sent (the panel shows a fictional sample)"); } catch { }
            return true;
        }
    }
}
