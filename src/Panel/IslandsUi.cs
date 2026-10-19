using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8 layout D+ (Islands.cs; work/hearthwoven-0.8/density-feedback/FEEDBACK.md part 4): the plate drawn as its strip, then a calm grid of
    /// islands. An island is a plain greyish rounded rectangle (PanelLook.Island, no outline), its measures PanelLayout.Island; two half
    /// islands stand side by side, as tall as each other; a plain island is its blocks without a ground. Each island lays its blocks out
    /// at its own inner width (Column). Built once per fill, nothing per frame. Mirrored by islands() in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        static readonly float IslandPad = PanelLayout.Island.Pad, IslandGap = PanelLayout.Island.PairGap, IslandInner = PanelLayout.Island.Inner, IslandHeroTop = PanelLayout.Island.HeroTop, IslandHeroBottom = PanelLayout.Island.HeroBottom;
        /// <summary>A column narrower than this is a half island: a ranking in one column, a heading's dated label under it (the preview's COLUMN &lt; 500).</summary>
        static readonly float NarrowColumn = PanelLayout.Island.Narrow;
        /// <summary>A fellow's book: whose copy and when, at the strip's right end (PanelView.StripNote, set by Fill while the plate is drawn).</summary>
        static string stripNote;
        /// <summary>0.9: the plate's links that ride in the strip (PanelModel.StripLinks): a Deeds page's Feed and Sets.</summary>
        static List<Block> stripLinks = new List<Block>();
        /// <summary>0.8.5: the page whose Compare legend (CompareNow, CompareThen) or greyed chip's reason (CompareNote) leads the strip; null: none.</summary>
        static PanelView stripCompare;
        /// <summary>0.8.6: the Back button's press at the strip's left end (PanelView.Back: what Backspace does); null: the page has no back step.</summary>
        static System.Action stripBack;
        /// <summary>Drawing inside an island: no air before a heading (the padding is the air), no rules between rows, no item band behind a
        /// weapon or a foe (the island is that band).</summary>
        static bool islanded;

        void PlateIslands(RectTransform col, Block plate)
        {
            var band = (plate.Items ?? new List<Block>()).FirstOrDefault(b => b.Kind == "featband");
            if (band != null) Draw(col, band);   // the strip: the page's head (PageHead.cs)
            var plan = PanelModel.Islands(plate);
            legends.Clear(); legendsLater = true;
            try { DrawIslands(col, plan); }
            finally { legendsLater = false; }
            LayLegends(legends, PlateColumn - 2 * IslandPad);   // every legend of the page on one grid (a wide island's inner width)
            legends.Clear();
        }

        void DrawIslands(RectTransform col, List<Island> plan)
        {
            for (int i = 0; i < plan.Count; i++)
            {
                var island = plan[i];
                if (island.Span == PanelModel.IslandHalf)
                {
                    var pair = Row(col, IslandGap); pair.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;   // the two as tall as each other
                    var w = (PlateColumn - IslandGap) / 2;
                    IslandBox(pair, island, w);
                    if (i + 1 < plan.Count && plan[i + 1].Span == PanelModel.IslandHalf) IslandBox(pair, plan[++i], w);
                    else Size(Node("Rest", pair), w, 1);
                }
                else if (island.Span == PanelModel.IslandWide) IslandBox(col, island, PlateColumn);
                else foreach (var p in island.Parts) DrawPart(col, p);
            }
        }

        void IslandBox(RectTransform parent, Island island, float width)
        {
            var hero = island.Parts.Any(p => p.Block.Kind == "hero" || p.Block.Kind == PanelModel.FightHeadKind);   // 0.9: a fight's head is its hero
            var box = VStack(parent, IslandInner);
            // 0.9.6 Meal plan's pinned meal (Island.Pin): the book's least island padding above and below it (the hero's top), so the pinned row takes little height
            var pinned = island.Pin;
            box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset((int)IslandPad, (int)IslandPad, (int)(hero || pinned ? IslandHeroTop : IslandPad), (int)(pinned ? IslandHeroTop : hero ? IslandHeroBottom : IslandPad));
            IslandGround(box.gameObject);
            Size(box, width, -1);
            var outer = Column; var was = islanded;
            Column = width - 2 * IslandPad; islanded = true;
            try { foreach (var p in island.Parts) DrawPart(box, p); }
            finally { Column = outer; islanded = was; }
            if (island.Pin && !pinIslandDrawn) pinIslandDrawn = box;   // 0.9.5: Meal plan's meal, pinned with the strip (StickyHeadUi.PinStrip)
        }

        /// <summary>An island's ground on <paramref name="on"/>: PanelLook.Island sliced, tinted PanelLook.IslandGround.</summary>
        static Image IslandGround(GameObject on)
        {
            var ground = on.AddComponent<Image>(); ground.sprite = PanelLook.Island; ground.color = PanelLook.IslandGround; ground.raycastTarget = false;
            if (PanelLook.Island) { ground.type = Image.Type.Sliced; ground.pixelsPerUnitMultiplier = 1f; }
            return ground;
        }

        // one part: a switch's row, a player's island (Recent's group), the open About box (its button rides in the strip), or the block itself
        void DrawPart(RectTransform col, IslandPart p)
        {
            if (p.Row) { SwitchRow(col, p.Block, null, LinkTo); return; }
            if (p.Block.Kind == "aboutnumbers") { AboutBox(col, p.Block); return; }
            Draw(col, PanelModel.PartBlock(p));
        }

        // a window chip in the heading row; greyed with its own reason (Choice.Why, PageHead.cs): the reason under it while the pointer is on it,
        // or now under the one just pressed (WindowTip: a day window before the history, B17). Returns its width
        float WindowChip(RectTransform row, Choice c, PanelView v, System.Action click)
        {
            var w = Chip(row, c.Label, null, c.Selected, click, off: c.Disabled, pad: WindowPad);
            if (!c.Disabled || string.IsNullOrEmpty(c.Why)) return w;
            var chip = (RectTransform)row.GetChild(row.childCount - 1);
            chip.GetComponent<Image>().raycastTarget = true;   // greyed, but the pointer finds it
            var tag = ReasonTag(chip, c.Why);
            tag.gameObject.SetActive(v.WindowTip == c.Id);
            chip.gameObject.AddComponent<SwapOnHover>().On = tag.gameObject;
            return w;
        }

        // Recent's group, one player per island (DeedsUi.People with Tone IslandPersonTone): the shield and name, their items under it in two
        // columns, each item its picture, its number bold and its name muted, wrapping on two lines rather than cut ("St..." in 0.8's first build)
        static void PersonIsland(RectTransform col, Block p, System.Func<string, System.Action> link = null)
        {
            var head = Line(col, 10); Size(head, -1, 34);
            Marker(head, p.Icon, 34);
            OneLine(Label(head, p.Title, PanelLayout.Text.Lead, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft));
            if (!string.IsNullOrEmpty(p.Note))   // "+3 more": what grew past the island (RecentPage.GroupGains), quiet at the head's right end; a click shows every gain (0.8.2)
            {
                Node("Rest", head).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                MoreLink(OneLine(Label(head, p.Note, PanelLook.MinText, PanelLook.Muted, align: TextAlignmentOptions.MidlineRight, style: FontStyles.Italic)), p.NoteLink != null ? link?.Invoke(p.NoteLink) : null);
            }
            var items = p.Items ?? new List<Block>();
            var cellW = (Column - 14) / 2;
            for (int k = 0; k < items.Count; k += 2)
            {
                var line = Line(col, 14, TextAnchor.UpperLeft);
                for (int c = k; c < Mathf.Min(k + 2, items.Count); c++)
                {
                    var i = items[c];
                    var cell = Line(line, 8); Size(cell, cellW, -1);
                    var room = cellW;
                    if (!string.IsNullOrEmpty(i.Icon)) { Pic(cell, i.Icon, 30); room -= 30 + 8; }
                    if (!string.IsNullOrEmpty(i.Value)) { var n = OneLine(Label(cell, i.Value, PanelLayout.Text.Row, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Bold)); room -= Mathf.Ceil(n.preferredWidth) + 8; }
                    var since = Labelled(i) ? Since(cell, PanelLayout.Text.Floor, i) : null; if (since) { since.transform.SetAsLastSibling(); room -= Mathf.Ceil(since.preferredWidth) + 8; }
                    var quiet = !string.IsNullOrEmpty(i.Value) || i.Unrecorded || i.Tone == "quiet";
                    var name = Unrecorded(Label(cell, i.Title, PanelLayout.Text.Small, quiet ? PanelLook.Muted : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft, style: i.Tone == "quiet" ? FontStyles.Italic : FontStyles.Normal), i, 15);
                    name.textWrappingMode = TextWrappingModes.Normal; Size(name, Mathf.Max(40f, room), -1);   // a known width, so a long name wraps onto a second line instead of being cut
                    if (since) since.transform.SetAsLastSibling();
                }
            }
        }
    }
}
