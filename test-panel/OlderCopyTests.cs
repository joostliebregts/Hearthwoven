// 0.8.2 (I; Joost's friend on Hearthwoven 0.6.5, whose Battle pages showed parts empty without a reason): a fellow's copy from an older
// Hearthwoven makes each page that misses something for that reason say it once, in one form ("Edda's Hearthwoven is 0.6.5: ... once they
// update."), and only there: a copy from this build says it nowhere. The copies are the sample world's as 0.6.5 and 0.8.1 send them
// (SampleWorld.OlderJson). A friend in the world whose battle record is quiet since you came in "has not fought", not "has not played".
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class OlderCopyTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var (owns, copies) = SampleWorld.Make(now);
        var t = copies[SampleWorld.Edda];
        PanelInput Book(string version)
        {
            var c = version == null ? t : SampleWorld.Share(owns[SampleWorld.Edda], now, version);
            c.Fellows = owns.Where(kv => kv.Key != SampleWorld.Edda).Select(kv => kv.Key == SampleWorld.Rowan ? kv.Value : copies[kv.Key]).ToList();
            c.Trail = t.Trail; c.ServerLive = true; c.Timed = version != "0.6.5" && t.Timed;   // 0.6.5 sends no live updates
            c.OnThisSession = t.OnThisSession; c.ViewerSessionStartUtc = t.ViewerSessionStartUtc; c.ReceivedUtc = t.ReceivedUtc; c.NowUtc = now;
            return c;
        }
        PanelView Page(PanelInput b, Chapter ch, string page, TimeWindow w = TimeWindow.Session, string view = null)
        {
            var s = new PanelState { Chapter = ch, Player = "Edda", Window = w, WindowPicked = true, Page = { [ch] = page } };
            if (view != null) s.View[ch + "/" + page + "/view"] = view;
            return PanelModel.Build(b, s);
        }
        List<string> Lines(PanelView v) => PanelModel.Content(v).SelectMany(b => new[] { b.Title, b.Text, b.Note }).Where(x => x != null && x.Contains("'s Hearthwoven is")).Distinct().ToList();
        var now82 = Book(null); var v065 = Book("0.6.5"); var v081 = Book("0.8.1");

        // ---------- this build's copy: no page says it ----------
        var said = new List<string>();
        foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
            foreach (var l in PanelModel.Build(now82, new PanelState { Chapter = ch, Player = "Edda" }).List)
                foreach (var w in new[] { TimeWindow.Session, TimeWindow.SinceInstall })
                {
                    var v = Page(now82, ch, l.Id, w);
                    said.AddRange(Lines(v).Concat(new[] { v.WindowWhy }.Where(x => x != null && x.Contains("'s Hearthwoven is"))).Select(x => ch + "/" + l.Id + ": " + x));
                }
        Check(now82.ModVersion == Plugin.Version && said.Count == 0, "OC this build: a copy from " + now82.ModVersion + " says no version line on any page" + (said.Count > 0 ? ": " + string.Join("; ", said.Take(3)) : ""));

        // ---------- 0.6.5: each page that misses something says what, once ----------
        // the page says it once, naming the version and what fills in (the sentence around them is PanelModel.OlderLine's)
        bool One(PanelView v, string ver, params string[] what) => Lines(v) is var ls && ls.Count == 1 && ls[0].Contains(ver) && what.All(ls[0].Contains);
        var feed = Page(v065, Chapter.Battle, PanelModel.FeedPage); var last = Page(v065, Chapter.Battle, PanelModel.LastFightPage);
        var defense = Page(v065, Chapter.Battle, "defense"); var byFoe = Page(v065, Chapter.Battle, "damage", view: "foe");
        var quiet = new[] { "overview", "foes", "deaths" }.ToDictionary(p => p, p => Lines(Page(v065, Chapter.Battle, p)));
        Check(One(feed, "0.6.5", "page") && One(last, "0.6.5", "page") && One(defense, "0.6.5", "blocks and parries", "×N") && One(byFoe, "0.6.5", "×N") && !Lines(byFoe)[0].Contains("blocks") &&
              quiet.Values.All(x => x.Count == 0) && Says(defense, "0.6.5", "10 min to 3 h") && Says(defense, "not shared"),
              "OC 0.6.5: Feed and Last fight \"" + Lines(feed).FirstOrDefault() + "\"; Defense once \"" + Lines(defense).FirstOrDefault() + "\"; By foe \"" + Lines(byFoe).FirstOrDefault() +
              "\"; the chips' reason names 0.6.5 for 10 min to 3 h and days not shared; Overview, Foes and Deaths need nothing newer and say nothing");

        // ---------- 0.8.1: only the feed and the block minutes wait ----------
        var feed81 = Page(v081, Chapter.Battle, PanelModel.FeedPage); var defense81 = Page(v081, Chapter.Battle, "defense"); var byFoe81 = Page(v081, Chapter.Battle, "damage", view: "foe");
        Check(One(feed81, "0.8.1", "page") && One(defense81, "0.8.1", "blocks and parries") && !Lines(defense81)[0].Contains("×N") && Lines(byFoe81).Count == 0,
              "OC 0.8.1: the feed and the block minutes wait (\"" + Lines(defense81).FirstOrDefault() + "\"); the ×N counts are there");

        // ---------- a friend in the world, quiet in battle since you came in: "has not fought", not "has not played" ----------
        var keep = (now82.ViewerSessionStartUtc, now82.OnThisSession);
        try
        {
            now82.ViewerSessionStartUtc = now.AddMinutes(-1); now82.OnThisSession = true;
            var playing = PanelModel.Content(Page(now82, Chapter.Battle, "foes")).FirstOrDefault(b => b.Kind == "empty");
            now82.OnThisSession = false;
            var away = PanelModel.Content(Page(now82, Chapter.Battle, "foes")).FirstOrDefault(b => b.Kind == "empty");
            Check(playing?.Text == "Edda has not fought " + PanelModel.SinceViewer(now82) + "." && away?.Text == PanelModel.FellowNotOn(now82),
                  "OC Foes in Session: Edda in the world but quiet in battle since you came in says \"" + playing?.Text + "\"; not in the world \"" + away?.Text + "\"");
        }
        finally { now82.ViewerSessionStartUtc = keep.Item1; now82.OnThisSession = keep.Item2; }
        return fails;
    }
}
