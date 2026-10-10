using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Deeds > Recent with Everyone on (RecentPage.cs; Joost 2026-10-10: one row per player was cut off in game, "St..." and "+...", so each
    /// player gets two or three rows of items and the list scrolls): per player the shield and name on the left, "+N more" under the name
    /// when more grew; their gains in a grid of RecentCols columns to the right, each the picture, "+N" in bold and the name, which may take
    /// two lines, so nothing is cut off. A row the window cannot tell ("Not recorded", "nothing new") is one quiet line. Laid out once per
    /// fill. Mirrored in preview/panel-preview.html (recentPeople).
    /// </summary>
    public partial class PanelUi
    {
        const int RecentCols = 3;
        const float RecentNameW = 172, RecentCellH = 44, RecentCellGap = 12, RecentRowGap = 4, RecentPad = 8, RecentPicSize = 34;

        static void RecentPeople(RectTransform col, Block b)
        {
            float x0 = RecentNameW + 14, cellW = (Column - x0 - (RecentCols - 1) * RecentCellGap) / RecentCols;
            foreach (var p in b.Items ?? new List<Block>())
            {
                var items = p.Items ?? new List<Block>();
                int rows = Mathf.Max(1, (items.Count + RecentCols - 1) / RecentCols);
                var h = rows * RecentCellH + (rows - 1) * RecentRowGap + 2 * RecentPad;
                var box = Node("Player", col); Size(box, Column, h);
                Marker(box, p.Icon, 38, layout: false).Box(0, RecentPad + (RecentCellH - 38) / 2, 38, 38);
                OneLine(Label(box, p.Title, 20, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft)).rectTransform.Box(52, RecentPad, RecentNameW - 52, RecentCellH);
                if (!string.IsNullOrEmpty(p.Note))   // "+3 more": what grew beyond the grid, quiet under the name
                    OneLine(Label(box, p.Note, 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic)).rectTransform.Box(52, RecentPad + RecentCellH, RecentNameW - 52, 20);
                for (int k = 0; k < items.Count; k++)
                {
                    var i = items[k];
                    float cx = x0 + (k % RecentCols) * (cellW + RecentCellGap), cy = RecentPad + (k / RecentCols) * (RecentCellH + RecentRowGap), x = cx;
                    var right = items.Count == 1 && string.IsNullOrEmpty(i.Value) ? Column : cx + cellW;   // a quiet line alone takes the row
                    if (!string.IsNullOrEmpty(i.Icon)) { Pic(box, i.Icon, RecentPicSize).Box(x, cy + (RecentCellH - RecentPicSize) / 2, RecentPicSize, RecentPicSize); x += RecentPicSize + 8; }
                    if (!string.IsNullOrEmpty(i.Value))
                    {
                        var value = OneLine(Label(box, i.Value, 18, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Bold));
                        var vw = Mathf.Ceil(value.preferredWidth) + 2;
                        value.rectTransform.Box(x, cy, vw, RecentCellH); x += vw + 6;
                    }
                    var quiet = !string.IsNullOrEmpty(i.Value) || i.Unrecorded || i.Tone == "quiet";
                    var name = Unrecorded(Label(box, i.Title, quiet ? 15 : 17, quiet ? PanelLook.Muted : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft,
                                                style: i.Tone == "quiet" ? FontStyles.Italic : FontStyles.Normal), i, 15);
                    name.overflowMode = TextOverflowModes.Ellipsis; name.maxVisibleLines = 2;   // a long name takes a second line, never "St..."
                    name.rectTransform.Box(x, cy, Mathf.Max(40f, right - x), RecentCellH);
                }
            }
        }
    }
}
