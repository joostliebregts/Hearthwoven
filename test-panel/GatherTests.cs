// What counts as wood and as mining (B12, Joost's 0.5 test: Coal and Iron showed in "Stone and ore brought in"). The rule
// reads components and damage modifiers (PanelModel.GatherKindOf); GameData also keeps everything a smelter makes out.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class GatherTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        Check(PanelModel.GatherKindOf("TreeLog", "Normal", "Immune") == "wood", "gather: a felled log's drops are wood");
        Check(PanelModel.GatherKindOf("TreeBase", "Normal", "Immune") == null, "gather: a standing tree's own drops (resin, cones) are not wood");
        Check(PanelModel.GatherKindOf("MineRock5", "Immune", "Normal") == "mining" && PanelModel.GatherKindOf("MineRock", "Ignore", "Weak") == "mining" &&
              PanelModel.GatherKindOf("Destructible", "Immune", "Normal") == "mining", "gather: a pickaxe-only rock, ore vein or pile (muddy scrap pile, Leviathan) is mining");
        Check(PanelModel.GatherKindOf("Destructible", "Normal", "Normal") == null && PanelModel.GatherKindOf("MineRock", "Normal", "Immune") == null,
              "gather: what any tool breaks (a crate, a pot) or what a pickaxe cannot touch is not mining");
        // 0.6.5 (a real character: Leather Scraps 123 and Withered Bone on the Mining bar, beside 5 Scrap Iron): the game data's
        // rule. Coal is smelter output; Leather Scraps a pile drops but boars drop too, and a pickup never says where it lay, so
        // they are not mining; Withered Bone only piles drop and no smelter takes, so a find of its own; a mod's ore a smelter takes is ore
        var known = new Dictionary<string, string>
        {
            ["$item_coal"] = PanelModel.GatheredItemKind("mining", refined: true, creatureDrops: false, smelted: false),
            ["$item_leatherscraps"] = PanelModel.GatheredItemKind("mining", refined: false, creatureDrops: true, smelted: false),
            ["$item_witheredbone"] = PanelModel.GatheredItemKind("mining", refined: false, creatureDrops: false, smelted: false),
            ["$item_modore"] = PanelModel.GatheredItemKind("mining", refined: false, creatureDrops: false, smelted: true),
            ["$item_wood"] = "wood",
        };
        var input = new PanelInput
        {
            IsSelf = true, Events = new SessionEvents(),
            ItemsPickedUp = new Dictionary<string, float> { ["$item_stone"] = 40, ["$item_coal"] = 1161, ["$item_iron"] = 539, ["$item_leatherscraps"] = 123, ["$item_witheredbone"] = 1, ["$item_modore"] = 5, ["$item_wood"] = 9 },
            GatherKind = t => known.TryGetValue(t, out var k) ? k : null,
        };
        var page = PanelModel.Build(input, new PanelState { Page = { [Chapter.Deeds] = "mining" } });
        var bars = PanelModel.Content(page).Where(b => b.Kind == "composition").ToList();
        Check(bars.Count == 2 && bars[0].Items.Select(i => i.Id).OrderBy(k => k).SequenceEqual(new[] { "$item_modore", "$item_stone" }) &&
              bars[1].Title == PanelModel.PickaxeFindsTitle && bars[1].Value == "1" && bars[1].Tone == "single" && bars[1].Items.Single().Id == "$item_witheredbone" &&
              PanelModel.Content(page).First(b => b.Kind == "hero").Value == "45" && !PanelModel.AllText(page).Any(t => t.Contains("Leather") || t.Contains("Coal")),
              "gather: Mining's bar and total are stone and ore (a mod's ore too), Withered Bone from scrap piles sits under it as an other pickaxe find, Leather Scraps (boars drop them too) and Coal are left out");
        // groundwork: raise and lower mirror each other; a tool's variants share their base's pattern, lighter
        var raise = PanelModel.GroundOf("$piece_raise"); var lower = PanelModel.GroundOf("$piece_lowerground"); var precise = PanelModel.GroundOf("$piece_raise_precision");
        Check(raise.pattern == "vocab:grain-ground-raise" && lower.pattern == "vocab:grain-ground-lower" && precise.pattern == raise.pattern && precise.colour != raise.colour &&
              PanelModel.GroundOf("$piece_levelground_square").pattern == "vocab:grain-ground-level" && PanelModel.GroundOf("$piece_pavedroad_path").pattern == "vocab:grain-ground-paved" &&
              PanelModel.GroundOf("$piece_cultivate_square").pattern == "vocab:grain-ground-cultivate" && PanelModel.GroundOf("$piece_woodwall").pattern == null,
              "groundwork: raise ^^^ and lower vvv; precision, square and path variants share their base's pattern, a lighter tint");
        return fails;
    }
}
