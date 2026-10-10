// The battle record as the Battle pages will read it (0.8, Chapters/BattleRecModel.cs), on the sample world: in every window a foe kind
// whose damage shows has a count, and its count splits into defeated, got away and still fighting; the day windows and All come from the
// foes kept on this PC; a fellow's book has their shared Session and All only, and no feed.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class BattleRecPanelTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var world = PanelSample.Full(now);

        // your own book, every window: a kind with damage dealt has a count, and the count is defeated + got away + still fighting
        var bad = new List<string>();
        foreach (var w in PanelModel.AllWindows)
        {
            var kinds = PanelModel.FoeKinds(world, w);
            foreach (var k in kinds.Where(k => k.DealtTotal > 0 && k.Counted))
                if (k.Count < 1 || (w == TimeWindow.Session && k.Defeated + k.GotAway + k.Fighting != k.Count)) bad.Add(w + " " + k.Kind + " x" + k.Count + " (" + k.Defeated + "/" + k.GotAway + "/" + k.Fighting + ")");
        }
        var session = PanelModel.FoeKinds(world, TimeWindow.Session);
        var week = PanelModel.FoeCountsIn(world, TimeWindow.SevenDays);
        Check(bad.Count == 0 && session.Count(k => k.Count > 0) >= 5 && session.Sum(k => k.Defeated) > 0 && session.Sum(k => k.GotAway) > 0 && session.Sum(k => k.DefeatedWith) > 0 &&
              week != null && week.Total(c => c.Fought) == PanelModel.FoeCountsIn(world, TimeWindow.Session).Total(c => c.Fought) && PanelModel.FoesFrom(world).HasValue,
              "BR your book: in every window a foe kind with damage dealt has its x N, and in Session it splits into defeated, got away and still fighting (" + session.Count(k => k.Count > 0) +
              " kinds, " + session.Sum(k => k.Count) + " foes); 7 days (the book's rows) holds the same foes as the session here" + (bad.Count > 0 ? ": " + string.Join("; ", bad.Take(4)) : ""));

        // the feed: fights newest first, foes newest first, the book's outcome words only
        var feed = PanelModel.BattleFeed(world);
        var words = feed?.SelectMany(f => f.Entries).Select(e => e.OutcomeText).Distinct().ToList() ?? new List<string>();
        Check(feed != null && feed.Count >= 2 && feed.Zip(feed.Skip(1), (a, b) => a.StartUtc >= b.EndUtc).All(x => x) &&
              feed.All(f => f.Entries.Zip(f.Entries.Skip(1), (a, b) => a.EndUtc >= b.EndUtc).All(x => x)) && words.Contains("defeated by you") && words.Any(x => x.StartsWith("defeated with ")) && words.Contains("survived"),
              "BR feed: " + feed?.Count + " fights newest first, each foe newest first, in the book's words (" + string.Join(", ", words) + ")");

        // a fellow's book: their shared Session and All as they sent them, no short or day windows, no feed
        var edda = world.Fellows.First(f => f.PlayerName == "Edda");
        var eddaSession = PanelModel.FoeCountsIn(edda, TimeWindow.Session);
        Check(!edda.IsSelf && eddaSession != null && eddaSession.Total(c => c.Fought) > 0 && PanelModel.FoeCountsIn(edda, TimeWindow.SinceInstall) != null &&
              PanelModel.FoeCountsIn(edda, TimeWindow.LastHour) == null && PanelModel.FoeCountsIn(edda, TimeWindow.SevenDays) == null && PanelModel.BattleFeed(edda) == null &&
              PanelModel.FoeKinds(edda, TimeWindow.LastHour).All(k => !k.Counted),
              "BR a fellow's book: their Session and All counts as shared, the short and day windows without a count (no x 0), no feed");
        return fails;
    }
}
