// 0.9 deed feeds (work/hearthwoven-0.9/TRACK-feeds.md; src/FeedBlocks.cs FeedBlock, DeedBlockPart, Uploads, BlockIndex, FellowBlocks): a friend's deed
// stretches travel exactly as their earlier fights do since 0.8.6, in the same block of the same closed session. Here: the round trip of a block with
// fights and deeds and of one with deeds alone, what the server keeps of a hostile upload, its bounds, the two indexes and the list tag an older
// reader stops at, the upload plan joining the two stores, the shrinking of a block too large, a busy evening's size, the friend's PC's rows and
// states. Fictional players only (Rowan, Edda). Files in a fresh temp folder.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class DeedBlockTests
{
    static readonly DateTime Now = new DateTime(2026, 10, 18, 22, 30, 0, DateTimeKind.Utc);
    const string World = "Midgard-123456789";

    static string Id(int n) => (0xd0eed0000000L + n * 7919L).ToString("x12");

    static DeedStretch Stretch(string family, DateTime from, int minutes, params (string key, float n)[] counts)
    {
        var s = new DeedStretch { Family = family, FromUtc = from, ToUtc = from.AddMinutes(minutes) };
        foreach (var (k, n) in counts) s.Counts[k] = n;
        return s;
    }

    /// <summary>An evening of deeds (crafting with an upgrade and a first, building, woodcutting), closed at <paramref name="end"/>.</summary>
    static DeedSession Evening(string id, DateTime end)
    {
        var from = end.AddHours(-3);
        var s = new DeedSession { World = World, Id = id, FromUtc = from, ToUtc = end };
        var c = Stretch(DeedStretches.Crafting, end.AddMinutes(-50), 18, ("made|$item_axe_bronze", 2), ("made|$item_arrow_wood", 40));
        c.Moments.Add(new DeedMoment { Kind = DeedMoment.Upgraded, Token = "$item_sword_iron", Utc = end.AddMinutes(-40).AddSeconds(17), From = 2, To = 3, Station = "forge", StationLevel = 3 });
        c.Moments.Add(new DeedMoment { Kind = DeedMoment.Made, Token = "$item_axe_bronze", Utc = end.AddMinutes(-49), Count = 2 });
        c.Moments.Add(new DeedMoment { Kind = DeedMoment.First, Token = "$item_axe_bronze", Utc = end.AddMinutes(-49) });
        var b = Stretch(DeedStretches.Building, end.AddMinutes(-140), 45, ("placed|$piece_woodwall", 40), ("placed|$piece_woodroof", 18), ("recovered|$item_wood", 12));
        var w = Stretch(DeedStretches.Woodcutting, end.AddMinutes(-175), 20, ("treesFelled|Beech1", 3), ("pickedUp|$item_wood", 46), ("chopHits|Beech1", 40));
        s.Stretches.AddRange(new[] { c, b, w });
        return s;
    }

    static string Fresh() { var d = Path.Combine(Path.GetTempPath(), "hw-deedblocks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    static string Describe(DeedSession s) => s == null ? "null" : string.Join("\n", s.Stretches.OrderBy(x => x.FromUtc).Select(x => x.Family + " " + x.FromUtc.ToString("o") + " " + x.ToUtc.ToString("o") + " " +
        string.Join(",", x.Counts.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + Json.F(k.Value))) + " | " +
        string.Join(",", x.Moments.Select(m => m.Kind + ":" + m.Token + "@" + m.Utc.ToString("o") + "x" + m.Count + (m.Kind == DeedMoment.Upgraded ? " " + m.From + ">" + m.To + " " + m.Station + m.StationLevel : "")))));

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var inv = CultureInfo.InvariantCulture;

        // ---------- DB1: one block, the fights and the deeds of one closed session; the round trip ----------
        var fights = SampleSessions.Block(Id(1), "Rowan", Now.AddHours(-2), 4, 5).Session;   // a closed session's fights, as the server stores them
        var deeds = Evening(Id(1), Now.AddHours(-2));
        var both = FeedBlock.Write(fights, deeds);
        var read = FeedBlock.Read(both);
        Check(read != null && read.Foes == 20 && read.Stretches == 3 && Describe(read.Deeds) == Describe(DeedBlockPart.Clean(deeds)) && read.Deeds.Id == Id(1),
              "DB1 a block of a closed session carries its fights and its deed stretches; read back the same stretches, counts, moments (levels, station, times to the second)");
        Check(FeedBlock.Canonical(read) == both && FeedBlock.Head(read).Contains("\"deeds\":3") && FeedBlock.Head(read).Contains("\"foes\":20"),
              "DB1 what the server stores is what it read, written again: the same text; its head names the stretches beside the foes");
        var battleOnly = FeedBlock.Write(fights);
        Check(battleOnly == FeedBlock.Write(fights, null) && !battleOnly.Contains(DeedBlockPart.Key) && !FeedBlock.Head(FeedBlock.Read(battleOnly)).Contains("deeds") &&
              both.IndexOf("\"battleFeed\"", StringComparison.Ordinal) < both.IndexOf("\"" + DeedBlockPart.Key + "\"", StringComparison.Ordinal),
              "DB1 a block of fights alone is written exactly as 0.8.6 wrote it; with deeds, the deed part comes after the fights (a 0.8.6 reader takes the fights and leaves the rest)");
        var other = Evening(Id(9), Now.AddHours(-2));
        Check(FeedBlock.Read(FeedBlock.Write(fights, other)).Stretches == 0, "DB1 another session's stretches never go under this session's id");

        // ---------- DB2: a session of deeds without a fight is a block too ----------
        var deedOnly = FeedBlock.Write(null, Evening(Id(2), Now.AddHours(-5)));
        var dRead = FeedBlock.Read(deedOnly);
        var dir = Fresh(); var players = Path.Combine(dir, "players");
        var store = new BlockStore(players, World);
        Check(dRead != null && dRead.Foes == 0 && dRead.Stretches == 3 && !deedOnly.Contains("battleFeed") && store.Put("1001", dRead, Now, null) == null && store.Rows("1001", Now).Single().Stretches == 3 &&
              FeedBlock.Write(null, new DeedSession { Id = Id(3), FromUtc = Now, ToUtc = Now }) == null && FeedBlock.Write(null, null) == null,
              "DB2 a session of deeds alone: a block with no fights, kept by the server with its stretches in the row; a session with neither is no block");
        var putBack = new BlockStore(players, World);
        Check(putBack.Rows("1001", Now).Single().Stretches == 3 && putBack.Serve("2002", Id(2), Now) == null,
              "DB2 a restart reads the stretches back from the file's head; a block is served only while its owner's copy is (no share file here: gone)");

        // ---------- DB3: what the server keeps of a hostile upload ----------
        var hostile = Evening(Id(4), Now.AddHours(-1));
        hostile.World = "Secret-World-42";
        hostile.Stretches.Add(Stretch("treasure", Now.AddHours(-1.5), 5, ("made|$item_gold", 3)));
        hostile.Stretches[0].Counts["made|" + new string('x', 300)] = 1; hostile.Stretches[0].Counts["teleported|$item_wood"] = 5;
        hostile.Stretches[0].Moments.Add(new DeedMoment { Kind = "teleport", Token = "$item_wood", Utc = Now.AddHours(-1) });
        hostile.Stretches[0].Moments.Add(new DeedMoment { Kind = DeedMoment.Made, Token = new string('y', 300), Utc = Now.AddHours(-1) });
        hostile.Stretches[0].MomentsLeft = int.MaxValue;
        for (int i = 0; i < 150; i++) hostile.Stretches.Add(Stretch(DeedStretches.Mining, Now.AddDays(-1).AddMinutes(i * 3), 2, ("pickedUp|$item_stone", i + 1)));
        hostile.FromUtc = Now.AddDays(-2);   // every time is written from here
        var hj = new Json().Open().Num("v", FeedBlock.Version).Str("id", Id(4)).Str("from", hostile.FromUtc.ToString("o", inv)).Str("to", hostile.ToUtc.ToString("o", inv));
        hj.Key(DeedBlockPart.Key); hostile.WriteTo(hj);   // as a hostile PC would send it: every stretch the session holds, its world, its odd names
        var raw = hj.Close().ToString().Replace("\"s\":[", "\"pos\":[1,2,3],\"s\":[");
        var kept = FeedBlock.Canonical(FeedBlock.Read(raw));
        var keptDeeds = FeedBlock.Read(kept).Deeds;
        Check(!kept.Contains("Secret-World") && !kept.Contains("treasure") && !kept.Contains("teleport") && !kept.Contains(new string('x', 200)) && !kept.Contains(new string('y', 200)) && !kept.Contains("\"pos\"") &&
              keptDeeds.Stretches.Count == DeedBlockPart.MaxStretches && keptDeeds.Stretches.Max(s => s.ToUtc) == hostile.Stretches.Where(s => s.Family != "treasure").Max(s => s.ToUtc) && keptDeeds.Stretches.All(s => s.MomentsLeft <= 100000),
              "DB3 the server keeps only what the feed draws: no world name, no unknown family, deed kind or moment kind, no name over " + DeedBlockPart.MaxName + " characters, no other key; the newest " + DeedBlockPart.MaxStretches + " stretches");

        // ---------- DB4: bounds per owner and world ----------
        var bdir = Fresh(); var bstore = new BlockStore(Path.Combine(bdir, "players"), World);
        for (int i = 0; i < 30; i++)
        {
            var big = new DeedSession { World = World, Id = Id(100 + i), FromUtc = Now.AddHours(-30 - i * 10), ToUtc = Now.AddHours(-26 - i * 10) };
            for (int k = 0; k < 100; k++) big.Stretches.Add(Stretch(DeedStretches.Building, big.FromUtc.AddMinutes(k * 2), 1, ("placed|$piece_woodwall", 1)));
            bstore.Put("1001", FeedBlock.Read(FeedBlock.Write(null, big)), Now, null);
        }
        var rows = bstore.Rows("1001", Now); var again = new BlockStore(Path.Combine(bdir, "players"), World).Rows("1001", Now);
        Check(rows.Sum(r => r.Stretches) <= BlockStore.MaxStretches && rows.Count == BlockStore.MaxStretches / 100 && rows[0].Id == Id(100) && again.Select(r => r.Id).SequenceEqual(rows.Select(r => r.Id)) &&
              Directory.GetFiles(Path.Combine(bdir, "players", "1001", "feed", World)).Length == rows.Count,
              "DB4 bounded on the server: per player and world at most " + BlockStore.MaxStretches + " deed stretches (the newest sessions while they fit, the rest deleted), beside 60 blocks and 2 000 foes; a restart lists the same");

        // ---------- DB5: the two indexes, the deeds tag, an older reader ----------
        var mixed = new List<BlockRow> { read.Row, dRead.Row, FeedBlock.Read(battleOnly).Row };
        var fightsIx = BlockIndex.Part(mixed, false); var deedsIx = BlockIndex.Part(mixed, true);
        Check(fightsIx.Count == 2 && fightsIx.All(r => r.Foes > 0) && deedsIx.Count == 2 && deedsIx.All(r => r.Foes == r.Stretches && r.Stretches > 0),
              "DB5 the fights index lists only sessions with fights (a 0.8.6 reader never asks for a block of deeds alone); the deeds index the sessions with stretches, their count in the foes' place");
        var names = new List<string> { "Edda" }; var keys = new List<string> { "Steam_1#77" };
        var blocksJson = BlockIndex.Write(new[] { Id(1) }, new[] { new KeyValuePair<string, List<BlockRow>>("Steam_1#77", fightsIx) });
        var deedsJson = BlockIndex.Write(new[] { Id(1) }, new[] { new KeyValuePair<string, List<BlockRow>>("Steam_1#77", deedsIx) });
        var wire = GroupShare.WriteList(names, "{\"self\":{}}", keys, live: true, blocksJson: blocksJson, deedsJson: deedsJson).GetArray();
        GroupShare.ReadList(new ZPackage(wire), out _, out var k2, out var live2, out var b2, out var d2);
        var old = OldReader(new ZPackage(wire));
        var dRows = BlockIndex.DeedRows(BlockIndex.Read(d2).Of);
        Check(live2 && b2 == blocksJson && d2 == deedsJson && k2.SequenceEqual(keys) && old.blocks == blocksJson && old.stoppedAt == GroupShare.DeedsTag &&
              dRows["Steam_1#77"].Select(r => r.Id + ":" + r.Stretches).SequenceEqual(deedsIx.Select(r => r.Id + ":" + r.Stretches)) && dRows["Steam_1#77"].All(r => r.Foes == 0),
              "DB5 the deeds index rides after the blocks tag (" + GroupShare.DeedsTag + "); a 0.8.6 reader reads its blocks index and stops there; this one reads both, the stretches back in place");
        var noDeeds = GroupShare.WriteList(names, null, keys, live: true, blocksJson: blocksJson).GetArray();
        GroupShare.ReadList(new ZPackage(noDeeds), out _, out _, out _, out var b3, out var d3);
        Check(b3 == blocksJson && d3 == null && GroupShare.WriteList(names, null, keys, live: true, blocksJson: null, deedsJson: deedsJson).GetArray().Length == GroupShare.WriteList(names, null, keys, live: true).GetArray().Length,
              "DB5 a server before 0.9 sends no deeds index (null: no deed feeds there, none are sent to it); no deeds index without the blocks index");

        // ---------- DB6: the upload plan joins your kept fights and your kept deeds ----------
        var fA = SampleSessions.Block(Id(20), "Rowan", Now.AddHours(-3), 2, 3).Session;   // fights and deeds
        var fB = SampleSessions.Block(Id(21), "Rowan", Now.AddHours(-6), 2, 3).Session;   // fights only
        var dA = Evening(Id(20), Now.AddHours(-3)); var dC = Evening(Id(22), Now.AddHours(-9)); var dOld = Evening(Id(23), Now.AddDays(-31)); var dHeld = Evening(Id(24), Now.AddHours(-12));
        var dLive = Evening(Id(25), Now); dLive.Live = true;
        var plan = Uploads.Plan(new[] { fA, fB }, new[] { dA, dC, dOld, dHeld, dLive }, new HashSet<string> { Id(24) }, new HashSet<string>(), Now);
        Check(plan.Select(p => p.Id).SequenceEqual(new[] { Id(20), Id(21), Id(22) }) && plan[0].HasFights && plan[0].HasDeeds && plan[1].HasFights && !plan[1].HasDeeds && !plan[2].HasFights && plan[2].HasDeeds &&
              Uploads.Plan(new[] { fA, fB }, null, null, null, Now).Select(p => p.Id).SequenceEqual(Uploads.Plan(new[] { fA, fB }, null, null, Now).Select(p => p.Id)),
              "DB6 each closed session once, newest first, its fights and deeds together; a session of deeds alone too; never the running one, one held already or older than 30 days; without deeds (a server before 0.9) as 0.8.6");
        var packed = Uploads.Pack(plan[0], out var problem); var unpacked = FeedBlock.Read(Transport.Unpack(packed));
        Check(problem == null && unpacked.Foes > 0 && unpacked.Stretches == 3, "DB6 a planned session packs into one block with its fights and its stretches");

        // ---------- DB7: a block too large sheds its oldest stretches, never its fights; a busy evening's size ----------
        var huge = new DeedSession { World = World, Id = Id(30), FromUtc = Now.AddHours(-8), ToUtc = Now.AddHours(-1) };
        var rnd = new Random(30);
        for (int i = 0; i < 200; i++)
        {
            var st = Stretch(DeedStretches.Building, huge.FromUtc.AddMinutes(i * 2), 1);
            for (int k = 0; k < 40; k++) st.Counts["placed|$piece_mod_" + rnd.Next(100000).ToString("x5", inv) + "_" + k] = rnd.Next(1, 50);
            for (int k = 0; k < 60; k++) st.Moments.Add(new DeedMoment { Kind = DeedMoment.First, Token = "$piece_mod_" + rnd.Next(100000).ToString("x5", inv), Utc = st.FromUtc.AddSeconds(k) });
            huge.Stretches.Add(st);
        }
        var hugeSource = new Uploads.Source { Id = Id(30), FromUtc = huge.FromUtc, ToUtc = huge.ToUtc, Fights = SampleSessions.Block(Id(30), "Rowan", Now.AddHours(-1), 4, 5).Session, Deeds = huge };
        var hugePacked = Uploads.Pack(hugeSource, out var hugeProblem); var hugeBack = hugePacked == null ? null : FeedBlock.Read(Transport.Unpack(hugePacked));
        Check(hugePacked != null && hugePacked.Length <= BlockStore.MaxBlockPacked && hugeBack.Foes == 20 && hugeBack.Stretches > 0 && hugeBack.Stretches < 200 &&
              hugeBack.Deeds.Stretches.Max(s => s.ToUtc) == huge.Stretches.Max(s => s.ToUtc),
              "DB7 a session too large for one block (200 stretches of 40 kinds and 60 moments) keeps its fights and its newest stretches that fit (" + (hugeBack?.Stretches ?? 0) + "), under " + BlockStore.MaxBlockPacked / 1024 + " KB packed");
        var busy = Evening(Id(31), Now);   // a realistic evening: the record track's DS10 counts about 13 stretches
        for (int i = 0; i < 10; i++) busy.Stretches.Add(Stretch(i % 2 == 0 ? DeedStretches.Mining : DeedStretches.Cooking, Now.AddHours(-6).AddMinutes(i * 15), 10, ("pickedUp|$item_copperore", 20 + i), ("made|$item_stew" + i, 3), ("pickaxeHits|rock" + i, 40)));
        var busyBlock = FeedBlock.Write(SampleSessions.Block(Id(31), "Rowan", Now, 30, 5).Session, busy); var busyPacked = Transport.Pack(busyBlock);
        System.Console.WriteLine("     a busy evening's block (150 foes in 30 fights, " + busy.Stretches.Count + " deed stretches): " + (busyBlock.Length / 1024.0).ToString("0.0", inv) + " KB raw, " + (busyPacked.Length / 1024.0).ToString("0.0", inv) + " KB packed");
        Check(busyPacked.Length < 12 * 1024, "DB7 a busy evening's block with its deeds stays under 12 KB packed (0.8.6's gate for fights alone)");

        // ---------- DB8: the friend's PC: rows, a block of deeds alone fetched, its cache ----------
        var fb = new FellowBlocks(); string asked = null; fb.Ask = id => asked = id;
        var deedRowsByFellow = BlockIndex.DeedRows(new Dictionary<string, List<BlockRow>> { ["Steam_1#77"] = BlockIndex.Part(new[] { dRead.Row, read.Row }, true) });
        fb.TakeRows(new Dictionary<string, List<BlockRow>> { ["Steam_1#77"] = BlockIndex.Part(new[] { dRead.Row, read.Row }, false) }, deedRowsByFellow);
        Check(fb.RowsOf("Steam_1#77").Select(r => r.Id).SequenceEqual(new[] { Id(1) }) && fb.DeedRowsOf("Steam_1#77").Select(r => r.Id).SequenceEqual(new[] { Id(1), Id(2) }) && fb.DeedRowsOf("Steam_1#77").All(r => r.Stretches == 3),
              "DB8 your PC keeps the friend's fights rows and deeds rows apart, newest first; a session in both is one block");
        var st1 = fb.Want(Id(2), 10); var arrived = fb.Arrived(Id(2), FeedBlock.Canonical(dRead));
        Check(st1 == FellowBlocks.State.Loading && asked == Id(2) && arrived != null && fb.StateOf(Id(2)) == FellowBlocks.State.Ready && fb.DeedsOf(Id(2))?.Stretches.Count == 3 && fb.DeedsOf(Id(1)) == null,
              "DB8 a friend's session of deeds alone is asked for, arrives and reads as their stretches (one fetch, one state per session)");
        var cdir = Fresh(); BlockCache.Save(cdir, "Steam_1#77", dRead); BlockCache.Save(cdir, "Steam_1#77", read); BlockCache.Save(cdir, "Steam_9#1", read);
        BlockCache.KeepOnly(cdir, BlockIndex.Joined(new Dictionary<string, List<BlockRow>> { ["Steam_1#77"] = new List<BlockRow> { read.Row } }, deedRowsByFellow));
        Check(BlockCache.Load(cdir, "Steam_1#77", Id(2)) != null && BlockCache.Load(cdir, "Steam_1#77", Id(1)) != null && BlockCache.Load(cdir, "Steam_9#1", Id(1)) == null &&
              FeedBlock.Read(BlockCache.Load(cdir, "Steam_1#77", Id(2))).Stretches == 3,
              "DB8 the cache on your PC keeps what either index names, with its stretches; a fellow neither names leaves it");
        fb.TakeRows(null, null);
        Check(fb.DeedRowsOf("Steam_1#77").Count == 0 && fb.DeedsOf(Id(2)) == null, "DB8 a list without them (sharing off, the friend stopped): their rows and blocks leave memory");

        foreach (var d in new[] { dir, bdir, cdir }) try { Directory.Delete(d, true); } catch { }
        return fails;
    }

    /// <summary>The list as Hearthwoven 0.8.6 reads it: the names, then the tags it knows (book, keys, live, blocks); an unknown tag ends the reading.</summary>
    static (string blocks, string stoppedAt) OldReader(ZPackage pkg)
    {
        int n = pkg.ReadInt(); for (int i = 0; i < n; i++) pkg.ReadString();
        string blocks = null;
        while (pkg.GetPos() < pkg.Size())
        {
            var tag = pkg.ReadString();
            if (tag == GroupShare.BooksTag) Transport.Unpack(pkg.ReadByteArray());
            else if (tag == GroupShare.KeysTag) { int k = pkg.ReadInt(); for (int i = 0; i < k; i++) pkg.ReadString(); }
            else if (tag == GroupShare.LiveTag) pkg.ReadInt();
            else if (tag == GroupShare.BlocksTag) blocks = Transport.Unpack(pkg.ReadByteArray());
            else return (blocks, tag);
        }
        return (blocks, null);
    }
}
