using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Company > Since you were away, drawn (AwayModel.cs). 0.8.5 (Joost: "love the design but tighten down the visual grammar like we did with
    /// the damage bar graphs; more dense, but still with visual hierarchy"; variant B of work/hearthwoven-0.8.5/proto/2-company-away): the header
    /// line and the group's totals as big numbers, each with its icon beside it and its colour line at its foot (awaytotals); then one damage
    /// row per fellow (awayrows, DamageRowsUi.cs): shield and name, their share of the totals before the bar in gold, the bar on the page's one
    /// scale with each total's count inside, the true-length line, each total's share as the entries under it, and "For you" at the row's
    /// right end: item pictures with x N. Five fellows fit the plate; a sixth scrolls. Laid out once per fill; nothing per frame.
    /// </summary>
    public partial class PanelUi
    {
        // the totals: a cell's icon, the number's line (the hero's second size, PanelLayout.Hero.Second), the label's line under it, the colour
        // line at the cell's foot (it ends AwayLineEnd short of the next cell), a cell's widest, the header line's height
        const float AwayIcon = 32, AwayIconGap = 10, AwayNumberH = 44, AwayLabelH = 18, AwayLinePx = 4, AwayLineGap = 5, AwayLineEnd = 16, AwayCellMax = 210, AwayHeadH = 28;
        // the rows: the name and share columns at their least (player names are short, so less than a foe list's), For you at its least, its
        // pictures and the gap between two of them
        const float AwayNameW = 138, AwayPctW = 56, AwayYouW = 120, AwayChip = 22, AwayChipGap = 4, AwayChipsGap = 12;
        static float AwayStatH => AwayNumberH + AwayLabelH + AwayLineGap + AwayLinePx;

        // 0.8 hover (HoverUi.cs): the totals' numbers of the page just drawn, by id, so a fellow's part lights its total's number above too
        static readonly Dictionary<string, TextMeshProUGUI> awayLit = new Dictionary<string, TextMeshProUGUI>();

        static void AwayTotals(RectTransform col, Block b, Func<string, Action> link)
        {
            awayLit.Clear();
            var head = Line(col, 10); Size(head, -1, AwayHeadH);
            if (!string.IsNullOrEmpty(b.Icon)) Marker(head, b.Icon, 22);
            OneLine(Label(head, b.Title, PanelLayout.Text.Lead, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft));
            if (headAbout != null)   // 0.8.5: About these numbers at the head line's right end, so the page spends no row on it (PanelModel.HeadAbout)
            {
                Node("Rest", head).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                AboutButton(head, headAbout, link);
            }
            if (!string.IsNullOrEmpty(b.Text)) Label(col, b.Text, PanelLayout.Text.Body, PanelLook.Muted);
            var totals = b.Items ?? new List<Block>();
            if (totals.Count == 0) return;
            float w = Mathf.Min(Mathf.Floor(Column / totals.Count), AwayCellMax), x = AwayIcon + AwayIconGap, room = w - x;   // the words may run to the next cell's icon
            var row = Node("Totals", col); Size(row, Column, AwayStatH);
            for (int i = 0; i < totals.Count; i++)
            {
                var t = totals[i]; var colour = Hex(t.Colour, PanelLook.Accent);
                var cell = Node("Total", row); cell.Box(i * w, 0, w, AwayStatH);
                var top = (AwayNumberH + AwayLabelH - AwayIcon) / 2;   // the icon beside the number and its label, on their middle
                if ((t.Icon ?? "").StartsWith("vocab:")) BtIcon(cell, t.Icon, colour, 0, top, AwayIcon);   // a line icon in the total's colour
                else Pic(cell, t.Icon, AwayIcon).Box(0, top, AwayIcon, AwayIcon);
                var number = Label(cell, t.Value, PanelLayout.Hero.Second, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft);
                number.textWrappingMode = TextWrappingModes.NoWrap; number.rectTransform.Box(x, 0, room, AwayNumberH);
                var need = Mathf.Ceil(number.preferredWidth);   // a long number (12 500 stone) takes a smaller size, the stat's at least, never the next cell
                if (need > room) number.fontSize = Mathf.Max(PanelLayout.Text.Stat, PanelLayout.Hero.Second * room / need);
                OneLine(Label(cell, t.Title, PanelLayout.Text.Small, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft)).rectTransform.Box(x, AwayNumberH, room, AwayLabelH);
                Fill(cell, "Underline", colour).rectTransform.Box(0, AwayStatH - AwayLinePx, w - AwayLineEnd, AwayLinePx);   // the same colour as this total's part in every fellow's bar
                if (!string.IsNullOrEmpty(t.Id)) awayLit[t.Id] = number;
            }
        }

        static void AwayRows(RectTransform col, Block b)
        {
            var rows = b.Items ?? new List<Block>();
            if (rows.Count == 0) return;
            List<Block> Chips(Block r) => (r.Items ?? new List<Block>()).Where(p => p.Kind == "chip").ToList();
            // the columns, measured once for the page: name, share, track, For you (only when a row has something for you)
            float whoW = NameColumn(col, rows, AwayNameW), numW = NumberColumn(col, rows.Select(r => r.Kind == PanelModel.AwayFellowKind ? r.Value : null), AwayPctW);
            var youW = rows.Any(r => Chips(r).Count > 0) ? Mathf.Max(AwayYouW, rows.Max(r => ChipsWidth(col, Chips(r)))) : 0;
            float trackX = whoW + DrCol + numW + DrCol, trackW = Mathf.Max(80f, Column - trackX - (youW > 0 ? DrCol + youW : 0));
            // the column heads, small capitals over the track and over For you, a rule under them
            if (!string.IsNullOrEmpty(b.Title) || !string.IsNullOrEmpty(b.Text))
            {
                var cap = Node("Caption", col); Size(cap, Column, PanelLayout.Bar.HeadLine + 1);
                if (!string.IsNullOrEmpty(b.Title))
                {
                    var c = Label(cap, b.Title, PanelLayout.Text.Floor, PanelLook.Muted, style: FontStyles.UpperCase, align: TextAlignmentOptions.MidlineLeft);
                    c.characterSpacing = 6; c.textWrappingMode = TextWrappingModes.NoWrap; c.rectTransform.Box(trackX, 0, trackW, PanelLayout.Bar.HeadLine);
                }
                if (!string.IsNullOrEmpty(b.Text) && youW > 0)
                {
                    var fy = Label(cap, b.Text, PanelLayout.Text.Floor, PanelLook.Muted, style: FontStyles.UpperCase, align: TextAlignmentOptions.MidlineRight);
                    fy.characterSpacing = 6; fy.textWrappingMode = TextWrappingModes.NoWrap; fy.rectTransform.Box(Column - youW, 0, youW, PanelLayout.Bar.HeadLine);
                }
                Img(cap, "Rule", null, PanelLook.Rule).rectTransform.Box(0, PanelLayout.Bar.HeadLine, Column, 1);
            }
            var list = VStack(col, PanelModel.DamageFoeGap);
            foreach (var r in rows)
            {
                var row = DamageRow(list, r, true, whoW, numW, trackX, trackW);
                // 0.8 hover: a part also lights its total's number above (the entry under the bar is lit by the damage row itself); no object for it
                var hover = row.GetComponent<BarHover>();
                if (hover != null)
                {
                    var parts = (r.Items ?? new List<Block>()).Where(p => p.Kind == PanelModel.AwayPartKind && p.Fraction > 0).ToList();
                    for (int k = 0; k < parts.Count && k < hover.Parts.Length; k++) if (parts[k].Id != null && awayLit.TryGetValue(parts[k].Id, out var lit)) hover.Numbers[k] = lit;
                }
                // For you: your food and gear they enjoyed or put to good use, on the bar's line, right-aligned in its column
                float x = Column;
                var chips = Chips(r);
                for (int k = chips.Count - 1; k >= 0; k--)
                {
                    var c = chips[k];
                    var n = Label(row, c.Value, PanelLayout.Text.Name, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft); n.textWrappingMode = TextWrappingModes.NoWrap;
                    var nw = Mathf.Ceil(n.preferredWidth) + 2; x -= nw; n.rectTransform.Box(x, 0, nw, PanelModel.DamageTrack);
                    x -= AwayChipGap + AwayChip;
                    var top = (PanelModel.DamageTrack - AwayChip) / 2;
                    if ((c.Icon ?? "").StartsWith("vocab:")) BtIcon(row, c.Icon, PanelLook.Gold, x, top, AwayChip);   // several kinds: the food-shared or gear-shared mark
                    else Pic(row, c.Icon, AwayChip).Box(x, top, AwayChip, AwayChip);
                    x -= AwayChipsGap;
                }
            }
        }

        // For you's room for one row's pictures with x N, as AwayRows lays them out
        static float ChipsWidth(RectTransform parent, List<Block> chips)
        {
            if (chips.Count == 0) return 0;
            var measure = Label(parent, "", PanelLayout.Text.Name, PanelLook.Text, style: FontStyles.Bold); measure.textWrappingMode = TextWrappingModes.NoWrap;
            float w = (chips.Count - 1) * AwayChipsGap;
            foreach (var c in chips) { measure.text = c.Value ?? ""; w += AwayChip + AwayChipGap + Mathf.Ceil(measure.preferredWidth) + 2; }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
            return w;
        }
    }
}
