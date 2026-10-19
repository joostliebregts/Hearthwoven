// Tests for the 0.9 crafting-history track (work/hearthwoven-0.9/DESIGN.md section 2, "Crafting" and "History"): Crafting's Upgrades per item island
// (src/Panel/Chapters/CraftingHistory.cs), the two faults Joost saw in game on Crafting (18 Oct: three tiles without names, an unlabelled grey By kind
// bar), every deed's History opened from its hero's growth line and one day of it, the Firsts island (src/Panel/Chapters/DeedHistory.cs). On the sample
// of src/Panel/PanelSampleCrafting.cs. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class CraftingHistoryTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelState Page(string page, Action<PanelState> more = null) { var s = new PanelState { Chapter = Chapter.Deeds }; s.Page[Chapter.Deeds] = page; more?.Invoke(s); return s; }
    static PanelView Build(PanelInput i, PanelState s) => PanelModel.Build(i, s);
    static List<Block> All(PanelView v) => PanelModel.Content(v);
    static Block Section(PanelView v, string title) => All(v).FirstOrDefault(b => b.Kind == "section" && b.Title == title);
    static Block After(PanelView v, Block b) { var l = All(v); var at = l.IndexOf(b); return at >= 0 && at + 1 < l.Count ? l[at + 1] : null; }
    static string Words(PanelView v) => string.Join(" | ", All(v).Select(b => b.Title + " " + b.Value + " " + b.Text + " " + b.RecordedFrom));

    /// <summary>Opens a page from another, as a click does (Visited, then Follow), and returns the state.</summary>
    static PanelState Open(PanelInput i, string from, string target)
    {
        var s = Page(from); var v0 = Build(i, s); PanelModel.Visited(s, v0.Active, v0.Page);
        PanelModel.Follow(s, target);
        return s;
    }

    public static int Run(DateTime now)
    {
        fails = 0;

        // ---------- Crafting: Upgrades per item ----------
        var up = CraftHistorySample.World(now, "upgrades");
        var craft = Build(up, Page("crafting"));
        var head = Section(craft, PanelModel.UpgradesTitle);
        var rows = head == null ? null : After(craft, head);
        Check(head != null && head.Value == "4" && head.Src == PanelModel.SrcPc && (head.RecordedFrom ?? "") == "Recorded from 2 October · this PC",
              "upgrades: the island's head says the four recorded and \"Recorded from 2 October · this PC\" (" + head?.Value + ", " + head?.RecordedFrom + ")");
        Check(rows != null && rows.Kind == "ranking" && rows.Tone == PanelModel.LevelTone && rows.Items.Select(r => r.Id).SequenceEqual(new[] { "$item_sword_iron", "$item_axe_bronze", "$item_shield_wood" }),
              "upgrades: one row per item, most upgraded first, the level rows' form (" + string.Join(", ", rows?.Items?.Select(r => r.Id) ?? new string[0]) + ")");
        var sword = rows?.Items?.FirstOrDefault(r => r.Id == "$item_sword_iron"); var shield = rows?.Items?.FirstOrDefault(r => r.Id == "$item_shield_wood");
        Check(sword != null && sword.Title == "Iron Sword" && sword.Icon == "item:$item_sword_iron" && sword.Value == "2" && sword.Text == "now level 3 of 4" && Math.Abs(sword.Fraction - 0.75f) < 1e-4 && sword.Level == 3 && sword.Count == 4,
              "upgrades: the Iron Sword: picture, name, two upgrades, \"now level 3 of 4\", the meter three parts of four (" + sword?.Text + ", " + sword?.Fraction + ")");
        Check(shield != null && shield.Text == "now level 2 of 3" && shield.Level == 2 && shield.Count == 3 && PanelModel.LevelMeterOf((int)shield.Level, shield.Count) == (3, 2f),
              "upgrades: the Wood Shield (max 3) is a meter of three parts, two filled, never four filled 2.5 (" + shield?.Text + ", " + PanelModel.LevelMeterOf((int)(shield?.Level ?? 0), shield?.Count ?? 0) + ")");
        Check(PanelModel.LevelMeterOf(3, 4) == (4, 3f) && PanelModel.LevelMeterOf(5, 5) == (5, 5f) && PanelModel.LevelMeterOf(2, 0) == (4, 2f) && PanelModel.LevelMeterOf(6, 20) == (4, 1.2f) && PanelModel.LevelMeterOf(7, 3) == (3, 3f),
              "level meter: a part per level of the max (4, 5), an unknown max four parts, a mod's max of 20 four parts by share, never more than full (one rule for the island and the deed feed)");
        var notes = All(craft).Where(b => b.Kind == "note").Select(b => b.Text).ToList();
        Check(notes.Contains("7 more upgrades came before 2 October: the game counts them, not which item."),
              "upgrades: the game's 11 minus the 4 recorded: \"7 more upgrades came before 2 October: the game counts them, not which item.\"");
        var order = All(craft).Select(b => b.Kind + ":" + b.Title).ToList();
        Check(order.IndexOf("section:" + PanelModel.UpgradesTitle) > order.FindIndex(x => x.StartsWith("itemgrid")) && order.IndexOf("section:" + PanelModel.UpgradesTitle) < Math.Max(order.IndexOf("section:Put to good use by"), int.MaxValue),
              "upgrades: the island stands after the gear's tiles");
        // nothing false: the line only when the game counted more than was recorded
        Check(PanelModel.UpgradesBeforeLine(0, "2 October", true) == null && PanelModel.UpgradesBeforeLine(-3, "2 October", true) == null && PanelModel.UpgradesBeforeLine(1, "2 October", true) == "1 more upgrade came before 2 October: the game counts them, not which item.",
              "upgrades: no before line when the game's count is not above the recorded one (0, or less after a reset); one says \"1 more upgrade\"");
        var over = CraftHistorySample.World(now, "upgrades"); over.Character["Upgrades"] = 2;
        Check(!All(Build(over, Page("crafting"))).Any(b => b.Kind == "note" && (b.Text ?? "").Contains("came before")), "upgrades: the game's counter below the recorded ones (2 < 4): no before line, never a negative");
        Check(PanelModel.LevelWords(3, 0) == "now level 3" && Math.Abs(PanelModel.LevelFill(3, 0) - 0.75f) < 1e-4 && Math.Abs(PanelModel.LevelFill(5, 4) - 1f) < 1e-4,
              "upgrades: an item whose max quality the game data does not give: \"now level 3\" on four parts; never more than full");
        // the empty state: recording began, nothing recorded, the game's count said in words
        var empty = Build(CraftHistorySample.World(now, "upgrades-empty"), Page("crafting"));
        var eHead = Section(empty, PanelModel.UpgradesTitle);
        Check(eHead != null && eHead.Value == null && (eHead.Text ?? "").StartsWith("Recorded from 7 October") && All(empty).Any(b => b.Kind == "note" && b.Text == PanelModel.UpgradesNoneYet)
              && All(empty).Any(b => b.Kind == "note" && b.Text == "11 upgrades came before 7 October: the game counts them, not which item.") && !All(empty).Any(b => b.Tone == PanelModel.LevelTone),
              "upgrades: none recorded yet: the head with its date and no number (never 0), what will show, the game's eleven before (" + eHead?.Text + ")");
        // not recorded on this book: a fellow's copy, a window, the totals not loaded
        var fellowCopy = CraftHistorySample.World(now, "upgrades"); fellowCopy.IsSelf = false;
        var unloaded = CraftHistorySample.World(now, "upgrades"); unloaded.Upgrades = null;
        Check(Section(Build(fellowCopy, Page("crafting")), PanelModel.UpgradesTitle) == null && Section(Build(unloaded, Page("crafting")), PanelModel.UpgradesTitle) == null
              && Section(Build(up, Page("crafting", s => { s.Window = TimeWindow.SevenDays; s.WindowPicked = true; })), PanelModel.UpgradesTitle) == null,
              "upgrades: a fellow's book, the record not loaded and a day window show no island (not recorded there; the game's counter has no item per day)");

        // ---------- fault 2: the unlabelled grey By kind bar ----------
        var one = Build(CraftHistorySample.World(now, "onekind"), Page("crafting"));
        var hero = All(one).FirstOrDefault(b => b.Kind == "hero");
        var kindBar = PanelModel.FilterOf(one)?.Items?.FirstOrDefault(b => b.Kind == "facetbar" && b.Id == "kind");
        var form = kindBar == null ? null : PanelModel.BarFormOf(kindBar.Items);
        Check(hero != null && hero.Value == "6" && hero.Items?.FirstOrDefault(n => n.Kind == "number")?.Value == "11", "fault 2: the in-game page: 6 gear crafted, 11 upgrades made (" + hero?.Value + ")");
        Check(kindBar != null && kindBar.Tone == PanelModel.FacetSingleTone && form.Rows.Count == 1 && form.Rows[0].Title == "Armor" && form.Rows[0].Value == "6" && PanelModel.ListShown(form, true) && !PanelModel.ListShown(form, false),
              "fault 2: a By kind bar of one kind (all armor) is drawn as its one named row, \"Armor 6\" under \"By kind\", not a one-colour bar with no list (" + kindBar?.Tone + ")");
        var two = PanelModel.FilterOf(Build(up, Page("crafting")))?.Items?.FirstOrDefault(b => b.Kind == "facetbar" && b.Id == "kind");
        Check(two != null && two.Tone == null && PanelModel.BarFormOf(two.Items).List, "fault 2: a bar of several kinds keeps its bar and its list");
        var tiles = All(one).FirstOrDefault(b => b.Kind == "itemgrid");
        Check(tiles != null && tiles.Items.Count == 3 && tiles.Items.All(t => !string.IsNullOrWhiteSpace(t.Title)), "fault 1: the three tiles carry their names in the model (" + string.Join(", ", tiles?.Items?.Select(t => t.Title) ?? new string[0]) + ")");

        // ---------- fault 1: the tile's height holds its name ----------
        float LB(float size) => PanelLayout.LineBox(size);
        var need = LB(PanelLayout.Text.Count) + LB(PanelLayout.Text.Floor);
        Check(PanelLayout.Tile.Base - 2 * PanelLayout.Tile.Pad < need, "fault 1 reproduced: the 0.8.6 tile (46 px, 3 px padding) held " + (PanelLayout.Tile.Base - 2 * PanelLayout.Tile.Pad) + " px for the count's and one name line's " + need + ": the name was squeezed below one line and the ellipsis dropped it");
        Check(PanelLayout.Tile.Height(false, 0) - 2 * PanelLayout.Tile.Pad >= need && PanelLayout.Tile.Height(true, 0) - 2 * PanelLayout.Tile.Pad >= need + LB(PanelLayout.Text.Floor)
              && PanelLayout.Tile.Height(false, 2) == PanelLayout.Tile.Height(false, 0) + 2 * PanelLayout.Tile.Line,
              "fault 1: a tile now holds the count and a name line (" + PanelLayout.Tile.Height(false, 0) + " px), a wrapped name a second line, each further line its own");

        // ---------- History: opened from the hero's growth line ----------
        var hist = CraftHistorySample.World(now, "history");
        var craftPage = Build(hist, Page("crafting"));
        var spark = PanelModel.SparkOf(All(craftPage).FirstOrDefault(b => b.Kind == "hero"));
        Check(spark != null && spark.Id == "Deeds/" + PanelModel.HistoryPageOf("crafting"), "history: your hero's growth line opens Crafting's History (" + spark?.Id + ")");
        var fellow = CraftHistorySample.World(now, "history"); fellow.IsSelf = false;
        Check(PanelModel.SparkOf(All(Build(fellow, Page("crafting"))).FirstOrDefault(b => b.Kind == "hero"))?.Id == null, "history: a fellow's line opens nothing (their day history is not on this PC)");
        var hs = Open(hist, "crafting", spark?.Id ?? "Deeds/history:crafting");
        var hv = Build(hist, hs);
        Check(hv.Page == "history:crafting" && hv.Heading == "Crafting history" && hv.List.Any(l => l.Id == "crafting" && l.Selected) && hv.Back == PanelModel.BackLabel && hv.Windows.Count == 0,
              "history: Crafting history, the list's Crafting lit, Back to Crafting, no window chips (" + hv.Page + ", " + hv.Heading + ")");
        var metric = All(hv).FirstOrDefault(b => b.Kind == "switch" && b.Id.EndsWith("/metric"));
        Check(metric != null && metric.Items.Select(i => i.Title).SequenceEqual(new[] { "Gear crafted", "Upgrades made" }) && metric.Items[0].Selected, "history: chips for the hero's numbers, Gear crafted first");
        var period = All(hv).FirstOrDefault(b => b.Kind == "switch" && b.Id.EndsWith("/period"));
        Check(period != null && period.Items[0].Title == "Days" && period.Items[0].Selected && period.Items[1].Tone == PanelModel.OffTone && period.Items[1].Title == "Weeks from 9 Oct" && period.Items[2].Tone == PanelModel.OffTone && period.Items[2].Title.StartsWith("Months from "),
              "history: Days now, Weeks and Months greyed with the date they open (" + string.Join(", ", period?.Items?.Select(i => i.Title) ?? new string[0]) + ")");
        var chart = All(hv).FirstOrDefault(b => b.Kind == PanelModel.SparkKind && b.Tone == PanelModel.SparkLargeTone);
        var days = chart?.Items ?? new List<Block>();
        Check(chart != null && days.Count == DayHistory.DayRows && days[0].Id == "2026-09-04" && days[days.Count - 1].Id == "2026-10-08",
              "history: every day the history holds, 4 Sep to today (35 columns), no 0 before it (" + days.Count + ", " + days.FirstOrDefault()?.Id + ")");
        var chartHead = Section(hv, "Gear crafted per day");
        Check(chartHead != null && chartHead.RecordedFrom == "Recorded from 4 September · this PC" && chartHead.Value == chart?.Value,
              "history: the chart's head says its total and from when, the history's first day (" + chartHead?.RecordedFrom + " | chart " + chart?.RecordedFrom + " | key " + All(hv).FirstOrDefault(b => b.Kind == "composition")?.RecordedFrom + ")");
        // Joost on the first preview: "shouldn't those bars be different heights?" (every played day made one piece in the sample): the heights follow the counts
        double V(Block d) => double.Parse(d.Value, System.Globalization.CultureInfo.InvariantCulture);
        var played = days.Where(d => d.Tone != "zero").ToList();
        Check(played.Select(V).Distinct().Count() >= 4 && played.All(a => played.All(b => V(a) >= V(b) || a.Fraction < b.Fraction)) && played.All(a => played.All(b => V(a) != V(b) || a.Fraction == b.Fraction))
              && played.Any(d => d.Items != null && d.Items.Count >= 3) && played.Any(d => d.Items != null && d.Items.Count == 2),
              "history: two days with different counts get different heights (more is taller, as many as tall), several days split two or three kinds (" + string.Join(" ", played.Select(d => d.Value)) + ")");
        var quiet = days.FirstOrDefault(d => d.Id == "2026-09-17");
        Check(quiet != null && quiet.Tone == "zero" && quiet.Value == "0" && quiet.Fraction == 0, "history: a kept day with nothing is a flat mark (17 Sep)");
        // the line in the hero and the chart large agree on the days they share (the same model)
        var smallDays = spark?.Items ?? new List<Block>();
        Check(smallDays.Count > 0 && smallDays.All(s => days.Any(d => d.Id == s.Id && d.Value == s.Value)), "history: the hero's line and History agree day by day (the growth line's own model, drawn large)");
        var gearSum = days.Sum(d => double.Parse(d.Value, System.Globalization.CultureInfo.InvariantCulture));
        Check(chart != null && chart.Value == gearSum.ToString(System.Globalization.CultureInfo.InvariantCulture), "history: the chart's total is its columns' sum (" + chart?.Value + ")");
        // option A: a tick over a day with a first of the page's kind (record 3) or a feat of this page
        var oct6 = days.FirstOrDefault(d => d.Id == "2026-10-06"); var oct7 = days.FirstOrDefault(d => d.Id == "2026-10-07");
        Check(oct6 != null && oct6.Count == 1 && oct6.Text == "First recorded: Bronze Health Trinket" && oct7 != null && oct7.Count == 1 && oct7.Text == "First recorded: Hoe",
              "history A: gold ticks on 6 Oct (first Bronze Health Trinket) and 7 Oct (first Hoe); the cider's first is Cooking's, not here (" + oct7?.Text + ")");
        Check(days.Where(d => d.Count > 0).Select(d => d.Id).SequenceEqual(new[] { "2026-10-04", "2026-10-06", "2026-10-07" }), "history A: only the days with a first of gear tick (4, 6, 7 Oct)");
        var featBook = CraftHistorySample.World(now, "history");
        var arms = PanelModel.FeatDefs.First(d => d.Chapter == Chapter.Deeds && d.Page == "crafting" && !d.Group);
        featBook.Feats = new FeatsLedger { Primed = true }; featBook.Feats.Earned[arms.Id] = new List<FeatMoment> { new FeatMoment { Utc = new DateTime(2026, 9, 20, 18, 0, 0, DateTimeKind.Utc) } };
        var featDay = All(Build(featBook, Open(featBook, "crafting", "Deeds/history:crafting"))).First(b => b.Kind == PanelModel.SparkKind && b.Tone == PanelModel.SparkLargeTone).Items.First(d => d.Id == "2026-09-20");
        Check(featDay.Count == 1 && featDay.Text == "Feat: " + arms.Name + (arms.Tiers.Length > 1 ? " I" : ""), "history A: a feat of this page earned that day ticks it (" + featDay.Text + ")");
        // option B: columns split by kind in the page's By kind colours (the facet bar's), the kinds' bar under the chart as their key
        var key = All(hv).FirstOrDefault(b => b.Kind == "composition" && (b.Title ?? "").StartsWith("By kind, these 35 days"));
        var facetColours = PanelModel.FilterOf(craftPage).Items.First(b => b.Kind == "facetbar" && b.Id == "kind").Items.ToDictionary(p => p.Id, p => p.Colour);
        Check(key != null && key.Items.All(p => facetColours.TryGetValue(p.Id, out var c) && c == p.Colour), "history B: the key's kinds wear the page's By kind colours (" + string.Join(", ", key?.Items?.Select(p => p.Title + " " + p.Colour) ?? new string[0]) + ")");
        var split = days.Where(d => d.Items != null && d.Items.Count > 0).ToList();
        Check(split.Count > 0 && split.All(d => Math.Abs(d.Items.Sum(p => p.Fraction) - 1f) < 1e-3 && d.Items.All(p => key.Items.Any(k => k.Id == p.Id && k.Colour == p.Colour))) && days.Where(d => d.Tone == "zero").All(d => d.Items == null),
              "history B: a day's parts add up to its column, each in its kind's colour; an empty day has none");
        // hover and click
        Check(oct6 != null && oct6.Title.StartsWith("Tue 6 Oct · ") && oct6.Title.Contains("gear crafted") && PanelModel.HistoryDayTarget(chart, oct6) == "Deeds/day:crafting:2026-10-06",
              "history: hover says the day (\"" + oct6?.Title + "\"); a click opens that day's page");
        Check(days[0].Note == "4 Sep" && days[days.Count - 1].Note == "today" && days.Count(d => d.Note != null) >= 4, "history: the dates under the columns, the first and today (" + string.Join(", ", days.Where(d => d.Note != null).Select(d => d.Note)) + ")");
        // the second number: Upgrades made per day
        var hu = Build(hist, Open(hist, "crafting", "Deeds/history:crafting").Also(s => s.View["Deeds/history:crafting/metric"] = "upgrades"));
        var upChart = All(hu).First(b => b.Kind == PanelModel.SparkKind && b.Tone == PanelModel.SparkLargeTone);
        Check(upChart.Title == "Upgrades made per day" && Section(hu, "Upgrades made per day") != null && upChart.Items.All(d => d.Items == null), "history: Upgrades made per day, one colour (the game counts no kinds of upgrade)");
        // one day's page
        var ds = Open(hist, "history:crafting", "Deeds/" + PanelModel.DayPageOf("crafting", new DateTime(2026, 10, 5)));
        var dv = Build(hist, ds); PanelModel.Visited(ds, dv.Active, dv.Page);
        var dayHero = All(dv).FirstOrDefault(b => b.Kind == "hero");
        var oct5 = days.First(d => d.Id == "2026-10-05");
        Check(dv.Heading == "Crafting, Mon 5 Oct" && dv.List.Any(l => l.Id == "crafting" && l.Selected) && dv.Windows.Count == 0 && dv.Back == PanelModel.BackLabel && (oct5.Tone == "zero" ? dayHero == null : dayHero?.Value == oct5.Value),
              "history: a column's click opens Crafting on that day, its hero that day's count (" + dv.Heading + ", " + dayHero?.Value + " = " + oct5.Value + ")");
        Check(PanelModel.Back(ds) && ds.PageOf(Chapter.Deeds) == "history:crafting", "history: Back from the day leads to History");
        var gone = Build(hist, Page(PanelModel.DayPageOf("crafting", new DateTime(2026, 8, 1))));
        Check(gone.Heading.StartsWith("Crafting, ") && All(gone).Any(b => b.Kind == "empty" && b.Title == "This day is not kept"), "history: a day the history no longer holds says so, never zeros");
        // every deed with a growth line
        foreach (var deed in PanelModel.HistoryPages)
        {
            var v = Build(hist, Open(hist, deed, "Deeds/" + PanelModel.HistoryPageOf(deed)));
            var c = All(v).FirstOrDefault(b => b.Kind == PanelModel.SparkKind && b.Tone == PanelModel.SparkLargeTone);
            Check(c != null && c.Items.Count > 0 && v.Heading.EndsWith(" history") && !All(v).Any(b => b.Kind == "empty"), "history: " + deed + " has its History (" + c?.Title + ", " + c?.Items?.Count + " days)");
        }
        // not recorded: no day history on this book
        var none = CraftHistorySample.World(now, "upgrades"); none.History = null;
        var nv = Build(none, Page("history:crafting"));
        Check(All(nv).Any(b => b.Kind == "empty" && b.Title == PanelModel.NotYetRecorded) && !All(nv).Any(b => b.Kind == PanelModel.SparkKind), "history: no day history loaded: \"" + PanelModel.NotYetRecorded + "\", no chart of zeros");

        // ---------- the Firsts island ----------
        var firsts = Section(hv, PanelModel.FirstsTitle); var frows = firsts == null ? null : After(hv, firsts);
        Check(firsts != null && firsts.Value == "3" && (firsts.RecordedFrom ?? "") == "Recorded from 2 October · this PC" && frows?.Kind == "rows"
              && frows.Items.Select(r => r.Title + ": " + r.Value).SequenceEqual(new[] { "Hoe: first recorded 7 Oct", "Bronze Health Trinket: first recorded 6 Oct", "Cultivator: first recorded 4 Oct" }),
              "firsts: the gear first made since 2 October, newest first, the date in the value's place (" + firsts?.Value + ", " + firsts?.RecordedFrom + ", " + frows?.Kind + ": " + string.Join(", ", frows?.Items?.Select(r => r.Title + ": " + r.Value) ?? new string[0]) + ")");
        Check(All(hv).Any(b => b.Kind == "note" && (b.Text ?? "").EndsWith("first made before 2 October: the game counts them, not when.")), "firsts: the kinds made before the record began are said in words, not dated");
        Check(Section(Build(hist, Open(hist, "woodcutting", "Deeds/history:woodcutting")), PanelModel.FirstsTitle) == null, "firsts: none on a gathering page (record 3 holds things made or placed)");

        System.Console.WriteLine(fails == 0 ? "crafting-history: ALL PASS" : "crafting-history: " + fails + " FAILED");
        return fails;
    }

    static PanelState Also(this PanelState s, Action<PanelState> f) { f(s); return s; }
}
