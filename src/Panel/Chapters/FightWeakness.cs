using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9.4 draft (branch h094-weakness; Joost on Last fight against Lord Reto: "we should definitely have some form of design in here that shows
    /// us the weaknesses"; then, of options A and B: "I think I want A, but we need to make each resist, weakness, immune more visually distinct"):
    /// what a foe takes more and less damage from, under a fight's foe rows, from the game's own modifiers (Character.m_damageModifiers, read by
    /// BattleGame.Foe in Chapters/BattleUi.cs; PanelInput.Foe here, the same data the Foes table and What to use read).
    ///
    /// A line per kind of foe under the foe rows' keys (DEALT, RECEIVED): a caption in small capitals per group (WEAK TO, RESISTS, IMMUNE) and its
    /// types, each group in its own form so the three read apart at a glance, by shape and not by a good or bad colour (a damage type's colour stays
    /// its identity, VOCABULARY.md): WEAK TO a solid chip (a ground, the type's swatch filled, the name bright, the multiplier in gold); RESISTS an
    /// open one (no ground, the swatch an outline, the name and multiplier quiet); IMMUNE a struck one (the outline crossed, the name struck through,
    /// faint, ×0). The game's VeryWeak and VeryResistant join Weak and Resistant, the strongest first: their multiplier (×2, ×0.25) says "very",
    /// and the line stays one line. Kinds with the same modifiers share one line, named together (past two, "Greydwarf and 2 more kinds"); a boss
    /// fight with foes around it shows the boss's line alone, led by the boss's ring; a fight of one kind names nobody. At most WeakLinesMost lines,
    /// a run's page one (the kind dealt the most damage to): the page still fits one screen (render-check).
    ///
    /// Kind WeakLineKind inside the fightfoerows block: Title = the kinds' names (null for one kind and for a boss), Tone "boss" with Icon the boss's
    /// trophy and Text its name (the ring's), Items Kind WeakKeyKind per group (Title the caption, Tone weak | resist | immune | none, Items Kind
    /// "key": Id the type, Title its name, Value the multiplier "×1.5", Colour the type's).
    /// Nothing at all when the foe's data carries no modifiers (a copy without them): never guessed. Pure C#: test-panel/FightWeaknessTests.cs.
    /// </summary>
    public static partial class PanelModel
    {
        public const string WeakLineKind = "weakline", WeakKeyKind = "weakkey";
        public const string WeakTone = "weak", ResistTone = "resist", ImmuneTone = "immune";
        /// <summary>The most weakness lines under a fight's foe rows (kinds with the same modifiers share one).</summary>
        public const int WeakLinesMost = 2;
        public const string NoWeakness = "Every type ×1";
        public const string WeakAboutTitle = "Weak to, resists, immune";
        public const string WeakAbout = "From the game's own numbers for each kind of foe, under the foe rows: ×1.5 means a hit of that type does half again as much, " +
                                        "×0.5 half, ×0 nothing. Weak to: a filled chip; resists: an open one; immune: struck through. Dealt is counted before them.";

        // the groups in their order: what helps most first
        static readonly (string tone, string caption, Func<float, bool> holds)[] WeakGroups =
        {
            (WeakTone, "Weak to", m => m > 1f), (ResistTone, "Resists", m => m < 1f && m > 0f), (ImmuneTone, "Immune", m => m <= 0f),
        };

        /// <summary>A multiplier as the page says it: "×1.5", "×2", "×0.25", "×0".</summary>
        public static string Times(float m) => "×" + m.ToString("0.##", CultureInfo.InvariantCulture);

        static float MultOf(FoeData d, string type) => Multiplier(d.Modifiers.TryGetValue(type, out var m) ? m : "Normal");

        /// <summary>Whether the game's data for a foe carries its modifiers (a copy without them: no line).</summary>
        public static bool KnowsModifiers(FoeData d) => d != null && BattleTypes.Any(t => d.Modifiers.ContainsKey(t));

        /// <summary>One line: the groups of a foe's modifiers, each with its types, the strongest first (×2 before ×1.5), else in the palette's
        /// order; null when its data carries none.</summary>
        public static Block WeakLine(FoeData d, string title)
        {
            if (!KnowsModifiers(d)) return null;
            var line = new Block { Kind = WeakLineKind, Title = title, Items = new List<Block>() };
            foreach (var g in WeakGroups)
            {
                var types = BattleTypes.Where(t => g.holds(MultOf(d, t))).OrderByDescending(t => Math.Abs(MultOf(d, t) - 1f)).ThenBy(t => Array.IndexOf(BattleTypes, t)).ToList();
                if (types.Count == 0) continue;
                line.Items.Add(new Block
                {
                    Kind = WeakKeyKind, Title = g.caption, Tone = g.tone,
                    Items = types.Select(t => new Block { Kind = "key", Id = t, Title = TypeName(t), Value = Times(MultOf(d, t)), Colour = DamageColour(t) }).ToList(),
                });
            }
            if (line.Items.Count == 0) line.Items.Add(new Block { Kind = WeakKeyKind, Title = NoWeakness, Tone = "none", Items = new List<Block>() });
            return line;
        }

        /// <summary>
        /// The lines under a fight's foe rows (<paramref name="rows"/> in the page's order, the boss first): a boss fight the boss's line alone (led
        /// by its ring when foes were around it); otherwise a line per kind the game has modifiers for, kinds with the same ones on one line, at most
        /// WeakLinesMost (a run's page: one). Named when the fight has more than one row and no boss.
        /// </summary>
        public static List<Block> WeakLines(PanelInput input, IList<FightFoe> rows, bool run = false)
        {
            var lines = new List<Block>();
            if (input == null || rows == null || rows.Count == 0) return lines;
            var boss = rows.FirstOrDefault(x => x.Boss);
            var groups = new List<(string sig, FoeData data, List<string> names)>();
            foreach (var x in boss != null ? new List<FightFoe> { boss } : rows.ToList())
            {
                var d = FoeOf(input, x.Prefab);
                if (!KnowsModifiers(d)) continue;
                var sig = string.Join(" ", BattleTypes.Select(t => Times(MultOf(d, t))).ToArray());
                var name = Who(input, x.Prefab);
                var at = groups.FindIndex(g => g.sig == sig);
                if (at >= 0) { if (!groups[at].names.Contains(name)) groups[at].names.Add(name); }
                else groups.Add((sig, d, new List<string> { name }));
            }
            foreach (var g in groups.Take(run ? 1 : WeakLinesMost))
            {
                var line = WeakLine(g.data, rows.Count > 1 && boss == null ? (g.names.Count <= 2 ? Names(g.names) : g.names[0] + " and " + (g.names.Count - 1) + " more kinds") : null);
                if (boss != null && rows.Count > 1) { line.Tone = "boss"; line.Icon = BossTrophy(input, boss.Prefab); line.Text = Who(input, boss.Prefab); }
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>Whether a fight's page shows a weakness line (its About line).</summary>
        public static bool ShowsWeakness(PanelInput input, FightView f) => input != null && f != null && FoeRowsOf(input, f).Any(x => KnowsModifiers(FoeOf(input, x.Prefab)));
    }
}
