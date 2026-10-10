using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8: the Foes page's keys (Chapters/FoesKeys.cs): A/D, the arrows and the D-pad move the cursor between the foe rows, Enter, Space or the
    /// pad's A open or close the cursor's foe. A row the keys reached is brought into view (scrolled only as far as it needs, with the ranking
    /// under it when it is open); a hover or an update never scrolls. Page Up and Page Down, and the pad's right stick, scroll every page (0.7
    /// scrolled by the wheel only).
    /// </summary>
    public partial class PanelUi
    {
        // the cursor's row on the page just drawn (BattleFoeTable), and whether a key moved it so it must be on screen
        static RectTransform foeCursorRow; static bool foeFollow;
        static readonly Vector3[] followCorners = new Vector3[4];

        // read every frame: nothing is looked up (or allocated) until one of its keys is down on the Foes page
        bool FoesKeys()
        {
            if (view == null || view.ShowAbout || view.Active != Chapter.Battle || view.Page != "foes") return false;
            var d = Key(KeyCode.A) || Key(KeyCode.LeftArrow) || Button("JoyDPadLeft") ? -1 : Key(KeyCode.D) || Key(KeyCode.RightArrow) || Button("JoyDPadRight") ? 1 : 0;
            var enter = d == 0 && (Key(KeyCode.Return) || Key(KeyCode.KeypadEnter) || Key(KeyCode.Space) || Button("JoyButtonA"));
            if ((d == 0 && !enter) || !PanelModel.OnFoesTable(view)) return false;   // By damage type: A/D turn the chapters as everywhere
            if (d != 0 ? PanelModel.StepFoe(state, view, d) : PanelModel.ToggleFoe(state, view)) { foeFollow = true; Render(true); }
            return true;
        }

        // 0.8: Page Up and Page Down glide the page a viewport's worth (less a line, so the last line stays in view), as the wheel glides; the
        // pad's right stick scrolls it while held (as the game's own Compendium text does; the stick is free while the book blocks the camera)
        const float StickDead = 0.15f, StickSpeed = 1100f;   // px per second at full tilt
        void PageKeys()
        {
            var d = ZInput.GetKeyDown(KeyCode.PageDown, false) ? 1 : ZInput.GetKeyDown(KeyCode.PageUp, false) ? -1 : 0;
            var stick = d == 0 ? ZInput.GetJoyRightStickY() : 0f;   // > 0: pushed down (ZInput flips the stick's y)
            if (d == 0 && Mathf.Abs(stick) < StickDead) return;
            var s = PageScroller();
            if (s == null) return;
            var room = Room(s);
            if (room <= 0f) return;
            var from = s.Easing ? s.Target : s.Rect.content.anchoredPosition.y;
            var by = d != 0 ? d * Mathf.Max(WheelStep, s.Rect.viewport.rect.height - 48f) : stick * StickSpeed * Time.unscaledDeltaTime;
            s.Target = Mathf.Clamp(from + by, 0f, room); s.Easing = true;
        }

        // the content column's scroll area (no lookup closure: PageKeys reads it every frame the stick is held)
        Scroll PageScroller()
        {
            for (int k = 0; k < scrollers.Count; k++) { var x = scrollers[k]; if (x.Rect && x.Rect.content == content && x.Rect.viewport) return x; }
            return null;
        }

        // after a key moved the cursor: the content column glides just far enough to show the cursor's row (and its open ranking)
        void FollowFoe()
        {
            if (!foeFollow) return;
            foeFollow = false;
            var row = foeCursorRow;
            var s = PageScroller();
            if (!row || s == null) return;
            Canvas.ForceUpdateCanvases();
            row.GetWorldCorners(followCorners);
            var top = content.rect.yMax - content.InverseTransformPoint(followCorners[1]).y;
            var bottom = content.rect.yMax - content.InverseTransformPoint(followCorners[0]).y;
            var at = ScrollAt(s);
            var to = PanelModel.GridScroll(at, true, top - 12f, bottom + 12f, s.Rect.viewport.rect.height, Room(s));
            if (Mathf.Abs(to - at) > 0.5f) { s.Target = to; s.Easing = true; }
        }
    }
}
