// Tests for 0.6's two pages (src/Panel/Chapters/CargoModel.cs): Cargo carried (Voyages > Cargo, Company > Together) and
// Born in your care (Deeds > Taming), built on the sample evening (src/Panel/PanelSampleCargo.cs, fictional players).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class CargoTests
{
    /// <summary>
    /// 0.8.5 why a page's cargo sums are wrong, null when they are right (CargoModel.CargoSum, CargoSumRows). A, the sum under the bar: it works
    /// out the bar's largest part (its result is that part's number) and load × distance gives the result as shown (within its last digit).
    /// C, Usually aboard as sums, where the page has them: a row per largest part of the bar in its order with its number (at most a half
    /// island's four, the rest in one Other row), every row's sum holds the same way, the top row's sum is A's, and "together" is the hero,
    /// which the rows add up to (each rounded as the bar's legend is).
    /// </summary>
    public static string SumWrong(List<Block> page)
    {
        var sum = page.FirstOrDefault(b => b.Kind == PanelModel.SumKind);
        var bar = page.FirstOrDefault(b => b.Kind == "composition" && (b.Items ?? new List<Block>()).Any(i => (i.Id ?? "").StartsWith("$item")));
        if (sum == null || bar == null) return "no sum or no bar";
        var part = bar.Items[0];
        if (sum.Id != part.Id || sum.Value != part.Value) return "the sum is of " + sum.Id + " = " + sum.Value + ", the bar's largest part " + part.Id + " = " + part.Value;
        // half a step of a shown number's last digit ("1 430": 0.5, "4.1": 0.05)
        double Half(string shown) => 0.5 * Math.Pow(10, -(shown.Contains(".") ? shown.Length - shown.IndexOf('.') - 1 : 0));
        string Wrong(List<Block> terms, string value)
        {
            var far = terms[1].Value;
            double load = PanelModel.ParseCount(terms[0].Value), distance = PanelModel.ParseCount(far.Substring(0, far.LastIndexOf(' ')));
            return Math.Abs(load * distance - PanelModel.ParseCount(value)) > Half(value) + 1e-9 ? terms[0].Value + " × " + far + " is " + load * distance + ", not " + value : null;
        }
        if (Wrong(sum.Items, sum.Value) is string a) return a;
        var rows = page.FirstOrDefault(b => b.Kind == PanelModel.SumRowsKind);
        if (rows == null) return null;
        var items = rows.Items.Where(r => r.Id != PanelModel.FoldId).ToList();
        if (items.Count > PanelModel.IslandHalfRows + 1 || (rows.Items.Count > items.Count && (items.Count != PanelModel.IslandHalfRows || rows.Items.Last().Id != PanelModel.FoldId)) ||
            string.Join(",", items.Select(r => r.Id + "=" + r.Value)) != string.Join(",", bar.Items.Take(items.Count).Select(i => i.Id + "=" + i.Value)))
            return "the rows " + string.Join(",", rows.Items.Select(r => r.Id + "=" + r.Value)) + " are not the bar's largest parts " + string.Join(",", bar.Items.Select(i => i.Id + "=" + i.Value)) +
                   " (at most " + PanelModel.IslandHalfRows + " and the rest in one Other row)";
        foreach (var r in rows.Items.Where(r => r.Items != null)) if (Wrong(r.Items, r.Value) is string w) return r.Title + ": " + w;
        if (rows.Items[0].Items == null || string.Join(" × ", rows.Items[0].Items.Select(t => t.Value)) != string.Join(" × ", sum.Items.Select(t => t.Value))) return "the top row's sum is not the one under the bar";
        var hero = page.FirstOrDefault(b => b.Kind == "hero" && (b.Title == PanelModel.ItemKmUnit || b.Title == PanelModel.ItemMetresUnit));
        var added = rows.Items.Sum(r => PanelModel.ParseCount(r.Value));
        if (hero == null || rows.Value != hero.Value || rows.Title != hero.Title || Math.Abs(added - PanelModel.ParseCount(rows.Value)) > rows.Items.Sum(r => Half(r.Value)) + Half(rows.Value) + 1e-9)
            return "together " + rows.Value + " " + rows.Title + ", the hero " + hero?.Value + " " + hero?.Title + ", the rows add up to " + added;
        return null;
    }

    public static int Run(PanelInput voyager, PanelInput input, DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
        PanelView Page(PanelInput who, Chapter c, string page, Action<PanelState> more = null)
        {
            var st = new PanelState { Chapter = c }; st.Page[c] = page; more?.Invoke(st);
            return PanelModel.Build(who, st);
        }

        // ---------- units ----------
        Check(PanelModel.CargoUnit(840) == "item-meters" && PanelModel.CargoUnit(2070000) == "item-km" && PanelModel.CargoText(2070000, 2070000) == "2 070 item-km" &&
              PanelModel.CargoText(1430000, 2070000) == "1 430 item-km" && PanelModel.CargoText(60000, 2070000) == "60 item-km" && PanelModel.CargoText(3000, 2070000) == "3 item-km" && PanelModel.CargoText(3000, 40000) == "3.0 item-km" && PanelModel.CargoText(400, 2070000) == "0.4 item-km" && PanelModel.CargoText(840, 840) == "840 item-meters" &&
              PanelModel.CargoNumber(260, 840) == "260",
              "cargo: item-metres up to 1 000, item-km above (no k or M, the word list keeps km as the one abbreviation); parts in the unit of the total, whole km once the total is 100 or more (\"4\" beside \"1 430\", not \"4.0\")");

        // ---------- Voyages > Cargo (fix3-rest: its own page; Sailing keeps the voyages, the route and the crew) ----------
        var sailing = Page(voyager, Chapter.Voyages, "sailing");
        var cargoPage = Page(voyager, Chapter.Voyages, "cargo");
        var all = Every(PanelModel.Content(cargoPage)).ToList();
        var sections = all.Where(b => b.Kind == "section").Select(b => b.Title).ToList();
        var hero = all.FirstOrDefault(b => b.Kind == "hero" && b.Title == "item-km");
        Check(sections.Contains("Cargo carried") && sections.Contains("Usually aboard") && hero != null && hero.Value == "2 070" && hero.Src == "pc",
              "cargo: the Cargo page shows Cargo carried: 2 070 item-km as the hero (Hearthwoven's own count, since install), then the average loads");
        var bar = all.FirstOrDefault(b => b.Kind == "composition" && b.Items.Any(i => i.Id == "$item_ironscrap"));
        Check(bar != null && string.Join(",", bar.Items.Select(i => i.Id)) == "$item_ironscrap,$item_wood,$item_stone,$item_coal" &&
              bar.Items[0].Icon == "item:$item_ironscrap" && bar.Items[0].Title == "Iron Scrap" && bar.Items[0].Value == "1 430" && !string.IsNullOrEmpty(bar.Items[0].Colour),
              "cargo: the bar is a composition by item, largest first, a game icon and the item's own colour per part (its shares add up: PropertyTests, every bar)");
        var aboard = all.FirstOrDefault(b => b.Kind == PanelModel.SumRowsKind);
        string Row(string id) { var r = aboard?.Items.FirstOrDefault(i => i.Id == id); return r?.Items == null ? null : r.Items[0].Value + " × " + r.Items[1].Value + " = " + r.Value; }
        Check(Row("$item_ironscrap") == "550 × 2.6 km = 1\u00a0430" && Row("$item_coal") == "50 × 1.2 km = 60" && aboard.Text == "together" && aboard.Value == "2\u00a0070" && aboard.Title == "item-km",
              "cargo 0.8.5: Usually aboard is a sum per item, the average load (item-metres / distance travelled with it aboard: 1 430 000 / 2 600 = 550) × that distance = its part of the bar, \"together 2 070 item-km\"");
        // 0.8.5 (Joost's wishes: item-km shown as a sum; say what counts, in one line)
        var counts = all.First(b => b.Kind == "section" && b.Title == "Cargo carried").Text; var sum = all.FirstOrDefault(b => b.Kind == PanelModel.SumKind);
        Check(counts == PanelModel.CargoCounts(voyager) && counts.Contains("own hold") && counts.Contains("you steer or pull it") && sum != null && sum.Text.StartsWith("item-km: items aboard × km, on a straight line.") &&
              !sum.Text.ToLowerInvariant().Contains("at least"),
              "cargo 0.8.5: the section's own line says what counts (" + counts + "); under the bar the unit first (item-km: items aboard × km, on a straight line; no \"at least\")");
        Check(SumWrong(all) == null && sum.Value == "1 430" && sum.Items[0].Value == "550" && sum.Items[1].Value == "2.6 km" && sum.Text.EndsWith(" For Iron Scrap, the item you carried most, 550 aboard on average:") && !sum.Text.Contains("trip"),
              "cargo 0.8.5: item-km as a small sum from your own numbers: 550 (Iron Scrap's Usually aboard, an average over the km) × 2.6 km = 1 430 item-km (its part of the bar), never \"a trip\": " + SumWrong(all));
        var round = PanelModel.CargoSum("$item_wood", "Wood", 369000, 3690, 576000, "the item you carried most");
        var small2 = PanelModel.CargoSum("$item_wood", "Wood", 600, 1200, 600, "the item you carried most");
        Check(round.Items[0].Value == "100" && round.Items[1].Value == "3.69 km" && round.Value == "369" && small2.Items[0].Value == "0.5" && small2.Items[1].Value == "1 200 m" && small2.Value == "600" && small2.Title == "item-meters" &&
              PanelModel.CargoSum("$item_wood", "Wood", 600, 0, 600, "the item you carried most") == null,
              "cargo 0.8.5: the sum holds as shown: where the card's rounding would break it (100 × 3.7 km is 370, the bar says 369) the distance gets a decimal (100 × 3.69 km = 369), then the load (0.5 × 1 200 m = 600 item-meters); no distance, no sum");
        // fix2-rest: the cargo sits above the crew (above the fold), its hero one modest line and its bar thin, the honest line under the bar it qualifies, Coal legible on the plate
        Check(all.Any(b => b.Kind == "section" && b.Title == "Cargo carried") && !Every(PanelModel.Content(sailing)).Any(b => b.Title == "Cargo carried" || b.Title == "Usually aboard" || b.Title == PanelModel.ServerCargoTitle) && !all.Any(b => b.Title == PanelModel.SailedWithTitle) &&
              hero.Tone == null && bar.Tone == PanelModel.Thin,
              "cargo: Cargo carried is on its own page (Sailing has none of it), the one hero form (0.8 layout D+), a thin bar");
        var coal = bar.Items.First(i => i.Id == "$item_coal");
        Check(PanelModel.Legible(coal.Colour) == coal.Colour && PanelModel.Legible("#1c1a18") != "#1c1a18" && PanelModel.Legible("#a04a2a") == "#a04a2a" && PanelModel.Legible("#d6ccb3") == "#d6ccb3" && PanelModel.Legible(null) == null,
              "cargo: a near-black item colour (Coal) is lifted so it reads on the plate; rust, birch and other colours stay as they are: Coal " + coal.Colour);
        Check(sailing.Blocks.Concat(PanelModel.Content(sailing)).Where(b => b.Kind == "journey").All(b => b.Icon == null), "sailing: the sea route has no lead mark, so its bar starts where the numbers above it start");
        Check(Every(cargoPage.Blocks).Where(b => b.Kind == "hero" && b.Title == "item-km").All(b => b.Src == "pc") && !PanelModel.AllText(cargoPage).Any(t => t.Contains(" k ") || t.EndsWith(" M")),
              "cargo: no abbreviation but km");
        var none = Page(PanelSample.Evening(now), Chapter.Voyages, "cargo");
        Check(!Every(PanelModel.Content(none)).Any(b => b.Title == "Cargo carried" || b.Title == "Usually aboard" || b.Kind == PanelModel.SumKind || (b.Text ?? "").Contains("own hold") || (b.Text ?? "").Contains("items aboard")),
              "cargo: nothing carried, nothing shown (no ghosts, no \"not measured yet\")");
        // a copy from before the distances were kept: the bar stays, the average loads (which need a distance) do not
        var noStretch = PanelSample.Voyager(PanelSample.Evening(now)); noStretch.Events.CargoStretch.Clear();
        var ns = Every(PanelModel.Content(Page(noStretch, Chapter.Voyages, "cargo"))).ToList();
        Check(ns.Any(b => b.Title == "Cargo carried") && !ns.Any(b => b.Title == "Usually aboard") && !ns.Any(b => b.Kind == PanelModel.SumKind) && ns.Any(b => b.Kind == "note" && b.Text == PanelModel.CargoUnitLine(2070000)),
              "cargo: without distances the average loads and the sum are left out, never guessed (the unit's line alone)");
        // small haul: item-metres, not item-km
        var small = new PanelInput { PlayerName = "Rowan", IsSelf = true, Character = new Dictionary<string, float> { ["DistanceSail"] = 900 }, Events = new SessionEvents(), DisplayName = _ => null };
        small.Events.CargoMeters["$item_wood"] = 600f; small.Events.CargoStretch["$item_wood"] = 40f;
        var sm = Every(PanelModel.Content(Page(small, Chapter.Voyages, "cargo"))).ToList();
        Check(sm.Any(b => b.Kind == "hero" && b.Value == "600" && b.Title == "item-meters") && sm.First(b => b.Kind == PanelModel.SumRowKind && b.Id == "$item_wood") is Block smRow && smRow.Items[0].Value == "15" && smRow.Items[1].Value == "40 m" && smRow.Value == "600" &&
              sm.First(b => b.Kind == PanelModel.SumRowsKind).Title == "item-meters" &&
              sm.First(b => b.Kind == PanelModel.SumKind) is Block smSum && smSum.Items[0].Value == "15" && smSum.Items[1].Value == "40 m" && smSum.Value == "600" && smSum.Title == "item-meters" && smSum.Text.StartsWith("item-meters: items aboard × meters"),
              "cargo: 600 item-meters over 40 m reads \"600 item-meters\", its row and its sum 15 × 40 m = 600 item-meters");
        // 0.8.5 compact: seven kinds aboard give a half island's four rows and one Other row, and still add up to the hero
        var many = new PanelInput { PlayerName = "Rowan", IsSelf = true, Character = new Dictionary<string, float> { ["DistanceSail"] = 9000 }, Events = new SessionEvents(), DisplayName = _ => null };
        foreach (var (id, im, s) in new[] { ("$item_wood", 410000f, 4100f), ("$item_stone", 170000f, 1700f), ("$item_coal", 60000f, 1200f), ("$item_ironscrap", 52000f, 650f),
                                             ("$item_copperore", 31000f, 900f), ("$item_tin", 9400f, 300f), ("$item_resin", 2500f, 2500f) })
        { many.Events.CargoMeters[id] = im; many.Events.CargoStretch[id] = s; }
        var manyPage = Every(PanelModel.Content(Page(many, Chapter.Voyages, "cargo"))).ToList();
        var manyRows = manyPage.FirstOrDefault(b => b.Kind == PanelModel.SumRowsKind);
        Check(manyRows != null && manyRows.Items.Count == PanelModel.IslandHalfRows + 1 && manyRows.Items.Last().Id == PanelModel.FoldId && manyRows.Items.Last().Title == "Other (3 kinds)" &&
              manyRows.Items.Last().Value == "43" && manyRows.Items.Last().Items == null && SumWrong(manyPage) == null,
              "cargo 0.8.5: Usually aboard stays compact: seven kinds give four rows and \"Other (3 kinds)\" with their 43 item-km together, and the rows add up to the hero: " + SumWrong(manyPage));

        // ---------- Company > Together ----------
        var group = PanelSample.Evening(now); group.PlayerId = 11; PanelSample.Voyager(group); group.Fellows = PanelSample.Fellows(now);
        var together = PanelModel.Content(Page(group, Chapter.Company, "together")).First(b => b.Kind == "together");
        var cat = together.Items.FirstOrDefault(c => c.Id == "cargo");
        Check(cat != null && cat.Title == "Cargo carried" && cat.Icon == "title:hauler" && cat.Text == "carried together" && cat.Value == "4 510 item-km" && cat.Src == "fellows",
              "cargo: Together has the category Cargo carried (the hauler's gold icon), the group's total in item-km");
        Check(cat != null && string.Join(",", cat.Items.Select(p => p.Id + "=" + p.Value)) == "Rowan=2 070 item-km,Edda=2 200 item-km,Finch=,Tor=240 item-km" &&
              Math.Abs(cat.Items.Sum(p => p.Fraction) - 1) < 1e-4 && cat.Items.Single(p => p.Id == "Finch").Fraction == 0,
              "cargo: one part per player in the fire's order, Finch (no haul) with no number; the shares sum to 1; no ranking");
        // 0.8.5: the book explains item-km one way: the chip says it in the Cargo page's own words, and no class A chip notes Earlier counts (About these numbers does)
        var cargoWords = Every(PanelModel.Content(Page(group, Chapter.Voyages, "cargo"))).Select(b => b.Text ?? "").FirstOrDefault(t => t.StartsWith(PanelModel.ItemKmUnit + ": "));
        Check(cargoWords != null && cargoWords.StartsWith(PanelModel.CargoUnitLine(2070000)) && cat.Value2.StartsWith("Recorded from ") && cat.Value2.EndsWith(" · " + PanelModel.CargoUnitLine(2070000)) &&
              cat.From == PanelModel.StartOf(group, "cargo") && together.Items.Where(c => c.Kind == "category").All(c => c.Value2 != PanelModel.EarlierIncomplete),
              "cargo 0.8.5: the cargo chip says from when Hearthwoven counts it (its own counter's start) and what an item-km is in the Cargo page's words (\"" + cat.Value2 + "\"); the other chips say no \"Earlier counts\"");
        var aloneT = PanelModel.Content(Page(voyager, Chapter.Company, "together")).FirstOrDefault(b => b.Kind == "together");
        Check(aloneT != null && aloneT.Items.Any(c => c.Id == "cargo" && c.Items.Count == 1 && c.Src == "pc"), "cargo: alone, your own row (Together keeps your row; the others join once they share)");
        var edda = group.Fellows.First(f => f.PlayerName == "Edda"); edda.ViewerName = "Rowan"; edda.PlayerNames = group.PlayerNames; edda.Fellows = group.Fellows.Where(f => f != edda).Concat(new[] { group }).ToList();
        var eddaSail = Every(PanelModel.Content(Page(edda, Chapter.Voyages, "cargo", s => s.Player = "Edda"))).ToList();
        Check(eddaSail.Any(b => b.Kind == "hero" && b.Title == "item-km" && b.Value == "2 200") && eddaSail.Any(b => b.Title == "Cargo carried") &&
              eddaSail.First(b => b.Title == "Cargo carried").Text == "Counts what is in the ship's or cart's own hold, only while Edda steers or pulls it." &&
              SumWrong(eddaSail) == null && eddaSail.First(b => b.Kind == PanelModel.SumKind).Text.Contains(", the item Edda carried most, "),
              "cargo: a fellow's book shows their cargo on their Cargo page, its line and its sum in their name (\"only while Edda steers or pulls it\", \"the item Edda carried most\"): " + SumWrong(eddaSail));
        // 0.8.5 Everyone's Cargo: the same sum for the item carried most together, its load the group's average over every km anyone carried it
        var groupCargo = Every(PanelModel.Content(Page(group, Chapter.Voyages, "cargo", s => { s.Everyone = true; s.WindowPicked = true; }))).ToList();
        var groupSum = groupCargo.FirstOrDefault(b => b.Kind == PanelModel.SumKind);
        Check(groupSum != null && SumWrong(groupCargo) == null && groupSum.Text.Contains(", the item carried most together, ") && groupCargo.Any(b => b.Kind == "note" && b.Text == PanelModel.CargoCountsGroup) &&
              !groupCargo.Any(b => (b.Text ?? "").Contains("1 item carried 1 km")),
              "cargo 0.8.5: Everyone's Cargo has the sum too, for the item carried most together (" + groupSum?.Items[0].Value + " × " + groupSum?.Items[1].Value + " = " + groupSum?.Value + "), and the line that says what counts: " + SumWrong(groupCargo));

        // ---------- Deeds > Taming ----------
        var deeds = PanelSample.DeedsRich(PanelSample.Evening(now));
        var taming = Page(deeds, Chapter.Deeds, "taming");
        var tall = Every(PanelModel.Content(taming)).ToList();
        var head = tall.FirstOrDefault(b => b.Kind == "section" && b.Title == "Born in your care");
        var grid = tall.FirstOrDefault(b => b.Kind == "strip" && b.Items.Any(i => i.Id == "Boar_piggy"));   // fix4: a strip (picture, count, name per kind), not a grid of tiles: the since-install block fits the plate
        Check(head != null && head.Value == "11" && head.Src == "pc" && grid != null && string.Join(",", grid.Items.Select(i => i.Title + "=" + i.Value)) == "Piglet=5,Chicken=3,Wolf Cub=2,Lox Calf=1",
              "born: Taming has Born in your care (11 in the head), a strip entry per creature, most first");
        var note = tall.FirstOrDefault(b => b.Kind == "note" && b.Text == "while your PC hosted them");
        Check(note != null && PanelModel.BornLine(deeds) == "while your PC hosted them", "born: labelled honestly, \"while your PC hosted them\" (the game runs breeding on the PC that owns the animals)");
        Check(grid != null && grid.Items.First(i => i.Id == "Boar_piggy").Icon == "vocab:tame-tamed" && grid.Items.All(i => !string.IsNullOrEmpty(i.Icon)),
              "born: a creature without a trophy known gets Codex's tamed mark, never an empty picture");
        var withFoe = PanelSample.DeedsRich(PanelSample.Evening(now)); withFoe.Foe = PanelSample.SampleFoe;
        Check(Every(PanelModel.Content(Page(withFoe, Chapter.Deeds, "taming"))).First(b => b.Id == "Boar_piggy").Icon == "item:TrophyBoar",
              "born: the picture is the trophy of the grown animal (Boar_piggy -> Boar -> TrophyBoar)");
        var tor = PanelSample.DeedsRich(PanelSample.Evening(now)); tor.IsSelf = false; tor.PlayerName = "Tor"; tor.ViewerName = "Rowan"; tor.Events = new SessionEvents(); CargoSample.Born(tor.Events, "Tor");
        var torT = Every(PanelModel.Content(Page(tor, Chapter.Deeds, "taming"))).ToList();
        Check(torT.Any(b => b.Kind == "section" && b.Title == "Born in Tor's care") && torT.Any(b => b.Kind == "note" && b.Text == "while Tor's PC hosted them"),
              "born: a fellow's book says whose care and whose PC (never \"you\")");
        var noBorn = Every(PanelModel.Content(Page(PanelSample.Evening(now), Chapter.Deeds, "taming"))).ToList();
        Check(!noBorn.Any(b => b.Title == "Born in your care" || b.Text == "while your PC hosted them"), "born: none counted, nothing shown");
        return fails;
    }
}
