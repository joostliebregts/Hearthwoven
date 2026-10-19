using System;
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
        static void DamageRows(RectTransform col, IList<Block> rows, bool dealt, Func<string, Action> link = null)
        {
            if (rows == null || rows.Count == 0) return;
            var list = VStack(col, PanelModel.DamageFoeGap);
            float whoW = NameColumn(list, rows), numW = NumberColumn(list, rows.Select(r => r.Value));
            if (rows.Count == 1 && IsPlanMeal(rows[0])) { whoW = PlanSlotsWidth(rows[0]); numW = 0; }   // 0.9.6 Meal plan's pinned meal: the slots alone, no number column, the bar right after them
            // 0.9.2 Meals by dish: what one serving gives, a column of its own right of the track (the eaten bar gives way)
            var servW = rows.Any(r => ServingOf(r) != null) ? ServingColumn(list, rows) : 0f;
            float trackX = whoW + DrCol + numW + DrCol, trackW = Mathf.Max(80f, Column - trackX - (servW > 0 ? DrCol + servW : 0f));
            foreach (var row in rows)
            {
                var r = DamageRow(list, row, dealt, whoW, numW, trackX, trackW, link);
                if (servW > 0) ServingCell(r, ServingOf(row), Column - servW);
            }
        }

        // 0.9.2 Meals by dish, what one serving gives (PanelModel.ServingOf; the mirror's .dr-serv): the bar's longest, its height, the gap between
        // its parts and from it to the numbers (at the book's 14 px floor, muted)
        const float DrServBar = 90, DrServH = 10, DrServPartGap = 2, DrServGap = 8, DrServPartMin = 2;
        static Block ServingOf(Block row) => row?.Items?.FirstOrDefault(i => i.Kind == PanelModel.ServingKind);
        static float ServingColumn(RectTransform parent, IList<Block> rows)
        {
            var measure = Label(parent, "", PanelLook.MinText, PanelLook.Muted); measure.textWrappingMode = TextWrappingModes.NoWrap;
            float w = 0;
            foreach (var r in rows) { var s = ServingOf(r); if (s == null) continue; measure.text = s.Text ?? ""; w = Mathf.Max(w, Mathf.Ceil(measure.preferredWidth) + DrTextSlack); }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
            return DrServBar + DrServGap + w;
        }
        // one row's serving on the bar's line: the small bar (its parts as long as their values, the whole on the list's one scale), then its numbers;
        // a dish the game's data gives no food value: nothing (never a 0)
        static void ServingCell(RectTransform row, Block s, float x0)
        {
            if (s == null) return;
            var parts = (s.Items ?? new List<Block>()).Where(p => p.Fraction > 0).ToList();
            var barW = Mathf.Max(parts.Count * DrFillMin, DrServBar * Mathf.Clamp01(s.Fraction2));
            var room = barW - DrServPartGap * Mathf.Max(0, parts.Count - 1); float x = x0, y = (PanelModel.DamageTrack - DrServH) / 2;
            foreach (var p in parts)
            {
                var w = Mathf.Max(DrServPartMin, room * p.Fraction);
                var seg = RoundedPart(row, "Serving", Hex(p.Colour, PanelLook.Accent)); seg.pixelsPerUnitMultiplier = DrRadiusScale; seg.rectTransform.Box(x, y, w, DrServH);
                x += w + DrServPartGap;
            }
            var t = Label(row, s.Text ?? "", PanelLook.MinText, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.Box(x0 + DrServBar + DrServGap, 0, Mathf.Ceil(t.preferredWidth) + DrTextSlack, PanelModel.DamageTrack);
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
        /// got-through number in the fill (its ink on the colour). 0.8.6 (Joost: "you want to know what comes through"): the stopped number is never on
        /// the bar, the part's hover says it ("Slash 53: 33 got through, 20 stopped"). Returns whether the got-through number did not fit (hover has it
        /// too).</summary>
        static bool StoppedPart(Image seg, BarRow row, float w, Color c)
        {
            if (PanelLook.DamageEdge) { seg.sprite = PanelLook.DamageEdge; seg.type = Image.Type.Sliced; seg.pixelsPerUnitMultiplier = DrRadiusScale; seg.color = c; }
            else BtHollow(seg.rectTransform, c, 0, 0, w, PanelModel.DamageBar);
            var m = Mathf.Min(DrFillMin, w / 2);
            var f = Mathf.Clamp01(row.Part.Fraction2);
            var fw = f <= 0 ? 0 : Mathf.Clamp(w * f, m, w - (row.Part.Value2 != null ? Mathf.Min(m, w / 2) : 0));   // room for the outline's piece when something was stopped
            bool Some(string v) => !string.IsNullOrEmpty(v) && v != "0";
            if (fw <= 0) return false;
            var fill = RoundedPart(seg.transform, "Fill", c); fill.pixelsPerUnitMultiplier = DrRadiusScale; fill.rectTransform.Box(0, 0, fw, PanelModel.DamageBar);
            return Some(row.Value) && !BarDigits(fill.transform, row.Value, fw, SegmentInk(c));
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
                whoW = Mathf.Max(whoW, DrNameX + Mathf.Ceil(measure.preferredWidth) + 2 + (KindMarks(r) ? DrKindMark : 0) + (string.IsNullOrEmpty(r.Value2) ? 0 : 8 + BadgeWidth(parent, r.Value2)));
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

        static void DamageRunRow(RectTransform col, Block b, Func<string, Action> link = null)
        {
            if (!(dmgRun != null && dmgRun.parent == col && dmgRun.GetSiblingIndex() == col.childCount - 1))
            {
                dmgRun = VStack(col, PanelModel.DamageFoeGap); dmgRunLabel = null;
                dmgRunNumW = NumberColumn(dmgRun, new[] { PanelModel.DamageWidestNumber(b), b.Value }); dmgRunWhoW = NameColumn(dmgRun, new[] { b });   // the weapon kinds' names are short: DrWho
            }
            float trackX = dmgRunWhoW + DrCol + dmgRunNumW + DrCol, trackW = Mathf.Max(80f, Column - trackX);   // 0.9.5: a kind row's opens mark stands after its name (KindOpens), so its bar keeps the whole track
            DamageRow(dmgRun, b, true, dmgRunWhoW, dmgRunNumW, trackX, trackW, link);
            // the run's label only while it is still this run's (PanelReuse.cs: a plain text a cleared page left may be handed out again elsewhere)
            if (dmgRunLabel && dmgRunLabel.transform.parent == dmgRun) dmgRunLabel.transform.SetAsLastSibling();
            else { dmgRunLabel = null; if (Labelled(b)) dmgRunLabel = Since(dmgRun, PanelLayout.Text.Floor, b); }
        }

        // 0.8.6 (DPS per named weapon): a kind's weapons under its row, drawn into the same list (the run the kind's row started stays the last child): per line the
        // weapon's name (an upgrade level under it a little in), its damage right-aligned in the rows' number column and, where the bars start, the words after the
        // damage ("level 3 · 42 DPS ▲7 · Clubs 38–45": the mark is a picture, as Compare's). A line that opens (a weapon of several upgrade levels, folded to its
        // newest: "level 3 · 149 DPS at 35 · 2 levels"; a line with two skill levels or more) opens in place (press, the ›, the row lit under the pointer, as a foe on
        // Foes; the keys' cursor wears the focus ring): its level lines, or one row per skill level, its name, pace and a small bar on one scale. Text only, no object
        // per part; mirrored by btWeapons() in preview/panel-preview.html
        static void WeaponLines(RectTransform col, Block b, Func<string, Action> link)
        {
            if (!(dmgRun != null && dmgRun.parent == col && dmgRun.GetSiblingIndex() == col.childCount - 1))
            {
                dmgRun = VStack(col, PanelModel.DamageFoeGap); dmgRunLabel = null;
                dmgRunNumW = NumberColumn(dmgRun, b.Items.Select(i => i.Value)); dmgRunWhoW = DrWho;
            }
            float trackX = dmgRunWhoW + DrCol + dmgRunNumW + DrCol, trackW = Mathf.Max(DrTrackLeast, Column - trackX), h = PanelModel.DamageTrack, size = PanelLayout.Text.Body;
            foreach (var line in b.Items)
            {
                var r = Node("Weapon", dmgRun); Size(r, -1, h);
                if (line.Kind == PanelModel.WeaponSkillKind)
                {
                    BtText(r, line.Title, DrNameX + 2 * DrLevelIndent, 0, Mathf.Max(DrNameLeast, dmgRunWhoW - DrNameX - 2 * DrLevelIndent), h, size, PanelLook.Muted);
                    BtText(r, line.Value, dmgRunWhoW + DrCol, 0, dmgRunNumW, h, size, PanelLook.Muted, TextAlignmentOptions.MidlineRight);   // its pace in the number column
                    var room = trackW - BtOpensW; var w = line.Fraction > 0f ? Mathf.Max(DrSkillBarLeast, Mathf.Clamp01(line.Fraction) * room) : 0f;
                    if (w > 0f) Fill(r, "Level", PanelLook.Dealt).rectTransform.Box(trackX, (h - DrSkillBarH) / 2, w, DrSkillBarH);
                    continue;
                }
                var level = line.Tone == PanelModel.WeaponLevelTone; var x = DrNameX + (level ? DrLevelIndent : 0f);
                if (line.Opens && link != null)   // the whole line opens (or closes) its skill levels; pointing at it lights it
                {
                    var ground = RoundedPart(r, "Ground", Color.clear, raycast: true); ground.rectTransform.Stretch();
                    if (!line.Selected) { var lit = r.gameObject.AddComponent<TintOnHover>(); lit.Ground = ground; lit.Lit = BtRowHover; }
                    r.gameObject.AddComponent<Press>().Act = link(PanelModel.WeaponOpenLink(line));
                    OpensMark(r, line.Selected, BtOpensRight);
                    if (line.Cursor) { FocusRing(r, 0); foeCursorRow = r; }   // the keys' cursor (WeaponKeys): the focus ring, brought into view as a foe's
                }
                BtText(r, line.Title, x, 0, Mathf.Max(DrNameLeast, dmgRunWhoW - x), h, size, level ? PanelLook.Muted : PanelLook.Text);
                if (!string.IsNullOrEmpty(line.Value)) BtText(r, line.Value, dmgRunWhoW + DrCol, 0, dmgRunNumW, h, size, PanelLook.Gold, TextAlignmentOptions.MidlineRight);
                var room2 = trackW - (line.Opens ? BtOpensW : 0f);
                if (string.IsNullOrEmpty(line.Text) && string.IsNullOrEmpty(line.Note)) continue;   // no DPS yet and no skill words: nothing after the damage (never a dash)
                if (string.IsNullOrEmpty(line.Change)) BtText(r, string.IsNullOrEmpty(line.Note) ? line.Text : string.IsNullOrEmpty(line.Text) ? line.Note : line.Text + " · " + line.Note, trackX, 0, room2, h, size, PanelLook.Muted);
                else   // the change mark between the words: a picture, then its number (CompareUi.ChangeMarkUi)
                {
                    var words = Line(r, DrMarkGap, TextAnchor.MiddleLeft); words.Box(trackX, 0, room2, h);
                    Label(words, line.Text, size, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
                    ChangeMarkUi(words, line.Change, size, PanelLook.Muted);
                    if (!string.IsNullOrEmpty(line.Note)) Label(words, "· " + line.Note, size, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
                }
            }
            if (dmgRunLabel && dmgRunLabel.transform.parent == dmgRun) dmgRunLabel.transform.SetAsLastSibling();   // the run's "Recorded from" line stays after its last row
        }
        // 0.9.5 (Joost in game: "Can we get a toggle on 'Melee' and 'Bow' etc.."): a weapon kind's row on By weapon shuts or opens the weapon lines under it
        // (PanelModel.KindRows), as a weapon line that opens does: the whole row a press, lit under the pointer while shut, the book's opens mark right after the
        // kind's name, before its DPS chip (Joost, 19 Oct: "on the actual name ... closer"), turned down while open; the keys' cursor in the focus ring. The name
        // column measures the mark's room (NameColumn), so the column keeps its width and the bar its whole track. Mirrored by dmgRows' kind rows in preview/panel-preview.html
        static void KindOpens(RectTransform row, Block b, Action toggle, float markX)
        {
            var ground = RoundedPart(row, "Ground", Color.clear, raycast: true); ground.rectTransform.Stretch(); ground.transform.SetAsFirstSibling();
            if (!b.Selected) { var lit = row.gameObject.AddComponent<TintOnHover>(); lit.Ground = ground; lit.Lit = BtRowHover; }
            row.gameObject.AddComponent<Press>().Act = toggle;
            var r = Node("Head", row); r.Box(markX, 0, PanelLook.ChevronW, PanelModel.DamageTrack);   // the mark's own box on the name's line, after the name
            OpensMark(r, b.Selected, 0);
            if (b.Cursor) { FocusRing(row, 0); foeCursorRow = row; }   // the keys' cursor (WeaponKeys), brought into view as a weapon line's
        }

        // 0.9 Meals' Your combinations: a dish's picture, the gap to the next and the names' gap under them (the mirror's .dr-combo). The picture is What to
        // use's item picture (UtIcon, 40), the size the panel uses for an item you must recognize (Joost, 18 Oct: "I always look at the icons"; at 22 px
        // the dishes did not read); the row grows to hold it, the bar stays at the row's top as every damage row's
        const float DrComboIcon = UtIcon, DrComboGap = 4, DrComboUnder = 2;

        // 0.9.6 Meal plan's pinned meal (Joost, the pinned meal on screen: "just make it efficient with the margin/padding. let's ensure the food text doesn't
        // need 2 lines. We know what we clicked. Icons only would be good. And then the bar all at one row height"): one row as tall as a slot (DrComboIcon).
        // The three slots as pictures only, a food's name in a tag under its slot while the pointer is on it (SlotNameTag); a filled slot's click still
        // empties it. Then the bar on the rest of the row, centred on the slots' height, its legend ("Health 141 · Stamina 141 · first wears off after 25
        // min") on the line under it, no true-length line (the meal has one length). Nothing picked: the empty track, "Pick foods below" in the legend's
        // place (PlanMealBar). The mirror: dmgRows' plan row in preview/panel-preview.html; render-check's pinned-meal rule measures it
        static bool IsPlanMeal(Block row) => row != null && row.Tone == PanelModel.ComboTone && row.Id == PanelModel.MealPlanId;
        static float PlanSlotsWidth(Block row)
        {
            var n = (row.Items ?? new List<Block>()).Count(i => i.Kind == PanelModel.ComboDishKind);
            return n > 0 ? n * DrComboIcon + (n - 1) * DrComboGap : 0;
        }

        // the pinned meal's bar, centred on the slots' height (a node of its own, moved once it is drawn); returns the row's height
        static float PlanMealBar(RectTransform r, BarForm form, Block row, float trackX, float trackW)
        {
            var host = Node("Meal", r);
            float bh;
            if (form.Rows.Count > 0) bh = DamageBar(host, form, null, row.Fraction, false, trackX, trackW, closing: row.Text, trueLine: false, exact: true);
            else
            {
                var track = RoundedPart(host, "Track", DrTrack); track.pixelsPerUnitMultiplier = DrRadiusScale; track.rectTransform.Box(trackX, 0, trackW, PanelModel.DamageTrack);
                var top = PanelModel.DamageTrack + PanelModel.DamageInner;
                if (!string.IsNullOrEmpty(row.Text)) BtText(host, row.Text, trackX, top, trackW, PanelModel.DamageShareLine, PanelLayout.Text.Floor, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
                bh = top + PanelModel.DamageShareLine;
            }
            host.Box(0, Mathf.Max(0, Mathf.Round((DrComboIcon - bh) / 2)), trackX + trackW, bh);
            return Mathf.Max(DrComboIcon, bh);
        }

        // a food's name under its slot while the pointer is on it (SwapOnHover), in the page tag's look (PageTag: opaque in the plate's colour, bold at the
        // book's floor); its own canvas sorts it over the foods that scroll under the pinned meal (as a greyed chip's reason, ReasonTag)
        static GameObject SlotNameTag(RectTransform row, string words, float x, float top)
        {
            var tag = Node("Name", row);
            tag.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var canvas = tag.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true;
            var root = row.GetComponentInParent<Canvas>();
            if (root) { canvas.sortingLayerID = root.rootCanvas.sortingLayerID; canvas.sortingOrder = root.rootCanvas.sortingOrder + 5; }
            var ground = RoundedPart(tag, "Ground", new Color(PlateColour.r, PlateColour.g, PlateColour.b, 1f)); ground.raycastTarget = false; ground.rectTransform.Stretch();
            var t = Label(tag, words, PanelLook.MinText, BarHover.LitText, style: FontStyles.Bold, align: TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch();
            tag.Box(x, top + HoverTag.Apart, Mathf.Ceil(t.preferredWidth) + 2 * HoverTag.Pad, PanelModel.DamageBar + HoverTag.Rise);
            tag.gameObject.SetActive(false);
            return tag.gameObject;
        }
        const float DrLevelIndent = 16, DrTrackLeast = 80, DrNameLeast = 20;   // an upgrade level under its weapon a little in; the least room of the words after the damage and of a name
        const float DrSkillBarH = 8, DrSkillBarLeast = 3, DrMarkGap = 6;   // an open skill level: its bar's height and least length; the gap round a change mark
        const float DrKindMarkGap = 6, DrKindMark = DrKindMarkGap + PanelLook.ChevronW;   // 0.9.5 a kind row's opens mark: its gap after the name (the name's box keeps 2 px slack) and the room it takes
        static bool KindMarks(Block row) => row.Opens && row.Tone == PanelModel.KindTone;   // a weapon kind's row that shuts or opens its lines (KindOpens)

        /// <summary>One damage row in <paramref name="list"/>; returns it, so a page can add a column of its own right of the track (While away's For you).</summary>
        static RectTransform DamageRow(RectTransform list, Block row, bool dealt, float whoW, float numW, float trackX, float trackW, Func<string, Action> link = null)
        {
            var r = Node("Foe", list);
            var items = row.Items ?? new List<Block>();
            var pips = items.FirstOrDefault(i => i.Kind == "pips");
            float h = PanelModel.DamageTrack, line = PanelModel.DamageTrack;
            // the name column, centred on the bar's track: picture, name (a long one gives way), the "×N" chip right after it
            var plan = IsPlanMeal(row);   // 0.9.6 Meal plan's pinned meal: one row, the slots as pictures only (PlanMealBar)
            if (row.Tone == PanelModel.ComboTone)   // 0.9 Meals' Your combinations: the dishes' pictures side by side, their names on the line under them
            {
                var dishes = items.Where(i => i.Kind == PanelModel.ComboDishKind).ToList();
                for (int k = 0; k < dishes.Count; k++)
                {
                    var dx = k * (DrComboIcon + DrComboGap);
                    if (dishes[k].Tone == PanelModel.PlanEmptyTone)   // 0.9.2 Plan a meal: an empty slot, dashed as a piece not made yet (Armor sets)
                    {
                        var slot = Node("Slot", r); slot.Box(dx, 0, DrComboIcon, DrComboIcon); DashEdge(slot, DrComboIcon, DrComboIcon, NotMadeEdge);
                        continue;
                    }
                    BtMarker(r, dishes[k].Icon, dx, 0, DrComboIcon);
                    var empty = !string.IsNullOrEmpty(dishes[k].Link) ? link?.Invoke(dishes[k].Link) : null;
                    if (empty != null || (plan && !string.IsNullOrEmpty(dishes[k].Title)))   // a filled slot: its click empties it; the pinned meal's says its food on hover
                    {
                        var hit = Img(r, "Slot", null, Color.clear, raycast: true); hit.rectTransform.Box(dx, 0, DrComboIcon, DrComboIcon);
                        if (empty != null) hit.gameObject.AddComponent<Press>().Act = empty;
                        if (plan && !string.IsNullOrEmpty(dishes[k].Title)) hit.gameObject.AddComponent<SwapOnHover>().On = SlotNameTag(r, dishes[k].Title, dx, DrComboIcon);
                    }
                }
                if (plan) h = DrComboIcon;   // 0.9.6: no names under the pinned meal's slots (their hover says them)
                else
                {
                    // the names wrap onto a second line rather than give way (three long names are wider than the column; never a cut name)
                    var names = BtText(r, row.Title, 0, DrComboIcon + DrComboUnder, whoW, DrNoteH, PanelLook.MinText, PanelLook.Muted, TextAlignmentOptions.TopLeft);
                    names.textWrappingMode = TextWrappingModes.Normal; names.overflowMode = TextOverflowModes.Overflow;
                    var nh = Mathf.Max(DrNoteH, Mathf.Ceil(names.GetPreferredValues(row.Title ?? "", whoW, 0).y));
                    names.rectTransform.sizeDelta = new Vector2(names.rectTransform.sizeDelta.x, nh);
                    h = Mathf.Max(h, DrComboIcon + DrComboUnder + nh);
                }
            }
            else if ((row.Icon ?? "").StartsWith("vocab:weapon-", System.StringComparison.Ordinal)) BtIcon(r, row.Icon, PanelLook.Gold, 0, 1, 22);   // a weapon kind (By weapon)
            else BattleSourcePicture(r, row);   // a foe's trophy, a cause's or a damage type's swatch
            float room = whoW - DrNameX, chip = string.IsNullOrEmpty(row.Value2) ? 0 : BadgeWidth(r, row.Value2) + 8, mark = KindMarks(row) ? DrKindMark : 0, markX = 0;
            if (row.Tone != PanelModel.ComboTone)
            {
                var name = BtText(r, row.Title, DrNameX, 0, room, line, DrName, PanelLook.Text);
                var nw = Mathf.Max(0, Mathf.Min(Mathf.Ceil(name.preferredWidth) + 2, room - chip - mark));
                name.rectTransform.sizeDelta = new Vector2(nw, name.rectTransform.sizeDelta.y);
                markX = DrNameX + nw + DrKindMarkGap;   // 0.9.5 a kind row's opens mark right after its name (KindOpens), its chip after the mark
                if (chip > 0) Badge(r, row.Value2, DrNameX + nw + mark + 8, (line - BadgeH) / 2);
            }
            if (row.Tone == PanelModel.ComboTone) { }   // its names stand under its pictures (above)
            else if (pips != null)
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
            // 0.9.2 Meals' Your combinations: the bar its food values on the list's one scale from zero (no floor), the thin line its meals (Fraction2)
            var combo = row.Tone == PanelModel.ComboTone;
            if (plan) h = PlanMealBar(r, form, row, trackX, trackW);   // 0.9.6: centred on the slots, its legend under it, one row
            else if (form.Rows.Count > 0) h = Mathf.Max(h, DamageBar(r, form, row.Tone == PanelModel.DamagePlayerTone ? PanelModel.BarFormOf(mix) : null, row.Fraction, dealt, trackX, trackW, shares: row.Tone != PanelModel.DamageNoLineTone, closing: row.Text,
                                                              exact: combo, trueFraction: combo ? row.Fraction2 : -1f));
            else if (!string.IsNullOrEmpty(row.Text)) BtText(r, row.Text, trackX, 0, trackW, line, PanelLayout.Text.Body, PanelLook.Muted, style: FontStyles.Italic);   // a player with no number: their words where the bar would be
            if (!string.IsNullOrEmpty(row.Link) && link?.Invoke(row.Link) is Action load)   // 0.9.2: a row of Your combinations loads it into Plan a meal
            {
                if (row.Opens) KindOpens(r, row, load, markX);   // 0.9.5: a weapon kind's row on By weapon shuts or opens its weapon lines
                else { var hit = Img(r, "Load", null, Color.clear, raycast: true); hit.rectTransform.Stretch(); hit.transform.SetAsFirstSibling(); hit.gameObject.AddComponent<Press>().Act = load; }
            }
            Size(r, -1, h);
            return r;
        }

        /// <summary>The page's one hover tag ("Frost 37", "Edda 350"; HoverTag): made with the first bar of this drawing that needs it, hidden, out of
        /// the page's layout, in the page's content (Fill draws it last, over every block); pointing at a part of a bar no share entry keys sets
        /// its words and moves it beside the bar (BarHover.TagOver: centred on the part, or from a start, kept inside the bar's room; below the bar, never over it). 0.8.2: one
        /// for the page, never one per part (two objects a part at about 58 µs an object, the 0.8.1 bench).</summary>
        static HoverTag pageTag; static RectTransform pageTagHome;
        static HoverTag PageTag(RectTransform near)
        {
            if (pageTag != null && pageTag.Ground) return pageTag;
            var home = pageTagHome ? pageTagHome : near;
            var tag = RoundedPart(home, "Tip", new Color(PlateColour.r, PlateColour.g, PlateColour.b, 1f)); tag.raycastTarget = false;   // 0.8.6: opaque, as the mirror's (at the plate's 0.95 the part's own number showed through it)
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
        // exact (0.9.2, Meals' Your combinations): the bar from zero, no floor (its parts as long as their values); trueFraction (0 or more): the thin
        // line's own length (the combination's meals), else the bar's fraction
        static float DamageBar(RectTransform r, BarForm form, BarForm mix, float fraction, bool dealt, float x0, float trackW, bool shares = true, string closing = null, bool trueLine = true,
                               bool exact = false, float trueFraction = -1f)
        {
            int n = form.Rows.Count;
            var track = RoundedPart(r, "Track", DrTrack); track.pixelsPerUnitMultiplier = DrRadiusScale; track.rectTransform.Box(x0, 0, trackW, PanelModel.DamageTrack);
            var barW = trackW * (exact ? PanelModel.DamageTrueWidth(fraction) : PanelModel.DamageBarWidth(fraction));
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
                if (string.IsNullOrEmpty(row.Value) || row.Other || row.Part?.Tone == PanelModel.ComboTone) continue;   // the folded rest, and a combination's part (its number in front is meals), carry no number inside
                BarDigits(seg.transform, row.Value, w, SegmentInk(c));   // the number inside the part where it fits at the book's floor, in the ink that reads on the colour
            }
            HoverRing(bar, hover);

            // the true length from zero, under the bar
            var top = PanelModel.DamageTrack + PanelModel.DamageInner;
            if (trueLine)
            {
                RoundedPart(r, "True", dealt ? DrTrueDealt : DrTrueReceived).rectTransform.Box(x0, top, Mathf.Max(2f, trackW * PanelModel.DamageTrueWidth(trueFraction >= 0 ? trueFraction : fraction)), PanelModel.DamageTrue);
                top += PanelModel.DamageTrue + PanelModel.DamageInner;
            }
            // no entry keys the parts (no share line, or a player's line of their damage by type), or (0.8.5) a row of damage dealt with its after,
            // whose words ("Slash 1 670: 770 got through, 900 stopped") would widen every entry of its line: the page's tag beside the bar, centred on
            // the part pointed at (kept inside the track); the entries keep their own words and light with their part
            var tagged = !shares || mix != null || form.Rows.Any(x => x.Part?.Whole != null);
            if (tagged) hover.TagOver(PageTag(r), bar, hoverWords, mid, -x0, trackW);
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
