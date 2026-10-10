// 0.7 redesign, page group G5 Battle: Overview, Damage, Defence, Deaths, Foes
// (work/hearthwoven-0.7/REDESIGN-RULES.md part 5). The page agent of this group adds ONE test per page here that captures the page's rule
// (pattern: RecordedGatherTests.Woodcutting). Registered in Program.cs; never edit Program.cs from a page agent.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class RecordedBattleTests
{
    public static int Run(PanelInput input, PanelInput edda, DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Show(PanelInput i, string page, TimeWindow w = TimeWindow.SinceInstall, Action<PanelState> more = null)
        {
            var s = new PanelState { Chapter = Chapter.Battle, Window = w, Page = { [Chapter.Battle] = page } };
            more?.Invoke(s); return PanelModel.Build(i, s);
        }
        Block First(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
        // the first block with this title, anywhere on the page (a switch holds its views, a view holds the blocks)
        Block Named(PanelView v, string title) => Deep(PanelModel.Content(v)).FirstOrDefault(b => b.Title == title);
        IEnumerable<Block> Deep(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Deep(b.Items ?? new List<Block>())));
        int Incomplete(PanelView v) => PanelModel.AllText(v).Count(t => t == PanelModel.EarlierIncomplete);
        bool NoSinceInstall(PanelView v) => !PanelModel.AllText(v).Any(t => t.IndexOf("since install", StringComparison.OrdinalIgnoreCase) >= 0);

        var world = PanelSample.Full(now);

        // Overview on All (0.7): the biome strip counted on this PC; no heading window; the About box with its three lines
        var overview = Show(world, "overview");
        var strip = First(overview, "biomes");
        Check(overview.Recorded && overview.HeadingWindow == null && NoSinceInstall(overview) && overview.AboutNumbers?.Items?.Count == 3 &&
              strip != null && strip.Src == PanelModel.SrcPc && Incomplete(overview) == 0,
              "battle 0.7, overview on All: the biome strip counted on this PC, no since install, no heading window, the About box with three lines");

        // Damage on All: the since-install totals (no biome on All), the Biome line says why, the grid counted on this PC
        var damage = Show(world, "damage");
        Check(damage.Recorded && damage.HeadingWindow == null && NoSinceInstall(damage) &&
              Deep(PanelModel.Content(damage)).Any(b => b.Kind == "filterbar" && (b.Items ?? new List<Block>()).Any(i => i.Title == PanelModel.DamageAllBiomeNote)) && Deep(PanelModel.Content(damage)).Where(b => b.Kind == "dmgmix").Any() && Deep(PanelModel.Content(damage)).Where(b => b.Kind == "dmgmix").All(b => b.Src == PanelModel.SrcPc),
              "battle 0.7, damage on All: the totals since the install counted on this PC, the Biome line says no biome on All, no since install word");

        // Defence on All: blocks and parries are Hearthwoven's, dated per block; the heading gets no label (the page mixes in the game's hits received)
        var defence = Show(world, "defense");
        var guard = First(defence, "guard");
        Check(defence.Recorded && defence.HeadingRecordedFrom == null && guard != null && guard.Src == PanelModel.SrcPc && !string.IsNullOrEmpty(guard.RecordedFrom) &&
              NoSinceInstall(defence) && PanelModel.Content(defence).Any(b => b.Kind == "section" && b.Title == "Hits received"),
              "battle 0.7, defence on All: blocks and parries carry their \"from ...\" date, the heading none, the game's hits received stay in their own section");

        // Your armour: the stopped count and the armour rows carry the ledger's own date, the same date as its "Recorded from" line
        var armour = Show(world, "defense", TimeWindow.SinceInstall, s => s.View["Battle/defense/view"] = PanelModel.ArmourId);
        var stopped = Named(armour, PanelModel.ArmourStopped);
        var ledger = Named(armour, PanelModel.ArmourByType);
        Check(stopped != null && ledger != null && stopped.From.HasValue && ledger.From == stopped.From && ledger.Text == PanelModel.ArmourFromLine(world) &&
              stopped.RecordedFrom == "from " + PanelModel.RecordDate(world, stopped.From.Value),
              "battle 0.7, defence Your armour: the stopped count and the armour rows carry the ledger's own date, the same as its \"Recorded from\" line");

        // Your armour on All has its own box (MERGE-NOTES G5: the chapter's box spoke of foes, hits and deaths there); Received keeps the chapter's
        Check(armour.AboutNumbers?.Items?.Count == PanelModel.ArmourAbout.Length && armour.AboutNumbers.Items[0].Title == PanelModel.FromLabel(world, world.ArmourFromUtc ?? world.SessionStartUtc ?? world.NowUtc) &&
              armour.AboutNumbers.Items.Skip(1).Select(l => l.Title).SequenceEqual(PanelModel.ArmourAbout.Skip(1).Select(l => l.title)) &&
              defence.AboutNumbers?.Items?.Any(l => l.Text == PanelModel.BattleBefore) == true,
              "battle 0.7, Your armour: its own About these numbers (the armour lines, the first under the ledger's date); Received keeps the chapter's box");

        // Deaths on All: the lifetime count is one sum (rule A) with the earlier counts note in its slot; the death list stays this session's
        var deaths = Show(world, "deaths");
        var lifetime = First(deaths, "hero");
        Check(deaths.Recorded && lifetime != null && lifetime.Title == "deaths" && lifetime.Src == PanelModel.SrcCharacter && lifetime.Note == null && PanelModel.AboutText(deaths).Contains(PanelModel.EarlierIncomplete) &&
              PanelModel.Content(deaths).Any(b => b.Kind == "deaths") && NoSinceInstall(deaths) && Incomplete(deaths) == 1,
              "battle 0.7, deaths on All: the lifetime deaths are one sum with \"Earlier counts may be incomplete.\" in About these numbers (0.8 layout D+), no since install, the list stays this session's");

        // Foes on All: hits on foes are one sum (rule A), the earlier counts note once for the page, By foe is counted on this PC
        var foes = Show(world, "foes");
        var hits = Named(foes, "hits on foes");
        Check(foes.Recorded && hits != null && hits.Src == PanelModel.SrcCharacter && Incomplete(foes) == 1 && NoSinceInstall(foes) &&
              PanelModel.Content(foes).Any(b => b.Kind == "foetable" && b.Src == PanelModel.SrcPc),
              "battle 0.7, foes on All: hits on foes are one sum (Src character) with the earlier counts note once; By foe counted on this PC; no since install");

        // A short window keeps its texts and carries no dates and no box (rule W.1)
        var session = Show(world, "overview", TimeWindow.Session);
        Check(!PanelModel.Content(session).Any(b => !string.IsNullOrEmpty(b.RecordedFrom)) && session.HeadingRecordedFrom == null && session.AboutNumbers == null,
              "battle 0.7, a short window (This session): no dates, no box (rule W.1)");

        // A fellow's book on All: no box, whose copy it is, never since install
        var eddaFoes = Show(edda, "foes");
        Check(eddaFoes.AboutNumbers == null && NoSinceInstall(eddaFoes) && (eddaFoes.Scope ?? "").Contains("Edda"),
              "battle 0.7, a fellow's Foes on All: no box, no since install, whose copy it is");
        return fails;
    }
}
