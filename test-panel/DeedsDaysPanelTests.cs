// 0.8.5 (v085-groupdeeds): a fellow's Deeds pages in every window. Their days come from what their copy shares per day ("dayDeeds": each page's
// hero and first bar), their 10 min to 3 h from what reached this PC while they play (their trail, Battle's live rule). A friend's Today on
// Woodcutting is their shared day and what their own book shows; Everyone's Deeds views add up on Today and 7 days, each fellow's row their own
// book's number; a copy from 0.8.3 keeps its day chips greyed with the one older line; 10 min opens while they play. Fictional sample world.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class DeedsDaysPanelTests
{
    static PanelState Deeds(string page, TimeWindow w, string who = null, bool everyone = false)
    {
        var s = new PanelState { Chapter = Chapter.Deeds, Window = w, WindowPicked = true, Everyone = everyone, Player = who ?? "" };
        s.Page[Chapter.Deeds] = page;
        return s;
    }

    static int Count(IEnumerable<string> text, string line) => text.Count(t => t == line);

    // the number a Deeds page puts first for the group (EveryoneModel's readers): the bar for Woodcutting, the hero elsewhere, Taming's tamed count
    internal static double Number(PanelView v, string page)
    {
        var all = PanelModel.Content(v).SelectMany(b => new[] { b }.Concat(b.Items ?? new List<Block>())).ToList();
        string Of(Func<Block, bool> where) => all.FirstOrDefault(where)?.Value;
        string shown;
        switch (page)
        {
            case "woodcutting": shown = Of(b => b.Kind == "composition" && b.Title == "Wood brought in"); break;
            case "taming": shown = all.Where(b => b.Kind == "counts").SelectMany(b => b.Items).FirstOrDefault(i => i.Id == "CreatureTamed")?.Value; break;
            default:
                var label = new Dictionary<string, string[]> { ["mining"] = new[] { "stone and ore brought in" }, ["building"] = new[] { "piece built", "pieces built" }, ["groundwork"] = new[] { "groundwork strokes" },
                                                               ["crafting"] = new[] { "gear crafted" }, ["cooking"] = new[] { "dish cooked", "dishes cooked" }, ["farming"] = new[] { "planted" }, ["fishing"] = new[] { "fish caught" } }[page];
                shown = Of(b => (b.Kind == "hero" || b.Kind == "number") && label.Contains(b.Title)); break;
        }
        return string.IsNullOrEmpty(shown) || shown == PanelModel.LessThanOne ? 0 : PanelModel.ParseCount(shown);
    }

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var (owns, copies) = SampleWorld.Make(now);
        var world = owns[SampleWorld.Rowan];
        var books = FullDump.Books(world).ToDictionary(b => b.who, b => b.book);
        var edda = books[SampleWorld.Edda];
        var todayKey = DayHistory.DayKey(PanelModel.LocalToday(world));

        // ---------- a friend's Today on Woodcutting: their shared day, and what their own book shows ----------
        var hers = PanelModel.Build(edda, Deeds("woodcutting", TimeWindow.Today, SampleWorld.Edda));
        var own = PanelModel.Build(owns[SampleWorld.Edda], Deeds("woodcutting", TimeWindow.Today));
        var day = edda.DayDeeds != null && edda.DayDeeds.TryGetValue(todayKey, out var d) ? d : new Dictionary<string, Dictionary<string, float>>();
        double Shared(string fam, string part = null) => day.TryGetValue(fam, out var p) ? (part == null ? p.Values.Sum() : p.TryGetValue(part, out var v) ? v : 0) : 0;
        double Hero(PanelView v, string label) => PanelModel.ParseCount(PanelModel.Content(v).SelectMany(b => new[] { b }.Concat(b.Items ?? new List<Block>())).FirstOrDefault(b => (b.Kind == "hero" || b.Kind == "number") && b.Title == label)?.Value ?? "0");
        var text = PanelModel.AllText(hers).ToList();
        var shareLine = PanelModel.DeedsSharedLine(edda, "woodcutting"); var dayLine = PanelModel.DayWindowLine(TimeWindow.Today, SampleWorld.Edda);
        Check(hers.ShownWindow == TimeWindow.Today && !hers.Windows.First(c => c.Id == "Today").Disabled && Shared(PanelModel.DdWood) > 0 && Shared(PanelModel.DdOwn, PanelModel.DdFelled) > 0 &&
              Number(hers, "woodcutting") == Shared(PanelModel.DdWood) && Hero(hers, "trees felled") == Shared(PanelModel.DdOwn, PanelModel.DdFelled) && Hero(hers, "axe hits") == Shared(PanelModel.DdOwn, PanelModel.DdChop) &&
              Number(own, "woodcutting") == Number(hers, "woodcutting") && Hero(own, "trees felled") == Hero(hers, "trees felled") &&
              Count(text, shareLine) == 1 && Count(text, dayLine) == 1 && !PanelModel.Content(hers).Any(b => b.Kind == "section" && (b.Title ?? "").EndsWith("per tree")) &&
              hers.StripNote != null && hers.StripNote.StartsWith(SampleWorld.Edda),
              "DDP friend's Today: Edda's Woodcutting opens Today from her shared day (" + Hero(hers, "trees felled") + " trees felled, " + Number(hers, "woodcutting") + " wood brought in), " +
              "the same numbers her own book shows today; \"" + shareLine + "\" and \"" + dayLine + "\" once, no per-tree lists");

        // ---------- every Deeds page: Rowan's copy (he has done some of everything) read as a fellow's book shows what his own book shows ----------
        var pages = new[] { "woodcutting", "mining", "building", "groundwork", "crafting", "cooking", "farming", "fishing", "taming" };
        var rowanCopy = SampleWorld.Share(world, now); rowanCopy.ViewerName = SampleWorld.Edda;
        var differ = new List<string>(); var shown = 0;
        foreach (var w in new[] { TimeWindow.Today, TimeWindow.SevenDays })
            foreach (var page in pages)
            {
                var asFellow = PanelModel.Build(rowanCopy, Deeds(page, w, SampleWorld.Rowan)); var asOwn = PanelModel.Build(world, Deeds(page, w));
                double f = Number(asFellow, page), o = Number(asOwn, page);
                var line = PanelModel.DeedsSharedLine(rowanCopy, page);
                if (asFellow.ShownWindow != w || f != o || (f > 0 && line != null && Count(PanelModel.AllText(asFellow), line) != 1)) differ.Add(page + " " + w + ": as a fellow " + f + ", own book " + o);
                else if (f > 0) shown++;
            }
        Check(differ.Count == 0 && shown >= 12,
              "DDP every page: on Today and 7 days each of the nine Deeds pages shows from a copy's shared days the number the player's own book shows (" + shown + " with numbers), with its one line of what the days hold" +
              (differ.Count > 0 ? ": " + string.Join("; ", differ) : ""));

        // ---------- Everyone's Deeds on Today and 7 days: the hero is the rows, each fellow's row their own book's number ----------
        var off = new List<string>(); var counted = 0;
        foreach (var w in new[] { TimeWindow.Today, TimeWindow.SevenDays })
            foreach (var page in pages)
            {
                var g = PanelModel.Build(world, Deeds(page, w, everyone: true));
                var rows = PanelModel.Content(g).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>();
                var members = rows.Where(r => r.Kind == PanelModel.GroupMemberKind).ToList();
                var hero = PanelModel.Content(g).FirstOrDefault(b => b.Kind == "hero");
                double heroV = hero == null ? 0 : PanelModel.ParseCount(hero.Value), sum = members.Sum(r => PanelModel.ParseCount(r.Value));
                if (g.ShownWindow != w) { off.Add(page + " " + w + " shows " + g.ShownWindow); continue; }
                if (Math.Abs(heroV - sum) > 0.5 * members.Count) off.Add(page + " " + w + ": hero " + heroV + " vs rows " + sum);
                foreach (var n in SampleWorld.Everyone.Where(n => n != SampleWorld.Rowan))
                {
                    var row = rows.FirstOrDefault(r => r.Id == n);
                    var mine = Number(PanelModel.Build(owns[n], Deeds(page, w)), page);
                    var theirs = row?.Kind == PanelModel.GroupMemberKind ? PanelModel.ParseCount(row.Value) : 0;
                    if (row == null || (row.Kind != PanelModel.GroupMemberKind && row.Kind != PanelModel.GroupQuietKind) || Math.Abs(theirs - mine) > 0.5) off.Add(page + " " + w + " " + n + ": row " + (row?.Value ?? row?.Text) + ", own book " + mine);
                    else if (row.Kind == PanelModel.GroupMemberKind) counted++;
                }
            }
        var cook7 = PanelModel.AllText(PanelModel.Build(world, Deeds("cooking", TimeWindow.SevenDays, everyone: true))).ToList();
        Check(off.Count == 0 && counted >= 12 && Count(cook7, PanelModel.DayWindowLine(TimeWindow.SevenDays)) == 1 && !cook7.Contains(PanelModel.GroupDaysLine),
              "DDP Everyone: on Today and 7 days every Deeds view's hero is the sum of its rows and each fellow's row is the number their own book shows (" + counted + " fellow rows with numbers); " +
              "\"" + PanelModel.DayWindowLine(TimeWindow.SevenDays) + "\" once" + (off.Count > 0 ? ": " + string.Join("; ", off.Take(6)) : ""));

        // ---------- a friend on 0.8.3 (no Deeds days): their day chips greyed with the one older line; the group leaves them out and says so ----------
        var torNow = world.Fellows.First(f => f.PlayerName == SampleWorld.Tor);
        var torOld = SampleWorld.Share(owns[SampleWorld.Tor], now, "0.8.3");
        torOld.Trail = torNow.Trail; torOld.Timed = torNow.Timed; torOld.ServerLive = torNow.ServerLive; torOld.ReceivedUtc = torNow.ReceivedUtc; torOld.OnThisSession = torNow.OnThisSession;
        torOld.ViewerSessionStartUtc = torNow.ViewerSessionStartUtc; torOld.ViewerName = torNow.ViewerName; torOld.PlayerNames = torNow.PlayerNames; torOld.Fellows = torNow.Fellows; torOld.NowUtc = torNow.NowUtc;
        var mixed = world.ShallowCopy(); mixed.Fellows = world.Fellows.Select(f => f == torNow ? torOld : f).ToList();
        var torPage = PanelModel.Build(torOld, Deeds("woodcutting", TimeWindow.Today, SampleWorld.Tor));
        var olderLine = "Tor's Hearthwoven is 0.8.3: their days fill in once they update.";
        var group = PanelModel.Build(mixed, Deeds("woodcutting", TimeWindow.Today, everyone: true));
        var groupText = PanelModel.AllText(group).ToList();
        var torRow = (PanelModel.Content(group).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>()).FirstOrDefault(r => r.Id == SampleWorld.Tor);
        Check(torOld.DayDeeds == null && torPage.ShownWindow == TimeWindow.SinceInstall && PanelModel.DayWindows.All(x => torPage.Windows.First(c => c.Id == x.ToString()).Disabled) &&
              (torPage.WindowWhy ?? "").Contains(olderLine) && torRow?.Kind == PanelModel.GroupQuietKind && torRow.Text == "shares no days yet" + PanelModel.GroupNotInSum &&
              Count(groupText, "Tor's Hearthwoven is 0.8.3: their days fill in once they update.") == 1,
              "DDP older: Tor on 0.8.3 keeps his Deeds day chips greyed and says \"" + olderLine + "\"; in the group's Today his row says \"" + torRow?.Text + "\" and the page says once why");

        // ---------- Meals: a friend's days and the group's, each player's meals as their own book counts them that window ----------
        double Meals(PanelView v, params string[] labels) => PanelModel.ParseCount(PanelModel.Content(v).SelectMany(b => new[] { b }.Concat(b.Items ?? new List<Block>()))
                                                                .FirstOrDefault(b => (b.Kind == "hero" || b.Kind == "number") && labels.Contains(b.Title))?.Value ?? "0");
        var mealsOff = new List<string>(); var withMeals = 0;
        foreach (var w in new[] { TimeWindow.Today, TimeWindow.SevenDays, TimeWindow.ThirtyDays })
            foreach (var n in SampleWorld.Everyone.Where(n => n != SampleWorld.Rowan))
            {
                var theirs = PanelModel.Build(books[n], Deeds(PanelModel.MealsPageId, w, n)); var ownBook = PanelModel.Build(owns[n], Deeds(PanelModel.MealsPageId, w));
                double a = Meals(theirs, "meal eaten", "meals eaten"), b = Meals(ownBook, "meal eaten", "meals eaten"), at = Meals(theirs, "dish tried", "dishes tried"), bt = Meals(ownBook, "dish tried", "dishes tried");
                if (theirs.ShownWindow != w || a != b || at != bt) mealsOff.Add(n + " " + w + ": " + a + " meals, " + at + " dishes on Rowan's PC, " + b + " and " + bt + " on their own");
                else if (a > 0) withMeals++;
            }
        var mealsState = Deeds(PanelModel.MealsPageId, TimeWindow.SevenDays, everyone: true); mealsState.View["Deeds/" + PanelModel.MealsPageId + "/" + PanelModel.MealsGroupSwitch] = PanelModel.MealsPlayerView;
        var groupMeals = PanelModel.Build(world, mealsState);
        var mealRows = PanelModel.Content(groupMeals).FirstOrDefault(b => b.Kind == "sources" && b.Title == PanelModel.MealsByPlayer)?.Items ?? new List<Block>();
        var mealsHero = Meals(groupMeals, "meal eaten together", "meals eaten together");
        var rowsOk = SampleWorld.Everyone.All(n => { var r = mealRows.FirstOrDefault(x => x.Id == n); var own7 = Meals(PanelModel.Build(owns[n], Deeds(PanelModel.MealsPageId, TimeWindow.SevenDays)), "meal eaten", "meals eaten");
                                                     return r != null && (string.IsNullOrEmpty(r.Value) ? own7 == 0 : PanelModel.ParseCount(r.Value) == own7); });
        var mixedMeals = PanelModel.Build(mixed, mealsState);
        var torMeals = (PanelModel.Content(mixedMeals).FirstOrDefault(b => b.Kind == "sources" && b.Title == PanelModel.MealsByPlayer)?.Items ?? new List<Block>()).FirstOrDefault(r => r.Id == SampleWorld.Tor);
        Check(mealsOff.Count == 0 && withMeals >= 3 && groupMeals.ShownWindow == TimeWindow.SevenDays && mealsHero > 0 && rowsOk && Math.Abs(mealRows.Sum(r => PanelModel.ParseCount(r.Value ?? "0")) - mealsHero) < 0.5 &&
              Count(PanelModel.AllText(groupMeals), PanelModel.DayWindowLine(TimeWindow.SevenDays)) == 1 && torMeals?.Text == "shares no days yet" + PanelModel.GroupNotInSum &&
              Count(PanelModel.AllText(mixedMeals), "Tor's Hearthwoven is 0.8.3: their days fill in once they update.") == 1,
              "DDP meals: a friend's Meals on Today, 7 days and 30 days shows the meals and dishes their own book shows (" + withMeals + " with meals); Everyone > Meals opens 7 days, " + mealsHero +
              " meals together, each player's row their own book's, the whole-day line once; a friend on 0.8.3 is left out and the page says why" + (mealsOff.Count > 0 ? ": " + string.Join("; ", mealsOff) : ""));

        // ---------- Cooking's Who enjoyed your food in a day window: each fellow's meals of your cooking from their shared days ----------
        List<Block> Fans(PanelView v)
        {
            var c = PanelModel.Content(v).ToList(); var at = c.FindIndex(b => b.Kind == "section" && (b.Title ?? "").StartsWith("Who enjoyed"));
            return at < 0 ? null : c.Skip(at + 1).FirstOrDefault(b => b.Kind == "ranking")?.Items;
        }
        double FanOf(List<Block> fans, string n) => PanelModel.ParseCount(fans?.FirstOrDefault(r => r.Id == n)?.Value ?? "0");
        // what each fellow's shared day says they ate of Rowan's cooking: meals with Rowan as the cook, feast servings from feasts Rowan set out
        double Ate(PanelInput f, string day) => f.DayDeeds != null && f.DayDeeds.TryGetValue(day, out var d)
            ? (d.TryGetValue(PanelModel.DdAte, out var a) ? a.Where(kv => kv.Key.StartsWith(SampleWorld.Rowan + "|")).Sum(kv => kv.Value) : 0)
              + (d.TryGetValue(PanelModel.DdFeastOf, out var fo) ? fo.Where(kv => kv.Key.StartsWith(SampleWorld.RowanId + "|")).Sum(kv => kv.Value) : 0) : 0;
        var cookToday = PanelModel.Build(world, Deeds("cooking", TimeWindow.Today)); var cookWeek = PanelModel.Build(world, Deeds("cooking", TimeWindow.SevenDays));
        var cookAll = PanelModel.Build(world, Deeds("cooking", TimeWindow.SinceInstall));
        List<Block> fansToday = Fans(cookToday), fansWeek = Fans(cookWeek), fansAll = Fans(cookAll);
        var enjoyedOff = world.Fellows.Where(f => FanOf(fansToday, f.PlayerName) != Ate(f, todayKey) || FanOf(fansToday, f.PlayerName) > FanOf(fansWeek, f.PlayerName) || FanOf(fansWeek, f.PlayerName) > FanOf(fansAll, f.PlayerName))
                                      .Select(f => f.PlayerName + ": today " + FanOf(fansToday, f.PlayerName) + " (shared " + Ate(f, todayKey) + "), 7 days " + FanOf(fansWeek, f.PlayerName) + ", All " + FanOf(fansAll, f.PlayerName)).ToList();
        var cookText = PanelModel.AllText(cookToday).ToList();
        var olderCook = PanelModel.AllText(PanelModel.Build(mixed, Deeds("cooking", TimeWindow.Today))).ToList();
        var allOld = world.ShallowCopy(); allOld.Fellows = world.Fellows.Select(f => { var o = f.ShallowCopy(); o.DayDeeds = null; o.ModVersion = "0.8.3"; return o; }).ToList();
        var allOldCook = PanelModel.Build(allOld, Deeds("cooking", TimeWindow.Today));
        Check(fansToday != null && fansToday.Count >= 2 && enjoyedOff.Count == 0 && Count(cookText, PanelModel.DayWindowLine(TimeWindow.Today)) == 1 && !cookText.Contains(PanelModel.NoDaysFood) &&
              FanOf(Fans(PanelModel.Build(mixed, Deeds("cooking", TimeWindow.Today))), SampleWorld.Tor) == 0 && Count(olderCook, "Tor's Hearthwoven is 0.8.3: what Tor ate of your food fills in once they update.") == 1 &&
              Fans(allOldCook) == null && PanelModel.AllText(allOldCook).Contains(PanelModel.NoDaysFood),
              "DDP enjoyed: Cooking's Who enjoyed your food opens in Today, each fellow's row the meals of your cooking their shared day holds (" +
              string.Join(", ", fansToday?.Select(r => r.Id + " " + r.Value) ?? new string[0]) + "), within their 7 days and All, with the whole-day line; Tor on 0.8.3 is left out with his " +
              "one line, and when nobody shares days the page keeps \"" + PanelModel.NoDaysFood + "\"" + (enjoyedOff.Count > 0 ? ": " + string.Join("; ", enjoyedOff) : ""));

        // ---------- 10 min on a friend's Deeds while they play; not playing: greyed with Battle's line ----------
        var ten = PanelModel.Build(edda, Deeds("woodcutting", TimeWindow.LastTenMinutes, SampleWorld.Edda));
        var groupTen = PanelModel.Build(world, Deeds("woodcutting", TimeWindow.LastTenMinutes, everyone: true));
        var eddaTen = (PanelModel.Content(groupTen).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>()).FirstOrDefault(r => r.Id == SampleWorld.Edda);
        var gone = edda.ShallowCopy(); gone.Trail = null; gone.ReceivedUtc = now.AddMinutes(-5);   // her last update came five minutes ago: she logged out
        var goneView = PanelModel.Build(gone, Deeds("woodcutting", TimeWindow.LastTenMinutes, SampleWorld.Edda));
        var shortIds = new[] { "LastTenMinutes", "LastThirtyMinutes", "LastHour", "LastThreeHours" };
        // the group's 10 min follows the same rule (0.8.5): five minutes after Edda's last update she is left out with why, her Session still counts
        var goneWorld = world.ShallowCopy(); goneWorld.NowUtc = now.AddMinutes(5);
        Block RowOf(PanelView v, string who) => (PanelModel.Content(v).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>()).FirstOrDefault(r => r.Id == who);
        var goneTen = RowOf(PanelModel.Build(goneWorld, Deeds("woodcutting", TimeWindow.LastTenMinutes, everyone: true)), SampleWorld.Edda);
        var goneSession = RowOf(PanelModel.Build(goneWorld, Deeds("woodcutting", TimeWindow.Session, everyone: true)), SampleWorld.Edda);
        Check(PanelModel.FellowLive(edda) && ten.ShownWindow == TimeWindow.LastTenMinutes && !ten.Windows.First(c => c.Id == "LastTenMinutes").Disabled && ten.StripNote == "Edda, " + PanelModel.GroupLive &&
              eddaTen?.Kind == PanelModel.GroupMemberKind && Number(ten, "woodcutting") == PanelModel.ParseCount(eddaTen.Value) && Number(ten, "woodcutting") > 0 &&
              goneView.ShownWindow == TimeWindow.SinceInstall && shortIds.All(id => goneView.Windows.First(c => c.Id == id).Disabled) && !goneView.Windows.First(c => c.Id == "Session").Disabled &&
              (goneView.WindowWhy ?? "").StartsWith("Edda is not playing now: 10 min to 3 h work while Edda plays.") &&
              goneTen?.Kind == PanelModel.GroupQuietKind && goneTen.Text == PanelModel.FellowNotPlaying + PanelModel.GroupNotInSum && goneSession?.Kind == PanelModel.GroupMemberKind,
              "DDP short: Edda plays, so her Woodcutting opens 10 min (" + Number(ten, "woodcutting") + " wood, as her row in the group's 10 min); her last update five minutes old greys 10 min to 3 h, " +
              "Session stays: \"" + goneView.WindowWhy + "\"; the group's 10 min leaves her out (\"" + goneTen?.Text + "\"), its Session keeps her");
        return fails;
    }
}
