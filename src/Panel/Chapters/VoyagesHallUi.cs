using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Renderers for the Voyages and Hall chapter kinds (PanelModel, Chapters/VoyagesHallModel.cs): journey, crew, compass,
    /// biometiles. Ported from the approved proto forms (r2-voyages.js route and crew rows, r4voy.js sea route, foot line,
    /// compass and biome tiles). Flat rectangles from the kit's meter fill, Codex's sprites (compass-rose, compass-home,
    /// biome emblems), native icons; everything placed, sized and tinted once when the page is filled.
    /// </summary>
    public partial class PanelUi
    {
        // PersonTint (a player's colour by name) lives in CompanyUi.cs; both chapters share it

        // ----- ledger (fix2 7, the dense ledger of r4voy-hall-a): one compact row per station or trader, its name on the left,
        // its items as inline chips (picture, count, name; a thin edge in its colour; fuel marked) flowing right and wrapping;
        // a rule between rows, no tall tiles -----

        const float LedgerNameW = 150, LedgerChipH = 30, LedgerGap = 6, LedgerPic = 22, LedgerWideNameW = 230, LedgerTotalW = 84;

        static void Ledger(RectTransform col, Block b)
        {
            var rows = b.Items ?? new List<Block>();
            if (rows.Count == 0) return;
            var stack = VStack(col, 0);
            // the kitchen ledger (Cooking) adds a source line under a row's name (Text) and the row's total at the right (Value, Note)
            var lined = rows.Any(r => !string.IsNullOrEmpty(r.Text)); var totals = rows.Any(r => !string.IsNullOrEmpty(r.Value));
            // Hall's rows (Trader, Smelters) are bigger than the kitchen's (fix-rest: they left the lower half of the plate empty at 11 to 13 px)
            float chipH = lined ? LedgerChipH : 38, pic = lined ? LedgerPic : 28, nSize = lined ? 16 : 19, tSize = lined ? 13 : 15, rowSize = lined ? 16 : 19, nameBase = lined ? LedgerNameW : 180;
            var nameW = lined ? LedgerWideNameW : nameBase;
            var room = Column - nameW - 12 - (totals ? LedgerTotalW + 12 : 0);
            foreach (var r in rows)
            {
                var row = Line(stack, 12, TextAnchor.UpperLeft);
                row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 5, 5);
                if (lined)
                {
                    // live-polish (in game the left column was empty): TMP's ellipsis drops a whole line whose box is lower than the
                    // font's line, so the 18/15 px boxes under 17/14 px text showed nothing; each line gets its font's height (LineBox)
                    var name = VStack(row, 0); Size(name, nameW, -1);
                    Size(OneLine(Label(name, r.Title, 17, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Bold)), nameW, LineBox(17));
                    if (!string.IsNullOrEmpty(r.Text)) Size(OneLine(Label(name, r.Text, 14, PanelLook.Faint, align: TextAlignmentOptions.TopLeft)), nameW, LineBox(13));
                }
                else
                {
                    var head = Line(row, 7); Size(head, nameBase, chipH);
                    if (!string.IsNullOrEmpty(r.Colour)) Size(Fill(head, "Mark", Hex(r.Colour, PanelLook.Accent)), 4, 20);
                    Size(OneLine(Label(head, r.Title, rowSize, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft)), nameBase - (string.IsNullOrEmpty(r.Colour) ? 0 : 11), -1);
                }
                var flow = VStack(row, LedgerGap); flow.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false; Size(flow, room, -1);
                RectTransform line = null; float used = 0;
                foreach (var c in r.Items ?? new List<Block>())
                {
                    var chip = Kit(flow, c.Title, "meter-track").rectTransform;
                    var h = chip.gameObject.AddComponent<HorizontalLayoutGroup>(); Layout(h, 5, TextAnchor.MiddleLeft);
                    var padL = string.IsNullOrEmpty(c.Colour) ? 7 : 10; h.padding = new RectOffset(padL, 9, 0, 0);
                    float w = padL + 9;
                    if (!string.IsNullOrEmpty(c.Icon)) { Pic(chip, c.Icon, pic); w += pic + 5; }
                    var n = Label(chip, c.Value, nSize, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft); n.textWrappingMode = TextWrappingModes.NoWrap; w += n.preferredWidth + 5;
                    var t = Label(chip, c.Title, tSize, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap; w += t.preferredWidth;
                    if (c.Tone == PanelModel.FuelTone) { var f = Label(chip, PanelModel.FuelTone, 14, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic); f.textWrappingMode = TextWrappingModes.NoWrap; w += 5 + f.preferredWidth; }
                    if (Labelled(c)) { var s = Since(chip, 14, c); w += 5 + s.preferredWidth; }
                    w = Mathf.Min(Mathf.Ceil(w), room);
                    Size(chip, w, chipH);
                    Edge(chip, c.Colour);
                    if (line == null || used + LedgerGap + w > room) { line = Line(flow, LedgerGap); used = 0; } else used += LedgerGap;
                    chip.SetParent(line, false); used += w;
                }
                if (totals)
                {
                    var tot = VStack(row, 0); Size(tot, LedgerTotalW, chipH);
                    Size(Label(tot, r.Value, 24, PanelLook.Text, align: TextAlignmentOptions.MidlineRight, style: FontStyles.Bold), LedgerTotalW, 28);
                    if (!string.IsNullOrEmpty(r.Note)) Size(Label(tot, r.Note, 14, PanelLook.Faint, align: TextAlignmentOptions.TopRight), LedgerTotalW, 15);
                }
                if (!islanded) { var rule = Img(stack, "Rule", null, PanelLook.Rule); Size(rule, -1, 1); }   // no divider lines in an island (Joost, part 4)
            }
            if (Labelled(b)) Since(col, 14, b);
        }

        // ----- journey: how far did I travel? one line, legs by km; the leg's number over it, each segment's under it -----

        const float LegHead = 40, JourneyLine = 24, SegLabel = 42, LegGap = 4, Lead = 46;
        static readonly Color PassengerInk = new Color(0.50f, 0.83f, 0.82f);   // the passenger label: a light sea teal, as its bar (PanelModel.PassengerColour) and no player's blue

        static void Journey(RectTransform col, Block b)
        {
            var legs = (b.Items ?? new List<Block>()).Where(l => l.Fraction > 0 && (l.Items?.Any(s => s.Fraction > 0) ?? false)).ToList();
            if (legs.Count == 0) return;
            var heads = legs.Any(l => !string.IsNullOrEmpty(l.Value) || !string.IsNullOrEmpty(l.Title));
            var labels = legs.Any(l => l.Items.Any(s => !string.IsNullOrEmpty(s.Title)));
            var lead = PanelLook.Icon(b.Icon) != null ? Lead : 0f;   // a leg without a head: its picture in front of the line
            var area = Node("Journey", col); Size(area, -1, (heads ? LegHead : 0) + JourneyLine + (labels ? SegLabel : 0));
            if (lead > 0) Marker(area, b.Icon, 34, layout: false).Box(0, (heads ? LegHead : 0) + JourneyLine / 2 - 17, 34, 34);
            var room = Column - lead - LegGap * (legs.Count - 1);
            float x = lead, lineTop = heads ? LegHead : 0;
            var placed = new List<(RectTransform box, float start, float width)>();   // the segment labels, placed once all are measured
            foreach (var leg in legs)
            {
                var w = room * leg.Fraction / legs.Sum(l => l.Fraction);
                if (heads)
                {
                    var head = Node("Leg", area); head.Box(x, 0, Mathf.Max(w, 200), LegHead - 6);
                    Layout(head.gameObject.AddComponent<HorizontalLayoutGroup>(), 8, TextAnchor.LowerLeft);
                    if (!string.IsNullOrEmpty(leg.Icon)) Marker(head, leg.Icon, 28);
                    var v = Label(head, leg.Value, 28, PanelLook.Text, style: FontStyles.Bold); v.textWrappingMode = TextWrappingModes.NoWrap;
                    var t = Label(head, leg.Title, 16, PanelLook.Text); t.textWrappingMode = TextWrappingModes.NoWrap;
                }
                var segs = leg.Items.Where(s => s.Fraction > 0).ToList(); var sum = segs.Sum(s => s.Fraction); float sx = x;
                foreach (var s in segs)
                {
                    var sw = w * s.Fraction / sum;
                    var seg = Fill(area, "Segment", Hex(s.Colour, PanelLook.Accent)); seg.rectTransform.Box(sx, lineTop, Mathf.Max(2f, sw - 1), JourneyLine);
                    Marks(seg.rectTransform, s.Tone, sw - 1);
                    if (!string.IsNullOrEmpty(s.Title))
                    {
                        // under its own segment; never over a neighbour's (PanelModel.LabelPositions, after the loop)
                        var lab = VStack(area, 0); lab.Box(sx, lineTop + JourneyLine + 4, 140, SegLabel - 4);
                        var n = Label(lab, s.Value, 18, s.Id == "helm" ? PanelLook.Gold : s.Id == "passenger" ? PassengerInk : PanelLook.Text, style: FontStyles.Bold);
                        n.textWrappingMode = TextWrappingModes.NoWrap; Size(n, -1, 22);
                        var tt = Label(lab, s.Title, 14, PanelLook.Muted); tt.textWrappingMode = TextWrappingModes.NoWrap; Size(tt, -1, 16);
                        placed.Add((lab, sx, Mathf.Ceil(Mathf.Max(n.preferredWidth, tt.preferredWidth)) + 4));
                    }
                    sx += sw;
                }
                x += w + LegGap;
            }
            var at = PanelModel.LabelPositions(placed.Select(p => p.start).ToArray(), placed.Select(p => p.width).ToArray(), Column, 16);
            for (int i = 0; i < placed.Count; i++) placed[i].box.Box(at[i], lineTop + JourneyLine + 4, placed[i].width, SegLabel - 4);
            if (Labelled(b)) Since(col, 14, b);
        }

        // the line's texture, placed once: a foot path's ticks, a run's dash, the sea's crests (tiny rects, no curves)
        static void Marks(RectTransform seg, string tone, float w)
        {
            if (w < 12) return;
            var ink = new Color(0.95f, 0.89f, 0.71f, 0.85f);
            switch (tone)
            {
                case "dots":
                    for (int i = 0, n = Mathf.FloorToInt(w / 14); i < n; i++) Fill(seg, "Tick", ink).rectTransform.Box((i + 0.5f) * w / n - 1, 7, 3, JourneyLine - 14);
                    break;
                case "dash":
                    Fill(seg, "Dash", ink).rectTransform.Box(4, JourneyLine / 2 - 1, w - 8, 2);
                    break;
                case "crest":
                    for (int i = 0, n = Mathf.FloorToInt(w / 18); i < n; i++) Fill(seg, "Crest", new Color(0.86f, 0.93f, 1f, 0.8f)).rectTransform.Box((i + 0.5f) * w / n - 1.5f, JourneyLine / 2 - 1.5f, 3, 3);
                    break;
            }
        }

        // ----- crew: with whom did I sail? the fellow's shield, a bar of minutes in their colour, the minutes -----

        // fix-rest: a fellow is a cell (shield, name and minutes over a bar the cell's width), as many cells to a line as the column holds
        // (three across the plate, two in a half column): the three rows of 40 px took the whole bottom of Overview and Sailing off the fold
        const float CrewCell = 180, CrewGap = 16, CrewHead = 28, CrewBar = 16, CrewLineGap = 8;

        static void Crew(RectTransform col, Block b)
        {
            var people = b.Items ?? new List<Block>();
            var per = Mathf.Max(1, Mathf.Min(people.Count, Mathf.FloorToInt((Column + CrewGap) / (CrewCell + CrewGap))));
            var w = Mathf.Floor((Column - CrewGap * (per - 1)) / per);
            RectTransform line = null;
            for (int i = 0; i < people.Count; i++)
            {
                var p = people[i];
                if (i % per == 0) line = Row(col, CrewGap);
                var cell = VStack(line, 4); Size(cell, w, CrewHead + 4 + CrewBar + (i + per < people.Count ? CrewLineGap : 0));
                var head = Line(cell, 8); Size(head, -1, CrewHead);
                Marker(head, p.Icon, 26);
                var name = Label(head, p.Title, 19, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis; Size(name, -1, -1).flexibleWidth = 1;
                var v = Label(head, p.Value, 19, PanelLook.Text, align: TextAlignmentOptions.MidlineRight); v.textWrappingMode = TextWrappingModes.NoWrap; Size(v, Mathf.Ceil(v.preferredWidth) + 2, -1);
                var track = Kit(cell, "Track", "meter-track"); Size(track, -1, CrewBar);
                // whose helm, when the model knows it per fellow (Items: helm -> share); else the bar in the fellow's colour
                var helm = (p.Items ?? new List<Block>()).Where(h => h.Fraction > 0).ToList();
                if (p.Fraction > 0)
                {
                    var fill = Node("Fill", track.transform); fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Clamp01(p.Fraction), 1); fill.offsetMin = new Vector2(3, 3); fill.offsetMax = new Vector2(-3, -3);
                    if (helm.Count == 0) Fill(fill, "Part", PersonTint(p.Title)).rectTransform.Stretch();
                    else { float x = 0, all = helm.Sum(h => h.Fraction); foreach (var h in helm) { var r = Fill(fill, "Helm", Hex(h.Colour, PersonTint(h.Title))).rectTransform; r.anchorMin = new Vector2(x, 0); x += h.Fraction / all; r.anchorMax = new Vector2(x, 1); r.offsetMin = r.offsetMax = Vector2.zero; } }
                }
            }
            if (Labelled(b)) Since(col, 14, b);
        }

        // ----- compass: time at the far edge. Codex's rose (arm-free ring), four amber arms from home, names outside -----
        // The rose shrinks to fit a column (at most 200 px, its native 160 x 1.25): room for an 84 px name either side.

        const float RoseMax = 160, ArmW = 18, EdgeLabel = 42, SideLabel = 84, HomeR = 12;   // fix-rest: the rose at 160 (Maps must fit above the fold); the arms start at the home mark's edge, so a short one (South, 3 min) is not hidden behind it

        static void Compass(RectTransform col, Block b)
        {
            var arms = (b.Items ?? new List<Block>()).ToDictionary(a => a.Id ?? "", a => a);
            if (arms.Count == 0) return;
            float rose = Mathf.Clamp(Column - 2 * (SideLabel + 10), 120, RoseMax), armMax = rose * 0.30f;   // the ring's inside is 60 of its 80 px radius; 12 px of home mark and the longest arm stay inside it
            var area = Node("Compass", col); Size(area, -1, rose + 2 * EdgeLabel);
            float cx = Column / 2, cy = EdgeLabel + rose / 2;
            VocabImg(area, "Rose", "compass-rose", Color.white).rectTransform.Box(cx - rose / 2, cy - rose / 2, rose, rose);
            Block Arm(string id) => arms.TryGetValue(id, out var a) ? a : null;
            float Len(string id) => Mathf.Clamp01(Arm(id)?.Fraction ?? 0) * armMax;
            if (Len("north") > 0) Fill(area, "North", PanelLook.Dealt).rectTransform.Box(cx - ArmW / 2, cy - HomeR - Len("north"), ArmW, Len("north"));
            if (Len("south") > 0) Fill(area, "South", PanelLook.Dealt).rectTransform.Box(cx - ArmW / 2, cy + HomeR, ArmW, Len("south"));
            if (Len("east") > 0) Fill(area, "East", PanelLook.Dealt).rectTransform.Box(cx + HomeR, cy - ArmW / 2, Len("east"), ArmW);
            if (Len("west") > 0) Fill(area, "West", PanelLook.Dealt).rectTransform.Box(cx - HomeR - Len("west"), cy - ArmW / 2, Len("west"), ArmW);
            VocabImg(area, "Home", "compass-home", Color.white).rectTransform.Box(cx - 12, cy - 12, 24, 24);
            EdgeName(area, Arm("north"), cx - 100, 0, 200, TextAlignmentOptions.Top);
            EdgeName(area, Arm("south"), cx - 100, cy + rose / 2 + 2, 200, TextAlignmentOptions.Top);
            EdgeName(area, Arm("east"), cx + rose / 2 + 8, cy - EdgeLabel / 2, SideLabel, TextAlignmentOptions.TopLeft);
            EdgeName(area, Arm("west"), cx - rose / 2 - 8 - SideLabel, cy - EdgeLabel / 2, SideLabel, TextAlignmentOptions.TopRight);
        }

        static void EdgeName(RectTransform area, Block arm, float x, float top, float w, TextAlignmentOptions align)
        {
            if (arm == null) return;
            var name = Label(area, arm.Title, 15, PanelLook.Text, align: align); name.rectTransform.Box(x, top, w, 20); name.textWrappingMode = TextWrappingModes.NoWrap;
            var v = Label(area, arm.Value, 16, PanelLook.Gold, style: FontStyles.Bold, align: align); v.rectTransform.Box(x, top + 20, w, 22); v.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // ----- biometiles: the biomes found, one tile each in its colour with Codex's emblem; wraps at the column's edge -----

        const float BioW = 140, BioH = 38, BioGap = 6;   // five to a line on the plate (741 px)

        static void BiomeTiles(RectTransform col, Block b)
        {
            var tiles = (b.Items ?? new List<Block>()).ToList();
            if (tiles.Count == 0) return;
            var perLine = Mathf.Max(1, Mathf.FloorToInt((Column + BioGap) / (BioW + BioGap)));
            var lines = (tiles.Count + perLine - 1) / perLine;
            var area = Node("Biomes", col); Size(area, -1, lines * BioH + (lines - 1) * BioGap);
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                var tile = Fill(area, "Tile", Hex(t.Colour, PanelLook.BiomeTile(t.Id)));
                tile.rectTransform.Box(i % perLine * (BioW + BioGap), i / perLine * (BioH + BioGap), BioW, BioH);
                VocabImg(tile.transform, "Emblem", VocabName(t.Icon) ?? PanelLook.BiomeEmblem(t.Id), Color.white).rectTransform.Box(8, (BioH - 30) / 2, 30, 30);
                var ink = t.Tone == "dark-text" ? DarkInk : t.Tone == "light-text" ? LightInk : PanelLook.BiomeInk(t.Id);
                var name = Label(tile.transform, t.Title, 15, ink, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft);
                name.rectTransform.Box(44, 0, BioW - 48, BioH); name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        // the biomes your character found: PanelUi.KnownBiomes (one gatherer), read through PanelModel.FoundBiomes
    }
}
