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
        },
        ItemsCrafted = new Dictionary<string, float> { ["$item_bread"] = 40, ["$item_fishwraps"] = 22, ["$item_sword_iron"] = 2 },
        PiecesPlaced = new Dictionary<string, float> { ["$piece_woodwall"] = 520, ["$piece_woodfloor"] = 310, ["$piece_sharpstakes"] = 36 },
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
Check(!Enumerable.Range(1, 8).Select(i => Show(input, Chapter.Deeds, deeds.List[i].Id)).SelectMany(PanelModel.AllText).Any(t => t.Contains("block") || t.Contains("helm")),
      "owner: no blocking or helm numbers on any Deeds page");
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
Check(Find(crafting, "rows").Items.First().Icon == "item:$item_bread" && Find(crafting, "rows").Note == PanelModel.SourceCharacter, "crafting: crafted items by the game's token, labelled since this character was made");

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
var bars = Find(battle, "bars");
Check(battle.Heading == "266 damage points received" && battle.Scope == "Swamp combat · all enemies · this session", "battle: one scope per screen, stated in the header");
Check(bars.Items.Select(b => b.Title).SequenceEqual(new[] { "Slash", "Poison" }) && Math.Abs(bars.Items[1].Fraction - 122f / 144f) < 1e-4 && bars.Items[1].Icon == "status:poison" && bars.Items[0].Icon == "",
      "battle: bars on one scale from the numbers; poison carries the game's status icon, slash none");
Check(battle.HasFilters && battle.Windows.Select(w => w.Label).SequenceEqual(new[] { "Last hour", "Last three hours", "This session" }), "battle: time window choices say session, not gathering");
Check(Find(battle, "stat", b => b.Title == "deaths from poison")?.Value == "2", "battle: deaths grouped by damage type, not pinned on an attacker");
Check(Find(battle, "note", b => b.Tone == "hint") != null, "battle: the resistance hint is on the overview");
var foes = Show(input, Chapter.Battle, "foes");
Check(foes.Heading == "860 foes defeated" && Find(foes, "rows").Items[0].Title == "Greydwarf" && !foes.HasFilters, "foes: lifetime kills per kind; lifetime pages have no time filter");
var deathsPage = Show(input, Chapter.Battle, "deaths");
Check(deathsPage.Heading == "3 deaths" && Find(deathsPage, "stat").Title == "Poison · Blob" && Find(deathsPage, "stat").Text == "Swamp · 8 Oct 00:18", "deaths: listed with local date and time");
Check(PanelModel.Build(new PanelInput(), new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = "deaths" } }).Heading == PanelModel.NoDeaths, "deaths: none says No deaths recorded");

// ---------- Skills ----------
var skills = Show(input, Chapter.Skills);
Check(skills.List.Select(l => l.Id).SequenceEqual(new[] { "Axes", "Blocking", "Cooking", "Run", "Swords" }) && skills.List.All(l => l.Icon == "skill:" + l.Id), "skills: one row per skill with the game's skill icon");
Check(Find(skills, "stat", b => b.Title == "level").Value == "38" && Find(skills, "stat", b => b.Title == "practice").Value == "14.5", "skills: level now and practice this session");

// ---------- Company ----------
var company = Show(input, Chapter.Company);
Check(company.List.Select(l => l.Label).SequenceEqual(new[] { "Edda", "Finch", "Tor" }) && company.Page == "Edda", "company: companions alphabetical, equal rows, you excluded");
Check(company.Toggle.Select(t => t.Label).SequenceEqual(new[] { "They ate your food", "You ate their food" }) && company.Toggle[0].Selected, "company: literal direction toggle, they-ate-yours first");
Check(Find(company, "empty").Title == "No record from Edda yet.", "company: their side without their shared record says so");
var mine = Show(input, Chapter.Company, "Edda", s => s.TheyReceived = false);
var thread = Find(mine, "thread");
Check(thread.Title == "Edda made" && thread.Text == "You ate" && thread.Items.Select(i => i.Title + " " + i.Value).SequenceEqual(new[] { "Bread 3", "Fish wraps 2" }), "company: your side, maker to eater with item and count");
Check(Find(mine, "stat", b => b.Title == "Edda held the helm for about 15 minutes") != null && Find(mine, "stat", b => b.Title == "Sailed together for about 21 minutes") != null, "company: helm and voyages as about-minutes");
var tor = Show(input, Chapter.Company, "Tor", s => s.TheyReceived = false);
Check(Find(tor, "tiles").Items.Select(i => i.Title + "|" + i.Value).SequenceEqual(new[] { "Iron scale mail|", "Iron sword|" }), "company: gear shown by name, not equip counts");
Check(company.Keys.Contains("[A/D] Direction") && !deeds.Keys.Contains("[A/D] Direction") && deeds.Keys.Contains("[W/S] List") && deeds.Keys.Last() == "[H/Esc] Close",
      "keys: the footer lists only bindings that work on this screen");
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
    SessionEvents.Add(ev.SkillPractice, "Cooking", 7.5f); SessionEvents.Add(ev.EquippedGearMadeBy, "Rowan|AxeBronze", 2);
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
var eddaTitles = PanelModel.Titles(edda);
Check(Line(eddaTitles, "Trailfinder", 0) == "52.3 km travelled" && Src(eddaTitles, "Shieldbearer", 0) == "measured, latest session", "snapshot: their values keep their source labels");
foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
{
    var a = PanelModel.Build(input, new PanelState { Chapter = ch }); var b = PanelModel.Build(edda, new PanelState { Chapter = ch });
    if (!a.Chapters.Select(c => c.Label).SequenceEqual(b.Chapters.Select(c => c.Label)) || b.Blocks.Count == 0) { Check(false, "snapshot: chapter " + ch + " renders"); }
}
Check(true, "snapshot: every chapter renders for a fellow player");
var eddaBattle = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Battle });
Check(eddaBattle.Scope.EndsWith("· Edda") && Find(eddaBattle, "stat", b => b.Title == "death from frost") != null, "snapshot: deaths without positions still count by type");
var eddaCompany = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Company });
Check(eddaCompany.List.Select(l => l.Label).SequenceEqual(new[] { "Rowan (you)", "Tor" }), "snapshot: in their company you are marked, they are not their own company");
edda.Fellows = new List<PanelInput> { input }; input.Fellows = new List<PanelInput> { edda };
var eddaSide = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Company });
var eddaThread = Find(eddaSide, "thread");
Check(eddaSide.Toggle[0].Label == "You ate Edda's food" && eddaThread.Title == "Edda made" && eddaThread.Text == "You ate" && eddaThread.Items.Select(i => i.Title + " " + i.Value).SequenceEqual(new[] { "Bread 3", "Fish wraps 2" }),
      "both ways: Edda's side shows what you ate of hers, from your record");
var rowanSide = PanelModel.Build(input, new PanelState { Chapter = Chapter.Company });
Check(Find(rowanSide, "thread").Items.Single().Title == "Queens Jam" && Find(rowanSide, "thread").Items.Single().Value == "2", "both ways: your side shows what Edda ate of yours, from her shared record");
Check(Find(rowanSide, "tiles").Items.Single().Icon == "item:AxeBronze", "both ways: gear she equipped that you made");
var cooking = Show(input, Chapter.Deeds, "cooking");
Check(cooking.Heading == "2 meals eaten by companions" && Find(cooking, "rows").Items.Single().Title == "Edda" && Find(cooking, "rows").Note == PanelModel.SourceFellows,
      "cooking: meals eaten by companions, who ate them (by name), labelled measured on their PCs");
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
                views.Add(PanelModel.Build(who, new PanelState { Chapter = ch, Page = { [ch] = l.Id }, TheyReceived = they, ShowHow = true }));
    }
var text = views.SelectMany(PanelModel.AllText).Distinct().ToList();
var dashes = text.Where(s => s.Contains('\u2014') || s.Contains('\u2013')).ToList();
Check(dashes.Count == 0, "copy: no em-dash or en-dash in any player-visible text" + (dashes.Count > 0 ? ": " + dashes[0] : ""));
var banned = new[] { "A shared saga", "Room for every kind of viking", "A shared deed, not a debt", "The things you bring to the fire", "Each title shows", "Counts meals eaten", "gathering" };
var hit = text.FirstOrDefault(s => banned.Any(b => s.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0));
Check(hit == null, "copy: the lines removed in the copy audit are gone, and no 'gathering'" + (hit != null ? ": " + hit : ""));
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
        ("battle-deaths", input, new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = "deaths" }, ShowHow = true }, true),
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

System.Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
return fails == 0 ? 0 : 1;
