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
        // fix4: the card 52 high and 6 between rows, so four whole rows (226) fit the grid's room; the band keeps its own 8 gap
        const float FeatH = 52, FeatHMax = 76, FeatGap = 8, FeatRowGap = 6, FeatDetailH = 184, FeatDetailLeft = 292, FeatRuleH = 20;   // B30: 184 high, rules 20 apart, so every tier, the counting and Who helped fit
        // titles-grid: a grid that scrolls keeps a strip of its own under the cards for the "More below" cue (it sat on a card and hid its text)
        const float FeatCueStrip = 26, FeatCueH = 22;
        static readonly Color FeatRim = new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.85f);
        static readonly Color FeatUnsungInk = new Color(0.62f, 0.6f, 0.58f, 0.55f);   // the emblem of a feat not earned: grey, faint
        static readonly Color FeatWaitingInk = new Color(0.62f, 0.6f, 0.58f, 0.28f);   // the emblem of a feat Hearthwoven does not count yet: fainter still, and a dashed edge
        static readonly Color FeatWaitingRim = new Color(PanelLook.Muted.r, PanelLook.Muted.g, PanelLook.Muted.b, 0.7f);
        static RectTransform featDetail; static float featDetailWidth;
        static float featH = FeatH;   // the cards' height now: 52, taller (up to 76) when a full plate leaves the grid room (fewer rows, more air, no empty band)
        internal static bool plateFull;   // the page's plate keeps the whole room (the Feats chapter, PanelModel.PlateFull): the grid grows into it

        /// <summary>A tier's colour from the model (PanelModel.TierColour, "#rrggbb"); the fallback when none is set.</summary>
        static Color TierInk(string hex, Color fallback) => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>The height the grid may take on a full plate: the room the plate gives, less what is drawn above it (the plate's own line) and the detail area
        /// under it, so the detail area sits at the plate's foot on every Feats page and nothing moves between Earned, Unsung and Together.</summary>
        static float FeatsRoom(RectTransform col)
        {
            if (!plateFull || Instance == null || col != Instance.content || !(col.parent is RectTransform box)) return PanelModel.FeatsGridRoom;
            var vlg = col.GetComponent<VerticalLayoutGroup>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(col);   // the plate's line measured at its real width (a label not laid out yet wraps narrow)
            float above = 0; int n = 0;
            foreach (RectTransform child in col) { if (!child.gameObject.activeSelf) continue; above += LayoutUtility.GetPreferredHeight(child); n++; }
            var gap = vlg ? vlg.spacing : 10f;
            var room = box.rect.height - (vlg ? vlg.padding.top + vlg.padding.bottom : 0) - above - gap * n - gap - FeatDetailH - 2;
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
            var cards = b.Items ?? new List<Block>();
            if (cards.Count == 0) return;
            var per = Mathf.Clamp(Mathf.FloorToInt((Column + FeatGap) / (180 + FeatGap)), 1, 4);
            if (plateFull && cards.Count <= 9) per = Mathf.Min(per, 3);   // a full plate with few cards: three across, so the rows fill the room
            if (b.Tone == PanelModel.TitlesPageId) per = Mathf.Min(per, 3);   // the Titles page: three across, so a title's reason fits its card (B18)
            var w = Mathf.Floor((Column - FeatGap * (per - 1)) / per);
            // the grid sits in an area of its own, at most FeatsGridRoom high: more rows scroll inside it (wheel, soft fade at the edge that has more),
            // so the detail area under it never moves and is never cut at the plate's fold
            var rows = (cards.Count + per - 1) / per;
            var fill = settled ?? FeatsRoom(col);
            featH = plateFull ? Mathf.Clamp(Mathf.Floor((fill - (rows - 1) * FeatRowGap) / rows), FeatH, FeatHMax) : FeatH;
            var natural = rows * featH + (rows - 1) * FeatRowGap;
            var room = plateFull ? fill : Mathf.Min(natural, fill);   // a full plate: the area keeps its whole height, the detail area under it stays at the foot
            var area = Node("FeatsArea", col); Size(area, -1, room);
            featsArea = area; featsRoom = room;
            var scrolls = natural > room + 0.5f && Instance != null;
            // a grid that scrolls: the cards in a view above the cue's own strip, so the cue never covers a card
            var view = area;
            if (scrolls) { view = Node("FeatsView", area); view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one; view.offsetMin = new Vector2(0, FeatCueStrip); view.offsetMax = Vector2.zero; }
            var viewH = scrolls ? room - FeatCueStrip : room;
            view.gameObject.AddComponent<RectMask2D>();
            var grid = Node("Feats", view);
            grid.anchorMin = new Vector2(0, 1); grid.anchorMax = new Vector2(1, 1); grid.pivot = new Vector2(0.5f, 1);
            grid.offsetMin = Vector2.zero; grid.offsetMax = Vector2.zero; grid.sizeDelta = new Vector2(0, natural);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(w, featH); g.spacing = new Vector2(FeatGap, FeatRowGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = per;
            foreach (var c in cards) FeatCard(grid, c, w);
            if (scrolls)
            {
                var scroll = view.gameObject.AddComponent<ScrollRect>();
                scroll.content = grid; scroll.viewport = view; scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 0f; scroll.inertia = false;
                var fadeTone = new Color(PlateColour.r, PlateColour.g, PlateColour.b, 0.9f);
                var s = new Scroll { Rect = scroll, Top = Fade(view, top: true), Bottom = Fade(view, top: false) };
                s.Top.color = s.Bottom.color = fadeTone;
                // the cue (fix4): rows lie below, and how to reach them; in its own strip under the cards (titles-grid), shown only while the bottom fade shows (PanelUi.Fades)
                var cue = Node("MoreCue", area); cue.anchorMin = cue.anchorMax = new Vector2(0.5f, 0); cue.pivot = new Vector2(0.5f, 0); cue.anchoredPosition = new Vector2(0, (FeatCueStrip - FeatCueH) / 2);
                var cueBack = Img(cue, "Back", null, new Color(0.035f, 0.027f, 0.02f, 0.92f)); Edge(cueBack.rectTransform, new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.5f));
                var cueText = Label(cue, PanelModel.FeatsMore, PanelLook.MinText, PanelLook.Gold, align: TextAlignmentOptions.Center); cueText.textWrappingMode = TextWrappingModes.NoWrap;
                cue.sizeDelta = new Vector2(Mathf.Ceil(cueText.preferredWidth) + 22, FeatCueH);
                cueBack.rectTransform.Stretch(); cueText.rectTransform.Stretch();
                s.Cue = cue.gameObject;
                // B31: the grid stays where the player left it when the same page is drawn again (a hover's choice, an update); the chosen feat is
                // brought into view, only as far as its row needs, on a new page or when A/D moved it (PanelModel.GridScroll)
                s.Key = Instance.ScrollPath(view); s.Placed = true;
                Instance.scrollers.Add(s);
                var at = Mathf.Max(0, cards.FindIndex(c => c.Selected)) / per;
                var rowTop = at * (featH + FeatRowGap);
                grid.anchoredPosition = new Vector2(0, PanelModel.GridScroll(Instance.KeptScroll(s.Key), featFollow, rowTop, rowTop + featH, viewH, natural - viewH));
            }
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

        static void FeatCard(RectTransform grid, Block c, float w)
        {
            var earned = c.Tone == PanelModel.FeatTone; var waiting = c.Tone == PanelModel.FeatWaitingTone;
            var card = Kit(grid, c.Title, "meter-track", raycast: true);   // its dark ground and edge
            var tier = TierInk(c.Colour, FeatRim);
            if (earned)   // the tier's colour (bronze, silver, gold): its rim and a bar down its left edge, with the numeral and the notches
            {
                Edge(card.rectTransform, new Color(tier.r, tier.g, tier.b, 0.9f));
                Img(card.transform, "Tier", null, tier).rectTransform.Box(0, 0, 3, featH);
            }
            if (waiting) DashEdge(card.rectTransform, w, featH, FeatWaitingRim);   // a dashed rim: not earnable yet, Hearthwoven does not count it
            var ring = FocusRing(card.rectTransform, focused: c.Selected);   // live-polish: the chosen feat under the keys: the soft rounded focus ring (shown after a key press)
            var notched = c.Count > 1;   // a mark for every tier (B30): reached ones filled, the rest outlines, also before the first tier, so the count shows
            // the emblem and its marks as one block in the card's middle; on the 52 px card the emblem steps down to 33 so the 10 px marks fit under it
            var size = notched ? Mathf.Min(36f, featH - 6 - TierMark - TierMarkGap) : 36f;
            var top = notched ? Mathf.Floor((featH - size - TierMarkGap - TierMark) / 2) : featH <= FeatH ? 5 : Mathf.Floor((featH - 36) / 2);
            var emblem = Marker(card.rectTransform, c.Icon, size, layout: false); emblem.Box(9 + Mathf.Floor((36 - size) / 2), top, size, size);
            if (!earned) foreach (var im in emblem.GetComponentsInChildren<Image>()) im.color = waiting ? FeatWaitingInk : FeatUnsungInk;
            // the name and its small line stacked in the middle of the card at one size: a long name wraps to a second line (never shrinks)
            var stack = Node("Text", card.transform); stack.Box(FeatTextLeft, 2, w - FeatTextLeft - 8, featH - 4);
            var v = stack.gameObject.AddComponent<VerticalLayoutGroup>(); v.childAlignment = TextAnchor.MiddleLeft; v.spacing = 0;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            // the tier reached is said in words too (fix4: the notches alone were small): "Drover II" on a feat with tiers
            // richtext-fix: the numeral styled here from the model's parts, on a label with rich text on, so TMP's ellipsis cuts letters, never a tag
            var tierWord = earned && c.Count > 1 && c.Level > 0 ? " " + Rich.Plain(PanelModel.Numeral((int)c.Level)).Ink(Hex(tier)).Bold() : Rich.Empty;
            var name = RichLabel(stack, "feats", Rich.Plain(c.Title) + tierWord, 15, earned ? PanelLook.Text : PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
            var noted = !string.IsNullOrEmpty(c.Note);
            name.overflowMode = TextOverflowModes.Ellipsis; name.maxVisibleLines = noted && featH < 60 ? 1 : 2;
            if (!string.IsNullOrEmpty(c.Text))
            {
                var when = Label(stack, c.Text, PanelLook.MinText, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic);
                when.textWrappingMode = TextWrappingModes.NoWrap; when.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (noted)   // feats-real: why an Unsung feat reads what it reads ("counting since 8 Oct"), or on an earned one the next tier only ("next: II at 5 000")
            {
                var why = Label(stack, c.Note, PanelLook.MinText, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft);
                why.textWrappingMode = TextWrappingModes.NoWrap; why.overflowMode = TextOverflowModes.Ellipsis;
                if (!string.IsNullOrEmpty(c.Value) && why.preferredWidth > w - FeatTextLeft - 8) why.text = c.Value;   // B30: the next tier's words too long for the card: number and unit only
            }
            if (notched) FeatNotches(card.rectTransform, (int)c.Level, c.Count, 8, top + size + TierMarkGap, waiting);
            var hover = card.gameObject.AddComponent<FeatHover>();
            hover.Id = c.Id; hover.Detail = (c.Items ?? new List<Block>()).FirstOrDefault(); hover.Ring = ring.gameObject;
            featCards.Add(hover);
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
        static readonly Color NotchNext = new Color(0.62f, 0.55f, 0.4f, 0.9f);
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
            var area = Kit(col, "FeatDetail", "meter-track");
            Size(area, -1, FeatDetailH);
            featDetail = area.rectTransform; featDetailWidth = Column;
            FeatDetailFill(area.rectTransform, Column, d);
        }

        /// <summary>Draws one feat into the detail area (again on a hover): emblem, name, tier and the warm line on the left; the rule per
        /// tier (reached ones lit), your progress, how it is counted, the caveat and the moment on the right.</summary>
        static void FeatDetailFill(RectTransform area, float w, Block d)
        {
            Clear(area);
            if (d == null) return;
            var earned = d.Tone == PanelModel.FeatTone; var waiting = d.Tone == PanelModel.FeatWaitingTone;
            var tier = TierInk(d.Colour, FeatRim);
            if (earned) { Edge(area, new Color(tier.r, tier.g, tier.b, 0.9f)); Img(area, "Tier", null, tier).rectTransform.Box(0, 0, 3, FeatDetailH); }
            if (waiting) DashEdge(area, w, FeatDetailH, FeatWaitingRim);
            var emblem = Marker(area, d.Icon, 56, layout: false); emblem.Box(16, 14, 56, 56);
            if (!earned) foreach (var im in emblem.GetComponentsInChildren<Image>()) im.color = waiting ? FeatWaitingInk : FeatUnsungInk;
            var name = Label(area, d.Title, 22, earned ? PanelLook.Text : PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
            name.rectTransform.Box(84, 12, FeatDetailLeft - 88, 30); name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
            // a long name (a group feat's riddle, "What the Deep Woods Hold") steps down to 16 px before it would be cut
            for (var size = 20; size >= 16 && name.preferredWidth > FeatDetailLeft - 88; size -= 2) name.fontSize = size;
            if (!string.IsNullOrEmpty(d.Value))
            {
                var tierLabel = Label(area, d.Value, 14, earned ? tier : PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft, style: earned ? FontStyles.Bold : FontStyles.Normal);
                tierLabel.rectTransform.Box(84, 44, FeatDetailLeft - 92, 22); tierLabel.textWrappingMode = TextWrappingModes.NoWrap; tierLabel.overflowMode = TextOverflowModes.Ellipsis;
            }
            var honours = Label(area, d.Text, 15, PanelLook.Muted, style: FontStyles.Italic);
            // two lines, three for a long one (0.7: the group feats' lines say what the thing is for); the moment sits under it either way
            var honoursH = Mathf.Clamp(Mathf.Ceil(honours.GetPreferredValues(d.Text ?? "", FeatDetailLeft - 32, 0).y), 40, 60);
            honours.rectTransform.Box(16, 74, FeatDetailLeft - 32, honoursH); honours.overflowMode = TextOverflowModes.Ellipsis;

            float x = FeatDetailLeft, rw = w - x - 16, y = 12;
            foreach (var r in (d.Items ?? new List<Block>()).Where(i => i.Kind == "rule"))
            {
                var numeral = !string.IsNullOrEmpty(r.Value);   // a one-off feat has one rule and no numeral
                if (numeral)
                {
                    var n = Label(area, r.Value, 15, r.Selected ? TierInk(r.Colour, PanelLook.Gold) : PanelLook.Faint, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft);
                    n.rectTransform.Box(x, y, 30, FeatRuleH); n.textWrappingMode = TextWrappingModes.NoWrap;
                }
                var t = Label(area, r.Title, 15, r.Selected ? PanelLook.Text : PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
                t.rectTransform.Box(x + (numeral ? 32 : 0), y, rw - (numeral ? 32 : 0), FeatRuleH); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
                y += FeatRuleH;
            }
            var progress = (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "progress");
            y += 4;
            if (progress != null)
            {
                var track = Kit(area, "Track", "meter-track"); track.rectTransform.Box(x, y + 6, rw - 196, 10);
                if (progress.Fraction > 0)
                {
                    var f = Fill(track.transform, "Fill", PanelLook.Accent).rectTransform;
                    f.anchorMin = Vector2.zero; f.anchorMax = new Vector2(Mathf.Clamp01(progress.Fraction), 1); f.offsetMin = new Vector2(2, 2); f.offsetMax = new Vector2(-2, -2);
                }
                var pt = Label(area, progress.Title, 14, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
                pt.rectTransform.Box(x + rw - 186, y, 186, 22); pt.textWrappingMode = TextWrappingModes.NoWrap;
            }
            y += 24;
            var counted = (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "counted");
            if (counted != null)
            {
                var c1 = Label(area, "Counted by", 14, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft); var w1 = Mathf.Ceil(c1.preferredWidth) + 2; c1.rectTransform.Box(x, y, w1, 18); c1.textWrappingMode = TextWrappingModes.NoWrap;
                var c2 = Label(area, counted.Text, 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); c2.rectTransform.Box(x + w1 + 8, y, rw - w1 - 8, 18); c2.textWrappingMode = TextWrappingModes.NoWrap;
            }
            y += 20;
            var caveat = (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "caveat");
            var caveatLines = caveat == null ? 0 : 2;   // no caveat (B37: the counting is said in Counted by): no room kept for one
            if (caveat != null)
            {
                var cv = Label(area, caveat.Text, 14, PanelLook.Muted, style: FontStyles.Italic); cv.rectTransform.Box(x, y, rw, 36); cv.overflowMode = TextOverflowModes.Ellipsis;
                if (cv.GetPreferredValues(caveat.Text ?? "", rw, 0).y <= 20) caveatLines = 1;   // a one-line caveat leaves the second line to who added to it (0.7 group feats)
            }
            y += caveatLines == 0 ? 0 : caveatLines == 1 ? 20 : 38;
            var crew = (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "crew");   // a group feat: who helped, with their parts (B37), or who carried
            if (crew != null && y + 22 <= FeatDetailH - 4) FeatCrew(area, crew, x, y, rw);
            var moment = (d.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "moment");
            if (moment != null)
            {
                // short (Joost 2026-10-09): "Earned 7 Oct", and a quiet "(or earlier)" only when the day is the day it was first seen
                var said = Rich.Plain(moment.Text) + (string.IsNullOrEmpty(moment.Note) ? Rich.Empty : " " + Rich.Plain(moment.Note).Ink(Hex(PanelLook.Muted)).Italic());
                var m = RichLabel(area, "featdetail", said, 15, PanelLook.Text, align: TextAlignmentOptions.TopLeft); var mTop = 74 + honoursH + 5; m.rectTransform.Box(16, mTop, FeatDetailLeft - 32, FeatDetailH - 4 - mTop); m.overflowMode = TextOverflowModes.Ellipsis;   // left column, under what the feat honours
            }
        }

        // B37 (Joost: '"added to by"? what does that mean?'): "Who cooked", then the parts as one bar in the players' colours (when the parts add up to
        // the group's number, Tone "share"), then each person largest first: their colour, name, part and share ("Rowan 610 · 66 %"). A set's parts
        // overlap (each different thing once): their own counts only, no bar, no per cent. Iron for the Forge's crew: shields by name, as before.
        const float CrewBarW = 96, CrewBarH = 8, CrewSwatch = 9;
        static void FeatCrew(RectTransform area, Block crew, float x, float y, float rw)
        {
            var label = Label(area, crew.Title, 14, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft); var lw = Mathf.Ceil(label.preferredWidth) + 2;
            label.rectTransform.Box(x, y, lw, 22); label.textWrappingMode = TextWrappingModes.NoWrap;
            float cx = x + lw + 10, end = x + rw;
            var people = crew.Items ?? new List<Block>();
            var shared = crew.Tone == "share";
            if (shared && people.Count > 1)   // one person's bar would only say 100 %
            {
                float bx = cx, room = CrewBarW - 2 * (people.Count - 1);
                foreach (var p in people)
                {
                    var pw = Mathf.Max(2f, Mathf.Round(room * p.Fraction));
                    if (bx + pw > cx + CrewBarW) pw = Mathf.Max(0f, cx + CrewBarW - bx);
                    var part = Img(area, "Share", PanelLook.Rounded, PersonTint(p.Title)); part.preserveAspect = false;
                    if (part.sprite) { part.type = Image.Type.Sliced; part.pixelsPerUnitMultiplier = 2f * 6f / CrewBarH; }
                    part.rectTransform.Box(bx, y + (22 - CrewBarH) / 2, pw, CrewBarH); bx += pw + 2;
                }
                cx += CrewBarW + 14;
            }
            for (int k = 0; k < people.Count; k++)
            {
                var p = people[k];
                var said = shared ? p.Value + " · " + p.Note : p.Value;
                var left = people.Count - k - 1;
                var more = left > 0 ? Label(area, PanelModel.MoreTitles(left), 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic) : null;
                var moreW = more ? Mathf.Ceil(more.preferredWidth) + 2 : 0f;
                if (more) { more.gameObject.SetActive(false); Destroy(more.gameObject); }
                var name = Label(area, p.Title, 14, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); name.textWrappingMode = TextWrappingModes.NoWrap; var nw = Mathf.Ceil(name.preferredWidth) + 2;
                var value = string.IsNullOrEmpty(said) ? null : Label(area, said, 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
                var vw = value ? Mathf.Ceil(value.preferredWidth) + 2 : 0f;
                var w = (shared || !string.IsNullOrEmpty(p.Value) ? CrewSwatch + 6 : 24) + nw + (value ? 6 + vw : 0);
                if (cx + w + (left > 0 ? 14 + moreW : 0) > end && k > 0)   // no room for this one and the rest: say how many more
                {
                    name.gameObject.SetActive(false); Destroy(name.gameObject); if (value) { value.gameObject.SetActive(false); Destroy(value.gameObject); }
                    var rest = Label(area, PanelModel.MoreTitles(people.Count - k), 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic);
                    rest.textWrappingMode = TextWrappingModes.NoWrap; rest.rectTransform.Box(cx, y, Mathf.Max(0, end - cx), 22);
                    break;
                }
                if (!string.IsNullOrEmpty(p.Value))   // their colour, as in the bar
                {
                    var sw = Img(area, "Who", PanelLook.Rounded, PersonTint(p.Title)); sw.preserveAspect = false;
                    if (sw.sprite) { sw.type = Image.Type.Sliced; sw.pixelsPerUnitMultiplier = PanelLook.TierMarkScale; }
                    sw.rectTransform.Box(cx, y + (22 - CrewSwatch) / 2, CrewSwatch, CrewSwatch); cx += CrewSwatch + 6;
                }
                else { Marker(area, p.Icon, 20, layout: false).Box(cx, y + 1, 20, 20); cx += 24; }   // Carried by: their shield
                name.rectTransform.Box(cx, y, nw, 22); cx += nw;
                if (value) { cx += 6; value.textWrappingMode = TextWrappingModes.NoWrap; value.rectTransform.Box(cx, y, vw, 22); cx += vw; }
                cx += 14;
            }
        }

        /// <summary>A hover or a click on a card (or A/D, which rebuilds the page): the selection ring moves and the detail area shows it.</summary>
        internal static void FeatSelect(FeatHover h)
        {
            if (!h) return;
            foreach (var c in featCards) if (c && c.Ring) Focus(c.Ring, c == h);   // the ring follows the choice; it shows only after a key press (KeyFocus)
            if (featDetail && h.Detail != null) FeatDetailFill(featDetail, featDetailWidth, h.Detail);
            if (Instance != null) Instance.state.FeatSel = h.Id;   // A/D go on from here
        }

        // ---------- Known for (Deeds > Overview) and the band on an owner page ----------

        static void KnownForBlock(RectTransform col, Block b, Func<string, Action> link)
        {
            var row = Line(col, 10); Size(row, -1, 36);
            Keeps(Label(row, b.Title, 15, PanelLook.Gold, style: FontStyles.Bold));
            foreach (var i in b.Items ?? new List<Block>())
            {
                var chip = Kit(row, i.Title, "meter-track", raycast: link != null);
                var tier = TierInk(i.Colour, FeatRim);
                Edge(chip.rectTransform, new Color(tier.r, tier.g, tier.b, 0.9f));
                Layout(chip.gameObject.AddComponent<HorizontalLayoutGroup>(), 6, TextAnchor.MiddleLeft);
                chip.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(8, 12, 4, 4);
                Marker(chip.rectTransform, i.Icon, 26);
                Keeps(Label(chip.transform, i.Title, 15, PanelLook.Text));
                if (!string.IsNullOrEmpty(i.Value)) Keeps(Label(chip.transform, i.Value, 15, tier, style: FontStyles.Bold));
                FeatLink(chip, link, b.Id);
            }
        }

        // fix4: the page's feat and its titles in ONE compact strip at the top of the plate (it was a feat tile and, apart from it, a title pill
        // in the heading row: two things, both shaped like buttons). Flat: a dark ground with a gold edge at its left, no raised border; the feat
        // part opens the Feats page like the tile did, each title (B18: with its reason, quiet after the name) the Titles page with it chosen.
        // One line; the titles drop to a second line only when the first is full.
        const float EarnedLine = 34, EarnedPad = 12, EarnedGap = 18, TitleReasonGap = 8;
        /// <summary>The strip's ground: black at this alpha over the plate (0.8 layout D+, FEEDBACK 5: set apart from the islands by contrast; was 0.30).</summary>
        const float StripGround = 0.55f;

        static void FeatBandBlock(RectTransform col, Block b, Func<string, Action> link)
        {
            var feats = PanelModel.BandFeats(b);
            var titles = PanelModel.BandTitles(b);
            // 0.8 layout D+ (PageHead.cs): the strip is the page's head; with no feat or title it still carries the switch, About these numbers or
            // a fellow's note, and the switch's caption on its left
            var caption = feats.Count == 0 && titles.Count == 0 ? stripSwitch?.Title : null;
            if (feats.Count == 0 && titles.Count == 0 && stripAbout == null && stripSwitch == null && string.IsNullOrEmpty(stripNote)) return;
            // what goes on the line, measured first (the same label settings as drawn): (marker icon, text, colour, style, tag)
            float Measure(Rich text, float size, FontStyles style, float spacing = 0)
            {
                var m = RichLabel(col, "featband", text, size, Color.white, style: style); m.textWrappingMode = TextWrappingModes.NoWrap; m.characterSpacing = spacing;   // measured as drawn: rich text on, so tags take no width; letter spacing counts (the FEAT tag was cut to "FE..." in 0.6.2)
                var w = Mathf.Ceil(m.preferredWidth); m.gameObject.SetActive(false); Destroy(m.gameObject); return w;
            }
            var many = feats.Count > 1;
            var featParts = feats.Select(f => (f, name: Rich.Plain(f.Title) + (string.IsNullOrEmpty(f.Value) ? Rich.Empty : " " + Rich.Plain(f.Value).Ink(Hex(TierInk(f.Colour, PanelLook.Gold))).Bold()))).ToList();
            float featW = featParts.Count == 0 ? 0 : Measure(Rich.Plain(many ? PanelModel.FeatsHeading : PanelModel.FeatLabel), PanelLook.MinText, FontStyles.UpperCase, 10) + 8
                + featParts.Sum(p => 22 + 6 + Measure(p.name, 15, FontStyles.Normal) + EarnedGap) - EarnedGap;
            var moment = !many && feats.Count == 1 ? feats[0].Text ?? "" : "";
            if (moment.Length > 0) featW += 10 + Measure(Rich.Plain(moment), PanelLook.MinText, FontStyles.Italic);
            var titleParts = titles.Select(t => (t, nameW: Measure(Rich.Plain(t.Title), 15, FontStyles.Normal), reasonW: string.IsNullOrEmpty(t.Text) ? 0f : Measure(Rich.Plain(t.Text), PanelLook.MinText, FontStyles.Italic))).ToList();
            float titleW = titleParts.Count == 0 ? 0 : Measure(Rich.Plain(b.Text ?? PanelModel.TitlesWord), PanelLook.MinText, FontStyles.UpperCase, 10) + 8
                + titleParts.Sum(p => 22 + 6 + p.nameW + (p.reasonW > 0 ? TitleReasonGap + p.reasonW : 0) + EarnedGap) - EarnedGap;
            var inner = Column - EarnedPad * 2;
            // B38: the top items in one row: the page's view switch and its "About these numbers" button at the strip's right end, reserved
            // first (PanelModel.StripFit: "Numbers" when it is full, then the feat's note goes; never a second row)
            var about = stripAbout != null && !aboutInStrip ? stripAbout : null;
            var sw = stripSwitch != null && !switchInStrip ? stripSwitch : null;
            float reserve = 0, aboutW = 0; RectTransform cluster = null;
            if (about != null || sw != null || !string.IsNullOrEmpty(stripNote))
            {
                // the feat part and room for "TITLES +2 more" beside it, or the tag and one title
                var titlesTag = titleParts.Count == 0 ? 0 : Measure(Rich.Plain(b.Text ?? PanelModel.TitlesWord), PanelLook.MinText, FontStyles.UpperCase, 10) + 8;
                var lead = featW > 0 ? featW + (titleParts.Count > 0 ? EarnedGap * 2 + 1 + titlesTag + Measure(Rich.Plain(PanelModel.MoreTitles(titleParts.Count)), PanelLook.MinText, FontStyles.Italic) : 0)
                         : titleParts.Count > 0 ? titlesTag + 22 + 6 + 40 : string.IsNullOrEmpty(caption) ? 0 : Mathf.Min(160f, Measure(Rich.Plain(caption), 15, FontStyles.Normal));
                cluster = TopCluster(col, sw, about, false, link, stripNote); aboutW = TopClusterWidth(cluster);
                var noteW = moment.Length > 0 ? 10 + Measure(Rich.Plain(moment), PanelLook.MinText, FontStyles.Italic) : 0;
                var shortW = aboutW;
                if (about != null) { var s = TopCluster(col, sw, about, true, link, stripNote); shortW = TopClusterWidth(s); s.gameObject.SetActive(false); Destroy(s.gameObject); }
                var (shortAbout, dropNote) = PanelModel.StripFit(lead, aboutW, shortW, noteW, inner);
                if (shortAbout && about != null) { cluster.gameObject.SetActive(false); Destroy(cluster.gameObject); cluster = TopCluster(col, sw, about, true, link, stripNote); aboutW = TopClusterWidth(cluster); }
                if (dropNote) { featW -= noteW; moment = ""; }
                reserve = PanelModel.StripAboutGap + aboutW;
                inner -= reserve;
            }
            // the titles do not fit beside the feat: a second line, unless the switch or the About button rides on the right (B38: one row; the
            // titles then end in "+N more" where the room ends)
            var second = cluster == null && featW > 0 && titleW > 0 && featW + EarnedGap * 2 + 1 + titleW > inner;
            var box = Node("Earned", col); Size(box, -1, second ? EarnedLine * 2 : EarnedLine);
            BtRect(box, "Ground", new Color(0f, 0f, 0f, StripGround), 0, 0, Column, second ? EarnedLine * 2 : EarnedLine);   // 0.8 layout D+: a darker band, clearly the page's head
            if (featParts.Count > 0 || titleParts.Count > 0) BtRect(box, "Edge", PanelLook.Gold, 0, 0, 3, second ? EarnedLine * 2 : EarnedLine);   // the gold edge leads the feats and titles; an empty left has none
            float x = EarnedPad, top = 0;
            if (!string.IsNullOrEmpty(caption))   // the switch's caption: what every number on the page is
            {
                var c = BtText(box, caption, x, top, Mathf.Max(0f, Column - EarnedPad - reserve - x), EarnedLine, 15, PanelLook.Muted);
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
                var bt = hit.gameObject.AddComponent<UnityEngine.UI.Button>(); bt.targetGraphic = hit; bt.transition = Selectable.Transition.None; bt.onClick.AddListener(() => click());
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
                    BtText(box, t.Title, x, top, Mathf.Min(nameW + 2, stop - x), EarnedLine, 15, PanelLook.Text); x += Mathf.Min(nameW, stop - x);
                    if (reasonW > 0 && x + TitleReasonGap + 40 <= stop)
                    {
                        x += TitleReasonGap; BtText(box, t.Text, x, top, Mathf.Min(reasonW + 2, stop - x), EarnedLine, PanelLook.MinText, PanelLook.Muted, style: FontStyles.Italic); x += Mathf.Min(reasonW, stop - x);
                    }
                    Hit(t.Id, from, Mathf.Min(x, stop));
                    x += EarnedGap;
                }
            }
        }

        static void FeatLink(Image chip, Func<string, Action> link, string target)
        {
            var click = string.IsNullOrEmpty(target) ? null : link?.Invoke(target);
            if (click == null) return;
            var button = chip.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = chip; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => click());
        }

        // ---------- keys and the render step ----------

        // A/D, the arrows and the D-pad choose the feat on the Feats chapter's pages (Q/E still turn the chapters); true when a key was used
        bool FeatsKeys()
        {
            if (view == null || !PanelModel.OnFeatsPage(view)) return false;
            var d = Key(KeyCode.A) || Key(KeyCode.LeftArrow) || Button("JoyDPadLeft") ? -1 : Key(KeyCode.D) || Key(KeyCode.RightArrow) || Button("JoyDPadRight") ? 1 : 0;
            if (d == 0) return false;
            if (PanelModel.StepFeat(state, view, d)) { followChoice = true; Render(true); }   // the new choice is scrolled into view (B31: a hover's is not)
            return true;
        }

        // the panel's own feats: noted before each build, and seen once their page is on screen in your own book
        void FeatsRender(PanelInput self, bool own)
        {
            if (SampleMode.On) return;
            FeatsTracker.Note(self);
            if (own) PanelModel.FeatsSeen(self, state);   // Earned marks your own tiers seen, Together the group's
        }

        internal static PanelInput GatherSelf() => Gather();
        internal static List<PanelInput> FellowsNow(PanelInput self) => Instance != null ? Instance.FellowsOf(self) : new List<PanelInput>();
    }

    /// <summary>A feat card: a hover or a click shows it in the detail area (the pointer needs a raycast target: the card's ground).</summary>
    sealed class FeatHover : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public string Id; public Block Detail; public GameObject Ring;
        public void OnPointerEnter(PointerEventData e) => PanelUi.FeatSelect(this);
        public void OnPointerClick(PointerEventData e) => PanelUi.FeatSelect(this);
    }
}
