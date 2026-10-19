using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Hearthwoven
{
    /// <summary>
    /// 0.8.2 (Joost, right after restarting the game to install a build: Battle > Damage in 3 h said "Nothing in the last 3 hours" though he had
    /// fought all evening, and Everyone showed "You: no damage dealt"): every connection starts its minute logs from zero (Plugin.NewConnection:
    /// the event log, Deeds > Recent's DeedLog, the armour minutes), so 10 min .. 3 h saw only the current connection. This keeps their last
    /// minutes on this PC beside the local totals, and the next connection in the same world puts them under its own: the short windows reach
    /// back across a restart or a reconnect. Session still starts at the new connection (every kept minute ends before it), and the day history
    /// and the session tallies never see them (the session that made them recorded them).
    ///
    /// Where: local/&lt;playerId&gt;-&lt;name&gt;.minutes.json, a companion of the character's totals file (as .fellows.json and .panel.json),
    /// written whole (AtomicFile, with one .bak) at every local save. Only for the same world (FellowCache.ScopeKey: its name and id) and the
    /// same character (player id and name): a file of another world or character is never joined, and the next save replaces it. Bounded: only
    /// the minutes the longest short window can still reach (From: KeepMinutes back, from a whole ten minutes), never a folded bucket, and at
    /// most EventLog.MaxBuckets hit keys. The shared copy carries the kept log as "logBefore" (SharedJson), beside this connection's measuredLog,
    /// which stays the connection's alone (the server and the report sum measuredLog per session). Pure C# with files: tested without the game
    /// (test/KeptMinutesTests.cs).
    /// </summary>
    public class KeptMinutes
    {
        public const int Version = 1;
        /// <summary>How far back minutes are kept: the 3 h window and a margin, as far as the logs keep one-minute buckets (EventLog.FoldAfterMinutes).</summary>
        public const int KeepMinutes = EventLog.FoldAfterMinutes;
        /// <summary>The shared copy's key of the kept log: the log's fields (EventLog.WriteFields) and "from".</summary>
        public const string SharedKey = "logBefore";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string World = "", PlayerId = "", Name = "";
        /// <summary>The earliest minute the parts may hold (From when taken or loaded).</summary>
        public DateTime FromUtc;
        public EventLog Log = new EventLog();
        /// <summary>Deeds > Recent's buckets (DeedLog keys) and the armour minutes (ArmourLog keys), as kept.</summary>
        public readonly Dictionary<string, float> Deeds = new Dictionary<string, float>();
        public readonly ArmourLog Armour = new ArmourLog();
        /// <summary>0.9: the land you were in per minute (DeedLog.Lands keys), as kept; this PC only, like the deeds (never in SharedJson).</summary>
        public readonly Dictionary<string, float> Lands = new Dictionary<string, float>();

        public bool Empty => Log.Damage.Count == 0 && Log.Hits.Count == 0 && Log.Guard.Count == 0 && Log.Armor.Count == 0 && Log.Swing.Count == 0 && Log.Weapons.Count == 0 && Log.Deaths.Count == 0 && Deeds.Count == 0 && Armour.Damage.Count == 0 && Armour.Hits.Count == 0 && Lands.Count == 0;

        /// <summary>The file beside a character's totals file (LocalTotals.PathFor): "&lt;id&gt;-&lt;name&gt;.minutes.json".</summary>
        public static string PathFor(string totalsPath) =>
            (totalsPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? totalsPath.Substring(0, totalsPath.Length - 5) : totalsPath) + ".minutes.json";

        /// <summary>The earliest minute worth keeping at <paramref name="nowUtc"/>: KeepMinutes back, up to the next whole ten minutes, so a folded
        /// ten-minute bucket is never cut in two (fellows put their own copies of older minutes under it, FellowTrail.Joined) and every minute the
        /// 3 h window reaches is still in.</summary>
        public static DateTime From(DateTime nowUtc)
        {
            var t = nowUtc.AddMinutes(-KeepMinutes);
            var ten = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute / 10 * 10, 0, DateTimeKind.Utc);
            return ten < t ? ten.AddMinutes(10) : ten;
        }

        /// <summary>What to keep at <paramref name="nowUtc"/>: the minutes from From(nowUtc) on of <paramref name="log"/> (this session's with the
        /// kept ones joined under it), of <paramref name="armour"/> and of the deed buckets (this session's and the kept ones). Copies: later play
        /// changes nothing taken. Past EventLog.MaxBuckets hit keys the oldest ten minutes go first.</summary>
        public static KeptMinutes Take(DateTime nowUtc, EventLog log, ArmourLog armour, params IDictionary<string, float>[] deeds)
        {
            var from = From(nowUtc);
            var kept = log != null ? log.Between(from, DateTime.MaxValue) : new EventLog();
            while (kept.Hits.Count > EventLog.MaxBuckets && from < nowUtc) { from = from.AddMinutes(10); kept = kept.Between(from, DateTime.MaxValue); }
            var k = new KeptMinutes { FromUtc = from, Log = kept };
            var lo = EventLog.Bucket(from);
            foreach (var d in deeds) if (d != null) EventLog.KeysBetween(d, k.Deeds, lo, null);
            if (armour != null) { EventLog.KeysBetween(armour.Damage, k.Armour.Damage, lo, null); EventLog.KeysBetween(armour.Hits, k.Armour.Hits, lo, null); }
            return k;
        }

        /// <summary>0.9: keeps the land minutes (DeedLog.Lands, EarlierLands) from FromUtc on, as Take keeps the deed buckets.</summary>
        public KeptMinutes WithLands(params IDictionary<string, float>[] lands)
        {
            var lo = EventLog.Bucket(FromUtc);
            foreach (var d in lands) if (d != null) EventLog.KeysBetween(d, Lands, lo, null);
            return this;
        }

        public string ToJson(DateTime savedUtc)
        {
            var j = new Json().Open().Num("version", Version).Str("world", World).Str("playerId", PlayerId).Str("name", Name)
                              .Str("saved", savedUtc.ToString("o", Inv)).Str("from", FromUtc.ToString("o", Inv));
            j.Key("log").Open(); Log.WriteFields(j); j.Close();
            j.Dict("deeds", Deeds).Dict("armorDamage", Armour.Damage).Dict("armorHits", Armour.Hits);
            if (Lands.Count > 0) j.Dict("lands", Lands);   // 0.9: an added key; a reader before 0.9 leaves it
            return j.Close().ToString();
        }

        /// <summary>Reads what ToJson wrote; null when it is not a minutes file of a version this mod reads.</summary>
        public static KeptMinutes FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root) || MiniJson.Num(root, "version") < 1 || MiniJson.Num(root, "version") > Version) return null;
            var k = new KeptMinutes { World = MiniJson.Str(root, "world"), PlayerId = MiniJson.Str(root, "playerId"), Name = MiniJson.Str(root, "name"), FromUtc = Time(MiniJson.Str(root, "from")) };
            k.Log.ReadFrom(MiniJson.Obj(root, "log"));
            MiniJson.Into(MiniJson.Obj(root, "deeds"), k.Deeds);
            MiniJson.Into(MiniJson.Obj(root, "armorDamage"), k.Armour.Damage); MiniJson.Into(MiniJson.Obj(root, "armorHits"), k.Armour.Hits);
            MiniJson.Into(MiniJson.Obj(root, "lands"), k.Lands);   // 0.9; none in a file from before
            return k;
        }

        static DateTime Time(string s) => DateTime.TryParse(s, Inv, DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : DateTime.MinValue;

        /// <summary>Written whole with one backup (AtomicFile); nothing while Dev.SampleData is on.</summary>
        public void Save(string path, DateTime savedUtc)
        {
            if (Panel.SampleMode.Quiet("recent minutes")) return;
            AtomicFile.Write(path, ToJson(savedUtc));
        }

        /// <summary>
        /// The minutes kept at <paramref name="path"/> for <paramref name="world"/> and this character, as a new connection that began at
        /// <paramref name="connectedUtc"/> joins them: from From(<paramref name="nowUtc"/>) on, and only what ends at or before connectedUtc (a
        /// reconnect within the same minute leaves that minute out, so Session never holds a kept one). null: no file, nothing left in the range,
        /// another world or character (the file stays; the next save replaces it), or a file this version cannot read (<paramref name="problem"/>
        /// says so; its .bak is tried first).
        /// </summary>
        public static KeptMinutes Load(string path, string world, long playerId, string name, DateTime nowUtc, DateTime connectedUtc, out string problem)
        {
            problem = null;
            if (!File.Exists(path) && !File.Exists(path + AtomicFile.BackupSuffix)) return null;
            KeptMinutes k;
            try { var text = AtomicFile.ReadWithBackup(path, t => FromJson(t) != null, out _); k = text == null ? null : FromJson(text); }
            catch (Exception e) { problem = "recent minutes " + path + " could not be read: " + e.Message; return null; }
            if (k == null) { problem = "recent minutes " + path + " are unreadable: the short windows start at this connection"; return null; }
            if (k.World != (world ?? "") || k.PlayerId != playerId.ToString(Inv) || k.Name != (name ?? "")) return null;
            var from = From(nowUtc);
            if (from < k.FromUtc) from = k.FromUtc;   // the clock went back: never claim minutes the file does not hold
            var kept = new KeptMinutes { World = k.World, PlayerId = k.PlayerId, Name = k.Name, FromUtc = from, Log = k.Log.Between(from, connectedUtc) };
            string lo = EventLog.Bucket(from), hi = EventLog.Bucket(connectedUtc);
            EventLog.KeysBetween(k.Deeds, kept.Deeds, lo, hi);
            EventLog.KeysBetween(k.Armour.Damage, kept.Armour.Damage, lo, hi); EventLog.KeysBetween(k.Armour.Hits, kept.Armour.Hits, lo, hi);
            EventLog.KeysBetween(k.Lands, kept.Lands, lo, hi);   // 0.9
            return kept.Empty ? null : kept;
        }

        /// <summary>0.8.5: the world of the minutes at <paramref name="path"/>, which every local save writes: the world of your last save before this
        /// connection, whatever world you play now. Null: no file, or none this version reads.</summary>
        public static string WorldOf(string path)
        {
            if (path == null || (!File.Exists(path) && !File.Exists(path + AtomicFile.BackupSuffix))) return null;
            try { KeptMinutes k = null; AtomicFile.ReadWithBackup(path, t => (k = FromJson(t)) != null, out _); return string.IsNullOrEmpty(k?.World) ? null : k.World; }
            catch (Exception) { return null; }
        }

        /// <summary>The kept log as your shared copy carries it (SharedKey): "from" and the log's fields. Fellows put it under your connection's
        /// log (FellowTrail.Joined); the deeds and armour minutes stay on this PC, as this session's do.</summary>
        public string SharedJson()
        {
            var j = new Json().Open().Str("from", FromUtc.ToString("o", Inv));
            Log.WriteFields(j, swing: false);   // 0.8.6: the fighting time per kind and minute stays on your PC (ToJson keeps it); the weapons per minute are shared on purpose
            return j.Close().ToString();
        }
    }
}
