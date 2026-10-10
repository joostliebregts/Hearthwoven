// 0.7 redesign, the shared pieces (work/hearthwoven-0.7/REDESIGN-RULES.md part 2; src/Panel/RecordedModel.cs): the date chain of a part
// (StartOf), the date words (RecordDate), where "Recorded from" lands (PlaceRecordedFrom) and the "About these numbers" box on its key.
// Also RealBook(): Joost's real character as the scenario real-*-* (his 0.6 RC local totals, test-panel/fixtures/sample-0.6rc-localtotals.json).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RecordedTests
{
    /// <summary>
    /// Joost's own character ("Astrid" in the fixture) as 0.6 RC saw it on 9 Oct 2026: FeatsTests.Joost() plus the file's
    /// baselines, their dates and the game's counters as they stood then (the baselines: nothing grew since, so every since-install part is
    /// 0, as in his book that evening) and his skill levels (ingame-0.6rc/skills-overview-levels.json). null when the fixture does not load.
    /// </summary>
    public static PanelInput RealBook()
    {
        var j = FeatsTests.Joost();
        if (j == null) return null;
        var src = AppContext.BaseDirectory;
        while (src != null && !System.IO.File.Exists(System.IO.Path.Combine(src, "src", "Plugin.cs"))) src = System.IO.Path.GetDirectoryName(src);
        var lt = LocalTotals.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(src, "test-panel", "fixtures", "sample-0.6rc-localtotals.json")));
        Dictionary<string, float> Kind(string k) => lt.Baseline.TryGetValue(k, out var d) ? new Dictionary<string, float>(d) : new Dictionary<string, float>();
        j.Baseline = lt.Baseline; j.ExactAtBaseline = lt.ExactAtBaseline; j.BaselineAt = lt.BaselineAt;
        lt.FillStarts(j.NowUtc); j.Starts = lt.Starts;   // as 0.7 fills them on its first load: the stats baseline's date
        foreach (var kv in Kind(LocalTotals.StatsKind)) j.Character[kv.Key] = kv.Value;
        foreach (var kv in Kind("treesFelled")) j.Character[kv.Key] = kv.Value;
        j.ItemsPickedUp = Kind("pickedUp"); j.PiecesPlaced = Kind(LocalTotals.PlacedKind); j.ItemsCrafted = Kind(LocalTotals.CraftedKind); j.Harvested = Kind(LocalTotals.PickablesKind);
        j.Log = new EventLog(); j.Session = new DamageTally();
        var look = PanelSample.Full(j.NowUtc);   // the game's own names and kinds of vanilla tokens, as the sample world reads them (no game here)
        j.DisplayName = look.DisplayName; j.ItemKind = look.ItemKind; j.StationDish = look.StationDish; j.PieceKind = look.PieceKind; j.GatherKind = look.GatherKind;
        j.CropOf = look.CropOf; j.ItemType = look.ItemType; j.MainMaterial = look.MainMaterial; j.DishType = look.DishType; j.DishBoost = look.DishBoost;
        var levels = new (string skill, float level, float progress)[]
        {
            ("Swords", 28, 0.2f), ("Clubs", 13, 0), ("Polearms", 9, 0), ("Blocking", 10, 0.28f), ("Axes", 16, 0), ("Bows", 31, 0.346f), ("ElementalMagic", 1, 0), ("BloodMagic", 4, 0),
            ("Unarmed", 14, 0.152f), ("Pickaxes", 22, 0.054f), ("WoodCutting", 33, 0.407f), ("Fishing", 5, 0), ("Farming", 57, 0.053f), ("Jump", 27, 0.734f), ("Sneak", 8, 0.005f),
            ("Run", 52, 0.307f), ("Swim", 9, 0), ("Dodge", 2, 0.032f), ("Cooking", 12, 0.971f), ("Crafting", 19, 0.163f),
        };
        j.SkillLevels = levels.ToDictionary(l => l.skill, l => l.level); j.SkillProgress = levels.ToDictionary(l => l.skill, l => l.progress);
        return j;
    }

    public static int Run(PanelInput input, PanelInput edda, DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- StartOf: a group's own start, else the kind's baseline date, else the install; a fellow's copy has none ----------
        var oct1 = new DateTime(2026, 10, 1, 16, 0, 0, DateTimeKind.Utc);
        var me = new PanelInput { NowUtc = now, ToLocal = t => t.AddHours(2), InstalledUtc = oct1,
                                  BaselineAt = new Dictionary<string, DateTime> { [LocalTotals.StatsKind] = oct1.AddDays(1) },
                                  Starts = new Dictionary<string, DateTime> { [LocalTotals.StartCargo] = oct1.AddDays(2) } };
        Check(PanelModel.StartOf(me, LocalTotals.StartCargo) == oct1.AddDays(2) && PanelModel.StartOf(me, LocalTotals.StatsKind) == oct1.AddDays(1) &&
              PanelModel.StartOf(me, "pickedUp") == oct1 && PanelModel.StartOf(me, null) == oct1 && PanelModel.StartOf(new PanelInput { IsSelf = false, InstalledUtc = oct1 }, null) == null,
              "StartOf: a counter group's own start, else the kind's baseline date, else the install; a fellow's copy: not known");

        // ---------- the date words: the player's local day, the month in full, the year only when it is not this year ----------
        Check(PanelModel.RecordDate(me, new DateTime(2026, 10, 7, 23, 0, 0, DateTimeKind.Utc)) == "8 October" && PanelModel.RecordDate(me, new DateTime(2025, 10, 8, 12, 0, 0, DateTimeKind.Utc)) == "8 October 2025" &&
              PanelModel.RecordedFromLine(me, oct1) == "Recorded from 1 October · this PC" && PanelModel.FromShort(me, oct1) == "from 1 October" && PanelModel.NothingFrom(me, oct1) == "Nothing from 1 October" &&
              !edda.SharedSinceInstall && PanelModel.RecordedFromLine(edda, null) == "as Edda last shared it" && PanelModel.FromShort(edda, null) == "as Edda last shared it" && PanelModel.NothingFrom(edda, null) == PanelModel.NothingYet &&
              new PanelInput { IsSelf = false, PlayerName = "Edda", SharedSinceInstall = true } is PanelInput eddaAll && PanelModel.RecordedFromLine(eddaAll, null) == "Recorded on Edda's PC" && PanelModel.FromShort(eddaAll, null) == "on Edda's PC",
              "words: \"8 October\" in the player's own time (23:00 UTC is the next day at +2), with the year when not this year; a fellow's book: \"Recorded on Edda's PC\", no date; a copy of only their last session: \"as Edda last shared it\"");

        // ---------- PlaceRecordedFrom on a made-up page: where each label lands, with whose date ----------
        Block Num(string kind, string value, string src, DateTime? from = null) => new Block { Kind = kind, Value = value, Title = "x", Src = src, Source = src, From = from };
        var hero = Num("hero", "410", PanelModel.SrcCharacter); hero.Items = new List<Block> { Num("number", "134", PanelModel.SrcPc) };
        var listed = new Block { Kind = "ranking", Src = PanelModel.SrcPc, Items = new List<Block> { Num("row", "17", PanelModel.SrcPc), Num("row", "9", PanelModel.SrcPc) } };
        var mixed = new Block { Kind = "section", Title = "Mixed" };
        var early = Num("stat", "5", PanelModel.SrcPc); var later = Num("stat", "7", PanelModel.SrcPc, oct1.AddDays(2));
        var head = new Block { Kind = "section", Title = "Per tree" };
        var view = new PanelView { Heading = "Page" };
        view.Blocks.Add(new Block { Kind = "plate", Items = new List<Block> { hero, head, listed, mixed, early, later } });
        PanelModel.PlaceRecordedFrom(me, view, Chapter.Deeds);
        Check(hero.RecordedFrom == null && hero.Items[0].RecordedFrom == "from 1 October" && head.RecordedFrom == "Recorded from 1 October · this PC" && listed.RecordedFrom == null &&
              mixed.RecordedFrom == null && early.RecordedFrom == "from 1 October" && later.RecordedFrom == "from 3 October" && view.HeadingRecordedFrom == null,
              "PlaceRecordedFrom: a single number among your character's gets \"from 1 October\"; a whole section \"Recorded from 1 October · this PC\" once; a section whose numbers began on different days says each number's own date");

        // ---------- the box: own book only, under the hero, opened by the key or its button; its key in the line before [T] About ----------
        var world = PanelSample.Full(now);
        var shut = PanelModel.Build(world, new PanelState { Page = { [Chapter.Deeds] = "woodcutting" } });
        var st = new PanelState { Page = { [Chapter.Deeds] = "woodcutting" } }; PanelModel.Follow(st, PanelModel.NumbersTarget);
        var open = PanelModel.Build(world, st);
        var plate = PanelModel.PlateOf(open).Items;
        var button = PanelModel.PlateOf(shut).Items.Single(b => b.Kind == "aboutnumbers");
        Check(st.ShowNumbers && button.Items == null && !button.Open && button.KeyCap == "Y" && plate[0].Kind == "featband" && plate[1].Kind == "aboutnumbers" &&
              plate.Single(b => b.Kind == "aboutnumbers").Items.Select(l => l.Title).SequenceEqual(new[] { "Before 1 October", "From 1 October", PanelModel.DetailsLabel, PanelModel.AboutEarlierLabel, PanelModel.AboutGrowthLabel }) &&
              shut.Keys.IndexOf("[Y] Numbers") == shut.Keys.IndexOf("[T] About") - 1,
              "About these numbers: the button in the strip with its Y cap (0.8 layout D+); its click (or Y) opens the box right under the strip with Before, From and Additional details, then the earlier counts and the growth line's span (both off the page); the key sits right before [T] About");
        var fellowBook = PanelModel.Build(edda, new PanelState { Page = { [Chapter.Deeds] = "woodcutting" }, ShowNumbers = true });
        Check(fellowBook.AboutNumbers == null && !PanelModel.Content(fellowBook).Any(b => b.Kind == "aboutnumbers") && !fellowBook.Keys.Any(k => k.Contains(PanelModel.AboutNumbersTitle)),
              "About these numbers: a fellow's book has no box and no key (their copy carries no dates)");
        // the hero row's one wrap rule (MERGE-NOTES G1/G3: "Farming lev" clipped at the plate edge on the real book's farming): what does not fit goes under, never off the plate
        Check(PanelModel.HeroLines(new[] { 300f, 200f }, 69, 180, 840).SequenceEqual(new[] { 0, 0, 0 }) &&     // all on one line
              PanelModel.HeroLines(new[] { 420f, 280f }, 69, 180, 840).SequenceEqual(new[] { 0, 0, 1 }) &&     // the real book's farming: the skill goes under
              PanelModel.HeroLines(new[] { 520f, 300f }, 69, 180, 840).SequenceEqual(new[] { 0, 1, 1 }) &&     // the second number goes under, the skill beside it
              PanelModel.HeroLines(new[] { 520f, 700f }, 69, 180, 840).SequenceEqual(new[] { 0, 1, 2 }) &&     // each its own line
              PanelModel.HeroLines(new[] { 300f }, 69, 0, 840).SequenceEqual(new[] { 0 }),
              "hero row wraps (0.7, PanelModel.HeroLines): a second number or the skill that does not fit the column goes to the next line, in order; never clipped");
        var shutPlate = PanelModel.PlateOf(shut);
        // B38: the top items in one row: the strip's right end carries the view switch and the About button, reserved first (the titles end in
        // "+N more" before it); too narrow: "Numbers" first, then the feat's note goes; never a second row
        Check(PanelModel.StripAbout(shutPlate) == shutPlate.Items.Single(b => b.Kind == "aboutnumbers") &&
              PanelModel.StripAbout(new Block { Kind = "plate", Items = new List<Block> { new Block { Kind = "hero" }, new Block { Kind = "aboutnumbers" } } }) == null &&
              PanelModel.StripFit(300, 400, 300, 120, 816) == (false, false) && PanelModel.StripFit(450, 400, 300, 120, 816) == (true, false) &&
              PanelModel.StripFit(600, 400, 300, 120, 816) == (true, true) &&
              PanelModel.TopSwitch(PanelModel.PlateOf(PanelModel.Build(world, new PanelState { Chapter = Chapter.Skills }))) is Block topSw && topSw.Kind == "switch",
              "top row (B38, PanelModel.StripFit): the switch and the About button ride at the strip's right end; too narrow, \"Numbers\" first, then the feat's note goes, never a second row");
        // the key line (MERGE-NOTES G2/G6): too wide, it leaves out the wheel, then Back; the page's own keys ([Y] Numbers, [K] Filter, [F] Category) stay
        var cookKeys = new[] { "[Q/E·A/D] Chapter", "[W/S] Page", "[K] Filter", "[Backspace] Back", "[Y] Numbers", "[T] About", "[H/Tab/Esc] Close", PanelModel.ScrollKey };
        string Line(int max) => PanelModel.KeyLine(cookKeys, l => l.Length <= max);
        Check(Line(200) == string.Join(PanelModel.KeyGap, cookKeys) && !Line(140).Contains("Scroll") && Line(140).Contains("[Backspace] Back") &&
              !Line(125).Contains("Back") && Line(125).Contains("[Y] Numbers") && Line(125).Contains("[K] Filter") && Line(125).Length <= 125 &&
              Line(60) == Line(125),
              "key line (PanelModel.KeyLine): too wide, it leaves out the wheel first, then Back; [Y] and the page's filter key always stay");
        // a section that is only its total, no item under it (Taming's Animals led, MERGE-NOTES G3): the shared placement labels it too
        var lone = new PanelView(); lone.Recorded = true;
        lone.Blocks.Add(new Block { Kind = "section", Title = "Animals led", Value = "3 km", Src = PanelModel.SrcPc, From = oct1.AddDays(2) });
        PanelModel.PlaceRecordedFrom(me, lone, Chapter.Deeds);
        Check(lone.Blocks[0].RecordedFrom == "Recorded from 3 October · this PC", "recorded label: a section that is only its total (no item under it) gets its own \"Recorded from\" line, with its own date");
        return fails;
    }
}
