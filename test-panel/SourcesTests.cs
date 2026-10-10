// Where each page's numbers come from, after the zones (design B) left in 0.7: no page has zones; a number counted on this PC says so
// with its "Recorded from" label (RecordedModel.PlaceRecordedFrom). Helpers for the other test files: Zoned (the name stays: the
// checks that once looked at a zone or the "since install" label now read the label).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

/// <summary>Whether a block or a page says where its numbers were counted (0.7: the "Recorded from" label).</summary>
static class Zoned
{
    /// <summary>The block says it was counted on this PC: its "Recorded from" label.</summary>
    public static bool Says(PanelView v, Block b) => !string.IsNullOrEmpty(b.RecordedFrom);
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
    /// <summary>How often the page says where its numbers were counted: the heading's label and the labels on numbers.</summary>
    public static int Labels(PanelView v) => (string.IsNullOrEmpty(v.HeadingRecordedFrom) ? 0 : 1) + Every(v.Blocks).Count(b => !string.IsNullOrEmpty(b.RecordedFrom));
    /// <summary>The zones of a page: none since 0.7 (kept so a check can still say so).</summary>
    public static List<Block> Zones(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "zone").ToList();
}

static class SourcesTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
    static PanelView Show(PanelInput i, Chapter c, string page = null, Action<PanelState> more = null)
    {
        var st = new PanelState { Chapter = c }; if (page != null) st.Page[c] = page; more?.Invoke(st);
        return PanelModel.Build(i, st);
    }
    static List<Block> TopLevel(PanelView v) => (PanelModel.PlateOf(v)?.Items ?? v.Blocks).Where(b => b.Kind != "featband").ToList();   // the plate's blocks, the feat band left out

    public static int Run(PanelInput input, PanelInput edda, PanelInput voyager, PanelInput battle)
    {
        fails = 0;
        // every page of every book, for the words check at the end
        var pages = new List<(string where, PanelView v)>();
        foreach (var who in new[] { input, edda, voyager, battle, new PanelInput() })
            foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
                foreach (var l in Show(who, ch).List)
                    foreach (var they in new[] { true, false })
                        pages.Add(((who.IsSelf ? who.PlayerName : "fellow " + who.PlayerName) + " " + ch + "/" + l.Id, PanelModel.Build(who, new PanelState { Chapter = ch, Page = { [ch] = l.Id }, TheyReceived = they })));
        Check(pages.All(p => Zoned.Zones(p.v).Count == 0), "Z every page (0.7): no zones anywhere, every book, every page");

        // ---------- Woodcutting: 0.7's worked page has no zones (REDESIGN-RULES.md part 3; its own test is RecordedGatherTests) ----------
        var wood = Show(input, Chapter.Deeds, "woodcutting");
        Check(wood.Recorded && Zoned.Zones(wood).Count == 0, "Z woodcutting (0.7): migrated, no zones");
        // Z mining (the zones' headings, drawn tight) retired with the zones: Mining is migrated (0.7, RecordedGatherTests: "woodcutting 0.7", "mining 0.7")
        Check(Zoned.Zones(Show(input, Chapter.Deeds, "mining")).Count == 0 && Zoned.Zones(Show(input, Chapter.Deeds, "groundwork")).Count == 0,
              "Z tight (0.7): Mining and Groundwork are migrated: no zones, so nothing on them is drawn tight");

        // ---------- Battle overview (sinceB-battle; 0.7 G5: no zones, RecordedBattleTests has the dates) ----------
        var bo = Show(input, Chapter.Battle);
        var strip = PanelModel.Content(bo).First(b => b.Kind == "biomes");
        var bosses = strip.Items.SelectMany(t => t.Items ?? new List<Block>()).Where(x => x.Kind == "boss").ToList();
        Check(Zoned.Zones(bo).Count == 0 && bosses.Select(b => b.Title).SequenceEqual(new[] { "Eikthyr", "The Elder" }) && bosses.All(b => b.Src == "character"),
              "Z battle overview (0.7): no zones; the bosses you defeated (the game's kill counter, your character's) mark their biome on the strip counted on this PC");
        Check(strip.Src == PanelModel.SrcPc && !PanelModel.Content(bo).Any(b => b.Kind == "section" && b.Title == "Hits"),
              "Z battle overview: the biome strip is counted on this PC; the lifetime hit counters live on Foes and Defense (battle-layer)");
        Check(Zoned.Labels(bo) == 0,
              "Z battle overview: its numbers are this session's log in a window, so nothing on it says since install or carries a date (rule W.1)");
        var fresh = new PanelInput { PlayerName = "Rowan", Log = new EventLog(), Character = input.Character, EnemyKills = new Dictionary<string, float> { ["$enemy_eikthyr"] = 1 },
                                     KnownBiomes = new[] { "Meadows", "BlackForest" }, SkillLevels = input.SkillLevels, InstalledUtc = input.InstalledUtc };
        var emptyBattle = PanelModel.Content(Show(fresh, Chapter.Battle));
        Check(!emptyBattle.Any(b => b.Kind == "zone") && emptyBattle.Count(b => b.Kind == "empty") == 1 &&
              emptyBattle.Where(b => b.Kind == "biomes").SelectMany(b => b.Items).SelectMany(t => t.Items ?? new List<Block>()).Where(x => x.Kind == "boss").Select(x => x.Title).SequenceEqual(new[] { "Eikthyr" }),
              "Z battle, first evening (0.7): no zones; one empty line, and the strip of known biomes still marks Eikthyr");

        // ---------- other mixes ----------
        var foes = Show(input, Chapter.Battle, "foes", s => s.Window = TimeWindow.SinceInstall);   // All: the lifetime line (HISTORY-06: Foes has the window set)
        var foesHero = PanelModel.Content(foes).FirstOrDefault(b => b.Kind == "hero");
        Check(foes.Heading == "Foes" && PanelModel.PlateOf(foes).Title == "Foes" && Zoned.Zones(foes).Count == 0 && foesHero?.Value == "860" && foesHero.Title == "foes defeated" && foesHero.Src == PanelModel.SrcCharacter &&
              PanelModel.Content(foes).Any(b => b.Kind == "foetable" && b.Src == PanelModel.SrcPc),
              "Z foes (0.7): no zones; the lifetime count is the page's hero (your character's), the foes struck counted on this PC");
        input.Fellows = new List<PanelInput> { edda }; edda.Fellows = new List<PanelInput> { input };   // as PanelUi wires the group
        var cooking = Show(input, Chapter.Deeds, "cooking");
        input.Fellows = null; edda.Fellows = null;
        var cz = TopLevel(cooking);
        Check(cz.Count(b => b.Kind == "zone") == 0 && cz.Any(b => b.Kind == "ranking"),
              "Z cooking (0.7, rule B/A): no zones; what fellow players recorded stays under the dishes");
        var maps = Show(voyager, Chapter.Voyages, "maps");
        var shared = PanelModel.Content(maps).Where(b => b.Kind == "rows").SelectMany(r => r.Items).Single(r => r.Title == "Maps shared");
        // 0.7 (G7): no zones on Maps; the one list keeps both kinds, the map shared says "from <date>" and the finds carry none
        Check(maps.Recorded && Zoned.Zones(maps).Count == 0 && shared.Src == PanelModel.SrcPc && shared.RecordedFrom != null && shared.RecordedFrom.StartsWith("from ") &&
              PanelModel.Content(maps).Where(b => b.Kind == "rows" && b.Items.Any(i => i.Title == "Portal trips")).SelectMany(r => r.Items).Where(i => i.Title != "Maps shared").All(i => i.Src == PanelModel.SrcCharacter && i.RecordedFrom == null),
              "Z maps (0.7): one list of both kinds, the finds your character's (no label), the map shared at the table \"from <date>\"; no zones");
        var axes = Show(input, Chapter.Skills, "Axes");
        // 0.7 (G8): no zones on a Skills page; the level is your character's count, the practice share a number counted on this PC (its own label)
        var axesLadder = PanelModel.Content(axes).First(b => b.Kind == "ladder");
        Check(!PanelModel.Content(axes).Any(b => b.Kind == "zone") && axesLadder.Src == PanelModel.SrcCharacter && !Zoned.Says(axes, axesLadder) &&
              axesLadder.Items.Single(i => i.Kind == "practice") is Block axesPractice && Zoned.Says(axes, axesPractice),
              "Z skill (0.7): no zones; the level is your character's, the practice share is counted on this PC (labelled)");
        var practised = Show(input, Chapter.Skills, null, s => s.View["Skills/overview/view"] = "practised");
        var overviewLevels = Show(input, Chapter.Skills);
        Check(!PanelModel.Content(overviewLevels).Any(b => b.Kind == "zone") && !PanelModel.Content(practised).Any(b => b.Kind == "zone") &&
              PanelModel.Content(overviewLevels).Any(b => b.Kind == "ladders" && b.Src == PanelModel.SrcCharacter) && PanelModel.Content(practised).Any(b => b.Kind == "ranking" && b.Src == PanelModel.SrcPc),
              "Z skills overview (0.7): no zones; the levels are your character's, the practised view is counted on this PC (the chosen view decides)");
        var trader = Show(new PanelInput(), Chapter.Stores, "trader");   // 0.7 (G7): no zone; the empty state is the one line
        Check(trader.Recorded && Zoned.Zones(trader).Count == 0 && PanelModel.Content(trader).Count(b => b.Kind == "empty" && b.Title == "Nothing yet" && b.Text == null) == 1,
              "Z empty (0.7): a Hall page that only counts since install, nothing yet: no zone, one empty state (\"Nothing yet\", no date known)");
        var cards = Show(input, Chapter.Deeds);
        var woodCard = PanelModel.Content(cards).Where(b => b.Kind == "cards").SelectMany(c => c.Items).FirstOrDefault(c => c.Title == "Woodcutter");
        Check(!PanelModel.Content(cards).Any(b => b.Kind == "zone") && woodCard != null && (woodCard.Items == null || woodCard.Items.Count == 0),
              "Z the Deeds has no zones (integrate-05): a deed card keeps its one number; the class C second line is gone (0.7, hard case 13)");

        // ---------- a fellow player's copy ----------
        // 0.7: every Deeds page is migrated, so a fellow's Deeds pages have no zones either; the plate says whose book it is, without "since install" and without a box
        var eddaDeeds = new[] { "mining", "building", "crafting", "cooking", "farming" }.Select(pg => Show(edda, Chapter.Deeds, pg)).ToList();
        Check(eddaDeeds.All(v => v.Recorded && Zoned.Zones(v).Count == 0 && v.AboutNumbers == null && (v.Scope ?? "").Contains("Edda") && !PanelModel.AllText(v).Any(t => t.Contains("since install"))),
              "Z fellow (0.7): their Deeds pages have no zones and no box; the plate names Edda's book, never \"since install\"");

        // ---------- words and dates ----------
        Check(PanelModel.ShortDate(new DateTime(2026, 10, 8), new DateTime(2026, 12, 1)) == "8 Oct" && PanelModel.ShortDate(new DateTime(2025, 3, 2), new DateTime(2026, 1, 1)) == "2 Mar 2025",
              "Z dates: day and month, the year only when it is not this year's");
        var old = Show(new PanelInput { PlayerName = "Ann", CharacterMade = new DateTime(2021, 2, 2), PiecesPlaced = new Dictionary<string, float> { ["$piece_woodwall"] = 3 }, NowUtc = input.NowUtc }, Chapter.Deeds, "building");   // 0.7: no Deeds page has zones now
        Check(Zoned.Zones(old).Count == 0 && !PanelModel.AllText(old).Any(t => t.Contains("2021")), "Z dates (0.7): a profile from before the game kept a date (2 Feb 2021 stands in) shows none, on a page without zones");
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
