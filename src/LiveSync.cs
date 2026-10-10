using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Hearthwoven
{
    /// <summary>
    /// Live small updates between full copies (0.7, work/hearthwoven-0.7/SYNC-DESIGN.md item 4). A delta is the CUMULATIVE difference
    /// between the last full shared copy and the same snapshot built now (Snapshot.Build with the same copyId, both through
    /// GroupShare.SharedCopy), so every number comes from the session tallies the full copy reads: one source of truth, never a second
    /// count. Applying the latest delta on that full copy gives exactly the copy a full send would give now.
    /// Shape: {"live":1,"base":"&lt;copyId&gt;","seq":N,"set":{"/json/pointer":value,...},"del":["/json/pointer",...]}. Objects are
    /// compared key by key, arrays of equal length element by element; an array whose length changed is replaced whole. Numbers keep
    /// their text (never a double round trip: 64-bit player ids stay exact). Pure C#, tested without the game (test/LiveSyncTests.cs).
    /// </summary>
    public static class LiveDelta
    {
        public const int Version = 1;
        /// <summary>One message, never fragments: a bigger delta is not sent, a full copy goes out instead (the new base).</summary>
        public const int MaxPacked = Fragments.Size;
        /// <summary>Unpacked bound on both ends (a real 10 s delta is well under 2 KB).</summary>
        public const int MaxJson = 64 * 1024;

        /// <summary>A number as it was written (its text), so a copy written back keeps every digit.</summary>
        public sealed class Num { public readonly string Raw; public Num(string raw) { Raw = raw; } public override string ToString() => Raw; }

        /// <summary>What one diff gave: the delta's JSON and packed bytes; Empty = nothing changed; TooBig = send a full copy instead.</summary>
        public sealed class Made { public string Text; public byte[] Packed; public bool Empty, TooBig; public string Error; }

        /// <summary>The delta from <paramref name="baseShared"/> (the last full shared copy) to <paramref name="nowShared"/> (the same snapshot
        /// now, same copyId), as seq <paramref name="seq"/> on base <paramref name="baseId"/>.</summary>
        public static Made Make(string baseShared, string nowShared, string baseId, long seq)
        {
            var a = Parse(baseShared); var b = Parse(nowShared);
            if (!(a is Dictionary<string, object>) || !(b is Dictionary<string, object>)) return new Made { Error = "a copy is not one JSON object" };
            var sets = new List<KeyValuePair<string, object>>(); var dels = new List<string>();
            Diff(a, b, "", sets, dels);
            if (sets.Count == 0 && dels.Count == 0) return new Made { Empty = true };
            var text = Write(baseId, seq, sets, dels);
            var packed = Transport.Pack(text);
            return new Made { Text = text, Packed = packed, TooBig = packed.Length > MaxPacked || text.Length > MaxJson };
        }

        static string Write(string baseId, long seq, List<KeyValuePair<string, object>> sets, List<string> dels)
        {
            var b = new StringBuilder();
            b.Append("{\"live\":").Append(Version).Append(",\"base\":").Append(Json.Q(baseId ?? "")).Append(",\"seq\":").Append(seq.ToString(CultureInfo.InvariantCulture)).Append(",\"set\":{");
            for (int i = 0; i < sets.Count; i++) { if (i > 0) b.Append(','); b.Append(Json.Q(sets[i].Key)).Append(':'); Write(b, sets[i].Value, 0); }
            b.Append("},\"del\":[");
            for (int i = 0; i < dels.Count; i++) { if (i > 0) b.Append(','); b.Append(Json.Q(dels[i])); }
            return b.Append("]}").ToString();
        }

        static void Diff(object a, object b, string path, List<KeyValuePair<string, object>> sets, List<string> dels)
        {
            if (a is Dictionary<string, object> da && b is Dictionary<string, object> db)
            {
                foreach (var kv in da) { var p = path + "/" + Esc(kv.Key); if (db.TryGetValue(kv.Key, out var bv)) Diff(kv.Value, bv, p, sets, dels); else dels.Add(p); }
                foreach (var kv in db) if (!da.ContainsKey(kv.Key)) sets.Add(new KeyValuePair<string, object>(path + "/" + Esc(kv.Key), kv.Value));
                return;
            }
            if (a is List<object> la && b is List<object> lb && la.Count == lb.Count)
            {
                for (int i = 0; i < la.Count; i++) Diff(la[i], lb[i], path + "/" + i.ToString(CultureInfo.InvariantCulture), sets, dels);
                return;
            }
            if (!Same(a, b)) sets.Add(new KeyValuePair<string, object>(path, b));
        }

        static bool Same(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is Num na) return b is Num nb && na.Raw == nb.Raw;
            if (a is string sa) return b is string sb && sa == sb;
            if (a is bool ba) return b is bool bb && ba == bb;
            return false;   // containers of different shape: replaced whole
        }

        static string Esc(string key) => key.Replace("~", "~0").Replace("/", "~1");
        static string Unesc(string seg) => seg.Replace("~1", "/").Replace("~0", "~");

        /// <summary>The delta's base copyId and seq; false when it is not a delta of this version.</summary>
        public static bool Read(string deltaText, out string baseId, out long seq)
        {
            baseId = null; seq = 0;
            if (!(Parse(deltaText) is Dictionary<string, object> d)) return false;
            if (!(d.TryGetValue("live", out var v) && v is Num n && n.Raw == Version.ToString(CultureInfo.InvariantCulture))) return false;
            if (!(d.TryGetValue("base", out var bs) && bs is string b) || b.Length == 0) return false;
            if (!(d.TryGetValue("seq", out var sq) && sq is Num sn && long.TryParse(sn.Raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out seq))) return false;
            baseId = b;
            return d.TryGetValue("set", out var s) && s is Dictionary<string, object> && d.TryGetValue("del", out var dl) && dl is List<object>;
        }

        /// <summary>
        /// The full copy <paramref name="baseText"/> with the delta applied, as JSON; null when the delta does not fit it (a path that is
        /// not there): then the copy stays as it is until the next full one. Never adds a delta to a delta: always base + the latest.
        /// </summary>
        public static string Apply(string baseText, string deltaText) => Apply(Parse(baseText), deltaText);

        /// <summary>
        /// The same on the full copy already parsed (Parse; LiveFellows keeps it, 0.8 performance pass: the 26 KB copy was parsed again for every
        /// update, about 0.8 MB of garbage each). <paramref name="baseTree"/> is never changed: only the containers on the delta's paths are copied
        /// (copy on write), so the next update applies onto the same untouched copy. The JSON written is the same as Apply(string, string) writes.
        /// </summary>
        public static string Apply(object baseTree, string deltaText)
        {
            var parsed = Parse(deltaText);
            if (Identity(parsed)) return null;   // whose copy it is changes only with a full copy
            if (!(baseTree is Dictionary<string, object> baseRoot) || !(parsed is Dictionary<string, object> d)) return null;
            if (!(d.TryGetValue("set", out var so) && so is Dictionary<string, object> sets) || !(d.TryGetValue("del", out var dlo) && dlo is List<object> dels)) return null;
            var mine = new HashSet<object>(SameObject.Instance);   // the containers copied for this update (the only ones it may change)
            var root = new Dictionary<string, object>(baseRoot); mine.Add(root);
            foreach (var p in dels)
            {
                if (!(p is string path) || !Parent(root, path, mine, out var parent, out var last) || !(parent is Dictionary<string, object> pd)) return null;
                pd.Remove(last);
            }
            foreach (var kv in sets)
            {
                if (!Parent(root, kv.Key, mine, out var parent, out var last)) return null;
                if (parent is Dictionary<string, object> pd) pd[last] = kv.Value;
                else if (parent is List<object> pl && int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < pl.Count) pl[i] = kv.Value;
                else return null;
            }
            return Write(root);
        }

        // reference identity for the copied containers (net472 has no ReferenceEqualityComparer)
        sealed class SameObject : IEqualityComparer<object>
        {
            public static readonly SameObject Instance = new SameObject();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }

        // a container of the copy, made this update's own before it is changed (a shallow copy; what it holds stays shared)
        static object Own(object v, HashSet<object> mine)
        {
            if (v == null || mine.Contains(v)) return v;
            if (v is Dictionary<string, object> d) { var c = new Dictionary<string, object>(d); mine.Add(c); return c; }
            if (v is List<object> l) { var c = new List<object>(l); mine.Add(c); return c; }
            return v;
        }

        // Parent on the update's own copy: every container on the way down is copied first (copy on write)
        static bool Parent(Dictionary<string, object> root, string path, HashSet<object> mine, out object parent, out string last)
        {
            parent = null; last = null;
            if (string.IsNullOrEmpty(path) || path[0] != '/') return false;
            var segs = path.Substring(1).Split('/');
            object at = root;
            for (int k = 0; k < segs.Length - 1; k++)
            {
                var s = Unesc(segs[k]);
                if (at is Dictionary<string, object> d) { if (!d.TryGetValue(s, out var child)) return false; var own = Own(child, mine); if (!ReferenceEquals(own, child)) d[s] = own; at = own; }
                else if (at is List<object> l && int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < l.Count) { var own = Own(l[i], mine); if (!ReferenceEquals(own, l[i])) l[i] = own; at = own; }
                else return false;
            }
            parent = at; last = Unesc(segs[segs.Length - 1]);
            return at is Dictionary<string, object> || at is List<object>;
        }

        /// <summary>The top-level keys that say whose copy and which copy this is: never changed by an update (the receiver keys a fellow
        /// by them, FellowIds.KeyOf), only by a full copy, which the server stamps with the platform id it knows.</summary>
        public static readonly string[] IdentityKeys = { FellowIds.PlatformField, "playerId", "name", "copyId", "share", "session", "mod", "world" };

        /// <summary>True when the delta sets or deletes one of the IdentityKeys (or the whole copy). Refused by the server, and never applied.</summary>
        public static bool Identity(string deltaText) => Identity(Parse(deltaText));

        static bool Identity(object parsedDelta)
        {
            if (!(parsedDelta is Dictionary<string, object> d)) return true;
            var paths = new List<string>();
            if (d.TryGetValue("set", out var s) && s is Dictionary<string, object> sd) paths.AddRange(sd.Keys);
            if (d.TryGetValue("del", out var dl) && dl is List<object> dd) paths.AddRange(dd.OfType<string>());
            foreach (var p in paths)
            {
                if (p.Length < 2 || p[0] != '/') return true;
                var first = Unesc(p.Substring(1).Split('/')[0]);
                if (IdentityKeys.Contains(first)) return true;
            }
            return false;
        }

        /// <summary>True when the delta carries something a shared copy never does (GroupShare.SharedCopy): a death position or the worlds
        /// played, in a path or inside a value. The server refuses such a delta (a 0.7 client builds deltas from shared copies only).</summary>
        public static bool Private(string deltaText)
        {
            if (string.IsNullOrEmpty(deltaText) || GroupShare.SharedCopy(deltaText) != deltaText) return true;
            if (!(Parse(deltaText) is Dictionary<string, object> d)) return true;
            var paths = new List<string>();
            if (d.TryGetValue("set", out var s) && s is Dictionary<string, object> sd) paths.AddRange(sd.Keys);
            if (d.TryGetValue("del", out var dl) && dl is List<object> dd) paths.AddRange(dd.OfType<string>());
            foreach (var p in paths)
            {
                var segs = p.Split('/').Select(Unesc).ToList();
                if (segs.Contains("secondsPerWorld")) return true;
                if (segs.Contains("deaths") && (segs.Last() == "x" || segs.Last() == "z")) return true;
            }
            return false;
        }

        // ---------- a JSON reader that keeps numbers as their text, and its writer ----------

        public static object Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            int i = 0;
            try { var v = Value(s, ref i, 0); Ws(s, ref i); return i == s.Length ? v : null; } catch { return null; }
        }

        public static string Write(object v) { var b = new StringBuilder(); Write(b, v, 0); return b.ToString(); }

        static void Write(StringBuilder b, object v, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("too deep");
            switch (v)
            {
                case null: b.Append("null"); break;
                case Num n: b.Append(n.Raw); break;
                case string s: Json.AppendQ(b, s); break;   // 0.8: written in place, the same text as Json.Q
                case bool x: b.Append(x ? "true" : "false"); break;
                case Dictionary<string, object> o:
                    b.Append('{'); var first = true;
                    foreach (var kv in o) { if (!first) b.Append(','); first = false; Json.AppendQ(b, kv.Key); b.Append(':'); Write(b, kv.Value, depth + 1); }
                    b.Append('}'); break;
                case List<object> l:
                    b.Append('[');
                    for (int i = 0; i < l.Count; i++) { if (i > 0) b.Append(','); Write(b, l[i], depth + 1); }
                    b.Append(']'); break;
                default: throw new FormatException("not JSON");
            }
        }

        const int MaxDepth = 64;
        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        static bool At(string s, int i, string w) => string.CompareOrdinal(s, i, w, 0, w.Length) == 0;
        static readonly string[] Words = { "-Infinity", "Infinity", "NaN" };

        static object Value(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("too deep");
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i); if (s[i] != '"') throw new FormatException("key");
                    var k = Str(s, ref i); Ws(s, ref i); if (s[i++] != ':') throw new FormatException(":");
                    d[k] = Value(s, ref i, depth + 1); Ws(s, ref i);
                    var e = s[i++]; if (e == '}') return d; if (e != ',') throw new FormatException(",");
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i, depth + 1)); Ws(s, ref i);
                    var e = s[i++]; if (e == ']') return l; if (e != ',') throw new FormatException(",");
                }
            }
            if (c == '"') return Str(s, ref i);
            if (At(s, i, "true")) { i += 4; return true; }
            if (At(s, i, "false")) { i += 5; return false; }
            if (At(s, i, "null")) { i += 4; return null; }
            foreach (var w in Words) if (At(s, i, w)) { i += w.Length; return new Num(w); }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException("value");
            return new Num(s.Substring(start, i - start));
        }

        static string Str(string s, ref int i)
        {
            var b = new StringBuilder(); i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': b.Append('\n'); break; case 't': b.Append('\t'); break; case 'r': b.Append('\r'); break;
                        case 'b': b.Append('\b'); break; case 'f': b.Append('\f'); break;
                        case 'u': b.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; break;
                        default: b.Append(s[i]); break;
                    }
                    i++;
                }
                else b.Append(s[i++]);
            }
            i++;
            return b.ToString();
        }
    }

    /// <summary>
    /// Server: the latest delta of every sharer, in memory only (no disk write per delta), next to the full copy on disk. A delta is
    /// kept only for a player whose shared copy this server holds (Full, from GroupShare.Store; cleared on unshare), on that copy's
    /// copyId, newer than the one kept, at most one per sender per MinGap and MaxPacked bytes. A new full copy drops the delta of the
    /// old one. Requesters get each fellow's latest delta once (For). Bounded: one delta per connected sharer, one served map per
    /// connected requester (Prune). Pure C#, tested without the game.
    /// </summary>
    public class LiveStore
    {
        public const double MinGap = 8;
        public class Entry { public string Key, Fellow, Base; public long Seq, Sender; public byte[] Packed; }
        readonly Dictionary<string, KeyValuePair<string, string>> bases = new Dictionary<string, KeyValuePair<string, string>>();   // key -> (copyId, fellow key)
        readonly Dictionary<string, Entry> latest = new Dictionary<string, Entry>();
        readonly Dictionary<long, double> lastOffer = new Dictionary<long, double>(), lastAsk = new Dictionary<long, double>(), lastTry = new Dictionary<long, double>();
        readonly Dictionary<long, Dictionary<string, string>> served = new Dictionary<long, Dictionary<string, string>>();

        /// <summary>A full shared copy was stored under <paramref name="key"/>: its copyId (none from a client before 0.7) and fellow key.</summary>
        public void Full(string key, string copyId, string fellow)
        {
            if (string.IsNullOrEmpty(key)) return;
            bases[key] = new KeyValuePair<string, string>(copyId ?? "", fellow ?? "");
            if (latest.TryGetValue(key, out var e) && e.Base != copyId) latest.Remove(key);
        }

        /// <summary>The player stopped sharing: nothing of theirs is kept or served.</summary>
        public void Unshare(string key) { if (key == null) return; bases.Remove(key); latest.Remove(key); }

        /// <summary>Keeps a delta; returns null when kept, else why not ("too big", "too often", "not shared", "other copy", "older").</summary>
        public string Offer(string key, long sender, string baseId, long seq, byte[] packed, double now)
        {
            if (packed == null || packed.Length == 0 || packed.Length > LiveDelta.MaxPacked) return "too big";
            if (lastOffer.TryGetValue(sender, out var t) && now - t < MinGap && now >= t) return "too often";
            if (key == null || !bases.TryGetValue(key, out var b)) return "not shared";
            if (string.IsNullOrEmpty(baseId) || b.Key != baseId) return "other copy";
            if (latest.TryGetValue(key, out var had) && had.Base == baseId && had.Seq >= seq) return "older";
            latest[key] = new Entry { Key = key, Fellow = b.Value, Base = baseId, Seq = seq, Sender = sender, Packed = packed };
            lastOffer[sender] = now;
            return null;
        }

        /// <summary>The cheap gate in front of everything else (before any gunzip or parse, as the full copy's RateGate): one message per
        /// sender per MinGap reaches the checks at all, whatever happens to it there.</summary>
        public bool MayOffer(long sender, double now)
        {
            if (lastTry.TryGetValue(sender, out var t) && now - t < MinGap && now >= t) return false;
            lastTry[sender] = now; return true;
        }

        /// <summary>A requester may ask at most once per MinGap.</summary>
        public bool MayAsk(long requester, double now)
        {
            if (lastAsk.TryGetValue(requester, out var t) && now - t < MinGap && now >= t) return false;
            lastAsk[requester] = now; return true;
        }

        /// <summary>The deltas <paramref name="requester"/> has not had yet, its own (<paramref name="ownKey"/>) left out. Not marked here:
        /// Served marks one once it actually went out, so an update dropped on the way is offered again on the next ask.</summary>
        public List<Entry> For(long requester, string ownKey)
        {
            served.TryGetValue(requester, out var had);
            var list = new List<Entry>();
            foreach (var e in latest.Values)
            {
                if (e.Key == ownKey) continue;
                if (had != null && had.TryGetValue(e.Key, out var h) && h == Tag(e)) continue;
                list.Add(e);
            }
            return list;
        }

        /// <summary>This update went out to <paramref name="requester"/>.</summary>
        public void Served(long requester, Entry e)
        {
            if (e == null) return;
            if (!served.TryGetValue(requester, out var had)) served[requester] = had = new Dictionary<string, string>();
            had[e.Key] = Tag(e);
        }

        /// <summary>The requester's updates were cleared (it switched sharing off): the next ask sends them all again.</summary>
        public void Forget(long requester) => served.Remove(requester);

        static string Tag(Entry e) => e.Base + "#" + e.Seq.ToString(CultureInfo.InvariantCulture);

        /// <summary>Forgets everything of peers no longer connected: their deltas (their logout copy is the anchor), rates and served maps.</summary>
        public void Prune(Func<long, bool> connected)
        {
            foreach (var k in latest.Where(kv => !connected(kv.Value.Sender)).Select(kv => kv.Key).ToList()) latest.Remove(k);
            foreach (var p in lastOffer.Keys.Where(p => !connected(p)).ToList()) lastOffer.Remove(p);
            foreach (var p in lastAsk.Keys.Where(p => !connected(p)).ToList()) lastAsk.Remove(p);
            foreach (var p in lastTry.Keys.Where(p => !connected(p)).ToList()) lastTry.Remove(p);
            foreach (var p in served.Keys.Where(p => !connected(p)).ToList()) served.Remove(p);
        }

        public IEnumerable<Entry> All => latest.Values;
        public int Count => latest.Count;
        public int Requesters => served.Count;
    }

    /// <summary>
    /// Client: the fellows' full copies as received (the bases) and each fellow's latest delta. What the book shows for a fellow
    /// (GroupShare.Group) is the base, or base + latest delta when the delta is for that base. A delta that arrives before its full copy
    /// waits for it; a resend of the same full copy keeps the delta applied (no numbers jumping back). Bounded by the fellows there are.
    /// Pure C#, tested without the game.
    /// </summary>
    public class LiveFellows
    {
        readonly Dictionary<string, string> bases = new Dictionary<string, string>();
        readonly Dictionary<string, (string Base, long Seq, string Text)> latest = new Dictionary<string, (string, long, string)>();
        // 0.8: each full copy parsed once (LiveDelta.Parse), on its first update; every later update applies onto it (copy on write)
        readonly Dictionary<string, KeyValuePair<string, object>> trees = new Dictionary<string, KeyValuePair<string, object>>();

        /// <summary>The base copy <paramref name="json"/> of <paramref name="key"/> as a parsed tree, parsed once per copy. Never changed by
        /// LiveDelta.Apply, so a worker thread may apply onto it while the next copy replaces it here.</summary>
        public object TreeOf(string key, string json)
        {
            if (key != null && trees.TryGetValue(key, out var t) && string.Equals(t.Key, json, StringComparison.Ordinal)) return t.Value;
            var tree = LiveDelta.Parse(json);
            if (key != null) trees[key] = new KeyValuePair<string, object>(json, tree);
            return tree;
        }

        public static string CopyIdOf(string json) => Transport.Field(json ?? "", "copyId");

        /// <summary>A full copy arrived for <paramref name="key"/>: returns what to show (the copy, with a waiting delta for it applied).</summary>
        public string OnFull(string key, string json)
        {
            if (string.IsNullOrEmpty(key) || json == null) return json;
            bases[key] = json;
            var id = CopyIdOf(json);
            if (id.Length > 0 && latest.TryGetValue(key, out var d) && d.Base == id) return SameFellow(key, LiveDelta.Apply(TreeOf(key, json), d.Text)) ?? json;
            return json;
        }

        /// <summary>A delta arrived: returns the copy to show now, or null (older than the one applied, or its full copy is not here yet).</summary>
        public string OnDelta(string key, string baseId, long seq, string text) =>
            Accept(key, baseId, seq, text, out var tree) ? SameFellow(key, LiveDelta.Apply(tree, text)) : null;

        /// <summary>
        /// The bookkeeping half of OnDelta (0.8): true when the delta is newer than the one applied and its full copy is here; then
        /// <paramref name="tree"/> is that copy parsed, to apply onto (LiveDelta.Apply, also on a worker: GroupShare.TakeLive).
        /// </summary>
        public bool Accept(string key, string baseId, long seq, string text, out object tree)
        {
            tree = null;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(baseId)) return false;
            if (latest.TryGetValue(key, out var had) && had.Base == baseId && had.Seq >= seq) return false;
            latest[key] = (baseId, seq, text);
            if (!bases.TryGetValue(key, out var b) || CopyIdOf(b) != baseId) return false;
            tree = TreeOf(key, b);
            return true;
        }

        /// <summary>Still the update to show for <paramref name="key"/> (a later one or a new full copy did not come meanwhile).</summary>
        public bool Current(string key, string baseId, long seq) =>
            key != null && latest.TryGetValue(key, out var had) && had.Base == baseId && had.Seq == seq && bases.TryGetValue(key, out var b) && CopyIdOf(b) == baseId;

        // an update never turns one fellow's copy into someone else's (the book re-keys a copy by its content)
        public static string SameFellow(string key, string shown) => shown != null && FellowIds.KeyOf(shown) == key ? shown : null;

        /// <summary>Only the fellows still there (GroupShare.Group's keys) are kept.</summary>
        public void KeepOnly(ICollection<string> keys)
        {
            foreach (var k in bases.Keys.Where(k => !keys.Contains(k)).ToList()) bases.Remove(k);
            foreach (var k in latest.Keys.Where(k => !keys.Contains(k)).ToList()) latest.Remove(k);
            foreach (var k in trees.Keys.Where(k => !bases.ContainsKey(k)).ToList()) trees.Remove(k);
        }

        public void Clear() { bases.Clear(); latest.Clear(); trees.Clear(); }
        public int Count => bases.Count + latest.Count;
    }
}
