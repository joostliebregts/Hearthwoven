// Fellow identity and hosted worlds (RESILIENCE-06 item 8, E1-E2): fellows keyed by platform id and shown by name, two
// players with one name both shown, old and new peers mixed, and the host as the server. Fictional players and ids.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hearthwoven;

static class FellowTests
{
    static string Copy(string name, long id, bool share = true) =>
        GroupShare.SharedCopy(Snapshot.Build("0.6.0", id, name, new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", new DamageTally(), "s-" + id, null, null, share));

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- the key ----------
        Check(FellowIds.PlatformOf("76561198000000001") == "Steam_76561198000000001" && FellowIds.PlatformOf("Steam_76561198000000001") == "Steam_76561198000000001" &&
              FellowIds.PlatformOf("Xbox_2535400000000001") == "Xbox_2535400000000001" && FellowIds.PlatformOf("") == "" && FellowIds.PlatformOf(null) == "" && FellowIds.PlatformOf("0") == "",
              "ID the platform id as the game gives it; a Steam socket's bare number gets the game's own Steam_ prefix");
        var rowanSteam = FellowIds.WithPlatform(Copy("Rowan", 501), "76561198000000001");
        var rowanXbox = FellowIds.WithPlatform(Copy("Rowan", 502), "Xbox_2535400000000001");
        Check(FellowIds.KeyOf(rowanSteam) == "Steam_76561198000000001#501" && FellowIds.KeyOf(rowanXbox) == "Xbox_2535400000000001#502",
              "ID a 0.6 server's copy is keyed by platform id (Steam or Xbox, crossplay) plus the character's profile id");
        Check(MiniJson.Parse(rowanSteam) is Dictionary<string, object> parsed && MiniJson.Str(parsed, "name") == "Rowan" && rowanSteam.StartsWith("{\"platformId\":") &&
              FellowIds.WithPlatform(rowanSteam, "Xbox_9") == rowanSteam && FellowIds.WithPlatform(Copy("Rowan", 501), "") == Copy("Rowan", 501),
              "ID the platform id goes in front of the copy, which still reads as JSON; added once; without a platform id the copy is unchanged");
        Check(FellowIds.KeyOf(Copy("Edda", 7)) == "id:7" && FellowIds.KeyOf("{\"name\":\"Edda\"}") == "name:Edda" && FellowIds.ByName("name:Edda") && !FellowIds.ByName("id:7") && FellowIds.KeyOf("{}") == "",
              "ID an older server's copy (no platform id) is keyed by profile id; a copy with only a name falls back to the name, marked");
        var twoChars = FellowIds.WithPlatform(Copy("Tor", 601), "76561198000000002"); var alt = FellowIds.WithPlatform(Copy("Torvald", 602), "76561198000000002");
        Check(FellowIds.KeyOf(twoChars) != FellowIds.KeyOf(alt), "ID two characters of one account stay two fellows (the profile id after the platform id)");

        // ---------- same name ----------
        var f = new FellowIds();
        var k1 = f.Receive(rowanSteam); var k2 = f.Receive(rowanXbox); var k3 = f.Receive(FellowIds.WithPlatform(Copy("Edda", 503), "76561198000000003"));
        var labels = f.Labelled("Finch");
        Check(f.Count == 3 && labels.Select(kv => kv.Value).SequenceEqual(new[] { "Rowan", "Rowan (2)", "Edda" }) && labels[0].Key == k1 && labels[1].Key == k2,
              "SAME two players named Rowan both show, the later one as Rowan (2); each label belongs to its key");
        var mine = new FellowIds(); mine.Receive(rowanSteam); mine.Receive(rowanXbox);
        Check(mine.Labelled("Rowan").Select(kv => kv.Value).SequenceEqual(new[] { "Rowan (2)", "Rowan (3)" }) && mine.KeyOfLabel("Rowan (3)", "Rowan") == k2 && mine.KeyOfLabel("Rowan", "Rowan") == null,
              "SAME a fellow with your own name shows beside you, numbered; your name stays yours");
        f.Receive(rowanSteam); f.Receive(rowanXbox);
        Check(f.Count == 3 && f.Labelled("Finch").Select(kv => kv.Value).SequenceEqual(new[] { "Rowan", "Rowan (2)", "Edda" }), "SAME a resend replaces, never adds a fellow, and nobody's label moves");
        f.KeepOnly(new[] { k2, k3 }, new[] { "Rowan", "Edda" });
        var after = f.Labelled("Finch");
        Check(f.Count == 2 && after.First(kv => kv.Key == k2).Value == "Rowan (2)" && f.KeyOfLabel("Rowan (2)", "Finch") == k2,
              "SAME when the first Rowan stops sharing, the second keeps its label (its chip and colour stay with it)");
        f.Receive(rowanSteam);
        Check(f.Labelled("Finch").First(kv => kv.Key == k1).Value == "Rowan", "SAME the first Rowan back gets the free plain name");

        // ---------- old and new peers mixed ----------
        var m = new FellowIds();
        m.Receive(Copy("Edda", 7));                                                       // from a server before the fix
        m.Receive(FellowIds.WithPlatform(Copy("Tor", 8), "Xbox_2535400000000009"));       // from a server with it
        m.Receive("{\"name\":\"Finch\",\"share\":true}");                                 // a copy with only a name
        Check(m.Count == 3 && m.Labelled("Rowan").Select(kv => kv.Value).SequenceEqual(new[] { "Edda", "Tor", "Finch" }) && m.Copies.Keys.Contains("name:Finch"),
              "MIX copies with and without platform ids live side by side; the name-only one is kept under its marked name key");
        m.Receive(FellowIds.WithPlatform(Copy("Edda", 7), "76561198000000007"));
        Check(m.Count == 3 && !m.Copies.ContainsKey("id:7") && m.Copies.ContainsKey("Steam_76561198000000007#7"), "MIX the same Edda arriving with her platform id replaces the older copy (one Edda, not two)");
        Check(m.Receive("{\"name\":\"Edda\"}") == null && m.Count == 3, "MIX a name-only copy never doubles a fellow already known by id");
        m.KeepOnly(null, new[] { "Edda", "Finch" });
        Check(m.Count == 2 && !m.Copies.Values.Any(c => c.Contains("\"Tor\"")) && m.Labelled("Rowan").Select(kv => kv.Value).OrderBy(x => x).SequenceEqual(new[] { "Edda", "Finch" }),
              "MIX a list from an older server (names only): everyone not named is forgotten, as before");

        // ---------- the list on the wire ----------
        var keys = new[] { k1, k2 }; var names = new[] { "Rowan", "Rowan" };
        var wire = GroupShare.WriteList(names, "{}", keys).GetArray();
        var r05 = new ZPackage(wire); int n05 = r05.ReadInt(); var got05 = new List<string>(); for (int i = 0; i < n05; i++) got05.Add(r05.ReadString());
        var r06 = new ZPackage(wire); int n06 = r06.ReadInt(); for (int i = 0; i < n06; i++) r06.ReadString(); var tag06 = r06.ReadString(); r06.ReadByteArray();
        Check(got05.SequenceEqual(names) && tag06 == GroupShare.BooksTag, "WIRE a 0.5 client reads the names, a 0.6 client before this fix reads the book and stops: the keys after it are never in their way");
        GroupShare.ReadList(new ZPackage(wire), out var wb, out var wk);
        Check(wb == "{}" && wk != null && wk.SequenceEqual(keys), "WIRE a client with the fix reads the book and each name's key");
        var oldList = new ZPackage(); oldList.Write(1); oldList.Write("Edda");
        GroupShare.ReadList(new ZPackage(oldList.GetArray()), out var ob, out var ok2);
        Check(ob == null && ok2 == null, "WIRE an older server's list (names only): no keys, so fellows are matched by name");
        GroupShare.ReadList(new ZPackage(GroupShare.WriteList(names, null, keys).GetArray()), out var nb, out var nk);
        Check(nb == null && nk != null && nk.Count == 2, "WIRE keys without a book still read");

        // ---------- the server's book by key ----------
        var book = "{\"self\":{\"bornNear\":{\"Wolf\":1}},\"players\":{\"Rowan\":{\"bornNear\":{\"Boar\":2}}},\"byKey\":{\"" + k1 + "\":{\"bornNear\":{\"Boar\":2}},\"" + k2 + "\":{\"bornNear\":{\"Lox\":3}}}}";
        GroupShare.ReadBooks(book);
        Check(GroupShare.BookOf(k1, "Rowan")?.BornNear["Boar"] == 2 && GroupShare.BookOf(k2, "Rowan")?.BornNear["Lox"] == 3 && GroupShare.BookOf("id:99", "Rowan") == null,
              "BOOK each Rowan gets their own part of the server's book (by key); a fellow with nothing has none");
        GroupShare.ReadBooks("{\"players\":{\"Edda\":{\"bornNear\":{\"Boar\":2}}}}");
        Check(GroupShare.BookOf("id:7", "Edda")?.BornNear["Boar"] == 2, "BOOK from a server without keys the book is found by name, as before");
        GroupShare.ReadBooks(null);

        // ---------- the server's answer carries the keys ----------
        var serve = new GroupServe();
        var srcs = new List<GroupServe.Source> { new GroupServe.Source { Path = "a", Key = "501", Stamp = "1" }, new GroupServe.Source { Path = "b", Key = "502", Stamp = "1" } };
        var texts = new Dictionary<string, string> { ["a"] = rowanSteam, ["b"] = rowanXbox };
        var ans = serve.Plan(1, srcs, 0f, p => texts[p], t => new List<byte[]> { new byte[1] }, t => Transport.Field(t, "name"), t => "m", FellowIds.KeyOf);
        Check(ans.Names.SequenceEqual(new[] { "Rowan", "Rowan" }) && ans.Fellows.SequenceEqual(new[] { k1, k2 }) && ans.Keys.SequenceEqual(new[] { "501", "502" }),
              "SERVE the answer lists each sharer's name, file key and fellow key in line (two Rowans, two keys)");
        var ansOld = new GroupServe().Plan(1, srcs, 0f, p => texts[p], t => new List<byte[]> { new byte[1] }, t => Transport.Field(t, "name"), t => "m");
        Check(ansOld.Fellows.All(x => x == "") && ansOld.Fellows.Count == 2, "SERVE without the fellow function (the old call) nothing changes but empty keys");

        // ---------- the host as the server ----------
        var dir = Path.Combine(Path.GetTempPath(), "hw-fellows-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        try
        {
            var now = DateTime.UtcNow;
            void Share(string key, string json) => File.WriteAllText(Path.Combine(dir, key + ".share.json"), json);
            // the host (profile 111) stores its own copy the way StoreOwn does; two players joined, both named Rowan; one old copy
            Share("111", FellowIds.WithPlatform(Copy("Hostess", 111), "76561198000000111"));
            Share("501", rowanSteam); Share("502", rowanXbox);
            Share("777", FellowIds.WithPlatform(Copy("Gone", 777), "76561198000000777"));
            File.SetLastWriteTimeUtc(Path.Combine(dir, "777.share.json"), now.AddDays(-GroupShare.MaxAgeDays - 1));
            var src = GroupShare.HostSources(dir, "111", now);
            Check(src != null && src.Select(s => s.Key).OrderBy(x => x).SequenceEqual(new[] { "501", "502" }), "HOST the host's group: every sharer's copy except its own and those not seen for two weeks");
            string seenKeys = null;
            GroupShare.HostAnswer(src, (fk, nm, fl) => { seenKeys = string.Join(",", fk) + "|" + string.Join(",", fl); return "{\"self\":{\"bornNear\":{\"Wolf\":4}}}"; });
            Check(GroupShare.Fellows.Count == 2 && GroupShare.Fellows.Labelled("Hostess").Select(kv => kv.Value).SequenceEqual(new[] { "Rowan", "Rowan (2)" }) &&
                  GroupShare.OwnBook?.BornNear["Wolf"] == 4 && seenKeys != null && seenKeys.Contains(k1) && seenKeys.Contains("501"),
                  "HOST the host sees its fellows as on a dedicated server (both Rowans, labelled), and its own part of the book");
            // the players who joined see the host: the server lists the share files for a requester, the host's own among them
            var forJoiner = Directory.GetFiles(dir, "*.share.json").Where(p => !p.EndsWith("501.share.json") && (now - File.GetLastWriteTimeUtc(p)).TotalDays <= GroupShare.MaxAgeDays)
                                     .Select(p => new GroupServe.Source { Path = p, Key = Path.GetFileName(p).Replace(".share.json", ""), Stamp = "1" }).ToList();
            var joinerAnswer = new GroupServe().Plan(9, forJoiner, 0f, File.ReadAllText, t => new List<byte[]> { new byte[1] }, t => Transport.Field(t, "name"), t => "m", FellowIds.KeyOf);
            Check(joinerAnswer.Names.Contains("Hostess") && joinerAnswer.Fellows.Contains("Steam_76561198000000111#111"), "HOST a player who joined the hosted world sees the host among the fellows");
            File.Delete(Path.Combine(dir, "111.share.json"));   // the host switched sharing off (Store removes its copy)
            Check(GroupShare.HostSources(dir, "111", now) == null, "HOST a host who does not share sees nobody (reciprocal, as on a dedicated server)");
            GroupShare.HostAnswer(null, null);
            Check(GroupShare.Fellows.Count == 0 && GroupShare.OwnBook == null, "HOST then the fellows and the book are forgotten");
            Check(GroupShare.HostSources(Path.Combine(dir, "missing"), "111", now) == null, "HOST no players folder yet (nobody joined): nothing, no error");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
        return fails;
    }
}
