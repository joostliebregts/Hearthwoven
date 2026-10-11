using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// Where a creature or you stood at one moment (0.9 boss fights and dungeon runs, work/hearthwoven-0.8.5/fights/DESIGN.md). Used on your own PC
    /// only, by the arena rule and the run's interior: never shared, never saved (BattleRecorder keeps it outside every FoeEncounter). Inside: in an
    /// interior, by the game's own rule (Character.InInterior: y above 3000, where every dungeon is built); Zone: the 64 m zone it stands in
    /// (ZoneSystem.GetZone's formula), which names one dungeon, since the game builds each dungeon 5000 m above its own entrance's zone
    /// (Location.Awake). default(Spot): not known (a burning tick, a creature no longer loaded).
    /// </summary>
    public readonly struct Spot
    {
        public readonly float X, Y, Z;
        public readonly bool Known, Inside;
        public readonly long Zone;
        public const float InteriorY = 3000f;

        public Spot(float x, float y, float z)
        {
            X = x; Y = y; Z = z; Known = Json.IsFinite(x) && Json.IsFinite(y) && Json.IsFinite(z);
            Inside = Known && y > InteriorY; Zone = Known ? ZoneOf(x, z) : 0;
        }

        /// <summary>The zone of a point, as ZoneSystem.GetZone computes it (64 m squares centred on multiples of 64), as one number.</summary>
        public static long ZoneOf(float x, float z) => ((long)Math.Floor((x + 32.0) / 64.0) << 32) ^ (uint)(int)Math.Floor((z + 32.0) / 64.0);

        /// <summary>The arena rule's test: both in the same interior (one dungeon, however far apart), or both outside within <paramref name="metres"/>.</summary>
        public bool Near(Spot o, float metres)
        {
            if (!Known || !o.Known) return false;
            if (Inside || o.Inside) return Inside && o.Inside && Zone == o.Zone;
            float dx = X - o.X, dy = Y - o.Y, dz = Z - o.Z;
            return dx * dx + dy * dy + dz * dz <= metres * metres;
        }
    }

    /// <summary>
    /// How a boss fight ended, in words that can be known (round 2: never "lost"). Fighting: still on. Won: every boss in it died. Reset: a boss
    /// was seen back at full health without dying (the game has no reset of its own: every creature heals MaxHealth / m_regenAllHPTime per second
    /// of world time, BaseAI.UpdateRegeneration; BossFight.Health keeps where it stood when the fighting stopped, "reset at 41 %"). GotAway: nobody
    /// on this PC saw it for BattleRecorder.GotAwayAfter, so the outcome is not known (BossFight.Health: last seen at). A later sign turns GotAway
    /// into Won or Reset.
    /// </summary>
    public enum BossOutcome { Fighting, Won, Reset, GotAway }

    /// <summary>
    /// One boss fight, one attempt (0.9): from the first hit either way between a player and a boss (yours, or a drop in its health seen on your PC)
    /// until every boss in it is down (Won), a boss is seen back at full health (Reset), or this PC has not seen it for BattleRecorder.GotAwayAfter
    /// (GotAway). A death and the run back never split it. The foes in it: the bosses, and every foe fought meanwhile that stood within
    /// BattleRecorder.ArenaMetres of a boss when it was hit, or in the same interior (the Queen's Seekers and brood): their encounters carry its
    /// Number (FoeEncounter.Boss), whichever recorder fight they are in. On a fellow's feed: their boss fight as their copy shares it.
    /// </summary>
    public sealed class BossFight
    {
        /// <summary>Its running number on the PC that recorded it (1, 2, 3 ... this session); the key its foes point to.</summary>
        public int Number;
        /// <summary>The first recorder fight it is part of (0: none yet, a boss fight seen before your first hit in it). A long attempt can run over
        /// several recorder fights (a death and the run back): their foes all carry this boss fight's Number.</summary>
        public int Fight;
        /// <summary>The dungeon run it was fought in (DungeonRun.Number; 0: outside one).</summary>
        public int Run;
        /// <summary>The first boss's prefab ("SeekerQueen", the key the Battle pages use) and name token ("$enemy_seekerqueen").</summary>
        public string Kind, Token;
        /// <summary>The bosses in it, the first one first (Zil and Thungr: two), by their network id: the same on every PC.</summary>
        public readonly List<FoeId> Bosses = new List<FoeId>();
        /// <summary>Its first hit either way, and its last sign of fighting (a hit either way on a boss, or a boss's health seen dropping); Won: the kill.
        /// The span of the fighting: a Reset or GotAway is known later (OutcomeUtc).</summary>
        public DateTime StartUtc, EndUtc;
        /// <summary>When this PC last saw a boss of it (loaded here, or a hit either way): "last seen" for GotAway.</summary>
        public DateTime LastSeenUtc;
        /// <summary>When the outcome became known here (the kill, the full health seen, GotAwayAfter passed); MinValue while Fighting.</summary>
        public DateTime OutcomeUtc = DateTime.MinValue;
        public BossOutcome Outcome;
        /// <summary>The first boss's health (0..1) as last seen on this PC: now while Fighting, "last seen at 58 %" after; 0 once won; -1: never seen
        /// (a PC far from the boss). Lowest: the lowest it reached ("reset at 41 %"); HealthMinuteAgo: while Fighting, about a minute before the
        /// newest look (the lane's "down 8 % in the last minute"); -1: not known.</summary>
        public float Health = -1f, Lowest = -1f, HealthMinuteAgo = -1f;
        /// <summary>Your deaths during it (each one a Fall with Boss = Number); the foes in it (bosses and arena, each creature once).</summary>
        public int Deaths, Foes;
        public bool Open => Outcome == BossOutcome.Fighting;
        public TimeSpan Length => EndUtc - StartUtc;
        public const int MaxBosses = 8;
        // this PC only: the latest recorder fight with its foes; the first boss's health every 5 s for 80 s (HealthMinuteAgo)
        internal int LastFight;
        internal readonly DateTime[] HistAt = new DateTime[16]; internal readonly float[] HistHp = new float[16]; internal int HistN;

        /// <summary>The outcome in the book's words (Joost: never "lost" unless the book can know it): "Live", "Won", "Reset at 41 %" (the lowest it
        /// reached), "Got away" (it ended with no hits and no reset seen: the group left).</summary>
        public static string Say(BossOutcome outcome, float lowest)
        {
            switch (outcome)
            {
                case BossOutcome.Won: return "Won";
                case BossOutcome.Reset: return lowest >= 0f ? "Reset at " + Math.Round(lowest * 100).ToString(CultureInfo.InvariantCulture) + " %" : "Reset";
                case BossOutcome.GotAway: return "Got away";
                default: return "Live";
            }
        }
        public string Words => Say(Outcome, Lowest);
    }

    /// <summary>One player's part of a foe row: what they dealt to the kind and what it dealt them, per type (BattleRecorder.Types order).</summary>
    public sealed class RowPart
    {
        public string Name;
        public bool Self;
        public readonly float[] Dealt = new float[BattleRecorder.Types.Length], Received = new float[BattleRecorder.Types.Length];
    }

    /// <summary>
    /// One foe kind in a fight, boss fight or run, with each player's part (0.9): the pages' foe row (FightView's FightFoe), kept in saved attempts
    /// and runs so their pages draw after a restart. Count: separate creatures of the kind; Defeated: of those, defeated in anyone's record.
    /// </summary>
    public sealed class FoeRow
    {
        public string Kind;
        public bool Boss;
        public int Count, Defeated;
        public readonly List<RowPart> Parts = new List<RowPart>();
        public const int MaxRows = 24;

        public RowPart Part(string name, bool self)
        {
            foreach (var p in Parts) if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p;
            var n = new RowPart { Name = name, Self = self }; Parts.Add(n); return n;
        }

        /// <summary>The rows of a group fight's creatures, one per kind, the bosses first, then most damage dealt.</summary>
        public static List<FoeRow> Of(IEnumerable<GroupFoe> foes)
        {
            var rows = new Dictionary<string, FoeRow>(StringComparer.Ordinal);
            foreach (var f in foes ?? Enumerable.Empty<GroupFoe>())
            {
                var key = (f.IsBoss ? "!" : "") + (f.Kind ?? "?");
                if (!rows.TryGetValue(key, out var r)) rows[key] = r = new FoeRow { Kind = f.Kind ?? "?", Boss = f.IsBoss };
                r.Count++; if (f.Defeated) r.Defeated++;
                foreach (var part in f.Parts)
                {
                    var rp = r.Part(part.Side.Name, part.Side.Self);
                    for (int t = 0; t < rp.Dealt.Length; t++) { rp.Dealt[t] += part.Encounter.Dealt[t]; rp.Received[t] += part.Encounter.Received[t]; }
                }
            }
            return Order(rows.Values);
        }

        internal static List<FoeRow> Order(IEnumerable<FoeRow> rows) =>
            rows.OrderByDescending(r => r.Boss).ThenByDescending(r => r.Parts.Sum(p => p.Dealt.Sum())).ThenBy(r => r.Kind, StringComparer.Ordinal).ToList();

        /// <summary>Grows <paramref name="into"/> with <paramref name="rows"/>: counts and every number only ever grow (a copy that dropped old
        /// foes never shrinks a saved record); returns whether anything changed.</summary>
        internal static bool Grow(List<FoeRow> into, IEnumerable<FoeRow> rows)
        {
            bool changed = false;
            foreach (var r in rows)
            {
                var have = into.FirstOrDefault(x => x.Kind == r.Kind && x.Boss == r.Boss);
                if (have == null) { if (into.Count >= MaxRows) continue; into.Add(have = new FoeRow { Kind = r.Kind, Boss = r.Boss }); changed = true; }
                if (r.Count > have.Count) { have.Count = r.Count; changed = true; }
                if (r.Defeated > have.Defeated) { have.Defeated = r.Defeated; changed = true; }
                foreach (var p in r.Parts)
                {
                    var hp = have.Parts.FirstOrDefault(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                    if (hp == null) { if (have.Parts.Count >= BossLog.MaxPlayers) continue; have.Parts.Add(hp = new RowPart { Name = p.Name, Self = p.Self }); changed = true; }
                    for (int t = 0; t < hp.Dealt.Length; t++)
                    {
                        if (p.Dealt[t] > hp.Dealt[t]) { hp.Dealt[t] = p.Dealt[t]; changed = true; }
                        if (p.Received[t] > hp.Received[t]) { hp.Received[t] = p.Received[t]; changed = true; }
                    }
                }
            }
            if (changed) { var sorted = Order(into); into.Clear(); into.AddRange(sorted); }
            return changed;
        }

        internal static void Write(Json j, string key, List<FoeRow> rows)
        {
            j.Key(key).OpenArr();
            foreach (var r in rows)
            {
                j.Open().Str("k", r.Kind ?? "?").Num("n", r.Count);
                if (r.Boss) j.Num("b", 1);
                if (r.Defeated > 0) j.Num("df", r.Defeated);
                j.Key("p").OpenArr();
                foreach (var p in r.Parts) { j.Open().Str("n", p.Name ?? ""); if (p.Self) j.Num("me", 1); FeedShare.Types(j, "d", p.Dealt); FeedShare.Types(j, "r", p.Received); j.Close(); }
                j.CloseArr().Close();
            }
            j.CloseArr();
        }

        internal static List<FoeRow> Read(Dictionary<string, object> x, string key)
        {
            var rows = new List<FoeRow>();
            if (x == null || !x.TryGetValue(key, out var v) || !(v is List<object> list)) return rows;
            foreach (var o in list.OfType<Dictionary<string, object>>().Take(MaxRows))
            {
                var r = new FoeRow { Kind = MiniJson.Str(o, "k", "?"), Boss = MiniJson.Num(o, "b") > 0, Count = (int)Math.Max(0, Math.Min(1e6, MiniJson.Num(o, "n"))), Defeated = (int)Math.Max(0, Math.Min(1e6, MiniJson.Num(o, "df"))) };
                if (o.TryGetValue("p", out var ps) && ps is List<object> parts)
                    foreach (var p in parts.OfType<Dictionary<string, object>>().Take(BossLog.MaxPlayers))
                    {
                        var rp = new RowPart { Name = MiniJson.Str(p, "n"), Self = MiniJson.Num(p, "me") > 0 };
                        FeedShare.ReadTypes(p, "d", rp.Dealt); FeedShare.ReadTypes(p, "r", rp.Received);
                        r.Parts.Add(rp);
                    }
                rows.Add(r);
            }
            return rows;
        }
    }

    /// <summary>
    /// One of your deaths (0.9, "falls" on a run's time line): when, in which fight, boss fight and run, and what hit you in its last seconds
    /// (BattleRecorder.FallWindow, the 10 s the game's own death log looks back too) per damage type after your armour, with the foe whose hit
    /// came last ("what took you down"). Only foes' hits are recorded: a fall, drowning or a player leave By null and Received as far as foes went.
    /// </summary>
    public sealed class Fall
    {
        public DateTime Utc;
        /// <summary>The recorder fight, boss fight (BossFight.Number) and dungeon run (DungeonRun.Number) it happened in; 0: none.</summary>
        public int Fight, Boss, Run;
        /// <summary>Damage taken from foes in the FallWindow before it, per type (BattleRecorder.Types order), after your armour.</summary>
        public readonly float[] Received = new float[BattleRecorder.Types.Length];
        /// <summary>The prefab of the foe whose hit came last ("SeekerQueen"); null: no foe's hit in the window. ByBoss: it was a boss.</summary>
        public string By;
        public bool ByBoss;
        public float ReceivedTotal { get { float s = 0; foreach (var v in Received) s += v; return s; } }
    }

    /// <summary>One fight inside a dungeon run, for its time line: the recorder fight's number and when it began and ended in the run; Boss: a
    /// boss fight's foe was in it (drawn as the gold span).</summary>
    public sealed class RunSpan
    {
        public int Fight;
        public DateTime StartUtc, EndUtc;
        public bool Boss;
    }

    /// <summary>
    /// One dungeon run (0.9): from entering an interior (crypts, burial chambers, frost caves, infested mines, the Queen's citadel ...) to leaving it.
    /// Stepping out for less than BattleRecorder.RunBreak and back into the same one goes on with the run (repair, unload, a corpse run from close by).
    /// Kind: the game's name of the place you entered through (its location prefab: "Crypt2", "SunkenCrypt4", "MountainCave02",
    /// "Mistlands_DvergrTownEntrance1", "Mistlands_DvergrBossEntrance1"), never where it is.
    /// </summary>
    public sealed class DungeonRun
    {
        public int Number;
        /// <summary>The place's game name (its location prefab, see above), and its own name token when the game gives it one (Location.m_discoverLabel,
        /// the label the game itself uses for a found place: "$location_dvergrboss" = "Infested Citadel", "$location_dvergrtown" = "Infested Mine"
        /// in the game's string table); null: none, name it from Kind.</summary>
        public string Kind, Token;
        public DateTime StartUtc, EndUtc;
        /// <summary>Seconds inside (a step out is not counted).</summary>
        public float Inside;
        /// <summary>You are inside now.</summary>
        public bool Open;
        /// <summary>Your deaths in it (each a Fall with Run = Number); the foes you fought in it (each creature once); the latest boss fight in it (0: none).</summary>
        public int Deaths, Foes, Boss;
        /// <summary>Its time line: the fights in it (at most MaxSpans, the newest), oldest first.</summary>
        public readonly List<RunSpan> Spans = new List<RunSpan>();
        public const int MaxSpans = 60;
        public TimeSpan Length => EndUtc - StartUtc;
        /// <summary>The interior's zone (Spot.Zone): on your PC only, so a step out and back finds the same run. Never shared or saved.</summary>
        internal long Place;
    }

    /// <summary>One player's part in a saved boss attempt: damage dealt and received per type (BattleRecorder.Types order), both over the boss
    /// and its arena; deaths, blocks and parries in it. Only you and players who share (they have a feed); anyone else is counted in
    /// BossAttempt.Unnamed.</summary>
    public sealed class AttemptPlayer
    {
        public string Name;
        public bool Self;
        public readonly float[] Dealt = new float[BattleRecorder.Types.Length], Received = new float[BattleRecorder.Types.Length];
        public int Deaths, Blocks, Parries;
        public float DealtTotal { get { float s = 0; foreach (var v in Dealt) s += v; return s; } }
        public float ReceivedTotal { get { float s = 0; foreach (var v in Received) s += v; return s; } }
    }

    /// <summary>One boss attempt you took part in, as the group's record showed it (GroupFights.Join over your recorder and the fellows' feeds).</summary>
    public sealed class BossAttempt
    {
        /// <summary>0.8.5: the world it was fought in (FellowCache.ScopeKey); null: saved before 0.8.5 and not yet adopted (BossLog.Adopt).</summary>
        public string World;
        /// <summary>The first boss's prefab and its network id as text (FoeId.ToString): with StartUtc the attempt's identity across saves.</summary>
        public string Kind, Lead;
        public DateTime StartUtc;
        public float Seconds;
        public BossOutcome Outcome;
        /// <summary>The boss's health at the last sign of fighting (0..1; 0 when won), -1: not seen; the dungeon it was fought in (its kind and name
        /// token), null: outside.</summary>
        public float Health = -1f;
        /// <summary>The lowest health the boss reached in it (0..1; 0 when won), -1: not seen. "Reset at N %".</summary>
        public float Lowest = -1f;
        public string Place, PlaceToken;
        public readonly List<AttemptPlayer> Players = new List<AttemptPlayer>();
        /// <summary>Its foe rows (the boss first, then the arena's kinds), each with every sharing player's part: the attempt's page after a restart.</summary>
        public readonly List<FoeRow> Foes = new List<FoeRow>();
        public string Words => BossFight.Say(Outcome, Lowest);
        /// <summary>Players in it who do not share: counted, never named.</summary>
        public int Unnamed;
    }

    /// <summary>
    /// The boss attempts kept on this PC (0.9, LocalTotals top-level "bossFights", schema 6), oldest first. Note takes a group fight once its boss
    /// fight is over and you were in it; the same attempt noted again (the fellows' copies arrive 10 s or more after the kill) grows it, never
    /// shrinks it, and a win sticks. 0.8.5 (the lead, after v085-lastfight: another world never shows the first world's attempts): each attempt
    /// carries its world, the pages read one world's (Of), and the log keeps the newest MaxAttempts of each world, for the KeptFights.MaxWorlds
    /// worlds noted last. JSON: [{"k":"SeekerQueen","id":"123:456","s":"ISO","t":312.5,"o":1,"hp":0,"w":"Midgard-123",
    /// "pl":"Mistlands_DvergrBossEntrance1","pt":"$location_dvergrboss","u":1,"p":[{"n":"Edda","me":1,"d":[0,52.3],"r":[0,0,15,0,0,0,20],"dd":1,
    /// "bk":4,"pa":1}]}] (o BossOutcome; d and r per type up to the last with damage; hp, w, pl, pt, u, me, dd, bk, pa only when there).
    /// </summary>
    public sealed class BossLog
    {
        public const int MaxAttempts = 50, MaxPlayers = 16;
        public readonly List<BossAttempt> Attempts = new List<BossAttempt>();
        public bool IsEmpty => Attempts.Count == 0;

        /// <summary>0.8.5: the attempts of one world (null: those not yet adopted), oldest first, as the pages read them. Shares the attempts: read only.</summary>
        public BossLog Of(string world)
        {
            var log = new BossLog();
            log.Attempts.AddRange(Attempts.Where(a => a.World == world));
            return log;
        }

        /// <summary>0.8.5: attempts saved before 0.8.5, with no world, go to <paramref name="world"/> (Plugin: the world of your last save before this
        /// version, else the one you play now); returns whether any did.</summary>
        public bool Adopt(string world)
        {
            var changed = false;
            foreach (var a in Attempts) if (a.World == null && world != null) { a.World = world; changed = true; }
            if (changed) Trim(Attempts, x => x.World, x => x.StartUtc, MaxAttempts);
            return changed;
        }

        /// <summary>0.8.5 (review, B23): sharing switched off: each fellow's part of every attempt goes (their row and their part of each foe row);
        /// yours stays. Returns whether any went.</summary>
        public bool ForgetFellows()
        {
            var gone = 0;
            foreach (var a in Attempts) gone += a.Players.RemoveAll(p => !p.Self) + ForgetFellowParts(a.Foes);
            return gone > 0;
        }

        /// <summary>The fellows' parts of foe rows gone (BossLog, RunLog.ForgetFellows); how many went.</summary>
        internal static int ForgetFellowParts(List<FoeRow> rows) => rows.Sum(r => r.Parts.RemoveAll(p => !p.Self));

        /// <summary>Keeps <paramref name="g"/>'s boss fight when it is over and you were in it, in <paramref name="world"/>; returns whether the log changed.</summary>
        public bool Note(GroupFight g, string world = null)
        {
            var b = g?.Boss;
            if (b == null || b.Outcome == BossOutcome.Fighting || b.Bosses.Count == 0) return false;
            var mine = g.Players.FirstOrDefault(p => p.Self);
            if (mine == null || !mine.InBoss) return false;
            var ids = new HashSet<string>(b.Bosses.Select(id => id.ToString()), StringComparer.Ordinal);
            var a = Attempts.LastOrDefault(x => x.World == world && ids.Contains(x.Lead) && x.StartUtc <= b.EndUtc + BattleRecorder.BossGap && b.StartUtc <= x.StartUtc.AddSeconds(x.Seconds) + BattleRecorder.BossGap);
            bool changed = false;
            if (a == null)
            {
                a = new BossAttempt { World = world, Kind = b.Kind, Lead = b.Bosses[0].ToString(), StartUtc = b.StartUtc };
                Attempts.Add(a); changed = true;
                Trim(Attempts, x => x.World, x => x.StartUtc, MaxAttempts);
            }
            var secs = (float)Math.Max(0, (b.EndUtc - a.StartUtc).TotalSeconds);
            if (b.StartUtc < a.StartUtc) { secs += (float)(a.StartUtc - b.StartUtc).TotalSeconds; a.StartUtc = b.StartUtc; changed = true; }
            if (secs > a.Seconds) { a.Seconds = secs; changed = true; }
            if (a.Outcome != BossOutcome.Won && b.Outcome != a.Outcome && !(a.Outcome == BossOutcome.Reset && b.Outcome == BossOutcome.GotAway)) { a.Outcome = b.Outcome; changed = true; }   // a win sticks; a known reset is not unknown again
            var hp = a.Outcome == BossOutcome.Won ? 0f : b.Health;
            if (hp >= 0f && (a.Health < 0f || hp != a.Health)) { a.Health = hp; changed = true; }
            var lo = a.Outcome == BossOutcome.Won ? 0f : b.Lowest;
            if (lo >= 0f && (a.Lowest < 0f || lo < a.Lowest)) { a.Lowest = lo; changed = true; }
            if (FoeRow.Grow(a.Foes, FoeRow.Of(b.Foes))) changed = true;
            if (a.Place == null && b.Place != null) { a.Place = b.Place; a.PlaceToken = b.PlaceToken; changed = true; }
            if (b.Unnamed > a.Unnamed) { a.Unnamed = b.Unnamed; changed = true; }
            foreach (var gp in g.Players.Where(p => p.InBoss))
            {
                var ap = a.Players.FirstOrDefault(p => string.Equals(p.Name, gp.Name, StringComparison.OrdinalIgnoreCase));
                if (ap == null) { if (a.Players.Count >= MaxPlayers) continue; ap = new AttemptPlayer { Name = gp.Name, Self = gp.Self }; a.Players.Add(ap); changed = true; }
                for (int t = 0; t < ap.Dealt.Length; t++)   // a copy that dropped old foes never shrinks it
                {
                    if (gp.BossDealt[t] > ap.Dealt[t]) { ap.Dealt[t] = gp.BossDealt[t]; changed = true; }
                    if (gp.BossReceived[t] > ap.Received[t]) { ap.Received[t] = gp.BossReceived[t]; changed = true; }
                }
                if (gp.BossDeaths > ap.Deaths) { ap.Deaths = gp.BossDeaths; changed = true; }
                if (gp.BossBlocks > ap.Blocks) { ap.Blocks = gp.BossBlocks; changed = true; }
                if (gp.BossParries > ap.Parries) { ap.Parries = gp.BossParries; changed = true; }
            }
            if (changed) a.Players.Sort((x, y) => y.DealtTotal.CompareTo(x.DealtTotal));
            return changed;
        }

        public void WriteTo(Json j, string key = "bossFights")
        {
            var inv = CultureInfo.InvariantCulture;
            j.Key(key).OpenArr();
            foreach (var a in Attempts)
            {
                j.Open().Str("k", a.Kind ?? "?").Str("id", a.Lead ?? "").Str("s", a.StartUtc.ToString("o", inv)).Num("t", a.Seconds).Num("o", (int)a.Outcome);
                if (a.World != null) j.Str("w", a.World);
                if (a.Health >= 0f) j.Num("hp", Math.Round(a.Health, 3));
                if (a.Lowest >= 0f) j.Num("lo", Math.Round(a.Lowest, 3));
                if (!string.IsNullOrEmpty(a.Place)) j.Str("pl", a.Place);
                if (!string.IsNullOrEmpty(a.PlaceToken)) j.Str("pt", a.PlaceToken);
                if (a.Unnamed > 0) j.Num("u", a.Unnamed);
                j.Key("p").OpenArr();
                foreach (var p in a.Players)
                {
                    j.Open().Str("n", p.Name ?? "");
                    if (p.Self) j.Num("me", 1);
                    FeedShare.Types(j, "d", p.Dealt); FeedShare.Types(j, "r", p.Received);
                    if (p.Deaths > 0) j.Num("dd", p.Deaths);
                    if (p.Blocks > 0) j.Num("bk", p.Blocks);
                    if (p.Parries > 0) j.Num("pa", p.Parries);
                    j.Close();
                }
                j.CloseArr();
                FoeRow.Write(j, "f", a.Foes);
                j.Close();
            }
            j.CloseArr();
        }

        /// <summary>Reads what WriteTo wrote (the array itself; null: an empty log). Unreadable entries are skipped; numbers clipped.</summary>
        public static BossLog ReadFrom(object o)
        {
            var log = new BossLog();
            if (!(o is List<object> list)) return log;
            foreach (var x in list.OfType<Dictionary<string, object>>().Skip(Math.Max(0, list.Count - MaxAttempts * KeptFights.MaxWorlds)))
            {
                if (!DateTime.TryParse(MiniJson.Str(x, "s"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var s)) continue;
                var a = new BossAttempt { World = MiniJson.Str(x, "w", null), Kind = MiniJson.Str(x, "k", "?"), Lead = MiniJson.Str(x, "id"), StartUtc = s.ToUniversalTime(), Seconds = Clip(MiniJson.Num(x, "t"), 1e7f),
                                          Outcome = (BossOutcome)Math.Min((int)Clip(MiniJson.Num(x, "o"), 9), (int)BossOutcome.GotAway),
                                          Health = x.ContainsKey("hp") ? Math.Min(1f, Clip(MiniJson.Num(x, "hp"), 1f)) : -1f, Lowest = x.ContainsKey("lo") ? Math.Min(1f, Clip(MiniJson.Num(x, "lo"), 1f)) : -1f,
                                          Unnamed = (int)Clip(MiniJson.Num(x, "u"), 1000) };
                a.Foes.AddRange(FoeRow.Read(x, "f"));
                var pl = MiniJson.Str(x, "pl", null); if (!string.IsNullOrEmpty(pl)) a.Place = pl;
                var pt = MiniJson.Str(x, "pt", null); if (!string.IsNullOrEmpty(pt)) a.PlaceToken = pt;
                if (x.TryGetValue("p", out var ps) && ps is List<object> players)
                    foreach (var p in players.OfType<Dictionary<string, object>>().Take(MaxPlayers))
                    {
                        var ap = new AttemptPlayer { Name = MiniJson.Str(p, "n"), Self = MiniJson.Num(p, "me") > 0, Deaths = (int)Clip(MiniJson.Num(p, "dd"), 1000),
                                                     Blocks = (int)Clip(MiniJson.Num(p, "bk"), 1e6f), Parries = (int)Clip(MiniJson.Num(p, "pa"), 1e6f) };
                        FeedShare.ReadTypes(p, "d", ap.Dealt); FeedShare.ReadTypes(p, "r", ap.Received);
                        a.Players.Add(ap);
                    }
                log.Attempts.Add(a);
            }
            return log;
        }

        internal static float Clip(double v, float max) => Json.IsFinite(v) ? (float)Math.Max(0, Math.Min(max, v)) : 0f;

        /// <summary>0.8.5: a log (oldest first) to its bounds: the newest <paramref name="max"/> of each world, the KeptFights.MaxWorlds worlds whose
        /// newest entry is newest.</summary>
        internal static void Trim<T>(List<T> log, Func<T, string> world, Func<T, DateTime> at, int max)
        {
            var worlds = log.GroupBy(world).OrderByDescending(g => g.Max(at)).Select(g => g.Key).Take(KeptFights.MaxWorlds).ToList();
            log.RemoveAll(x => !worlds.Contains(world(x)));
            foreach (var w in worlds) { var n = log.Count(x => world(x) == w); for (int i = 0; n > max && i < log.Count; ) if (world(log[i]) == w) { log.RemoveAt(i); n--; } else i++; }
        }
    }

    /// <summary>One player's part of a saved run: their own seconds inside, deaths, foes, and damage dealt and received per type in it.</summary>
    public sealed class RunPlayer
    {
        public string Name;
        public bool Self;
        public float Inside;
        public int Deaths, Foes;
        public readonly float[] Dealt = new float[BattleRecorder.Types.Length], Received = new float[BattleRecorder.Types.Length];
    }

    /// <summary>One dungeon run you were in, as the group's record showed it (GroupFights.JoinRuns), kept so Runs and bosses lists it after a restart.</summary>
    public sealed class RunRecord
    {
        /// <summary>0.8.5: the world it was in (FellowCache.ScopeKey); null: saved before 0.8.5 and not yet adopted (RunLog.Adopt).</summary>
        public string World;
        /// <summary>The place's game name and name token (DungeonRun.Kind, Token).</summary>
        public string Kind, Token;
        public DateTime StartUtc;
        /// <summary>From the first of the group in to the last out.</summary>
        public float Seconds;
        /// <summary>Deaths of everyone in it; creatures fought in it, each once.</summary>
        public int Deaths, Foes;
        /// <summary>The boss fight in it: its first boss's id and start (a saved BossAttempt's Lead and StartUtc); null: none.</summary>
        public string BossLead;
        public DateTime BossStartUtc = DateTime.MinValue;
        public readonly List<RunPlayer> Players = new List<RunPlayer>();
        /// <summary>Its foe rows, one per kind met in it (a boss first), each with every sharing player's part.</summary>
        public readonly List<FoeRow> Rows = new List<FoeRow>();
        /// <summary>Its time line, merged over the group: the stretches of fighting, a boss fight's flagged, oldest first (at most MaxSpans).</summary>
        public readonly List<RunSpan> Spans = new List<RunSpan>();
        public const int MaxSpans = 30;
    }

    /// <summary>
    /// The dungeon runs you were in, kept on this PC (0.9, LocalTotals top-level "dungeonRuns", schema 6), oldest first, each noted as it goes (a
    /// run still on after a restart shows as it was last saved) and grown, never shrunk, as the fellows' copies catch up. 0.8.5: per world as the
    /// BossLog (each run's "w"; Of, Adopt), the newest MaxRuns of each world.
    /// JSON: [{"k":"Crypt2","w":"Midgard-123","tk":"$location_crypt","s":"ISO","t":1080,"dd":2,"n":31,"bl":"123:456","bs":"ISO","p":[{"n":"Edda","me":1,"in":400,"dd":1,
    /// "fo":12,"d":[..],"r":[..]}],"f":[foe rows as BossLog],"sp":[[0,70],[300,520,1]]}] (sp: seconds from s; 1 = the boss fight's).
    /// </summary>
    public sealed class RunLog
    {
        public const int MaxRuns = 50;
        public readonly List<RunRecord> Runs = new List<RunRecord>();
        public bool IsEmpty => Runs.Count == 0;

        /// <summary>0.8.5: the runs of one world (null: those not yet adopted), oldest first, as the pages read them. Shares the runs: read only.</summary>
        public RunLog Of(string world)
        {
            var log = new RunLog();
            log.Runs.AddRange(Runs.Where(r => r.World == world));
            return log;
        }

        /// <summary>0.8.5: runs saved before 0.8.5, with no world, go to <paramref name="world"/> (as BossLog.Adopt); returns whether any did.</summary>
        public bool Adopt(string world)
        {
            var changed = false;
            foreach (var r in Runs) if (r.World == null && world != null) { r.World = world; changed = true; }
            if (changed) BossLog.Trim(Runs, x => x.World, x => x.StartUtc, MaxRuns);
            return changed;
        }

        /// <summary>0.8.5 (review, B23): sharing switched off: each fellow's part of every run goes, as BossLog.ForgetFellows; yours stays.</summary>
        public bool ForgetFellows()
        {
            var gone = 0;
            foreach (var r in Runs) gone += r.Players.RemoveAll(p => !p.Self) + BossLog.ForgetFellowParts(r.Rows);
            return gone > 0;
        }

        /// <summary>Keeps or grows <paramref name="r"/> when you were in it, in <paramref name="world"/>; returns whether the log changed.</summary>
        public bool Note(GroupRun r, string world = null)
        {
            if (r == null || !r.Players.Any(w => w.Self)) return false;
            var rec = Runs.LastOrDefault(x => x.World == world && x.Kind == r.Kind && x.StartUtc <= r.EndUtc + BattleRecorder.RunBreak && r.StartUtc <= x.StartUtc.AddSeconds(x.Seconds) + BattleRecorder.RunBreak);
            bool changed = false;
            if (rec == null)
            {
                rec = new RunRecord { World = world, Kind = r.Kind, StartUtc = r.StartUtc };
                Runs.Add(rec); changed = true;
                BossLog.Trim(Runs, x => x.World, x => x.StartUtc, MaxRuns);
            }
            if (rec.Token == null && r.Token != null) { rec.Token = r.Token; changed = true; }
            if (r.StartUtc < rec.StartUtc) { rec.Seconds += (float)(rec.StartUtc - r.StartUtc).TotalSeconds; rec.StartUtc = r.StartUtc; changed = true; }
            var secs = (float)Math.Max(0, (r.EndUtc - rec.StartUtc).TotalSeconds);
            if (secs > rec.Seconds) { rec.Seconds = secs; changed = true; }
            var deaths = r.Players.Sum(w => w.Deaths);
            if (deaths > rec.Deaths) { rec.Deaths = deaths; changed = true; }
            if (r.Foes > rec.Foes) { rec.Foes = r.Foes; changed = true; }
            var boss = r.BossSpans.OrderBy(b => b.StartUtc).FirstOrDefault();
            if (boss != null && rec.BossLead == null) { rec.BossLead = boss.Lead.ToString(); rec.BossStartUtc = boss.StartUtc; changed = true; }
            foreach (var w in r.Players)
            {
                var p = rec.Players.FirstOrDefault(x => string.Equals(x.Name, w.Name, StringComparison.OrdinalIgnoreCase));
                if (p == null) { if (rec.Players.Count >= BossLog.MaxPlayers) continue; rec.Players.Add(p = new RunPlayer { Name = w.Name, Self = w.Self }); changed = true; }
                if (w.Inside > p.Inside) { p.Inside = w.Inside; changed = true; }
                if (w.Deaths > p.Deaths) { p.Deaths = w.Deaths; changed = true; }
                if (w.Foes > p.Foes) { p.Foes = w.Foes; changed = true; }
                for (int t = 0; t < p.Dealt.Length; t++)
                {
                    if (w.Dealt[t] > p.Dealt[t]) { p.Dealt[t] = w.Dealt[t]; changed = true; }
                    if (w.Received[t] > p.Received[t]) { p.Received[t] = w.Received[t]; changed = true; }
                }
            }
            if (FoeRow.Grow(rec.Rows, r.Rows)) changed = true;
            var spans = GroupFights.Merge(r).Skip(0).ToList();
            if (spans.Count > rec.Spans.Count || (spans.Count > 0 && rec.Spans.Count > 0 && spans[spans.Count - 1].EndUtc > rec.Spans[rec.Spans.Count - 1].EndUtc))
            {
                rec.Spans.Clear(); rec.Spans.AddRange(spans.Skip(Math.Max(0, spans.Count - RunRecord.MaxSpans))); changed = true;
            }
            return changed;
        }

        public void WriteTo(Json j, string key = "dungeonRuns")
        {
            var inv = CultureInfo.InvariantCulture;
            j.Key(key).OpenArr();
            foreach (var r in Runs)
            {
                j.Open().Str("k", r.Kind ?? "?");
                if (r.World != null) j.Str("w", r.World);
                if (!string.IsNullOrEmpty(r.Token)) j.Str("tk", r.Token);
                j.Str("s", r.StartUtc.ToString("o", inv)).Num("t", Math.Round(r.Seconds)).Num("dd", r.Deaths).Num("n", r.Foes);
                if (r.BossLead != null) j.Str("bl", r.BossLead).Str("bs", r.BossStartUtc.ToString("o", inv));
                j.Key("p").OpenArr();
                foreach (var p in r.Players)
                {
                    j.Open().Str("n", p.Name ?? "");
                    if (p.Self) j.Num("me", 1);
                    j.Num("in", Math.Round(p.Inside));
                    if (p.Deaths > 0) j.Num("dd", p.Deaths);
                    if (p.Foes > 0) j.Num("fo", p.Foes);
                    FeedShare.Types(j, "d", p.Dealt); FeedShare.Types(j, "r", p.Received);
                    j.Close();
                }
                j.CloseArr();
                FoeRow.Write(j, "f", r.Rows);
                j.Raw("sp", "[" + string.Join(",", r.Spans.Select(sp => "[" + Math.Round((sp.StartUtc - r.StartUtc).TotalSeconds).ToString(inv) + "," + Math.Round((sp.EndUtc - r.StartUtc).TotalSeconds).ToString(inv) + (sp.Boss ? ",1" : "") + "]").ToArray()) + "]");
                j.Close();
            }
            j.CloseArr();
        }

        /// <summary>Reads what WriteTo wrote (the array itself; null: an empty log). Unreadable entries are skipped; numbers clipped.</summary>
        public static RunLog ReadFrom(object o)
        {
            var log = new RunLog();
            if (!(o is List<object> list)) return log;
            foreach (var x in list.OfType<Dictionary<string, object>>().Skip(Math.Max(0, list.Count - MaxRuns * KeptFights.MaxWorlds)))
            {
                if (!DateTime.TryParse(MiniJson.Str(x, "s"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var s)) continue;
                var r = new RunRecord { World = MiniJson.Str(x, "w", null), Kind = MiniJson.Str(x, "k", "?"), StartUtc = s.ToUniversalTime(), Seconds = BossLog.Clip(MiniJson.Num(x, "t"), 1e7f),
                                        Deaths = (int)BossLog.Clip(MiniJson.Num(x, "dd"), 1e5f), Foes = (int)BossLog.Clip(MiniJson.Num(x, "n"), 1e6f) };
                var tk = MiniJson.Str(x, "tk", null); if (!string.IsNullOrEmpty(tk)) r.Token = tk;
                var bl = MiniJson.Str(x, "bl", null);
                if (!string.IsNullOrEmpty(bl) && DateTime.TryParse(MiniJson.Str(x, "bs"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var bs)) { r.BossLead = bl; r.BossStartUtc = bs.ToUniversalTime(); }
                if (x.TryGetValue("p", out var ps) && ps is List<object> players)
                    foreach (var p in players.OfType<Dictionary<string, object>>().Take(BossLog.MaxPlayers))
                    {
                        var rp = new RunPlayer { Name = MiniJson.Str(p, "n"), Self = MiniJson.Num(p, "me") > 0, Inside = BossLog.Clip(MiniJson.Num(p, "in"), 1e7f),
                                                 Deaths = (int)BossLog.Clip(MiniJson.Num(p, "dd"), 1e5f), Foes = (int)BossLog.Clip(MiniJson.Num(p, "fo"), 1e6f) };
                        FeedShare.ReadTypes(p, "d", rp.Dealt); FeedShare.ReadTypes(p, "r", rp.Received);
                        r.Players.Add(rp);
                    }
                r.Rows.AddRange(FoeRow.Read(x, "f"));
                if (x.TryGetValue("sp", out var sps) && sps is List<object> spans)
                    foreach (var sp in spans.OfType<List<object>>().Take(RunRecord.MaxSpans))
                        if (sp.Count >= 2 && sp[0] is double a && sp[1] is double z && Json.IsFinite(a) && Json.IsFinite(z) && Math.Abs(a) < 1e7 && Math.Abs(z) < 1e7)
                            r.Spans.Add(new RunSpan { StartUtc = r.StartUtc.AddSeconds(a), EndUtc = r.StartUtc.AddSeconds(Math.Max(a, z)), Boss = sp.Count > 2 && sp[2] is double bo && bo > 0 });
                log.Runs.Add(r);
            }
            return log;
        }
    }
}
