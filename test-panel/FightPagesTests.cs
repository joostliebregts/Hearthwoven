// 0.9 fight pages (src/Panel/Chapters/FightPages.cs, FightView.cs) on the fight sample (PanelSampleFights.cs): the numbers of a page add up every way
// they can be read (foe rows by player, by type, the players' table, the hero), the outcome words are the ones the book can know, Runs and bosses
// nests a boss fight under its run and every row opens a page, the "bring next time" line names only a drink the game has, and Last fight's
// entry turns into "Boss fight" while one is on.
using System;
using System.Collections.Generic;
using System.Linq;
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
        Check(words.SequenceEqual(new[] { "Live, 64 % left", "Reset at 41 %", "Got away at 58 %", "Won", "Out, 4 deaths" }) && !everyPage.Any(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"\blost\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)),
              "FP outcome words: " + string.Join(" / ", words) + "; \"lost\" nowhere on the fight pages (" + everyPage.Count + " strings)");

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
              !earlier.Any(r => r.Id.EndsWith("citadel-now") || r.Id.EndsWith("queen-2") || r.Id.EndsWith("camp")) && opened.All(o => o.ok) && runs.Blocks[0].Pill == "3 runs, 3 boss fights",
              "FP Runs and bosses: the Infested Citadel run on now on top (› to its page, 18:05), The Queen live under it with her lane and the 3 foes around her, then " + string.Join(", ", earlier.Select(r => r.Title)) + " newest first, the earlier Queen nested under its Citadel run; each of " +
              opened.Count + " rows opens its page with its list entry lit and Backspace returns" + (opened.Any(o => !o.ok) ? ": " + string.Join(", ", opened.Where(o => !o.ok).Select(o => o.t)) : ""));

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
        string Note(PanelInput book, string page) => PanelModel.Build(book, At(page)).StripNote;   // the strip's quiet slot
        var onQueen = Note(live, PanelModel.LastFightPage); var onCamp = Note(after, PanelModel.LastFightPage); var onMine = Note(live, "fight:mine-1");
        Check(b1 == "Poison was 62 % of what hit you: bring Poison resistance mead." && b2 == "Fire was 50 % of what hit you: bring Fire resistance barley wine." &&
              b3 == "Slash was 70 % of what hit Edda: bring a shield." && b4 == null && b5 == null && b6 == "Blunt and slash were 75 % of what hit you: bring a shield." &&
              b7 == "Frost was 100 % of what hit you: bring Frost resistance mead." && b8 == "Blunt was 26 % of what hit you: bring a shield." && b9 == null && b10 == null &&
              onQueen == "Poison was 62 % of what hit you: bring Poison resistance mead." && onCamp == "Blunt was 68 % of what hit you: bring a shield." && onMine == "Poison was 37 % of what hit you: bring Poison resistance mead.",
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
        return fails;
    }
}
