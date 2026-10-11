using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>One creature in the world, by its network object (the game's ZDOID: the user part and the id part). Pure, so the tests
    /// need no game object; BattleHooks makes one from a ZDOID.</summary>
    public readonly struct FoeId : IEquatable<FoeId>
    {
        public readonly long User; public readonly uint Id;
        public FoeId(long user, uint id) { User = user; Id = id; }
        public bool IsNone => User == 0 && Id == 0;
        public bool Equals(FoeId o) => User == o.User && Id == o.Id;
        public override bool Equals(object o) => o is FoeId f && Equals(f);
        public override int GetHashCode() => unchecked(((int)User * 397) ^ (int)(User >> 32) ^ (int)Id);
        public override string ToString() => User.ToString(CultureInfo.InvariantCulture) + ":" + Id.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>What became of a foe you fought. Fighting: the fight is still on. GotAway: the fight ended and it lived (AwayHow says how).</summary>
    public enum FoeOutcome { Fighting, DefeatedByYou, DefeatedWith, GotAway }
    /// <summary>How a foe got away: it was still there when the fight ended (Survived), the game removed it without a kill (Despawned, a night
    /// creature at dawn), or it was no longer loaded on your PC when the fight ended (YouLeft).</summary>
    public enum AwayHow { None, Survived, Despawned, YouLeft }

    /// <summary>
    /// One entry of the battle feed: one foe in one fight. Damage in the eight battle types (PanelModel.BattleTypes order, BattleRecorder.Types):
    /// Dealt as your hits left your weapon (the Damage page's numbers), Received after your armour (the Defence page's "Received from").
    /// </summary>
    public sealed class FoeEncounter
    {
        public FoeId Id;
        /// <summary>The foe's prefab ("Troll", the key every Battle page uses), its name token ("$enemy_troll"), the biome where the fight began.</summary>
        public string Kind, Token, Biome;
        public DateTime StartUtc, EndUtc;   // its first and last hit, either way
        public int Fight;                   // the fight it belongs to (BattleRecorder: a gap of more than FightGap starts the next)
        public readonly float[] Dealt = new float[BattleRecorder.Types.Length], Received = new float[BattleRecorder.Types.Length];
        public int HitsDealt, HitsReceived;
        public FoeOutcome Outcome;
        public AwayHow Away;
        /// <summary>The fellow players the game says hit it too (the creature's own record of its attackers, read on your PC), when it was
        /// defeated with others; Others = how many more the game counted that could not be named (left the world). Your name is never in it.</summary>
        public string[] With = NoNames;
        public int Others;
        /// <summary>0.8.2, a fellow's shared feed only (FeedShare): the players it was defeated with who do not share, so their copy names them not
        /// ("another player"); With then holds only those who share. Always 0 on your own PC.</summary>
        public int Outside;
        /// <summary>0.8.2: the encounter's running number in this session's recorder (1, 2, 3 ...): its key in the shared feed (FeedShare), the same
        /// in every update, so a hit changes a few numbers of one foe there.</summary>
        public int Seq;
        /// <summary>0.9 (work/hearthwoven-0.8.5/fights/DESIGN.md): the foe is a boss (the game's Character.IsBoss); the boss fight it belongs to (its
        /// BossFight.Number on the PC that recorded it, 0: none: a boss, or a foe in the arena); the dungeon run it was fought in (DungeonRun.Number,
        /// 0: outside); your blocks and parries against it that held. Never a place: the arena rule's positions stay in the recorder.</summary>
        public bool IsBoss;
        public int Boss, Run, Blocks, Parries;
        public static readonly string[] NoNames = new string[0];

        public bool Defeated => Outcome == FoeOutcome.DefeatedByYou || Outcome == FoeOutcome.DefeatedWith;
        public float DealtTotal { get { float s = 0; foreach (var v in Dealt) s += v; return s; } }
        public float ReceivedTotal { get { float s = 0; foreach (var v in Received) s += v; return s; } }

        /// <summary>The outcome in the book's words: "defeated by you", "defeated with Edda", "defeated with Edda and Tor", "defeated with Edda and
        /// a fellow player", "defeated with 2 fellow players", "survived", "got away" (the game removed it), "still fighting".</summary>
        public string OutcomeText => Text(Outcome, Away, With, Others, Outside);

        public static string Text(FoeOutcome outcome, AwayHow away, string[] with, int others, int outside = 0)
        {
            switch (outcome)
            {
                case FoeOutcome.DefeatedByYou: return "defeated by you";
                case FoeOutcome.DefeatedWith: return "defeated with " + Names(with ?? NoNames, others, outside);
                case FoeOutcome.GotAway: return away == AwayHow.Despawned ? "got away" : "survived";
                default: return "still fighting";
            }
        }

        /// <summary>"Edda", "Edda and Tor", "Edda, Tor and Ylva", "Edda and a fellow player", "2 fellow players" (never "others": WORDING.md).
        /// <paramref name="outside"/> (a fellow's shared feed): players who do not share, "another player" or "2 other players", as Meals names a
        /// feast's maker who is not known (MealsModel.OutsideCookTitle).</summary>
        public static string Names(IEnumerable<string> with, int others, int outside = 0)
        {
            var parts = new List<string>(with ?? NoNames);
            if (outside > 0) parts.Add(outside == 1 ? AnotherPlayer : outside + " other players");
            if (others > 0) parts.Add(others == 1 ? "a fellow player" : others + " fellow players");
            if (parts.Count == 0) return "a fellow player";
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1).ToArray()) + " and " + parts[parts.Count - 1];
        }

        /// <summary>A player a shared feed does not name (they do not share), in running text: Meals' "Another player" (MealsModel.OutsideCookTitle).</summary>
        public const string AnotherPlayer = "another player";
    }

    /// <summary>One fight of the battle feed: the foes you met with no gap of more than BattleRecorder.FightGap between hits. Entries newest
    /// first (by their last hit); Dealt and Received summed over the entries, in BattleRecorder.Types order.</summary>
    public sealed class FoeFight
    {
        public int Number;
        public DateTime StartUtc, EndUtc;
        /// <summary>The fight is still on (its last hit less than FightGap ago, as of the recorder's last tick).</summary>
        public bool Open;
        public readonly List<FoeEncounter> Entries = new List<FoeEncounter>();
        public readonly float[] Dealt = new float[BattleRecorder.Types.Length], Received = new float[BattleRecorder.Types.Length];
        /// <summary>0.8.2, a fellow's shared feed only (FeedShare): the fight had this many foes where the copy holds fewer (a fight larger than
        /// FeedShare.MaxFoes keeps its newest); 0: every foe is here.</summary>
        public int Of;
        public int Foes => Math.Max(Entries.Count, Of);
        /// <summary>0.8.2, a fellow's shared feed only: the players in the fight with them who do not share (near for NearSeconds, or a foe defeated
        /// with them), counted, never named (FeedShare). Always 0 on your own PC, where With names everyone.</summary>
        public int Outside;
        /// <summary>0.9: your deaths while the fight was on (each a Fall with this Fight); your blocks and parries that held, summed over its foes.</summary>
        public int Deaths, Blocks, Parries;
        public float DealtTotal { get { float s = 0; foreach (var v in Dealt) s += v; return s; } }
        public float ReceivedTotal { get { float s = 0; foreach (var v in Received) s += v; return s; } }
        public TimeSpan Length => EndUtc - StartUtc;
        /// <summary>Who was near you during the fight (0.8, Last fight): player name -> seconds within BattleRecorder.NearMetres of you, sampled
        /// every BattleRecorder.NearEvery while the fight was on (Player.GetAllPlayers on your PC; nothing shared, nothing written). The
        /// recorder's own record, read only; null when nobody was near.</summary>
        public Dictionary<string, float> Near;

        /// <summary>The players in the fight with you: near you for BattleRecorder.NearSeconds or more, or named by the game as having hit a foe
        /// of this fight with you ("defeated with"). Sorted by name; never you.</summary>
        public List<string> With()
        {
            var names = new List<string>();
            if (Near != null) foreach (var kv in Near) if (kv.Value >= BattleRecorder.NearSeconds && !names.Contains(kv.Key)) names.Add(kv.Key);
            foreach (var e in Entries) foreach (var n in e.With) if (!string.IsNullOrEmpty(n) && !names.Contains(n)) names.Add(n);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>Seconds a player was near you in this fight (0: not sampled near).</summary>
        public float NearFor(string name) => Near != null && name != null && Near.TryGetValue(name, out var s) ? s : 0f;

        /// <summary>0.8.5: a fight of an earlier session, kept on this PC (KeptFights): that session; null: this session's, or a fellow's copy.</summary>
        public KeptSession Kept;
    }

    /// <summary>
    /// How many separate foes of each kind you fought and what became of them. Per kind: Fought (foes you hit), Defeated (of those, the game
    /// counted the kill for you: alone or with fellow players), With (of the defeated, together with fellow players), HitYou (foes of the kind
    /// that damaged you), Fighting (still in a running fight: only in this session's live counts, never saved). GotAway is what is left of
    /// Fought. The saved form keeps the first four, as [fought, defeated, with, hitYou] per kind; the shared form adds Fighting as a fifth
    /// number where there is one (REVIEW-08 #8); at most MaxKinds kinds (past it, a new kind is counted under OtherKind, never lost).
    /// </summary>
    public sealed class FoeCounts
    {
        public const int MaxKinds = 300;
        public const string OtherKind = "~other";

        public sealed class Count
        {
            public int Fought, Defeated, With, HitYou, Fighting;
            /// <summary>Foes you fought that lived: Fought less Defeated and those still in the fight. In a day window a foe fought before the
            /// window and defeated in it can make Defeated larger than Fought: never below zero.</summary>
            public int GotAway => Math.Max(0, Fought - Defeated - Fighting);
            /// <summary>The "×N" of the Damage page: separate foes of the kind you fought (at least those defeated).</summary>
            public int Separate => Math.Max(Fought, Defeated);
            public bool Empty => Fought == 0 && Defeated == 0 && With == 0 && HitYou == 0 && Fighting == 0;
            public void Add(Count o) { Fought += o.Fought; Defeated += o.Defeated; With += o.With; HitYou += o.HitYou; Fighting += o.Fighting; }
        }

        public readonly Dictionary<string, Count> Kinds = new Dictionary<string, Count>(StringComparer.Ordinal);
        public bool Empty => Kinds.Values.All(c => c.Empty);

        /// <summary>The count of a kind, made when new (past MaxKinds: OtherKind's).</summary>
        public Count Of(string kind)
        {
            kind = string.IsNullOrEmpty(kind) ? "?" : kind;
            if (Kinds.TryGetValue(kind, out var c)) return c;
            if (Kinds.Count >= MaxKinds && kind != OtherKind) return Of(OtherKind);
            return Kinds[kind] = new Count();
        }

        /// <summary>A kind's count, or null when there is none.</summary>
        public Count Get(string kind) => kind != null && Kinds.TryGetValue(kind, out var c) ? c : null;

        public int Total(Func<Count, int> field) { int s = 0; foreach (var c in Kinds.Values) s += field(c); return s; }

        public void AddAll(FoeCounts o) { if (o == null) return; foreach (var kv in o.Kinds) Of(kv.Key).Add(kv.Value); }
        public static FoeCounts Sum(params FoeCounts[] parts) { var s = new FoeCounts(); foreach (var p in parts) s.AddAll(p); return s; }

        /// <summary>What <paramref name="now"/> holds beyond <paramref name="was"/> (null: nothing before), field by field, never below zero; Fighting
        /// is left out (it is not saved). Every saved field only grows within a session, so this is what one save added.</summary>
        public static FoeCounts Minus(FoeCounts now, FoeCounts was)
        {
            var d = new FoeCounts();
            if (now == null) return d;
            foreach (var kv in now.Kinds)
            {
                var w = was?.Get(kv.Key); var n = kv.Value;
                var c = new Count { Fought = Math.Max(0, n.Fought - (w?.Fought ?? 0)), Defeated = Math.Max(0, n.Defeated - (w?.Defeated ?? 0)), With = Math.Max(0, n.With - (w?.With ?? 0)), HitYou = Math.Max(0, n.HitYou - (w?.HitYou ?? 0)) };
                if (!c.Empty) d.Kinds[kv.Key] = c;
            }
            return d;
        }

        /// <summary>A copy with at most <paramref name="max"/> kinds: the most fought (then hit you) stay, the rest are summed under OtherKind.</summary>
        public FoeCounts Top(int max)
        {
            var t = new FoeCounts();
            var order = Kinds.Where(kv => !kv.Value.Empty).OrderByDescending(kv => kv.Value.Fought + kv.Value.HitYou).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            for (int i = 0; i < order.Count; i++) (i < max - 1 || order.Count <= max ? t.Of(order[i].Key) : t.OfOther()).Add(order[i].Value);
            return t;
        }
        Count OfOther() => Kinds.TryGetValue(OtherKind, out var c) ? c : Kinds[OtherKind] = new Count();

        /// <summary>The kinds as one JSON object under <paramref name="key"/>: "Troll":[fought,defeated,with,hitYou]. <paramref name="fighting"/> (the
        /// shared copy only, never the saved book): a kind with foes still in the fight gets them as a fifth number, so a fellow's book does not
        /// show them as got away (REVIEW-08 #8); an older reader reads the first four.</summary>
        public void WriteTo(Json j, string key, bool fighting = false)
        {
            var b = new System.Text.StringBuilder("{"); var first = true;
            foreach (var kv in Kinds.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var c = kv.Value; if (c.Fought == 0 && c.Defeated == 0 && c.With == 0 && c.HitYou == 0) continue;
                if (!first) b.Append(','); first = false;
                b.Append(Json.Q(kv.Key)).Append(":[").Append(c.Fought).Append(',').Append(c.Defeated).Append(',').Append(c.With).Append(',').Append(c.HitYou);
                if (fighting && c.Fighting > 0) b.Append(',').Append(c.Fighting);
                b.Append(']');
            }
            j.Raw(key, b.Append('}').ToString());
        }

        /// <summary>Reads what WriteTo wrote (the object itself; null: nothing), Fighting too where a shared copy has it. Unreadable entries are
        /// skipped; numbers are clipped to 0 .. 10^7.</summary>
        public FoeCounts ReadFrom(Dictionary<string, object> o)
        {
            if (o == null) return this;
            int N(List<object> a, int i) => i < a.Count && a[i] is double d && Json.IsFinite(d) ? (int)Math.Max(0, Math.Min(1e7, d)) : 0;
            foreach (var kv in o)
            {
                if (!(kv.Value is List<object> a) || string.IsNullOrEmpty(kv.Key)) continue;
                var c = new Count { Fought = N(a, 0), Defeated = N(a, 1), With = N(a, 2), HitYou = N(a, 3), Fighting = N(a, 4) };
                if (!c.Empty) Of(kv.Key).Add(c);
            }
            return this;
        }
    }

    /// <summary>
    /// The battle recorder of this session (0.8, work/hearthwoven-0.8/BACKLOG.md "Battle detail"): every foe you fought as its own creature
    /// (keyed by its network object, so a hundred hits on one troll are one troll), what became of it, and the feed of your fights.
    /// Read-only toward the world: BattleHooks feeds it from the game's own events, nothing here touches the game. Pure C#, unit-tested
    /// (test/BattleRecTests.cs).
    ///
    /// How the outcome is known (decompile, assembly_valheim Character.RPC_Damage / OnDeath, Game.RPC_RegisterKill):
    ///   - the PC that controls a creature marks every player who hit it on the creature's own network record ("Attackers": a count, and one
    ///     flag per player name); every PC near it can read that record (Attackers);
    ///   - when it dies, that PC sends the game's kill message to EVERY player who hit it, whoever landed the last blow, with the number of
    ///     players who hit it (Game.RPC_RegisterKill: the game's own kill counter counts it for each of them). That is Killed: one player =
    ///     "defeated by you", more = "defeated with" the fellow players the record names;
    ///   - the creature's network object is then destroyed on every PC (Destroyed). The kill message names the kind, not the creature, so a
    ///     kill is matched to the creature destroyed within MatchWindow (either order); a kill whose destroy never comes (you were far
    ///     away) goes to the kind's foe you fought last. A destroy without a kill within MatchWindow: the game removed it (Despawned);
    ///   - a fight ends when nothing hit or was hit for FightGap: the foes still standing "survived" (or "you left", when the creature is no
    ///     longer loaded on your PC). A foe that got away and is killed later (a fellow player finished it) still turns to defeated.
    /// Bounded: the feed keeps the last MaxFeed encounters, the creature table MaxInstances (past it the oldest leave the table, their counts
    /// kept for This session), the waiting kills and destroys MaxWaiting each. Nothing allocates per frame; a hit allocates only for a foe
    /// new to the fight.
    /// </summary>
    public sealed class BattleRecorder
    {
        public static readonly string[] Types = { "blunt", "slash", "pierce", "fire", "frost", "lightning", "poison", "spirit" };
        public const int MaxFeed = 200, MaxInstances = 5000, MaxWaiting = 32;
        public static readonly TimeSpan FightGap = TimeSpan.FromSeconds(60), MatchWindow = TimeSpan.FromSeconds(5);
        /// <summary>Who was near you in a fight (0.8, PICKS.md section 2): players within NearMetres, sampled every NearEvery seconds while a fight
        /// is on; a fellow is in the fight after NearSeconds near you. Kept for the last MaxNearFights fights, with them.</summary>
        public const float NearMetres = 40f, NearEvery = 2f, NearSeconds = 10f;
        /// <summary>Near-time counts only while hits are coming (REVIEW-08 #7): within this of the fight's last hit, not the whole FightGap after
        /// it (someone walking past a minute after the boar fell is not in the fight).</summary>
        public static readonly TimeSpan NearAfterHit = TimeSpan.FromSeconds(10);
        public const int MaxNearFights = 20, MaxNearNames = 32;
        /// <summary>
        /// 0.9 boss fights (work/hearthwoven-0.8.5/fights/DESIGN.md; the round 2 rules Joost picked). An attempt ends on the kill (Won), on the
        /// boss seen back at full health (Reset), or when this PC has not seen a boss of it for GotAwayAfter (GotAway: outcome not known); never on
        /// a quiet gap, so a death and the run back stay one attempt. BossGap only keeps the recorder's own fight open while the boss fight is
        /// active (a hit or a health drop within it), and lines up two players' records of a boss (GroupFights.Join). The arena: within
        /// ArenaMetres of a boss, or in its interior.
        /// </summary>
        public static readonly TimeSpan BossGap = TimeSpan.FromSeconds(120), GotAwayAfter = TimeSpan.FromMinutes(10);
        public const float ArenaMetres = 50f;
        /// <summary>0.9 dungeon runs: out of an interior for less than this and back into the same one goes on with the run.</summary>
        public static readonly TimeSpan RunBreak = TimeSpan.FromMinutes(5);
        /// <summary>0.9 falls: what hit you this long before a death is its "last seconds" (the game's own death log looks back as far).</summary>
        public static readonly TimeSpan FallWindow = TimeSpan.FromSeconds(10);
        public const int MaxBossFights = 20, MaxRuns = 20, MaxMembers = 1000, MaxFalls = 30;

        /// <summary>
        /// 0.8.5 (Joost, 11 Oct): Hildir's minibosses are boss fights whether or not the game flags them (Character.IsBoss): Brenna
        /// (Skeleton_Hildir), Geirrhafa (Fenring_Cultist_Hildir), Zil (GoblinShaman_Hildir) and Thungr (GoblinBruteBros), and the copies her raids
        /// send (the "_nochest" prefabs). Named by prefab and by name token (the game's, the same in every language), so the hooks can ask with
        /// whichever they have without making a string (BattleHooks.Watch has the token only). The prefab names: the game's own strings (the
        /// valheim-save-research prefab-hashlib.json beside the report); the tokens: the game's English names (reference/item-names.json,
        /// "enemy_skeletonfire" = "&lt;color=orange&gt;Brenna"). A name here that is no creature in the game is never asked for: harmless.
        /// </summary>
        static readonly HashSet<string> Minibosses = new HashSet<string>(StringComparer.Ordinal)
        {
            "Skeleton_Hildir", "Skeleton_Hildir_nochest", "Fenring_Cultist_Hildir", "Fenring_Cultist_Hildir_nochest",
            "GoblinShaman_Hildir", "GoblinShaman_Hildir_nochest", "GoblinBruteBros", "GoblinBruteBros_nochest", "GoblinBrute_Hildir",
            "$enemy_skeletonfire", "$enemy_fenringcultist_hildir", "$enemy_goblin_hildir", "$enemy_goblinbrute_hildir", "$enemy_goblinbrute_hildircombined",
        };

        /// <summary>Whether a creature is a boss for the boss fights: the game says so (<paramref name="flag"/>, Character.IsBoss), or it is one of
        /// Hildir's minibosses by its prefab or name token (Minibosses).</summary>
        public static bool IsBoss(bool flag, string kind, string token) =>
            flag || (kind != null && Minibosses.Contains(kind)) || (token != null && Minibosses.Contains(token));

        sealed class Inst
        {
            public FoeId Id; public string Kind, Token;
            public DateTime LastDealt = DateTime.MinValue, LastReceived = DateTime.MinValue;
            public bool YouHit, HitYou, Gone, Boss;
            public FoeOutcome Outcome; public AwayHow Away;
            public FoeEncounter Latest;      // its encounter in the feed (null once the feed dropped it)
            public int Attackers;            // the creature's record: how many players hit it
            public string[] Names = FoeEncounter.NoNames;   // and the fellow players it names
            public DateTime Last => LastDealt > LastReceived ? LastDealt : LastReceived;
            public bool Defeated() => Outcome == FoeOutcome.DefeatedByYou || Outcome == FoeOutcome.DefeatedWith;
        }
        struct Credit { public string Token; public int Attackers; public DateTime At; }
        struct GoneMark { public Inst Inst; public DateTime At; }

        readonly Dictionary<FoeId, Inst> instances = new Dictionary<FoeId, Inst>();
        readonly Queue<FoeEncounter> feed = new Queue<FoeEncounter>();
        readonly Dictionary<FoeId, FoeEncounter> open = new Dictionary<FoeId, FoeEncounter>();
        readonly List<Credit> credits = new List<Credit>();
        readonly List<GoneMark> gone = new List<GoneMark>();
        readonly FoeCounts evicted = new FoeCounts();   // the creatures that left the table (MaxInstances): This session keeps them
        readonly Dictionary<int, Dictionary<string, float>> near = new Dictionary<int, Dictionary<string, float>>();   // fight -> name -> seconds near you
        readonly Dictionary<int, int> deaths = new Dictionary<int, int>();   // fight -> your deaths in it (the last MaxNearFights fights)
        int fight, seq; bool fightOn; DateTime fightLast;
        // 0.9: boss fights (oldest first) and, for the open ones, their foes and each boss's last place and health; dungeon runs (oldest first),
        // the open or latest one and its foes. Places stay here: nothing below is ever in a FoeEncounter, the feed or the book.
        readonly List<BossFight> bossFights = new List<BossFight>();
        readonly Dictionary<int, HashSet<FoeId>> members = new Dictionary<int, HashSet<FoeId>>();
        readonly Dictionary<FoeId, Spot> bossAt = new Dictionary<FoeId, Spot>();
        readonly Dictionary<FoeId, float> bossHealth = new Dictionary<FoeId, float>();
        readonly List<DungeonRun> runs = new List<DungeonRun>();
        readonly HashSet<FoeId> runFoes = new HashSet<FoeId>();
        DungeonRun run; DateTime insideAt; int bossSeq, runSeq;
        // 0.9 falls: your deaths (oldest first), and the foes' hits on you in a small ring (no allocation per hit) for each one's last seconds
        readonly List<Fall> falls = new List<Fall>();
        const int Ring = 32;
        readonly DateTime[] ringAt = new DateTime[Ring]; readonly string[] ringKind = new string[Ring]; readonly bool[] ringBoss = new bool[Ring];
        readonly float[] ringTypes = new float[Ring * 8]; int ringNext;

        /// <summary>The session's first record (0.8.2: its first hit; 0.9: or entering a dungeon, or a boss fight seen): the shared feed's times are
        /// whole seconds from it (FeedShare.T0); MinValue before any.</summary>
        public DateTime FirstUtc { get; private set; } = DateTime.MinValue;

        /// <summary>A fight is on (its last hit less than FightGap ago, as of the last report): BattleHooks samples who is near only then.</summary>
        public bool FightOn => fightOn;

        /// <summary>Whether a creature is still loaded on this PC (the game: ZNetScene.FindInstance), asked when a fight ends; null (the tests):
        /// every foe counts as still there.</summary>
        public Func<FoeId, bool> Loaded;
        /// <summary>Told when a foe's outcome is settled, with how it was known (Dev.SelfCheck's in-game check: BattleHooks); null: nothing is said.</summary>
        public Action<FoeEncounter, string> Settled;
        /// <summary>Changes with every change the recorder makes: the panel rebuilds only when it did.</summary>
        public int Version { get; private set; }
        /// <summary>Kills the game counted for you that matched no foe you fought here (a hit this PC did not see): counted, never shown.</summary>
        public int UnmatchedKills { get; private set; }
        public int Instances => instances.Count;
        public int FeedCount => feed.Count;
        /// <summary>REVIEW-08 #9: the foes of this session's feed the book no longer keeps (it keeps the newest MaxFeed): those the ring dropped,
        /// and the oldest fight's rest when the ring cut into it (Fights leaves such a fight out rather than show it with part of its foes).</summary>
        public int FeedLeftOut { get { Fights(); return feedLeftOut; } }
        int feedDropped, droppedFight = -1, feedLeftOut;

        // ---------- what the hooks report ----------

        /// <summary>Your hit on a foe (before its resistances, as the Damage page). <paramref name="token"/> is its name token (the kill message's).
        /// 0.9: <paramref name="boss"/>, the game says it is a boss (Character.IsBoss); <paramref name="at"/>, where it stood (the arena rule, this PC only).</summary>
        public void Dealt(DateTime utc, FoeId id, string kind, string token, string biome, HitData.DamageTypes d, bool boss = false, Spot at = default)
        {
            if (id.IsNone || Sum(d) <= 0f) return;
            var (e, inst) = Touch(utc, id, kind, token, biome, boss, at);
            AddTypes(e.Dealt, d); e.HitsDealt++;
            inst.YouHit = true; inst.LastDealt = utc;
        }

        /// <summary>A foe's hit on you, after your armour (as the Defence page's Received from). 0.9: <paramref name="boss"/> and <paramref name="at"/> as Dealt.</summary>
        public void Received(DateTime utc, FoeId id, string kind, string token, string biome, HitData.DamageTypes d, bool boss = false, Spot at = default)
        {
            if (id.IsNone || Sum(d) <= 0f) return;
            var (e, inst) = Touch(utc, id, kind, token, biome, boss, at);
            AddTypes(e.Received, d); e.HitsReceived++;
            var r = ringNext; ringNext = (ringNext + 1) % Ring;   // 0.9: for a fall's last seconds
            ringAt[r] = utc; ringKind[r] = inst.Kind; ringBoss[r] = inst.Boss;
            for (int t = 0; t < 8; t++) ringTypes[r * 8 + t] = 0f;
            AddTypes(ringTypes, d, r * 8);
            inst.HitYou = true; inst.LastReceived = utc;
        }

        /// <summary>What the creature's own record says about who hit it: the number of players, and the fellow players it names (not you).
        /// Only for a creature already in the table.</summary>
        public void Attackers(FoeId id, int count, string[] names)
        {
            if (!instances.TryGetValue(id, out var inst)) return;
            bool more = count > inst.Attackers, named = names != null && names.Length > inst.Names.Length;   // the record only grows: an older copy changes nothing
            if (!more && !named) return;
            if (more) inst.Attackers = count;
            if (named) inst.Names = names;
            Version++;
        }

        /// <summary>How many players the creature's record said hit it, as last seen (0: not known or not in the table).</summary>
        public int AttackersSeen(FoeId id) => instances.TryGetValue(id, out var inst) ? inst.Attackers : 0;

        /// <summary>Whether the recorder knows this creature (the destroy hook asks first: every destroyed object in the world passes there).</summary>
        public bool Knows(FoeId id) => instances.ContainsKey(id) || bossAt.ContainsKey(id);

        /// <summary>The game's kill message for you (Game.RPC_RegisterKill): a foe of this name token died that you hit; <paramref name="attackers"/>
        /// players hit it.</summary>
        public void Killed(DateTime utc, string token, int attackers)
        {
            Advance(utc);
            int best = -1;
            for (int i = 0; i < gone.Count; i++)
                if (gone[i].Inst.Token == token && utc - gone[i].At <= MatchWindow && (best < 0 || (gone[i].Inst.Attackers == attackers && gone[best].Inst.Attackers != attackers))) best = i;
            if (best >= 0) { var g = gone[best]; gone.RemoveAt(best); Resolve(g.Inst, attackers, "its destroy came first", utc); return; }
            if (credits.Count >= MaxWaiting) { Fallback(credits[0]); credits.RemoveAt(0); }
            credits.Add(new Credit { Token = token, Attackers = attackers, At = utc });
            Version++;
        }

        /// <summary>A creature's network object was destroyed (it died, or the game removed it). Only acts on a creature in the table.</summary>
        public void Destroyed(DateTime utc, FoeId id)
        {
            if (!instances.TryGetValue(id, out var inst))
            {
                // 0.9: a boss you never hit (its fight seen by its health) gets no kill message here; a boss's network object is only ever destroyed
                // when it dies (bosses never get the despawn flags, SpawnSystem only sets them on world spawns), so its boss fight is won
                if (bossAt.ContainsKey(id)) { Advance(utc); BossDown(id, utc); }
                return;
            }
            if (inst.Gone) return;
            Advance(utc);
            inst.Gone = true;
            if (inst.Defeated()) return;   // the kill came first and went to it already
            int best = -1;
            for (int i = 0; i < credits.Count; i++)
                if (credits[i].Token == inst.Token && utc - credits[i].At <= MatchWindow && (best < 0 || (credits[i].Attackers == inst.Attackers && credits[best].Attackers != inst.Attackers))) best = i;
            if (best >= 0) { var c = credits[best]; credits.RemoveAt(best); Resolve(inst, c.Attackers, "matched to its destroy", utc); return; }
            if (gone.Count >= MaxWaiting) { Despawn(gone[0].Inst, gone[0].At); gone.RemoveAt(0); }
            gone.Add(new GoneMark { Inst = inst, At = utc });
            Version++;
        }

        /// <summary>
        /// A player near you while a fight is on (BattleHooks, every NearEvery seconds: Player.GetAllPlayers within NearMetres, you left out):
        /// <paramref name="seconds"/> more near you in the running fight. Nothing when no fight is on, or no hit came for NearAfterHit. Bounded: MaxNearNames names per fight, the
        /// last MaxNearFights fights. The page changes only when a player reaches NearSeconds (Version), not with every sample.
        /// </summary>
        public void Near(DateTime utc, string name, float seconds)
        {
            Advance(utc);
            if (!fightOn || string.IsNullOrEmpty(name) || !(seconds > 0f) || !Json.IsFinite(seconds)) return;
            if (utc - fightLast > NearAfterHit) return;   // the fight has gone quiet: the minute before it ends credits nobody
            if (!near.TryGetValue(fight, out var names))
            {
                if (near.Count >= MaxNearFights) { var oldest = int.MaxValue; foreach (var k in near.Keys) if (k < oldest) oldest = k; near.Remove(oldest); }
                near[fight] = names = new Dictionary<string, float>(StringComparer.Ordinal);
                Version++;   // once per fight: the fight's record now holds it
            }
            names.TryGetValue(name, out var was);
            if (was == 0f && names.Count >= MaxNearNames) return;
            names[name] = was + Math.Min(seconds, 2 * NearEvery);   // a late sample never counts for more than two
            if (was < NearSeconds && names[name] >= NearSeconds) Version++;   // the player is in the fight now: the page shows it
        }

        /// <summary>
        /// 0.9: a boss loaded on your PC, looked at once a second (BattleHooks: Character.GetAllCharacters, IsBoss): its health left (0..1, the game's
        /// GetHealthPercentage, read from the creature's network record on any PC) and where it stands. A drop since the last look means a player hit
        /// it: its boss fight goes on, or starts, even before you hit it yourself (you fight the adds while a fellow fights the boss). The first look
        /// at a boss starts nothing, whatever its health.
        /// </summary>
        public void BossSeen(DateTime utc, FoeId id, string kind, string token, float health, Spot at)
        {
            if (id.IsNone || !Json.IsFinite(health)) return;
            Advance(utc);
            health = Math.Max(0f, Math.Min(1f, health));
            var had = bossHealth.TryGetValue(id, out var before);
            if (!had && bossHealth.Count >= 4 * BossFight.MaxBosses) bossHealth.Clear();   // bounded: bosses are few; a stale one is looked at again
            bossHealth[id] = health;
            var drop = had && health < before;
            var bf = OpenBossOf(id);
            if (bf == null)
            {
                if (health >= 0.999f) { var gone = LatestBossOf(id); if (gone != null && gone.Outcome == BossOutcome.GotAway) { gone.Outcome = BossOutcome.Reset; gone.OutcomeUtc = utc; Version++; } }   // learned later: it reset
                if (!drop) return;   // no fight seen yet: only a hit (a drop) starts one
                if (at.Known) bossAt[id] = at;
                bf = OpenBossNear(at) ?? StartBoss(utc, id, kind, token);
                if (!bf.Bosses.Contains(id) && bf.Bosses.Count < BossFight.MaxBosses) bf.Bosses.Add(id);
                Member(bf, id);
            }
            if (at.Known) bossAt[id] = at;
            bf.LastSeenUtc = utc;
            if (bf.Bosses.Count > 0 && bf.Bosses[0].Equals(id)) SeeHealth(bf, utc, health);
            if (drop) Busy(bf, utc);
            // the game has no reset: a boss heals to full over world time (BaseAI.UpdateRegeneration). Seen full after it was hit, with no fighting
            // for a few seconds (never a race with the hit that starts it): reset, "at" the lowest it reached
            if (health >= 0.999f && bf.Lowest >= 0f && bf.Lowest < 0.999f && utc - bf.EndUtc >= TimeSpan.FromSeconds(10)) { bf.Outcome = BossOutcome.Reset; bf.OutcomeUtc = utc; bf.HealthMinuteAgo = -1f; CloseBoss(bf); }
        }

        /// <summary>0.9: you died (ClientHooks' death hook): one more death in the running fight, every open boss fight and the run you are in.</summary>
        public void Died(DateTime utc)
        {
            Advance(utc);
            var fall = new Fall { Utc = utc, Fight = fightOn ? fight : 0, Run = run != null && run.Open ? run.Number : 0 };
            if (fightOn)
            {
                if (!deaths.ContainsKey(fight) && deaths.Count >= MaxNearFights) { var oldest = int.MaxValue; foreach (var k in deaths.Keys) if (k < oldest) oldest = k; deaths.Remove(oldest); }
                deaths.TryGetValue(fight, out var n); deaths[fight] = n + 1;
            }
            foreach (var b in bossFights) if (b.Open) { b.Deaths++; if (fall.Boss == 0) fall.Boss = b.Number; }
            if (run != null && run.Open) run.Deaths++;
            // its last seconds: the foes' hits in the ring within FallWindow, the newest naming what took you down
            DateTime latest = DateTime.MinValue;
            for (int i = 0; i < Ring; i++)
            {
                if (ringKind[i] == null || ringAt[i] > utc || utc - ringAt[i] > FallWindow) continue;
                for (int t = 0; t < fall.Received.Length; t++) fall.Received[t] += ringTypes[i * 8 + t];
                if (ringAt[i] >= latest) { latest = ringAt[i]; fall.By = ringKind[i]; fall.ByBoss = ringBoss[i]; }
            }
            falls.Add(fall); if (falls.Count > MaxFalls) falls.RemoveAt(0);
            Version++;
        }

        /// <summary>0.9: you blocked a foe's hit and the block held (FeatsHooks' block hook), a parry by the game's rule or not: counted on its
        /// encounter in the running fight (a foe you have not met in this fight yet is not counted).</summary>
        public void Blocked(DateTime utc, FoeId id, bool parry)
        {
            if (id.IsNone || !open.TryGetValue(id, out var e)) return;
            e.Blocks++; if (parry) e.Parries++;
            Version++;
        }

        /// <summary>
        /// 0.9 dungeon runs: where you are, once a second (BattleHooks): <paramref name="kind"/> the interior's place kind when you are inside one
        /// (null: outside), <paramref name="place"/> its zone (Spot.Zone, this PC only). Entering starts a run (or goes on with the last one, back
        /// within RunBreak); leaving ends it; another interior ends one and starts the next.
        /// </summary>
        public void Inside(DateTime utc, string kind, long place, string token = null)
        {
            bool inNow = !string.IsNullOrEmpty(kind);
            if (run != null && run.Open)
            {
                if (inNow && run.Place == place)
                {
                    var step = (utc - insideAt).TotalSeconds;
                    if (step > 0) run.Inside += (float)Math.Min(step, 2.0);   // sampled once a second: a stall never counts for more
                    insideAt = utc; if (utc > run.EndUtc) run.EndUtc = utc;
                    return;
                }
                run.Open = false; run.EndUtc = utc; Version++;   // out (or into another interior: a new run below)
            }
            if (!inNow) return;
            if (FirstUtc == DateTime.MinValue) FirstUtc = utc;
            insideAt = utc;
            if (run != null && run.Place == place && utc - run.EndUtc <= RunBreak) { run.Open = true; run.EndUtc = utc; Version++; return; }   // back in after a short step out
            run = new DungeonRun { Number = ++runSeq, Kind = kind, Token = string.IsNullOrEmpty(token) ? null : token, Place = place, StartUtc = utc, EndUtc = utc, Open = true };
            runs.Add(run); if (runs.Count > MaxRuns) runs.RemoveAt(0);
            runFoes.Clear();
            Version++;
        }

        /// <summary>0.9: this session's boss fights, newest first (at most MaxBossFights). Read only.</summary>
        public List<BossFight> BossFights() { var l = new List<BossFight>(bossFights); l.Reverse(); return l; }
        /// <summary>0.9: this session's dungeon runs, newest first (at most MaxRuns). Read only.</summary>
        public List<DungeonRun> Runs() { var l = new List<DungeonRun>(runs); l.Reverse(); return l; }
        /// <summary>0.9: this session's falls (your deaths), newest first (at most MaxFalls). Read only.</summary>
        public List<Fall> Falls() { var l = new List<Fall>(falls); l.Reverse(); return l; }
        /// <summary>0.9: the run you are in now, or null.</summary>
        public DungeonRun CurrentRun => run != null && run.Open ? run : null;
        public int BossFightCount => bossFights.Count;

        /// <summary>Once a second from the game (and before the panel reads): ends a fight after FightGap, settles the kills and destroys that
        /// waited longer than MatchWindow.</summary>
        public void Tick(DateTime utc)
        {
            Advance(utc);
            for (int i = credits.Count - 1; i >= 0; i--) if (utc - credits[i].At > MatchWindow) { var c = credits[i]; credits.RemoveAt(i); Fallback(c); }
            for (int i = gone.Count - 1; i >= 0; i--) if (utc - gone[i].At > MatchWindow) { var g = gone[i]; gone.RemoveAt(i); Despawn(g.Inst, g.At); }
        }

        // ---------- what the panel reads ----------

        /// <summary>The fights of the feed, newest first, each with its foes newest first. The oldest fight is left out when the ring dropped some
        /// of its foes and a newer fight is there (FeedLeftOut counts them). Built again only when the recorder changed (Version); the lists are
        /// shared: read only.</summary>
        public List<FoeFight> Fights()
        {
            if (fightsAt == Version && fights != null) return fights;
            var list = new List<FoeFight>(); FoeFight cur = null;
            foreach (var e in feed)   // oldest first: an encounter joins the feed when its foe is first met in a fight, so fights come in order
            {
                if (cur == null || cur.Number != e.Fight) { cur = new FoeFight { Number = e.Fight, StartUtc = e.StartUtc, EndUtc = e.EndUtc, Open = fightOn && e.Fight == fight, Near = near.TryGetValue(e.Fight, out var nr) ? nr : null, Deaths = deaths.TryGetValue(e.Fight, out var dd) ? dd : 0 }; list.Add(cur); }
                cur.Entries.Add(e); cur.Blocks += e.Blocks; cur.Parries += e.Parries;
                if (e.StartUtc < cur.StartUtc) cur.StartUtc = e.StartUtc;
                if (e.EndUtc > cur.EndUtc) cur.EndUtc = e.EndUtc;
                for (int t = 0; t < Types.Length; t++) { cur.Dealt[t] += e.Dealt[t]; cur.Received[t] += e.Received[t]; }
            }
            feedLeftOut = feedDropped;
            if (list.Count > 1 && list[0].Number == droppedFight) { feedLeftOut += list[0].Entries.Count; list.RemoveAt(0); }   // a fight with part of its foes: not shown as if whole
            foreach (var f in list) f.Entries.Sort((a, b) => b.EndUtc != a.EndUtc ? b.EndUtc.CompareTo(a.EndUtc) : b.StartUtc.CompareTo(a.StartUtc));
            list.Reverse();
            fights = list; fightsAt = Version;
            return list;
        }
        List<FoeFight> fights; int fightsAt = -1;

        /// <summary>
        /// The foes you fought per kind from <paramref name="fromUtc"/> on (null: the whole session). A foe counts in a window when you hit it in it
        /// (by the minute of your last hit, as the damage buckets: a foe whose damage shows always counts), HitYou when it hit you in it; its
        /// outcome is what became of it. The session's counts are kept until the recorder changes.
        /// </summary>
        public FoeCounts Counts(DateTime? fromUtc)
        {
            if (!fromUtc.HasValue && sessionAt == Version && session != null) return session;
            var c = new FoeCounts();
            foreach (var inst in instances.Values)
            {
                bool fought = inst.YouHit && In(inst.LastDealt, fromUtc), hitYou = inst.HitYou && In(inst.LastReceived, fromUtc);
                if (!fought && !hitYou) continue;
                var k = c.Of(inst.Kind);
                if (hitYou) k.HitYou++;
                if (!fought) continue;
                k.Fought++;
                if (inst.Defeated()) { k.Defeated++; if (inst.Outcome == FoeOutcome.DefeatedWith) k.With++; }
                else if (inst.Outcome == FoeOutcome.Fighting) k.Fighting++;
            }
            if (!fromUtc.HasValue) { c.AddAll(evicted); session = c; sessionAt = Version; }
            return c;
        }
        FoeCounts session; int sessionAt = -1;

        static bool In(DateTime t, DateTime? from)
        {
            if (!from.HasValue) return t > DateTime.MinValue;
            var minute = new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerMinute, t.Kind);
            return minute.AddMinutes(1) > from.Value;
        }

        // ---------- inside ----------

        (FoeEncounter, Inst) Touch(DateTime utc, FoeId id, string kind, string token, string biome, bool boss = false, Spot at = default)
        {
            Advance(utc);
            if (!fightOn) { fight++; fightOn = true; }
            if (FirstUtc == DateTime.MinValue) FirstUtc = utc;
            fightLast = utc;
            if (!instances.TryGetValue(id, out var inst))
            {
                if (instances.Count >= MaxInstances) Evict();
                instances[id] = inst = new Inst { Id = id, Kind = kind ?? "?", Token = token ?? "" };
            }
            if (IsBoss(boss, kind, token)) inst.Boss = true;   // 0.8.5: Hildir's minibosses too, flagged or not
            if (!open.TryGetValue(id, out var e))
            {
                e = new FoeEncounter { Id = id, Kind = inst.Kind, Token = inst.Token, Biome = biome ?? "None", StartUtc = utc, EndUtc = utc, Fight = fight, Seq = ++seq };
                if (inst.Defeated())   // a late hit on a foe already down (an arrow in flight): it stays defeated
                {
                    e.Outcome = inst.Outcome;
                    if (inst.Outcome == FoeOutcome.DefeatedWith) { e.With = inst.Names; e.Others = Math.Max(0, inst.Attackers - 1 - inst.Names.Length); }
                }
                else { inst.Outcome = FoeOutcome.Fighting; inst.Away = AwayHow.None; }
                open[id] = e;
                feed.Enqueue(e);
                if (feed.Count > MaxFeed) { var old = feed.Dequeue(); feedDropped++; droppedFight = old.Fight; if (instances.TryGetValue(old.Id, out var oi) && oi.Latest == old) oi.Latest = null; }
                inst.Latest = e;
            }
            e.EndUtc = utc;
            e.IsBoss = inst.Boss;
            Arena(utc, inst, e, at);
            if (run != null && run.Open)   // 0.9: fought in the dungeon you are in, and on its time line
            {
                if (e.Run == 0) e.Run = run.Number;
                if (runFoes.Count < MaxMembers && runFoes.Add(id)) run.Foes++;
                var span = run.Spans.Count > 0 ? run.Spans[run.Spans.Count - 1] : null;
                if (span == null || span.Fight != fight) { run.Spans.Add(span = new RunSpan { Fight = fight, StartUtc = utc, EndUtc = utc }); if (run.Spans.Count > DungeonRun.MaxSpans) run.Spans.RemoveAt(0); }
                span.EndUtc = utc; span.Boss |= e.Boss > 0;
            }
            Version++;
            return (e, inst);
        }

        // ---------- 0.9: boss fights (BossFight; the rules: BossGap, ArenaMetres) ----------

        /// <summary>A boss hit either way starts or goes on with its boss fight; any other foe hit where a boss of an open one stands (the arena
        /// rule) joins it. Positions are compared here and dropped: the encounter keeps only the boss fight's number.</summary>
        void Arena(DateTime utc, Inst inst, FoeEncounter e, Spot at)
        {
            if (inst.Boss)
            {
                if (at.Known) bossAt[inst.Id] = at;
                var bf = OpenBossOf(inst.Id);
                if (bf == null)
                {
                    bf = OpenBossNear(at) ?? StartBoss(utc, inst.Id, inst.Kind, inst.Token);   // Zil and Thungr: the second joins the first's fight
                    if (!bf.Bosses.Contains(inst.Id) && bf.Bosses.Count < BossFight.MaxBosses) bf.Bosses.Add(inst.Id);
                }
                e.Boss = bf.Number; Member(bf, inst.Id); Busy(bf, utc); bf.LastSeenUtc = utc;
                return;
            }
            if (e.Boss != 0 || !at.Known) return;
            var near = OpenBossNear(at);
            if (near != null) { e.Boss = near.Number; Member(near, inst.Id); }
        }

        BossFight OpenBossOf(FoeId id) { foreach (var b in bossFights) if (b.Open && b.Bosses.Contains(id)) return b; return null; }

        // the first boss's health as seen now: the newest, the lowest, and every 5 s into a short history for the health a minute ago
        void SeeHealth(BossFight bf, DateTime utc, float health)
        {
            if (Math.Abs(bf.Health - health) >= 0.01f) Version++;
            bf.Health = health;
            if (bf.Lowest < 0f || health < bf.Lowest) bf.Lowest = health;
            int n = bf.HistN, cap = bf.HistAt.Length;
            if (n > 0 && utc - bf.HistAt[(n - 1) % cap] < TimeSpan.FromSeconds(5)) return;
            bf.HistAt[n % cap] = utc; bf.HistHp[n % cap] = health; bf.HistN = n + 1;
            float ago = -1f; double best = double.MaxValue;
            for (int i = Math.Max(0, bf.HistN - cap); i < bf.HistN; i++)
            {
                var off = Math.Abs((utc - bf.HistAt[i % cap]).TotalSeconds - 60);
                if (off < best && off <= 10) { best = off; ago = bf.HistHp[i % cap]; }
            }
            bf.HealthMinuteAgo = ago;
        }
        BossFight LatestBossOf(FoeId id) { for (int i = bossFights.Count - 1; i >= 0; i--) if (bossFights[i].Bosses.Contains(id)) return bossFights[i]; return null; }

        // the open boss fight with a boss whose last known place is near this one (the same interior, or within ArenaMetres)
        BossFight OpenBossNear(Spot at)
        {
            if (!at.Known) return null;
            foreach (var b in bossFights)
                if (b.Open) foreach (var id in b.Bosses) if (bossAt.TryGetValue(id, out var s) && s.Near(at, ArenaMetres)) return b;
            return null;
        }

        BossFight StartBoss(DateTime utc, FoeId id, string kind, string token)
        {
            if (FirstUtc == DateTime.MinValue) FirstUtc = utc;
            var bf = new BossFight { Number = ++bossSeq, Fight = fightOn ? fight : 0, LastFight = fightOn ? fight : 0, Kind = kind ?? "?", Token = token ?? "", StartUtc = utc, EndUtc = utc, LastSeenUtc = utc };
            bf.Bosses.Add(id);
            if (bossHealth.TryGetValue(id, out var hp)) { bf.Health = hp; bf.Lowest = hp; }
            if (run != null && run.Open) { bf.Run = run.Number; run.Boss = bf.Number; }
            bossFights.Add(bf);
            if (bossFights.Count > MaxBossFights) { members.Remove(bossFights[0].Number); bossFights.RemoveAt(0); }
            members[bf.Number] = new HashSet<FoeId>();
            Version++;
            return bf;
        }

        void Member(BossFight bf, FoeId id)
        {
            if (members.TryGetValue(bf.Number, out var m) && m.Count < MaxMembers && m.Add(id)) { bf.Foes = m.Count; Version++; }
            if (bf.Fight == 0 && fightOn) bf.Fight = fight;   // seen before your first hit: it is part of the fight that hit started
            if (fightOn) bf.LastFight = fight;
        }

        // a sign of fighting in a boss fight: it goes on, and so does the recorder's fight it is part of
        void Busy(BossFight bf, DateTime utc)
        {
            if (utc > bf.EndUtc) bf.EndUtc = utc;
            if (fightOn && utc > fightLast) fightLast = utc;
        }

        /// <summary>A boss died (its kill, or its destroy: see Destroyed): its boss fight is won once every boss in it is down. A boss fight that went
        /// quiet (Lost) less than BossGap before turns to won: the group finished it while you were away.</summary>
        void BossDown(FoeId id, DateTime utc)
        {
            bossHealth[id] = 0f;
            BossFight bf = null;
            var latest = LatestBossOf(id);   // open, or ended with the outcome not known: the kill tells it now (the group won while you were away)
            if (latest != null && (latest.Open || latest.Outcome == BossOutcome.GotAway)) bf = latest;
            if (bf == null) return;
            bool all = true;
            foreach (var b in bf.Bosses) if (!b.Equals(id) && !(bossHealth.TryGetValue(b, out var h) && h <= 0f) && !(instances.TryGetValue(b, out var bi) && bi.Defeated())) all = false;
            if (bf.Bosses[0].Equals(id)) { bf.Health = 0f; bf.Lowest = 0f; }
            if (!all) { Busy(bf, utc); Version++; return; }
            bf.Outcome = BossOutcome.Won; bf.Health = 0f; bf.Lowest = 0f; bf.HealthMinuteAgo = -1f; bf.OutcomeUtc = utc;
            if (utc > bf.EndUtc) bf.EndUtc = utc;
            CloseBoss(bf);
        }

        void CloseBoss(BossFight bf)
        {
            members.Remove(bf.Number);
            foreach (var id in bf.Bosses) if (OpenBossOf(id) == null) bossAt.Remove(id);
            Version++;
        }

        /// <summary>Ends the fight when its last hit is more than FightGap before <paramref name="utc"/>: every foe still standing got away. 0.9: a
        /// boss fight this PC has not seen for GotAwayAfter ends first (GotAway); while one in this fight is active (BossGap) the fight stays open.</summary>
        void Advance(DateTime utc)
        {
            for (int i = 0; i < bossFights.Count; i++)
                if (bossFights[i].Open && utc - bossFights[i].LastSeenUtc > GotAwayAfter) { bossFights[i].Outcome = BossOutcome.GotAway; bossFights[i].OutcomeUtc = utc; bossFights[i].HealthMinuteAgo = -1f; CloseBoss(bossFights[i]); }
            if (!fightOn || utc - fightLast <= FightGap) return;
            foreach (var b in bossFights) if (b.Open && b.LastFight == fight && utc - b.EndUtc <= BossGap) return;   // the boss fight is active: so is the fight
            fightOn = false;
            foreach (var e in open.Values)
            {
                if (e.Outcome != FoeOutcome.Fighting) continue;
                var away = Loaded == null || SafeLoaded(e.Id) ? AwayHow.Survived : AwayHow.YouLeft;
                e.Outcome = FoeOutcome.GotAway; e.Away = away;
                Settled?.Invoke(e, away == AwayHow.YouLeft ? "the fight ended and it is no longer loaded here" : "the fight ended (a minute without hits) and it still stands");
                if (instances.TryGetValue(e.Id, out var inst) && inst.Outcome == FoeOutcome.Fighting) { inst.Outcome = FoeOutcome.GotAway; inst.Away = away; }
            }
            open.Clear();
            Version++;
        }
        bool SafeLoaded(FoeId id) { try { return Loaded(id); } catch (Exception) { return true; } }

        void Resolve(Inst inst, int attackers, string how, DateTime utc)
        {
            inst.Attackers = Math.Max(inst.Attackers, attackers);
            inst.Outcome = attackers <= 1 ? FoeOutcome.DefeatedByYou : FoeOutcome.DefeatedWith; inst.Away = AwayHow.None;
            var e = inst.Latest;
            if (e != null)
            {
                e.Outcome = inst.Outcome; e.Away = AwayHow.None;
                e.With = inst.Outcome == FoeOutcome.DefeatedWith ? inst.Names : FoeEncounter.NoNames;
                e.Others = inst.Outcome == FoeOutcome.DefeatedWith ? Math.Max(0, attackers - 1 - e.With.Length) : 0;
                if (Settled != null) Settled(e, "the game's kill message (" + attackers + (attackers == 1 ? " player" : " players") + " hit it), " + how);
            }
            if (inst.Boss) BossDown(inst.Id, utc);
            Version++;
        }

        /// <summary>A kill whose destroy never came within MatchWindow (you were far): the kind's foe you hit last that is not down.</summary>
        void Fallback(Credit c)
        {
            Inst best = null;
            foreach (var inst in instances.Values)
                if (inst.Token == c.Token && inst.YouHit && !inst.Defeated() && (best == null || inst.LastDealt > best.LastDealt)) best = inst;
            if (best != null) Resolve(best, c.Attackers, "no destroy within 5 s: given to the kind's foe you hit last", c.At); else { UnmatchedKills++; Version++; }
        }

        void Despawn(Inst inst, DateTime utc)
        {
            if (inst.Defeated()) return;
            if (inst.Boss) BossDown(inst.Id, utc);   // 0.9: a boss is only ever destroyed when it dies (see Destroyed), kill message or not
            inst.Outcome = FoeOutcome.GotAway; inst.Away = AwayHow.Despawned;
            if (inst.Latest != null) { inst.Latest.Outcome = FoeOutcome.GotAway; inst.Latest.Away = AwayHow.Despawned; Settled?.Invoke(inst.Latest, "destroyed with no kill message within 5 s: the game removed it"); }
            open.Remove(inst.Id);
            Version++;
        }

        /// <summary>The table is full: the creature with the oldest last hit outside the running fight leaves it; its counts stay in This session.</summary>
        void Evict()
        {
            Inst oldest = null;
            foreach (var inst in instances.Values) if (!open.ContainsKey(inst.Id) && (oldest == null || inst.Last < oldest.Last)) oldest = inst;
            if (oldest == null) return;
            var k = evicted.Of(oldest.Kind);
            if (oldest.HitYou) k.HitYou++;
            if (oldest.YouHit) { k.Fought++; if (oldest.Defeated()) { k.Defeated++; if (oldest.Outcome == FoeOutcome.DefeatedWith) k.With++; } }
            instances.Remove(oldest.Id);
            if (oldest.Latest != null) oldest.Latest = null;
        }

        static float Sum(HitData.DamageTypes d)
        {
            float s = 0f;
            void A(float v) { if (v > 0f && Json.IsFinite(v)) s += v; }
            A(d.m_blunt); A(d.m_slash); A(d.m_pierce); A(d.m_fire); A(d.m_frost); A(d.m_lightning); A(d.m_poison); A(d.m_spirit);
            return s;
        }

        static void AddTypes(float[] into, HitData.DamageTypes d, int at = 0)
        {
            void A(int i, float v) { if (v > 0f && Json.IsFinite(v)) into[at + i] += v; }
            A(0, d.m_blunt); A(1, d.m_slash); A(2, d.m_pierce); A(3, d.m_fire); A(4, d.m_frost); A(5, d.m_lightning); A(6, d.m_poison); A(7, d.m_spirit);
        }
    }

    /// <summary>
    /// The foes you fought, kept on this PC (LocalTotals top-level "foes", schema 5), the way ArmourBook keeps the armour ledger: when it began
    /// (From: the first run of a version that records it, the "Recorded from" date), the earlier sessions folded, the latest saved session
    /// under its own id, and its own day rows (outside "history", which 0.6.1 and 0.6.2 rewrite with only the fields they know), folded to weeks
    /// and months by DayHistory's rule. A foe counts on the day of the save that first saw it; one fought in two sessions counts in each.
    /// JSON: {"from":"ISO","previous":{"Troll":[f,d,w,h]},"lastSession":{"id":"..","kinds":{..}},"days":[{"p":"2026-10-10","kinds":{..}}]}.
    /// </summary>
    public sealed class FoeBook
    {
        public const int MaxRowKinds = 200;
        public DateTime FromUtc = DateTime.MinValue;
        public FoeCounts Previous = new FoeCounts();
        public string LastSession = "";
        public FoeCounts Last = new FoeCounts();
        /// <summary>Oldest first: "before", months, weeks, days (DayHistory's period keys).</summary>
        public readonly List<(string Period, FoeCounts Counts)> Days = new List<(string, FoeCounts)>();

        public bool IsEmpty => FromUtc == DateTime.MinValue && Previous.Empty && Last.Empty && LastSession.Length == 0 && Days.Count == 0;

        /// <summary>Everything saved before the session <paramref name="current"/>.</summary>
        public FoeCounts BeforeSession(string current) => FoeCounts.Sum(Previous, LastSession == current ? null : Last);

        /// <summary>What the running session holds beyond its last save: the live part of today.</summary>
        public FoeCounts Pending(string session, FoeCounts now) => FoeCounts.Minus(now, !string.IsNullOrEmpty(session) && session == LastSession ? Last : null);

        /// <summary>Records a session's counts as they are now: the difference to its last save goes to the day of <paramref name="localNow"/>; a new
        /// session id first folds the latest saved one into Previous.</summary>
        public void Record(string session, FoeCounts now, DateTime localNow)
        {
            if (string.IsNullOrEmpty(session) || now == null) return;
            var same = session == LastSession;
            var diff = FoeCounts.Minus(now, same ? Last : null);
            if (!diff.Empty) AddDay(localNow.Date, diff);
            if (!same) { if (LastSession.Length > 0) Previous.AddAll(Last); LastSession = session; }
            Last = FoeCounts.Minus(now, null);   // a copy without Fighting
        }

        void AddDay(DateTime day, FoeCounts diff)
        {
            var key = DayHistory.DayKey(day);
            var last = Days.Count > 0 ? Days[Days.Count - 1] : default;
            FoeCounts row;
            if (Days.Count > 0 && (last.Period == key || DayHistory.StartOf(last.Period) > day)) row = last.Counts;   // the clock went back: never a row out of order
            else { row = new FoeCounts(); Days.Add((key, row)); }
            row.AddAll(diff);
            if (row.Kinds.Count > MaxRowKinds) { var i = Days.Count - 1; Days[i] = (Days[i].Period, row.Top(MaxRowKinds)); }   // the row is always the last one
            Fold(day);
        }

        /// <summary>The period an old row folds into by DayHistory's rule: days for DayRows days, then weeks, months, "before".</summary>
        static string Target(string period, DateTime today)
        {
            var dayCut = today.AddDays(-(DayHistory.DayRows - 1));
            var weekCut = DayHistory.Monday(dayCut).AddDays(-7 * DayHistory.WeekRows);
            var monthCut = new DateTime(weekCut.Year, weekCut.Month, 1).AddMonths(-DayHistory.MonthRows);
            if (period == DayHistory.BeforeKey) return period;
            var start = DayHistory.StartOf(period);
            var kind = period.Length == 10 ? 'd' : period[0];
            if (kind == 'd') { if (start >= dayCut) return period; kind = 'w'; start = DayHistory.Monday(start); }
            if (kind == 'w') { if (start >= weekCut) return DayHistory.WeekKey(start); kind = 'm'; start = new DateTime(start.Year, start.Month, 1); }
            if (kind == 'm' && start >= monthCut) return DayHistory.MonthKey(start);
            return DayHistory.BeforeKey;
        }

        public void Fold(DateTime today)
        {
            today = today.Date;
            if (Days.All(r => Target(r.Period, today) == r.Period)) return;
            var merged = new Dictionary<string, FoeCounts>(); var order = new List<string>();
            foreach (var r in Days)
            {
                var t = Target(r.Period, today);
                if (!merged.TryGetValue(t, out var into)) { merged[t] = into = new FoeCounts(); order.Add(t); }
                into.AddAll(r.Counts);
            }
            Days.Clear();
            Days.AddRange(order.Select(k => (k, merged[k].Kinds.Count > MaxRowKinds ? merged[k].Top(MaxRowKinds) : merged[k])).OrderBy(r => DayHistory.StartOf(r.k)));
        }

        /// <summary>The day rows from <paramref name="fromDay"/> to <paramref name="toDay"/> (local days, both included) added up.</summary>
        public FoeCounts Sum(DateTime fromDay, DateTime toDay)
        {
            var s = new FoeCounts();
            foreach (var r in Days) { var start = DayHistory.StartOf(r.Period); if (r.Period.Length == 10 && start >= fromDay.Date && start <= toDay.Date) s.AddAll(r.Counts); }
            return s;
        }

        public void WriteTo(Json j, string key = "foes")
        {
            j.Key(key).Open();
            if (FromUtc > DateTime.MinValue) j.Str("from", FromUtc.ToString("o", CultureInfo.InvariantCulture));
            Previous.WriteTo(j, "previous");
            j.Key("lastSession").Open().Str("id", LastSession); Last.WriteTo(j, "kinds"); j.Close();
            j.Key("days").OpenArr();
            foreach (var r in Days) { j.Open().Str("p", r.Period); r.Counts.WriteTo(j, "kinds"); j.Close(); }
            j.CloseArr().Close();
        }

        /// <summary>Reads what WriteTo wrote; null or missing: an empty book (From set later, on load). Rows out of order or unreadable are dropped.</summary>
        public static FoeBook ReadFrom(Dictionary<string, object> o)
        {
            var b = new FoeBook();
            if (o == null) return b;
            if (DateTime.TryParse(MiniJson.Str(o, "from"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var from)) b.FromUtc = from.ToUniversalTime();
            b.Previous.ReadFrom(MiniJson.Obj(o, "previous"));
            var last = MiniJson.Obj(o, "lastSession");
            b.LastSession = MiniJson.Str(last, "id"); b.Last.ReadFrom(MiniJson.Obj(last, "kinds"));
            if (o.TryGetValue("days", out var ds) && ds is List<object> rows)
                foreach (var x in rows.OfType<Dictionary<string, object>>())
                {
                    var p = MiniJson.Str(x, "p");
                    if (p != DayHistory.BeforeKey && DayHistory.StartOf(p) == DateTime.MinValue) continue;
                    var t = new FoeCounts().ReadFrom(MiniJson.Obj(x, "kinds"));
                    if (b.Days.Count > 0 && DayHistory.StartOf(b.Days[b.Days.Count - 1].Period) > DayHistory.StartOf(p)) continue;   // out of order: never trusted
                    if (b.Days.Count > 0 && b.Days[b.Days.Count - 1].Period == p) { b.Days[b.Days.Count - 1].Counts.AddAll(t); continue; }
                    b.Days.Add((p, t));
                }
            return b;
        }
    }

    /// <summary>
    /// What of the battle record goes in your shared copy (snapshot key "foes"): the count per foe kind and what became of them, for This session
    /// and since the foes were first recorded on your PC (with that date), at most MaxKinds kinds each (the rest summed under FoeCounts.OtherKind).
    /// The feed itself goes under its own key (0.8.2, FeedShare). An older reader ignores the key.
    /// JSON: "foes":{"from":"ISO","session":{"Troll":[f,d,w,h]},"since":{..}}; a kind with foes still in the fight: [f,d,w,h,fighting].
    /// </summary>
    public sealed class FoeShare
    {
        public const int MaxKinds = 60;
        public FoeCounts Session, Since;
        public DateTime? FromUtc;

        public void WriteTo(Json j)
        {
            j.Key("foes").Open();
            if (FromUtc.HasValue) j.Str("from", FromUtc.Value.ToString("o", CultureInfo.InvariantCulture));
            (Session ?? new FoeCounts()).Top(MaxKinds).WriteTo(j, "session", fighting: true);
            if (Since != null) Since.Top(MaxKinds).WriteTo(j, "since", fighting: true);
            j.Close();
        }

        /// <summary>Reads a fellow's "foes" (null: they share none: an older Hearthwoven).</summary>
        public static FoeShare ReadFrom(Dictionary<string, object> o)
        {
            if (o == null) return null;
            var s = new FoeShare { Session = new FoeCounts().ReadFrom(MiniJson.Obj(o, "session")) };
            if (MiniJson.Obj(o, "since") != null) s.Since = new FoeCounts().ReadFrom(MiniJson.Obj(o, "since"));
            if (DateTime.TryParse(MiniJson.Str(o, "from"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var from)) s.FromUtc = from.ToUniversalTime();
            return s;
        }
    }

    /// <summary>
    /// The battle feed in your shared copy (0.8.2, snapshot key "battleFeed"; Joost in game, on a fellow's book: "why no feed? I want that in"), so a
    /// fellow's book shows your Feed and Last fight. What goes: the newest MaxFoes foes of this session's feed, whole fights while they fit (the newest
    /// fight always: past MaxFoes its newest, Of says how many it had), and how many of the session's foes this copy does not hold.
    ///
    /// Shaped for small live updates (LiveSync.cs: a delta is the difference to the last full copy, objects key by key, an array of one length element
    /// by element): the foes are an object keyed by their running number (FoeEncounter.Seq), never a growing array; times are whole seconds from the
    /// session's first hit (T0); damage per type as the rest of the copy writes it (Json.F), trailing types without damage left out. A hit then
    /// changes a few numbers of one foe.
    ///
    /// Names (who a foe was defeated with, who was near) only of players who share themselves (From's <c>shares</c>: Plugin asks the fellows the
    /// server lists); anyone else is counted (Outside) as another player, never named, the word Meals has for a feast's maker it does not know. No
    /// positions: a foe's biome only. A near name is a key, and Valheim names are 3 characters or more, so none is ever the death place's "x" or "z"
    /// (GroupShare.SharedCopy). An older reader ignores the key; the server stores and relays it with the rest of the copy, unread.
    /// JSON: "battleFeed":{"t0":"ISO","left":12,"open":7,"fights":{"7":{"near":{"Edda":34},"out":1,"of":120,"dd":1}},
    ///       "foes":{"123":{"f":7,"k":"Troll","b":"Swamp","s":12,"e":40,"o":1,"a":2,"d":[0,52.3],"r":[0,12.5],"w":["Edda"],"n":1,"u":1,
    ///                      "i":"-81234:5521","bf":2,"rn":1,"bk":3,"pa":1}},
    ///       "boss":{"2":{"b":["-81234:5520"],"k":"SeekerQueen","f":7,"s":10,"e":300,"ls":300,"o":1,"hp":0,"lo":0,"hm":0.3,"dd":1,"n":31,"rn":1}},
    ///       "runs":{"1":{"k":"Mistlands_DvergrBossEntrance1","tk":"$location_dvergrboss","s":2,"e":320,"t":318,"in":1,"dd":1,"n":40,"bf":2,
    ///                    "sp":[[5,70],[90,300,1]]}},
    ///       "falls":[{"t":200,"f":7,"bf":2,"rn":1,"by":"SeekerQueen","bo":1,"r":[0,0,15,0,0,0,20]}]}
    /// (o FoeOutcome, a AwayHow, d and r dealt and received in BattleRecorder.Types order, w the names it was defeated with, n Others, u Outside;
    /// a, d, r, w, n and u only when not empty; a fight is in "fights" only with near, out, of or dd.)
    /// 0.9 (work/hearthwoven-0.8.5/fights/DESIGN.md), so fellows' books can join one fight from everyone's feeds: each foe's network id "i" (the
    /// creature's ZDOID, the same on every PC; the user part is the session id of the PC that made it, never a player's account), its boss fight
    /// "bf", its dungeon run "rn", your held blocks "bk" and parries "pa"; dd your deaths in a fight; the newest MaxBosses boss fights ("boss":
    /// their bosses' ids, kind, fight, span, BossOutcome, the first boss's health left, your deaths, the foes in it, its run) and MaxRuns dungeon
    /// runs ("runs": the place's kind and name token, span, seconds inside, in = you are inside now, your deaths, the foes, its boss fight, its time
    /// line "sp": each fight's start and end, 1 for a boss fight's); the newest MaxFalls falls ("falls": when, fight, boss fight, run, what took you
    /// down, bo = a boss, and what hit you in the last seconds per type). Boss fights add "ls" (last seen), BossOutcome, the boss's health
    /// last seen "hp", the lowest it reached "lo" and, while on, a minute ago "hm". No position, no place:
    /// a run says only its kind. An older reader ignores every one of these keys.
    /// </summary>
    public sealed class FeedShare
    {
        public const int MaxFoes = 100;
        /// <summary>Reading a fellow's copy, whatever it says: at most this many foes, and MaxNames names per list.</summary>
        public const int MaxRead = 2 * MaxFoes, MaxNames = BattleRecorder.MaxNearNames;
        /// <summary>0.9: the newest boss fights, dungeon runs (each with its newest MaxSpans fights) and falls in the copy (and read, whatever it says).</summary>
        public const int MaxBosses = 5, MaxRuns = 5, MaxSpans = 30, MaxFalls = 10;
        /// <summary>The session's first hit, to the whole second: every time in the copy is whole seconds from it.</summary>
        public DateTime T0;
        /// <summary>The foes of the session's feed this copy does not hold: older than the newest MaxFoes, or no longer kept by the recorder (FeedLeftOut).</summary>
        public int LeftOut;
        /// <summary>Newest first, each with its foes newest first, as BattleRecorder.Fights gives them.</summary>
        public readonly List<FoeFight> Fights = new List<FoeFight>();
        /// <summary>0.9: the newest boss fights and dungeon runs, newest first (BattleRecorder.BossFights, Runs).</summary>
        public readonly List<BossFight> Bosses = new List<BossFight>();
        public readonly List<DungeonRun> Runs = new List<DungeonRun>();
        /// <summary>0.9: the newest falls (deaths), newest first (BattleRecorder.Falls).</summary>
        public readonly List<Fall> Falls = new List<Fall>();

        /// <summary>What of <paramref name="rec"/>'s feed goes in the shared copy now; <paramref name="shares"/>: whether a player shares (null: nobody,
        /// so every name is left out). <paramref name="maxFoes"/>: the newest foes it holds (0.8.5: your kept fights hold more, KeptFights.MaxFoes).</summary>
        public static FeedShare From(BattleRecorder rec, Func<string, bool> shares, int maxFoes = MaxFoes)
        {
            var s = new FeedShare();
            if (rec == null) return s;
            s.T0 = Whole(rec.FirstUtc); s.LeftOut = rec.FeedLeftOut;
            shares = shares ?? (_ => false);
            bool Named(string nm) => !string.IsNullOrEmpty(nm) && shares(nm);
            int n = 0; var full = false;
            foreach (var f in rec.Fights())
            {
                full = full || n >= maxFoes || (s.Fights.Count > 0 && n + f.Foes > maxFoes);   // whole fights while they fit, then none older (no gap)
                if (full) { s.LeftOut += f.Foes; continue; }
                var take = Math.Min(f.Entries.Count, maxFoes - n);
                var c = new FoeFight { Number = f.Number, Open = f.Open, Of = take < f.Foes ? f.Foes : 0, Deaths = f.Deaths };
                var outside = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (f.Near != null)
                    foreach (var kv in f.Near)
                        if (Named(kv.Key)) (c.Near ?? (c.Near = new Dictionary<string, float>(StringComparer.Ordinal)))[kv.Key] = kv.Value;
                        else if (!string.IsNullOrEmpty(kv.Key) && kv.Value >= BattleRecorder.NearSeconds) outside.Add(kv.Key);   // in the fight (FoeFight.With), not named
                for (int i = 0; i < take; i++)
                {
                    var e = f.Entries[i];
                    var hidden = e.With.Where(nm => !string.IsNullOrEmpty(nm) && !shares(nm)).ToList();
                    foreach (var nm in hidden) outside.Add(nm);
                    var x = new FoeEncounter { Id = e.Id, Seq = e.Seq, Kind = e.Kind, Token = e.Token, Biome = e.Biome, StartUtc = e.StartUtc, EndUtc = e.EndUtc, Fight = e.Fight,
                                               HitsDealt = e.HitsDealt, HitsReceived = e.HitsReceived, Outcome = e.Outcome, Away = e.Away,
                                               With = e.With.Where(Named).ToArray(), Others = e.Others, Outside = hidden.Count,
                                               IsBoss = e.IsBoss, Boss = e.Boss, Run = e.Run, Blocks = e.Blocks, Parries = e.Parries };
                    Array.Copy(e.Dealt, x.Dealt, x.Dealt.Length); Array.Copy(e.Received, x.Received, x.Received.Length);
                    Add(c, x);
                }
                c.Outside = outside.Count;
                s.LeftOut += f.Foes - take;
                s.Fights.Add(c); n += take;
            }
            foreach (var b in rec.BossFights().Take(MaxBosses))   // copies: what a fellow may see of each (no place)
            {
                var c = new BossFight { Number = b.Number, Fight = b.Fight, Run = b.Run, Kind = b.Kind, Token = b.Token, StartUtc = b.StartUtc, EndUtc = b.EndUtc, LastSeenUtc = b.LastSeenUtc, OutcomeUtc = b.OutcomeUtc, Outcome = b.Outcome,
                                        Health = b.Health, Lowest = b.Lowest, HealthMinuteAgo = b.Open ? b.HealthMinuteAgo : -1f, Deaths = b.Deaths, Foes = b.Foes };
                c.Bosses.AddRange(b.Bosses);
                s.Bosses.Add(c);
            }
            foreach (var r in rec.Runs().Take(MaxRuns))
            {
                var c = new DungeonRun { Number = r.Number, Kind = r.Kind, Token = r.Token, StartUtc = r.StartUtc, EndUtc = r.EndUtc, Inside = r.Inside, Open = r.Open, Deaths = r.Deaths, Foes = r.Foes, Boss = r.Boss };
                foreach (var sp in r.Spans.Skip(Math.Max(0, r.Spans.Count - MaxSpans))) c.Spans.Add(new RunSpan { Fight = sp.Fight, StartUtc = sp.StartUtc, EndUtc = sp.EndUtc, Boss = sp.Boss });
                s.Runs.Add(c);
            }
            s.Falls.AddRange(rec.Falls().Take(MaxFalls));   // no names, no place: when, where in the record, what took you down
            return s;
        }

        // a foe into its fight: the fight's span and sums follow (as BattleRecorder.Fights builds them)
        static void Add(FoeFight f, FoeEncounter e)
        {
            if (f.Entries.Count == 0 || e.StartUtc < f.StartUtc) f.StartUtc = e.StartUtc;
            if (f.Entries.Count == 0 || e.EndUtc > f.EndUtc) f.EndUtc = e.EndUtc;
            f.Entries.Add(e); f.Blocks += e.Blocks; f.Parries += e.Parries;
            for (int t = 0; t < BattleRecorder.Types.Length; t++) { f.Dealt[t] += e.Dealt[t]; f.Received[t] += e.Received[t]; }
        }

        static DateTime Whole(DateTime t) => new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        long Secs(DateTime t) => t.Ticks / TimeSpan.TicksPerSecond - T0.Ticks / TimeSpan.TicksPerSecond;

        /// <summary>Writes the copy under <paramref name="key"/>. <paramref name="hits"/> (0.8.5, your kept fights only, never the shared copy): each foe's
        /// hits both ways too ("hd", "hr"), so a kept fight weighs your blocks against the hits that landed (FightPages.BringLine).</summary>
        public void WriteTo(Json j, string key = "battleFeed", bool hits = false)
        {
            var inv = CultureInfo.InvariantCulture;
            j.Key(key).Open().Str("t0", Whole(T0).ToString("o", inv)).Num("left", LeftOut);
            var open = Fights.FirstOrDefault(f => f.Open);
            if (open != null) j.Num("open", open.Number);
            j.Key("fights").Open();
            foreach (var f in Fights)
            {
                var near = f.Near != null && f.Near.Count > 0;
                if (!near && f.Outside == 0 && f.Of == 0 && f.Deaths == 0) continue;
                j.Key(f.Number.ToString(inv)).Open();
                if (near) { j.Key("near").Open(); foreach (var kv in f.Near.OrderBy(k => k.Key, StringComparer.Ordinal)) j.Num(kv.Key, kv.Value); j.Close(); }
                if (f.Outside > 0) j.Num("out", f.Outside);
                if (f.Of > 0) j.Num("of", f.Of);
                if (f.Deaths > 0) j.Num("dd", f.Deaths);
                j.Close();
            }
            j.Close().Key("foes").Open();
            foreach (var f in Fights)
                foreach (var e in f.Entries)
                {
                    j.Key(e.Seq.ToString(inv)).Open().Num("f", f.Number).Str("k", e.Kind ?? "?").Str("b", e.Biome ?? "None").Num("s", Secs(e.StartUtc)).Num("e", Secs(e.EndUtc)).Num("o", (int)e.Outcome);
                    if (e.Away != AwayHow.None) j.Num("a", (int)e.Away);
                    Types(j, "d", e.Dealt); Types(j, "r", e.Received);
                    if (e.With.Length > 0) j.Raw("w", "[" + string.Join(",", e.With.Select(Json.Q).ToArray()) + "]");
                    if (e.Others > 0) j.Num("n", e.Others);
                    if (e.Outside > 0) j.Num("u", e.Outside);
                    if (!e.Id.IsNone) j.Str("i", e.Id.ToString());
                    if (e.Boss > 0) j.Num("bf", e.Boss);
                    if (e.Run > 0) j.Num("rn", e.Run);
                    if (e.Blocks > 0) j.Num("bk", e.Blocks);
                    if (e.Parries > 0) j.Num("pa", e.Parries);
                    if (hits && e.HitsDealt > 0) j.Num("hd", e.HitsDealt);
                    if (hits && e.HitsReceived > 0) j.Num("hr", e.HitsReceived);
                    j.Close();
                }
            j.Close();
            if (Bosses.Count > 0)
            {
                j.Key("boss").Open();
                foreach (var b in Bosses)
                {
                    j.Key(b.Number.ToString(inv)).Open().Raw("b", "[" + string.Join(",", b.Bosses.Select(id => Json.Q(id.ToString())).ToArray()) + "]")
                     .Str("k", b.Kind ?? "?").Num("f", b.Fight).Num("s", Secs(b.StartUtc)).Num("e", Secs(b.EndUtc)).Num("o", (int)b.Outcome);
                    if (b.LastSeenUtc > b.EndUtc) j.Num("ls", Secs(b.LastSeenUtc));
                    if (b.Health >= 0f) j.Num("hp", Math.Round(b.Health, 2));
                    if (b.Lowest >= 0f) j.Num("lo", Math.Round(b.Lowest, 2));
                    if (b.Open && b.HealthMinuteAgo >= 0f) j.Num("hm", Math.Round(b.HealthMinuteAgo, 2));
                    if (b.Deaths > 0) j.Num("dd", b.Deaths);
                    if (b.Foes > 0) j.Num("n", b.Foes);
                    if (b.Run > 0) j.Num("rn", b.Run);
                    j.Close();
                }
                j.Close();
            }
            if (Runs.Count > 0)
            {
                j.Key("runs").Open();
                foreach (var r in Runs)
                {
                    j.Key(r.Number.ToString(inv)).Open().Str("k", r.Kind ?? "?").Num("s", Secs(r.StartUtc)).Num("e", Secs(r.EndUtc)).Num("t", Math.Round(r.Inside));
                    if (!string.IsNullOrEmpty(r.Token)) j.Str("tk", r.Token);
                    if (r.Spans.Count > 0) j.Raw("sp", "[" + string.Join(",", r.Spans.Select(sp => "[" + Secs(sp.StartUtc).ToString(inv) + "," + Secs(sp.EndUtc).ToString(inv) + (sp.Boss ? ",1" : "") + "]").ToArray()) + "]");
                    if (r.Open) j.Num("in", 1);
                    if (r.Deaths > 0) j.Num("dd", r.Deaths);
                    if (r.Foes > 0) j.Num("n", r.Foes);
                    if (r.Boss > 0) j.Num("bf", r.Boss);
                    j.Close();
                }
                j.Close();
            }
            if (Falls.Count > 0)
            {
                j.Key("falls").OpenArr();
                foreach (var f in Falls)
                {
                    j.Open().Num("t", Secs(f.Utc));
                    if (f.Fight > 0) j.Num("f", f.Fight);
                    if (f.Boss > 0) j.Num("bf", f.Boss);
                    if (f.Run > 0) j.Num("rn", f.Run);
                    if (!string.IsNullOrEmpty(f.By)) j.Str("by", f.By);
                    if (f.ByBoss) j.Num("bo", 1);
                    Types(j, "r", f.Received);
                    j.Close();
                }
                j.CloseArr();
            }
            j.Close();
        }

        // the amounts per type up to the last one with damage (none: no key)
        internal static void Types(Json j, string key, float[] a)
        {
            int last = a.Length - 1;
            while (last >= 0 && !(a[last] > 0f && Json.IsFinite(a[last]))) last--;
            if (last < 0) return;
            var b = new System.Text.StringBuilder("[");
            for (int i = 0; i <= last; i++) { if (i > 0) b.Append(','); b.Append(a[i] > 0f && Json.IsFinite(a[i]) ? Json.F(a[i]) : "0"); }
            j.Raw(key, b.Append(']').ToString());
        }

        /// <summary>Reads a fellow's "battleFeed" (null: they share none, an older Hearthwoven). Bounded and forgiving: at most MaxRead foes and MaxNames
        /// names per list, numbers clipped, a foe without a fight left out; the fights newest first, their foes newest first.</summary>
        public static FeedShare ReadFrom(Dictionary<string, object> o)
        {
            if (o == null) return null;
            var s = new FeedShare();
            if (DateTime.TryParse(MiniJson.Str(o, "t0"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t0)) s.T0 = t0.ToUniversalTime();
            s.LeftOut = Count(MiniJson.Num(o, "left"));
            var open = Count(MiniJson.Num(o, "open"));
            var byFight = new Dictionary<int, FoeFight>();
            int read = 0;
            foreach (var kv in MiniJson.Obj(o, "foes") ?? new Dictionary<string, object>())
            {
                if (read >= MaxRead) break;
                if (!(kv.Value is Dictionary<string, object> x) || !int.TryParse(kv.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var seq)) continue;
                var number = Count(MiniJson.Num(x, "f"));
                if (number <= 0) continue;
                var e = new FoeEncounter
                {
                    Seq = seq, Id = ParseId(MiniJson.Str(x, "i")), Kind = MiniJson.Str(x, "k", "?"), Biome = MiniJson.Str(x, "b", "None"), Fight = number,
                    Boss = Count(MiniJson.Num(x, "bf")), Run = Count(MiniJson.Num(x, "rn")), Blocks = Count(MiniJson.Num(x, "bk")), Parries = Count(MiniJson.Num(x, "pa")),
                    StartUtc = At(s.T0, MiniJson.Num(x, "s")), EndUtc = At(s.T0, MiniJson.Num(x, "e")),
                    Outcome = (FoeOutcome)Math.Min(Count(MiniJson.Num(x, "o")), (int)FoeOutcome.GotAway), Away = (AwayHow)Math.Min(Count(MiniJson.Num(x, "a")), (int)AwayHow.YouLeft),
                    Others = Count(MiniJson.Num(x, "n")), Outside = Count(MiniJson.Num(x, "u")), HitsDealt = Count(MiniJson.Num(x, "hd")), HitsReceived = Count(MiniJson.Num(x, "hr")),
                };
                if (e.EndUtc < e.StartUtc) e.EndUtc = e.StartUtc;
                ReadTypes(x, "d", e.Dealt); ReadTypes(x, "r", e.Received);
                if (x.TryGetValue("w", out var w) && w is List<object> names) e.With = names.OfType<string>().Where(nm => nm.Length > 0).Take(MaxNames).ToArray();
                if (!byFight.TryGetValue(number, out var f)) byFight[number] = f = new FoeFight { Number = number, Open = number == open };
                Add(f, e);
                read++;
            }
            var info = MiniJson.Obj(o, "fights");
            foreach (var f in byFight.Values)
            {
                var fi = MiniJson.Obj(info, f.Number.ToString(CultureInfo.InvariantCulture));
                var near = MiniJson.Obj(fi, "near");
                if (near != null)
                    foreach (var kv in near.Take(MaxNames))
                        if (kv.Key.Length > 0 && kv.Value is double v && Json.IsFinite(v) && v > 0) (f.Near ?? (f.Near = new Dictionary<string, float>(StringComparer.Ordinal)))[kv.Key] = (float)Math.Min(v, 1e7);
                f.Outside = Count(MiniJson.Num(fi, "out")); f.Deaths = Count(MiniJson.Num(fi, "dd"));
                var of = Count(MiniJson.Num(fi, "of")); f.Of = of > f.Entries.Count ? of : 0;
                f.Entries.Sort((a, b) => b.EndUtc != a.EndUtc ? b.EndUtc.CompareTo(a.EndUtc) : b.StartUtc.CompareTo(a.StartUtc));
            }
            s.Fights.AddRange(byFight.Values.OrderByDescending(f => f.Number));
            // 0.9: the boss fights and runs (an older copy has none); a foe is a boss when its boss fight lists its id
            foreach (var kv in (MiniJson.Obj(o, "boss") ?? new Dictionary<string, object>()).Take(MaxBosses))
            {
                if (!(kv.Value is Dictionary<string, object> x) || !int.TryParse(kv.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0) continue;
                var b = new BossFight { Number = number, Kind = MiniJson.Str(x, "k", "?"), Fight = Count(MiniJson.Num(x, "f")), StartUtc = At(s.T0, MiniJson.Num(x, "s")), EndUtc = At(s.T0, MiniJson.Num(x, "e")),
                                        Outcome = (BossOutcome)Math.Min(Count(MiniJson.Num(x, "o")), (int)BossOutcome.GotAway), Health = Frac(x, "hp"), Lowest = Frac(x, "lo"), HealthMinuteAgo = Frac(x, "hm"),
                                        Deaths = Count(MiniJson.Num(x, "dd")), Foes = Count(MiniJson.Num(x, "n")), Run = Count(MiniJson.Num(x, "rn")) };
                if (b.EndUtc < b.StartUtc) b.EndUtc = b.StartUtc;
                b.LastSeenUtc = x.ContainsKey("ls") ? At(s.T0, MiniJson.Num(x, "ls")) : b.EndUtc;
                if (b.LastSeenUtc < b.EndUtc) b.LastSeenUtc = b.EndUtc;
                if (x.TryGetValue("b", out var ids) && ids is List<object> list)
                    foreach (var t in list.OfType<string>().Take(BossFight.MaxBosses)) { var id = ParseId(t); if (!id.IsNone) b.Bosses.Add(id); }
                s.Bosses.Add(b);
            }
            s.Bosses.Sort((a, b) => b.Number.CompareTo(a.Number));
            foreach (var e in s.Fights.SelectMany(f => f.Entries))
                if (e.Boss > 0 && !e.Id.IsNone) e.IsBoss = s.Bosses.Any(b => b.Number == e.Boss && b.Bosses.Contains(e.Id));
            foreach (var kv in (MiniJson.Obj(o, "runs") ?? new Dictionary<string, object>()).Take(MaxRuns))
            {
                if (!(kv.Value is Dictionary<string, object> x) || !int.TryParse(kv.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0) continue;
                var r = new DungeonRun { Number = number, Kind = MiniJson.Str(x, "k", "?"), StartUtc = At(s.T0, MiniJson.Num(x, "s")), EndUtc = At(s.T0, MiniJson.Num(x, "e")), Inside = Count(MiniJson.Num(x, "t")),
                                         Open = MiniJson.Num(x, "in") > 0, Deaths = Count(MiniJson.Num(x, "dd")), Foes = Count(MiniJson.Num(x, "n")), Boss = Count(MiniJson.Num(x, "bf")) };
                var tk = MiniJson.Str(x, "tk", null); if (!string.IsNullOrEmpty(tk) && tk.Length <= 64) r.Token = tk;
                if (r.EndUtc < r.StartUtc) r.EndUtc = r.StartUtc;
                if (x.TryGetValue("sp", out var sps) && sps is List<object> spans)
                    foreach (var sp in spans.OfType<List<object>>().Take(MaxSpans))
                    {
                        if (sp.Count < 2 || !(sp[0] is double a) || !(sp[1] is double z)) continue;
                        var span = new RunSpan { StartUtc = At(s.T0, a), EndUtc = At(s.T0, z), Boss = sp.Count > 2 && sp[2] is double bo && bo > 0 };
                        if (span.EndUtc < span.StartUtc) span.EndUtc = span.StartUtc;
                        r.Spans.Add(span);
                    }
                s.Runs.Add(r);
            }
            s.Runs.Sort((a, b) => b.Number.CompareTo(a.Number));
            if (o.TryGetValue("falls", out var fs) && fs is List<object> fl)
                foreach (var x in fl.OfType<Dictionary<string, object>>().Take(MaxFalls))
                {
                    var f = new Fall { Utc = At(s.T0, MiniJson.Num(x, "t")), Fight = Count(MiniJson.Num(x, "f")), Boss = Count(MiniJson.Num(x, "bf")), Run = Count(MiniJson.Num(x, "rn")), ByBoss = MiniJson.Num(x, "bo") > 0 };
                    var by = MiniJson.Str(x, "by", null); if (!string.IsNullOrEmpty(by) && by.Length <= 64) f.By = by;
                    ReadTypes(x, "r", f.Received);
                    s.Falls.Add(f);
                }
            s.Falls.Sort((a, b) => b.Utc.CompareTo(a.Utc));
            return s;
        }

        /// <summary>A foe's network id as FoeId.ToString wrote it ("user:id"); FoeId none when there is none (a copy from before 0.9) or it is unreadable.</summary>
        public static FoeId ParseId(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 40) return default;
            var c = text.LastIndexOf(':');
            if (c <= 0 || !long.TryParse(text.Substring(0, c), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var user) ||
                !uint.TryParse(text.Substring(c + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var id)) return default;
            return new FoeId(user, id);
        }

        static int Count(double v) => Json.IsFinite(v) ? (int)Math.Max(0, Math.Min(1e7, v)) : 0;
        static float Frac(Dictionary<string, object> x, string key) => x.ContainsKey(key) ? (float)Math.Min(1.0, Count(MiniJson.Num(x, key) * 1000) / 1000.0) : -1f;
        static DateTime At(DateTime t0, double secs) => Json.IsFinite(secs) && Math.Abs(secs) < 1e8 ? t0.AddSeconds(Math.Round(secs)) : t0;

        internal static void ReadTypes(Dictionary<string, object> x, string key, float[] into)
        {
            if (!x.TryGetValue(key, out var v) || !(v is List<object> a)) return;
            for (int i = 0; i < into.Length && i < a.Count; i++) if (a[i] is double d && Json.IsFinite(d) && d > 0) into[i] = (float)Math.Min(d, 1e9);
        }
    }
}
