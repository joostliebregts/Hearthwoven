// 0.9.1 feats, batch 2 (Chapters/FeatsMore2.cs, work/hearthwoven-0.9/FEAT-CATALOG-2.md): as MoreFeatsTests, each new feat's number from a record built
// by hand that also holds what it must not count, and its tiers at their lines (one step under: not earned; on it: earned). Then the batch's new
// counters (FeatsCounters: Through the Storm, One Long Road, Far Horizon, Saddled) as plain rules, the Armsman family, the counting dates and the
// ledger's room. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FeatsBatch2Tests
{
    static readonly DateTime Now = new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc);

    static PanelInput Me() => new PanelInput
    {
        PlayerName = "Rowan", PlayerId = 11, IsSelf = true, NowUtc = Now, InstalledUtc = Now.AddDays(-10), Events = new SessionEvents(), Feats = new FeatsLedger { Primed = true },
        Character = new Dictionary<string, float>(), Fellows = new List<PanelInput>(), PlayerNames = new Dictionary<long, string>(),
    };
    static PanelInput Fellow(string name) => new PanelInput { PlayerName = name, IsSelf = false, NowUtc = Now, Events = new SessionEvents(), Feats = new FeatsLedger(), Character = new Dictionary<string, float>() };
    static PanelInput WithFellow(PanelInput me, PanelInput f) { me.Fellows.Add(f); f.Fellows = new List<PanelInput> { me }; return me; }

    static BossAttempt Boss(BossOutcome outcome, int unnamed, params (string name, bool self, int blocks)[] players)
    {
        var a = new BossAttempt { Kind = "Eikthyr", Outcome = outcome, StartUtc = Now.AddHours(-1), Unnamed = unnamed };
        foreach (var p in players) a.Players.Add(new AttemptPlayer { Name = p.name, Self = p.self, Blocks = p.blocks });
        return a;
    }

    /// <summary>A weapon style's record: <c>v</c> dealt with the style's skill over two damage types, and 900 with another skill (not counted).</summary>
    static PanelInput Dealt(string skill, double v, string other = "Bows")
    {
        var p = Me(); p.DamageSinceInstall = new DamageTally();
        p.DamageSinceInstall.Dealt["Troll|" + skill + "|slash"] = (float)(v / 2); p.DamageSinceInstall.Dealt["Greydwarf|" + skill + "|fire"] = (float)(v / 2);
        p.DamageSinceInstall.Dealt["Troll|" + other + "|pierce"] = 900;
        return p;
    }

    static readonly string[] Trees = { "Beech1", "Birch2", "Oak1", "FirTree", "Pinetree_01", "SwampTree1", "YggaShoot1" };

    /// <summary>Each new personal feat: a record that holds exactly <c>v</c> of what it counts (plus something it must not count), and the step its number moves by.</summary>
    public static readonly (string id, Func<double, PanelInput> make, double step)[] Personal =
    {
        ("everydraught", v =>
        {
            var p = Me(); p.ItemsCrafted = new Dictionary<string, float> { ["$item_bread"] = 4 };
            for (int k = 0; k < (int)v; k++) p.ItemsCrafted["$item_meadbase" + k] = 2;
            if (v >= 1) p.Events.Made["$item_meadbase0"] = 3;   // the same kind brewed again: once
            return p;
        }, 1),
        ("everyfeast", v =>
        {
            var p = Me();
            for (int k = 0; k < (int)v; k++) { p.Events.SetOutFeastMadeBy["Rowan|piece_feast" + k + "|1:" + k] = 1; p.Events.SetOutFeastMadeBy["Edda|piece_feast" + k + "|2:" + k] = 1; }   // each kind set out twice
            return p;
        }, 1),
        ("crewmate", v =>
        {
            var p = Me(); p.Events.SailedUnderHelmOf["Rowan"] = 300;   // your own name (a reload under your own helm): not a fellow
            for (int k = 0; k < (int)v; k++) p.Events.SailedUnderHelmOf["Edda" + k] = 60;
            return p;
        }, 1),
        ("throughstorm", v => { var p = Me(); p.Feats.Counts[FeatsCounters.StormBest] = v * 60; p.Feats.Counts[FeatsCounters.CrewBest] = 9000; return p; }, 1 / 6.0),
        ("beforeforsaken", v =>
        {
            var p = Me(); p.BossLog = new BossLog();
            p.BossLog.Attempts.Add(Boss(BossOutcome.Won, 0, ("Rowan", true, (int)v), ("Edda", false, 99)));   // Edda's blocks are hers
            p.BossLog.Attempts.Add(Boss(BossOutcome.Reset, 0, ("Rowan", true, (int)v + 40)));               // not won: not counted
            return p;
        }, 1),
        ("oldcompanion", v =>
        {
            var p = Me(); p.DealtArmourSince = new ArmourTally();
            p.DealtArmourSince.AddWeapon("SwordIron", 2, "Swords", 20, 0, 0, (float)(v / 2)); p.DealtArmourSince.AddWeapon("SwordIron", 3, "Swords", 22, 0, 0, (float)(v / 2));   // one sword, two upgrades
            p.DealtArmourSince.AddWeapon("AxeBronze", 1, "Axes", 10, 0, 0, (float)(v * 0.6));            // another weapon: less
            p.DealtArmourSince.AddWeapon(WeaponCells.Fists, 0, "Unarmed", 5, 0, 0, (float)(v * 3 + 50)); // bare hands are no weapon here
            p.DealtArmourSince.AddWeapon(null, 0, "Spears", 5, 0, 0, (float)(v * 3 + 50));               // a hit not tied to a weapon
            return p;
        }, 1),
        ("axes", v => Dealt("Axes", v), 2), ("maces", v => Dealt("Clubs", v), 2), ("spears", v => Dealt("Spears", v), 2), ("polearms", v => Dealt("Polearms", v), 2),
        ("knives", v => Dealt("Knives", v), 2), ("fists", v => Dealt("Unarmed", v), 2), ("swords", v => Dealt("Swords", v, "Axes"), 2),
        ("heavybolt", v => Dealt("Crossbows", v), 2),
        ("raisedead", v => { var p = Me(); p.Character["SkeletonSummons"] = (float)v; p.Character["ArrowsShot"] = 99; return p; }, 1),
        ("forsakengifts", v =>
        {
            var p = Me(); var powers = new[] { "UsePowerEikthyr", "UsePowerElder", "UsePowerBonemass", "UsePowerModer", "UsePowerYagluth", "UsePowerQueen", "UsePowerAshlands" };
            for (int k = 0; k < (int)v; k++) p.Character[powers[k]] = 3;
            p.Character["SetPowerAshlands"] = 1; p.Character["UsePowerDeepNorth"] = 0;   // set at a stone, not used: not counted
            return p;
        }, 1),
        ("wholehall", v =>
        {
            var p = Me(); p.BossLog = new BossLog();
            p.BossLog.Attempts.Add(Boss(BossOutcome.Reset, 6, ("Rowan", true, 0), ("Edda", false, 0)));   // eight in it, not won
            if (v >= 1)
            {
                var named = new[] { ("Rowan", true, 0), ("Edda", false, 0), ("Tor", false, 0) }.Take(Math.Min(3, (int)v)).ToArray();
                p.BossLog.Attempts.Add(Boss(BossOutcome.Won, (int)v - named.Length, named));   // the others counted, never named
            }
            return p;
        }, 1),
        ("longroad", v => { var p = Me(); p.Feats.Counts[FeatsCounters.LandsBest] = v; return p; }, 1),
        ("farhorizon", v => { var p = Me(); p.Feats.Counts[FeatsCounters.FarBest] = v * 1000; return p; }, 0.1),
        ("dayfirsts", v =>
        {
            var p = Me(); p.FirstsFrom = Now.AddDays(-9); p.Firsts = new Dictionary<string, DateTime>();
            var day = Now.Date.AddDays(-2);
            for (int k = 0; k < (int)v; k++) p.Firsts["$item_new" + k] = day;
            for (int k = 0; k < (int)v - 1; k++) p.Firsts["$piece_other" + k] = day.AddDays(1);   // the day after: one fewer
            return p;
        }, 1),
        ("festive", v => { var p = Me(); p.Character["BuildClusterSeasonal"] = (float)v; p.Character["BuildClusterDefense"] = 99; return p; }, 1),
        ("shipwright", v => { var p = Me(); p.Events.Repairs["Karve"] = (float)(v - Math.Floor(v / 2)); p.Events.Repairs["Cart"] = (float)Math.Floor(v / 2); p.Events.Repairs["woodwall"] = 50; return p; }, 1),
        ("snare", v => { var p = Me(); p.Character["TrapArmed"] = (float)v; p.Character["TrapTriggered"] = 99; return p; }, 1),
        ("everygrove", v =>
        {
            var p = Me();
            for (int k = 0; k < (int)v; k++) p.Events.Felled[Trees[k]] = 3;
            if (v >= 1) p.Events.Felled["Beech_small"] = 2;   // a beech of another size: the same kind
            return p;
        }, 1),
        ("stonewalls", v => { var p = Me(); p.ItemsPickedUp = new Dictionary<string, float> { ["$item_stone"] = (float)(v * 0.5), ["$item_blackmarble"] = (float)(v * 0.25), ["$item_grausten"] = (float)(v * 0.25), ["$item_copperore"] = 900, ["$item_wood"] = 70 }; return p; }, 1),
        ("saddled", v => { var p = Me(); p.Feats.Counts[FeatsCounters.Ridden] = v * 1000; return p; }, 0.1),
        ("fishtotell", v =>
        {
            var p = Me(); p.Character["FishCaught"] = 99; p.Character["FishCaughtTier1"] = 50;   // fish with no star
            if (v >= 1) { p.Character["FishCaughtTier" + ((int)v + 1)] = 2; p.Character["FishCaughtTier2"] = 9; }
            return p;
        }, 1),
        ("fedbymany", v =>
        {
            var p = Me(); p.Events.AteFoodMadeBy["Rowan|Bread"] = 9;   // your own cooking
            for (int k = 0; k < (int)v; k++) { p.Events.AteFoodMadeBy["Edda" + k + "|Bread"] = 2; p.Events.AteFoodMadeBy["Edda" + k + "|Fish"] = 1; }
            return p;
        }, 1),
        ("wornwork", v =>
        {
            var p = Me(); p.Events.EquippedGearMadeBy["Rowan|AxeBronze"] = 5;
            for (int k = 0; k < (int)v; k++) { p.Events.EquippedGearMadeBy["Tor" + k + "|AxeBronze"] = 1; p.Events.EquippedGearMadeBy["Tor" + k + "|ShieldWood"] = 2; }
            return p;
        }, 1),
    };
    public static readonly string[] GroupIds = { "onecrew" };

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- every new feat: its number from the record, its tiers at their lines ----------
        foreach (var t in Personal)
        {
            var d = PanelModel.FeatById(t.id);
            if (d == null) { Check(false, "feats batch 2: " + t.id + " is in the table"); continue; }
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
            Check(bad.Count == 0, "feats batch 2: " + d.Name + " (" + d.Family + ") counts only its record, tiers " + string.Join(" / ", d.Tiers.Select(PanelModel.Number)) + " at their lines" + (bad.Count > 0 ? ": " + string.Join("; ", bad) : ""));
        }
        Check(Personal.All(p => PanelModel.FeatById(p.id) is FeatDef d && !d.Group) && GroupIds.All(id => PanelModel.FeatById(id)?.Group == true) && Personal.Length + GroupIds.Length == 30,
              "feats batch 2: " + Personal.Length + " personal and " + GroupIds.Length + " group feat added, each tested here (" + PanelModel.FeatDefs.Length + " in all)");

        // the room: the ledger keeps every feat with room to grow, and every counter the hooks keep
        var counters = new[] { FeatsCounters.BlocksNear, FeatsCounters.Stopped, FeatsCounters.BestStreak, FeatsCounters.BossParries, FeatsCounters.GrillBest, FeatsCounters.CrewBest, FeatsCounters.HeldLineBest,
                               FeatsCounters.Summons, FeatsCounters.RepairsOthers, FeatsCounters.Raids, FeatsCounters.RaidLast, FeatsCounters.StormBest, FeatsCounters.LandsBest, FeatsCounters.FarBest,
                               FeatsCounters.Ridden, CargoVoyage.BestKey, LedTracker.BestKey };
        Check(PanelModel.FeatDefs.Length + 16 <= FeatsLedger.MaxFeats && counters.Distinct().Count() == counters.Length && counters.Length + 8 <= FeatsLedger.MaxCounts,
              "feats batch 2: the ledger has room: " + PanelModel.FeatDefs.Length + " of " + FeatsLedger.MaxFeats + " feats, " + counters.Length + " of " + FeatsLedger.MaxCounts + " counters, each with 8 or more spare");
        {
            var l = new FeatsLedger(); foreach (var c in counters) l.Add(c, 5);
            var j = new Json().Open(); l.WriteTo(j); j.Close();
            var back = FeatsLedger.ReadFrom(MiniJson.Obj(MiniJson.Parse(j.ToString()) as Dictionary<string, object>, "feats"));
            Check(counters.All(c => back.Count(c) == 5), "feats batch 2: every counter survives the ledger's save and load");
        }

        // a fellow's copy: the hooks' counters, your boss fights and your weapons are yours only
        {
            var f = Fellow("Edda"); f.Feats.Counts[FeatsCounters.StormBest] = 900; f.Feats.Counts[FeatsCounters.Ridden] = 30000;
            f.BossLog = new BossLog(); f.BossLog.Attempts.Add(Boss(BossOutcome.Won, 9, ("Edda", true, 99)));
            Check(new[] { "throughstorm", "saddled", "beforeforsaken", "wholehall" }.All(id => PanelModel.FeatValue(PanelModel.FeatById(id), f) == 0),
                  "a fellow's copy: their PC's counters and boss fights are not read on your book (their earned tiers come with their ledger)");
        }

        // One Crew: every member who shares sailed under another's helm; two or more members
        {
            var d = PanelModel.FeatById("onecrew");
            var me = Me(); var edda = Fellow("Edda"); WithFellow(me, edda);
            me.Events.SailedUnderHelmOf["Edda"] = 120;
            Check(PanelModel.FeatValue(d, me) == 0, "one crew: one of two under the other's helm is not yet");
            edda.Events.SailedUnderHelmOf["Edda"] = 500;
            Check(PanelModel.FeatValue(d, me) == 0, "one crew: under one's own name does not count");
            edda.Events.SailedUnderHelmOf["Tor"] = 30;
            Check(PanelModel.FeatValue(d, me) == 1 && PanelModel.FeatTierOf(d, 1) == 1, "one crew: both under another's helm (one who does not share counts as the helm): earned");
            var alone = Me(); alone.Events.SailedUnderHelmOf["Edda"] = 400;
            Check(PanelModel.FeatValue(d, alone) == 0, "one crew: one player alone never earns it (it needs two who share)");
            var together = PanelModel.Build(me, new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = PanelModel.FeatsTogetherId } });
            Check(PanelModel.VisibleFeats(together).Any(c => c.Id == "onecrew"), "one crew: on the Together page");
        }

        // ---------- the batch's counters (plain rules, FeatsCounters) ----------
        {
            var l = new FeatsLedger(); FeatsCounters.NewSession();
            Check(FeatsCounters.IsStorm("ThunderStorm") && FeatsCounters.IsStorm("Ashlands_storm") && FeatsCounters.IsStorm("Mistlands_thunder") && !FeatsCounters.IsStorm("Rain") && !FeatsCounters.IsStorm(null),
                  "storm: the game's weather by its own name (thunder or storm), rain is not a storm");
            FeatsCounters.OnStormSample(l, false, true, 10);   // a passenger in the storm
            FeatsCounters.OnStormSample(l, true, false, 10);   // at the helm in clear weather
            Check(l.Count(FeatsCounters.StormBest) == 0, "storm: a passenger, or the helm in clear weather, is no storm stint");
            for (int k = 0; k < 6; k++) FeatsCounters.OnStormSample(l, true, true, 10);
            FeatsCounters.OnStormSample(l, true, false, 10);   // the storm passed
            FeatsCounters.OnStormSample(l, true, true, 10);
            Check(l.Count(FeatsCounters.StormBest) == 60 && FeatsCounters.StormStint == 10, "storm: a minute at the helm through it; when it passes the stint ends, the best stays 60 s (" + l.Count(FeatsCounters.StormBest) + ")");
            FeatsCounters.OnStormSample(null, true, true, 10);
            Check(FeatsCounters.StormStint == 10, "storm: no ledger (totals not loaded): nothing counted");
        }
        {
            var l = new FeatsLedger(); FeatsCounters.NewSession();
            foreach (var b in new[] { "Meadows", "BlackForest", "Meadows", "Ocean", "None", null, "Swamp" }) FeatsCounters.OnLand(l, b);
            Check(l.Count(FeatsCounters.LandsBest) == 3, "lands: Meadows, the Black Forest and the Swamp in one session; the sea, no land and a land walked again add nothing (" + l.Count(FeatsCounters.LandsBest) + ")");
            FeatsCounters.NewSession(); FeatsCounters.OnLand(l, "Plains"); FeatsCounters.OnLand(l, "Mountain");
            Check(l.Count(FeatsCounters.LandsBest) == 3 && FeatsCounters.SessionLands.Count == 2, "lands: a new session starts again; two lands in it do not lower the best of three");
            FeatsCounters.Streak = 7; FeatsCounters.StormStint = 30; FeatsCounters.NewSession();
            Check(FeatsCounters.Streak == 0 && FeatsCounters.StormStint == 0 && FeatsCounters.SessionLands.Count == 0, "session: a new connection starts the parry streak, the storm stint and the lands again");
        }
        {
            var l = new FeatsLedger();
            FeatsCounters.OnFar(l, 3000, 4000); FeatsCounters.OnFar(l, -100, 200); FeatsCounters.OnFar(l, 20000, 0); FeatsCounters.OnFar(l, float.NaN, 0);
            Check(l.Count(FeatsCounters.FarBest) == 5000, "far: 5 km from the middle (3 km east, 4 km north); a nearer look keeps the best, a reading past any world's edge is left out");
            FeatsCounters.OnRide(l, 8.5f); FeatsCounters.OnRide(l, 0); FeatsCounters.OnRide(l, 600); FeatsCounters.OnRide(l, 11.5f); FeatsCounters.OnRide(null, 50);
            Check(Math.Abs(l.Count(FeatsCounters.Ridden) - 20) < 1e-6, "saddle: 20 m ridden; a jump of 600 m (a teleport, a reload) is no ride");
        }

        // ---------- the family, the pictures, the counting dates ----------
        Check(PanelModel.FeatFamilyName("melee") == "Armsman" && PanelModel.FeatFamily("melee").colour == "#b8befa" &&
              new[] { "axes", "maces", "spears", "polearms", "knives", "fists", "swords" }.All(id => PanelModel.FeatById(id).Family == "melee" && PanelModel.FeatIcon(PanelModel.FeatById(id)).StartsWith("item:$item_")),
              "families: the weapon styles are the Armsman's (no title of its own), each with its weapon's picture");
        {
            var p = Me(); p.Starts = new Dictionary<string, DateTime> { [LocalTotals.StartFeats09] = Now.AddDays(-3), [LocalTotals.StartFeats091] = Now.AddDays(-1) };
            p.SwingFromUtc = Now.AddDays(-6); p.FirstsFrom = Now.AddDays(-5);
            Check(new[] { "throughstorm", "longroad", "farhorizon", "saddled" }.All(id => PanelModel.CountFrom(p, id) == Now.AddDays(-1)) && PanelModel.CountFrom(p, "steadygrill") == Now.AddDays(-3) &&
                  PanelModel.CountFrom(p, "oldcompanion") == Now.AddDays(-6) && PanelModel.CountFrom(p, "dayfirsts") == Now.AddDays(-5) && PanelModel.CountFrom(p, "axes") == p.InstalledUtc,
                  "dates: the batch's new counters count from the day this version first ran, one weapon from the weapons record, a day of firsts from the firsts record, the rest from the install");
            Check(PanelModel.FeatHint(p, PanelModel.FeatById("saddled"), false).Contains(Now.AddDays(-1).ToLocalTime().ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture)),
                  "dates: an unsung batch-2 hook feat says it is counting from that day: " + PanelModel.FeatHint(p, PanelModel.FeatById("saddled"), false));
            var lt = new LocalTotals(); lt.FillStarts(Now);
            Check(lt.Starts.TryGetValue(LocalTotals.StartFeats091, out var s) && s == Now, "dates: the batch's counters' start is set on the first load that counts them");
        }
        // a fellow's book says the rule for them
        {
            var f = Fellow("Edda"); f.Events.AteFoodMadeBy["Tor|Bread"] = 2;
            var d = PanelModel.FeatById("fedbymany");
            Check(PanelModel.FeatWords(f, d.Brief(4)) == "4 cooks fed them" && PanelModel.FeatWords(Me(), d.Brief(4)) == "4 cooks fed you", "words: a rule said for whose book it is (fed you, fed them)");
        }
        return fails;
    }
}
