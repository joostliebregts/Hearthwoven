using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8 hover on a bar part (work/hearthwoven-0.8/prototypes/PICKS.md section 4, pick B): pointing at a part of a bar rings it in gold and
    /// lights its row in the list under the bar; pointing at a row lights its part. Nothing is drawn on top of the page and nothing moves:
    /// the ring (one per bar, drawn over its parts), the row's band and the row's words are built with the bar (PanelUi.BarWithList) and only
    /// change colour here; the ring takes the hovered part's anchors. No text is set, nothing is rebuilt and nothing is allocated per hover.
    /// Mirrored by the .hov rules in preview/panel-preview.html.
    /// </summary>
    sealed class BarHover : MonoBehaviour
    {
        /// <summary>The ring's line colour (#f0cd86) with its glow (the focus-ring sprite), a lit row's band (gold at 15 %), a lit row's
        /// name and number (#f6d896).</summary>
        internal static readonly Color Ring = new Color(0.941f, 0.804f, 0.525f, 1f), Band = new Color(0.91f, 0.76f, 0.48f, 0.15f), LitText = new Color(0.965f, 0.847f, 0.588f, 1f);
        /// <summary>How far the ring's line runs outside its part (the focus-ring sprite's line lies 8 px in from its edge).</summary>
        internal const float RingGap = 1f;

        internal RectTransform[] Parts;
        internal Image RingImage;
        internal Image[] Bands;
        internal TextMeshProUGUI[] Names, Numbers, Shares;
        internal Color[] NameColours;
        /// <summary>A damage row's share entry whose part was too narrow for its number (DamageRowsUi.cs): lit, its words swap for the
        /// number ("Frost 37" for "Frost 11 %"); both labels are built with the row, only switched here.</summary>
        internal GameObject[] SwapOn, SwapOff;
        int lit = -1;

        internal void Init(int n)
        {
            Parts = new RectTransform[n]; Bands = new Image[n];
            Names = new TextMeshProUGUI[n]; Numbers = new TextMeshProUGUI[n]; Shares = new TextMeshProUGUI[n]; NameColours = new Color[n];
            SwapOn = new GameObject[n]; SwapOff = new GameObject[n];
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
            if (SwapOn[k]) { SwapOn[k].SetActive(true); if (SwapOff[k]) SwapOff[k].SetActive(false); }
        }

        void Dim(int k)
        {
            lit = -1;
            if (RingImage) RingImage.color = Color.clear;
            if (Bands[k]) Bands[k].color = Color.clear;
            if (Names[k]) Names[k].color = NameColours[k];
            if (Numbers[k]) Numbers[k].color = PanelLook.Text;
            if (Shares[k]) Shares[k].color = PanelLook.Faint;
            if (SwapOn[k]) { SwapOn[k].SetActive(false); if (SwapOff[k]) SwapOff[k].SetActive(true); }
        }
    }

    /// <summary>A bar part or a list row of one bar (BarHover): the pointer entering or leaving it lights or dims part and row together.</summary>
    sealed class HoverPart : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal BarHover Bar; internal int Index;
        public void OnPointerEnter(PointerEventData e) { if (Bar) Bar.Light(Index, true); }
        public void OnPointerExit(PointerEventData e) { if (Bar) Bar.Light(Index, false); }
    }
}
