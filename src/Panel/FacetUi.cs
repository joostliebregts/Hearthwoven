using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Renderer of the filter bar (FacetModel.cs; prototype craft-G). Collapsed by default: one header line ("Filter", the filter
    /// key's keycap, the chosen chips as tokens or "all", the result line, Clear all, Show filters) and, when they fit above the
    /// fold, the slim linked bars. Open (the key's focus, or a click on the header): the chip rows with their live counts between
    /// the header and the bars. The header stays on the same spot in both states, so only what is below it moves.
    /// Rects, the kit's meter track and text only; every click is a link target (PanelModel.FacetLink), pressed rather than
    /// clicked (a chip on the scrolling plate lost its click once, see Press). Placed and sized once when the page is filled.
    /// Block Title (optional) is the header's name ("Biome" on Damage and Deaths; default "Filter").
    /// All text is PanelLook.MinText or larger (the rubric's floor); counts live on the chips, the legends name the colours only.
    /// </summary>
    public partial class PanelUi
    {
        const float FacetLabelW = 128, FacetChipH = 24, FacetBarH = 20, FacetText = PanelLook.MinText;   // fix4: the bar 20 high (was 11) so a wide segment can say its name
        static readonly Color FacetTrack = new Color(0.047f, 0.035f, 0.024f, 1f), FacetOn = new Color(0.91f, 0.66f, 0.28f, 0.2f);

        static void FilterBar(RectTransform col, Block b, Func<string, Action> link)
        {
            var box = VStack(col, 5);
            var items = b.Items ?? new List<Block>();
            var rows = items.Where(x => x.Kind == "facet").ToList();
            var applied = items.FirstOrDefault(x => x.Kind == "applied");
            // Tone "inline": the choice lives elsewhere on the page (Battle Overview's biome tiles); only the applied line is drawn,
            // led by the filter key and the row's name (gold while the keys act on it)
            var inline = b.Tone == "inline";
            if (inline)
            {
                if (applied != null) Applied(box, b, applied, link, rows.FirstOrDefault() ?? (string.IsNullOrEmpty(b.Title) ? null : new Block { Kind = "facet", Title = b.Title }));   // a bar with no row (Damage since install) still names what it is about
            }
            else
            {
                Spacer(box, 4);   // air under the hero above
                if (applied != null) FilterHeader(box, b, applied, link);
                if (b.Open)
                {
                    // Tone "compact": rows with short names take a narrower label column, so the chips get the room (Battle's rows); else 128
                    var labelW = b.Tone == "compact" ? LabelWidth(rows) : FacetLabelW;
                    for (int k = 0; k < rows.Count; k++) FacetRow(box, b, rows[k], link, labelW);
                }
            }
            var shownBars = items.Where(x => x.Kind == "facetbar" && x.Tone != "hidden").ToList();
            if (b.Open && !inline && shownBars.Count > 0) Spacer(box, 4);   // air between the chip rows and the linked bars below them
            foreach (var bar in shownBars) FacetBar(box, b, bar, link);
        }

        // "Kind" and "Main material" (with its honest line under it) on the left, the chips wrapping on the right
        // the label column of a compact bar: the longest name (14 px) or honest line (13 px), 56..128
        static float LabelWidth(List<Block> rows)
        {
            float w = 0;
            for (int k = 0; k < rows.Count; k++) w = Mathf.Max(w, (rows[k].Title ?? "").Length * 7.6f, (rows[k].Note ?? "").Length * 6.8f);
            return Mathf.Clamp(Mathf.Ceil(w) + 6, 56, FacetLabelW);
        }

        static void FacetRow(RectTransform box, Block filter, Block row, Func<string, Action> link, float labelW = FacetLabelW)
        {
            var line = Row(box, 8);
            var label = VStack(line, 2); Size(label, labelW, -1);
            label.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            var head = Line(label, 6, TextAnchor.MiddleLeft); Size(head, -1, 24);
            var focus = row.Tone == "focus";
            var t = Label(head, row.Title, 14, focus ? PanelLook.Gold : PanelLook.Muted, style: focus ? FontStyles.Bold : FontStyles.Normal); t.textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(row.Note)) { var s = Label(label, row.Note, FacetText, PanelLook.Faint); s.textWrappingMode = TextWrappingModes.NoWrap; }
            if (focus) Size(Img(label, "Focus", null, PanelLook.Gold), 54, 2);   // the row the keys act on: a gold line under its name
            var chips = VStack(line, 4); chips.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            chips.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            RectTransform cur = null; float used = 0; var room = Column - labelW - 8;
            foreach (var chip in row.Items ?? new List<Block>())
            {
                var c = FacetChip(chips, filter, row, chip, link, out var w);
                if (cur == null || used + 4 + w > room) { cur = Line(chips, 4, TextAnchor.UpperLeft); used = 0; } else used += 4;
                c.SetParent(cur, false); used += w;
            }
        }

        // a chip: name and its live count; chosen = a gold tint and edge, no count = dimmed and inert, the cursor = the focus ring
        static RectTransform FacetChip(RectTransform parent, Block filter, Block row, Block chip, Func<string, Action> link, out float width)
        {
            bool on = chip.Selected, zero = chip.Tone == "zero", cursor = chip.Tone == "cursor";
            var click = zero ? null : link?.Invoke(PanelModel.FacetLink(filter.Id, row.Id, chip.Id));
            var img = Kit(parent, "Chip", "meter-track", raycast: click != null);
            if (on) Img(img.transform, "On", null, FacetOn).rectTransform.Stretch();
            if (on) Edge(img.rectTransform, PanelLook.Gold);
            if (cursor) FocusRing(img.rectTransform);   // live-polish: the soft rounded focus ring, shown after a key press
            var count = ColorUtility.ToHtmlStringRGB(on ? PanelLook.Gold : PanelLook.Muted);
            var text = RichLabel(img.transform, "filterbar", Rich.Plain(chip.Title) + "  " + Rich.Plain(chip.Value).Ink("#" + count).Bold(), FacetText, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.NoWrap; text.rectTransform.Stretch();
            text.rectTransform.offsetMin = new Vector2(8, 0); text.rectTransform.offsetMax = new Vector2(-8, 0);
            width = Mathf.Ceil(text.preferredWidth) + 16;
            Size(img, width, FacetChipH);
            if (zero) img.gameObject.AddComponent<CanvasGroup>().alpha = 0.35f;   // a disabled option: dimmed on purpose (the rubric asks for it), the live count says 0
            if (click != null) img.gameObject.AddComponent<Press>().Act = click;
            return img.rectTransform;
        }

        // a second edge one pixel in: the cursor's ring and a chosen bar part's outline are two pixels thick
        static void Ring(RectTransform box, Color c)
        {
            var r = Node("Ring", box); r.Stretch(); r.offsetMin = new Vector2(1, 1); r.offsetMax = new Vector2(-1, -1);
            Edge(r, c);
        }

        // one chosen chip as a removable token (a click unchooses it)
        static void Token(RectTransform line, Block filter, Block tok, Func<string, Action> link)
        {
            var p = tok.Id.Split('|');
            var click = p.Length == 2 ? link?.Invoke(PanelModel.FacetLink(filter.Id, p[0], p[1])) : null;
            var img = Img(line, "Token", null, FacetOn, raycast: click != null);
            Edge(img.rectTransform, PanelLook.Gold);
            var t = RichLabel(img.transform, "filterbar", Rich.Plain(tok.Title) + " ×", FacetText, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch(); t.rectTransform.offsetMin = new Vector2(7, 0); t.rectTransform.offsetMax = new Vector2(-7, 0);
            Size(img, Mathf.Ceil(t.preferredWidth) + 14, 20);
            if (click != null) img.gameObject.AddComponent<Press>().Act = click;
        }

        // a small kit button with gold words (Clear all, Show filters, Hide filters)
        static void FacetButton(RectTransform line, string text, Action click)
        {
            var cb = Kit(line, "Button", "meter-track", raycast: click != null);
            var ct = Label(cb.transform, text, FacetText, PanelLook.Gold, align: TextAlignmentOptions.Center); ct.textWrappingMode = TextWrappingModes.NoWrap; ct.rectTransform.Stretch();
            Size(cb, Mathf.Ceil(ct.preferredWidth) + 16, 22);
            if (click != null) cb.gameObject.AddComponent<Press>().Act = click;
        }

        // the header, the same line collapsed or open: "Filter [K]", the tokens (or "all"), the result line, Clear all, Show/Hide filters.
        // The name, the cap and the button open and close the rows with a click; the tokens unchoose their chip.
        static void FilterHeader(RectTransform box, Block filter, Block applied, Func<string, Action> link)
        {
            var line = Line(box, 6, TextAnchor.MiddleLeft); Size(line, -1, 26);
            var toggle = link?.Invoke(PanelModel.FacetOpenLink(filter.Id));
            var name = Label(line, string.IsNullOrEmpty(filter.Title) ? "Filter" : filter.Title, 14, filter.Open ? PanelLook.Gold : PanelLook.Muted, style: filter.Open ? FontStyles.Bold : FontStyles.Normal); name.textWrappingMode = TextWrappingModes.NoWrap;
            if (toggle != null) { name.raycastTarget = true; name.gameObject.AddComponent<Press>().Act = toggle; }
            if (!string.IsNullOrEmpty(filter.KeyCap)) Size(Keycap(line, filter.KeyCap), KeycapWidth(filter.KeyCap, 20), 20);   // the key that opens the rows, as quiet as Q/E/W/S; a word key ("Tab") gets a wider cap
            var tokens = applied.Items ?? new List<Block>();
            // the tokens that fit; the rest say how many more (the open rows show every chosen chip)
            var budget = Column - 52 - (string.IsNullOrEmpty(filter.KeyCap) ? 0 : KeycapWidth(filter.KeyCap, 20) + 6) - (applied.Text ?? "").Length * 7.2f - (tokens.Count > 0 ? 84 : 0) - 120 - 24;
            float spent = 0; int shown = 0;
            foreach (var tok in tokens)
            {
                var w = (tok.Title ?? "").Length * 7.2f + 32;
                if (spent + w > budget && shown > 0) break;
                Token(line, filter, tok, link); spent += w + 6; shown++;
            }
            if (tokens.Count > shown) Label(line, "+" + (tokens.Count - shown) + " more", FacetText, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            if (tokens.Count == 0) Label(line, string.IsNullOrEmpty(applied.Value) ? "all" : applied.Value, FacetText, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft).textWrappingMode = TextWrappingModes.NoWrap;
            Node("Rest", line).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var res = Label(line, applied.Text, FacetText, PanelLook.Muted, align: TextAlignmentOptions.MidlineRight); res.textWrappingMode = TextWrappingModes.NoWrap;
            if (tokens.Count > 0) FacetButton(line, "Clear all", link?.Invoke(PanelModel.FacetClearTarget + filter.Id));
            FacetButton(line, filter.Open ? "Hide filters" : "Show filters", toggle);
        }

        // the inline bar's one line (Battle Overview): the row's name and the key first, the tokens (or what shows with no filter), the result line
        static void Applied(RectTransform box, Block filter, Block applied, Func<string, Action> link, Block lead = null)
        {
            var line = Line(box, 6, TextAnchor.MiddleLeft); Size(line, -1, 22);
            if (lead != null)
            {
                var focus = lead.Tone == "focus";
                var name = Label(line, lead.Title, 14, focus ? PanelLook.Gold : PanelLook.Muted, style: focus ? FontStyles.Bold : FontStyles.Normal); name.textWrappingMode = TextWrappingModes.NoWrap;
                if (!string.IsNullOrEmpty(filter.KeyCap)) Size(Keycap(line, filter.KeyCap), KeycapWidth(filter.KeyCap, 20), 20);
            }
            var tokens = applied.Items ?? new List<Block>();
            foreach (var tok in tokens) Token(line, filter, tok, link);
            if (tokens.Count == 0) Label(line, applied.Title, FacetText, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft).textWrappingMode = TextWrappingModes.NoWrap;
            Node("Rest", line).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var res = Label(line, applied.Text, FacetText, PanelLook.Muted, align: TextAlignmentOptions.MidlineRight); res.textWrappingMode = TextWrappingModes.NoWrap;
            if (tokens.Count > 0) FacetButton(line, "Clear all", link?.Invoke(PanelModel.FacetClearTarget + filter.Id));
        }

        const float SwatchW = 18;

        // the part's pattern (PanelModel.FacetPatternOf) tiled over its colour; the solid part has none
        static void PartPattern(RectTransform on, Block p)
        {
            var name = VocabName(p.Pattern);
            if (name != null && PanelLook.Vocab(name)) VocabImg(on, "Pattern", name, Color.white).rectTransform.Stretch();
        }

        // zones-wording (Joost 2026-10-09: the white words in black boxes were ugly; the legend names the parts): a segment wide enough
        // carries its number only, no box, in the ink that reads on its own colour (dark on a light part, light on a dark one); else nothing
        static void SegmentLabel(RectTransform seg, Block p, float width)
        {
            if (string.IsNullOrEmpty(p.Value)) return;
            var t = Label(seg, p.Value, FacetText, SegmentInk(Hex(p.Colour, PanelLook.Accent)), style: FontStyles.Bold, align: TextAlignmentOptions.Center); t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
            var need = Mathf.Ceil(t.preferredWidth) + 10;
            if (need > width - 4) { UnityEngine.Object.Destroy(t.gameObject); return; }
            // the pattern stops under the number: a patch of the part's own colour (it reads as the bar, not a box on it)
            var patch = Img(seg, "NumberGround", null, Hex(p.Colour, PanelLook.Accent));
            foreach (var r in new[] { patch.rectTransform, t.rectTransform }) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(need, 16); }
            patch.transform.SetSiblingIndex(t.transform.GetSiblingIndex());
        }

        /// <summary>The ink of a number on a bar part: near-black on a light colour, warm cream on a dark one (relative luminance, the WCAG split).</summary>
        internal static Color SegmentInk(Color c)
        {
            float L(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            var lum = 0.2126f * L(c.r) + 0.7152f * L(c.g) + 0.0722f * L(c.b);
            return lum > 0.18f ? new Color(0.08f, 0.06f, 0.04f) : new Color(0.97f, 0.93f, 0.84f);
        }

        // a linked bar (20 px, fix4; was a slim 11): its parts in the row's colours, each with its pattern, the wide ones named, the chosen part outlined in gold;
        // its legend under it (swatch with the same pattern and the name only: the counts are on the chips; the chosen colour's name is gold)
        static void FacetBar(RectTransform box, Block filter, Block bar, Func<string, Action> link)
        {
            var parts = bar.Items ?? new List<Block>();
            var head = Line(box, 8); Size(head, -1, 18);
            var title = Label(head, bar.Title, FacetText, PanelLook.Muted); title.textWrappingMode = TextWrappingModes.NoWrap;
            Node("Rest", head).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            if (!string.IsNullOrEmpty(bar.Note)) { var n = Label(head, bar.Note, FacetText, PanelLook.Faint, align: TextAlignmentOptions.MidlineRight); n.textWrappingMode = TextWrappingModes.NoWrap; }
            var track = Img(box, "FacetBar", null, FacetTrack); Size(track, -1, FacetBarH + 2);
            Edge(track.rectTransform, PanelLook.SlotEdge);
            var inner = Node("Parts", track.transform); inner.Stretch();   // the parts' own row; the edge stays out of its layout
            var h = inner.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(1, 1, 1, 1); h.spacing = 0; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            foreach (var p in parts.Where(x => x.Fraction > 0))
            {
                var click = p.Id == PanelModel.FoldId ? null : link?.Invoke(PanelModel.FacetLink(filter.Id, bar.Id, p.Id));   // the folded part: its kinds are chosen by their chips
                var seg = Img(inner, "Part", null, Hex(p.Colour, PanelLook.Accent), raycast: click != null);
                var le = seg.gameObject.AddComponent<LayoutElement>(); le.flexibleWidth = p.Fraction; le.minWidth = 3; le.preferredWidth = 0;
                PartPattern(seg.rectTransform, p);   // a mark of its own on the colour, so the part is told apart without the hue
                SegmentLabel(seg.rectTransform, p, (Column - 2f) * p.Fraction);   // a segment wide enough says its name (and its count when that fits too)
                if (p.Selected) { Edge(seg.rectTransform, PanelLook.Gold); Ring(seg.rectTransform, PanelLook.Gold); }
                if (click != null) seg.gameObject.AddComponent<Press>().Act = click;
            }
            // the legend: every part of the bar (at most PanelModel.BarMaxParts, the rest as "Other (n kinds)"), a swatch and its name; a part with nothing under the other rows' choice stays, quiet
            var lines = VStack(box, 1); lines.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            RectTransform cur = null; float used = 0;
            foreach (var p in parts)
            {
                var zero = p.Tone == "zero";
                var entry = Line(lines, 4, TextAnchor.MiddleLeft);
                var swatch = Img(entry, "Swatch", null, Hex(p.Colour, PanelLook.Accent)); Size(swatch, SwatchW, 12);   // the segment in small: colour and mark
                PartPattern(swatch.rectTransform, p);
                var t = Label(entry, p.Title, FacetText, p.Selected ? PanelLook.Gold : zero ? PanelLook.Faint : PanelLook.Text, style: p.Selected ? FontStyles.Bold : FontStyles.Normal); t.textWrappingMode = TextWrappingModes.NoWrap;
                var w = SwatchW + 4 + Mathf.Ceil(t.preferredWidth);
                if (cur == null || used + 12 + w > Column) { cur = Line(lines, 12, TextAnchor.MiddleLeft); Size(cur, -1, 16); used = 0; } else used += 12;
                entry.SetParent(cur, false); used += w;
            }
        }
    }
}
