using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The panel's look, taken from the game itself: Valheim's TMP fonts (Averia Serif Libre for text, Norse for the
    /// wordmark), the game's own sprites for items, pieces, skills and status effects, and damage colours derived from
    /// the status icons. Only the title emblems are Hearthwoven art. Whatever cannot be found falls back (an initial,
    /// a fixed colour) and is logged once, so the first real client shows what matched.
    /// </summary>
    static class PanelLook
    {
        // compact dark translucent menu over the world, warm text, one gold accent
        public static readonly Color Panel = new Color(0.075f, 0.058f, 0.042f, 0.88f), Edge = Hex("#6b5232"), Slot = new Color(0.11f, 0.085f, 0.06f, 0.95f),
            SlotEdge = Hex("#4a3a28"), Text = Hex("#e9dcc4"), Muted = Hex("#b9a688"), Faint = Hex("#8c7b62"),
            Gold = Hex("#e8c27a"), Accent = Hex("#e8a948"), Track = new Color(0.17f, 0.13f, 0.09f, 1f), Rule = new Color(0.42f, 0.32f, 0.2f, 0.6f);

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static TMP_FontAsset Body, Title;
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
                Title = fonts.FirstOrDefault(f => f.name.IndexOf("Norsebold", StringComparison.OrdinalIgnoreCase) >= 0)
                        ?? fonts.FirstOrDefault(f => f.name.IndexOf("Norse", StringComparison.OrdinalIgnoreCase) >= 0) ?? Body;
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel fonts: " + e.Message); }
            Circle = Drawn(CircleTexture());
            Debug.Log("[Hearthwoven] Hearthwoven panel uses font '" + (Body ? Body.name : "TMP default") + "', titles '" + (Title ? Title.name : "TMP default") + "'");
        }

        // ---------- icons: the game's own sprites ----------

        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        static Dictionary<string, Sprite> byToken;

        /// <summary>Resolves "item:Bread", "item:$item_bread", "piece:Cart", "skill:Blocking", "status:poison", "title:cook"; null if not found.</summary>
        /// <summary>Forget lookups that found nothing, so the next open tries again (prefabs may not have been loaded yet).</summary>
        public static void RetryMissing() { foreach (var k in icons.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) icons.Remove(k); }

        public static Sprite Icon(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            if (icons.TryGetValue(reference, out var s)) return s;
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

        // ---------- damage colours: the game's status colours, physical types in iron ----------

        static readonly Dictionary<string, Color> tones = new Dictionary<string, Color>();
        static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>
            { ["poison"] = "#6fae3a", ["fire"] = "#e0582a", ["frost"] = "#8ec9e8", ["lightning"] = "#8d7be6", ["spirit"] = "#c9e4e0" };
        // the game draws no colour for physical damage: neutral iron, told apart by lightness
        static readonly Dictionary<string, string> Iron = new Dictionary<string, string>
            { ["slash"] = "#a8a49b", ["pierce"] = "#c4bfb4", ["blunt"] = "#8a867e", ["chop"] = "#736f68", ["pickaxe"] = "#5f5b55", ["damage"] = "#9b8f80" };

        public static Color Tone(string tone)
        {
            if (string.IsNullOrEmpty(tone) || tone == "ember") return Accent;
            if (tones.TryGetValue(tone, out var c)) return c;
            if (Iron.TryGetValue(tone, out var iron)) c = Hex(iron);
            else if (Fallback.ContainsKey(tone))
            {
                var source = "fallback palette";
                var sprite = Icon("status:" + tone);
                if (sprite != null && TryAverage(sprite, out var avg)) { c = avg; source = "game icon '" + sprite.name + "'"; }
                else c = Hex(Fallback[tone]);
                Debug.Log("[Hearthwoven] Hearthwoven " + tone + " colour from " + source);
            }
            else c = Accent;
            tones[tone] = c;
            return c;
        }

        // Average opaque colour of a sprite. Game textures are usually not readable, so copy through a render target once.
        static bool TryAverage(Sprite sprite, out Color color)
        {
            color = Color.clear;
            RenderTexture rt = null; var previous = RenderTexture.active;
            try
            {
                var tex = sprite.texture; var r = sprite.textureRect;
                rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var w = Mathf.Max(1, (int)r.width); var h = Mathf.Max(1, (int)r.height);
                var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
                copy.Apply();
                float sr = 0, sg = 0, sb = 0; int n = 0;
                foreach (var p in copy.GetPixels32()) if (p.a > 128) { sr += p.r; sg += p.g; sb += p.b; n++; }
                UnityEngine.Object.Destroy(copy);
                if (n == 0) return false;
                color = new Color(sr / n / 255f, sg / n / 255f, sb / n / 255f, 1f);
                Color.RGBToHSV(color, out var hh, out var ss, out var vv);   // lift a little: icons carry dark outlines
                color = Color.HSVToRGB(hh, Mathf.Clamp01(ss * 1.15f), Mathf.Clamp01(Mathf.Max(vv, 0.62f)));
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] colour from " + sprite.name + ": " + e.Message); return false; }
            finally { RenderTexture.active = previous; if (rt != null) RenderTexture.ReleaseTemporary(rt); }
        }

        // One colour per person in the group (PanelModel.PersonColors hands out distinct indexes, up to eight people).
        static readonly string[] People = { "#a83a2c", "#3b62a0", "#d8d2c0", "#5f8a3b", "#c08a2e", "#c0508a", "#b07ad8", "#2f8a82" };
        public static Color PersonColor(int index) => Hex(People[((index % People.Length) + People.Length) % People.Length]);

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

        // no mipmaps, clamped, bilinear (kit README); full-rect mesh so 9-slicing works
        static Sprite Embedded(string name, Vector4 border)
        {
            var bytes = Resource(name);
            if (bytes == null || LoadImage == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            if (!(bool)LoadImage.Invoke(null, new object[] { tex, bytes })) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        // ---------- the UI kit: original Hearthwoven art by Codex Finn ----------

        static Dictionary<string, Vector4> borders;
        static readonly Dictionary<string, Sprite> kit = new Dictionary<string, Sprite>();

        /// <summary>A kit sprite by name ("frame", "row-selected", "chapter-battle"), with its 9-slice border from kit.json.</summary>
        public static Sprite Ui(string name)
        {
            if (kit.TryGetValue(name, out var s)) return s;
            if (borders == null)
            {
                borders = new Dictionary<string, Vector4>();
                var json = Resource("Hearthwoven.ui.kit.json");
                if (json != null && MiniJson.Parse(System.Text.Encoding.UTF8.GetString(json)) is Dictionary<string, object> manifest)
                    foreach (var kv in manifest)
                        if (kv.Value is Dictionary<string, object> e && e.TryGetValue("border", out var b) && b is List<object> l && l.Count == 4)
                            // kit.json: left, top, right, bottom; Unity: left, bottom, right, top
                            borders[kv.Key.Replace(".png", "")] = new Vector4(Convert.ToSingle(l[0]), Convert.ToSingle(l[3]), Convert.ToSingle(l[2]), Convert.ToSingle(l[1]));
            }
            borders.TryGetValue(name, out var border);
            s = Embedded("Hearthwoven.ui." + name + ".png", border);
            if (s == null) Debug.LogWarning("[Hearthwoven] UI kit sprite missing: " + name);
            kit[name] = s;
            return s;
        }

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
