// Battle > Defence's armour switch (0.7, Chapters/ArmourModel.cs): a visible switch Received (default, the page as it was) / Your armour on
// your own book; Received from never changes because armour data is there; the Your armour view's numbers (before and after armour) are both
// the ledger's, with its own "Recorded from" date, its About box in every window, and an empty window says so instead of 0; a fellow's book has no switch.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class ArmourPanelTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelState Defence(TimeWindow w, string view = null) { var s = new PanelState { Chapter = Chapter.Battle, Window = w }; s.Page[Chapter.Battle] = "defense"; if (view != null) s.View["Battle/defense/view"] = view; return s; }
        string Sources(PanelView v) { var b = PanelModel.Content(v).FirstOrDefault(x => x.Kind == "sources"); return b == null ? null : string.Join(";", b.Items.Select(i => i.Title + "=" + i.Value + ":" + string.Join(",", i.Items.Select(p => p.Id + p.Value)))); }

        var world = PanelSample.Full(now);
        var bare = world.ShallowCopy(); bare.ArmourSession = bare.ArmourSince = bare.ArmourPending = null; bare.ArmourMinutes = null; bare.ArmourBook = null; bare.ArmourFromUtc = null;
        bare.IsSelf = false;   // a fellow's book: no armour numbers are shared

        // the switch: visible on your own book, Received first and chosen, and Received from exactly as without armour data
        foreach (var w in new[] { TimeWindow.Session, TimeWindow.LastHour, TimeWindow.SevenDays, TimeWindow.SinceInstall })
        {
            var mine = PanelModel.Build(world, Defence(w));
            var sw = PanelModel.SwitchOf(mine);
            var plain = world.ShallowCopy(); plain.ArmourSession = plain.ArmourSince = plain.ArmourPending = null; plain.ArmourMinutes = null; plain.ArmourBook = null;
            Check(sw != null && string.IsNullOrEmpty(sw.Title) && sw.Items.Select(i => i.Title).SequenceEqual(new[] { PanelModel.ReceivedView, PanelModel.ArmourViewLabel }) &&
                  sw.Items[0].Selected && Sources(mine) != null && Sources(mine) == Sources(PanelModel.Build(plain, Defence(w))) && mine.Keys.Any(k => k.EndsWith("] View")),
                  "armour " + w + ": a visible switch Received (default) / Your armour, no caption over the chips (they say it), its key the View key; Received from unchanged by armour data");
        }

        // Your armour: the ledger's numbers, one bar per type on one scale, per foe from This session on, the recorded-from date
        var before = PanelModel.Build(world, Defence(TimeWindow.Session, PanelModel.ArmourId));
        var head = PanelModel.Content(before).FirstOrDefault(b => b.Kind == "stat" && b.Title == PanelModel.ArmourStopped);
        var types = PanelModel.Content(before).FirstOrDefault(b => b.Kind == "armour" && b.Title == PanelModel.ArmourByType);
        var foes = PanelModel.Content(before).FirstOrDefault(b => b.Kind == "armour" && b.Title == PanelModel.ArmourByFoe);
        var bt = ArmourTally.Total(world.ArmourSession.Before); var at = ArmourTally.Total(world.ArmourSession.After);
        var fromLine = PanelModel.ArmourFromLine(world);
        double Num(string v) => double.Parse(new string((v ?? "0").Where(char.IsDigit).ToArray()).PadLeft(1, '0'));
        // REVIEW-07 #3: every "X of Y" on the view means the same as the hero: stopped of what reached your armour (rows add up to the hero)
        Check(types != null && head != null && Math.Abs(types.Items.Sum(r => Num(r.Value)) - Num(head.Value)) <= types.Items.Count && types.Items.All(r => Num(r.Value) <= Num(r.Value2)),
              "armour: each type's \"X of Y\" is what your armour stopped of what hit it, as the hero: " + string.Join(", ", (types?.Items ?? new List<Block>()).Select(r => r.Title + " " + r.Value + " of " + r.Value2)));
        Check(head != null && head.Value == PanelModel.Number(bt - at) && head.Text.StartsWith("of " + PanelModel.Number(bt) + " ") &&
              types != null && types.Text == fromLine && fromLine.StartsWith("Recorded from ") && fromLine.EndsWith(" · this PC") && types.Items.Max(r => r.Fraction2) == 1f &&
              types.Items.All(r => r.Fraction <= r.Fraction2) && foes != null && foes.Items.Count > 0 && PanelModel.Content(before).All(b => b.Kind != "sources"),
              "armour: Your armour shows what the armour stopped of what reached it, bars per type on one scale (reached inside before), per foe, \"" + fromLine + "\"");
        var ten = PanelModel.Build(world, Defence(TimeWindow.LastTenMinutes, PanelModel.ArmourId));
        Check(PanelModel.Content(ten).All(b => !(b.Kind == "armour" && b.Title == PanelModel.ArmourByFoe)) && PanelModel.Content(ten).Any(b => b.Kind == "note" && b.Text == PanelModel.ArmourPerFoe),
              "armour: the short windows have no foe (the minute buckets keep none) and say where it is");

        // review fix (a like-for-like pair): Your armour's before and after are both the ledger's (stopped = before - after of the same hits), it carries
        // no "can differ a little" line, and its About these numbers (the ArmourAbout lines, with the older-version gap) is there in every window
        foreach (var w in new[] { TimeWindow.LastTenMinutes, TimeWindow.Session, TimeWindow.SevenDays, TimeWindow.SinceInstall })
        {
            var v = PanelModel.Build(world, Defence(w, PanelModel.ArmourId));
            var rec = PanelModel.Build(world, Defence(w));
            Check(v.AboutNumbers != null && v.AboutNumbers.Items.Skip(1).Select(l => l.Title).SequenceEqual(PanelModel.ArmourAbout.Skip(1).Select(l => l.title)) &&
                  PanelModel.ArmourAbout.Any(l => l.text.Contains("older Hearthwoven")) && !PanelModel.AllText(v).Any(t => t != null && t.Contains("differ a little")) &&
                  (rec.AboutNumbers == null || rec.AboutNumbers.Items.All(l => !PanelModel.ArmourAbout.Any(a => a.text == l.Text))),
                  "armour " + w + ": Your armour has its own About these numbers (incl. the older-version gap), no \"can differ a little\"; Received keeps the page's own");
        }

        // nothing on your armour in a window: said with the date, never a 0
        var none = world.ShallowCopy(); none.ArmourSession = new ArmourTally();
        var empty = PanelModel.Content(PanelModel.Build(none, Defence(TimeWindow.Session, PanelModel.ArmourId))).FirstOrDefault(b => b.Kind == "empty");
        // B27 (Joost: the empty view was unclear right after installing): it says why (counted from its own date) and when it fills
        var counted = "Counted from " + PanelModel.RecordDate(none, none.ArmourFromUtc ?? none.SessionStartUtc ?? none.NowUtc);
        Check(empty != null && empty.Title == "Your armor has stopped nothing this session" && empty.Text.StartsWith(counted) && empty.Text.EndsWith("A foe's hit on your armor shows up here.") &&
              !PanelModel.AllText(PanelModel.Build(none, Defence(TimeWindow.Session, PanelModel.ArmourId))).Any(t => t == "0" || (t != null && t.Contains("\u2014"))),
              "armour (B27): an empty window says what it is and why (\"" + empty?.Title + "\", \"" + empty?.Text + "\"), never 0");

        // a fellow's book: no switch, Received from as before
        var fellow = PanelModel.Build(bare, Defence(TimeWindow.Session));
        Check(PanelModel.Content(fellow).All(b => b.Kind != "switch" && b.Kind != "armour") && Sources(fellow) != null,
              "armour: a fellow's book has no Your armour (they share no armour numbers); its Received from stays");
        return fails;
    }
}
