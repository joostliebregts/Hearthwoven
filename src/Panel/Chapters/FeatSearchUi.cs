using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9.2 feat search in the panel (Chapters/FeatSearch.cs holds the rules): the search field on Feats > Earned, Unsung and Together, its key, and the
    /// page tag on each card of the search's grid.
    ///   The field: one TMP input field made once, on the book's frame, laid each frame over the slot its row keeps at the plate's top right
    ///             (FeatSearchRow): the page is drawn again on every letter, and a field inside the page would be destroyed with it and lose the keys.
    ///   Its keys: Panel.SearchKey (/ by default) or a click puts the cursor in it; Enter keeps the search and leaves the field, Esc clears it and leaves
    ///             (the book stays open on that press). While it has the keys nothing else acts: the panel's keys wait (PanelUi.Typing, FeatSearchFrame
    ///             before every other key) and the game's input stays off the way it does for chat (FeatSearchHooks: Chat.HasFocus and
    ///             Minimap.InTextInput say true, which every window of the game asks before it reads a key: the map, the inventory, opening chat).
    /// </summary>
    public partial class PanelUi
    {
        /// <summary>The key that puts the cursor in the Feats search field.</summary>
        internal static ConfigEntry<KeyCode> SearchKey;

        /// <summary>0.9.5: the key that marks every New feat read (Chapters/FeatsReadAll.cs).</summary>
        internal static ConfigEntry<KeyCode> ReadKey;

        static void BindFeatSearchConfig(ConfigFile config)
        {
            ReadKey = config.Bind("Panel", "MarkReadKey", KeyCode.R, "Key that marks every New feat read at once, on Feats > Earned and Together, while the panel is open and one of those pages shows New feats (the 'Mark N read' button in the page's strip does the same): the New marks, the gold dots on the list and on the Feats tab go. It does nothing while the search field has the keys. R: the game's own R (sheathe) is not read while the book is open, and no mod in the group binds it.");
            SearchKey = config.Bind("Panel", "SearchKey", KeyCode.Slash, "Key that puts the cursor in the search field on Feats > Earned, Unsung and Together while the panel is open (a click on the field does the same). Typing filters the feats by name and description across the three pages; Enter keeps the search and leaves the field, Esc clears it and leaves the field. While the field has the keys no other key acts, in the book or in the game. Slash: Valheim binds nothing to it, nor do the group's other mods.");
        }

        static TMP_InputField searchField;
        static Image searchCap;
        static RectTransform searchSlot;     // the room the field's row keeps at the plate's top right (FeatSearchRow); null or destroyed: no field
        static bool searchWanted, searchSeenOpen;
        static int searchOpenedFrame = int.MinValue / 2;
        static readonly float SearchW = PanelLayout.FeatSearch.W, SearchH = PanelLayout.FeatSearch.H, SearchPad = PanelLayout.FeatSearch.Pad, SearchCap = PanelLayout.FeatSearch.Cap;
        /// <summary>The frames after the search key opened the field in which its letter is not typed (the press that opened it).</summary>
        const int SearchOpenerFrames = 2;
        const float SearchCaret = 2;
        /// <summary>0.9.5: between Mark read's chip and the search field (as the strip's chips are apart).</summary>
        static readonly float MarkReadGap = PanelLayout.FeatSearch.Pad;
        static readonly Color SearchTagGround = new Color(0f, 0f, 0f, 0.5f);
        static readonly Vector3[] slotCorners = new Vector3[4], viewCorners = new Vector3[4];
        const int SlotBottomLeft = 0, SlotTopRight = 2;   // GetWorldCorners' order

        // ---------- the row on the page (DrawVocab: Kind "featsearch") ----------

        /// <summary>The field's place: at the right end of the page's strip, where the chips were, when the strip's line leaves it the room (the line ends
        /// before it); else (a line too long, or a page with no line in the strip) a row of its own at the plate's top with the slot at its right end. The
        /// field is laid over the slot (FeatSearchLate).</summary>
        static void FeatSearchRow(RectTransform col, Block b)
        {
            var strip = stripDrawn;
            var line = Instance != null && Instance.view != null ? PanelModel.FeatsIntro(Instance.view) : null;
            var caption = strip && !string.IsNullOrEmpty(line) ? strip.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.text == line) : null;
            // 0.9.5 Mark read (Chapters/FeatsReadAll.cs): a chip left of the field, MarkReadGap apart, measured as drawn; "Mark 34 read" when the line
            // leaves it the room, else "Mark read" (as About these numbers turns "Numbers"), else the field's own row carries both
            var read = b != null && b.Link == PanelModel.FeatsReadTarget && !string.IsNullOrEmpty(b.Value) && Instance != null ? Instance.LinkTo(b.Link) : null;
            if (caption)
                foreach (var words in read != null ? new[] { b.Value, PanelModel.FeatsReadWord } : new string[] { null })
                {
                    RectTransform chip = null; float readW = 0;
                    if (words != null) { readW = Chip(strip, words, null, false, read); chip = (RectTransform)strip.GetChild(strip.childCount - 1); chip.name = "MarkRead"; }
                    var room = caption.rectTransform.sizeDelta.x - SearchW - (chip ? readW + MarkReadGap : 0f) - PanelModel.StripAboutGap;
                    if (caption.preferredWidth <= room)
                    {
                        var slot = Node("SearchSlot", strip);
                        slot.anchorMin = slot.anchorMax = slot.pivot = Vector2.right + Vector2.up / 2;
                        slot.anchoredPosition = new Vector2(-EarnedPad, 0); slot.sizeDelta = new Vector2(SearchW, SearchH);
                        if (chip)
                        {
                            chip.anchorMin = chip.anchorMax = chip.pivot = Vector2.right + Vector2.up / 2;
                            chip.anchoredPosition = new Vector2(-(EarnedPad + SearchW + MarkReadGap), 0); chip.sizeDelta = new Vector2(readW, PanelLayout.Chip.Height);
                        }
                        caption.rectTransform.sizeDelta = new Vector2(room, caption.rectTransform.sizeDelta.y);   // the line ends before the action and the field
                        searchSlot = slot;
                        return;
                    }
                    if (chip) { chip.gameObject.SetActive(false); Destroy(chip.gameObject); }
                }
            var row = Line(col, 0, TextAnchor.MiddleRight); Size(row, -1, SearchH);
            if (read != null) { Chip(row, b.Value, null, false, read); Size(Node("Gap", row), MarkReadGap, 1); }
            var own = Node("SearchSlot", row); Size(own, SearchW, SearchH);
            searchSlot = own;
        }

        // ---------- the field ----------

        /// <summary>The key's word on its cap ("/"), and the letter it types (the press that opens the field does not type it).</summary>
        static string SearchKeyWord(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Slash: return "/"; case KeyCode.Backslash: return "\\"; case KeyCode.Period: return "."; case KeyCode.Comma: return ",";
                case KeyCode.Semicolon: return ";"; case KeyCode.Quote: return "'"; case KeyCode.BackQuote: return "`"; case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "="; case KeyCode.LeftBracket: return "["; case KeyCode.RightBracket: return "]"; case KeyCode.KeypadDivide: return "/";
            }
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((char)('0' + (k - KeyCode.Alpha0))).ToString();
            return GameNames.Of(k);
        }

        static TextMeshProUGUI SearchText(RectTransform parent, string name, Color color, FontStyles style)
        {
            // a text of its own, not one of the page's kept labels (PanelReuse): it lives as long as the field
            var go = Node(name, parent).gameObject; go.SetActive(false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (PanelLook.Body) t.font = PanelLook.Body;
            go.SetActive(true);
            t.fontSize = Mathf.Max(PanelLayout.Text.Small, PanelLook.MinText); t.color = color; t.fontStyle = style; t.richText = false; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.MidlineLeft; t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
            t.rectTransform.Stretch();
            return t;
        }

        void MakeSearchField()
        {
            var go = new GameObject("FeatSearch", typeof(RectTransform)); go.SetActive(false);   // its parts set before the field wakes (TMP_InputField needs its text)
            go.transform.SetParent(frame, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = Vector2.one / 2; r.pivot = Vector2.zero;
            var ground = go.AddComponent<Image>(); KitLook(ground, "meter-track"); ground.raycastTarget = true;   // the dark ground and edge of a card
            var key = SearchKey != null && SearchKey.Value != KeyCode.None ? SearchKeyWord(SearchKey.Value) : null;
            var capW = key == null ? 0 : KeycapWidth(key, SearchCap);
            var area = Node("Text Area", go.transform); area.Stretch();
            area.offsetMin = new Vector2(SearchPad, 0); area.offsetMax = new Vector2(-(SearchPad + (capW > 0 ? capW + SearchPad : 0)), 0);
            area.gameObject.AddComponent<RectMask2D>();
            var hint = SearchText(area, "Placeholder", PanelLook.Faint, FontStyles.Italic); hint.text = PanelModel.FeatSearchPlaceholder;
            var text = SearchText(area, "Text", PanelLook.Text, FontStyles.Normal);
            if (key != null)
            {
                searchCap = Keycap(r, key); var cr = searchCap.rectTransform;
                cr.anchorMin = cr.anchorMax = cr.pivot = Vector2.right + Vector2.up / 2; cr.anchoredPosition = new Vector2(-SearchPad, 0); cr.sizeDelta = new Vector2(capW, SearchCap);
            }
            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = area; input.textComponent = text; input.placeholder = hint;
            if (PanelLook.Body) input.fontAsset = PanelLook.Body;
            input.pointSize = text.fontSize;
            input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = PanelModel.FeatSearchMax; input.richText = false;
            input.restoreOriginalTextOnEscape = false; input.onFocusSelectAll = false; input.resetOnDeActivation = false;
            input.customCaretColor = true; input.caretColor = PanelLook.Gold; input.caretWidth = (int)SearchCaret;
            input.selectionColor = PanelWash.HoverBand.Game;   // the gold of a hovered line
            input.navigation = new Navigation { mode = Navigation.Mode.None };   // the arrows move the caret, never the selection to another control
            var opener = key != null && key.Length == 1 ? char.ToLowerInvariant(key[0]) : '\0';
            input.onValidateInput = (all, at, ch) => ch < ' ' || (opener != '\0' && char.ToLowerInvariant(ch) == opener && Time.frameCount - searchOpenedFrame <= SearchOpenerFrames) ? '\0' : ch;   // the press that opened it is not typed
            input.onValueChanged.AddListener(q => { if (Instance) Instance.SearchTyped(q); });
            go.SetActive(true);
            searchField = input;
            FeatSearchInput.FocusedNow = () => searchField && searchField.isFocused;
        }

        void SearchTyped(string q)
        {
            if (PanelModel.FeatQuery(state) == (q ?? "")) return;
            PanelModel.SetFeatQuery(state, q); searchWanted = true;   // the page drawn again at the next frame's keys (FeatSearchFrame), once however fast the letters come
        }

        static void SearchSet(string q) { if (searchField && searchField.text != q) searchField.SetTextWithoutNotify(q); }

        void SearchFocus()
        {
            if (!searchField) return;
            searchOpenedFrame = Time.frameCount;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(searchField.gameObject);
            searchField.ActivateInputField();
            searchField.caretPosition = searchField.text.Length;
        }

        static void SearchLeave()
        {
            if (!searchField) return;
            if (searchField.isFocused) searchField.DeactivateInputField();
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == searchField.gameObject) EventSystem.current.SetSelectedGameObject(null);
        }

        // ---------- the keys (Frame, before every other key of the panel) ----------

        /// <summary>
        /// The search's keys each frame the book is open, before the panel's own: true when this frame's keys were the search's (the panel reads no key then).
        /// While the field has the keys (now or at the end of the last frame) Esc clears and leaves, Enter keeps and leaves, anything else is typing; the
        /// search key opens it. The query ends with each opening of the book and when the page leaves the three pages.
        /// </summary>
        bool FeatSearchFrame()
        {
            var page = state.PageOf(Chapter.Feats) ?? PanelModel.FeatsPageId;
            var off = state.ShowAbout || state.Chapter != Chapter.Feats || !PanelModel.FeatSearchPage(page);
            if ((off || !searchSeenOpen) && PanelModel.FeatQuery(state).Length > 0)   // another chapter or Titles, or a new opening: the search ends
            {
                PanelModel.SetFeatQuery(state, ""); SearchSet(""); SearchLeave();
                if (!searchSeenOpen && !off) { searchSeenOpen = true; Render(true); return true; }
            }
            searchSeenOpen = true;
            var had = FeatSearchInput.FocusedLastFrame; var now = searchField && searchField.isFocused;
            if (had || now)
            {
                var act = FeatSearchInput.KeyAct(ZInput.GetKeyDown(KeyCode.Escape, false), ZInput.GetKeyDown(KeyCode.Return, false) || ZInput.GetKeyDown(KeyCode.KeypadEnter, false));
                if (act == FeatSearchInput.Act.Clear) { searchWanted = false; PanelModel.SetFeatQuery(state, ""); SearchSet(""); SearchLeave(); Render(true); return true; }
                if (act == FeatSearchInput.Act.Keep) { SearchLeave(); if (searchWanted) { searchWanted = false; Render(true); } return true; }
                if (searchWanted) { searchWanted = false; Render(true); }
                if (now) return true;   // typing: nothing else reads a key
                // the field let go by a click elsewhere: the panel's keys work again from here
            }
            if (searchWanted) { searchWanted = false; Render(true); }
            if (!off && SearchKey != null && SearchKey.Value != KeyCode.None && searchField && searchField.gameObject.activeSelf && ZInput.GetKeyDown(SearchKey.Value, false)) { SearchFocus(); return true; }
            return false;
        }

        // ---------- the field over its slot (LateFrame) ----------

        void FeatSearchLate()
        {
            var on = open && root && root.activeSelf && searchSlot && searchSlot.gameObject.activeInHierarchy && view != null && PanelModel.OnFeatsPage(view) && PanelModel.FeatSearchPage(view.Page);
            if (!open) searchSeenOpen = false;
            if (!on)
            {
                if (searchField) { SearchLeave(); searchField.gameObject.SetActive(false); }
                FeatSearchInput.FocusedLastFrame = false;
                return;
            }
            if (!searchField) MakeSearchField();
            if (!searchField) { FeatSearchInput.FocusedLastFrame = false; return; }
            var r = (RectTransform)searchField.transform;
            searchSlot.GetWorldCorners(slotCorners);
            Vector2 a = frame.InverseTransformPoint(slotCorners[SlotBottomLeft]), c = frame.InverseTransformPoint(slotCorners[SlotTopRight]);
            r.anchoredPosition = a - frame.rect.center; r.sizeDelta = c - a;
            // a slot scrolled out of the page's view (the page scrolls under the strip): the field hides with it
            var shown = true;
            if (content && content.parent is RectTransform viewport)
            {
                viewport.GetWorldCorners(viewCorners);
                shown = slotCorners[SlotBottomLeft].y >= viewCorners[SlotBottomLeft].y - 1f && slotCorners[SlotTopRight].y <= viewCorners[SlotTopRight].y + 1f;
            }
            if (searchField.gameObject.activeSelf != shown) searchField.gameObject.SetActive(shown);
            if (!searchField.isFocused) SearchSet(PanelModel.FeatQuery(state));   // cleared elsewhere (a new opening, another page): the field says so
            searchField.transform.SetAsLastSibling();
            FeatSearchInput.FocusedLastFrame = searchField.isFocused;
        }

        /// <summary>The chat's own input has the keys (as Chat.HasFocus answers for the game): read off the chat itself, because FeatSearchHooks makes
        /// Chat.HasFocus say true while the search field has the keys, and what decides whether the book opens never asks a patched method (CanOpen).</summary>
        static bool ChatHasKeys() => Chat.instance && Chat.instance.m_wasFocused;

        // ---------- the page tag on a card of the search's grid (FeatsBlock) ----------

        /// <summary>While a query is on, each card says which page it stands on ("Earned", "Unsung", "Together"): a small tag at its top right, left of
        /// its New mark; the card's text moves below it (centred in the rest of the card), so the name keeps its whole line.</summary>
        static void FeatSearchTags(float w)
        {
            var tags = Instance != null && Instance.view != null ? PanelModel.FeatSearchBlock(Instance.view)?.Items : null;
            if (tags == null || tags.Count == 0) return;
            foreach (var h in featCards)
            {
                if (!h) continue;
                var tag = tags.FirstOrDefault(t => t.Id == h.Id); if (tag == null) continue;
                var box = Node("PageTag", h.transform);
                var back = Img(box, "Back", PanelLook.Rounded, SearchTagGround); back.preserveAspect = false;
                if (PanelLook.Rounded) { back.type = Image.Type.Sliced; back.pixelsPerUnitMultiplier = PanelLook.TierMarkScale; }
                var word = Label(box, tag.Title, PanelLook.MinText, PanelLook.Muted, align: TextAlignmentOptions.Center);
                word.textWrappingMode = TextWrappingModes.NoWrap;
                float tw = Mathf.Ceil(word.preferredWidth) + 2 * PanelLayout.FeatSearch.TagPad, th = PanelLayout.FeatSearch.TagH;
                var mark = h.NewMark ? (RectTransform)h.NewMark.transform : null;
                var right = PanelLayout.Feats.NewInset + (mark ? mark.rect.width + PanelLayout.FeatSearch.TagGap : 0);
                box.Box(w - right - tw, PanelLayout.FeatSearch.TagTop, tw, th);
                back.rectTransform.Stretch(); word.rectTransform.Stretch();
                // the card's text stands under the tag: its stack starts below it (still centred in what is left), so no name runs under the tag
                if (h.transform.Find("Text") is RectTransform stack) { var below = PanelLayout.FeatSearch.TagTop + th; stack.sizeDelta = new Vector2(stack.sizeDelta.x, stack.sizeDelta.y - below); stack.anchoredPosition = new Vector2(stack.anchoredPosition.x, stack.anchoredPosition.y - below); }
            }
        }
    }

    /// <summary>
    /// While the Feats search field has the keys the game reads none of them, as while chat has them: every window of the game asks Chat.HasFocus
    /// before it reads a key (the map's M, the inventory's Tab and I, the camera, the hotbar), and Chat.Update asks Minimap.InTextInput before Enter
    /// opens chat. Both say true then. Only then: with the field idle (or the book shut) the game's answer stands. Picked up by the per-type patch loop
    /// (Plugin.Awake); on an error the game's answer stands.
    /// </summary>
    static class FeatSearchHooks
    {
        [HarmonyPatch(typeof(Chat), nameof(Chat.HasFocus))]
        static class SearchAsChat
        {
            static void Postfix(ref bool __result) { try { if (!__result && FeatSearchInput.Typing) __result = true; } catch (Exception e) { HookGuard.Fail(e, "FeatSearchHooks.SearchAsChat.Postfix"); } }
        }

        [HarmonyPatch(typeof(Minimap), nameof(Minimap.InTextInput))]
        static class SearchAsMapText
        {
            static void Postfix(ref bool __result) { try { if (!__result && FeatSearchInput.Typing) __result = true; } catch (Exception e) { HookGuard.Fail(e, "FeatSearchHooks.SearchAsMapText.Postfix"); } }
        }
    }
}
