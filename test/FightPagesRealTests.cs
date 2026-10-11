// 0.9 fight pages (src/Panel/Chapters/FightPages.cs) drawn from the data layer's own recorded fight (test/FightGroupTests.cs: four PCs fight The
// Queen in her citadel, their feeds joined, src/FightAdapter.cs's FightBook): every page builds, adds up and links on real amounts, an open fight
// reads Boss fight, a reset says where, and after a restart (counts only) the falls are counted without inventing their types.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven.Panel;

static class FightPagesRealTests
{
    static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        ["SeekerQueen"] = "The Queen", ["Seeker"] = "Seeker", ["SeekerBrood"] = "Seeker Brood", ["$item_mead_poisonres"] = "Poison resistance mead",
    };

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        if (FightGroupTests.RealPages == null) { Check(false, "FR the data layer's fight was not built (FightGroupTests runs first)"); return fails; }
        var now = FightGroupTests.RealNow;
        PanelInput Book(FightBook fights) => new PanelInput { PlayerName = "Edda", NowUtc = now, ToLocal = t => t.AddHours(2), Fights = fights, DisplayName = k => k != null && Names.TryGetValue(k, out var n) ? n : null };
        PanelState At(string page, Action<PanelState> more = null) { var s = new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = page } }; more?.Invoke(s); return s; }
        List<Block> Of(Block b, string kind) => (b?.Items ?? new List<Block>()).Where(i => i.Kind == kind).ToList();
        Block Find(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
        double V(string s) => s == null ? 0 : PanelModel.ParseCount(s);
        var real = Book(FightGroupTests.RealPages); var restart = Book(FightGroupTests.RealRestart);

        // ---------- every page of the recorded fights builds and adds up (the amounts are real, so a total may round a point from its parts' sum) ----------
        var bad = new List<string>(); int pages = 0;
        foreach (var (book, what) in new[] { (real, "live session"), (restart, "after a restart") })
            foreach (var page in new[] { PanelModel.LastFightPage, PanelModel.RunsPage }.Concat(book.Fights.Fights.Select(PanelModel.FightPageOf)))
            {
                PanelView v;
                try { v = PanelModel.Build(book, At(page)); } catch (Exception e) { bad.Add(what + " " + page + ": " + e.GetType().Name + " " + e.Message); continue; }
                pages++;
                var text = PanelModel.AllText(v).ToList();
                if (text.Any(t => t.Contains("—") || System.Text.RegularExpressions.Regex.IsMatch(t, @"\blost\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))) bad.Add(what + " " + page + ": an em-dash or \"lost\"");
                var foes = Of(Find(v, PanelModel.FightFoesKind), "foe"); var players = Of(Find(v, PanelModel.FightPlayersKind), "player");
                if (page == PanelModel.RunsPage) continue;
                if (foes.Count == 0 || players.Count == 0) { bad.Add(what + " " + page + ": no foe rows or players"); continue; }
                foreach (var r in foes)
                {
                    if (Math.Abs(Of(r, "part").Sum(x => V(x.Value)) - V(r.Value)) > 0.5) bad.Add(what + " " + page + ": " + r.Title + "'s players do not make its " + r.Value);
                    if (Math.Abs(Of(r, "rpart").Sum(x => V(x.Value)) - V(r.Note)) > 0.5) bad.Add(what + " " + page + ": " + r.Title + "'s types do not make its " + r.Note);
                }
                foreach (var p in players)
                {
                    var fromFoes = foes.Sum(r => Of(r, "part").Where(x => x.Id == p.Id).Sum(x => V(x.Value)));
                    if (Math.Abs(fromFoes - V(p.Value)) > foes.Count) bad.Add(what + " " + page + ": " + p.Id + " dealt " + p.Value + ", the foe rows " + fromFoes);
                }
            }
        Check(pages >= 8 && bad.Count == 0, "FR every fight page of the recorded Queen fight builds and adds up (" + pages + " pages, the live session and after a restart)" + (bad.Count > 0 ? ": " + string.Join("; ", bad.Take(6)) : ""));

        // ---------- the won Queen fight: Last fight holds it, nested under its run in Runs and bosses, every row opens its page and Back returns ----------
        var last = PanelModel.Build(real, At(PanelModel.LastFightPage));
        var head = Find(last, PanelModel.FightHeadKind); var lane = Of(head, "lane").FirstOrDefault();
        var runs = PanelModel.Build(real, At(PanelModel.RunsPage));
        var rows = PanelModel.Content(runs).Where(b => b.Kind == PanelModel.FightListKind).SelectMany(b => Of(b, "fight")).ToList();
        var nested = rows.SelectMany(r => Of(r, "fight")).ToList();
        var opened = rows.Concat(nested).Select(r =>
        {
            var s = At(PanelModel.RunsPage); PanelModel.Visited(s, Chapter.Battle, PanelModel.Build(real, s).Page);
            PanelModel.Jump(s, r.Id); var v = PanelModel.Build(real, s); PanelModel.Visited(s, Chapter.Battle, v.Page);
            return Find(v, PanelModel.FightFoesKind) != null && PanelModel.Back(s) && s.PageOf(Chapter.Battle) == PanelModel.RunsPage;
        }).ToList();
        var nowBlock = PanelModel.Content(runs).FirstOrDefault(b => b.Kind == PanelModel.FightListKind && b.Tone == "now");
        Check(last.Heading == PanelModel.LastFightLabel && head?.Title == "The Queen" && lane?.Tone == "won" && rows.Count == 1 && rows[0].Tone == "run" && rows[0].Title == "Infested Citadel" && Of(nowBlock, "fight").Count == 1 &&
              nested.Count == 1 && nested[0].Id == "Battle/" + PanelModel.LastFightPage && opened.Count == 2 && opened.All(o => o) &&
              last.List.Any(l => l.Id == PanelModel.RunsPage) && last.List.First(l => l.Id == PanelModel.LastFightPage).Label == PanelModel.LastFightLabel,
              "FR the won Queen fight: Last fight holds it (the lane says defeated), Runs and bosses has the Infested Citadel run on now with the won Queen nested under it, both rows open their page and Backspace returns (ids " +
              string.Join(", ", rows.Concat(nested).Select(r => r.Id)) + ")");

        // ---------- an open boss fight reads Boss fight with the live dot; a reset says where; after a restart the falls count without their types ----------
        var liveBook = new FightBook(); liveBook.Fights.Add(FightGroupTests.RealLive);
        var live = PanelModel.Build(Book(liveBook), At(PanelModel.LastFightPage));
        var entry = live.List.First(l => l.Id == PanelModel.LastFightPage);
        var resetWords = PanelModel.OutcomeWords(FightGroupTests.RealReset);
        var savedRun = restart.Fights.Fights.First(f => f.Kind == FightKind.Run);
        var runPage = PanelModel.Build(restart, At(PanelModel.FightPageOf(savedRun)));
        var falls = Of(Find(runPage, PanelModel.FightPlayersKind), "player").SelectMany(p => Of(p, "deaths")).ToList();
        var line = Of(Find(runPage, PanelModel.FightHeadKind), "runline").FirstOrDefault();
        Check(entry.Label == PanelModel.BossFightLabel && entry.Dot && live.Heading == PanelModel.BossFightLabel && (live.Blocks[0].Pill ?? "").StartsWith("Live, ") &&
              resetWords == "Reset at 97 %" && falls.Sum(d => (int)V(d.Value)) == savedRun.Players.Sum(p => p.Deaths.Count) && falls.All(d => Of(d, "rpart").Count == 0) &&
              line != null && Of(line, "fall").Count == 0 && PanelModel.AllText(runPage).Contains(PanelModel.FallsAbout),
              "FR an open boss fight: \"" + entry.Label + "\" with the dot, " + live.Blocks[0].Pill + "; a reset: " + resetWords + "; after a restart the run's " + falls.Sum(d => (int)V(d.Value)) +
              " falls count without types or a place on its line, and About says why");
        // ---------- the page's numbers are the group join's (GroupFights): each player's dealt and received in the boss fight, their death ----------
        var g = FightGroupTests.RealGroup;
        var lastPlayers = Of(Find(last, PanelModel.FightPlayersKind), "player"); var lastFoes = Of(Find(last, PanelModel.FightFoesKind), "foe");
        var joinOff = new List<string>();
        foreach (var gp in g.Players.Where(p => p.InBoss))
        {
            var row = lastPlayers.FirstOrDefault(p => p.Id == gp.Name);
            if (row == null) { joinOff.Add(gp.Name + " has no row"); continue; }
            double dealt = gp.BossDealt.Sum(), received = gp.BossReceived.Sum();
            if (Math.Abs(V(row.Value) - dealt) > 0.5) joinOff.Add(gp.Name + " dealt " + row.Value + ", the join " + dealt.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            if (Math.Abs(V(row.Value2) - received) > 0.5) joinOff.Add(gp.Name + " received " + row.Value2 + ", the join " + received.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            var parts = lastFoes.Sum(r => Of(r, "part").Where(x => x.Id == gp.Name).Sum(x => V(x.Value)));
            if (Math.Abs(parts - dealt) > lastFoes.Count) joinOff.Add(gp.Name + "'s foe parts make " + parts + ", the join " + dealt.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            var deaths = Of(row, "deaths").Single();
            if ((int)V(deaths.Value) != gp.BossDeaths) joinOff.Add(gp.Name + " died " + deaths.Value + " times, the join " + gp.BossDeaths);
        }
        var eddaDeath = Of(lastPlayers.FirstOrDefault(p => p.Id == "Edda"), "deaths").SingleOrDefault();
        var eddaTypes = string.Join(", ", Of(eddaDeath, "rpart").Select(x => x.Id));
        var liveLane = Of(Find(live, PanelModel.FightHeadKind), "lane").FirstOrDefault();
        var savedRuns = PanelModel.Build(restart, At(PanelModel.RunsPage));
        var savedRows = PanelModel.Content(savedRuns).Where(b => b.Kind == PanelModel.FightListKind).SelectMany(b => Of(b, "fight")).ToList();
        var savedNested = savedRows.SelectMany(r => Of(r, "fight")).ToList();
        Check(joinOff.Count == 0 && eddaDeath?.Value == "1" && eddaTypes == "pierce, poison" && liveLane?.Value == "97 % left" &&
              savedRows.Count == 1 && savedRows[0].Title == "Infested Citadel" && savedRows[0].Note == "Out, 1 death" && savedNested.Count == 1 && savedNested[0].Title == "The Queen" && savedNested[0].Note == "Won",
              "FR the pages say what the join holds: each player's dealt, received and deaths in the boss fight are GroupFights' (" + g.Players.Count(p => p.InBoss) + " players), the foe rows' parts make them; " +
              "Edda's death with its last seconds' types (" + eddaTypes + "); the open fight's lane " + liveLane?.Value + "; after a restart the saved Infested Citadel run (" + savedRows.FirstOrDefault()?.Note +
              ") holds the won Queen (" + savedNested.FirstOrDefault()?.Note + ")" + (joinOff.Count > 0 ? ": " + string.Join("; ", joinOff) : ""));

        // HW_FIGHT_REAL_DUMP=<file.js>: these pages as preview data (window.PANEL_DATA), to look at them in a copy of preview/panel-preview.html
        var dump = Environment.GetEnvironmentVariable("HW_FIGHT_REAL_DUMP");
        if (!string.IsNullOrEmpty(dump))
        {
            var shots = new List<(string name, PanelView v)> { ("real-lastfight", last), ("real-runs", runs), ("real-live", live), ("real-run-restart", runPage) };
            foreach (var f in FightGroupTests.RealPages.Fights) shots.Add(("real-" + f.Kind.ToString().ToLowerInvariant(), PanelModel.Build(real, At(PanelModel.FightPageOf(f)))));
            System.IO.File.WriteAllText(dump, "window.PANEL_DATA = {\n" + string.Join(",\n", shots.Select(x => "  \"" + x.name + "\": " + PanelModel.ToJson(x.v, true))) + "\n};\n");
        }
        return fails;
    }
}
