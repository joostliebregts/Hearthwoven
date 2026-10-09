// Tests for Battle's filters (src/Panel/Chapters/BattleFacets.cs): the biome tiles as the Overview's filter, Biome and Foe on
// Damage, Weapon, Damage type and Kin on Foes. The facet counts per window (10 minutes, the session, since install), OR within
// a row and AND across, zero chips that stay, the stale choice, the rows that are not offered, and the filter keys on a strip.
// Called from Program.cs with its own Battle evening (PanelSample.Battle) plus a fight in the last ten minutes.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static partial class BattleFilterTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static HitData.DamageTypes D(string type, float v)
    {
        var d = new HitData.DamageTypes();
        switch (type) { case "blunt": d.m_blunt = v; break; case "pierce": d.m_pierce = v; break; case "slash": d.m_slash = v; break; }
        return d;
    }

    // the Battle evening, plus two hits in the last ten minutes (Draugr 90 with a club, Leech 45 with a bow), both in the Swamp
    static PanelInput Evening()
    {
        var i = PanelSample.Battle();
        i.Log.AddDamage(i.NowUtc.AddMinutes(-4), "Swamp", true, "Draugr", "Clubs", D("blunt", 90));
        i.Log.AddDamage(i.NowUtc.AddMinutes(-2), "Swamp", true, "Leech", "Bows", D("pierce", 45));
        i.DamageSinceInstall = PanelSample.SinceInstall(i.Log); i.BiomeSinceInstall = PanelSample.BiomeSinceInstall(i.Log);
        return i;
    }

    static PanelView Page(PanelInput i, string page, Action<PanelState> more = null)
    {
        var st = new PanelState { Chapter = Chapter.Battle }; st.Page[Chapter.Battle] = page; more?.Invoke(st);
        return PanelModel.Build(i, st);
    }
    static Block Bar(PanelView v) => PanelModel.FilterOf(v);
    static Block Row(PanelView v, string id) => Bar(v).Items.FirstOrDefault(b => b.Kind == "facet" && b.Id == id);
    static string Chips(PanelView v, string id) => Row(v, id) == null ? "(no row)" : string.Join(",", Row(v, id).Items.Select(c => c.Title + "=" + c.Value + (c.Tone == "zero" ? "(0)" : "")));
    static Block Of(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
    static Action<PanelState> Pick(string filter, string facet, params string[] ids) => s => { foreach (var id in ids) PanelModel.ToggleFacet(s, filter, facet, id); };
    static Action<PanelState> All(params Action<PanelState>[] picks) => s => { foreach (var p in picks) p(s); };
    static Action<PanelState> Window(TimeWindow w) => s => s.Window = w;
    const string Ov = PanelModel.BattleOverviewFilter, Dm = PanelModel.BattleDamageFilter, Fo = PanelModel.BattleFoesFilter;
    static string Gap(string n) => n.Replace(" ", PanelModel.ThousandsGap);

    static partial void RunDamage(PanelInput ev);
    static partial void RunFoes(PanelInput ev);

    // ---------- Damage and Deaths: the same Biome row in the page (fix3), the window chips never move ----------
    static void RunBiomeRow(PanelInput ev)
    {
        var dm = Page(ev, "damage", s => s.View["Battle/damage/view"] = "type");   // the grid view (By weapon is the default)
        var dmBar = Bar(dm);
        var all = PanelModel.DealtRows(PanelModel.Damage(ev.Log, TimeWindow.Session, "", ev.NowUtc)).Sum(r => (double)r.Amount);
        Check(dm.Biomes.Count == 0 && dmBar != null && dmBar.Id == Dm && dmBar.Title == "Biome" && dmBar.Tone == "compact" && Row(dm, "biome") != null && Row(dm, "biome").Title == "Biome" &&
              dmBar.Items.First(b => b.Kind == "applied").Value == "All biomes" && Of(dm, "damagegrid").Value == PanelModel.Number(all) && dm.Keys.Contains("[Tab] Filter") && dmBar.Text == Gap(PanelModel.Number(all)) + " damage dealt",
              "damage: a Biome row in the page, collapsed to one line (Biome, G, All biomes, the damage), no heading chip: " + Chips(dm, "biome"));
        var swamp = Page(ev, "damage", s => { Pick(Dm, "biome", "Swamp")(s); s.View["Battle/damage/view"] = "type"; });
        var swampDealt = Chips(swamp, "biome");
        var swampSum = PanelModel.DealtRows(PanelModel.Damage(ev.Log, TimeWindow.Session, "", ev.NowUtc).Where(r => r.Biome == "Swamp").ToList()).Sum(r => (double)r.Amount);
        Check(Of(swamp, "damagegrid").Value == PanelModel.Number(swampSum) && swampSum < all && Bar(swamp).Text == Gap(PanelModel.Number(swampSum)) + " of " + Gap(PanelModel.Number(all)) + " damage dealt" &&
              Bar(swamp).Items.First(b => b.Kind == "applied").Items.Single().Title == "Swamp" && swamp.Scope.StartsWith("Swamp combat"),
              "damage: choosing the Swamp narrows the grid to the Swamp's hits, the result line says of how many, a token, the scope names it: " + swampDealt);
        var dmTen = Page(ev, "damage", Window(TimeWindow.LastTenMinutes));
        Check(Row(dmTen, "biome").Items.Select(c => c.Title + (c.Tone == "zero" ? "(0)" : "")).SequenceEqual(new[] { "Meadows(0)", "Black Forest(0)", "Swamp" }), "damage 10 min: only the Swamp has hits, the other chips are dimmed and cannot be chosen: " + Chips(dmTen, "biome"));
        var dmAll = Page(ev, "damage", Window(TimeWindow.SinceInstall));
        var line = Bar(dmAll);
        Check(dmAll.Biomes.Count == 0 && line != null && line.Tone == "inline" && line.Title == "Biome" && line.KeyCap == null && line.Items.Count == 1 && line.Items[0].Title == PanelModel.DamageAllBiomeNote && !dmAll.Keys.Contains("[Tab] Filter"),
              "damage since install: no dimmed chip: one line where the Biome row would be says why there is no choice (and no key)");
        var dmNone = Page(ev, "damage", All(Window(TimeWindow.LastTenMinutes), Pick(Dm, "biome", "Meadows")));
        Check(PanelModel.Content(dmNone).Any(b => b.Kind == "empty" && b.Title == PanelModel.NothingForChoice) && Bar(dmNone) != null && Bar(dmNone).Items.First(b => b.Kind == "applied").Items.Count == 1,
              "damage: a choice with nothing in the window says so and keeps the bar, so it can be unchosen");

        var de = Page(ev, "deaths");
        var deBar = Bar(de);
        Check(de.Biomes.Count == 0 && deBar != null && deBar.Id == PanelModel.BattleDeathsFilter && deBar.Title == "Biome" && deBar.Text == "3 falls" && Of(de, "deathstrip").Items.All(t => !t.Selected) && Of(de, "deaths").Items.Count == 3,
              "deaths: the same Biome row as Damage, 3 falls, no tile lit, the whole list: " + Chips(de, "biome"));
        var deSwamp = Page(ev, "deaths", Pick(PanelModel.BattleDeathsFilter, "biome", "Swamp"));
        Check(Bar(deSwamp).Text == "2 of 3 falls" && Of(deSwamp, "deaths").Items.Count == 2 && Of(deSwamp, "deaths").Items.All(i => i.Text.EndsWith("Swamp")) &&
              Of(deSwamp, "deathstrip").Items.Where(t => t.Selected).Select(t => t.Id).SequenceEqual(new[] { "Swamp" }) && Of(deSwamp, "deathstrip").Items.Count == Of(de, "deathstrip").Items.Count,
              "deaths: choosing the Swamp narrows the list to its falls and lights its tile on the strip (the strip keeps every biome)");
        var deAll = Page(ev, "deaths", Window(TimeWindow.SinceInstall));
        Check(Bar(deAll) != null && Bar(deAll).Id == PanelModel.BattleDeathsFilter && deAll.Biomes.Count == 0 && Row(deAll, "biome") != null, "deaths since install: the Biome row stays and works (the strip counts per biome, the list is the session's)");
        var ks = new PanelState { Chapter = Chapter.Battle }; ks.Page[Chapter.Battle] = "damage";
        var kv = PanelModel.Build(ev, ks);
        Check(PanelModel.FilterKeyPressed(ks, kv) && ks.FilterRow == 0 && Bar(PanelModel.Build(ev, ks)).Open && PanelModel.Build(ev, ks).Keys.Contains("[A/D] Move"), "damage keys: the filter key opens the Biome row and enters its focus, the footer lists the focus keys");
        // one place for the windows: the heading carries the same chips on all three pages, whatever the biome choice
        var places = new[] { Page(ev, "overview"), dm, de, swamp, deSwamp }.Select(v => string.Join("|", v.Windows.Select(c => c.Label)) + "/" + v.Biomes.Count).Distinct().ToList();
        Check(places.Count == 1, "windows: Overview, Damage and Deaths carry the same window chips and no biome chip, so the row never shifts: " + string.Join(" ; ", places));
    }

    public static int Run()
    {
        fails = 0;
        var ev = Evening();

        Check(PanelModel.ScopeOfSet(new string[0]) == null && PanelModel.ScopeOfSet(new[] { "Swamp" }) == "in the Swamp" && PanelModel.ScopeOfSet(new[] { "Swamp", "BlackForest" }) == "in the Black Forest and Swamp" &&
              PanelModel.ScopeOfSet(new[] { "Swamp", "BlackForest", "Meadows" }) == "in 3 biomes", "scope: one biome, two, or a count in the legend");

        // ---------- Overview: the biome tiles are the filter ----------
        var ov = Page(ev, "overview");
        var strip = Of(ov, "biomes"); var ovBar = Bar(ov);
        Check(ovBar != null && ovBar.Id == Ov && ovBar.Tone == "inline" && strip.Id == Ov && ov.Biomes.Count == 0 && strip.Items.All(t => !t.Selected) && ovBar.Items.First(b => b.Kind == "applied").Title == "No filter: all biomes · press a tile to narrow",
              "overview: the strip carries the filter's id, its inline filter bar sits under it; there is no Biome chip in the heading any more: " + ovBar?.Items.First(b => b.Kind == "applied").Title);
        var ovSwamp = Page(ev, "overview", Pick(Ov, "biome", "Swamp"));
        var ss = Of(ovSwamp, "biomes");
        Check(ss.Items.Where(t => t.Selected).Select(t => t.Id).SequenceEqual(new[] { "Swamp" }) && ss.Value == "765" && ss.Title == "in the Swamp" && Of(ovSwamp, "composition").Title.EndsWith("in the Swamp") &&
              Bar(ovSwamp).Text == "1 of 3 biomes" && Bar(ovSwamp).Items.First(b => b.Kind == "applied").Items.Single().Title == "Swamp",
              "overview: choosing the Swamp tile: its tile lit, the legend and what hurt you are the Swamp's, the token and \"1 of 3 biomes\"");
        var ovTwo = Page(ev, "overview", Pick(Ov, "biome", "Swamp", "BlackForest"));
        Check(Of(ovTwo, "biomes").Items.Count(t => t.Selected) == 2 && Of(ovTwo, "biomes").Value == PanelModel.Number(1240 + 765) && Of(ovTwo, "biomes").Title == "in the Black Forest and Swamp", "overview: two tiles are OR (the Black Forest and the Swamp together)");
        var ovTen = Page(ev, "overview", Window(TimeWindow.LastTenMinutes));
        Check(Row(ovTen, "biome").Items.Select(c => c.Title + (c.Tone == "zero" ? "(0)" : "")).SequenceEqual(new[] { "Meadows(0)", "Black Forest(0)", "Swamp" }),
              "overview window 10 min: only the Swamp has anything, the other tiles are dimmed chips that cannot be chosen");
        var ovAll = Page(ev, "overview", All(Window(TimeWindow.SinceInstall), Pick(Ov, "biome", "BlackForest")));
        Check(Of(ovAll, "biomes").Items.Single(t => t.Selected).Id == "BlackForest" && Bar(ovAll) != null && Bar(ovAll).Id == Ov && ovAll.Biomes.Count == 0, "overview since install: the per-biome totals keep the tiles as the filter");
        var noBiome = Evening(); noBiome.BiomeSinceInstall = null;
        Check(Bar(Page(noBiome, "overview", Window(TimeWindow.SinceInstall))) == null, "overview since install without per-biome totals: no strip, so no filter");

        // ---------- the keys on the tiles ----------
        var ks = new PanelState { Chapter = Chapter.Battle };
        var kv = PanelModel.Build(ev, ks);
        Check(PanelModel.FilterKeyPressed(ks, kv) && ks.FilterRow == 0, "keys: the filter key on the Overview enters the focus on the Biome row");
        kv = PanelModel.Build(ev, ks);
        Check(Row(kv, "biome").Tone == "focus" && Of(kv, "biomes").Items.Count(t => t.Note == "cursor") == 1 && Of(kv, "biomes").Items.First(t => t.Note == "cursor").Id == "Meadows" && kv.Keys.Contains("[A/D] Move"),
              "keys: the cursor stands on the first tile, the footer lists the focus keys");
        PanelModel.FilterMove(ks, kv, 1); kv = PanelModel.Build(ev, ks);
        Check(Of(kv, "biomes").Items.First(t => t.Note == "cursor").Id == "BlackForest", "keys: D moves the cursor to the next tile");
        PanelModel.FilterToggle(ks, kv); kv = PanelModel.Build(ev, ks);
        Check(PanelModel.Chosen(ks, Ov, "biome").SequenceEqual(new[] { "BlackForest" }) && Of(kv, "biomes").Items.Single(t => t.Selected).Id == "BlackForest", "keys: Enter chooses the tile under the cursor");
        PanelModel.FilterClear(ks, kv);
        Check(ks.Facets.Count == 0, "keys: Delete clears all");
        Check(PanelModel.FollowFacet(ks, PanelModel.FacetLink(Ov, "biome", "Swamp")) && PanelModel.Chosen(ks, Ov, "biome").SequenceEqual(new[] { "Swamp" }), "mouse: pressing a tile is the link target a chip has");
        RunDamage(ev);
        RunFoes(ev);
        RunBiomeRow(ev);

        // ---------- what stays ----------
        var deaths = Page(ev, "deaths");
        Check(deaths.Biomes.Count == 0 && Bar(deaths) != null && Bar(Page(ev, "defense")) == null && Bar(Page(ev, "foes")) == null, "deaths: the Biome row is in the page, never a heading chip; Defence and Foes have none");
        var crafting = PanelModel.Build(DeedsTests.Rich(PanelSample.Evening(ev.NowUtc)), new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = "crafting" } });
        Check(Bar(crafting) != null && Bar(crafting).Tone == null, "component: Crafting's bar keeps its own look (no compact label column)");
        return fails;
    }
}
