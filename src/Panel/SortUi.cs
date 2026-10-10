using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The sort control (SortModel.cs): one quiet line over the list, the caption "Sort" and the pills Most, A-Z and, where the page
    /// has a grouping, By category, in the look of the view switch's and the window chips (Chip). A pill's click picks that order
    /// (a view link, PanelModel.Follow); the chosen one is filled amber, and a click on it flips it (Most and Least, A-Z and Z-A;
    /// PanelModel.SortFlip). A right click steps back, as on the other chips. No key or pad button reaches the sort control.
    /// </summary>
    public partial class PanelUi
    {
        static void SortRow(RectTransform col, Block b, Func<string, Action> link)
        {
            var orders = b.Items ?? new List<Block>();
            if (orders.Count == 0) return;
            var row = Line(col, 6); Size(row, -1, 28);
            var caption = Label(row, b.Title ?? PanelModel.SortCaption, 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            Size(caption, Mathf.Ceil(caption.preferredWidth) + 4, 28);
            var chosen = Math.Max(0, orders.FindIndex(x => x.Selected));
            foreach (var o in orders)
            {
                var back = orders[((chosen - 1) % orders.Count + orders.Count) % orders.Count];
                Chip(row, o.Title, null, o.Selected, link?.Invoke(PanelModel.SortLink(b, o)), back: link?.Invoke(PanelModel.ViewLink(b, back)));   // the chosen one flips
            }
        }
    }
}
