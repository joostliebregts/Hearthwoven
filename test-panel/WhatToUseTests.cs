// Tests for What to use (src/Panel/Chapters/WhatToUse.cs; board 7, work/hearthwoven-0.9/sketch/project/WhatToUse.dc.html and its prototype's
// logic): the third view on Battle > Foes and the same view on Last fight; the tabs and the sets (You know, You can make by what you brought in
// where you fought, All) and the default; the table (the 7 foes you hit most as columns, the cells' expected damage, their bars, the gold edge,
// the quiet resisted cell, the foes' weaknesses); the cards; a foe's column opening its detail; By foe untouched; no Best arrows block left.
// On the sample Ashlands evening (src/Panel/PanelSampleArrows.cs). Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class WhatToUseTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static PanelState Foes(TimeWindow w, Action<PanelState> more = null)
    {
        var s = new PanelState { Chapter = Chapter.Battle, Window = w, WindowPicked = true }; s.Page[Chapter.Battle] = "foes"; s.View["Battle/foes/view"] = PanelModel.UseView;
        more?.Invoke(s); return s;
    }
    static Block Of(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
    static Block Sw(PanelView v, string id) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "switch" && b.Id == id);
    static string Picked(Block sw) => sw?.Items?.FirstOrDefault(x => x.Selected)?.Id;
    static List<Block> Cols(Block t) => t.Items.Where(i => i.Kind == "usefoe").ToList();
    static List<Block> Rows(Block t) => t.Items.Where(i => i.Kind == "userow").ToList();
    static Block Cell(Block t, string item, string foe) => Rows(t).First(r => r.Id == item).Items.First(i => i.Kind == "usecell" && i.Id == foe);

    public static int Run(DateTime now)
    {
        fails = 0;
        var own = ArrowsSample.World(now);
        var v = PanelModel.Build(own, Foes(TimeWindow.Session));
        var main = PanelModel.SwitchOf(v);
        Check(main != null && main.Items.Select(x => x.Title).SequenceEqual(new[] { "By foe", "By damage type", "What to use" }) && Picked(main) == PanelModel.UseView,
              "W Foes has a third view chip, \"What to use\", after By foe and By damage type");
        Check(PanelModel.Content(v).All(b => b.Kind != "bestarrows") && PanelModel.Content(PanelModel.Build(own, Foes(TimeWindow.Session, s => s.View["Battle/foes/view"] = "foe"))).All(b => b.Kind != "bestarrows"),
              "W no Best arrows block on the page any more");
        var byFoe = PanelModel.Build(own, Foes(TimeWindow.Session, s => s.View.Remove("Battle/foes/view")));
        Check(Picked(PanelModel.SwitchOf(byFoe)) == "foe" && Of(byFoe, "foetable") != null && Of(byFoe, PanelModel.UseTableKind) == null &&
              Of(PanelModel.Build(own, Foes(TimeWindow.Session, s => { s.View.Remove("Battle/foes/view"); s.View[PanelModel.FoeOpenKey] = ArrowsSample.Valkyrie; })), "foetable").Items.First(r => r.Id == ArrowsSample.Valkyrie).Items.Any(i => i.Kind == "ranking"),
              "W By foe stays the default and as it was, its open foe with its ranking");

        // ---------- tabs and sets ----------
        var tabs = Sw(v, "Battle/foes/usetab"); var sets = Sw(v, "Battle/foes/useset");
        Check(tabs.Items.Select(x => x.Title).SequenceEqual(new[] { "Arrows", "Bolts", "Weapons" }) && Picked(tabs) == "arrow" && tabs.Title == "Against the 7 foes you hit",
              "W tabs: Arrows · Bolts · Weapons, Arrows first (\"" + tabs.Title + "\")");
        Check(sets.Items.Select(x => x.Title).SequenceEqual(new[] { "You know", "You can make", "All" }) && Picked(sets) == PanelModel.UseMake,
              "W sets: You know · You can make · All; You can make by default (it holds two or more)");
        var table = Of(v, PanelModel.UseTableKind);
        var make = Rows(table).Select(r => r.Title).ToList();
        Check(make.Contains("Charred Arrow") && make.Contains("Silver Arrow") && make.Contains("Frost Arrow") && !make.Contains("Blackmetal Arrow") && !make.Contains("Carapace Arrow") && !make.Contains("Bloodgold Arrow"),
              "W You can make: owned or made, or can craft with every material brought in where you fought (Charred: its bone, feathers and ashwood; not Blackmetal: no black metal brought in; not Bloodgold: not known) (" + string.Join(", ", make) + ")");
        var known = Rows(Of(PanelModel.Build(own, Foes(TimeWindow.Session, s => s.View["Battle/foes/useset"] = PanelModel.UseKnown)), PanelModel.UseTableKind));
        var all = Rows(Of(PanelModel.Build(own, Foes(TimeWindow.Session, s => s.View["Battle/foes/useset"] = PanelModel.UseAll)), PanelModel.UseTableKind));
        Check(known.Any(r => r.Title == "Blackmetal Arrow" && r.Text == "can craft") && known.All(r => r.Text != PanelModel.GearNotKnown) && all.Any(r => r.Title == "Bloodgold Arrow" && r.Tone == "unknown") && all.Count > known.Count,
              "W You know: owned, made, can craft (" + known.Count + "); All: also what is not known yet, quieter (" + all.Count + ")");
        var noFeathers = own.ShallowCopy(); noFeathers.Deeds = ArrowsSample.Log(now.AddHours(-1), now, (0, 61, ArrowsSample.Land, new[] { ("$item_grausten", 4) }));
        noFeathers.Owned = t => false; noFeathers.ItemsCrafted = new Dictionary<string, float>(); noFeathers.ItemsPickedUp = new Dictionary<string, float>();
        Check(Picked(Sw(PanelModel.Build(noFeathers, Foes(TimeWindow.Session)), "Battle/foes/useset")) == PanelModel.UseKnown, "W with fewer than two you can make, the set opens on You know");

        // ---------- the table ----------
        Check(Cols(table).Select(c => c.Title).SequenceEqual(new[] { "Fallen Valkyrie", "Charred Warrior", "Asksvin", "Morgen", "Charred Warlock", "Volture", "Lava Blob" }) && Cols(table).All(c => c.Icon.StartsWith("item:Trophy")) &&
              table.Title == "Arrow, then each foe you hit" && table.Text == null,
              "W columns: the foes you hit, most damage first, each with its trophy");
        var frostVolture = Cell(table, "ArrowFrost", ArrowsSample.Volture);
        Check(frostVolture.Value == "104" && frostVolture.Tone == PanelModel.UseBest && frostVolture.Items.Select(p => p.Id + "=" + p.Value).SequenceEqual(new[] { "pierce=26", "frost=78" }),
              "W a cell is the expected damage (Frost Arrow on a Volture: 26 pierce + 52 frost x1.5 = 104), the most against it: gold; its bar by type after the multiplier");
        var tieV = new[] { "ArrowCharred", "ArrowSilver" }.Select(a => Cell(table, a, ArrowsSample.Valkyrie)).ToList();
        Check(tieV.All(c => c.Value == "82" && c.Tone == PanelModel.UseBest), "W a tie is gold on both (Charred and Silver Arrow on the Fallen Valkyrie, 82)");
        var low = Cell(table, "ArrowCharred", ArrowsSample.Morgen);
        Check(low.Value == "41" && low.Tone == PanelModel.UseLow, "W a resisted cell is quiet (Charred Arrow on a Morgen: 41 of its 82)");
        var cells = Rows(table).SelectMany(r => r.Items.Where(i => i.Kind == "usecell")).ToList();
        Check(Math.Abs(cells.Max(c => c.Fraction) - 1f) < 1e-4 && cells.All(c => Math.Abs(c.Items.Sum(p => p.Fraction) - 1f) < 1e-3 || c.Value == "0"),
              "W one length scale for the table's bars, each bar's parts adding up");
        Check(Cols(table).First(c => c.Id == ArrowsSample.Volture).Items.Select(i => i.Id).SequenceEqual(new[] { "frost" }) &&
              Cols(table).First(c => c.Id == ArrowsSample.LavaBlob).Items.Select(i => i.Id).SequenceEqual(new[] { "blunt", "frost" }),
              "W under each foe its weaknesses as type swatches (Volture: frost; Lava Blob: blunt, frost)");
        Check(Rows(table).First(r => r.Id == "ArrowFrost").Note == "8 Wood · 4 Obsidian · 1 Freeze Gland · 2 Feathers" && Rows(table).First(r => r.Id == "ArrowFrost").Items.Where(i => i.Kind == "part").Select(p => p.Value + " " + p.Title).SequenceEqual(new[] { "26 pierce", "52 frost" }),
              "W a row: name, status, its damage types, its materials");
        Check(table.Note.StartsWith(make.Count + " arrows you can make from what you brought in, in the Ashlands, where you fought these") && table.Note.Contains("more under All") && table.Note.Contains("gold edge = the most against that foe"),
              "W the italic line: how many, where from, what gold means (" + table.Note + ")");

        // ---------- the cards ----------
        var cards = Of(v, "cards").Items;
        Check(cards.Count >= 2 && cards.All(c => c.Tone == PanelModel.SayTone) && cards[0].Title.EndsWith(" in the quiver") && cards[0].Text.StartsWith("Best or tied best against ") &&
              cards[1].Title.EndsWith(" as backup") && cards[1].Text.StartsWith("Beats "),
              "W cards: " + string.Join(" | ", cards.Select(c => c.Title + ": " + c.Text)));

        // ---------- a foe's column opens its detail ----------
        var open = PanelModel.Build(own, Foes(TimeWindow.Session, s => s.View["Battle/foes/useopen"] = ArrowsSample.Volture));
        var detail = Of(open, PanelModel.UseDetailKind); var openTable = Of(open, PanelModel.UseTableKind);
        Check(detail != null && detail.Id == ArrowsSample.Volture && detail.Title == "Volture" && detail.Items.Select(r => r.Id).SequenceEqual(new[] { "best", "worst" }) &&
              Cols(openTable).Single(c => c.Selected).Id == ArrowsSample.Volture && PanelModel.UseOpenLink(openTable, Cols(openTable).First(c => c.Selected)) == "view:Battle/foes/useopen=" &&
              PanelModel.UseOpenLink(table, Cols(table)[0]) == "view:Battle/foes/useopen=" + ArrowsSample.Valkyrie,
              "W a click on a foe's column opens its own Best and Worst under the table; a click on the open one shuts it");
        Check(PanelModel.Islands(open.Blocks[0]).Any(i => i.Span == PanelModel.IslandPlain && i.Parts.Any(p => p.Block == detail)), "W the detail draws as under an open foe: its islands, no outline round them");

        // ---------- the other tabs, many foes, Last fight, a fellow's book ----------
        var weapons = PanelModel.Build(own, Foes(TimeWindow.Session, s => { s.View["Battle/foes/usetab"] = "weapon"; s.View["Battle/foes/useset"] = PanelModel.UseKnown; }));
        var wt = Of(weapons, PanelModel.UseTableKind);
        Check(wt.Title == "Weapon, then each foe you hit" && Rows(wt).Count > 2 && Of(weapons, "cards").Items[0].Title.EndsWith(" in hand"), "W the Weapons tab: their damage per hit the same way, \"in hand\"");
        var bolts = Of(PanelModel.Build(own, Foes(TimeWindow.Session, s => { s.View["Battle/foes/usetab"] = "bolt"; s.View["Battle/foes/useset"] = PanelModel.UseAll; })), "cards");
        Check(bolts == null || bolts.Items[0].Title.EndsWith(" in the crossbow"), "W the Bolts tab: \"in the crossbow\"");
        var many = Of(PanelModel.Build(ArrowsSample.World(now, true), Foes(TimeWindow.Session)), PanelModel.UseTableKind);
        Check(Cols(many).Count == PanelModel.UseFoes && many.Text == "2 more foes you hit are left out: these are the 7 you hit most.", "W more than 7 foes: the 7 you hit most, one line names how many are left out");
        var last = PanelModel.Build(own, new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = PanelModel.LastFightPage } });
        var lt = Of(last, PanelModel.UseTableKind);
        Check(lt != null && Sw(last, "Battle/lastfight/usetab") != null && PanelModel.Content(last).Any(b => b.Kind == "section" && b.Title == PanelModel.UseLabel) &&
              Cols(lt).Select(c => c.Id).SequenceEqual(new[] { ArrowsSample.Valkyrie }) && PanelModel.Content(last).All(b => b.Kind != "bestarrows"),
              "W Last fight: the same view over that fight's foes (the Fallen Valkyrie), no Best arrows block");
        var friend = own.ShallowCopy(); friend.IsSelf = false;
        Check(PanelModel.SwitchOf(PanelModel.Build(friend, Foes(TimeWindow.Session))).Items.All(x => x.Id != PanelModel.UseView), "W a fellow's book has no What to use: the gear and the gathering are yours");
        Check(PanelModel.FoesAbout.Any(l => l.title == PanelModel.UseAboutTitle && l.text.Contains("A gold edge")), "W About these numbers says how the view is worked out");
        return fails;
    }
}
