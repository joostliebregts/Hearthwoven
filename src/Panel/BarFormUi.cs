using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The book's one bar form (work/hearthwoven-0.7/BAR-FORM.md, prototype bar-prototypes/index-v4.html): every composition and
    /// facet bar is drawn here, the pages only pass parts. The bar: rounded parts 4 px apart that together make one rounded
    /// rectangle; numbers inside a part only on damage-type bars, where they fit; every part its own sliver (at least 6 px, B39). The
    /// list under it: one row per part, a compact table that hugs its content (swatch and name, the number right-aligned, the share
    /// greyed), two columns side by side past four rows. Measures in PanelModel (BarFormBar ...); rows from PanelModel.BarFormOf.
    /// Mirrored by barForm() in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        /// <summary>How one bar form is drawn where its page needs more than the parts: a thin bar (the Hall), no bar (a "single"
        /// composition), the bar's length against the largest of its kind (Battle's weapons), a colour of its own per part (a
        /// fellow's colour), a click per part (the filter's bars), a damage-type list in two columns from three rows (Pairs, B34), the air
        /// after the block when its page sets its own (After: Defence's foes without a list, a weapon's band).</summary>
        sealed class BarLook
        {
            public bool Thin, NoBar, Pairs; public float Length = 1, After = PanelModel.BarFormAfter;
            public Func<Block, Color> Colour; public Func<Block, Action> Click;
        }

        const float BarListName = PanelLook.MinText, BarListText = 15, BarListShare = 14, BarListCellGap = PanelModel.BarFormCellGap, BarSwatch = 12, BarSwatchGap = 8;   // 0.8 layout D+: compact entries on the page's legend grid, names at the floor on every legend

        static Color BarRowColour(BarRow r, BarLook look) =>
            look?.Colour != null && !(r.Other && r.Part.Id == PanelModel.FoldId) ? look.Colour(r.Part) : Hex(r.Colour, PanelLook.Accent);

        // a rounded part (or swatch) in its colour; the faded veil inside it is clipped to its shape
        static Image RoundedPart(Transform parent, string name, Color c, bool raycast = false)
        {
            var img = Img(parent, name, PanelLook.Rounded, c, raycast);
            img.preserveAspect = false;
            if (PanelLook.Rounded) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            return img;
        }

        // a part's wood grain or groundwork pattern, exactly as 0.6.5 drew it (Joost 2026-10-09: keep the textures and colours): the vocab
        // grain-* sprite tiled at its native size (never stretched) under the part's rounded shape; a neutral "-n" grain multiplied by the
        // part's colour lifted by GrainBase, an authored grain in its own colours. null: the part has no pattern (or its sprite is missing)
        const float GrainBase = 0.85f;
        static string GrainOf(BarRow r) => r == null || r.Other || r.Part == null ? null : VocabName(r.Part.Pattern) is string g && g.StartsWith("grain-") && PanelLook.Vocab(g) ? g : null;
        static Color GrainTint(string grain, Block part)
        {
            if (!grain.EndsWith("-n")) return Color.white;
            var c = Hex(part.Colour, Color.white);
            return new Color(Mathf.Clamp01(c.r / GrainBase), Mathf.Clamp01(c.g / GrainBase), Mathf.Clamp01(c.b / GrainBase), c.a);
        }
        static void Grain(Image part, string grain, Block of, float scale = 1)
        {
            part.gameObject.AddComponent<Mask>().showMaskGraphic = false;   // the rounded shape clips the tiles; the grain is the part's fill
            var g = VocabImg(part.transform, "Grain", grain, GrainTint(grain, of)).rectTransform;
            if (scale == 1) { g.Stretch(); return; }
            // a swatch: the same tiles at a smaller scale (a rect 1/scale the swatch's size, scaled down to cover it)
            g.pivot = Vector2.zero; g.anchorMin = Vector2.zero; g.anchorMax = new Vector2(1 / scale, 1 / scale); g.offsetMin = g.offsetMax = Vector2.zero;
            g.localScale = Vector3.one * scale;
        }

        static void BarWithList(RectTransform col, IList<Block> parts, bool numbers, BarLook look = null)
        {
            look = look ?? new BarLook();
            var form = PanelModel.BarFormOf(parts, numbers, look.Pairs);
            if (form.Rows.Count == 0) return;
            var box = VStack(col, 0);
            // 0.8 hover (PICKS 4 B, HoverUi.cs): a bar with a list lights part and row together; a one-part bar or a list without a bar needs none
            var hover = form.List && !look.NoBar ? box.gameObject.AddComponent<BarHover>() : null;
            hover?.Init(form.Rows.Count);
            if (!look.NoBar)
            {
                var length = Mathf.Clamp01(look.Length <= 0 ? 1 : look.Length);
                var width = Mathf.Max(40f, Column * length);
                var height = PanelModel.BarHeightOf(parts, look.Thin);   // 0.8 layout D+: 22; a grain or groundwork pattern keeps 24
                RectTransform bar;
                if (length < 1)
                {
                    var holder = Line(box, 0); Size(holder, -1, height);
                    bar = Node("BarForm", holder); Size(bar, width, height);
                    Node("Rest", holder).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                }
                else { bar = Node("BarForm", box); Size(bar, -1, height); }
                // each part at least 6 px wide, so a tiny Other still shows between its 4 px gaps (PanelModel.BarParts)
                var spans = PanelModel.BarParts(form.Rows.Select(r => r.Fraction).ToArray(), form.Rows.Select(r => r.Part.Fraction2).ToArray(), (6f + PanelModel.BarFormGap) / width);
                for (int k = 0; k < form.Rows.Count; k++)
                {
                    var row = form.Rows[k]; var c = BarRowColour(row, look);
                    var click = row.Part.Id == PanelModel.FoldId ? null : look.Click?.Invoke(row.Part);
                    var seg = RoundedPart(bar, "Part", c, raycast: click != null || hover != null);
                    var r = seg.rectTransform;
                    r.anchorMin = new Vector2(spans[k].from, 0); r.anchorMax = new Vector2(spans[k].to, 1); r.pivot = new Vector2(0, 0.5f);
                    r.offsetMin = new Vector2(PanelModel.BarFormGap / 2, 0); r.offsetMax = new Vector2(-PanelModel.BarFormGap / 2, 0);
                    if (hover != null) { hover.Parts[k] = r; var hp = seg.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = k; }
                    var grain = GrainOf(row);
                    if (grain != null) Grain(seg, grain, row.Part);
                    if (spans[k].fadedTo > spans[k].from)
                    {
                        // the part counted before Hearthwoven (K1): the same colour at 55 %, one fill per kind, no line inside it
                        if (grain == null) seg.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                        var veil = Img(seg.transform, "Faded", null, FadedVeil).rectTransform;
                        veil.anchorMin = Vector2.zero; veil.anchorMax = new Vector2((spans[k].fadedTo - spans[k].from) / (spans[k].to - spans[k].from), 1);
                        veil.offsetMin = veil.offsetMax = Vector2.zero;
                    }
                    if (form.Numbers && !row.Other && !string.IsNullOrEmpty(row.Value) && !look.Thin)
                    {
                        // damage bars: the number inside the part where it fits (14 px and more, in the ink that reads on the colour)
                        var t = Label(seg.transform, row.Value, 15, SegmentInk(c), style: FontStyles.Bold, align: TextAlignmentOptions.Center);
                        t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch();
                        if (Mathf.Ceil(t.preferredWidth) + 12 > (spans[k].to - spans[k].from) * width - PanelModel.BarFormGap) UnityEngine.Object.Destroy(t.gameObject);
                    }
                    if (row.Selected && PanelLook.RoundedEdge)
                    {
                        var edge = Img(seg.transform, "Chosen", PanelLook.RoundedEdge, PanelLook.Gold); edge.preserveAspect = false; edge.type = Image.Type.Sliced; edge.pixelsPerUnitMultiplier = 1f;
                        edge.rectTransform.Stretch();
                    }
                    if (click != null) seg.gameObject.AddComponent<Press>().Act = click;
                }
                if (hover != null)
                {
                    // the hover ring: one per bar, over all its parts (so its glow lies evenly on both neighbours), clear until a part is pointed at
                    var ring = PanelLook.Vocab("focus-ring") ? VocabImg(bar, "Hover", "focus-ring", Color.clear) : Img(bar, "Hover", PanelLook.RoundedEdge, Color.clear);
                    if (ring.sprite == PanelLook.RoundedEdge) { ring.preserveAspect = false; ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 1f; }
                    ring.raycastTarget = false; hover.RingImage = ring;
                }
                if (form.List) Spacer(box, PanelModel.BarFormToList);
            }
            if (form.List) BarList(box, form, look, hover);   // one part: no list, the head line says its name and total
            if (look.After > 0) Spacer(box, look.After);
        }

        // 0.8 layout D+ (Joost, after step A): the page's legends on one grid (PanelModel.LegendGrid, LegendPage): drawn as one column each, then laid
        // out together once the plate is drawn (PlateIslands calls LayLegends), so every legend on the page has the same columns
        sealed class Legend { public RectTransform Holder; public List<RectTransform> Rows; public float Column; public Func<int, int, bool, float> Width; }
        static readonly List<Legend> legends = new List<Legend>();
        static bool legendsLater;   // PlateIslands is drawing: the legends wait for the page's grid

        static void LayLegends(List<Legend> page, float wide)
        {
            int Per(int n, int k) { k = Math.Max(1, Math.Min(k, n)); return (n + k - 1) / k; }
            float Need(Legend l, int k) { var n = l.Rows.Count; var per = Per(n, k); float w = 0; for (int c = 0; c * per < n; c++) w = Mathf.Max(w, l.Width(c * per, Math.Min(n, (c + 1) * per), false)); return w; }
            var columns = PanelModel.LegendPage(page.Select(l => (l.Column, (Func<int, float>)(k => Need(l, k)))), wide);
            foreach (var l in page)
            {
                var (k, w) = PanelModel.LegendGrid(columns, l.Column, wide);
                var n = l.Rows.Count; var per = Per(n, k);
                var stacks = new List<RectTransform> { (RectTransform)l.Rows[0].parent };
                for (int c = 1; c * per < n; c++) { var s = VStack(l.Holder, PanelModel.BarFormRowGap); s.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false; stacks.Add(s); }
                for (int c = 0; c < stacks.Count; c++)
                {
                    Size(stacks[c], w, -1);
                    for (int i = c * per; i < Math.Min(n, (c + 1) * per); i++) l.Rows[i].SetParent(stacks[c], false);
                    l.Width(c * per, Math.Min(n, (c + 1) * per), true);   // each column a compact table: name, the number right-aligned, the share
                }
            }
        }

        // the list: a compact table per column (swatch and name | number, right-aligned | share, greyed), on the page's legend grid (LayLegends)
        static void BarList(RectTransform box, BarForm form, BarLook look, BarHover hover = null)
        {
            var holder = Line(box, PanelModel.LegendGap, TextAnchor.UpperLeft);
            var left = VStack(holder, PanelModel.BarFormRowGap); left.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            var cells = new List<(RectTransform row, RectTransform name, TextMeshProUGUI title, TextMeshProUGUI n, TextMeshProUGUI pct, float extra)>();
            foreach (var r in form.Rows)
            {
                var row = Line(left, BarListCellGap); Size(row, -1, PanelModel.BarFormRow);
                var name = Line(row, BarSwatchGap);
                var sw = RoundedPart(name, "Swatch", BarRowColour(r, look)); Size(sw, BarSwatch, BarSwatch);
                if (GrainOf(r) is string g) Grain(sw, g, r.Part, 0.5f);   // the list's swatch wears the same texture
                var zero = r.Part?.Tone == "zero";
                var title = Label(name, r.Title, BarListName, r.Selected ? PanelLook.Gold : zero ? PanelLook.Faint : PanelLook.Text, style: r.Selected ? FontStyles.Bold : FontStyles.Normal, align: TextAlignmentOptions.MidlineLeft);
                title.textWrappingMode = TextWrappingModes.NoWrap;
                var n = Label(row, r.Value ?? "", BarListText, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineRight); n.textWrappingMode = TextWrappingModes.NoWrap;
                var pct = Label(row, r.Share, BarListShare, PanelLook.Faint, align: TextAlignmentOptions.MidlineRight); pct.textWrappingMode = TextWrappingModes.NoWrap;
                float extra = 0;
                if (r.Part != null && Labelled(r.Part)) { var s = Since(row, 14, r.Part); extra = BarListCellGap + Mathf.Ceil(s.preferredWidth); }   // a part counted on this PC in a bar of other counts
                if (hover != null)
                {
                    // the row's band (behind its cells, a little wider than them) is its pointer target, clear until it or its part is pointed at
                    var k = cells.Count;
                    var band = RoundedPart(row, "Lit", Color.clear, raycast: true); band.transform.SetAsFirstSibling();
                    band.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    var br = band.rectTransform; br.Stretch(); br.offsetMin = new Vector2(-8, -4); br.offsetMax = new Vector2(8, 4);
                    hover.Bands[k] = band; hover.Names[k] = title; hover.Numbers[k] = n; hover.Shares[k] = pct; hover.NameColours[k] = title.color;
                    var hp = row.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = k;
                }
                cells.Add((row, name, title, n, pct, extra));
            }
            // each column a table of its own that hugs its content: the numbers sit right after that column's longest name
            float Width(int from, int to, bool apply)
            {
                var part = cells.Skip(from).Take(to - from).ToList();
                if (part.Count == 0) return 0;
                float nameW = part.Max(c => BarSwatch + BarSwatchGap + Mathf.Ceil(c.title.preferredWidth)), numW = part.Max(c => Mathf.Ceil(c.n.preferredWidth)), pctW = part.Max(c => Mathf.Ceil(c.pct.preferredWidth));
                if (apply) foreach (var c in part) { Size(c.name, nameW, PanelModel.BarFormRow); Size(c.title, Mathf.Ceil(c.title.preferredWidth), PanelModel.BarFormRow); Size(c.n, numW, PanelModel.BarFormRow); Size(c.pct, pctW, PanelModel.BarFormRow); }
                return nameW + numW + pctW + 2 * BarListCellGap + part.Max(c => c.extra);
            }
            var legend = new Legend { Holder = holder, Rows = cells.Select(c => c.row).ToList(), Column = Column, Width = Width };
            if (legendsLater) legends.Add(legend); else LayLegends(new List<Legend> { legend }, Column);   // off a plate: its own grid
        }
    }
}
