// Tests for the book's one bar form (0.7, work/hearthwoven-0.7/BAR-FORM.md; src/Panel/BarForm.cs, FacetModel.BarColours):
// the abstract categories' palette, a category's colour fixed by its name, the list's shares and columns, the numbers only on
// damage-type bars. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class BarFormTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static IEnumerable<Block> Walk(IEnumerable<Block> bs) { foreach (var b in bs ?? Enumerable.Empty<Block>()) { yield return b; foreach (var c in Walk(b.Items)) yield return c; } }
    static IEnumerable<Block> All(PanelView v) => Walk(PanelModel.Content(v));
    static PanelView Page(PanelInput i, Chapter ch, string page, TimeWindow w = TimeWindow.SinceInstall, bool open = false, string filter = null)
    {
        var st = new PanelState { Chapter = ch, Window = w }; st.Page[ch] = page;
        if (open && filter != null) st.OpenFilters.Add(filter);
        return PanelModel.Build(i, st);
    }
    // a facet bar's part colour by its name, or null when the bar does not show it
    static string ColourOf(PanelView v, string facet, string name) => PanelModel.FilterOf(v)?.Items.FirstOrDefault(b => b.Kind == "facetbar" && b.Id == facet)?.Items.FirstOrDefault(p => p.Title == name)?.Colour;
    static Block P(string id, float f, string title = null) => new Block { Id = id, Title = title ?? id, Value = ((int)Math.Round(f * 1000)).ToString(), Fraction = f };

    public static int Run(DateTime now)
    {
        // ---------- the palette: D's family, told apart, visible on the dark plate ----------
        var pal = PanelModel.BarPalette; var worst = double.MaxValue; var pair = "";
        foreach (var a in pal.Concat(new[] { PanelModel.BarOtherColour }))
            foreach (var b in pal) if (a != b) { var d = PanelModel.ColourDistance(a, b); if (d < worst) { worst = d; pair = a + "/" + b; } }
        Check(pal.Length >= 10 && pal.Length <= 12 && worst >= PanelModel.BarApart && PanelModel.BarApart >= 15 && pal.All(c => PanelModel.Lightness(c) >= 45),
              "bar palette: " + pal.Length + " tones, every two (and each against Other's grey) at least CIEDE2000 " + PanelModel.BarApart + " apart (closest " + pair + " " + worst.ToString("0.0") + "), none darker than L 45 (darkest " + pal.Min(PanelModel.Lightness).ToString("0.0") + ")");

        // ---------- a category's colour comes from its name: the same on another window and another hammer ----------
        var full = PanelSample.Full(now); var modded = BuildingSample.Modded(PanelSample.Full(now));
        var builds = new[] { Page(full, Chapter.Deeds, "building", open: true, filter: PanelModel.BuildFilter), Page(full, Chapter.Deeds, "building", TimeWindow.SevenDays, true, PanelModel.BuildFilter), Page(modded, Chapter.Deeds, "building", open: true, filter: PanelModel.BuildFilter) };
        var tabColours = new[] { "Building", "Stonecutter" }.Select(t => builds.Select(v => ColourOf(v, "tab", t)).Where(c => c != null).Distinct().ToList()).ToList();
        var cooks = new[] { Page(full, Chapter.Deeds, "cooking", open: true, filter: PanelModel.CookFilter), Page(full, Chapter.Deeds, "cooking", TimeWindow.SevenDays, true, PanelModel.CookFilter) };
        var meals = cooks.Select(v => ColourOf(v, "type", "Meals")).Where(c => c != null).Distinct().ToList();
        Check(tabColours.All(l => l.Count == 1) && tabColours[0][0] == PanelModel.CategoryColour("Building") && meals.Count == 1 && meals[0] == PanelModel.CategoryColour("Meals") &&
              PanelModel.CategoryColour("Clay Build Pieces") == PanelModel.CategoryColour("clay build pieces.") && builds.Count(v => ColourOf(v, "tab", "Building") != null) == 3,
              "bar colours: Building, Stonecutter and Meals wear the same colour on All, on 7 days and beside a modded hammer's tabs (" + string.Join(" / ", tabColours.Select(l => string.Join(",", l))) + ", Meals " + string.Join(",", meals) + "), a mod's category by its name");
        // the same set always looks the same: two categories that start on one colour, the later by name steps on, whatever their rank
        var clash = Enumerable.Range(1, 40).Select(k => "Mod pieces " + k).GroupBy(PanelModel.CategoryIndex).First(g => g.Count() > 1).Take(2).ToList();
        {
            var one = PanelModel.BarColours(clash.Select(n => (n, n, (string)null, false)).ToList(), false);
            var two = PanelModel.BarColours(clash.AsEnumerable().Reverse().Select(n => (n, n, (string)null, false)).ToList(), false);
            var first = clash.OrderBy(PanelModel.SameLabel, StringComparer.Ordinal).First();
            Check(one.All(kv => two[kv.Key] == kv.Value) && one.Values.Distinct().Count() == clash.Count && one[first] == PanelModel.CategoryColour(first),
                  "bar colours: " + string.Join(" and ", clash) + " start on one colour; the first by name keeps it, the other steps on, in either rank order");
        }

        // ---------- the list: shares add up to 100, every part its own row (B39), Other last, two columns past four rows ----------
        var thirds = PanelModel.BarFormOf(new[] { P("a", 1 / 3f), P("b", 1 / 3f), P("c", 1 / 3f) });
        var wood = All(Page(full, Chapter.Deeds, "woodcutting")).First(b => b.Kind == "composition");
        var woodForm = PanelModel.BarFormOf(wood.Items);
        var mixed = PanelModel.BarFormOf(new[] { P("Wood", 0.6f), P("Stone", 0.2f), P("Bark", 0.02f), P("Resin", 0.01f), P(PanelModel.FoldId, 0.02f, "Other (3 kinds)"), P("Iron", 0.15f) });
        Check(thirds.Rows.Select(r => r.Percent).SequenceEqual(new[] { 34, 33, 33 }) && woodForm.Rows.Sum(r => r.Percent) == 100 && mixed.Rows.Sum(r => r.Percent) == 100 &&
              string.Join(",", mixed.Rows.Select(r => r.Title)) == "Wood,Stone,Bark,Resin,Iron,Other (3 kinds)" && mixed.Rows[2].Percent == 2 && mixed.Rows[3].Percent == 1,
              "bar list (B39): whole shares add up to 100 (thirds 34/33/33, Woodcutting " + string.Join("/", woodForm.Rows.Select(r => r.Percent)) + "); a part under 3 % keeps its own row, Other (n kinds) last: " + string.Join(", ", mixed.Rows.Select(r => r.Title + " " + r.Percent + "%")));
        // past eight rows the smallest parts and the Other part fold into one "Other (n kinds)" row (the 0.6.5 rule; Battle's nine damage types)
        var nine = PanelModel.BarFormOf(Enumerable.Range(0, 9).Select(k => P("p" + k, (9 - k) / 45f)).ToList());
        Check(nine.Rows.Count == PanelModel.BarMaxParts && nine.Rows.Last().Other && nine.Rows.Last().Title == "Other (2 kinds)" && nine.Rows.Sum(r => r.Percent) == 100,
              "bar list: nine parts make eight rows, the last \"" + nine.Rows.Last().Title + "\"");
        // a real part that rounds to 0 % says "<1 %" (0.6.5 rule: never 0 for a real amount; B26 words); the shares still add up to 100
        var tiny = PanelModel.BarFormOf(new[] { P("Wood", 0.996f), P("Bark", 0.003f), P(PanelModel.FoldId, 0.001f, "Other (2 kinds)") });
        Check(tiny.Rows.Sum(r => r.Percent) == 100 && tiny.Rows.Skip(1).All(r => r.Percent == 0 && r.Share == PanelModel.UnderOnePercent) && tiny.Rows[0].Share == "100 %",
              "bar list: a part well under 1 % reads \"<1 %\", never \"0 %\": " + string.Join(", ", tiny.Rows.Select(r => r.Title + " " + r.Share)));
        var four = PanelModel.BarFormOf(new[] { P("a", .4f), P("b", .3f), P("c", .2f), P("d", .1f) });
        var five = PanelModel.BarFormOf(new[] { P("a", .3f), P("b", .25f), P("c", .2f), P("d", .15f), P("e", .1f) });
        var eight = PanelModel.BarFormOf(Enumerable.Range(0, 8).Select(k => P("p" + k, 1 / 8f)).ToList());
        var duo = PanelModel.BarFormOf(new[] { P("a", .6f), P("b", .4f) });
        Check(duo.Columns == 2 && four.Columns == 3 && four.Split == 2 && five.Columns == 3 && five.Split == 2 && eight.Columns == 3 && eight.Split == 3 && PanelModel.BarListSplit(5, 2) == 3 && PanelModel.BarListLines(8) == 3,
              "bar list (0.8 layout D+, FEEDBACK 1): as flat as it fits, up to three columns filled top to bottom: four rows 2 + 2, five 2 + 2 + 1, eight 3 + 3 + 2 (two columns where three do not fit: 3 + 2)");
        // the page's legend grid: four columns when every legend's columns fit a quarter of the island, else three for the whole page; a half
        // island takes half the page's columns at its own width
        var fits4 = new List<(float, Func<int, float>)> { (812f, k => 150f), (812f, k => 180f), (385f, k => 170f) };
        var oneWide = new List<(float, Func<int, float>)> { (812f, k => 150f), (812f, k => k >= 4 ? 210f : 200f) };   // 210 > 195.5
        Check(PanelModel.LegendPage(fits4, 812f) == 4 && PanelModel.LegendPage(oneWide, 812f) == 3 && PanelModel.LegendGrid(4, 812f, 812f).columns == 4 && Math.Abs(PanelModel.LegendGrid(4, 812f, 812f).width - 195.5f) < 0.01f &&
              PanelModel.LegendGrid(4, 385f, 812f).columns == 2 && PanelModel.LegendGrid(3, 385f, 812f).columns == 1,
              "legend grid (0.8 layout D+): one column count per page, four when every legend fits a quarter (195.5 px of 812), else three for all; a half island takes two of four, one of three");
        var single = PanelModel.BarFormOf(new[] { P("Blunt", 1f) });
        Check(!single.List && single.Rows.Count == 1 && four.List && PanelModel.BarFormHeight(1, false) < PanelModel.BarFormHeight(2, false),
              "bar list: a bar with one part has no list (its head line says the name and total), and takes no room for one");
        var rows = new[] { new Block { Kind = "composition", Fraction = 1f }, new Block { Kind = "composition", Fraction = 0.4f }, new Block { Kind = "composition" } };
        Check(rows.Select(PanelModel.BarLength).SequenceEqual(new[] { 1f, 0.4f, 1f }),
              "bar length: rows of one kind share one scale (a bar per foe 40 % of the largest is 40 % long); a bar with no such share runs the full width");

        // ---------- numbers inside the parts: damage-type bars only ----------
        var battle = PanelSample.Battle();
        var damageBars = new[] { "damage", "defense", null }.SelectMany(p => All(Page(battle, Chapter.Battle, p))).Where(b => b.Kind == "dmgmix" || (b.Kind == "composition" && b.Items.All(i => (i.Icon ?? "").StartsWith("damage:") || i.Id == PanelModel.FoldId))).ToList();
        var otherBars = new[] { Page(full, Chapter.Deeds, "woodcutting"), Page(full, Chapter.Deeds, "mining"), Page(full, Chapter.Voyages, "cargo"), Page(full, Chapter.Stores, "smelters"), builds[0] }.SelectMany(All).Where(b => b.Kind == "composition" || b.Kind == "facetbar").ToList();
        Check(damageBars.Count >= 2 && damageBars.All(PanelModel.NumbersOnBar) && otherBars.Count >= 4 && !otherBars.Any(PanelModel.NumbersOnBar),
              "bar numbers: the " + damageBars.Count + " damage-type bars carry their numbers inside the parts; the " + otherBars.Count + " other bars (wood, ore, cargo, smelters, the filter's) carry none");

        // ---------- 0.8 damage rows (density-feedback/DAMAGE-BARS.md): one scale per page, the zero at a quarter of the track, the true length under it ----------
        {
            var st = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.SinceInstall }; st.Page[Chapter.Battle] = "damage"; st.View["Battle/damage/view"] = "foe";
            var foes = All(PanelModel.Build(full, st)).FirstOrDefault(b => b.Kind == "dealtfoes")?.Items.Where(i => i.Kind == "source").ToList() ?? new List<Block>();
            var fight = All(Page(full, Chapter.Battle, PanelModel.LastFightPage)).FirstOrDefault(b => b.Kind == "fightfoes")?.Items.Where(i => i.Kind == "foe").ToList() ?? new List<Block>();
            bool OnScale(List<Block> rows) => rows.Any(f => PanelModel.DamageBarWidth(f.Fraction) == 1f && PanelModel.DamageTrueWidth(f.Fraction) == 1f) &&   // the biggest (or the tied biggest) fills it
                rows.All(f => f.Fraction > 0 && Math.Abs(PanelModel.DamageBarWidth(f.Fraction) - (0.25f + 0.75f * f.Fraction)) < 1e-6 && PanelModel.DamageTrueWidth(f.Fraction) == f.Fraction);
            var least = foes.OrderBy(f => f.Fraction).FirstOrDefault();
            // By weapon and By type (Joost: one grammar for Damage's three views): the weapons on one scale; By type's rows from the grid, each type on one
            // scale, its bar split by weapon kind in their fixed category colours, the parts' shares and amounts the grid's own
            PanelView View(string view) { var s = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.SinceInstall }; s.Page[Chapter.Battle] = "damage"; s.View["Battle/damage/view"] = view; return PanelModel.Build(full, s); }
            var weapons = PanelModel.Content(View("weapon")).Where(b => b.Kind == "dmgmix").ToList();   // Content: each block once (All walks a box's blocks again)
            var grid = All(View("type")).FirstOrDefault(b => b.Kind == "damagegrid");
            var types = PanelModel.DamageTypeRows(grid);
            var kindColour = new[] { "melee", "bow", "magic" }.ToDictionary(k => k, k => PanelModel.CategoryColour(PanelModel.WeaponName(k)));
            var typesOk = types.Count >= 4 && OnScale(types) && types.All(t => t.Tone == "type" && Math.Abs(t.Items.Sum(p => p.Fraction) - 1) < 1e-4 && t.Items.All(p => p.Colour == kindColour[p.Id] && p.Icon == "vocab:weapon-" + p.Id)) &&
                          types.Sum(t => t.Items.Sum(p => PanelModel.ParseCount(p.Value))) == grid.Items.Where(i => i.Kind == "dmgtype").Sum(t => t.Items.Sum(c => PanelModel.ParseCount(c.Value))) &&
                          kindColour.Values.Distinct().Count() == 3;
            // the group's rows by player (Everyone's Damage, Last fight): a player's bar on one scale, their share of the group inside it, their damage by type the line under it
            var evState = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.LastHour, WindowPicked = true, Everyone = true }; evState.Page[Chapter.Battle] = "damage";
            var groups = new[] { PanelModel.Build(full, evState), Page(full, Chapter.Battle, PanelModel.LastFightPage) }.Select(v => PanelModel.Content(v).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)).ToList();
            var players = groups.Select(g => PanelModel.DamagePlayerRows(g).Where(r => r.Tone == PanelModel.DamagePlayerTone).ToList()).ToList();
            var playersOk = groups.All(g => g?.Tone == PanelModel.DamagePlayersDealt) && players.All(l => l.Count >= 2 && OnScale(l) &&
                            l.All(r => r.Items.Count(i => i.Kind == "part") == 1 && r.Items.First(i => i.Kind == "part").Value.EndsWith("%") && Math.Abs(r.Items.Where(i => i.Kind == "mix").Sum(i => i.Fraction) - 1) < 1e-4));
            Check(PanelModel.DamageFloor == 0.25f && PanelModel.DamageBarWidth(0) == 0.25f && PanelModel.DamageBarWidth(0.5f) == 0.625f && PanelModel.DamageTrueWidth(0.5f) == 0.5f &&
                  foes.Count >= 3 && OnScale(foes) && fight.Count >= 2 && OnScale(fight) && fight.All(f => f.Items.Any(p => p.Kind == "part")) && weapons.Count >= 2 && OnScale(weapons) && typesOk && playersOk,
                  "damage rows: the zero at 25 % of the track (a bar is 25 % + its share of the page's biggest x 75 %), the biggest row fills it, the line under it at the true " +
                  "length; By foe's " + foes.Count + " foes (the least, " + least?.Title + ", " + (PanelModel.DamageBarWidth(least?.Fraction ?? 0) * 100).ToString("0") + " % long, true " +
                  (PanelModel.DamageTrueWidth(least?.Fraction ?? 0) * 100).ToString("0") + " %), By weapon's " + weapons.Count + " weapons, By type's " + types.Count + " types (each split by weapon kind in " +
                  string.Join(", ", kindColour.Select(kv => kv.Key + " " + kv.Value)) + "), Last fight's " + fight.Count + " foes, and the players of Everyone's By player (" + string.Join(", ", players.ElementAtOrDefault(0)?.Select(r => r.Title + " " + r.Items[0].Value) ?? new string[0]) +
                  ") and of Last fight, each list on its own scale");
            // REVIEW-081 #6: a weapons run keeps the number column of its first row, so that row's guess at the run's largest never comes out a digit
            // short: Melee 4.4 (shown "4") beside Bow 10 004, and a weapon under 1 beside 9 996
            int Digits(string t) => t.Count(char.IsDigit);
            var shortGuess = PanelModel.DamageWidestNumber(new Block { Value = "4", Fraction = 4.4f / 10004f });
            var lessGuess = PanelModel.DamageWidestNumber(new Block { Value = PanelModel.LessThanOne, Fraction = 0.4f / 9996f });
            var topGuess = PanelModel.DamageWidestNumber(new Block { Value = PanelModel.Number(9996), Fraction = 1f });
            Check(Digits(shortGuess) >= 5 && Digits(lessGuess) >= 4 && topGuess == "8" + PanelModel.ThousandsGap + "888",
                  "damage rows (REVIEW-081 #6): a run's number column from its first row never undershoots the run's largest (4 of 10 004: " + shortGuess + "; <1 of 9 996: " + lessGuess + "; the largest itself: " + topGuess + ")");
        }
        return fails;
    }
}
