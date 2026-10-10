using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The About page's kinds (PanelModel.About; work/hearthwoven-visual-vocabulary/proto/vocab-about.png): origins (the
    /// three sources as cards: icon, name, since when, examples), sincewhen (one line per source from the character's
    /// making past install to now; the character's line broken where its long past is shortened, this PC's in amber from
    /// install, fellow players' dashed from install) and promises (four marks in one row). Rects, the kit's sprites and the
    /// vocabulary sprites only, placed once when the page is filled.
    /// </summary>
    public partial class PanelUi
    {
        // fix-rest: the cards' body text 15 px in a lighter grey (it was 13 px faint on black), so the cards are 12 px taller
        const float OriginH = 200, OriginGap = 14, SpanRowH = 32, SpanLabelW = 196, InstallAt = 0.72f;
        static readonly Color OriginBody = new Color(0.78f, 0.72f, 0.61f);

        // the colour each source speaks in: your character parchment, this PC amber, fellow players muted
        static Color SourceInk(string src) => src == PanelModel.SrcPc ? PanelLook.Accent : src == PanelModel.SrcFellows ? PanelLook.Muted : PanelLook.Text;

        static void Origins(RectTransform col, Block b)
        {
            var cards = b.Items ?? new List<Block>();
            if (cards.Count == 0) return;
            var w = Mathf.Floor((Column - OriginGap * (cards.Count - 1)) / cards.Count);
            var row = Line(col, OriginGap, TextAnchor.UpperLeft); Size(row, -1, OriginH);
            foreach (var c in cards)
            {
                var card = Kit(row, c.Title, "meter-track"); Size(card, w, OriginH);
                var icon = Marker(card.rectTransform, c.Icon, 32, layout: false); icon.Box(16, 16, 32, 32);
                var t = Label(card.transform, c.Title, 18, PanelLook.Text); t.rectTransform.Box(16, 58, w - 32, 48);
                var since = Label(card.transform, c.Value, 15, SourceInk(c.Tone), style: FontStyles.Bold); since.rectTransform.Box(16, 108, w - 32, 22);
                since.textWrappingMode = TextWrappingModes.NoWrap; since.overflowMode = TextOverflowModes.Ellipsis;
                var ex = Label(card.transform, c.Text, 15, OriginBody); ex.rectTransform.Box(16, 134, w - 32, OriginH - 142);
                ex.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        // ----- readrows (What it reads, Sharing): one statement per row, the mark of its source card, the title, the words; a rule between -----
        // fix-rest: it was a wall of text that stopped short of the plate with its last line cut. Rows now: a 40 px mark, the title at 22 px,
        // the words at 18 px in a lighter grey, the plate's scroll fade when more rows than room.

        const float ReadMark = 40, ReadGap = 20;

        static void ReadRows(RectTransform col, Block b)
        {
            var rows = b.Items ?? new List<Block>();
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var row = Line(col, ReadGap, TextAnchor.UpperLeft); row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 10, 10);
                var mark = Node("Mark", row); Size(mark, ReadMark, ReadMark);
                if ((r.Icon ?? "").StartsWith("vocab:promise-") || r.Icon == "vocab:about-info") VocabImg(mark, "Mark", VocabName(r.Icon), PanelLook.Muted).rectTransform.Stretch();
                else Marker(mark, r.Icon, ReadMark, layout: false).Stretch();
                var text = VStack(row, 6); Size(text, Column - ReadMark - ReadGap, -1);
                Label(text, r.Title, 22, PanelLook.Text);
                Label(text, r.Text, 18, OriginBody);
                if (i < rows.Count - 1) Size(Img(col, "Rule", null, PanelLook.Rule), -1, 1);
            }
        }

        static Image Bar(RectTransform parent, float x, float top, float w, float h, Color colour)
        {
            var r = Img(parent, "Line", null, colour); r.rectTransform.Box(x, top, w, h); return r;
        }

        static void SinceWhen(RectTransform col, Block b)
        {
            var spans = b.Items ?? new List<Block>();
            if (spans.Count == 0) return;
            if (!string.IsNullOrEmpty(b.Title)) Sect(Line(col, 8), b.Title, null);
            var h = spans.Count * SpanRowH + 34;
            var box = Node("SinceWhen", col); Size(box, Column, h);
            float x0 = SpanLabelW, x1 = Column, xi = Mathf.Round(x0 + (x1 - x0) * InstallAt);
            var rule = new Color(PanelLook.Rule.r, PanelLook.Rule.g, PanelLook.Rule.b, 0.5f);
            for (int k = 0; k < spans.Count; k++)
            {
                var s = spans[k]; float top = k * SpanRowH, mid = top + SpanRowH / 2f;
                var label = Line(box, 8); label.Box(0, top, SpanLabelW - 12, SpanRowH);
                Marker(label, s.Icon, 18);
                var t = Label(label, s.Title, 15, PanelLook.Text); t.textWrappingMode = TextWrappingModes.NoWrap;
                if (s.Tone == PanelModel.SrcCharacter)
                {
                    // the whole life of the character, shortened where the break marks sit
                    var breakAt = Mathf.Round(x0 + (xi - x0) * 0.5f);
                    Bar(box, x0, mid - 3, breakAt - 6 - x0, 6, PanelLook.Text);
                    Bar(box, breakAt + 6, mid - 3, x1 - breakAt - 6, 6, PanelLook.Text);
                    for (int m = 0; m < 2; m++) { var cut = Bar(box, breakAt - 4 + m * 6, mid - 8, 2, 16, PanelLook.Text); cut.rectTransform.localRotation = Quaternion.Euler(0, 0, -25); }
                }
                else
                {
                    Bar(box, x0, mid - 0.5f, xi - x0, 1, rule);   // nothing before install
                    if (s.Tone == PanelModel.SrcPc) Bar(box, xi, mid - 3, x1 - xi, 6, PanelLook.Accent);
                    else
                    {
                        for (var x = xi + 4; x + 8 <= x1; x += 15) Bar(box, x, mid - 3, 8, 6, PanelLook.Muted);   // dashed: only while they share
                        // what a fellow's line is: the copy they last shared, not a live count (fix-rest, review)
                        if (!string.IsNullOrEmpty(s.Text)) { var cap = Label(box, s.Text, 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineRight); cap.rectTransform.Box(x0, top, xi - x0 - 10, SpanRowH); cap.textWrappingMode = TextWrappingModes.NoWrap; }
                    }
                }
            }
            for (var y = 0f; y < spans.Count * SpanRowH + 4; y += 6) Bar(box, xi, y, 1, 3, new Color(PanelLook.Accent.r, PanelLook.Accent.g, PanelLook.Accent.b, 0.6f));   // install
            var foot = spans.Count * SpanRowH + 6;
            Bar(box, x0, foot, x1 - x0, 1, rule);
            var made = Label(box, b.Text, 14, PanelLook.Faint); made.rectTransform.Box(x0, foot + 6, xi - x0 - 100, 20); made.textWrappingMode = TextWrappingModes.NoWrap;
            var now = Label(box, b.Value2, 14, PanelLook.Faint, align: TextAlignmentOptions.TopRight); now.rectTransform.Box(x1 - 60, foot + 6, 60, 20);
            var inst = Label(box, b.Value, 14, PanelLook.Accent, align: TextAlignmentOptions.TopRight, style: FontStyles.Bold); inst.rectTransform.Box(xi - 120, foot + 6, x1 - 60 - (xi - 120) - 12, 20);
            inst.textWrappingMode = TextWrappingModes.NoWrap;
        }

        static void Promises(RectTransform col, Block b)
        {
            var items = b.Items ?? new List<Block>();
            if (items.Count == 0) return;
            Spacer(col, 4);
            var row = Line(col, 0); row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            foreach (var p in items)
            {
                var one = Line(row, 8);
                var mark = Node("Mark", one); Size(mark, 22, 22);
                VocabImg(mark, "Mark", VocabName(p.Icon), PanelLook.Muted).rectTransform.Stretch();
                Label(one, p.Title, 14, PanelLook.Text).textWrappingMode = TextWrappingModes.NoWrap;
            }
            // a promise's one explaining line (other mods' items) under the row, quiet
            foreach (var p in items.Where(x => !string.IsNullOrEmpty(x.Text))) Label(col, p.Text, 14, PanelLook.Muted);
        }
    }
}
