// RESILIENCE-06 server items (res-server): atomic server files with a backup (item 3), input hardening (item 5: bounded parts,
// expiry of half messages, rate limit, bounded gunzip, strict JSON), the cargo book rebuilt per world (item 7, I8) and old daily
// logs compressed in place, never deleted (B2). Real fixtures from ~/ValheimReport/hearthwoven (or HW_REAL_CHESTS) when present;
// only sizes and totals are printed, never names. Every file this writes lives in a fresh temp folder that is removed at the end.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Hearthwoven;

static class ServerResilienceTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var real = Environment.GetEnvironmentVariable("HW_REAL_CHESTS") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ValheimReport", "hearthwoven");
        var tmpRoot = Path.Combine(Path.GetTempPath(), "hw-res-server-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tmpRoot);
        try
        {
            // ---------- real snapshots (the profile inside each stored envelope) ----------
            var snaps = new List<string>();
            if (Directory.Exists(Path.Combine(real, "players")))
                foreach (var f in Directory.GetFiles(Path.Combine(real, "players"), "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith(".share.json")))
                    using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(f)))
                        if (doc.RootElement.TryGetProperty("profile", out var prof)) snaps.Add(prof.GetRawText());
            string sample = snaps.OrderByDescending(s => s.Length).FirstOrDefault()
                         ?? "{\"mod\":\"0.6.0\",\"playerId\":1001,\"name\":\"Rowan\",\"session\":\"ab12cd34ef56\",\"share\":true,\"stats\":[{\"index\":0}]}";

            // ---------- item 5: bounded gunzip ----------
            var packed = Transport.Pack(sample);
            Check(ServerIntake.Gunzip(packed, ServerIntake.MaxJsonBytes, out _) == sample && Transport.Unpack(packed) == sample,
                  $"IN a real snapshot ({sample.Length / 1024} KB, {packed.Length / 1024} KB packed) passes the bounded gunzip unchanged");
            // a gzip bomb: 200 MB of one byte packs to about 200 KB, under the 600 KB packed limit
            byte[] bomb;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal)) { var zeros = new byte[1 << 20]; for (int i = 0; i < 200; i++) gz.Write(zeros, 0, zeros.Length); }
                bomb = ms.ToArray();
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            var bombText = ServerIntake.Gunzip(bomb, ServerIntake.MaxJsonBytes, out var bombWhy);
            clock.Stop();
            var grew = GC.GetAllocatedBytesForCurrentThread() - before;
            bool unpackThrew = false; try { Transport.Unpack(bomb); } catch (InvalidDataException) { unpackThrew = true; }
            System.Console.WriteLine($"MEASURED gzip bomb: {bomb.Length / 1024} KB packed (200 MB inside) refused after {clock.ElapsedMilliseconds} ms, {grew / 1024} KB allocated in all ({bombWhy})");
            Check(bombText == null && bomb.Length <= ServerIntake.MaxPackedBytes && grew < 6 * ServerIntake.MaxJsonBytes && unpackThrew,
                  "IN a gzip bomb (200 MB in about 200 KB) stops at the 4 MB limit: never inflated in memory; Transport.Unpack throws, callers catch it");
            Check(ServerIntake.Gunzip(new byte[ServerIntake.MaxPackedBytes + 1], ServerIntake.MaxJsonBytes, out _) == null &&
                  ServerIntake.Gunzip(Encoding.UTF8.GetBytes("{\"not\":\"gzip\"}"), ServerIntake.MaxJsonBytes, out _) == null &&
                  ServerIntake.Gunzip(new byte[0], ServerIntake.MaxJsonBytes, out _) == null,
                  "IN over 600 000 bytes packed, not gzip, or empty: refused before anything is unpacked");
            Check(ServerIntake.Gunzip(Transport.Pack("{\"a\":\"" + new string('x', ServerIntake.MaxJsonBytes) + "\"}"), ServerIntake.MaxJsonBytes, out _) == null &&
                  ServerIntake.Gunzip(Transport.Pack("{\"a\":\"" + new string('x', ServerIntake.MaxJsonBytes - 10) + "\"}"), ServerIntake.MaxJsonBytes, out _) != null,
                  "IN the limit is exact: 4 MB of text passes, a few bytes more is refused");

            // ---------- item 5: strict JSON ----------
            clock.Restart();
            bool allReal = snaps.All(ServerIntake.IsJsonObject);
            clock.Stop();
            Check(allReal && ServerIntake.IsJsonObject(sample), $"IN every real stored snapshot ({snaps.Count}) is one JSON object (checked in {clock.Elapsed.TotalMilliseconds:0.0} ms)");
            var bad = new[] { "", "   ", "[1,2]", "\"text\"", "{", "{\"a\":1", "{\"a\":1}}", "{\"a\":1} trailing", "{\"a\" 1}", "{\"a\":1,}", "{a:1}", "{\"a\":01x}",
                              "{\"a\":\"unterminated}", "{\"a\":\"bad \\q escape\"}", "{\"a\":\"\u0001\"}", "{\"a\":[1,,2]}", "{\"a\":tru}", "{\"a\":1}{\"b\":2}",
                              "{\"a\":" + new string('[', 200) + new string(']', 200) + "}" };
            Check(bad.All(b => !ServerIntake.IsJsonObject(b)), $"IN malformed text is refused, without an exception ({bad.Length} cases: truncated, trailing garbage, missing colon, bare keys, bad escapes, control characters, two objects, nesting over 64)");
            Check(ServerIntake.IsJsonObject(" {\"a\":[1,-2.5e3,true,false,null,NaN,Infinity,-Infinity,\"\\u00f8\\n\"],\"b\":{}} \n"),
                  "IN whitespace around, all value kinds, unicode escapes, and NaN/Infinity (older clients write them; MiniJson and Python read them) pass");
            // the stored envelope stays valid JSON for kstats: Raw() of an accepted snapshot
            var env = new Json().Open().Str("reason", "interval").Raw("profile", sample).Close().ToString();
            Check(ServerIntake.IsJsonObject(env), "IN the envelope around an accepted snapshot (players/<key>.json) is valid JSON for kstats");

            // ---------- item 5: half messages expire, per sender bounded ----------
            double now = 0;
            var asm = new Fragments.Assembler { Clock = () => now };
            var parts = Fragments.Split(Transport.Pack(sample));
            for (int m = 0; m < 1000; m++) asm.Add(42, "orphan" + m, 0, parts.Count + 1, parts[0]);   // never completed
            Check(asm.Pending == Fragments.Assembler.MaxOpenPerSender && asm.Dropped == 1000 - Fragments.Assembler.MaxOpenPerSender,
                  $"IN 1 000 half-sent messages from one sender keep at most {Fragments.Assembler.MaxOpenPerSender} open (the oldest go)");
            asm.Add(43, "other", 0, 2, parts[0]);
            now = 121;
            byte[] whole = null;
            for (int i = 0; i < parts.Count; i++) whole = asm.Add(43, "fresh", i, parts.Count, parts[i]) ?? whole;
            Check(asm.Pending == 0 && asm.Expired == 5 && whole != null && Transport.Unpack(whole) == sample,
                  "IN a message still incomplete 2 minutes after its first part is dropped; a new one from the same sender still completes");
            now = 200;
            asm.Add(44, "slow", 0, 2, parts[0]); now = 330; var late = asm.Add(44, "slow", 1, 2, parts.Count > 1 ? parts[1] : parts[0]);
            Check(late == null && asm.Pending == 1, "IN the last part of a message older than 2 minutes starts it again instead of completing stale data");
            int rej = asm.Rejected;
            asm.Add(45, "big", 0, 1, new byte[Fragments.Size + 1]); asm.Add(45, "many", 0, 201, parts[0]); asm.Add(45, "neg", -1, 2, parts[0]); asm.Add(45, "null", 0, 1, null);
            Check(asm.Rejected == rej + 4 && asm.Pending == 1, "IN a part over 3 000 bytes, over 200 parts, a bad index or no data is refused (and counted for the log)");

            // ---------- item 5: rate limit per sender ----------
            var gate = new ServerIntake.RateGate();
            bool f1, f2, f3;
            var a1 = gate.Allow(7, "interval", 100, out _);
            var a2 = gate.Allow(7, "interval", 110, out f1);
            var a3 = gate.Allow(7, "interval", 115, out f2);
            var a4 = gate.Allow(7, "logout", 116, out _);     // a logout may come sooner, once
            var a5 = gate.Allow(7, "quit", 117, out f3);       // the quit right after it is not
            var a6 = gate.Allow(7, "interval", 136, out _);    // 20 s after the logout
            var a7 = gate.Allow(8, "interval", 110, out _);    // another sender is not affected
            Check(a1 && !a2 && f1 && !a3 && !f2 && a4 && !a5 && !f3 && a6 && a7 && gate.Refused == 3,
                  "IN rate: one snapshot per 20 s per sender, a logout/quit/share change may come early once per 20 s; the refusal is logged once per sender");
            var flood = new ServerIntake.RateGate(); int accepted = 0;
            for (int i = 0; i < 3600; i++) if (flood.Allow(9, i % 2 == 0 ? "quit" : "interval", i, out _)) accepted++;
            Check(accepted <= 3600 / 20 * 2 + 1, $"IN a client sending every second for an hour (half of them 'quit') gets {accepted} snapshots stored, at most two per 20 s");
            gate.Prune(id => id == 8);
            Check(gate.Senders == 1, "IN the gate forgets senders that left (bounded by the connected peers)");
            var once = new ServerIntake.OnceEach();
            Check(once.First(1, "shape") && !once.First(1, "shape") && once.First(1, "parts") && once.First(2, "shape"), "IN each problem is logged once per sender");

            // ---------- item 3: the server book on disk: atomic, with a backup ----------
            var bookPath = Path.Combine(tmpRoot, "server-book-Test.json");
            var b1 = new ServerBook(); b1.Cargo("ship1", "ship", "put", "sender", "111", "IronScrap", 30, 0, 0, new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));
            AtomicFile.Write(bookPath, b1.ToJson());
            var b2 = new ServerBook(); b2.Cargo("ship1", "ship", "put", "sender", "111", "IronScrap", 30, 0, 0, new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc));
            b2.Cargo("ship1", "ship", "take", "sender", "222", "IronScrap", 30, 1000, 0, new DateTime(2026, 10, 8, 20, 20, 0, DateTimeKind.Utc));
            AtomicFile.Write(bookPath, b2.ToJson());
            var loaded = ServerBook.Load(bookPath, out var note1);
            Check(loaded != null && note1 == null && loaded.ToJson() == b2.ToJson() && File.Exists(bookPath + ".bak") && ServerBook.FromJson(File.ReadAllText(bookPath + ".bak")).ToJson() == b1.ToJson() && !File.Exists(bookPath + ".tmp"),
                  "AT the book saves whole (temp, flushed, replaced); the save before stays as .bak; no temp file left");
            File.WriteAllBytes(bookPath, new byte[4096]);   // a power loss after the rename: a zero-filled file
            var fromBak = ServerBook.Load(bookPath, out var note2);
            Check(fromBak != null && fromBak.ToJson() == b1.ToJson() && note2.Contains("backup") && Directory.GetFiles(tmpRoot, "server-book-Test.json.unread-*").Length == 1 && !File.Exists(bookPath),
                  "AT a zero-filled book: the backup (the save before) is used, the broken file set aside, never deleted");
            File.Delete(bookPath + ".bak");
            File.WriteAllText(bookPath, "{\"version\":99}");
            var newer = ServerBook.Load(bookPath, out var note3);
            Check(newer == null && note3.Contains("new one starts") && Directory.GetFiles(tmpRoot, "server-book-Test.json.unread-*").Length == 2,
                  "AT a newer format without a backup: set aside (not overwritten), a new book starts");
            Check(ServerBook.Load(Path.Combine(tmpRoot, "none.json"), out var note4) == null && note4 == null, "AT no book at all: a new one, nothing logged");
            // the server's per-player JSON goes through the same writer: a stale temp file from a crash never blocks the next write
            var pj = Path.Combine(tmpRoot, "players", "1001.json");
            Directory.CreateDirectory(Path.GetDirectoryName(pj)); File.WriteAllText(pj + ".tmp", "half");
            AtomicFile.Write(pj, env); AtomicFile.Write(pj, env);
            Check(File.ReadAllText(pj) == env && File.ReadAllText(pj + ".bak") == env && !File.Exists(pj + ".tmp") && File.ReadAllBytes(pj)[0] == (byte)'{',
                  "AT players/<key>.json: a crash's leftover temp file is replaced, the file is whole UTF-8 without BOM (kstats reads it), one .bak");

            // ---------- item 7: the cargo book per world ----------
            string M(string t, string what, string world) => "{\"t\":\"" + t + "\",\"marker\":\"" + what + "\"" + (world == null ? "" : ",\"world\":\"" + world + "\"") + "}";
            string L(string t, string action, string who, int n, float x) => "{\"t\":\"" + t + "\",\"player\":\"P\",\"playerId\":" + (who ?? "0") + ",\"playerKey\":\"" + (who ?? "") +
                "\",\"via\":\"sender\",\"container\":\"Karve\",\"kind\":\"ship\",\"containerId\":\"9:1\",\"containerBuilder\":1,\"x\":" + x + ",\"z\":0,\"item\":\"IronScrap\",\"maker\":\"\",\"quality\":1,\"action\":\"" + action + "\",\"count\":" + n + "}";
            var mixed = new[]
            {
                M("2026-10-08T10:00:00Z", "server-start", null),       // before 0.6.1: no world named
                L("2026-10-08T10:10:00Z", "put", "111", 10, 0), L("2026-10-08T10:20:00Z", "take", "222", 10, 1000),
                M("2026-10-08T10:30:00Z", "world-saved", null),
                M("2026-10-08T11:00:00Z", "server-start", "Old"),      // 0.6.1 deployed while the old world ran
                L("2026-10-08T11:10:00Z", "put", "111", 5, 0), L("2026-10-08T11:20:00Z", "take", "222", 5, 2000),
                M("2026-10-08T11:30:00Z", "world-saved", "Old"),
                M("2026-10-09T09:00:00Z", "server-start", "New"),      // a new world is started
                L("2026-10-09T09:10:00Z", "put", "333", 7, 0), L("2026-10-09T09:20:00Z", "take", "444", 7, 500),
                M("2026-10-09T09:30:00Z", "world-saved", "New"),
            };
            double Deliv(ServerBook b, string who) => b.Of(who) != null && b.Of(who).Delivered.TryGetValue("IronScrap", out var v) ? v : 0;
            var bNew = new ServerBook(); var rNew = bNew.Replay(mixed, "New");
            var bOld = new ServerBook(); var rOld = bOld.Replay(mixed, "Old");
            var bAll = new ServerBook(); bAll.Replay(mixed);
            Check(Deliv(bNew, "444") == 3500 && Deliv(bNew, "222") == 0 && rNew.OtherWorld == 4 && rNew.Applied == 2,
                  "WO the new world's book holds only its own cargo (7 x 500 m); the old world's 4 lines are left out");
            Check(Deliv(bOld, "222") == 20000 && Deliv(bOld, "444") == 0 && rOld.Applied == 4 && rOld.OtherWorld == 2,
                  "WO the old world's book keeps the lines from before 0.6.1 (they belong to the first world a marker names) plus its own");
            Check(Deliv(bAll, "222") == 20000 && Deliv(bAll, "444") == 3500, "WO without a world (older callers) every line counts, as before");
            var unnamedOnly = mixed.Take(4).ToList();
            var bU = new ServerBook(); var rU = bU.Replay(unnamedOnly, "Any");
            Check(Deliv(bU, "222") == 10000 && rU.Applied == 2, "WO logs with no world named at all belong to the world being rebuilt");

            // ---------- B2 / decision d: old daily logs compressed in place, never deleted ----------
            var logDir = Path.Combine(tmpRoot, "Hearthwoven"); var playersDir = Path.Combine(logDir, "players");
            Directory.CreateDirectory(playersDir);
            var realChests = Directory.Exists(real) ? Directory.GetFiles(real, "chests-*.jsonl").OrderBy(f => f, StringComparer.Ordinal).ToList() : new List<string>();
            var realRouted = Directory.Exists(real) ? Directory.GetFiles(real, "damage-routed-*.jsonl").ToList() : new List<string>();
            // the real logs moved to old days (and synthetic ones when the fixtures are absent), plus today's and yesterday's
            if (realChests.Count > 0) { for (int i = 0; i < realChests.Count; i++) File.Copy(realChests[i], Path.Combine(logDir, "chests-2026080" + (1 + i) + ".jsonl")); }
            else File.WriteAllLines(Path.Combine(logDir, "chests-20260801.jsonl"), mixed.Take(4));
            if (realRouted.Count > 0) File.Copy(realRouted[0], Path.Combine(logDir, "damage-routed-20260801.jsonl"));
            File.WriteAllText(Path.Combine(playersDir, "received-20260801.jsonl"), "{\"t\":\"x\",\"reason\":\"interval\"}\n");
            File.WriteAllText(Path.Combine(logDir, "births-20261008.jsonl"), "{\"creature\":\"Wolf_cub\"}\n");         // yesterday: kept plain
            File.WriteAllText(Path.Combine(logDir, "chests-20261009.jsonl"), M("2026-10-09T08:00:00Z", "server-start", "Test") + "\n");   // today
            File.WriteAllText(Path.Combine(logDir, "server-book-Test.json"), "{}");                                     // not a daily log
            var expectBefore = new ServerBook(); expectBefore.ReplayLogs(logDir, "Test", out var filesBefore);
            var linesBefore = DailyLogs.Files(logDir, "chests").SelectMany(DailyLogs.ReadLines).ToList();
            var today = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
            var none = DailyLogs.Compress(new[] { logDir, playersDir }, today, 0);
            clock.Restart();
            var rep = DailyLogs.Compress(new[] { logDir, playersDir }, today, 30);
            clock.Stop();
            System.Console.WriteLine($"MEASURED log compression: {rep.Compressed} files, {rep.BytesBefore / 1024} KB -> {rep.BytesAfter / 1024} KB ({(rep.BytesBefore == 0 ? 0 : 100.0 * rep.BytesAfter / rep.BytesBefore):0.0} %) in {clock.ElapsedMilliseconds} ms");
            var after = new ServerBook(); var rAfter = after.ReplayLogs(logDir, "Test", out var filesAfter);
            var linesAfter = DailyLogs.Files(logDir, "chests").SelectMany(DailyLogs.ReadLines).ToList();
            Check(none.Compressed == 0 && rep.Failed == 0 && rep.Compressed == realChests.Count + (realChests.Count > 0 ? 0 : 1) + (realRouted.Count > 0 ? 1 : 0) + 1,
                  "LG the days older than 30 days are compressed (chests, routed damage, received); 0 days = off");
            Check(!Directory.GetFiles(logDir, "chests-2026080*.jsonl").Any() && Directory.GetFiles(logDir, "chests-2026080*.jsonl.gz").Length == Math.Max(1, realChests.Count) &&
                  File.Exists(Path.Combine(logDir, "chests-20261009.jsonl")) && File.Exists(Path.Combine(logDir, "births-20261008.jsonl")) && File.Exists(Path.Combine(logDir, "server-book-Test.json")) &&
                  File.Exists(Path.Combine(playersDir, "received-20260801.jsonl.gz")) && !Directory.GetFiles(logDir, "*.tmp", SearchOption.AllDirectories).Any(),
                  "LG in place: <name>.jsonl.gz replaces <name>.jsonl; today's and yesterday's logs, the book and other files untouched; no temp files");
            Check(linesAfter.SequenceEqual(linesBefore) && filesAfter == filesBefore && after.ToJson() == expectBefore.ToJson() && rAfter.Lines == linesBefore.Count,
                  $"LG nothing lost: the readers read .jsonl.gz the same ({linesAfter.Count} chest lines, the rebuilt book identical)");
            if (realChests.Any(f => f.EndsWith("chests-20261008.jsonl")))
            {
                double sent = 0; foreach (var who in RealKeys(after)) sent += after.Of(who).Sent.TryGetValue("IronScrap", out var v) ? v : 0;
                Check(Math.Abs(sent - 1951700) < 10000, $"LG the real 8 Oct iron haul replays from the compressed log to {sent / 1000:0.0} item-km sent, as from the plain one");
            }
            var again = DailyLogs.Compress(new[] { logDir, playersDir }, today, 30);
            Check(again.Compressed == 0 && again.Failed == 0, "LG a second run changes nothing");
            // a crash between writing the .gz and removing the original; and a .gz that differs from its original
            var dupe = Path.Combine(logDir, "births-20260701.jsonl"); File.WriteAllText(dupe, "{\"a\":1}\n");
            using (var o = File.Create(dupe + ".gz")) using (var z = new GZipStream(o, CompressionLevel.Optimal)) { var by = File.ReadAllBytes(dupe); z.Write(by, 0, by.Length); }
            var diff = Path.Combine(logDir, "births-20260702.jsonl"); File.WriteAllText(diff, "{\"a\":2}\n");
            using (var o = File.Create(diff + ".gz")) using (var z = new GZipStream(o, CompressionLevel.Optimal)) { var by = Encoding.UTF8.GetBytes("{\"a\":3}\n"); z.Write(by, 0, by.Length); }
            var mixedRun = DailyLogs.Compress(new[] { logDir }, today, 1);   // 1 day asks for less than the minimum of 2
            Check(!File.Exists(dupe) && File.Exists(dupe + ".gz") && File.Exists(diff) && File.Exists(diff + ".gz") && mixedRun.Kept == 1 && File.Exists(Path.Combine(logDir, "births-20261008.jsonl")),
                  "LG an interrupted run is finished only when the .gz matches; a different .gz keeps both files; yesterday is never touched (minimum 2 days)");
            Check(DailyLogs.Files(logDir, "births").Select(Path.GetFileName).SequenceEqual(new[] { "births-20260701.jsonl.gz", "births-20260702.jsonl", "births-20261008.jsonl" }),
                  "LG the readers take one file per day, oldest first, the plain one when both exist");
            Check(DailyLogs.Day(new DateTime(2026, 10, 9)) == "20261009", "LG day names are invariant (yyyyMMdd)");
        }
        finally { try { Directory.Delete(tmpRoot, true); } catch { } }
        return fails;
    }

    static IEnumerable<string> RealKeys(ServerBook b)
    {
        using (var doc = System.Text.Json.JsonDocument.Parse(b.ToJson()))
            return doc.RootElement.GetProperty("players").EnumerateObject().Select(p => p.Name).ToList();
    }
}
