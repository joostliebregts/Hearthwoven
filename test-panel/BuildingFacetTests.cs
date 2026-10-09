// Tests for Deeds > Building's filter (src/Panel/Chapters/BuildingFacets.cs): the same filter bar as Crafting (FacetTests.cs) over
// Category (the hammer's tab) and Main material (the build resources), on the sample's hall (PanelSampleBuilding.cs).
// Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class BuildingFacetTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelView Show(PanelInput i, PanelState st = null)
    {
        st = st ?? new PanelState();
        st.Chapter = Chapter.Deeds; st.Page[Chapter.Deeds] = "building";
        return PanelModel.Build(i, st);
    }
    static Block Bar(PanelView v) => PanelModel.FilterOf(v);
    static Block Row(PanelView v, string id) => Bar(v).Items.First(b => b.Kind == "facet" && b.Id == id);
    static string Chips(PanelView v, string id) => string.Join(",", Row(v, id).Items.Select(c => c.Title + "=" + c.Value + (c.Tone == "zero" ? "(0)" : "")));
    static Block Parts(PanelView v, string id) => Bar(v).Items.First(b => b.Kind == "facetbar" && b.Id == id);
    static string Grid(PanelView v) => string.Join(",", PanelModel.Content(v).First(b => b.Kind == "itemgrid").Items.Select(i => i.Title + "=" + i.Value));
    static PieceIngredient R(string token, int amount = 1, bool refined = false, bool material = false, string name = null) => new PieceIngredient { Token = token, Amount = amount, Refined = refined, Material = material, Name = name };
    static PanelState Chosen(params (string facet, string option)[] picks)
    {
        var s = new PanelState();
        foreach (var p in picks) PanelModel.ToggleFacet(s, PanelModel.BuildFilter, p.facet, p.option);
        return s;
    }

    public static int Run(DateTime now)
    {
        fails = 0;

        // ---------- the main-material rule (what a piece costs to build) ----------
        Check(PanelModel.PieceMainMaterial(new[] { R("$item_wood", 10), R("$item_ironnails", 4) }) == "Wood" && PanelModel.PieceMainMaterial(new[] { R("$item_wood", 10), R("$item_bronzenails", 4) }) == "Wood",
              "piece material: nails never mark a tier, a Wood Gate with iron nails is Wood");
        Check(PanelModel.PieceMainMaterial(new[] { R("$item_wood", 10), R("$item_iron", 2) }) == "Iron", "piece material: a Chest of wood and iron is Iron: the metal marks the tier");
        Check(PanelModel.PieceMainMaterial(new[] { R("$item_finewood", 20), R("$item_greydwarfeye", 10, material: true, name: "Greydwarf Eye"), R("$item_surtlingcore", 2, material: true, name: "Surtling Core") }) == "Fine wood",
              "piece material: a Portal (fine wood, eyes, cores) is Fine wood: what monsters drop is no material");
        Check(PanelModel.PieceMainMaterial(new[] { R("$item_stone", 6), R("$item_wood", 2) }) == "Stone" && PanelModel.PieceMainMaterial(new[] { R("$item_roundlog", 4) }) == "Core wood" &&
              PanelModel.PieceMainMaterial(new[] { R("$item_blackmarble", 4), R("$item_iron", 1) }) == "Black marble", "piece material: stone over wood, core wood, black marble over iron");
        Check(PanelModel.PieceMainMaterial(new[] { R("$item_mithril", 5, refined: true, material: true, name: "Mithril"), R("$item_iron", 2) }) == "Mithril",
              "piece material: a mod's own bar (a smelter makes it) is its own material above the vanilla ones");
        Check(PanelModel.PieceMainMaterial(new[] { R("$item_clay", 12, material: true, name: "Clay"), R("$item_clayblock", 3, material: true, name: "Fired Clay"), R("$item_resin", 20, refined: true, material: true, name: "Resin") }) == "Clay",
              "piece material: a mod's material that no tier knows is the one the piece costs most of (Clay), resin and the like never");
        Check(PanelModel.PieceMainMaterial(new[] { R("$item_resin", 4), R("$item_flint", 2), R("$item_trophy_x", 1, name: "Trophy") }) == null && PanelModel.PieceMainMaterial(null) == null,
              "piece material: resources that mark no tier give no material (Other)");
        Check(PanelModel.TabNameOf("BuildingWorkbench") == "Building" && PanelModel.TabNameOf("BuildingStonecutter") == "Stonecutter" && PanelModel.TabNameOf("Furniture") == "Furniture" && PanelModel.TabNameOf(null) == null,
              "piece tab: the game's own words for the categories its piece tables give no label");

        // ---------- no filter: the rows, the counts, the bars ----------
        var full = PanelSample.Full(now);
        var all = Show(full);
        var bar = Bar(all);
        Check(bar != null && bar.Id == PanelModel.BuildFilter && bar.Src == "character" && bar.KeyCap == "Tab" && bar.Items.Count(b => b.Kind == "facet") == 2 && bar.Items.Count(b => b.Kind == "facetbar") == 2,
              "building filter: a filter bar with a Category row and a Main material row, a bar for each, the filter key as its keycap");
        // fix4 (rubric 4): no part of a linked bar is told by colour alone: each part has a pattern of its own (the first solid), the same on every state of the bar
        foreach (var id in new[] { "tab", "material" })
        {
            var parts = Parts(all, id).Items;
            Check(parts.Select(p => p.Pattern).Distinct().Count() == parts.Count && parts[0].Pattern == null && parts.Skip(1).All(p => p.Pattern != null && p.Pattern.StartsWith("vocab:grain-hatch-")),
                  "building filter: the " + id + " bar's parts (" + parts.Count + ") each carry a pattern of their own (the first solid), so colour is not the only cue");
        }
        Check(Row(all, "tab").Title == "Category" && Row(all, "material").Title == "Main material" && Row(all, "material").Note == "by main material" && Parts(all, "material").Note == null && Parts(all, "tab").Note == "the hammer's tab",
              "building filter: the rows are named Category and Main material, with the honest line \"by main material\"");
        Check(!Bar(all).Open && Parts(all, "tab").Tone == null && Parts(all, "material").Tone == null && Bar(Show(full, new PanelState { OpenFilters = { PanelModel.BuildFilter } })).Open, "building filter: collapsed by default with its slim bars (they fit), open after a click on the header");
        Check(Chips(all, "tab") == "Misc=38,Crafting=4,Building=1 213,Stonecutter=272,Furniture=49", "building filter: the Category chips count the pieces built per hammer tab: " + Chips(all, "tab"));
        Check(Chips(all, "material") == "Wood=1 081,Stone=272,Core wood=194,Fine wood=7,Bronze=1,Iron=21", "building filter: the Main material chips (zero ones stay, dimmed): " + Chips(all, "material"));
        var hero = PanelModel.Content(all).First(b => b.Kind == "hero");
        Check(bar.Text == "27 pieces · 1 576 built" && hero.Value == "1 576" && bar.Items.First(b => b.Kind == "applied").Title == "No filter: every piece",
              "building filter: the result line says \"27 pieces · 1 576 built\", the hero's number: " + bar.Text);
        Check(Grid(all).StartsWith("Wood Wall=520,Wood Floor 2x2=310,Stone Wall 1x1=140,Wood Beam=96,Stone Floor 2x2=90") && PanelModel.Content(all).First(b => b.Kind == "itemgrid").Items.Count == 27,
              "building filter: with nothing chosen the grid shows every piece, most first: " + Grid(all));
        Check(Math.Abs(Parts(all, "tab").Items.Sum(p => p.Fraction) - 1) < 1e-5 && Math.Abs(Parts(all, "material").Items.Sum(p => p.Fraction) - 1) < 1e-5, "building filter: each bar adds up to its whole");
        Check(Parts(all, "tab").Items.Select(p => p.Colour).Intersect(Parts(all, "material").Items.Select(p => p.Colour)).Count() == 0, "building filter: the tabs' bar and the materials' bar share no colour (no colour means two things)");

        // ---------- Furniture and Fine wood: OR within a row, AND across rows, the other row's counts follow ----------
        var fw = Show(full, Chosen(("tab", "Furniture"), ("material", "Fine wood")));
        Check(Grid(fw) == "Bed=4,Throne=1" && Bar(fw).Text == "2 pieces · 5 built", "building filter: Furniture and Fine wood leave the Bed and the Throne: \"2 pieces · 5 built\"");
        Check(Chips(fw, "tab") == "Misc=2,Crafting=0(0),Building=0(0),Stonecutter=0(0),Furniture=5" && Chips(fw, "material") == "Wood=26,Stone=0(0),Core wood=0(0),Fine wood=5,Bronze=0(0),Iron=18",
              "building filter: each row's counts follow the other row: " + Chips(fw, "tab") + " | " + Chips(fw, "material"));
        Check(Bar(fw).Items.First(b => b.Kind == "applied").Items.Select(t => t.Title + "/" + t.Text).SequenceEqual(new[] { "Furniture/Category", "Fine wood/Main material" }), "building filter: the chosen chips are removable tokens, in the order chosen");
        Check(Parts(fw, "tab").Items.First(p => p.Id == "Furniture").Selected && Parts(fw, "material").Items.First(p => p.Id == "Fine wood").Selected && Math.Abs(Parts(fw, "material").Items.First(p => p.Id == "Wood").Fraction - 26f / 49) < 1e-5,
              "building filter: the bars stay linked: the chosen parts outlined, the material bar counted over the tab only (wood 26 of 49)");
        Check(PanelModel.Content(fw).First(b => b.Kind == "hero").Value == hero.Value, "building filter: the hero keeps the whole (pieces built does not change with the filter)");
        var two = Show(full, Chosen(("tab", "Stonecutter"), ("tab", "Crafting")));
        Check(Bar(two).Text == "7 pieces · 276 built", "building filter: two chips in one row are OR: Stonecutter or Crafting");
        var empty = Show(full, Chosen(("tab", "Crafting"), ("material", "Stone")));
        Check(PanelModel.Content(empty).Any(b => b.Kind == "empty" && b.Title == "Nothing for this choice") && Row(empty, "material").Items.First(c => c.Id == "Stone").Selected && Row(empty, "material").Items.First(c => c.Id == "Stone").Tone != "zero",
              "building filter: a choice that matches nothing says so, and its chip stays choosable so it can be taken off");

        // ---------- clicks and keys are the component's own ----------
        var s = new PanelState();
        PanelModel.Follow(s, PanelModel.FacetLink(PanelModel.BuildFilter, "tab", "Furniture"));
        PanelModel.Follow(s, PanelModel.FacetLink(PanelModel.BuildFilter, "material", "Fine wood"));
        Check(PanelModel.Chosen(s, PanelModel.BuildFilter, "tab").SequenceEqual(new[] { "Furniture" }) && Grid(Show(full, s)) == "Bed=4,Throne=1", "building click: a chip's link chooses it");
        PanelModel.Follow(s, PanelModel.FacetClearTarget + PanelModel.BuildFilter);
        Check(s.Facets.Count == 0, "building click: Clear all takes every chip off");
        var f = new PanelState(); var v = Show(full, f);
        Check(PanelModel.FilterKeyPressed(f, v) && f.FilterRow == 0 && Show(full, f).Keys.Contains("[A/D] Move") && Row(Show(full, f), "tab").Items[f.FilterCursor].Title == "Misc", "building focus: the filter key enters on the first row, the cursor on its first chip");
        Check(Show(full).Keys.Any(k => k == "[Tab] Filter"), "building focus: the footer says [Tab] Filter on this page");

        // ---------- where it sits ----------
        var z = Zoned.ZoneOf(all, Bar(all));
        Check(z != null && z.Id == "character" && !Zoned.Says(all, Bar(all)) && !Bar(all).SinceInstall, "building filter: the bar sits in your character's zone with the pieces it filters, no \"since install\" on it");
        Check(PanelModel.Content(all).Count(b => b.Kind == "filterbar") == 1 && PanelModel.Content(all).Any(b => b.Kind == "ranking" && b.Src == "pc"), "building filter: the since-install zone (pieces built since, repaired) stays as it was, unfiltered");
        var tile = PanelModel.Content(all).First(b => b.Kind == "itemgrid").Items.First(i => i.Title == "Wood Wall");
        Check(tile.Value2 != null && tile.Text == PanelModel.SinceWord, "building filter: the tiles keep their part since install, said in words under the whole");

        // ---------- a mod's tab, a piece the game data cannot place, no data at all ----------
        var modded = PanelSample.Full(now); var tab0 = modded.PieceTab;
        modded.PieceTab = t => t == "$piece_table" ? "Clay Works" : t == "$piece_bed" ? null : tab0(t);
        var moddedView = Show(modded);
        Check(Row(moddedView, "tab").Items.Select(c => c.Title).SequenceEqual(new[] { "Misc", "Crafting", "Building", "Stonecutter", "Furniture", "Clay Works", "Other" }) &&
              Row(moddedView, "tab").Items.First(c => c.Title == "Clay Works").Value == "5" && Row(moddedView, "tab").Items.First(c => c.Title == "Other").Value == "4",
              "building filter: a mod's own tab joins after the hammer's (only when something was built on it), a piece with no tab is Other: " + Chips(moddedView, "tab"));
        var bare = PanelSample.Full(now); bare.PieceTab = null; bare.PieceMaterial = null;
        var bareView = Show(bare);
        Check(Bar(bareView) == null && PanelModel.Content(bareView).Any(b => b.Kind == "itemgrid") && PanelModel.Content(bareView).Any(b => b.Kind == "section" && b.Title == "Every piece") && !bareView.Keys.Any(k => k.Contains("Filter")),
              "building filter: without tabs or materials from the game data (a copy without it) the page stays the plain grid, no filter bar, no key hint");
        var onlyTabs = PanelSample.Full(now); onlyTabs.PieceMaterial = null;
        // ---------- fix3: the since-install block follows the filter ----------
        var built = PanelModel.Placed(full, "built");
        var layers = PanelModel.CounterLayers(full, PanelModel.PlacedBaseline, full.PiecesPlaced, k => built.ContainsKey(k));
        Block PcHero(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "hero" && b.Src == "pc");
        var sinceAll = layers.Values.Sum(l => l.since);
        Check(PcHero(all) != null && PanelModel.ParseCount(PcHero(all).Value) == sinceAll && PcHero(all).Note == null, "building filter: nothing chosen, the since-install hero counts every piece built since install (" + sinceAll + ")");
        var bs = Show(full, Chosen(("tab", "Building"), ("tab", "Stonecutter")));
        var sinceBs = layers.Where(kv => new[] { "Building", "Stonecutter" }.Contains(full.PieceTab(kv.Key))).Sum(kv => kv.Value.since);
        Check(PcHero(bs) != null && PanelModel.ParseCount(PcHero(bs).Value) == sinceBs && sinceBs < sinceAll && PcHero(bs).Note == PanelModel.FollowsFilter,
              "building filter: Building and Stonecutter chosen, the since-install hero follows (" + sinceBs + " of " + sinceAll + ") and says so");
        Check(PcHero(fw) == null && PanelModel.Content(fw).Any(b => b.Kind == "note" && b.Src == "pc" && b.Text == "Nothing for this choice since install."),
              "building filter: a choice with nothing since install says so in the since-install block, which is not the empty zone (its line carries no fill-up clause)");
        Check(PanelModel.Content(fw).Single(b => b.Kind == "zone" && b.Id == "pc").Tone != PanelModel.ZoneEmpty, "building filter: that zone keeps its place and its date, it is not the empty zone");
        // the same on Crafting: the gear follows the filter, upgrades are not counted by kind and the note says so
        PanelView Craft(params (string facet, string option)[] picks) { var s = new PanelState { Chapter = Chapter.Deeds }; s.Page[Chapter.Deeds] = "crafting"; foreach (var p in picks) PanelModel.ToggleFacet(s, PanelModel.CraftFilter, p.facet, p.option); return PanelModel.Build(full, s); }
        var cAll = Craft(); var cLeather = Craft(("kind", "armour"), ("material", "Leather"));
        Check(PcHero(cAll) != null && PcHero(cAll).Items != null && PcHero(cAll).Items.Any(n => n.Title.StartsWith("upgrade")) && PcHero(cAll).Note == null, "crafting filter: nothing chosen, the since-install hero says gear crafted and upgrades made");
        Check(PcHero(cLeather) != null && PcHero(cLeather).Items == null && PcHero(cLeather).Note.StartsWith(PanelModel.FollowsFilter) && PcHero(cLeather).Note.Contains("Upgrades are not counted by kind") &&
              PanelModel.ParseCount(PcHero(cLeather).Value) < PanelModel.ParseCount(PcHero(cAll).Value),
              "crafting filter: armour and leather chosen, the since-install hero counts only that gear and leaves the upgrades out, saying so");
        Check(!PanelModel.Content(cAll).Any(b => b.Kind == "note" && b.Text == PanelModel.FadedKeyTwin) && PanelModel.Content(cAll).First(b => b.Kind == "itemgrid").Items.Any(i => i.Text == PanelModel.SinceWord),
              "crafting: the tiles say \"since install\" themselves, so no key above the grid");
        Check(Chips(Show(onlyTabs), "material") == "Wood=0(0),Stone=0(0),Core wood=0(0),Fine wood=0(0),Bronze=0(0),Iron=0(0),Other=1 576" && Grid(Show(onlyTabs)).StartsWith("Wood Wall=520"),
              "building filter: tabs without materials: every piece is Other for the material, the grid still lists it all");
        return fails;
    }
}
