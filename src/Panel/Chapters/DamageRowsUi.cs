using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The damage rows (0.8, Joost's battle plate settings, work/hearthwoven-0.8/density-feedback/DAMAGE-BARS.md): every list of per-foe
    /// damage-type bars in the book, one row per foe. On the bar's line the foe's picture, name and "×N" chip (a column as wide as the list's
    /// widest, 200 to 230 px), its damage number right-aligned
    /// before the bar (a column as wide as the list's widest number, 64 px at least), then the bar on a faint track: one scale for the page with its zero at a quarter of the track
    /// (PanelModel.DamageBarWidth), rounded parts 3 px apart, each part's number inside it where it fits at the book's 14 px floor. Under the
    /// bar a thin line at the true length from zero (gold for damage dealt, quiet for damage received: gold means dealt in this chapter),
    /// then one line of the parts with their share of the row ("Slash 33 %"; By type's weapon kinds with their icon). The outcome marks under the
    /// name (Damage > By foe, Last fight). The rows of Damage's three views (By weapon, By type, By foe), Defence's Received from and Last fight's
    /// foes (0.8, Joost: one grammar for them all).
    /// Pointing at a part rings it and lights its share (HoverUi.cs); a part too narrow for its number shows the number there instead of its
    /// share. Laid out once per fill in fixed rows, nothing per frame. Mirrored by dmgRows() in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        const float DrWho = 200, DrWhoMax = 230, DrNum = 64, DrCol = 12, DrNameX = 30, DrName = 17, DrDigits = 13, DrInsidePad = 6, DrSwatch = 10, DrKeyIcon = 14, DrSwatchGap = 5, DrEntryGap = 14, DrLineGap = 4, DrNoteH = 18;
        static readonly Color DrTrack = new Color(1f, 0.941f, 0.824f, 0.05f), DrTrueDealt = new Color(0.91f, 0.761f, 0.478f, 0.8f), DrTrueReceived = new Color(0.725f, 0.651f, 0.533f, 0.8f);

        /// <summary>The rows of one list: <paramref name="rows"/> carry Title, Value (the total), Value2 (the "×N" or null), Icon (or Tone "type"
        /// with Colour), Fraction (the total against the list's largest) and Items: Kind "part" per damage type and an optional Kind "pips".
        /// <paramref name="dealt"/>: damage you dealt (the number and the true line in gold), else damage you received.</summary>
        static void DamageRows(RectTransform col, IList<Block> rows, bool dealt)
        {
            if (rows == null || rows.Count == 0) return;
            var list = VStack(col, PanelModel.DamageFoeGap);
            float whoW = NameColumn(list, rows), numW = NumberColumn(list, rows.Select(r => r.Value));
            float trackX = whoW + DrCol + numW + DrCol, trackW = Mathf.Max(80f, Column - trackX);
            foreach (var row in rows) DamageRow(list, row, dealt, whoW, numW, trackX, trackW);
        }

        // Battle's rows by player (Everyone's Damage and Defence, Last fight; PanelModel.DamagePlayerRows): the caption, then a damage row per player,
        // their bar in their colour with their share of the group in it, their damage by type as the line under it
        static void DamagePlayers(RectTransform col, Block b)
        {
            BtCaption(col, b.Title, null);
            DamageRows(col, PanelModel.DamagePlayerRows(b), b.Tone == PanelModel.DamagePlayersDealt);
        }

        // the radius of the track and the parts (PanelModel.DamageRadius): the rounded sprite's own corner (5 px) drawn smaller
        const float DrRadiusScale = 5f / PanelModel.DamageRadius;

        /// <summary>A part's number inside it (bold, in the ink that reads on its colour, 3 px clear either side). The book's one text under
        /// PanelLook.MinText (Joost 13:40): a number of digits only is 13 px, or 12 (PanelLook.MinBarDigits) to fit, never less; anything else
        /// ("28 %", "&lt;1") the 14 px floor. False when it does not fit at its smallest: the part shows it on hover instead.</summary>
        static bool BarDigits(Transform part, string text, float partW, Color ink)
        {
            var t = Label(part, text, PanelLook.MinText, ink, style: FontStyles.Bold, align: TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch();
            if (PanelLook.BarDigitsText(text))
            {
                t.name = PanelLook.BarDigitsName;
                t.fontSize = DrDigits;
                if (Mathf.Ceil(t.preferredWidth) + DrInsidePad > partW) t.fontSize = PanelLook.MinBarDigits;
            }
            if (Mathf.Ceil(t.preferredWidth) + DrInsidePad <= partW) return true;
            t.gameObject.SetActive(false); Destroy(t.gameObject);
            return false;
        }

        // the name column (REVIEW-081 #7): as wide as the list's widest picture, name and "×N" ("Greydwarf Shaman ×3"), DrWho at least and DrWhoMax at
        // most: one column for the list, so its rows keep one x and one scale. A name still too wide gives way (an ellipsis), never its "×N"
        static float NameColumn(RectTransform parent, IEnumerable<Block> rows)
        {
            var measure = Label(parent, "", DrName, PanelLook.Text); measure.textWrappingMode = TextWrappingModes.NoWrap;
            float whoW = DrWho;
            foreach (var r in rows)
            {
                measure.text = r.Title ?? "";
                whoW = Mathf.Max(whoW, DrNameX + Mathf.Ceil(measure.preferredWidth) + 2 + (string.IsNullOrEmpty(r.Value2) ? 0 : 8 + BadgeWidth(parent, r.Value2)));
            }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
            return Mathf.Min(whoW, DrWhoMax);
        }

        // the number column: DrNum, wider when the list's widest number needs it
        static float NumberColumn(RectTransform parent, IEnumerable<string> values)
        {
            var measure = Label(parent, "", DrName, PanelLook.Text); measure.textWrappingMode = TextWrappingModes.NoWrap;
            float numW = DrNum;
            foreach (var v in values) if (!string.IsNullOrEmpty(v)) { measure.text = v; numW = Mathf.Max(numW, Mathf.Ceil(measure.preferredWidth) + 2); }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
            return numW;
        }

        // By weapon: each weapon kind is a block of its own (dmgmix; Compare reads them as a run), drawn one at a time: a weapon's row joins the
        // list the weapon before it started when nothing came between, so the weapons are one list (6 px apart, one scale: their Fraction) with
        // one "Recorded from" line after its last row. Its number column is measured once, when the run starts (REVIEW-081 #6: per row, a "less
        // than 1" or a rounding near a power of ten moved that row's track off the others'), and kept with the run: every row shares one x
        // and one scale. PanelModel.DamageWidestNumber never undershoots the run's largest; a later number wider still grows to the left
        static RectTransform dmgRun; static TextMeshProUGUI dmgRunLabel; static float dmgRunNumW, dmgRunWhoW;

        static void DamageRunRow(RectTransform col, Block b)
        {
            if (!(dmgRun != null && dmgRun.parent == col && dmgRun.GetSiblingIndex() == col.childCount - 1))
            {
                dmgRun = VStack(col, PanelModel.DamageFoeGap); dmgRunLabel = null;
                dmgRunNumW = NumberColumn(dmgRun, new[] { PanelModel.DamageWidestNumber(b), b.Value }); dmgRunWhoW = NameColumn(dmgRun, new[] { b });   // the weapon kinds' names are short: DrWho
            }
            float trackX = dmgRunWhoW + DrCol + dmgRunNumW + DrCol, trackW = Mathf.Max(80f, Column - trackX);
            DamageRow(dmgRun, b, true, dmgRunWhoW, dmgRunNumW, trackX, trackW);
            // the run's label only while it is still this run's (PanelReuse.cs: a plain text a cleared page left may be handed out again elsewhere)
            if (dmgRunLabel && dmgRunLabel.transform.parent == dmgRun) dmgRunLabel.transform.SetAsLastSibling();
            else { dmgRunLabel = null; if (Labelled(b)) dmgRunLabel = Since(dmgRun, 14, b); }
        }

        static void DamageRow(RectTransform list, Block row, bool dealt, float whoW, float numW, float trackX, float trackW)
        {
            var r = Node("Foe", list);
            var items = row.Items ?? new List<Block>();
            var pips = items.FirstOrDefault(i => i.Kind == "pips");
            float h = PanelModel.DamageTrack, line = PanelModel.DamageTrack;
            // the name column, centred on the bar's track: picture, name (a long one gives way), the "×N" chip right after it
            if ((row.Icon ?? "").StartsWith("vocab:weapon-", System.StringComparison.Ordinal)) BtIcon(r, row.Icon, PanelLook.Gold, 0, 1, 22);   // a weapon kind (By weapon)
            else BattleSourcePicture(r, row);   // a foe's trophy, a cause's or a damage type's swatch
            float room = whoW - DrNameX, chip = string.IsNullOrEmpty(row.Value2) ? 0 : BadgeWidth(r, row.Value2) + 8;
            var name = BtText(r, row.Title, DrNameX, 0, room, line, DrName, PanelLook.Text);
            var nw = Mathf.Max(0, Mathf.Min(Mathf.Ceil(name.preferredWidth) + 2, room - chip));
            name.rectTransform.sizeDelta = new Vector2(nw, name.rectTransform.sizeDelta.y);
            if (chip > 0) Badge(r, row.Value2, DrNameX + nw + 8, (line - BadgeH) / 2);
            if (pips != null)
            {
                PipsAt(r, pips, DrNameX, line + 6, room);   // filled = defeated, ring = got away
                h = Mathf.Max(h, line + 6 + PipsHeight(pips, room));
            }
            else if (!string.IsNullOrEmpty(row.Note))
            {
                BtText(r, row.Note, DrNameX, line + 2, room, DrNoteH, PanelLook.MinText, PanelLook.Faint, style: FontStyles.Italic);   // a player's "live", "this PC", "near you 12 s"
                h = Mathf.Max(h, line + 2 + DrNoteH);
            }
            if (!string.IsNullOrEmpty(row.Value))
            {
                var num = BtText(r, row.Value, whoW + DrCol, 0, numW, line, DrName, dealt ? PanelLook.Gold : PanelLook.Text, TextAlignmentOptions.MidlineRight);
                // wider than its list's column (a weapons run measured from its first row): it grows to the left, the track stays where the list's is
                var need = Mathf.Ceil(num.preferredWidth) + 2; var rt = num.rectTransform;
                if (need > numW) { rt.anchoredPosition = new Vector2(whoW + DrCol + numW - need, rt.anchoredPosition.y); rt.sizeDelta = new Vector2(need, rt.sizeDelta.y); }
            }
            var form = PanelModel.BarFormOf(items.Where(i => i.Kind == "part").ToList(), true);
            var mix = items.Where(i => i.Kind == "mix").ToList();
            if (form.Rows.Count > 0) h = Mathf.Max(h, DamageBar(r, form, row.Tone == PanelModel.DamagePlayerTone ? PanelModel.BarFormOf(mix) : null, row.Fraction, dealt, trackX, trackW));
            else if (!string.IsNullOrEmpty(row.Text)) BtText(r, row.Text, trackX, 0, trackW, line, 16, PanelLook.Muted, style: FontStyles.Italic);   // a player with no number: their words where the bar would be
            Size(r, -1, h);
        }

        // the track, the bar with its parts, the true line and the share line; returns the height they take
        // mix: a player's damage by type for the line under their one-part bar (its entries key no part: no hover, no swap); null = the line keys the parts.
        // shares false (the feed's Log, one line a hit): no share line; a part too narrow for its number shows it in a small tag over the bar on hover
        static float DamageBar(RectTransform r, BarForm form, BarForm mix, float fraction, bool dealt, float x0, float trackW, bool shares = true)
        {
            int n = form.Rows.Count;
            var track = RoundedPart(r, "Track", DrTrack); track.pixelsPerUnitMultiplier = DrRadiusScale; track.rectTransform.Box(x0, 0, trackW, PanelModel.DamageTrack);
            var barW = trackW * PanelModel.DamageBarWidth(fraction);
            var bar = Node("Bar", r); bar.Box(x0, (PanelModel.DamageTrack - PanelModel.DamageBar) / 2, barW, PanelModel.DamageBar);
            var hover = r.gameObject.AddComponent<BarHover>(); hover.Init(n);
            // the parts on the bar's own width plus one gap (the first starts at the bar's start, the last ends at its end), each at least DamagePartMin wide
            var span = barW + PanelModel.DamageGap;
            var spans = PanelModel.BarParts(form.Rows.Select(x => x.Fraction).ToArray(), null, (PanelModel.DamagePartMin + PanelModel.DamageGap) / span);
            var swap = new bool[n]; var at = new (float x, float w)[n];
            for (int k = 0; k < n; k++)
            {
                var row = form.Rows[k]; var c = Hex(row.Colour, PanelLook.Accent);
                var seg = RoundedPart(bar, "Part", c, raycast: true); seg.pixelsPerUnitMultiplier = DrRadiusScale;
                var w = Mathf.Max(1f, (spans[k].to - spans[k].from) * span - PanelModel.DamageGap);
                seg.rectTransform.Box(spans[k].from * span, 0, w, PanelModel.DamageBar); at[k] = (spans[k].from * span, w);
                hover.Parts[k] = seg.rectTransform; var hp = seg.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = k;
                if (string.IsNullOrEmpty(row.Value)) continue;
                if (row.Other) { swap[k] = true; continue; }   // the folded rest carries no number inside; pointing at it shows it
                // the number inside the part where it fits at the book's floor, in the ink that reads on the colour; else in its share's place on hover
                if (!BarDigits(seg.transform, row.Value, w, SegmentInk(c))) swap[k] = true;
            }
            // the hover ring: one per bar, over its parts, clear until a part is pointed at
            var ring = PanelLook.Vocab("focus-ring") ? VocabImg(bar, "Hover", "focus-ring", Color.clear) : Img(bar, "Hover", PanelLook.RoundedEdge, Color.clear);
            if (ring.sprite == PanelLook.RoundedEdge) { ring.preserveAspect = false; ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 1f; }
            ring.raycastTarget = false; hover.RingImage = ring;

            // the true length from zero, under the bar
            var top = PanelModel.DamageTrack + PanelModel.DamageInner;
            RoundedPart(r, "True", dealt ? DrTrueDealt : DrTrueReceived).rectTransform.Box(x0, top, Mathf.Max(2f, trackW * PanelModel.DamageTrueWidth(fraction)), PanelModel.DamageTrue);
            top += PanelModel.DamageTrue + PanelModel.DamageInner;
            if (!shares)
            {
                // the tag over the bar, centred on its part (kept inside the track), shown while the part is pointed at (BarHover.SwapOn)
                for (int k = 0; k < n; k++)
                {
                    if (!swap[k]) continue;
                    var row = form.Rows[k];
                    var tag = RoundedPart(bar, "Tip", PlateColour); tag.raycastTarget = false;
                    var t = Label(tag.transform, row.Title + " " + row.Value, PanelLook.MinText, BarHover.LitText, style: FontStyles.Bold, align: TextAlignmentOptions.Center);
                    t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch();
                    var tw = Mathf.Ceil(t.preferredWidth) + 12;
                    tag.rectTransform.Box(Mathf.Clamp(at[k].x + at[k].w / 2 - tw / 2, -x0, trackW - tw), -2, tw, PanelModel.DamageBar + 4);
                    tag.gameObject.SetActive(false); hover.SwapOn[k] = tag.gameObject;
                }
                return top - PanelModel.DamageInner;
            }

            // one line of the parts and their share of this row (a player's: their damage by type), wrapping at the track's end
            float x = 0, y = top, lh = PanelModel.DamageShareLine;
            var line = mix ?? form;
            if (mix != null && mix.Rows.Count == 0) return PanelModel.DamageTrack + PanelModel.DamageInner + PanelModel.DamageTrue;
            for (int k = 0; k < line.Rows.Count; k++)
            {
                var row = line.Rows[k]; var c = Hex(row.Colour, PanelLook.Accent); var keyed = mix == null;
                var e = Node("Share", r);
                var words = Label(e, row.Title + " " + row.Share, PanelLook.MinText, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); words.textWrappingMode = TextWrappingModes.NoWrap;
                TextMeshProUGUI number = null;
                if (keyed && swap[k]) { number = Label(e, row.Title + " " + row.Value, PanelLook.MinText, BarHover.LitText, align: TextAlignmentOptions.MidlineLeft); number.textWrappingMode = TextWrappingModes.NoWrap; }
                var tw = Mathf.Max(Mathf.Ceil(words.preferredWidth), number == null ? 0 : Mathf.Ceil(number.preferredWidth)) + 2;
                var pic = (row.Part?.Icon ?? "").StartsWith("vocab:weapon-", System.StringComparison.Ordinal) ? DrKeyIcon : DrSwatch;
                var ew = pic + DrSwatchGap + tw;
                if (x > 0 && x + ew > trackW) { x = 0; y += lh + DrLineGap; }
                e.Box(x0 + x, y, ew, lh);
                // the entry's band (a little wider than it) is its pointer target, clear until it or its part is pointed at
                Image band = null;
                if (keyed)
                {
                    band = RoundedPart(e, "Lit", Color.clear, raycast: true); band.transform.SetAsFirstSibling();
                    var br = band.rectTransform; br.Stretch(); br.offsetMin = new Vector2(-5, -1); br.offsetMax = new Vector2(5, 1);
                }
                // the key: the part's swatch, or a weapon kind's own icon in the part's colour (By type)
                if (pic > DrSwatch) BtIcon(e, row.Part.Icon, c, 0, (lh - pic) / 2, pic);
                else { var sw = RoundedPart(e, "Swatch", c); sw.pixelsPerUnitMultiplier = 5f / 3f; sw.rectTransform.Box(0, (lh - DrSwatch) / 2, DrSwatch, DrSwatch); }
                words.rectTransform.Box(pic + DrSwatchGap, 0, tw, lh);
                if (number != null) { number.rectTransform.Box(pic + DrSwatchGap, 0, tw, lh); number.gameObject.SetActive(false); hover.SwapOn[k] = number.gameObject; hover.SwapOff[k] = words.gameObject; }
                if (keyed) { hover.Bands[k] = band; hover.Names[k] = words; hover.NameColours[k] = PanelLook.Muted; var hp = e.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = k; }
                x += ew + DrEntryGap;
            }
            return y + lh;
        }
    }
}
