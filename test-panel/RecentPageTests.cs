// 0.7 Deeds > Recent (Chapters/RecentPage.cs): the page shows only what grew in the chosen window; "Not recorded" is never a 0; The group
// (0.8: the Everyone chip on) is you first, then by name, never a ranking; a fellow's short window is greyed with its one Why line.
// Fictional names (the sample world).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RecentPageTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Show(PanelInput i, TimeWindow w, string view = null, string player = null)
        {
            var s = new PanelState { Window = w, Page = { [Chapter.Deeds] = "recent" }, Player = player ?? "", Everyone = view == "group" };
            return PanelModel.Build(i, s);
        }
        List<Block> All(PanelView v) => PanelModel.Content(v).SelectMany(b => new[] { b }.Concat(PanelModel.IsBox(b) ? new List<Block>() : b.Items ?? new List<Block>())).ToList();   // every block once, and the rows of each
        bool NoZero(PanelView v) => All(v).All(b => b.Value != "0" && b.Value != "+0") && !PanelModel.AllText(v).Any(t => t == "0" || t == "+0");

        // ---------- only what grew in the window: wood 50 and 40 minutes ago, stone 20 minutes ago, a tree now ----------
        var ev = new SessionEvents(); var log = new DeedLog(); var at = now;
        log.Tick(at.AddMinutes(-50), ev, null);
        SessionEvents.Add(ev.PickedUp, "$item_wood", 10); log.Tick(at.AddMinutes(-40), ev, null);   // booked at -50
        SessionEvents.Add(ev.PickedUp, "$item_wood", 5); log.Tick(at.AddMinutes(-20), ev, null);    // booked at -40
        SessionEvents.Add(ev.PickedUp, "$item_stone", 2); log.Tick(at.AddMinutes(-5), ev, null);    // booked at -20
        SessionEvents.Add(ev.Felled, "Oak1", 1); log.Flush(at, ev, null);                           // the running minute
        var me = new PanelInput { PlayerName = "Rowan", IsSelf = true, NowUtc = at, SessionStartUtc = at.AddMinutes(-55), Deeds = log, Events = ev };
        string Sections(PanelView v) => string.Join(", ", All(v).Where(b => b.Kind == "section").Select(b => b.Title + " " + b.Value));
        var ten = Show(me, TimeWindow.LastTenMinutes); var half = Show(me, TimeWindow.LastThirtyMinutes); var session = Show(me, TimeWindow.Session);
        Check(Sections(ten) == "Trees felled +1" && Sections(half) == "Brought in +2, Trees felled +1" && Sections(session) == "Brought in +17, Trees felled +1" &&
              All(half).Where(b => b.Kind == "item").All(b => b.Title != "Wood") && ten.HeadingWindow == "last 10 minutes" && ten.Windows.Select(c => c.Label).SequenceEqual(new[] { "10 min", "30 min", "1 h", "3 h", "Session", "Today", "7 days", "30 days", "All" }),
              "Recent: each window shows only what grew inside it (10 min: the tree; 30 min: the stone too, no wood; Session: all of it), Battle's chips 10 min .. All (B33)");
        var quiet = new PanelInput { PlayerName = "Rowan", IsSelf = true, NowUtc = at.AddMinutes(30), SessionStartUtc = at.AddMinutes(-55), Deeds = log, Events = ev };
        var empty = Show(quiet, TimeWindow.LastTenMinutes);
        Check(All(empty).Count(b => b.Kind == "empty") == 1 && All(empty).Single(b => b.Kind == "empty").Title == "Nothing new in the last 10 minutes" && !All(empty).Any(b => b.Kind == "section"),
              "Recent: an empty window is one calm line, no empty sections");

        // ---------- the group: alphabetical; a fellow without live updates greyed in 10 min with the Why line, "Not recorded" never 0 ----------
        var world = PanelSample.Full(now);
        world.Fellows = world.Fellows.OrderByDescending(f => f.PlayerName, StringComparer.Ordinal).ToList();   // the order the group arrives in must not matter
        var g = Show(world, TimeWindow.Session, "group"); var g10 = Show(world, TimeWindow.LastTenMinutes, "group");
        var rows = All(g).Single(b => b.Kind == "people").Items; var rows10 = All(g10).Single(b => b.Kind == "people").Items;
        var why = PanelModel.RecentOfFellow(world.Fellows.Single(f => f.PlayerName == "Tor"), TimeWindow.LastTenMinutes).Why;
        var you = Show(world, TimeWindow.Session);
        Check(rows.Select(r => r.Title).SequenceEqual(new[] { "You", "Edda", "Finch", "Tor" }) && rows.All(r => r.Items.Any(i => i.Value != null && i.Value.StartsWith("+"))) &&
              g.EveryoneOn && PanelModel.SwitchOf(g) == null && !you.EveryoneOn && PanelModel.SwitchOf(you) == null && !All(you).Any(b => b.Kind == "people") && All(you).Any(b => b.Kind == "section") &&
              PanelModel.EveryoneOff(Chapter.Deeds, "recent", false) == null,
              "Recent, the group (0.8): the Everyone chip turns Recent into the group (no switch of its own; the chip works here), one row per player who shares, you first, then by name (never a ranking), each with their gains this session; off: your own gains");
        // each player: every family that grew shows once before a second kind of any family, at most six gains (two rows of three), "+N more" for the rest
        var parts = PanelModel.RecentParts(world, PanelModel.RecentOf(world, TimeWindow.Session));
        var grew = parts.Values.Sum(d => d.Count);
        var mine = rows[0];
        Check(rows.All(r => r.Items.Count <= PanelModel.GroupTop) && mine.Items.Count == Math.Min(grew, PanelModel.GroupTop) &&
              parts.Values.Where(d => d.Count > 0).Take(PanelModel.GroupTop).All(d => mine.Items.Any(i => i.Id == d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key)) &&
              mine.Note == (grew > PanelModel.GroupTop ? PanelModel.RecentMore(grew - PanelModel.GroupTop) : null),
              "Recent, the group: each player up to " + PanelModel.GroupTop + " gains, every family that grew first (" + string.Join(", ", mine.Items.Select(i => i.Value + " " + i.Title)) + "), then the next largest; " + (mine.Note ?? "nothing more") + " for the rest");
        var tor10 = rows10.Single(r => r.Title == "Tor");
        Check(tor10.Items.Count == 1 && tor10.Items[0].Unrecorded && tor10.Items[0].Value == null && tor10.Items[0].Title == PanelModel.NotRecorded &&
              rows10.Where(r => r.Title != "Tor").All(r => r.Items.All(i => !i.Unrecorded)) && why == PanelModel.FellowNotTimed(world.Fellows.Single(f => f.PlayerName == "Tor")) &&
              All(g10).Count(b => b.Kind == "note" && b.Text == why) == 1 && NoZero(g10) && NoZero(g) &&
              All(g10).FindIndex(b => b.Kind == "note" && b.Text == why) > All(g10).FindIndex(b => b.Kind == "people"),
              "Recent, the group in 10 min: fellows with live updates show their last 10 minutes; Tor (full copies only) says Not recorded (muted, never 0) with the Why line once, under the rows (no loose text above the numbers)");
        // 0.8.1 review 5: a group of three (the fellows' note follows the players): the third player's island is a half like the others, never the plate's width
        var three = PanelSample.Full(now); three.Fellows.RemoveAll(f => f.PlayerName == "Finch");
        var isles = PanelModel.Islands(PanelModel.PlateOf(Show(three, TimeWindow.Session, "group"))).Where(i => i.Parts.Any(p => p.Only >= 0)).ToList();
        Check(isles.Count == 3 && isles.All(i => i.Span == PanelModel.IslandHalf),
              "Recent, the group of three: each player's island is a half, the third too (" + string.Join(", ", isles.Select(i => i.Span)) + ")");

        // ---------- a fellow's own book: Session and All; their days and (no live updates) the short windows greyed; never 0 ----------
        var tor = world.Fellows.Single(f => f.PlayerName == "Tor");
        tor.Fellows = world.Fellows.Where(f => f != tor).Concat(new[] { world }).ToList();
        var torBook = Show(tor, TimeWindow.LastTenMinutes, null, "Tor");
        var open = torBook.Windows.Where(c => !c.Disabled).Select(c => c.Id).ToList();
        Check(open.SequenceEqual(new[] { TimeWindow.Session.ToString(), TimeWindow.SinceInstall.ToString() }) && torBook.Windows.Single(c => c.Selected).Id == TimeWindow.Session.ToString() &&
              torBook.Scope == "Tor, " + PanelModel.SinceViewer(tor) && NoZero(torBook) && All(torBook).Any(b => b.Kind == "section"),
              "Recent on a fellow's book: Session (since your session began) and All; their days and the short windows (no live updates) greyed; never 0");

        // ---------- 7 days: the kills have no foes per day: their count stands as one entry with its line, never a 0 per foe ----------
        var week = Show(world, TimeWindow.SevenDays);
        var foes = All(week).FirstOrDefault(b => b.Kind == "section" && b.Title == "Foes defeated");
        var foeStrip = foes == null ? null : PanelModel.Content(week).SkipWhile(b => b != foes).Skip(1).FirstOrDefault();
        Check(foes != null && foeStrip?.Items?.Count == 1 && foeStrip.Note == PanelModel.RecentFoesNoDays && NoZero(week),
              "Recent, 7 days: foes defeated is the game's kill count with one line why it has no foes per day; nothing reads 0");
        return fails;
    }
}
