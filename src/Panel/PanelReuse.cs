using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The book's objects kept between drawings (0.8.1 performance pass, UI-PLAN.md). Joost's in-game bench: a page's UI costs about 58 µs per
    /// object (0.76 ms + 58 µs x objects, 59 pages), and every drawing destroyed and made all of them again, the chapter tabs, the left list
    /// and the player row too, although a page turn rarely changes them (50 to 100 objects of a median page's 226).
    /// 1. The chrome: the tabs, the list and the player row are drawn again only when what they show changed (a key of their labels, icons,
    ///    dots and the rest); a new choice only restyles the tab or row it moved from and to (the kit's lit sprite, the gold label).
    /// 2. Texts: a drawing that clears a part of the book keeps its plain labels (a TextMeshProUGUI with nothing on its object but a layout
    ///    size) in a sleeping holder, and Label takes them again, reset to exactly what a new label is, instead of making a new one: no new
    ///    GameObject, mesh or TMP arrays (a text's arrays grow with its length and were garbage at every drawing), no Awake. A kept text is
    ///    handed out only from the next frame on, when everything that could still hold it from its last drawing (a hover on the old page) is
    ///    destroyed. Plain images (an Image alone on its object: icons, bars, tracks, dots, edges) the same way, through Img and Kit.
    /// The rules that keep a reused object from carrying its last life (REVIEW-081 HIGH on 7a93463, swept for the class):
    /// - one way in: Clear (KeepTexts), never the part it empties (the feat detail area is a Kit image Clear empties and fills again);
    /// - plain only: the graphic alone on its object, no children (a button and its listeners, a hover, a canvas group, an outline: destroyed);
    /// - one way out: SpareText / SpareImage, only from the next frame, only while still bare (no child, no component added while it waited),
    ///   reset to a new one's state (RectTransform, name, active, enabled, every property the book sets);
    /// - nothing long-lived points into a page that is cleared: the feats statics are let go before Clear(content); the damage run's label is
    ///   checked against its run; every other such field points at a row, a grid or a box with children, never kept.
    /// Dev.UiReuse (on) switches all of it: off draws everything new, as 0.8.0 did (for Dev.Bench to compare, or if something looks wrong).
    /// </summary>
    public partial class PanelUi
    {
        internal static ConfigEntry<bool> UiReuse;
        static bool ReuseOn => !reuseSuspended && (UiReuse == null || UiReuse.Value);
        static bool reuseSuspended;   // the parity check's fresh drawing (below): the off path for one Fill

        static void BindReuseConfig(ConfigFile config)
        {
            UiReuse = config.Bind("Dev", "UiReuse", true, "Keep the book's chapter tabs, page list and player row between pages while they stay the same, and reuse its text and picture objects, and bring a new page to the top without updating every canvas of the game, instead of making every object again on every drawing (faster page turns, less garbage). Off: everything is made new, as in 0.8.0 (to compare with Dev.Bench, or if something in the book looks wrong). Applies at the next drawing.");
        }

        // ---------- 1. the chrome ----------

        /// <summary>A chapter tab's parts, for a restyle when only the chosen chapter changed.</summary>
        sealed class TabParts { public Image Ground, Icon; public TextMeshProUGUI Label; public Color Ivory; }
        /// <summary>A list row's parts, for a restyle when only the chosen page changed.</summary>
        sealed class RowParts { public Image Ground; public TextMeshProUGUI Label; }
        readonly List<TabParts> tabParts = new List<TabParts>();
        readonly List<RowParts> rowParts = new List<RowParts>();
        string tabsKey, listKey, playersKey;   // what the kept tabs, list and player row show; null: draw them new

        /// <summary>The kept chrome is forgotten (the frame was built again, or a drawing broke off).</summary>
        void ForgetChrome() { tabsKey = listKey = playersKey = null; tabParts.Clear(); rowParts.Clear(); }

        // the kit sprite's look on an image: Kit's own rules, also for an image that had another sprite before
        static void KitLook(Image img, string sprite)
        {
            img.sprite = PanelLook.Ui(sprite); img.color = img.sprite ? Color.white : PanelLook.Slot;
            if (PanelLook.Sliced(sprite)) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            else { img.type = Image.Type.Simple; img.preserveAspect = true; }
        }

        void TabLook(TabParts t, bool selected)
        {
            if (!t.Ground) return;
            KitLook(t.Ground, selected ? "tab-selected" : "tab");
            if (t.Icon) t.Icon.color = selected ? Amber : t.Ivory;
            if (t.Label) t.Label.color = selected ? PanelLook.Gold : PanelLook.Text;
        }

        static void RowLook(RowParts r, bool selected)
        {
            if (!r.Ground) return;
            KitLook(r.Ground, selected ? "row-selected" : "row");
            if (r.Label) r.Label.color = selected ? PanelLook.Gold : PanelLook.Text;
        }

        // ---------- 2. texts ----------

        static RectTransform spareHolder;   // inactive, under the book's root: a text waiting there sleeps (no canvas, no layout, no mesh)
        static readonly Stack<TextMeshProUGUI> spareTexts = new Stack<TextMeshProUGUI>();
        static readonly List<TextMeshProUGUI> coolingTexts = new List<TextMeshProUGUI>();   // kept this frame: handed out from the next
        static int coolingFrame = -1;
        const int MaxSpareTexts = 400;
        static readonly List<TextMeshProUGUI> foundTexts = new List<TextMeshProUGUI>();
        static readonly Stack<Image> spareImages = new Stack<Image>();
        static readonly List<Image> coolingImages = new List<Image>(), foundImages = new List<Image>();
        const int MaxSpareImages = 400;
        static NewImage newImage;   // what a new Image is, read from the first one made
        static readonly List<Component> foundParts = new List<Component>();
        static NewText newText;   // what a new label is before Label sets it, read from the first one made
        /// <summary>Texts made new and texts reused since the bench last asked (Dev.Bench).</summary>
        internal static int TextsMade, TextsReused, ImagesMade, ImagesReused;

        /// <summary>A new book frame: the holder under it, nothing kept from an earlier one.</summary>
        static void SpareHolder(Transform root)
        {
            spareTexts.Clear(); coolingTexts.Clear(); spareImages.Clear(); coolingImages.Clear();
            spareHolder = Node("Spare", root); spareHolder.gameObject.SetActive(false);
        }

        /// <summary>Before a part of the book is cleared: its plain labels and images go to the holder (Clear destroys the rest).</summary>
        static void KeepTexts(Transform part)
        {
            if (!ReuseOn || !spareHolder) return;
            Cooled();
            if (newText != null)
            {
                part.GetComponentsInChildren(true, foundTexts);
                foreach (var t in foundTexts)
                {
                    if (spareTexts.Count + coolingTexts.Count >= MaxSpareTexts) break;
                    if (t.transform == part || !Plain(t, typeof(TextMeshProUGUI))) continue;   // never the part itself: Clear empties it, it stays
                    t.rectTransform.SetParent(spareHolder, false);
                    coolingTexts.Add(t);
                }
                foundTexts.Clear();
            }
            if (newImage != null)
            {
                part.GetComponentsInChildren(true, foundImages);
                foreach (var i in foundImages)
                {
                    if (spareImages.Count + coolingImages.Count >= MaxSpareImages) break;
                    if (i.transform == part || !Plain(i, typeof(Image))) continue;   // never the part itself (the feat detail area is a Kit image Clear empties)
                    i.rectTransform.SetParent(spareHolder, false);
                    coolingImages.Add(i);
                }
                foundImages.Clear();
            }
        }

        // what was kept in an earlier frame may be handed out now
        static void Cooled()
        {
            if (coolingFrame == Time.frameCount) return;
            foreach (var c in coolingTexts) spareTexts.Push(c);
            foreach (var c in coolingImages) spareImages.Push(c);
            coolingTexts.Clear(); coolingImages.Clear(); coolingFrame = Time.frameCount;
        }

        // a label or image as Label or Img made it: the graphic alone on its object (a LayoutElement from Size may stay: it goes now), no child
        // objects (TMP's fallback-font submeshes, a chip's label); anything else a caller added (a hover, a button, a canvas group, an outline)
        // and it is destroyed with its part
        static bool Plain(Graphic t, System.Type type)
        {
            if (!t || t.GetType() != type || t.transform.childCount > 0) return false;
            t.GetComponents(foundParts);
            LayoutElement le = null; var plain = true;
            foreach (var c in foundParts)
            {
                if (c == t || c is RectTransform || c is CanvasRenderer) continue;
                if (c is LayoutElement l && le == null) { le = l; continue; }
                plain = false; break;
            }
            foundParts.Clear();
            if (!plain) return false;
            if (le) UnityEngine.Object.DestroyImmediate(le);   // now, not at the frame's end: the text's next use may size it again
            return true;
        }

        // still as it was kept: in the holder, no children, nothing on its object but the graphic
        static bool Bare(Graphic g)
        {
            if (!g || g.transform.parent != spareHolder || g.transform.childCount > 0) return false;
            g.GetComponents(foundParts);
            var bare = true;
            foreach (var c in foundParts) if (c != g && !(c is RectTransform) && !(c is CanvasRenderer)) { bare = false; break; }
            foundParts.Clear();
            return bare;
        }

        /// <summary>A kept text from an earlier frame, reset to what a new label is, still asleep in the holder; null: Label makes one.</summary>
        static TextMeshProUGUI SpareText()
        {
            if (!ReuseOn || newText == null || !spareHolder) return null;
            Cooled();
            while (spareTexts.Count > 0)
            {
                var t = spareTexts.Pop();
                if (!Bare(t)) { if (t) UnityEngine.Object.Destroy(t.gameObject); continue; }   // destroyed, moved, or given something while it waited (a stale hover of the old page)
                newText.Reset(t);
                TextsReused++;
                return t;
            }
            return null;
        }

        /// <summary>A kept image from an earlier frame, reset to what a new one is, named, still asleep in the holder; null: make one.</summary>
        static Image SpareImage(string name)
        {
            if (!ReuseOn || newImage == null || !spareHolder) return null;
            Cooled();
            while (spareImages.Count > 0)
            {
                var i = spareImages.Pop();
                if (!Bare(i)) { if (i) UnityEngine.Object.Destroy(i.gameObject); continue; }
                newImage.Reset(i, name);
                ImagesReused++;
                return i;
            }
            return null;
        }

        /// <summary>A plain image made new: what a new one is is read from the first (the reset for kept images).</summary>
        static Image MadeImage(Image i) { if (newImage == null) newImage = new NewImage(i); ImagesMade++; return i; }

        /// <summary>An Image's state as AddComponent makes it: every property Img, Kit or a caller sets (sprite, colour, kind and fill, aspect,
        /// raycasting, material, the RectTransform), read from the first new one.</summary>
        sealed class NewImage
        {
            readonly Vector2 anchorMin, anchorMax, pivot, anchored, size; readonly Color color; readonly Image.Type type; readonly Image.FillMethod fillMethod;
            readonly bool preserveAspect, fillCenter, fillClockwise, useSpriteMesh, raycastTarget, maskable; readonly float fillAmount, ppum, alphaHit; readonly int fillOrigin;
            readonly Vector4 raycastPadding;
            public NewImage(Image i)
            {
                var r = i.rectTransform; anchorMin = r.anchorMin; anchorMax = r.anchorMax; pivot = r.pivot; anchored = r.anchoredPosition; size = r.sizeDelta;
                color = i.color; type = i.type; fillMethod = i.fillMethod; preserveAspect = i.preserveAspect; fillCenter = i.fillCenter; fillClockwise = i.fillClockwise;
                useSpriteMesh = i.useSpriteMesh; raycastTarget = i.raycastTarget; maskable = i.maskable; fillAmount = i.fillAmount; ppum = i.pixelsPerUnitMultiplier;
                alphaHit = i.alphaHitTestMinimumThreshold; fillOrigin = i.fillOrigin; raycastPadding = i.raycastPadding;
            }
            public void Reset(Image i, string name)
            {
                var go = i.gameObject;
                if (go.name != name) go.name = name;
                if (!go.activeSelf) go.SetActive(true);
                i.enabled = true;
                var r = i.rectTransform;
                r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot; r.anchoredPosition3D = new Vector3(anchored.x, anchored.y, 0f); r.sizeDelta = size;
                r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
                i.sprite = null; i.overrideSprite = null; i.material = null; i.color = color; i.type = type; i.fillMethod = fillMethod; i.preserveAspect = preserveAspect;
                i.fillCenter = fillCenter; i.fillClockwise = fillClockwise; i.useSpriteMesh = useSpriteMesh; i.raycastTarget = raycastTarget; i.maskable = maskable;
                i.fillAmount = fillAmount; i.pixelsPerUnitMultiplier = ppum; i.alphaHitTestMinimumThreshold = alphaHit; i.fillOrigin = fillOrigin; i.raycastPadding = raycastPadding;
            }
        }

        /// <summary>A label's state before Label and its caller set it: every property a caller in the book changes after Label (the RectTransform,
        /// overflow, spacing, visible lines, auto size, margins, the object's name and its being on) as TMP made it, read from the first new one.</summary>
        sealed class NewText
        {
            readonly Vector2 anchorMin, anchorMax, pivot, anchored, size; readonly Vector4 margin;
            readonly TextOverflowModes overflow; readonly float characterSpacing, wordSpacing, lineSpacing, paragraphSpacing, sizeMin, sizeMax;
            readonly int maxLines, maxCharacters; readonly bool autoSize, rightToLeft;
            public NewText(TextMeshProUGUI t)
            {
                var r = t.rectTransform; anchorMin = r.anchorMin; anchorMax = r.anchorMax; pivot = r.pivot; anchored = r.anchoredPosition; size = r.sizeDelta;
                margin = t.margin; overflow = t.overflowMode; characterSpacing = t.characterSpacing; wordSpacing = t.wordSpacing; lineSpacing = t.lineSpacing;
                paragraphSpacing = t.paragraphSpacing; sizeMin = t.fontSizeMin; sizeMax = t.fontSizeMax; maxLines = t.maxVisibleLines; maxCharacters = t.maxVisibleCharacters;
                autoSize = t.enableAutoSizing; rightToLeft = t.isRightToLeftText;
            }
            public void Reset(TextMeshProUGUI t)
            {
                var go = t.gameObject;
                if (go.name != "Text") go.name = "Text";
                if (!go.activeSelf) go.SetActive(true);   // in the sleeping holder: it wakes only when Label gives it its place
                t.enabled = true;
                var r = t.rectTransform;
                r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot; r.anchoredPosition3D = new Vector3(anchored.x, anchored.y, 0f); r.sizeDelta = size;
                r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
                t.margin = margin; t.overflowMode = overflow; t.characterSpacing = characterSpacing; t.wordSpacing = wordSpacing; t.lineSpacing = lineSpacing;
                t.paragraphSpacing = paragraphSpacing; t.enableAutoSizing = autoSize; t.fontSizeMin = sizeMin; t.fontSizeMax = sizeMax;
                t.maxVisibleLines = maxLines; t.maxVisibleCharacters = maxCharacters; t.isRightToLeftText = rightToLeft;
            }
        }

        // ---------- 3. the parity check (Dev.SelfCheck) ----------

        const int ParityTurns = 5;
        int parityLeft = ParityTurns, parityTexts, parityImages, parityFills, fills, parityFailed;
        readonly List<string> parityPages = new List<string>();
        bool parityRunning, parityAsked, chromeKept, lastFillNew;

        /// <summary>Before a Render: whether this one is to be checked (Dev.SelfCheck on, Dev.UiReuse on, a forced drawing outside the page walks
        /// and the bench, checks left); notes what was reused so far.</summary>
        bool ParityBefore(bool force)
        {
            parityAsked = force && DevCheck.On && parityLeft > 0 && !parityRunning && !snapping && benchRender == null && ReuseOn;   // SelfCheck off: one read
            if (parityAsked) { parityTexts = TextsReused; parityImages = ImagesReused; parityFills = fills; }
            return parityAsked;
        }

        /// <summary>
        /// The in-game proof the offline harness cannot give (Dev.SelfCheck): on the first ParityTurns drawings of a session that kept or reused
        /// anything, the page as drawn (kept tabs, list or player row; reused texts and images) is laid out and written down object by object
        /// (path and place among its siblings, child count, active, the RectTransform, the components, a text's or image's properties), then the
        /// same view is drawn fresh, Dev.UiReuse's off path, laid out and written down the same way, and the two compared:
        /// "HW-CHECK PASS ui-reuse" or FAIL with the first difference. A kept object that carried something of its last life (a child, a component,
        /// a property) shows as a difference. The greyed Everyone chip's reason tag is left out (it follows the pointer, kept or not).
        /// </summary>
        void ReuseParity()
        {
            if (!parityAsked || fills == parityFills || !root || !root.activeInHierarchy || view == null) return;
            var texts = TextsReused - parityTexts; var images = ImagesReused - parityImages;
            if (!chromeKept && texts == 0 && images == 0) return;   // nothing reused: nothing to prove
            parityLeft--; parityRunning = true;
            var page = (view.ShowAbout ? "About" : view.Active.ToString()) + "/" + (view.Page ?? "");
            try
            {
                KeyLineNow(); Canvas.ForceUpdateCanvases();
                var kept = UiLines(frame);
                var scroll = content.anchoredPosition; var follow = featFollow; var wasNew = lastFillNew;
                reuseSuspended = true;
                try { if (wasNew) lastPage = null; followChoice = follow; Fill(view); }
                finally { reuseSuspended = false; }
                content.anchoredPosition = scroll;   // where the page stood (a foe the keys moved to was scrolled into view after the first drawing)
                KeyLineNow(); Canvas.ForceUpdateCanvases();
                var fresh = UiLines(frame);
                var diff = UiParity.Compare(kept, fresh);
                var what = page + ": " + kept.Count + " objects" + (chromeKept ? ", the chrome kept" : "") + ", " + texts + " texts and " + images + " images reused";
                DevCheck.Book.Say(diff == null ? "PASS" : "FAIL", "ui-reuse", diff == null ? what + ", the same as drawn fresh" : what + "; drawn fresh it differs: " + diff);
                if (diff != null) parityFailed++;
            }
            catch (Exception e) { shown = null; parityFailed++; DevCheck.Book.Say("FAIL", "ui-reuse", page + ": the check could not run: " + e.Message); }   // shown = null: a half-drawn page is drawn again at the next refresh (REVIEW-081 part 2, batch 4)
            finally { reuseSuspended = false; parityRunning = false; parityAsked = false; ForgetChrome(); }   // the fresh drawing's chrome has no keys: drawn again with them next time
            parityPages.Add(page);
            if (parityLeft == 0)   // the last check of the session: one line for all of them
                DevCheck.Book.Say(parityFailed == 0 ? "PASS" : "FAIL", "ui-reuse", "(" + parityPages.Count + " pages" + (parityFailed > 0 ? ", " + parityFailed + " differ" : "") + ": " +
                                  string.Join(", ", parityPages) + ") " + (parityFailed == 0 ? "reused drawings the same as fresh ones, object by object" : "see the lines above for the first difference of each"));
        }

        static readonly List<RectTransform> uiObjects = new List<RectTransform>();
        static readonly List<Component> uiParts = new List<Component>();

        // every active object under the frame, in order, as one line each: what the parity check compares
        static List<string> UiLines(RectTransform top)
        {
            var lines = new List<string>();
            top.GetComponentsInChildren(false, uiObjects);
            var paths = new Dictionary<Transform, string>();
            var b = new StringBuilder();
            string F(float f) => f.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            string V(Vector2 v) => F(v.x) + "," + F(v.y);
            foreach (var r in uiObjects)
            {
                var parentPath = r.parent != null && paths.TryGetValue(r.parent, out var pp) ? pp : "";
                var path = parentPath + "/" + r.name + "#" + r.GetSiblingIndex();
                paths[r] = path;
                if (path.Contains("/Why#")) continue;   // the greyed Everyone chip's reason tag: shown by the pointer
                b.Clear().Append(path).Append(" children=").Append(r.childCount).Append(" active=").Append(r.gameObject.activeSelf ? 1 : 0)
                 .Append(" anchors=").Append(V(r.anchorMin)).Append('/').Append(V(r.anchorMax)).Append(" pivot=").Append(V(r.pivot))
                 .Append(" pos=").Append(V(r.anchoredPosition)).Append(" size=").Append(V(r.sizeDelta)).Append(" scale=").Append(F(r.localScale.x)).Append(" parts=");
                r.GetComponents(uiParts);
                foreach (var c in uiParts)
                {
                    b.Append(c.GetType().Name).Append(';');
                    if (c is TMP_Text t)
                        b.Append(" text=\"").Append(t.text).Append("\" font=").Append(t.font ? t.font.name : "").Append(" size=").Append(F(t.fontSize)).Append(" colour=").Append(ColorUtility.ToHtmlStringRGBA(t.color))
                         .Append(" align=").Append(t.alignment).Append(" style=").Append(t.fontStyle).Append(" wrap=").Append(t.textWrappingMode).Append(" overflow=").Append(t.overflowMode)
                         .Append(" rich=").Append(t.richText ? 1 : 0).Append(" spacing=").Append(F(t.characterSpacing)).Append('/').Append(F(t.lineSpacing)).Append(" lines=").Append(t.maxVisibleLines)
                         .Append(" auto=").Append(t.enableAutoSizing ? 1 : 0).Append(" ray=").Append(t.raycastTarget ? 1 : 0).Append(" on=").Append(t.enabled ? 1 : 0).Append(';');
                    else if (c is Image i)
                        b.Append(" sprite=").Append(i.sprite ? i.sprite.name : "").Append(" colour=").Append(ColorUtility.ToHtmlStringRGBA(i.color)).Append(" type=").Append(i.type)
                         .Append(" aspect=").Append(i.preserveAspect ? 1 : 0).Append(" fill=").Append(F(i.fillAmount)).Append(i.fillMethod).Append(" ppu=").Append(F(i.pixelsPerUnitMultiplier))
                         .Append(" ray=").Append(i.raycastTarget ? 1 : 0).Append(" on=").Append(i.enabled ? 1 : 0).Append(';');
                    else if (c is LayoutElement le)
                        b.Append(" min=").Append(F(le.minWidth)).Append(',').Append(F(le.minHeight)).Append(" pref=").Append(F(le.preferredWidth)).Append(',').Append(F(le.preferredHeight))
                         .Append(" flex=").Append(F(le.flexibleWidth)).Append(',').Append(F(le.flexibleHeight)).Append(" ignore=").Append(le.ignoreLayout ? 1 : 0).Append(';');
                }
                uiParts.Clear();
                lines.Add(b.ToString());
            }
            uiObjects.Clear();
            return lines;
        }

        // the key line's keys as the page gives them: the same keys keep the list they have, and with it the line worked out for them
        static bool SameKeys(List<string> kept, List<string> keys)
        {
            if (kept == null || keys == null || kept.Count != keys.Count) return false;
            for (int i = 0; i < kept.Count; i++) if (!string.Equals(kept[i], keys[i], System.StringComparison.Ordinal)) return false;
            return true;
        }
    }

    /// <summary>
    /// What the kept chrome shows (PanelReuse.cs, 0.8.1), as keys: the same key, the same tabs, list or player row; the chosen tab and row are
    /// left out of theirs (a restyle). Pure, so the panel tests hold a page turn inside a chapter to keeping all three.
    /// </summary>
    public static class ChromeKeys
    {
        const char Sep = '\u0001', End = '\u0002';

        /// <summary>The chapter tabs: ids, labels, icons and dots.</summary>
        public static string Tabs(PanelView v)
        {
            var b = new StringBuilder();
            foreach (var c in v.Chapters) b.Append(c.Id).Append(Sep).Append(c.Label).Append(Sep).Append(c.Icon).Append(Sep).Append(c.Dot ? '1' : '0').Append(End);
            return b.ToString();
        }

        /// <summary>The left list and what its rows do (the chapter or About they lead in), at its row height and gap.</summary>
        public static string List(PanelView v, float rowH, float gap)
        {
            var b = new StringBuilder().Append(v.Active).Append(Sep).Append(v.ShowAbout ? '1' : '0').Append(Sep).Append(rowH).Append(Sep).Append(gap).Append(Sep);
            foreach (var c in v.List) b.Append(c.Id).Append(Sep).Append(c.Label).Append(Sep).Append(c.Icon).Append(Sep).Append(c.Dot ? '1' : '0').Append(c.Disabled ? '1' : '0').Append(End);
            return b.ToString();
        }

        /// <summary>Everything the player row draws and its chips do: who is shown and chosen, their colours, the page of chips
        /// (<paramref name="playerPage"/> of <paramref name="perPage"/>), the Everyone chip's state.</summary>
        public static string Players(PanelView v, int playerPage, int perPage)
        {
            var b = new StringBuilder().Append(v.ShareNote).Append(Sep).Append(v.Players.Count).Append(Sep).Append(playerPage).Append(Sep).Append(perPage).Append(Sep)
                .Append(v.Players.Count - 1 > perPage ? '1' : '0').Append(v.EveryoneChip ? '1' : '0').Append(v.EveryoneOn ? '1' : '0').Append(Sep)
                .Append(v.EveryoneWhy).Append(Sep).Append(v.EveryoneSub).Append(Sep).Append(v.EveryoneTo).Append(Sep);
            int Colour(string name) => name != null && v.PersonColors != null && v.PersonColors.TryGetValue(name, out var i) ? i : -1;
            void Chip(Choice c)
            {
                b.Append(c.Id).Append(Sep).Append(c.Label).Append(Sep).Append(c.Icon).Append(Sep).Append(c.Selected ? '1' : '0').Append(c.Disabled ? '1' : '0').Append(Sep);
                b.Append(Colour(c.Icon != null && c.Icon.StartsWith("person:") ? c.Icon.Substring(7) : null)).Append(Sep)   // the shield (Marker)
                 .Append(Colour(KeyName(c))).Append(Sep).Append(Colour(c.Id ?? c.Label)).Append(End);                       // the colour key (NameKey), the glow (Glow)
            }
            if (v.Players.Count > 0) Chip(v.Players[0]);
            for (int i = 1 + playerPage * perPage; i < v.Players.Count && i < 1 + (playerPage + 1) * perPage; i++) Chip(v.Players[i]);
            return b.ToString();
        }

        /// <summary>The name a chip stands for in the group's colour key: the person of its shield, else its label.</summary>
        public static string KeyName(Choice c) => c.Icon != null && c.Icon.StartsWith("person:") ? c.Icon.Substring(7) : c.Label;
    }

    /// <summary>The parity check's comparison (Dev.SelfCheck "ui-reuse", PanelReuse.cs): two drawings written down one line per object; null when
    /// they are the same, else the first difference: the object's path with what differs in each, or the object one of them has and the other not.</summary>
    public static class UiParity
    {
        public static string Compare(List<string> kept, List<string> fresh)
        {
            int n = Math.Min(kept.Count, fresh.Count);
            for (int i = 0; i < n; i++)
            {
                if (kept[i] == fresh[i]) continue;
                string Path(string l) { var at = l.IndexOf(' '); return at < 0 ? l : l.Substring(0, at); }
                var pk = Path(kept[i]); var pf = Path(fresh[i]);
                if (pk != pf) return "object " + (i + 1) + ": reused " + pk + ", fresh " + pf;
                var a = kept[i].Split(' '); var b = fresh[i].Split(' '); var differs = new List<string>();
                for (int k = 1; k < Math.Max(a.Length, b.Length) && differs.Count < 3; k++)
                {
                    var x = k < a.Length ? a[k] : ""; var y = k < b.Length ? b[k] : "";
                    if (x != y) differs.Add(x + " (fresh " + y + ")");
                }
                return pk + ": " + string.Join(", ", differs);
            }
            if (kept.Count != fresh.Count) return kept.Count + " objects reused, " + fresh.Count + " fresh; the first extra: " + (kept.Count > fresh.Count ? "reused " + kept[n] : "fresh " + fresh[n]).Split(' ')[0];
            return null;
        }
    }
}
