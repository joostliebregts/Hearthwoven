using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The Hearthwoven panel in game: a compact dark menu over the world that draws whatever PanelModel.Build returns.
    /// Built on first open, so a player who never presses the key gets no UI objects at all. Reads only; changes nothing.
    /// While open the character takes no input (PanelHooks), the way chat and the console block it, and the panel reads
    /// its own keys; none of them reach the game while any text field has focus.
    /// </summary>
    public partial class PanelUi : MonoBehaviour
    {
        internal static PanelUi Instance;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyCode> Hotkey, InfoKey, ViewKey, FilterKey;
        internal static ConfigEntry<int> KeyLayout;
        internal static ConfigEntry<float> Scale;
        internal static DateTime? SessionStart;
        static int hiddenFrames = 99;
        // Like the game's own windows: still "visible" for a frame after closing, so the Escape that closed it does not
        // also open the main menu.
        internal static bool Blocking => Instance != null && (Instance.open || hiddenFrames <= 1);
        // Tab closes the book, as the hotkey and Esc do, unless Tab is the filter key itself (a player who set FilterKey to Tab keeps that)
        internal static bool TabCloses => FilterKey == null || FilterKey.Value != KeyCode.Tab;

        /// <summary>
        /// The one-time move to key layout 1 (Tab closes the book, the filter key is K). A config at layout 0 whose filter key is
        /// Tab was Tab by our default, not by a choice: it moves to K. Layout 1 or later is left alone, so a player who sets
        /// FilterKey back to Tab keeps it. Returns the layout and the filter key to store.
        /// </summary>
        public static (int layout, KeyCode filterKey) MigrateFilterKey(int layout, KeyCode filterKey)
        {
            if (layout >= 1) return (layout, filterKey);
            return (1, filterKey == KeyCode.Tab ? KeyCode.K : filterKey);
        }

        internal static void BindConfig(ConfigFile config)
        {
            if (Enabled != null) return;
            Enabled = config.Bind("Panel", "Enabled", true, "Show the Hearthwoven panel on the hotkey.");
            Hotkey = config.Bind("Panel", "Hotkey", KeyCode.H, "Key that opens and closes the Hearthwoven panel. H is unbound in Valheim and in the group's other mods.");
            Scale = config.Bind("Panel", "Scale", 1f, new ConfigDescription("Size of the whole panel: 1.0 fits 1920 x 1080; smaller for small screens, larger for big ones (0.8 to 1.3).", new AcceptableValueRange<float>(0.8f, 1.3f)));
            InfoKey = config.Bind("Panel", "InfoKey", KeyCode.T, "Key that opens and closes the About Hearthwoven page while the panel is open. T: I opens the AdventureBackpacks backpack.");
            ViewKey = config.Bind("Panel", "ViewKey", KeyCode.F, "Key that flips to the next view on pages with a view switch (the chips top right) while the panel is open. F: no mod in the group binds it.");
            FilterKey = config.Bind("Panel", "FilterKey", KeyCode.K, "Key that enters and leaves the filter focus on pages with a filter bar (Deeds > Crafting and Building; Battle: Overview, Damage, Foes) while the panel is open; inside it A/D move along a row, W/S between rows, Enter or Space chooses, Delete clears all. K: Valheim and the other mods on our server do not use it. Tab closes the book while it is open, and then does not open the inventory (with the book shut Tab opens the inventory as always). Set this to Tab to keep Tab for the filters; then Tab does not close the book. Not G: ZenDragon Zen.ModLib binds G to its radial menu.");
            KeyLayout = config.Bind("Panel", "KeyLayout", 0, "Internal: which key layout this config was migrated to (1: Tab closes the book, filter key K). Do not edit.");
            var migrated = MigrateFilterKey(KeyLayout.Value, FilterKey.Value);   // once per config; before KeyClashCheck reads the key
            if (migrated.layout != KeyLayout.Value || migrated.filterKey != FilterKey.Value) { FilterKey.Value = migrated.filterKey; KeyLayout.Value = migrated.layout; }
            BindSnapshotConfig(config);   // Dev: panel snapshots (PanelSnapshot.cs)
            WatchConfig(config);          // live settings: a change in Gale applies while the game runs (ConfigWatch.cs)
            BindSampleConfig(config);     // Dev: the fictional sample for screenshots (PanelSampleUi.cs)
            KeyClashCheck();              // once: is the filter key bound by another mod too? (PanelCheck.cs)
        }

        readonly PanelState state = new PanelState();
        PanelView view;
        bool open;
        internal bool IsOpen => open;
        float nextRefresh;
        string shown, lastPage;
        int playerPage;
        GameObject root;
        RectTransform frame, players, chapters, list, content, headIcon, headRight;
        TextMeshProUGUI owner, heading, headingSince, scope, keys, listTitle;
        Image plateFill, plateEdge;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; if (root) Destroy(root); }

        // ---------- keys ----------

        // a key or pad button the panel reads also turns the focus ring on (focus-visible: the ring shows after a key press, KeyFocus)
        static bool Key(KeyCode k) { var down = ZInput.GetKeyDown(k, false); if (down) SetKeyFocus(true); return down; }
        static bool Button(string b) { var down = ZInput.GetButtonDown(b); if (down) SetKeyFocus(true); return down; }

        // ---------- the focus ring (live-polish, Joost in game: the square cream ring was ugly) ----------

        /// <summary>Focus-visible: true after a key or pad button the panel reads, false again on mouse use (a move of more than 4 px, a
        /// click, the wheel) and when the panel opens. The focus ring is drawn only while it is true.</summary>
        internal static bool KeyFocus;
        Vector2 lastPointer; bool pointerKnown;

        internal static void SetKeyFocus(bool on)
        {
            if (KeyFocus == on) return;
            KeyFocus = on;
            var root = Instance ? Instance.root : null;
            if (root) foreach (var m in root.GetComponentsInChildren<FocusMark>(true)) m.gameObject.SetActive(on && m.Focused);
        }

        /// <summary>Moves a focus ring on or off its box (a feat chosen by hover or A/D): drawn only while KeyFocus holds.</summary>
        internal static void Focus(GameObject ring, bool focused)
        {
            var m = ring ? ring.GetComponent<FocusMark>() : null; if (!m) return;
            m.Focused = focused; ring.SetActive(focused && KeyFocus);
        }

        const string FocusRingName = "FocusRing";

        /// <summary>The keyboard focus on a box (a filter chip, a biome tile, a feat card): one soft gold glow with rounded corners as the
        /// panel's own (vocab focus-ring, sliced 14 px; its line 8 px in), placed so the line runs <paramref name="gap"/> px outside the box.
        /// Shown only while KeyFocus holds.</summary>
        static RectTransform FocusRing(RectTransform box, float gap = 2, bool focused = true)
        {
            var img = VocabImg(box, FocusRingName, "focus-ring", new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.9f));
            var r = img.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(-(8 + gap), -(8 + gap)); r.offsetMax = new Vector2(8 + gap, 8 + gap);
            img.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            img.gameObject.AddComponent<FocusMark>().Focused = focused;
            img.gameObject.SetActive(KeyFocus && focused);
            return r;
        }

        // mouse use hides the ring again: the pointer moved more than 4 px since the last frame
        void PointerWatch()
        {
            var p = (Vector2)ZInput.pointerPosition;
            if (pointerKnown && (p - lastPointer).sqrMagnitude > 16f) SetKeyFocus(false);
            lastPointer = p; pointerKnown = true;
        }

        // someone is typing: chat, console, a sign, a map pin, the build search, or any other mod's text field
        static bool Typing()
        {
            if ((Chat.instance && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible() || Minimap.InTextInput()) return true;
            if (Hud.instance && Hud.instance.m_buildUi != null && Hud.instance.m_buildUi.SearchFieldFocused) return true;
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (!selected) return false;
            var tmp = selected.GetComponent<TMP_InputField>(); if (tmp && tmp.isFocused) return true;
            var field = selected.GetComponent<InputField>(); return field && field.isFocused;
        }

        // Dev.SelfCheck: the panel's per-frame cost (DevCheck.Perf, PerfMeter.cs); with it off no clock is read
        void Update()
        {
            if (!DevCheck.On || snapping) { Frame(); return; }   // a snapshot run renders every page on purpose: left out
            var start = PerfMeter.Now;
            try { Frame(); } finally { DevCheck.Perf.AddPanel(PerfMeter.Now - start); }
        }

        void Frame()
        {
            try
            {
                LiveConfig();   // a changed .cfg (Gale): reload and apply (ConfigWatch.cs)
                if (SnapshotTick()) return;   // Dev: a panel snapshot run owns the panel (PanelSnapshot.cs)
                CheckTick();                  // Dev.SelfCheck: the report key and the filter-key watch (PanelCheck.cs); nothing when off
                if (!open)
                {
                    if (hiddenFrames < 99) hiddenFrames++;
                    if (Enabled.Value && !Typing() && Pressed() && CanOpen()) Open();
                    return;
                }
                if (Typing()) return;
                PointerWatch();   // focus-visible: the mouse hides the focus ring, a key shows it (Key, Button)
                // on the About page, Esc (or B) goes back to the page it was opened from; the hotkey still closes the panel
                if (state.ShowAbout && !Pressed() && (Key(KeyCode.Escape) || Button("JoyButtonB"))) { state.ShowAbout = false; Render(true); return; }
                // in the filter focus Esc leaves it (the page stays), as on the About page; Tab does the same first (unless Tab is the filter key)
                if (PanelModel.FilterAnyOpen(state, view) && !state.ShowAbout && !Pressed() && (Key(KeyCode.Escape) || Button("JoyButtonB") || (TabCloses && Key(KeyCode.Tab)))) { PanelModel.FilterLeave(state, view); Render(true); return; }
                if (Pressed() || Key(KeyCode.Escape) || (TabCloses && Key(KeyCode.Tab)) || Button("JoyButtonB") || !Player.m_localPlayer ||
                    InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || Player.m_localPlayer.IsDead())
                { Close(); return; }
                if (state.FilterRow >= 0 && !state.ShowAbout && FilterKeys()) { }   // the filter focus: A/D, W/S, Enter, Delete and the filter key are its own (FacetModel.cs)
                else if (FilterKey.Value != KeyCode.None && !state.ShowAbout && view != null && Key(FilterKey.Value) && PanelModel.FilterKeyPressed(state, view)) Render(true);
                else if (Key(KeyCode.Backspace)) { if (PanelModel.Back(state)) Render(true); }   // back to the page you came from
                else if (FeatsKeys()) { }   // on the Feats chapter A/D (and the D-pad) choose the feat; Q/E still turn the chapters (Chapters/FeatsUi.cs)
                else if (Button("TabLeft") || Button("JoyTabLeft") || Key(KeyCode.LeftArrow) || Key(KeyCode.A)) { state.ShowAbout = false; PanelModel.StepChapter(state, -1); Render(true); }
                else if (Button("TabRight") || Button("JoyTabRight") || Key(KeyCode.RightArrow) || Key(KeyCode.D)) { state.ShowAbout = false; PanelModel.StepChapter(state, 1); Render(true); }
                else if (Key(KeyCode.W) || Key(KeyCode.UpArrow) || Button("JoyDPadUp")) { PanelModel.StepList(state, view, -1); Render(true); }   // on About: its own list
                else if (Key(KeyCode.S) || Key(KeyCode.DownArrow) || Button("JoyDPadDown")) { PanelModel.StepList(state, view, 1); Render(true); }
                else if (view != null && view.Toggle.Count > 0 && Button("JoyDPadLeft")) { state.TheyReceived = true; Render(true); }
                else if (view != null && view.Toggle.Count > 0 && Button("JoyDPadRight")) { state.TheyReceived = false; Render(true); }
                else if (InfoKey.Value != KeyCode.None && Key(InfoKey.Value)) { state.ShowAbout = !state.ShowAbout; Render(true); }
                else if (view != null && !state.ShowAbout && ViewKey.Value != KeyCode.None && Key(ViewKey.Value)) ViewKeyPressed();
                Wheel();
                if (Time.unscaledTime >= nextRefresh) Render(false);
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel: " + e.Message); Close(); }
        }

        string baseKeys = "";
        public const string ScrollKey = "[Wheel] Scroll";   // shown in the key line only while a page has more below it (the soft fade says where; this says how)

        void LateUpdate()
        {
            if (!DevCheck.On || snapping) { LateFrame(); return; }
            var start = PerfMeter.Now;
            try { LateFrame(); } finally { DevCheck.Perf.AddPanel(PerfMeter.Now - start); }
        }

        void LateFrame()
        {
            // free the cursor while reading; the game takes it back on its own once nothing is open
            if (open && ZInput.IsMouseActive()) { ZCursor.LockState = CursorLockMode.None; ZCursor.Show(); }
            if (root && root.activeSelf && plateRefits > 0 && plateFill && plateFill.enabled) { plateRefits--; CutPlate(); }   // the plate measured again while the page settles
            if (open) foreach (var s in scrollers) { Ease(s); Fades(s); }
            if (open && keys)
            {
                var more = scrollers.Any(s => s.Rect && s.Rect.content && Room(s) > 0f);
                var want = more ? baseKeys + "      " + ScrollKey : baseKeys;
                if (keys.text != want) keys.text = want;
            }
        }

        static bool Pressed() => Hotkey.Value != KeyCode.None && Key(Hotkey.Value);

        static bool CanOpen()
        {
            var p = Player.m_localPlayer;
            return p && !p.IsDead() && !p.InCutscene() && (!Chat.instance || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !TextInput.IsVisible() &&
                   !Menu.IsVisible() && !InventoryGui.IsVisible() && !Minimap.IsOpen() && !StoreGui.IsVisible() && !Hud.IsPieceSelectionVisible() && !Hud.InRadial() &&
                   (!TextViewer.instance || !TextViewer.instance.IsVisible());
        }

        CursorLockMode lockBefore; bool cursorBefore;

        void Open()
        {
            state.ShowAbout = false;   // always reopen on the page, not on About
            KeyFocus = false; pointerKnown = false;   // the hotkey that opened the panel is not a move inside it: no focus ring yet
            PanelLook.Resolve();
            PanelLook.RetryMissing();
            LoadPrefs();   // this character's filter choices (PanelPrefs.cs)
            if (!root) Build();
            ApplyScale();   // Panel.Scale, read each time the panel opens
            root.SetActive(true);
            lockBefore = ZCursor.LockState; cursorBefore = ZCursor.IsRequested;
            open = true; hiddenFrames = 0; shown = null;
            PanelModel.ForgetHistory(state);   // Backspace stays within this opening
            GroupShare.Request();   // fellow players' stats, if you share; rate-limited inside
            Render(true);
        }

        // give the cursor back as it was when the panel opened (playing: locked and hidden)
        void Close()
        {
            var wasOpen = open;
            open = false; hiddenFrames = 0;
            if (root) root.SetActive(false);
            SavePrefs();
            if (!wasOpen) return;
            ZCursor.LockState = lockBefore;
            if (cursorBefore) ZCursor.Show(); else ZCursor.Hide();
        }

        // the view key: the page's view switch, else its biome choice; one log line per press, so a test can tell a key that
        // never arrives (no line) from a page that did not follow (a line, then the old view)
        // the filter focus (the filter key entered it): along a row, between rows, choose, clear all, leave. false when no key of it was pressed.
        bool FilterKeys()
        {
            if (view == null || PanelModel.FilterOf(view) == null) { state.FilterRow = -1; return false; }   // leaves the focus; every page keeps its own bar open or shut
            bool done;
            if (FilterKey.Value != KeyCode.None && Key(FilterKey.Value)) done = PanelModel.FilterKeyPressed(state, view);   // leaves
            else if (Key(KeyCode.A) || Key(KeyCode.LeftArrow) || Button("JoyDPadLeft")) done = PanelModel.FilterMove(state, view, -1);
            else if (Key(KeyCode.D) || Key(KeyCode.RightArrow) || Button("JoyDPadRight")) done = PanelModel.FilterMove(state, view, 1);
            else if (Key(KeyCode.W) || Key(KeyCode.UpArrow) || Button("JoyDPadUp")) done = PanelModel.FilterRowStep(state, view, -1);
            else if (Key(KeyCode.S) || Key(KeyCode.DownArrow) || Button("JoyDPadDown")) done = PanelModel.FilterRowStep(state, view, 1);
            else if (Key(KeyCode.Return) || Key(KeyCode.KeypadEnter) || Key(KeyCode.Space) || Button("JoyButtonA")) done = PanelModel.FilterToggle(state, view);
            else if (Key(KeyCode.Delete)) done = PanelModel.FilterClear(state, view);
            else if (Button("TabLeft") || Button("JoyTabLeft") || Button("TabRight") || Button("JoyTabRight") || Key(KeyCode.Backspace) || Key(KeyCode.Tab)) { state.FilterRow = -1; return false; }   // leaves the focus, the rows stay open as the player left them   // Q/E and Backspace do their usual thing and leave the focus
            else return false;
            if (done) Render(true);
            return done;
        }

        void ViewKeyPressed()
        {
            if (PanelModel.StepView(state, view, 1))
            {
                Render(true);
                var sw = PanelModel.SwitchOf(view);
                Debug.Log("[Hearthwoven] view key: " + (sw != null ? sw.Id + " = " + sw.Items.FirstOrDefault(x => x.Selected)?.Id : "window = " + state.Window));   // vf-fix1's per-press line
            }
            else Debug.Log("[Hearthwoven] view key: nothing to flip on " + view.Active + "/" + view.Page);
        }

        void ApplyScale() { if (frame) frame.localScale = Vector3.one * PanelModel.PanelScale(Scale?.Value ?? 1f); }

        // ---------- whose book (Joost: it was not obvious you had left your own) ----------

        RectTransform bookChip;

        // "Edda's book" in Edda's colour at x in the heading row; returns its width
        float BookChip(RectTransform row, string name, float x)
        {
            var tint = PersonTint(name);
            var bg = Img(row, "Book", null, new Color(tint.r, tint.g, tint.b, 0.22f));
            Edge(bg.rectTransform, new Color(tint.r, tint.g, tint.b, 0.85f));
            var light = Color.Lerp(tint, Color.white, 0.45f);
            var t = Label(bg.transform, name + "'s book", 15, light, style: FontStyles.Bold, align: TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch();
            var w = Mathf.Ceil(t.preferredWidth) + 20;
            bg.rectTransform.Box(x, 5, w, 26);
            bookChip = bg.rectTransform;
            return w;
        }

        // a chosen fellow's chip: their colour over the kit's row, with a soft edge of it
        static void Glow(GameObject chip, Color c)
        {
            var img = chip ? chip.GetComponent<Image>() : null; if (!img) return;
            img.color = Color.Lerp(Color.white, c, 0.55f);
            var o = chip.AddComponent<Outline>(); o.effectColor = new Color(c.r, c.g, c.b, 0.9f); o.effectDistance = new Vector2(2, -2);
            var o2 = chip.AddComponent<Outline>(); o2.effectColor = new Color(c.r, c.g, c.b, 0.45f); o2.effectDistance = new Vector2(-2, 2);
        }

        // ---------- keycaps (A2): small quiet keys where they act; the game's own binding for the tab keys ----------

        Image keyQ, keyE, keyW, keyS;
        float listRow = RowH, listGap = 8;
        const float KeyRoom = 40;   // under the list: the S keycap and its margin
        static readonly Color ListIconGold = new Color(0.91f, 0.76f, 0.48f);   // #e8c27a: the warm gold of the title emblems

        static Image Keycap(RectTransform parent, string key)
        {
            // quiet (Joost's F11 run: an Outline copied the whole quad in gold under the 35 % fill: light tan squares): a dark fill and
            // a 1 px gold edge of four thin rects, the letter gold at 60 %
            var cap = Img(parent, "Key " + key, null, new Color(0f, 0f, 0f, 0.35f));
            Edge(cap.rectTransform, new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.35f));
            var t = Label(cap.transform, key, 13, new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.6f), style: FontStyles.Bold, align: TextAlignmentOptions.Center);
            t.rectTransform.Stretch(); t.textWrappingMode = TextWrappingModes.NoWrap; t.name = "Letter";
            return cap;
        }

        /// <summary>A keycap's width: square for one letter, wider for a word key ("Tab": 14 px bold needs about 9 px a letter).</summary>
        internal static float KeycapWidth(string key, float height) => string.IsNullOrEmpty(key) || key.Length <= 1 ? height : Mathf.Ceil(height + (key.Length - 1) * 9f);

        // a 1 px edge of four thin rects inside a box (an Outline effect would copy the whole quad under a translucent fill)
        static void Edge(RectTransform box, Color c)
        {
            void Side(string n, Vector2 min, Vector2 max, Vector2 omin, Vector2 omax)
            { var r = Img(box, n, null, c).rectTransform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = omin; r.offsetMax = omax; }
            Side("Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -1), Vector2.zero);
            Side("Bottom", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            Side("Left", Vector2.zero, new Vector2(0, 1), new Vector2(0, 1), new Vector2(1, -1));
            Side("Right", new Vector2(1, 0), Vector2.one, new Vector2(-1, 1), new Vector2(0, -1));
        }

        static void KeyLetter(Image cap, string key) { var t = cap ? cap.GetComponentInChildren<TextMeshProUGUI>() : null; if (t) t.text = key; }

        static string Bound(string button, string fallback)
        {
            try { var k = ZInput.instance?.GetBoundKeyString(button, true); return string.IsNullOrEmpty(k) ? fallback : k.Length > 3 ? k.Substring(0, 3) : k; }
            catch { return fallback; }
        }

        // W right of the list title, S just under the last list row (at the list's foot when it scrolls); Q and E from the game's tab keys
        void PlaceListKeys(PanelView v)
        {
            KeyLetter(keyQ, Bound("TabLeft", "Q")); KeyLetter(keyE, Bound("TabRight", "E"));
            var many = v.List.Count > 1; keyW.gameObject.SetActive(many); keyS.gameObject.SetActive(many);
            if (!many) return;
            keyW.rectTransform.Box(Inset + Mathf.Min(ListW - 34, Mathf.Ceil(listTitle.preferredWidth) + 10), HeadTop + 3, 24, 24);
            var below = ListTop + 2 + v.List.Count * (listRow + listGap) + 4;
            keyS.rectTransform.Box(Inset + (ListW - 24) / 2, Mathf.Min(below, H - Foot - 26), 24, 24);
        }

        // a click runs outside Update: same safety net
        Action Safe(Action a) => () => { try { a(); } catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel click: " + e.Message); Close(); } };

        // ---------- data ----------

        static readonly Dictionary<string, string> names = new Dictionary<string, string>();

        // prefab name or $token -> the game's localized name
        static string Localized(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (names.TryGetValue(key, out var n)) return n;
            try
            {
                string token = key.StartsWith("$") ? key : null;
                if (token == null)
                {
                    var go = ZNetScene.instance ? ZNetScene.instance.GetPrefab(key) : null;
                    if (go)
                    {
                        var ch = go.GetComponent<Character>(); if (ch) token = ch.m_name;
                        var pc = go.GetComponent<Piece>(); if (token == null && pc) token = pc.m_name;
                        var r5 = go.GetComponent<MineRock5>(); if (token == null && r5) token = r5.m_name;
                        var r = go.GetComponent<MineRock>(); if (token == null && r) token = r.m_name;
                    }
                    if (token == null)
                    {
                        var item = go ? go : ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(key) : null;
                        var drop = item ? item.GetComponent<ItemDrop>() : null;
                        if (drop) token = drop.m_itemData.m_shared.m_name;
                    }
                }
                n = token != null && Localization.instance != null ? Localization.instance.Localize(token) : null;
                if (n != null && n.StartsWith("[")) n = null;   // the game marks unknown tokens with brackets
            }
            catch { n = null; }
            names[key] = n;
            return n;
        }

        // fellow players' snapshots, parsed once per received copy
        static readonly Dictionary<string, KeyValuePair<string, PanelInput>> parsed = new Dictionary<string, KeyValuePair<string, PanelInput>>();

        // name = the fellow's shown label (GroupShare.Fellows): the name, or "Rowan (2)" when two people share it; the copy is found by key
        PanelInput Fellow(string name, PanelInput self)
        {
            if (SampleMode.On) return SampleFellow(name, self);   // Dev.SampleData (PanelSampleUi.cs)
            var key = string.IsNullOrEmpty(name) || !GroupShare.Sharing() ? null : GroupShare.Fellows.KeyOfLabel(name, self.PlayerName);
            if (key == null || !GroupShare.Group.TryGetValue(key, out var json)) return null;
            if (!parsed.TryGetValue(key, out var hit) || !string.Equals(hit.Key, json, StringComparison.Ordinal))   // the server resends unchanged copies
                parsed[key] = hit = new KeyValuePair<string, PanelInput>(json, PanelInput.FromSnapshot(json));
            var other = hit.Value;
            if (other == null) return null;
            other.PlayerName = name;   // the label: chips, colours and pages follow this person, not whoever else has the name
            other.NowUtc = self.NowUtc; other.DisplayName = Localized; other.PlayerNames = self.PlayerNames; other.ViewerName = self.PlayerName;
            other.ItemKind = GameData.ItemKind; other.GatherKind = GameData.GatherKind; other.PieceKind = GameData.PieceKind; other.ItemToken = GameData.ItemToken; other.StationDish = GameData.StationDish; other.DishType = GameData.DishType; other.DishBoost = GameData.DishBoost;
            other.Foe = BattleGame.Foe; other.Arrows = BattleGame.Arrows;
            other.CropOf = GameData.CropOf; other.ItemType = GameData.ItemType; other.MainMaterial = GameData.MainMaterial; other.PieceTab = GameData.PieceTab; other.PieceMaterial = GameData.PieceMaterial;
            other.Book = GroupShare.BookOf(key, GroupShare.Fellows.NameOf(key));   // 0.6: their part of the server's book, which came with the group list
            return other;
        }

        static PanelInput Gather()
        {
            if (SampleMode.On) return SampleSelf(); else sample = null;   // Dev.SampleData: the fictional sample (PanelSampleUi.cs)
            var input = new PanelInput
            {
                NowUtc = DateTime.UtcNow, SessionStartUtc = SessionStart,
                Session = Plugin.Session, Events = Plugin.EventsSinceInstall, Log = Plugin.Log, DamageSinceInstall = Plugin.DamageSinceInstall, BiomeSinceInstall = Plugin.BiomeSinceInstall, BiomeFromUtc = Plugin.BiomeFromUtc, DisplayName = Localized,
                SessionOnly = Plugin.Events, History = Plugin.History, Pending = Plugin.PendingDay(),   // the day windows (HISTORY-06.md): the saved days plus what this session counted since the last save
                ItemKind = GameData.ItemKind, GatherKind = GameData.GatherKind, PieceKind = GameData.PieceKind, ItemColour = PanelLook.IconColour,
                Foe = BattleGame.Foe, Arrows = BattleGame.Arrows,
                CropOf = GameData.CropOf, ItemType = GameData.ItemType, MainMaterial = GameData.MainMaterial, PieceTab = GameData.PieceTab, PieceMaterial = GameData.PieceMaterial, ItemToken = GameData.ItemToken, StationDish = GameData.StationDish, DishType = GameData.DishType, DishBoost = GameData.DishBoost,
                PlayerNames = new Dictionary<long, string>(),
                Book = GroupShare.Sharing() ? GroupShare.OwnBook : null,   // 0.6: your part of the server's book (cargo loaded and unloaded, born near), while you share
                Solo = ZNet.IsSinglePlayer,   // singleplayer: one line where fellow players would be (PanelModel.SoloNote)
            };
            var profile = Game.instance ? Game.instance.GetPlayerProfile() : null;
            if (profile != null)
            {
                input.PlayerName = profile.GetName();
                input.PlayerId = profile.GetPlayerID();
                input.CharacterMade = profile.m_dateCreated; input.InstalledUtc = Plugin.InstalledUtc;   // the zones' two dates (ZonesModel.cs)
                var stats = profile.m_playerStats;
                // slot 0 only: the raw totals (the other slots overlap, see Snapshot.cs)
                if (stats != null && stats.Length > 0 && stats[0] != null)
                {
                    input.Character = stats[0].m_stats.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
                    input.ItemsCrafted = stats[0].m_itemCraftStats;
                    input.PiecesPlaced = stats[0].m_piecesPlacedStats;
                    input.ItemsPickedUp = stats[0].m_itemPickupStats;
                    input.Baseline = Plugin.Baseline;
                    input.ExactAtBaseline = Plugin.ExactAtBaseline;
                    input.BaselineAt = Plugin.BaselineAt;
                    input.Harvested = stats[0].m_pickableStats;
                    if (stats[0].m_enemyStats != null && stats[0].m_enemyStats.Length > 0) input.EnemyKills = stats[0].m_enemyStats[0];
                }
            }
            var skills = Player.m_localPlayer ? Player.m_localPlayer.GetSkills()?.GetSkillList() : null;
            if (skills != null)
            {
                input.SkillLevels = skills.ToDictionary(s => s.m_info.m_skill.ToString(), s => s.m_level);
                input.SkillProgress = skills.ToDictionary(s => s.m_info.m_skill.ToString(), s => s.GetLevelPercentage());
            }
            foreach (var p in Player.GetAllPlayers())
                if (p) input.PlayerNames[p.GetPlayerID()] = p.GetPlayerName();
            input.KnownBiomes = KnownBiomes(Player.m_localPlayer);
            input.RecipeKnown = t => Player.m_localPlayer && !string.IsNullOrEmpty(t) && Player.m_localPlayer.IsRecipeKnown(t);   // Best arrow: the recipes your character knows
            input.Feats = Plugin.FeatsLedger;   // the feats earned on this PC and their counters (FeatsLedger); null until the local totals load
            return input;
        }

        static readonly System.Reflection.FieldInfo knownBiome = HarmonyLib.AccessTools.Field(typeof(Player), "m_knownBiome");
        static readonly Heightmap.Biome[] journey = { Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain,
            Heightmap.Biome.Plains, Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth, Heightmap.Biome.Ocean };

        // the biomes this character found, from the game's own record (Player.m_knownBiome, the set behind "new biome
        // discovered"). The game keeps names there: "$biome_meadows" in older characters, the shown name (with a variant's
        // prefix or suffix) in newer ones; each maps back to its Heightmap.Biome. Reads only; null when it cannot be read.
        static List<string> KnownBiomes(Player p)
        {
            try
            {
                if (!p || knownBiome == null || !(knownBiome.GetValue(p) is IEnumerable<string> names)) return null;
                var list = names.Where(n => !string.IsNullOrEmpty(n)).ToList();
                var found = new List<string>();
                foreach (var b in journey)
                {
                    var token = "$biome_" + b.ToString().ToLowerInvariant();
                    var sector = BiomeSector.GetBiomeName(b);   // the game's own token; a variant sector's name carries it ("<prefix> $biome_mountain")
                    var shown = Localization.instance != null ? Localization.instance.Localize(token) : token;
                    if (list.Any(n => n == token || n == shown || n.StartsWith(shown + " ") || n.EndsWith(" " + shown) || n.Contains(" " + shown + " ") ||
                                      (!string.IsNullOrEmpty(sector) && n.Split(' ').Contains(sector))))
                        found.Add(b.ToString());
                }
                return found;
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] known biomes: " + e.Message); return null; }
        }

        // zones-wording: the filter choices and open bars of this character, kept on disk (PanelPrefs.cs); loaded when the panel opens for a
        // character it has not loaded yet, written after a change (only when something changed) and when the panel closes
        string prefsPath, prefsSaved;
        void LoadPrefs()
        {
            try
            {
                var profile = Game.instance?.GetPlayerProfile(); if (profile == null) return;
                var path = PanelPrefs.PathFor(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "Hearthwoven"), profile.GetPlayerID());
                if (path == prefsPath) return;
                state.FilterRow = -1;
                prefsSaved = PanelPrefs.Load(path, state); prefsPath = path;
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel filter choices not loaded: " + e.Message); }
        }
        void SavePrefs()
        {
            if (prefsPath == null) return;
            try { prefsSaved = PanelPrefs.Save(prefsPath, state, prefsSaved); }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel filter choices not saved: " + e.Message); prefsPath = null; }
        }

        // Dev.SelfCheck: one refresh's cost (model build + UI draw; DevCheck.Perf). The self-check's page walk and the snapshot
        // runs (snapping) render every page on purpose and are left out.
        void Render(bool force)
        {
            if (!DevCheck.On || snapping) { Draw(force); return; }
            var start = PerfMeter.Now;
            try { Draw(force); } finally { DevCheck.Perf.AddRefresh(PerfMeter.Now - start); }
        }

        void Draw(bool force)
        {
            if (force) SavePrefs();
            nextRefresh = Time.unscaledTime + 2f;
            if (GroupShare.Sharing()) GroupShare.Request();   // keeps the group fresh while open (at most every 30 s)
            var self = Gather();
            self.Fellows = new List<PanelInput>();
            var subject = Fellow(state.Player, self);
            if (subject == null) state.Player = "";
            // everyone who shares, so links run both ways (they ate your food); looking at a fellow, you are one of theirs
            var fellows = FellowsOf(self);
            self.Fellows = fellows;
            FeatsRender(self, subject == null);   // note the feats earned so far; opening the Feats page marks them seen (Chapters/FeatsUi.cs)
            if (subject != null) subject.Fellows = fellows.Where(f => f != subject).Concat(new[] { self }).ToList();
            state.Hotkey = Hotkey.Value == KeyCode.None ? "" : Hotkey.Value.ToString();
            state.InfoKey = InfoKey.Value == KeyCode.None ? "" : InfoKey.Value.ToString();
            state.ViewKey = ViewKey.Value == KeyCode.None ? "" : ViewKey.Value.ToString();
            state.FilterKey = FilterKey.Value == KeyCode.None ? "" : FilterKey.Value.ToString();
            var v = PanelModel.Build(subject ?? self, state);
            PanelModel.AddPlayers(v, self.PlayerName, GroupNames(self.PlayerName), state.Player, SharingShown(), self.Solo);
            view = v;
            PanelModel.Visited(state, v.Active, v.Page);   // the back stack (Backspace)
            var json = PanelModel.ToJson(v);
            if (!force && json == shown) return;   // nothing new: keep what is on screen
            shown = json;
            Fill(v);
        }

        // ---------- building the frame (once) ----------
        // Layout follows the UI kit's assembly (ui-kit/preview-company.png, preview-battle.png): a 1180 x 760 nine-sliced
        // frame, header text clear of the 56 px corner ornaments, six tabs, a 268 px left list, content to the right.

        // chrome A2 (Joost 2026-10-08, proto/chrome-A2.png + chromeA*.css): 52 px tabs inset for the Q/E keycaps, a 190 px list of
        // 40 px rows from y 180, the page heading and the list title at y 144, the content right after the list
        const float W = 1180, H = 760, Inset = 32, ListW = 190, HeadTop = 144, ListTop = 180, Foot = 72, TabTop = 77, TabH = 52, TabInset = 70, RowH = 40;
        static readonly Color Amber = new Color(1f, 0.81f, 0.5f);

        void Build()
        {
            root = new GameObject("Hearthwoven", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100;   // kit sprites are 100 px per unit, borders in source pixels
            root.AddComponent<GraphicRaycaster>();

            // the panel only: the world stays visible around it (the frame's centre is translucent)
            var bg = Kit(root.transform, "Panel", "frame", raycast: true);
            frame = bg.rectTransform;
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(W, H);
            ApplyScale();

            // Joost's logo (flame mark + wordmark, ui/hearthwoven-logo.png: a 2x texture of its 60 px height with a 2 px clear
            // margin) where the title text stood, centred on the player chips row (y 44); the text stays as the fallback
            var logo = PanelLook.Ui("hearthwoven-logo");
            if (logo != null) Img(frame, "Logo", logo, Color.white).rectTransform.Box(64, 14, logo.rect.width * 60f / logo.rect.height, 60);
            else Label(frame, "HEARTHWOVEN", 32, PanelLook.Gold, title: true, align: TextAlignmentOptions.MidlineLeft).rectTransform.Box(66, 22, 300, 44);
            owner = Label(frame, "", 20, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
            owner.rectTransform.Box(385, 22, 300, 44);

            players = Node("Players", frame);
            players.anchorMin = players.anchorMax = players.pivot = new Vector2(1, 1);
            players.anchoredPosition = new Vector2(-64, -20); players.sizeDelta = new Vector2(620, 48);
            Layout(players.gameObject.AddComponent<HorizontalLayoutGroup>(), 8, TextAnchor.MiddleRight);

            chapters = Node("Chapters", frame);
            chapters.anchorMin = new Vector2(0, 1); chapters.anchorMax = new Vector2(1, 1); chapters.pivot = new Vector2(0.5f, 1);
            chapters.offsetMin = new Vector2(TabInset, -(TabTop + TabH)); chapters.offsetMax = new Vector2(-TabInset, -TabTop);
            var cl = chapters.gameObject.AddComponent<HorizontalLayoutGroup>(); Layout(cl, 8, TextAnchor.MiddleCenter); cl.childForceExpandWidth = true;
            keyQ = Keycap(frame, "Q"); keyQ.rectTransform.Box(TabInset - 8 - 24, TabTop + (TabH - 24) / 2, 24, 24);   // the keys where they act (A2)
            keyE = Keycap(frame, "E"); keyE.rectTransform.Box(W - TabInset + 8, TabTop + (TabH - 24) / 2, 24, 24);

            listTitle = Label(frame, "", 22, PanelLook.Gold, align: TextAlignmentOptions.MidlineLeft);
            listTitle.rectTransform.Box(Inset, HeadTop, ListW, 30); listTitle.textWrappingMode = TextWrappingModes.NoWrap;
            list = Scroller("List", frame, Inset, ListTop, ListW, Foot + KeyRoom, 8);   // fix3-rest: ends above the S keycap, so the keycap never covers an entry when the list scrolls
            keyW = Keycap(frame, "W"); keyS = Keycap(frame, "S");

            var right = Node("Content", frame);
            right.anchorMin = Vector2.zero; right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(Inset + ListW + 22, Foot); right.offsetMax = new Vector2(-Inset - 8, -HeadTop);
            heading = Label(right, "", 26, PanelLook.Gold, align: TextAlignmentOptions.MidlineLeft); heading.rectTransform.Box(0, 0, Wide, 36);
            heading.textWrappingMode = TextWrappingModes.NoWrap; heading.overflowMode = TextOverflowModes.Ellipsis;
            headingSince = Label(right, PanelModel.SinceInstallLabel, 15, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic);
            headingSince.textWrappingMode = TextWrappingModes.NoWrap; headingSince.enabled = false;
            scope = Label(right, "", 18, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); scope.rectTransform.Box(0, 38, Wide, 26);
            scope.textWrappingMode = TextWrappingModes.NoWrap; scope.overflowMode = TextOverflowModes.Ellipsis;
            // the heading row of a page on the plate (vocab.css .hrow): its icon left of the heading, the pill or the
            // window choices on the right
            headIcon = Node("HeadIcon", right); headIcon.Box(0, 5, 26, 26);
            headRight = Node("HeadRight", right);
            headRight.anchorMin = headRight.anchorMax = headRight.pivot = new Vector2(1, 1); headRight.anchoredPosition = Vector2.zero; headRight.sizeDelta = new Vector2(Column, 36);
            Layout(headRight.gameObject.AddComponent<HorizontalLayoutGroup>(), 4, TextAnchor.MiddleRight);
            // the plate (vocab.css .plate), behind the blocks: a flat dark fill with the kit's meter track sliced over it (its 1 px edge, as .plate's border), from
            // the list's top line down, so heading and list title, plate and list share their edges
            plateFill = Img(right, "Plate", null, PlateColour); PlateBox(plateFill.rectTransform, 0);
            plateEdge = Kit(right, "PlateEdge", "meter-track"); PlateBox(plateEdge.rectTransform, 0);
            content = Scroller("Blocks", right, 0, 76, -1, 0, 12);

            keys = Label(frame, "", 15, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
            keys.rectTransform.Bottom(64, 22, W - 128, 30);
            BuildSampleTag();   // "Sample data" in the footer while Dev.SampleData is on (PanelSampleUi.cs)
            root.SetActive(false);
        }

        // a vertical list that scrolls inside its box (mouse wheel), never drawing over its neighbours
        RectTransform Scroller(string name, RectTransform parent, float left, float top, float width, float bottom, float spacing)
        {
            var box = Node(name, parent);
            box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(width < 0 ? 1 : 0, 1);
            box.offsetMin = new Vector2(left, bottom); box.offsetMax = new Vector2(width < 0 ? 0 : left + width, -top);
            box.gameObject.AddComponent<RectMask2D>();
            Img(box, "Catch", null, new Color(0, 0, 0, 0), raycast: true).rectTransform.Stretch();   // lets the wheel reach the scroll area
            var inner = Node("Items", box);
            inner.anchorMin = new Vector2(0, 1); inner.anchorMax = new Vector2(1, 1); inner.pivot = new Vector2(0.5f, 1);
            inner.offsetMin = inner.offsetMax = Vector2.zero;
            var v = inner.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = spacing; v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            v.padding = new RectOffset(2, 2, 2, 2);   // room for the slices' outer glow
            inner.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = box.gameObject.AddComponent<ScrollRect>();
            scroll.content = inner; scroll.viewport = box; scroll.horizontal = false; scroll.vertical = true;
            // the wheel is ours (Wheel): the game's input system scales a notch down to a few pixels, which felt stuck
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 0f; scroll.inertia = false;
            scrollers.Add(new Scroll { Rect = scroll, Top = Fade(box, top: true), Bottom = Fade(box, top: false), ByRow = width >= 0 });
            return inner;
        }

        // ---------- scrolling: one wheel notch moves at least one row; a soft fade where more lies beyond the edge ----------

        class Scroll { public ScrollRect Rect; public Image Top, Bottom; public GameObject Cue; public bool ByRow, Easing; public float Target, Velocity; }
        readonly List<Scroll> scrollers = new List<Scroll>();
        const float WheelStep = 64f, FadeHeight = 40f, FadeInset = 12f, EaseTime = 0.04f;   // SmoothDamp 0.04 s: settled in about 0.12 s
        static float Room(Scroll s) => PanelModel.ScrollRoom(s.Rect.content.rect.height, s.Rect.viewport.rect.height);

        void Wheel()
        {
            var d = ZInput.GetMouseScrollWheel();
            if (!Mathf.Approximately(d, 0f)) SetKeyFocus(false);   // the wheel is mouse use: the focus ring hides
            if (Mathf.Approximately(d, 0f) || scrollers.Count == 0) return;
            var pos = (Vector2)ZInput.pointerPosition;
            // the innermost area under the pointer that has room to scroll (a feats grid is registered after the page that holds it; one that
            // fits passes the wheel on to the page), else the content column
            var under = scrollers.Where(x => x.Rect && x.Rect.viewport && RectTransformUtility.RectangleContainsScreenPoint(x.Rect.viewport, pos, null)).ToList();
            var s = under.LastOrDefault(x => Room(x) > 0f) ?? under.LastOrDefault() ?? scrollers.FirstOrDefault(x => x.Rect && x.Rect.content == content);
            if (s == null) return;
            var c = s.Rect.content; var room = Room(s);
            if (room <= 0f) return;
            var step = WheelStep;
            if (s.ByRow && c.childCount > 0)   // the left list: exactly one entry (row plus its spacing) per notch
            {
                var vlg = c.GetComponent<VerticalLayoutGroup>();
                step = Mathf.Max(step, ((RectTransform)c.GetChild(0)).rect.height + (vlg ? vlg.spacing : 0f));
            }
            // eased, not a jump: the wheel moves the target, Ease glides there; notches in a row add up
            var from = s.Easing ? s.Target : c.anchoredPosition.y;
            s.Target = Mathf.Clamp(from - Mathf.Sign(d) * step, 0f, room); s.Easing = true;
        }

        // glide toward the wheel's target; with under 12 px of overflow there is no scroll range at all (no drag either)
        static void Ease(Scroll s)
        {
            if (!s.Rect) return;
            var c = s.Rect.content; var room = Room(s); var y = c.anchoredPosition.y;
            s.Rect.vertical = room > 0f;
            if (room <= 0f) { s.Easing = false; s.Velocity = 0f; if (y != 0f) c.anchoredPosition = new Vector2(c.anchoredPosition.x, 0f); return; }
            if (!s.Easing) return;
            s.Target = Mathf.Clamp(s.Target, 0f, room);
            y = Mathf.SmoothDamp(y, s.Target, ref s.Velocity, EaseTime, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(y - s.Target) < 0.5f) { y = s.Target; s.Easing = false; s.Velocity = 0f; }
            c.anchoredPosition = new Vector2(c.anchoredPosition.x, y);
        }

        // a fade only on an edge that has content beyond it; nothing when everything fits
        static void Fades(Scroll s)
        {
            if (!s.Rect) return;
            var room = Room(s);                          // 0 unless there is real overflow
            var y = s.Rect.content.anchoredPosition.y;   // 0 = top, room = bottom
            s.Top.enabled = room > 0f && y > 1f;
            s.Bottom.enabled = room > 0f && y < room - 1f;
            if (s.Cue) s.Cue.SetActive(s.Bottom.enabled);   // "More feats below": only while the bottom fade shows (Chapters/FeatsUi.cs)
        }

        // the panel's own warm tone as a pure gradient (no solid part): at most 65% at the clipped edge, gone 40 px in, kept
        // 12 px inside the column on both sides and softened toward its ends; drawn over the items, never catching clicks
        static Image Fade(RectTransform box, bool top)
        {
            var img = Img(box, top ? "FadeTop" : "FadeBottom", FadeSprite(top), new Color(PanelLook.Panel.r, PanelLook.Panel.g, PanelLook.Panel.b, 0.65f));
            img.preserveAspect = false; img.enabled = false;
            var r = img.rectTransform;
            r.anchorMin = new Vector2(0, top ? 1 : 0); r.anchorMax = new Vector2(1, top ? 1 : 0); r.pivot = new Vector2(0.5f, top ? 1 : 0);
            r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(-2f * FadeInset, FadeHeight);
            return img;
        }

        static Sprite fadeTop, fadeBottom;
        static Sprite FadeSprite(bool top)
        {
            if (top ? fadeTop : fadeBottom) return top ? fadeTop : fadeBottom;
            const int h = 32, w = 32;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HearthwovenFade" };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var edge = top ? y / (h - 1f) : 1f - y / (h - 1f);          // 1 at the clipped edge, 0 inside
                    var side = Mathf.Clamp01(Mathf.Min(x, w - 1 - x) / (w * 0.2f)); // soft ends instead of a hard cut
                    side = side * side * (3f - 2f * side);
                    t.SetPixel(x, y, new Color(1, 1, 1, edge * edge * side));    // quadratic: only the very edge is strongest
                }
            t.Apply(false, true);
            var sprite = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            if (top) fadeTop = sprite; else fadeBottom = sprite;
            return sprite;
        }

        // ---------- filling it from the model ----------

        static Dictionary<string, int> personColors = new Dictionary<string, int>();

        void Fill(PanelView v)
        {
            personColors = v.PersonColors;
            var pageKey = v.Active + "/" + v.Page + "/" + state.Player;
            var newPage = pageKey != lastPage; lastPage = pageKey;

            owner.text = v.Players.Count > 0 ? "" : v.Owner;
            listTitle.text = v.ListTitle ?? "";
            var plate = PanelModel.PlateOf(v);
            plateFull = plate != null && plate.Tone == PanelModel.PlateFull;   // the Feats chapter: the plate keeps the whole room (Chapters/FeatsUi.cs)
            Plated(plate != null);
            // the heading row: on a plate the page icon leads and the pill or the window choices sit on the right
            Clear(headIcon); Clear(headRight); if (bookChip) Destroy(bookChip.gameObject);
            var headX = 0f;
            if (plate != null && !string.IsNullOrEmpty(plate.Icon)) { Marker(headIcon, plate.Icon, 26, layout: false).Stretch(); headX = 36; }
            // another player's book: a chip in their colour left of the title says whose book this is (nothing on your own)
            if (!string.IsNullOrEmpty(state.Player)) headX += BookChip(headIcon.parent as RectTransform, state.Player, headX) + 10;
            var rightW = plate != null ? HeadRight(v, plate) : 0f;
            heading.text = (v.Heading ?? "") + (string.IsNullOrEmpty(v.HeadingWindow) ? "" : ", " + v.HeadingWindow);
            heading.fontSize = plate != null ? 24 : 26;
            // "since install" just after the heading when the page's numbers were counted on this PC (no icons, Joost 2026-10-08)
            headingSince.enabled = v.HeadingSinceInstall && heading.text.Length > 0;
            var headW = Wide - headX - (rightW > 0 ? rightW + 16 : 0) - (headingSince.enabled ? 140 : 0);
            if (headW > 0 && heading.preferredWidth > headW) heading.fontSize = Mathf.Max(17f, heading.fontSize * headW / heading.preferredWidth);   // six window chips take room: the longer heading shrinks to fit
            heading.rectTransform.Box(headX, 0, headW, 36);
            if (headingSince.enabled) headingSince.rectTransform.Box(headX + Mathf.Min(heading.preferredWidth, headW) + 12, 3, 130, 32);
            scope.text = plate != null ? "" : v.Scope ?? "";   // on a plate the scope is the heading row's choices, or a line on the plate
            baseKeys = string.Join("      ", v.Keys.ToArray()); keys.text = baseKeys;
            ShowSampleTag();

            Clear(players);
            if (!string.IsNullOrEmpty(v.ShareNote) && v.Players.Count == 0)   // sharing off (fix4-rest): a framed notice in the chip row: what off means, and where to read how to turn it on
            {
                var lines = v.ShareNote.Split('\n');
                var box = Kit(players, "ShareOff", "meter-track"); box.color = new Color(0.03f, 0.025f, 0.02f, 0.85f);
                var bl = box.gameObject.AddComponent<LayoutElement>(); bl.preferredWidth = bl.minWidth = 572; bl.preferredHeight = bl.minHeight = 48;
                var glyph = VocabImg(box.rectTransform, "Glyph", "src-fellows", Color.white); glyph.rectTransform.Box(10, 14, 20, 20);
                var first = Label(box.transform, lines[0], 15, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.MidlineLeft); first.textWrappingMode = TextWrappingModes.NoWrap; first.rectTransform.Box(40, 3, 524, 22);
                if (lines.Length > 1) { var how = Label(box.transform, lines[1], 15, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); how.textWrappingMode = TextWrappingModes.NoWrap; how.rectTransform.Box(40, 25, 524, 20); }
            }
            else if (!string.IsNullOrEmpty(v.ShareNote))   // the line that waits for fellow players sits in the header, left of the chips
            {
                var note = Label(players, v.ShareNote, 15, PanelLook.Muted, align: TextAlignmentOptions.MidlineRight);
                var nl = note.gameObject.AddComponent<LayoutElement>(); nl.preferredWidth = nl.minWidth = 330;
            }
            const int perPage = 5;
            var others = v.Players.Skip(1).ToList();
            if (playerPage * perPage >= others.Count) playerPage = 0;
            if (v.Players.Count > 0) Entry(players, v.Players[0].Label, v.Players[0].Icon, v.Players[0].Selected, PlayerClick(v.Players[0]), 48);
            foreach (var c in others.Skip(playerPage * perPage).Take(perPage))
            {
                var chip = Entry(players, c.Label, c.Icon, c.Selected, PlayerClick(c), 48);
                if (c.Selected) Glow(chip, PersonTint(c.Id ?? c.Label));   // the fellow whose book is open glows in their colour
            }
            if (others.Count > perPage) Entry(players, "More", "", false, () => { playerPage++; Render(true); }, 48);

            Clear(chapters);
            foreach (var c in v.Chapters)
            {
                var id = (Chapter)Enum.Parse(typeof(Chapter), c.Id);
                Tab(c, () => { state.ShowAbout = false; state.Chapter = id; Render(true); });
            }

            Clear(list);
            // A2: 40 px rows 8 apart; a long list (Deeds has ten) first closes the gaps to 4 px, then rows go to 36 px, so up to
            // ten entries fit without scrolling with the S keycap under the last row (PanelModel.ListRows)
            var (rowH, gap) = PanelModel.ListRows(v.List.Count, H - Foot - ListTop - KeyRoom);
            list.GetComponent<VerticalLayoutGroup>().spacing = gap; listRow = rowH; listGap = gap;
            foreach (var c in v.List)
            {
                var id = c.Id; var chapter = v.Active;
                var about = v.ShowAbout;   // About's own list (How it counts, What it reads, Sharing) keeps About open
                var entry = Entry(list, c.Label, c.Icon, c.Selected, () => { if (about) state.AboutPage = id; else { state.ShowAbout = false; state.Page[chapter] = id; } Render(true); }, rowH, stretch: true);
                if (c.Dot) FeatDot((RectTransform)entry.transform);   // earned feats not seen yet
            }
            PlaceListKeys(v);

            scrollers.RemoveAll(x => !x.Rect);   // the grids of the page just left
            Clear(content);
            if (plate != null)
            {
                // the page on its plate: the plate's own line first, then its blocks at the plate's width
                if (!string.IsNullOrEmpty(plate.Text)) Label(content, plate.Text, 15, PanelLook.Muted, style: FontStyles.Italic);
                // the page's own skill: its chip in the heading row, but that row holds the window chips here, so one line at the top of the plate
                if (PanelModel.HeadSkillsOnPlate(v)) { var skills = Line(content, 4); foreach (var s in HeadSkills(plate)) SkillChip(skills, s); }
                foreach (var b in plate.Items ?? new List<Block>()) Draw(content, b);
                plated = false; Column = Wide;
            }
            if (plate == null && v.Badges.Count > 0)
            {
                var badges = Row(content, 18);
                foreach (var b in v.Badges) { var r = Row(badges, 8); r.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft; Marker(r, b.Icon, 34); Label(r, b.Label, 18, PanelLook.Gold); }
            }
            if (plate == null && v.HasFilters)
            {
                var row = Row(content, 8);
                foreach (var w in v.Windows) { var id = (TimeWindow)Enum.Parse(typeof(TimeWindow), w.Id); Entry(row, w.Label, "", w.Selected, () => { state.Window = id; Render(true); }, 48); }
            }
            if (v.Toggle.Count > 0)
            {
                var row = Row(content, 14);
                foreach (var t in v.Toggle) { var they = t.Id == "they"; Toggle(row, t, () => { state.TheyReceived = they; Render(true); }); }
            }
            if (plate == null) foreach (var b in v.Blocks) Draw(content, b);
            FitPlate(plate != null);
            if (newPage)
            {
                foreach (var s in scrollers) if (s.Rect && s.Rect.content == content) { s.Easing = false; s.Velocity = 0f; }   // a new page starts at the top, no glide
                Canvas.ForceUpdateCanvases(); content.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1f;
            }
        }

        Action PlayerClick(Choice c) { var id = c.Id; return () => { state.Player = id; Render(true); }; }

        // ---------- the plate (slice 3: vocab.css .hrow and .plate) ----------

        const float Wide = 889f, PlateColumn = 840f, PlateTop = 36, PlateInset = 4;   // A2: the content 99 px wider; the plate from the list's top line
        const int PlatePadX = 24, PlatePadTop = 12, PlatePadBottom = 10;   // denser (Joost 2026-10-08: pages fit without scrolling)
        static readonly Color PlateColour = new Color(0.058f, 0.045f, 0.032f, 0.95f);
        static float Column = Wide;   // the width of what is being drawn: the content column, the plate's inside or one column
        static bool plated;           // drawing on a plate: section headings in the plate's small capitals (vocab.css .sect)

        static void PlateBox(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -PlateTop - inset);
        }

        // the content column as an open page (heading, scope, blocks from 76 px) or as the plate (blocks inside it from the
        // list's top line, padded like the prototype's 20 / 28 px, the scroll fades in the plate's colour)
        void Plated(bool on)
        {
            plateFill.enabled = plateEdge.enabled = on;
            var box = (RectTransform)content.parent;
            if (on) PlateBox(box, PlateInset); else { box.offsetMin = Vector2.zero; box.offsetMax = new Vector2(0, -76); }
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.padding = on ? new RectOffset(PlatePadX, PlatePadX, PlatePadTop, PlatePadBottom) : new RectOffset(2, 2, 2, 2);
            vlg.spacing = on ? 10 : 10;
            var tone = on ? PlateColour : PanelLook.Panel;
            foreach (var s in scrollers.Where(s => s.Rect && s.Rect.content == content)) s.Top.color = s.Bottom.color = new Color(tone.r, tone.g, tone.b, on ? 0.9f : 0.65f);
            plated = on; Column = on ? PlateColumn : Wide;
        }

        // the plate's height = min(content, room) (PanelModel.PlateCut): a short page's plate ends where its content ends, a
        // longer one keeps the whole room and scrolls with its fades. The content is what is really drawn (ContentNeed), and
        // it is measured again over the next frames (LateUpdate) while texts and grids settle: a measure taken too early cut
        // Woodcutting, Mining and Farming short in game, clipping their lower blocks with nothing to scroll (fix2 1)
        const int PlateRefits = 3;
        int plateRefits;
        void FitPlate(bool on)
        {
            plateRefits = on ? PlateRefits : 0;
            PlateBox(plateFill.rectTransform, 0); PlateBox(plateEdge.rectTransform, 0);
            if (on) CutPlate();
        }

        void CutPlate()
        {
            PlateBox(plateFill.rectTransform, 0); PlateBox(plateEdge.rectTransform, 0);
            var box = (RectTransform)content.parent;
            PlateBox(box, PlateInset);
            if (plateFull) return;   // a full plate keeps the whole room (its grid grows into it)
            var cut = PanelModel.PlateCut(box.rect.height, ContentNeed());
            if (cut <= 0) return;
            box.offsetMin += new Vector2(0, cut);
            plateFill.rectTransform.offsetMin += new Vector2(0, cut); plateEdge.rectTransform.offsetMin += new Vector2(0, cut);
        }

        // the height the page's blocks take: the layout's own height, or more when a drawn picture or text reaches below it
        // (PanelModel.PlateNeed); then a spacer at the end grows the content to it, so the scroll range covers it too
        const string OverflowName = "Overflow";
        float ContentNeed()
        {
            var old = content.Find(OverflowName);
            if (old) { old.SetParent(null, false); Destroy(old.gameObject); }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var layout = LayoutUtility.GetPreferredHeight(content);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            var need = PanelModel.PlateNeed(layout, Reach(content), vlg.padding.bottom);
            if (need - layout >= 1f)
            {
                var pad = Node(OverflowName, content); Size(pad, -1, Mathf.Max(0f, need - layout - vlg.spacing)); pad.SetAsLastSibling();
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                need = Mathf.Max(need, LayoutUtility.GetPreferredHeight(content));
            }
            return need;
        }

        // how far below the content's top edge its lowest drawn picture or text ends
        static readonly Vector3[] corners = new Vector3[4];
        static float Reach(RectTransform c)
        {
            float top = c.rect.yMax, low = top;
            foreach (var g in c.GetComponentsInChildren<Graphic>(false))
            {
                if (!g.enabled || g.rectTransform.rect.height <= 0f) continue;
                if (g is TMP_Text t && string.IsNullOrEmpty(t.text)) continue;
                var mask = g.GetComponentInParent<RectMask2D>(); if (mask && mask.transform != c.parent) continue;   // inside a nested scroll area (the feats grid): what its mask hides is not the page's height
                g.rectTransform.GetWorldCorners(corners);
                low = Mathf.Min(low, c.InverseTransformPoint(corners[0]).y);
            }
            return top - low;
        }

        // the heading row's right side: a Battle page's window and biome choices, else the plate's pill (its title badges)
        float HeadRight(PanelView v, Block plate)
        {
            float w = 0;
            if (v.HasFilters)
            {
                foreach (var c in v.Windows)
                {
                    var id = (TimeWindow)Enum.Parse(typeof(TimeWindow), c.Id);
                    // a window a fellow's copy cannot show stays in the row, greyed and inert (the plate says why); a day window your own day history
                    // does not reach yet is greyed too, but pressing it chooses it: All shows and one line says from when it works (B17, PanelModel.WaitLine)
                    w += Chip(headRight, c.Label, null, c.Selected, c.Disabled && !c.Waits ? null : Safe(() => { state.Window = id; Render(true); }), off: c.Disabled, pad: WindowPad) + 4;
                }
            }
            else
            {
                // zones-wording: the page's own skill (Woodcutting's Wood Cutting) as a small chip left of the pill, not a band across the plate
                foreach (var s in HeadSkills(plate)) w += SkillChip(headRight, s) + 4;
                if (!string.IsNullOrEmpty(plate.Pill)) w += Chip(headRight, plate.Pill, plate.PillIcon, false, null);
            }
            return w;
        }

        /// <summary>The skills a page shows as chips: a skill strip without a scope line (Battle's weapon skills keep theirs on the plate).</summary>
        static IEnumerable<Block> HeadSkills(Block plate) => PanelModel.HeadSkills(plate);
        static bool IsHeadStrip(Block b) => PanelModel.IsHeadStrip(b);
        static float SkillChip(RectTransform row, Block s) => Chip(row, s.Title + "  " + PanelModel.LevelWord + " " + s.Value, s.Icon, false, null);

        // a click's target: a page ("Battle/defense") or a view of a switch ("view:Skills/overview/view=practised")
        Action LinkTo(string target) => Safe(() =>
        {
            PanelModel.Follow(state, target); Render(true);
            if (target.StartsWith(PanelModel.ViewTarget, StringComparison.Ordinal)) Debug.Log("[Hearthwoven] view chip: " + target.Substring(PanelModel.ViewTarget.Length));
        });

        static bool zoneTight;   // a tight zone (Woodcutting) is being drawn: its section headings sit closer

        void Draw(RectTransform col, Block b)
        {
            // composition and hero write their note in their own line; the layout boxes hold blocks, not a note
            if (DrawVocab(col, b, LinkTo, Draw)) { if (b.Kind != "composition" && b.Kind != "hero" && b.Kind != "cropgrid" && b.Kind != "featband" && !PanelModel.IsBox(b)) Note(col, b.Note); return; }   // the strip says its own words (B18: its titles were drawn twice)
            switch (b.Kind)
            {
                case "section":
                    {
                        Spacer(col, zoneTight ? 3 : plated ? 8 : 4);
                        var row = Row(col, 8); row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                        if (!string.IsNullOrEmpty(b.Icon)) Marker(row, b.Icon, 22);
                        if (plated) Sect(row, b.Title, b.Value); else Label(row, b.Title, 20, PanelLook.Gold);
                        if (b.SinceInstall) Since(row, plated ? 13 : 14);
                        else if (!string.IsNullOrEmpty(b.Text)) Label(row, b.Text, 13, PanelLook.Faint, style: FontStyles.Italic).textWrappingMode = TextWrappingModes.NoWrap;   // whose record (their last session)
                        break;
                    }
                case "divider": Divider(col); break;
                case "stat": Stat(col, b); break;
                // a whole list or grid counted on this PC, outside a section that says so: the label once, under it
                case "tiles": Tiles(col, b.Items, 6); if (b.SinceInstall || (b.Items?.Any(i => i.SinceInstall) ?? false)) Since(col, 13); Note(col, b.Note); break;
                case "bars": Bars(col, b); if (b.SinceInstall) Since(col, 13); Note(col, b.Note); break;
                case "rows": Rows(col, b); if (b.SinceInstall) Since(col, 13); Note(col, b.Note); break;
                case "titles": Titles(col, b); break;
                case "thread": Thread(col, b); if (b.SinceInstall) Since(col, 13); Note(col, b.Note); break;
                case "link":
                    {
                        var target = b.Id;
                        var link = Entry(col, b.Title, b.Icon, false, () => { PanelModel.Jump(state, target); Render(true); }, 48);
                        link.GetComponent<LayoutElement>().flexibleWidth = 0;
                        break;
                    }
                case "empty":   // compact: the heading and its one line together (Joost 2026-10-08)
                    {
                        var box = VStack(col, 2);
                        Label(box, b.Title, 18, PanelLook.Muted, style: FontStyles.Italic);
                        if (!string.IsNullOrEmpty(b.Text)) Label(box, b.Text, 15, PanelLook.Faint);
                        break;
                    }
                default:   // note, or a hint before setting out
                    if (b.Tone == "hint")
                    {
                        var row = Row(col, 10);
                        var mark = Img(row, "Mark", null, PanelLook.Accent); var le = mark.gameObject.AddComponent<LayoutElement>(); le.minWidth = le.preferredWidth = 3; le.flexibleHeight = 1;
                        Label(row, b.Text, 17, PanelLook.Text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                    }
                    else if (b.Tone == "tag") { }   // the key is drawn on the faded number itself (HeroNumber)
                    else if (b.Text == PanelModel.FadedKey || b.Text == PanelModel.FadedKeyTwin) FadedChip(Line(col, 0), b.Text);   // the faded legend chip, as under a layered bar; the twin key above a grid with both layers
                    else Label(col, b.Text, 15, PanelLook.Muted);
                    break;
            }
        }

        void Note(RectTransform col, string note)
        {
            if (string.IsNullOrEmpty(note)) return;
            if (note == PanelModel.FadedKey || note == PanelModel.FadedKeyTwin) { FadedChip(Line(col, 0), note); return; }   // the faded key: a chip, not a sentence
            Label(col, note, 13, PanelLook.Faint);
        }

        // "faded = before install": a small quiet chip (the kit's dark track), where a legend ends; returns its width
        static float FadedChip(RectTransform row, string text = null)
        {
            var img = Kit(row, "FadedKey", "meter-track");
            var t = Label(img.transform, text ?? PanelModel.FadedKey, 13, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic);
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch(); t.rectTransform.offsetMin = new Vector2(9, 0); t.rectTransform.offsetMax = new Vector2(-9, 0);
            var w = Mathf.Ceil(t.preferredWidth) + 18; Size(img, w, 22);
            return w;
        }

        void Divider(RectTransform col)
        {
            var d = Kit(col, "Divider", "divider"); var le = d.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 16;   // fixed height (kit)
        }

        void Stat(RectTransform col, Block b)
        {
            if (b.Tone == PanelModel.Compact)   // fix3-rest: one line (Heavy Keel on Voyages > Cargo): the number, its name, the rest muted beside them
            {
                var one = Row(col, 10);
                one.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
                if (!string.IsNullOrEmpty(b.Icon)) Marker(one, b.Icon, 28);
                if (!string.IsNullOrEmpty(b.Value)) Label(one, b.Value, 22, PanelLook.Text);
                Label(one, b.Title, 16, PanelLook.Text);
                if (!string.IsNullOrEmpty(b.Text)) Label(one, b.Text, 14, PanelLook.Muted);
                if (b.SinceInstall) Since(one, 13);
                return;
            }
            var row = Row(col, 16);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            if (!string.IsNullOrEmpty(b.Icon)) Marker(row, b.Icon, 52);
            var texts = VStack(row, 0); texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var line = Row(texts, 10);
            line.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            if (!string.IsNullOrEmpty(b.Value)) Label(line, b.Value, 30, PanelLook.Text);
            Label(line, b.Title, 22, PanelLook.Text);
            if (b.SinceInstall) Since(line, 15);
            Node("Rest", line).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            if (!string.IsNullOrEmpty(b.Text)) Label(texts, b.Text, 18, PanelLook.Muted);
            if (!string.IsNullOrEmpty(b.Note)) Label(texts, b.Note, 13, PanelLook.Faint);
        }

        // the kit's slot with the game's own sprite inside (about 14 px of 128 inset, as the kit asks), its name beneath
        RectTransform Slot(RectTransform parent, string icon, float size)
        {
            var slot = Kit(parent, "Slot", "slot").rectTransform;
            slot.sizeDelta = new Vector2(size, size);
            var sprite = PanelLook.Icon(icon);
            if (sprite) { var i = Img(slot, "Item", sprite, Color.white).rectTransform; i.Stretch(); var inset = size * 14f / 128f; i.offsetMin = new Vector2(inset, inset); i.offsetMax = new Vector2(-inset, -inset); }
            return slot;
        }

        void Tiles(RectTransform col, List<Block> items, int columns)
        {
            var grid = Node("Tiles", col);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(112, 150); g.spacing = new Vector2(16, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = columns;
            foreach (var t in items ?? new List<Block>())
            {
                var cell = Node(t.Title, grid);
                var slot = Slot(cell, t.Icon, 96);
                slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 1); slot.anchoredPosition = Vector2.zero;
                var name = Label(cell, t.Title, 15, PanelLook.Text, align: TextAlignmentOptions.Top); name.rectTransform.Box(0, 100, 112, 22); name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
                if (!string.IsNullOrEmpty(t.Value))
                {
                    var count = Label(cell, t.Value, 19, PanelLook.Gold, align: TextAlignmentOptions.Top); count.rectTransform.Box(0, 122, 112, 26);
                }
            }
        }

        // the kit's meter: track and grey fill tinted by the damage type, one zero-based scale, its ends written beneath
        void Bars(RectTransform col, Block b)
        {
            foreach (var i in b.Items ?? new List<Block>())
            {
                var row = Row(col, 14);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                var icon = Node("Icon", row); var il = icon.gameObject.AddComponent<LayoutElement>(); il.minWidth = il.preferredWidth = 26; il.minHeight = il.preferredHeight = 26;
                var s = PanelLook.Icon(i.Icon); if (s) Img(icon, "Status", s, Color.white).rectTransform.Stretch();
                var tone = PanelLook.Tone(i.Tone);
                var label = Label(row, i.Title, 20, i.Tone == "ember" ? (i.Selected ? PanelLook.Gold : PanelLook.Text) : Color.Lerp(tone, PanelLook.Text, 0.25f), align: TextAlignmentOptions.MidlineLeft);
                var ll = label.gameObject.AddComponent<LayoutElement>(); ll.minWidth = ll.preferredWidth = 120;
                var track = Kit(row, "Track", "meter-track"); var tl = track.gameObject.AddComponent<LayoutElement>(); tl.flexibleWidth = 1; tl.minHeight = tl.preferredHeight = 20;
                if (i.Fraction > 0)
                {
                    var fill = Kit(track.transform, "Fill", "meter-fill"); fill.color = tone;
                    var fr = fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(Mathf.Clamp01(i.Fraction), 1);
                    fr.offsetMin = new Vector2(3, 3); fr.offsetMax = new Vector2(-3, -3);   // fill inset 3 px inside the track (kit)
                }
                var value = Label(row, i.Value, 20, PanelLook.Text, align: TextAlignmentOptions.MidlineRight); var vl = value.gameObject.AddComponent<LayoutElement>(); vl.minWidth = vl.preferredWidth = 80;
            }
            if (!string.IsNullOrEmpty(b.Value))
            {
                var axis = Row(col, 0);
                var pad = Node("Pad", axis).gameObject.AddComponent<LayoutElement>(); pad.minWidth = pad.preferredWidth = 26 + 14 + 120 + 14;
                Label(axis, "0", 13, PanelLook.Faint).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                Label(axis, b.Value, 13, PanelLook.Faint, align: TextAlignmentOptions.TopRight);
                var end = Node("End", axis).gameObject.AddComponent<LayoutElement>(); end.minWidth = end.preferredWidth = 14 + 80;
            }
        }

        // equal rows, never scaled to the top entry
        void Rows(RectTransform col, Block b)
        {
            // single rows counted on this PC in a list that also holds other counts: the label after their number, in a
            // column of its own so the values stay aligned
            var labelled = (b.Items ?? new List<Block>()).Any(i => i.SinceInstall);
            var marked = (b.Items ?? new List<Block>()).Any(i => !string.IsNullOrEmpty(i.Icon));
            col = VStack(col, 0);   // the rows and their rules close together, not spaced like blocks
            foreach (var i in b.Items ?? new List<Block>())
            {
                var row = Row(col, 14);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                row.gameObject.AddComponent<LayoutElement>().minHeight = 34;
                if (i.Icon != null && i.Icon.StartsWith("vocab:")) { var box = Node("Mark", row); Size(box, 34, 34); VocabImg(box, "Mark", VocabName(i.Icon), PanelLook.Muted).rectTransform.Box(4, 4, 26, 26); }   // a white mask, tinted
                else if (!string.IsNullOrEmpty(i.Icon)) Marker(row, i.Icon, 34);
                else if (marked) Size(Node("NoMark", row), 34, 34);   // keeps the names in one column
                if (string.IsNullOrEmpty(i.Text)) Label(row, i.Title, 19, PanelLook.Text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                else   // a second, quieter number under the name (picked up: the game's own count beside the exact one)
                {
                    var names = VStack(row, 0); names.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                    Label(names, i.Title, 19, PanelLook.Text); Label(names, i.Text, 14, PanelLook.Muted);
                }
                var value = Label(row, i.Value, 19, PanelLook.Text, align: TextAlignmentOptions.MidlineRight); var vl = value.gameObject.AddComponent<LayoutElement>(); vl.minWidth = vl.preferredWidth = Mathf.Min(170f, Column * 0.24f);   // narrower in a column
                if (labelled) { var slot = Node("Since", row); Size(slot, 92, 20); if (i.SinceInstall) Since(slot, 13).rectTransform.Stretch(); }
                var rule = Img(col, "Rule", null, PanelLook.Rule); var rl = rule.gameObject.AddComponent<LayoutElement>(); rl.minHeight = rl.preferredHeight = 1;
            }
        }

        // Deeds > Overview: each earned title is a shortcut to the page that owns its numbers
        void Titles(RectTransform col, Block b)
        {
            var grid = Node("Titles", col);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(254, 64); g.spacing = new Vector2(10, 6);   // 3 x 254 + 2 x 10 = 782, inside the 797 px column   // six rows hold all eighteen titles
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 3;
            foreach (var t in b.Items ?? new List<Block>())
            {
                var target = t.Id;
                var cell = Kit(grid, t.Title, "row", raycast: true);
                var button = cell.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = cell;
                button.onClick.AddListener(() => Safe(() => { PanelModel.Jump(state, target); Render(true); })());
                var m = Marker(cell.rectTransform, t.Icon, 40, layout: false); m.anchorMin = m.anchorMax = m.pivot = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(12, 0);
                var name = Label(cell.transform, t.Title, 17, PanelLook.Gold); name.rectTransform.Box(60, 6, 190, 22);
                var value = Label(cell.transform, t.Value, 12.5f, PanelLook.Muted, style: FontStyles.Italic); value.rectTransform.Box(60, 27, 190, 32);
            }
        }

        // the food in its slot, the count over the kit's woven thread, the eater's shield: one row per food
        void Thread(RectTransform col, Block b)
        {
            var ends = Row(col, 0);
            Label(ends, b.Title, 20, PanelLook.Text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Label(ends, b.Text, 20, PanelLook.Text, align: TextAlignmentOptions.TopRight).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var first = true;
            foreach (var t in b.Items ?? new List<Block>())
            {
                var row = Row(col, 20);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                var item = VStack(row, 4); var il = item.gameObject.AddComponent<LayoutElement>(); il.minWidth = il.preferredWidth = 120;
                var slotBox = Node("SlotBox", item); var sl = slotBox.gameObject.AddComponent<LayoutElement>(); sl.minHeight = sl.preferredHeight = 112;
                var slot = Slot(slotBox, t.Icon, 112); slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 0.5f); slot.anchoredPosition = Vector2.zero;
                Label(item, t.Title, 18, PanelLook.Text, align: TextAlignmentOptions.Top);
                var mid = VStack(row, 2); mid.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                mid.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                var said = Row(mid, 8); said.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                Label(said, t.Value + " " + t.Title.ToLowerInvariant(), 24, PanelLook.Text, align: TextAlignmentOptions.Center);
                if (t.SinceInstall) Since(said, 14);
                var threadBox = Node("ThreadBox", mid); var tb = threadBox.gameObject.AddComponent<LayoutElement>(); tb.minHeight = tb.preferredHeight = 40;
                var thread = Kit(threadBox, "Thread", "thread").rectTransform;   // decorative direction only, uniform scale
                thread.anchorMin = thread.anchorMax = thread.pivot = new Vector2(0.5f, 0.5f); thread.sizeDelta = new Vector2(320, 40);
                var shieldBox = Node("Eater", row); var sb = shieldBox.gameObject.AddComponent<LayoutElement>(); sb.minWidth = sb.preferredWidth = 120; sb.minHeight = sb.preferredHeight = 112;
                if (first) { var m = Marker(shieldBox, b.Icon, 112, layout: false); m.anchorMin = m.anchorMax = m.pivot = new Vector2(0.5f, 0.5f); m.anchoredPosition = Vector2.zero; }
                first = false;
            }
            Divider(col);
        }

        // ---------- the visual vocabulary (work/hearthwoven-visual-vocabulary/VOCABULARY.md) ----------
        // composition, biomes, ladders and ladder, ported from the approved proto forms (vocab.js compBar + layoutCompBars,
        // r2-refine.js biomeStrip2, r2-skills.js ladderMini/ladderBig). Every picture is a ready sprite (Codex's vocab set,
        // the UI kit or a native game icon) placed, sized, tinted and sliced once when the page is filled; nothing per frame.

        /// <summary>
        /// Draws a vocabulary or page-layout block; false for any other kind. link: a target -> its click (null: links and
        /// chips drawn inert). child: draws a block inside a layout box (the panel's own Draw; null: vocabulary kinds only,
        /// anything else as its plain title, as the self-test's probe needs).
        /// </summary>
        internal static bool DrawVocab(RectTransform col, Block b, Func<string, Action> link, Action<RectTransform, Block> child = null)
        {
            if (child == null) child = (c, x) => { if (!DrawVocab(c, x, link, null)) Label(c, x.Title ?? x.Text ?? "", 15, PanelLook.Text); };
            switch (b.Kind)
            {
                case "composition": Composition(col, b); return true;
                case "biomes": BiomeStrip(col, b, link); return true;
                case "ladders": Ladders(col, b); return true;
                case "ladder": Ladder(col, b, link); return true;
                case "axis": Axis(col, b); return true;
                case "giving": Giving(col, b); return true;
                case "together": Together(col, b, link); return true;
                case "madeby": MadeBy(col, b); return true;
                case "journey": Journey(col, b); return true;
                case "crew": Crew(col, b); return true;
                case "compass": Compass(col, b); return true;
                case "biometiles": BiomeTiles(col, b); return true;
                case "ranking": Ranking(col, b); return true;   // Chapters/DeedsUi.cs
                case "counts": Counts(col, b); return true;
                case "itemgrid": ItemGrid(col, b); return true;
                case "filterbar": FilterBar(col, b, link); return true;   // FacetUi.cs
                case "ledger": Ledger(col, b); return true;   // Chapters/VoyagesHallUi.cs (fix2 7)
                case "strip": Strip(col, b, link); return true;
                case "grades": Grades(col, b); return true;
                case "band": Band(col, b); return true;
                case "people": People(col, b); return true;
                case "hero": Hero(col, b); return true;
                case "cards": Cards(col, b, link); return true;
                case "columns": Columns(col, b, child); return true;
                case "switch": Switch(col, b, link, child); return true;
                case "plate": PlateBlock(col, b, child); return true;
                case "zone": Zone(col, b, child); return true;   // ZonesUi.cs
                case "bosses": Bosses(col, b); return true;
                case "origins": Origins(col, b); return true;   // Chapters/AboutUi.cs
                case "readrows": ReadRows(col, b); return true;
                case "sincewhen": SinceWhen(col, b); return true;
                case "promises": Promises(col, b); return true;
                case "cropgrid": CropGrid(col, b); return true;   // Chapters/CropsUi.cs
                case "feats": FeatsBlock(col, b); return true;   // Chapters/FeatsUi.cs
                case "featdetail": FeatDetailBlock(col, b); return true;
                case "knownfor": KnownForBlock(col, b, link); return true;
                case "featband": FeatBandBlock(col, b, link); return true;
                case "damagegrid": case "dmgmix": case "foetable": case "foetypes": case "guard": case "sources": case "deathstrip": case "deaths": case "deathrows": BattleBlock(col, b, link); return true;   // ch-battle
                default: return false;
            }
        }

        static Color Hex(string hex, Color fallback) => hex != null && hex.StartsWith("person:") ? PersonTint(hex.Substring(7)) : !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;   // "person:Edda": that fellow's colour
        static string VocabName(string reference) => string.IsNullOrEmpty(reference) ? null : reference.StartsWith("vocab:") ? reference.Substring(6) : reference;
        static readonly Color DarkInk = new Color(0.086f, 0.067f, 0.043f), LightInk = new Color(0.953f, 0.906f, 0.796f);

        static LayoutElement Size(Component c, float w, float h)
        {
            var le = c.GetComponent<LayoutElement>(); if (!le) le = c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) le.minWidth = le.preferredWidth = w;
            if (h >= 0) le.minHeight = le.preferredHeight = h;
            return le;
        }

        static RectTransform Line(RectTransform parent, float spacing, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var row = Row(parent, spacing); row.GetComponent<HorizontalLayoutGroup>().childAlignment = align; return row;
        }

        // a vocabulary sprite: sliced where its manifest gives borders, tiled for the grain fills (never stretched), else whole
        // at its own aspect; a missing sprite leaves its place empty
        static Image VocabImg(Transform parent, string name, string sprite, Color tint)
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            img.sprite = PanelLook.Vocab(sprite); img.color = tint; img.raycastTarget = false;
            if (!img.sprite) img.enabled = false;
            else if (img.sprite.border != Vector4.zero) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            else if (sprite.StartsWith("grain-")) { img.type = Image.Type.Tiled; img.pixelsPerUnitMultiplier = 1f; }
            else img.preserveAspect = true;
            return img;
        }

        // a layered number (Block.Faded/Solid, Farming's planted): the game's count before Hearthwoven faint, "+", then what
        // Hearthwoven counted since in the number's own colour (or <paramref name="solid"/>). Both are formatted numbers only,
        // so rich text is safe on that one label.
        internal static bool Layered(Block n) => n != null && !string.IsNullOrEmpty(n.Faded) && !string.IsNullOrEmpty(n.Solid);
        // zones-wording (Joost 2026-10-09: "114 + 52" did not say which part is which): with a size, each part says it at the number, small and
        // quiet: "114 before install + 52 since install", the words at a third of the number (never under the floor)
        internal static Rich LayeredText(Block n, Color faded, Color? solid = null, float size = 0)
        {
            Rich Word(string w) => size > 0 ? (Rich.Empty.Bold() + Rich.Plain(" " + w).Italic()).Sized(Mathf.Max(PanelLook.MinText, Mathf.Round(size * 0.32f))) : Rich.Empty;
            var since = Rich.Plain(n.Solid) + Word(PanelModel.SinceWord);
            return (Rich.Plain(n.Faded) + Word(PanelModel.BeforeWord) + " +").Ink(Hex(faded)) + " " + (solid.HasValue ? since.Ink(Hex(solid.Value)) : since);
        }

        // "since install" after a number Hearthwoven counted on this PC (Block.SinceInstall): small, italic, faint. No icons
        // beside numbers (Joost 2026-10-08): your character's and fellow players' counts carry nothing
        static TextMeshProUGUI Since(RectTransform row, float size)
        {
            var t = Label(row, PanelModel.SinceInstallLabel, size, PanelLook.Faint, align: TextAlignmentOptions.MidlineLeft, style: FontStyles.Italic);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        // a flat bar or tile: the kit's grey meter fill, multiplied by its colour
        static Image Fill(Transform parent, string name, Color colour)
        {
            var img = Kit(parent, name, "meter-fill"); img.color = colour; img.preserveAspect = false; return img;
        }

        // ----- composition: what is it made of? one proportional bar; the legend under it, left-aligned -----

        static void Composition(RectTransform col, Block b)
        {
            if (!string.IsNullOrEmpty(b.Title) || !string.IsNullOrEmpty(b.Value))
            {
                var head = Line(col, 8);
                if (plated) Sect(head, b.Title, b.Value);   // on the plate: "WOOD BROUGHT IN 3,305" (vocab.css .sect + .tot)
                else
                {
                    if (!string.IsNullOrEmpty(b.Title)) Label(head, b.Title, 18, PanelLook.Gold);
                    if (!string.IsNullOrEmpty(b.Value)) Label(head, b.Value, 18, PanelLook.Text, style: FontStyles.Bold);
                }
                if (!string.IsNullOrEmpty(b.Note) && b.Note != PanelModel.FadedKey) Label(head, b.Note, 13, PanelLook.Faint);   // its qualifier beside the total ("after your armour"); the faded key ends the legend
                if (!string.IsNullOrEmpty(b.Text)) { var q = Label(head, b.Text, 13, PanelLook.Faint, style: FontStyles.Italic); q.textWrappingMode = TextWrappingModes.NoWrap; q.rectTransform.pivot = new Vector2(0, 0.5f); }   // a caveat beside the total (the pickup gap)
                if (b.SinceInstall) Since(head, 14);
            }
            var parts = (b.Items ?? new List<Block>()).Where(p => p.Fraction > 0).ToList();
            if (parts.Count == 0) return;
            var bar = Kit(col, "Composition", "meter-track"); Size(bar, -1, b.Tone == PanelModel.Thin ? 20 : 38); if (b.Tone == "single") bar.gameObject.SetActive(false);   // 32 px of fill (the grain's native height), inset 3; thin: 14 px (fix2 7)
            // one fill per kind (PanelModel.BarParts: the smallest kind keeps at least 4 px, as in the proto), 2 px of the dark
            // track between kinds and nowhere else. A layered kind (K1) is that one fill with its part counted before
            // Hearthwoven veiled first (a flat quad, no edge of its own), the exact count since install bright after it: the
            // faded to solid change inside a kind draws no line (fix2 3: in game the faded and solid pieces each brought their
            // fill's edges, so lines stood inside kinds and read as misplaced separators)
            var spans = PanelModel.BarParts(parts.Select(p => p.Fraction).ToArray(), parts.Select(p => p.Fraction2).ToArray(), 4f / Column);
            for (int k = 0; k < parts.Count; k++)
            {
                float left = k == 0 ? 3 : 1, right = k == parts.Count - 1 ? -3 : -1;   // 2 px between kinds
                var f = PartFill(bar.transform, parts[k]);
                PlacePart(f.rectTransform, spans[k].from, spans[k].to, left, right);
                if (spans[k].fadedTo <= spans[k].from) continue;
                var veil = Img(f.transform, "Faded", null, FadedVeil).rectTransform;
                veil.anchorMin = Vector2.zero; veil.anchorMax = new Vector2((spans[k].fadedTo - spans[k].from) / (spans[k].to - spans[k].from), 1);
                veil.offsetMin = veil.offsetMax = Vector2.zero;
            }
            // legend (round 3): swatch = a small copy of its segment, number, label; entries wrap at the column's edge. The item
            // picture only when every part has one and no part has a pattern (a grain swatch already is the picture), so one
            // legend never mixes parts with and without
            var pictures = parts.Where(p => p.Id != PanelModel.FoldId).All(p => p.Pattern == null && PanelLook.Icon(p.Icon) != null);   // the folded "Other (n kinds)" has no picture of its own
            var lines = VStack(col, 6); lines.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            RectTransform line = null; float used = 0;
            foreach (var p in parts)
            {
                var entry = Line(lines, 7);
                var w = Swatch(entry, p);
                if (pictures && p.Id != PanelModel.FoldId) { Marker(entry, p.Icon, 22); w += 7 + 22; }
                else if (p.Icon != null && p.Icon.StartsWith("vocab:")) { Size(VocabImg(entry, "Mark", VocabName(p.Icon), PanelLook.Muted), 20, 20); w += 7 + 20; }   // a part the game has no picture for
                var n = Label(entry, p.Value, 22, PanelLook.Text, style: FontStyles.Bold); n.textWrappingMode = TextWrappingModes.NoWrap;
                var t = Label(entry, p.Title, 15, PanelLook.Muted); t.textWrappingMode = TextWrappingModes.NoWrap;
                w += 7 + n.preferredWidth + 7 + t.preferredWidth;
                if (p.SinceInstall) { var s = Since(entry, 13); w += 7 + s.preferredWidth; }   // a part counted on this PC in a bar of other counts
                if (line == null || used + 22 + w > Column) { line = Line(lines, 22); used = 0; } else used += 22;
                entry.SetParent(line, false); used += w;
                Size(entry, w, 28);
            }
            if (b.Note == PanelModel.FadedKey)   // what the faded parts are, as the legend's last entry
            {
                var chip = Line(lines, 0); var w = FadedChip(chip);
                if (line == null || used + 22 + w > Column) { line = Line(lines, 22); used = 0; } else used += 22;
                chip.SetParent(line, false); used += w;
                Size(chip, w, 28);
            }
        }

        // the part counted before Hearthwoven (K1): the same colour at 55 %, never a darker one (diff-05). Drawn as one fill per
        // kind (fix2 3: no line inside a kind) with the dark track's colour at 45 % over its faded part, which reads as that fill
        // at 55 % over the track
        const float FadedAlpha = 0.55f;
        static readonly Color FadedVeil = new Color(0.035f, 0.027f, 0.02f, 1f - FadedAlpha);
        static void PlacePart(RectTransform r, float from, float to, float left, float right)
        {
            r.anchorMin = new Vector2(from, 0); r.anchorMax = new Vector2(to, 1); r.pivot = new Vector2(0, 0.5f);
            r.offsetMin = new Vector2(left, 3); r.offsetMax = new Vector2(right, -3);
        }

        // a neutral grain ("grain-wood-n", base ~0.85 grey) is multiplied by the part's colour, lifted by 1 / 0.85 so the grain
        // averages to the approved colour instead of darkening it; an authored grain stays white
        const float GrainBase = 0.85f;
        static Color GrainTint(string grain, Block part)
        {
            if (!grain.EndsWith("-n")) return Color.white;
            var c = Hex(part.Colour, Color.white);
            return new Color(Mathf.Clamp01(c.r / GrainBase), Mathf.Clamp01(c.g / GrainBase), Mathf.Clamp01(c.b / GrainBase), c.a);
        }

        // a part's fill: its grain tiled at native height, or the kit's meter fill in its colour
        static Image PartFill(Transform parent, Block part)
        {
            var grain = VocabName(part.Pattern);
            return grain != null && PanelLook.Vocab(grain) ? VocabImg(parent, "Grain", grain, GrainTint(grain, part)) : Fill(parent, "Part", Hex(part.Colour, PanelLook.Accent));
        }

        static float Swatch(RectTransform row, Block part)
        {
            var grain = VocabName(part.Pattern);
            if (grain != null && PanelLook.Vocab(grain))
            {
                // the segment's own grain at half size (placed at native 60 x 32, scaled uniformly), so it reads as the same wood
                var box = Node("Swatch", row); Size(box, 30, 16);
                var g = VocabImg(box, "Grain", grain, GrainTint(grain, part)).rectTransform;
                g.anchorMin = g.anchorMax = g.pivot = Vector2.zero; g.anchoredPosition = Vector2.zero; g.sizeDelta = new Vector2(60, 32); g.localScale = Vector3.one * 0.5f;
                return 30;
            }
            Size(Fill(row, "Swatch", Hex(part.Colour, PanelLook.Accent)), 14, 14);
            return 14;
        }

        // ----- biomes: where? the journey as an axis; dealt rises, received hangs; deaths under, bosses on the tile -----

        const float Up = 50, Down = 26, ValueH = 20, TileH = 58, DeathH = 20, BossRing = 34;

        static void BiomeStrip(RectTransform col, Block b, Func<string, Action> link = null)
        {
            var tiles = (b.Items ?? new List<Block>()).Where(t => t.Kind == "biome" || PanelLook.BiomeKey(t.Id) != null).ToList();
            if (tiles.Count == 0) return;
            // nothing measured in the window (found biomes only, perhaps a boss): just the tiles, no legend, no room for bars
            var quiet = tiles.All(t => string.IsNullOrEmpty(t.Value) && string.IsNullOrEmpty(t.Value2) && t.Count == 0);
            if (!quiet)
            {
                var legend = Line(col, 8);   // no source marks: the Battle page states its time window once
                // narrowed to one biome (b.Title: "in the Swamp"): the totals are that biome's, and say so
                var scope = string.IsNullOrEmpty(b.Title) ? "" : " " + b.Title;
                Key(legend, PanelLook.Dealt, PanelModel.DealtLabel + scope, PanelModel.DealtQualifier, b.Value);
                Size(Node("Gap", legend), 18, 1);
                Key(legend, PanelLook.Received, PanelModel.ReceivedLabel + scope, string.IsNullOrEmpty(b.Text) ? PanelModel.ReceivedQualifier : b.Text, b.Value2);
            }

            var strip = Node("Biomes", col); var hangs = tiles.Any(t => t.Fraction2 > 0 || t.Count > 0);   // nothing under any tile: no room kept for it
            Size(strip, -1, quiet ? TileH + 4 : ValueH + Up + TileH + (hangs ? Down + ValueH + 4 : 6));
            var at = strip;
            if (quiet) { at = Node("Lift", strip); at.Box(0, 2 - ValueH - Up, Column, ValueH + Up + TileH + 4); }   // the tiles at the strip's top
            int n = tiles.Count;
            var apart = n > 1 && PanelLook.BiomeKey(tiles[n - 1].Id) == "ocean" ? 12f : 0f;   // the sea apart from the journey
            const float Gap = 4;
            var w = Mathf.Min(150f, (Column - Gap * (n - 1) - apart) / n);
            float x = 0;
            var chosen = tiles.Any(t => t.Selected);   // a biome is chosen: the others step back
            for (int i = 0; i < n; i++)
            {
                if (i == n - 1) x += apart;
                var idle = !quiet && string.IsNullOrEmpty(tiles[i].Value) && string.IsNullOrEmpty(tiles[i].Value2) && tiles[i].Count == 0;   // nothing in this window: dim, a dash for the number
                var holder = Node("Col", at); holder.Stretch();
                var back = chosen && !tiles[i].Selected && tiles[i].Note != "cursor";   // the filter keys' cursor stays bright
                if (back || idle) holder.gameObject.AddComponent<CanvasGroup>().alpha = idle && !back ? 0.6f : 0.4f;
                // a strip that is a filter (b.Id = the filter's id, BattleFacets.cs): a tile with something in the window is pressed to choose it
                var pick = !string.IsNullOrEmpty(b.Id) && !quiet && (!idle || tiles[i].Selected) ? link?.Invoke(PanelModel.FacetLink(b.Id, "biome", tiles[i].Id)) : null;
                BiomeTile(holder, tiles[i], x, w, idle, pick);
                x += w + Gap;
            }

            var deaths = tiles.Where(t => t.Count > 0).ToList();
            var bosses = tiles.SelectMany(t => (t.Items ?? new List<Block>()).Where(c => c.Kind == "boss")).ToList();
            if (deaths.Count == 0 && bosses.Count == 0) return;
            var key = Line(col, 6);
            if (deaths.Count > 0) { Size(VocabImg(key, "Death", "death", Color.white), 14, 14); Label(key, PanelModel.DiedHere, 14, PanelLook.Muted); }
            if (deaths.Count > 0 && bosses.Count > 0) Size(Node("Gap", key), 14, 1);
            if (bosses.Count > 0) { Size(VocabImg(key, "Boss", "boss-mark", PanelLook.Gold), 16, 16); Label(key, PanelModel.BossDefeated, 14, PanelLook.Muted); }
        }

        static void Key(RectTransform row, Color colour, string label, string qualifier, string total = null)
        {
            Size(Fill(row, "Swatch", colour), 14, 14);
            if (!string.IsNullOrEmpty(total)) Label(row, total, 15, PanelLook.Text, style: FontStyles.Bold);   // the window's total (slice 3)
            Label(row, label, 15, PanelLook.Text);
            if (!string.IsNullOrEmpty(qualifier)) Label(row, qualifier, 13, PanelLook.Muted);   // 13 px reads at 4.5:1 on the plate only in the muted tone
        }

        static void BiomeTile(RectTransform strip, Block t, float x, float w, bool idle = false, Action pick = null)
        {
            float tileTop = ValueH + Up, below = tileTop + TileH;
            if (t.Fraction > 0)   // dealt rises from the tile, its number on top
            {
                var h = Mathf.Max(3f, Mathf.Clamp01(t.Fraction) * Up);   // fix4: at least 3 px, so a small share still reads as a bar (the number says the rest)
                Fill(strip, "Dealt", PanelLook.Dealt).rectTransform.Box(x + w * 0.28f, tileTop - h, w * 0.44f, h);
                Value(strip, t.Value, x, tileTop - h - ValueH, w, PanelLook.Text);
            }
            if (idle) Value(strip, "–", x, tileTop - ValueH, w, PanelLook.Faint);
            if (t.Selected) Img(strip, "Chosen", null, PanelLook.Gold).rectTransform.Box(x - 2, tileTop - 2, w + 4, TileH + 4);
            if (t.Note == "cursor") { var fb = Node("Cursor", strip); fb.Box(x, tileTop, w, TileH); FocusRing(fb); }   // the filter keys' cursor: the soft rounded focus ring, never the square outline a chosen tile has
            var tile = Fill(strip, "Tile", Hex(t.Colour, PanelLook.BiomeTile(t.Id)));
            tile.rectTransform.Box(x, tileTop, w, TileH);
            if (pick != null) { tile.raycastTarget = true; tile.gameObject.AddComponent<Press>().Act = pick; }
            var bosses = (t.Items ?? new List<Block>()).Where(c => c.Kind == "boss").ToList();
            // boss mark inside the tile, top left: Codex's ring with the game's own trophy in its aperture (25/64 of the ring)
            for (int k = 0; k < bosses.Count; k++)
            {
                var holder = Node("Boss", tile.transform); holder.Box(2 + k * (BossRing - 2), 2, BossRing, BossRing);
                var trophy = PanelLook.Icon(bosses[k].Icon);
                if (trophy)   // the trophy fills the ring (Joost: it was tiny inside it); the ring is its frame, drawn over it
                {
                    var tr = Img(holder, "Trophy", trophy, Color.white).rectTransform; var size = BossRing * 0.86f;
                    tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(size, size);
                }
                VocabImg(holder, "Ring", "boss-ring", Color.white).rectTransform.Stretch();
            }
            var emblemX = (w - 30) / 2; if (bosses.Count > 0) emblemX = Mathf.Max(emblemX, 2 + bosses.Count * (BossRing - 2) + 2);
            VocabImg(tile.transform, "Emblem", VocabName(t.Icon) ?? PanelLook.BiomeEmblem(t.Id), Color.white).rectTransform.Box(emblemX, 5, 30, 30);
            var ink = t.Tone == "dark-text" ? DarkInk : t.Tone == "light-text" ? LightInk : PanelLook.BiomeInk(t.Id);
            var name = Label(tile.transform, t.Title ?? PanelLook.BiomeName(t.Id), 13, ink, style: FontStyles.Bold, align: TextAlignmentOptions.Top);
            name.rectTransform.Box(2, 37, w - 4, 18); name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;

            var y = below;
            if (t.Fraction2 > 0)   // received hangs from the tile
            {
                var h = Mathf.Max(3f, Mathf.Clamp01(t.Fraction2) * Down);
                Fill(strip, "Received", PanelLook.Received).rectTransform.Box(x + w * 0.28f, below, w * 0.44f, h);
                y = below + h;
            }
            if (!string.IsNullOrEmpty(t.Value2) || t.Count > 0)   // under it, on one line: its number, and where you died (the death mark with the count)
            {
                var row = Node("Hang", strip); row.Box(x, y + 4, w, ValueH);
                var h = row.gameObject.AddComponent<HorizontalLayoutGroup>(); Layout(h, 10, TextAnchor.MiddleCenter);
                if (!string.IsNullOrEmpty(t.Value2)) Label(row, t.Value2, 16, PanelLook.ReceivedText, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
                if (t.Count > 0)
                {
                    var d = Node("Deaths", row); Layout(d.gameObject.AddComponent<HorizontalLayoutGroup>(), 4, TextAnchor.MiddleCenter);
                    Size(VocabImg(d, "Death", "death", Color.white), 16, 16);
                    Label(d, t.Count.ToString(), 14, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
                }
            }
        }

        static void Value(RectTransform strip, string text, float x, float top, float w, Color colour)
        {
            if (string.IsNullOrEmpty(text)) return;
            var v = Label(strip, text, 16, colour, style: FontStyles.Bold, align: TextAlignmentOptions.Center);
            v.rectTransform.Box(x, top, w, ValueH); v.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // ----- ladders: how far along? one small ladder per skill, height = level, grouped; glow = practised since install -----

        // fix-rest: the ladders at three quarters of the sprites' size (they were half: 30 x 84 on a plate with 60 % of it empty); ten skills still
        // share one line of the stone zone (10 x 72 + 3 x 24 = 792 of 808)
        const float LadderCol = 72, GroupGap = 24, LadderScale = 0.75f, LadderBodyX = 26;

        static void Ladders(RectTransform col, Block b)
        {
            var groups = (b.Items ?? new List<Block>()).Where(g => g.Items != null && g.Items.Count > 0).ToList();
            var skills = groups.SelectMany(g => g.Items).ToList();
            if (skills.Count == 0) return;
            if (IsHeadStrip(b)) return;   // drawn as a chip in the heading row (HeadRight)
            if (b.Tone == PanelModel.SkillStripTone) { SkillStrip(col, groups); return; }
            // no "height = level" key: a ladder reads as a level by itself (Joost 2026-10-08); the glow is not obvious, so its
            // key stays, and only when some skill glows
            if (skills.Any(s => s.Practised))
            {
                // the halo behind the climber and the hearth's flame under the level say "practised since install"; the key counts them
                var key = Line(col, 6);
                Size(VocabImg(key, "Glow", "glow-soft", Color.white), 22, 22);
                Size(VocabImg(key, "Flame", "src-hearth", Color.white), 16, 16);
                Label(key, PanelModel.PractisedKey + ": " + skills.Count(s => s.Practised) + " of " + skills.Count + " skills", 15, PanelLook.Text);   // the key itself says "since install"
            }
            // every column the same width; a group longer than a line wraps, and groups share a line while they fit
            var perLine = Mathf.FloorToInt(Column / LadderCol);
            RectTransform line = null; float used = 0;
            foreach (var g in groups)
                for (int i = 0; i < g.Items.Count; i += perLine)
                {
                    var chunk = g.Items.Skip(i).Take(perLine).ToList(); var w = chunk.Count * LadderCol;
                    if (line == null || used + GroupGap + w > Column) { Spacer(col, 6); line = Row(col, GroupGap); used = 0; } else used += GroupGap;
                    used += w;
                    var group = VStack(line, 6); Size(group, w, -1);
                    var head = Label(group, i == 0 ? g.Title : "", 13, PanelLook.Muted, style: FontStyles.UpperCase);
                    head.characterSpacing = 14; head.textWrappingMode = TextWrappingModes.NoWrap; Size(head, -1, 18);
                    Size(Img(group, "Rule", null, PanelLook.Rule), -1, 1);
                    var cols = Row(group, 0);
                    foreach (var s in chunk) SkillColumn(cols, s);
                }
        }

        // the skill beside a page's deed: small, at the bottom of the plate. The same ladder picture at a third of its size (20 x 54),
        // the game's skill icon and name, the level under them; entries wrap at the column's edge
        const float StripScale = 0.32f, StripH = 54, StripLeft = 4;
        static void SkillStrip(RectTransform col, List<Block> groups)
        {
            foreach (var g in groups)
            {
                // the heading stands to the left of the entries, so the strip is one line tall when the skills fit on one; a strip with a
                // scope (Battle: "your character, now") has its heading and scope on a line of their own, the entries a full width under it
                var wide = !string.IsNullOrEmpty(g.Text);
                var stack = wide ? VStack(col, 4) : Row(col, 14);
                var head = Label(stack, g.Title, 13, PanelLook.Muted, style: FontStyles.UpperCase, align: TextAlignmentOptions.TopLeft);
                head.characterSpacing = 14; head.textWrappingMode = TextWrappingModes.NoWrap;
                float headW = 0;
                if (wide)
                {
                    var top = Line(stack, 10); head.transform.SetParent(top, false);
                    var scope = Label(top, g.Text, 13, PanelLook.Faint); scope.textWrappingMode = TextWrappingModes.NoWrap;
                }
                else { headW = Mathf.Ceil(head.preferredWidth) + 4; var hl = head.gameObject.AddComponent<LayoutElement>(); hl.minWidth = hl.preferredWidth = headW; hl.minHeight = hl.preferredHeight = 22; }
                var lines = VStack(stack, 8); lines.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
                lines.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                var room = wide ? Column : Column - headW - 14;
                RectTransform line = null; float used = 0;
                foreach (var s in g.Items)
                {
                    var c = Node("Skill", lines);
                    var ladder = LadderPicture(c, 60, 168, s); ladder.Box(StripLeft, 0, 60, 168); ladder.localScale = Vector3.one * StripScale;
                    float lx = StripLeft + 60 * StripScale + 10;
                    var icon = Marker(c, s.Icon, 22, layout: false); icon.Box(lx, 4, 22, 22);
                    var name = Label(c, s.Title, 15, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); name.textWrappingMode = TextWrappingModes.NoWrap;
                    var nw = Mathf.Ceil(name.preferredWidth) + 4; name.rectTransform.Box(lx + 28, 4, nw, 22);
                    var level = Label(c, (wide ? PanelModel.LevelWord + " " : "") + s.Value, 17, s.Level > 0 ? PanelLook.Gold : PanelLook.Faint, style: s.Level > 0 ? FontStyles.Bold : FontStyles.Normal, align: TextAlignmentOptions.MidlineLeft);
                    level.textWrappingMode = TextWrappingModes.NoWrap; level.rectTransform.Box(lx, 30, wide ? 100 : 60, 22);
                    var w = lx + 28 + nw + 18;
                    if (line == null || used + w > room) { line = Row(lines, 0); used = 0; }
                    c.SetParent(line, false); used += w; Size(c, w, StripH);
                }
            }
        }

        // ladder 45 x 126 (built at the sprites' native 60 x 168, scaled by three quarters), the game's skill icon, name, level; a skill
        // practised since install carries the hearth's flame beside its level
        // fix3-rest: the overview's ladders at 84 px (they were 126), the game's skill icon and the level on one line under the ladder, the name under that,
        // so all four groups (Fight, Gather, Move, Make) stand above the fold
        const float LadderH = 112;   // built at 60 x 112, scaled by three quarters: 45 x 84
        static void SkillColumn(RectTransform row, Block s)
        {
            var h = LadderH * LadderScale;
            var c = Node("Skill", row); Size(c, LadderCol, h + 6 + 24 + 2 + 18);
            var ladder = LadderPicture(c, 60, LadderH, s); ladder.Box(LadderBodyX, 0, 60, LadderH); ladder.localScale = Vector3.one * LadderScale;
            var icon = Marker(c, s.Icon, 22, layout: false); icon.Box(4, h + 7, 22, 22);
            var level = Label(c, s.Value, 16, s.Level > 0 ? PanelLook.Gold : PanelLook.Faint, style: s.Level > 0 ? FontStyles.Bold : FontStyles.Normal, align: TextAlignmentOptions.MidlineRight);
            level.rectTransform.Box(26, h + 6, 28, 24); level.textWrappingMode = TextWrappingModes.NoWrap;
            if (s.Practised) VocabImg(c, "Flame", "src-hearth", Color.white).rectTransform.Box(56, h + 10, 16, 16);
            var name = Label(c, s.Title, 14, PanelLook.Text, align: TextAlignmentOptions.Top); name.rectTransform.Box(-8, h + 32, LadderCol + 16, 18);
            name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Overflow;
        }

        // one ladder, 0 at the foot and 100 at the top: two rails, a rung every ten levels (amber once reached), the reach as
        // a soft fill, the climber's mark at level + progress pointing right, the glow behind it when practised since install
        static RectTransform LadderPicture(RectTransform parent, float w, float h, Block s)
        {
            var root = Node("Ladder", parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1); root.sizeDelta = new Vector2(w, h);
            var at = Mathf.Clamp(s.Level + Mathf.Max(0f, s.Progress), 0f, 100f) / 100f * h;   // progress -1 (unknown): the level alone
            if (at > 0) Fill(root, "Reach", new Color(PanelLook.Dealt.r, PanelLook.Dealt.g, PanelLook.Dealt.b, 0.36f)).rectTransform.Bottom(8, 0, w - 16, at);
            for (int v = 10; v <= 100; v += 10)
                VocabImg(root, "Rung", "ladder-rung", v <= s.Level ? PanelLook.RungDone : PanelLook.RungAhead).rectTransform
                    .Bottom(8, Mathf.Clamp(v / 100f * h, 8f, h - 8f) - 8, w - 16, 16);
            VocabImg(root, "Rail", "ladder-rail", PanelLook.Rail).rectTransform.Bottom(0, 0, 16, h);
            VocabImg(root, "Rail", "ladder-rail", PanelLook.Rail).rectTransform.Bottom(w - 16, 0, 16, h);
            if (s.Practised) VocabImg(root, "Glow", "glow-soft", Color.white).rectTransform.Bottom(-18 - 32, at - 32, 64, 64);
            VocabImg(root, "Climber", "ladder-mark", Color.white).rectTransform.Bottom(-34, at - 16, 32, 32);
            return root;
        }

        // ----- ladder: where am I on this skill? one large ladder; level, progress to the next, practice since install -----

        static void Ladder(RectTransform col, Block b, Func<string, Action> link)
        {
            var row = Row(col, 30);
            var left = Node("Scale", row); Size(left, 150, 288);   // 280 px of ladder (fix-rest: the skill page must fit the plate with its practice card below)
            var ladder = LadderPicture(left, 80, 280, b); ladder.Box(62, 4, 80, 280);
            foreach (var v in new[] { 0, 50, 100 })
            {
                var l = Label(left, v.ToString(), 13, PanelLook.Faint, align: TextAlignmentOptions.MidlineRight);
                l.rectTransform.Box(0, 4 + 280 - v / 100f * 280 - 8, 22, 16);
            }

            // the level is the page's hero (fix-rest: it was a small 54 px number beside an empty half card): the number at the hero
            // size, the progress to the next level as a wide bar with both levels at its ends
            var right = VStack(row, 12); right.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var hero = Line(right, 12, TextAnchor.LowerLeft);
            Label(hero, b.Value, HeroSize, PanelLook.Gold, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            Label(hero, PanelModel.LevelWord, HeroLabel, PanelLook.Text); if (b.SinceInstall) Since(hero, 15);
            if (b.Progress >= 0)
            {
                var head = Line(right, 10);
                Label(head, PanelModel.ProgressTo + " " + b.Value2, 18, PanelLook.Muted);
                Label(head, Mathf.RoundToInt(Mathf.Clamp01(b.Progress) * 100f) + "%", 20, PanelLook.Gold, style: FontStyles.Bold);
                var track = Kit(right, "Progress", "meter-track"); Size(track, -1, 28);
                if (b.Progress > 0)
                {
                    var fr = Fill(track.transform, "Fill", PanelLook.Dealt).rectTransform;
                    fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(Mathf.Clamp01(b.Progress), 1); fr.offsetMin = new Vector2(3, 3); fr.offsetMax = new Vector2(-3, -3);
                }
                var ends = Line(right, 0);
                Label(ends, b.Value, 14, PanelLook.Faint);
                Size(Node("Fill", ends), 1, 1).flexibleWidth = 1;
                Label(ends, b.Value2, 14, PanelLook.Faint);
            }
            foreach (var c in b.Items ?? new List<Block>())
            {
                if (c.Kind == "practice")
                {
                    var p = Line(right, 12);
                    Size(VocabImg(p, "Glow", "glow-soft", Color.white), 30, 30);
                    Label(p, c.Value, 28, PanelLook.Gold, style: FontStyles.Bold);
                    Label(p, c.Title, 15, PanelLook.Muted); if (!string.IsNullOrEmpty(c.Note)) Label(p, c.Note, 13, PanelLook.Faint); if (c.SinceInstall) Since(p, 14);
                }
                else if (c.Kind == "link") LinkChip(right, c, link);
            }
        }

        // a shortcut to the page that owns the deed: the kit's row with the title, clickable when a link is given
        static void LinkChip(RectTransform parent, Block target, Func<string, Action> link)
        {
            var holder = Line(parent, 0);
            var click = link?.Invoke(target.Id);
            var img = Kit(holder, "Link", "row", raycast: click != null);
            var hasIcon = PanelLook.Icon(target.Icon) != null;
            var label = Label(img.transform, target.Title, 18, PanelLook.Gold, align: TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap; label.rectTransform.Stretch();
            label.rectTransform.offsetMin = new Vector2(hasIcon ? 52 : 18, 0); label.rectTransform.offsetMax = new Vector2(-16, 0);
            if (hasIcon) { var m = Marker(img.rectTransform, target.Icon, 28, layout: false); m.anchorMin = m.anchorMax = m.pivot = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(14, 0); }
            Size(img, label.preferredWidth + (hasIcon ? 70 : 36), 48);
            if (click == null) return;
            var button = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = img; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => click());
        }

        // ---------- page layout (slice 3): hero, columns, switch, cards, plate; ported from vocab.css .hero2/.cols2/.sect,
        // r4battle.css .r4-seg and r4over.css .dk. Flat tinted rects, the kit's meter track sliced, text: built once
        // when the page is filled, nothing per frame. ----------

        // a heading on the plate: small capitals, letter-spaced, muted; a total beside it in bold (vocab.css .sect, .tot)
        static void Sect(RectTransform row, string title, string total)
        {
            if (!string.IsNullOrEmpty(title))
            {
                var t = Label(row, title, 13, PanelLook.Muted, style: FontStyles.UpperCase);
                t.characterSpacing = 14; t.textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (!string.IsNullOrEmpty(total)) Label(row, total, 15, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
        }

        // a small chip (the heading row's pill, a view switch, the window choices): the kit's meter track as its dark
        // ground, an amber fill and dark bold text when chosen; clickable when given a click. Returns its width.
        /// <summary>The side padding of the window chips in the heading row (nine of them since HISTORY-06: tighter than other chips, so the
        /// heading keeps the window's full name; the preview's .chip.win).</summary>
        const float WindowPad = 8;

        static float Chip(RectTransform row, string text, string icon, bool on, Action click, float height = 26, Action back = null, bool off = false, float pad = 11)
        {
            var img = Kit(row, "Chip", "meter-track", raycast: click != null);
            if (off) img.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;   // greyed (the preview's .chip.off): offered nowhere, drawn so the row keeps its shape
            if (on) { var f = Fill(img.transform, "On", PanelLook.Accent).rectTransform; f.Stretch(); f.offsetMin = new Vector2(2, 2); f.offsetMax = new Vector2(-2, -2); }
            var hasIcon = PanelLook.Icon(icon) != null;
            var label = Label(img.transform, text, 14, on ? DarkInk : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft, style: on ? FontStyles.Bold : FontStyles.Normal);
            label.textWrappingMode = TextWrappingModes.NoWrap; label.rectTransform.Stretch();
            label.rectTransform.offsetMin = new Vector2(hasIcon ? 32 : pad, 0); label.rectTransform.offsetMax = new Vector2(-pad, 0);
            if (hasIcon) { var m = Marker(img.rectTransform, icon, 18, layout: false); m.anchorMin = m.anchorMax = m.pivot = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(9, 0); }
            var w = Mathf.Ceil(label.preferredWidth) + (hasIcon ? 32 + pad : 2 * pad);
            Size(img, w, height);
            if (click != null || back != null) { var press = img.gameObject.AddComponent<Press>(); press.Act = click; press.Back = back; }   // left: next, right: previous
            return w;
        }

        // ----- hero: how much? the big number and its label; further numbers on the right, smaller, after a thin rule -----

        const float HeroSize = 84, HeroLabel = 26, HeroSecond = 54, HeroSecondLabel = 20;

        static void Hero(RectTransform col, Block b)
        {
            var compact = b.Tone == PanelModel.Compact;   // one modest line (Hall > Trader, Smelters; fix2 7)
            float size = compact ? 40 : HeroSize, label = compact ? 18 : HeroLabel, second = compact ? 28 : HeroSecond, secondLabel = compact ? 15 : HeroSecondLabel, gap = compact ? 20 : 34;
            var row = Line(col, 0, TextAnchor.LowerLeft);
            HeroNumber(row, b, size, label);
            foreach (var n in b.Items ?? new List<Block>())
            {
                Size(Node("Gap", row), gap, 1);
                Size(Img(row, "Rule", null, PanelLook.Rule), 1, second * 0.8f);
                Size(Node("Gap", row), gap, 1);
                HeroNumber(row, n, second, secondLabel);
            }
        }

        // the number in gold and bold, its label on the number's baseline (lifted by the bigger font's descent), then a
        // quiet qualifier and the "since install" label when this number was counted on this PC
        static void HeroNumber(RectTransform row, Block n, float size, float labelSize)
        {
            var group = Line(row, 14, TextAnchor.LowerLeft);
            var num = RichLabel(group, "hero", Layered(n) ? LayeredText(n, PanelLook.Faint, null, size) : Rich.Plain(n.Value), size, PanelLook.Gold, style: FontStyles.Bold); num.textWrappingMode = TextWrappingModes.NoWrap;
            var words = Line(group, 8, TextAnchor.LowerLeft);
            words.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, Mathf.RoundToInt((size - labelSize) * 0.22f));
            Label(words, n.Title, labelSize, PanelLook.Text).textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(n.Note)) Label(words, n.Note, 13, PanelLook.Faint).textWrappingMode = TextWrappingModes.NoWrap;
            if (n.SinceInstall) Since(words, 14);
            // zones-wording: a layered number says "before install" and "since install" at its parts (LayeredText), so the fix4 chip under it is gone
        }

        // ----- columns: stretches of blocks side by side, equal widths, 34 px apart (vocab.css .cols2) -----

        const float ColumnGap = 34;

        static void Columns(RectTransform col, Block b, Action<RectTransform, Block> child)
        {
            var cols = (b.Items ?? new List<Block>()).Where(c => c.Items != null && c.Items.Count > 0).ToList();
            if (cols.Count == 0) return;
            var outer = Column; var w = (outer - ColumnGap * (cols.Count - 1)) / cols.Count;
            var row = Row(col, ColumnGap);
            try
            {
                Column = w;
                foreach (var c in cols)
                {
                    var stack = VStack(row, 12); Size(stack, w, -1);
                    foreach (var x in c.Items) child(stack, x);
                }
            }
            finally { Column = outer; }
        }

        // ----- switch: the chips top right (r4battle .r4-seg), an optional caption on the left, then the chosen view -----

        static void Switch(RectTransform col, Block b, Func<string, Action> link, Action<RectTransform, Block> child)
        {
            var views = b.Items ?? new List<Block>();
            var top = Line(col, 12); Size(top, -1, 28);
            var caption = Label(top, b.Title ?? "", 15, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
            caption.textWrappingMode = TextWrappingModes.NoWrap; caption.overflowMode = TextOverflowModes.Ellipsis;
            caption.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            if (!string.IsNullOrEmpty(b.KeyCap)) Size(Keycap(top, b.KeyCap), 22, 22);   // the key that turns the view (fix4: Earned / Unsung on Deeds), as quiet as the other caps
            var chips = Line(top, 2, TextAnchor.MiddleRight);
            var chosen = Math.Max(0, views.FindIndex(x => x.Selected));
            Block Step(int d) { for (int k = 1; k <= views.Count; k++) { var x = views[((chosen + d * k) % views.Count + views.Count) % views.Count]; if (x.Tone != PanelModel.OffTone) return x; } return views[chosen]; }   // a greyed view is skipped
            foreach (var v in views)
            {
                var off = v.Tone == PanelModel.OffTone;   // greyed (Together's day window before the history), as the heading row's window chips:
                // inert, or, when it works later (Waits), pressing it chooses it and one line says from when (B17)
                Chip(chips, v.Title, null, v.Selected, off ? (v.Waits ? link?.Invoke(PanelModel.ViewLink(b, v)) : null) : link?.Invoke(PanelModel.ViewLink(b, v.Selected ? Step(1) : v)), back: off ? null : link?.Invoke(PanelModel.ViewLink(b, Step(-1))), off: off);
            }
            foreach (var v in views.Where(v => v.Selected)) foreach (var x in v.Items ?? new List<Block>()) child(col, x);
        }

        // ----- cards: a shelf of deeds (r4over deeds A); title with its icon, the big number, its label, one small line -----

        const float CardH = 128, UnsungCardH = 92, CardGap = 10;   // a name not earned yet has no numbers: its card is shorter, so all of them show

        static void Cards(RectTransform col, Block b, Func<string, Action> link)
        {
            var cards = b.Items ?? new List<Block>();
            if (cards.Count == 0) return;
            var per = Mathf.Clamp(Mathf.FloorToInt((Column + CardGap) / (170 + CardGap)), 1, 4);
            var w = Mathf.Floor((Column - CardGap * (per - 1)) / per);
            var grid = Node("Cards", col);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            var cellH = cards.All(x => x.Tone == "unsung") ? UnsungCardH : CardH;
            g.cellSize = new Vector2(w, cellH); g.spacing = new Vector2(CardGap, CardGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = per;
            foreach (var c in cards)
            {
                var click = string.IsNullOrEmpty(c.Id) ? null : link?.Invoke(c.Id);
                var card = Kit(grid, c.Title, "meter-track", raycast: click != null);   // its dark ground and edge (.dk)
                if (c.Tone == "unsung")   // not earned yet: the same card dimmed, what earns it in place of the numbers
                {
                    // dimmed: the picture and the name only; what earns it stays at body contrast (4.5:1 on the card)
                    var ut = Line(card.rectTransform, 8); ut.Box(12, 6, w - 24, 30);
                    if (PanelLook.Icon(c.Icon) != null) { var um = Marker(ut, c.Icon, 24); um.gameObject.AddComponent<CanvasGroup>().alpha = 0.6f; }
                    var un = Label(ut, c.Title, 16, PanelLook.Muted); un.textWrappingMode = TextWrappingModes.NoWrap; un.overflowMode = TextOverflowModes.Ellipsis;
                    var d = Label(card.transform, c.Text, 15, PanelLook.Text, align: TextAlignmentOptions.TopLeft); d.rectTransform.Box(12, 42, w - 24, cellH - 50);
                    d.textWrappingMode = TextWrappingModes.Normal; d.overflowMode = TextOverflowModes.Ellipsis;
                    if (click != null) { var ub = card.gameObject.AddComponent<UnityEngine.UI.Button>(); ub.targetGraphic = card; ub.transition = Selectable.Transition.None; ub.onClick.AddListener(() => click()); }
                    continue;
                }
                var top = Line(card.rectTransform, 8); top.Box(12, 6, w - 24, 30);
                if (PanelLook.Icon(c.Icon) != null) Marker(top, c.Icon, 24);
                var t = Label(top, c.Title, 16, PanelLook.Gold); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
                var num = Label(card.transform, c.Value, 38, PanelLook.Gold, style: FontStyles.Bold, align: TextAlignmentOptions.BottomLeft);
                num.rectTransform.Box(12, 34, w - 24, 42); num.textWrappingMode = TextWrappingModes.NoWrap;
                var lab = Line(card.rectTransform, 6); lab.Box(12, 78, w - 24, 20);
                Shrinks(Label(lab, c.Text, 15, PanelLook.Text));
                if (c.SinceInstall) Keeps(Since(lab, 12));
                var sub = (c.Items ?? new List<Block>()).FirstOrDefault();
                if (sub != null)
                {
                    var s = Line(card.rectTransform, 6); s.Box(12, 100, w - 24, 20);
                    Keeps(Label(s, sub.Value, 14, PanelLook.Text, style: FontStyles.Bold));
                    Shrinks(Label(s, sub.Title, 13, PanelLook.Muted));
                    if (sub.SinceInstall) Keeps(Since(s, 12));
                }
                if (click == null) continue;
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = card; button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => click());
            }
        }

        // on a card's line the words give way (ellipsis) and the number and "since install" keep their whole width
        static void Shrinks(TextMeshProUGUI t)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
            var le = Size(t, -1, -1); le.minWidth = 0; le.flexibleWidth = 1; le.preferredWidth = t.preferredWidth;
        }
        static void Keeps(TextMeshProUGUI t) { t.textWrappingMode = TextWrappingModes.NoWrap; Size(t, Mathf.Ceil(t.preferredWidth), -1); }

        // ----- plate inside a page (a second plate, or the probe): its heading row and the recessed plate as one block.
        // The page's own plate is drawn by Fill into the content column instead (heading row on the list title's line). -----

        static void PlateBlock(RectTransform col, Block b, Action<RectTransform, Block> child)
        {
            var head = Line(col, 10); Size(head, -1, 36);
            if (PanelLook.Icon(b.Icon) != null) Marker(head, b.Icon, 26);
            var title = Label(head, b.Title ?? "", 24, PanelLook.Gold); title.textWrappingMode = TextWrappingModes.NoWrap;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            if (!string.IsNullOrEmpty(b.Pill)) Chip(head, b.Pill, b.PillIcon, false, null);
            var box = VStack(col, 14);
            box.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(PlatePadX, PlatePadX, PlatePadTop, PlatePadBottom);
            var fill = Img(box, "Plate", null, PlateColour); fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; fill.rectTransform.Stretch();
            var edge = Kit(box, "PlateEdge", "meter-track"); edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; edge.rectTransform.Stretch();
            if (!string.IsNullOrEmpty(b.Text)) Label(box, b.Text, 15, PanelLook.Muted, style: FontStyles.Italic);
            var outer = Column; var was = plated;
            try { Column = outer - 2 * PlatePadX; plated = true; foreach (var x in b.Items ?? new List<Block>()) child(box, x); }
            finally { Column = outer; plated = was; }
        }

        // ---------- kit controls ----------

        // a chapter tab: the kit's tab with its chapter icon above the name, amber when chosen
        void Tab(Choice c, Action click)
        {
            var img = Kit(chapters, c.Label, c.Selected ? "tab-selected" : "tab", raycast: true);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = TabH; le.flexibleWidth = 1;
            // A2: the icon (26 px) left of the label (18 px), centred on one line
            var line = Node("Line", img.transform); line.Stretch();
            Layout(line.gameObject.AddComponent<HorizontalLayoutGroup>(), 8, TextAnchor.MiddleCenter);
            var icon = PanelLook.Icon(c.Icon);
            var ivory = (c.Icon ?? "").StartsWith("vocab:") ? new Color(0.86f, 0.82f, 0.75f) : Color.white;   // a white line mask (the Feats tab) in the kit icons' ivory
            if (icon) Size(Img(line, "Icon", icon, c.Selected ? Amber : ivory), 26, 26);
            var label = Label(line, c.Label, 18, c.Selected ? PanelLook.Gold : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (c.Dot) FeatDot(img.rectTransform);   // earned feats not seen yet (Chapters/FeatsUi.cs)
            Clickable(img, click);
        }

        // the kit's toggle: radio baked into its fixed 60 px left strip, height kept at 72
        void Toggle(RectTransform row, Choice t, Action click)
        {
            var img = Kit(row, t.Label, t.Selected ? "toggle-selected" : "toggle", raycast: true);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 72; le.flexibleWidth = 1;
            var label = Label(img.transform, t.Label, 21, t.Selected ? PanelLook.Gold : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
            label.rectTransform.Stretch(); label.rectTransform.offsetMin = new Vector2(62, 0); label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            Clickable(img, click);
        }

        // a list row, player chip, filter or button: the kit's row, lit when chosen; icon (or a person's shield) left of the text
        GameObject Entry(RectTransform parent, string text, string icon, bool selected, Action click, float height, bool stretch = false)
        {
            var img = Kit(parent, text, selected ? "row-selected" : "row", raycast: true);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = height;
            var hasIcon = !string.IsNullOrEmpty(icon) && (icon.StartsWith("person:") || PanelLook.Icon(icon) != null);
            var size = height < 34 ? height - 10 : height <= RowH ? Mathf.Min(22, height - 14) : height - 20;   // A2: a list row's line icon is 22 px (the compact rows of a long list: 18)
            var label = Label(img.transform, text, height >= 60 ? 22 : height < 34 ? 16 : 18, selected ? PanelLook.Gold : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.Stretch();
            var pad = height <= RowH ? 10f : 14f;
            label.rectTransform.offsetMin = new Vector2(hasIcon ? pad + size + 10 : 20, 0); label.rectTransform.offsetMax = new Vector2(-16, 0);
            if (hasIcon)
            {
                var m = Marker(img.rectTransform, icon, size, layout: false); m.anchorMin = m.anchorMax = m.pivot = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(pad, 0);
                // a list line icon (ADDENDUM-6) is a white mask: it takes the row text's colour, gold when chosen
                if (icon.StartsWith("vocab:")) foreach (var im in m.GetComponentsInChildren<Image>()) im.color = ListIconGold;   // as the Deeds title icons
            }
            // preferred: the whole text; a row that runs out of width shrinks it (ellipsis) instead of spilling past the edge
            if (!stretch) { le.preferredWidth = label.preferredWidth + (hasIcon ? size + 46 : 40); le.minWidth = Mathf.Min(le.preferredWidth, (hasIcon ? size + 46 : 40) + 48); }
            Clickable(img, click);
            return img.gameObject;
        }

        void Clickable(Image img, Action click)
        {
            var button = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = img; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Safe(click)());
        }

        // ---------- small UGUI helpers ----------

        static void Layout(HorizontalLayoutGroup h, float spacing, TextAnchor align)
        {
            h.spacing = spacing; h.childAlignment = align; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        }

        static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Clear(RectTransform r) { for (int i = r.childCount - 1; i >= 0; i--) Destroy(r.GetChild(i).gameObject); r.DetachChildren(); }

        static RectTransform Row(RectTransform parent, float spacing) { var row = Node("Row", parent); Layout(row.gameObject.AddComponent<HorizontalLayoutGroup>(), spacing, TextAnchor.UpperLeft); return row; }

        static RectTransform VStack(RectTransform parent, float spacing)
        {
            var s = Node("Stack", parent);
            var v = s.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = spacing; v.childControlWidth = v.childControlHeight = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            return s;
        }

        static void Spacer(RectTransform parent, float height) { var le = Node("Space", parent).gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = height; }

        static Image Img(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.raycastTarget = raycast; img.preserveAspect = sprite != null;
            return img;
        }

        // a kit sprite at white tint (its colour is authored), nine-sliced where the kit gives borders
        static Image Kit(Transform parent, string name, string sprite, bool raycast = false)
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            img.sprite = PanelLook.Ui(sprite); img.color = img.sprite ? Color.white : PanelLook.Slot; img.raycastTarget = raycast;
            if (PanelLook.Sliced(sprite)) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            else img.preserveAspect = true;
            return img;
        }

        static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, bool title = false,
                                     TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal, TMP_FontAsset face = null)
        {
            var t = Node("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            var font = face ? face : title ? PanelLook.Title : PanelLook.Body;
            if (font) t.font = font;
            t.text = text ?? ""; t.fontSize = Mathf.Max(size, PanelLook.MinText); t.color = color; t.alignment = align; t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false; t.richText = false;
            return t;
        }

        /// <summary>A label of TMP rich text (richtext-fix): the tags made only by <see cref="Rich"/> from the model's parts, model text kept literal,
        /// and rich text on for the block kinds <see cref="PanelRich.Kinds"/> lists. A kind left off that list draws its tags as letters, here and
        /// in the HTML bridge alike, and the self-check's "markup" line fails on it: the list is the one switch.</summary>
        static TextMeshProUGUI RichLabel(Transform parent, string kind, Rich text, float size, Color color, bool title = false,
                                         TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal, TMP_FontAsset face = null)
        {
            var t = Label(parent, text.Markup, size, color, title, align, style, face);
            t.richText = PanelRich.On(kind);
            return t;
        }

        /// <summary>Gives a label that exists already rich text made by <see cref="Rich"/> (the switch as in <see cref="RichLabel"/>).</summary>
        static void SetRich(TextMeshProUGUI t, string kind, Rich text) { t.text = text.Markup; t.richText = PanelRich.On(kind); }

        // the game's sprite for an icon reference; a person gets the kit's shield in their colour; a missing game sprite
        // leaves the place empty (the kit asks not to stand in letters or new art for game objects)
        static RectTransform Marker(RectTransform parent, string icon, float size, bool layout = true)
        {
            var box = Node("Marker", parent);
            box.sizeDelta = new Vector2(size, size);
            if (layout) { var le = box.gameObject.AddComponent<LayoutElement>(); le.minWidth = le.preferredWidth = le.minHeight = le.preferredHeight = size; }
            if (icon != null && icon.StartsWith("person:"))
            {
                var name = icon.Substring(7);
                var shield = Img(box, "Shield", PanelLook.Ui("shield"), PanelLook.ShieldTint(personColors.TryGetValue(name, out var pi) ? pi : 0));
                shield.rectTransform.Stretch();
                return box;
            }
            var sprite = PanelLook.Icon(icon);
            if (sprite) Img(box, "Icon", sprite, PanelLook.GoldLine(icon) ? PanelLook.Gold : Color.white).rectTransform.Stretch();
            return box;
        }
    }

    /// <summary>
    /// Draws vocabulary blocks off screen and destroys them again, for the self-test (selftest/SelfTest.cs): no canvas, no
    /// player, no game state and no click targets, so it is safe on a dedicated server, where the panel itself never opens.
    /// </summary>
    /// <summary>
    /// A chip acts when it is pressed, not on the click that follows: a chip on the scrolling plate (the view switch) lost
    /// its click in game (Joost 2026-10-08: Levels/Practised did nothing while the time chips in the heading row worked).
    /// A click is dropped once the pointer moves a few pixels over a ScrollRect, which takes it as the start of a drag, or
    /// when the page is rebuilt between press and release; a press has neither problem.
    /// </summary>
    // a chip that acts on press: the left button its own act (the next view or biome), the right button the previous one
    /// <summary>A focus ring (PanelUi.FocusRing): whether its box holds the focus; KeyFocus decides whether it shows.</summary>
    sealed class FocusMark : MonoBehaviour { public bool Focused; }

    sealed class Press : MonoBehaviour, IPointerDownHandler
    {
        public Action Act, Back;
        public void OnPointerDown(PointerEventData e)
        {
            if (e != null) PanelUi.SetKeyFocus(false);   // a click is mouse use: the focus ring hides before the page redraws
            if (e != null && e.button == PointerEventData.InputButton.Right) Back?.Invoke();
            else if (e == null || e.button == PointerEventData.InputButton.Left) Act?.Invoke();
        }
    }

    public static class PanelProbe
    {
        /// <summary>Draws a view switch and presses its first chip that is not chosen; the target that press asks for
        /// ("view:&lt;switch&gt;=&lt;view&gt;"), or null when no chip reacts.</summary>
        public static string PressSwitch(Block sw)
        {
            var root = new GameObject("HearthwovenProbe", typeof(RectTransform));
            try
            {
                string asked = null;
                PanelUi.DrawVocab((RectTransform)root.transform, sw, t => () => asked = t);
                var press = root.GetComponentsInChildren<Press>(true).FirstOrDefault();
                press?.OnPointerDown(null);
                return asked;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>The number of UI objects the blocks placed; throws whatever a renderer throws.</summary>
        public static int Draw(IEnumerable<Block> blocks)
        {
            var root = new GameObject("HearthwovenProbe", typeof(RectTransform));
            try
            {
                var col = (RectTransform)root.transform; col.sizeDelta = new Vector2(797, 800);
                foreach (var b in blocks)
                    if (!PanelUi.DrawVocab(col, b, null)) throw new ArgumentException("not a vocabulary block: " + b.Kind);
                return root.GetComponentsInChildren<RectTransform>(true).Length - 1;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>Vocabulary sprites named in the embedded manifest that do not load (empty: all of them decode).</summary>
        public static List<string> MissingSprites() => PanelLook.VocabNames().Where(n => PanelLook.Vocab(n) == null).ToList();
    }

    static class RectExtensions
    {
        public static void Stretch(this RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        // a fixed box hanging from the top-left corner
        public static void Box(this RectTransform r, float x, float top, float width, float height)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -top); r.sizeDelta = new Vector2(width, height);
        }

        public static void Bottom(this RectTransform r, float x, float bottom, float width, float height, bool right = false)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(right ? 1 : 0, 0);
            r.anchoredPosition = new Vector2(x, bottom); r.sizeDelta = new Vector2(width, height);
        }
    }
}
