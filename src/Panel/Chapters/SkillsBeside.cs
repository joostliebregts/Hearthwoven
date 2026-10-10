using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8: the matching skill next to its component where 0.7 did not show it (VOCABULARY.md rule: the skill's name, level and progress line at the
    /// right end of the component's head row, never below the fold). 0.7 already had it on Woodcutting, Mining, Cooking, Crafting, Farming and Fishing
    /// (SkillBeside, the hero's row) and Jump, Run and Swim beside On foot (ladders). 0.8 adds: Defence's blocks and parries their Blocking skill;
    /// Voyages > Sailing a sailing skill when a mod adds one (the game has none). A skill shows only when the character has it (a level or
    /// practice), as SkillBeside. Battle > Damage's weapons keep theirs in the page's Weapon skills strip (0.8 damage rows: By weapon, By type and
    /// By foe in one grammar, no skills on a weapon's own row).
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>The skills a block's head row carries at its right end (Kind "skill", in order).</summary>
        public static List<Block> SkillsOf(Block b) => (b?.Items ?? new List<Block>()).Where(i => i.Kind == "skill").ToList();

        /// <summary>A skill as a head row's item (Kind "skill", Id "Skills/&lt;skill&gt;" so a click opens its page); null when the character
        /// lacks it (no level, no progress, no practice). It stands beside the block's counts, not among them: the "Recorded from ... · this PC"
        /// pass (RecordedModel.LabelRecorded) labels the block as it did without it (once under a weapon band, not after every damage type).</summary>
        static Block SkillItem(PanelInput input, string skill, ICollection<string> have = null)
        {
            if (string.IsNullOrEmpty(skill) || !(have ?? SkillNames(input).ToList()).Contains(skill)) return null;
            var s = LadderOf(input, skill, "skill");
            if (s.Level <= 0 && s.Progress <= 0 && Practice(input, skill) <= 0) return null;
            s.Id = Chapter.Skills + "/" + skill;
            return s;
        }

        /// <summary>A skill a mod adds, found by its name: the game keys a mod's skill by a number, so the key whose own name or shown name is
        /// <paramref name="name"/>; null when the character has no such skill.</summary>
        static string SkillKeyNamed(PanelInput input, string name) =>
            SkillNames(input).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase) || string.Equals(SkillName(input, k), name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Defence's blocks and parries carry the Blocking skill at their row's right end.</summary>
        static void GuardSkill(PanelInput input, Block guard)
        {
            var s = guard == null ? null : SkillItem(input, "Blocking");
            if (s != null) (guard.Items ?? (guard.Items = new List<Block>())).Add(s);
        }

        /// <summary>The name a mod's sailing skill goes by (Voyages > Sailing shows it in the hero's row when the character has one).</summary>
        public const string SailingSkillName = "Sailing";
    }
}
