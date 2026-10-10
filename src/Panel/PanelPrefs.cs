using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// What a player set on the panel's filters (and, 0.8, whether the Everyone chip is on: PanelState.Everyone), kept across panel opens and game sessions (Joost 2026-10-09: "closed by default, but
    /// remember what a player set, per page"): the chips chosen in each filter bar (PanelState.Facets) and which bars are open
    /// (PanelState.OpenFilters). One small file per character next to the local totals: BepInEx/Hearthwoven/local/&lt;playerId&gt;-&lt;name&gt;.panel.json
    /// (0.8: id and name, as LocalTotals.PathFor, so two characters sharing a copied id keep their own; a file from before, keyed by
    /// the id only, is read once as the starting point and left as it is), written atomically with one backup (AtomicFile), only when
    /// something changed. A broken file falls back to its .bak (the note says so); a file that cannot be read at all is ignored: the
    /// filters start closed and empty, as on a first run. Keys a newer version wrote are kept; a newer version's file is never saved
    /// over. Nothing here is a count; it never touches the totals file. 0.8: also the views a player chose where the book keeps them
    /// (PanelModel.RememberedViews: the battle feed's Log, Cards or Timeline), under "views".
    /// 0.8: the Compare chip (PanelState.Compare, Chapters/CompareModel.cs), so every page reads the same way after a restart: "compare":1,
    /// written only when on (a file without it reads as off; an older reader keeps it as an unknown key).
    /// </summary>
    public static class PanelPrefs
    {
        public const int Version = 1;

        /// <summary>The file from before 0.8, keyed by the player id only (read as the starting point by Load).</summary>
        public static string PathFor(string hearthwovenDir, long playerId) =>
            Path.Combine(Path.Combine(hearthwovenDir, "local"), playerId.ToString(CultureInfo.InvariantCulture) + ".panel.json");

        /// <summary>One character's file: id and name (LocalTotals.SafeName), next to its local totals.</summary>
        public static string PathFor(string hearthwovenDir, long playerId, string name) =>
            Path.Combine(Path.Combine(hearthwovenDir, "local"), playerId.ToString(CultureInfo.InvariantCulture) + "-" + LocalTotals.SafeName(name) + ".panel.json");

        static readonly HashSet<string> KnownKeys = new HashSet<string> { "version", "facets", "open", "everyone", "views", "compare" };   // every key this version writes (else a stale one is carried as a newer version's)
        static Dictionary<string, object> Root(string json) => MiniJson.Parse(json ?? "") as Dictionary<string, object>;
        static bool IsPrefs(string json) => Root(json) is Dictionary<string, object> r && MiniJson.Num(r, "version") >= 1;

        /// <summary>The filter state as JSON (sorted, so the same state always writes the same text: it doubles as the change check).</summary>
        public static string ToJson(PanelState s)
        {
            var b = new StringBuilder();
            b.Append("{\"version\":").Append(Version).Append(",\"facets\":{");
            var first = true;
            foreach (var kv in s.Facets.Where(kv => kv.Value != null && kv.Value.Count > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (!first) b.Append(','); first = false;
                b.Append(Json.Q(kv.Key)).Append(":[").Append(string.Join(",", kv.Value.Select(Json.Q).ToArray())).Append(']');
            }
            b.Append("},\"open\":[").Append(string.Join(",", s.OpenFilters.OrderBy(x => x, StringComparer.Ordinal).Select(Json.Q).ToArray())).Append(']');
            if (s.Everyone) b.Append(",\"everyone\":true");   // 0.8: the Everyone chip, written only when on (a file without it reads as off)
            if (s.Compare) b.Append(",\"compare\":1");   // 0.8: the Compare chip, written only when on
            // 0.8: the views kept between sessions (PanelModel.RememberedViews: the battle feed's Log, Cards or Timeline); written only when set,
            // so a file without one reads and writes as before
            var views = PanelModel.RememberedViews.Where(k => s.View.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (views.Count > 0) b.Append(",\"views\":{").Append(string.Join(",", views.Select(k => Json.Q(k) + ":" + Json.Q(s.View[k])).ToArray())).Append('}');
            return b.Append('}').ToString();
        }

        /// <summary>Puts what <paramref name="json"/> holds into the state (its filters cleared first); false when it is not a prefs file this version reads.</summary>
        public static bool Apply(string json, PanelState s)
        {
            s.Facets.Clear(); s.OpenFilters.Clear(); s.Everyone = false; s.Compare = false; foreach (var k in PanelModel.RememberedViews) s.View.Remove(k);
            if (!(MiniJson.Parse(json ?? "") is Dictionary<string, object> root)) return false;
            var v = MiniJson.Num(root, "version");
            if (v < 1 || v > Version) return false;
            foreach (var kv in MiniJson.Obj(root, "facets") ?? new Dictionary<string, object>())
            {
                var chips = (kv.Value as List<object> ?? new List<object>()).OfType<string>().Where(x => x.Length > 0).Distinct().ToList();
                if (chips.Count > 0 && kv.Key.IndexOf('|') > 0) s.Facets[kv.Key] = chips;
            }
            if (root.TryGetValue("open", out var open) && open is List<object> l) foreach (var id in l.OfType<string>()) if (id.Length > 0) s.OpenFilters.Add(id);
            s.Everyone = root.TryGetValue("everyone", out var every) && every is bool on && on;
            foreach (var kv in MiniJson.Obj(root, "views") ?? new Dictionary<string, object>())
                if (kv.Value is string pick && pick.Length > 0 && pick.Length <= 32 && Array.IndexOf(PanelModel.RememberedViews, kv.Key) >= 0) s.View[kv.Key] = pick;
            s.Compare = MiniJson.Num(root, "compare") > 0;
            return true;
        }

        /// <summary>Loads the file into the state; no file or an unreadable one: the filters closed and empty. Returns the text it read (the saved state).</summary>
        public static string Load(string path, PanelState s) => Load(path, s, null, null);

        /// <summary>
        /// Loads <paramref name="path"/> into the state: the file, else its .bak when the file is missing or broken (the broken one set
        /// aside, never deleted; <paramref name="note"/> gets one line naming both), else the file from before 0.8
        /// (<paramref name="legacyPath"/>, read only). Nothing usable: the filters closed and empty. Never throws. Returns the state as
        /// read (Save's change check).
        /// </summary>
        public static string Load(string path, PanelState s, string legacyPath, Action<string> note)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && (File.Exists(path) || File.Exists(path + AtomicFile.BackupSuffix)))
                {
                    var text = AtomicFile.ReadWithBackup(path, IsPrefs, out var fromBackup);
                    if (fromBackup)
                    {
                        var aside = AtomicFile.SetAside(path);
                        note?.Invoke("panel filter choices " + path + (aside != null ? " were unreadable (set aside as " + aside + ")" : " were missing") + "; restored from the backup " + path + AtomicFile.BackupSuffix);
                    }
                    if (Apply(text, s)) return ToJson(s);
                }
                else if (!string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath) && Apply(File.ReadAllText(legacyPath, Encoding.UTF8), s)) return ToJson(s);
            }
            catch { }
            s.Facets.Clear(); s.OpenFilters.Clear(); s.Everyone = false; s.Compare = false; foreach (var k in PanelModel.RememberedViews) s.View.Remove(k);
            return ToJson(s);
        }

        /// <summary>
        /// Writes the state when it differs from <paramref name="saved"/> (what is on disk); returns what is on disk now. Atomic with one
        /// backup (AtomicFile). The keys of the file on disk that this version does not know (a newer Hearthwoven's) are written back as
        /// they were; a file of a newer version is left as it is.
        /// </summary>
        public static string Save(string path, PanelState s, string saved)
        {
            var now = ToJson(s);
            if (now == saved || string.IsNullOrEmpty(path)) return saved;
            if (SampleMode.Quiet("panel filter choices")) return now;   // Dev.SampleData: the real file stays untouched
            Dictionary<string, object> onDisk = null;
            try { onDisk = Root(AtomicFile.ReadWithBackup(path, IsPrefs, out _)); } catch { }
            if (onDisk != null && MiniJson.Num(onDisk, "version") > Version) return now;   // a newer Hearthwoven's file: never saved over
            var text = now;
            if (onDisk != null)
            {
                var extra = new StringBuilder();
                foreach (var kv in onDisk) if (!KnownKeys.Contains(kv.Key)) extra.Append(',').Append(Json.Q(kv.Key)).Append(':').Append(MiniJson.Write(kv.Value));
                if (extra.Length > 0) text = now.Substring(0, now.Length - 1) + extra + "}";
            }
            AtomicFile.Write(path, text);
            return now;
        }
    }
}
