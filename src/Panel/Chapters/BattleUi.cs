using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Battle chapter renderers (ch-battle): the block kinds of Chapters/BattleModel.cs, ported from the approved prototypes
    /// (proto/r4battle.js + r4battle.css: damage grid, weapon bars, foe table, damage-type chips, defence; r2-voyages.js +
    /// css: where you fell, the last 10 seconds). Ready sprites only, placed once when the page is filled: Codex's dmg-*,
    /// weapon-*, block-mark, parry-spark, immunity-stamp and death masks tinted with the palette (VOCABULARY.md), the game's
    /// own trophy and arrow icons, the kit's meter fill for bars; nothing per frame.
    /// </summary>
    public partial class PanelUi
    {
        // r4battle.css: weak = light warm cell, resistant = dimmed, immune = the stamp over the icon, normal = the type's colour
        // battle-answers: weakness is a cream cell with a "+" (gold stays "damage dealt" in the whole chapter, and "chosen" on chips)
        static readonly Color BtWeak = new Color32(0xf3, 0xec, 0xdc, 0xff), BtResist = new Color32(0x8a, 0x86, 0x80, 0xff), BtImmune = new Color32(0xe0, 0x82, 0x6a, 0xff),
            BtNormal = new Color32(0xf3, 0xec, 0xdc, 0xff), BtWeakChip = new Color32(0xb3, 0xae, 0xa4, 0xff), BtWeakInk = new Color32(0x2a, 0x1d, 0x10, 0xff), BtCell = new Color(0f, 0f, 0f, 0.30f),
            BtDealtBar = new Color32(0xa9, 0xa0, 0x8c, 0xff), BtDark = new Color(0f, 0f, 0f, 0.45f), BtLine = new Color(0.42f, 0.32f, 0.2f, 0.28f), BtTrack = new Color(0.42f, 0.32f, 0.2f, 0.45f), BtSlot = new Color(0.17f, 0.13f, 0.098f, 1f);

        /// <summary>Draws one Battle block (DrawVocab hands these kinds over).</summary>
        internal static void BattleBlock(RectTransform col, Block b, Func<string, Action> link)
        {
            switch (b.Kind)
            {
                case "damagegrid": BattleGrid(col, b); break;
                case "dmgmix": BattleMix(col, b); break;
                case "foetable": BattleFoeTable(col, b); break;
                case "foetypes": BattleFoeTypes(col, b); break;
                case "guard": BattleGuard(col, b); break;
                case "sources": BattleSources(col, b); break;
                case "deathstrip": BattleDeathStrip(col, b); break;
                case "deaths": BattleDeathList(col, b); break;
                case "deathrows": BattleDeathRows(col, b); break;
            }
            if (b.SinceInstall) Since(col, 13);   // a page without a time window: counted on this PC since install
        }

        // ----- small absolute-layout helpers: a row of fixed height in the column, its children placed by Box -----

        static RectTransform BtRow(RectTransform col, float h) { var r = Node("Row", col); Size(r, -1, h); return r; }

        static TextMeshProUGUI BtText(RectTransform parent, string text, float x, float top, float w, float h, float size, Color colour,
                                      TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, FontStyles style = FontStyles.Normal)
        {
            var t = Label(parent, text, size, colour, align: align, style: style);
            // live-polish: a box lower than the font's line loses the whole line to the ellipsis (TMP); grow it evenly round its middle
            var need = LineBox(size); if (h < need) { top -= (need - h) / 2; h = need; }
            t.rectTransform.Box(x, top, w, h); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        /// <summary>BtText for <see cref="Rich"/> text: rich text on for the block kinds PanelRich lists (richtext-fix; see RichLabel).</summary>
        static TextMeshProUGUI BtRich(RectTransform parent, string kind, Rich text, float x, float top, float w, float h, float size, Color colour,
                                      TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, FontStyles style = FontStyles.Normal)
        {
            var t = BtText(parent, text.Markup, x, top, w, h, size, colour, align, style);
            t.richText = PanelRich.On(kind);
            return t;
        }

        static Image BtRect(RectTransform parent, string name, Color colour, float x, float top, float w, float h)
        {
            var i = Img(parent, name, null, colour); i.rectTransform.Box(x, top, w, h); return i;
        }

        static Image BtIcon(RectTransform parent, string sprite, Color tint, float x, float top, float size)
        {
            var i = VocabImg(parent, "Icon", VocabName(sprite) ?? "", tint); i.rectTransform.Box(x, top, size, size); return i;
        }

        static void BtMarker(RectTransform parent, string icon, float x, float top, float size)
        {
            if (string.IsNullOrEmpty(icon)) return;
            Marker(parent, icon, size, layout: false).Box(x, top, size, size);
        }

        static Color BtColour(Block b) => Hex(b.Colour, PanelLook.Accent);

        // a section-style caption in small capitals (the prototypes' .sect), with an optional quiet qualifier
        static void BtCaption(RectTransform col, string title, string qualifier)
        {
            var head = Line(col, 8);
            var t = Label(head, (title ?? "").ToUpperInvariant(), 14, PanelLook.Muted); t.characterSpacing = 8; t.textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(qualifier)) { var q = Label(head, qualifier, PanelLook.MinText, PanelLook.Muted); q.textWrappingMode = TextWrappingModes.NoWrap; }
        }

        // ----- Damage, by type: types down, weapon kinds across, one bar per cell on one scale, totals right and under -----

        const float BtTypeW = 130, BtTotalW = 80, BtGridRow = 26, BtCellBar = 150;

        static void BattleGrid(RectTransform col, Block b)
        {
            var items = b.Items ?? new List<Block>();
            var head = items.FirstOrDefault(i => i.Kind == "weapons");
            var weapons = head?.Items ?? new List<Block>();
            var types = items.Where(i => i.Kind == "dmgtype").ToList();
            if (weapons.Count == 0 || types.Count == 0) return;
            var cw = (Column - BtTypeW - BtTotalW) / weapons.Count;

            var h = BtRow(col, BtGridRow);
            for (int k = 0; k < weapons.Count; k++)
            {
                var x = BtTypeW + k * cw; var idle = weapons[k].Tone == "idle";   // a weapon with nothing in this window keeps its column, dim
                var ink = idle ? PanelLook.Faint : PanelLook.Muted;
                BtIcon(h, weapons[k].Icon, ink, x, 4, 20);
                BtText(h, weapons[k].Title, x + 26, 0, cw - 26, BtGridRow, 14, ink);
            }
            BtText(h, head.Title, Column - BtTotalW, 0, BtTotalW, BtGridRow, 17, PanelLook.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            BtRect(h, "Rule", PanelLook.Rule, 0, BtGridRow - 1, Column, 1);

            foreach (var t in types)
            {
                var colour = BtColour(t);
                var r = BtRow(col, BtGridRow);
                BtIcon(r, t.Icon, colour, 0, 5, 18);
                BtText(r, t.Title, 26, 0, BtTypeW - 26, BtGridRow, 17, Legible(colour));
                var cells = t.Items ?? new List<Block>();
                for (int k = 0; k < weapons.Count && k < cells.Count; k++)
                {
                    if (string.IsNullOrEmpty(cells[k].Value)) continue;
                    float x = BtTypeW + k * cw, w = Mathf.Max(3f, Mathf.Clamp01(cells[k].Fraction) * Mathf.Min(BtCellBar, cw - 56));
                    Fill(r, "Bar", colour).rectTransform.Box(x, 8, w, 12);
                    BtText(r, cells[k].Value, x + w + 8, 0, Mathf.Max(40, cw - w - 8), BtGridRow, 16, PanelLook.Text, style: FontStyles.Bold);
                }
                BtText(r, t.Value, Column - BtTotalW, 0, BtTotalW, BtGridRow, 17, PanelLook.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                BtRect(r, "Rule", BtLine, 0, BtGridRow - 1, Column, 1);
            }

            var f = BtRow(col, BtGridRow + 4);
            BtRect(f, "Rule", PanelLook.Rule, 0, 0, Column, 1);
            BtText(f, b.Title, 0, 2, BtTypeW, BtGridRow, 17, PanelLook.Text, style: FontStyles.Bold);
            for (int k = 0; k < weapons.Count; k++) BtText(f, weapons[k].Tone == "idle" ? "\u2013" : weapons[k].Value, BtTypeW + k * cw, 2, cw, BtGridRow, 17, weapons[k].Tone == "idle" ? PanelLook.Faint : PanelLook.Text, style: FontStyles.Bold);
            BtText(f, b.Value, Column - BtTotalW, 2, BtTotalW, BtGridRow, 17, PanelLook.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        }

        // a damage colour as text on the plate: lifted toward white until it reads at 5:1 (the palette stays on the bars and icons)
        static Color Legible(Color c)
        {
            float F(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            float Lum(Color x) => 0.2126f * F(x.r) + 0.7152f * F(x.g) + 0.0722f * F(x.b);
            for (int i = 0; i < 12 && (Lum(c) + 0.05f) / 0.062f < 5f; i++) c = Color.Lerp(c, Color.white, 0.12f);
            return c;
        }

        // ----- Damage, by weapon: a composition bar per weapon kind; the legend carries each type's tinted icon -----

        static void BattleMix(RectTransform col, Block b)
        {
            var head = Line(col, 8);
            Size(VocabImg(head, "Weapon", VocabName(b.Icon) ?? "", PanelLook.Gold), 22, 22);
            var name = Label(head, (b.Title ?? "").ToUpperInvariant(), 14, PanelLook.Muted); name.characterSpacing = 8; name.textWrappingMode = TextWrappingModes.NoWrap;
            Label(head, b.Value, 15, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            var parts = (b.Items ?? new List<Block>()).Where(p => p.Fraction > 0).ToList();
            if (parts.Count == 0) return;
            var shares = parts.Select(p => Mathf.Max(p.Fraction, 4f / Column)).ToList(); var sum = shares.Sum();
            var bar = Kit(col, "Bar", "meter-track"); Size(bar, -1, 26);
            if (b.Fraction > 0 && b.Fraction < 1) { var le = bar.GetComponent<LayoutElement>(); le.flexibleWidth = 0; le.preferredWidth = le.minWidth = Mathf.Max(40f, Column * b.Fraction); }   // one scale for the three weapons: the bar is as long as its total against the largest
            float x = 0;
            for (int k = 0; k < parts.Count; k++)
            {
                var r = Fill(bar.transform, "Part", BtColour(parts[k])).rectTransform; var w = shares[k] / sum;
                r.anchorMin = new Vector2(x, 0); r.anchorMax = new Vector2(x + w, 1); r.pivot = new Vector2(0, 0.5f);
                r.offsetMin = new Vector2(k == 0 ? 3 : 1, 3); r.offsetMax = new Vector2(k == parts.Count - 1 ? -3 : -1, -3);
                x += w;
            }
            // legend: swatch, the type's icon in its colour, number, name; entries wrap at the column's edge
            var lines = VStack(col, 4); lines.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            RectTransform line = null; float used = 0;
            foreach (var p in parts)
            {
                var c = BtColour(p);
                var entry = Line(lines, 6);
                var w = 0f;   // the type's icon in its colour is the swatch: one mark per entry, so the legend fits one line
                if (!string.IsNullOrEmpty(p.Icon)) { Size(VocabImg(entry, "Type", VocabName(p.Icon), c), 16, 16); w += 16; }
                else { Size(Fill(entry, "Swatch", c), 10, 10); w += 10; }
                var n = Label(entry, p.Value, 18, PanelLook.Text, style: FontStyles.Bold); n.textWrappingMode = TextWrappingModes.NoWrap;
                var t = Label(entry, p.Title, 15, PanelLook.Muted); t.textWrappingMode = TextWrappingModes.NoWrap;
                w += 6 + n.preferredWidth + 6 + t.preferredWidth;
                if (line == null || used + 16 + w > Column) { line = Line(lines, 16); used = 0; } else used += 16;
                entry.SetParent(line, false); used += w;
                Size(entry, w, 28);
            }
            Spacer(col, 6);
        }

        // ----- Foes, by foe: trophy and name, the dealt bar, the best arrow, one effectiveness cell per damage type -----

        const float BtFoeName = 176, BtFoeDealt = 100, BtFoeArrow = 134, BtFoeRow = 36, BtFoeHead = 84, BtCellW = 42, BtCellH = 30, BtFoeTilt = 40;   // live-polish: a wider foe column ("Greydwarf Brute" kept clear of its bar), one header row with tilted type names

        // one effectiveness cell: weakness = light warm fill, dark icon; resistance = dark cell, dimmed icon; immunity = the
        // type's icon under Codex's stamp; normal = dark cell, the icon in its colour
        static void BattleCell(RectTransform parent, float x, float top, float w, float h, string icon, Color colour, string tone, float iconSize)
        {
            var weak = tone == PanelModel.Weakness; var resist = tone == PanelModel.Resistance;
            BtRect(parent, "Cell", weak ? BtWeak : BtCell, x, top, w, h);
            var ink = weak ? BtWeakInk : resist ? new Color(BtResist.r, BtResist.g, BtResist.b, 0.55f) : colour;
            // the mark leads, the icon follows: "+" more, "-" less (a shape as well as a fill, never colour alone)
            var mark = weak || resist;
            var ix = mark ? x + w / 2 - 2 : x + (w - iconSize) / 2;
            BtIcon(parent, icon, ink, ix, top + (h - iconSize) / 2, iconSize);
            if (mark) BtText(parent, weak ? "+" : "–", x + 1, top, w / 2 - 2, h, PanelLook.MinText, weak ? BtWeakInk : BtResist, TextAlignmentOptions.Center, FontStyles.Bold);
            if (tone == PanelModel.Immunity) BtIcon(parent, "immunity-stamp", Color.white, x + (w - h + 2) / 2, top + 1, h - 2);
        }

        static void BattleFoeTable(RectTransform col, Block b)
        {
            var rows = (b.Items ?? new List<Block>()).Where(i => i.Kind == "foe").ToList();
            if (rows.Count == 0) return;
            var arrows = rows.Any(r => (r.Items ?? new List<Block>()).Any(i => i.Kind == "arrow"));
            var types = rows.Select(r => (r.Items ?? new List<Block>()).Where(i => i.Kind == "mod").ToList()).FirstOrDefault(m => m.Count > 0) ?? new List<Block>();
            float x0 = BtFoeName + BtFoeDealt + (arrows ? BtFoeArrow : 0), tw = types.Count > 0 ? (Column - x0) / types.Count : 0;

            // the key leads (what the three looks of a cell mean, in the foe's terms), drawn on the fire icon as in the prototype
            if (types.Count > 0)
            {
                var key = Line(col, 6);
                var fire = types.FirstOrDefault(t => t.Id == "fire") ?? types[0];
                Label(key, PanelModel.FoeHead, 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;   // "Foe [+] takes more dmg from" (Joost's words)
                foreach (var (tone, says) in new[] { (PanelModel.Weakness, PanelModel.WeaknessKey), (PanelModel.Resistance, PanelModel.ResistanceKey), (PanelModel.Immunity, PanelModel.ImmunityKey) })
                {
                    var box = Node("Key", key); Size(box, 40, 24);
                    BattleCell(box, 0, 0, 40, 24, fire.Icon, BtColour(fire), tone, 14);
                    Label(key, says, 14, PanelLook.Text).textWrappingMode = TextWrappingModes.NoWrap;
                    Size(Node("Gap", key), 10, 1);
                }
            }

            // live-polish (review 5: the type names sat on two staggered rows): one header row. Every heading stands on the line just over the
            // rule, each type's icon there over its column, its name above the icon tilted 40 degrees (no abbreviations: "Lightning" is wider
            // than a column); tilted names run parallel, 32 px apart, so none touches the next
            var h = BtRow(col, BtFoeHead); float iconTop = BtFoeHead - 26, baseY = BtFoeHead - 15;
            BtText(h, PanelModel.FoeHead, 0, baseY - 12, BtFoeName, 24, PanelLook.MinText, PanelLook.Muted);
            BtText(h, PanelModel.DealtHead, BtFoeName, baseY - 12, BtFoeDealt, 24, PanelLook.MinText, PanelLook.Muted);
            if (arrows) BtText(h, PanelModel.BestArrowHead, BtFoeName + BtFoeDealt, baseY - 12, BtFoeArrow, 24, PanelLook.MinText, PanelLook.Gold, style: FontStyles.Italic);
            for (int k = 0; k < types.Count; k++)
            {
                var c = BtColour(types[k]); var x = x0 + k * tw;
                BtIcon(h, types[k].Icon, c, x + (tw - 18) / 2, iconTop, 18);
                var name = Label(h, types[k].Title, PanelLook.MinText, c, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic);
                name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Overflow;
                var nr = name.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(0, 1); nr.pivot = new Vector2(0, 0.5f);
                nr.sizeDelta = new Vector2(Mathf.Ceil(name.preferredWidth) + 4, LineBox(PanelLook.MinText));
                nr.anchoredPosition = new Vector2(x + tw / 2 - 6, -(iconTop - 10)); nr.localEulerAngles = new Vector3(0, 0, BtFoeTilt);
            }
            BtRect(h, "Rule", PanelLook.Rule, 0, BtFoeHead - 1, Column, 1);

            foreach (var row in rows)
            {
                var r = BtRow(col, BtFoeRow);
                BtMarker(r, row.Icon, 0, 7, 22);
                BtText(r, row.Title, 30, 0, BtFoeName - 34, BtFoeRow, 16, PanelLook.Text);
                var w = Mathf.Max(2f, Mathf.Clamp01(row.Fraction) * 44f);
                Fill(r, "Dealt", PanelLook.Dealt).rectTransform.Box(BtFoeName, 13, w, 10);   // gold = dealt, as on every Battle page (weakness is the cream "+" cell)
                BtText(r, row.Value, BtFoeName + w + 8, 0, BtFoeDealt - w - 8, BtFoeRow, 16, PanelLook.Text, style: FontStyles.Bold);
                var cells = row.Items ?? new List<Block>();
                var arrow = cells.FirstOrDefault(i => i.Kind == "arrow");
                if (arrow != null)
                {
                    BtMarker(r, arrow.Icon, BtFoeName + BtFoeDealt, 9, 18);
                    BtText(r, arrow.Title, BtFoeName + BtFoeDealt + 22, 0, BtFoeArrow - 26, BtFoeRow, PanelLook.MinText, PanelLook.Text);
                }
                var mods = cells.Where(i => i.Kind == "mod").ToList();
                for (int k = 0; k < mods.Count && k < types.Count; k++)
                    BattleCell(r, x0 + k * tw + (tw - BtCellW) / 2, (BtFoeRow - BtCellH) / 2, BtCellW, BtCellH, mods[k].Icon, BtColour(mods[k]), mods[k].Tone, 18);
                BtRect(r, "Rule", BtLine, 0, BtFoeRow - 1, Column, 1);
            }

        }

        // ----- Foes, by damage type: per type the foes it is strong against, weak against and has no effect on -----

        const float BtFtType = 150, BtChipH = 22, BtChipGap = 4, BtChipGapX = 10, BtChipPad = 16, BtFtGap = 14;

        // fix4: a foe tag is a label, not a button: no box, no pill. A 3 px edge in the relation's colour, then the name (weakness: the
        // warm tone, resistance: grey, immunity: struck through). Returns its width.
        static float BattleChip(RectTransform parent, string text, string tone, float x, float top)
        {
            var edge = tone == "strong" ? BtWeak : tone == "weak" ? BtResist : BtImmune;
            var t = Label(parent, text, PanelLook.MinText, tone == "strong" ? BtWeak : BtWeakChip, align: TextAlignmentOptions.MidlineLeft);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var w = Mathf.Ceil(t.preferredWidth) + BtChipPad;
            BtRect(parent, "Edge", edge, x, top, 3, BtChipH);
            t.rectTransform.Box(x + 10, top, w - 10, BtChipH);
            if (tone == "none") BtRect(parent, "Struck", BtImmune, x + 10, top + BtChipH / 2 - 1, w - 16, 2);
            return w;
        }

        static void BattleFoeTypes(RectTransform col, Block b)
        {
            var rows = (b.Items ?? new List<Block>()).Where(i => i.Kind == "dmgtype").ToList();
            if (rows.Count == 0) return;
            if (!string.IsNullOrEmpty(b.Note)) Label(col, b.Note, 14, PanelLook.Muted);   // where the classification comes from
            // a column nobody fills is dropped; the others take the width their tags need (one line per row where it fits), the rest is shared
            var all = new[] { ("strong", PanelModel.StrongAgainst, BtWeak), ("weak", PanelModel.WeakAgainst, BtWeakChip), ("none", PanelModel.NoEffectOn, BtImmune) };
            var shown = all.Where(a => rows.Any(r => (r.Items ?? new List<Block>()).Any(i => i.Kind == "foe" && i.Tone == a.Item1))).ToArray();
            var tones = shown.Select(a => a.Item1).ToArray();
            var measure = Label(col, "", PanelLook.MinText, BtNormal); measure.textWrappingMode = TextWrappingModes.NoWrap;
            float Wide(string text) { measure.text = text ?? ""; return Mathf.Ceil(measure.preferredWidth) + BtChipPad; }
            var natural = new float[tones.Length]; var widest = new float[tones.Length];
            for (int c = 0; c < tones.Length; c++)
                foreach (var row in rows)
                {
                    var ws = (row.Items ?? new List<Block>()).Where(i => i.Kind == "foe" && i.Tone == tones[c]).Select(i => Wide(i.Title)).ToList();
                    if (ws.Count == 0) continue;
                    natural[c] = Mathf.Max(natural[c], ws.Sum() + BtChipGapX * (ws.Count - 1)); widest[c] = Mathf.Max(widest[c], ws.Max());
                }
            for (int c = 0; c < tones.Length; c++) { measure.text = shown[c].Item2; var hw = Mathf.Ceil(measure.preferredWidth) + 4; natural[c] = Mathf.Max(natural[c], hw); widest[c] = Mathf.Max(widest[c], hw); }   // a column is never narrower than its own heading
            var avail = Column - BtFtType - BtFtGap * tones.Length; var sum = natural.Sum();
            var colW = (float[])natural.Clone();
            if (sum <= avail) for (int c = 0; c < colW.Length; c++) colW[c] += (avail - sum) / colW.Length;   // room to spare: shared
            else
            {
                // too wide for one line everywhere: the column with the most to give gives way first (down to its widest tag), so only the rare long row wraps
                var over = sum - avail;
                for (int guard = 0; guard < 8 && over > 0.5f; guard++)
                {
                    int big = 0; for (int c = 1; c < colW.Length; c++) if (colW[c] - widest[c] > colW[big] - widest[big]) big = c;
                    var cut = Mathf.Min(over, colW[big] - widest[big]);
                    if (cut <= 0.01f) break;
                    colW[big] -= cut; over -= cut;
                }
                if (over > 0.5f) { var scale = avail / colW.Sum(); for (int c = 0; c < colW.Length; c++) colW[c] = Mathf.Max(widest[c], colW[c] * scale); }
            }
            var xs = new float[tones.Length]; var at = BtFtType;
            for (int c = 0; c < tones.Length; c++) { at += BtFtGap; xs[c] = at; at += colW[c]; }

            var h = BtRow(col, 30);
            BtText(h, PanelModel.TypeDealtHead, 0, 0, BtFtType, 30, 14, PanelLook.Muted);
            for (int c = 0; c < shown.Length; c++) BtText(h, shown[c].Item2, xs[c], 0, colW[c], 30, 14, shown[c].Item3);
            BtRect(h, "Rule", PanelLook.Rule, 0, 29, Column, 1);

            foreach (var row in rows)
            {
                var r = Node("Row", col);
                var lines = 1;
                for (int c = 0; c < tones.Length; c++)
                {
                    float x = 0; int line = 0;
                    foreach (var chip in (row.Items ?? new List<Block>()).Where(i => i.Kind == "foe" && i.Tone == tones[c]))
                    {
                        var cw = Wide(chip.Title);
                        if (x > 0 && x + cw > colW[c] + 0.5f) { line++; x = 0; }
                        BattleChip(r, chip.Title, chip.Tone, xs[c] + x, 5 + line * (BtChipH + BtChipGap));
                        x += cw + BtChipGapX;
                    }
                    if (x > 0) lines = Mathf.Max(lines, line + 1);
                }
                var height = Mathf.Max(32f, lines * (BtChipH + BtChipGap) + 6);
                Size(r, -1, height);
                var colour = BtColour(row);
                BtIcon(r, row.Icon, colour, 0, (height - 18) / 2, 18);
                var name = BtText(r, row.Title, 24, 0, BtFtType - 24, height, 17, colour, style: FontStyles.Bold);
                if (!string.IsNullOrEmpty(row.Value)) BtText(r, row.Value, 24 + Mathf.Min(name.preferredWidth, 80f) + 6, 0, 56, height, 14, BtNormal);
                BtRect(r, "Rule", BtLine, 0, height - 1, Column, 1);
            }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
        }

        // ----- Defense: blocks and parries as two big numbers, the parries' share of the blocks under them -----

        static void BattleGuard(RectTransform col, Block b)
        {
            var row = Line(col, 12, TextAnchor.LowerLeft);
            Label(row, b.Value, 50, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            Size(VocabImg(row, "Block", "block-mark", PanelLook.Gold), 24, 24);
            Label(row, b.Title, 20, PanelLook.Text).textWrappingMode = TextWrappingModes.NoWrap;
            Size(Node("Gap", row), 32, 1);
            Label(row, b.Value2, 50, PanelLook.Gold, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            Size(VocabImg(row, "Parry", "parry-spark", PanelLook.Gold), 24, 24);
            Label(row, b.Text, 20, PanelLook.Text).textWrappingMode = TextWrappingModes.NoWrap;
        }

        // ----- Defense: damage received per source, stacked by type, the used types' key under it -----

        const float BtSrcName = 170, BtSrcVal = 60, BtSrcRow = 30;

        static void BattleSources(RectTransform col, Block b)
        {
            var rows = (b.Items ?? new List<Block>()).Where(i => i.Kind == "source").ToList();
            if (rows.Count == 0) return;
            BtCaption(col, b.Title, b.Text);
            BattleTypeKey(col, rows.SelectMany(r => r.Items ?? new List<Block>()), true);   // the key leads, under the caption
            var track = Column - BtSrcName - BtSrcVal - 12;
            foreach (var row in rows)
            {
                var r = BtRow(col, BtSrcRow);
                BtMarker(r, row.Icon, 0, 4, 22);
                BtText(r, row.Title, 30, 0, BtSrcName - 34, BtSrcRow, 16, PanelLook.Text);
                var width = Mathf.Max(2f, Mathf.Clamp01(row.Fraction) * track); float x = 0;
                foreach (var p in row.Items ?? new List<Block>())
                {
                    var w = Mathf.Clamp01(p.Fraction) * width; if (w <= 0) continue;
                    BtRect(r, "Part", BtColour(p), BtSrcName + x, 8, w, 14); x += w;
                }
                BtText(r, row.Value, Column - BtSrcVal, 0, BtSrcVal, BtSrcRow, 17, PanelLook.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            }
        }

        // the types used, in the palette's order: a swatch (with the type's icon in dark ink when boxed) and the name
        static void BattleTypeKey(RectTransform col, IEnumerable<Block> parts, bool boxed)
        {
            var used = parts.GroupBy(p => p.Id).Select(g => g.First()).OrderBy(p => { var i = Array.IndexOf(PanelModel.BattleTypes, p.Id); return i < 0 ? 99 : i; }).ToList();
            if (used.Count == 0) return;
            var key = Line(col, 6);
            foreach (var p in used)
            {
                if (boxed)
                {
                    var box = Node("Swatch", key); Size(box, 22, 22);
                    BtRect(box, "Fill", BtColour(p), 0, 0, 22, 22);
                    if (!string.IsNullOrEmpty(p.Icon)) BtIcon(box, p.Icon, DarkInk, 3, 3, 16);
                }
                else Size(Fill(key, "Swatch", BtColour(p)), 10, 10);
                Label(key, p.Title, boxed ? 14 : 13, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
                Size(Node("Gap", key), 8, 1);
            }
        }

        // ----- Deaths: where you fell, wider where you fell more, a death mark and the time per fall above the tile -----

        const float BtFellTile = 38, BtFellLine = 18;

        static void BattleDeathStrip(RectTransform col, Block b)
        {
            var tiles = (b.Items ?? new List<Block>()).Where(t => t.Kind == "biome").ToList();
            if (tiles.Count == 0) return;
            BtCaption(col, b.Title, b.Note);
            // as tall as the most falls over one tile (a count alone is one line)
            Func<Block, List<string>> linesOf = t =>
            {
                var falls = (t.Items ?? new List<Block>()).Where(d => d.Kind == "death").ToList();
                if (t.Count == 0) return new List<string>();
                return falls.Count == t.Count && t.Count <= 3 ? falls.Select(d => d.Value).ToList() : new List<string> { t.Count.ToString() };
            };
            var BtStack = Mathf.Max(1, tiles.Max(t => linesOf(t).Count)) * BtFellLine + 8;
            var strip = BtRow(col, BtStack + BtFellTile);
            int n = tiles.Count; const float Gap = 4;
            var apart = n > 1 && PanelLook.BiomeKey(tiles[n - 1].Id) == "ocean" ? 10f : 0f;
            var weight = tiles.Sum(t => Mathf.Max(1f, t.Fraction));
            var avail = Column - Gap * (n - 1) - apart;
            float x = 0;
            var picked = tiles.Any(t => t.Selected);   // the Biome row's choice: the chosen tile gets a gold edge, the others step back (as on the overview)
            for (int i = 0; i < n; i++)
            {
                var t = tiles[i];
                if (i == n - 1) x += apart;
                var held = Node("Col", strip); held.Stretch();
                if (picked && !t.Selected) held.gameObject.AddComponent<CanvasGroup>().alpha = 0.4f;
                var w = avail * Mathf.Max(1f, t.Fraction) / weight;
                // the falls, newest on top; more than fit: one mark with the count
                var lines = linesOf(t);
                for (int j = 0; j < lines.Count; j++)
                {
                    var row = Node("Fall", held); row.Box(x, BtStack - 4 - (lines.Count - j) * BtFellLine, w, BtFellLine);
                    Layout(row.gameObject.AddComponent<HorizontalLayoutGroup>(), 3, TextAnchor.MiddleCenter);
                    Size(VocabImg(row, "Death", "death", Color.white), 16, 16);
                    Label(row, lines[j], 14, PanelLook.Text).textWrappingMode = TextWrappingModes.NoWrap;
                }
                if (t.Selected) Img(held, "Chosen", null, PanelLook.Gold).rectTransform.Box(x - 2, BtStack - 2, w + 4, BtFellTile + 4);
                Fill(held, "Tile", Hex(t.Colour, PanelLook.BiomeTile(t.Id))).rectTransform.Box(x, BtStack, w, BtFellTile);
                var ink = t.Tone == "dark-text" ? DarkInk : LightInk;
                BtText(held, t.Title, x + 2, BtStack, w - 4, BtFellTile, 14, ink, TextAlignmentOptions.Center, FontStyles.Bold);
                x += w + Gap;
            }
            Spacer(col, 8);
        }

        // ----- Deaths: per fall the killer's trophy, when and where, the last 10 seconds by type (totals, not a timeline) -----

        const float BtDzRow = 70, BtDzPic = 46, BtDzInfo = 168, BtDzTot = 64, BtHitMax = 0.16f;   // the largest hit: 16 % of the strip

        // ----- Overview: the window's deaths, one row each, aligned columns: picture, how many, from what with when under it
        // (the overview's half column is too narrow for the times beside the words) -----
        const float BtDrRow = 48, BtDrPic = 30, BtDrNum = 50;
        static void BattleDeathRows(RectTransform col, Block b)
        {
            foreach (var d in (b.Items ?? new List<Block>()).Where(i => i.Kind == "death"))
            {
                var r = BtRow(col, BtDrRow);
                if ((d.Icon ?? "").StartsWith("vocab:")) BtIcon(r, d.Icon, string.IsNullOrEmpty(d.Colour) ? Color.white : BtColour(d), 2, 11, BtDrPic - 4);
                else BtMarker(r, d.Icon, 0, 9, BtDrPic);
                BtIcon(r, "death", Color.white, BtDrPic + 6, 17, 14);   // the death mark before the count
                BtText(r, d.Value, BtDrPic + 6 + 18, 0, BtDrNum - 18, BtDrRow, 20, PanelLook.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                float xt = BtDrPic + 6 + BtDrNum + 10;
                BtText(r, d.Title, xt, 4, Column - xt, 22, 17, PanelLook.Text);
                BtText(r, d.Text, xt, 26, Column - xt, 16, PanelLook.MinText, PanelLook.Muted);
                BtRect(r, "Rule", BtLine, 0, BtDrRow - 1, Column, 1);
            }
        }

        static void BattleDeathList(RectTransform col, Block b)
        {
            var rows = (b.Items ?? new List<Block>()).Where(i => i.Kind == "death").ToList();
            if (rows.Count == 0) return;
            BtCaption(col, PanelModel.DeathsCaption(rows.Count, b.Count), PanelModel.DeathsQualifier(b));
            BattleTypeKey(col, rows.SelectMany(r => r.Items ?? new List<Block>()), false);
            float xb = BtDzPic + 14 + BtDzInfo + 14, wb = Column - xb - 14 - BtDzTot;
            foreach (var d in rows)
            {
                var r = BtRow(col, BtDzRow);
                BtRect(r, "Slot", BtSlot, 0, 12, BtDzPic, BtDzPic);
                BtMarker(r, d.Icon, 3, 15, BtDzPic - 6);
                BtText(r, d.Title, BtDzPic + 14, 6, BtDzInfo, 22, 19, PanelLook.Text);
                BtText(r, d.Text, BtDzPic + 14, 28, BtDzInfo, 18, 14, PanelLook.Muted);
                var mini = Node("Types", r); mini.Box(BtDzPic + 14, 48, BtDzInfo, 16);
                Layout(mini.gameObject.AddComponent<HorizontalLayoutGroup>(), 5, TextAnchor.MiddleLeft);
                var parts = d.Items ?? new List<Block>();
                var timeline = d.Tone == "timeline";
                foreach (var p in parts.GroupBy(p => p.Id).Select(g => g.First()))
                {
                    Size(Fill(mini, "Swatch", BtColour(p)), 9, 9);
                    Label(mini, p.Title, PanelLook.MinText, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
                    Size(Node("Gap", mini), 4, 1);
                }
                BtRect(r, "Bar", BtDark, xb, 14, wb, 26);
                float x = 0;
                if (timeline)
                    // time runs left to right (30 s before .. the death); each hit ends where it landed, as wide as it hurt
                    foreach (var p in parts)
                    {
                        var w = Mathf.Max(3f, Mathf.Clamp01(p.Fraction2) * wb * BtHitMax);
                        BtRect(r, "Hit", BtColour(p), xb + Mathf.Clamp(Mathf.Clamp01(p.Fraction) * wb - w, 0, wb - w), 14, w, 26);
                    }
                else
                    foreach (var p in parts)
                    {
                        var w = Mathf.Clamp01(p.Fraction) * wb; if (w <= 0) continue;
                        BtRect(r, "Part", BtColour(p), xb + x, 14, w, 26);
                        if (w >= 28) BtText(r, p.Value, xb + x, 14, w, 26, PanelLook.MinText, DarkInk, TextAlignmentOptions.Center, FontStyles.Bold);
                        x += w;
                    }
                if (!string.IsNullOrEmpty(d.Note)) BtText(r, d.Note, xb, 44, 200, 14, PanelLook.MinText, PanelLook.Faint);   // only when the list mixes 30 and 10 seconds
                BtIcon(r, "death", Color.white, xb + wb - 14, 44, 14);
                if (!string.IsNullOrEmpty(d.Value))
                {
                    BtText(r, d.Value, Column - BtDzTot, 8, BtDzTot, 28, 24, PanelLook.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                    BtText(r, PanelModel.ReceivedWord, Column - BtDzTot, 36, BtDzTot, 14, PanelLook.MinText, PanelLook.Faint, TextAlignmentOptions.MidlineRight);
                }
                BtRect(r, "Rule", BtLine, 0, BtDzRow - 1, Column, 1);
            }
        }
    }

    /// <summary>
    /// The game's own creature and arrow data for the Battle pages, read lazily and kept per prefab (reads only): a
    /// creature's Character.m_damageModifiers and the trophy among its CharacterDrop drops; every arrow in ObjectDB (ammo
    /// type "$ammo_arrows") with its damage. Not loaded yet (no ZNetScene / ObjectDB): null, and asked again next time.
    /// </summary>
    static class BattleGame
    {
        static readonly Dictionary<string, PanelModel.FoeData> foes = new Dictionary<string, PanelModel.FoeData>();
        static List<PanelModel.ArrowData> arrows;

        public static PanelModel.FoeData Foe(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return null;
            if (foes.TryGetValue(prefab, out var f)) return f;
            if (!ZNetScene.instance) return null;
            try
            {
                var go = ZNetScene.instance.GetPrefab(prefab);
                var ch = go ? go.GetComponent<Character>() : null;
                if (ch)
                {
                    f = new PanelModel.FoeData();
                    var m = ch.m_damageModifiers;
                    f.Modifiers["blunt"] = m.m_blunt.ToString(); f.Modifiers["slash"] = m.m_slash.ToString(); f.Modifiers["pierce"] = m.m_pierce.ToString();
                    f.Modifiers["fire"] = m.m_fire.ToString(); f.Modifiers["frost"] = m.m_frost.ToString(); f.Modifiers["lightning"] = m.m_lightning.ToString();
                    f.Modifiers["poison"] = m.m_poison.ToString(); f.Modifiers["spirit"] = m.m_spirit.ToString();
                    var drops = go.GetComponent<CharacterDrop>();
                    if (drops != null && drops.m_drops != null)
                        foreach (var d in drops.m_drops)
                        {
                            var p = d.m_prefab;
                            var item = p ? p.GetComponent<ItemDrop>() : null;
                            if (item && item.m_itemData?.m_shared?.m_itemType == ItemDrop.ItemData.ItemType.Trophy) { f.Trophy = p.name; break; }
                        }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] foe data " + prefab + ": " + e.Message); f = null; }
            foes[prefab] = f;
            return f;
        }

        public static IList<PanelModel.ArrowData> Arrows()
        {
            if (arrows != null) return arrows;
            if (!ObjectDB.instance || ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0) return null;
            var list = new List<PanelModel.ArrowData>();
            try
            {
                foreach (var go in ObjectDB.instance.m_items)
                {
                    var shared = go ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                    if (shared == null || shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo || shared.m_ammoType != "$ammo_arrows") continue;
                    var a = new PanelModel.ArrowData { Prefab = go.name, Token = shared.m_name };
                    var dt = shared.m_damages;
                    void Put(string k, float v) { if (v > 0) a.Damage[k] = v; }
                    Put("blunt", dt.m_blunt); Put("slash", dt.m_slash); Put("pierce", dt.m_pierce); Put("fire", dt.m_fire); Put("frost", dt.m_frost);
                    Put("lightning", dt.m_lightning); Put("poison", dt.m_poison); Put("spirit", dt.m_spirit);
                    if (a.Damage.Count > 0) list.Add(a);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] arrow data: " + e.Message); return null; }
            arrows = list;
            return arrows;
        }
    }
}
