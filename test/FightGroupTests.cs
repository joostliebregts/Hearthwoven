// 0.9: fights as a group, boss fights and dungeon runs (src/GroupFight.cs, src/BossRecord.cs, BattleRecorder's boss and run parts;
// work/hearthwoven-0.8.5/fights/DESIGN.md). Four players fight The Queen in her citadel, each on their own PC, and their feeds join into one fight
// with each player's damage; the arena rule; a run from border to border; nothing of a place or a non-sharer in a shared copy; the bytes.
// Fictional players: Rowan, Edda, Finch and Tor share; Ylva plays along and does not.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FightGroupTests
{
    sealed class Pc
    {
        public string Name; public readonly BattleRecorder Rec = new BattleRecorder();
        public int From, To, InFrom, InTo;   // the seconds they fight the Queen, and are inside her citadel
        public HitData.DamageTypes Swing;
        public string Json; public FeedShare Copy; public DateTime CopyAt; public float[] Expect;   // their shared feed as a fellow reads it, and its boss part as their recorder had it
        public readonly DamageTally Dmg = new DamageTally(); public readonly EventLog Log = new EventLog();
    }
    sealed class Add { public FoeId Id; public string Kind, Token; public Spot At; public int Born, Hits; public bool Dead; public readonly List<Pc> By = new List<Pc>(); }
    /// <summary>The adapter's books of this fight, for the fight pages' own test (test/FightPagesRealTests.cs): the four feeds joined, the same after a
    /// restart (the saved records only), an open and a reset boss fight; Edda is the book's owner.</summary>
    public static FightBook RealPages, RealRestart; public static FightView RealLive, RealReset; public static DateTime RealNow; public static GroupFight RealGroup;

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var t0 = new DateTime(2026, 10, 10, 21, 0, 0, DateTimeKind.Utc).AddMilliseconds(400);
        DateTime T(double s) => t0.AddSeconds(s);
        const string Citadel = "Mistlands_DvergrBossEntrance1", CitadelLabel = "$location_dvergrboss", QueenKind = "SeekerQueen", QueenToken = "$enemy_seekerqueen";
        var queen = new FoeId(-81234, 1);
        var queenAt = new Spot(-2761.375f, 5033.875f, 3191.625f);   // inside: every dungeon stands 5000 m above its entrance
        var sharers = new HashSet<string>(new[] { "Rowan", "Edda", "Finch", "Tor" }, StringComparer.OrdinalIgnoreCase);
        var pcs = new[]
        {
            new Pc { Name = "Edda", From = 30, To = 200, InFrom = 5, InTo = 205, Swing = new HitData.DamageTypes { m_slash = 40 } },   // dies at 200, wakes outside
            new Pc { Name = "Rowan", From = 30, To = 300, InFrom = 6, InTo = 330, Swing = new HitData.DamageTypes { m_pierce = 55, m_fire = 6.25f } },
            new Pc { Name = "Tor", From = 30, To = 300, InFrom = 7, InTo = 330, Swing = new HitData.DamageTypes { m_blunt = 48 } },
            new Pc { Name = "Finch", From = 120, To = 300, InFrom = 110, InTo = 330, Swing = new HitData.DamageTypes { m_frost = 30, m_spirit = 12.5f } },   // joins mid-fight
        };
        Pc edda = pcs[0], rowan = pcs[1], tor = pcs[2], finch = pcs[3];
        float Health(double s) => s < 30 ? 1f : (float)Math.Max(0.0, 1.0 - (s - 30) / 270.0);
        bool Inside(Pc p, int s) => s >= p.InFrom && s < p.InTo;
        void Hit(Pc p, int s, FoeId id, string kind, string token, HitData.DamageTypes d, bool boss, Spot at)
        { p.Rec.Dealt(T(s), id, kind, token, "Mistlands", d, boss, at); p.Log.AddDamage(T(s), "Mistlands", true, kind, "Clubs", d); p.Dmg.AddDealt(kind, "Clubs", d); }
        void Took(Pc p, int s, FoeId id, string kind, string token, HitData.DamageTypes d, bool boss, Spot at)
        { p.Rec.Received(T(s), id, kind, token, "Mistlands", d, boss, at); p.Log.AddDamage(T(s), "Mistlands", false, kind, "EnemyHit", d); p.Dmg.AddTaken(kind, "EnemyHit", d); }
        // the game's kill: the creature's record names every player who hit it (Ylva too, when she did); the kill message reaches each of them, the
        // destroy every PC where it is loaded
        void Kill(double s, FoeId id, string token, List<Pc> by, bool ylva)
        {
            int n = by.Count + (ylva ? 1 : 0);
            foreach (var p in by)
            {
                var others = by.Where(o => o != p).Select(o => o.Name).Concat(ylva ? new[] { "Ylva" } : new string[0]).ToArray();
                p.Rec.Attackers(id, n, others); p.Rec.Killed(T(s), token, n);
            }
            foreach (var p in pcs) if (Inside(p, (int)s)) p.Rec.Destroyed(T(s + 0.5), id);
        }
        float[] BossPart(BattleRecorder r)
        {
            var sum = new float[BattleRecorder.Types.Length];
            foreach (var e in r.Fights().SelectMany(f => f.Entries).Where(e => e.Boss > 0)) for (int t = 0; t < sum.Length; t++) sum[t] += e.Dealt[t];
            return sum;
        }
        // a player's shared feed as a fellow reads it: written, made a shared copy (GroupShare.SharedCopy), read back
        (string json, FeedShare feed) Share(Pc p)
        {
            var j = new Json().Open(); FeedShare.From(p.Rec, sharers.Contains).WriteTo(j);
            var json = GroupShare.SharedCopy(j.Close().ToString());
            return (json, FeedShare.ReadFrom(MiniJson.Obj(MiniJson.Parse(json) as Dictionary<string, object>, "battleFeed")));
        }
        void Copy(Pc p, int s) { (p.Json, p.Copy) = Share(p); p.CopyAt = T(s + 2); p.Expect = BossPart(p.Rec); }

        // the adds: 12 Seekers and 40 brood come up near the Queen as the fight goes on; brood 7 comes up 55 m off, across the citadel, and only Tor fights it
        var adds = new List<Add>();
        for (int i = 0; i < 12; i++) adds.Add(new Add { Id = new FoeId(-81234, (uint)(100 + i)), Kind = "Seeker", Token = "$enemy_seeker", Born = 40 + 20 * i, At = new Spot(queenAt.X + (i * 7 % 31) - 15, 5030.5f, queenAt.Z + (i * 11 % 29) - 14) });
        for (int i = 0; i < 40; i++) adds.Add(new Add { Id = new FoeId(-81234, (uint)(200 + i)), Kind = "SeekerBrood", Token = "$enemy_seekerbrood", Born = 35 + 6 * i, At = i == 7 ? new Spot(-2722.875f, 5021.5f, 3229.25f) : new Spot(queenAt.X + (i * 5 % 27) - 13, 5031.25f, queenAt.Z + (i * 13 % 23) - 11) });
        var far = adds.First(a => a.Id.Id == 207);
        var queenBy = new List<Pc>();
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats();
        var skills = new List<Snapshot.SkillInfo> { new Snapshot.SkillInfo { Name = "Clubs", Level = 50, Accumulator = 1 } };
        string TorFull() => GroupShare.SharedCopy(Snapshot.Build("0.9.0", 79, "Tor", stats, skills, "Midgard", tor.Dmg, "s9", new SessionEvents(), tor.Log, true, copyId: "s9-1", feed: FeedShare.From(tor.Rec, sharers.Contains)));
        (float now, float ago, float low) minuteAgo = (-1, -1, -1);
        string torBase0 = null, torBase = null; var window = new List<int>(); var refused = new List<string>(); int whole = 0;
        bool Kept(LiveDelta.Made m) => m.Text != null && ServerIntake.IsJsonObject(m.Text) && LiveDelta.Read(m.Text, out _, out _) && !LiveDelta.Private(m.Text) && !LiveDelta.Identity(m.Text) && !m.TooBig;

        for (int s = 0; s <= 345; s++)
        {
            for (int k = 0; k < pcs.Length; k++)
            {
                var p = pcs[k]; bool inside = Inside(p, s);
                p.Rec.Inside(T(s), inside ? Citadel : null, inside ? queenAt.Zone : 0, inside ? CitadelLabel : null);   // BattleHooks.Watch, once a second
                if (inside && s < 300) p.Rec.BossSeen(T(s), queen, QueenKind, QueenToken, Health(s), queenAt);
                if (p == edda && s == 200) p.Rec.Died(T(s));
                p.Rec.Tick(T(s));
                if (s < p.From || s >= p.To) continue;
                if (s % 2 == 0) { Hit(p, s, queen, QueenKind, QueenToken, p.Swing, true, queenAt); if (!queenBy.Contains(p)) queenBy.Add(p); }
                if (s % 7 == k) Took(p, s, queen, QueenKind, QueenToken, new HitData.DamageTypes { m_poison = 20, m_pierce = 15 }, true, queenAt);
                if (s % 3 == 0)
                {
                    var alive = adds.Where(a => !a.Dead && a.Born <= s && (a != far || p == tor)).ToList();
                    if (p == tor && !far.Dead && far.Born <= s) alive = new List<Add> { far };
                    if (alive.Count == 0) continue;
                    var a = alive[(s / 3 + k) % alive.Count];
                    Hit(p, s, a.Id, a.Kind, a.Token, new HitData.DamageTypes { m_slash = 11.5f }, false, a.At);
                    if (!a.By.Contains(p)) a.By.Add(p);
                    if (++a.Hits >= 3) { a.Dead = true; Kill(s + 0.3, a.Id, a.Token, a.By, a.Id.Id % 5 == 0); }
                }
            }
            if (s == 0) torBase0 = TorFull();
            if (s == 120) torBase = TorFull();
            if (s > 120 && s <= 240 && s % 10 == 0)   // the default 2 min between full copies, at the busiest: a live update every 10 s
            {
                var made = LiveDelta.Make(torBase, TorFull(), "s9-1", (s - 120) / 10);
                window.Add(made.Packed?.Length ?? 0);
                if (!Kept(made)) refused.Add("s=" + s);
            }
            if (s == 200) { var tb = tor.Rec.BossFights().First(); minuteAgo = (tb.Health, tb.HealthMinuteAgo, tb.Lowest); }
            if (s == 290) Copy(rowan, s);   // Rowan's copy is 10 s behind the kill: as of 290
            if (s == 300) Kill(300, queen, QueenToken, queenBy, true);
            if (s == 340) { Copy(tor, s); Copy(finch, s); Copy(edda, s); whole = LiveDelta.Make(torBase0, TorFull(), "s9-1", 99).Packed?.Length ?? 0; }
        }
        var now = T(345);
        var sides = new List<FightSide> { FightSide.Of(edda.Rec, "Edda"), FightSide.Of(rowan.Copy, "Rowan", "k-rowan", rowan.CopyAt), FightSide.Of(tor.Copy, "Tor", "k-tor", tor.CopyAt), FightSide.Of(finch.Copy, "Finch", "k-finch", finch.CopyAt) };
        var fights = GroupFights.Join(sides, now);
        var g = fights.FirstOrDefault(); var b = g?.Boss;
        bool Near(float[] a, float[] x) => a != null && x != null && a.Zip(x, (u, v) => Math.Abs(u - v)).Max() < 0.05f;
        string Parts() => g == null ? "none" : string.Join(", ", g.Players.Select(p => p.Name + " " + Json.F(p.BossDealtTotal)));
        var mine = edda.Rec.BossFights().FirstOrDefault();
        var farFoe = g?.Foe(far.Id);
        var fall = g?.Player("Edda")?.Falls.SingleOrDefault();   // what took her down: the Queen's last hit, poison and pierce
        string Missed(params (bool ok, string what)[] parts) => string.Join(", ", parts.Where(x => !x.ok).Select(x => x.what));
        var missed = g == null || b == null ? "no boss fight" : Missed((fights.Count == 1, "one fight (" + fights.Count + ")"), (b.Kind == QueenKind && b.Outcome == BossOutcome.Won && b.Health == 0f, "won " + b.Outcome + " " + b.Health),
              (b.Place == Citadel, "place " + b.Place), (b.Bosses.SequenceEqual(new[] { queen }), "bosses"), (g.Players.Count == 4 && g.Players.All(p => p.InBoss), "players " + g.Players.Count),
              (Near(g.Player("Edda").BossDealt, BossPart(edda.Rec)), "Edda's part"), (Near(g.Player("Rowan").BossDealt, rowan.Expect), "Rowan's part"), (Near(g.Player("Tor").BossDealt, tor.Expect), "Tor's part"),
              (Near(g.Player("Finch").BossDealt, finch.Expect), "Finch's part"), (Math.Abs((g.Player("Finch").FirstUtc - T(120)).TotalSeconds) < 1, "Finch joined " + g.Player("Finch").FirstUtc.ToString("HH:mm:ss.f")),   // a shared feed's times are whole seconds
              (g.StartUtc <= T(30.5), "start"), (g.Player("Rowan").AsOfUtc == T(292) && g.Player("Rowan").LastUtc <= T(290), "Rowan as of"),
              (g.Player("Edda").BossDeaths == 1 && g.Player("Edda").Deaths == 1, "Edda's deaths " + g.Player("Edda").BossDeaths + "/" + g.Player("Edda").Deaths), (mine != null && mine.Outcome == BossOutcome.Won, "Edda's own outcome " + mine?.Outcome),
              (fall != null && fall.By == QueenKind && fall.ByBoss && fall.Received[2] == 15 && fall.Received[6] == 20 && fall.Boss == mine?.Number && fall.Run > 0, "Edda's fall " + fall?.By + " " + string.Join(",", fall?.Received ?? new float[0])),
              (b.PlaceToken == CitadelLabel, "place token " + b.PlaceToken),
              (farFoe != null && farFoe.InBoss && farFoe.Parts.Count == 1 && farFoe.Parts[0].Side.Name == "Tor", "far brood"), (b.Unnamed >= 1, "unnamed"),
              (b.Foes.Count == 1 + adds.Count(a => a.By.Count > 0), "foes " + b.Foes.Count + " of " + (1 + adds.Count(a => a.By.Count > 0))));
        Check(missed.Length == 0,
              (missed.Length > 0 ? "[" + missed + "] " : "") + "FG one fight from four feeds: four players' records of The Queen (her network id the same on every PC) join into one won boss fight in her citadel with each player's damage by type (" + Parts() +
              "); Finch joined at 2:00, Rowan's copy is 10 s behind, Edda died (her fall: what hit her in the last 10 s, the Queen took her down) and woke outside, and her PC still saw the win (the kill message); brood only Tor fought is in it through Tor; Ylva who does not share is counted");

        // ---------- older copies: a fellow's feed from before 0.9 carries no ids and stays out of the join ----------
        var oldFeed = MiniJson.Obj(MiniJson.Parse(System.Text.RegularExpressions.Regex.Replace(tor.Json, ",\"(i|bf|rn)\":(\"[^\"]*\"|[0-9]+)", "")) as Dictionary<string, object>, "battleFeed");
        oldFeed.Remove("boss"); oldFeed.Remove("runs");   // as 0.8.2 writes it
        var old = FeedShare.ReadFrom(oldFeed);
        var withOld = GroupFights.Join(new List<FightSide> { sides[0], sides[1], sides[3], FightSide.Of(old, "Tor", "k-tor", tor.CopyAt) }, now);
        Check(old.Fights.SelectMany(f => f.Entries).All(e => e.Id.IsNone) && withOld.Count == 2 && withOld[0].Players.Count == 3 && withOld.Any(x => x.Players.Count == 1 && x.Players[0].Name == "Tor"),
              "[" + old.Fights.SelectMany(f => f.Entries).Count(e => !e.Id.IsNone) + " ids, " + withOld.Count + " fights: " + string.Join(" | ", withOld.Select(x => string.Join("+", x.Players.Select(p => p.Name)))) + "] FG older copies: a feed without the 0.9 ids reads as before and stays a fight of its own (no join on a guess)");

        // ---------- the run: the group in the citadel, the boss fight in it ----------
        var runs = GroupFights.JoinRuns(sides, now);
        var run = runs.FirstOrDefault(); var eddaRun = edda.Rec.Runs().FirstOrDefault();
        var eddaWho = run?.Players.FirstOrDefault(w => w.Self);
        Check(runs.Count == 1 && run.Kind == Citadel && run.Token == CitadelLabel && run.Players.Count == 4 && eddaWho.Deaths == 1 && eddaWho.Falls.Count == 1 && eddaWho.Spans.Count >= 1 && run.Bosses.Contains(queen) &&
              run.BossSpans.Any(x => x.Kind == QueenKind && x.Outcome == BossOutcome.Won) && run.Players.All(w => w.Spans.Count >= 1) && Math.Abs(run.Players.First(w => w.Name == "Finch").Inside - 220) <= 2 &&
              eddaRun != null && eddaRun.StartUtc == T(5) && eddaRun.EndUtc == T(205) && Math.Abs(eddaRun.Inside - 200) <= 1 && eddaRun.Boss == mine.Number && mine.Run == eddaRun.Number && run.Foes >= 40,
              "FG group run: the four runs into the Infested Citadel ($location_dvergrboss) are one run of the group (" + run?.Foes + " foes); its time line: each player's fights and falls, The Queen's boss fight (won); Finch inside 220 s of it; Edda's own run 0:05 to 0:205, " + eddaRun?.Inside + " s inside");

        // ---------- the arena rule, outside: the Elder; a Greydwarf at 30 m is in the fight, one 200 m off and one hit before it are not ----------
        var elder = new FoeId(-55, 7); var elderAt = new Spot(812.5f, 41.25f, -1630.75f);
        var nearAt = new Spot(838.5f, 40f, -1615.75f); var farAt = new Spot(1012.5f, 44f, -1630.75f);
        FoeId gEarly = new FoeId(-55, 10), gNear = new FoeId(-55, 8), gFar = new FoeId(-55, 9);
        var slash = new HitData.DamageTypes { m_slash = 20 };
        var a1 = new BattleRecorder();   // Edda fights the Elder
        a1.Dealt(T(0), gEarly, "Greydwarf", "$enemy_greydwarf", "BlackForest", slash, false, nearAt);
        a1.Dealt(T(2), elder, "gd_king", "$enemy_gdking", "BlackForest", slash, true, elderAt);
        a1.Dealt(T(4), gNear, "Greydwarf", "$enemy_greydwarf", "BlackForest", slash, false, nearAt);
        a1.Dealt(T(6), gFar, "Greydwarf", "$enemy_greydwarf", "BlackForest", slash, false, farAt);
        var b1 = new BattleRecorder();   // Finch never hits the Elder: his PC sees its health drop, and he fights the Greydwarfs
        b1.BossSeen(T(0), elder, "gd_king", "$enemy_gdking", 1f, elderAt);
        b1.BossSeen(T(3), elder, "gd_king", "$enemy_gdking", 0.97f, elderAt);
        b1.Dealt(T(5), gNear, "Greydwarf", "$enemy_greydwarf", "BlackForest", slash, false, nearAt);
        var liveView = FightAdapter.Build(new List<FightSide> { FightSide.Of(b1, "Finch") }, null, null, T(6)).Fights.FirstOrDefault(f => f.Kind == FightKind.Boss);   // the pages, while it is on
        b1.Dealt(T(7), gFar, "Greydwarf", "$enemy_greydwarf", "BlackForest", slash, false, farAt);
        FoeEncounter E(BattleRecorder r, FoeId id) => r.Fights().SelectMany(f => f.Entries).FirstOrDefault(e => e.Id.Equals(id));
        var bfA = a1.BossFights().FirstOrDefault(); var bfB = b1.BossFights().FirstOrDefault();
        a1.Tick(T(100)); var onAt100 = a1.FightOn && bfA.Open;
        a1.Tick(T(130)); var quietOn = !a1.FightOn && bfA.Open;   // two quiet minutes end the recorder's fight, never the attempt
        a1.Tick(T(2 + 601)); var gotAway = bfA.Outcome == BossOutcome.GotAway;   // not seen for 10 min: ended, outcome not known
        a1.BossSeen(T(900), elder, "gd_king", "$enemy_gdking", 1f, elderAt); var learnedReset = bfA.Outcome == BossOutcome.Reset;   // later seen at full health: it reset
        for (int s = 4; s <= 40; s++) b1.BossSeen(T(s), elder, "gd_king", "$enemy_gdking", Math.Min(1f, 0.97f + (s - 3) * 0.001f), elderAt);   // healing back, nobody fighting
        var reset = bfB.Outcome == BossOutcome.Reset && Math.Abs(bfB.Lowest - 0.97f) < 0.001f && bfB.EndUtc == T(3) && bfB.Words == "Reset at 97 %";
        var resetView = FightAdapter.Build(new List<FightSide> { FightSide.Of(b1, "Finch") }, null, null, T(41)).Fights.FirstOrDefault(f => f.Kind == FightKind.Boss);
        var queenFoes = edda.Rec.Fights().SelectMany(f => f.Entries).Where(e => e.Kind != QueenKind).ToList();
        Check(bfA != null && E(a1, gNear).Boss == bfA.Number && E(a1, gFar).Boss == 0 && E(a1, gEarly).Boss == 0 && E(a1, elder).IsBoss && bfA.Foes == 2 &&
              bfB != null && bfB.Kind == "gd_king" && bfB.Fight == 1 && E(b1, gNear).Boss == bfB.Number && E(b1, gFar).Boss == 0 &&
              tor.Rec.Fights().SelectMany(f => f.Entries).First(e => e.Id.Equals(far.Id)).Boss > 0 && queenFoes.Count > 0 && queenFoes.All(e => e.Boss > 0) && onAt100 && quietOn && gotAway && learnedReset && reset,
              "FG arena: a foe hit within " + BattleRecorder.ArenaMetres + " m of a boss is in its fight, one 200 m off or hit before the boss is not; inside, the whole interior is the arena (brood 55 m off across the citadel); a PC that only saw the boss's health drop has the fight too; " +
              "the fight stays open while its boss fight is active (100 s without a hit); an attempt ends only on the kill, on the boss seen back at full health (Reset, at the health it had when the fighting stopped: 97 %), " +
              "or after " + BattleRecorder.GotAwayAfter.TotalMinutes + " min unseen (Got away, turned into Reset when she is seen full later); never \"lost\"");

        // ---------- a run, border to border ----------
        var r = new BattleRecorder(); var crypt = new Spot(-1200.25f, 5010f, 455.75f); var crypt2 = new Spot(300.5f, 5008f, -2900.5f);
        for (int s = 0; s < 10; s++) r.Inside(T(s), null, 0);
        r.Dealt(T(5), new FoeId(9, 1), "Skeleton", "$enemy_skeleton", "BlackForest", slash);
        for (int s = 10; s < 100; s++)
        {
            r.Inside(T(s), "Crypt2", crypt.Zone);
            if (s == 50) r.Dealt(T(s), new FoeId(9, 2), "Skeleton", "$enemy_skeleton", "BlackForest", slash);
            if (s == 55) r.Received(T(s), new FoeId(9, 2), "Skeleton", "$enemy_skeleton", "BlackForest", new HitData.DamageTypes { m_slash = 31, m_poison = 4 });
            if (s == 60) r.Died(T(s));
            if (s == 95) r.Dealt(T(s), new FoeId(9, 2), "Skeleton", "$enemy_skeleton", "BlackForest", slash);
        }
        r.Inside(T(100), null, 0);
        r.Dealt(T(120), new FoeId(9, 3), "Skeleton", "$enemy_skeleton", "BlackForest", slash);
        var first = r.Runs().First(); bool closedAt100 = !first.Open && first.EndUtc == T(100) && first.StartUtc == T(10);
        for (int s = 150; s < 160; s++) r.Inside(T(s), "Crypt2", crypt.Zone);   // back in within RunBreak: the same run
        r.Inside(T(160), null, 0);
        for (int s = 600; s < 610; s++) r.Inside(T(s), "Crypt2", crypt.Zone);   // back after more than RunBreak: the next run
        for (int s = 610; s < 615; s++) r.Inside(T(s), "Crypt2", crypt2.Zone);   // another crypt of the same kind: another run
        var all = r.Runs(); string RunOf(uint n) => E(r, new FoeId(9, n))?.Run.ToString();
        var rf = r.Falls().Single();
        Check(closedAt100 && RunOf(1) == "0" && RunOf(2) == "1" && RunOf(3) == "0" && first.Foes == 1 && first.Deaths == 1 && Math.Abs(first.Inside - 89 - 9) <= 1 && first.EndUtc == T(160) &&
              first.Spans.Count == 1 && first.Spans[0].StartUtc == T(50) && first.Spans[0].EndUtc == T(95) && rf.Run == first.Number && rf.By == "Skeleton" && rf.Received[1] == 31 && rf.Received[6] == 4 &&
              all.Count == 3 && all[0].Open && all[0].StartUtc == T(610) && all[1].StartUtc == T(600) && !all[1].Open,
              "FG run: starts at the first second inside, ends at the first second out; a foe fought inside is in it, one before or after is not; its time line holds the fight (0:50 to 0:95) and the fall " +
              "(a Skeleton's slash and poison in its last seconds); out and back within " + BattleRecorder.RunBreak.TotalMinutes + " min goes on with it (" + Json.F(first.Inside) + " s inside), later or another dungeon is the next run");

        // ---------- nothing of a place, nothing of a non-sharer in any shared copy ----------
        var leaks = new List<string>();
        var places = new[] { queenAt, far.At }.Concat(adds.Select(a => a.At)).SelectMany(p => new[] { ((int)Math.Abs(p.X)).ToString(), ((int)Math.Abs(p.Z)).ToString(), ((int)p.Y).ToString(), p.Zone.ToString() }).Distinct();
        foreach (var p in new[] { edda, rowan, tor, finch })
        {
            foreach (var w in places.Concat(new[] { "Ylva", "\"x\":", "\"y\":", "\"z\":", "\"pos\"", "\"zone\"" })) if (p.Json.Contains(w)) leaks.Add(p.Name + ": " + w);
            if (!p.Json.Contains("\"i\":\"-81234:1\"") || !p.Json.Contains("\"bf\":") || !p.Json.Contains("\"runs\":")) leaks.Add(p.Name + ": the ids are missing");
        }
        var eddaWith = edda.Rec.Fights().SelectMany(f => f.Entries).First(e => e.Id.Equals(queen)).With;
        Check(leaks.Count == 0 && eddaWith.Contains("Ylva") && tor.Copy.Fights.SelectMany(f => f.Entries).First(e => e.Id.Equals(queen)).Outside == 1,
              "FG privacy: no shared copy holds a position, a zone or Ylva's name (her PC never shares; she counts as another player), while each holds the creatures' ids and the boss fight and run tags" +
              (leaks.Count > 0 ? ": " + string.Join("; ", leaks.Take(6)) : ""));

        // ---------- the attempt kept on this PC ----------
        var log = new BossLog(); var noted = log.Note(g);
        var t = new LocalTotals { PlayerId = 77 }; t.Bosses = log;
        var json = t.ToJson(now); var back = LocalTotals.FromJson(json); var att = back?.Bosses.Attempts.SingleOrDefault();
        var rowanBefore = att?.Players.FirstOrDefault(p => p.Name == "Rowan")?.DealtTotal ?? 0;
        Copy(rowan, 300);   // Rowan's next copy holds his last ten seconds
        var again = GroupFights.Join(new List<FightSide> { FightSide.Of(edda.Rec, "Edda"), FightSide.Of(rowan.Copy, "Rowan", "k-rowan", T(347)), sides[2], sides[3] }, T(350));
        var grew = back.Bosses.Note(again[0]);
        var att2 = back.Bosses.Attempts.Single();
        Check(noted && att != null && att.Kind == QueenKind && att.Outcome == BossOutcome.Won && att.Health == 0f && att.Place == Citadel && att.PlaceToken == CitadelLabel && att.Lead == queen.ToString() && att.Players.Count == 4 &&
              att.Players.All(p => p.ReceivedTotal > 0) &&
              att.Players.Count(p => p.Self) == 1 && att.Players.First(p => p.Self).Name == "Edda" && att.Players.First(p => p.Self).Deaths == 1 && att.Unnamed >= 1 && Math.Abs(att.Seconds - 270) <= 2 &&
              !json.Contains("Ylva") && json.Contains("\"bossFights\":[") && MiniJson.Num((Dictionary<string, object>)MiniJson.Parse(json), "schema") == LocalTotals.Schema &&
              grew && back.Bosses.Attempts.Count == 1 && att2.Players.First(p => p.Name == "Rowan").DealtTotal > rowanBefore && att2.Outcome == BossOutcome.Won,
              "FG attempts: the won Queen fight is kept on this PC (boss, start, " + Json.F(att?.Seconds ?? 0) + " s, won, the citadel, each sharing player's part, Ylva counted not named) and reads back; noted again as Rowan's copy catches up, the same attempt grows");

        // ---------- the outcome in the book's words, and the boss's health a minute ago ----------
        Check(BossFight.Say(BossOutcome.Won, 0) == "Won" && BossFight.Say(BossOutcome.Reset, 0.41f) == "Reset at 41 %" && BossFight.Say(BossOutcome.GotAway, 0.58f) == "Got away" && BossFight.Say(BossOutcome.Fighting, 0.5f) == "Live" &&
              att.Words == "Won" && Math.Abs(minuteAgo.ago - Health(140)) < 0.03f && Math.Abs(minuteAgo.now - Health(200)) < 0.01f && Math.Abs(minuteAgo.low - minuteAgo.now) < 0.001f,
              "FG words: Won, Reset at N % (the lowest it reached), Got away, Live; never \"lost\"; at 3:20 Tor's PC has the Queen at " + Json.F(minuteAgo.now) + ", a minute before at " + Json.F(minuteAgo.ago) + " (the lane's last minute)");

        // ---------- the runs kept on this PC: the citadel run, read back, drawn after a restart ----------
        var runLog = new RunLog(); var runNoted = runLog.Note(run);
        var rt = new LocalTotals { PlayerId = 77 }; rt.Runs = runLog; rt.Bosses = back.Bosses;
        var rjson = rt.ToJson(now); var rback = LocalTotals.FromJson(rjson); var saved = rback?.Runs.Runs.SingleOrDefault();
        string Place(string kind, string token) => token == CitadelLabel ? "Infested Citadel" : null;   // the game's Localization in the mod
        var restart = FightAdapter.Build(new List<FightSide>(), rback.Bosses, rback.Runs, T(4000), Place);   // a new session: nothing live, the saved ones only
        var savedRun = restart.Fights.FirstOrDefault(f => f.Kind == FightKind.Run); var savedBoss = restart.Fights.FirstOrDefault(f => f.Kind == FightKind.Boss);
        Check(runNoted && saved != null && saved.Kind == Citadel && saved.Token == CitadelLabel && saved.StartUtc == T(5) && Math.Abs(saved.Seconds - 325) <= 2 && saved.Deaths == 1 && saved.Foes >= 40 &&
              saved.BossLead == queen.ToString() && saved.Players.Count == 4 && Math.Abs(saved.Players.First(p => p.Self).Inside - 200) <= 1 && saved.Rows.Count > 0 && saved.Rows[0].Boss && saved.Spans.Any(x => x.Boss) &&
              !rjson.Contains("Ylva") && !runLog.Note(run) &&
              savedRun != null && savedRun.Place == "Infested Citadel" && savedRun.Players.Count == 4 && savedBoss != null && savedBoss.Outcome == FightOutcome.Won && savedBoss.ParentId == savedRun.Id &&
              savedRun.Spans.Any(x => x.FightId == savedBoss.Id) && savedBoss.Foes.Count > 0 && savedBoss.Foes[0].Boss,
              "FG saved runs: the citadel run is kept on this PC (kind, its name token, start, " + Json.F(saved?.Seconds ?? 0) + " s, deaths, foes, The Queen's attempt linked, each sharing player's part, the time line) and read back; " +
              "noted again unchanged it does not grow; after a restart the pages list it with the won attempt nested under it");

        // ---------- the pages' view (FightView) of the four-feed Queen fight ----------
        var pages = FightAdapter.Build(sides, back.Bosses, rback.Runs, now, Place);
        var qv = pages.Fights.FirstOrDefault(f => f.Kind == FightKind.Boss); var rv = pages.Fights.FirstOrDefault(f => f.Kind == FightKind.Run);
        RealPages = pages; RealRestart = restart; RealLive = liveView; RealReset = resetView; RealNow = now; RealGroup = g;   // the fight pages draw these (test/FightPagesRealTests.cs)
        bool Same(Dictionary<string, double> d, float[] a) => BattleRecorder.Types.Select((t, i) => Math.Abs((d.TryGetValue(t, out var x) ? x : 0) - a[i])).Max() < 0.05;
        var pageMissed = qv == null || rv == null ? "no boss or run view" : Missed(
              (pages.Fights.Count(f => f.Kind == FightKind.Boss) == 1 && pages.Fights.Count(f => f.Kind == FightKind.Run) == 1, "one boss fight and one run (saved ones not twice): " + string.Join(",", pages.Fights.Select(f => f.Kind))),
              (qv.Id == FightAdapter.BossId(queen, b.StartUtc) && qv.Outcome == FightOutcome.Won && qv.Health == 0f && qv.Boss == QueenKind && qv.Place == "Infested Citadel" && qv.ParentId == rv.Id, "boss view " + qv.Id + " " + qv.Outcome + " " + qv.Place),
              (qv.Foes[0].Boss && qv.Foes[0].Prefab == QueenKind && qv.Foes[0].Count == 1, "boss row first"),
              (g.Players.All(gp => Same(qv.DealtBy(gp.Name), gp.BossDealt) && Same(qv.ReceivedBy(gp.Name), gp.BossReceived)), "the foe parts add up to each player's boss part"),
              (Math.Abs(qv.DealtTotal - g.Players.Sum(gp => gp.BossDealtTotal)) < 0.5, "dealt total " + Json.F((float)qv.DealtTotal)),
              (qv.Players.Count == 4 && qv.Players[0].You && qv.Players[0].Name == "Edda" && qv.Players.All(x => x.Has), "players"),
              (qv.PlayerOf("Edda").Deaths.Count == 1 && qv.PlayerOf("Edda").Deaths[0].By == QueenKind && qv.PlayerOf("Edda").Deaths[0].Last["poison"] == 20 && qv.PlayerOf("Edda").Deaths[0].Last["pierce"] == 15, "Edda's death"),
              (qv.PlayerOf("Rowan").AsOfUtc == T(292) && !qv.PlayerOf("Rowan").Live, "Rowan as of"),
              (rv.Place == "Infested Citadel" && Math.Abs(rv.PlayerOf("Finch").InsideSeconds - 220) <= 2 && rv.Spans.Any(x => x.FightId == qv.Id) && rv.Spans.Count >= 1, "run view"),
              (pages.Last == qv && pages.LiveBoss == null, "Last fight is the Queen"),
              (liveView != null && liveView.Outcome == FightOutcome.Live && Math.Abs(liveView.Health - 0.97f) < 0.001f && liveView.HealthMinuteAgo == -1f, "an open boss fight is Live (" + liveView?.Outcome + ")"),
              (resetView != null && resetView.Outcome == FightOutcome.Reset && Math.Abs(resetView.Health - 0.97f) < 0.001f, "a reset is Reset at its lowest (" + resetView?.Outcome + " " + resetView?.Health + ")"));
        Check(pageMissed.Length == 0, (pageMissed.Length > 0 ? "[" + pageMissed + "] " : "") +
              "FG pages: the adapter gives the pages one won boss fight in the Infested Citadel nested in its run: the Queen's row first, each player's foe parts adding up to their boss part by type (" +
              Json.F((float)(qv?.DealtTotal ?? 0)) + " dealt), Edda's death with its last seconds, the run's time line pointing at the boss fight; the saved attempt and run are not listed twice; an open boss fight is Live, a reset is Reset at its lowest health");

        // ---------- the bytes: Tor's live updates through the busiest two minutes of the Queen fight ----------
        System.Console.WriteLine("  Queen fight (The Queen, 12 Seekers and 40 brood in 5 min, four players): Tor's update since the last full copy, every 10 s from 2:00 to 4:00: " + string.Join(", ", window) +
                                 " bytes packed (limit " + LiveDelta.MaxPacked + "); with no full copy for the whole fight (5:40) it would be " + whole + " bytes");
        Check(refused.Count == 0 && window.Count == 12 && window.Max() <= LiveDelta.MaxPacked,
              "FG bytes: each 10 s update of the busy Queen fight is one message the 0.8.1 server keeps (JSON object, base and seq, nothing private, no identity key), at most " + (window.Count > 0 ? window.Max() : 0) +
              " bytes packed since the last full copy (limit " + LiveDelta.MaxPacked + ")" + (refused.Count > 0 ? ": refused " + string.Join(", ", refused) : ""));

        // ---------- 0.8.5: Hildir's minibosses are boss fights, flagged by the game or not ----------
        // Geirrhafa's raid copy hits and is hit with the game's boss flag off (as the hooks pass Character.IsBoss); a Fenring Cultist is no boss
        {
            var rec = new BattleRecorder(); var at = new Spot(1200f, 160f, 3400f);
            var geir = new FoeId(-90001, 1); var cultist = new FoeId(-90001, 2);
            for (int s = 0; s < 60; s += 5) rec.Dealt(T(s), geir, "Fenring_Cultist_Hildir_nochest", "$enemy_fenringcultist_hildir", "Mountain", new HitData.DamageTypes { m_fire = 50 }, false, at);
            rec.Dealt(T(400), cultist, "Fenring_Cultist", "$enemy_fenringcultist", "Mountain", new HitData.DamageTypes { m_fire = 50 }, false, new Spot(2200f, 160f, 3400f));
            var bossFights = rec.BossFights();
            var book = FightAdapter.Build(new List<FightSide> { FightSide.Of(rec, "Rowan") }, null, null, T(500));
            Check(bossFights.Count == 1 && bossFights[0].Bosses.SequenceEqual(new[] { geir }) && book.Fights.Count(f => f.Kind == FightKind.Boss) == 1 &&
                  book.Fights.First(f => f.Kind == FightKind.Boss).Boss == "Fenring_Cultist_Hildir_nochest" && BattleRecorder.IsBoss(false, null, "$enemy_skeletonfire") && !BattleRecorder.IsBoss(false, "Skeleton", "$enemy_skeleton"),
                  "FG Hildir's miniboss starts a boss fight with the game's boss flag off (Geirrhafa's raid copy, by prefab; Brenna by name token); a Fenring Cultist starts none");
        }
        return fails;
    }
}
