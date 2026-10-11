// 0.8.5 (src/Panel/Chapters/FoeNames.cs): one row per foe by the game's name, and the game's names without their color tags. Two prefabs the game
// gives one name (Draugr and the ranged Draugr) are one row wherever a foe is a row or a filter, and its numbers are both kinds' added up; a fight's
// Skeletons with and without a bow are one row; Brenna (Hildir's miniboss, not flagged a boss by the game) gets the boss fight page, and her name's
// color tags never reach a label. Fictional players only.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FoeNamesTests
{
    /// <summary>The sample world with a few ranged Draugr in the Swamp this session (the preview battle-damage-byfoe): Rowan's hits on them, their
    /// arrows into Rowan, the battle record replayed from the log as the world does.</summary>
    public static PanelInput KinBook(DateTime now)
    {
        var p = PanelSample.Full(now);
        foreach (var m in new[] { -9, -8, -6 })
        {
            var hit = new HitData.DamageTypes { m_slash = 120, m_fire = 25 };
            p.Log.AddDamage(now.AddMinutes(m), "Swamp", true, "Draugr_Ranged", "Swords", hit);
            p.Log.AddAfter(now.AddMinutes(m), "Swamp", "Draugr_Ranged", "Swords", hit, hit);   // 0.8.5: its after as ClientHooks keeps it (no sample modifiers: as swung)
        }
        foreach (var m in new[] { -9, -6 }) p.Log.AddDamage(now.AddMinutes(m), "Swamp", false, "Draugr_Ranged", "EnemyHit", new HitData.DamageTypes { m_pierce = 34 });
        p.Session = SampleWorld.TallyOf(p.Log); p.DamageSinceInstall = PanelSample.SinceInstall(p.Log);
        BattleSample.Session(p, now, new[] { "Edda", "Tor" }, new[] { "Edda", "Tor", "Finch" });
        return p;
    }

    /// <summary>Brenna's name as the game gives it in English (reference/item-names.json, "enemy_skeletonfire"): with the game's color tags.</summary>
    public const string TaggedBrenna = "<color=orange>Brenna</color> ";

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelState At(string page, TimeWindow w, Action<PanelState> more = null) { var s = new PanelState { Chapter = Chapter.Battle, Window = w, Page = { [Chapter.Battle] = page } }; more?.Invoke(s); return s; }
        Block Find(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
        List<Block> Rows(Block b, string kind) => (b?.Items ?? new List<Block>()).Where(i => i.Kind == kind).ToList();
        double V(string s) => s == null ? 0 : PanelModel.ParseCount(s);

        // ---------- two prefabs, one game name: one row, its numbers add up ----------
        var book = KinBook(now);
        var rows = PanelModel.Damage(book.Log, TimeWindow.Session, "", book.NowUtc);
        double Sum(string dir) => rows.Where(r => r.Dir == dir && (r.Other == "Draugr" || r.Other == "Draugr_Ranged") && PanelModel.BattleTypes.Concat(new[] { "damage" }).Contains(r.Type) && r.Amount > 0).Sum(r => (double)r.Amount);
        var prefabCounts = book.Battle.Counts(null);
        int Fought(string k) => prefabCounts.Get(k)?.Separate ?? 0;
        var byFoe = Rows(Find(PanelModel.Build(book, At("damage", TimeWindow.Session, s => s.View["Battle/damage/view"] = "foe")), "dealtfoes"), "source");
        var foes = Rows(Find(PanelModel.Build(book, At("foes", TimeWindow.Session)), "foetable"), "foe");
        var received = Rows(Find(PanelModel.Build(book, At("defense", TimeWindow.Session)), "sources"), "source");
        var feed = PanelModel.Build(book, At(PanelModel.FeedPage, TimeWindow.Session, s => s.OpenFilters.Add(PanelModel.FeedFilter)));
        var chips = PanelModel.Content(feed).Where(b => b.Kind == "filterbar").SelectMany(b => b.Items ?? new List<Block>()).Where(b => b.Kind == "facet" && b.Id == "foe").SelectMany(b => b.Items ?? new List<Block>()).ToList();
        bool OneDraugr(List<Block> list) => list.Count(r => r.Title == "Draugr") == 1 && list.All(r => r.Id != "Draugr_Ranged") && list.Select(r => r.Title).Distinct().Count() == list.Count;
        var dealt = byFoe.FirstOrDefault(r => r.Title == "Draugr"); var hit = received.FirstOrDefault(r => r.Title == "Draugr");
        // the open Draugr says so when its kinds take damage differently (a ranged Draugr weak to fire here), and not when they agree (wherever the page says it)
        bool OpenSays(PanelInput b, string line) => Says(PanelModel.Build(b, At("foes", TimeWindow.Session, s => s.View[PanelModel.FoeOpenKey] = "Draugr")), line);
        var differ = book.ShallowCopy(); var sameFoe = book.Foe;
        differ.Foe = p => p == "Draugr_Ranged" ? new PanelModel.FoeData { Trophy = "TrophyDraugr", Modifiers = { ["fire"] = "Weak" } } : sameFoe(p);
        var said = PanelModel.KinDiffersLine("Draugr");
        Check(Fought("Draugr_Ranged") > 0 && Fought("Draugr") > 0 && OneDraugr(byFoe) && OneDraugr(foes) && OneDraugr(received) && OneDraugr(chips) &&
              Math.Abs(V(dealt?.Value) - Sum("dealt")) <= 1 && Math.Abs(V(foes.First(r => r.Title == "Draugr").Value) - Sum("dealt")) <= 1 && Math.Abs(V(hit?.Value) - Sum("taken")) <= 1 &&
              dealt?.Value2 == PanelModel.Badge(Fought("Draugr") + Fought("Draugr_Ranged")) && OpenSays(differ, said) && !OpenSays(book, said),
              "FN one row per foe by the game's name: Draugr and the ranged Draugr are one Draugr on Damage by foe (" + dealt?.Value + " " + dealt?.Value2 + "), Foes, Defense's Received from (" + hit?.Value +
              ") and the feed's Foe chips; dealt, received and the x N are both kinds' added up; the open Draugr says when its kinds take damage differently: " + said);

        // ---------- Hildir's miniboss: a boss fight, her name without its color tags ----------
        var brenna = FightsSample.WithBrenna(PanelSample.Full(now), TaggedBrenna);
        var fight = brenna.Fights?.Last;
        var last = PanelModel.Build(brenna, At(PanelModel.LastFightPage, TimeWindow.Session));
        var fightRows = Rows(Find(last, PanelModel.FightFoesKind), "foe");
        var skeletons = fightRows.Where(r => r.Title == "Skeleton").ToList();
        Check(fight != null && fight.Kind == FightKind.Boss && fight.Outcome == FightOutcome.Won && fight.Boss == "Skeleton_Hildir" && fightRows.FirstOrDefault()?.Tone == "boss" &&
              skeletons.Count == 1 && skeletons[0].Value2 == PanelModel.Badge(5) && Math.Abs(V(skeletons[0].Value) - fight.Foes.Where(x => !x.Boss).Sum(x => x.DealtTotal)) <= 1,
              "FN Brenna (the game flags no boss) gets the boss fight page, won; her Skeletons with and without a bow are one row, x5, their damage added up (" + skeletons.FirstOrDefault()?.Value + ")");
        var pages = new[] { last, PanelModel.Build(brenna, At(PanelModel.RunsPage, TimeWindow.Session)), PanelModel.Build(brenna, At("foes", TimeWindow.Session)) };
        var tagged = pages.SelectMany(PanelModel.AllText).Where(t => t != null && (t.Contains("<color") || t.Contains("</color>"))).ToList();
        Check(tagged.Count == 0 && PanelModel.AllText(last).Any(t => t != null && t.Contains("Brenna")) && PanelRich.Untagged("<color=orange>Zil & Thungr</color> ") == "Zil & Thungr" && PanelRich.Untagged("<3 Edda <Boar>") == "<3 Edda <Boar>",
              "FN a color tag never reaches a label: Brenna's Last fight, Runs and bosses and Foes show \"Brenna\", no \"<color\"" + (tagged.Count > 0 ? " (found: " + tagged[0] + ")" : ""));

        // ---------- a kill token joins its foe's row (the game counts kills by the creature's token, the pages by prefab) ----------
        // Deeds > Recent's Foes defeated has only the game's kill table: each token's row wears its kind's trophy. Without the game's data (the
        // sample's kin, no tokens) the alias table joins them; with it (a kin map that knows the tokens, as GameData reads them) a mod's creature too
        var joins = new[] { ("SeekerBrood", "$enemy_babyseeker"), ("TentaRoot_wild", "$enemy_root"), ("Dverger", "$enemy_dvergr"), ("DvergerMage", "$enemy_dvergr_mage"),
                            ("Skeleton_Hildir", "$enemy_skeletonfire"), ("Greydwarf_Elite", "$enemy_greydwarfbrute"), ("Troll", "$enemy_troll") };
        var deeds = new DeedLog(); var none = new SessionEvents();
        Dictionary<string, float> Kills(float n) => DeedLog.GameKeys(new Dictionary<string, float> { ["EnemyKills"] = n * joins.Length }, null, null, joins.ToDictionary(j => j.Item2, j => n));
        deeds.Flush(now.AddMinutes(-20), none, Kills(0)); deeds.Flush(now.AddMinutes(-10), none, Kills(3));
        var recent = new PanelInput { PlayerName = "Rowan", IsSelf = true, NowUtc = now, SessionStartUtc = now.AddMinutes(-30), Deeds = deeds, Events = none, FoeKin = PanelSample.SampleFoeKin,
                                      Foe = p => new PanelModel.FoeData { Trophy = "Trophy_" + p }, DisplayName = t => t };
        // the foes defeated wherever the page lays them out: the block whose items are the kill tokens
        var strip = PanelModel.Content(PanelModel.Build(recent, new PanelState { Window = TimeWindow.Session, Page = { [Chapter.Deeds] = "recent" } }))
                              .FirstOrDefault(b => (b.Items ?? new List<Block>()).Any(i => i.Kind == "item" && joins.Any(j => j.Item2 == i.Id)));
        var icons = (strip?.Items ?? new List<Block>()).Where(b => b.Kind == "item").ToDictionary(b => b.Id, b => b.Icon);
        var wrong = joins.Where(j => !icons.TryGetValue(j.Item2, out var icon) || icon != "item:Trophy_" + j.Item1).Select(j => j.Item2 + " " + (icons.TryGetValue(j.Item2, out var i) ? i : "missing")).ToList();
        var known = PanelModel.FoeKinOf(new[] { ("SeekerBrood", "Seeker Brood", "$enemy_babyseeker"), ("Wolf_Mod", "Moonwolf", "$enemy_moonwolf") });
        var game = new PanelInput { FoeKin = t => known.TryGetValue(t, out var k) ? k : null };
        Check(wrong.Count == 0 && PanelModel.FoeGroup(game, "$enemy_babyseeker") == "SeekerBrood" && PanelModel.FoeGroup(game, "$enemy_moonwolf") == "Wolf_Mod" &&
              PanelModel.FoeGroup(recent, "$enemy_babyseeker") == PanelModel.FoeGroup(recent, "SeekerBrood"),
              "FN a kill token joins its foe's row: Recent's Foes defeated shows the Seeker Brood's, Root's, Dvergr's, Dvergr Mage's, Brenna's and Greydwarf Brute's own kind (the alias table, no game data), " +
              "and with the game's data any creature's token, a mod's too" + (wrong.Count > 0 ? ": wrong " + string.Join(", ", wrong) : ""));
        return fails;
    }
}
