using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>One player's battle record as the group view reads it (0.9): yours (the recorder, live) or a fellow's shared feed (FeedShare, as of
    /// their copy). Name: as the book shows them; Key: a fellow's key (FellowIds), null for you.</summary>
    public sealed class FightSide
    {
        public string Name, Key;
        public bool Self;
        public List<FoeFight> Fights = new List<FoeFight>();
        public List<BossFight> Bosses = new List<BossFight>();
        public List<DungeonRun> Runs = new List<DungeonRun>();
        /// <summary>Their deaths, newest first (a fellow's copy holds the newest FeedShare.MaxFalls).</summary>
        public List<Fall> Falls = new List<Fall>();
        /// <summary>A fellow: when their copy reached this PC; their record is as of then (a live update every 10 s, so 10 s behind or more). Null: yours, live.</summary>
        public DateTime? ReceivedUtc;

        public static FightSide Of(BattleRecorder rec, string name) =>
            rec == null ? null : new FightSide { Name = name ?? "", Self = true, Fights = rec.Fights(), Bosses = rec.BossFights(), Runs = rec.Runs(), Falls = rec.Falls() };

        public static FightSide Of(FeedShare feed, string name, string key, DateTime? receivedUtc) =>
            feed == null ? null : new FightSide { Name = name ?? "", Key = key, Fights = feed.Fights, Bosses = feed.Bosses, Runs = feed.Runs, Falls = feed.Falls, ReceivedUtc = receivedUtc };
    }

    /// <summary>One creature in a group fight, with each player's encounter with it: Parts[i].Encounter holds that player's dealt AND received per
    /// type (BattleRecorder.Types order), hits, blocks, parries and outcome with this one creature in this one fight ("a part is one player's damage
    /// to that creature, this fight").</summary>
    public sealed class GroupFoe
    {
        public sealed class Part { public FightSide Side; public FoeEncounter Encounter; }
        public FoeId Id;
        public string Kind, Token, Biome;
        /// <summary>A boss; in the boss fight (a boss, or a foe someone's PC put in its arena).</summary>
        public bool IsBoss, InBoss;
        public DateTime StartUtc, EndUtc;
        /// <summary>Someone's record says it was defeated (the game's kill goes to every player who hit it).</summary>
        public bool Defeated;
        /// <summary>The players the game credited with it: everyone who hit it (who landed the last blow is not known). Names from your own record
        /// may include a player who does not share (your book only: none of this is shared or saved).</summary>
        public readonly List<string> DefeatedBy = new List<string>();
        /// <summary>Players in it no record names (they do not share, or left the world): the most any one record counted.</summary>
        public int Unnamed;
        public readonly List<Part> Parts = new List<Part>();
        public float Dealt(FightSide side) { float s = 0; foreach (var p in Parts) if (p.Side == side) s += p.Encounter.DealtTotal; return s; }
        public float DealtTotal { get { float s = 0; foreach (var p in Parts) s += p.Encounter.DealtTotal; return s; } }
    }

    /// <summary>One player's part in a group fight: sums over the foes they fought in it, and over the boss fight's foes (the boss and its arena).</summary>
    public sealed class GroupPlayer
    {
        public string Name, Key;
        public bool Self;
        public readonly float[] Dealt = new float[BattleRecorder.Types.Length], Received = new float[BattleRecorder.Types.Length];
        public readonly float[] BossDealt = new float[BattleRecorder.Types.Length], BossReceived = new float[BattleRecorder.Types.Length];
        /// <summary>Creatures they fought in it, of those defeated, their deaths, held blocks and parries; the same three over the boss fight only.</summary>
        public int Foes, Defeated, Deaths, Blocks, Parries, BossDeaths, BossBlocks, BossParries;
        /// <summary>Their deaths in it, oldest first: when, and what hit them in the last seconds per type, with what took them down (Fall).</summary>
        public readonly List<Fall> Falls = new List<Fall>();
        /// <summary>Their recorder fights that make up this group fight (yours: with FoeFight.Near, who was near you).</summary>
        public readonly List<FoeFight> Fights = new List<FoeFight>();
        /// <summary>They fought the boss or a foe in its arena, or their PC saw the boss fight.</summary>
        public bool InBoss;
        /// <summary>Their first and last hit in the fight (a player who joined mid-fight starts later than the fight).</summary>
        public DateTime FirstUtc = DateTime.MaxValue, LastUtc = DateTime.MinValue;
        /// <summary>A fellow: their record is as of this time (their copy's arrival); null: yours, live.</summary>
        public DateTime? AsOfUtc;
        /// <summary>Their fight is still on (a fellow's only while their copy is fresh, GroupFights.Stale).</summary>
        public bool Open;
        public float DealtTotal { get { float s = 0; foreach (var v in Dealt) s += v; return s; } }
        public float BossDealtTotal { get { float s = 0; foreach (var v in BossDealt) s += v; return s; } }
    }

    /// <summary>The boss fight inside a group fight: its bosses, span, outcome as the whole group's records show it, and its foes.</summary>
    public sealed class GroupBoss
    {
        public string Kind, Token;
        public readonly List<FoeId> Bosses = new List<FoeId>();
        public DateTime StartUtc = DateTime.MaxValue, EndUtc = DateTime.MinValue;
        /// <summary>Won once every boss is down in anyone's record; Fighting while someone's boss fight is open (yours, or a fresh copy's); Reset when
        /// someone saw a boss back at full health; else GotAway (ended, outcome not known). Never "lost".</summary>
        public BossOutcome Outcome;
        /// <summary>The first boss's health (0..1): as last seen by anyone (now while Fighting, "last seen at 58 %" after; 0 once won); the lowest
        /// it reached in anyone's record ("reset at 41 %"); while Fighting, a minute ago; -1: nobody saw it.</summary>
        public float Health = -1f, Lowest = -1f, HealthMinuteAgo = -1f;
        public string Words => BossFight.Say(Outcome, Lowest);
        /// <summary>When anyone last saw a boss of it (GotAway's "last seen").</summary>
        public DateTime LastSeenUtc = DateTime.MinValue;
        /// <summary>The dungeon it was fought in: its kind ("Mistlands_DvergrBossEntrance1") and name token ("$location_dvergrboss"); null: outside one.</summary>
        public string Place, PlaceToken;
        /// <summary>Players in it who do not share: counted, never named in what is saved (BossLog).</summary>
        public int Unnamed;
        /// <summary>The bosses first, then the arena's foes, newest first.</summary>
        public readonly List<GroupFoe> Foes = new List<GroupFoe>();
        public TimeSpan Length => EndUtc > StartUtc ? EndUtc - StartUtc : TimeSpan.Zero;
    }

    /// <summary>One fight of the group: every player's record of it joined by the creatures' network ids (GroupFights.Join).</summary>
    public sealed class GroupFight
    {
        public DateTime StartUtc = DateTime.MaxValue, EndUtc = DateTime.MinValue;
        /// <summary>Someone's fight is still on (yours, or a fellow's whose copy is fresh).</summary>
        public bool Open;
        /// <summary>Newest first (by the last hit anyone saw).</summary>
        public readonly List<GroupFoe> Foes = new List<GroupFoe>();
        /// <summary>Most damage dealt first.</summary>
        public readonly List<GroupPlayer> Players = new List<GroupPlayer>();
        /// <summary>The boss fight in it, null: none.</summary>
        public GroupBoss Boss;
        public TimeSpan Length => EndUtc > StartUtc ? EndUtc - StartUtc : TimeSpan.Zero;
        public GroupPlayer Player(string name) => Players.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        public GroupFoe Foe(FoeId id) => Foes.FirstOrDefault(f => f.Id.Equals(id));
    }

    /// <summary>A dungeon run of the group: everyone's runs into the same kind of place at the same time.</summary>
    public sealed class GroupRun
    {
        /// <summary>One player in the run: their own in to out (StartUtc, EndUtc), seconds inside, deaths, foes, whether they are inside now (a fellow:
        /// as of their copy), and their part of the time line: their fights in it (Spans) and their falls (Falls), oldest first.</summary>
        public sealed class Who
        {
            public string Name, Key; public bool Self, In; public float Inside; public int Deaths, Foes, Blocks, Parries; public DateTime StartUtc, EndUtc; public DateTime? AsOfUtc;
            /// <summary>Their damage dealt and received per type in the run (their encounters fought inside it, as their record holds them).</summary>
            public readonly float[] Dealt = new float[BattleRecorder.Types.Length], Received = new float[BattleRecorder.Types.Length];
            public readonly List<RunSpan> Spans = new List<RunSpan>();
            public readonly List<Fall> Falls = new List<Fall>();
        }
        /// <summary>A boss fight in the run, for the time line's gold span: its first boss (the GroupBoss's Bosses[0]) and span.</summary>
        public sealed class BossSpan { public string Kind; public FoeId Lead; public DateTime StartUtc, EndUtc; public BossOutcome Outcome; }
        /// <summary>The place's kind and name token (DungeonRun.Kind, Token); the land of its first foe (Heightmap.Biome's name), null: no foe.</summary>
        public string Kind, Token, Biome;
        public DateTime StartUtc = DateTime.MaxValue, EndUtc = DateTime.MinValue;
        /// <summary>Someone is inside now (you, or a fellow whose copy is fresh).</summary>
        public bool Open;
        public readonly List<Who> Players = new List<Who>();
        /// <summary>The creatures fought in it, each once, as the records hold them (at least the most any one player counted).</summary>
        public int Foes;
        /// <summary>The bosses fought in it (the Queen in her citadel): their group fight has a GroupBoss with these ids.</summary>
        public readonly List<FoeId> Bosses = new List<FoeId>();
        /// <summary>The boss fights in it as anyone recorded them (one per attempt and player; the time line merges what overlaps).</summary>
        public readonly List<BossSpan> BossSpans = new List<BossSpan>();
        /// <summary>Its foe rows: one per kind met in it (a boss first), each with every player's part.</summary>
        public readonly List<FoeRow> Rows = new List<FoeRow>();
        public TimeSpan Length => EndUtc > StartUtc ? EndUtc - StartUtc : TimeSpan.Zero;
    }

    /// <summary>
    /// The group's fights and dungeon runs (0.9, work/hearthwoven-0.8.5/fights/DESIGN.md), joined on your PC from your recorder and the fellows'
    /// shared feeds. Nothing here is shared or saved (BossLog keeps what it takes of a boss fight). Pure C#, tested (test/FightGroupTests.cs).
    ///
    /// How fights from different PCs line up: every recorder splits its own fights (BattleRecorder.FightGap, BossGap). Two players' fights are one
    /// group fight when they share a creature (its network id, the same on every PC) at about the same time: their encounters with it overlap, or
    /// lie within FightGap of each other (BossGap for a boss). Joined both ways and onwards, so a foe one player fought and another did not is in
    /// it through the player who did, a player who joined mid-fight joins through the first creature both hit, and a copy 10 s behind joins on
    /// what it already holds (its part ends earlier: GroupPlayer.AsOfUtc says as of when). A fellow's copy from before 0.9 has no ids and stays
    /// out of the join.
    /// </summary>
    public static class GroupFights
    {
        /// <summary>A fellow's copy older than this no longer keeps a fight or boss fight open (they left, or their updates stopped).</summary>
        public static readonly TimeSpan Stale = TimeSpan.FromSeconds(60);

        sealed class Node { public int Side; public FoeFight Fight; public readonly List<BossFight> Bosses = new List<BossFight>(); }

        public static List<GroupFight> Join(IList<FightSide> sides, DateTime nowUtc)
        {
            var result = new List<GroupFight>();
            if (sides == null) return result;
            var nodes = new List<Node>(); var at = new Dictionary<long, int>();
            long K(int side, int fight) => ((long)side << 32) | (uint)fight;
            for (int s = 0; s < sides.Count; s++)
            {
                var side = sides[s]; if (side == null) continue;
                foreach (var f in side.Fights ?? new List<FoeFight>()) if (!at.ContainsKey(K(s, f.Number))) { at[K(s, f.Number)] = nodes.Count; nodes.Add(new Node { Side = s, Fight = f }); }
                foreach (var b in side.Bosses ?? new List<BossFight>())
                {
                    if (b.Fight > 0 && at.TryGetValue(K(s, b.Fight), out var n)) { nodes[n].Bosses.Add(b); continue; }
                    var alone = new Node { Side = s }; alone.Bosses.Add(b); nodes.Add(alone);   // seen before their first hit, or its fight left their copy
                }
            }
            var parent = new int[nodes.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
            void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b); }

            // the same creature in two records at about the same time: the same fight
            var seen = new Dictionary<FoeId, List<(int node, DateTime s, DateTime e, bool boss)>>();
            void Note(FoeId id, int node, DateTime s, DateTime e, bool boss)
            {
                if (id.IsNone) return;
                if (!seen.TryGetValue(id, out var l)) seen[id] = l = new List<(int, DateTime, DateTime, bool)>();
                l.Add((node, s, e, boss));
            }
            for (int n = 0; n < nodes.Count; n++)
            {
                if (nodes[n].Fight != null) foreach (var e in nodes[n].Fight.Entries) Note(e.Id, n, e.StartUtc, e.EndUtc, e.IsBoss);
                foreach (var b in nodes[n].Bosses) foreach (var id in b.Bosses) Note(id, n, b.StartUtc, b.EndUtc, true);
            }
            foreach (var l in seen.Values)
                for (int i = 0; i < l.Count; i++)
                    for (int k = i + 1; k < l.Count; k++)
                    {
                        if (l[i].node == l[k].node) continue;
                        var tol = l[i].boss || l[k].boss ? BattleRecorder.BossGap : BattleRecorder.FightGap;
                        if (l[i].s <= l[k].e + tol && l[k].s <= l[i].e + tol) Union(l[i].node, l[k].node);
                    }

            // one side's boss fight spans its recorder fights (a death and the run back): every fight with a foe of it is the same group fight
            var bossNode = new Dictionary<long, int>();
            for (int n = 0; n < nodes.Count; n++)
            {
                foreach (var b in nodes[n].Bosses) { if (bossNode.TryGetValue(K(nodes[n].Side, b.Number), out var m)) Union(n, m); else bossNode[K(nodes[n].Side, b.Number)] = n; }
                if (nodes[n].Fight != null)
                    foreach (var e in nodes[n].Fight.Entries)
                        if (e.Boss > 0) { if (bossNode.TryGetValue(K(nodes[n].Side, e.Boss), out var m)) Union(n, m); else bossNode[K(nodes[n].Side, e.Boss)] = n; }
            }
            var groups = new Dictionary<int, List<int>>();
            for (int n = 0; n < nodes.Count; n++) { var r = Find(n); if (!groups.TryGetValue(r, out var g)) groups[r] = g = new List<int>(); g.Add(n); }
            foreach (var g in groups.Values) result.Add(Build(sides, nodes, g, nowUtc));
            result.Sort((a, b) => b.EndUtc.CompareTo(a.EndUtc));
            return result;
        }

        static bool Fresh(FightSide side, DateTime now) => side.Self || (side.ReceivedUtc.HasValue && now - side.ReceivedUtc.Value <= Stale);

        static GroupFight Build(IList<FightSide> sides, List<Node> nodes, List<int> members, DateTime now)
        {
            var g = new GroupFight();
            var foes = new Dictionary<FoeId, GroupFoe>(); var all = new List<GroupFoe>();
            var players = new Dictionary<int, GroupPlayer>();
            var bossFights = new List<(FightSide side, BossFight b)>();
            var fightsOf = new Dictionary<int, HashSet<int>>(); var bossesOf = new Dictionary<int, HashSet<int>>();   // side -> its fight and boss fight numbers in this group fight
            GroupPlayer P(int s)
            {
                if (players.TryGetValue(s, out var p)) return p;
                var side = sides[s];
                return players[s] = new GroupPlayer { Name = side.Name, Key = side.Key, Self = side.Self, AsOfUtc = side.ReceivedUtc };
            }
            foreach (var n in members)
            {
                var node = nodes[n]; var side = sides[node.Side]; var p = P(node.Side);
                if (!fightsOf.ContainsKey(node.Side)) { fightsOf[node.Side] = new HashSet<int>(); bossesOf[node.Side] = new HashSet<int>(); }
                foreach (var b in node.Bosses) { bossFights.Add((side, b)); bossesOf[node.Side].Add(b.Number); }
                if (node.Fight == null) continue;
                fightsOf[node.Side].Add(node.Fight.Number); p.Fights.Add(node.Fight);
                p.Deaths += node.Fight.Deaths;
                if (node.Fight.Open && Fresh(side, now)) { p.Open = true; g.Open = true; }
                foreach (var e in node.Fight.Entries)
                {
                    GroupFoe f = null;
                    if (!e.Id.IsNone) foes.TryGetValue(e.Id, out f);
                    if (f == null)
                    {
                        f = new GroupFoe { Id = e.Id, Kind = e.Kind, Token = e.Token, Biome = e.Biome, StartUtc = e.StartUtc, EndUtc = e.EndUtc };
                        if (!e.Id.IsNone) foes[e.Id] = f;
                        all.Add(f);
                    }
                    f.Parts.Add(new GroupFoe.Part { Side = side, Encounter = e });
                    if (e.StartUtc < f.StartUtc) f.StartUtc = e.StartUtc;
                    if (e.EndUtc > f.EndUtc) f.EndUtc = e.EndUtc;
                    if (string.IsNullOrEmpty(f.Token) && !string.IsNullOrEmpty(e.Token)) f.Token = e.Token;
                    f.IsBoss |= e.IsBoss; f.InBoss |= e.Boss > 0 || e.IsBoss;
                    if (e.Defeated)
                    {
                        f.Defeated = true;
                        Add(f.DefeatedBy, side.Name);
                        foreach (var w in e.With) Add(f.DefeatedBy, w);
                        f.Unnamed = Math.Max(f.Unnamed, e.Others + e.Outside);
                    }
                    for (int t = 0; t < p.Dealt.Length; t++) { p.Dealt[t] += e.Dealt[t]; p.Received[t] += e.Received[t]; }
                    p.Foes++; if (e.Defeated) p.Defeated++;
                    if (e.Boss > 0) bossesOf[node.Side].Add(e.Boss);
                    p.Blocks += e.Blocks; p.Parries += e.Parries;
                    if (e.StartUtc < p.FirstUtc) p.FirstUtc = e.StartUtc;
                    if (e.EndUtc > p.LastUtc) p.LastUtc = e.EndUtc;
                    if (e.StartUtc < g.StartUtc) g.StartUtc = e.StartUtc;
                    if (e.EndUtc > g.EndUtc) g.EndUtc = e.EndUtc;
                }
            }
            var bossIds = new List<FoeId>();
            foreach (var (_, b) in bossFights.OrderBy(x => x.b.StartUtc)) foreach (var id in b.Bosses) if (!bossIds.Contains(id)) bossIds.Add(id);
            foreach (var f in all.Where(x => x.IsBoss && !x.Id.IsNone).OrderBy(x => x.StartUtc)) if (!bossIds.Contains(f.Id)) bossIds.Add(f.Id);
            foreach (var f in all) if (!f.Id.IsNone && bossIds.Contains(f.Id)) { f.IsBoss = true; f.InBoss = true; }
            all.Sort((a, b) => b.EndUtc != a.EndUtc ? b.EndUtc.CompareTo(a.EndUtc) : b.StartUtc.CompareTo(a.StartUtc));
            g.Foes.AddRange(all);

            if (bossIds.Count > 0)
            {
                var boss = g.Boss = new GroupBoss();
                boss.Bosses.AddRange(bossIds);
                var first = bossFights.OrderBy(x => x.b.StartUtc).Select(x => x.b).FirstOrDefault();
                var lead = all.FirstOrDefault(f => f.Id.Equals(bossIds[0]));
                boss.Kind = first?.Kind ?? lead?.Kind ?? "?"; boss.Token = first?.Token ?? lead?.Token;
                foreach (var (side, b) in bossFights)
                {
                    if (b.StartUtc < boss.StartUtc) boss.StartUtc = b.StartUtc;
                    if (b.EndUtc > boss.EndUtc) boss.EndUtc = b.EndUtc;
                    if (b.StartUtc < g.StartUtc) g.StartUtc = b.StartUtc;
                    if (b.EndUtc > g.EndUtc) g.EndUtc = b.EndUtc;
                    var inRun = b.Run > 0 ? side.Runs?.FirstOrDefault(r => r.Number == b.Run) : null;
                    if (boss.Place == null && inRun != null) { boss.Place = inRun.Kind; boss.PlaceToken = inRun.Token; }
                }
                foreach (var f in all.Where(x => x.IsBoss))
                {
                    if (f.StartUtc < boss.StartUtc) boss.StartUtc = f.StartUtc;
                    if (f.EndUtc > boss.EndUtc) boss.EndUtc = f.EndUtc;
                }
                bool Down(FoeId id) => (foes.TryGetValue(id, out var f) && f.Defeated) || bossFights.Any(x => x.b.Outcome == BossOutcome.Won && x.b.Bosses.Contains(id));
                if (bossIds.All(Down)) boss.Outcome = BossOutcome.Won;
                else if (bossFights.Any(x => x.b.Open && Fresh(x.side, now)) || (bossFights.Count == 0 && g.Open)) boss.Outcome = BossOutcome.Fighting;
                else if (bossFights.Any(x => x.b.Outcome == BossOutcome.Reset)) boss.Outcome = BossOutcome.Reset;
                else boss.Outcome = BossOutcome.GotAway;   // nobody can say: never "lost"
                foreach (var (_, b) in bossFights) if (b.LastSeenUtc > boss.LastSeenUtc) boss.LastSeenUtc = b.LastSeenUtc;
                var leadFights = bossFights.Where(x => x.b.Bosses.Count > 0 && x.b.Bosses[0].Equals(bossIds[0])).Select(x => x.b).ToList();
                foreach (var b in leadFights) if (b.Lowest >= 0f && (boss.Lowest < 0f || b.Lowest < boss.Lowest)) boss.Lowest = b.Lowest;
                var newest = leadFights.Where(b => b.Health >= 0f).OrderByDescending(b => b.LastSeenUtc).FirstOrDefault();
                if (newest != null) boss.Health = newest.Health;
                if (boss.Outcome == BossOutcome.Fighting) boss.HealthMinuteAgo = leadFights.Where(b => b.Open && b.HealthMinuteAgo >= 0f).OrderByDescending(b => b.LastSeenUtc).Select(b => b.HealthMinuteAgo).DefaultIfEmpty(-1f).First();
                if (boss.Outcome == BossOutcome.Won) { boss.Health = 0f; boss.Lowest = 0f; }
                boss.Foes.AddRange(all.Where(f => f.IsBoss).Concat(all.Where(f => f.InBoss && !f.IsBoss)));
                // each player's part of the boss fight: their damage on its foes, their deaths in it
                var sideIndex = new Dictionary<FightSide, int>();
                for (int s = 0; s < sides.Count; s++) if (sides[s] != null) sideIndex[sides[s]] = s;
                foreach (var f in boss.Foes)
                    foreach (var part in f.Parts)
                    {
                        var p = P(sideIndex[part.Side]); p.InBoss = true;
                        for (int t = 0; t < p.BossDealt.Length; t++) { p.BossDealt[t] += part.Encounter.Dealt[t]; p.BossReceived[t] += part.Encounter.Received[t]; }
                        p.BossBlocks += part.Encounter.Blocks; p.BossParries += part.Encounter.Parries;
                    }
                foreach (var (side, b) in bossFights) { var p = P(sideIndex[side]); p.InBoss = true; p.BossDeaths += b.Deaths; }
                var names = new HashSet<string>(players.Values.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
                var named = boss.Foes.SelectMany(f => f.DefeatedBy).Where(nm => !names.Contains(nm)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                boss.Unnamed = Math.Max(named, boss.Foes.Select(f => f.Unnamed).DefaultIfEmpty(0).Max());
            }
            foreach (var kv in players)   // each player's falls in it: in one of their fights here, or during one of their boss fights here
            {
                var side = sides[kv.Key];
                foreach (var f in (side.Falls ?? new List<Fall>()).OrderBy(x => x.Utc))
                    if ((f.Fight > 0 && fightsOf[kv.Key].Contains(f.Fight)) || (f.Boss > 0 && bossesOf[kv.Key].Contains(f.Boss))) kv.Value.Falls.Add(f);
            }
            foreach (var p in players.Values) if (p.FirstUtc == DateTime.MaxValue) { p.FirstUtc = g.StartUtc; p.LastUtc = g.StartUtc; }
            g.Players.AddRange(players.Values.OrderByDescending(p => p.DealtTotal).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase));
            return g;
        }

        /// <summary>A run's time line merged over the group: every player's fights in it, overlapping stretches joined (one flagged Boss when a boss
        /// fight's foe was in it, the boss fights' own spans included), oldest first.</summary>
        public static List<RunSpan> Merge(GroupRun run)
        {
            var all = run.Players.SelectMany(w => w.Spans).Select(s => new RunSpan { Fight = s.Fight, StartUtc = s.StartUtc, EndUtc = s.EndUtc, Boss = s.Boss })
                         .Concat(run.BossSpans.Select(b => new RunSpan { StartUtc = b.StartUtc, EndUtc = b.EndUtc, Boss = true })).OrderBy(s => s.StartUtc).ToList();
            var merged = new List<RunSpan>();
            foreach (var s in all)
            {
                var last = merged.Count > 0 ? merged[merged.Count - 1] : null;
                if (last != null && s.StartUtc <= last.EndUtc && s.Boss == last.Boss) { if (s.EndUtc > last.EndUtc) last.EndUtc = s.EndUtc; }
                else merged.Add(s);
            }
            return merged;
        }

        static void Add(List<string> names, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (var n in names) if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return;
            names.Add(name);
        }

        /// <summary>
        /// The group's dungeon runs: runs into the same kind of place whose spans meet (within BattleRecorder.RunBreak) are one run. A run says only
        /// its kind, never where it is, so two groups in two dungeons of one kind at the same time read as one run (DESIGN.md, open question).
        /// Newest first.
        /// </summary>
        public static List<GroupRun> JoinRuns(IList<FightSide> sides, DateTime nowUtc)
        {
            var result = new List<GroupRun>();
            if (sides == null) return result;
            var items = new List<(FightSide side, DungeonRun r)>();
            foreach (var side in sides) if (side != null) foreach (var r in side.Runs ?? new List<DungeonRun>()) items.Add((side, r));
            var parent = new int[items.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
            for (int i = 0; i < items.Count; i++)
                for (int k = i + 1; k < items.Count; k++)
                {
                    var a = items[i].r; var b = items[k].r;
                    if (a.Kind == b.Kind && a.StartUtc <= b.EndUtc + BattleRecorder.RunBreak && b.StartUtc <= a.EndUtc + BattleRecorder.RunBreak)
                    { int x = Find(i), y = Find(k); if (x != y) parent[Math.Max(x, y)] = Math.Min(x, y); }
                }
            foreach (var grp in Enumerable.Range(0, items.Count).GroupBy(Find))
            {
                var run = new GroupRun { Kind = items[grp.Key].r.Kind, Token = grp.Select(i => items[i].r.Token).FirstOrDefault(t => !string.IsNullOrEmpty(t)) };
                var ids = new HashSet<FoeId>(); int most = 0;
                var kinds = new Dictionary<string, (FoeRow row, HashSet<FoeId> seen)>(StringComparer.Ordinal);
                foreach (var i in grp)
                {
                    var (side, r) = items[i];
                    if (r.StartUtc < run.StartUtc) run.StartUtc = r.StartUtc;
                    if (r.EndUtc > run.EndUtc) run.EndUtc = r.EndUtc;
                    var inNow = r.Open && Fresh(side, nowUtc);
                    run.Open |= inNow;
                    var who = run.Players.FirstOrDefault(w => w.Self == side.Self && w.Key == side.Key && w.Name == side.Name);
                    if (who == null) run.Players.Add(who = new GroupRun.Who { Name = side.Name, Key = side.Key, Self = side.Self, StartUtc = r.StartUtc, EndUtc = r.EndUtc, AsOfUtc = side.ReceivedUtc });
                    who.In |= inNow; who.Inside += r.Inside; who.Deaths += r.Deaths; who.Foes += r.Foes;
                    who.Spans.AddRange(r.Spans); who.Spans.Sort((a, b) => a.StartUtc.CompareTo(b.StartUtc));
                    foreach (var f in side.Falls ?? new List<Fall>()) if (f.Run == r.Number && !who.Falls.Contains(f)) who.Falls.Add(f);
                    who.Falls.Sort((a, b) => a.Utc.CompareTo(b.Utc));
                    if (r.StartUtc < who.StartUtc) who.StartUtc = r.StartUtc;
                    if (r.EndUtc > who.EndUtc) who.EndUtc = r.EndUtc;
                    most = Math.Max(most, who.Foes);
                    foreach (var e in side.Fights?.SelectMany(f => f.Entries) ?? Enumerable.Empty<FoeEncounter>())
                    {
                        if (e.Run != r.Number) continue;
                        for (int t = 0; t < who.Dealt.Length; t++) { who.Dealt[t] += e.Dealt[t]; who.Received[t] += e.Received[t]; }
                        who.Blocks += e.Blocks; who.Parries += e.Parries;
                        if (run.Biome == null && !string.IsNullOrEmpty(e.Biome) && e.Biome != "None") run.Biome = e.Biome;
                        var key = (e.IsBoss ? "!" : "") + (e.Kind ?? "?");
                        if (!kinds.TryGetValue(key, out var k)) kinds[key] = k = (new FoeRow { Kind = e.Kind ?? "?", Boss = e.IsBoss }, new HashSet<FoeId>());
                        if (e.Id.IsNone || k.seen.Add(e.Id)) { k.row.Count++; if (e.Defeated) k.row.Defeated++; }
                        var part = k.row.Part(side.Name, side.Self);
                        for (int t = 0; t < part.Dealt.Length; t++) { part.Dealt[t] += e.Dealt[t]; part.Received[t] += e.Received[t]; }
                        if (e.Id.IsNone) continue;
                        ids.Add(e.Id); if (e.IsBoss && !run.Bosses.Contains(e.Id)) run.Bosses.Add(e.Id);
                    }
                    foreach (var b in side.Bosses ?? new List<BossFight>())
                        if (b.Run == r.Number)
                        {
                            foreach (var id in b.Bosses) if (!run.Bosses.Contains(id)) run.Bosses.Add(id);
                            run.BossSpans.Add(new GroupRun.BossSpan { Kind = b.Kind, Lead = b.Bosses.Count > 0 ? b.Bosses[0] : default, StartUtc = b.StartUtc, EndUtc = b.EndUtc, Outcome = b.Outcome });
                        }
                }
                run.Foes = Math.Max(ids.Count, most);
                run.Rows.AddRange(FoeRow.Order(kinds.Values.Select(k => k.row)));
                run.Players.Sort((a, b) => a.StartUtc.CompareTo(b.StartUtc));
                result.Add(run);
            }
            result.Sort((a, b) => b.EndUtc.CompareTo(a.EndUtc));
            return result;
        }
    }
}
