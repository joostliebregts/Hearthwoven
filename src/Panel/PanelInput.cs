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
    public partial class PanelInput
    {
        public string PlayerName = "";
        /// <summary>0.8.2 (I): a fellow's copy: the Hearthwoven that sent it ("mod", e.g. "0.6.5"), so a page can say what fills in once they
        /// update (PanelModel.OlderLine); null on your own book or when the copy does not say.</summary>
        public string ModVersion;
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
        /// <summary>0.8.2: Log reaches back before this session (yourself: your kept minutes of earlier connections, KeptMinutes, joined under
        /// it): where this session begins, which Session counts from (PanelModel.SessionFrom). null: the log is this session's alone.</summary>
        public DateTime? LogSessionFromUtc;
        /// <summary>0.8.2: a fellow's copy: their earlier connections' last minutes in this world as they kept them ("logBefore", KeptMinutes),
        /// from LogBeforeFromUtc on; FellowTrail.Joined puts them under Log. null: none (one connection, or a copy from before 0.8.2).</summary>
        public EventLog LogBefore;
        public DateTime? LogBeforeFromUtc;
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
        /// <summary>0.9: a fellow's damage dealt per local day split by weapon kind and battle type (day -> "melee|slash" -> amount, the last 30 days;
        /// their snapshot's "dayDealt", DayHistory.DayDealt), so the group's By weapon and By type work on Today to 30 days; null = not shared (a
        /// Hearthwoven before 0.9: their days are one total each, DealtByDay).</summary>
        public Dictionary<string, Dictionary<string, float>> DayDealt;
        /// <summary>0.8.6 (DPS per named weapon): their weapons per local day, the last 30 days (their snapshot's "dayWeapons"), so their By weapon shows them on Today, 7 days
        /// and 30 days; null = not shared (a Hearthwoven before 0.8.6). Never read into a ranking: only their own book shows it.</summary>
        public Dictionary<string, WeaponCells> DayWeapons;
        /// <summary>0.8.5: a fellow's Deeds per local day, the last 30 days (day -> family -> part -> amount; their snapshot's "dayDeeds",
        /// PanelModel.DayDeeds): what their Deeds pages' heroes and first bars show, so those pages and the group's work on Today to 30 days;
        /// null = not shared (a Hearthwoven before 0.8.5).</summary>
        public Dictionary<string, Dictionary<string, Dictionary<string, float>>> DayDeeds;
        /// <summary>0.8.5: their DayDeeds carry km sailed and cargo per day (PanelModel.SharesVoyageDays); false = not shared (the 0.8.5 test build or older),
        /// so Together leaves them out of Sailed and Cargo carried in a day window instead of reading "sailed nothing".</summary>
        public bool DayVoyages;
        /// <summary>0.9.1: their DayDeeds carry the foes defeated per day (the game's kill counter, PanelModel.SharesDayKills); false = not shared (before 0.9.1),
        /// so their Recent days say so instead of "defeated nothing".</summary>
        public bool DayKills;
        /// <summary>A day window's input (PanelModel.InWindow): the window this copy holds; null = the input as recorded.</summary>
        public PanelModel.DayView Window;
        /// <summary>Defence's armour view (0.7, ArmourTally.cs). This session's ledger, the same in one-minute buckets (the short windows), everything
        /// recorded on this PC, the book with its day rows, the running session's unsaved part (added to today), and when recording began ("Recorded
        /// from"); null = not known. 0.9 (armor-share): a fellow's copy has ArmourSince (their ledger since it began, per damage type, keys "||type") and
        /// ArmourFromUtc from its "armorSince"; their short windows read the "armor" minutes of their log (EventLog.Armor); the rest stays null. A copy
        /// without "armorSince" (a Hearthwoven before 0.9): ArmourSince null.</summary>
        public ArmourTally ArmourSession, ArmourSince, ArmourPending;
        public ArmourLog ArmourMinutes;
        public ArmourBook ArmourBook;
        public DateTime? ArmourFromUtc;
        /// <summary>The battle record (0.8, BattleRecord.cs; the model: Chapters/BattleRecModel.cs). Yourself: this session's recorder (foes per
        /// creature, the fights of the feed), the foes kept on this PC (FoeBook, day rows), the foes since recording began (book + session), the
        /// running session's unsaved part (added to today) and when recording began. A fellow's copy: FoesSession and FoesSince as they shared them
        /// (no recorder, no feed, no book). Battle.DealtAfterArmour (dev, yourself only): your damage after the foe's armour this session. null = not known.</summary>
        public BattleRecorder Battle;
        /// <summary>0.8.2: a fellow's battle feed as their copy shares it (FeedShare: their newest foes, names only of players who share); null on
        /// your own book (the recorder holds yours) and for a copy from before 0.8.2.</summary>
        public FeedShare SharedFeed;
        /// <summary>0.8.5: your fights of earlier sessions in this world, kept on this PC (KeptFights), newest first: the feed lists them under this
        /// session's, Last fight shows the newest when it is newer than any of this session (Fights, through the fight adapter). Null on a fellow's
        /// copy (their copy carries their feed of this session only) and before the totals load.</summary>
        public List<KeptSession> KeptSessions;
        /// <summary>0.8.6 (a friend's older feed, FeedBlocks.cs): on a fellow's book, the sessions the server holds of theirs, newest first (the one their running
        /// copy is excluded); null on your own book and where none is listed. BlockSessions: the ones fetched, as kept sessions of theirs (id -> session);
        /// BlockStates: the others by id ("loading" or "failed", else not asked). BlocksStamp changes whenever any of it does (the feed's cache key).</summary>
        public List<BlockRow> BlockRows;
        public Dictionary<string, KeptSession> BlockSessions;
        public Dictionary<string, string> BlockStates;
        /// <summary>0.8.6: the seconds a session on its way has waited (id -> seconds), for the sample and the tests; null in game, where the book counts them on the line itself
        /// (PanelUi.FetchTick), so the page stays the same while it waits.</summary>
        public Dictionary<string, int> BlockWaited;
        public string BlocksStamp = "";
        /// <summary>0.9: the boss attempts you took part in, kept on this PC (BossLog); null on a fellow's copy and before the totals load.</summary>
        public BossLog BossLog;
        /// <summary>0.9: the fights for Battle > Last fight, Runs and bosses and their pages (Chapters/FightView.cs), filled by the fight adapter; null = none yet (Last fight draws the 0.8 page).</summary>
        public FightBook Fights;
        public FoeBook FoeBook;
        public FoeCounts FoesSession, FoesSince, FoesPending;
        public DateTime? FoesFromUtc;
        /// <summary>0.8.5, the dealt ledger (Plugin.DealtArmour; Chapters/AfterArmourModel.cs): your hits on foes as you swung them and after the foe's
        /// resistances and armor, per "foe|skill|type": this session, everything recorded (earlier sessions + this one), the book (day rows), this
        /// session's unsaved part and when recording began. Your own book only (the short windows read the log's After, which is shared); null: none.</summary>
        public ArmourTally DealtArmourSession, DealtArmourSince, DealtArmourPending;
        public ArmourBook DealtArmourBook;
        public DateTime? DealtArmourFromUtc;
        /// <summary>0.8.6 (DPS): when your fighting time began to be recorded (the dealt ledger's book); the ledger's Swing (All, Session, the days) counts
        /// from then. null: not yet. Yourself only.</summary>
        public DateTime? SwingFromUtc;
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
        /// <summary>0.8.5: the world's part of the server's book, the same on every book (Stoker's Chest feeding, counted once on the server whoever
        /// hosts the chest); null = none (a server before 0.8.5, nothing fed yet, or you do not share).</summary>
        public ServerBook.World World;
        public FeatsLedger Feats;                           // the feats earned and their moments: yourself, the ledger on this PC (counters too); a fellow's, what they shared (no counters); null = none known

        // ---------- 0.9, the records (work/hearthwoven-0.9/DESIGN.md sections 1 and 3, TRACK-record.md). null = not recorded on this book
        // (a fellow's copy, the totals not loaded yet, the sample); every one is yours only and never shared in this form ----------
        /// <summary>0.9, record 1: your upgrades (UpgradeLog): Recent = {Token, From, To, Station, StationLevel, Utc} newest first (the newest 2 000),
        /// Folded = the older ones per item {Token, MaxLevel, Count, First, Last}; PerItem() joins both per item.</summary>
        public UpgradeLog Upgrades;
        /// <summary>0.9: when upgrades began to be recorded (UTC instant; the panel shows its local day: "Recorded from").</summary>
        public DateTime? UpgradesFrom;
        /// <summary>0.9, record 3: token ("$item_axe_bronze", "$piece_woodwall") -> the local day you first made or placed it, from FirstsFrom on.</summary>
        public IDictionary<string, DateTime> Firsts;
        /// <summary>0.9: when firsts began to be recorded (UTC instant).</summary>
        public DateTime? FirstsFrom;
        /// <summary>0.9 (Meals' Your combinations): the foods you had on together at each meal (Hearthwoven.MealCombos: per combination its count,
        /// first and last local day, the game's food values summed, the meals per local day of the last 40 days), from its FromUtc on. Yours only.</summary>
        public MealCombos MealCombos;
        /// <summary>0.9, record 4: the earned feat tiers you opened, "featId:tier" (FeatsLedger.SeenTiers, the same set: FeatsLedger.MarkTierSeen adds
        /// to it). A tier not in it is New.</summary>
        public ISet<string> FeatsSeen;
        /// <summary>0.9, record 2: your deed stretches per session in this world (DeedSession {World, Id, FromUtc, ToUtc, Live, Stretches}; a stretch
        /// DeedStretch {Family, FromUtc, ToUtc, Counts "deedfamily|token" -> n, Moments, MomentsLeft}; a moment DeedMoment {Kind made | upgraded |
        /// feast | first, Token, Utc, Count, From, To, Station, StationLevel}): this session first (Live) when it has one, then the earlier sessions
        /// kept on this PC (KeptDeeds, 30 days), newest first.</summary>
        public List<DeedSession> DeedStretches;
        /// <summary>0.9 (crafting-history, Crafting's Upgrades per item): an item token's highest quality the game allows (ItemData.SharedData.m_maxQuality,
        /// GameData.MaxQuality: 4 for most gear); null or 0 = not known.</summary>
        public Func<string, int> MaxQuality;
        public List<PanelInput> Fellows;                   // everyone else who shares (and you, when looking at a fellow player)
        public IDictionary<long, string> PlayerNames;       // player id -> name, for feast creators (players nearby)
        public Func<string, string> DisplayName;            // prefab or $token -> localized name; null = prettified
        public Func<DateTime, DateTime> ToLocal;            // null = the PC's own time zone
        // Battle (ch-battle): the game's own creature and arrow data, read at runtime (BattleGame); null = unknown, nothing shown
        public Func<string, PanelModel.FoeData> Foe;        // creature prefab -> its damage modifiers and trophy
        public Func<string, string> FoeKin;                 // 0.8.5: creature prefab -> the main prefab of every creature the game gives its name (GameData.FoeKin, PanelModel.FoeGroup); null = itself
        public Func<IList<PanelModel.ArrowData>> Arrows;    // every arrow the game defines, with its damage
        public Func<string, bool> RecipeKnown;              // item token -> your character knows its recipe (Player.IsRecipeKnown); null = not known (a fellow's copy)
        public Func<IList<PanelModel.ArrowData>> Gear;      // 0.7 Foes ranking: every bolt and weapon the game defines, with its base damage (Kind bolt | weapon); null = unknown
        public Func<string, bool> Owned;                    // item token -> in your inventory now (read once per page build); null = not known (a fellow's copy, the tests)
        public IDictionary<string, float> Harvested;        // since this character was made: m_pickableStats, pickable item prefab ("Barley") or fish token ("$animal_fish1") -> count
        public Func<string, string> CropOf;                 // planted piece token -> the item prefab its grown plant yields ("Barley"); null = not a crop
        public Func<string, string> TreeOf;                 // tree, log or log half prefab -> the tree it is or fell from (GameData.TreeOf); null = judged by its name
        public Func<string, PanelModel.RockData> Rock;      // prefab a pickaxe hit -> the rock it is part of, mined or not, its main drop (GameData.Rock); null = judged by its name
        public Func<string, string> MainMaterial;           // gear token -> its main material ("Bronze"), from its recipe in the game's data (PanelModel.MainMaterial); null = not known
        public Func<string, string> ItemType;               // item token -> the game's item type ("OneHandedWeapon"); null = not known
        public Func<string, string> PieceTab;               // piece token -> the hammer tab it sits on ("Furniture"), from Piece.m_category and the piece table's own labels; null = not known (Building's filter)
        public Func<string, string> PieceMaterial;          // piece token -> its main material ("Fine wood"), from the build resources in the game's data (PanelModel.PieceMainMaterial); null = not known
        public Func<string, string> DishType;               // food token -> "meal" | "grilled" | "baked" | "feast" | "uncooked" | "meadbase", from the game's data (PanelModel.DishTypeOf); null = not known (Cooking's filter: Other)
        public Func<string, string> DishBoost;              // food token -> "health" | "stamina" | "eitr" | "balanced", its biggest food value (PanelModel.DishBoostOf); null = no food value or not known
        public Func<IList<PanelModel.FoodData>> Foods;      // 0.9.2 Meals' Plan a meal: every food of the game's data with its values, feast mark and what it is made from (GameData.Foods); null = not known
        public Func<string, PanelModel.FoodStats> DishStats; // 0.9.2 food token (or prefab) -> one serving's health, stamina, eitr and seconds from the game's data (GameData.DishStats); null = not known
        public Func<string, string> ItemToken;              // item prefab ("CookedMeat") -> its token ("$item_cookedmeat"), as fellows record food they ate; null = not known
        public Func<string, int> RecipeYield;               // item token -> how many one craft puts in the bag (Recipe.m_amount: 4 sausages), from this PC's game data; null or 0 = not known (one)
        /// <summary>0.9 (Armor sets, Chapters/ArmorSets.cs): every armor piece of this PC's game data, with its recipe and set name (GameData.ArmorPieces); null = not read.</summary>
        public Func<IList<ArmorPiece>> ArmorPieces;
        /// <summary>0.9: a fellow's copy: the armor pieces they made, yes or no per item token (their snapshot's "armorMade"); null = not shared (a Hearthwoven
        /// before 0.9) or your own book (your own counts are in ItemsCrafted).</summary>
        public ICollection<string> ArmorMade;

        /// <summary>
        /// A fellow player's shared snapshot (Snapshot.Build output, received through GroupShare). Rebuilds the mod's own
        /// recorder objects from the JSON; null if it cannot be read. Shared copies carry no death positions.
        /// </summary>
        public static PanelInput FromSnapshot(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) return null;
            var input = new PanelInput { IsSelf = false, PlayerName = Str(root, "name"), PlayerId = (long)Num(root, "playerId"), SessionId = Str(root, "session"), Session = new DamageTally(), Events = new SessionEvents(), Log = new EventLog() };
            input.ModVersion = Str(root, "mod").Length > 0 ? Str(root, "mod") : null;
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
            var dayDealt = Obj(root, "dayDealt");   // 0.9: per day by weapon kind and type (an older copy has none: null); only real days, at most 40 of them
            if (dayDealt != null) input.DayDealt = dayDealt.Where(kv => kv.Value is Dictionary<string, object> && DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                                                           .Take(40).ToDictionary(kv => kv.Key, kv => Dict((Dictionary<string, object>)kv.Value));
            var dayWeapons = Obj(root, "dayWeapons");   // 0.8.6: their weapons per day (an older copy has none: null); only real days, at most 40 of them
            if (dayWeapons != null) input.DayWeapons = dayWeapons.Where(kv => kv.Value is Dictionary<string, object> && DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                                                                 .Take(40).ToDictionary(kv => kv.Key, kv => WeaponCells.Read((Dictionary<string, object>)kv.Value, 60));
            input.DayDeeds = PanelModel.ReadDayDeeds(Obj(root, PanelModel.DayDeedsKey));   // 0.8.5: their Deeds per day (an older copy has none: null); only real days, bounded
            input.DayVoyages = PanelModel.SharesVoyageDays(Obj(root, PanelModel.DayDeedsKey));   // 0.8.5: km sailed and cargo among them
            input.DayKills = PanelModel.SharesDayKills(Obj(root, PanelModel.DayDeedsKey));   // 0.9.1: the foes defeated per day among them
            var foes = FoeShare.ReadFrom(Obj(root, "foes"));   // 0.8: the foes they fought per kind (an older copy has none: null)
            if (foes != null) { input.FoesSession = foes.Session; input.FoesSince = foes.Since; input.FoesFromUtc = foes.FromUtc; }
            input.SharedFeed = FeedShare.ReadFrom(Obj(root, "battleFeed"));   // 0.8.2: their battle feed (an older copy has none: null)
            // the biomes they found (0.7 on; an older copy has none: null, and the group feats' gate falls back on the evidence the copy carries)
            if (root.TryGetValue("knownBiomes", out var kb) && kb is List<object> biomes) input.KnownBiomes = biomes.OfType<string>().Where(b => b.Length > 0).Take(16).ToList();
            // 0.9: which armor pieces they made, yes or no (Armor sets on Everyone); at most 300 tokens, an older copy has none (null)
            if (root.TryGetValue(PanelModel.ArmorMadeKey, out var am) && am is List<object> armor) input.ArmorMade = new HashSet<string>(armor.OfType<string>().Where(t => t.Length > 0 && t.Length < 120).Take(300), StringComparer.Ordinal);
            // 0.9 (armor-share): their armour ledger since it began, per damage type (an older copy has none: null, "Their version doesn't share this yet")
            input.ArmourSince = ArmourTally.ReadShared(Obj(root, ArmourTally.SharedKey), out var armourFrom); if (input.ArmourSince != null) input.ArmourFromUtc = armourFrom;
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
            input.Log.ReadFrom(Obj(root, "measuredLog"));
            // 0.8.2: their earlier connections' last minutes in this world (KeptMinutes), apart from this connection's log: FellowTrail.Joined puts
            // them under it for the windows, while the trail, the recorded span and the foe counts stay this connection's
            var before = Obj(root, KeptMinutes.SharedKey);
            if (before != null && DateTime.TryParse(Str(before, "from"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var beforeFrom))
            { input.LogBefore = new EventLog().ReadFrom(before); input.LogBeforeFromUtc = beforeFrom.ToUniversalTime(); }
            input.RecordedFromLog();
            return input;
        }

        /// <summary>A fellow's FirstRecordedUtc and LastRecordedUtc: the oldest and newest moment their Log holds (its damage buckets and deaths).</summary>
        public void RecordedFromLog()
        {
            DateTime? last = null, first = null;
            foreach (var k in Log.Damage.Keys)
                if (DateTime.TryParseExact(k.Split('|')[0], "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var b))
                { if (last == null || b > last) last = b; if (first == null || b < first) first = b; }
            foreach (var d in Log.Deaths)
            {
                if (last == null || d.Time > last) last = d.Time;
                if (first == null || d.Time < first) first = d.Time;
            }
            LastRecordedUtc = last; FirstRecordedUtc = first;
        }

        static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) => MiniJson.Obj(d, k);
        static double Num(Dictionary<string, object> d, string k) => MiniJson.Num(d, k);
        static string Str(Dictionary<string, object> d, string k, string otherwise = "") => MiniJson.Str(d, k, otherwise);
        static Dictionary<string, float> Dict(Dictionary<string, object> from) { if (from == null) return null; var d = new Dictionary<string, float>(); Into(from, d); return d; }
        static void Into(Dictionary<string, object> from, IDictionary<string, float> to) => MiniJson.Into(from, to);
    }
}
