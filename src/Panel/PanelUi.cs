using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The Hearthwoven panel in game: a compact dark menu over the world that draws whatever PanelModel.Build returns.
    /// Built on first open, so a player who never presses the key gets no UI objects at all. Reads only; changes nothing.
    /// While open it reports itself as a store window (PanelHooks), which is how the game itself frees the cursor and
    /// keeps movement, camera and hotbar from reacting to keys; the panel reads its own keys meanwhile.
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
        TextMeshProUGUI owner, heading, scope, shareNote, keys;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; if (root) Destroy(root); }

        // ---------- keys ----------

        static bool Key(KeyCode k) => ZInput.GetKeyDown(k, false);
        static bool Button(string b) => ZInput.GetButtonDown(b);

        void Update()
        {
            try
            {
                if (!open)
                {
                    if (hiddenFrames < 99) hiddenFrames++;
                    if (Enabled.Value && Pressed() && CanOpen()) Open();
                    return;
                }
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

        void Open()
        {
            PanelLook.Resolve();
            if (!root) Build();
            root.SetActive(true);
            open = true; hiddenFrames = 0; shown = null;
            GroupShare.Request();   // fellow players' stats, if you share; rate-limited inside
            Render(true);
        }

        void Close()
        {
            open = false; hiddenFrames = 0;
            if (root) root.SetActive(false);
        }

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
                    if (go) { var ch = go.GetComponent<Character>(); if (ch) token = ch.m_name; var pc = go.GetComponent<Piece>(); if (token == null && pc) token = pc.m_name; }
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
            if (!parsed.TryGetValue(name, out var hit) || !ReferenceEquals(hit.Key, json))
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

        const float W = 1180, H = 760, ListW = 270;

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
            root.AddComponent<GraphicRaycaster>();

            // the panel only: the world stays visible around it
            var bg = Img(root.transform, "Panel", null, PanelLook.Panel, raycast: true);
            frame = bg.rectTransform;
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(W, H);
            frame.localScale = Vector3.one * Mathf.Clamp(Scale.Value, 0.6f, 1.4f);
            var edge = bg.gameObject.AddComponent<Outline>(); edge.effectColor = PanelLook.Edge; edge.effectDistance = new Vector2(2, -2);

            var title = Label(frame, "HEARTHWOVEN", 30, PanelLook.Gold, title: true);
            title.rectTransform.Box(24, 14, 300, 40);
            owner = Label(frame, "", 18, PanelLook.Muted);
            owner.rectTransform.Box(300, 24, 300, 28);

            players = Node("Players", frame);
            players.anchorMin = players.anchorMax = players.pivot = new Vector2(1, 1);
            players.anchoredPosition = new Vector2(-20, -14); players.sizeDelta = new Vector2(640, 36);
            Layout(players.gameObject.AddComponent<HorizontalLayoutGroup>(), 6, TextAnchor.MiddleRight);

            chapters = Node("Chapters", frame);
            chapters.anchorMin = new Vector2(0, 1); chapters.anchorMax = new Vector2(1, 1); chapters.pivot = new Vector2(0.5f, 1);
            chapters.offsetMin = new Vector2(20, -130); chapters.offsetMax = new Vector2(-20, -60);
            var cl = chapters.gameObject.AddComponent<HorizontalLayoutGroup>(); Layout(cl, 6, TextAnchor.MiddleCenter); cl.childForceExpandWidth = true;

            list = Scroller("List", frame, 20, 140, ListW, 50);
            var right = Node("Content", frame);
            right.anchorMin = Vector2.zero; right.anchorMax = Vector2.one;
            right.offsetMin = new Vector2(20 + ListW + 18, 50); right.offsetMax = new Vector2(-20, -140);
            heading = Label(right, "", 28, PanelLook.Gold); heading.rectTransform.Box(0, 0, 820, 36); heading.textWrappingMode = TextWrappingModes.NoWrap; heading.overflowMode = TextOverflowModes.Ellipsis;
            scope = Label(right, "", 15, PanelLook.Muted); scope.rectTransform.Box(0, 38, 820, 22); scope.textWrappingMode = TextWrappingModes.NoWrap; scope.overflowMode = TextOverflowModes.Ellipsis;
            content = Scroller("Blocks", right, 0, 68, -1, 0);

            shareNote = Label(frame, "", 13, PanelLook.Muted, style: FontStyles.Italic, align: TextAlignmentOptions.MidlineLeft);
            shareNote.rectTransform.Bottom(24, 10, 560, 30);
            keys = Label(frame, "", 14, PanelLook.Muted, align: TextAlignmentOptions.MidlineRight);
            keys.rectTransform.Bottom(-24, 10, 560, 30, right: true);
            root.SetActive(false);
        }

        // a vertical list that scrolls inside its box (mouse wheel), never drawing over its neighbours
        RectTransform Scroller(string name, RectTransform parent, float left, float top, float width, float bottom)
        {
            var box = Node(name, parent);
            box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(width < 0 ? 1 : 0, 1);
            box.offsetMin = new Vector2(left, bottom); box.offsetMax = new Vector2(width < 0 ? 0 : left + width, -top);
            box.gameObject.AddComponent<RectMask2D>();
            Img(box, "Catch", null, new Color(0, 0, 0, 0), raycast: true).rectTransform.Stretch();   // lets the wheel reach the scroll area
            var inner = Node("Items", box);
            inner.anchorMin = new Vector2(0, 1); inner.anchorMax = new Vector2(1, 1); inner.pivot = new Vector2(0.5f, 1);
            inner.offsetMin = inner.offsetMax = Vector2.zero;
            var v = inner.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = 8; v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            inner.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = box.gameObject.AddComponent<ScrollRect>();
            scroll.content = inner; scroll.viewport = box; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f; scroll.inertia = false;
            return inner;
        }

        // ---------- filling it from the model ----------

        void Fill(PanelView v)
        {
            var pageKey = v.Active + "/" + v.Page + "/" + state.Player;
            var newPage = pageKey != lastPage; lastPage = pageKey;

            owner.text = v.Owner;
            heading.text = v.Heading ?? "";
            scope.text = v.Scope ?? "";
            shareNote.text = v.ShareNote ?? "";
            keys.text = string.Join("    ", v.Keys.ToArray());

            Clear(players);
            const int perPage = 5;
            var others = v.Players.Skip(1).ToList();
            if (playerPage * perPage >= others.Count) playerPage = 0;
            if (v.Players.Count > 0) PlayerChip(v.Players[0]);
            foreach (var c in others.Skip(playerPage * perPage).Take(perPage)) PlayerChip(c);
            if (others.Count > perPage) Chip(players, "More", "", false, () => { playerPage++; Render(true); }, 0, 34);

            Clear(chapters);
            foreach (var c in v.Chapters)
            {
                var id = (Chapter)Enum.Parse(typeof(Chapter), c.Id);
                var cell = Chip(chapters, c.Label, c.Icon, c.Selected, () => { state.Chapter = id; Render(true); }, 0, 64, vertical: true);
                cell.GetComponent<LayoutElement>().flexibleWidth = 1;
            }

            Clear(list);
            foreach (var c in v.List)
            {
                var id = c.Id; var chapter = v.Active;
                var row = Chip(list, c.Label, c.Icon, c.Selected, () => { state.Page[chapter] = id; Render(true); }, 0, 52);
                row.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
            }

            Clear(content);
            if (v.Badges.Count > 0)
            {
                var badges = Row(content, 14);
                foreach (var b in v.Badges) { var r = Row(badges, 6); Marker(r, b.Icon, b.Label, 28); Label(r, b.Label, 16, PanelLook.Gold); }
            }
            if (v.HasFilters)
            {
                var row = Row(content, 6);
                foreach (var w in v.Windows) { var id = (TimeWindow)Enum.Parse(typeof(TimeWindow), w.Id); Chip(row, w.Label, "", w.Selected, () => { state.Window = id; Render(true); }, 0, 30); }
                var at = Math.Max(0, v.Biomes.FindIndex(c => c.Selected));
                var biomes = v.Biomes;
                Chip(row, "Biome: " + biomes[at].Label, "", !string.IsNullOrEmpty(biomes[at].Id), () => { state.Biome = biomes[(at + 1) % biomes.Count].Id; Render(true); }, 0, 30);
            }
            if (v.Toggle.Count > 0)
            {
                var row = Row(content, 10);
                foreach (var t in v.Toggle) { var they = t.Id == "they"; Chip(row, t.Label, "", t.Selected, () => { state.TheyReceived = they; Render(true); }, 0, 38); }
            }
            foreach (var b in v.Blocks) Draw(content, b);
            if (!string.IsNullOrEmpty(v.HowCounted))
            {
                Spacer(content, 6);
                var how = Chip(content, (v.ShowHow ? "Hide" : "Show") + " how this was counted [I]", "", false, () => { state.ShowHow = !state.ShowHow; Render(true); }, 0, 28);
                how.GetComponent<LayoutElement>().flexibleWidth = 0;
                if (v.ShowHow) Label(content, v.HowCounted, 14, PanelLook.Muted);
            }
            if (newPage) { Canvas.ForceUpdateCanvases(); content.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 1f; }
        }

        void PlayerChip(Choice c)
        {
            var id = c.Id;
            Chip(players, c.Label, c.Icon, c.Selected, () => { state.Player = id; Render(true); }, 0, 34);
        }

        void Draw(RectTransform col, Block b)
        {
            switch (b.Kind)
            {
                case "section": Spacer(col, 4); Label(col, b.Title, 17, PanelLook.Gold); break;
                case "stat": Stat(col, b); break;
                case "tiles": Tiles(col, b.Items, 7); Note(col, b.Note); break;
                case "bars": Bars(col, b); Note(col, b.Note); break;
                case "rows": Rows(col, b); Note(col, b.Note); break;
                case "titles": Titles(col, b); break;
                case "thread": Thread(col, b); break;
                case "empty":
                    Label(col, b.Title, 18, PanelLook.Muted, style: FontStyles.Italic);
                    if (!string.IsNullOrEmpty(b.Text)) Label(col, b.Text, 15, PanelLook.Faint);
                    break;
                default:   // note, or a hint before setting out
                    if (b.Tone == "hint")
                    {
                        var row = Row(col, 10);
                        var mark = Img(row, "Mark", null, PanelLook.Accent); var le = mark.gameObject.AddComponent<LayoutElement>(); le.minWidth = le.preferredWidth = 3; le.flexibleHeight = 1;
                        Label(row, b.Text, 15, PanelLook.Text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                    }
                    else Label(col, b.Text, 14, PanelLook.Muted);
                    break;
            }
        }

        void Note(RectTransform col, string note) { if (!string.IsNullOrEmpty(note)) Label(col, note, 12.5f, PanelLook.Faint); }

        void Stat(RectTransform col, Block b)
        {
            var row = Row(col, 12);
            if (!string.IsNullOrEmpty(b.Icon)) Marker(row, b.Icon, b.Title, 40);
            var texts = VStack(row, 0); texts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var line = Row(texts, 8);
            line.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerLeft;
            if (!string.IsNullOrEmpty(b.Value)) Label(line, b.Value, 26, PanelLook.Text);
            Label(line, b.Title, 18, PanelLook.Text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            if (!string.IsNullOrEmpty(b.Text)) Label(texts, b.Text, 15, PanelLook.Muted);
            if (!string.IsNullOrEmpty(b.Note)) Label(texts, b.Note, 12.5f, PanelLook.Faint);
        }

        // item slots like the inventory's: the game's own sprite, its name and count beneath
        void Tiles(RectTransform col, List<Block> items, int columns)
        {
            var grid = Node("Tiles", col);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(100, 128); g.spacing = new Vector2(12, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = columns;
            foreach (var t in items ?? new List<Block>())
            {
                var cell = Node(t.Title, grid);
                var slot = Img(cell, "Slot", null, PanelLook.Slot); var o = slot.gameObject.AddComponent<Outline>(); o.effectColor = PanelLook.SlotEdge; o.effectDistance = new Vector2(1, -1);
                slot.rectTransform.anchorMin = slot.rectTransform.anchorMax = slot.rectTransform.pivot = new Vector2(0.5f, 1);
                slot.rectTransform.anchoredPosition = Vector2.zero; slot.rectTransform.sizeDelta = new Vector2(84, 84);
                var m = Marker(slot.rectTransform, t.Icon, t.Title, 66, layout: false); m.anchorMin = m.anchorMax = m.pivot = new Vector2(0.5f, 0.5f); m.anchoredPosition = Vector2.zero;
                var name = Label(cell, t.Title, 13, PanelLook.Text, align: TextAlignmentOptions.Top); name.rectTransform.Box(0, 88, 100, 18); name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
                if (!string.IsNullOrEmpty(t.Value)) { var count = Label(cell, t.Value, 17, PanelLook.Gold, align: TextAlignmentOptions.Top); count.rectTransform.Box(0, 106, 100, 22); }
            }
        }

        // one scale per block, lengths computed from the numbers; elemental types carry the game's status icon
        void Bars(RectTransform col, Block b)
        {
            foreach (var i in b.Items ?? new List<Block>())
            {
                var row = Row(col, 10);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                var icon = Node("Icon", row); var il = icon.gameObject.AddComponent<LayoutElement>(); il.minWidth = il.preferredWidth = 24; il.minHeight = il.preferredHeight = 24;
                if (!string.IsNullOrEmpty(i.Icon)) { var s = PanelLook.Icon(i.Icon); if (s) Img(icon, "Status", s, Color.white).rectTransform.Stretch(); }
                var label = Label(row, i.Title, 17, i.Selected ? PanelLook.Gold : PanelLook.Text); var ll = label.gameObject.AddComponent<LayoutElement>(); ll.minWidth = ll.preferredWidth = 130;
                var track = Img(row, "Track", null, PanelLook.Track); var tl = track.gameObject.AddComponent<LayoutElement>(); tl.flexibleWidth = 1; tl.minHeight = tl.preferredHeight = i.Selected ? 16 : 13;
                var fill = Img(track.transform, "Fill", null, PanelLook.Tone(i.Tone)).rectTransform;
                fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Clamp01(i.Fraction), 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
                var value = Label(row, i.Value, 17, PanelLook.Text, align: TextAlignmentOptions.MidlineRight); var vl = value.gameObject.AddComponent<LayoutElement>(); vl.minWidth = vl.preferredWidth = 80;
            }
        }

        // equal rows, never scaled to the top entry
        void Rows(RectTransform col, Block b)
        {
            foreach (var i in b.Items ?? new List<Block>())
            {
                var row = Row(col, 12);
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                row.gameObject.AddComponent<LayoutElement>().minHeight = 34;
                if (!string.IsNullOrEmpty(i.Icon)) Marker(row, i.Icon, i.Title, 30);
                Label(row, i.Title, 17, PanelLook.Text).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                var value = Label(row, i.Value, 17, PanelLook.Text, align: TextAlignmentOptions.MidlineRight); var vl = value.gameObject.AddComponent<LayoutElement>(); vl.minWidth = vl.preferredWidth = 160;
                var rule = Img(col, "Rule", null, PanelLook.Rule); var rl = rule.gameObject.AddComponent<LayoutElement>(); rl.minHeight = rl.preferredHeight = 1;
            }
        }

        // Deeds > Overview: each earned title is a shortcut to the page that owns its numbers
        void Titles(RectTransform col, Block b)
        {
            var grid = Node("Titles", col);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(268, 72); g.spacing = new Vector2(10, 8);   // six rows hold all eighteen titles without scrolling
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 3;
            foreach (var t in b.Items ?? new List<Block>())
            {
                var target = t.Id;
                var cell = Img(grid, t.Title, null, PanelLook.Slot, raycast: true);
                var o = cell.gameObject.AddComponent<Outline>(); o.effectColor = PanelLook.SlotEdge; o.effectDistance = new Vector2(1, -1);
                var button = cell.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = cell;
                button.onClick.AddListener(() => { PanelModel.Jump(state, target); Render(true); });
                var m = Marker(cell.rectTransform, t.Icon, t.Title, 48, layout: false); m.anchorMin = m.anchorMax = m.pivot = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(10, 0);
                var name = Label(cell.transform, t.Title, 17, PanelLook.Gold); name.rectTransform.Box(68, 5, 194, 22);
                var value = Label(cell.transform, t.Value, 13.5f, PanelLook.Text); value.rectTransform.Box(68, 27, 194, 19); value.textWrappingMode = TextWrappingModes.NoWrap; value.overflowMode = TextOverflowModes.Ellipsis;
                var note = Label(cell.transform, t.Note, 11f, PanelLook.Faint); note.rectTransform.Box(68, 47, 194, 17); note.textWrappingMode = TextWrappingModes.NoWrap; note.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        // maker -> food -> eater: labels on both ends, the foods with their game sprite and count along a thin line
        void Thread(RectTransform col, Block b)
        {
            var top = Row(col, 0);
            Label(top, b.Title, 16, PanelLook.Muted).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Label(top, b.Text, 16, PanelLook.Muted, align: TextAlignmentOptions.TopRight).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var line = Img(col, "Thread", null, PanelLook.Accent); var ll = line.gameObject.AddComponent<LayoutElement>(); ll.minHeight = ll.preferredHeight = 2;
            Tiles(col, b.Items, 7);
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

        // the game's sprite for an icon reference; a person gets a coloured disc with an initial; anything else unknown, an initial
        static RectTransform Marker(RectTransform parent, string icon, string label, float size, bool layout = true)
        {
            var box = Node("Marker", parent);
            box.sizeDelta = new Vector2(size, size);
            if (layout) { var le = box.gameObject.AddComponent<LayoutElement>(); le.minWidth = le.preferredWidth = le.minHeight = le.preferredHeight = size; }
            var sprite = PanelLook.Icon(icon);
            if (sprite) { Img(box, "Icon", sprite, Color.white).rectTransform.Stretch(); return box; }
            var person = icon != null && icon.StartsWith("person:");
            var disc = Img(box, "Disc", person ? PanelLook.Circle : null, person ? PanelLook.PersonColor(icon.Substring(7)) : PanelLook.Slot);
            disc.rectTransform.Stretch();
            var letter = Label(box, string.IsNullOrEmpty(label) ? "?" : label.Substring(0, 1).ToUpperInvariant(), size * 0.5f, person ? Color.white : PanelLook.Gold, title: true, align: TextAlignmentOptions.Center);
            letter.rectTransform.Stretch();
            return box;
        }

        // a selectable entry: chapter (icon over label), list row, chip or toggle; the chosen one is lit like the game's selection
        GameObject Chip(RectTransform parent, string text, string icon, bool selected, Action click, float width, float height, bool vertical = false)
        {
            var img = Img(parent, text, null, selected ? PanelLook.Selected : PanelLook.Slot, raycast: true);
            var o = img.gameObject.AddComponent<Outline>(); o.effectColor = selected ? PanelLook.Accent : PanelLook.SlotEdge; o.effectDistance = new Vector2(1, -1);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.minHeight = le.preferredHeight = height;
            var hasIcon = !string.IsNullOrEmpty(icon);
            var label = Label(img.transform, text, vertical ? 15 : 16, selected ? PanelLook.Gold : PanelLook.Text, align: vertical ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.Stretch();
            if (hasIcon)
            {
                var size = vertical ? 32f : height - 12;
                var m = Marker(img.rectTransform, icon, text, size, layout: false);
                if (vertical) { m.anchorMin = m.anchorMax = m.pivot = new Vector2(0.5f, 1); m.anchoredPosition = new Vector2(0, -6); label.rectTransform.offsetMin = new Vector2(4, 6); }
                else { m.anchorMin = m.anchorMax = m.pivot = new Vector2(0, 0.5f); m.anchoredPosition = new Vector2(8, 0); label.rectTransform.offsetMin = new Vector2(size + 16, 0); label.rectTransform.offsetMax = new Vector2(-8, 0); }
            }
            if (width > 0) le.minWidth = le.preferredWidth = width;
            else if (!vertical) le.preferredWidth = le.minWidth = label.preferredWidth + (hasIcon ? height + 20 : 28);
            var button = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => click());
            return img.gameObject;
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
