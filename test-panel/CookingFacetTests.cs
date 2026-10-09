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

        Check(PanelModel.DishBoostOf(23, 23, 0) == "balanced" && PanelModel.DishBoostOf(27, 13, 80) == "eitr" && PanelModel.DishBoostOf(0, 0, 0) == null &&
              PanelModel.DishTypeOf(false, false, true, false, false, true) == "baked" && PanelModel.DishTypeOf(false, false, false, false, true, false) == "meadbase" && PanelModel.DishTypeOf(false, false, false, false, false, false) == null,
              "cooking filter rules: jerky (23/23) is balanced, porridge (eitr 80) eitr, no food value none; an oven's dish is baked, a fermenter's input a mead base, the rest without food value Other");
        return fails;
    }
}
