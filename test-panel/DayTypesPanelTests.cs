// 0.9 (Joost's option A, "Today works like everything else"): with each fellow's damage dealt per day split by weapon kind and type
// (their copy's "dayDealt"), Everyone > Battle > Damage on Today, 7 days and 30 days has By weapon and By type for the whole group and a
// split under every player's row; one line says each player's whole day counts; a copy from before 0.9 keeps a total-only row with its
// one older line; a friend's own Damage opens their days. Fictional sample world (SampleWorld).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class DayTypesPanelTests
{
    static PanelState Group(TimeWindow w, string view = null)
    {
        var s = new PanelState { Chapter = Chapter.Battle, Window = w, WindowPicked = true, Everyone = true };
        s.Page[Chapter.Battle] = "damage";
        if (view != null) s.View["Battle/damage/group"] = view;
        return s;
    }

    static int Count(IEnumerable<string> text, string line) => text.Count(t => t == line);

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        double Num(string s) => s == PanelModel.LessThanOne ? 0.5 : PanelModel.ParseCount(s);
        var (owns, copies) = SampleWorld.Make(now);
        var world = owns[SampleWorld.Rowan];
        var todayKey = DayHistory.DayKey(PanelModel.LocalToday(world));

        // ---------- Today's By type is the sum of every player's day by type: yours from your day history, each fellow's from their shared parts ----------
        var want = new Dictionary<string, double>();
        void Add(string type, double v) { want.TryGetValue(type, out var o); want[type] = o + v; }
        foreach (var r in PanelModel.DealtRows(PanelModel.DamageSinceInstallRows(PanelModel.InWindow(world, TimeWindow.Today)))) Add(r.Type, r.Amount);
        foreach (var f in world.Fellows) if (f.DayDealt != null && f.DayDealt.TryGetValue(todayKey, out var parts)) foreach (var kv in parts) Add(kv.Key.Split('|')[1], kv.Value);
        var typeView = PanelModel.Build(world, Group(TimeWindow.Today, "type"));
        var grid = PanelModel.Content(typeView).FirstOrDefault(b => b.Kind == "damagegrid");
        var got = grid?.Items.Where(b => b.Kind == "dmgtype").ToDictionary(b => b.Id, b => Num(b.Value)) ?? new Dictionary<string, double>();
        var columns = grid?.Items.FirstOrDefault(b => b.Kind == "weapons")?.Items.Select(c => c.Id).ToList() ?? new List<string>();
        var off = want.Where(kv => kv.Value >= 0.5 && (!got.TryGetValue(kv.Key, out var g) || Math.Abs(g - kv.Value) > 1)).Select(kv => kv.Key + " " + Math.Round(kv.Value) + " vs " + (got.TryGetValue(kv.Key, out var g2) ? g2.ToString() : "none")).ToList();
        var fellowsToday = world.Fellows.Where(f => f.DayDealt != null && f.DayDealt.TryGetValue(todayKey, out var p) && p.Count > 0).Select(f => f.PlayerName).ToList();
        Check(typeView.ShownWindow == TimeWindow.Today && grid != null && off.Count == 0 && got.Count == want.Count(kv => kv.Value >= 0.5) && fellowsToday.Count >= 2 && fellowsToday.All(columns.Contains),
              "DTP By type: Everyone's Today grid is the sum of your day history's types and each fellow's shared parts for today, a column for every player who dealt today (" +
              string.Join(", ", columns) + ")" + (off.Count > 0 ? ": " + string.Join("; ", off) : ""));

        // ---------- By player: every row with its split, the hero their sum; By weapon adds up to the same; the one line once, the old note gone ----------
        var byPlayer = PanelModel.Build(world, Group(TimeWindow.Today));
        var rows = PanelModel.Content(byPlayer).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items.Where(r => r.Kind == PanelModel.GroupMemberKind).ToList() ?? new List<Block>();
        var hero = PanelModel.Content(byPlayer).FirstOrDefault(b => b.Kind == "hero");
        var weapons = PanelModel.Content(PanelModel.Build(world, Group(TimeWindow.Today, "weapon"))).Where(b => b.Kind == "dmgmix").ToList();
        var text = PanelModel.AllText(byPlayer).ToList();
        var line = PanelModel.DayWindowLine(TimeWindow.Today);
        var seven = PanelModel.Build(world, Group(TimeWindow.SevenDays));
        var sevenText = PanelModel.AllText(seven).ToList();
        Check(hero != null && rows.Count >= 3 && rows.All(r => r.Items != null && r.Items.Count > 0) && Math.Abs(rows.Sum(r => Num(r.Value)) - Num(hero.Value)) <= 0.5 * rows.Count &&
              weapons.Count > 0 && Math.Abs(weapons.Sum(w => Num(w.Value)) - Num(hero.Value)) <= 0.5 * weapons.Count && Math.Abs(got.Values.Sum() - Num(hero.Value)) <= 0.5 * got.Count &&
              line.Contains("whole day") && line.Contains("online") && Count(text, line) == 1 && !text.Any(t => t.Contains("every player's hits")) &&
              seven.ShownWindow == TimeWindow.SevenDays && Count(sevenText, PanelModel.DayWindowLine(TimeWindow.SevenDays)) == 1 && !sevenText.Contains(line) &&
              PanelModel.DayWindowLine(TimeWindow.SevenDays).Contains("whole days") && PanelModel.DayWindowLine(TimeWindow.SevenDays) != line,
              "DTP By player: Today's rows each carry their split by type and add up to the hero (" + hero?.Value + "); By weapon and By type add up to it too; \"" + line + "\" once on the page, " +
              "7 days its own words, and the old \"need every player's hits\" note is gone");

        // ---------- a friend on a Hearthwoven before 0.9 (one total per day): a total-only row, left out of By weapon and By type, one line ----------
        var torNow = world.Fellows.First(f => f.PlayerName == SampleWorld.Tor);
        var torOld = SampleWorld.Share(owns[SampleWorld.Tor], now, "0.8.1");
        torOld.Trail = torNow.Trail; torOld.Timed = torNow.Timed; torOld.ServerLive = torNow.ServerLive; torOld.ReceivedUtc = torNow.ReceivedUtc; torOld.OnThisSession = torNow.OnThisSession;
        torOld.ViewerSessionStartUtc = torNow.ViewerSessionStartUtc; torOld.ViewerName = torNow.ViewerName; torOld.PlayerNames = torNow.PlayerNames;
        var mixed = world.ShallowCopy(); mixed.Fellows = world.Fellows.Select(f => f == torNow ? torOld : f).ToList();
        var oldRows = PanelModel.Content(PanelModel.Build(mixed, Group(TimeWindow.Today))).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>();
        var torRow = oldRows.FirstOrDefault(r => r.Id == SampleWorld.Tor);
        var torTotal = torOld.DealtByDay != null && torOld.DealtByDay.TryGetValue(todayKey, out var tt) ? tt : 0;
        var oldText = PanelModel.AllText(PanelModel.Build(mixed, Group(TimeWindow.Today))).ToList();
        var oldLines = oldText.Where(t => t.Contains(SampleWorld.Tor) && t.Contains(torOld.ModVersion)).ToList();   // the line naming Tor's older Hearthwoven
        var oldLine = oldLines.FirstOrDefault();
        var oldGrid = PanelModel.Content(PanelModel.Build(mixed, Group(TimeWindow.Today, "type"))).FirstOrDefault(b => b.Kind == "damagegrid");
        var oldColumns = oldGrid?.Items.FirstOrDefault(b => b.Kind == "weapons")?.Items.Select(c => c.Id).ToList() ?? new List<string>();
        Check(torOld.DayDealt == null && torTotal > 0 && torRow?.Kind == PanelModel.GroupMemberKind && Math.Abs(Num(torRow.Value) - torTotal) <= 0.5 && (torRow.Items == null || torRow.Items.Count == 0) &&
              oldLines.Count == 1 && oldLine.Contains("By weapon") && Count(oldText, line) == 1 && oldGrid != null && !oldColumns.Contains(SampleWorld.Tor) && oldColumns.Count == columns.Count - 1,
              "DTP older: Tor on 0.8.1 keeps a total-only row today (" + torRow?.Value + "), By type leaves his column out (" + string.Join(", ", oldColumns) + "), and the page says once: \"" + oldLine + "\"");

        // ---------- a friend's own book: Damage opens their days from the shared parts; the other Battle pages keep them greyed and say why ----------
        var books = FullDump.Books(world).ToDictionary(b => b.who, b => b.book);
        var edda = books[SampleWorld.Edda];
        PanelView Hers(PanelInput book, string page, TimeWindow w, string view = null)
        {
            var s = new PanelState { Chapter = Chapter.Battle, Player = book.PlayerName, Window = w, WindowPicked = true };
            s.Page[Chapter.Battle] = page;
            if (view != null) s.View["Battle/" + page + "/view"] = view;
            return PanelModel.Build(book, s);
        }
        var herToday = Hers(edda, "damage", TimeWindow.Today, "type");
        var herGrid = PanelModel.Content(herToday).FirstOrDefault(b => b.Kind == "damagegrid");
        var herParts = edda.DayDealt != null && edda.DayDealt.TryGetValue(todayKey, out var hp) ? hp : new Dictionary<string, float>();
        var herText = PanelModel.AllText(herToday).ToList();
        var herOverview = Hers(edda, "overview", TimeWindow.Today);
        var torOwn = torOld.ShallowCopy(); torOwn.Fellows = edda.Fellows; torOwn.NowUtc = edda.NowUtc;
        var torToday = Hers(torOwn, "damage", TimeWindow.Today);
        Check(herToday.ShownWindow == TimeWindow.Today && herGrid != null && Math.Abs(Num(herGrid.Value) - herParts.Values.Sum()) <= 0.5 && herParts.Count > 0 &&
              !PanelModel.Content(herToday).Any(b => b.Kind == "dealtfoes") && Count(herText, PanelModel.DayWindowLine(TimeWindow.Today, "Edda")) == 1 &&
              herOverview.ShownWindow != TimeWindow.Today && herOverview.Windows.First(c => c.Id == "Today").Disabled && Says(herOverview, "Edda", "only for damage dealt") &&
              torToday.ShownWindow != TimeWindow.Today && torToday.Windows.First(c => c.Id == "Today").Disabled && Says(torToday, "not shared"),
              "DTP friend's book: Edda's Damage opens Today from her shared parts (" + herGrid?.Value + ", no By foe: a day is not split by foe) with \"" + PanelModel.DayWindowLine(TimeWindow.Today, "Edda") +
              "\"; her Overview keeps Today greyed: \"" + herOverview.WindowWhy + "\"; Tor on 0.8.1 keeps his Damage days greyed: \"" + torToday.WindowWhy + "\"");
        return fails;
    }
}
