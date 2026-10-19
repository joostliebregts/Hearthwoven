using System;
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
        static readonly Color SparkGold = PanelWash.SparkBar.Game, SparkMark = PanelWash.SparkMark.Game;   // the mirror's .spark as the game draws it (PanelWash.cs)

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

        // ----- 0.9 History (Chapters/DeedHistory.cs): the growth line drawn large (Tone SparkLargeTone). A column per day (week, month) as wide as the room
        // allows up to History.MaxColumn, as tall as the day against the largest; an empty day a flat mark on the foot; a day split by kind stacks its parts in
        // the page's By kind colours, largest at the foot; a gold tick over a day with a first or a feat; the dates under the columns (each day's Note).
        // Pointing at a column rings it and the page's one tag says the day (its Title); a click on a day opens that day's page. Mirrored by sparkLarge()
        // in preview/panel-preview.html -----

        static readonly Color HistoryTick = PanelLook.Gold;

        static void SparkLarge(RectTransform col, Block chart, Func<string, Action> link)
        {
            var days = chart.Items ?? new List<Block>();
            if (days.Count == 0) return;
            float gap = PanelLayout.History.ColumnGap, top = PanelLayout.History.Tick + PanelLayout.History.TickGap, room = PanelLayout.History.ChartH;
            var w = Mathf.Floor(Mathf.Min(PanelLayout.History.MaxColumn, (Column - gap * (days.Count - 1)) / days.Count));
            var area = Node("History", col); Size(area, -1, top + room + PanelLayout.History.Axis);
            var hover = area.gameObject.AddComponent<BarHover>(); hover.Init(days.Count);
            var words = new string[days.Count]; var at = new float[days.Count];
            for (int k = 0; k < days.Count; k++)
            {
                var d = days[k]; var x = k * (w + gap);
                var zero = d.Tone == "zero";
                var h = zero ? PanelLayout.History.Flat : Mathf.Max(PanelLayout.History.Least, Mathf.Round(Mathf.Clamp01(d.Fraction) * room));
                var parts = zero ? null : d.Items;
                if (d.Tone == PanelModel.SparkNoneTone) { }   // 0.9.5 draft: no number to draw (a DPS day before fighting time was recorded), its hover says why (Chapters/DamageHistory.cs)
                else if (parts == null || parts.Count == 0) Img(area, "Day", null, zero ? SparkMark : SparkGold).rectTransform.Box(x, top + room - h, w, h);
                else
                {
                    float y = top + room;   // the foot; each part stacked on the one under it, the last one ends at the column's top
                    for (int p = 0; p < parts.Count; p++)
                    {
                        var ph = p == parts.Count - 1 ? y - (top + room - h) : Mathf.Round(h * Mathf.Clamp01(parts[p].Fraction));
                        if (ph <= 0) continue;
                        Img(area, "Part", null, Hex(parts[p].Colour, SparkGold)).rectTransform.Box(x, y - ph, w, ph);
                        y -= ph;
                    }
                }
                if (d.Count > 0)   // option A: a first or a feat that day, a small gold tick over the column
                    Img(area, "Tick", null, HistoryTick).rectTransform.Box(x + (w - PanelLayout.History.Tick) / 2, top + room - h - PanelLayout.History.TickGap - PanelLayout.History.Tick, PanelLayout.History.Tick, PanelLayout.History.Tick);
                if (!string.IsNullOrEmpty(d.Note))
                {
                    var t = Label(area, d.Note, PanelLayout.Text.Floor, PanelLook.Muted); t.textWrappingMode = TextWrappingModes.NoWrap;
                    t.rectTransform.Box(x, top + room + PanelLayout.History.TickGap, Mathf.Ceil(t.preferredWidth), PanelLayout.History.Axis);
                }
                // the column's whole height is its pointer target: hover rings it and the tag says the day; a click opens the day's page
                var target = PanelModel.HistoryDayTarget(chart, d);
                var click = target == null ? null : link?.Invoke(target);
                var hit = Img(area, "Hit", null, Color.clear, raycast: true); hit.rectTransform.Box(x, 0, w, top + room);
                hover.Parts[k] = hit.rectTransform; var hp = hit.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = k;
                if (click != null) hit.gameObject.AddComponent<Press>().Act = click;
                words[k] = d.Title ?? d.Value; at[k] = x + w / 2;
            }
            hover.TagOver(PageTag(area), area, words, at, 0, Column);
            HoverRing(area, hover);
        }
    }
}
