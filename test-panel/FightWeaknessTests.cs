// 0.9.4 draft (h094-weakness): what a foe is weak to on a fight's page (src/Panel/Chapters/FightWeakness.cs), on the fight sample with its SAMPLE
// modifiers (PanelSampleFights.cs): a line per kind under the foe rows (a boss alone, kinds with the same modifiers on one named line, a run one
// line), its three groups each in its own form (Tone weak | resist | immune) with the multipliers; By type, together left as it was (option B
// dropped, Joost: "I think I want A"); nothing at all without modifiers. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FightWeaknessTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelState At(string page) => new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = page } };
        IEnumerable<Block> All(Block b) => new[] { b }.Concat((b?.Items ?? new List<Block>()).SelectMany(All));
        Block Find(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
        List<Block> Of(Block b, string kind) => (b?.Items ?? new List<Block>()).Where(i => i.Kind == kind).ToList();
        List<Block> Lines(PanelView v) => Of(Find(v, PanelModel.FightFoesKind), PanelModel.WeakLineKind);
        string Groups(Block line) => string.Join(" | ", Of(line, PanelModel.WeakKeyKind).Select(g => g.Title + ": " + string.Join(", ", Of(g, "key").Select(k => k.Title + " " + k.Value))));
        Block Together(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "composition" && b.Tone == PanelModel.FightTypesTone);
        bool AboutSays(PanelView v) => new[] { v.AboutNumbers }.Concat(PanelModel.Content(v)).Where(b => b != null).SelectMany(All).Any(b => b != null && b.Title == PanelModel.WeakAboutTitle);
        bool Plain(PanelView v) => Together(v) != null && Together(v).Items.All(p => string.IsNullOrEmpty(p.Value2)) && PanelModel.Content(v).All(b => b.Kind != "weaksay");

        Check(PanelModel.Times(1.5f) == "×1.5" && PanelModel.Times(2f) == "×2" && PanelModel.Times(0.25f) == "×0.25" && PanelModel.Times(0f) == "×0", "K multipliers read x1.5, x2, x0.25, x0");

        // Moder alone
        var v = PanelModel.Build(FightsSample.WithSoloBoss(FightsSample.With(PanelSample.Full(now), false)), At(PanelModel.LastFightPage));
        var lines = Lines(v);
        Check(lines.Count == 1 && lines[0].Title == null && Groups(lines[0]) == "Weak to: Pierce ×1.5, Fire ×1.5 | Resists: Blunt ×0.5 | Immune: Frost ×0",
              "K Moder alone: one unnamed line, weak to pierce and fire, resists blunt, immune to frost (" + (lines.Count > 0 ? Groups(lines[0]) : "none") + ")");
        Check(lines.Count == 1 && Of(lines[0], PanelModel.WeakKeyKind).Select(g => g.Tone).SequenceEqual(new[] { PanelModel.WeakTone, PanelModel.ResistTone, PanelModel.ImmuneTone }),
              "K the three groups carry their own form (weak, resist, immune), in that order");
        Check(Plain(v), "K By type, together is as it was: no marks, no words under it (option B dropped)");
        Check(AboutSays(v), "K About these numbers says what the multipliers and the three forms are");

        // Lord Reto (Joost's fight, sample cast and SAMPLE modifiers)
        var reto = PanelModel.Build(FightsSample.WithReto(FightsSample.With(PanelSample.Full(now), false)), At(PanelModel.LastFightPage));
        lines = Lines(reto);
        Check(lines.Count == 1 && lines[0].Title == null && Groups(lines[0]) == "Weak to: Frost ×1.5, Spirit ×1.5 | Resists: Poison ×0.5 | Immune: Fire ×0",
              "K Lord Reto: weak to frost and spirit, resists poison, immune to fire (" + (lines.Count > 0 ? Groups(lines[0]) : "none") + ")");
        var hero = Find(reto, PanelModel.FightHeadKind);
        Check(hero != null && hero.Items.First(i => i.Kind == "number").Value.Replace(" ", " ").Replace(" ", " ").Replace(",", " ") == "13 032", "K Lord Reto: 13 032 dealt together, as on Joost's screenshot");

        // 0.9.10 Joost's boss fight (on 0.9.9 its name and land were drawn over the first number), retold with the sample cast: Fader in the Ashlands,
        // won, the head's three numbers and their words as on his screenshot (the preview battle-lastfight-bosshead draws it)
        var fader = Find(PanelModel.Build(FightsSample.WithBigBoss(FightsSample.With(PanelSample.Full(now), false)), At(PanelModel.LastFightPage)), PanelModel.FightHeadKind);
        string Flat(string t) => t?.Replace(" ", " ").Replace(" ", " ").Replace(" ", " ").Replace(",", " ");
        var faderNums = fader == null ? "" : string.Join(" | ", fader.Items.Where(i => i.Kind == "number").Select(i => Flat(i.Value) + " " + i.Title));
        Check(fader != null && fader.Tone == "boss" && fader.Title == "Fader" && fader.Text == "Ashlands" && faderNums == "58 118 dealt together | 2 092 received together | 144 DPS together"
              && fader.Items.Any(i => i.Kind == "lane" && i.Title == "defeated"),
              "K Fader (0.9.10): the head says Fader, Ashlands, 58 118 dealt together, 2 092 received together, 144 DPS together, defeated (" + faderNums + ")");

        // two kinds: a named line each
        var two = PanelModel.Build(FightsSample.WithTwoKinds(FightsSample.With(PanelSample.Full(now), false)), At(PanelModel.LastFightPage));
        lines = Lines(two);
        Check(lines.Select(l => l.Title).SequenceEqual(new[] { "Draugr", "Blob" }) && Groups(lines[0]) == "Resists: Fire ×0.5 | Immune: Poison ×0",
              "K two kinds: Draugr and Blob, each its own named line (" + string.Join(" / ", lines.Select(l => l.Title + " " + Groups(l))) + ")");

        // the Queen with her adds: her line alone, led by her ring
        lines = Lines(PanelModel.Build(FightsSample.With(PanelSample.Full(now), true), At(PanelModel.LastFightPage)));
        Check(lines.Count == 1 && lines[0].Title == null && lines[0].Tone == "boss" && lines[0].Text == "The Queen" && lines[0].Icon == "item:TrophySeekerQueen",
              "K a boss with adds: the boss's line alone, led by her ring");
        // Bonemass: VeryWeak folded into Weak to, the strongest first
        lines = Lines(PanelModel.Build(FightsSample.With(PanelSample.Full(now), true), At("fight:bonemass-1")));
        Check(lines.Count == 1 && Groups(lines[0]) == "Weak to: Blunt ×2, Spirit ×1.5 | Resists: Slash ×0.5, Pierce ×0.5 | Immune: Poison ×0",
              "K Bonemass: weak to blunt (x2, the strongest first) and spirit (" + (lines.Count > 0 ? Groups(lines[0]) : "none") + ")");
        var mine = Lines(PanelModel.Build(FightsSample.With(PanelSample.Full(now), true), At("fight:mine-1")));
        Check(mine.Count == 1 && mine[0].Title == "Seeker", "K a run's page: one line, the kind dealt the most damage to (" + string.Join(" / ", mine.Select(l => l.Title)) + ")");
        lines = Lines(PanelModel.Build(FightsSample.With(PanelSample.Full(now), false), At(PanelModel.LastFightPage)));
        Check(lines.Count == 1 && lines[0].Title == "Greydwarf and 2 more kinds", "K kinds with the same modifiers share one line (" + string.Join(" / ", lines.Select(l => l.Title)) + ")");

        // no modifiers: nothing
        var none = PanelModel.Build(FightsSample.WithoutModifiers(FightsSample.Friend(FightsSample.WithSoloBoss(FightsSample.With(PanelSample.Full(now), false)), "Edda")), At(PanelModel.LastFightPage));
        Check(Find(none, PanelModel.FightFoesKind) != null && Lines(none).Count == 0 && Plain(none) && !AboutSays(none), "K a book whose foe data carries no modifiers: no line, no About line");
        return fails;
    }
}
