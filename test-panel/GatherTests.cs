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
        // the small fixed set the sample uses: the game data says Coal and Iron are not gathered (smelter output)
        var known = new Dictionary<string, string> { ["$item_stone"] = "mining", ["$item_leatherscraps"] = "mining", ["$item_wood"] = "wood" };
        var input = new PanelInput
        {
            ItemsPickedUp = new Dictionary<string, float> { ["$item_stone"] = 40, ["$item_coal"] = 1161, ["$item_iron"] = 539, ["$item_leatherscraps"] = 12, ["$item_wood"] = 9 },
            GatherKind = t => known.TryGetValue(t, out var k) ? k : null,
        };
        var mining = PanelModel.PickedUp(input, "mining");
        Check(mining.Keys.OrderBy(k => k).SequenceEqual(new[] { "$item_leatherscraps", "$item_stone" }) && !PanelModel.PickedUp(input, "wood").ContainsKey("$item_coal"),
              "gather: Mining counts stone and what the piles drop, never Coal or Iron (refined at a smelter)");
        // groundwork: raise and lower mirror each other; a tool's variants share their base's pattern, lighter
        var raise = PanelModel.GroundOf("$piece_raise"); var lower = PanelModel.GroundOf("$piece_lowerground"); var precise = PanelModel.GroundOf("$piece_raise_precision");
        Check(raise.pattern == "vocab:grain-ground-raise" && lower.pattern == "vocab:grain-ground-lower" && precise.pattern == raise.pattern && precise.colour != raise.colour &&
              PanelModel.GroundOf("$piece_levelground_square").pattern == "vocab:grain-ground-level" && PanelModel.GroundOf("$piece_pavedroad_path").pattern == "vocab:grain-ground-paved" &&
              PanelModel.GroundOf("$piece_cultivate_square").pattern == "vocab:grain-ground-cultivate" && PanelModel.GroundOf("$piece_woodwall").pattern == null,
              "groundwork: raise ^^^ and lower vvv; precision, square and path variants share their base's pattern, a lighter tint");
        return fails;
    }
}
