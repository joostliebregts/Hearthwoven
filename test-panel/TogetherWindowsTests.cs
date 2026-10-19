// 0.8.5 (v085-together; Joost in game on Company > Together with three fellows: "why no time filters on the other tabs?"): every Together category
// in the time windows its data can answer, for you and your fellows. Wood, stone and ore, pieces and dishes read each player's Deeds page in the
// window (your minute log and day history, a fellow's trail and shared days), so Together, Everyone and each player's own book tell one number;
// a fellow on an older Hearthwoven is left out of the day sums with its one line; sailed and cargo come from each player's minutes and shared
// voyage days, and a copy without those days (the 0.8.5 test build) is left out of their day sums, never read as "sailed nothing"; a fellow's own
// Sailing and Cargo (v085-fellowvoy) read the copy their Everyone row reads, or keep the day chips greyed with the older-copy line. Fictional sample world.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class TogetherWindowsTests
{
    const string CatKey = "Company/together/category", WinKey = "Company/together/window";
    static readonly (string cat, string page)[] Deeds = { ("wood", "woodcutting"), ("ore", "mining"), ("built", "building"), ("cooked", "cooking") };

    static PanelState Together(string cat, TimeWindow w)
    {
        var s = new PanelState { Chapter = Chapter.Company };
        s.Page[Chapter.Company] = "together"; s.View[CatKey] = cat; s.View[WinKey] = w.ToString();
        return s;
    }

    static PanelState DeedsPage(string page, TimeWindow w, bool everyone = false)
    {
        var s = new PanelState { Chapter = Chapter.Deeds, Window = w, WindowPicked = true, Everyone = everyone };
        s.Page[Chapter.Deeds] = page;
        return s;
    }

    static Block TogetherOf(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "together");
    static double Num(string shown) => string.IsNullOrEmpty(shown) || shown == PanelModel.LessThanOne ? 0 : PanelModel.ParseCount(shown);

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var (owns, _) = SampleWorld.Make(now);
        var world = owns[SampleWorld.Rowan];

        // ---------- the window's sum is the players' numbers of that window, as their Deeds pages tell them ----------
        var off = new List<string>(); int counted = 0;
        foreach (var w in new[] { TimeWindow.Today, TimeWindow.SevenDays, TimeWindow.Session, TimeWindow.LastHour })
            foreach (var (cat, page) in Deeds)
            {
                var v = PanelModel.Build(world, Together(cat, w));
                var t = TogetherOf(v); var c = t?.Items.FirstOrDefault(i => i.Kind == "category" && i.Id == cat);
                var shown = t?.Items.FirstOrDefault(i => i.Kind == "switch")?.Items.FirstOrDefault(x => x.Selected)?.Id;
                if (c == null || shown != w.ToString() || t.Tone != PanelModel.WindowedTone) { off.Add(cat + " " + w + ": shows " + shown); continue; }
                if (Math.Abs(Num(c.Value) - c.Items.Sum(p => Num(p.Value))) > 0.5 * c.Items.Count) off.Add(cat + " " + w + ": total " + c.Value + " vs parts " + string.Join("+", c.Items.Select(p => p.Value)));
                // each player's part: the row Everyone's view of their Deeds page gives in the same window (yours: your own page), and in Today their own book
                var group = PanelModel.Build(world, DeedsPage(page, w, everyone: true));
                var rows = PanelModel.Content(group).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>();
                foreach (var p in c.Items)
                {
                    var row = rows.FirstOrDefault(r => r.Id == p.Id);
                    double mine = Num(p.Value), theirs = row?.Kind == PanelModel.GroupMemberKind ? Num(row.Value) : 0;
                    if (row == null || Math.Abs(mine - theirs) > 0.5) { off.Add(cat + " " + w + " " + p.Id + ": Together " + p.Value + ", Everyone " + (row?.Value ?? row?.Text)); continue; }
                    if (w == TimeWindow.Today && owns.TryGetValue(p.Id, out var book) && Math.Abs(DeedsDaysPanelTests.Number(PanelModel.Build(book, DeedsPage(page, w)), page) - mine) > 0.5)
                        off.Add(cat + " Today " + p.Id + ": Together " + p.Value + ", own book " + DeedsDaysPanelTests.Number(PanelModel.Build(book, DeedsPage(page, w)), page));
                    else if (mine > 0) counted++;
                }
                if (Math.Abs(c.Items.Sum(p => p.Fraction) - (Num(c.Value) > 0 ? 1 : 0)) > 1e-3) off.Add(cat + " " + w + ": shares add to " + c.Items.Sum(p => p.Fraction));
            }
        // a fellow's book read on this PC (their Together: they first, you among the others) adds up the same way in every window
        foreach (var (who, book) in FullDump.Books(world).Where(b => b.who != SampleWorld.Rowan))
            foreach (var w in PanelModel.AllWindows)
                foreach (var (cat, _) in Deeds)
                {
                    var st = Together(cat, w); st.Player = who;
                    var c = TogetherOf(PanelModel.Build(book, st))?.Items.FirstOrDefault(i => i.Kind == "category" && i.Id == cat);
                    if (c != null && Math.Abs(Num(c.Value) - c.Items.Sum(p => Num(p.Value))) > 0.5 * c.Items.Count) off.Add(who + "'s book " + cat + " " + w + ": total " + c.Value + " vs parts " + string.Join("+", c.Items.Select(p => p.Value)));
                }
        Check(off.Count == 0 && counted >= 30,
              "together windows: wood, stone and ore, pieces and dishes in Today, 7 days, Session and 1 h are the sum of each player's number of that window, the one Everyone's row and (Today) " +
              "their own book's Deeds page give, and the share row is of that sum (" + counted + " parts with numbers); a fellow's book adds up in every window" + (off.Count > 0 ? ": " + string.Join("; ", off.Take(6)) : ""));

        // ---------- a fellow on 0.8.3 (no Deeds days): out of the day sums, with the older-version line ----------
        var torNow = world.Fellows.First(f => f.PlayerName == SampleWorld.Tor);
        var torOld = SampleWorld.Share(owns[SampleWorld.Tor], now, "0.8.3");
        torOld.Trail = torNow.Trail; torOld.Timed = torNow.Timed; torOld.ServerLive = torNow.ServerLive; torOld.ReceivedUtc = torNow.ReceivedUtc; torOld.OnThisSession = torNow.OnThisSession;
        torOld.ViewerSessionStartUtc = torNow.ViewerSessionStartUtc; torOld.ViewerName = torNow.ViewerName; torOld.PlayerNames = torNow.PlayerNames; torOld.Fellows = torNow.Fellows; torOld.NowUtc = torNow.NowUtc;
        var mixed = world.ShallowCopy(); mixed.Fellows = world.Fellows.Select(f => f == torNow ? torOld : f).ToList();
        var line = "Tor's Hearthwoven is 0.8.3: their days fill in once they update.";
        var oldOff = new List<string>();
        foreach (var (cat, _) in Deeds)
        {
            var v = PanelModel.Build(mixed, Together(cat, TimeWindow.Today)); var c = TogetherOf(v).Items.First(i => i.Kind == "category" && i.Id == cat);
            var tor = c.Items.First(p => p.Id == SampleWorld.Tor); var notes = PanelModel.Content(v).Where(b => b.Kind == "note").Select(b => b.Text).ToList();
            var withTor = TogetherOf(PanelModel.Build(world, Together(cat, TimeWindow.Today))).Items.First(i => i.Kind == "category" && i.Id == cat);
            if (tor.Value != "" || tor.Fraction != 0 || notes.Count(n => n == line) != 1 || Math.Abs(Num(c.Value) - c.Items.Sum(p => Num(p.Value))) > 0.5 * c.Items.Count ||
                Math.Abs(Num(withTor.Value) - Num(c.Value) - Num(withTor.Items.First(p => p.Id == SampleWorld.Tor).Value)) > 0.5 * c.Items.Count)
                oldOff.Add(cat + ": Tor " + tor.Value + " (" + tor.Fraction + "), total " + c.Value + ", notes " + string.Join(" | ", notes));
        }
        Block TorIn(PanelInput book, TimeWindow w) => TogetherOf(PanelModel.Build(book, Together("wood", w))).Items.First(i => i.Id == "wood").Items.First(p => p.Id == SampleWorld.Tor);
        Check(oldOff.Count == 0 && torOld.DayDeeds == null && TorIn(mixed, TimeWindow.Session).Value == TorIn(world, TimeWindow.Session).Value,
              "together older: Tor on 0.8.3 shares no Deeds days, so Today leaves him out of every Deeds category's sum (his part empty, the total the others') and says \"" + line + "\" once; " +
              "his Session, from what reached this PC, is as before" + (oldOff.Count > 0 ? ": " + string.Join("; ", oldOff) : ""));

        // ---------- sailed and cargo in every window: a fellow's days from their voyage family, a copy without it out of the day sums ----------
        double Km(string shown) => string.IsNullOrEmpty(shown) ? 0 : double.Parse(shown.Replace(" km", "").Replace("\u00A0", "").Replace(" ", ""), System.Globalization.CultureInfo.InvariantCulture);
        var voyOff = new List<string>(); int voyParts = 0, groupRows = 0, fellowPages = 0;
        // 0.8.5 review: Edda carried 30 kinds of items today, past the 24 parts a shared day keeps per family: her rest comes as one part, so her
        // number here is still her own book's
        var eddaOwn = owns[SampleWorld.Edda]; var eddaDay = eddaOwn.History.Rows.Single(r => r.IsDay && r.Start == PanelModel.LocalToday(eddaOwn));
        for (int k = 0; k < 30; k++) eddaDay.Events.CargoMeters["$item_kind" + k] = 400f + 37f * k;
        var eddaWas = world.Fellows.First(f => f.PlayerName == SampleWorld.Edda); var eddaWide = SampleWorld.Share(eddaOwn, now);
        eddaWide.Trail = eddaWas.Trail; eddaWide.Timed = eddaWas.Timed; eddaWide.ServerLive = eddaWas.ServerLive; eddaWide.ReceivedUtc = eddaWas.ReceivedUtc; eddaWide.OnThisSession = eddaWas.OnThisSession;
        eddaWide.ViewerSessionStartUtc = eddaWas.ViewerSessionStartUtc; eddaWide.ViewerName = eddaWas.ViewerName; eddaWide.PlayerNames = eddaWas.PlayerNames; eddaWide.Fellows = eddaWas.Fellows; eddaWide.NowUtc = eddaWas.NowUtc;
        world.Fellows = world.Fellows.Select(f => f == eddaWas ? eddaWide : f).ToList();
        var eddaRest = eddaWide.DayDeeds.TryGetValue(DayHistory.DayKey(PanelModel.LocalToday(eddaOwn)), out var eddaToday) && eddaToday.TryGetValue(PanelModel.DdCargo, out var eddaCargo) &&
                       eddaCargo.Count == PanelModel.DayDeedsParts + 1 && eddaCargo.ContainsKey(PanelModel.DdCargoRest);
        var books = FullDump.Books(world).ToDictionary(b => b.who, b => b.book);   // Edda, Finch and Tor as Rowan's player chips open them
        Block HeroOf(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "hero");
        foreach (var w in new[] { TimeWindow.Today, TimeWindow.SevenDays })
            foreach (var cat in new[] { "sailed", "cargo" })
            {
                var t = TogetherOf(PanelModel.Build(world, Together(cat, w))); var c = t.Items.First(i => i.Kind == "category" && i.Id == cat);
                if (t.Items.First(i => i.Kind == "switch").Items.Single(x => x.Selected).Id != w.ToString()) { voyOff.Add(cat + " " + w + " not shown"); continue; }
                // each player's own book in the same window: Voyages' own copy of their day history (InWindow), km sailed and the cargo carried
                var want = c.Items.ToDictionary(p => p.Id, p => { var own = PanelModel.InWindow(owns[p.Id], w); return own == null ? 0 : cat == "sailed" ? (own.Character.TryGetValue("DistanceSail", out var m) ? m : 0) : PanelModel.CargoItemMetres(own); });
                var sum = want.Values.Sum();
                foreach (var p in c.Items)
                {
                    var ok = Math.Abs(p.Fraction - (sum > 0 ? want[p.Id] / sum : 0)) < 2e-3 &&
                             (cat == "sailed" ? Math.Abs(Km(p.Value) - Math.Round(want[p.Id] / 1000, 1)) < 0.051 : p.Value == (want[p.Id] > 0 ? PanelModel.CargoText(want[p.Id], sum) : ""));
                    if (!ok) voyOff.Add(cat + " " + w + " " + p.Id + ": Together " + p.Value + " (" + p.Fraction.ToString("0.000") + "), own book " + want[p.Id]);
                    else if (want[p.Id] > 0 && p.Id != SampleWorld.Rowan) voyParts++;
                }
                // Everyone on Voyages > Sailing and Cargo (0.8.5): the same window, each row that player's own book, the group's day line once
                var gs = new PanelState { Chapter = Chapter.Voyages, Window = w, WindowPicked = true, Everyone = true }; gs.Page[Chapter.Voyages] = cat == "sailed" ? "sailing" : "cargo";
                var g = PanelModel.Build(world, gs); var gText = PanelModel.AllText(g).ToList();
                var rows = PanelModel.Content(g).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>();
                if (g.ShownWindow != w || rows.Count != want.Count || gText.Contains(PanelModel.GroupDaysLine) || gText.Count(x => x.StartsWith(PanelModel.DayWindowLine(w), StringComparison.Ordinal)) != 1) { voyOff.Add("Everyone " + gs.Page[Chapter.Voyages] + " " + w + ": shows " + g.ShownWindow + ", " + rows.Count + " rows"); continue; }
                foreach (var r in rows)
                {
                    var member = r.Kind == PanelModel.GroupMemberKind;
                    var ok = want[r.Id] > 0 ? member && (cat == "sailed" ? Math.Abs(Km(r.Value) - Math.Round(want[r.Id] / 1000, 1)) < 0.051 : r.Value == PanelModel.CargoNumber(want[r.Id], sum)) : !member;
                    if (!ok) voyOff.Add("Everyone " + cat + " " + w + " " + r.Id + ": row " + (r.Value ?? r.Text) + ", own book " + want[r.Id]);
                    else if (member && r.Id != SampleWorld.Rowan) groupRows++;
                    // the fellow's own Sailing or Cargo (their book from the player chips, Everyone off) in the same window: the copy their row reads,
                    // so the number their row shows (cargo in its own unit), and one paragraph: what their days hold (with numbers) and their whole day
                    if (r.Id == SampleWorld.Rowan) continue;
                    var fs = new PanelState { Chapter = Chapter.Voyages, Window = w, WindowPicked = true, Player = r.Id }; fs.Page[Chapter.Voyages] = gs.Page[Chapter.Voyages];
                    var fv = PanelModel.Build(books[r.Id], fs); var fText = PanelModel.AllText(fv).ToList(); var hero = HeroOf(fv);
                    var same = want[r.Id] <= 0 ? string.IsNullOrEmpty(hero?.Value)
                             : cat == "sailed" ? hero?.Title == "km sailed" && member && Math.Abs(Km(hero.Value) - Km(r.Value)) < 1e-9
                             : hero != null && hero.Value + " " + hero.Title == PanelModel.CargoText(want[r.Id], want[r.Id]);
                    var para = (want[r.Id] > 0 ? PanelModel.DeedsSharedLine(books[r.Id], gs.Page[Chapter.Voyages]) + " " : "") + PanelModel.DayWindowLine(w, r.Id);
                    if (fv.ShownWindow != w || !same || fText.Count(x => x == para) != 1 || fText.Count(x => x.Contains(PanelModel.DayWindowLine(w, r.Id))) != 1 ||
                        fText.Contains(PanelModel.NoDaysServerBook))
                        voyOff.Add(r.Id + "'s book " + gs.Page[Chapter.Voyages] + " " + w + ": shows " + fv.ShownWindow + ", " + hero?.Value + " " + hero?.Title + ", Everyone row " + (r.Value ?? r.Text));
                    else if (want[r.Id] > 0) fellowPages++;
                }
            }
        // minutes: your DeedLog and a fellow's trail now carry km sailed and the cargo total (two numbers a minute while sailing)
        var minutes = new[] { TimeWindow.Session, TimeWindow.LastHour }.SelectMany(w => new[] { "sailed", "cargo" }.Select(cat => (w, cat, t: TogetherOf(PanelModel.Build(world, Together(cat, w)))))).ToList();
        var minutesOk = minutes.All(m => m.t.Tone == PanelModel.WindowedTone && m.t.Items.First(i => i.Kind == "switch").Items.Single(x => x.Selected).Id == m.w.ToString() &&
                                         m.t.Items.First(i => i.Id == m.cat).Items.Sum(p => p.Fraction) is float f && (f == 0 || Math.Abs(f - 1) < 1e-3));
        var sailSession = minutes.First(m => m.w == TimeWindow.Session && m.cat == "sailed").t.Items.First(i => i.Id == "sailed").Items.Where(p => p.Fraction > 0).Select(p => p.Id).ToList();
        // the 0.8.5 test build: its days carry no voyage family, so its sailing is left out of the day sums, never read as "sailed nothing"
        var torTest = torNow.ShallowCopy(); torTest.DayVoyages = false;
        var testBuild = world.ShallowCopy(); testBuild.Fellows = world.Fellows.Select(f => f == torNow ? torTest : f).ToList();
        var testLine = PanelModel.OlderLine(torTest, "their days fill in");
        var sailToday = PanelModel.Build(testBuild, Together("sailed", TimeWindow.Today));
        string SailOf(PanelInput book, TimeWindow w) => TogetherOf(PanelModel.Build(book, Together("sailed", w))).Items.First(i => i.Id == "sailed").Items.First(p => p.Id == SampleWorld.Tor).Value;
        var torSail = TogetherOf(sailToday).Items.First(i => i.Id == "sailed").Items.First(p => p.Id == SampleWorld.Tor);
        var evTest = new PanelState { Chapter = Chapter.Voyages, Window = TimeWindow.Today, WindowPicked = true, Everyone = true }; evTest.Page[Chapter.Voyages] = "sailing";
        var evSail = PanelModel.Build(testBuild, evTest);
        var torRow = (PanelModel.Content(evSail).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>()).FirstOrDefault(r => r.Id == SampleWorld.Tor);
        // and his own Sailing (his book, Everyone off) keeps Today, 7 days and 30 days greyed with the older-copy reason, the page his All
        PanelView TorSailing(TimeWindow w) { var s = new PanelState { Chapter = Chapter.Voyages, Window = w, WindowPicked = true, Player = SampleWorld.Tor }; s.Page[Chapter.Voyages] = "sailing"; return PanelModel.Build(torTest, s); }
        var torPage = TorSailing(TimeWindow.Today); var torWhy = PanelModel.OlderLine(torTest, "their days fill in");
        var torGreyed = torPage.ShownWindow == TimeWindow.SinceInstall && HeroOf(torPage)?.Value is string torAll && torAll == HeroOf(TorSailing(TimeWindow.SinceInstall))?.Value &&
                        PanelModel.DayWindows.All(d => torPage.Windows.FirstOrDefault(x => x.Id == d.ToString()) is Choice ch && ch.Disabled && ch.Why == torWhy);
        Check(voyOff.Count == 0 && eddaRest && voyParts >= 3 && minutesOk && sailSession.Count >= 2 && world.Fellows.All(f => f.DayVoyages) &&
              torSail.Value == "" && torSail.Fraction == 0 && PanelModel.Content(sailToday).Count(b => b.Kind == "note" && b.Text == testLine) == 1 &&
              SailOf(testBuild, TimeWindow.Session) == SailOf(world, TimeWindow.Session) && groupRows >= 3 && fellowPages >= 3 && torGreyed &&
              torRow?.Kind == PanelModel.GroupQuietKind && torRow.Text == "shares no days of this yet" + PanelModel.GroupNotInSum && PanelModel.AllText(evSail).Count(x => x == testLine) == 1,
              "together sailed and cargo (0.8.5): in Today and 7 days each player's part is their own book's km sailed and cargo of those days (" + voyParts + " fellow parts with numbers; " +
              "Edda's today holds 30 kinds of items, past the 24 a shared day keeps, the rest one part), " +
              "in Session and 1 h from the minutes (" + string.Join(", ", sailSession) + " sailed this session); Everyone on Voyages > Sailing and Cargo opens the same days, each row " +
              "that player's own book (" + groupRows + " fellow rows with numbers), and each fellow's own Sailing and Cargo from the player chips shows their row's number (" + fellowPages + " pages) with one paragraph of what their days hold and their whole day, no server-book line; " +
              "a copy whose days carry no voyages (the test build) is left out of the day sums on both pages with \"" + testLine + "\", and its own Sailing keeps the day chips greyed " +
              "with \"" + torWhy + "\"" + (voyOff.Count > 0 ? ": " + string.Join("; ", voyOff.Take(6)) : ""));
        return fails;
    }
}
