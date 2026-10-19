using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 What to use (Chapters/WhatToUse.cs; board 7, WhatToUse.dc.html): the table of a set, a variant of the foe table with the foes as columns.
    /// The head row: the name column's words, then per foe its trophy (40 px), its name (two lines where it needs them) and its weaknesses as type
    /// swatches; a click on a foe opens its detail under the table (PanelModel.UseOpenLink), the open one's name in gold over a gold rule. A row:
    /// the item's name, never cut (with its status on one line where both fit, else up to two lines and the status under it), its damage types in
    /// their colours ("26 pierce + 52 frost"), its materials on one line (cut: "..." and the whole list on hover). Then a cell per foe, centred in
    /// its column: the number over a small bar of the damage-row parts (rounded, 1 px apart, the type colours; one length scale for the table),
    /// the column's highest (a tie: both) hugged by a thin gold edge and a faint gold ground, no box on the others, a resisted one quiet. A
    /// not-known item's row is quieter. Under the table: the italic lines (Note, Text). Mirrored by useTable in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        const float UtName = 236, UtColMax = 84, UtHead = 104, UtIcon = 40, UtIconTop = 4, UtNameTop = 48, UtSwatch = 12, UtLine = 18, UtPad = 4, UtGap = 6;
        const float UtNumH = 20, UtBarH = 7, UtBarGap = 1, UtBarMax = 64, UtCellPadX = 6, UtCellPadY = 5, UtCellMin = 56, UtCellIn = 4;   // a cell (board 7's .cv): its number, its bar 4 px under it
        const float UtEdgeScale = 2f / 3f, UtPartShare = 0.5f;   // the rounded trace's 1 px rim drawn at 1.5 px; a part's floor share (as GroupDealtBar)
        const float UtNameLines = 2, UtQuiet = 0.5f, UtGround = 0.08f, UtResisted = 0.45f;   // a not-known item's row; the best cell's faint gold ground; a resisted cell's bar

        static void UseTable(RectTransform col, Block b, Func<string, Action> link)
        {
            var cols = (b.Items ?? new List<Block>()).Where(i => i.Kind == "usefoe").ToList();
            var rows = (b.Items ?? new List<Block>()).Where(i => i.Kind == "userow").ToList();
            if (cols.Count == 0 || rows.Count == 0) return;
            var colW = Mathf.Min(UtColMax, (Column - UtName) / cols.Count); var x0 = Column - colW * cols.Count;
            var barMax = Mathf.Min(UtBarMax, colW - 2 * UtCellPadX - UtPad);   // one length scale for the table: the highest cell's bar

            var h = BtRow(col, UtHead);
            BtText(h, b.Title, 0, UtHead - UtLine - UtPad, x0, UtLine, PanelLayout.Text.Floor, PanelLook.Muted);
            for (int k = 0; k < cols.Count; k++)
            {
                var c = cols[k];
                var cell = Node("Foe", h); cell.Box(x0 + k * colW, 0, colW, UtHead);
                if (link != null)
                {
                    var ground = RoundedPart(cell, "Ground", Color.clear, raycast: true); ground.rectTransform.Stretch();
                    cell.gameObject.AddComponent<Press>().Act = link(PanelModel.UseOpenLink(b, c));
                }
                BtMarker(cell, c.Icon, (colW - UtIcon) / 2, UtIconTop, UtIcon);
                var name = Label(cell, c.Title, PanelLook.MinText, c.Selected ? PanelLook.Gold : PanelLook.Text, align: TextAlignmentOptions.Top);
                name.textWrappingMode = TextWrappingModes.Normal; name.overflowMode = TextOverflowModes.Overflow;
                name.rectTransform.Box(UtPad / 2, UtNameTop, colW - UtPad, UtHead - UtNameTop - UtSwatch - UtPad);
                // its weaknesses: a small swatch per damage type it takes more from, in the type's colour, centred under the name
                var weak = (c.Items ?? new List<Block>()).Where(i => i.Kind == "weak").ToList();
                var sx = (colW - weak.Count * (UtSwatch + UtPad / 2)) / 2;
                foreach (var w in weak) { var sw = RoundedPart(cell, "Weak", Hex(w.Colour, PanelLook.Text)); sw.rectTransform.Box(sx, UtHead - UtSwatch - UtPad, UtSwatch, UtSwatch); sx += UtSwatch + UtPad / 2; }
                if (c.Selected) BtRect(cell, "Open", PanelLook.Gold, UtPad, UtHead - UtPad / 2, colW - 2 * UtPad, UtPad / 2);
            }
            BtRect(h, "Rule", PanelLook.Rule, 0, UtHead - 1, Column, 1);

            var measure = Label(col, "", PanelLayout.Text.Small, PanelLook.Text); measure.textWrappingMode = TextWrappingModes.NoWrap;
            float Wide(string text, float size, FontStyles style = FontStyles.Normal) { measure.text = text ?? ""; measure.fontSize = size; measure.fontStyle = style; return Mathf.Ceil(measure.preferredWidth) + 1; }
            var room = x0 - UtGap;
            foreach (var row in rows)
            {
                // the name never cut: its status beside it where both fit, else the name on up to two lines and the status under it
                var status = row.Text ?? "";
                var nameW = Wide(row.Title, PanelLayout.Text.Small, FontStyles.Bold); var statusW = status.Length > 0 ? Wide(status, PanelLook.MinText, FontStyles.Italic) : 0;
                var oneLine = nameW + UtGap + statusW <= room;
                var nameLines = nameW <= room ? 1 : UtNameLines;
                var lines = oneLine ? 1 : nameLines + (status.Length > 0 ? 1 : 0);
                var rowH = 2 * UtPad + lines * UtLine + 2 * UtLine;   // the name lines, the types, the materials
                var r = BtRow(col, rowH);
                if (row.Tone == "unknown") r.gameObject.AddComponent<CanvasGroup>().alpha = UtQuiet;
                var nl = Label(r, row.Title, PanelLayout.Text.Small, PanelLook.Text, align: TextAlignmentOptions.TopLeft, style: FontStyles.Bold);
                nl.textWrappingMode = TextWrappingModes.Normal; nl.overflowMode = TextOverflowModes.Overflow;
                nl.rectTransform.Box(0, UtPad, oneLine ? nameW : room, nameLines * UtLine);
                if (status.Length > 0)
                    BtText(r, status, oneLine ? nameW + UtGap : 0, UtPad + (oneLine ? 0 : nameLines * UtLine), oneLine ? room - nameW - UtGap : room, UtLine, PanelLook.MinText, PanelLook.Faint, style: FontStyles.Italic);
                var y = UtPad + lines * UtLine;
                // its damage types in their colours: "26 pierce + 52 frost"
                float x = 0; var parts = (row.Items ?? new List<Block>()).Where(i => i.Kind == "part").ToList();
                for (int p = 0; p < parts.Count; p++)
                {
                    if (p > 0) { var plus = " + "; var pw = Wide(plus, PanelLook.MinText); BtText(r, plus, x, y, pw, UtLine, PanelLook.MinText, PanelLook.Muted); x += pw; }
                    var words = parts[p].Value + " " + parts[p].Title; var ww = Wide(words, PanelLook.MinText);
                    BtText(r, words, x, y, Mathf.Min(ww, Mathf.Max(0, room - x)), UtLine, PanelLook.MinText, Hex(parts[p].Colour, PanelLook.Text));
                    x += ww;
                }
                // its materials on one line; cut, it ends in "..." and says the whole list when pointed at (the page's one tag)
                if (!string.IsNullOrEmpty(row.Note))
                {
                    var m = BtText(r, row.Note, 0, y + UtLine, room, UtLine, PanelLook.MinText, PanelLook.Muted);
                    if (Wide(row.Note, PanelLook.MinText) > room) MaterialsHover(r, m.rectTransform, row.Note, room);
                }
                // a cell per foe, centred in its column: its number, its bar under it; the column's highest (a tie: both) hugged by the gold edge
                var cells = (row.Items ?? new List<Block>()).Where(i => i.Kind == "usecell").ToList();
                for (int k = 0; k < cells.Count && k < cols.Count; k++)
                {
                    var best = cells[k].Tone == PanelModel.UseBest; var low = cells[k].Tone == PanelModel.UseLow;
                    var barW = barMax * Mathf.Clamp01(cells[k].Fraction);
                    var cw = Mathf.Min(colW - UtPad, Mathf.Max(UtCellMin, barW + 2 * UtCellPadX)); var ch = 2 * UtCellPadY + UtNumH + UtCellIn + UtBarH;
                    var box = Node("Cell", r); box.Box(x0 + k * colW + (colW - cw) / 2, (rowH - ch) / 2, cw, ch);
                    if (best)
                    {
                        var gold = PanelLook.Gold;
                        RoundedPart(box, "Ground", new Color(gold.r, gold.g, gold.b, UtGround)).rectTransform.Stretch();
                        var edge = Img(box, "Edge", PanelLook.RoundedTrace, gold);
                        if (PanelLook.RoundedTrace) { edge.type = Image.Type.Sliced; edge.pixelsPerUnitMultiplier = UtEdgeScale; }
                        edge.rectTransform.Stretch();
                    }
                    BtText(box, cells[k].Value, 0, UtCellPadY, cw, UtNumH, PanelLayout.Text.Small, best ? PanelLook.Gold : low ? PanelLook.Faint : PanelLook.Text, TextAlignmentOptions.Center, best ? FontStyles.Bold : FontStyles.Normal);
                    CellBar(box, cells[k], (cw - barW) / 2, UtCellPadY + UtNumH + UtCellIn, barW, low);
                }
                BtRect(r, "Rule", BtLine, 0, rowH - 1, Column, 1);
            }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
            foreach (var line in new[] { b.Text, b.Note }.Where(l => !string.IsNullOrEmpty(l)))
            {
                var t = Label(col, line, PanelLook.MinText, PanelLook.Muted, style: FontStyles.Italic); t.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        // a cell's bar: the damage-row parts (as DamageRowsUi.DamageBar draws them: BarParts' shares, rounded parts, the type colours), 1 px apart,
        // as tall as board 7's
        static void CellBar(RectTransform box, Block cell, float x, float top, float w, bool resisted)
        {
            var parts = (cell.Items ?? new List<Block>()).Where(i => i.Kind == "part").ToList();
            if (parts.Count == 0 || w <= 0) return;
            var bar = Node("Bar", box); bar.Box(x, top, w, UtBarH);
            if (resisted) bar.gameObject.AddComponent<CanvasGroup>().alpha = UtResisted;
            var span = w + UtBarGap;
            var spans = PanelModel.BarParts(parts.Select(p => p.Fraction).ToArray(), null, Mathf.Min(UtPartShare, (1 + UtBarGap) / span));
            for (int k = 0; k < parts.Count; k++)
            {
                var seg = RoundedPart(bar, "Part", Hex(parts[k].Colour, PanelLook.Text)); seg.pixelsPerUnitMultiplier = DrRadiusScale;
                seg.rectTransform.Box(spans[k].from * span, 0, Mathf.Max(1f, (spans[k].to - spans[k].from) * span - UtBarGap), UtBarH);
            }
        }

        // a cut materials line says the whole list when pointed at, through the page's one tag (as the Best arrow tie's cell, BattleUi.ArrowTieHover)
        static void MaterialsHover(RectTransform row, RectTransform line, string words, float w)
        {
            var hit = Img(line, "Hit", null, Color.clear, raycast: true); hit.rectTransform.Stretch();
            var hover = row.gameObject.AddComponent<BarHover>(); hover.Init(1);
            hover.Parts[0] = line; var hp = hit.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = 0;
            hover.TagOver(PageTag(row), line, new[] { words }, new float[1], default, w, centred: false);
        }
    }
}
