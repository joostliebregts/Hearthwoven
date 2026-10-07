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
    public class PanelUi : MonoBehaviour
    {
        internal static PanelUi Instance;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyCode> Hotkey;
        internal static ConfigEntry<float> Scale;
        internal static DateTime? SessionStart;
        static int hiddenFrames = 99;
        // Like the game's own windows: still "visible" for a frame after closing, so the Escape that closed it does not
        // also open the main menu.
        internal static bool Blocking => Instance != null && (Instance.open || hiddenFrames <= 1);

        internal static void BindConfig(ConfigFile config)
        {
            if (Enabled != null) return;
            Enabled = config.Bind("Panel", "Enabled", true, "Show the Hearthwoven panel on the hotkey.");
            Hotkey = config.Bind("Panel", "Hotkey", KeyCode.H, "Key that opens and closes the Hearthwoven panel. H is unbound in Valheim and in the group's other mods.");
            Scale = config.Bind("Panel", "Scale", 1f, "Size of the panel (0.6 to 1.4).");
        }

        readonly PanelState state = new PanelState();
        PanelView view;
        bool open;
        float nextRefresh;
        string shown, lastPage;
        int playerPage;
        GameObject root;
        RectTransform frame, players, chapters, list, content;
        TextMeshProUGUI owner, heading, scope, keys, listTitle;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; if (root) Destroy(root); }

        // ---------- keys ----------

        static bool Key(KeyCode k) => ZInput.GetKeyDown(k, false);
        static bool Button(string b) => ZInput.GetButtonDown(b);

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

        void Update()
        {
            try
            {
                if (!open)
                {
                    if (hiddenFrames < 99) hiddenFrames++;
                    if (Enabled.Value && !Typing() && Pressed() && CanOpen()) Open();
                    return;
                }
                if (Typing()) return;
                if (Pressed() || Key(KeyCode.Escape) || Button("JoyButtonB") || !Player.m_localPlayer ||
                    InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || Player.m_localPlayer.IsDead())
                { Close(); return; }
                if (Button("TabLeft") || Button("JoyTabLeft")) { PanelModel.StepChapter(state, -1); Render(true); }
                else if (Button("TabRight") || Button("JoyTabRight")) { PanelModel.StepChapter(state, 1); Render(true); }
                else if (Key(KeyCode.W) || Key(KeyCode.UpArrow) || Button("JoyDPadUp")) { PanelModel.StepList(state, view, -1); Render(true); }
                else if (Key(KeyCode.S) || Key(KeyCode.DownArrow) || Button("JoyDPadDown")) { PanelModel.StepList(state, view, 1); Render(true); }
                else if (view != null && view.Toggle.Count > 0 && (Key(KeyCode.A) || Key(KeyCode.LeftArrow) || Button("JoyDPadLeft"))) { state.TheyReceived = true; Render(true); }
                else if (view != null && view.Toggle.Count > 0 && (Key(KeyCode.D) || Key(KeyCode.RightArrow) || Button("JoyDPadRight"))) { state.TheyReceived = false; Render(true); }
                else if (Key(KeyCode.I)) { state.ShowHow = !state.ShowHow; Render(true); }
                if (Time.unscaledTime >= nextRefresh) Render(false);
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel: " + e.Message); Close(); }
        }

        void LateUpdate()
        {
            // free the cursor while reading; the game takes it back on its own once nothing is open
            if (open && ZInput.IsMouseActive()) { ZCursor.LockState = CursorLockMode.None; ZCursor.Show(); }
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
            PanelLook.Resolve();
            PanelLook.RetryMissing();
            if (!root) Build();
            root.SetActive(true);
            lockBefore = ZCursor.LockState; cursorBefore = ZCursor.IsRequested;
            open = true; hiddenFrames = 0; shown = null;
            GroupShare.Request();   // fellow players' stats, if you share; rate-limited inside
            Render(true);
        }

        // give the cursor back as it was when the panel opened (playing: locked and hidden)
        void Close()
        {
            var wasOpen = open;
            open = false; hiddenFrames = 0;
            if (root) root.SetActive(false);
            if (!wasOpen) return;
            ZCursor.LockState = lockBefore;
            if (cursorBefore) ZCursor.Show(); else ZCursor.Hide();
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

        PanelInput Fellow(string name, PanelInput self)
        {
            if (string.IsNullOrEmpty(name) || !GroupShare.Sharing() || !GroupShare.Group.TryGetValue(name, out var json)) return null;
            if (!parsed.TryGetValue(name, out var hit) || !string.Equals(hit.Key, json, StringComparison.Ordinal))   // the server resends unchanged copies
                parsed[name] = hit = new KeyValuePair<string, PanelInput>(json, PanelInput.FromSnapshot(json));
            var other = hit.Value;
            if (other == null) return null;
            other.NowUtc = self.NowUtc; other.DisplayName = Localized; other.PlayerNames = self.PlayerNames; other.ViewerName = self.PlayerName;
            return other;
        }

        static PanelInput Gather()
        {
            var input = new PanelInput
            {
                NowUtc = DateTime.UtcNow, SessionStartUtc = SessionStart,
                Session = Plugin.Session, Events = Plugin.Events, Log = Plugin.Log, DisplayName = Localized,
                PlayerNames = new Dictionary<long, string>(),
            };
            var profile = Game.instance ? Game.instance.GetPlayerProfile() : null;
            if (profile != null)
            {
                input.PlayerName = profile.GetName();
                input.PlayerId = profile.GetPlayerID();
                var stats = profile.m_playerStats;
                // slot 0 only: the raw totals (the other slots overlap, see Snapshot.cs)
                if (stats != null && stats.Length > 0 && stats[0] != null)
                {
                    input.Character = stats[0].m_stats.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
                    input.ItemsCrafted = stats[0].m_itemCraftStats;
                    input.PiecesPlaced = stats[0].m_piecesPlacedStats;
                    if (stats[0].m_enemyStats != null && stats[0].m_enemyStats.Length > 0) input.EnemyKills = stats[0].m_enemyStats[0];
                }
            }
            var skills = Player.m_localPlayer ? Player.m_localPlayer.GetSkills()?.GetSkillList() : null;
            if (skills != null) input.SkillLevels = skills.ToDictionary(s => s.m_info.m_skill.ToString(), s => s.m_level);
            foreach (var p in Player.GetAllPlayers())
                if (p) input.PlayerNames[p.GetPlayerID()] = p.GetPlayerName();
            return input;
        }

        void Render(bool force)
        {
            nextRefresh = Time.unscaledTime + 2f;
            if (GroupShare.Sharing()) GroupShare.Request();   // keeps the group fresh while open (at most every 30 s)
            var self = Gather();
            self.Fellows = new List<PanelInput>();
            var subject = Fellow(state.Player, self);
            if (subject == null) state.Player = "";
            // everyone who shares, so links run both ways (they ate your food); looking at a fellow, you are one of theirs
            var fellows = GroupShare.Sharing() ? GroupShare.Group.Keys.Select(n => Fellow(n, self)).Where(f => f != null).ToList() : new List<PanelInput>();
            self.Fellows = fellows;
            if (subject != null) subject.Fellows = fellows.Where(f => f != subject).Concat(new[] { self }).ToList();
            state.Hotkey = Hotkey.Value == KeyCode.None ? "" : Hotkey.Value.ToString();
            var v = PanelModel.Build(subject ?? self, state);
            PanelModel.AddPlayers(v, self.PlayerName, GroupShare.Group.Keys, state.Player, GroupShare.Sharing());
            view = v;
            var json = PanelModel.ToJson(v);
            if (!force && json == shown) return;   // nothing new: keep what is on screen
            shown = json;
            Fill(v);
        }

        // ---------- building the frame (once) ----------
        // Layout follows the UI kit's assembly (ui-kit/preview-company.png, preview-battle.png): a 1180 x 760 nine-sliced
        // frame, header text clear of the 56 px corner ornaments, six tabs, a 268 px left list, content to the right.

        const float W = 1180, H = 760, Inset = 32, ListW = 268, Top = 180, Foot = 72;
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
            frame.localScale = Vector3.one * Mathf.Clamp(Scale.Value, 0.6f, 1.4f);

            var title = Label(frame, "HEARTHWOVEN", 32, PanelLook.Gold, title: true, align: TextAlignmentOptions.MidlineLeft);
            title.rectTransform.Box(66, 22, 300, 44);
            owner = Label(frame, "", 20, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
            owner.rectTransform.Box(385, 22, 300, 44);

            players = Node("Players", frame);
            players.anchorMin = players.anchorMax = players.pivot = new Vector2(1, 1);
            players.anchoredPosition = new Vector2(-64, -20); players.sizeDelta = new Vector2(620, 48);
            Layout(players.gameObject.AddComponent<HorizontalLayoutGroup>(), 8, TextAnchor.MiddleRight);

            chapters = Node("Chapters", frame);
            chapters.anchorMin = new Vector2(0, 1); chapters.anchorMax = new Vector2(1, 1); chapters.pivot = new Vector2(0.5f, 1);
            chapters.offsetMin = new Vector2(Inset, -155); chapters.offsetMax = new Vector2(-Inset, -77);
            var cl = chapters.gameObject.AddComponent<HorizontalLayoutGroup>(); Layout(cl, 12, TextAnchor.MiddleCenter); cl.childForceExpandWidth = true;

            listTitle = Label(frame, "", 24, PanelLook.Gold, align: TextAlignmentOptions.MidlineLeft);
            listTitle.rectTransform.Box(Inset, Top, ListW, 36);
            list = Scroller("List", frame, Inset, Top + 46, ListW, Foot, 16);

            var right = Node("Content", frame);
            right.anchorMin = Vector2.zero; right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(Inset + ListW + 43, Foot); right.offsetMax = new Vector2(-Inset - 8, -Top);
            heading = Label(right, "", 26, PanelLook.Gold, align: TextAlignmentOptions.MidlineLeft); heading.rectTransform.Box(0, 0, 790, 36);
            heading.textWrappingMode = TextWrappingModes.NoWrap; heading.overflowMode = TextOverflowModes.Ellipsis;
            scope = Label(right, "", 18, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft); scope.rectTransform.Box(0, 38, 790, 26);
            scope.textWrappingMode = TextWrappingModes.NoWrap; scope.overflowMode = TextOverflowModes.Ellipsis;
            content = Scroller("Blocks", right, 0, 76, -1, 0, 12);

            keys = Label(frame, "", 15, PanelLook.Muted, align: TextAlignmentOptions.MidlineLeft);
            keys.rectTransform.Bottom(64, 22, W - 128, 30);
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
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f; scroll.inertia = false;
            return inner;
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
            heading.text = v.Heading ?? "";
            scope.text = v.Scope ?? "";
            keys.text = string.Join("      ", v.Keys.ToArray());

            Clear(players);
            if (!string.IsNullOrEmpty(v.ShareNote))   // the sharing line sits in the header, left of the chips
            {
                var note = Label(players, v.ShareNote, 13, PanelLook.Muted, style: FontStyles.Italic, align: TextAlignmentOptions.MidlineRight);
                var nl = note.gameObject.AddComponent<LayoutElement>(); nl.preferredWidth = nl.minWidth = v.Players.Count > 0 ? 330 : 420;
            }
            const int perPage = 5;
            var others = v.Players.Skip(1).ToList();
            if (playerPage * perPage >= others.Count) playerPage = 0;
            if (v.Players.Count > 0) Entry(players, v.Players[0].Label, v.Players[0].Icon, v.Players[0].Selected, PlayerClick(v.Players[0]), 48);
            foreach (var c in others.Skip(playerPage * perPage).Take(perPage)) Entry(players, c.Label, c.Icon, c.Selected, PlayerClick(c), 48);
            if (others.Count > perPage) Entry(players, "More", "", false, () => { playerPage++; Render(true); }, 48);

            Clear(chapters);
            foreach (var c in v.Chapters)
            {
                var id = (Chapter)Enum.Parse(typeof(Chapter), c.Id);
                Tab(c, () => { state.Chapter = id; Render(true); });
            }

            Clear(list);
            var compact = v.List.Count > 5;   // long lists (Deeds, Skills) use the kit's minimum row height so they fit
            list.GetComponent<VerticalLayoutGroup>().spacing = compact ? 4 : 16;
            foreach (var c in v.List)
            {
                var id = c.Id; var chapter = v.Active;
                Entry(list, c.Label, c.Icon, c.Selected, () => { state.Page[chapter] = id; Render(true); }, compact ? 48 : 68, stretch: true);
            }

            Clear(content);
            if (v.Badges.Count > 0)
            {
                var badges = Row(content, 18);
                foreach (var b in v.Badges) { var r = Row(badges, 8); r.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft; Marker(r, b.Icon, 34); Label(r, b.Label, 18, PanelLook.Gold); }
            }
            if (v.HasFilters)
            {
                var row = Row(content, 8);
                foreach (var w in v.Windows) { var id = (TimeWindow)Enum.Parse(typeof(TimeWindow), w.Id); Entry(row, w.Label, "", w.Selected, () => { state.Window = id; Render(true); }, 48); }
                var at = Math.Max(0, v.Biomes.FindIndex(c => c.Selected));
                var biomes = v.Biomes;
                Entry(row, "Biome: " + biomes[at].Label, "", !string.IsNullOrEmpty(biomes[at].Id), () => { state.Biome = biomes[(at + 1) % biomes.Count].Id; Render(true); }, 48);
            }
            if (v.Toggle.Count > 0)
            {
                var row = Row(content, 14);
                foreach (var t in v.Toggle) { var they = t.Id == "they"; Toggle(row, t, () => { state.TheyReceived = they; Render(true); }); }
            }
            foreach (var b in v.Blocks) Draw(content, b);
            if (!string.IsNullOrEmpty(v.HowCounted))
            {
                Spacer(content, 4);
                var how = Entry(content, (v.ShowHow ? "Hide" : "Show") + " how this was counted [I]", "", false, () => { state.ShowHow = !state.ShowHow; Render(true); }, 48);
                how.GetComponent<LayoutElement>().flexibleWidth = 0;
                if (v.ShowHow) Label(content, v.HowCounted, 15, PanelLook.Muted);
            }
            if (newPage) { Canvas.ForceUpdateCanvases(); content.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1f; }
        }

        Action PlayerClick(Choice c) { var id = c.Id; return () => { state.Player = id; Render(true); }; }

        void Draw(RectTransform col, Block b)
        {
            switch (b.Kind)
            {
                case "section": Spacer(col, 4); Label(col, b.Title, 20, PanelLook.Gold); break;
                case "divider": Divider(col); break;
                case "stat": Stat(col, b); break;
                case "tiles": Tiles(col, b.Items, 6); Note(col, b.Note); break;
                case "bars": Bars(col, b); Note(col, b.Note); break;
                case "rows": Rows(col, b); Note(col, b.Note); break;
                case "titles": Titles(col, b); break;
                case "thread": Thread(col, b); Note(col, b.Note); break;
                case "link":
                    {
                        var target = b.Id;
                        var link = Entry(col, b.Title, b.Icon, false, () => { PanelModel.Jump(state, target); Render(true); }, 48);
                        link.GetComponent<LayoutElement>().flexibleWidth = 0;
                        break;
                    }
                case "empty":
                    Label(col, b.Title, 20, PanelLook.Muted, style: FontStyles.Italic);
                    if (!string.IsNullOrEmpty(b.Text)) Label(col, b.Text, 16, PanelLook.Faint);
                    break;
                default:   // note, or a hint before setting out
                    if (b.Tone == "hint")
                    {
                        var row = Row(col, 10);
                        var mark = Img(row, "Mark", null, PanelLook.Accent); var le = mark.gameObject.AddComponent<LayoutElement>(); le.minWidth = le.preferredWidth = 3; le.flexibleHeight = 1;
                        Label(row, b.Text, 17, PanelLook.Text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                    }
                    else Label(col, b.Text, 15, PanelLook.Muted);
                    break;
            }
        }

        void Note(RectTransform col, string note) { if (!string.IsNullOrEmpty(note)) Label(col, note, 13, PanelLook.Faint); }

        void Divider(RectTransform col)
        {
            var d = Kit(col, "Divider", "divider"); var le = d.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 16;   // fixed height (kit)
        }

        void Stat(RectTransform col, Block b)
        {
            var row = Row(col, 16);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            if (!string.IsNullOrEmpty(b.Icon)) Marker(row, b.Icon, 52);
            var texts = VStack(row, 0); texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var line = Row(texts, 10);
            line.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            if (!string.IsNullOrEmpty(b.Value)) Label(line, b.Value, 30, PanelLook.Text);
            Label(line, b.Title, 22, PanelLook.Text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
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
                if (!string.IsNullOrEmpty(t.Value)) { var count = Label(cell, t.Value, 19, PanelLook.Gold, align: TextAlignmentOptions.Top); count.rectTransform.Box(0, 122, 112, 26); }
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
            foreach (var i in b.Items ?? new List<Block>())
            {
                var row = Row(col, 14);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                row.gameObject.AddComponent<LayoutElement>().minHeight = 38;
                if (!string.IsNullOrEmpty(i.Icon)) Marker(row, i.Icon, 34);
                Label(row, i.Title, 19, PanelLook.Text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                var value = Label(row, i.Value, 19, PanelLook.Text, align: TextAlignmentOptions.MidlineRight); var vl = value.gameObject.AddComponent<LayoutElement>(); vl.minWidth = vl.preferredWidth = 170;
                var rule = Img(col, "Rule", null, PanelLook.Rule); var rl = rule.gameObject.AddComponent<LayoutElement>(); rl.minHeight = rl.preferredHeight = 1;
            }
        }

        // Deeds > Overview: each earned title is a shortcut to the page that owns its numbers
        void Titles(RectTransform col, Block b)
        {
            var grid = Node("Titles", col);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(258, 64); g.spacing = new Vector2(10, 6);   // six rows hold all eighteen titles
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
                Label(mid, t.Value + " " + t.Title.ToLowerInvariant(), 24, PanelLook.Text, align: TextAlignmentOptions.Center);
                var threadBox = Node("ThreadBox", mid); var tb = threadBox.gameObject.AddComponent<LayoutElement>(); tb.minHeight = tb.preferredHeight = 40;
                var thread = Kit(threadBox, "Thread", "thread").rectTransform;   // decorative direction only, uniform scale
                thread.anchorMin = thread.anchorMax = thread.pivot = new Vector2(0.5f, 0.5f); thread.sizeDelta = new Vector2(320, 40);
                var shieldBox = Node("Eater", row); var sb = shieldBox.gameObject.AddComponent<LayoutElement>(); sb.minWidth = sb.preferredWidth = 120; sb.minHeight = sb.preferredHeight = 112;
                if (first) { var m = Marker(shieldBox, b.Icon, 112, layout: false); m.anchorMin = m.anchorMax = m.pivot = new Vector2(0.5f, 0.5f); m.anchoredPosition = Vector2.zero; }
                first = false;
            }
            Divider(col);
        }

        // ---------- kit controls ----------

        // a chapter tab: the kit's tab with its chapter icon above the name, amber when chosen
        void Tab(Choice c, Action click)
        {
            var img = Kit(chapters, c.Label, c.Selected ? "tab-selected" : "tab", raycast: true);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 78; le.flexibleWidth = 1;
            var icon = PanelLook.Icon(c.Icon);
            if (icon) { var i = Img(img.transform, "Icon", icon, c.Selected ? Amber : Color.white).rectTransform; i.anchorMin = i.anchorMax = i.pivot = new Vector2(0.5f, 1); i.anchoredPosition = new Vector2(0, -10); i.sizeDelta = new Vector2(32, 32); }
            var label = Label(img.transform, c.Label, 20, c.Selected ? PanelLook.Gold : PanelLook.Text, align: TextAlignmentOptions.Bottom);
            label.rectTransform.Stretch(); label.rectTransform.offsetMin = new Vector2(4, 8); label.textWrappingMode = TextWrappingModes.NoWrap;
            Clickable(img, click);
        }

        // the kit's toggle: radio baked into its fixed 60 px left strip, height kept at 72
        void Toggle(RectTransform row, Choice t, Action click)
        {
            var img = Kit(row, t.Label, t.Selected ? "toggle-selected" : "toggle", raycast: true);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 72; le.flexibleWidth = 1;
            var label = Label(img.transform, t.Label, 21, t.Selected ? PanelLook.Gold : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
            label.rectTransform.Stretch(); label.rectTransform.offsetMin = new Vector2(62, 0); label.textWrappingMode = TextWrappingModes.NoWrap;
            Clickable(img, click);
        }

        // a list row, player chip, filter or button: the kit's row, lit when chosen; icon (or a person's shield) left of the text
        GameObject Entry(RectTransform parent, string text, string icon, bool selected, Action click, float height, bool stretch = false)
        {
            var img = Kit(parent, text, selected ? "row-selected" : "row", raycast: true);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = height;
            var hasIcon = !string.IsNullOrEmpty(icon) && (icon.StartsWith("person:") || PanelLook.Icon(icon) != null);
            var size = height - 20;
            var label = Label(img.transform, text, height >= 60 ? 22 : 18, selected ? PanelLook.Gold : PanelLook.Text, align: TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.Stretch();
            label.rectTransform.offsetMin = new Vector2(hasIcon ? size + 28 : 20, 0); label.rectTransform.offsetMax = new Vector2(-16, 0);
            if (hasIcon) { var m = Marker(img.rectTransform, icon, size, layout: false); m.anchorMin = m.anchorMax = m.pivot = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(14, 0); }
            if (!stretch) le.preferredWidth = le.minWidth = label.preferredWidth + (hasIcon ? size + 46 : 40);
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
                                     TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            var t = Node("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            var font = title ? PanelLook.Title : PanelLook.Body;
            if (font) t.font = font;
            t.text = text ?? ""; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false; t.richText = false;
            return t;
        }

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
                var shield = Img(box, "Shield", PanelLook.Ui("shield"), PanelLook.PersonColor(personColors.TryGetValue(name, out var pi) ? pi : 0));
                shield.rectTransform.Stretch();
                return box;
            }
            var sprite = PanelLook.Icon(icon);
            if (sprite) Img(box, "Icon", sprite, Color.white).rectTransform.Stretch();
            return box;
        }
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
