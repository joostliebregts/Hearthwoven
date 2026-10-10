// 0.8 Compare periods (src/Panel/Chapters/CompareModel.cs; work/hearthwoven-0.8/prototypes/PICKS.md section 3, pick B): the chip pairs a day window
// with the stretch just before it, the two periods add up to the doubled window of the day history, the chip is greyed (with the day it opens)
// until the history reaches both, a page merges into compare rows, a comparison never mixes sources, and the chip is remembered.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class CompareTests
{
    static int Hash(string s) { int h = 7; foreach (var c in s ?? "") h = h * 31 + c; return h & 0x7fffffff; }

    /// <summary>
    /// The sample world with two weeks of day history (the preview scenario "compare"): the world's own play days, and the seven days before
    /// them played too, each of those a copy of the day a week later with its amounts a little different per kind (60 to 120 %), some kinds not
    /// at all (so they read "new" now). The history then starts 13 days back, so 7 days against the 7 before works.
    /// </summary>
    public static PanelInput Scenario(DateTime now)
    {
        var p = PanelSample.Full(now);
        var h = p.History; var today = PanelModel.LocalToday(p);
        // a few kinds were not there the week before (finewood, birch: "new" now), the rest 60 to 120 % of this week's
        float Scale(string key, float v) { if (key.Contains("finewood") || key.Contains("Birch")) return 0; var k = Hash(key) % 8; return k == 0 ? 0 : (float)Math.Round(v * (0.6 + 0.6 * (k - 1) / 6.0)); }
        void Into(Dictionary<string, float> from, Dictionary<string, float> to) { foreach (var kv in from) { var v = Scale(kv.Key, kv.Value); if (v > 0) to[kv.Key] = v; } }
        var earlier = new List<DayHistory.Row>();
        foreach (var r in h.Rows.Where(r => r.IsDay && r.Start >= today.AddDays(-6)).ToList())
        {
            var day = r.Start.AddDays(-7);
            if (h.Rows.Any(o => o.IsDay && o.Start == day)) continue;
            var c = new DayHistory.Row { Period = DayHistory.DayKey(day) };
            foreach (var (name, map) in r.Events.Named().Zip(c.Events.Named(), (a, b) => (a.Key, (a.Value, b.Value)))) Into(map.Item1, map.Item2);
            c.Events.Blocks = (int)Math.Round(r.Events.Blocks * 0.8); c.Events.Parries = (int)Math.Round(r.Events.Parries * 1.2);
            Into(r.Damage.Dealt, c.Damage.Dealt); Into(r.Damage.Taken, c.Damage.Taken); c.Damage.HitsDealt = (int)(r.Damage.HitsDealt * 0.9); c.Damage.HitsTaken = (int)(r.Damage.HitsTaken * 0.9);
            Into(r.Biome.Damage, c.Biome.Damage);
            Into(r.Game, c.Game);
            earlier.Add(c);
        }
        h.Rows.InsertRange(0, earlier); h.Rows.Sort((a, b) => a.Start.CompareTo(b.Start));
        h.From = today.AddDays(-13);
        foreach (var f in p.Fellows ?? new List<PanelInput>())   // the fellows' damage dealt per day, two weeks of it, as their copies share it
        {
            if (f.DealtByDay == null) continue;
            foreach (var kv in f.DealtByDay.ToList())
                if (DateTime.TryParse(kv.Key, out var d)) { var e = DayHistory.DayKey(d.AddDays(-7)); if (!f.DealtByDay.ContainsKey(e)) f.DealtByDay[e] = (float)Math.Round(kv.Value * (0.5 + Hash(f.PlayerName) % 5 / 5.0)); }
        }
        return p;
    }

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelState Page(Chapter c, string page, TimeWindow w, bool compare = true) { var s = new PanelState { Chapter = c, Window = w, WindowPicked = true, Compare = compare }; s.Page[c] = page; return s; }

        var two = Scenario(now); var today = PanelModel.LocalToday(two);

        // the two periods add up to the doubled window of the day history (DayHistory.Sum), and the period before holds nothing of this session
        {
            float Wood(PanelInput i, TimeWindow w) { var c = PanelModel.InWindow(i, w); return c == null ? -1 : c.Events.PickedUp.TryGetValue("$item_wood", out var v) ? v : 0; }
            float Pend(PanelInput i) => i.Pending != null && i.Pending.Events.PickedUp.TryGetValue("$item_wood", out var v) ? v : 0;
            float Sum(int days) => two.History.Sum(today.AddDays(1 - days), today).Events.PickedUp.TryGetValue("$item_wood", out var v) ? v : 0;
            var b7 = PanelModel.BeforeInput(two, TimeWindow.SevenDays); var b1 = PanelModel.BeforeInput(two, TimeWindow.Today);
            Check(PanelModel.LocalToday(b7) == today.AddDays(-7) && b7.Pending == null && PanelModel.LocalToday(b1) == today.AddDays(-1),
                  "compare: the period before is the same window seen from 7 days (yesterday) back, without the running session's unsaved part");
            Check(Sum(14) > 0 && Math.Abs(Wood(two, TimeWindow.SevenDays) - Pend(two) + Wood(b7, TimeWindow.SevenDays) - Sum(14)) < 0.01 &&
                  Math.Abs(Wood(two, TimeWindow.Today) - Pend(two) + Wood(b1, TimeWindow.Today) - Sum(2)) < 0.01,
                  "compare: 7 days and the 7 before add up to the 14-day sum of the day rows; today and yesterday to the 2-day sum (wood " + Wood(two, TimeWindow.SevenDays) + " + " + Wood(b7, TimeWindow.SevenDays) + ")");
        }

        // greyed until the history reaches both periods, with the day it opens; open from then on
        {
            var young = PanelSample.Full(now); young.History.From = today.AddDays(-7);   // a history a week old: 7 days works, its pair does not yet
            var v = PanelModel.Build(young, Page(Chapter.Deeds, "woodcutting", TimeWindow.SevenDays));
            var opens = today.AddDays(-7 + 13);
            Check(v.Compare != null && v.Compare.Disabled && !v.Comparing && v.CompareWhy != null && v.CompareWhy.StartsWith("Compare opens on " + PanelModel.ShortDate(opens, today) + ".")
                  && v.CompareWhy.Contains("starts on " + PanelModel.ShortDate(today.AddDays(-7), today)) && v.CompareWhy.Contains("14 full days"),
                  "compare: before the history reaches the 7 days before, the chip is greyed and says when it opens (history.from + 13: " + v.CompareWhy + ")");
            var t = PanelModel.Build(young, Page(Chapter.Deeds, "woodcutting", TimeWindow.Today));
            Check(t.Compare != null && !t.Compare.Disabled && t.Comparing, "compare: today against yesterday opens as soon as the history holds yesterday");
            var fresh = PanelSample.Full(now); fresh.History.From = today;
            var f = PanelModel.Build(fresh, Page(Chapter.Deeds, "woodcutting", TimeWindow.Today));
            Check(f.Compare.Disabled && f.CompareWhy.StartsWith("Compare opens on " + PanelModel.ShortDate(today.AddDays(1), today) + "."), "compare: a history that began today opens Today's comparison tomorrow");
            var o = PanelModel.Build(two, Page(Chapter.Deeds, "woodcutting", TimeWindow.SevenDays));
            Check(!o.Compare.Disabled && o.Compare.Selected && o.Comparing && o.CompareWhy == null, "compare: two weeks of history: 7 days against the 7 before works");
        }

        // the windows without a pair, a fellow's book, the pages without numbers that add up
        {
            var all = PanelModel.Build(two, Page(Chapter.Deeds, "woodcutting", TimeWindow.SinceInstall));
            var hour = PanelModel.Build(two, Page(Chapter.Battle, "damage", TimeWindow.LastHour));
            Check(all.Compare.Disabled && all.CompareWhy == PanelModel.CompareNeedsDay && hour.Compare.Disabled && hour.CompareWhy == PanelModel.CompareNeedsDay && !all.Comparing,
                  "compare: All and the session's windows are greyed: Compare needs a day window");
            var month = PanelModel.Build(two, Page(Chapter.Deeds, "woodcutting", TimeWindow.ThirtyDays));
            Check(month.Compare.Disabled && !month.Comparing, "compare: 30 days against the 30 before reaches past the day rows the history keeps: greyed (" + month.CompareWhy + ")");
            var edda = FullDump.Books(PanelSample.Full(now)).First(b => b.who == "Edda").book;
            var ev = PanelModel.Build(edda, new PanelState { Chapter = Chapter.Deeds, Player = "Edda", Window = TimeWindow.SevenDays, WindowPicked = true, Compare = true, Page = { [Chapter.Deeds] = "woodcutting" } });
            Check(ev.Compare != null && ev.Compare.Disabled && ev.CompareWhy.Contains("Edda") && !ev.Comparing, "compare: a fellow's book shares no days: greyed with the reason");
            var none = new[] { (Chapter.Deeds, "overview"), (Chapter.Deeds, "recent"), (Chapter.Battle, "overview"), (Chapter.Battle, "deaths"), (Chapter.Skills, (string)null) }
                .Where(c => PanelModel.Build(two, Page(c.Item1, c.Item2, TimeWindow.SevenDays)).Compare != null).ToList();
            Check(none.Count == 0, "compare: not offered where the numbers do not add up over days (Deeds Overview and Recent, Battle Overview and Deaths, Skills)");
        }

        // the change mark: whole per cent, neutral words
        Check(PanelModel.ChangeMark(96, 86) == "▲ 12 %" && PanelModel.ChangeMark(640, 910) == "▼ 30 %" && PanelModel.ChangeMark(90, 0) == "new" && PanelModel.ChangeMark(30, 30) == "same"
              && PanelModel.ChangeMark(1001, 1000) == "▲ <1 %" && PanelModel.ChangeMark(0, 40) == "▼ 100 %",
              "compare: the change mark is round((now - before) / before x 100) %, \"new\" with nothing before, \"same\" when equal, never 0 % for a change");

        // a page compared (Woodcutting): the hero carries the number before; the wood and the trees open into rows on one scale, every part of either period
        {
            var v = PanelModel.Build(two, Page(Chapter.Deeds, "woodcutting", TimeWindow.SevenDays));
            var plate = PanelModel.PlateOf(v); var items = plate.Items;
            var hero = items.First(b => b.Kind == "hero");
            var wood = items.FirstOrDefault(b => b.Kind == PanelModel.CompareRowsKind && b.Title == "Wood brought in");
            var trees = items.FirstOrDefault(b => b.Kind == PanelModel.CompareRowsKind && b.Title == null && b.Items.Any(r => r.Id == "Beech"));
            Check(hero.Before != null && hero.Change != null && hero.BeforeLabel == "the 7 days before" && hero.Items.Where(n => n.Kind == "number").All(n => n.Before != null),
                  "compare: the hero's numbers carry the 7 days before and the change (" + hero.Value + " " + hero.Change + ", " + hero.Before + " " + hero.BeforeLabel + ")");
            Check(wood != null && trees != null && !items.Any(b => b.Kind == "composition" || b.Kind == "ranking"), "compare: every composition and ranking becomes compare rows");
            var max = wood.Items.Max(r => Math.Max(r.Fraction, r.Fraction2));
            Check(wood != null && wood.Items.All(r => r.Before != null && r.Change != null) && Math.Abs(max - 1) < 1e-4 && wood.Before != null
                  && wood.Items.All(r => Math.Abs(r.Fraction - PanelModel.Amount(r.Value) / wood.Items.Max(q => Math.Max(PanelModel.Amount(q.Value), PanelModel.Amount(q.Before)))) < 1e-3),
                  "compare: one scale per block: now and before are lengths of the block's largest number, solid and outline");
            var line = PanelModel.AboutText(v).Split('\n')[0];   // 0.8 layout D+ (PageHead.cs): About these numbers' first line, not the plate's
            Check(plate.Text == null && line.StartsWith("7 days, ") && line.Contains("against the 7 days before") && line.Contains(PanelModel.DayRange(today.AddDays(-13), today.AddDays(-7), today)),
                  "compare: About these numbers' first line names both periods (" + line + ")");
            var parts = new List<Block> { new Block { Kind = "plate", Items = new List<Block> { new Block { Kind = "composition", Title = "Wood brought in", Value = "15", Src = PanelModel.SrcPc,
                Items = new List<Block> { new Block { Id = "a", Title = "Wood", Value = "10" }, new Block { Id = "b", Title = "Finewood", Value = "5" } } } } } };
            var was = new List<Block> { new Block { Kind = "plate", Items = new List<Block> { new Block { Kind = "composition", Title = "Wood brought in", Value = "12", Src = PanelModel.SrcPc,
                Items = new List<Block> { new Block { Id = "a", Title = "Wood", Value = "8" }, new Block { Id = "c", Title = "Corewood", Value = "4" } } } } } };
            PanelModel.CompareBlocks(parts, was, TimeWindow.SevenDays);
            var cr = parts[0].Items[0];
            Check(cr.Kind == PanelModel.CompareRowsKind && cr.Items.Select(r => r.Id + ":" + r.Value + "/" + r.Before + "/" + r.Change).SequenceEqual(new[] { "a:10/8/\u25B2 25 %", "b:5/0/new", "c:0/4/\u25BC 100 %" })
                  && cr.Change == "\u25B2 25 %" && cr.Before == "12",
                  "compare: every part of either period keeps a row: one only now reads 0 before and new, one only before 0 now and \u25BC 100 %");
            var off = PanelModel.Build(two, Page(Chapter.Deeds, "woodcutting", TimeWindow.SevenDays, compare: false));
            Check(!off.Comparing && off.Compare != null && !off.Compare.Selected && !PanelModel.Content(off).Any(b => b.Kind == PanelModel.CompareRowsKind || b.Before != null),
                  "compare: off, the page is as it was, the chip offered");
        }

        // Battle > Damage compared: the damage dealt as the hero, By type and By weapon as rows per type and per weapon
        {
            var t = Page(Chapter.Battle, "damage", TimeWindow.SevenDays); t.View["Battle/damage/view"] = "type";
            var v = PanelModel.Build(two, t); var c = PanelModel.Content(v);
            var hero = c.FirstOrDefault(b => b.Kind == "hero"); var rows = c.FirstOrDefault(b => b.Kind == PanelModel.CompareRowsKind);
            var dealt = PanelModel.InWindow(two, TimeWindow.SevenDays); var was = PanelModel.InWindow(PanelModel.BeforeInput(two, TimeWindow.SevenDays), TimeWindow.SevenDays);
            double Of(PanelInput i) => i.DamageSinceInstall.Dealt.Where(kv => new[] { "blunt", "slash", "pierce", "fire", "frost", "lightning", "poison", "spirit" }.Contains(kv.Key.Substring(kv.Key.LastIndexOf('|') + 1))).Sum(kv => (double)kv.Value);
            Check(hero != null && hero.Title == "damage dealt" && PanelModel.Amount(hero.Value) == Math.Round(Of(dealt)) && PanelModel.Amount(hero.Before) == Math.Round(Of(was)),
                  "compare: Battle Damage leads with the damage dealt of both periods (" + hero?.Value + " against " + hero?.Before + ")");
            Check(rows != null && rows.Text == "By type" && rows.Items.All(r => r.Icon != null && r.Colour != null && r.Colour.StartsWith("#")), "compare: By type becomes one row per damage type, with its icon and colour");
            var w = PanelModel.Build(two, Page(Chapter.Battle, "damage", TimeWindow.SevenDays));
            var wr = PanelModel.Content(w).FirstOrDefault(b => b.Kind == PanelModel.CompareRowsKind);
            Check(wr != null && wr.Text == "By weapon" && wr.Items.Select(r => r.Id).SequenceEqual(new[] { "melee", "bow", "magic" }.Where(id => wr.Items.Any(r => r.Id == id))) && !PanelModel.Content(w).Any(b => b.Kind == "dmgmix"),
                  "compare: By weapon becomes one row per weapon");
        }

        // a comparison never mixes a game total with a count from install: a number whose source differs between the periods is left as it is
        {
            var now0 = new List<Block> { new Block { Kind = "plate", Items = new List<Block> { new Block { Kind = "hero", Value = "40", Title = "trees felled", Src = PanelModel.SrcCharacter } } } };
            var was0 = new List<Block> { new Block { Kind = "plate", Items = new List<Block> { new Block { Kind = "hero", Value = "30", Title = "trees felled", Src = PanelModel.SrcPc } } } };
            PanelModel.CompareBlocks(now0, was0, TimeWindow.SevenDays);
            var same = new List<Block> { new Block { Kind = "plate", Items = new List<Block> { new Block { Kind = "hero", Value = "40", Title = "trees felled", Src = PanelModel.SrcPc } } } };
            PanelModel.CompareBlocks(same, was0, TimeWindow.SevenDays);
            Check(now0[0].Items[0].Before == null && same[0].Items[0].Before == "30" && same[0].Items[0].Change == "▲ 33 %",
                  "compare: a game total is never set beside a count from install (one recorded total); the same source compares");
        }

        // Together > Damage dealt: the group's total and a row per player, each from their own days
        {
            var s = new PanelState { Chapter = Chapter.Company, Compare = true, Page = { [Chapter.Company] = "together" } };
            s.View["Company/together/category"] = "dealt"; s.View["Company/together/window"] = "SevenDays";
            var v = PanelModel.Build(two, s); var tg = PanelModel.TogetherOf(v);
            var sw = tg?.Items.First(i => i.Kind == "switch"); var cat = tg?.Items.First(i => i.Kind == "category" && i.Id == "dealt"); var rows = tg?.Items.FirstOrDefault(i => i.Kind == PanelModel.CompareRowsKind);
            Check(v.Compare == null && sw?.Chip != null && sw.Chip.Selected && cat.Before != null && rows != null && rows.Items.Count == cat.Items.Count(p => !string.IsNullOrEmpty(p.Value)) && rows.Items.All(r => r.Colour.StartsWith("person:")),
                  "compare: Together's Compare chip sits with its window chips; the group's damage dealt and a row per player in their colours");
            var old = Scenario(now); foreach (var f in old.Fellows.Where(f => f.PlayerName == "Finch")) f.DealtByDay = null;
            var g = PanelModel.Build(old, s); var gc = PanelModel.TogetherOf(g).Items.First(i => i.Kind == "switch").Chip;
            Check(gc != null && gc.Tone == PanelModel.OffTone && gc.Text.Contains("Finch") && !g.Comparing, "compare: Together is greyed while a fellow shares no days (" + gc?.Text + ")");
        }

        // the book redraws every 2 s: the period before is built once while nothing it reads changed, again when the page or the window does
        {
            var s = Page(Chapter.Deeds, "mining", TimeWindow.SevenDays);
            PanelModel.Build(two, s); var n0 = PanelModel.CompareBeforeBuilds;
            for (int k = 0; k < 5; k++) PanelModel.Build(two, s);
            var same = PanelModel.CompareBeforeBuilds == n0;
            s.Window = TimeWindow.Today; PanelModel.Build(two, s);
            Check(same && PanelModel.CompareBeforeBuilds == n0 + 1, "compare: the period before is built once per page and window, not on every redraw");
        }

        // every page that offers Compare, in every window, on the two weeks and on the world, with every view of its switch: no error, and a
        // compared page keeps every number the plain page shows (no block lost in the merge)
        {
            int pages = 0, compared = 0; var lost = new List<string>();
            foreach (var book in new[] { two, PanelSample.Full(now) })
                foreach (Chapter c in new[] { Chapter.Deeds, Chapter.Battle, Chapter.Voyages, Chapter.Company })
                    foreach (var l in PanelModel.Build(book, new PanelState { Chapter = c }).List)
                        foreach (var w in PanelModel.AllWindows)
                        {
                            var s = Page(c, l.Id, w); if (c == Chapter.Company) { s.View["Company/together/category"] = "dealt"; s.View["Company/together/window"] = w.ToString(); }
                            var plain = PanelModel.Build(book, Page(c, l.Id, w, compare: false));
                            var sw = PanelModel.SwitchOf(plain);
                            foreach (var view in sw == null || c == Chapter.Company ? new string[] { null } : sw.Items.Select(i => i.Id).ToArray())
                            {
                                if (view != null) s.View[sw.Id] = view;
                                var v = PanelModel.Build(book, s); pages++;
                                if (!v.Comparing) continue;
                                compared++;
                                var p = view == null ? plain : PanelModel.Build(book, ((Func<PanelState>)(() => { var q = Page(c, l.Id, w, compare: false); q.View[sw.Id] = view; return q; }))());
                                var heroes = PanelModel.Content(p).Where(b => b.Kind == "hero").Select(b => b.Value).ToList();
                                if (heroes.Any(h => !PanelModel.Content(v).Any(b => b.Kind == "hero" && b.Value == h))) lost.Add(c + "/" + l.Id + "/" + w + "/" + view);
                            }
                        }
            Check(PanelModel.CompareError == null && compared > 20 && lost.Count == 0,
                  "compare: every page that offers it, in every window and view (" + pages + " builds, " + compared + " compared): no error, and the plain page's hero numbers all stay" + (lost.Count > 0 ? " (lost: " + string.Join(", ", lost.Take(4)) + ")" : "") + (PanelModel.CompareError != null ? " (" + PanelModel.CompareError + ")" : ""));
        }

        // remembered: PanelPrefs keeps the chip; off writes the file as before
        {
            var off = new PanelState(); var on = new PanelState { Compare = true };
            var back = new PanelState(); PanelPrefs.Apply(PanelPrefs.ToJson(on), back);
            var reset = new PanelState { Compare = true }; PanelPrefs.Apply(PanelPrefs.ToJson(off), reset);
            Check(back.Compare && !reset.Compare && !PanelPrefs.ToJson(off).Contains("compare"), "compare: the chip is remembered in PanelPrefs; off adds nothing to the file");
        }

        // every word the comparison adds follows the text rules (no em-dash, no abbreviation)
        {
            var texts = new[] { (Chapter.Deeds, "woodcutting"), (Chapter.Deeds, "cooking"), (Chapter.Battle, "damage"), (Chapter.Battle, "defense"), (Chapter.Voyages, "sailing"), (Chapter.Voyages, "cargo") }
                .SelectMany(c => PanelModel.AllText(PanelModel.Build(two, Page(c.Item1, c.Item2, TimeWindow.SevenDays))).Concat(PanelModel.AllText(PanelModel.Build(two, Page(c.Item1, c.Item2, TimeWindow.LastHour))))).ToList();
            Check(!texts.Any(t => t.Contains("—") || t.Contains(" dmg") || t.Contains("vs")), "compare: its words have no em-dash and no abbreviation");
        }
        // 0.8 with the Everyone chip (PICKS.md section 3): the group's pages grey Compare with the reason, except Battle > Damage By player,
        // which sets each player's damage dealt beside the period before (the fellows' shared damage per day)
        {
            PanelState Ev(Chapter c, string page, bool compare = true) { var st = Page(c, page, TimeWindow.SevenDays, compare); st.Everyone = true; return st; }
            var wood = PanelModel.Build(two, Ev(Chapter.Deeds, "woodcutting"));
            var dmg = PanelModel.Build(two, Ev(Chapter.Battle, "damage"));
            var plain = PanelModel.Content(PanelModel.Build(two, Ev(Chapter.Battle, "damage", compare: false))).First(b => b.Kind == PanelModel.GroupRowsKind);
            var rows = PanelModel.Content(dmg).FirstOrDefault(b => b.Kind == PanelModel.CompareRowsKind);
            var hero = PanelModel.Content(dmg).FirstOrDefault(b => b.Kind == "hero");
            var members = plain.Items.Where(r => r.Kind == PanelModel.GroupMemberKind).Select(r => r.Id).ToList();
            Check(wood.Compare != null && wood.Compare.Disabled && wood.CompareWhy == PanelModel.CompareEveryoneOff && !wood.Comparing &&
                  dmg.Compare != null && !dmg.Compare.Disabled && dmg.Comparing && rows != null && rows.Items.Select(r => r.Id).SequenceEqual(members) &&
                  members.Count > 1 && hero != null && !string.IsNullOrEmpty(hero.Before) &&
                  Math.Abs(rows.Items.Sum(r => PanelModel.Amount(r.Value)) - PanelModel.Amount(hero.Value)) < 1.5,
                  "compare + Everyone: Woodcutting greyed with the reason; Battle > Damage compares a row per player (" + string.Join(", ", members) + ") that add up to the group's hero, which gets its number before");
        }
        return fails;
    }
}
