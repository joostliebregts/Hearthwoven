using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Compare periods drawn (0.8, Chapters/CompareModel.cs; PICKS.md section 3, pick B; prototype work/hearthwoven-0.8/prototypes/compare):
    /// the Compare chip (greyed with a reason tag on hover, drawn above the page, no layout change), the hero's number before beside each
    /// number with its change mark, a cap's change after its total, and the compare mode of the bar form: one row per part on one scale, this
    /// period solid in the part's own colour or wood grain, the period before as a thin outline at its length on the same track, then now ·
    /// before · change. The change mark's arrow is a picture (PanelLook.Triangle): the game's fonts may not hold ▲. Built once per page; the
    /// tag only switches on and off while the pointer is on the chip. Mirrored by compareRows() and friends in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        static readonly Color BeforeInk = new Color(0.541f, 0.486f, 0.4f);              // #8a7c66: the period before, faded (PICKS B)
        static readonly Color OutlineInk = new Color(0.914f, 0.863f, 0.769f, 0.75f);     // the period before's outline: cream at 75 %
        static readonly Color TagGround = new Color(0.035f, 0.027f, 0.02f, 0.98f), TagEdge = new Color(0.541f, 0.416f, 0.243f);
        const float CmpRow = 34, CmpHead = 22, CmpTrack = 18, CmpTagWidth = 360;

        /// <summary>The Compare chip: the window chips' look (accent fill when on); greyed, it is inert but shows its reason as a tag under it while
        /// the pointer is on it (or always, when tip). Returns its width.</summary>
        static float CompareChip(RectTransform row, string label, bool on, bool off, string why, bool tip, Action click)
        {
            var w = Chip(row, label, null, on, off ? null : click, off: off, pad: WindowPad);
            var chip = (RectTransform)row.GetChild(row.childCount - 1);
            if (off && !string.IsNullOrEmpty(why))
            {
                chip.GetComponent<Image>().raycastTarget = true;   // inert, but the pointer finds it
                var tag = ReasonTag(chip, why);
                tag.gameObject.SetActive(tip);
                chip.gameObject.AddComponent<SwapOnHover>().On = tag.gameObject;
            }
            return w;
        }

        // the reason under a greyed chip, right-aligned to it, over whatever lies below (its own sorting, its own alpha: the chip is at 45 %);
        // out of any layout group its parent has (the hero's growth line is a Line: shown, the group took the tag and drew bare text, 0.8.1 review 2)
        static RectTransform ReasonTag(RectTransform chip, string why)
        {
            var tag = Node("Tag", chip);
            tag.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tag.anchorMin = tag.anchorMax = new Vector2(1, 0); tag.pivot = new Vector2(1, 1); tag.anchoredPosition = new Vector2(0, -6);
            tag.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            var root = chip.GetComponentInParent<Canvas>();
            var canvas = tag.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true;
            if (root) { canvas.sortingLayerID = root.rootCanvas.sortingLayerID; canvas.sortingOrder = root.rootCanvas.sortingOrder + 5; }
            Img(tag, "Ground", null, TagGround).rectTransform.Stretch();
            Edge(tag, TagEdge);
            var t = Label(tag, why, 14, PanelLook.Text);
            t.textWrappingMode = TextWrappingModes.Normal;
            var width = Mathf.Min(CmpTagWidth, Mathf.Ceil(t.GetPreferredValues(why, 10000, 0).x) + 2);
            var height = Mathf.Ceil(t.GetPreferredValues(why, width, 0).y);
            t.rectTransform.Box(13, 8, width, height);
            tag.sizeDelta = new Vector2(width + 26, height + 17);
            return tag;
        }

        /// <summary>A change mark: the arrow as a picture (turned for down) and its words ("12 %", "new"), in one colour.</summary>
        static RectTransform ChangeMarkUi(RectTransform parent, string mark, float size, Color c, FontStyles style = FontStyles.Normal)
        {
            var (dir, words) = PanelModel.ChangeParts(mark);
            var line = Line(parent, Mathf.Round(size * 0.3f), TextAnchor.MiddleLeft);
            if (dir != 0)
            {
                var a = Img(line, "Arrow", PanelLook.Triangle, c); a.preserveAspect = true;
                Size(a, Mathf.Round(size * 0.62f), Mathf.Round(size * 0.54f));
                if (dir < 0) a.rectTransform.localEulerAngles = new Vector3(0, 0, 180);
            }
            Label(line, words, size, c, style: style).textWrappingMode = TextWrappingModes.NoWrap;
            return line;
        }

        /// <summary>The hero's number before: the change mark over the number before (faded) and what it is ("the 7 days before"), 22 px after the
        /// number's words (PICKS B: 20 px mark, 34 px number; a second or compact number smaller).</summary>
        static void HeroBefore(RectTransform group, Block n, float size)
        {
            var lead = size >= HeroSize; var second = !lead && size >= HeroSecond;   // the hero's own number, or a number beside it (0.8 layout D+: 67 and 43 px)
            float mark = lead ? 20 : 15, num = lead ? 34 : second ? 26 : 20, words = lead || second ? 15 : 14;
            var stack = VStack(group, 4);   // 14 px after the words (the group's spacing) and 8 more: 22 px
            var v = stack.GetComponent<VerticalLayoutGroup>(); v.childForceExpandWidth = false; v.childAlignment = TextAnchor.LowerLeft;
            v.padding = new RectOffset(8, 0, 0, Mathf.RoundToInt((size - num) * 0.12f));
            ChangeMarkUi(stack, n.Change, mark, PanelLook.Text, FontStyles.Bold);
            var line = Line(stack, 8, TextAnchor.LowerLeft);
            Label(line, n.Before, num, BeforeInk, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(n.BeforeLabel)) Label(line, n.BeforeLabel, words, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>The number before as a line of its own under a hero whose words fill the row (Together): mark, number before, what it is.</summary>
        static void HeroBeforeLine(RectTransform col, Block n)
        {
            var line = Line(col, 10, TextAnchor.LowerLeft);
            ChangeMarkUi(line, n.Change, 20, PanelLook.Text, FontStyles.Bold);
            Label(line, n.Before, 34, BeforeInk, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(n.BeforeLabel)) Label(line, n.BeforeLabel, 15, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>A cap's comparison after its total, in the qualifier's quiet type: "▼ 30 %, 910 the 7 days before".</summary>
        static void CompareQualifier(RectTransform row, Block b)
        {
            if (b?.Before == null) return;
            var q = Line(row, 0, TextAnchor.MiddleLeft);
            ChangeMarkUi(q, b.Change, 14, PanelLook.Faint);
            Label(q, ", " + b.Before + (string.IsNullOrEmpty(b.BeforeLabel) ? "" : " " + b.BeforeLabel), 14, PanelLook.Faint).textWrappingMode = TextWrappingModes.NoWrap;
        }

        // a row's picture: a damage type's icon in its colour (on its swatch, in dark ink, for a cause that is no creature), a person's shield, a
        // game or vocabulary picture; none: the part's swatch (its grain for wood)
        static void CompareRowPicture(RectTransform row, Block r, float x, float top)
        {
            var colour = Hex(r.Colour, PanelLook.Accent);
            if (!string.IsNullOrEmpty(r.Icon) && r.Icon.StartsWith("vocab:dmg-", StringComparison.Ordinal))
            {
                if (r.Tone == "type") { RoundedPart(row, "Swatch", colour).rectTransform.Box(x, top, 20, 20); BtIcon(row, r.Icon, DarkInk, x + 2, top + 2, 16); }
                else BtIcon(row, r.Icon, colour, x, top, 20);
                return;
            }
            if (!string.IsNullOrEmpty(r.Icon) && r.Icon.StartsWith("vocab:weapon-", StringComparison.Ordinal)) { BtIcon(row, r.Icon, PanelLook.Gold, x, top, 20); return; }   // a weapon's mask in gold, as Battle draws it
            if (!string.IsNullOrEmpty(r.Icon)) { Marker(row, r.Icon, 20, layout: false).Box(x, top, 20, 20); return; }
            var sw = RoundedPart(row, "Swatch", colour); sw.rectTransform.Box(x + 4, top + 4, 12, 12);
            if (PartGrain(r) is string g) Grain(sw, g, r, 0.5f);
        }

        static string PartGrain(Block part) => part == null || string.IsNullOrEmpty(part.Pattern) ? null : VocabName(part.Pattern) is string g && g.StartsWith("grain-") && PanelLook.Vocab(g) ? g : null;

        /// <summary>
        /// The bar form in compare mode (kind "comparerows"): the cap as a composition's (title, total, its change, its qualifier), a header row
        /// (the block's own words on the left, "now" and "before" over their columns, a rule under it), then one 34 px row per part: picture and
        /// name, the track with this period solid (radius 4, inset 2 px) and the period before as an outline at its length, both on the block's
        /// one scale (Fraction, Fraction2), now 17 bold, before 15 faded, the change 15 muted. Columns 150 / track / 64 / 64 / 82, 14 apart,
        /// each wider when its words need it (a long name up to 200, "1 hour 1 min"); in a half-width column 110 / track / 54 / 54 / 72, 10 apart.
        /// </summary>
        static void CompareRows(RectTransform col, Block b)
        {
            var rows = (b.Items ?? new List<Block>()).Where(r => r.Kind == "row").ToList();
            if (rows.Count == 0) return;
            if (!string.IsNullOrEmpty(b.Title) || !string.IsNullOrEmpty(b.Value))
            {
                var head = Line(col, 8);
                if (plated) Sect(head, b.Title, b.Value);
                else { if (!string.IsNullOrEmpty(b.Title)) Label(head, b.Title, 18, PanelLook.Gold); if (!string.IsNullOrEmpty(b.Value)) Label(head, b.Value, 18, PanelLook.Text, style: FontStyles.Bold); }
                CompareQualifier(head, b);
                if (!string.IsNullOrEmpty(b.Note)) Label(head, b.Note, 14, PanelLook.Faint).textWrappingMode = TextWrappingModes.NoWrap;
            }
            var narrow = Column < 600;
            float gap = narrow ? 10 : 14, nameMin = narrow ? 110 : 150, nameMax = narrow ? 140 : 200;
            var box = Node("CompareRows", col);
            Size(box, Column, CmpHead + 6 + rows.Count * CmpRow);
            // every label first, then the columns from what they need: the names 150 px (so a page's tracks line up), wider for a long name up
            // to 200; now, before and the change at least 64 / 64 / 82 (54 / 54 / 72 in a half column), wider for "1 hour 1 min"
            var names = rows.Select(r => { var t = Label(box, r.Title ?? "", 17, r.Id == PanelModel.FoldId ? PanelLook.Faint : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis; return t; }).ToList();
            var nows = rows.Select(r => { var t = Label(box, r.Value ?? "", 17, PanelLook.Text, align: TextAlignmentOptions.MidlineRight, style: FontStyles.Bold); t.textWrappingMode = TextWrappingModes.NoWrap; return t; }).ToList();
            var befores = rows.Select(r => { var t = Label(box, r.Before ?? "", 15, BeforeInk, align: TextAlignmentOptions.MidlineRight); t.textWrappingMode = TextWrappingModes.NoWrap; return t; }).ToList();
            var marks = rows.Select(r => string.IsNullOrEmpty(r.Change) ? null : ChangeMarkUi(box, r.Change, 15, PanelLook.Muted)).ToList();
            float MarkW(RectTransform m) { if (!m) return 0; LayoutRebuilder.ForceRebuildLayoutImmediate(m); return Mathf.Ceil(LayoutUtility.GetPreferredWidth(m)); }
            var nameW = Mathf.Min(nameMax, Mathf.Max(nameMin, 30 + names.Max(t => Mathf.Ceil(t.preferredWidth))));
            var nowW = Mathf.Max(narrow ? 54 : 64, nows.Max(t => Mathf.Ceil(t.preferredWidth)));
            var beforeW = Mathf.Max(narrow ? 54 : 64, befores.Max(t => Mathf.Ceil(t.preferredWidth)));
            var changeW = Mathf.Max(narrow ? 72 : 82, marks.Max(MarkW));
            var trackX = nameW + gap; var trackW = Mathf.Max(40, Column - nameW - nowW - beforeW - changeW - 4 * gap);
            float nowX = trackX + trackW + gap, beforeX = nowX + nowW + gap, changeX = beforeX + beforeW + gap;
            // the header row
            if (!string.IsNullOrEmpty(b.Text)) { var h = Label(box, b.Text, 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); h.characterSpacing = 6; h.textWrappingMode = TextWrappingModes.NoWrap; h.rectTransform.Box(0, 0, trackX + trackW, CmpHead); }
            var hn = Label(box, PanelModel.CompareNowHead, 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineRight); hn.rectTransform.Box(nowX, 0, nowW, CmpHead);
            var hb = Label(box, PanelModel.CompareBeforeHead, 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineRight); hb.rectTransform.Box(beforeX, 0, beforeW, CmpHead);
            Img(box, "Rule", null, PanelLook.Rule).rectTransform.Box(0, CmpHead, Column, 1);
            for (int k = 0; k < rows.Count; k++)
            {
                var r = rows[k]; var top = CmpHead + 6 + k * CmpRow; var mid = top + CmpRow / 2;
                CompareRowPicture(box, r, 0, mid - 10);
                names[k].rectTransform.Box(30, top, nameW - 30, CmpRow);
                var colour = Hex(r.Colour, PanelLook.Accent);
                if (r.Fraction > 0)
                {
                    var solid = RoundedPart(box, "Now", colour); solid.rectTransform.Box(trackX, mid - CmpTrack / 2 + 2, Mathf.Max(4, r.Fraction * trackW), CmpTrack - 4);
                    if (PartGrain(r) is string g) Grain(solid, g, r);
                }
                if (r.Fraction2 > 0 && PanelLook.RoundedEdge)
                {
                    var o = Img(box, "Before", PanelLook.RoundedEdge, OutlineInk); o.preserveAspect = false; o.type = Image.Type.Sliced; o.pixelsPerUnitMultiplier = 1f;
                    o.rectTransform.Box(trackX, mid - CmpTrack / 2 - 1, Mathf.Max(8, r.Fraction2 * trackW), CmpTrack + 2);
                }
                nows[k].rectTransform.Box(nowX, top, nowW, CmpRow);
                befores[k].rectTransform.Box(beforeX, top, beforeW, CmpRow);
                if (marks[k]) marks[k].Box(changeX, top, changeW, CmpRow);
            }
            // no air after: it sits in an island, whose padding is the air (0.8 layout D+, PanelModel.BarFormAfter)
        }
    }
}
