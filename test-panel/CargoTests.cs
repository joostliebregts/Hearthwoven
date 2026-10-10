// Tests for 0.6's two pages (src/Panel/Chapters/CargoModel.cs): Cargo carried (Voyages > Cargo, Company > Together) and
// Born in your care (Deeds > Taming), built on the sample evening (src/Panel/PanelSampleCargo.cs, fictional players).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class CargoTests
{
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
        Check(bar != null && string.Join(",", bar.Items.Select(i => i.Id)) == "$item_ironscrap,$item_wood,$item_stone,$item_coal" && Math.Abs(bar.Items.Sum(i => i.Fraction) - 1) < 1e-4 &&
              bar.Items[0].Icon == "item:$item_ironscrap" && bar.Items[0].Title == "Iron Scrap" && bar.Items[0].Value == "1 430" && !string.IsNullOrEmpty(bar.Items[0].Colour),
              "cargo: the bar is a composition by item, largest first, a game icon and the item's own colour per part, shares sum to 1");
        var tiles = all.FirstOrDefault(b => b.Kind == "itemgrid" && b.Items.Any(i => i.Id == "$item_ironscrap"));
        var scrap = tiles?.Items.FirstOrDefault(i => i.Id == "$item_ironscrap");
        Check(scrap != null && scrap.Value == "550" && scrap.Value2 == "carried 2.6 km" && tiles.Items.First(i => i.Id == "$item_coal").Value == "50" && tiles.Items.First(i => i.Id == "$item_coal").Value2 == "carried 1.2 km",
              "cargo: per item the average load (item-metres / distance travelled with it aboard: 1 430 000 / 2 600 = 550) and that distance");
        var line = all.FirstOrDefault(b => b.Kind == "note" && b.Text == PanelModel.CargoNote(2070000));
        Check(line != null && line.Text.StartsWith("item-km: 1 item carried 1 km") && line.Text.Contains("at the helm or pulling a cart") && line.Text.Contains("straight line") && !line.Text.ToLowerInvariant().Contains("at least"),
              "cargo: one line says first what an item-km is (1 item carried 1 km), then at the helm or pulling a cart, and straight line (in the panel's words: no \"at least\")");
        // fix2-rest: the cargo sits above the crew (above the fold), its hero one modest line and its bar thin, the honest line under the bar it qualifies, Coal legible on the plate
        Check(all.FindIndex(b => b.Kind == "section" && b.Title == "Cargo carried") >= 0 && !Every(PanelModel.Content(sailing)).Any(b => b.Title == "Cargo carried" || b.Title == "Usually aboard" || b.Title == PanelModel.ServerCargoTitle) && !all.Any(b => b.Title == PanelModel.SailedWithTitle) &&
              hero.Tone == null && bar.Tone == PanelModel.Thin && all.FindIndex(b => b.Text == PanelModel.CargoNote(2070000)) > all.IndexOf(bar) && all.FindIndex(b => b.Text == PanelModel.CargoNote(2070000)) < all.FindIndex(b => b.Kind == "section" && b.Title == "Usually aboard"),
              "cargo: Cargo carried is on its own page (Sailing has none of it), the one hero form (0.8 layout D+), a thin bar, the honest line right under the bar");
        var coal = bar.Items.First(i => i.Id == "$item_coal");
        Check(PanelModel.Legible(coal.Colour) == coal.Colour && PanelModel.Legible("#1c1a18") != "#1c1a18" && PanelModel.Legible("#a04a2a") == "#a04a2a" && PanelModel.Legible("#d6ccb3") == "#d6ccb3" && PanelModel.Legible(null) == null,
              "cargo: a near-black item colour (Coal) is lifted so it reads on the plate; rust, birch and other colours stay as they are: Coal " + coal.Colour);
        Check(sailing.Blocks.Concat(PanelModel.Content(sailing)).Where(b => b.Kind == "journey").All(b => b.Icon == null), "sailing: the sea route has no lead mark, so its bar starts where the numbers above it start");
        Check(Every(cargoPage.Blocks).Where(b => b.Kind == "hero" && b.Title == "item-km").All(b => b.Src == "pc") && !PanelModel.AllText(cargoPage).Any(t => t.Contains(" k ") || t.EndsWith(" M")),
              "cargo: no abbreviation but km");
        var none = Page(PanelSample.Evening(now), Chapter.Voyages, "cargo");
        Check(!Every(PanelModel.Content(none)).Any(b => b.Title == "Cargo carried" || b.Title == "Usually aboard" || (b.Text ?? "").Contains(PanelModel.CargoLine)),
              "cargo: nothing carried, nothing shown (no ghosts, no \"not measured yet\")");
        // a copy from before the distances were kept: the bar stays, the average loads (which need a distance) do not
        var noStretch = PanelSample.Voyager(PanelSample.Evening(now)); noStretch.Events.CargoStretch.Clear();
        var ns = Every(PanelModel.Content(Page(noStretch, Chapter.Voyages, "cargo"))).ToList();
        Check(ns.Any(b => b.Title == "Cargo carried") && !ns.Any(b => b.Title == "Usually aboard"), "cargo: without distances the average loads are left out, never guessed");
        // small haul: item-metres, not item-km
        var small = new PanelInput { PlayerName = "Rowan", IsSelf = true, Character = new Dictionary<string, float> { ["DistanceSail"] = 900 }, Events = new SessionEvents(), DisplayName = _ => null };
        small.Events.CargoMeters["$item_wood"] = 600f; small.Events.CargoStretch["$item_wood"] = 40f;
        var sm = Every(PanelModel.Content(Page(small, Chapter.Voyages, "cargo"))).ToList();
        Check(sm.Any(b => b.Kind == "hero" && b.Value == "600" && b.Title == "item-meters") && sm.First(b => b.Kind == "item" && b.Id == "$item_wood").Value == "15" && sm.First(b => b.Kind == "item").Value2 == "carried 40 m",
              "cargo: 600 item-meters over 40 m reads \"600 item-meters\" and \"15, over 40 m in all\"");

        // ---------- Company > Together ----------
        var group = PanelSample.Evening(now); group.PlayerId = 11; PanelSample.Voyager(group); group.Fellows = PanelSample.Fellows(now);
        var together = PanelModel.Content(Page(group, Chapter.Company, "together")).First(b => b.Kind == "together");
        var cat = together.Items.FirstOrDefault(c => c.Id == "cargo");
        Check(cat != null && cat.Title == "Cargo carried" && cat.Icon == "title:hauler" && cat.Text == "carried together" && cat.Value == "4 510 item-km" && cat.Src == "fellows",
              "cargo: Together has the category Cargo carried (the hauler's gold icon), the group's total in item-km");
        Check(cat != null && string.Join(",", cat.Items.Select(p => p.Id + "=" + p.Value)) == "Rowan=2 070 item-km,Edda=2 200 item-km,Finch=,Tor=240 item-km" &&
              Math.Abs(cat.Items.Sum(p => p.Fraction) - 1) < 1e-4 && cat.Items.Single(p => p.Id == "Finch").Fraction == 0,
              "cargo: one part per player in the fire's order, Finch (no haul) with no number; the shares sum to 1; no ranking");
        Check(cat.Value2.StartsWith("Recorded from ") && cat.Value2.Contains("item-km: 1 item carried 1 km") && cat.Value2.EndsWith("straight line, so the real figure is higher") && cat.From == PanelModel.StartOf(group, "cargo") &&
              together.Items.Where(c => c.Kind == "category" && c.Id != "cargo" && c.Id != "dealt" && c.Id != "built" && c.Id != "sailed").All(c => c.Value2 == null || c.Value2 == PanelModel.EarlierIncomplete) &&
              together.Items.Single(c => c.Id == "dealt").Value2 == null && PanelModel.CargoUnitWords(840) == "1 item carried 1 m" && PanelModel.CargoUnitWords(2070000) == "1 item carried 1 km",
              "cargo 0.7: the cargo chip says from when Hearthwoven counts it (its own counter's start), and what an item-km is (1 item carried 1 km) and that the straight line is a floor; the class A chips say at most \"Earlier counts may be incomplete.\"");
        var aloneT = PanelModel.Content(Page(voyager, Chapter.Company, "together")).FirstOrDefault(b => b.Kind == "together");
        Check(aloneT != null && aloneT.Items.Any(c => c.Id == "cargo" && c.Items.Count == 1 && c.Src == "pc"), "cargo: alone, your own row (Together keeps your row; the others join once they share)");
        var edda = group.Fellows.First(f => f.PlayerName == "Edda"); edda.ViewerName = "Rowan"; edda.PlayerNames = group.PlayerNames; edda.Fellows = group.Fellows.Where(f => f != edda).Concat(new[] { group }).ToList();
        var eddaSail = Every(PanelModel.Content(Page(edda, Chapter.Voyages, "cargo", s => s.Player = "Edda"))).ToList();
        Check(eddaSail.Any(b => b.Kind == "hero" && b.Title == "item-km" && b.Value == "2 200") && eddaSail.Any(b => b.Title == "Cargo carried"), "cargo: a fellow's book shows their cargo on their Cargo page");

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
