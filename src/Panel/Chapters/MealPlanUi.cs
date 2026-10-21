using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Meals' "Plan a meal" (Chapters/MealPlanModel.cs; 0.9.5: Deeds > Meal plan, its own page): its foods as a named variant of the item grid (DeedsUi.ItemGrid, Tone MealPlanTone).
    /// 0.9.3 (Joost: "lets make this more dense ... And make the bargraph thicker. like in our damage graphs"): a compact tile, five or six to a line on
    /// the plate (at least MpTileMin wide). On top the food's name across the tile, on one line (a grid with a name too long for that gives every
    /// tile a second line: the grid's cells are one size, and a name is never cut); under it the game's picture, What to use's status as a small mark
    /// on its corner (the book's round marks, BattleFeedUi.MarkImg: a gold dot made or owned, a gold ring can make; the key under the chips draws the
    /// same marks, PlanMarksKey), then the book's damage bar
    /// (DamageRowsUi.DamageBar: the track, rounded parts, each part's number inside where it fits) from zero on one scale for the planner, in Your
    /// combinations' colours, and how long it lasts ("50m"). A food not known yet quiet (What to use's UtQuiet), a food in a slot outlined in gold, its
    /// name gold, the keys' cursor in the focus ring. A tile is a click (its Link). The meal's row itself is Your combinations' row
    /// (DamageRowsUi.DamageRow, an empty slot a dashed square). Mirrored by planGrid() in preview/panel-preview.html.
    /// </summary>
    public partial class PanelUi
    {
        // the tile's least width (five to a line on the plate) and the most to a line, its padding, the picture beside the bar and the gap after it,
        // the corner mark and how far it stands out of the picture, the gap between the name and the bar's line, between the bar and its time
        const float MpTileMin = 140, MpPad = 6, MpPic = 20, MpPicGap = 5, MpMark = 8, MpMarkOut = 2, MpRowGap = 4, MpTimeGap = 5;
        const int MpPerMax = 6;

        static void PlanGrid(RectTransform col, Block b, Func<string, Action> link)
        {
            var items = b.Items ?? new List<Block>();
            if (items.Count == 0) return;
            var per = Mathf.Clamp(Mathf.FloorToInt((Column + GridGap) / (MpTileMin + GridGap)), 1, MpPerMax);
            var w = Mathf.Floor((Column - GridGap * (per - 1)) / per);
            float nameW = w - MpPad - MpPad, line = LineBox(PanelLayout.Text.Floor);
            // a name wider than the tile gives every tile of the grid a second line; the time column as wide as the grid's widest ("50m"), so every
            // bar of the grid ends at the same x
            var measure = Label(col, "", PanelLayout.Text.Floor, PanelLook.Text); measure.textWrappingMode = TextWrappingModes.NoWrap;
            var wraps = false; float timeW = 0;
            foreach (var i in items)
            {
                measure.text = i.Title ?? ""; if (measure.preferredWidth > nameW) wraps = true;
                if (!string.IsNullOrEmpty(i.Text)) { measure.text = i.Text; timeW = Mathf.Max(timeW, Mathf.Ceil(measure.preferredWidth) + DrTextSlack); }
            }
            measure.gameObject.SetActive(false); Destroy(measure.gameObject);
            float nameH = wraps ? line + line : line, top = MpPad + nameH + MpRowGap, barX = MpPad + MpPic + MpPicGap;
            var trackW = w - barX - MpPad - (timeW > 0 ? MpTimeGap + timeW : 0);
            var h = top + PanelModel.DamageTrack + MpPad;
            var grid = Node("PlanGrid", col);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.spacing = new Vector2(GridGap, GridGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = per;
            g.cellSize = new Vector2(w, h);
            foreach (var i in items)
            {
                var tile = Kit(grid, i.Title, "meter-track").rectTransform;   // the item grid's tile
                if (i.Tone == PanelModel.PlanUnknownTone) tile.gameObject.AddComponent<CanvasGroup>().alpha = UtQuiet;   // not known yet: quiet, as What to use's row
                var name = BtText(tile, i.Title, MpPad, MpPad, nameW, nameH, PanelLayout.Text.Floor, i.Selected ? PanelLook.Gold : PanelLook.Text, TextAlignmentOptions.TopLeft);
                name.textWrappingMode = wraps ? TextWrappingModes.Normal : TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Overflow;
                var p = Pic(tile, i.Icon, MpPic); p.anchorMin = p.anchorMax = p.pivot = new Vector2(0, 1); p.anchoredPosition = new Vector2(MpPad, -(top + (PanelModel.DamageTrack - MpPic) / 2));
                if (!string.IsNullOrEmpty(i.Note)) PlanMark(tile, i.Note, MpPad + MpPic - MpMark + MpMarkOut, top + (PanelModel.DamageTrack + MpPic) / 2 - MpMark + MpMarkOut);
                // one serving as the book's damage bar: from zero on the planner's one scale (Fraction2), the numbers inside the parts where they fit
                var parts = (i.Items ?? new List<Block>()).Where(x => x.Kind == "part").ToList();
                if (parts.Count > 0)
                {
                    var bar = Node("Bar", tile); bar.Box(barX, top, trackW, PanelModel.DamageTrack);
                    DamageBar(bar, PanelModel.BarFormOf(parts, true), null, i.Fraction2, true, 0, trackW, shares: false, trueLine: false, exact: true);
                }
                if (timeW > 0 && !string.IsNullOrEmpty(i.Text))
                    BtText(tile, i.Text, w - MpPad - timeW, top, timeW, PanelModel.DamageTrack, PanelLook.MinText, PanelLook.Muted, TextAlignmentOptions.MidlineRight).textWrappingMode = TextWrappingModes.NoWrap;
                if (i.Selected) Edge(tile, PanelLook.Gold);   // in a slot
                if (i.Cursor) { FocusRing(tile); foeCursorRow = tile; }   // the keys' cursor (MealPlanKeys): brought into view as a foe row is
                if (!string.IsNullOrEmpty(i.Link) && link?.Invoke(i.Link) is Action act)
                {
                    tile.GetComponent<Image>().raycastTarget = true; tile.gameObject.AddComponent<Press>().Act = act;
                }
            }
        }

        // What to use's status as a corner mark on the picture: made or owned a gold dot, can make a gold ring; the book's round marks (MarkImg:
        // PanelLook.Circle and PanelLook.Ring, as the deed feed's made and upgraded), so the key under the chips (PlanMarksKey) draws the very same
        static void PlanMark(RectTransform parent, string status, float x, float top) =>
            MarkImg(parent, PanelModel.RingMark(status) ? PanelModel.PipOpen : "made", x, top, MpMark);   // 0.9 Armor sets: worn is the ring too

        // the key under the chips (PanelModel.PlanMarksTone, Joost in game 0.9.8: "why not show the actual icons there?"): each mark as a tile draws
        // it, then its meaning: the gold dot, the ring (PlanMark), a food's picture as quiet as a tile not known yet (UtQuiet); KeyApart between
        static void PlanMarksKey(RectTransform col, Block b)
        {
            var key = Line(col, KeyMarkGap);
            foreach (var m in b.Items ?? new List<Block>())
            {
                if (key.childCount > 0) Size(Node("Gap", key), KeyApart - KeyMarkGap, 1);
                if (m.Tone == PanelModel.PlanUnknownTone) Pic(key, m.Icon, MpPic).gameObject.AddComponent<CanvasGroup>().alpha = UtQuiet;
                else { var holder = Node("Key", key); Size(holder, MpMark, MpMark); PlanMark(holder, m.Note, 0, 0); }
                Label(key, m.Title, PanelLayout.Text.Floor, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        // on Deeds > Meal plan (0.9.5, its own page): A/D (the arrows, the D-pad) move a cursor over the foods and Enter, Space or the pad's A puts the
        // cursor's food in a slot or takes it out (Chapters/MealPlanModel.cs); Q/E still turn the chapters. Nothing is looked up until one of its keys is
        // down there.
        bool MealPlanKeys()
        {
            if (view == null || view.ShowAbout || view.Active != Chapter.Deeds || view.Page != PanelModel.MealPlanPageId) return false;
            var d = Key(KeyCode.A) || Key(KeyCode.LeftArrow) || Button("JoyDPadLeft") ? -1 : Key(KeyCode.D) || Key(KeyCode.RightArrow) || Button("JoyDPadRight") ? 1 : 0;
            var enter = d == 0 && (Key(KeyCode.Return) || Key(KeyCode.KeypadEnter) || Key(KeyCode.Space) || Button("JoyButtonA"));
            if (d == 0 && !enter) return false;
            if (!(d != 0 ? PanelModel.StepPlanFood(state, view, d) : PanelModel.TogglePlanFood(state, view))) return false;   // no foods shown: the keys are the book's
            foeCursorRow = null; foeFollow = true; Render(true);
            return true;
        }
    }
}
