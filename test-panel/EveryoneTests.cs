// 0.8 the Everyone chip (src/Panel/Chapters/EveryoneModel.cs; work/hearthwoven-0.8/prototypes/PICKS.md section 1): the group's total is the sum
// of the players shown, each row being that player's own page; the windows the group cannot fill are greyed, with the reason on the plate.
// Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class EveryoneTests
{
    static PanelState On(Chapter c, string page, TimeWindow w = TimeWindow.SinceInstall)
    {
        var s = new PanelState { Chapter = c, Window = w, WindowPicked = true, Everyone = true };
        if (page != null) s.Page[c] = page;
        return s;
    }

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var world = PanelSample.Full(now);
        var books = FullDump.Books(world).ToDictionary(b => b.who, b => b.book);

        // 1. the group's total is the sum of the players shown: every page with a group view, on All and in a short window (where Tor, who
        // sends no live updates, is named and left out), and Edda's row is the number her own page says
        var bad = new List<string>();
        foreach (var (c, page, w) in new[] { (Chapter.Deeds, "woodcutting", TimeWindow.SinceInstall), (Chapter.Deeds, "woodcutting", TimeWindow.LastTenMinutes), (Chapter.Deeds, "mining", TimeWindow.SinceInstall),
                                            (Chapter.Deeds, "building", TimeWindow.SinceInstall), (Chapter.Deeds, "groundwork", TimeWindow.SinceInstall), (Chapter.Deeds, "crafting", TimeWindow.SinceInstall),
                                            (Chapter.Deeds, "cooking", TimeWindow.Session), (Chapter.Deeds, "farming", TimeWindow.SinceInstall), (Chapter.Deeds, "fishing", TimeWindow.SinceInstall),
                                            (Chapter.Deeds, "taming", TimeWindow.SinceInstall), (Chapter.Battle, "damage", TimeWindow.LastHour), (Chapter.Battle, "damage", TimeWindow.SevenDays),
                                            (Chapter.Battle, "defense", TimeWindow.SinceInstall), (Chapter.Voyages, "sailing", TimeWindow.SinceInstall), (Chapter.Voyages, "cargo", TimeWindow.SinceInstall),
                                            (Chapter.Voyages, "onfoot", TimeWindow.SinceInstall), (Chapter.Stores, "trader", TimeWindow.SinceInstall), (Chapter.Stores, "smelters", TimeWindow.SinceInstall) })
        {
            var v = PanelModel.Build(world, On(c, page, w));
            var all = PanelModel.Content(v);
            var rows = all.FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind);
            var hero = all.FirstOrDefault(b => b.Kind == "hero");
            var members = rows?.Items.Where(r => r.Kind == PanelModel.GroupMemberKind).ToList() ?? new List<Block>();
            double Num(string s) => s == PanelModel.LessThanOne ? 0.5 : PanelModel.ParseCount(s);
            var sum = members.Sum(r => Num(r.Value)); var shown = hero == null ? 0 : Num(hero.Value);
            // counts add up exactly; km and item-km show one decimal per row and a whole number from 100 on, so they add up within that rounding
            var slack = page == "cargo" || page == "sailing" || page == "onfoot" ? 0.5 + 0.05 * members.Count : 0.01 + 0.5 * members.Count(r => r.Value == PanelModel.LessThanOne);
            if (!v.EveryoneOn || rows == null || rows.Items.Count != 4 || members.Count == 0 || Math.Abs(sum - shown) > slack)
                bad.Add(c + "/" + page + "/" + w + ": hero " + (hero?.Value ?? "none") + ", rows " + string.Join(" + ", members.Select(r => r.Value)));
            if (members.Sum(r => PanelModel.ParseCount((r.Value2 ?? "").Replace("%", ""))) is double pct && members.Count > 0 && Math.Abs(pct - 100) > 0.01 && !members.Any(r => r.Value2 == PanelModel.UnderOnePercent))
                bad.Add(c + "/" + page + "/" + w + ": shares add to " + pct);
        }
        var tenMin = PanelModel.Content(PanelModel.Build(world, On(Chapter.Deeds, "woodcutting", TimeWindow.LastTenMinutes))).First(b => b.Kind == PanelModel.GroupRowsKind);
        var torLeftOut = tenMin.Items.FirstOrDefault(r => r.Id == "Tor") is Block tor && tor.Kind == PanelModel.GroupQuietKind && (tor.Text ?? "").Contains("not in this sum");
        var eddaOwn = PanelModel.Content(PanelModel.Build(books["Edda"], new PanelState { Chapter = Chapter.Deeds, Player = "Edda", Window = TimeWindow.SinceInstall, Page = { [Chapter.Deeds] = "woodcutting" } }))
                                .First(b => b.Kind == "composition" && b.Title == "Wood brought in").Value;
        var eddaRow = PanelModel.Content(PanelModel.Build(world, On(Chapter.Deeds, "woodcutting"))).First(b => b.Kind == PanelModel.GroupRowsKind).Items.First(r => r.Id == "Edda").Value;
        Check(bad.Count == 0 && torLeftOut && eddaRow == eddaOwn,
              "Everyone: the group's total is the sum of the players shown on every group page (shares add to 100 %); a fellow who cannot fill the window is named and left out; Edda's row is her own page's " + eddaOwn +
              (bad.Count > 0 ? ": " + string.Join("; ", bad) : "") + (torLeftOut ? "" : " (Tor not left out in 10 min)") + (eddaRow == eddaOwn ? "" : " (Edda's row " + eddaRow + ")"));

        // REVIEW-08 #12: a fellow whose Hearthwoven shares no since-install totals adds their last shared session on All: Deeds, Voyages and the
        // Hall say so under the rows, as Battle does
        {
            var edda = books["Edda"]; var keepSince = edda.SharedSinceInstall; var line = "Edda shares no totals: their number is their last shared session.";
            try
            {
                edda.SharedSinceInstall = false;
                var said = new[] { (Chapter.Deeds, "woodcutting"), (Chapter.Voyages, "onfoot"), (Chapter.Stores, "trader") }.Select(x => PanelModel.AllText(PanelModel.Build(world, On(x.Item1, x.Item2))).Contains(line)).ToList();
                edda.SharedSinceInstall = keepSince;
                var quiet = !PanelModel.AllText(PanelModel.Build(world, On(Chapter.Stores, "trader"))).Contains(line);
                Check(said.All(x => x) && quiet, "Everyone REVIEW-08 #12: an older fellow's last-session number is said on Deeds, Voyages and the Hall on All (" + string.Join(", ", said) + "), and not for one who shares totals");
            }
            finally { edda.SharedSinceInstall = keepSince; }
        }

        // 2. greyed: on Deeds the day windows (fellows share days only for damage dealt) with the reason on the plate, and the heading keeps no
        // window phrase; Battle's Damage keeps them as your own day history allows; Skills greys the chip itself with its reason
        var wood = PanelModel.Build(world, On(Chapter.Deeds, "woodcutting", TimeWindow.LastHour));
        bool Greyed(PanelView v, TimeWindow w) => v.Windows.First(x => x.Id == w.ToString()).Disabled;
        var dayGrey = PanelModel.DayWindows.All(d => Greyed(wood, d)) && !Greyed(wood, TimeWindow.LastHour) && !Greyed(wood, TimeWindow.SinceInstall) && wood.ShownWindow == TimeWindow.LastHour
                      && (wood.WindowWhy ?? "").Contains(PanelModel.GroupDaysLine) && PanelModel.PlateOf(wood).Text == null && string.IsNullOrEmpty(wood.HeadingWindow);
        var dmg = PanelModel.Build(world, On(Chapter.Battle, "damage", TimeWindow.SevenDays));
        var dmgDays = PanelModel.DayWindows.All(d => Greyed(dmg, d) == !PanelModel.DayOpen(world, d)) && dmg.ShownWindow == (PanelModel.DayOpen(world, TimeWindow.SevenDays) ? TimeWindow.SevenDays : TimeWindow.SinceInstall);
        var skills = PanelModel.Build(world, On(Chapter.Skills, null));
        PanelModel.AddPlayers(skills, "Rowan", new[] { "Edda", "Tor", "Finch" }, "", true);
        var chipGrey = !skills.EveryoneOn && skills.EveryoneChip && !string.IsNullOrEmpty(skills.EveryoneSub) && !string.IsNullOrEmpty(skills.EveryoneWhy) && skills.Players[0].Selected;
        Check(dayGrey && dmgDays && chipGrey, "Everyone: Deeds greys Today, 7 days and 30 days with the reason on the plate (no window in the heading); Damage dealt keeps the day windows; Skills greys the chip with its reason" +
              (dayGrey ? "" : " (Deeds windows)") + (dmgDays ? "" : " (Damage days)") + (chipGrey ? "" : " (Skills chip)"));

        // 3. v08-fix-open: a window where nobody can be counted (your deeds per minute not kept yet, no fellow's copies timed) asked for the
        // names of an empty sum and threw on every build; now each row says why and there is no total
        var bare = PanelSample.Full(now); bare.Deeds = null;
        foreach (var f in bare.Fellows) { f.Trail = null; f.Timed = false; }
        PanelView empty = null; Exception threw = null;
        try { empty = PanelModel.Build(bare, On(Chapter.Deeds, "crafting", TimeWindow.LastTenMinutes)); } catch (Exception e) { threw = e; }
        var quietRows = empty == null ? null : PanelModel.Content(empty).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items;
        Check(threw == null && empty.EveryoneOn && quietRows != null && quietRows.Count > 0 && quietRows.All(r => r.Kind == PanelModel.GroupQuietKind && !string.IsNullOrEmpty(r.Text)) && !PanelModel.Content(empty).Any(b => b.Kind == "hero"),
              "Everyone: in 10 min with nobody to count, the page builds: every row says why, no total" + (threw != null ? " (threw " + threw.GetType().Name + ": " + threw.Message + ")" : ""));

        // 4. a page that throws is shown as such (PanelUi.Draw's net, CouldNotDraw.cs): here the group's Crafting with a clock that throws, as
        // Joost's book threw ArgumentNullException ("source") on Deeds > Crafting with Everyone on every open, and the book shut in the same frame
        var broken = PanelSample.Full(now); broken.ToLocal = t => throw new ArgumentNullException("source");
        var crafting = On(Chapter.Deeds, "crafting");
        Exception build = null;
        try { PanelModel.Build(broken, crafting); } catch (Exception e) { build = e; }
        var could = PanelModel.CouldNotDraw(broken, crafting);
        PanelModel.AddPlayers(could, "Rowan", new[] { "Edda", "Tor", "Finch" }, "", true);
        var only = could.Blocks.SingleOrDefault();
        var drawnAsSuch = could.Chapters.Count(c => c.Selected) == 1 && could.Chapters.First(c => c.Selected).Id == "Deeds" && could.List.Count > 1 && could.List.Single(l => l.Selected).Id == "crafting"
                    && could.Heading == "Crafting" && only?.Kind == "empty" && only.Title == PanelModel.CouldNotDrawTitle && (only.Text ?? "").Contains(PanelModel.CouldNotDrawEveryone)
                    && could.EveryoneChip && could.EveryoneOn && could.Players.Count == 4 && could.Keys.Any(k => k.EndsWith("Close")) && PanelModel.ToJson(could).Contains(PanelModel.CouldNotDrawTitle);
        Check(build is ArgumentNullException && drawnAsSuch, "a page that throws (" + (build?.GetType().Name ?? "nothing thrown") + ") is shown as could not be drawn: its tab and list entry chosen, its name, one line, the player row with Everyone on so one press leaves the group's view, the close keys");

        // 5. greyed, the chip says where it works and a press opens the nearest such page with Everyone on (Joost 2026-10-10: say where it
        // DOES work): the next page down the chapter's list, else the nearest up; from a chapter without a group view, Deeds where you left it;
        // About goes back to the page it was opened from. The press lands on your own book with the group shown.
        {
            string Press(PanelState st)
            {
                var v = PanelModel.Build(world, st);
                PanelModel.AddPlayers(v, "Rowan", new[] { "Edda", "Tor", "Finch" }, st.Player, true);
                var said = v.EveryoneSub + " / " + v.EveryoneWhy;
                if (!PanelModel.EveryoneJump(st, v)) return said + " -> nowhere";
                var after = PanelModel.Build(world, st);
                return said + " -> " + st.Chapter + "/" + st.PageOf(st.Chapter) + (after.EveryoneOn && st.Player == "" && !st.ShowAbout ? " (group)" : " (NOT the group)");
            }
            PanelState At(Chapter c, string page, Action<PanelState> more = null) { var st = new PanelState { Chapter = c, Window = TimeWindow.SinceInstall, WindowPicked = true }; if (page != null) st.Page[c] = page; more?.Invoke(st); return st; }
            var got = new[]
            {
                Press(At(Chapter.Deeds, "overview")),
                Press(At(Chapter.Battle, PanelModel.FeedPage)),
                Press(At(Chapter.Battle, "deaths", st => st.Player = "Edda")),   // 0.8.2: Foes has the group view, the next page down from Deaths
                Press(At(Chapter.Voyages, "maps")),
                Press(At(Chapter.Stores, "overview")),
                Press(At(Chapter.Skills, null, st => st.Page[Chapter.Deeds] = "woodcutting")),
                Press(At(Chapter.Battle, "damage", st => st.ShowAbout = true)),
            };
            var want = new[]
            {
                "on the other pages / Press to open Cooking for the whole group. -> Deeds/cooking (group)",
                "on Damage and more / The group view is on Damage, Defense and Foes. Press to open Damage for the whole group. -> Battle/damage (group)",
                "on Damage and more / The group view is on Damage, Defense and Foes. Press to open Foes for the whole group. -> Battle/foes (group)",
                "on Sailing and more / The group view is on Sailing, Cargo and On foot. Press to open On foot for the whole group. -> Voyages/onfoot (group)",
                "on Trader, Smelters / Press to open Trader for the whole group. -> Stores/trader (group)",
                "on Deeds and more / The group view is on Deeds, Hall, Battle and Voyages. Press to open Deeds > Woodcutting for the whole group. -> Deeds/woodcutting (group)",
                "on Deeds and more / The group view is on Deeds, Hall, Battle and Voyages. Press to open Battle > Damage for the whole group. -> Battle/damage (group)",
            };
            var alone = PanelModel.Build(world, At(Chapter.Skills, null)); PanelModel.AddPlayers(alone, "Rowan", new string[0], "", true);
            var bad3 = Enumerable.Range(0, want.Length).Where(k => got[k] != want[k]).Select(k => got[k]).ToList();
            Check(bad3.Count == 0 && !PanelModel.EveryoneJump(new PanelState(), alone) && got.All(g => !g.Contains("\u2014")),
                  "Everyone greyed: the chip says where it works, its hover what a press opens, and the press opens the nearest such page with the group shown; nobody else yet: nowhere to go" +
                  (bad3.Count == 0 ? "" : " [" + string.Join(" | ", bad3) + "]"));
        }

        // 6. release-0.8.0 (Joost's PC, Deeds > Building with Everyone): more hammer tabs than a bar shows (his mods add tabs), so a player's
        // tab bar folds its smallest into "Other (n kinds)", a part without the kinds inside it; the group's bar summed that part's kinds
        // (null) and threw ArgumentNullException ("source") in GroupComposition. Now the folded kinds are read from the filter's chips: the
        // group's bar counts every tab of every player, so its parts add up to the pieces built together
        var tabs = PanelSample.Full(now);
        Func<string, string> manyTabs = t => "Tab " + (t ?? "").Sum(ch => (int)ch) % 13;   // a mod-heavy hammer: 13 tabs
        foreach (var p in new[] { tabs }.Concat(tabs.Fellows)) p.PieceTab = manyTabs;
        PanelView built = null; Exception folded = null;
        try { built = PanelModel.Build(tabs, On(Chapter.Deeds, "building")); } catch (Exception e) { folded = e; }
        var groupBar = built == null ? null : PanelModel.Content(built).FirstOrDefault(b => b.Kind == "composition");
        double PartsTotal(Block bar) => (bar?.Items ?? new List<Block>()).Sum(x => PanelModel.ParseCount(x.Value));
        var together = built == null ? 0 : PanelModel.ParseCount(PanelModel.Content(built).First(b => b.Kind == "hero").Value);
        Check(folded == null && groupBar != null && groupBar.Items.Count > 1 && Math.Abs(PartsTotal(groupBar) - together) < 0.5,
              "Everyone: Building with 13 hammer tabs (each player's bar folds some) builds, and the group's bar counts every tab: its parts add up to the " + together + " pieces built together" +
              (folded != null ? " (threw " + folded.GetType().Name + ": " + folded.Message + ")" : groupBar == null ? " (no bar)" : " (parts " + PartsTotal(groupBar) + ")"));

        // 7. 0.8.2 (Joost in game, Battle > Foes, 1 h: "switch between my friends"): Foes with Everyone, a row per foe kind for the whole group. In
        // every window each player's parts over the foe rows add up to their row on Everyone > Damage, a foe's parts to its Dealt, and the hero is
        // Damage's; the day windows are greyed with Foes' own reason. The open foe starts with Who hit it: one scale (the top player 1, the bar's
        // zero at a quarter of the track), each row that player's part of the foe in the group's order, its types adding up to it, then Best and
        // Worst; a foe the game does not know opens to Who hit it alone. Your own book (Everyone off) has neither
        {
            double Num(string s) => s == PanelModel.LessThanOne ? 0.5 : PanelModel.ParseCount(s);
            Block Table(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "foetable");
            var bad7 = new List<string>();
            foreach (var w in new[] { TimeWindow.LastTenMinutes, TimeWindow.LastHour, TimeWindow.Session, TimeWindow.SinceInstall })
            {
                var dmgW = PanelModel.Content(PanelModel.Build(world, On(Chapter.Battle, "damage", w)));
                var foesV = PanelModel.Build(world, On(Chapter.Battle, "foes", w));
                var members = dmgW.First(b => b.Kind == PanelModel.GroupRowsKind).Items.Where(r => r.Kind == PanelModel.GroupMemberKind).ToList();
                var foeRows = Table(foesV)?.Items.Where(i => i.Kind == "foe").ToList() ?? new List<Block>();
                if (foeRows.Count == 0 || foeRows.Count >= PanelModel.FoeTop) { bad7.Add(w + ": " + foeRows.Count + " foe rows (the sample must show every foe kind for the sums to hold)"); continue; }
                foreach (var m in members)
                {
                    var parts = foeRows.SelectMany(f => f.Items.Where(i => i.Kind == "part" && i.Id == m.Id)).ToList();
                    if (Math.Abs(parts.Sum(p => Num(p.Value)) - Num(m.Value)) > 0.5 * parts.Count + 0.01) bad7.Add(w + ": " + m.Id + "'s parts " + string.Join(" + ", parts.Select(p => p.Value)) + " against Damage's " + m.Value);
                }
                foreach (var f in foeRows)
                {
                    var parts = f.Items.Where(i => i.Kind == "part").ToList();
                    if (parts.Count == 0 || Math.Abs(parts.Sum(p => Num(p.Value)) - Num(f.Value)) > 0.5 * parts.Count + 0.01) bad7.Add(w + ": " + f.Id + " " + f.Value + " split " + string.Join(" + ", parts.Select(p => p.Value)));
                }
                var heroD = dmgW.FirstOrDefault(b => b.Kind == "hero")?.Value; var heroF = PanelModel.Content(foesV).FirstOrDefault(b => b.Kind == "hero")?.Value;
                if (heroD != heroF) bad7.Add(w + ": hero " + heroF + ", Damage's " + heroD);
            }
            var days = PanelModel.Build(world, On(Chapter.Battle, "foes", TimeWindow.LastHour));
            var daysGrey = PanelModel.DayWindows.All(d => Greyed(days, d)) && (days.WindowWhy ?? "").Contains(PanelModel.GroupFoesDaysLine) && days.EveryoneOn;
            Check(bad7.Count == 0 && daysGrey, "Everyone on Foes: in 10 min, 1 h, Session and All each player's parts over the foe rows add up to their row on Everyone > Damage, each foe's parts to its Dealt, the hero is Damage's; the day windows greyed with their reason" +
                  (bad7.Count == 0 ? "" : ": " + string.Join("; ", bad7)) + (daysGrey ? "" : " (day windows)"));

            PanelState Open(string foe, bool everyone = true) { var st = On(Chapter.Battle, "foes", TimeWindow.LastHour); st.Everyone = everyone; st.View[PanelModel.FoeOpenKey] = foe; return st; }
            var top = Table(days).Items.First(i => i.Kind == "foe");
            var opened = Table(PanelModel.Build(world, Open(top.Id))).Items.First(i => i.Kind == "foe" && i.Selected);
            var rk = opened.Items.First(i => i.Kind == "ranking");
            var who = rk.Items.FirstOrDefault();
            var whoRows = who?.Items ?? new List<Block>();
            var split = opened.Items.Where(i => i.Kind == "part").ToList();
            var maxWho = whoRows.Select(r => Num(r.Value)).DefaultIfEmpty(0).Max();
            var scale = who?.Kind == PanelModel.WhoHitKind && who.Text == "in the last hour" && whoRows.Count >= 2 && whoRows.Count(r => r.Fraction == 1f) == 1 && Num(whoRows.First(r => r.Fraction == 1f).Value) == maxWho
                        && whoRows.All(r => Math.Abs(r.Fraction - Num(r.Value) / maxWho) < 0.01 && r.Icon == "person:" + r.Id && r.Colour == "person:" + r.Id)
                        && PanelModel.DamageBarWidth(1f) == 1f && PanelModel.DamageBarWidth(0f) == PanelModel.DamageFloor && PanelModel.DamageFloor == 0.25f;
            var same = whoRows.Select(r => r.Id + " " + r.Value).SequenceEqual(split.Select(p => p.Id + " " + p.Value)) && whoRows.First().Title == "You"
                       && whoRows.All(r => (r.Items ?? new List<Block>()).Where(i => i.Kind == "part").Sum(i => Num(i.Value)) == Num(r.Value));
            var thenRanking = rk.Items.Skip(1).Select(i => i.Kind + ":" + i.Id).SequenceEqual(new[] { "rankrow:best", "rankrow:worst" });
            var unknown = Table(days).Items.FirstOrDefault(i => i.Kind == "foe" && !i.Items.Any(c => c.Kind == "mod"));
            var unknownOpen = unknown == null ? null : Table(PanelModel.Build(world, Open(unknown.Id))).Items.First(i => i.Id == unknown.Id);
            var aloneWho = unknownOpen != null && unknownOpen.Tone == "opens" && unknownOpen.Selected && unknownOpen.Items.First(i => i.Kind == "ranking").Items.Select(i => i.Kind).SequenceEqual(new[] { PanelModel.WhoHitKind });
            var own = Table(PanelModel.Build(world, Open(top.Id, everyone: false)));
            var ownPlain = !own.Items.Any(f => f.Items.Any(i => i.Kind == "part")) && own.Items.Any(f => f.Selected) && !own.Items.First(f => f.Selected).Items.First(i => i.Kind == "ranking").Items.Any(i => i.Kind == PanelModel.WhoHitKind)
                           && (unknown == null || own.Items.All(f => f.Id != unknown.Id || f.Tone == null));
            Check(scale && same && thenRanking && aloneWho && ownPlain,
                  "Everyone on Foes, the open foe (" + top.Title + "): Who hit it first, one scale (the top player 1, zero at a quarter of the track), each row that player's part of the Dealt bar in the group's order, its types adding up to it, then Best and Worst; " +
                  (unknown?.Title ?? "no unknown foe") + " (no game data) opens to Who hit it alone; your own book unchanged" +
                  (scale ? "" : " (scale: " + string.Join(", ", whoRows.Select(r => r.Id + " " + r.Value + " at " + r.Fraction)) + ")") + (same ? "" : " (rows against the split: " + string.Join(", ", split.Select(p => p.Id + " " + p.Value)) + ")") +
                  (thenRanking ? "" : " (then " + string.Join(", ", rk.Items.Skip(1).Select(i => i.Kind)) + ")") + (aloneWho ? "" : " (unknown foe)") + (ownPlain ? "" : " (own book)"));
        }

        // 0.8.1 review 1, follow-up: a name and "as of 8 Oct 00:27" too long for the row: the note drops "as of " before the name gives way; a short
        // name keeps the whole note; "live" and "this PC" stay. The widths stand in for the game's font (about 11 px a letter for the 18 px bold name,
        // 6.3 for the 14 px italic note, +2 each as PanelUi measures); the room is GroupRows' 150 px. Ylvara: a test-only name, never in a preview
        float NameW(string n) => n.Length * 11f + 2;
        float NoteW(string t) => t.Length * 6.3f + 2;
        string Fit(string name, string note) => PanelModel.GroupNoteFit(note, NameW(name), NoteW, 150);
        const string asOf = "as of 8 Oct 00:27";
        Check(Fit("Tor", asOf) == asOf && Fit("Ylvara", asOf) == "8 Oct 00:27" && NameW("Ylvara") + NoteW(Fit("Ylvara", asOf)) <= 150 && NameW("Ylvara") + NoteW(asOf) > 150 &&
              Fit("Ylvarandottir", asOf) == "8 Oct 00:27" && Fit("Ylvarandottir", PanelModel.GroupLive) == PanelModel.GroupLive && Fit("Ylvarandottir", PanelModel.GroupThisPc) == PanelModel.GroupThisPc,
              "group rows: a 6-letter name beside \"as of 8 Oct 00:27\" stays whole and the note says \"8 Oct 00:27\"; \"Tor\" keeps the whole note; live and this PC stay as they are");
        return fails;
    }
}

static class EveryoneDebug
{
    public static string Text(DateTime now)
    {
        var world = PanelSample.Full(now);
        var sb = new System.Text.StringBuilder();
        foreach (var (c, page) in new[] { (Chapter.Deeds, "woodcutting"), (Chapter.Deeds, "building"), (Chapter.Battle, "damage") })
            foreach (var on in new[] { false, true })
            {
                var st = new PanelState { Chapter = c, Window = TimeWindow.SinceInstall, WindowPicked = true, Everyone = on }; st.Page[c] = page;
                PanelModel.Build(world, st);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 20; i++) PanelModel.Build(world, st);
                sb.Append("perf ").Append(c).Append('/').Append(page).Append(on ? " everyone " : " own ").Append((watch.Elapsed.TotalMilliseconds / 20).ToString("0.00")).Append(" ms per build\n");
            }
        void Out(Block b, string indent)
        {
            sb.Append(indent).Append(b.Kind).Append(": ").Append(string.Join(" | ", new[] { b.Id != null ? "#" + b.Id : null, b.Title, b.Value, b.Value2, b.Text, b.Note, b.Colour }.Where(x => !string.IsNullOrEmpty(x)))).Append('\n');
            foreach (var i in b.Items ?? new List<Block>()) Out(i, indent + "    ");
        }
        foreach (var (c, page, w) in new[] { (Chapter.Deeds, "woodcutting", TimeWindow.SinceInstall), (Chapter.Deeds, "woodcutting", TimeWindow.LastTenMinutes), (Chapter.Deeds, "mining", TimeWindow.SinceInstall),
                                            (Chapter.Deeds, "building", TimeWindow.SinceInstall), (Chapter.Deeds, "groundwork", TimeWindow.SinceInstall), (Chapter.Deeds, "crafting", TimeWindow.SinceInstall),
                                            (Chapter.Deeds, "cooking", TimeWindow.Session), (Chapter.Deeds, "farming", TimeWindow.SinceInstall), (Chapter.Deeds, "fishing", TimeWindow.SinceInstall),
                                            (Chapter.Deeds, "taming", TimeWindow.SinceInstall), (Chapter.Battle, "damage", TimeWindow.LastHour), (Chapter.Battle, "damage", TimeWindow.SevenDays),
                                            (Chapter.Battle, "defense", TimeWindow.SinceInstall), (Chapter.Voyages, "sailing", TimeWindow.SinceInstall), (Chapter.Voyages, "cargo", TimeWindow.SinceInstall),
                                            (Chapter.Voyages, "onfoot", TimeWindow.SinceInstall), (Chapter.Stores, "trader", TimeWindow.SinceInstall), (Chapter.Stores, "smelters", TimeWindow.SinceInstall),
                                            (Chapter.Battle, "foes", TimeWindow.LastHour), (Chapter.Battle, "foes", TimeWindow.SinceInstall) })
        {
            var s = new PanelState { Chapter = c, Window = w, WindowPicked = true, Everyone = true }; s.Page[c] = page;
            if (page == "foes") s.View[PanelModel.FoeOpenKey] = "Draugr";
            var v = PanelModel.Build(world, s);
            sb.Append("=== ").Append(c).Append(" / ").Append(page).Append(" / ").Append(w).Append('\n').Append("heading: ").Append(v.Heading).Append(" | window: ").Append(v.HeadingWindow).Append(" | on ").Append(v.EveryoneOn).Append('\n')
              .Append("windows: ").Append(string.Join(" ", v.Windows.Select(x => x.Label + (x.Disabled ? "(off)" : "") + (x.Selected ? "*" : "")))).Append('\n').Append("keys: ").Append(string.Join("  ", v.Keys)).Append('\n');
            foreach (var b in v.Blocks) Out(b, "  ");
        }
        return sb.ToString();
    }
}
