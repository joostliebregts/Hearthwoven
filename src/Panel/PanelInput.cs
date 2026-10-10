using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Everything the panel reads about one player. Built either from the live objects on this PC (PanelUi) or from a
    /// fellow player's shared snapshot (FromSnapshot), so every chapter renders the same way for anyone.
    /// Pure C#: filled by hand in the tests.
    /// </summary>
    public class PanelInput
    {
        public string PlayerName = "";
        public long PlayerId;                               // the game's player id (feasts name their creator by it)
        public string FellowKey;                            // a fellow's copy: who it is (FellowIds.KeyOf: platform id and profile id), never the shown name; null on your own book
        public DateTime NowUtc = DateTime.UtcNow;
        public DateTime? SessionStartUtc;                   // first spawn of this connection (a session); null = not known
        public bool IsSelf = true;                          // false: a fellow player's shared snapshot
        public bool Solo;                                   // singleplayer: no fellow players can appear (PanelModel.SoloNote)
        public string ViewerName;                           // who is looking (marked "(you)" in someone else's company)
        public DateTime? LastRecordedUtc;                   // fellow player: newest moment in their shared log
        public DateTime? FirstRecordedUtc;                  // fellow player: oldest moment in their shared log (its log starts with their connection)
        public DateTime? CharacterMade;                     // the day the character was made (PlayerProfile.m_dateCreated); null = not known (a fellow's copy)
        public DateTime? InstalledUtc;                      // when Hearthwoven first counted for this character on this PC (LocalTotals.FirstRunUtc); null = not known

        // since this character was made (the game's own profile, slot 0)
        public IDictionary<string, float> Character;        // PlayerStatType name -> value; null = no profile
        public IDictionary<string, float> ItemsCrafted;     // item token ("$item_bread") -> count
        public IDictionary<string, float> PiecesPlaced;     // piece token ("$piece_woodwall") -> count
        public IDictionary<string, float> EnemyKills;       // enemy token ("$enemy_troll") -> kills
        public IDictionary<string, float> SkillLevels;      // skill name ("Blocking") -> level now
        public IDictionary<string, float> SkillProgress;    // skill name -> progress to the next level, 0..1 (Skills.Skill.GetLevelPercentage); null = not known
        public IDictionary<string, float> ItemsPickedUp;    // item token ("$item_wood") -> amount, first time anyone held it (any source)
        public IDictionary<string, Dictionary<string, float>> Baseline;
        public IDictionary<string, Dictionary<string, float>> ExactAtBaseline;   // kind -> what Hearthwoven had counted exactly (since install) when the baseline was taken (LocalTotals.ExactAtBaseline); null = none   // kind ("pickedUp") -> the game counter when Hearthwoven first ran for this character (LocalTotals.Baseline); null = not known (a fellow's copy)
        public IDictionary<string, DateTime> BaselineAt;    // kind -> when that baseline was taken (LocalTotals.BaselineAt); a kind without one counts from the install; null = not known
        public IDictionary<string, DateTime> Starts;        // counter group -> UTC start (LocalTotals.Starts: cargo, led, born, feats); null = not known (a fellow's copy)
        public ICollection<string> KnownBiomes;             // biomes this character found (Player.m_knownBiome, as Heightmap.Biome names); null = not known

        // what the game data says about a token, derived at runtime by PanelUi (item type, drop tables, piece components).
        // null (or a null answer) = unknown; PanelModel then falls back to the vanilla names it knows or keeps it apart.
        public Func<string, string> ItemKind;               // item token -> "gear" | "food" | "other"
        public Func<string, bool> StationDish;              // item token -> true = a dish a cooking station hands out (grill, iron cooking station, oven, a mod's); false or unknown = made in the crafting window (cauldron, prep table)
        public Func<string, string> GatherKind;             // item token -> "wood" (dropped by trees and logs) | "mining" (stone and ore) | PanelModel.PickaxeFinds (other pickaxe drops)
        public Func<string, string> PieceKind;              // piece token -> "built" | "ground" | "planted" | "feast"
        public Func<string, string> ItemColour;             // item token -> "#rrggbb", its icon's own colour (PanelLook.IconColour); null = unknown

        // measured by Hearthwoven on a PC. Yourself: Events since install (previous sessions + this one, LocalTotals), Log and
        // Session this session only (Battle's time windows). A fellow player: their last session (their shared snapshot).
        public DamageTally Session;
        public SessionEvents Events;
        public EventLog Log;
        // damage since install (yourself: LocalTotals' earlier sessions + this one). Null = not known (an older fellow's copy):
        // Foes, Defense and Together then read Session and Log as before.
        public DamageTally DamageSinceInstall;
        public BiomeTally BiomeSinceInstall;                // damage and deaths per biome since install (BiomeTally); null = not known (an older fellow's copy)
        public DateTime? BiomeFromUtc;                      // per biome counted from this moment, when later than the install (an older file); null = not known
        public bool SharedSinceInstall;                     // a fellow's copy carries their since-install totals (Events, DamageSinceInstall)
        /// <summary>This session's measured tallies alone (Plugin.Events; a fellow's copy: their "measuredThisSession"); null = not known.
        /// Defence's blocks and parries in the Session window.</summary>
        public SessionEvents SessionOnly;
        /// <summary>The day history (yourself: LocalTotals.History, HISTORY-06.md): what each play day added. null = none (a fellow's copy,
        /// totals not loaded): the day windows are greyed.</summary>
        public DayHistory History;
        /// <summary>What the running session counted since the last save, as today's row (LocalTotals.Pending); added to the history's today.</summary>
        public DayHistory.Row Pending;
        /// <summary>A fellow's damage dealt per local day, the last 30 days (their snapshot's "dealtByDay"); null = not shared (an older Hearthwoven).</summary>
        public Dictionary<string, float> DealtByDay;
        /// <summary>A day window's input (PanelModel.InWindow): the window this copy holds; null = the input as recorded.</summary>
        public PanelModel.DayView Window;
        /// <summary>Defence's armour view (0.7, ArmourTally.cs), yourself only (never shared; a fellow's copy: all null). This session's ledger, the same
        /// in one-minute buckets (the short windows), everything recorded on this PC, the book with its day rows, the running session's unsaved part
        /// (added to today), and when recording began ("Recorded from"); null = not known.</summary>
        public ArmourTally ArmourSession, ArmourSince, ArmourPending;
        public ArmourLog ArmourMinutes;
        public ArmourBook ArmourBook;
        public DateTime? ArmourFromUtc;
        /// <summary>The battle record (0.8, BattleRecord.cs; the model: Chapters/BattleRecModel.cs). Yourself: this session's recorder (foes per
        /// creature, the fights of the feed), the foes kept on this PC (FoeBook, day rows), the foes since recording began (book + session), the
        /// running session's unsaved part (added to today) and when recording began. A fellow's copy: FoesSession and FoesSince as they shared them
        /// (no recorder, no feed, no book). Battle.DealtAfterArmour (dev, yourself only): your damage after the foe's armour this session. null = not known.</summary>
        public BattleRecorder Battle;
        public FoeBook FoeBook;
        public FoeCounts FoesSession, FoesSince, FoesPending;
        public DateTime? FoesFromUtc;
        public ArmourTally DealtArmourSession;
        public ArmourLog DealtArmourMinutes;
        /// <summary>Deeds > Recent and Since you were away (0.7, RecentModel.cs). Yourself: this session's deeds per minute (DeedLog, flushed when
        /// gathered) and when your previous session on this PC ended (LocalTotals' last save before this session; null = no earlier session
        /// here). A fellow's copy: its session id, when this PC received this copy, and their mark as this PC last saw it before this session
        /// (FellowMarks.Before; null = never seen before). Null = not known.</summary>
        public DeedLog Deeds;
        public DateTime? PreviousSessionEndUtc;
        public string SessionId;
        public DateTime? ReceivedUtc;
        public FellowMarks.Mark SeenBefore;
        /// <summary>A fellow's copy (0.7, B33): when their copies reached this PC this connection (null: none yet, e.g. only the cached one);
        /// whether their copies come timed finely enough for 10 min to 3 h (the server sends live updates and their Hearthwoven makes them);
        /// whether they were in the world at all this connection (null: not known); and the start of YOUR session, which their Session counts from.</summary>
        public FellowTrail Trail;
        public bool Timed;
        /// <summary>The server sends live updates (a 0.7 server): a fellow's copy that still comes untimed is from an older Hearthwoven (REVIEW-07 #11).</summary>
        public bool ServerLive;
        public bool? OnThisSession;
        public DateTime? ViewerSessionStartUtc;
        /// <summary>A fellow's copy shown from this PC's cache until the server's fresh one comes (B23): "as Edda last shared it" with ReceivedUtc's date.</summary>
        public bool Cached;

        /// <summary>A copy that shares every reference (PanelModel.InWindow swaps the tallies of the window into it).</summary>
        public PanelInput ShallowCopy() => (PanelInput)MemberwiseClone();

        public ServerBook.Shared Book;                      // the server's book for this player (cargo loaded and unloaded, born near; GroupShare); null = none (a server before 0.6, or you do not share)
        public FeatsLedger Feats;                           // the feats earned and their moments: yourself, the ledger on this PC (counters too); a fellow's, what they shared (no counters); null = none known
        public List<PanelInput> Fellows;                    // everyone else who shares (and you, when looking at a fellow player)
        public IDictionary<long, string> PlayerNames;       // player id -> name, for feast creators (players nearby)
        public Func<string, string> DisplayName;            // prefab or $token -> localized name; null = prettified
        public Func<DateTime, DateTime> ToLocal;            // null = the PC's own time zone
        // Battle (ch-battle): the game's own creature and arrow data, read at runtime (BattleGame); null = unknown, nothing shown
        public Func<string, PanelModel.FoeData> Foe;        // creature prefab -> its damage modifiers and trophy
        public Func<IList<PanelModel.ArrowData>> Arrows;    // every arrow the game defines, with its damage
        public Func<string, bool> RecipeKnown;              // item token -> your character knows its recipe (Player.IsRecipeKnown); null = not known (a fellow's copy)
        public Func<IList<PanelModel.ArrowData>> Gear;      // 0.7 Foes ranking: every bolt and weapon the game defines, with its base damage (Kind bolt | weapon); null = unknown
        public Func<string, bool> Owned;                    // item token -> in your inventory now (read once per page build); null = not known (a fellow's copy, the tests)
        public IDictionary<string, float> Harvested;        // since this character was made: m_pickableStats, pickable item prefab ("Barley") or fish token ("$animal_fish1") -> count
        public Func<string, string> CropOf;                 // planted piece token -> the item prefab its grown plant yields ("Barley"); null = not a crop
        public Func<string, string> MainMaterial;           // gear token -> its main material ("Bronze"), from its recipe in the game's data (PanelModel.MainMaterial); null = not known
        public Func<string, string> ItemType;               // item token -> the game's item type ("OneHandedWeapon"); null = not known
        public Func<string, string> PieceTab;               // piece token -> the hammer tab it sits on ("Furniture"), from Piece.m_category and the piece table's own labels; null = not known (Building's filter)
        public Func<string, string> PieceMaterial;          // piece token -> its main material ("Fine wood"), from the build resources in the game's data (PanelModel.PieceMainMaterial); null = not known
        public Func<string, string> DishType;               // food token -> "meal" | "grilled" | "baked" | "feast" | "uncooked" | "meadbase", from the game's data (PanelModel.DishTypeOf); null = not known (Cooking's filter: Other)
        public Func<string, string> DishBoost;              // food token -> "health" | "stamina" | "eitr" | "balanced", its biggest food value (PanelModel.DishBoostOf); null = no food value or not known
        public Func<string, string> ItemToken;              // item prefab ("CookedMeat") -> its token ("$item_cookedmeat"), as fellows record food they ate; null = not known
        public Func<string, int> RecipeYield;               // item token -> how many one craft puts in the bag (Recipe.m_amount: 4 sausages), from this PC's game data; null or 0 = not known (one)

        /// <summary>
        /// A fellow player's shared snapshot (Snapshot.Build output, received through GroupShare). Rebuilds the mod's own
        /// recorder objects from the JSON; null if it cannot be read. Shared copies carry no death positions.
        /// </summary>
        public static PanelInput FromSnapshot(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) return null;
            var input = new PanelInput { IsSelf = false, PlayerName = Str(root, "name"), PlayerId = (long)Num(root, "playerId"), SessionId = Str(root, "session"), Session = new DamageTally(), Events = new SessionEvents(), Log = new EventLog() };
            input.FellowKey = FellowIds.KeyOf(json);   // the same key GroupShare keeps the copy under (the group feats count each person once by it)
            if (root.TryGetValue("stats", out var st) && st is List<object> slots && slots.Count > 0 && slots[0] is Dictionary<string, object> slot0)
            {
                input.Character = Dict(Obj(slot0, "counters"));
                input.ItemsCrafted = Dict(Obj(slot0, "itemsCrafted"));
                input.PiecesPlaced = Dict(Obj(slot0, "piecesPlaced"));
                input.ItemsPickedUp = Dict(Obj(slot0, "itemsPickedUp"));
                input.Harvested = Dict(Obj(slot0, "harvested"));
                if (slot0.TryGetValue("enemyKills", out var ek) && ek is List<object> tables && tables.Count > 0 && tables[0] is Dictionary<string, object> t0)
                    input.EnemyKills = Dict(Obj(t0, "kills"));
            }
            if (root.TryGetValue("skills", out var sk) && sk is List<object> skills)
            {
                input.SkillLevels = new Dictionary<string, float>();
                input.SkillProgress = new Dictionary<string, float>();
                foreach (var s in skills.OfType<Dictionary<string, object>>())
                {
                    if (Str(s, "skill").Length == 0) continue;
                    input.SkillLevels[Str(s, "skill")] = (float)Num(s, "level");
                    if (s.ContainsKey("progress")) input.SkillProgress[Str(s, "skill")] = PanelModel.ProgressOf((float)Num(s, "level"), (float)Num(s, "progress"));
                }
            }
            input.Session.ReadFrom(Obj(root, "damageThisSession"));
            // since install when the sender's Hearthwoven shares it (SOURCES.md fix 3); an older one: their last session, as before
            var since = Obj(root, "measuredSinceInstall");
            input.Events.ReadFrom(since ?? Obj(root, "measuredThisSession"));
            input.SharedSinceInstall = since != null;
            if (Obj(root, "measuredThisSession") != null) input.SessionOnly = new SessionEvents().ReadFrom(Obj(root, "measuredThisSession"));
            input.DealtByDay = Dict(Obj(root, "dealtByDay"));
            var foes = FoeShare.ReadFrom(Obj(root, "foes"));   // 0.8: the foes they fought per kind (an older copy has none: null)
            if (foes != null) { input.FoesSession = foes.Session; input.FoesSince = foes.Since; input.FoesFromUtc = foes.FromUtc; }
            // the biomes they found (0.7 on; an older copy has none: null, and the group feats' gate falls back on the evidence the copy carries)
            if (root.TryGetValue("knownBiomes", out var kb) && kb is List<object> biomes) input.KnownBiomes = biomes.OfType<string>().Where(b => b.Length > 0).Take(16).ToList();
            if (Obj(root, "feats") != null) input.Feats = FeatsLedger.ReadShared(Obj(root, "feats"));   // the feats they earned (Feats page, their book)
            if (Obj(root, "featBests") != null) { input.Feats = input.Feats ?? new FeatsLedger(); input.Feats.ReadSharedBests(Obj(root, "featBests")); }   // the bests behind Heavy Keel and Long Lead
            var damageSince = Obj(root, "damageSinceInstall");
            if (damageSince != null) input.DamageSinceInstall = new DamageTally().ReadFrom(damageSince);
            var biomeSince = Obj(root, "biomeSinceInstall");
            if (biomeSince != null)
            {
                input.BiomeSinceInstall = new BiomeTally().ReadFrom(biomeSince);
                if (DateTime.TryParse(Str(biomeSince, "from"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var from)) input.BiomeFromUtc = from.ToUniversalTime();
            }
            var log = Obj(root, "measuredLog");
            Into(Obj(log, "damage"), input.Log.Damage); Into(Obj(log, "hits"), input.Log.Hits);
            input.Log.Span = log != null && Num(log, "bucketMinutes") >= 1 ? (int)Num(log, "bucketMinutes") : 10;   // an older sender kept ten-minute buckets
            DateTime? last = null, first = null;
            foreach (var k in input.Log.Damage.Keys)
                if (DateTime.TryParseExact(k.Split('|')[0], "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var b))
                { if (last == null || b > last) last = b; if (first == null || b < first) first = b; }
            if (log != null && log.TryGetValue("deaths", out var ds) && ds is List<object> deaths)
                foreach (var o in deaths.OfType<Dictionary<string, object>>())
                {
                    if (!DateTime.TryParse(Str(o, "t"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)) continue;
                    var d = new EventLog.Death { Time = t.ToUniversalTime(), Biome = Str(o, "biome", "None"), Killer = Str(o, "killer", "unknown"), Cause = Str(o, "cause", "unknown") };
                    Into(Obj(o, "last10s"), d.Last10s);
                    if (o.TryGetValue("timeline", out var tl) && tl is List<object> hits)   // an older snapshot has none: the totals stay
                        foreach (var h in hits.OfType<Dictionary<string, object>>())
                            d.Timeline.Add(new EventLog.Hit { Ago = (float)Num(h, "ago"), Amount = (float)Num(h, "amount"), Source = Str(h, "source", "?"), Cause = Str(h, "cause", "?"), Type = Str(h, "type", "damage") });
                    input.Log.Deaths.Add(d);
                    if (last == null || d.Time > last) last = d.Time;
                    if (first == null || d.Time < first) first = d.Time;
                }
            input.LastRecordedUtc = last; input.FirstRecordedUtc = first;
            return input;
        }

        static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) => MiniJson.Obj(d, k);
        static double Num(Dictionary<string, object> d, string k) => MiniJson.Num(d, k);
        static string Str(Dictionary<string, object> d, string k, string otherwise = "") => MiniJson.Str(d, k, otherwise);
        static Dictionary<string, float> Dict(Dictionary<string, object> from) { if (from == null) return null; var d = new Dictionary<string, float>(); Into(from, d); return d; }
        static void Into(Dictionary<string, object> from, IDictionary<string, float> to) => MiniJson.Into(from, to);
    }
}
