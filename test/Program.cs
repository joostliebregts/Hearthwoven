// Unit tests for the pure parts of Hearthwoven: snapshot JSON from the game's own PlayerStats, transport, damage tally,
// and the routed-damage parser (game-serialized hit, read back).
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
Check(EventLog.Bucket(t0) == "2026-10-07T20:10Z", "P4 10-minute time buckets in UTC");
Check(log.Damage["2026-10-07T20:10Z|Swamp|taken|BlobElite|Poisoned|poison"] == 25f && log.Damage["2026-10-07T20:10Z|Swamp|dealt|Draugr|Axes|slash"] == 40f, "P4 damage per time, biome, enemy, cause and type");
Check(death.Killer == "BlobElite" && death.Cause == "Poisoned" && death.Last10s["BlobElite|Poisoned|poison"] == 25f && death.Biome == "Swamp", "P4 a death names biome, killer, cause and the last 10 seconds");
var js3 = Snapshot.Build("0.2.0", 1, "Edda", stats, new Snapshot.SkillInfo[0], "w", null, "s1", new SessionEvents(), log);
var ml = JsonDocument.Parse(js3).RootElement.GetProperty("measuredLog");
Check(ml.GetProperty("deaths")[0].GetProperty("killer").GetString() == "BlobElite" && ml.GetProperty("bucketMinutes").GetInt32() == 10, "P4 event log travels in the snapshot");
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

System.Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
return fails;
