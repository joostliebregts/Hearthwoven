// Tests for Deeds > Cooking's filter (src/Panel/Chapters/CookingFacets.cs, 0.6.5): the same filter bar as Crafting and Building over
// Type (how the game makes a dish) and Main boost (its biggest food value), on the sample world's kitchen (PanelSampleWorld.cs).
// Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class CookingFacetTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelView Show(PanelInput i, params (string facet, string option)[] picks)
    {
        var st = new PanelState { Chapter = Chapter.Deeds }; st.Page[Chapter.Deeds] = "cooking";
        foreach (var p in picks) PanelModel.ToggleFacet(st, PanelModel.CookFilter, p.facet, p.option);
        return PanelModel.Build(i, st);
    }
    static string Chips(PanelView v, string id) => string.Join(",", PanelModel.FilterOf(v).Items.First(b => b.Kind == "facet" && b.Id == id).Items.Select(c => c.Title + "=" + c.Value));
    static string Dishes(PanelView v) => string.Join(",", PanelModel.Content(v).First(b => b.Kind == "composition").Items.Select(i => i.Title + "=" + i.Value));

    public static int Run(DateTime now)
    {
        fails = 0;
        var world = PanelSample.Full(now);

        // a dish lands in its type by the game's data; a mod's brew the data cannot place lands in Other on both rows, by its name, never its id
        var all = Show(world);
        Check(Chips(all, "type") == "Meals=138,Grilled=8,Baked=12,Other=8" && Chips(all, "boost") == "Health=54,Stamina=104,Eitr=0,Other=8" &&
              !PanelModel.FacetNumberLabels(all).Any() && !PanelModel.AllText(all).Any(t => t.Contains("$item_")),
              "cooking filter: the dishes by type (cauldron and prep table 138, grill 8, oven 12) and main boost; Spiced Cider, a mod's brew, under Other: " + Chips(all, "type") + " / " + Chips(all, "boost"));

        // a chosen type narrows the dish bar and the ledger; who enjoyed your food is counted on the fellows' PCs and says it stays whole
        var stamina = Show(world, ("boost", "stamina"));
        var ledger = PanelModel.Content(stamina).First(b => b.Kind == "ledger");
        Check(Dishes(stamina) == "Carrot Soup=60,Queen's Jam=20,Bread=12,Turnip Stew=12" && ledger.Items.Select(r => r.Title + "=" + r.Value).SequenceEqual(new[] { "Cauldron and prep table=92", "Cooking stations and oven=12" }) &&
              PanelModel.Content(stamina).First(b => b.Kind == "hero").Value == "166" && PanelModel.AllText(stamina).Contains(PanelModel.EnjoyedWhole("your")) && !PanelModel.AllText(all).Contains(PanelModel.EnjoyedWhole("your")),
              "cooking filter: Stamina narrows the dish bar and the ledger (92 + 12), the headline stays the whole 166, and the fellows' servings say they are not filtered: " + Dishes(stamina));

        // 0.8.2 (Joost in game: Dishes by kind did not filter while By type did): a click on a dish's part chooses that dish as By type's parts
        // choose a type. The bar keeps every dish, the chosen one outlined (it is the Dish row's bar, counted over what the other rows leave);
        // the ledger and the result line follow; the Dish chip and the token say it. In a day window without that dish its chip stays, chosen
        var st = new PanelState { Chapter = Chapter.Deeds }; st.Page[Chapter.Deeds] = "cooking";
        var soup = PanelModel.Content(all).First(b => b.Kind == "composition").Items.First(p => p.Title == "Carrot Soup");
        var followed = PanelModel.FollowFacet(st, PanelModel.PartTarget(PanelModel.Content(all).First(b => b.Kind == "composition"), soup));
        var picked = PanelModel.Build(world, st);
        var byKind = PanelModel.Content(picked).First(b => b.Kind == "composition");
        var chip = PanelModel.FilterOf(picked).Items.First(b => b.Kind == "facet" && b.Id == PanelModel.DishFacet).Items.First(c => c.Id == soup.Id);
        var applied = PanelModel.FilterOf(picked).Items.First(b => b.Kind == "applied");
        Check(followed && Dishes(picked) == Dishes(all) && byKind.Items.Where(p => p.Selected).Select(p => p.Title).SequenceEqual(new[] { "Carrot Soup" }) &&
              PanelModel.BarFormOf(byKind.Items).Rows.Single(r => r.Selected).Title == "Carrot Soup" && chip.Selected &&
              applied.Text == "1 dish · 60 cooked" && applied.Items.Select(t => t.Title).SequenceEqual(new[] { "Carrot Soup" }) &&
              PanelModel.Content(picked).First(b => b.Kind == "ledger").Items.SelectMany(r => r.Items).Select(c => c.Title).SequenceEqual(new[] { "Carrot Soup" }),
              "cooking by kind: a click on Carrot Soup's part chooses it (its legend item lit, its chip chosen, its token, 1 dish · 60 cooked, the ledger narrowed), the bar keeps every dish: " + applied.Text + " / " + Dishes(picked));
        PanelView Week(params string[] dishes) => PanelModel.Build(world, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = "cooking" }, Window = TimeWindow.SevenDays, WindowPicked = true,
                                                                                         Facets = { [PanelModel.CookFilter + "|" + PanelModel.DishFacet] = dishes.ToList() } });
        var none = PanelModel.Content(all).First(b => b.Kind == "composition").Items.First(p => !PanelModel.Content(Week()).First(b => b.Kind == "composition").Items.Any(w => w.Id == p.Id));
        var week = Week(none.Id);
        var stale = PanelModel.FilterOf(week).Items.First(b => b.Kind == "facet" && b.Id == PanelModel.DishFacet).Items.FirstOrDefault(c => c.Id == none.Id);
        Check(stale != null && stale.Selected && stale.Title == none.Title && stale.Tone != "zero" && PanelModel.FilterOf(week).Items.First(b => b.Kind == "applied").Items.Select(t => t.Title).SequenceEqual(new[] { none.Title }) &&
              PanelModel.Content(week).Any(b => b.Kind == "empty"),
              "cooking by kind: " + none.Title + ", chosen, none in the last 7 days: its chip and token stay, so it can be unchosen; Nothing for this choice under the bar");

        Check(PanelModel.DishBoostOf(23, 23, 0) == "balanced" && PanelModel.DishBoostOf(27, 13, 80) == "eitr" && PanelModel.DishBoostOf(0, 0, 0) == null &&
              PanelModel.DishTypeOf(false, false, true, false, false, true) == "baked" && PanelModel.DishTypeOf(false, false, false, false, true, false) == "meadbase" && PanelModel.DishTypeOf(false, false, false, false, false, false) == null,
              "cooking filter rules: jerky (23/23) is balanced, porridge (eitr 80) eitr, no food value none; an oven's dish is baked, a fermenter's input a mead base, the rest without food value Other");
        return fails;
    }
}
