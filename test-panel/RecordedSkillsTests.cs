// 0.7 redesign, page group G8 Skills and About (T): Skills Overview, Practised, skill pages; About
// (work/hearthwoven-0.7/REDESIGN-RULES.md part 5). One test per page group that captures the page's rule (pattern: RecordedGatherTests.Run).
// Registered in Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RecordedSkillsTests
{
    public static int Run(PanelInput input, PanelInput edda, DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Show(PanelInput i, Chapter chapter, string page, Action<PanelState> more = null) { var s = new PanelState { Chapter = chapter, Page = { [chapter] = page } }; more?.Invoke(s); return PanelModel.Build(i, s); }

        // ---------- Skills overview and its Practised view: levels class D (the game's own), practice class C (counted here from the install) ----------
        var world = PanelSample.Full(now);
        var day = PanelModel.RecordDate(world, PanelModel.StartOf(world, null).Value);
        var levels = Show(world, Chapter.Skills, null);
        var practised = Show(world, Chapter.Skills, null, s => s.View["Skills/overview/view"] = "practised");
        var practisedSection = PanelModel.Content(practised).Single(b => b.Kind == "section" && b.Title == PanelModel.PracticeWhere);
        var topShare = PanelModel.Content(practised).First(b => b.Kind == "hero");
        var ladders = PanelModel.Content(levels).First(b => b.Kind == "ladders");
        var box = Show(world, Chapter.Skills, null, s => s.ShowNumbers = true).AboutNumbers;
        Check(levels.Recorded && practised.Recorded && !PanelModel.Content(levels).Any(b => b.Kind == "zone") && !PanelModel.Content(practised).Any(b => b.Kind == "zone") &&
              ladders.Src == PanelModel.SrcCharacter && string.IsNullOrEmpty(ladders.RecordedFrom) && ladders.Text == "practiced from " + day &&
              practisedSection.RecordedFrom == "Recorded from " + day + " · this PC" && string.IsNullOrEmpty(topShare.RecordedFrom) && topShare.Src == PanelModel.SrcPc &&
              box != null && box.Items.Count == 3 && box.Items[0].Title == "Before " + day && box.Items[1].Title == "From " + day && box.Items[2].Title == "Additional details" &&
              !PanelModel.AllText(practised).Any(t => t.Contains("since install") || t.Contains("faded") || t.Contains("in all ·")) && !PanelModel.AllText(levels).Any(t => t.Contains("since install")),
              "skills 0.7: the levels are the game's own (no label); the practised section says \"Recorded from " + day + " · this PC\", its top share \"from " + day +
              "\"; the ladders' key says \"practiced from " + day + "\"; the box says before and from " + day + "; no since install");
        var eddaSkills = Show(edda, Chapter.Skills, null);
        Check(eddaSkills.AboutNumbers == null && !string.IsNullOrEmpty(eddaSkills.Scope) && !eddaSkills.Scope.Contains("since install"),
              "skills 0.7, Edda's book: no box (a fellow's copy carries no dates); the plate line says whose copy it is, without since install");

        // ---------- a single skill page (Axes): the level is the game's own; the practice share is counted from the install ----------
        var axes = Show(world, Chapter.Skills, "Axes");
        var axesLadder = PanelModel.Content(axes).First(b => b.Kind == "ladder");
        var practice = axesLadder.Items.Single(i => i.Kind == "practice");
        Check(axes.Recorded && axesLadder.Value == "38" && axesLadder.Src == PanelModel.SrcCharacter && string.IsNullOrEmpty(axesLadder.RecordedFrom) &&
              practice.Value == "2 %" && practice.Src == PanelModel.SrcPc && practice.RecordedFrom == "from " + day &&
              axes.AboutNumbers != null && axes.AboutNumbers.Items[0].Title == "Before " + day && !PanelModel.Content(axes).Any(b => b.Kind == "zone"),
              "skills 0.7, a skill page (Axes): level 38 as the game's own; the practice share 2 % \"from " + day + "\"; the box names the same install date; no zone");

        // ---------- About (T): technical background; no box, no Recorded label; the timeline dates what the mod knows ----------
        var reads = Show(world, Chapter.Skills, null, s => { s.ShowAbout = true; s.AboutPage = "reads"; });
        var about = Show(world, Chapter.Skills, null, s => s.ShowAbout = true);
        var sinceWhen = PanelModel.PlateOf(about).Items.Single(b => b.Kind == "sincewhen");
        Check(!reads.Recorded && reads.AboutNumbers == null && !PanelModel.AllText(reads).Any(t => t.Contains("Faded") || t.Contains("faded") || t.Contains("since install")) &&
              PanelModel.Content(reads).Where(b => b.Kind == "readrows").SelectMany(b => b.Items).All(r => r.Tone == null && r.Title != "Faded") &&
              sinceWhen.Value == "installed " + day && sinceWhen.Text == world.PlayerName + " made " + PanelModel.RecordDate(world, world.CharacterMade.Value),
              "About 0.7: no Faded row, no box, no label; the timeline says \"installed " + day + "\" and the character's making date");
        return fails;
    }
}
