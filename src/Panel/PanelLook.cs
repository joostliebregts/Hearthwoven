using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The panel's look, taken from the game itself: Valheim's TMP fonts (Averia Serif Libre for text, Norse for the
    /// wordmark), the game's own sprites for items, pieces, skills and status effects. Hearthwoven art: the title emblems,
    /// Codex Finn's UI kit (ui/) and the visual vocabulary sprites (vocab/); colours from the vocabulary (damage palette,
    /// biome tiles). Whatever cannot be found falls back (an empty place, a fixed colour) and is logged once.
    /// </summary>
    static class PanelLook
    {
        // compact dark translucent menu over the world, warm text, one gold accent
        public static readonly Color Panel = new Color(0.075f, 0.058f, 0.042f, 0.88f), Edge = Hex("#6b5232"), Slot = new Color(0.11f, 0.085f, 0.06f, 0.95f),
            SlotEdge = Hex("#4a3a28"), Text = Hex("#e9dcc4"), Muted = Hex("#b9a688"), Faint = Hex("#b0a086"),
            Gold = Hex("#e8c27a"), Focus = Hex("#fff3d6"), Accent = Hex("#e8a948"), Track = new Color(0.17f, 0.13f, 0.09f, 1f), Rule = PanelWash.Rule.Game;   // Rule: the mirror's --rule as the game draws it (PanelWash.cs)

        /// <summary>The smallest text in the panel, px at 1080p (rubric 4; was 13, raised round 3: Averia has a small x-height, 13 px read as about 11): Label never goes below it. Faint was #8c7b62 (2.9:1 on the character zone), now #b0a086 (4.6:1 there, 7:1 on the plate).
        /// One exception (Joost, 2026-10-10 13:40): a number inside a damage-bar part, digits only and bold, may be MinBarDigits. The floor exists because
        /// Averia's lowercase reads small at 1080p (13 px read like 11); lining digits are cap height, so a bold 12 px digit stands taller than 14 px
        /// lowercase. Only PanelUi.BarDigits draws it (named BarDigitsName, its text BarDigitsText); render-check, its source scan and PanelCheck allow
        /// nothing else under MinText.</summary>
        public const float MinText = 14f;
        public const float MinBarDigits = 12f;
        public const string BarDigitsName = "BarDigits";
        /// <summary>What an in-bar number under MinText may hold: digits and the spaces between thousands, nothing else.</summary>
        public static bool BarDigitsText(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            var digit = false;
            foreach (var c in s) { if (char.IsDigit(c)) digit = true; else if (c != ' ' && c != '\u00A0' && c != '\u2009' && c != '\u202F') return false; }
            return digit;
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static TMP_FontAsset Body, Title, Plain;   // Plain: a sans for small counts (the serif and Norse faces blur a "11")
        public static Sprite Circle;
        /// <summary>The one bar form's part (BAR-FORM.md): a white rounded rectangle, radius 5, nine-sliced, tinted per part; and its
        /// 2 px outline in the same shape (a chosen part).</summary>
        public static Sprite Rounded, RoundedEdge;
        /// <summary>The same shape as a 1 px outline: Compare's period before, a quiet trace beside the solid bar now (0.8.1, Joost).</summary>
        public static Sprite RoundedTrace;
        /// <summary>A feat's tier mark (B30, Joost: each tier its own achievement): a small square, all four corners rounded. Filled: Rounded;
        /// not reached yet: this outline, its rim thick enough to stay 1 px when drawn TierMarkScale times smaller (radius 5 / 2.5 = 2 px).</summary>
        public static Sprite TierMarkEdge;
        /// <summary>0.8.5 a damage part's outline (DamageRowsUi.StoppedPart: What your armor stopped, damage dealt after the foe's armor): the same shape
        /// with its rim 2 px and its corners 2 px when drawn at the damage bars' scale (radius 5 / 2.5), one image where four edges were.</summary>
        public static Sprite DamageEdge;
        /// <summary>0.8 Battle (a foe's outcome, Chapters/BattleFeedUi.cs): a ring and a half-filled ring, drawn at twice the 11 to 13 px they show
        /// at, so the rim stays about 1.5 px. The filled mark is Circle.</summary>
        public static Sprite Ring, HalfRing;
        /// <summary>The "×N" badge's outline (0.8): a pill 20 px tall, fully rounded, its rim about 1 px; nine-sliced to any width.</summary>
        public static Sprite PillEdge;
        /// <summary>0.8 Compare: the change mark's arrow, a small triangle pointing up (turned for down), white, tinted by the label's colour: the
        /// game's text faces may not hold the ▲ glyph (Chapters/CompareModel.cs ChangeParts).</summary>
        public static Sprite Triangle;
        /// <summary>0.8.2 the opens mark (PanelUi.OpensMark): a chevron, drawn rather than typed (the game's text faces may not hold U+203A),
        /// in a box ChevronW x ChevronH; its line runs (1.5, 1.5), (6.5, 7), (1.5, 12.5) from the box's top left, ChevronStroke wide, round
        /// ends and corner, white, tinted by the caller. The preview's opensMark draws the same line (an SVG polyline).</summary>
        public static Sprite Chevron;
        public const float ChevronW = 8, ChevronH = 14, ChevronStroke = 1.8f;
        /// <summary>0.8 layout D+ (PanelUi.Islands): an island's ground, a white rounded rectangle, radius 10, nine-sliced, tinted IslandGround.</summary>
        public static Sprite Island;
        /// <summary>The island's ground: a lighter, greyish wash over the plate. The game blends in linear color space and the preview in sRGB,
        /// so the game takes about 0.4 % where the preview has rgba(255, 240, 210, .04): about (20, 18, 15) on the page's (10, 9, 7) in both. Joost picked
        /// it in game (0.8.2, shade 4 of four tried with the temporary Dev.IslandShade; 4 % showed about (58, 53, 46), too loud). Since 0.8.5 worked
        /// out with every other wash in PanelWash.cs (the same look).</summary>
        public static readonly Color IslandGround = PanelWash.IslandGround.Game;
        /// <summary>0.8.6 the shade under the pinned strip while rows lie above it (StickyHeadUi.cs), darkest at the strip's foot.</summary>
        public static readonly Color StripShade = new Color(0f, 0f, 0f, 0.45f);
        public const float TierMarkScale = 2.5f;
        /// <summary>0.9 feat looks (Chapters/FeatLookUi.cs): the family ground's fade to the right (white, its alpha rising along FeatLookShape.GroundStops),
        /// the family wash from the left (falling along WashStops) and the soft disc behind an icon (FeatLookShape.Disc), all tinted by the caller.</summary>
        public static Sprite GroundFade, GroundFadeDown, WashFade, SoftDisc;
        /// <summary>0.9.1 the soft band of the card's own ground under a feat card's text (Feats.TextBacking Fade): rising over its width to FeatLookShape.Backing.</summary>
        public static Sprite BackingFade;
        /// <summary>0.9.2 the More below cue's band over the Feats grid's foot: nothing at its top, the card's own ground at FeatLookShape.CueGround at its
        /// bottom (FeatLookShape.CueStops; tinted by the caller).</summary>
        public static Sprite CueFade;
        static bool resolved;

        public static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            try
            {
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                Body = fonts.FirstOrDefault(f => f.name.IndexOf("Averia", StringComparison.OrdinalIgnoreCase) >= 0 && f.name.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) < 0)
                       ?? fonts.FirstOrDefault(f => f.name.IndexOf("Averia", StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? InventoryGui.instance?.m_recipeDecription?.font;
                Plain = fonts.FirstOrDefault(f => f.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0 && f.name.IndexOf("Fallback", StringComparison.OrdinalIgnoreCase) < 0)
                        ?? fonts.FirstOrDefault(f => f.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0) ?? Body;
                Title = fonts.FirstOrDefault(f => f.name.IndexOf("Norsebold", StringComparison.OrdinalIgnoreCase) >= 0)
                        ?? fonts.FirstOrDefault(f => f.name.IndexOf("Norse", StringComparison.OrdinalIgnoreCase) >= 0) ?? Body;
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel fonts: " + e.Message); }
            Circle = Drawn(CircleTexture());
            Rounded = RoundedSprite(false); RoundedEdge = RoundedSprite(true); RoundedTrace = RoundedSprite(true, 1f); TierMarkEdge = RoundedSprite(true, 2.5f); DamageEdge = RoundedSprite(true, 5f);
            Ring = Drawn(RingTexture(false)); HalfRing = Drawn(RingTexture(true)); PillEdge = PillSprite();
            Triangle = Drawn(TriangleTexture());
            Chevron = Drawn(ChevronTexture());
            Island = IslandSprite();
            // 0.9.1: each ramp's alpha as the game must draw it to show what the mirror shows (FeatLookShape.Veil, Ink: the book blends in linear space)
            GroundFade = Drawn(RampTexture(FeatLookShape.GroundStops, "hearthwoven-ground-fade", false, FeatLookShape.Veil)); GroundFadeDown = Drawn(RampTexture(FeatLookShape.DownStops, "hearthwoven-ground-fade-down", true, FeatLookShape.Veil));
            WashFade = Drawn(RampTexture(FeatLookShape.WashStops, "hearthwoven-wash-fade", false, a => FeatLookShape.Ink(a * FeatLookShape.Wash)));
            BackingFade = Drawn(RampTexture(FeatLookShape.BackingStops, "hearthwoven-backing-fade", false, a => FeatLookShape.Veil(a * FeatLookShape.Backing)));
            CueFade = Drawn(RampTexture(FeatLookShape.CueStops, "hearthwoven-cue-fade", true, FeatLookShape.Veil));
            SoftDisc = Drawn(SoftDiscTexture());
            Debug.Log("[Hearthwoven] Hearthwoven panel uses font '" + (Body ? Body.name : "TMP default") + "', titles '" + (Title ? Title.name : "TMP default") +
                      "', digits '" + (Plain ? Plain.name : "TMP default") + "'" + (Plain == Body ? " (LiberationSans not found: the panel's own font)" : ""));
        }

        // ---------- icons: the game's own sprites ----------

        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        static Dictionary<string, Sprite> byToken;

        /// <summary>Resolves "item:Bread", "item:$item_bread", "piece:Cart", "skill:Blocking", "status:poison", "title:cook"; null if not found.</summary>
        /// <summary>Forget lookups that found nothing, so the next open tries again (prefabs may not have been loaded yet).</summary>
        /// <summary>The icon references looked up so far that found nothing (Dev.SelfCheck lists them after its page walk).</summary>
        public static List<string> MissingIcons() => icons.Where(kv => kv.Value == null).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

        public static void RetryMissing() { foreach (var k in icons.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) icons.Remove(k); }

        public static Sprite Icon(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            if (icons.TryGetValue(reference, out var s)) return s;
            if (reference.IndexOf('|') > 0)   // "item:CopperOre|vocab:rock-copper": the first picture that exists at runtime, the others as fallbacks
            {
                foreach (var part in reference.Split('|')) { s = Icon(part); if (s) break; }
                icons[reference] = s;
                return s;
            }
            try
            {
                var i = reference.IndexOf(':'); var kind = i < 0 ? "" : reference.Substring(0, i); var key = reference.Substring(i + 1);
                switch (kind)
                {
                    case "item":
                        if (key.StartsWith("$")) { Tokens().TryGetValue(key, out s); break; }
                        var prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(key) : null;
                        var drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
                        if (drop) s = drop.m_itemData.GetIcon();
                        break;
                    case "piece":
                        if (key.StartsWith("$")) { s = GameData.PieceIcon(key); break; }   // the game's placed-piece record keeps the name token
                        var go = ZNetScene.instance ? ZNetScene.instance.GetPrefab(key) : null;
                        var piece = go ? go.GetComponent<Piece>() : null;
                        if (piece) s = piece.m_icon;
                        break;
                    case "skill":
                        s = Player.m_localPlayer?.GetSkills()?.m_skills.FirstOrDefault(d => d.m_skill.ToString() == key)?.m_icon;
                        break;
                    case "status":
                        var hash = StatusHash(key);
                        if (hash != 0 && ObjectDB.instance) s = ObjectDB.instance.GetStatusEffect(hash)?.m_icon;
                        break;
                    case "title":
                        s = RoleIcon(key) ?? Ui("title-" + key);   // the five newer emblems come with the UI kit
                        break;
                    case "ui":
                        s = Ui(key);
                        break;
                    case "vocab":
                        s = Vocab(key);
                        break;
                    case "damage":   // the game draws icons for the elements only; physical types have none
                        s = (StatusHash(key) != 0 ? Icon("status:" + key) : null) ?? Vocab("dmg-" + key);   // the physical types: Codex's damage marks (MISSING-SPRITES Wire)
                        break;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] icon " + reference + ": " + e.Message); s = null; }
            icons[reference] = s;
            return s;
        }

        // the item's display token ("$item_bread") -> its sprite, for the profile's crafted-item list
        static Dictionary<string, Sprite> Tokens()
        {
            if (byToken != null) return byToken;
            byToken = new Dictionary<string, Sprite>();
            if (ObjectDB.instance == null) return byToken;
            foreach (var go in ObjectDB.instance.m_items)
            {
                var drop = go ? go.GetComponent<ItemDrop>() : null;
                if (drop && drop.m_itemData?.m_shared != null && !byToken.ContainsKey(drop.m_itemData.m_shared.m_name)) byToken[drop.m_itemData.m_shared.m_name] = drop.m_itemData.GetIcon();
            }
            return byToken;
        }

        static readonly Dictionary<string, string> iconColours = new Dictionary<string, string>();

        /// <summary>
        /// An item's own colour ("#rrggbb") from its icon, for composition bars: the average of the icon's opaque pixels
        /// (alpha over 0.5), a little darker and more saturated so it reads as a bar. Worked out once per item, the first
        /// time a bar needs it, then kept for the rest of the game; any mod's item works the same way. null when the item
        /// or its icon cannot be read (the model then uses its fallback colours).
        /// </summary>
        public static string IconColour(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            if (iconColours.TryGetValue(token, out var known)) return known;
            Sprite s = null;
            if (token.StartsWith("$")) Tokens().TryGetValue(token, out s);
            if (!s) return null;   // not loaded yet: ask again next time, keep nothing
            string hex = null;
            try { hex = Average(s); }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] icon colour " + token + ": " + e.Message); }
            iconColours[token] = hex;
            return hex;
        }

        // the sprite's rectangle, copied into a small readable texture through a RenderTexture (game icons sit in atlases
        // that cannot be read directly), then averaged. ReadPixels waits for the GPU: the first page with new items paid it per
        // item, so the colours are asked ahead without waiting (WarmColours) and this is only the fallback for one not asked yet
        static string Average(Sprite s)
        {
            var rt = Blitted(s, out var w, out var h); if (!rt) return null;
            var before = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                RenderTexture.active = rt;
                copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                var px = copy.GetPixels32(); var sum = new ColourSum();
                for (int i = 0; i < px.Length; i++) sum.Add(px[i]);
                return sum.Bar();
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(rt);
                if (copy) UnityEngine.Object.Destroy(copy);
            }
        }

        // the icon's rectangle drawn into a small temporary RenderTexture (at most 64 px a side); null when the sprite has no texture
        static RenderTexture Blitted(Sprite s, out int w, out int h)
        {
            w = h = 0;
            var tex = s ? s.texture : null; if (!tex) return null;
            var r = s.textureRect;
            w = Mathf.Clamp(Mathf.RoundToInt(r.width), 1, 64); h = Mathf.Clamp(Mathf.RoundToInt(r.height), 1, 64);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var before = RenderTexture.active;
            try { Graphics.Blit(tex, rt, new Vector2(r.width / tex.width, r.height / tex.height), new Vector2(r.x / tex.width, r.y / tex.height)); }
            catch { RenderTexture.ReleaseTemporary(rt); throw; }
            finally { RenderTexture.active = before; }
            return rt;
        }

        // the average of an icon's opaque pixels (alpha over 0.5) as a bar colour: a little darker and more saturated
        struct ColourSum
        {
            double r, g, b; int n;
            public void Add(Color32 p) { if (p.a <= 127) return; r += p.r; g += p.g; b += p.b; n++; }
            public string Bar()
            {
                if (n == 0) return null;
                Color.RGBToHSV(new Color((float)(r / n / 255), (float)(g / n / 255), (float)(b / n / 255)), out var hue, out var sat, out var val);
                var bar = Color.HSVToRGB(hue, Mathf.Clamp01(sat * 1.2f + 0.04f), val * 0.85f);
                return "#" + ColorUtility.ToHtmlStringRGB(bar).ToLowerInvariant();
            }
        }

        // ---------- warming up (0.8.1, PanelWarm.cs): what the book loads the first time, done before it is needed ----------

        static List<string> colourQueue; static int colourNext, coloursAsked, coloursPending, coloursRead;
        /// <summary>Item colours asked of the GPU so far, answered, and still on their way (Dev.Bench's header).</summary>
        public static string ColourWarmth => coloursRead + " of " + coloursAsked + " read" + (coloursPending > 0 ? ", " + coloursPending + " on the way" : "");

        /// <summary>
        /// Asks the GPU for up to <paramref name="budget"/> more items' icon colours without waiting for it (AsyncGPUReadback: the answer
        /// comes a frame or two later, on the main thread, and fills the same table IconColour reads, by the same sum). true once every item
        /// of the game was asked and answered. Where the GPU cannot answer that way nothing is asked: IconColour reads them as before.
        /// </summary>
        public static bool WarmColours(int budget)
        {
            if (!SystemInfo.supportsAsyncGPUReadback) return true;
            if (colourQueue == null)
            {
                var all = Tokens(); if (all.Count == 0) { byToken = null; return false; }   // ObjectDB not filled yet: look again later
                colourQueue = all.Keys.ToList();
            }
            for (; budget > 0 && colourNext < colourQueue.Count; colourNext++)
            {
                var token = colourQueue[colourNext];
                if (iconColours.ContainsKey(token) || !Tokens().TryGetValue(token, out var s) || !s) continue;
                Ask(token, s); budget--;
            }
            return colourNext >= colourQueue.Count && coloursPending == 0;
        }

        static void Ask(string token, Sprite s)
        {
            var rt = Blitted(s, out _, out _); if (!rt) return;
            coloursAsked++; coloursPending++;
            AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, answer =>
            {
                coloursPending--;
                try
                {
                    if (answer.hasError || iconColours.ContainsKey(token)) return;   // read meanwhile (IconColour, for a page that needed it first)
                    var px = answer.GetData<Color32>(); var sum = new ColourSum();
                    for (int i = 0; i < px.Length; i++) sum.Add(px[i]);
                    iconColours[token] = sum.Bar(); coloursRead++;
                }
                catch (Exception e) { Debug.LogWarning("[Hearthwoven] icon colour " + token + ": " + e.Message); }
                finally { RenderTexture.ReleaseTemporary(rt); }
            });
        }

        /// <summary>Every picture of the panel's own, as Icon references: the UI kit, the visual vocabulary and the title emblems.</summary>
        public static List<string> OwnPictures()
        {
            var refs = new List<string>();
            foreach (var n in kit.Names()) refs.Add("ui:" + n);
            foreach (var n in vocab.Names()) refs.Add("vocab:" + n);
            const string icons = "Hearthwoven.icons.";
            foreach (var name in typeof(PanelLook).Assembly.GetManifestResourceNames())
                if (name.StartsWith(icons, StringComparison.Ordinal) && name.EndsWith(".png", StringComparison.Ordinal)) refs.Add("title:" + name.Substring(icons.Length, name.Length - icons.Length - 4));
            return refs;
        }

        static int StatusHash(string type)
        {
            switch (type)
            {
                case "fire": return SEMan.s_statusEffectBurning;
                case "frost": return SEMan.s_statusEffectFrost;
                case "poison": return SEMan.s_statusEffectPoison;
                case "lightning": return SEMan.s_statusEffectLightning;
                case "spirit": return SEMan.s_statusEffectSpirit;
                default: return 0;
            }
        }

        // ---------- damage colours: the vocabulary palette (VOCABULARY.md, Joost 2026-10-08: genre convention) ----------
        // Physical types are neutral metals told apart by icon and lightness; the elements keep their genre colours.
        // Chop and pickaxe are tool damage (never shown in Battle) and stay in darker iron.

        static readonly Dictionary<string, Color> tones = new Dictionary<string, Color>();
        public static readonly Dictionary<string, string> DamagePalette = new Dictionary<string, string>
        {
            ["blunt"] = "#6b7076", ["slash"] = "#aab6c2", ["pierce"] = "#e8dcbc", ["fire"] = "#e2552a", ["frost"] = "#2f9bff",
            ["lightning"] = "#f2cf2e", ["poison"] = "#3ccf6e", ["spirit"] = "#9b6cf0",
        };
        static readonly Dictionary<string, string> Tool = new Dictionary<string, string> { ["chop"] = "#736f68", ["pickaxe"] = "#5f5b55", ["damage"] = "#9b8f80" };

        public static Color Tone(string tone)
        {
            if (string.IsNullOrEmpty(tone) || tone == "ember") return Accent;
            if (tones.TryGetValue(tone, out var c)) return c;
            c = DamagePalette.TryGetValue(tone, out var hex) || Tool.TryGetValue(tone, out hex) ? Hex(hex) : Accent;
            tones[tone] = c;
            return c;
        }

        // ---------- the vocabulary: biome tiles, the two damage directions, ladder tints, source marks ----------

        /// <summary>The journey order (Meadows to Deep North), the sea apart at the end: key, Valheim's name, tile colour, ink.
        /// Light ink on every tile except Meadows and Plains (VOCABULARY.md sprite picks).</summary>
        public static readonly string[][] Biomes =
        {
            new[] { "meadows", "Meadows", "#7fa04a", "#16110b" }, new[] { "blackforest", "Black Forest", "#3f6b4a", "#f3e7cb" },
            new[] { "swamp", "Swamp", "#6b6a3a", "#f3e7cb" }, new[] { "mountains", "Mountains", "#55657a", "#f3e7cb" },
            new[] { "plains", "Plains", "#c9a64a", "#16110b" }, new[] { "mistlands", "Mistlands", "#7b6a8e", "#f6eefa" },
            new[] { "ashlands", "Ashlands", "#b0442c", "#fff1df" }, new[] { "deepnorth", "Deep North", "#4f7a90", "#f3e7cb" },
            new[] { "ocean", "Ocean", "#3d6f96", "#f3e7cb" },
        };

        /// <summary>"Black Forest", "BlackForest", "blackforest", "Mountain", "AshLands" -> the vocabulary key; null if not a biome.</summary>
        public static string BiomeKey(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var k = new string(id.Where(char.IsLetter).ToArray()).ToLowerInvariant();
            if (k == "mountain") k = "mountains";
            return Biomes.Any(b => b[0] == k) ? k : null;
        }

        public static int BiomeOrder(string id) { var k = BiomeKey(id); return k == null ? int.MaxValue : Array.FindIndex(Biomes, b => b[0] == k); }
        static string[] BiomeRow(string id) { var k = BiomeKey(id); return k == null ? null : Biomes.First(b => b[0] == k); }
        public static string BiomeName(string id) => BiomeRow(id)?[1] ?? id;
        public static Color BiomeTile(string id) => Hex(BiomeRow(id)?[2] ?? "#9b8f80");
        public static Color BiomeInk(string id) => Hex(BiomeRow(id)?[3] ?? "#16110b");
        public static string BiomeEmblem(string id) { var k = BiomeKey(id); return k == null ? null : "biome-" + k; }

        public static readonly Color Dealt = Hex("#e8a948"), Received = Hex("#b8432e"), ReceivedText = Hex("#e09a84"),
            RungDone = Hex("#e8a948"), RungAhead = Hex("#4a3a28"), Rail = Hex("#8c7b62"), Climber = Hex("#f3e2b4");

        /// <summary>The small source mark for a Block.Source tag: the tally board (your character), the hearth flame (this PC),
        /// the paired shields (fellow players). Null for an untagged number.</summary>
        public static Sprite SourceMark(string tag)
        {
            switch (tag)
            {
                case "character": return Vocab("src-stone");
                case "pc": return Vocab("src-hearth");
                case "fellows": return Vocab("src-fellows");
                default: return null;
            }
        }

        // One colour per person in the group (PanelModel.PersonColors hands out distinct indexes, up to eight people).
        static string[] People => PanelModel.PlayerPalette;   // the one player palette table (PanelModel.PlayerPalette)
        public static Color PersonColor(int index) => Hex(People[((index % People.Length) + People.Length) % People.Length]);
        /// <summary>Codex's pure white line masks that are multiplied by the list and feat gold (kit-additions: "multiply by list/feat gold").</summary>
        public static bool GoldLine(string icon) => icon == "vocab:list-feats" || icon == "vocab:feat-waymate" || icon == "vocab:young-creature" || icon == "vocab:lead-rope" || icon == "vocab:cargo-mark" ||
            icon != null && icon.StartsWith("vocab:" + PanelModel.FeatSceneSprite, StringComparison.Ordinal);   // the feat scenes: white line, grey fills (multiplied, the fills go dark)

        /// <summary>The shield's tint: the grey shield sprite (mean about 0.6) multiplied by the player colour lifted by that much,
        /// so the painted face reads in the palette colour itself, not a darker, more saturated one (Joost's F11 run).</summary>
        public static Color ShieldTint(int index) { var c = PersonColor(index); const float lift = 1f / 0.62f; return new Color(Mathf.Clamp01(c.r * lift), Mathf.Clamp01(c.g * lift), Mathf.Clamp01(c.b * lift), 1f); }

        // ---------- Hearthwoven title emblems (embedded) ----------

        // ImageConversion.LoadImage, looked up at runtime: its module is built against netstandard 2.1, this mod targets net462
        static readonly System.Reflection.MethodInfo LoadImage =
            Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });

        static Sprite RoleIcon(string id) => Embedded("Hearthwoven.icons." + id + ".png", Vector4.zero);

        static byte[] Resource(string name)
        {
            using (var stream = typeof(PanelLook).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length]; int read = 0;
                while (read < bytes.Length) { var n = stream.Read(bytes, read, bytes.Length - read); if (n <= 0) break; read += n; }
                return bytes;
            }
        }

        // no mipmaps, clamped, bilinear (kit README); full-rect mesh so 9-slicing works. A repeating fill (wood grain)
        // wraps horizontally and clamps vertically, so Unity's tiled Image repeats it without seams (vocab README).
        static Sprite Embedded(string name, Vector4 border, bool repeatX = false)
        {
            var bytes = Resource(name);
            if (bytes == null || LoadImage == null) return null;
            // 0.9.1 (Joost: the feat pictures "seem very fuzzy"): a feat's scene is drawn at half its texels (112 at 56 px, 72 at 36 px) and smaller still on a
            // panel scaled below 1 or a screen under 1080p; without mipmaps the bilinear filter samples only some texels there and the thin lines shimmer and
            // blur. Its own mip chain (LoadImage builds it), trilinear, a little sharpened
            var mips = name.IndexOf(FeatSceneTexture, StringComparison.Ordinal) >= 0;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mips) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear };
            if (mips) tex.mipMapBias = FeatSceneBias;
            if (!(bool)LoadImage.Invoke(null, new object[] { tex, bytes })) return null;
            if (repeatX) { tex.wrapModeU = TextureWrapMode.Repeat; tex.wrapModeV = TextureWrapMode.Clamp; }
            return Own(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border));
        }

        const string FeatSceneTexture = "feat-scene-";
        const float FeatSceneBias = -0.3f;

        // A sprite set: embedded PNGs plus their manifest (filename -> size, border, type, tint). Only a sliced entry keeps
        // its border; a simple one is placed whole.
        sealed class SpriteSet
        {
            readonly string prefix, manifestName;
            Dictionary<string, Vector4> borders;
            readonly Dictionary<string, Sprite> loaded = new Dictionary<string, Sprite>();
            public SpriteSet(string prefix, string manifest) { this.prefix = prefix; manifestName = manifest; }

            public IEnumerable<string> Names()
            {
                var json = Resource(prefix + manifestName);
                return json != null && MiniJson.Parse(System.Text.Encoding.UTF8.GetString(json)) is Dictionary<string, object> manifest
                    ? manifest.Keys.Select(k => k.Replace(".png", "")).ToList() : new List<string>();
            }

            public Sprite Get(string name, bool repeatX = false)
            {
                if (loaded.TryGetValue(name, out var s)) return s;
                if (borders == null)
                {
                    borders = new Dictionary<string, Vector4>();
                    var json = Resource(prefix + manifestName);
                    if (json != null && MiniJson.Parse(System.Text.Encoding.UTF8.GetString(json)) is Dictionary<string, object> manifest)
                        foreach (var kv in manifest)
                            if (kv.Value is Dictionary<string, object> e && e.TryGetValue("border", out var b) && b is List<object> l && l.Count == 4
                                && !(e.TryGetValue("type", out var t) && t as string == "simple"))
                                // manifest: left, top, right, bottom; Unity: left, bottom, right, top
                                borders[kv.Key.Replace(".png", "")] = new Vector4(Convert.ToSingle(l[0]), Convert.ToSingle(l[3]), Convert.ToSingle(l[2]), Convert.ToSingle(l[1]));
                }
                borders.TryGetValue(name, out var border);
                s = Embedded(prefix + name + ".png", border, repeatX);
                if (s == null) Debug.LogWarning("[Hearthwoven] sprite missing: " + prefix + name);
                loaded[name] = s;
                return s;
            }
        }

        // ---------- the UI kit: original Hearthwoven art by Codex Finn ----------

        static readonly SpriteSet kit = new SpriteSet("Hearthwoven.ui.", "kit.json");
        static readonly SpriteSet vocab = new SpriteSet("Hearthwoven.vocab.", "kit-additions.json");

        /// <summary>A kit sprite by name ("frame", "row-selected", "chapter-battle"), with its 9-slice border from kit.json.</summary>
        public static Sprite Ui(string name) => kit.Get(name);

        /// <summary>A visual-vocabulary sprite by name ("biome-swamp", "ladder-rung", "grain-wood", "src-hearth"), with its
        /// border from kit-additions.json; grain fills come back wrapping horizontally.</summary>
        public static Sprite Vocab(string name) => string.IsNullOrEmpty(name) ? null : vocab.Get(name, repeatX: name.StartsWith("grain-"));

        public static IEnumerable<string> VocabNames() => vocab.Names();

        public static bool Sliced(string name) => Ui(name) != null && Ui(name).border != Vector4.zero;

        public static readonly Color Selected = Hex("#ffcf80");   // chapter icons multiply to amber when chosen (kit.json)

        /// <summary>Every sprite (each with a texture of its own) and material the book makes itself, never the game's: Dev.LiveReload destroys
        /// them when this copy goes (Unload); a player's game keeps them for the whole run, as before.</summary>
        static readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        internal static T Own<T>(T made) where T : UnityEngine.Object { if (made) owned.Add(made); return made; }

        /// <summary>Dev.LiveReload, the old copy (PanelUi.Leave): what Own kept, destroyed with the sprites' textures.</summary>
        internal static void Unload()
        {
            foreach (var o in owned)
            {
                if (!o) continue;
                if (o is Sprite s && s.texture) UnityEngine.Object.Destroy(s.texture);
                UnityEngine.Object.Destroy(o);
            }
            owned.Clear();
        }

        static Sprite Drawn(Texture2D t) => Own(Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect));

        // a rounded rectangle 16 x 16, radius 5 (anti-aliased), border 6 so it slices to any size; edge: only its rim (2 px, or rim)
        static Sprite RoundedSprite(bool edge, float rim = 2f)
        {
            const int s = RoundedShape.Size, b = RoundedShape.Border;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = edge ? "hearthwoven-rounded-edge" : "hearthwoven-rounded", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++) px[y * s + x] = new Color(1, 1, 1, RoundedShape.Alpha(x, y, edge, rim));
            t.SetPixels32(px); t.Apply(false, true);
            return Own(Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b)));
        }

        // an island's ground: 24 px square, radius 10 (anti-aliased), border 11 so it slices to any size
        static Sprite IslandSprite()
        {
            const int s = 24; const float r = 10f;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "hearthwoven-island", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f, cx = Mathf.Clamp(fx, r, s - r), cy = Mathf.Clamp(fy, r, s - r);
                    px[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(r + 0.5f - Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy))));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return Own(Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(11, 11, 11, 11)));
        }

        // a pill's outline: 22 px square, radius 10, rim 1.25 px (anti-aliased), border 10 so it slices to any width at a 20 px height
        static Sprite PillSprite()
        {
            const int s = 22; const float r = 10f, rim = 1.25f;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "hearthwoven-pill-edge", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f, cx = Mathf.Clamp(fx, r + 1, s - r - 1), cy = Mathf.Clamp(fy, r + 1, s - r - 1);
                    float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                    float outer = Mathf.Clamp01(r + 0.5f - d), inner = Mathf.Clamp01(r - rim + 0.5f - d);
                    px[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(outer - inner));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return Own(Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10)));
        }

        // a ring 26 px across with a 3 px rim (anti-aliased); half: its left half filled too (a foe defeated with fellow players)
        static Texture2D RingTexture(bool half)
        {
            const int s = 26; const float rim = 3f;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = half ? "hearthwoven-half-ring" : "hearthwoven-ring", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - s / 2f + 0.5f, dy = y - s / 2f + 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float outer = Mathf.Clamp01(s / 2f - r), inner = Mathf.Clamp01(s / 2f - rim - r);
                    var a = outer - inner;
                    if (half && dx < 0) a = outer;
                    px[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(a));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // an upward triangle 32 x 28 (apex at the top), anti-aliased by 4 x 4 samples per pixel
        static Texture2D TriangleTexture()
        {
            const int w = 32, h = 28;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "hearthwoven-triangle", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float fx = x + (sx + 0.5f) / 4f, fy = y + (sy + 0.5f) / 4f;   // y up: the base at 1, the apex at h - 1
                            float half = (w / 2f - 1) * (1 - (fy - 1) / (h - 2));
                            if (fy >= 1 && fy <= h - 1 && Mathf.Abs(fx - w / 2f) <= half) hit++;
                        }
                    px[y * w + x] = new Color(1, 1, 1, hit / 16f);
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // the opens mark's chevron (Chevron): its line in the ChevronW x ChevronH box, drawn at 4 texels a pixel, anti-aliased over one texel
        static Texture2D ChevronTexture()
        {
            const int k = 4; int w = (int)ChevronW * k, h = (int)ChevronH * k;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "hearthwoven-chevron", wrapMode = TextureWrapMode.Clamp };
            var line = new[] { new Vector2(1.5f, 1.5f), new Vector2(6.5f, 7f), new Vector2(1.5f, 12.5f) };   // from the top left, y down (the preview's polyline)
            float Off(Vector2 p, Vector2 a, Vector2 b) { var ab = b - a; var u = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); return (p - (a + u * ab)).magnitude; }
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2((x + 0.5f) / k, ChevronH - (y + 0.5f) / k);   // texture y runs up
                    var d = Mathf.Min(Off(p, line[0], line[1]), Off(p, line[1], line[2]));
                    px[y * w + x] = new Color(1, 1, 1, Mathf.Clamp01((ChevronStroke / 2 - d) * k + 0.5f));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // 0.9 feat looks: a horizontal ramp, its alpha along the stops (x 0..1 -> alpha), clamped so a stretched Image keeps the ends
        static Texture2D RampTexture((float x, float a)[] stops, string name, bool down = false, Func<float, float> game = null)
        {
            const int n = 128, across = 4;
            int w = down ? across : n, h = down ? n : across;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var a = FeatLookShape.Ramp(stops, down ? 1f - (y + 0.5f) / h : (x + 0.5f) / w);   // down: from the top (texture y runs up)
                    px[y * w + x] = new Color(1, 1, 1, game != null ? game(a) : a);
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // 0.9 feat looks: the soft dark disc behind an icon, full in the middle and gone at its rim (FeatLookShape.Disc)
        static Texture2D SoftDiscTexture()
        {
            const int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "hearthwoven-soft-disc", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - s / 2f + 0.5f, dy = y - s / 2f + 0.5f;
                    px[y * s + x] = new Color(1, 1, 1, FeatLookShape.Veil(FeatLookShape.DiscDark * FeatLookShape.Disc(Mathf.Sqrt(dx * dx + dy * dy) / (s / 2f))));   // 0.9.1: its darkness baked in, as the game blends
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // a soft-edged disc for person markers
        static Texture2D CircleTexture()
        {
            const int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "hearthwoven-circle" };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - s / 2f + 0.5f, dy = y - s / 2f + 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    px[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(s / 2f - r));
                }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }
    }

    /// <summary>
    /// 0.9 feat looks (Chapters/FeatLookUi.cs): the shapes of the family ground's fade, its wash, the soft disc behind an icon and the band under the
    /// text, one place for the game's textures (PanelLook) and the mirror's gradients (panel-preview.html writes the same stops and alphas).
    /// 0.9.1 (Joost: "I really did like all the cards when the pattern was on the background of the full card ... for some reason the legibility in game
    /// sucked"): the ground spans the whole card again as board 9 drew it (ground2(): clear at the left, a quarter at 22 %, most at 58 %, the card's own
    /// ground from 80 %). Why 0.8.93 read badly in game while the mirror passed: the book blends in linear colour space (PanelWash.cs). A dark veil at
    /// alpha a leaves (1 - a)^(1/2.2) of the pattern under it in game, not 1 - a: the fade's 0.7 left 58 % of the hatch under the text instead of 30 %;
    /// and the hatch's lines at 0.67 drew as bright as 0.83. Every alpha here is the mirror's; the game draws Veil(a) for a darkening layer and Ink(a)
    /// for a light one, so both show the same. Under the text a soft band of the card's ground (Backing) and a dark underlay on the letters
    /// (Feats.TextBacking), the pattern's strength a live setting (Feats.PatternStrength). FeatLookTests checks every ink at 4.5:1 or more on the
    /// worst pixel under the text for every family, at the defaults.
    /// </summary>
    public static class FeatLookShape
    {
        /// <summary>The ground's fade over the card's width (board 9's ground2()); the detail area's ground fades down too (DownStops, over its zone).</summary>
        public static readonly (float x, float a)[] GroundStops = { (0f, 0f), (0.22f, 0.25f), (0.58f, 0.85f), (0.8f, 1f), (1f, 1f) };
        public static readonly (float x, float a)[] DownStops = { (0f, 0f), (0.45f, 0.2f), (0.8f, 0.85f), (1f, 1f) };
        public static readonly (float x, float a)[] WashStops = { (0f, 1f), (0.6f, 0f), (1f, 0f) };
        /// <summary>The band under the text: from nothing to Backing over PanelLayout.FeatLook.BackingIn px, then flat to the card's end.</summary>
        public static readonly (float x, float a)[] BackingStops = { (0f, 0f), (1f, 1f) };
        /// <summary>0.9.2 the More below cue's band over the grid's foot (PanelLayout.Feats.CueBand), from its top (0) to its bottom (1): nothing at the
        /// top, the card's own ground at CueGround over the band's lower half, where the words lie (Joost: "more transparent and fade with a gradient
        /// from bottom to transparent at the top").</summary>
        public static readonly (float x, float a)[] CueStops = { (0f, 0f), (0.5f, CueGround), (1f, CueGround) };
        public const float CueGround = 0.85f;
        /// <summary>The family's wash at the card's left, the soft disc's darkness at its middle, the band's darkness under the text (the mirror's alphas).</summary>
        public const float Wash = 0.14f, DiscDark = 0.5f, Backing = 0.7f;
        /// <summary>The hatch tile's own line alpha and the layers it is laid in (0.9: three, Joost "a little bit clearer"): at strength 100 a line shows at
        /// 1 - (1 - 0.31)^3 = 0.67 of the family's colour.</summary>
        public const float TileAlpha = 0.31f; public const int HatchLayers = 3;
        /// <summary>The live settings' defaults (Feats.PatternStrength, Feats.TextBacking): the pattern as the boards draw it, both the band and the shade.</summary>
        public const int PatternDefault = 100;
        public const FeatBacking BackingDefault = FeatBacking.Both;

        /// <summary>The game's alpha for a dark layer the mirror draws at <paramref name="a"/> over a lighter one (linear blending: (1 - a) becomes (1 - a)^2.2).</summary>
        public static float Veil(float a) => 1f - (float)Math.Pow(1f - Clamp01(a), 2.2);
        /// <summary>The game's alpha for a light ink the mirror draws at <paramref name="a"/> over the dark ground (a becomes a^2.2).</summary>
        public static float Ink(float a) => (float)Math.Pow(Clamp01(a), 2.2);
        static float Clamp01(float a) => a < 0 ? 0 : a > 1 ? 1 : a;

        /// <summary>A hatch line's alpha as the mirror shows it at <paramref name="strength"/> (0 to 100).</summary>
        public static float HatchLine(int strength) => 1f - (float)Math.Pow(1f - TileAlpha * Clamp01(strength / 100f), HatchLayers);
        /// <summary>The tint alpha of each of the game's hatch layers so the lines show as HatchLine does in the mirror (through Ink).</summary>
        public static float HatchLayerTint(int strength) => Clamp01((1f - (float)Math.Pow(1f - Ink(HatchLine(strength)), 1.0 / HatchLayers)) / TileAlpha);
        /// <summary>How much of a hatch line shows under text at <paramref name="x"/> (0..1 across the card): its alpha after the fade and the band.</summary>
        public static float UnderText(int strength, bool band, float x) => HatchLine(strength) * (1f - Ramp(GroundStops, x)) * (band ? 1f - Backing : 1f);

        /// <summary>The alpha at <paramref name="x"/> (0..1) along the stops, straight between them.</summary>
        public static float Ramp((float x, float a)[] stops, float x)
        {
            if (x <= stops[0].x) return stops[0].a;
            for (int k = 1; k < stops.Length; k++)
                if (x <= stops[k].x) { var (x0, a0) = stops[k - 1]; var (x1, a1) = stops[k]; return a0 + (a1 - a0) * (x - x0) / (x1 - x0); }
            return stops[stops.Length - 1].a;
        }

        /// <summary>The disc's alpha at <paramref name="r"/> (0 the middle, 1 the rim): a smooth step down, 1 - (3r² - 2r³), nothing beyond the rim.</summary>
        public static float Disc(float r) => r >= 1f ? 0f : 1f - r * r * (3f - 2f * r);
    }

    /// <summary>The rounded sprites' shape (PanelLook.RoundedSprite): a rounded rectangle Size texels square, radius Radius, nine-sliced at Border;
    /// an edge sprite keeps only its rim, rim texels wide. Its own class so the panel tests can read each texel (test-panel/WashTests.cs).</summary>
    public static class RoundedShape
    {
        public const int Size = 16, Border = 6; public const float Radius = 5f;

        /// <summary>The alpha of texel (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public static float Alpha(int x, int y, bool edge, float rim)
        {
            float fx = x + 0.5f, fy = y + 0.5f, a = Inside(fx, fy, Radius, 0);
            if (edge) a = Mathf.Clamp01(a - Inside(fx, fy, Radius - rim, rim));
            return a;
        }

        // how much of the texel at (x, y) lies inside the rounded box inset from the texture edge, from its distance outside the box (negative
        // inside). 0.8.6: inside the box's straight core the distance is to its nearest side, not 0: with no radius left (DamageEdge, rim 5 at
        // radius 5) a 0 there made the inside half covered, and the outline's sliced middle drew its colour at half alpha
        static float Inside(float x, float y, float rad, float inset)
        {
            float lo = inset + rad, hi = Size - inset - rad, cx = Mathf.Clamp(x, lo, hi), cy = Mathf.Clamp(y, lo, hi);
            float d = cx == x && cy == y ? -Mathf.Min(Mathf.Min(x - lo, hi - x), Mathf.Min(y - lo, hi - y)) : Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(0.5f - (d - rad));
        }
    }
}
