// Tests for the Voyages and Hall chapters (src/Panel/Chapters/VoyagesHallModel.cs): the approved pages Voyages Overview,
// Sailing, On foot, Maps and Hall Overview, Trader, Smelters, built on the sample evening plus a voyager's numbers.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Hearthwoven;
using Hearthwoven.Panel;

static partial class Program
{
    /// <summary>The sample evening plus the numbers the Voyages and Hall pages show (fictional, after the prototypes).</summary>
    internal static PanelInput VoyagesHallSample(PanelInput i) => PanelSample.Voyager(i);   // src/Panel/PanelSample.cs

    static IEnumerable<Block> All(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(All(b.Items ?? new List<Block>())));
    static Block First(PanelView v, string kind, Func<Block, bool> where = null) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind && (where == null || where(b)));
    static string Seq(IEnumerable<string> s) => string.Join(",", s);

    internal static void VoyagesHallChecks(PanelInput voyager, Action<bool, string> Check)
    {
        PanelView Show(Chapter c, string page = null, PanelInput who = null)
        {
            var st = new PanelState { Chapter = c }; if (page != null) st.Page[c] = page;
            return PanelModel.Build(who ?? voyager, st);
        }

        // ---------- formats ----------
        Check(PanelModel.Minutes(0) == "0 min" && PanelModel.Minutes(30) == "under 1 min" && PanelModel.Minutes(1260) == "21 min" && PanelModel.Minutes(3600) == "1 hour" &&
              PanelModel.Minutes(22200) == "6 hours 10 min", "VH words: minutes as the word list writes them (only km and min shortened)");

        // ---------- Voyages > Overview ----------
        var over = Show(Chapter.Voyages);
        Check(Seq(over.List.Select(l => l.Label)) == "Overview,Sailing,Cargo,On foot,Maps" && over.Page == "overview" && over.Heading == "Voyages", "VH voyages: Overview first, then Sailing, Cargo, On foot, Maps");
        var journey = First(over, "journey");
        Check(journey != null && journey.Src == "character" && Seq(journey.Items.Select(l => l.Id + "=" + l.Value + " " + l.Title)) == "foot=48.2 km on foot,sail=21.5 km sailed" &&
              Math.Abs(journey.Items[0].Fraction - 48200f / 69700f) < 1e-4 && Math.Abs(journey.Items.Sum(l => l.Fraction) - 1) < 1e-4,
              "VH journey: one line, on foot (walking and running) and sailed, each as long as its share");
        var sea = journey.Items[1].Items;
        Check(Seq(sea.Select(s => s.Id + "=" + s.Value + "=" + s.Title)) == "helm=6.6 km=at the helm,passenger=14.9 km=as passenger" && Math.Abs(sea.Sum(s => s.Fraction) - 1) < 1e-4 && sea.All(s => s.Src == "character"),
              "VH journey: sailed splits into at the helm and as passenger (sailed minus at the helm)");
        var home = First(over, "composition", b => b.Title == PanelModel.HomeAway);
        Check(home != null && Seq(home.Items.Select(p => p.Title + "=" + p.Value)) == "resting at home=6 hours 10 min,away from home=11 hours 40 min" && home.Src == "character" && !home.SinceInstall,
              "VH home and away: the game's time in base and away, your character's count (no label)");
        var crew = First(over, "crew");
        var crewHead = PanelModel.Content(over).Single(b => b.Kind == "section" && b.Title == PanelModel.SailedWithTitle);
        Check(crew != null && Seq(crew.Items.Select(p => p.Title + "=" + p.Value)) == "Edda=21 min,Finch=10 min,Tor=8 min" && crew.Items[0].Fraction == 1 && crew.Items.All(p => p.Icon == "person:" + p.Title && p.Src == "pc") &&
              Zoned.Says(over, crewHead) && !crew.SinceInstall && !over.HeadingSinceInstall, "VH crew: fellow players by name (never ranked), minutes aboard, since install once on the section");
        Check(crew.Items.All(p => p.Items == null), "VH crew: no whose-helm split per fellow (the mod does not record it)");

        // ---------- Voyages > Sailing ----------
        var sailing = Show(Chapter.Voyages, "sailing");
        Check(sailing.Heading == "Sailing" && First(sailing, "hero", b => b.Title == "km sailed")?.Value == "21.5" && sailing.Badges.Any(b => b.Label == "Helmskeeper") &&
              Seq(First(sailing, "journey").Items.Single().Items.Select(s => s.Value)) == "6.6 km,14.9 km" && First(sailing, "journey").Icon == null,
              "VH sailing: km sailed, the sea route at the helm and as passenger, the Helmskeeper title");
        var helm = First(sailing, "composition", b => b.Title == PanelModel.UnderEachHelm);
        Check(helm != null && Seq(helm.Items.Select(p => p.Title + "=" + p.Value + "=" + p.Colour)) == "Edda=15 min=person:Edda,Tor=5 min=person:Tor" && helm.Src == "pc",
              "VH sailing: minutes under each fellow's helm, in their colours");
        Check(First(sailing, "hero").Items.Single().Title == "leviathans sunk" && First(sailing, "hero").Items.Single().Value == "2" && First(sailing, "hero").Items.Single().Src == "character", "VH sailing: leviathans sunk, the game's count, beside the km in the hero");
        var helmPages = new List<string>();
        foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
            foreach (var l in Show(ch).List)
                if (All(Show(ch, l.Id).Blocks).Any(b => b.Kind == "segment" && b.Id == "helm") || PanelModel.AllText(Show(ch, l.Id)).Any(t => t.Contains("at the helm") && t.Any(char.IsDigit) && !t.StartsWith("item-"))) helmPages.Add(ch + "/" + l.Id);
        Check(Seq(helmPages.Distinct()) == "Voyages/overview,Voyages/sailing,Feats/titles", "VH owner: the helm distance shows on Voyages > Overview (the journey) and Sailing only, and as Helmskeeper's reason on Feats > Titles (B18): " + Seq(helmPages.Distinct()));

        // ---------- Voyages > On foot ----------
        var foot = Show(Chapter.Voyages, "onfoot");
        Check(First(foot, "hero")?.Value == "48.2" && First(foot, "hero").Title == "km on foot" && Seq(First(foot, "journey").Items.Single().Items.Select(s => s.Title + "=" + s.Value + "=" + s.Tone)) == "walking=39.1 km=dots,running=9.1 km=dash",
              "VH on foot: km on foot, walking and running as one line");
        Check(Seq(First(foot, "rows").Items.Select(r => r.Title + "=" + r.Value)) == "Jumps made=412,In the air, not in the km above=2.6 km", "VH on foot: jumps and the air");
        var move = First(foot, "ladders");
        Check(move != null && move.Items.Single().Title == PanelModel.MovementSkills && Seq(move.Items.Single().Items.Select(s => s.Id + "=" + s.Value + (s.Practised ? "*" : ""))) == "Jump=28,Run=55*,Swim=9" && move.Src == "character",
              "VH on foot: the Jump, Run and Swim ladders (the existing ladders kind), Run practised since install");

        // ---------- Voyages > Maps ----------
        var maps = Show(Chapter.Voyages, "maps");
        Check(Seq(PanelModel.Content(maps).Where(b => b.Kind == "rows").SelectMany(r => r.Items).Select(r => r.Title + "=" + r.Value + "=" + r.Src)) == "Buried treasures=4=character,Dungeon treasures=3=character,Other treasures=6=character,Dungeons entered=9=character,Portal trips=37=character,Maps shared=2=pc",
              "VH maps: found, in the prototype's order, your character's counts");
        var shared = PanelModel.Content(maps).Where(b => b.Kind == "rows").SelectMany(r => r.Items).Last();
        Check(Zoned.Says(maps, shared) && PanelModel.Content(maps).Where(b => b.Kind == "rows").SelectMany(r => r.Items).Count(c => Zoned.Says(maps, c)) == 1, "VH maps: your map shared at the table, since install (only that row)");
        var compass = First(maps, "compass");
        Check(compass != null && Seq(compass.Items.Select(a => a.Title + "=" + a.Value)) == "North=14 min,East=22 min,South=3 min,West=9 min" && compass.Items.Single(a => a.Id == "east").Fraction == 1 && compass.Src == "character",
              "VH compass: time at the far edge, east and west unmirrored from the game's names, the longest arm full length");
        var bios = First(maps, "biometiles");
        Check(bios != null && Seq(bios.Items.Select(t => t.Title)) == "Meadows,Black Forest,Swamp,Mountains,Ocean" && bios.Items.All(t => t.Icon.StartsWith("vocab:biome-") && t.Colour.StartsWith("#")) &&
              bios.Items[0].Tone == "dark-text" && bios.Items[3].Colour == "#55657a", "VH biomes found: your character's biomes in journey order, Ocean last, emblems and tile colours");
        var lost = VoyagesHallSample(new PanelInput { Character = new Dictionary<string, float>(), SkillLevels = new Dictionary<string, float>(), SkillProgress = new Dictionary<string, float>(), Events = new SessionEvents() });
        lost.KnownBiomes = null;
        Check(First(Show(Chapter.Voyages, "maps", lost), "biometiles") == null, "VH biomes found: none when the biomes are not known (a fellow's shared copy): never guessed");

        // ---------- Hall ----------
        var hall = Show(Chapter.Stores);
        Check(hall.Chapters.Single(c => c.Id == "Stores").Label == "Hall" && hall.ListTitle == "Hall" && Seq(hall.List.Select(l => l.Label)) == "Overview,Trader,Smelters" && hall.Heading == PanelModel.HallOverview,
              "VH hall: the chapter is Hall, its list Overview, Trader, Smelters");
        var hallText = PanelModel.AllText(hall).Concat(PanelModel.AllText(Show(Chapter.Stores, "trader"))).Concat(PanelModel.AllText(Show(Chapter.Stores, "smelters"))).ToList();
        Check(!hallText.Any(t => t.IndexOf("chest", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("cart", StringComparison.OrdinalIgnoreCase) >= 0 || t.Contains("Stowed") || t.Contains("Drawn")),
              "VH hall: chest and cart records (server-only) are not shown");
        var comps = PanelModel.Content(hall).Where(b => b.Kind == "composition").ToList(); var coins = comps[0];   // under their heroes, untitled
        var heroes = PanelModel.Content(hall).Where(b => b.Kind == "hero").ToList();
        Check(coins != null && coins.Value == null && heroes[0].Value == "650" && heroes[0].Title == "coins spent" && Seq(coins.Items.Select(p => p.Title + "=" + p.Value + "=" + p.Colour)) == "Haldor=350=#e8a948,Hildir=300=#b8708c" && coins.Src == "pc",
              "VH hall: coins spent per trader as one composition, the traders' colours");
        var hallAll = PanelModel.Content(hall);
        var boughtHead = hallAll.FirstOrDefault(b => b.Kind == "section" && b.Title.EndsWith(" bought"));
        var ledger = hallAll[hallAll.IndexOf(boughtHead) + 1];
        Check(boughtHead?.Title == "25 items bought" && ledger.Kind == "itemgrid" && ledger.Src == "pc" && Seq(ledger.Items.Select(r => r.Kind + ":" + r.Title + "=" + r.Value)) == "item:Fishing Bait=20,item:Ymir Flesh=3,item:Megingjord=1,item:Hat=1" &&
              ledger.Items.All(r => r.Icon.StartsWith("item:")), "VH hall: what was bought as an item grid, most first, the full list (coins per item are not recorded)");
        var smelt = comps[1];
        Check(smelt != null && heroes[1].Value == "127" && heroes[1].Title == "items put in" && Seq(smelt.Items.Select(p => p.Title + "=" + p.Value)) == "Ore=60,Wood=40,Fuel=27" && Math.Abs(smelt.Items.Sum(p => p.Fraction) - 1) < 1e-4,
              "VH hall: put in the smelters by kind (ore, fuel, and wood in the kiln on its own)");
        var byItem = hallAll.Last(b => b.Kind == "itemgrid");   // the smelted items follow the smelters bar, no "by item" heading
        Check(byItem.Kind == "itemgrid" && Seq(byItem.Items.Select(r => r.Title + "=" + r.Value)) == "Wood=40,Copper Ore=30,Coal=27,Tin Ore=20,Black Metal Scrap=10", "VH hall: smelters by item, the vanilla fuel named (Coal), top five");
        Check(Zoned.Labels(hall) == 1 && All(hall.Blocks).Count(b => b.SinceInstall) == 0, "VH hall: every number from this PC: since install once, after the heading");
        var trader = Show(Chapter.Stores, "trader");
        var trLedger = PanelModel.Content(trader).FirstOrDefault(b => b.Kind == "ledger");
        Check(trader.Heading == "Trader" && trader.Badges.Any(b => b.Label == "Far Trader") && trLedger != null && Seq(trLedger.Items.Select(r => r.Title + "=" + r.Colour)) == "Haldor=#e8a948,Hildir=#b8708c" &&
              Seq(trLedger.Items[0].Items.Select(c => c.Kind + ":" + c.Title + "=" + c.Value + "=" + c.Colour)) == "chip:Fishing Bait=20=#e8a948,chip:Ymir Flesh=3=#e8a948,chip:Megingjord=1=#e8a948" &&
              trLedger.Items.SelectMany(r => r.Items).All(c => c.Src == "pc" && c.Icon.StartsWith("item:")),
              "fix2 7 trader: one ledger row per trader (most coins first), what was bought there as chips edged in the trader's colour; the Far Trader title");
        var trHero = PanelModel.Content(trader).First(b => b.Kind == "hero"); var trBar = PanelModel.Content(trader).First(b => b.Kind == "composition");
        Check(trHero.Tone == PanelModel.Compact && trHero.Value == "650" && trBar.Tone == PanelModel.Thin && !PanelModel.Content(trader).Any(b => b.Kind == "itemgrid" || b.Kind == "columns"),
              "fix2 7 trader: a compact hero with the bar right under it at reduced height, no tall tiles");
        var smelters = Show(Chapter.Stores, "smelters");
        var smAll = PanelModel.Content(smelters); var smLedger = smAll.FirstOrDefault(b => b.Kind == "ledger");
        Check(smLedger != null && Seq(smLedger.Items.Select(r => r.Title)) == "Blast Furnace,Charcoal Kiln,Smelter" &&
              Seq(smLedger.Items[0].Items.Select(r => r.Title + "=" + r.Value + "=" + (r.Tone ?? ""))) == "Coal=15=fuel,Black Metal Scrap=10=" &&
              smAll.First(b => b.Kind == "hero").Tone == PanelModel.Compact && smAll.First(b => b.Kind == "composition").Tone == PanelModel.Thin,
              "fix2 7 smelters: a compact hero and thin bar, then one ledger row per station by its game name, its goods as chips, the fuel marked as fuel");
        var smBar = smAll.First(b => b.Kind == "composition"); string PartColour(string id) => smBar.Items.Single(p => p.Id == id).Colour;
        Check(PartColour("Fuel") == PanelModel.FuelColour && PartColour("Ore") == PanelModel.OreColour && PartColour("Wood") != PartColour("Fuel") &&
              smLedger.Items.SelectMany(r => r.Items).All(c => c.Colour == (c.Tone == "fuel" ? PanelModel.FuelColour : c.Title == "Wood" ? PartColour("Wood") : PanelModel.OreColour)) &&
              smLedger.Items.SelectMany(r => r.Items).Where(c => c.Tone == "fuel").Sum(c => int.Parse(c.Value)) == int.Parse(smBar.Items.Single(p => p.Id == "Fuel").Value),
              "fix2 7 smelters: fuel in one distinct colour (not Wood's), each chip edged in its part's colour, the fuel chips add up to the bar's Fuel");
        Check(Seq(ledger.Items.Select(i => i.Title + "=" + i.Colour)) == "Fishing Bait=#e8a948,Ymir Flesh=#e8a948,Megingjord=#e8a948,Hat=#b8708c" &&
              byItem.Items.Single(i => i.Title == "Coal").Tone == "fuel" && byItem.Items.Where(i => i.Title != "Coal").All(i => i.Tone == null),
              "fix2 7 hall overview: each bought item edged in its trader's colour (Haldor gold, Hildir rose); Coal marked as fuel");

        // ---------- every page: sources, labels, words, empty states, JSON ----------
        var views = new List<PanelView>();
        foreach (var who in new[] { voyager, new PanelInput() })
            foreach (var ch in new[] { Chapter.Voyages, Chapter.Stores })
                foreach (var l in Show(ch, null, who).List) views.Add(Show(ch, l.Id, who));
        string bad = null;
        foreach (var v in views)
            foreach (var b in All(v.Blocks))
            {
                if ((b.Value ?? "").Any(char.IsDigit) && (b.Src == null || b.Source == null)) bad ??= v.Page + ": no source on " + (b.Title ?? b.Value);
                if (b.SinceInstall && b.Kind != "section" && b.Src != "pc") bad ??= v.Page + ": since install on a " + b.Src + " number";
            }
        Check(bad == null, "VH sources: every number carries Src and Source; since install only on numbers from this PC" + (bad != null ? ": " + bad : ""));
        var unlabelled = views.Where(v => All(v.Blocks).Any(b => b.Src == "pc") && Zoned.Labels(v) == 0).Select(v => v.Page).ToList();
        Check(unlabelled.Count == 0, "VH since install: every page with a number from this PC says so" + (unlabelled.Count > 0 ? ": " + unlabelled[0] : ""));
        var text = views.SelectMany(PanelModel.AllText).ToList();
        var abbr = text.FirstOrDefault(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"\b\d+ (h|hr|hrs|s|sec|m)\b") || t.Contains('—') || t.Contains('–') ||
                                            System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(enemy|enemies|picked up|taken|used|ate)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        Check(abbr == null, "VH words: no abbreviations but km and min, no dashes, no banned words" + (abbr != null ? ": " + abbr : ""));
        Check(views.Where(v => v.Page != null).All(v => v.Blocks.Count > 0) && views.Skip(views.Count / 2).All(v => PanelModel.Content(v).Any(b => b.Kind == "empty")) && views.All(v => PanelModel.PlateOf(v) != null), "VH empty: every Voyages and Hall page survives no data with the agreed empty line");
        var json = true;
        foreach (var v in views) try { JsonDocument.Parse(PanelModel.ToJson(v)); } catch { json = false; }
        Check(json, "VH preview JSON is valid for every Voyages and Hall page");
    }
}
