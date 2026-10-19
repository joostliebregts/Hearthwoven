// 0.8.5 Battle > Damage and Foes with your damage after the foe's armor (Chapters/AfterArmourModel.cs): a part's outlined piece is what you swung
// minus what got through; a type the foe is immune to shows 0 got through and all of it stopped; a window reaching back before the record began
// draws those hits as the part's faded piece, never as stopped; a fellow's copy has the after where it is shared (the log), not on All.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class AfterArmourPanelTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        double N(string v) => v == null || v == PanelModel.LessThanOne ? 0 : PanelModel.ParseCount(v);
        PanelState Damage(TimeWindow w) { var s = new PanelState { Chapter = Chapter.Battle, Window = w, Page = { [Chapter.Battle] = "damage" } }; s.View["Battle/damage/view"] = "foe"; return s; }
        List<Block> Rows(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "dealtfoes")?.Items.Where(i => i.Kind == "source").ToList() ?? new List<Block>();
        List<Block> Parts(Block row) => (row?.Items ?? new List<Block>()).Where(p => p.Kind == "part").ToList();
        var world = PanelSample.Full(now);

        // the outline piece = before - after (the shown amounts add up to what you swung), a weakness all filled with nothing stopped, an immune
        // type 0 got through and all of it stopped (the sample's Draugr: immune to poison, resistant to fire)
        var session = PanelModel.Build(world, Damage(TimeWindow.Session));
        var split = Rows(session).SelectMany(Parts).Where(p => p.Whole != null).ToList();
        var resisted = split.Where(p => N(p.Value) <= N(p.Whole)).ToList(); var weak = split.Where(p => N(p.Value) > N(p.Whole)).ToList();
        var draugr = Rows(session).FirstOrDefault(r => r.Id == "Draugr"); var poison = Parts(draugr).FirstOrDefault(p => p.Id == "poison"); var fire = Parts(draugr).FirstOrDefault(p => p.Id == "fire");
        Check(split.Count >= 10 && resisted.All(p => Math.Abs(N(p.Value) + N(p.Value2) - N(p.Whole)) < 1.01 && Math.Abs(p.Fraction2 - N(p.Value) / N(p.Whole)) < 0.02) &&
              weak.Count > 0 && weak.All(p => p.Value2 == null && p.Fraction2 >= 0.999f) &&
              poison?.Value == "0" && poison.Value2 == poison.Whole && poison.Fraction2 == 0 && fire != null && N(fire.Value2) > 0 && N(fire.Value) < N(fire.Whole) &&
              draugr.Text != null && draugr.Text.EndsWith(PanelModel.ThroughWords) &&
              // 0.9 more feats: with a feat earned on Damage the strip carries it, and the caption moves to About these numbers (PageHead.SettleHead)
              (PanelModel.SwitchOf(session)?.Title == PanelModel.DealtAfterCaption || PanelModel.AllText(session).Any(t => t.IndexOf(PanelModel.DealtAfterCaption, StringComparison.OrdinalIgnoreCase) >= 0)),
              "after: the outlined piece is what you swung minus what got through (" + resisted.Count + " parts), a weakness all filled (" + weak.Count + "), Draugr's poison (immune) 0 got through and " +
              poison?.Value2 + " stopped; the line ends \"" + draugr?.Text + "\"; the caption says before and after");

        // All reaches back before the record began (0.8.6): armor was not counted for all of it, so no bar of the window is split (no part by the after
        // rule, no caption of before and after), and one line says where the split is known: the longest chip that starts after the record's date.
        // The same on Foes; the short windows and the day windows that start after that date are split again. Hover on a split part says both numbers.
        var all = PanelModel.Build(world, Damage(TimeWindow.SinceInstall));
        var from = world.DealtArmourFromUtc;
        var line = PanelModel.Content(all).FirstOrDefault(b => b.Kind == "note" && b.Text != null && b.Text.StartsWith("Armor counted from"))?.Text;
        var expect = from.HasValue ? "Armor counted from " + PanelModel.RecordDate(world, from.Value) + ": choose " : null;
        var foesAll = PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.SinceInstall, Page = { [Chapter.Battle] = "foes" } });
        var tableAll = PanelModel.Content(foesAll).FirstOrDefault(b => b.Kind == "foetable");
        bool Plain(PanelView v) => !PanelModel.Content(v).SelectMany(b => b.Items ?? new List<Block>()).Any(i => i.Whole != null || i.Fraction2 > 0 && i.Kind == "foe") && PanelModel.SwitchOf(v)?.Title != PanelModel.DealtAfterCaption;
        var plain = Rows(all).SelectMany(Parts).All(p => p.Whole == null && p.Value2 == null && p.Fraction2 == 0 && p.Tone != PanelModel.DamageStoppedTone) && Rows(all).All(r => r.Text == null);
        Check(plain && Plain(all) && line != null && expect != null && line.StartsWith(expect) && line.EndsWith(" or shorter to see what got through.") &&
              tableAll != null && tableAll.Items.All(r => r.Text == null && r.Fraction2 == 0) && PanelModel.Content(foesAll).Any(b => b.Kind == "note" && b.Text == line),
              "after on All: hits from before armor was counted make every bar a plain dealt bar (" + Rows(all).SelectMany(Parts).Count() + " parts, no split, no caption of before and after), one line: \"" + line + "\", also on Foes");
        var draugrRow = Rows(session).FirstOrDefault(r => r.Id == "Draugr"); var slash = Parts(draugrRow).FirstOrDefault(p => p.Value2 != null);
        var hover = slash == null ? "" : PanelModel.DamageHoverWords(PanelModel.BarFormOf(Parts(draugrRow), true).Rows.First(r => r.Part == slash));
        Check(slash != null && hover.StartsWith(slash.Title + " " + slash.Whole + ": ") && hover.Contains(slash.Value + " " + PanelModel.ThroughWords) && hover.EndsWith(slash.Value2 + " " + PanelModel.ArmourStoppedWord),
              "hover on a split part says both numbers, what got through and what was stopped: \"" + hover + "\"");

        // Foes: your Dealt bar by the same rule, its words on the tag; the open foe says your hits on it; a fellow's copy has the after in its
        // shared short windows, none on All (only the log is shared), and then the page says before only
        var foes = PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Session, Page = { [Chapter.Battle] = "foes" }, View = { [PanelModel.FoeOpenKey] = "Draugr" } });
        var dr = PanelModel.Content(foes).FirstOrDefault(b => b.Kind == "foetable")?.Items.FirstOrDefault(r => r.Id == "Draugr");
        var mine = dr?.Items.FirstOrDefault(i => i.Kind == "ranking")?.Items.FirstOrDefault(i => i.Kind == PanelModel.YourHitsKind);
        var edda = world.Fellows.First(f => f.PlayerName == "Edda");
        var eddaShort = Rows(PanelModel.Build(edda, Damage(TimeWindow.Session))); var eddaAll = PanelModel.Build(edda, Damage(TimeWindow.SinceInstall));
        Check(dr?.Text != null && dr.Text.Contains(PanelModel.ThroughWords) && dr.Fraction2 > 0 && dr.Fraction2 < 1 && mine != null && mine.Items.Count == 1 && mine.Items[0].Text == draugr.Text &&
              eddaShort.SelectMany(Parts).Any(p => p.Whole != null) && !Rows(eddaAll).SelectMany(Parts).Any(p => p.Whole != null) && PanelModel.SwitchOf(eddaAll)?.Title != PanelModel.DealtAfterCaption,
              "after on Foes: Draugr's Dealt bar \"" + dr?.Text + "\", the open foe's Your hits on it (" + mine?.Items.FirstOrDefault()?.Text + "); Edda's copy: after in Session (the shared log), none on All");
        return fails;
    }
}
