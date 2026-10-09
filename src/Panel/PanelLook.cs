using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

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
            Gold = Hex("#e8c27a"), Focus = Hex("#fff3d6"), Accent = Hex("#e8a948"), Track = new Color(0.17f, 0.13f, 0.09f, 1f), Rule = new Color(0.42f, 0.32f, 0.2f, 0.6f);

        /// <summary>The smallest text in the panel, px at 1080p (rubric 4; was 13, raised round 3: Averia has a small x-height, 13 px read as about 11): Label never goes below it. Faint was #8c7b62 (2.9:1 on the character zone), now #b0a086 (4.6:1 there, 7:1 on the plate).</summary>
        public const float MinText = 14f;

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static TMP_FontAsset Body, Title, Plain;   // Plain: a sans for small counts (the serif and Norse faces blur a "11")
        public static Sprite Circle;
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
        // that cannot be read directly), then averaged
        static string Average(Sprite s)
        {
            var tex = s.texture; if (!tex) return null;
            var r = s.textureRect;
            int w = Mathf.Clamp(Mathf.RoundToInt(r.width), 1, 64), h = Mathf.Clamp(Mathf.RoundToInt(r.height), 1, 64);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var before = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(tex, rt, new Vector2(r.width / tex.width, r.height / tex.height), new Vector2(r.x / tex.width, r.y / tex.height));
                RenderTexture.active = rt;
                copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                double sr = 0, sg = 0, sb = 0; int n = 0;
                foreach (var p in copy.GetPixels32())
                {
                    if (p.a <= 127) continue;
                    sr += p.r; sg += p.g; sb += p.b; n++;
                }
                if (n == 0) return null;
                Color.RGBToHSV(new Color((float)(sr / n / 255), (float)(sg / n / 255), (float)(sb / n / 255)), out var hue, out var sat, out var val);
                var bar = Color.HSVToRGB(hue, Mathf.Clamp01(sat * 1.2f + 0.04f), val * 0.85f);
                return "#" + ColorUtility.ToHtmlStringRGB(bar).ToLowerInvariant();
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(rt);
                if (copy) UnityEngine.Object.Destroy(copy);
            }
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
        public static bool GoldLine(string icon) => icon == "vocab:list-feats" || icon == "vocab:feat-waymate" || icon == "vocab:young-creature" || icon == "vocab:lead-rope" || icon == "vocab:cargo-mark";

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
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            if (!(bool)LoadImage.Invoke(null, new object[] { tex, bytes })) return null;
            if (repeatX) { tex.wrapModeU = TextureWrapMode.Repeat; tex.wrapModeV = TextureWrapMode.Clamp; }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

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

        static Sprite Drawn(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

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
}
