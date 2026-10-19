using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 deed feeds, drawn (Chapters/DeedFeedModel.cs; mirrored in preview/panel-preview.html): the Battle feed's own drawing (BattleFeedUi.cs) with
    /// two named variants. An entry of a deed stretch has, in the dealt bar's place, the four-part level meter of an upgrade (the weakness meter drawer,
    /// BattleUi.BattleMeter, filled to the level reached out of the item's highest level) and its words ("level 3 of 4"), or a made line's count ("×2").
    /// Under a stretch's lines, what it did by kind: each kind's words, quiet, over the strip of picture, number and name (DeedsUi.Strip).
    /// </summary>
    public partial class PanelUi
    {
        const float DfValueGap = 8, DfCountsGap = 6, DfMeterParts = 4, DfLevelMost = 99;
        /// <summary>The Timeline's second line (BattleFeedUi.FeedTimeline: its top and height) and the Cards' gap under the bar's place.</summary>
        const float DfLineTop = 26, DfLineH = 22, DfCardGap = 6;
        /// <summary>The Log's name column in a deed feed at most: a piece's long name whole ("Whole Roasted Meadow Boar"); its lines have no damage bar.</summary>
        const float DfNameMax = 280;

        /// <summary>An entry of a deed stretch (made or upgraded), not a foe's.</summary>
        static bool IsDeedEntry(Block e) => e != null && (e.Tone == "made" || e.Tone == "upgraded");

        /// <summary>In the dealt bar's place of a deed line: the upgrade's level meter and its words, or the count. Returns the width used.</summary>
        static float DeedValue(RectTransform r, Block e, float x, float top, float rowH, float width)
        {
            float used = 0;
            if (e.Tone == "upgraded")
            {
                LevelMeter(r, x, top + Mathf.Round((rowH - BtMeterH) / 2), BtMeterW, BtMeterH, BtMeterGap, Mathf.RoundToInt(e.Level), e.Count);   // 0.9: a part per level of the item's max (DeedsUi.LevelMeter, PanelModel.LevelMeterOf)
                used = BtMeterW + DfValueGap;
            }
            if (string.IsNullOrEmpty(e.Value)) return used;
            var t = BtText(r, e.Value, x + used, top, Mathf.Max(0, width - used), rowH, FdText, e.Tone == "upgraded" ? PanelLook.Muted : PanelLook.Text, style: e.Tone == "upgraded" ? FontStyles.Normal : FontStyles.Bold);
            return used + Mathf.Ceil(t.preferredWidth);
        }

        /// <summary>A stretch's counts by kind under its lines: per kind its words (quiet), then its strip (picture, number, name), "+N more" after it.</summary>
        static void DeedCounts(RectTransform col, Block stretch)
        {
            var counts = (stretch.Items ?? new List<Block>()).Where(x => x.Kind == "counts" && x.Items != null && x.Items.Count > 0).ToList();
            foreach (var c in counts)
            {
                Spacer(col, DfCountsGap);
                var head = Line(col, DfValueGap);
                var t = Label(head, c.Title ?? "", PanelLook.MinText, PanelLook.Muted); t.textWrappingMode = TextWrappingModes.NoWrap;
                if (!string.IsNullOrEmpty(c.Note)) { var n = Label(head, c.Note, PanelLook.MinText, PanelLook.Faint); n.textWrappingMode = TextWrappingModes.NoWrap; }
                Strip(col, new Block { Kind = "strip", Items = c.Items }, null);
            }
        }
    }
}
