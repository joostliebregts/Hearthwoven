using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Renderers of the general kinds the Deeds pages brought in (Chapters/DeedsModel.cs), ported from the approved proto
    /// pages r4deeds-cooking, r2-deeds-building, r4deeds-crafting, r4deeds-farming, r4deeds-fishing and r4over-taming:
    /// ranking (picture, name, bar, number; one or two columns), counts (tiles of picture, big number, name, with an
    /// optional proportional bar under them), itemgrid (every item as a small tile, most first, the full list), strip (one line of
    /// picture, number, name), grades (a picture that grows per grade, the count above it), band (a title's line) and people
    /// (a fellow's shield and the things of yours they put to good use). Rects, the kit's sprites and the game's own icons
    /// only, placed and sized once when the page is filled.
    /// </summary>
    public partial class PanelUi
    {
        const float RankRowH = 34, RankPic = 28, RankValueW = 56;

        // a number column wide enough for the longest number of the block ("6,399", "128,450"), never narrower than min
        static float NumberWidth(IEnumerable<string> values, float perChar, float min) => Mathf.Max(min, perChar * values.Select(v => (v ?? "").Length).DefaultIfEmpty(0).Max() + 6);

        // the game's icon for a reference; a placed piece is known by its name token ("piece:$piece_woodwall"), which the
        // game data (GameData.PieceIcon) resolves, every other reference goes through Marker
        static RectTransform Pic(RectTransform parent, string icon, float size)
        {
            if (icon != null && icon.StartsWith("piece:$"))
            {
                var box = Node("Marker", parent); Size(box, size, size); box.sizeDelta = new Vector2(size, size);   // its size also outside a layout (an item tile: fix2 4, the picture covered the number)
                var s = GameData.PieceIcon(icon.Substring(6));
                if (s) Img(box, "Icon", s, Color.white).rectTransform.Stretch();
                return box;
            }
            return Marker(parent, icon, size);
        }

        // a bar on the kit's meter track: the fill inset 3 px, its width the fraction of the track
        static Image Meter(RectTransform parent, float fraction, Color colour, float height)
        {
            var track = Kit(parent, "Track", "meter-track"); Size(track, -1, height).flexibleWidth = 1;
            if (fraction > 0)
            {
                var r = Fill(track.transform, "Fill", colour).rectTransform;
                r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1); r.offsetMin = new Vector2(3, 3); r.offsetMax = new Vector2(-3, -3);
            }
            return track;
        }

        // a small head over a block: its title in gold and its total ("Pieces built 866"), "since install" when the model says so
        static void Head(RectTransform col, Block b)
        {
            if (string.IsNullOrEmpty(b.Title) && string.IsNullOrEmpty(b.Value)) return;
            var head = Line(col, 8);
            if (!string.IsNullOrEmpty(b.Title)) Label(head, b.Title, 18, PanelLook.Gold);
            if (!string.IsNullOrEmpty(b.Value)) Label(head, b.Value, 18, PanelLook.Text, style: FontStyles.Bold);
            if (b.SinceInstall) Since(head, 14);
        }

        static TextMeshProUGUI OneLine(TextMeshProUGUI t) { t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis; return t; }
        /// <summary>The height a one-line box needs for text of <paramref name="size"/> (the MinText floor applied): TMP's ellipsis drops a whole
        /// line whose box is lower than the font's line (about 1.16 em in the game's serif), so a box never goes under 1.3 em (live-polish).</summary>
        static float LineBox(float size) => Mathf.Ceil(Mathf.Max(size, PanelLook.MinText) * 1.3f);

        // ----- ranking: what most? -----

        static void Ranking(RectTransform col, Block b)
        {
            Head(col, b);
            var items = b.Items ?? new List<Block>();
            if (items.Count == 0) return;
            var columns = Math.Max(1, Math.Min(b.Columns, items.Count));
            var pictures = items.Any(i => !string.IsNullOrEmpty(i.Icon));
            var since = items.Any(i => i.SinceInstall);
            var tightRows = b.Tone == PanelModel.ZoneTight; float gap = tightRows ? 44 : 28;   // three across in Woodcutting: a wider gap so a number never leans on the next picture
            var colW = (Column - gap * (columns - 1)) / columns;
            var valueW = NumberWidth(items.Select(i => i.Value), 10.5f, b.Tone == PanelModel.ZoneTight ? 30f : RankValueW);
            var tight = b.Tone == PanelModel.ZoneTight;   // Woodcutting's rows: three across, a narrower name, closer rows
            var nameW = tight ? Mathf.Clamp(colW * 0.4f, 64f, columns < 3 ? 170f : 110f) : Mathf.Clamp(colW * 0.5f, 120f, 220f);   // a name column that leaves the bar room in a narrow column
            var holder = columns > 1 ? Line(col, gap, TextAnchor.UpperLeft) : col;
            var per = (items.Count + columns - 1) / columns;
            for (int c = 0; c < columns; c++)
            {
                var stack = VStack(holder, tight ? 2 : 4);
                if (columns > 1) Size(stack, colW, -1);
                var names = new List<TextMeshProUGUI>(); var values = new List<TextMeshProUGUI>(); var bars = new List<Image>();
                foreach (var i in items.Skip(c * per).Take(per))
                {
                    var row = Line(stack, columns > 1 ? 6 : 8); Size(row, -1, tight ? RankRowH - 4 : RankRowH);
                    if (pictures) Pic(row, i.Icon, RankPic);
                    var nm = OneLine(Label(row, i.Title, 17, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft)); Size(nm, nameW, -1); names.Add(nm);
                    bars.Add(Meter(row, i.Fraction, string.IsNullOrEmpty(i.Colour) && (i.Icon ?? "").StartsWith("person:") ? PanelLook.PersonColor(personColors.TryGetValue(i.Id ?? "", out var personSlot) ? personSlot : 0) : Hex(i.Colour, PanelLook.Dealt), 16));   // a fellow's bar in their player colour (Cooking: who enjoyed your food)
                    var v = Label(row, i.Value, 17, PanelLook.Text, align: columns > 1 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight, style: FontStyles.Bold); Size(v, valueW, -1); values.Add(v);
                    if (since) { var slot = Node("Since", row); Size(slot, 84, 20); if (i.SinceInstall) Since(slot, 13).rectTransform.Stretch(); }
                }
                // live-polish (Joost in game: "Hooked [bar] 20   Got away [bar] 8" spread over the plate): side by side, a row is one unit,
                // name, bar and number close together (the name and number as wide as this column's longest), the room left over is the
                // gap before the next column. A single column keeps its table (names, bars and numbers aligned down the page)
                if (columns > 1 && names.Count > 0)
                {
                    var nw = Mathf.Min(nameW, Mathf.Ceil(names.Max(t => t.preferredWidth)) + 2);
                    var vw = Mathf.Ceil(values.Max(t => { t.textWrappingMode = TextWrappingModes.NoWrap; return t.preferredWidth; })) + 2;
                    var bw = Mathf.Clamp(colW - (pictures ? RankPic + 6 : 0) - nw - vw - 12 - (since ? 90 : 0), 48f, RankBarSide);
                    foreach (var t in names) Size(t, nw, -1);
                    foreach (var t in values) Size(t, vw, -1);
                    foreach (var m in bars) { var le = Size(m, bw, -1); le.flexibleWidth = 0; }
                }
            }
        }

        const float RankBarSide = 120;   // a bar beside its name and number when rows stand side by side (the HTML bridge: .rk.two)

        // ----- counts: one tile per kind, and the kinds' proportional bar under them when they carry colours -----

        static void Counts(RectTransform col, Block b)
        {
            var items = b.Items ?? new List<Block>();
            if (items.Count == 0) return;
            var pictures = items.Any(i => !string.IsNullOrEmpty(i.Icon));
            var large = items.Count <= 3;
            var framed = b.Tone == "framed";   // Taming (r4over-taming): a thin single frame, each picture in its own small framed square
            float pic = large ? 96 : 46, number = large ? 44 : 32, height = b.Tone == "compact" ? 56 : framed ? (items.Any(i => !string.IsNullOrEmpty(i.Note)) ? 98 : 88) : (pictures ? pic + 12 + (framed ? 16 : 0) : 0) + (large ? 112 : 92);
            var row = Line(col, 10, TextAnchor.UpperLeft);
            foreach (var i in items)
            {
                var tile = Kit(row, i.Title, framed ? "meter-track" : "slot"); var tl = Size(tile, -1, height); tl.flexibleWidth = 1; tl.minWidth = 0;
                if (b.Tone == "compact")   // picture, number and name on one line (Crafting's kinds): half the height of the tall tile
                {
                    var one = Line(tile.rectTransform, 10, TextAnchor.MiddleLeft); one.Stretch(); one.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(12, 8, 4, 10);
                    if (pictures) Pic(one, i.Icon, 36);
                    var cn = Label(one, i.Value, 26, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft); cn.textWrappingMode = TextWrappingModes.NoWrap;
                    OneLine(Label(one, i.Title, 16, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft));
                }
                else if (framed)   // Taming (fix3): the picture in its small frame, the number and its name beside it: a third of the height of the tall tiles
                {
                    var one = Line(tile.rectTransform, 16, TextAnchor.MiddleLeft); one.Stretch(); one.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(14, 10, 8, 8);
                    if (pictures)
                    {
                        var square = Kit(one, "Frame", "meter-track"); Size(square, 70, 70);
                        var m = Pic(square.rectTransform, i.Icon, 54); m.anchorMin = m.anchorMax = m.pivot = new Vector2(0.5f, 0.5f); m.anchoredPosition = Vector2.zero;
                    }
                    var words = VStack(one, 0); words.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                    var fn = Label(words, i.Value, 38, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Bold); Size(fn, -1, 42);
                    if (i.SinceInstall) Size(Since(words, 13), -1, 16);
                    Size(OneLine(Label(words, i.Title, 18, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft)), -1, 24);
                    if (!string.IsNullOrEmpty(i.Note)) Size(OneLine(Label(words, i.Note, PanelLook.MinText, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft)), -1, LineBox(PanelLook.MinText));   // zones-wording: "in all · 7 of them since install"
                }
                else
                {
                    var stack = VStack(tile.rectTransform, 2); stack.Stretch();
                    var v = stack.GetComponent<VerticalLayoutGroup>(); v.childAlignment = TextAnchor.MiddleCenter; v.padding = new RectOffset(6, 6, 10, 10);
                    if (pictures) Pic(stack, i.Icon, pic);
                    var n = Label(stack, i.Value, number, PanelLook.Text, align: TextAlignmentOptions.Center, style: FontStyles.Bold); Size(n, -1, number + 4);
                    if (i.SinceInstall) Size(Since(stack, 13), -1, 16).GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
                    Size(OneLine(Label(stack, i.Title, large ? 19 : 16, large ? PanelLook.Text : PanelLook.Muted, align: TextAlignmentOptions.Center)), -1, 24);
                }
                if (!string.IsNullOrEmpty(i.Colour))   // the kind's colour as the tile's bottom edge
                {
                    var edge = Fill(tile.transform, "Edge", Hex(i.Colour, PanelLook.Accent)).rectTransform;
                    edge.anchorMin = new Vector2(0, 0); edge.anchorMax = new Vector2(1, 0); edge.pivot = new Vector2(0.5f, 0); edge.offsetMin = new Vector2(2, 2); edge.offsetMax = new Vector2(-2, 6);
                    edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                }
            }
            if (items.All(i => !string.IsNullOrEmpty(i.Colour) && i.Fraction > 0))
            {
                Spacer(col, 2);
                var bar = Kit(col, "Kinds", "meter-track"); Size(bar, -1, 28);
                float x = 0;
                for (int k = 0; k < items.Count; k++)
                {
                    var r = Fill(bar.transform, "Part", Hex(items[k].Colour, PanelLook.Accent)).rectTransform; var w = items[k].Fraction;
                    r.anchorMin = new Vector2(x, 0); r.anchorMax = new Vector2(Mathf.Min(1, x + w), 1);
                    r.offsetMin = new Vector2(k == 0 ? 3 : 1, 3); r.offsetMax = new Vector2(k == items.Count - 1 ? -3 : -1, -3);
                    x += w;
                }
            }
        }

        // ----- itemgrid: every item as a compact tile (fix2 6, Joost in game: ~110 px for one number was far too tall): the
        // picture left, the count big with the name small under it on the right (a long name wraps to a second line, never
        // "..."), an optional quieter second number and "since install" under those. As many tiles per line as the column
        // holds at about 170 px: four across the plate, two in a half column. The full list, most first; the page scrolls. -----

        const float GridTileW = PanelModel.ItemTileMinWidth, GridGap = PanelModel.ItemTileGap, GridPic = 32, GridTileH = 46, GridLine = 14;

        static void ItemGrid(RectTransform col, Block b)
        {
            var items = b.Items ?? new List<Block>();
            if (items.Count == 0) return;
            var second = items.Any(i => !string.IsNullOrEmpty(i.Value2));
            var since = items.Any(i => i.SinceInstall);
            var per = PanelModel.ItemTilesPerLine(Column);
            var w = Mathf.Floor((Column - GridGap * (per - 1)) / per);
            var textW = w - (8 + GridPic + 8) - 6;
            var grid = Node("ItemGrid", col);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.spacing = new Vector2(GridGap, GridGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = per;
            var wraps = false;
            foreach (var i in items)
            {
                var tile = Kit(grid, i.Title, "meter-track").rectTransform;   // the dark ground and edge of the overview cards, smaller
                var p = Pic(tile, i.Icon, GridPic); p.anchorMin = p.anchorMax = p.pivot = new Vector2(0, 0.5f); p.anchoredPosition = new Vector2(8, 0);
                var box = VStack(tile, 0);
                box.anchorMin = Vector2.zero; box.anchorMax = Vector2.one; box.offsetMin = new Vector2(8 + GridPic + 8, 3); box.offsetMax = new Vector2(-6, -3);
                box.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                var n = Label(box, i.Value, 19, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Bold); n.textWrappingMode = TextWrappingModes.NoWrap;
                var name = Label(box, i.Title, 12, PanelLook.Muted, align: TextAlignmentOptions.TopLeft);
                name.lineSpacing = -12; name.maxVisibleLines = 2; name.overflowMode = TextOverflowModes.Ellipsis;   // two short lines at most
                if (name.preferredWidth > textW) wraps = true;
                if (i.Tone == PanelModel.FuelTone) FuelMark(name, i.Title);   // Hall: a fuel item says so (fix2 7)
                Edge(tile, i.Colour);   // its trader's or its part's colour, a thin edge on the left (fix2 7)
                if (!string.IsNullOrEmpty(i.Value2))
                {
                    var more = OneLine(Label(box, (Layered(i) ? LayeredText(i, PanelLook.Faint, PanelLook.Text) : i.Value2) + " " + i.Text, 11, PanelLook.Faint));
                    more.richText = Layered(i);
                }
                if (i.SinceInstall) Since(box, 10);
            }
            g.cellSize = new Vector2(w, GridTileH + (wraps ? GridLine : 0) + (second ? GridLine : 0) + (since ? GridLine : 0));
            if (b.SinceInstall) Since(col, 13);
        }

        // a thin edge in a colour down the left of a tile or chip (Hall: an item's trader, a smelted good's part of the bar)
        static void Edge(RectTransform tile, string colour)
        {
            if (string.IsNullOrEmpty(colour)) return;
            var e = Fill(tile, "Edge", Hex(colour, PanelLook.Accent)).rectTransform;
            e.anchorMin = Vector2.zero; e.anchorMax = new Vector2(0, 1); e.pivot = new Vector2(0, 0.5f); e.offsetMin = new Vector2(2, 3); e.offsetMax = new Vector2(5, -3);
            e.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        // the name of a fuel item, then "fuel" small and faint after it (the name kept literal)
        static void FuelMark(TextMeshProUGUI name, string title)
        {
            name.richText = true;
            name.text = "<noparse>" + (title ?? "").Replace("</noparse>", "") + "</noparse> <size=" + PanelLook.MinText.ToString(System.Globalization.CultureInfo.InvariantCulture) + "><i><color=#" + ColorUtility.ToHtmlStringRGB(PanelLook.Faint) + ">" + PanelModel.FuelTone + "</color></i></size>";
        }

        // ----- strip: one line of picture, number, name; wraps at the column's edge. An entry with an Id is a shortcut
        // to that page (the names earned on the Deeds overview) -----

        static void Strip(RectTransform col, Block b, Func<string, Action> link)
        {
            var items = b.Items ?? new List<Block>();
            var lines = VStack(col, 6); lines.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            RectTransform line = null; float used = 0;
            foreach (var i in items)
            {
                var entry = Line(lines, 7); float w = 0;
                if (!string.IsNullOrEmpty(i.Icon)) { Pic(entry, i.Icon, 28); w += 28 + 7; }
                if (!string.IsNullOrEmpty(i.Value)) { var n = Label(entry, Layered(i) ? LayeredText(i, PanelLook.Faint, null, 19) : i.Value, 19, PanelLook.Text, style: FontStyles.Bold); n.richText = Layered(i); n.textWrappingMode = TextWrappingModes.NoWrap; w += n.preferredWidth + 7; }
                var named = string.IsNullOrEmpty(i.Value);   // a name without a number (a title earned): the name speaks, its place (Text) quiet after it
                var t = Label(entry, i.Title, named ? 18 : 15, named ? PanelLook.Text : PanelLook.Muted); t.textWrappingMode = TextWrappingModes.NoWrap;
                w += t.preferredWidth;
                if (!string.IsNullOrEmpty(i.Text)) { var x = Label(entry, i.Text, 13, PanelLook.Muted); x.textWrappingMode = TextWrappingModes.NoWrap; w += 7 + x.preferredWidth; }
                if (i.SinceInstall) { var s = Since(entry, 13); w += 7 + s.preferredWidth; }
                if (line == null || used + 26 + w > Column) { line = Line(lines, 26); used = 0; } else used += 26;
                entry.SetParent(line, false); used += w;
                Size(entry, w, 32);
                var click = string.IsNullOrEmpty(i.Id) || i.Id.IndexOf('/') < 0 ? null : link?.Invoke(i.Id);
                if (click == null) continue;
                var hit = entry.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
                var button = entry.gameObject.AddComponent<Button>(); button.targetGraphic = hit; button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => click());
            }
        }

        // ----- grades: the count above a picture that grows with each grade, the grade beneath (fish quality 1..6) -----

        static void Grades(RectTransform col, Block b)
        {
            var items = b.Items ?? new List<Block>();
            if (items.Count == 0) return;
            var row = Line(col, 0, TextAnchor.LowerCenter); Size(row, -1, 104);
            for (int k = 0; k < items.Count; k++)
            {
                var i = items[k];
                var cell = VStack(row, 4); Size(cell, -1, 104).flexibleWidth = 1;
                var v = cell.GetComponent<VerticalLayoutGroup>(); v.childAlignment = TextAnchor.LowerCenter; v.childForceExpandWidth = false;
                Size(Label(cell, i.Value, 17, PanelLook.Text, align: TextAlignmentOptions.Center, style: FontStyles.Bold), 60, 22);
                Pic(cell, i.Icon, 22 + k * 6);
                Size(Label(cell, i.Title, 15, PanelLook.Muted, align: TextAlignmentOptions.Center), 60, 20);
            }
            var rule = Img(col, "Rule", null, PanelLook.Rule); Size(rule, -1, 1);
        }

        // ----- band: a title earned on this page, its name and its line -----

        static void Band(RectTransform col, Block b)
        {
            var bg = Kit(col, "Band", "meter-track"); Size(bg, -1, 58);   // a thin single frame (r4over-taming)
            var h = bg.gameObject.AddComponent<HorizontalLayoutGroup>(); Layout(h, 14, TextAnchor.MiddleLeft); h.padding = new RectOffset(18, 20, 0, 0);
            Marker((RectTransform)bg.transform, b.Icon, 34);
            Label(bg.transform, b.Title, 21, PanelLook.Gold, align: TextAlignmentOptions.MidlineLeft);
            Node("Rest", bg.transform).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Label(bg.transform, b.Value, 17, PanelLook.Text, align: TextAlignmentOptions.MidlineRight);
            if (b.SinceInstall) Since((RectTransform)bg.transform, 13);
        }

        // ----- people: a fellow's shield and name, then the things they put to good use, by picture and name -----

        static void People(RectTransform col, Block b)
        {
            foreach (var p in b.Items ?? new List<Block>())
            {
                var row = Line(col, 14); Size(row, -1, 60);
                Marker(row, p.Icon, 38);
                Size(OneLine(Label(row, p.Title, 20, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft)), 120, -1);
                foreach (var i in p.Items ?? new List<Block>())
                {
                    var item = Line(row, 8);
                    Pic(item, i.Icon, 34);
                    OneLine(Label(item, i.Title, 17, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft));
                    if (i.SinceInstall) Since(item, 13);
                }
                Node("Rest", row).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                var rule = Img(col, "Rule", null, PanelLook.Rule); Size(rule, -1, 1);
            }
        }
    }
}
