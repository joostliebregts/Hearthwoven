using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Company > Since you were away, drawn (AwayModel.cs; variant C of work/hearthwoven-0.7/away-prototypes): the header line and the
    /// group's totals as big numbers (awaytotals), then one slim row per fellow (awayrows): shield and name, their share of the totals as a
    /// bar on one shared scale (the one bar form's rounded parts, each total in its fixed colour), the per cent, and "For you": item
    /// pictures with x N. Laid out once per fill; nothing per frame.
    /// </summary>
    public partial class PanelUi
    {
        const float AwayIconPx = 44, AwayNumber = 50, AwayLabelPx = 16, AwayRowH = 64, AwayNameW = 176, AwayPctW = 74, AwayYouW = 190, AwayChip = 26;

        // 0.8 hover (HoverUi.cs): the totals of the page just drawn, by id, so a fellow's part lights its total (the away rows have no list of their own)
        static readonly Dictionary<string, (TextMeshProUGUI number, TextMeshProUGUI title, Image band)> awayLit = new Dictionary<string, (TextMeshProUGUI, TextMeshProUGUI, Image)>();

        static void AwayTotals(RectTransform col, Block b)
        {
            awayLit.Clear();
            var head = Line(col, 10); Size(head, -1, 30);
            if (!string.IsNullOrEmpty(b.Icon)) Marker(head, b.Icon, 22);
            OneLine(Label(head, b.Title, 20, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft));
            if (!string.IsNullOrEmpty(b.Text)) Label(col, b.Text, 16, PanelLook.Muted);
            var totals = b.Items ?? new List<Block>();
            if (totals.Count == 0) return;
            Spacer(col, 6);
            var w = Mathf.Min(Mathf.Floor(Column / totals.Count), 210f);
            var row = Line(col, 0, TextAnchor.UpperLeft);
            foreach (var t in totals)
            {
                var cell = VStack(row, 4); Size(cell, w, -1);
                cell.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
                var colour = Hex(t.Colour, PanelLook.Accent);
                if ((t.Icon ?? "").StartsWith("vocab:")) Size(VocabImg(cell, "Mark", VocabName(t.Icon), colour), AwayIconPx, AwayIconPx);   // a line icon in the total's colour
                else Pic(cell, t.Icon, AwayIconPx);
                var band = RoundedPart(cell, "Lit", Color.clear); band.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                band.rectTransform.Stretch(); band.rectTransform.offsetMin = new Vector2(8, -6); band.rectTransform.offsetMax = new Vector2(-8, 4);
                var number = OneLine(Label(cell, t.Value, AwayNumber, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.Center));
                var title = OneLine(Label(cell, t.Title, AwayLabelPx, PanelLook.Muted, align: TextAlignmentOptions.Center));
                if (!string.IsNullOrEmpty(t.Id)) awayLit[t.Id] = (number, title, band);
                Size(Fill(cell, "Underline", colour), 64, 4);   // the same colour as this total's part in every fellow's bar
            }
        }

        static void AwayRows(RectTransform col, Block b)
        {
            var rows = b.Items ?? new List<Block>();
            if (rows.Count == 0) return;
            float barX = AwayNameW, barW = Column - AwayNameW - AwayPctW - AwayYouW, pctX = barX + barW, youX = pctX + AwayPctW;
            Spacer(col, 14);
            if (!string.IsNullOrEmpty(b.Title) || !string.IsNullOrEmpty(b.Text))
            {
                var cap = Node("Caption", col); Size(cap, Column, 22);
                if (!string.IsNullOrEmpty(b.Title))
                {
                    var c = Label(cap, b.Title, 14, PanelLook.Muted, style: FontStyles.UpperCase, align: TextAlignmentOptions.MidlineLeft);
                    c.characterSpacing = 6; c.textWrappingMode = TextWrappingModes.NoWrap; c.rectTransform.Box(barX, 0, barW + AwayPctW, 20);
                }
                if (!string.IsNullOrEmpty(b.Text))
                {
                    var fy = Label(cap, b.Text, 14, PanelLook.Muted, style: FontStyles.UpperCase, align: TextAlignmentOptions.MidlineRight);
                    fy.characterSpacing = 6; fy.textWrappingMode = TextWrappingModes.NoWrap; fy.rectTransform.Box(youX, 0, AwayYouW, 20);
                }
                Size(Img(col, "Rule", null, PanelLook.Rule), -1, 1);
            }
            foreach (var r in rows)
            {
                var box = Node("Fellow", col); Size(box, Column, AwayRowH);
                var shield = Marker(box, r.Icon, 36, layout: false); shield.Box(0, (AwayRowH - 36) / 2, 36, 36);
                var hasNote = !string.IsNullOrEmpty(r.Note);
                var name = OneLine(Label(box, r.Title, 21, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft));
                name.rectTransform.Box(46, hasNote ? 8 : 16, AwayNameW - 52, 30);
                if (hasNote) OneLine(Label(box, r.Note, 14, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft)).rectTransform.Box(46, 38, AwayNameW - 52, 20);   // their copy is older than your last session
                var parts = (r.Items ?? new List<Block>()).Where(p => p.Kind != "chip" && p.Fraction > 0).ToList();
                if (r.Kind == PanelModel.AwayFellowKind && parts.Count > 0)
                {
                    // one shared scale: the whole track is the group, so this fellow's parts end where their share ends
                    // the one bar form's shape (BAR-FORM.md): rounded parts 4 px apart, no track; one shared scale, so this fellow's parts end
                    // where their share of the group ends; each total keeps its fixed colour (the line under its big number)
                    float tw = barW - 18, x = 0, min = (6f + PanelModel.BarFormGap) / tw;
                    var bar = Node("BarForm", box); bar.Box(barX, (AwayRowH - PanelModel.BarFormBar) / 2, tw, PanelModel.BarFormBar);
                    // 0.8 hover: a part rings and lights its total above (the colour line under the same number), as a bar part lights its list row
                    var hover = box.gameObject.AddComponent<BarHover>(); hover.Init(parts.Count);
                    for (int k = 0; k < parts.Count; k++)
                    {
                        var from = x; var to = Mathf.Min(1f, x + Mathf.Max(parts[k].Fraction, min)); x = to;
                        var seg = RoundedPart(bar, "Part", Hex(parts[k].Colour, PanelLook.Accent), raycast: true).rectTransform;
                        seg.anchorMin = new Vector2(from, 0); seg.anchorMax = new Vector2(to, 1); seg.pivot = new Vector2(0, 0.5f);
                        seg.offsetMin = new Vector2(PanelModel.BarFormGap / 2, 0); seg.offsetMax = new Vector2(-PanelModel.BarFormGap / 2, 0);
                        hover.Parts[k] = seg; var hp = seg.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = k;
                        if (parts[k].Id != null && awayLit.TryGetValue(parts[k].Id, out var lit)) { hover.Numbers[k] = lit.number; hover.Names[k] = lit.title; hover.NameColours[k] = PanelLook.Muted; hover.Bands[k] = lit.band; }
                    }
                    var ring = VocabImg(bar, "Hover", "focus-ring", Color.clear); ring.raycastTarget = false; hover.RingImage = ring;
                    var pct = OneLine(Label(box, r.Value, 24, PanelLook.Gold, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineRight));
                    pct.rectTransform.Box(pctX, (AwayRowH - 32) / 2, AwayPctW - 6, 32);
                }
                else OneLine(Label(box, r.Text, 16, PanelLook.Muted, style: FontStyles.Italic, align: TextAlignmentOptions.MidlineLeft)).rectTransform.Box(barX, (AwayRowH - 24) / 2, barW + AwayPctW - 18, 24);
                // For you: your items they enjoyed or put to good use, right-aligned
                var chips = (r.Items ?? new List<Block>()).Where(p => p.Kind == "chip").ToList();
                if (chips.Count > 0)
                {
                    var you = Node("ForYou", box); you.Box(youX, (AwayRowH - 32) / 2, AwayYouW, 32);
                    var line = you.gameObject.AddComponent<HorizontalLayoutGroup>(); Layout(line, 14, TextAnchor.MiddleRight);
                    foreach (var c in chips)
                    {
                        var chip = Line(you, 5);
                        if ((c.Icon ?? "").StartsWith("vocab:")) Size(VocabImg(chip, "Mark", VocabName(c.Icon), PanelLook.Gold), AwayChip, AwayChip);   // several kinds: the food-shared or gear-shared mark
                        else Pic(chip, c.Icon, AwayChip);
                        Keeps(Label(chip, c.Value, 19, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft));
                    }
                }
                Size(Img(col, "Rule", null, new Color(PanelLook.Rule.r, PanelLook.Rule.g, PanelLook.Rule.b, PanelLook.Rule.a * 0.5f)), -1, 1);
            }
        }
    }
}
