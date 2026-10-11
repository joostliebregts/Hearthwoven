using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hearthwoven.Panel;

namespace Hearthwoven
{
    /// <summary>
    /// The fights the pages draw (Panel/Chapters/FightView.cs), built from this data layer (0.9, work/hearthwoven-0.8.5/fights/DESIGN.md): the group
    /// fights, boss fights and dungeon runs this session joined from every record (GroupFights.Join, JoinRuns), then the saved attempts and runs
    /// (BossLog, RunLog) the session no longer holds, newest first. Every amount goes into a foe row's player part (FightFoe.By); no player total is
    /// written, so foe rows and player rows always add up. Pure C#, tested (test/FightGroupTests.cs).
    ///
    /// Ids, the same on every PC: a boss fight "boss:" + its first boss's network id + "@" + its start minute (UTC); a run "run:" + its kind + "@" +
    /// its start minute; a group fight "fight:" + its first creature's id + "@" + its start minute.
    /// </summary>
    public static class FightAdapter
    {
        /// <param name="placeName">A dungeon's name from its kind and the game's name token (the game's own Localization in the mod); null: not known.</param>
        public static FightBook Build(IList<FightSide> sides, BossLog bosses, RunLog runs, DateTime nowUtc, Func<string, string, string> placeName = null)
        {
            placeName = placeName ?? ((kind, token) => null);
            var book = new FightBook();
            sides = sides?.Where(x => x != null).ToList() ?? new List<FightSide>();
            var groups = GroupFights.Join(sides, nowUtc); var gruns = GroupFights.JoinRuns(sides, nowUtc);
            var names = new HashSet<string>(sides.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            var bossViews = new List<(GroupBoss b, FightView v)>();
            foreach (var g in groups)
            {
                FightView v;
                if (g.Boss != null && g.Boss.Bosses.Count > 0)
                {
                    v = BossView(g, names, nowUtc);
                    var parent = gruns.FirstOrDefault(r => r.Bosses.Any(g.Boss.Bosses.Contains) && Meet(r.StartUtc, r.EndUtc, g.Boss.StartUtc, g.Boss.EndUtc));
                    if (parent != null) { v.ParentId = RunId(parent.Kind, parent.StartUtc); v.Place = placeName(parent.Kind, parent.Token); }
                    else if (g.Boss.Place != null) v.Place = placeName(g.Boss.Place, g.Boss.PlaceToken);
                    bossViews.Add((g.Boss, v));
                }
                else v = GroupView(g, names);
                book.Fights.Add(v);
            }
            foreach (var r in gruns) book.Fights.Add(RunView(r, bossViews, nowUtc, placeName));
            // what the session no longer holds: the saved attempts and runs (after a restart, or beyond the feed)
            foreach (var a in bosses?.Attempts ?? new List<BossAttempt>())
            {
                var lead = FeedShare.ParseId(a.Lead);
                if (bossViews.Any(x => x.b.Bosses.Contains(lead) && Meet(x.b.StartUtc, x.b.EndUtc, a.StartUtc, a.StartUtc.AddSeconds(a.Seconds)))) continue;
                var v = SavedBoss(a);
                var parent = runs?.Runs.FirstOrDefault(r => r.BossLead == a.Lead && Math.Abs((r.BossStartUtc - a.StartUtc).TotalSeconds) <= BattleRecorder.BossGap.TotalSeconds);
                var live = gruns.FirstOrDefault(r => r.Bosses.Contains(lead) && Meet(r.StartUtc, r.EndUtc, a.StartUtc, a.StartUtc.AddSeconds(a.Seconds)));
                if (live != null) v.ParentId = RunId(live.Kind, live.StartUtc);
                else if (parent != null) v.ParentId = RunId(parent.Kind, parent.StartUtc);
                v.Place = a.Place != null ? placeName(a.Place, a.PlaceToken) : null;
                book.Fights.Add(v);
            }
            foreach (var r in runs?.Runs ?? new List<RunRecord>())
            {
                if (gruns.Any(x => x.Kind == r.Kind && Meet(x.StartUtc, x.EndUtc, r.StartUtc, r.StartUtc.AddSeconds(r.Seconds)))) continue;
                book.Fights.Add(SavedRun(r, placeName));
            }
            var sorted = book.Fights.OrderByDescending(f => f.EndUtc).ThenByDescending(f => f.StartUtc).ToList();
            book.Fights.Clear(); book.Fights.AddRange(sorted);
            return book;
        }

        public static string BossId(FoeId lead, DateTime startUtc) => "boss:" + lead + "@" + Minute(startUtc);
        public static string RunId(string kind, DateTime startUtc) => "run:" + (kind ?? "?") + "@" + Minute(startUtc);
        static string Minute(DateTime t) => t.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        static bool Meet(DateTime a0, DateTime a1, DateTime b0, DateTime b1) => a0 <= b1 + BattleRecorder.BossGap && b0 <= a1 + BattleRecorder.BossGap;

        static FightOutcome Say(BossOutcome o) =>
            o == BossOutcome.Won ? FightOutcome.Won : o == BossOutcome.Reset ? FightOutcome.Reset : o == BossOutcome.GotAway ? FightOutcome.GotAway : FightOutcome.Live;

        // the health the page shows: now (Live), the lowest it reached (Reset at N %), last seen (Got away), 0 (Won)
        static float HealthOf(BossOutcome o, float health, float lowest) =>
            o == BossOutcome.Won ? 0f : o == BossOutcome.Reset ? lowest : health;

        static FightView BossView(GroupFight g, HashSet<string> sharers, DateTime now)
        {
            var b = g.Boss;
            var v = new FightView
            {
                Id = BossId(b.Bosses[0], b.StartUtc), Kind = FightKind.Boss, Outcome = Say(b.Outcome), Boss = b.Kind,
                Biome = b.Foes.FirstOrDefault(f => f.IsBoss)?.Biome, StartUtc = b.StartUtc,
                EndUtc = b.Outcome == BossOutcome.Fighting ? Max(b.EndUtc, b.LastSeenUtc) : b.EndUtc,
                Health = HealthOf(b.Outcome, b.Health, b.Lowest), HealthMinuteAgo = b.Outcome == BossOutcome.Fighting ? b.HealthMinuteAgo : -1f,
            };
            foreach (var r in FoeRow.Of(b.Foes)) v.Foes.Add(Foe(r));
            AddPlayers(v, g, g.Players.Where(p => p.InBoss), p => p.BossBlocks, p => p.BossParries, sharers, now, b.Foes);
            return v;
        }

        static FightView GroupView(GroupFight g, HashSet<string> sharers)
        {
            var first = g.Foes.Where(f => !f.Id.IsNone).OrderBy(f => f.StartUtc).FirstOrDefault();
            var v = new FightView
            {
                Id = "fight:" + (first != null ? first.Id.ToString() : "") + "@" + Minute(g.StartUtc), Kind = FightKind.Group, Outcome = g.Open ? FightOutcome.Live : FightOutcome.Ended,
                Biome = g.Foes.GroupBy(f => f.Biome).OrderByDescending(x => x.Count()).Select(x => x.Key).FirstOrDefault(x => !string.IsNullOrEmpty(x) && x != "None"),
                StartUtc = g.StartUtc, EndUtc = g.EndUtc,
            };
            foreach (var r in FoeRow.Of(g.Foes)) v.Foes.Add(Foe(r));
            AddPlayers(v, g, g.Players, p => p.Blocks, p => p.Parries, sharers, DateTime.MinValue, g.Foes);
            return v;
        }

        // the players in it: you first, then the group in the order they joined; a sharer who was near you or credited with a foe but whose copy
        // holds none of it is listed with Has false
        static void AddPlayers(FightView v, GroupFight g, IEnumerable<GroupPlayer> players, Func<GroupPlayer, int> blocks, Func<GroupPlayer, int> parries, HashSet<string> sharers, DateTime now, IEnumerable<GroupFoe> foes)
        {
            // the hits that landed on you, from your own recorder's encounters with these foes (a fellow's copy does not share its hits: not known)
            var hitsOnYou = foes.SelectMany(f => f.Parts).Where(x => x.Side != null && x.Side.Self && x.Encounter != null).Sum(x => x.Encounter.HitsReceived);
            var me = g.Players.FirstOrDefault(p => p.Self);
            float Near(string name) => me == null ? -1f : me.Fights.Sum(f => f.NearFor(name));
            foreach (var p in players.OrderByDescending(p => p.Self).ThenBy(p => p.FirstUtc))
            {
                var fp = new FightPlayer
                {
                    Name = p.Name, You = p.Self, Has = true, Live = p.Open && v.IsLive, AsOfUtc = p.Self ? (DateTime?)null : p.AsOfUtc,
                    NearSeconds = p.Self ? -1f : Near(p.Name), Blocks = blocks(p), Parries = parries(p), HitsReceived = p.Self ? hitsOnYou : -1,
                };
                foreach (var f in p.Falls) fp.Deaths.Add(Death(f));
                for (int i = p.Falls.Count; i < p.Deaths; i++) fp.Deaths.Add(new FightDeath { AtUtc = DateTime.MinValue });   // counted, no time: its copy dropped it
                v.Players.Add(fp);
            }
            var seen = new HashSet<string>(v.Players.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            var near = me == null ? Enumerable.Empty<string>() : me.Fights.Where(f => f.Near != null).SelectMany(f => f.Near.Where(kv => kv.Value >= BattleRecorder.NearSeconds).Select(kv => kv.Key));
            foreach (var name in near.Concat(g.Foes.SelectMany(f => f.DefeatedBy)).Distinct(StringComparer.OrdinalIgnoreCase))
                if (sharers.Contains(name) && seen.Add(name)) v.Players.Add(new FightPlayer { Name = name, Has = false, NearSeconds = Near(name) });
        }

        static FightView RunView(GroupRun r, List<(GroupBoss b, FightView v)> bossViews, DateTime now, Func<string, string, string> placeName)
        {
            var v = new FightView { Id = RunId(r.Kind, r.StartUtc), Kind = FightKind.Run, Outcome = r.Open ? FightOutcome.Live : FightOutcome.Ended, Place = placeName(r.Kind, r.Token),
                                    Biome = r.Biome, StartUtc = r.StartUtc, EndUtc = r.EndUtc };
            foreach (var row in r.Rows) v.Foes.Add(Foe(row));
            foreach (var w in r.Players.OrderByDescending(w => w.Self).ThenBy(w => w.StartUtc))
            {
                var fp = new FightPlayer { Name = w.Name, You = w.Self, Has = true, Live = w.In, AsOfUtc = w.Self ? (DateTime?)null : w.AsOfUtc, InsideSeconds = w.Inside, Blocks = w.Blocks, Parries = w.Parries };
                foreach (var f in w.Falls) fp.Deaths.Add(Death(f));
                for (int i = w.Falls.Count; i < w.Deaths; i++) fp.Deaths.Add(new FightDeath { AtUtc = DateTime.MinValue });
                v.Players.Add(fp);
            }
            foreach (var s in GroupFights.Merge(r))
            {
                string id = null;
                if (s.Boss) id = bossViews.Where(x => Meet(x.b.StartUtc, x.b.EndUtc, s.StartUtc, s.EndUtc) && r.Bosses.Any(x.b.Bosses.Contains)).Select(x => x.v.Id).FirstOrDefault();
                v.Spans.Add(new FightSpan { StartUtc = s.StartUtc, EndUtc = s.EndUtc, FightId = id });
            }
            return v;
        }

        static FightView SavedBoss(BossAttempt a)
        {
            var lead = FeedShare.ParseId(a.Lead);
            var v = new FightView { Id = BossId(lead, a.StartUtc), Kind = FightKind.Boss, Outcome = Say(a.Outcome == BossOutcome.Fighting ? BossOutcome.GotAway : a.Outcome), Boss = a.Kind,
                                    StartUtc = a.StartUtc, EndUtc = a.StartUtc.AddSeconds(a.Seconds), Health = HealthOf(a.Outcome, a.Health, a.Lowest) };
            foreach (var r in a.Foes) v.Foes.Add(Foe(r));
            foreach (var p in a.Players.OrderByDescending(p => p.Self))
            {
                var fp = new FightPlayer { Name = p.Name, You = p.Self, Has = true, Blocks = p.Blocks, Parries = p.Parries };
                for (int i = 0; i < p.Deaths; i++) fp.Deaths.Add(new FightDeath { AtUtc = DateTime.MinValue });   // saved as a count
                v.Players.Add(fp);
            }
            return v;
        }

        static FightView SavedRun(RunRecord r, Func<string, string, string> placeName)
        {
            var v = new FightView { Id = RunId(r.Kind, r.StartUtc), Kind = FightKind.Run, Place = placeName(r.Kind, r.Token), StartUtc = r.StartUtc, EndUtc = r.StartUtc.AddSeconds(r.Seconds) };
            foreach (var row in r.Rows) v.Foes.Add(Foe(row));
            foreach (var p in r.Players.OrderByDescending(p => p.Self))
            {
                var fp = new FightPlayer { Name = p.Name, You = p.Self, Has = true, InsideSeconds = p.Inside };
                for (int i = 0; i < p.Deaths; i++) fp.Deaths.Add(new FightDeath { AtUtc = DateTime.MinValue });
                v.Players.Add(fp);
            }
            var bossId = r.BossLead != null ? BossId(FeedShare.ParseId(r.BossLead), r.BossStartUtc) : null;
            foreach (var s in r.Spans) v.Spans.Add(new FightSpan { StartUtc = s.StartUtc, EndUtc = s.EndUtc, FightId = s.Boss ? bossId : null });
            return v;
        }

        static FightFoe Foe(FoeRow r)
        {
            var f = new FightFoe { Prefab = r.Kind, Boss = r.Boss, Count = Math.Max(1, r.Count), Defeated = r.Defeated };
            foreach (var p in r.Parts)
            {
                if (!f.By.TryGetValue(p.Name ?? "", out var part)) f.By[p.Name ?? ""] = part = new FightPart();
                Add(part.Dealt, p.Dealt); Add(part.Received, p.Received);
            }
            return f;
        }

        static FightDeath Death(Fall f)
        {
            var d = new FightDeath { AtUtc = f.Utc, By = f.By };
            Add(d.Last, f.Received);
            return d;
        }

        // amounts by the book's damage type ids (BattleRecorder.Types), only the ones with damage
        static void Add(Dictionary<string, double> into, float[] amounts)
        {
            for (int t = 0; t < amounts.Length && t < BattleRecorder.Types.Length; t++)
                if (amounts[t] > 0f && Json.IsFinite(amounts[t])) { into.TryGetValue(BattleRecorder.Types[t], out var had); into[BattleRecorder.Types[t]] = had + amounts[t]; }
        }

        static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    }
}
