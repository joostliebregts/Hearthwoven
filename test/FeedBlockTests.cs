// 0.8.6 (a friend's older feed, work/hearthwoven-0.8.6/DESIGN-feed-blocks.md; src/FeedBlocks.cs): a friend's PC cuts each closed session into one block, the
// server stores what it read back, lists it in the group list and serves it to a friend who asks, the friend's PC keeps it beside the fellow cache.
// Here: what a block holds and what the server keeps of an upload, once and never replaced, the bounds, the size limits and the size of a busy evening,
// who sees a block, the list tag older clients stop at, the upload plan and the cache. Fictional players: Rowan (you), Edda and Finch share, Ylva does not,
// Tor appears only as a fellow's side. Files in a fresh temp folder.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Hearthwoven;

static class FeedBlockTests
{
    const string World = "Midgard-123456789", Other = "Ashlands-987";
    static readonly DateTime Now = new DateTime(2026, 10, 10, 22, 30, 0, DateTimeKind.Utc);
    static readonly string[][] Kinds =
    {
        new[] { "Greydwarf", "BlackForest" }, new[] { "Troll", "BlackForest" }, new[] { "Draugr", "Swamp" }, new[] { "Wraith", "Swamp" }, new[] { "Seeker", "Mistlands" },
        new[] { "Gjall", "Mistlands" }, new[] { "Tick", "Mistlands" }, new[] { "Deathsquito", "Plains" }, new[] { "Lox", "Plains" }, new[] { "Fenring", "Mountain" },
    };
    static readonly HashSet<string> Sharers = new HashSet<string>(new[] { "Rowan", "Edda", "Finch", "Tor" }, StringComparer.OrdinalIgnoreCase);

    // a session id: 12 hex characters
    static int Seed(string s) { int h = 17; foreach (var c in s) h = h * 31 + c; return h; }   // string.GetHashCode differs per run
    static string Id(int n) => (0x3f9a0c1e2b7dL + n * 104729L).ToString("x12");

    static HitData.DamageTypes Dmg(Random r, int kind) => new HitData.DamageTypes
    {
        m_slash = (float)Math.Round(10 + r.NextDouble() * 60, 2), m_pierce = kind % 2 == 0 ? (float)Math.Round(r.NextDouble() * 40, 1) : 0,
        m_fire = kind % 3 == 0 ? (float)Math.Round(r.NextDouble() * 25, 2) : 0, m_poison = kind % 4 == 1 ? (float)Math.Round(r.NextDouble() * 12, 1) : 0,
    };

    /// <summary>One evening of <paramref name="fights"/> fights of <paramref name="perFight"/> foes that ended at <paramref name="end"/>: varied kinds and biomes, damage both ways, Edda and
    /// Finch (who share) and Ylva (who does not) in some of them, a death. The session as the book keeps it (KeptFights.Take): yours, with hits and foe ids.</summary>
    static KeptSession Evening(string id, DateTime end, int fights, int perFight, IEnumerable<FightSide> fellows = null)
    {
        var r = new Random(Seed(id)); var rec = new BattleRecorder();
        var t = end.AddMinutes(-(fights * (perFight * 8 / 60.0 + 3) + 5)); uint seq = (uint)(Math.Abs(Seed(id)) % 100000) * 100;
        for (int f = 0; f < fights; f++)
        {
            for (int k = 0; k < perFight; k++)
            {
                var kind = Kinds[r.Next(Kinds.Length)]; var foe = new FoeId(-900, ++seq); var token = "$enemy_" + kind[0].ToLowerInvariant();
                rec.Dealt(t, foe, kind[0], token, kind[1], Dmg(r, k)); rec.Dealt(t.AddSeconds(1), foe, kind[0], token, kind[1], Dmg(r, k + 1));
                if (r.Next(3) > 0) rec.Received(t.AddSeconds(2), foe, kind[0], token, kind[1], Dmg(r, k + 2));
                var who = new[] { "Edda", "Finch", "Ylva" }.Where(_ => r.Next(3) == 0).ToArray();
                if (who.Length > 0) rec.Attackers(foe, 1 + who.Length, who);
                rec.Killed(t.AddSeconds(3), token, 1 + who.Length); rec.Destroyed(t.AddSeconds(3.5), foe);
                foreach (var nm in new[] { "Edda", "Finch", "Ylva" }) if (r.Next(2) == 0) rec.Near(t, nm, 2);
                t = t.AddSeconds(8);
            }
            if (f == 2) rec.Died(t.AddSeconds(1));
            t = t.AddMinutes(3);
        }
        rec.Tick(t);
        return KeptFights.Take(World, id, end.AddMinutes(-(fights * (perFight * 8 / 60.0 + 3) + 5)), end, rec, "Rowan", Sharers.Contains, fellows);
    }

    static string Describe(FeedShare s) => string.Join("\n", s.Fights.Select(f => "fight " + f.Number + " " + f.Foes + " out " + f.Outside + " near " + string.Join(",", (f.Near ?? new Dictionary<string, float>()).OrderBy(k => k.Key).Select(k => k.Key + "=" + Json.F(k.Value))) +
        "\n" + string.Join("\n", f.Entries.Select(e => "  " + e.Seq + " " + e.Kind + " " + e.Biome + " " + (long)(e.StartUtc - s.T0).TotalSeconds + "-" + (long)(e.EndUtc - s.T0).TotalSeconds + " " + e.OutcomeText + " w" + string.Join("+", e.With) + " u" + e.Outside +
        " d" + string.Join(",", e.Dealt.Select(v => Json.F(v))) + " r" + string.Join(",", e.Received.Select(v => Json.F(v))))))) + "\nfalls " + s.Falls.Count;

    /// <summary>A block of exactly <paramref name="foes"/> foes (fights of up to five) that ended at <paramref name="end"/>.</summary>
    static FeedBlock Block(string id, DateTime end, int foes)
    {
        var s = Evening(id, end, (foes + 4) / 5, 5);
        // trim the last fight to the exact count
        var fights = s.Mine.Fights; int extra = fights.Sum(f => f.Entries.Count) - foes;
        for (int i = fights.Count - 1; i >= 0 && extra > 0; i--) { while (extra > 0 && fights[i].Entries.Count > 0) { fights[i].Entries.RemoveAt(fights[i].Entries.Count - 1); extra--; } }
        s.Mine.Fights = fights.Where(f => f.Entries.Count > 0).ToList();
        return FeedBlock.Read(FeedBlock.Write(s));
    }

    static string Fresh() { var d = Path.Combine(Path.GetTempPath(), "hw-blocks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static void Shares(string players, string owner, DateTime written)
    {
        Directory.CreateDirectory(players); var f = Path.Combine(players, owner + ".share.json");
        File.WriteAllText(f, "{\"name\":\"" + owner + "\"}"); File.SetLastWriteTimeUtc(f, written);
    }

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var inv = CultureInfo.InvariantCulture;
        var dir = Fresh(); var players = Path.Combine(dir, "players");

        // ---------- FB content: what a block holds, and what the server keeps of an upload that holds more ----------
        var tor = new BattleRecorder(); var torFoe = new FoeId(-900, 1);
        tor.Dealt(Now.AddHours(-1), torFoe, "Troll", "$enemy_troll", "BlackForest", new HitData.DamageTypes { m_slash = 99 }); tor.Tick(Now);
        var session = Evening(Id(1), Now.AddHours(-2), 6, 5);
        var tors = FightSide.Of(tor, "Tor"); tors.Self = false; tors.Key = "k-tor"; session.Sides.Add(tors);   // a fellow's part beside yours: never written
        var withHits = session.Mine.Fights.SelectMany(f => f.Entries).Any(e => e.HitsDealt > 0) && session.Mine.Fights.SelectMany(f => f.Entries).Any(e => !e.Id.IsNone);
        var json = FeedBlock.Write(session);
        var hostile = json.Replace("{\"v\":1,", "{\"v\":1,\"x\":1,").Replace(",\"s\":", ",\"pos\":[1.5,2.5],\"hd\":7,\"hr\":2,\"i\":\"-900:1\",\"s\":");
        var store = new BlockStore(players, World);
        Shares(players, "1001", Now);
        var put = store.PutPacked("1001", Transport.Pack(hostile), Now, "ffffffffffff");
        var stored = Directory.GetFiles(Path.Combine(players, "1001", "feed", World), "*.json").Select(File.ReadAllText).FirstOrDefault() ?? "";
        var served = store.Serve("2002", Id(1), Now);
        var back = served == null ? null : FeedBlock.Read(served);
        var mineFeed = FeedShare.ReadFrom(MiniJson.Obj((Dictionary<string, object>)MiniJson.Parse(json), "battleFeed"));
        Check(withHits && hostile != json && put == null && stored.Length > 0 && !json.Contains("\"hd\"") && !json.Contains("\"hr\"") && !json.Contains("\"i\":") && !json.Contains("Tor") &&
              !stored.Contains("\"hd\"") && !stored.Contains("\"hr\"") && !stored.Contains("\"i\":") && !stored.Contains("\"x\":") && !stored.Contains("\"pos\"") && !stored.Contains("Tor") && !stored.Contains("\"boss\"") &&
              back != null && back.Id == Id(1) && back.FromUtc == session.FromUtc.ToUniversalTime() && back.ToUtc == session.ToUtc && Describe(back.Feed) == Describe(mineFeed) &&
              back.Foes == session.Mine.Fights.Sum(f => f.Entries.Count) && back.Session.Mine.Fights.All(f => !f.Open && f.Kept == back.Session) && back.Feed.Falls.Count == session.Mine.Falls.Count && back.Feed.Falls.Count > 0,
              "FB content: a block is your side of one closed session in the shared copy's form (" + back?.Foes + " foes in " + back?.Feed.Fights.Count + " fights, " + back?.Feed.Falls.Count + " fall); your hits, the foes' network ids, a fellow's part and an unknown key (x, pos) are not in what the server stores and serves, and it reads back with the same fights, foes, falls and times");

        // ---------- FB once: written once, never replaced, by anyone; the running session never ----------
        var other = Block(Id(1), Now.AddHours(-2), 12);   // the same id, other content
        var again = store.Put("1001", other, Now, null);
        var thief = store.Put("3003", other, Now, null);
        var clone = FeedBlock.Read(FeedBlock.Write(Evening(Id(2), Now.AddHours(-5), 4, 5)));
        var running = store.Put("1001", clone, Now, Id(2));
        var badId = FeedBlock.Read(FeedBlock.Write(Evening(Id(3), Now.AddHours(-6), 4, 5))); badId.Id = "NOTHEX";
        var bad = store.Put("1001", badId, Now, null);
        var unsafeOwner = store.Put("../evil", clone, Now, null);
        var bareJson = FeedBlock.Write(Evening("xyz", Now, 2, 2));
        var future = FeedBlock.Read(FeedBlock.Write(Evening(Id(4), Now.AddHours(1), 2, 2))); var inFuture = store.Put("1001", future, Now, null);
        var still = FeedBlock.Read(store.Serve("2002", Id(1), Now));
        Check(again == "held" && thief == "held" && running == "the running session" && bad == "bad id" && unsafeOwner != null && bareJson == null && inFuture == "in the future" &&
              still.Foes == session.Mine.Fights.Sum(f => f.Entries.Count) && store.Rows("1001", Now).Count == 1 && store.Rows("3003", Now).Count == 0,
              "FB once: an id the server holds is never replaced (the same owner: " + again + ", another owner: " + thief + "); the owner's running session, a bad id, an unsafe owner key, an id that is no session id and a block ending in the future are refused (" +
              running + ", " + bad + ", " + inFuture + ")");

        // ---------- FB bounds: 30 days, 60 blocks, 2 000 foes; newest kept; a restart lists the same ----------
        var bd = Fresh(); var bp = Path.Combine(bd, "players");
        var blocks = Enumerable.Range(0, 70).Select(i => Block(Id(100 + i), Now.AddHours(-i * 14 - 1), 43)).ToList();   // 70 blocks of 43 foes over 41 days
        var s1 = new BlockStore(bp, World); var refusals = new List<string>();
        foreach (var b in blocks) { var why = s1.Put("1001", b, Now, null); if (why != null) refusals.Add(why); }
        var rows1 = s1.Rows("1001", Now);
        // the newest while they fit: 30 days, 60 blocks, 2 000 foes (46 blocks of 43 = 1 978)
        var expected = new List<string>(); int sum = 0;
        foreach (var b in blocks.OrderByDescending(b => b.ToUtc)) { if (b.ToUtc < Now.AddDays(-30) || expected.Count >= 60 || sum + b.Foes > 2000) break; expected.Add(b.Id); sum += b.Foes; }
        var s2 = new BlockStore(bp, World);   // a restart: the index is rebuilt from the files
        var rows2 = s2.Rows("1001", Now);
        var shuffled = new BlockStore(Path.Combine(Fresh(), "players"), World);
        foreach (var b in blocks.OrderBy(b => Seed(b.Id))) shuffled.Put("1001", b, Now, null);
        var files = Directory.GetFiles(Path.Combine(bp, "1001", "feed", World), "*.json").Length;
        Check(rows1.Select(r => r.Id).SequenceEqual(expected) && rows1.Count <= 60 && rows1.Sum(r => r.Foes) <= 2000 && rows1.All(r => r.ToUtc >= Now.AddDays(-30)) && rows1.Count == 46 &&
              rows2.Select(r => r.Id).SequenceEqual(expected) && shuffled.Rows("1001", Now).Select(r => r.Id).SequenceEqual(expected) && files == expected.Count && refusals.Count == 70 - expected.Count,
              "FB bounds: 70 blocks of 43 foes over 41 days keep the newest " + rows1.Count + " (" + rows1.Sum(r => r.Foes) + " foes, none older than 30 days; at most 60 and 2 000), in any order they arrive, the files match, and a new store on the same folder lists the same");

        // ---------- FB limits: before any parse; a busy evening's size ----------
        var rnd = new byte[60000]; new Random(5).NextBytes(rnd);
        var tooBig = store.PutPacked("1001", rnd, Now, null);
        var bomb = store.PutPacked("1001", Transport.Pack(new string('0', 600000)), Now, null);
        var notJson = store.PutPacked("1001", Transport.Pack("hello"), Now, null);
        var many = new BattleRecorder(); var mt = Now.AddHours(-9);
        for (uint i = 0; i < 300; i++) many.Dealt(mt.AddSeconds(i), new FoeId(-700, 500 + i), "Seeker", "$enemy_seeker", "Mistlands", new HitData.DamageTypes { m_pierce = 40, m_fire = 6 });
        many.Tick(mt.AddMinutes(30));
        var big = FeedShare.From(many, null, 300); var bj = new Json().Open().Num("v", 1).Str("id", Id(7)).Str("from", mt.ToString("o", inv)).Str("to", mt.AddMinutes(10).ToString("o", inv)); big.WriteTo(bj, "battleFeed", ids: false);
        var bigJson = bj.Close().ToString();
        var three = store.PutPacked("1001", Transport.Pack(bigJson), Now, null);
        var capped = store.Rows("1001", Now).FirstOrDefault(r => r.Id == Id(7));
        var evening = Evening(Id(8), Now.AddHours(-3), 30, 5);
        var largest = Evening(Id(9), Now.AddHours(-3), 40, 5);   // 200 foes, the most a block holds
        var eveningRaw = FeedBlock.Write(evening); var eveningPacked = Transport.Pack(eveningRaw);
        var friends = Enumerable.Range(0, 4).Select(k => new KeyValuePair<string, List<BlockRow>>("Steam_76561198" + (100000 + k) + "#" + (770 + k),
            Enumerable.Range(0, 14).Select(i => new BlockRow { Id = Id(1000 + k * 100 + i), FromUtc = Now.AddDays(-i * 2).AddHours(-3), ToUtc = Now.AddDays(-i * 2), Foes = 100 + i * 7 }).ToList())).ToList();
        var indexJson = BlockIndex.Write(Enumerable.Range(0, 14).Select(i => Id(2000 + i)), friends);
        var indexPacked = Transport.Pack(indexJson);
        System.Console.WriteLine("  feed blocks, a busy evening (" + evening.Mine.Fights.Sum(f => f.Entries.Count) + " foes in " + evening.Mine.Fights.Count + " fights, 10 kinds, 5 biomes, Edda, Finch and Ylva in some, a fall): " + (eveningRaw.Length / 1024.0).ToString("0.0", inv) +
                          " KB raw, " + (eveningPacked.Length / 1024.0).ToString("0.0", inv) + " KB packed (" + ((double)eveningRaw.Length / eveningPacked.Length).ToString("0.0", inv) + " to 1, " + (eveningPacked.Length + Fragments.Size - 1) / Fragments.Size + " parts); the largest block (200 foes) " +
                          (Transport.Pack(FeedBlock.Write(largest)).Length / 1024.0).ToString("0.0", inv) + " KB packed, " + (FeedBlock.Write(largest).Length / 1024.0).ToString("0.0", inv) + " KB raw; the index for four friends with 14 sessions each " + (indexJson.Length / 1024.0).ToString("0.0", inv) + " KB raw, " + (indexPacked.Length / 1024.0).ToString("0.0", inv) + " KB packed");
        Check(tooBig != null && tooBig.StartsWith("packed over") && bomb != null && bomb.StartsWith("unpacked") && notJson == "not one JSON object" && three == null && capped != null && capped.Foes == 200 &&
              evening.Mine.Fights.Sum(f => f.Entries.Count) == 150 && eveningPacked.Length < 12 * 1024 && largest.Mine.Fights.Sum(f => f.Entries.Count) == 200 && Transport.Pack(FeedBlock.Write(largest)).Length < 10 * 1024 && indexPacked.Length < 1024,
              "FB limits: a block over 48 000 bytes packed (16 parts of 3 000) and a zero-filled gzip that unpacks over 128 KB are refused before any parse (" + tooBig + "; " + bomb + "), one of 300 foes is stored with " + capped?.Foes +
              "; a busy evening of 150 foes is " + eveningRaw.Length / 1024 + " KB raw and " + (eveningPacked.Length / 1024.0).ToString("0.0", inv) + " KB packed (under 12), a block of 200 foes under 10, the index for four friends " + indexPacked.Length + " bytes packed (under 1 024)");

        // ---------- FB unshare / serve: who sees a block ----------
        var sd = Fresh(); var sp = Path.Combine(sd, "players");
        var home = new BlockStore(sp, World); var away = new BlockStore(sp, Other);
        var edda = FeedBlock.Read(FeedBlock.Write(Evening(Id(30), Now.AddHours(-4), 3, 5))); var eddaAway = FeedBlock.Read(FeedBlock.Write(Evening(Id(31), Now.AddHours(-8), 3, 5)));
        home.Put("EddaKey", edda, Now, null); away.Put("EddaKey", eddaAway, Now, null);
        var finch = FeedBlock.Read(FeedBlock.Write(Evening(Id(32), Now.AddHours(-5), 3, 5))); home.Put("FinchKey", finch, Now, null);
        Shares(sp, "EddaKey", Now.AddDays(-15)); Shares(sp, "FinchKey", Now.AddDays(-1)); Shares(sp, "RowanKey", Now);
        var oldCopy = home.Listed("EddaKey", Now).Count == 0 && home.Serve("RowanKey", Id(30), Now) == null;   // 15 days: her copy is no longer served, nor her blocks
        Shares(sp, "EddaKey", Now.AddDays(-2));
        var served2 = home.Listed("EddaKey", Now).Count == 1 && home.Serve("RowanKey", Id(30), Now) != null;
        var foreign = away.Serve("RowanKey", Id(31), Now) != null && home.Serve("RowanKey", Id(31), Now) == null;   // each world serves its own
        var own = home.Serve("EddaKey", Id(30), Now) == null;                                                        // never to its own owner
        File.Delete(Path.Combine(sp, "FinchKey.share.json"));
        var noShare = home.Listed("FinchKey", Now).Count == 0 && home.Serve("RowanKey", Id(32), Now) == null;
        home.Forget("EddaKey");
        var gone = !Directory.Exists(Path.Combine(sp, "EddaKey", "feed")) && home.Rows("EddaKey", Now).Count == 0 && home.Serve("RowanKey", Id(30), Now) == null && new BlockStore(sp, Other).Rows("EddaKey", Now).Count == 0;
        // the sweep: a block of a world no one plays ages out
        var old = FeedBlock.Read(FeedBlock.Write(Evening(Id(33), Now.AddDays(-20), 2, 5)));
        new BlockStore(sp, "Old-1").Put("FinchKey", old, Now.AddDays(-20), null);
        var swept = BlockStore.SweepAll(sp, Now);
        Check(oldCopy && served2 && foreign && own && noShare && gone && swept == 0 && Directory.GetFiles(Path.Combine(sp, "FinchKey", "feed", "Old-1")).Length == 1 &&
              BlockStore.SweepAll(sp, Now.AddDays(15)) >= 1 && !Directory.Exists(Path.Combine(sp, "FinchKey", "feed", "Old-1")),
              "FB unshare/serve: a block is listed and served only while its owner's copy is served (14 days), only in the server's own world, never to its owner; sharing off deletes the owner's blocks of every world; a world no one plays ages out at the next sweep");

        // the age bound: a block that ended 29.5 days ago is listed and served; 3 days later it is past 30 days, so neither
        var ap = Path.Combine(sd, "aged"); var agedStore = new BlockStore(ap, World);
        var agedBlock = FeedBlock.Read(FeedBlock.Write(Evening(Id(50), Now.AddDays(-29.5), 3, 5)));
        agedStore.Put("EddaKey", agedBlock, Now, null); Shares(ap, "EddaKey", Now.AddDays(-1));
        var agedListed = agedStore.Listed("EddaKey", Now).Count == 1 && agedStore.Serve("RowanKey", Id(50), Now) != null;
        var agedLater = agedStore.Listed("EddaKey", Now.AddDays(3)).Count == 0 && agedStore.Rows("EddaKey", Now.AddDays(3)).Count == 0 && agedStore.Serve("RowanKey", Id(50), Now.AddDays(3)) == null;
        Check(agedListed && agedLater, "FB age: a block that ended 29.5 days ago is listed and served; at 32.5 days it is neither listed nor served (the 30-day bound Put and Trim keep, now in Rows, Listed and Serve)");

        // ---------- FB wire: the list tag, and what an older client reads ----------
        var names = new List<string> { "Edda", "Finch" }; var keys = new List<string> { "Steam_1#77", "Steam_2#78" };
        var list = GroupShare.WriteList(names, "{\"self\":{}}", keys, live: true, blocksJson: indexJson);
        var bytes = list.GetArray();
        var read = GroupShare.ReadList(new ZPackage(bytes), out var books, out var rkeys, out var live, out var indexText);
        var ix = BlockIndex.Read(indexText);
        // a 0.8.5 reader: the names, the book, the keys and the live tag; any other tag ends it
        var oldp = new ZPackage(bytes); int n = oldp.ReadInt(); var oldNames = new HashSet<string>(); for (int i = 0; i < n; i++) oldNames.Add(oldp.ReadString());
        string oldBooks = null; List<string> oldKeys = null; bool oldLive = false; string stoppedAt = null;
        while (oldp.GetPos() < oldp.Size())
        {
            var tag = oldp.ReadString();
            if (tag == GroupShare.BooksTag) oldBooks = Transport.Unpack(oldp.ReadByteArray());
            else if (tag == GroupShare.KeysTag) { int k = oldp.ReadInt(); oldKeys = new List<string>(); for (int i = 0; i < k; i++) oldKeys.Add(oldp.ReadString()); }
            else if (tag == GroupShare.LiveTag) oldLive = oldp.ReadInt() >= LiveDelta.Version;
            else { stoppedAt = tag; break; }
        }
        var noBlocks = GroupShare.ReadList(new ZPackage(GroupShare.WriteList(names, null, keys, live: true).GetArray()), out _, out _, out var live2, out var none);
        // hostile input stays inside its caps
        var hostileRows = string.Join(",", Enumerable.Range(0, 500).Select(i => "[\"" + (i % 3 == 0 ? "zz" : Id(5000 + i)) + "\",-1e30,1e30,1e12]"));
        var hostileIx = BlockIndex.Read("{\"v\":1,\"mine\":[" + string.Join(",", Enumerable.Range(0, 500).Select(i => "\"" + Id(6000 + i) + "\"")) + "],\"of\":{" +
                                        string.Join(",", Enumerable.Range(0, 500).Select(i => "\"f" + i + "\":[" + hostileRows + "]")) + "}}");
        Check(read.SetEquals(names) && books == "{\"self\":{}}" && rkeys.SequenceEqual(keys) && live && ix != null && ix.Mine.Count == 14 && ix.Of.Count == 4 && ix.Of.Values.All(l => l.Count == 14) &&
              ix.Of["Steam_76561198100000#770"][0].Id == Id(1000) && ix.Of["Steam_76561198100000#770"][0].Foes == 100 &&
              oldNames.SetEquals(names) && oldBooks == "{\"self\":{}}" && oldKeys.SequenceEqual(keys) && oldLive && stoppedAt == GroupShare.BlocksTag &&
              none == null && live2 && BlockIndex.Read("not json") == null && BlockIndex.Read("{\"v\":2}") == null &&
              hostileIx != null && hostileIx.Mine.Count == 60 && hostileIx.Of.Count == FellowCache.MaxFellows && hostileIx.Of.Values.All(l => l.Count <= 60 && l.All(r => r.ToUtc <= new DateTime(2100, 1, 1) && r.FromUtc >= new DateTime(1970, 1, 1) && r.Foes <= FeedShare.MaxRead)),
              "FB wire: the list carries the blocks tag after the live tag; the new reader gets the index, a 0.8.5 reader gets the same names, book, keys and live and stops at the tag; a list without it reads as before; a hostile index (500 fellows of 500 rows, bad ids, huge times) stays within " +
              FellowCache.MaxFellows + " fellows, 60 rows each, 60 own ids");

        // ---------- FB upload plan: which sessions go, in what order ----------
        var plan = new List<KeptSession>();
        for (int i = 0; i < 8; i++) plan.Add(OneNight(Id(70 + i), Now.AddDays(-i * 5).AddHours(-1)));
        var mineIds = new HashSet<string> { Id(71) };   // the server holds it already
        var earlier = plan.Where(s => s.Id != Id(70)).ToList();   // Earlier(world, running): everything but the running connection's
        var tried = new HashSet<string>(); var order = new List<string>();
        for (List<KeptSession> next; (next = Uploads.Plan(earlier, mineIds, tried, Now)).Count > 0;) { order.Add(next[0].Id); tried.Add(next[0].Id); }
        var expectedOrder = earlier.Where(s => s.ToUtc >= Now.AddDays(-30) && s.Id != Id(71)).OrderByDescending(s => s.ToUtc).Select(s => s.Id).ToList();
        Check(order.SequenceEqual(expectedOrder) && order.Count == 4 && !order.Contains(Id(70)) && !order.Contains(Id(71)) && order.Distinct().Count() == order.Count && !order.Contains(Id(77)) && Uploads.Plan(null, null, null, Now).Count == 0 &&
              Uploads.Pack(plan[2], out var problem) != null && problem == null && Uploads.Pack(new KeptSession { Id = Id(1) }, out var empty) == null && empty != null,
              "FB upload plan: newest first, each once per connection; never the running session, a session the server holds, or one that ended over 30 days ago (" + string.Join(" ", order.Select(i => i.Substring(8))) + ")");

        // ---------- FB cache: on this PC beside the fellow cache, only what the list names; the client state ----------
        var root = Path.Combine(Fresh(), "fellows"); var worldDir = Path.Combine(root, World); Directory.CreateDirectory(worldDir);
        var b1 = Block(Id(40), Now.AddHours(-3), 20); var b2 = Block(Id(41), Now.AddHours(-30), 25); var b3 = Block(Id(42), Now.AddHours(-9), 15);
        BlockCache.Save(worldDir, "Steam_1#77", b1); BlockCache.Save(worldDir, "Steam_1#77", b2); BlockCache.Save(worldDir, "Steam_2#78", b3);
        FellowCache.Save(worldDir, "Steam_1#77", "{\"playerId\":1,\"name\":\"Edda\"}", Now);   // the fellow's own copy beside the blocks
        var loadedBack = BlockCache.Load(worldDir, "Steam_1#77", Id(40));
        var allThree = BlockCache.Load(worldDir, "Steam_2#78", Id(42)) != null && BlockCache.Load(worldDir, "Steam_1#77", Id(41)) != null && BlockCache.Load(worldDir, "Steam_2#78", Id(40)) == null;
        var coexist = Directory.GetFiles(worldDir, "*.json").Length == 1 && FellowCache.Load(worldDir, Now).Count == 0;   // top-level files only: the fake copy is no fellow, and the blocks folder is not read as one
        FellowCache.KeepOnly(worldDir, new[] { "Steam_1#77" });   // the fellow cache's own sweep leaves the blocks folder alone
        var untouched = BlockCache.Load(worldDir, "Steam_2#78", Id(42)) != null;
        // the list names one block of the first fellow and none of the second
        BlockCache.KeepOnly(worldDir, new Dictionary<string, List<BlockRow>> { { "Steam_1#77", new List<BlockRow> { b1.Row } } });
        var pruned = BlockCache.Load(worldDir, "Steam_1#77", Id(40)) == loadedBack && BlockCache.Load(worldDir, "Steam_1#77", Id(41)) == null && !Directory.Exists(BlockCache.FolderOf(worldDir, "Steam_2#78"));
        BlockCache.Save(worldDir, "Steam_1#77", Block(Id(43), Now.AddDays(-40), 10));   // a block that ended 40 days ago ages out with the fellow cache's sweep
        FellowCache.SweepAll(root, Now);
        var aged = BlockCache.Load(worldDir, "Steam_1#77", Id(43)) == null && BlockCache.Load(worldDir, "Steam_1#77", Id(40)) != null;
        FellowCache.ForgetAll(root);   // you stopped sharing
        var forgotten = !Directory.Exists(root) && BlockCache.Load(worldDir, "Steam_1#77", Id(40)) == null;
        // the client state
        var asked = new List<string>(); var reads = new List<string>(); var changed = 0;
        var fb = new FellowBlocks { Ask = id => asked.Add(id), ReadCache = (f, id) => { reads.Add(f + "/" + id); return true; }, Changed = () => changed++ };
        var rowA = b1.Row; var rowB = b2.Row;
        fb.TakeRows(new Dictionary<string, List<BlockRow>> { { "Steam_1#77", new List<BlockRow> { rowB, rowA } } });   // kept newest first, whatever order the list gives
        var v0 = fb.Version;
        var s0 = fb.Want(b1.Id, 100); var second = fb.Want(b2.Id, 100);                         // one fetch at a time
        var vAsked = fb.Version;                                                               // going to Loading changes what the page says ("Getting ...")
        fb.Cached(b1.Id, null); fb.Drain(101);                                                 // this PC has no copy: the ask goes out
        var loadingAsk = s0 == FellowBlocks.State.Loading && second == FellowBlocks.State.NotAsked && asked.SequenceEqual(new[] { b1.Id }) && reads.SequenceEqual(new[] { "Steam_1#77/" + b1.Id });
        var stillLoading = fb.StateOf(b1.Id) == FellowBlocks.State.Loading && fb.Version == vAsked && vAsked > v0;
        fb.Drain(101 + FellowBlocks.AskTimeout - 1); var beforeTimeout = fb.StateOf(b1.Id) == FellowBlocks.State.Loading;
        fb.Drain(101 + FellowBlocks.AskTimeout + 1);
        var failed = fb.StateOf(b1.Id) == FellowBlocks.State.Failed && fb.Version > vAsked;
        var retried = fb.Retry(b1.Id, 200); var retryAsks = asked.Count;
        fb.Cached(b1.Id, null); fb.Drain(201);
        var wrong = fb.Arrived(b2.Id, FeedBlock.Canonical(b2));                                // not the block asked for
        var v1 = fb.Version;
        var arrived = fb.Arrived(b1.Id, FeedBlock.Canonical(b1));
        var session1 = fb.SessionOf(b1.Id, World, "Edda");
        var ready = arrived != null && wrong == null && fb.StateOf(b1.Id) == FellowBlocks.State.Ready && fb.Version > v1 && changed > 0 && session1 != null && session1.World == World && session1.Mine.Name == "Edda" && session1.Fights.Count == b1.Feed.Fights.Count &&
                    fb.Want(b1.Id, 210) == FellowBlocks.State.Ready && asked.Count == retryAsks + 1;   // from memory: no new ask
        fb.Want(b2.Id, 220); fb.Cached(b2.Id, FeedBlock.Canonical(b2)); fb.Drain(221);          // the second one comes from this PC's cache: no ask
        var fromCache = fb.StateOf(b2.Id) == FellowBlocks.State.Ready && asked.Count == retryAsks + 1;
        var b4 = Block(Id(44), Now.AddHours(-12), 12);
        fb.TakeRows(new Dictionary<string, List<BlockRow>> { { "Steam_1#77", new List<BlockRow> { rowB, rowA, b4.Row } } });
        fb.Want(b4.Id, 230); fb.Cached(b4.Id, null); fb.Drain(231); fb.Gone(b4.Id);            // the server no longer holds it
        var hidden = fb.RowsOf("Steam_1#77").Select(r => r.Id).SequenceEqual(new[] { rowA.Id, rowB.Id }) && fb.StateOf(b4.Id) == FellowBlocks.State.Gone;
        fb.TakeRows(new Dictionary<string, List<BlockRow>> { { "Steam_1#77", new List<BlockRow> { rowB, rowA } } });   // the next list: b4 is not in it, b1 and b2 stay in memory
        var afterList = fb.StateOf(b1.Id) == FellowBlocks.State.Ready && fb.StateOf(b4.Id) == FellowBlocks.State.NotAsked;
        fb.TakeRows(new Dictionary<string, List<BlockRow>> { { "Steam_1#77", new List<BlockRow> { rowA } } });          // the server pruned b2
        var pruned2 = fb.StateOf(b2.Id) == FellowBlocks.State.NotAsked && fb.SessionOf(b2.Id, World, "Edda") == null && fb.RowsOf("Steam_1#77").Count == 1;
        var exceptCopy = fb.RowsOf("Steam_1#77", rowA.Id).Count == 0;   // the session their running copy is leaves the list
        fb.Clear(); var cleared = fb.RowsOf("Steam_1#77").Count == 0 && fb.StateOf(b1.Id) == FellowBlocks.State.NotAsked;
        Check(loadedBack != null && FeedBlock.Read(loadedBack)?.Id == b1.Id && allThree && coexist && untouched && pruned && aged && forgotten &&
              loadingAsk && stillLoading && beforeTimeout && failed && retried == FellowBlocks.State.Loading && ready && fromCache && hidden && afterList && pruned2 && exceptCopy && cleared,
              "FB cache: a block is kept beside the fellow cache and only while the list names it (an unlisted fellow's folder and an unlisted id go; 30 days age out; stopping sharing deletes all); the client asks one block at a time (this PC's copy first, then the server), a timeout fails it after " +
              FellowBlocks.AskTimeout + " s and Try again asks once more, an answer for any other id is ignored, a gone block hides its row until the next list");

        // the ask spacing: the server drops an ask sooner than 2 s after the last one, so a block wanted 0.4 s after the first one arrived is held and asked at 2.5 s
        var spacedAsks = new List<KeyValuePair<string, double>>(); double clock = 300;
        var fbSpaced = new FellowBlocks { Ask = id => spacedAsks.Add(new KeyValuePair<string, double>(id, clock)) };
        fbSpaced.TakeRows(new Dictionary<string, List<BlockRow>> { { "Steam_1#77", new List<BlockRow> { rowB, rowA } } });
        fbSpaced.Want(b1.Id, clock);                                                          // the first ask goes out at once
        clock = 300.3; fbSpaced.Arrived(b1.Id, FeedBlock.Canonical(b1));
        clock = 300.4; var wantSecond = fbSpaced.Want(b2.Id, clock);                          // 0.4 s after the first ask: held, not dropped
        for (clock = 300.5; clock < 302.5; clock += 0.5) fbSpaced.Drain(clock);
        var heldUntilGap = spacedAsks.Count == 1 && fbSpaced.StateOf(b2.Id) == FellowBlocks.State.Loading;
        clock = 302.5; fbSpaced.Drain(clock);
        var sentAtGap = spacedAsks.Count == 2 && spacedAsks[1].Key == b2.Id && spacedAsks[1].Value - spacedAsks[0].Value >= 2.5;
        clock = 302.6; fbSpaced.Arrived(b2.Id, FeedBlock.Canonical(b2));
        for (clock = 303; clock < 340; clock += 1) fbSpaced.Drain(clock);                    // well past AskTimeout after the first ask: no failure
        var noDidNotArrive = fbSpaced.StateOf(b1.Id) == FellowBlocks.State.Ready && fbSpaced.StateOf(b2.Id) == FellowBlocks.State.Ready;
        Check(wantSecond == FellowBlocks.State.Loading && heldUntilGap && sentAtGap && noDidNotArrive,
              "FB ask spacing: an ask 0.4 s after the last one is held and sent at 2.5 s (the server's 2 s gap and half a second), never dropped; the first block's arrival leaves no \"did not arrive\" behind");

        // FB wait (0.8.6 test build 5, Joost: "the loading took a while ... We should have something of a progress indicator there"): the seconds a block has been on its way,
        // counted from when the feed wanted it (the held ask and the cache read included), whole seconds; none once it is here or for a block not on its way
        var fbWait = new FellowBlocks { Ask = id => { } };
        fbWait.TakeRows(new Dictionary<string, List<BlockRow>> { { "Steam_1#77", new List<BlockRow> { rowB, rowA } } });
        fbWait.Want(b1.Id, 400); fbWait.Arrived(b1.Id, FeedBlock.Canonical(b1));
        fbWait.Want(b2.Id, 400.4);                                                              // held 2.1 s by the ask spacing
        var waitNow = fbWait.WaitedSeconds(b2.Id, 400.4); var waitHeld = fbWait.WaitedSeconds(b2.Id, 401.9);
        fbWait.Drain(402.5); var waitSent = fbWait.WaitedSeconds(b2.Id, 406.8);
        fbWait.Arrived(b2.Id, FeedBlock.Canonical(b2));
        Check(waitNow == 0 && waitHeld == 1 && waitSent == 6 && fbWait.WaitedSeconds(b2.Id, 407) == -1 && fbWait.WaitedSeconds(b1.Id, 407) == -1 && fbWait.WaitedSeconds("000000000000", 407) == -1,
              "FB wait: a block on its way counts its seconds from when the feed wanted it (" + waitNow + ", " + waitHeld + " while its ask is held, " + waitSent + " after it went), none once it is here");

        // FB order (0.8.6 test build 5: a friend's block reached Rowan's PC 124 ms after a fellow's copy it had queued behind): the server sends one part per 100 ms to everyone, and a
        // block part takes turns with the copies and live updates already waiting, so a 2-part block is out in 3 parts' time instead of behind all 12; the copies still get every
        // other part when blocks keep coming; one cap counts both
        var q = new AnswerQueue<string>();
        for (int i = 0; i < 12; i++) q.Add("copy" + i);
        q.AddBlock("block0"); q.AddBlock("block1");
        var sent = new List<string>(); while (q.TryNext(out var part)) sent.Add(part);
        var blockDone = sent.IndexOf("block1") + 1;
        var busy = new AnswerQueue<string>(); var sentBusy = new List<string>();
        for (int i = 0; i < 10; i++) busy.Add("copy" + i);
        for (int tick = 0; tick < 20; tick++) { busy.AddBlock("block" + tick); if (busy.TryNext(out var p)) sentBusy.Add(p); }
        var copiesOut = sentBusy.Count(s => s.StartsWith("copy"));
        var counted = new AnswerQueue<string>(); counted.Add("a"); counted.AddBlock("b");
        Check(blockDone == 3 && sent.Count == 14 && sent.Where(s => s.StartsWith("copy")).SequenceEqual(Enumerable.Range(0, 12).Select(i => "copy" + i)) && copiesOut == 10 &&
              counted.Count == 2 && counted.Blocks.Count() == 1 && counted.Rest.Count() == 1,
              "FB order: a block asked behind 12 queued copy parts is out after " + blockDone + " parts (300 ms) instead of 14, the copies keep their order and get every other part (" + copiesOut + " of 20 while blocks keep coming), one count covers both");

        try { Directory.Delete(dir, true); Directory.Delete(bd, true); Directory.Delete(sd, true); } catch { }
        return fails;
    }

    /// <summary>A session of one wolf that ended at <paramref name="end"/>.</summary>
    static KeptSession OneNight(string id, DateTime end)
    {
        var r = new BattleRecorder(); r.Dealt(end.AddSeconds(-30), new FoeId(-999, 1), "Wolf", "$enemy_wolf", "Mountain", new HitData.DamageTypes { m_slash = 50 }); r.Tick(end);
        return KeptFights.Take(World, id, end.AddHours(-2), end, r, "Rowan", n => true, null);
    }
}
