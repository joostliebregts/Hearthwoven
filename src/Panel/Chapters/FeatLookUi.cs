using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 feat look (Chapters/FeatLook.cs, work/hearthwoven-0.9/TRACK-featlook.md): the feat card on Feats > Earned, Unsung and Together, a named
    /// variant of FeatsUi.FeatCard (the "feats" block's cards; the grid, the scroll, the New mark, the hover and the one detail area are FeatsUi's):
    /// the family's ground, a large icon on a soft dark disc, a flat tier bar, the family's colour as the left edge, the feat's own name first and one
    /// short line. 0.9.2 (Joost in game: "Let's stick with Look 2 and remove look 1"): look 1's frame and badges are gone.
    /// 0.8.96 (Joost in game: "I would love to actually see the name of the thing"): no family word on a card, the family reads from its color,
    /// hatch and icon; the detail area keeps the word over the name, where it explains the color (FeatLookFamilyLine).
    /// Only the vocabulary's grain-hatch tiles and textures drawn in code (PanelLook.GroundFade, WashFade, SoftDisc, BackingFade); no new art.
    /// 0.9.1 (Joost: "I really did like all the cards when the pattern was on the background of the full card ... maybe it's just a small drop shadow or
    /// like a black fade behind the text, or just slightly more transparency on the pattern"): the family's ground over the whole card again (board 9),
    /// every alpha drawn as the linear-space game needs it to show the mirror's (FeatLookShape), a soft band of the card's ground under the text and a
    /// dark underlay on its letters (Feats.TextBacking), the pattern's strength live (Feats.PatternStrength); the same on Earned, Unsung and Together.
    /// </summary>
    public partial class PanelUi
    {
        static readonly float LkCardMin = PanelLayout.FeatLook.CardMin, LkCardMax = PanelLayout.FeatLook.CardMax;
        // the family's edge, the tier bar's filling part (the darkening layers' alphas live in FeatLookShape)
        static readonly float LkEdge = 0.95f, LkInner = 0.6f;
        const int LkNewNameLines = 2;   // 0.9.2: a New card's name beside its mark may wrap once

        /// <summary>0.9.1 the live settings (PanelUi.BindConfig, section Feats): the pattern's strength (0 to 100) and what lies under the text.</summary>
        internal static ConfigEntry<int> FeatPattern;
        internal static ConfigEntry<FeatBacking> FeatTextBacking;
        static readonly int LkStrongest = 100;
        static int LkStrength => FeatPattern != null ? Mathf.Clamp(FeatPattern.Value, 0, LkStrongest) : FeatLookShape.PatternDefault;
        static FeatBacking LkBacking => FeatTextBacking != null ? FeatTextBacking.Value : FeatLookShape.BackingDefault;

        static void BindFeatLookConfig(ConfigFile config)
        {
            FeatPattern = config.Bind("Feats", "PatternStrength", FeatLookShape.PatternDefault, new ConfigDescription("Test period (0.9.1): how strong the family's pattern shows on the Feats cards and the detail (0: none, 100: as the design boards). Applies at once (Gale or the file).", new AcceptableValueRange<int>(0, LkStrongest)));
            FeatTextBacking = config.Bind("Feats", "TextBacking", FeatLookShape.BackingDefault, "Test period (0.9.1): what lies under the text of the Feats cards and the detail, so it reads over the pattern. None: nothing; Fade: a soft dark band of the card's ground under the text; Shadow: a dark shade on the letters; Both. Applies at once (Gale or the file).");
            FeatPattern.SettingChanged += (_, __) => RefreshSoon();
            FeatTextBacking.SettingChanged += (_, __) => RefreshSoon();
        }
        static readonly Color LkCardGround = new Color(9f / 255f, 9f / 255f, 7f / 255f, 1f);   // ui/meter-track's ground under the fade
        static readonly Color LkDiscInk = new Color(0f, 0f, 0f, 1f);
        static readonly Color LkTrack = PanelWash.FeatLookTrack.Game;   // a tier bar's part not reached (the mirror's rgba(107, 82, 51, .35))
        static readonly float LkLetterSpacing = 4f;   // the family line's small capitals, spaced (TMP em/100)

        /// <summary>One card of the look (FeatsUi.FeatsBlock calls it in FeatCard's place for a grid with Look).</summary>
        static void FeatLookCard(RectTransform grid, Block c, float w)
        {
            var earned = c.Tone == PanelModel.FeatTone; var waiting = c.Tone == PanelModel.FeatWaitingTone;
            var h = featH;
            var card = Kit(grid, c.Title, "meter-track", raycast: true);   // its dark ground and edge, as FeatCard
            var family = TierInk(c.Ground, PanelLook.Muted);
            var size = Mathf.Min(PanelLayout.FeatLook.IconLarge, h - 2 * PanelLayout.FeatLook.IconLeftLarge);
            var ix = PanelLayout.FeatLook.IconLeftLarge + PanelLayout.FeatLook.Edge;
            var tx = ix + size + PanelLayout.FeatLook.IconGap; var tw = w - tx - PanelLayout.FeatLook.TextRight;
            // 0.9.1: the family's ground over the whole card (board 9; 0.8.93 kept it behind the icon), the band under the text column
            var zone = Node("FamilyZone", card.transform); zone.Box(0, 0, w, h);
            FamilyGround(zone, c.Pattern, family, false);
            TextBand(card.rectTransform, tx, w, h);
            var liftAt = card.transform.childCount;   // 0.9.3: the chosen card's warm lift lies here, over the pattern and the band, under the edge, icon and text (FeatsUi.FeatChosen)
            var tier = TierInk(c.Colour, FeatRim);
            Img(card.transform, "FamilyEdge", null, new Color(family.r, family.g, family.b, LkEdge)).rectTransform.Box(0, 0, PanelLayout.FeatLook.Edge, h);
            if (waiting) DashEdge(card.rectTransform, w, h, FeatWaitingRim);
            // the icon on its soft dark disc (never a hard square)
            var iy = Mathf.Floor((h - size) / 2);
            SoftDiscBehind(card.rectTransform, ix, iy, size);
            var emblem = Marker(card.rectTransform, c.Icon, size, layout: false); emblem.Box(ix, iy, size, size);
            if (!earned) foreach (var im in emblem.GetComponentsInChildren<Image>()) im.color = waiting ? FeatWaitingInk : FeatUnsungInk;
            var (newMark, newRoom) = FeatNewMark(card.transform, c, w);
            // the name, the short line, the tier bar: stacked in the card's middle. 0.8.96 (Joost in game, cards showed no words): TMP gives a label no
            // least height, so when the stack's lines asked more than the card the layout squeezed every text below its font's line and TMP's ellipsis
            // dropped them whole.
            // Each text now holds its line box (LkLine: least = wanted height), and the card's lines fit its least height (FeatLookTests)
            var stack = Node("Text", card.transform); stack.Box(tx, 0, tw, h);
            var v = stack.gameObject.AddComponent<VerticalLayoutGroup>(); v.childAlignment = TextAnchor.MiddleLeft; v.spacing = 0;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var tierWord = earned && c.Count > 1 && c.Level > 0 ? " " + Rich.Plain(PanelModel.Numeral((int)c.Level)).Ink(Hex(tier)).Bold() : Rich.Empty;   // "Drover II"
            var name = RichLabel(stack, "feats", Rich.Plain(c.Title) + tierWord, PanelLayout.Text.Small, earned ? PanelLook.Text : PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
            FeatLookShade(name);
            name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis; LkLine(name, PanelLayout.Text.Small);
            // the name keeps clear of the New mark, top right; 0.9.2: every New card keeps its mark until it is chosen (the page no longer opens the newest),
            // so a long name ("Copper Wed to Tin I") takes a second line beside the mark rather than lose its end
            if (newRoom > 0) { name.margin = new Vector4(0, 0, newRoom, 0); name.textWrappingMode = TextWrappingModes.Normal; name.maxVisibleLines = LkNewNameLines; }
            if (!string.IsNullOrEmpty(c.Line))
            {
                var line = Label(stack, c.Line, PanelLook.MinText, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
                FeatLookShade(line);
                line.textWrappingMode = TextWrappingModes.NoWrap; line.overflowMode = TextOverflowModes.Ellipsis; LkLine(line, PanelLook.MinText);
            }
            TierBar(stack, c, Mathf.Min(tw, PanelLayout.FeatLook.BarW));
            var hover = card.gameObject.AddComponent<FeatHover>();
            hover.Id = c.Id; hover.Detail = (c.Items ?? new List<Block>()).FirstOrDefault(); hover.NewMark = newMark; hover.Name = name; hover.NameInk = name.color; hover.LiftAt = liftAt;
            FeatChosen(hover, c.Selected);   // 0.9.3: the gold frame over everything, the warm lift, the name in gold (FeatsUi.FeatChosen)
            featCards.Add(hover);
        }

        /// <summary>A card's one-line text holds its line box in the stack (PanelLayout.LineBox): never squeezed under its font's line, where TMP's
        /// ellipsis would drop it whole.</summary>
        static void LkLine(TextMeshProUGUI t, float size) => Size(t, -1, PanelLayout.LineBox(size));

        /// <summary>The family's ground in its zone (0.9.1: the whole card; the detail area's left part): its hatch tiled in its colour at the pattern's
        /// strength, its colour washed in from the left, and the card's own ground laid over the pattern toward the zone's right end, where nothing of it is
        /// left (FeatLookShape's stops, each alpha as the game must draw it to show the mirror's); <paramref name="down"/>: toward its foot too (the
        /// detail area, whose left column has text under the emblem).</summary>
        static void FamilyGround(RectTransform box, string pattern, Color family, bool down)
        {
            // inside the card's 1 px edge (ui/meter-track's own line stays)
            Img(box, "FamilyWash", PanelLook.WashFade, new Color(family.r, family.g, family.b, 1f)).Inside();   // the wash's alpha is in its texture
            var hatch = VocabName(pattern); var tint = FeatLookShape.HatchLayerTint(LkStrength);
            if (!string.IsNullOrEmpty(hatch) && tint > 0)   // the tile three times over (Joost: "a little bit clearer"), each layer's tint so the lines show as the mirror's
                for (int k = 0; k < FeatLookShape.HatchLayers; k++) { var g = VocabImg(box, "FamilyGround", hatch, new Color(family.r, family.g, family.b, tint)); g.rectTransform.Stretch(); g.rectTransform.offsetMin = Vector2.one; g.rectTransform.offsetMax = -Vector2.one; }
            Img(box, "FamilyFade", PanelLook.GroundFade, LkCardGround).Inside();
            if (down) Img(box, "FamilyFadeDown", PanelLook.GroundFadeDown, LkCardGround).Inside();
        }

        /// <summary>0.9.1 the soft band of the card's own ground under a text column from <paramref name="x"/> to <paramref name="right"/> (Feats.TextBacking
        /// Fade or Both): it rises over BackingIn px before the column (never a hard edge), then lies flat under the text, top to <paramref name="h"/>.</summary>
        static void TextBand(RectTransform box, float x, float right, float h)
        {
            if (!PanelModel.FeatBand(LkBacking) || !PanelLook.BackingFade) return;
            var rin = PanelLayout.FeatLook.BackingIn; var top = PanelLayout.FeatLook.RimThin;
            var ramp = Img(box, "TextBand", PanelLook.BackingFade, LkCardGround); ramp.preserveAspect = false;
            ramp.rectTransform.Box(Mathf.Max(top, x - rin), top, Mathf.Min(rin, x - top), h - 2 * top);
            var flat = Img(box, "TextBand", null, new Color(LkCardGround.r, LkCardGround.g, LkCardGround.b, FeatLookShape.Veil(FeatLookShape.Backing)));
            flat.rectTransform.Box(x, top, Mathf.Max(0, right - x - top), h - 2 * top);
        }

        /// <summary>0.9.1 a dark underlay on a look's text (Feats.TextBacking Shadow or Both): TMP's underlay on a copy of the label's font material, one copy
        /// per material, made once; the shared font materials are never changed. A face whose shader has no underlay keeps its own material.</summary>
        static void FeatLookShade(TextMeshProUGUI t, bool always = false)   // always: whatever the setting (the More below cue's words, FeatsUi.FeatMoreCue)
        {
            if (!t || !(always || PanelModel.FeatShade(LkBacking))) return;
            var src = t.fontSharedMaterial; if (!src) return;
            if (!shadeMaterials.TryGetValue(src, out var m))
            {
                m = null;
                try
                {
                    if (src.HasProperty(ShaderUtilities.ID_UnderlayColor))
                    {
                        m = new Material(src) { name = src.name + " (Hearthwoven shade)" };
                        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, LkShadeInk));
                        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, LkShadeOffset); m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -LkShadeOffset);
                        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, LkShadeDilate); m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, LkShadeSoft);
                    }
                }
                catch (System.Exception e) { m = null; Debug.LogWarning("[Hearthwoven] feat text shade: " + e.Message); }
                shadeMaterials[src] = m;
            }
            if (m) t.fontSharedMaterial = m;
        }
        static readonly Dictionary<Material, Material> shadeMaterials = new Dictionary<Material, Material>();
        // the underlay: black at 85 %, a little down and right, a little wider than the letter, soft (TMP's units: offsets and dilate -1..1, softness 0..1)
        static readonly float LkShadeInk = 0.85f, LkShadeOffset = 0.35f, LkShadeDilate = 0.25f, LkShadeSoft = 0.5f;

        /// <summary>The soft dark disc behind an icon: the icon's size and Halo more, centred on it.</summary>
        static void SoftDiscBehind(RectTransform box, float x, float y, float size)
        {
            var reach = size + PanelLayout.FeatLook.Halo;
            var disc = Img(box, "IconFade", PanelLook.SoftDisc, LkDiscInk); disc.preserveAspect = false;   // its darkness is in its texture (FeatLookShape.DiscDark)
            disc.rectTransform.Box(x + size / 2 - reach / 2, y + size / 2 - reach / 2, reach, reach);
        }

        /// <summary>The flat, wide tier bar: one part per tier, a reached part filled in its colour, the next one filling as far as your number goes
        /// (Fraction), the rest the quiet track.</summary>
        static void TierBar(RectTransform stack, Block c, float bw)
        {
            var row = Node("TierBar", stack);
            var le = row.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = PanelLayout.FeatLook.BarH + PanelLayout.FeatLook.BarTop;
            var n = Mathf.Max(1, c.Count); var reached = (int)c.Level;
            var pw = (bw - PanelLayout.FeatLook.BarGap * (n - 1)) / n;
            for (int k = 0; k < n; k++)
            {
                var x = k * (pw + PanelLayout.FeatLook.BarGap); var top = PanelLayout.FeatLook.BarTop;
                var colour = TierInk(PanelModel.TierColour(k + 1, n), PanelLook.Gold);
                var part = Img(row, "Tier", PanelLook.Rounded, k < reached ? colour : LkTrack); Bar(part); part.rectTransform.Box(x, top, pw, PanelLayout.FeatLook.BarH);
                if (k == reached && c.Fraction > 0)
                {
                    var fill = Img(row, "Filling", PanelLook.Rounded, new Color(colour.r, colour.g, colour.b, LkInner)); Bar(fill);
                    fill.rectTransform.Box(x, top, Mathf.Max(PanelLayout.FeatLook.BarH, pw * Mathf.Clamp01(c.Fraction)), PanelLayout.FeatLook.BarH);
                }
            }
        }

        static void Bar(Image img) { img.preserveAspect = false; if (img.sprite) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2f * RoundedShape.Border / PanelLayout.FeatLook.BarH; } }   // the part's ends rounded, half its height

        /// <summary>The detail area under a look (FeatsUi.FeatDetailFill): the family's ground over its left part (0.9.1: behind the emblem and the name,
        /// fading right and down as a card's does), the band under the name and the family's edge (the disc behind the emblem and the family line over
        /// the name: FeatsUi.FeatDetailFill).</summary>
        static void FeatLookDetail(RectTransform area, Block d, float w, float h, Color tier, bool earned)
        {
            var family = TierInk(d.Ground, PanelLook.Muted);
            var gw = Mathf.Min(w, PanelLayout.FeatLook.DetailGroundW); var gh = Mathf.Min(h, PanelLayout.FeatLook.DetailGroundH);
            var ground = Node("FamilyArea", area); ground.Box(0, 0, gw, gh);
            FamilyGround(ground, d.Pattern, family, true);
            TextBand(area, FeatNameLeft, gw, gh);
            Img(area, "FamilyEdge", null, new Color(family.r, family.g, family.b, LkEdge)).rectTransform.Box(0, 0, PanelLayout.FeatLook.Edge, h);
        }

        /// <summary>The detail's family line over the name (a look's detail only).</summary>
        static void FeatLookFamilyLine(RectTransform area, Block d, float x, float width, bool earned)
        {
            var family = TierInk(d.Ground, PanelLook.Muted);
            var fam = Label(area, (d.Family ?? "").ToUpperInvariant(), PanelLook.MinText, family, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Bold);
            FeatLookShade(fam);
            fam.characterSpacing = LkLetterSpacing; fam.textWrappingMode = TextWrappingModes.NoWrap; fam.overflowMode = TextOverflowModes.Ellipsis;
            fam.rectTransform.Box(x, PanelLayout.FeatLook.DetailFamilyTop, width, PanelLayout.LineBox(PanelLook.MinText));
        }
    }

    public partial class PanelUi
    {
        // ---------- 0.9.1 the in-game proof of legibility (Dev.SelfCheck) ----------
        // The mirror's contrast passed in 0.8.93 and the game still read badly (it blends in linear space): this reads the game's own pixels. At the end of
        // a frame once the look's Feats page settled (after FeatNamesCheck), for up to LegibleCards cards on screen: each text's box read back from the screen,
        // its letters told from what lies behind them by their distance to the text's ink, and the ink's contrast to the brightest tenth of that background
        // (a pattern line's pixels) worked out. "HW-CHECK PASS feat-legibility: Feats/earned (pattern 100, backing Both): text at least 7.9:1 over
        // the pixels behind it (lowest 'Drover' 'line', behind it up to #2a241c); the pattern shows at most 18 % under the text". FAIL under 4.5:1. Once
        // per page a session; off unless Dev.SelfCheck.
        const int LegibleCards = 9;
        // a pixel within InkNear of the text's ink is a letter, one further than BehindFar what lies behind it (between: a letter's soft edge); the
        // worst of what lies behind is its BehindWorst-th least contrasting pixel (a pattern line's); a box under LegibleLeast px is not read
        static readonly float InkNear = 0.16f, BehindFar = 0.3f, BehindWorst = 0.1f, LegibleFloor = 4.5f, LegibleLeast = 4f, LegibleSlack = 4f, RgbSpan = Mathf.Sqrt(3f), Percent = 100f;
        static readonly int Corners = 4, TopRight = 2;
        static readonly WaitForEndOfFrame legibleFrame = new WaitForEndOfFrame();

        void FeatLegibilitySoon(string page) { if (DevCheck.On && featsData != null && featsData.Look > 0) StartCoroutine(FeatLegibility(page)); }

        IEnumerator FeatLegibility(string page)
        {
            yield return legibleFrame;
            try { FeatLegibilityNow(page); }
            catch (System.Exception e) { DevCheck.Book.Say("FAIL", "feat-legibility", page + ": the check could not run: " + e.Message); }
        }

        void FeatLegibilityNow(string page)
        {
            var strength = LkStrength; var backing = LkBacking;
            double worst = double.MaxValue; string worstAt = null, behind = null; float most = 0; int texts = 0;
            foreach (var h in featCards.Where(c => c && c.gameObject.activeInHierarchy).Take(LegibleCards))
            {
                var card = (RectTransform)h.transform; var stack = card.Find("Text") as RectTransform;
                if (!stack) continue;
                var cw = card.rect.width; var tx = stack.anchoredPosition.x - stack.rect.width * stack.pivot.x;
                most = Mathf.Max(most, FeatLookShape.UnderText(strength, PanelModel.FeatBand(backing), cw > 0 ? Mathf.Clamp01(tx / cw) : 0));
                foreach (var t in stack.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    if (!t.enabled || string.IsNullOrEmpty(t.text) || t.transform.parent != stack) continue;   // the name and the line
                    var c = Legible(t, out var bg);
                    if (c <= 0) continue;
                    texts++;
                    if (c < worst) { worst = c; worstAt = "'" + h.Id + "' " + (t == h.Name ? "name" : "line"); behind = bg; }
                }
            }
            if (texts == 0) { DevCheck.Book.Say("FAIL", "feat-legibility", page + ": no card text could be read back from the screen"); return; }
            var ok = worst >= LegibleFloor;
            DevCheck.Book.Say(ok ? "PASS" : "FAIL", "feat-legibility", page + " (pattern " + strength + ", backing " + backing + "): text at least " + worst.ToString("0.0") + ":1 over the pixels behind it (" + texts +
                " texts, lowest " + worstAt + ", behind it up to " + behind + "); the pattern shows at most " + Mathf.RoundToInt(most * Percent) + " % under the text");
        }

        // one text's contrast on screen: its ink against the least contrasting tenth of the pixels in its box that are not its letters; 0 when nothing was read
        static double Legible(TextMeshProUGUI t, out string behind)
        {
            behind = null;
            var corners = new Vector3[Corners]; t.rectTransform.GetWorldCorners(corners);   // a screen-space overlay: world is screen px, from the bottom left
            var canvas = t.canvas; var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), b = RectTransformUtility.WorldToScreenPoint(cam, corners[TopRight]);
            var scale = canvas ? canvas.scaleFactor : 1f;
            var wide = Mathf.Min(b.x - a.x, (t.preferredWidth + LegibleSlack) * scale);   // the letters' width, not the empty rest of the box
            int x0 = Mathf.Clamp(Mathf.RoundToInt(a.x), 0, Screen.width - 1), y0 = Mathf.Clamp(Mathf.RoundToInt(a.y), 0, Screen.height - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(wide), 0, Screen.width - x0), hh = Mathf.Clamp(Mathf.RoundToInt(b.y - a.y), 0, Screen.height - y0);
            if (w < LegibleLeast || hh < LegibleLeast) return 0;
            var tex = new Texture2D(w, hh, TextureFormat.RGB24, false);
            try
            {
                tex.ReadPixels(new Rect(x0, y0, w, hh), 0, 0, false);
                var px = tex.GetPixels32(); var ink = t.color; var inkHex = "#" + ColorUtility.ToHtmlStringRGB(ink);
                var back = new List<(double c, Color32 p)>(); int letters = 0;
                foreach (var p in px)
                {
                    Color q = p; float dr = q.r - ink.r, dg = q.g - ink.g, db = q.b - ink.b; var d = Mathf.Sqrt(dr * dr + dg * dg + db * db) / RgbSpan;
                    if (d < InkNear) { letters++; continue; }
                    if (d < BehindFar) continue;   // a letter's soft edge
                    back.Add((PanelModel.Contrast(inkHex, "#" + ColorUtility.ToHtmlStringRGB(q)), p));
                }
                if (letters == 0 || back.Count == 0) return 0;
                back.Sort((x, y) => x.c.CompareTo(y.c));
                var at = back[Mathf.Clamp((int)(back.Count * BehindWorst), 0, back.Count - 1)];
                behind = "#" + ColorUtility.ToHtmlStringRGB(at.p);
                return at.c;
            }
            finally { Destroy(tex); }
        }
    }

    static class LookRectExtensions
    {
        /// <summary>An image stretched over its parent, its sprite stretched with it (a fade or a wash).</summary>
        public static void Stretched(this Image img) { img.preserveAspect = false; img.rectTransform.Stretch(); }
        /// <summary>The same, inside its parent's 1 px edge.</summary>
        public static void Inside(this Image img) { img.Stretched(); img.rectTransform.offsetMin = Vector2.one; img.rectTransform.offsetMax = -Vector2.one; }
    }
}
