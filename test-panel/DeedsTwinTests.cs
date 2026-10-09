// Deeds twins (src/Panel/Chapters/DeedsZones.cs and the six pages): every Deeds page the game keeps a complete counter for shows
// the two zones Woodcutting and Mining have, "Your character" (the game's count now) and "Since install, this PC" (that count minus
// what it stood at when Hearthwoven first ran, or what Hearthwoven counts exactly). Sample: PanelSample.Twins on the rich sample.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class DeedsTwinTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static PanelView Show(PanelInput i, string page) => PanelModel.Build(i, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = page } });
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
    static Block Zone(PanelView v, string id) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "zone" && b.Id == id);
    static double Num(string s) => double.Parse((s ?? "0").Replace(" ", "").Replace(" ", "").Replace(",", ""), CultureInfo.InvariantCulture);

    /// <summary>The sample with the baselines taken six days ago (PanelSample.Twins), on top of the rich sample.</summary>
    public static PanelInput Twin(PanelInput input, bool startedLater = false) => PanelSample.Twins(DeedsTests.Rich(input), startedLater);

    // every number a page shows in the ember zone is at most the same count in the stone zone: the game's counter now holds the
    // baseline plus everything since
    static void Zones(string page, PanelView v)
    {
        Check(Zone(v, "character") != null && Zone(v, "pc") != null && PanelModel.Content(v).Where(b => b.Kind == "zone").Select(b => b.Id).SequenceEqual(new[] { "character", "pc" }),
              page + " twin: the stone zone (your character) first, then the ember zone (since install, this PC)");
        var ember = Zone(v, "pc");
        Check(ember.Title == PanelModel.ZoneEmber && Every(ember.Items).All(b => b.Src != "character" && b.Src != "fellows"), page + " twin: nothing of your character's inside the since-install zone");
        Check(Every(Zone(v, "character").Items).All(b => b.Src != "pc"), page + " twin: nothing counted on this PC inside your character's zone");
        Check(Every(v.Blocks).All(b => !b.SinceInstall) && !v.HeadingSinceInstall, page + " twin: no 'since install' label beside a number (the zone says it)");
    }

    public static int Run(PanelInput input, PanelInput edda)
    {
        fails = 0;
        var keep = input.Fellows; var keepE = edda.Fellows;
        input.Fellows = new List<PanelInput> { edda }; edda.Fellows = new List<PanelInput> { input };
        var twin = Twin(input);

        // ---------- Building: pieces built (the game's counter) | pieces built since install, repairs counted here ----------
        var building = Show(twin, "building");
        Zones("building", building);
        var grid = Every(Zone(building, "character").Items).First(b => b.Kind == "itemgrid");
        var wall = grid.Items.Single(i => i.Id == "$piece_woodwall");
        Check(wall.Value == "520" && wall.Faded == null && wall.Value2 == "80" && wall.Text == PanelModel.SinceWord && grid.Note == null &&
              !Every(Zone(building, "character").Items).Any(b => b.Kind == "note" && b.Text == PanelModel.FadedKeyTwin),
              "building twin: a tile is the game's count (520) in all, the part since install said in words under it (\"80 since install\"), no key needed");
        var bh = Zone(building, "pc").Items.First(b => b.Kind == "hero");
        Check(bh.Value == "126" && bh.Title == "pieces built" && bh.Items.Single().Value == "9" && bh.Items.Single().Title == "pieces repaired" && bh.Src == "pc",
              "building twin: since install, 126 pieces built (the game's counter minus its baseline: 80 + 40 + 6) and the 9 repairs Hearthwoven counted");
        var rank = Zone(building, "pc").Items.First(b => b.Kind == "ranking");
        Check(rank.Items.Select(r => r.Title + "=" + r.Value).SequenceEqual(new[] { "Wood Wall=80", "Wood Floor 2x2=40", "Sharp Stakes=6" }) && rank.Src == "pc",
              "building twin: the pieces since install, most first");
        Check(Num(bh.Value) <= Num(Every(Zone(building, "character").Items).First(b => b.Kind == "hero").Value),
              "building twin: since install never exceeds the character's total (126 of 866)");
        Check(Zone(building, "pc").Text == "this PC, since 1 Oct" && Zone(building, "pc").Note == null, "building twin: the tight zone folds 'counting since the install' into its heading (this PC, since 1 Oct)");

        // nothing built since install yet (the baseline holds the whole counter): the ember zone says so, in its empty variant
        var idle = Twin(input); idle.Baseline[LocalTotals.PlacedKind] = idle.PiecesPlaced.ToDictionary(kv => kv.Key, kv => kv.Value); idle.Events.Repairs.Clear();
        var idleEmber = Zone(Show(idle, "building"), "pc");
        Check(idleEmber != null && idleEmber.Tone == PanelModel.ZoneEmpty && idleEmber.Note == PanelModel.ZoneLine("1 Oct", true) && idleEmber.Items.Single().Kind == "empty",
              "building twin: nothing since install yet: the ember zone is empty, its line carries the fill-up clause");
        // no baseline (a fellow's copy, totals not loaded): no twin, no number invented
        var none = DeedsTests.Rich(input);
        Check(Zone(Show(none, "building"), "pc") == null || Every(Zone(Show(none, "building"), "pc").Items).All(b => b.Kind != "hero"), "building twin: without a baseline no since-install number appears");
        var fellow = Twin(input); fellow.IsSelf = false; fellow.PlayerName = "Edda";
        Check(Every(Show(fellow, "building").Blocks).All(b => b.Kind != "zone" || b.Id == "character" || b.Items.All(x => x.Kind != "hero")), "building twin: a fellow's page carries no since-install twin (their copy has no baseline)");

        // a counter baselined after the install (an older install on its first run of this version): the zone line says since when
        var late = Twin(input, startedLater: true);
        var lateView = Show(late, "building");   // building reads the placed-pieces kind, taken at the install: no change
        Check(lateView.EmberFrom == late.InstalledUtc && Zone(lateView, "pc").Text == "this PC, since 1 Oct", "building twin: the placed-pieces baseline was taken at the install: the heading stays 'since 1 Oct'");

        // ---------- Groundwork ----------
        var ground = Show(twin, "groundwork");
        Zones("groundwork", ground);
        var gbar = Every(Zone(ground, "character").Items).First(b => b.Kind == "composition");
        var level = gbar.Items.Single(p => p.Id == "$piece_levelground");
        Check(level.Value == "850" && Math.Abs(level.Fraction2 - 780f / 850) < 1e-5 && gbar.Note == PanelModel.FadedKey && gbar.Items.Single(p => p.Id == "$piece_pavedroad").Fraction2 == 1f,
              "groundwork twin: the bar in two layers: Level Ground 850, its faded share 780 of 850 (before install), Paved Road all faded");
        var gh = Zone(ground, "pc").Items.First(b => b.Kind == "hero");
        var gr = Zone(ground, "pc").Items.First(b => b.Kind == "ranking");
        Check(gh.Value == "83" && gh.Title == "groundwork strokes" && gr.Items.Select(r => r.Title + "=" + r.Value).SequenceEqual(new[] { "Level Ground=70", "Raise Ground=13" }),
              "groundwork twin: since install 83 groundwork strokes (70 level, 13 raise; paved road none)");
        Check(Num(gh.Value) <= Num(Every(Zone(ground, "character").Items).First(b => b.Kind == "hero").Value), "groundwork twin: since install never exceeds the character's total (83 of 1,023)");

        // ---------- Crafting: gear crafted and upgrades (the game's counters) | the same since install ----------
        var crafting = Show(twin, "crafting");
        Zones("crafting", crafting);
        var stoneCraft = Zone(crafting, "character");
        var axe = Every(stoneCraft.Items).First(b => b.Kind == "itemgrid").Items.Single(i => i.Id == "$item_axe_bronze");
        Check(axe.Value == "6" && axe.Value2 == "1" && axe.Text == PanelModel.SinceWord, "crafting twin: a tile is the game's count (6) in all, \"1 since install\" under it");
        var ch = stoneCraft.Items.First(b => b.Kind == "hero");
        var eh = Zone(crafting, "pc").Items.First(b => b.Kind == "hero");
        Check(ch.Value == "20" && ch.Items.Single().Value == "11" && eh.Value == "6" && eh.Title == "gear crafted" && eh.Items.Single().Value == "3" && eh.Items.Single().Title == "upgrades made",
              "crafting twin: since install 6 gear crafted and 3 upgrades (the counters now minus their baselines) beside 20 and 11 for the character");
        Check(Num(eh.Value) <= Num(ch.Value) && Num(eh.Items.Single().Value) <= Num(ch.Items.Single().Value), "crafting twin: since install never exceeds the character's total");
        var madeSince = PanelModel.CounterLayers(twin, PanelModel.CraftedBaseline, twin.ItemsCrafted, k => twin.ItemKind(k) == "gear");   // the items made since install: the tiles carry them as their solid numbers
        Check(madeSince.Values.Sum(l => l.since) == Num(eh.Value) && madeSince.All(kv => kv.Value.since <= kv.Value.before + kv.Value.since),
              "crafting twin: the items made since install (the solid part of the tiles) add up to the hero's gear crafted");
        var plain = DeedsTests.Rich(input);
        Check(Zone(Show(plain, "crafting"), "pc") == null, "crafting twin: no baseline, no since-install zone (the page is as before)");

        // ---------- Farming: planted and picked | planted counted exactly since install, picked = the game's counter minus its baseline ----------
        var farming = Show(twin, "farming");
        Zones("farming", farming);
        var fs = Zone(farming, "character"); var fe = Zone(farming, "pc");
        var fsh = fs.Items.First(b => b.Kind == "hero"); var feh = fe.Items.First(b => b.Kind == "hero");
        Check(fsh.Value == "353" && fsh.Title == "planted" && fsh.Items.Single().Value == "312" && fsh.Items.Single().Title == "picked",
              "farming twin: your character: 353 planted (162 at the first run + 191 counted since, as before) and the game's 312 picked");
        Check(feh.Value == "191" && feh.Title == "planted" && feh.Items.Single().Value == "42" && feh.Items.Single().Title == "picked" && feh.Src == "pc",
              "farming twin: since install: 191 planted (counted exactly, the PlantEasily grids included) and 42 picked (312 now minus 270 at the first run)");
        Check(Num(feh.Items.Single().Value) <= Num(fsh.Items.Single().Value), "farming twin: picked since install never exceeds the character's total (42 of 312)");
        var picks = fe.Items.First(b => b.Kind == "ranking");
        Check(picks.Items.Select(r => r.Title + "=" + r.Value).SequenceEqual(new[] { "Barley=25", "Carrot=11", "Turnip=6" }) && picks.Items.Sum(r => Num(r.Value)) == Num(feh.Items.Single().Value),
              "farming twin: the crops picked since install add up to the 42");
        Check(fe.Items.All(b => b.Kind != "strip"), "farming twin: no strip of other harvests in the since-install zone (the page has to fit the plate)");
        var tiles = Every(fs.Items).First(b => b.Kind == "cropgrid").Items;
        var barley = tiles.Single(t => t.Id == "Barley").Items.Single(p => p.Tone == PanelModel.PickedWord);
        Check(barley.Value == "66" && Math.Abs(barley.Fraction2 - 41f / 66) < 1e-5 && barley.Faded == "41" && barley.Solid == "25" && Every(fs.Items).First(b => b.Kind == "cropgrid").Note == PanelModel.FadedKey,
              "farming twin: a crop tile's picked bar in two layers: 66 picked, 41 before install faded, 25 since solid");
        Check(tiles.All(t => { var p = t.Items.Single(x => x.Tone == PanelModel.PickedWord); return p.Faded == null || Num(p.Faded) + Num(p.Solid) == Num(p.Value); }), "farming twin: every layered picked number adds up to the tile's picked");
        // a character that started with Hearthwoven: every plant counted since install, so planting belongs to the ember zone alone
        var fresh = Twin(input); fresh.Baseline[LocalTotals.PlacedKind] = new Dictionary<string, float>();
        var freshV = Show(fresh, "farming");
        Check(Zone(freshV, "character").Items.First(b => b.Kind == "hero").Title == "picked" && Zone(freshV, "pc").Items.First(b => b.Kind == "hero").Value == "191" &&
              Every(Zone(freshV, "character").Items).All(b => b.Kind != "hero" || b.Title != "planted"),
              "farming twin: a character that started with Hearthwoven: planted only in the since-install zone, picked leads the character's");
        // the older install that first read the picked counters after an update: the zone line says since when
        var laterV = Show(Twin(input, startedLater: true), "farming");
        var laterEmber = Zone(laterV, "pc");
        Check(laterV.EmberFrom.HasValue && laterEmber.Text == "this PC" && laterEmber.Note == PanelModel.ZoneLineBegan("6 Oct", false), "farming twin: counters first read on 6 Oct, after the install (1 Oct): the zone says so, even drawn tight");
        var laterEmpty = Twin(input, startedLater: true); laterEmpty.Events.Planted.Clear(); foreach (var k in laterEmpty.Baseline[LocalTotals.StatsKind].Keys.ToList()) laterEmpty.Character[k] = laterEmpty.Baseline[LocalTotals.StatsKind][k];
        foreach (var k in laterEmpty.Baseline[LocalTotals.PickablesKind].Keys.ToList()) laterEmpty.Harvested[k] = laterEmpty.Baseline[LocalTotals.PickablesKind][k];
        var emptyEmber = Zone(Show(laterEmpty, "farming"), "pc");
        Check(emptyEmber != null && emptyEmber.Tone == PanelModel.ZoneEmpty && emptyEmber.Note == PanelModel.ZoneLineBegan("6 Oct", true),
              "farming twin: nothing yet since a later start: the empty ember zone says 'Counting since 6 Oct, when Hearthwoven started these counts: keep playing and this fills up.'");
        Check(PanelModel.ZoneLineBegan("6 Oct", false) == "Counting since 6 Oct, when Hearthwoven started these counts.", "farming twin: once it has numbers the line drops the fill-up clause");

        // ---------- Fishing: hooked, caught, got away (the game's counters) | the same since install ----------
        var fishing = Show(twin, "fishing");
        Zones("fishing", fishing);
        var fishS = Zone(fishing, "character"); var fishE = Zone(fishing, "pc");
        var perch = Every(fishS.Items).First(b => b.Kind == "itemgrid").Items.Single(i => i.Id == "$animal_fish1");
        Check(perch.Value == "88" && perch.Value2 == "20" && perch.Text == PanelModel.SinceWord, "fishing twin: a fish tile is the game's count (88) in all, \"20 since install\" under it");
        var fh = fishE.Items.First(b => b.Kind == "hero");
        Check(fh.Value == "46" && fh.Title == "fish caught" && fh.Items.Select(n => n.Value + " " + n.Title).SequenceEqual(new[] { "72 hooked", "17 got away" }),
              "fishing twin: since install 46 fish caught, 72 hooked, 17 got away (each counter now minus its baseline)");
        var fishRows = Every(fishS.Items).First(b => b.Kind == "ranking").Items.ToDictionary(r => r.Title, r => Num(r.Value));
        Check(Num(fh.Value) <= fishRows["Caught"] && Num(fh.Items[0].Value) <= fishRows["Hooked"] && Num(fh.Items[1].Value) <= fishRows["Got away"], "fishing twin: since install never exceeds the character's totals (46 of 251, 72 of 402, 17 of 97)");
        var fishTiles = Every(fishS.Items).First(b => b.Kind == "itemgrid").Items.Where(i => i.Value2 != null).ToList();   // the since-install part of each tile (the page has no second list: it would not fit the plate)
        Check(fishTiles.Select(r => r.Title + "=" + r.Value2).OrderBy(x => x).SequenceEqual(new[] { "Giant Herring=14", "Perch=20", "Pike=12" }) && fishTiles.Sum(r => Num(r.Value2)) == Num(fh.Value),
              "fishing twin: the fish caught since install per kind (the solid part of each tile) add up to the 46");
        Check(Zone(Show(DeedsTests.Rich(input), "fishing"), "pc") == null, "fishing twin: no baseline, no since-install zone (the page is as before)");

        // ---------- Taming: petted and commands (complete counters) | since install; tamed is owner-only and has no twin ----------
        var taming = Show(twin, "taming");
        Zones("taming", taming);
        var tameS = Zone(taming, "character"); var tameE = Zone(taming, "pc");
        var tameTiles = Every(tameS.Items).First(b => b.Kind == "counts").Items.ToDictionary(i => i.Title, i => Num(i.Value));
        var th = tameE.Items.First(b => b.Kind == "hero");
        Check(th.Value == "7" && th.Title == "petted" && th.Items.Single().Value == "3" && th.Items.Single().Title == "commands given" && Num(th.Value) <= tameTiles["Petted"] && Num(th.Items.Single().Value) <= tameTiles["Commands given"],
              "taming twin: since install 7 petted and 3 commands given (41 and 19 for the character): never more than the character's");
        Check(!Every(tameE.Items).Any(b => (b.Title ?? "").IndexOf("tamed", StringComparison.OrdinalIgnoreCase) >= 0 && b.Kind != "section") && Every(tameS.Items).Any(b => b.Kind == "note" && b.Text == PanelModel.TamedNote),
              "taming twin: tamed has no since-install number (the game books it to the creature's owner); its note stays with the character's");
        var noBase = Zone(Show(DeedsTests.Rich(input), "taming"), "pc");   // the sample carries cargo-06's born-in-your-care count, a Hearthwoven count of this PC: its own zone, with no twin numbers
        Check(noBase == null || (!Every(noBase.Items).Any(b => b.Kind == "hero") && Every(noBase.Items).Any(b => b.Kind == "section" && b.Title == PanelModel.BornTitle)), "taming twin: no baseline, no twin numbers in the since-install zone (only the born-in-your-care count, which has no baseline, can sit there)");

        // ---------- every twin page: since install adds up to at most the character's, in both zones, on the sample; a fellow has none ----------
        foreach (var page in new[] { "building", "groundwork", "crafting", "farming", "fishing", "taming" })
        {
            var mine = Show(twin, page);
            Check(Every(Zone(mine, "pc").Items).Where(b => b.Kind == "hero").All(h => new[] { h }.Concat(h.Items ?? new List<Block>()).All(n => Num(n.Value) > 0)), page + " twin: the since-install hero shows only numbers above zero");
            var theirs = Twin(input); theirs.IsSelf = false; theirs.PlayerName = "Edda";
            var tv = Show(theirs, page);
            Check(!PanelModel.AllText(tv).Any(t => t.Contains("Counting since")) || Zone(tv, "pc") == null, page + " twin: a fellow's page says nothing about counting since your install");
        }

        // ---------- the in-game sample (Dev.SampleData) carries the twins too ----------
        var full = PanelSample.Full(new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));
        foreach (var page in new[] { "building", "groundwork", "crafting", "farming", "fishing", "taming" })
        {
            var v = Show(full, page);
            Check(Zone(v, "character") != null && Zone(v, "pc") != null && Zone(v, "pc").Items.Any(b => b.Kind == "hero"), "sample: Deeds > " + page + " shows both zones, each with its numbers");
        }

        input.Fellows = keep; edda.Fellows = keepE;
        return fails;
    }
}
