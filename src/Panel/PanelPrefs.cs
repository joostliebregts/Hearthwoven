using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// What a player set on the panel's filters, kept across panel opens and game sessions (Joost 2026-10-09: "closed by default, but
    /// remember what a player set, per page"): the chips chosen in each filter bar (PanelState.Facets) and which bars are open
    /// (PanelState.OpenFilters). One small file per character next to the local totals: BepInEx/Hearthwoven/local/&lt;playerId&gt;.panel.json,
    /// written atomically (a temp file, then a replace), only when something changed. A file that cannot be read is ignored: the
    /// filters start closed and empty, as on a first run. Nothing here is a count; it never touches the totals file.
    /// </summary>
    public static class PanelPrefs
    {
        public const int Version = 1;

        public static string PathFor(string hearthwovenDir, long playerId) =>
            Path.Combine(Path.Combine(hearthwovenDir, "local"), playerId.ToString(CultureInfo.InvariantCulture) + ".panel.json");

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
            b.Append("},\"open\":[").Append(string.Join(",", s.OpenFilters.OrderBy(x => x, StringComparer.Ordinal).Select(Json.Q).ToArray())).Append("]}");
            return b.ToString();
        }

        /// <summary>Puts what <paramref name="json"/> holds into the state (its filters cleared first); false when it is not a prefs file this version reads.</summary>
        public static bool Apply(string json, PanelState s)
        {
            s.Facets.Clear(); s.OpenFilters.Clear();
            if (!(MiniJson.Parse(json ?? "") is Dictionary<string, object> root)) return false;
            var v = MiniJson.Num(root, "version");
            if (v < 1 || v > Version) return false;
            foreach (var kv in MiniJson.Obj(root, "facets") ?? new Dictionary<string, object>())
            {
                var chips = (kv.Value as List<object> ?? new List<object>()).OfType<string>().Where(x => x.Length > 0).Distinct().ToList();
                if (chips.Count > 0 && kv.Key.IndexOf('|') > 0) s.Facets[kv.Key] = chips;
            }
            if (root.TryGetValue("open", out var open) && open is List<object> l) foreach (var id in l.OfType<string>()) if (id.Length > 0) s.OpenFilters.Add(id);
            return true;
        }

        /// <summary>Loads the file into the state; no file or an unreadable one: the filters closed and empty. Returns the text it read (the saved state).</summary>
        public static string Load(string path, PanelState s)
        {
            try
            {
                if (File.Exists(path) && Apply(File.ReadAllText(path, Encoding.UTF8), s)) return ToJson(s);
            }
            catch { }
            s.Facets.Clear(); s.OpenFilters.Clear();
            return ToJson(s);
        }

        /// <summary>Writes the state when it differs from <paramref name="saved"/> (what is on disk); returns what is on disk now.</summary>
        public static string Save(string path, PanelState s, string saved)
        {
            var now = ToJson(s);
            if (now == saved || string.IsNullOrEmpty(path)) return saved;
            if (SampleMode.Quiet("panel filter choices")) return now;   // Dev.SampleData: the real file stays untouched
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, now, new UTF8Encoding(false));
            if (!File.Exists(path)) File.Move(tmp, path);
            else
            {
                try { File.Replace(tmp, path, null); }
                catch (PlatformNotSupportedException) { File.Copy(tmp, path, true); File.Delete(tmp); }
            }
            return now;
        }
    }
}
