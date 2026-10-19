// 0.9.1 (Joost in game, Deeds > Recent with Everyone on: "I want to know what happened yesterday ... who was on yesterday", "and maybe also last three
// days"; his friends' rows on 7 days said "Not recorded" although their copies carry their days): the Yesterday and 3 days windows, and a friend's days
// on Recent from what their copy shares per day. Day edges at local midnight, the window order, what a friend's day says (shared, nothing that day, an
// older Hearthwoven, a copy from before the window), the foes defeated per day (bounds, round trip, the mark, the bytes). Fictional names (the sample world).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class YesterdayTests
{
    /// <summary>
    /// The preview scenario (deeds-recent-group-yesterday and -3days) and the tests' group: the sample world with Tor's copy from Hearthwoven 0.8.3 (it
    /// shares no days) and Finch's copy holding no day for yesterday (he did nothing that day; his other days stay). Edda shares her days as before.
    /// </summary>
    public static PanelInput GroupDays(DateTime now)
    {
        var w = PanelSample.Full(now);
        var yesterday = DayHistory.DayKey(PanelModel.LocalToday(w).AddDays(-1));
        foreach (var f in w.Fellows)
        {
            if (f.PlayerName == SampleWorld.Tor) { f.ModVersion = "0.8.3"; f.DayDeeds = null; f.DayVoyages = false; f.DayKills = false; f.DayWeapons = null; }
            if (f.PlayerName == SampleWorld.Finch) { f.DayDeeds?.Remove(yesterday); f.DayDealt?.Remove(yesterday); f.DealtByDay?.Remove(yesterday); f.DayWeapons?.Remove(yesterday); }
        }
        return w;
    }

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        DateTime Utc(int d, int h, int m = 0, int s = 0) => new DateTime(2026, 10, d, h, m, s, DateTimeKind.Utc);

        // ---------- the windows: Yesterday and 3 days between Today and 7 days, everywhere the day windows are ----------
        var shorts = PanelModel.AllWindows.Select(PanelModel.WindowShort).ToList();
        Check(shorts.SequenceEqual(new[] { "10 min", "30 min", "1 h", "3 h", "Session", "Today", "Yesterday", "3 days", "7 days", "30 days", "All" }) &&
              PanelModel.DayWindows.SequenceEqual(new[] { TimeWindow.Today, TimeWindow.Yesterday, TimeWindow.ThreeDays, TimeWindow.SevenDays, TimeWindow.ThirtyDays }) &&
              PanelModel.WindowsOf(Chapter.Battle, "damage").SequenceEqual(PanelModel.AllWindows) && PanelModel.RecentWindows.SequenceEqual(PanelModel.AllWindows) &&
              PanelModel.WindowsOf(Chapter.Voyages, "sailing").SequenceEqual(PanelModel.DayAndAll) && PanelModel.DayAndAll.Take(5).SequenceEqual(PanelModel.DayWindows) &&
              PanelModel.TogetherWindows.SequenceEqual(PanelModel.AllWindows) && PanelModel.IsDayWindow(TimeWindow.Yesterday) && PanelModel.IsDayWindow(TimeWindow.ThreeDays) &&
              !PanelModel.IsShortWindow(TimeWindow.Yesterday) && PanelModel.DaysOf(TimeWindow.Yesterday) == 1 && PanelModel.DaysOf(TimeWindow.ThreeDays) == 3,
              "Y order: the chips read " + string.Join(", ", shorts) + " on Battle, Recent, the Deeds and Voyages pages and Together; Yesterday and 3 days are day windows (1 and 3 days)");

        // ---------- the edges: local midnight in the reader's own clock ----------
        // a reader at UTC+2 at 00:30 on Thursday 8 Oct (22:30 UTC on the 7th): Today is the 8th, Yesterday the 7th midnight to midnight, 3 days the 6th to the 8th
        PanelInput At(DateTime utc) => new PanelInput { IsSelf = true, NowUtc = utc, ToLocal = t => t.AddHours(2) };
        var r = At(Utc(7, 22, 30));
        var d7 = new DateTime(2026, 10, 7); var d8 = new DateTime(2026, 10, 8); var d6 = new DateTime(2026, 10, 6);
        Check(PanelModel.DayFrom(r, TimeWindow.Yesterday) == d7 && PanelModel.DayTo(r, TimeWindow.Yesterday) == d7 && PanelModel.DayFrom(r, TimeWindow.Today) == d8 && PanelModel.DayTo(r, TimeWindow.Today) == d8 &&
              PanelModel.DayFrom(r, TimeWindow.ThreeDays) == d6 && PanelModel.DayTo(r, TimeWindow.ThreeDays) == d8 && PanelModel.DayFrom(r, TimeWindow.SevenDays) == d8.AddDays(-6) &&
              PanelModel.WindowStartUtc(r, TimeWindow.Yesterday) == Utc(6, 22) && PanelModel.WindowEndUtc(r, TimeWindow.Yesterday) == Utc(7, 22) &&
              PanelModel.WindowEndUtc(r, TimeWindow.Today) == null && PanelModel.WindowEndUtc(r, TimeWindow.ThreeDays) == null &&
              PanelModel.BeforeEnd(r, TimeWindow.Yesterday, Utc(7, 21, 59, 59)) && !PanelModel.BeforeEnd(r, TimeWindow.Yesterday, Utc(7, 22)) && PanelModel.BeforeEnd(r, TimeWindow.Today, Utc(7, 22, 29)) &&
              PanelModel.YesterdayWords(r) == "yesterday, Wednesday 7 Oct" && PanelModel.HoldsToday(TimeWindow.ThreeDays) && !PanelModel.HoldsToday(TimeWindow.Yesterday),
              "Y edges: at 00:30 local on 8 Oct, Yesterday is 7 Oct from 22:00 UTC on the 6th to 22:00 UTC on the 7th (a second before midnight is in, midnight is not); 3 days is 6 to 8 Oct; the heading says \"" + PanelModel.YesterdayWords(r) + "\"");
        var before = At(Utc(7, 21, 59, 59)); var after = At(Utc(7, 22, 0, 0));
        Check(PanelModel.DayFrom(before, TimeWindow.Yesterday) == d6 && PanelModel.DayFrom(after, TimeWindow.Yesterday) == d7 && PanelModel.LocalToday(before) == d7 && PanelModel.LocalToday(after) == d8,
              "Y midnight: one second before local midnight Yesterday is still the 6th; at midnight it turns to the 7th");

        // ---------- your own Yesterday: that day's row of your history, never the running session's unsaved part ----------
        var (owns, _) = SampleWorld.Make(now);
        var me = owns[SampleWorld.Rowan];
        var today = PanelModel.LocalToday(me); var yday = today.AddDays(-1);
        double Picked(PanelInput c) => c?.Events?.PickedUp.Values.Sum(v => (double)v) ?? -1;
        var pend = me.ShallowCopy(); pend.Pending = new DayHistory.Row { Period = DayHistory.DayKey(today) }; SessionEvents.Add(pend.Pending.Events.PickedUp, "$item_wood", 7);
        var yIn = PanelModel.InWindow(pend, TimeWindow.Yesterday); var tIn = PanelModel.InWindow(pend, TimeWindow.Today); var threeIn = PanelModel.InWindow(pend, TimeWindow.ThreeDays);
        double RowPicked(DateTime a, DateTime b) => me.History.Sum(a, b).Events.PickedUp.Values.Sum(v => (double)v);
        Check(yIn != null && yIn.Window.From == yday && yIn.Window.To == yday && Picked(yIn) == RowPicked(yday, yday) && Picked(yIn) > 0 &&
              Picked(tIn) == RowPicked(today, today) + 7 && Picked(threeIn) == RowPicked(today.AddDays(-2), today) + 7 && threeIn.Window.From == today.AddDays(-2),
              "Y own: Yesterday reads that day's row (" + Picked(yIn) + " brought in), without the running session's 7 wood; Today and 3 days hold it (3 days = the last three rows + 7)");
        var young = me.ShallowCopy(); young.History = new DayHistory { From = today };
        Check(!PanelModel.DayOpen(young, TimeWindow.Yesterday) && PanelModel.DayOpensOn(young, TimeWindow.Yesterday) == today.AddDays(1) && PanelModel.DayOpensOn(young, TimeWindow.ThreeDays) == today.AddDays(2) &&
              PanelModel.DayOpen(young, TimeWindow.Today) && (PanelModel.WaitLine(young, TimeWindow.Yesterday) ?? "").StartsWith("Yesterday works from "),
              "Y greyed: a history that began today greys Yesterday until tomorrow and 3 days until the day after, with its line (\"" + PanelModel.WaitLine(young, TimeWindow.Yesterday) + "\")");

        // ---------- Recent with Everyone on, Yesterday: who was on and what they did; nothing that day; an older Hearthwoven; never "Not recorded" ----------
        var group = GroupDays(now);
        PanelView Recent(PanelInput i, TimeWindow w, bool ev = true, string player = "") => PanelModel.Build(i, new PanelState { Window = w, Everyone = ev, Player = player, Page = { [Chapter.Deeds] = PanelModel.RecentPageId } });
        List<Block> Rows(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "people")?.Items ?? new List<Block>();
        string RowText(Block row) => string.Join(" ", row.Items.Select(i => (i.Value ?? "") + " " + i.Title));
        var yv = Recent(group, TimeWindow.Yesterday);
        var rows = Rows(yv); var texts = PanelModel.AllText(yv).ToList();
        Block Row(string n) => rows.FirstOrDefault(x => x.Id == n);
        var edda = group.Fellows.First(f => f.PlayerName == SampleWorld.Edda); var eddaParts = PanelModel.RecentParts(edda, PanelModel.RecentOf(edda, TimeWindow.Yesterday, group));
        var eddaDay = edda.DayDeeds.TryGetValue(DayHistory.DayKey(yday), out var ed) ? ed : null;
        double Fam(Dictionary<string, Dictionary<string, float>> d, params string[] fams) => d == null ? 0 : fams.Sum(f => d.TryGetValue(f, out var p) ? p.Values.Sum(v => (double)v) : 0);
        var torLine = PanelModel.OlderLine(group.Fellows.First(f => f.PlayerName == SampleWorld.Tor), "their days fill in");
        Check(yv.ShownWindow == TimeWindow.Yesterday && yv.EveryoneOn && yv.HeadingWindow == "yesterday, Wednesday 7 Oct" &&
              rows.Select(x => x.Title).SequenceEqual(new[] { "You", SampleWorld.Edda, SampleWorld.Finch, SampleWorld.Tor }) &&
              Row(SampleWorld.Rowan).Items.Any(i => (i.Value ?? "").StartsWith("+")) && Row(SampleWorld.Edda).Items.Any(i => (i.Value ?? "").StartsWith("+")) &&
              eddaDay != null && eddaParts["broughtIn"].Values.Sum() == Fam(eddaDay, PanelModel.DdWood, PanelModel.DdOre) &&
              RowText(Row(SampleWorld.Finch)).Trim() == "nothing that day" && Row(SampleWorld.Finch).Items.All(i => !i.Unrecorded) &&
              RowText(Row(SampleWorld.Tor)).Trim() == PanelModel.RecentRowOlder && texts.Count(t => t == torLine) == 1 &&
              !texts.Contains(PanelModel.NotRecorded) && !texts.Contains(PanelModel.FellowNoDays) && texts.Contains(PanelModel.RecentFellowsDaysLine) &&
              PanelModel.DeedsWindowOpen(group, TimeWindow.Yesterday) && !yv.Windows.First(c => c.Id == "Yesterday").Disabled,
              "Y group: Recent with Everyone on Yesterday (heading \"" + yv.HeadingWindow + "\"): You and Edda show what they did that day (Edda's brought in = her shared wood and ore of 7 Oct, " +
              eddaParts["broughtIn"].Values.Sum() + "); Finch says \"" + RowText(Row(SampleWorld.Finch)).Trim() + "\"; Tor (0.8.3) \"" + RowText(Row(SampleWorld.Tor)).Trim() + "\" with \"" + torLine + "\" once; never Not recorded");
        var tv = Recent(group, TimeWindow.ThreeDays); var trows = Rows(tv);
        Check(tv.ShownWindow == TimeWindow.ThreeDays && trows.First(x => x.Id == SampleWorld.Finch).Items.Any(i => (i.Value ?? "").StartsWith("+")) &&
              RowText(trows.First(x => x.Id == SampleWorld.Tor)).Trim() == PanelModel.RecentRowOlder && !PanelModel.AllText(tv).Contains(PanelModel.NotRecorded) &&
              tv.HeadingWindow == null,
              "Y 3 days: Finch's other days show (only yesterday was empty), Tor still says his days are not shared yet; no date in the heading (the chip says the window)");
        // the 7 days of Joost's screenshot: his friends' rows now show their days
        var world = PanelSample.Full(now);
        var sv = Recent(world, TimeWindow.SevenDays); var srows = Rows(sv);
        Check(srows.Count == 4 && srows.All(x => x.Items.Any(i => (i.Value ?? "").StartsWith("+"))) && !PanelModel.AllText(sv).Contains(PanelModel.NotRecorded) && !PanelModel.AllText(sv).Contains(PanelModel.FellowNoDays),
              "Y 7 days: with Everyone on, every friend's row shows their 7 days from their shared days (before: \"Not recorded\" and \"" + PanelModel.FellowNoDays + "\")");
        // a copy on this PC from before the window: its date, not "nothing"
        var stale = GroupDays(now); var staleEdda = stale.Fellows.First(f => f.PlayerName == SampleWorld.Edda); staleEdda.ReceivedUtc = now.AddDays(-4); staleEdda.Cached = true;
        var staleRow = Rows(Recent(stale, TimeWindow.Yesterday)).First(x => x.Id == SampleWorld.Edda);
        Check(RowText(staleRow).Trim().StartsWith("copy from ") && staleRow.Items.All(i => !(i.Value ?? "").StartsWith("+")) &&
              PanelModel.AllText(Recent(stale, TimeWindow.Yesterday)).Any(t => t.StartsWith("Edda's copy on this PC is from ") && t.EndsWith("yesterday is not in it yet.")),
              "Y stale: a copy this PC got before yesterday began says its date (\"" + RowText(staleRow).Trim() + "\"), never nothing that day");
        // a friend's own Recent on Yesterday: open, their day with the line of what a shared day holds
        var books = FullDump.Books(world).ToDictionary(b => b.who, b => b.book);
        var hers = Recent(books[SampleWorld.Edda], TimeWindow.Yesterday, false, SampleWorld.Edda);
        Check(hers.ShownWindow == TimeWindow.Yesterday && PanelModel.Content(hers).Any(b => b.Kind == "section") &&
              PanelModel.AllText(hers).Contains(PanelModel.RecentFellowDayLine(books[SampleWorld.Edda], TimeWindow.Yesterday)) && hers.HeadingWindow == "yesterday, Wednesday 7 Oct",
              "Y friend's book: Edda's Recent opens Yesterday from her shared days and says once what a shared day holds");

        // ---------- the share: the foes defeated per day, its mark, round trip, bounds and bytes ----------
        var kinds = new PanelInput { IsSelf = true };
        DayHistory Month(bool kills)
        {
            var rnd = new Random(17); var h = new DayHistory { From = today.AddDays(-44) };
            for (var d = today.AddDays(-44); d <= today; d = d.AddDays(1))
            {
                var row = new DayHistory.Row { Period = DayHistory.DayKey(d) };
                SessionEvents.Add(row.Game, "CraftWeapon", rnd.Next(1, 4));
                var k = rnd.Next(5, 120); if (kills) SessionEvents.Add(row.Game, "EnemyKills", k);
                h.Rows.Add(row);
            }
            return h;
        }
        string Write(Dictionary<string, Dictionary<string, Dictionary<string, float>>> dd) { var j = new Json().Open(); PanelModel.WriteDayDeeds(j, dd); j.Close(); return j.ToString(); }
        var withK = PanelModel.DayDeeds(Month(true), today, 30, null, kinds); var noK = PanelModel.DayDeeds(Month(false), today, 30, null, kinds);
        var jsonWith = Write(withK); var jsonNo = Write(noK);
        var root = MiniJson.Parse(jsonWith) as Dictionary<string, object>; var dd = MiniJson.Obj(root, PanelModel.DayDeedsKey);
        var back = PanelModel.ReadDayDeeds(dd);
        bool Same(Dictionary<string, Dictionary<string, Dictionary<string, float>>> a, Dictionary<string, Dictionary<string, Dictionary<string, float>>> b) =>
            a != null && b != null && a.Count == b.Count && a.All(x => b.TryGetValue(x.Key, out var y) && x.Value.Count == y.Count &&
                x.Value.All(f => y.TryGetValue(f.Key, out var z) && f.Value.Count == z.Count && f.Value.All(p => z.TryGetValue(p.Key, out var v) && v == p.Value)));
        var oldCopy = MiniJson.Parse("{\"dayDeeds\":{\"parts\":{\"voyage\":[\"sail\",\"helm\"],\"game\":[\"CraftWeapon\"]},\"2026-10-07\":{\"game\":[2]}}}") as Dictionary<string, object>;
        var emptyCopy = MiniJson.Parse(Write(new Dictionary<string, Dictionary<string, Dictionary<string, float>>>())) as Dictionary<string, object>;
        int mark = Json.Q(PanelModel.DayKillsPart).Length + 1;
        // the same month as a copy before 0.9.1 wrote it: no mark, so no leading 0 for the kills on a day with other game counters
        var jsonOld = jsonNo.Replace(Json.Q(PanelModel.DayKillsPart) + ",", "").Replace("\"" + PanelModel.DdGame + "\":[0,", "\"" + PanelModel.DdGame + "\":[");
        int perMonth = jsonWith.Length - jsonOld.Length, packedWith = Transport.Pack(jsonWith).Length, packedNo = Transport.Pack(jsonOld).Length;
        System.Console.WriteLine("INFO Y share: the foes defeated per day add " + perMonth + " bytes of JSON to a month with kills every day, " + mark + " of them the mark once (" +
                                 ((perMonth - mark) / 30.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " per day), " + (packedWith - packedNo) + " bytes packed; a day share of only gear and kills for 30 days is " + jsonWith.Length + " bytes of JSON");
        Check(withK.Count == 30 && withK.Values.All(x => x.TryGetValue(PanelModel.DdGame, out var g) && g.ContainsKey(PanelModel.DayKillsPart) && g.Count <= PanelModel.DayDeedsParts) &&
              Same(back, withK) && PanelModel.SharesDayKills(dd) && !PanelModel.SharesDayKills(MiniJson.Obj(oldCopy, PanelModel.DayDeedsKey)) &&
              PanelModel.SharesDayKills(MiniJson.Obj(emptyCopy, PanelModel.DayDeedsKey)) && jsonOld.Length < jsonNo.Length && perMonth <= mark + 30 * 5 && packedWith - packedNo <= 128 && mark <= 16,
              "Y share: a copy shares the foes defeated per day (a count, no foe) for its last 30 days and reads them back as sent; the mark stands also with nothing defeated, an older copy has none; " +
              "a month with kills every day costs " + perMonth + " bytes of JSON, the mark " + mark + " of them (" + (packedWith - packedNo) + " packed)");
        var friend = PanelInput.FromSnapshot(GroupShare.SharedCopy(SampleWorld.ShareJson(owns[SampleWorld.Edda])));
        var rv = PanelModel.RecentOfFellow(friend, TimeWindow.SevenDays, me);
        var dayKills = friend.DayDeeds.Where(x => string.CompareOrdinal(x.Key, DayHistory.DayKey(today.AddDays(-6))) >= 0).Sum(x => x.Value.TryGetValue(PanelModel.DdGame, out var g) && g.TryGetValue(PanelModel.DayKillsPart, out var k) ? k : 0);
        var older = friend.ShallowCopy(); older.DayKills = false; older.ModVersion = "0.9.0";
        var ro = PanelModel.RecentOfFellow(older, TimeWindow.SevenDays, me);
        Check(friend.DayKills && rv.Shown && rv.Total(DeedLog.Counters) == dayKills && dayKills > 0 && PanelModel.RecentParts(friend, rv)["foes"].Values.Sum() == dayKills &&
              ro.Shown && ro.NotRecorded.Contains(DeedLog.Counters) && ro.Total(DeedLog.Counters) == 0,
              "Y kills: Edda's 7 days on Recent count her foes defeated from her shared days (" + dayKills + "); a copy before 0.9.1 leaves them out as not shared, never 0");
        return fails;
    }
}
