// 0.9 more feats (Chapters/FeatsMore.cs, work/hearthwoven-0.9/FEAT-CATALOG.md): each new feat's number from a record built by hand, and its tiers
// at their boundaries (one step under a line: not earned; on it: earned), so a count never reads something the record does not hold. Then the
// new hooks' counters (FeatsCounters: Steady at the Grill, Whole Crew, Held the Line, Before the Altar, Mender of Others, Raid Breaker) as plain
// rules, the families' names and pictures, and a land's own feat waiting until the land is found. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class MoreFeatsTests
{
    static readonly DateTime Now = new DateTime(2026, 10, 18, 14, 0, 0, DateTimeKind.Utc);

    static PanelInput Me() => new PanelInput
    {
        PlayerName = "Rowan", PlayerId = 11, IsSelf = true, NowUtc = Now, InstalledUtc = Now.AddDays(-10), Events = new SessionEvents(), Feats = new FeatsLedger { Primed = true },
        Character = new Dictionary<string, float>(), Fellows = new List<PanelInput>(), PlayerNames = new Dictionary<long, string>(),
    };
    static PanelInput Fellow(string name) => new PanelInput { PlayerName = name, IsSelf = false, NowUtc = Now, Events = new SessionEvents(), Feats = new FeatsLedger(), Character = new Dictionary<string, float>() };
    static PanelInput WithFellow(PanelInput me, PanelInput f) { me.Fellows.Add(f); f.Fellows = new List<PanelInput> { me }; return me; }

    static BossAttempt Boss(string kind, BossOutcome outcome, params (string name, bool self, int deaths)[] players)
    {
        var a = new BossAttempt { Kind = kind, Outcome = outcome, StartUtc = Now.AddHours(-1) };
        foreach (var p in players) a.Players.Add(new AttemptPlayer { Name = p.name, Self = p.self, Deaths = p.deaths });
        return a;
    }
    static FightView Run(string place, bool live, params (string name, bool you, int deaths)[] players)
    {
        var v = new FightView { Id = place + players.Length + live, Kind = FightKind.Run, Outcome = live ? FightOutcome.Live : FightOutcome.Ended, Place = place, StartUtc = Now.AddHours(-2), EndUtc = Now.AddHours(-1) };
        foreach (var p in players) { var fp = new FightPlayer { Name = p.name, You = p.you }; for (int k = 0; k < p.deaths; k++) fp.Deaths.Add(new FightDeath()); v.Players.Add(fp); }
        return v;
    }
    static readonly string[] Types = { "blunt", "slash", "pierce", "fire", "frost", "lightning", "poison", "spirit" };

    /// <summary>Each new personal feat: a record that holds exactly <c>v</c> of what it counts (plus something it must not count), and the step its number moves by.</summary>
    static readonly (string id, Func<double, PanelInput> make, double step)[] Personal =
    {
        ("secondhelping", v => { var f = Fellow("Edda"); f.Events.AteFoodMadeBy["Rowan|Bread"] = (float)v; f.Events.AteFoodMadeBy["Edda|Bread"] = 9; f.Events.AteFoodMadeBy["Tor|Bread"] = 7; return WithFellow(Me(), f); }, 1),
        ("steadygrill", v => { var p = Me(); p.Feats.Counts[FeatsCounters.GrillBest] = v; return p; }, 1),
        ("trustededge", v => { var f = Fellow("Edda"); f.Events.EquippedGearMadeBy["Rowan|AxeBronze"] = (float)v; f.Events.EquippedGearMadeBy["Tor|AxeBronze"] = 5; return WithFellow(Me(), f); }, 1),
        ("honed", v =>
        {
            var p = Me(); p.Upgrades = new UpgradeLog { FromUtc = Now.AddDays(-3) }; p.MaxQuality = t => 4;
            for (int k = 0; k < (int)v; k++) p.Upgrades.Add(new UpgradeMoment { Token = "$item_top" + k, From = 3, To = 4, Utc = Now });
            p.Upgrades.Add(new UpgradeMoment { Token = "$item_half", From = 2, To = 3, Utc = Now });   // not at its top: not counted
            return p;
        }, 1),
        ("arrowsforall", v => { var p = Me(); p.RecipeYield = t => 20; p.ItemsCrafted = new Dictionary<string, float> { ["$item_arrow_wood"] = (float)(v / 20), ["$item_turretbolt"] = 50, ["$item_bread"] = 4 }; return p; }, 20),
        ("cartroad", v => { var p = Me(); p.Events.CartMeters["Cart"] = (float)(v * 1000); return p; }, 0.1),
        ("wholecrew", v => { var p = Me(); p.Feats.Counts[FeatsCounters.CrewBest] = v * 60; return p; }, 1 / 6.0),
        ("heldtheline", v => { var p = Me(); p.Feats.Counts[FeatsCounters.HeldLineBest] = v; return p; }, 1),
        ("braced", v => { var p = Me(); p.Events.Blocks = (int)v; return p; }, 1),
        ("ironhide", v => { var p = Me(); p.ArmourSince = new ArmourTally(); p.ArmourSince.Before["Troll|EnemyHit|blunt"] = (float)(v + 120); p.ArmourSince.After["Troll|EnemyHit|blunt"] = 120; return p; }, 1),
        ("shoulder", v =>
        {
            var p = Me(); p.FoesSince = new FoeCounts();
            var g = p.FoesSince.Of("Greydwarf"); g.Fought = (int)v + 30; g.Defeated = (int)v + 20; g.With = (int)v;   // twenty defeated alone: not counted
            return p;
        }, 1),
        ("weakspot", v =>
        {
            var p = Me(); p.DealtArmourSince = new ArmourTally();
            p.DealtArmourSince.Incoming["Troll|Axes|fire"] = (float)(v / 2); p.DealtArmourSince.After["Troll|Axes|fire"] = (float)v;   // weak to fire: more got through than was swung
            p.DealtArmourSince.Incoming["Troll|Axes|slash"] = 400; p.DealtArmourSince.After["Troll|Axes|slash"] = 300;            // resisted: not counted
            return p;
        }, 1),
        ("everyedge", v => { var p = Me(); p.DamageSinceInstall = new DamageTally(); for (int k = 0; k < 8; k++) p.DamageSinceInstall.Dealt["Troll|Axes|" + Types[k]] = k < v ? 500 : 499; return p; }, 1),
        ("spiritdead", v => { var p = Me(); p.DamageSinceInstall = new DamageTally(); p.DamageSinceInstall.Dealt["Draugr|Swords|spirit"] = (float)v; p.DamageSinceInstall.Dealt["Draugr|Swords|slash"] = 900; return p; }, 1),
        ("coldiron", v => { var p = Me(); p.KnownBiomes = new[] { "AshLands" }; p.BiomeSinceInstall = new BiomeTally(); p.BiomeSinceInstall.AddDamage("AshLands", true, "frost", (float)v); p.BiomeSinceInstall.AddDamage("Mountain", true, "frost", 999); return p; }, 1),
        ("longreach", v => { var p = Me(); p.DamageSinceInstall = new DamageTally(); p.DamageSinceInstall.Dealt["Deer|Bows|pierce"] = (float)(v / 2); p.DamageSinceInstall.Dealt["Troll|Crossbows|pierce"] = (float)(v / 2); p.DamageSinceInstall.Dealt["Troll|Swords|slash"] = 900; return p; }, 2),
        ("loosed", v => { var p = Me(); p.Character["ArrowsShot"] = (float)v; return p; }, 1),
        ("fletchings", v => { var p = Me(); p.DamageSinceInstall = new DamageTally(); for (int k = 0; k < 8; k++) { p.DamageSinceInstall.Dealt["Troll|Bows|" + Types[k]] = k < v ? 250 : 100; p.DamageSinceInstall.Dealt["Troll|Swords|" + Types[k]] = 900; } return p; }, 1),
        ("eitrunbound", v => { var p = Me(); p.DamageSinceInstall = new DamageTally(); p.DamageSinceInstall.Dealt["Troll|ElementalMagic|fire"] = (float)(v / 2); p.DamageSinceInstall.Dealt["Troll|BloodMagic|blunt"] = (float)(v / 2); p.DamageSinceInstall.Dealt["Troll|Bows|fire"] = 900; return p; }, 2),
        ("threestorms", v => { var p = Me(); p.DamageSinceInstall = new DamageTally(); var e = new[] { "fire", "frost", "lightning" }; for (int k = 0; k < 3; k++) p.DamageSinceInstall.Dealt["Troll|ElementalMagic|" + e[k]] = k < v ? 1000 : 999; p.DamageSinceInstall.Dealt["Troll|Bows|fire"] = 5000; return p; }, 1),
        ("rightspell", v =>
        {
            var p = Me(); p.DealtArmourSince = new ArmourTally();
            p.DealtArmourSince.Incoming["Draugr|ElementalMagic|fire"] = (float)(v / 2); p.DealtArmourSince.After["Draugr|ElementalMagic|fire"] = (float)v;
            p.DealtArmourSince.Incoming["Draugr|Swords|spirit"] = 300; p.DealtArmourSince.After["Draugr|Swords|spirit"] = 600;   // weak, but not by a staff
            return p;
        }, 2),
        ("sidebyside", v => { var p = Me(); p.Character["BossKillMultiplayer"] = (float)v; p.Character["BossKillSolo"] = 9; return p; }, 1),
        ("altar", v => { var p = Me(); p.Feats.Counts[FeatsCounters.Summons] = v; return p; }, 1),
        ("facedtogether", v =>
        {
            var p = Me(); p.BossLog = new BossLog();
            for (int k = 0; k < (int)v; k++) { p.BossLog.Attempts.Add(Boss("Boss" + k, BossOutcome.Won, ("Rowan", true, 0), ("Edda", false, 1))); p.BossLog.Attempts.Add(Boss("Boss" + k, BossOutcome.Won, ("Rowan", true, 0), ("Tor", false, 0))); }
            p.BossLog.Attempts.Add(Boss("Solo", BossOutcome.Won, ("Rowan", true, 0)));                     // alone: not together
            p.BossLog.Attempts.Add(Boss("Reset", BossOutcome.Reset, ("Rowan", true, 0), ("Edda", false, 0)));   // not won
            return p;
        }, 1),
        ("notonefell", v =>
        {
            var p = Me(); p.BossLog = new BossLog();
            p.BossLog.Attempts.Add(Boss("Eikthyr", BossOutcome.Won, ("Rowan", true, 0), ("Edda", false, 1)));
            p.BossLog.Attempts.Add(Boss("gd_king", BossOutcome.Reset, ("Rowan", true, 0)));
            if (v >= 1) p.BossLog.Attempts.Add(Boss("Bonemass", BossOutcome.Won, ("Rowan", true, 0), ("Edda", false, 0)));
            return p;
        }, 1),
        ("intodark", v => { var p = Me(); p.Character["PortalDungeonIn"] = (float)v; p.Character["PortalDungeonOut"] = 99; return p; }, 1),
        ("deephalls", v => { var p = Me(); p.Character["TreasureDungeonFound"] = (float)v; p.Character["TreasureBuriedFound"] = 99; return p; }, 1),
        ("hallsbelow", v =>
        {
            var p = Me(); p.Fights = new FightBook();
            for (int k = 0; k < (int)v; k++) { p.Fights.Fights.Add(Run("Place " + k, false, ("Rowan", true, 0))); p.Fights.Fights.Add(Run("place " + k, false, ("Rowan", true, 0))); }   // the same kind twice
            p.Fights.Fights.Add(Run("Live Place", true, ("Rowan", true, 0)));       // still inside: not run through yet
            p.Fights.Fights.Add(Run("Their Place", false, ("Edda", false, 0)));     // you were not in it
            return p;
        }, 1),
        ("cameback", v =>
        {
            var p = Me(); p.Fights = new FightBook();
            for (int k = 0; k < (int)v; k++) p.Fights.Fights.Add(Run("Crypt", false, ("Rowan", true, 0), ("Edda", false, 0)));
            p.Fights.Fights.Add(Run("Crypt", false, ("Rowan", true, 0), ("Edda", false, 1)));   // a fellow fell
            p.Fights.Fights.Add(Run("Crypt", false, ("Rowan", true, 0)));                       // alone
            return p;
        }, 1),
        ("manylands", v => { var p = Me(); p.KnownBiomes = new[] { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Ocean", "Mistlands", "AshLands", "DeepNorth" }.Take((int)v).Concat(new[] { "None" }).ToList(); return p; }, 1),
        ("portalraiser", v => { var p = Me(); p.PiecesPlaced = new Dictionary<string, float> { ["$piece_portal"] = (float)(v - Math.Floor(v / 2)), ["$piece_portal_stone"] = (float)Math.Floor(v / 2), ["$piece_woodwall"] = 40 }; return p; }, 1),
        ("mapgiver", v => { var p = Me(); p.Events.MapShared["piece_cartographytable"] = (float)v; return p; }, 1),
        ("groundbreaker", v => { var p = Me(); p.PieceKind = t => t == "$piece_level" || t == "$piece_paved" ? "ground" : "built"; p.PiecesPlaced = new Dictionary<string, float> { ["$piece_level"] = (float)(v * 0.75), ["$piece_paved"] = (float)(v * 0.25), ["$piece_woodwall"] = 400 }; return p; }, 1),
        ("keptstanding", v => { var p = Me(); p.Events.Repairs["woodwall"] = (float)v; return p; }, 1),
        ("menderothers", v => { var p = Me(); p.Feats.Counts[FeatsCounters.RepairsOthers] = v; return p; }, 1),
        ("ballista", v => { var p = Me(); p.Character["TurretAmmoAdded"] = (float)v; return p; }, 1),
        ("raidbreaker", v => { var p = Me(); p.Feats.Counts[FeatsCounters.Raids] = v; return p; }, 1),
        ("timber", v => { var p = Me(); p.ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = (float)(v * 0.75), ["$item_finewood"] = (float)(v * 0.25), ["$item_stone"] = 900, ["$item_resin"] = 70 }; return p; }, 1),
        ("deepore", v => { var p = Me(); p.ItemsPickedUp = new Dictionary<string, float> { ["$item_copperore"] = (float)(v * 0.75), ["$item_tinore"] = (float)(v * 0.25), ["$item_stone"] = 900, ["$item_copper"] = 70 }; return p; }, 1),
        ("seedsower", v => { var p = Me(); p.Events.Planted["$piece_sapling_carrot"] = (float)v; return p; }, 1),
        ("everyfield", v =>
        {
            var p = Me(); p.PieceKind = t => t.StartsWith("$piece_sapling", StringComparison.Ordinal) ? "planted" : "built";
            p.CropOf = t => t.StartsWith("$piece_sapling_crop", StringComparison.Ordinal) ? "Crop" + t.Substring(19) : null;   // the tree sapling has no crop
            p.PiecesPlaced = new Dictionary<string, float> { ["$piece_sapling_birch"] = 30 };
            for (int k = 0; k < (int)v; k++) p.PiecesPlaced["$piece_sapling_crop" + k] = 5;
            return p;
        }, 1),
        ("tappedroot", v => { var p = Me(); p.KnownBiomes = new[] { "Mistlands" }; p.Character["SapHarvested"] = (float)v; p.Character["BeesHarvested"] = 999; return p; }, 1),
        ("fishhall", v => { var p = Me(); p.Character["FishCaught"] = (float)v; p.Character["FishHooked"] = 999; return p; }, 1),
        ("everywater", v => { var p = Me(); p.Harvested = new Dictionary<string, float> { ["Barley"] = 40 }; for (int k = 1; k <= (int)v; k++) p.Harvested["$animal_fish" + k] = 2; return p; }, 1),
    };

    /// <summary>Each new group feat: a group of you and Edda whose parts add up to <c>v</c>.</summary>
    static readonly (string id, Func<double, PanelInput> make, double step)[] Group =
    {
        ("silvervein", v => Metal("$item_silver", v), 2), ("blackmetal", v => Metal("$item_blackmetal", v), 2), ("eitr", v => Metal("$item_eitr", v), 2), ("flametal", v => Metal("$item_flametal", v), 2),
        ("herdgrows", v => { var me = Me(); var f = Fellow("Edda"); me.Events.BornInCare["Boar_piggy"] = (float)(v / 2); f.Events.BornInCare["Wolf_cub"] = (float)(v / 2); return WithFellow(me, f); }, 2),
    };
    static PanelInput Metal(string token, double v)
    {
        var me = Me(); var f = Fellow("Edda");
        me.ItemsPickedUp = new Dictionary<string, float> { [token] = (float)(v / 2) }; f.ItemsPickedUp = new Dictionary<string, float> { [token] = (float)(v / 2), ["$item_iron"] = 500 };
        return WithFellow(me, f);
    }

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- every new feat: its number from the record, its tiers at their lines ----------
        void Lines((string id, Func<double, PanelInput> make, double step) t)
        {
            var d = PanelModel.FeatById(t.id);
            if (d == null) { Check(false, "more feats: " + t.id + " is in the table"); return; }
            var bad = new List<string>();
            for (int k = 0; k < d.Tiers.Length; k++)
            {
                var at = t.make(d.Tiers[k]); var under = t.make(d.Tiers[k] - t.step);
                var vAt = PanelModel.FeatValue(d, at); var vUnder = PanelModel.FeatValue(d, under);
                if (Math.Abs(vAt - d.Tiers[k]) > 1e-6) bad.Add("reads " + vAt + " for " + d.Tiers[k]);
                if (PanelModel.FeatTierOf(d, vAt) != k + 1) bad.Add("tier " + (k + 1) + " not earned at " + d.Tiers[k]);
                if (PanelModel.FeatTierOf(d, vUnder) != k) bad.Add("tier " + (k + 1) + " earned at " + vUnder);
            }
            var none = t.make(0);
            if (PanelModel.FeatValue(d, none) != 0 || PanelModel.FeatTier(d, none) != 0) bad.Add("an empty record reads " + PanelModel.FeatValue(d, none));
            Check(bad.Count == 0, "more feats: " + d.Name + " (" + d.Family + ") counts only its record, tiers " + string.Join(" / ", d.Tiers.Select(PanelModel.Number)) + " at their lines" + (bad.Count > 0 ? ": " + string.Join("; ", bad) : ""));
        }
        foreach (var t in Personal.Concat(Group)) Lines(t);

        var news = new[] { "secondhelping", "steadygrill", "trustededge", "honed", "arrowsforall", "cartroad", "wholecrew", "heldtheline", "braced", "ironhide", "shoulder", "weakspot", "everyedge",
                           "spiritdead", "coldiron", "longreach", "loosed", "fletchings", "eitrunbound", "threestorms", "rightspell", "sidebyside", "altar", "facedtogether", "notonefell",
                           "intodark", "deephalls", "hallsbelow", "cameback", "manylands", "portalraiser", "mapgiver", "groundbreaker", "keptstanding", "menderothers", "ballista", "raidbreaker",
                           "timber", "deepore", "seedsower", "everyfield", "tappedroot", "fishhall", "everywater" };
        var groupNews = new[] { "silvervein", "blackmetal", "eitr", "flametal", "onehearth", "herdgrows" };
        Check(news.All(id => PanelModel.FeatById(id) is FeatDef d && !d.Group) && groupNews.All(id => PanelModel.FeatById(id)?.Group == true) && PanelModel.FeatDefs.Length == 26 + news.Length + groupNews.Length + FeatsBatch2Tests.Personal.Length + FeatsBatch2Tests.GroupIds.Length + TogetherBatch3Tests.Ids.Length + 1 &&   // + 1: Clad as One (ArmorWornTests)
              Personal.Select(p => p.id).OrderBy(x => x).SequenceEqual(news.OrderBy(x => x)),
              "more feats: " + news.Length + " personal and " + groupNews.Length + " group feats added (" + PanelModel.FeatDefs.Length + " in all), each tested above");
        Check(PanelModel.FeatDefs.Length <= FeatsLedger.MaxFeats, "more feats: the ledger has room for every feat (" + PanelModel.FeatDefs.Length + " of " + FeatsLedger.MaxFeats + ")");

        // arrows: a craft makes what its recipe makes; where this PC does not know the recipe, one per craft (never more than recorded)
        {
            var p = Me(); p.ItemsCrafted = new Dictionary<string, float> { ["$item_arrow_fire"] = 7, ["$item_bolt_iron"] = 3 };
            Check(PanelModel.FeatValue(PanelModel.FeatById("arrowsforall"), p) == 10, "arrows for all: recipe yield unknown: one per craft (10 crafts, 10)");
            p.RecipeYield = t => 20;
            Check(PanelModel.FeatValue(PanelModel.FeatById("arrowsforall"), p) == 200, "arrows for all: each craft counts what its recipe makes (10 crafts of 20)");
        }
        // One Hearth: every member who shares fed by another; two or more members
        {
            var d = PanelModel.FeatById("onehearth");
            var me = Me(); var edda = Fellow("Edda"); WithFellow(me, edda);
            me.Events.AteFoodMadeBy["Edda|Bread"] = 2;
            Check(PanelModel.FeatValue(d, me) == 0, "one hearth: one of two fed by the other is not yet");
            edda.Events.AteFoodMadeBy["Edda|Bread"] = 5;
            Check(PanelModel.FeatValue(d, me) == 0, "one hearth: food of one's own does not count");
            edda.Events.AteFoodMadeBy["Rowan|Fish"] = 1;
            Check(PanelModel.FeatValue(d, me) == 1 && PanelModel.FeatTierOf(d, 1) == 1, "one hearth: both fed by the other: earned");
            var alone = Me(); alone.Events.AteFoodMadeBy["Edda|Bread"] = 4;
            Check(PanelModel.FeatValue(d, alone) == 0, "one hearth: one player alone never earns it (it needs two who share)");
        }
        // a fellow's book: the hooks' counters and your own records are never read from a fellow's copy
        {
            var f = Fellow("Edda"); f.Feats.Counts[FeatsCounters.HeldLineBest] = 40; f.DamageSinceInstall = new DamageTally();
            Check(PanelModel.FeatValue(PanelModel.FeatById("heldtheline"), f) == 0 && PanelModel.FeatValue(PanelModel.FeatById("ironhide"), f) == 0,
                  "a fellow's copy: their PC's counters are not read on your book (their earned tiers come with their ledger)");
        }

        // ---------- the new hooks' counters (plain rules, FeatsCounters) ----------
        {
            var l = new FeatsLedger(); FeatsCounters.GrillRun = 0;
            for (int k = 0; k < 3; k++) FeatsCounters.OnTakenOff(l, false);
            FeatsCounters.OnTakenOff(l, true);
            FeatsCounters.OnTakenOff(l, false); FeatsCounters.OnTakenOff(l, false);
            Check(l.Count(FeatsCounters.GrillBest) == 3 && FeatsCounters.GrillRun == 2, "grill: three unburnt, a burnt one ends the run, the best stays 3 (" + l.Count(FeatsCounters.GrillBest) + ")");
            FeatsCounters.OnTakenOff(null, false);
            Check(FeatsCounters.GrillRun == 2, "grill: no ledger (totals not loaded): nothing counted");
        }
        {
            var l = new FeatsLedger(); FeatsCounters.CrewStint = 0;
            FeatsCounters.OnCrewSample(l, true, 2, 10);
            Check(l.Count(FeatsCounters.CrewBest) == 0, "crew: two fellows aboard is not a whole crew");
            for (int k = 0; k < 4; k++) FeatsCounters.OnCrewSample(l, true, 3, 10);
            FeatsCounters.OnCrewSample(l, false, 4, 10);   // someone else took the helm
            FeatsCounters.OnCrewSample(l, true, 3, 10);
            Check(l.Count(FeatsCounters.CrewBest) == 40 && FeatsCounters.CrewStint == 10, "crew: forty seconds at the helm with three aboard; leaving the helm ends the stint, the best stays 40 (" + l.Count(FeatsCounters.CrewBest) + ")");
        }
        {
            var l = new FeatsLedger(); FeatsCounters.LineRun = 0; FeatsCounters.LineFell = false;
            for (int k = 0; k < 5; k++) FeatsCounters.OnBlock(l, false, false, true, 10);
            FeatsCounters.OnBlock(l, false, false, false, 10);   // no fellow near: not the line
            Check(l.Count(FeatsCounters.HeldLineBest) == 0 && FeatsCounters.LineRun == 5, "line: held blocks near a fellow count for the fight on now, not before it is over");
            FeatsCounters.OnFightOver(l);
            Check(l.Count(FeatsCounters.HeldLineBest) == 5 && FeatsCounters.LineRun == 0, "line: the fight over and you stood: five held");
            for (int k = 0; k < 9; k++) FeatsCounters.OnBlock(l, false, false, true, 10);
            FeatsCounters.OnFell(); FeatsCounters.OnFightOver(l);
            Check(l.Count(FeatsCounters.HeldLineBest) == 5 && !FeatsCounters.LineFell, "line: nine held but you fell in that fight: it does not count, the best stays 5");
            FeatsCounters.OnFell(); FeatsCounters.OnFightOver(l); FeatsCounters.OnBlock(l, false, false, true, 10); FeatsCounters.OnFightOver(l);
            Check(l.Count(FeatsCounters.HeldLineBest) == 5, "line: a fall between fights ends with that fight; one held block next fight does not lower the best");
        }
        {
            var l = new FeatsLedger();
            FeatsCounters.OnSummoned(l); FeatsCounters.OnSummoned(l); FeatsCounters.OnSummoned(null);
            Check(l.Count(FeatsCounters.Summons) == 2, "altar: each altar that answered counts once");
            FeatsCounters.OnRepaired(l, 0, 11); FeatsCounters.OnRepaired(l, 11, 11); FeatsCounters.OnRepaired(l, 22, 11); FeatsCounters.OnRepaired(l, 33, 11);
            Check(l.Count(FeatsCounters.RepairsOthers) == 2, "mender: a world piece (no builder) and your own do not count, a fellow's do (2)");
        }
        {
            var l = new FeatsLedger();
            var raid = FeatsCounters.RaidKey("army_eikthyr", 120.4f, -55.6f);
            Check(raid == "army_eikthyr|120|-56" && FeatsCounters.RaidKey(null, 0, 0) == null, "raid: one raid, one key (its name and centre)");
            Check(FeatsCounters.OnRaidHit(l, raid) && !FeatsCounters.OnRaidHit(l, raid) && FeatsCounters.OnRaidHit(l, FeatsCounters.RaidKey("foresttrolls", 3, 4)) && !FeatsCounters.OnRaidHit(l, null) && l.Count(FeatsCounters.Raids) == 2,
                  "raid: many hits in one raid count it once; the next raid counts again");
            var jb = new Json().Open(); l.WriteTo(jb); jb.Close();
            var back = FeatsLedger.ReadFrom(MiniJson.Obj(MiniJson.Parse(jb.ToString()) as Dictionary<string, object>, "feats"));
            Check(!FeatsCounters.OnRaidHit(back, FeatsCounters.RaidKey("foresttrolls", 3, 4)) && back.Count(FeatsCounters.Raids) == 2, "raid: after a restart in the same raid it is not counted again (its key is kept in the ledger)");
            Check(FeatsCounters.KeyHash("a") != 0 && FeatsCounters.KeyHash("a") == FeatsCounters.KeyHash("a") && FeatsCounters.KeyHash("a") != FeatsCounters.KeyHash("b"), "raid: the key's number is stable and never 0");
        }

        // ---------- the families, the counting dates, a land's own feat ----------
        Check(PanelModel.FeatFamilyName("delver") == "Delver" && PanelModel.FeatFamilyName("archer") == "Bowhand" && PanelModel.FeatFamilyName("mage") == "Staffbearer" && PanelModel.FeatFamilyName("fighter") == "Battlehand",
              "families: the new ones by name (Battlehand and the others from their titles; Delver, Bowhand and Staffbearer have no title)");
        Check(PanelModel.FeatIcon(PanelModel.FeatById("intodark")) == "vocab:find-dungeon" && PanelModel.FeatIcon(PanelModel.FeatById("longreach")).StartsWith("item:$item_bow") &&
              PanelModel.FeatIcon(PanelModel.FeatById("shoulder")) == "title:fighter",
              "families: a family with no title has a picture of its own (the dungeon find, a bow, a staff); the others their title's emblem");
        {
            var p = Me(); p.Starts = new Dictionary<string, DateTime> { [LocalTotals.StartFeats] = Now.AddDays(-9), [LocalTotals.StartFeats09] = Now.AddDays(-1) };
            Check(PanelModel.CountFrom(p, "heldtheline") == Now.AddDays(-1) && PanelModel.CountFrom(p, "raidbreaker") == Now.AddDays(-1) && PanelModel.CountFrom(p, "shieldwall") == Now.AddDays(-9) && PanelModel.CountFrom(p, "keptstanding") == p.InstalledUtc,
                  "dates: the new hooks' feats count from the day this version first ran on the PC, the older ones from their own start");
            Check(PanelModel.FeatHint(p, PanelModel.FeatById("heldtheline"), false).Contains(Now.AddDays(-1).ToLocalTime().ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture)),
                  "dates: an unsung hook feat says it is counting from that day: " + PanelModel.FeatHint(p, PanelModel.FeatById("heldtheline"), false));
            var lt = new LocalTotals(); lt.FillStarts(Now);
            Check(lt.Starts.TryGetValue(LocalTotals.StartFeats09, out var s09) && s09 == Now, "dates: the new counters' start is set on the first load that counts them");
        }
        {
            var p = Me(); p.KnownBiomes = new[] { "Meadows", "BlackForest" };
            var unsung = PanelModel.Build(p, new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = PanelModel.FeatsUnsungId } });
            var ids = PanelModel.VisibleFeats(unsung).Select(c => c.Id).ToList();
            Check(!ids.Contains("coldiron") && !ids.Contains("tappedroot") && ids.Contains("spiritdead") && !PanelModel.AllText(unsung).Any(t => t.Contains("Ashlands")),
                  "lands: a land's own feat (Cold Iron in the Ashes, Tapped the Root) waits off the page, unnamed, until the group found the land");
            p.KnownBiomes = new[] { "Meadows", "AshLands" };
            Check(PanelModel.VisibleFeats(PanelModel.Build(p, new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = PanelModel.FeatsUnsungId } })).Any(c => c.Id == "coldiron"),
                  "lands: the Ashlands found: Cold Iron in the Ashes shows");
        }
        // the Unsung grid holds every unearned feat: it scrolls, nothing is left out
        {
            var p = Me();
            var unsung = PanelModel.Build(p, new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = PanelModel.FeatsUnsungId } });
            var shown = PanelModel.FeatDefs.Count(d => !d.Group && PanelModel.OwnVisible(d, p));
            Check(PanelModel.VisibleFeats(unsung).Count == shown && shown > 40, "unsung: a new book lists all " + shown + " feats it can see (the grid scrolls; none left out)");
        }
        return fails;
    }
}
