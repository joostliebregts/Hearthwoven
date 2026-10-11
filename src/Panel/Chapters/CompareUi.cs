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
    /// 0.8.5 (proto/4-compare, A and C): the heading's chip carries a small down arrow and opens its menu under it (CompareMenuUi: what to compare
    /// against, each option with its dates, Off under a rule; on a canvas of its own over the page, its rows take the pointer); a greyed chip in a
    /// day window says its date and its reason is one line in the strip (StripCompare), as the legend is while comparing: "solid bar: today",
    /// "thin outline: yesterday", each with a swatch drawn as the bars are. Mirrored by compareMenu() and stripCompare() in the preview.
    /// </summary>
    public partial class PanelUi
    {
        static readonly Color BeforeInk = new Color(0.541f, 0.486f, 0.4f);              // #8a7c66: the period before, faded (PICKS B)
        // the period before's outline: a 1 px trace in the row's own colour at 55 % (0.8.1, Joost: the cream 2 px outline led over the bar now)
        const float TraceAlpha = 0.55f;
        static readonly Color TagGround = new Color(0.035f, 0.027f, 0.02f, 0.98f), TagEdge = new Color(0.541f, 0.416f, 0.243f);
        const float CmpRow = 34, CmpHead = 22, CmpTrack = 18, CmpTagWidth = 360;
        // 0.8.5: the chip's arrow (the chevron turned down, 6 x 10 before the turn) and the room it adds; the menu's measures (proto C: 330 wide, here
        // 360 so "The same 7 days last month" fits beside "opens 29 Oct"; 36 px rows, a 26 px caption, 10 px inside), the legend's swatches (26 x 10
        // solid, 26 x 12 outline)
        const float CmpArrowW = 6, CmpArrowH = 10, CmpArrowRoom = 14, OpenTurn = 90, Half = 0.5f;
        const float MenuW = 360, MenuRow = 36, MenuCap = 26, MenuPad = 10, MenuDrop = 8, MenuCapIn = 4, MenuCapLow = 6, MenuRuleAt = 5, MenuRuleRoom = 11;
        const float MenuMark = 12, MenuMarkX = 8, MenuWordsX = 30, MenuGap = 8, MenuDatesMax = 140, MenuAbove = 6;
        // the catcher's side, centred on the menu: wider than any screen at any Panel.Scale, so every click outside the menu lands on it (and its
        // hover line, at its edges, stays off the screen)
        const float MenuOutside = 20000;
        const float KeySwatch = 26, KeySolidH = 10, KeyTraceH = 12, KeyGap = 8, KeyWordsGap = 5, KeyBetween = 18, KeyTagGap = 12, KeyTagSpacing = 10;
        /// <summary>The room a measured text gets past its own width (TMP cuts a run drawn in exactly its width).</summary>
        const float Slack = 2;
        // 0.8.5: in the strip, what follows Compare's part: the rule between (as between the feat and the titles), the feat's note's gap
        const float CmpSep = EarnedGap * 2 + 1, CmpRuleInset = 7, CmpNoteGap = 10;

        /// <summary>The Compare chip: the window chips' look (accent fill when on); greyed, it is inert but shows its reason as a tag under it while
        /// the pointer is on it (or always, when tip). 0.8.5: with <paramref name="menu"/> it ends in a small down arrow (it opens a menu), and the
        /// menu hangs under it while open (<paramref name="open"/>, CompareMenuUi). Returns its width.</summary>
        static float CompareChip(RectTransform row, string label, bool on, bool off, string why, bool tip, Action click, bool menu = false, Block open = null, Func<string, Action> link = null)
        {
            var w = Chip(row, label, null, on, off ? null : click, off: off, pad: WindowPad);
            var chip = (RectTransform)row.GetChild(row.childCount - 1);
            if (menu && !off)
            {
                var arrow = Img(chip, "Arrow", PanelLook.Chevron, on ? DarkInk : PanelLook.Text).rectTransform;
                arrow.anchorMin = arrow.anchorMax = new Vector2(1, Half); arrow.pivot = new Vector2(Half, Half);
                arrow.sizeDelta = new Vector2(CmpArrowW, CmpArrowH); arrow.localEulerAngles = new Vector3(0, 0, -OpenTurn);   // the opens mark's chevron, pointing down
                arrow.anchoredPosition = new Vector2(-WindowPad - CmpArrowH / 2, 0);
                w += CmpArrowRoom; Size(chip, w, -1);   // its label stays where it was (drawn from the left)
            }
            if (open != null) CompareMenuUi(chip, open, link);
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
            var t = Label(tag, why, PanelLayout.Text.Floor, PanelLook.Text);
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

        /// <summary>The hero's number before as one line 22 px after the number's words, on their baseline (Joost, 0.8.1: "new" sat over "0
        /// yesterday" like a label): "▲ 12 % from 86 yesterday", "0 yesterday · new" (PanelModel.HeroCompare), in the text colour, the mark bold;
        /// 20 px beside the hero's own number, 16 beside a second one.</summary>
        static void HeroBefore(RectTransform group, Block n, float size)
        {
            var lead = size >= HeroSize; var second = !lead && size >= HeroSecond;   // the hero's own number, or a number beside it (0.8 layout D+: 67 and 43 px)
            float s = lead ? 20 : second ? 16 : 14;
            var line = CompareLineUi(group, n, s);   // 14 px after the words (the group's spacing) and 8 more: 22 px
            line.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(8, 0, 0, Mathf.RoundToInt((size - s) * 0.22f));
        }

        /// <summary>The number before as a line of its own under a hero whose words fill the row (Together), worded as HeroBefore.</summary>
        static void HeroBeforeLine(RectTransform col, Block n) => CompareLineUi(col, n, 20);

        // the comparison's one line: the mark first for a change, after the number before for "new" and "same"
        static RectTransform CompareLineUi(RectTransform parent, Block n, float s)
        {
            var (markFirst, words) = PanelModel.HeroCompare(n);
            var line = Line(parent, Mathf.Round(s * 0.3f), TextAnchor.LowerLeft);
            if (markFirst) ChangeMarkUi(line, n.Change, s, PanelLook.Text, FontStyles.Bold);
            Label(line, words, s, PanelLook.Text).textWrappingMode = TextWrappingModes.NoWrap;
            if (!markFirst) ChangeMarkUi(line, n.Change, s, PanelLook.Text, FontStyles.Bold);
            return line;
        }

        /// <summary>A cap's comparison after its total, in the qualifier's quiet type: "▼ 30 %, 910 the 7 days before".</summary>
        static void CompareQualifier(RectTransform row, Block b)
        {
            if (b?.Before == null) return;
            var q = Line(row, 0, TextAnchor.MiddleLeft);
            ChangeMarkUi(q, b.Change, 14, PanelLook.Faint);
            Label(q, ", " + b.Before + (string.IsNullOrEmpty(b.BeforeLabel) ? "" : " " + b.BeforeLabel), PanelLayout.Text.Floor, PanelLook.Faint).textWrappingMode = TextWrappingModes.NoWrap;
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
        /// (the block's own words on the left, the two periods named over their columns, a rule under it), then one 34 px row per part: picture and
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
                else { if (!string.IsNullOrEmpty(b.Title)) Label(head, b.Title, PanelLayout.Text.Row, PanelLook.Gold); if (!string.IsNullOrEmpty(b.Value)) Label(head, b.Value, PanelLayout.Text.Row, PanelLook.Text, style: FontStyles.Bold); }
                CompareQualifier(head, b);
                if (!string.IsNullOrEmpty(b.Note)) Label(head, b.Note, PanelLayout.Text.Floor, PanelLook.Faint).textWrappingMode = TextWrappingModes.NoWrap;
            }
            var narrow = Column < 600;
            float gap = narrow ? 10 : 14, nameMin = narrow ? 110 : 150, nameMax = narrow ? 140 : 200;
            var box = Node("CompareRows", col);
            Size(box, Column, CmpHead + 6 + rows.Count * CmpRow);
            // every label first, then the columns from what they need: the names 150 px (so a page's tracks line up), wider for a long name up
            // to 200; now, before and the change at least 64 / 64 / 82 (54 / 54 / 72 in a half column), wider for "1 hour 1 min"
            var names = rows.Select(r => { var t = Label(box, r.Title ?? "", PanelLayout.Text.Name, r.Id == PanelModel.FoldId ? PanelLook.Faint : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis; return t; }).ToList();
            var nows = rows.Select(r => { var t = Label(box, r.Value ?? "", PanelLayout.Text.Name, PanelLook.Text, align: TextAlignmentOptions.MidlineRight, style: FontStyles.Bold); t.textWrappingMode = TextWrappingModes.NoWrap; return t; }).ToList();
            var befores = rows.Select(r => { var t = Label(box, r.Before ?? "", PanelLayout.Text.Small, BeforeInk, align: TextAlignmentOptions.MidlineRight); t.textWrappingMode = TextWrappingModes.NoWrap; return t; }).ToList();
            var marks = rows.Select(r => string.IsNullOrEmpty(r.Change) ? null : ChangeMarkUi(box, r.Change, 15, PanelLook.Muted)).ToList();
            // the column heads name the periods ("today" and "yesterday", "these 7 days" and "the 7 before"; 0.8.1): a column is as wide as its head too
            var hn = Label(box, b.NowHead ?? "", PanelLayout.Text.Floor, PanelLook.Muted, align: TextAlignmentOptions.MidlineRight); hn.textWrappingMode = TextWrappingModes.NoWrap;
            var hb = Label(box, b.BeforeHead ?? "", PanelLayout.Text.Floor, PanelLook.Muted, align: TextAlignmentOptions.MidlineRight); hb.textWrappingMode = TextWrappingModes.NoWrap;
            float MarkW(RectTransform m) { if (!m) return 0; LayoutRebuilder.ForceRebuildLayoutImmediate(m); return Mathf.Ceil(LayoutUtility.GetPreferredWidth(m)); }
            var nameW = Mathf.Min(nameMax, Mathf.Max(nameMin, 30 + names.Max(t => Mathf.Ceil(t.preferredWidth))));
            var nowW = Mathf.Max(narrow ? 54 : 64, Mathf.Max(Mathf.Ceil(hn.preferredWidth) + 2, nows.Max(t => Mathf.Ceil(t.preferredWidth))));
            var beforeW = Mathf.Max(narrow ? 54 : 64, Mathf.Max(Mathf.Ceil(hb.preferredWidth) + 2, befores.Max(t => Mathf.Ceil(t.preferredWidth))));
            var changeW = Mathf.Max(narrow ? 72 : 82, marks.Max(MarkW));
            var trackX = nameW + gap; var trackW = Mathf.Max(40, Column - nameW - nowW - beforeW - changeW - 4 * gap);
            float nowX = trackX + trackW + gap, beforeX = nowX + nowW + gap, changeX = beforeX + beforeW + gap;
            // the header row
            if (!string.IsNullOrEmpty(b.Text)) { var h = Label(box, b.Text, PanelLayout.Text.Floor, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); h.characterSpacing = 6; h.textWrappingMode = TextWrappingModes.NoWrap; h.rectTransform.Box(0, 0, trackX + trackW, CmpHead); }
            hn.rectTransform.Box(nowX, 0, nowW, CmpHead); hb.rectTransform.Box(beforeX, 0, beforeW, CmpHead);
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
                if (r.Fraction2 > 0 && PanelLook.RoundedTrace)
                {
                    var o = Img(box, "Before", PanelLook.RoundedTrace, PanelWash.Toward(new Color(colour.r, colour.g, colour.b, TraceAlpha), PanelWash.Island)); o.preserveAspect = false; o.type = Image.Type.Sliced; o.pixelsPerUnitMultiplier = 1f;
                    o.rectTransform.Box(trackX, mid - CmpTrack / 2 - 1, Mathf.Max(8, r.Fraction2 * trackW), CmpTrack + 2);
                }
                nows[k].rectTransform.Box(nowX, top, nowW, CmpRow);
                befores[k].rectTransform.Box(beforeX, top, beforeW, CmpRow);
                if (marks[k]) marks[k].Box(changeX, top, changeW, CmpRow);
            }
            // no air after: it sits in an island, whose padding is the air (0.8 layout D+, PanelModel.BarFormAfter)
        }

        // ---------- 0.8.5: the menu under the chip, the strip's legend and reason ----------

        /// <summary>
        /// The Compare chip's menu (PanelView.CompareMenu; proto C): under the chip, right-aligned, on a canvas of its own over the page (its rows
        /// take the pointer before the page under them). Its caption ("COMPARE TODAY AGAINST"), then one 36 px row per option: a round mark (filled
        /// gold on the one chosen), its words, its dates on the right; a greyed option faint, its short reason where the dates would be, and no
        /// click; Off under a rule. The keys' place has the focus ring (W/S and Enter, PanelUi.CompareMenuKeys). A click anywhere but on an option
        /// only closes it (PanelModel.CompareCloseTarget): a clear catcher over the whole screen lies under the rows, on the menu's canvas, so the
        /// click reaches nothing under it (Unity's own dropdown does the same with its blocker).
        /// </summary>
        static void CompareMenuUi(RectTransform chip, Block menu, Func<string, Action> link)
        {
            var box = Node("Menu", chip);
            box.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            box.anchorMin = box.anchorMax = new Vector2(1, 0); box.pivot = new Vector2(1, 1); box.anchoredPosition = new Vector2(0, -MenuDrop);
            var root = chip.GetComponentInParent<Canvas>();
            var canvas = box.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true;
            if (root) { canvas.sortingLayerID = root.rootCanvas.sortingLayerID; canvas.sortingOrder = root.rootCanvas.sortingOrder + (int)MenuAbove; }
            box.gameObject.AddComponent<PanelRaycaster>();   // a canvas of its own needs its own raycaster for its rows to take a click
            box.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            var close = link?.Invoke(PanelModel.CompareCloseTarget);
            if (close != null)
            {
                var outside = Img(box, "Outside", null, Color.clear, raycast: true);   // first: under the ground and the rows
                outside.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var r = outside.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(Half, Half); r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(MenuOutside, MenuOutside);
                ClickTarget(outside, close);   // a button takes the press too, so it never reaches the chip the menu hangs from (its Press would open it again)
            }
            Img(box, "Ground", null, TagGround).rectTransform.Stretch();
            Edge(box, TagEdge);
            var items = menu.Items ?? new List<Block>();
            float inner = MenuW - 2 * MenuPad, top = MenuPad;
            var cap = BtText(box, (menu.Title ?? "").ToUpperInvariant(), MenuPad + MenuCapIn, top, inner - MenuCapIn, MenuCap - MenuCapLow, PanelLayout.Text.Floor, PanelLook.Muted);
            cap.characterSpacing = KeyTagSpacing;
            top += MenuCap;
            foreach (var o in items)
            {
                if (o.Id == PanelModel.CompareOffId) { BtRect(box, "Rule", PanelLook.Rule, MenuPad, top + MenuRuleAt, inner, 1); top += MenuRuleRoom; }
                var off = o.Tone == PanelModel.OffTone;
                var row = BtRect(box, "Option", o.Selected ? PanelWash.MenuChosen.Game : Color.clear, MenuPad, top, inner, MenuRow);
                row.raycastTarget = !off && link != null;
                if (o.Selected) Edge(row.rectTransform, PanelWash.MenuChosenEdge.Game);
                if (o.Open) FocusRing(row.rectTransform, 0);
                var mark = Img(box, "Mark", o.Selected ? PanelLook.Circle : PanelLook.Ring, o.Selected ? PanelLook.Gold : off ? PanelLook.Faint : PanelLook.Muted);
                mark.preserveAspect = true; mark.rectTransform.Box(MenuPad + MenuMarkX, top + (MenuRow - MenuMark) / 2, MenuMark, MenuMark);
                var ink = off ? PanelLook.Faint : o.Selected ? PanelLook.Gold : PanelLook.Text;
                var right = !string.IsNullOrEmpty(o.Value) ? o.Value : o.Text;
                var rightW = string.IsNullOrEmpty(right) ? 0 : Mathf.Min(MenuDatesMax, TextWidth(box, right, PanelLayout.Text.Floor, off ? FontStyles.Italic : FontStyles.Normal) + Slack);
                BtText(box, o.Title ?? "", MenuPad + MenuWordsX, top, inner - MenuWordsX - 2 * MenuGap - rightW, MenuRow, PanelLayout.Text.Body, ink, style: o.Selected ? FontStyles.Bold : FontStyles.Normal);
                if (rightW > 0) BtText(box, right, MenuPad + inner - MenuGap - rightW, top, rightW, MenuRow, PanelLayout.Text.Floor, off ? PanelLook.Faint : PanelLook.Muted, TextAlignmentOptions.MidlineRight, off ? FontStyles.Italic : FontStyles.Normal);
                if (!off && link != null && !string.IsNullOrEmpty(o.Note)) { var click = link(o.Note); if (click != null) ClickTarget(row, click); }
                top += MenuRow;
            }
            box.sizeDelta = new Vector2(MenuW, top + MenuPad);
        }

        // a text's width as drawn (no layout): made, measured, put away
        static float TextWidth(RectTransform parent, string text, float size, FontStyles style = FontStyles.Normal, float spacing = 0)
        {
            var t = Label(parent, text, size, Color.white, style: style); t.textWrappingMode = TextWrappingModes.NoWrap; t.characterSpacing = spacing;
            var w = Mathf.Ceil(t.preferredWidth); t.gameObject.SetActive(false); Destroy(t.gameObject); return w;
        }

        /// <summary>
        /// The strip's Compare part, from x (FeatsUi.FeatBandBlock places it first on the left): a greyed chip's reason as one quiet line (italic,
        /// cut with an ellipsis at maxW), or while comparing the legend: the strip's COMPARE tag, a solid swatch and "solid bar: today", an outline
        /// swatch and "thin outline: yesterday", the period's words in the text colour (narrower forms where maxW is short). Returns its width;
        /// draw false only measures.
        /// </summary>
        static float StripCompare(RectTransform box, PanelView v, float x, float maxW, bool draw)
        {
            const float line = EarnedLine;
            if (!string.IsNullOrEmpty(v.CompareNote))
            {
                var w = Mathf.Min(maxW, TextWidth(box, v.CompareNote, PanelLook.MinText, FontStyles.Italic) + Slack);
                if (draw) BtText(box, v.CompareNote, x, 0, w, line, PanelLook.MinText, PanelLook.Muted, style: FontStyles.Italic);
                return w;
            }
            if (v.CompareNow == null) return 0;
            // the legend in its widest form that fits maxW: with the strip's COMPARE tag, without it, then without the words (the swatches say solid
            // and outline; a strip with the view switch and About these numbers on its right has room for no more)
            var tag = PanelModel.CompareLabel.ToUpperInvariant();
            float tagW = TextWidth(box, tag, PanelLook.MinText, FontStyles.Normal, KeyTagSpacing), solidW = TextWidth(box, PanelModel.CompareSolidWords, PanelLayout.Text.Small) + KeyWordsGap,
                  traceW = TextWidth(box, PanelModel.CompareOutlineWords, PanelLayout.Text.Small) + KeyWordsGap, nowW = TextWidth(box, v.CompareNow, PanelLayout.Text.Small), thenW = TextWidth(box, v.CompareThen ?? "", PanelLayout.Text.Small);
            float Width(bool withTag, bool words) => (withTag ? tagW + KeyTagGap : 0) + 2 * (KeySwatch + KeyGap) + (words ? solidW + traceW : 0) + nowW + KeyBetween + thenW;
            var showTag = Width(true, true) <= maxW; var showWords = showTag || Width(false, true) <= maxW;
            float at = x;
            if (showTag)
            {
                if (draw) BtText(box, tag, at, 0, tagW + Slack, line, PanelLook.MinText, PanelLook.Muted).characterSpacing = KeyTagSpacing;
                at += tagW + KeyTagGap;
            }
            void Key(bool solid, string words, float wordsW, string period, float periodW)
            {
                if (draw)
                {
                    if (solid) RoundedPart(box, "Solid", PanelWash.KeySolid.Game).rectTransform.Box(at, (line - KeySolidH) / 2, KeySwatch, KeySolidH);
                    else if (PanelLook.RoundedTrace)
                    {
                        var o = Img(box, "Outline", PanelLook.RoundedTrace, PanelWash.KeyTrace.Game); o.preserveAspect = false; o.type = Image.Type.Sliced; o.pixelsPerUnitMultiplier = 1f;
                        o.rectTransform.Box(at, (line - KeyTraceH) / 2, KeySwatch, KeyTraceH);
                    }
                }
                at += KeySwatch + KeyGap;
                if (showWords) { if (draw) BtText(box, words, at, 0, wordsW + Slack, line, PanelLayout.Text.Small, PanelLook.Muted); at += wordsW; }
                if (draw) BtText(box, period, at, 0, Mathf.Max(0, Mathf.Min(periodW + Slack, x + maxW - at)), line, PanelLayout.Text.Small, PanelLook.Text);
                at += periodW;
            }
            Key(true, PanelModel.CompareSolidWords, solidW, v.CompareNow, nowW);
            at += KeyBetween;
            Key(false, PanelModel.CompareOutlineWords, traceW, v.CompareThen ?? "", thenW);
            return Mathf.Min(maxW, at - x);
        }
    }
}
