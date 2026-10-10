// 0.7 redesign, page group G6 Company: Fireside, Together, Food shared, Gear shared
// (work/hearthwoven-0.7/REDESIGN-RULES.md part 5). One test per page that captures the page's rule (pattern: RecordedGatherTests.Woodcutting).
// The detailed wording of the four pages stays in CompanyTests.cs (CompanyChecks). Registered in Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RecordedCompanyTests
{
    public static int Run(PanelInput input, PanelInput edda, DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Show(PanelInput i, string page, Action<PanelState> more = null)
        {
            var s = new PanelState { Chapter = Chapter.Company }; s.Page[Chapter.Company] = page; more?.Invoke(s); return PanelModel.Build(i, s);
        }
        IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));

        var keepFellows = input.Fellows; var keepId = input.PlayerId;
        input.Fellows = Program.CompanyFellows(now); input.PlayerId = 11;
        try
        {
            var install = PanelModel.RecordDate(input, PanelModel.StartOf(input, null).Value);
            var from = "from " + install;

            // ----- Fireside: your gifts are counted on this PC from the install date; fellow players' copies say whose PC they are -----
            var fire = Show(input, "fireside");
            var giving = PanelModel.Content(fire).First(b => b.Kind == "giving");
            var box = fire.AboutNumbers;
            Check(fire.Recorded && !PanelModel.AllText(fire).Any(t => t.Contains("since install")) && giving.Items.Where(i => i.Kind == "gift" && i.Src == "pc").All(g => g.RecordedFrom != null && g.RecordedFrom.Contains(from)) &&
                  giving.Items.Where(i => i.Kind == "gift" && i.Src == "fellows").All(g => g.RecordedFrom == null && (g.Value2 == null || g.Value2.StartsWith("as "))) &&
                  box != null && box.Items.Count == 3 && box.Items[0].Title == "Before " + install && box.Items[2].Title == PanelModel.DetailsLabel,
                  "company 0.7, Fireside: your gifts \"" + from + "\" (this PC), fellow players' gifts with their own words and no date; the box says before and from " + install);

            // ----- Together: wood, ore and dishes (class A) say Earlier counts may be incomplete. in the scope line; cargo (C6) says its own date -----
            var tg = Show(input, "together");
            var cats = PanelModel.Content(tg).First(b => b.Kind == "together").Items;
            var wood = cats.Single(c => c.Id == "wood"); var cargo = cats.Single(c => c.Id == "cargo"); var built = cats.Single(c => c.Id == "built");
            var cargoDate = "Recorded from " + PanelModel.RecordDate(input, PanelModel.StartOf(input, "cargo") ?? PanelModel.StartOf(input, null).Value);
            Check(tg.Recorded && wood.Value2 == PanelModel.EarlierIncomplete && built.Value2 != null && cargo.Value2 != null && cargo.Value2.StartsWith(cargoDate) &&
                  cargo.From == PanelModel.StartOf(input, "cargo") && tg.AboutNumbers != null && !PanelModel.AllText(tg).Any(t => t.Contains("since install") || t.Contains("before install")),
                  "company 0.7, Together: wood \"Earlier counts may be incomplete.\" once in its scope line, pieces built keep the game's since-when, cargo \"" + cargoDate + "\" (its own counter's start)");

            // ----- Food shared: what you enjoyed is dated (this PC), what was enjoyed of yours says whose PC or their last session -----
            var food = Show(input, "food");
            var ends = PanelModel.Content(food).First(b => b.Kind == "axis").Items.Where(i => i.Kind == "end").ToList();
            Check(food.Recorded && ends.Count == 2 && ends[0].RecordedFrom == from && ends[1].RecordedFrom == null && !string.IsNullOrEmpty(ends[1].Value2) &&
                  food.AboutNumbers != null && food.AboutNumbers.Items.Last().Title == PanelModel.DetailsLabel,
                  "company 0.7, Food shared: \"" + from + "\" on what you enjoyed, their words on what was enjoyed of yours, the box with its three lines");

            // ----- Gear shared: your side is dated (this PC), a fellow's side says whose PC or their last session; no zone, no since install -----
            var gear = Show(input, "gear");
            var madeby = PanelModel.Content(gear).First(b => b.Kind == "madeby");
            Check(gear.Recorded && madeby.Title == "Their gear, Recorded from " + install && !PanelModel.AllText(gear).Any(t => t.Contains("since install")) &&
                  gear.AboutNumbers != null && PanelModel.Content(gear).All(b => b.Kind != "zone"),
                  "company 0.7, Gear shared: their gear \"Recorded from " + install + "\" (yours), your gear with their words, no zone");
        }
        finally { input.Fellows = keepFellows; input.PlayerId = keepId; }
        return fails;
    }
}
