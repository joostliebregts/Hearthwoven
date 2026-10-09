// 0.6.1 local-file resilience tests (RESILIENCE-06 items 2, 3, 4, 7 and the downgrade note): non-finite numbers never
// written and tolerated on read; atomic writes with one .bak and the fallback to it; one file per character (player id +
// name) and the migration of a file keyed by the id only; unknown top-level keys kept through a rewrite; a newer format
// left alone. Fictional characters and ids; every file lives in a fresh temp folder.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hearthwoven;

static class LocalFileTests
{
    const long Id = 2718281828L;
    static readonly DateTime T0 = new DateTime(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);

    static string NewDir() { var d = Path.Combine(Path.GetTempPath(), "hw-res-local-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }
    static float Chops(SessionEvents e) => e.ChopHits.TryGetValue("Beech1", out var v) ? v : 0;
    static SessionEvents Ev(float chops) { var e = new SessionEvents(); SessionEvents.Add(e.ChopHits, "Beech1", chops); return e; }
    static IDictionary<string, IDictionary<string, float>> Counters(float wood) =>
        new Dictionary<string, IDictionary<string, float>> { ["pickedUp"] = new Dictionary<string, float> { ["Wood"] = wood } };

    // a file as 0.6.0 wrote it: keyed by the id only, no name, no schema, a pickup baseline of 100 wood, 7 axe hits
    static string LegacyFile(string dir)
    {
        var t = new LocalTotals { PlayerId = Id, FirstRunUtc = T0, BiomeFromUtc = T0 };
        t.TakeBaseline("pickedUp", new Dictionary<string, float> { ["Wood"] = 100 }, null, T0);
        t.Record("S1", null, Ev(7));
        var json = t.ToJson(T0).Replace("\"schema\":" + LocalTotals.Schema + ",", "");
        var path = LocalTotals.PathFor(dir, Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
        return path;
    }

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- item 2: NaN and Infinity ----------
        Check(Json.F(double.NaN) == "0" && Json.F(double.PositiveInfinity) == "0" && Json.F(double.NegativeInfinity) == "0" && Json.F(float.NaN) == "0" && Json.F(2.5) == "2.5",
              "R2 NaN and +/-Infinity are written as 0, never as NaN/Infinity");
        var raw = new Dictionary<string, float> { ["a"] = float.NaN, ["b"] = float.PositiveInfinity, ["c"] = 3 };
        var dictJson = new Json().Open().Dict("d", raw).Close().ToString();
        Check(dictJson == "{\"d\":{\"c\":3}}", "R2 a tally's non-finite entries are left out of the JSON: " + dictJson);
        var ev = new SessionEvents();
        SessionEvents.Add(ev.ChopHits, "Beech1", float.NaN); SessionEvents.Add(ev.ChopHits, "Beech1", float.PositiveInfinity); SessionEvents.Add(ev.ChopHits, "Beech1", 2);
        var dt = new DamageTally(); dt.AddDealt("Troll", "Axes", new HitData.DamageTypes { m_slash = float.NaN, m_fire = 4 });
        var bio = new BiomeTally(); bio.AddDamage("Meadows", true, "slash", float.NaN); bio.AddDamage("Meadows", true, "fire", float.PositiveInfinity);
        var log = new EventLog(); log.AddDamage(T0, "Meadows", false, "Troll", "EnemyHit", new HitData.DamageTypes { m_blunt = float.NaN, m_frost = float.NegativeInfinity, m_pierce = 3 });
        var death = log.AddDeath(T0, "Meadows", float.NaN, float.PositiveInfinity);
        var feats = new FeatsLedger(); feats.Add("k", double.NaN); feats.Max("best", double.PositiveInfinity); feats.Add("k", 2);
        Check(Chops(ev) == 2 && dt.Dealt.Values.All(v => Json.IsFinite(v)) && dt.Dealt.Values.Sum() == 4 && bio.Damage.Count == 0 &&
              log.Damage.Values.All(v => Json.IsFinite(v)) && log.Damage.Values.Sum() == 3 && feats.Count("k") == 2 && feats.Count("best") == 0,
              "R2 the tallies (session, damage, biome, event log, feats) never add a NaN or infinite value");
        var logJson = new Json().Open(); log.WriteTo(logJson); logJson.Close();
        Check(!logJson.ToString().Contains("NaN") && !logJson.ToString().Contains("Infinity") && MiniJson.Parse(logJson.ToString()) != null,
              "R2 a death at a NaN position writes valid JSON (x/z as 0)");
        var odd = MiniJson.Parse("{\"a\":NaN,\"b\":Infinity,\"c\":-Infinity,\"d\":1e999,\"e\":2.5,\"f\":-NaN,\"g\":[NaN,1]}") as Dictionary<string, object>;
        Check(odd != null && (double)odd["a"] == 0 && (double)odd["b"] == 0 && (double)odd["c"] == 0 && (double)odd["d"] == 0 && (double)odd["e"] == 2.5 && (double)odd["f"] == 0 &&
              ((List<object>)odd["g"]).Count == 2, "R2 the reader takes NaN, Infinity, -Infinity and out-of-range numbers as 0 instead of failing the whole file");
        Check(MiniJson.Parse("{\"a\":x}") == null && MiniJson.Parse("{\"a\":-}") == null, "R2 the reader still rejects what is not a number at all");
        // an older file that already holds a NaN (written by 0.6.0) loads instead of being set aside
        var nanDir = NewDir(); var nanPath = LocalTotals.PathFor(nanDir, Id, "Rowan");
        var nanT = new LocalTotals { Name = "Rowan" }; nanT.Record("S1", null, Ev(5));
        Directory.CreateDirectory(Path.GetDirectoryName(nanPath));
        File.WriteAllText(nanPath, nanT.ToJson(T0).Replace("\"Beech1\":5", "\"Beech1\":5,\"Oak1\":NaN"));
        var nanBack = LocalTotals.Load(nanPath, Id, out var nanProblem);
        Check(nanBack != null && nanProblem == null && Chops(nanBack.EventsBefore("S2")) == 5 && Directory.GetFiles(Path.GetDirectoryName(nanPath), "*.unreadable").Length == 0,
              "R2 a local file with a NaN in it (0.6.0) loads; since install is kept, nothing set aside");
        var nanIn = new LocalTotals { Name = "Rowan" }; nanIn.LastEvents.ChopHits["Beech1"] = float.NaN; nanIn.PreviousEvents.ChopHits["Beech1"] = float.PositiveInfinity;
        Check(LocalTotals.FromJson(nanIn.ToJson(T0)) != null && !nanIn.ToJson(T0).Contains("NaN") && !nanIn.ToJson(T0).Contains("Infinity"),
              "R2 a NaN that reaches the totals in memory is never written to the file");

        // ---------- item 3: atomic writes, one .bak, the fallback ----------
        var aDir = NewDir(); var aPath = Path.Combine(aDir, "f.json");
        AtomicFile.Write(aPath, "one"); AtomicFile.Write(aPath, "two"); AtomicFile.Write(aPath, "three");
        Check(File.ReadAllText(aPath) == "three" && File.ReadAllText(aPath + ".bak") == "two" && !File.Exists(aPath + ".tmp") &&
              Directory.GetFiles(aDir).Length == 2, "R3 AtomicFile: the new text in place, the previous save as the one .bak, no temp file left");
        var bytes = File.ReadAllBytes(aPath);
        Check(bytes.Length == 5 && bytes[0] == (byte)'t', "R3 written as UTF-8 without a BOM");

        var dir3 = NewDir(); var p3 = LocalTotals.PathFor(dir3, Id, "Rowan");
        var t3 = new LocalTotals { Name = "Rowan" };
        t3.Record("A", null, Ev(4)); t3.Save(p3, T0);
        t3.Record("B", null, Ev(6)); t3.Save(p3, T0.AddMinutes(5));   // main: A 4 + B 6; .bak: A 4
        // crash between writing the temp file and the replace: the temp file is there, the old main untouched
        File.WriteAllText(p3 + ".tmp", "{\"version\":1,\"totals\":{\"prev");
        var c1 = LocalTotals.Load(p3, Id, out var pr1);
        Check(c1 != null && pr1 == null && Chops(c1.EventsBefore("C")) == 10, "R3 crash mid-write (temp file half written, never swapped in): the main file loads as it was");
        // power loss after the swap but before the data reached the disk: the main file is zero-filled
        File.WriteAllBytes(p3, new byte[300]);
        var c2 = LocalTotals.Load(p3, Id, out var pr2);
        Check(c2 != null && Chops(c2.EventsBefore("C")) == 4 && pr2 != null && pr2.Contains("backup") &&
              Directory.GetFiles(dir3, "*.unreadable", SearchOption.AllDirectories).Length == 1,
              "R3 a zero-filled main file (power loss): the .bak is used (4 axe hits, the save before), the broken main set aside, the log says so once");
        c2.Record("C", null, Ev(1)); c2.Save(p3, T0.AddMinutes(10));
        var c3 = LocalTotals.Load(p3, Id, out var pr3);
        Check(c3 != null && pr3 == null && Chops(c3.EventsBefore("D")) == 5 && File.Exists(p3 + ".bak"),
              "R3 the next save writes a fresh main file and the load after it is clean (4 + 1)");
        // a truncated main file (half the JSON) with a good .bak
        var good = File.ReadAllText(p3);
        File.WriteAllText(p3, good.Substring(0, good.Length / 2));
        var c4 = LocalTotals.Load(p3, Id, out var pr4);
        Check(c4 != null && pr4 != null && pr4.Contains("unreadable") && pr4.Contains("backup"), "R3 a truncated main file: the .bak is used, and the log names both");
        // main missing, .bak there (a file system without an atomic replace, interrupted)
        File.Delete(p3);
        var c5 = LocalTotals.Load(p3, Id, out var pr5);
        Check(c5 != null && pr5 != null && pr5.Contains("missing"), "R3 a missing main file with a .bak: the .bak is used");
        // both unreadable: as before, counting restarts at zero and nothing is overwritten
        File.WriteAllText(p3, "{\"version\":1,\"tot"); File.WriteAllText(p3 + ".bak", "garbage");
        var c6 = LocalTotals.Load(p3, Id, out var pr6);
        Check(c6 != null && Chops(c6.EventsBefore("x")) == 0 && pr6 != null && File.ReadAllText(p3 + ".bak") == "garbage" && !File.Exists(p3),
              "R3 main and .bak both unreadable: main set aside, .bak left as it is, counting from zero");
        // a newer main file is never swapped for the older .bak
        File.WriteAllText(p3, "{\"version\":2,\"totals\":{}}"); t3.Save(p3 + ".other", T0); File.Copy(p3 + ".other", p3 + ".bak", true);
        var c7 = LocalTotals.Load(p3, Id, out var pr7);
        Check(c7 == null && pr7 != null && pr7.Contains("newer") && File.ReadAllText(p3).Contains("\"version\":2"),
              "R3/R7 a newer-format main file: not loaded, not replaced by the backup, not saved over");

        // ---------- item 4: one file per character (player id + name) ----------
        var dir4 = NewDir();
        var rowan = LocalTotals.LoadFor(dir4, Id, "Rowan", Counters(0), () => new List<IDictionary<string, IDictionary<string, float>>>(), out var rowanPath, out var rp);
        var ash = LocalTotals.LoadFor(dir4, Id, "Ash", Counters(0), () => new List<IDictionary<string, IDictionary<string, float>>>(), out var ashPath, out var ap);
        rowan.Record("A", null, Ev(30)); rowan.Save(rowanPath, T0);
        ash.Record("B", null, Ev(2)); ash.Save(ashPath, T0);
        var rowan2 = LocalTotals.LoadFor(dir4, Id, "Rowan", Counters(0), null, out var rowanPath2, out rp);
        var ash2 = LocalTotals.LoadFor(dir4, Id, "Ash", Counters(0), null, out var ashPath2, out ap);
        Check(rowanPath != ashPath && rowanPath2 == rowanPath && rowanPath.EndsWith(Path.Combine("local", Id + "-Rowan.json")) && rp == null && ap == null &&
              Chops(rowan2.EventsBefore("C")) == 30 && Chops(ash2.EventsBefore("C")) == 2 && rowan2.Name == "Rowan" && File.ReadAllText(rowanPath).Contains("\"name\":\"Rowan\""),
              "R4 two characters sharing one player id (a copied .fch) keep separate files: 30 and 2 axe hits, never mixed");
        var lower = LocalTotals.LoadFor(dir4, Id, "rowan", Counters(0), null, out var lowerPath, out var lp);
        lower.Record("D", null, Ev(1)); lower.Save(lowerPath, T0);
        Check(Chops(lower.EventsBefore("E")) == 1 && Chops(LocalTotals.LoadFor(dir4, Id, "Rowan", Counters(0), null, out _, out _).EventsBefore("E")) == 30,
              "R4 names equal but for case (one file on a case-insensitive disk) still get separate counts");
        Check(LocalTotals.SafeName("Bjørn") != LocalTotals.SafeName("Bjärn") && LocalTotals.SafeName("Rowan") == "Rowan" && LocalTotals.SafeName("").StartsWith("unnamed~") &&
              !LocalTotals.SafeName("../x\\y:z").Contains("/") && !LocalTotals.SafeName("../x\\y:z").Contains("\\") && LocalTotals.SafeName(new string('a', 80)).Length < 52,
              "R4 file-safe names: non-ASCII names get a hash (Bjørn and Bjärn differ), no path characters, bounded length");

        // legacy file (keyed by the id only), exactly one plausible character: migrated
        var dir5 = NewDir(); var legacy5 = LegacyFile(dir5);
        var m1 = LocalTotals.LoadFor(dir5, Id, "Rowan", Counters(150), () => new List<IDictionary<string, IDictionary<string, float>>>(), out var m1Path, out var m1p);
        Check(m1 != null && Chops(m1.EventsBefore("S2")) == 7 && m1.Baseline["pickedUp"]["Wood"] == 100 && m1.FirstRunUtc == T0 && m1p != null && m1p.Contains("moved") &&
              !File.Exists(legacy5) && File.Exists(legacy5 + ".migrated-Rowan") && File.Exists(m1Path) && File.ReadAllText(m1Path).Contains("\"name\":\"Rowan\""),
              "R4 a 0.6.0 file with exactly one plausible character: moved to that character's file (7 axe hits, baseline, first run kept), the old file kept as .migrated-Rowan");
        var m2 = LocalTotals.LoadFor(dir5, Id, "Ash", Counters(150), null, out _, out var m2p);
        Check(m2 != null && Chops(m2.EventsBefore("S2")) == 0 && m2p == null, "R4 a second character with that id afterwards starts at zero (nothing adopted twice)");
        // two plausible characters (the file could be either's): left alone, neither adopts
        var dir6 = NewDir(); var legacy6 = LegacyFile(dir6); var before6 = File.ReadAllText(legacy6);
        var other = new List<IDictionary<string, IDictionary<string, float>>> { Counters(120) };
        var n1 = LocalTotals.LoadFor(dir6, Id, "Rowan", Counters(150), () => other, out var n1Path, out var n1p);
        n1.Record("X", null, Ev(1)); n1.Save(n1Path, T0);
        var n2 = LocalTotals.LoadFor(dir6, Id, "Rowan2", Counters(120), () => new List<IDictionary<string, IDictionary<string, float>>> { Counters(150) }, out _, out var n2p);
        Check(Chops(n1.EventsBefore("Y")) == 1 && Chops(n2.EventsBefore("Y")) == 0 && n1p != null && n1p.Contains("2 characters") && n2p != null &&
              File.Exists(legacy6) && File.ReadAllText(legacy6) == before6,
              "R4 a 0.6.0 file two characters on this PC could have made: neither adopts it, nothing merged, the file left exactly as it was");
        // the current character cannot have made it (its counter is below the file's baseline): not adopted
        var dir7 = NewDir(); var legacy7 = LegacyFile(dir7);
        var q1 = LocalTotals.LoadFor(dir7, Id, "Rowan", Counters(50), () => new List<IDictionary<string, IDictionary<string, float>>>(), out _, out var q1p);
        Check(Chops(q1.EventsBefore("Y")) == 0 && File.Exists(legacy7) && q1p != null && q1p.Contains("do not fit"),
              "R4 a 0.6.0 file whose baseline exceeds this character's counters (not its file): not adopted, left as it is");
        // only another character fits; and the character list could not be read (null): the one plausible character adopts it
        var dir8 = NewDir(); LegacyFile(dir8);
        var o1 = LocalTotals.LoadFor(dir8, Id, "Rowan", Counters(50), () => new List<IDictionary<string, IDictionary<string, float>>> { Counters(150) }, out _, out _);
        var o2 = LocalTotals.LoadFor(dir8, Id, "Ash", Counters(150), () => null, out _, out var o2p);
        Check(Chops(o1.EventsBefore("Y")) == 0 && Chops(o2.EventsBefore("Y")) == 7 && o2p.Contains("moved"),
              "R4 the file goes to the one character it fits (Ash), not to Rowan; an unreadable character list counts as no other character");

        // ---------- downgrade / schema: unknown top-level keys survive a rewrite ----------
        var dir9 = NewDir(); var p9 = LocalTotals.PathFor(dir9, Id, "Rowan");
        var t9 = new LocalTotals { Name = "Rowan" }; t9.Record("A", null, Ev(3)); t9.Save(p9, T0);
        var future = "\"days\":{\"2026-10-09\":{\"chops\":5,\"big\":1234567890123,\"frac\":0.1}},\"future\":[1,\"x\\\"y\",true,null,2.5,{\"deep\":[[]]}],";
        File.WriteAllText(p9, File.ReadAllText(p9).Replace("\"schema\":2,", "\"schema\":9,").Replace("\"totals\":", future + "\"totals\":"));
        var t9b = LocalTotals.Load(p9, Id, out var p9p);
        t9b.Record("B", null, Ev(1)); t9b.Save(p9, T0.AddMinutes(1));
        var after = MiniJson.Parse(File.ReadAllText(p9)) as Dictionary<string, object>;
        var days = MiniJson.Obj(MiniJson.Obj(after, "days"), "2026-10-09");
        Check(t9b != null && p9p == null && Chops(t9b.EventsBefore("C")) == 4 && days != null && (double)days["chops"] == 5 && (double)days["big"] == 1234567890123d && (double)days["frac"] == 0.1 &&
              MiniJson.Write(after["future"]) == "[1,\"x\\\"y\",true,null,2.5,{\"deep\":[[]]}]" && MiniJson.Num(after, "schema") == LocalTotals.Schema,
              "R-D a newer writer's additive keys (days, future) are read past and written back unchanged on the next save; the file is readable (same version, higher schema)");
        Check(t9b.Extra.Count == 2 && !t9b.Extra.ContainsKey("totals") && !t9b.Extra.ContainsKey("schema"), "R-D only unknown top-level keys are kept aside, the known ones are rewritten");
        var noName = LocalTotals.FromJson("{\"version\":1,\"playerId\":5,\"totals\":{\"sessions\":0,\"previous\":{},\"lastSession\":{\"id\":\"A\"}}}");
        Check(noName != null && noName.Name == "" && noName.Extra.Count == 0, "R-D the oldest file form (no name, schema, baseline or feats) still reads");

        // ---------- item 7 (client): a newer file format is never misread ----------
        var dir10 = NewDir(); var p10 = LocalTotals.PathFor(dir10, Id, "Rowan");
        Directory.CreateDirectory(Path.GetDirectoryName(p10));
        var newer = "{\"version\":2,\"playerId\":1,\"name\":\"Rowan\",\"totals\":{\"sessions\":\"changed meaning\"}}";
        File.WriteAllText(p10, newer);
        var r1 = LocalTotals.LoadFor(dir10, Id, "Rowan", Counters(0), null, out _, out var r1p);
        var r2 = LocalTotals.LoadFor(dir10, Id, "Rowan", Counters(0), null, out _, out var r2p);
        Check(r1 == null && r2 == null && r1p != null && r1p.Contains("newer") && r1p == r2p && File.ReadAllText(p10) == newer && !File.Exists(p10 + ".bak"),
              "R7 a newer-format file: refused (no totals, so nothing saved over it), the same message each time (logged once), the file byte-for-byte kept");
        var dir11 = NewDir(); var legacy11 = LocalTotals.PathFor(dir11, Id);
        Directory.CreateDirectory(Path.GetDirectoryName(legacy11)); File.WriteAllText(legacy11, newer);
        var r3 = LocalTotals.LoadFor(dir11, Id, "Rowan", Counters(0), null, out _, out var r3p);
        Check(r3 != null && Chops(r3.EventsBefore("A")) == 0 && r3p.Contains("newer") && File.ReadAllText(legacy11) == newer,
              "R7 a newer-format file under the old id-only key is not migrated or touched");

        foreach (var d in new[] { nanDir, aDir, dir3, dir4, dir5, dir6, dir7, dir8, dir9, dir10, dir11 }) try { Directory.Delete(d, true); } catch { }
        return fails;
    }
}
