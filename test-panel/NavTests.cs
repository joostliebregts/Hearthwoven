// Navigation (integrate-05, Joost's live test): Backspace goes back to the page you came from while the panel is open.
using System;
using System.Linq;
using Hearthwoven.Panel;

static class NavTests
{
    public static int Run(PanelInput input)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var s = new PanelState();
        void Show() { var v = PanelModel.Build(input, s); PanelModel.Visited(s, v.Active, v.Page); }   // as PanelUi.Render

        PanelModel.ForgetHistory(s); Show();                               // opens on Deeds > Overview
        s.Page[Chapter.Deeds] = "cooking"; Show(); Show();                 // the list; a refresh adds nothing
        PanelModel.StepChapter(s, 2); Show();                              // two chapters on (Q/E or the arrows)
        s.ShowAbout = true; Show();                                        // About
        Check(s.History.Count == 3, "nav: every page you leave goes on the back stack once (refreshes add nothing)");
        Check(PanelModel.Back(s) && !s.ShowAbout && s.Chapter == Chapter.Company, "nav: Backspace on About goes back to the page it was opened from");
        Show();
        Check(PanelModel.Back(s) && s.Chapter == Chapter.Deeds && s.PageOf(Chapter.Deeds) == "cooking", "nav: Backspace again returns to Deeds > Cooking, the page you came from");
        Show();
        Check(PanelModel.Back(s) && s.PageOf(Chapter.Deeds) == "overview" && s.History.Count == 0, "nav: and then to Deeds > Overview, where the panel opened");
        Show();
        Check(!PanelModel.Back(s), "nav: nothing left to go back to: Backspace does nothing");
        s.Page[Chapter.Deeds] = "building"; Show();
        PanelModel.ForgetHistory(s); Show();
        Check(!PanelModel.Back(s), "nav: a new opening starts a fresh history");
        for (int i = 0; i < PanelModel.HistoryMax + 10; i++) { PanelModel.StepChapter(s, 1); Show(); }
        Check(s.History.Count == PanelModel.HistoryMax, "nav: the back stack keeps the last " + PanelModel.HistoryMax + " pages");
        // the plate on a short page (B12 screenshot note): it ends where the content ends; a long page keeps it all and scrolls
        Check(PanelModel.PlateCut(640, 300) == 340 && PanelModel.PlateCut(640, 900) == 0 && PanelModel.PlateCut(640, 640) == 0, "plate: a short page's plate ends at its content, a long one keeps the full height");
        // fix2 1 (Joost, in game: Woodcutting, Mining and Farming cut short, no scrolling): plate height = min(content, room);
        // the content is what is really drawn, so a block that draws below its reported height still counts; a page taller
        // than the room keeps the room and scrolls (Woodcutting, Mining or Farming with real data: 1,150 px in 640)
        Check(PanelModel.PlateHeight(640, 300) == 300 && PanelModel.PlateHeight(640, 1150) == 640 && PanelModel.PlateHeight(640, 640) == 640,
              "plate: its height is min(content, room)");
        Check(PanelModel.PlateNeed(420, 610, 10) == 620 && PanelModel.PlateNeed(420, 300, 10) == 420 && PanelModel.PlateNeed(420, 0, 10) == 420,
              "plate: the content needs the lowest drawn thing plus its padding when that reaches below the layout's height, never less than the layout");
        foreach (var page in new[] { "woodcutting", "mining", "farming" })
        {
            var need = PanelModel.PlateNeed(380, 1150, 10);   // the layout reported 380 px, the blocks drew down to 1,150 px
            var shown = PanelModel.PlateHeight(640, need);
            Check(shown == 640 && PanelModel.ScrollRoom(need, shown) == 520, "plate: a long " + page + " page keeps the whole room and scrolls the rest (520 px), not cut at a short measure");
        }
        // fix2 3 (Joost: the wood bar's dark lines did not sit on the kind boundaries): one span per kind, back to back; the
        // faded part ends inside its own kind, never on another's boundary
        var bp = PanelModel.BarParts(new[] { 0.715f, 0.168f, 0.095f, 0.021f }, new[] { 0.93f, 1f, 0.95f, 0f }, 4f / 741f);
        Check(bp.Length == 4 && bp[0].from == 0 && bp[3].to == 1 && Enumerable.Range(0, 3).All(k => bp[k].to == bp[k + 1].from) &&
              bp.All(p => p.fadedTo >= p.from && p.fadedTo <= p.to) && Math.Abs(bp[0].fadedTo - 0.715f * 0.93f) < 1e-3 && bp[1].fadedTo == bp[1].to && bp[3].fadedTo == bp[3].from,
              "bar: kinds back to back (a separator only on a kind boundary), the faded part inside its kind (Wood 93 % faded, Corewood all, Ancient Bark none)");
        var tiny = PanelModel.BarParts(new[] { 0.999f, 0.001f }, null, 4f / 741f);
        Check(tiny[1].to - tiny[1].from >= 4f / 741f / (0.999f + 4f / 741f) - 1e-6 && tiny[0].to == tiny[1].from, "bar: the smallest kind keeps at least 4 px");
        Check(PanelModel.PanelScale(0.5f) == 0.8f && PanelModel.PanelScale(2f) == 1.3f && PanelModel.PanelScale(1f) == 1f && PanelModel.PanelScale(float.NaN) == 1f, "scale: Panel.Scale stays between 0.8 and 1.3");
        Check(PanelModel.Number(4180) == "4 180" && PanelModel.Number(1234567.4) == "1 234 567" && PanelModel.Number(999) == "999" && !PanelModel.Number(12345).Contains(","),
              "numbers: thousands apart by a no-break space, never a comma (Joost: 4\u00A0180 confuses European players)");
        var lp = PanelModel.LabelPositions(new[] { 0f, 600f }, new[] { 150f, 160f }, 741, 16);
        var crowd = PanelModel.LabelPositions(new[] { 0f, 690f, 720f }, new[] { 150f, 140f, 120f }, 741, 16);
        Check(lp[0] == 0 && lp[1] == 581 && crowd[2] + 120 <= 741 && crowd[1] + 140 + 16 <= crowd[2] + 0.01f && crowd[0] + 150 + 16 <= crowd[1] + 0.01f,
              "labels: under their segments, never over each other, never past the column (Voyages: helm over passenger)");

        // live settings: reload half a second after the last write of a burst, once
        float due = 0; var r1 = PanelModel.ConfigDue(ref due, true, 10f, 0.5f); var r2 = PanelModel.ConfigDue(ref due, true, 10.3f, 0.5f);
        var r3 = PanelModel.ConfigDue(ref due, false, 10.6f, 0.5f); var r4 = PanelModel.ConfigDue(ref due, false, 10.81f, 0.5f); var r5 = PanelModel.ConfigDue(ref due, false, 12f, 0.5f);
        Check(!r1 && !r2 && !r3 && r4 && !r5, "settings: a change in Gale reloads once, half a second after the last write");
        // Dev.SampleData toggled live both ways: on, nothing is saved; off again, the real totals save and no sample is in them
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hw-toggle-" + System.Guid.NewGuid().ToString("N"));
        var path = System.IO.Path.Combine(dir, "local", "1.json");
        var real = new Hearthwoven.LocalTotals(); var ev = new Hearthwoven.SessionEvents(); Hearthwoven.SessionEvents.Add(ev.PickedUp, "$item_wood", 7); real.Record("S1", null, ev);
        var keep = SampleMode.Check;
        SampleMode.Check = () => true; real.Save(path, new System.DateTime(2026, 10, 8, 20, 0, 0, System.DateTimeKind.Utc)); var whileOn = System.IO.File.Exists(path);
        SampleMode.Check = () => false; real.Save(path, new System.DateTime(2026, 10, 8, 20, 1, 0, System.DateTimeKind.Utc));
        var saved = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "";
        SampleMode.Check = keep;
        Check(!whileOn && saved.Contains("$item_wood") && !PanelSample.Players.Any(n => saved.Contains(n)) && !SampleMode.On,
              "settings: Dev.SampleData on pauses saving; off again, the real totals save with nothing of the sample in them");
        try { System.IO.Directory.Delete(dir, true); } catch { }
        var pj = PanelModel.ToJson(PanelModel.Build(input, new PanelState()));
        Check(PanelModel.PlayerPalette.Length == 8 && PanelModel.PlayerPalette.All(c => pj.Contains("\"" + c + "\"")), "palette: one table of eight player colours, also handed to the preview");
        var room = 760f - 72 - 180 - 40;
        Check(PanelModel.ListRows(4, room) == (40f, 8f) && PanelModel.ListRows(10, room).gap == 4 && 10 * PanelModel.ListRows(10, room).row + 9 * 4 + 4 <= room &&
              PanelModel.ListRows(12, room) == (32f, 3f) && 15 * PanelModel.ListRows(15, room).row + 14 * PanelModel.ListRows(15, room).gap + 4 <= room && PanelModel.ListRows(20, room) == (28f, 3f),
              "list: up to fifteen entries fit without scrolling (the gap closes first, then the rows shrink to 28 px; Skills has fourteen beside Overview), longer ones scroll");
        return fails;
    }
}
