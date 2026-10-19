// 0.9 armor-share (Joost, 18 Oct: "Yes, add friends' armour view (What hurt you / What your armor stopped on a friend's Defense page) to 0.9"):
// a friend's Defense gets the same switch, drawn by the same drawer, worded as their PC's record; All is their shared ledger, Session and the short
// windows their armor minutes cut at your session's start; their About box never says "Complete for you"; a copy from an older Hearthwoven says one
// plain line, never an empty or zero bar; Compare's armor line never shows on a friend's book.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FellowArmourTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var world = PanelSample.Full(now);
        var books = FullDump.Books(world).ToDictionary(b => b.who, b => b.book);
        var edda = books["Edda"];
        PanelState Defense(PanelInput book, TimeWindow w, string view = null, bool compare = false)
        {
            var s = new PanelState { Chapter = Chapter.Battle, Player = book.IsSelf ? "" : book.PlayerName, Window = w, WindowPicked = true, Compare = compare };
            s.Page[Chapter.Battle] = "defense"; if (view != null) s.View["Battle/defense/view"] = view;
            return s;
        }
        PanelView Build(PanelInput book, TimeWindow w, string view = null, bool compare = false) => PanelModel.Build(book, Defense(book, w, view, compare));
        double Num(string v) => PanelModel.ParseCount(v ?? "0");
        Block Types(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "armour");
        Block Bar(PanelView v) => Types(v)?.Items.FirstOrDefault(i => i.Kind == "armorbar");
        double Reached(PanelView v) { var p = Bar(v)?.Items[0]; return p == null ? -1 : Num(p.Value) + Num(p.Value2); }
        string Sources(PanelView v) { var b = PanelModel.Content(v).FirstOrDefault(x => x.Kind == "sources"); return b == null ? null : string.Join(";", b.Items.Select(i => i.Title + "=" + i.Value)); }
        // the texts of the armor view itself (the chosen view of the switch): the page's guard line keeps its own "0 blocks"
        IEnumerable<string> ViewText(PanelView v) { var sel = PanelModel.SwitchOf(v)?.Items.FirstOrDefault(i => i.Selected); var t = new PanelView(); t.Blocks.AddRange(sel?.Items ?? new List<Block>()); return PanelModel.AllText(t).Where(x => x != null); }
        IEnumerable<string> Text(PanelView v) => PanelModel.AllText(v).Where(t => t != null).Concat((v.AboutNumbers?.Items ?? new List<Block>()).SelectMany(l => new[] { l.Title, l.Text }));

        // ---------- the switch on a friend's book: their words, Received first and unchanged ----------
        var received = Build(edda, TimeWindow.Session);
        var sw = PanelModel.SwitchOf(received);
        var noShare = edda.ShallowCopy(); noShare.ArmourSince = null; noShare.ArmourFromUtc = null;
        Check(sw != null && sw.Items.Select(i => i.Title).SequenceEqual(new[] { PanelModel.FellowReceivedView, PanelModel.FellowArmourViewLabel }) && sw.Items[0].Selected &&
              Sources(received) != null && Sources(received) == Sources(Build(noShare, TimeWindow.Session)) && !Text(received).Any(t => t.Contains("your armor")),
              "FA switch: Edda's Defense has the view switch in her words (\"" + PanelModel.FellowReceivedView + "\" / \"" + PanelModel.FellowArmourViewLabel + "\"), Received first and chosen, Received from as without her armor");

        // ---------- the armor view: the same drawer, their PC's record, types only ----------
        var all = Build(edda, TimeWindow.SinceInstall, PanelModel.ArmourId);
        var types = Types(all);
        var fromLine = "Recorded from " + PanelModel.RecordDate(edda, edda.ArmourFromUtc.Value) + " · their PC";
        var rows = (types?.Items ?? new List<Block>()).Where(i => i.Kind == "armourrow").ToList();
        Check(types != null && types.Title == PanelModel.FellowArmourByType && types.Tone == PanelModel.FellowArmourKeyTone && types.Text == fromLine && Bar(all)?.Title == PanelModel.FellowArmourStopped &&
              Math.Abs(Reached(all) - Math.Round(ArmourTally.Total(edda.ArmourSince.Before))) <= 1 && rows.Count == edda.ArmourSince.Before.Count &&
              PanelModel.Content(all).Count(b => b.Kind == "armour") == 1 && !PanelModel.Content(all).Any(b => b.Kind == "sources") &&
              !Text(all).Any(t => t.Contains("your armor") || t.Contains("Your armor")),
              "FA All: her ledger since it began (" + Reached(all) + " reached her armor), the same armour drawer with the key in her words, \"" + fromLine + "\", a row per type, no By foe, no \"your armor\"");

        // About these numbers: her lines, undated like every box on her book, never "Complete for you"
        Check(all.AboutNumbers != null && all.AboutNumbers.Items.Select(l => l.Title).SequenceEqual(PanelModel.FellowArmourAbout.Select(l => l.title)) &&
              !Text(all).Any(t => t.Contains("Complete for you")) && !PanelModel.FellowArmourAbout.Any(l => l.text.Contains("your armor") || l.text.Contains("this PC")),
              "FA About: her view's box reads right for a friend (" + string.Join(", ", PanelModel.FellowArmourAbout.Select(l => l.title)) + "), no \"Complete for you\"");

        // ---------- the window cut: her minutes from your session's start, the short windows from now ----------
        double MinutesIn(PanelInput b, DateTime? cutoff, string stage) =>
            b.Log.Armor.Where(kv => kv.Key.Split('|')[1] == stage && (!cutoff.HasValue || DateTime.ParseExact(kv.Key.Split('|')[0], "yyyy-MM-dd'T'HH:mm'Z'", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal).AddMinutes(b.Log.Span) > cutoff.Value)).Sum(kv => (double)kv.Value);
        var session = Build(edda, TimeWindow.Session, PanelModel.ArmourId);
        var hour = Build(edda, TimeWindow.LastHour, PanelModel.ArmourId);
        double inSession = MinutesIn(edda, edda.ViewerSessionStartUtc, "b"), inHour = MinutesIn(edda, now.AddHours(-1), "b");
        var cutFromYours = edda.ViewerSessionStartUtc.HasValue && MinutesIn(edda, null, "b") > inSession;
        Check(inSession > 0 && Math.Abs(Reached(session) - Math.Round(inSession)) <= 1 && hour.ShownWindow == TimeWindow.LastHour && Math.Abs(Reached(hour) - Math.Round(inHour)) <= 1 &&
              Reached(all) >= Reached(session) && Types(session)?.Text == fromLine,
              "FA cut: her Session counts her armor minutes from your session's start (" + Reached(session) + (cutFromYours ? ", less than her whole log" : "") + "), 1 h from now (" + Reached(hour) + "), All her ledger (" + Reached(all) + ")");
        var torSession = Build(books["Tor"], TimeWindow.Session, PanelModel.ArmourId);
        var fellowSessionFrom = now.AddMinutes(-5);
        var late = edda.ShallowCopy(); late.ViewerSessionStartUtc = fellowSessionFrom;   // you came in five minutes ago: what she did before stays out
        var lateView = Build(late, TimeWindow.Session, PanelModel.ArmourId);
        double lateWant = MinutesIn(edda, fellowSessionFrom, "b");
        Check((lateWant <= 0 ? PanelModel.Content(lateView).Any(b => b.Kind == "empty") : Math.Abs(Reached(lateView) - Math.Round(lateWant)) <= 1) && lateWant < inSession && Types(torSession) != null,
              "FA cut: you came in five minutes ago, so her Session holds only her armor minutes since (" + lateWant + " of " + inSession + "); Tor's Session has his");

        // ---------- nothing on her armor in the window: said in her words, never a 0 ----------
        var quiet = edda.ShallowCopy(); quiet.Log = edda.Log.Between(DateTime.MinValue, DateTime.MaxValue); quiet.Log.Armor.Clear();
        var quietView = Build(quiet, TimeWindow.Session, PanelModel.ArmourId);
        var empty = PanelModel.Content(quietView).FirstOrDefault(b => b.Kind == "empty");
        Check(empty != null && empty.Title.StartsWith("Their armor has stopped nothing") && empty.Text.Contains("their PC") && Bar(quietView) == null && !ViewText(quietView).Any(t => t == "0"),
              "FA empty: a window with nothing on her armor says so in her words (\"" + empty?.Title + "\", \"" + empty?.Text + "\"), no bar, never 0");

        // ---------- an older Hearthwoven: one plain line, never an empty or zero bar ----------
        var old = SampleWorld.Share(SampleWorld.Make(now).owns[SampleWorld.Edda], now, "0.8.3");
        old.Fellows = edda.Fellows; old.ViewerName = edda.ViewerName; old.PlayerNames = edda.PlayerNames; old.ViewerSessionStartUtc = edda.ViewerSessionStartUtc;
        foreach (var w in new[] { TimeWindow.Session, TimeWindow.SinceInstall })
        {
            var v = Build(old, w, PanelModel.ArmourId);
            var olderSw = PanelModel.SwitchOf(v);
            var shown = olderSw?.Items.FirstOrDefault(i => i.Selected);
            Check(old.ArmourSince == null && !old.Log.ArmorKept && shown?.Id == PanelModel.ArmourId && shown.Items.Count == 1 && shown.Items[0].Kind == "note" && shown.Items[0].Text == PanelModel.FellowArmourOlder &&
                  !PanelModel.Content(v).Any(b => b.Kind == "armour" || b.Kind == "armorbar" || b.Kind == "empty") && !ViewText(v).Any(t => t == "0") && Sources(Build(old, w)) != null,
                  "FA older " + w + ": Edda on 0.8.3 shares no armor: the view says \"" + PanelModel.FellowArmourOlder + "\" once, no empty or zero bar; What hurt them stays");
        }

        // ---------- Compare: greyed on a friend's book, so its armor line never shows there; your own book keeps it ----------
        var friendCompare = Build(edda, TimeWindow.Session, PanelModel.ArmourId, compare: true);
        var ownCompare = PanelModel.Build(CompareTests.Scenario(now), new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.SevenDays, WindowPicked = true, Compare = true, Page = { [Chapter.Battle] = "defense" }, View = { ["Battle/defense/view"] = PanelModel.ArmourId } });
        Check(!PanelModel.AllText(friendCompare).Any(t => t != null && t.Contains(PanelModel.CompareArmourLine)) && Types(friendCompare) != null &&
              PanelModel.AllText(ownCompare).Any(t => t == PanelModel.CompareArmourLine),
              "FA Compare: on Edda's book Compare is greyed (her copy has no day history), so \"" + PanelModel.CompareArmourLine + "\" never shows there; your own Defense in 7 days keeps it");

        // ---------- a day window on a friend's book: their copy shares no armor days, the page stays as it was ----------
        var dayInput = edda.ShallowCopy(); var dayState = Defense(dayInput, TimeWindow.Today, PanelModel.ArmourId);
        var dayView = PanelModel.Build(dayInput, dayState);
        Check(dayView.ShownWindow != TimeWindow.Today || PanelModel.SwitchOf(dayView) == null,
              "FA days: a day window on her Defense never shows a made-up armor day (the page falls back to " + dayView.ShownWindow + ")");
        return fails;
    }
}
