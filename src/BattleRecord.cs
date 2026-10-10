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
        public static readonly string[] NoNames = new string[0];

        public bool Defeated => Outcome == FoeOutcome.DefeatedByYou || Outcome == FoeOutcome.DefeatedWith;
        public float DealtTotal { get { float s = 0; foreach (var v in Dealt) s += v; return s; } }
        public float ReceivedTotal { get { float s = 0; foreach (var v in Received) s += v; return s; } }

        /// <summary>The outcome in the book's words: "defeated by you", "defeated with Edda", "defeated with Edda and Tor", "defeated with Edda and
        /// a fellow player", "defeated with 2 fellow players", "survived", "got away" (the game removed it), "still fighting".</summary>
        public string OutcomeText => Text(Outcome, Away, With, Others);

        public static string Text(FoeOutcome outcome, AwayHow away, string[] with, int others)
        {
            switch (outcome)
            {
                case FoeOutcome.DefeatedByYou: return "defeated by you";
                case FoeOutcome.DefeatedWith: return "defeated with " + Names(with ?? NoNames, others);
                case FoeOutcome.GotAway: return away == AwayHow.Despawned ? "got away" : "survived";
                default: return "still fighting";
            }
        }

        /// <summary>"Edda", "Edda and Tor", "Edda, Tor and Ylva", "Edda and a fellow player", "2 fellow players" (never "others": WORDING.md).</summary>
        public static string Names(string[] with, int others)
        {
            var parts = new List<string>(with);
            if (others > 0) parts.Add(others == 1 ? "a fellow player" : others + " fellow players");
            if (parts.Count == 0) return "a fellow player";
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1).ToArray()) + " and " + parts[parts.Count - 1];
        }
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
        public int Foes => Entries.Count;
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

        sealed class Inst
        {
            public FoeId Id; public string Kind, Token;
            public DateTime LastDealt = DateTime.MinValue, LastReceived = DateTime.MinValue;
            public bool YouHit, HitYou, Gone;
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
        int fight; bool fightOn; DateTime fightLast;

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

        /// <summary>Your hit on a foe (before its resistances, as the Damage page). <paramref name="token"/> is its name token (the kill message's).</summary>
        public void Dealt(DateTime utc, FoeId id, string kind, string token, string biome, HitData.DamageTypes d)
        {
            if (id.IsNone || Sum(d) <= 0f) return;
            var (e, inst) = Touch(utc, id, kind, token, biome);
            AddTypes(e.Dealt, d); e.HitsDealt++;
            inst.YouHit = true; inst.LastDealt = utc;
        }

        /// <summary>A foe's hit on you, after your armour (as the Defence page's Received from).</summary>
        public void Received(DateTime utc, FoeId id, string kind, string token, string biome, HitData.DamageTypes d)
        {
            if (id.IsNone || Sum(d) <= 0f) return;
            var (e, inst) = Touch(utc, id, kind, token, biome);
            AddTypes(e.Received, d); e.HitsReceived++;
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
        public bool Knows(FoeId id) => instances.ContainsKey(id);

        /// <summary>The game's kill message for you (Game.RPC_RegisterKill): a foe of this name token died that you hit; <paramref name="attackers"/>
        /// players hit it.</summary>
        public void Killed(DateTime utc, string token, int attackers)
        {
            Advance(utc);
            int best = -1;
            for (int i = 0; i < gone.Count; i++)
                if (gone[i].Inst.Token == token && utc - gone[i].At <= MatchWindow && (best < 0 || (gone[i].Inst.Attackers == attackers && gone[best].Inst.Attackers != attackers))) best = i;
            if (best >= 0) { var g = gone[best]; gone.RemoveAt(best); Resolve(g.Inst, attackers, "its destroy came first"); return; }
            if (credits.Count >= MaxWaiting) { Fallback(credits[0]); credits.RemoveAt(0); }
            credits.Add(new Credit { Token = token, Attackers = attackers, At = utc });
            Version++;
        }

        /// <summary>A creature's network object was destroyed (it died, or the game removed it). Only acts on a creature in the table.</summary>
        public void Destroyed(DateTime utc, FoeId id)
        {
            if (!instances.TryGetValue(id, out var inst) || inst.Gone) return;
            Advance(utc);
            inst.Gone = true;
            if (inst.Defeated()) return;   // the kill came first and went to it already
            int best = -1;
            for (int i = 0; i < credits.Count; i++)
                if (credits[i].Token == inst.Token && utc - credits[i].At <= MatchWindow && (best < 0 || (credits[i].Attackers == inst.Attackers && credits[best].Attackers != inst.Attackers))) best = i;
            if (best >= 0) { var c = credits[best]; credits.RemoveAt(best); Resolve(inst, c.Attackers, "matched to its destroy"); return; }
            if (gone.Count >= MaxWaiting) { Despawn(gone[0].Inst); gone.RemoveAt(0); }
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

        /// <summary>Once a second from the game (and before the panel reads): ends a fight after FightGap, settles the kills and destroys that
        /// waited longer than MatchWindow.</summary>
        public void Tick(DateTime utc)
        {
            Advance(utc);
            for (int i = credits.Count - 1; i >= 0; i--) if (utc - credits[i].At > MatchWindow) { var c = credits[i]; credits.RemoveAt(i); Fallback(c); }
            for (int i = gone.Count - 1; i >= 0; i--) if (utc - gone[i].At > MatchWindow) { var g = gone[i]; gone.RemoveAt(i); Despawn(g.Inst); }
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
                if (cur == null || cur.Number != e.Fight) { cur = new FoeFight { Number = e.Fight, StartUtc = e.StartUtc, EndUtc = e.EndUtc, Open = fightOn && e.Fight == fight, Near = near.TryGetValue(e.Fight, out var nr) ? nr : null }; list.Add(cur); }
                cur.Entries.Add(e);
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

        (FoeEncounter, Inst) Touch(DateTime utc, FoeId id, string kind, string token, string biome)
        {
            Advance(utc);
            if (!fightOn) { fight++; fightOn = true; }
            fightLast = utc;
            if (!instances.TryGetValue(id, out var inst))
            {
                if (instances.Count >= MaxInstances) Evict();
                instances[id] = inst = new Inst { Id = id, Kind = kind ?? "?", Token = token ?? "" };
            }
            if (!open.TryGetValue(id, out var e))
            {
                e = new FoeEncounter { Id = id, Kind = inst.Kind, Token = inst.Token, Biome = biome ?? "None", StartUtc = utc, EndUtc = utc, Fight = fight };
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
            Version++;
            return (e, inst);
        }

        /// <summary>Ends the fight when its last hit is more than FightGap before <paramref name="utc"/>: every foe still standing got away.</summary>
        void Advance(DateTime utc)
        {
            if (!fightOn || utc - fightLast <= FightGap) return;
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

        void Resolve(Inst inst, int attackers, string how)
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
            Version++;
        }

        /// <summary>A kill whose destroy never came within MatchWindow (you were far): the kind's foe you hit last that is not down.</summary>
        void Fallback(Credit c)
        {
            Inst best = null;
            foreach (var inst in instances.Values)
                if (inst.Token == c.Token && inst.YouHit && !inst.Defeated() && (best == null || inst.LastDealt > best.LastDealt)) best = inst;
            if (best != null) Resolve(best, c.Attackers, "no destroy within 5 s: given to the kind's foe you hit last"); else { UnmatchedKills++; Version++; }
        }

        void Despawn(Inst inst)
        {
            if (inst.Defeated()) return;
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

        static void AddTypes(float[] into, HitData.DamageTypes d)
        {
            void A(int i, float v) { if (v > 0f && Json.IsFinite(v)) into[i] += v; }
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
    /// The feed itself stays on your PC. An older reader ignores the key.
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
}
