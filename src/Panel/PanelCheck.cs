using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The panel's half of the in-game self-check (DevCheck.cs, Dev.SelfCheck) and the filter-key clash check.
    /// - KeyClashCheck (always, once, when the panel's config binds): reads every other mod's .cfg in BepInEx/config and logs
    ///   "filter key: &lt;key&gt;, conflicts: none" or a warning naming each setting bound to the same key.
    /// - FilterKeyHeld: while the panel is open, the press of the filter key does not also open the inventory (Tab is the game's
    ///   inventory key; PanelHooks.FilterKeyNotInventory asks here). Only then.
    /// - CheckTick (Dev.SelfCheck only): the report key, and the watch on the filter key (did the inventory or a radial open too?).
    /// - The page walk after the report: every chapter, page and view of the real panel drawn as the snapshot run plans them (no
    ///   pictures), one line each: drawn without a warning, the smallest text against the 14 px floor, the focus marks, the kinds of
    ///   block that are new in 0.6; on a page with a filter bar the focus is entered once and the ring looked for. Afterwards the
    ///   panel returns to what the player had open.
    /// - richtext-fix: on every page each visible text is read as TMP shows it (GetParsedText); a text that shows a tag as letters
    ///   ("&lt;b&gt;", "&lt;color=…&gt;": rich text off on a marked-up label, the 0.6.1 Feats bug) fails the page and gets an
    ///   "HW-CHECK FAIL markup" line naming it; after the walk one "markup" line says PASS or how many pages failed.
    /// </summary>
    public partial class PanelUi
    {
        // ---------- the filter key: other mods, the inventory ----------

        static bool clashChecked;

        /// <summary>Settings in other mods' configs bound to <paramref name="key"/> (CheckBook.KeyClashes per file); our own file is left out.</summary>
        internal static List<string> ModClashes(KeyCode key)
        {
            var found = new List<string>();
            if (key == KeyCode.None || !Directory.Exists(Paths.ConfigPath)) return found;
            var own = BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Plugin.Guid, out var info) && info.Instance != null ? Path.GetFullPath(info.Instance.Config.ConfigFilePath) : "";
            foreach (var file in Directory.GetFiles(Paths.ConfigPath, "*.cfg"))
            {
                if (string.Equals(Path.GetFullPath(file), own, StringComparison.OrdinalIgnoreCase)) continue;
                try { found.AddRange(CheckBook.KeyClashes(Path.GetFileName(file), File.ReadAllLines(file), key.ToString())); } catch { }
            }
            return found;
        }

        /// <summary>Once per game run: is the filter key bound by another mod too? One info line, or one warning naming the settings.</summary>
        static void KeyClashCheck()
        {
            if (clashChecked) return;
            clashChecked = true;
            try
            {
                var key = FilterKey.Value;
                var clashes = ModClashes(key);
                var line = "filter key: " + key + ", conflicts: " + (clashes.Count == 0 ? "none" : string.Join("; ", clashes.ToArray()));
                if (clashes.Count == 0) Debug.Log("[Hearthwoven] " + line);
                else Debug.LogWarning("[Hearthwoven] " + line + " (set another key in Panel.FilterKey)");
                if (DevCheck.On) DevCheck.Book.Say(clashes.Count == 0 ? "PASS" : "WARN", "filter-key-conflicts", line);
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] filter key check: " + e.Message); }
        }

        /// <summary>The game's own actions bound to a key ("Inventory (&lt;Keyboard&gt;/tab)"), read from ZInput's button table.</summary>
        static List<string> GameBindings(KeyCode key)
        {
            var found = new List<string>();
            var zi = ZInput.instance;
            var dict = zi == null ? null : AccessTools.Field(typeof(ZInput), "m_buttons")?.GetValue(zi) as IDictionary;
            if (dict == null) return found;
            var want = "/" + key.ToString().ToLowerInvariant();
            foreach (DictionaryEntry e in dict)
            {
                var path = AccessTools.Method(e.Value.GetType(), "GetActionPath", new[] { typeof(bool) });
                foreach (var alt in new[] { false, true })
                {
                    string p = null;
                    try { p = path?.Invoke(e.Value, new object[] { alt }) as string; } catch { }
                    if (p != null && p.ToLowerInvariant().Contains("keyboard") && p.ToLowerInvariant().EndsWith(want)) { found.Add(e.Key + " (" + p + ")"); break; }
                }
            }
            return found;
        }

        /// <summary>For the report: each panel key against the other mods' configs and the game's own bindings.</summary>
        internal static void KeyReport()
        {
            var keys = new List<KeyValuePair<string, ConfigEntryKey>>
            {
                new KeyValuePair<string, ConfigEntryKey>("Hotkey", new ConfigEntryKey(Hotkey)), new KeyValuePair<string, ConfigEntryKey>("InfoKey", new ConfigEntryKey(InfoKey)),
                new KeyValuePair<string, ConfigEntryKey>("ViewKey", new ConfigEntryKey(ViewKey)), new KeyValuePair<string, ConfigEntryKey>("FilterKey", new ConfigEntryKey(FilterKey)),
                new KeyValuePair<string, ConfigEntryKey>("SnapshotKey", new ConfigEntryKey(SnapshotKey)), new KeyValuePair<string, ConfigEntryKey>("SelfCheckKey", new ConfigEntryKey(DevCheck.Key)),
            };
            foreach (var k in keys)
            {
                var key = k.Value.Get();
                if (key == KeyCode.None) continue;
                var mods = ModClashes(key);
                var game = GameBindings(key);
                var guarded = k.Key == "FilterKey" && game.Any(g => g.StartsWith("Inventory"));
                var text = k.Key + " = " + key + ": other mods " + (mods.Count == 0 ? "none" : string.Join("; ", mods.ToArray())) + "; the game's own " + (game.Count == 0 ? "none" : string.Join(", ", game.ToArray())) +
                           (guarded ? " (the inventory is held shut for this key while the panel is open)" : "");
                DevCheck.Book.Say(mods.Count == 0 && (game.Count == 0 || guarded) ? "PASS" : "WARN", "keys", text);
            }
        }

        sealed class ConfigEntryKey
        {
            readonly BepInEx.Configuration.ConfigEntry<KeyCode> entry;
            public ConfigEntryKey(BepInEx.Configuration.ConfigEntry<KeyCode> e) { entry = e; }
            public KeyCode Get() { try { return entry != null ? entry.Value : KeyCode.None; } catch { return KeyCode.None; } }
        }

        /// <summary>True while the panel is open and the filter key is down: the inventory must not open on that press (PanelHooks).</summary>
        internal static bool FilterKeyHeld()
        {
            try { return Blocking && FilterKey != null && FilterKey.Value != KeyCode.None && ZInput.GetKey(FilterKey.Value, false); }
            catch { return false; }
        }

        /// <summary>"on" while the filter focus or an opened filter is on the page, else "off" (DevCheck's filter-key line).</summary>
        internal string FocusNow() => view != null && PanelModel.FilterAnyOpen(state, view) ? "on" : "off";

        string PageName() => state.ShowAbout ? "About" : (view != null ? view.Active + (string.IsNullOrEmpty(view.Page) ? "" : "/" + view.Page) : state.Chapter.ToString());

        // ---------- the self-check key ----------

        void CheckTick()
        {
            if (!DevCheck.On) return;
            try
            {
                if (open && FilterKey.Value != KeyCode.None && !Typing() && Key(FilterKey.Value))
                    DevCheck.FilterKey(FilterKey.Value.ToString(), PageName() + (view != null && PanelModel.FilterOf(view) != null ? "" : " (no filter bar here)"));
                if (Snapshots != null && !Snapshots.Value && SnapshotKey.Value != KeyCode.None && !Typing() && Key(SnapshotKey.Value))
                    DevCheck.Book.Once("INFO", "snapshots", SnapshotKey.Value + " pressed, but Dev.PanelSnapshots is off: no pictures taken");
                if (DevCheck.Key == null || DevCheck.Key.Value == KeyCode.None || snapping || Typing() || !Key(DevCheck.Key.Value)) return;
                DevCheck.Report();
                if (!CanSnap()) { DevCheck.Book.Say("WAIT", "pages", "the panel cannot open here (in a menu, dead, or no player): page walk skipped"); DevCheck.Done("no page walk"); return; }
                snapping = true; snapProgress = Time.unscaledTime;
                snapRun = StartCoroutine(CheckRun());
            }
            catch (Exception e) { snapping = false; Debug.LogWarning("[Hearthwoven] self-check: " + e.Message); }
        }

        // ---------- the page walk ----------

        static readonly HashSet<string> NewKinds = new HashSet<string> { "feats", "featdetail", "featband", "knownfor", "filterbar", "facetbar", "strip", "cards", "composition", "biomes", "ladders" };

        static IEnumerable<Block> Every(IEnumerable<Block> bs) => (bs ?? new List<Block>()).Where(b => b != null).SelectMany(b => new[] { b }.Concat(Every(b.Items)));

        int Marks() => root ? root.GetComponentsInChildren<RectTransform>(false).Count(r => r.name == "Ring" || r.name == "Focus" || r.name == FocusRingName) : 0;

        IEnumerator CheckRun()
        {
            string failure = null;
            int pages = 0, bad = 0, small = 0, rawPages = 0;
            var warnings = new List<string>();
            Application.LogCallback grab = (msg, stack, type) =>
            {
                if (type == LogType.Log || msg == null) return;
                if (msg.Contains("[Hearthwoven]") || (type == LogType.Exception && (stack ?? "").Contains("Hearthwoven"))) lock (warnings) warnings.Add(msg.Length > 160 ? msg.Substring(0, 160) : msg);
            };
            try
            {
                Application.logMessageReceived += grab;
                snapSaved = SaveState();
                if (!open) Open();
            }
            catch (Exception e) { failure = "start: " + e.Message; }
            yield return null;
            yield return null;
            if (failure == null)
            {
                try
                {
                    var want = PanelModel.PanelScale(Scale.Value); var have = frame.localScale.x;
                    var corners = new Vector3[4]; frame.GetWorldCorners(corners);
                    bool fits = corners[0].x >= -1 && corners[0].y >= -1 && corners[2].x <= Screen.width + 1 && corners[2].y <= Screen.height + 1;
                    DevCheck.Book.Say(Mathf.Abs(want - have) < 0.001f && fits ? "PASS" : "FAIL", "scale", "Panel.Scale " + Scale.Value.ToString("0.00") + ", panel drawn at " + have.ToString("0.00") + ", " +
                        Mathf.RoundToInt(corners[2].x - corners[0].x) + " x " + Mathf.RoundToInt(corners[2].y - corners[0].y) + " px on a " + Screen.width + " x " + Screen.height + " screen" + (fits ? ", fits" : ", does NOT fit"));
                }
                catch (Exception e) { DevCheck.Book.Say("FAIL", "scale", e.Message); }
            }

            var chapters = (Chapter[])Enum.GetValues(typeof(Chapter));
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int ci = 0; failure == null && ci <= chapters.Length; ci++)
            {
                List<Shot> plan = null;
                try { plan = ci < chapters.Length ? Plan(chapters[ci], null, names) : AboutPlan(null, names); }
                catch (Exception e) { DevCheck.Book.Say("FAIL", "page", (ci < chapters.Length ? chapters[ci].ToString() : "About") + ": could not plan its pages: " + e.Message); bad++; continue; }
                foreach (var shot in plan)
                {
                    snapProgress = Time.unscaledTime;
                    if (!open || !Player.m_localPlayer || Player.m_localPlayer.IsDead()) { failure = "the panel closed or the player died"; break; }
                    var label = shot.About ? "About" : shot.Chapter + (shot.Page != null ? "/" + shot.Page : "") + (shot.View != null ? " (" + shot.View + ")" : "");
                    lock (warnings) warnings.Clear();
                    string error = null;
                    try { Apply(shot); } catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
                    yield return null;
                    yield return null;
                    bool hasFilter = false;
                    try
                    {
                        pages++;
                        var objects = root.GetComponentsInChildren<RectTransform>(false).Length;
                        float scale = frame.lossyScale.y, least = 999f; string leastText = null; int below = 0, hidden = 0; string hiddenText = null;
                        int raw = 0; var rawTexts = new List<string>();
                        foreach (var t in root.GetComponentsInChildren<TMP_Text>(false))
                        {
                            if (!t.enabled || string.IsNullOrEmpty(t.text) || t.color.a < 0.05f || scale <= 0f) continue;
                            // live-polish: a text whose box is lower than its line shows nothing at all under TMP's ellipsis (Cooking's ledger names in game)
                            if ((t.overflowMode == TextOverflowModes.Ellipsis || t.overflowMode == TextOverflowModes.Truncate) && t.text.Trim().Length > 0)
                            {
                                t.ForceMeshUpdate();
                                var info = t.textInfo; var drawn = false;
                                for (int c = 0; info != null && c < info.characterCount && !drawn; c++) drawn = info.characterInfo[c].isVisible && info.characterInfo[c].character != '…';
                                if (!drawn) { hidden++; if (hiddenText == null) hiddenText = "'" + (t.text.Length > 24 ? t.text.Substring(0, 24) : t.text) + "' in " + (t.transform.parent ? t.transform.parent.name : t.name); }
                            }
                            // richtext-fix: what the label shows, tags parsed or not: a tag name after '<' means the player reads markup
                            t.ForceMeshUpdate();
                            var shown = t.GetParsedText();
                            if (PanelRich.HasMarkup(shown)) { raw++; if (rawTexts.Count < 3) rawTexts.Add("'" + (shown.Length > 60 ? shown.Substring(0, 60) : shown) + "' in " + (t.transform.parent ? t.transform.parent.name : t.name)); }
                            var px = t.fontSize * t.transform.lossyScale.y / scale;
                            if (px < PanelLook.MinText - 0.05f) below++;
                            if (px < least) { least = px; leastText = "'" + (t.text.Length > 24 ? t.text.Substring(0, 24) : t.text) + "' in " + (t.transform.parent ? t.transform.parent.name : t.name); }
                        }
                        var kinds = view == null ? new List<string>() : Every(PanelModel.Content(view)).Select(b => b.Kind).Where(k => k != null && NewKinds.Contains(k)).Distinct().ToList();
                        hasFilter = view != null && PanelModel.FilterOf(view) != null;
                        List<string> seen; lock (warnings) seen = warnings.ToList();
                        var shows = Shows(shot);
                        var status = error != null || seen.Count > 0 || !shows || raw > 0 ? "FAIL" : below > 0 || hidden > 0 ? "WARN" : "PASS";
                        if (status == "FAIL") bad++; if (below > 0) small++;
                        var text = label + ": " + (error != null ? "threw " + error : seen.Count > 0 ? "warned: " + seen[0] + (seen.Count > 1 ? " (+" + (seen.Count - 1) + " more)" : "") : shows ? "drawn" : "another page showed") +
                                   ", " + objects + " objects, smallest text " + (least < 999f ? least.ToString("0.0") + " px" + (below > 0 ? " (" + below + " under " + PanelLook.MinText + ": " + leastText + ")" : "") : "none") +
                                   (hidden > 0 ? ", " + hidden + " text(s) hidden by a too-low box (" + hiddenText + ")" : "") +
                                   (raw > 0 ? ", " + raw + " text(s) show raw markup" : "") +
                                   ", focus marks " + Marks() + (kinds.Count > 0 ? "; " + string.Join(", ", kinds.ToArray()) : "");
                        DevCheck.Book.Say(status, "page", text);
                        if (raw > 0) { rawPages++; DevCheck.Book.Say("FAIL", "markup", label + ": " + raw + " text(s) show raw markup: " + string.Join("; ", rawTexts.ToArray()) + (raw > rawTexts.Count ? " (+" + (raw - rawTexts.Count) + " more)" : "")); }
                    }
                    catch (Exception e) { DevCheck.Book.Say("FAIL", "page", label + ": measuring failed: " + e.Message); bad++; }
                    if (!hasFilter || error != null) continue;
                    // the filter focus, as the filter key enters it: the cursor's ring must be drawn
                    int before = Marks(); string ferror = null; bool entered = false;
                    lock (warnings) warnings.Clear();
                    try { if (!PanelModel.FilterAnyOpen(state, view)) { entered = PanelModel.FilterKeyPressed(state, view); if (entered) { SetKeyFocus(true); Render(true); } } }   /* as the filter key does: a key press shows the ring */ catch (Exception e) { ferror = e.GetType().Name + ": " + e.Message; }
                    yield return null;
                    yield return null;
                    try
                    {
                        int after = Marks(); List<string> seen; lock (warnings) seen = warnings.ToList();
                        if (entered || ferror != null)
                            DevCheck.Book.Say(ferror == null && seen.Count == 0 && after > before && state.FilterRow >= 0 ? "PASS" : "FAIL", "focus",
                                label + ": filter focus " + (ferror != null ? "threw " + ferror : "entered, row " + state.FilterRow + ", focus marks " + before + " -> " + after) + (seen.Count > 0 ? ", warned: " + seen[0] : ""));
                        PanelModel.FilterLeave(state, view); Render(true);
                    }
                    catch (Exception e) { DevCheck.Book.Say("FAIL", "focus", label + ": " + e.Message); }
                    yield return null;
                }
            }
            try { Application.logMessageReceived -= grab; } catch { }
            Restore(snapSaved);
            snapping = false; snapRun = null;
            try
            {
                var missing = PanelLook.MissingIcons();
                DevCheck.Book.Say(missing.Count == 0 ? "PASS" : "WARN", "icons", missing.Count == 0 ? "every icon the pages asked for was found" :
                    missing.Count + " icon references found nothing (a fallback or no picture was drawn): " + string.Join(", ", missing.Take(25).ToArray()));
            }
            catch (Exception e) { DevCheck.Book.Say("FAIL", "icons", e.Message); }
            DevCheck.Book.Say(rawPages == 0 ? "PASS" : "FAIL", "markup", rawPages == 0 ? pages + " pages walked, no visible text shows a tag as letters" : rawPages + " of " + pages + " pages show raw markup (each named in a markup line above)");
            if (failure != null) DevCheck.Book.Say("FAIL", "pages", "walk stopped: " + failure);
            DevCheck.Done(pages + " pages walked, " + bad + " failed, " + small + " with text under " + PanelLook.MinText + " px, " + rawPages + " with raw markup");
        }
    }
}
