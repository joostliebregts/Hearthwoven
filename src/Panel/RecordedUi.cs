using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The 0.7 redesign's shared drawing (work/hearthwoven-0.7/REDESIGN-RULES.md, part 2; the model is RecordedModel.cs): the
    /// "Recorded from ..." labels through one helper (LabelOf), the "About these numbers" button and box, the deed's skill in the hero's
    /// row, and a "Not recorded" value. Mirrored in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        /// <summary>The label a block carries after its heading or number: 0.7's RecordedFrom ("Recorded from 8 October · this PC",
        /// "from 8 October"); null = none.</summary>
        internal static string LabelOf(Block b) => b == null || string.IsNullOrEmpty(b.RecordedFrom) ? null : b.RecordedFrom;
        internal static bool Labelled(Block b) => LabelOf(b) != null;

        /// <summary>A value whose counter did not run (Block.Unrecorded, "Not recorded"): at the label's size, muted, italic, never gold.</summary>
        static TextMeshProUGUI Unrecorded(TextMeshProUGUI value, Block b, float labelSize)
        {
            if (value == null || b == null || !b.Unrecorded) return value;
            value.fontSize = Mathf.Max(labelSize, PanelLook.MinText); value.color = PanelLook.Muted; value.fontStyle = FontStyles.Italic;
            return value;
        }

        // ----- About these numbers: the button (right-aligned, with the numbers key's cap) and, while open, the box under it -----

        const float NumbersLabelW = 190;

        // 0.7 compact tops: the button rides at the right end of the plate's title strip when the page has one (PanelModel.StripAbout,
        // FeatsUi.FeatBandBlock); its own row is then left out, or hidden again when the strip comes after it (On foot)
        static Block stripAbout;          // this page's button that the strip may carry (set by Render before the plate is drawn)
        static bool aboutInStrip;         // the strip drew it
        static RectTransform aboutRow;    // its own row, when drawn before the strip
        // B38 (Joost: "try to get all top items into one row max"): the page's view switch shares that row too (PanelModel.TopSwitch): in the
        // title strip when the plate has one (stripSwitch), else the About button joins the switch's own row (switchAbout)
        static Block stripSwitch, switchAbout, topSwitch;   // topSwitch: no strip, its row leads the plate (switchOnTop once drawn)
        static bool switchInStrip, switchOnTop;

        static void AboutNumbersBox(RectTransform col, Block b, Func<string, Action> link)
        {
            if (b == switchAbout) return;   // it rides in the view switch's row, which draws the button and its box (SwitchRow)
            if (!(aboutInStrip && b == stripAbout))
            {
                var top = Line(col, 8);
                Node("Rest", top).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;   // the button sits on the right, under the skill
                AboutButton(top, b, link);
                if (b == stripAbout) aboutRow = top;
            }
            AboutBox(col, b);
        }

        // the box under the button while it is open
        static void AboutBox(RectTransform col, Block b)
        {
            if (!b.Open || b.Items == null || b.Items.Count == 0) return;
            // the approved prototype (install-rethink preview-a-woodcutting-open): a dark inset, a 3 px gold rule on the left, the heading in
            // the plate's small capitals, then one row per line: the label in gold (190 px), the sentence beside it, wrapping
            var box = Row(col, 0);
            var bg = box.gameObject.AddComponent<Image>(); bg.color = new Color(0f, 0f, 0f, 0.3f); bg.raycastTarget = false;
            var rule = Img(box, "Rule", null, PanelLook.Gold); var rl = rule.gameObject.AddComponent<LayoutElement>(); rl.minWidth = rl.preferredWidth = 3; rl.flexibleHeight = 1;
            var inner = VStack(box, 8); inner.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(16, 16, 12, 14); inner.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Sect(Line(inner, 8), b.Title, null);
            foreach (var l in b.Items)
            {
                var row = Row(inner, 12);
                var label = Label(row, l.Title, 15, PanelLook.Gold); Size(label, NumbersLabelW, -1);
                Label(row, l.Text, 15, PanelLook.Text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            }
        }

        // the button itself: the chip, then the numbers key's cap 8 px after it; returns its width. title: the short label (B38)
        static float AboutButton(RectTransform row, Block b, Func<string, Action> link, string title = null)
        {
            var w = Chip(row, title ?? b.Title, null, b.Open, link?.Invoke(b.Id), 26);
            if (!string.IsNullOrEmpty(b.KeyCap)) { Size(Keycap(row, b.KeyCap), KeycapWidth(b.KeyCap, 22), 22); w += 8 + KeycapWidth(b.KeyCap, 22); }
            return w;
        }

        // B38: what rides at the right end of the top row: the view switch (its key, its chips), then the About button with its key; the
        // node is measured as laid out (its width: TopClusterWidth). shortAbout: "Numbers" for "About these numbers"
        static RectTransform TopCluster(RectTransform parent, Block sw, Block about, bool shortAbout, Func<string, Action> link, string note = null)
        {
            var row = Line(parent, 8, TextAnchor.MiddleRight);
            if (!string.IsNullOrEmpty(note)) { var n = Label(row, note, PanelLook.MinText, PanelLook.Muted, style: FontStyles.Italic, align: TextAlignmentOptions.MidlineRight); n.textWrappingMode = TextWrappingModes.NoWrap; }   // 0.8 layout D+: a fellow's book, whose copy and when
            if (sw != null) SwitchChips(row, sw, link);
            if (sw != null && about != null) Size(Node("Gap", row), 8, 1);
            if (about != null) AboutButton(row, about, link, shortAbout ? PanelModel.AboutShort : null);
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            return row;
        }
        static float TopClusterWidth(RectTransform cluster) => Mathf.Ceil(LayoutUtility.GetPreferredWidth(cluster));

        // ----- the deed's skill in the hero's row (rule K): icon, name, "level 34", a thin progress bar; a click opens the skill -----

        // 0.8 layout D+ (FEEDBACK part 4): the hero's right side, the growth line over the deed's skill, one column (so the numbers keep their line);
        // returns the spacer that pushes it right and the column (Hero moves both to the line HeroLines gives it). A click on the skill opens it
        static (RectTransform rest, RectTransform skill, RectTransform spark) HeroRight(RectTransform row, Block spark, Block s, Func<string, Action> link)
        {
            var rest = Node("Rest", row); rest.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;   // right-aligned, the hero row's last item
            var right = VStack(row, 8); var v = right.GetComponent<VerticalLayoutGroup>(); v.childForceExpandWidth = false; v.childAlignment = TextAnchor.LowerRight; v.padding = new RectOffset(0, 0, 0, 6);
            RectTransform sparkBox = null;
            if (spark != null)   // its columns only; the span ("since 1 Oct") on hover and in About these numbers (Chapters/GrowthLinesUi.cs, PageHead.cs)
            {
                sparkBox = Spark(right, spark, false, caption: false);
                if (!string.IsNullOrEmpty(spark.Title))
                {
                    var hit = sparkBox.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0f); hit.raycastTarget = true;
                    var tag = ReasonTag(sparkBox, spark.Title); tag.gameObject.SetActive(false);
                    sparkBox.gameObject.AddComponent<SwapOnHover>().On = tag.gameObject;
                }
            }
            if (s != null)
            {
                var click = link?.Invoke(s.Id);
                var hit = Img(right, "Skill", null, new Color(0f, 0f, 0f, 0f), raycast: click != null);
                Layout(hit.gameObject.AddComponent<HorizontalLayoutGroup>(), 10, TextAnchor.LowerLeft);
                Marker((RectTransform)hit.transform, s.Icon, 28);
                SkillWords((RectTransform)hit.transform, s);
                if (click != null) hit.gameObject.AddComponent<Press>().Act = click;
            }
            return (rest, right, sparkBox);
        }

        // the skill's words: its name muted, "level 57" bold, and the thin bar to the next level (120 x 4) under them
        static void SkillWords(RectTransform parent, Block s)
        {
            var words = VStack(parent, 4); words.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;   // the bar keeps its 120 px
            var line = Line(words, 8, TextAnchor.LowerLeft);
            Label(line, s.Title, 15, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            Label(line, PanelModel.LevelWord + " " + s.Value, 15, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            if (s.Progress >= 0)
            {
                var track = Img(words, "Progress", null, PanelLook.Track); Size(track, 120, 4);
                var fill = Img(track.transform, "Fill", null, PanelLook.Gold).rectTransform;
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Clamp01(s.Progress), 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            }
        }

    }
}
