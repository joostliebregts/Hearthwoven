using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Farming's crop grid (cropgrid, Chapters/DeedsModel.cs Farming; diff-05, Joost 2026-10-08): the item grid's compact
    /// tile (picture left, name) with two thin paired bars under the name, planted (tan) over picked (green), each with its
    /// number and word, on the crop's own scale, so "how much came back" reads at a glance. A planted bar's faded
    /// share (counted before Hearthwoven) is the same colour at 55 %. The legend once under the grid: the two colours, what
    /// picked includes, and the faded chip when any part is faded. Rects and the game's own crop icons only.
    /// </summary>
    public partial class PanelUi
    {
        const float CropTileMin = 220, CropGap = 10, CropPic = 32, CropTileH = 66, PairH = 6, PairLabelW = 96;

        static void CropGrid(RectTransform col, Block b)
        {
            var all = b.Items ?? new List<Block>();
            if (all.Count == 0) return;
            var items = all.Where(i => i.Tone != PanelModel.SlimTone).ToList();   // fix4: a slim entry (Other plants: picked alone) is one row under the grid, not a tile of its own
            var per = Mathf.Clamp(Mathf.FloorToInt((Column + CropGap) / (CropTileMin + CropGap)), 1, 6);
            var w = Mathf.Floor((Column - CropGap * (per - 1)) / per);
            var grid = Node("CropGrid", col);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(w, CropTileH); g.spacing = new Vector2(CropGap, CropGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = per;
            var faint = "#" + ColorUtility.ToHtmlStringRGB(PanelLook.Faint);
            foreach (var i in items)
            {
                var tile = Kit(grid, i.Title, "meter-track").rectTransform;
                var p = Pic(tile, i.Icon, CropPic); p.anchorMin = p.anchorMax = p.pivot = new Vector2(0, 0.5f); p.anchoredPosition = new Vector2(8, 0);
                float x = 8 + CropPic + 8, bw = w - x - 8 - PairLabelW - 6;
                OneLine(Label(tile, i.Title, 14, PanelLook.Muted)).rectTransform.Box(x, 5, w - x - 6, 18);
                var pairs = i.Items ?? new List<Block>();
                for (int k = 0; k < pairs.Count && k < 2; k++)
                {
                    var pair = pairs[k]; var top = 27 + (pair.Tone == PanelModel.PlantedWord ? 0 : 1) * 17;   // planted over picked; a tile with picked alone (Other plants) keeps picked on the picked row
                    Img(tile, "Track", null, PanelLook.Track).rectTransform.Box(x, top + 5, bw, PairH);
                    var colour = Hex(pair.Colour, PanelLook.Accent);
                    var full = Mathf.Clamp01(pair.Fraction) * bw; var fade = Mathf.Clamp01(pair.Fraction2);
                    if (full > 0 && fade > 0) Img(tile, "Faded", null, new Color(colour.r, colour.g, colour.b, FadedAlpha)).rectTransform.Box(x, top + 5, Mathf.Max(1, full * fade), PairH);
                    if (full > 0 && fade < 1) Img(tile, "Fill", null, colour).rectTransform.Box(x + full * fade, top + 5, Mathf.Max(2, full * (1 - fade)), PairH);
                    // the number and its word beside the bar ("72 planted"); formatted numbers and fixed words only, so rich text is safe
                    var t = RichLabel(tile, "cropgrid", Rich.Plain(pair.Value).Bold() + " " + Rich.Plain(pair.Title).Ink(faint), 14, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
                    t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Box(x + bw + 6, top, PairLabelW, 16);
                }
            }
            // the slim rows: the crop list's last entries, picture, name and the picked bar's colour with its number (one 40 px row, not a 76 px tile row)
            foreach (var i in all.Where(x => x.Tone == PanelModel.SlimTone))
            {
                var row = Kit(col, i.Title, "meter-track"); Size(row, -1, 40);
                var r = row.rectTransform;
                var sp = Pic(r, i.Icon, 26); sp.anchorMin = sp.anchorMax = sp.pivot = new Vector2(0, 0.5f); sp.anchoredPosition = new Vector2(8, 0);
                OneLine(Label(r, i.Title, 14, PanelLook.Muted)).rectTransform.Box(44, 11, 150, 18);
                var x = 206f;
                foreach (var pair in i.Items ?? new List<Block>())
                {
                    Img(r, "Swatch", null, Hex(pair.Colour, PanelLook.Accent)).rectTransform.Box(x, 17, 18, PairH);
                    var t = RichLabel(r, "cropgrid", Rich.Plain(pair.Value).Bold() + " " + Rich.Plain(pair.Title).Ink(faint), 14, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
                    t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Box(x + 24, 11, PairLabelW, 18);
                    x += 24 + PairLabelW + 8;
                }
            }
            // the legend once: planted, picked (and what it includes), the faded chip
            var key = Line(col, 8);
            void Swatch(string hex, string word)
            {
                Size(Img(key, "Swatch", null, Hex(hex, PanelLook.Accent)), 18, PairH);
                Label(key, word, 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
                Size(Node("Gap", key), 6, 1);
            }
            Swatch(PanelModel.PlantedColour, PanelModel.PlantedWord);
            Swatch(PanelModel.PickedColour, PanelModel.PickedWord);
            if (!string.IsNullOrEmpty(b.Text)) Label(key, b.Text, 14, PanelLook.Faint, style: FontStyles.Italic).textWrappingMode = TextWrappingModes.NoWrap;
            if (b.Note == PanelModel.FadedKey) { Size(Node("Gap", key), 6, 1); FadedChip(key); }
            if (Labelled(b)) Since(col, 14, b);
        }
    }
}
