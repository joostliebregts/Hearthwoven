// Tests for Deeds > Meals (src/Panel/Chapters/MealsModel.cs, 0.8.1): what a player ate, a damage row per dish split by who cooked it, and the
// group's rows split by whose cooking; on the sample world (PanelSampleWorld.cs) and a long menu. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class MealsTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelView Show(PanelInput i, bool everyone = false, string view = null)
    {
        var st = new PanelState { Chapter = Chapter.Deeds, Everyone = everyone }; st.Page[Chapter.Deeds] = PanelModel.MealsPageId;
        if (view != null) st.View["Deeds/" + PanelModel.MealsPageId + "/" + PanelModel.MealsGroupSwitch] = view;
        return PanelModel.Build(i, st);
    }
    static double Num(string s) => PanelModel.ParseCount(s ?? "0");
    static Block Rows(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "sources");
    static string Parts(Block row) => string.Join(",", (row.Items ?? new List<Block>()).Where(p => p.Kind == "part").Select(p => p.Title + "=" + p.Value));
    // every row's numbered parts add up to the row, and the rows to the hero
    static bool AddsUp(PanelView v)
    {
        var rows = Rows(v)?.Items ?? new List<Block>();
        var hero = PanelModel.Content(v).First(b => b.Kind == "hero");
        return rows.All(r => !r.Items.Any(p => p.Kind == "part" && p.Value != null) || r.Items.Where(p => p.Kind == "part").Sum(p => Num(p.Value)) == Num(r.Value)) && rows.Sum(r => Num(r.Value)) == Num(hero.Value);
    }

    public static int Run(DateTime now)
    {
        fails = 0;
        var world = PanelSample.Full(now);

        // your own book: every meal and feast serving once, one row per dish in the dish's own colour, no other players on it (Joost)
        var mine = Show(world);
        var rows = Rows(mine).Items;
        var hero = PanelModel.Content(mine).First(b => b.Kind == "hero");
        Check(hero.Value == "41" && hero.Title == "meals eaten" && hero.Items?.FirstOrDefault(n => n.Kind == "number")?.Value == "9" && AddsUp(mine)
              && rows[0].Title == "Carrot Soup" && rows.Any(r => r.Title == "Meadows feast")
              && rows.All(r => r.Items.Count(p => p.Kind == "part") == 1 && r.Tone == PanelModel.DamagePlayerTone && !string.IsNullOrEmpty(r.Items[0].Colour) && !r.Items[0].Colour.StartsWith("person:"))
              && !PanelModel.AllText(mine).Any(t => t == "Edda" || t == "Tor" || t == PanelModel.NoCookTitle),
              "meals: 41 meals eaten, 9 dishes tried, one row per dish adding up to them, each one part in the dish's colour with no cooks: " + string.Join(", ", rows.Select(r => r.Title + "=" + r.Value)));

        // Everyone, By dish (the default): the group's dishes split by who cooked it, food with no cook as Gathered, a feast for its maker
        var group = Show(world, true);
        var gd = Rows(group).Items.ToDictionary(r => r.Title, r => r);
        Check(group.EveryoneOn && PanelModel.Content(group).Any(b => b.Kind == "switch" && b.Items.Any(x => x.Id == PanelModel.MealsDishView && x.Selected)) && AddsUp(group)
              && PanelModel.Content(group).First(b => b.Kind == "hero").Value == "92" && Parts(gd["Bread"]) == "You=4,Edda=3" && Parts(gd["Raspberries"]) == "Gathered=6" && Parts(gd["Meadows feast"]) == "You=17",
              "meals, Everyone By dish: 92 meals together, each dish split by who cooked it (you first, then by name), Gathered for food with no cook, a feast for its maker: "
              + Parts(gd["Bread"]) + " / " + Parts(gd["Meadows feast"]));

        // Everyone, By player: a row per player (you first, then by name), split by whose cooking they ate; the hero's second number is the cooking of another
        var byPlayer = Show(world, true, PanelModel.MealsPlayerView);
        var g = Rows(byPlayer).Items;
        var others = g.Sum(r => r.Items.Where(p => p.Kind == "part" && p.Id != PanelModel.NoCook && !string.Equals(p.Id, r.Id, StringComparison.OrdinalIgnoreCase)).Sum(p => Num(p.Value)));
        var gHero = PanelModel.Content(byPlayer).First(b => b.Kind == "hero");
        Check(g.Select(r => r.Title).SequenceEqual(new[] { "You", "Edda", "Finch", "Tor" }) && g[0].Value == "41" && AddsUp(byPlayer)
              && Num(gHero.Items?.FirstOrDefault(n => n.Kind == "number")?.Value) == others && Parts(g[3]).StartsWith("You=", StringComparison.Ordinal),
              "meals, Everyone By player: a row per player in the fixed order, each split by whose cooking it was, the hero their sum and the meals cooked by another player beside it: "
              + string.Join(" / ", g.Select(r => r.Title + " " + r.Value + " (" + Parts(r) + ")")));

        // a cook who is not around the fire (Ylva, no Hearthwoven) is Another player in the quiet grey, never a name or a person colour: the
        // group's colours stay Cooking's (REVIEW-081 batch 5: an outside cook took a person colour of their own, and one whose name sorts
        // before a member's moved that member's colour)
        var visit = PanelSample.Full(now);
        SessionEvents.Add(visit.Events.AteFoodMadeBy, "Ylva|Bread", 1);
        var vm = Show(visit, true);
        var cookSt = new PanelState { Chapter = Chapter.Deeds, Everyone = true }; cookSt.Page[Chapter.Deeds] = "cooking";
        var vc = PanelModel.Build(visit, cookSt);
        foreach (var v in new[] { vm, vc }) PanelModel.AddPlayers(v, "Rowan", visit.Fellows.Select(f => f.PlayerName), "", true);   // the player row, as PanelUi adds it
        var bread = Rows(vm).Items.First(r => r.Title == "Bread");
        Check(Parts(bread) == "You=4,Edda=3,Another player=1" && bread.Items.Last().Colour == PanelModel.BarOtherColour && AddsUp(vm)
              && vm.PersonColors.Count == vc.PersonColors.Count && vm.PersonColors.All(kv => vc.PersonColors.TryGetValue(kv.Key, out var c) && c == kv.Value)
              && !PanelModel.AllText(vm).Any(t => t.Contains("Ylva")),
              "meals, Everyone: a cook outside the group is Another player in grey, unnamed, and the person colours are Cooking's: " + Parts(bread) + " / "
              + string.Join(",", vm.PersonColors.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value)));

        // a long menu: past MealsTop dishes the smallest fold into one row, so the rows still add up to every meal; a feast set out by someone
        // not around the fire counts, its cook Another player
        var long1 = new PanelInput { PlayerName = "Rowan", NowUtc = now, Events = new SessionEvents(), DisplayName = t => t };
        for (int k = 0; k < 15; k++) SessionEvents.Add(long1.Events.AteFoodMadeBy, (k % 2 == 0 ? "Rowan" : "Edda") + "|Dish" + k.ToString("00"), 20 - k);
        SessionEvents.Add(long1.Events.AteFromFeastOf, "999|FeastSwamp", 2);
        var lv = Show(long1);
        var lr = Rows(lv).Items;
        Check(lr.Count == PanelModel.MealsTop && lr.Last().Title == PanelModel.OtherDishes(5) && lr.Last().Value == "32" && AddsUp(lv)
              && PanelModel.Content(lv).First(b => b.Kind == "hero").Value == ((20 + 6) * 15 / 2 + 2).ToString()
              && PanelModel.MealsOf(long1, long1)["FeastSwamp"].TryGetValue(PanelModel.OutsideCook, out var stranger) && stranger == 2,
              "meals: sixteen dishes show as eleven rows and \"5 other dishes\", the rows adding up to every meal; a stranger's feast counts as Another player's: " + string.Join(", ", lr.Select(r => r.Title + "=" + r.Value)));
        return fails;
    }
}
