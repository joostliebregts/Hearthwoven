// Zones (design B, src/Panel/ZonesModel.cs): your character's counts in the stone zone, this PC's in the ember zone, sorted
// by the Src every block carries; no "since install" label inside a zone. Helpers for the other test files: Zoned.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

/// <summary>Where a block sits among the zones, for the checks that used to look at the "since install" label.</summary>
static class Zoned
{
    public static List<Block> Zones(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "zone").ToList();
    static bool Under(Block z, Block b) => (z.Items ?? new List<Block>()).Any(x => ReferenceEquals(x, b) || Under(x, b));
    public static Block ZoneOf(PanelView v, Block b) => Zones(v).FirstOrDefault(z => Under(z, b));
    /// <summary>The block says it was counted since install: its own label, or it sits in the "Since install" zone.</summary>
    public static bool Says(PanelView v, Block b) => b.SinceInstall || ZoneOf(v, b)?.Title == PanelModel.ZoneEmber;
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
    /// <summary>How often the page says "since install": the heading's label, labels on numbers, and each "Since install" zone.</summary>
    public static int Labels(PanelView v) => (v.HeadingSinceInstall ? 1 : 0) + Every(v.Blocks).Count(b => b.SinceInstall) + Zones(v).Count(z => z.Title == PanelModel.ZoneEmber);
}

static class ZonesTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
    static PanelView Show(PanelInput i, Chapter c, string page = null, Action<PanelState> more = null)
    {
        var st = new PanelState { Chapter = c }; if (page != null) st.Page[c] = page; more?.Invoke(st);
        return PanelModel.Build(i, st);
    }
    // a zone's blocks in reading order, its layout boxes opened (a column, a view), not the parts inside one number
    static IEnumerable<Block> Inside(Block zone)
    {
        foreach (var b in zone.Items ?? new List<Block>())
        {
            yield return b;
            if (PanelModel.IsBox(b)) foreach (var x in Inside(b)) yield return x;
        }
    }
    // a pair (Taming: born in your care beside born near you, the server's book) keeps both parts in the zone, each with its own source mark
    static bool InPair(Block zone, Block x) => x.Tone == PanelModel.PairTone || Every(zone.Items ?? new List<Block>()).Where(p => p.Tone == PanelModel.PairTone).SelectMany(p => Every(p.Items ?? new List<Block>())).Contains(x);
    static List<Block> TopLevel(PanelView v) => (PanelModel.PlateOf(v)?.Items ?? v.Blocks).Where(b => b.Kind != "featband").ToList();   // the feat band sits above the zones, outside their order

    public static int Run(PanelInput input, PanelInput edda, PanelInput voyager, PanelInput battle)
    {
        fails = 0;
        // ---------- every page: blocks in the zone of their Src, no label inside a zone, stone first ----------
        var pages = new List<(string where, PanelView v)>();
        foreach (var who in new[] { input, edda, voyager, battle, new PanelInput() })
            foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
                foreach (var l in Show(who, ch).List)
                    foreach (var they in new[] { true, false })
                        pages.Add(((who.IsSelf ? who.PlayerName : "fellow " + who.PlayerName) + " " + ch + "/" + l.Id, PanelModel.Build(who, new PanelState { Chapter = ch, Page = { [ch] = l.Id }, TheyReceived = they })));
        string misplaced = null, labelled = null, outside = null, order = null;
        foreach (var (where, v) in pages)
        {
            var zones = Zoned.Zones(v);
            foreach (var z in zones)
                foreach (var b in Inside(z).Where(x => !InPair(z, x)))   // a pair (Taming: born in your care beside born near you) keeps both parts, each with its own source mark
                {
                    var k = PanelModel.ZoneOf(b);
                    if (k != null && k != z.Id) misplaced ??= where + ": " + b.Kind + " " + (b.Title ?? b.Value) + " (" + k + ") in the " + z.Id + " zone";
                }
            foreach (var z in zones) foreach (var b in Every(z.Items ?? new List<Block>()).Where(b => b.SinceInstall)) labelled ??= where + ": " + (b.Title ?? b.Value);
            if (zones.Count > 0)
            {
                foreach (var b in TopLevel(v).Where(b => b.Kind != "zone" && b.Tone != PanelModel.SkillStripTone))   // the skill beside the deed ends the plate on purpose
                    if (PanelModel.ZoneOf(b) == "character" || PanelModel.ZoneOf(b) == "pc") outside ??= where + ": " + b.Kind + " " + (b.Title ?? b.Value) + " outside the zones";
                var top = TopLevel(v).Select(b => b.Kind == "zone" && b.Id != "server" ? b.Id : "rest").ToList();   // fix4-rest: the server's book is a zone of its own, counted with the rest
                var pcFirst = v.Active == Chapter.Battle && v.Page == "defense";   // fix4: Defence leads with this PC's blocks and parries
                var want = (pcFirst ? top.Where(t => t == "pc").Concat(top.Where(t => t == "character")) : top.Where(t => t == "character").Concat(top.Where(t => t == "pc"))).Concat(top.Where(t => t == "rest")).ToList();
                if (!top.SequenceEqual(want) || top.Count(t => t == "character") > 1 || top.Count(t => t == "pc") > 1) order ??= where + ": " + string.Join(",", top);
            }
        }
        Check(misplaced == null, "Z every page: each block sits in the zone of its Src (stone: your character, ember: this PC)" + (misplaced != null ? ": " + misplaced : ""));
        Check(labelled == null, "Z every page: no \"since install\" label inside a zone (the zone says it)" + (labelled != null ? ": " + labelled : ""));
        Check(outside == null, "Z every page: on a page with zones, no number of your character or this PC outside them" + (outside != null ? ": " + outside : ""));
        Check(order == null, "Z every page: one stone zone, then one ember zone, then the rest (fellow players' numbers)" + (order != null ? ": " + order : ""));
        Check(pages.Where(p => p.where.Contains(" Company/")).All(p => Zoned.Zones(p.v).Count == 0) && Zoned.Zones(PanelModel.Build(input, new PanelState { ShowAbout = true })).Count == 0,
              "Z Company (the group's numbers) and About carry no zones");

        // ---------- Woodcutting (sinceB-woodcutting) ----------
        var wood = Show(input, Chapter.Deeds, "woodcutting");
        var wz = TopLevel(wood);
        Check(wz.Count == 3 && wz[0].Kind == "zone" && wz[0].Id == "character" && wz[1].Kind == "zone" && wz[1].Id == "pc" && wz[2].Kind == "ladders" && wz[2].Tone == PanelModel.SkillStripTone, "Z woodcutting: two zones, your character first, since install under it, then the skill strip under both");
        Check(wz[0].Title == PanelModel.ZoneStone && wz[0].Text == "since you made this character, 15 Sep" && wz[1].Title == PanelModel.ZoneEmber && wz[1].Text == PanelModel.ZoneThisPc + ", since 1 Oct" &&
              wz[1].Note == null && wz[1].Tone == PanelModel.ZoneTight && wz[0].Tone == PanelModel.ZoneTight,
              "Z woodcutting: the zones' headings and right sides, drawn tight (it must fit the plate): the install date in the ember heading, no line under it");
        Check(Zoned.Zones(Show(input, Chapter.Deeds, "building")).Single(z => z.Id == "pc").Text == "this PC, since 1 Oct" && PanelModel.ZoneLine("1 Oct") == "Counting since you installed Hearthwoven on 1 Oct: keep playing and this fills up.",
              "Z zone line: a zone with numbers says only when counting began; the fill-up clause stays for an empty zone");
        Check(Every(wz[0].Items).FirstOrDefault(b => b.Kind == "hero")?.Tone == PanelModel.ZoneSecond && Every(wz[0].Items).FirstOrDefault(b => b.Kind == "composition")?.Tone == PanelModel.Thin && wz[1].Items.Where(b => b.Kind == "ranking").All(b => b.Tone == PanelModel.ZoneTight) &&
              Zoned.Zones(Show(input, Chapter.Deeds, "mining")).All(z => z.Tone == PanelModel.ZoneTight) && Zoned.Zones(Show(input, Chapter.Deeds, "groundwork")).All(z => z.Tone != PanelModel.ZoneTight),
              "Z woodcutting: the stone hero at the second-number size, the bar thin, the per-tree rows tight; Mining is drawn tight too (two heroes), Groundwork keeps its zones as they were");
        var stoneHero = wz[0].Items[0]; var emberHero = wz[1].Items.FirstOrDefault(b => b.Kind == "hero");
        Check(stoneHero.Kind == "hero" && stoneHero.Value == "410" && stoneHero.Items == null && emberHero?.Value == "64" && emberHero.Title == "axe hits" && emberHero.Src == "pc" &&
              wz[0].Items.Any(b => b.Kind == "composition") && wz[1].Items.Any(b => b.Kind == "section" && b.Title == "Axe hits per tree"),
              "Z woodcutting: trees felled and the wood brought in (one layered bar) on stone; the axe hits leave the hero for the ember zone, with the hits per tree");
        Check(!wood.HeadingSinceInstall && Zoned.Labels(wood) == 1, "Z woodcutting: said once, by the ember zone");

        // ---------- Battle overview (sinceB-battle): bosses on stone, the strip in the ember zone ----------
        var bo = Show(input, Chapter.Battle);
        var bz = TopLevel(bo);
        var bosses = bz[0].Items.FirstOrDefault(b => b.Kind == "bosses");
        Check(bz[0].Id == "character" && bosses != null && bosses.Items.Select(b => b.Title).SequenceEqual(new[] { "Eikthyr", "The Elder" }) && bosses.Items.All(b => b.Icon.StartsWith("item:Trophy") && b.Src == "character"),
              "Z battle overview: the bosses you defeated (the game's kill counter) sit on stone, each with its trophy");
        var strip = PanelModel.Content(bo).First(b => b.Kind == "biomes");
        Check(Zoned.ZoneOf(bo, strip)?.Id == "pc" && strip.Items.All(t => t.Items == null || t.Items.All(x => x.Kind != "boss")) && !PanelModel.Content(bo).Any(b => b.Kind == "section" && b.Title == "Hits"),
              "Z battle overview: the biome strip in the ember zone without the boss marks; the lifetime hit counters live on Foes and Defense (battle-layer)");
        Check(bz[1].Title == PanelModel.WindowLabel(TimeWindow.Session) && bz[1].Note == null && Zoned.Labels(bo) == 0,
              "Z battle overview: its numbers are this session's log in a window, so the ember zone names the window, not since install");
        var fresh = new PanelInput { PlayerName = "Rowan", Log = new EventLog(), Character = input.Character, EnemyKills = new Dictionary<string, float> { ["$enemy_eikthyr"] = 1 },
                                     KnownBiomes = new[] { "Meadows", "BlackForest" }, SkillLevels = input.SkillLevels, InstalledUtc = input.InstalledUtc };
        var emptyBattle = TopLevel(Show(fresh, Chapter.Battle));
        Check(emptyBattle.Count == 2 && emptyBattle[1].Tone == PanelModel.ZoneEmpty && emptyBattle[1].Items.Single().Kind == "empty" && !PanelModel.Content(Show(fresh, Chapter.Battle)).Any(b => b.Kind == "biomes") &&
              emptyBattle[0].Items.Any(b => b.Kind == "bosses"), "Z battle, first evening: the ember zone dimmed with nothing in it but its empty line; no strip without numbers; Eikthyr on stone");

        // ---------- other mixes ----------
        var foes = Show(input, Chapter.Battle, "foes", s => s.Window = TimeWindow.SinceInstall);   // All: the lifetime line (HISTORY-06: Foes has the window set)
        var fz = TopLevel(foes);
        Check(foes.Heading == "Foes" && PanelModel.PlateOf(foes).Title == "Foes" && fz[0].Id == "character" && fz[0].Items[0].Kind == "hero" && fz[0].Items[0].Value == "860" && fz[0].Items[0].Title == "foes defeated" &&
              Zoned.ZoneOf(foes, PanelModel.Content(foes).First(b => b.Kind == "foetable"))?.Id == "pc", "Z foes: the heading's lifetime count becomes the stone zone's hero; the foes struck since install in the ember zone");
        input.Fellows = new List<PanelInput> { edda }; edda.Fellows = new List<PanelInput> { input };   // as PanelUi wires the group
        var cooking = Show(input, Chapter.Deeds, "cooking");
        input.Fellows = null; edda.Fellows = null;
        var cz = TopLevel(cooking);
        Check(cz[0].Kind == "zone" && cz[0].Id == "character" && cz.Count(b => b.Kind == "zone") == 1 && cz.Skip(1).Any(b => b.Kind == "ranking"),
              "Z cooking: one kind of your own (stone); what fellow players recorded stays outside, under it");
        var maps = Show(voyager, Chapter.Voyages, "maps");
        var shared = PanelModel.Content(maps).Where(b => b.Kind == "rows").SelectMany(r => r.Items).Single(r => r.Title == "Maps shared");
        Check(Zoned.ZoneOf(maps, shared)?.Id == "pc" && PanelModel.Content(maps).Where(b => b.Kind == "rows" && b.Items.Any(i => i.Title == "Portal trips")).All(r => Zoned.ZoneOf(maps, r)?.Id == "character"),
              "Z maps: a list of both kinds splits: what your character found on stone, the map shared at the table in the ember zone");
        var axes = Show(input, Chapter.Skills, "Axes");
        Check(Zoned.ZoneOf(axes, PanelModel.Content(axes).First(b => b.Kind == "ladder"))?.Id == "character" &&
              Zoned.ZoneOf(axes, PanelModel.Content(axes).First(b => b.Kind == "hero" && b.Title == PanelModel.PracticeGained))?.Id == "pc",
              "Z skill: the level on stone, the practice since install in the ember zone");
        var practised = Show(input, Chapter.Skills, null, s => s.View["Skills/overview/view"] = "practised");
        Check(TopLevel(Show(input, Chapter.Skills)).Single().Id == "character" && TopLevel(practised).Single().Id == "pc", "Z skills overview: the chosen view decides (Levels: stone, Practised: ember)");
        var trader = TopLevel(Show(new PanelInput(), Chapter.Stores, "trader"));
        Check(trader.Count == 1 && trader[0].Id == "pc" && trader[0].Tone == PanelModel.ZoneEmpty && trader[0].Note == PanelModel.ZoneLine(null),
              "Z empty: a page that only counts since install, nothing yet: the dimmed ember zone, the same line (no date known: without one)");
        var cards = Show(input, Chapter.Deeds);
        var woodCard = PanelModel.Content(cards).Where(b => b.Kind == "cards").SelectMany(c => c.Items).FirstOrDefault(c => c.Title == "Woodcutter");
        Check(!PanelModel.Content(cards).Any(b => b.Kind == "zone") && woodCard?.Items?.FirstOrDefault()?.SinceInstall == true,
              "Z the Deeds has no zones (integrate-05): a deed card keeps its second line with its own small since-install label");

        // ---------- a fellow player's copy ----------
        var ew = TopLevel(Show(edda, Chapter.Deeds, "woodcutting"));
        var ez = ew.Where(b => b.Kind == "zone").ToList();
        Check(ez.Count > 0 && ez.All(z => z.Id == "character" ? z.Title == "Edda's character" && z.Text == "since Edda made this character" : z.Text == "Edda's PC" && z.Note == null),
              "Z fellow: their character, since they were made (no date in a shared copy), their PC; no line addressed to you");

        // ---------- words and dates ----------
        Check(PanelModel.ZoneDate(new DateTime(2026, 10, 8), new DateTime(2026, 12, 1)) == "8 Oct" && PanelModel.ZoneDate(new DateTime(2025, 3, 2), new DateTime(2026, 1, 1)) == "2 Mar 2025",
              "Z dates: day and month, the year only when it is not this year's");
        var old = Show(new PanelInput { PlayerName = "Ann", CharacterMade = new DateTime(2021, 2, 2), Character = new Dictionary<string, float> { ["Tree"] = 3 }, NowUtc = input.NowUtc }, Chapter.Deeds, "woodcutting");
        Check(TopLevel(old)[0].Text == "since you made this character" || TopLevel(old)[0].Text == "since Ann made this character", "Z dates: a profile from before the game kept a date (2 Feb 2021 stands in) shows none");
        var text = pages.SelectMany(p => PanelModel.AllText(p.v)).Distinct().ToList();
        Check(!text.Any(t => t.Contains('—') || t.Contains('–')), "Z words: no dashes");

        // ---------- LocalTotals: when counting began (the ember zone's date) ----------
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hw-zones-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var path = LocalTotals.PathFor(dir, 7);
            var before = DateTime.UtcNow.AddSeconds(-1);
            var first = LocalTotals.Load(path, 7, out _);
            Check(first.FirstRunUtc >= before && first.FirstRunUtc <= DateTime.UtcNow.AddSeconds(1), "Z install date: a character's first file starts counting now");
            first.FirstRunUtc = new DateTime(2026, 10, 1, 18, 0, 0, DateTimeKind.Utc); first.Save(path, DateTime.UtcNow);
            Check(LocalTotals.Load(path, 7, out _).FirstRunUtc == first.FirstRunUtc && LocalTotals.FromJson(first.ToJson(DateTime.UtcNow)).FirstRunUtc == first.FirstRunUtc,
                  "Z install date: kept in the file (firstRun) and read back unchanged");
            var older = first.ToJson(DateTime.UtcNow).Replace("\"firstRun\":\"" + first.FirstRunUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture) + "\",", "");
            Check(!older.Contains("firstRun") && LocalTotals.FromJson(older).FirstRunUtc == DateTime.MinValue, "Z install date: a file from before the field reads as not known");
            System.IO.File.WriteAllText(path, older);
            var made = System.IO.File.GetCreationTimeUtc(path);
            var reread = LocalTotals.Load(path, 7, out _);
            Check(made.Year < 2025 ? reread.FirstRunUtc == DateTime.MinValue : Math.Abs((reread.FirstRunUtc - made).TotalSeconds) < 2,
                  "Z install date: an older file takes its own creation time once (when Hearthwoven first wrote it)");
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
        return fails;
    }
}
