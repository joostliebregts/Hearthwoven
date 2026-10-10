// 0.8 resilience tests (work/hearthwoven-0.8, RESIL track): who a file belongs to (a renamed character, a world made again under
// the same name, two accounts with one character id), the .bak as the last good copy of every file the mod reads back (with one log
// line naming it), and a newer version's keys kept through a save by this one. Fictional characters and ids; every file lives in a
// fresh temp folder.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class ResilienceTests
{
    const long Id = 3141592653L;
    static readonly DateTime T0 = new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static string NewDir() { var d = Path.Combine(Path.GetTempPath(), "hw-res08-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static float Chops(SessionEvents e) => e.ChopHits.TryGetValue("Beech1", out var v) ? v : 0;
    static SessionEvents Ev(float chops) { var e = new SessionEvents(); SessionEvents.Add(e.ChopHits, "Beech1", chops); return e; }
    static IDictionary<string, IDictionary<string, float>> Counters(float wood) =>
        new Dictionary<string, IDictionary<string, float>> { ["pickedUp"] = new Dictionary<string, float> { ["Wood"] = wood } };
    static readonly List<IDictionary<string, IDictionary<string, float>>> NoCounters = new List<IDictionary<string, IDictionary<string, float>>>();

    // one character's file as this version saves it: a wood baseline and some axe hits
    static string Character(string dir, string name, float woodBaseline, float chops)
    {
        var t = LocalTotals.LoadFor(dir, Id, name, Counters(woodBaseline), () => NoCounters, out var path, out _);   // no names: never adopts (setup only)
        t.TakeBaseline("pickedUp", new Dictionary<string, float> { ["Wood"] = woodBaseline }, null, T0);
        t.Record("S-" + name, null, Ev(chops)); t.Save(path, T0);
        return path;
    }

    public static int Run()
    {
        fails = 0;
        var dirs = new List<string>();
        try
        {
            Renamed(dirs); Worlds(dirs); Accounts(dirs); Backups(dirs); NewerKeys(dirs);
        }
        catch (Exception e) { Check(false, "resilience tests threw: " + e); }
        foreach (var d in dirs) try { Directory.Delete(d, true); } catch { }
        return fails;
    }

    // ---------- item 1: two characters, a renamed character ----------
    static void Renamed(List<string> dirs)
    {
        var dir = NewDir(); dirs.Add(dir);
        var rowanPath = Character(dir, "Rowan", 50, 12);
        var rowanMarks = FellowMarks.PathFor(rowanPath); File.WriteAllText(rowanMarks, "{\"version\":1,\"fellows\":{}}");

        // a copied character file (same id) while Rowan is still on this PC: its own file from zero, Rowan's left exactly as it was
        var before = File.ReadAllText(rowanPath);
        var finch = LocalTotals.LoadFor(dir, Id, "Finch", Counters(80), () => NoCounters, () => new[] { "Rowan" }, out var finchPath, out var finchProblem);
        Check(Chops(finch.EventsBefore("x")) == 0 && finchProblem == null && File.ReadAllText(rowanPath) == before && finchPath != rowanPath,
              "RES1 two characters with one id (a copied character file, the original still here): each its own file, counts never mix");

        // Rowan renamed with a save editor (same id, the name Rowan gone from this PC): the renamed character keeps its counts
        var rowena = LocalTotals.LoadFor(dir, Id, "Rowena", Counters(80), () => NoCounters, () => new string[0], out var rowenaPath, out var renameNote);
        var reload = LocalTotals.LoadFor(dir, Id, "Rowena", Counters(80), () => NoCounters, () => new string[0], out _, out var reloadNote);
        Check(Chops(rowena.EventsBefore("x")) == 12 && rowena.Name == "Rowena" && File.Exists(rowenaPath) && File.ReadAllText(rowenaPath).Contains("\"name\":\"Rowena\"") &&
              !File.Exists(rowanPath) && File.ReadAllText(rowanPath + ".renamed-Rowena") == before && File.Exists(FellowMarks.PathFor(rowenaPath)) && !File.Exists(rowanMarks) &&
              renameNote != null && renameNote.Contains("Rowan") && reloadNote == null && Chops(reload.EventsBefore("x")) == 12,
              "RES1 a renamed character (same id, the old name gone) adopts its earlier file once: counts kept, the old file kept as .renamed-, since last time moves along");

        // two earlier files that both fit: neither is adopted, nothing merged, both left as they are
        var dir2 = NewDir(); dirs.Add(dir2);
        var tor = Character(dir2, "Tor", 10, 3); var ylva = Character(dir2, "Ylva", 20, 4);
        var edda = LocalTotals.LoadFor(dir2, Id, "Edda", Counters(90), () => NoCounters, () => new string[0], out _, out var eddaProblem);
        // and a file whose baseline is above this character's counter now cannot be its own
        var dir3 = NewDir(); dirs.Add(dir3);
        Character(dir3, "Tor", 500, 3);
        var low = LocalTotals.LoadFor(dir3, Id, "Edda", Counters(90), () => NoCounters, () => new string[0], out _, out var lowProblem);
        Check(Chops(edda.EventsBefore("x")) == 0 && eddaProblem != null && eddaProblem.Contains("never merged") && File.Exists(tor) && File.Exists(ylva) &&
              Chops(low.EventsBefore("x")) == 0 && lowProblem == null,
              "RES1 two earlier files that fit, or one whose baseline does not fit this character's counters: nothing adopted, never merged");
    }

    // ---------- item 1: a world made again under the same name, a renamed world ----------
    static void Worlds(List<string> dirs)
    {
        var root = NewDir(); dirs.Add(root);
        var old = new ServerBook { BornFrom = T0 };
        old.Birth("Boar_piggy", new List<string> { "111" });
        var oldJson = old.ToJson();
        var legacy = Path.Combine(root, "server-book-Midgard.json");
        File.WriteAllText(legacy, oldJson); File.WriteAllText(legacy + ".bak", oldJson);
        var first = ServerBook.BookPath(root, "Midgard", "777", out var adopted);
        var again = ServerBook.BookPath(root, "Midgard", "888", out var notAdopted);   // the world made again: a new id
        var renamed = ServerBook.BookPath(root, "Asgard", "777", out _);               // the first world renamed: the same id
        Check(Path.GetFileName(first) == "server-book-Midgard.777.json" && File.ReadAllText(first) == oldJson && File.Exists(first + ".bak") &&
              !File.Exists(legacy) && File.ReadAllText(legacy + ".migrated-777") == oldJson && adopted != null &&
              Path.GetFileName(again) == "server-book-Midgard.888.json" && !File.Exists(again) && notAdopted == null && renamed == first &&
              ServerBook.BookPath(root, "My World", "1", out _) != ServerBook.BookPath(root, "MyWorld", "2", out _),
              "RES1 the server book is per world id: the book from before 0.8 is adopted once (old file kept), a world made again under the same name starts its own, a renamed world keeps its book");

        // a rebuild from the chest logs: lines from before 0.8 belong to the world that ran when the server got 0.8, never to a new world of that name
        string M(string t, string what, string uid) => "{\"t\":\"" + t + "\",\"marker\":\"" + what + "\",\"world\":\"Midgard\"" + (uid == null ? "" : ",\"worldUid\":\"" + uid + "\"") + "}";
        string L(string t, string action, string who, int n, float x) => "{\"t\":\"" + t + "\",\"player\":\"P\",\"playerId\":" + who + ",\"playerKey\":\"" + who +
            "\",\"via\":\"sender\",\"container\":\"Karve\",\"kind\":\"ship\",\"containerId\":\"9:1\",\"containerBuilder\":1,\"x\":" + x + ",\"z\":0,\"item\":\"IronScrap\",\"maker\":\"\",\"quality\":1,\"action\":\"" + action + "\",\"count\":" + n + "}";
        var lines = new[]
        {
            M("2026-10-08T10:00:00Z", "server-start", null), L("2026-10-08T10:10:00Z", "put", "111", 10, 0), L("2026-10-08T10:20:00Z", "take", "222", 10, 1000), M("2026-10-08T10:30:00Z", "world-saved", null),
            M("2026-10-10T10:00:00Z", "server-start", "777"), L("2026-10-10T10:10:00Z", "put", "111", 5, 0), L("2026-10-10T10:20:00Z", "take", "222", 5, 2000), M("2026-10-10T10:30:00Z", "world-saved", "777"),
            M("2026-10-11T10:00:00Z", "server-start", "888"), L("2026-10-11T10:10:00Z", "put", "333", 7, 0), L("2026-10-11T10:20:00Z", "take", "444", 7, 500), M("2026-10-11T10:30:00Z", "world-saved", "888"),
        };
        double Deliv(ServerBook b, string who) => b.Of(who) != null && b.Of(who).Delivered.TryGetValue("IronScrap", out var v) ? v : 0;
        var bNew = new ServerBook(); var rNew = bNew.Replay(lines, "Midgard", "888");
        var bOld = new ServerBook(); var rOld = bOld.Replay(lines, "Midgard", "777");
        var bName = new ServerBook(); bName.Replay(lines, "Midgard");
        Check(Deliv(bNew, "444") == 3500 && Deliv(bNew, "222") == 0 && rNew.OtherWorld == 4 && Deliv(bOld, "222") == 20000 && Deliv(bOld, "444") == 0 && rOld.Applied == 4 &&
              Deliv(bName, "222") == 20000 && Deliv(bName, "444") == 3500,
              "RES1 a book rebuilt from the chest logs holds only its own world id's cargo; lines from before 0.8 go to the world that ran then; without an id, by name as before");
    }

    // ---------- item 2: crossplay, two accounts with one character id ----------
    static void Accounts(List<string> dirs)
    {
        const string steam = "76561198000000123", xbox = "Xbox_2535400000000123";
        Check(PeerIdentity.KeyFor(Id, steam, "Steam_" + steam) == Id.ToString() && PeerIdentity.KeyFor(Id, "Steam_" + steam, steam) == Id.ToString() &&
              PeerIdentity.KeyFor(Id, xbox, "") == Id.ToString() && PeerIdentity.KeyFor(Id, "", xbox) == Id.ToString(),
              "RES2 crossplay: one Steam account on a Steam server (a bare number) and on a crossplay server (Steam_...) is one player; an unknown owner or sender: the id, as before");
        var players = NewDir(); dirs.Add(players);
        var envelope = new Json().Open().Str("received", "x").Str("reason", "interval").Str("peerName", "Rowan").Num("peerPlayerId", 0).Str("playerKey", Id.ToString()).Str("platform", steam).Raw("profile", "{\"name\":\"Rowan\"}").Close().ToString();
        File.WriteAllText(Path.Combine(players, Id + ".json"), envelope);
        var hostOnly = NewDir(); dirs.Add(hostOnly);
        File.WriteAllText(Path.Combine(hostOnly, Id + ".share.json"), FellowIds.WithPlatform("{\"playerId\":" + Id + ",\"name\":\"Rowan\"}", xbox));
        var second = PeerIdentity.KeyFor(Id, xbox, PeerIdentity.OwnerOf(players, Id));
        Check(PeerIdentity.OwnerOf(players, Id) == steam && second == Id + "~" + xbox && PeerIdentity.OwnerOf(hostOnly, Id) == xbox &&
              PeerIdentity.KeyFor(Id, steam, PeerIdentity.OwnerOf(players, Id)) == Id.ToString(),
              "RES2 a second account bringing the same character id (a character file given to a friend) is kept apart on the server (" + second + "); the first keeps its files");
    }

    // ---------- items 3 and 5: one backup, the last good copy, loaded with one log line ----------
    static void Backups(List<string> dirs)
    {
        var dir = NewDir(); dirs.Add(dir);
        // a blank main file (power loss) is never turned into the .bak: the good copy stays
        var plain = Path.Combine(dir, "plain.json");
        AtomicFile.Write(plain, "{\"v\":1}"); AtomicFile.Write(plain, "{\"v\":2}");
        File.WriteAllBytes(plain, new byte[64]);
        AtomicFile.Write(plain, "{\"v\":3}");
        Check(File.ReadAllText(plain) == "{\"v\":3}" && File.ReadAllText(plain + ".bak") == "{\"v\":1}", "RES3 a zero-filled main file is replaced without becoming the .bak: the .bak stays the last good copy");

        // every file the client reads back: two saves, then the main file cut in half (a crash) -> the save before loads, a line says so
        var notes = new List<string>();
        // fellow marks
        var marksPath = Path.Combine(dir, "local", Id + "-Rowan.fellows.json"); Directory.CreateDirectory(Path.GetDirectoryName(marksPath));
        FellowMarks MarksWith(float placed) { var f = new FellowMarks(); var m = new FellowMarks.Mark { Name = "Edda", Session = "s", SeenUtc = T0 }; m.Values["placed|$piece_bed"] = placed; f.Latest["Steam_1#7"] = m; return f; }
        MarksWith(3).Save(marksPath); MarksWith(5).Save(marksPath);
        Cut(marksPath);
        var marks = FellowMarks.Load(marksPath, out var marksNote); notes.Add(marksNote);
        var marksBefore = marks.BeforeOf("Steam_1#7")?.Values["placed|$piece_bed"];
        marks.Latest["Steam_1#7"].Values["placed|$piece_bed"] = 6; marks.Save(marksPath);   // the next save: the .bak must still be the good copy
        // panel filter choices
        var prefsPath = PanelPrefs.PathFor(dir, Id, "Rowan");
        var s1 = new PanelState(); s1.OpenFilters.Add("craft"); var saved = PanelPrefs.Save(prefsPath, s1, null);
        var s2 = new PanelState(); s2.OpenFilters.Add("build"); PanelPrefs.Save(prefsPath, s2, saved);
        Cut(prefsPath);
        var back = new PanelState(); string prefsNote = null; PanelPrefs.Load(prefsPath, back, null, m => prefsNote = m); notes.Add(prefsNote);
        // the server book (a host's PC or the server)
        var bookPath = Path.Combine(dir, "server-book-Midgard.777.json");
        var b1 = new ServerBook(); b1.Birth("Boar_piggy", new List<string> { "111" }); AtomicFile.Write(bookPath, b1.ToJson());
        var b2 = new ServerBook(); b2.Birth("Boar_piggy", new List<string> { "111" }); b2.Birth("Boar_piggy", new List<string> { "111" }); AtomicFile.Write(bookPath, b2.ToJson());
        Cut(bookPath);
        var book = ServerBook.Load(bookPath, out var bookNote); notes.Add(bookNote);
        Check(marksBefore == 3 && FellowMarks.Load(marksPath + ".bak").BeforeOf("Steam_1#7")?.Values["placed|$piece_bed"] == 3 &&
              FellowMarks.Load(marksPath, out var marksClean).BeforeOf("Steam_1#7")?.Values["placed|$piece_bed"] == 6 && marksClean == null &&
              back.OpenFilters.SetEquals(new[] { "craft" }) && book != null && book.Of("111")?.Born.Values.Sum() == 1 &&
              notes.All(n => n != null && n.Contains("backup")) && Directory.GetFiles(dir, "*.unreadable", SearchOption.AllDirectories).Length == 2,
              "RES3/RES5 fellow marks, panel choices, server book: a half-written main file loads the save before (nothing lost beyond the last save), one line names the backup, the broken file is set aside and the .bak stays good");

        // a broken file without a backup never throws: the loaders come back empty and the plugin goes on
        var junk = NewDir(); dirs.Add(junk);
        var files = new[] { "a.fellows.json", "a.panel.json", "server-book-x.1.json", "a.json" }.Select(f => Path.Combine(junk, "local", f)).ToArray();
        Directory.CreateDirectory(Path.Combine(junk, "local"));
        foreach (var f in files) File.WriteAllText(f, "{\"version\":1,\"fell\u0000\u0000");
        bool threw = false;
        try
        {
            FellowMarks.Load(files[0], out _); PanelPrefs.Load(files[1], new PanelState(), null, null); ServerBook.Load(files[2], out _); LocalTotals.Load(files[3], Id, out _);
        }
        catch { threw = true; }
        Check(!threw, "RES5 every local file half written and without a backup: each loader comes back empty, none throws");
    }

    static void Cut(string path) { var t = File.ReadAllText(path); File.WriteAllText(path, t.Substring(0, t.Length / 2)); }

    // ---------- item 4: a newer version's keys survive a save by this one ----------
    static void NewerKeys(List<string> dirs)
    {
        var dir = NewDir(); dirs.Add(dir);
        // local totals: a new top-level key, and a new family inside the earlier sessions, the last session and a day row
        var t = new LocalTotals { PlayerId = Id, Name = "Rowan", FirstRunUtc = T0 };
        t.Record("A", null, Ev(2), null, null, T0.ToLocalTime()); t.Record("B", null, Ev(3), null, null, T0.ToLocalTime());
        var fam = ",\"lanternsLit\":{\"$item_lantern\":4}";
        var newer = t.ToJson(T0).Replace("\"measured\":{\"blocks\":0,\"parries\":0", "\"measured\":{\"blocks\":0,\"parries\":0" + fam)
                                .Replace("\"ev\":{", "\"ev\":{\"lanternsLit\":{\"$item_lantern\":1},");
        newer = newer.Substring(0, newer.Length - 1) + ",\"futureBook\":{\"x\":[1,2]}}";
        var path = Path.Combine(dir, "t.json"); File.WriteAllText(path, newer);
        var loaded = LocalTotals.Load(path, Id, out _);
        loaded.Record("C", null, Ev(1), null, null, T0.ToLocalTime()); loaded.Save(path, T0);   // a new session folds B (with its lanterns) into the earlier ones
        var after = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
        var prevLanterns = MiniJson.Num(MiniJson.Obj(MiniJson.Obj(MiniJson.Obj(MiniJson.Obj(after, "totals"), "previous"), "measured"), "lanternsLit"), "$item_lantern");
        var rowLanterns = File.ReadAllText(path).Contains("\"lanternsLit\":{\"$item_lantern\":1}");   // only the day row holds 1
        // fellow marks, panel choices, the server book: a newer key at the top and (marks) inside a fellow
        var marksPath = Path.Combine(dir, "m.fellows.json");
        File.WriteAllText(marksPath, "{\"version\":1,\"fellows\":{\"Steam_1#7\":{\"name\":\"Edda\",\"session\":\"s\",\"seen\":\"2026-10-10T09:00:00.0000000Z\",\"since\":true,\"story\":true,\"v\":{\"placed|$piece_bed\":3},\"mood\":\"glad\"}},\"futureMarks\":7}");
        var marks = FellowMarks.Load(marksPath, out _); marks.Save(marksPath);
        var prefsPath = Path.Combine(dir, "p.panel.json");
        File.WriteAllText(prefsPath, "{\"version\":1,\"facets\":{},\"open\":[],\"pinned\":[\"battle\"]}");
        var ps = new PanelState(); var saved = PanelPrefs.Load(prefsPath, ps, null, null); ps.OpenFilters.Add("craft"); PanelPrefs.Save(prefsPath, ps, saved);
        var bookPath = Path.Combine(dir, "server-book-x.1.json");
        var bk = new ServerBook(); bk.Birth("Boar_piggy", new List<string> { "111" });
        var bkJson = bk.ToJson(); File.WriteAllText(bookPath, bkJson.Substring(0, bkJson.Length - 1) + ",\"futureLedger\":{\"a\":1}}");
        var bk2 = ServerBook.Load(bookPath, out _); AtomicFile.Write(bookPath, bk2.ToJson());
        var marksText = File.ReadAllText(marksPath); var prefsText = File.ReadAllText(prefsPath); var bookText = File.ReadAllText(bookPath);
        Check(MiniJson.Obj(after, "futureBook") != null && prevLanterns == 8 &&   // 4 in the earlier sessions + 4 in the last one, folded by the new session rowLanterns && Chops(LocalTotals.FromJson(File.ReadAllText(path)).EventsBefore("D")) == 6 &&
              marksText.Contains("\"futureMarks\":7") && marksText.Contains("\"mood\":\"glad\"") &&
              prefsText.Contains("\"pinned\":[\"battle\"]") && prefsText.Contains("\"craft\"") && bookText.Contains("\"futureLedger\":{\"a\":1}"),
              "RES4 a newer version's keys survive a save by this one: local totals (top level, and a new family in earlier sessions and day rows), fellow marks (top and per fellow), panel choices, server book");

        // a newer version's file is never saved over
        File.WriteAllText(marksPath, "{\"version\":9,\"fellows\":{}}"); var m9 = FellowMarks.Load(marksPath, out var m9Note); m9.Latest["k"] = new FellowMarks.Mark(); m9.Save(marksPath);
        File.WriteAllText(prefsPath, "{\"version\":9}"); var p9 = new PanelState(); var s9 = PanelPrefs.Load(prefsPath, p9, null, null); p9.OpenFilters.Add("craft"); PanelPrefs.Save(prefsPath, p9, s9);
        Check(File.ReadAllText(marksPath) == "{\"version\":9,\"fellows\":{}}" && m9Note != null && m9Note.Contains("newer") && File.ReadAllText(prefsPath) == "{\"version\":9}",
              "RES4 fellow marks and panel choices from a newer Hearthwoven are read for nothing and never saved over");

        // a key this version writes itself is never carried as a newer version's: the Everyone chip turned off stays off
        File.Delete(prefsPath);
        var on = new PanelState { Everyone = true }; var savedOn = PanelPrefs.Save(prefsPath, on, null);
        var off = new PanelState(); PanelPrefs.Load(prefsPath, off, null, null); off.Everyone = false; PanelPrefs.Save(prefsPath, off, savedOn);
        var again = new PanelState(); PanelPrefs.Load(prefsPath, again, null, null);
        Check(!again.Everyone && !File.ReadAllText(prefsPath).Contains("everyone"), "RES4 the Everyone chip turned off is saved as off (its key is this version's own, not kept as a newer one's)");
    }
}
