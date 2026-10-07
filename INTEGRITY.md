# KritterStats integrity rules: nothing counted twice, nothing missed

Joost's ask (2026-10-07): every number must be honest. Each rule below names its test: `test/Program.cs` (unit, real
game types) and `selftest/SelfTest.cs` (runtime on the Docker test server with all production mods).

Principle: **we only read.** KritterStats never changes, blocks or replaces what another mod does. Where another mod
changes how things work (MultiUserChest, craft-from-chest mods), we read the messages it already sends and fall back
to the plain rule when it is absent.

## Chests, carts, ships, tombstones (server, no player mod needed)

| # | Rule | Why | Test |
|---|---|---|---|
| C1 | **What** changed = the server's own previous copy vs the new copy of the item list, read in `ZDO.Deserialize` (prefix keeps the old copy, postfix compares). | 0.1.0 remembered the first sighting after every server start and so **missed the first change to every chest after each restart**. The server's copy comes from the world save, so nothing is missed. | unit C1, runtime C1 |
| C2 | An update that arrives twice changes nothing and counts nothing. Unchanged bytes are skipped without parsing. | Revisions and resends must never double-count. | unit C2, runtime C2 |
| C3 | Items are counted per prefab + maker + quality; stacking, splitting and moving inside a chest are no events. | Sorting a chest is not a contribution. | unit C3 |
| C4 | Quality is part of the item: swapping a quality 1 for a quality 3 sword is a take and a put. | 0.1.0 saw no change. | unit C4 |
| C5 | A container nobody built (dungeon, camp, world chest) that gets its first item list = `loot-spawned`, never a `put`. | The game generates loot when the first player opens it; that is not a gift from the opener. | unit C5, runtime C5 |
| C6 | A tombstone is `grave-drop` / `grave-take`, never a chest gift. | Dying is not stocking a chest. | unit C6, runtime C6 |
| C7 | A player-built container seen for the first time (new, or never opened before) counts as `put`. | Its empty start is real. | unit C7 |
| C8 | **Who**, two players in one chest (MultiUserChest): the add/remove/consume/drop requests that pass the server name the mover, item and amount; they are matched against the diff. The manager's **answers** pass the server too: a failed request is removed, a partial one gets the real amount, and a drop onto an occupied slot books the item that came out as the friend's take (swap). Unanswered requests expire after 15 s. A maker name without a crafter id is ignored, as the game does. | MUC lets several players use one chest; its manager's PC sends the combined change. | unit C8, C8b, C8c, swap, failed add/remove, maker; runtime C8 |
| C9 | **Who**, otherwise: the PC that sent the change (`RPC_ZDOData` sender). Plain Valheim allows one player per chest, so that is exact. If the sender is not the chest's manager (craft-from-chest mods), the line says `sender-not-owner`; if nobody had the chest open (the game's `InUse` flag) it says `unattended`. A chest a mod empties by itself (SmelterUpgrades' Stoker's chest, `*feeder*`) logs those takes as `auto-feed`, never as a player's take. | AzuCraftyBoxes pulls from chests it does not own; the Stoker's chest runs on whoever owns that area (verification F1, 4 in the world). | unit C9 (x2) |
| C10 | Smelter/kiln outputs drop on the ground and are not chest events. The cargo of a sunk ship or broken cart goes into a `CargoCrate`: that is `salvage-drop` / `salvage-take`, never stocking. | Verification F4 (2 crates in the world). | unit C10 |
| C11 | An item list in an old format (before item version 108, still loadable by the game) is read as unknown: its first change counts nothing instead of the whole chest. | Verification F6 (latent: the world is all 109 today). | unit C11 |
| C12 | The log carries markers: `world-saved` and `server-start`. Changes after the last save that a restart without saving rolled back are dropped by the companion; changes after the newest save are marked provisional. A removed object revived by a late update is ignored. | A stop does not save (AGENTS.md); verification F5, F7. | `test_kstats.py` C12 |

Every line in `chests-<date>.jsonl` carries `via` (`sender`, `shared-chest`, `sender-not-owner`) so a reader can see how
sure the attribution is. A server with a different shared-chest mod than MUC falls back to C9.

## Player data (client sends, server stores)

| # | Rule | Why | Test |
|---|---|---|---|
| P1 | The character's own counters (`profile`): absolute snapshots of stat slot 0 only, latest wins. | Other slots overlap and double-count (DudeWhatAreMyStats lesson). | unit "only slot 0" |
| P2 | Measured values are absolute **per session**; the server keeps the latest copy of every session in `players/<id>/sessions/<session>.json`. **Measured total = sum over sessions of each session's latest copy.** | 0.1.0 kept only `players/<id>.json`, so a new session overwrote the measured values of the previous one. | unit P2 |
| P3 | A session = one connection: tallies reset and a new session id starts when a connection starts (`ZNet.Awake`), not in `Logout` (which can be cancelled and is not called when the link drops). | Verification F13. | code review (`Plugin.NewConnection`) |
| P4 | The event log stores damage in 10-minute UTC buckets per biome, enemy, cause and type, plus each death (time, biome, killer, cause, the last 10 s of damage). Burning and poison ticks are credited to whoever set you on fire or poisoned you in the last 30 s. | Joost's per-biome page with time filter; "which potion next time". | unit P4 |
| P5 | Limits: a crash or lost link loses at most the last interval (5 min default) of measured data; the log caps at 4000 buckets (overflow to an `other` bucket) and 200 listed deaths (`deathsNotListed` counts the rest); a snapshot over 200 parts is rejected with a warning in the server log. Nothing is dropped silently. | Verification F9. | unit P5 |
| P6 | Kills: `enemyKills` table 0 is the total; the other tables repeat the same kills per modifier. Only table 0 may be summed. Feast bites count as feasts only (not also as food made by "unknown"). Parries follow the game's own rule. | Verification F8, F11, F12. | companion reads table 0; code review |

Estimates are never mixed in: retroactive numbers (world save, backups) stay in the companion and are labelled
`estimate`; see README design rules.

## Non-enforced install (nobody gets kicked)

| # | Rule | Evidence |
|---|---|---|
| N1 | No reference to Jotunn or ServerSync. Jotunn's version check only enumerates plugins that depend on Jotunn (`ModCompatibility.GetEnforcableMods` -> `GetDependentPlugins`, decompiled Jotunn on the test server); ServerSync only checks the mod that embeds it. | unit N1 |
| N2 | No `BepInDependency`, no `NetworkCompatibility` attribute. | unit N2 |
| N3 | The server never sends anything unasked: its only message to a client is the reply to that client's own group request (GroupShare), so a client without the mod receives nothing. | code review (`GroupShare.OnRequest` is the only server send) |
| N4 | A client with the mod on a server without it: its message is an unknown routed RPC, ignored without error. | runtime "unknown RPC" |

## Seeing each other (reciprocal sharing, Joost 2026-10-07)

| # | Rule | Test |
|---|---|---|
| S1 | One choice, `Client.ShareWithGroup`, ON by default: "Do you want to see other players' stats? Then you'll share yours as well." The snapshot carries `share`. | unit S1 |
| S2 | Others see the same snapshot without death positions (biome stays) and without the list of worlds you played. Web pages strip positions too. | unit S2 (x2) |
| S3 | The server keeps `players/<id>.share.json` only while that player shares and serves the group only to a requester whose own share file exists. Switching off is its own message (`ShareState`, on every spawn and on change), so it works with SendStats off; copies not refreshed for 14 days are no longer served (covers uninstalling). Each answer starts with the current list of sharers, so a player who stopped sharing disappears from everyone's panel. One answer per player at a time, at most every 20 s, a queue cap of 600 parts, one part per 100 ms. The reader is linear and refuses nesting deeper than 64. | Verification G1-G5. | unit G3, G4; runtime pending |
| S4 | The web page of a player who does not share shows only their own data, with a line on how to turn sharing on. | `test_kstats.py`, screenshot |

What only a real client can prove: the client hooks firing in real play, and two different game installs. First
evening on the server with Joost's go = field test.

- **2026-10-07** — Independent verification (`VERIFY-INTEGRITY.md` in the session scratch: C8, C9, C10, S3 broken; 8 counterexamples reproduced). Fixed: MUC answers and swaps, feeder/unattended, cargo crates, old formats (C11), save/restart markers (C12), session reset per connection, silent drops, feast double count, unshare message, sharer list, linear and depth-limited reader, queue cap, worlds stripped. Every counterexample is now a unit test. Open: the sender lookup through the real network path (`RPC_ZDOData` with a live peer) needs a real client; forged MUC claims or group requests from a modified client (friends server: accepted risk); names as keys in the group list (two characters with one name).
- **2026-10-07** — S1-S4 reciprocal sharing; N3 reworded (server answers only when asked).
- **2026-10-07** — Rules C1-C10, P1-P5, N1-N4 written with tests (unit 36/36, runtime 14/14 on the test server with all production mods, Valheim 1.0.17). 0.2.0.
