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

var now = new DateTime(2026, 10, 7, 22, 30, 0, DateTimeKind.Utc);
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

// A sample evening for Rowan (fictional players), built through the mod's own recorders.
PanelInput Sample()
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
        Character = new Dictionary<string, float>
        {
            ["DistanceTraveled"] = 184200f, ["HarvestCrop"] = 312f, ["CraftFood"] = 146f, ["CraftWeapon"] = 9f, ["CraftArmor"] = 6f,
            ["DistanceSailHelm"] = 6600f, ["Builds"] = 1204f, ["EnemyKills"] = 860f, ["BossKills"] = 3f, ["FishCaught"] = 0f,
            ["Tree"] = 410f, ["BuildClusterDefense"] = 40f, ["TrapArmed"] = 2f, ["CraftGrill"] = 20f,
            ["CraftTool"] = 4f, ["Upgrades"] = 11f, ["MineHits"] = 650f, ["EnemyHits"] = 5400f, ["HitsTakenEnemies"] = 1300f, ["PlayerHits"] = 3f,
        },
        // as the game books them: per craft action (a batch of arrows counts once), food from the grill +1 per piece
        ItemsCrafted = new Dictionary<string, float> { ["$item_bread"] = 40, ["$item_fishwraps"] = 22, ["$item_sword_iron"] = 2, ["$item_carrotsoup"] = 118,
            ["$item_shield_wood"] = 7, ["$item_axe_bronze"] = 6, ["$item_arrow_wood"] = 12, ["$item_modthing"] = 3 },
        PiecesPlaced = new Dictionary<string, float> { ["$piece_woodwall"] = 520, ["$piece_woodfloor"] = 310, ["$piece_sharpstakes"] = 36,
            ["$piece_levelground"] = 850, ["$piece_lowerground"] = 133, ["$piece_pavedroad"] = 40, ["$piece_sapling_carrot"] = 60, ["$piece_feast_meadows"] = 2 },
        ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 2400, ["$item_finewood"] = 310, ["$item_roundlog"] = 520, ["$item_elderbark"] = 75,
            ["$item_stone"] = 1800, ["$item_copperore"] = 240, ["$item_tinore"] = 90, ["$item_raspberries"] = 40 },
        // what PanelUi derives from the game data at runtime (item type, drop tables, piece components); null = unknown
        ItemKind = t => new Dictionary<string, string> { ["$item_bread"] = "food", ["$item_fishwraps"] = "food", ["$item_carrotsoup"] = "food", ["$item_sword_iron"] = "gear",
            ["$item_shield_wood"] = "gear", ["$item_axe_bronze"] = "gear", ["$item_arrow_wood"] = "other" }.TryGetValue(t, out var k) ? k : null,
        PieceKind = t => t == "$piece_lowerground" ? "ground" : t == "$piece_sapling_carrot" ? "planted" : t == "$piece_feast_meadows" ? "feast" : null,
        EnemyKills = new Dictionary<string, float> { ["$enemy_greydwarf"] = 410, ["$enemy_troll"] = 12, ["$enemy_draugr"] = 96 },
        SkillLevels = new Dictionary<string, float> { ["Axes"] = 38.4f, ["Blocking"] = 42.7f, ["Run"] = 55f, ["Swords"] = 21f, ["Cooking"] = 18f },
        Session = tally, Events = ev, Log = log,
        PlayerNames = new Dictionary<long, string> { [42] = "Edda" },
        // what Localization returns in game for these prefabs (English)
        DisplayName = p => new Dictionary<string, string> { ["BlobElite"] = "Oozer", ["SwordIron"] = "Iron sword", ["ArmorIronChest"] = "Iron scale mail",
            ["FishWraps"] = "Fish wraps", ["SerpentStew"] = "Serpent stew", ["Greyling"] = "Greyling" }.TryGetValue(p, out var n) ? n : null,
    };
}

var input = Sample();
PanelView Show(PanelInput i, Chapter c, string page = null, Action<PanelState> more = null)
{
    var st = new PanelState { Chapter = c }; if (page != null) st.Page[c] = page; more?.Invoke(st);
    return PanelModel.Build(i, st);
}
Block Find(PanelView v, string kind, Func<Block, bool> where = null) => v.Blocks.FirstOrDefault(b => b.Kind == kind && (where == null || where(b)));

// ---------- the measured event log: windows, biomes, deaths, hints ----------
var all = PanelModel.Damage(input.Log, TimeWindow.Session, "", now);
var hour = PanelModel.Damage(input.Log, TimeWindow.LastHour, "", now);
var three = PanelModel.Damage(input.Log, TimeWindow.LastThreeHours, "", now);
Check(all.Any(r => r.Biome == "Meadows") && !three.Any(r => r.Biome == "Meadows") && three.Any(r => r.Biome == "BlackForest"), "window: 3.5 h ago only in the whole session, 2 h ago in the last three hours");
Check(hour.All(r => r.Biome == "Swamp") && hour.Count > 0, "window: last hour keeps only the Swamp fight");
var later = now.AddMinutes(5);   // window starts 21:35, inside the 21:30-21:40 bucket
var edge = new EventLog(); edge.AddDamage(new DateTime(2026, 10, 7, 21, 31, 0, DateTimeKind.Utc), "Plains", true, "Deathsquito", "Bows", D("pierce", 5));
Check(PanelModel.Damage(edge, TimeWindow.LastHour, "", later).Count == 1, "window: a 10-minute bucket that reaches into the window counts");
edge = new EventLog(); edge.AddDamage(new DateTime(2026, 10, 7, 21, 28, 0, DateTimeKind.Utc), "Plains", true, "Deathsquito", "Bows", D("pierce", 5));
Check(PanelModel.Damage(edge, TimeWindow.LastHour, "", later).Count == 0, "window: a bucket that ended before the window does not");
var deaths = PanelModel.Deaths(input.Log, TimeWindow.Session, "", now);
Check(PanelModel.Deaths(input.Log, TimeWindow.LastHour, "", now).Count == 2 && deaths[0].Time > deaths[1].Time, "deaths: window filter, latest first");
var killers = PanelModel.MostCommonKiller(deaths);
Check(killers["Swamp"] == "Blob" && killers["BlackForest"] == "Troll", "most common killer per biome");
var hints = PanelModel.Hints(all, deaths);
Check(hints.Count > 0 && hints[0] == "Swamp: poison was behind both deaths there. Bring Poison resistance mead.", "hint: poison falls in the Swamp ask for Poison resistance mead");
Check(!hints.Any(h => h.StartsWith("Black Forest")), "hint: no advice for a blunt Troll fall (no remedy guess)");

// ---------- the menu: six chapters, left lists, one owner per metric ----------
var deeds = Show(input, Chapter.Deeds);
Check(deeds.Chapters.Select(c => c.Label).SequenceEqual(new[] { "Deeds", "Company", "Stores", "Battle", "Voyages", "Skills" }) && deeds.Chapters[0].Selected,
      "menu: six chapters in the agreed order, Deeds first");
Check(deeds.List.Select(l => l.Label).SequenceEqual(new[] { "Overview", "Cooking", "Building", "Crafting", "Woodcutting", "Mining", "Farming", "Fishing", "Taming" }) && deeds.Page == "overview",
      "menu: Deeds left list per the menu tree, Overview first");
var grid = Find(deeds, "titles");
Check(grid != null && grid.Items.Single(t => t.Title == "Shieldbearer").Id == "Battle/defense" && grid.Items.Single(t => t.Title == "Helmskeeper").Id == "Voyages/sailing" &&
      grid.Items.Single(t => t.Title == "Hearth Cook").Id == "Deeds/cooking", "overview: each title is a shortcut to its owner page (blocking in Battle, sailing in Voyages)");
var jump = new PanelState(); PanelModel.Jump(jump, "Battle/defense");
var defense = PanelModel.Build(input, jump);
Check(defense.Active == Chapter.Battle && defense.Page == "defense" && defense.Badges.Select(b => b.Label).SequenceEqual(new[] { "Wallwarden", "Shieldbearer" }),
      "overview: the jump lands on Battle > Defense with its titles in the header");
var blocksStat = Find(defense, "stat", b => b.Title == "successful blocks");
Check(blocksStat != null && blocksStat.Value == "55" && blocksStat.Text == "including 27 parries", "defense: blocks phrased inclusively (55 blocks, including 27 parries)");
Check(Find(defense, "rows", b => b.Items.Any(i => i.Title == "Defences built" && i.Value == "40")) != null, "defense: base defences live in Battle > Defense, from the character record");
// one owner per number: walk every page of every chapter (both Company directions) and note where each number shows up
var pages = new List<(string where, PanelView v)>();
foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
    foreach (var l in PanelModel.Build(input, new PanelState { Chapter = ch }).List)
        foreach (var they in new[] { true, false })
            pages.Add((ch + "/" + l.Id, PanelModel.Build(input, new PanelState { Chapter = ch, Page = { [ch] = l.Id }, TheyReceived = they })));
IEnumerable<string> WhereShown(Func<string, bool> has) => pages.Where(p => PanelModel.AllText(p.v).Any(has)).Select(p => p.where).Distinct();
Check(WhereShown(t => t.Contains("55 block") || t.Contains("successful block") || t.Contains("27 parr")).SequenceEqual(new[] { "Battle/defense" }),
      "owner: blocks and parries show on Battle > Defense only (not Battle > Overview, not Deeds > Overview): " + string.Join(", ", WhereShown(t => t.Contains("27 parr"))));
Check(WhereShown(t => t.Contains("at the helm") && t.Any(char.IsDigit)).SequenceEqual(new[] { "Voyages/sailing" }), "owner: helm distance shows on Voyages > Sailing only");
Check(Find(deeds, "titles").Items.All(t => !t.Value.Any(char.IsDigit) && string.IsNullOrEmpty(t.Note)), "owner: Deeds > Overview titles are shortcuts without numbers");
var battleOverview = Show(input, Chapter.Battle);
Check(Find(battleOverview, "link")?.Id == "Battle/defense" && !PanelModel.AllText(battleOverview).Any(t => t.Contains("27 parr") || t.Contains("55")), "owner: Battle > Overview links to Defense instead of repeating the count");
var sailing = Show(input, Chapter.Voyages);
Check(sailing.Page == "sailing" && sailing.Heading == "" + "Sailing" && Find(sailing, "stat", b => b.Title == "6.6 km at the helm") != null && sailing.Badges.Any(b => b.Label == "Helmskeeper"),
      "owner: the helm lives in Voyages > Sailing with the Helmskeeper title");
var stocked = Show(input, Chapter.Stores);
Check(stocked.List.Select(l => l.Label).SequenceEqual(new[] { "Stocked", "Taken", "Carts", "Trader" }) && Find(stocked, "empty").Title == PanelModel.ChestsOnServer,
      "stores: chest stocking says the records live on the server");
var trader = Show(input, Chapter.Stores, "trader");
Check(trader.Heading == "350 coins spent" && Find(trader, "tiles").Items.Select(t => t.Icon).SequenceEqual(new[] { "item:YmirRemains", "item:BeltStrength" }) && trader.Badges.Any(b => b.Label == "Far Trader"),
      "stores: trader purchases as item tiles with the game's sprites, from this PC");
Check(Show(input, Chapter.Stores, "carts").Heading == "840 m pulling a cart", "stores: carts from this PC");

// ---------- Deeds pages ----------
var cookingAlone = Show(input, Chapter.Deeds, "cooking");
Check(cookingAlone.Heading == "166 dishes cooked or grilled" && cookingAlone.Badges.Any(b => b.Label == "Hearth Cook"), "cooking: without fellow records, the profile count leads");
var crafting = Show(input, Chapter.Deeds, "crafting");
Check(Find(crafting, "tiles").Items.Select(t => t.Icon + "=" + t.Value).SequenceEqual(new[] { "item:CopperOre=30" }), "crafting: ore into smelters as tiles (fuel is not an item)");
Check(Find(crafting, "rows").Note == PanelModel.SourceCharacter, "crafting: crafted items labelled since this character was made");

// ---------- B6: Crafting counts gear, food is Cooking (the game's own m_itemType switch) ----------
Check(PanelModel.ItemKindOf("Consumable", "$item_carrotsoup") == "food" && PanelModel.ItemKindOf("Consumable", "$item_fishingbait_forest") == "other" &&
      PanelModel.ItemKindOf("TwoHandedWeaponLeft", "$item_x") == "gear" && PanelModel.ItemKindOf("Shield", "$item_x") == "gear" && PanelModel.ItemKindOf("Utility", "$item_x") == "gear" &&
      PanelModel.ItemKindOf("Trinket", "$item_x") == "gear" && PanelModel.ItemKindOf("Ammo", "$item_x") == "other" && PanelModel.ItemKindOf("Material", "$item_x") == "other" && PanelModel.ItemKindOf(null, "$item_x") == null,
      "B6 classify: food = consumable without bait; gear = weapons, shields, armour, tools, utility, trinkets (the game's craft switch)");
var gearRows = crafting.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Gear made most")).Skip(1).First();
Check(gearRows.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Shield wood=7", "Axe bronze=6", "Sword iron=2" }),
      "B6 crafting: 'Gear made most' lists gear only: " + string.Join(", ", gearRows.Items.Select(i => i.Title + "=" + i.Value)));
Check(!PanelModel.AllText(crafting).Any(t => t.Contains("Carrot") || t.Contains("Bread")), "B6 crafting: no food on the Crafting page");
Check(Find(crafting, "stat", b => b.Title == "19 pieces of gear made") != null && Find(crafting, "stat", b => b.Title == "11 upgrades") != null,
      "B6 crafting: the title line counts the same gear as the list (weapons 9 + armour 6 + tools 4) and upgrades on their own line");
var alsoMade = crafting.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title.StartsWith("Also crafted (per craft"))).Skip(1).FirstOrDefault();
Check(alsoMade != null && alsoMade.Items.Select(i => i.Title).SequenceEqual(new[] { "Arrow wood", "Modthing" }), "B6 crafting: ammo and items the game data does not know go under 'Also made', never under gear");
var cookedRows = cookingAlone.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Cooked and brewed most")).Skip(1).FirstOrDefault();
Check(cookedRows != null && cookedRows.Items.First().Title == "Carrotsoup" && cookedRows.Items.First().Value == "118" && cookedRows.Source == "character",
      "B6 cooking: the food from ItemsCrafted is under Cooking, as a game counter");

// ---------- B8: Building is build pieces; groundwork, plantings and feasts have their own place ----------
Check(PanelModel.PieceKindOf(true, false, false) == "ground" && PanelModel.PieceKindOf(false, true, false) == "planted" && PanelModel.PieceKindOf(false, false, true) == "feast" && PanelModel.PieceKindOf(false, false, false) == "built",
      "B8 classify: TerrainOp = groundwork, Plant = planted, Feast = feast, anything else built");
var building = Show(input, Chapter.Deeds, "building");
var built = building.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Pieces built most")).Skip(1).First();
var ground = building.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Groundwork")).Skip(1).First();
Check(built.Items.Select(i => i.Value).SequenceEqual(new[] { "520", "310", "36" }) && ground.Items.Select(i => i.Value).SequenceEqual(new[] { "850", "133", "40" }),
      "B8 building: built pieces and groundwork (levelled, lowered, paved) are separate lists");
Check(Find(building, "stat", b => b.Title == "866 pieces built") != null && building.Badges.Any(b => b.Label == "Hallwright"),
      "B8 building: a title line with the built count, the same pieces as the list");
Check(!PanelModel.AllText(building).Any(t => t.Contains("Sapling") || t.Contains("Feast")), "B8 building: plantings and feasts are not building");
var farming = Show(input, Chapter.Deeds, "farming");
Check(farming.Blocks.Any(b => b.Kind == "section" && b.Title == "Planted most"), "B8 farming: plantings live on Farming");
Check(Find(cookingAlone, "stat", b => b.Title == "feasts set out")?.Value == "2", "B8 cooking: feasts set out live on Cooking");

// ---------- C1/C2: what came from trees and rocks, per kind ----------
var wood = Show(input, Chapter.Deeds, "woodcutting");
var woodRows = wood.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Wood picked up, at least")).Skip(1).First();
Check(woodRows.Items.Select(i => i.Icon + "=" + i.Value).SequenceEqual(new[] { "item:$item_wood=2,400", "item:$item_roundlog=520", "item:$item_finewood=310", "item:$item_elderbark=75" }) && woodRows.Source == "character",
      "C2 woodcutting: wood types picked up per kind, a game counter");
var mining = Show(input, Chapter.Deeds, "mining");
var oreRows = mining.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Stone and ore picked up, at least")).Skip(1).First();
Check(oreRows.Items.Select(i => i.Icon + "=" + i.Value).SequenceEqual(new[] { "item:$item_stone=1,800", "item:$item_copperore=240", "item:$item_tinore=90" }) && !PanelModel.AllText(mining).Any(t => t.Contains("aspberr")),
      "C1 mining: stone and ore picked up per kind, berries left out");
var dataWood = new PanelInput { ItemsPickedUp = new Dictionary<string, float> { ["$item_modlog"] = 9, ["$item_wood"] = 3 }, GatherKind = t => t == "$item_modlog" ? "wood" : null };
Check(PanelModel.Build(dataWood, new PanelState { Page = { [Chapter.Deeds] = "woodcutting" } }).Blocks.Any(b => b.Kind == "rows" && b.Items.Any(i => i.Icon == "item:$item_modlog")),
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
var woodEx = Show(input, Chapter.Deeds, "woodcutting");
var woodExRows = woodEx.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Wood picked up this session")).Skip(1).First();
Check(woodExRows.Items.Select(i => i.Title + "=" + i.Value + "/" + i.Source + "/" + (i.Items?.Single().Value ?? "-")).SequenceEqual(new[]
      { "Wood=140/measured/2,400", "Finewood=12/measured/310", "Roundlog=/character/520", "Elderbark=/character/75" }) && woodExRows.Source == "measured" &&
      woodExRows.Items[0].Text == "at least 2,400 since this character was made" && woodExRows.Items[0].Items[0].Source == "character" && !PanelModel.AllText(woodEx).Any(t => t.Contains("Resin")),
      "C7 woodcutting: one row per kind, the exact count this session as the main number, the game's floor beside it (tagged character); resin is not wood");
var miningEx = Show(input, Chapter.Deeds, "mining");
var oreEx = miningEx.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Stone and ore picked up this session")).Skip(1).First();
Check(oreEx.Items.Select(i => i.Title + "=" + i.Value).Take(3).SequenceEqual(new[] { "Copperore=18", "Silverore=4", "Stone=" }) && oreEx.Items[1].Text == null,
      "C7 mining: a kind only measured has no floor line; a kind only in the game's count has no exact number");

// ---------- titles: one table ----------
var titles = PanelModel.Titles(input);
string Line(List<PanelModel.TitleRow> ts, string title, int i) => ts.Single(t => t.Title == title).Lines[i].Key;
string Src(List<PanelModel.TitleRow> ts, string title, int i) => ts.Single(t => t.Title == title).Lines[i].Value;
Check(Line(titles, "Trailfinder", 0) == "184 km travelled" && Src(titles, "Trailfinder", 0) == PanelModel.SourceCharacter, "source: a lifetime counter says since this character was made");
Check(Line(titles, "Woodcutter", 0) == "64 axe hits on trees and logs" && Src(titles, "Woodcutter", 0) == PanelModel.SourceSession &&
      Line(titles, "Woodcutter", 1) == "410 trees felled" && Src(titles, "Woodcutter", 1) == PanelModel.SourceCharacter, "titles: several lines, each with its own source");
Check(Line(titles, "Mapmaker", 0) == "Map shared 2 times at the table" && Line(titles, "Mender", 0) == "9 repairs with the hammer" &&
      Line(titles, "Wallwarden", 0) == "42 defences built, traps armed or turrets loaded" && Line(titles, "Bossbane", 0) == "3 boss fights won",
      "titles: the five new titles read the new measures and profile counters");
Check(!titles.Any(t => t.Title == "Tidecatcher" || t.Title == "Beastkeeper" || t.Title == "Waymate"), "titles: zero counters earn no title, and no Waymate");
Check(PanelModel.SagaTitles.Single(t => t.Title == "Hallwright").Descriptor == "Pieces raised with the hammer" && PanelModel.SagaTitles.Single(t => t.Title == "Tidecatcher").Descriptor == "Fish caught on the line" &&
      PanelModel.SagaTitles.Single(t => t.Title == "Storekeeper").Descriptor == "Supplies stowed in chests and carts" && PanelModel.SagaTitles.Single(t => t.Title == "Beastkeeper").Descriptor == "Creatures petted, named and led",
      "titles: descriptors from the naming review");

// ---------- Battle ----------
var battle = Show(input, Chapter.Battle, null, s => s.Biome = "Swamp");
var dmgPage = Show(input, Chapter.Battle, "damage", s => s.Biome = "Swamp");
var bars = Find(dmgPage, "bars");
Check(battle.Heading == "434 damage dealt, 266 received" && battle.Scope == "Swamp combat · all enemies · this session", "battle: one scope per screen, stated in the header");
var hitRows = battle.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Hits")).Skip(1).First();
Check(hitRows.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Hits on foes=5,400", "Hits on other players=3", "Hits received from foes=1,300" }) && hitRows.Source == "character",
      "C3 battle overview: retroactive hit counts, labelled as game counters");
var byBiome = battle.Blocks.Where(b => b.Kind == "bars").ToList();
Check(byBiome.Count == 2 && byBiome[0].Items.Select(i => i.Title).SequenceEqual(new[] { "Swamp", "Black Forest", "Meadows" }) && byBiome[0].Items.Single(i => i.Selected).Title == "Swamp" &&
      byBiome[1].Items.Select(i => i.Title).SequenceEqual(new[] { "Black Forest", "Swamp" }) && byBiome.All(b => b.Source == "measured"),
      "C3 battle overview: measured damage dealt and received, split by biome (every biome, the chosen one lit)");
Check(!PanelModel.AllText(battle).Concat(PanelModel.AllText(dmgPage)).Any(t => t.IndexOf("dealt after", StringComparison.OrdinalIgnoreCase) >= 0), "C3: no dealt-after-resistance number (owner trap, see FEEDBACK C3)");
Check(bars.Items.Select(b => b.Title).SequenceEqual(new[] { "Slash", "Poison" }) && bars.Value == "150" && Math.Abs(bars.Items[1].Fraction - 122f / 150f) < 1e-4 && Math.Abs(bars.Items[0].Fraction - 144f / 150f) < 1e-4 && bars.Items[1].Icon == "status:poison" && bars.Items[0].Icon == "",
      "battle: bars on one zero-based round scale (0 to 150) from the numbers; poison carries the game's status icon, slash none");
Check(PanelModel.ScrollRoom(508, 500) == 0 && PanelModel.ScrollRoom(511.9f, 500) == 0 && PanelModel.ScrollRoom(512, 500) == 12 && PanelModel.ScrollRoom(400, 500) == 0,
      "B9 scrolling: under 12 px of overflow there is no scroll range (no jump on the next notch, no fade)");
Check(PanelModel.NiceMax(580) == 600 && PanelModel.NiceMax(1410) == 1500 && PanelModel.NiceMax(100) == 100 && PanelModel.NiceMax(7) == 8, "bars: the scale ends on a round number (580 -> 600, as in the kit's assembly)");
Check(battle.HasFilters && battle.Windows.Select(w => w.Label).SequenceEqual(new[] { "Last hour", "Last three hours", "This session" }), "battle: time window choices say session, not gathering");
Check(Find(battle, "stat", b => b.Title == "deaths from poison")?.Value == "2", "battle: deaths grouped by damage type, not pinned on an attacker");
Check(Find(battle, "note", b => b.Tone == "hint") != null, "battle: the resistance hint is on the overview");
var foes = Show(input, Chapter.Battle, "foes");
Check(foes.Heading == "860 foes defeated" && Find(foes, "rows").Items[0].Title == "Greydwarf" && !foes.HasFilters, "foes: lifetime kills per kind; lifetime pages have no time filter");
var deathsPage = Show(input, Chapter.Battle, "deaths");
Check(deathsPage.Heading == "3 deaths" && Find(deathsPage, "stat").Title == "Poison · Blob" && Find(deathsPage, "stat").Text == "Swamp · 8 Oct 00:18", "deaths: listed with local date and time");
Check(PanelModel.Build(new PanelInput(), new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = "deaths" } }).Heading == PanelModel.NoDeaths, "deaths: none says No deaths recorded");

// ---------- Skills ----------
var skillsOverview = Show(input, Chapter.Skills);
Check(skillsOverview.List.Select(l => l.Id).SequenceEqual(new[] { "overview", "Axes", "Blocking", "Cooking", "Run", "Swords" }) && skillsOverview.List.Skip(1).All(l => l.Icon == "skill:" + l.Id) && skillsOverview.Page == "overview",
      "skills: Overview first, then one row per skill with the game's skill icon");
var highest = skillsOverview.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Levels now")).Skip(1).First();
var practised = skillsOverview.Blocks.SkipWhile(b => !(b.Kind == "section" && b.Title == "Practised most this session")).Skip(1).First();
Check(highest.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Run=55", "Blocking=42", "Axes=38", "Swords=21", "Cooking=18" }) && highest.Source == "character" &&
      practised.Items.First().Title == "Axes" && practised.Items.First().Value == "14.5" && practised.Source == "measured",
      "C4 skills overview: highest skills (the game's levels) and practised most this session (measured)");
var many = new PanelInput { SkillLevels = Enumerable.Range(1, 12).ToDictionary(n => "Skill" + n, n => (float)n) };
Check(PanelModel.Build(many, new PanelState { Chapter = Chapter.Skills }).Blocks.Single(b => b.Kind == "rows").Items.Count == 12, "C4 skills overview: every skill is listed, not only the top eight");
var skills = Show(input, Chapter.Skills, "Axes");
Check(Find(skills, "stat", b => b.Title == "level").Value == "38" && Find(skills, "stat", b => b.Title == "practice").Value == "14.5", "skills: level now and practice this session");
var unpractised = new PanelInput { SkillLevels = new Dictionary<string, float> { ["Bows"] = 12f }, Events = new SessionEvents() };
Check(Find(PanelModel.Build(unpractised, new PanelState { Chapter = Chapter.Skills, Page = { [Chapter.Skills] = "Bows" } }), "stat", b => b.Title == "practice") == null, "skills: no practice line when there was none (no '0 practice')");
var colours = PanelModel.PersonColors(new[] { "Rowan", "Edda", "Tor", "Finch", "Asa", "Bo", "Gunn", "Ylva", "edda" });
Check(colours.Count == 8 && colours.Values.Distinct().Count() == 8 && Show(input, Chapter.Company).PersonColors["Edda"] != Show(input, Chapter.Company).PersonColors["Tor"], "people: eight players get eight different colours, stable by name");

// ---------- Company ----------
var company = Show(input, Chapter.Company);
Check(company.List.Select(l => l.Label).SequenceEqual(new[] { "Edda", "Finch", "Tor" }) && company.Page == "Edda", "company: companions alphabetical, equal rows, you excluded");
Check(company.Toggle.Select(t => t.Label).SequenceEqual(new[] { "They enjoyed your food", "You enjoyed their food" }) && company.Toggle[0].Selected, "company: literal direction toggle, their side first, in grateful-use wording");
Check(Find(company, "empty").Title == "No record from Edda yet.", "company: their side without their shared record says so");
var mine = Show(input, Chapter.Company, "Edda", s => s.TheyReceived = false);
var thread = Find(mine, "thread");
Check(thread.Title == "Food Edda made" && thread.Text == "You enjoyed" && thread.Icon == "person:Rowan" && thread.Items.Select(i => i.Title + " " + i.Value).SequenceEqual(new[] { "Bread 3", "Fish wraps 2" }), "company: your side, maker to eater with item and count");
Check(thread.Note == PanelModel.SourceSession && mine.Scope == "Rowan and Edda · this session, since 20:54", "company: your side is scoped and labelled as your session");
Check(Find(mine, "stat", b => b.Title == "Edda held the helm for about 15 minutes") != null && Find(mine, "stat", b => b.Title == "Sailed together for about 21 minutes") != null, "company: helm and voyages as about-minutes");
var tor = Show(input, Chapter.Company, "Tor", s => s.TheyReceived = false);
Check(Find(tor, "tiles").Items.Select(i => i.Title + "|" + i.Value).SequenceEqual(new[] { "Iron scale mail|", "Iron sword|" }), "company: gear shown by name, not equip counts");
Check(company.Keys.Contains("[A/D] Direction") && !deeds.Keys.Contains("[A/D] Direction") && deeds.Keys.Contains("[W/S] List") && deeds.Keys.Last() == "[H/Esc] Close",
      "keys: the footer lists only bindings that work on this screen");
Check(deeds.Keys.Contains("[T] About") && !deeds.Keys.Any(k => k.StartsWith("[I]")), "ISC-1 keys: the info key is T (I opens the AdventureBackpacks backpack)");
// Joost 2026-10-08: where a number comes from is not interesting to most players; T opens one About page instead of a line per page
var about = Show(input, Chapter.Battle, "damage", s => s.ShowAbout = true);
Check(about.Heading == "About Hearthwoven" && about.Blocks.Select(b => b.Kind + ":" + b.Title).SequenceEqual(new[] { "stat:The game counts", "stat:Hearthwoven measures", "stat:Fellow players share", "stat:Titles", "stat:Your world" }) &&
      about.Blocks.All(b => !string.IsNullOrEmpty(b.Text)) && !about.HasFilters && about.Badges.Count == 0,
      "ISC-6 About: T shows one page of labelled rows (game counts, Hearthwoven measures, fellows share, titles, your world)");
Check(about.Keys.SequenceEqual(new[] { "[Q/E] Chapter", "[W/S] List", "[T/Esc] Back", "[H] Close" }), "ISC-6 About: T or Esc goes back, H closes: " + string.Join("  ", about.Keys));
Check(PanelModel.AllText(about).Any(t => t.Contains("ShareWithGroup")) && PanelModel.AllText(about).Any(t => t.Contains("remove Hearthwoven.dll")),
      "ISC-6 About: says sharing is two-way and can be switched off, and how to uninstall");
var crowd = new PanelInput { PlayerName = "Rowan", Events = new SessionEvents() };
foreach (var n in new[] { "Gunn", "Asa", "Bo" }) SessionEvents.Add(crowd.Events.SailedWith, n, 60);
Check(PanelModel.Build(crowd, new PanelState { Chapter = Chapter.Company }).List.Select(l => l.Id).SequenceEqual(new[] { "Asa", "Bo", "Gunn" }), "company: order is by name, never by amount");

// ---------- navigation ----------
var nav = new PanelState();
PanelModel.StepChapter(nav, -1);
Check(nav.Chapter == Chapter.Skills, "keys: Q from the first chapter wraps to the last");
nav = new PanelState(); var v0 = PanelModel.Build(input, nav); PanelModel.StepList(nav, v0, -1);
Check(nav.PageOf(Chapter.Deeds) == "taming", "keys: W from the top of the list wraps to the bottom");
PanelModel.StepList(nav, PanelModel.Build(input, nav), 1);
Check(nav.PageOf(Chapter.Deeds) == "overview", "keys: S from the bottom wraps to the top");

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
Check(Line(eddaTitles, "Trailfinder", 0) == "52.3 km travelled" && Src(eddaTitles, "Shieldbearer", 0) == "measured in their last session", "snapshot: their values keep their source labels");
foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
{
    var a = PanelModel.Build(input, new PanelState { Chapter = ch }); var b = PanelModel.Build(edda, new PanelState { Chapter = ch });
    Check(a.Chapters.Select(c => c.Label).SequenceEqual(b.Chapters.Select(c => c.Label)) && b.Blocks.Count > 0, "snapshot: chapter " + ch + " renders for a fellow player");
}
var eddaBattle = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Battle });
Check(eddaBattle.Scope == "All biomes · all enemies · this session · Edda, their last session, 8 Oct 00:05" && Find(eddaBattle, "stat", b => b.Title == "death from frost") != null,
      "snapshot: the Battle scope says whose copy and when it is from; deaths without positions still count by type");
var oldCopy = PanelInput.FromSnapshot(eddaJson); oldCopy.NowUtc = now.AddDays(3); oldCopy.ToLocal = t => t.AddHours(2);
var stale = PanelModel.Build(oldCopy, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.LastHour });
Check(stale.Heading == "Edda's last record is from 8 Oct 00:05" && !PanelModel.AllText(stale).Contains(PanelModel.NoDeaths) && !PanelModel.AllText(stale).Contains("No damage received"),
      "snapshot: a window after an old copy says when the copy is from, not that nothing happened");
var eddaCompany = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Company });
Check(eddaCompany.List.Select(l => l.Label).SequenceEqual(new[] { "Rowan (you)", "Tor" }), "snapshot: in their company you are marked, they are not their own company");
edda.Fellows = new List<PanelInput> { input }; input.Fellows = new List<PanelInput> { edda };
var eddaSide = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Company });
var eddaThread = Find(eddaSide, "thread");
Check(eddaSide.Toggle[0].Label == "You enjoyed Edda's food" && eddaThread.Title == "Food Edda made" && eddaThread.Text == "You enjoyed" && eddaThread.Icon == "person:Rowan" && eddaThread.Items.Select(i => i.Title + " " + i.Value).SequenceEqual(new[] { "Bread 3", "Fish wraps 2" }),
      "both ways: Edda's side shows what you ate of hers, from your record");
var rowanSide = PanelModel.Build(input, new PanelState { Chapter = Chapter.Company });
Check(Find(rowanSide, "thread").Items.Single().Title == "Queens Jam" && Find(rowanSide, "thread").Items.Single().Value == "2", "both ways: your side shows what Edda ate of yours, from her shared record");
Check(rowanSide.Scope == "Rowan and Edda · Edda's last session, 8 Oct 00:05" && Find(rowanSide, "thread").Note == PanelModel.SourceFellows,
      "both ways: what she ate is scoped to her last session (with date) and labelled measured on their PCs");
Check(Find(eddaSide, "thread").Note == "measured on your PC" && eddaSide.Scope == "Edda and Rowan · this session, since 20:54", "both ways: on Edda's page, what you ate of hers is labelled as measured on your PC");
Check(Find(rowanSide, "tiles").Items.Single().Icon == "item:AxeBronze", "both ways: gear she equipped that you made");
var cooking = Show(input, Chapter.Deeds, "cooking");
Check(cooking.Heading == "2 meals enjoyed by companions" && Find(cooking, "rows").Items.Single().Title == "Edda" && Find(cooking, "rows").Note == PanelModel.SourceFellows,
      "cooking: meals enjoyed by companions, who enjoyed them (by name), labelled measured on their PCs");
Check(PanelModel.Titles(input).Single(t => t.Title == "Hearth Cook").Lines[0].Key == "2 times someone enjoyed food you made", "wording: Hearth Cook says someone enjoyed food you made");
Check(PanelInput.FromSnapshot("not json") == null && PanelInput.FromSnapshot("") == null, "snapshot: unreadable JSON gives no input, no exception");
var bare = PanelInput.FromSnapshot("{\"name\":\"Finch\"}");
Check(bare != null && Find(PanelModel.Build(bare, new PanelState()), "empty").Text == "Finch's deeds will appear here as they are recorded.", "snapshot: a snapshot with nothing in it shows empty states");

// ---------- the player switcher ----------
var sw = new PanelView();
PanelModel.AddPlayers(sw, "Rowan", new[] { "Tor", "edda", "Rowan", "Edda", "" }, "", true);
Check(sw.Players.Select(p => p.Label).SequenceEqual(new[] { "Rowan", "edda", "Tor" }) && sw.Players[0].Selected && sw.ShareNote == null, "switcher: you first and selected, then fellow players by name");
PanelModel.AddPlayers(sw, "Rowan", new[] { "Tor" }, "Gone", true);
Check(sw.Players[0].Selected, "switcher: a player no longer shared falls back to you");
PanelModel.AddPlayers(sw, "Rowan", new string[0], "", true);
Check(sw.Players.Count == 1 && sw.ShareNote == PanelModel.ShareWaitingNote, "switcher: sharing on but nobody else yet says so");
PanelModel.AddPlayers(sw, "Rowan", new[] { "Tor" }, "Tor", false);
Check(sw.Players.Count == 0 && sw.ShareNote == "You keep your stats to yourself, so you see only your own. Turn on ShareWithGroup in the mod settings to see your fellow players.",
      "switcher: sharing off hides the switcher and says how to turn it on");

// ---------- empty states, copy ----------
var empty = new PanelInput();
var emptyViews = Enum.GetValues(typeof(Chapter)).Cast<Chapter>().Select(c => PanelModel.Build(empty, new PanelState { Chapter = c })).ToList();
Check(emptyViews.All(v => v.Blocks.Count > 0) && Find(emptyViews[1], "empty").Text == PanelModel.CompanyEmpty, "empty: every chapter survives no data; Company uses the agreed empty text");
Check(PanelModel.Build(null, null).Chapters.Count == 6, "empty: null input and state do not throw");
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
        if (b.Kind != "bars" && ((b.Value ?? "").Any(char.IsDigit) || (b.Kind != "section" && (b.Title ?? "").Any(char.IsDigit))) && src == null) untagged ??= v.Active + "/" + v.Page + ": " + (b.Title ?? b.Value);
        foreach (var i in b.Items ?? new List<Block>()) Walk(i, src);
    }
    foreach (var b in v.Blocks) Walk(b, null);
}
Check(untagged == null, "ISC-A-2 source tags: every number has a source tag" + (untagged != null ? ": " + untagged : ""));
var tags = views.SelectMany(v => v.Blocks).Select(b => b.Source).Where(t => t != null).Distinct().OrderBy(t => t).ToList();
Check(tags.SequenceEqual(new[] { "character", "fellows", "measured" }), "ISC-6 source tags: only character, fellows, measured: " + string.Join(",", tags));
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

var dump = Array.IndexOf(args, "--dump");
if (dump >= 0 && dump + 1 < args.Length)
{
    var dir = args[dump + 1];
    System.IO.Directory.CreateDirectory(dir);
    input.Fellows = new List<PanelInput> { edda }; edda.Fellows = new List<PanelInput> { input };   // as PanelUi wires the group
    var group = new[] { "Edda", "Tor", "Finch" };
    var shots = new (string name, PanelInput inp, PanelState st, bool sharing)[]
    {
        ("deeds", input, new PanelState { Chapter = Chapter.Deeds }, true),
        ("deeds-cooking", input, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = "cooking" } }, true),
        ("company", input, new PanelState { Chapter = Chapter.Company }, true),
        ("company-you-ate", input, new PanelState { Chapter = Chapter.Company, TheyReceived = false }, true),
        ("stores", input, new PanelState { Chapter = Chapter.Stores }, true),
        ("stores-trader", input, new PanelState { Chapter = Chapter.Stores, Page = { [Chapter.Stores] = "trader" } }, true),
        ("battle", input, new PanelState { Chapter = Chapter.Battle, Biome = "Swamp" }, true),
        ("battle-deaths", input, new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = "deaths" } }, true),
        ("about", input, new PanelState { ShowAbout = true }, true),
        ("voyages", input, new PanelState { Chapter = Chapter.Voyages }, true),
        ("skills", input, new PanelState { Chapter = Chapter.Skills }, true),
        ("edda-company", edda, new PanelState { Chapter = Chapter.Company, Player = "Edda" }, true),
        ("sharing-off", input, new PanelState { Chapter = Chapter.Deeds }, false),
    };
    var js = "// Generated by test-panel --dump from PanelModel.Build on a sample evening (fictional players). Do not edit.\nwindow.PANEL_DATA = {\n" +
             string.Join(",\n", shots.Select(s =>
             {
                 var keep = s.inp.Fellows;
                 if (!s.sharing) s.inp.Fellows = null;   // sharing off: PanelUi passes no fellow players
                 var v = PanelModel.Build(s.inp, s.st);
                 s.inp.Fellows = keep;
                 PanelModel.AddPlayers(v, "Rowan", group, s.st.Player, s.sharing);
                 return "  \"" + s.name + "\": " + PanelModel.ToJson(v);
             })) + "\n};\n";
    System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "preview-data.js"), js);
    System.Console.WriteLine("wrote " + System.IO.Path.Combine(dir, "preview-data.js"));
}

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
