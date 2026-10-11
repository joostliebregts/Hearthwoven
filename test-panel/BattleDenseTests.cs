// 0.8.5 Battle, denser (work/hearthwoven-0.8.5/proto/1-battle-overview variant A, proto/3-deaths-height variant B): the scenarios the previews shoot
// (a late game with all nine lands found; Deaths with ten dated falls, and a long campaign) and the checks that earn their place: the Overview's
// islands as the picture has them, Where you fell's heights follow the counts, and the falls with no place are the page's count less the placed
// ones, with the true reason. Called from Program.cs. Sample names only.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class BattleDenseTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static HitData.DamageTypes D(string type, float v)
    {
        var d = new HitData.DamageTypes();
        switch (type)
        {
            case "slash": d.m_slash = v; break; case "pierce": d.m_pierce = v; break; case "blunt": d.m_blunt = v; break;
            case "poison": d.m_poison = v; break; case "fire": d.m_fire = v; break;
        }
        return d;
    }

    // a fall: the foe's last hit three seconds before it, so the fall has its killer and its type
    static void Fall(PanelInput p, int minutesAgo, string biome, string foe, string type, float hit)
    {
        var at = p.NowUtc.AddMinutes(-minutesAgo);
        p.Log.AddDamage(at.AddSeconds(-3), biome, false, foe, "EnemyHit", D(type, hit));
        p.Log.AddDeath(at, biome, 0, 0);
    }

    /// <summary>A late game (the overview's stress, Joost: "also with nine biomes found"): all nine lands found, seven fought in this session, five
    /// bosses down, eight falls in five lands, so the strip's tiles are at their narrowest and the falls fold into a line that opens Deaths.</summary>
    public static PanelInput Late(DateTime now)
    {
        var p = PanelSample.Full(now);
        p.KnownBiomes = new List<string> { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "DeepNorth", "Ocean" };
        foreach (var (biome, foe, mins, dealt, taken) in new[] { ("Mountain", "Wolf", 80, 1560f, 280f), ("Plains", "Goblin", 64, 2210f, 690f), ("Mistlands", "Seeker", 44, 640f, 410f), ("Ocean", "Serpent", 30, 120f, 60f) })
        {
            p.Log.AddDamage(p.NowUtc.AddMinutes(-mins), biome, true, foe, "Swords", D("slash", dealt));
            p.Log.AddDamage(p.NowUtc.AddMinutes(-mins), biome, false, foe, "EnemyHit", D("blunt", taken));
        }
        Fall(p, 78, "Mountain", "Wolf", "slash", 40); Fall(p, 62, "Plains", "Goblin", "fire", 60); Fall(p, 58, "Plains", "Goblin", "fire", 55);
        Fall(p, 54, "Plains", "Deathsquito", "pierce", 45); Fall(p, 42, "Mistlands", "Seeker", "pierce", 70);
        p.EnemyKills["$enemy_dragon"] = 1; p.EnemyKills["$enemy_goblinking"] = 1;
        p.DamageSinceInstall = PanelSample.SinceInstall(p.Log); p.BiomeSinceInstall = PanelSample.BiomeSinceInstall(p.Log);
        return p;
    }

    /// <summary>Deaths on a fuller evening: ten falls this session in four lands (five in the Swamp), each with its time.</summary>
    public static PanelInput DeathsFull(DateTime now)
    {
        var p = PanelSample.Full(now);
        Fall(p, 150, "Meadows", "Neck", "pierce", 20); Fall(p, 140, "BlackForest", "Greydwarf", "slash", 30); Fall(p, 130, "BlackForest", "Troll", "blunt", 90);
        Fall(p, 100, "Swamp", "Blob", "poison", 25); Fall(p, 90, "Swamp", "Draugr", "slash", 60); Fall(p, 80, "Swamp", "Leech", "poison", 30); Fall(p, 70, "Mountain", "Wolf", "slash", 45);
        p.DamageSinceInstall = PanelSample.SinceInstall(p.Log); p.BiomeSinceInstall = PanelSample.BiomeSinceInstall(p.Log);
        return p;
    }

    /// <summary>A long campaign on Deaths > All: 40 falls in the game's count, 32 placed (14 in the Swamp); 5 came before Hearthwoven first ran on this PC
    /// and 3 before it began to keep places, three days later.</summary>
    public static PanelInput DeathsMany(DateTime now)
    {
        var p = PanelSample.Full(now);
        p.BiomeSinceInstall.Deaths.Clear();
        foreach (var (biome, n) in new[] { ("Meadows", 3), ("BlackForest", 7), ("Swamp", 14), ("Mountain", 6), ("Ocean", 2) }) p.BiomeSinceInstall.Deaths[biome] = n;
        p.Events.Battle["Deaths"] = 35; p.Baseline[LocalTotals.BattleKind]["Deaths"] = 5; p.Character["Deaths"] = 40;
        p.BiomeFromUtc = SampleWorld.InstalledAt(now).AddDays(3);
        return p;
    }

    static PanelView Page(PanelInput i, string page, TimeWindow w)
    {
        var st = new PanelState { Chapter = Chapter.Battle, Window = w }; st.Page[Chapter.Battle] = page;
        return PanelModel.Build(i, st);
    }
    static Block Of(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
    static string Plan(PanelView v) => string.Join(" | ", PanelModel.Islands(PanelModel.PlateOf(v)).Select(i => i.Span + ":" + string.Join("+", i.Parts.Select(p => p.Block.Kind + (string.IsNullOrEmpty(p.Block.Tone) ? "" : "/" + p.Block.Tone)))));

    public static int Run(DateTime now)
    {
        fails = 0;
        var world = PanelSample.Full(now);

        // the Overview as variant A draws it: the totals, the tiles and the filter line with the marks' key in one island; What hurt you (one damage row)
        // with the way to Defense under it, beside the falls as lines; the tip
        var ov = Page(world, "overview", TimeWindow.Session); var key = PanelModel.FilterOf(ov)?.Items.FirstOrDefault(x => x.Kind == PanelModel.StripKeyKind);
        var guard = Of(ov, "guard");
        Check(Plan(ov) == "wide:biomes+filterbar/inline | half:composition/onerow+guard/line | half:deathrows | plain:note/hint" && key != null && key.Items.Count == 2 &&
              guard.Id == PanelModel.DefensePage && guard.Value == "5" && guard.Value2 == "2" && (guard.Items ?? new List<Block>()).Any(i => i.Kind == "skill" && i.Title == "Blocking") &&
              Of(ov, "deathrows").Title == PanelModel.FallsTitle && Of(ov, "deathrows").Value == "3" && Of(ov, "deathrows").Note == PanelModel.NewestFirst,
              "0.8.5 Battle > Overview (variant A): one island for the totals, the tiles and the filter line with the key; What hurt you as one damage row with the session's blocks and parries under it (Joost: the stats, not a button; the line opens Defense), beside the falls as lines; one tip (" + Plan(ov) + ")");

        // the late game: nine tiles, the falls past three folded into a line that opens Deaths; where nothing is listed (7 days, All) What hurt you has
        // its island to itself with Defense in its corner
        var late = Late(now); var lv = Page(late, "overview", TimeWindow.Session); var falls = Of(lv, "deathrows"); var lall = Page(late, "overview", TimeWindow.SinceInstall);
        Check(Of(lv, "biomes").Items.Count(t => t.Kind == "biome") == 9 && falls.Value == "8" && falls.Note == PanelModel.MostFirst && falls.Items.Count == PanelModel.OverviewFallRows &&
              falls.Items.Last().Kind == "more" && falls.Items.Last().Title == "5 more falls" && falls.Items.Last().Id == "Battle/deaths" &&
              PanelModel.Content(lv).Count(b => b.Kind == "note" && b.Tone == "hint") == PanelModel.HintTop &&
              Plan(lall).StartsWith("wide:biomes+filterbar/inline | wide:composition/onerow+guard/" + PanelModel.GuardLineTone, StringComparison.Ordinal),
              "0.8.5 Overview, nine lands found: every tile, the falls past three fold into \"5 more falls\" that opens Deaths, one tip; on All What hurt you alone with the blocks and parries under it (" + Plan(lall) + ")");

        // Where you fell, the tiles grow: the heights follow the counts, in both forms (ten dated falls, and a long campaign as counts)
        bool Follows(Block strip, bool dated)
        {
            var tiles = strip.Items.Where(t => t.Kind == "biome").ToList(); var most = tiles.Max(t => t.Count);
            var h = tiles.Select(t => (t.Count, h: PanelModel.FellHeight(t.Count, most, dated))).ToList();
            return PanelModel.FellDated(tiles) == dated && h.All(a => h.All(b => a.Count < b.Count ? a.h < b.h : a.Count == b.Count ? a.h == b.h : a.h > b.h)) &&
                   h.Where(x => x.Count == 0).All(x => x.h == PanelLayout.Fell.Foot) && h.Max(x => x.h) <= PanelLayout.Fell.Foot + PanelLayout.Fell.Room;
        }
        var full = Of(Page(DeathsFull(now), "deaths", TimeWindow.Session), "deathstrip"); var manyPage = Page(DeathsMany(now), "deaths", TimeWindow.SinceInstall); var many = Of(manyPage, "deathstrip");
        Check(Follows(full, true) && full.Items.Single(t => t.Id == "Swamp").Count == 5 && Follows(many, false) && many.Items.Single(t => t.Id == "Swamp").Count == 14,
              "0.8.5 Where you fell: taller where you fell more, the heights following the counts, with each fall's time (ten falls) and as counts (a long campaign)");

        // the falls with no place: the page's count less the placed ones, with the reason the recorder gives (the sample: before the install;
        // the long campaign: before the install and before places were kept)
        int Hero(PanelView v) => (int)PanelModel.ParseCount(Of(v, "hero").Value);
        var all = Page(world, "deaths", TimeWindow.SinceInstall); var strip = Of(all, "deathstrip"); var placed = strip.Items.Sum(t => t.Count);
        Check(strip.Count == Hero(all) - placed && strip.Count == 11 && strip.Text == "11 of your 15 falls have no place: they came before 1 October, when Hearthwoven first ran on this PC." &&
              many.Count == Hero(manyPage) - many.Items.Sum(t => t.Count) && many.Count == 8 &&
              many.Text == "8 of your 40 falls have no place: 5 came before 1 October, when Hearthwoven first ran on this PC; 3 came before 4 October, when Hearthwoven began to keep places." &&
              Of(Page(world, "deaths", TimeWindow.Session), "deathstrip").Count == 0,
              "0.8.5 Deaths > All: the falls with no place are the page's count less the placed ones, and one line says the true reason (" + strip.Text + " | " + many.Text + ")");
        return fails;
    }
}
