using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8 hover on a bar part (work/hearthwoven-0.8/prototypes/PICKS.md section 4, pick B): pointing at a part of a bar rings it in gold and
    /// lights its row in the list under the bar; pointing at a row lights its part. Nothing moves: the ring (one per bar, drawn over its
    /// parts), the row's band and the row's words are built with the bar (PanelUi.BarWithList) and only change colour here; the ring takes the
    /// hovered part's anchors. A damage bar's part (0.8.2, every part) also shows its name and number: its share entry's own words are swapped
    /// for them (OnWords, set back on leaving), or, with no entry keying it, the page's one tag (HoverTag) is moved over it with them. No object
    /// is made, nothing is rebuilt and no string is built per hover: the words are made with the bar, only set here.
    /// Mirrored by the .hov rules in preview/panel-preview.html.
    /// </summary>
    sealed class BarHover : MonoBehaviour
    {
        /// <summary>The ring's line colour (#f0cd86) with its glow (the focus-ring sprite), a lit row's band (gold at 15 %), a lit row's
        /// name and number (#f6d896).</summary>
        internal static readonly Color Ring = new Color(0.941f, 0.804f, 0.525f, 1f), Band = PanelWash.HoverBand.Game, LitText = new Color(0.965f, 0.847f, 0.588f, 1f);
        /// <summary>How far the ring's line runs outside its part (the focus-ring sprite's line lies 8 px in from its edge).</summary>
        internal const float RingGap = 1f;

        internal RectTransform[] Parts;
        internal Image RingImage;
        internal Image[] Bands;
        internal TextMeshProUGUI[] Names, Numbers, Shares;
        internal Color[] NameColours;
        /// <summary>A damage row's share entry (DamageRowsUi.cs): lit, its words (Names) swap for the part's name and number ("Frost 37" for
        /// "Frost 11 %"), and back on leaving (OffWords: what it said); its width was kept for the longer of the two, so nothing moves.</summary>
        internal string[] OnWords, OffWords;
        /// <summary>A bar whose parts no entry keys (no share line, a player's line, Foes' Dealt, the armor bar): the page's one tag
        /// (PanelUi.PageTag) says TagWords[k] over the part, at TagAt[k] in TagBar's x (its centre, or its start when not TagCentred), TagTop
        /// from TagBar's top, kept between TagMin and TagMax; TagHides (Foes' total, whose place the tag takes) hides while it shows.</summary>
        internal HoverTag Tag; internal RectTransform TagBar; internal string[] TagWords; internal float[] TagAt;
        internal float TagTop, TagMin, TagMax; internal bool TagCentred = true; internal GameObject TagHides;
        int lit = -1;

        internal void Init(int n)
        {
            Parts = new RectTransform[n]; Bands = new Image[n];
            Names = new TextMeshProUGUI[n]; Numbers = new TextMeshProUGUI[n]; Shares = new TextMeshProUGUI[n]; NameColours = new Color[n];
        }

        /// <summary>The page's one tag shows <paramref name="words"/> over this bar's parts (PanelUi.PageTag); <paramref name="at"/> per part.</summary>
        internal void TagOver(HoverTag tag, RectTransform bar, string[] words, float[] at, float top, float min, float max, bool centred = true)
        {
            Tag = tag; TagBar = bar; TagWords = words; TagAt = at; TagTop = top; TagMin = min; TagMax = max; TagCentred = centred;
        }

        internal void Light(int k, bool on)
        {
            if (Parts == null || k < 0 || k >= Parts.Length) return;
            if (!on) { if (lit == k) Dim(k); return; }
            if (lit >= 0 && lit != k) Dim(lit);
            lit = k;
            var part = Parts[k];
            if (RingImage && part)
            {
                var r = RingImage.rectTransform; var pad = 8 + RingGap;
                r.anchorMin = part.anchorMin; r.anchorMax = part.anchorMax;
                r.offsetMin = part.offsetMin - new Vector2(pad, pad); r.offsetMax = part.offsetMax + new Vector2(pad, pad);
                RingImage.color = Ring;
            }
            if (Bands[k]) Bands[k].color = Band;
            if (Names[k]) Names[k].color = LitText;
            if (Numbers[k]) Numbers[k].color = LitText;
            if (Shares[k]) Shares[k].color = PanelLook.Text;
            if (OnWords != null && OnWords[k] != null && Names[k]) Names[k].text = OnWords[k];
            if (Tag != null && TagWords != null && Tag.Show(this, k) && TagHides) TagHides.SetActive(false);
        }

        void Dim(int k)
        {
            lit = -1;
            if (RingImage) RingImage.color = Color.clear;
            if (Bands[k]) Bands[k].color = Color.clear;
            if (Names[k]) Names[k].color = NameColours[k];
            if (Numbers[k]) Numbers[k].color = PanelLook.Text;
            if (Shares[k]) Shares[k].color = PanelLook.Faint;
            if (OnWords != null && OnWords[k] != null && Names[k]) Names[k].text = OffWords[k];
            if (Tag != null) { Tag.Hide(this, k); if (TagHides) TagHides.SetActive(true); }
        }
    }

    /// <summary>
    /// 0.8.2: the page's one hover tag (PanelUi.PageTag): a part of a bar that no share entry keys says its name and number in it ("Frost 37",
    /// "Edda 310"). Made once with the page, hidden, drawn over the page's blocks; pointing at a part sets its words and moves it over the part
    /// (in the bar's place, kept inside the bar's room), leaving hides it. One for the page instead of one per part: 4e72aff's tag per part and
    /// second entry label cost about 3.5 ms a drawing on the sample feed and 11 ms on a long one at the 0.8.1 bench's 58 µs an object (Joost felt
    /// 0.8.1's speed in game; test-panel/HoverCostTests.cs counts it).
    /// </summary>
    sealed class HoverTag
    {
        /// <summary>The words' room either side, and the tag's height over a part (the part's height and 2 px above and below it).</summary>
        internal const float Pad = 6, Rise = 4;
        internal Image Ground; internal TextMeshProUGUI Text;
        BarHover owner; int ownerPart = -1;

        /// <summary>Shows the bar's part <paramref name="k"/>'s words over it; false when the tag is gone (its page cleared).</summary>
        internal bool Show(BarHover bar, int k)
        {
            if (!Ground || !Text || bar.TagWords == null || k < 0 || k >= bar.TagWords.Length || !bar.TagBar) return false;
            owner = bar; ownerPart = k;
            Ground.gameObject.SetActive(true);   // shown first, then its words set and measured
            Text.text = bar.TagWords[k] ?? "";
            var tw = Mathf.Ceil(Text.preferredWidth) + 2 * Pad;
            var at = bar.TagAt[k]; var x = Mathf.Clamp(bar.TagCentred ? at - tw / 2 : at, bar.TagMin, bar.TagMax - tw);
            // its place in the bar's room (x from the bar's left, TagTop down from its top), carried into the page's content where the tag lives
            var room = bar.TagBar.rect; var g = Ground.rectTransform;
            var p = g.parent.InverseTransformPoint(bar.TagBar.TransformPoint(new Vector3(room.xMin + x, room.yMax - bar.TagTop, 0)));
            g.localPosition = new Vector3(p.x, p.y, 0); g.sizeDelta = new Vector2(tw, PanelModel.DamageBar + Rise);
            return true;
        }

        /// <summary>Hides the tag if it is still this bar's part's (the pointer may have reached the next part first).</summary>
        internal void Hide(BarHover bar, int k)
        {
            if (owner != bar || ownerPart != k) return;
            owner = null; ownerPart = -1;
            if (Ground) Ground.gameObject.SetActive(false);
        }
    }

    /// <summary>A bar part or a list row of one bar (BarHover): the pointer entering or leaving it lights or dims part and row together.</summary>
    sealed class HoverPart : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal BarHover Bar; internal int Index;
        public void OnPointerEnter(PointerEventData e) { if (Bar) Bar.Light(Index, true); }
        public void OnPointerExit(PointerEventData e) { if (Bar) Bar.Light(Index, false); }
    }

    /// <summary>
    /// 0.8.1 (Joost: one rule): anything you can point at shows that you point at it, a 1 px warm line round it at low opacity, no motion and
    /// no colour change. Made on the first hover (nothing for what is never pointed at), only shown and hidden after. Every press and click in
    /// the book comes through here: Press (chips, tiles, rows that act) and FeatHover (the feat cards) are HoverMarks, and every button is made
    /// by PanelUi.ClickTarget, which adds one. A part that lights itself on hover (HoverPart, TintOnHover, SwapOnHover's reason) keeps its own
    /// look and gets no line. Mirrored by the :hover rule in preview/panel-preview.html.
    /// </summary>
    class HoverMark : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly Color Ink = PanelWash.HoverLine.Game;   // the mirror's :hover outline as the game draws it (PanelWash.cs)
        Image line;
        public virtual void OnPointerEnter(PointerEventData e) => Mark(true);
        public void OnPointerExit(PointerEventData e) => Mark(false);
        protected void OnDisable() => Mark(false);   // the book closed, or a kept row left the page: no line left lit

        void Mark(bool on)
        {
            if (!on) { if (line) line.enabled = false; return; }
            if (line) { line.enabled = true; return; }
            if (GetComponent<HoverPart>() || GetComponent<TintOnHover>() || GetComponent<SwapOnHover>() || !PanelLook.RoundedTrace) return;
            var r = new GameObject(UiParity.HoverLine, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(transform, false);   // its own name: the ui-reuse parity check leaves it out
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            r.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line = r.gameObject.AddComponent<Image>(); line.sprite = PanelLook.RoundedTrace; line.type = Image.Type.Sliced; line.pixelsPerUnitMultiplier = 1f;
            line.color = Ink; line.raycastTarget = false;
        }
    }
}
