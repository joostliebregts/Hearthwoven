using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The battle pages of 0.8, drawn (Chapters/BattleFeedModel.cs; Joost's forms in work/hearthwoven-0.8/battle-prototypes, Last fight from
    /// prototypes/group-battle pick A): the "×N" badge and the outcome marks (Damage > By foe, Defence, Last fight), the battle feed in its three
    /// views (Log, Cards, Timeline) and the Last fight's foes and received line (its rows per player: EveryoneUi.GroupRows). Laid out once per fill in fixed rows (BtRow), ready
    /// sprites only (the book's rounded part, Circle, Ring, HalfRing); nothing per frame. Mirrored in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        const float PipPx = 11, PipGap = 5, BadgeH = 20, MarkPx = 13;
        static readonly Color PipAway = new Color32(0x8f, 0x82, 0x70, 0xff), BadgeEdge = new Color32(0x6b, 0x52, 0x33, 0xff);

        // ----- the outcome marks: filled = defeated, ring = got away, a gold ring = still fighting; half filled = defeated with fellow players -----

        static Image MarkImg(RectTransform parent, string tone, float x, float top, float size)
        {
            Sprite sprite; Color c;
            switch (tone)
            {
                case PanelModel.PipOn: case "you": sprite = PanelLook.Circle; c = PanelLook.Gold; break;
                case "with": sprite = PanelLook.HalfRing; c = PanelLook.Gold; break;
                case PanelModel.PipOpen: sprite = PanelLook.Ring; c = PanelLook.Gold; break;   // "fighting", the feed's tone too
                default: sprite = PanelLook.Ring; c = PipAway; break;   // got away, survived
            }
            var i = Img(parent, "Mark", sprite, c); i.preserveAspect = false; i.rectTransform.Box(x, top, size, size);
            if (!sprite) i.enabled = false;
            return i;
        }

        /// <summary>How tall the marks of a kind are in a column this wide (one row of PipPx, PipGap between; words: one line).</summary>
        static float PipsHeight(Block pips, float width)
        {
            if (pips == null) return 0;
            if (!string.IsNullOrEmpty(pips.Text)) return LineBox(PanelLook.MinText);
            var n = (pips.Items ?? new List<Block>()).Sum(p => p.Count);
            var per = Mathf.Max(1, Mathf.FloorToInt((width + PipGap) / (PipPx + PipGap)));
            var rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)per));
            return rows * PipPx + (rows - 1) * PipGap;
        }

        /// <summary>The marks of a kind at (x, top), flowing to the next row at <paramref name="width"/>: the defeated first, then those that got
        /// away, then those still fighting; past PanelModel.PipMax the model gives the words instead.</summary>
        static void PipsAt(RectTransform parent, Block pips, float x, float top, float width)
        {
            if (!string.IsNullOrEmpty(pips.Text)) { BtText(parent, pips.Text, x, top, width, LineBox(PanelLook.MinText), PanelLook.MinText, PanelLook.Muted); return; }
            var per = Mathf.Max(1, Mathf.FloorToInt((width + PipGap) / (PipPx + PipGap))); int k = 0;
            foreach (var p in pips.Items ?? new List<Block>())
                for (int i = 0; i < p.Count; i++, k++)
                    MarkImg(parent, p.Tone, x + (k % per) * (PipPx + PipGap), top + (k / per) * (PipPx + PipGap), PipPx);
        }

        /// <summary>The one-line key of the marks a list uses ("● defeated  ○ got away"), in a horizontal row.</summary>
        static void PipKeyLine(RectTransform row, Block key)
        {
            foreach (var p in key.Items ?? new List<Block>())
            {
                var holder = Node("Key", row); Size(holder, PipPx, PipPx); MarkImg(holder, p.Tone, 0, 0, PipPx);
                var t = Label(row, p.Title, PanelLook.MinText, PanelLook.Muted); t.textWrappingMode = TextWrappingModes.NoWrap;
                Size(Node("Gap", row), 6, 1);
            }
        }

        // ----- the "×N" badge: a quiet pill (1 px edge, fully rounded) after a name -----

        static float BadgeWidth(RectTransform parent, string text)
        {
            var t = Label(parent, text, 14, PanelLook.Muted, style: FontStyles.Bold); t.textWrappingMode = TextWrappingModes.NoWrap;
            var w = Mathf.Ceil(t.preferredWidth) + 16; t.gameObject.SetActive(false); Destroy(t.gameObject);   // out of the layout at once
            return w;
        }

        static float Badge(RectTransform parent, string text, float x, float top)
        {
            var w = BadgeWidth(parent, text);
            var edge = Img(parent, "Badge", PanelLook.PillEdge, BadgeEdge); edge.preserveAspect = false;
            if (PanelLook.PillEdge) { edge.type = Image.Type.Sliced; edge.pixelsPerUnitMultiplier = 1f; }   // the rim at about 1 px, the ends round
            edge.rectTransform.Box(x, top, w, BadgeH);
            BtText(parent, text, x, top, w, BadgeH, 14, PanelLook.Muted, TextAlignmentOptions.Center, FontStyles.Bold);
            return w;
        }

        // ----- the battle feed: fights newest first, Log · Cards · Timeline -----

        const float FdHead = 34, FdTime = 78, FdName = 150, FdOut = 196, FdRow = 36, FdText = 15, FdAxis = 88, FdCardGap = 14, FdGapRow = 30;

        /// <summary>One entry's damage both ways on one line (Joost: "dealt fire 26 · blunt 18 · received blunt 12"): the type in its colour (lifted
        /// to read on the plate), the amount bold; received after your armour. A line breaks only between items, at their " · ": the word, the
        /// type and the amount are held together by no-break spaces (0.8.1: in game "received blunt less than 1" wrapped as "less than" / "1").</summary>
        static Rich FeedLine(Block entry)
        {
            Rich Side(string word, string kind)
            {
                var parts = (entry.Items ?? new List<Block>()).Where(p => p.Kind == kind).ToList();
                if (parts.Count == 0) return Rich.Empty;
                var r = Rich.Plain(PanelModel.NoBreak(word + " ")).Ink(Html(PanelLook.Muted));
                for (int k = 0; k < parts.Count; k++)
                {
                    if (k > 0) r += Rich.Plain(" · ").Ink(Html(PanelLook.Faint));
                    r += Rich.Plain(PanelModel.NoBreak(parts[k].Title + " ")).Ink(Html(Legible(BtColour(parts[k])))) + Rich.Plain(PanelModel.NoBreak(parts[k].Value)).Bold();
                }
                return r;
            }
            var dealt = Side(PanelModel.FeedDealt, PanelModel.FeedDealt); var received = Side(PanelModel.FeedReceived, PanelModel.FeedReceived);
            return dealt.IsEmpty ? received : received.IsEmpty ? dealt : dealt + Rich.Plain("  ·  ").Ink(Html(PanelLook.Faint)) + received;
        }

        static string Html(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // a line's height when it wraps at this width (TMP measures without a layout pass)
        static float Wrapped(TextMeshProUGUI t, float width)
        {
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow;
            return Mathf.Ceil(t.GetPreferredValues(t.text, width, 0).y);
        }

        static void FightHead(RectTransform col, Block f)
        {
            var h = BtRow(col, FdHead);
            var title = BtText(h, f.Title, 0, 4, Column * 0.6f, 26, 17, PanelLook.Gold, style: FontStyles.Bold);
            var tw = Mathf.Ceil(title.preferredWidth) + 2; title.rectTransform.sizeDelta = new Vector2(tw, title.rectTransform.sizeDelta.y);
            var rest = "· " + f.Text + (string.IsNullOrEmpty(f.Note) ? "" : " · " + f.Note);
            var right = string.Join(" · ", new[] { string.IsNullOrEmpty(f.Value) ? null : PanelModel.FeedDealt + " " + f.Value, string.IsNullOrEmpty(f.Value2) ? null : PanelModel.FeedReceived + " " + f.Value2 }.Where(x => x != null).ToArray());
            var r = BtText(h, right, Column - 260, 4, 260, 26, FdText, PanelLook.Muted, TextAlignmentOptions.MidlineRight);
            var rw = Mathf.Ceil(r.preferredWidth) + 4;
            BtText(h, rest, tw + 6, 4, Mathf.Max(40, Column - tw - 6 - rw - 12), 26, FdText, PanelLook.Muted);
            BtRect(h, "Rule", PanelLook.Rule, 0, FdHead - 2, Column, 1);
        }

        static void BattleFeed(RectTransform col, Block b)
        {
            var fights = (b.Items ?? new List<Block>()).Where(i => i.Kind == "fight").ToList();
            if (fights.Count == 0) return;
            col = VStack(col, 0);   // the feed's own rhythm: its rows touch, the gaps are its own (the plate's 12 px stays between blocks)
            var logCols = b.Tone == PanelModel.FeedCards || b.Tone == PanelModel.FeedTimeline ? (FdName, FdOut, 0f) : FeedColumns(col, fights.SelectMany(f => (f.Items ?? new List<Block>()).Where(x => x.Kind == "entry")).ToList());
            for (int i = 0; i < fights.Count; i++)
            {
                if (i > 0) Spacer(col, b.Tone == PanelModel.FeedTimeline ? 6 : 22);
                var f = fights[i];
                FightHead(col, f);
                var entries = (f.Items ?? new List<Block>()).Where(x => x.Kind == "entry").ToList();
                if (b.Tone == PanelModel.FeedCards) FeedCards(col, entries);
                else if (b.Tone == PanelModel.FeedTimeline) FeedTimeline(col, entries, i + 1 < fights.Count ? (f.Items ?? new List<Block>()).FirstOrDefault(x => x.Kind == "gap") : null);
                else FeedLog(col, entries, logCols);
            }
        }

        // Log (prototype A): one line per foe, time, the foe, what became of it, then (0.8.1, Joost in game) the dealt damage as a damage bar
        // (DamageRowsUi.cs: the types' colours, one scale for the feed's page with its zero at a quarter of the track, the true-length line, the
        // numbers inside the parts, no share line: one line a hit), and what you received after it as quiet words, in a column as wide as the page's
        // widest (FdRecvMax at most, then it wraps). Gold and the types' colours mean dealt in Battle; received stays words
        const float FdRecvMax = 150, FdRecvGap = 12;
        /// <summary>The Log's outcome column at least holds the commonest outcome's words: "defeated by you".</summary>
        const string FdOutLeast = "defeated by you";

        // the received words ("received pierce 12 · poison 88"): each item held together by no-break spaces, so a line breaks only at " · ", never
        // between a type and its amount (v081-fix's FeedLine does the same: "less than 1" wrapped as "less than" / "1" in game)
        static string FeedReceived(Block e)
        {
            var parts = (e.Items ?? new List<Block>()).Where(p => p.Kind == PanelModel.FeedReceived).ToList();
            // a line breaks only between items, at " · " (as FeedLine): "received blunt <1" stays together
            return parts.Count == 0 ? null : PanelModel.NoBreak(PanelModel.FeedReceived + " ") + string.Join(" · ", parts.Select(p => PanelModel.NoBreak(p.Title + " " + p.Value)).ToArray());
        }

        // the Log's columns over the whole page (every fight's entries), so every fight's bars keep one x and one scale, and the bar gets the rest:
        // the name as wide as the page's widest picture and name (FdName to FdNameMax, then it gives way: "Greydwarf Shaman" was cut), the
        // outcome as its widest words ("defeated by you" at least, FdOut at most, then they give way), the received words as their widest
        // (FdRecvMax at most, then they wrap at a " · "; none: no column)
        const float FdNameMax = 200;
        static (float name, float outcome, float recv) FeedColumns(RectTransform col, List<Block> entries)
        {
            var measure = Label(col, "", 16, PanelLook.Text, style: FontStyles.Bold); measure.textWrappingMode = TextWrappingModes.NoWrap;
            float name = FdName; foreach (var e in entries) { measure.text = e.Title ?? ""; name = Mathf.Max(name, 28 + Mathf.Ceil(measure.preferredWidth) + 2); }
            measure.fontStyle = FontStyles.Normal; measure.fontSize = FdText;
            float outcome = 0; foreach (var o in entries.Select(e => e.Note).Where(o => !string.IsNullOrEmpty(o)).Concat(new[] { FdOutLeast })) { measure.text = o; outcome = Mathf.Max(outcome, MarkPx + 8 + Mathf.Ceil(measure.preferredWidth) + 2); }
            measure.fontSize = 14;
            float recv = 0; foreach (var t in entries.Select(FeedReceived).Where(t => t != null)) { measure.text = t; recv = Mathf.Max(recv, Mathf.Ceil(measure.preferredWidth) + 2); }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
            return (Mathf.Min(name, FdNameMax), Mathf.Min(outcome, FdOut), Mathf.Min(recv, FdRecvMax));
        }

        static void FeedLog(RectTransform col, List<Block> entries, (float name, float outcome, float recv) cols)
        {
            var recvW = cols.recv;
            float xName = FdTime, xOut = xName + cols.name + 8, xDmg = xOut + cols.outcome + 8, wDmg = Column - xDmg;
            var recv = entries.Select(FeedReceived).ToList();
            var trackW = Mathf.Max(80f, wDmg - (recvW > 0 ? recvW + FdRecvGap : 0));
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var r = BtRow(col, FdRow); var h = FdRow;
                if (recv[i] != null)
                {
                    var t = Label(r, recv[i], 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
                    var th = Mathf.Max(LineBox(14), Wrapped(t, recvW)); h = Mathf.Max(FdRow, th + 14);
                    t.rectTransform.Box(xDmg + trackW + FdRecvGap, (h - th) / 2, recvW, th);
                }
                if (h > FdRow) Size(r, -1, h);
                BtText(r, e.Text, 0, 0, FdTime - 6, FdRow, 14, PanelLook.Faint);
                BtMarker(r, e.Icon, xName, (FdRow - 22) / 2, 22);
                BtText(r, e.Title, xName + 28, 0, cols.name - 28, FdRow, 16, PanelLook.Text, style: FontStyles.Bold);
                MarkImg(r, e.Tone, xOut, (FdRow - MarkPx) / 2, MarkPx);
                BtText(r, e.Note, xOut + MarkPx + 8, 0, cols.outcome - MarkPx - 8, FdRow, FdText, PanelLook.Muted);
                var dealt = (e.Items ?? new List<Block>()).Where(p => p.Kind == PanelModel.FeedDealt).ToList();
                if (dealt.Count > 0)
                {
                    var form = PanelModel.BarFormOf(dealt, true);
                    var bar = Node("Dealt", r); bar.Box(xDmg, (FdRow - FdBarH) / 2, trackW, FdBarH);
                    DamageBar(bar, form, null, e.Fraction, true, 0, trackW, shares: false);
                }
                BtRect(r, "Rule", BtLine, 0, h - 1, Column, 1);
            }
        }

        // the Log's bar with its true-length line under it: the track, the inner air and the line
        static readonly float FdBarH = PanelModel.DamageTrack + PanelModel.DamageInner + PanelModel.DamageTrue;

        // Cards (prototype B): three to a row, each foe its own item band: name and time, what became of it, the dealt damage as the damage bar, received in words
        static void FeedCards(RectTransform col, List<Block> entries)
        {
            Spacer(col, 10);
            const int per = 3; const float pad = 12;
            var w = (Column - (per - 1) * FdCardGap) / per; var inner = w - 2 * pad;
            for (int k = 0; k < entries.Count; k += per)
            {
                var row = entries.Skip(k).Take(per).ToList();
                var r = BtRow(col, 120);
                float tallest = 0;
                for (int j = 0; j < row.Count; j++)
                {
                    var e = row[j]; var x = j * (w + FdCardGap);
                    var card = Node("Card", r); card.Box(x, 0, w, 120);
                    var bg = RoundedPart(card, "Band", BtBand); bg.rectTransform.Stretch();
                    BtMarker(card, e.Icon, pad, 10, 20);
                    var time = BtText(card, e.Text, pad, 10, inner, 22, 14, PanelLook.Faint, TextAlignmentOptions.MidlineRight);
                    BtText(card, e.Title, pad + 26, 10, inner - 26 - Mathf.Ceil(time.preferredWidth) - 6, 22, 16, PanelLook.Text, style: FontStyles.Bold);
                    MarkImg(card, e.Tone, pad, 39, MarkPx);
                    BtText(card, e.Note, pad + MarkPx + 8, 34, inner - MarkPx - 8, 22, FdText, PanelLook.Muted);
                    // 0.8.1 (Joost, who loves the Cards): the dealt damage as the damage bar at the card's inner width, on the feed page's one scale as the
                    // Log's (zero at a quarter, the true-length line, the numbers inside the parts, a too narrow part's number in a tag on hover); the
                    // dealt words go, the numbers are in the bar. Received stays quiet words under it. A foe you never hit has no bar
                    var dealt = (e.Items ?? new List<Block>()).Where(p => p.Kind == PanelModel.FeedDealt).ToList();
                    float y = 62, lh = 0;
                    if (dealt.Count > 0)
                    {
                        var bar = Node("Dealt", card); bar.Box(pad, y, inner, FdBarH);
                        DamageBar(bar, PanelModel.BarFormOf(dealt, true), null, e.Fraction, true, 0, inner, shares: false);
                        y += FdBarH + 6;
                    }
                    var recv = FeedReceived(e);
                    if (recv != null)
                    {
                        var line = Label(card, recv, 14, PanelLook.Muted, align: TextAlignmentOptions.TopLeft);
                        lh = Wrapped(line, inner); line.rectTransform.Box(pad, y, inner, lh);
                    }
                    else y -= 6;
                    tallest = Mathf.Max(tallest, y + lh + pad);
                }
                Size(r, -1, tallest + FdCardGap);
                foreach (RectTransform c in r) c.sizeDelta = new Vector2(c.sizeDelta.x, tallest);
            }
        }

        // Timeline (prototype C): a time axis down the left, each foe a mark on it with the time before it, name and outcome, its damage under them;
        // the quiet time to the fight before as a line on the axis (even spacing, not to scale)
        static void FeedTimeline(RectTransform col, List<Block> entries, Block gap)
        {
            float x = FdAxis + 16, w = Column - x;
            for (int k = 0; k < entries.Count; k++)
            {
                var e = entries[k];
                var r = BtRow(col, 52);
                var dmg = BtRich(r, "feed", FeedLine(e), x, 26, w, 22, FdText, PanelLook.Text);
                var dh = Wrapped(dmg, w); dmg.rectTransform.Box(x, 26, w, dh);
                var h = 26 + dh + 12; Size(r, -1, h);
                float axTop = k == 0 ? 8 : 0, axH = k == entries.Count - 1 && gap == null ? 14 - axTop : h - axTop;   // from the first mark down to the last one (or on to the quiet time)
                BtRect(r, "Axis", PanelLook.Rule, FdAxis - 1, axTop, 2, axH);
                BtText(r, e.Text, 0, 0, FdAxis - 14, 26, 14, PanelLook.Faint, TextAlignmentOptions.MidlineRight);
                var under = Img(r, "Ground", PanelLook.Circle, PlateColour); under.preserveAspect = false; under.rectTransform.Box(FdAxis - MarkPx / 2 - 1, 5, MarkPx + 2, MarkPx + 2);   // the axis stops at a ring
                MarkImg(r, e.Tone, FdAxis - MarkPx / 2, 6, MarkPx);
                var name = BtText(r, e.Title, x, 0, w * 0.5f, 26, 16, PanelLook.Text, style: FontStyles.Bold);
                var nw = Mathf.Ceil(name.preferredWidth) + 2; name.rectTransform.sizeDelta = new Vector2(nw, name.rectTransform.sizeDelta.y);
                BtText(r, e.Note, x + nw + 10, 0, Mathf.Max(40, w - nw - 10), 26, FdText, PanelLook.Muted);
            }
            if (gap == null) return;
            var g = BtRow(col, FdGapRow);
            BtRect(g, "Axis", PanelLook.Rule, FdAxis - 1, 0, 2, FdGapRow);
            BtText(g, gap.Text, x, 0, w, FdGapRow, 14, PanelLook.Faint, style: FontStyles.Italic);
        }

        // ----- Last fight: the foes fought, what the group received (its rows per player are the Everyone chip's: EveryoneUi.GroupRows) -----

        static void FightFoes(RectTransform col, Block b)
        {
            var foes = (b.Items ?? new List<Block>()).Where(i => i.Kind == "foe").ToList();
            if (foes.Count == 0) return;
            var head = Line(col, 8);
            var t = Label(head, (b.Title ?? "").ToUpperInvariant(), 14, PanelLook.Muted); t.characterSpacing = 8; t.textWrappingMode = TextWrappingModes.NoWrap;
            Label(head, b.Value, 15, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(b.Text)) Label(head, b.Text, PanelLook.MinText, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;   // "your damage to each"
            Node("Rest", head).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var key = (b.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "pipkey");
            if (key != null) PipKeyLine(head, key);
            // 0.8 (Joost's damage bars): one damage row per kind, picture, name and "×N", the marks under the name, your damage by type
            DamageRows(col, foes, true);
        }

        static void FightReceived(RectTransform col, Block b)
        {
            var row = Line(col, 8);
            Label(row, b.Value, 17, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            var people = (b.Items ?? new List<Block>()).Where(i => i.Kind == "player").ToList();
            Label(row, b.Title + ", " + b.Text + (people.Count > 0 ? ":" : ""), FdText, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            foreach (var p in people)
            {
                Size(Node("Gap", row), 6, 1);
                Size(RoundedPart(row, "Swatch", Hex(p.Colour, PanelLook.Accent)), 12, 12);
                Label(row, p.Title, FdText, PanelLook.Text).textWrappingMode = TextWrappingModes.NoWrap;
                Label(row, p.Value, FdText, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            }
        }
    }
}
