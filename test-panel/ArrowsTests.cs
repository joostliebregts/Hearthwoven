// Tests for the 0.9 arrows track (src/Panel/Chapters/BestArrows.cs; work/hearthwoven-0.9/DESIGN.md "Best arrows for these foes" and "Best arrow
// tie"): the tie named in Foes' Best arrow column (Joost's in-game case of 18 Oct: Fallen Valkyrie, Charred Arrow 82 = Silver Arrow 82, the
// Bloodgold Arrow 100 not known yet; his own book showed no Best arrow, a friend's showed Frost Arrow), the Best arrows island on Foes and Last
// fight (You know, weighted by your damage to each foe; From what you gathered here, by the land of each pickup minute), its states and its
// place. On the sample Ashlands evening (src/Panel/PanelSampleArrows.cs) and the sample world. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class ArrowsTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static IEnumerable<Block> Each(Block b) => new[] { b }.Concat((b.Items ?? new List<Block>()).SelectMany(Each));
    static PanelState Battle(TimeWindow w, string page)
    {
        var s = new PanelState { Chapter = Chapter.Battle, Window = w, WindowPicked = true }; s.Page[Chapter.Battle] = page;
        return s;
    }
        static Block Group(Block island, string id) => island?.Items?.FirstOrDefault()?.Items?.FirstOrDefault(g => g.Id == id);
    static string Lines(Block g) => g == null ? "(none)" : string.Join(", ", (g.Items ?? new List<Block>()).Select(i => i.Title + " " + i.Text + " " + i.Value));

    public static int Run(DateTime now)
    {
        fails = 0;
        var arrows = ArrowsSample.Arrows();
        var valkyrie = ArrowsSample.Foe(ArrowsSample.Valkyrie);

        // ---------- the tie, Joost's case: Fallen Valkyrie, Charred 82 = Silver 82, Bloodgold 100 not known yet ----------
        var own = ArrowsSample.World(now);
        var known = PanelModel.KnownArrows(own);
        Check(known.Any(a => a.Prefab == "ArrowCharred") && known.Any(a => a.Prefab == "ArrowSilver") && !known.Any(a => a.Prefab == "ArrowBloodgold") &&
              Math.Abs(PanelModel.Expected(valkyrie, arrows.First(a => a.Prefab == "ArrowCharred")) - 82) < 1e-6 && Math.Abs(PanelModel.Expected(valkyrie, arrows.First(a => a.Prefab == "ArrowSilver")) - 82) < 1e-6 &&
              Math.Abs(PanelModel.Expected(valkyrie, arrows.First(a => a.Prefab == "ArrowBloodgold")) - 100) < 1e-6,
              "T the case: on the Fallen Valkyrie the Charred and Silver Arrows both do 82, the Bloodgold Arrow 100 and is not known yet");
        Check(PanelModel.BestArrow(valkyrie, known) == null, "T before the fix: the tie at the top named no best arrow (BestArrow, unchanged: null), so Joost's own book showed no Best arrow");
        var cell = PanelModel.ArrowCell(own, valkyrie, known);
        Check(cell != null && cell.Kind == "arrow" && cell.Title == "Charred or Silver Arrow" && cell.Tone == PanelModel.TieTone && cell.Text == "They do the same damage to this foe" &&
              cell.Items.Select(i => i.Id).SequenceEqual(new[] { "ArrowCharred", "ArrowSilver" }) && cell.Icon == null && cell.Items[0].Icon == "item:ArrowCharred",
              "T now: a tie of two is named \"" + cell?.Title + "\", the hover says \"" + cell?.Text + "\"");
        var friend = own.ShallowCopy(); friend.IsSelf = false; friend.RecipeKnown = null; friend.Owned = null;
        friend.ItemsCrafted = new Dictionary<string, float> { ["$item_arrow_wood"] = 40, ["$item_arrow_frost"] = 20 }; friend.ItemsPickedUp = new Dictionary<string, float> { ["$item_arrow_fire"] = 10 };
        var friendCell = PanelModel.ArrowCell(friend, valkyrie, PanelModel.KnownArrows(friend));
        Check(friendCell != null && friendCell.Title == "Frost Arrow" && friendCell.Tone == null, "T a friend's book (arrows made or brought in: Wood, Fire, Frost) names Frost Arrow, as Joost saw");
        var rows = new List<PanelModel.DamageRow> { new PanelModel.DamageRow { Dir = "dealt", Other = ArrowsSample.Valkyrie, Cause = "Bows", Type = "pierce", Amount = 400, Biome = "AshLands" } };
        var table = PanelModel.FoeTable(own, rows);
        var tableCell = table.Items.Single().Items.FirstOrDefault(i => i.Kind == "arrow");
        Check(tableCell != null && tableCell.Title == "Charred or Silver Arrow", "T Foes' table: the Fallen Valkyrie row carries the tie in its Best arrow cell, so the column shows on Joost's own book");
        var three = new List<PanelModel.ArrowData>(known) { new PanelModel.ArrowData { Prefab = "ArrowTwin", Token = "$item_arrow_twin", Damage = { ["pierce"] = 82 } } };
        var cell3 = PanelModel.ArrowCell(own, valkyrie, three);
        Check(cell3.Title == "3 arrows tied" && cell3.Text.StartsWith(PanelModel.TieHover + ": ") && new[] { "Charred Arrow", "Silver Arrow", "Arrow Twin" }.All(cell3.Text.Contains) && cell3.Items.Count == 3,
              "T three or more tied: \"" + cell3.Title + "\", the hover names them all (\"" + cell3.Text + "\")");
        Check(PanelModel.TiedName(new[] { "Wood Arrow", "Bone Bolt" }) == "Wood Arrow or Bone Bolt" && PanelModel.TiedName(new[] { "Needle Arrow", "Frost Arrow" }) == "Needle or Frost Arrow",
              "T the shared last word is said once only when both names end in it");
        Check(PanelModel.ArrowCell(own, valkyrie, known.Take(1).ToList()) == null && PanelModel.ArrowCell(own, null, known) == null &&
              PanelModel.ArrowCell(own, ArrowsSample.Foe(ArrowsSample.Charred), known)?.Title == "Silver Arrow",
              "T the column needs two known arrows and the foe's data; a single best is named as before (Charred Warrior: Silver Arrow)");
        Check(PanelModel.FoesAbout.Single(l => l.title == "Best arrow").text.Contains("same damage") && PanelModel.FoesAbout.All(l => !l.text.Contains("armour")),
              "T Foes' About these numbers says ties are named");

        // ---------- what you brought in: the land is where you fought, not where you stayed (three hours at home base, an hour's Ashlands fight) ----------
        var based = own.ShallowCopy();
        based.Deeds = ArrowsSample.Log(now.AddHours(-4), now, (0, 180, ArrowsSample.Home, new[] { ("$item_wood", 40), ("$item_feathers", 6) }),
                                       (180, 241, ArrowsSample.Land, new[] { ("$item_grausten", 4), ("$item_flametal", 2), ("$item_feathers", 2) }));
        var bm = based.Deeds.LandMinutes(null);
        var rowsNow = PanelModel.Damage(based.Log, TimeWindow.Session, "", based.NowUtc, PanelModel.SessionFrom(based));
        var bg = PanelModel.GatheredIn(based, based, TimeWindow.Session, rowsNow);
        Check(bm[ArrowsSample.Home] > 2 * bm[ArrowsSample.Land] && bg.Land == ArrowsSample.Land && bg.Items.ContainsKey("$item_grausten") && !bg.Items.ContainsKey("$item_wood"),
              "G the land is where most of your damage on these foes was dealt: 180 min at home in the Meadows, an hour's fight in the Ashlands, it picks the Ashlands and its pickups only");
        Check(PanelModel.FoughtIn(new[] { new PanelModel.DamageRow { Dir = "dealt", Type = "slash", Amount = 50, Biome = "Meadows", Other = "Boar" }, new PanelModel.DamageRow { Dir = "dealt", Type = "pierce", Amount = 80, Biome = "AshLands", Other = "Asksvin" },
                                       new PanelModel.DamageRow { Dir = "taken", Type = "fire", Amount = 500, Biome = "Swamp", Other = "Blob" } }) == "AshLands" && PanelModel.FoughtIn(new[] { new PanelModel.DamageRow { Dir = "dealt", Type = "slash", Amount = 50, Biome = "" } }) == null,
              "G the fought land counts your damage dealt only (not what hit you); rows without a land give none");
        var world = PanelSample.Full(now);
        var wg = PanelModel.GatheredIn(world, world, TimeWindow.Session, PanelModel.Damage(world.Log, TimeWindow.Session, "", world.NowUtc, PanelModel.SessionFrom(world)));
        Check(wg.Land == null && wg.Items != null && wg.When == "this session", "G no land minutes (the world's log has none): every pickup of the window");
        var noLog = own.ShallowCopy(); noLog.Deeds = null;
        Check(PanelModel.GatheredIn(noLog, noLog, TimeWindow.Session, rowsNow).Items == null, "G no minute log on this book: not recorded (never \"none\")");

        // ---------- the land and the arrow data in game: read once, nothing per frame beyond one comparison ----------
        var lands = own.Deeds.LandMinutes(null);
        Check(lands.ContainsKey(ArrowsSample.Land) && lands.ContainsKey(ArrowsSample.Home) && lands[ArrowsSample.Land] > lands[ArrowsSample.Home],
              "D the sample evening's land minutes: the Meadows first, then the Ashlands, most of the session");
        return fails;
    }

    static PanelState With(this PanelState s, Action<PanelState> f) { f(s); return s; }
}
