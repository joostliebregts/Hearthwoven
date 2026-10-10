// 0.7 redesign, page group G7 Hall and Voyages: Hall (Overview, Trader, Smelters), Voyages (Overview, Sailing, Cargo, On foot, Maps)
// (work/hearthwoven-0.7/REDESIGN-RULES.md part 5). One test per page that captures the page's rule (pattern: RecordedGatherTests.Woodcutting).
// Registered in Program.cs; never edit Program.cs from a page agent.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RecordedHallTests
{
    public static int Run(PanelInput input, PanelInput edda, DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Show(PanelInput i, Chapter c, string page, Action<PanelState> more = null) { var s = new PanelState { Chapter = c, Page = { [c] = page } }; more?.Invoke(s); return PanelModel.Build(i, s); }
        const string Since = "Recorded from 1 October · this PC";

        // ---------- Hall > Overview (all class C): one label on the heading, the box says what the game never counted ----------
        var world = PanelSample.Full(now);
        var hall = Show(world, Chapter.Stores, null);
        var open = Show(world, Chapter.Stores, null, s => s.ShowNumbers = true);
        var box = PanelModel.PlateOf(open).Items.Single(b => b.Kind == "aboutnumbers");
        Check(hall.Recorded && hall.HeadingRecordedFrom == Since && !PanelModel.Content(hall).Any(b => b.Kind == "zone") && hall.Keys.Contains("[Y] Numbers") &&
              box.Items.Count == 3 && box.Items[0].Title == "Before 1 October" && box.Items[0].Text == "Not recorded: the game keeps no count of trades or smelting." &&
              box.Items[1].Text == "Hearthwoven counted what you bought and put in, on this PC." && !PanelModel.AllText(hall).Any(t => t.Contains("since install") || t.Contains("Since install")),
              "hall 0.7: the heading says \"Recorded from 1 October · this PC\", no zones; About these numbers: before 1 October not recorded, from 1 October what this PC counted");

        // ---------- Voyages > Overview: the crew is this PC's (class C, the label says so); the journey is the game's (class D) ----------
        var voyage = Show(world, Chapter.Voyages, null);
        var crew = PanelModel.Content(voyage).FirstOrDefault(b => b.Kind == "section" && b.Title == "Sailed with");
        Check(voyage.Recorded && Zoned.Zones(voyage).Count == 0 && crew != null && crew.RecordedFrom == Since && PanelModel.Content(voyage).Any(b => b.Kind == "journey" && b.Src == PanelModel.SrcCharacter),
              "voyages 0.7: no zones; Sailed with says \"Recorded from 1 October · this PC\"; the journey stays your character's game count");

        // ---------- Voyages > Cargo, All and a day window: the cargo's own date; a day window has no box and no label (rule W.1) ----------
        var cargo = Show(world, Chapter.Voyages, "cargo");
        var cargo7 = Show(world, Chapter.Voyages, "cargo", s => s.Window = TimeWindow.SevenDays);
        var carried = PanelModel.Content(cargo).FirstOrDefault(b => b.Kind == "hero" && b.Src == PanelModel.SrcPc);
        Check(cargo.Recorded && carried != null && carried.From.HasValue && cargo.AboutNumbers != null &&
              cargo7.Recorded && cargo7.Windowed && cargo7.AboutNumbers == null && !PanelModel.Content(cargo7).Any(b => !string.IsNullOrEmpty(b.RecordedFrom) || b.Kind == "zone"),
              "cargo 0.7: the cargo carried counted from its own date (From); All has the box; 7 days: no label, no box (rules C6, W.1)");

        // ---------- Voyages > Maps: the map shared at the table is this PC's (from a date), the finds are your character's ----------
        var maps = Show(world, Chapter.Voyages, "maps");
        var shared = PanelModel.Content(maps).Where(b => b.Kind == "rows").SelectMany(r => r.Items).FirstOrDefault(i => i.Title == "Maps shared");
        Check(maps.Recorded && shared != null && shared.Src == PanelModel.SrcPc && shared.RecordedFrom == "from 1 October" &&
              !PanelModel.Content(maps).Where(b => b.Kind == "rows").SelectMany(r => r.Items).Where(i => i.Src == PanelModel.SrcCharacter).Any(i => i.RecordedFrom != null),
              "maps 0.7: \"Maps shared\" from 1 October; the finds carry no label");
        return fails;
    }
}
