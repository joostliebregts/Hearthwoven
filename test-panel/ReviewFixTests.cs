// 0.6.5 review fixes: one test per finding (the review of the 0.6.5 test build, 2026-10-09).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class ReviewFixTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var now = new DateTime(2026, 10, 7, 22, 30, 0, DateTimeKind.Utc);

        // 1. a greyed 7 days pressed holds on the page where it was pressed: Mining and Defence again read the last window that worked, with no line
        {
            var i = PanelSample.Full(now); i.History.From = PanelModel.LocalToday(i);   // the day history began today: 7 days waits
            bool Line(PanelView v) => PanelModel.AllText(v).Any(t => t.Contains("works from"));
            var st = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Today }; st.Page[Chapter.Battle] = "defense";
            var before = PanelModel.Build(i, st);
            st.Window = TimeWindow.SevenDays;   // the greyed chip pressed (PanelUi's chip)
            var pressed = PanelModel.Build(i, st);
            st.Chapter = Chapter.Deeds; st.Page[Chapter.Deeds] = "mining"; var mining = PanelModel.Build(i, st);
            st.Chapter = Chapter.Battle; var back = PanelModel.Build(i, st);
            Check(Line(pressed) && pressed.ShownWindow == TimeWindow.SinceInstall && !Line(mining) && mining.ShownWindow == TimeWindow.Today && !Line(back) && back.ShownWindow == TimeWindow.Today && st.Window == TimeWindow.Today,
                  "wait line: a greyed 7 days pressed on Defence says its line there only; Mining and Defence again show Today, the last window that worked (" + st.Window + ")");
            // 0.8.1 review 3: each greyed day chip its own reason; with 7 days pressed the greyed 30 days says its own day, never 7 days' line
            string Why(PanelView v, string id) => v.Windows.Single(c => c.Id == id).Why ?? "";
            Check(pressed.WindowTip == "SevenDays" && Why(pressed, "SevenDays").StartsWith("7 days works from ") && Why(pressed, "ThirtyDays").StartsWith("30 days works from ") &&
                  !Why(pressed, "ThirtyDays").Contains(Why(pressed, "SevenDays")) && before.WindowTip == null &&
                  Why(before, "SevenDays") == Why(pressed, "SevenDays") && Why(before, "ThirtyDays") == Why(pressed, "ThirtyDays"),
                  "window reasons: each greyed day chip says its own day, before a press too; the pressed 7 days keeps its line, the greyed 30 days never shows it (" + Why(pressed, "ThirtyDays") + ")");
            // Together: the same with its own switch
            const string wkey = "Company/together/window";
            var tg = new PanelState { Chapter = Chapter.Company }; tg.Page[Chapter.Company] = "together"; tg.View["Company/together/category"] = "dealt"; tg.View[wkey] = "Today";
            i.Fellows = new List<PanelInput>();
            PanelModel.Build(i, tg);
            tg.View[wkey] = "SevenDays";
            var tPressed = PanelModel.Build(i, tg);
            tg.Chapter = Chapter.Battle; PanelModel.Build(i, tg);
            tg.Chapter = Chapter.Company; var tBack = PanelModel.Build(i, tg);
            Check(Line(tPressed) && !Line(tBack) && tg.View[wkey] == "Today", "wait line: Together's greyed 7 days says its line once; after another page Together is back on Today (" + tg.View[wkey] + ")");
        }
        // 2. Shieldbearer's reason says blocks and parries as Defence's tile does (the game's held blocks include the parries)
        {
            var b = PanelSample.Battle();
            var st = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.SinceInstall }; st.Page[Chapter.Battle] = "defense";
            var def = PanelModel.Build(b, st);
            var guard = PanelModel.Content(def).FirstOrDefault(x => x.Kind == "guard");
            var reason = PanelModel.Titles(b).Single(t => t.Id == "defender").Lines.Single().Key;
            var strip = PanelModel.BandTitles(PanelModel.Content(def).FirstOrDefault(x => x.Kind == "featband")).FirstOrDefault(t => t.Title == "Shieldbearer");
            Check(guard != null && reason == guard.Value + " " + guard.Title + " · " + guard.Value2 + " " + guard.Text && (strip?.Text == null || strip.Text == reason) && PanelModel.GuardSplit(5, 0) == "5 blocks" && PanelModel.GuardSplit(3, 3) == "3 parries",
                  "Shieldbearer: its reason has the Defence tile's numbers (" + reason + " / " + guard?.Value + " " + guard?.Title + ", " + guard?.Value2 + " " + guard?.Text + "), a zero part left out");
        }
        // 3. Cooking's Other (id "other") is the bars' Other as Building's and Crafting's "Other" are: last, the quiet colour, its own name
        {
            var st = new PanelState { Chapter = Chapter.Deeds }; st.Page[Chapter.Deeds] = "cooking";
            var cook = PanelModel.Build(PanelSample.Full(now), st);
            var bars = PanelModel.FilterOf(cook).Items.Where(b => b.Kind == "facetbar").ToList();
            Check(bars.Count == 2 && bars.All(b => b.Items.Last().Id == PanelModel.DishOther && b.Items.Last().Title == "Other" && b.Items.Last().Colour == PanelModel.BarOtherColour && b.Items.Count(p => p.Id == PanelModel.DishOther) == 1),
                  "Cooking's Other: last in both bars, the quiet colour, never a named or folded part (" + string.Join(" / ", bars.Select(b => string.Join(",", b.Items.Select(p => p.Title + ":" + p.Colour)))) + ")");
        }
        // 5. a mod's all-in-one tool with one seed and one ground tool among its buildings is no cultivator: the buildings stay built
        {
            var table = new List<(bool terrainOp, bool plant, bool feast, bool pickable)> { (true, false, false, false), (false, true, false, false) };
            for (int k = 0; k < 8; k++) table.Add((false, false, false, false));
            table.Add((false, false, true, false));
            var kinds = PanelModel.PieceKindsOfTable(table);
            Check(kinds[0] == "ground" && kinds[1] == "planted" && kinds.Skip(2).Take(8).All(k => k == "built") && kinds[10] == "feast",
                  "cultivator rule: a mixed modded table (8 buildings, a seed, a ground tool, a feast) keeps its buildings built (" + string.Join(",", kinds) + ")");
        }
        return fails;
    }
}
