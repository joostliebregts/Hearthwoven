using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8 growth lines (Chapters/GrowthLines.cs) drawn: one gold column per day, 4 px wide and 2 px apart, as tall as the day against the line's
    /// largest (14 px; at least 2 px for any amount), a kept day with nothing a 1 px mark on the line's foot. Beside a hero the line stands in the
    /// room over the hero's words, its caption ("last 14 days") above the columns, so the hero row keeps its width (Woodcutting's row is full to
    /// the pixel); in the Battle strip's legend it runs inline, the caption after the columns. Greyed (a fellow's Deeds page): fourteen faint marks
    /// and "no days shared". Plain rects built once with the page, nothing per frame. Mirrored by spark() in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        const float SparkW = 4, SparkGap = 2, SparkH = 14;
        static readonly Color SparkGold = new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.85f), SparkMark = new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.35f);

        static RectTransform Spark(RectTransform parent, Block spark, bool stacked, bool caption = true)
        {
            RectTransform box;
            if (stacked) { box = VStack(parent, 1); box.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false; }
            else box = Line(parent, 8, TextAnchor.LowerLeft);
            var off = spark.Tone == PanelModel.OffTone;
            TextMeshProUGUI Caption()
            {
                var cap = Label(box, spark.Title ?? "", PanelLook.MinText, off ? PanelLook.Faint : PanelLook.Muted, style: off ? FontStyles.Italic : FontStyles.Normal);
                cap.textWrappingMode = TextWrappingModes.NoWrap; return cap;
            }
            if (stacked && caption) Caption();
            var days = spark.Items ?? new List<Block>();
            var n = off ? PanelModel.GrowthDays : days.Count;
            var area = Node("Days", box); Size(area, n * (SparkW + SparkGap) - SparkGap, SparkH);
            for (int k = 0; k < n; k++)
            {
                var zero = off || days[k].Tone == "zero";
                var h = zero ? 1f : Mathf.Max(2f, Mathf.Round(Mathf.Clamp01(days[k].Fraction) * SparkH));
                Img(area, "Day", null, zero ? SparkMark : SparkGold).rectTransform.Box(k * (SparkW + SparkGap), SparkH - h, SparkW, h);
            }
            if (!stacked && caption) Caption();   // the hero's: no caption (0.8 layout D+), its span on hover
            return box;
        }
    }
}
