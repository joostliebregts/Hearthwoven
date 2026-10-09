using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The "axis" block (shared by Company > Food shared and Deeds > Cooking; work/hearthwoven-visual-vocabulary/proto
    /// r4over-foodshared): one person per row on a vertical axis; what flowed one way grows to the left, the other way to
    /// the right, on one zero-based scale (Value). Meal amber, feast serving orchid (Fraction2 = the feast share of a
    /// row). Rows: Kind "row", Id = person (pairs a left and a right row), Tone "left"/"right" (null = right), Value,
    /// Fraction, Fraction2, Items = chips (Kind "chip": Icon item, Value "× n"). Optional Kind "end" per side (Tone, Value
    /// = the big total, Title its label); optional column headings Text (left) and Note (right). Rects and the game's
    /// item sprites only, placed once when the page is filled.
    /// </summary>
    public partial class PanelUi
    {
        public static readonly Color Meal = new Color(0.910f, 0.663f, 0.282f), FeastServing = new Color(0.753f, 0.498f, 0.839f);   // #e8a948, #c07fd6 (orchid: no player colour, Joost chose those)
        /// <summary>The axis key words (VOCABULARY.md word list).</summary>
        public const string MealKey = "meal", FeastServingKey = "feast serving", DishKey = "servings of one dish, its picture beside";
        const float AxisOneSide = 96, AxisOneGap = 16;   // one-sided (no left rows or ends, Deeds > Cooking): shield column, axis at 96 px
        const float AxisRowH = 92, AxisGap = 46, AxisValueW = 48, AxisBarH = 22, AxisShield = 30;

        static bool IsLeft(Block row) => row.Tone == "left";

        internal static void Axis(RectTransform col, Block b)
        {
            var items = b.Items ?? new List<Block>();
            var rows = items.Where(i => i.Kind == "row").ToList();
            var ends = items.Where(i => i.Kind == "end").ToList();
            if (rows.Count == 0 && ends.Count == 0) return;
            var left = ends.FirstOrDefault(IsLeft); var right = ends.FirstOrDefault(e => !IsLeft(e));
            // "since install" once per side where any number on that side was counted on this PC
            bool SideSince(bool l) => items.Where(i => IsLeft(i) == l).SelectMany(i => new[] { i }.Concat(i.Items ?? new List<Block>())).Any(i => i.SinceInstall);
            bool sinceLeft = SideSince(true), sinceRight = SideSince(false);

            // two-sided: the axis in the middle; one-sided: the axis near the left edge, the bars across the rest
            var oneSided = !items.Any(IsLeft);
            float W = Column, mid = oneSided ? AxisOneSide : W / 2, gap = oneSided ? AxisOneGap : AxisGap;
            if (left != null || right != null)
            {
                var head = Node("Ends", col); Size(head, -1, 74);
                if (left != null) AxisEnd(head, left, 0, mid - 20, TextAlignmentOptions.Left, sinceLeft);
                if (right != null) AxisEnd(head, right, mid + 20, W - mid - 20, TextAlignmentOptions.Right, sinceRight);
                if (left != null) sinceLeft = false;   // said beside its end
                if (right != null) sinceRight = false;
            }
            if (!string.IsNullOrEmpty(b.Text) || !string.IsNullOrEmpty(b.Note))
            {
                var heads = Node("Headings", col); Size(heads, -1, 20);
                AxisHeading(heads, b.Text, 0, mid - gap, TextAlignmentOptions.Right);
                AxisHeading(heads, b.Note, mid + gap, W - mid - gap, TextAlignmentOptions.Left);
            }

            // one line per person, in the model's order; the axis runs through every line
            var people = new List<string>();
            foreach (var r in rows) if (!people.Contains(r.Id ?? r.Title)) people.Add(r.Id ?? r.Title);
            var track = (oneSided ? W - mid : mid) - gap - AxisValueW - 8;
            var centre = oneSided ? mid / 2 : mid;   // the shield and name: on the axis, or in the column left of it
            for (int k = 0; k < people.Count; k++)
            {
                var line = Node("Person", col); Size(line, -1, AxisRowH);
                Img(line, "Axis", null, PanelLook.Rule).rectTransform.Box(mid - 1, 0, 2, AxisRowH);
                var mine = rows.Where(r => (r.Id ?? r.Title) == people[k]).ToList();
                var who = mine[0];
                var shield = Marker(line, who.Icon, AxisShield, layout: false); shield.Box(centre - AxisShield / 2, 14, AxisShield, AxisShield);
                var name = Label(line, who.Title, 14, PanelLook.Text, align: TextAlignmentOptions.Top);
                name.rectTransform.Box(centre - (oneSided ? mid / 2 : 60), 16 + AxisShield, oneSided ? mid : 120, 20); name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
                foreach (var r in mine)
                {
                    var l = IsLeft(r);
                    var labelHere = l ? sinceLeft : sinceRight;
                    AxisSide(line, r, l, mid, gap, track, labelHere && k == 0);
                }
                if (k < people.Count - 1) Img(line, "Rule", null, PanelLook.Rule).rectTransform.Box(0, AxisRowH - 1, W, 1);
            }

            var key = Line(col, 8);
            Size(Fill(key, "Meal", Meal), 26, 10); Label(key, MealKey, 14, PanelLook.Muted);
            if (rows.Any(r => r.Fraction2 > 0)) { Size(Node("Gap", key), 14, 1); Size(Fill(key, "Feast", FeastServing), 26, 10); Label(key, FeastServingKey, 14, PanelLook.Muted); }
            // fix3-rest: the small "× 3 × 2" under a bar had no key: each is the servings of one dish (its picture, then how many)
            if (rows.Any(r => (r.Items ?? new List<Block>()).Any(c => c.Kind == "chip")))
            {
                Size(Node("Gap", key), 14, 1); Label(key, "× n", 14, PanelLook.Text, style: FontStyles.Bold); Label(key, DishKey, 14, PanelLook.Muted);
            }
        }

        static void AxisEnd(RectTransform head, Block end, float x, float w, TextAlignmentOptions align, bool since)
        {
            var v = Label(head, end.Value, 48, PanelLook.Gold, style: FontStyles.Bold, align: align == TextAlignmentOptions.Left ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight);
            v.rectTransform.Box(x, 0, w, 50); v.textWrappingMode = TextWrappingModes.NoWrap;
            var t = Label(head, end.Title, 16, PanelLook.Text, align: align); t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.Box(x, 50, w, 22);
            var scope = since ? PanelModel.SinceInstallLabel : end.Value2;   // whose record: since install (this PC) or their last session
            if (string.IsNullOrEmpty(scope)) return;
            var s = Label(head, scope, 13, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic);
            s.textWrappingMode = TextWrappingModes.NoWrap;
            var tw = Mathf.Min(t.preferredWidth, w);
            if (align == TextAlignmentOptions.Left) s.rectTransform.Box(x + tw + 10, 50, 120, 22);
            else s.rectTransform.Box(x + w - tw - 10 - s.preferredWidth, 50, s.preferredWidth + 4, 22);
        }

        static void AxisHeading(RectTransform heads, string text, float x, float w, TextAlignmentOptions align)
        {
            if (string.IsNullOrEmpty(text)) return;
            var h = Label(heads, text, 13, PanelLook.Muted, style: FontStyles.UpperCase, align: align);
            h.characterSpacing = 14; h.textWrappingMode = TextWrappingModes.NoWrap; h.rectTransform.Box(x, 0, w, 20);
        }

        // one side of a person's line: the track from the axis outward, the fill (meals, then feast servings, read left to
        // right), the count at the track's outer end, the dishes under it
        static void AxisSide(RectTransform line, Block r, bool left, float mid, float gap, float track, bool since)
        {
            const float top = 14;
            var x0 = left ? mid - gap - track : mid + gap;
            var t = Kit(line, "Track", "meter-track"); t.rectTransform.Box(x0, top, track, AxisBarH);
            var len = Mathf.Clamp01(r.Fraction) * (track - 6);
            if (len > 0)
            {
                var start = left ? x0 + 3 + (track - 6) - len : x0 + 3;
                var feast = Mathf.Clamp01(r.Fraction2) * len;
                if (len - feast > 0) Fill(line, "Meal", Meal).rectTransform.Box(start, top + 3, len - feast, AxisBarH - 6);
                if (feast > 0) Fill(line, "Feast", FeastServing).rectTransform.Box(start + len - feast, top + 3, feast, AxisBarH - 6);
                // the count inside each part, where it fits: meals, then feast servings (the chips below say which dishes)
                void Inside(float x, float w, int n) { if (n <= 0 || w < 26) return; var t = Label(line, n.ToString(System.Globalization.CultureInfo.InvariantCulture), 14, DarkInk, style: FontStyles.Bold, align: TextAlignmentOptions.Center); t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Box(x, top + 1, w, AxisBarH - 2); }
                Inside(start, len - feast, r.Count); Inside(start + len - feast, feast, (int)r.Level);
            }
            var v = Label(line, r.Value, 20, PanelLook.Text, style: FontStyles.Bold, align: left ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
            v.textWrappingMode = TextWrappingModes.NoWrap;
            v.rectTransform.Box(left ? x0 - AxisValueW - 6 : x0 + track + 8, top - 2, AxisValueW, AxisBarH + 4);
            if (since)
            {
                var s = Label(line, PanelModel.SinceInstallLabel, 12, PanelLook.Faint, align: left ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft, style: FontStyles.Italic);
                s.textWrappingMode = TextWrappingModes.NoWrap;
                s.rectTransform.Box(left ? x0 - AxisValueW - 6 - 40 : x0 + track + 8, top + AxisBarH + 2, AxisValueW + 40, 16);
            }
            // dishes: the game's item sprite and its count, flush with the axis side of the track
            var chips = Node("Dishes", line); chips.Box(x0, top + AxisBarH + 8, track, 24);
            var h = chips.gameObject.AddComponent<HorizontalLayoutGroup>(); Layout(h, 12, left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            foreach (var c in (r.Items ?? new List<Block>()).Where(c => c.Kind == "chip"))
            {
                var chip = Line(chips, 4);
                Marker(chip, c.Icon, 20);
                var n = Label(chip, c.Value, 14, PanelLook.Text, style: FontStyles.Bold); n.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }
    }
}
