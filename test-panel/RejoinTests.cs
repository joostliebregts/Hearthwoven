// 0.8.2 (J; Joost on a friend's book: Battle > Foes in Session was empty, while All showed The Queen the friend fought with the group that
// evening): the friend's copy came from a new connection of theirs (a reconnect after the fight), and a copy's log is the sender's connection
// only. The trail notes each copy's connection and puts the earlier ones under the latest (FellowTrail.Joined), so their Session shows what
// they did since you came in, across their reconnects. The copies are synthetic and shaped as Hearthwoven 0.6.5 sends them (the real one's
// fields, one-minute buckets, no foe counts, no live updates); a 0.8.2-shaped pair checks the foe counts.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Hearthwoven;
using Hearthwoven.Panel;

static class RejoinTests
{
    /// <summary>A copy as Hearthwoven <paramref name="mod"/> sends it from connection <paramref name="session"/>: its log holds the fights of that
    /// connection (one-minute buckets, Mistlands, swords); <paramref name="foes"/>: the 0.8 foe counts ("Troll":[fought,defeated,with,hitYou,fighting]).</summary>
    public static string Copy(string session, string mod, (DateTime t, string foe, float dealt)[] fights, string foes = null)
    {
        string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        var dmg = new StringBuilder(); var hits = new StringBuilder(); var dealt = new Dictionary<string, float>();
        foreach (var f in fights)
        {
            var head = EventLog.Bucket(f.t) + "|Mistlands|dealt|" + f.foe + "|Swords";
            dmg.Append(dmg.Length > 0 ? "," : "").Append("\"" + head + "|slash\":" + F(f.dealt));
            hits.Append(hits.Length > 0 ? "," : "").Append("\"" + head + "\":1");
            dealt.TryGetValue(f.foe, out var d); dealt[f.foe] = d + f.dealt;
        }
        var tally = "{\"hitsDealt\":" + fights.Length + ",\"hitsTaken\":0,\"dealt\":{" + string.Join(",", dealt.Select(kv => "\"" + kv.Key + "|Swords|slash\":" + F(kv.Value))) + "},\"taken\":{}}";
        var since = "{\"hitsDealt\":400,\"hitsTaken\":300,\"dealt\":{\"SeekerQueen|Swords|slash\":4229.407,\"Greydwarf|Swords|slash\":368.083},\"taken\":{}}";
        return "{\"platformId\":\"Steam_70000000000000099\",\"mod\":\"" + mod + "\",\"playerId\":-990099,\"name\":\"Ylva\",\"world\":\"Midgard\",\"session\":\"" + session + "\",\"share\":true," +
               "\"stats\":[{\"index\":0,\"counters\":{\"Deaths\":21,\"EnemyHits\":5125,\"EnemyKills\":3123,\"BossKills\":21},\"enemyKills\":[{\"table\":0,\"kills\":{\"$enemy_seekerqueen\":1,\"$enemy_greydwarf\":497}}]}]," +
               "\"skills\":[{\"skill\":\"Swords\",\"level\":60.5,\"progress\":0}],\"damageThisSession\":" + tally + "," +
               "\"measuredThisSession\":{\"blocks\":0,\"parries\":0},\"measuredLog\":{\"bucketMinutes\":1,\"deathsNotListed\":0,\"damage\":{" + dmg + "},\"hits\":{" + hits + "},\"deaths\":[]}," +
               "\"measuredSinceInstall\":{\"blocks\":27,\"parries\":12},\"damageSinceInstall\":" + since + ",\"biomeSinceInstall\":{\"damage\":{},\"deaths\":{}}," +
               (foes == null ? "" : "\"foes\":{\"session\":{" + foes + "}},") + "\"feats\":{},\"dealtByDay\":{}}";
    }

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var start = now.AddHours(-3);   // you came in at 19:30
        var queen = (now.AddMinutes(-140), "SeekerQueen", 4229.4f); var before = (now.AddMinutes(-210), "Greydwarf", 368f);   // 20:10 with you, 19:00 before you
        var first = Copy("y1", "0.6.5", new[] { before, queen }); var rejoined = Copy("y2", "0.6.5", new (DateTime, string, float)[0]);   // their new connection: nothing fought yet
        var key = FellowIds.KeyOf(first);
        var trails = new FellowTrails();
        trails.Add(key, first, now.AddMinutes(-130));   // the copy after the fight, as the server sent it
        var trail = trails.Of(key);
        var one = FellowCopies.Of(key, first);
        Check(trail.Joined(one) == one && trail.RejoinedUtc == null, "J one connection: the copy itself, nothing joined");
        trails.Add(key, rejoined, now.AddMinutes(-120));   // they reconnected: the server's copy is the new connection's
        var copy = FellowCopies.Of(key, rejoined);
        PanelInput Book(PanelInput c)
        {
            c.PlayerName = "Ylva"; c.NowUtc = now; c.ViewerName = "Rowan"; c.PlayerNames = new Dictionary<long, string>(); c.ToLocal = t => t.AddHours(2);
            c.Trail = trail; c.OnThisSession = true; c.ViewerSessionStartUtc = start; c.ReceivedUtc = now.AddMinutes(-120);
            var self = new PanelInput { IsSelf = true, PlayerName = "Rowan", NowUtc = now, SessionStartUtc = start, Log = new EventLog(), Session = new DamageTally(), Events = new SessionEvents(), ToLocal = t => t.AddHours(2) };
            c.Fellows = new List<PanelInput> { self }; self.Fellows = new List<PanelInput> { c };
            return c;
        }
        PanelView Foes(PanelInput b) => PanelModel.Build(b, new PanelState { Chapter = Chapter.Battle, Player = "Ylva", Window = TimeWindow.Session, WindowPicked = true, Page = { [Chapter.Battle] = "foes" } });
        List<string> Rows(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "foetable").SelectMany(b => b.Items ?? new List<Block>()).Where(b => b.Kind == "foe").Select(b => b.Id).ToList();

        // ---------- the bug as it was: the copy alone (the real one: an empty log) shows nothing in Session ----------
        var alone = Foes(Book(copy.ShallowCopy()));
        var empty = PanelModel.Content(alone).FirstOrDefault(b => b.Kind == "empty");
        Check(copy.Log.Damage.Count == 0 && Rows(alone).Count == 0 && empty?.Title == "Nothing since 21:30",
              "J before: the new connection's copy alone has an empty log, so Foes in Session says \"" + empty?.Title + "\" though Ylva fought the Queen at 22:10");

        // ---------- joined: the earlier connection's fight since you came in shows, the one before you came in does not ----------
        var joined = trail.Joined(copy);
        var view = Foes(Book(joined));
        var fightMinute = new DateTime(queen.Item1.Year, queen.Item1.Month, queen.Item1.Day, queen.Item1.Hour, queen.Item1.Minute, 0, DateTimeKind.Utc);
        Check(Rows(view).SequenceEqual(new[] { "SeekerQueen" }) && !PanelModel.FellowOffThisSession(joined) && joined.LastRecordedUtc == fightMinute && trail.RejoinedUtc == now.AddMinutes(-120),
              "J after: Ylva reconnected (" + trail.RejoinedUtc?.AddHours(2).ToString("HH:mm") + "), and her Session shows the Queen from her earlier connection (" + string.Join(", ", Rows(view)) +
              "), not the Greydwarf from before you came in; her latest record is the fight's minute");
        Check(trail.Joined(copy) == joined && joined != copy && copy.Log.Damage.Count == 0 && one.Log.Damage.Count == 2 && Rows(Foes(Book(trail.Joined(one)))).Count == 1,
              "J the joined copy is a clone, the same one each refresh; the shared copies keep their own logs; a copy of an earlier connection (from the cache) is never joined twice");

        // ---------- foe counts: a 0.8.2 sender's counts add up over its connections; one connection without counts drops them ----------
        string Counts(string s, string troll) => Copy(s, "0.8.2", new[] { (now.AddMinutes(-100), "Troll", 50f) }, "\"Troll\":" + troll);
        var k82 = FellowIds.KeyOf(Counts("t1", "[2,1,0,1,1]"));
        var t82 = new FellowTrails();
        t82.Add(k82, Counts("t1", "[2,1,0,1,1]"), now.AddMinutes(-95)); t82.Add(k82, Counts("t2", "[1,1,0,0]"), now.AddMinutes(-60));
        var troll = t82.Of(k82).Joined(FellowCopies.Of(k82, Counts("t2", "[1,1,0,0]")))?.FoesSession?.Get("Troll");
        var mixed = new FellowTrails();
        mixed.Add(k82, Copy("u1", "0.6.5", new[] { queen }), now.AddMinutes(-130)); mixed.Add(k82, Counts("u2", "[1,1,0,0]"), now.AddMinutes(-60));
        var mixedJoined = mixed.Of(k82).Joined(FellowCopies.Of(k82, Counts("u2", "[1,1,0,0]")));
        Check(troll != null && troll.Fought == 3 && troll.Defeated == 2 && troll.Fighting == 0 && troll.GotAway == 1 && mixedJoined.FoesSession == null && mixedJoined.Log.Damage.Count == 2,
              "J counts: two 0.8.2 connections add up (Troll fought " + troll?.Fought + ", defeated " + troll?.Defeated + "; the one still fighting when they left got away); " +
              "a 0.6.5 connection then a 0.8.2 one: no counts, the rows of both");
        return fails;
    }
}
