using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9.5 draft, Damage history (Chapters/DamageHistory.cs): By weapon's one answer ("49 920 Melee damage, the most of any weapon", a "stat") carries
    /// the damage growth line at its line's right end with the gold History word after the columns, drawn as a Deeds hero's (RecordedUi.HeroRight): the
    /// columns (GrowthLinesUi.Spark), "History", the span on hover, a click opens Damage history. The History page itself is the deeds' (SparkLarge).
    /// Mirrored by the stat case in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        static void StatSpark(RectTransform line, Block spark, Func<string, Action> link)
        {
            var box = Spark(line, spark, false, caption: false);
            var open = string.IsNullOrEmpty(spark.Id) ? null : link?.Invoke(spark.Id);
            if (open != null) { var word = Label(box, PanelModel.HistoryWord, PanelLook.MinText, PanelLook.Gold); word.textWrappingMode = TextWrappingModes.NoWrap; Size(word, -1, SparkH); }
            if (string.IsNullOrEmpty(spark.Title) && open == null) return;
            var hit = box.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0f); hit.raycastTarget = true;
            if (!string.IsNullOrEmpty(spark.Title))
            {
                var tag = ReasonTag(box, spark.Title); tag.gameObject.SetActive(false);
                box.gameObject.AddComponent<SwapOnHover>().On = tag.gameObject;
            }
            if (open != null) box.gameObject.AddComponent<Press>().Act = open;
        }
    }
}
