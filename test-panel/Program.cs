// Tests for the Hearthwoven panel model (src/Panel): the chapters and pages, filters, companions both ways, source
// labels, empty states, the switcher and keys, and views built from a fellow player's shared snapshot.
// With --dump <dir> it writes the model output for a fictional sample evening as preview-data.js, which
// src/Panel/preview/panel-preview.html renders (the static preview is driven by this same model output).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Hearthwoven;
using Hearthwoven.Panel;

int fails = 0;
void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
PanelModel.SceneExists = FeatScenesTests.FromFiles();   // 0.9.1: the feats' drawn scenes, as the game finds them (its manifest), for the pages and the dump

var now = new DateTime(2026, 10, 7, 22, 30, 0, DateTimeKind.Utc);
if (Array.IndexOf(args, "--bench") >= 0) return PanelBenchOffline.Run(args, now);   // 0.8: the offline page bench instead of the tests (test-panel/Bench.cs)
HitData.DamageTypes D(string type, float v)
{
    var d = new HitData.DamageTypes();
    switch (type)
    {
        case "slash": d.m_slash = v; break; case "pierce": d.m_pierce = v; break; case "blunt": d.m_blunt = v; break;
        case "poison": d.m_poison = v; break; case "fire": d.m_fire = v; break; case "frost": d.m_frost = v; break;
    }
    return d;
}

// A sample evening for Rowan (fictional players), built through the mod's own recorders (src/Panel/PanelSample.cs, where the
// game's Dev.SampleData mode reads it too)
PanelInput Sample() => PanelSample.Evening(now);

var input = Sample();
var voyager = Program.VoyagesHallSample(Sample());   // Voyages and Hall numbers on top (test-panel/VoyagesHallTests.cs)
PanelView Show(PanelInput i, Chapter c, string page = null, Action<PanelState> more = null)
{
    var st = new PanelState { Chapter = c }; if (page != null) st.Page[c] = page; more?.Invoke(st);
    return PanelModel.Build(i, st);
}
// the page's blocks with the layout boxes opened (a plate, its columns, the chosen view of a switch)
Block Find(PanelView v, string kind, Func<Block, bool> where = null) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind && (where == null || where(b)));

// ---------- the measured event log: windows, biomes, deaths, hints ----------
var all = PanelModel.Damage(input.Log, TimeWindow.Session, "", now);
var hour = PanelModel.Damage(input.Log, TimeWindow.LastHour, "", now);
var three = PanelModel.Damage(input.Log, TimeWindow.LastThreeHours, "", now);
Check(all.Any(r => r.Biome == "Meadows") && !three.Any(r => r.Biome == "Meadows") && three.Any(r => r.Biome == "BlackForest"), "window: 3.5 h ago only in the whole session, 2 h ago in the last three hours");
Check(hour.All(r => r.Biome == "Swamp") && hour.Count > 0, "window: last hour keeps only the Swamp fight");
var later = now.AddMinutes(5);   // window starts 21:35, inside the 21:35-21:36 bucket
var edge = new EventLog(); edge.AddDamage(new DateTime(2026, 10, 7, 21, 35, 30, DateTimeKind.Utc), "Plains", true, "Deathsquito", "Bows", D("pierce", 5));
Check(PanelModel.Damage(edge, TimeWindow.LastHour, "", later).Count == 1, "window: a minute bucket that reaches into the window counts");
edge = new EventLog(); edge.AddDamage(new DateTime(2026, 10, 7, 21, 34, 30, DateTimeKind.Utc), "Plains", true, "Deathsquito", "Bows", D("pierce", 5));
Check(PanelModel.Damage(edge, TimeWindow.LastHour, "", later).Count == 0, "window: a bucket that ended before the window does not");
// the finer windows (Joost, play-test): the last 10 and 30 minutes of a fight, since install apart
var fine = new EventLog(); var nowF = new DateTime(2026, 10, 7, 22, 30, 0, DateTimeKind.Utc);
fine.AddDamage(nowF.AddMinutes(-25), "Swamp", true, "Draugr", "Axes", D("slash", 100));
fine.AddDamage(nowF.AddSeconds(-510), "Swamp", true, "Draugr", "Axes", D("slash", 50));
fine.AddDamage(nowF.AddMinutes(-2), "Swamp", true, "Troll", "Axes", D("slash", 7));
fine.AddDamage(nowF.AddMinutes(-50), "Swamp", true, "Troll", "Axes", D("slash", 1000));
double DealtIn(TimeWindow w) => PanelModel.Damage(fine, w, "", nowF).Where(r => r.Dir == "dealt").Sum(r => (double)r.Amount);
Check(DealtIn(TimeWindow.LastTenMinutes) == 57 && DealtIn(TimeWindow.LastThirtyMinutes) == 157 && DealtIn(TimeWindow.LastHour) == 1157 && DealtIn(TimeWindow.Session) == 1157 &&
      PanelModel.Cutoff(TimeWindow.LastTenMinutes, nowF) == nowF.AddMinutes(-10) && PanelModel.Cutoff(TimeWindow.SinceInstall, nowF) == null,
      "window: last 10 and 30 minutes count the minutes that reach into them (57, 157), the hour 1 157; since install has no cutoff");
Check(Enum.GetNames(typeof(TimeWindow)).SequenceEqual(new[] { "LastTenMinutes", "LastThirtyMinutes", "LastHour", "LastThreeHours", "Session", "Today", "Yesterday", "ThreeDays", "SevenDays", "ThirtyDays", "SinceInstall" }) &&
      Enum.GetValues(typeof(TimeWindow)).Cast<TimeWindow>().Select(PanelModel.WindowLabel).SequenceEqual(new[] { "Last 10 minutes", "Last 30 minutes", "Last hour", "Last 3 hours", "This session", "Today", "Yesterday", "Last 3 days", "Last 7 days", "Last 30 days", "All" }) &&
      Enum.GetValues(typeof(TimeWindow)).Cast<TimeWindow>().Select(PanelModel.WindowShort).SequenceEqual(new[] { "10 min", "30 min", "1 h", "3 h", "Session", "Today", "Yesterday", "3 days", "7 days", "30 days", "All" }) &&
      PanelModel.AllWindows.SequenceEqual(Enum.GetValues(typeof(TimeWindow)).Cast<TimeWindow>()),
      "window: eleven windows, shortest first (the log's, the days' with Yesterday and 3 days between Today and 7 days (0.9.1), All), each with its full name and its chip (0.7 rule W.2: All, not \"Since install\")");
// a copy from an older sender keeps ten-minute buckets (its JSON says so): a bucket reaches ten minutes on, not one
var oldSender = PanelInput.FromSnapshot("{\"name\":\"Old\",\"measuredLog\":{\"bucketMinutes\":10,\"damage\":{\"2026-10-07T20:10Z|Swamp|dealt|Draugr|Axes|slash\":40},\"hits\":{}}}");
var newSender = PanelInput.FromSnapshot("{\"name\":\"New\",\"measuredLog\":{\"bucketMinutes\":1,\"damage\":{\"2026-10-07T20:10Z|Swamp|dealt|Draugr|Axes|slash\":40},\"hits\":{}}}");
var at2025 = new DateTime(2026, 10, 7, 20, 25, 0, DateTimeKind.Utc);
Check(oldSender.Log.Span == 10 && newSender.Log.Span == 1 && PanelModel.Damage(oldSender.Log, TimeWindow.LastTenMinutes, "", at2025).Count == 1 && PanelModel.Damage(newSender.Log, TimeWindow.LastTenMinutes, "", at2025).Count == 0,
      "window: a copy from an older sender reads its buckets as ten minutes long (the span travels in the JSON)");
var deaths = PanelModel.Deaths(input.Log, TimeWindow.Session, "", now);
Check(PanelModel.Deaths(input.Log, TimeWindow.LastHour, "", now).Count == 2 && deaths[0].Time > deaths[1].Time, "deaths: window filter, latest first");
var killers = PanelModel.MostCommonKiller(deaths);
Check(killers["Swamp"] == "Blob" && killers["BlackForest"] == "Troll", "most common killer per biome");
var hints = PanelModel.Hints(all, deaths);
Check(hints.Count > 0 && hints[0].StartsWith("Swamp") && hints[0].Contains("poison") && hints[0].Contains("both deaths") && hints[0].Contains("Poison resistance mead"), "hint: poison falls in the Swamp (both deaths) ask for Poison resistance mead: " + hints.FirstOrDefault());
Check(!hints.Any(h => h.StartsWith("Black Forest")), "hint: no advice for a blunt Troll fall (no remedy guess)");

// ---------- the menu: six chapters, left lists, one owner per metric ----------
var deeds = Show(input, Chapter.Deeds);
Check(deeds.Chapters.Select(c => c.Label).SequenceEqual(new[] { "Deeds", "Feats", "Company", "Hall", "Battle", "Voyages", "Skills" }) && deeds.Chapters[0].Selected,
      "menu: seven chapters in the agreed order, Deeds first, Feats its own chapter right after it (Joost 2026-10-09)");
Check(deeds.List.Select(l => l.Label).SequenceEqual(new[] { "Recent", "Overview", "Cooking", "Meals", "Meal plan", "Building", "Groundwork", "Crafting", "Woodcutting", "Mining", "Farming", "Fishing", "Taming" }) && deeds.Page == "overview",
      "menu: Deeds left list per the menu tree, Recent first (0.7), the book still opens on the Overview");
var grid = Find(deeds, "strip");   // the other chapters' names earned (Deeds overview A)
Check(grid != null && grid.Items.Single(t => t.Title == "Shieldbearer").Id == "Battle/defense" && grid.Items.Single(t => t.Title == "Helmskeeper").Id == "Voyages/sailing" &&
      Find(deeds, "cards").Items.Single(t => t.Title == "Hearth Cook").Id == "Deeds/cooking", "overview: each title is a shortcut to its owner page (blocking in Battle, sailing in Voyages)");
var jump = new PanelState { Window = TimeWindow.SinceInstall }; PanelModel.Jump(jump, "Battle/defense");
var defense = PanelModel.Build(input, jump);
Check(defense.Active == Chapter.Battle && defense.Page == "defense" && defense.Badges.Select(b => b.Label).SequenceEqual(new[] { "Shieldbearer" }),   // B28: Wallwarden moved to Deeds > Building
      "overview: the jump lands on Battle > Defense with its titles in the header");
var guardBlock = Find(defense, "guard");   // ch-battle: the approved Defense page (r4battle-defense) replaced the stat
Check(guardBlock != null && guardBlock.Value == "28" && guardBlock.Value2 == "27" && guardBlock.Note == null, "defense: blocks and parries apart (55 held blocks: 28 blocks and 27 parries)");
// B28 (Joost: "Most defences standing at once" is a building thing, not a fight): base defences and Wallwarden live on Deeds > Building (All)
var buildingAll = PanelModel.Build(input, new PanelState { Chapter = Chapter.Deeds, Window = TimeWindow.SinceInstall, Page = { [Chapter.Deeds] = "building" } });
Check(Find(defense, "rows", b => b.Items.Any(i => i.Title == PanelModel.MostDefences)) == null && Find(buildingAll, "rows", b => b.Items.Any(i => i.Title == "Most defenses standing at once" && i.Value == "40")) != null &&
      buildingAll.Badges.Any(b => b.Label == "Wallwarden"),
      "defense (B28): base defences and Wallwarden live on Deeds > Building, from the character record, not on Battle > Defence");
// one owner per number: walk every page of every chapter (both Company directions) and note where each number shows up
var pages = new List<(string where, PanelView v)>();
foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
    foreach (var l in PanelModel.Build(input, new PanelState { Chapter = ch }).List)
        foreach (var they in new[] { true, false })
            pages.Add((ch + "/" + l.Id, PanelModel.Build(input, new PanelState { Chapter = ch, Page = { [ch] = l.Id }, TheyReceived = they, Window = TimeWindow.SinceInstall })));   // All: every number since install (Defence's blocks have no session tally in this sample)
IEnumerable<string> WhereShown(Func<string, bool> has) => pages.Where(p => PanelModel.AllText(p.v).Any(has)).Select(p => p.where).Distinct();
Check(pages.Where(p => PanelModel.Content(p.v).Any(b => b.Kind == "guard")).Select(p => p.where).Distinct().SequenceEqual(new[] { "Battle/overview", "Battle/defense" }) &&
      pages.Where(p => p.where == "Battle/overview").All(p => PanelModel.Content(p.v).Where(b => b.Kind == "guard").All(b => b.Tone == PanelModel.GuardLineTone && b.Id == PanelModel.DefensePage)) &&
      WhereShown(t => t.Contains("27 parr") || t.Contains("successful block")).All(w => w == PanelModel.TitlesLink || w == "Battle/defense" || w == "Battle/overview"),
      "owner: blocks and parries show on Battle > Defense, and (0.8.5, Joost: \"rather than stats\") as the overview's quiet line that opens Defense; not on Deeds > Overview; Feats > Titles and Defence's title strip say them as Shieldbearer's reason (B18)");
// helm distance: Voyages > Overview (the journey) and Sailing only, in test-panel/VoyagesHallTests.cs
Check(Find(deeds, "strip").Items.All(t => !(t.Value + t.Text).Any(char.IsDigit) && string.IsNullOrEmpty(t.Note)), "owner: Deeds > Overview: the other chapters' names earned are shortcuts without numbers (the deed cards may repeat their own page's numbers: Joost 2026-10-08)");
var battleOverview = Show(input, Chapter.Battle);
Check(Find(battleOverview, "guard")?.Id == "Battle/defense" && Find(battleOverview, "guard").Tone == PanelModel.GuardLineTone && Find(battleOverview, "link") == null, "owner: Battle > Overview shows the window's blocks and parries as a quiet line that opens Defense, not a button (0.8.5, Joost)");
// Voyages and Hall (Overview, Trader, Smelters; chests and carts are server-only): test-panel/VoyagesHallTests.cs
Program.VoyagesHallChecks(voyager, Check);

// ---------- Deeds pages ----------
var cookingAlone = Show(input, Chapter.Deeds, "cooking");
Check(cookingAlone.Heading == "Cooking" && Find(cookingAlone, "hero")?.Value == "166" && Find(cookingAlone, "hero").Title == "dishes cooked" && cookingAlone.Badges.Any(b => b.Label == "Hearth Cook"), "cooking: without fellow records, the profile count leads (the hero number)");
var crafting = Show(input, Chapter.Deeds, "crafting");
Check(Find(crafting, "tiles") == null && !PanelModel.AllText(crafting).Any(t => t.Contains("smelter")), "crafting: smelters are the Hall's, not on Crafting (round 4 page)");
Check(Find(crafting, "filterbar").Src == "character" && Find(crafting, "filterbar").Note == null && Find(crafting, "itemgrid").Src == "character", "crafting: crafted gear carries the character mark, no source sentence");

// ---------- B6: Crafting counts gear, food is Cooking (the game's own m_itemType switch) ----------
Check(PanelModel.ItemKindOf("Consumable", "$item_carrotsoup") == "food" && PanelModel.ItemKindOf("Consumable", "$item_fishingbait_forest") == "other" &&
      PanelModel.ItemKindOf("TwoHandedWeaponLeft", "$item_x") == "gear" && PanelModel.ItemKindOf("Shield", "$item_x") == "gear" && PanelModel.ItemKindOf("Utility", "$item_x") == "gear" &&
      PanelModel.ItemKindOf("Trinket", "$item_x") == "gear" && PanelModel.ItemKindOf("Ammo", "$item_x") == "other" && PanelModel.ItemKindOf("Material", "$item_x") == "other" && PanelModel.ItemKindOf(null, "$item_x") == null,
      "B6 classify: food = consumable without bait; gear = weapons, shields, armour, tools, utility, trinkets (the game's craft switch)");
var gearRows = PanelModel.Content(crafting).First(b => b.Kind == "itemgrid");
Check(gearRows.Items.Select(i => i.Title).OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(new[] { "Bronze Axe", "Crude Bow", "Cultivator", "Hoe", "Iron Sword", "Leather Helmet", "Hammer", "Wood Shield" }.OrderBy(t => t, StringComparer.Ordinal)),
      "B6 crafting: the item grid lists gear only (the game's gear switch): " + string.Join(", ", gearRows.Items.Select(i => i.Title + "=" + i.Value)));
Check(!PanelModel.AllText(crafting).Any(t => t.Contains("Carrot") || t.Contains("Bread")), "B6 crafting: no food on the Crafting page");
Check(Find(crafting, "hero")?.Value == "19" && Find(crafting, "hero").Title == "gear crafted" && Find(crafting, "hero").Items.Single().Value == "11" && Find(crafting, "hero").Items.Single().Title == "upgrades made",
      "B6 crafting: the hero counts the same gear as the tiles (weapons 9 + armour 6 + tools 4) and upgrades on their own");
Check(!PanelModel.AllText(crafting).Any(t => t.Contains("Wood Arrow") || t.Contains("Modthing")), "B6 crafting: ammo and items the game data does not know never count as gear");
var cookedRows = PanelModel.Content(cookingAlone).FirstOrDefault(b => b.Kind == "composition");
Check(cookedRows != null && cookedRows.Items.First().Title == "Carrot Soup" && cookedRows.Items.First().Value == "118" && cookedRows.Source == "character",
      "B6 cooking: the food from ItemsCrafted is under Cooking, as a game counter");

// ---------- B8: Building is build pieces; groundwork, plantings and feasts have their own place ----------
Check(PanelModel.PieceKindOf(true, false, false) == "ground" && PanelModel.PieceKindOf(false, true, false) == "planted" && PanelModel.PieceKindOf(false, false, true) == "feast" && PanelModel.PieceKindOf(false, false, false) == "built",
      "B8 classify: TerrainOp = groundwork, Plant = planted, Feast = feast, anything else built");
var building = Show(input, Chapter.Deeds, "building");
var built = PanelModel.Content(building).SkipWhile(b => !(b.Kind == "section" && b.Title == "Every piece")).Skip(1).First();
var groundPage = Show(input, Chapter.Deeds, "groundwork");
var ground = Find(groundPage, "composition");
Check(built.Items.Select(i => i.Value).SequenceEqual(new[] { "520", "310", "36" }) && ground.Items.Select(i => i.Value).SequenceEqual(new[] { "850", "133", "40" }) &&
      Find(building, "composition") == null && !building.Blocks.Any(b => b.Kind == "section" && b.Title == "Groundwork"),
      "B8 building: built pieces as a list; groundwork (levelled, lowered, paved) is not here but on its own page, Groundwork, in one composition");
Check(groundPage.Heading == "Groundwork" && groundPage.List.Select(l => l.Id).SkipWhile(i => i != "building").Skip(1).First() == "groundwork" && groundPage.List.Single(l => l.Id == "groundwork").Icon == "vocab:ground-lower" &&
      Find(groundPage, "hero")?.Value == "1\u00A0023" && Find(groundPage, "hero").Title == "groundwork strokes" && ground.Title == null,
      "Groundwork page: right after Building in the list, the ground-lower icon, the total as the hero, the bar under it");
Check(Find(building, "hero").Value == "866" && building.Badges.Any(b => b.Label == "Hallwright"),
      "B8 building: the built count in the ranking's head, the same pieces as the list");
Check(!PanelModel.AllText(building).Any(t => t.Contains("Sapling") || t.Contains("Feast")), "B8 building: plantings and feasts are not building");
// fix2 4 (Joost in game, Dev.SampleData: the game data does not know the sample's tokens, so "Beech Sapling" and "Feast
// meadows" stood under pieces built): a piece the game data does not know is placed by its name
var unknownPieces = Sample(); unknownPieces.PieceKind = _ => null;
var builtUnknown = Show(unknownPieces, Chapter.Deeds, "building");
Check(!PanelModel.AllText(builtUnknown).Any(t => t.Contains("Sapling") || t.Contains("Feast")) &&
      PanelModel.Placed(unknownPieces, "planted").ContainsKey("$piece_sapling_carrot") && PanelModel.Placed(unknownPieces, "feast").ContainsKey("$piece_feast_meadows") &&
      PanelModel.PieceKindByName("$piece_sapling_beech") == "planted" && PanelModel.PieceKindByName("$piece_feast_meadows") == "feast" && PanelModel.PieceKindByName("$piece_woodwall") == "built" &&
      PanelModel.PieceKindByName("$piece_levelground") == "ground",
      "fix2 4 building: a piece the game data does not know goes by its name: saplings and seeds to Farming, feasts to Cooking, never under pieces built");
Check(Find(builtUnknown, "itemgrid").Items.All(i => i.Icon.StartsWith("piece:$")) && Find(builtUnknown, "itemgrid").Items.Select(i => i.Value).SequenceEqual(new[] { "520", "310", "36" }),
      "fix2 4 building: pieces built is the item grid (picture, number, name) of the build pieces only");
var farming = Show(input, Chapter.Deeds, "farming");
Check(Find(farming, "hero")?.Value == "60" && Find(farming, "hero").Title == "planted", "B8 farming: plantings live on Farming");
Check((Find(cookingAlone, "stat", b => b.Title == "feasts set out")?.Value ?? Find(cookingAlone, "hero")?.Items?.FirstOrDefault(n => n.Title == "feasts set out")?.Value) == "2", "B8 cooking: feasts set out live on Cooking (beside the dishes cooked, or alone when those are counted on this PC)");

// ---------- C1/C2: what came from trees and rocks, per kind ----------
var wood = Show(input, Chapter.Deeds, "woodcutting");
var woodRows = Find(wood, "composition");
Check(woodRows.Items.Select(i => i.Icon + "=" + i.Value).SequenceEqual(new[] { "item:$item_wood=2\u00A0400", "item:$item_roundlog=520", "item:$item_finewood=310", "item:$item_elderbark=75" }) && woodRows.Source == "character" &&
      woodRows.Note == null && woodRows.Items.All(i => i.Fraction2 == 0) && !PanelModel.AllText(wood).Contains(PanelModel.AtLeast) && !PanelModel.Content(wood).Any(b => b.Kind == "section" && b.Title.StartsWith("Wood brought in")),
      "C2 woodcutting: wood brought in per kind in the composition (your character's count, no 'at least': About explains brought in; 0.7: plain sums, no faded parts); nothing counted exactly yet, so no list repeating it");
var mining = Show(input, Chapter.Deeds, "mining");
var oreRows = Find(mining, "composition");
Check(oreRows.Items.Select(i => i.Icon + "=" + i.Value).SequenceEqual(new[] { "item:$item_stone=1\u00A0800", "item:$item_copperore=240", "item:$item_tinore=90" }) && !PanelModel.AllText(mining).Any(t => t.Contains("aspberr")),
      "C1 mining: stone and ore brought in per kind (the composition), berries left out");
// composition colours follow the item's own icon (coal near black): the game's icon colour first, the sample table without
// the game, the neutral palette for an item neither knows; wood keeps its grain look, a modded log takes its icon colour
var coalIn = new PanelInput { ItemsPickedUp = new Dictionary<string, float> { ["$item_coal"] = 5, ["$item_stone"] = 4, ["$item_modore"] = 2, ["$item_wood"] = 1 }, GatherKind = _ => "mining" };
var coalBar = Find(PanelModel.Build(coalIn, new PanelState { Page = { [Chapter.Deeds] = "mining" } }), "composition");
coalIn.ItemColour = t => t == "$item_modore" ? "#123456" : t == "$item_stone" ? "#707070" : null;
var tinted = Find(PanelModel.Build(coalIn, new PanelState { Page = { [Chapter.Deeds] = "mining" } }), "composition");
var coalHex = coalBar.Items.First(i => i.Id == "$item_coal").Colour;
Check(int.Parse(coalHex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber) < 0x40 && tinted.Items.First(i => i.Id == "$item_modore").Colour == "#123456" &&
      tinted.Items.First(i => i.Id == "$item_stone").Colour == PanelModel.MaterialColour["$item_stone"] && coalBar.Items.First(i => i.Id == "$item_modore").Colour.StartsWith("#"),
      "C1 colours: a part takes its item's own icon colour (a mod's ore too), a well-known vanilla material its approved colour over the icon, coal near black, unknown items the neutral palette");
var oreBar = Find(PanelModel.Build(new PanelInput { ItemsPickedUp = new Dictionary<string, float> { ["$item_stone"] = 9, ["$item_copperore"] = 5, ["$item_tinore"] = 3, ["$item_ironscrap"] = 1 }, GatherKind = _ => "mining",
                                                 ItemColour = _ => "#556b2f" }, new PanelState { Page = { [Chapter.Deeds] = "mining" } }), "composition");
int Hue(string hex) { var r = Convert.ToInt32(hex.Substring(1, 2), 16); var g = Convert.ToInt32(hex.Substring(3, 2), 16); var bl = Convert.ToInt32(hex.Substring(5, 2), 16); return r > g && g > bl ? 1 : r > g && r > bl ? 2 : 0; }
var copperHex = oreBar.Items.First(i => i.Id == "$item_copperore").Colour; var tinHex = oreBar.Items.First(i => i.Id == "$item_tinore").Colour; var stoneHex = oreBar.Items.First(i => i.Id == "$item_stone").Colour;
Check(Hue(copperHex) == 1 && Convert.ToInt32(copperHex.Substring(1, 2), 16) > 0xb0 && Convert.ToInt32(tinHex.Substring(1, 2), 16) > 0xc8 && Convert.ToInt32(stoneHex.Substring(1, 2), 16) > 0x98 &&
      oreBar.Items.First(i => i.Id == "$item_ironscrap").Colour == PanelModel.MaterialColour["$item_ironscrap"],
      "diff-05 colours: copper ore copper-orange, tin ore pale, stone light grey, iron scrap rust, even when their icons average otherwise");
var woodTint = new PanelInput { ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 3, ["$item_modlog"] = 2 }, GatherKind = _ => "wood", ItemColour = _ => "#ff00ff" };
var woodBar = Find(PanelModel.Build(woodTint, new PanelState { Page = { [Chapter.Deeds] = "woodcutting" } }), "composition");
Check(woodBar.Items.First(i => i.Id == "$item_wood").Pattern == "vocab:grain-wood-n" && woodBar.Items.First(i => i.Id == "$item_wood").Colour == PanelModel.MaterialColour["$item_wood"] &&
      woodBar.Items.First(i => i.Id == "$item_modlog").Colour == "#ff00ff" && woodBar.Items.First(i => i.Id == "$item_modlog").Pattern == PanelModel.GenericGrain,
      "C2 colours (Addendum 5, diff-05): vanilla wood its approved colour, any other wood its icon's own colour over a neutral grain; a modded log the generic grain");
var dataWood = new PanelInput { ItemsPickedUp = new Dictionary<string, float> { ["$item_modlog"] = 9, ["$item_wood"] = 3 }, GatherKind = t => t == "$item_modlog" ? "wood" : null };
Check(PanelModel.Content(PanelModel.Build(dataWood, new PanelState { Page = { [Chapter.Deeds] = "woodcutting" } })).Any(b => b.Kind == "composition" && b.Items.Any(i => i.Icon == "item:$item_modlog")),
      "C2 woodcutting: the list follows the game data (a mod's log counts as wood when trees drop it)");
var muddled = new PanelInput { ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 5, ["$item_stone"] = 4 }, GatherKind = _ => "mining" };
Check(PanelModel.PickedUp(muddled, "wood").Keys.SequenceEqual(new[] { "$item_wood" }) && PanelModel.PickedUp(muddled, "mining").Keys.SequenceEqual(new[] { "$item_stone" }),
      "C1/C2: vanilla wood and stone keep their group whatever a drop table says (a root or a fossil can drop either)");

// ---------- C7: Hearthwoven counts pickups itself, exactly (the game's itemsPickedUp is a floor) ----------
Check(SessionEvents.PickedAmount(false, 20, 0, 20) == 20 && SessionEvents.PickedAmount(false, 20, 35, 55) == 20,
      "C7 rule: a fresh drop counts in full, also when it lands on a stack you carry (the pickup the game skips)");
Check(SessionEvents.PickedAmount(false, 20, 45, 50) == 5, "C7 rule: inventory full, only the 5 that went in count (the rest stays on the ground, counted when picked later)");
Check(SessionEvents.PickedAmount(true, 20, 0, 20) == 0, "C7 rule: re-picking a stack someone already held (your own dropped wood) counts 0");
Check(SessionEvents.PickedAmount(false, 1, 3, 3) == 0 && SessionEvents.PickedAmount(false, 20, 10, 40) == 20, "C7 rule: refused counts 0; never more than the drop held");
SessionEvents.Add(input.Events.PickedUp, "$item_wood", 140); SessionEvents.Add(input.Events.PickedUp, "$item_finewood", 12); SessionEvents.Add(input.Events.PickedUp, "$item_resin", 6);
SessionEvents.Add(input.Events.PickedUp, "$item_copperore", 18); SessionEvents.Add(input.Events.PickedUp, "$item_silverore", 4);
// K1: one layered bar per kind; without a stored baseline (this sample, a fellow's copy) the faded part is the game counter
// minus the exact count, so nothing counts twice
var woodEx = Show(input, Chapter.Deeds, "woodcutting");
var woodExBar = Find(woodEx, "composition");
var woodExWood = woodExBar.Items.Single(i => i.Id == "$item_wood");
Check(woodExBar.Value == "3\u00A0305" && woodExWood.Value == "2\u00A0400" && PanelModel.BroughtIn(input, "wood")["$item_wood"] == (2260, 140) && woodExWood.Fraction2 == 0 && woodExBar.Note == null &&
      !PanelModel.Content(woodEx).Any(b => b.Kind == "section" && b.Title.Contains("counted exactly")) && !PanelModel.AllText(woodEx).Any(t => t.Contains("Resin")),
      "K1 woodcutting: one bar, each wood its total (before Hearthwoven, no baseline: counter 2\u00A0400 minus 140 exact, + the 140; 0.7: drawn as one sum, no faded part); no separate exact list; resin is not wood");
var withBase = Sample(); withBase.Baseline = new Dictionary<string, Dictionary<string, float>> { ["pickedUp"] = new Dictionary<string, float> { ["$item_wood"] = 1000, ["$item_finewood"] = 300, ["$item_stone"] = 1500 } };
SessionEvents.Add(withBase.Events.PickedUp, "$item_wood", 140); SessionEvents.Add(withBase.Events.PickedUp, "$item_finewood", 12); SessionEvents.Add(withBase.Events.PickedUp, "$item_copperore", 18);
var baseBar = Find(Show(withBase, Chapter.Deeds, "woodcutting"), "composition");
Check(baseBar.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Wood=1\u00A0140", "Finewood=312" }) && PanelModel.BroughtIn(withBase, "wood")["$item_wood"] == (1000, 140) && baseBar.Items[0].Fraction2 == 0 &&
      baseBar.Value == "1\u00A0452" && PanelModel.BroughtInTotal(withBase, "wood") == 1452 && PanelModel.BroughtInTotal(withBase, "mining") == 1518,
      "K1 baseline: total = the game counter when Hearthwoven first ran + the exact count since; the counter's later growth (2\u00A0400) is never added; Together uses the same total");
// an install from before the baseline: 50 wood counted exactly, then the baseline (the game counter, which partly holds
// those 50) was taken; the solid part starts at 0 and grows only with new pickups
var older = new PanelInput { Baseline = new Dictionary<string, Dictionary<string, float>> { ["pickedUp"] = new Dictionary<string, float> { ["$item_wood"] = 1000 } },
                             ExactAtBaseline = new Dictionary<string, Dictionary<string, float>> { ["pickedUp"] = new Dictionary<string, float> { ["$item_wood"] = 50 } }, Events = new SessionEvents() };
SessionEvents.Add(older.Events.PickedUp, "$item_wood", 50);
var olderBefore = PanelModel.BroughtIn(older, "wood")["$item_wood"];
SessionEvents.Add(older.Events.PickedUp, "$item_wood", 7);
var olderAfter = PanelModel.BroughtIn(older, "wood")["$item_wood"];
Check(olderBefore.before == 1000 && olderBefore.exact == 0 && olderAfter.before == 1000 && olderAfter.exact == 7 && PanelModel.BroughtInTotal(older, "wood") == 1007,
      "K1 an older install: 50 exact before the baseline show as baseline + 0 solid (not 1\u00A0050), then only new pickups add (1\u00A0007)");
var allExact = new PanelInput { Baseline = new Dictionary<string, Dictionary<string, float>> { ["pickedUp"] = new Dictionary<string, float>() }, Events = new SessionEvents(), ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 3 } };
SessionEvents.Add(allExact.Events.PickedUp, "$item_wood", 40);
var exactBar = Find(PanelModel.Build(allExact, new PanelState { Page = { [Chapter.Deeds] = "woodcutting" } }), "composition");
Check(exactBar.Value == "40" && exactBar.Note == null && exactBar.Text == null && exactBar.Src == "character" && exactBar.Items.All(i => i.Fraction2 == 0),
      "K1 a character made with Hearthwoven: all counted exactly, no faded note, no \"Earlier counts\" (0.7 rule A.1: still the character's whole count)");
var chop = Sample(); chop.Events = new SessionEvents();
foreach (var (k, v) in new[] { ("FirTree", 10f), ("FirTree_log", 5f), ("FirTree_log_half", 3f), ("Beech1", 64f), ("beech_log_half", 2f), ("Pinetree_01", 4f), ("SwampTree1_log", 1f), ("ModTree3_log", 2f) })
    SessionEvents.Add(chop.Events.ChopHits, k, v);
var chopRows = PanelModel.Content(Show(chop, Chapter.Deeds, "woodcutting")).SkipWhile(b => !(b.Kind == "section" && b.Title == "Axe hits per tree")).Skip(1).First();
Check(chopRows.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Beech=66", "Fir=18", "Pine=4", "Mod Tree=2", "Ancient tree=1" }),
      "I axe hits per tree: logs and log halves fold into their tree kind, plain tree names (a mod's tree without its suffixes)");
Check(chopRows.Kind == "ranking" && chopRows.Columns == 2 && chopRows.Items.Select(i => i.Icon).SequenceEqual(new[] { "vocab:tree-beech", "vocab:tree-fir", "vocab:tree-pine", "", "vocab:tree-ancient" }) &&
      chopRows.Items[0].Fraction == 1f && Math.Abs(chopRows.Items[1].Fraction - 18f / 66f) < 1e-4 && chopRows.Src == "pc" && chopRows.Items.All(i => i.Src == "pc"),
      "fix2 2 axe hits per tree as approved (r2-refine-woodcutting): the tree's picture by kind (Codex tree-*), a bar on one scale, the number, two across (0.7: no tight zones); a mod's tree keeps an empty place");
var pick = Sample(); pick.Events = new SessionEvents();
foreach (var (k, v) in new[] { ("rock4_copper", 120f), ("MineRock_Tin", 40f), ("rock1_mountain", 30f), ("silvervein", 20f), ("mudpile", 6f), ("Leviathan", 3f) })
    SessionEvents.Add(pick.Events.PickaxeHits, k, v);
var pickRows = PanelModel.Content(Show(pick, Chapter.Deeds, "mining")).SkipWhile(b => !(b.Kind == "section" && b.Title == "Pickaxe hits per rock")).Skip(1).First();
Check(pickRows.Kind == "ranking" && pickRows.Columns == 2 && pickRows.Items.Select(i => i.Icon).SequenceEqual(new[] { "item:CopperOre|vocab:rock-copper", "item:TinOre|vocab:rock-tin", "item:Stone|vocab:rock-stone", "item:SilverOre|vocab:rock-silver", "item:IronScrap|vocab:rock-scrap", "item:Chitin|vocab:rock-stone" }) &&
      pickRows.Items.Select(i => i.Value).SequenceEqual(new[] { "120", "40", "30", "20", "6", "3" }),
      "fix2 2 pickaxe hits per rock: the rock's picture (each rock shows the game's own ore or stone icon, Codex's rock-* sketch as fallback: copper, tin, stone, silver, scrap pile, the Leviathan's chitin), a bar, the number, two columns");
// ---------- T3: trees felled, the game counter when Hearthwoven first ran (faded) + every tree counted since (solid) ----------
Block TreeHero(PanelView v) { var h = Find(v, "hero"); return h == null ? null : new[] { h }.Concat(h.Items ?? new List<Block>()).FirstOrDefault(n => n.Title.Contains("felled")); }
List<Block> TreeSection(PanelView v) => PanelModel.Content(v).SkipWhile(b => !(b.Kind == "section" && b.Title == "Trees felled per tree")).Skip(1).TakeWhile(b => b.Kind != "section").ToList();
// "Earlier counts may be incomplete." once on the page, with trees felled and wood brought in both marked (Joost 2026-10-09; 0.8 layout D+ keeps it
// in About these numbers, a fellow's book has none: PropertyTests)
bool IncompleteOnce(PanelView v) => TimesSaid(v, PanelModel.EarlierIncomplete) == 1;
var noTreeBase = Show(input, Chapter.Deeds, "woodcutting");
Check(TreeHero(noTreeBase)?.Value == "410" && IncompleteOnce(noTreeBase) && TreeSection(noTreeBase).Count == 0 &&
      !PanelModel.AllText(noTreeBase).Any(t => t.Contains("own area") || t.Contains("owner")),
      "T3 no baseline (local totals not loaded): the game's counter alone, with the one line that it misses trees; never 'own area'");
var treesIn = Sample(); treesIn.Events = new SessionEvents();
treesIn.Baseline = new Dictionary<string, Dictionary<string, float>> { ["treesFelled"] = new Dictionary<string, float> { ["Tree"] = 410 } };
treesIn.Character["Tree"] = 500;   // the game counter grew since (trees in an area this PC hosts): never added
foreach (var (k, v) in new[] { ("Beech1", 12f), ("Birch2_aut", 3f), ("FirTree", 8f) }) SessionEvents.Add(treesIn.Events.Felled, k, v);
var treesPage = Show(treesIn, Chapter.Deeds, "woodcutting");
var treeStrip = Find(treesPage, "strip"); var treeRows = TreeSection(treesPage).FirstOrDefault();   // zones: the layered whole on stone, the rows under their heading in the ember zone
var treesHead = PanelModel.Content(treesPage).SingleOrDefault(b => b.Kind == "section" && b.Title == "Trees felled per tree");
Check(TreeHero(treesPage)?.Value == "433" && TreeHero(treesPage).Src == "character" && treeStrip == null && IncompleteOnce(treesPage) &&   // 0.7: one total, "Earlier counts may be incomplete." once
      Says(treesPage, PanelModel.TreesMissedBefore) && treesHead?.Value == "23" && Zoned.Says(treesPage, treesHead) &&
      treeRows?.Kind == "ranking" && treeRows.Columns == 2 && treeRows.Items[0].Icon == "vocab:tree-beech" && treeRows.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Beech=12", "Fir=8", "Birch=3" }),
      "T3 layers: 410 before Hearthwoven + 23 felled since = 433 in all, the 23 as the per-tree heading with its own date label; the game counter's later growth (500) never added; per tree kind since install");
Check(PanelModel.DeedCards(PanelModel.Titles(treesIn)).Items.Single(c => c.Title == "Woodcutter").Value == "433", "T3 the Woodcutter card shows the same 433 as the page");
var treesNew = new PanelInput { Character = new Dictionary<string, float>(), Events = new SessionEvents(),
                                Baseline = new Dictionary<string, Dictionary<string, float>> { ["treesFelled"] = new Dictionary<string, float>() } };
SessionEvents.Add(treesNew.Events.Felled, "Beech1", 5);
var treesNewPage = Show(treesNew, Chapter.Deeds, "woodcutting");
Check(TreeHero(treesNewPage)?.Value == "5" && TreeHero(treesNewPage).Src == "character" &&   // 0.7 rule A.1: the character's whole count, also when all of it was counted here TreeSection(treesNewPage).Single().Kind == "ranking" && TreeSection(treesNewPage)[0].Items.Single().Value == "5" &&
      PanelModel.Titles(treesNew).Any(t => t.Title == "Woodcutter"),
      "T3 a character made with Hearthwoven: every tree counted since install (no faded part, no key), and they earn Woodcutter");
var treesKept = new PanelInput { Character = new Dictionary<string, float> { ["Tree"] = 7 }, Events = new SessionEvents(),
                                 Baseline = new Dictionary<string, Dictionary<string, float>> { ["treesFelled"] = new Dictionary<string, float>() } };
Check(PanelModel.Titles(treesKept).Any(t => t.Title == "Woodcutter"), "T3 titles are never taken away: the game's own counter still earns Woodcutter");
// a fellow's page never says the earlier-counts line (their copy has no box): PropertyTests, every page of every fellow's book
// titles are never taken away (K6): the game's MineHits still earns Stonebreaker; the page shows no number from it
var oldMiner = new PanelInput { Character = new Dictionary<string, float> { ["MineHits"] = 650 }, Events = new SessionEvents() };
var oldMinerPage = Show(oldMiner, Chapter.Deeds, "mining");
Check(PanelModel.Titles(oldMiner).Any(t => t.Title == "Stonebreaker") && !PanelModel.AllText(oldMinerPage).Any(t => t.Contains("650")),
      "K6 titles: 650 game pickaxe hits and none since install keep Stonebreaker; the page shows no game hits number");
// titles-grid (Joost in game, 0.6.5): that Stonebreaker card said its descriptor where the others say their count; it says the work and when
oldMiner.IsSelf = true; oldMiner.InstalledUtc = new DateTime(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);
var minerCard = PanelModel.VisibleFeats(Show(oldMiner, Chapter.Feats, PanelModel.TitlesPageId)).Single(c => c.Title == "Stonebreaker");
Check(minerCard.Tone == PanelModel.FeatTone && minerCard.Text.Contains("before " + PanelModel.RecordDate(oldMiner, oldMiner.InstalledUtc.Value)) && minerCard.Text.Contains("ore") && minerCard.Items[0].Items.Any(r => r.Kind == "rule" && r.Title == minerCard.Text) && !minerCard.Text.Contains("650"),
      "titles: a title only the game's own count holds says the work and the date before which it was done (card and detail), still no game number: " + minerCard.Text);
var miningEx = Show(input, Chapter.Deeds, "mining");
Check(!PanelModel.Content(miningEx).Any(b => b.Kind == "section" && b.Title.Contains("counted exactly")) && Find(miningEx, "composition").Items.Any(i => i.Id == "$item_silverore" && i.Fraction2 == 0),
      "K1 mining: no separate exact list; an ore only Hearthwoven counted is all solid");

// ---------- titles: one table ----------
var titles = PanelModel.Titles(input);
string Line(List<PanelModel.TitleRow> ts, string title, int i) => ts.Single(t => t.Title == title).Lines[i].Key;
string Src(List<PanelModel.TitleRow> ts, string title, int i) => ts.Single(t => t.Title == title).Lines[i].Value;
Check(Line(titles, "Trailfinder", 0) == "184 km traveled" && Src(titles, "Trailfinder", 0) == PanelModel.SourceCharacter, "source: a lifetime counter says since this character was made");
Check(Line(titles, "Woodcutter", 0) == "64 axe hits" && Src(titles, "Woodcutter", 0) == PanelModel.SourceSession &&
      Line(titles, "Woodcutter", 1) == "410 trees felled" && Src(titles, "Woodcutter", 1) == PanelModel.SourceCharacter, "titles: several lines, each with its own source");
bool Reads(string line, string count, string what) => line.Contains(count) && line.Contains(what);
Check(Reads(Line(titles, "Mapmaker", 0), "2 times", "Map shared") && Reads(Line(titles, "Mender", 0), "9 ", "repairs") &&
      Reads(Line(titles, "Wallwarden", 0), "42 ", "defenses") && Reads(Line(titles, "Bossbane", 0), "3 ", "boss fights"),
      "titles: the five new titles read the new measures and profile counters");
Check(!titles.Any(t => t.Title == "Tidecatcher" || t.Title == "Beastkeeper" || t.Title == "Waymate"), "titles: zero counters earn no title, and no Waymate");
Check(PanelModel.SagaTitles.Single(t => t.Title == "Hallwright").Descriptor == "Pieces raised with the hammer" && PanelModel.SagaTitles.Single(t => t.Title == "Tidecatcher").Descriptor == "Fish caught on the line" &&
      PanelModel.SagaTitles.Single(t => t.Title == "Storekeeper").Descriptor == "Carts pulled home" && PanelModel.SagaTitles.Single(t => t.Title == "Beastkeeper").Descriptor == "Creatures tamed, petted and led",
      "titles: descriptors from the naming review");

// ---------- Battle ----------
var battle = Show(input, Chapter.Battle, null, s => PanelModel.ToggleFacet(s, PanelModel.BattleOverviewFilter, "biome", "Swamp"));
var dmgPage = Show(input, Chapter.Battle, "damage", s => { PanelModel.ToggleFacet(s, PanelModel.BattleDamageFilter, "biome", "Swamp"); s.View["Battle/damage/view"] = "type"; });   // By type (By weapon is the default)
var dmgGrid = Find(dmgPage, "damagegrid");
var battleStrip = Find(battle, "biomes");
Check(battle.Heading == "Battle" && Says(battle, "Swamp", PanelModel.WindowLabel(TimeWindow.Session).ToLowerInvariant()) && battleStrip.Value == "434" && battleStrip.Value2 == "266" && Find(battle, "hero") == null,
      "battle: one line says the chosen biome and window; their dealt and received totals ride in the strip's legend (slice 3: were the heading)");
Check(!PanelModel.Content(battle).Any(b => b.Kind == "section" && b.Title == "Hits") && !PanelModel.AllText(battle).Any(t => t.Contains("5\u00A0400") || t.Contains("1\u00A0300")) &&
      PanelModel.Content(Show(input, Chapter.Battle, "foes", s => s.Window = TimeWindow.SinceInstall)).SelectMany(b => new[] { b }.Concat(b.Items ?? new List<Block>())).Any(b => (b.Kind == "hero" || b.Kind == "number") && b.Value == "5\u00A0400" && b.Title == "hits on foes" && b.Src == "character"),
      "C3 battle overview: the lifetime hit counts do not sit under the window chips; hits on foes live on Foes (your character's count here: no baseline in this sample)");
var byBiome = Find(battle, "biomes");
Check(!PanelModel.Content(battle).Any(b => b.Kind == "bars") && byBiome.Items.Select(i => i.Title + "=" + i.Value + "/" + i.Value2).SequenceEqual(new[] { "Meadows=108/", "Black Forest=420/310", "Swamp=434/266" }) &&
      byBiome.Items.Single(i => i.Selected).Title == "Swamp" && byBiome.Source == "measured",
      "C3 battle overview: damage dealt and received per biome on the strip only (the chosen one lit), no biome bars repeating it");
Check(!PanelModel.AllText(battle).Concat(PanelModel.AllText(dmgPage)).Any(t => t.IndexOf("dealt after", StringComparison.OrdinalIgnoreCase) >= 0), "C3: no dealt-after-resistance number (owner trap, see FEEDBACK C3)");
Check(dmgGrid != null && dmgGrid.Value == "434" && dmgGrid.Items.Skip(1).Select(t => t.Title + "=" + t.Value).SequenceEqual(new[] { "Slash=434" }) && !PanelModel.Content(dmgPage).Any(b => b.Kind == "bars"),
      "battle: Damage shows what you dealt, by type and weapon (r4battle-damage-type); received lives on the overview and Defense");
Check(PanelModel.ScrollRoom(508, 500) == 0 && PanelModel.ScrollRoom(511.9f, 500) == 0 && PanelModel.ScrollRoom(512, 500) == 12 && PanelModel.ScrollRoom(400, 500) == 0,
      "B9 scrolling: under 12 px of overflow there is no scroll range (no jump on the next notch, no fade)");
Check(PanelModel.NiceMax(580) == 600 && PanelModel.NiceMax(1410) == 1500 && PanelModel.NiceMax(100) == 100 && PanelModel.NiceMax(7) == 8, "bars: the scale ends on a round number (580 -> 600, as in the kit's assembly)");
Check(battle.HasFilters && battle.Windows.Select(w => w.Id).SequenceEqual(PanelModel.AllWindows.Select(w => w.ToString())) && battle.Windows.Select(w => w.Label).SequenceEqual(PanelModel.AllWindows.Select(PanelModel.WindowShort)) &&
      Says(battle, PanelModel.WindowLabel(TimeWindow.Session).ToLowerInvariant()), "battle: all nine window chips, short; the page says the chosen window in full");
var deathRows = Find(battle, "deathrows");
Check(deathRows?.Items.Select(d => d.Value + " " + d.Title + "|" + d.Icon).SequenceEqual(new[] { "2 deaths from poison|vocab:dmg-poison" }) == true && deathRows.Items[0].Colour == "#3ccf6e" &&
      Find(Show(input, Chapter.Battle), "deathrows").Items.Select(d => d.Value + " " + d.Title).SequenceEqual(new[] { "2 deaths from poison", "1 death from blunt" }),
      "battle overview: deaths in aligned rows, per killer and damage type (a damage-over-time is never pinned on a newer attacker), the type's icon when the game gives no trophy");
Check(Find(battle, "note", b => b.Tone == "hint") != null, "battle: the resistance hint is on the overview");
var foes = Show(input, Chapter.Battle, "foes", s => s.Window = TimeWindow.SinceInstall);
Check(foes.Heading == "Foes" && Find(foes, "hero")?.Value == "860" && Find(foes, "hero").Title == "foes defeated" && Find(foes, "foetable").Items[0].Title == "Draugr" && foes.HasFilters, "foes on All: lifetime kills in the heading, the foes struck since install; the window chips (HISTORY-06)");
var deathsPage = Show(input, Chapter.Battle, "deaths");
Check(deathsPage.Heading == "Deaths" && Find(deathsPage, "deaths").Items[0].Title == "Blob" && Find(deathsPage, "deaths").Items[0].Text.EndsWith(" 00:18 · Swamp") && !Find(deathsPage, "deaths").Items[0].Text.StartsWith("today"), "deaths: listed with a dated local time (never \"today\") and biome");
Check(PanelModel.Build(new PanelInput(), new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = "deaths" } }).Heading == PanelModel.NoDeaths, "deaths: none says No deaths yet");
Program.BattleChecks(input, Check);   // ch-battle: test-panel/BattleTests.cs

// ---------- Skills ----------
var skillsOverview = Show(input, Chapter.Skills);
Check(skillsOverview.List.Select(l => l.Id).SequenceEqual(new[] { "overview", "Axes", "Blocking", "Cooking", "Fishing", "Jump", "Pickaxes", "Run", "Swim", "Swords", "WoodCutting" }) && skillsOverview.List.Skip(1).All(l => l.Icon == "skill:" + l.Id) && skillsOverview.Page == "overview",
      "skills: Overview first, then one row per skill with the game's skill icon");
var practisedView = Show(input, Chapter.Skills, null, s => s.View["Skills/overview/view"] = "practised");
var practised = PanelModel.Content(practisedView).First(b => b.Kind == "ranking");   // no "Practiced most" heading: the chip says Practiced (J)
Check(Math.Abs(practised.Items.Sum(i => i.Fraction) - 1) < 1e-3 && Math.Abs(practised.Items.First().Fraction - 0.42f) < 0.01f && practised.Items.All(i => PanelModel.ParseCount(i.Value.Replace(" %", "")) == Math.Round(i.Fraction * 100)),
      "skills practised: each bar is its share of all the practice (the track is 100 %), so the top skill at 42 % does not fill it");
Check(!PanelModel.Content(skillsOverview).Any(b => b.Kind == "section" && (b.Title == "Levels now" || b.Title == "Practiced most")) && Find(practisedView, "ladders") == null && Find(skillsOverview, "ladders").Items.SelectMany(g => g.Items).Select(i => i.Id + "=" + i.Value).OrderBy(x => x)
      .SequenceEqual(new[] { "Axes=38", "Blocking=42", "Cooking=18", "Fishing=15", "Jump=28", "Pickaxes=22", "Run=55", "Swim=9", "Swords=21", "WoodCutting=34" }) && practised.Items.First().Title == "Axes" && practised.Items.First().Value == "42 %" && practised.Items.Select(i => i.Value).SequenceEqual(new[] { "42 %", "27 %", "18 %", "13 %" }) && practised.Items.Sum(i => int.Parse(i.Value.TrimEnd(' ', '%'))) == 100 && Find(practisedView, "hero")?.Value == "42 %" && practised.Src == "pc" && !PanelModel.Content(practisedView).Any(b => b.Items != null && b.Items.Any(i => i.Value == "14.5")),
      "C4 skills overview: every level once, on the ladders (no 'Levels now' list repeating them); practised most since install (measured) in its own view of the switch");
var bestSkill = Find(skillsOverview, "hero");
Check(bestSkill != null && bestSkill.Value == "55" && bestSkill.Title == "Run" && bestSkill.Note == "your highest skill" && bestSkill.Src == "character" && Find(skillsOverview, "ladders").Items.Single(g => g.Title == "Move").Items.Select(i => i.Id).SequenceEqual(new[] { "Jump", "Run", "Swim" }) && Find(Show(new PanelInput { SkillLevels = new Dictionary<string, float> { ["Run"] = 5 } }, Chapter.Skills), "hero") == null,
      "skills overview: the highest level is the hero (one skill alone: none), Jump and Swim are on the ladders as on Voyages > On foot");
var many = new PanelInput { SkillLevels = Enumerable.Range(1, 12).ToDictionary(n => "Skill" + n, n => (float)n) };
Check(Find(PanelModel.Build(many, new PanelState { Chapter = Chapter.Skills }), "ladders").Items.SelectMany(g => g.Items).Count() == 12, "C4 skills overview: every skill gets its ladder, not only the top eight");
var skills = Show(input, Chapter.Skills, "Axes");
Check(Find(skills, "ladder").Value == "38" && Find(skills, "ladder").Items.Single(i => i.Kind == "practice").Value == "42 %" && !skills.Blocks.Any(b => b.Kind == "stat"),
      "skills: level now and practice share (counted from install), once each, on the skill's ladder (no stats repeating them)");
var unpractised = new PanelInput { SkillLevels = new Dictionary<string, float> { ["Bows"] = 12f }, Events = new SessionEvents() };
Check(!Find(PanelModel.Build(unpractised, new PanelState { Chapter = Chapter.Skills, Page = { [Chapter.Skills] = "Bows" } }), "ladder").Items.Any(i => i.Kind == "practice"), "skills: no practice line when there was none (no '0 practice')");
var colours = PanelModel.PersonColors(new[] { "Rowan", "Edda", "Tor", "Finch", "Asa", "Bo", "Gunn", "Ylva", "edda" });
Check(colours.Count == 8 && colours.Values.Distinct().Count() == 8 && Show(input, Chapter.Voyages).PersonColors["Edda"] != Show(input, Chapter.Voyages).PersonColors["Finch"], "people: eight players get eight different colours, stable by name");
var withYou = PanelModel.PersonColors(new[] { "Edda", "Asa", "Rowan", "Tor" }, "Rowan");
Check(withYou["Rowan"] == 0 && withYou["Asa"] == 1 && withYou["Edda"] == 2 && withYou["Tor"] == 3 && PanelModel.PlayerPalette[0] == "#d6ccb3" && PanelModel.DarkTextOn(0) && !PanelModel.DarkTextOn(1),
      "people: you are always slot 0 (birch), the others alphabetically after you (Nordic earth palette)");

// ---------- Company: Fireside, Together, Food shared, Gear shared (test-panel/CompanyTests.cs) ----------
CompanyChecks(input, now, Check);
FiresideChecks(now, Check);   // Fireside's threads never cross or run together where they need not; the counts on them touch nothing (test-panel/FiresideTests.cs)
CompanyOpenChecks(now, Check);   // 0.9: the first Company opening never blocks a frame for long (Fireside on a worker); Armor sets: About and the feat link (test-panel/CompanyOpenTests.cs)
var company = Show(input, Chapter.Company);
Check(!company.Keys.Contains("[A/D] Direction") && !deeds.Keys.Contains("[A/D] Direction") && deeds.Keys.Contains("[W/S] Page") && deeds.Keys.Last() == "[H/Tab/Esc] Close",
      "keys: the footer lists only bindings that work on this screen");
Check(deeds.Keys.Contains("[T] About") && !deeds.Keys.Any(k => k.StartsWith("[I]")), "ISC-1 keys: the info key is T (I opens the AdventureBackpacks backpack)");
// Joost 2026-10-08: where a number comes from is not interesting to most players; T opens one About page instead of a line per page
var about = Show(input, Chapter.Battle, "damage", s => s.ShowAbout = true);
Check(deeds.Keys.SequenceEqual(new[] { "[Q/E\u00b7A/D] Chapter", "[W/S] Page", "[F] View", "[Backspace] Back", "[Y] Numbers", "[T] About", "[H/Tab/Esc] Close" }) || deeds.Keys.SequenceEqual(new[] { "[Q/E\u00b7A/D] Chapter", "[W/S] Page", "[F] View", "[Backspace] Back", "[T] About", "[H/Tab/Esc] Close" }) || deeds.Keys.SequenceEqual(new[] { "[Q/E\u00b7A/D] Chapter", "[W/S] Page", "[Backspace] Back", "[T] About", "[H/Tab/Esc] Close" }),
      "keys: the footer names every key that works on the page, short: " + string.Join("  ", deeds.Keys));
// diff-05: About as designed (proto/vocab-about.png): its own list, no chapter tab lit; How it counts = the three source
// cards, the Since when timeline and the four promises on the plate; What it reads and Sharing carry the honest words
var aboutPlate = PanelModel.PlateOf(about);
Check(about.Heading == "How Hearthwoven counts" && about.ListTitle == "About" && about.List.Select(l => l.Label).SequenceEqual(new[] { "How it counts", "What it reads", "Sharing" }) &&
      about.List[0].Selected && about.Chapters.All(c => !c.Selected) && aboutPlate != null &&
      aboutPlate.Items.Select(b => b.Kind).SequenceEqual(new[] { "origins", "sincewhen", "promises" }) && !about.HasFilters && about.Badges.Count == 0,
      "ISC-6 About: its own list, no chapter lit; How it counts = source cards, Since when, promises on the plate");
Check(aboutPlate.Items[0].Items.Select(c => c.Icon + " " + c.Title + " | " + c.Value).SequenceEqual(new[] {
          "vocab:src-stone Your character's own count | since you made this character", "vocab:src-hearth Hearthwoven on this PC | from " + PanelModel.RecordDate(input, PanelModel.StartOf(input, null).Value), "vocab:src-fellows Your fellow players' PCs | when they share too" }) &&
      aboutPlate.Items[0].Items.All(c => !string.IsNullOrEmpty(c.Text)) &&
      aboutPlate.Items[1].Items.Select(r => r.Tone).SequenceEqual(new[] { "character", "pc", "fellows" }) && aboutPlate.Items[1].Text == "Rowan made " + PanelModel.RecordDate(input, input.CharacterMade.Value) && aboutPlate.Items[1].Value == "installed " + PanelModel.RecordDate(input, PanelModel.StartOf(input, null).Value),
      "About: three source cards with icon, a short since line and examples; the timeline made (date) -> installed (date) -> now; no dates the mod does not know (a fellow's book: none)");
Check(aboutPlate.Items[2].Items.Select(p => p.Title).SequenceEqual(new[] { "Only reads", "Your world stays untouched", "Optional for every player", PanelModel.OtherModsTitle }) && aboutPlate.Items[2].Items[3].Text == PanelModel.OtherModsLine &&
      aboutPlate.Items[2].Items.All(p => p.Icon.StartsWith("vocab:promise-")), "About: the four promises in one row; other mods' items show too, with its one true line (no 'works with any mod')");
Check(about.Keys.SequenceEqual(new[] { "[Q/E\u00b7A/D] Chapter", "[W/S] Page", "[T/Esc] Back", "[H/Tab] Close" }), "ISC-6 About: T or Esc goes back, H or Tab closes: " + string.Join("  ", about.Keys));
// Tab as the filter key (a player who set Panel.FilterKey to Tab): Tab keeps the filters, so the footer does not say Tab closes
var tabFilter = Show(input, Chapter.Battle, "damage", s => s.FilterKey = "Tab");
Check(tabFilter.Keys.Contains("[Tab] Filter") && tabFilter.Keys.Contains("[H/Esc] Close"), "filter key Tab: the footer says [Tab] Filter and [H/Esc] Close: " + string.Join("  ", tabFilter.Keys));
// one-time key layout migration (PanelUi.MigrateFilterKey): an old Tab default moves to K once; a choice made after that is kept
Check(PanelUi.MigrateFilterKey(0, UnityEngine.KeyCode.Tab) == (1, UnityEngine.KeyCode.K) && PanelUi.MigrateFilterKey(0, UnityEngine.KeyCode.G) == (1, UnityEngine.KeyCode.G) &&
      PanelUi.MigrateFilterKey(1, UnityEngine.KeyCode.Tab) == (1, UnityEngine.KeyCode.Tab), "key layout: (0, Tab) -> (1, K); (0, G) -> (1, G); (1, Tab) stays Tab");
var aboutState = new PanelState { ShowAbout = true }; PanelModel.StepList(aboutState, about, 1);
var aboutReads = PanelModel.Build(input, aboutState); PanelModel.StepList(aboutState, aboutReads, 1);
var aboutSharing = PanelModel.Build(input, aboutState);
Check(aboutState.ShowAbout && aboutReads.Heading == "What it reads" && aboutSharing.Heading == "Sharing" && aboutState.Page.Count == 0, "About: W/S step its own list and keep About open, the chapter pages untouched");
var aboutText = string.Join(" ", PanelModel.AllText(about).Concat(PanelModel.AllText(aboutReads)).Concat(PanelModel.AllText(aboutSharing)));
Check(PanelModel.AboutRows.Any(r => r.label == "Feats" && r.text.StartsWith("A moment worth telling") && r.text.Contains("Unsung") && r.text.Contains("never a rank")) &&
      PanelModel.Content(aboutReads).SelectMany(b => b.Items ?? new List<Block>()).Any(r => r.Kind == "readrow" && r.Title == "Feats"),
      "About > What it reads: one row says what a feat is, that none is a rank, and what Unsung means");
Check(aboutSharing.Heading == "Sharing" && PanelModel.Content(aboutSharing).SelectMany(b => b.Items ?? new List<Block>()).Where(b => b.Kind == "readrow").Select(b => b.Title).SequenceEqual(new[] { "What fellow players see", "Turn sharing on or off", "What stays private", "Where your counts go" }) &&
      aboutText.Contains("You see theirs the same way") && aboutText.Contains("ShareWithGroup") && aboutText.Contains("nobody sees yours") && aboutText.Contains("never see where you died, which worlds you played, or the name of a player who does not share") && aboutText.Contains("Remove Hearthwoven.dll") && !aboutText.Contains("(co-op)") &&
      PanelModel.AboutRows.Any(r => r.label == "Feats" && r.text.Contains("A title is earned by its own count") && r.text.Contains("no combined score")) && PanelModel.AboutRows.All(r => r.label != "Titles"),
      "ISC-6 About > Sharing: what fellow players see, how to switch it (the setting and where), what stays private (0.8.2: the names of players who do not share too), where the counts go, how to uninstall; the Titles line moved to What it reads, under Feats");
// K5 (SOURCES.md fix 4): says once what comes from the character (every world; misses co-op hits in a fellow player's area and pickups onto a
// carried stack) and that Hearthwoven counts since install, except Battle's time windows; never "this book shows the current session"
Check(aboutText.Contains("in every world") && aboutText.Contains("a fellow player's PC hosts") && aboutText.Contains("onto a stack you already carry") &&
      aboutText.Contains("Only these start at install") && aboutText.Contains("look back from now") && !aboutText.Contains("Every number counts since install") && !aboutText.Contains("current session") && !aboutText.Contains("true amount") &&
      !aboutText.ToLowerInvariant().Contains("friend") && !aboutText.Contains("this session") &&
      PanelModel.AboutRows.All(r => r.text.Length <= 280 && !r.text.Contains('—') && !r.text.Contains('–')),
      "K5 About: the character's counts span every world and miss co-op hits in a fellow player's area and pickups onto a carried stack; Hearthwoven counts since install except Battle's windows; short, no dashes, never 'friend'");

// ---------- navigation ----------
var nav = new PanelState();
PanelModel.StepChapter(nav, -1);
Check(nav.Chapter == Chapter.Skills, "keys: Q from the first chapter wraps to the last");
nav = new PanelState(); var v0 = PanelModel.Build(input, nav); PanelModel.StepList(nav, v0, -1);
var wentUp = nav.PageOf(Chapter.Deeds) == "recent"; PanelModel.StepList(nav, PanelModel.Build(input, nav), -1);
Check(wentUp && nav.PageOf(Chapter.Deeds) == "taming", "keys: W from the Overview goes up to Recent, and from the top of the list wraps to the bottom");
PanelModel.StepList(nav, PanelModel.Build(input, nav), 1);
Check(nav.PageOf(Chapter.Deeds) == "recent", "keys: S from the bottom wraps to the top");

// ---------- a fellow player's shared snapshot renders the same chapters ----------
string EddaSnapshot()
{
    var st = new PlayerProfile.PlayerStats[1]; st[0] = new PlayerProfile.PlayerStats();
    st[0][PlayerStatType.DistanceTraveled] = 52300f; st[0][PlayerStatType.HarvestCrop] = 88f; st[0][PlayerStatType.CraftFood] = 210f;
    var ev = new SessionEvents { Blocks = 12, Parries = 3 };
    SessionEvents.Add(ev.AteFoodMadeBy, "Rowan|QueensJam", 2); SessionEvents.Add(ev.AteFoodMadeBy, "Edda|Bread", 5);
    SessionEvents.Add(ev.SailedWith, "Rowan", 1260); SessionEvents.Add(ev.SailedUnderHelmOf, "Tor", 300);
    SessionEvents.Add(ev.SkillPractice, "Cooking", 7.5f); SessionEvents.Add(ev.EquippedGearMadeBy, "Rowan|AxeBronze", 2); SessionEvents.Add(ev.PickedUp, "$item_wood", 33);
    var log = new EventLog();
    log.AddDamage(now.AddMinutes(-30), "Mountain", true, "Wolf", "Spears", D("pierce", 40));
    log.AddDamage(now.AddMinutes(-25), "Mountain", false, "Freezing", "Freezing", D("frost", 18));
    log.AddDeath(now.AddMinutes(-25).AddSeconds(2), "Mountain", 300, -120);
    var tally = new DamageTally(); tally.AddDealt("Wolf", "Spears", D("pierce", 40)); tally.AddTaken("Freezing", "Freezing", D("frost", 18));
    var json = Snapshot.Build("0.2.0", 77L, "Edda", st, new[] { new Snapshot.SkillInfo { Name = "Cooking", Level = 31.2f } }, "Midgard", tally, "s1", ev, log, share: true);
    return GroupShare.SharedCopy(json);
}
var eddaJson = EddaSnapshot();
var edda = PanelInput.FromSnapshot(eddaJson);
Check(edda != null && !edda.IsSelf && edda.PlayerName == "Edda" && edda.PlayerId == 77 && !eddaJson.Contains("\"x\":"), "snapshot: a shared copy (no death positions) is read back as panel input");
edda.NowUtc = now; edda.ToLocal = t => t.AddHours(2); edda.ViewerName = "Rowan"; edda.DisplayName = input.DisplayName;
Check(edda.SkillLevels["Cooking"] == 31.2f && edda.Character["CraftFood"] == 210f, "snapshot: profile counters and skills come back");
Check(eddaJson.Contains("\"pickedUp\"") && edda.Events.PickedUp.TryGetValue("$item_wood", out var eddaWood) && eddaWood == 33f, "C7 snapshot: measured pickups travel in the existing snapshot and the shared copy");
var eddaTitles = PanelModel.Titles(edda);
Check(Line(eddaTitles, "Trailfinder", 0) == "52.3 km traveled" && Src(eddaTitles, "Shieldbearer", 0) == "measured in last shared", "snapshot: their values keep their source labels");
foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
{
    var a = PanelModel.Build(input, new PanelState { Chapter = ch }); var b = PanelModel.Build(edda, new PanelState { Chapter = ch });
    Check(a.Chapters.Select(c => c.Label).SequenceEqual(b.Chapters.Select(c => c.Label)) && b.Blocks.Count > 0, "snapshot: chapter " + ch + " renders for a fellow player");
}
// a fellow player's book never says "you" or "your" about them (the viewer's own chip and the Company's "(you)" are the only places)
{
    var sampleSelf = PanelSample.Full(now); var sampleFellows = PanelSample.Fellows(now);
    foreach (var f in sampleFellows) { f.ViewerName = sampleSelf.PlayerName; f.PlayerNames = sampleSelf.PlayerNames; f.Fellows = sampleFellows.Where(o => o != f).Concat(new[] { sampleSelf }).ToList(); f.ItemKind = sampleSelf.ItemKind; f.GatherKind = sampleSelf.GatherKind; f.PieceKind = sampleSelf.PieceKind; f.ItemToken = sampleSelf.ItemToken; f.CropOf = sampleSelf.CropOf; f.ItemType = sampleSelf.ItemType; f.Foe = sampleSelf.Foe; f.Arrows = sampleSelf.Arrows; }
    sampleSelf.Fellows = sampleFellows;
    var yous = new System.Text.RegularExpressions.Regex(@"(you|your|yours|You|Your)");
    var bad = new List<string>(); var scanned = 0;
    var rich = DeedsTests.Rich(input); rich.IsSelf = false; rich.PlayerName = "Tor"; rich.ViewerName = "Rowan"; rich.Fellows = new List<PanelInput> { input };   // deeds, fishing, taming, crops
    var battleFellow = Program.BattleSample(); battleFellow.IsSelf = false; battleFellow.PlayerName = "Finch"; battleFellow.ViewerName = "Rowan";   // deaths, damage taken, biomes
    foreach (var who in sampleFellows.Concat(new[] { edda, rich, battleFellow }))
        foreach (var (name, v) in SampleTests.AllPages(who))
        {
            IEnumerable<string> Of(Block b) => new[] { b.Title, b.Value, b.Text, b.Note }.Concat((b.Items ?? new List<Block>()).SelectMany(Of));
            foreach (var t in v.Blocks.SelectMany(Of).Concat(new[] { v.Heading, v.Scope }).Where(s => !string.IsNullOrEmpty(s)))
            {
                scanned++;
                if (yous.IsMatch(t) && !t.Contains("(you)")) bad.Add(who.PlayerName + " " + name + ": " + t);
            }
        }
    var finchText = SampleTests.AllPages(battleFellow).SelectMany(p => PanelModel.AllText(p.view)).ToList();
    Check(finchText.Contains(PanelModel.WhatHurt(battleFellow)) && finchText.Any(t => t.Contains("Finch") && t.Contains("fell")) && finchText.Contains(PanelModel.AfterOf(battleFellow)) &&
          SampleTests.AllPages(rich).SelectMany(p => PanelModel.AllText(p.view)).Any(t => t.Contains("Tor's") && t.Contains("enjoyed")),
          "a fellow's Battle and Cooking pages name the player: What hurt Finch, Where Finch fell, after their armor");
    foreach (var s in bad.Distinct().Take(60)) System.Console.WriteLine("  you-text: " + s);
    Check(scanned > 500 && bad.Count == 0, "a fellow player's pages never say you or your about them (" + bad.Distinct().Count() + " strings in " + scanned + ")");
}
var eddaBattle = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Battle });
Check(Says(eddaBattle, "Edda", "last shared", "8 Oct 00:05") && !Says(eddaBattle, PanelModel.WindowLabel(TimeWindow.Session).ToLowerInvariant()) && Says(eddaBattle, PanelModel.SharedWindowsLine(edda)) && Find(eddaBattle, "deathrows")?.Items.Any(b => b.Title == "death from frost") == true,
      "snapshot: the Battle page says whose copy and when it is from (never your \"this session\"), and why its window chips are greyed; deaths without positions still count by type");
// B33 on every page with windows: on a fellow's book Session is YOUR session's span. Their damage from before you began is left out; a fellow
// who did not play during your session says so instead of showing their last session
{
    var b33Rows = PanelModel.Damage(edda.Log, TimeWindow.Session, "", edda.NowUtc);
    var b33Mid = b33Rows.Select(r => r.Bucket).OrderBy(t => t).Skip(b33Rows.Count / 2).FirstOrDefault();
    var b33Keep = (edda.ViewerSessionStartUtc, edda.OnThisSession);
    try
    {
        edda.ViewerSessionStartUtc = b33Mid; edda.OnThisSession = true;
        var b33Cut = PanelModel.Damage(edda.Log, TimeWindow.Session, "", edda.NowUtc, PanelModel.SessionFrom(edda));
        // 0.8.2 (Joost: "Other players' blocks and parries show in All. Why?"): their blocks and parries per minute (EventLog.Guard) are cut at
        // your session's start as their damage is; a copy from before 0.8.2 (one untimed tally of their whole session) leaves Session and says so
        var guardKeep = (new Dictionary<string, float>(edda.Log.Guard), edda.Log.GuardKept);
        try
        {
            edda.Log.Guard.Clear(); edda.Log.GuardKept = true;
            string Min(int m) => EventLog.Bucket(b33Mid.AddMinutes(m));
            edda.Log.Guard[Min(-5) + "|block"] = 7; edda.Log.Guard[Min(-5) + "|parry"] = 2;   // before you came in
            edda.Log.Guard[Min(2) + "|block"] = 3; edda.Log.Guard[Min(2) + "|parry"] = 1;     // after
            var b33Def = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Session, Page = { [Chapter.Battle] = "defense" } });
            var b33Guard = Find(b33Def, "guard");
            edda.Log.GuardKept = false;
            var b33Old = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Session, Page = { [Chapter.Battle] = "defense" } });
            Check(b33Guard != null && b33Guard.Value == "2" && b33Guard.Value2 == "1" && !PanelModel.AllText(b33Def).Contains(PanelModel.FellowBlocksAll(edda)) &&
                  !PanelModel.Content(b33Old).Any(b => b.Kind == "guard") && PanelModel.AllText(b33Old).Count(t => t.StartsWith("Edda's Hearthwoven is " + edda.ModVersion + ": blocks and parries")) == 1,
                  "0.8.2 blocks: a fellow's Session counts their blocks and parries from your session's start (2 blocks + 1 parry, not the 7 + 2 before you came in); a copy from before 0.8.2 leaves them out and says it once: " +
                  PanelModel.AllText(b33Old).FirstOrDefault(t => t.StartsWith("Edda's Hearthwoven is")));
        }
        finally { edda.Log.Guard.Clear(); foreach (var kv in guardKeep.Item1) edda.Log.Guard[kv.Key] = kv.Value; edda.Log.GuardKept = guardKeep.Item2; }
        edda.OnThisSession = false;
        var b33Off = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Session, Page = { [Chapter.Battle] = "damage" } });
        Check(b33Rows.Count > 1 && b33Cut.Count > 0 && b33Cut.Count < b33Rows.Count && b33Cut.All(r => r.Bucket.AddMinutes(edda.Log.Span) > b33Mid) &&
              PanelModel.Content(b33Off).Any(b => b.Kind == "empty" && b.Text == PanelModel.FellowNotOn(edda)),
              "B33 Battle: a fellow's Session counts from your session's start (" + b33Cut.Count + " of " + b33Rows.Count + " rows); one who did not play then says \"" + PanelModel.FellowNotOn(edda) + "\"");
    }
    finally { edda.ViewerSessionStartUtc = b33Keep.Item1; edda.OnThisSession = b33Keep.Item2; }
}
var oldCopy = PanelInput.FromSnapshot(eddaJson); oldCopy.NowUtc = now.AddDays(3); oldCopy.ToLocal = t => t.AddHours(2);
var stale = PanelModel.Build(oldCopy, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.LastHour });
Check(stale.Windows.Where(w => !w.Disabled).Select(w => w.Id).SequenceEqual(new[] { "Session" }) && stale.Windows.Count == 11 && !Says(stale, PanelModel.WindowLabel(TimeWindow.LastHour).ToLowerInvariant()) && Says(stale, "8 Oct 00:05") && !Says(stale, "No damage received"),
      "snapshot: a fellow's book offers only the window it can show (their last shared session; the others greyed, not dropped) and says when the copy is from, not that nothing happened");
var staleOv = PanelModel.Build(oldCopy, new PanelState { Chapter = Chapter.Battle });
Check(Says(staleOv, PanelModel.SharedWindowsLine(oldCopy)) && Says(staleOv, oldCopy.ModVersion, "10 min to 3 h", "update") && Says(staleOv, "totals", "not shared") &&
      !PanelModel.StepView(new PanelState { Chapter = Chapter.Battle }, staleOv, 1) && !staleOv.Keys.Any(k => k.EndsWith("Window")),
      "snapshot: the greyed window chips say why: their older version, what fills in once they update, what is not shared (" + staleOv.WindowWhy + "), and the view key does not cycle into a greyed window");
edda.Fellows = new List<PanelInput> { input }; input.Fellows = new List<PanelInput> { edda };
var cooking = Show(input, Chapter.Deeds, "cooking");
Check(cooking.Heading == "Cooking" && Find(cooking, "ranking").Items.Single().Title == "Edda" && Find(cooking, "ranking").Items.Single().Value == "2" && Find(cooking, "ranking").Note == null && Find(cooking, "ranking").Src == "fellows",
      "cooking: meals enjoyed by companions, who enjoyed them (by name), labelled measured on their PCs");
Check(Reads(PanelModel.Titles(input).Single(t => t.Title == "Hearth Cook").Lines[0].Key, "2 times", "enjoyed food you made"), "wording: Hearth Cook counts the times someone enjoyed food you made (2)");
Check(PanelInput.FromSnapshot("not json") == null && PanelInput.FromSnapshot("") == null, "snapshot: unreadable JSON gives no input, no exception");

// K3 (SOURCES.md fix 3): a fellow's copy carries their since-install totals too; an older copy (without them) reads as before
string EddaSinceInstallSnapshot()
{
    var st = new PlayerProfile.PlayerStats[1]; st[0] = new PlayerProfile.PlayerStats(); st[0][PlayerStatType.CraftFood] = 210f;
    var ev = new SessionEvents(); SessionEvents.Add(ev.AteFoodMadeBy, "Rowan|QueensJam", 2);   // this session: just reconnected
    var since = new SessionEvents { Blocks = 40 }; SessionEvents.Add(since.AteFoodMadeBy, "Rowan|QueensJam", 9); SessionEvents.Add(since.EquippedGearMadeBy, "Rowan|AxeBronze", 4);
    var log = new EventLog(); log.AddDamage(now.AddMinutes(-30), "Mountain", true, "Wolf", "Spears", D("pierce", 40));
    var tally = new DamageTally(); tally.AddDealt("Wolf", "Spears", D("pierce", 40));
    var tallySince = DamageTally.Sum(tally); tallySince.AddDealt("Troll", "Spears", D("pierce", 900)); tallySince.AddTaken("Troll", "EnemyHit", D("blunt", 120));
    return GroupShare.SharedCopy(Snapshot.Build("0.2.1", 77L, "Edda", st, new Snapshot.SkillInfo[0], "Midgard", tally, "s2", ev, log, share: true, eventsSinceInstall: since, damageSinceInstall: tallySince));
}
var eddaNow = PanelInput.FromSnapshot(EddaSinceInstallSnapshot());
eddaNow.NowUtc = now; eddaNow.ToLocal = t => t.AddHours(2); eddaNow.ViewerName = "Rowan"; eddaNow.DisplayName = input.DisplayName;
Check(eddaNow.SharedSinceInstall && eddaNow.Events.AteFoodMadeBy["Rowan|QueensJam"] == 9f && eddaNow.Events.Blocks == 40 && eddaNow.Session.Dealt.Values.Sum() == 40f &&
      eddaNow.DamageSinceInstall.Dealt.Values.Sum() == 940f && !edda.SharedSinceInstall && edda.DamageSinceInstall == null && edda.Events.AteFoodMadeBy["Rowan|QueensJam"] == 2f,
      "K3 snapshot: a fellow's since-install totals come back (events and damage); an older copy keeps last shared (missing field = old behaviour)");
var keepFellows = input.Fellows; input.Fellows = new List<PanelInput> { eddaNow };
var foodNow = Find(Show(input, Chapter.Company, "food"), "axis");
var giftsNow = Find(Show(input, Chapter.Company), "giving").Items.Where(g => g.Kind == "gift").ToDictionary(g => g.Id + "/" + g.Tone);
input.Fellows = keepFellows;
Check(foodNow.Items.Single(e => e.Kind == "end" && e.Tone == "right").Value == "9" && giftsNow["Rowan>Edda/food"].Value == "9" && giftsNow["Rowan>Edda/gear"].Value == "4",   // gear: times put to good use, as Gear shared's tile (fireside-route)
      "K3 company: Food shared and Fireside read the fellow's since-install record, so a reconnect does not drop them back to zero");
Check(Says(Show(eddaNow, Chapter.Deeds), "Edda", "as of 8 Oct 00:00") && !Says(Show(eddaNow, Chapter.Deeds), "since install") && Says(Show(edda, Chapter.Deeds), "Edda", "last shared", "8 Oct 00:05"),
      "K3 scope (0.7 overview, RecordedScope): a since-install copy says as of its latest record, never since install; an older copy still says last shared");
var eddaFoes = Show(eddaNow, Chapter.Battle, "foes", s => s.Window = TimeWindow.SinceInstall);
Check(Find(eddaFoes, "foetable").Items.Select(r => r.Value).SequenceEqual(new[] { "900", "40" }) && Says(eddaFoes, "Edda", "as of 8 Oct 00:00") && !Says(eddaFoes, "since install") &&
      Says(eddaFoes, PanelModel.SharedWindowsLine(eddaNow)) && Texts(eddaFoes).Any(t => t.ToLowerInvariant().Contains("days are not shared")) && Says(Show(eddaNow, Chapter.Battle), "Edda", "last shared", "8 Oct 00:00"),
      "K3 battle (0.7): a fellow's Foes read their damage since install, dated as of their latest record (never \"since install\"); the windowed overview keeps last shared");
var bare = PanelInput.FromSnapshot("{\"name\":\"Finch\"}");
Check(bare != null && Find(PanelModel.Build(bare, new PanelState()), "empty")?.Text?.Contains("Finch") == true, "snapshot: a snapshot with nothing in it shows empty states, in the player's name");

// ---------- the player switcher ----------
bool SaysShareLine(PanelView v) => new[] { PanelModel.SoloNote, PanelModel.ShareWaitingNote, PanelModel.ShareOffNote, PanelModel.JoiningNote }.Any(n => Says(v, n));
var sw = new PanelView();
PanelModel.AddPlayers(sw, "Rowan", new[] { "Tor", "edda", "Rowan", "Edda", "" }, "", true);
Check(sw.Players.Select(p => p.Label).SequenceEqual(new[] { "Rowan", "edda", "Tor" }) && sw.Players[0].Selected && !SaysShareLine(sw), "switcher: you first and selected, then fellow players by name, no sharing line");
PanelModel.AddPlayers(sw, "Rowan", new[] { "Tor" }, "Gone", true);
Check(sw.Players[0].Selected, "switcher: a player no longer shared falls back to you");
PanelModel.AddPlayers(sw, "Rowan", new string[0], "", true);
Check(sw.Players.Count == 1 && Says(sw, PanelModel.ShareWaitingNote), "switcher: sharing on but nobody else yet says so");
PanelModel.AddPlayers(sw, "Rowan", new[] { "Tor" }, "Tor", false);
var withKey = new PanelView(); withKey.Keys.Add("[T] About");
Check(sw.Players.Count == 0 && Says(sw, PanelModel.ShareOffNote) && Says(sw, PanelModel.ShareOffHow(sw)) && PanelModel.ShareOffHow(sw).Contains("About > Sharing") &&
      PanelModel.ShareOffHow(withKey).StartsWith("[T]") && PanelModel.ShareOffHow(withKey).Contains("About > Sharing"),
      "switcher: sharing off hides the switcher, says what off means and where to read how to turn it on (About > Sharing, with its key): " + string.Join(" | ", Texts(sw)));

// ---------- the visual vocabulary: composition, biomes, ladders, source marks (2026-10-08) ----------
var vWood = Show(input, Chapter.Deeds, "woodcutting");
var cWood = Find(vWood, "composition");
Check(cWood != null && cWood.Title == "Wood brought in" && cWood.Value == "3\u00A0305" && cWood.Src == "character" && cWood.Items.All(i => i.Src == "character") &&
      cWood.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Wood=2\u00A0400", "Corewood=520", "Finewood=310", "Ancient Bark=75" }) &&
      cWood.Items[0].Pattern == "vocab:grain-wood-n" && cWood.Items[1].Pattern == "vocab:grain-corewood-n" && cWood.Items[1].Colour == PanelModel.MaterialColour["$item_roundlog"] && cWood.Items.All(i => i.Colour != null && i.Colour.StartsWith("#")),
      "V composition woodcutting: wood brought in per kind (2 400 + 520 + 310 + 75 = 3 305), a neutral grain per wood kind in the icon's colour (Codex's samples without the game)");
var cOre = Find(Show(input, Chapter.Deeds, "mining"), "composition");
Check(cOre.Items.Select(i => i.Title).SequenceEqual(new[] { "Stone", "Copper Ore", "Tin Ore", "Silver Ore" }) && Find(Show(input, Chapter.Deeds, "groundwork"), "composition").Items.Select(i => i.Title).SequenceEqual(new[] { "Level Ground", "Raise Ground", "Paved Road" }),
      "names: the compositions show the game's display names (through the shared name token), never prefab names");
var prefabish = new[] { "Roundlog", "Elderbark", "Copperore", "Tinore", "Silverore", "Levelground", "Pavedroad", "Woodwall", "Carrotsoup" };
var prefabHit = new[] { "woodcutting", "mining", "building", "crafting", "cooking" }.SelectMany(pg => PanelModel.AllText(Show(input, Chapter.Deeds, pg))).FirstOrDefault(t => prefabish.Any(w => t.Contains(w)));
Check(prefabHit == null, "names: no prefab-style name on the Deeds pages when the game has a display name" + (prefabHit != null ? ": " + prefabHit : ""));
Check(PanelModel.Content(PanelModel.Build(new PanelInput { ItemsPickedUp = new Dictionary<string, float> { ["$item_modwood"] = 4 }, GatherKind = _ => "wood", DisplayName = t => t },
      new PanelState { Page = { [Chapter.Deeds] = "woodcutting" } })).Single(b => b.Kind == "composition").Items.Single().Title == "Modwood",
      "names: a token the game cannot name falls back to a readable name, never the raw $token");
var oreHero = Find(Show(input, Chapter.Deeds, "mining"), "hero");
Check(cOre != null && cOre.Title == null && oreHero.Title == "stone and ore brought in" && oreHero.Value == "2\u00A0134" && cOre.Items.Sum(i => double.Parse(i.Value.Replace(PanelModel.ThousandsGap, ""), System.Globalization.CultureInfo.InvariantCulture)) == 2134 && !cOre.Items.Any(i => i.Title.Contains("aspberr")) && cOre.Items.All(i => i.Pattern == null),
      "V composition mining: the hero is the stone and ore brought in (silver only counted exactly), the bar under it has no head of its own, berries left out, sums match");
var cGround = Find(Show(input, Chapter.Deeds, "groundwork"), "composition");
Check(cGround != null && cGround.Title == null && Find(Show(input, Chapter.Deeds, "groundwork"), "hero").Value == "1\u00A0023" && Math.Abs(cGround.Items.Sum(i => i.Fraction) - 1f) < 1e-4 && cGround.Items.Select(i => i.Value).SequenceEqual(new[] { "850", "133", "40" }),
      "V composition building: groundwork per kind, apart from building (materials per piece are not collected, so not shown)");
var vBattle = Show(input, Chapter.Battle, null, s => PanelModel.ToggleFacet(s, PanelModel.BattleOverviewFilter, "biome", "Swamp"));
var strip = PanelModel.BiomeStrip(input, PanelModel.Damage(input.Log, TimeWindow.Session, "", now), PanelModel.Deaths(input.Log, TimeWindow.Session, "", now), "Swamp");   // the block itself
Check(strip != null && strip.Items.Select(i => i.Id).SequenceEqual(new[] { "Meadows", "BlackForest", "Swamp" }) && strip.Items.Select(i => i.Title).SequenceEqual(new[] { "Meadows", "Black Forest", "Swamp" }),
      "V biomes: only biomes with evidence, in journey order (no Mountains, Plains, Ocean: never fought there, no boss)");
Check(strip.Items.Select(i => i.Value + "/" + i.Value2 + "/" + i.Count).SequenceEqual(new[] { "108//0", "420/310/1", "434/266/2" }) && strip.Items.Single(i => i.Selected).Id == "Swamp" &&
      Math.Abs(strip.Items.Max(i => i.Fraction) - 1f) < 1e-4 && strip.Items.All(i => i.Src == "pc" && i.Colour != null && i.Icon.StartsWith("vocab:biome-")),
      "V biomes: dealt rises, received hangs, deaths per biome, the chosen biome lit, every tile carries Src pc (data: Battle draws no label)");
var allRows = PanelModel.Damage(input.Log, TimeWindow.Session, "", now);
Check(strip.Items.Sum(i => double.Parse((i.Value == "" ? "0" : i.Value).Replace(PanelModel.ThousandsGap, ""), System.Globalization.CultureInfo.InvariantCulture)) == Math.Round(allRows.Where(r => r.Dir == "dealt").Sum(r => r.Amount)) &&
      strip.Items.Sum(i => i.Count) == PanelModel.Deaths(input.Log, TimeWindow.Session, "", now).Count, "V biomes: tile sums match the session's dealt damage and deaths");
var meadowsBoss = strip.Items[0].Items?.Single(); var elder = strip.Items[1].Items?.Single();
Check(meadowsBoss != null && meadowsBoss.Kind == "boss" && meadowsBoss.Title == "Eikthyr" && meadowsBoss.Icon == "item:TrophyEikthyr" && meadowsBoss.Src == "character" &&
      elder?.Title == "The Elder" && strip.Items[2].Items == null, "V biomes: a defeated boss sits on its biome with Src character; zero kills (Bonemass) is no medal");
input.EnemyKills["$enemy_dragon"] = 1;
Check(Find(Show(input, Chapter.Battle), "biomes").Items.Select(i => i.Id).SequenceEqual(new[] { "Meadows", "BlackForest", "Swamp", "Mountain" }), "V biomes: a defeated boss alone is evidence of the biome (Moder: Mountains shown)");
input.EnemyKills.Remove("$enemy_dragon");
// the game's own record of found biomes (Player.m_knownBiome): a biome found without a fight still has its tile (Joost's
// first test: Meadows missing), in journey order with the Ocean last; a defeated boss sits in its tile
var explorer = new PanelInput { Log = new EventLog(), KnownBiomes = new[] { "Ocean", "BlackForest", "Meadows" }, EnemyKills = new Dictionary<string, float> { ["$enemy_eikthyr"] = 1 } };
var foundStrip = PanelModel.BiomeStrip(explorer, new List<PanelModel.DamageRow>(), new List<EventLog.Death>(), "");   // the block itself (a page without numbers leaves it out: SourcesTests.cs)
Check(foundStrip != null && foundStrip.Items.Select(i => i.Id).SequenceEqual(new[] { "Meadows", "BlackForest", "Ocean" }) && foundStrip.Items[0].Items?.Single().Title == "Eikthyr" &&
      foundStrip.Items.All(i => i.Value == "" && i.Value2 == "" && i.Count == 0),
      "V biomes: every biome the character found shows (the game's record), journey order, Ocean last, Eikthyr on Meadows, no numbers without data");
var explorerState = new PanelState { Chapter = Chapter.Battle, ViewKey = "F" }; explorerState.Page[Chapter.Battle] = "deaths";   // Overview, Damage and Deaths filter by biome in a bar; no heading chip on any
explorer.KnownBiomes = new[] { "Ocean", "BlackForest", "Meadows" };
var bv = PanelModel.Build(explorer, explorerState);
Check(bv.Biomes.Count == 0 && PanelModel.FilterOf(bv) == null && !bv.Keys.Any(k => k.Contains("Filter")),
      "biome choice: no falls yet, nothing to narrow: no Biome row, no key, and never a heading chip (the Biome row lives in BattleFilterTests)");
Check(PanelModel.Build(new PanelInput { Log = new EventLog() }, new PanelState { Chapter = Chapter.Battle, ViewKey = "F", Page = { [Chapter.Battle] = "deaths" } }) is var none && none.Biomes.Count == 0 && !none.Keys.Any(k => k.Contains("Filter")),
      "biome choice: nothing found, no choice and no key (nothing to filter)");
var quiet = PanelModel.Build(explorer, new PanelState { Chapter = Chapter.Battle });
var quietEmpty = Find(quiet, "empty");
Check(quietEmpty != null &&
      quietEmpty.Title.StartsWith(PanelModel.NothingYet) && quietEmpty.Title.Contains(PanelModel.WindowLabel(TimeWindow.Session).ToLowerInvariant()) && !string.IsNullOrEmpty(quietEmpty.Text) &&   // the session window says so, not "from install"
      !PanelModel.AllText(quiet).Contains(PanelModel.NoDeaths),
      "empty: Battle without data says Nothing yet and the next step, once (no No damage / No deaths lines under it)");
// rc2 (Joost on rc1): Today's empty overview said only its line, Session's also shows the biome tiles. A day window with nothing in it says its
// own line and keeps the same tiles (the biomes you found); no boss crowns there, as on a day with fights (a day has no kills per foe)
explorer.History = new DayHistory();
var quietDay = PanelModel.Build(explorer, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Today });
var dayEmpty = Find(quietDay, "empty"); var dayTiles = Find(quietDay, "biomes"); var sessionTiles = Find(quiet, "biomes");
Check(dayEmpty?.Title == "Nothing today" && dayEmpty.Text == PanelModel.DayEmptyLine && dayTiles != null && sessionTiles != null &&
      dayTiles.Items.Select(i => i.Id).SequenceEqual(sessionTiles.Items.Select(i => i.Id)) && dayTiles.Items.All(i => i.Value == "" && i.Value2 == "" && i.Items == null),
      "empty: a day window with nothing in it says its own line and keeps Session's biome tiles (no numbers, no boss crowns): " + string.Join(", ", dayTiles?.Items.Select(i => i.Id) ?? new string[0]));
explorer.History = null;
// B19 (Joost 2026-10-09): an empty short window on Defence stacked three explanations (the blocks line, "Nothing in the last 30 minutes",
// "Choose a longer window, or All for everything since install"): one calm empty state, its heading and one line, nothing above or under it
var quietDef = PanelModel.Build(explorer, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.LastThirtyMinutes, Page = { [Chapter.Battle] = "defense" } });
var quietDefText = PanelModel.Content(quietDef).Where(b => b.Kind == "empty" || b.Kind == "note").ToList();
Check(quietDefText.Count == 1 && quietDefText[0].Kind == "empty" && quietDefText[0].Title.StartsWith("Nothing") && quietDefText[0].Title.Contains(PanelModel.WindowLabel(TimeWindow.LastThirtyMinutes).ToLowerInvariant()) && quietDefText[0].Text == PanelModel.DayEmptyLine &&
      !PanelModel.AllText(quietDef).Any(t => t.Contains("since install")),
      "B19: an empty 30 minutes on Defence is one empty state with one line: " + string.Join(" | ", quietDefText.Select(b => b.Title + " / " + b.Text)));
explorer.KnownBiomes = null;
Check(PanelModel.BiomeStrip(explorer, new List<PanelModel.DamageRow>(), new List<EventLog.Death>(), "").Items.Select(i => i.Id).SequenceEqual(new[] { "Meadows" }),
      "V biomes: without the game's record (a fellow's copy) only evidence shows");
var hourStrip = Find(Show(input, Chapter.Battle, null, s => s.Window = TimeWindow.LastHour), "biomes");
Check(hourStrip.Items.Select(i => i.Id + "=" + i.Value + "/" + i.Count).SequenceEqual(new[] { "Meadows=/0", "BlackForest=/0", "Swamp=434/2" }), "V biomes: the time window filters the numbers; bosses (since you were made) keep their tiles");
var hurt = Find(vBattle, "composition");
Check(hurt != null && hurt.Title.StartsWith(PanelModel.WhatHurtYou) && hurt.Title.Contains("Swamp") && hurt.Value == "266" && hurt.Note == PanelModel.AfterResistance && hurt.Src == "pc" &&
      hurt.Items.Select(i => i.Title + "=" + i.Value + "=" + i.Colour).SequenceEqual(new[] { "Slash=144=#aab6c2", "Poison=122=#3ccf6e" }) && hurt.Items[1].Icon == "damage:poison",
      "V What hurt you: received by damage type in the chosen biome, palette colours, after your armour, matches the heading");
Check(PanelModel.DealtQualifier == "before the foe's armor" && PanelModel.ReceivedQualifier == "after your armor" && PanelModel.DealtLabel == "damage dealt" && PanelModel.ReceivedLabel == "damage received" &&
      PanelModel.DiedHere == "died here" && PanelModel.BossDefeated == "boss defeated", "V words: the strip's legend in the word list's terms");
var vSkills = Show(input, Chapter.Skills);
var lad = Find(vSkills, "ladders");
Check(lad != null && lad.Items.Select(g => g.Title).SequenceEqual(new[] { "Fight", "Gather", "Move", "Make" }) && lad.Items[1].Items.Select(i => i.Id).SequenceEqual(new[] { "Pickaxes", "WoodCutting", "Fishing" }) && lad.Items[0].Items.Select(i => i.Id).SequenceEqual(new[] { "Swords", "Blocking", "Axes" }),
      "V ladders: grouped Fight, Gather, Move, Make in the prototype's order; empty groups left out");
var axes = lad.Items[0].Items[2];
Check(axes.Level == 38 && axes.Value == "38" && Math.Abs(axes.Progress - 0.62f) < 1e-4 && axes.Practised && axes.Src == "character" && axes.Icon == "skill:Axes" &&
      !lad.Items[3].Items[0].Practised, "V ladders: level, progress to the next level, practised since install as the glow (Cooking: not practised)");
var oneAxes = Find(Show(input, Chapter.Skills, "Axes"), "ladder");
var onePractice = Find(Show(input, Chapter.Skills, "Axes"), "ladder").Items.Single(i => i.Kind == "practice");   // 0.7: the ladder's practice share (no zone, no hero)
Check(oneAxes != null && oneAxes.Value2 == "39" && PanelModel.Ladder(input, "Axes").Items.Single(i => i.Kind == "practice").Value == "42 %" && onePractice?.Value == "42 %" && onePractice.Note == "the most of any skill" && onePractice.Src == "pc" &&
      !(oneAxes.Items ?? new List<Block>()).Any(i => i.Kind == "link"), "V ladder: one skill large, next level, the practice share counted from install with Src pc");
var wcInput = Sample(); wcInput.SkillLevels["WoodCutting"] = 34; wcInput.SkillProgress = null;
var wc = Find(PanelModel.Build(wcInput, new PanelState { Chapter = Chapter.Skills, Page = { [Chapter.Skills] = "WoodCutting" } }), "ladder");
Check(wc != null && wc.Progress == -1 && wc.Items.Single(i => i.Kind == "link").Id == "Deeds/woodcutting" && Find(PanelModel.Build(wcInput, new PanelState { Chapter = Chapter.Skills }), "ladders").Items.Single(g => g.Title == "Gather").Items.Any(i => i.Id == "WoodCutting"),
      "V ladder: progress unknown stays -1 (not drawn, never guessed); the skill links to its deed page");
var gameSkill = new Skills.Skill(new Skills.SkillDef()) { m_level = 10, m_accumulator = 7.3f };
Check(Math.Abs(PanelModel.ProgressOf(10, 7.3f) - gameSkill.GetLevelPercentage()) < 1e-4, "V ladder: a shared snapshot's progress matches the game's own GetLevelPercentage (" + gameSkill.GetLevelPercentage() + ")");
Check(edda.SkillProgress != null && edda.SkillProgress.ContainsKey("Cooking"), "V ladder: progress travels in the shared snapshot");
// the skill beside its deed (polish-06): a skill strip on the page, the same ladder block as On foot's Jump, Run and Swim; its skills and levels
string Strip(PanelView v) { var strip = PanelModel.Content(v).FirstOrDefault(b => b.Kind == "ladders"); return strip == null ? null : string.Join(",", strip.Items.Single().Items.Select(i => i.Id + "=" + i.Value)); }
Check(Strip(Show(input, Chapter.Deeds, "woodcutting")) == null && PanelModel.SkillOf(Find(Show(input, Chapter.Deeds, "woodcutting"), "hero"))?.Value == "34" && Strip(Show(input, Chapter.Deeds, "mining")) == null && PanelModel.SkillOf(Find(Show(input, Chapter.Deeds, "mining"), "hero"))?.Value == "22" &&
      Strip(Show(input, Chapter.Deeds, "cooking")) == null && PanelModel.SkillOf(Find(Show(input, Chapter.Deeds, "cooking"), "hero"))?.Value == "18" && Strip(Show(DeedsTests.Rich(input), Chapter.Deeds, "fishing")) == null && PanelModel.SkillOf(Find(Show(DeedsTests.Rich(input), Chapter.Deeds, "fishing"), "hero"))?.Value == "15",
      "skill beside its deed (0.7 rule K): Woodcutting, Mining, Cooking and Fishing carry their own skill in the hero's row, no strip at the bottom of the plate");
Check(Strip(Show(Program.BattleSample(), Chapter.Battle, "damage")) == "Swords=21,Knives=9,Clubs=16,Spears=27,Axes=38,Bows=31",
      "skill beside its deed: Battle > Damage carries the weapon skills the hits were booked on (tool damage left out), in the Fight order");
Check(Strip(Show(new PanelInput(), Chapter.Deeds, "woodcutting")) == null && Strip(Show(Sample(), Chapter.Deeds, "fishing")) == null,
      "skill beside its deed: a character without the skill, or an empty page, shows no strip");
// source marks replace the source sentences (Joost 2026-10-08)
Check(new[] { Show(input, Chapter.Deeds, "woodcutting"), Show(input, Chapter.Voyages), Show(input, Chapter.Skills), Show(input, Chapter.Stores) }.All(v => !Says(v, PanelModel.SourceCharacter) && !Says(v, PanelModel.SourceSession) && !Says(v, PanelModel.SourceFellows)) &&
      Says(Show(input, Chapter.Battle, "foes"), PanelModel.WindowLabel(TimeWindow.Session).ToLowerInvariant()), "Src: no source sentence on your own pages (the since-install label says it); Foes says its window (HISTORY-06)");
Check(Find(vBattle, "biomes").Src == "pc" && Find(Show(input, Chapter.Battle, "foes", s => s.Window = TimeWindow.SinceInstall), "hero").Src == "character" && Show(input, Chapter.Deeds, "cooking").HeadingSrc == null, "Src: the heading's number carries its Src; a heading without a number carries none; (data)");
// no icons beside numbers (Joost 2026-10-08): Src stays data; only a number counted on this PC gets "since install", once
IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
int Labels(PanelView v) => Zoned.Labels(v);   // the "Recorded from" labels (SourcesTests.cs)
var carts = Show(input, Chapter.Stores);
Check(Labels(carts) == 1, "since install: a page whose numbers were all counted on this PC says it once, after the heading");
var yourSide = Show(input, Chapter.Company, "food");
Check(Labels(yourSide) >= 1 && Every(yourSide.Blocks).Where(b => !string.IsNullOrEmpty(b.RecordedFrom)).All(b => b.Src == "pc"), "company 0.7: Food shared dates what you enjoyed (this PC), never what fellow players recorded");
var skillsLabels = Show(input, Chapter.Skills, null, s => s.View["Skills/overview/view"] = "practised");
// 0.7 (G8): the practised section says "Recorded from <install> · this PC" after its heading (inside the switch's view); its hero's share right above it says nothing (once);
// the levels view and the ladders (your character) carry no label
var practisedSection = PanelModel.Content(skillsLabels).Single(b => b.Kind == "section" && b.Title == PanelModel.PracticeWhere);
var installDay = PanelModel.RecordDate(input, PanelModel.StartOf(input, null).Value);
var skillsLevels = Show(input, Chapter.Skills);
Check(practisedSection.RecordedFrom == "Recorded from " + installDay + " · this PC" && Labels(skillsLabels) == 1 && Labels(skillsLevels) == 0 && Find(skillsLevels, "ladders").RecordedFrom == null,
      "0.7 skills: the practised section says \"Recorded from <install> · this PC\" once after its heading, also inside a switch's view (its hero's share right above does not say it again); the levels and the ladders (your character) carry no label");
var woodLabels = Show(input, Chapter.Deeds, "woodcutting");
var woodHero = Find(woodLabels, "hero");
var woodAxe = woodHero.Items?.SingleOrDefault(n => n.Kind == "number");   // 0.7: the hero's second number again (no zones)
Check(woodHero.Value == "410" && woodHero.Title == "trees felled" && woodAxe?.Value == "64" && woodAxe.Title == "axe hits" &&
      Zoned.Says(woodLabels, woodAxe) && !Zoned.Says(woodLabels, woodHero) && !Zoned.Says(woodLabels, Find(woodLabels, "composition")),
      "since install: a single number from this PC gets it beside the number (the hero's second number); the character's counts carry nothing");
var axesPage = Show(input, Chapter.Skills, "Axes");
Check(PanelModel.PlateOf(axesPage) != null && PanelModel.PlateOf(axesPage).Icon == "skill:Axes", "plate: a single skill's page sits on the plate like every page (in-game snapshot: unreadable over the world)");
Check(Find(axesPage, "ladder").Items.Single(i => i.Kind == "practice") is Block axesPractice && axesPractice.RecordedFrom == "from " + PanelModel.RecordDate(input, PanelModel.StartOf(input, null).Value) && !Zoned.Says(axesPage, Find(axesPage, "ladder")),
      "0.7 skill page: the practice share under the ladder says \"from <install>\", not the level");

// ---------- page layout: plate, hero, columns, switch, cards (slice 3) ----------
Check(PanelModel.SplitNumber("410 trees felled") == ("410", "trees felled") && PanelModel.SplitNumber("1\u00A0180 pickaxe hits") == ("1\u00A0180", "pickaxe hits") &&
      PanelModel.SplitNumber("6.6 km at the helm") == ("6.6", "km at the helm") && PanelModel.SplitNumber("Map shared 2 times at the table") == (null, "Map shared 2 times at the table"),
      "L hero: a title line splits into its number and its label; a line without a leading number stays whole");
var hero2 = PanelModel.Hero(("220", "planted", "character", null), ("", "nothing", "pc", null), ("194", "harvested", "pc", null));
Check(hero2.Kind == "hero" && hero2.Value == "220" && hero2.Title == "planted" && hero2.Src == "character" && hero2.Source == "character" &&
      hero2.Items.Single().Kind == "number" && hero2.Items.Single().Value == "194" && hero2.Items.Single().Src == "pc" && hero2.Items.Single().Source == "measured" &&
      PanelModel.Hero(("", "x", "pc", null)) == null, "L hero: the big number leads, further numbers ride on the right with their own Src; no value, no number; none, no hero");
var cards = PanelModel.DeedCards(PanelModel.Titles(input));
var woodCard = cards.Items.Single(c => c.Title == "Woodcutter");
Check(cards.Kind == "cards" && cards.Items.All(c => c.Kind == "card" && c.Id.StartsWith("Deeds/") && c.Icon.StartsWith("title:")) &&
      woodCard.Value == "410" && woodCard.Text == "trees felled" && woodCard.Src == "character" && woodCard.Items == null && woodCard.Id == "Deeds/woodcutting" && !cards.Items.Any(c => c.Title == "Trailfinder" || c.Title == "Shieldbearer"),
      "L cards: one card per earned deed title, your character's count big; the class C axe hits drop off the card (0.7, hard case 13); a click opens the owner page; other chapters' titles stay out");
var follow = new PanelState();
PanelModel.Follow(follow, "view:Skills/overview/view=practised"); PanelModel.Follow(follow, "Battle/defense");
Check(follow.View["Skills/overview/view"] == "practised" && follow.Chapter == Chapter.Battle && follow.PageOf(Chapter.Battle) == "defense", "L switch: a chip's target picks the view; other targets still jump to their page");
Check(PanelModel.IsBox(new Block { Kind = "plate" }) && PanelModel.IsBox(new Block { Kind = "columns" }) && PanelModel.IsBox(new Block { Kind = "view" }) && !PanelModel.IsBox(new Block { Kind = "hero" }) && !PanelModel.IsBox(new Block { Kind = "cards" }),
      "L boxes: plate, columns, column, switch and view hold blocks of the page; hero and cards are blocks themselves");
var plated = new[] { Show(input, Chapter.Deeds, "woodcutting"), Show(input, Chapter.Deeds, "mining"), Show(input, Chapter.Skills), Show(input, Chapter.Battle) };
Check(plated.All(p => PanelModel.PlateOf(p) != null && p.Blocks.Count == 1 && PanelModel.PlateOf(p).Title == p.Heading) &&
      plated.Select(p => PanelModel.PlateOf(p).Icon).SequenceEqual(new[] { "title:woodcutter", "title:miner", "ui:chapter-skills", "ui:chapter-battle" }),
      "L plate: Woodcutting, Mining, Skills overview and Battle sit on the plate; its heading row is the page heading with its icon");
var woodPlate = PanelModel.PlateOf(plated[0]);
// Joost in game 0.6.2 (2026-10-09): Woodcutting and Mining showed their skill nowhere. Their heading row holds the window chips, so the page's own
// skill goes on the plate (PanelUi and the preview draw it there when HeadSkillsOnPlate); a page without windows keeps it in the heading row
var woodWeek = Show(input, Chapter.Deeds, "woodcutting", st => st.Window = TimeWindow.SevenDays);
var cookingPage = Show(input, Chapter.Deeds, "cooking");
Check(plated[0].HasFilters && !PanelModel.HeadSkillsOnPlate(plated[0]) && PanelModel.SkillOf(Find(woodWeek, "hero"))?.Title == "Wood Cutting" && PanelModel.SkillOf(Find(plated[0], "hero")) != null && !PanelModel.HeadSkillsOnPlate(plated[1]) && PanelModel.SkillOf(Find(plated[1], "hero")) != null &&   // 0.7 Woodcutting and Mining: in the hero's row (rule K)
      cookingPage.HasFilters && PanelModel.SkillOf(Find(cookingPage, "hero"))?.Value == "18" && !PanelModel.HeadSkillsOnPlate(cookingPage),
      "skill: a page whose heading row holds the window chips (Woodcutting, Mining, All and 7 days; Cooking too since 0.7 gave every Deeds page its windows) draws its skill on the plate");
Check(woodPlate.Pill == null && PanelModel.BandTitles(Find(plated[0], "featband")).Select(t => t.Title).SequenceEqual(new[] { "Woodcutter" }) && PanelModel.PlateOf(plated[2]).Pill == null && (Find(plated[2], "featband") == null || PanelModel.BandTitles(Find(plated[2], "featband")).Count == 0) &&
      PanelModel.PlateOf(Show(input, Chapter.Battle, "defense")).Pill == null && Find(Show(input, Chapter.Battle, "defense", s => s.Window = TimeWindow.SinceInstall), "featband") is Block defBand && defBand.Note == null && defBand.Text == "Title" &&
      PanelModel.BandTitles(defBand).Select(t => t.Title + ": " + t.Text).SequenceEqual(new[] { "Shieldbearer: 28 blocks · 27 parries" }),   // B28: Wallwarden on Deeds > Building
      "L plate (B18): a page's titles ride in the strip at the top of its plate with their reason, never in a pill; on Battle > Defense (All) one strip with its feat; pages without a title have none: " +
      string.Join(", ", PanelModel.BandTitles(Find(Show(input, Chapter.Battle, "defense", s => s.Window = TimeWindow.SinceInstall), "featband")).Select(t => t.Title + ": " + t.Text)));
Check(PanelModel.Content(plated[0]).Where(b => b.Kind != "plate" && b.Kind != "zone" && b.Kind != "featband" && b.Kind != "aboutnumbers" && b.Kind != "headlink").Select(b => b.Kind).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "composition", "hero", "ranking", "section" }) &&   // 0.7: the axe hits ride in the hero, no skill strip; 0.9: the Feed chip rides in the heading row
      Find(plated[0], "section").Title == "Axe hits per tree" && Find(Show(input, Chapter.Deeds, "mining"), "section").Title == "Pickaxe hits per rock",   // zones: the axe hits lead the ember zone
      "L woodcutting holds its hero, the wood bar and the hits per tree, nothing else (no skill strip; the exact counts live in the bar now, K1); mining its hits per rock");
var eddaWoodPage = Show(edda, Chapter.Deeds, "woodcutting");
Check(Says(eddaWoodPage, "Edda", "last shared", "8 Oct 00:05") && Says(eddaWoodPage, PanelModel.DeedsWindowsLine(edda)),
      "L plate: a fellow player's copy says whose it is and when, and its greyed window chips why");
// as Battle: a fellow's Deeds page keeps every window chip (the short ones too), greyed where their copy cannot tell it (0.7 integration; v07-deedshort
// showed them no short chips). 0.8.5: their Session opens (what reached this PC from them while you played); this copy is not live and shares no days
Check(eddaWoodPage.Windows.Select(c => c.Id).SequenceEqual(PanelModel.AllWindows.Select(w => w.ToString())) && !eddaWoodPage.Windows.First(c => c.Id == TimeWindow.Session.ToString()).Disabled &&
      eddaWoodPage.Windows.Where(c => c.Id != TimeWindow.SinceInstall.ToString() && c.Id != TimeWindow.Session.ToString()).All(c => c.Disabled) && eddaWoodPage.ShownWindow == TimeWindow.SinceInstall,
      "fellow's Deeds: every window chip in the row, the short and day ones greyed, Session open, the page on All: " + string.Join(" ", eddaWoodPage.Windows.Select(c => c.Label + (c.Disabled ? "(off)" : ""))));
var bPlate = PanelModel.PlateOf(plated[3]);
Check(Find(plated[3], "biomes") != null && Find(plated[3], "composition")?.Title == "What hurt you" && Find(plated[3], "guard")?.Id == "Battle/defense" && Find(plated[3], "section", s => s.Title == "Hits") == null,
      "L battle overview: the biome strip, then what hurt you with the blocks and parries that open Defense (the lifetime hit counts left for Foes and Defense)");
var skSw = Find(plated[2], "switch");
Check(skSw.Id == "Skills/overview/view" && skSw.Items.Select(x => x.Id + "=" + x.Title + "=" + x.Selected).SequenceEqual(new[] { "levels=Levels=True", "practised=Practiced=False" }) &&
      skSw.Items[0].Items.Select(i => i.Kind).SequenceEqual(new[] { "hero", "ladders" }) && skSw.Items[1].Items == null && plated[2].Keys.Contains("[F] View") && !plated[0].Keys.Any(k => k.Contains("View")),
      "L switch: Skills overview has two views, Levels first; only the chosen view carries blocks; the footer names the view key on pages with a switch only");
var swState = new PanelState { Chapter = Chapter.Skills };
var step = PanelModel.StepView(swState, PanelModel.Build(input, swState), 1);
var afterStep = PanelModel.Build(input, swState);
Check(step && swState.View["Skills/overview/view"] == "practised" && Find(afterStep, "switch").Items[1].Selected && Find(afterStep, "ranking") != null &&
      !PanelModel.StepView(new PanelState(), Show(input, Chapter.Stores, "trader"), 1), "L switch: the view key moves to the next view and the page keeps it; a page without a switch or windows ignores the key");
var wcState = new PanelState { Chapter = Chapter.Deeds }; wcState.Page[Chapter.Deeds] = "woodcutting";
Check(PanelModel.StepView(wcState, PanelModel.Build(input, wcState), 1) && wcState.WindowPicked && PanelModel.Build(input, wcState).ShownWindow == wcState.Window && wcState.Window != TimeWindow.SinceInstall,
      "L windows (0.7): on a Deeds page with the short windows the view key cycles them, as on Battle, and the page then shows the chosen one (" + wcState.Window + ")");
// a chip's press (Follow) and the view key (StepView) both rebuild the page on the other view, and back again
var flip = new PanelState { Chapter = Chapter.Skills };
var before = PanelModel.ToJson(PanelModel.Build(input, flip));
PanelModel.Follow(flip, PanelModel.ViewLink(Find(PanelModel.Build(input, flip), "switch"), Find(PanelModel.Build(input, flip), "switch").Items[1]));
var flipped = PanelModel.Build(input, flip);
PanelModel.StepView(flip, flipped, 1);
Check(PanelModel.ToJson(flipped) != before && Find(flipped, "ladders") == null && Find(flipped, "ranking") != null &&
      PanelModel.ToJson(PanelModel.Build(input, flip)) == before, "L switch: a chip press rebuilds the page on the chosen view; the view key flips it back");
swState.Page[Chapter.Skills] = "Axes"; PanelModel.Build(input, swState); swState.Page[Chapter.Skills] = "overview";
Check(Find(PanelModel.Build(input, swState), "switch").Items[1].Selected, "L switch: the choice is kept per page while you look at other pages");
// every skill the character has a level in gets a ladder: each of the game's own skills in its group (Dodge under Move,
// Joost's first test showed it under Other), a mod's skill under Other, level 0 left out (the game lists it nowhere either)
var gameSkills = Enum.GetNames(typeof(Skills.SkillType)).Where(n => n != "None" && n != "All").ToList();
var everySkill = new PanelInput { SkillLevels = gameSkills.ToDictionary(n => n, n => 5f) };
everySkill.SkillLevels["-48211"] = 3; everySkill.SkillLevels["Sneak"] = 0;
var allLadders = Find(PanelModel.Build(everySkill, new PanelState { Chapter = Chapter.Skills }), "ladders");
Check(allLadders.Items.Select(g => g.Title).SequenceEqual(new[] { "Fight", "Gather", "Move", "Make", "Other" }) && allLadders.Items.Last().Items.Select(i => i.Id).SequenceEqual(new[] { "-48211" }) &&
      allLadders.Items.SelectMany(g => g.Items).Count() == gameSkills.Count && allLadders.Items[2].Items.Any(i => i.Id == "Dodge") && !allLadders.Items.SelectMany(g => g.Items).Any(i => i.Id == "Sneak"),
      "ladders: every skill with a level shows, each of the game's skills in its group (Dodge under Move), a mod's skill under Other, level 0 left out");
var only = new PanelInput { SkillLevels = new Dictionary<string, float> { ["Run"] = 5 } };
Check(Find(PanelModel.Build(only, new PanelState { Chapter = Chapter.Skills }), "switch") == null && Find(PanelModel.Build(only, new PanelState { Chapter = Chapter.Skills }), "ladders") != null,
      "L switch: nothing practised since install, no second view: the ladders stand alone, no chips");

// ---------- empty states, copy ----------
var empty = new PanelInput();
var emptyViews = Enum.GetValues(typeof(Chapter)).Cast<Chapter>().Select(c => PanelModel.Build(empty, new PanelState { Chapter = c })).ToList();
Check(emptyViews.All(v => v.Blocks.Count > 0) && Find(emptyViews[1], "empty").Text == PanelModel.CompanyEmpty, "empty: every chapter survives no data; Company uses the agreed empty text");
Check(PanelModel.Build(null, null).Chapters.Count == 7, "empty: null input and state do not throw");
var views = new List<PanelView>(emptyViews);
foreach (var who in new[] { input, edda })
    foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
    {
        var first = PanelModel.Build(who, new PanelState { Chapter = ch });
        foreach (var l in first.List)
            foreach (var they in new[] { true, false })
                views.Add(PanelModel.Build(who, new PanelState { Chapter = ch, Page = { [ch] = l.Id }, TheyReceived = they }));
views.Add(PanelModel.Build(input, new PanelState { ShowAbout = true }));
    }
var text = views.SelectMany(PanelModel.AllText).Distinct().ToList();
// ISC-A-2: every number on screen carries a machine-readable source (character counter / measured / fellows) for the source marks
string untagged = null;
foreach (var v in views)
{
    if (v.Heading != null && v.Heading.Any(char.IsDigit) && v.HeadingSource == null && !v.Heading.Contains("last record")) untagged ??= v.Active + "/" + v.Page + " heading: " + v.Heading;
    void Walk(Block b, string parent)
    {
        var src = b.Source;   // every row carries its own tag (inherited from its block by the model, not by the reader)
        if (b.Kind != "bars" && b.Kind != "sincewhen" && b.Kind != "origin" && b.Kind != "view" && ((b.Value ?? "").Any(char.IsDigit) || (b.Kind != "section" && (b.Title ?? "").Any(char.IsDigit))) && src == null) untagged ??= v.Active + "/" + v.Page + ": " + (b.Title ?? b.Value);   // sincewhen, origin: a date, not a count (0.7 About); view: a window chip's name ("10 min")
        foreach (var i in b.Items ?? new List<Block>()) Walk(i, src);
    }
    foreach (var b in v.Blocks) Walk(b, null);
}
Check(untagged == null, "ISC-A-2 source tags: every number has a source tag" + (untagged != null ? ": " + untagged : ""));
string unmarked = null;
foreach (var v in views)
{
    void Mark(Block b) { if (b.Kind != "bars" && b.Kind != "sincewhen" && b.Kind != "origin" && (b.Value ?? "").Any(char.IsDigit) && b.Src == null) unmarked ??= v.Active + "/" + v.Page + ": " + (b.Title ?? b.Value); foreach (var i in b.Items ?? new List<Block>()) Mark(i); }
    foreach (var b in v.Blocks) Mark(b);
}
Check(unmarked == null, "Src: every number carries its Src (data; drawn only as since install)" + (unmarked != null ? ": " + unmarked : ""));
var labelled = new List<string>(); var battleLabels = 0; var pcUnlabelled = new List<string>();
foreach (var v in views)
{
    foreach (var b in Every(v.Blocks).Where(b => !string.IsNullOrEmpty(b.RecordedFrom)))
        if (b.Kind != "section" && b.Src == "character") labelled.Add(v.Active + "/" + v.Page + ": " + (b.Title ?? b.Value));
    if (v.Active == Chapter.Battle && !v.ShowAbout && v.HasFilters) battleLabels += Labels(v);
    else if (Every(v.Blocks).Any(b => b.Src == "pc") && Labels(v) == 0) pcUnlabelled.Add(v.Active + "/" + v.Page);
}
Check(labelled.Count == 0, "Recorded from: never on a number from your character's own record" + (labelled.Count > 0 ? ": " + labelled[0] : ""));
Check(battleLabels == 0, "since install: none on Battle's windowed pages (the page states its time window once)");
Check(Labels(defense) == 2 && defense.HeadingRecordedFrom == null && (Find(defense, "guard")?.RecordedFrom ?? "").StartsWith("from "),
      "since install (0.7, P7): Battle > Defence on All dates what Hearthwoven counted where it stands (blocks \"from <date>\", the received-from list its Recorded from line), no heading label (the page mixes in the game's hits received)");
Check(pcUnlabelled.Count == 0, "since install: every page outside Battle with a number from this PC says so somewhere" + (pcUnlabelled.Count > 0 ? ": " + pcUnlabelled[0] : ""));
var oldWords = new[] { "since this character was made", "measured this session", "measured in last shared", "measured on", "picked up", "cooked or grilled", "enemies", "enemy", "damage taken", "resistance  " };
var oldHit = text.FirstOrDefault(t => oldWords.Any(w => t.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0));
Check(oldHit == null, "words: no source sentences, no picked up, cooked or grilled, enemies or damage taken" + (oldHit != null ? ": " + oldHit : ""));
var tags = views.SelectMany(PanelModel.Content).Select(b => b.Source).Where(t => t != null).Distinct().OrderBy(t => t).ToList();
Check(tags.Except(new[] { "server" }).SequenceEqual(new[] { "character", "fellows", "measured" }), "ISC-6 source tags: only character, fellows, measured (and the server book of 0.6): " + string.Join(",", tags));
var dashes = text.Where(s => s.Contains('\u2014') || s.Contains('\u2013')).ToList();
Check(dashes.Count == 0, "copy: no em-dash or en-dash in any player-visible text" + (dashes.Count > 0 ? ": " + dashes[0] : ""));
var banned = new[] { "A shared saga", "Room for every kind of viking", "A shared deed, not a debt", "The things you bring to the fire", "Each title shows", "Counts meals eaten", "gathering" };
var hit = text.FirstOrDefault(s => banned.Any(b => s.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0));
Check(hit == null, "copy: the lines removed in the copy audit are gone, and no 'gathering'" + (hit != null ? ": " + hit : ""));
// Joost's wording rule: using what another made is grateful or good use, never a bare "ate", "equipped", "used" or "took"
var bareUse = text.FirstOrDefault(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(ate|eats|equipped|used|took|taken from)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
Check(bareUse == null, "wording: no bare ate, equipped, used or took in player-visible text" + (bareUse != null ? ": " + bareUse : ""));
bool valid = true;
foreach (var v in views) try { JsonDocument.Parse(PanelModel.ToJson(v)); } catch (Exception e) { valid = false; System.Console.WriteLine(e.Message); }
Check(valid, "preview JSON is valid for every chapter and page");
input.Fellows = null; edda.Fellows = null;

fails += DeedsTests.Run(input, edda);   // the Deeds chapter pages (test-panel/DeedsTests.cs)
// 0.7 redesign (work/hearthwoven-0.7/REDESIGN-RULES.md): the shared pieces, then one file per page group; the page agents fill their own file only
fails += RecordedTests.Run(input, edda, now);           // StartOf, the date words, PlaceRecordedFrom, the About these numbers box (test-panel/RecordedTests.cs)
fails += RecordedGatherTests.Run(input, edda, now);     // G1 Woodcutting, Mining
fails += RecordedMakeTests.Run(input, edda, now);       // G2 Cooking, Building, Groundwork, Crafting
fails += RecordedFieldTests.Run(input, edda, now);      // G3 Farming, Fishing, Taming
fails += RecordedOverviewTests.Run(input, edda, now);   // G4 Deeds Overview, Feats
fails += RecordedBattleTests.Run(input, edda, now);     // G5 Battle
fails += RecordedCompanyTests.Run(input, edda, now);    // G6 Company
fails += RecordedHallTests.Run(input, edda, now);       // G7 Hall and Voyages
fails += RecordedSkillsTests.Run(input, edda, now);     // G8 Skills and About
fails += RecentPageTests.Run(now);                      // 0.7 Deeds > Recent (test-panel/RecentPageTests.cs)
fails += YesterdayTests.Run(now);                        // 0.9.1: Yesterday and 3 days, a friend's days on Recent, the foes defeated per day (test-panel/YesterdayTests.cs)
fails += ParseCountTests.Run(input, edda);   // RESILIENCE-06 item 1: counts read back culture-proof, Cooking from the numbers (test-panel/ParseCountTests.cs)
fails += DeedsTwinTests.Run(input, edda);   // the Deeds twins: both zones on every page the game counts completely (test-panel/DeedsTwinTests.cs)
fails += FacetTests.Run(input);   // the filter bar and Crafting's filter (test-panel/FacetTests.cs)
fails += BattleFilterTests.Run();   // Battle's filters: the biome tiles, Damage's Biome and Foe, Foes' Weapon, Damage type and Kin (test-panel/BattleFilterTests.cs)
fails += BattleDenseTests.Run(now);   // 0.8.5: Battle > Overview on one screen (variant A), Where you fell as height and the falls with no place (test-panel/BattleDenseTests.cs)
fails += BuildingFacetTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // the same bar on Deeds > Building (test-panel/BuildingFacetTests.cs)
fails += BarFormTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // 0.7: the one bar form's colours, list and numbers (test-panel/BarFormTests.cs)
fails += CookingFacetTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // the same bar on Deeds > Cooking (test-panel/CookingFacetTests.cs)
fails += LegendFilterTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // 0.8.2: a bar's legend items filter as its parts do, every bar of a filtered page filters or says why not (test-panel/LegendFilterTests.cs)
fails += MealsTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // 0.8.1 Deeds > Meals: what each player ate, split by who cooked it (test-panel/MealsTests.cs)
fails += MealCombosTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));
fails += MealPlanTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // 0.9.2 Meals' Plan a meal: three foods of the lands you explore together (test-panel/MealPlanTests.cs)   // 0.9 Meals' Your combinations: the foods you had on together (test-panel/MealCombosTests.cs)
fails += ArmorSetsTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // 0.9 Deeds > Crafting > Armor sets: grouping, lands, names, the page's states, Everyone, the copy's yes/no, the feat, the dump (test-panel/ArmorSetsTests.cs)
fails += ArrowsTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // 0.9 arrows track: the Best arrow tie (Joost's Fallen Valkyrie), the Best arrows island on Foes and Last fight, its states (test-panel/ArrowsTests.cs)
fails += WhatToUseTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // 0.9 What to use: the third view on Foes and Last fight, tabs, sets, the table, the cards, a foe's detail (test-panel/WhatToUseTests.cs)
fails += DamageHistoryTests.Run(now);   // 0.9.5 draft: Battle > Damage's History, the Deeds History form on damage per weapon kind (test-panel/DamageHistoryTests.cs)
fails += CraftingHistoryTests.Run(now);   // 0.9 crafting-history: Crafting's Upgrades per item, the two Crafting faults, every deed's History and its days, the Firsts island (test-panel/CraftingHistoryTests.cs)
fails += DeedFeedTests.Run(now);   // 0.9 deed feeds: the Feed chip, a deed's feed and the mixed one in the Battle feed's blocks, windows, filter, Show older, states, a friend's from the server (test-panel/DeedFeedTests.cs)
fails += SortTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // the sort control over the item lists (test-panel/SortTests.cs)
fails += ManyKindsTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // 0.8.1: a long-played book with hundreds of kinds: every list folds (test-panel/ManyKindsTests.cs)
fails += GatherTests.Run();   // B12: what counts as wood and mining (test-panel/GatherTests.cs)
fails += TreeNamesTests.Run();   // 0.8.2: a tree the book has no word for by the game's name, its logs folded in (test-panel/TreeNamesTests.cs)
fails += RocksTests.Run(now);   // 0.8.2: Mining's pickaxe hits per rock by the game's names, split rocks as one, the rest one quiet row (test-panel/RocksTests.cs)
fails += UxTests.Run(now);   // 0.8: the skill beside its component, the growth lines from the day history (test-panel/UxTests.cs)
fails += UnderOneTests.Run();   // 0.6.5: an amount between 0 and 1 says "under 1", never "0" (test-panel/UnderOneTests.cs)
fails += ReviewFixTests.Run();   // 0.6.5 review fixes, one test each (test-panel/ReviewFixTests.cs)
fails += NavTests.Run(input);   // Backspace: back to the page you came from (test-panel/NavTests.cs)
fails += FeatsTests.Run();   // Feats: the table, noticing, the ledger, the page with its detail area, Known for, the band (test-panel/FeatsTests.cs)
fails += GroupFeatsTests.Run();   // 0.7 group feats: the gate, the veil, one sum without double counting, tiers that stick, "knownBiomes" in the snapshot (test-panel/GroupFeatsTests.cs)
fails += FeatLookTests.Run();   // 0.9 feat looks (test period): the families' colours, the Look switch and the config, the cards per look, a hover never chooses (test-panel/FeatLookTests.cs)
fails += FeatScenesTests.Run();   // 0.9.1 a drawn picture for every feat: sources, both sprites and their entries, both looks and the detail, the mirror (test-panel/FeatScenesTests.cs)
fails += MoreFeatsTests.Run();   // 0.9 more feats: each new feat from a hand-built record at its tier lines, the new hooks' counters, the families, a land's own feat (test-panel/MoreFeatsTests.cs)
fails += TogetherBatch3Tests.Run();   // 0.9.2 Together, batch 3: each new group feat summed over everyone who shares, each once, tiers, alone, an older fellow, the ledger's room (test-panel/TogetherBatch3Tests.cs)
fails += FeatsBatch2Tests.Run();   // 0.9.1 feats, batch 2: each new feat from a hand-built record at its tier lines, the batch's counters, the Armsman family, the ledger's room (test-panel/FeatsBatch2Tests.cs)
fails += FeatSearchTests.Run();   // 0.9.2 feat search: the matching rules, one grid across the pages with page tags, the count line, the field as typing (test-panel/FeatSearchTests.cs)
fails += FeatsReadAllTests.Run();   // 0.9.5 Mark read: the strip's action on Earned and Together, both pages cleared, kept, hidden, its key (test-panel/FeatsReadAllTests.cs)
fails += FeatsNewTests.Run();   // 0.9 New feats: the New mark, its order and count, opening a card, the dot from the per-tier record, the first 0.9 start (test-panel/FeatsNewTests.cs)
fails += EveryoneShownTests.Run(now);   // 0.8.2: no list of people says "+N more" or folds a person away; a fold of things is a click (test-panel/EveryoneShownTests.cs)
var textGroup = Sample(); textGroup.PlayerId = 11; textGroup.Fellows = CompanyFellows(now);   // Company with three sharing
var textSamples = new (string, PanelInput)[] { ("sample", input), ("voyager", voyager), ("battle", Program.BattleSample()), ("deeds", DeedsTests.Rich(input)), ("company", textGroup), ("edda", edda), ("edda-since", eddaNow), ("twin", DeedsTwinTests.Twin(input)) };   // edda-since (0.7): a copy with their since-install totals; twin: the baselines taken (DeedsTwinTests)
fails += PanelTextTests.Run(textSamples);   // every label fits, no "at least" (test-panel/PanelTextTests.cs)
fails += SourcesTests.Run(input, edda, voyager, Program.BattleSample());   // where each page's numbers come from, no zones (test-panel/SourcesTests.cs)
fails += MarksTests.Run(voyager, textSamples);   // Codex's last sprites wired; every vocab picture a page asks for is shipped (test-panel/MarksTests.cs)
fails += ShippedTextTests.Run();   // 0.9.8: no person named in the texts that ship (test-panel/ShippedTextTests.cs)
fails += CommentedCallTests.Run();   // 0.9.8: no drawing call hidden in a trailing comment (test-panel/CommentedCallTests.cs)
fails += CoherenceTests.Run();   // one story for every number: overview = detail pages, windows nest, fish and crops add up (test-panel/CoherenceTests.cs)
fails += PropertyTests.Run(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));   // 0.8.5: the rules that hold on every page, each over every page of every book, proven by seeded breaks (test-panel/PropertyTests.cs)
fails += SampleTests.Run();   // Dev.SampleData: the in-game sample fills every page and writes nothing (test-panel/SampleTests.cs)
fails += CargoTests.Run(voyager, input, now);
fails += ServerBookPanelTests.Run(now);   // 0.6: the server book on Sailing and Taming (test-panel/ServerBookPanelTests.cs)   // 0.6: cargo carried and born in your care (test-panel/CargoTests.cs)
fails += LedTests.Run(now);   // 0.6: Heavy Keel best load and the animals led, with their feats (test-panel/LedTests.cs)
fails += ArmourPanelTests.Run(now);   // 0.7: Defence's Received / Your armour switch (test-panel/ArmourPanelTests.cs)
fails += FellowArmourTests.Run(now);   // 0.9 armor-share: a friend's Defense: the armor view from their shared ledger and minutes, their words, the window cut, an older copy, Compare (test-panel/FellowArmourTests.cs)
fails += AfterArmourPanelTests.Run(now);   // 0.8.5: Damage and Foes with your damage after the foe's armor (test-panel/AfterArmourPanelTests.cs)
fails += PaceTests.Run(now);
fails += KindToggleTests.Run(now);   // 0.9.5: By weapon's kind rows shut and open their weapon lines (test-panel/KindToggleTests.cs)   // 0.8.6: damage per second in three spots, and none where players would be compared (test-panel/PaceTests.cs)
fails += BattleRecPanelTests.Run(now);   // 0.8: the battle record as the Battle pages read it (test-panel/BattleRecPanelTests.cs)
fails += BattleFeedTests.Run(now);   // 0.8: the battle pages: x N only where counted, the feed's view kept, the last fight's fellows (test-panel/BattleFeedTests.cs)
fails += FellowFeedTests.Run(now);   // 0.8.2: a fellow's Feed and Last fight as their copy shares it, in their words; an older copy; their Session from yours (test-panel/FellowFeedTests.cs)
fails += FellowWindowsTests.Run(now);   // 0.8.2 (H): a live fellow's 10 min to 3 h from their minute log (their pages, Everyone); a copy not live keeps them greyed with why (test-panel/FellowWindowsTests.cs)
fails += DayTypesPanelTests.Run(now);   // 0.9: Everyone's Damage on Today to 30 days from each fellow's day split by weapon kind and type, the whole-day line, an older copy's total row, a friend's own days (test-panel/DayTypesPanelTests.cs)
fails += TogetherWindowsTests.Run(now);   // 0.8.5: every Together category in the windows its data answers; the sums agree with the Deeds pages (test-panel/TogetherWindowsTests.cs)
fails += DeedsDaysPanelTests.Run(now);   // 0.8.5: a friend's Deeds pages in every window: their shared days = their own book, Everyone adds up, an older copy, 10 min while they play (test-panel/DeedsDaysPanelTests.cs)
fails += OlderCopyTests.Run(now);   // 0.8.2 (I): a copy from an older Hearthwoven: one line per page that misses something, in one form, none on this build's (test-panel/OlderCopyTests.cs)
fails += RejoinTests.Run(now);   // 0.8.2 (J): a fellow who reconnected during yours keeps their earlier connection's fights in your Session; foe counts add up (test-panel/RejoinTests.cs)
fails += SessionEmptyTests.Run(now);   // 0.8.6: a fellow quiet in battle since you came in keeps the page frame in Session (heading, plate, empty block inside; test-panel/SessionEmptyTests.cs)
fails += KeptLogTests.Run(now);   // 0.8.2: your minutes kept across a restart: 1 h and 3 h reach back, Session does not; a fellow's kept minutes count once (test-panel/KeptLogTests.cs)
fails += DefenceTypesTests.Run(now);   // 0.7 ISC-22: Defence > Received from per foe per damage type (test-panel/DefenceTypesTests.cs)
fails += SelfCheckTests.Run();   // Dev.SelfCheck bookkeeping, the filter key Tab and the clash scan (test-panel/SelfCheckTests.cs)
fails += PerfTests.Run();   // Dev.SelfCheck: the frame-cost meter and its one-minute line (test-panel/PerfTests.cs)
fails += PerfPassTests.Run();   // 0.8: the idle frame, the JSON writer, the log key cache, the meter's bytes, the bench numbers (test-panel/PerfPassTests.cs)
fails += HoverCostTests.Run(now);   // 0.8.2: the damage bars' hover makes no object per part, the page's one tag (test-panel/HoverCostTests.cs)
fails += AwayTests.Run(now);   // 0.7: Company > Since you were away opens itself once after a long break; the story never says 0 (test-panel/AwayTests.cs)
fails += FellowPanelTests.Run();   // same-name fellows each with a chip and a colour; the singleplayer line (test-panel/FellowPanelTests.cs)
fails += EveryoneTests.Run(now);   // 0.8: the Everyone chip's group view adds up, its greyed windows and pages say why (test-panel/EveryoneTests.cs)
fails += UsSpellingTests.Run();   // 0.8.1: US spelling (armor, defense, color, gray): no British form in a string literal of src/ (test-panel/UsSpellingTests.cs)
fails += RichTextTests.Run(textSamples);   // richtext-fix: no model string holds a tag; tags only via Rich, rich text only via the kind map (test-panel/RichTextTests.cs)
fails += CompareTests.Run(now);   // 0.8: Compare periods, this period beside the one before (test-panel/CompareTests.cs)
fails += FightWeaknessTests.Run(now);   // 0.9.4 draft: what the foe is weak to on a fight's page, A and B (test-panel/FightWeaknessTests.cs)
fails += FightPagesTests.Run(now);   // 0.9: the fight pages add up every way, the outcome words, Runs and bosses nested, bring next time (test-panel/FightPagesTests.cs)
fails += FoeNamesTests.Run(now);   // 0.8.5: one row per foe by the game's name, Hildir's minibosses as boss fights, no color tag in a label (test-panel/FoeNamesTests.cs)
fails += LayoutTests.Run(Array.IndexOf(args, "--dump") is int ld && ld >= 0 && ld + 1 < args.Length ? args[ld + 1] : null);   // 0.8.5: the book's layout numbers in one table, PanelLayout.cs: --dump writes preview/layout.css from it; the ratchet over the numbers still written out (test-panel/LayoutTests.cs)
fails += WashTests.Run();   // 0.8.5: every translucent colour draws in game as in the mirror (linear against sRGB), through PanelWash (test-panel/WashTests.cs)

var dump = Array.IndexOf(args, "--dump");
if (dump >= 0 && dump + 1 < args.Length)
{
    var dir = args[dump + 1];
    System.IO.Directory.CreateDirectory(dir);
    input.Fellows = new List<PanelInput> { edda }; edda.Fellows = new List<PanelInput> { input };   // as PanelUi wires the group (the --text dump below reads them)
    // sample-one (2026-10-09): EVERY preview shot reads the one sample world, PanelSample.Full (Rowan with Edda, Finch and Tor: SampleWorld), so the pages
    // never contradict each other (CoherenceTests.cs checks the identities). The only shots that read anything else are the NAMED SCENARIOS below, labelled in
    // preview-data.js (window.PANEL_SCENARIOS): states the world is not in (an empty first evening, a young character, a stress sample, an install that
    // began later, sharing off). A scenario's name says what it is for; none of them is a page of the world.
    var world = PanelSample.Full(now);
    var books = FullDump.Books(world).ToDictionary(b => b.who, b => b.book);   // Rowan's own; Edda, Finch and Tor as the copies Rowan receives
    var group = new[] { "Edda", "Tor", "Finch" };
    var scenarios = new Dictionary<string, string>();
    PanelInput Scenario(string name, string what, PanelInput inp) { scenarios[name] = what; return inp; }
    var young = Sample(); young.Character = new Dictionary<string, float> { ["Tree"] = 12, ["CraftFood"] = 3, ["FishCaught"] = 4 };   // a young character: a few names earned, the rest unsung
    young.Events = new SessionEvents(); young.Fellows = null;
    young.PiecesPlaced = new Dictionary<string, float> { ["$piece_woodwall"] = 12 }; young.ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 30, ["$item_stone"] = 8 }; young.ItemsCrafted = new Dictionary<string, float>();
    // a first evening with Hearthwoven: nothing measured yet, Meadows and Black Forest found, Eikthyr defeated
    var fresh = new PanelInput { PlayerName = "Rowan", Log = new EventLog(), Character = world.Character, EnemyKills = new Dictionary<string, float> { ["$enemy_eikthyr"] = 1 },
                                 KnownBiomes = new[] { "Meadows", "BlackForest" }, SkillLevels = world.SkillLevels };
    // an older install: the picked counters were first read two days ago (Deeds > Farming says so), on the world itself
    var installLater = PanelSample.Full(now);
    installLater.BaselineAt[LocalTotals.StatsKind] = installLater.BaselineAt[LocalTotals.PickablesKind] = installLater.NowUtc.AddDays(-2);
    // fish-held: Joost's own fishing book in game (ingame-0.6rc/deeds-fishing.json and his local totals, 9 Oct) on the sample character: 20 hooked,
    // 8 got away, no catch reeled in, 6 fish picked up (4 + 1 + 1); the rest of the sample stays
    var fishHeld = PanelSample.Full(now);
    fishHeld.Character = fishHeld.Character.Where(kv => !kv.Key.StartsWith("Fish")).ToDictionary(kv => kv.Key, kv => kv.Value); fishHeld.Character["FishHooked"] = 20; fishHeld.Character["FishLost"] = 8;
    fishHeld.Harvested = (fishHeld.Harvested ?? new Dictionary<string, float>()).Where(kv => !kv.Key.StartsWith("$")).ToDictionary(kv => kv.Key, kv => kv.Value);
    fishHeld.ItemsPickedUp = (fishHeld.ItemsPickedUp ?? new Dictionary<string, float>()).Where(kv => !kv.Key.StartsWith("$animal_fish")).ToDictionary(kv => kv.Key, kv => kv.Value);
    fishHeld.ItemsPickedUp["$animal_fish5"] = 4; fishHeld.ItemsPickedUp["$animal_fish1"] = 1; fishHeld.ItemsPickedUp["$animal_fish7"] = 1;
    // 0.7 rule A (hard case 10): the fish picked up since the first run are Hearthwoven's exact count too (the sample holds them as its own pickups)
    SessionEvents.Add(fishHeld.Events.PickedUp, "$animal_fish5", 4); SessionEvents.Add(fishHeld.Events.PickedUp, "$animal_fish1", 1); SessionEvents.Add(fishHeld.Events.PickedUp, "$animal_fish7", 1);
    // the game's own names (Hearthwoven's in-game icon dump, Fish5 / Fish7: "$animal_fish5" Trollfish, "$animal_fish7" Grouper)
    { var dn = fishHeld.DisplayName; fishHeld.DisplayName = k => k == "$animal_fish5" ? "Trollfish" : k == "$animal_fish7" ? "Grouper" : dn?.Invoke(k); }
    if (fishHeld.BaselineAt != null) { fishHeld.BaselineAt.Remove(LocalTotals.StatsKind); fishHeld.BaselineAt.Remove(LocalTotals.PickablesKind); }
    var fishHeldS = Scenario("deeds-fishing-picked", "case: Joost's fishing book in game, 20 hooked, 8 got away, none reeled in, 6 fish picked up another way", fishHeld);
    // new-history (B17/B19, Joost's first evening with 0.6.2, 9 Oct): the day history began today, so 7 days and 30 days do not work yet, and the
    // last half hour holds no fight: the greyed chips say their day, the empty Defence says one thing
    var newHistory = PanelSample.Full(now); newHistory.History.From = PanelModel.LocalToday(newHistory); newHistory.Log = new EventLog();
    var newHistoryS = Scenario("battle-defense-30min-new", "case: Joost's first evening, the day history began today (7 days and 30 days not yet), no fight in the last half hour", newHistory);
    scenarios["battle-defense-7days-new"] = scenarios["battle-defense-30min-new"];
    // joost-defence (B25-B27, Joost's first evening with 0.7, 10 Oct): Lox (trophy), Bat (no trophy), Smoke (no creature, raw damage) and a falling tree
    // (<1) hurt him this session; the armour ledger began today and holds nothing yet
    var joostDef = PanelSample.Full(now); joostDef.Log = new EventLog();
    joostDef.Log.AddDamage(now.AddMinutes(-40), "Plains", false, "Lox", "EnemyHit", new HitData.DamageTypes { m_blunt = 53, m_frost = 52, m_poison = 0.4f });
    joostDef.Log.AddDamage(now.AddMinutes(-30), "Plains", false, "Bat", "EnemyHit", new HitData.DamageTypes { m_slash = 14 });
    joostDef.Log.AddDamage(now.AddMinutes(-20), "Plains", false, "Smoke", "Smoke", new HitData.DamageTypes { m_damage = 12 });
    joostDef.Log.AddDamage(now.AddMinutes(-10), "Plains", false, "Tree", "Tree", new HitData.DamageTypes { m_blunt = 0.4f });
    { var foe = joostDef.Foe; joostDef.Foe = p => p == "Lox" ? new PanelModel.FoeData { Trophy = "TrophyLox" } : p == "Bat" ? new PanelModel.FoeData() : foe?.Invoke(p); }
    joostDef.ArmourSession = new ArmourTally(); joostDef.ArmourSince = new ArmourTally(); joostDef.ArmourBook = null; joostDef.ArmourMinutes = null; joostDef.ArmourFromUtc = now.AddHours(-1);
    // 0.8.5 (Battle > Overview on one screen, Joost: "also with nine biomes found"; Deaths as height, few and many: test-panel/BattleDenseTests.cs)
    var lateS = Scenario("battle-late", "stress: a late game, all nine lands found, seven fought in this session, five bosses down, eight falls in five lands: the strip's tiles at their narrowest, the falls folded", BattleDenseTests.Late(now));
    scenarios["battle-late-all"] = scenarios["battle-late-7days"] = scenarios["battle-late"];
    var deathsFullS = Scenario("battle-deaths-full", "case: a fuller evening, ten falls this session in four lands, five in the Swamp, each with its time", BattleDenseTests.DeathsFull(now));
    var deathsManyS = Scenario("battle-deaths-many", "stress: a long campaign, 40 falls in the game's count and 32 placed, 14 in the Swamp; 8 with no place and why", BattleDenseTests.DeathsMany(now));
    var joostDefS = Scenario("battle-defense-joost", "case: Joost's first evening with 0.7: Lox, a bat, smoke and a falling tree hurt him; nothing on his armour yet (the ledger began today)", joostDef);
    scenarios["battle-defense-joost-armour"] = scenarios["battle-defense-joost"];
    // stoker (0.7): the group's real case, smelters fed by Stoker's Chests (OverDrive-SmelterUpgrades): hardly anything put in by hand, hundreds by the chests
    var stoker = PanelSample.Full(now); stoker.Events.SmelterAdded.Clear();
    SessionEvents.Add(stoker.Events.SmelterAdded, "smelter|fuel", 6); SessionEvents.Add(stoker.Events.SmelterAdded, "charcoal_kiln|Wood", 15);
    SessionEvents.Add(stoker.Events.ChestFed, "blastfurnace|IronScrap", 420); SessionEvents.Add(stoker.Events.ChestFed, "blastfurnace|fuel", 210);
    SessionEvents.Add(stoker.Events.ChestFed, "smelter|CopperOre", 180); SessionEvents.Add(stoker.Events.ChestFed, "smelter|fuel", 240); SessionEvents.Add(stoker.Events.ChestFed, "charcoal_kiln|Wood", 600);
    // compare (0.8, CompareModel.cs): the world with two weeks of day history (CompareTests.Scenario), so 7 days can be set beside the 7 before
    var compareS = Scenario("deeds-woodcutting-compare", "case: two weeks of day history, so 7 days sets beside the 7 before (the earlier week a little different per kind)", CompareTests.Scenario(now));
    foreach (var n in new[] { "deeds-mining-compare", "deeds-cooking-compare", "deeds-building-compare", "deeds-farming-compare", "deeds-fishing-compare", "deeds-taming-compare", "battle-defense-armour-compare", "battle-damage-compare", "battle-damage-type-compare",
                              "battle-defense-compare", "voyages-sailing-compare", "voyages-cargo-compare", "company-together-dealt-compare",
                              "battle-damage-foe-compare", "everyone-battle-damage-compare", "everyone-woodcutting-compare-greyed",
                              "deeds-woodcutting-today-compare-week", "deeds-woodcutting-today-compare-best", "deeds-woodcutting-today-compare-menu", "deeds-building-compare-menu", "deeds-building-compare-best" }) scenarios[n] = scenarios["deeds-woodcutting-compare"];
    var stokerS = Scenario("hall-smelters-stoker", "case: the group's smelters fed by Stoker's Chests (a smelter mod): 21 put in by hand, 1 650 by the chests", stoker);
    // 0.8.5 (Joost in game, Everyone > Smelters: "Put in together, by kind" stood as a heading with no rows): the chests fed the smelters, and
    // only Edda put one item in by hand, one kind (Soft tissue in the eitr refinery)
    var handOne = PanelSample.Full(now); handOne.Events.SmelterAdded.Clear(); handOne.Events.ChestFed.Clear();
    foreach (var f in handOne.Fellows) { f.Events?.SmelterAdded.Clear(); f.Events?.ChestFed.Clear(); }
    SessionEvents.Add(handOne.Fellows.First(f => f.PlayerName == "Edda").Events.SmelterAdded, "eitrrefinery|Softtissue", 1);
    // what the chests fed, counted once by the server (0.8.5, ServerBook.World), the same on every book
    var fedWorld = new ServerBook.World { FedFrom = now.Date.AddHours(-6) };
    foreach (var (item, n) in new[] { ("Wood", 640.0), ("Coal", 410.0), ("IronScrap", 380.0), ("CopperOre", 120.0), ("TinOre", 60.0) }) fedWorld.ChestFed[item] = n;
    handOne.World = fedWorld; foreach (var f in handOne.Fellows) f.World = fedWorld;
    var handOneS = Scenario("everyone-hall-smelters-one", "case: the group's evening in game: the chests fed the smelters (counted by the server), and only Edda put one item in by hand, one kind", handOne);
    scenarios["hall-smelters-server"] = "case: the same evening on your own book: nothing put in by hand, what the group's chests fed as the server counted it";
    scenarios["feats-titles-stoker"] = scenarios["hall-smelters-stoker"];
    // 0.8.2 (Joost in game: The Mead Hall's "Who brewed" ended in "+3 more"): FeatsTests.Brewers, every fellow brewing; then three more who share
    scenarios["feats-together-meadhall"] = "case: every fellow brewed mead bases, so The Mead Hall's crew holds all four";
    scenarios["feats-together-meadhall-seven"] = "case: a group of seven (Ylva, Bryn and Sigrid share and brew too), so The Mead Hall's crew holds seven";
    var emptyS = Scenario("battle-empty", "empty: a first evening with Hearthwoven, nothing measured yet", fresh);
    scenarios["battle-feed-empty"] = scenarios["battle-empty"];
    scenarios["deeds-meals-empty"] = "empty: a first evening with Hearthwoven, nothing eaten yet (Deeds > Meals)";
    var mealsOutside = PanelSample.Full(now);   // Ylva, who does not share, cooked a bread and two fish wraps Rowan ate; a feast set out by someone not around the fire
    SessionEvents.Add(mealsOutside.Events.AteFoodMadeBy, "Ylva|Bread", 1); SessionEvents.Add(mealsOutside.Events.AteFoodMadeBy, "Ylva|FishWraps", 2); SessionEvents.Add(mealsOutside.Events.AteFromFeastOf, "999|FeastMeadows", 1);
    var mealsOutsideS = Scenario("everyone-meals-outside", "case: a cook outside the group (Ylva does not share) by name in gray, a feast whose maker is not known as Another player", mealsOutside);
    // 0.9 Meals' Your combinations (Chapters/MealCombosModel.cs, PanelSampleMeals.cs): Rowan's combinations, then none yet and not recorded on this book
    scenarios["deeds-meals-combos"] = "case (0.9 Meals): Rowan's food combinations, recorded from 3 Oct: Carrot Soup, Queen's Jam and Honey most, three of the top five lean on stamina";
    scenarios["deeds-meals-combos-7days"] = "case (0.9 Meals): the combinations of the last 7 days; the record counts them from 3 Oct, one line says so";
    var combosEmpty = PanelSample.Full(now); combosEmpty.MealCombos = MealCombosSample.For(combosEmpty, "empty");
    var combosEmptyS = Scenario("deeds-meals-combos-empty", "case (0.9 Meals): combinations recorded from an hour ago, none yet: the island's empty state under the meals", combosEmpty);
    var combosNone = PanelSample.Full(now); combosNone.MealCombos = MealCombosSample.For(combosNone, "notrecorded");
    // 0.9.2 Plan a meal (Chapters/MealPlanModel.cs, PanelSampleMeals.cs MealPlanSample); 0.9.5 its own page, Deeds > Meal plan, the meal pinned under the strip
    scenarios["deeds-mealplan"] = "case (0.9.5 Meal plan): nothing picked yet: three empty slots pinned under the strip, the foods of the Mountains, Swamp and Black Forest (Rowan's furthest lands) under them";
    scenarios["deeds-mealplan-three"] = "case (0.9.5 Meal plan): Wolf Skewer, Eyescream and the Mountains feast: the summed bar on the planner's one scale, the first to wear off";
    scenarios["deeds-mealplan-three-scrolled"] = "case (0.9.5 Meal plan): the same three, the page scrolled down to the Black Forest's foods: the slots and the summed bar stay pinned at the top";
    scenarios["deeds-mealplan-combo"] = "case (0.9.5 Meal plan): opened from a row of Your combinations on Meals (Carrot Soup, Honey, Queen's Jam in the slots), the strip's Back leads to Meals";
    var combosNoneS = Scenario("deeds-meals-combos-notrecorded", "case (0.9 Meals): no combination record on this book yet (the totals not loaded): Not recorded yet, never a 0", combosNone);
    // 0.9.3: Rowan in the Ashlands with Best for: Eitr (sorted, filtered, the eitr part first)
    var mealsAsh = PanelSample.Full(now); mealsAsh.KnownBiomes = new List<string> { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "Ocean" };
    if (mealsAsh.Deeds == null) mealsAsh.Deeds = new DeedLog();
    mealsAsh.Deeds.NoteLand(now.AddMinutes(-40), "Mistlands"); mealsAsh.Deeds.NoteLand(now.AddMinutes(-20), "AshLands");
    var mealsAshS = Scenario("deeds-mealplan-eitr", "case (0.9.3 Meal plan): Rowan in the Ashlands, Best for: Eitr: only the foods that give most eitr, most first, the eitr part first in their bars", mealsAsh);
    scenarios["deeds-mealplan-ash"] = "case (0.9.3 Meal plan): Rowan in the Ashlands, Best for: Any: the foods of the Ashlands, Mistlands and Plains, strongest first";
    // 0.8.1 review 1: a fellow with a longer name whose copy is not live, so their group row says "as of" beside it: the name gives way, never the date
    var finchAsOf = PanelSample.Full(now); foreach (var f in finchAsOf.Fellows.Where(f => f.PlayerName == "Finch")) f.Timed = false;
    var finchAsOfS = Scenario("everyone-woodcutting-asof", "case: Finch sends no live updates, so their row says when their copy is from beside a longer name", finchAsOf);
    // 0.8.1 review 5: Recent's group of three, so one player's island has no partner: it stays a half like the others
    var groupOfThree = PanelSample.Full(now); groupOfThree.Fellows.RemoveAll(f => f.PlayerName == "Finch");
    var threeS = Scenario("deeds-recent-group-three", "case: a group of three (Finch not sharing), so the last player's island stands alone", groupOfThree);
    // 0.9.1 (Joost in game: "who was on yesterday and what happened yesterday"): Recent with Everyone on in Yesterday and 3 days. Edda shares her days, Finch did
    // nothing yesterday (his copy holds no day for it, his other days stay), Tor's Hearthwoven is 0.8.3 (it shares no days)
    var groupDaysS = Scenario("deeds-recent-group-yesterday", "case (0.9.1): Recent with Everyone on, Yesterday: Edda shares her days, Finch did nothing yesterday, Tor's Hearthwoven 0.8.3 shares no days", YesterdayTests.GroupDays(now));
    scenarios["deeds-recent-group-3days"] = "case (0.9.1): the same group on 3 days: Finch's other days show, Tor's still are not shared";
    // 0.8.1 (Joost sailing, Battle > Feed): a hit that took <1 in the last fight, so a feed line carries "<1"
    var feedLess = PanelSample.Full(now);
    var draugrHit = feedLess.Log.Damage.Keys.Select(k => k.Split('|')).Where(k => k.Length >= 6 && k[2] == "taken" && k[3] == "Draugr").OrderBy(k => k[0], StringComparer.Ordinal).Last();
    feedLess.Log.AddDamage(DateTime.ParseExact(draugrHit[0], "yyyy-MM-dd'T'HH:mm'Z'", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal).AddSeconds(30),
                           draugrHit[1], false, "Draugr", draugrHit[4], new HitData.DamageTypes { m_blunt = 0.4f });
    Hearthwoven.Panel.BattleSample.Session(feedLess, now, new[] { "Edda", "Tor" }, new[] { "Edda", "Tor", "Finch" });   // the battle record again, from the log with that hit (as SampleWorld does)
    var feedLessS = Scenario("battle-feed-cards-less", "case: a Draugr's hit took <1 (blunt 0.4) in the last fight, so a feed line says <1", feedLess);
    // rc2: the same first evening on Today (the day history open, nothing fought today): its own line and the biome tiles, as Session
    var freshDay = new PanelInput { PlayerName = "Rowan", NowUtc = now, ToLocal = t => t.AddHours(2), Log = new EventLog(), Character = world.Character, EnemyKills = fresh.EnemyKills,
                                    KnownBiomes = fresh.KnownBiomes, SkillLevels = world.SkillLevels, History = new DayHistory() };
    var emptyDayS = Scenario("battle-today-empty", "empty: a first evening on Today, nothing fought today", freshDay);
    var youngS = Scenario("deeds-young", "young: a character with a few names earned and the rest unsung", young);
    var laterS = Scenario("deeds-farming-later", "install-later: the picked counters were first read two days after the install", installLater);
    var bigS = Scenario("deeds-farming-big", "stress: a long-played character, thousands of crops", DeedsTests.Big(Sample()));
    var layeredS = Scenario("deeds-farming-layered", "case: the live Flax case, the game's planted counter beside every plant counted", DeedsTests.Layered(Sample()));
    // 0.8.1 (Joost's PC: his 385 piece kinds drew 2 630 objects on Building): a long-played book with hundreds of kinds, so render-check sees the folds
    var manyKindsS = Scenario("deeds-building-manykinds", "stress: a long-played book with a mod-heavy hammer (400 piece kinds on 30 tabs): the grid folded, its last tile the Show all button", ManyKindsTests.World(now));
    scenarios["deeds-building-manykinds-bycategory"] = "stress: the same book By category: the tabs past eight fold into one Other group, each group's grid short, ending on Show all";
    scenarios["deeds-building-manykinds-showall"] = "stress: the same book after Show all: every piece, the full list as before 0.8.1 (this visit only)";
    var moddedHallS = Scenario("deeds-building-modded", "case: Joost's modded hammer in game (0.6.2): nineteen tabs with \"Misc\" and \"Misc.\", twenty materials; the bars fold past eight parts", BuildingSample.Modded(PanelSample.Full(now)));
    // salvage (0.8): the old hall taken down and a new one begun, few pieces built yet, so the materials recovered sit in view (the world's hall is too long
    // for the shot): three kinds as the bar form, and one kind as one tile; no game data for the pieces' tabs, so no filter bar
    PanelInput Rebuild(Dictionary<string, float> placed, Dictionary<string, float> recovered)
    {
        var r = PanelSample.Full(now); r.PiecesPlaced = placed; r.PieceTab = null; r.PieceMaterial = null; r.Baseline?.Remove(LocalTotals.PlacedKind);
        if (recovered != null) { r.Events.Recovered.Clear(); foreach (var kv in recovered) r.Events.Recovered[kv.Key] = kv.Value; }
        return r;
    }
    var rebuildS = Scenario("deeds-building-recovered", "case: the old hall taken down, a new one begun: 30 pieces built, 80 materials recovered (wood, stone, core wood)",
                            Rebuild(new Dictionary<string, float> { ["$piece_woodwall"] = 24, ["$piece_stonewall1x1"] = 6 }, null));
    var rebuildOneS = Scenario("deeds-building-recovered-one", "case: one stone corner taken down: 24 stone recovered, one kind as one tile",
                               Rebuild(new Dictionary<string, float> { ["$piece_stonewall1x1"] = 6 }, new Dictionary<string, float> { ["$item_stone"] = 24 }));
    var miningHeroS = Scenario("deeds-mining-hero", "case: Mining's long hero, stone and ore brought in, pickaxe hits with their date, the Pickaxes skill and its growth line (0.8.2: one baseline, the skill on the first line)", RocksTests.HeroScenario(now));
    var rocksS = Scenario("deeds-mining-rocks", "case: a pickaxe on a tin deposit, plain rocks (one broken into its pieces), a stump and a berry bush (0.8.2)", RocksTests.Scenario(now));
    var recentEmpty = PanelSample.Full(now); recentEmpty.Deeds = new DeedLog(); recentEmpty.SessionStartUtc = now.AddMinutes(-4);   // a session four minutes old: nothing gathered yet
    var recentEmptyS = Scenario("deeds-recent-empty", "empty: a session four minutes old, nothing gathered yet (Deeds > Recent)", recentEmpty);
    // 0.7: Company > Since you were away (Chapters/AwayModel.cs): back after a break, the fellows' marks as your PC last saw them (AwayTests.Scenario)
    var away3S = Scenario("company-away-3days", "case: back after 3 days; Edda, Finch and Tor played meanwhile (your PC's marks of them at 80% of today's totals)", AwayTests.Scenario(now, 72, new[] { "Edda", "Finch", "Tor" }, 0.8));
    scenarios["company-away-3days-hover"] = scenarios["company-away-3days-numbers"] = scenarios["company-away-3days"];
    var away30S = Scenario("company-away-30h", "case: back after 30 hours; only Edda shares (her mark at 95% of today's totals)", AwayTests.Scenario(now, 30, new[] { "Edda" }, 0.95));
    var awayFirstS = Scenario("company-away-first", "case: back after 3 days; Tor shares for the first time, Edda as before", AwayTests.Scenario(now, 72, new[] { "Edda", "Tor" }, 0.85, "Tor"));
    // 0.8.5 (While away on one screen): five fellows, the most that fit the plate; six, the first that scrolls (render-check's rule)
    var away5S = Scenario("company-away-5", "stress: back after 3 days; five fellows played meanwhile (Ylva and Bryn are copies of Edda's and Tor's records, renamed)", AwayTests.Scenario(now, 72, AwayTests.Five, 0.8));
    var away6S = Scenario("company-away-6", "stress: back after 3 days; six fellows (Sigrid a copy of Finch's record): the page scrolls, the rows keep their form", AwayTests.Scenario(now, 72, AwayTests.Six, 0.8));
    var cookedS = Scenario("deeds-cooking-layered", "case: dishes taken off a fellow's grill, the game books them to the station's owner", DeedsTests.Cooked(Sample(), edda));
    // realBook (0.7 redesign): Joost's real book from his 0.6 RC local totals (every baseline from 8 or 9 Oct, nothing counted since), RecordedTests.RealBook
    var realBook = RecordedTests.RealBook();
    if (realBook != null) foreach (var n in new[] { "real-woodcutting", "real-farming", "real-fishing" }) scenarios[n] = "real: Joost's own book from his 0.6 RC local totals (9 Oct), every since-install part 0";
    scenarios["deeds-unsung"] = "young: the unsung names of a young character"; scenarios["company-together-alone"] = "alone: nobody else shares yet";
    scenarios["company-food-alone"] = "alone: nobody else shares yet"; scenarios["sharing-off"] = "sharing off: no fellow players"; scenarios["joining"] = "joining: Ylva just joined, their first copy not here yet (0.7)"; scenarios["joining-alone"] = "joining: nobody else shares yet, Ylva just joined (0.7)";
    scenarios["company-fireside-busy"] = "stress: a busy six around the fire (gifts both ways on most pairs), FireLayout's test group; the drawing only";
    scenarios["company-fireside-four"] = "case: Joost's evening of 10 Oct (four players, feasts both ways, gear and food to and from you, a few gifts among fellows), sample names";
    // REVIEW-08 #2: Tor was playing before you came in, so Tor's Session counts per foe (one tally of the whole connection) are left out, one line says so
    var torBefore = books["Tor"].ShallowCopy();
    if (torBefore.FirstRecordedUtc.HasValue && torBefore.LastRecordedUtc.HasValue) torBefore.ViewerSessionStartUtc = torBefore.FirstRecordedUtc.Value + TimeSpan.FromTicks((torBefore.LastRecordedUtc.Value - torBefore.FirstRecordedUtc.Value).Ticks / 2);
    // REVIEW-081 #7: a long foe name with its "×3" (the name column fits the list's widest name and "×N", 200 to 230 px): three Greydwarf Shamans in
    // Rowan's last fight, so By foe and Last fight both carry "Greydwarf Shaman ×3"; the battle record replayed from the log as the world does
    var longFoe = PanelSample.Full(now);
    foreach (var m in new[] { -4, -3, -2 }) longFoe.Log.AddDamage(now.AddMinutes(m), "Swamp", true, "Greydwarf_Shaman", "Swords", new HitData.DamageTypes { m_slash = 130 });
    longFoe.Session = SampleWorld.TallyOf(longFoe.Log); longFoe.DamageSinceInstall = PanelSample.SinceInstall(longFoe.Log);
    Hearthwoven.Panel.BattleSample.Session(longFoe, now, new[] { "Edda", "Tor" }, new[] { "Edda", "Tor", "Finch" });
    var longFoeS = Scenario("battle-damage-foe-long", "case: three Greydwarf Shamans in the last fight: a long foe name with its \"×3\" in By foe and Last fight (REVIEW-081 #7)", longFoe);
    scenarios["battle-lastfight-long"] = scenarios["battle-feed-long"] = scenarios["battle-damage-foe-long"];
    // 0.8.2 (the hover's cost, HoverCostTests): a long feed, 100 more foes in the session in 20 fights of five, the newest FeedTop drawn, every
    // part's name and number on hover through the page's one tag
    var feed100S = Scenario("battle-feed-100", "case: a long feed: 100 more foes in this session, 20 fights of five, each hit with three damage types; the newest fights up to 40 foes drawn, Show older foes at the end (HoverCostTests.LongFeed)", HoverCostTests.LongFeed(now));
    scenarios["battle-feed-100-end"] = "case (0.8.5): the long feed scrolled to its end: Show older foes, and how many foes and fights are shown and left";
    scenarios["battle-feed-100-scrolled"] = "case (0.8.6): the long feed scrolled part way down: the strip (its caption, the Log, Cards and Timeline chips, About these numbers) stays at the plate's top, the fights pass under it, the shade under it";
    scenarios["battle-feed-100-more"] = "case (0.8.5): the long feed after one press of Show older foes, scrolled to its end: the next fights, whole, about 40 foes more, and what is shown and left";
    // 0.8.2 (Joost in game, Battle > Foes: The Queen's Dealt read "12 6…"): a long-played book whose top foes dealt 123 456 and 12 645, so the
    // Dealt column shows a 6-digit number with its space beside a full bar (render-check measures the column's room against "123 456")
    var bigFoes = PanelSample.Full(now);
    bigFoes.DamageSinceInstall.Dealt["SeekerQueen|Bows|pierce"] = 98000; bigFoes.DamageSinceInstall.Dealt["SeekerQueen|Swords|slash"] = 25456;
    bigFoes.DamageSinceInstall.Dealt["SeekerBrute|Swords|slash"] = 12645;
    bigFoes.DamageSinceInstall.Dealt["Greydwarf_Shaman|Swords|slash"] = 3400; bigFoes.DamageSinceInstall.Dealt["Charred_Archer|Swords|slash"] = 2900;   // the longest foe names (0.8.2: the name box fits them)
    { var foe = bigFoes.Foe; bigFoes.Foe = p => p == "SeekerQueen" ? new PanelModel.FoeData { Trophy = "TrophySeekerQueen", Modifiers = { ["pierce"] = "Resistant", ["fire"] = "Weak" } } : p == "SeekerBrute" ? new PanelModel.FoeData { Trophy = "TrophySeekerBrute", Modifiers = { ["frost"] = "Weak" } } : foe?.Invoke(p);
      var dn = bigFoes.DisplayName; bigFoes.DisplayName = k => k == "SeekerQueen" ? "The Queen" : k == "SeekerBrute" ? "Seeker Soldier" : k == "Charred_Archer" ? "Charred Marksman" : dn?.Invoke(k); }
    var bigFoesS = Scenario("battle-foes-big", "case: a long-played book whose top foes dealt 123 456 (The Queen) and 12 645 (Seeker Soldier): 6- and 5-digit numbers whole in the Dealt column; Greydwarf Shaman and Charred Marksman, the longest foe names, whole", bigFoes);
    var torBeforeS = Scenario("tor-battle-damage-foe-before", "case: Tor was playing before you came in: Tor's counts per foe show in All", torBefore);
    // 0.9 fight pages (Chapters/FightPages.cs): the fight book the fight adapter will fill (v09-fights), not in the world yet (PanelSampleFights.cs)
    var fightsLiveS = Scenario("battle-bossfight", "case (0.9): the fight pages' sample, The Queen fought now in the Infested Citadel (the fights are not in the world until the fight adapter lands)", FightsSample.With(PanelSample.Full(now), true));
    var fightsAfterS = Scenario("battle-lastfight-group", "case (0.9): the fight pages' sample after the fights: the newest a Greydwarf camp", FightsSample.With(PanelSample.Full(now), false));
    // 0.8.6 (Last fight is yours): a fellow's newer fight you were not in, as one quiet line under your own Last fight, and that fight's own page
    var serverBook = FightsSample.WithServerFight(FightsSample.With(PanelSample.Full(now), false));
    var lastServerS = Scenario("battle-lastfight-server", "case (0.8.6): Last fight is the Greydwarf camp you were in; Edda fought a Seeker 3 minutes ago without you: \"Newer on the server\" under it, a link to her fight", serverBook);
    // 0.8.6 (DPS): the same fight with the boss nearly down, 22 % left and 9 % lost in the last minute: at this pace 2:27 left, too long for the short fill, so it follows the lane's note
    var fightsLow = FightsSample.With(PanelSample.Full(now), true); fightsLow.Fights.LiveBoss.Health = 0.22f; fightsLow.Fights.LiveBoss.HealthMinuteAgo = 0.31f;
    var fightsLowS = Scenario("battle-bossfight-low", "case (0.8.6): the Queen fought now with 22 % left, down 9 % in the last minute: the time left at this pace (2:27) after the lane's note, as the fill is too short to hold it", fightsLow);
    foreach (var n in new[] { "battle-bossfight-open", "battle-bossfight-numbers", "battle-bossfight-stacked", "battle-runs", "battle-fight-bonemass", "battle-fight-queen-reset", "battle-run-mine", "battle-run-opened", "battle-fight-opened", "battle-runs-today" }) scenarios[n] = scenarios["battle-bossfight"];
    scenarios["battle-fight-server"] = scenarios["battle-lastfight-server"];   // 0.8.6: the fellow's fight's own page
    // 0.8.6 (Joost in game: a friend's Last fight was the 0.8 page, with other numbers than his own for the same fight): Edda's book on Rowan's PC reads the
    // fights his PC joined, seen by her
    var eddaFightS = Scenario("edda-battle-lastfight-fight", "case (0.8.6): Edda's Last fight on Rowan's PC: the Greydwarf camp as Rowan's page shows it, seen by her: Edda first as the book's owner, \"Rowan (you)\" near her 12 s, her DPS in the head, the same foes, dealt and received, By type, together under the players", FightsSample.Friend(FightsSample.With(PanelSample.Full(now), false), "Edda"));
    scenarios["battle-runs-30min"] = scenarios["battle-lastfight-group"];   // 0.8.6: the fights after the fights, none in 30 min
    // 0.8.5 (test-panel/FoeNamesTests.cs): the ranged Draugr beside the Draugr, one row by the game's name; Brenna, Hildir's miniboss, a boss fight through
    // the real recorder although the game flags no boss, her Skeletons with and without a bow one row, her name's color tags off
    var kinS = Scenario("battle-damage-byfoe", "case (0.8.5): a few ranged Draugr in the Swamp this session: Damage by foe has one Draugr row, both kinds' damage and x N added up", FoeNamesTests.KinBook(now));
    // 0.8.6 (Joost's PC, 11 Oct: two runs listed as "A dungeon", one a 34 s look with nothing fought): saved runs shaped as the PC's, through the adapter
    var savedRunsS = Scenario("battle-runs-saved", "case (0.8.6): runs a restart kept, with the place's game name and no name token, as on Joost's PC: an Infested Mine (1 death), a Burial Chamber, and a 34 s look into another one (kept, not listed)", FightsSample.WithSavedRuns(PanelSample.Full(now)));
    // 0.9.4 draft (h094-weakness, Chapters/FightWeakness.cs): what the foe is weak to on a fight's page, A (under the foe rows) and B (on By type, together)
    var weakBossS = Scenario("battle-lastfight-weak-boss", "case (0.9.4 draft): Last fight is Moder alone, won 4 min ago: her line under the foe rows, each group in its own form (weak to pierce and fire: solid chips; resists blunt: open; immune to frost: struck). SAMPLE modifiers, not the game's", FightsSample.WithSoloBoss(FightsSample.With(PanelSample.Full(now), false)));
    var weakTwoS = Scenario("battle-lastfight-weak-twokinds", "case (0.9.4 draft): Last fight is a Swamp fight with Draugr and Blob: each kind its own named line (Blob's wraps)", FightsSample.WithTwoKinds(FightsSample.With(PanelSample.Full(now), false)));
    var weakNoneS = Scenario("edda-battle-lastfight-weak-none", "case (0.9.4 draft): Edda's Last fight on a PC whose foe data carries no modifiers: the Moder fight with no weakness line (nothing guessed)", FightsSample.WithoutModifiers(FightsSample.Friend(FightsSample.WithSoloBoss(FightsSample.With(PanelSample.Full(now), false)), "Edda")));
    var weakRetoS = Scenario("battle-lastfight-weak-reto", "case (0.9.4 draft): Joost's Last fight retold with the sample cast: Lord Reto (prefab Fader, SAMPLE modifiers) in the Ashlands, fought now, 13 032 dealt, 337 received, your DPS 107", FightsSample.WithReto(FightsSample.With(PanelSample.Full(now), false)));
    var brennaS = Scenario("battle-bossfight-brenna", "case (0.8.5): Brenna, Hildir's miniboss the game does not flag a boss, won with her five Skeletons around her (three with a bow, two without: one row)", FightsSample.WithBrenna(PanelSample.Full(now), FoeNamesTests.TaggedBrenna));
    // 0.8.5 (Joost in game, after a restart: "last fight should show your last fight, but indicate that it was x hours/days ago. Same for the feed."):
    // the sample evening kept as an earlier session (KeptFights), Edda and Tor beside you in its last fight, two hours on
    var lastEarlierS = Scenario("battle-lastfight-earlier", "case (0.8.5): two hours after a restart, nothing fought yet: Last fight is the evening's last fight with Edda's and Tor's parts, \"2 hours ago\"", Hearthwoven.Panel.BattleSample.Restarted(PanelSample.Full(now), TimeSpan.FromHours(2)));
    var feedEarlierS = Scenario("battle-feed-earlier", "case (0.8.5): two hours after a restart, one fight since: the feed in 7 days, this session's fight over the evening's, the evening under its own line", Hearthwoven.Panel.BattleSample.Restarted(PanelSample.Full(now), TimeSpan.FromHours(2), 10));
    // 0.8.2 (H): Edda logged out five minutes ago, so her copy is not live: 10 min to 3 h greyed, the pointer on 10 min shows why
    var eddaOff = books["Edda"].ShallowCopy(); eddaOff.Trail = null; eddaOff.ReceivedUtc = now.AddMinutes(-5);
    // 0.8.6 (a friend's older feed, work/hearthwoven-0.8.6/DESIGN-feed-blocks.md): Edda's All: her copy's fights, then the session from three days back under its own line, the count
    // line naming the session from nine days back that is still on the server
    var eddaOlder = books["Edda"].ShallowCopy(); SampleSessions.Give(eddaOlder, now, 5);
    var eddaOlderS = Scenario("battle-feed-fellow-older", "case (0.8.6): Edda's feed on All, scrolled to its end: her copy's last fights, then her session of three days back under its day and span, and the count line naming her session of nine days back that is still on the server (Show older foes)", eddaOlder);
    // 0.8.6 (Joost in game, test build 5: "the loading took a while ... We should have something of a progress indicator there"): Edda has not fought this session and none of
    // her sessions is on this PC yet; her All asks the server for the newest of the two, six seconds on its way: the line counts where it is and the seconds
    var eddaFetching = books["Edda"].ShallowCopy(); eddaFetching.SharedFeed = new FeedShare(); var fetchingNear = SampleSessions.Give(eddaFetching, now, 150);
    eddaFetching.BlockSessions = new Dictionary<string, KeptSession>(); eddaFetching.BlockStates = new Dictionary<string, string> { [fetchingNear.Id] = "loading" };
    eddaFetching.BlockWaited = new Dictionary<string, int> { [fetchingNear.Id] = 6 }; eddaFetching.BlocksStamp = "fetching";
    var eddaFetchingS = Scenario("battle-feed-fellow-fetching", "case (0.8.6): Edda's feed on All with none of her sessions on this PC yet: the newest of her two sessions on the server is on its way, its line says \"1 of 2\" and the seconds it has waited (the book counts them on that line only)", eddaFetching);
    // 0.8.6 (Joost in game, test build 4): Edda in the world and live, nothing fought since you came in: her Damage in Session keeps the page frame
    var eddaQuietS = Scenario("edda-battle-damage-session-quiet", "case (0.8.6): Edda is in the world but has not fought since you came in: her Damage in Session keeps the page's heading, chips and plate, the empty block inside", SessionEmptyTests.Quiet(books["Edda"], now));
    var eddaOffS = Scenario("edda-battle-offline", "case: Edda's last update came five minutes ago (not playing now): her 10 min to 3 h greyed, the reason under 10 min", eddaOff);
    // 0.8.2 (I): Edda's copy as Hearthwoven 0.6.5 sends it (SampleWorld.OlderJson): the pages that miss something say it once
    var eddaOld = SampleWorld.Share(SampleWorld.Make(now).owns[SampleWorld.Edda], now, "0.6.5"); var eddaBook = books["Edda"];
    eddaOld.Fellows = eddaBook.Fellows; eddaOld.NowUtc = eddaBook.NowUtc; eddaOld.ViewerName = eddaBook.ViewerName; eddaOld.PlayerNames = eddaBook.PlayerNames; eddaOld.Trail = eddaBook.Trail;
    eddaOld.ServerLive = true; eddaOld.Timed = false; eddaOld.OnThisSession = eddaBook.OnThisSession; eddaOld.ViewerSessionStartUtc = eddaBook.ViewerSessionStartUtc; eddaOld.ReceivedUtc = eddaBook.ReceivedUtc;
    var eddaOldS = Scenario("edda-old-battle-defense", "case: Edda's copy as Hearthwoven 0.6.5 sends it: Defense says once what fills in when she updates", eddaOld);
    scenarios["edda-old-battle-feed"] = "case: Edda's copy as Hearthwoven 0.6.5 sends it: no feed, and the one line why";
    // 0.9 armor-share (Joost, 18 Oct: "add friends' armour view ... to 0.9"): Edda's Defense with the view switch in her words, both views, and an older copy's one line
    scenarios["edda-battle-defense-hurt"] = "case (0.9): Edda's Defense this session, What hurt them chosen: her Received from as before, the switch to What their armor stopped above it";
    scenarios["edda-battle-defense-armor"] = "case (0.9): Edda's Defense this session, What their armor stopped: her shared armor minutes from your session's start, the same drawer, \"Recorded from <date> · their PC\", types only";
    scenarios["edda-battle-defense-armor-numbers"] = "case (0.9): Edda's armor view with About these numbers open: a friend's lines (Windows, Shared), undated, never \"Complete for you\"";
    scenarios["edda-old-battle-defense-armor"] = "case (0.9): Edda's copy from Hearthwoven 0.6.5 shares no armor: What their armor stopped says so in one line, never an empty or zero bar";
    // 0.8.2 (J): Edda on 0.6.5 reconnected after her fights, so her new connection's copy has an empty log; the trail joins her earlier
    // connection's under it, so her Session's Foes still show them (test-panel/RejoinTests.cs)
    var eddaNew = eddaOld.ShallowCopy(); eddaNew.SessionId = "s2"; eddaNew.Log = new EventLog { Span = eddaOld.Log.Span, GuardKept = false }; eddaNew.Session = new DamageTally(); eddaNew.RecordedFromLog();
    var eddaRejoin = new FellowTrail(eddaOld, now.AddMinutes(-30)); eddaRejoin.Add(eddaNew, now.AddMinutes(-20));
    var eddaRejoined = eddaRejoin.Joined(eddaNew); eddaRejoined.Trail = eddaBook.Trail;
    var eddaRejoinedS = Scenario("edda-rejoined-battle-foes", "case: Edda (Hearthwoven 0.6.5) reconnected after her fights: her new copy's log is empty, her Session's Foes still show them from her earlier connection", eddaRejoined);
    bool FoeRows(PanelInput b) => PanelModel.Content(PanelModel.Build(b, As("Edda", Chapter.Battle, "foes", s => { s.Window = TimeWindow.Session; s.WindowPicked = true; }))).Any(x => x.Kind == "foetable");
    Check(FoeRows(eddaRejoined) && !FoeRows(eddaNew), "J preview: Edda's Session Foes after her reconnect show her earlier connection's fights; her new copy alone showed none");
    PanelState Battle(TimeWindow w, string page = null, Dictionary<string, List<string>> facets = null)
    {
        var s = new PanelState { Chapter = Chapter.Battle, Window = w }; if (page != null) s.Page[Chapter.Battle] = page;
        if (facets != null) foreach (var kv in facets) s.Facets[kv.Key] = kv.Value;
        return s;
    }
    PanelState Armour(TimeWindow w) { var s = Battle(w, "defense"); s.View["Battle/defense/view"] = PanelModel.ArmourId; return s; }
    PanelState FoesOpen(string foe, TimeWindow w = TimeWindow.Session) { var s = Battle(w, "foes"); s.View[PanelModel.FoeOpenKey] = foe; return s; }   // 0.7: a foe's ranking open
    var swamp = new Dictionary<string, List<string>> { [PanelModel.BattleOverviewFilter + "|biome"] = new List<string> { "Swamp" } };
    PanelState Page(Chapter c, string page, Action<PanelState> more = null) { var s = new PanelState { Chapter = c }; if (page != null) s.Page[c] = page; more?.Invoke(s); return s; }
    // 0.8.6: a page opened from another, as the book reaches it: the first page built and noted (PanelModel.Visited, as PanelUi.Render), then its
    // row's link followed (PanelModel.Follow, as the click): the state the opened page is built from, with its Back button
    PanelState OpenedFrom(PanelInput inp, PanelState from, string page)
    {
        var v0 = PanelModel.Build(inp, from); PanelModel.Visited(from, v0.Active, v0.Page);
        IEnumerable<Block> Each(Block b) => new[] { b }.Concat((b.Items ?? new List<Block>()).SelectMany(Each));
        PanelModel.Follow(from, PanelModel.Content(v0).SelectMany(Each).First(b => b.Kind == "fight" && b.Id != null && b.Id.EndsWith(page)).Id);
        return from;
    }
    PanelState Together(string category = null, string window = null) => Page(Chapter.Company, "together", s => { if (category != null) s.View["Company/together/category"] = category; if (window != null) s.View["Company/together/window"] = window; });
    PanelState As(string who, Chapter c, string page, Action<PanelState> more = null) { var s = Page(c, page, more); s.Player = who; return s; }
    PanelState CompareOn(PanelState s) { s.Compare = true; return s; }
    PanelState Compared(Chapter c, string page, Action<PanelState> more = null) => Page(c, page, s => { s.Window = TimeWindow.SevenDays; s.Compare = true; more?.Invoke(s); });   // 0.8: Compare on
    void Numbers(PanelState s) => s.ShowNumbers = true;   // 0.7: the About these numbers box open (the Y key)
    // 0.9.5: Meal plan opened from a row of Your combinations on Meals (its click followed, as in game: the slots filled, Back to Meals)
    PanelState FromCombo()
    {
        var s = Page(Chapter.Deeds, PanelModel.MealsPageId);
        var from = PanelModel.Build(world, s); PanelModel.Visited(s, from.Active, from.Page);
        var row = PanelModel.Content(from).First(b => b.Kind == "sources" && b.Tone == PanelModel.ComboTone).Items.First(r => r.Link != null);
        PanelModel.Follow(s, row.Link); return s;
    }
    PanelState Numbered(PanelState s) { s.ShowNumbers = true; return s; }
    PanelState Ev(PanelState s) { s.Everyone = true; return s; }   // 0.8: the Everyone chip on
    // 0.8.2: a part chosen from its legend item: that row's own target (BarFormOf, PartTarget) followed, as a press on it does in game
    Action<PanelState> FromLegend(string bar, string part) => s =>
    {
        var v = PanelModel.Build(world, Page(s.Chapter, s.PageOf(s.Chapter)));
        var b = PanelModel.Content(v).SelectMany(x => x.Kind == "filterbar" ? x.Items : new List<Block> { x }).First(x => (x.Kind == "facetbar" || x.Kind == "composition") && x.Title == bar);
        PanelModel.FollowFacet(s, PanelModel.PartTarget(b, PanelModel.BarFormOf(b.Items).Rows.First(r => r.Title == part).Part));
    };
    var longWeapons = Scenario("battle-damage-weapons", "case (0.8.6): a long evening (all damage dealt and fighting time 12 times as big, the kind rows with their weapon cells, so a pace and a change mark show and the lines add up to their row): By weapon with its most recent DPS and change marks; a weapon of several upgrade levels folded to its newest (the Iron Sword)", WeaponScenario.Longer(PanelSample.Full(now)));
    scenarios["battle-damage-weapons-open"] = "case (0.8.6): the same long evening with the Bronze Spear's skill levels open, one row each with a bar on one scale";
    scenarios["battle-damage-weapons-levels"] = "case (0.8.6 fold): the same long evening with the Iron Sword opened: its two upgrade levels in place under its folded line, the cursor of the keys on it";
    scenarios["battle-damage-weapons-bowshut"] = "case (0.9.5 the kind toggle): the same long evening with Bow shut (its row, total, DPS chip, bar and legend; no weapon lines under it), the cursor of the keys on it";
    scenarios["battle-damage-weapons-allshut"] = "case (0.9.5 the kind toggle): the same long evening with every weapon kind shut: the kind rows alone, as Everyone's By weapon, each with the opens mark";
    var longEdda = Scenario("edda-battle-damage-weapons-open", "case (0.8.6): Edda's By weapon on a long evening, one of her weapons opened to its levels and one level to its skill levels", WeaponScenario.Longer(SampleWorld.Make(now).copies["Edda"]));
    // 0.9 Armor sets (Chapters/ArmorSets.cs, PanelSampleSets.cs): opened from Crafting's heading-row chip (so its Back button shows)
    PanelState SetsFrom(PanelInput inp, bool everyone = false)
    {
        var from = Page(Chapter.Deeds, "crafting", s => s.Everyone = everyone);
        var v0 = PanelModel.Build(inp, from); PanelModel.Visited(from, v0.Active, v0.Page);
        PanelModel.Follow(from, "Deeds/" + PanelModel.SetsPageId);
        return from;
    }
    var setsYours = Scenario("deeds-crafting-sets", "case (0.9 Armor sets): Rowan's sets: the leather in full, bronze and troll leather begun, iron known and not begun, the rest waiting", SetsSample.World(now, "yours"));
    scenarios["deeds-crafting-sets-numbers"] = scenarios["deeds-crafting-sets"]; scenarios["deeds-crafting-sets-chip"] = scenarios["deeds-crafting-sets"];
    var setsLand = Scenario("deeds-crafting-sets-land", "case (0.9 Armor sets): every set of the Meadows made in full", SetsSample.World(now, "land"));
    var setsNew = Scenario("deeds-crafting-sets-new", "case (0.9 Armor sets): a new character: the Meadows' recipes known, nothing made yet", SetsSample.World(now, "new"));
    // 0.9 deed feeds (Chapters/DeedFeedModel.cs, PanelSampleFeeds.cs): each opened from its page's Feed chip (so its Back button shows)
    PanelState FeedOpen(PanelInput inp, string page, TimeWindow w, Action<PanelState> more = null, string who = null)
    {
        var from = Page(Chapter.Deeds, page, s => { s.Window = w; s.WindowPicked = true; s.Player = who; });
        var v0 = PanelModel.Build(inp, from); PanelModel.Visited(from, v0.Active, v0.Page);
        PanelModel.Follow(from, "Deeds/" + PanelModel.DeedFeedPageOf(page == PanelModel.RecentPageId ? null : page));
        more?.Invoke(from);
        return from;
    }
    var feedYours = Scenario("deeds-crafting-feed", "case (0.9 deed feeds): Rowan's crafting feed in 7 days: this session's stretch (two upgrades of the Iron Sword with the level meter, a first, gear made, the rest counted), then the morning before under its line, its first half hour said to the nearest 10 minutes", FeedSample.Yours(world));
    foreach (var (n, what) in new[] { ("deeds-crafting-feed-cards", "the Cards"), ("deeds-crafting-feed-timeline", "the Timeline"), ("deeds-crafting-feed-session", "Session: this session's stretch only, and where the earlier ones show"),
                                      ("deeds-crafting-feed-filter", "the filter on the Iron Sword: its two upgrades, the island says 1 of 5 kinds"), ("deeds-building-feed", "Building: the pieces counted by kind, a first, the materials recovered"),
                                      ("deeds-recent-feed", "the mixed feed from Recent: every deed of the evening in time order"), ("deeds-crafting-chip", "Crafting with its Feed and Sets chips in the heading row") })
        scenarios[n] = "case (0.9 deed feeds): " + what;
    var feedBusy = Scenario("deeds-recent-feed-all", "case (0.9 all-feed): the mixed feed of a busy evening, every deed family the log records: crafting to mining, taming, skill practice, what was brought in, sailing, and each fight one line that opens the Battle feed", FeedSample.Yours(world, "busy"));
    foreach (var (n, what) in new[] { ("deeds-recent-feed-all-cards", "the Cards"), ("deeds-recent-feed-all-timeline", "the Timeline"), ("deeds-recent-feed-all-filter", "the Deed filter open: every family present, Fights and Taming chosen"),
                                      ("deeds-recent-feed-all-end", "Show older pressed, scrolled to the end: sailing and what was brought in, the count line and what no minute holds, said once"),
                                      ("deeds-recent-feed-all-scrolled", "Show older pressed, part way down: skills practiced, a wolf tamed, crafting, between the fights") })
        scenarios[n] = "case (0.9 all-feed): " + what;
    var feedEmpty = Scenario("deeds-crafting-feed-empty", "case (0.9 deed feeds): no stretch recorded yet: the feed says what will show", FeedSample.Yours(world, "empty"));
    var feedUnread = Scenario("deeds-crafting-feed-notrecorded", "case (0.9 deed feeds): the book has not read the records yet (not in a world): \"Not recorded\", never 0", FeedSample.Yours(world, "unread"));
    var eddaFeed = Scenario("edda-deeds-crafting-feed", "case (0.9 deed feeds): Edda's crafting feed on All: her session of two days back from the server (an upgrade and gear made), her session of five days back on its way", FeedSample.Edda(books["Edda"], now));
    scenarios["edda-deeds-recent-feed"] = "case (0.9 deed feeds): Edda's mixed feed on All: her farming, cooking and crafting of two days back, the next session on its way";
    var eddaFeedFailed = Scenario("edda-deeds-crafting-feed-failed", "case (0.9 deed feeds): Edda's session of five days back did not arrive: Try again", FeedSample.Edda(books["Edda"], now, "failed"));
    var eddaFeedNone = Scenario("edda-deeds-crafting-feed-none", "case (0.9 deed feeds): the server lists no session of Edda's yet: the feed says when hers come", FeedSample.Edda(books["Edda"], now, null, "none"));
    var eddaFeedOld = Scenario("edda-deeds-crafting-feed-oldserver", "case (0.9 deed feeds): a server before 0.9 keeps no deed feeds: the feed says so", FeedSample.Edda(books["Edda"], now, null, "oldserver"));
    var setsEveryone = Scenario("deeds-crafting-sets-everyone", "case (0.9 Armor sets): the Everyone chip: who made each set in full; Finch's copy from 0.8.6", SetsSample.World(now, "everyone"));
    // 0.9 arrows track (Chapters/BestArrows.cs, PanelSampleArrows.cs): Rowan's Ashlands evening with the land per minute, Joost's Fallen Valkyrie tie
    var arrowsAsh = Scenario("battle-foes-whattouse", "case (0.9 What to use): Rowan's Ashlands evening: the Fallen Valkyrie (Charred Arrow 82 = Silver Arrow 82, a tie named), an Asksvin, a Charred Warrior; Grausten, Flametal and Feathers brought in there, the Bloodgold Arrow not known yet", ArrowsSample.World(now));
    var arrowsMany = Scenario("battle-foes-whattouse-many", "case (0.9 What to use): the same evening with two more foes and a third Charred kind: 9 foes, the 7 you hit most as columns", ArrowsSample.World(now, true));
    foreach (var n in new[] { "battle-foes-whattouse-weapons", "battle-foes-whattouse-know", "battle-foes-whattouse-open", "battle-foes-whattouse-lastfight", "battle-foes-tie-table" }) scenarios[n] = scenarios["battle-foes-whattouse"];
    PanelState UseView(Action<PanelState> more = null) { var s = Battle(TimeWindow.Session, "foes"); s.View["Battle/foes/view"] = PanelModel.UseView; more?.Invoke(s); return s; }
    // 0.9 crafting-history (Chapters/CraftingHistory.cs, Chapters/DeedHistory.cs, PanelSampleCrafting.cs): Crafting's Upgrades per item, the one-kind
    // By kind bar (Joost's in-game fault), every deed's History opened from its hero's growth line, one day of it, and History with no day history
    PanelState HistoryFrom(PanelInput inp, string deed, Action<PanelState> more = null)
    {
        var from = Page(Chapter.Deeds, deed);
        var v0 = PanelModel.Build(inp, from); PanelModel.Visited(from, v0.Active, v0.Page);
        PanelModel.Follow(from, "Deeds/" + PanelModel.HistoryPageOf(deed));
        more?.Invoke(from);
        return from;
    }
    PanelState DayFrom(PanelInput inp, string deed, DateTime day)
    {
        var from = HistoryFrom(inp, deed);
        var v0 = PanelModel.Build(inp, from); PanelModel.Visited(from, v0.Active, v0.Page);
        PanelModel.Follow(from, "Deeds/" + PanelModel.DayPageOf(deed, day));
        return from;
    }
    var upgradesS = Scenario("deeds-crafting-upgrades", "case (0.9 crafting-history): upgrades recorded from 2 Oct: the Iron Sword to level 3 of 4, the Bronze Axe and the Wood Shield; seven more before", CraftHistorySample.World(now, "upgrades"));
    var upgradesEmptyS = Scenario("deeds-crafting-upgrades-empty", "case (0.9 crafting-history): upgrades are recorded from an hour ago, none yet: the island's empty state beside the game's eleven", CraftHistorySample.World(now, "upgrades-empty"));
    var oneKindS = Scenario("deeds-crafting-onekind", "case (0.9 fault, Joost in game 18 Oct): a new character, six pieces of gear all armor, eleven upgrades: the By kind bar names its one kind", CraftHistorySample.World(now, "onekind"));
    var historyS = Scenario("deeds-crafting-history", "case (0.9 History): a month of days (from 3 Sep), the firsts as ticks, the upgrades; opened from the hero's growth line", CraftHistorySample.World(now, "history"));
    foreach (var deed in PanelModel.HistoryPages) if (deed != "crafting") scenarios["deeds-" + deed + "-history"] = scenarios["deeds-crafting-history"];
    scenarios["deeds-crafting-history-upgrades"] = scenarios["deeds-crafting-history"]; scenarios["deeds-crafting-day"] = "case (0.9 History): a History column clicked: Crafting on Tue 6 Oct"; scenarios["deeds-crafting-history-firsts"] = "case (0.9 History): Crafting history scrolled to its Firsts island";
    var noHistoryS = CraftHistorySample.World(now, "upgrades"); noHistoryS.History = null;
    Scenario("deeds-crafting-history-none", "case (0.9 History): the day history not loaded yet: History says it is not recorded, never a 0", noHistoryS);
    // 0.9.5 draft Damage history (Chapters/DamageHistory.cs, PanelSampleDamage.cs): Battle > Damage's History, opened from By weapon's growth line
    var dmgMonth = Scenario("battle-damage-history-30days", "case (0.9.5 draft Damage history): thirty days of day history, fighting time from twelve days back, a staff picked up in the last ten days; opened from By weapon's growth line", DamageHistorySample.World(now, "month"));
    scenarios["battle-damage-history-bow"] = "case (0.9.5 draft Damage history): the same month, Bow chosen: its damage per day and its DPS per day on the same columns, none before fighting time was recorded";
    var dmgBooks = FullDump.Books(dmgMonth).ToDictionary(b => b.who, b => b.book);
    var dmgEdda = Scenario("edda-battle-damage-history", "case (0.9.5 draft Damage history): Edda's copy on Rowan's PC: thirty shared days of damage by kind, her weapons' seconds for the last fourteen", dmgBooks["Edda"]);
    PanelState DmgHistoryFrom(PanelInput inp, string who = null, Action<PanelState> more = null)
    {
        var from = Battle(TimeWindow.SevenDays, "damage"); from.WindowPicked = true; from.Player = who;
        var v0 = PanelModel.Build(inp, from); PanelModel.Visited(from, v0.Active, v0.Page);
        PanelModel.Follow(from, "Battle/" + PanelModel.DamageHistoryPage);
        more?.Invoke(from);
        return from;
    }
    var shots = new (string name, PanelInput inp, PanelState st, bool sharing)[]
    {
        ("deeds-crafting-sets", setsYours, SetsFrom(setsYours), true),
        ("deeds-crafting-sets-numbers", setsYours, Numbered(SetsFrom(setsYours)), true),
        ("deeds-crafting-sets-chip", setsYours, Page(Chapter.Deeds, "crafting"), true),   // Crafting with its Armor sets chip in the heading row
        ("deeds-crafting-sets-land", setsLand, SetsFrom(setsLand), true),
        ("deeds-crafting-sets-new", setsNew, SetsFrom(setsNew), true),
        ("deeds-crafting-sets-everyone", setsEveryone, SetsFrom(setsEveryone, true), true),
        // 0.9 arrows track, What to use (Chapters/WhatToUse.cs): the arrows (You can make), the weapons, You know, many foes, a foe's column open, Last fight;
        // and the Best arrow tie in By foe's table
        ("battle-foes-whattouse", arrowsAsh, UseView(), true),
        ("battle-foes-whattouse-weapons", arrowsAsh, UseView(s => s.View["Battle/foes/usetab"] = "weapon"), true),
        ("battle-foes-whattouse-know", arrowsAsh, UseView(s => s.View["Battle/foes/useset"] = PanelModel.UseKnown), true),
        ("battle-foes-whattouse-many", arrowsMany, UseView(s => s.View["Battle/foes/useset"] = PanelModel.UseAll), true),
        ("battle-foes-whattouse-open", arrowsAsh, UseView(s => s.View["Battle/foes/useopen"] = ArrowsSample.Volture), true),
        ("battle-foes-whattouse-lastfight", arrowsAsh, Battle(TimeWindow.Session, PanelModel.LastFightPage), true),
        ("battle-foes-tie-table", arrowsAsh, Battle(TimeWindow.Session, "foes"), true),   // By foe's table: the Fallen Valkyrie's Best arrow, "Charred or Silver Arrow"
        ("deeds-crafting-upgrades", upgradesS, Page(Chapter.Deeds, "crafting"), true),
        ("deeds-crafting-upgrades-empty", upgradesEmptyS, Page(Chapter.Deeds, "crafting"), true),
        ("deeds-crafting-onekind", oneKindS, Page(Chapter.Deeds, "crafting"), true),
        ("deeds-crafting-history", historyS, HistoryFrom(historyS, "crafting"), true),
        ("deeds-crafting-history-upgrades", historyS, HistoryFrom(historyS, "crafting", s => s.View["Deeds/" + PanelModel.HistoryPageOf("crafting") + "/metric"] = "upgrades"), true),
        ("deeds-woodcutting-history", historyS, HistoryFrom(historyS, "woodcutting"), true),
        ("deeds-mining-history", historyS, HistoryFrom(historyS, "mining"), true),
        ("deeds-cooking-history", historyS, HistoryFrom(historyS, "cooking"), true),
        ("deeds-building-history", historyS, HistoryFrom(historyS, "building"), true),
        ("deeds-groundwork-history", historyS, HistoryFrom(historyS, "groundwork"), true),
        ("deeds-farming-history", historyS, HistoryFrom(historyS, "farming"), true),
        ("deeds-fishing-history", historyS, HistoryFrom(historyS, "fishing"), true),
        ("deeds-crafting-day", historyS, DayFrom(historyS, "crafting", new DateTime(2026, 10, 6)), true),
        ("deeds-crafting-history-firsts", historyS, HistoryFrom(historyS, "crafting"), true),
        ("deeds-crafting-history-none", noHistoryS, HistoryFrom(noHistoryS, "crafting"), true),
        ("battle-damage-history-7days", world, DmgHistoryFrom(world), true),   // 0.9.5 draft Damage history: the sample's own week of days
        ("battle-damage-history-30days", dmgMonth, DmgHistoryFrom(dmgMonth), true),
        ("battle-damage-history-bow", dmgMonth, DmgHistoryFrom(dmgMonth, null, s => s.View["Battle/" + PanelModel.DamageHistoryPage + "/kind"] = "bow"), true),
        ("edda-battle-damage-history", dmgEdda, DmgHistoryFrom(dmgEdda, "Edda"), true),
        ("deeds-crafting-feed", feedYours, FeedOpen(feedYours, "crafting", TimeWindow.SevenDays), true),
        ("deeds-crafting-feed-cards", feedYours, FeedOpen(feedYours, "crafting", TimeWindow.SevenDays, s => s.View["Deeds/feed-crafting/view"] = PanelModel.FeedCards), true),
        ("deeds-crafting-feed-timeline", feedYours, FeedOpen(feedYours, "crafting", TimeWindow.SevenDays, s => s.View["Deeds/feed-crafting/view"] = PanelModel.FeedTimeline), true),
        ("deeds-crafting-feed-session", feedYours, FeedOpen(feedYours, "crafting", TimeWindow.Session), true),
        ("deeds-crafting-feed-filter", feedYours, FeedOpen(feedYours, "crafting", TimeWindow.SevenDays, s => { PanelModel.ToggleFacet(s, "Deeds/feed-crafting", "kind", "$item_sword_iron"); s.OpenFilters.Add("Deeds/feed-crafting"); }), true),
        ("deeds-building-feed", feedYours, FeedOpen(feedYours, "building", TimeWindow.SevenDays), true),
        ("deeds-recent-feed", feedYours, FeedOpen(feedYours, PanelModel.RecentPageId, TimeWindow.SevenDays), true),
        ("deeds-recent-feed-all", feedBusy, FeedOpen(feedBusy, PanelModel.RecentPageId, TimeWindow.Session), true),
        ("deeds-recent-feed-all-cards", feedBusy, FeedOpen(feedBusy, PanelModel.RecentPageId, TimeWindow.Session, s => s.View["Deeds/feed-all/view"] = PanelModel.FeedCards), true),
        ("deeds-recent-feed-all-timeline", feedBusy, FeedOpen(feedBusy, PanelModel.RecentPageId, TimeWindow.Session, s => s.View["Deeds/feed-all/view"] = PanelModel.FeedTimeline), true),
        ("deeds-recent-feed-all-filter", feedBusy, FeedOpen(feedBusy, PanelModel.RecentPageId, TimeWindow.Session, s => { PanelModel.ToggleFacet(s, "Deeds/feed-all", "deed", PanelModel.DeedFeedFights); PanelModel.ToggleFacet(s, "Deeds/feed-all", "deed", DeedStretches.Taming); s.OpenFilters.Add("Deeds/feed-all"); }), true),
        ("deeds-recent-feed-all-end", feedBusy, FeedOpen(feedBusy, PanelModel.RecentPageId, TimeWindow.Session, s => s.FeedMore = 1), true),
        ("deeds-recent-feed-all-scrolled", feedBusy, FeedOpen(feedBusy, PanelModel.RecentPageId, TimeWindow.Session, s => s.FeedMore = 1), true),
        ("deeds-crafting-chip", feedYours, Page(Chapter.Deeds, "crafting"), true),
        ("deeds-crafting-feed-empty", feedEmpty, FeedOpen(feedEmpty, "crafting", TimeWindow.Session), true),
        ("deeds-crafting-feed-notrecorded", feedUnread, FeedOpen(feedUnread, "crafting", TimeWindow.Session), true),
        ("edda-deeds-crafting-feed", eddaFeed, FeedOpen(eddaFeed, "crafting", TimeWindow.SinceInstall, null, "Edda"), true),
        ("edda-deeds-recent-feed", eddaFeed, FeedOpen(eddaFeed, PanelModel.RecentPageId, TimeWindow.SinceInstall, null, "Edda"), true),
        ("edda-deeds-crafting-feed-failed", eddaFeedFailed, FeedOpen(eddaFeedFailed, "crafting", TimeWindow.SinceInstall, null, "Edda"), true),
        ("edda-deeds-crafting-feed-none", eddaFeedNone, FeedOpen(eddaFeedNone, "crafting", TimeWindow.SinceInstall, null, "Edda"), true),
        ("edda-deeds-crafting-feed-oldserver", eddaFeedOld, FeedOpen(eddaFeedOld, "crafting", TimeWindow.SinceInstall, null, "Edda"), true),
        ("battle-10min", world, Battle(TimeWindow.LastTenMinutes), true),
        ("battle-30min", world, Battle(TimeWindow.LastThirtyMinutes), true),
        ("battle-all", world, Battle(TimeWindow.SinceInstall), true),
        ("battle-all-swamp", world, Battle(TimeWindow.SinceInstall, null, swamp), true),
        ("battle-damage-10min", world, Battle(TimeWindow.LastTenMinutes, "damage"), true),
        ("battle-damage-swamp", world, Page(Chapter.Battle, "damage", s => { PanelModel.ToggleFacet(s, PanelModel.BattleDamageFilter, "biome", "Swamp"); s.OpenFilters.Add(PanelModel.BattleDamageFilter); }), true),
        ("battle-deaths-swamp", world, Page(Chapter.Battle, "deaths", s => { PanelModel.ToggleFacet(s, PanelModel.BattleDeathsFilter, "biome", "Swamp"); s.OpenFilters.Add(PanelModel.BattleDeathsFilter); }), true),
        ("battle-damage-all", world, Battle(TimeWindow.SinceInstall, "damage"), true),
        ("battle-deaths-all", world, Battle(TimeWindow.SinceInstall, "deaths"), true),
        ("deeds", world, Page(Chapter.Deeds, null), true),
        // 0.7 Deeds > Recent (Chapters/RecentPage.cs): You in Session (the default window) and 10 min, 7 days; The group (0.8: the Everyone chip on)
        // in Session and 10 min (fellows greyed with their line); a fellow's book (Session only); a session with nothing yet (scenario)
        ("deeds-recent", world, Page(Chapter.Deeds, "recent"), true),
        ("deeds-recent-10min", world, Page(Chapter.Deeds, "recent", s => s.Window = TimeWindow.LastTenMinutes), true),
        ("deeds-recent-7days", world, Page(Chapter.Deeds, "recent", s => s.Window = TimeWindow.SevenDays), true),
        ("deeds-recent-group", world, Ev(Page(Chapter.Deeds, "recent")), true),
        ("deeds-recent-group-10min", world, Ev(Page(Chapter.Deeds, "recent", s => s.Window = TimeWindow.LastTenMinutes)), true),
        ("deeds-recent-group-three", threeS, Ev(Page(Chapter.Deeds, "recent")), true),   // 0.8.1 review 5: three players, the third a half island
        // 0.9.1: Yesterday and 3 days (Joost: "I want to know what happened yesterday"): yours, the group's with a friend on an older version and one with nothing that day
        ("deeds-recent-yesterday", world, Page(Chapter.Deeds, "recent", s => s.Window = TimeWindow.Yesterday), true),
        ("deeds-recent-group-7days", world, Ev(Page(Chapter.Deeds, "recent", s => s.Window = TimeWindow.SevenDays)), true),   // the screenshot's window: every friend's days
        ("deeds-recent-group-yesterday", groupDaysS, Ev(Page(Chapter.Deeds, "recent", s => s.Window = TimeWindow.Yesterday)), true),
        ("deeds-recent-group-3days", groupDaysS, Ev(Page(Chapter.Deeds, "recent", s => s.Window = TimeWindow.ThreeDays)), true),
        ("edda-deeds-recent-yesterday", books["Edda"], As("Edda", Chapter.Deeds, "recent", s => s.Window = TimeWindow.Yesterday), true),
        ("deeds-recent-numbers", world, Page(Chapter.Deeds, "recent", Numbers), true),
        ("deeds-recent-empty", recentEmptyS, Page(Chapter.Deeds, "recent"), true),
        ("tor-recent", books["Tor"], As("Tor", Chapter.Deeds, "recent"), true),
        ("deeds-cooking", world, Page(Chapter.Deeds, "cooking"), true),
        // 0.8.1 Deeds > Meals (Chapters/MealsModel.cs): what you ate, one damage row per dish; the group's By dish (split by who cooked it) and By player
        ("deeds-meals", world, Page(Chapter.Deeds, "meals"), true),
        ("deeds-meals-7days", world, Page(Chapter.Deeds, "meals", s => { s.Window = TimeWindow.SevenDays; s.WindowPicked = true; }), true),
        ("deeds-meals-numbers", world, Page(Chapter.Deeds, "meals", Numbers), true),
        ("deeds-meals-empty", fresh, Page(Chapter.Deeds, "meals"), true),
        ("deeds-meals-combos", world, Page(Chapter.Deeds, "meals"), true),   // 0.9: scrolled to Your combinations (PANEL_SCROLL)
        ("deeds-meals-combos-7days", world, Page(Chapter.Deeds, "meals", s => { s.Window = TimeWindow.SevenDays; s.WindowPicked = true; }), true),
        ("deeds-meals-combos-empty", combosEmptyS, Page(Chapter.Deeds, "meals"), true),
        ("deeds-meals-combos-notrecorded", combosNoneS, Page(Chapter.Deeds, "meals"), true),
        ("deeds-mealplan", world, Page(Chapter.Deeds, PanelModel.MealPlanPageId), true),   // 0.9.5: Meal plan, its own page (Chapters/MealPlanModel.cs)
        ("deeds-mealplan-ash", mealsAshS, Page(Chapter.Deeds, PanelModel.MealPlanPageId), true),
        ("deeds-mealplan-eitr", mealsAshS, Page(Chapter.Deeds, PanelModel.MealPlanPageId, s => s.View[PanelModel.MealPlanBestKey] = "eitr"), true),
        ("deeds-mealplan-three", world, Page(Chapter.Deeds, PanelModel.MealPlanPageId, s => s.View[PanelModel.MealPlanKey] = "WolfMeatSkewer,Eyescream,FeastMountains"), true),
        ("deeds-mealplan-three-scrolled", world, Page(Chapter.Deeds, PanelModel.MealPlanPageId, s => s.View[PanelModel.MealPlanKey] = "WolfMeatSkewer,Eyescream,FeastMountains"), true),   // scrolled 260 px down (PANEL_SCROLL): the meal stays pinned
        ("deeds-mealplan-combo", world, FromCombo(), true),
        ("tor-meals", books["Tor"], As("Tor", Chapter.Deeds, "meals"), true),
        ("everyone-meals", world, Ev(Page(Chapter.Deeds, "meals")), true),
        ("everyone-meals-player", world, Ev(Page(Chapter.Deeds, "meals", s => s.View["Deeds/meals/group"] = PanelModel.MealsPlayerView)), true),
        ("everyone-meals-7days", world, Ev(Page(Chapter.Deeds, "meals", s => { s.Window = TimeWindow.SevenDays; s.WindowPicked = true; })), true),   // 0.8.5: each player's meals of their days, cooks split
        ("everyone-meals-outside", mealsOutsideS, Ev(Page(Chapter.Deeds, "meals")), true),
        ("deeds-cooking-open", world, Page(Chapter.Deeds, "cooking", s => s.OpenFilters.Add(PanelModel.CookFilter)), true),
        ("deeds-cooking-stamina", world, Page(Chapter.Deeds, "cooking", s => s.Facets[PanelModel.CookFilter + "|boost"] = new List<string> { "stamina" }), true),
        ("deeds-cooking-meals", world, Page(Chapter.Deeds, "cooking", FromLegend("By type", "Meals")), true),   // 0.8.2: Meals chosen from By type's legend item
        ("deeds-cooking-bykind", world, Page(Chapter.Deeds, "cooking", FromLegend(PanelModel.DishesByKind, "Carrot Soup")), true),   // 0.8.2: one dish chosen from Dishes by kind
        ("deeds-young", youngS, Page(Chapter.Deeds, null), true),
        ("deeds-unsung", youngS, Page(Chapter.Deeds, null, s => s.View["Deeds/overview/view"] = "unsung"), true),
        ("company", world, Page(Chapter.Company, null), true),
        ("company-together", world, Together(), true),
        ("company-together-cargo", world, Together("cargo"), true),
        ("company-food", world, Page(Chapter.Company, "food"), true),
        ("company-gear", world, Page(Chapter.Company, "gear"), true),
        ("company-together-dealt", world, Together("dealt"), true),
        ("company-together-dealt-10min", world, Together("dealt", "LastTenMinutes"), true),
        ("company-together-dealt-hour", world, Together("dealt", "LastHour"), true),
        ("company-together-dealt-3h", world, Together("dealt", "LastThreeHours"), true),
        ("company-together-dealt-session", world, Together("dealt", "Session"), true),
        ("company-away-3days", away3S, Page(Chapter.Company, PanelModel.AwayPage), true),
        ("company-away-3days-hover", away3S, Page(Chapter.Company, PanelModel.AwayPage), true),   // 0.8 hover (PANEL_HOVER): Finch's wood lights its entry and its total above
        ("company-away-30h", away30S, Page(Chapter.Company, PanelModel.AwayPage), true),
        ("company-away-first", awayFirstS, Page(Chapter.Company, PanelModel.AwayPage), true),
        ("company-away-3days-numbers", away3S, Page(Chapter.Company, PanelModel.AwayPage, Numbers), true),   // 0.8.5: About these numbers open, under the totals (its button in their head line)
        ("company-away-5", away5S, Page(Chapter.Company, PanelModel.AwayPage), true),
        ("company-away-6", away6S, Page(Chapter.Company, PanelModel.AwayPage), true),
        ("company-together-alone", world, Together(), false),
        ("company-food-alone", world, Page(Chapter.Company, "food"), false),
        ("hall", world, Page(Chapter.Stores, null), true),
        ("hall-trader", world, Page(Chapter.Stores, "trader"), true),
        ("hall-smelters", world, Page(Chapter.Stores, "smelters"), true),
        ("hall-smelters-stoker", stokerS, Page(Chapter.Stores, "smelters"), true),
        ("feats-titles-stoker", stokerS, Page(Chapter.Feats, "titles", s => s.FeatSel = "smith"), true),   // Forgekeeper chosen: its smelter line says "by hand"
        ("battle", world, Battle(TimeWindow.Session), true),
        ("battle-swamp", world, Battle(TimeWindow.Session, null, swamp), true),
        ("battle-focus", world, new PanelState { Chapter = Chapter.Battle, FilterRow = 0, FilterCursor = 1, Facets = { [PanelModel.BattleOverviewFilter + "|biome"] = new List<string> { "Swamp" } } }, true),
        ("battle-deaths", world, Battle(TimeWindow.Session, "deaths"), true),
        ("battle-late", lateS, Battle(TimeWindow.Session), true),   // 0.8.5: the overview fits one screen with nine lands found
        ("battle-late-all", lateS, Battle(TimeWindow.SinceInstall), true),
        ("battle-late-7days", lateS, Battle(TimeWindow.SevenDays), true),
        ("battle-deaths-full", deathsFullS, Battle(TimeWindow.Session, "deaths"), true),   // 0.8.5: Where you fell, the tiles grow
        ("battle-deaths-many", deathsManyS, Battle(TimeWindow.SinceInstall, "deaths"), true),
        ("battle-damage", world, Battle(TimeWindow.Session, "damage"), true),
        ("battle-damage-hover", world, Battle(TimeWindow.Session, "damage"), true),   // 0.8 hover (PANEL_HOVER): the pointer on Melee's Lightning sliver
        ("battle-damage-type", world, Page(Chapter.Battle, "damage", s => s.View["Battle/damage/view"] = "type"), true),   // By weapon is the default (battle-damage)
        ("battle-foes", world, Battle(TimeWindow.Session, "foes"), true),
        ("battle-foes-type", world, Page(Chapter.Battle, "foes", s => s.View["Battle/foes/view"] = "type"), true),
        ("battle-foes-open", world, FoesOpen("Draugr"), true),   // 0.7: a foe's ranking open under its row
        ("battle-foes-open-hover", world, FoesOpen("Draugr"), true),   // 0.8.2 (Joost: the hover on every part): the pointer on Frost Arrow's Frost part, wide enough for its 52, its tag "Frost 52"
        // 0.8 (Chapters/BattleFeedModel.cs): Damage by foe, the battle feed in its three views, the last fight, a fellow's feed (0.8.2: shared)
        ("battle-damage-foe", world, Page(Chapter.Battle, "damage", s => s.View["Battle/damage/view"] = "foe"), true),
        ("battle-damage-foe-all", world, Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.SinceInstall; s.View["Battle/damage/view"] = "foe"; }), true),
        ("battle-damage-foe-hover", world, Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.SinceInstall; s.View["Battle/damage/view"] = "foe"; }), true),   // 0.8 damage rows (PANEL_HOVER): the pointer on Greydwarf's Frost sliver, too narrow for its number: the number in its share's place
        // 0.8.5 (Chapters/AfterArmourModel.cs): your damage before and after the foe's armor; PANEL_HOVER: the pointer on Draugr's Fire (resisted: half
        // stopped, its words on the tag), and on Foes the pointer on Draugr's Dealt bar with Draugr open (Your hits on it)
        ("battle-damage-afterarmor", world, Page(Chapter.Battle, "damage", s => s.View["Battle/damage/view"] = "foe"), true),
        ("battle-foes-afterarmor", world, FoesOpen("Draugr"), true),
        ("battle-feed", world, Battle(TimeWindow.Session, PanelModel.FeedPage), true),
        ("battle-feed-cards", world, Page(Chapter.Battle, PanelModel.FeedPage, s => s.View[PanelModel.FeedViewKey] = PanelModel.FeedCards), true),
        ("battle-feed-cards-less", feedLessS, Page(Chapter.Battle, PanelModel.FeedPage, s => s.View[PanelModel.FeedViewKey] = PanelModel.FeedCards), true),   // 0.8.1: "blunt <1" stays on one line
        ("battle-feed-timeline", world, Page(Chapter.Battle, PanelModel.FeedPage, s => s.View[PanelModel.FeedViewKey] = PanelModel.FeedTimeline), true),
        ("battle-feed-empty", emptyS, Page(Chapter.Battle, PanelModel.FeedPage), true),
        // 0.9 fight pages (Chapters/FightPages.cs): A the fight (live, a boss row open, the group's, an attempt), C Runs and bosses, D a run
        ("battle-bossfight", fightsLiveS, Page(Chapter.Battle, PanelModel.LastFightPage), true),
        ("battle-bossfight-open", fightsLiveS, Page(Chapter.Battle, PanelModel.LastFightPage, s => s.View["Battle/" + PanelModel.LastFightPage + "/open"] = "SeekerQueen"), true),
        ("battle-bossfight-numbers", fightsLiveS, Page(Chapter.Battle, PanelModel.LastFightPage, Numbers), true),
        ("battle-bossfight-stacked", fightsLiveS, Page(Chapter.Battle, PanelModel.LastFightPage, s => s.View[PanelModel.FightLayoutKey] = PanelModel.FightStacked), true),   // the other layout, for Joost to pick
        ("battle-lastfight-group", fightsAfterS, Page(Chapter.Battle, PanelModel.LastFightPage), true),
        ("battle-lastfight-server", lastServerS, Page(Chapter.Battle, PanelModel.LastFightPage), true),   // 0.8.6: your Last fight with the server line
        ("battle-fight-server", lastServerS, Page(Chapter.Battle, "fight:server-fight"), true),   // 0.8.6: that fight's own page, "You were not in this fight"
        ("battle-damage-byfoe", kinS, Page(Chapter.Battle, "damage", s => s.View["Battle/damage/view"] = "foe"), true),   // 0.8.5: Draugr and the ranged Draugr, one row
        ("battle-bossfight-low", fightsLowS, Page(Chapter.Battle, PanelModel.LastFightPage), true),   // 0.8.6: the lane with little left: the time at this pace after its note
        ("battle-bossfight-brenna", brennaS, Page(Chapter.Battle, PanelModel.LastFightPage), true),
        ("battle-lastfight-weak-reto", weakRetoS, Page(Chapter.Battle, PanelModel.LastFightPage), true),   // 0.9.4 draft: Joost's fight, sample cast
        ("battle-lastfight-weak-boss", weakBossS, Page(Chapter.Battle, PanelModel.LastFightPage), true),   // 0.9.4 draft: the three forms
        ("battle-lastfight-weak-twokinds", weakTwoS, Page(Chapter.Battle, PanelModel.LastFightPage), true),   // 0.9.4 draft: a line per kind
        ("edda-battle-lastfight-weak-none", weakNoneS, As("Edda", Chapter.Battle, PanelModel.LastFightPage), true),   // 0.9.4 draft: no modifiers, nothing shown   // 0.8.5: Hildir's miniboss, a boss fight
        ("battle-lastfight-earlier", lastEarlierS, Page(Chapter.Battle, PanelModel.LastFightPage), true),   // 0.8.5: Last fight from an earlier session, "2 hours ago"
        ("battle-feed-earlier", feedEarlierS, Page(Chapter.Battle, PanelModel.FeedPage, s => { s.Window = TimeWindow.SevenDays; s.WindowPicked = true; }), true),   // 0.8.5: the feed over a restart
        ("battle-runs", fightsLiveS, Page(Chapter.Battle, PanelModel.RunsPage), true),
        ("battle-runs-saved", savedRunsS, Page(Chapter.Battle, PanelModel.RunsPage), true),   // 0.8.6: named from the place, "Left, 1 death", the 34 s look not listed
        // 0.8.6 (Joost: "how does one filter the runs and bosses by time?"): the window chips on Runs and bosses: Today (the run on now, its Queen under it),
        // and 30 min after the fights (nothing in it: one line names the newest)
        ("battle-runs-today", fightsLiveS, Page(Chapter.Battle, PanelModel.RunsPage, s => { s.Window = TimeWindow.Today; s.WindowPicked = true; }), true),
        ("battle-runs-30min", fightsAfterS, Page(Chapter.Battle, PanelModel.RunsPage, s => { s.Window = TimeWindow.LastThirtyMinutes; s.WindowPicked = true; }), true),
        ("battle-fight-bonemass", fightsLiveS, Page(Chapter.Battle, "fight:bonemass-1"), true),
        ("battle-fight-queen-reset", fightsLiveS, Page(Chapter.Battle, "fight:queen-2"), true),
        ("battle-run-mine", fightsLiveS, Page(Chapter.Battle, "fight:mine-1"), true),
        // 0.8.6 (Joost: "How does one get back to the previous view?"): a run and a boss fight opened from Runs and bosses, the Back button at the strip's left end
        ("battle-run-opened", fightsLiveS, OpenedFrom(fightsLiveS, Page(Chapter.Battle, PanelModel.RunsPage), "fight:mine-1"), true),
        ("battle-fight-opened", fightsLiveS, OpenedFrom(fightsLiveS, Page(Chapter.Battle, PanelModel.RunsPage), "fight:bonemass-1"), true),
        // 0.8.1 (Joost: "Battle feed, why can't I filter this?"): the window chips and the Biome and Foe rows; Foe = Troll open, so the fight of five
        // shows its Troll only ("1 of 5 foes"), and the last hour in Cards
        ("battle-feed-foe-troll", world, Page(Chapter.Battle, PanelModel.FeedPage, s => { s.Facets[PanelModel.FeedFilter + "|foe"] = new List<string> { "Troll" }; s.OpenFilters.Add(PanelModel.FeedFilter); }), true),
        ("battle-feed-1h", world, Page(Chapter.Battle, PanelModel.FeedPage, s => { s.Window = TimeWindow.LastHour; s.WindowPicked = true; s.View[PanelModel.FeedViewKey] = PanelModel.FeedCards; }), true),
        ("battle-lastfight", world, Battle(TimeWindow.Session, PanelModel.LastFightPage), true),
        ("battle-damage-foe-long", longFoeS, Page(Chapter.Battle, "damage", s => s.View["Battle/damage/view"] = "foe"), true),   // REVIEW-081 #7: "Greydwarf Shaman ×3" whole
        ("battle-lastfight-long", longFoeS, Battle(TimeWindow.Session, PanelModel.LastFightPage), true),
        ("battle-feed-long", longFoeS, Battle(TimeWindow.Session, PanelModel.FeedPage), true),   // 0.8.1 the Log's damage bars: the last fight with five foes, three of them Greydwarf Shamans
        ("battle-feed-100", feed100S, Battle(TimeWindow.Session, PanelModel.FeedPage), true),   // 0.8.2: a long feed, the page's one hover tag over 40 bars (HoverCostTests)
        ("battle-feed-100-end", feed100S, Battle(TimeWindow.Session, PanelModel.FeedPage), true),   // 0.8.5: its end, Show older foes
        ("battle-feed-100-scrolled", feed100S, Battle(TimeWindow.Session, PanelModel.FeedPage), true),   // 0.8.6: part way down, the strip pinned over the fights
        ("battle-feed-100-more", feed100S, Page(Chapter.Battle, PanelModel.FeedPage, s => { s.Window = TimeWindow.Session; s.FeedMore = 1; }), true),   // 0.8.5: Show older foes pressed once
        ("battle-lastfight-numbers", world, Numbered(Battle(TimeWindow.Session, PanelModel.LastFightPage)), true),
        ("tor-battle-feed", books["Tor"], As("Tor", Chapter.Battle, PanelModel.FeedPage), true),
        // 0.8.2 (Joost on a fellow's book: "why no feed? I want that in", "same for last fight"): Edda's feed and last fight as her copy shares them,
        // Rowan as "Rowan (you)", Ylva (who does not share) as another player
        ("edda-battle-feed", books["Edda"], As("Edda", Chapter.Battle, PanelModel.FeedPage), true),
        ("edda-battle-feed-cards", books["Edda"], As("Edda", Chapter.Battle, PanelModel.FeedPage, s => s.View[PanelModel.FeedViewKey] = PanelModel.FeedCards), true),
        ("edda-battle-lastfight", books["Edda"], As("Edda", Chapter.Battle, PanelModel.LastFightPage), true),
        ("edda-battle-lastfight-fight", eddaFightS, As("Edda", Chapter.Battle, PanelModel.LastFightPage), true),   // 0.8.6: her Last fight on the fight page, from your PC's joined fights
        ("battle-feed-fellow-older", eddaOlderS, As("Edda", Chapter.Battle, PanelModel.FeedPage, s => { s.Window = TimeWindow.SinceInstall; s.WindowPicked = true; s.View[PanelModel.FeedViewKey] = PanelModel.FeedCards; }), true),   // 0.8.6: her sessions from the server, at the end of the page (Cards: the Log cuts "defeated with Rowan (you)" at its outcome column, as in 0.8.5)
        ("battle-feed-fellow-fetching", eddaFetchingS, As("Edda", Chapter.Battle, PanelModel.FeedPage, s => { s.Window = TimeWindow.SinceInstall; s.WindowPicked = true; }), true),   // 0.8.6: a session on its way, counting
        ("battle-foes-keys", world, Page(Chapter.Battle, "foes", s => s.View[PanelModel.FoeCursorKey] = "Troll"), true),   // 0.8: the keys' cursor on Troll: its focus ring, every factor shown
        ("battle-defense", world, Battle(TimeWindow.Session, "defense"), true),
        // the day history (HISTORY-06.md): the one window set on the pages Joost asked for
        ("battle-defense-7days", world, Battle(TimeWindow.SevenDays, "defense"), true),
        ("battle-defense-today", world, Battle(TimeWindow.Today, "defense"), true),
        ("battle-defense-1h", world, Battle(TimeWindow.LastHour, "defense"), true),
        ("battle-defense-30min", world, Battle(TimeWindow.LastThirtyMinutes, "defense"), true),
        ("battle-defense-30min-new", newHistoryS, Battle(TimeWindow.LastThirtyMinutes, "defense"), true),
        ("battle-defense-7days-new", newHistoryS, Battle(TimeWindow.SevenDays, "defense"), true),   // the greyed 7 days pressed: All, and the one line
        ("deeds-woodcutting-today", world, Page(Chapter.Deeds, "woodcutting", s => s.Window = TimeWindow.Today), true),
        ("battle-defense-all", world, Battle(TimeWindow.SinceInstall, "defense"), true),
        // 0.7: Defence's Your armour view (ArmourModel.cs): the session, the short window (no foes), a day window from before the ledger began, All
        ("battle-defense-armour", world, Armour(TimeWindow.Session), true),
        ("battle-defense-joost", joostDefS, Battle(TimeWindow.Session, "defense"), true),   // B25-B27: every source with a picture, no list's air under a one-type foe
        ("battle-defense-joost-armour", joostDefS, Armour(TimeWindow.Session), true),        // B27: the empty view says why and when it fills
        ("battle-defense-armour-10min", world, Armour(TimeWindow.LastTenMinutes), true),
        ("battle-defense-armour-7days", world, Armour(TimeWindow.SevenDays), true),
        ("battle-defense-armour-all", world, Armour(TimeWindow.SinceInstall), true),
        ("battle-deaths-7days", world, Battle(TimeWindow.SevenDays, "deaths"), true),
        ("battle-foes-7days", world, Battle(TimeWindow.SevenDays, "foes"), true),
        ("battle-foes-1h", world, Battle(TimeWindow.LastHour, "foes"), true),
        ("battle-7days", world, Battle(TimeWindow.SevenDays), true),
        ("battle-damage-yesterday", world, Battle(TimeWindow.Yesterday, "damage"), true),   // 0.9.1: the new chips on Battle
        ("battle-3days", world, Battle(TimeWindow.ThreeDays), true),
        ("battle-damage-7days", world, Battle(TimeWindow.SevenDays, "damage"), true),
        // 0.8.6 (DPS per named weapon): your By weapon with its weapons and upgrade levels under each kind (7 days has the seconds for a pace; Session the live evening), the About box
        // open, and a friend's By weapon (Edda, from the weapons her copy shares) in 7 days and Session
        ("battle-damage-weapons-numbers", world, Numbered(Battle(TimeWindow.SevenDays, "damage")), true),
        ("battle-damage-weapons-10min", world, Battle(TimeWindow.LastTenMinutes, "damage"), true),
        ("edda-battle-damage-7days", books["Edda"], As("Edda", Chapter.Battle, "damage", s => { s.Window = TimeWindow.SevenDays; s.WindowPicked = true; }), true),
        ("edda-battle-damage", books["Edda"], As("Edda", Chapter.Battle, "damage", s => { s.Window = TimeWindow.Session; s.WindowPicked = true; }), true),
        ("edda-battle-damage-session-quiet", eddaQuietS, As("Edda", Chapter.Battle, "damage", s => { s.Window = TimeWindow.Session; s.WindowPicked = true; }), true),   // 0.8.6: nothing fought since you came in, the same frame
        // a long evening in the weapons (WeaponScenario: every weapon cell 12 times as long, so a pace and a change mark show): your By weapon shut, with the Bronze Spear's skill levels open, and Edda's
        ("battle-damage-weapons", longWeapons, Battle(TimeWindow.SevenDays, "damage"), true),
        ("battle-damage-weapons-open", longWeapons, Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.SevenDays; s.View[PanelModel.WeaponOpenKey] = "SpearBronze|Spears"; }), true),
        ("battle-damage-weapons-levels", longWeapons, Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.SevenDays; s.View[PanelModel.WeaponOpenKey] = "SwordIron|Swords"; s.View[PanelModel.WeaponCursorKey] = "SwordIron|Swords"; }), true),
        ("battle-damage-weapons-bowshut", longWeapons, Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.SevenDays; s.View[PanelModel.KindShutKey] = "bow"; s.View[PanelModel.WeaponCursorKey] = PanelModel.KindToggleLink("bow"); }), true),
        ("battle-damage-weapons-allshut", longWeapons, Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.SevenDays; s.View[PanelModel.KindShutKey] = string.Join(";", PanelModel.WeaponKinds.OrderBy(k => k, StringComparer.Ordinal)); }), true),
        ("edda-battle-damage-weapons-open", longEdda, As("Edda", Chapter.Battle, "damage", s => { s.Window = TimeWindow.SevenDays; s.WindowPicked = true; s.View[PanelModel.WeaponOpenKey] = "AxeIron|Axes;AxeIron|Axes|2"; }), true),
        ("voyages-sailing-7days", world, Page(Chapter.Voyages, "sailing", s => s.Window = TimeWindow.SevenDays), true),
        ("voyages-sailing-today", world, Page(Chapter.Voyages, "sailing", s => s.Window = TimeWindow.Today), true),
        ("voyages-cargo-7days", world, Page(Chapter.Voyages, "cargo", s => s.Window = TimeWindow.SevenDays), true),
        ("deeds-woodcutting-7days", world, Page(Chapter.Deeds, "woodcutting", s => s.Window = TimeWindow.SevenDays), true),
        ("deeds-mining-7days", world, Page(Chapter.Deeds, "mining", s => s.Window = TimeWindow.SevenDays), true),
        // 0.7: every Deeds page but the Overview has the day windows
        ("deeds-cooking-7days", world, Page(Chapter.Deeds, "cooking", s => s.Window = TimeWindow.SevenDays), true),
        ("deeds-building-7days", world, Page(Chapter.Deeds, "building", s => s.Window = TimeWindow.SevenDays), true),
        ("deeds-groundwork-7days", world, Page(Chapter.Deeds, "groundwork", s => s.Window = TimeWindow.SevenDays), true),
        ("deeds-crafting-7days", world, Page(Chapter.Deeds, "crafting", s => s.Window = TimeWindow.SevenDays), true),
        ("deeds-farming-7days", world, Page(Chapter.Deeds, "farming", s => s.Window = TimeWindow.SevenDays), true),
        ("deeds-fishing-7days", world, Page(Chapter.Deeds, "fishing", s => s.Window = TimeWindow.SevenDays), true),
        ("deeds-taming-7days", world, Page(Chapter.Deeds, "taming", s => s.Window = TimeWindow.SevenDays), true),
        ("deeds-building-today", world, Page(Chapter.Deeds, "building", s => s.Window = TimeWindow.Today), true),
        // 0.7: the short windows on the Deeds pages, from this session's DeedLog (DeedsShort)
        ("deeds-woodcutting-10min", world, Page(Chapter.Deeds, "woodcutting", s => s.Window = TimeWindow.LastTenMinutes), true),
        ("deeds-mining-30min", world, Page(Chapter.Deeds, "mining", s => s.Window = TimeWindow.LastThirtyMinutes), true),
        ("deeds-building-1h", world, Page(Chapter.Deeds, "building", s => s.Window = TimeWindow.LastHour), true),
        ("deeds-cooking-session", world, Page(Chapter.Deeds, "cooking", s => { s.Window = TimeWindow.Session; s.WindowPicked = true; }), true),
        ("deeds-farming-10min", world, Page(Chapter.Deeds, "farming", s => s.Window = TimeWindow.LastTenMinutes), true),
        ("company-together-dealt-7days", world, Together("dealt", "SevenDays"), true),
        ("company-together-dealt-30days", world, Together("dealt", "ThirtyDays"), true),   // before the history: greyed, All shown, the one line
        // 0.8.5 (Joost in game: "why no time filters on the other tabs?"): every category in every window; sailed and cargo from each player's minutes
        // and shared voyage days
        ("company-together-wood-today", world, Together("wood", "Today"), true),
        ("company-together-pieces-7days", world, Together("built", "SevenDays"), true),
        ("company-together-dishes-session", world, Together("cooked", "Session"), true),
        ("company-together-ore", world, Together("ore"), true),   // the longest hero words: its qualifier now in About these numbers (it ran into them in game)
        ("company-together-sailed-7days", world, Together("sailed", "SevenDays"), true),
        ("company-together-cargo-session", world, Together("cargo", "Session"), true),
        ("battle-deaths-30min", world, Battle(TimeWindow.LastThirtyMinutes, "deaths"), true),
        ("battle-empty", emptyS, new PanelState { Chapter = Chapter.Battle, ViewKey = "F" }, true),
        ("battle-today-empty", emptyDayS, new PanelState { Chapter = Chapter.Battle, ViewKey = "F", Window = TimeWindow.Today }, true),
        ("about", world, new PanelState { ShowAbout = true }, true),
        ("about-reads", world, new PanelState { ShowAbout = true, AboutPage = "reads" }, true),
        ("about-sharing", world, new PanelState { ShowAbout = true, AboutPage = "sharing" }, true),
        ("voyages", world, Page(Chapter.Voyages, null), true),
        ("voyages-sailing", world, Page(Chapter.Voyages, "sailing"), true),
        ("voyages-cargo", world, Page(Chapter.Voyages, "cargo"), true),
        ("voyages-onfoot", world, Page(Chapter.Voyages, "onfoot"), true),
        ("voyages-maps", world, Page(Chapter.Voyages, "maps"), true),
        ("skills", world, Page(Chapter.Skills, null), true),
        ("skills-practised", world, Page(Chapter.Skills, null, s => s.View["Skills/overview/view"] = "practised"), true),
        ("skill-axes", world, Page(Chapter.Skills, "Axes"), true),
        ("deeds-woodcutting", world, Page(Chapter.Deeds, "woodcutting"), true),
        ("deeds-woodcutting-hover", world, Page(Chapter.Deeds, "woodcutting"), true),   // 0.8 hover (PANEL_HOVER): the pointer on Corewood
        ("deeds-mining", world, Page(Chapter.Deeds, "mining"), true),
        ("deeds-mining-rocks", rocksS, Page(Chapter.Deeds, "mining"), true),
        ("deeds-mining-hero", miningHeroS, Page(Chapter.Deeds, "mining"), true),   // 0.8.2: the one-hero rule on Mining's long labels   // 0.8.2: the per-rock list by the game's names, the rest one quiet row
        ("deeds-building", world, Page(Chapter.Deeds, "building"), true),
        ("deeds-building-furniture-finewood", world, Page(Chapter.Deeds, "building", s => { s.Facets["Deeds/building/pieces|tab"] = new List<string> { "Furniture" }; s.Facets["Deeds/building/pieces|material"] = new List<string> { "Fine wood" }; }), true),
        ("deeds-building-open", world, Page(Chapter.Deeds, "building", s => s.OpenFilters.Add(PanelModel.BuildFilter)), true),
        ("deeds-building-modded", moddedHallS, Page(Chapter.Deeds, "building", s => s.OpenFilters.Add(PanelModel.BuildFilter)), true),
        ("deeds-building-manykinds", manyKindsS, Page(Chapter.Deeds, "building"), true),
        ("deeds-building-manykinds-bycategory", manyKindsS, Page(Chapter.Deeds, "building", s => s.View["Deeds/building/sort"] = PanelModel.SortGroup), true),
        ("deeds-building-manykinds-showall", manyKindsS, Page(Chapter.Deeds, "building", s => s.ShowAll = PanelModel.ShowAllKey(s)), true),
        ("deeds-building-recovered", rebuildS, Page(Chapter.Deeds, "building"), true),          // 0.8: materials recovered, the bar form
        ("deeds-building-recovered-one", rebuildOneS, Page(Chapter.Deeds, "building"), true),   // 0.8: one kind, one tile
        ("deeds-building-focus", world, Page(Chapter.Deeds, "building", s => { s.FilterRow = 1; s.FilterCursor = 2; s.Facets["Deeds/building/pieces|tab"] = new List<string> { "Building", "Stonecutter" }; }), true),
        ("deeds-building-az", world, Page(Chapter.Deeds, "building", s => s.View["Deeds/building/sort"] = PanelModel.SortAz), true),
        ("deeds-building-least", world, Page(Chapter.Deeds, "building", s => s.View["Deeds/building/sort"] = PanelModel.SortLeast), true),   // 0.8: Most clicked again
        ("deeds-fishing-za", world, Page(Chapter.Deeds, "fishing", s => s.View["Deeds/fishing/sort"] = PanelModel.SortZa), true),            // 0.8: A-Z clicked again
        ("deeds-building-bycategory", world, Page(Chapter.Deeds, "building", s => s.View["Deeds/building/sort"] = PanelModel.SortGroup), true),
        ("deeds-building-bycategory-stone", world, Page(Chapter.Deeds, "building", s => { s.View["Deeds/building/sort"] = PanelModel.SortGroup; s.Facets["Deeds/building/pieces|material"] = new List<string> { "Stone", "Wood" }; }), true),
        ("deeds-groundwork", world, Page(Chapter.Deeds, "groundwork"), true),
        ("deeds-crafting", world, Page(Chapter.Deeds, "crafting"), true),
        ("deeds-crafting-open", world, Page(Chapter.Deeds, "crafting", s => s.OpenFilters.Add(PanelModel.CraftFilter)), true),
        ("deeds-crafting-armour-leather", world, Page(Chapter.Deeds, "crafting", s => { s.Facets["Deeds/crafting/gear|kind"] = new List<string> { "armour" }; s.Facets["Deeds/crafting/gear|material"] = new List<string> { "Leather" }; }), true),
        ("deeds-crafting-focus", world, Page(Chapter.Deeds, "crafting", s => { s.FilterRow = 1; s.FilterCursor = 2; s.Facets["Deeds/crafting/gear|kind"] = new List<string> { "weapons", "armour" }; }), true),
        ("deeds-crafting-bykind", world, Page(Chapter.Deeds, "crafting", s => s.View["Deeds/crafting/sort"] = PanelModel.SortGroup), true),
        ("deeds-fishing-az", world, Page(Chapter.Deeds, "fishing", s => s.View["Deeds/fishing/sort"] = PanelModel.SortAz), true),
        ("deeds-farming", world, Page(Chapter.Deeds, "farming"), true),
        ("deeds-fishing", world, Page(Chapter.Deeds, "fishing"), true),
        ("deeds-fishing-picked", fishHeldS, Page(Chapter.Deeds, "fishing"), true),
        ("deeds-taming", world, Page(Chapter.Deeds, "taming"), true),
        ("deeds-farming-later", laterS, Page(Chapter.Deeds, "farming"), true),
        ("deeds-farming-big", bigS, Page(Chapter.Deeds, "farming"), true),
        ("deeds-farming-layered", layeredS, Page(Chapter.Deeds, "farming"), true),
        ("deeds-cooking-layered", cookedS, Page(Chapter.Deeds, "cooking"), true),
        ("deeds-cooking-today", world, Page(Chapter.Deeds, "cooking", s => { s.Window = TimeWindow.Today; s.WindowPicked = true; }), true),   // 0.8.5: Who enjoyed your food from the fellows' shared days
        // the fellows' books, as Rowan sees them (Edda hauls and sails, Finch scouts, Tor holds the line)
        ("edda-woodcutting", books["Edda"], As("Edda", Chapter.Deeds, "woodcutting"), true),
        ("edda-deeds-woodcutting-today", books["Edda"], As("Edda", Chapter.Deeds, "woodcutting", s => { s.Window = TimeWindow.Today; s.WindowPicked = true; }), true),   // 0.8.5: her shared day, the two lines
        ("tor-cooking", books["Tor"], As("Tor", Chapter.Deeds, "cooking"), true),
        ("edda-company", books["Edda"], As("Edda", Chapter.Company, "food"), true),
        ("edda-fireside", books["Edda"], As("Edda", Chapter.Company, "fireside"), true),
        ("company-fireside-busy", world, Page(Chapter.Company, null), true),
        ("company-fireside-four", world, Page(Chapter.Company, null), true),
        ("edda-sailing", books["Edda"], As("Edda", Chapter.Voyages, "sailing"), true),
        ("edda-cargo", books["Edda"], As("Edda", Chapter.Voyages, "cargo"), true),
        ("edda-sailing-7days", books["Edda"], As("Edda", Chapter.Voyages, "sailing", s => { s.Window = TimeWindow.SevenDays; s.WindowPicked = true; }), true),   // 0.8.5: her shared voyage days, the number her Everyone row shows
        ("edda-cargo-today", books["Edda"], As("Edda", Chapter.Voyages, "cargo", s => { s.Window = TimeWindow.Today; s.WindowPicked = true; }), true),        // 0.8.5: her cargo per item today, no sum (no distances shared)
        ("finch-maps", books["Finch"], As("Finch", Chapter.Voyages, "maps"), true),
        ("tor-battle", books["Tor"], As("Tor", Chapter.Battle, null), true),
        ("tor-battle-defense", books["Tor"], As("Tor", Chapter.Battle, "defense"), true),   // ISC-22: a fellow's Received from, per foe per damage type
        ("tor-battle-damage-foe", books["Tor"], As("Tor", Chapter.Battle, "damage", s => s.View["Battle/damage/view"] = "foe"), true),   // REVIEW-08 #2: a fellow's By foe in Session
        ("tor-battle-damage-foe-before", torBeforeS, As("Tor", Chapter.Battle, "damage", s => s.View["Battle/damage/view"] = "foe"), true),
        // 0.8.2 (H; Joost on a friend's Battle > Foes: "Why are there no 10 min and 30 min on my friend's stats?"): Edda's copy is live, so her 10 min is open
        ("edda-battle-foes-10min", books["Edda"], As("Edda", Chapter.Battle, "foes", s => { s.Window = TimeWindow.LastTenMinutes; s.WindowPicked = true; }), true),
        ("edda-battle-damage-10min", books["Edda"], As("Edda", Chapter.Battle, "damage", s => { s.Window = TimeWindow.LastTenMinutes; s.WindowPicked = true; }), true),
        ("edda-battle-damage-today", books["Edda"], As("Edda", Chapter.Battle, "damage", s => { s.Window = TimeWindow.Today; s.WindowPicked = true; s.View["Battle/damage/view"] = "type"; }), true),   // 0.9: her days from her shared parts, the whole-day line
        ("edda-battle-offline", eddaOffS, As("Edda", Chapter.Battle, "damage", s => { s.Window = TimeWindow.LastTenMinutes; s.WindowPicked = true; }), true),   // PANEL_HOVER: the pointer on 10 min
        // 0.8.2 (I): a friend on Hearthwoven 0.6.5: one line where a page misses something
        ("edda-old-battle-defense", eddaOldS, As("Edda", Chapter.Battle, "defense"), true),
        // 0.9 armor-share: a friend's armor view, both views, and the older copy's one line
        ("edda-battle-defense-hurt", books["Edda"], As("Edda", Chapter.Battle, "defense", s => s.View["Battle/defense/view"] = PanelModel.ReceivedId), true),
        ("edda-battle-defense-armor", books["Edda"], As("Edda", Chapter.Battle, "defense", s => s.View["Battle/defense/view"] = PanelModel.ArmourId), true),
        ("edda-battle-defense-armor-numbers", books["Edda"], Numbered(As("Edda", Chapter.Battle, "defense", s => s.View["Battle/defense/view"] = PanelModel.ArmourId)), true),   // her About box: a friend's lines, no "Complete for you"
        ("edda-old-battle-defense-armor", eddaOldS, As("Edda", Chapter.Battle, "defense", s => s.View["Battle/defense/view"] = PanelModel.ArmourId), true),
        ("edda-old-battle-feed", eddaOldS, As("Edda", Chapter.Battle, PanelModel.FeedPage), true),
        // 0.8.2 (J; Joost, a friend's Foes empty in Session after the friend reconnected): her earlier connection's fights, joined
        ("edda-rejoined-battle-foes", eddaRejoinedS, As("Edda", Chapter.Battle, "foes", s => { s.Window = TimeWindow.Session; s.WindowPicked = true; }), true),
        ("sharing-off", world, Page(Chapter.Deeds, null), false),
        // 0.7 redesign (REDESIGN-RULES.md part 2, step 8): every page of part 5 with its "About these numbers" box open (Y). A page that has
        // not migrated yet builds no box, so its -numbers shot is its plain page; Battle's on All, where the periods differ
        ("deeds-woodcutting-numbers", world, Page(Chapter.Deeds, "woodcutting", Numbers), true),
        ("deeds-mining-numbers", world, Page(Chapter.Deeds, "mining", Numbers), true),
        ("deeds-cooking-numbers", world, Page(Chapter.Deeds, "cooking", Numbers), true),
        ("deeds-building-numbers", world, Page(Chapter.Deeds, "building", Numbers), true),
        ("deeds-groundwork-numbers", world, Page(Chapter.Deeds, "groundwork", Numbers), true),
        ("deeds-crafting-numbers", world, Page(Chapter.Deeds, "crafting", Numbers), true),
        ("deeds-farming-numbers", world, Page(Chapter.Deeds, "farming", Numbers), true),
        ("deeds-fishing-numbers", world, Page(Chapter.Deeds, "fishing", Numbers), true),
        ("deeds-taming-numbers", world, Page(Chapter.Deeds, "taming", Numbers), true),
        ("deeds-numbers", world, Page(Chapter.Deeds, null, Numbers), true),
        ("feats-numbers", world, Page(Chapter.Feats, null, Numbers), true),
        ("battle-numbers", world, Numbered(Battle(TimeWindow.SinceInstall, null)), true),
        ("battle-damage-numbers", world, Numbered(Battle(TimeWindow.SinceInstall, "damage")), true),
        ("battle-defense-numbers", world, Numbered(Battle(TimeWindow.SinceInstall, "defense")), true),
        ("battle-defense-armour-numbers", world, Numbered(Armour(TimeWindow.SinceInstall)), true),   // Your armour on All: its own box (ArmourModel.ArmourNumbers)
        ("battle-deaths-numbers", world, Numbered(Battle(TimeWindow.SinceInstall, "deaths")), true),
        ("battle-foes-numbers", world, Numbered(Battle(TimeWindow.SinceInstall, "foes")), true),
        ("company-numbers", world, Page(Chapter.Company, null, Numbers), true),
        ("company-together-numbers", world, Page(Chapter.Company, "together", Numbers), true),
        ("company-food-numbers", world, Page(Chapter.Company, "food", Numbers), true),
        ("company-gear-numbers", world, Page(Chapter.Company, "gear", Numbers), true),
        ("hall-numbers", world, Page(Chapter.Stores, null, Numbers), true),
        ("hall-trader-numbers", world, Page(Chapter.Stores, "trader", Numbers), true),
        ("hall-smelters-numbers", world, Page(Chapter.Stores, "smelters", Numbers), true),
        ("voyages-numbers", world, Page(Chapter.Voyages, null, Numbers), true),
        ("voyages-sailing-numbers", world, Page(Chapter.Voyages, "sailing", Numbers), true),
        ("voyages-cargo-numbers", world, Page(Chapter.Voyages, "cargo", Numbers), true),
        ("voyages-onfoot-numbers", world, Page(Chapter.Voyages, "onfoot", Numbers), true),
        ("voyages-maps-numbers", world, Page(Chapter.Voyages, "maps", Numbers), true),
        ("skills-numbers", world, Page(Chapter.Skills, null, Numbers), true),
        ("skills-practised-numbers", world, Page(Chapter.Skills, null, s => { s.View["Skills/overview/view"] = "practised"; s.ShowNumbers = true; }), true),
        ("skill-axes-numbers", world, Page(Chapter.Skills, "Axes", Numbers), true),
        ("joining", world, Page(Chapter.Deeds, null), true),
        ("joining-alone", world, Page(Chapter.Deeds, null), true),
        // 0.8 the Everyone chip (Chapters/EveryoneModel.cs, PICKS.md section 1): the group's view of every page that adds up, the chip greyed where nothing adds up
        ("everyone-row-off", world, Page(Chapter.Deeds, "woodcutting"), true),   // the chip off: your own book, the chip at the row's end
        ("everyone-woodcutting", world, Ev(Page(Chapter.Deeds, "woodcutting")), true),
        ("everyone-woodcutting-10min", world, Ev(Page(Chapter.Deeds, "woodcutting", s => { s.Window = TimeWindow.LastTenMinutes; s.WindowPicked = true; })), true),   // Tor sends no live updates: named, left out
        ("everyone-woodcutting-asof", finchAsOfS, Ev(Page(Chapter.Deeds, "woodcutting")), true),   // 0.8.1 review 1: "Finch" beside "as of ...": the name gives way
        ("everyone-mining", world, Ev(Page(Chapter.Deeds, "mining")), true),
        ("everyone-building", world, Ev(Page(Chapter.Deeds, "building")), true),
        ("everyone-groundwork", world, Ev(Page(Chapter.Deeds, "groundwork")), true),
        ("everyone-crafting", world, Ev(Page(Chapter.Deeds, "crafting")), true),
        ("everyone-cooking", world, Ev(Page(Chapter.Deeds, "cooking")), true),
        ("everyone-cooking-7days", world, Ev(Page(Chapter.Deeds, "cooking", s => { s.Window = TimeWindow.SevenDays; s.WindowPicked = true; })), true),   // 0.8.5: each fellow's shared days
        ("everyone-farming", world, Ev(Page(Chapter.Deeds, "farming")), true),
        ("everyone-fishing", world, Ev(Page(Chapter.Deeds, "fishing")), true),
        ("everyone-taming", world, Ev(Page(Chapter.Deeds, "taming")), true),
        ("everyone-battle-damage", world, Ev(Battle(TimeWindow.LastHour, "damage")), true),
        ("everyone-battle-damage-weapon", world, Ev(Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.LastHour; s.View["Battle/damage/group"] = "weapon"; })), true),
        // 0.8.6 (the tag beside its bar, render-check): Everyone > Damage > By weapon, Session; PANEL_HOVER: the pointer on Melee's Blunt part, whose tag says both numbers, "Blunt 1 180: 960 got through, 220 stopped", beside the bar, not over it
        ("everyone-battle-damage-weapon-all", world, Ev(Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.SinceInstall; s.View["Battle/damage/group"] = "weapon"; })), true),   // 0.8.6 (Joost in game): All, most of it from before armor was counted: plain bars and the one line where the split is known
        ("everyone-battle-damage-weapon-hover", world, Ev(Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.Session; s.View["Battle/damage/group"] = "weapon"; })), true),
        ("everyone-battle-damage-type", world, Ev(Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.Session; s.View["Battle/damage/group"] = "type"; })), true),   // types down, players across (group-battle b)
        ("everyone-battle-damage-7days", world, Ev(Battle(TimeWindow.SevenDays, "damage")), true),   // 0.9: each fellow's days by weapon kind and type: By player with splits, By weapon, By type
        ("everyone-battle-damage-today", world, Ev(Battle(TimeWindow.Today, "damage")), true),   // 0.9: By player with every row's split, and the one line that each player's whole day counts
        ("everyone-battle-damage-today-weapon", world, Ev(Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.Today; s.View["Battle/damage/group"] = "weapon"; })), true),
        ("everyone-battle-damage-today-type", world, Ev(Page(Chapter.Battle, "damage", s => { s.Window = TimeWindow.Today; s.View["Battle/damage/group"] = "type"; })), true),
        ("everyone-battle-defense", world, Ev(Battle(TimeWindow.SinceInstall, "defense")), true),
        // 0.8.2 (Joost in game, Battle > Foes, 1 h: "switch between my friends"): a row per foe kind for the group, its Dealt bar split by player; the
        // open foe with Who hit it on top of Best and Worst; the pointer on Edda's part of the top foe's bar (PANEL_HOVER)
        ("everyone-battle-foes", world, Ev(Battle(TimeWindow.LastHour, "foes")), true),
        ("everyone-battle-foes-open", world, Ev(FoesOpen("Draugr", TimeWindow.LastHour)), true),
        ("everyone-battle-foes-hover", world, Ev(Battle(TimeWindow.LastHour, "foes")), true),
        ("battle-foes-big", bigFoesS, Battle(TimeWindow.SinceInstall, "foes"), true),   // 0.8.2: "123 456" and "12 645" whole beside their bars
        ("everyone-voyages-sailing", world, Ev(Page(Chapter.Voyages, "sailing")), true),
        ("everyone-voyages-cargo", world, Ev(Page(Chapter.Voyages, "cargo")), true),
        // 0.8.5: Sailing and Cargo open Today to 30 days from each fellow's shared voyage days (one cargo total each: the item bar leaves them out, said once)
        ("everyone-voyages-sailing-7days", world, Ev(Page(Chapter.Voyages, "sailing", s => { s.Window = TimeWindow.SevenDays; s.WindowPicked = true; })), true),
        ("everyone-voyages-cargo-today", world, Ev(Page(Chapter.Voyages, "cargo", s => { s.Window = TimeWindow.Today; s.WindowPicked = true; })), true),
        ("everyone-voyages-onfoot", world, Ev(Page(Chapter.Voyages, "onfoot")), true),
        ("everyone-hall-trader", world, Ev(Page(Chapter.Stores, "trader")), true),
        ("everyone-hall-smelters", world, Ev(Page(Chapter.Stores, "smelters")), true),
        ("everyone-hall-smelters-one", handOneS, Ev(Page(Chapter.Stores, "smelters")), true),
        ("hall-smelters-server", handOneS, Page(Chapter.Stores, "smelters"), true),
        ("everyone-skills", world, Ev(Page(Chapter.Skills, null)), true),
        ("everyone-skills-hover", world, Ev(Page(Chapter.Skills, null)), true),   // the greyed chip's hover: what a press opens
        ("could-not-draw", world, Ev(Page(Chapter.Deeds, "crafting")), true),   // v08-fix-open: a page that throws, as the book shows it (PanelModel.CouldNotDraw)
        ("everyone-overview-hover", world, Ev(Page(Chapter.Deeds, "overview")), true),   // 0.8: where it works ("on the other pages"), a press opens Cooking
        ("everyone-voyages-maps-hover", world, Ev(Page(Chapter.Voyages, "maps")), true),   // "on Sailing and more", the whole list on hover; the nearest page is up the list
        // 0.8 Compare periods (CompareModel.cs): 7 days against the 7 before on the scenario's two weeks; on the world (a week of history) Today pairs
        // with yesterday, 7 days greys with the day it opens, an hour greys because Compare needs a day window (both with the reason's tag shown)
        ("deeds-woodcutting-compare", compareS, Compared(Chapter.Deeds, "woodcutting"), true),
        ("deeds-mining-compare", compareS, Compared(Chapter.Deeds, "mining"), true),
        ("deeds-cooking-compare", compareS, Compared(Chapter.Deeds, "cooking"), true),
        ("deeds-building-compare", compareS, Compared(Chapter.Deeds, "building"), true),
        ("deeds-farming-compare", compareS, Compared(Chapter.Deeds, "farming"), true),
        ("deeds-fishing-compare", compareS, Compared(Chapter.Deeds, "fishing"), true),
        ("deeds-taming-compare", compareS, Compared(Chapter.Deeds, "taming"), true),
        ("battle-defense-armour-compare", compareS, Compared(Chapter.Battle, "defense", s => s.View["Battle/defense/view"] = PanelModel.ArmourId), true),
        ("battle-damage-compare", compareS, Compared(Chapter.Battle, "damage"), true),
        ("battle-damage-type-compare", compareS, Compared(Chapter.Battle, "damage", s => s.View["Battle/damage/view"] = "type"), true),
        ("battle-defense-compare", compareS, Compared(Chapter.Battle, "defense"), true),
        ("voyages-sailing-compare", compareS, Compared(Chapter.Voyages, "sailing"), true),
        ("voyages-cargo-compare", compareS, Compared(Chapter.Voyages, "cargo"), true),
        ("company-together-dealt-compare", compareS, CompareOn(Together("dealt", "SevenDays")), true),
        ("deeds-woodcutting-today-compare", world, Compared(Chapter.Deeds, "woodcutting", s => s.Window = TimeWindow.Today), true),
        ("deeds-woodcutting-compare-greyed", world, Compared(Chapter.Deeds, "woodcutting", s => s.CompareTip = true), true),
        // 0.8 integration: By foe compares the damage per foe kind; with Everyone on, Battle > Damage compares a row per player, other pages grey Compare
        ("battle-damage-foe-compare", compareS, Compared(Chapter.Battle, "damage", s => s.View["Battle/damage/view"] = "foe"), true),
        ("everyone-battle-damage-compare", compareS, Compared(Chapter.Battle, "damage", s => s.Everyone = true), true),
        ("everyone-woodcutting-compare-greyed", compareS, Compared(Chapter.Deeds, "woodcutting", s => { s.Everyone = true; s.CompareTip = true; }), true),
        ("battle-damage-compare-hour", world, Compared(Chapter.Battle, "damage", s => { s.Window = TimeWindow.LastHour; s.CompareTip = true; }), true),
        // 0.8.5 (proto/4-compare C): what to compare against, chosen in the chip's menu: the same day last week, your best day (Today), your best
        // 7 days; the menu open on Today and on 7 days (there the same 7 days last month is greyed: two weeks of history do not reach it)
        ("deeds-woodcutting-today-compare-week", compareS, Compared(Chapter.Deeds, "woodcutting", s => { s.Window = TimeWindow.Today; s.CompareWith["Deeds/woodcutting"] = PanelModel.CompareSameId; }), true),
        ("deeds-woodcutting-today-compare-best", compareS, Compared(Chapter.Deeds, "woodcutting", s => { s.Window = TimeWindow.Today; s.CompareWith["Deeds/woodcutting"] = PanelModel.CompareBestId; }), true),
        ("deeds-woodcutting-today-compare-menu", compareS, Compared(Chapter.Deeds, "woodcutting", s => { s.Window = TimeWindow.Today; s.CompareMenu = true; }), true),
        ("deeds-building-compare-menu", compareS, Compared(Chapter.Deeds, "building", s => { s.CompareMenu = true; s.CompareWith["Deeds/building"] = PanelModel.CompareBestId; }), true),
        ("deeds-building-compare-best", compareS, Compared(Chapter.Deeds, "building", s => s.CompareWith["Deeds/building"] = PanelModel.CompareBestId), true),
    }.Concat(realBook == null ? new (string name, PanelInput inp, PanelState st, bool sharing)[0] : new (string name, PanelInput inp, PanelState st, bool sharing)[]
    {
        // Joost's real book (scenario real-*): his own numbers on the pages the rules name
        ("real-woodcutting", realBook, Page(Chapter.Deeds, "woodcutting"), false),
        ("real-farming", realBook, Page(Chapter.Deeds, "farming"), false),
        ("real-fishing", realBook, Page(Chapter.Deeds, "fishing"), false),
    }).ToArray();
    // the rule: a shot that is not a named scenario reads the world (Rowan's own book, or a fellow's copy of it)
    var offWorld = shots.Where(s => !scenarios.ContainsKey(s.name) && !ReferenceEquals(s.inp, world) && !books.Values.Any(b => ReferenceEquals(b, s.inp))).Select(s => s.name).ToList();
    Check(offWorld.Count == 0, "dump: every preview shot reads the sample world (PanelSample.Full) unless it is a named scenario" + (offWorld.Count > 0 ? ": " + string.Join(", ", offWorld) : " (" + shots.Length + " shots, " + scenarios.Count + " scenarios)"));
    var firePlans = new List<string>();   // the Fireside drawings as FireLayout worked them out: panel-preview.html draws exactly these (0.8.5)
    var loose = new List<string>();   // 0.8 layout D+ (FEEDBACK 13b): text between the strip and the hero, on any preview page
    // 0.8 layout D+ (Islands.cs): every block a page draws sits in exactly one island: none lost, none twice (the strip and the heading row's
    // blocks excepted; a switch opens into its chosen view, a page's two columns into two islands, Recent's group into one island per player)
    var islandsOff = new List<string>();
    var numbersOff = new List<string>();   // 0.8.6 honest numbers (HonestNumbersTests.cs): no part bigger than its whole, no share above 100 %
    var groupSparks = new List<string>();   // FEEDBACK 13: fellow players share no days, so a group page has no growth line anywhere
    List<string> IslandsLost(Block pl)
    {
        var drawn = new Dictionary<Block, int>();
        foreach (var isl in PanelModel.Islands(pl)) foreach (var part in isl.Parts) drawn[part.Block] = (drawn.TryGetValue(part.Block, out var n) ? n : 0) + 1;
        var strip = PanelModel.TopSwitch(pl); var off = new List<string>();
        void Want(Block b, int times)
        {
            var got = drawn.TryGetValue(b, out var n) ? n : 0;
            if (got != times) off.Add(b.Kind + " '" + (b.Title ?? b.Text) + "' drawn " + got + " times, not " + times);
        }
        void Walk(IEnumerable<Block> bs)
        {
            foreach (var b in bs ?? new List<Block>())
            {
                if (b.Kind == "featband" || b.Kind == "headlink" || b.Kind == "divider" || (b.Kind == "ladders" && PanelModel.IsHeadStrip(b)) || (b.Kind == "aboutnumbers" && (!b.Open || pl.Items.Any(x => x.Kind == "together")))) continue;   // 0.8.5: Together draws its open box under its key line
                if (b.Kind == "switch") { if (b != strip) Want(b, 1); Walk(b.Items.Where(x => x.Selected).SelectMany(x => x.Items ?? new List<Block>())); continue; }
                var cols = b.Kind == "columns" ? (b.Items ?? new List<Block>()).Where(c => c.Items?.Count > 0).ToList() : null;
                if (cols != null && cols.Count == 2) { Walk(cols.SelectMany(c => c.Items)); continue; }
                Want(b, b.Kind == "people" && drawn.TryGetValue(b, out var n) && n > 1 ? b.Items.Count : 1);
            }
        }
        Walk(pl.Items);
        return off;
    }
    var js = "// Generated by test-panel --dump from PanelModel.Build on the sample world (PanelSample.Full: Rowan, Edda, Finch and Tor; fictional). Do not edit.\n" +
             "// Every shot reads that one world except the scenarios listed in window.PANEL_SCENARIOS.\nwindow.PANEL_DATA = {\n" +
             string.Join(",\n", shots.Select(s =>
             {
                 var keep = s.inp.Fellows;
                 if (!s.sharing) s.inp.Fellows = null;   // sharing off: PanelUi passes no fellow players
                 var v = s.name == "could-not-draw" ? PanelModel.CouldNotDraw(s.inp, s.st) : PanelModel.Build(s.inp, s.st);
                 s.inp.Fellows = keep;
                 loose.AddRange(PanelModel.LooseText(v).Select(t => s.name + ": " + t));
                 numbersOff.AddRange(HonestNumbers.Off(v).Select(t => s.name + ": " + t));
                 if (v.EveryoneOn && PanelModel.Content(v).Any(b => (b.Items ?? new List<Block>()).Any(i => i.Kind == PanelModel.SparkKind))) groupSparks.Add(s.name);
                 if (PanelModel.PlateOf(v) is Block pl && pl.Tone != PanelModel.PlateFull) islandsOff.AddRange(IslandsLost(pl).Select(t => s.name + ": " + t));
                 var give = PanelModel.Content(v).FirstOrDefault(b => b.Kind == "giving");
                 if (give != null && s.name == "company-fireside-busy") give.Items = FireBusy(6, 69).Items;   // the stress group in place of the world's four (labelled scenario)
                 if (give != null && s.name == "company-fireside-four") give.Items = FireFour().Items;   // Joost's evening of 10 Oct (labelled scenario)
                 if (give != null) firePlans.Add("  \"" + s.name + "\": " + FireLayout.ToJson(FireLayout.Build(give, 180, 232)));
                 PanelModel.AddPlayers(v, "Rowan", s.name == "joining-alone" ? new string[0] : group, s.st.Player, s.sharing, joining: s.name.StartsWith("joining", StringComparison.Ordinal) ? new[] { "Ylva" } : null, bookKey: s.st.BookKey);
                 return "  \"" + s.name + "\": " + PanelModel.ToJson(v, islands: true);
             })) + ",\n" + FeatsTests.DumpShots(group, now) + ",\n" + FeatSearchTests.DumpShots(group, now) + "\n};\n" +
             "window.PANEL_SCENARIOS = {\n" + string.Join(",\n", scenarios.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => "  \"" + kv.Key + "\": \"" + kv.Value.Replace("\"", "'") + "\"")) + "\n};\n";   // FeatsTests: the Feats views (test-panel/FeatsTests.cs)
    js += "window.FIRE_PLANS = {" + string.Join(",", firePlans) + "};\n";
    // the header-space rule (FEEDBACK 13b, PageHead.cs): one page head with fixed slots; an explanation goes into About these numbers or a chip's hover
    // reason, never between the strip and the hero. Over every page the preview draws, so a new page cannot break it unnoticed
    foreach (var t in loose.Take(20)) System.Console.WriteLine("  loose: " + t);
    Check(loose.Count == 0, "header space: no text between the strip and the hero on any of the " + shots.Length + " preview pages (" + loose.Count + " found)");
    foreach (var t in islandsOff.Take(20)) System.Console.WriteLine("  island: " + t);
    Check(groupSparks.Count == 0, "group pages: no growth line on any Everyone page (" + (groupSparks.Count == 0 ? "none of " + shots.Count(x => x.st.Everyone) + " shots with Everyone on" : string.Join(", ", groupSparks)) + ")");
    foreach (var t in numbersOff.Take(20)) System.Console.WriteLine("  numbers: " + t);
    Check(numbersOff.Count == 0, "honest numbers: on every preview page no weapon line is bigger than its kind row nor do the lines add up to more; every after-armor part adds up to what was swung (got through + stopped; a weak part its counted part); a share above 100 % says why; a window with any damage from before armor was counted has no split and says where it is known (" + numbersOff.Count + " off)");
    Check(islandsOff.Count == 0, "islands: every block of every preview page sits in exactly one island, none lost, none twice (" + islandsOff.Count + " off)");
    // 0.8 hover (BarHover, PICKS 4 B): a hovered-state shot names the bar (its place among the page's bars with a list) and the part the pointer is on
    js += "window.PANEL_HOVER = {\"edda-battle-offline\": {\"win\": \"LastTenMinutes\"}, \"deeds-woodcutting-hover\": {\"bar\": 0, \"part\": 1}, \"battle-damage-hover\": {\"bar\": 0, \"part\": 5}, \"battle-damage-foe-hover\": {\"bar\": 1, \"part\": 3}, \"company-away-3days-hover\": {\"bar\": 1, \"part\": 2}, \"everyone-battle-damage-weapon-hover\": {\"bar\": 0, \"part\": 0}, \"everyone-battle-foes-hover\": {\"bar\": 0, \"part\": 1}, \"battle-foes-open-hover\": {\"bar\": 0, \"part\": 1}, \"battle-damage-afterarmor\": {\"bar\": 0, \"part\": 2}, \"battle-foes-afterarmor\": {\"bar\": 0, \"part\": 0}};\n";
    js += "window.PANEL_SCROLL = {\"deeds-cooking-today\": \"Who enjoyed your food\", \"deeds-crafting-upgrades\": \"Upgrades per item\", \"deeds-crafting-upgrades-empty\": \"Upgrades per item\", \"deeds-meals-combos\": \"Your combinations\", \"deeds-meals-combos-7days\": \"Your combinations\", \"deeds-meals-combos-empty\": \"Your combinations\", \"deeds-meals-combos-notrecorded\": \"Your combinations\", \"deeds-mealplan-three-scrolled\": 260, \"deeds-crafting-history-firsts\": \"Firsts\", \"battle-feed-100-end\": \"end\", \"battle-feed-100-scrolled\": 610, \"battle-feed-100-more\": \"end\", \"battle-feed-fellow-older\": \"end\", \"deeds-recent-feed-all-end\": \"end\", \"deeds-recent-feed-all-scrolled\": 470, \"deeds-recent-feed-all-cards\": 470, \"deeds-recent-feed-all-timeline\": 470};\n";   // 0.8.5: the plate scrolled to a section, as the wheel does in game ("end": to its end; 0.8.6: a number, that many px)
    js += "Object.assign(window.PANEL_SCROLL, {\"battle-foes-whattouse-lastfight\": 600, \"battle-foes-tie-table\": 330, \"battle-foes-whattouse-open\": 330});\n";   // 0.9 arrows track: Last fight's Best arrows island, at the page's end
    js += "window.PANEL_RICH = [" + string.Join(", ", PanelRich.Kinds.Select(k => "\"" + k + "\"")) + "];   // richtext-fix: the block kinds whose labels have rich text on (PanelRich.Kinds)\n";
    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "preview-data.js"), js);
    System.Console.WriteLine("wrote " + System.IO.Path.Combine(dir, "preview-data.js"));
}

var evAt = Array.IndexOf(args, "--everyone");
if (evAt >= 0 && evAt + 1 < args.Length) { System.IO.File.WriteAllText(args[evAt + 1], EveryoneDebug.Text(now)); System.Console.WriteLine("wrote " + args[evAt + 1]); }

// --fulltext <file>: every page of the whole sample world (PanelSample.Full), Rowan and each fellow, as plain text
var fullAt = Array.IndexOf(args, "--fulltext");
if (fullAt >= 0 && fullAt + 1 < args.Length) { System.IO.File.WriteAllText(args[fullAt + 1], FullDump.Text(now)); System.Console.WriteLine("wrote " + args[fullAt + 1]); }

// --text <file>: every page of the sample evening as plain text (heading, the [T] line, blocks with their source tags), for reading
var textAt = Array.IndexOf(args, "--text");
if (textAt >= 0 && textAt + 1 < args.Length)
{
    input.Fellows = new List<PanelInput> { edda }; edda.Fellows = new List<PanelInput> { input };
    var sb = new System.Text.StringBuilder();
    void Out(Block b, string indent)
    {
        sb.Append(indent).Append(b.Kind).Append(b.Source != null ? " {" + b.Source + "}" : "").Append(": ").Append(string.Join(" | ", new[] { b.Title, b.Value, b.Text, b.Note }.Where(x => !string.IsNullOrEmpty(x)))).Append('\n');
        foreach (var i in b.Items ?? new List<Block>()) Out(i, indent + "    ");
    }
    foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
        foreach (var l in PanelModel.Build(input, new PanelState { Chapter = ch }).List)
        {
            var v = PanelModel.Build(input, new PanelState { Chapter = ch, Page = { [ch] = l.Id } });
            sb.Append("=== ").Append(ch).Append(" / ").Append(l.Label).Append('\n')
              .Append("heading: ").Append(v.Heading).Append(v.HeadingSource != null ? " {" + v.HeadingSource + "}" : "").Append('\n')
              .Append("scope: ").Append(v.Scope).Append('\n')
              .Append("keys: ").Append(string.Join("  ", v.Keys)).Append('\n');
            foreach (var b in v.Blocks) Out(b, "  ");
        }
    var ab = PanelModel.Build(input, new PanelState { ShowAbout = true });
    sb.Append("=== About (T)\n").Append("heading: ").Append(ab.Heading).Append('\n').Append("scope: ").Append(ab.Scope).Append('\n')
      .Append("keys: ").Append(string.Join("  ", ab.Keys)).Append('\n');
    foreach (var b in ab.Blocks) Out(b, "  ");
    System.IO.File.WriteAllText(args[textAt + 1], sb.ToString());
    System.Console.WriteLine("wrote " + args[textAt + 1]);
}

System.Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
return fails == 0 ? 0 : 1;

// The game's own English names for the sample's tokens and prefabs (reference/item-names.json, the game's localization),
// so the preview shows what the panel shows in game: "Corewood", never the prefab's "Roundlog". In game PanelUi resolves
// every key through its shared name token and Localization; this small table stands in for that here.
static partial class Program
{
    internal static readonly Dictionary<string, string> GameNames = PanelSample.Names;   // src/Panel/PanelSample.cs
}
