// Heavy Keel's best load (Voyages > Cargo), the animals led (Deeds > Taming) and the feats behind them (Heavy Keel, Drover, Long Lead),
// built on the fictional sample (src/Panel/PanelSampleLed.cs). The arithmetic (CargoVoyage, LedTracker) is tested in test/Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class LedTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
        PanelView Page(PanelInput who, Chapter c, string page, Action<PanelState> more = null)
        {
            var st = new PanelState { Chapter = c }; st.Page[c] = page; more?.Invoke(st);
            return PanelModel.Build(who, st);
        }

        var full = FeatsTests.Full();
        var rowan = full;

        // ---------- Voyages > Sailing: Heavy Keel's best load ----------
        var sail = Every(PanelModel.Content(Page(rowan, Chapter.Voyages, "cargo"))).ToList();
        var tile = sail.FirstOrDefault(b => b.Kind == "item" && b.Id == "heavykeel");
        Check(tile != null && tile.Value == "140" && tile.Src == "pc" && tile.Title == "Heaviest load" && tile.Value2 == "metal and ore, 2 km",
              "cargo: one tile in the grid of loads, named as a record: 140, Heaviest load, metal and ore over 2 km, Hearthwoven's own count: " + tile?.Title + " / " + tile?.Value2);
        var allText = PanelModel.AllText(Page(rowan, Chapter.Voyages, "cargo")).ToList();
        Check(!allText.Any(t => t.Contains("at least") || t.Contains("—") || t.Contains("–")) && sail.Where(b => b.Id == "heavykeel").Distinct().Count() == 1,
              "cargo: the tile's words follow the word list (no dash, no \"at least\"), once: " + string.Join(" | ", allText.Where(t => t.Contains("at least") || t.Contains("—") || t.Contains("–"))) + " x" + sail.Count(b => b.Id == "heavykeel"));
        var ghost = Every(PanelModel.Content(Page(PanelSample.Evening(now), Chapter.Voyages, "cargo"))).ToList();
        Check(!ghost.Any(b => b.Id == "heavykeel"), "cargo: no best load, no tile (no ghosts)");
        var finch = rowan.Fellows.First(f => f.PlayerName == "Finch"); finch.ViewerName = "Rowan";
        Check(!Every(PanelModel.Content(Page(finch, Chapter.Voyages, "cargo", s => s.Player = "Finch"))).Any(b => b.Id == "heavykeel"), "cargo: a fellow with no best shared shows no tile");

        // ---------- Deeds > Taming: the animals led ----------
        var tame = Every(PanelModel.Content(Page(rowan, Chapter.Deeds, "taming"))).ToList();
        var section = tame.FirstOrDefault(b => b.Kind == "section" && b.Title == "Animals led");
        var longest = tame.FirstOrDefault(b => b.Kind == "stat" && b.Title == "longest lead");
        var line = section?.Text == "straight line, while your PC hosted them" ? section : null;   // fix4: the honest line rides on the section heading
        Check(section != null && section.Value == "6.8 km" && section.Src == "pc" && longest != null && longest.Value == "2.3 km" && longest.Src == "pc" && longest.Text.StartsWith("Wolf · ") && line != null,
              "taming: Animals led with the metres walked by tamed animals following you (6.8 km, the sum over animals), the longest lead (2.3 km, a wolf), labelled \"while your PC hosted them\"");
        Check(PanelModel.LedTotal(rowan) == 6800 && PanelModel.BestOf(rowan, "ledBestMeters").Value.What == "Wolf", "taming: the total is the sum over animals, the longest lead is one animal's");
        var finchT = Every(PanelModel.Content(Page(finch, Chapter.Deeds, "taming", s => s.Player = "Finch"))).ToList();
        Check(finchT.Any(b => b.Kind == "section" && b.Title == "Animals led" && b.Value == "3.1 km") && finchT.Any(b => b.Kind == "stat" && b.Title == "longest lead" && b.Value == "2.4 km" && b.Text.StartsWith("Boar")) &&
              finchT.Any(b => b.Kind == "section" && b.Title == "Animals led" && b.Text == "straight line, while Finch's PC hosted them"),
              "taming: a fellow's book says whose PC (never \"you\") and shows the best they shared");
        var small = new PanelInput { PlayerName = "Rowan", IsSelf = true, Character = new Dictionary<string, float>(), Events = new SessionEvents(), DisplayName = _ => null };
        small.Events.LedMeters["Boar"] = 640f;
        var smallT = Every(PanelModel.Content(Page(small, Chapter.Deeds, "taming"))).ToList();
        Check(smallT.Any(b => b.Kind == "section" && b.Title == "Animals led" && b.Value == "640 m") && !smallT.Any(b => b.Title == "longest lead"), "taming: under a kilometre reads in metres; no best shared, no longest-lead tile");
        var none = Every(PanelModel.Content(Page(PanelSample.Evening(now), Chapter.Deeds, "taming"))).ToList();
        Check(!none.Any(b => b.Title == "Animals led" || b.Title == "longest lead" || (b.Text ?? "").StartsWith("straight line, while")), "taming: nothing led, nothing shown (no ghosts)");

        // ---------- the feats behind them ----------
        var hk = PanelModel.FeatById("heavykeel"); var dr = PanelModel.FeatById("drover"); var ll = PanelModel.FeatById("longlead");
        var feats = Every(PanelModel.Content(Page(rowan, Chapter.Feats, "earned"))).Where(b => b.Kind == "feat").ToList();
        Check(new[] { "Heavy Keel", "Drover", "Long Lead" }.All(n => feats.Any(f => f.Title == n && f.Tone == "earned")) && feats.First(f => f.Title == "Drover").Level == 2,
              "feats: Heavy Keel, Drover (II) and Long Lead are earned on the sample player's Feats page");
        Check(PanelModel.FeatTierOf(dr, 0.9) == 0 && PanelModel.FeatTierOf(dr, 1) == 1 && PanelModel.FeatTierOf(dr, 5) == 2 && PanelModel.FeatTierOf(dr, 20) == 3 && PanelModel.FeatTierOf(ll, 1.99) == 0 && PanelModel.FeatTierOf(ll, 2) == 1 &&
              PanelModel.FeatTierOf(hk, 99) == 0 && PanelModel.FeatTierOf(hk, 100) == 1,
              "feats: Drover at 1, 5 and 20 km led, Long Lead at 2 km, Heavy Keel at 100 items");

        // noticing: the hooks' numbers earn the tiers with the day and biome; a fresh player on this version is counted, not "not counted yet"
        var mine = new PanelInput { PlayerName = "Rowan", IsSelf = true, Character = new Dictionary<string, float>(), Events = new SessionEvents(), Feats = new FeatsLedger { Primed = true }, DisplayName = _ => null };
        Check(!PanelModel.FeatWaiting(hk, mine) && !PanelModel.FeatWaiting(ll, mine) && !PanelModel.FeatWaiting(dr, mine) && PanelModel.FeatValue(hk, mine) == 0, "feats: on this PC with no best yet they read 0 and are counted, not \"does not count this yet\"");
        mine.Feats.NoteBest(CargoVoyage.BestKey, 104, now, "Ocean"); mine.Feats.NoteBest(LedTracker.BestKey, 2050, now, "Meadows", "Wolf"); mine.Events.LedMeters["Wolf"] = 1200f;
        var earned = PanelModel.EvaluateFeats(mine, now, "Ocean", "aboard");
        Check(mine.Feats.Tier("heavykeel") == 1 && mine.Feats.Tier("longlead") == 1 && mine.Feats.Tier("drover") == 1 && earned.Count == 3, "feats: 104 items over 2 km, a 2 050 m lead and 1.2 km led earn Heavy Keel, Long Lead and Drover I");
        mine.Feats.NoteBest(CargoVoyage.BestKey, 90, now, "Swamp");
        Check(mine.Feats.Count(CargoVoyage.BestKey) == 104 && PanelModel.EvaluateFeats(mine, now, "Swamp", null).Count == 0, "feats: a lighter voyage after changes nothing, nothing is earned twice");

        // ---------- what travels in the snapshot ----------
        var snap = Snapshot.Build("0.6.0", 11, "Rowan", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", new DamageTally(), events: full.Events, feats: full.Feats);
        Check(snap.Contains("\"featBests\":{") && snap.Contains("\"ledBestMeters\":{\"v\":2300") && snap.Contains("\"cargoBestVoyage\":{\"v\":140") && snap.Contains("\"ledMeters\":{") && !snap.Contains("aboard"),
              "snapshot: the bests and the led metres travel; the place word never does");
        var read = PanelInput.FromSnapshot(snap);
        Check(read.Feats != null && read.Feats.BestsShared && PanelModel.FeatValue(hk, read) == 140 && PanelModel.FeatValue(ll, read) == 2.3 && PanelModel.FeatTier(hk, read) == 1 && read.Feats.Tier("drover") == 2 &&
              PanelModel.BestOf(read, "ledBestMeters").Value.What == "Wolf" && !PanelModel.FeatWaiting(hk, read),
              "snapshot: a fellow's copy reads back their best load and best lead, with their tiers; counted, not waiting");
        var noBests = PanelInput.FromSnapshot(Snapshot.Build("0.6.0", 11, "Old", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", new DamageTally(), feats: new FeatsLedger()).Replace(",\"featBests\":{}", ""));
        noBests.Feats = noBests.Feats ?? new FeatsLedger();
        Check(PanelModel.FeatWaiting(hk, noBests) && PanelModel.FeatWaiting(ll, noBests), "snapshot: a copy from a sender that does not share the bests says Hearthwoven does not count Heavy Keel and Long Lead for them");
        var emptyShare = PanelInput.FromSnapshot(Snapshot.Build("0.6.0", 11, "New", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", new DamageTally(), feats: new FeatsLedger()));
        Check(emptyShare.Feats != null && emptyShare.Feats.BestsShared && !PanelModel.FeatWaiting(hk, emptyShare) && PanelModel.FeatValue(hk, emptyShare) == 0, "snapshot: a sender that shares but has no best yet is counted (0), not \"does not count this yet\"");
        return fails;
    }
}
