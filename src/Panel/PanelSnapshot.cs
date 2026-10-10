using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Developer tool, off unless Dev.PanelSnapshots is true: photographs every chapter, page and view of the real panel to
    /// PNG, so the drawn panel can be reviewed without screenshots by hand.
    /// Trigger: the snapshot key (Dev.SnapshotKey, F11), or a file named "request" in BepInEx/Hearthwoven/snapshots/ (looked
    /// for once a second, deleted when the run starts; it waits while the panel cannot open). Lines in that file, if any,
    /// limit the run to pages whose "Chapter/page" starts with one of them ("Skills", "Battle/defense"; "about" = the About
    /// page). Output: BepInEx/Hearthwoven/snapshots/&lt;yyyyMMdd-HHmmss&gt;/&lt;chapter&gt;-&lt;page&gt;[-&lt;view&gt;][-pN].png, each
    /// page's view model as &lt;name&gt;.json beside it, and index.json. A page taller than its plate is shot in parts (-p2..).
    /// One page every few frames (a coroutine), PNG encoding off the main thread; afterwards the panel returns to the page
    /// the player had open, or closes again. Never throws into the game: a failure ends the run, restores and is logged.
    /// Hooked into PanelUi in two places: BindConfig (the config) and the top of Update (SnapshotTick).
    /// </summary>
    public partial class PanelUi
    {
        internal static ConfigEntry<bool> Snapshots;
        internal static ConfigEntry<KeyCode> SnapshotKey;

        static void BindSnapshotConfig(ConfigFile config)
        {
            Snapshots = config.Bind("Dev", "PanelSnapshots", false, "Developer tool: photograph every chapter, page and view of the panel to PNG (BepInEx/Hearthwoven/snapshots/) on the snapshot key, or when a file named 'request' appears in that folder. Off for players.");
            SnapshotKey = config.Bind("Dev", "SnapshotKey", KeyCode.F11, "Key that starts a panel snapshot run (only with PanelSnapshots on). F11: F9 is SeneaL UI's settings key; F1, F5, F6, F7, F8 and F10 belong to the console and the group's other mods.");
        }

        class Shot { public Chapter Chapter; public bool About; public string Page, Switch, View, ChapterLabel, PageLabel, ViewLabel, Name; public bool? They; }
        class Saved { public bool Open, They, About; public Chapter Chapter; public string Player; public Dictionary<Chapter, string> Page; public Dictionary<string, string> View; }

        const int MaxParts = 6, Margin = 6;
        const float Watchdog = 30f;
        static string SnapshotDir => Path.Combine(Paths.BepInExRootPath, Path.Combine("Hearthwoven", "snapshots"));
        static readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();

        bool snapping, requestWaitLogged;
        float nextRequestCheck, snapProgress;
        Coroutine snapRun;
        Saved snapSaved;
        int pendingWrites;
        readonly List<string> writeErrors = new List<string>();

        /// <summary>The hook at the top of Update: starts a run on the key or a request file. True while a run owns the panel
        /// (the panel's own keys then wait).</summary>
        bool SnapshotTick()
        {
            if (snapping)
            {
                if (Time.unscaledTime - snapProgress < Watchdog) return true;
                // a run that stopped moving (it never should): give the panel back
                Debug.LogWarning("[Hearthwoven] snapshot run stalled; stopped");
                try { if (snapRun != null) StopCoroutine(snapRun); } catch { }
                Restore(snapSaved);
                snapping = false;
                return false;
            }
            if (Snapshots == null || !Snapshots.Value) return false;
            try
            {
                string trigger = null, filter = null;
                if (SnapshotKey.Value != KeyCode.None && !Typing() && Key(SnapshotKey.Value)) trigger = "key";
                else if (Time.unscaledTime >= nextRequestCheck)
                {
                    nextRequestCheck = Time.unscaledTime + 1f;
                    var request = Path.Combine(SnapshotDir, "request");
                    if (!File.Exists(request)) return false;
                    if (!CanSnap())
                    {
                        if (!requestWaitLogged) Debug.Log("[Hearthwoven] snapshot request waits until the panel can open");
                        requestWaitLogged = true;
                        return false;
                    }
                    filter = File.ReadAllText(request);
                    File.Delete(request);
                    requestWaitLogged = false;
                    trigger = "file";
                }
                if (trigger == null) return false;
                if (!CanSnap()) { Debug.Log("[Hearthwoven] snapshot: not now (the panel cannot open here)"); return false; }
                snapping = true; snapProgress = Time.unscaledTime;
                snapRun = StartCoroutine(SnapshotRun(trigger, filter));
                return snapping;
            }
            catch (Exception e) { snapping = false; Debug.LogWarning("[Hearthwoven] snapshot not started: " + e.Message); return false; }
        }

        bool CanSnap() => open ? Player.m_localPlayer && !Player.m_localPlayer.IsDead() : CanOpen();

        IEnumerator SnapshotRun(string trigger, string filterText)
        {
            var started = DateTime.Now;
            string dir = null, failure = null;
            var entries = new Json().OpenArr();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int shots = 0, pages = 0;
            List<string> filters = null;
            try
            {
                filters = (filterText ?? "").Split('\n').Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length > 0).ToList();
                snapSaved = SaveState();
                dir = Path.Combine(SnapshotDir, started.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(dir);
                lock (writeErrors) writeErrors.Clear();
                if (!open) Open();
            }
            catch (Exception e) { failure = "start: " + e.Message; }

            var chapters = (Chapter[])Enum.GetValues(typeof(Chapter));
            for (int ci = 0; failure == null && ci <= chapters.Length; ci++)
            {
                List<Shot> plan = null;
                try { plan = ci < chapters.Length ? Plan(chapters[ci], filters, names) : AboutPlan(filters, names); }
                catch (Exception e) { failure = "plan " + (ci < chapters.Length ? chapters[ci].ToString() : "about") + ": " + e.Message; break; }
                foreach (var shot in plan)
                {
                    snapProgress = Time.unscaledTime;
                    if (!open || !Player.m_localPlayer || Player.m_localPlayer.IsDead()) { failure = "the panel closed or the player died"; break; }
                    try { Apply(shot); } catch (Exception e) { failure = shot.Name + ": " + e.Message; break; }
                    yield return null;   // layout groups, size fitters and text meshes settle over a frame or two
                    yield return null;
                    Scroll sc = null; float room = 0f, step = 1f; int parts = 1;
                    try
                    {
                        Canvas.ForceUpdateCanvases();
                        sc = scrollers.FirstOrDefault(s => s.Rect && s.Rect.content == content);
                        if (sc != null) { room = Room(sc); step = Mathf.Max(100f, sc.Rect.viewport.rect.height - 48f); }
                        parts = room <= 0f ? 1 : Mathf.Min(MaxParts, 1 + Mathf.CeilToInt(room / step));
                        File.WriteAllText(Path.Combine(dir, shot.Name + ".json"), PanelModel.ToJson(view, islands: true), PeerIdentity.Utf8);   // with the island plan, as the preview draws it
                        pages++;
                    }
                    catch (Exception e) { failure = shot.Name + ": " + e.Message; break; }
                    for (int p = 0; p < parts; p++)
                    {
                        try
                        {
                            if (sc != null)
                            {
                                sc.Easing = false; sc.Velocity = 0f;
                                var c = sc.Rect.content; c.anchoredPosition = new Vector2(c.anchoredPosition.x, Mathf.Min(room, p * step));
                            }
                        }
                        catch (Exception e) { failure = shot.Name + ": " + e.Message; break; }
                        yield return endOfFrame;   // the screen-space overlay is drawn: read it back
                        try
                        {
                            var file = shot.Name + (p > 0 ? "-p" + (p + 1) : "") + ".png";
                            var size = Capture(Path.Combine(dir, file));
                            shots++;
                            entries.Open().Str("file", file).Str("model", shot.Name + ".json")
                                .Str("chapter", shot.About ? "About" : shot.Chapter.ToString()).Str("chapterLabel", shot.ChapterLabel ?? "")
                                .Str("page", shot.Page ?? "").Str("pageLabel", shot.PageLabel ?? "")
                                .Str("view", shot.View ?? "").Str("viewLabel", shot.ViewLabel ?? "")
                                .Num("part", p + 1).Num("parts", parts).Str("heading", view?.Heading ?? "")
                                .Num("shownAsPlanned", Shows(shot) ? 1 : 0).Num("width", size.x).Num("height", size.y).Close();
                        }
                        catch (Exception e) { failure = shot.Name + ": " + e.Message; }
                        if (failure != null) break;
                    }
                    if (failure != null) break;
                }
            }

            // the PNGs are encoded on worker threads: wait for them (bounded) before writing the index
            var until = Time.unscaledTime + Watchdog - 5f;
            while (Interlocked.CompareExchange(ref pendingWrites, 0, 0) > 0 && Time.unscaledTime < until) { snapProgress = Time.unscaledTime; yield return null; }
            try
            {
                if (dir != null)
                {
                    string errors;
                    lock (writeErrors) errors = "[" + string.Join(",", writeErrors.Select(Json.Q).ToArray()) + "]";
                    var index = new Json().Open().Str("time", started.ToString("yyyy-MM-dd'T'HH:mm:ss")).Str("trigger", trigger).Str("filter", string.Join(",", (filters ?? new List<string>()).ToArray()))
                        .Str("version", Plugin.Version).Num("screenWidth", Screen.width).Num("screenHeight", Screen.height).Num("panelScale", Scale.Value)
                        .Num("pages", pages).Num("shots", shots).Str("failure", failure ?? "").Raw("writeErrors", errors)
                        .Raw("entries", entries.CloseArr().ToString()).Close();
                    File.WriteAllText(Path.Combine(dir, "index.json"), index.ToString(), PeerIdentity.Utf8);
                }
            }
            catch (Exception e) { failure = (failure ?? "") + " index: " + e.Message; }
            Restore(snapSaved);
            snapping = false; snapRun = null;
            if (failure == null) Debug.Log("[Hearthwoven] panel snapshots: " + shots + " images of " + pages + " pages in " + dir);
            else Debug.LogWarning("[Hearthwoven] panel snapshots stopped after " + shots + " images (" + failure + ") in " + dir);
            if (DevCheck.On) DevCheck.Book.Say(failure == null && shots > 0 ? "PASS" : "FAIL", "snapshots", shots + " images of " + pages + " pages, " + (SampleMode.On ? "sample data" : "your own data") + (failure != null ? ", stopped: " + failure : "") + ", in " + dir);
        }

        // ---------- what to shoot: every page of a chapter, every view of its switch (or both sides of the toggle) ----------

        PanelView Model(PanelState s)
        {
            var self = Gather();
            self.Fellows = FellowsOf(self);   // the sample's fellows with Dev.SampleData on
            return PanelModel.Build(self, s);
        }

        PanelState Probe(Chapter c)
        {
            var s = new PanelState { Chapter = c, Window = state.Window, WindowPicked = state.WindowPicked, TheyReceived = state.TheyReceived, Hotkey = state.Hotkey, InfoKey = state.InfoKey, ViewKey = state.ViewKey };
            foreach (var kv in state.View) s.View[kv.Key] = kv.Value;
            return s;
        }

        List<Shot> Plan(Chapter c, List<string> filters, HashSet<string> names)
        {
            var shots = new List<Shot>();
            var probe = Probe(c);
            var first = Model(probe);
            var chapterLabel = first.Chapters.FirstOrDefault(x => x.Selected)?.Label ?? c.ToString();
            var pages = first.List.Count > 0 ? first.List.Select(l => new Choice { Id = l.Id, Label = l.Label }).ToList() : new List<Choice> { new Choice { Id = null, Label = chapterLabel } };
            foreach (var page in pages)
            {
                if (!Wanted(filters, c + "/" + (page.Id ?? ""))) continue;
                if (page.Id != null) probe.Page[c] = page.Id;
                var v = Model(probe);
                var baseName = Safe(c.ToString()) + (page.Id != null ? "-" + Safe(page.Id) : "");
                var sw = PanelModel.Content(v).FirstOrDefault(b => b.Kind == "switch" && b.Items != null && b.Items.Count > 1);
                if (sw != null)
                    foreach (var o in sw.Items)
                        shots.Add(new Shot { Chapter = c, Page = page.Id, Switch = sw.Id, View = o.Id, ChapterLabel = chapterLabel, PageLabel = page.Label, ViewLabel = o.Title, Name = Unique(names, baseName + "-" + Safe(o.Id)) });
                else if (v.Toggle.Count > 1)
                    foreach (var t in v.Toggle)
                        shots.Add(new Shot { Chapter = c, Page = page.Id, They = t.Id == "they", View = t.Id, ChapterLabel = chapterLabel, PageLabel = page.Label, ViewLabel = t.Label, Name = Unique(names, baseName + "-" + Safe(t.Id)) });
                else shots.Add(new Shot { Chapter = c, Page = page.Id, ChapterLabel = chapterLabel, PageLabel = page.Label, Name = Unique(names, baseName) });
            }
            return shots;
        }

        List<Shot> AboutPlan(List<string> filters, HashSet<string> names) =>
            Wanted(filters, "about") ? new List<Shot> { new Shot { About = true, Chapter = state.Chapter, ChapterLabel = "About", Name = Unique(names, "about") } } : new List<Shot>();

        static bool Wanted(List<string> filters, string key)
        {
            if (filters == null || filters.Count == 0) return true;
            key = key.ToLowerInvariant();
            return filters.Any(f => f == "*" || key.StartsWith(f, StringComparison.Ordinal));
        }

        static string Safe(string s) => PanelBenchWalk.Safe(s);   // one naming for the page walk, the snapshots and Dev.Bench
        static string Unique(HashSet<string> names, string name) => PanelBenchWalk.Unique(names, name);

        void Apply(Shot shot)
        {
            state.Player = ""; state.ShowAbout = shot.About;
            if (!shot.About)
            {
                state.Chapter = shot.Chapter;
                if (shot.Page != null) state.Page[shot.Chapter] = shot.Page;
                if (shot.Switch != null) state.View[shot.Switch] = shot.View;
                if (shot.They.HasValue) state.TheyReceived = shot.They.Value;
            }
            Render(true);
        }

        // the panel shows what the plan asked for (a page that vanished between planning and shooting shows another)
        bool Shows(Shot shot)
        {
            if (view == null) return false;
            if (shot.About) return view.ShowAbout;
            return view.Active == shot.Chapter && (shot.Page == null || view.Page == shot.Page);
        }

        // ---------- the picture: the panel frame's screen rect, read back at the end of the frame ----------

        Vector2 Capture(string path)
        {
            var corners = new Vector3[4];
            frame.GetWorldCorners(corners);   // a screen-space overlay canvas: world units are screen pixels
            int x0 = Mathf.Clamp(Mathf.FloorToInt(corners[0].x) - Margin, 0, Screen.width), y0 = Mathf.Clamp(Mathf.FloorToInt(corners[0].y) - Margin, 0, Screen.height);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(corners[2].x) + Margin, 0, Screen.width), y1 = Mathf.Clamp(Mathf.CeilToInt(corners[2].y) + Margin, 0, Screen.height);
            int w = x1 - x0, h = y1 - y0;
            if (w <= 0 || h <= 0) throw new InvalidOperationException("the panel is off screen");
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Color32[] px;
            try { tex.ReadPixels(new Rect(x0, y0, w, h), 0, 0, false); px = tex.GetPixels32(); }
            finally { Destroy(tex); }
            Interlocked.Increment(ref pendingWrites);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var rgb = new byte[w * h * 3];
                    for (int i = 0, o = 0; i < px.Length; i++) { rgb[o++] = px[i].r; rgb[o++] = px[i].g; rgb[o++] = px[i].b; }
                    File.WriteAllBytes(path, PanelPng.Encode(rgb, w, h, bottomUp: true));
                }
                catch (Exception e) { lock (writeErrors) writeErrors.Add(Path.GetFileName(path) + ": " + e.Message); }
                finally { Interlocked.Decrement(ref pendingWrites); }
            });
            return new Vector2(w, h);
        }

        // ---------- back to what the player had ----------

        Saved SaveState() => new Saved
        {
            Open = open, Chapter = state.Chapter, Player = state.Player, They = state.TheyReceived, About = state.ShowAbout,
            Page = new Dictionary<Chapter, string>(state.Page), View = new Dictionary<string, string>(state.View),
        };

        void Restore(Saved s)
        {
            if (s == null) return;
            try
            {
                state.Chapter = s.Chapter; state.Player = s.Player; state.TheyReceived = s.They; state.ShowAbout = s.About;
                state.Page.Clear(); foreach (var kv in s.Page) state.Page[kv.Key] = kv.Value;
                state.View.Clear(); foreach (var kv in s.View) state.View[kv.Key] = kv.Value;
                if (!s.Open) Close();
                else if (open) Render(true);
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] snapshot restore: " + e.Message); Close(); }
            snapSaved = null;
        }
    }

    /// <summary>A minimal PNG writer (8-bit RGB, Sub filter, zlib via DeflateStream): the game ships no image encoder this
    /// mod references (ImageConversion lives in a module the csproj does not load), and this keeps it that way.</summary>
    public static class PanelPng
    {
        static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        static uint[] crcTable;

        /// <summary>rgb: width * height * 3 bytes, rows top first (or bottom first with bottomUp, as ReadPixels gives them).</summary>
        public static byte[] Encode(byte[] rgb, int width, int height, bool bottomUp = false)
        {
            if (rgb == null || width <= 0 || height <= 0 || rgb.Length < width * height * 3) throw new ArgumentException("bad image size");
            int stride = width * 3;
            var raw = new byte[(stride + 1) * height];
            for (int y = 0; y < height; y++)
            {
                int src = (bottomUp ? height - 1 - y : y) * stride, o = y * (stride + 1);
                raw[o] = 1;   // Sub: each byte minus the same channel of the pixel to its left
                for (int i = 0; i < stride; i++) raw[o + 1 + i] = (byte)(rgb[src + i] - (i >= 3 ? rgb[src + i - 3] : 0));
            }
            byte[] zlib;
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78); ms.WriteByte(0x01);
                using (var d = new DeflateStream(ms, CompressionMode.Compress, true)) d.Write(raw, 0, raw.Length);
                uint a = 1, b = 0;
                foreach (var x in raw) { a = (a + x) % 65521; b = (b + a) % 65521; }
                WriteBE(ms, (b << 16) | a);
                zlib = ms.ToArray();
            }
            using (var png = new MemoryStream())
            {
                png.Write(Signature, 0, Signature.Length);
                var ihdr = new byte[13];
                Put(ihdr, 0, (uint)width); Put(ihdr, 4, (uint)height);
                ihdr[8] = 8; ihdr[9] = 2;   // 8 bits per channel, truecolour; compression, filter and interlace 0
                Chunk(png, "IHDR", ihdr); Chunk(png, "IDAT", zlib); Chunk(png, "IEND", new byte[0]);
                return png.ToArray();
            }
        }

        static void Chunk(Stream s, string type, byte[] data)
        {
            WriteBE(s, (uint)data.Length);
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(t, 0, 4); s.Write(data, 0, data.Length);
            uint c = 0xFFFFFFFFu;
            c = Crc(c, t); c = Crc(c, data);
            WriteBE(s, c ^ 0xFFFFFFFFu);
        }

        static uint Crc(uint c, byte[] data)
        {
            if (crcTable == null)
            {
                var t = new uint[256];
                for (uint n = 0; n < 256; n++) { var k = n; for (int i = 0; i < 8; i++) k = (k & 1) != 0 ? 0xEDB88320u ^ (k >> 1) : k >> 1; t[n] = k; }
                crcTable = t;
            }
            foreach (var x in data) c = crcTable[(c ^ x) & 0xFF] ^ (c >> 8);
            return c;
        }

        static void Put(byte[] b, int at, uint v) { b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v; }
        static void WriteBE(Stream s, uint v) { var b = new byte[4]; Put(b, 0, v); s.Write(b, 0, 4); }
    }
}
