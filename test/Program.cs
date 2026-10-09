// Unit tests for the pure parts of Hearthwoven: snapshot JSON from the game's own PlayerStats, transport, damage tally,
// the routed-damage parser (game-serialized hit, read back) and the local since-install totals.
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Hearthwoven;

int fails = 0;
void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

var stats = new PlayerProfile.PlayerStats[10];
stats[0] = new PlayerProfile.PlayerStats();
stats[0][PlayerStatType.DistanceSail] = 12345.6f;
stats[0][PlayerStatType.DistanceSailHelm] = 9000f;
stats[0][PlayerStatType.DeathByBurning] = 3f;
stats[0].m_itemPickupStats["IronScrap"] = 812f;
stats[0].m_itemPickupStats["CopperOre"] = 430f;
stats[0].m_knownWorlds["My Server"] = 3600f * 120;
stats[0].m_enemyStats[0]["$enemy_troll"] = 7f;
stats[0].m_foodEatenStats["FeastMountains"] = 4f;
stats[3] = new PlayerProfile.PlayerStats();            // an all-zero copy must be skipped

var tally = new DamageTally();
var d = new HitData.DamageTypes { m_slash = 30f, m_fire = 12.5f };
tally.AddDealt("Troll", "Axes", d); tally.AddDealt("Troll", "Axes", d);
tally.AddTaken("Troll", "EnemyHit", new HitData.DamageTypes { m_blunt = 40f });

var json = Snapshot.Build("0.1.0", 1001L, "Rowan", stats,
    new[] { new Snapshot.SkillInfo { Name = "Blocking", Level = 42.5f, Accumulator = 3.2f } }, "My Server", tally);
JsonDocument doc = null;
try { doc = JsonDocument.Parse(json); } catch (System.Exception e) { System.Console.WriteLine(e.Message); }
Check(doc != null, "snapshot is valid JSON");
var root = doc.RootElement;
Check(root.GetProperty("playerId").GetInt64() == 1001L && root.GetProperty("name").GetString() == "Rowan", "player id and name (with ø)");
var s0 = root.GetProperty("stats");
Check(s0.GetArrayLength() == 1 && s0[0].GetProperty("index").GetInt32() == 0, "only slot 0 (raw totals) is sent");
var c = s0[0].GetProperty("counters");
Check(c.GetProperty("DistanceSailHelm").GetDouble() == 9000 && c.GetProperty("DeathByBurning").GetDouble() == 3, "counters by name");
Check(!c.TryGetProperty("Jumps", out _), "zero counters are left out");
Check(s0[0].GetProperty("itemsPickedUp").GetProperty("IronScrap").GetDouble() == 812, "items picked up per kind");
Check(s0[0].GetProperty("secondsPerWorld").GetProperty("My Server").GetDouble() == 432000, "seconds played per world");
Check(s0[0].GetProperty("enemyKills")[0].GetProperty("kills").GetProperty("$enemy_troll").GetDouble() == 7, "kills per enemy table");
Check(root.GetProperty("skills")[0].GetProperty("skill").GetString() == "Blocking", "skills");
var dmg = root.GetProperty("damageThisSession");
Check(dmg.GetProperty("hitsDealt").GetInt32() == 2 && dmg.GetProperty("dealt").GetProperty("Troll|Axes|fire").GetDouble() == 25, "damage dealt summed per target, skill and type");
Check(dmg.GetProperty("taken").GetProperty("Troll|EnemyHit|blunt").GetDouble() == 40, "damage taken per source, cause and type");

var packed = Transport.Pack(json);
Check(Transport.Unpack(packed) == json, $"gzip transport round-trip ({json.Length} -> {packed.Length} bytes)");

// routed-damage parser
var hit = new HitData { m_skill = Skills.SkillType.Bows, m_hitType = HitData.HitType.PlayerHit };
hit.m_damage.m_pierce = 55f;
var rd = new ZRoutedRpc.RoutedRPCData { m_senderPeerID = 1, m_targetPeerID = 2, m_methodHash = StringExtensionMethods.GetStableHashCode("RPC_Damage") };
ZRpc.Serialize(new object[] { hit }, ref rd.m_parameters);
var pkg = new ZPackage(); rd.Serialize(pkg); pkg.SetPos(0);
var back = RoutedDamage.TryRead(pkg, out _);
Check(back != null && back.m_damage.m_pierce == 55f && back.m_skill == Skills.SkillType.Bows, "routed RPC_Damage parsed");

// fragments: split under 4 KiB, reassemble in any order, per sender and message
var big = new byte[10000]; new System.Random(1).NextBytes(big);
var frags = Fragments.Split(big);
Check(frags.Count == 4 && frags.All(f => f.Length <= 3000), "fragments stay under 4 KiB");
var asm = new Fragments.Assembler();
byte[] whole = null;
foreach (var i in new[] { 2, 0, 3, 1 }) whole = asm.Add(7, "m1", i, frags.Count, frags[i]) ?? whole;
Check(whole != null && whole.SequenceEqual(big) && asm.Pending == 0, "fragments reassemble out of order");
Check(asm.Add(7, "m2", 0, 2, frags[0]) == null && asm.Pending == 1, "an incomplete message waits");

// ---------- chest integrity (INTEGRITY.md rules C1-C9) ----------
// inventory bytes exactly as the game writes them (Inventory.Save, item version 109)
byte[] Inv(params (int prefab, int stack, string maker, int quality, int x, int y)[] items)
{
    var p = new ZPackage(); p.Write(109); p.Write((ushort)items.Length);
    foreach (var (prefab, stack, maker, quality, x, y) in items)
    {
        p.Write(10000); p.Write((byte)x); p.Write((byte)y); p.Write((byte)0);
        byte flags = (byte)(0x40 | (quality > 1 ? 4 : 0) | (stack > 1 ? 8 : 0) | (maker != "" ? 0x20 : 0));
        p.Write(flags);
        if (quality > 1) p.Write((ushort)quality);
        if (stack > 1) p.Write((ushort)stack);
        if (maker != "") { p.Write(123L); p.Write(maker); }
        p.Write(prefab);
        p.Write((byte)0);   // cheated flag (item version >= ChunksNCheats)
    }
    return p.GetArray();
}
int H(string s) => StringExtensionMethods.GetStableHashCode(s);
int fishWraps = H("FishWraps"), iron = H("IronScrap"), wood = H("Wood"), sword = H("SwordIron");
string K(int prefab, string maker = "", int q = 1) => prefab + "|" + maker + "|" + q;
List<ChestLedger.Slot> P(byte[] b) => ChestLedger.Parse(b);
const long A = 101, B = 202, C = 303;
string Ev(List<ChestLedger.Event> es) => string.Join(", ", es.Select(e => $"{e.Peer}:{e.Action}:{e.Count}:{e.Via}").OrderBy(x => x));

var saved = Inv((fishWraps, 10, "Rowan", 1, 0, 0), (iron, 30, "", 1, 1, 0));
var changed = Inv((fishWraps, 7, "Rowan", 1, 0, 0), (iron, 30, "", 1, 1, 0), (iron, 20, "", 1, 2, 0));
Check(ChestLedger.Count(P(saved))[K(fishWraps, "Rowan")] == 10 && ChestLedger.Count(P(saved))[K(iron)] == 30, "C0 inventory parsed per item, maker and quality");
// C1: the first change after a server restart is counted (baseline = the server's saved copy, not 'first sighting')
var e1 = ChestLedger.Attribute(P(saved), P(changed), "chest", false, true, null, A, A);
Check(Ev(e1) == "101:put:20:sender, 101:take:3:sender", "C1 first change after a restart: 3 fish wraps taken, 20 iron put in (" + Ev(e1) + ")");
// C2: the same update arriving again counts nothing
Check(ChestLedger.Attribute(P(changed), P(changed), "chest", false, true, null, A, A).Count == 0, "C2 a repeated update counts nothing twice");
// C3: splitting, merging and moving stacks inside the chest is no event
var shuffled = Inv((iron, 25, "", 1, 3, 1), (iron, 25, "", 1, 0, 2), (fishWraps, 7, "Rowan", 1, 4, 0));
Check(ChestLedger.Attribute(P(changed), P(shuffled), "chest", false, true, null, A, A).Count == 0, "C3 stacking, splitting and moving inside a chest is no event");
// C4: a better copy of the same item is a different item (quality is part of the key)
var swapQ = ChestLedger.Attribute(P(Inv((sword, 1, "Edda", 1, 0, 0))), P(Inv((sword, 1, "Edda", 3, 0, 0))), "chest", false, true, null, A, A);
Check(Ev(swapQ) == "101:put:1:sender, 101:take:1:sender", "C4 swapping a quality 1 sword for a quality 3 sword is a take and a put");
// C5: a world chest (dungeon) filling with loot on first open is not a gift from the opener
var loot = ChestLedger.Attribute(P(null), P(Inv((iron, 3, "", 1, 0, 0))), "chest", true, false, null, A, A);
Check(loot.Count == 1 && loot[0].Action == "loot-spawned", "C5 dungeon loot appearing on first open is 'loot-spawned', not 'put'");
// C6: a tombstone is not a chest: dropping on death and picking it back up are grave events
var grave = ChestLedger.Attribute(P(null), P(Inv((iron, 30, "", 1, 0, 0), (sword, 1, "Edda", 2, 1, 0))), "grave", true, false, null, A, A);
var graveBack = ChestLedger.Attribute(P(Inv((iron, 30, "", 1, 0, 0))), P(null), "grave", false, false, null, A, A);
Check(grave.All(e => e.Action == "grave-drop") && graveBack.All(e => e.Action == "grave-take") && grave.Count == 2, "C6 death drops and tombstone pickups are grave events, never chest gifts");
// C7: player-built chest seen for the first time (new or never opened): a put is a put
Check(Ev(ChestLedger.Attribute(P(null), P(Inv((wood, 50, "", 1, 0, 0))), "chest", true, true, null, A, A)) == "101:put:50:sender", "C7 first fill of a player-built chest counts as put");
// C8: two players in one chest (MultiUserChest): the request that passed the server names who did what
var claims = new List<ChestLedger.Claim> {
    new ChestLedger.Claim { Peer = B, Kind = "add", Key = K(fishWraps, "Rowan"), Amount = 5, T = 0 },
    new ChestLedger.Claim { Peer = B, Kind = "remove", X = 1, Y = 0, Amount = 3, T = 0 },
};
var mucBefore = Inv((fishWraps, 10, "Rowan", 1, 0, 0), (iron, 30, "", 1, 1, 0));
var mucAfter = Inv((fishWraps, 15, "Rowan", 1, 0, 0), (iron, 27, "", 1, 1, 0), (wood, 10, "", 1, 2, 0));
var muc = ChestLedger.Attribute(P(mucBefore), P(mucAfter), "chest", false, true, claims, A, A);
Check(Ev(muc) == "101:put:10:sender, 202:put:5:shared-chest, 202:take:3:shared-chest" && claims.Count == 0,
      "C8 shared chest: friend's 5 fish wraps in and 3 iron out are theirs, the owner's 10 wood is the owner's (" + Ev(muc) + ")");
// C8b: a request that failed (chest full) never matches and expires
var failed = new List<ChestLedger.Claim> { new ChestLedger.Claim { Peer = B, Kind = "add", Key = K(sword, "Edda"), Amount = 1, T = 0 } };
var noMatch = ChestLedger.Attribute(P(mucBefore), P(mucAfter), "chest", false, true, failed, A, A);
ChestLedger.Expire(failed, 20.0);
Check(noMatch.All(e => e.Peer == A) && failed.Count == 0, "C8b a failed shared-chest request is never counted and expires after 15 s");
// C8c: the MultiUserChest package format (InventoryHelper.WriteItemToPackage) is read correctly
var req = new ZPackage(); req.Write(7); req.Write(new Vector2i(2, 1));
req.Write(true); req.Write("FishWraps"); req.Write(5); req.Write(100f); req.Write(new Vector2i(0, 0)); req.Write(1); req.Write(0); req.Write(55L); req.Write("Rowan"); req.Write(0); req.Write(false); req.Write(false); req.Write(0);
req.Write(true); req.SetPos(0);
var parsed = ChestLedger.ReadMucRequest("MUC_RequestItemAdd", req, B, 1.0);
var rem = new ZPackage(); rem.Write(8); rem.Write(new Vector2i(1, 0)); rem.Write(new Vector2i(-1, -1)); rem.Write(3); rem.Write(false); rem.SetPos(0);
var parsedRem = ChestLedger.ReadMucRequest("MUC_RequestItemRemove", rem, B, 1.0);
Check(parsed.Count == 2 && parsed[0].Key == K(fishWraps, "Rowan") && parsed[0].Amount == 5 && parsed[1].Swap && parsedRem.Count == 1 && parsedRem[0].X == 1 && parsedRem[0].Amount == 3
      && ChestLedger.ReadMucRequest("SomethingElse", new ZPackage(), B, 1).Count == 0, "C8c MultiUserChest add/remove requests parsed; other messages ignored");
// C9: a change sent by a PC that is not the chest's manager (craft-from-chest mods) belongs to that PC
Check(Ev(ChestLedger.Attribute(P(saved), P(changed), "chest", false, true, null, C, A)).Contains("303:take:3:sender-not-owner"), "C9 craft-from-chests: the sending PC is the actor, marked sender-not-owner");

// measured events in the snapshot
var ev = new SessionEvents();
SessionEvents.Add(ev.AteFoodMadeBy, "Rowan|FishWraps", 2); ev.Parries = 5; ev.Blocks = 9;
SessionEvents.Add(ev.Bought, "Haldor|Megingjord", 1); SessionEvents.Add(ev.PickaxeHits, "rock4_copper", 12);
var js2 = Snapshot.Build("0.1.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", null, "s1", ev);
var m = JsonDocument.Parse(js2).RootElement.GetProperty("measuredThisSession");
Check(m.GetProperty("parries").GetInt32() == 5 && m.GetProperty("ateFoodMadeBy").GetProperty("Rowan|FishWraps").GetDouble() == 2
      && m.GetProperty("bought").GetProperty("Haldor|Megingjord").GetDouble() == 1, "measured events: parries, whose food, trader");
// K3: the since-install totals travel beside this session's, under their own keys, and survive the shared copy; left out when not given
var evAll = SessionEvents.Sum(ev); SessionEvents.Add(evAll.AteFoodMadeBy, "Rowan|FishWraps", 7);
var dmgAll = new DamageTally(); dmgAll.AddDealt("Troll", "Axes", new HitData.DamageTypes { m_slash = 300f });
var js2b = GroupShare.SharedCopy(Snapshot.Build("0.2.1", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", new DamageTally(), "s1", ev, null, true, evAll, dmgAll));
var rootB = JsonDocument.Parse(js2b).RootElement;
Check(rootB.GetProperty("measuredSinceInstall").GetProperty("ateFoodMadeBy").GetProperty("Rowan|FishWraps").GetDouble() == 9
      && rootB.GetProperty("measuredThisSession").GetProperty("ateFoodMadeBy").GetProperty("Rowan|FishWraps").GetDouble() == 2
      && rootB.GetProperty("damageSinceInstall").GetProperty("dealt").GetProperty("Troll|Axes|slash").GetDouble() == 300
      && !JsonDocument.Parse(js2).RootElement.TryGetProperty("measuredSinceInstall", out _),
      "K3 snapshot: measuredSinceInstall and damageSinceInstall beside this session's numbers, kept in the shared copy; absent when not given (old readers ignore them)");

// ---------- profile and measured data (INTEGRITY.md rules P1-P5) ----------
// P2: every session is archived by its id; the sum over sessions is the measured total
Check(Transport.SessionOf("{\"mod\":\"0.2.0\",\"session\":\"ab12cd34ef56\",\"x\":1}") == "ab12cd34ef56" && Transport.SafeName("../../etc") == "etc", "P2 session id read from the snapshot; file name sanitised");
// P4: event log with time and biome
var log = new EventLog();
var t0 = new System.DateTime(2026, 10, 7, 20, 17, 30, System.DateTimeKind.Utc);
log.AddDamage(t0, "Swamp", false, "BlobElite", "EnemyHit", new HitData.DamageTypes { m_blunt = 10f });
log.AddDamage(t0.AddSeconds(3), "Swamp", false, "BlobElite", "Poisoned", new HitData.DamageTypes { m_poison = 25f });
log.AddDamage(t0.AddSeconds(4), "Swamp", true, "Draugr", "Axes", new HitData.DamageTypes { m_slash = 40f });
var death = log.AddDeath(t0.AddSeconds(6), "Swamp", 10f, 20f);
Check(EventLog.Bucket(t0) == "2026-10-07T20:17Z", "P4 one-minute time buckets in UTC (Battle's 10 and 30 minute windows)");
Check(log.Damage["2026-10-07T20:17Z|Swamp|taken|BlobElite|Poisoned|poison"] == 25f && log.Damage["2026-10-07T20:17Z|Swamp|dealt|Draugr|Axes|slash"] == 40f, "P4 damage per time, biome, enemy, cause and type");
Check(death.Killer == "BlobElite" && death.Cause == "Poisoned" && death.Last10s["BlobElite|Poisoned|poison"] == 25f && death.Biome == "Swamp", "P4 a death names biome, killer, cause and the last 10 seconds");
Check(death.Timeline.Select(h => h.Ago + "|" + h.Source + "|" + h.Type + "|" + h.Amount).SequenceEqual(new[] { "6|BlobElite|blunt|10", "3|BlobElite|poison|25" }),
      "Death timeline: the hits received before the death with their time (seconds before), oldest first, one entry per type; dealt hits are not in it");
var tlog = new EventLog(); var tt = new System.DateTime(2026, 10, 8, 20, 0, 0, System.DateTimeKind.Utc);
tlog.AddDamage(tt.AddSeconds(-40), "Swamp", false, "Draugr", "EnemyHit", new HitData.DamageTypes { m_slash = 99f });
tlog.AddDamage(tt.AddSeconds(-25), "Swamp", false, "Draugr", "EnemyHit", new HitData.DamageTypes { m_slash = 30f, m_frost = 5f });
tlog.AddDamage(tt.AddSeconds(-4), "Swamp", false, "Leech", "EnemyHit", new HitData.DamageTypes { m_pierce = 12f });
var tdeath = tlog.AddDeath(tt, "Swamp", 0, 0);
Check(tdeath.Timeline.Select(h => h.Ago + "|" + h.Type + "|" + h.Amount).SequenceEqual(new[] { "25|slash|30", "25|frost|5", "4|pierce|12" }) &&
      tdeath.Last10s.Count == 1 && tdeath.Last10s["Leech|EnemyHit|pierce"] == 12f && tdeath.Killer == "Leech",
      "Death timeline: 30 s back (the hit 40 s before is left out); the last 10 s totals and the killer stay as they were");
for (int i = 0; i < 100; i++) tlog.AddDamage(tt.AddSeconds(10 + i * 0.2), "Swamp", false, "Blob", "Poisoned", new HitData.DamageTypes { m_poison = 1f });
var tmany = tlog.AddDeath(tt.AddSeconds(31), "Swamp", 0, 0);
Check(tmany.Timeline.Count == EventLog.MaxTimelineHits && System.Math.Abs(tmany.Timeline[tmany.Timeline.Count - 1].Ago - 1.2f) < 1e-3, "Death timeline: bounded, at most 40 hits per death, the latest kept");
var js3 = Snapshot.Build("0.2.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", null, "s1", new SessionEvents(), log);
var ml = JsonDocument.Parse(js3).RootElement.GetProperty("measuredLog");
Check(ml.GetProperty("deaths")[0].GetProperty("killer").GetString() == "BlobElite" && ml.GetProperty("bucketMinutes").GetInt32() == EventLog.BucketMinutes, "P4 event log travels in the snapshot");
// N1-N3: never enforced: no dependency on Jotunn or ServerSync, no network-compatibility attribute, nothing sent to clients
var ksAsm = typeof(Snapshot).Assembly;
var refs = ksAsm.GetReferencedAssemblies().Select(a => a.Name).ToList();
Check(!refs.Any(r => r.Contains("Jotunn") || r.Contains("ServerSync")), "N1 Hearthwoven references neither Jotunn nor ServerSync (" + string.Join(",", refs) + ")");
Check(!ksAsm.GetType("Hearthwoven.Plugin").GetCustomAttributesData().Any(a => a.AttributeType.Name.Contains("BepInDependency") || a.AttributeType.Name.Contains("NetworkCompatibility")), "N2 no BepInDependency or NetworkCompatibility attribute (nobody gets kicked)");

// ---------- group sharing (INTEGRITY.md S1-S3) ----------
var shared = Snapshot.Build("0.2.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", null, "s1", new SessionEvents(), log, true);
var notShared = Snapshot.Build("0.2.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", null, "s1", new SessionEvents(), log, false);
Check(GroupShare.Shares(shared) && !GroupShare.Shares(notShared), "S1 the snapshot says whether this player shares (ON is the config default)");
var copy = GroupShare.SharedCopy(shared);
var cd = JsonDocument.Parse(copy).RootElement.GetProperty("measuredLog").GetProperty("deaths")[0];
Check(!cd.TryGetProperty("x", out _) && !cd.TryGetProperty("z", out _) && cd.GetProperty("biome").GetString() == "Swamp", "S2 the shared copy keeps the biome of a death but not its position");
var parsedJson = MiniJson.Parse(copy) as Dictionary<string, object>;
Check(parsedJson != null && (string)parsedJson["name"] == "Edda" && Transport.Field(copy, "name") == "Edda"
      && ((Dictionary<string, object>)parsedJson["measuredLog"])["deaths"] is List<object>, "S3 MiniJson reads a shared snapshot back (name, nested log)");
Check(MiniJson.Parse("{\"a\":[1,2.5,-3e2,true,null,\"\\u00f8\"]}") is Dictionary<string, object> mj && ((List<object>)mj["a"]).Count == 6 && (string)((List<object>)mj["a"])[5] == "ø", "S3 MiniJson: numbers, booleans, null, unicode escapes");

// ---------- regressions from the independent verification (VERIFY-INTEGRITY.md X1-X8, F1-F15, G1-G8) ----------
void MucItem(ZPackage z, string prefab, int stack, long crafterId, string maker)
{
    z.Write(true); z.Write(prefab); z.Write(stack); z.Write(100f); z.Write(new Vector2i(0, 0)); z.Write(1); z.Write(0);
    z.Write(crafterId); z.Write(maker); z.Write(0); z.Write(false); z.Write(false); z.Write(0);
}
ZPackage Pk(System.Action<ZPackage> w) { var z = new ZPackage(); w(z); z.SetPos(0); return z; }
// X1 / C11: an old-format item list is never diffed
var oldFmt = new ZPackage(); oldFmt.Write(106); oldFmt.Write(2); oldFmt.Write("IronScrap"); oldFmt.Write(30);
Check(ChestLedger.Parse(oldFmt.GetArray()) == null && ChestLedger.Attribute(null, P(Inv((iron, 31, "", 1, 0, 0))), "chest", false, true, null, A, A).Count == 0,
      "C11 an old-format chest is read as unknown, so its first change books nothing (was: the whole chest as put)");
// X2 / C8: a swap in a shared chest: the friend dropped 20 wood on the owner's sword; the sword went to the friend
var swapClaims = ChestLedger.ReadMucRequest("MUC_RequestItemAdd", Pk(z => { z.Write(41); z.Write(new Vector2i(0, 0)); MucItem(z, "Wood", 20, 0, ""); z.Write(true); }), B, 0);
ChestLedger.ApplyMucResponse("MUC_RequestItemAddResponse", Pk(z => { z.Write(41); z.Write(new Vector2i(3, 2)); z.Write(true); z.Write(20); MucItem(z, "SwordIron", 1, 77, "Edda"); }), B, swapClaims);
var swapEv = ChestLedger.Attribute(P(Inv((sword, 1, "Edda", 1, 0, 0))), P(Inv((wood, 20, "", 1, 0, 0))), "chest", false, true, swapClaims, A, A);
Check(Ev(swapEv) == "202:put:20:shared-chest, 202:take:1:shared-chest", "C8 swap: the friend put 20 wood in and took the sword out (" + Ev(swapEv) + ")");
// X3 / C8: a failed request is removed by the manager's answer and never takes credit for a later change
var failClaims = ChestLedger.ReadMucRequest("MUC_RequestItemAdd", Pk(z => { z.Write(42); z.Write(new Vector2i(-1, -1)); MucItem(z, "Wood", 10, 0, ""); z.Write(false); }), B, 0);
ChestLedger.ApplyMucResponse("MUC_RequestItemAddResponse", Pk(z => { z.Write(42); z.Write(new Vector2i(0, 0)); z.Write(false); z.Write(0); z.Write(false); }), B, failClaims);
var later = ChestLedger.Attribute(P(Inv((iron, 30, "", 1, 0, 0))), P(Inv((iron, 30, "", 1, 0, 0), (wood, 10, "", 1, 1, 0))), "chest", false, true, failClaims, A, A);
Check(failClaims.Count == 0 && Ev(later) == "101:put:10:sender", "C8 a failed request is dropped on the answer; the owner's own 10 wood stay the owner's");
// X4: a failed remove likewise
var remClaims = ChestLedger.ReadMucRequest("MUC_RequestItemRemove", Pk(z => { z.Write(43); z.Write(new Vector2i(1, 0)); z.Write(new Vector2i(-1, -1)); z.Write(20); z.Write(false); }), B, 0);
ChestLedger.ApplyMucResponse("MUC_RequestItemRemoveResponse", Pk(z => { z.Write(43); z.Write(false); z.Write(0); z.Write(false); z.Write(false); }), B, remClaims);
Check(remClaims.Count == 0, "C8 a failed remove is dropped on the answer");
// F15: a maker name without a crafter id is not a maker (as in the chest's own bytes)
var noId = ChestLedger.ReadMucRequest("MUC_RequestItemAdd", Pk(z => { z.Write(44); z.Write(new Vector2i(-1, -1)); MucItem(z, "FishWraps", 2, 0, "Rowan"); z.Write(false); }), B, 0);
Check(noId[0].Key == K(fishWraps), "C8 crafter name without crafter id is ignored, like the game does");
// F1 / C9: a feeder chest emptied by its mod while nobody has it open is automation, not that player's take
var feed = ChestLedger.Attribute(P(Inv((H("Coal"), 50, "", 1, 0, 0))), P(Inv((H("Coal"), 49, "", 1, 0, 0))), "feeder", false, true, null, A, A, inUse: false);
var chestUnattended = ChestLedger.Attribute(P(Inv((iron, 30, "", 1, 0, 0))), P(Inv((iron, 20, "", 1, 0, 0))), "chest", false, true, null, C, A, inUse: false);
Check(feed.Count == 1 && feed[0].Action == "auto-feed" && feed[0].Via == "unattended" && chestUnattended[0].Via == "unattended",
      "C9 a feeder chest taking coal by itself is auto-feed; any change with nobody at the chest is marked unattended");
// F4 / C10: cargo of a sunk ship or broken cart is salvage, never a chest gift or take
var crate1 = ChestLedger.Attribute(new List<ChestLedger.Slot>(), P(Inv((iron, 60, "", 1, 0, 0))), "crate", true, false, null, A, A);
var crate2 = ChestLedger.Attribute(P(Inv((iron, 60, "", 1, 0, 0))), new List<ChestLedger.Slot>(), "crate", false, false, null, A, A);
Check(crate1[0].Action == "salvage-drop" && crate2[0].Action == "salvage-take", "C10 cargo crates are salvage-drop / salvage-take");
// X6 / P5: deaths beyond the cap are counted, not lost silently
var many = new EventLog();
for (int i = 0; i < EventLog.MaxDeaths + 5; i++) many.AddDeath(t0, "Swamp", 0, 0);
Check(many.Deaths.Count == EventLog.MaxDeaths && many.DeathsNotListed == 5, "P5 deaths over the cap are counted (deathsNotListed)");
// P6: minute buckets older than three hours and ten minutes are folded into ten-minute buckets (the log and what is shared stay small)
var folding = new EventLog(); var f0 = new System.DateTime(2026, 10, 7, 12, 0, 0, System.DateTimeKind.Utc);
for (int i = 0; i < 300; i++) folding.AddDamage(f0.AddMinutes(i), "Swamp", true, "Draugr", "Axes", new HitData.DamageTypes { m_slash = 2f });
var foldedKeys = folding.Damage.Keys.Where(k => string.CompareOrdinal(k, 0, EventLog.Bucket(f0.AddMinutes(299 - EventLog.FoldAfterMinutes)), 0, 17) < 0).ToList();
Check(System.Math.Abs(folding.Damage.Values.Sum() - 600f) < 1e-3 && System.Math.Abs(folding.Hits.Values.Sum() - 300f) < 1e-3 && foldedKeys.Count > 0 && foldedKeys.All(k => k[15] == '0') && folding.Damage.Count < 300 - 100 + 5,
      $"P6 old minute buckets fold into ten-minute ones, nothing lost ({folding.Damage.Count} keys for 300 minutes of fighting)");
// X7 / G3: reading a big shared snapshot stays fast (was 1.4 s at 4000 buckets)
var bigLog = new EventLog();
for (int i = 0; i < EventLog.MaxBuckets; i++) bigLog.AddDamage(t0.AddMinutes(10 * i), "Swamp", i % 2 == 0, "Enemy" + i, "EnemyHit", new HitData.DamageTypes { m_slash = 1, m_poison = 1 });
var bigJson = Snapshot.Build("0.2.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", null, "s1", new SessionEvents(), bigLog, true);
var sw = System.Diagnostics.Stopwatch.StartNew(); var bigParsed = MiniJson.Parse(bigJson); sw.Stop();
Check(bigParsed != null && sw.ElapsedMilliseconds < 150, $"G3 MiniJson reads a {bigJson.Length / 1024} KB snapshot in {sw.ElapsedMilliseconds} ms (linear)");
// X8 / G4: deep nesting is refused instead of crashing the game
Check(MiniJson.Parse(new string('[', 200000)) == null, "G4 a deeply nested snapshot is refused, no stack overflow");
// G8: the shared copy leaves out which worlds you played
Check(!GroupShare.SharedCopy(shared).Contains("secondsPerWorld") && GroupShare.SharedCopy(shared).Contains("\"stats\""), "S2 the shared copy also leaves out the worlds you played");

// ---------- server identity: ZNetPeer.m_playerID is always 0 on a dedicated server (nothing invokes "PlayerID") ----------
Check(PeerIdentity.IdOf(0, 2718281828L, 0) == 2718281828L && PeerIdentity.IdOf(0, 0, 2718281828L) == 2718281828L && PeerIdentity.IdOf(55, 66, 77) == 55 && PeerIdentity.IdOf(0, 66, 77) == 66,
      "ID1 a peer's id: the game's own if set, else the character ZDO's playerID, else the id its snapshot carries");
Check(PeerIdentity.IdOf(0, 0, 0) == null && PeerIdentity.KeyOf(null, "Platform_42", 9) == "Platform42" && PeerIdentity.KeyOf(null, "", 9) == "peer-9" &&
      PeerIdentity.KeyOf(PeerIdentity.IdOf(0, 0, 0), null, 0) != "0",
      "ID2 no id at all: files are keyed by the platform id (file-safe), never by 0");
var rowan = Snapshot.Build("0.2.0", 2718281828L, "Rowan", stats, new Snapshot.SkillInfo[0], "TestWorld", tally, "41f2aa004d54");
Check(PeerIdentity.SnapshotPlayerId(rowan) == 2718281828L && PeerIdentity.KeyOf(PeerIdentity.IdOf(0, 0, PeerIdentity.SnapshotPlayerId(rowan)), "Platform_42", 1) == "2718281828",
      "ID3 the live case (peer id 0, no ZDO yet): the snapshot is stored as 2718281828, not 0");
Check(PeerIdentity.Utf8.GetPreamble().Length == 0, "ID4 server files are written without a BOM");

// ---------- local since-install totals on the player's PC (INTEGRITY P7) ----------
var ltDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hw-local-" + System.Guid.NewGuid().ToString("N"));
var ltPath = LocalTotals.PathFor(ltDir, 1234567890L);
SessionEvents Ev1(float chops, int blocks) { var e = new SessionEvents { Blocks = blocks }; SessionEvents.Add(e.ChopHits, "Beech1", chops); return e; }
float Chops(SessionEvents e) => e.ChopHits.TryGetValue("Beech1", out var v) ? v : 0;
var t2026 = new System.DateTime(2026, 10, 8, 12, 0, 0, System.DateTimeKind.Utc);
var fresh = LocalTotals.Load(ltPath, 1234567890L, out var ltProblem);
Check(fresh != null && ltProblem == null && Chops(fresh.EventsBefore("A")) == 0 && fresh.EventsBefore("A").Blocks == 0 && ltPath.EndsWith(System.IO.Path.Combine("local", "1234567890.json")),
      "P7 no file (older install, new character): counting starts at zero, at local/<playerId>.json");
// session A, saved twice at the send interval: the second save replaces the first
fresh.Record("A", tally, Ev1(3, 1)); fresh.Save(ltPath, t2026);
fresh.Record("A", tally, Ev1(5, 2)); fresh.Save(ltPath, t2026);
var afterA = LocalTotals.Load(ltPath, 1234567890L, out ltProblem);
Check(ltProblem == null && Chops(afterA.EventsBefore("B")) == 5 && afterA.EventsBefore("B").Blocks == 2 && afterA.Sessions == 0 && afterA.LastSession == "A",
      "P7 the same session saved twice counts once (replaced, not added): 5 axe hits, not 8");
Check(Chops(afterA.EventsBefore("A")) == 0, "P7 a respawn in the same session does not add that session to what came before");
Check(afterA.DamageBefore("B").HitsDealt == tally.HitsDealt && afterA.DamageBefore("B").Dealt.Count == tally.Dealt.Count, "P7 damage dealt and taken are kept too");
// session B: saved once at 2, then 7 counted, then a crash before the next save
afterA.Record("B", null, Ev1(2, 0)); afterA.Save(ltPath, t2026);
// (then 5 more axe hits are counted in memory, never saved)
var afterCrash = LocalTotals.Load(ltPath, 1234567890L, out ltProblem);
Check(Chops(afterCrash.EventsBefore("C")) == 5 + 2 && afterCrash.Sessions == 1, "P7 a crash loses only what came after the last save (A 5 + B 2 kept, B's unsaved 5 lost), nothing doubled");
afterCrash.Record("C", null, Ev1(4, 1)); afterCrash.Save(ltPath, t2026);
var afterC = LocalTotals.Load(ltPath, 1234567890L, out ltProblem);
Check(Chops(afterC.EventsBefore("D")) == 11 && afterC.EventsBefore("D").Blocks == 3 && afterC.Sessions == 2, "P7 a new session folds the last one in: 5 + 2 + 4 = 11 axe hits over three sessions");
// a session that crashed before its first save adds nothing and breaks nothing
var noSave = LocalTotals.Load(ltPath, 1234567890L, out ltProblem); noSave.Record("E", null, Ev1(100, 0));   // crash: never saved
Check(Chops(LocalTotals.Load(ltPath, 1234567890L, out ltProblem).EventsBefore("F")) == 11, "P7 a session that crashed before its first save adds nothing");
// every kind survives the file
var allKinds = new SessionEvents { Blocks = 4, Parries = 1 };
foreach (var kv in allKinds.Named()) SessionEvents.Add(kv.Value, "k|" + kv.Key, 1.25f);
var roundTrip = LocalTotals.FromJson(new LocalTotals { LastSession = "Z", LastEvents = allKinds }.ToJson(t2026)).EventsBefore("other");
Check(System.Linq.Enumerable.All(roundTrip.Named(), kv => kv.Value.TryGetValue("k|" + kv.Key, out var v) && v == 1.25f) && roundTrip.Parries == 1 && roundTrip.Blocks == 4,
      "P7 every measured kind (food, feasts, trader, smelters, skills, gear, sailing, axe, pickaxe, repairs, maps, carts, gathering) survives the file");
// atomic write, bounded size, room for history
var savedJson = System.IO.File.ReadAllText(ltPath);
var ltRoot = JsonDocument.Parse(savedJson).RootElement;
Check(!System.IO.File.Exists(ltPath + ".tmp") && ltRoot.GetProperty("version").GetInt32() == 1 && ltRoot.GetProperty("totals").GetProperty("lastSession").GetProperty("id").GetString() == "C" &&
      !savedJson.Contains("measuredLog") && !savedJson.Contains("timeline") && !savedJson.Contains("\"killer\""), "P7 written via a temp file (none left over); version + totals object, no raw events or event log (per-biome counts only)");
var few2 = new LocalTotals(); var many2 = new LocalTotals();
for (int i = 0; i < 2; i++) few2.Record("s" + i, tally, Ev1(3, 1));
for (int i = 0; i < 1000; i++) many2.Record("s" + i, tally, Ev1(3, 1));
var big2 = many2.ToJson(t2026);
Check(Chops(many2.EventsBefore("next")) == 3000 && big2.Length < few2.ToJson(t2026).Length + 100, $"P7 bounded: 1000 sessions fold into one number per kind ({big2.Length} bytes)");
System.IO.File.WriteAllText(ltPath + ".tmp", "half a file");   // a crash mid-write left a temp file
many2.Save(ltPath, t2026);
Check(Chops(LocalTotals.Load(ltPath, 1234567890L, out ltProblem).EventsBefore("next")) == 3000, "P7 a leftover temp file from a crash mid-write does not break the next save");
// unreadable and newer files are never silently overwritten
System.IO.File.Delete(ltPath + ".bak");   // no backup here: the fallback to the .bak is in LocalFileTests
System.IO.File.WriteAllText(ltPath, "{\"version\":1,\"totals\":{\"previous\":{\"meas");
var broken = LocalTotals.Load(ltPath, 1234567890L, out ltProblem);
Check(broken != null && Chops(broken.EventsBefore("x")) == 0 && ltProblem != null && !System.IO.File.Exists(ltPath) &&
      System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(ltPath), "*.unreadable").Length == 1, "P7 an unreadable file is set aside (renamed, kept), never overwritten; counting restarts at zero and the log says so");
System.IO.File.WriteAllText(ltPath, "{\"version\":2,\"totals\":{}}");
Check(LocalTotals.Load(ltPath, 1234567890L, out ltProblem) == null && ltProblem != null && System.IO.File.ReadAllText(ltPath).Contains("\"version\":2"), "P7 a file from a newer format is left alone (not loaded, not saved over)");
// the game's pickup counter when Hearthwoven first ran (K1): taken once, kept through saves, never moved by later growth
var withBase = new LocalTotals();
var counterNow = new System.Collections.Generic.Dictionary<string, float> { ["$item_wood"] = 900, ["$item_stone"] = 40 };
var tookFirst = withBase.TakeBaseline("pickedUp", counterNow);
counterNow["$item_wood"] = 950;   // the game counter grows after install: never taken again
var tookAgain = withBase.TakeBaseline("pickedUp", counterNow);
withBase.Record("A", null, Ev1(1, 0));
var baseBack = LocalTotals.FromJson(withBase.ToJson(t2026));
Check(tookFirst && !tookAgain && baseBack.Baseline["pickedUp"]["$item_wood"] == 900 && baseBack.Baseline["pickedUp"]["$item_stone"] == 40 && Chops(baseBack.EventsBefore("B")) == 1,
      "K1 baseline: the game's pickup counter is stored once (900 wood), a later counter (950) does not move it, it survives the file beside the totals");
var lateBase = new LocalTotals();
lateBase.TakeBaseline("pickedUp", new System.Collections.Generic.Dictionary<string, float> { ["$item_wood"] = 900 }, new System.Collections.Generic.Dictionary<string, float> { ["$item_wood"] = 50 });
var lateBack = LocalTotals.FromJson(lateBase.ToJson(t2026));
Check(lateBack.ExactAtBaseline["pickedUp"]["$item_wood"] == 50 && lateBack.Baseline["pickedUp"]["$item_wood"] == 900 && withBase.ExactAtBaseline["pickedUp"].Count == 0,
      "K1 an older install: what Hearthwoven had counted exactly when the baseline was taken (50 wood) is kept beside it; a fresh install keeps none");
// Battle (K1 for hits and deaths): the game's EnemyHits/PlayerHits/Deaths when Hearthwoven first ran, Hearthwoven's own count since
var bt = new LocalTotals();
var bEv1 = new SessionEvents(); SessionEvents.Add(bEv1.Battle, "EnemyHits", 30); SessionEvents.Add(bEv1.Battle, "Deaths", 2);
bt.Record("A", null, bEv1);
var bTook = bt.TakeBaseline(LocalTotals.BattleKind, new System.Collections.Generic.Dictionary<string, float> { ["EnemyHits"] = 5000, ["Deaths"] = 40, ["PlayerHits"] = 0 }, bt.EventsBefore("B").Battle);
var bAgain = bt.TakeBaseline(LocalTotals.BattleKind, new System.Collections.Generic.Dictionary<string, float> { ["EnemyHits"] = 5600, ["Deaths"] = 41 }, bt.EventsBefore("B").Battle);
var bEv2 = new SessionEvents(); SessionEvents.Add(bEv2.Battle, "EnemyHits", 15); SessionEvents.Add(bEv2.Battle, "Deaths", 1);
bt.Record("B", null, bEv2);
var bBack = LocalTotals.FromJson(bt.ToJson(t2026));
var bSince = SessionEvents.Sum(bBack.EventsBefore("C"));
Check(bTook && !bAgain && bBack.Baseline["battle"]["EnemyHits"] == 5000 && bBack.Baseline["battle"]["Deaths"] == 40 && !bBack.Baseline["battle"].ContainsKey("PlayerHits") &&
      bBack.ExactAtBaseline["battle"]["EnemyHits"] == 30 && bBack.ExactAtBaseline["battle"]["Deaths"] == 2 && bSince.Battle["EnemyHits"] == 45 && bSince.Battle["Deaths"] == 3,
      "Battle baseline: the game's hit and death counters stored once (a later 5\u00A0600 does not move it), with what Hearthwoven had counted by then; the battle counts carry over sessions in the file");
var bHits = LocalTotals.Layers(bBack.Baseline, bBack.ExactAtBaseline, LocalTotals.BattleKind, "EnemyHits", bSince.Battle["EnemyHits"], 5600);
var bDeaths = LocalTotals.Layers(bBack.Baseline, bBack.ExactAtBaseline, LocalTotals.BattleKind, "Deaths", bSince.Battle["Deaths"], 41);
Check(bHits.before == 5000 && bHits.exact == 15 && bHits.before + bHits.exact == 5015 && bDeaths.before == 40 && bDeaths.exact == 1,
      "Battle layers, no double count: an older install's 30 hits before the baseline are in the game's 5\u00A0000 already (5\u00A0015, not 5\u00A0045); the game counter's later growth (5\u00A0600) is never added");
var bFresh = LocalTotals.Layers(new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, float>> { ["battle"] = new System.Collections.Generic.Dictionary<string, float> { ["EnemyHits"] = 800 } },
                                new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, float>> { ["battle"] = new System.Collections.Generic.Dictionary<string, float>() }, "battle", "EnemyHits", 120, 850);
var bNone = LocalTotals.Layers(null, null, "battle", "EnemyHits", 120, 850);
var bShort = LocalTotals.Layers(null, null, "battle", "Deaths", 5, 3);
Check(bFresh.before == 800 && bFresh.exact == 120 && bNone.before == 730 && bNone.exact == 120 && bShort.before == 0 && bShort.exact == 5,
      "Battle layers: a fresh install adds every hit counted since (800 + 120); without a baseline (a fellow's copy) the game counter minus the exact count, never below 0");
Check(LocalTotals.FromJson(new LocalTotals().ToJson(t2026)).Baseline.Count == 0 && new LocalTotals().TakeBaseline("pickedUp", null) == false,
      "K1 baseline: a file without one reads as none (an older install takes it on this version's first load); no counter, nothing stored");
System.IO.Directory.Delete(ltDir, true);

// P8: damage and deaths per biome, folded across sessions (Battle's All overview keeps its biome strip)
var bLog1 = new EventLog(); var bt0 = new System.DateTime(2026, 10, 7, 20, 0, 0, System.DateTimeKind.Utc);
bLog1.AddDamage(bt0, "Swamp", true, "Draugr", "Axes", new HitData.DamageTypes { m_slash = 40f, m_chop = 5f });
bLog1.AddDamage(bt0.AddMinutes(3), "Swamp", false, "Draugr", "EnemyHit", new HitData.DamageTypes { m_slash = 12f });
bLog1.AddDamage(bt0.AddMinutes(9), "Meadows", true, "Boar", "Clubs", new HitData.DamageTypes { m_blunt = 7f });
bLog1.AddDeath(bt0.AddMinutes(4), "Swamp", 0, 0);
var bio1 = BiomeTally.FromLog(bLog1);
Check(bio1.Damage["Swamp|dealt|slash"] == 40f && bio1.Damage["Swamp|taken|slash"] == 12f && bio1.Damage["Meadows|dealt|blunt"] == 7f && bio1.Deaths["Swamp"] == 1f && bio1.Damage.Count == 4,
      "P8 a session's log folds to biome, direction and type (no foe, no time); deaths by biome");
var bioJson = new Json().Open(); bio1.WriteTo(bioJson, "b"); var bioBack = new BiomeTally().ReadFrom(MiniJson.Obj((System.Collections.Generic.Dictionary<string, object>)MiniJson.Parse(bioJson.Close().ToString()), "b"));
Check(bioBack.Damage["Swamp|dealt|slash"] == 40f && bioBack.Deaths["Swamp"] == 1f && bioBack.Damage.Count == 4, "P8 per-biome totals survive their JSON");
var bioTotals = new LocalTotals { FirstRunUtc = t2026, BiomeFromUtc = t2026 };
bioTotals.Record("S1", new DamageTally(), new SessionEvents(), bLog1); bioTotals.Record("S1", new DamageTally(), new SessionEvents(), bLog1);   // saved twice: replaced
var bLog2 = new EventLog(); bLog2.AddDamage(bt0.AddDays(1), "Swamp", true, "Draugr", "Axes", new HitData.DamageTypes { m_slash = 10f }); bLog2.AddDeath(bt0.AddDays(1), "Mistlands", 0, 0);
bioTotals.Record("S2", new DamageTally(), new SessionEvents(), bLog2);
var bioAll = BiomeTally.Sum(bioTotals.BiomeBefore("S3"), BiomeTally.FromLog(new EventLog()));
var bioSame = bioTotals.BiomeBefore("S2");
var bioFile = LocalTotals.FromJson(bioTotals.ToJson(t2026)).BiomeBefore("S3");
Check(bioAll.Damage["Swamp|dealt|slash"] == 50f && bioAll.Deaths["Swamp"] == 1f && bioAll.Deaths["Mistlands"] == 1f && bioSame.Damage["Swamp|dealt|slash"] == 40f && bioFile.Damage["Swamp|dealt|slash"] == 50f && bioFile.Deaths["Mistlands"] == 1f,
      "P8 per biome folds across sessions like the rest (the same session saved twice counts once), a respawn does not add its own session, and it survives the file");
var noBio = new LocalTotals { FirstRunUtc = t2026 }; var noBioJson = noBio.ToJson(t2026).Replace("\"biome\":{\"damage\":{},\"deaths\":{}},", "").Replace(",\"biome\":{\"damage\":{},\"deaths\":{}}", "");
var bioDir = System.IO.Path.Combine(ltDir, "biome-old"); var bioPath = LocalTotals.PathFor(bioDir, 77);
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(bioPath)); System.IO.File.WriteAllText(bioPath, noBioJson);
var oldFile = LocalTotals.Load(bioPath, 77, out var bioProblem); var newFile = LocalTotals.Load(LocalTotals.PathFor(bioDir, 78), 78, out bioProblem);
Check(!noBioJson.Contains("\"biome\"") && oldFile.BiomeFromUtc > oldFile.FirstRunUtc.AddMinutes(10) && oldFile.BiomeBefore("x").Empty && newFile.BiomeFromUtc == newFile.FirstRunUtc,
      "P8 a file from before per-biome counting loads (no biome in it) and says per biome counts from now; a new file counts per biome from its first run");
{   // P8 flake: two separate clock reads made FirstRun and BiomeFrom differ by a tick now and then; a new file reads the clock once
    int apart = 0; for (int i = 0; i < 20000; i++) { var fr = LocalTotals.Load(LocalTotals.PathFor(bioDir, 1000 + (i % 7)), 1000 + (i % 7), out var _p); if (fr.FirstRunUtc != fr.BiomeFromUtc) apart++; }
    Check(apart == 0, "P8 a new file's first run and its per-biome start are the same instant (20000 loads, " + apart + " apart)");
}
var capped = new BiomeTally(); var capLog = new EventLog();
for (int i = 0; i < 2000; i++) capLog.AddDamage(bt0, "Mod" + i, true, "x", "y", new HitData.DamageTypes { m_slash = 1f });
capped.AddAll(BiomeTally.FromLog(capLog));
Check(capped.Damage.Count == BiomeTally.MaxKeys, "P8 bounded: a mod that invents biomes cannot grow the file past " + BiomeTally.MaxKeys + " keys");
// the shared snapshot carries it (and an older reader ignores the key)
var bioSnap = Snapshot.Build("0.2.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", null, "s1", new SessionEvents(), bLog1, true, null, null, bio1, t2026);
var bioSnapDoc = JsonDocument.Parse(bioSnap).RootElement.GetProperty("biomeSinceInstall");
Check(bioSnapDoc.GetProperty("damage").GetProperty("Swamp|dealt|slash").GetSingle() == 40f && bioSnapDoc.GetProperty("deaths").GetProperty("Swamp").GetSingle() == 1f && bioSnapDoc.TryGetProperty("from", out _) &&
      !JsonDocument.Parse(Snapshot.Build("0.2.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", null, "s1", new SessionEvents(), bLog1, true)).RootElement.TryGetProperty("biomeSinceInstall", out _),
      "P8 the snapshot carries per-biome since install (and leaves it out when there is none: an older sender)");
// the size of what is shared: a three-hour session of steady fighting, one-minute buckets against the ten-minute ones before
EventLog Session3h(bool tenMinute)
{
    var l = new EventLog(); var s0 = new System.DateTime(2026, 10, 7, 12, 0, 0, System.DateTimeKind.Utc);
    var foes = new[] { "Draugr", "Blob", "Leech", "Skeleton", "Troll" }; var skills = new[] { "Swords", "Bows", "Clubs" };
    for (int m = 0; m < 180; m++)
    {
        if (m % 5 >= 3) continue;   // fighting three minutes in five
        var at = s0.AddMinutes(tenMinute ? m / 10 * 10 : m);
        for (int h = 0; h < 4; h++)
        {
            l.AddDamage(at, "Swamp", true, foes[(m + h) % 5], skills[h % 3], new HitData.DamageTypes { m_slash = 30f, m_poison = 10f });
            l.AddDamage(at, "Swamp", false, foes[(m + h) % 5], "EnemyHit", new HitData.DamageTypes { m_blunt = 20f });
        }
    }
    return l;
}
var json10 = Snapshot.Build("0.2.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", null, "s1", new SessionEvents(), Session3h(true), true);
var json1 = Snapshot.Build("0.2.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", null, "s1", new SessionEvents(), Session3h(false), true);
int size10 = json10.Length, size1 = json1.Length, wire10 = Transport.Pack(json10).Length, wire1 = Transport.Pack(json1).Length;   // what leaves the PC is gzipped (Transport.Pack)
System.Console.WriteLine($"INFO shared snapshot, three hours of steady fighting: {size10 / 1024.0:0.0} KB ({wire10 / 1024.0:0.0} KB gzipped) with ten-minute buckets, {size1 / 1024.0:0.0} KB ({wire1 / 1024.0:0.0} KB gzipped) with one-minute buckets");
Check(size1 < 130 * 1024 && wire1 < 24 * 1024, $"P8 the shared snapshot stays modest with one-minute buckets: {size1 / 1024} KB, {wire1 / 1024} KB on the wire (ten-minute buckets: {size10 / 1024} KB, {wire10 / 1024} KB) for three hours of steady fighting");
// ---------- Farming: every plant you put in the ground (F1), the game books one per click ----------
var plantedTally = new SessionEvents();
const long planter = 1234567890L, otherPlanter = 77L;
// a PlantEasily grid: 20 flax plants created in one frame, each getting you as its creator
int countedNow = 0;
for (int i = 0; i < 20; i++) if (SessionEvents.CountPlanting(plantedTally.Planted, "$piece_sapling_flax", true, 0, planter, planter)) countedNow++;
Check(countedNow == 20 && plantedTally.Planted["$piece_sapling_flax"] == 20, "F1 twenty plants placed in one frame (a PlantEasily grid) count 20, not 1");
Check(!SessionEvents.CountPlanting(plantedTally.Planted, "$piece_woodwall", false, 0, planter, planter), "F1 a piece that is no plant (a wall) is not planted");
Check(!SessionEvents.CountPlanting(plantedTally.Planted, "$piece_sapling_flax", true, 0, otherPlanter, planter), "F1 a fellow's plant (another creator) is not yours");
Check(!SessionEvents.CountPlanting(plantedTally.Planted, "$piece_sapling_flax", true, planter, planter, planter), "F1 a piece that already had a creator (loaded again, set twice) counts nothing");
Check(!SessionEvents.CountPlanting(plantedTally.Planted, "$piece_sapling_flax", true, 0, planter, 0), "F1 no local player known: nothing counted");
Check(plantedTally.Planted.Count == 1 && plantedTally.Planted["$piece_sapling_flax"] == 20, "F1 only the twenty flax plants are booked");
// it is a since-install tally: kept in the local totals and summed over sessions
var plantTotals = new LocalTotals();
plantTotals.Record("S1", null, plantedTally);
var plantS2 = new SessionEvents(); SessionEvents.CountPlanting(plantS2.Planted, "$piece_sapling_onion", true, 0, planter, planter);
plantTotals.Record("S2", null, plantS2);
var plantBack = LocalTotals.FromJson(plantTotals.ToJson(t2026)).EventsBefore("S3").Planted;
Check(plantBack["$piece_sapling_flax"] == 20 && plantBack["$piece_sapling_onion"] == 1, "F1 plantings since install survive the totals file, summed over sessions");
var placedBase = new LocalTotals();
placedBase.TakeBaseline("piecesPlaced", new System.Collections.Generic.Dictionary<string, float> { ["$piece_sapling_flax"] = 16 });
Check(LocalTotals.FromJson(placedBase.ToJson(t2026)).Baseline["piecesPlaced"]["$piece_sapling_flax"] == 16, "F1 the game's planted counter at install (16 flax) is kept as the faded baseline");

// ---------- Cooking: dishes you made yourself (G1): the game books a cooking station's dish to the station's owner ----------
var cook = new SessionEvents();
// you take cooked meat off a fellow's grill: counted on your PC with the amount sent to the owner (a skill bonus: 2)
Check(SessionEvents.CountTakenOff(cook.Made, "$item_cookedmeat", false, new object[] { null, 1 }) == 1 &&
      SessionEvents.CountTakenOff(cook.Made, "$item_cookedmeat", false, new object[] { null, 2 }) == 2 && cook.Made["$item_cookedmeat"] == 3,
      "G1 dishes you take off any cooking station count for you, bonus included (1 + 2 = 3 cooked meat)");
Check(SessionEvents.CountTakenOff(cook.Made, "$item_coal", true, new object[] { null, 1 }) == 0 && !cook.Made.ContainsKey("$item_coal"), "G1 a burnt one (the station's overcooked item) is no dish");
Check(SessionEvents.CountTakenOff(cook.Made, "$item_neckskewer", false, null) == 1, "G1 no amount sent (an unexpected call): one dish");
// the owner's side: SpawnItem books the dish to the owner's profile; Hearthwoven skips that booking (the taker counted it)
Check(!SessionEvents.CountMade(cook.Made, "$item_cookedmeat", 1, true, true) && cook.Made["$item_cookedmeat"] == 3, "G1 a dish someone takes off YOUR grill is not yours (the station's booking on the owner's PC is skipped)");
// the crafting window (cauldron, food table, mead ketill): already the crafter's own, multicraft amount
Check(SessionEvents.CountMade(cook.Made, "$item_carrotsoup", 4, false, true) && cook.Made["$item_carrotsoup"] == 4, "G1 the cauldron's dishes count as made, with the craft's amount");
Check(!SessionEvents.CountMade(cook.Made, "$item_carrotsoup", 1, false, false) && !SessionEvents.CountMade(cook.Made, "", 1, false, true) && !SessionEvents.CountMade(cook.Made, "$item_bread", 0, false, true),
      "G1 not your profile, no item or nothing made: nothing counted");
// since install: kept in the local totals, summed over sessions; the game's craft counter at first run is the faded baseline
var cookTotals = new LocalTotals();
cookTotals.Record("S1", null, cook);
var cook2 = new SessionEvents(); SessionEvents.CountTakenOff(cook2.Made, "$item_cookedmeat", false, new object[] { null, 1 });
cookTotals.Record("S2", null, cook2);
cookTotals.TakeBaseline("itemsCrafted", new System.Collections.Generic.Dictionary<string, float> { ["$item_cookedmeat"] = 40, ["$item_sword_iron"] = 2 });
var cookBack = LocalTotals.FromJson(cookTotals.ToJson(t2026));
Check(cookBack.EventsBefore("S3").Made["$item_cookedmeat"] == 4 && cookBack.EventsBefore("S3").Made["$item_carrotsoup"] == 4 &&
      cookBack.Baseline["itemsCrafted"]["$item_cookedmeat"] == 40 && cookBack.ExactAtBaseline["itemsCrafted"].Count == 0,
      "G1 made since install survives the totals file (4 cooked meat over two sessions); the game's craft counter at first run (40) is the baseline, nothing exact before it");
// the game methods the cooking hooks attach to exist (ClientHooks: TakeOff, TakeOffAmount, StationSpawn, MadeByYou), read
// from the game assembly's metadata (loading its types needs the game's other assemblies)
var gameMethods = new System.Collections.Generic.HashSet<string>();
using (var gameFile = System.IO.File.OpenRead(typeof(CookingStation).Assembly.Location))
using (var pe = new System.Reflection.PortableExecutable.PEReader(gameFile))
{
    var md = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
    foreach (var th in md.TypeDefinitions)
    {
        var td = md.GetTypeDefinition(th); var tn = md.GetString(td.Name);
        foreach (var mh in td.GetMethods()) { var gm = md.GetMethodDefinition(mh); gameMethods.Add(tn + "." + md.GetString(gm.Name) + "/" + gm.GetParameters().Count); }
    }
}
Check(new[] { "CookingStation.OnInteract/1", "CookingStation.IsItemDone/1", "CookingStation.SpawnItem/4", "CookingStation.RPC_RemoveDoneItem/3", "ZNetView.InvokeRPC/2", "PlayerProfile.IncrementStatItemCraft/3" }.All(gameMethods.Contains),
      "G1 the game methods the cooking hooks attach to exist (OnInteract, IsItemDone, SpawnItem, RPC_RemoveDoneItem(sender, point, amount), InvokeRPC, IncrementStatItemCraft)");

// ---------- trees you felled, counted on your own PC (TreeFalls: the felling blow, then the tree destroyed) ----------
var tf = new TreeFalls<int>();
Check(!tf.Hit(1, "Beech1", 100, 40, 10f) && !tf.Hit(1, "Beech1", 60, 40, 11f) && tf.Hit(1, "Beech1", 20, 40, 12f) && tf.Destroyed(1, 12.05f) == "Beech1" && tf.Tracked == 0,
      "T1 your area: three hits, the third brings the tree down, the destroy right after counts it");
var lag = new TreeFalls<int>();
Check(!lag.Hit(2, "Birch2", 100, 40, 10f) && !lag.Hit(2, "Birch2", 100, 40, 11f) && lag.Hit(2, "Birch2", 100, 40, 12f) && lag.Destroyed(2, 12.4f) == "Birch2",
      "T2 a fellow's area, its shared health a hit behind: your own hits still know the third fells it");
var help = new TreeFalls<int>();
Check(help.Hit(3, "Oak1", 30, 40, 10f) && help.Destroyed(3, 10.3f) == "Oak1",
      "T3 a fellow chopped it first (shared health 30): your hit of 40 is the felling one, yours");
var theirs = new TreeFalls<int>();
Check(!theirs.Hit(4, "Oak1", 200, 40, 10f) && theirs.Destroyed(4, 11f) == null && theirs.Tracked == 0,
      "T4 your hit could not fell it and it fell anyway (a fellow's blow): not yours");
var late = new TreeFalls<int>();
Check(late.Hit(5, "Pine", 10, 40, 10f) && late.Destroyed(5, 10f + TreeFalls<int>.Window + 1f) == null,
      "T5 a felling blow with no destroy within the window: not counted");
var gone = new TreeFalls<int>();
gone.Hit(6, "FirTree", 10, 40, 10f);
Check(gone.Destroyed(7, 10.1f) == null && gone.Destroyed(6, 10.1f) == "FirTree" && gone.Destroyed(6, 10.2f) == null,
      "T6 another object destroyed counts nothing; the felled tree counts once");
var weak = new TreeFalls<int>();
Check(!weak.Hit(8, "Beech1", 10, 0, 10f) && weak.Destroyed(8, 10.1f) == null, "T7 a hit that does no damage (the tree shrugs it off) never fells");
var oldTrees = new TreeFalls<int>();
for (int i = 0; i < 500; i++) oldTrees.Hit(i, "Beech1", 100, 10, i);
Check(oldTrees.Tracked < 200, $"T8 trees not hit for two minutes are forgotten ({oldTrees.Tracked} kept of 500)");
var treeBase = new LocalTotals();
Check(treeBase.TakeBaseline("treesFelled", new System.Collections.Generic.Dictionary<string, float> { ["Tree"] = 410 }) &&
      LocalTotals.FromJson(treeBase.ToJson(t2026)).Baseline["treesFelled"]["Tree"] == 410 && LocalTotals.FromJson(treeBase.ToJson(t2026)).ExactAtBaseline["treesFelled"].Count == 0,
      "T9 the game's trees-felled counter at first run is the baseline; nothing Hearthwoven counted is in it (exactAtBaseline 0)");

// ---------- Deeds twins (DeedsZones): "since install" of a counter the game keeps complete = the counter now minus its baseline ----------
var twinBase = new LocalTotals();
var statsAtRun = new System.Collections.Generic.Dictionary<string, float> { ["CraftWeapon"] = 9, ["FishCaught"] = 200, ["Upgrades"] = 0 };
var firstAt = new System.DateTime(2026, 10, 9, 8, 30, 0, System.DateTimeKind.Utc);
Check(twinBase.TakeBaseline(LocalTotals.StatsKind, statsAtRun, null, firstAt) && !twinBase.TakeBaseline(LocalTotals.StatsKind, new System.Collections.Generic.Dictionary<string, float> { ["CraftWeapon"] = 99 }),
      "W1 the stats baseline is taken once per character (a second take changes nothing)");
Check(LocalTotals.Since(twinBase.Baseline, LocalTotals.StatsKind, "CraftWeapon", 14) == 5 && LocalTotals.Since(twinBase.Baseline, LocalTotals.StatsKind, "FishCaught", 251) == 51,
      "W2 since install = the game's counter now minus the baseline (14 - 9 = 5 weapons, 251 - 200 = 51 fish)");
Check(LocalTotals.Since(twinBase.Baseline, LocalTotals.StatsKind, "TamedPetting", 7) == 7 && LocalTotals.Since(twinBase.Baseline, LocalTotals.StatsKind, "Upgrades", 3) == 3,
      "W3 a stat the baseline does not hold (or held at 0) stood at 0: all of it is since install");
Check(LocalTotals.Since(twinBase.Baseline, LocalTotals.StatsKind, "CraftWeapon", 4) == 0, "W4 a counter that is lower than its baseline (a character swapped) never gives a negative number");
Check(LocalTotals.Since(twinBase.Baseline, LocalTotals.PickablesKind, "Barley", 50) == null && LocalTotals.Since(null, LocalTotals.StatsKind, "CraftWeapon", 5) == null && LocalTotals.Since(twinBase.Baseline, null, "x", 5) == null,
      "W5 no baseline of that kind (a fellow's copy, totals not loaded, a kind not taken yet): no since-install number, nothing guessed");
var twinBack = LocalTotals.FromJson(twinBase.ToJson(t2026));
Check(twinBack.BaselineAt[LocalTotals.StatsKind] == firstAt && LocalTotals.Since(twinBack.Baseline, LocalTotals.StatsKind, "CraftWeapon", 14) == 5 && !twinBack.Baseline[LocalTotals.StatsKind].ContainsKey("Upgrades"),
      "W6 the baseline and the day it was taken survive a save and a load (zeros are not stored)");
var olderFile = LocalTotals.FromJson("{\"version\":1,\"playerId\":5,\"saved\":\"2026-10-08T10:00:00Z\",\"totals\":{\"sessions\":0,\"previous\":{},\"lastSession\":{\"id\":\"\"}},\"baseline\":{\"pickedUp\":{\"$item_wood\":900}}}");
Check(olderFile != null && olderFile.Baseline["pickedUp"]["$item_wood"] == 900 && olderFile.BaselineAt.Count == 0,
      "W7 a file from before baselineAt loads: those kinds have no date (counted from the install)");
Check(new LocalTotals().ToJson(t2026).Contains("baselineAt") == false && twinBase.ToJson(t2026).Contains("\"baselineAt\":{\"stats\":\"2026-10-09T08:30:00.0000000Z\"}"),
      "W8 baselineAt is written only when a baseline has a date");
var twinKinds = LocalTotals.StatsTokens;
Check(twinKinds.Contains("CraftTrinket") && twinKinds.Contains("FishCaughtTier6") && twinKinds.Contains("TamedPetting") && !twinKinds.Contains("CreatureTamed") && twinKinds.Distinct().Count() == twinKinds.Length,
      "W9 the stats baseline holds the complete counters only (CreatureTamed is owner-only, left out), each once");

// O3: DamageTally skips a zero damage type before building its key; the totals are exactly those of adding every type (the earlier code, kept here as the reference)
{
    var refDealt = new System.Collections.Generic.Dictionary<string, float>(); var refTaken = new System.Collections.Generic.Dictionary<string, float>();
    void RefAdd(System.Collections.Generic.Dictionary<string, float> dd, string key, float v) { if (v == 0f) return; dd.TryGetValue(key, out var o); dd[key] = o + v; }
    void RefEach(HitData.DamageTypes t, System.Action<string, float> f)
    {
        f("blunt", t.m_blunt); f("slash", t.m_slash); f("pierce", t.m_pierce); f("chop", t.m_chop); f("pickaxe", t.m_pickaxe);
        f("fire", t.m_fire); f("frost", t.m_frost); f("lightning", t.m_lightning); f("poison", t.m_poison); f("spirit", t.m_spirit); f("damage", t.m_damage);
    }
    var rnd = new System.Random(11); var tl = new DamageTally(); int dealtHits = 0, takenHits = 0;
    string[] who = { "Troll", "Greydwarf", "Draugr", null, "Boar" }, how = { "Axes", "Swords", "EnemyHit", "Poisoned" };
    for (int i = 0; i < 20000; i++)
    {
        float R() => rnd.Next(3) == 0 ? (float)(rnd.Next(1, 400) / 8.0) : 0f;   // a third of the types carry damage
        var h = new HitData.DamageTypes { m_blunt = R(), m_slash = R(), m_pierce = R(), m_chop = R(), m_pickaxe = R(), m_fire = R(), m_frost = R(), m_lightning = R(), m_poison = R(), m_spirit = R(), m_damage = R() };
        var w3 = who[rnd.Next(who.Length)]; var c3 = how[rnd.Next(how.Length)];
        if (rnd.Next(2) == 0) { tl.AddDealt(w3, c3, h); dealtHits++; RefEach(h, (t, v) => RefAdd(refDealt, w3 + "|" + c3 + "|" + t, v)); }
        else { tl.AddTaken(w3, c3, h); takenHits++; RefEach(h, (t, v) => RefAdd(refTaken, w3 + "|" + c3 + "|" + t, v)); }
    }
    bool Same(System.Collections.Generic.Dictionary<string, float> a, System.Collections.Generic.Dictionary<string, float> b) => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);
    var zero = new DamageTally(); zero.AddDealt("Troll", "Axes", new HitData.DamageTypes());
    Check(Same(tl.Dealt, refDealt) && Same(tl.Taken, refTaken) && tl.HitsDealt == dealtHits && tl.HitsTaken == takenHits && tl.Dealt.Count > 50 && zero.HitsDealt == 1 && zero.Dealt.Count == 0,
          "O3 DamageTally sums are exactly the earlier code's over 20000 random hits (every type, zeros, a null name); an all-zero hit still counts as a hit and adds no key");
}

// O5: the per-biome tally is folded as hits come in (EventLog.Biome); it equals the pass over the whole log (BiomeTally.FromLog, the earlier code)
{
    bool SameTally(BiomeTally a, BiomeTally b, float tol) =>
        a.Damage.Count == b.Damage.Count && a.Deaths.Count == b.Deaths.Count &&
        a.Damage.All(kv => b.Damage.TryGetValue(kv.Key, out var v) && System.Math.Abs(v - kv.Value) <= tol * System.Math.Max(1f, System.Math.Abs(v))) &&
        a.Deaths.All(kv => b.Deaths.TryGetValue(kv.Key, out var v) && v == kv.Value);
    var rnd = new System.Random(5); var start = new System.DateTime(2026, 10, 9, 8, 0, 0, System.DateTimeKind.Utc);
    string[] biomes = { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", null, "Mistlands" }, foes = { "Boar", "Troll", "Draugr", "Wolf", "Blob", null }, causes = { "Axes", "EnemyHit", "Poisoned", "Bows" };
    var exactLog = new EventLog(); var looseLog = new EventLog(); int deaths = 0;
    // five hours of play (the log folds old minutes into ten-minute buckets on the way), 12000 hits of up to four damage types, 230 deaths (200 are listed)
    for (int i = 0; i < 12000; i++)
    {
        var at = start.AddSeconds(i * 1.5); var b = biomes[rnd.Next(biomes.Length)]; var f = foes[rnd.Next(foes.Length)]; var cz = causes[rnd.Next(causes.Length)]; bool dealt = rnd.Next(3) != 0;
        float E() => rnd.Next(2) == 0 ? rnd.Next(1, 300) / 8f : 0f;
        float L() => rnd.Next(2) == 0 ? (float)(rnd.NextDouble() * 90 + 0.05) : 0f;
        exactLog.AddDamage(at, b, dealt, f, cz, new HitData.DamageTypes { m_slash = E(), m_blunt = E(), m_fire = E(), m_poison = E() });
        looseLog.AddDamage(at, b, dealt, f, cz, new HitData.DamageTypes { m_slash = L(), m_pierce = L(), m_frost = L(), m_damage = L() });
        if (i % 52 == 7 && deaths < 230) { exactLog.AddDeath(at, b, 0, 0); looseLog.AddDeath(at, b, 0, 0); deaths++; }
    }
    Check(SameTally(exactLog.Biome, BiomeTally.FromLog(exactLog), 0f) && exactLog.Biome.Damage.Count > 40 && exactLog.Biome.Deaths.Values.Sum() == 200,
          "O5 the folded per-biome tally equals the whole-log pass exactly (5 h, folded buckets, null biome and foe, 230 deaths of which 200 are listed)");
    Check(SameTally(looseLog.Biome, BiomeTally.FromLog(looseLog), 1e-4f), "O5 and within rounding for arbitrary damage values (only the order of the float additions differs)");
    // more distinct heads than MaxBuckets in one minute: the overflow keys fold into other|other, the biome totals are unchanged
    var flood = new EventLog();
    for (int i = 0; i < EventLog.MaxBuckets + 500; i++) flood.AddDamage(start, "Swamp", true, "foe" + i, "Axes", new HitData.DamageTypes { m_slash = 2f });
    Check(flood.Hits.Count <= EventLog.MaxBuckets + 1 && SameTally(flood.Biome, BiomeTally.FromLog(flood), 0f) && flood.Biome.Damage["Swamp|dealt|slash"] == 2f * (EventLog.MaxBuckets + 500),
          "O5 hits beyond the bucket limit are still counted per biome");
    var inventing = new EventLog();
    for (int i = 0; i < 2000; i++) inventing.AddDamage(start, "Mod" + i, true, "x", "y", new HitData.DamageTypes { m_slash = 1f });
    Check(inventing.Biome.Damage.Count == BiomeTally.MaxKeys, "O5 the folded tally is bounded like the other: a mod that invents biomes stops at " + BiomeTally.MaxKeys + " keys");
    // the cached sum (what the panel reads every 2 s): the same as summing again, rebuilt only when something changed
    var cache = new BiomeTally.SumCache(); var earlier = BiomeTally.FromLog(exactLog); var running = new EventLog();
    var c0 = cache.Get(earlier, running.Biome); var c1 = cache.Get(earlier, running.Biome);
    running.AddDamage(start, "Swamp", true, "Draugr", "Axes", new HitData.DamageTypes { m_slash = 10f }); var c2 = cache.Get(earlier, running.Biome); var c3 = cache.Get(earlier, running.Biome);
    running.AddDeath(start, "Swamp", 0, 0); var c4 = cache.Get(earlier, running.Biome);
    var c5 = cache.Get(BiomeTally.FromLog(exactLog), running.Biome);   // another earlier tally (a reload): rebuilt
    Check(ReferenceEquals(c0, c1) && !ReferenceEquals(c1, c2) && ReferenceEquals(c2, c3) && !ReferenceEquals(c3, c4) && !ReferenceEquals(c4, c5) &&
          c0.Damage.Count == earlier.Damage.Count && c2.Damage["Swamp|dealt|slash"] == earlier.Damage["Swamp|dealt|slash"] + 10f && c4.Deaths["Swamp"] == earlier.Deaths["Swamp"] + 1f &&
          SameTally(c5, BiomeTally.Sum(BiomeTally.FromLog(exactLog), BiomeTally.FromLog(running)), 0f),
          "O5 the cached sum equals Sum(earlier, this session), is rebuilt on a new hit, a new death or a new earlier tally, and is the same object otherwise");
}

// ---------- 0.6: cargo carried and born in your care (src/Cargo.cs) ----------
{
    var im = new Dictionary<string, float>(); var st = new Dictionary<string, float>();
    var scrap550 = new Dictionary<string, int> { ["$item_ironscrap"] = 550, ["$item_wood"] = 40 };
    // one sample: 550 scrap and 40 wood moved 100 m = 55 000 + 4 000 item-metres; each item's stretch is the 100 m
    var added = Cargo.Add(im, st, scrap550, 100);
    Check(added == 59000 && im["$item_ironscrap"] == 55000f && im["$item_wood"] == 4000f && st["$item_ironscrap"] == 100f && st["$item_wood"] == 100f,
          "CG cargo arithmetic: amount aboard x metres moved, per item; the metres travelled with each item aboard beside it");
    // a second sample with less aboard: the sums add up, the average aboard is item-metres / stretch
    Cargo.Add(im, st, new Dictionary<string, int> { ["$item_ironscrap"] = 250 }, 100);
    Check(im["$item_ironscrap"] == 80000f && st["$item_ironscrap"] == 200f && Cargo.AverageAboard(im["$item_ironscrap"], st["$item_ironscrap"]) == 400 && st["$item_wood"] == 100f,
          "CG two samples: 550 then 250 aboard over 200 m is 400 aboard on average; wood, no longer aboard, stops adding");
    // nothing moved, a jump (teleport, a bottled ship), nothing aboard, a negative step, an empty or zero entry: nothing
    var before = im["$item_ironscrap"];
    var zero = Cargo.Add(im, st, scrap550, 0) + Cargo.Add(im, st, scrap550, Cargo.JumpMax) + Cargo.Add(im, st, scrap550, 5000) + Cargo.Add(im, st, scrap550, -3) +
               Cargo.Add(im, st, new Dictionary<string, int>(), 80) + Cargo.Add(im, st, new Dictionary<string, int> { [""] = 9, ["$item_x"] = 0, ["$item_y"] = -4 }, 80) + Cargo.Add(im, st, null, 80);
    Check(zero == 0 && im["$item_ironscrap"] == before && !im.ContainsKey("$item_x") && !im.ContainsKey("$item_y") && !im.ContainsKey(""),
          "CG no step, a jump of 250 m or more, nothing aboard, zero or negative amounts, no container: nothing is counted");
    Check(Cargo.Add(im, st, scrap550, Cargo.JumpMax - 1) > 0, "CG a step just under the jump limit still counts (a fast ship in ten seconds)");
    // bounded: at most MaxKinds kinds, however many mods add items; known kinds keep adding
    var bigIm = new Dictionary<string, float>(); var bigSt = new Dictionary<string, float>();
    var lots = new Dictionary<string, int>(); for (int i = 0; i < 400; i++) lots["$item_mod" + i] = 3;
    Cargo.Add(bigIm, bigSt, lots, 10);
    var firstKey = bigIm.Keys.First();
    Cargo.Add(bigIm, bigSt, new Dictionary<string, int> { [firstKey] = 1 }, 10);
    Check(bigIm.Count == Cargo.MaxKinds && bigSt.Count == Cargo.MaxKinds && bigIm[firstKey] == 40f, "CG bounded: " + Cargo.MaxKinds + " kinds at most (the saved file stays small); a kind already counted keeps adding");
    Check(Cargo.AverageAboard(0, 100) == 0 && Cargo.AverageAboard(5000, 0) == 0, "CG an average needs both item-metres and a distance (none: 0, never a division error)");

    // persistence: both tallies and the born tally survive the profile JSON, the local file and the folding of sessions
    var s1 = new SessionEvents(); s1.CargoMeters["$item_ironscrap"] = 55000f; s1.CargoStretch["$item_ironscrap"] = 100f; s1.BornInCare["Boar_piggy"] = 2f;
    var profile = new Json().Open(); s1.WriteTo(profile); profile.Close();
    Check(profile.ToString().Contains("\"cargoMeters\":{\"$item_ironscrap\":55000}") && profile.ToString().Contains("\"cargoStretch\"") && profile.ToString().Contains("\"bornInCare\":{\"Boar_piggy\":2}"),
          "CG the profile JSON carries cargoMeters, cargoStretch and bornInCare in the measured block, flat like cartMeters (the server stores it raw)");
    var read = new SessionEvents().ReadFrom((MiniJson.Parse(profile.ToString()) as Dictionary<string, object>)?["measuredThisSession"] as Dictionary<string, object>);
    Check(read.CargoMeters["$item_ironscrap"] == 55000f && read.CargoStretch["$item_ironscrap"] == 100f && read.BornInCare["Boar_piggy"] == 2f, "CG and reads back (a fellow's copy)");
    var s2 = new SessionEvents(); s2.CargoMeters["$item_ironscrap"] = 5000f; s2.CargoStretch["$item_ironscrap"] = 50f; s2.BornInCare["Boar_piggy"] = 1f; s2.BornInCare["Wolf_cub"] = 1f;
    var lt = new LocalTotals { PlayerId = 7 };
    lt.Record("A", null, s1); lt.Record("B", null, s2);   // a new session id folds the first in
    var backC = LocalTotals.FromJson(lt.ToJson(t2026));
    var since = backC.EventsBefore("C");
    Check(since.CargoMeters["$item_ironscrap"] == 60000f && since.CargoStretch["$item_ironscrap"] == 150f && since.BornInCare["Boar_piggy"] == 3f && since.BornInCare["Wolf_cub"] == 1f,
          "CG since install through the local file (same version 1, same rules): sessions fold, a re-saved session replaces and never adds twice");
    lt.Record("B", null, s2);
    Check(LocalTotals.FromJson(lt.ToJson(t2026)).EventsBefore("C").CargoMeters["$item_ironscrap"] == 60000f, "CG saving the same session again changes nothing");
    var older = LocalTotals.FromJson("{\"version\":1,\"playerId\":5,\"totals\":{\"sessions\":1,\"previous\":{\"damage\":{},\"measured\":{\"cartMeters\":{\"Cart\":300}}},\"lastSession\":{\"id\":\"x\",\"damage\":{},\"measured\":{}}}}");
    Check(older != null && older.EventsBefore("y").CartMeters["Cart"] == 300f && older.EventsBefore("y").CargoMeters.Count == 0, "CG a local file from before 0.6 (no cargo keys) loads and counts from zero");

    // born in your care: tamed, inside a birth, within 40 m; never the grow-up, never a wild one, never far away
    var born = new Dictionary<string, float>();
    Check(Cargo.CountBirth(born, "Boar_piggy", true, true, 12), "BC a tamed piglet born 12 m from you counts");
    Check(!Cargo.CountBirth(born, "Boar_piggy", true, false, 12) && born["Boar_piggy"] == 1f, "BC the same animal growing up (Growup.GrowUpdate, outside a birth) never counts");
    Check(!Cargo.CountBirth(born, "Boar_piggy", false, true, 12) && !Cargo.CountBirth(born, "Boar_piggy", true, true, 40.5) && born["Boar_piggy"] == 1f, "BC a wild newborn, or one more than 40 m away, never counts");
    Check(Cargo.CountBirth(born, "Wolf_cub", true, true, Cargo.CareRange) && born["Wolf_cub"] == 1f && !Cargo.CountBirth(born, "", true, true, 1) && !Cargo.CountBirth(null, "Wolf_cub", true, true, 1),
          "BC exactly 40 m counts; no name or no table counts nothing");
}

// ---------- Heavy Keel (CargoVoyage), Drover and Long Lead (LedTracker), the bests in the feats ledger ----------
{
    // metal and ore: ores, scrap and bars; not wood, stone, food or nails
    var haul = new Dictionary<string, int> { ["$item_ironscrap"] = 60, ["$item_iron"] = 30, ["$item_copperore"] = 15, ["$item_wood"] = 500, ["$item_stone"] = 80, ["$item_ironnails"] = 200, ["$item_blackmetal"] = 0 };
    Check(CargoVoyage.MetalOreCount(haul) == 105 && CargoVoyage.MetalOreCount(null) == 0 && CargoVoyage.MetalOreCount(new Dictionary<string, int>()) == 0,
          "HK metal and ore: scrap, ore and bars count (105 of 885 items), wood, stone and nails do not; nothing aboard: 0");

    // the best load: the biggest number carried for 2 km in all, in one voyage at the helm
    var shipA = new object(); var shipB = new object();
    var v = new CargoVoyage(); double t = 0;
    v.Step(t, shipA, 0, 0);                                   // the first sample at the helm: no step yet
    for (int k = 0; k < 10; k++) { t += 10; v.Step(t, shipA, 120, 100); }
    Check(v.Active && v.Best == 0 && System.Math.Abs(v.LoadedMetres - 1000) < 1e-9, "HK 120 items over 1 000 m is not 2 km yet: best 0");
    for (int k = 0; k < 10; k++) { t += 10; v.Step(t, shipA, 120, 100); }
    Check(v.Best == 120, "HK 120 items over 2 000 m: best 120");
    var mix = new CargoVoyage(); t = 0; mix.Step(t, shipA, 0, 0);
    for (int k = 0; k < 15; k++) { t += 10; mix.Step(t, shipA, 150, 100); }   // 1 500 m with 150 aboard
    for (int k = 0; k < 5; k++) { t += 10; mix.Step(t, shipA, 100, 100); }    // 500 m with 100
    for (int k = 0; k < 6; k++) { t += 10; mix.Step(t, shipA, 50, 100); }     // 600 m with 50
    Check(mix.Best == 100, "HK the best is the biggest load carried for 2 km in all: 1 500 m with 150 plus 500 m with 100 is 2 000 m with 100 or more, so 100");
    var short1 = new CargoVoyage(); t = 0; short1.Step(t, shipA, 0, 0);
    for (int k = 0; k < 19; k++) { t += 10; short1.Step(t, shipA, 400, 100); }
    Check(short1.Best == 0, "HK 400 items over 1 900 m: no load has been carried 2 km yet");

    // steps that do not count: no movement, a jump, nothing aboard, an unreadable box (load 0)
    var odd = new CargoVoyage(); t = 0; odd.Step(t, shipA, 0, 0);
    odd.Step(t += 10, shipA, 300, 0); odd.Step(t += 10, shipA, 300, Cargo.JumpMax); odd.Step(t += 10, shipA, 300, 5000); odd.Step(t += 10, shipA, 300, -50); odd.Step(t += 10, shipA, 0, 100);
    Check(odd.LoadedMetres == 0 && odd.Best == 0 && odd.Active, "HK no movement, a jump of 250 m or more, a negative step, or nothing aboard adds nothing (the voyage goes on)");

    // a voyage: pauses off the helm, ends after more than 5 minutes away, or on another ship
    var trip = new CargoVoyage(); t = 0; trip.Step(t, shipA, 0, 0);
    for (int k = 0; k < 20; k++) { t += 10; trip.Step(t, shipA, 200, 100); }
    trip.Step(t + 200, null, 0, 0);                                           // away 200 s: docked to unload
    Check(trip.Active && trip.Best == 200, "HK left the helm for 200 s (under 5 minutes): the voyage is still on");
    trip.Step(t + 250, shipA, 200, 0);                                         // back at the helm, first sample after: no step
    for (int k = 0; k < 20; k++) { trip.Step(t + 260 + k * 10, shipA, 150, 100); }
    Check(trip.Best == 200 && System.Math.Abs(trip.LoadedMetres - 4000) < 1e-9, "HK back at the helm within 5 minutes it is the same voyage: the metres go on adding (4 000 m with some aboard)");
    trip.Step(t + 1000, null, 0, 0);                                          // away more than 5 minutes
    Check(!trip.Active && trip.Best == 0 && trip.LoadedMetres == 0, "HK away from the helm for more than 5 minutes ends the voyage");
    var swap = new CargoVoyage(); t = 0; swap.Step(t, shipA, 0, 0);
    for (int k = 0; k < 20; k++) { t += 10; swap.Step(t, shipA, 200, 100); }
    swap.Step(t + 10, shipB, 90, 0);
    Check(swap.Best == 0 && swap.Active, "HK another ship is another voyage (a reconnect gives a new ship object too)");
    var hk_late = new CargoVoyage(); hk_late.Step(0, shipA, 0, 0); for (int k = 1; k <= 20; k++) hk_late.Step(k * 10, shipA, 200, 100);
    hk_late.Step(200 + 400, shipA, 200, 100);   // back at the same helm after 400 s away without a sample in between
    Check(hk_late.Best == 0 && hk_late.LoadedMetres == 0, "HK a gap of more than 5 minutes between samples at the helm starts a new voyage");
    var hk_capped = new CargoVoyage(); hk_capped.Step(0, shipA, 0, 0);
    for (int k = 1; k <= CargoVoyage.MaxLoads + 50; k++) hk_capped.Step(k * 10, shipA, k, 10);
    Check(hk_capped.Active, "HK bounded: more different loads than the cap are ignored, not an error");

    // the ledger keeps the best with its day and biome, only when it beats the one before
    var led1 = new FeatsLedger { Primed = true };
    Check(led1.NoteBest(CargoVoyage.BestKey, 110, t2026, "Ocean") && !led1.NoteBest(CargoVoyage.BestKey, 110, t2026.AddDays(1), "Mistlands") && !led1.NoteBest(CargoVoyage.BestKey, 90, t2026.AddDays(2), "Swamp") &&
          led1.NoteBest(CargoVoyage.BestKey, 140, t2026.AddDays(3), "Mountain") && led1.Count(CargoVoyage.BestKey) == 140 && led1.Bests[CargoVoyage.BestKey].Biome == "Mountain" && led1.Bests[CargoVoyage.BestKey].Utc == t2026.AddDays(3),
          "HK the ledger keeps the best load (the number in the counters, the day and biome beside it) only when it beats the one before");
    led1.NoteBest(LedTracker.BestKey, 2300, t2026, "Meadows", "Wolf");
    var tot1 = new LocalTotals { PlayerId = 12, Feats = led1 };
    var back1 = LocalTotals.FromJson(tot1.ToJson(t2026));
    Check(back1.Feats.Count(CargoVoyage.BestKey) == 140 && back1.Feats.Bests[CargoVoyage.BestKey].Value == 140 && back1.Feats.Bests[CargoVoyage.BestKey].Utc == t2026.AddDays(3) &&
          back1.Feats.Bests[LedTracker.BestKey].What == "Wolf" && back1.Feats.Bests[LedTracker.BestKey].Biome == "Meadows",
          "HK the bests survive the local file (day, biome, animal)");
    var sharedJson = new Json().Open(); led1.WriteSharedBests(sharedJson); sharedJson.Close();
    var sharedRoot = MiniJson.Parse(sharedJson.ToString()) as Dictionary<string, object>;
    var hk_theirs = new FeatsLedger(); hk_theirs.ReadSharedBests(MiniJson.Obj(sharedRoot, "featBests"));
    Check(hk_theirs.BestsShared && hk_theirs.Bests[CargoVoyage.BestKey].Value == 140 && hk_theirs.Bests[LedTracker.BestKey].What == "Wolf" && hk_theirs.Counts.Count == 0 && !sharedJson.ToString().Contains("place"),
          "HK shared in the snapshot as featBests: the number, day, biome and animal, never a place and no counters");
    var emptyJson = new Json().Open(); new FeatsLedger().WriteSharedBests(emptyJson); emptyJson.Close();
    var emptyShared = new FeatsLedger(); emptyShared.ReadSharedBests(MiniJson.Obj(MiniJson.Parse(emptyJson.ToString()) as Dictionary<string, object>, "featBests"));
    Check(emptyJson.ToString() == "{\"featBests\":{}}" && emptyShared.BestsShared && emptyShared.Bests.Count == 0, "HK a sender with no best yet still says it shares them (an empty featBests), so a fellow is not told it is not counted");
    Check(LocalTotals.FromJson(new LocalTotals { PlayerId = 11, Feats = new FeatsLedger { Primed = true } }.ToJson(t2026)).Feats.Bests.Count == 0 && !new LocalTotals().ToJson(t2026).Contains("bests"),
          "HK an older file (no bests) reads as none; an empty ledger writes no bests key");
    var hk_many = new FeatsLedger();
    for (int k = 0; k < 20; k++) hk_many.NoteBest("best" + k, 1, t2026, null);
    Check(hk_many.Bests.Count == FeatsLedger.MaxBests && hk_many.Counts.Count <= FeatsLedger.MaxCounts, "HK bounded: the bests stay under their cap (" + hk_many.Bests.Count + " of " + FeatsLedger.MaxBests + ")");

    // ---------- Drover / Long Lead ----------
    LedTracker.Follower F(string id, string kind, float x, float z = 0f) => new LedTracker.Follower { Id = id, Kind = kind, X = x, Y = 0, Z = z };
    var tallies = new Dictionary<string, float>(); var lead = new LedTracker(); double ts = 0;
    lead.Sample(ts, new[] { F("w1", "Wolf", 0) }, tallies);
    for (int k = 1; k <= 3; k++) lead.Sample(ts += 10, new[] { F("w1", "Wolf", 50f * k) }, tallies);
    Check(tallies["Wolf"] == 150f && lead.BestMetres == 150 && lead.BestKind == "Wolf" && lead.Total == 150, "LD a wolf that follows 50 m per sample for three samples: 150 m led, its longest lead 150 m");
    // the first sight of an animal counts nothing (no earlier place); a second animal adds to the sum over animals
    lead.Sample(ts += 10, new[] { F("w1", "Wolf", 200f), F("b1", "Boar", 10f) }, tallies);
    lead.Sample(ts += 10, new[] { F("w1", "Wolf", 250f), F("b1", "Boar", 70f) }, tallies);
    Check(tallies["Wolf"] == 250f && tallies["Boar"] == 60f && lead.Total == 310 && lead.Leading == 2, "LD the first sight counts nothing; two animals led at once add up (a wolf 250 m, a boar 60 m: 310 m, the sum over animals)");
    // a jump over 100 m is skipped, the lead goes on
    lead.Sample(ts += 10, new[] { F("w1", "Wolf", 250f + 150f), F("b1", "Boar", 100f) }, tallies);
    lead.Sample(ts += 10, new[] { F("w1", "Wolf", 250f + 150f + 40f), F("b1", "Boar", 130f) }, tallies);
    Check(tallies["Wolf"] == 290f && tallies["Boar"] == 120f && lead.BestMetres == 290, "LD a step over 100 m (a teleport) counts for nothing; the lead goes on from where the animal is");
    Check(LedTracker.StepMax == 100f, "LD the teleport guard is 100 m per sample");
    // exactly 100 m counts; not seen following for 60 s is still the same lead, over 60 s ends it
    var l2 = new LedTracker(); var t2 = new Dictionary<string, float>(); double s2 = 0;
    l2.Sample(s2, new[] { F("a", "Lox", 0) }, t2); l2.Sample(s2 += 10, new[] { F("a", "Lox", 100f) }, t2);
    Check(t2["Lox"] == 100f, "LD a step of exactly 100 m counts");
    l2.Sample(s2 += 10, new LedTracker.Follower[0], t2); l2.Sample(s2 += 10, new LedTracker.Follower[0], t2);
    l2.Sample(s2 += 40, new[] { F("a", "Lox", 130f) }, t2);   // 60 s after it was last seen: the lead goes on
    Check(t2["Lox"] == 130f && l2.BestMetres == 130, "LD not seen for exactly 60 s: still the same lead");
    l2.Sample(s2 += 10, new LedTracker.Follower[0], t2);
    l2.Sample(s2 += 61, new[] { F("a", "Lox", 400f) }, t2);   // 61 s without following: told to stay or lost; a new lead
    Check(t2["Lox"] == 130f && l2.Leading == 1, "LD not seen following for more than 60 s ends the lead: the walk in between counts nothing");
    l2.Sample(s2 += 10, new[] { F("a", "Lox", 420f) }, t2);
    Check(t2["Lox"] == 150f && l2.BestMetres == 130 && l2.BestKind == "Lox", "LD the new lead starts again at 0 (20 m); the longest lead stays 130 m");
    // the longest lead is one animal's continuous follow, not the sum over animals
    var l3 = new LedTracker(); var t3 = new Dictionary<string, float>(); double s3 = 0;
    l3.Sample(s3, new[] { F("w", "Wolf", 0), F("b", "Boar", 0) }, t3);
    for (int k = 1; k <= 10; k++) l3.Sample(s3 += 10, new[] { F("w", "Wolf", 60f * k), F("b", "Boar", 40f * k) }, t3);
    Check(l3.BestMetres == 600 && l3.BestKind == "Wolf" && l3.Total == 1000, "LD the longest lead is one animal's (the wolf, 600 m), the total is the sum over animals (1 000 m)");
    // never a ghost: no animals, no followers
    var l4 = new LedTracker(); var t4 = new Dictionary<string, float>();
    Check(l4.Sample(0, null, t4) == 0 && l4.Sample(10, new LedTracker.Follower[0], t4) == 0 && t4.Count == 0 && l4.BestMetres == 0 &&
          l4.Sample(20, new[] { F("", "Wolf", 0), F("x", "", 0) }, t4) == 0 && l4.Leading == 0, "LD no followers, no id or no kind: nothing counted");
    // bounded: animals tracked, kinds kept
    var l5 = new LedTracker(); var t5 = new Dictionary<string, float>();
    var herd = Enumerable.Range(0, 200).Select(i => F("id" + i, "Kind" + i, 0)).ToArray();
    l5.Sample(0, herd, t5);
    Check(l5.Leading == LedTracker.MaxStints, "LD bounded: at most " + LedTracker.MaxStints + " animals tracked at once");
    var herd2 = Enumerable.Range(0, 200).Select(i => F("id" + i, "Kind" + i, 10)).ToArray();
    for (int k = 0; k < 4; k++) l5.Sample(10, herd2, t5);
    Check(t5.Count <= Cargo.MaxKinds, "LD bounded: at most " + Cargo.MaxKinds + " kinds in the tally");
    // the tally travels: profile JSON (flat, like cartMeters), local file, folding of sessions
    var sl1 = new SessionEvents(); sl1.LedMeters["Wolf"] = 3900f; var prof = new Json().Open(); sl1.WriteTo(prof); prof.Close();
    var readL = new SessionEvents().ReadFrom((MiniJson.Parse(prof.ToString()) as Dictionary<string, object>)?["measuredThisSession"] as Dictionary<string, object>);
    Check(prof.ToString().Contains("\"ledMeters\":{\"Wolf\":3900}") && readL.LedMeters["Wolf"] == 3900f, "LD the led metres are in the profile JSON as ledMeters and read back (a fellow's copy)");
    var sl2 = new SessionEvents(); sl2.LedMeters["Wolf"] = 1100f; sl2.LedMeters["Boar"] = 500f;
    var ltL = new LocalTotals { PlayerId = 8 }; ltL.Record("A", null, sl1); ltL.Record("B", null, sl2);
    var sinceL = LocalTotals.FromJson(ltL.ToJson(t2026)).EventsBefore("C");
    Check(sinceL.LedMeters["Wolf"] == 5000f && sinceL.LedMeters["Boar"] == 500f, "LD since install through the local file: sessions fold, nothing counts twice");
}

fails += ServerBookTests.Run();   // 0.6: the server book (cargo loaded and unloaded, born near) and the group answer (test/ServerBookTests.cs)
fails += FellowTests.Run();   // fellows by platform id, same-name players, old and new peers, the host as the server (test/FellowTests.cs)
fails += LocalFileTests.Run();
fails += HistoryTests.Run();   // the day history (HISTORY-06.md): recording, live part, folding, size, file, shared per day   // 0.6.1: local-file resilience (NaN, atomic writes + .bak, one file per character, unknown keys, newer formats)
fails += ServerResilienceTests.Run();   // RESILIENCE-06 server items: atomic files, input limits, the book per world, old logs compressed (test/ServerResilienceTests.cs)
fails += HookGuardTests.Run();   // RESILIENCE-06 item 6: one guard for every Harmony hook, checked on the IL of each (test/HookGuardTests.cs)

System.Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
return fails;
