using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Hearthwoven
{
    /// <summary>
    /// "Since install" on the player's own PC (INTEGRITY P7). What Hearthwoven counts on this PC starts from zero every
    /// session (one connection, P3); this keeps the running total in BepInEx/Hearthwoven/local/&lt;playerId&gt;.json, so the
    /// panel shows the previous sessions plus this one.
    ///
    /// Per character, across every world and server: the same scope as the character's own counters the numbers sit
    /// beside (skills, food, gear and trader all travel with the character). Only folded counters, one number per kind,
    /// never raw events: the file stays bounded however long someone plays. The per-hit event log (Battle's time windows)
    /// stays per session and is not carried over.
    ///
    /// Nothing counted twice: the file holds the earlier sessions folded together ("previous") plus the latest saved
    /// session under its id ("lastSession"). Saving that session again replaces it; a new session id first folds the old
    /// one into "previous". A crash loses at most what was counted since the last save (the send interval), never more.
    /// Written atomically with one backup (AtomicFile: temp file flushed to disk, File.Replace, the previous save kept as
    /// .bak); a main file that is missing or unreadable falls back to the .bak. Pure C# (no Unity calls), unit-tested.
    ///
    /// One file per character (RESILIENCE-06 item 4): local/&lt;playerId&gt;-&lt;name&gt;.json (PathFor). The player id
    /// alone is not enough: a copied .fch keeps the id, and two characters writing one file would mix their counts.
    /// Files from before 0.6.1 are keyed by the id only (local/&lt;playerId&gt;.json); LoadFor migrates such a file to the
    /// character loading it only when that character is the one plausible owner (see LoadFor), and never merges two.
    ///
    /// Format: {"version":1,"schema":2,"playerId":..,"name":"..","saved":"..","totals":{"sessions":n,"previous":{..},
    /// "lastSession":{"id":"..",..}},"baseline":{..},"exactAtBaseline":{..},"baselineAt":{..},"feats":{..}}.
    /// Versioning rule: "version" is the breaking-format number. A reader refuses a file with a higher one (it is left alone,
    /// never saved over: Load returns null), so bump it only when an older reader would misread the file. Additive changes
    /// keep "version", add NEW top-level keys (never new fields inside an object an older reader rewrites) and bump
    /// "schema" (informational: which writer). Every reader keeps the top-level keys it does not know and writes them back
    /// unchanged (Extra), so an older Hearthwoven never drops what a newer one stored; a newer one reads every older file.
    /// (0.6.0 and earlier drop unknown keys on save: their files carry no feats/history worth protecting from them.)
    ///
    /// Schema 3 (HISTORY-06.md): top-level "history", the day history (DayHistory): one compact row per play day of what that day
    /// added, filled by Record from the difference each save makes, folded to weeks and months when old. A file without it starts
    /// its history at the next save.
    /// </summary>
    public class LocalTotals
    {
        public const int Version = 1;      // breaking-format number: a reader refuses a higher one
        public const int Schema = 3;       // additive revision of this writer (2: name, schema, unknown keys kept; 3: history)
        public long PlayerId;
        /// <summary>The character's name as the file was last saved for (JSON "name"); "" in a file from before 0.6.1.</summary>
        public string Name = "";
        /// <summary>When the file was saved (JSON "saved"); DateTime.MinValue = not known.</summary>
        public DateTime SavedUtc = DateTime.MinValue;
        /// <summary>Top-level keys this version does not know (from a newer Hearthwoven), written back as they were read.</summary>
        public readonly Dictionary<string, object> Extra = new Dictionary<string, object>();
        static readonly HashSet<string> KnownKeys = new HashSet<string>
            { "version", "schema", "playerId", "name", "saved", "firstRun", "biomeFrom", "totals", "baseline", "exactAtBaseline", "baselineAt", "feats", "history" };
        public int Sessions;                                     // sessions folded into Previous
        /// <summary>When Hearthwoven first counted for this character on this PC (the zones' "since you installed Hearthwoven on
        /// 8 Oct"): set when the file is first made; a file from before this field takes the file's own creation time once.
        /// DateTime.MinValue = not known. JSON: top-level "firstRun".</summary>
        public DateTime FirstRunUtc = DateTime.MinValue;
        public DamageTally PreviousDamage = new DamageTally();   // every earlier session, folded
        public SessionEvents PreviousEvents = new SessionEvents();
        public BiomeTally PreviousBiome = new BiomeTally();      // damage and deaths per biome, every earlier session folded (Battle's All overview)
        public BiomeTally LastBiome = new BiomeTally();          // and the latest saved session's
        /// <summary>When per-biome counting began for this character. A file from before it has earlier sessions without a biome: the
        /// panel says per biome is counted from this day. Set to the first load of a version that has it (Load); a new file: its first run.
        /// DateTime.MinValue = not known. JSON: top-level "biomeFrom".</summary>
        public DateTime BiomeFromUtc = DateTime.MinValue;
        public string LastSession = "";                          // the latest saved session's id ("" = none yet)
        public DamageTally LastDamage = new DamageTally();       // and its counters as last saved
        public SessionEvents LastEvents = new SessionEvents();
        /// <summary>
        /// The game's own counters as they stood when Hearthwoven first ran for this character, per kind ("pickedUp": the
        /// game's itemsPickedUp, token -> amount). Taken once and never updated (TakeBaseline): the panel draws it faded
        /// under what Hearthwoven counted exactly since, and never adds the game counter's later growth. An install from
        /// before this field takes it on the first load of the version that has it. JSON: top-level "baseline".
        /// </summary>
        public Dictionary<string, Dictionary<string, float>> Baseline = new Dictionary<string, Dictionary<string, float>>();
        /// <summary>
        /// What Hearthwoven had already counted exactly (since install) of that kind at the moment the baseline was taken,
        /// per kind and token. An install from before the baseline counted pickups the game counter (the baseline) partly
        /// holds too: the panel's solid part is exact minus this, so nothing counts twice. A fresh install: empty (0).
        /// JSON: top-level "exactAtBaseline".
        /// </summary>
        public Dictionary<string, Dictionary<string, float>> ExactAtBaseline = new Dictionary<string, Dictionary<string, float>>();
        /// <summary>
        /// When each baseline kind was taken (UTC). The Deeds zones say "counting since 9 Oct" for a counter that began later
        /// than the install (an older install, the first run of a version that baselines more counters); a kind without a
        /// date (taken before this field) counts from the install. JSON: top-level "baselineAt", kind -> ISO time.
        /// </summary>
        public Dictionary<string, DateTime> BaselineAt = new Dictionary<string, DateTime>();
        /// <summary>The Feats system's own record on this PC (FeatsLedger: earned tiers with their moments, the hook counters).
        /// JSON: top-level "feats", left out while empty; an older file has none and reads as empty.</summary>
        public FeatsLedger Feats = new FeatsLedger();
        /// <summary>The day history (DayHistory, HISTORY-06.md): what each play day added, for the panel's Today, 7 days and 30 days.
        /// JSON: top-level "history", left out while it has no row; an older file has none and starts it at the next save.</summary>
        public DayHistory History = new DayHistory();

        /// <summary>The baseline kind of the game's battle counters (Plugin.LoadLocal), tokens = the game's stat names, the
        /// same keys as SessionEvents.Battle.</summary>
        public const string BattleKind = "battle";
        public static readonly string[] BattleTokens = { "EnemyHits", "PlayerHits", "Deaths" };

        /// <summary>
        /// The baseline kind of the Deeds counters the game keeps complete, tokens = the game's stat names (crafting per kind,
        /// upgrades, fishing, harvest, taming care). Their "since install" is the game's counter now minus this baseline (Since).
        /// </summary>
        public const string StatsKind = "stats";
        public static readonly string[] StatsTokens =
        {
            "CraftWeapon", "CraftArmor", "CraftTool", "CraftTrinket", "Upgrades",
            "FishHooked", "FishCaught", "FishLost", "FishCaughtTier1", "FishCaughtTier2", "FishCaughtTier3", "FishCaughtTier4", "FishCaughtTier5", "FishCaughtTier6",
            "HarvestCrop", "HarvestBerry", "HarvestMushroom", "HarvestVine", "BeesHarvested", "SapHarvested",
            "TamedPetting", "TamedCommand",
        };
        /// <summary>The baseline kind of the game's picked-plant counter (m_pickableStats: per crop prefab, per fish name token).</summary>
        public const string PickablesKind = "pickables";
        /// <summary>The older baseline kinds, by name: the game's placed-pieces and item-craft counters (token -> count).</summary>
        public const string PlacedKind = "piecesPlaced", CraftedKind = "itemsCrafted";

        /// <summary>
        /// Since install of a counter the game keeps complete (no pickup gap, no owner trap): the game's counter now
        /// (<paramref name="gameNow"/>) minus what it stood at when Hearthwoven first ran (the baseline of
        /// <paramref name="kind"/>), never below 0. null = no baseline of that kind (a fellow's copy, totals not loaded, a
        /// kind not taken yet): the caller shows no since-install number then. A token the baseline does not hold stood at 0.
        /// The exactAtBaseline guard is for what Hearthwoven counts itself (Layers); a game counter needs none: whatever
        /// Hearthwoven counted already sits in the game's own number.
        /// </summary>
        public static double? Since(IDictionary<string, Dictionary<string, float>> baseline, string kind, string token, double gameNow)
        {
            if (baseline == null || kind == null || token == null || !baseline.TryGetValue(kind, out var stored) || stored == null) return null;
            stored.TryGetValue(token, out var before);
            return Math.Max(0, gameNow - before);
        }

        /// <summary>
        /// One count in two layers (K1): before = the game's counter when Hearthwoven first ran (the baseline of
        /// <paramref name="kind"/>, drawn faded), exact = what Hearthwoven counted since (<paramref name="exactNow"/>, since
        /// install) minus what it had already counted when the baseline was taken (an older install: the baseline holds that
        /// part too). The game counter's growth after the baseline is never added. No baseline (a fellow's copy, totals not
        /// loaded): before = the game counter now minus the exact count, at least 0. Either way nothing counts twice.
        /// </summary>
        public static (double before, double exact) Layers(IDictionary<string, Dictionary<string, float>> baseline, IDictionary<string, Dictionary<string, float>> exactAtBaseline,
                                                           string kind, string token, double exactNow, double gameNow)
        {
            exactNow = Math.Max(0, exactNow);
            if (baseline == null || kind == null || token == null || !baseline.TryGetValue(kind, out var stored) || stored == null)
                return (Math.Max(0, gameNow - exactNow), exactNow);
            float already = 0;
            if (exactAtBaseline != null && exactAtBaseline.TryGetValue(kind, out var at) && at != null) at.TryGetValue(token, out already);
            stored.TryGetValue(token, out var before);
            return (before, Math.Max(0, exactNow - already));
        }

        static Dictionary<string, float> Copy(IDictionary<string, float> d)
        {
            var copy = new Dictionary<string, float>();
            foreach (var kv in d ?? new Dictionary<string, float>()) if (kv.Value > 0) copy[kv.Key] = kv.Value;
            return copy;
        }

        /// <summary>Stores a copy of <paramref name="counter"/> as the baseline of <paramref name="kind"/>, once, with what
        /// Hearthwoven had counted exactly of it by then (<paramref name="exactNow"/>, since install; null = nothing yet): true
        /// when it was stored just now (save then), false when that kind already has one (or there is no counter).</summary>
        public bool TakeBaseline(string kind, IDictionary<string, float> counter, IDictionary<string, float> exactNow = null, DateTime? atUtc = null)
        {
            if (string.IsNullOrEmpty(kind) || counter == null || Baseline.ContainsKey(kind)) return false;
            Baseline[kind] = Copy(counter);
            ExactAtBaseline[kind] = Copy(exactNow);
            BaselineAt[kind] = (atUtc ?? DateTime.UtcNow).ToUniversalTime();
            return true;
        }

        /// <summary>Everything counted before the session <paramref name="current"/>: the folded sessions, plus the latest saved
        /// one unless that is <paramref name="current"/> itself (a respawn in the same session must not add it twice).</summary>
        public SessionEvents EventsBefore(string current) => SessionEvents.Sum(PreviousEvents, LastSession == current ? null : LastEvents);
        public DamageTally DamageBefore(string current) => DamageTally.Sum(PreviousDamage, LastSession == current ? null : LastDamage);

        /// <summary>
        /// What the running session counted since the last save, as a row of today (the panel adds it to the day history, so Today never
        /// lags This session by a save interval). Nothing is changed. <paramref name="biome"/> is the log's own per-biome fold
        /// (EventLog.Biome, kept up to date as hits come in); the game counters' growth beyond the mark, the mark left where it is.
        /// </summary>
        public DayHistory.Row Pending(string session, DamageTally damage, SessionEvents events, BiomeTally biome, IDictionary<string, float> gameNow, DateTime localNow)
        {
            var same = !string.IsNullOrEmpty(session) && session == LastSession;
            var row = new DayHistory.Row { Period = DayHistory.DayKey(localNow.Date) };
            row.Events = SessionEvents.Minus(events, same ? LastEvents : null);
            row.Damage = DamageTally.Minus(damage, same ? LastDamage : null);
            row.Biome = BiomeTally.Minus(biome, same ? LastBiome : null);
            foreach (var kv in History.GameGrowth(gameNow, false)) row.Game[kv.Key] = kv.Value;
            return row;
        }
        public BiomeTally BiomeBefore(string current) => BiomeTally.Sum(PreviousBiome, LastSession == current ? null : LastBiome);

        /// <summary>
        /// Records a session's absolute counters as they are now. The same id as the latest saved session replaces it; a new
        /// id first folds the latest saved session into the previous ones. Copies, so later play changes nothing recorded.
        /// </summary>
        public void Record(string session, DamageTally damage, SessionEvents events, EventLog log = null, IDictionary<string, float> gameNow = null, DateTime? localNow = null)
        {
            if (string.IsNullOrEmpty(session)) return;
            // the day history first (HISTORY-06.md): what this save adds beyond what was recorded of the same session (all of it for a
            // new session) goes to today's row, and the game counters' growth since the last save with it; nothing counts twice
            if (localNow.HasValue)
            {
                var same = session == LastSession;
                History.Add(localNow.Value, SessionEvents.Minus(events, same ? LastEvents : null), DamageTally.Minus(damage, same ? LastDamage : null),
                            BiomeTally.Minus(BiomeTally.FromLog(log), same ? LastBiome : null), gameNow);
            }
            if (session != LastSession)
            {
                if (LastSession.Length > 0) { PreviousDamage.AddAll(LastDamage); PreviousEvents.AddAll(LastEvents); PreviousBiome.AddAll(LastBiome); Sessions++; }
                LastSession = session;
            }
            LastDamage = DamageTally.Sum(damage);
            LastEvents = SessionEvents.Sum(events);
            LastBiome = BiomeTally.FromLog(log);
        }

        public string ToJson(DateTime savedUtc)
        {
            var j = new Json().Open().Num("version", Version).Num("schema", Schema).Num("playerId", PlayerId);
            if (!string.IsNullOrEmpty(Name)) j.Str("name", Name);
            j.Str("saved", savedUtc.ToString("o", CultureInfo.InvariantCulture));
            if (FirstRunUtc > DateTime.MinValue) j.Str("firstRun", FirstRunUtc.ToString("o", CultureInfo.InvariantCulture));
            if (BiomeFromUtc > DateTime.MinValue) j.Str("biomeFrom", BiomeFromUtc.ToString("o", CultureInfo.InvariantCulture));
            j.Key("totals").Open().Num("sessions", Sessions);
            j.Key("previous").Open(); PreviousDamage.WriteTo(j, "damage"); PreviousEvents.WriteTo(j, "measured"); PreviousBiome.WriteTo(j, "biome"); j.Close();
            j.Key("lastSession").Open().Str("id", LastSession); LastDamage.WriteTo(j, "damage"); LastEvents.WriteTo(j, "measured"); LastBiome.WriteTo(j, "biome"); j.Close();
            j.Close();
            Kinds(j, "baseline", Baseline);
            Kinds(j, "exactAtBaseline", ExactAtBaseline);
            if (BaselineAt.Count > 0)
            {
                j.Key("baselineAt").Open();
                foreach (var kv in BaselineAt) j.Str(kv.Key, kv.Value.ToString("o", CultureInfo.InvariantCulture));
                j.Close();
            }
            if (!Feats.IsEmpty) Feats.WriteTo(j);
            if (History.Rows.Count > 0 || History.GameMark.Count > 0) History.WriteTo(j);
            foreach (var kv in Extra) if (!KnownKeys.Contains(kv.Key)) j.Raw(kv.Key, MiniJson.Write(kv.Value));   // a newer version's data, kept as read
            return j.Close().ToString();
        }

        static void Kinds(Json j, string key, Dictionary<string, Dictionary<string, float>> kinds)
        {
            j.Key(key).Open();
            foreach (var kind in kinds)
            {
                j.Key(kind.Key).Open();
                foreach (var kv in kind.Value) j.Num(kv.Key, kv.Value);
                j.Close();
            }
            j.Close();
        }
        static void ReadKinds(Dictionary<string, object> o, Dictionary<string, Dictionary<string, float>> into)
        {
            if (o == null) return;
            foreach (var kind in o)
            {
                if (!(kind.Value is Dictionary<string, object> counts)) continue;
                var d = new Dictionary<string, float>(); MiniJson.Into(counts, d); into[kind.Key] = d;
            }
        }

        /// <summary>Reads what ToJson wrote; null when it is not a totals file of a version this mod can read.</summary>
        public static LocalTotals FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root) || !root.ContainsKey("version")) return null;
            var version = (int)MiniJson.Num(root, "version");
            if (version < 1 || version > Version) return null;
            var totals = MiniJson.Obj(root, "totals");
            if (totals == null) return null;
            var previous = MiniJson.Obj(totals, "previous"); var last = MiniJson.Obj(totals, "lastSession");
            var t = new LocalTotals { PlayerId = (long)MiniJson.Num(root, "playerId"), Name = MiniJson.Str(root, "name"), SavedUtc = FirstRunOf(MiniJson.Str(root, "saved")), Sessions = (int)MiniJson.Num(totals, "sessions"), LastSession = MiniJson.Str(last, "id"), FirstRunUtc = FirstRunOf(MiniJson.Str(root, "firstRun")), BiomeFromUtc = FirstRunOf(MiniJson.Str(root, "biomeFrom")) };
            t.PreviousDamage.ReadFrom(MiniJson.Obj(previous, "damage")); t.PreviousEvents.ReadFrom(MiniJson.Obj(previous, "measured"));
            t.LastDamage.ReadFrom(MiniJson.Obj(last, "damage")); t.LastEvents.ReadFrom(MiniJson.Obj(last, "measured"));
            t.PreviousBiome.ReadFrom(MiniJson.Obj(previous, "biome")); t.LastBiome.ReadFrom(MiniJson.Obj(last, "biome"));
            ReadKinds(MiniJson.Obj(root, "baseline"), t.Baseline);
            ReadKinds(MiniJson.Obj(root, "exactAtBaseline"), t.ExactAtBaseline);
            var at = MiniJson.Obj(root, "baselineAt");
            if (at != null) foreach (var kv in at) { var when = FirstRunOf(kv.Value as string); if (when > DateTime.MinValue) t.BaselineAt[kv.Key] = when; }
            t.Feats = FeatsLedger.ReadFrom(MiniJson.Obj(root, "feats"));
            t.History = DayHistory.ReadFrom(MiniJson.Obj(root, "history"));
            foreach (var kv in root) if (!KnownKeys.Contains(kv.Key)) t.Extra[kv.Key] = kv.Value;
            return t;
        }

        /// <summary>The breaking-format number of a totals file that is newer than this reader, or 0 (not newer, not JSON).</summary>
        static double NewerVersion(string json) =>
            MiniJson.Parse(json) is Dictionary<string, object> root && MiniJson.Num(root, "version") > Version ? MiniJson.Num(root, "version") : 0;
        // a text Load may use: totals this version reads, or a newer file (which Load then refuses rather than skipping to the backup)
        static bool Usable(string json) => FromJson(json) != null || NewerVersion(json) > 0;

        static DateTime FirstRunOf(string s) =>
            DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : DateTime.MinValue;
        /// <summary>A file that is not there yet: counting begins now, for the character and for the biomes at the same instant (one clock read: two reads could differ by a tick).</summary>
        static LocalTotals Fresh(long playerId) { var now = DateTime.UtcNow; return new LocalTotals { PlayerId = playerId, FirstRunUtc = now, BiomeFromUtc = now }; }

        // a file from before FirstRunUtc: its creation time is when this character's counting began (kept through File.Replace)
        static DateTime CreatedUtc(string path)
        {
            try { var c = File.GetCreationTimeUtc(path); return c.Year >= 2025 && c <= DateTime.UtcNow ? c : DateTime.MinValue; }
            catch { return DateTime.MinValue; }
        }

        /// <summary>The file of a character from before 0.6.1, keyed by the player id only (LoadFor migrates it). Low-level tests use it as any path.</summary>
        public static string PathFor(string hearthwovenDir, long playerId) => Path.Combine(Path.Combine(hearthwovenDir, "local"), playerId.ToString(CultureInfo.InvariantCulture) + ".json");

        /// <summary>
        /// The file of one character: local/&lt;playerId&gt;-&lt;name&gt;.json. The name keeps ASCII letters, digits, '-' and '_'
        /// (at most 40); anything else becomes '_', and then a short hash of the exact name is added ("Bj_rn~1a2b3c4d"), so two
        /// names that differ only in such characters never share a file. <paramref name="forceHash"/> adds the hash always:
        /// the fallback when the plain file holds another name (names equal but for case, on a case-insensitive disk).
        /// </summary>
        public static string PathFor(string hearthwovenDir, long playerId, string name, bool forceHash = false) =>
            Path.Combine(Path.Combine(hearthwovenDir, "local"), playerId.ToString(CultureInfo.InvariantCulture) + "-" + SafeName(name, forceHash) + ".json");

        public static string SafeName(string name, bool forceHash = false)
        {
            name = name ?? "";
            var b = new StringBuilder(); var changed = name.Length == 0 || name.Length > 40;
            foreach (var c in name.Length > 40 ? name.Substring(0, 40) : name)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_') b.Append(c);
                else { b.Append('_'); changed = true; }
            }
            if (b.Length == 0) b.Append("unnamed");
            if (changed || forceHash)
            {
                uint h = 2166136261;   // FNV-1a over the exact name: the same on every runtime (string.GetHashCode is not)
                foreach (var c in name) { h ^= c; h *= 16777619; }
                b.Append('~').Append(h.ToString("x8", CultureInfo.InvariantCulture));
            }
            return b.ToString();
        }

        /// <summary>
        /// True when this file can have been made by a character whose game counters are now <paramref name="gameNow"/>
        /// (kind -> token -> count, the kinds TakeBaseline stores): the game's counters only grow, so every baseline the file
        /// took must be at most what that character's counter holds now. A kind <paramref name="gameNow"/> lacks is no evidence.
        /// </summary>
        public bool PlausibleFor(IDictionary<string, IDictionary<string, float>> gameNow)
        {
            if (gameNow == null) return true;
            foreach (var kind in Baseline)
            {
                if (!gameNow.TryGetValue(kind.Key, out var now) || now == null) continue;
                foreach (var kv in kind.Value)
                {
                    now.TryGetValue(kv.Key, out var n);
                    if (kv.Value > n + 0.5f) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The totals of the character <paramref name="name"/> (id <paramref name="playerId"/>) under
        /// <paramref name="hearthwovenDir"/>; <paramref name="path"/> = the file to save to. The rule (RESILIENCE-06 item 4):
        /// 1. The character's own file (PathFor with the name) wins; one that holds another name (a case-only clash) sends
        ///    this character to the hash-qualified file instead.
        /// 2. Else a file from before 0.6.1 (local/&lt;playerId&gt;.json, no name in it) is adopted only when exactly one
        ///    character can have made it: this character is plausible (PlausibleFor its counters <paramref name="gameNow"/>)
        ///    and none of the OTHER characters on this PC with the same id is (<paramref name="othersWithSameId"/>: their
        ///    counters; asked only here; null = could not be listed, counted as none). Adopted: saved as this character's file
        ///    and the old file renamed to "&lt;id&gt;.json.migrated-&lt;name&gt;" (kept, never deleted), so no second character can
        ///    adopt it too. Not adopted: left exactly as it is; this character counts from zero in its own file, and
        ///    <paramref name="problem"/> says why. Two characters' counts are never merged.
        /// Null (do not save) as Load: a newer format, or a file that cannot be opened.
        /// </summary>
        public static LocalTotals LoadFor(string hearthwovenDir, long playerId, string name, IDictionary<string, IDictionary<string, float>> gameNow,
                                          Func<IEnumerable<IDictionary<string, IDictionary<string, float>>>> othersWithSameId, out string path, out string problem)
        {
            name = name ?? "";
            path = PathFor(hearthwovenDir, playerId, name);
            if (Exists(path))
            {
                var own = Load(path, playerId, out problem);
                if (own == null || own.Name.Length == 0 || own.Name == name) { if (own != null) own.Name = name; return own; }
                path = PathFor(hearthwovenDir, playerId, name, true);   // the plain file is another character's (case-only clash)
                var hashed = Load(path, playerId, out var p2); problem = p2 ?? problem;
                if (hashed != null) hashed.Name = name;
                return hashed;
            }
            problem = null;
            var legacy = PathFor(hearthwovenDir, playerId);
            if (!File.Exists(legacy)) { var fresh = Fresh(playerId); fresh.Name = name; return fresh; }

            string text;
            try { text = File.ReadAllText(legacy, Encoding.UTF8); }
            catch (Exception e) { problem = "local totals " + legacy + " (from before 0.6.1) could not be opened; this character counts from zero in " + path + ": " + e.Message; return Named(Fresh(playerId), name); }
            var old = FromJson(text);
            if (old == null)
            {
                var v = NewerVersion(text);
                problem = "local totals " + legacy + (v > 0 ? " are from a newer Hearthwoven (version " + v + ")" : " (from before 0.6.1) are unreadable") +
                          "; left as they are, this character counts from zero in " + path;
                return Named(Fresh(playerId), name);
            }
            int plausible = old.PlausibleFor(gameNow) ? 1 : 0, others = 0;
            IEnumerable<IDictionary<string, IDictionary<string, float>>> list = null;
            try { list = othersWithSameId?.Invoke(); } catch { list = null; }
            if (list != null) foreach (var other in list) if (old.PlausibleFor(other)) others++;
            if (plausible == 1 && others == 0)
            {
                old.PlayerId = playerId; old.Name = name;
                if (old.FirstRunUtc == DateTime.MinValue) old.FirstRunUtc = CreatedUtc(legacy);
                if (old.BiomeFromUtc == DateTime.MinValue) old.BiomeFromUtc = DateTime.UtcNow;
                if (Panel.SampleMode.Quiet("local totals")) return old;   // Dev.SampleData: nothing moved, nothing saved
                old.Save(path, DateTime.UtcNow);
                var moved = legacy + ".migrated-" + SafeName(name);
                try { File.Move(legacy, moved); problem = "local totals " + legacy + " (from before 0.6.1) moved to this character's file " + path + "; the old file is kept as " + moved; }
                catch (Exception e) { problem = "local totals " + legacy + " copied to " + path + " but could not be renamed (" + e.Message + "); another character with this id will not adopt it while this one exists"; }
                return old;
            }
            problem = "local totals " + legacy + " (from before 0.6.1, keyed by the player id only) " +
                      (plausible == 0 ? "do not fit this character's game counters" : "fit " + (others + 1) + " characters with this id on this PC") +
                      "; left as they are, never merged: this character counts from zero in " + path;
            return Named(Fresh(playerId), name);
        }

        static LocalTotals Named(LocalTotals t, string name) { t.Name = name; return t; }
        static bool Exists(string path) => File.Exists(path) || File.Exists(path + AtomicFile.BackupSuffix);

        /// <summary>
        /// Loads the totals at <paramref name="path"/>. No file and no backup (an older install, a new character): empty
        /// totals, counting starts at zero. A main file that is missing or cannot be read as totals while its .bak can: the
        /// backup is used (the broken main set aside, renamed, never overwritten) and <paramref name="problem"/> says so. Neither
        /// readable: the main file is set aside and counting starts at zero. A file from a newer format (main or, when the main
        /// is broken, the backup), or one that cannot be opened at all: null (do not save over it), with the reason in
        /// <paramref name="problem"/>; the file stays exactly as it is.
        /// </summary>
        public static LocalTotals Load(string path, long playerId, out string problem)
        {
            problem = null;
            var bak = path + AtomicFile.BackupSuffix;
            if (!Exists(path)) return Fresh(playerId);
            string text; bool fromBackup;
            try { text = AtomicFile.ReadWithBackup(path, Usable, out fromBackup); }
            catch (Exception e) { problem = "local totals " + path + " could not be opened, not saving over them: " + e.Message; return null; }
            var newer = text == null ? 0 : NewerVersion(text);
            if (newer > 0)
            {
                problem = "local totals " + (fromBackup ? bak : path) + " are from a newer Hearthwoven (version " + newer + "), not saving over them";
                return null;
            }
            var t = text == null ? null : FromJson(text);
            if (t != null && !fromBackup)
            {
                t.PlayerId = playerId; if (t.FirstRunUtc == DateTime.MinValue) t.FirstRunUtc = CreatedUtc(path);
                if (t.BiomeFromUtc == DateTime.MinValue) t.BiomeFromUtc = DateTime.UtcNow;   // earlier sessions of this file have no biome: per biome counts from now
                return t;
            }
            if (Panel.SampleMode.Quiet("local totals")) { problem = "local totals " + path + " are unreadable; left as they are while Dev.SampleData is on"; return null; }
            string aside = null;
            if (File.Exists(path))
            {
                aside = path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".unreadable";
                for (int n = 2; File.Exists(aside); n++) aside = path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "-" + n + ".unreadable";   // never over an earlier one
                try { File.Move(path, aside); }
                catch (Exception e) { problem = "local totals " + path + " are unreadable and could not be set aside, not saving over them: " + e.Message; return null; }
            }
            if (t != null)   // the backup
            {
                t.PlayerId = playerId; if (t.FirstRunUtc == DateTime.MinValue) t.FirstRunUtc = CreatedUtc(bak);
                if (t.BiomeFromUtc == DateTime.MinValue) t.BiomeFromUtc = DateTime.UtcNow;
                problem = "local totals " + path + (aside == null ? " were missing" : " were unreadable (set aside as " + aside + ")") + "; using the backup " + bak +
                          (t.SavedUtc > DateTime.MinValue ? " saved " + t.SavedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC" : "");
                return t;
            }
            problem = "local totals " + path + (aside == null ? " were missing" : " were unreadable; set aside as " + aside) +
                      (File.Exists(bak) ? " and the backup " + bak + " is unreadable too (left as it is)" : "") + ", counting again from zero";
            return Fresh(playerId);
        }

        /// <summary>Writes the totals atomically with one backup (AtomicFile): a crash or power loss mid-write keeps the old
        /// file, and the save before it stays as .bak.</summary>
        public void Save(string path, DateTime savedUtc)
        {
            if (Panel.SampleMode.Quiet("local totals")) return;   // Dev.SampleData: the real file stays untouched
            AtomicFile.Write(path, ToJson(savedUtc));
        }
    }
}
