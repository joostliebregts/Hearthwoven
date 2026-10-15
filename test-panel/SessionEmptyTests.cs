// 0.8.6 (Joost in game, test build 4: a friend's Battle > Damage in Session, nothing fought since Joost came in): the page dropped back to an old
// layout, the list's label as heading ("Damage", not "Damage dealt"), the scope line, the window chips on a row of their own, no head strip and no
// plate. A fellow who did not fight during your session (PanelModel.FellowOffThisSession) gets the page frame every other window has: the page's
// own heading, the plate with whose copy it is in its head strip (PageHead.cs: StripNote), the empty block inside it. Checked on each Battle page with windows, against the same page in
// 3 h on a copy with nothing in it.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class SessionEmptyTests
{
    public static readonly string[] Pages = { "overview", "damage", "defense", "deaths", "foes" };

    /// <summary>Edda's book, in the world and live, with nothing recorded since your session began (her latest record ten minutes before it).</summary>
    public static PanelInput Quiet(PanelInput edda, DateTime now)
    {
        var q = edda.ShallowCopy();
        q.OnThisSession = true; q.LastRecordedUtc = now.AddMinutes(-40); q.ViewerSessionStartUtc = now.AddMinutes(-30);
        return q;
    }

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var world = PanelSample.Full(now);
        var books = FullDump.Books(world).ToDictionary(b => b.who, b => b.book);
        var quiet = Quiet(books["Edda"], now);
        // the reference: the same live copy with nothing in it, in 3 h (the window Joost set beside it)
        var blank = books["Edda"].ShallowCopy(); blank.Log = new EventLog { Span = quiet.Log.Span }; blank.Session = new DamageTally(); blank.RecordedFromLog();
        PanelView Page(PanelInput b, string page, TimeWindow w)
        {
            var s = new PanelState { Chapter = Chapter.Battle, Player = "Edda", Window = w, WindowPicked = true };
            s.Page[Chapter.Battle] = page;
            return PanelModel.Build(b, s);
        }
        Check(PanelModel.FellowOffThisSession(quiet) && !PanelModel.FellowOffThisSession(blank), "SE setup: Edda is quiet in battle since you came in; the reference copy is not");
        foreach (var page in Pages)
        {
            var v = Page(quiet, page, TimeWindow.Session); var r = Page(blank, page, TimeWindow.LastThreeHours);
            var plate = PanelModel.PlateOf(v); var refPlate = PanelModel.PlateOf(r);
            var empty = plate?.Items?.FirstOrDefault(b => b.Kind == "empty");
            Check(refPlate != null && plate != null && v.Heading == r.Heading && plate.Title == refPlate.Title && plate.Icon == refPlate.Icon &&
                  !string.IsNullOrEmpty(r.StripNote) && (v.StripNote ?? "").StartsWith("Edda, ") && v.ShownWindow == TimeWindow.Session &&
                  empty != null && empty.Title == PanelModel.NothingYet + " this session" && empty.Text == "Edda has not fought " + PanelModel.SinceViewer(quiet) + ".",
                  "SE " + page + ": Edda's quiet Session has the frame of her 3 h: heading \"" + v.Heading + "\" (3 h \"" + r.Heading + "\"), plate " + (plate != null) +
                  ", the strip's \"" + v.StripNote + "\", the empty block in it: \"" + empty?.Title + " / " + empty?.Text + "\"");
        }
        // your own book with nothing fought this session: every page already had the frame (no early return there); it stays so
        var own = PanelSample.Full(now); own.Log = new EventLog();
        var bare = Pages.Where(p => PanelModel.PlateOf(PanelModel.Build(own, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Session, WindowPicked = true, Page = { [Chapter.Battle] = p } })) == null).ToList();
        Check(bare.Count == 0, "SE your own book: an empty Session keeps the plate on every Battle page with windows" + (bare.Count > 0 ? "; not on " + string.Join(", ", bare) : ""));
        return fails;
    }
}
