// Mining's "Pickaxe hits per rock" (0.8.2): Joost's own list in game read "Mine Rock Tin", "Rock", "Blueberry Bush", "Rock4 forest",
// "Rock4 forest frac" and "Stubbe", the last two without a picture. The rocks now carry the game's own names, a rock's split pieces count
// as the rock, rocks of one name are one row, and what is no rock (a stump, a berry bush) is one quiet last row. The scenario builder
// (Scenario) is also the preview's (Program.cs --dump). Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RocksTests
{
    // The prefabs as GameData reads them in the game (their components per Valheim's prefab list): MineRock_Tin a Destructible named only by
    // its HoverText ($piece_deposit_tin) that drops tin ore; Rock_4 a Destructible that drops stone; rock4_forest a Destructible that drops
    // nothing and breaks into rock4_forest_frac, a MineRock5 without a name; stubbe a Destructible that drops wood; BlueberryBush a
    // Destructible whose Pickable yields blueberries. Numbers are the sample's, not anyone's real ones.
    static readonly Dictionary<string, (string component, bool dropsMining, string split, string drop, string name)> Game = new Dictionary<string, (string, bool, string, string, string)>
    {
        ["MineRock_Tin"] = ("Destructible", true, null, "TinOre", "Tin Deposit"),
        ["Rock_4"] = ("Destructible", true, null, "Stone", null),
        ["rock4_forest"] = ("Destructible", false, "rock4_forest_frac", null, null),
        ["rock4_forest_frac"] = ("MineRock5", true, null, "Stone", null),
        ["stubbe"] = ("Destructible", false, null, "Wood", null),
        ["BlueberryBush"] = ("Destructible", false, null, null, "Blueberries"),
    };
    public static readonly (string prefab, float hits)[] Hits =
        { ("MineRock_Tin", 58), ("Rock_4", 12), ("rock4_forest", 1), ("rock4_forest_frac", 9), ("BlueberryBush", 2), ("stubbe", 3) };

    /// <summary>As GameData.Rocks: a split piece's rock is the one that breaks into it; mined by PanelModel.RockMined on the rock's own
    /// components or on the piece it breaks into; the rock's drop first.</summary>
    public static PanelModel.RockData RockOf(string prefab)
    {
        if (!Game.ContainsKey(prefab)) return null;
        var whole = Game.FirstOrDefault(g => g.Value.split == prefab).Key ?? prefab;
        var top = Game[whole];
        bool Own(string p) => Game.TryGetValue(p ?? "", out var g) && PanelModel.RockMined(g.component, "Normal", g.dropsMining, false);
        return new PanelModel.RockData { Whole = whole, Mined = Own(whole) || PanelModel.RockMined(top.component, "Normal", false, Own(top.split)), Drop = top.drop ?? (top.split != null ? Game[top.split].drop : null) };
    }

    /// <summary>The sample world (PanelSample.Full) with these pickaxe hits for Rowan, read with the game's names and rock data.</summary>
    public static PanelInput Scenario(DateTime now)
    {
        var w = PanelSample.Full(now);
        Hit(w);
        return w;
    }

    /// <summary>The sample world with Mining's hero as Joost's 0.8.2 build drew it badly (0.8.2): a four-digit stone and ore count, a
    /// two-digit pickaxe hits count with its date, the Pickaxes skill and its growth line beside them. Sample numbers.</summary>
    public static PanelInput HeroScenario(DateTime now)
    {
        var w = PanelSample.Full(now);
        w.Events.PickaxeHits.Clear();
        SessionEvents.Add(w.Events.PickaxeHits, "rock4_copper", 47);
        return w;
    }

    static void Hit(PanelInput w)
    {
        w.Events.PickaxeHits.Clear();
        foreach (var (p, n) in Hits) SessionEvents.Add(w.Events.PickaxeHits, p, n);
        var names = w.DisplayName;
        w.DisplayName = t => t != null && Game.TryGetValue(t, out var g) ? g.name : names?.Invoke(t);
        w.Rock = RockOf;
    }

    static Block Rows(PanelInput input) =>
        PanelModel.Content(PanelModel.Build(input, new PanelState { Page = { [Chapter.Deeds] = "mining" } })).SkipWhile(b => !(b.Kind == "section" && b.Title == "Pickaxe hits per rock")).Skip(1).FirstOrDefault();

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        Check(PanelModel.RockMined("MineRock5", "Normal", false, false) && PanelModel.RockMined("Destructible", "Normal", true, false) && PanelModel.RockMined("Destructible", "Normal", false, true) &&
              !PanelModel.RockMined("Destructible", "Normal", false, false) && !PanelModel.RockMined("MineRock", "Immune", true, false),
              "rocks: mined is a rock or vein the pickaxe works on, or a Destructible that drops stone or ore or breaks into a rock; a stump or bush (drops wood, berries) is not");
        Check(PanelModel.WholeRockByName("rock4_forest_frac") == "rock4_forest" && PanelModel.WholeRockByName("giant_helmet1_destruction") == "giant_helmet1" &&
              PanelModel.WholeRockByName("TrollFrost_Frac_arm") == "TrollFrost" && PanelModel.WholeRockByName("Rock_4") == "Rock_4",
              "rocks: without game data a split piece's rock is its name without _frac, _fractured or _destruction");

        var game = Sample(now); Hit(game);
        var rows = Rows(game);
        var hero = PanelModel.Content(PanelModel.Build(game, new PanelState { Page = { [Chapter.Deeds] = "mining" } })).First(b => b.Kind == "hero");
        var heroHits = new[] { hero }.Concat(hero.Items ?? new List<Block>()).FirstOrDefault(n => (n.Title ?? "").Contains("pickaxe hit"));
        Check(rows != null && rows.Kind == "ranking" && rows.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Tin Deposit=58", "Rock=22", "Other things hit=5" }),
              "rocks: the game's own names (Tin Deposit), a split rock and its pieces with the plain rocks as one Rock row (12 + 1 + 9), the stump and the bush as one last row");
        Check(rows != null && rows.Items.Select(i => i.Icon).SequenceEqual(new[] { "item:TinOre|vocab:rock-tin", "item:Stone|vocab:rock-stone", PanelModel.OtherThingsHitIcon }) &&
              rows.Items.Last().Id == PanelModel.FoldId && rows.Items.Last().Colour == PanelModel.BarOtherColour && rows.Items.Take(2).All(i => i.Colour == null),
              "rocks: every row has a picture (the ore's or stone's icon; the Miner's emblem on the quiet gray last row, as Farming's Other plants)");
        Check(heroHits?.Value == "85" && rows != null && rows.Items.Sum(i => double.Parse(i.Value)) == 85,
              "rocks: the rows add up to the pickaxe hits beside the hero (85)");
        var text = PanelModel.AllText(PanelModel.Build(game, new PanelState { Page = { [Chapter.Deeds] = "mining" } })).ToList();
        Check(!text.Any(t => t.Contains("Mine Rock") || t.Contains("Rock4") || t.Contains("frac") || t.Contains("Stubbe") || t.Contains("Blueberr")),
              "rocks: no prefab name on the page (Mine Rock Tin, Rock4 forest frac, Stubbe), and the bush is not listed as a rock");

        // the same hits without the game's data (the tests, a removed mod): the names by the words in the prefabs, the same grouping
        var bare = Sample(now); Hit(bare); bare.Rock = null; bare.DisplayName = null;
        var bareRows = Rows(bare);
        Check(bareRows != null && bareRows.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Tin Deposit=58", "Rock=22", "Other things hit=5" }),
              "rocks: without game data the same rows, named by the vanilla deposit's name or Rock");
        var mod = Sample(now); mod.Events.PickaxeHits.Clear(); mod.DisplayName = null;
        SessionEvents.Add(mod.Events.PickaxeHits, "MineRock_Meteorite", 4); SessionEvents.Add(mod.Events.PickaxeHits, "ModVein2", 6);
        mod.Rock = p => p == "ModVein2" ? new PanelModel.RockData { Whole = p, Mined = true, Drop = "ModOre" } : null;
        var modRows = Rows(mod);
        Check(modRows != null && modRows.Items.Select(i => i.Title + "|" + i.Icon).SequenceEqual(new[] { "Rock|item:ModOre|vocab:rock-stone", "Meteorite|item:FlametalOre|vocab:rock-stone" }),
              "rocks: a mined thing without a name or picture of its own: Rock with the game's icon of what it drops; a vanilla prefab cleaned (Meteorite)");
        return fails;
    }

    static PanelInput Sample(DateTime now) => new PanelInput { IsSelf = true, PlayerName = "Rowan", NowUtc = now, Events = new SessionEvents(), Character = new Dictionary<string, float>() };
}
