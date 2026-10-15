// 0.8.5 (Joost in game, after a restart: Battle > Last fight said "No fight yet this session"; "last fight should show your last fight, but indicate
// that it was x hours/days ago. Same for the feed."): your recent fights kept across a restart (src/KeptFights.cs). Here: a group fight before the
// restart comes back as Last fight, with the fellow's part and its "2 hours ago"; another world or character never mixes in; the bounds; and the
// file and its load time for a busy month (0.8.6: 30 days back, the newest 2 000 foes). Fictional players: Rowan (you) and Edda share; Ylva plays along and does not. Files in a fresh temp folder.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class KeptFightsTests
{
    const long Id = 4242424242L;
    const string World = "Midgard-123456789", Other = "Ashlands-987";
    static readonly DateTime Now = new DateTime(2026, 10, 10, 22, 30, 0, DateTimeKind.Utc);   // 00:30 on the PC (UTC+2): the evening before was "yesterday"

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var dir = Path.Combine(Path.GetTempPath(), "hw-fights-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var path = KeptFights.PathFor(LocalTotals.PathFor(dir, Id, "Rowan"));
        var sharers = new HashSet<string>(new[] { "Rowan", "Edda" }, StringComparer.OrdinalIgnoreCase);
        HitData.DamageTypes D(float slash) => new HitData.DamageTypes { m_slash = slash };

        // ---------- the evening before the restart: a Troll alone at 20:10 (it survived), then three Greydwarfs with Edda at 20:25 ----------
        var rowan = new BattleRecorder(); var edda = new BattleRecorder();
        var troll = new FoeId(-555, 1); var at = Now.AddHours(-2).AddMinutes(-20);
        rowan.Dealt(at, troll, "Troll", "$enemy_troll", "BlackForest", D(200)); rowan.Received(at.AddSeconds(4), troll, "Troll", "$enemy_troll", "BlackForest", D(60));
        var camp = Now.AddHours(-2).AddMinutes(-5);
        for (uint i = 0; i < 3; i++)
        {
            var id = new FoeId(-555, 10 + i); var t = camp.AddSeconds(i * 6);
            rowan.Dealt(t, id, "Greydwarf", "$enemy_greydwarf", "BlackForest", D(40)); rowan.Received(t.AddSeconds(1), id, "Greydwarf", "$enemy_greydwarf", "BlackForest", D(8));
            edda.Dealt(t.AddSeconds(2), id, "Greydwarf", "$enemy_greydwarf", "BlackForest", D(30));
            var ylva = i == 2;   // Ylva hit the last one too: the game names her, the kept record never does
            rowan.Attackers(id, ylva ? 3 : 2, ylva ? new[] { "Edda", "Ylva" } : new[] { "Edda" }); edda.Attackers(id, ylva ? 3 : 2, ylva ? new[] { "Rowan", "Ylva" } : new[] { "Rowan" });
            rowan.Killed(t.AddSeconds(4), "$enemy_greydwarf", ylva ? 3 : 2); edda.Killed(t.AddSeconds(4), "$enemy_greydwarf", ylva ? 3 : 2);
            rowan.Destroyed(t.AddSeconds(4.5), id); edda.Destroyed(t.AddSeconds(4.5), id);
        }
        var logout = Now.AddHours(-2);   // 20:30, the last save of the session (its logout)
        rowan.Tick(logout); edda.Tick(logout);
        FeedShare Shared(BattleRecorder rec) { var j = new Json().Open(); FeedShare.From(rec, sharers.Contains).WriteTo(j); return FeedShare.ReadFrom(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(j.Close().ToString()), "battleFeed")); }
        var eddaCopy = FightSide.Of(Shared(edda), "Edda", "k-edda", logout.AddSeconds(-8));   // her copy as Rowan's PC holds it
        var book = KeptFights.Load(path, Id, "Rowan", out var none);
        book.Record(KeptFights.Take(World, "s1", Now.AddHours(-3.5), logout, rowan, "Rowan", sharers.Contains, new[] { eddaCopy }), logout);
        book.Save(path, logout);

        // ---------- the restart: a new session at 00:25, nothing fought yet; the totals load, and Last fight is the Greydwarf camp ----------
        var back = KeptFights.Load(path, Id, "Rowan", out var problem);
        var earlier = back.Earlier(World, "s2", Now);
        var live = new List<FightSide> { FightSide.Of(new BattleRecorder(), "Rowan") };
        var fights = FightAdapter.Build(live, null, null, Now, null, earlier);
        var last = fights.Last;
        var input = new PanelInput { IsSelf = true, PlayerName = "Rowan", NowUtc = Now, SessionStartUtc = Now.AddMinutes(-5), ToLocal = t => t.AddHours(2), Fights = fights, KeptSessions = earlier };
        var pill = last == null ? "" : PanelModel.FightWhen(input, last.StartUtc, last.EndUtc, PanelModel.FightLength(last.Length));
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        var greydwarf = last?.Foes.FirstOrDefault(f => f.Prefab == "Greydwarf");
        Check(none == null && problem == null && path.EndsWith("-Rowan.fights.json") && earlier.Count == 1 && earlier[0].Fights.Count == 2 && earlier[0].Sides.Count == 2 &&
              last != null && last.Kind == FightKind.Group && !last.IsLive && greydwarf != null && greydwarf.Count == 3 && greydwarf.Defeated == 3 &&
              last.Players.Count == 2 && last.Players.All(p => p.Has) && last.Players[0].You && Math.Abs(last.DealtBy("Rowan").Values.Sum() - 120) < 0.01 && Math.Abs(last.DealtBy("Edda").Values.Sum() - 90) < 0.01 &&
              last.PlayerOf("Rowan").HitsReceived == 3 && pill.StartsWith("2 hours ago, yesterday 22:25 · ") && !text.Contains("Ylva") && text.Contains("\"u\":1"),
              "KF a group fight before the restart is Last fight after it: the camp with Edda's part beside yours (" + Json.F((float)(last?.DealtTotal ?? 0)) + " dealt), your hits kept, its head \"" + pill +
              "\"; Ylva, who does not share, is another player in the file, never named");
        // 0.8.5 review: Edda did not restart, so her shared feed still holds the camp and the live join builds it without you: one fight, not two
        var withEdda = FightAdapter.Build(new List<FightSide> { FightSide.Of(new BattleRecorder(), "Rowan"), eddaCopy }, null, null, Now, null, earlier);
        var one = withEdda.Last;
        var ids = withEdda.Fights.Select(f => f.Id).ToList();
        Check(ids.Distinct().Count() == ids.Count && withEdda.Fights.Count(f => f.Kind == FightKind.Group && f.Foes.Any(x => x.Prefab == "Greydwarf")) == 1 &&
              one != null && one.Players.Count(p => p.Has) == 2 && one.Players[0].You && Math.Abs(one.DealtBy("Rowan").Values.Sum() - 120) < 0.01 && Math.Abs(one.DealtBy("Edda").Values.Sum() - 90) < 0.01,
              "KF a kept fight a fellow still shares after your restart is one fight: Last fight has your kept part (" + Json.F((float)(one?.DealtBy("Rowan").Values.Sum() ?? 0)) + ") beside Edda's (" +
              string.Join(", ", (one?.Players ?? new List<FightPlayer>()).Select(p => p.Name + (p.Has ? "+" : "-"))) + "), every id once (" + ids.Count + " fights)");
        var thisSession = PanelModel.FightWhen(new PanelInput { IsSelf = true, NowUtc = Now, SessionStartUtc = Now.AddHours(-3), ToLocal = t => t.AddHours(2) }, camp, camp.AddMinutes(2), "2 min");
        Check(thisSession == "22:25 · 2 min · 2 h ago" && PanelModel.AgoWords(TimeSpan.FromDays(2.2)) == "2 days ago" && PanelModel.AgoWords(TimeSpan.FromMinutes(45)) == "45 minutes ago",
              "KF the same fight in this session reads as today, as before (\"" + thisSession + "\"); further back in days: \"" + PanelModel.AgoWords(TimeSpan.FromDays(2.2)) + "\"");

        // ---------- never another world or character; another world's sessions stay in the file for that world ----------
        var ash = new BattleRecorder(); ash.Dealt(Now.AddMinutes(-3), new FoeId(-777, 1), "Charred", "$enemy_charred", "AshLands", D(90)); ash.Tick(Now);
        back.Record(KeptFights.Take(Other, "s2", Now.AddMinutes(-5), Now, ash, "Rowan", sharers.Contains, null), Now); back.Save(path, Now);
        var again = KeptFights.Load(path, Id, "Rowan", out _);
        Check(again.Earlier(World, "s3", Now).SelectMany(s => s.Fights).Count() == 2 && again.Earlier(Other, "s3", Now).SelectMany(s => s.Fights).Single().Entries.Single().Kind == "Charred" &&
              again.Earlier(World, "s1", Now).Count == 0 && KeptFights.Load(path, Id, "Tor", out _).Sessions.Count == 0 && KeptFights.Load(path, Id + 1, "Rowan", out _).Sessions.Count == 0,
              "KF worlds and characters: Midgard's fights only in Midgard, the Ashlands' only there, both kept in the one file; the running session is never an earlier one; another name or player id: nothing");

        // ---------- the bounds (0.8.6): 30 days back at most; the newest whole fights up to 2 000 foes; a foe still fighting at the save got away when it ended ----------
        var week = KeptFights.Load(path, Id, "Rowan", out _);
        var stale = week.Earlier(World, "s9", logout.AddDays(30).AddMinutes(1));
        var fresh = KeptFights.Load(path, Id, "Rowan", out _).Earlier(World, "s9", logout.AddDays(29));
        var open = new BattleRecorder(); open.Dealt(Now.AddSeconds(-20), new FoeId(-888, 1), "Wolf", "$enemy_wolf", "Mountain", D(50));
        var ob = new KeptFights { PlayerId = Id.ToString(), Name = "Rowan" }; ob.Record(KeptFights.Take(World, "s4", Now.AddMinutes(-9), Now, open, "Rowan", sharers.Contains, null), Now);
        var reread = KeptFights.FromJson(ob.ToJson(Now)).Sessions.Single().Fights.Single();
        // age: one wolf a session, 3, 12, 29 and 31 days back; the one of 31 days is gone, the others stay
        var ages = new KeptFights { PlayerId = Id.ToString(), Name = "Rowan" };
        foreach (var back_ in new[] { 31, 29, 12, 3 }) ages.Record(OneWolf(World, "a" + back_, Now.AddDays(-back_)), Now.AddDays(-back_));
        var aged = ages.Earlier(World, "s-next", Now).Select(s => (int)Math.Round((Now - s.ToUtc).TotalDays)).ToList();
        // the cap: a busy month (150 foes an evening, five evenings a week) keeps its newest whole fights up to MaxFoes, none cut in half
        var many = Busy(World, Now, 30, 3, out _);
        var foesKept = many.Earlier(World, "s-next", Now).SelectMany(s => s.Fights).ToList();
        var newest = foesKept.Max(f => f.EndUtc); var oldest = foesKept.Min(f => f.EndUtc);
        Check(stale.Count == 0 && fresh.Count == 1 && !reread.Open && reread.Entries.Single().Outcome == FoeOutcome.GotAway && reread.Entries.Single().OutcomeText == "survived" &&
              aged.SequenceEqual(new[] { 3, 12, 29 }) &&
              KeptFights.MaxAge == TimeSpan.FromDays(30) && foesKept.All(f => f.Foes == 5) &&
              foesKept.Sum(f => f.Foes) <= KeptFights.MaxFoes && foesKept.Sum(f => f.Foes) > KeptFights.MaxFoes - 5 && newest > Now.AddDays(-1.5) && oldest > Now.AddDays(-30) && oldest < Now.AddDays(-14),
              "KF bounds: a fight 30 days and a minute back is gone and one of 29 days stays (sessions " + string.Join("/", aged) + " days back kept of 31, 29, 12, 3); a wolf still fighting at the save comes back as \"" +
              reread.Entries.Single().OutcomeText + "\", the fight ended; a busy month keeps its newest " + foesKept.Sum(f => f.Foes) + " foes (of " + KeptFights.MaxFoes + "), whole fights, " +
              Math.Round((Now - oldest).TotalDays) + " days back");

        // ---------- the size and the load time: a busy month (an evening of 150 foes in 30 fights, five evenings a week, three fellows beside you) ----------
        many = Busy(World, Now, 30, 3, out var json);
        var file = Path.Combine(dir, "busy.fights.json"); File.WriteAllText(file, json);
        KeptFights.Load(file, Id, "Rowan", out _);   // warm: the first parse pays the JIT
        var sw = Stopwatch.StartNew(); const int runs = 5; List<KeptSession> loaded = null;
        for (int r = 0; r < runs; r++) loaded = KeptFights.Load(file, Id, "Rowan", out _).Earlier(World, "s-next", Now);
        var ms = sw.Elapsed.TotalMilliseconds / runs;
        sw.Restart(); for (int r = 0; r < runs; r++) KeptFightsJoin(loaded); var joinMs = sw.Elapsed.TotalMilliseconds / runs;
        sw.Restart(); for (int r = 0; r < runs; r++) many.ToJson(Now); var saveMs = sw.Elapsed.TotalMilliseconds / runs;   // what every local save writes whole
        var solo = Busy(World, Now, 30, 0, out var soloJson); var duo = Busy(World, Now, 30, 1, out var oneJson);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        System.Console.WriteLine("  kept fights, a busy month (30 days, 150 foes in 30 fights on five evenings a week, three fellows sharing each fight): the file " + json.Length / 1024 + " KB (" + Gz(json) / 1024 +
                          " KB gzipped), " + loaded.Sum(s => s.Fights.Sum(f => f.Foes)) + " foes in " + loaded.Count + " sessions, its load " + ms.ToString("0.0", inv) +
                          " ms, one save's writing " + saveMs.ToString("0.0", inv) + " ms, the adapter's join of the kept fights " + joinMs.ToString("0.0", inv) +
                          " ms (once per session: KeptViews); with no fellow " + soloJson.Length / 1024 + " KB, with one " + oneJson.Length / 1024 + " KB");
        // 0.8.5 reads this file: version 1 as before, and no session holds more of yours than a feed reads back (FeedShare.MaxRead), so nothing is cut at its load
        var perSession = many.Sessions.Select(s => s.Mine.Fights.Sum(f => f.Foes)).Max();
        Check(json.Length < 800 * 1024 && ms < 250 && loaded.SelectMany(s => s.Fights).Sum(f => f.Foes) <= KeptFights.MaxFoes && KeptFights.Version == 1 && json.Contains("\"version\":1") &&
              KeptFights.PerSession <= FeedShare.MaxRead && perSession <= FeedShare.MaxRead,
              "KF size: a busy month's file is " + json.Length / 1024 + " KB (under 800) and loads in " + ms.ToString("0.0", inv) + " ms here (under 250): the newest " + KeptFights.MaxFoes +
              " foes and the fellows' parts beside them; still version 1, and at most " + perSession + " of your foes in a session, which 0.8.5 reads whole (it reads " + FeedShare.MaxRead + ")");

        // ---------- what a fellow's shared copy would cost if it carried the earlier sessions' feed too (it does not: measured only) ----------
        FellowCost(many, inv);

        // ---------- 0.8.5 (the lead): boss attempts and runs per world, like the kept fights; those saved before 0.8.5 go to the world of the last save ----------
        (GroupFight, GroupRun) BossAndRun(DateTime t, uint n)
        {
            var r = new BattleRecorder(); var boss = new FoeId(-321, n);
            r.Inside(t, "Crypt2", 77, "$location_crypt"); r.Inside(t.AddSeconds(20), "Crypt2", 77, "$location_crypt"); r.Inside(t.AddSeconds(40), null, 0);
            r.Dealt(t.AddSeconds(60), boss, "Eikthyr", "$enemy_eikthyr", "Meadows", D(300), true, new Spot(10, 30, 10));
            r.Killed(t.AddSeconds(65), "$enemy_eikthyr", 1); r.Destroyed(t.AddSeconds(65.5), boss); r.Tick(t.AddMinutes(4));
            var sides = new List<FightSide> { FightSide.Of(r, "Rowan") };
            return (GroupFights.Join(sides, t.AddMinutes(4)).First(g => g.Boss != null), GroupFights.JoinRuns(sides, t.AddMinutes(4)).First());
        }
        var (gm, rm) = BossAndRun(Now.AddDays(-1), 1); var (ga, ra) = BossAndRun(Now.AddHours(-3), 2);
        var bl = new BossLog(); var rl = new RunLog();
        var noted = bl.Note(gm, World) & rl.Note(rm, World) & bl.Note(ga, Other) & rl.Note(ra, Other);
        var lt = new LocalTotals { PlayerId = 77 }; lt.Bosses = bl; lt.Runs = rl; var ltJson = lt.ToJson(Now); var lback = LocalTotals.FromJson(ltJson);
        string Leads(FightBook b) => string.Join(",", b.Fights.Select(f => f.Kind + (f.Kind == FightKind.Boss ? " " + f.Id.Substring(5, 6) : "")));
        var inOther = FightAdapter.Build(live, lback.Bosses.Of(Other), lback.Runs.Of(Other), Now); var inWorld = FightAdapter.Build(live, lback.Bosses.Of(World), lback.Runs.Of(World), Now);
        // a file from 0.8.4: 55 attempts and a run with no world, one attempt already of the Ashlands; the kept minutes name Midgard as the world of the last save
        var old = new BossLog(); var oldRuns = new RunLog();
        for (int i = 0; i < 55; i++) old.Attempts.Add(new BossAttempt { Kind = "Eikthyr", Lead = "-321:9", StartUtc = Now.AddHours(-60 + i) });
        old.Attempts.Insert(0, new BossAttempt { World = Other, Kind = "Eikthyr", Lead = "-321:8", StartUtc = Now.AddHours(-70) });
        oldRuns.Runs.Add(new RunRecord { Kind = "Crypt2", StartUtc = Now.AddHours(-50) });
        var minutes = KeptMinutes.PathFor(LocalTotals.PathFor(dir, Id, "Rowan"));
        var km = KeptMinutes.Take(Now.AddHours(-5), new EventLog(), null); km.World = World; km.PlayerId = Id.ToString(); km.Name = "Rowan"; km.Save(minutes, Now.AddHours(-5));
        var lastWorld = KeptMinutes.WorldOf(minutes);
        var adopted = old.Adopt(lastWorld ?? Other) & oldRuns.Adopt(lastWorld ?? Other);
        Check(noted && ltJson.Contains("\"w\":\"" + World + "\"") && lback.Bosses.Attempts.Count == 2 && lback.Runs.Runs.Count == 2 &&
              Leads(inOther) == "Boss -321:2,Run" && Leads(inWorld) == "Boss -321:1,Run" &&
              lastWorld == World && KeptMinutes.WorldOf(Path.Combine(dir, "none.minutes.json")) == null && adopted &&
              old.Of(World).Attempts.Count == BossLog.MaxAttempts && old.Of(World).Attempts[0].StartUtc == Now.AddHours(-55) && old.Of(Other).Attempts.Count == 1 && oldRuns.Of(World).Runs.Count == 1 &&
              !old.Adopt("Somewhere-else"),
              "KF boss attempts and runs per world: Midgard's and the Ashlands' kept apart in the totals file and read back; in the Ashlands the pages show only its attempt and run (" + Leads(inOther) +
              "), in Midgard only Midgard's; attempts and runs saved before 0.8.5 go to the world of the last save (the kept minutes name " + lastWorld + "), " + BossLog.MaxAttempts + " kept, the Ashlands' one untouched");

        try { Directory.Delete(dir, true); } catch { }
        return fails;
    }

    /// <summary>A session of one wolf that ended at <paramref name="end"/>.</summary>
    static KeptSession OneWolf(string world, string id, DateTime end)
    {
        var r = new BattleRecorder(); r.Dealt(end.AddSeconds(-30), new FoeId(-999, 1), "Wolf", "$enemy_wolf", "Mountain", new HitData.DamageTypes { m_slash = 50 }); r.Tick(end);
        return KeptFights.Take(world, id, end.AddHours(-2), end, r, "Rowan", n => true, null);
    }

    static int Gz(string text)
    {
        using (var ms = new MemoryStream())
        {
            using (var z = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) { var b = System.Text.Encoding.UTF8.GetBytes(text); z.Write(b, 0, b.Length); }
            return (int)ms.Length;
        }
    }

    /// <summary>0.8.6 (measured, not built): what a fellow's shared copy would add if it carried your earlier sessions' feed beside this session's (it
    /// carries this session's newest FeedShare.MaxFoes only): the feed of every kept session without hits (a fellow's view of it), raw and gzipped as the server packs it.</summary>
    static void FellowCost(KeptFights book, System.Globalization.CultureInfo inv)
    {
        string Feed(IEnumerable<KeptSession> sessions, int perSession)
        {
            var j = new Json().Open();
            foreach (var s in sessions)
            {
                var feed = new FeedShare(); var fights = new List<FoeFight>(); var foes = 0;
                foreach (var f in s.Mine.Fights.OrderByDescending(f => f.EndUtc)) { if (foes + f.Foes > perSession) break; fights.Add(f); foes += f.Foes; }
                feed.Fights.AddRange(fights); var nums = new HashSet<int>(fights.Select(f => f.Number)); feed.Falls.AddRange(s.Mine.Falls.Where(f => nums.Contains(f.Fight)));
                if (fights.Count == 0) continue;
                feed.T0 = fights.Select(f => f.StartUtc).Min();
                feed.WriteTo(j, "s" + s.Id, hits: false);
            }
            return j.Close().ToString();
        }
        var all = book.Sessions.Where(s => s.World == World).OrderByDescending(s => s.ToUtc).ToList();
        var weekAgo = Now.AddDays(-7);
        var latest = Feed(all.Take(1), FeedShare.MaxFoes);
        var week = Feed(all.Where(s => s.ToUtc > weekAgo), FeedShare.MaxFoes); var weekN = all.Count(s => s.ToUtc > weekAgo);
        var month = Feed(all, FeedShare.MaxFoes);
        var monthFull = Feed(all, KeptFights.PerSession);
        System.Console.WriteLine("  fellow copy, if it carried earlier sessions' feed too (newest " + FeedShare.MaxFoes + " foes of each session, no hits; today it carries one session's): this session's feed " + latest.Length / 1024 +
                                 " KB raw, " + Gz(latest) / 1024 + " KB gzipped; a busy week (" + weekN + " earlier sessions) " + week.Length / 1024 + " KB raw, " + Gz(week) / 1024 + " KB gzipped; a busy month (" + all.Count +
                                 " sessions) " + month.Length / 1024 + " KB raw, " + Gz(month) / 1024 + " KB gzipped; with every kept foe of the month (" + KeptFights.MaxFoes + ") " + monthFull.Length / 1024 + " KB raw, " +
                                 Gz(monthFull) / 1024 + " KB gzipped");
    }

    static void KeptFightsJoin(List<KeptSession> kept) { foreach (var s in kept) GroupFights.Join(s.Sides, s.ToUtc.AddDays(1)); }

    /// <summary>A busy stretch in <paramref name="world"/>: <paramref name="days"/> days, an evening on five days of every seven, 30 fights of five foes each (150 foes), you and
    /// <paramref name="fellows"/> fellows hitting every foe, each evening's record kept at its end. Its file as written after the last evening.</summary>
    static KeptFights Busy(string world, DateTime now, int days, int fellows, out string json)
    {
        var book = new KeptFights { PlayerId = Id.ToString(), Name = "Rowan" };
        var names = new[] { "Edda", "Finch", "Tor" }.Take(fellows).ToArray();
        var share = new HashSet<string>(names.Concat(new[] { "Rowan" }), StringComparer.OrdinalIgnoreCase);
        for (int d = days - 1; d >= 0; d--)
        {
            if (d % 7 >= 5) continue;   // two quiet days a week
            var start = now.AddDays(-d).AddHours(-4); uint id = (uint)(d * 1000);
            var me = new BattleRecorder(); var them = names.Select(_ => new BattleRecorder()).ToArray();
            var t = start;
            for (int f = 0; f < 30; f++)
            {
                for (int k = 0; k < 5; k++)
                {
                    var foe = new FoeId(-900, ++id);
                    me.Dealt(t, foe, "Seeker", "$enemy_seeker", "Mistlands", new HitData.DamageTypes { m_pierce = 40, m_fire = 6 }); me.Received(t.AddSeconds(1), foe, "Seeker", "$enemy_seeker", "Mistlands", new HitData.DamageTypes { m_pierce = 12, m_poison = 4 });
                    foreach (var r in them) r.Dealt(t.AddSeconds(2), foe, "Seeker", "$enemy_seeker", "Mistlands", new HitData.DamageTypes { m_slash = 35 });
                    me.Attackers(foe, 1 + names.Length, names); me.Killed(t.AddSeconds(3), "$enemy_seeker", 1 + names.Length); me.Destroyed(t.AddSeconds(3.5), foe);
                    t = t.AddSeconds(8);
                }
                t = t.AddMinutes(3);
            }
            me.Tick(t); foreach (var r in them) r.Tick(t);
            var sides = them.Select((r, k) => { var j = new Json().Open(); FeedShare.From(r, share.Contains).WriteTo(j); return FightSide.Of(FeedShare.ReadFrom(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(j.Close().ToString()), "battleFeed")), names[k], "k-" + names[k], t); }).ToList();
            book.Record(KeptFights.Take(world, "d" + d, start, t, me, "Rowan", share.Contains, sides), t);
        }
        json = book.ToJson(now);
        return book;
    }
}
