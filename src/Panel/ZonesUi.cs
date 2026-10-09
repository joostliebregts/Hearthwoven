using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    // Zones (design B, ZonesModel.cs): the stone zone for your character's counts, the ember zone for this PC's, drawn with
    // the kit's own sliced sprites, tinted once when the page is built (no new art, no shaders, nothing computed per frame):
    // the meter fill (neutral grey base) multiplied by the zone's ground, the meter track as its edge, and for the ember
    // zone a thin fill along the top as its rim. The glyph is the source sprite (src-stone: the tally board, src-hearth:
    // the fire), dimmed with the rim while the ember zone is empty.
    public partial class PanelUi
    {
        static readonly Color StoneGround = new Color(0.20f, 0.215f, 0.235f, 1f), StoneEdge = new Color(0.62f, 0.66f, 0.72f, 1f),
            StoneInk = new Color(0.84f, 0.82f, 0.75f), StoneQuiet = new Color(0.65f, 0.63f, 0.56f),
            EmberGround = new Color(0.15f, 0.09f, 0.045f, 1f), EmberEdge = new Color(0.62f, 0.38f, 0.16f, 1f), EmberRim = new Color(0.91f, 0.66f, 0.28f, 1f),
            EmberDim = new Color(0.36f, 0.23f, 0.11f, 1f), EmberInk = new Color(0.94f, 0.74f, 0.38f), EmberQuiet = new Color(0.71f, 0.54f, 0.34f),
            EmberLine = new Color(0.89f, 0.79f, 0.62f), StoneNumber = new Color(0.94f, 0.90f, 0.82f),
            ServerGround = new Color(0.075f, 0.13f, 0.15f, 1f), ServerEdge = new Color(0.33f, 0.52f, 0.58f, 1f), ServerInk = new Color(0.62f, 0.82f, 0.88f), ServerQuiet = new Color(0.52f, 0.68f, 0.73f);   // fix4-rest: the server's book, a cool teal apart from the stone and the ember
        const int ZonePadX = 16, ZonePadTop = 10, ZonePadBottom = 10;

        static void Zone(RectTransform col, Block b, Action<RectTransform, Block> child)
        {
            var stone = b.Id == PanelModel.SrcCharacter; var server = b.Id == PanelModel.SrcServer; var empty = b.Tone == PanelModel.ZoneEmpty; var tight = b.Tone == PanelModel.ZoneTight;
            var box = VStack(col, tight ? 2 : 8);
            box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(ZonePadX, ZonePadX, (tight ? 6 : ZonePadTop) + (stone || server ? 0 : 3), tight ? 6 : ZonePadBottom);
            var ground = Kit(box, "ZoneGround", "meter-fill"); ground.color = stone ? StoneGround : server ? ServerGround : EmberGround;
            ground.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; ground.rectTransform.Stretch();
            var edge = Kit(box, "ZoneEdge", "meter-track"); edge.color = stone ? StoneEdge : server ? ServerEdge : empty ? EmberDim : EmberEdge;
            edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; edge.rectTransform.Stretch();
            if (!stone && !server)   // the ember rim along the top edge, dim while nothing is written yet
            {
                var rim = Img(box, "ZoneRim", null, empty ? EmberDim : EmberRim); rim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var r = rim.rectTransform; r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 1);
                r.offsetMin = new Vector2(3, -4); r.offsetMax = new Vector2(-3, -1);
            }

            var head = Line(box, 8); Size(head, -1, tight ? 20 : 22);
            var glyph = VocabImg(head, "Glyph", stone ? "src-stone" : server ? "src-fellows" : "src-hearth", empty ? new Color(0.6f, 0.6f, 0.6f, 0.45f) : Color.white); Size(glyph, 20, 20);
            var title = Label(head, b.Title ?? "", 15, stone ? StoneInk : server ? ServerInk : EmberInk, style: FontStyles.UpperCase | FontStyles.Bold);
            title.characterSpacing = 14; title.textWrappingMode = TextWrappingModes.NoWrap;
            Size(Node("Gap", head), 0, 1).flexibleWidth = 1;
            var right = Label(head, b.Text ?? "", 15, stone ? StoneQuiet : server ? ServerQuiet : EmberQuiet, align: TextAlignmentOptions.MidlineRight);
            right.textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(b.Note)) Label(box, b.Note, 15, EmberLine);

            var outer = Column;
            try
            {
                zoneTight = tight;
                Column = outer - 2 * ZonePadX;
                foreach (var x in b.Items ?? new List<Block>())
                {
                    // a number lifted out of another block (Tone "second") keeps the size it had there
                    if (x.Kind == "hero" && x.Tone == PanelModel.ZoneSecond) { HeroNumber(Line(box, 0, TextAnchor.LowerLeft), x, HeroSecond, HeroSecondLabel); continue; }
                    child(box, x);
                }
            }
            finally { Column = outer; zoneTight = false; }
            // the stone zone's big numbers are carved: cream instead of gold (sinceB), recoloured once here
            if (stone) foreach (var t in box.GetComponentsInChildren<TextMeshProUGUI>(true)) if (t.color == PanelLook.Gold && t.fontSize >= HeroSecond) t.color = StoneNumber;
        }

        // the bosses the character defeated (the game's kill counter): Codex's ring with the game's own trophy in it, the name beside
        const float ZoneBoss = 34;

        static void Bosses(RectTransform col, Block b)
        {
            var row = Line(col, 34); Size(row, -1, ZoneBoss);
            if (!string.IsNullOrEmpty(b.Title)) { Label(row, b.Title, 15, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft).textWrappingMode = TextWrappingModes.NoWrap; Size(Node("Gap", row), 10, 1); }   // what the marks are
            foreach (var boss in b.Items ?? new List<Block>())
            {
                var g = Line(row, 12);
                var holder = Node("Boss", g); Size(holder, ZoneBoss, ZoneBoss);
                var trophy = PanelLook.Icon(boss.Icon);
                if (trophy)
                {
                    var tr = Img(holder, "Trophy", trophy, Color.white).rectTransform; var size = ZoneBoss * 0.86f;
                    tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(size, size);
                }
                VocabImg(holder, "Ring", "boss-ring", Color.white).rectTransform.Stretch();
                Label(g, boss.Title ?? "", 20, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft).textWrappingMode = TextWrappingModes.NoWrap;
            }
        }
    }
}
