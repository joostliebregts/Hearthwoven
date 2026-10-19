using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Feats in the panel (ACHIEVEMENTS-06 section 4): the grid of feat cards, the one fixed detail area under it, the "Known for" line
    /// on Deeds > Overview and the band on an owner page. Only ready sprites: the family's title emblem, the kit's meter track as the
    /// ground, plain rects for the rim and the tier notches. Built once when the page is filled; a hover (or a click, or A/D) only
    /// refills the detail area and moves the selection ring, it never rebuilds the page.
    /// </summary>
    public partial class PanelUi
    {
        // fix4: the card and the gap between rows (PanelLayout.Feats) so four whole rows fit the grid's room (Feats.GridRoom); the band keeps its own 8 gap
        static readonly float FeatH = PanelLayout.Feats.Card, FeatGap = PanelLayout.Feats.ColumnGap, FeatRowGap = PanelLayout.Feats.RowGap;
        // B30: every tier, the counting and Who helped fit (0.8.2: taller when a crew needs the rows); 0.9.1: as high as the page's details need (PanelLayout.FeatDetail)
        const float FeatHMax = 76, FeatDetailLeft = 292;
        static readonly float FeatRuleH = PanelLayout.FeatDetail.RuleH;
        const int FeatLookPer = 3, TitleReasonLines = 2;   // 0.9 feat look: three cards across
        static readonly float FeatEmblemTop = PanelLayout.FeatDetail.EmblemTop;
        const float FeatEmblemLeft = 16, FeatEmblem = 56, FeatNameLeft = 84, FeatNameRoom = 88, FeatRimAlpha = 0.9f;   // the detail's emblem and name (the look's parts use them)
        static readonly Color FeatRim = PanelWash.TierRim.Game;   // the mirror's fallback tier rim as the game draws it (PanelWash.cs)
        static readonly Color FeatUnsungInk = new Color(0.62f, 0.6f, 0.58f, 0.55f);   // the emblem of a feat not earned: grey, faint
        static readonly Color FeatWaitingInk = new Color(0.62f, 0.6f, 0.58f, 0.28f);   // the emblem of a feat Hearthwoven does not count yet: fainter still, and a dashed edge
        static readonly Color FeatWaitingRim = PanelWash.WaitingRim.Game;
        static RectTransform featDetail; static float featDetailWidth;
        /// <summary>The detail area's height on this page: the tallest card's need on the page (FeatDetailNeed: its lines, a group feat's crew, 0.8.2:
        /// everyone shows), at least PanelLayout.FeatDetail.Least, so a hover never moves the grid above it. 0.9.1: no empty band kept (it was 184 always).</summary>
        static float featDetailH = PanelLayout.FeatDetail.Least;
        static float featH = FeatH;   // the cards' height now: 52, taller (up to 76) when a full plate leaves the grid room (fewer rows, more air, no empty band)
        internal static bool plateFull;   // the page's plate keeps the whole room (the Feats chapter, PanelModel.PlateFull): the grid grows into it

        /// <summary>A tier's colour from the model (PanelModel.TierColour, "#rrggbb"); the fallback when none is set.</summary>
        static Color TierInk(string hex, Color fallback) => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>The height the grid may take on a full plate: the room the plate gives, less what is drawn above it (the plate's own line) and the detail area
        /// under it, so the detail area sits at the plate's foot on every Feats page and nothing moves between Earned, Unsung and Together. 0.9.2 (Joost:
        /// "show me the full list before I select anything"): with no card chosen there is no detail area, and the grid takes its room too.</summary>
        static float FeatsRoom(RectTransform col, bool detail)
        {
            if (!plateFull || Instance == null || col != Instance.content || !(col.parent is RectTransform box)) return PanelModel.FeatsGridRoom;
            var vlg = col.GetComponent<VerticalLayoutGroup>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(col);   // the plate's line measured at its real width (a label not laid out yet wraps narrow)
            float above = 0; int n = 0;
            foreach (RectTransform child in col) { if (!child.gameObject.activeSelf) continue; above += LayoutUtility.GetPreferredHeight(child); n++; }
            var gap = vlg ? vlg.spacing : 10f;
            var room = box.rect.height - (vlg ? vlg.padding.top + vlg.padding.bottom : 0) - above - gap * n - (detail ? gap + featDetailH : 0) - 2;
            return Mathf.Max(FeatH, Mathf.Floor(room));
        }   // the detail area drawn last on the page, refilled by a hover
        static readonly List<FeatHover> featCards = new List<FeatHover>();
        static RectTransform featsArea; static Block featsData; static float featsRoom;   // the grid drawn last, its block and its height (FeatsSettle)
        // B31: the chosen feat must be brought into view on this drawing (a new page, or A/D moved the choice: followChoice, set by FeatsKeys), not on
        // a hover's choice or an update, which keep the grid where the player scrolled it (PanelUi.Fill sets featFollow for the page and its settling)
        static bool featFollow, followChoice;

        // ---------- the grid ----------

        static void FeatsBlock(RectTransform col, Block b, float? settled = null)
        {
            featCards.Clear(); featDetail = null; featsArea = null; featsData = b;
            if (DevCheck.On) featNamesIn = FeatNamesFrames;   // the names checked once the page has settled (FeatNamesCheck)
            var cards = b.Items ?? new List<Block>();
            if (cards.Count == 0) return;
            // 0.9.2 (Joost: "Only show when I click to select something"): a page opens with no card chosen and no detail area, the grid over the whole
            // room; a click (or A/D) chooses one and the detail area comes in under the grid (the Titles page always has one chosen)
            var detail = cards.Any(c => c.Selected);
            if (settled == null) featDetailH = detail ? FeatDetailPage(col, cards) : 0f;   // the detail area's height first: the grid takes the room it leaves
            var per = Mathf.Clamp(Mathf.FloorToInt((Column + FeatGap) / (180 + FeatGap)), 1, 4);
            if (plateFull && cards.Count <= 9) per = Mathf.Min(per, 3);   // a full plate with few cards: three across, so the rows fill the room
            if (b.Tone == PanelModel.TitlesPageId) per = Mathf.Min(per, 3);   // the Titles page: three across, so a title's reason fits its card (B18)
            var look = b.Look > 0; if (look) per = Mathf.Min(per, FeatLookPer);   // 0.9 feat look: three across, as the boards (FeatLookUi.cs)
            var titles = b.Tone == PanelModel.TitlesPageId;   // 0.9.1: a title's reason may take two lines (PanelLayout.Feats.TitleCard)
            float least = look ? LkCardMin : titles ? PanelLayout.Feats.TitleCard : FeatH, most = look ? LkCardMax : FeatHMax;
            var w = Mathf.Floor((Column - FeatGap * (per - 1)) / per);
            // the grid sits in an area of its own, at most FeatsGridRoom high: more rows scroll inside it (wheel, soft fade at the edge that has more),
            // so the detail area under it never moves and is never cut at the plate's fold
            var rows = (cards.Count + per - 1) / per;
            var fill = settled ?? FeatsRoom(col, detail);
            featH = plateFull ? Mathf.Clamp(Mathf.Floor((fill - (rows - 1) * FeatRowGap) / rows), least, most) : least;
            var natural = rows * featH + (rows - 1) * FeatRowGap;
            var room = plateFull ? fill : Mathf.Min(natural, fill);   // a full plate: the area keeps its whole height, the detail area under it stays at the foot
            var area = Node("FeatsArea", col); Size(area, -1, room);
            featsArea = area; featsRoom = room;
            var scrolls = natural > room + 0.5f && Instance != null;
            // a grid that scrolls: the cards in a view over the area's whole height; the More below cue lies over its foot (FeatMoreCue), it takes no row
            var view = area;
            if (scrolls) { view = Node("FeatsView", area); view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one; view.offsetMin = Vector2.zero; view.offsetMax = Vector2.zero; }
            var viewH = room;
            view.gameObject.AddComponent<RectMask2D>();
            var grid = Node("Feats", view);
            grid.anchorMin = new Vector2(0, 1); grid.anchorMax = new Vector2(1, 1); grid.pivot = new Vector2(0.5f, 1);
            grid.offsetMin = Vector2.zero; grid.offsetMax = Vector2.zero; grid.sizeDelta = new Vector2(0, natural);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(w, featH); g.spacing = new Vector2(FeatGap, FeatRowGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = per;
            foreach (var c in cards) { if (look) FeatLookCard(grid, c, w); else FeatCard(grid, c, w, titles); }
            FeatSearchTags(w);   // 0.9.2 feat search: each card's page on the search's grid (FeatSearchUi.cs)
            if (scrolls)
            {
                var scroll = view.gameObject.AddComponent<ScrollRect>();
                scroll.content = grid; scroll.viewport = view; scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 0f; scroll.inertia = false;
                var fadeTone = new Color(PlateColour.r, PlateColour.g, PlateColour.b, 0.9f);
                var s = new Scroll { Rect = scroll, Top = Fade(view, top: true), Bottom = Fade(view, top: false) };
                s.Top.color = fadeTone;
                s.Bottom.color = Color.clear;   // the cue's own band is the bottom edge's fade (FeatMoreCue); this one only tells Fades that more lies below
                // the cue (fix4): rows lie below, and how to reach them; 0.9.2 (Joost: "can we not make that a full horizontal bar that overlays the
                // items? ... This is wasteful"): a band over the grid's foot, shown only while more lies below (PanelUi.Fades)
                s.Cue = FeatMoreCue(area).gameObject;
                // B31: the grid stays where the player left it when the same page is drawn again (a hover's choice, an update); the chosen feat is
                // brought into view, only as far as its row needs, on a new page or when A/D moved it (PanelModel.GridScroll)
                s.Key = Instance.ScrollPath(view); s.Placed = true;
                Instance.scrollers.Add(s);
                // 0.9.2: no card chosen (the page opened, or the chosen one closed): the grid stays where it was, nothing is followed
                var chosen = cards.FindIndex(c => c.Selected);
                var at = Mathf.Max(0, chosen) / per;
                var rowTop = at * (featH + FeatRowGap);
                // the chosen card's row clear of the cue's band too (it lies over the grid's foot while more lies below)
                var rowEnd = rowTop + featH + (rowTop + featH < natural - 0.5f ? PanelLayout.Feats.CueBand : 0f);
                grid.anchoredPosition = new Vector2(0, PanelModel.GridScroll(Instance.KeptScroll(s.Key), featFollow && chosen >= 0, rowTop, rowEnd, viewH, natural - viewH));
            }
        }

        /// <summary>
        /// 0.9.2 the More below cue (Joost in game: "can we not make that a full horizontal bar that overlays the items? Make it more transparent and fade
        /// with a gradient from bottom to transparent at the top"): a band across the grid's whole width over the foot of the cards, the card's own ground
        /// from CueGround at the bottom to nothing at its top (FeatLookShape.CueStops, drawn as the game must to show the mirror's: PanelLook.CueFade), the
        /// words small and centred near its bottom edge on the cards' text backing (the same dark underlay, FeatLookShade). It catches no click: the
        /// cards under it stay clickable. The grid keeps its whole height (the cue had a 26 px strip of its own under the cards).
        /// </summary>
        static RectTransform FeatMoreCue(RectTransform area)
        {
            var cue = Node("MoreCue", area);
            cue.anchorMin = Vector2.zero; cue.anchorMax = new Vector2(1, 0); cue.pivot = new Vector2(0.5f, 0);
            cue.anchoredPosition = Vector2.zero; cue.sizeDelta = new Vector2(0, PanelLayout.Feats.CueBand);
            var band = Img(cue, "Band", PanelLook.CueFade, PanelLook.CueFade ? LkCardGround : new Color(LkCardGround.r, LkCardGround.g, LkCardGround.b, FeatLookShape.Veil(FeatLookShape.CueGround)));
            band.raycastTarget = false; band.Stretched();
            var words = Label(cue, PanelModel.FeatsMore, PanelLook.MinText, PanelLook.Gold, align: TextAlignmentOptions.Center);
            words.textWrappingMode = TextWrappingModes.NoWrap; words.raycastTarget = false;
            FeatLookShade(words, always: true);
            var r = words.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0);
            r.anchoredPosition = new Vector2(0, PanelLayout.Feats.CueTextBottom); r.sizeDelta = new Vector2(0, PanelLayout.LineBox(PanelLook.MinText));
            return cue;
        }

        /// <summary>
        /// titles-grid (Joost in game, 0.6.5: Titles showed four rows with empty room under the detail area): the grid's height measured again once the
        /// page has settled (PanelUi.CutPlate, the frame it is drawn and the next ones, as the other plates are). What the first measure missed goes to
        /// the grid, so the detail area sits at the plate's foot; the grid is drawn again at that height, at its place, the detail area kept.
        /// </summary>
        void FeatsSettle(RectTransform box)
        {
            if (!featsArea || featsData == null || featsArea.parent != content) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var room = Mathf.Max(FeatH, Mathf.Floor(featsRoom + box.rect.height - LayoutUtility.GetPreferredHeight(content) - 2));
            if (Mathf.Abs(room - featsRoom) < 1f) return;
            var old = featsArea; var at = old.GetSiblingIndex(); var detail = featDetail;
            KeepScroll(old);   // B31: drawn again at its new height where it stood (a wheel turned since the page was drawn counts too)
            scrollers.RemoveAll(s => !s.Rect || s.Rect.transform.IsChildOf(old));
            old.SetParent(null, false); Destroy(old.gameObject);
            FeatsBlock(content, featsData, room);
            if (featsArea) featsArea.SetSiblingIndex(at);
            featDetail = detail;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        static void FeatCard(RectTransform grid, Block c, float w, bool reasonWraps = false)
        {
            var earned = c.Tone == PanelModel.FeatTone; var waiting = c.Tone == PanelModel.FeatWaitingTone;
            var card = Kit(grid, c.Title, "meter-track", raycast: true);   // its dark ground and edge
            var liftAt = card.transform.childCount;   // 0.9.3: the chosen card's warm lift lies here, under the tier's rim and everything on the card (FeatChosen)
            var tier = TierInk(c.Colour, FeatRim);
            if (earned)   // the tier's colour (bronze, silver, gold): its rim and a bar down its left edge, with the numeral and the notches
            {
                Edge(card.rectTransform, new Color(tier.r, tier.g, tier.b, 0.9f));
                Img(card.transform, "Tier", null, tier).rectTransform.Box(0, 0, 3, featH);
            }
            if (waiting) DashEdge(card.rectTransform, w, featH, FeatWaitingRim);   // a dashed rim: not earnable yet, Hearthwoven does not count it
            var notched = c.Count > 1;   // a mark for every tier (B30): reached ones filled, the rest outlines, also before the first tier, so the count shows
            // the emblem and its marks as one block in the card's middle; on the 52 px card the emblem steps down to 33 so the 10 px marks fit under it
            var size = notched ? Mathf.Min(36f, featH - 6 - TierMark - TierMarkGap) : 36f;
            var top = notched ? Mathf.Floor((featH - size - TierMarkGap - TierMark) / 2) : featH <= FeatH ? 5 : Mathf.Floor((featH - 36) / 2);
            var emblem = Marker(card.rectTransform, c.Icon, size, layout: false); emblem.Box(9 + Mathf.Floor((36 - size) / 2), top, size, size);
            if (!earned) foreach (var im in emblem.GetComponentsInChildren<Image>()) im.color = waiting ? FeatWaitingInk : FeatUnsungInk;
            var (newMark, newRoom) = FeatNewMark(card.transform, c, w);
            // the name and its small line stacked in the middle of the card at one size: a long name wraps to a second line (never shrinks)
            var stack = Node("Text", card.transform); stack.Box(FeatTextLeft, 2, w - FeatTextLeft - 8, featH - 4);
            var v = stack.gameObject.AddComponent<VerticalLayoutGroup>(); v.childAlignment = TextAnchor.MiddleLeft; v.spacing = 0;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            // the tier reached is said in words too (fix4: the notches alone were small): "Drover II" on a feat with tiers
            // richtext-fix: the numeral styled here from the model's parts, on a label with rich text on, so TMP's ellipsis cuts letters, never a tag
            var tierWord = earned && c.Count > 1 && c.Level > 0 ? " " + Rich.Plain(PanelModel.Numeral((int)c.Level)).Ink(Hex(tier)).Bold() : Rich.Empty;
            var name = RichLabel(stack, "feats", Rich.Plain(c.Title) + tierWord, PanelLayout.Text.Small, earned ? PanelLook.Text : PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
            var noted = !string.IsNullOrEmpty(c.Note);
            name.overflowMode = TextOverflowModes.Ellipsis; name.maxVisibleLines = noted && featH < 60 ? 1 : 2;
            if (newRoom > 0) name.margin = new Vector4(0, 0, newRoom, 0);   // 0.9: the name keeps clear of the New mark beside it; the lines under it keep the card's width
            if (!string.IsNullOrEmpty(c.Text))
            {
                var when = Label(stack, c.Text, PanelLook.MinText, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic);
                when.textWrappingMode = TextWrappingModes.NoWrap; when.overflowMode = TextOverflowModes.Ellipsis;
                if (reasonWraps && !noted) { when.textWrappingMode = TextWrappingModes.Normal; when.maxVisibleLines = TitleReasonLines; name.maxVisibleLines = 1; }   // 0.9.1: a title's reason on two lines, never cut
            }
            if (noted)   // feats-real: why an Unsung feat reads what it reads ("counting since 8 Oct"), or on an earned one the next tier only ("next: II at 5 000")
            {
                var why = Label(stack, c.Note, PanelLook.MinText, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft);
                why.textWrappingMode = TextWrappingModes.NoWrap; why.overflowMode = TextOverflowModes.Ellipsis;
                if (!string.IsNullOrEmpty(c.Value) && why.preferredWidth > w - FeatTextLeft - 8) why.text = c.Value;   // B30: the next tier's words too long for the card: number and unit only
            }
            if (notched) FeatNotches(card.rectTransform, (int)c.Level, c.Count, 8, top + size + TierMarkGap, waiting);
            var hover = card.gameObject.AddComponent<FeatHover>();
            hover.Id = c.Id; hover.Detail = (c.Items ?? new List<Block>()).FirstOrDefault(); hover.NewMark = newMark; hover.Name = name; hover.NameInk = name.color; hover.LiftAt = liftAt;
            FeatChosen(hover, c.Selected);
            featCards.Add(hover);
        }

        // ---------- 0.9.3 the chosen card (Joost in game: "'currently selected' is way too subtle. or not even active. Let's make selected way more clear.") ----------
        // The card shown in the detail area had only the keys' focus ring: a soft glow drawn only after a key press and hidden again by any mouse move or
        // click (PanelUi.KeyFocus), so a card chosen with the mouse showed nothing at all. Now the chosen card, by a click or by A/D alike, carries the
        // chosen look: a solid gold frame (PanelLook.Gold, the 2 px rounded rim of a chosen bar part, BarFormUi), drawn over everything on the card
        // (the family's pattern, the band under the text); its ground lifted by a warm gold wash (PanelWash.FeatChosen) laid over the pattern and the band,
        // under the icon and the text; its name in gold. A hover keeps its own 1 px line at 45 % (HoverMark), so pointing at a card never reads as choosing
        // it. Made only for the chosen card (a page has one at most); a choice moved without a rebuild (FeatSelect) makes or hides them. Mirrored by
        // .fcard.sel's .fsel and .fframe in preview/panel-preview.html.
        internal const string FeatChosenLift = "ChosenLift", FeatChosenFrame = "ChosenFrame";

        static void FeatChosen(FeatHover h, bool on)
        {
            if (!h) return;
            if (on && !h.Lift)
            {
                var lift = Img(h.transform, FeatChosenLift, null, PanelWash.FeatChosen.Game); lift.Inside();
                lift.transform.SetSiblingIndex(Mathf.Clamp(h.LiftAt, 0, h.transform.childCount - 1));
                h.Lift = lift.gameObject;
            }
            if (on && !h.Frame)
            {
                var frame = Img(h.transform, FeatChosenFrame, PanelLook.RoundedEdge, PanelLook.Gold); frame.Stretched();
                if (frame.sprite) { frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 1f; }
                else { frame.color = Color.clear; Edge(frame.rectTransform, PanelLook.Gold); }   // no sprite drawn: a square 1 px rim
                frame.transform.SetAsLastSibling();   // over the pattern, the band, the icon and the text
                h.Frame = frame.gameObject;
            }
            if (h.Lift) h.Lift.SetActive(on);
            if (h.Frame) h.Frame.SetActive(on);
            if (h.Name) h.Name.color = on ? PanelLook.Gold : h.NameInk;
        }

        // ---------- the in-game proof of the names (Dev.SelfCheck) ----------

        // 0.8.96 (Joost in game, 0.8.95: the Unsung cards showed no names, the preview did): what only Unity's layout and TMP can show. Some
        // frames after a Feats grid is drawn (the page settled, FeatsSettle included), every card's name must have a box and show letters:
        // "HW-CHECK PASS feat-names: Feats/unsung: all 30 card names shown, lowest name box 20 px" or FAIL with the first card that does not.
        // Once per page a session; off unless Dev.SelfCheck
        const int FeatNamesFrames = 5;
        static int featNamesIn;
        static readonly HashSet<string> featNamesSeen = new HashSet<string>();

        void FeatNamesLate() { if (featNamesIn > 0 && --featNamesIn == 0) FeatNamesCheck(); }

        void FeatNamesCheck()
        {
            if (!DevCheck.On || !root || !root.activeInHierarchy || featsData == null || featCards.Count == 0) return;
            var page = view != null ? view.Active + "/" + (view.Page ?? "") : "Feats";
            if (!featNamesSeen.Add(page)) return;
            try
            {
                Canvas.ForceUpdateCanvases();
                int n = 0, bad = 0; float least = float.MaxValue; string first = null;
                foreach (var h in featCards)
                {
                    if (!h || !h.gameObject.activeInHierarchy) continue;
                    n++;
                    var t = h.Name; var r = t ? t.rectTransform.rect : default(Rect); var shown = false;
                    if (t && t.enabled && !string.IsNullOrEmpty(t.text) && r.width >= 1f && r.height >= 1f)
                    {
                        t.ForceMeshUpdate();
                        var info = t.textInfo;
                        for (int c = 0; info != null && c < info.characterCount && !shown; c++) shown = info.characterInfo[c].isVisible && info.characterInfo[c].character != '\u2026';
                    }
                    if (t) least = Mathf.Min(least, r.height);
                    if (!shown) { bad++; if (first == null) first = "'" + (t ? t.text : h.Id) + "' (" + h.Id + ") name box " + r.width.ToString("0") + " x " + r.height.ToString("0") + " px" + (t == null ? ", no name label" : string.IsNullOrEmpty(t.text) ? ", empty text" : ", no letter drawn"); }
                }
                var lowest = least < float.MaxValue ? ", lowest name box " + least.ToString("0") + " px" : "";
                DevCheck.Book.Say(bad == 0 ? "PASS" : "FAIL", "feat-names", page + ": " + (bad == 0 ? "all " + n + " card names shown" + lowest : bad + " of " + n + " card names not shown, first " + first + lowest));
                FeatLegibilitySoon(page);   // 0.9.1: the look's text read back from the screen (FeatLookUi.cs)
            }
            catch (Exception e) { DevCheck.Book.Say("FAIL", "feat-names", page + ": the check could not run: " + e.Message); }
        }

        // 0.9 New feats (DESIGN.md section 2): an earned tier not opened yet: the Feats tab's gold dot and the word "New", top right; the name beside it
        // keeps clear of it (the room returned). Also a look's card (FeatLookUi.cs)
        static (GameObject mark, float room) FeatNewMark(Transform card, Block c, float w)
        {
            if (!c.New) return (null, 0);
            var mark = Node("New", card);
            var word = Label(mark, PanelModel.FeatNewWord, PanelLook.MinText, PanelLook.Accent, align: TextAlignmentOptions.MidlineRight, style: FontStyles.Bold);
            word.textWrappingMode = TextWrappingModes.NoWrap;
            float ww = Mathf.Ceil(word.preferredWidth), wh = Mathf.Ceil(word.preferredHeight), dot = PanelLayout.Feats.NewDot, mw = dot + PanelLayout.Feats.NewGap + ww;
            mark.Box(w - PanelLayout.Feats.NewInset - mw, PanelLayout.Feats.NewInset, mw, wh);
            var spot = Img(mark, "Dot", PanelLook.Circle, PanelLook.Accent); spot.preserveAspect = false;
            spot.rectTransform.Box(0, Mathf.Floor((wh - dot) / 2), dot, dot);
            word.rectTransform.Box(dot + PanelLayout.Feats.NewGap, 0, ww, wh);
            return (mark.gameObject, mw + PanelLayout.Feats.NewInset);
        }

        // a dashed 1 px rim (a feat Hearthwoven does not count yet): short rects along the four sides
        static void DashEdge(RectTransform box, float w, float h, Color c)
        {
            const float dash = 5, gap = 4;
            void Seg(float x, float top, float sw, float sh) => Img(box, "Dash", null, c).rectTransform.Box(x, top, sw, sh);
            for (float x = 2; x < w - 2; x += dash + gap) { var len = Mathf.Min(dash, w - 2 - x); Seg(x, 0, len, 1); Seg(x, h - 1, len, 1); }
            for (float y = 2; y < h - 2; y += dash + gap) { var len = Mathf.Min(dash, h - 2 - y); Seg(0, y, 1, len); Seg(w - 1, y, 1, len); }
        }

        // the tiers as marks under the emblem (B30, Joost in game 0.7: tier III was there but never seen): one mark for every tier the feat has, earned
        // or not, so the card says how many there are. Each tier is an achievement of its own: a small square, all four corners rounded, 10 x 10, the
        // same for every tier, 4 apart. A tier reached is filled in its own colour (bronze, silver, gold), one not reached yet is an outline (fainter on
        // a feat Hearthwoven does not count yet). Three end at 46; the card's text starts at 54 (the old strokes touched "next: III at 2 500")
        static readonly Color NotchNext = PanelWash.NotchNext.Game;
        const float TierMark = 10, TierMarkStep = 14, TierMarkGap = 3, FeatTextLeft = 54;
        static void FeatNotches(RectTransform parent, int level, int count, float x, float top, bool waiting = false)
        {
            var ahead = waiting ? FeatWaitingRim : NotchNext;
            for (int k = 0; k < count; k++)
            {
                var reached = k < level;
                var sprite = reached ? PanelLook.Rounded : PanelLook.TierMarkEdge;
                var mark = Img(parent, reached ? "TierMark" : "TierMarkAhead", sprite, reached ? TierInk(PanelModel.TierColour(k + 1, count), PanelLook.Gold) : ahead);
                mark.preserveAspect = false;
                mark.rectTransform.Box(x + k * TierMarkStep, top, TierMark, TierMark);
                if (sprite) { mark.type = Image.Type.Sliced; mark.pixelsPerUnitMultiplier = PanelLook.TierMarkScale; }
                else if (!reached) { mark.color = Color.clear; Edge(mark.rectTransform, ahead); }   // no sprite drawn yet: a square outline
            }
        }

        // a gold dot where something new waits (the Deeds tab, the Feats entry), top right
        internal static void FeatDot(RectTransform parent)
        {
            var dot = Img(parent, "Dot", PanelLook.Circle, PanelLook.Accent);
            dot.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var r = dot.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(1, 1);
            r.anchoredPosition = new Vector2(-8, -8); r.sizeDelta = new Vector2(11, 11);
        }

        // ---------- the one fixed detail area ----------

        static void FeatDetailBlock(RectTransform col, Block d)
        {
            // the page's height (FeatsBlock worked it out over every card); a detail drawn without its grid takes its own need
            if (featsData == null || !(featsData.Items ?? new List<Block>()).Any(c => c.Items != null && c.Items.Contains(d))) featDetailH = FeatDetailPage(col, new List<Block> { new Block { Items = new List<Block> { d } } });
            var area = Kit(col, "FeatDetail", "meter-track");
            Size(area, -1, featDetailH);
            featDetail = area.rectTransform; featDetailWidth = Column;
            FeatDetailFill(area.rectTransform, Column, d);
        }

        // one hidden label measures text as the detail area draws it (size, bold or italic), so the page's detail height and the drawing agree
        sealed class FeatMeasure : IDisposable
        {
            readonly TextMeshProUGUI probe;
            public FeatMeasure(RectTransform parent)
            {
                probe = Label(parent, "", PanelLayout.Text.Floor, Color.clear);
                probe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            public float Width(string text, float size, FontStyles style)
            {
                probe.fontSize = size; probe.fontStyle = style; probe.textWrappingMode = TextWrappingModes.NoWrap;
                return Mathf.Ceil(probe.GetPreferredValues(text ?? "").x) + 2;
            }
            public float Height(string text, float size, FontStyles style, float width)
            {
                var key = (text ?? "") + "\u0001" + size + "\u0001" + (int)style + "\u0001" + width;   // 0.9.1: every card's detail is measured on each page: kept per text
                if (heights.TryGetValue(key, out var h)) return h;
                probe.fontSize = size; probe.fontStyle = style; probe.textWrappingMode = TextWrappingModes.Normal;
                h = probe.GetPreferredValues(text ?? "", width, 0).y;
                if (heights.Count > HeightsMost) heights.Clear();
                heights[key] = h; return h;
            }
            static readonly Dictionary<string, float> heights = new Dictionary<string, float>();
            const int HeightsMost = 2048;
            public void Dispose() { probe.gameObject.SetActive(false); UnityEngine.Object.Destroy(probe.gameObject); }
        }

        /// <summary>The detail area's height for a page of cards: the tallest detail's need (0.8.2: every person of a crew shows, the area grows to hold
        /// them; 0.9.1: every detail's lines counted, at least PanelLayout.FeatDetail.Least, no room kept for lines no card has).</summary>
        static float FeatDetailPage(RectTransform col, List<Block> cards)
        {
            var details = cards.Select(c => c.Items?.FirstOrDefault()).Where(d => d != null).ToList();
            if (details.Count == 0) return PanelLayout.FeatDetail.Least;
            using (var m = new FeatMeasure(col)) return Mathf.Max(PanelLayout.FeatDetail.Least, details.Max(d => FeatDetailNeed(d, Column, m)));
        }

        // what the detail's columns hold, in the order FeatDetailFill draws them: the right column down to its last line (the crew's last row, the link's
        // chip at its foot), the left down to the moment under what the feat honours, and the pad under the lowest
        static float FeatDetailNeed(Block d, float w, FeatMeasure m)
        {
            var items = d.Items ?? new List<Block>();
            var rw = w - FeatDetailLeft - 16;
            var rules = items.Count(i => i.Kind == "rule");
            var caveat = CaveatLines(items.FirstOrDefault(i => i.Kind == "caveat"), rw, m);
            var link = items.Any(i => i.Kind == "link" && !string.IsNullOrEmpty(i.Id));
            var need = PanelLayout.FeatDetail.RightNeed(rules, caveat, link);
            var crew = items.FirstOrDefault(i => i.Kind == "crew");
            if (crew != null)
            {
                var fit = CrewLayout(crew, rw, m);
                need = Mathf.Max(need, PanelModel.FeatCrewTop(rules, caveat) + PanelModel.CrewHeight(fit.per, PanelModel.CrewBar(crew)) + PanelModel.FeatDetailFoot + (link ? PanelLayout.FeatDetail.LinkRow : 0));
            }
            return Mathf.Max(need, PanelLayout.FeatDetail.LeftNeed(HonoursHeight(d, m), MomentHeight(d, m)));
        }

        // what the feat honours and its moment, as the left column draws them (FeatDetailFill)
        static float HonoursWidth => FeatDetailLeft - 32;
        static float HonoursHeight(Block d, FeatMeasure m) => m.Height(d.Text, PanelLayout.Text.Small, FontStyles.Italic, HonoursWidth);
        static float MomentHeight(Block d, FeatMeasure m)
        {
            var moment = d.Items?.FirstOrDefault(i => i.Kind == "moment");
            return moment == null ? 0 : m.Height(moment.Text + (string.IsNullOrEmpty(moment.Note) ? "" : " " + moment.Note), PanelLayout.Text.Small, FontStyles.Normal, HonoursWidth);
        }

        // the caveat's lines: none, one (it leaves its second line to the crew) or two
        static int CaveatLines(Block caveat, float rw, FeatMeasure m) => caveat == null ? 0 : m.Height(caveat.Text, 14, FontStyles.Italic, rw) <= 20 ? 1 : 2;

        /// <summary>Draws one feat into the detail area (again on a hover): emblem, name, tier and the warm line on the left; the rule per
        /// tier (reached ones lit), your progress, how it is counted, the caveat and the moment on the right.</summary>
        static void FeatDetailFill(RectTransform area, float w, Block d)
        {
            Clear(area);
            if (d == null) return;
            var earned = d.Tone == PanelModel.FeatTone; var waiting = d.Tone == PanelModel.FeatWaitingTone;
            var tier = TierInk(d.Colour, FeatRim);
            var h = featDetailH;   // the page's height: a hover draws into the same area
            var look = d.Look > 0;   // 0.9 feat look: the family's ground and edge, the disc behind the emblem, the family line (FeatLookUi.cs)
            if (look) FeatLookDetail(area, d, w, h, tier, earned);
            else if (earned) { Edge(area, new Color(tier.r, tier.g, tier.b, 0.9f)); Img(area, "Tier", null, tier).rectTransform.Box(0, 0, 3, h); }
            if (look && earned) Edge(area, new Color(tier.r, tier.g, tier.b, FeatRimAlpha));
            if (waiting) DashEdge(area, w, h, FeatWaitingRim);
            if (look) SoftDiscBehind(area, FeatEmblemLeft, FeatEmblemTop, FeatEmblem);
            var emblem = Marker(area, d.Icon, FeatEmblem, layout: false); emblem.Box(FeatEmblemLeft, FeatEmblemTop, FeatEmblem, FeatEmblem);
            if (!earned) foreach (var im in emblem.GetComponentsInChildren<Image>()) im.color = waiting ? FeatWaitingInk : FeatUnsungInk;
            var name = Label(area, d.Title, PanelLayout.Text.Title, earned ? PanelLook.Text : PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
            if (look) FeatLookShade(name);
            name.rectTransform.Box(84, look ? PanelLayout.FeatLook.DetailNameTop : PanelLayout.FeatDetail.NameTop, FeatDetailLeft - 88, 30); name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
            // a long name (a group feat's riddle, "What the Deep Woods Hold") steps down to 16 px before it would be cut
            for (var size = PanelLayout.Text.Lead; size >= PanelLayout.Text.Body && name.preferredWidth > FeatDetailLeft - 88; size -= 2) name.fontSize = size;
            if (look) FeatLookFamilyLine(area, d, FeatNameLeft, FeatDetailLeft - FeatNameRoom, earned);
            if (!string.IsNullOrEmpty(d.Value))
            {
                var tierLabel = Label(area, d.Value, PanelLayout.Text.Floor, earned ? tier : PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft, style: earned ? FontStyles.Bold : FontStyles.Normal);
                if (look) FeatLookShade(tierLabel);
                tierLabel.rectTransform.Box(84, look ? PanelLayout.FeatLook.DetailTierTop : PanelLayout.FeatDetail.TierTop, FeatDetailLeft - 92, 22); tierLabel.textWrappingMode = TextWrappingModes.NoWrap; tierLabel.overflowMode = TextOverflowModes.Ellipsis;
            }
            var honours = Label(area, d.Text, PanelLayout.Text.Small, PanelLook.Muted, style: FontStyles.Italic);
            if (look) FeatLookShade(honours);
            // one to three lines (0.7: the group feats' lines say what the thing is for); the moment sits under it either way. 0.9.1: no second line kept
            // for a one-line text (the area is as high as its page needs)
            var honoursH = PanelLayout.FeatDetail.Clamp(honours.GetPreferredValues(d.Text ?? "", HonoursWidth, 0).y, PanelLayout.FeatDetail.HonoursLine, PanelLayout.FeatDetail.HonoursMost);
            honours.rectTransform.Box(FeatEmblemLeft, PanelLayout.FeatDetail.HonoursTop, HonoursWidth, honoursH); honours.overflowMode = TextOverflowModes.Ellipsis;

            float x = FeatDetailLeft, rw = w - x - 16, y = PanelLayout.FeatDetail.Top;
            foreach (var r in (d.Items ?? new List<Block>()).Where(i => i.Kind == "rule"))
            {
                var numeral = !string.IsNullOrEmpty(r.Value);   // a one-off feat has one rule and no numeral
                if (numeral)
                {
                    var n = Label(area, r.Value, PanelLayout.Text.Small, r.Selected ? TierInk(r.Colour, PanelLook.Gold) : PanelLook.Faint, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft);
                    n.rectTransform.Box(x, y, 30, FeatRuleH); n.textWrappingMode = TextWrappingModes.NoWrap;
                }
                var t = Label(area, r.Title, PanelLayout.Text.Small, r.Selected ? PanelLook.Text : PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
                t.rectTransform.Box(x + (numeral ? 32 : 0), y, rw - (numeral ? 32 : 0), FeatRuleH); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
                y += FeatRuleH;
            }
            var progress = (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "progress");
            y += PanelLayout.FeatDetail.ProgressGap;
            if (progress != null)
            {
                var track = Kit(area, "Track", "meter-track"); track.rectTransform.Box(x, y + 6, rw - 196, 10);
                if (progress.Fraction > 0)
                {
                    var f = Fill(track.transform, "Fill", PanelLook.Accent).rectTransform;
                    f.anchorMin = Vector2.zero; f.anchorMax = new Vector2(Mathf.Clamp01(progress.Fraction), 1); f.offsetMin = new Vector2(2, 2); f.offsetMax = new Vector2(-2, -2);
                }
                var pt = Label(area, progress.Title, PanelLayout.Text.Floor, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
                pt.rectTransform.Box(x + rw - 186, y, 186, 22); pt.textWrappingMode = TextWrappingModes.NoWrap;
            }
            y += PanelLayout.FeatDetail.ProgressRow;
            var counted = (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "counted");
            if (counted != null)
            {
                var c1 = Label(area, "Counted by", PanelLayout.Text.Floor, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft); var w1 = Mathf.Ceil(c1.preferredWidth) + 2; c1.rectTransform.Box(x, y, w1, 18); c1.textWrappingMode = TextWrappingModes.NoWrap;
                var c2 = Label(area, counted.Text, PanelLayout.Text.Floor, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); c2.rectTransform.Box(x + w1 + 8, y, rw - w1 - 8, 18); c2.textWrappingMode = TextWrappingModes.NoWrap;
            }
            y += PanelLayout.FeatDetail.CountedRow;
            var caveat = (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "caveat");   // none (B37: the counting is said in Counted by): no room kept for one
            if (caveat != null) { var cv = Label(area, caveat.Text, PanelLayout.Text.Floor, PanelLook.Muted, style: FontStyles.Italic); cv.rectTransform.Box(x, y, rw, 36); cv.overflowMode = TextOverflowModes.Ellipsis; }
            var crew = (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "crew");   // a group feat: who helped, with their parts (B37), or who carried
            if (crew != null)
                using (var m = new FeatMeasure(area))   // a one-line caveat leaves the second line to who added to it (0.7 group feats); the crew always shows, whole (0.8.2)
                    FeatCrew(area, crew, x, PanelModel.FeatCrewTop((d.Items ?? new List<Block>()).Count(i => i.Kind == "rule"), CaveatLines(caveat, rw, m)), rw, m);
            FeatOpenLink(area, (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "link"), x, h, rw);
            var moment = (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "moment");
            if (moment != null)
            {
                // short (Joost 2026-10-09): "Earned 7 Oct", and a quiet "(or earlier)" only when the day is the day it was first seen
                var said = Rich.Plain(moment.Text) + (string.IsNullOrEmpty(moment.Note) ? Rich.Empty : " " + Rich.Plain(moment.Note).Ink(Hex(PanelLook.Muted)).Italic());
                var m = RichLabel(area, "featdetail", said, PanelLayout.Text.Small, PanelLook.Text, align: TextAlignmentOptions.TopLeft); var mTop = PanelLayout.FeatDetail.HonoursTop + honoursH + PanelLayout.FeatDetail.MomentGap; m.rectTransform.Box(FeatEmblemLeft, mTop, HonoursWidth, h - 4 - mTop); if (look) FeatLookShade(m); m.overflowMode = TextOverflowModes.Ellipsis;   // left column, under what the feat honours
            }
        }

        // B37 (Joost: '"added to by"? what does that mean?'): "Who cooked", then the parts as one bar in the players' colors (when the parts add up to
        // the group's number, Tone "share"). 0.8.2 (Joost: "I want to see everyone ... not plus three more"): under the bar every person in the book's
        // legend grammar (BarFormUi.BarList): their color, name, the number right-aligned in bold, the share; a compact table per column, filled top to
        // bottom, as many columns as fit beside the label (PanelModel.CrewFit), as many rows as it takes. A set's parts overlap (each different thing
        // once): their own counts only, no bar, no share. Iron for the Forge's crew: shields by name. Without a bar the first row sits on the label's line.
        const float CrewBarH = 8, CrewSwatch = 12, CrewSwatchGap = 8, CrewShield = 20, CrewShieldGap = 4;

        // a crew's cells as drawn (the mark and name, the number, the share) and the legend's columns beside its label
        static (float label, List<(float name, float number, float share)> cells, int columns, int per) CrewLayout(Block crew, float rw, FeatMeasure m)
        {
            var shared = crew.Tone == "share";
            var label = m.Width(crew.Title, 14, FontStyles.Normal);
            var cells = (crew.Items ?? new List<Block>()).Select(p => (
                name: (string.IsNullOrEmpty(p.Value) ? CrewShield + CrewShieldGap : CrewSwatch + CrewSwatchGap) + m.Width(p.Title, PanelLook.MinText, FontStyles.Normal),
                number: string.IsNullOrEmpty(p.Value) ? 0f : m.Width(p.Value, 15, FontStyles.Bold),
                share: shared && !string.IsNullOrEmpty(p.Note) ? m.Width(p.Note, 14, FontStyles.Normal) : 0f)).ToList();
            var (columns, per) = PanelModel.CrewFit(cells, rw - label - PanelModel.CrewLabelGap);
            return (label, cells, columns, per);
        }

        static void FeatCrew(RectTransform area, Block crew, float x, float y, float rw, FeatMeasure m)
        {
            const float row = PanelModel.CrewRowH;
            var (lw, cells, _, per) = CrewLayout(crew, rw, m);
            var label = Label(area, crew.Title, PanelLayout.Text.Floor, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft);
            label.rectTransform.Box(x, y, lw, row); label.textWrappingMode = TextWrappingModes.NoWrap;
            float cx = x + lw + PanelModel.CrewLabelGap, end = x + rw;
            var people = crew.Items ?? new List<Block>();
            if (people.Count == 0) return;
            if (PanelModel.CrewBar(crew))   // the parts as one bar across the room beside the label, the legend under it
            {
                float bx = cx, room = end - cx - 2 * (people.Count - 1);
                for (int k = 0; k < people.Count; k++)
                {
                    var p = people[k];
                    var pw = k == people.Count - 1 ? end - bx : Mathf.Max(2f, Mathf.Round(room * p.Fraction));   // the last part ends at the bar's end
                    if (bx + pw > end) pw = Mathf.Max(0f, end - bx);
                    var part = Img(area, "Share", PanelLook.Rounded, PersonTint(p.Title)); part.preserveAspect = false;
                    if (part.sprite) { part.type = Image.Type.Sliced; part.pixelsPerUnitMultiplier = 2f * 6f / CrewBarH; }
                    part.rectTransform.Box(bx, y + (row - CrewBarH) / 2, pw, CrewBarH); bx += pw + 2;
                }
                y += row;
            }
            // the legend: each column a compact table that hugs its content (the mark and name | the number, right-aligned | the share, right-aligned)
            for (int from = 0; from < people.Count; from += per)
            {
                var to = Math.Min(people.Count, from + per);
                float nameW = 0, numW = 0, shareW = 0;
                for (int i = from; i < to; i++) { nameW = Mathf.Max(nameW, cells[i].name); numW = Mathf.Max(numW, cells[i].number); shareW = Mathf.Max(shareW, cells[i].share); }
                for (int i = from; i < to; i++)
                {
                    var p = people[i]; var top = y + (i - from) * row; float px = cx;
                    if (!string.IsNullOrEmpty(p.Value))   // their color, as in the bar
                    {
                        var sw = Img(area, "Who", PanelLook.Rounded, PersonTint(p.Title)); sw.preserveAspect = false;
                        if (sw.sprite) { sw.type = Image.Type.Sliced; sw.pixelsPerUnitMultiplier = PanelLook.TierMarkScale; }
                        sw.rectTransform.Box(px, top + (row - CrewSwatch) / 2, CrewSwatch, CrewSwatch); px += CrewSwatch + CrewSwatchGap;
                    }
                    else { Marker(area, p.Icon, CrewShield, layout: false).Box(px, top + (row - CrewShield) / 2, CrewShield, CrewShield); px += CrewShield + CrewShieldGap; }   // Carried by: their shield
                    var name = Label(area, p.Title, PanelLook.MinText, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); name.textWrappingMode = TextWrappingModes.NoWrap;
                    name.rectTransform.Box(px, top, cx + nameW - px, row);
                    if (numW > 0 && !string.IsNullOrEmpty(p.Value))
                    {
                        var n = Label(area, p.Value, PanelLayout.Text.Small, PanelLook.Text, align: TextAlignmentOptions.MidlineRight, style: FontStyles.Bold); n.textWrappingMode = TextWrappingModes.NoWrap;
                        n.rectTransform.Box(cx + nameW + PanelModel.CrewCellGap, top, numW, row);
                    }
                    if (shareW > 0 && !string.IsNullOrEmpty(p.Note))
                    {
                        var s = Label(area, p.Note, PanelLayout.Text.Floor, PanelLook.Faint, align: TextAlignmentOptions.MidlineRight); s.textWrappingMode = TextWrappingModes.NoWrap;
                        s.rectTransform.Box(cx + nameW + (numW > 0 ? PanelModel.CrewCellGap + numW : 0) + PanelModel.CrewCellGap, top, shareW, row);
                    }
                }
                cx += PanelModel.CrewColumnWidth(cells, from, to) + PanelModel.CrewColumnGap;
            }
        }

        /// <summary>The selection ring moves and the detail area shows the card (a click or A/D rebuild the page; 0.9: never a hover,
        /// PanelModel.FeatPointer, so the pointer can cross other cards on its way to the detail area's link).</summary>
        internal static void FeatSelect(FeatHover h)
        {
            if (!h) return;
            foreach (var c in featCards) if (c) FeatChosen(c, c == h);   // 0.9.3: the chosen look follows the choice, mouse or keys alike (FeatChosen)
            if (featDetail && h.Detail != null) FeatDetailFill(featDetail, featDetailWidth, h.Detail);
            if (Instance != null) Instance.state.FeatSel = h.Id;   // A/D go on from here
        }

        /// <summary>A hover on a card: nothing chosen (PanelModel.FeatPointer says so), the card's own hover line only.</summary>
        internal static void FeatHovered(FeatHover h) { if (h && Instance != null && PanelModel.FeatPointer(Instance.state, h.Id, click: false)) FeatSelect(h); }

        /// <summary>0.9 New feats: a click opens the card: chosen, then its tiers seen and its mark gone (your own book only: a New card is never on a
        /// fellow's). A hover never opens one: a mouse sweep must not clear the marks (Joost: new feats are hard to spot). 0.9.2 (Joost: "Only show when
        /// I click to select something"): the page is drawn again, the detail area coming in under the grid (or, a click on the chosen card, going), the
        /// grid where the player left it with the chosen card in view (PanelModel.FeatPointer, GridScroll).</summary>
        internal static void FeatClick(FeatHover h)
        {
            if (!h || Instance == null) return;
            if (h.NewMark && h.NewMark.activeSelf && !SampleMode.On) PanelModel.FeatOpened(Plugin.FeatsLedger, h.Id);
            if (!PanelModel.FeatPointer(Instance.state, h.Id, click: true)) return;
            followChoice = true; Instance.Render(true);
        }

        // ---------- Known for (Deeds > Overview) and the band on an owner page ----------

        static void KnownForBlock(RectTransform col, Block b, Func<string, Action> link)
        {
            var row = Line(col, 10); Size(row, -1, 36);
            Keeps(Label(row, b.Title, PanelLayout.Text.Small, PanelLook.Gold, style: FontStyles.Bold));
            foreach (var i in b.Items ?? new List<Block>())
            {
                var chip = Kit(row, i.Title, "meter-track", raycast: link != null);
                var tier = TierInk(i.Colour, FeatRim);
                Edge(chip.rectTransform, new Color(tier.r, tier.g, tier.b, 0.9f));
                Layout(chip.gameObject.AddComponent<HorizontalLayoutGroup>(), 6, TextAnchor.MiddleLeft);
                chip.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(8, 12, 4, 4);
                Marker(chip.rectTransform, i.Icon, 26);
                Keeps(Label(chip.transform, i.Title, PanelLayout.Text.Small, PanelLook.Text));
                if (!string.IsNullOrEmpty(i.Value)) Keeps(Label(chip.transform, i.Value, PanelLayout.Text.Small, tier, style: FontStyles.Bold));
                FeatLink(chip, link, b.Id);
            }
        }

        // fix4: the page's feat and its titles in ONE compact strip at the top of the plate (it was a feat tile and, apart from it, a title pill
        // in the heading row: two things, both shaped like buttons). Flat: a dark ground with a gold edge at its left, no raised border; the feat
        // part opens the Feats page like the tile did, each title (B18: with its reason, quiet after the name) the Titles page with it chosen.
        // One line; the titles drop to a second line only when the first is full.
        // 0.8.6 (Joost: "a full black bar at the top. Can we make that the same color as the other islands?"): the strip is an island, its
        // ground and its padding the islands' (it was black at 0.55 over the plate, and 12 px in); its gold edge stays clear of the round corners
        static readonly float EarnedPad = PanelLayout.Island.Pad, EdgeInset = PanelLayout.Island.Radius - 3;
        const float EarnedLine = 34, EarnedGap = 18, TitleReasonGap = 8;
        /// <summary>0.8.6: the Back button's chip and its key's cap in the strip, as tall as the About button's, and the room between them.</summary>
        const float StripChipH = 26, StripCapH = 22, StripCapGap = 8;

        static void FeatBandBlock(RectTransform col, Block b, Func<string, Action> link)
        {
            var feats = PanelModel.BandFeats(b);
            var titles = PanelModel.BandTitles(b);
            // 0.8 layout D+ (PageHead.cs): the strip is the page's head; with no feat or title it still carries the switch, About these numbers or
            // a fellow's note, and the switch's caption on its left
            var caption = feats.Count == 0 && titles.Count == 0 ? stripSwitch?.Title : null;
            if (feats.Count == 0 && titles.Count == 0 && stripAbout == null && stripSwitch == null && string.IsNullOrEmpty(stripNote) && stripCompare == null && stripBack == null && stripLinks.Count == 0) return;
            // what goes on the line, measured first (the same label settings as drawn): (marker icon, text, colour, style, tag)
            float Measure(Rich text, float size, FontStyles style, float spacing = 0)
            {
                var m = RichLabel(col, "featband", text, size, Color.white, style: style); m.textWrappingMode = TextWrappingModes.NoWrap; m.characterSpacing = spacing;   // measured as drawn: rich text on, so tags take no width; letter spacing counts (the FEAT tag was cut to "FE..." in 0.6.2)
                var w = Mathf.Ceil(m.preferredWidth); m.gameObject.SetActive(false); Destroy(m.gameObject); return w;
            }
            var many = feats.Count > 1;
            var featParts = feats.Select(f => (f, name: Rich.Plain(f.Title) + (string.IsNullOrEmpty(f.Value) ? Rich.Empty : " " + Rich.Plain(f.Value).Ink(Hex(TierInk(f.Colour, PanelLook.Gold))).Bold()))).ToList();
            var featTagW = featParts.Count == 0 ? 0 : Measure(Rich.Plain(many ? PanelModel.FeatsHeading : PanelModel.FeatLabel), PanelLook.MinText, FontStyles.UpperCase, 10) + 8;
            var featPartW = featParts.Select(p => 22 + 6 + Measure(p.name, 15, FontStyles.Normal)).ToList();   // 0.9: each feat's width, kept for the "+N more" fit below
            float featW = featParts.Count == 0 ? 0 : featTagW + featPartW.Sum(w => w + EarnedGap) - EarnedGap;
            var moment = !many && feats.Count == 1 ? feats[0].Text ?? "" : "";
            if (moment.Length > 0) featW += 10 + Measure(Rich.Plain(moment), PanelLook.MinText, FontStyles.Italic);
            var titleParts = titles.Select(t => (t, nameW: Measure(Rich.Plain(t.Title), 15, FontStyles.Normal), reasonW: string.IsNullOrEmpty(t.Text) ? 0f : Measure(Rich.Plain(t.Text), PanelLook.MinText, FontStyles.Italic))).ToList();
            float titleW = titleParts.Count == 0 ? 0 : Measure(Rich.Plain(b.Text ?? PanelModel.TitlesWord), PanelLook.MinText, FontStyles.UpperCase, 10) + 8
                + titleParts.Sum(p => 22 + 6 + p.nameW + (p.reasonW > 0 ? TitleReasonGap + p.reasonW : 0) + EarnedGap) - EarnedGap;
            var inner = Column - EarnedPad * 2;
            // 0.8.6: the Back button leads at the strip's left end, its width taken first; what was there starts after it
            RectTransform back = null; float backW = 0;
            if (stripBack != null) { back = BackCluster(col, stripBack); backW = TopClusterWidth(back); inner -= backW + EarnedGap; }
            // 0.8.5: Compare's legend (or a greyed chip's reason) leads the left; what was there follows it when it still fits (CompareUi.StripCompare)
            var cmpW = stripCompare != null ? StripCompare(col, stripCompare, 0, float.MaxValue, false) : 0f;
            // B38: the top items in one row: the page's view switch and its "About these numbers" button at the strip's right end, reserved
            // first (PanelModel.StripFit: "Numbers" when it is full, then the feat's note goes; never a second row)
            var about = stripAbout != null && !aboutInStrip ? stripAbout : null;
            var sw = stripSwitch != null && !switchInStrip ? stripSwitch : null;
            float reserve = 0, aboutW = 0; RectTransform cluster = null;
            if (about != null || sw != null || !string.IsNullOrEmpty(stripNote) || stripLinks.Count > 0)   // 0.9: a Deeds page's links ride at its right end too
            {
                // the feat part and room for "TITLES +2 more" beside it, or the tag and one title
                var titlesTag = titleParts.Count == 0 ? 0 : Measure(Rich.Plain(b.Text ?? PanelModel.TitlesWord), PanelLook.MinText, FontStyles.UpperCase, 10) + 8;
                var lead = featW > 0 ? featW + (titleParts.Count > 0 ? EarnedGap * 2 + 1 + titlesTag + Measure(Rich.Plain(PanelModel.MoreTitles(titleParts.Count)), PanelLook.MinText, FontStyles.Italic) : 0)
                         : titleParts.Count > 0 ? titlesTag + 22 + 6 + 40 : string.IsNullOrEmpty(caption) ? 0 : Mathf.Min(160f, Measure(Rich.Plain(caption), 15, FontStyles.Normal));
                if (cmpW > 0) lead = cmpW + (lead > 0 ? CmpSep + lead : 0);
                cluster = TopCluster(col, sw, about, false, link, stripNote, stripLinks); aboutW = TopClusterWidth(cluster);
                var noteW = moment.Length > 0 ? 10 + Measure(Rich.Plain(moment), PanelLook.MinText, FontStyles.Italic) : 0;
                var shortW = aboutW;
                if (about != null) { var s = TopCluster(col, sw, about, true, link, stripNote, stripLinks); shortW = TopClusterWidth(s); s.gameObject.SetActive(false); Destroy(s.gameObject); }
                var (shortAbout, dropNote) = PanelModel.StripFit(lead, aboutW, shortW, noteW, inner);
                if (shortAbout && about != null) { cluster.gameObject.SetActive(false); Destroy(cluster.gameObject); cluster = TopCluster(col, sw, about, true, link, stripNote, stripLinks); aboutW = TopClusterWidth(cluster); }
                if (dropNote) { featW -= noteW; moment = ""; }
                reserve = aboutW > 0 ? PanelModel.StripAboutGap + aboutW : 0f;   // 0.9.5: an empty cluster (the Feats line's switch, no chips) keeps no room: Mark read and the search field use it
                inner -= reserve;
            }
            // 0.9 (armor-share: a friend's Defense, many feats beside the view switch drew under it): with the switch or About at the strip's end,
            // feats past the room end in "+N more" (PanelModel.MoreTitles), as the titles do, part of the band's link to the Feats page
            int featsMore = 0; float featMoreW = 0;
            if (cluster != null && cmpW <= 0 && many && featW > inner)
            {
                float MoreW(int k) => Measure(Rich.Plain(PanelModel.MoreTitles(k)), PanelLook.MinText, FontStyles.Italic);
                float Upto(int k) => featTagW + featPartW.Take(k).Sum(w => w + EarnedGap) + MoreW(featParts.Count - k);   // k feats, then "+N more"
                var n = featParts.Count;
                while (n > 1 && Upto(n) > inner) n--;
                featsMore = featParts.Count - n; featMoreW = MoreW(featsMore);
                featParts = featParts.Take(n).ToList(); featW = Upto(n);
            }
            // 0.8.5: beside Compare's part the feat keeps its place while it fits (its note goes first), else the Feats page and All show it
            if (cmpW > 0)
            {
                cmpW = Mathf.Min(cmpW, inner);
                var left = inner - cmpW - CmpSep;
                if (featParts.Count > 0 && featW > left && moment.Length > 0) { featW -= CmpNoteGap + Measure(Rich.Plain(moment), PanelLook.MinText, FontStyles.Italic); moment = ""; }
                if (featParts.Count > 0 && featW > left) { featParts.Clear(); featW = 0; }
            }
            // the titles do not fit beside the feat: a second line, unless the switch or the About button rides on the right (B38: one row; the
            // titles then end in "+N more" where the room ends)
            var second = cluster == null && featW > 0 && titleW > 0 && featW + EarnedGap * 2 + 1 + titleW > inner;
            var box = Node("Earned", col); Size(box, -1, second ? EarnedLine * 2 : EarnedLine);
            if (!stripDrawn) stripDrawn = box;   // 0.8.6: the page's strip, pinned by Fill when it leads the plate (StickyHeadUi.cs)
            var ground = Img(box, "Ground", PanelLook.Island, PanelLook.IslandGround); if (PanelLook.Island) { ground.type = Image.Type.Sliced; ground.pixelsPerUnitMultiplier = 1f; }   // 0.8.6: an island, as the ones under it
            ground.rectTransform.Box(0, 0, Column, second ? EarnedLine * 2 : EarnedLine);
            if (featParts.Count > 0 || titleParts.Count > 0) BtRect(box, "Edge", PanelLook.Gold, 0, EdgeInset, 3, (second ? EarnedLine * 2 : EarnedLine) - 2 * EdgeInset);   // the gold edge leads the feats and titles; an empty left has none
            float x = EarnedPad, top = 0;
            if (back != null)
            {
                back.SetParent(box, false); var ble = back.GetComponent<LayoutElement>(); if (ble) Destroy(ble);
                back.Box(x, (EarnedLine - StripChipH) / 2, backW, StripChipH); x += backW + EarnedGap;
            }
            if (cmpW > 0)
            {
                x += StripCompare(box, stripCompare, x, cmpW, true);
                if (featParts.Count > 0 || titleParts.Count > 0) { x += EarnedGap; BtRect(box, "Rule", PanelLook.Rule, x, CmpRuleInset, 1, EarnedLine - 2 * CmpRuleInset); x += 1 + EarnedGap; }
            }
            if (!string.IsNullOrEmpty(caption))   // the switch's caption: what every number on the page is
            {
                var c = BtText(box, caption, x, top, Mathf.Max(0f, Column - EarnedPad - reserve - x), EarnedLine, PanelLayout.Text.Small, PanelLook.Muted);
                c.textWrappingMode = TextWrappingModes.NoWrap; c.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (cluster != null)
            {
                cluster.SetParent(box, false); var le = cluster.GetComponent<LayoutElement>(); if (le) Destroy(le);
                cluster.Box(Column - EarnedPad - aboutW, (EarnedLine - 26) / 2, aboutW, 26);
                aboutInStrip = about != null; switchInStrip = sw != null;
                if (aboutRow && about != null) aboutRow.gameObject.SetActive(false);   // drawn before the strip (On foot): its own row goes
            }
            void Tag(string text, Color c) { var w = Measure(Rich.Plain(text), PanelLook.MinText, FontStyles.UpperCase, 10); var t = BtText(box, text, x, top, w + 2, EarnedLine, PanelLook.MinText, c, style: FontStyles.UpperCase); t.characterSpacing = 10; x += w + 8; }
            void Hit(string target, float from, float to)
            {
                var click = string.IsNullOrEmpty(target) ? null : link?.Invoke(target);
                if (click == null) return;
                var hit = Img(box, "Hit", null, new Color(0, 0, 0, 0), raycast: true); hit.rectTransform.Box(from, top, to - from, EarnedLine);
                ClickTarget(hit, click);
            }
            if (featParts.Count > 0)
            {
                var from = x;
                Tag(many ? PanelModel.FeatsHeading : PanelModel.FeatLabel, PanelLook.Gold);
                foreach (var (f, name) in featParts)
                {
                    BtMarker(box, f.Icon, x, top + (EarnedLine - 22) / 2, 22); x += 22 + 6;
                    var w = Measure(name, 15, FontStyles.Normal); BtRich(box, "featband", name, x, top, w + 2, EarnedLine, 15, PanelLook.Text); x += w + EarnedGap;
                }
                x -= EarnedGap;
                if (featsMore > 0) { x += EarnedGap; BtText(box, PanelModel.MoreTitles(featsMore), x, top, featMoreW + EarnedGap, EarnedLine, PanelLook.MinText, PanelLook.Muted, style: FontStyles.Italic); x += featMoreW; }   // 0.9: the feats past the room
                if (moment.Length > 0) { x += 10; var w = Measure(Rich.Plain(moment), PanelLook.MinText, FontStyles.Italic); BtText(box, moment, x, top, w + 2, EarnedLine, PanelLook.MinText, PanelLook.Muted, style: FontStyles.Italic); x += w; }
                Hit(b.Id, from, x);
            }
            // B38: one row and no room left even for "TITLES +2 more": the titles stay on the Titles page
            var tagW = titleParts.Count == 0 ? 0 : Measure(Rich.Plain(b.Text ?? PanelModel.TitlesWord), PanelLook.MinText, FontStyles.UpperCase, 10) + 8 + Measure(Rich.Plain(PanelModel.MoreTitles(titleParts.Count)), PanelLook.MinText, FontStyles.Italic);
            if (titleParts.Count > 0 && (second || cluster == null || x + (featParts.Count > 0 ? EarnedGap * 2 + 1 : 0) + tagW <= Column - EarnedPad - reserve))
            {
                if (featParts.Count > 0 && !second) { x += EarnedGap; BtRect(box, "Rule", PanelLook.Rule, x, 7, 1, EarnedLine - 14); x += 1 + EarnedGap; }
                if (second) { x = EarnedPad; top = EarnedLine; }
                Tag(b.Text ?? PanelModel.TitlesWord, PanelLook.Muted);
                var end = Column - EarnedPad - reserve;
                for (int k = 0; k < titleParts.Count; k++)
                {
                    var (t, nameW, reasonW) = titleParts[k];
                    // room is kept for "+N more" while titles follow (review 0.6.5: the ones past the edge were dropped without a word)
                    var after = titleParts.Count - k - 1;
                    var keep = after > 0 ? EarnedGap + Measure(Rich.Plain(PanelModel.MoreTitles(after)), PanelLook.MinText, FontStyles.Italic) : 0f;
                    if (x + 22 + 6 + 40 + keep > end)   // no room left for one more name: say how many more, linked to the Titles page as a title is
                    {
                        var more = PanelModel.MoreTitles(titleParts.Count - k); var moreW = Measure(Rich.Plain(more), PanelLook.MinText, FontStyles.Italic);
                        BtText(box, more, x, top, Mathf.Min(moreW + 2, end - x), EarnedLine, PanelLook.MinText, PanelLook.Muted, style: FontStyles.Italic);
                        Hit(t.Id, x, Mathf.Min(x + moreW, end));
                        break;
                    }
                    var stop = end - keep;
                    var from = x;
                    BtMarker(box, t.Icon, x, top + (EarnedLine - 22) / 2, 22); x += 22 + 6;
                    BtText(box, t.Title, x, top, Mathf.Min(nameW + 2, stop - x), EarnedLine, PanelLayout.Text.Small, PanelLook.Text); x += Mathf.Min(nameW, stop - x);
                    if (reasonW > 0 && x + TitleReasonGap + 40 <= stop)
                    {
                        x += TitleReasonGap; BtText(box, t.Text, x, top, Mathf.Min(reasonW + 2, stop - x), EarnedLine, PanelLook.MinText, PanelLook.Muted, style: FontStyles.Italic); x += Mathf.Min(reasonW, stop - x);
                    }
                    Hit(t.Id, from, Mathf.Min(x, stop));
                    x += EarnedGap;
                }
            }
        }

        // 0.8.6: the Back button and its key's cap, as the About button and its cap: the chip with the opens mark turned back before "Back", then
        // the cap 8 px after it; measured as laid out (TopClusterWidth)
        static RectTransform BackCluster(RectTransform parent, Action back)
        {
            var row = Line(parent, StripCapGap);
            Chip(row, PanelModel.BackLabel, null, false, back);
            var chip = (RectTransform)row.GetChild(row.childCount - 1);
            var label = chip.GetComponentInChildren<TextMeshProUGUI>();
            if (PanelLook.Chevron && label)   // the › that marks what opens, turned to point back, in its own room before the word
            {
                var room = PanelLook.ChevronW + BackChevronGap;
                label.rectTransform.offsetMin += new Vector2(room, 0);
                var le = chip.GetComponent<LayoutElement>(); if (le) { le.minWidth += room; le.preferredWidth += room; }
                var mark = Img(chip, "Back", PanelLook.Chevron, PanelLook.Text).rectTransform;
                mark.anchorMin = mark.anchorMax = mark.pivot = Vector2.up / 2; mark.sizeDelta = new Vector2(PanelLook.ChevronW, PanelLook.ChevronH);
                mark.anchoredPosition = new Vector2(PanelLayout.Chip.Pad + PanelLook.ChevronW, 0); mark.localScale = new Vector3(-1, 1, 1);   // mirrored about its left edge: it fills [Pad, Pad + ChevronW]
            }
            Size(Keycap(row, PanelModel.BackKeyCap), KeycapWidth(PanelModel.BackKeyCap, StripCapH), StripCapH);
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            return row;
        }
        /// <summary>0.9: a feat's link to the page that shows what it counts (Clad for Every Land: "Open Armor sets"), at the foot of the detail's right
        /// column, right-aligned: the heading row's link chip (PanelUi.HeadRight's headlink) with the opens mark after its word, as Compare's.</summary>
        static void FeatOpenLink(RectTransform area, Block link, float x, float h, float rw)
        {
            if (link == null || string.IsNullOrEmpty(link.Id)) return;
            var chipH = PanelLayout.Chip.Height;
            var row = Node("Open", area); row.Box(x, h - PanelLayout.Feats.LinkBottom - chipH, rw, chipH);
            Layout(row.gameObject.AddComponent<HorizontalLayoutGroup>(), 0, TextAnchor.MiddleRight);
            var w = Chip(row, link.Title, null, false, Instance != null ? Instance.LinkTo(link.Id) : null);
            var chip = (RectTransform)row.GetChild(row.childCount - 1);
            if (!PanelLook.Chevron) return;
            var mark = Img(chip, "Opens", PanelLook.Chevron, PanelLook.Text).rectTransform;
            mark.anchorMin = mark.anchorMax = Vector2.right + Vector2.up / 2; mark.pivot = Vector2.one / 2;
            mark.sizeDelta = new Vector2(PanelLook.ChevronW, PanelLook.ChevronH); mark.anchoredPosition = new Vector2(-PanelLayout.Chip.Pad - PanelLook.ChevronW / 2, 0);
            Size(chip, w + PanelLook.ChevronW + PanelLayout.Feats.LinkMarkGap, -1);   // its word stays where it was (drawn from the left)
        }

        /// <summary>Between the Back button's turned › and its word.</summary>
        const float BackChevronGap = 6;

        static void FeatLink(Image chip, Func<string, Action> link, string target)
        {
            var click = string.IsNullOrEmpty(target) ? null : link?.Invoke(target);
            if (click == null) return;
            ClickTarget(chip, click);
        }

        // ---------- keys and the render step ----------

        // A/D, the arrows and the D-pad choose the feat on the Feats chapter's pages (Q/E still turn the chapters); true when a key was used
        bool FeatsKeys()
        {
            if (view == null || !PanelModel.OnFeatsPage(view)) return false;
            if (ReadKey != null && ReadKey.Value != KeyCode.None && PanelModel.FeatsReadOn(view) && Key(ReadKey.Value))   // 0.9.5 Mark read (Chapters/FeatsReadAll.cs); never while typing: Frame stops before the keys then
            {
                if (!SampleMode.On && PanelModel.FeatsReadKey(view, Plugin.FeatsLedger)) Render(true);
                return true;
            }
            var d = Key(KeyCode.A) || Key(KeyCode.LeftArrow) || Button("JoyDPadLeft") ? -1 : Key(KeyCode.D) || Key(KeyCode.RightArrow) || Button("JoyDPadRight") ? 1 : 0;
            if (d == 0) return false;
            if (PanelModel.StepFeat(state, view, d)) { followChoice = true; Render(true); }   // the new choice is scrolled into view (B31: a hover's is not)
            return true;
        }

        // the panel's own feats: noted before each build
        void FeatsRender(PanelInput self)
        {
            if (SampleMode.On) return;
            FeatsTracker.Note(self);
        }

        // 0.9 New feats: after the build, in your own book, the card the detail area shows is opened (PanelModel.FeatsSeen: its tiers seen; a ledger
        // without the per-tier record keeps 0.8.6's rule, the page marks its side)
        void FeatsOpened(PanelInput self, bool own)
        {
            if (SampleMode.On || !own) return;
            PanelModel.FeatsSeen(self, state);
        }

        internal static PanelInput GatherSelf() => Gather();
        internal static List<PanelInput> FellowsNow(PanelInput self) => Instance != null ? Instance.FellowsOf(self) : new List<PanelInput>();
    }

    /// <summary>A feat card: a click shows it in the detail area (the pointer needs a raycast target: the card's ground); as a HoverMark the card
    /// shows that it is pointed at (0.8.1). 0.9: a hover only lights that line, it never chooses the card (PanelModel.FeatPointer).</summary>
    sealed class FeatHover : HoverMark, IPointerClickHandler
    {
        public string Id; public Block Detail;
        /// <summary>0.9.3 the chosen look (PanelUi.FeatChosen): the warm lift (made at LiftAt among the card's children), the gold frame, the name's own ink.</summary>
        public GameObject Lift, Frame; public int LiftAt; public Color NameInk;
        /// <summary>0.9: the card's New mark (null when it has none); hidden once the card is opened.</summary>
        public GameObject NewMark;
        /// <summary>The card's name label (FeatNamesCheck, Dev.SelfCheck).</summary>
        public TextMeshProUGUI Name;
        public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); PanelUi.FeatHovered(this); }
        public void OnPointerClick(PointerEventData e) => PanelUi.FeatClick(this);
    }
}
