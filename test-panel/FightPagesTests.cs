// 0.9 fight pages (src/Panel/Chapters/FightPages.cs, FightView.cs) on the fight sample (PanelSampleFights.cs): the numbers of a page add up every way
// they can be read (foe rows by player, by type, the players' table, the hero), the outcome words are the ones the book can know, Runs and bosses
// nests a boss fight under its run and every row opens a page, the "bring next time" line names only a drink the game has, and Last fight's
// entry turns into "Boss fight" while one is on.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FightPagesTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelInput Book(bool live) => FightsSample.With(PanelSample.Full(now), live);
        var live = Book(true); var after = Book(false);
        PanelState At(string page, Action<PanelState> more = null) { var s = new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = page } }; more?.Invoke(s); return s; }
        IEnumerable<Block> All(Block b) => new[] { b }.Concat((b?.Items ?? new List<Block>()).SelectMany(All));
        Block Find(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
        double V(string s) => s == null ? 0 : PanelModel.ParseCount(s);
        List<Block> Of(Block b, string kind) => (b?.Items ?? new List<Block>()).Where(i => i.Kind == kind).ToList();

        // ---------- every way of reading a page adds up ----------
        // the foe rows' player parts make each player's dealt; opening every foe row, the players' own received from each make their received;
        // a foe row's parts make its numbers both ways; a player's parts make theirs; the hero says the sums
        var pages = new[] { (live, PanelModel.LastFightPage, "the Queen, live"), (live, "fight:bonemass-1", "Bonemass, reset"), (live, "fight:queen-2", "the Queen, reset"),
                            (live, "fight:mine-1", "the Infested Mine"), (after, PanelModel.LastFightPage, "the Greydwarf camp") };
        foreach (var (book, page, what) in pages)
        {
            var v = PanelModel.Build(book, At(page));
            var foes = Of(Find(v, PanelModel.FightFoesKind), "foe"); var players = Of(Find(v, PanelModel.FightPlayersKind), "player");
            var bad = new List<string>();
            foreach (var p in players)
            {
                var fromFoes = foes.Sum(r => Of(r, "part").Where(x => x.Id == p.Id).Sum(x => V(x.Value)));
                if (Math.Abs(fromFoes - V(p.Value)) > 0.5) bad.Add(p.Id + " dealt " + p.Value + " but the foe rows give " + fromFoes);
                // received: each foe row opened, the player's own sub row
                var recv = foes.Sum(r =>
                {
                    var open = PanelModel.Build(book, At(page, s => s.View[Find(v, PanelModel.FightFoesKind).Id] = r.Id));
                    var row = Of(Find(open, PanelModel.FightFoesKind), "foe").First(x => x.Id == r.Id);
                    return Of(row, "sub").Where(x => x.Id == p.Id).Sum(x => V(x.Note));
                });
                if (Math.Abs(recv - V(p.Value2)) > 0.5) bad.Add(p.Id + " received " + p.Value2 + " but the open foe rows give " + recv);
                if (Math.Abs(Of(p, "part").Sum(x => V(x.Value)) - V(p.Value)) > 0.5) bad.Add(p.Id + "'s dealt parts");
                if (Math.Abs(Of(p, "rpart").Sum(x => V(x.Value)) - V(p.Value2)) > 0.5) bad.Add(p.Id + "'s received parts");
            }
            foreach (var r in foes)
            {
                if (Math.Abs(Of(r, "part").Sum(x => V(x.Value)) - V(r.Value)) > 0.5) bad.Add(r.Title + " dealt by player");
                if (Math.Abs(Of(r, "rpart").Sum(x => V(x.Value)) - V(r.Note)) > 0.5) bad.Add(r.Title + " received by type");
            }
            var head = PanelModel.Content(v).FirstOrDefault(b => b.Kind == PanelModel.FightHeadKind || b.Kind == "hero");
            var nums = head == null ? new List<Block>() : head.Kind == "hero" ? new[] { head }.Concat(head.Items ?? new List<Block>()).ToList() : Of(head, "number");
            double dealt = V(nums.ElementAtOrDefault(0)?.Value), received = V(nums.ElementAtOrDefault(1)?.Value);
            if (Math.Abs(foes.Sum(r => V(r.Value)) - dealt) > 0.5 || Math.Abs(players.Sum(p => V(p.Value)) - dealt) > 0.5) bad.Add("dealt together " + dealt);
            if (Math.Abs(foes.Sum(r => V(r.Note)) - received) > 0.5 || Math.Abs(players.Sum(p => V(p.Value2)) - received) > 0.5) bad.Add("received together " + received);
            Check(foes.Count > 0 && players.Count > 0 && bad.Count == 0, "FP " + what + ": the foe rows' player parts make each player's dealt, the open rows each one's received, every bar its number, the hero the sums (" +
                  foes.Count + " foes, " + players.Count + " players)" + (bad.Count > 0 ? ": " + string.Join("; ", bad) : ""));
        }

        // ---------- the outcome words ----------
        var queen = live.Fights.ById("queen-now"); var bonemass = live.Fights.ById("bonemass-1"); var mine = live.Fights.ById("mine-1");
        var away = new FightView { Kind = FightKind.Boss, Outcome = FightOutcome.GotAway, Health = 0.58f };
        var won = new FightView { Kind = FightKind.Boss, Outcome = FightOutcome.Won, Health = 0 };
        var words = new[] { PanelModel.OutcomeWords(queen), PanelModel.OutcomeWords(bonemass), PanelModel.OutcomeWords(away), PanelModel.OutcomeWords(won), PanelModel.OutcomeWords(mine) };
        var everyPage = new[] { PanelModel.LastFightPage, PanelModel.RunsPage }.Concat(live.Fights.Fights.Select(PanelModel.FightPageOf)).SelectMany(p => PanelModel.AllText(PanelModel.Build(live, At(p)))).ToList();
        Check(words.Distinct().Count() == 5 && words[0].Contains("64 %") && words[1].Contains("41 %") && words[2].Contains("58 %") && !words[3].Any(char.IsDigit) && words[4].Contains("4 deaths") && !everyPage.Any(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"\blost\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)),
              "FP outcome words, one for each outcome with its own number (health left, reset at, got away at, the deaths): " + string.Join(" / ", words) + "; \"lost\" nowhere on the fight pages (" + everyPage.Count + " strings)");

        // ---------- 0.8.6 run words (Joost, 13 Oct, on "Out, 1 death": "wat betekent out?"): a run is "Inside now" or "Left", the same word in the
        // list, the page's pill and its time line; never a bare "Out" or "Inside" ----------
        {
            var citadelNow = live.Fights.ById("citadel-now");
            var minePg = PanelModel.Build(live, At("fight:mine-1")); var nowPage = PanelModel.Build(live, At("fight:citadel-now")); var runsPage = PanelModel.Build(live, At(PanelModel.RunsPage));
            var mineLine = Of(Find(minePg, PanelModel.FightHeadKind), "runline").SingleOrDefault(); var nowLine = Of(Find(nowPage, PanelModel.FightHeadKind), "runline").SingleOrDefault();
            var runText = new[] { minePg, nowPage, runsPage }.SelectMany(PanelModel.AllText).ToList();
            var bare = runText.Where(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"\bOut\b|\bout \d|^Inside(?! now)\b|^in \d")).Distinct().ToList();
            var w = new[] { PanelModel.OutcomeWords(mine), PanelModel.OutcomeWords(citadelNow), minePg.Blocks[0].Pill, nowPage.Blocks[0].Pill, mineLine?.Value, mineLine?.Value2, nowLine?.Value2 };
            Check(w[0] == "Left, 4 deaths" && w[1].StartsWith("Inside now, ") && w[2] == "Left after 18 min" && w[3].StartsWith("Inside now · ") && (w[4] ?? "").StartsWith("went in ") && (w[5] ?? "").StartsWith("left ") && w[6] == "now" && bare.Count == 0,
                  "FP 0.8.6 a run's words a cold player gets, the same in the list, the pill and the time line: " + string.Join(" / ", w) + (bare.Count > 0 ? "; still bare: " + string.Join(", ", bare) : ""));
        }

        // ---------- 0.8.6 (Joost's PC: "A dungeon 34 s · Out, no deaths · 0"): a look into a dungeon under a minute with nothing dealt, received or
        // fallen is kept, not listed; one longer, or with any damage, or on now, is listed ----------
        {
            var book = Book(false);
            FightView Visit(string id, double minAgo, int seconds, bool isLive = false, double dealt = 0)
            {
                var f = new FightView { Id = id, Kind = FightKind.Run, Outcome = isLive ? FightOutcome.Live : FightOutcome.Ended, Place = "Burial Chamber", Biome = "BlackForest", StartUtc = now.AddMinutes(-minAgo), EndUtc = now.AddMinutes(-minAgo).AddSeconds(seconds) };
                f.Players.Add(new FightPlayer { Name = FightsSample.Rowan, You = true, Has = true, InsideSeconds = seconds });
                if (dealt > 0) { var foe = new FightFoe { Prefab = "Skeleton", Count = 1 }; var part = new FightPart(); part.Dealt["slash"] = dealt; foe.By[FightsSample.Rowan] = part; f.Foes.Add(foe); }
                return f;
            }
            book.Fights.Fights.InsertRange(0, new[] { Visit("glance", 1, 34), Visit("slow", 4, 120), Visit("quick", 6, 40, dealt: 60) });
            var rows = PanelModel.Content(PanelModel.Build(book, At(PanelModel.RunsPage))).Where(b => b.Kind == PanelModel.FightListKind).SelectMany(b => Of(b, "fight")).Select(r => r.Id).ToList();
            var glancePage = PanelModel.Build(book, At("fight:glance"));
            var only = Book(false); only.Fights.Fights.Clear(); only.Fights.Fights.Add(Visit("glance", 1, 34));
            var onlyPage = PanelModel.Build(only, At(PanelModel.RunsPage));
            var inside = Book(false); inside.Fights.Fights.Insert(0, Visit("inside", 0.2, 10, isLive: true));
            var insideRows = PanelModel.Content(PanelModel.Build(inside, At(PanelModel.RunsPage))).Where(b => b.Kind == PanelModel.FightListKind).SelectMany(b => Of(b, "fight")).Select(r => r.Id).ToList();
            Check(!rows.Contains("Battle/fight:glance") && rows.Contains("Battle/fight:slow") && rows.Contains("Battle/fight:quick") && glancePage.Heading.StartsWith("Burial Chamber") &&
                  PanelModel.AllText(onlyPage).Contains(PanelModel.NoRunsYet) && insideRows.Contains("Battle/fight:inside"),
                  "FP 0.8.6 the 34 s look with nothing fought is not a row (its page still opens: " + glancePage.Heading + "); 2 min with nothing, or 40 s with a hit, are rows; the look alone leaves the page empty; a run on now is listed at 10 s");
        }

        // ---------- 0.8.6: runs a restart kept, shaped as on Joost's PC (the place's game name, no name token): named from the place, not "A dungeon" ----------
        {
            var saved = FightsSample.WithSavedRuns(PanelSample.Full(now));
            var rows = PanelModel.Content(PanelModel.Build(saved, At(PanelModel.RunsPage))).Where(b => b.Kind == PanelModel.FightListKind).SelectMany(b => Of(b, "fight")).ToList();
            var lands = saved.Fights.Fights.Select(f => f.Biome ?? "-").ToList();
            Check(rows.Select(r => r.Title).SequenceEqual(new[] { "Burial Chamber", "Infested Mine" }) && rows[1].Note == "Left, 1 death" && saved.Fights.Fights.Count == 3 && lands.SequenceEqual(new[] { "BlackForest", "BlackForest", "Mistlands" }) &&
                  FightAdapter.DungeonName("SunkenCrypt4") == "Sunken Crypt" && FightAdapter.DungeonName("MountainCave02") == "Frost Cave" && FightAdapter.DungeonName("Interior") == null && FightAdapter.DungeonLand("MorgenHole3") == "AshLands",
                  "FP 0.8.6 saved runs with no name token are named from the place's game name: " + string.Join(", ", rows.Select(r => r.Title + " (" + r.Note + ")")) + "; lands " + string.Join(", ", lands) + "; the 34 s look kept, not listed");
        }

        // ---------- Runs and bosses: the fight on now on top, then newest first, a boss fight under its run; every row opens its page ----------
        var runs = PanelModel.Build(live, At(PanelModel.RunsPage));
        var lists = PanelModel.Content(runs).Where(b => b.Kind == PanelModel.FightListKind).ToList();
        var nowRow = Of(lists.FirstOrDefault(l => l.Tone == "now"), "fight").FirstOrDefault();
        var earlier = Of(lists.FirstOrDefault(l => l.Tone == "earlier"), "fight");
        var nested = earlier.ToDictionary(r => r.Id, r => Of(r, "fight").Select(n => n.Id).ToList());
        var nowNested = Of(nowRow, "fight");
        var targets = new[] { nowRow }.Concat(nowNested).Concat(earlier).Concat(earlier.SelectMany(r => Of(r, "fight"))).Where(r => r != null).Select(r => r.Id).ToList();
        var opened = targets.Select(t =>
        {
            var s = At(PanelModel.RunsPage); var seen = PanelModel.Build(live, s); PanelModel.Visited(s, Chapter.Battle, seen.Page);
            PanelModel.Jump(s, t); var v = PanelModel.Build(live, s); PanelModel.Visited(s, Chapter.Battle, v.Page);
            var back = PanelModel.Back(s) && s.PageOf(Chapter.Battle) == PanelModel.RunsPage;
            return (t, ok: v.Heading != PanelModel.RunsLabel && PanelModel.Content(v).Any(b => b.Kind == PanelModel.FightFoesKind) && v.List.Single(l => l.Selected).Id == (t.EndsWith(PanelModel.LastFightPage) ? PanelModel.LastFightPage : PanelModel.RunsPage) && back);
        }).ToList();
        Check(nowRow?.Id == "Battle/fight:citadel-now" && nowRow.Title == "Infested Citadel" && nowRow.Text == "18:05" && nowNested.Count == 1 && nowNested[0].Id == "Battle/" + PanelModel.LastFightPage &&
              Of(nowNested[0], "lane").Count == 1 && Of(nowNested[0], "foe").Count == 3 &&
              earlier.Select(r => r.Id).SequenceEqual(new[] { "Battle/fight:mine-1", "Battle/fight:citadel-2", "Battle/fight:bonemass-1" }) &&
              nested["Battle/fight:citadel-2"].SequenceEqual(new[] { "Battle/fight:queen-2" }) && nested["Battle/fight:mine-1"].Count == 0 && nested["Battle/fight:bonemass-1"].Count == 0 &&
              !earlier.Any(r => r.Id.EndsWith("citadel-now") || r.Id.EndsWith("queen-2") || r.Id.EndsWith("camp")) && opened.All(o => o.ok) && Says(runs, "3 runs", "3 boss fights"),
              "FP Runs and bosses: the Infested Citadel run on now on top (› to its page, 18:05), The Queen live under it with her lane and the 3 foes around her, then " + string.Join(", ", earlier.Select(r => r.Title)) + " newest first, the earlier Queen nested under its Citadel run; each of " +
              opened.Count + " rows opens its page with its list entry lit and Backspace returns" + (opened.Any(o => !o.ok) ? ": " + string.Join(", ", opened.Where(o => !o.ok).Select(o => o.t)) : ""));

        // ---------- Runs and bosses by time (0.8.6): a run in the window when any part of it is; a live one always; a boss fight follows its run ----------
        {
            var t0 = now; var from = (DateTime?)t0.AddMinutes(-30);
            FightView F(string id, FightKind k, int startAgo, int endAgo, string parent = null, bool isLive = false) =>
                new FightView { Id = id, Kind = k, ParentId = parent, Outcome = isLive ? FightOutcome.Live : FightOutcome.Ended, StartUtc = t0.AddMinutes(-startAgo), EndUtc = t0.AddMinutes(-endAgo) };
            var straddle = F("straddle", FightKind.Run, 50, 20);        // began before the window, ended inside
            var before = F("before", FightKind.Run, 90, 45);            // ended before the window
            var inner = F("inner", FightKind.Boss, 50, 45, "straddle"); // its own times lie before the window, its run's reach in
            var strayBoss = F("stray", FightKind.Boss, 60, 40);         // no run kept: its own times
            var outerBoss = F("outer", FightKind.Boss, 90, 60, "before");
            var liveRun = F("live", FightKind.Run, 200, 0, isLive: true);
            var ids = PanelModel.RunsInWindow(new[] { straddle, before, inner, strayBoss, outerBoss, liveRun }, from).Select(f => f.Id).OrderBy(x => x).ToList();
            var tight = PanelModel.RunsInWindow(new[] { straddle, before, inner, strayBoss, outerBoss, liveRun }, t0.AddMinutes(-10)).Select(f => f.Id).OrderBy(x => x).ToList();
            var all = PanelModel.RunsInWindow(new[] { straddle, before, inner, strayBoss, outerBoss, liveRun }, null).Count;
            var thirty = PanelModel.Build(after, At(PanelModel.RunsPage, s => { s.Window = TimeWindow.LastThirtyMinutes; s.WindowPicked = true; }));
            var empty = Find(thirty, "empty");
            Check(ids.SequenceEqual(new[] { "inner", "live", "straddle" }) && tight.SequenceEqual(new[] { "live" }) && all == 6 &&
                  thirty.Windows.Count == 11 && thirty.ShownWindow == TimeWindow.LastThirtyMinutes && empty != null && empty.Title == "Nothing in the last 30 min" && empty.Text.StartsWith("Newest run: ") && empty.Text.EndsWith(" ago."),
                  "FP Runs and bosses by time: began before and ended inside counts, ended before does not, a live one is in, a boss fight follows its run (" + string.Join(",", ids) + "); an empty 30 min says so and names the newest (" + empty?.Title + " / " + empty?.Text + ")");
        }

        // ---------- bring next time: Remedy's drinks from a third, a shield only when physical hits landed and you blocked little ----------
        Dictionary<string, double> R(params (string t, double v)[] x) => x.ToDictionary(p => p.t, p => p.v);
        var b1 = PanelModel.BringLine(R(("poison", 62), ("slash", 38)), "you", 6, 20);
        var b2 = PanelModel.BringLine(R(("fire", 50), ("blunt", 50)), "you", 0, 10);   // a tie goes to the drink
        var b3 = PanelModel.BringLine(R(("slash", 70), ("poison", 30)), "Edda", 1, 10);   // one block against ten landing: the shield
        var b4 = PanelModel.BringLine(R(("slash", 70), ("poison", 30)), "you", 3, 10);   // three against ten: the shield was up; poison under a third: nothing
        var b5 = PanelModel.BringLine(R(("lightning", 60), ("spirit", 40)), "you", 0, 10);   // nothing in the game against them
        var b6 = PanelModel.BringLine(R(("blunt", 40), ("slash", 35), ("poison", 25)), "you", 0, 12);   // no one physical type leads: together
        var b7 = PanelModel.BringLine(R(("frost", 100)), "you", 0, -1);
        var b8 = PanelModel.BringLine(R(("blunt", 26), ("lightning", 74)), "you", 0, 8);   // a quarter is enough for the shield
        var b9 = PanelModel.BringLine(R(("blunt", 100)), "you", 0, 4);   // under five hits landed: too small a fight to advise on
        var b10 = PanelModel.BringLine(R(("blunt", 100)), "you", 0, -1);   // hits not known (a fellow's copy, a saved fight): no shield line
        // the facts of a line: the damage type, its share, whom it hit and what to bring (the sentence around them is free to change)
        bool Has(string line, params string[] facts) => line != null && facts.All(line.Contains);
        var queenPage = PanelModel.Build(live, At(PanelModel.LastFightPage)); var campPage = PanelModel.Build(after, At(PanelModel.LastFightPage)); var minePage = PanelModel.Build(live, At("fight:mine-1"));
        var onQueen = Texts(queenPage).FirstOrDefault(t => t.Contains("Poison resistance mead")); var onCamp = Texts(campPage).FirstOrDefault(t => t.Contains("shield"));
        Check(Has(b1, "Poison", "62 %", "you", "Poison resistance mead") && Has(b2, "Fire", "50 %", "Fire resistance barley wine") &&
              Has(b3, "Slash", "70 %", "Edda", "shield") && b4 == null && b5 == null && Has(b6, "Blunt", "slash", "75 %", "shield") &&
              Has(b7, "Frost", "100 %", "Frost resistance mead") && Has(b8, "Blunt", "26 %", "shield") && b9 == null && b10 == null &&
              Says(queenPage, "Poison", "62 %", "Poison resistance mead") && Says(campPage, "Blunt", "68 %", "shield") && Says(minePage, "Poison", "37 %", "Poison resistance mead"),
              "FP bring next time: " + b1 + " | " + b2 + " | " + b3 + " | " + b6 + " | " + b8 + " | none when the shield worked (a block or more per 5 hits landed), under 5 hits, hits not known, a drink under a third, or for lightning and spirit | the Queen: " + onQueen + " | the camp (1 block, 9 hits landed): " + onCamp);

        // ---------- the layout switch never changes a number; the players in the book's order ----------
        var side = PanelModel.Build(live, At(PanelModel.LastFightPage));
        var stacked = PanelModel.Build(live, At(PanelModel.LastFightPage, s2 => s2.View[PanelModel.FightLayoutKey] = PanelModel.FightStacked));
        var sideRows = Of(Find(side, PanelModel.FightFoesKind), "foe");
        var twoBlocks = PanelModel.Content(stacked).Where(b => b.Kind == PanelModel.FightFoesKind).ToList();
        var dealtRows = Of(twoBlocks.FirstOrDefault(b => b.Tone == "dealt"), "foe"); var recvRows = Of(twoBlocks.FirstOrDefault(b => b.Tone == "received"), "foe");
        var order = Of(Find(PanelModel.Build(live, At("fight:mine-1")), PanelModel.FightPlayersKind), "player").Select(p => p.Id).ToList();
        var bonemassPage = PanelModel.Build(live, At("fight:bonemass-1"));
        var eddaFell = Of(Find(bonemassPage, PanelModel.FightPlayersKind), "player").First(p => p.Id == "Edda");
        Check(twoBlocks.Count == 2 && dealtRows.Select(r => r.Value).SequenceEqual(sideRows.Select(r => r.Value)) && recvRows.Select(r => r.Note).SequenceEqual(sideRows.Select(r => r.Note)) &&
              dealtRows.All(r => r.Note == null && Of(r, "rpart").Count == 0) && recvRows.All(r => r.Value == null && Of(r, "part").Count == 0) &&
              order.SequenceEqual(new[] { "Rowan", "Edda", "Finch", "Tor" }) &&
              Of(eddaFell, "deaths").Single().Value == "1" && Of(Of(eddaFell, "deaths").Single(), "rpart").Count == 0 && PanelModel.AllText(bonemassPage).Contains(PanelModel.FallsAbout) &&
              !PanelModel.AllText(side).Contains(PanelModel.FallsAbout),
              "FP stacked shows the same rows as side by side, one column a block; the table runs you, then by name (" + string.Join(", ", order) + "); a fellow's fall past their shared ten counts without types, and About says why");

        // ---------- Last fight's entry: "Boss fight" with the live dot while one is on; no fights known: the 0.8 page, no Runs and bosses ----------
        Choice Entry(PanelInput book, string id) => PanelModel.Build(book, At(PanelModel.LastFightPage)).List.FirstOrDefault(l => l.Id == id);
        var plain = PanelSample.Full(now);
        var liveEntry = Entry(live, PanelModel.LastFightPage); var afterEntry = Entry(after, PanelModel.LastFightPage);
        var oldPage = PanelModel.Build(plain, At(PanelModel.LastFightPage));
        Check(liveEntry.Label == PanelModel.BossFightLabel && liveEntry.Dot && liveEntry.Icon == PanelModel.BossFightIcon && PanelModel.Build(live, At(PanelModel.LastFightPage)).Heading == PanelModel.BossFightLabel &&
              afterEntry.Label == PanelModel.LastFightLabel && !afterEntry.Dot && Entry(live, PanelModel.RunsPage) != null &&
              Entry(plain, PanelModel.RunsPage) == null && PanelModel.Content(oldPage).Any(b => b.Kind == "fightfoes") && !PanelModel.Content(oldPage).Any(b => b.Kind == PanelModel.FightFoesKind),
              "FP Last fight's entry: \"" + liveEntry.Label + "\" with the dot during The Queen, \"" + afterEntry.Label + "\" after; a book without fights keeps the 0.8 page and has no Runs and bosses");
        // ---------- 0.8.6: Last fight is yours; what the server holds without you is one quiet line and its own page ----------
        {
            var server = FightsSample.WithServerFight(FightsSample.With(PanelSample.Full(now), false));
            var bossBook = FightsSample.WithServerFight(FightsSample.With(PanelSample.Full(now), true), boss: true);
            string Hm(PanelInput b, DateTime t) => b.ToLocal(t).ToString("HH:mm");
            var lastPage = PanelModel.Build(server, At(PanelModel.LastFightPage));
            var line = PanelModel.PlateOf(lastPage)?.Items?.FirstOrDefault(b => b.Kind == "link" && b.Id != null && b.Id.StartsWith("Battle/fight:server"));
            var want = "Newer on the server: Edda fought a Seeker at " + Hm(server, server.Fights.ById("server-fight").StartUtc) + " (you were not in it)";
            var theirs = PanelModel.Build(server, At("fight:server-fight"));
            var theirsText = PanelModel.AllText(theirs).ToList();
            var lastText = PanelModel.AllText(lastPage).ToList();
            Check(server.Fights.Last?.Id == "camp" && server.Fights.NewerOnServer?.Id == "server-fight" && PanelModel.Content(lastPage).Any(b => b.Kind == PanelModel.FightHeadKind && b.Title == "Black Forest") &&
                  line != null && line.Title == want && line.Id == "Battle/fight:server-fight" && lastText.Count(t => t.StartsWith("Newer on the server")) == 1 &&
                  theirs.Heading == "Fight at " + Hm(server, server.Fights.ById("server-fight").StartUtc) + ", " + server.ToLocal(server.Fights.ById("server-fight").StartUtc).ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture) && theirs.StripNote == PanelModel.NotInFight &&
                  !theirsText.Contains(PanelModel.KeptAbout) && theirs.List.Single(l => l.Selected).Id == PanelModel.LastFightPage &&
                  PanelModel.PlateOf(PanelModel.Build(after, At(PanelModel.LastFightPage)))?.Items?.Any(b => b.Kind == "link") != true && PanelModel.Build(after, At(PanelModel.LastFightPage)).StripNote != PanelModel.NotInFight,
                  "FP 0.8.6 Last fight stays your own fight when a fellow's is newer: the camp, with one line \"" + (line?.Title ?? "none") + "\" linking to the fellow's own page, which says \"" + theirs.StripNote + "\" and has no earlier-session note; with nothing newer there is no line");
            var bossPage = PanelModel.Build(bossBook, At(PanelModel.LastFightPage));
            var bossLine = PanelModel.PlateOf(bossPage)?.Items?.FirstOrDefault(b => b.Kind == "link");
            var bossEntry = Entry(bossBook, PanelModel.LastFightPage);
            var bossTheirs = PanelModel.Build(bossBook, At("fight:server-boss"));
            Check(bossBook.Fights.LiveBoss?.Id == "server-boss" && bossBook.Fights.Last?.Id == "queen-now" && bossLine != null &&
                  bossLine.Title == "Newer on the server: Finch and Tor are fighting Eikthyr (you are not in it)" && bossTheirs.StripNote == PanelModel.NotInFight,
                  "FP 0.8.6 a fellow's live boss fight without you: Last fight is still your Queen, the line says \"" + (bossLine?.Title ?? "none") + "\", its page \"" + bossTheirs.StripNote + "\"");
            var noRecord = PanelSample.Full(now); noRecord.Battle = new BattleRecorder(); noRecord.KeptSessions = null;   // none of your own fights, as the fight book says
            var onlyTheirs = FightsSample.WithServerFight(FightsSample.With(noRecord, false));
            onlyTheirs.Fights.Fights.RemoveAll(f => f.Id != "server-fight");
            var noneYours = PanelModel.Build(onlyTheirs, At(PanelModel.LastFightPage));
            var fellowLive = FightsSample.WithServerFight(FightsSample.With(PanelSample.Full(now), false), boss: true);
            fellowLive.Fights.Fights.RemoveAll(f => f.Id != "server-boss");
            var entryNoBoss = Entry(fellowLive, PanelModel.LastFightPage);
            Check(onlyTheirs.Fights.Last == null && PanelModel.Content(noneYours).Any(b => b.Kind == "empty") && PanelModel.PlateOf(noneYours).Items.Any(b => b.Kind == "link" && b.Title.StartsWith("Newer on the server: Edda fought a Seeker")) &&
                  entryNoBoss.Label == PanelModel.LastFightLabel && !entryNoBoss.Dot && PanelModel.Build(fellowLive, At(PanelModel.LastFightPage)).Heading == PanelModel.LastFightLabel,
                  "FP 0.8.6 none of your fights: Last fight falls back to the empty 0.8 page with the server line, and a fellow's live boss fight alone leaves the entry \"" + entryNoBoss.Label + "\" without the live dot");
        }
        // ---------- 0.8.6: a friend's Last fight is the same fight page, seen by them ----------
        // (Joost in game, 14 Oct: the same fight at 22:31 read "all 31 foes defeated" on his book and "18 foes defeated of 18" on a friend's, the 0.8 page)
        {
            const string Rowan = FightsSample.Rowan, Edda = FightsSample.Edda, Tor = FightsSample.Tor;
            PanelState AsFriend(string page) => At(page, s => s.Player = Edda);
            Block Head(PanelView v) => Find(v, PanelModel.FightHeadKind);
            List<string> Nums(PanelView v) => Of(Head(v), "number").Select(n => n.Value).ToList();
            var own = FightsSample.With(PanelSample.Full(now), false);
            var friend = FightsSample.Friend(own, Edda);
            var theirs = PanelModel.Build(friend, AsFriend(PanelModel.LastFightPage)); var ours = PanelModel.Build(own, At(PanelModel.LastFightPage));
            var rows = Of(Find(theirs, PanelModel.FightPlayersKind), "player");
            var pace = Of(Head(theirs), "number").ElementAtOrDefault(2);
            var fallback = PanelModel.Build(FightsSample.Friend(PanelSample.Full(now), Edda), AsFriend(PanelModel.LastFightPage));   // your PC joined no fights: the 0.8 page from her copy
            Check(Head(theirs) != null && Head(theirs).Text == Head(ours).Text && rows.FirstOrDefault()?.Id == Edda && rows[0].Title == Edda && rows[0].Note == Edda + "'s PC" &&
                  rows.Any(r => r.Id == Rowan && r.Title == Rowan + " (you)") && rows.Any(r => r.Id == Tor && r.Note == "near " + Edda + " 30 s") && pace != null && pace.Title == Edda + "'s DPS" && pace.Value + " DPS" == PanelModel.FightPace(32, 400) &&
                  theirs.StripNote != PanelModel.NotInFight && Head(fallback) == null && PanelModel.Content(fallback).Any(b => b.Kind == "hero"),
                  "FP 0.8.6 a friend's Last fight is the fight page with them as its owner: \"" + Head(theirs)?.Text + "\", " + rows.FirstOrDefault()?.Title + " first (" + rows.FirstOrDefault()?.Note + "), \"" +
                  rows.FirstOrDefault(r => r.Id == Rowan)?.Title + "\", \"" + pace?.Value + " " + pace?.Title + "\"; with no fights joined on your PC their 0.8 page");

            // the one truth, through the recorder and the fight adapter: Rowan hits six Greydwarfs, Edda three of them; her own copy holds those three, so her
            // 0.8 page counted "3 foes defeated of 3" where the group fight has six: both books read the one joined fight
            var t0 = now.AddMinutes(-5); var dr = new BattleRecorder(); var de = new BattleRecorder();
            for (uint i = 0; i < 6; i++)
            {
                var id = new FoeId(42, 300 + i); var at = t0.AddSeconds(6 * i);
                dr.Dealt(at, id, "Greydwarf", "$enemy_greydwarf", "BlackForest", new HitData.DamageTypes { m_slash = 50 });
                if (i == 0) dr.Received(at.AddSeconds(1), id, "Greydwarf", "$enemy_greydwarf", "BlackForest", new HitData.DamageTypes { m_blunt = 20 });
                if (i >= 3) { de.Dealt(at.AddSeconds(1), id, "Greydwarf", "$enemy_greydwarf", "BlackForest", new HitData.DamageTypes { m_pierce = 40 }); de.Killed(at.AddSeconds(2), "$enemy_greydwarf", 2); de.Destroyed(at.AddSeconds(2.5), id); }
                if (i == 4) de.Received(at.AddSeconds(2), id, "Greydwarf", "$enemy_greydwarf", "BlackForest", new HitData.DamageTypes { m_blunt = 15 });
                dr.Killed(at.AddSeconds(2), "$enemy_greydwarf", 2); dr.Destroyed(at.AddSeconds(2.5), id);
            }
            dr.Tick(now); de.Tick(now);
            var feed = FeedShare.From(de, _ => true);
            var viewer = PanelSample.Full(now);
            viewer.Fights = FightAdapter.Build(new List<FightSide> { FightSide.Of(dr, viewer.PlayerName), FightSide.Of(feed, Edda, "k-" + Edda, now) }, null, null, now);
            var hers = FightsSample.Friend(viewer, Edda); hers.SharedFeed = feed;
            var a = PanelModel.Build(viewer, At(PanelModel.LastFightPage)); var b = PanelModel.Build(hers, AsFriend(PanelModel.LastFightPage));
            var typesA = PanelModel.Content(a).FirstOrDefault(x => x.Kind == "composition"); var typesB = PanelModel.Content(b).FirstOrDefault(x => x.Kind == "composition");
            Check(Head(a) != null && Head(b) != null && Head(a).Text == "all 6 foes defeated" && Head(b).Text == Head(a).Text && Nums(a).Take(2).SequenceEqual(Nums(b).Take(2)) &&
                  typesA != null && typesB != null && Math.Abs(Of(typesA, null).Sum(x => V(x.Value)) - V(Nums(a)[0])) < 0.5 && Of(typesA, null).Select(x => x.Value).SequenceEqual(Of(typesB, null).Select(x => x.Value)) && typesA.Title == "By type, together" && typesA.Value == Nums(a)[0],
                  "FP 0.8.6 the same fight reads the same on both books: \"" + Head(a)?.Text + "\" and \"" + Head(b)?.Text + "\", dealt and received " + string.Join(" / ", Nums(a).Take(2)) + " and " + string.Join(" / ", Nums(b).Take(2)) +
                  ", dealt by type " + string.Join(" + ", Of(typesA, null).Select(x => x.Value)) + " on both");
        }

        // ---------- the panel and the preview draw the fight pages with one set of measures ----------
        // every Ft* measure of Chapters/FightUi.cs is the preview's FT value of the same name (FtListLine1 = FT.listLine1), and the preview has
        // no measure of its own beyond the damage bar's (track, barH: PanelModel.DamageTrack and the bar with its true line)
        var src = AppContext.BaseDirectory;
        while (src != null && !System.IO.File.Exists(System.IO.Path.Combine(src, "src", "Plugin.cs"))) src = System.IO.Path.GetDirectoryName(src);
        var cs = src == null ? "" : System.IO.File.ReadAllText(System.IO.Path.Combine(src, "src", "Panel", "Chapters", "FightUi.cs"));
        var html = src == null ? "" : System.IO.File.ReadAllText(System.IO.Path.Combine(src, "src", "Panel", "preview", "panel-preview.html"));
        var csFt = System.Text.RegularExpressions.Regex.Matches(cs, @"\bFt(\w+) = (\d+(?:\.\d+)?)f?\b").Cast<System.Text.RegularExpressions.Match>()
                     .ToDictionary(m => char.ToLowerInvariant(m.Groups[1].Value[0]) + m.Groups[1].Value.Substring(1), m => double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        var ftObj = System.Text.RegularExpressions.Regex.Match(html, @"const FT = \{([^}]*)\}").Groups[1].Value;
        var htmlFt = System.Text.RegularExpressions.Regex.Matches(ftObj, @"(\w+): (\d+(?:\.\d+)?)").Cast<System.Text.RegularExpressions.Match>()
                       .ToDictionary(m => m.Groups[1].Value, m => double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        var off = csFt.Where(kv => !htmlFt.TryGetValue(kv.Key, out var h) || h != kv.Value).Select(kv => kv.Key + " " + kv.Value + " vs " + (htmlFt.TryGetValue(kv.Key, out var h2) ? h2.ToString() : "none"))
                      .Concat(htmlFt.Keys.Where(k => !csFt.ContainsKey(k) && k != "track" && k != "barH").Select(k => "the preview's own " + k)).ToList();
        Check(csFt.Count >= 40 && off.Count == 0 && htmlFt["track"] == PanelModel.DamageTrack && htmlFt["barH"] == PanelModel.DamageTrack + PanelModel.DamageInner + PanelModel.DamageTrue,
              "FP one set of measures: the " + csFt.Count + " Ft measures of FightUi.cs are the preview's FT" + (off.Count > 0 ? ": " + string.Join(", ", off) : ""));

        // ---------- 0.8.5 (the lead, after v085-lastfight): a player without numbers gets the line that is true of them ----------
        // The fight adapter lists without numbers only a player who shares but whose copy holds nothing of the fight; the row said "does not share"
        // of them. Edda shares in the sample; her copy is changed one way at a time, each giving its own line; Ylva does not share
        {
            var fight = new FightView { Id = "fight:t", Kind = FightKind.Group, StartUtc = now.AddMinutes(-6), EndUtc = now.AddMinutes(-4) };
            fight.Players.Add(new FightPlayer { Name = "Rowan", You = true }); fight.Players.Add(new FightPlayer { Name = "Edda", Has = false, Shares = true });
            var eddaRow = fight.Players[1];
            var self = PanelSample.Full(now);
            FeedShare Feed(DateTime t0, DateTime fightAt, int leftOut)
            {
                var feed = new FeedShare { T0 = t0, LeftOut = leftOut }; var ff = new FoeFight { Number = 1, StartUtc = fightAt, EndUtc = fightAt.AddSeconds(30) };
                ff.Entries.Add(new FoeEncounter { Id = new FoeId(7, 1), Kind = "Boar", StartUtc = fightAt, EndUtc = fightAt.AddSeconds(30) }); feed.Fights.Add(ff);
                return feed;
            }
            string Line(Action<PanelInput> change, PanelInput book = null, FightPlayer who = null)
            {
                var b = (book ?? self).ShallowCopy(); var e = b.Fellows.First(x => x.PlayerName == "Edda").ShallowCopy();
                e.ReceivedUtc = now.AddSeconds(-5); e.SharedFeed = Feed(now.AddHours(-2), now.AddMinutes(-30), 0); change?.Invoke(e);
                b.Fellows = b.Fellows.Select(x => x.PlayerName == "Edda" ? e : x).ToList();
                return PanelModel.NoPartLine(b, fight, who ?? eddaRow);
            }
            var lines = new[]
            {
                ("covers the fight, no hit on its foes", Line(null), PanelModel.NoHitLine),
                ("Hearthwoven 0.8.2", Line(e => e.ModVersion = "0.8.2"), "Edda's Hearthwoven is 0.8.2: Edda's part fills in once they update."),
                ("no feed (0.6.5)", Line(e => { e.ModVersion = "0.6.5"; e.SharedFeed = null; }), "Edda's Hearthwoven is 0.6.5: Edda's part fills in once they update."),
                ("copy from before the fight", Line(e => e.ReceivedUtc = now.AddMinutes(-7)), "shares, but Edda's newest copy came in before this fight"),
                ("no fight yet", Line(e => e.SharedFeed = new FeedShare()), "shares, but Edda's copy holds no fight yet"),
                ("fight dropped", Line(e => e.SharedFeed = Feed(now.AddHours(-2), now.AddMinutes(-2), 40)), "shares, but Edda's copy no longer holds this fight"),
                ("reconnected after it", Line(e => e.SharedFeed = Feed(now.AddMinutes(-2), now.AddMinutes(-2), 0)), "shares, but Edda's copy starts at " + self.ToLocal(now.AddMinutes(-2)).ToString("HH:mm") + ", after this fight"),
                ("kept from an earlier session", Line(null, ((Func<PanelInput>)(() => { var k = self.ShallowCopy(); k.SessionStartUtc = now.AddMinutes(-1); return k; }))()), "shares, but Edda's copy held nothing of this fight when it was kept"),
                ("Ylva, who does not share", Line(null, null, new FightPlayer { Name = "Ylva", Has = false }), PanelModel.NotSharingLine),
                ("Ylva on a fight of an earlier session", Line(null, ((Func<PanelInput>)(() => { var k = self.ShallowCopy(); k.SessionStartUtc = now.AddMinutes(-1); return k; }))(), new FightPlayer { Name = "Ylva", Has = false }), PanelModel.NotSharingLine),
                ("Bryn, sharing, no copy here now", Line(null, null, new FightPlayer { Name = "Bryn", Has = false, Shares = true }), "shares, but no copy of Bryn's is on this PC now"),
            };
            var wrong = lines.Where(l => l.Item2 != l.Item3).Select(l => l.Item1 + ": \"" + l.Item2 + "\"").ToList();
            var table = PanelModel.FightPlayers(self, fight).Items.FirstOrDefault(i => i.Kind == "quiet" && i.Id == "Edda")?.Text;
            Check(wrong.Count == 0 && table == PanelModel.NoPartLine(self, fight, eddaRow) && table != PanelModel.NotSharingLine,
                  "FP 0.8.5 a player without numbers: " + lines.Length + " cases each with its own true line (" + string.Join("; ", lines.Take(3).Select(l => l.Item2)) + " ...), the players table uses it" +
                  (wrong.Count > 0 ? "; wrong: " + string.Join("; ", wrong) : ""));
        }
        // ---------- 0.8.6 (Joost on a run page: "How does one get back to the previous view? There should be a 'back' button") ----------
        // Walked as the book walks (Build, then Visited): the Back button shows exactly on a page opened from another (a row, a link) while
        // Backspace leads back to that page, its press is Backspace's (PanelModel.Back, which the button calls) and lands there; a page reached by
        // the list, a chapter key or Back itself has none, and the strip that holds it is there whenever it shows
        {
            var s = new PanelState { Chapter = Chapter.Battle }; PanelModel.ForgetHistory(s);
            string Where() => s.Chapter + "/" + s.PageOf(s.Chapter);
            var walk = new List<string>(); var wrong = new List<string>(); PanelView v = null;
            void Show(string how, string backTo)   // backTo: the page the button leads to, null = no button
            {
                v = PanelModel.Build(live, s); PanelModel.Visited(s, v.Active, v.Page);
                var here = Where(); walk.Add(how + " " + here + (v.Back != null ? " [Back]" : ""));
                if ((v.Back != null) != (backTo != null)) { wrong.Add(how + " " + here + (v.Back != null ? " shows the button" : " has no button")); return; }
                if (v.Back == null) return;
                var to = s.History.LastOrDefault(); var strip = PanelModel.PlateOf(v)?.Items?.FirstOrDefault()?.Kind == "featband";
                if (to == null || to.Chapter + "/" + to.Page != backTo) wrong.Add(how + " " + here + ": Backspace leads to " + (to == null ? "nowhere" : to.Chapter + "/" + to.Page) + ", not " + backTo);
                if (!strip) wrong.Add(how + " " + here + ": the button with no strip to hold it");
            }
            void Press(string backTo) { var went = PanelModel.Back(s); if (!went || Where() != backTo) wrong.Add("Back from the button went to " + Where() + ", not " + backTo); }
            string RowTo(string page) => PanelModel.Content(v).SelectMany(All).FirstOrDefault(b => b.Kind == "fight" && b.Id != null && b.Id.EndsWith(page))?.Id;
            s.Page[Chapter.Battle] = PanelModel.RunsPage; Show("list:", null);
            const string Runs = "Battle/" + PanelModel.RunsPage;
            PanelModel.Follow(s, RowTo("fight:mine-1")); Show("row:", Runs);
            s.View[PanelModel.FightLayoutKey] = PanelModel.FightStacked; Show("view:", Runs); s.View.Remove(PanelModel.FightLayoutKey);
            Press(Runs); Show("back:", null);
            PanelModel.Follow(s, RowTo("fight:bonemass-1")); Show("row:", Runs);
            PanelModel.StepChapter(s, 1); Show("tab:", null);
            PanelModel.StepChapter(s, -1); Show("tab:", null);   // the fight's page again, by a key: the list is its way back
            s.Page[Chapter.Battle] = PanelModel.RunsPage; Show("list:", null);
            PanelModel.Follow(s, RowTo("fight:mine-1")); Show("row:", Runs);
            PanelModel.Follow(s, "Battle/foes"); Show("link:", "Battle/fight:mine-1");
            Press("Battle/fight:mine-1"); Show("back:", Runs);   // back on a page that was itself opened: its button leads on back
            Press(Runs); Show("back:", null);
            Check(wrong.Count == 0 && walk.Count(w => w.EndsWith("[Back]")) == 6,
                  "FP 0.8.6 the Back button shows exactly where a page was opened and Backspace leads back to it, and its press lands there: " + string.Join(", ", walk) +
                  (wrong.Count > 0 ? "; wrong: " + string.Join("; ", wrong) : ""));
        }
        return fails;
    }
}
