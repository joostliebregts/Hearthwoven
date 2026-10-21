// Tests for Armor sets, made or worn (0.9; Joost: "made or worn (worn counted from install) and add the Together version for the group"): the record
// of the armor you put on (src/ArmorWorn.cs: from its start date, one entry per item token, bounded, in the local totals), a piece counting when made
// or worn (Chapters/ArmorSets.cs), the marks and the start date on the page, the copy's "armorWorn" (only what armorMade does not say, its size), and
// the Together feat Clad as One (each piece made or worn by anyone who shares, each person once; an older copy adds what it shares). Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class ArmorWornTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelState Sets(bool everyone = false)
    {
        var s = new PanelState { Chapter = Chapter.Deeds, Everyone = everyone }; s.Page[Chapter.Deeds] = PanelModel.SetsPageId;
        return s;
    }
    static List<Block> Bands(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "itemgrid" && b.Tone == PanelModel.SetBandTone).ToList();
    static Block Band(PanelView v, string name) => Bands(v).FirstOrDefault(b => b.Title == name);

    static readonly string[] Meadows = { "$item_chest_rags", "$item_legs_rags", "$item_helmet_leather", "$item_chest_leather", "$item_legs_leather", "$item_cape_deerhide" };

    /// <summary>A book of the sample world with exactly this armor made (the game's counts) and worn (the record, counted from <paramref name="from"/>).</summary>
    static PanelInput With(DateTime now, IEnumerable<string> made, IEnumerable<string> worn, DateTime? from)
    {
        var w = SetsSample.World(now, "new");
        foreach (var t in made ?? new string[0]) w.ItemsCrafted[t] = 1;
        w.ArmorWorn = worn == null ? null : new HashSet<string>(worn, StringComparer.Ordinal); w.ArmorWornFrom = worn == null ? null : from;
        return w;
    }

    public static int Run(DateTime now)
    {
        fails = 0;

        // ---- the record: nothing before counting began, one entry per token, bounded; the local file keeps it ----
        var rec = new ArmorWornBook();
        Check(!rec.Note("$item_helmet_leather") && rec.Tokens.Count == 0, "record: a piece put on before counting began is not recorded (worn before the start date never counts)");
        Check(rec.Begin(now.AddDays(-3)) && !rec.Begin(now) && rec.FromUtc == now.AddDays(-3), "record: counting begins once, on its first day");
        Check(rec.Note("$item_helmet_leather") && !rec.Note("$item_helmet_leather") && rec.Tokens.Count == 1, "record: one entry per item token, however often it is put on");
        Check(!rec.Note("") && !rec.Note(null) && !rec.Note(new string('x', 200)), "record: no empty or oversized token");
        for (int k = 0; k < 400; k++) rec.Note("$item_chest_x" + k.ToString("000"));
        Check(rec.Tokens.Count == ArmorWornBook.MaxTokens, "record: at most " + ArmorWornBook.MaxTokens + " tokens (" + rec.Tokens.Count + ")");
        var totals = new LocalTotals { PlayerId = 7, Name = "Rowan" };
        totals.Worn.Begin(now.AddDays(-3)); totals.Worn.Note("$item_legs_bronze"); totals.Worn.Note("$item_helmet_iron");
        var back = LocalTotals.FromJson(totals.ToJson(now));
        Check(back != null && back.Worn.Begun && back.Worn.FromUtc == now.AddDays(-3) && back.Worn.Tokens.SetEquals(new[] { "$item_legs_bronze", "$item_helmet_iron" }), "record: the local totals keep it (top-level \"armorWorn\") with its start date");
        var older = LocalTotals.FromJson(new LocalTotals { PlayerId = 7 }.ToJson(now));
        Check(older != null && !older.Worn.Begun && older.Worn.Tokens.Count == 0 && !new LocalTotals { PlayerId = 7 }.ToJson(now).Contains(ArmorWornBook.Key), "record: a file without it reads as not begun, and nothing is written while it is empty");

        // ---- made only, worn only, both; the retro made (the game's counts, no record at all) ----
        var from = now.AddDays(-3);
        Check(PanelModel.LandsArmored(With(now, Meadows, null, null)) == 1, "feat: made only, no record of wearing (the character's whole life): the Meadows count (1 land)");
        Check(PanelModel.LandsArmored(With(now, null, Meadows, from)) == 1, "feat: worn only, nothing made: the Meadows count (1 land)");
        Check(PanelModel.LandsArmored(With(now, Meadows.Take(3), Meadows.Skip(3), from)) == 1 && PanelModel.LandsArmored(With(now, Meadows.Take(3), Meadows.Skip(4), from)) == 0,
              "feat: both: half made and half worn completes the land; one piece neither, it does not");
        // worn before the start date: not in the record, so the piece is not counted, and the page says from when wearing is counted
        var late = new ArmorWornBook(); late.Note("$item_legs_leather"); late.Begin(from); late.Note("$item_cape_deerhide");
        var lateBook = With(now, Meadows.Take(4), late.Tokens, late.FromUtc);
        Check(PanelModel.LandsArmored(lateBook) == 0, "feat: the trousers put on before counting began do not count (the cape after it does)");
        var lv = PanelModel.Build(lateBook, Sets());
        var key = PanelModel.Content(lv).FirstOrDefault(b => b.Id == PanelModel.SetMarksId);
        var date = PanelModel.RecordDate(lateBook, from);
        Check(key != null && key.Tone == PanelModel.PlanMarksTone && key.Items.Select(i => i.Note + ":" + i.Title).SequenceEqual(new[] { PanelModel.GearMade + ":made by you", PanelModel.SetWornNote + ":worn by you, counted from " + date }),
              "page: the marks' key: a gold dot made by you, a ring worn by you, counted from " + date + " (" + string.Join(" / ", key?.Items.Select(i => i.Title) ?? new string[0]) + ")");
        Check((lv.AboutNumbers?.Items ?? new List<Block>()).Any(i => (i.Text ?? "").Contains("Wearing is recorded on this PC from " + date + "; what you wore before that is not known")),
              "page: About these numbers says from when wearing is recorded, and that earlier wear is not known (never shown as not worn)");
        var leather = Band(lv, "Deerstalker's Garb");
        Check(leather.Items.First(t => t.Id == "$item_helmet_leather").Note == PanelModel.GearMade && leather.Items.First(t => t.Id == "$item_cape_deerhide").Note == PanelModel.SetWornNote
              && leather.Items.First(t => t.Id == "$item_cape_deerhide").Value == PanelModel.SetWornNote && leather.Items.First(t => t.Id == "$item_legs_leather").Tone == PanelModel.NotMadeTone,
              "page: a made piece carries the gold dot and its count, a worn one the ring and \"worn\", one neither stays dashed");
        Check(PanelModel.RingMark(PanelModel.SetWornNote) && PanelModel.RingMark(PanelModel.PlanCanMake) && !PanelModel.RingMark(PanelModel.GearMade), "page: worn draws the ring (as can make), made the gold dot");
        var both = With(now, new[] { "$item_chest_leather" }, new[] { "$item_chest_leather" }, from);
        Check(Band(PanelModel.Build(both, Sets()), "Deerstalker's Garb").Items.First(t => t.Id == "$item_chest_leather").Note == PanelModel.GearMade, "page: a piece made and worn shows made");

        // ---- the wording: about pieces you made or wore ----
        var def = PanelModel.FeatById("armorsets");
        Check(PanelModel.FeatWords(both, def.Honours) == "Every armor set of a land, each piece made or worn by you." && PanelModel.FeatWords(new PanelInput { PlayerName = "Edda", IsSelf = false }, def.Honours).EndsWith("made or worn by them.")
              && PanelModel.FeatWords(both, def.Caveat).Contains("once you made or wore it") && def.Brief(1) == "every set of 1 land",
              "wording: the feat says each piece made or worn by you (by them on a friend's book)");

        // ---- the copy: what you wore that armorMade does not say, from when; an older copy has none; bounded ----
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats();
        string Snap(IEnumerable<string> made, IEnumerable<string> worn, DateTime? wornFrom) =>
            GroupShare.SharedCopy(Snapshot.Build("0.9.9", 7, "Edda", stats, new Snapshot.SkillInfo[0], "Midgard", new DamageTally(), "s1", new SessionEvents(), new EventLog(), true, armorMade: made, armorWorn: worn, armorWornFrom: wornFrom));
        var copy = PanelInput.FromSnapshot(Snap(new[] { "$item_helmet_leather" }, new[] { "$item_helmet_leather", "$item_legs_trollleather" }, from));
        Check(copy.ArmorWorn != null && copy.ArmorWorn.SequenceEqual(new[] { "$item_legs_trollleather" }) && copy.ArmorWornFrom == from.Date, "copy: armorWorn carries only what armorMade does not say, and the day counting began");
        Check(PanelInput.FromSnapshot(Snap(new[] { "$item_helmet_leather" }, null, null)).ArmorWorn == null && PanelInput.FromSnapshot(Snap(null, new[] { "$item_legs_rags" }, null)).ArmorWorn == null,
              "copy: no key without a record or before counting began (null: an older Hearthwoven reads the same)");
        Check(PanelInput.FromSnapshot(Snap(null, new string[0], from)).ArmorWorn?.Count == 0, "copy: counting begun, nothing worn: an empty list (never \"not shared\")");
        var many = Enumerable.Range(0, 400).Select(i => "$item_chest_x" + i.ToString("000")).ToList();
        Check(PanelInput.FromSnapshot(Snap(null, many, from)).ArmorWorn.Count == ArmorWornBook.MaxTokens, "copy: at most " + ArmorWornBook.MaxTokens + " worn tokens travel");
        var allTokens = SetsSample.Pieces.Select(p => p.Token).ToList();
        var bytes = Snap(null, allTokens, from).Length - Snap(null, null, null).Length;
        var halfBytes = Snap(allTokens.Take(allTokens.Count / 2), allTokens, from).Length - Snap(allTokens.Take(allTokens.Count / 2), null, null).Length;
        System.Console.WriteLine("  armorWorn: every sample piece worn (" + allTokens.Count + " tokens) " + bytes + " bytes; half of them made " + halfBytes + " bytes; none worn " + (Snap(null, new string[0], from).Length - Snap(null, null, null).Length) + " bytes");
        Check(bytes < 2000 && halfBytes < bytes, "copy: every sample piece worn costs " + bytes + " bytes (under 2 KB), less when armorMade says some (" + halfBytes + ")");

        // ---- Together: Clad as One, each piece made or worn by anyone who shares, each person once; an older copy adds what it shares ----
        var ev = SetsSample.World(now, "everyone");
        var group = PanelModel.FeatById(PanelModel.ArmorTogetherFeatId);
        Check(group != null && group.Group && group.Name == "Clad as One" && group.Tiers.SequenceEqual(PanelModel.ArmorFeatTiers) && group.Page == PanelModel.FeatsTogetherId,
              "together: Clad as One on Together, tiers as the personal one (1 land, half the lands, every land)");
        Check(PanelModel.LandsArmoredTogether(ev) == 2 && group.Value(ev) == 2,
              "together: the Meadows and the Black Forest complete across the group (the Troll set by Rowan's made hood and tunic and Edda's worn legs and cape): 2 lands");
        var withStale = SetsSample.World(now, "everyone"); withStale.Fellows.Add(withStale.Fellows.First(f => f.PlayerName == SampleWorld.Edda));
        Check(PanelModel.GroupMembers(withStale).Count == PanelModel.GroupMembers(ev).Count && PanelModel.LandsArmoredTogether(withStale) == 2, "together: each person once: a second copy of Edda adds no one");
        var alone = SetsSample.World(now, "everyone"); alone.Fellows = new List<PanelInput>();
        Check(group.Value(alone) == 0 && PanelModel.NeedsTwo.Contains(PanelModel.ArmorTogetherFeatId), "together: alone it reads 0 (it needs two who share), never your own feat under another name");
        // Tor's copy shares only what he made: his iron helmet and scale mail count, his worn pieces wait; the page says so once
        var gv = PanelModel.Build(ev, Sets(true));
        Check(PanelModel.Content(gv).Any(b => b.Kind == "note" && b.Text == "Tor's Hearthwoven is 0.9.9: the pieces Tor wore fill in once they update."), "together: a copy without armorWorn adds what it made, and the page says what fills in");
        var torWore = SetsSample.World(now, "everyone"); var t2 = torWore.Fellows.First(f => f.PlayerName == SampleWorld.Tor); t2.ArmorWorn = new HashSet<string> { "$item_legs_iron" }; t2.ArmorWornFrom = from;
        Check(PanelModel.LandsArmoredTogether(torWore) == 3, "together: once Tor's copy shares his worn iron greaves, the Swamp completes too (3 lands)");
        Check(group.Part(ev) > 0 && group.Overlaps && group.Helped == "Who made or wore them", "together: Who made or wore them, each one's pieces (they overlap: no per cent)");
        Check(PanelModel.FeatTier(group, torWore) == 1, "together: 3 lands earn tier I (one land), not yet II (half the lands)");

        // ---- US spelling, no person's name in the rules ----
        var texts = new[] { def.Honours, def.Caveat, def.Rule(1), group.Honours, group.Caveat, group.Rule(1), group.Rule(4), group.Rule(8), PanelModel.SetsGroupLine, PanelModel.SetsGroupShares, PanelModel.SetsAbout(lateBook) };
        Check(texts.All(t => t.IndexOf("armour", StringComparison.OrdinalIgnoreCase) < 0 && t.IndexOf("Joost", StringComparison.OrdinalIgnoreCase) < 0), "wording: US spelling, no person named");
        return fails;
    }
}
