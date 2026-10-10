using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8: skills at the right end of a component's head row (Chapters/SkillsBeside.cs): the hero's skill form (RecordedUi.HeroSkill: the game's
    /// skill icon, its name muted, "level 38" bold, the thin progress line under them), one after another, 24 px apart; a click opens the skill's page.
    /// </summary>
    public partial class PanelUi
    {
        const float HeadSkillGap = 24;

        static void HeadSkills(RectTransform row, IList<Block> skills, Func<string, Action> link, float icon = 22, int bottom = 0)
        {
            if (skills == null || skills.Count == 0) return;
            Node("Rest", row).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;   // right-aligned: the row's last items
            for (int k = 0; k < skills.Count; k++)
            {
                var s = skills[k];
                if (k > 0) Size(Node("Gap", row), HeadSkillGap, 1);
                var click = link?.Invoke(s.Id);
                var hit = Img(row, "Skill", null, new Color(0f, 0f, 0f, 0f), raycast: click != null);
                var h = hit.gameObject.AddComponent<HorizontalLayoutGroup>(); Layout(h, 8, TextAnchor.LowerLeft);
                h.padding = new RectOffset(0, 0, 0, bottom);
                Marker((RectTransform)hit.transform, s.Icon, icon);
                SkillWords((RectTransform)hit.transform, s);
                if (click != null) hit.gameObject.AddComponent<Press>().Act = click;
            }
        }
    }
}
