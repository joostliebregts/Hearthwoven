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
    /// foes (0.8, Joost: one grammar for them all), and Defense's What your armor stopped (0.8.1: its parts outlined, StoppedPart).
    /// Pointing at any part rings it and lights its share, whose words swap for the part's name and number (HoverUi.cs; 0.8.2: every part, not
    /// only one too narrow for its number). Laid out once per fill in fixed rows, nothing per frame. Mirrored by dmgRows() in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        const float DrWho = 200, DrWhoMax = 230, DrNum = 64, DrCol = 12, DrNameX = 30, DrName = 17, DrDigits = 13, DrInsidePad = 6, DrSwatch = 10, DrKeyIcon = 14, DrSwatchGap = 5, DrEntryGap = 14, DrLineGap = 4, DrNoteH = 18, DrFillMin = 3, DrTextSlack = 2;
        static readonly Color DrTrack = PanelWash.Track.Game, DrTrueDealt = PanelWash.TrueDealt.Game, DrTrueReceived = PanelWash.TrueReceived.Game;   // the mirror's .dr-track and .dr-true as the game draws them (PanelWash.cs)

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

        /// <summary>A part drawn as a whole with what got through of it (PanelModel.DamageStoppedTone; Defense's What your armor stopped, and 0.8.5
        /// damage dealt after the foe's armor): the part itself is its outline in its colour (<paramref name="seg"/>, PanelLook.DamageEdge: one image,
        /// 0.8.5, where four edges were), filled from the left by what got through (at least DrFillMin of each where both have an amount); the
        /// got-through number in the fill (its ink on the colour), the stopped number in the outline (the colour lifted to read on the track).
        /// 0.8.5: a part with an Earlier share starts with that piece faded (the colour at FadedAlpha, as every faded part: from before it was
        /// split), no number, then the fill. Returns which numbers did not fit: the part's entry shows them on hover ("Pierce 12 got through",
        /// "Pierce 6 stopped"). A piece without an amount draws no number.</summary>
        static (bool through, bool stopped) StoppedPart(Image seg, BarRow row, float w, Color c)
        {
            if (PanelLook.DamageEdge) { seg.sprite = PanelLook.DamageEdge; seg.type = Image.Type.Sliced; seg.pixelsPerUnitMultiplier = DrRadiusScale; seg.color = c; }
            else BtHollow(seg.rectTransform, c, 0, 0, w, PanelModel.DamageBar);
            var m = Mathf.Min(DrFillMin, w / 2);
            var e = Mathf.Clamp01(row.Part.Earlier); var ew = e <= 0 ? 0 : e >= 1 ? w : Mathf.Clamp(w * e, m, w - m);
            var f = Mathf.Clamp01(row.Part.Fraction2); var room = w - ew;
            var fw = f <= 0 || room <= 0 ? 0 : Mathf.Clamp(w * f, Mathf.Min(m, room), room - (row.Part.Value2 != null ? Mathf.Min(m, room / 2) : 0));   // room for the outline's piece when something was stopped
            bool Some(string v) => !string.IsNullOrEmpty(v) && v != "0";
            bool through = false, stopped = false;
            if (ew > 0) { var early = RoundedPart(seg.transform, "Earlier", Faded(c)); early.pixelsPerUnitMultiplier = DrRadiusScale; early.rectTransform.Box(0, 0, ew, PanelModel.DamageBar); }
            if (fw > 0)
            {
                var fill = RoundedPart(seg.transform, "Fill", c); fill.pixelsPerUnitMultiplier = DrRadiusScale; fill.rectTransform.Box(ew, 0, fw, PanelModel.DamageBar);
                if (Some(row.Value)) through = !BarDigits(fill.transform, row.Value, fw, SegmentInk(c));
            }
            var rest = w - ew - fw;
            if (rest > 0 && Some(row.Part.Value2)) stopped = !BarDigitsAt(seg.transform, row.Part.Value2, ew + fw, rest - BtArmLine, Legible(c));
            return (through, stopped);
        }

        /// <summary>A colour as a faded part draws it: FadedAlpha of it over the plate's dark (PanelUi.FadedVeil's look, in one image).</summary>
        static Color Faded(Color c) => Color.Lerp(FadedVeil, c, FadedAlpha) is var f ? new Color(f.r, f.g, f.b, 1f) : c;

        /// <summary>BarDigits in a box of <paramref name="parent"/> from <paramref name="x"/>, <paramref name="w"/> wide, without a holder of its own
        /// (0.8.5: one object less per stopped part).</summary>
        static bool BarDigitsAt(Transform parent, string text, float x, float w, Color ink)
        {
            if (!BarDigits(parent, text, w, ink)) return false;
            var t = parent.GetChild(parent.childCount - 1) as RectTransform;
            if (t != null) t.Box(x, 0, w, PanelModel.DamageBar);
            return true;
        }

        // the name column (REVIEW-081 #7): as wide as the list's widest picture, name and "×N" ("Greydwarf Shaman ×3"), DrWho at least and DrWhoMax at
        // most: one column for the list, so its rows keep one x and one scale. A name still too wide gives way (an ellipsis), never its "×N".
        // least: a list of short names (While away's players) takes less
        static float NameColumn(RectTransform parent, IEnumerable<Block> rows, float least = DrWho)
        {
            var measure = Label(parent, "", DrName, PanelLook.Text); measure.textWrappingMode = TextWrappingModes.NoWrap;
            float whoW = least;
            foreach (var r in rows)
            {
                measure.text = r.Title ?? "";
                whoW = Mathf.Max(whoW, DrNameX + Mathf.Ceil(measure.preferredWidth) + 2 + (string.IsNullOrEmpty(r.Value2) ? 0 : 8 + BadgeWidth(parent, r.Value2)));
            }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
            return Mathf.Min(whoW, DrWhoMax);
        }

        // the number column: DrNum (or least), wider when the list's widest number needs it
        static float NumberColumn(RectTransform parent, IEnumerable<string> values, float least = DrNum)
        {
            var measure = Label(parent, "", DrName, PanelLook.Text); measure.textWrappingMode = TextWrappingModes.NoWrap;
            float numW = least;
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
            else { dmgRunLabel = null; if (Labelled(b)) dmgRunLabel = Since(dmgRun, PanelLayout.Text.Floor, b); }
        }

        /// <summary>One damage row in <paramref name="list"/>; returns it, so a page can add a column of its own right of the track (While away's For you).</summary>
        static RectTransform DamageRow(RectTransform list, Block row, bool dealt, float whoW, float numW, float trackX, float trackW)
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
            if (form.Rows.Count > 0) h = Mathf.Max(h, DamageBar(r, form, row.Tone == PanelModel.DamagePlayerTone ? PanelModel.BarFormOf(mix) : null, row.Fraction, dealt, trackX, trackW, shares: row.Tone != PanelModel.DamageNoLineTone, closing: row.Text));
            else if (!string.IsNullOrEmpty(row.Text)) BtText(r, row.Text, trackX, 0, trackW, line, PanelLayout.Text.Body, PanelLook.Muted, style: FontStyles.Italic);   // a player with no number: their words where the bar would be
            Size(r, -1, h);
            return r;
        }

        /// <summary>The page's one hover tag ("Frost 37", "Edda 350"; HoverTag): made with the first bar of this drawing that needs it, hidden, out of
        /// the page's layout, in the page's content (Fill draws it last, over every block); pointing at a part of a bar no share entry keys sets
        /// its words and moves it over the part (BarHover.TagOver: centred on the part, or from a start, kept inside the bar's room). 0.8.2: one
        /// for the page, never one per part (two objects a part at about 58 µs an object, the 0.8.1 bench).</summary>
        static HoverTag pageTag; static RectTransform pageTagHome;
        static HoverTag PageTag(RectTransform near)
        {
            if (pageTag != null && pageTag.Ground) return pageTag;
            var home = pageTagHome ? pageTagHome : near;
            var tag = RoundedPart(home, "Tip", PlateColour); tag.raycastTarget = false;
            tag.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tag.rectTransform.Box(0, 0, 2 * HoverTag.Pad, PanelModel.DamageBar + HoverTag.Rise);
            var t = Label(tag.transform, "", PanelLook.MinText, BarHover.LitText, style: FontStyles.Bold, align: TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch();
            tag.gameObject.SetActive(false);
            return pageTag = new HoverTag { Ground = tag, Text = t };
        }

        /// <summary>A drawing of the page starts: its tag is made again with its first bar that needs one (Clear took the last).</summary>
        static void NewPageTag(RectTransform home) { pageTag = null; pageTagHome = home; }

        /// <summary>The page drawn: its tag over every block.</summary>
        static void PageTagOnTop() { if (pageTag != null && pageTag.Ground && pageTag.Ground.transform.parent == pageTagHome) pageTag.Ground.transform.SetAsLastSibling(); }

        /// <summary>A damage bar's hover ring (BarHover): one per bar, over its parts (drawn after them in <paramref name="parent"/>), clear until a
        /// part is pointed at.</summary>
        static void HoverRing(RectTransform parent, BarHover hover)
        {
            var ring = PanelLook.Vocab("focus-ring") ? VocabImg(parent, "Hover", "focus-ring", Color.clear) : Img(parent, "Hover", PanelLook.RoundedEdge, Color.clear);
            if (ring.sprite == PanelLook.RoundedEdge) { ring.preserveAspect = false; ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 1f; }
            ring.raycastTarget = false; hover.RingImage = ring;
        }

        // the track, the bar with its parts, the true line and the share line; returns the height they take
        // mix: a player's damage by type for the line under their one-part bar (its entries key no part: no hover, no swap); null = the line keys the parts.
        // shares false (the feed's Log, one line a hit): no share line.
        // 0.8.2 (Joost in game): pointing at ANY part shows its name and number (PanelModel.DamageHoverWords): its share entry's words swap to them,
        // or, where no entry keys it (no share line, a player's line), the page's one tag says them over the bar; a number that fits stays inside
        // the part too. No object per part for it (0.8.1's speed): the parts are the pointer targets, the entry keeps its one label
        // closing (0.8.5): words after the line's entries, a row with parts' Text ("73 % got through", PanelModel.ThroughLine): one label, no key
        // trueLine false (0.8.5, the overview's What hurt you): one row has nothing to compare with, so no true-length line under it
        static float DamageBar(RectTransform r, BarForm form, BarForm mix, float fraction, bool dealt, float x0, float trackW, bool shares = true, string closing = null, bool trueLine = true)
        {
            int n = form.Rows.Count;
            var track = RoundedPart(r, "Track", DrTrack); track.pixelsPerUnitMultiplier = DrRadiusScale; track.rectTransform.Box(x0, 0, trackW, PanelModel.DamageTrack);
            var barW = trackW * PanelModel.DamageBarWidth(fraction);
            var bar = Node("Bar", r); bar.Box(x0, (PanelModel.DamageTrack - PanelModel.DamageBar) / 2, barW, PanelModel.DamageBar);
            var hover = r.gameObject.AddComponent<BarHover>(); hover.Init(n);
            // the parts on the bar's own width plus one gap (the first starts at the bar's start, the last ends at its end), each at least DamagePartMin wide
            var span = barW + PanelModel.DamageGap;
            var spans = PanelModel.BarParts(form.Rows.Select(x => x.Fraction).ToArray(), null, (PanelModel.DamagePartMin + PanelModel.DamageGap) / span);
            var mid = new float[n];   // each part's middle in the bar's x (where the page's tag centres on it)
            var hoverWords = form.Rows.Select(PanelModel.DamageHoverWords).ToArray();   // what pointing at each part shows (every part, 0.8.2)
            for (int k = 0; k < n; k++)
            {
                var row = form.Rows[k]; var c = Hex(row.Colour, PanelLook.Accent); var stopped = PanelModel.IsStoppedPart(row);
                var seg = RoundedPart(bar, "Part", stopped ? Color.clear : c, raycast: true); seg.pixelsPerUnitMultiplier = DrRadiusScale;
                var w = Mathf.Max(1f, (spans[k].to - spans[k].from) * span - PanelModel.DamageGap);
                seg.rectTransform.Box(spans[k].from * span, 0, w, PanelModel.DamageBar); mid[k] = spans[k].from * span + w / 2;
                hover.Parts[k] = seg.rectTransform; var hp = seg.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = k;
                if (stopped) { StoppedPart(seg, row, w, c); continue; }
                if (string.IsNullOrEmpty(row.Value) || row.Other) continue;   // the folded rest carries no number inside
                BarDigits(seg.transform, row.Value, w, SegmentInk(c));   // the number inside the part where it fits at the book's floor, in the ink that reads on the colour
            }
            HoverRing(bar, hover);

            // the true length from zero, under the bar
            var top = PanelModel.DamageTrack + PanelModel.DamageInner;
            if (trueLine)
            {
                RoundedPart(r, "True", dealt ? DrTrueDealt : DrTrueReceived).rectTransform.Box(x0, top, Mathf.Max(2f, trackW * PanelModel.DamageTrueWidth(fraction)), PanelModel.DamageTrue);
                top += PanelModel.DamageTrue + PanelModel.DamageInner;
            }
            // no entry keys the parts (no share line, or a player's line of their damage by type), or (0.8.5) a row of damage dealt with its after,
            // whose words ("Slash 1 670: 770 got through, 900 stopped") would widen every entry of its line: the page's tag over the bar, centred on
            // the part pointed at (kept inside the track); the entries keep their own words and light with their part
            var tagged = !shares || mix != null || form.Rows.Any(x => x.Part?.Whole != null);
            if (tagged) hover.TagOver(PageTag(r), bar, hoverWords, mid, -2, -x0, trackW);
            if (!shares) return top - PanelModel.DamageInner;

            // one line of the parts and their share of this row (a player's: their damage by type), wrapping at the track's end
            float x = 0, y = top, lh = PanelModel.DamageShareLine;
            var line = mix ?? form;
            if (mix != null && mix.Rows.Count == 0) return PanelModel.DamageTrack + PanelModel.DamageInner + PanelModel.DamageTrue;
            if (mix == null) { hover.OnWords = tagged ? null : hoverWords; hover.OffWords = new string[n]; }
            for (int k = 0; k < line.Rows.Count; k++)
            {
                var row = line.Rows[k]; var c = Hex(row.Colour, PanelLook.Accent); var keyed = mix == null;
                var e = Node("Share", r);
                var said = PanelModel.DamageLineWords(row);
                var words = Label(e, said, PanelLook.MinText, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); words.textWrappingMode = TextWrappingModes.NoWrap;
                // a keyed entry's words swap for the part's name and number on hover (BarHover.OnWords): its width is kept for the longer of the two
                var tw = Mathf.Max(Mathf.Ceil(words.preferredWidth), keyed && !tagged ? Mathf.Ceil(words.GetPreferredValues(hoverWords[k]).x) : 0) + 2;
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
                if (keyed) { hover.Bands[k] = band; hover.Names[k] = words; hover.NameColours[k] = PanelLook.Muted; hover.OffWords[k] = said; var hp = e.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = k; }
                x += ew + DrEntryGap;
            }
            if (!string.IsNullOrEmpty(closing) && mix == null)
            {
                var t = Label(r, closing, PanelLayout.Text.Floor, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap;
                var cw = Mathf.Ceil(t.preferredWidth) + DrTextSlack;
                if (x > 0 && x + cw > trackW) { x = 0; y += lh + DrLineGap; }
                t.rectTransform.Box(x0 + x, y, cw, lh);
            }
            return y + lh;
        }
    }
}
