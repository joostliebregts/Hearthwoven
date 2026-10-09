// Tests for the filter bar (src/Panel/FacetModel.cs) and its first page, Deeds > Crafting (src/Panel/Chapters/CraftingFacets.cs):
// OR within a row, AND across rows, live counts for the other row, zero chips that stay, the crossfilter bars, the tokens,
// the main-material rule, the filter focus keys. Called from Program.cs with the sample evening.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FacetTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelView Show(PanelInput i, PanelState st = null)
    {
        st = st ?? new PanelState();
        st.Chapter = Chapter.Deeds; st.Page[Chapter.Deeds] = "crafting";
        return PanelModel.Build(i, st);
    }
    static Block Bar(PanelView v) => PanelModel.FilterOf(v);
    static Block Row(PanelView v, string id) => Bar(v).Items.First(b => b.Kind == "facet" && b.Id == id);
    static string Chips(PanelView v, string id) => string.Join(",", Row(v, id).Items.Select(c => c.Title + "=" + c.Value + (c.Tone == "zero" ? "(0)" : "")));
    static Block Parts(PanelView v, string id) => Bar(v).Items.First(b => b.Kind == "facetbar" && b.Id == id);
    static string Grid(PanelView v) => string.Join(",", PanelModel.Content(v).First(b => b.Kind == "itemgrid").Items.Select(i => i.Title + "=" + i.Value));
    static Ingredient I(string token, bool refined = false, string name = null) => new Ingredient { Token = token, Refined = refined, Name = name };
    static PanelState Chosen(params (string facet, string option)[] picks)
    {
        var s = new PanelState();
        foreach (var p in picks) PanelModel.ToggleFacet(s, PanelModel.CraftFilter, p.facet, p.option);
        return s;
    }

    public static int Run(PanelInput input)
    {
        fails = 0;
        var rich = DeedsTests.Rich(input);

        // ---------- the main-material rule (what the recipe's ingredients say) ----------
        Check(PanelModel.MainMaterial(new[] { I("$item_bronze"), I("$item_wood") }) == "Bronze", "material: a Bronze Axe (bronze, wood) is Bronze: the metal marks the tier, not the handle");
        Check(PanelModel.MainMaterial(new[] { I("$item_wolfpelt"), I("$item_silver"), I("$item_leatherscraps") }) == "Silver", "material: a Wolf Armour (pelt, silver) is Silver");
        Check(PanelModel.MainMaterial(new[] { I("$item_trollhide"), I("$item_leatherscraps") }) == "Leather", "material: a Troll armour (hide, scraps) is Leather");
        Check(PanelModel.MainMaterial(new[] { I("$item_finewood"), I("$item_resin") }) == "Wood" && PanelModel.MainMaterial(new[] { I("$item_iron"), I("$item_wood") }) == "Iron", "material: wood alone is Wood, iron over wood is Iron");
        Check(PanelModel.MainMaterial(new[] { I("$item_blackmetal"), I("$item_iron"), I("$item_flametal") }) == "Flametal", "material: later biomes' materials rank above the older ones");
        Check(PanelModel.MainMaterial(new[] { I("$item_mithril", true, "Mithril"), I("$item_iron") }) == "Mithril", "material: a mod's own bar (a smelter makes it) is its own material above the vanilla ones");
        Check(PanelModel.MainMaterial(new[] { I("$item_resin"), I("$item_flint"), I("$item_coal") }) == null && PanelModel.MainMaterial(null) == null, "material: ingredients that mark no tier give no material (Other)");

        // ---------- no filter: the rows, the counts, the bars ----------
        var all = Show(rich);
        var bar = Bar(all);
        Check(bar != null && bar.Src == "character" && bar.KeyCap == "K" && bar.Items.Count(b => b.Kind == "facet") == 2 && bar.Items.Count(b => b.Kind == "facetbar") == 2,
              "filter: a filter bar with a Kind row and a Main material row, a bar for each, the filter key as its keycap");
        Check(Chips(all, "kind") == "Weapons=9,Armour=6,Tools=4,Trinkets=1", "filter: the Kind chips count what was crafted: " + Chips(all, "kind"));
        Check(Chips(all, "material") == "Wood=9,Leather=2,Bronze=7,Iron=2,Silver=0(0),Black metal=0(0)", "filter: the Main material chips, zero chips stay in place, dimmed: " + Chips(all, "material"));
        Check(Row(all, "material").Note == "by main material" && Parts(all, "material").Note == null, "filter: the honest line \"by main material\" stays under the row; the bar's own title says it, so it is not repeated there");
        Check(bar.Text == "9 items · 20 crafted" && bar.Items.First(b => b.Kind == "applied").Title == "No filter: every item" && !bar.Items.First(b => b.Kind == "applied").Items.Any(),
              "filter: no token and the result line says \"9 items · 20 crafted\"");
        Check(Grid(all) == "Bronze Axe=6,Wood Shield=4,Hammer=2,Leather Helmet=2,Iron Sword=2,Crude Bow=1,Cultivator=1,Hoe=1,Bronze Health Trinket=1", "filter: with nothing chosen the grid shows every item, most first: " + Grid(all));
        Check(Math.Abs(Parts(all, "kind").Items.Sum(p => p.Fraction) - 1) < 1e-5 && Math.Abs(Parts(all, "material").Items.Sum(p => p.Fraction) - 1) < 1e-5, "filter: each bar adds up to its whole");
        var kindColours = Parts(all, "kind").Items.Select(p => p.Colour).ToList(); var matColours = Parts(all, "material").Items.Select(p => p.Colour).ToList();
        Check(kindColours.Intersect(matColours).Count() == 0 && Parts(all, "material").Items.First(p => p.Id == "Wood").Colour == "#8a5a34" && Parts(all, "material").Items.First(p => p.Id == "Iron").Colour == "#647488",
              "filter: the kinds' bar and the materials' bar share no colour (blue never means two things); each material has its own");

        // ---------- OR within a row, AND across rows, live counts, the crossfilter bars ----------
        var armour = Show(rich, Chosen(("kind", "armour")));
        Check(Chips(armour, "material") == "Wood=4,Leather=2,Bronze=0(0),Iron=0(0),Silver=0(0),Black metal=0(0)", "filter: a chosen kind narrows the material chips' counts: " + Chips(armour, "material"));
        Check(Chips(armour, "kind") == "Weapons=9,Armour=6,Tools=4,Trinkets=1" && Row(armour, "kind").Items[1].Selected, "filter: the chosen row's own counts stay (you can add another kind), the chosen chip is marked");
        var both = Show(rich, Chosen(("kind", "armour"), ("material", "Leather")));
        Check(Grid(both) == "Leather Helmet=2" && Bar(both).Text == "1 item · 2 crafted", "filter: Armour and Leather leave the Leather Helmet: \"1 item · 2 crafted\"");
        Check(Chips(both, "kind") == "Weapons=0(0),Armour=2,Tools=0(0),Trinkets=0(0)" && Chips(both, "material") == "Wood=4,Leather=2,Bronze=0(0),Iron=0(0),Silver=0(0),Black metal=0(0)",
              "filter: each row's counts follow the other row: " + Chips(both, "kind") + " | " + Chips(both, "material"));
        var tokens = Bar(both).Items.First(b => b.Kind == "applied").Items;
        Check(tokens.Select(t => t.Title + "/" + t.Text).SequenceEqual(new[] { "Armour/Kind", "Leather/Main material" }), "filter: the chosen chips are removable tokens, in the order chosen");
        var kb = Parts(both, "kind").Items; var mb = Parts(both, "material").Items;
        Check(kb.Where(p => p.Fraction > 0).Select(p => p.Id).SequenceEqual(new[] { "armour" }) && kb.First(p => p.Id == "armour").Selected && Math.Abs(kb.First(p => p.Id == "armour").Fraction - 1) < 1e-5,
              "filter: the kind bar is filtered by the material, not by its own choice: armour 100 %, its part outlined");
        Check(mb.First(p => p.Id == "Leather").Selected && !mb.First(p => p.Id == "Wood").Selected && Math.Abs(mb.First(p => p.Id == "Wood").Fraction - 4f / 6) < 1e-5 && Math.Abs(mb.First(p => p.Id == "Leather").Fraction - 2f / 6) < 1e-5,
              "filter: the material bar is filtered by the kind only: wood 4 of 6 stays beside the chosen leather 2 of 6");
        var two = Show(rich, Chosen(("material", "Wood"), ("material", "Leather")));
        Check(Grid(two) == "Wood Shield=4,Hammer=2,Leather Helmet=2,Crude Bow=1,Cultivator=1,Hoe=1" && Bar(two).Text == "6 items · 11 crafted", "filter: two chips in one row are OR: Wood or Leather");
        var empty = Show(rich, Chosen(("kind", "weapons"), ("material", "Leather")));
        Check(PanelModel.Content(empty).Any(b => b.Kind == "empty" && b.Title == "Nothing for this choice") && Row(empty, "material").Items.First(c => c.Id == "Leather").Selected && Row(empty, "material").Items.First(c => c.Id == "Leather").Tone != "zero",
              "filter: a stale choice that matches nothing says so, and its chip stays choosable so it can be taken off");

        // ---------- clicks: chip, token, bar part, Clear all ----------
        var s = new PanelState();
        PanelModel.Follow(s, PanelModel.FacetLink(PanelModel.CraftFilter, "kind", "armour"));
        PanelModel.Follow(s, PanelModel.FacetLink(PanelModel.CraftFilter, "material", "Leather"));
        Check(PanelModel.Chosen(s, PanelModel.CraftFilter, "kind").SequenceEqual(new[] { "armour" }) && PanelModel.Chosen(s, PanelModel.CraftFilter, "material").SequenceEqual(new[] { "Leather" }), "filter click: a chip's link chooses it");
        PanelModel.Follow(s, PanelModel.FacetLink(PanelModel.CraftFilter, "kind", "armour"));
        Check(PanelModel.Chosen(s, PanelModel.CraftFilter, "kind").Count == 0, "filter click: the same link again (a token's x, a bar part) takes it off");
        PanelModel.Follow(s, PanelModel.FacetLink(PanelModel.CraftFilter, "kind", "tools"));
        PanelModel.Follow(s, PanelModel.FacetClearTarget + PanelModel.CraftFilter);
        Check(s.Facets.Count == 0, "filter click: Clear all takes every chip off");
        var clicked = Show(rich, Chosen(("kind", "armour")));
        var link = PanelModel.FacetLink(Bar(clicked).Id, "kind", Bar(clicked).Items.First(b => b.Kind == "applied").Items[0].Id.Split('|')[1]);
        Check(link == PanelModel.FacetLink(PanelModel.CraftFilter, "kind", "armour"), "filter click: a token's id is its facet and chip");

        // ---------- what a page without material data and without item types says ----------
        var bare = DeedsTests.Rich(input); bare.MainMaterial = null; bare.ItemType = null;
        var bareView = Show(bare);
        Check(Chips(bareView, "kind") == "Weapons=0(0),Armour=0(0),Tools=0(0),Trinkets=0(0)" && Chips(bareView, "material").EndsWith("Other=20") && Grid(bareView).StartsWith("Bronze Axe=6"),
              "filter: without item types or recipes (a fellow's copy) every item is Other, the grid still lists all of it: " + Chips(bareView, "material"));
        var modded = DeedsTests.Rich(input); modded.MainMaterial = t => t == "$item_axe_bronze" ? "Mithril" : t == "$item_hoe" ? "Carapace" : "Wood";
        var moddedView = Show(modded);
        Check(Row(moddedView, "material").Items.Select(c => c.Title).SequenceEqual(new[] { "Wood", "Leather", "Bronze", "Iron", "Silver", "Black metal", "Carapace", "Mithril" }),
              "filter: a later biome's material and a mod's own join the row after the known ones (only when something was crafted from it)");

        // ---------- where it sits ----------
        var z = Zoned.ZoneOf(all, Bar(all));
        Check(z != null && z.Id == "character" && !Zoned.Says(all, Bar(all)) && !Bar(all).SinceInstall, "filter: the bar sits in your character's zone with the gear it filters, no \"since install\" on it");
        Check(PanelModel.ToJson(all).Contains("filterbar") && PanelModel.ToJson(all).Contains("keyCap"), "filter: the preview bridge gets the bar and its keycap");
        Check(all.Keys.Any(k => k == "[K] Filter") && !PanelModel.Build(rich, new PanelState { Chapter = Chapter.Skills }).Keys.Any(k => k.Contains("Filter")), "filter: the footer says [K] Filter on this page only");
        var noKey = new PanelState { FilterKey = "" };
        Check(Bar(Show(rich, noKey)).KeyCap == null && !Show(rich, new PanelState { FilterKey = "" }).Keys.Any(k => k.Contains("Filter")), "filter: with the key unbound there is no keycap and no footer hint");

        // ---------- the filter focus ----------
        var f = new PanelState();
        var v = Show(rich, f);
        Check(PanelModel.FilterKeyPressed(f, v) && f.FilterRow == 0 && f.FilterCursor == 0, "focus: the filter key enters on the first row, first chip");
        v = Show(rich, f);
        Check(Row(v, "kind").Tone == "focus" && Row(v, "kind").Items[0].Tone == "cursor" && Row(v, "material").Tone == null && v.Keys.SequenceEqual(new[] { "[A/D] Move", "[W/S] Row", "[Enter] Choose", "[Delete] Clear all", "[K/Esc] Leave" }),
              "focus: the row and the chip under the cursor are marked, the footer lists the focus keys");
        PanelModel.FilterMove(f, v, 1); PanelModel.FilterMove(f, v, 1);
        Check(f.FilterCursor == 2, "focus: D moves along the row");
        PanelModel.FilterMove(f, v, -1); PanelModel.FilterMove(f, v, -1); PanelModel.FilterMove(f, v, -1);
        Check(f.FilterCursor == 3, "focus: A from the first chip wraps to the last");
        PanelModel.FilterToggle(f, v);
        v = Show(rich, f);
        Check(PanelModel.Chosen(f, PanelModel.CraftFilter, "kind").SequenceEqual(new[] { "trinkets" }) && Row(v, "kind").Items[3].Selected && Grid(v) == "Bronze Health Trinket=1", "focus: Enter chooses the chip under the cursor");
        PanelModel.FilterRowStep(f, v, 1); v = Show(rich, f);
        Check(f.FilterRow == 1 && Row(v, "material").Tone == "focus" && Row(v, "material").Items[f.FilterCursor].Tone == "cursor" && Row(v, "material").Items[f.FilterCursor].Title == "Bronze",
              "focus: S goes to the next row, the cursor on its first chip that can be chosen (zero chips are skipped)");
        PanelModel.FilterMove(f, v, 1); v = Show(rich, f);
        Check(Row(v, "material").Items[f.FilterCursor].Title == "Bronze", "focus: with Trinkets chosen only Bronze is left to choose, so D stays on it (the dimmed chips are skipped)");
        PanelModel.FilterClear(f, v); v = Show(rich, f);
        Check(f.Facets.Count == 0 && f.FilterRow == 1, "focus: Delete clears all and stays in the focus");
        PanelModel.FilterKeyPressed(f, v);
        Check(f.FilterRow == -1, "focus: the filter key again leaves");
        PanelModel.FilterKeyPressed(f, v);
        Check(f.FilterRow == 0 && PanelModel.FilterLeave(f) && f.FilterRow == -1 && !PanelModel.FilterLeave(f), "focus: Esc leaves");
        var left = new PanelState { Chapter = Chapter.Skills, FilterRow = 1 };
        var elsewhere = PanelModel.Build(rich, left);
        Check(PanelModel.FilterOf(elsewhere) == null && !PanelModel.FilterKeyPressed(new PanelState(), elsewhere) && left.FilterRow == -1,
              "focus: a page without a filter bar has no focus to enter, and drops one a chapter key left behind");

        // ---------- the filter bar on its own, any list ----------
        var items = new List<FacetItem>
        {
            new FacetItem { Key = "a", Weight = 5, Values = { ["tab"] = "misc", ["mat"] = "Wood" } },
            new FacetItem { Key = "b", Weight = 3, Values = { ["tab"] = "furniture", ["mat"] = "Wood" } },
            new FacetItem { Key = "c", Weight = 2, Values = { ["tab"] = "furniture", ["mat"] = "Stone" } },
        };
        var defs = new List<FacetDef>
        {
            new FacetDef { Id = "tab", Title = "Tab", Options = { new FacetOption { Id = "misc", Label = "Misc" }, new FacetOption { Id = "furniture", Label = "Furniture" } } },
            new FacetDef { Id = "mat", Title = "Material", Bar = false, Options = { new FacetOption { Id = "Wood", Label = "Wood" }, new FacetOption { Id = "Stone", Label = "Stone" } } },
        };
        var st2 = new PanelState(); PanelModel.ToggleFacet(st2, "Deeds/building/pieces", "mat", "Wood");
        var r = PanelModel.Facets(st2, "Deeds/building/pieces", defs, items, "placed", "character", "piece", "pieces", "Every piece");
        Check(r.Shown.Select(i => i.Key).SequenceEqual(new[] { "a", "b" }) && r.Bar.Text == "2 pieces · 8 placed" && r.Bar.Items.Count(b => b.Kind == "facetbar") == 1 &&
              r.Bar.Items.First(b => b.Kind == "facet" && b.Id == "tab").Items.Select(c => c.Value).SequenceEqual(new[] { "5", "3" }) && r.Bar.Items.First(b => b.Kind == "applied").Title == "Every piece",
              "component: any page can bring its own rows, items and words (Building next): filter, counts, one bar for the row that asked for one");

        // ---------- collapsed by default; the key or a click on the header opens it, Esc, the key or the header closes it ----------
        var cs = new PanelState(); var cv = Show(rich, cs); var cb = Bar(cv);
        Check(!cb.Open && cb.Items.Count(b => b.Kind == "facet") == 2 && cb.Items.First(b => b.Kind == "applied").Value == "all" && cb.Text == "9 items · 20 crafted",
              "collapse: the bar starts collapsed: the header says all and the result line, the rows stay in the data for the keys");
        Check(cb.Items.Where(b => b.Kind == "facetbar").All(b => b.Tone == null), "collapse: the two slim bars fit above the fold on this page, so they stay while collapsed");
        var tallBar = PanelModel.Facets(new PanelState(), "x/y", defs, items, "placed", "character", "piece", "pieces", "Every piece", above: 420);
        var tallOpen = new PanelState(); PanelModel.ToggleFilterOpen(tallOpen, "x/y");
        Check(tallBar.Bar.Items.Where(b => b.Kind == "facetbar").All(b => b.Tone == "hidden") && tallBar.Bar.Items.Where(b => b.Kind == "facetbar").All(b => b.Items.Count > 0) &&
              PanelModel.Facets(tallOpen, "x/y", defs, items, "placed", "character", "piece", "pieces", "Every piece", above: 420).Bar.Items.Where(b => b.Kind == "facetbar").All(b => b.Tone == null),
              "collapse: a page that leaves no room hides the slim bars while collapsed (they are still in the data), and shows them once open");
        Check(PanelModel.FollowFacet(cs, PanelModel.FacetOpenLink(PanelModel.CraftFilter)) && Bar(Show(rich, cs)).Open && cs.FilterRow == -1 && cs.OpenFilters.Contains(PanelModel.CraftFilter),
              "collapse: a click on the header opens the rows without entering the key focus");
        cv = Show(rich, cs);
        Check(Row(cv, "kind").Tone == null && PanelModel.FilterAnyOpen(cs, cv) && cv.Keys.Any(k => k == "[K] Filter"), "collapse: opened by a click there is no cursor and the footer still says [K] Filter");
        Check(PanelModel.FilterKeyPressed(cs, cv) && !Bar(Show(rich, cs)).Open && cs.FilterRow == -1 && cs.OpenFilters.Count == 0, "collapse: the filter key closes rows that a click opened");
        Check(PanelModel.FilterKeyPressed(cs, cv) && cs.FilterRow == 0 && Bar(Show(rich, cs)).Open, "collapse: the filter key from the collapsed line opens the rows and enters the focus");
        Check(PanelModel.FilterKeyPressed(cs, cv) && !Bar(Show(rich, cs)).Open && cs.FilterRow == -1, "collapse: the filter key in the focus collapses it again");
        PanelModel.FilterKeyPressed(cs, cv);
        Check(PanelModel.FilterAnyOpen(cs, cv) && PanelModel.FilterLeave(cs) && !Bar(Show(rich, cs)).Open && !PanelModel.FilterAnyOpen(cs, Show(rich, cs)), "collapse: Esc leaves the focus and collapses");
        PanelModel.FollowFacet(cs, PanelModel.FacetOpenLink(PanelModel.CraftFilter));
        Check(PanelModel.FilterAnyOpen(cs, Show(rich, cs)) && PanelModel.FilterLeave(cs) && !Bar(Show(rich, cs)).Open, "collapse: Esc also collapses rows a click opened (the panel stays open)");
        Check(!PanelModel.FilterLeave(cs) && !PanelModel.FilterAnyOpen(cs, Show(rich, cs)), "collapse: with nothing open Esc has nothing to leave, so it closes the panel as before");
        PanelModel.FollowFacet(cs, PanelModel.FacetOpenLink(PanelModel.CraftFilter)); PanelModel.FollowFacet(cs, PanelModel.FacetOpenLink(PanelModel.CraftFilter));
        Check(!Bar(Show(rich, cs)).Open, "collapse: the header's button toggles (open, then closed)");
        var fo = new PanelState(); PanelModel.FilterKeyPressed(fo, Show(rich, fo)); PanelModel.FollowFacet(fo, PanelModel.FacetOpenLink(PanelModel.CraftFilter));
        Check(!Bar(Show(rich, fo)).Open && fo.FilterRow == -1, "collapse: Hide filters in the focus leaves it too");
        var chosenCollapsed = Show(rich, Chosen(("kind", "armour"), ("material", "Leather")));
        Check(!Bar(chosenCollapsed).Open && Bar(chosenCollapsed).Items.First(b => b.Kind == "applied").Items.Count == 2 && Bar(chosenCollapsed).Text == "1 item · 2 crafted" && Grid(chosenCollapsed) == "Leather Helmet=2",
              "collapse: a collapsed bar keeps its choices: the tokens and the result line stay on the header, the list stays filtered");
        Check(Parts(cv, "kind").Items.Select(p => p.Title).SequenceEqual(new[] { "Weapons", "Armour", "Tools", "Trinkets" }) && Parts(cv, "kind").Title == "By kind",
              "collapse: a legend entry is the colour's name; the counts are on the chips");
        Check(PanelModel.ToJson(Show(rich, new PanelState())).Contains("\"open\"") && Bar(Show(rich, new PanelState { OpenFilters = { PanelModel.CraftFilter } })).Open, "collapse: the preview bridge gets the open flag");

        // ---------- remembered (zones-wording, Joost 2026-10-09: closed by default, but each page keeps what a player set) ----------
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hw-prefs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pathA = PanelPrefs.PathFor(dir, 111); var pathB = PanelPrefs.PathFor(dir, 222);
            Check(pathA != pathB && pathA.EndsWith("111.panel.json") && System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(pathA)) == "local", "prefs: one file per character, next to the local totals");
            var fresh = new PanelState(); var saved0 = PanelPrefs.Load(pathA, fresh);
            Check(fresh.Facets.Count == 0 && fresh.OpenFilters.Count == 0 && !Bar(Show(rich, fresh)).Open && !System.IO.File.Exists(pathA), "prefs: no file: every filter closed and empty, nothing written");
            var set = new PanelState(); PanelModel.ToggleFacet(set, PanelModel.CraftFilter, "kind", "armour"); PanelModel.FollowFacet(set, PanelModel.FacetOpenLink(PanelModel.CraftFilter));
            PanelModel.ToggleFacet(set, PanelModel.BuildFilter, "tab", "Misc");
            var saved1 = PanelPrefs.Save(pathA, set, saved0);
            Check(System.IO.File.Exists(pathA) && !System.IO.File.Exists(pathA + ".tmp") && PanelPrefs.Save(pathA, set, saved1) == saved1, "prefs: written atomically (no temp file left), and only when something changed");
            var back = new PanelState(); PanelPrefs.Load(pathA, back);
            Check(PanelModel.Chosen(back, PanelModel.CraftFilter, "kind").SequenceEqual(new[] { "armour" }) && PanelModel.Chosen(back, PanelModel.BuildFilter, "tab").SequenceEqual(new[] { "Misc" }) &&
                  back.OpenFilters.SetEquals(new[] { PanelModel.CraftFilter }) && Bar(Show(rich, back)).Open, "prefs: a new session finds each page's chips and its open bar as the player left them; Building stays closed");
            var other = new PanelState(); PanelPrefs.Load(pathB, other);
            Check(other.Facets.Count == 0 && other.OpenFilters.Count == 0, "prefs: another character starts with its own (none)");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pathB)); System.IO.File.WriteAllText(pathB, "{not json");
            var broken = new PanelState { OpenFilters = { "x" } }; PanelPrefs.Load(pathB, broken);
            Check(broken.Facets.Count == 0 && broken.OpenFilters.Count == 0, "prefs: an unreadable file is ignored: filters start closed and empty");
            var keyed = new PanelState(); PanelModel.FilterKeyPressed(keyed, Show(rich, keyed)); keyed.FilterRow = -1;
            Check(Bar(Show(rich, keyed)).Open && PanelModel.FilterLeave(keyed, Show(rich, keyed)) && !Bar(Show(rich, keyed)).Open,
                  "prefs: opened by the key it stays open when the focus is left by Q/E; Esc closes this page's bar");
            var twoBars = new PanelState { OpenFilters = { PanelModel.CraftFilter, PanelModel.BuildFilter } };
            PanelModel.FilterLeave(twoBars, Show(rich, twoBars));
            Check(twoBars.OpenFilters.SetEquals(new[] { PanelModel.BuildFilter }), "prefs: Esc on Crafting closes Crafting's bar only; Building keeps its own");
        }
        finally { try { System.IO.Directory.Delete(dir, true); } catch { } }
        return fails;
    }
}
