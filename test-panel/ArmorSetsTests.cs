// Tests for Deeds > Crafting > Armor sets (src/Panel/Chapters/ArmorSets.cs, 0.9): the grouping from the game's data (vanilla-like and a mod's set),
// the lands, the names, the page in its states (yours, a land in full, nothing made, a fellow's book, Everyone), the feat's number, the shared
// yes/no per armor piece in the copy, the dev dump. On the sample armor (src/Panel/PanelSampleSets.cs). Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class ArmorSetsTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static ArmorPiece P(string token, string slot, string station, int level, string set, params (string token, string name, int n, bool refined)[] cost) =>
        new ArmorPiece { Token = token, Name = token, Slot = slot, Station = station, StationName = station, StationLevel = level, SetName = set ?? "", HasRecipe = true,
                         Recipe = cost.Select(c => new ArmorCost { Token = c.token, Name = c.name, Amount = c.n, Refined = c.refined }).ToList() };

    static PanelState Sets(bool everyone = false)
    {
        var s = new PanelState { Chapter = Chapter.Deeds, Everyone = everyone }; s.Page[Chapter.Deeds] = PanelModel.SetsPageId;
        return s;
    }
    static List<Block> Bands(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "itemgrid" && b.Tone == PanelModel.SetBandTone).ToList();
    static Block Band(PanelView v, string name) => Bands(v).FirstOrDefault(b => b.Title == name);
    static IEnumerable<Block> Each(Block b) => new[] { b }.Concat((b.Items ?? new List<Block>()).SelectMany(Each));

    public static int Run(DateTime now)
    {
        fails = 0;

        // ---- the grouping on the vanilla-like sample: the game's set names first, else main ingredient + station; the station level no part of it ----
        var sets = PanelModel.ArmorSetsOf(SetsSample.Pieces);
        string Of(string token) { var s = sets.FirstOrDefault(x => x.Pieces.Any(p => p.Token == token)); return s == null ? null : s.Name + "@" + PanelModel.LandName(s.Land) + "#" + s.Pieces.Count; }
        Check(Of("$item_helmet_leather") == "Deerstalker's Garb@Meadows#4" && Of("$item_cape_deerhide") == Of("$item_helmet_leather") && sets.First(s => s.Name == "Deerstalker's Garb").Material == "deer hide",
              "sets: the leather pieces and the Deer Hide Cape are one set in the Meadows, Hearthwoven's name with the material small beside it (" + Of("$item_helmet_leather") + ")");
        Check(Of("$item_chest_rags") == "Leather Scraps@Meadows#2", "sets: the rags are a set of their own (another main ingredient), named by their material (" + Of("$item_chest_rags") + ")");
        Check(Of("$item_helmet_bronze") == "Bronze-Clad@Black Forest#3" && Of("$item_cape_trollhide") == "Troll@Black Forest#4" && sets.First(s => s.Name == "Troll").GameNamed,
              "sets: Bronze-Clad and the game's own Troll set (its name, no material beside it) in the Black Forest");
        Check(Of("$item_helmet_iron") == "Cryptbreaker's Mail@Swamp#3", "sets: the Iron Helmet (forge 1) and the Scale Mail (forge 2) one set: the station level does not split it (" + Of("$item_helmet_iron") + ")");
        Check(Of("$item_cape_wolf") == "Frostfang@Mountains#4" && Of("$item_helmet_drake") == Of("$item_chest_wolf"), "sets: the Drake Helmet with the wolf hide, and the Wolf Fur Cape (another station) joins them by its material (" + Of("$item_cape_wolf") + ")");
        Check(Of("$item_chest_mithril") == "Mithril@Mountains#2" && !sets.First(s => s.Name == "Mithril").HearthNamed && sets.First(s => s.Name == "Mithril").Material == null,
              "sets: a mod's set without a name keeps its material's name, in the land its other ingredient comes from (" + Of("$item_chest_mithril") + ")");
        Check(Of("$item_chest_pcuirass") == "Plainswarden@Plains#3" && Of("$item_helmet_carapace") == "Seeker-Shell@Mistlands#3" && Of("$item_chest_flametal") == "Emberforged@Ashlands#3",
              "sets: padded by its linen in the Plains, carapace (not its Refined Eitr) in the Mistlands, flametal in the Ashlands");
        Check(Of("$item_chest_mage") == "Mage@Mistlands#3" && Of("$item_chest_heavy_deepnorth") == "Protector@Deep North#2", "sets: the game's Mage set, and the Protector hardened from casts in the Deep North");
        Check(Of("$item_helmet_hildir1") == null, "sets: a piece no recipe makes (Hildir sells it) is in no set");
        Check(sets.Select(s => s.Land).SequenceEqual(sets.Select(s => s.Land).OrderBy(x => x)), "sets: in biome order, Meadows first");

        // ---- a mod's data: a bar no vanilla name knows, a station no land: the set keeps its material's name in Other lands; a cape alone stays alone ----
        var modded = PanelModel.ArmorSetsOf(new[]
        {
            P("$item_helm_orichalcum", "Helmet", "orichalcum_anvil", 1, null, ("$item_orichalcumbar", "Orichalcum", 10, true)),
            P("$item_chest_orichalcum", "Chest", "orichalcum_anvil", 2, null, ("$item_orichalcumbar", "Orichalcum", 14, true), ("$item_coal", "Coal", 4, false)),
            P("$item_cape_star", "Shoulder", "orichalcum_anvil", 1, null, ("$item_stardust", "Stardust", 3, false)),
            P("$item_chest_guild", "Chest", "piece_workbench", 1, "guild", ("$item_deerhide", "Deer Hide", 2, false)),
            P("$item_legs_guild", "Legs", "piece_workbench", 1, "guild", ("$item_deerhide", "Deer Hide", 2, false)),
        });
        Check(modded.Any(s => s.Name == "Orichalcum" && s.Land == PanelModel.ArmorLands.Length && s.Pieces.Count == 2) && PanelModel.LandName(PanelModel.ArmorLands.Length) == PanelModel.OtherLands,
              "sets: a mod's bar and station: \"Orichalcum\" in Other lands, its two pieces (levels 1 and 2) one set");
        Check(modded.Any(s => s.Name == "Stardust" && s.Pieces.Count == 1), "sets: a mod's cape of no material stays a set of its own, by its ingredient's name");
        Check(modded.Any(s => s.Name == "Guild" && s.GameNamed && s.Land == 0 && s.Pieces.Count == 2), "sets: a mod's set name is the game's name (made readable)");

        // ---- the page, your own book: a land per island, a set per band; made = the game's count, not made = the dashed tile with its materials ----
        var yours = SetsSample.World(now, "yours");
        var v = PanelModel.Build(yours, Sets());
        var hero = PanelModel.Content(v).First(b => b.Kind == "hero");
        Check(v.Heading == PanelModel.SetsLabel && hero.Value == "2" && hero.Title == "sets complete" && hero.Items?.FirstOrDefault()?.Value == "3" && hero.Items[0].Title == "begun",
              "page: \"2 sets complete\" (the leather, the bronze with its leggings worn), \"3 begun\" (rags, troll, iron) (" + hero.Value + " " + hero.Title + " / " + hero.Items?.FirstOrDefault()?.Value + ")");
        var leather = Band(v, "Deerstalker's Garb"); var iron = Band(v, "Cryptbreaker's Mail"); var bronze = Band(v, "Bronze-Clad"); var troll = Band(v, "Troll");
        Check(leather != null && leather.Selected && leather.Value == PanelModel.SetFull && leather.Text == "deer hide" && leather.Items.Count == 4 && leather.Items.All(t => t.Tone == null && t.Value != null),
              "page: Deerstalker's Garb complete, four tiles each with the game's count or \"worn\"");
        Check(leather.Items.First(t => t.Id == "$item_helmet_leather").Value == "2", "page: a tile's number is the game's own count of that piece made (Leather Helmet 2)");
        Check(bronze != null && bronze.Value == PanelModel.SetFull && bronze.Selected && bronze.Items.First(t => t.Id == "$item_legs_bronze").Value == PanelModel.SetWornNote,
              "page: Bronze-Clad complete with the leggings worn, not made (" + bronze?.Value + ")");
        Check(troll != null && troll.Value == "2 of 4 made or worn" && !troll.Selected && troll.Items.Count(t => t.Tone == PanelModel.NotMadeTone) == 2,
              "page: Troll \"2 of 4 made or worn\", two dashed tiles (" + troll?.Value + ")");
        var leggings = troll.Items.First(t => t.Tone == PanelModel.NotMadeTone);
        Check(leggings.Title == "Troll Leather Trousers" && leggings.Value == null && leggings.Items.Select(c => c.Title + " " + c.Value).SequenceEqual(new[] { "Troll Hide 5", "Bone Fragments 3" }) && leggings.Items.All(c => c.Icon.StartsWith("item:$item_")),
              "page: a piece neither made nor worn shows its recipe's materials under its name, pictures and amounts, no number of its own");
        Check(iron != null && iron.Value == "1 of 3 made or worn" && iron.Items.Count(t => t.Tone == PanelModel.NotMadeTone) == 2, "page: a set begun by wearing: the worn helmet, two tiles dashed, \"1 of 3 made or worn\"");
        Check(Band(v, "Frostfang") == null && Band(v, "Protector") == null && Band(v, "Mithril") == null, "page: sets whose recipes you do not know stay hidden");
        var waitLine = PanelModel.Content(v).FirstOrDefault(b => b.Kind == "note" && (b.Text ?? "").Contains("wait in lands further on"));
        var hidden = sets.Count - Bands(v).Count;
        Check(waitLine != null && waitLine.Text.StartsWith(hidden + " more sets wait"), "page: one line counts the sets that wait (" + waitLine?.Text + ")");
        var sections = PanelModel.Content(v).Where(b => b.Kind == "section").Select(b => b.Title + (b.Value != null ? "·" + b.Value : "")).ToList();
        Check(sections.SequenceEqual(new[] { "Meadows", "Black Forest", "Swamp" }), "page: a section per land, in biome order, no land label claims every set (" + string.Join(", ", sections) + ")");
        var plate = PanelModel.PlateOf(v);
        var islands = PanelModel.Islands(plate);
        var meadowsIsland = islands.FirstOrDefault(i => i.Parts.Any(p => p.Block.Kind == "section" && p.Block.Title == "Meadows"));
        Check(meadowsIsland != null && meadowsIsland.Parts.Count(p => p.Block.Tone == PanelModel.SetBandTone) == 2, "islands: a land's sets are one island, a band each (the Meadows: rags and leather)");
        Check(v.AboutNumbers != null && v.AboutNumbers.Items.Any(i => (i.Text ?? "").Contains("whole character")), "page: About these numbers says what made means and how sets are grouped");
        Check(!PanelModel.AllText(v).Any(t => (t ?? "").IndexOf("armour", StringComparison.OrdinalIgnoreCase) >= 0), "page: US spelling, \"armor\" in every word the page says");

        // ---- one land in full: the land label; nothing made yet: the hero gives way, every tile dashed ----
        var land = PanelModel.Build(SetsSample.World(now, "land"), Sets());
        Check(PanelModel.Content(land).Any(b => b.Kind == "section" && b.Title == "Meadows" && b.Value == PanelModel.LandFull) && PanelModel.Content(land).Any(b => b.Kind == "section" && b.Title == "Black Forest" && b.Value == null),
              "land: \"MEADOWS · every set complete\" when both its sets are complete, the Black Forest says nothing");
        var fresh = PanelModel.Build(SetsSample.World(now, "new"), Sets());
        Check(!PanelModel.Content(fresh).Any(b => b.Kind == "hero") && Bands(fresh).Count == 2 && Bands(fresh).SelectMany(b => b.Items).All(t => t.Tone == PanelModel.NotMadeTone),
              "new: nothing made: no hero number (never a 0), the two Meadows sets with every tile dashed");

        // ---- Crafting's heading-row chip opens it, with Back; the left list keeps Crafting lit ----
        var craft = new PanelState { Chapter = Chapter.Deeds }; craft.Page[Chapter.Deeds] = "crafting";
        var cv = PanelModel.Build(yours, craft);
        var chip = PanelModel.PlateOf(cv)?.Items?.FirstOrDefault(b => b.Kind == "headlink" && b.Id == "Deeds/" + PanelModel.SetsPageId);   // 0.9: beside the Feed chip (Chapters/DeedFeedModel.cs)
        Check(chip != null && chip.Id == "Deeds/" + PanelModel.SetsPageId && chip.Title == PanelModel.SetsChip, "crafting: a headlink chip \"Sets\" in the heading row");
        PanelModel.Visited(craft, cv.Active, cv.Page); PanelModel.Follow(craft, chip.Id);
        var opened = PanelModel.Build(yours, craft);
        Check(opened.Page == PanelModel.SetsPageId && opened.Back == PanelModel.BackLabel && opened.List.First(l => l.Selected).Id == "crafting", "crafting: the chip opens Armor sets with its Back button, Crafting lit in the list");
        PanelModel.Visited(craft, opened.Active, opened.Page);
        Check(PanelModel.Back(craft) && craft.PageOf(Chapter.Deeds) == "crafting", "crafting: Back returns to Crafting");
        var eddaBook = FullDump.Books(yours).First(b => b.who == SampleWorld.Edda).book;
        Check(PanelModel.PlateOf(PanelModel.Build(eddaBook, new PanelState { Chapter = Chapter.Deeds, Player = SampleWorld.Edda, Page = { [Chapter.Deeds] = "crafting" } }))?.Items?.Any(b => b.Kind == "headlink" && b.Id == "Deeds/" + PanelModel.SetsPageId) != true,
              "crafting: no chip on a fellow's book (the recipes known are yours)");

        // ---- Everyone: per set who made or wore a piece of it, in the fixed order, and whether the group completed it; a copy that does not say is named once ----
        var ev = SetsSample.World(now, "everyone");
        var gv = PanelModel.Build(ev, Sets(true));
        string Who(string set) { var b = Band(gv, set); return b == null ? null : string.Join(",", b.Items.Select(i => i.Value + (i.Note == PanelModel.SetWornNote ? "(worn)" : ""))) + (b.Value != null ? "|" + b.Value : ""); }
        Check(gv.EveryoneOn && Who("Deerstalker's Garb") == "You,Edda,Tor|complete" && Who("Bronze-Clad") == "You,Edda|complete" && Who("Leather Scraps") == "You,Tor|complete"
              && Who("Troll") == "You,Edda(worn)|complete" && Who("Cryptbreaker's Mail") == "You(worn),Tor|2 of 3 made or worn",
              "everyone: who made or wore a piece of each set, you first then by name, a ring for only worn (Troll: " + Who("Troll") + "; Iron: " + Who("Cryptbreaker's Mail") + ")");
        Check(PanelModel.Content(gv).First(b => b.Kind == "hero").Value == "4", "everyone: 4 sets complete in the group (Troll by Edda's worn legs and cape)");
        Check(Bands(gv).SelectMany(b => b.Items).All(i => i.Tone == PanelModel.PersonTileTone && i.Icon.StartsWith("person:") && !i.Value.Any(char.IsDigit)), "everyone: a person tile names the player, never a count");
        Check(PanelModel.Content(gv).Any(b => b.Kind == "note" && b.Text == "Finch's Hearthwoven is 0.8.6: the sets Finch made or wore fill in once they update."), "everyone: a copy from before 0.9 is named once, what fills in");
        Check(!PanelModel.Content(gv).Any(b => b.Kind == "hero" && (b.Items?.Count ?? 0) > 0), "everyone: one group number, no number per player");

        // ---- a fellow's book: only what they began, and why ----
        var fv = PanelModel.Build(ev.Fellows.First(f => f.PlayerName == SampleWorld.Edda), new PanelState { Chapter = Chapter.Deeds, Player = SampleWorld.Edda, Page = { [Chapter.Deeds] = PanelModel.SetsPageId } });
        Check(Bands(fv).Select(b => b.Title).SequenceEqual(new[] { "Deerstalker's Garb", "Bronze-Clad", "Troll" }) && PanelModel.Content(fv).Any(b => b.Kind == "note" && (b.Text ?? "").StartsWith("Only the sets Edda began")),
              "fellow: Edda's book shows the sets she began (the Troll by wearing), and one line says why the rest is not there");

        // ---- the copy: which armor pieces were made, yes or no; bounded; an older reader ignores it, an older copy has none ----
        var made = PanelModel.ArmorMadeOf(new Dictionary<string, float> { ["$item_helmet_leather"] = 3, ["$item_axe_bronze"] = 2, ["$item_chest_bronze"] = 0 }, SetsSample.TypeOf);
        Check(made.SequenceEqual(new[] { "$item_helmet_leather" }), "copy: only armor made, no count, no weapon, no piece at 0");
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats(); stats[0].m_itemCraftStats["$item_helmet_leather"] = 3;
        string Snap(IEnumerable<string> armor) => GroupShare.SharedCopy(Snapshot.Build("0.9.0", 7, "Edda", stats, new Snapshot.SkillInfo[0], "Midgard", new DamageTally(), "s1", new SessionEvents(), new EventLog(), true, armorMade: armor));
        var back = PanelInput.FromSnapshot(Snap(made));
        Check(back.ArmorMade != null && back.ArmorMade.SequenceEqual(made), "copy: armorMade read back as sent");
        Check(PanelInput.FromSnapshot(Snap(null)).ArmorMade == null && PanelInput.FromSnapshot(Snap(new string[0])).ArmorMade?.Count == 0, "copy: no key = an older Hearthwoven (null), an empty list = none made");
        var many = Enumerable.Range(0, 400).Select(i => "$item_chest_x" + i.ToString("000")).ToList();
        Check(PanelInput.FromSnapshot(Snap(many)).ArmorMade.Count == 300, "copy: at most 300 armor tokens travel");
        var bytes = Snap(SetsSample.Pieces.Select(p => p.Token)).Length - Snap(new string[0]).Length;
        System.Console.WriteLine("  armorMade for every piece of the sample (" + SetsSample.Pieces.Count + " tokens): " + bytes + " bytes in the copy");
        Check(bytes < 2000, "copy: every sample piece made costs " + bytes + " bytes (under 2 KB)");

        // ---- the feat: one feat, three tiers: one land, half the lands, every land ----
        var def = PanelModel.FeatById("armorsets");
        Check(def != null && def.Tiers.SequenceEqual(new[] { 1.0, 4.0, 8.0 }) && def.Chapter == Chapter.Deeds && def.Page == PanelModel.SetsPageId && def.Rule(1) == "Make or wear every piece of every armor set of one land."
              && def.Rule(4) == "Make or wear every piece of every armor set of 4 lands, half of them." && def.Rule(8) == "Make or wear every piece of every armor set of every land.",
              "feat: Clad for Every Land, tiers I one land, II four (half the eight), III every land");
        Check(PanelModel.LandsArmored(SetsSample.World(now, "land")) == 1 && PanelModel.LandsArmored(yours) == 0 && PanelModel.LandsArmored(new PanelInput()) == null,
              "feat: the lands complete: the Meadows = 1, none = 0, no game data = not counted (null)");

        // ---- the dev dump: every armor item, its set, a piece without a recipe said so ----
        var dump = PanelModel.ArmorDump(SetsSample.Pieces);
        var lines = dump.Split('\n').Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
        Check(lines.Count == SetsSample.Pieces.Count + 1 && lines[0].StartsWith("token\tprefab\tname\tslot\tgame set name\tset size") && dump.Contains("(no recipe)") && dump.Contains("Deerstalker's Garb · deer hide [Hearthwoven]") && dump.Contains("Troll [game]"),
              "dump: a header and one line per armor item with its set (" + lines.Count + " lines)");
        return fails;
    }
}
