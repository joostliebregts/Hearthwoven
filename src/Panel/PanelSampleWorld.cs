using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The fictional sample as ONE world (sample-one, 2026-10-09): Rowan, Edda, Finch and Tor, a group of friends a few weeks into a
    /// Valheim world, six days into Hearthwoven. Every chapter and every fellow's book reads from it (PanelSample.Full), so the
    /// pages never contradict each other: the reviewers failed pages for "Ore Road earned on the Hall but not on the Feats page",
    /// "Kept the Fires 1 377 beside 127 put in", "Rowan 0 % sailed beside voyages". test-panel/CoherenceTests.cs checks every identity.
    ///
    /// Who is known for what (no ranking: each is simply the one who does it):
    ///   Rowan  the cook and builder: 166 dishes, the feasts, the hall and the smelters (he smelts what Edda hauls), the farm, a pen of young animals.
    ///   Edda   the hauler and sailor: the iron run by ship (Ore Road, Heavy Keel), the helm of the group's ships (Ferryman).
    ///   Finch  the scout: the world's edge, treasure and portals, 150 km on foot, a boar and a wolf following behind (Drover, Long Lead, Waymate).
    ///   Tor    the tank and parrier: Shield Wall, Stood Fast, Unbroken, Turned Blades, Turned the Forsaken; he grills for everyone (Full Table).
    ///
    /// How it is made, so it cannot drift:
    ///   - Voyages (six, since install) are one list; every km, every minute aboard and every minute under a helm is worked out from it.
    ///   - Meals, feasts and gear that someone ate or put on are one list each; the eater's record and the maker's page both read it.
    ///   - Fights are lists per player: the session's log, the damage since install (the session plus earlier evenings) and the biome totals
    ///     all come from the same rows, so a session can never exceed since install and the windows nest.
    ///   - Feats are NEVER set by hand: the counters are set, and the ledger is what PanelModel.FeatDefs make of them (Derive); only the
    ///     moment (the day and the biome) is narrative.
    ///   - A fellow's book is their own record sent through Snapshot.Build and read back by PanelInput.FromSnapshot, as the game does it, so
    ///     it holds exactly what a shared copy holds and nothing else.
    /// Pure C#, no game calls beyond the data types. Built fresh on every call.
    /// </summary>
    public static class SampleWorld
    {
        public const string Rowan = "Rowan", Edda = "Edda", Finch = "Finch", Tor = "Tor";
        public const long RowanId = 11, EddaId = 77, TorId = 88, FinchId = 99;
        public static readonly string[] Everyone = { Rowan, Edda, Finch, Tor };

        // ---------- the clock ----------

        /// <summary>Hearthwoven was first run on this PC six days ago in the evening; the character was made 22 days ago.</summary>
        public static DateTime InstalledAt(DateTime now) => now.Date.AddDays(-6).AddHours(16);
        public static DateTime CharacterMadeAt(DateTime now) => now.Date.AddDays(-22);
        public static DateTime SessionStart(DateTime now) => now.AddHours(-3.6);

        // ---------- the voyages since install (the one list behind every sea number) ----------

        public sealed class Voyage
        {
            public readonly string Helm; public readonly string[] Aboard; public readonly int Minutes;
            public Voyage(string helm, int minutes, params string[] others) { Helm = helm; Minutes = minutes; Aboard = new[] { helm }.Concat(others).ToArray(); }
            public double Km => Minutes * KmPerMinute;
        }
        /// <summary>The group's ships average 12 km an hour (a Karve under a fair wind), the straight line the game counts.</summary>
        public const double KmPerMinute = 0.2;

        public static readonly Voyage[] Voyages =
        {
            new Voyage(Edda, 30, Rowan, Finch, Tor),   // to the Swamp islands, the whole group
            new Voyage(Edda, 21, Rowan),               // a second trip with Rowan for stone
            new Voyage(Edda, 25),                      // the iron run, alone
            new Voyage(Rowan, 10, Edda, Tor),          // Rowan takes the helm for the way home
            new Voyage(Finch, 14, Tor),                // Finch scouting the coast, Tor along
            new Voyage(Tor, 5, Rowan),                 // Tor brings the ship in
        };

        /// <summary>Km sailed since install by a player (aboard any voyage).</summary>
        public static double SailedSince(string who) => Voyages.Where(v => v.Aboard.Contains(who)).Sum(v => v.Km);
        public static double HelmSince(string who) => Voyages.Where(v => v.Helm == who).Sum(v => v.Km);
        /// <summary>Seconds a player was aboard the same ship as another.</summary>
        public static float WithSeconds(string who, string other) => Voyages.Where(v => v.Aboard.Contains(who) && v.Aboard.Contains(other)).Sum(v => v.Minutes) * 60f;
        /// <summary>Seconds a player was aboard while another held the helm.</summary>
        public static float UnderSeconds(string who, string helm) => Voyages.Where(v => v.Helm == helm && v.Aboard.Contains(who) && who != helm).Sum(v => v.Minutes) * 60f;

        // the character's sail counters before Hearthwoven (km): sailed in all, of which at the helm
        static readonly Dictionary<string, (double sail, double helm)> SailBefore = new Dictionary<string, (double, double)>
        {
            [Rowan] = (8.3, 4.6), [Edda] = (24.3, 17.8), [Finch] = (10.8, 4.1), [Tor] = (4.6, 0.8),
        };

        // ---------- names the pages need that the older samples did not carry ----------

        static readonly Dictionary<string, string> MoreNames = new Dictionary<string, string>
        {
            ["IronScrap"] = "Iron Scrap", ["$item_ironscrap"] = "Iron Scrap", ["$item_coal"] = "Coal", ["Coal"] = "Coal",
            ["ShieldWood"] = "Wood Shield", ["Bow"] = "Crude Bow", ["HelmetLeather"] = "Leather Helmet", ["ArmorLeatherChest"] = "Leather Tunic",
            ["CookedMeat"] = "Cooked Boar Meat", ["CarrotSoup"] = "Carrot Soup", ["SwordIron"] = "Iron Sword", ["ArmorIronChest"] = "Iron Scale Mail", ["AxeBronze"] = "Bronze Axe", ["$item_cookedmeat"] = "Cooked Boar Meat", ["$item_boarjerky"] = "Boar Jerky",
            ["$item_queensjam"] = "Queen's Jam", ["$item_sausages"] = "Sausages", ["$item_turnipstew"] = "Turnip Stew", ["$item_mod_spicedcider"] = "Spiced Cider",
            ["FeastMeadows"] = "Whole Roasted Meadow Boar", ["FeastBlackforest"] = "Black Forest Buffet Platter",
            ["$piece_feast_meadows"] = "Whole Roasted Meadow Boar", ["$piece_feast_blackforest"] = "Black Forest Buffet Platter",
            ["$item_chest_iron"] = "Iron Scale Mail", ["$item_helmet_iron"] = "Iron Helmet", ["$item_shield_banded"] = "Banded Shield",
            ["$item_pickaxe_antler"] = "Antler Pickaxe", ["PickaxeAntler"] = "Antler Pickaxe", ["$item_chest_leather"] = "Leather Tunic",
            ["$item_arrow_fire"] = "Fire Arrow", ["$item_arrow_frost"] = "Frost Arrow", ["$item_arrow_needle"] = "Needle Arrow", ["$item_arrow_poison"] = "Poison Arrow",
            ["$item_boar_piggy"] = "Piglet", ["Boar_piggy"] = "Piglet", ["Wolf_cub"] = "Wolf Cub", ["Lox_Calf"] = "Lox Calf", ["Chicken"] = "Chicken",
            ["Wolf"] = "Wolf", ["Boar"] = "Boar", ["Lox"] = "Lox", ["$item_wood"] = "Wood", ["$item_stone"] = "Stone",
            ["$piece_sapling_barley"] = "Barley", ["$piece_sapling_turnip"] = "Turnip", ["$piece_sapling_carrot"] = "Carrot", ["$animal_fish3"] = "Tuna",
        };

        static void Register()
        {
            foreach (var kv in PanelSample.DeedsNames) if (!PanelSample.Names.ContainsKey(kv.Key)) PanelSample.Names[kv.Key] = kv.Value;
            foreach (var kv in MoreNames) if (!PanelSample.Names.ContainsKey(kv.Key)) PanelSample.Names[kv.Key] = kv.Value;
        }

        // what the game's data says about the items and pieces of the world (PanelUi derives these in game; the same for everyone)
        static readonly Dictionary<string, string> Kinds = new Dictionary<string, string>
        {
            ["$item_bread"] = "food", ["$item_fishwraps"] = "food", ["$item_carrotsoup"] = "food", ["$item_cookedmeat"] = "food", ["$item_boarjerky"] = "food",
            ["$item_queensjam"] = "food", ["$item_sausages"] = "food", ["$item_turnipstew"] = "food", ["$item_mod_spicedcider"] = "food",
            ["$item_sword_iron"] = "gear", ["$item_shield_wood"] = "gear", ["$item_axe_bronze"] = "gear", ["$item_bow"] = "gear", ["$item_helmet_leather"] = "gear",
            ["$item_hammer"] = "gear", ["$item_hoe"] = "gear", ["$item_cultivator"] = "gear", ["$item_trinketbronzehealth"] = "gear", ["$item_chest_iron"] = "gear",
            ["$item_helmet_iron"] = "gear", ["$item_shield_banded"] = "gear", ["$item_pickaxe_antler"] = "gear", ["$item_chest_leather"] = "gear",
        };
        static readonly Dictionary<string, string> Types = new Dictionary<string, string>
        {
            ["$item_sword_iron"] = "OneHandedWeapon", ["$item_axe_bronze"] = "OneHandedWeapon", ["$item_bow"] = "Bow", ["$item_shield_wood"] = "Shield", ["$item_shield_banded"] = "Shield",
            ["$item_helmet_leather"] = "Helmet", ["$item_helmet_iron"] = "Helmet", ["$item_chest_iron"] = "Chest", ["$item_chest_leather"] = "Chest",
            ["$item_hammer"] = "Tool", ["$item_hoe"] = "Tool", ["$item_cultivator"] = "Tool", ["$item_pickaxe_antler"] = "Tool", ["$item_trinketbronzehealth"] = "Trinket",
        };
        static readonly Dictionary<string, string> Materials = new Dictionary<string, string>
        {
            ["$item_sword_iron"] = "Iron", ["$item_axe_bronze"] = "Bronze", ["$item_bow"] = "Wood", ["$item_shield_wood"] = "Wood", ["$item_shield_banded"] = "Iron",
            ["$item_helmet_leather"] = "Leather", ["$item_helmet_iron"] = "Iron", ["$item_chest_iron"] = "Iron", ["$item_chest_leather"] = "Leather",
            ["$item_hammer"] = "Wood", ["$item_hoe"] = "Wood", ["$item_cultivator"] = "Wood", ["$item_pickaxe_antler"] = "Wood", ["$item_trinketbronzehealth"] = "Bronze",
        };

        // Cooking's filter: each dish's type and biggest food value, as GameData reads them (the game's own food values: carrot soup 15 health
        // 45 stamina, boar jerky 23 and 23). The spiced cider is a mod's brew: no food value, made at no station the game data knows, so
        // the page puts it under Other on both rows
        static readonly Dictionary<string, (string type, string boost)> Dishes = new Dictionary<string, (string, string)>
        {
            ["$item_carrotsoup"] = ("meal", "stamina"), ["$item_fishwraps"] = ("meal", "health"), ["$item_queensjam"] = ("meal", "stamina"), ["$item_sausages"] = ("meal", "health"),
            ["$item_turnipstew"] = ("meal", "stamina"), ["$item_boarjerky"] = ("meal", "balanced"), ["$item_bread"] = ("baked", "stamina"), ["$item_cookedmeat"] = ("grilled", "health"),
        };

        /// <summary>The game's lookups, set on every book (a fellow's copy gets them from PanelUi in game, the same ones).</summary>
        static void Lookups(PanelInput p, bool self)
        {
            p.DisplayName = t => t != null && PanelSample.Names.TryGetValue(t, out var n) ? n : PanelSample.BattleNames.TryGetValue(t ?? "", out var b) ? b : null;
            p.ItemKind = t => t != null && Kinds.TryGetValue(t, out var k) ? k : (t != null && t.StartsWith("$item_arrow") ? "other" : null);
            p.StationDish = t => t == "$item_bread" || t == "$item_cookedmeat";   // the oven and the grill hand these out; the cauldron and prep table make the rest
            p.PieceKind = PanelModel.PieceKindByName;
            // what the game data says of the pickaxe drops that are neither stone nor ore (GameData: muddy scrap piles, the Leviathan);
            // Leather Scraps, which boars drop too, it leaves out (PanelModel.GatheredItemKind)
            p.GatherKind = t => t == "$item_witheredbone" || t == "$item_chitin" ? PanelModel.PickaxeFinds : null;
            p.CropOf = t => t == "$piece_sapling_carrot" ? "Carrot" : t == "$piece_sapling_barley" ? "Barley" : t == "$piece_sapling_turnip" ? "Turnip" : null;
            p.ItemType = t => t != null && Types.TryGetValue(t, out var k) ? k : null;
            p.MainMaterial = t => t != null && Materials.TryGetValue(t, out var k) ? k : null;
            p.DishType = t => t != null && Dishes.TryGetValue(t, out var d) ? d.type : null;
            p.DishBoost = t => t != null && Dishes.TryGetValue(t, out var d) ? d.boost : null;
            p.Foe = PanelSample.SampleFoe; p.Arrows = PanelSample.SampleArrows;
            p.RecipeKnown = self ? (Func<string, bool>)(t => PanelSample.SampleRecipes.Contains(t)) : null;
            p.ToLocal = t => t.AddHours(2);
        }

        // ---------- small builders ----------

        static HitData.DamageTypes DT(params (string type, float v)[] parts)
        {
            var d = new HitData.DamageTypes();
            foreach (var (t, v) in parts)
                switch (t)
                {
                    case "blunt": d.m_blunt += v; break; case "slash": d.m_slash += v; break; case "pierce": d.m_pierce += v; break;
                    case "fire": d.m_fire += v; break; case "frost": d.m_frost += v; break; case "lightning": d.m_lightning += v; break;
                    case "poison": d.m_poison += v; break; case "spirit": d.m_spirit += v; break;
                }
            return d;
        }

        static void Add(Dictionary<string, float> d, string key, float v) => SessionEvents.Add(d, key, v);

        /// <summary>The damage tally of a log (what the session's tally holds: the same rows folded to foe, skill and type).</summary>
        public static DamageTally TallyOf(EventLog log)
        {
            var t = new DamageTally();
            foreach (var kv in log.Damage)
            {
                var k = kv.Key.Split('|');   // time|biome|dir|foe|cause|type
                var into = k[2] == "dealt" ? t.Dealt : t.Taken; var key = k[3] + "|" + k[4] + "|" + k[5];
                into.TryGetValue(key, out var o); into[key] = o + kv.Value;
            }
            foreach (var kv in log.Hits) if (kv.Key.Split('|')[2] == "dealt") t.HitsDealt += (int)kv.Value; else t.HitsTaken += (int)kv.Value;
            return t;
        }

        /// <summary>An earlier evening, before this session: folded into the damage since install and the biome totals, never into the log.</summary>
        static void Earlier(DamageTally since, BiomeTally bio, string biome, bool dealt, string foe, string skill, params (string type, float v)[] parts)
        {
            if (dealt) since.AddDealt(foe, skill, DT(parts)); else since.AddTaken(foe, skill, DT(parts));
            foreach (var (t, v) in parts) bio.AddDamage(biome, dealt, t, v);
        }

        // ---------- the world ----------

        /// <summary>The four books at one clock: Rowan's own, with Edda, Finch and Tor as the copies Rowan receives (fellows wired as PanelUi wires them).</summary>
        public static PanelInput Build(DateTime now) => Make(now).owns[Rowan];

        /// <summary>
        /// Everyone's own record (IsSelf, with their counters and ledger: what their PC shows them) and the copies the others receive (the snapshot
        /// read back). Each own book has the other three as copies in its Fellows; Rowan's are Edda, Tor and Finch in the group's order.
        /// </summary>
        public static (Dictionary<string, PanelInput> owns, Dictionary<string, PanelInput> copies) Make(DateTime now)
        {
            Register();
            var owns = new Dictionary<string, PanelInput>
            {
                [Rowan] = RowanOwn(now), [Edda] = EddaOwn(now), [Finch] = FinchOwn(now), [Tor] = TorOwn(now),
            };
            // the feats come from the counters (and from what the others shared), so every book is first sent without them
            Dictionary<string, PanelInput> Copies() => owns.ToDictionary(kv => kv.Key, kv => Share(kv.Value, now));
            var copies = Copies();
            foreach (var kv in owns) kv.Value.Fellows = Everyone.Where(k => k != kv.Key).Select(k => copies[k]).ToList();
            foreach (var kv in owns) History(kv.Value, now);   // the day history (HISTORY-06.md), worked out from the same lists as the totals
            foreach (var kv in owns) Derive(kv.Value, now);
            foreach (var kv in owns) kv.Value.Feats.Seen = kv.Key == Rowan ? Math.Max(0, kv.Value.Feats.TiersEarned - 2) : kv.Value.Feats.TiersEarned;   // two earned tiers Rowan has not opened yet: the gold dots
            copies = Copies();   // now with the feats they earned
            foreach (var kv in owns)
            {
                var others = kv.Key == Rowan ? new[] { Edda, Tor, Finch } : Everyone.Where(k => k != kv.Key).ToArray();   // Edda, Tor and Finch, as the group's list reads
                kv.Value.Fellows = others.Select(n => copies[n]).ToList();
            }
            foreach (var f in copies.Values) { f.ViewerName = Rowan; f.PlayerNames = owns[Rowan].PlayerNames; }
            return (owns, copies);
        }

        /// <summary>A player's own record as a fellow receives it: through the snapshot the game sends and the reader it is read back by.</summary>
        public static PanelInput Share(PanelInput own, DateTime now)
        {
            var stats = new PlayerProfile.PlayerStats[1]; var s = new PlayerProfile.PlayerStats(); stats[0] = s;
            foreach (var kv in own.Character) s[(PlayerStatType)Enum.Parse(typeof(PlayerStatType), kv.Key)] = kv.Value;
            foreach (var kv in own.ItemsCrafted) s.m_itemCraftStats[kv.Key] = kv.Value;
            foreach (var kv in own.ItemsPickedUp) s.m_itemPickupStats[kv.Key] = kv.Value;
            foreach (var kv in own.PiecesPlaced) s.m_piecesPlacedStats[kv.Key] = kv.Value;
            foreach (var kv in own.Harvested ?? new Dictionary<string, float>()) s.m_pickableStats[kv.Key] = kv.Value;
            if (s.m_enemyStats.Length > 0)
            {
                s.m_enemyStats[0] = s.m_enemyStats[0] ?? new Dictionary<string, float>();
                foreach (var kv in own.EnemyKills ?? new Dictionary<string, float>()) s.m_enemyStats[0][kv.Key] = kv.Value;
            }
            var skills = own.SkillLevels.Select(kv => new Snapshot.SkillInfo
            {
                Name = kv.Key, Level = kv.Value,
                Accumulator = (float)(own.SkillProgress[kv.Key] * (Math.Pow(Math.Floor(kv.Value) + 1, 1.5) * 0.5 + 0.5)),
            }).ToList();
            var json = Snapshot.Build("0.6.0", own.PlayerId, own.PlayerName, stats, skills, "Midgard", own.Session, "s1", own.Events, own.Log, true,
                                      own.Events, own.DamageSinceInstall, own.BiomeSinceInstall, own.BiomeFromUtc, own.Feats,
                                      own.History?.DealtByDay(PanelModel.LocalToday(own), 30));   // their damage dealt per day, as the game sends it
            var copy = PanelInput.FromSnapshot(GroupShare.SharedCopy(json));
            copy.NowUtc = now; copy.ViewerName = Rowan; copy.PlayerNames = own.PlayerNames;
            copy.Book = own.Book;   // the server's book for them comes with the group list, not through their snapshot (GroupShare.BookOf)
            Lookups(copy, false);
            return copy;
        }

        static PanelInput Base(string name, long id, DateTime now)
        {
            var p = new PanelInput
            {
                PlayerName = name, PlayerId = id, NowUtc = now, SessionStartUtc = SessionStart(now), IsSelf = true,
                PlayerNames = new Dictionary<long, string> { [RowanId] = Rowan, [EddaId] = Edda, [FinchId] = Finch, [TorId] = Tor }.Where(kv => kv.Value != name).ToDictionary(kv => kv.Key, kv => kv.Value),
                InstalledUtc = InstalledAt(now), CharacterMade = CharacterMadeAt(now),
                Events = new SessionEvents(), Log = new EventLog(), Session = new DamageTally(),
                Harvested = new Dictionary<string, float>(), EnemyKills = new Dictionary<string, float>(),
                SkillLevels = new Dictionary<string, float>(), SkillProgress = new Dictionary<string, float>(),
                PiecesPlaced = new Dictionary<string, float>(), ItemsCrafted = new Dictionary<string, float>(), ItemsPickedUp = new Dictionary<string, float>(),
                Feats = new FeatsLedger { Primed = true },
            };
            Lookups(p, true);
            p.Book = ServerBookOf(name, now);   // what the server's book says about this player (loaded and unloaded cargo, born near)
            return p;
        }

        /// <summary>
        /// The server's book for one player (0.6, srv-06): cargo they loaded onto a ship or cart (sent) and unloaded (delivered), in item-metres per item, and the tamed
        /// young born near them. Consistent with the world's own tallies: nothing is sent or delivered that no one in the group carried (the sums per item stay under the
        /// cargo the four of them hauled, CoherenceWorld), Rowan loads the iron Edda hauls and unloads, Finch is not on the server's book (no cargo, no births near).
        /// </summary>
        public static ServerBook.Shared ServerBookOf(string name, DateTime now)
        {
            var b = new ServerBook.Shared { CargoFrom = InstalledAt(now).AddDays(1), BornFrom = InstalledAt(now).AddDays(2) };
            switch (name)
            {
                case Rowan: b.Sent["$item_ironscrap"] = 1430000; b.Sent["$item_wood"] = 4000; b.Delivered["$item_ironscrap"] = 84000; b.Delivered["$item_wood"] = 49000; b.BornNear["Boar_piggy"] = 4; b.BornNear["Wolf_cub"] = 1; break;
                case Edda: b.Sent["$item_ironscrap"] = 1900000; b.Delivered["$item_ironscrap"] = 1425000; break;
                case Tor: b.Sent["$item_ironscrap"] = 214000; b.Delivered["$item_ironscrap"] = 442000; b.BornNear["Boar_piggy"] = 3; break;
                default: return null;
            }
            return b;
        }

        /// <summary>The voyages, worked out for one player: the seconds with and under, the cargo hauled is theirs to set.</summary>
        static void Sea(PanelInput p, Dictionary<string, float> character)
        {
            var name = p.PlayerName; var before = SailBefore[name];
            character["DistanceSail"] = (float)((before.sail + SailedSince(name)) * 1000);
            character["DistanceSailHelm"] = (float)((before.helm + HelmSince(name)) * 1000);
            foreach (var other in Everyone.Where(o => o != name))
            {
                var with = WithSeconds(name, other); if (with > 0) Add(p.Events.SailedWith, other, with);
                var under = UnderSeconds(name, other); if (under > 0) Add(p.Events.SailedUnderHelmOf, other, under);
            }
        }

        // ---------- the day history (HISTORY-06.md) ----------

        /// <summary>The play days since install, as days before today (local): the install evening (7 days back), three evenings, the session's
        /// evening (yesterday) and the half hour after midnight it ran into. Each share of what Hearthwoven counted since install, in order.</summary>
        static readonly (int day, double share)[] PlayDays = { (-7, 0.10), (-6, 0.20), (-4, 0.15), (-2, 0.15), (-1, 0.30), (0, 0.10) };
        /// <summary>The day of each voyage (Voyages, in order), as days before today.</summary>
        static readonly int[] VoyageDays = { -6, -4, -4, -1, -2, 0 };
        /// <summary>The earlier evenings' fights (since install minus this session) were on this day.</summary>
        const int EarlierFights = -4;

        /// <summary>
        /// A book's day history, worked out from the very lists its totals come from, so every window nests (Today inside 7 days inside All) and the
        /// rows add up to since install exactly (the history began with the install here): the session's log hit by hit on the local day of its
        /// bucket, the earlier evenings' fights on one day, every voyage on its day (km, minutes aboard, minutes under a helm), deaths per biome
        /// by day with the game's death counter beside them, and the other counts split over the play days by fixed shares (whole numbers; what
        /// does not divide goes to the install evening). The game's counters without a baseline grew by a twelfth since install.
        /// </summary>
        static void History(PanelInput p, DateTime now)
        {
            Func<DateTime, DateTime> local = t => p.ToLocal != null ? p.ToLocal(t) : t.ToLocalTime();
            var today = local(now).Date;
            var h = new DayHistory { From = local(InstalledAt(now)).Date };
            var rows = new SortedDictionary<DateTime, DayHistory.Row>();
            DayHistory.Row Row(DateTime day) { day = day.Date; if (!rows.TryGetValue(day, out var r)) rows[day] = r = new DayHistory.Row { Period = DayHistory.DayKey(day) }; return r; }
            DayHistory.Row Ago(int d) => Row(today.AddDays(d));

            // the session's log, per local day of each bucket (the session ran over midnight)
            var sessionBiome = new BiomeTally(); var sessionTally = new DamageTally();
            foreach (var kv in p.Log.Damage)
            {
                var k = kv.Key.Split('|');   // time|biome|dir|foe|cause|type
                if (!DateTime.TryParseExact(k[0], "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
                var r = Row(local(t)); var dealt = k[2] == "dealt"; var key = k[3] + "|" + k[4] + "|" + k[5];
                Add(dealt ? r.Damage.Dealt : r.Damage.Taken, key, kv.Value); Add(dealt ? sessionTally.Dealt : sessionTally.Taken, key, kv.Value);
                r.Biome.AddDamage(k[1], dealt, k[5], kv.Value); sessionBiome.AddDamage(k[1], dealt, k[5], kv.Value);
            }
            foreach (var kv in p.Log.Hits)
            {
                var k = kv.Key.Split('|');
                if (!DateTime.TryParseExact(k[0], "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
                var r = Row(local(t));
                if (k[2] == "dealt") { r.Damage.HitsDealt += (int)kv.Value; sessionTally.HitsDealt += (int)kv.Value; } else { r.Damage.HitsTaken += (int)kv.Value; sessionTally.HitsTaken += (int)kv.Value; }
            }
            foreach (var d in p.Log.Deaths) { Row(local(d.Time)).Biome.AddDeath(d.Biome); sessionBiome.AddDeath(d.Biome); }
            // the earlier evenings: what since install holds beyond the session
            if (p.DamageSinceInstall != null) Ago(EarlierFights).Damage.AddAll(DamageTally.Minus(p.DamageSinceInstall, sessionTally));
            if (p.BiomeSinceInstall != null) Ago(EarlierFights).Biome.AddAll(BiomeTally.Minus(p.BiomeSinceInstall, sessionBiome));

            // the voyages, each on its day
            var name = p.PlayerName;
            for (int i = 0; i < Voyages.Length; i++)
            {
                var v = Voyages[i]; if (!v.Aboard.Contains(name)) continue;
                var r = Ago(VoyageDays[i]);
                Add(r.Game, "DistanceSail", (float)(v.Km * 1000)); if (v.Helm == name) Add(r.Game, "DistanceSailHelm", (float)(v.Km * 1000));
                foreach (var other in v.Aboard.Where(o => o != name)) Add(r.Events.SailedWith, other, v.Minutes * 60f);
                if (v.Helm != name) Add(r.Events.SailedUnderHelmOf, v.Helm, v.Minutes * 60f);
            }

            // everything else Hearthwoven counted since install, split over the play days
            void Split(Func<SessionEvents, Dictionary<string, float>> pick)
            {
                foreach (var kv in pick(p.Events).ToList())
                {
                    double left = kv.Value;
                    foreach (var (day, share) in PlayDays.Skip(1)) { var part = Math.Floor(kv.Value * share); if (part > 0) { Add(pick(Ago(day).Events), kv.Key, (float)part); left -= part; } }
                    if (left > 0) Add(pick(Ago(PlayDays[0].day).Events), kv.Key, (float)left);
                }
            }
            var skip = new HashSet<string> { "sailedWith", "sailedUnderHelmOf", "battle" };
            var named = p.Events.Named().Select(n => n.Key).ToList();
            for (int m = 0; m < named.Count; m++) { if (skip.Contains(named[m])) continue; var idx = m; Split(e => e.Named().ElementAt(idx).Value); }
            int SplitInt(int total, Action<DayHistory.Row, int> into)
            {
                var left = total;
                foreach (var (day, share) in PlayDays.Skip(1)) { var part = (int)Math.Floor(total * share); if (part > 0) { into(Ago(day), part); left -= part; } }
                if (left > 0) into(Ago(PlayDays[0].day), left);
                return total;
            }
            SplitInt(p.Events.Blocks, (r, n) => r.Events.Blocks += n); SplitInt(p.Events.Parries, (r, n) => r.Events.Parries += n);
            p.Events.Battle.TryGetValue("EnemyHits", out var hitsSince); SplitInt((int)hitsSince, (r, n) => Add(r.Events.Battle, "EnemyHits", n));
            p.Events.Battle.TryGetValue("PlayerHits", out var pvpSince); SplitInt((int)pvpSince, (r, n) => Add(r.Events.Battle, "PlayerHits", n));
            // deaths: by the day of the fall, Hearthwoven's count and the game's counter alike
            foreach (var r in rows.Values) { var fell = r.Biome.Deaths.Values.Sum(); if (fell > 0) { Add(r.Events.Battle, "Deaths", fell); Add(r.Game, "Deaths", fell); } }

            // the game's own counters: the part since install (counter minus baseline where there is one, else a twelfth), split the same way
            var baseline = new Dictionary<string, float>();
            foreach (var kind in new[] { LocalTotals.StatsKind, LocalTotals.BattleKind }) if (p.Baseline != null && p.Baseline.TryGetValue(kind, out var b)) foreach (var kv in b) baseline[kv.Key] = kv.Value;
            if (p.Baseline != null && p.Baseline.TryGetValue(PanelModel.TreesBaseline, out var trees) && trees.TryGetValue("Tree", out var tb)) baseline["Tree"] = tb;
            foreach (var stat in DayHistory.GameStats)
            {
                if (stat == "DistanceSail" || stat == "DistanceSailHelm" || stat == "Deaths" || p.Character == null || !p.Character.TryGetValue(stat, out var now2) || now2 <= 0) continue;
                var since = baseline.TryGetValue(stat, out var at) ? Math.Max(0, now2 - at) : Math.Floor(now2 / (stat == "EnemyKills" ? 20.0 : 12.0));   // a week of a few weeks' play; fewer kills than hits
                if (stat == "Tree") since = p.Events.Felled.Values.Sum();   // the trees felled since install are the ones Hearthwoven counted (the sample's game counter agrees)
                SplitInt((int)since, (r, n) => Add(r.Game, stat, n));
            }

            h.Rows.AddRange(rows.Values);
            h.GameMark["DistanceSail"] = p.Character != null && p.Character.TryGetValue("DistanceSail", out var ds) ? ds : 0f;
            p.History = h;
        }

        static void Skill(PanelInput p, string skill, float level, float progress) { p.SkillLevels[skill] = level; p.SkillProgress[skill] = progress; }

        // the moments of the sample's feats are the one narrative part of them: what the counters earn is worked out, the day and place told
        static FeatMoment Day(DateTime now, double daysAgo, string biome, string place = null) => new FeatMoment { Utc = now.AddDays(-daysAgo), Biome = biome, Place = place };

        static FeatMoment MomentOf(string who, FeatDef d, int tier, DateTime now)
        {
            if (d.Retro) return new FeatMoment { Before = true };
            if (d.Derived) return new FeatMoment { Utc = now.AddDays(-(1 + (who.Length + d.Id.Length + tier) % 3)), Noticed = true };
            switch (d.Id)
            {
                case "keptfires": return Day(now, 2, "Meadows", "at the smelters");
                case "oreroad": return Day(now, tier == 1 ? 4.5 : 1.5, "Ocean", "aboard");
                case "heavykeel": return Day(now, 3, "Ocean", "aboard");
                case "shieldwall": return Day(now, tier == 1 ? 4.5 : 2.5, tier == 1 ? "BlackForest" : "Swamp");
                case "stoodfast": return Day(now, 3, "Swamp");
                case "unbroken": return Day(now, 1.5, "Swamp");
                case "turnedblades": return Day(now, tier == 1 ? 4.8 : 2, "Meadows");
                case "forsaken": return Day(now, 1, "Mountain");
                case "drover": return Day(now, tier == 1 ? 4.5 : 3.5, "Meadows");
                case "longlead": return Day(now, 3.5, "Meadows");
                case "born": return Day(now, 1, "Meadows");
                default: return Day(now, 2, "Meadows");
            }
        }

        /// <summary>The feats a book has, from its counters through the real rules (PanelModel.FeatDefs): the ledger is never written by hand.</summary>
        static void Derive(PanelInput own, DateTime now)
        {
            var ledger = own.Feats;
            foreach (var d in PanelModel.FeatDefs)
            {
                var tier = PanelModel.FeatTierOf(d, PanelModel.FeatValue(d, own));
                for (int t = ledger.Tier(d.Id) + 1; t <= tier; t++) ledger.Earn(d.Id, t, MomentOf(own.PlayerName, d, t, now));
            }
        }

        // =====================================================================================================================
        // ROWAN, the cook and builder (the book the screenshots open on)
        // =====================================================================================================================

        static PanelInput RowanOwn(DateTime now)
        {
            var p = Base(Rowan, RowanId, now);
            var ch = new Dictionary<string, float>
            {
                // on foot, by sea, in the air: the Voyages pages
                ["DistanceWalk"] = 39100, ["DistanceRun"] = 9100, ["DistanceAir"] = 2600, ["Jumps"] = 412, ["DistanceTraveled"] = 79500,
                ["TimeInBase"] = 22200, ["TimeOutOfBase"] = 42000, ["LeviathanSink"] = 2,
                // seconds beyond the far edge (the game's x-axis names are mirrored: ExploreWest is the east)
                ["ExploreNorth"] = 840, ["ExploreSouth"] = 180, ["ExploreWest"] = 1320, ["ExploreEast"] = 540,
                ["TreasureBuriedFound"] = 4, ["TreasureDungeonFound"] = 3, ["TreasureLocationFound"] = 6, ["PortalDungeonIn"] = 9, ["PortalsUsed"] = 37,
                // the kitchen, the forge, the farm, the water, the herd (Deeds)
                ["CraftFood"] = 146, ["CraftGrill"] = 20, ["CraftWeapon"] = 9, ["CraftArmor"] = 6, ["CraftTool"] = 4, ["CraftTrinket"] = 1, ["Upgrades"] = 11,
                ["HarvestCrop"] = 312, ["HarvestBerry"] = 512, ["HarvestMushroom"] = 84, ["BeesHarvested"] = 36, ["SapHarvested"] = 22,
                ["FishHooked"] = 362, ["FishCaught"] = 251, ["FishLost"] = 97, ["FishCaughtTier1"] = 104, ["FishCaughtTier2"] = 71, ["FishCaughtTier3"] = 44,
                ["FishCaughtTier4"] = 22, ["FishCaughtTier5"] = 9, ["FishCaughtTier6"] = 1, ["CreatureTamed"] = 6, ["TamedPetting"] = 41, ["TamedCommand"] = 19,
                ["Tree"] = 410, ["MineHits"] = 650, ["TombstonesOpenedOther"] = 1,
                // the fights: the game's counters now (hits on foes grew by 40 since install; Hearthwoven counted 70, the game misses some)
                ["EnemyKills"] = 860, ["BossKills"] = 3, ["EnemyHits"] = 2640, ["HitsTakenEnemies"] = 1300, ["PlayerHits"] = 3, ["Deaths"] = 15,
                ["BuildClusterDefense"] = 40, ["TrapArmed"] = 2,
            };
            Sea(p, ch);
            p.Character = ch;

            // ---- what he made, built, picked up ----
            foreach (var kv in new Dictionary<string, float>
            {
                // the kitchen: 146 from the cauldron and prep table (CraftFood), 20 off the oven and the grill (CraftGrill)
                ["$item_carrotsoup"] = 60, ["$item_fishwraps"] = 28, ["$item_queensjam"] = 20, ["$item_sausages"] = 18, ["$item_turnipstew"] = 12, ["$item_mod_spicedcider"] = 8,
                ["$item_bread"] = 12, ["$item_cookedmeat"] = 8,
                ["$item_sword_iron"] = 2, ["$item_shield_wood"] = 4, ["$item_axe_bronze"] = 6, ["$item_bow"] = 1, ["$item_helmet_leather"] = 2,
                ["$item_hammer"] = 2, ["$item_hoe"] = 1, ["$item_cultivator"] = 1, ["$item_trinketbronzehealth"] = 1,
                ["$item_arrow_wood"] = 12, ["$item_arrow_fire"] = 4, ["$item_arrow_frost"] = 3, ["$item_arrow_needle"] = 2,
            }) p.ItemsCrafted[kv.Key] = kv.Value;
            foreach (var kv in new Dictionary<string, float>
            {
                ["$piece_woodwall"] = 520, ["$piece_woodfloor2x2"] = 310, ["$piece_sharpstakes"] = 36, ["$piece_levelground"] = 850, ["$piece_raise"] = 133, ["$piece_pavedroad"] = 40,
                ["$piece_sapling_barley"] = 72, ["$piece_sapling_carrot"] = 60, ["$piece_sapling_turnip"] = 64, ["$piece_sapling_beech"] = 12,
                ["$piece_feast_meadows"] = 3, ["$piece_feast_blackforest"] = 2,
            }) p.PiecesPlaced[kv.Key] = kv.Value;
            // the game's pickup counter now = what it was at install + what Hearthwoven counted since (no pickup landed on a stack he carried this week), so the
            // fellows' copy of him, which has no baseline, adds up to the same totals as his own book (a real week would leave the game's count a little behind)
            foreach (var kv in new Dictionary<string, float>
            {
                ["$item_wood"] = 2040, ["$item_finewood"] = 272, ["$item_roundlog"] = 510, ["$item_elderbark"] = 60,
                ["$item_stone"] = 1720, ["$item_copperore"] = 218, ["$item_tinore"] = 99, ["$item_raspberries"] = 40, ["$item_arrow_poison"] = 20,
                ["$item_witheredbone"] = 6, ["$item_chitin"] = 3, ["$item_leatherscraps"] = 64,   // 0.6.5: scrap piles, a Leviathan, boars
            }) p.ItemsPickedUp[kv.Key] = kv.Value;
            foreach (var kv in new Dictionary<string, float>
            {
                ["Carrot"] = 71, ["Barley"] = 155, ["Turnip"] = 66, ["Flint"] = 30, ["$animal_fish1"] = 88, ["$animal_fish2"] = 61, ["$animal_fish3"] = 60, ["$animal_fish6"] = 42,
            }) p.Harvested[kv.Key] = kv.Value;
            // the kills by foe add up to the 860 (and the three bosses to BossKills)
            foreach (var kv in new Dictionary<string, float>
            {
                ["$enemy_greydwarf"] = 372, ["$enemy_boar"] = 118, ["$enemy_neck"] = 64, ["$enemy_greyling"] = 71, ["$enemy_troll"] = 14, ["$enemy_draugr"] = 190,
                ["$enemy_leech"] = 20, ["$enemy_blob"] = 8, ["$enemy_eikthyr"] = 2, ["$enemy_gdking"] = 1,
            }) p.EnemyKills[kv.Key] = kv.Value;
            p.KnownBiomes = new List<string> { "Meadows", "BlackForest", "Swamp", "Mountain", "Ocean" };

            // ---- what Hearthwoven counted since install (Events) ----
            var ev = p.Events;
            ev.Blocks = 312; ev.Parries = 58;
            Add(ev.PickedUp, "$item_wood", 140); Add(ev.PickedUp, "$item_finewood", 12); Add(ev.PickedUp, "$item_roundlog", 30);
            Add(ev.PickedUp, "$item_stone", 220); Add(ev.PickedUp, "$item_copperore", 18); Add(ev.PickedUp, "$item_tinore", 9);
            Add(ev.PickedUp, "$item_witheredbone", 2); Add(ev.PickedUp, "$item_leatherscraps", 10);
            Add(ev.Felled, "Beech1", 17); Add(ev.Felled, "FirTree", 9); Add(ev.Felled, "Birch2", 4);
            Add(ev.ChopHits, "Beech1", 64); Add(ev.ChopHits, "Beech1_log", 20); Add(ev.ChopHits, "beech_log_half", 9); Add(ev.ChopHits, "FirTree", 41);
            Add(ev.PickaxeHits, "rock4_copper", 120);
            Add(ev.Planted, "$piece_sapling_barley", 171); Add(ev.Planted, "$piece_sapling_turnip", 20);
            Add(ev.Made, "$item_carrotsoup", 16); Add(ev.Made, "$item_fishwraps", 14); Add(ev.Made, "$item_sausages", 6); Add(ev.Made, "$item_turnipstew", 4);
            Add(ev.Made, "$item_bread", 6); Add(ev.Made, "$item_cookedmeat", 6);   // 52 dishes since install on top of the 114 before
            Add(ev.Repairs, "woodwall", 9); Add(ev.MapShared, "piece_cartographytable", 2); Add(ev.CartMeters, "Cart", 2700);
            // the trader and the smelters (Hall)
            Add(ev.Spent, "Haldor", 350); Add(ev.Spent, "Hildir", 300);
            Add(ev.Bought, "Haldor|FishingBait", 20); Add(ev.Bought, "Haldor|YmirRemains", 3); Add(ev.Bought, "Hildir|HelmetHat1", 1);   // 24 items for 650 coins: nothing dear
            Add(ev.SmelterAdded, "smelter|CopperOre", 160); Add(ev.SmelterAdded, "smelter|TinOre", 80); Add(ev.SmelterAdded, "smelter|fuel", 120);
            Add(ev.SmelterAdded, "blastfurnace|IronScrap", 640); Add(ev.SmelterAdded, "blastfurnace|fuel", 280); Add(ev.SmelterAdded, "charcoal_kiln|Wood", 60);
            // the table: what he ate that others made, what others' hands put on
            Add(ev.AteFoodMadeBy, "Edda|Bread", 3); Add(ev.AteFoodMadeBy, "Edda|FishWraps", 2); Add(ev.AteFoodMadeBy, "Tor|CookedMeat", 2);
            Add(ev.EquippedGearMadeBy, "Tor|SwordIron", 1); Add(ev.EquippedGearMadeBy, "Tor|ArmorIronChest", 1);
            // cargo at the helm or by cart: wood and stone for the hall, a little coal (item-metres, and the metres with it aboard)
            ev.CargoMeters["$item_wood"] = 410000f; ev.CargoStretch["$item_wood"] = 4100f; ev.CargoMeters["$item_stone"] = 170000f; ev.CargoStretch["$item_stone"] = 1700f;
            ev.CargoMeters["$item_coal"] = 60000f; ev.CargoStretch["$item_coal"] = 1200f;
            // the herd: a wolf and a boar followed him home, young ones born at the farm
            ev.LedMeters["Wolf"] = 1000f; ev.LedMeters["Boar"] = 600f;
            ev.BornInCare["Boar_piggy"] = 5f; ev.BornInCare["Wolf_cub"] = 2f; ev.BornInCare["Lox_Calf"] = 1f; ev.BornInCare["Chicken"] = 3f;
            Add(ev.Battle, "EnemyHits", 70); Add(ev.Battle, "Deaths", 4);
            // what he practised since install: hits, blocks, dishes, bites, seconds on the run, jumps (the game's raise factors, one per use)
            Add(ev.SkillPractice, "WoodCutting", 134); Add(ev.SkillPractice, "Pickaxes", 120); Add(ev.SkillPractice, "Cooking", 52); Add(ev.SkillPractice, "Fishing", 70);
            Add(ev.SkillPractice, "Blocking", 312); Add(ev.SkillPractice, "Run", 340); Add(ev.SkillPractice, "Jump", 41);
            Add(ev.SkillPractice, "Axes", 22); Add(ev.SkillPractice, "Swords", 14); Add(ev.SkillPractice, "Clubs", 10); Add(ev.SkillPractice, "Bows", 12); Add(ev.SkillPractice, "Spears", 8); Add(ev.SkillPractice, "Knives", 4);

            // ---- the baselines: the game's counters when Hearthwoven first ran (every one at or below the counter now) ----
            p.Baseline = new Dictionary<string, Dictionary<string, float>>
            {
                [LocalTotals.StatsKind] = new Dictionary<string, float>
                {
                    ["CraftWeapon"] = 7, ["CraftArmor"] = 4, ["CraftTool"] = 3, ["Upgrades"] = 8,
                    ["FishHooked"] = 292, ["FishCaught"] = 205, ["FishLost"] = 80,
                    ["FishCaughtTier1"] = 88, ["FishCaughtTier2"] = 60, ["FishCaughtTier3"] = 34, ["FishCaughtTier4"] = 17, ["FishCaughtTier5"] = 6,
                    ["HarvestCrop"] = 270, ["HarvestBerry"] = 470, ["HarvestMushroom"] = 70, ["BeesHarvested"] = 36, ["SapHarvested"] = 20,
                    ["TamedPetting"] = 34, ["TamedCommand"] = 16,
                },
                [LocalTotals.PickablesKind] = new Dictionary<string, float>
                {
                    ["Carrot"] = 60, ["Barley"] = 130, ["Turnip"] = 60, ["Flint"] = 30, ["$animal_fish1"] = 68, ["$animal_fish2"] = 49, ["$animal_fish3"] = 60, ["$animal_fish6"] = 28,
                },
                [LocalTotals.PlacedKind] = new Dictionary<string, float>
                {
                    ["$piece_woodwall"] = 440, ["$piece_woodfloor2x2"] = 270, ["$piece_sharpstakes"] = 30, ["$piece_levelground"] = 780, ["$piece_raise"] = 120, ["$piece_pavedroad"] = 40,
                    ["$piece_sapling_barley"] = 40, ["$piece_sapling_carrot"] = 60, ["$piece_sapling_turnip"] = 50, ["$piece_sapling_beech"] = 12,
                },
                // the dishes before install: 114 of the 166 (44 + 14 + 20 + 12 + 8 + 8 from the crafting window, 6 + 2 off the stations); the gear as it stood
                [LocalTotals.CraftedKind] = new Dictionary<string, float>
                {
                    ["$item_carrotsoup"] = 44, ["$item_fishwraps"] = 14, ["$item_queensjam"] = 20, ["$item_sausages"] = 12, ["$item_turnipstew"] = 8, ["$item_mod_spicedcider"] = 8,
                    ["$item_bread"] = 6, ["$item_cookedmeat"] = 2,
                    ["$item_sword_iron"] = 1, ["$item_shield_wood"] = 3, ["$item_axe_bronze"] = 5, ["$item_bow"] = 1, ["$item_helmet_leather"] = 1,
                    ["$item_hammer"] = 2, ["$item_hoe"] = 1, ["$item_arrow_wood"] = 12,
                },
                ["pickedUp"] = new Dictionary<string, float>
                {
                    ["$item_wood"] = 1900, ["$item_finewood"] = 260, ["$item_roundlog"] = 480, ["$item_elderbark"] = 60, ["$item_stone"] = 1500, ["$item_copperore"] = 200, ["$item_tinore"] = 90,
                    ["$item_witheredbone"] = 4, ["$item_chitin"] = 3, ["$item_leatherscraps"] = 54,
                },
                [PanelModel.TreesBaseline] = new Dictionary<string, float> { ["Tree"] = 380 },   // 380 before + 30 felled since = the 410
                ["battle"] = new Dictionary<string, float> { ["EnemyHits"] = 2600, ["Deaths"] = 11 },
            };
            p.ExactAtBaseline = new Dictionary<string, Dictionary<string, float>>
            {
                [LocalTotals.StatsKind] = new Dictionary<string, float>(), [LocalTotals.PickablesKind] = new Dictionary<string, float>(),
                [LocalTotals.PlacedKind] = new Dictionary<string, float>(), [LocalTotals.CraftedKind] = new Dictionary<string, float>(), ["battle"] = new Dictionary<string, float>(),
            };
            var installed = InstalledAt(now);
            p.BaselineAt = new Dictionary<string, DateTime> { [LocalTotals.StatsKind] = installed, [LocalTotals.PickablesKind] = installed, [LocalTotals.PlacedKind] = installed, [LocalTotals.CraftedKind] = installed };

            // ---- skills (levels now, progress to the next) ----
            Skill(p, "Swords", 21, 0.4f); Skill(p, "Knives", 9, 0.5f); Skill(p, "Clubs", 16, 0.1f); Skill(p, "Spears", 27, 0.8f); Skill(p, "Axes", 38.4f, 0.62f);
            Skill(p, "Bows", 31, 0.25f); Skill(p, "Blocking", 28, 0.3f); Skill(p, "Pickaxes", 22, 0.2f); Skill(p, "WoodCutting", 34, 0.55f); Skill(p, "Fishing", 15, 0.7f);
            Skill(p, "Jump", 28, 0.7f); Skill(p, "Run", 55, 0.15f); Skill(p, "Swim", 9, 0.8f); Skill(p, "Cooking", 31, 0.9f);

            // ---- the evening: Battle's session (three falls, all eight damage types) and what came before it ----
            var battle = PanelSample.Battle(now);
            p.Log = battle.Log;
            var bn = now;
            p.Log.AddDamage(bn.AddMinutes(-26), "Swamp", true, "Blob", "ElementalMagic", DT(("spirit", 80)));
            p.Log.AddDamage(bn.AddMinutes(-7), "Swamp", true, "Draugr", "Swords", DT(("slash", 140)));
            p.Log.AddDamage(bn.AddMinutes(-4), "Swamp", true, "Draugr", "Clubs", DT(("blunt", 90)));
            p.Log.AddDamage(bn.AddMinutes(-3), "Swamp", false, "Draugr", "EnemyHit", DT(("slash", 33)));
            p.Log.AddDamage(bn.AddMinutes(-2), "Swamp", true, "Leech", "Bows", DT(("pierce", 45)));
            p.Session = TallyOf(p.Log);
            p.DamageSinceInstall = PanelSample.SinceInstall(p.Log);        // this evening plus the two earlier ones in the Black Forest
            p.BiomeSinceInstall = PanelSample.BiomeSinceInstall(p.Log);    // and the fall of an earlier evening

            // ---- the counters the shield hooks keep (his own PC), and the bests ----
            var f = p.Feats;
            f.Counts[FeatsCounters.BlocksNear] = 252; f.Counts[FeatsCounters.Stopped] = 8750; f.Counts[FeatsCounters.BestStreak] = 6;
            f.NoteBest(LedTracker.BestKey, 1400, now.AddDays(-5), "Meadows", "Wolf");
            BuildingSample.Hall(p);   // Rowan's hall: the pieces with their hammer tabs and materials (Deeds > Building's filter; PanelSampleBuilding.cs)
            return p;
        }

        // =====================================================================================================================
        // EDDA, the hauler and sailor
        // =====================================================================================================================

        static PanelInput EddaOwn(DateTime now)
        {
            var p = Base(Edda, EddaId, now);
            var ch = new Dictionary<string, float>
            {
                ["DistanceWalk"] = 52600, ["DistanceRun"] = 7400, ["DistanceAir"] = 1900, ["Jumps"] = 233, ["DistanceTraveled"] = 138000,
                ["TimeInBase"] = 30600, ["TimeOutOfBase"] = 69000, ["PortalsUsed"] = 21, ["PortalDungeonIn"] = 4, ["TreasureBuriedFound"] = 2, ["ExploreSouth"] = 600,
                ["CraftFood"] = 31, ["CraftGrill"] = 0, ["CraftWeapon"] = 0, ["CraftArmor"] = 1, ["CraftTool"] = 2, ["Upgrades"] = 2,
                ["Tree"] = 85, ["MineHits"] = 150, ["EnemyKills"] = 96, ["EnemyHits"] = 410, ["HitsTakenEnemies"] = 260,
                ["TombstonesOpenedOther"] = 0, ["TamedPetting"] = 4,
            };
            Sea(p, ch); p.Character = ch;
            foreach (var kv in new Dictionary<string, float> { ["$item_bread"] = 21, ["$item_fishwraps"] = 10, ["$item_chest_leather"] = 1, ["$item_hammer"] = 1, ["$item_pickaxe_antler"] = 1 }) p.ItemsCrafted[kv.Key] = kv.Value;
            foreach (var kv in new Dictionary<string, float> { ["$piece_woodwall"] = 52, ["$piece_woodfloor2x2"] = 38, ["$piece_levelground"] = 30 }) p.PiecesPlaced[kv.Key] = kv.Value;
            foreach (var kv in new Dictionary<string, float> { ["$item_wood"] = 640, ["$item_stone"] = 410, ["$item_ironscrap"] = 1880, ["$item_copperore"] = 120 }) p.ItemsPickedUp[kv.Key] = kv.Value;
            foreach (var kv in new Dictionary<string, float> { ["$enemy_greydwarf"] = 41, ["$enemy_draugr"] = 55 }) p.EnemyKills[kv.Key] = kv.Value;
            var ev = p.Events;
            ev.Blocks = 46; ev.Parries = 9;
            Add(ev.PickedUp, "$item_wood", 90); Add(ev.PickedUp, "$item_stone", 60); Add(ev.PickedUp, "$item_ironscrap", 410);
            Add(ev.ChopHits, "Beech1", 24); Add(ev.ChopHits, "FirTree", 16); Add(ev.Felled, "Beech1", 5); Add(ev.Felled, "FirTree", 3);
            Add(ev.PickaxeHits, "rock4_copper", 70); Add(ev.MapShared, "piece_cartographytable", 1);
            Add(ev.Made, "$item_bread", 8); Add(ev.Made, "$item_fishwraps", 4);   // 12 dishes since install
            Add(ev.Spent, "Haldor", 150); Add(ev.Bought, "Haldor|FishingBait", 15);
            Add(ev.AteFoodMadeBy, "Rowan|CarrotSoup", 4); Add(ev.AteFoodMadeBy, "Rowan|FishWraps", 2); Add(ev.AteFoodMadeBy, "Tor|CookedMeat", 3);
            Add(ev.AteFromFeastOf, RowanId + "|FeastMeadows", 5); Add(ev.AteFromFeastOf, RowanId + "|FeastBlackforest", 3);
            Add(ev.EquippedGearMadeBy, "Rowan|AxeBronze", 2);
            // the iron run: scrap by ship (about 520 aboard over 7.5 km), coal, a little wood; the helm and a cart
            ev.CargoMeters["$item_ironscrap"] = 3900000f; ev.CargoStretch["$item_ironscrap"] = 7500f;
            ev.CargoMeters["$item_coal"] = 1000000f; ev.CargoStretch["$item_coal"] = 4000f;
            ev.CargoMeters["$item_wood"] = 450000f; ev.CargoStretch["$item_wood"] = 3000f;
            Add(ev.CartMeters, "Cart", 1800);
            Add(ev.Battle, "EnemyHits", 14);
            Add(ev.SkillPractice, "Run", 210); Add(ev.SkillPractice, "Pickaxes", 70); Add(ev.SkillPractice, "WoodCutting", 40); Add(ev.SkillPractice, "Axes", 14);
            Add(ev.SkillPractice, "Blocking", 46); Add(ev.SkillPractice, "Cooking", 12); Add(ev.SkillPractice, "Swim", 36); Add(ev.SkillPractice, "Jump", 14);
            Skill(p, "Run", 41, 0.35f); Skill(p, "Jump", 19, 0.6f); Skill(p, "Swim", 22, 0.5f); Skill(p, "Axes", 24, 0.2f); Skill(p, "WoodCutting", 27, 0.45f);
            Skill(p, "Pickaxes", 29, 0.7f); Skill(p, "Blocking", 14, 0.55f); Skill(p, "Cooking", 12, 0.3f); Skill(p, "Fishing", 6, 0.1f);
            // the fights: two in the Swamp this session; an evening in the Black Forest before it
            p.Log.AddDamage(now.AddMinutes(-150), "Swamp", true, "Draugr", "Axes", DT(("slash", 100)));
            p.Log.AddDamage(now.AddMinutes(-6), "Swamp", true, "Draugr", "Axes", DT(("slash", 200)));
            p.Log.AddDamage(now.AddMinutes(-6), "Swamp", false, "Draugr", "EnemyHit", DT(("slash", 40)));
            Since(p, now, (s, b) => { Earlier(s, b, "BlackForest", true, "Greydwarf", "Axes", ("slash", 340)); Earlier(s, b, "BlackForest", false, "Greydwarf", "EnemyHit", ("slash", 60)); });
            p.Feats.NoteBest(CargoVoyage.BestKey, 500, now.AddDays(-3), "Ocean", null);
            return p;
        }

        // =====================================================================================================================
        // FINCH, the scout
        // =====================================================================================================================

        static PanelInput FinchOwn(DateTime now)
        {
            var p = Base(Finch, FinchId, now);
            var ch = new Dictionary<string, float>
            {
                ["DistanceWalk"] = 118000, ["DistanceRun"] = 31000, ["DistanceAir"] = 4800, ["Jumps"] = 1120, ["DistanceTraveled"] = 176000,
                ["TimeInBase"] = 18000, ["TimeOutOfBase"] = 96000, ["LeviathanSink"] = 0,
                ["ExploreNorth"] = 2400, ["ExploreWest"] = 1800, ["ExploreSouth"] = 900, ["ExploreEast"] = 2100, ["Deaths"] = 3,
                ["TreasureBuriedFound"] = 11, ["TreasureDungeonFound"] = 6, ["TreasureLocationFound"] = 14, ["PortalDungeonIn"] = 15, ["PortalsUsed"] = 52,
                ["CraftFood"] = 0, ["CraftGrill"] = 0, ["Tree"] = 14, ["MineHits"] = 140, ["EnemyKills"] = 320, ["EnemyHits"] = 980, ["HitsTakenEnemies"] = 410,
                ["TombstonesOpenedOther"] = 6, ["CreatureTamed"] = 9, ["TamedPetting"] = 88, ["TamedCommand"] = 41,
            };
            Sea(p, ch); p.Character = ch;
            foreach (var kv in new Dictionary<string, float> { ["$item_arrow_wood"] = 60, ["$item_arrow_fire"] = 20 }) p.ItemsCrafted[kv.Key] = kv.Value;
            foreach (var kv in new Dictionary<string, float> { ["$item_wood"] = 180, ["$item_copperore"] = 120, ["$item_silverore"] = 12 }) p.ItemsPickedUp[kv.Key] = kv.Value;
            foreach (var kv in new Dictionary<string, float> { ["$enemy_greydwarf"] = 90, ["$enemy_boar"] = 110, ["$enemy_draugr"] = 70, ["$enemy_neck"] = 50 }) p.EnemyKills[kv.Key] = kv.Value;
            var ev = p.Events;
            ev.Blocks = 12; ev.Parries = 1;
            Add(ev.PickedUp, "$item_wood", 40); Add(ev.PickedUp, "$item_copperore", 30);
            Add(ev.ChopHits, "FirTree", 6); Add(ev.Felled, "FirTree", 1); Add(ev.PickaxeHits, "rock4_copper", 30); Add(ev.MapShared, "piece_cartographytable", 6);
            Add(ev.AteFoodMadeBy, "Tor|CookedMeat", 5); Add(ev.AteFoodMadeBy, "Rowan|FishWraps", 3); Add(ev.AteFoodMadeBy, "Rowan|CarrotSoup", 2);
            Add(ev.AteFromFeastOf, RowanId + "|FeastMeadows", 3); Add(ev.AteFromFeastOf, RowanId + "|FeastBlackforest", 3);
            Add(ev.EquippedGearMadeBy, "Edda|ArmorLeatherChest", 1); Add(ev.EquippedGearMadeBy, "Rowan|Bow", 1); Add(ev.EquippedGearMadeBy, "Rowan|HelmetLeather", 1);
            ev.LedMeters["Boar"] = 4900f; ev.LedMeters["Wolf"] = 3600f;
            Add(ev.Battle, "EnemyHits", 38); Add(ev.Battle, "Deaths", 1);
            Add(ev.SkillPractice, "Run", 620); Add(ev.SkillPractice, "Jump", 190); Add(ev.SkillPractice, "Swim", 44); Add(ev.SkillPractice, "Bows", 38); Add(ev.SkillPractice, "Pickaxes", 30);
            Add(ev.SkillPractice, "WoodCutting", 6); Add(ev.SkillPractice, "Blocking", 12);
            Skill(p, "Run", 72, 0.5f); Skill(p, "Jump", 61, 0.2f); Skill(p, "Swim", 33, 0.4f); Skill(p, "Bows", 46, 0.7f); Skill(p, "Knives", 28, 0.35f);
            Skill(p, "Blocking", 11, 0.6f); Skill(p, "Pickaxes", 15, 0.3f); Skill(p, "WoodCutting", 9, 0.5f); Skill(p, "Swords", 9, 0.15f);
            // the scout is everywhere in one evening: the Meadows, the Black Forest, the Plains, then the Swamp (a fellow's copy knows the biomes found only by where they fought)
            p.Log.AddDamage(now.AddMinutes(-205), "Meadows", true, "Boar", "Bows", DT(("pierce", 50)));
            p.Log.AddDamage(now.AddMinutes(-140), "BlackForest", true, "Greydwarf", "Bows", DT(("pierce", 70)));
            p.Log.AddDamage(now.AddMinutes(-55), "Plains", true, "Goblin", "Bows", DT(("pierce", 90)));
            p.Log.AddDamage(now.AddMinutes(-32), "Swamp", true, "Leech", "Bows", DT(("pierce", 60)));
            p.Log.AddDamage(now.AddMinutes(-9), "Swamp", true, "Draugr", "Bows", DT(("pierce", 80)));
            p.Log.AddDamage(now.AddMinutes(-9), "Swamp", false, "Draugr", "EnemyHit", DT(("pierce", 20)));
            p.Log.AddDamage(now.AddMinutes(-75).AddSeconds(-8), "Mountain", false, "Freezing", "Freezing", DT(("frost", 38)));   // a scout in the Mountains without warm clothes
            p.Log.AddDeath(now.AddMinutes(-75), "Mountain", 300, -120);
            Since(p, now, (s, b) =>
            {
                Earlier(s, b, "Meadows", true, "Boar", "Bows", ("pierce", 110)); Earlier(s, b, "BlackForest", true, "Greydwarf", "Bows", ("pierce", 110), ("fire", 60));
                Earlier(s, b, "BlackForest", false, "Greydwarf", "EnemyHit", ("slash", 45));
            });
            LedSample.Bests(p.Feats, "Finch", now);   // the longest lead: 2.4 km, a boar
            return p;
        }

        // =====================================================================================================================
        // TOR, the tank and parrier
        // =====================================================================================================================

        static PanelInput TorOwn(DateTime now)
        {
            var p = Base(Tor, TorId, now);
            var ch = new Dictionary<string, float>
            {
                ["DistanceWalk"] = 41000, ["DistanceRun"] = 6000, ["DistanceAir"] = 1100, ["Jumps"] = 160, ["DistanceTraveled"] = 72000,
                ["TimeInBase"] = 26400, ["TimeOutOfBase"] = 54000, ["PortalsUsed"] = 18,
                ["CraftFood"] = 14, ["CraftGrill"] = 61, ["CraftWeapon"] = 1, ["CraftArmor"] = 3, ["Upgrades"] = 5,
                ["Tree"] = 60, ["MineHits"] = 90, ["EnemyKills"] = 410, ["EnemyHits"] = 1700, ["HitsTakenEnemies"] = 2900, ["Deaths"] = 4,
                ["BuildClusterDefense"] = 22, ["TrapArmed"] = 5, ["TombstonesOpenedOther"] = 3,
            };
            Sea(p, ch); p.Character = ch;
            foreach (var kv in new Dictionary<string, float>
            {
                ["$item_cookedmeat"] = 61, ["$item_boarjerky"] = 14, ["$item_sword_iron"] = 1, ["$item_chest_iron"] = 1, ["$item_shield_banded"] = 1, ["$item_helmet_iron"] = 1,
            }) p.ItemsCrafted[kv.Key] = kv.Value;
            foreach (var kv in new Dictionary<string, float> { ["$piece_woodwall"] = 86, ["$piece_woodfloor2x2"] = 30, ["$piece_sharpstakes"] = 64, ["$piece_raise"] = 55 }) p.PiecesPlaced[kv.Key] = kv.Value;
            foreach (var kv in new Dictionary<string, float> { ["$item_wood"] = 420, ["$item_stone"] = 150 }) p.ItemsPickedUp[kv.Key] = kv.Value;
            foreach (var kv in new Dictionary<string, float> { ["$enemy_greydwarf"] = 150, ["$enemy_troll"] = 9, ["$enemy_draugr"] = 160, ["$enemy_boar"] = 60, ["$enemy_neck"] = 31 }) p.EnemyKills[kv.Key] = kv.Value;
            var ev = p.Events;
            ev.Blocks = 1940; ev.Parries = 560;
            Add(ev.PickedUp, "$item_wood", 70); Add(ev.ChopHits, "Beech1", 28); Add(ev.Felled, "Beech1", 5); Add(ev.PickaxeHits, "rock4_copper", 40); Add(ev.Repairs, "woodwall", 14);
            Add(ev.Made, "$item_cookedmeat", 20); Add(ev.Made, "$item_boarjerky", 9);   // 29 dishes since install
            Add(ev.AteFoodMadeBy, "Rowan|CarrotSoup", 6); Add(ev.AteFoodMadeBy, "Rowan|Bread", 2);
            Add(ev.AteFromFeastOf, RowanId + "|FeastMeadows", 4); Add(ev.AteFromFeastOf, RowanId + "|FeastBlackforest", 3);
            Add(ev.EquippedGearMadeBy, "Rowan|ShieldWood", 1);
            ev.CargoMeters["$item_wood"] = 150000f; ev.CargoStretch["$item_wood"] = 1500f; ev.CargoMeters["$item_stone"] = 90000f; ev.CargoStretch["$item_stone"] = 900f;
            Add(ev.CartMeters, "Cart", 900);
            ev.BornInCare["Boar_piggy"] = 3f;
            Add(ev.Battle, "EnemyHits", 52); Add(ev.Battle, "Deaths", 1);
            Add(ev.SkillPractice, "Blocking", 1940); Add(ev.SkillPractice, "Clubs", 38); Add(ev.SkillPractice, "Swords", 14); Add(ev.SkillPractice, "Run", 190);
            Add(ev.SkillPractice, "Cooking", 29); Add(ev.SkillPractice, "Pickaxes", 40); Add(ev.SkillPractice, "WoodCutting", 28);
            Skill(p, "Blocking", 61, 0.45f); Skill(p, "Clubs", 44, 0.3f); Skill(p, "Swords", 31, 0.6f); Skill(p, "Run", 38, 0.2f); Skill(p, "Jump", 22, 0.55f);
            Skill(p, "Swim", 15, 0.4f); Skill(p, "Axes", 19, 0.1f); Skill(p, "Cooking", 21, 0.7f); Skill(p, "Pickaxes", 12, 0.5f); Skill(p, "WoodCutting", 16, 0.25f);
            // the fights: a Troll in the Black Forest and a Draugr in the Swamp, one fall to poison; earlier evenings in the Meadows and Black Forest
            p.Log.AddDamage(now.AddMinutes(-100), "BlackForest", true, "Troll", "Clubs", DT(("blunt", 320)));
            p.Log.AddDamage(now.AddMinutes(-98), "BlackForest", false, "Troll", "EnemyHit", DT(("blunt", 140)));
            p.Log.AddDamage(now.AddMinutes(-55), "Swamp", true, "Draugr", "Clubs", DT(("blunt", 180)));
            p.Log.AddDamage(now.AddMinutes(-54), "Swamp", false, "Draugr", "EnemyHit", DT(("slash", 60)));
            p.Log.AddDamage(now.AddMinutes(-20).AddSeconds(-6), "Swamp", false, "Blob", "EnemyHit", DT(("poison", 45)));
            p.Log.AddDeath(now.AddMinutes(-20), "Swamp", 10, 20);
            Since(p, now, (s, b) =>
            {
                Earlier(s, b, "Meadows", true, "Boar", "Clubs", ("blunt", 400)); Earlier(s, b, "BlackForest", true, "Greydwarf", "Clubs", ("blunt", 700)); Earlier(s, b, "Meadows", true, "Neck", "Clubs", ("blunt", 500));
                Earlier(s, b, "BlackForest", false, "Greydwarf", "EnemyHit", ("slash", 380)); Earlier(s, b, "Meadows", false, "Boar", "EnemyHit", ("pierce", 120));
            });
            var f = p.Feats;
            f.Counts[FeatsCounters.BlocksNear] = 1180; f.Counts[FeatsCounters.Stopped] = 61800; f.Counts[FeatsCounters.BestStreak] = 14; f.Counts[FeatsCounters.BossParries] = 1;
            return p;
        }

        /// <summary>The log's session and the earlier evenings, folded: Session = the log's rows, since install = the session plus the earlier ones, per biome likewise.</summary>
        static void Since(PanelInput p, DateTime now, Action<DamageTally, BiomeTally> earlier)
        {
            p.Session = TallyOf(p.Log);
            var since = DamageTally.Sum(p.Session); var bio = BiomeTally.FromLog(p.Log);
            earlier(since, bio);
            p.DamageSinceInstall = since; p.BiomeSinceInstall = bio;
        }
    }
}
