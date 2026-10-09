using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// Who a fellow player is (RESILIENCE-06 item 8): fellows are keyed by identity, shown by name.
    /// Key, best first:
    ///   1. "&lt;platform id&gt;#&lt;profile id&gt;": the platform id as the game gives it ("Steam_7656…", "Xbox_…"; a Steam socket's bare
    ///      number gets the game's own "Steam_" prefix), which a 0.6 server writes into each shared copy (WithPlatform), plus the
    ///      character's profile id, so two characters of one account stay apart;
    ///   2. "id:&lt;profile id&gt;": a copy from a server before this fix (no platform id; every snapshot carries its playerId);
    ///   3. "name:&lt;name&gt;": a copy with neither, the old way, marked by the prefix (ByName).
    /// Display: the name; two fellows with the same name (or a fellow with yours) both show, the later one as "Rowan (2)". A label
    /// sticks to its key for as long as this list lives (a connection), so a chip and its colour stay with the same person.
    /// Pure C#: tested without the game (test/FellowTests.cs).
    /// </summary>
    public class FellowIds
    {
        public const string IdPrefix = "id:", NamePrefix = "name:", PlatformField = "platformId";

        /// <summary>The platform id as the game gives it, made comparable: a bare Steam number gets "Steam_" (as ZNet's admin check
        /// does), anything else ("Steam_…", "Xbox_…", a PlayFab id) stays as it is; "" when unknown.</summary>
        public static string PlatformOf(string host)
        {
            var h = (host ?? "").Trim();
            if (h.Length == 0 || h == "0") return "";
            return h.All(char.IsDigit) ? "Steam_" + h : h;
        }

        /// <summary>A shared copy with the sender's platform id in front (server side, or the host for its own copy). A copy that
        /// already starts with one keeps it; no platform id: unchanged.</summary>
        public static string WithPlatform(string json, string host)
        {
            var p = PlatformOf(host);
            if (string.IsNullOrEmpty(json) || p.Length == 0 || !json.StartsWith("{", StringComparison.Ordinal)) return json;
            if (json.StartsWith("{\"" + PlatformField + "\":", StringComparison.Ordinal)) return json;
            return "{" + "\"" + PlatformField + "\":" + Json.Q(p) + (json.Length > 2 && json[1] != '}' ? "," : "") + json.Substring(1);
        }

        /// <summary>The key of one shared copy (see the class); "" when the copy says nothing about who it is.</summary>
        public static string KeyOf(string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var platform = PlatformOf(Transport.Field(json, PlatformField));
            var id = PeerIdentity.SnapshotPlayerId(json);
            var idText = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (platform.Length > 0) return id != 0 ? platform + "#" + idText : platform;
            if (id != 0) return IdPrefix + idText;
            var name = Transport.Field(json, "name");
            return name.Length > 0 ? NamePrefix + name : "";
        }

        /// <summary>Keyed the old way, by name only (the fallback, marked).</summary>
        public static bool ByName(string key) => key != null && key.StartsWith(NamePrefix, StringComparison.Ordinal);

        // ---------- the received copies ----------

        readonly Dictionary<string, string> copies = new Dictionary<string, string>();   // key -> shared copy
        readonly Dictionary<string, string> names = new Dictionary<string, string>();    // key -> the name inside it
        readonly Dictionary<string, string> labels = new Dictionary<string, string>();   // key -> shown label (sticks)
        readonly List<string> order = new List<string>();                                // keys in the order they first arrived

        /// <summary>key -> shared copy (GroupShare.Group).</summary>
        public IDictionary<string, string> Copies => copies;
        public int Count => copies.Count;

        /// <summary>Keeps one received copy under its key; returns the key, or null for a copy without name or identity.</summary>
        public string Receive(string json)
        {
            var name = Transport.Field(json ?? "", "name");
            var key = KeyOf(json);
            if (name.Length == 0 || key.Length == 0) return null;
            // a copy keyed by name only stands in for nobody who is already known by id under that name
            if (ByName(key) && names.Any(kv => !ByName(kv.Key) && Same(kv.Value, name))) return null;
            // the same character arriving with a better key (the server was updated): the weaker copies of it go
            foreach (var weaker in copies.Keys.Where(k => k != key && Weaker(k, key, name)).ToList()) Forget(weaker);
            if (!order.Contains(key)) order.Add(key);
            copies[key] = json; names[key] = name;
            return key;
        }

        // "id:5" and "name:Edda" are the same fellow as "Steam_1#5" or "id:5" named Edda: replaced by the better key
        bool Weaker(string k, string better, string name)
        {
            if (ByName(better)) return false;
            if (ByName(k)) return names.TryGetValue(k, out var n) && Same(n, name);
            if (!k.StartsWith(IdPrefix, StringComparison.Ordinal) || better.StartsWith(IdPrefix, StringComparison.Ordinal)) return false;
            var hash = better.LastIndexOf('#');
            return hash > 0 && better.Substring(hash + 1) == k.Substring(IdPrefix.Length);
        }

        void Forget(string key) { copies.Remove(key); names.Remove(key); labels.Remove(key); order.Remove(key); }

        /// <summary>The server's list of current sharers: <paramref name="keys"/> from a server with this fix, else the names (an older
        /// server): everyone not on it is forgotten (they stopped sharing).</summary>
        public void KeepOnly(ICollection<string> keys, ICollection<string> listNames)
        {
            var nameSet = new HashSet<string>(listNames ?? new string[0], StringComparer.OrdinalIgnoreCase);
            var keySet = keys == null ? null : new HashSet<string>(keys, StringComparer.Ordinal);
            foreach (var k in copies.Keys.ToList())
                if (keySet != null ? !keySet.Contains(k) : !nameSet.Contains(names[k])) Forget(k);
        }

        public void Clear() { copies.Clear(); names.Clear(); labels.Clear(); order.Clear(); }

        public string NameOf(string key) => key != null && names.TryGetValue(key, out var n) ? n : null;

        static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Every fellow as (key, label), in arrival order: the label is the name, or "Name (2)", "Name (3)" when the name is already
        /// shown, yours (<paramref name="self"/>) included. A label once given stays with its key while it is still unique.
        /// </summary>
        public List<KeyValuePair<string, string>> Labelled(string self)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(self)) taken.Add(self);
            var result = new List<KeyValuePair<string, string>>();
            // first the labels already given (so nobody's label moves to someone else), then the newcomers
            foreach (var pass in new[] { true, false })
                foreach (var k in order)
                {
                    bool had = labels.TryGetValue(k, out var l);
                    if (pass != had) continue;
                    if (!had || taken.Contains(l)) l = Free(names[k], taken);
                    labels[k] = l; taken.Add(l);
                }
            foreach (var k in order) result.Add(new KeyValuePair<string, string>(k, labels[k]));
            return result;
        }

        static string Free(string name, HashSet<string> taken)
        {
            if (!taken.Contains(name)) return name;
            for (int n = 2; ; n++) { var l = name + " (" + n + ")"; if (!taken.Contains(l)) return l; }
        }

        /// <summary>The key behind a shown label (Labelled first); null when nobody has it.</summary>
        public string KeyOfLabel(string label, string self)
        {
            if (string.IsNullOrEmpty(label)) return null;
            foreach (var kv in Labelled(self)) if (string.Equals(kv.Value, label, StringComparison.OrdinalIgnoreCase)) return kv.Key;
            return null;
        }
    }
}
