using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Hearthwoven
{
    /// <summary>
    /// The server's book (0.6), pure C# so the tests run it without the game. Two things only the server can see whole:
    ///
    /// Cargo loaded and unloaded (FEASIBILITY-06 option A, a real evening's iron haul): for every ship and cart (never a chest:
    /// chests are storage, not transport) a first-in-first-out list of lots. A put adds a lot (item, count, where, who); a take
    /// uses up the oldest lots of that item and books count x the straight line between where the lot went in and where it
    /// came out. The leg is credited twice, to two different questions, and never summed: "sent" to the one who loaded it,
    /// "delivered" to the one who unloaded it. A put or take with nobody at the container (via "unattended": a mod or
    /// automation) credits nobody for that side. A take with no lot behind it (cargo loaded before the book began) books
    /// nothing: a floor, never an overcount.
    ///
    /// Born near (FEASIBILITY-06 section 2, the server watcher): a tamed young animal appearing for the first time is a
    /// birth or a hatch, credited to every player within NearRange metres as "born near", never "bred by".
    ///
    /// Bounded: at most MaxCarriers ships and carts (the one untouched longest goes first), MaxLots lots per carrier (the oldest
    /// goes first; its cargo then has no start and counts nothing), MaxKinds items or creatures per player and table, MaxPlayers
    /// players. Restart-safe through ToJson / FromJson, which the server writes with every world save, so a restart without a
    /// save rolls the book back with the world (INTEGRITY C12). Replay rebuilds the cargo once from the chest logs.
    /// </summary>
    public class ServerBook
    {
        public const int MaxCarriers = 128, MaxLots = 200, MaxKinds = 150, MaxPlayers = 500, SharedKinds = 24, FormatVersion = 1;
        /// <summary>A young animal counts as born near a player within this many metres (the same 40 m as "born in your care").</summary>
        public const float NearRange = 40f;
        /// <summary>A put of the same item by the same player within this many metres of the newest lot joins that lot (a stack split in two puts).</summary>
        const double JoinMetres = 5;

        public class Lot { public string Item, Who; public int Count; public double X, Z; }
        class Carrier { public readonly List<Lot> Lots = new List<Lot>(); public long Touched; }
        public class Totals
        {
            public readonly Dictionary<string, double> Sent = new Dictionary<string, double>(), Delivered = new Dictionary<string, double>(), Born = new Dictionary<string, double>();
        }

        readonly Dictionary<string, Carrier> carriers = new Dictionary<string, Carrier>();
        readonly Dictionary<string, Totals> players = new Dictionary<string, Totals>();
        long clock;
        /// <summary>When the cargo book begins (its first ship or cart event) and when births began to be watched; null = not yet.</summary>
        public DateTime? CargoFrom, BornFrom;
        /// <summary>Top-level keys a newer Hearthwoven wrote (0.8, RESILIENCE item 4): kept as read and written back with every save.</summary>
        public readonly Dictionary<string, object> Extra = new Dictionary<string, object>();
        static readonly HashSet<string> KnownKeys = new HashSet<string> { "version", "clock", "cargoFrom", "bornFrom", "carriers", "players" };
        /// <summary>Grows with every credit (not saved): a reader can tell that something changed.</summary>
        public int Version { get; private set; }

        public static bool IsCarrier(string kind) => kind == "ship" || kind == "cart";
        public int Carriers => carriers.Count;
        public int LotsIn(string container) => carriers.TryGetValue(container ?? "", out var c) ? c.Lots.Count : 0;
        public int Players => players.Count;
        public Totals Of(string key) => key != null && players.TryGetValue(key, out var t) ? t : null;

        /// <summary>
        /// One line of the chest log, as ChestWatch writes it. Only ships and carts, only put and take; anything else is ignored.
        /// <paramref name="who"/> is the player key (the player id), null or "" when not known. Returns the item-metres the take booked.
        /// </summary>
        public double Cargo(string container, string kind, string action, string via, string who, string item, int count, double x, double z, DateTime t)
        {
            if (!IsCarrier(kind) || count <= 0 || string.IsNullOrEmpty(container) || string.IsNullOrEmpty(item) || (action != "put" && action != "take")) return 0;
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z)) return 0;
            var credit = via == "unattended" || string.IsNullOrEmpty(who) ? null : who;
            if (CargoFrom == null || t < CargoFrom) CargoFrom = t;
            if (action == "put")
            {
                var c = CarrierFor(container);
                var last = c.Lots.Count > 0 ? c.Lots[c.Lots.Count - 1] : null;
                if (last != null && last.Item == item && last.Who == credit && Math.Abs(last.X - x) + Math.Abs(last.Z - z) <= JoinMetres) last.Count += count;
                else c.Lots.Add(new Lot { Item = item, Count = count, X = x, Z = z, Who = credit });
                if (c.Lots.Count > MaxLots) c.Lots.RemoveAt(0);
                return 0;
            }
            if (!carriers.TryGetValue(container, out var from)) return 0;   // nothing was ever put in: no lot, no start, no credit
            from.Touched = ++clock;
            double booked = 0; int left = count;
            foreach (var lot in from.Lots)
            {
                if (left <= 0) break;
                if (lot.Item != item || lot.Count <= 0) continue;
                int k = Math.Min(left, lot.Count); lot.Count -= k; left -= k;
                var im = k * Math.Sqrt((x - lot.X) * (x - lot.X) + (z - lot.Z) * (z - lot.Z));
                if (im <= 0) continue;
                booked += im;
                if (lot.Who != null) Add(lot.Who, p => p.Sent, item, im);
                if (credit != null) Add(credit, p => p.Delivered, item, im);
            }
            from.Lots.RemoveAll(l => l.Count <= 0);
            return booked;
        }

        /// <summary>A tamed young animal appeared: one birth for every player key in <paramref name="near"/> (each once).</summary>
        public void Birth(string creature, IEnumerable<string> near)
        {
            if (string.IsNullOrEmpty(creature) || near == null) return;
            foreach (var key in near.Where(k => !string.IsNullOrEmpty(k)).Distinct()) Add(key, p => p.Born, creature, 1);
        }

        /// <summary>The keys of the players within NearRange of a point (3D, as the client's "born in your care" measures).</summary>
        public static List<string> Near(IEnumerable<KeyValuePair<string, float[]>> players, float x, float y, float z)
        {
            var near = new List<string>();
            foreach (var p in players ?? Enumerable.Empty<KeyValuePair<string, float[]>>())
            {
                if (string.IsNullOrEmpty(p.Key) || p.Value == null || p.Value.Length < 3) continue;
                float dx = p.Value[0] - x, dy = p.Value[1] - y, dz = p.Value[2] - z;
                if (dx * dx + dy * dy + dz * dz <= NearRange * NearRange && !near.Contains(p.Key)) near.Add(p.Key);
            }
            return near;
        }

        Carrier CarrierFor(string id)
        {
            if (!carriers.TryGetValue(id, out var c))
            {
                if (carriers.Count >= MaxCarriers) carriers.Remove(carriers.OrderBy(kv => kv.Value.Touched).First().Key);   // the one untouched longest
                carriers[id] = c = new Carrier();
            }
            c.Touched = ++clock;
            return c;
        }

        void Add(string key, Func<Totals, Dictionary<string, double>> table, string what, double v)
        {
            if (!players.TryGetValue(key, out var p))
            {
                if (players.Count >= MaxPlayers) return;
                players[key] = p = new Totals();
            }
            var d = table(p);
            if (!d.ContainsKey(what) && d.Count >= MaxKinds) return;
            d.TryGetValue(what, out var o); d[what] = o + v;
            Version++;
        }

        // ---------- what a player's book shows ----------

        /// <summary>
        /// One player's numbers as the server shares them: {"cargoSent":{item:item-metres},"cargoDelivered":{...},"bornNear":{creature:n},
        /// "cargoFrom":"...","bornFrom":"..."}, the SharedKinds largest of each table, items named by <paramref name="itemName"/>
        /// (the server passes the game's token). null when the player has none of them (no ghosts).
        /// </summary>
        public string SharedJson(string key, Func<string, string> itemName = null)
        {
            var p = Of(key);
            if (p == null || (p.Sent.Count == 0 && p.Delivered.Count == 0 && p.Born.Count == 0)) return null;
            var b = new StringBuilder("{");
            bool first = true;
            void Table(string name, Dictionary<string, double> d, Func<string, string> rename)
            {
                if (d.Count == 0) return;
                var merged = new Dictionary<string, double>();
                foreach (var kv in d) { var k = rename?.Invoke(kv.Key) ?? kv.Key; merged.TryGetValue(k, out var o); merged[k] = o + kv.Value; }
                if (!first) b.Append(','); first = false;
                b.Append(Json.Q(name)).Append(':');
                WriteDict(b, merged.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(SharedKinds));
            }
            Table("cargoSent", p.Sent, itemName); Table("cargoDelivered", p.Delivered, itemName); Table("bornNear", p.Born, null);
            if (CargoFrom.HasValue) b.Append(",\"cargoFrom\":").Append(Json.Q(Iso(CargoFrom.Value)));
            if (BornFrom.HasValue) b.Append(",\"bornFrom\":").Append(Json.Q(Iso(BornFrom.Value)));
            return b.Append('}').ToString();
        }

        /// <summary>What a client reads back from SharedJson: item-metres loaded and unloaded per item, births near per creature.</summary>
        public class Shared
        {
            public readonly Dictionary<string, double> Sent = new Dictionary<string, double>(), Delivered = new Dictionary<string, double>(), BornNear = new Dictionary<string, double>();
            public DateTime? CargoFrom, BornFrom;
            public double SentTotal => Sent.Values.Where(v => v > 0).Sum();
            public double DeliveredTotal => Delivered.Values.Where(v => v > 0).Sum();
            public double BornTotal => BornNear.Values.Where(v => v > 0).Sum();

            public static Shared Read(Dictionary<string, object> o)
            {
                if (o == null) return null;
                var s = new Shared();
                void Into(string k, Dictionary<string, double> to) { var d = MiniJson.Obj(o, k); if (d == null) return; foreach (var kv in d) if (kv.Value is double v && v > 0 && to.Count < MaxKinds) to[kv.Key] = v; }
                Into("cargoSent", s.Sent); Into("cargoDelivered", s.Delivered); Into("bornNear", s.BornNear);
                s.CargoFrom = Date(MiniJson.Str(o, "cargoFrom")); s.BornFrom = Date(MiniJson.Str(o, "bornFrom"));
                return s.Sent.Count + s.Delivered.Count + s.BornNear.Count == 0 ? null : s;
            }
        }

        // ---------- saved with the world ----------

        public string ToJson()
        {
            var b = new StringBuilder("{\"version\":").Append(FormatVersion).Append(",\"clock\":").Append(clock.ToString(CultureInfo.InvariantCulture));
            if (CargoFrom.HasValue) b.Append(",\"cargoFrom\":").Append(Json.Q(Iso(CargoFrom.Value)));
            if (BornFrom.HasValue) b.Append(",\"bornFrom\":").Append(Json.Q(Iso(BornFrom.Value)));
            b.Append(",\"carriers\":{");
            bool first = true;
            foreach (var kv in carriers)
            {
                if (kv.Value.Lots.Count == 0) continue;   // an empty carrier needs no memory
                if (!first) b.Append(','); first = false;
                b.Append(Json.Q(kv.Key)).Append(":{\"touched\":").Append(kv.Value.Touched.ToString(CultureInfo.InvariantCulture)).Append(",\"lots\":[");
                for (int i = 0; i < kv.Value.Lots.Count; i++)
                {
                    var l = kv.Value.Lots[i];
                    if (i > 0) b.Append(',');
                    b.Append('[').Append(Json.Q(l.Item)).Append(',').Append(l.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
                     .Append(Json.F(l.X)).Append(',').Append(Json.F(l.Z)).Append(',').Append(Json.Q(l.Who ?? "")).Append(']');
                }
                b.Append("]}");
            }
            b.Append("},\"players\":{");
            first = true;
            foreach (var kv in players)
            {
                if (!first) b.Append(','); first = false;
                b.Append(Json.Q(kv.Key)).Append(":{\"sent\":"); WriteDict(b, kv.Value.Sent);
                b.Append(",\"delivered\":"); WriteDict(b, kv.Value.Delivered);
                b.Append(",\"born\":"); WriteDict(b, kv.Value.Born);
                b.Append('}');
            }
            b.Append('}');
            foreach (var kv in Extra) if (!KnownKeys.Contains(kv.Key)) b.Append(',').Append(Json.Q(kv.Key)).Append(':').Append(MiniJson.Write(kv.Value));   // a newer version's data, kept as read
            return b.Append('}').ToString();
        }

        /// <summary>The book a ToJson wrote. null when the text is not one (unreadable, or a newer format this version must not overwrite).</summary>
        public static ServerBook FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) return null;
            var version = MiniJson.Num(root, "version");
            if (version < 1 || version > FormatVersion) return null;
            var book = new ServerBook { clock = (long)MiniJson.Num(root, "clock"), CargoFrom = Date(MiniJson.Str(root, "cargoFrom")), BornFrom = Date(MiniJson.Str(root, "bornFrom")) };
            foreach (var kv in MiniJson.Obj(root, "carriers") ?? new Dictionary<string, object>())
            {
                if (!(kv.Value is Dictionary<string, object> co) || book.carriers.Count >= MaxCarriers) continue;
                var c = new Carrier { Touched = (long)MiniJson.Num(co, "touched") };
                if (co.TryGetValue("lots", out var ls) && ls is List<object> lots)
                    foreach (var o in lots)
                    {
                        if (!(o is List<object> a) || a.Count < 5 || !(a[0] is string item) || !(a[1] is double n) || !(a[2] is double x) || !(a[3] is double z) || n <= 0) continue;
                        if (c.Lots.Count >= MaxLots) break;
                        c.Lots.Add(new Lot { Item = item, Count = (int)n, X = x, Z = z, Who = a[4] is string w && w.Length > 0 ? w : null });
                    }
                book.carriers[kv.Key] = c;
            }
            foreach (var kv in MiniJson.Obj(root, "players") ?? new Dictionary<string, object>())
            {
                if (!(kv.Value is Dictionary<string, object> po) || book.players.Count >= MaxPlayers) continue;
                var p = new Totals();
                void Into(string k, Dictionary<string, double> to) { foreach (var e in MiniJson.Obj(po, k) ?? new Dictionary<string, object>()) if (e.Value is double v && v > 0 && to.Count < MaxKinds) to[e.Key] = v; }
                Into("sent", p.Sent); Into("delivered", p.Delivered); Into("born", p.Born);
                book.players[kv.Key] = p;
            }
            foreach (var kv in root) if (!KnownKeys.Contains(kv.Key)) book.Extra[kv.Key] = kv.Value;
            return book;
        }

        // ---------- on disk (server-book-<world>.<id>.json) ----------

        /// <summary>
        /// The book file of one world (0.8, RESILIENCE item 1): server-book-&lt;name&gt;.&lt;id&gt;.json, <paramref name="uid"/> = the game's
        /// world id (World.m_uid) as text. A world made again under the same name has a new id, so it gets its own book; a renamed world
        /// (same id) keeps its book, found by the id under its old name. A book from before 0.8 (server-book-&lt;name&gt;.json, no id) is
        /// adopted once, by the first world of that name that loads it: copied to that world's file, then renamed to
        /// "&lt;file&gt;.migrated-&lt;id&gt;" (kept, never deleted), so the next world of that name starts its own. No id: the name-only file,
        /// as before. <paramref name="note"/> says when a file was adopted.
        /// </summary>
        public static string BookPath(string root, string world, string uid, out string note)
        {
            note = null;
            var name = Transport.SafeName(world).Length > 0 ? Transport.SafeName(world) : "world";
            var legacy = Path.Combine(root, "server-book-" + name + ".json");
            if (string.IsNullOrEmpty(uid)) return legacy;
            var path = Path.Combine(root, "server-book-" + name + "." + uid + ".json");
            if (OnDisk(path) || !Directory.Exists(root)) return path;
            var tail = "." + uid + ".json";
            foreach (var f in Directory.GetFiles(root, "server-book-*" + tail + "*"))   // the world was renamed: its id's book under the old name
            {
                var main = f.EndsWith(AtomicFile.BackupSuffix, StringComparison.Ordinal) ? f.Substring(0, f.Length - AtomicFile.BackupSuffix.Length) : f;
                if (main.EndsWith(tail, StringComparison.Ordinal) && Path.GetFileName(main).IndexOf('.') == Path.GetFileName(main).Length - tail.Length) return main;
            }
            if (!OnDisk(legacy)) return path;
            foreach (var from in new[] { legacy + AtomicFile.BackupSuffix, legacy })
                if (File.Exists(from)) File.Copy(from, from == legacy ? path : path + AtomicFile.BackupSuffix, false);
            var moved = legacy + ".migrated-" + uid;
            try
            {
                File.Move(legacy, moved);
                if (File.Exists(legacy + AtomicFile.BackupSuffix)) File.Move(legacy + AtomicFile.BackupSuffix, moved + AtomicFile.BackupSuffix);
                note = "server book " + Path.GetFileName(legacy) + " (from before 0.8) belongs to world " + world + " (id " + uid + "): copied to " + Path.GetFileName(path) + ", the old file kept as " + Path.GetFileName(moved);
            }
            catch (Exception e) { note = "server book " + Path.GetFileName(legacy) + " copied to " + Path.GetFileName(path) + " but could not be renamed (" + e.Message + ")"; }
            return path;
        }

        static bool OnDisk(string file) => File.Exists(file) || File.Exists(file + AtomicFile.BackupSuffix);

        /// <summary>
        /// The book at <paramref name="file"/>: the file itself, else its .bak (the save before, AtomicFile), else null for a new one.
        /// A main file that is unreadable or a newer format is set aside as .unread-&lt;time&gt;, never overwritten or deleted; the
        /// .bak stays as it is. <paramref name="note"/> says what happened when it was not the plain case.
        /// </summary>
        public static ServerBook Load(string file, out string note)
        {
            note = null;
            if (!File.Exists(file) && !File.Exists(file + AtomicFile.BackupSuffix)) return null;
            var text = AtomicFile.ReadWithBackup(file, t => ServerBook.FromJson(t) != null, out var fromBackup);
            var book = text == null ? null : ServerBook.FromJson(text);
            if ((fromBackup || book == null) && File.Exists(file))
            {
                var aside = file + ".unread-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                for (int n = 2; File.Exists(aside); n++) aside = file + ".unread-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "-" + n;
                File.Move(file, aside);   // unreadable or a newer format: kept for a person to look at (no data loss)
                note = "server book not readable, set aside as " + Path.GetFileName(aside) + (book != null ? "; the backup copy (the save before) is used" : "; a new one starts");
            }
            else if (fromBackup) note = "server book missing; the backup copy (the save before) is used";
            return book;
        }

        /// <summary>A dedicated server without a book (ServerBookHooks.Start): the cargo again from this world's chest logs, plain or compressed (C12, I8).</summary>
        public ReplayResult ReplayLogs(string root, string worldName, out int files, string worldUid = null)
        {
            lock (DailyLogs.Gate)
            {
                var list = DailyLogs.Files(root, "chests");
                files = list.Count;
                return Replay(list.SelectMany(DailyLogs.ReadLines), worldName, worldUid);
            }
        }

        // ---------- rebuilt once from the chest logs ----------

        /// <summary>What a replay did, for the server log. OtherWorld: ship and cart lines left out because another world wrote them.</summary>
        public class ReplayResult { public int Lines, Applied, RolledBack, Unreadable, OtherWorld; }

        /// <summary>
        /// The chest log's lines in order (all days, oldest first). Events are held until the next "world-saved" marker and dropped at a
        /// "server-start" that follows without a save (that restart rolled them back with the world, INTEGRITY C12). Events after the
        /// last save are dropped as well: the world this server loads is the one of that save. A line with playerId 0 credits nobody.
        /// Per world (RESILIENCE-06 I8): with <paramref name="world"/> given, only events of that world count. The world of an event is
        /// the one its "server-start" marker names (0.6.1 on); events under markers without a name (written before 0.6.1) belong to
        /// the first world any marker names (the world the server ran when it got 0.6.1), or to <paramref name="world"/> when no marker
        /// names one. <paramref name="lines"/> is read twice in that case (once for the names); null world = every world.
        /// 0.8 (RESILIENCE item 1): a world made again under the same name is another world. Markers name the world's id too
        /// ("worldUid"); with <paramref name="worldUid"/> given only events of that id count. Events under markers without an id (before
        /// 0.8) of this world's name belong to the first id a marker names for that name (the world that ran when the server got 0.8),
        /// or to <paramref name="worldUid"/> when none does.
        /// </summary>
        public ReplayResult Replay(IEnumerable<string> lines, string world = null, string worldUid = null)
        {
            var r = new ReplayResult();
            var held = new List<Dictionary<string, object>>();
            string legacyWorld = world == null ? null : FirstWorld(lines) ?? world;
            string legacyUid = world == null || worldUid == null ? null : FirstUid(lines, world) ?? worldUid;
            string segment = null, segmentUid = null;   // the world of the events being read now; null = a marker without a name (before 0.6.1) or id (before 0.8)
            bool Mine() => world == null || ((segment ?? legacyWorld) == world && (worldUid == null || (segmentUid ?? legacyUid) == worldUid));
            foreach (var line in lines ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                r.Lines++;
                if (!(MiniJson.Parse(line) is Dictionary<string, object> o)) { r.Unreadable++; continue; }
                var marker = MiniJson.Str(o, "marker");
                var named = MiniJson.Str(o, "world", null);
                var uid = MiniJson.Str(o, "worldUid", null); if (uid == "") uid = null;
                if (marker == "world-saved")
                {
                    if (named != null) { segment = named; segmentUid = uid; }
                    if (Mine()) { foreach (var e in held) Apply(e); r.Applied += held.Count; } else r.OtherWorld += held.Count;
                    held.Clear(); continue;
                }
                if (marker == "server-start") { if (Mine()) r.RolledBack += held.Count; else r.OtherWorld += held.Count; held.Clear(); segment = named; segmentUid = uid; continue; }
                if (marker.Length > 0) continue;
                if (IsCarrier(MiniJson.Str(o, "kind"))) held.Add(o);
            }
            if (Mine()) r.RolledBack += held.Count; else r.OtherWorld += held.Count;
            return r;
        }

        /// <summary>The first world a marker names, or null (cheap: only marker lines that carry a world are parsed).</summary>
        static string FirstWorld(IEnumerable<string> lines)
        {
            foreach (var line in lines ?? Enumerable.Empty<string>())
            {
                if (line == null || line.IndexOf("\"marker\"", StringComparison.Ordinal) < 0 || line.IndexOf("\"world\"", StringComparison.Ordinal) < 0) continue;
                if (MiniJson.Parse(line) is Dictionary<string, object> o && MiniJson.Str(o, "world", null) is string w) return w;
            }
            return null;
        }

        /// <summary>The first world id a marker names for the world <paramref name="name"/>, or null (only marker lines with an id are parsed).</summary>
        static string FirstUid(IEnumerable<string> lines, string name)
        {
            foreach (var line in lines ?? Enumerable.Empty<string>())
            {
                if (line == null || line.IndexOf("\"worldUid\"", StringComparison.Ordinal) < 0 || line.IndexOf("\"marker\"", StringComparison.Ordinal) < 0) continue;
                if (MiniJson.Parse(line) is Dictionary<string, object> o && MiniJson.Str(o, "world", null) == name && MiniJson.Str(o, "worldUid", null) is string u) return u;
            }
            return null;
        }

        void Apply(Dictionary<string, object> o)
        {
            var t = Date(MiniJson.Str(o, "t")) ?? DateTime.MinValue;
            var who = MiniJson.Num(o, "playerId") != 0 ? MiniJson.Str(o, "playerKey") : null;
            Cargo(MiniJson.Str(o, "containerId"), MiniJson.Str(o, "kind"), MiniJson.Str(o, "action"), MiniJson.Str(o, "via"), who,
                  MiniJson.Str(o, "item"), (int)MiniJson.Num(o, "count"), MiniJson.Num(o, "x"), MiniJson.Num(o, "z"), t);
        }

        // ---------- helpers ----------

        static void WriteDict(StringBuilder b, IEnumerable<KeyValuePair<string, double>> d)
        {
            b.Append('{'); bool first = true;
            foreach (var kv in d) { if (!(kv.Value >= 0.0005)) continue; if (!first) b.Append(','); first = false; b.Append(Json.Q(kv.Key)).Append(':').Append(kv.Value.ToString("0.###", CultureInfo.InvariantCulture)); }
            b.Append('}');
        }

        static string Iso(DateTime t) => t.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        static DateTime? Date(string s) => !string.IsNullOrEmpty(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t) ? t.ToUniversalTime() : (DateTime?)null;
    }
}
