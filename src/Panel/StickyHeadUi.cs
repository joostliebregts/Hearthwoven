using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8.6 the sticky head (Joost in game, a friend's Battle feed: "the Log, Cards and Timeline views sticky at the top. If you scroll underneath
    /// it, it needs to stay up there"). The strip that leads the plate (PageHead.cs: its caption, the view chips, About these numbers) stays at the
    /// plate's top on every page whose plate scrolls, and the islands scroll under it. One object moves: the strip, drawn into the scroll content as
    /// before (FeatsUi.FeatBandBlock), is reparented to stripPin (made once in Build, over the plate's scroll area) at the very place it was drawn,
    /// and the scroll area's visible part starts at its foot (ScrollBox). At rest the page looks as it did; scrolled, the rows pass under the
    /// strip's bottom edge, clipped there by the area's mask, so nothing is drawn over the strip or shows through it. The wheel's range, Page Up and
    /// Page Down and the Foes cursor's follow (FoesKeysUi.FollowFoe) all measure the area under the strip, so a row the keys reach shows below it.
    /// While rows lie above, the shade hangs under the strip (Fades, with the top fade). Nothing per row, nothing per frame but the shade's on and
    /// off. The mirror: pinHead() in preview/panel-preview.html, checked by render-check's sticky-head rule.
    /// 0.9.5 (Meal plan, Joost in game: "an always visible anchored 'health/stamina/eitr' bar"): an island the model marks Pin (Island.Pin: Meal plan's
    /// meal, the slots and the summed bar) is pinned with the strip when it comes right after it: the two stacked in the holder, the plate's gap
    /// between them, the scroll area starting under the island, the shade under it. One object more moves; nothing else changes.
    /// </summary>
    public partial class PanelUi
    {
        RectTransform stripPin;
        Image stripShade;
        /// <summary>The strip this drawing made first (FeatsUi.FeatBandBlock), for PinStrip; null: none yet.</summary>
        static RectTransform stripDrawn;
        /// <summary>0.9.5: the island this drawing marked to pin with the strip (Island.Pin, IslandsUi.IslandBox); null: none.</summary>
        static RectTransform pinIslandDrawn;
        /// <summary>How far down the scroll area starts for the pinned strip: the plate's top padding and the strip's height; 0: nothing pinned.</summary>
        float pinDrop;

        // the holder hangs from the plate's top, as wide as the plate's inside (its padding off both sides); the shade under it is made once too
        void BuildPin(RectTransform right)
        {
            stripPin = Node("StripPin", right);
            stripShade = Img(right, "StripShade", FadeSprite(true), PanelLook.StripShade);
            stripShade.preserveAspect = false; stripShade.enabled = false;
            foreach (var r in new[] { stripPin, stripShade.rectTransform }) { r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(0, 1); }
            stripPin.gameObject.SetActive(false); stripShade.gameObject.SetActive(false);
            scrollers[scrollers.Count - 1].Shade = stripShade;   // the content column's scroll area, made just before
        }

        // a new drawing: the last one's strip goes (its plain texts kept for reuse, as Clear does) and the scroll area has the plate's whole room
        void Unpin()
        {
            stripDrawn = null; pinIslandDrawn = null; pinDrop = 0f;
            if (!stripPin) return;
            Clear(stripPin);
            stripPin.gameObject.SetActive(false); stripShade.gameObject.SetActive(false);
        }

        // after the plate is drawn: its strip leads it (the first object shown in the content column) on any plate but the Feats chapter's full
        // one (its grid takes the room that is left, FeatsUi.FeatsSettle). The strip keeps its place: the plate's top padding above it; the gap
        // between it and the first island becomes the scroll content's top padding
        void PinStrip()
        {
            var strip = stripDrawn; stripDrawn = null;
            var island = pinIslandDrawn; pinIslandDrawn = null;
            if (!strip || strip.parent != content || plateFull || FirstShown(content) != strip) return;
            var h = LayoutUtility.GetPreferredHeight(strip);
            // 0.9.5: the island marked Pin rides under the strip when it is the next thing shown (measured at the plate's width, laid out first)
            float ih = 0f;
            if (island && island.parent == content && NextShown(content, strip) == island)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                ih = LayoutUtility.GetPreferredHeight(island);
                if (ih <= 0f) island = null;
            }
            else island = null;
            var all = island ? h + PlateGap + ih : h;
            pinDrop = PlatePadTop + all;
            float side = PlateInset + PlatePadX, top = PlateTop + PlateInset + PlatePadTop;
            stripPin.offsetMin = new Vector2(side, -(top + all)); stripPin.offsetMax = new Vector2(-side, -top);
            stripShade.rectTransform.offsetMin = new Vector2(side, -(top + all + PanelLayout.Plate.StripShade)); stripShade.rectTransform.offsetMax = new Vector2(-side, -(top + all));
            stripPin.gameObject.SetActive(true); stripShade.gameObject.SetActive(true); stripShade.enabled = false;
            strip.SetParent(stripPin, false);
            if (!island) strip.Stretch();
            else
            {
                Hang(strip, 0f, h);
                island.SetParent(stripPin, false); Hang(island, h + PlateGap, ih);
            }
            content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(PlatePadX, PlatePadX, (int)PlateGap, PlatePadBottom);
            ScrollBox((RectTransform)content.parent);
        }

        // the plate's scroll area: the plate inside its edge, less the pinned strip's room at its top
        void ScrollBox(RectTransform box)
        {
            PlateBox(box, PlateInset);
            box.offsetMax -= new Vector2(0, pinDrop);
        }

        // a child of the holder hung from its top: as wide as the holder, y px down, h px tall (its layout group lays its own children out)
        static void Hang(RectTransform r, float y, float h)
        {
            r.anchorMin = r.pivot = new Vector2(0, 1); r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(0, -(y + h)); r.offsetMax = new Vector2(0, -y);
        }

        static Transform NextShown(Transform t, Transform after)
        {
            for (int i = after.GetSiblingIndex() + 1; i < t.childCount; i++) if (t.GetChild(i).gameObject.activeSelf) return t.GetChild(i);
            return null;
        }

        static Transform FirstShown(Transform t)
        {
            for (int i = 0; i < t.childCount; i++) if (t.GetChild(i).gameObject.activeSelf) return t.GetChild(i);
            return null;
        }
    }
}
