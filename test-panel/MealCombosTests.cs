// Tests for Meals' Your combinations (src/Panel/Chapters/MealCombosModel.cs, 0.9): the foods you had on together at each meal, one damage row per
// combination with its dishes' pictures, most eaten first, the bar split by the game's food values, the caption's words on where the top
// combinations lean, the day windows, the fold, and the house sentences. On Rowan's sample (src/Panel/PanelSampleMeals.cs). Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class MealCombosTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelView Show(PanelInput i, TimeWindow? w = null, bool everyone = false, string player = "")
    {
        var st = new PanelState { Chapter = Chapter.Deeds, Everyone = everyone, Player = player }; st.Page[Chapter.Deeds] = PanelModel.MealsPageId;
        if (w.HasValue) { st.Window = w.Value; st.WindowPicked = true; }
        return PanelModel.Build(i, st);
    }
    static Block Head(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "section" && b.Title == PanelModel.CombosTitle);
    static Block After(PanelView v, Block b) { var l = PanelModel.Content(v); var at = l.IndexOf(b); return at >= 0 && at + 1 < l.Count ? l[at + 1] : null; }
    static Block List(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "sources" && b.Tone == PanelModel.ComboTone);
    static string Ids(Block list) => string.Join(" ", (list?.Items ?? new List<Block>()).Select(r => r.Id + "=" + r.Value));

    public static int Run(DateTime now)
    {
        fails = 0;
        var rowan = PanelSample.Full(now);

        // ---------- All: the island ----------
        var all = Show(rowan);
        var head = Head(all); var list = List(all);
        Check(head != null && list != null && After(all, head) == list, "combos: the island's head \"Your combinations\" with the rows under it, on your own book in All");
        var from = PanelModel.RecordDate(rowan, rowan.MealCombos.FromUtc);
        Check(head?.Text == "Recorded from " + from + " · this PC" && string.IsNullOrEmpty(head.RecordedFrom), "combos: the head says \"Recorded from " + from + " · this PC\" (" + head?.Text + ")");
        Check(all.HeadingRecordedFrom == "Recorded from " + PanelModel.RecordDate(rowan, rowan.InstalledUtc.Value) + " · this PC" && PanelModel.Content(all).Count(b => !string.IsNullOrEmpty(b.RecordedFrom)) == 0,
              "combos: the page's own date stays one line in the heading (the island says its own), never said twice (" + all.HeadingRecordedFrom + ")");
        Check(Ids(list) == "CarrotSoup+Honey+QueensJam=7 Bread+CarrotSoup+Raspberry=5 CookedMeat+FishWraps+Sausages=4 Bread+CarrotSoup+Honey=3 CarrotSoup+FishWraps+Sausages=3 FeastMeadows+QueensJam=2 Raspberry=1",
              "combos: one row per combination, most eaten first, ties by name (" + Ids(list) + ")");
        var first = list?.Items?.FirstOrDefault();
        var dishes = first?.Items?.Where(i => i.Kind == PanelModel.ComboDishKind).ToList() ?? new List<Block>();
        Check(first != null && first.Tone == PanelModel.ComboTone && dishes.Select(d => d.Icon).SequenceEqual(new[] { "item:CarrotSoup", "item:Honey", "item:QueensJam" }) && first.Title == "Carrot Soup, Honey, Queen's Jam",
              "combos: a row's picture is its dishes' item icons side by side, their names under them (" + first?.Title + ")");
        var parts = first?.Items?.Where(i => i.Kind == "part").ToList() ?? new List<Block>();
        Check(parts.Select(p => p.Id).SequenceEqual(new[] { "health", "stamina" }) && Math.Abs(parts.Sum(p => p.Fraction) - 1f) < 1e-4 && Math.Abs(parts[1].Fraction - 120f / 157f) < 1e-4
              && parts[0].Colour == PanelModel.CategoryColour("health") && parts[1].Colour == PanelModel.CategoryColour("stamina") && parts.All(p => p.Tone == PanelModel.ComboTone),
              "combos: the bar is split by the game's food values summed (37 health, 120 stamina), in Cooking's boost colours, no number inside (Tone combo)");
        // 0.9.2 (Joost in game: "this is silly in percentages.. We need real numbers"): the line says each part's value in game units, the bars
        // stand on one scale of the combinations' totals (the biggest total the full track), the thin line under each is its meals
        var form = PanelModel.BarFormOf(parts, true);
        var lineWords = form.Rows.Select(PanelModel.DamageLineWords).ToList();
        Check(lineWords.SequenceEqual(new[] { "Health 37", "Stamina 120" }) && !lineWords.Any(w => w.Contains("%")) && form.Rows.Select(PanelModel.DamageHoverWords).SequenceEqual(lineWords),
              "real numbers: the line says each part's value as the game does (" + string.Join(" · ", lineWords) + "), never a share; pointing at a part says the same");
        var totals = new Dictionary<string, double> { ["CarrotSoup+Honey+QueensJam"] = 157, ["Bread+CarrotSoup+Raspberry"] = 180, ["CookedMeat+FishWraps+Sausages"] = 206, ["Bread+CarrotSoup+Honey"] = 196,
                                                      ["CarrotSoup+FishWraps+Sausages"] = 226, ["FeastMeadows+QueensJam"] = 124, ["Raspberry"] = 27 };
        Check(list.Items.All(r => totals.ContainsKey(r.Id) && Math.Abs(r.Fraction - totals[r.Id] / 226.0) < 1e-4) && list.Items.Count(r => Math.Abs(r.Fraction - 1f) < 1e-4) == 1
              && list.Items.First(r => r.Id == "CarrotSoup+FishWraps+Sausages").Fraction > first.Fraction && list.Items.All(r => r.Src == null),
              "real numbers: the bars on one scale of the totals, the biggest (226) the full track, a stronger combination a longer bar (" + string.Join(" ", list.Items.Select(r => r.Id + "=" + r.Fraction.ToString("0.00"))) + ")");
        Check(list.Items.All(r => Math.Abs(r.Fraction2 - PanelModel.ParseCount(r.Value) / 7f) < 1e-4) && Math.Abs(first.Fraction2 - 1f) < 1e-4,
              "real numbers: the thin line under a bar is its meals against the most eaten row's (the number in front, 7 = full)");
        Check(PanelModel.ComboValue(32.5) == "33" && PanelModel.ComboValue(157) == "157" && PanelModel.ComboValue(-3) == "0",
              "real numbers: whole game units, rounded half away from zero as the game's tooltip (ToString(\"0\"))");
        Check(list?.Text == "Going hard on stamina: 3 of your top 5 lean on stamina.", "combos: the caption says where the top five lean (" + list?.Text + ")");
        var words = string.Join(" ", PanelModel.Content(all).Where(b => b == head || b == list).SelectMany(b => new[] { b.Title, b.Text }.Concat((b.Items ?? new List<Block>()).Select(r => r.Title))));
        Check(!new[] { "best", "rank", "score", "top player", "winner" }.Any(x => words.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0), "combos: plain words about foods, never a score or a ranking");

        // ---------- the focus line ----------
        (float, float, float) S(float h, float s, float e = 0) => (h, s, e);
        Check(PanelModel.CombosFocus(new[] { S(70, 23), S(55, 18), S(20, 60) }) == "Going hard on health: 2 of your top 3 lean on health.", "focus: two of three lean on health");
        Check(PanelModel.CombosFocus(new[] { S(15, 45), S(7, 20) }) == "Going hard on stamina: all your top 2 lean on stamina.", "focus: every one leans the same way");
        Check(PanelModel.CombosFocus(new[] { S(70, 23), S(15, 45), S(10, 10, 60), S(30, 30) }) == "A mix: of your top 4, 1 leans on health, 1 on stamina, 1 on eitr and 1 is balanced.", "focus: none most share: a mix, each named");
        Check(PanelModel.CombosFocus(new[] { S(30, 28) }) == "Your combination is balanced." && PanelModel.CombosFocus(new[] { S(5, 40) }) == "Your combination leans on stamina.", "focus: one combination");
        Check(PanelModel.CombosFocus(new[] { S(0, 0) }) == null && PanelModel.CombosFocus(new (float, float, float)[0]) == null, "focus: no food value known: no words");

        // ---------- the day windows ----------
        // Today: on the sample's evening the day history holds no meal today, so the page says Nothing today and the island stays away
        var today = Show(rowan, TimeWindow.Today);
        Check(Head(today) == null && PanelModel.Content(today).Any(b => b.Kind == "empty"), "combos: a window with no meal on the page: no island, the page's own empty line");
        var lt = PanelModel.LocalToday(rowan);
        var dayIn = rowan.MealCombos.Between(lt.AddDays(-1), lt.AddDays(-1));
        Check(dayIn.Count == 2 && dayIn["CarrotSoup+Honey+QueensJam"] == 2 && dayIn["CookedMeat+FishWraps+Sausages"] == 2, "combos: one day's combinations are that day's meals only (yesterday: 2 and 2)");
        var week = Show(rowan, TimeWindow.SevenDays);
        var line = PanelModel.KeptFromLine("Combinations", PanelModel.LocalToday(rowan).AddDays(-4), PanelModel.LocalToday(rowan));
        Check(List(week) != null && PanelModel.Content(week).Any(b => b.Kind == "note" && b.Text == line), "combos: 7 days reaches before the record: \"" + line + "\"");
        Check(Ids(List(week)) == Ids(list), "combos: 7 days holds every meal of the record (all of it in the last 5 days)");

        // ---------- the fold ----------
        var many = PanelSample.Full(now); var rec = new MealCombos { FromUtc = now.AddDays(-1) };
        for (int k = 0; k < 10; k++) for (int n = 0; n <= k; n++) rec.Add(new[] { "Dish" + k }, 10, 20, 0, PanelModel.LocalToday(many));
        many.MealCombos = rec;
        var fold = List(Show(many));
        Check(fold != null && fold.Items.Last().Items.Count == 0 && fold.Items.Last().Fraction == 0 && fold.Items.Take(7).All(r => Math.Abs(r.Fraction - 1f) < 1e-4),
              "fold: the folded row has its meals only, no bar on the totals' scale; equal totals, equal bars");
        Check(fold != null && fold.Items.Count == PanelModel.CombosTop && fold.Items.Last().Title == "3 other combinations" && fold.Items.Last().Value == "6" && fold.Items.Take(7).All(r => r.Tone == PanelModel.ComboTone),
              "fold: past " + PanelModel.CombosTop + " rows the rest fold into one row that says how many (" + fold?.Items.Last().Title + " " + fold?.Items.Last().Value + ")");
        var capped = PanelSample.Full(now); var big = new MealCombos { FromUtc = now.AddDays(-1) };
        for (int k = 0; k < MealCombos.MaxCombos + 5; k++) big.Add(new[] { "Dish" + k.ToString("000") }, 10, 20, 0, PanelModel.LocalToday(capped));
        capped.MealCombos = big;
        var cappedList = List(Show(capped));
        Check(cappedList != null && cappedList.Items.Last().Title == PanelModel.OtherCombosTitle && cappedList.Items.Sum(r => PanelModel.ParseCount(r.Value)) == MealCombos.MaxCombos + 5,
              "fold: the record's own rest (past its 200) is \"Other combinations\", never a number too low; every meal still in a row");

        // ---------- the house sentences, and where there is no island ----------
        var empty = PanelSample.Full(now); empty.MealCombos = MealCombosSample.For(empty, "empty");
        var ev = Show(empty); var eh = Head(ev); var eb = eh == null ? null : After(ev, eh);
        Check(eh != null && eb?.Kind == "empty" && eb.Title == PanelModel.NothingFrom(empty, empty.MealCombos.FromUtc) && eb.Text == PanelModel.CombosNoneYet && List(ev) == null,
              "empty: recorded, none yet: \"" + eb?.Title + "\" and how it fills");
        var none = PanelSample.Full(now); none.MealCombos = null;
        var nv = Show(none); var nh = Head(nv); var nb = nh == null ? null : After(nv, nh);
        Check(nh != null && nb?.Kind == "empty" && nb.Title == PanelModel.CombosNotRecordedTitle && List(nv) == null && nh.Text == null, "not recorded: \"Not recorded yet\", never a 0");
        Check(Head(Show(rowan, null, true)) == null, "no island with the Everyone chip: the record is this PC's only, never shared");
        Check(Head(Show(rowan.Fellows.First(f => f.PlayerName == "Edda"))) == null, "no island on a fellow's book: their combinations stay on their PC");
        var hungry = PanelSample.Full(now); hungry.Events.AteFoodMadeBy.Clear(); hungry.Events.AteFromFeastOf.Clear(); hungry.Events.AteFromFeastAt.Clear();
        Check(Head(Show(hungry)) == null, "no meal on the page: no island (the page's own empty line speaks)");

        // ---------- the dishes' pictures: What to use's item picture, in the panel and the preview alike (Joost, 18 Oct: "I always look at the icons") ----------
        {
            var src = AppContext.BaseDirectory;
            while (src != null && !System.IO.File.Exists(System.IO.Path.Combine(src, "src", "Plugin.cs"))) src = System.IO.Path.GetDirectoryName(src);
            string Read(params string[] p) => src == null ? "" : System.IO.File.ReadAllText(System.IO.Path.Combine(new[] { src, "src", "Panel" }.Concat(p).ToArray()));
            var rows = Read("Chapters", "DamageRowsUi.cs"); var use = Read("Chapters", "WhatToUseUi.cs"); var html = Read("preview", "panel-preview.html");
            var ut = System.Text.RegularExpressions.Regex.Match(use, @"\bUtIcon = (\d+)\b"); var size = ut.Success ? int.Parse(ut.Groups[1].Value) : 0;
            Check(rows.Contains("exact: combo, trueFraction: combo ? row.Fraction2 : -1f") && rows.Contains("row.Part?.Tone == PanelModel.ComboTone) continue;")
                  && html.Contains("combo ? drTrue(fraction) : drWidth(fraction)") && html.Contains("trueF = combo ? r.fraction2 : fraction") && html.Contains("!(x.part && x.part.tone === \"combo\")"),
                  "real numbers: panel and preview draw a combination's bar from zero (no floor), the thin line its meals, no number inside");
            Check(size >= 40 && System.Text.RegularExpressions.Regex.IsMatch(rows, @"\bDrComboIcon = UtIcon\b")
                  && html.Contains("btSlot(d.icon, " + size + ")") && html.Contains(".dr-name.dr-combo > .pic { width:auto; height:auto; }"),
                  "pictures: a dish's picture is What to use's item picture (" + size + " px), in the panel (DrComboIcon = UtIcon) and the preview's .dr-combo");
        }

        // ---------- Meals by dish: what one serving gives (0.9.2, Joost chose it from a prototype) ----------
        {
            Block Dishes(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "sources" && b.Title == PanelModel.MealsByDish);
            Block Serving(Block row) => row?.Items?.FirstOrDefault(i => i.Kind == PanelModel.ServingKind);
            var dl = Dishes(all);
            var soup = dl?.Items?.FirstOrDefault(r => r.Id == "CarrotSoup");
            Check(dl != null && dl.Text == PanelModel.MealsServingText && Serving(soup)?.Text == "15 · 45 · 25m",
                  "serving: the game's food values of one serving and how long it lasts beside the dish (" + Serving(soup)?.Text + "), the caption says what the column is");
            var sp = Serving(soup)?.Items ?? new List<Block>();
            Check(sp.Select(p => p.Id).SequenceEqual(new[] { "health", "stamina" }) && sp[0].Colour == PanelModel.CategoryColour("health") && Math.Abs(sp[1].Fraction - 45f / 60f) < 1e-4,
                  "serving: its bar in the boosts' colours (Your combinations'), parts as long as their values");
            var most = 93.0;   // Bread and Fish Wraps: 23 + 70, 70 + 23
            var servTotals = new Dictionary<string, double> { ["CarrotSoup"] = 60, ["Raspberry"] = 27, ["Bread"] = 93, ["CookedMeat"] = 40, ["FishWraps"] = 93, ["QueensJam"] = 54, ["Honey"] = 43, ["Sausages"] = 73, ["FeastMeadows"] = 70 };
            var served = (dl?.Items ?? new List<Block>()).Where(r => Serving(r) != null).ToList();
            Check(served.Count == 9 && served.All(r => servTotals.ContainsKey(r.Id) && Math.Abs(Serving(r).Fraction2 - (float)(servTotals[r.Id] / most)) < 1e-4)
                  && served.Count(r => Math.Abs(Serving(r).Fraction2 - 1f) < 1e-4) == 2 && Math.Abs(Serving(soup).Fraction2 - (float)(60 / most)) < 1e-4
                  && served.All(r => Serving(r).Fraction == 0) && (dl?.Items ?? new List<Block>()).All(r => Math.Abs(r.Items.Where(i => i.Kind == "part").Sum(i => i.Fraction) - 1f) < 1e-4),
                  "serving: one scale for the list (the biggest serving, 93, the longest); the row's own bar still its whole");
            Check(PanelModel.ServingOf(new PanelModel.FoodStats { Health = 80, Stamina = 26, Seconds = 1800 }, 100).Text == "80 · 26 · 30m"
                  && PanelModel.ServingOf(new PanelModel.FoodStats { Health = 30, Stamina = 26, Eitr = 30, Seconds = 1800 }, 100).Text == "30 · 26 · 30 · 30m"
                  && PanelModel.ServingTime(3600) == "1h" && PanelModel.ServingTime(3900) == "1h 5m" && PanelModel.ServingTime(0) == null
                  && PanelModel.ServingOf(new PanelModel.FoodStats(), 100) == null,
                  "serving: eitr only when there is some, minutes as the duration, nothing for no food value");
            // a mod's dish the game's data has no food value for, and the dishFold past MealsTop: no column, never a 0
            var modded = PanelSample.Full(now);
            for (int k = 0; k < 6; k++) SessionEvents.Add(modded.Events.AteFoodMadeBy, "unknown|ModDish" + k, 1);
            var ml = Dishes(Show(modded));
            var dishFold = ml?.Items?.LastOrDefault();
            Check(ml != null && ml.Items.Count == PanelModel.MealsTop && dishFold?.Id == PanelModel.FoldId && Serving(dishFold) == null
                  && ml.Items.Where(r => r.Id.StartsWith("ModDish")).All(r => Serving(r) == null) && ml.Items.Any(r => r.Id.StartsWith("ModDish"))
                  && Serving(ml.Items.First(r => r.Id == "CarrotSoup"))?.Text == "15 · 45 · 25m",
                  "serving: an unknown dish's column stays empty, the folded row has none, the known dishes keep theirs");
            var blind = PanelSample.Full(now); blind.DishStats = null;
            var bl = Dishes(Show(blind));
            Check(bl != null && bl.Text == null && bl.Items.All(r => Serving(r) == null), "serving: no game data at all: no column and no caption words");
            var group = Dishes(Show(rowan, null, true));
            Check(group == null || group.Items.All(r => Serving(r) == null), "serving: a player's own book only; the group's by-cook bars keep their line");
            var src = AppContext.BaseDirectory;
            while (src != null && !System.IO.File.Exists(System.IO.Path.Combine(src, "src", "Plugin.cs"))) src = System.IO.Path.GetDirectoryName(src);
            string Read(params string[] p) => src == null ? "" : System.IO.File.ReadAllText(System.IO.Path.Combine(new[] { src, "src", "Panel" }.Concat(p).ToArray()));
            var gd = Read("GameData.cs"); var rowsUi = Read("Chapters", "DamageRowsUi.cs"); var html = Read("preview", "panel-preview.html");
            Check(gd.Contains("Health = shared.m_food, Stamina = shared.m_foodStamina, Eitr = shared.m_foodEitr, Seconds = shared.m_foodBurnTime") && rowsUi.Contains("ServingCell(r, ServingOf(row), Column - servW)")
                  && html.Contains("right: drServCell"),
                  "serving: read from the game's own food data (ObjectDB), drawn right of the track in the panel and the preview alike");
        }

        System.Console.WriteLine(fails == 0 ? "meal-combos: ALL PASS" : "meal-combos: " + fails + " FAILED");
        return fails;
    }
}
