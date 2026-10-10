// 0.7 redesign, page group G1 Deeds gather: Woodcutting, Mining (and their day windows)
// (work/hearthwoven-0.7/REDESIGN-RULES.md part 5). One test per page that captures the page's rule. Woodcutting is the worked page of
// part 3 (shared agent); the G1 page agent adds Mining here in the same pattern. Registered in Program.cs; never edit Program.cs from a page agent.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RecordedGatherTests
{
    public static int Run(PanelInput input, PanelInput edda, DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Show(PanelInput i, string page, Action<PanelState> more = null) { var s = new PanelState { Page = { [Chapter.Deeds] = page } }; more?.Invoke(s); return PanelModel.Build(i, s); }
        int Incomplete(PanelView v) => PanelModel.AllText(v).Count(t => t == PanelModel.EarlierIncomplete);

        // ---------- Woodcutting (part 3): one recorded total with its breakdown, the skill beside the hero, the box ----------
        var world = PanelSample.Full(now);
        var wood = Show(world, "woodcutting");
        var items = PanelModel.PlateOf(wood).Items;
        var hero = items.First(b => b.Kind == "hero");
        var axe = hero.Items.Single(n => n.Kind == "number");
        var skill = PanelModel.SkillOf(hero);
        var bar = items.Single(b => b.Kind == "composition");
        var perTree = items.Single(b => b.Kind == "section" && b.Title == "Trees felled per tree");
        var open = Show(world, "woodcutting", s => s.ShowNumbers = true);
        var box = PanelModel.PlateOf(open).Items.Single(b => b.Kind == "aboutnumbers");
#pragma warning disable CS0618   // the retired layer fields: asserted empty
        Check(wood.Recorded && !items.Any(b => b.Kind == "zone" || b.Kind == "ladders") && hero.Value == "410" && hero.Title == "trees felled" && hero.Src == PanelModel.SrcCharacter &&
              axe.Value == "134" && axe.Src == PanelModel.SrcPc && axe.RecordedFrom == "from 1 October" && hero.RecordedFrom == null &&
              skill != null && skill.Id == "Skills/WoodCutting" && skill.Value == "34" && Math.Abs(skill.Progress - 0.55f) < 0.001f &&
              bar.Value == "2 882" && bar.Src == PanelModel.SrcCharacter && bar.Note == null && bar.Items.All(p => p.Fraction2 == 0) &&
              perTree.Value == "30" && perTree.RecordedFrom == "Recorded from 1 October · this PC" &&
              items.Single(b => b.Kind == "section" && b.Title == "Axe hits per tree").RecordedFrom == "Recorded from 1 October · this PC" &&
              Incomplete(wood) == 1 && !items.Any(b => b.Text == PanelModel.EarlierIncomplete) && PanelModel.AboutText(wood).Contains(PanelModel.EarlierIncomplete) &&
              !PanelModel.AllText(wood).Any(t => t.Contains("since install") || t.Contains("faded") || t.Contains("in all ·")),
              "woodcutting 0.7: 410 trees felled (your character's whole count, no layers) with 134 axe hits \"from 1 October\" and Wood Cutting level 34 in the hero's row; " +
              "wood brought in 2 882 as plain sums; the per-tree lists \"Recorded from 1 October · this PC\"; \"Earlier counts may be incomplete.\" once, in About these numbers (0.8 layout D+); no zone, no skill strip, no since install");
#pragma warning restore CS0618
        Check(box.Items.Count == 5 && box.Items[3].Title == PanelModel.AboutEarlierLabel && box.Items[4].Text == PanelModel.GrowthAbout("since 1 Oct") && box.Items[0].Title == "Before 1 October" && box.Items[0].Text.StartsWith("The game's own count: 380 trees, 2 700 wood.") && box.Items[0].Text.EndsWith("so the real numbers are higher.") &&
              box.Items[1].Text.EndsWith(": 30 trees, 182 wood so far.") && box.Items[2].Text == "Trees per kind and axe hits are only known from 1 October. From 3 October, wood from pieces that came down counts on Building, not here." &&
              wood.Keys.Contains("[Y] Numbers"),
              "woodcutting 0.7: About these numbers says before 1 October the game's own 380 trees and 2 700 wood (what they missed), from 1 October 30 trees and 182 wood counted here; " +
              "0.8: wood from pieces that came down counts on Building from its own date; Y opens it; 0.8 layout D+: then the earlier counts and the growth line's span");
        var week = Show(world, "woodcutting", s => s.Window = TimeWindow.SevenDays);
        Check(week.Recorded && week.Windowed && PanelModel.SkillOf(PanelModel.Content(week).FirstOrDefault(b => b.Kind == "hero")) != null && week.AboutNumbers == null &&
              !PanelModel.Content(week).Any(b => b.Kind == "ladders" || b.Kind == "zone" || !string.IsNullOrEmpty(b.RecordedFrom)) && Incomplete(week) == 0,
              "woodcutting 0.7, 7 days: the window's numbers, the skill beside the hero; no label, no \"Earlier counts\", no box (rules W.1, A.6, K.5)");
        var eddaWood = Show(edda, "woodcutting");
        var eddaItems = PanelModel.Content(eddaWood);
        Check(eddaWood.Recorded && eddaWood.AboutNumbers == null && Incomplete(eddaWood) == 0 && !eddaItems.Any(b => b.Kind == "zone") &&
              eddaItems.Where(b => !string.IsNullOrEmpty(b.RecordedFrom)).All(b => b.RecordedFrom.Contains("Edda's PC")),
              "woodcutting 0.7, Edda's book: the game's counts, her own counts \"on Edda's PC\", no box (0.8 layout D+: \"Earlier counts may be incomplete.\" lives only in your About these numbers, so not on her page)");
        var realBook = RecordedTests.RealBook();
        if (realBook == null) Check(false, "woodcutting 0.7, the real book: the fixture loads");
        else
        {
            var bw = Show(realBook, "woodcutting"); var bh = PanelModel.Content(bw).First(b => b.Kind == "hero");
            var bbox = Show(realBook, "woodcutting", s => s.ShowNumbers = true).AboutNumbers;
            Check(bh.Value == "108" && bh.Items.Single(n => n.Kind == "number").Value == "18" && bh.Items.Single(n => n.Kind == "number").RecordedFrom == "from 8 October" &&
                  PanelModel.Content(bw).Single(b => b.Kind == "composition").Value == "2 774" && !PanelModel.Content(bw).Any(b => b.Title == "Trees felled per tree") &&
                  PanelModel.SkillOf(bh)?.Value == "33" && bbox != null && bbox.Items[1].Text.EndsWith(": 0 trees, 0 wood so far."),
                  "woodcutting 0.7, the real book: 108 trees felled, 18 axe hits from 8 October, 2 774 wood; no per-tree section (none felled since); Wood Cutting 33; the box says 0 trees, 0 wood so far");
        }
        // ---------- Mining (0.7, the worked page's pattern): stone and ore brought in with the pickaxe hits beside it, the Pickaxes skill in the hero's row, the box ----------
        var mine = Show(world, "mining");
        var mItems = PanelModel.PlateOf(mine).Items;
        var mHero = mItems.First(b => b.Kind == "hero");
        var mRocks = mItems.Single(b => b.Kind == "section" && b.Title == "Pickaxe hits per rock");
        var mBar = mItems.Single(b => b.Kind == "composition" && b.Title == null);
        var mSkill = PanelModel.SkillOf(mHero);
        var mBox = PanelModel.PlateOf(Show(world, "mining", s => s.ShowNumbers = true)).Items.Single(b => b.Kind == "aboutnumbers");
        Check(mine.Recorded && !mItems.Any(b => b.Kind == "zone" || b.Kind == "ladders") && mHero.Value == "2\u00A0037" && mHero.Title == "stone and ore brought in" && mHero.Src == PanelModel.SrcCharacter &&
              mHero.Note == null && mHero.Items.Count(i => i.Kind != PanelModel.SparkKind) == 2 && mHero.Items[0] is Block mHits && mHits.Kind == "number" && mHits.Value == "120" && mHits.Title == "pickaxe hits" && mHits.Src == PanelModel.SrcPc && mHits.RecordedFrom == "from 1 October" &&
              mBar.Value == null && mBar.Src == PanelModel.SrcCharacter && mBar.Text == null && mBar.Items.All(p => p.Fraction2 == 0) &&
              mRocks.Value == null && mRocks.RecordedFrom == "Recorded from 1 October · this PC" &&
              mSkill != null && mSkill.Id == "Skills/Pickaxes" && mSkill.Value == "22" &&
              !mItems.Any(b => b.Text == PanelModel.EarlierIncomplete) && PanelModel.AboutText(mine).Contains(PanelModel.EarlierIncomplete) && Incomplete(mine) == 1 &&
              !PanelModel.AllText(mine).Any(t => t.Contains("since install") || t.Contains("faded") || t.Contains("in all ·") || t.Contains("since you made")),
              "mining 0.7: 2 037 stone and ore brought in (your character's whole count), 120 pickaxe hits \"from 1 October\" beside it and Pickaxes level 22 in the hero's row (it wraps when full); \"Pickaxe hits per rock\" \"Recorded from 1 October · this PC\"; \"Earlier counts may be incomplete.\" once, in About these numbers (0.8 layout D+); no zone, no skill strip, no since install");
        Check(mBox.Items.Count == 5 && mBox.Items[3].Title == PanelModel.AboutEarlierLabel && mBox.Items[0].Title == "Before 1 October" && mBox.Items[0].Text.StartsWith("The game's own count: 1\u00A0790. It missed stone and ore") &&
              mBox.Items[1].Text == "Hearthwoven counted every piece you picked up, on this PC: 247 so far." &&
              mBox.Items[2].Text == "Pickaxe hits are only known from 1 October. The game's own pickaxe count leaves out rocks in an area another player's PC hosted, so the book shows Hearthwoven's. " +
                                  "From 3 October, stone and ore from pieces that came down count on Building, not here." &&
              mine.Keys.Contains("[Y] Numbers"),
              "mining 0.7: About these numbers says before 1 October the game's own 1 790 and from 1 October 247 counted here; the pickaxe hits only from 1 October; " +
              "0.8: stone and ore from pieces that came down count on Building from its own date; Y opens it");
        var mWeek = Show(world, "mining", s => s.Window = TimeWindow.SevenDays);
        Check(mWeek.Recorded && mWeek.Windowed && PanelModel.SkillOf(PanelModel.Content(mWeek).FirstOrDefault(b => b.Kind == "hero")) != null && mWeek.AboutNumbers == null &&
              !PanelModel.Content(mWeek).Any(b => b.Kind == "ladders" || b.Kind == "zone" || !string.IsNullOrEmpty(b.RecordedFrom)) && Incomplete(mWeek) == 0,
              "mining 0.7, 7 days: the window's numbers, the skill beside the hero; no label, no \"Earlier counts\", no box (rules W.1, A.6, K.5)");
        return fails;
    }
}
