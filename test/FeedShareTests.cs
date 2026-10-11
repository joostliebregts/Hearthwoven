// 0.8.2: the battle feed in the shared copy (src/BattleRecord.cs FeedShare; Joost on a fellow's book: "why no feed? I want that in"). A fellow
// reads the feed and the last fight as the sender's recorder holds them, through the full copy and the live update the server keeps and relays,
// and the 0.8.1 server's own checks pass them; a name only of a player who shares (anyone else is another player); a busy fight's live
// updates stay under the one-message limit. Fictional players and fights.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FeedShareTests
{
    // one line per fight and per foe, everything a page reads from them
    static string Describe(FeedShare s) => s == null ? "null" : "left " + s.LeftOut + "\n" + string.Join("\n", s.Fights.Select(f =>
        "fight " + f.Number + (f.Open ? " open" : "") + " of " + f.Of + " out " + f.Outside + " near " + string.Join(",", (f.Near ?? new Dictionary<string, float>()).OrderBy(k => k.Key).Select(k => k.Key + "=" + Json.F(k.Value))) + " " +
        f.StartUtc.ToString("HH:mm:ss") + "-" + f.EndUtc.ToString("HH:mm:ss") + "\n" + string.Join("\n", f.Entries.Select(e =>
            "  " + e.Seq + " " + e.Kind + " " + e.Biome + " " + e.StartUtc.ToString("HH:mm:ss") + "-" + e.EndUtc.ToString("HH:mm:ss") + " " + e.OutcomeText +
            " d" + string.Join(",", e.Dealt.Select(v => Json.F(v))) + " r" + string.Join(",", e.Received.Select(v => Json.F(v)))))));

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var t0 = new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc).AddMilliseconds(400);   // the first hit off the whole second
        var sharers = new HashSet<string>(new[] { "Rowan", "Tor" }, StringComparer.OrdinalIgnoreCase);   // Ylva plays along but does not share

        // ---------- Edda's evening on her PC: the recorder, the log and the tallies the snapshot reads ----------
        var rec = new BattleRecorder(); var log = new EventLog(); var dmg = new DamageTally(); var ev = new SessionEvents();
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats(); stats[0][PlayerStatType.Deaths] = 3;
        var skills = new List<Snapshot.SkillInfo> { new Snapshot.SkillInfo { Name = "Swords", Level = 40, Accumulator = 2 } };
        string Token(string kind) => "$enemy_" + kind.ToLowerInvariant();
        void Hit(DateTime at, FoeId id, string kind, string biome, HitData.DamageTypes d) { rec.Dealt(at, id, kind, Token(kind), biome, d); log.AddDamage(at, biome, true, kind, "Swords", d); dmg.AddDealt(kind, "Swords", d); }
        void Took(DateTime at, FoeId id, string kind, string biome, HitData.DamageTypes d) { rec.Received(at, id, kind, Token(kind), biome, d); log.AddDamage(at, biome, false, kind, "EnemyHit", d); dmg.AddTaken(kind, "EnemyHit", d); }
        void Kill(DateTime at, FoeId id, string kind, params string[] with) { if (with.Length > 0) rec.Attackers(id, 1 + with.Length, with); rec.Killed(at, Token(kind), 1 + with.Length); rec.Destroyed(at.AddSeconds(0.5), id); }
        string Full(string copyId, bool feed = true) => Snapshot.Build("0.8.2", 77, "Edda", stats, skills, "Midgard", dmg, "s1", ev, log, true, copyId: copyId, feed: feed ? FeedShare.From(rec, sharers.Contains) : null);
        string Stored(string json) => FellowIds.WithPlatform(GroupShare.SharedCopy(json), "76561198000000077");   // as the server keeps and relays it (GroupShare.Store)
        FoeId Id(uint n) => new FoeId(5005, n);

        // fight 1, Black Forest: three Greydwarfs, one defeated with Tor (shares), one with Ylva (does not), one by Edda alone; Tor and Ylva near
        // her for 12 s, Rowan for a moment (under the 10 s that puts a player in a fight)
        var slash = new HitData.DamageTypes { m_slash = 23.456f };
        for (int i = 0; i < 6; i++)
        {
            var at = t0.AddSeconds(2 * i);
            for (uint g = 1; g <= 3; g++) Hit(at, Id(g), "Greydwarf", "BlackForest", slash);
            rec.Near(at, "Tor", 2); rec.Near(at, "Ylva", 2); if (i < 2) rec.Near(at, "Rowan", 2);
        }
        Took(t0.AddSeconds(5), Id(2), "Greydwarf", "BlackForest", new HitData.DamageTypes { m_blunt = 9.5f });
        Kill(t0.AddSeconds(11), Id(1), "Greydwarf", "Tor"); Kill(t0.AddSeconds(11.2), Id(2), "Greydwarf", "Ylva"); Kill(t0.AddSeconds(11.4), Id(3), "Greydwarf");
        rec.Tick(t0.AddMinutes(3));   // the fight ended a minute after its last hit

        var baseFull = Full("s1-1"); var baseShared = GroupShare.SharedCopy(baseFull); var baseBare = GroupShare.SharedCopy(Full("s1-1", false));
        var shared1 = FeedShare.ReadFrom(MiniJson.Obj(MiniJson.Parse(baseShared) as Dictionary<string, object>, "battleFeed"));
        var f1 = shared1?.Fights.FirstOrDefault();
        string Outcome(FeedShare s, uint seq) => s?.Fights.SelectMany(f => f.Entries).FirstOrDefault(e => e.Seq == seq)?.OutcomeText;
        Check(f1 != null && f1.Entries.Count == 3 && f1.Outside == 1 && f1.Near != null && f1.Near.Keys.OrderBy(k => k).SequenceEqual(new[] { "Rowan", "Tor" }) &&
              f1.With().SequenceEqual(new[] { "Tor" }) && Outcome(shared1, 1) == "defeated with Tor" && Outcome(shared1, 2) == "defeated with another player" && Outcome(shared1, 3) == "defeated by you" &&
              !baseShared.Contains("Ylva") && baseShared.Contains("\"Tor\"") && FeedShare.From(rec, null).Fights[0].Entries.All(e => e.With.Length == 0 && (e.Seq != 1 && e.Seq != 2 || e.Outside == 1)),
              "FS privacy: a name in the shared feed only for a player who shares (Tor near and in a kill, Rowan's moment near kept as seconds); Ylva, who does not share, is never written and counts as another player, once for the fight (" +
              Outcome(shared1, 2) + "); nobody named when no one shares");

        // ---------- fight 2, Mistlands, the busy one: a Gjall and 48 Roots in the two minutes before the next full copy, sent as 10 s updates ----------
        var start = t0.AddMinutes(4); uint roots = 100; const uint Gjall = 99;
        var pierce = new HitData.DamageTypes { m_pierce = 61.25f, m_spirit = 12.5f }; var fire = new HitData.DamageTypes { m_fire = 31.7f, m_slash = 18.25f };
        var alive = new List<(FoeId id, int hits)>();
        string prev = baseShared; var cumulative = new List<int>(); var steps = new List<int>(); var texts = new List<int>(); var bare = new List<int>(); var deltas = new List<string>(); var refused = new List<string>();
        for (int step = 0; step < 12; step++)
        {
            for (int s = 0; s < 10; s++)
            {
                var at = start.AddSeconds(step * 10 + s);
                if (s % 5 == 0 && roots < 148) { alive.Add((Id(roots), 0)); roots++; alive.Add((Id(roots), 0)); roots++; }   // four Roots come up every 10 s
                if (s % 2 == 0)   // every 2 s a swing that hits up to three Roots
                    for (int k = 0; k < Math.Min(3, alive.Count); k++)
                    {
                        var (id, hits) = alive[k]; Hit(at, id, "Root", "Mistlands", fire); alive[k] = (id, hits + 1);
                    }
                foreach (var dead in alive.Where(a => a.hits >= 3).ToList()) { Kill(at.AddSeconds(0.3), dead.id, "Root", dead.id.Id % 6 == 0 ? new[] { "Rowan" } : dead.id.Id % 7 == 0 ? new[] { "Ylva" } : new string[0]); alive.Remove(dead); }
                if (s % 3 == 0 && alive.Count > 0) Took(at, alive[alive.Count - 1].id, "Root", "Mistlands", new HitData.DamageTypes { m_poison = 7.75f });
                if (s == 4) { Hit(at, Id(Gjall), "Gjall", "Mistlands", pierce); Took(at, Id(Gjall), "Gjall", "Mistlands", new HitData.DamageTypes { m_fire = 22.5f, m_blunt = 14 }); }
                if (s % 2 == 0) { rec.Near(at, "Rowan", 2); rec.Near(at, "Ylva", 2); }
                rec.Tick(at);
            }
            var now = GroupShare.SharedCopy(Full("s1-1"));
            var made = LiveDelta.Make(baseShared, now, "s1-1", step + 1);
            var inc = LiveDelta.Make(prev, now, "s1-1", step + 1);
            cumulative.Add(made.Packed?.Length ?? 0); texts.Add(made.Text?.Length ?? 0); steps.Add(inc.Packed?.Length ?? 0); deltas.Add(made.Text); prev = now;
            bare.Add(LiveDelta.Make(baseBare, GroupShare.SharedCopy(Full("s1-1", false)), "s1-1", step + 1).Packed?.Length ?? 0);   // the same update without the feed (0.8.1)
            if (made.Text == null || !ServerIntake.IsJsonObject(made.Text) || !LiveDelta.Read(made.Text, out _, out _) || LiveDelta.Private(made.Text) || LiveDelta.Identity(made.Text) || made.TooBig)
                refused.Add("step " + (step + 1) + (made.TooBig ? " too big" : " refused"));
        }
        var nowFull = Full("s1-1"); var nowShared = GroupShare.SharedCopy(nowFull);
        var feedBytes = Transport.Pack(nowFull).Length - Transport.Pack(Snapshot.Build("0.8.2", 77, "Edda", stats, skills, "Midgard", dmg, "s1", ev, log, true, copyId: "s1-1")).Length;
        System.Console.WriteLine("  busy fight (48 Roots and a Gjall in 2 min): each 10 s update on its own " + string.Join(", ", steps) + " bytes packed; the update the server gets (since the last full copy) " +
                          string.Join(", ", cumulative) + " bytes packed (" + texts.Last() + " bytes of JSON at 2 min), without the feed " + string.Join(", ", bare) + "; the limit " + LiveDelta.MaxPacked + "; the feed adds " + feedBytes + " bytes to a packed full copy");
        string FeedOf(string json) => MiniJson.Write(MiniJson.Obj(MiniJson.Parse(json) as Dictionary<string, object>, "battleFeed"));
        Check(refused.Count == 0 && cumulative.Max() <= LiveDelta.MaxPacked && steps.Max() * 3 <= LiveDelta.MaxPacked && FeedOf(nowFull) == FeedOf(nowShared) && FeedOf(nowFull).Length > 1000,
              "FS busy fight: every 10 s update of 48 Roots and a Gjall is one message the 0.8.1 server keeps (its JSON object, base and seq, nothing private, no identity key): on its own at most " + steps.Max() +
              " bytes packed, since the last full copy at most " + cumulative.Max() + " (limit " + LiveDelta.MaxPacked + "); the full copy loses nothing to the shared form" + (refused.Count > 0 ? ": " + string.Join(", ", refused) : ""));

        // ---------- what the fellow reads: the full copy the server stored, the latest update applied, read as a book ----------
        var shown = LiveDelta.Apply(Stored(baseFull), deltas.Last());
        var book = shown == null ? null : PanelInput.FromSnapshot(shown);
        var direct = FeedShare.ReadFrom(MiniJson.Obj(MiniJson.Parse(nowShared) as Dictionary<string, object>, "battleFeed"));
        var sent = FeedShare.From(rec, sharers.Contains);
        var feed = book?.SharedFeed;
        bool Same(FoeEncounter a, FoeEncounter b) => a != null && b != null && a.Kind == b.Kind && a.Biome == b.Biome && a.Fight == b.Fight && a.OutcomeText == b.OutcomeText &&
            Math.Abs((a.StartUtc - b.StartUtc).TotalSeconds) < 1 && Math.Abs((a.EndUtc - b.EndUtc).TotalSeconds) < 1 && a.Dealt.Zip(b.Dealt, (x, y) => Math.Abs(x - y)).Max() < 0.001 && a.Received.Zip(b.Received, (x, y) => Math.Abs(x - y)).Max() < 0.001;
        var sentFoes = sent.Fights.SelectMany(f => f.Entries).ToList(); var readFoes = feed?.Fights.SelectMany(f => f.Entries).ToList() ?? new List<FoeEncounter>();
        Check(book != null && Describe(feed) == Describe(direct) && sentFoes.Count == readFoes.Count && sentFoes.All(e => Same(e, readFoes.FirstOrDefault(x => x.Seq == e.Seq))) &&
              feed.Fights.Select(f => f.Number + ":" + f.Foes + ":" + f.Open + ":" + f.Outside + ":" + string.Join("+", f.With())).SequenceEqual(sent.Fights.Select(f => f.Number + ":" + f.Foes + ":" + f.Open + ":" + f.Outside + ":" + string.Join("+", f.With()))) &&
              PanelModel.BattleFeed(book)?.Count == 2 && PanelModel.BattleFeed(book, 1)[0].Number == rec.Fights()[0].Number,
              "FS round trip: Edda's feed through Snapshot, the stored copy and the latest live update reads back as her recorder holds it (" + readFoes.Count + " foes in " + feed?.Fights.Count +
              " fights, the busy one still on, the same outcomes, names and counts, damage to 0.001, times to the second), the same as her full copy now; her last fight is the busy one");

        // ---------- bounded both ways ----------
        var many = new BattleRecorder(); var at2 = t0;
        foreach (var size in new[] { 30, 50, 60 }) { for (uint i = 0; i < size; i++) many.Dealt(at2 = at2.AddSeconds(1), new FoeId(9, (uint)(size * 1000 + i)), "Seeker", "$enemy_seeker", "Mistlands", fire); many.Tick(at2 = at2.AddMinutes(3)); }
        var capped = FeedShare.From(many, null);
        var big = new BattleRecorder();
        for (uint i = 0; i < 130; i++) big.Dealt(t0.AddSeconds(i), new FoeId(9, 7000 + i), "Seeker", "$enemy_seeker", "Mistlands", fire);
        var bigShared = FeedShare.From(big, null); var bigJson = new Json().Open(); bigShared.WriteTo(bigJson);
        var bigRead = FeedShare.ReadFrom(MiniJson.Obj(MiniJson.Parse(bigJson.Close().ToString()) as Dictionary<string, object>, "battleFeed"));
        var hostile = new System.Text.StringBuilder("{\"t0\":\"2026-10-10T20:00:00Z\",\"foes\":{");
        for (int i = 0; i < 500; i++) hostile.Append(i > 0 ? "," : "").Append('"').Append(i).Append("\":{\"f\":1,\"k\":\"Seeker\",\"s\":1e300,\"e\":-5,\"o\":42,\"d\":[1e99,\"x\",-3]}");
        var hostileRead = FeedShare.ReadFrom(MiniJson.Parse(hostile.Append("}}").ToString()) as Dictionary<string, object>);
        Check(capped.Fights.Select(f => f.Foes).SequenceEqual(new[] { 60 }) && capped.LeftOut == 80 && bigShared.Fights.Count == 1 && bigShared.Fights[0].Entries.Count == FeedShare.MaxFoes &&
              bigRead.Fights[0].Foes == 130 && bigRead.Fights[0].Entries.Count == FeedShare.MaxFoes && bigRead.LeftOut == 30 &&
              hostileRead.Fights.Sum(f => f.Entries.Count) == FeedShare.MaxRead && hostileRead.Fights[0].Entries.All(e => e.Outcome == FoeOutcome.GotAway && e.Dealt[0] <= 1e9f && e.Dealt[2] == 0 && e.EndUtc >= e.StartUtc),
              "FS bounds: whole fights while they fit in " + FeedShare.MaxFoes + " foes (60 of 140, the rest counted as left out); a fight of 130 keeps its newest " + FeedShare.MaxFoes + " and says it had 130; a copy that says anything reads at most " + FeedShare.MaxRead + " foes, numbers clipped");
        return fails;
    }
}
