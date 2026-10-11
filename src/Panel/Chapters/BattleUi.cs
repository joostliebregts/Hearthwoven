using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
            BtDealtBar = new Color32(0xa9, 0xa0, 0x8c, 0xff), BtDark = new Color(0f, 0f, 0f, 0.45f), BtLine = PanelWash.Line.Game, BtTrack = PanelWash.BattleTrack.Game, BtSlot = new Color(0.17f, 0.13f, 0.098f, 1f);

        /// <summary>Draws one Battle block (DrawVocab hands these kinds over).</summary>
        internal static void BattleBlock(RectTransform col, Block b, Func<string, Action> link)
        {
            switch (b.Kind)
            {
                case "damagegrid": BattleGrid(col, b); break;
                case "dmgmix": DamageRunRow(col, b); break;   // 0.8: a weapon's damage row; the page's weapons make one list (DamageRowsUi.cs)
                case "foetable": BattleFoeTable(col, b, link); break;
                case "foetypes": BattleFoeTypes(col, b); break;
                case "guard": BattleGuard(col, b, link); break;
                case "sources": case "dealtfoes": BattleSources(col, b); break;   // dealtfoes: Damage > By foe (0.8), the same form with the "×N" and the marks
                case "feed": BattleFeed(col, b); break;   // 0.8 (Chapters/BattleFeedUi.cs)
                case PanelModel.FeedLineKind: FeedQuiet(col, b); break;   // 0.8.1: between the feed's fight islands (Chapters/BattleFeedUi.cs)
                case "fightfoes": FightFoes(col, b); break;
                case "fightreceived": FightReceived(col, b); break;
                case "armour": BattleArmour(col, b); break;   // 0.7: Defence > Your armour
                case "deathstrip": BattleDeathStrip(col, b); break;
                case "deaths": BattleDeathList(col, b); break;
                case "deathrows": BattleDeathRows(col, b, link); break;
            }
            if (Labelled(b) && b.Kind != "dmgmix") Since(col, PanelLayout.Text.Floor, b);   // a page without a time window: counted on this PC since install (the weapons' list says it once, after its last row)
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
            var t = Label(head, (title ?? "").ToUpperInvariant(), PanelLayout.Text.Floor, PanelLook.Muted); t.characterSpacing = 8; t.textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(qualifier)) { var q = Label(head, qualifier, PanelLook.MinText, PanelLook.Muted); q.textWrappingMode = TextWrappingModes.NoWrap; }
        }

        // ----- Damage, by type (0.8, Joost: one grammar with By weapon and By foe): a damage row per type, its bar split by weapon kind (or by
        // player with Everyone), one scale for the types (PanelModel.DamageTypeRows, Chapters/DamageRowsUi.cs) -----

        static void BattleGrid(RectTransform col, Block b) => DamageRows(col, PanelModel.DamageTypeRows(b), true);

        // a damage colour as text on the plate: lifted toward white until it reads at 5:1 (the palette stays on the bars and icons)
        static Color Legible(Color c)
        {
            float F(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            float Lum(Color x) => 0.2126f * F(x.r) + 0.7152f * F(x.g) + 0.0722f * F(x.b);
            for (int i = 0; i < 12 && (Lum(c) + 0.05f) / 0.062f < 5f; i++) c = Color.Lerp(c, Color.white, 0.12f);
            return c;
        }

        // the "one item" tint (B41, Joost 2026-10-10: "a visual indicator that it is one item, without explicitly making it look like a card"): a very
        // faint warm tint with rounded corners, no edge; the open foe on Foes and the feed's cards. Its padding: the ranking's
        const int BtBandPadX = 12, BtBandPadBottom = 12;
        static readonly Color BtBand = PanelWash.Band.Game;   // the mirror's .bt-band as the game draws it (PanelWash.cs)

        // ----- Foes, by foe: trophy and name, the dealt bar, the best arrow, one meter per damage type (0.7); a click opens the foe's ranking -----

        const float BtFoeName = 184, BtFoeDealt = 112, BtFoeArrow = 166, BtFoeRow = 40, BtFoeHead = 84, BtCellW = 44, BtFoeTilt = 40;   // live-polish: a wider foe column ("Greydwarf Brute" kept clear of its bar), one header row with tilted type names
        // 0.8.2 (Joost in game: The Queen's "12 6…"): the Dealt column holds its bar (BtFoeBar at most, BtFoeDealtH tall) and, 8 px after it, a
        // 6-digit number with its space ("123 456", bold 16 px) whole: BtFoeDealt - BtFoeBar - 8 = 64 px (render-check measures it).
        // 0.8.2 (Greydwarf Shaman cut at 142 px): the foe's name has BtFoeName - BtFoeNamePad px after its trophy, room for the longest common
        // foe names ("Greydwarf Shaman" 147, the Ashlands' "Charred Marksman" 148) whole (render-check measures them). Both widenings came off
        // the eight type columns (44 to 41.75 px each, the 38 px meters still apart) and the Dealt bar (44 to 40 px at most)
        const float BtFoeBar = 40, BtFoeDealtH = 10, BtFoeNumGap = 8, BtFoeNamePad = 34;
        // 0.8.2 (Joost in game: "Surtling Fire Arr…"): the arrow's name has BtFoeArrow - 26 px, room for the longest arrow names in the game data
        // (ValheimArmory on the server: "Ancient wood arrow", "Surtling Fire Arrow"; the game's own: "Bronzehead Arrow"), whole (render-check measures them).
        // A row that opens ends in the opens mark (OpensMark), BtOpensRight in from its end; every row keeps its room (BtOpensW) so the columns stay put
        const float BtOpensRight = 4, BtOpensW = BtOpensRight + PanelLook.ChevronW + 4;
        const float BtMeterW = 38, BtMeterH = 8, BtMeterGap = 2;   // four whole 8 px parts
        static readonly Color BtMeterTrack = new Color(0.20f, 0.155f, 0.115f, 1f), BtRowHover = PanelWash.RowHover.Game;

        // a part of the meter: square, or rounded on its outer side only (the meter is one thing split into four; VOCABULARY.md): the rounded
        // sprite, its inner half covered square in the same colour
        static void MeterPart(RectTransform parent, Color c, float x, float top, float w, float h, bool roundLeft, bool roundRight)
        {
            if (w <= 0) return;
            if (!roundLeft && !roundRight || !PanelLook.Rounded) { BtRect(parent, "Part", c, x, top, w, h); return; }
            var img = RoundedPart(parent, "Part", c); img.pixelsPerUnitMultiplier = 2f; img.rectTransform.Box(x, top, w, h);
            if (!roundRight) BtRect(parent, "Square", c, x + w / 2, top, w - w / 2, h);
            if (!roundLeft) BtRect(parent, "Square", c, x, top, w / 2, h);
        }

        /// <summary>The weakness meter (VOCABULARY.md "How weak is this foe to that type?"): four parts with small gaps, only the outer corners
        /// rounded, filled by the game's factor, one part per x0.5 (x1 half full, x2 full); x0 empty with a small cross.</summary>
        static void BattleMeter(RectTransform parent, float x, float top, float factor)
        {
            var seg = (BtMeterW - 3 * BtMeterGap) / 4; var units = Mathf.Clamp(factor / 0.5f, 0, 4);
            for (int i = 0; i < 4; i++)
            {
                var sx = x + i * (seg + BtMeterGap);
                MeterPart(parent, BtMeterTrack, sx, top, seg, BtMeterH, i == 0, i == 3);
                var fill = Mathf.Clamp01(units - i);
                if (fill > 0) MeterPart(parent, PanelLook.Gold, sx, top, seg * fill, BtMeterH, i == 0, i == 3 && fill >= 1);
            }
            if (units <= 0) BtText(parent, "\u00D7", x, top - 8, BtMeterW, BtMeterH + 16, PanelLook.MinText, BtImmune, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        // one cell: the type's icon (dimmed for an immunity) over its meter; pointing at it swaps the icon for the exact factor ("×1.5"); on the keys'
        // cursor row (0.8) the factor shows while the focus ring does (FocusMark), so the keys read what the pointer reads
        static void BattleMeterCell(RectTransform row, float x, Block mod, bool cursor = false)
        {
            var cell = Node("Cell", row); cell.Box(x, 0, BtCellW, BtFoeRow);
            var hit = Img(cell, "Hit", null, Color.clear, raycast: true); hit.rectTransform.Stretch();
            var c = BtColour(mod); if (mod.Tone == PanelModel.Immunity) c = new Color(c.r, c.g, c.b, 0.4f);
            var icon = BtIcon(cell, mod.Icon, c, (BtCellW - 16) / 2, 6, 16);
            var factor = BtText(cell, mod.Value, 0, 4, BtCellW, 20, PanelLook.MinText, PanelLook.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            factor.gameObject.SetActive(false);
            BattleMeter(cell, (BtCellW - BtMeterW) / 2, 27, mod.Fraction);
            var show = cell.gameObject.AddComponent<SwapOnHover>(); show.Off = icon.gameObject; show.On = factor.gameObject;
            if (cursor) { var fm = factor.gameObject.AddComponent<FocusMark>(); fm.Focused = true; fm.Hide = icon.gameObject; factor.gameObject.SetActive(KeyFocus); icon.gameObject.SetActive(!KeyFocus); }
        }

        static void BattleFoeTable(RectTransform col, Block b, Func<string, Action> link)
        {
            var rows = (b.Items ?? new List<Block>()).Where(i => i.Kind == "foe").ToList();
            foeCursorRow = null;
            if (rows.Count == 0) return;
            var arrows = rows.Any(r => (r.Items ?? new List<Block>()).Any(i => i.Kind == "arrow"));
            var types = rows.Select(r => (r.Items ?? new List<Block>()).Where(i => i.Kind == "mod").ToList()).FirstOrDefault(m => m.Count > 0) ?? new List<Block>();
            var opens = link != null && rows.Any(r => r.Tone == "opens");
            float x0 = BtFoeName + BtFoeDealt + (arrows ? BtFoeArrow : 0), tw = types.Count > 0 ? (Column - x0 - (opens ? BtOpensW : 0)) / types.Count : 0;

            // live-polish (review 5: the type names sat on two staggered rows): one header row. Every heading stands on the line just over the
            // rule, each type's icon there over its column, its name above the icon tilted 40 degrees (no abbreviations: "Lightning" is wider
            // than a column); tilted names run parallel, 32 px apart, so none touches the next. No key: the meter carries its meaning (About, Y)
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
                var cells = row.Items ?? new List<Block>();
                var ranking = cells.FirstOrDefault(i => i.Kind == "ranking");
                // the open foe and its ranking are one item: one quiet band behind both (BtBand's tint), no padding, so the columns stay put
                var holder = col;
                if (row.Selected)
                {
                    holder = VStack(col, 0);
                    var fill = RoundedPart(holder, "Band", BtBand); fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; fill.rectTransform.Stretch();
                }
                var r = BtRow(holder, BtFoeRow);
                if (row.Tone == "opens" && link != null)
                {
                    // the whole row opens (or closes) its ranking; pointing at it lights it (hover is new in the panel: pointer enter)
                    var ground = RoundedPart(r, "Ground", Color.clear, raycast: true); ground.rectTransform.Stretch();
                    if (!row.Selected) { var lit = r.gameObject.AddComponent<TintOnHover>(); lit.Ground = ground; lit.Lit = BtRowHover; }
                    r.gameObject.AddComponent<Press>().Act = link(PanelModel.FoeOpenLink(row));
                    OpensMark(r, row.Selected, BtOpensRight);   // 0.8.2: it opens, so it ends in the ›, turned down while its ranking shows
                }
                var cursor = row.Note == PanelModel.CursorNote;   // 0.8: the keys' cursor (Chapters/FoesKeys.cs): the focus ring, the factors shown
                if (cursor) { FocusRing(r, 0); foeCursorRow = holder == col ? r : holder; }
                BtMarker(r, row.Icon, 0, 9, 22);
                BtText(r, row.Title, 30, 0, BtFoeName - BtFoeNamePad, BtFoeRow, PanelLayout.Text.Body, PanelLook.Text);
                var w = Mathf.Max(2f, Mathf.Clamp01(row.Fraction) * BtFoeBar);
                var split = cells.Where(i => i.Kind == "part").ToList();
                var total = BtText(r, row.Value, BtFoeName + w + BtFoeNumGap, 0, BtFoeDealt - w - BtFoeNumGap, BtFoeRow, PanelLayout.Text.Body, PanelLook.Text, style: FontStyles.Bold);
                RectTransform dealt = null;
                if (split.Count > 0) dealt = GroupDealtBar(r, split, BtFoeName, (BtFoeRow - BtFoeDealtH) / 2, w, total.gameObject);   // 0.8.2 Everyone: the bar split in the players' colors
                else if (row.Text != null) dealt = AfterDealtBar(r, row, BtFoeName, (BtFoeRow - BtFoeDealtH) / 2, w, total.gameObject);   // 0.8.5: with its after
                else Fill(r, "Dealt", PanelLook.Dealt).rectTransform.Box(BtFoeName, (BtFoeRow - BtFoeDealtH) / 2, w, BtFoeDealtH);   // gold = dealt, as on every Battle page
                var arrow = cells.FirstOrDefault(i => i.Kind == "arrow");
                if (arrow != null)
                {
                    BtMarker(r, arrow.Icon, BtFoeName + BtFoeDealt, 11, 18);
                    BtText(r, arrow.Title, BtFoeName + BtFoeDealt + 22, 0, BtFoeArrow - 26, BtFoeRow, PanelLook.MinText, PanelLook.Text);
                }
                var mods = cells.Where(i => i.Kind == "mod").ToList();
                for (int k = 0; k < mods.Count && k < types.Count; k++) BattleMeterCell(r, x0 + k * tw + (tw - BtCellW) / 2, mods[k], cursor);
                if (dealt) dealt.SetAsLastSibling();   // its hover ring over the cells after it, never under them (its tag is the page's, over all)
                if (ranking != null) BattleRanking(holder, ranking);
                else BtRect(r, "Rule", BtLine, 0, BtFoeRow - 1, Column, 1);
            }
        }

        // ----- Foes with Everyone (0.8.2, Joost in game: "switch between my friends") -----

        /// <summary>A foe's Dealt bar with Everyone: one bar at the row's length, a part per player who hit it in their color (the group's order),
        /// 1 px apart. Pointing at a part rings it, and the foe's total (<paramref name="total"/>) gives its place to the page's tag, whose part
        /// it is and the number ("Edda 310", BarHover.TagOver, as DamageBar's Log). The row's press still opens the foe: the parts only light.
        /// Returns the bar, which the row draws last so its ring lies over the next cell.</summary>
        static RectTransform GroupDealtBar(RectTransform row, List<Block> parts, float x, float top, float w, GameObject total)
        {
            const float gap = 1;
            var bar = Node("Dealt", row); bar.Box(x, top, w, BtFoeDealtH);
            var hover = row.gameObject.AddComponent<BarHover>(); hover.Init(parts.Count);
            var span = w + gap;
            var spans = PanelModel.BarParts(parts.Select(p => p.Fraction).ToArray(), null, Mathf.Min(0.5f, (1 + gap) / span));
            var words = new string[parts.Count]; var start = new float[parts.Count];
            for (int k = 0; k < parts.Count; k++)
            {
                var px = spans[k].from * span; var pw = Mathf.Max(1f, (spans[k].to - spans[k].from) * span - gap);
                var seg = RoundedPart(bar, "Part", Hex(parts[k].Colour, PanelLook.Dealt), raycast: true); seg.pixelsPerUnitMultiplier = DrRadiusScale; seg.rectTransform.Box(px, 0, pw, BtFoeDealtH);
                hover.Parts[k] = seg.rectTransform; var hp = seg.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = k;
                // the tag's words where the total's start (its 6 px padding before them), centred on the row
                words[k] = parts[k].Title + " " + parts[k].Value; start[k] = w + BtFoeNumGap - HoverTag.Pad;
            }
            hover.TagOver(PageTag(row), bar, words, start, (BtFoeDealtH - PanelModel.DamageBar - HoverTag.Rise) / 2, 0, BtFoeDealt + BtFoeArrow, centred: false);
            hover.TagHides = total;
            HoverRing(bar, hover);
            return bar;
        }

        /// <summary>0.8.5: your Dealt bar with its after (PanelModel.AfterPart on the row: Fraction2 the fill, Earlier the faded piece): the bar is its
        /// gold outline (PanelLook.DamageEdge), filled from the left by what got through after a faded piece for hits from before it was recorded.
        /// Pointing at it rings it and the page's tag says its numbers in the total's place (Text: "490 got through, 180 stopped"), as GroupDealtBar's.
        /// Two images more than the plain bar at most (the fill, the faded piece); returns the bar, which the row draws last.</summary>
        static RectTransform AfterDealtBar(RectTransform row, Block foe, float x, float top, float w, GameObject total)
        {
            var bar = Node("Dealt", row); bar.Box(x, top, w, BtFoeDealtH);
            var seg = RoundedPart(bar, "Part", Color.clear, raycast: true); seg.pixelsPerUnitMultiplier = DrRadiusScale; seg.rectTransform.Box(0, 0, w, BtFoeDealtH);
            if (PanelLook.DamageEdge) { seg.sprite = PanelLook.DamageEdge; seg.type = Image.Type.Sliced; seg.color = PanelLook.Dealt; }
            var e = Mathf.Clamp01(foe.Earlier); var ew = e <= 0 ? 0 : Mathf.Max(1, w * e);
            var fw = Mathf.Clamp(w * Mathf.Clamp01(foe.Fraction2), foe.Fraction2 > 0 ? 1 : 0, Mathf.Max(0, w - ew));
            if (ew > 0) { var early = RoundedPart(seg.transform, "Earlier", Faded(PanelLook.Dealt)); early.pixelsPerUnitMultiplier = DrRadiusScale; early.rectTransform.Box(0, 0, ew, BtFoeDealtH); }
            if (fw > 0) { var fill = RoundedPart(seg.transform, "Fill", PanelLook.Dealt); fill.pixelsPerUnitMultiplier = DrRadiusScale; fill.rectTransform.Box(ew, 0, fw, BtFoeDealtH); }
            var hover = row.gameObject.AddComponent<BarHover>(); hover.Init(1);
            hover.Parts[0] = seg.rectTransform; var hp = seg.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = 0;
            hover.TagOver(PageTag(row), bar, new[] { foe.Text }, new[] { w + BtFoeNumGap - HoverTag.Pad }, (BtFoeDealtH - PanelModel.DamageBar - HoverTag.Rise) / 2, 0, BtFoeDealt + BtFoeArrow, centred: false);
            hover.TagHides = total;
            HoverRing(bar, hover);
            return bar;
        }

        /// <summary>Who hit it (PanelModel.WhoHit): its caption and the window's words, then one island of damage rows, a row per player on one scale
        /// (DamageRowsUi.cs: the top player fills the track, zero at a quarter of it, the types inside where their numbers fit).</summary>
        static void WhoHitIsland(RectTransform box, Block who, float inner)
        {
            var rows = (who.Items ?? new List<Block>()).Where(i => i.Kind == "source").ToList();
            if (rows.Count == 0) return;
            BtCaption(box, who.Title, who.Text);
            var isl = VStack(box, 0);
            isl.GetComponent<VerticalLayoutGroup>().padding = new RectOffset((int)IslandPad, (int)IslandPad, (int)IslandPad, (int)IslandPad);
            IslandGround(isl.gameObject); Size(isl, inner, -1);
            var outer = Column; Column = inner - 2 * IslandPad;
            try { DamageRows(isl, rows, true); }
            finally { Column = outer; }
        }

        // ----- the ranking under an open foe (0.8.2, Joost: Best compares with Best): a row of Best islands, Arrows · Bolts · Weapons, and the row
        // of Worst islands under it, each row under its caption; an island holds its group's name and count, then its lines. A line is a damage
        // row (Joost: every list in the book is one): its name and status, then its expected damage before its bar, the bar on one scale for the
        // whole detail (PanelModel.FoeRanking: the best item fills the track, zero at a quarter of it), a part per damage type with its number
        // inside where it fits (BarDigits; else on hover), the true length under it. The Worst row in a quieter tone: its parts' colours stepped
        // back (PanelModel.WorstColour), its names and numbers muted -----

        const float BtRankLine = 50, BtRankName = 20, BtRankBarTop = 22, BtRankHead = 22, BtRankNumGap = 8;   // a line: name and status, its number and bar 2 px under, 4 px to the next

        static void BattleRanking(RectTransform parent, Block rk)
        {
            var rows = (rk.Items ?? new List<Block>()).Where(r => r.Kind == "rankrow").ToList();
            var who = (rk.Items ?? new List<Block>()).FirstOrDefault(r => r.Kind == PanelModel.WhoHitKind || r.Kind == PanelModel.YourHitsKind);   // 0.8.2 Everyone: who hit it, first; 0.8.5 your own book: your hits on it
            if (rows.Count == 0 && who == null) return;
            var box = VStack(parent, 6);
            box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(BtBandPadX, BtBandPadX, 4, BtBandPadBottom);
            var inner = Column - 2 * BtBandPadX;
            if (who != null) WhoHitIsland(box, who, inner);
            if (rows.Count == 0) return;
            // one number column for the whole detail, as wide as its widest number, so every bar starts at one x and keeps one scale
            var measure = Label(box, "", PanelLayout.Text.Small, PanelLook.Text, style: FontStyles.Bold); measure.textWrappingMode = TextWrappingModes.NoWrap;
            float numW = 24;
            foreach (var v in rows.SelectMany(r => r.Items ?? new List<Block>()).SelectMany(g => g.Items ?? new List<Block>()).Select(i => i.Value))
                if (!string.IsNullOrEmpty(v)) { measure.text = v; numW = Mathf.Max(numW, Mathf.Ceil(measure.preferredWidth) + 2); }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
            foreach (var row in rows)
            {
                var groups = (row.Items ?? new List<Block>()).Where(g => g.Kind == "rankgroup").ToList();
                if (groups.Count == 0) continue;
                BtCaption(box, row.Title, row.Text);
                var iw = (inner - IslandGap * (groups.Count - 1)) / groups.Count;
                float Body(Block g) => BtRankHead + 6 + (g.Items?.Count ?? 0) * BtRankLine + (string.IsNullOrEmpty(g.Note) ? 0 : BtRankHead);
                var h = 2 * IslandPad + groups.Max(Body);   // the islands of a row as tall as each other
                var area = BtRow(box, h);
                for (int k = 0; k < groups.Count; k++) RankIsland(area, groups[k], k * (iw + IslandGap), iw, h, numW);
            }
            var small = Label(box, rk.Note ?? "", PanelLook.MinText, PanelLook.Faint); small.textWrappingMode = TextWrappingModes.Normal;
        }

        // one group in its island: its name, its count in the room the name leaves (0.8.2: a half each cut "157 owned, made o..."), its lines
        static void RankIsland(RectTransform area, Block g, float x, float w, float h, float numW)
        {
            var isl = Node("Island", area); isl.Box(x, 0, w, h); IslandGround(isl.gameObject);
            var ink = g.Tone == PanelModel.RankWorstTone ? PanelLook.Muted : PanelLook.Text;
            float gx = x + IslandPad, gw = w - 2 * IslandPad, y = IslandPad;
            var head = BtText(area, g.Title, gx, y, gw, BtRankHead, PanelLayout.Text.Small, ink, style: FontStyles.Bold);
            var hw = Mathf.Min(gw, Mathf.Ceil(head.preferredWidth) + 2); head.rectTransform.sizeDelta = new Vector2(hw, head.rectTransform.sizeDelta.y);
            if (!string.IsNullOrEmpty(g.Text)) BtText(area, g.Text, gx + hw + 8, y, Mathf.Max(0, gw - hw - 8), BtRankHead, PanelLook.MinText, PanelLook.Faint, TextAlignmentOptions.MidlineRight);
            y += BtRankHead + 6;
            foreach (var it in g.Items ?? new List<Block>())
            {
                var line = Node("Line", area); line.Box(gx, y, gw, BtRankLine - 4);
                if (it.Tone == "unknown") line.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;   // not known yet: quieter
                // the status at the right end (quiet italics), the name in the room it leaves
                var st = BtText(line, it.Text, 0, 0, gw, BtRankName, PanelLook.MinText, PanelLook.Faint, TextAlignmentOptions.MidlineRight, FontStyles.Italic);
                var sw = Mathf.Min(gw, Mathf.Ceil(st.preferredWidth) + 2); var sr = st.rectTransform;
                sr.anchoredPosition = new Vector2(gw - sw, sr.anchoredPosition.y); sr.sizeDelta = new Vector2(sw, sr.sizeDelta.y);
                BtText(line, it.Title, 0, 0, Mathf.Max(0, gw - sw - 8), BtRankName, PanelLayout.Text.Small, ink);
                // the expected damage before the bar, the bar on the detail's one scale (no share line: the parts' colours and numbers say it)
                var bar = Node("Bar", line); bar.Box(0, BtRankBarTop, gw, PanelModel.DamageTrack + PanelModel.DamageInner + PanelModel.DamageTrue);
                BtText(bar, it.Value, 0, 0, numW, PanelModel.DamageTrack, PanelLayout.Text.Small, ink, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                var form = PanelModel.BarFormOf((it.Items ?? new List<Block>()).Where(i => i.Kind == "part").ToList(), true);
                if (form.Rows.Count > 0) DamageBar(bar, form, null, it.Fraction, false, numW + BtRankNumGap, gw - numW - BtRankNumGap, shares: false);
                y += BtRankLine;
            }
            if (!string.IsNullOrEmpty(g.Note)) BtText(area, g.Note, gx, y, gw, BtRankHead, PanelLook.MinText, PanelLook.Muted, style: FontStyles.Italic);   // a group with nothing left for Worst
        }

        // ----- Foes, by damage type: per type the foes it is strong against, weak against and has no effect on -----

        const float BtFtType = 150, BtChipH = 22, BtChipGap = 4, BtChipGapX = 10, BtChipPad = 16, BtFtGap = 14;

        // fix4: a foe tag is a label, not a button: no box, no pill. A 3 px edge in the relation's colour, then the name (weakness: the
        // warm tone, resistance: grey, immunity: struck through). Returns its width.
        static float BattleChip(RectTransform parent, string text, string tone, float x, float top)
        {
            var edge = tone == "strong" ? BtWeak : tone == "normal" ? PanelLook.Faint : tone == "weak" ? BtResist : BtImmune;
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
            if (!string.IsNullOrEmpty(b.Note)) Label(col, b.Note, PanelLayout.Text.Floor, PanelLook.Muted);   // where the classification comes from
            // a column nobody fills is dropped; the others take the width their tags need (one line per row where it fits), the rest is shared
            var all = new[] { ("strong", PanelModel.StrongAgainst, BtWeak), ("normal", PanelModel.NormalOn, PanelLook.Muted), ("weak", PanelModel.WeakAgainst, BtWeakChip), ("none", PanelModel.NoEffectOn, BtImmune) };   // 0.7: from the type's side, "normal" its own column
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
                // too wide for one line everywhere (0.7: four columns, "Normal on" the longest): each column its share of the width by what it
                // holds, never narrower than its widest tag; what the floors take comes off the others by what they have to give
                for (int c = 0; c < colW.Length; c++) colW[c] = Mathf.Max(widest[c], natural[c] * avail / sum);
                var over = colW.Sum() - avail; var give = 0f; for (int c = 0; c < colW.Length; c++) give += colW[c] - widest[c];
                if (over > 0.5f && give > 0.5f) for (int c = 0; c < colW.Length; c++) colW[c] -= over * (colW[c] - widest[c]) / give;
            }
            var xs = new float[tones.Length]; var at = BtFtType;
            for (int c = 0; c < tones.Length; c++) { at += BtFtGap; xs[c] = at; at += colW[c]; }

            var h = BtRow(col, 30);
            BtText(h, PanelModel.TypeDealtHead, 0, 0, BtFtType, 30, PanelLayout.Text.Floor, PanelLook.Muted);
            for (int c = 0; c < shown.Length; c++) BtText(h, shown[c].Item2, xs[c], 0, colW[c], 30, PanelLayout.Text.Floor, shown[c].Item3);
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
                var name = BtText(r, row.Title, 24, 0, BtFtType - 24, height, PanelLayout.Text.Name, colour, style: FontStyles.Bold);
                if (!string.IsNullOrEmpty(row.Value)) BtText(r, row.Value, 24 + Mathf.Min(name.preferredWidth, 80f) + 6, 0, 56, height, PanelLayout.Text.Floor, BtNormal);
                BtRect(r, "Rule", BtLine, 0, height - 1, Column, 1);
            }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
        }

        // ----- Defense: blocks and parries as two big numbers, the parries' share of the blocks under them -----

        static void BattleGuard(RectTransform col, Block b, Func<string, Action> link = null)
        {
            if (b.Tone == PanelModel.GuardLineTone) { BattleGuardLine(col, b, link); return; }
            var row = Line(col, 12, TextAnchor.LowerLeft);
            Label(row, b.Value, PanelLayout.Text.Guard, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            Size(VocabImg(row, "Block", "block-mark", PanelLook.Gold), 24, 24);
            Label(row, b.Title, PanelLayout.Text.Lead, PanelLook.Text).textWrappingMode = TextWrappingModes.NoWrap;
            Size(Node("Gap", row), 32, 1);
            Label(row, b.Value2, PanelLayout.Text.Guard, PanelLook.Gold, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            Size(VocabImg(row, "Parry", "parry-spark", PanelLook.Gold), 24, 24);
            Label(row, b.Text, PanelLayout.Text.Lead, PanelLook.Text).textWrappingMode = TextWrappingModes.NoWrap;
            HeadSkills(row, PanelModel.SkillsOf(b), link, 28, 10);   // 0.8: the Blocking skill at the row's right end, as a hero's skill (SkillsBesideUi.cs)
        }

        // ----- Overview: the window's blocks and parries (0.8.5, Joost: "why does it show this button here, rather than stats"): one quiet line under
        // What hurt you in the guard row's form at a small size, each number bold with its mark and word, the Blocking skill at the right end where the
        // line has room for it, the whole line a click to Defense ending in the opens mark (PanelModel.GuardLine). Only the way there when the window
        // keeps no minutes of them -----
        const float BtGlIcon = 18, BtGlGap = 6, BtGlApart = 18, BtGlSkill = 22;
        static void BattleGuardLine(RectTransform col, Block b, Func<string, Action> link)
        {
            var line = Line(col, BtGlGap);
            line.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, (int)(OpensRight + OpensRoom), 0, 0);   // clear of the opens mark
            if (string.IsNullOrEmpty(b.Value)) Label(line, b.Title, PanelLayout.Text.Name, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            else
            {
                Size(VocabImg(line, "Block", "block-mark", PanelLook.Gold), BtGlIcon, BtGlIcon);
                Label(line, b.Value, PanelLayout.Text.Lead, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
                Label(line, b.Title, PanelLayout.Text.Small, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
                Size(Node("Gap", line), BtGlApart - BtGlGap, 1);
                Size(VocabImg(line, "Parry", "parry-spark", PanelLook.Gold), BtGlIcon, BtGlIcon);
                Label(line, b.Value2, PanelLayout.Text.Lead, PanelLook.Gold, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
                Label(line, b.Text, PanelLayout.Text.Small, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
                var before = line.childCount;
                HeadSkills(line, PanelModel.SkillsOf(b), link, BtGlSkill);   // the Blocking skill, as on Defense, while it fits
                LayoutRebuilder.ForceRebuildLayoutImmediate(line);
                if (LayoutUtility.GetPreferredWidth(line) > Column)
                    for (int i = line.childCount - 1; i >= before; i--) { var c = line.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
            }
            var hit = line.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            var go = link?.Invoke(b.Id); if (go != null) ClickTarget(hit, go, opens: true);   // the way to Defense: the whole line, the opens mark at its end
        }

        // ----- Defense: damage received per source (ISC-22): per source its trophy, name and total, then the shared bar with its list
        // (PanelUi.Composition, BAR-FORM.md: the amount per damage type), so Defence has the granularity Damage has -----

        // 0.8: one row per foe in the damage rows (DamageRowsUi.cs), which replace B40-B41's band per foe: the picture, name, "×N" and
        // total on the bar's line, every foe's bar on one scale with its zero at a quarter of the track

        static void BattleSources(RectTransform col, Block b)
        {
            var rows = (b.Items ?? new List<Block>()).Where(i => i.Kind == "source").ToList();
            if (rows.Count == 0) return;
            // 0.8 (Damage > By foe, dealtfoes): the one-line key of the marks at the caption's right end
            var key = (b.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "pipkey");
            if (key == null) BtCaption(col, b.Title, b.Text);
            else
            {
                var head = Line(col, 8);
                var t = Label(head, (b.Title ?? "").ToUpperInvariant(), PanelLayout.Text.Floor, PanelLook.Muted); t.characterSpacing = 8; t.textWrappingMode = TextWrappingModes.NoWrap;
                if (!string.IsNullOrEmpty(b.Text)) { var q = Label(head, b.Text, PanelLook.MinText, PanelLook.Muted); q.textWrappingMode = TextWrappingModes.NoWrap; }
                Node("Rest", head).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                PipKeyLine(head, key);
            }
            // 0.8 (Joost's damage bars, density-feedback/DAMAGE-BARS.md): one row per foe, its bar's zero at a quarter of the track, the true
            // length under it, the types' shares on one line under that (Chapters/DamageRowsUi.cs); dealt on By foe, received on Defence
            DamageRows(col, rows, b.Kind == "dealtfoes");
        }

        // a source's picture (B25): a creature's trophy or the foe mark; a cause that is no creature (smoke, a fall) the swatch of the type it
        // did most, with that type's icon in dark ink (as Your armour's type rows); the summed rest none
        static void BattleSourcePicture(RectTransform who, Block row)
        {
            if (row.Tone == "type")
            {
                BtRect(who, "Swatch", Hex(row.Colour, PanelLook.Muted), 0, 1, 22, 22);
                if (!string.IsNullOrEmpty(row.Icon)) BtIcon(who, row.Icon, DarkInk, 3, 4, 16);
            }
            else BtMarker(who, row.Icon, 0, 1, 22);
        }

        // ----- Defense > What your armor stopped (Chapters/ArmourModel.cs; 0.8.1, Joost: the damage-row grammar): the caption, the view's one bar
        // (what reached your armor, "82 % stopped" in front) with the key under it, then a damage row per type or foe on one scale (DamageRowsUi.cs);
        // a part as wide as what reached your armor, filled = what got through, the outline in its colour = what your armor stopped
        // (DamageRowsUi.StoppedPart; never faded: faded means "before install"). The bar and the key start where the rows' track does -----

        const float BtArmLine = 2, BtArmShare = 20;

        static void BattleArmour(RectTransform col, Block b)
        {
            var rows = (b.Items ?? new List<Block>()).Where(i => i.Kind == "armourrow").ToList();
            if (rows.Count == 0) return;
            BtCaption(col, b.Title, b.Text);
            var trackX = NameColumn(col, rows) + DrCol + NumberColumn(col, rows.Select(r => r.Value)) + DrCol;   // as DamageRows lays the rows out
            var bar = (b.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "armorbar");
            if (bar != null) ArmourBar(col, bar, trackX);
            if (b.Tone == "key") ArmourKey(col, trackX);
            DamageRows(col, rows, false);
        }

        // the view's one bar: its share stopped in front ("82 %" bold, "stopped"), the bar on a track as wide as the rows' track, its two numbers inside
        static void ArmourBar(RectTransform col, Block bar, float trackX)
        {
            var form = PanelModel.BarFormOf((bar.Items ?? new List<Block>()).Where(i => i.Kind == "part").ToList(), true);
            if (form.Rows.Count == 0) return;
            var r = BtRow(col, PanelModel.DamageTrack);
            var share = BtText(r, bar.Value, 0, 0, trackX - DrCol, PanelModel.DamageTrack, BtArmShare, PanelLook.Text, style: FontStyles.Bold);
            var sw = Mathf.Ceil(share.preferredWidth) + 2; share.rectTransform.sizeDelta = new Vector2(sw, share.rectTransform.sizeDelta.y);
            BtText(r, bar.Text, sw + 6, 0, Mathf.Max(0, trackX - DrCol - sw - 6), PanelModel.DamageTrack, DrName, PanelLook.Muted);
            var w = Mathf.Max(80f, Column - trackX);
            var track = RoundedPart(r, "Track", DrTrack); track.pixelsPerUnitMultiplier = DrRadiusScale; track.rectTransform.Box(trackX, 0, w, PanelModel.DamageTrack);
            // the bar is its own pointer target, clear (as DamageBar's outlined parts): no object of its own for it
            var seg = RoundedPart(r, "Bar", Color.clear, raycast: true); seg.pixelsPerUnitMultiplier = DrRadiusScale;
            seg.rectTransform.Box(trackX, (PanelModel.DamageTrack - PanelModel.DamageBar) / 2, w, PanelModel.DamageBar);
            StoppedPart(seg, form.Rows[0], w, Hex(form.Rows[0].Colour, PanelLook.Muted));
            // 0.8.2: pointing at it rings it and the page's tag over it says its numbers, as every damage bar's part (PanelModel.DamageHoverWords)
            var hover = r.gameObject.AddComponent<BarHover>(); hover.Init(1);
            hover.Parts[0] = seg.rectTransform; var hp = seg.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = 0;
            HoverRing(r, hover);
            hover.TagOver(PageTag(r), r, new[] { PanelModel.DamageHoverWords(form.Rows[0]) }, new[] { trackX + w / 2 }, (PanelModel.DamageTrack - PanelModel.DamageBar) / 2 - 2, 0, trackX + w);
        }

        // the key under the bar: a whole part (what reached your armor), its filled piece (got through), its outlined piece (stopped by your armor)
        static void ArmourKey(RectTransform col, float trackX)
        {
            var key = Line(col, 6); key.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset((int)trackX, 0, 0, 0);
            void Entry(string words, float fill, bool outline)
            {
                if (key.childCount > 0) Size(Node("Gap", key), 10, 1);
                var sw = Node("Swatch", key); Size(sw, 22, 14);
                if (outline) BtHollow(sw, PanelLook.Muted, 0, 0, 22, 14);
                if (fill > 0) BtRect(sw, "Fill", PanelLook.Muted, 0, 0, fill, 14);
                Label(key, words, PanelLayout.Text.Floor, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            }
            Entry(PanelModel.ArmourKeyWhole, 9, true);
            Entry(PanelModel.ArmourKeyThrough, 22, false);
            Entry(PanelModel.ArmourKeyStopped, 0, true);
        }

        // an outlined box: four thin edges, the inside left open
        static void BtHollow(RectTransform parent, Color colour, float x, float top, float w, float h)
        {
            var line = Mathf.Min(BtArmLine, w / 2);
            BtRect(parent, "Edge", colour, x, top, w, BtArmLine); BtRect(parent, "Edge", colour, x, top + h - BtArmLine, w, BtArmLine);
            BtRect(parent, "Edge", colour, x, top, line, h); BtRect(parent, "Edge", colour, x + w - line, top, line, h);
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
                Label(key, p.Title, PanelLook.MinText, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;   // the floor: Label lifted 13 to 14 anyway, and its box was laid out for 13
                Size(Node("Gap", key), 8, 1);
            }
        }

        // ----- Deaths: where you fell (0.8.5, a friend's idea Joost liked: the tiles grow; proto/3-deaths-height variant B). Each found biome's tile is a
        // column in its land's colour, taller where you fell more (PanelModel.FellHeight, the one rule the preview draws too), the time of each fall
        // in it while they fit (PanelModel.FellDated), else the death mark and the count over its name, the name at its foot (two lines when the
        // tile is narrow). On All one tile more at the end, a dashed edge round "12 no place", and under the strip the line that says why -----

        const float BtFellMark = 14, BtFellMarkGap = 5, BtFellNamePad = 2, BtDash = 6, BtDashGap = 4, BtDashLine = 2, BtFellDim = 0.4f;
        static readonly Color BtFellMarkLight = new Color32(0xd9, 0xcc, 0xab, 0xff), BtNoPlaceEdge = PanelWash.NoPlaceEdge.Game;

        static void BattleDeathStrip(RectTransform col, Block b)
        {
            var tiles = (b.Items ?? new List<Block>()).Where(t => t.Kind == "biome").ToList();
            if (tiles.Count == 0) return;
            BtCaption(col, b.Title, b.Note);
            var dated = PanelModel.FellDated(tiles); var most = Mathf.Max(1, tiles.Max(t => t.Count));
            var none = b.Count > 0; int k = tiles.Count + (none ? 1 : 0);
            var sea = tiles.Count > 1 && PanelLook.BiomeKey(tiles[tiles.Count - 1].Id) == "ocean" ? PanelLayout.Fell.Apart : 0f;   // the sea apart from the journey
            var w = Mathf.Min(PanelLayout.Fell.TileMax, (Column - PanelLayout.Fell.Gap * (k - 1) - sea - (none ? PanelLayout.Fell.Apart : 0)) / k);
            var tall = tiles.Max(t => PanelModel.FellHeight(t.Count, most, dated));
            var strip = BtRow(col, tall);
            var picked = tiles.Any(t => t.Selected);   // the Biome row's choice: the chosen tile gets a gold edge, the others step back (as on the overview)
            float x = 0, foot = PanelLayout.Fell.Foot;
            for (int i = 0; i < tiles.Count; i++)
            {
                var t = tiles[i];
                if (i == tiles.Count - 1) x += sea;
                var held = Node("Col", strip); held.Stretch();
                if (picked && !t.Selected) held.gameObject.AddComponent<CanvasGroup>().alpha = BtFellDim;
                var h = PanelModel.FellHeight(t.Count, most, dated); var top = tall - h;
                if (t.Selected) Img(held, "Chosen", null, PanelLook.Gold).rectTransform.Box(x - 2, top - 2, w + 4, h + 4);
                Fill(held, "Tile", Hex(t.Colour, PanelLook.BiomeTile(t.Id))).rectTransform.Box(x, top, w, h);
                var dark = t.Tone == "dark-text"; var ink = dark ? DarkInk : LightInk; var mark = dark ? DarkInk : BtFellMarkLight;
                if (dated)
                {
                    var falls = (t.Items ?? new List<Block>()).Where(d => d.Kind == "death").ToList();   // newest first, from the top
                    for (int j = 0; j < falls.Count; j++) FellLine(held, falls[j].Value, x, top + j * PanelLayout.Fell.Line, w, PanelLayout.Fell.Line, ink, mark);
                }
                else if (t.Count > 0) FellLine(held, t.Count.ToString(), x, top + h - foot - PanelLayout.Fell.CountLine, w, PanelLayout.Fell.CountLine, ink, mark);
                var name = BtText(held, t.Title, x + BtFellNamePad, tall - foot, w - 2 * BtFellNamePad, foot, PanelLayout.Text.Floor, ink, TextAlignmentOptions.Center, FontStyles.Bold);
                name.textWrappingMode = TextWrappingModes.Normal;   // "Black Forest" on two lines in a narrow tile, never cut
                x += w + PanelLayout.Fell.Gap;
            }
            if (none)
            {
                x += PanelLayout.Fell.Apart;
                var held = Node("NoPlace", strip); held.Stretch();
                if (picked) held.gameObject.AddComponent<CanvasGroup>().alpha = BtFellDim;
                BtDashed(held, BtNoPlaceEdge, x, tall - foot, w, foot);
                var words = Node("Words", held); words.Box(x, tall - foot, w, foot);
                Layout(words.gameObject.AddComponent<HorizontalLayoutGroup>(), BtFellMarkGap, TextAnchor.MiddleCenter);
                Label(words, b.Count.ToString(), PanelLayout.Text.Floor, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
                Label(words, PanelModel.NoPlaceWords, PanelLayout.Text.Floor, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (!string.IsNullOrEmpty(b.Text)) Label(col, b.Text, PanelLayout.Text.Floor, PanelLook.Muted).textWrappingMode = TextWrappingModes.Normal;   // why some falls have no place
        }

        // a fall in its tile: the death mark and its time (or a tile's count), centred, bold, in the ink that reads on the land's colour
        static void FellLine(RectTransform parent, string text, float x, float top, float w, float h, Color ink, Color mark)
        {
            var row = Node("Fall", parent); row.Box(x, top, w, h);
            Layout(row.gameObject.AddComponent<HorizontalLayoutGroup>(), BtFellMarkGap, TextAnchor.MiddleCenter);
            Size(VocabImg(row, "Death", "death", mark), BtFellMark, BtFellMark);
            Label(row, text, PanelLayout.Text.Floor, ink, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
        }

        // a dashed edge round a box (the falls with no place: a tile with nothing known in it), once per page
        static void BtDashed(RectTransform parent, Color colour, float x, float top, float w, float h)
        {
            for (float d = 0; d < w; d += BtDash + BtDashGap)
            {
                var len = Mathf.Min(BtDash, w - d);
                BtRect(parent, "Dash", colour, x + d, top, len, BtDashLine); BtRect(parent, "Dash", colour, x + d, top + h - BtDashLine, len, BtDashLine);
            }
            for (float d = 0; d < h; d += BtDash + BtDashGap)
            {
                var len = Mathf.Min(BtDash, h - d);
                BtRect(parent, "Dash", colour, x, top + d, BtDashLine, len); BtRect(parent, "Dash", colour, x + w - BtDashLine, top + d, BtDashLine, len);
            }
        }

        // ----- Deaths: per fall the killer's trophy, when and where, the last 10 seconds by type (totals, not a timeline) -----

        const float BtDzRow = 70, BtDzPic = 46, BtDzInfo = 168, BtDzTot = 64, BtHitMax = 0.16f;   // the largest hit: 16 % of the strip

        // ----- Overview: the window's falls (0.8.5, variant A; Joost: "Overview should fit in one screen"): the caption with how many and in what order,
        // then one line each, aligned: the killer's picture, the death mark and how many, from what, the newest one's time at the right end; past
        // PanelModel.OverviewFallRows a last line that opens Deaths. None: the caption and "No deaths yet" -----
        const float BtFlRow = 26, BtFlGap = 2, BtFlPic = 22, BtFlCol = 10, BtFlMark = 14, BtFlCount = 24, BtFlMarkGap = 4, BtFlHeadGap = 8;
        static void BattleDeathRows(RectTransform col, Block b, Func<string, Action> link)
        {
            var head = Line(col, BtFlHeadGap);
            Sect(head, b.Title, b.Value);
            if (!string.IsNullOrEmpty(b.Note)) Label(head, b.Note, PanelLayout.Text.Floor, PanelLook.Faint).textWrappingMode = TextWrappingModes.NoWrap;
            var items = b.Items ?? new List<Block>();
            if (items.Count == 0) { if (!string.IsNullOrEmpty(b.Text)) Label(col, b.Text, PanelLayout.Text.Body, PanelLook.Muted, style: FontStyles.Italic); return; }
            var list = VStack(col, BtFlGap);
            float xm = BtFlPic + BtFlCol, xt = xm + BtFlMark + BtFlMarkGap + BtFlCount + BtFlCol;
            foreach (var d in items)
            {
                var r = BtRow(list, BtFlRow);
                if (d.Kind == "more")   // the falls past the lines: a click opens Deaths
                {
                    var more = BtText(r, d.Title, xt, 0, Column - xt, BtFlRow, PanelLayout.Text.Name, PanelLook.Muted, style: FontStyles.Italic);
                    var go = link?.Invoke(d.Id); if (go != null) { more.raycastTarget = true; ClickTarget(more, go, opens: true); }   // the opens mark at the line's end
                    continue;
                }
                if ((d.Icon ?? "").StartsWith("vocab:")) BtIcon(r, d.Icon, string.IsNullOrEmpty(d.Colour) ? Color.white : BtColour(d), 0, (BtFlRow - BtFlPic) / 2, BtFlPic);
                else { BtRect(r, "Slot", BtSlot, 0, (BtFlRow - BtFlPic) / 2, BtFlPic, BtFlPic); BtMarker(r, d.Icon, 0, (BtFlRow - BtFlPic) / 2, BtFlPic); }
                BtIcon(r, "death", Color.white, xm, (BtFlRow - BtFlMark) / 2, BtFlMark);   // the death mark before the count
                BtText(r, d.Value, xm + BtFlMark + BtFlMarkGap, 0, BtFlCount, BtFlRow, PanelLayout.Text.Name, PanelLook.Text, style: FontStyles.Bold);
                var when = BtText(r, d.Text, xt, 0, Column - xt, BtFlRow, PanelLayout.Text.Floor, PanelLook.Muted, TextAlignmentOptions.MidlineRight);
                var tw = Mathf.Ceil(when.preferredWidth) + 2;
                BtText(r, d.Title, xt, 0, Mathf.Max(0, Column - xt - tw - BtFlCol), BtFlRow, PanelLayout.Text.Name, PanelLook.Text);   // a long cause gives way, never the time
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
                BtText(r, d.Title, BtDzPic + 14, 6, BtDzInfo, 22, PanelLayout.Text.Count, PanelLook.Text);
                BtText(r, d.Text, BtDzPic + 14, 28, BtDzInfo, 18, PanelLayout.Text.Floor, PanelLook.Muted);
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
                    BtText(r, d.Value, Column - BtDzTot, 8, BtDzTot, 28, PanelLayout.Text.Heading, PanelLook.Text, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
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

        static List<PanelModel.ArrowData> gear;

        // the prefabs a recipe makes (ObjectDB.m_recipes): a creature's copy of a weapon has none (PanelModel.OneLinePerItem)
        static HashSet<string> Recipes()
        {
            var made = new HashSet<string>();
            if (ObjectDB.instance.m_recipes != null) foreach (var r in ObjectDB.instance.m_recipes) if (r && r.m_item) made.Add(r.m_item.gameObject.name);
            return made;
        }

        /// <summary>Every bolt (ammo "$ammo_bolts") and weapon (one- and two-handed, bows; not pickaxes) in ObjectDB with battle damage, its
        /// base damage (quality 1), for the Foes ranking (0.7). Read once; null before ObjectDB loads.</summary>
        public static IList<PanelModel.ArrowData> Gear()
        {
            if (gear != null) return gear;
            if (!ObjectDB.instance || ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0) return null;
            var list = new List<PanelModel.ArrowData>();
            try
            {
                var made = Recipes();
                foreach (var go in ObjectDB.instance.m_items)
                {
                    var shared = go ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                    if (shared == null) continue;
                    var type = shared.m_itemType; string kind = null;
                    if (type == ItemDrop.ItemData.ItemType.Ammo && shared.m_ammoType == "$ammo_bolts") kind = "bolt";
                    else if ((type == ItemDrop.ItemData.ItemType.OneHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || type == ItemDrop.ItemData.ItemType.Bow)
                             && shared.m_skillType != Skills.SkillType.Pickaxes) kind = "weapon";
                    if (kind == null) continue;
                    var a = new PanelModel.ArrowData { Prefab = go.name, Token = shared.m_name, Kind = kind, Recipe = made.Contains(go.name) };
                    var dt = shared.m_damages;
                    void Put(string k, float v) { if (v > 0) a.Damage[k] = v; }
                    Put("blunt", dt.m_blunt); Put("slash", dt.m_slash); Put("pierce", dt.m_pierce); Put("fire", dt.m_fire); Put("frost", dt.m_frost);
                    Put("lightning", dt.m_lightning); Put("poison", dt.m_poison); Put("spirit", dt.m_spirit);
                    if (a.Damage.Count > 0) list.Add(a);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] gear data: " + e.Message); return null; }
            gear = list;
            return gear;
        }

        public static IList<PanelModel.ArrowData> Arrows()
        {
            if (arrows != null) return arrows;
            if (!ObjectDB.instance || ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0) return null;
            var list = new List<PanelModel.ArrowData>();
            try
            {
                var made = Recipes();
                foreach (var go in ObjectDB.instance.m_items)
                {
                    var shared = go ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                    if (shared == null || shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo || shared.m_ammoType != "$ammo_arrows") continue;
                    var a = new PanelModel.ArrowData { Prefab = go.name, Token = shared.m_name, Recipe = made.Contains(go.name) };
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

    /// <summary>Pointing at a meter cell swaps its icon for the exact factor (Foes, 0.7); built once with the page, nothing per frame.</summary>
    sealed class SwapOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject On, Off;
        public void OnPointerEnter(PointerEventData e) { if (On) On.SetActive(true); if (Off) Off.SetActive(false); }
        public void OnPointerExit(PointerEventData e) { if (On) On.SetActive(false); if (Off) Off.SetActive(true); }
    }

    /// <summary>A foe row that opens: pointing at it lights its ground (Foes, 0.7).</summary>
    sealed class TintOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Ground; public Color Lit;
        public void OnPointerEnter(PointerEventData e) { if (Ground) Ground.color = Lit; }
        public void OnPointerExit(PointerEventData e) { if (Ground) Ground.color = Color.clear; }
    }
}
