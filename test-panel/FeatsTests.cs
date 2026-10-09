// Feats (ACHIEVEMENTS-06): the table, noticing a feat, the ledger and what travels in the snapshot, the hook rules, the Feats page
// with its two views and its one fixed detail area, Known for, the owner-page band, keys, dots, and the feats that wait for data.
// Called from Program.cs; Full() is the fictional sample (Rowan, Edda, Finch, Tor) with its feats.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FeatsTests
{
    public static readonly DateTime Now = new DateTime(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc);

    /// <summary>The fictional sample with its feats; fellows wired as PanelUi wires the group.</summary>
    public static PanelInput Full()
    {
        var full = PanelSample.FeatsFixture(Now);   // the hand-set ledger these tests assert; the previews read the derived world (World below)
        // the cargo and birth tallies other parts of Hearthwoven keep (cargoMeters, cargoStretch, bornInCare) are left out here, so these
        // tests read the same with or without them: Ore Road and Born in Your Care wait for their data, as on a build without those tallies
        foreach (var p in new[] { full }.Concat(full.Fellows))
            foreach (var kv in p.Events.Named().Where(n => n.Key == "cargoMeters" || n.Key == "cargoStretch" || n.Key == "bornInCare")) kv.Value.Clear();
        foreach (var f in full.Fellows) { f.PlayerNames = full.PlayerNames; f.Fellows = full.Fellows.Where(x => x != f).Concat(new[] { full }).ToList(); }
        return full;
    }

    /// <summary>The sample world (PanelSample.Full) with each book wired as PanelUi wires the group: the feats there come from its counters through the real rules.</summary>
    public static (PanelInput full, Dictionary<string, PanelInput> books) World(DateTime? at = null)
    {
        var full = PanelSample.Full(at ?? Now);
        var books = FullDump.Books(full).ToDictionary(b => b.who, b => b.book);
        return (full, books);
    }

    static bool FeatsPage(string page) => page == PanelModel.FeatsPageId || page == PanelModel.FeatsUnsungId || page == PanelModel.FeatsTogetherId;
    /// <summary>A page: the Feats chapter's own (earned, unsung, together) or, for any other page id, Deeds unless a chapter is given.</summary>
    static PanelView Page(PanelInput i, string page = "earned", Action<PanelState> more = null, Chapter? chapter = null)
    {
        var ch = chapter ?? (FeatsPage(page) ? Chapter.Feats : Chapter.Deeds);
        var st = new PanelState { Chapter = ch, Page = { [ch] = page } }; more?.Invoke(st);
        return PanelModel.Build(i, st);
    }
    static Block Find(PanelView v, string kind, Func<Block, bool> where = null) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind && (where == null || where(b)));
    static string Rules(Block detail) => string.Join(" | ", detail.Items.Where(x => x.Kind == "rule").Select(x => (x.Selected ? "[x] " : "[ ] ") + x.Value + " " + x.Title));


    /// <summary>The Feats previews for the HTML bridge (test-panel --dump): Earned with the first feat selected and the Ferryman, Unsung with
    /// an unearned feat's rule and with one that waits for its data, Deeds > Overview with Known for, an owner page with its band, and Tor's book.</summary>
    public static string DumpShots(string[] group, DateTime? at = null)
    {
        // fix3: the previews' one world (Program.cs now): a feat's day reads the same on Feats, on the owner page's band and on Known for
        var when = at ?? Now;
        var (full, books) = World(when);
        var tor = books["Tor"]; var edda = books["Edda"]; var finch = books["Finch"];
        var older = PanelSample.Full(when).Fellows.First(f => f.PlayerName == "Tor"); older.ViewerName = "Rowan"; older.Feats.BestsShared = false;   // a fellow from before the bests were shared: Heavy Keel says it is not counted
        PanelState Deeds(string page, string sel = null, string player = "")
        {
            var s = new PanelState { Chapter = Chapter.Deeds, Player = player }; if (page != null) s.Page[Chapter.Deeds] = page;
            return s;
        }
        PanelState Feats(string page, string sel = null, string player = "") =>
            new PanelState { Chapter = Chapter.Feats, Player = player, FeatSel = sel ?? "", Page = { [Chapter.Feats] = page } };
        (string name, PanelInput inp, PanelState st)[] shots = new (string name, PanelInput inp, PanelState st)[]
        {
            ("feats", full, Feats("earned")),
            ("feats-ferryman", edda, Feats("earned", "ferryman", "Edda")),
            ("feats-heavykeel", edda, Feats("earned", "heavykeel", "Edda")),
            ("feats-drover", full, Feats("earned", "drover")),
            ("feats-unsung-oreroad", finch, Feats("unsung", "oreroad", "Finch")),
            ("deeds-taming-led", full, Deeds("taming")),
            ("feats-unsung", full, Feats("unsung", "stoodfast")),
            ("feats-unsung-waiting", older, Feats("unsung", "heavykeel", "Tor")),
            ("feats-together", full, Feats("together")),
            ("feats-overview", full, Deeds(null)),
            ("feats-defense", tor, new PanelState { Chapter = Chapter.Battle, Player = "Tor", Page = { [Chapter.Battle] = "defense" } }),
            ("feats-tor", tor, Feats("earned", player: "Tor")),
            ("feats-tor-unsung", tor, Feats("unsung", "keptfires", "Tor")),
        }.Concat(Joost() is PanelInput j ? new[] { ("feats-joost-unsung", j, Feats("unsung", "keptfires")), ("feats-joost-earned", j, Feats("earned")), ("feats-joost-together", j, Feats("together")) } : new (string, PanelInput, PanelState)[0]).ToArray();
        return string.Join(",\n", shots.Select(s =>
        {
            var v = PanelModel.Build(s.inp, s.st);
            if (s.name.StartsWith("feats-joost", StringComparison.Ordinal)) PanelModel.AddPlayers(v, s.inp.PlayerName, new[] { s.inp.PlayerName }, "", true);   // his own book, alone that evening
            else PanelModel.AddPlayers(v, "Rowan", group, s.st.Player, true);
            return "  \"" + s.name + "\": " + PanelModel.ToJson(v);
        }));
    }
    // ---------- Joost's real book (feats-real): the 0.6 RC on his own character showed "0 of 16 earned" ----------

    /// <summary>
    /// Joost's own character as the 0.6 release candidate saw it on 9 Oct 2026, 15:08 (his book, under a fictional name and player id): his local totals file
    /// (test-panel/fixtures/sample-0.6rc-localtotals.json, copied from BepInEx/Hearthwoven/local with the name, player id and session id replaced) and the game's counters from his character
    /// save at the same moment (slot 0, the ones the feats or this test read). Alone on the server that evening: no fellow shared, no server book.
    /// </summary>
    public static PanelInput Joost()
    {
        var src = AppContext.BaseDirectory;
        while (src != null && !System.IO.File.Exists(System.IO.Path.Combine(src, "src", "Plugin.cs"))) src = System.IO.Path.GetDirectoryName(src);
        if (src == null) return null;
        var lt = LocalTotals.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(src, "test-panel", "fixtures", "sample-0.6rc-localtotals.json")));
        if (lt == null) return null;
        return new PanelInput
        {
            PlayerName = lt.Name, PlayerId = lt.PlayerId, IsSelf = true, NowUtc = new DateTime(2026, 10, 9, 13, 8, 7, DateTimeKind.Utc), ToLocal = u => u.AddHours(2),   // CEST
            InstalledUtc = lt.FirstRunUtc, CharacterMade = new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc),
            Events = lt.EventsBefore(""), Feats = lt.Feats, Fellows = new List<PanelInput>(), PlayerNames = new Dictionary<long, string>(),
            Character = new Dictionary<string, float>
            {
                ["Deaths"] = 10, ["EnemyHits"] = 1086, ["HitsTakenEnemies"] = 1375, ["TombstonesOpenedOwn"] = 8, ["TombstonesFit"] = 8,   // no TombstonesOpenedOther: he never opened a fellow's grave
                ["DistanceSail"] = 43822.1f, ["DistanceSailHelm"] = 37240.5f, ["CreatureTamed"] = 2, ["TamedPetting"] = 55, ["TamedCommand"] = 3,
                ["CraftFood"] = 520, ["CraftWeapon"] = 30, ["CraftArmor"] = 17, ["BossKills"] = 4, ["BossKillMultiplayer"] = 4, ["LeviathanSink"] = 1,
            },
        };
    }

    /// <summary>His real book: the 0 of 16 is true (every feat's number is under its first line), and every Unsung card now says why.</summary>
    static int RunJoost()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var j = Joost();
        Check(j != null && j.PlayerName == "Astrid" && j.InstalledUtc.HasValue && j.Feats != null && j.Feats.Primed, "joost: his real local totals load (Astrid, Hearthwoven since 8 Oct, the feats ledger primed and empty)");
        if (j == null) return fails;
        var own = PanelModel.FeatDefs.Where(d => !d.Group).ToList();
        var values = own.ToDictionary(d => d.Id, d => PanelModel.FeatValue(d, j));
        Check(own.All(d => PanelModel.FeatTier(d, j) == 0), "joost: no feat is earned on his real numbers (" + string.Join(", ", own.Select(d => d.Id + "=" + values[d.Id].ToString("0.##"))) + ")");
        Check(values["waymate"] == 0 && values["keptfires"] == 0 && values["turnedblades"] == 0 && values["arms"] == 0 && values["fulltable"] == 0,
              "joost: the zeros are real: no fellow's grave opened in his record, nothing into smelters and no parries since 8 Oct, his own food and gear on himself do not count");
        var unsung = PanelModel.Build(j, new PanelState { Chapter = Chapter.Feats, FeatSel = "keptfires", Page = { [Chapter.Feats] = "unsung" } });
        var cards = PanelModel.Content(unsung).First(b => b.Kind == "feats").Items;
        string NoteOf(string id) => cards.Single(c => c.Id == id).Note;
        Check(cards.Count == 16 && cards.All(c => !string.IsNullOrEmpty(c.Note)), "joost: all 16 Unsung cards say why they read what they read: " + string.Join("; ", cards.Select(c => c.Id + "=" + c.Note)));
        Check(NoteOf("keptfires") == "counting since 8 Oct" && NoteOf("shieldwall") == "counting since 8 Oct" && NoteOf("drover") == "counting since 8 Oct",
              "joost: a feat counted on this PC says it counts from his install day (8 Oct), not from the character's start");
        Check(new[] { "fulltable", "feastgiver", "arms", "ferryman" }.All(id => NoteOf(id) == PanelModel.NeedsFellows), "joost: a feat worked out from fellows says it needs fellows who share (none shared that evening)");
        Check(NoteOf("waymate") == "0 so far", "joost: Waymate reads his character's own record: 0 graves of fellows opened");
        Check(PanelModel.Content(unsung).Any(b => b.Kind == "featdetail" && b.Value == "Tier I at 1 000"), "joost: Kept the Fires' detail names only Tier I's target");
        var together = PanelModel.Build(j, new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = "together" } });
        Check(PanelModel.Content(together).First(b => b.Kind == "feats").Items.Single().Text == PanelModel.NeedsServer, "joost: Iron for the Forge says it needs the server on 0.6");
        return fails;
    }

    static string FeatBriefOf(FeatDef d, double tier) => d.Brief(tier).Replace("{your}", "their");
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- the table ----------
        var defs = PanelModel.FeatDefs;
        Check(defs.Select(d => d.Id).Distinct().Count() == defs.Length && defs.Length >= 15, "feats table: " + defs.Length + " feats, every id once");
        Check(defs.All(d => d.Tiers.Length >= 1 && d.Tiers.Length <= 3 && d.Tiers.Zip(d.Tiers.Skip(1), (a, b) => a < b).All(x => x)), "feats table: one to three tiers each, rising (never more than three)");
        Check(defs.All(d => !string.IsNullOrEmpty(d.Honours) && !string.IsNullOrEmpty(d.Source) && d.Rule(d.Tiers[0]).Length > 10 && !string.IsNullOrEmpty(d.Family)), "feats table: every feat has its warm line, its source words, a rule and an emblem family");
        var words = defs.SelectMany(d => new[] { d.Name, d.Honours, d.Caveat }.Concat(d.Tiers.Select(d.Rule))).Where(s => s != null).ToList();
        Check(words.All(s => !s.Contains("—") && !s.Contains("–") && s.IndexOf("at least", StringComparison.OrdinalIgnoreCase) < 0 && s.IndexOf("achievement", StringComparison.OrdinalIgnoreCase) < 0 &&
                             s.IndexOf("unlock", StringComparison.OrdinalIgnoreCase) < 0 && s.IndexOf("friend", StringComparison.OrdinalIgnoreCase) < 0),
              "feats words: no dash, no \"at least\", never \"achievement\" or \"unlock\" (the game's and the platform's words), \"fellow players\" never \"friends\"");
        var usedFamilies = defs.Select(d => d.Family).Distinct().ToList();
        Check(usedFamilies.All(f => PanelModel.SagaTitles.Any(t => t.Id == f) || f == "helper"), "feats table: every feat sits under a title family's emblem (Waymate has no title yet: the helper's)");
        Check(PanelModel.FeatDefs.Where(d => d.Needs != null).Select(d => d.Id).OrderBy(x => x).SequenceEqual(new[] { "born", "drover", "heavykeel", "ironforge", "longlead", "oreroad" }),
              "feats table: Ore Road, Heavy Keel, Drover, Long Lead, Born in Your Care and Iron for the Forge (the server's book) wait for their data key");

        // fix3: every feat's progress names its unit, every unsung tile has its rule in a few words, one date wherever a feat is told
        Check(defs.All(d => d.Brief != null && d.Tiers.All(t => FeatBriefOf(d, t).Length <= 22)) && defs.Where(d => d.Exact).All(d => !string.IsNullOrEmpty(d.Unit)),
              "feats units: every feat has a brief rule of at most 22 characters, every exact feat a unit for its progress");
        Check(PanelModel.FeatProgress(PanelModel.FeatById("drover"), 1.64, 5) == "1.6 of 5 km" && PanelModel.FeatProgress(PanelModel.FeatById("stoodfast"), 8750, 10000) == "8\u00A0750 of 10\u00A0000 damage" &&
              PanelModel.FeatProgress(PanelModel.FeatById("waymate"), 2, 3) == "2 of 3 graves" && PanelModel.FeatProgress(PanelModel.FeatById("forsaken"), 0, 1) == "0 of 1 Forsaken parry",
              "feats units: a distance has one decimal and its unit, damage and graves their words, one in the singular");
        {
            var (world, _) = World();
            var cards = Find(Page(world), "feats").Items;
            var told = new List<string>();
            foreach (var d in defs.Where(d => PanelModel.FeatTier(d, world) > 0))
            {
                var card = cards.FirstOrDefault(c => c.Id == d.Id); var onBand = PanelModel.FeatBand(world, d.Chapter, d.Page)?.Items.FirstOrDefault(i => i.Id == d.Id);
                if (card == null || onBand == null || card.Text != onBand.Text) told.Add(d.Id + ": " + card?.Text + " vs " + onBand?.Text);
            }
            Check(told.Count == 0, "feats dates: a feat's day on the Feats page is the same on its owner page's band (one ledger, one source)" + (told.Count > 0 ? ": " + string.Join("; ", told) : ""));
        }

        // ---------- the sample, Deeds list and the page ----------
        var full = Full();
        var feats = Page(full);
        Check(feats.Chapters.Select(c => c.Id).Take(3).SequenceEqual(new[] { "Deeds", "Feats", "Company" }) && feats.Chapters.Single(c => c.Id == "Feats").Selected && feats.Chapters.Single(c => c.Id == "Feats").Icon == PanelModel.FeatsIcon,
              "chapter: Feats is a chapter of its own, its tab right after Deeds, with the feats medallion");
        Check(feats.List.Select(l => l.Id + ":" + l.Label).SequenceEqual(new[] { "earned:Earned", "unsung:Unsung", "together:Together" }) && feats.ListTitle == "Feats" && !PanelModel.ListOf(full, Chapter.Deeds).Any(l => l.Id == "feats"),
              "chapter: its own left list (Earned, Unsung, Together); Deeds no longer has a Feats entry");
        var qe = new PanelState(); PanelModel.StepChapter(qe, 1); var qe2 = new PanelState { Chapter = Chapter.Feats }; PanelModel.StepChapter(qe2, 1); var qe3 = new PanelState { Chapter = Chapter.Skills }; PanelModel.StepChapter(qe3, 1);
        Check(qe.Chapter == Chapter.Feats && qe2.Chapter == Chapter.Company && qe3.Chapter == Chapter.Deeds, "chapter: Q/E follow the tab row: Deeds, Feats, Company ... Skills, then Deeds again");
        Check(feats.Page == "earned" && feats.Heading == "Earned feats" && PanelModel.PlateOf(feats)?.Title == "Earned feats" && PanelModel.PlateOf(feats).Tone == PanelModel.PlateFull && Find(feats, "switch") == null,
              "page: Earned sits on a full plate (it fills the room like Cooking), no Earned / Unsung switch (the list says it)");
        Check(PanelModel.PlateOf(feats).Text == PanelModel.FeatsDefinition + " 8 of 16 earned." && PanelModel.PlateOf(Page(full, "unsung")).Text.StartsWith(PanelModel.UnsungDefinition) && !PanelModel.FeatsDefinition.Contains("rank"),
              "page: one plain line says what a feat is (or what Unsung means) and how many of yours are earned: " + PanelModel.PlateOf(feats).Text);
        var grid = Find(feats, "feats");
        Check(grid.Items.Select(c => c.Title).SequenceEqual(new[] { "Kept the Fires", "Heavy Keel", "Ferryman", "Shield Wall", "Unbroken", "Drover", "Long Lead", "Waymate" }),
              "page: Earned lists the eight earned feats in the table's order (title families), not by size or rarity: " + string.Join(", ", grid.Items.Select(c => c.Title)));
        Check(grid.Items.All(c => c.Tone == "earned" && c.Level >= 1 && c.Icon == PanelModel.FeatIcon(PanelModel.FeatById(c.Id))), "page: earned cards carry their feat's picture (the family emblem, or Codex's own line picture: Waymate, the young, the lead rope) and their tier");
        Check(grid.Items.Count(c => c.Selected) == 1 && grid.Items[0].Selected, "page: the first card is selected on open, so the detail area is never empty");
        var detail = Find(feats, "featdetail");
        Check(detail != null && detail.Id == grid.Items[0].Id && detail.Title == "Kept the Fires" && detail.Value == "Tier I · next: II at 5 000" && detail.Colour == PanelModel.TierColours[1], "detail: one fixed area under the grid shows the selected feat (name, tier in bronze)");
        Check(Rules(detail) == "[x] I Put 1 000 ore and fuel into smelters, kilns and furnaces. | [ ] II Put 5 000 ore and fuel into smelters, kilns and furnaces.",
              "detail: the tier reached (ticked) and only the next one, never the tiers beyond it (Joost 2026-10-09): " + Rules(detail));
        var progress = detail.Items.FirstOrDefault(x => x.Kind == "progress");
        Check(progress != null && progress.Title == "1 377 of 5 000 ore and fuel" && Math.Abs(progress.Fraction - 1377f / 5000f) < 1e-3, "detail: your progress on your own page, toward the next tier (exact source)");
        Check(detail.Items.Any(x => x.Kind == "counted" && x.Text == PanelModel.CountedPc) && detail.Items.Any(x => x.Kind == "caveat" && x.Text.Contains("feeder chest")) &&
              detail.Items.Any(x => x.Kind == "moment" && x.Text == "Tier I earned 6 Oct in the Mountains" && x.Note == null),
              "detail: counted by (the zones' words), the caveat, the earned moment with date and biome");
        var ferryman = grid.Items.Single(c => c.Title == "Ferryman");
        Check(ferryman.Text == "5 Oct or earlier" && ferryman.Items[0].Items.Any(x => x.Kind == "moment" && x.Text == "Tier " + PanelModel.Numeral((int)ferryman.Level) + " earned 5 Oct" && x.Note == "(or earlier)") && !ferryman.Items[0].Items.Any(x => x.Kind == "progress") && ferryman.Items[0].Items.Any(x => x.Kind == "counted" && x.Text == PanelModel.CountedFellows),
              "detail: a feat worked out from fellow players' copies says \"Earned 5 Oct\" with a quiet \"(or earlier)\" (short, honest) and shows no progress");
        var waymate = grid.Items.Single(c => c.Title == "Waymate");
        Check(waymate.Text == "before install" && waymate.Items[0].Items.Any(x => x.Kind == "moment" && x.Text == "Earned before install" && x.Note == "(Hearthwoven counts from 2 Oct)"), "detail: a character's old record: " + waymate.Text + " | " + string.Join(";", waymate.Items[0].Items.Select(x => x.Text + " " + x.Note)));
        // ---------- tiers revealed one step at a time (Joost 2026-10-09): the reached ones and only the next ----------
        var fires = grid.Items.Single(c => c.Id == "keptfires");
        Check(fires.Level == 1 && fires.Count == 3 && fires.Note == "next: II at 5 000" && grid.Items.Where(c => c.Count > 1 && c.Level >= c.Count).All(c => c.Note == null) && grid.Items.Where(c => c.Count == 1).All(c => c.Note == null),
              "tiers: an earned card names only the next tier (\"" + fires.Note + "\"); a feat with every tier, or a one-off, names none");
        Check(grid.Items.All(c => Rules(c.Items[0]).Split('|').Length == Math.Min(c.Count, c.Level + 1)) && Find(Page(full, "unsung"), "feats").Items.Where(c => c.Count > 1).All(c => Rules(c.Items[0]).Split('|').Length == 1 && c.Items[0].Value == "Tier I at " + PanelModel.Number(PanelModel.FeatById(c.Id).Tiers[0])),
              "tiers: the detail lists the tiers reached and the next one only; an Unsung feat with tiers says \"Tier I at ...\" and shows its first rule alone");
        Check(PanelModel.FeatTierLine(PanelModel.FeatById("keptfires"), 3) == "Tier III · every tier earned" && PanelModel.FeatTierLine(PanelModel.FeatById("keptfires"), 0) == "Tier I at 1 000" && PanelModel.FeatTierLine(PanelModel.FeatById("stoodfast"), 0) == "" && PanelModel.FeatNextLine(PanelModel.FeatById("keptfires"), 2) == "next: III at 20 000",
              "tiers: the words for no tier, a tier with one ahead, and every tier");
        Check(grid.Items.Single(c => c.Title == "Shield Wall").Text == "4 Oct · Black Forest", "card: the moment on one small line, date and biome");

        // the Unsung view: the rule of an unearned feat, honest captions
        var unsung = Page(full, "unsung");
        var ug = Find(unsung, "feats");
        Check(ug.Items.All(c => c.Level == 0 && (c.Tone == "unsung" && !string.IsNullOrEmpty(c.Text) && c.Text.Length <= 22 || c.Tone == PanelModel.FeatWaitingTone && c.Text == PanelModel.NotCountedShort)) && ug.Items.Count == PanelModel.OwnFeatCount - 8, "unsung: the other " + ug.Items.Count + " feats, grey, each with its rule in a few words on the tile (the ones not counted yet say so)");
        Check(ug.Items.Where(c => c.Tone == PanelModel.FeatWaitingTone).Select(c => c.Id).OrderBy(x => x).SequenceEqual(defs.Where(d => !d.Group && PanelModel.FeatWaiting(d, full)).Select(d => d.Id).OrderBy(x => x)) && ug.Items.Single(c => c.Title == "Stood Fast").Tone == "unsung",
              "unsung: exactly the feats whose data is not counted yet carry the waiting tone and the line \"not counted yet\"");
        // a fellow whose sender is from before the bests were shared: Heavy Keel and Long Lead have no data key from them (older version)
        var older = Full().Fellows.First(f => f.PlayerName == "Tor"); older.Feats.BestsShared = false;
        var wait = Find(Page(older, "unsung"), "feats").Items.Single(c => c.Title == "Heavy Keel");
        var waitDetail = wait.Items[0];
        Check(waitDetail.Tone == PanelModel.FeatWaitingTone && waitDetail.Items.Single(x => x.Kind == "counted").Text == PanelModel.CountedNotYet && waitDetail.Items.Any(x => x.Kind == "caveat" && x.Text == PanelModel.NotCountedYet) && !waitDetail.Items.Any(x => x.Kind == "progress" || x.Kind == "moment") && Rules(waitDetail).Contains("Steer 2 km with 100 metal or ore items aboard."),
              "unsung: a feat whose data is not counted yet shows its rule and says Hearthwoven does not count this yet (reads 0, never a made-up number)");
        // ---------- why an Unsung feat reads what it reads (feats-real: Joost's real book showed "0 of 16" with no reason) ----------
        var stoodCard = ug.Items.Single(c => c.Title == "Stood Fast");
        Check(stoodCard.Note == "4 180 so far · since 2 Oct" && ug.Items.Where(c => c.Tone == "unsung").All(c => !string.IsNullOrEmpty(c.Note)) && ug.Items.Where(c => c.Tone == PanelModel.FeatWaitingTone).All(c => c.Note == null),
              "unsung: every card says why on a second small line (counted on this PC since the install day, so far): " + string.Join("; ", ug.Items.Select(c => c.Title + "=" + c.Note)));
        var stood = stoodCard.Items[0];
        Check(stood.Items.Single(x => x.Kind == "progress").Title == "4 180 of 10 000 damage" && stood.Value == "" && Rules(stood) == "[ ]  Stop 10 000 damage with your shield.",
              "unsung: a one-off feat shows its single rule and progress: [" + stood.Items.Single(x => x.Kind == "progress").Title + "] [" + stood.Value + "] [" + Rules(stood) + "]");
        var unsungPicked = Page(full, "unsung", s => s.FeatSel = "turnedblades");
        Check(Find(unsungPicked, "featdetail").Title == "Turned Blades" && Find(unsungPicked, "feats").Items.Single(c => c.Selected).Id == "turnedblades" &&
              Find(unsungPicked, "featdetail").Items.Single(x => x.Kind == "caveat").Text.Contains("Tower shields never parry"),
              "unsung: the detail area follows the selection (the Unsung view with an unearned feat's rule)");
        var strayed = Page(full, more: s => s.FeatSel = "turnedblades");   // a selection from the other view: this view takes its first card
        Check(Find(strayed, "featdetail").Id == Find(strayed, "feats").Items[0].Id, "page: a selection that is not in the view shown falls back to the view's first card");

        // ---------- keys: A/D choose, F flips, the footer says so ----------
        Check(feats.Keys.Contains("[A/D] Feat") && feats.Keys.Contains("[Q/E] Chapter") && feats.Keys.Contains("[W/S] Page") && !feats.Keys.Any(k => k.Contains("Q/E·A/D")) && !feats.Keys.Any(k => k.Contains("View")),
              "keys: the Feats chapter's footer: [Q/E] Chapter, [W/S] Page (Earned, Unsung, Together), [A/D] Feat, no view key: " + string.Join("  ", feats.Keys));
        Check(!Page(full, "cooking").Keys.Contains("[A/D] Feat") && Page(full, "cooking").Keys.Contains("[Q/E·A/D] Chapter"), "keys: other pages keep A/D for the chapters");
        var step = new PanelState { Chapter = Chapter.Feats };
        var seen = new List<string>();
        for (int k = 0; k < 9; k++) { var v = PanelModel.Build(full, step); PanelModel.StepFeat(step, v, 1); seen.Add(step.FeatSel); }
        Check(seen.SequenceEqual(new[] { "heavykeel", "ferryman", "shieldwall", "unbroken", "drover", "longlead", "waymate", "keptfires", "heavykeel" }), "keys: D moves to the next card, left to right, and wraps from the last to the first: " + string.Join(", ", seen));
        var back = new PanelState { Chapter = Chapter.Feats };
        PanelModel.StepFeat(back, PanelModel.Build(full, back), -1);
        Check(back.FeatSel == "waymate", "keys: A from the first card wraps to the last");
        Check(Find(unsung, "feats").Tone == "unsung" && !PanelModel.StepView(new PanelState { Chapter = Chapter.Feats }, feats, 1), "keys: Unsung is a list entry (W/S), no view switch to flip");
        Check(!PanelModel.StepFeat(new PanelState(), new PanelView(), 1), "keys: a page without feat cards has nothing to step");

        // ---------- a fellow's Feats: what they earned with their moments, the rest greyed with the rule, no progress ----------
        var tor = full.Fellows.First(f => f.PlayerName == "Tor");
        var torFeats = Page(tor);
        var tg = Find(torFeats, "feats");
        Check(tg.Items.Select(c => c.Title).SequenceEqual(new[] { "Full Table", "Shield Wall", "Stood Fast", "Unbroken", "Turned Blades" }), "fellow: Tor's earned feats from what he shared: " + string.Join(", ", tg.Items.Select(c => c.Title)));
        var shield = tg.Items.Single(c => c.Title == "Shield Wall");
        Check(shield.Level == 2 && shield.Text == "4 Oct · Black Forest" && shield.Items[0].Items.Single(x => x.Kind == "moment").Text == "Tier II earned 4 Oct in the Black Forest", "fellow: the tier and the moment travel in the snapshot");
        var torUnsung = Find(Page(tor, "unsung"), "feats");
        Check(torUnsung.Items.All(c => !c.Items[0].Items.Any(x => x.Kind == "progress")) && torUnsung.Items.Any(c => c.Title == "Kept the Fires"),
              "fellow: their Unsung feats show the rule and no progress (no comparison, their counts can lag a session)");
        Check(torFeats.Scope != null && torFeats.Scope.Contains("Tor") || PanelModel.PlateOf(torFeats).Text.Contains("Tor"), "fellow: the page says whose copy it is");
        Check(Rules(shield.Items[0]).Contains("with a fellow player within 15 m.") && Rules(shield.Items[0]).Contains("on their shield") && shield.Items[0].Items.Single(x => x.Kind == "counted").Text == "Since install · Tor's PC" && Rules(Find(Page(full, more: s => s.FeatSel = "shieldwall"), "featdetail")).Contains("on your shield"),
              "fellow: a rule is said for whose book it is (their shield, Tor's PC), on your own page yours (your shield)");

        // ---------- Known for, the owner-page band, the dots ----------
        var overview = Page(full, "overview");
        var known = Find(overview, "knownfor");
        Check(known != null && known.Title == "Known for" && known.Id == "Feats/earned" && known.Items.Count == 3 && known.Items.All(i => i.Colour == PanelModel.TierColour((int)i.Level, i.Count)), "known for: Deeds > Overview has one line with up to three feats");
        Check(known.Items.Select(i => i.Title).SequenceEqual(new[] { "Drover", "Unbroken", "Kept the Fires" }),
              "known for: the highest tier first, then the most recent (no comparison with anyone else): " + string.Join(", ", known.Items.Select(i => i.Title)));
        Check(PanelModel.Content(overview).TakeWhile(b => b.Kind != "cards").Any(b => b.Kind == "knownfor"), "known for: above the deed cards on the Earned view");
        Check(Find(Page(full, "overview", s => s.View["Deeds/overview/view"] = "unsung"), "knownfor") == null, "known for: not on the Unsung view");
        var none = Full(); none.Feats = new FeatsLedger(); none.Events = new SessionEvents(); none.Character = new Dictionary<string, float>(); none.Fellows = null;
        Check(Find(Page(none, "overview"), "knownfor") == null && Find(Page(none), "note")?.Text == PanelModel.FeatsNone, "known for: nobody earned anything: no line, and the Feats page says so");
        var band = Find(Page(full, "defense", chapter: Chapter.Battle), "featband");
        Check(band != null && band.Items.Select(i => i.Title).SequenceEqual(new[] { "Shield Wall", "Unbroken" }) && band.Items[0].Value == "I" && band.Items[0].Text == "4 Oct · Black Forest",
              "band: Battle > Defence shows its feats under the heading, tier and moment: " + (band == null ? "none" : string.Join(", ", band.Items.Select(i => i.Title))));
        Check(band.Items.Count > 0 && band.Items.All(i => i.Note == PanelModel.FeatLabel), "band: every feat chip on an owner page carries the small \"Feat\" tag");
        Check(PanelModel.PlateOf(Page(full, "defense", chapter: Chapter.Battle)).Items[0].Kind == "featband" && PanelModel.PlateOf(Page(full, "defense", chapter: Chapter.Battle)).Items.Skip(1).Any(b => b.Kind == "zone"),
              "band: on the plate above the zones, not inside one");
        Check(Find(Page(full, "cooking"), "featband") == null && Find(Page(full, "sailing", chapter: Chapter.Voyages), "featband").Items.Single().Title == "Ferryman" && Find(Page(full, "smelters", chapter: Chapter.Stores), "featband").Items.Single().Title == "Kept the Fires" &&
              Find(Page(full, "deaths", chapter: Chapter.Battle), "featband").Items.Single().Title == "Waymate",
              "band: each page shows only its own feats (Voyages > Sailing the Ferryman, Hall > Smelters the fires, Battle > Deaths the Waymate)");
        Check(Find(Page(tor, "defense", chapter: Chapter.Battle), "featband").Items.Select(i => i.Title).SequenceEqual(new[] { "Shield Wall", "Stood Fast", "Unbroken", "Turned Blades" }), "band: a fellow's owner page shows their feats too");
        var dotted = Page(full, "overview");
        Check(dotted.Chapters.Single(c => c.Id == "Feats").Dot && !dotted.Chapters.Single(c => c.Id == "Deeds").Dot && !dotted.Chapters.Single(c => c.Id == "Battle").Dot && Page(full, "unsung").List.Single(l => l.Id == "earned").Dot,
              "dots: a gold dot on the Feats tab (and, inside it, on Earned) while earned feats are not seen");
        var onFeats = Page(full);
        Check(PanelModel.OnFeatsPage(onFeats) && !onFeats.Chapters.Any(c => c.Dot) && !onFeats.List.Any(l => l.Dot), "dots: the Earned page itself carries none (the page you are reading answers its own dot)");
        // fix4: every feat shows whole in its grid (four rows of four, no row cut), and a grid that has more says so (PanelModel.FeatsMore)
        Check(Math.Ceiling(PanelModel.OwnFeatCount / 4.0) * 52 + (Math.Ceiling(PanelModel.OwnFeatCount / 4.0) - 1) * 6 <= PanelModel.FeatsGridRoom && PanelModel.FeatsMore.Contains("wheel") && PanelModel.FeatsMore.Contains("A/D"),
              "feats: all " + PanelModel.OwnFeatCount + " feats fit the grid's room in whole rows at four across (52 px cards, 6 px apart), and a longer grid names the wheel and A/D as the way on");
        Check(PanelModel.SwitchOf(dotted)?.KeyCap == "F" && PanelModel.SwitchOf(Page(full)) == null && PanelModel.SwitchOf(Page(full, "defense", chapter: Chapter.Battle))?.KeyCap == null,
              "feats: the view key's cap stays at Deeds > Overview's Earned / Unsung switch; the Feats chapter has none");

        // ---------- the tiers' colours (Joost 2026-10-09): bronze, silver, gold, on cards, the strip and the detail ----------
        Check(PanelModel.TierColour(1, 3) == "#c27a4a" && PanelModel.TierColour(2, 3) == "#c9d2da" && PanelModel.TierColour(3, 3) == "#f0c862" && PanelModel.TierColour(1, 1) == "#f0c862" && PanelModel.TierColour(0, 3) == null,
              "tiers: I bronze, II silver, III gold; a one-off feat earned is gold (earned in full); nothing earned, no colour");
        var palette = PanelModel.PlayerPalette.Concat(new[] { "#6b7076", "#aab6c2", "#e8dcbc", "#e2552a", "#2f9bff", "#f2cf2e", "#3ccf6e", "#9b6cf0" }).ToList();
        Check(PanelModel.TierColours.Skip(1).All(c => !palette.Contains(c)), "tiers: no tier colour is a player's colour or a damage type's");
        Check(grid.Items.All(c => c.Colour == PanelModel.TierColour((int)c.Level, c.Count)) && detail.Items.Where(x => x.Kind == "rule").All(r => r.Selected ? r.Colour != null : r.Colour == null) &&
              band.Items.All(i => i.Colour == PanelModel.TierColour((int)i.Level, i.Count)) && ug.Items.All(c => c.Colour == null),
              "tiers: every earned card, every reached rule, every strip chip carries its tier's colour; unsung cards none");

        // ---------- Together: the group's feat (Iron for the Forge) from the server's book ----------
        {
            var (world, books) = World();
            var together = Page(world, "together");
            var g = Find(together, "feats"); var gd = Find(together, "featdetail");
            var forge = PanelModel.FeatById("ironforge");
            Check(g.Items.Select(c => c.Id).SequenceEqual(new[] { "ironforge" }) && together.Heading == PanelModel.FeatsTogetherHeading && PanelModel.PlateOf(together).Text == PanelModel.TogetherDefinition,
                  "together: the group's feats on their own page (Iron for the Forge), with one line on what a group feat is");
            var unload = new[] { world }.Concat(world.Fellows).Where(p => p.Book != null).GroupBy(p => p.PlayerName).Select(x => x.First().Book).Sum(b => b.Delivered.Where(kv => CargoVoyage.MetalOre.Contains(kv.Key)).Sum(kv => kv.Value)) / 1000.0;
            Check(Math.Abs(PanelModel.FeatValue(forge, world) - unload) < 1e-6 && unload > 1000 && gd.Items.Single(x => x.Kind == "progress").Title == PanelModel.FeatProgress(forge, unload, 10000) && gd.Items.Single(x => x.Kind == "counted").Text == PanelModel.CountedServer,
                  "together: Iron for the Forge counts the ore and metal the server's book saw come off ships and carts, everyone's summed: " + gd.Items.Single(x => x.Kind == "progress").Title);
            Check(gd.Items.Single(x => x.Kind == "crew").Items.Select(i => i.Title).SequenceEqual(new[] { "Edda", "Rowan", "Tor" }) && gd.Items.Single(x => x.Kind == "crew").Items.All(i => i.Icon == "person:" + i.Title && i.Value == null),
                  "together: who carried, by name with their shield, no number each (no comparison; the credit rule is Joost's call)");
            var tg2 = Find(Page(books["Tor"], "together"), "featdetail");
            Check(tg2.Items.Single(x => x.Kind == "progress").Title == gd.Items.Single(x => x.Kind == "progress").Title, "together: the same group number on a fellow's book");
            Check(!PanelModel.VisibleFeats(Page(world)).Any(c => c.Id == "ironforge") && !PanelModel.VisibleFeats(Page(world, "unsung")).Any(c => c.Id == "ironforge") && (PanelModel.FeatsKnownFor(world)?.Items.All(i => i.Id != "ironforge") ?? true),
                  "together: never in a player's own Earned or Unsung, never in Known for");
            var alone = Full(); alone.Book = null; foreach (var f in alone.Fellows) f.Book = null;
            var lone = Find(Page(alone, "together"), "feats").Items.Single();
            Check(lone.Tone == PanelModel.FeatWaitingTone && lone.Text == PanelModel.NeedsServer && lone.Items[0].Items.Any(x => x.Kind == "caveat" && x.Text == PanelModel.NeedsServerCaveat),
                  "together: no server book anywhere (a server before 0.6): the feat waits and says it needs the server on 0.6, never a made-up number");
        }
        full.Feats.MarkSeen();
        var seenAll = Page(full);
        Check(!seenAll.Chapters.Any(c => c.Dot) && !seenAll.List.Any(l => l.Dot) && !Page(tor).Chapters.Any(c => c.Dot), "dots: gone once the page was opened; a fellow's book never shows one");

        // ---------- the page leaves the other pages alone ----------
        Check(Find(feats, "zone") == null, "page: no zones on the Feats page (no counts of one kind)");

        // ---------- noticing a feat ----------
        var p = new PanelInput { PlayerName = "Rowan", PlayerId = 11, IsSelf = true, Events = new SessionEvents(), Character = new Dictionary<string, float>(), Feats = new FeatsLedger(), InstalledUtc = Now.AddDays(-5), Fellows = new List<PanelInput>() };
        SessionEvents.Add(p.Events.SmelterAdded, "smelter|CopperOre", 1500);   // already over the first line at the very first look
        p.Character["TombstonesOpenedOther"] = 3;
        var fresh = PanelModel.EvaluateFeats(p, Now, "Meadows", "on foot");
        Check(fresh.Select(f => f.feat.Id + f.tier).OrderBy(x => x).SequenceEqual(new[] { "keptfires1", "waymate1" }) && p.Feats.Primed, "noticing: the first look records what is already over a line, then is primed");
        Check(p.Feats.Moment("waymate").Value.Before && !p.Feats.Moment("keptfires").Value.Before && p.Feats.Moment("keptfires").Value.Noticed && p.Feats.Moment("keptfires").Value.Utc == Now,
              "noticing: a character's old record is \"before install\"; Hearthwoven's own count first seen at the first look is \"noticed\" (by that day), never a made-up day");
        Check(PanelModel.EvaluateFeats(p, Now.AddMinutes(1), "Meadows", null).Count == 0, "noticing: a second look adds nothing");
        // the feats came five days after the install (0.6 on a PC that ran 0.5): the record crossed the line before that first look, maybe after the install
        Check(p.Feats.Moment("waymate").Value.Utc == Now && PanelModel.FeatMomentLine(p, p.Feats.Moment("waymate").Value) == PanelModel.FeatMomentLine(p, new FeatMoment { Utc = Now, Noticed = true }) &&
              PanelModel.FeatMomentSentence(p, p.Feats.Moment("waymate").Value).EndsWith(PanelModel.OrEarlier),
              "noticing: a character's record first looked at days after the install is \"by that day (or earlier)\", never \"before install\": " + PanelModel.FeatMomentSentence(p, p.Feats.Moment("waymate").Value));
        var sameDay = new PanelInput { IsSelf = true, InstalledUtc = Now.AddHours(-1), NowUtc = Now };
        Check(PanelModel.FeatMomentLine(sameDay, new FeatMoment { Before = true, Utc = Now }) == "before install", "noticing: looked at on the install day itself, it is \"before install\"");
        SessionEvents.Add(p.Events.SmelterAdded, "smelter|CopperOre", 4000);   // 5 500: tier II
        var later = PanelModel.EvaluateFeats(p, Now.AddHours(3), "Swamp", "on foot");
        var m2 = p.Feats.Moment("keptfires", 2).Value;
        Check(later.Count == 1 && later[0].tier == 2 && m2.Utc == Now.AddHours(3) && m2.Biome == "Swamp" && m2.Place == "on foot" && !m2.Noticed && !m2.Before && p.Feats.Tier("keptfires") == 2 && p.Feats.Moment("keptfires", 1).Value.Noticed,
              "noticing: a later crossing is recorded with its date, the biome under the player's feet and a coarse place; earlier tiers keep theirs");
        var skip = new PanelInput { PlayerName = "Rowan", IsSelf = true, Events = new SessionEvents(), Feats = new FeatsLedger { Primed = true } };
        SessionEvents.Add(skip.Events.SmelterAdded, "smelter|x", 30000);
        PanelModel.EvaluateFeats(skip, Now, "None", null);
        Check(skip.Feats.Tier("keptfires") == 3 && skip.Feats.Moment("keptfires", 1).Value.Biome == null && skip.Feats.TiersEarned == 3, "noticing: a jump over several tiers records each tier; the biome None is not kept");
        var other = new PanelInput { PlayerName = "Tor", IsSelf = false, Events = new SessionEvents(), Feats = new FeatsLedger() };
        SessionEvents.Add(other.Events.SmelterAdded, "smelter|x", 3000);
        Check(PanelModel.EvaluateFeats(other, Now, "Meadows", null).Count == 0 && other.Feats.TiersEarned == 0, "noticing: a fellow's copy is never written to");
        Check(PanelModel.EvaluateFeats(new PanelInput { IsSelf = true, Events = new SessionEvents() }, Now, "Meadows", null).Count == 0, "noticing: no ledger (local totals not loaded): nothing, no error");

        // derived from fellows' copies: noticed, matched by name, only fellows who share
        var rowan = Full();
        rowan.Feats.Earned.Remove("ferryman");   // noticed afresh below
        var food = PanelModel.EvaluateFeats(rowan, Now.AddDays(1), "Swamp", null);
        Check(food.Select(f => f.feat.Id).SequenceEqual(new[] { "ferryman" }) && rowan.Feats.Moment("ferryman").Value.Noticed && rowan.Feats.Moment("ferryman").Value.Biome == null,
              "derived: Ferryman I from Edda's 2.5 hours under his helm, noticed rather than seen: " + string.Join(", ", food.Select(f => f.feat.Id + f.tier)));
        SessionEvents.Add(rowan.Fellows.First(f => f.PlayerName == "Finch").Events.AteFoodMadeBy, "Rowan|Bread", 2);
        Check(PanelModel.EvaluateFeats(rowan, Now.AddDays(2), "Swamp", null).Select(f => f.feat.Id).SequenceEqual(new[] { "fulltable" }),
              "derived: Full Table when a third different fellow player enjoys his food (Edda, Tor, Finch)");
        SessionEvents.Add(rowan.Fellows.First(f => f.PlayerName == "Finch").Events.EquippedGearMadeBy, "Rowan|AxeBronze", 1);
        Check(PanelModel.EvaluateFeats(rowan, Now.AddDays(3), "Swamp", null).Select(f => f.feat.Id).SequenceEqual(new[] { "arms" }), "derived: Arms for the Hall at three different fellow players");
        SessionEvents.Add(rowan.Fellows.First(f => f.PlayerName == "Tor").Events.AteFromFeastOf, "11|FeastMeadows", 17);   // 3 + 17 = 20 servings from his feasts
        Check(PanelModel.EvaluateFeats(rowan, Now.AddDays(4), "Swamp", null).Select(f => f.feat.Id).SequenceEqual(new[] { "feastgiver" }), "derived: Feast-Giver at 20 servings from the feasts with his id");
        SessionEvents.Add(rowan.Fellows.First(f => f.PlayerName == "Edda").Events.SailedUnderHelmOf, "rowan", 3600f * 2.6f);   // 2.5 + 2.6 = 5.1 hours (names match without case)
        var tier2 = PanelModel.EvaluateFeats(rowan, Now.AddDays(5), "Swamp", null);
        Check(tier2.Count == 1 && tier2[0].feat.Id == "ferryman" && tier2[0].tier == 2, "derived: Ferryman II at five hours in all, the name matched without case");
        var lonely = Full(); lonely.Fellows = null; lonely.Feats = new FeatsLedger { Primed = true };
        Check(!PanelModel.EvaluateFeats(lonely, Now, "Meadows", null).Any(f => f.feat.Derived), "derived: without fellows who share nothing is derived, nothing made up");

        // ---------- the hooks' rules ----------
        var ledger = new FeatsLedger(); FeatsCounters.Streak = 0;
        FeatsCounters.OnBlock(ledger, parry: false, bossAttacker: false, fellowNear: true, stopped: 40f);
        FeatsCounters.OnBlock(ledger, parry: true, bossAttacker: true, fellowNear: false, stopped: 60f);
        Check(ledger.Count(FeatsCounters.BlocksNear) == 1 && ledger.Count(FeatsCounters.Stopped) == 100 && ledger.Count(FeatsCounters.BossParries) == 1 && FeatsCounters.Streak == 1 && ledger.Count(FeatsCounters.BestStreak) == 1,
              "hooks: a held block counts near a fellow only when one is within 15 m; damage stopped adds up; a parry from a boss counts once");
        for (int k = 0; k < 9; k++) FeatsCounters.OnBlock(ledger, true, false, false, 0f);
        Check(ledger.Count(FeatsCounters.BestStreak) == 10 && FeatsCounters.Streak == 10, "hooks: ten parries in a row make the streak");
        FeatsCounters.OnHurt(hadAttacker: false, damage: 12f);   // burning or poison: no attacker
        FeatsCounters.OnHurt(hadAttacker: true, damage: 0f);     // fully blocked: nothing reached you
        Check(FeatsCounters.Streak == 10, "hooks: burning, poison and a block that let nothing through do not break the streak");
        FeatsCounters.OnHurt(hadAttacker: true, damage: 18f);
        FeatsCounters.OnBlock(ledger, true, false, false, 0f);
        Check(FeatsCounters.Streak == 1 && ledger.Count(FeatsCounters.BestStreak) == 10, "hooks: a hit that lands ends the run; the best run stays");
        FeatsCounters.OnBlock(null, true, true, true, 5f);   // no ledger (totals not loaded): nothing, no error
        for (int k = 0; k < 40; k++) ledger.Add("key" + k, 1);
        Check(ledger.Counts.Count <= FeatsLedger.MaxCounts, "hooks: the counters stay bounded (" + ledger.Counts.Count + " of at most " + FeatsLedger.MaxCounts + ")");

        // integration with cargo-06: Ore Road and Born in Your Care read the keys the cargo branch keeps (cargoMeters, bornInCare) from the measured tallies
        var withCargo = PanelSample.FeatsFixture(Now);
        var oreRoad = PanelModel.FeatById("oreroad"); var bornFeat = PanelModel.FeatById("born");
        Check(!PanelModel.FeatWaiting(oreRoad, withCargo) && !PanelModel.FeatWaiting(bornFeat, withCargo) &&
              Math.Abs(PanelModel.FeatValue(oreRoad, withCargo) - withCargo.Events.CargoMeters.Values.Sum() / 1000.0) < 1e-6 && PanelModel.FeatValue(oreRoad, withCargo) > 0 &&
              PanelModel.FeatValue(bornFeat, withCargo) == withCargo.Events.BornInCare.Values.Sum() && PanelModel.FeatValue(bornFeat, withCargo) > 0,
              "feats x cargo: Ore Road reads the sample's cargoMeters (item-km) and Born in Your Care its bornInCare; neither waits when the tallies are there");
        var hk = PanelModel.FeatById("heavykeel"); var dr = PanelModel.FeatById("drover"); var ll = PanelModel.FeatById("longlead");
        Check(!new[] { hk, dr, ll }.Any(d => PanelModel.FeatWaiting(d, withCargo)) && PanelModel.FeatValue(hk, withCargo) == 140 &&
              Math.Abs(PanelModel.FeatValue(dr, withCargo) - withCargo.Events.LedMeters.Values.Sum() / 1000.0) < 1e-6 && PanelModel.FeatValue(dr, withCargo) > 6 && PanelModel.FeatValue(ll, withCargo) == 2.3,
              "feats x led: Heavy Keel reads the sample best load (140), Drover the led metres (km, the sum over animals), Long Lead the longest lead (2.3 km); none waits");
        // ---------- feats that wait for data: read 0 and Unsung until the key is there; then the tiers work ----------
        var key = Full(); key.Feats = new FeatsLedger { Primed = true };
        Check(!PanelModel.FeatWaiting(PanelModel.FeatById("heavykeel"), key) && !PanelModel.FeatWaiting(PanelModel.FeatById("longlead"), key) &&
              PanelModel.FeatValue(PanelModel.FeatById("heavykeel"), key) == 0 && PanelModel.FeatTier(PanelModel.FeatById("heavykeel"), key) == 0,
              "counted: Heavy Keel and Long Lead on this PC with no best yet read 0 and Unsung (counted, not 'does not count this yet')");
        key.Feats.Counts["cargoMeters"] = 5_200_000;   // 5 200 item-km: another part of Hearthwoven will keep this tally (cargo)
        key.Feats.Counts["bornInCare"] = 30; key.Feats.Counts["ledMeters"] = 6000; key.Feats.Counts["ledBestMeters"] = 2500; key.Feats.Counts["cargoBestVoyage"] = 140;
        PanelModel.EvaluateFeats(key, Now, "Ocean", "aboard");
        Check(key.Feats.Tier("oreroad") == 2 && key.Feats.Tier("born") == 2 && key.Feats.Tier("drover") == 2 && key.Feats.Tier("longlead") == 1 && key.Feats.Tier("heavykeel") == 1 && !PanelModel.FeatWaiting(PanelModel.FeatById("oreroad"), key),
              "waiting: once the data is there the tiers follow the table (Ore Road II at 5 200 item-km, Born in Your Care II at 30, Drover II at 6 km, Long Lead, Heavy Keel)");

        // ---------- the ledger on disk and in the snapshot ----------
        var totals = new LocalTotals { PlayerId = 11, FirstRunUtc = Now.AddDays(-5) };
        totals.Feats = p.Feats; totals.Feats.Counts[FeatsCounters.BlocksNear] = 77; totals.Feats.Seen = 1;
        var json = totals.ToJson(Now);
        var back2 = LocalTotals.FromJson(json);
        Check(back2 != null && back2.Feats.Primed && back2.Feats.Seen == 1 && back2.Feats.Tier("keptfires") == 2 && back2.Feats.Tier("waymate") == 1 && back2.Feats.Moment("keptfires", 2).Value.Biome == "Swamp" &&
              back2.Feats.Moment("keptfires", 2).Value.Place == "on foot" && back2.Feats.Moment("waymate").Value.Before && back2.Feats.Count(FeatsCounters.BlocksNear) == 77, "ledger: earned tiers, moments and counters survive LocalTotals.ToJson/FromJson");
        Check(LocalTotals.FromJson(new LocalTotals { PlayerId = 11 }.ToJson(Now)).Feats.IsEmpty && !new LocalTotals().ToJson(Now).Contains("\"feats\""), "ledger: an empty ledger writes nothing and an older file reads as empty (additive, version 1)");
        var plain = LocalTotals.FromJson("{\"version\":1,\"playerId\":11,\"saved\":\"2026-10-08T10:00:00Z\",\"totals\":{\"sessions\":0,\"previous\":{},\"lastSession\":{\"id\":\"\"}}}");
        Check(plain != null && plain.Feats.IsEmpty, "ledger: a totals file from before feats loads (no feats key)");
        var snap = Snapshot.Build("0.6.0", 11, "Rowan", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", new DamageTally(), feats: p.Feats);
        Check(snap.Contains("\"feats\":{") && !snap.Contains("on foot") && !snap.Contains("shieldHitsNear") && !snap.Contains("\"counts\"") && !snap.Contains("\"x\":") && !snap.Contains("\"z\":"),
              "snapshot: feats travel as id -> tier, date, biome; no place, no counter, no x or z");
        var read = PanelInput.FromSnapshot(snap);
        Check(read.Feats != null && read.Feats.Tier("keptfires") == 2 && read.Feats.Moment("keptfires").Value.Biome == "Swamp" && read.Feats.Tier("waymate") == 1 && read.Feats.Moment("waymate").Value.Before &&
              read.Feats.Moment("keptfires", 1).Value.Utc == DateTime.MinValue && read.Feats.Count(FeatsCounters.BlocksNear) == 0,
              "snapshot: a fellow's copy reads back with the highest tier's moment and nothing of their counters");
        Check(PanelInput.FromSnapshot(Snapshot.Build("0.6.0", 11, "Rowan", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", new DamageTally())).Feats == null, "snapshot: a copy from an older sender has no feats and reads as none known");
        Check(!Snapshot.Build("0.6.0", 11, "Rowan", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", new DamageTally(), feats: new FeatsLedger()).Contains("\"feats\""), "snapshot: nothing earned writes no feats key");
        var big = new FeatsLedger();
        for (int k = 0; k < 200; k++) big.Earn("feat" + k, 3, new FeatMoment { Utc = Now, Biome = "Meadows" });
        var jb = new Json().Open(); big.WriteTo(jb); jb.Close();
        var reread = FeatsLedger.ReadFrom(MiniJson.Obj(MiniJson.Parse(jb.ToString()) as Dictionary<string, object>, "feats"));
        Check(big.Earned.Count == FeatsLedger.MaxFeats && reread.Earned.Count <= FeatsLedger.MaxFeats && reread.Earned.Values.All(l => l.Count <= FeatsLedger.MaxTiers), "ledger: bounded however long someone plays (" + big.Earned.Count + " feats of at most " + FeatsLedger.MaxFeats + ", three tiers each)");
        Check(!big.Earn("never", 3, new FeatMoment()) && !new FeatsLedger().Earn("x", 0, new FeatMoment()), "ledger: past the cap, or tier 0, nothing is recorded");

        // ---------- the page wording on every page and the em dash rule ----------
        var every = new List<PanelView> { feats, unsung, torFeats, overview };
        Check(every.SelectMany(PanelModel.AllText).All(t => !t.Contains("—") && !t.Contains("–") && t.IndexOf("at least", StringComparison.OrdinalIgnoreCase) < 0), "wording: no dash and no \"at least\" on the Feats page, a fellow's Feats and the overview");
        fails += RunJoost();   // his real book (feats-real)
        fails += PanelTextTests.Run(new[] { ("feats", Full()) });   // every label fits its box, no "at least" (the Feats pages and views are among the sample's pages)
        return fails;
    }
}
