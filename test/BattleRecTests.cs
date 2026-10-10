// The battle record (0.8, src/BattleRecord.cs): one foe is one creature however often you hit it; what became of it (the game's kill message
// matched to the destroyed creature in either order, a destroy without a kill, a fight that ended, a late kill); fights split at a gap of more
// than 60 s; the feed and the creature table stay bounded; the counts kept on this PC (schema 5, its own top-level key) and what is shared.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;

static class BattleRecTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        HitData.DamageTypes D(float slash, float fire = 0) => new HitData.DamageTypes { m_slash = slash, m_fire = fire, m_chop = 7 };   // chop: never battle damage
        var t0 = new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc);
        FoeId Id(uint n) => new FoeId(1001, n);

        // ---------- one creature, many hits: counted once; a short window counts by the minute of your last hit ----------
        var r = new BattleRecorder();
        for (int i = 0; i < 50; i++) r.Dealt(t0.AddSeconds(i), Id(1), "Troll", "$enemy_troll", "BlackForest", D(20));
        for (int g = 2; g <= 3; g++) for (int i = 0; i < 3; i++) r.Dealt(t0.AddSeconds(10 + g * 5 + i), Id((uint)g), "Greydwarf", "$enemy_greydwarf", "BlackForest", D(10));
        r.Received(t0.AddSeconds(30), Id(1), "Troll", "$enemy_troll", "BlackForest", D(40));
        r.Received(t0.AddSeconds(31), Id(9), "Neck", "$enemy_neck", "BlackForest", D(5));   // it hit you, you never hit it
        var c = r.Counts(null);
        var tenMin = r.Counts(t0.AddSeconds(49).AddMinutes(-10));
        var later = r.Counts(t0.AddMinutes(3));   // a window that starts after every hit
        Check(c.Get("Troll").Fought == 1 && c.Get("Greydwarf").Fought == 2 && c.Get("Troll").HitYou == 1 && c.Get("Neck").Fought == 0 && c.Get("Neck").HitYou == 1 &&
              c.Get("Troll").Fighting == 1 && c.Get("Troll").GotAway == 0 && tenMin.Get("Troll").Fought == 1 && later.Get("Troll") == null && r.Instances == 4 &&
              r.Fights()[0].Entries.First(e => e.Kind == "Troll").HitsDealt == 50 && r.Fights()[0].Entries.First(e => e.Kind == "Troll").Dealt[1] == 1000,
              "B one creature hit 50 times is one Troll (its 50 hits and 1 000 slash on one entry, chop left out); two Greydwarfs are two; a Neck that only hit you counts as hit you, not fought; a window counts what you hit in it");

        // ---------- what became of them ----------
        r.Attackers(Id(2), 2, new[] { "Edda" });
        r.Killed(t0.AddSeconds(50), "$enemy_troll", 1); r.Destroyed(t0.AddSeconds(50.5), Id(1));          // the kill message first (the usual order)
        r.Destroyed(t0.AddSeconds(51), Id(2)); r.Killed(t0.AddSeconds(51.2), "$enemy_greydwarf", 2);      // the destroy first
        r.Destroyed(t0.AddSeconds(52), Id(3)); r.Tick(t0.AddSeconds(58));                                  // destroyed, no kill within 5 s: the game removed it
        var f0 = r.Fights()[0];
        string Out(uint n) => f0.Entries.First(e => e.Id.Equals(Id(n))).OutcomeText;
        c = r.Counts(null);
        Check(Out(1) == "defeated by you" && Out(2) == "defeated with Edda" && Out(3) == "got away" && c.Get("Troll").Defeated == 1 && c.Get("Greydwarf").Defeated == 1 &&
              c.Get("Greydwarf").With == 1 && c.Get("Greydwarf").GotAway == 1 && f0.Open,
              "B outcome: the game's kill message matched to the destroyed creature in either order (one player: by you; two: with the fellow player the creature's record names); a destroy without a kill got away");

        // a fight that ends: what still stands survived (or "you left" when it is no longer loaded); a kill that comes later still counts
        var s = new BattleRecorder { Loaded = id => !id.Equals(Id(21)) };
        s.Dealt(t0, Id(20), "Boar", "$enemy_boar", "Meadows", D(10)); s.Dealt(t0, Id(21), "Boar", "$enemy_boar", "Meadows", D(10));
        s.Tick(t0.AddSeconds(61));
        var gotAway = s.Counts(null).Get("Boar").GotAway; var hows = s.Fights()[0].Entries.Select(e => e.Away).OrderBy(a => a).ToList();
        s.Attackers(Id(20), 2, new string[0]);
        s.Killed(t0.AddMinutes(5), "$enemy_boar", 2); s.Destroyed(t0.AddMinutes(5).AddSeconds(1), Id(20));   // a fellow player finished it later
        Check(gotAway == 2 && hows.SequenceEqual(new[] { AwayHow.Survived, AwayHow.YouLeft }) && !s.Fights()[0].Open &&
              s.Fights()[0].Entries.First(e => e.Id.Equals(Id(20))).OutcomeText == "defeated with a fellow player" && s.Counts(null).Get("Boar").Defeated == 1 && s.Counts(null).Get("Boar").GotAway == 1,
              "B outcome: a fight that ended leaves what stands as survived (or you left: no longer loaded); a foe a fellow player finished later turns to defeated with them");

        // ---------- fights: a gap of more than 60 s starts the next ----------
        var g1 = new BattleRecorder();
        g1.Dealt(t0, Id(30), "Draugr", "$enemy_draugr", "Swamp", D(10));
        g1.Dealt(t0.AddSeconds(59), Id(31), "Draugr", "$enemy_draugr", "Swamp", D(10));     // 59 s: the same fight
        g1.Received(t0.AddSeconds(118), Id(31), "Draugr", "$enemy_draugr", "Swamp", D(5));  // 59 s after that: still the same
        g1.Dealt(t0.AddSeconds(179), Id(32), "Blob", "$enemy_blob", "Swamp", D(10));         // 61 s: a new fight
        g1.Dealt(t0.AddSeconds(180), Id(30), "Draugr", "$enemy_draugr", "Swamp", D(10));     // the first Draugr again: a new entry in the new fight
        var fights = g1.Fights();
        Check(fights.Count == 2 && fights[0].Number == 2 && fights[0].Foes == 2 && fights[0].Entries[0].Kind == "Draugr" && fights[1].Foes == 2 &&
              fights[1].Length == TimeSpan.FromSeconds(118) && fights[1].ReceivedTotal == 5 && fights[1].Entries.All(e => e.Outcome == FoeOutcome.GotAway) &&
              g1.Counts(null).Get("Draugr").Fought == 2,
              "B fights: hits 59 s apart stay one fight, 61 s starts the next; newest first, foes newest first; the same creature in a later fight is a new entry but still one foe");

        // ---------- bounded ----------
        var big = new BattleRecorder();
        for (uint i = 0; i < 250; i++) big.Dealt(t0.AddSeconds(i * 2), Id(100 + i), i % 2 == 0 ? "Seeker" : "Gjall", "$enemy_x" + (i % 2), "Mistlands", D(5));
        Check(big.FeedCount == BattleRecorder.MaxFeed && big.Fights().Sum(f => f.Foes) == BattleRecorder.MaxFeed && big.Counts(null).Total(k => k.Fought) == 250,
              "B bounded: 250 foes in one long fight keep the last " + BattleRecorder.MaxFeed + " in the feed, while the counts keep all 250");

        // ---------- kept on this PC: a second save adds only the growth, a new session folds, the file reads back ----------
        var t = new LocalTotals { PlayerId = 42 }; var day = new DateTime(2026, 10, 10, 21, 0, 0);
        t.Foes.FromUtc = t0;
        t.Foes.Record("S1", r.Counts(null), day);
        t.Foes.Record("S1", r.Counts(null), day.AddMinutes(2));
        t.Foes.Record("S2", s.Counts(null), day.AddDays(1));
        var json = t.ToJson(DateTime.UtcNow);
        var back = LocalTotals.FromJson(json);
        var since = FoeCounts.Sum(back.Foes.BeforeSession("S3"));
        Check(back.Foes.FromUtc == t0 && back.Foes.Days.Count == 2 && since.Get("Greydwarf").Fought == 2 && since.Get("Troll").Defeated == 1 && since.Get("Boar").Fought == 2 &&
              back.Foes.Sum(day.Date, day.Date).Get("Troll").Fought == 1 && json.Contains("\"foes\":{") && back.Extra.Count == 0 &&
              MiniJson.Num((Dictionary<string, object>)MiniJson.Parse(json), "schema") == 5,
              "B book: the same session saved twice adds only its growth, a new session folds the last, the day rows add up; schema 5, top-level \"foes\" (never inside \"history\"); reads back exactly");

        // ---------- shared: the counts, compact; the feed stays here; the copy grows by little ----------
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats();
        var log = new EventLog(); var tally = new DamageTally(); var many = new BattleRecorder();
        var kinds = new[] { "Greydwarf", "Greydwarf_Elite", "Greydwarf_Shaman", "Troll", "Skeleton", "Draugr", "Draugr_Elite", "Blob", "Leech", "Wraith", "Wolf", "Fenring", "Drake", "Goblin", "GoblinBrute", "Deathsquito", "Lox", "Seeker", "Gjall", "Tick", "Dverger", "Charred_Melee", "Morgen", "Asksvin", "Boar", "Neck", "Deer", "Serpent", "Ulv", "Hatchling" };
        for (int i = 0; i < 600; i++)   // 200 foes over four hours, three hits each, every one defeated
        {
            var n = i / 3; var k = kinds[n % kinds.Length]; var at = t0.AddSeconds(i * 20);
            log.AddDamage(at, "Swamp", true, k, "Swords", D(30)); tally.AddDealt(k, "Swords", D(30));
            many.Dealt(at, Id(5000 + (uint)n), k, "$enemy_" + k.ToLowerInvariant(), "Swamp", D(30));
            if (i % 3 == 2) { many.Killed(at.AddSeconds(1), "$enemy_" + k.ToLowerInvariant(), n % 4 == 0 ? 2 : 1); many.Destroyed(at.AddSeconds(1), Id(5000 + (uint)n)); }
        }
        var trolls = Enumerable.Range(0, 200).Count(n => kinds[n % kinds.Length] == "Troll");
        many.Tick(t0.AddHours(4));
        var share = new FoeShare { Session = many.Counts(null), Since = FoeCounts.Sum(many.Counts(null), many.Counts(null)), FromUtc = t0 };
        string Snap(FoeShare f) => GroupShare.SharedCopy(Snapshot.Build("0.8.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", tally, "s1", new SessionEvents(), log, true, null, tally, null, null, null, null, null, "s1-1", f));
        var without = Snap(null); var with = Snap(share);
        int packedWithout = Transport.Pack(without).Length, packedWith = Transport.Pack(with).Length;
        var copy = Hearthwoven.Panel.PanelInput.FromSnapshot(with);
        System.Console.WriteLine($"INFO share: {kinds.Length} foe kinds this session and since: the shared copy grows from {without.Length} to {with.Length} bytes of JSON (+{with.Length - without.Length}), " +
                          $"{packedWithout} to {packedWith} bytes packed (+{packedWith - packedWithout}); the feed ({many.FeedCount} entries) is not in it");
        var foesKeys = ((Dictionary<string, object>)((Dictionary<string, object>)MiniJson.Parse(with))["foes"]).Keys.OrderBy(k => k).ToArray();
        Check(copy.FoesSession.Get("Troll").Fought == trolls && copy.FoesSession.Get("Troll").Defeated == trolls && copy.FoesSince.Get("Troll").Fought == 2 * trolls && copy.FoesFromUtc == t0 &&
              copy.Battle == null && foesKeys.SequenceEqual(new[] { "from", "session", "since" }) && packedWith - packedWithout < 1024,
              "B share: a fellow reads your counts per kind (this session and since recording began, with the date); the feed is not shared; 30 kinds add " + (packedWith - packedWithout) + " bytes packed (under 1 KB)");
        // REVIEW-08 #8: a foe still in the fight is shared as such (a fifth number, only where there is one), so a fellow's book does not show
        // it as got away; the saved book keeps four numbers
        var running = new BattleRecorder(); running.Dealt(t0, Id(9001), "Troll", "$enemy_troll", "Swamp", D(30));
        var liveCopy = Hearthwoven.Panel.PanelInput.FromSnapshot(Snap(new FoeShare { Session = running.Counts(null), Since = FoeCounts.Sum(running.Counts(null)), FromUtc = t0 }));
        var savedJ = new Json().Open(); running.Counts(null).WriteTo(savedJ, "kinds"); var saved = savedJ.Close().ToString();
        Check(liveCopy.FoesSession.Get("Troll")?.Fighting == 1 && liveCopy.FoesSession.Get("Troll").GotAway == 0 && liveCopy.FoesSince.Get("Troll")?.Fighting == 1 && saved.Contains("\"Troll\":[1,0,0,0]}") &&
              copy.FoesSession.Kinds.Values.All(c => c.Fighting == 0),
              "B share: a troll still in your fight reaches a fellow as still fighting (not got away), in Session and All; the saved book never writes it (" + saved + ")");
        return fails;
    }
}
