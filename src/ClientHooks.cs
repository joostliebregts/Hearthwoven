using System;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// Client-side measuring points. Each one only reads, never changes a result, and only counts the local player.
    /// Hook choices from the decompile study (audits/kritterstats-2026-10-07/GAME-METRICS.md).
    /// </summary>
    static class ClientHooks
    {
        static bool Local(Character c) => c != null && c == Player.m_localPlayer;
        static string Prefab(Component c) => c == null ? "?" : Utils.GetPrefabName(c.gameObject);
        static void Safe(Action a) => HookGuard.Run(a);   // RESILIENCE-06 item 6: one guard for every hook

        // Where the local player is: the biome under their feet, looked up at most once a second.
        static string biome = "None"; static float biomeAt = -10f;
        internal static string Biome()
        {
            var p = Player.m_localPlayer;
            if (p != null && Time.time - biomeAt > 1f) { biome = Heightmap.FindBiome(p.transform.position).ToString(); biomeAt = Time.time; }
            return biome;
        }

        // Burning and poison ticks carry no attacker; remember who set you on fire or poisoned you (per damage type, 30 s).
        static readonly System.Collections.Generic.Dictionary<string, string> dotSource = new System.Collections.Generic.Dictionary<string, string>();
        static readonly System.Collections.Generic.Dictionary<string, float> dotAt = new System.Collections.Generic.Dictionary<string, float>();
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        static class DotSource
        {
            static void Prefix(Character __instance, HitData hit) => Safe(() =>
            {
                if (hit == null || !Local(__instance)) return;
                var a = hit.GetAttacker(); if (a == null) return;
                var who = Prefab(a);
                if (hit.m_damage.m_fire > 0f || hit.m_damage.m_spirit > 0f) { dotSource["Burning"] = who; dotAt["Burning"] = Time.time; }
                if (hit.m_damage.m_poison > 0f) { dotSource["Poisoned"] = who; dotAt["Poisoned"] = Time.time; }
            });
        }
        static string Source(HitData hit)
        {
            var attacker = hit.GetAttacker();
            if (attacker != null) return Prefab(attacker);
            var cause = hit.m_hitType.ToString();
            return dotSource.TryGetValue(cause, out var s) && Time.time - dotAt[cause] < 30f ? s : cause;
        }

        // Damage you deal: runs on the attacker's PC for every hit, before the target's resistances.
        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        static class DamageDealt
        {
            static void Prefix(Character __instance, HitData hit) => Safe(() =>
            {
                if (hit != null && Local(hit.GetAttacker()) && !Local(__instance))
                {
                    Plugin.Session.AddDealt(Prefab(__instance), hit.m_skill.ToString(), hit.m_damage);
                    // every hit, also on a foe another PC owns (the game's EnemyHits/PlayerHits miss those: owner trap)
                    SessionEvents.Add(Plugin.Events.Battle, __instance is Player ? "PlayerHits" : "EnemyHits");
                    Plugin.Log.AddDamage(DateTime.UtcNow, Biome(), true, Prefab(__instance), hit.m_skill.ToString(), hit.m_damage);
                }
            });
        }

        // Damage you take: runs on your own PC after resistances and armour.
        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        static class DamageTaken
        {
            static void Postfix(Character __instance, HitData hit) => Safe(() =>
            {
                if (hit == null || !Local(__instance)) return;
                var source = Source(hit);
                Plugin.Session.AddTaken(source, hit.m_hitType.ToString(), hit.m_damage);
                Plugin.Log.AddDamage(DateTime.UtcNow, Biome(), false, source, hit.m_hitType.ToString(), hit.m_damage);
            });
        }

        // Deaths: when, where, and what hit you in the last 10 seconds.
        [HarmonyPatch(typeof(Player), "OnDeath")]
        static class Died
        {
            static void Prefix(Player __instance) => Safe(() =>
            {
                if (!Local(__instance)) return;
                var pos = __instance.transform.position;
                Plugin.Log.AddDeath(DateTime.UtcNow, Biome(), pos.x, pos.z);
                SessionEvents.Add(Plugin.Events.Battle, "Deaths");   // since install (LocalTotals), under the game's count at first run
            });
        }

        // Blocks and parries, by the game's own rule (Humanoid.BlockAttack): a parry needs a shield that can parry
        // (m_timedBlockBonus > 1, so tower shields never do), a raise within 0.25 s, and a block that held (not staggered).
        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        static class Block
        {
            static readonly GameField<Humanoid, float> timer = new GameField<Humanoid, float>("m_blockTimer");   // looked up on first use, never at type load
            static readonly System.Reflection.MethodInfo getBlocker = AccessTools.Method(typeof(Humanoid), "GetCurrentBlocker");
            static void Prefix(Humanoid __instance, out bool __state)
            {
                __state = false;
                try
                {
                    if (!Local(__instance)) return;
                    var blocker = getBlocker?.Invoke(__instance, null) as ItemDrop.ItemData;
                    float t = timer.Of(__instance);
                    __state = blocker != null && blocker.m_shared.m_timedBlockBonus > 1f && t != -1f && t < 0.25f;
                }
                catch (Exception e) { __state = false; HookGuard.Fail(e, "ClientHooks.Block.Prefix"); }   // the block itself goes on as the game decides
            }
            static void Postfix(Humanoid __instance, bool __result, bool __state) => Safe(() =>
            {
                if (!__result || !Local(__instance) || __instance.IsStaggering()) return;   // guard broken: not a held block
                Plugin.Events.Blocks++;
                if (__state) Plugin.Events.Parries++;
            });
        }

        // Mining: your pickaxe hits per rock type (the profile's own mining counters only count for the area owner).
        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
        static class Mine5 { static void Prefix(MineRock5 __instance, HitData hit) => Pick(__instance, hit); }
        [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
        static class Mine { static void Prefix(MineRock __instance, HitData hit) => Pick(__instance, hit); }
        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        static class Destr { static void Prefix(Destructible __instance, HitData hit) => Pick(__instance, hit); }
        static void Pick(Component rock, HitData hit) => Safe(() =>
        {
            if (hit != null && hit.m_damage.m_pickaxe > 0f && Local(hit.GetAttacker()))
                SessionEvents.Add(Plugin.Events.PickaxeHits, Prefab(rock));
        });

        // Woodcutting: your axe hits per tree and log (the profile's own tree counters only count for the area owner).
        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
        static class ChopTree { static void Prefix(TreeBase __instance, HitData hit) { Chop(__instance, hit); Felling(__instance, hit); } }
        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
        static class ChopLog { static void Prefix(TreeLog __instance, HitData hit) => Chop(__instance, hit); }
        static void Chop(Component tree, HitData hit) => Safe(() =>
        {
            if (hit != null && hit.m_damage.m_chop > 0f && Local(hit.GetAttacker()))
                SessionEvents.Add(Plugin.Events.ChopHits, Prefab(tree));
        });

        // Trees you felled, wherever the area is hosted (the game's own counter only books trees felled where your PC hosts
        // the area). One rule, in TreeFalls: your axe hit that should bring the tree down by the game's own sum (its damage
        // after the tree's resistances, a good enough tool, at least the health left), then that tree's network object
        // destroyed within 5 s. The destroy reaches every PC in the area; walking away only unloads a tree, never counts.
        static readonly TreeFalls<ZDOID> falls = new TreeFalls<ZDOID>();
        static void Felling(TreeBase tree, HitData hit) => Safe(() =>
        {
            if (hit == null || hit.m_damage.m_chop <= 0f || !Local(hit.GetAttacker())) return;
            var zdo = tree.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null || !hit.CheckToolTier(tree.m_minToolTier, true)) return;
            var probe = hit.Clone(); probe.ApplyResistance(tree.m_damageModifiers, out _);   // a copy: the hit itself stays as it is
            falls.Hit(zdo.m_uid, Prefab(tree), zdo.GetFloat(ZDOVars.s_health, tree.m_health), probe.GetTotalDamage(), Time.time);
        });
        [HarmonyPatch(typeof(ZDOMan), "HandleDestroyedZDO")]
        static class TreeGone
        {
            static void Prefix(ZDOID uid) => Safe(() =>
            {
                var tree = falls.Destroyed(uid, Time.time);
                if (tree != null) SessionEvents.Add(Plugin.Events.Felled, tree);
            });
        }

        // Farming: every plant you put in the ground, counted per plant. Piece.SetCreator runs for each placed piece (the
        // game's Player.PlacePiece, and mods that plant many at once such as PlantEasily's grid); it only sets a creator that
        // was still empty, on the piece's owner. The game's own planted counter books one per TryPlacePiece, so a grid of 171
        // plants can show as 1 there (SOURCES.md, live doubt 1).
        [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
        static class Planting
        {
            static void Prefix(Piece __instance, out long __state) { __state = -1; try { if (__instance != null) __state = __instance.GetCreator(); } catch (Exception e) { __state = -1; HookGuard.Fail(e, "ClientHooks.Planting.Prefix"); } }
            static void Postfix(Piece __instance, long __state) => Safe(() =>
            {
                if (__instance == null || __state < 0 || Player.m_localPlayer == null || Game.instance == null) return;
                var you = Game.instance.GetPlayerProfile()?.GetPlayerID() ?? 0L;
                SessionEvents.CountPlanting(Plugin.Events.Planted, __instance.m_name, __instance.GetComponent<Plant>() != null, __state, __instance.GetCreator(), you);
            });
        }

        // Cooking: what you made yourself, per item. The game books a cooking station's dish (grill, iron cooking station, oven,
        // any mod's CookingStation) to the station's OWNER: OnInteract on the cook's PC sends "RPC_RemoveDoneItem" to the
        // owner, whose SpawnItem calls IncrementStatItemCraft there (GAME-METRICS 1f). So the dish is counted on the PC of
        // whoever takes it off (TakeOff: the dish the station hands out first, the amount it sends, bonus included), the
        // owner-side booking is skipped (StationSpawn), and every other booking (the crafting window: cauldron, food table,
        // mead ketill) is the crafter's own (MadeByYou). Read only: nothing the game does changes.
        static int stationSpawn;          // > 0 inside CookingStation.SpawnItem
        static string takingOff;          // the dish (item token) the local player is taking off right now; null = none
        static bool takingBurnt;
        static readonly System.Reflection.MethodInfo isItemDone = AccessTools.Method(typeof(CookingStation), "IsItemDone");

        [HarmonyPatch(typeof(CookingStation), "OnInteract")]
        static class TakeOff
        {
            static void Prefix(CookingStation __instance, Humanoid user) => Safe(() =>
            {
                takingOff = null; takingBurnt = false;
                if (!Local(user) || __instance == null || isItemDone == null) return;
                var zdo = __instance.GetComponent<ZNetView>()?.GetZDO();
                if (zdo == null || __instance.m_slots == null) return;
                for (int i = 0; i < __instance.m_slots.Length; i++)   // the slot RPC_RemoveDoneItem empties: the first done one
                {
                    var item = zdo.GetString("slot" + i);
                    if (string.IsNullOrEmpty(item) || !(isItemDone.Invoke(__instance, new object[] { item }) is bool done) || !done) continue;
                    var prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(item) : null;
                    takingOff = prefab ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name : null;
                    takingBurnt = __instance.m_overCookedItem && __instance.m_overCookedItem.name == item;
                    return;
                }
            });
            static void Finalizer() { try { takingOff = null; } catch (Exception e) { HookGuard.Fail(e, "ClientHooks.TakeOff.Finalizer"); } }
        }

        // the amount OnInteract sends with the take-off (only while a take-off of the local player is under way)
        [HarmonyPatch(typeof(ZNetView), nameof(ZNetView.InvokeRPC), typeof(string), typeof(object[]))]
        static class TakeOffAmount
        {
            static void Prefix(string method, object[] parameters)   // every RPC of the game passes here: the cheap check first, no closure
            {
                try
                {
                    if (takingOff == null || method != "RPC_RemoveDoneItem") return;
                    var dish = takingOff; takingOff = null;
                    SessionEvents.CountTakenOff(Plugin.Events.Made, dish, takingBurnt, parameters);
                }
                catch (Exception e) { HookGuard.Fail(e, "ClientHooks.TakeOffAmount.Prefix"); }
            }
        }

        [HarmonyPatch(typeof(CookingStation), "SpawnItem")]
        static class StationSpawn
        {
            static void Prefix() { try { stationSpawn++; } catch (Exception e) { HookGuard.Fail(e, "ClientHooks.StationSpawn.Prefix"); } }
            static void Finalizer() { try { if (stationSpawn > 0) stationSpawn--; } catch (Exception e) { HookGuard.Fail(e, "ClientHooks.StationSpawn.Finalizer"); } }
        }

        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.IncrementStatItemCraft))]
        static class MadeByYou
        {
            static void Prefix(PlayerProfile __instance, string name, float amount) => Safe(() =>
                SessionEvents.CountMade(Plugin.Events.Made, name, amount, stationSpawn > 0, Game.instance != null && __instance == Game.instance.GetPlayerProfile()));
        }

        // Sharing your map at the cartography table: your exploration becomes the group's.
        [HarmonyPatch(typeof(MapTable), "OnWrite")]
        static class MapWrite
        {
            static void Postfix(MapTable __instance, Humanoid user, ItemDrop.ItemData item, bool __result) => Safe(() =>
            {
                if (__result && item == null && Local(user)) SessionEvents.Add(Plugin.Events.MapShared, Prefab(__instance));
            });
        }

        // Repairs with the hammer: the quiet upkeep of the hall.
        static bool repairing;
        [HarmonyPatch(typeof(Player), "Repair")]
        static class RepairBy
        {
            static void Prefix(Player __instance) { try { repairing = __instance == Player.m_localPlayer; } catch (Exception e) { HookGuard.Fail(e, "ClientHooks.RepairBy.Prefix"); } }   // the postfix sets it back
            static void Postfix() { try { repairing = false; } catch (Exception e) { HookGuard.Fail(e, "ClientHooks.RepairBy.Postfix"); } }
        }
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Repair))]
        static class Repaired
        {
            static void Postfix(WearNTear __instance, bool __result) => Safe(() =>
            {
                if (__result && repairing) SessionEvents.Add(Plugin.Events.Repairs, Prefab(__instance));
            });
        }

        // Trader purchases (no other mod tracks these).
        [HarmonyPatch(typeof(Trader), nameof(Trader.OnBought))]
        static class Bought
        {
            static void Postfix(Trader __instance, Trader.TradeItem item) => Safe(() =>
            {
                if (item?.m_prefab == null) return;
                var trader = __instance.m_name ?? "trader";
                SessionEvents.Add(Plugin.Events.Bought, trader + "|" + item.m_prefab.name, item.m_stack);
                SessionEvents.Add(Plugin.Events.Spent, trader, item.m_price);
            });
        }

        // Ore and fuel into smelters, kilns, furnaces, refineries: `user` is who put it in.
        [HarmonyPatch(typeof(Smelter), "OnAddOre")]
        static class SmeltOre
        {
            static void Postfix(Smelter __instance, Humanoid user, ItemDrop.ItemData item, bool __result) => Safe(() =>
            {
                if (__result && Local(user) && item != null) SessionEvents.Add(Plugin.Events.SmelterAdded, Prefab(__instance) + "|" + (item.m_dropPrefab ? item.m_dropPrefab.name : item.m_shared.m_name));
            });
        }
        [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
        static class SmeltFuel
        {
            static void Postfix(Smelter __instance, Humanoid user, bool __result) => Safe(() =>
            {
                if (__result && Local(user)) SessionEvents.Add(Plugin.Events.SmelterAdded, Prefab(__instance) + "|fuel");
            });
        }

        // Eating: whose cooking did you eat?
        [HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
        static class Eat
        {
            static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result) => Safe(() =>
            {
                if (!__result || !Local(__instance) || item == null || feasting) return;   // a feast bite is counted as a feast only
                var food = item.m_dropPrefab ? item.m_dropPrefab.name : item.m_shared.m_name;
                var maker = string.IsNullOrEmpty(item.m_crafterName) ? "unknown" : item.m_crafterName;
                SessionEvents.Add(Plugin.Events.AteFoodMadeBy, maker + "|" + food);
            });
        }

        // Feasts: who ate from whose placed feast (the piece's creator).
        static bool feasting;
        [HarmonyPatch(typeof(Feast), "RPC_EatConfirmation")]
        static class FeastEat
        {
            static void Finalizer() { try { feasting = false; } catch (Exception e) { HookGuard.Fail(e, "ClientHooks.FeastEat.Finalizer"); } }   // runs even if the game's method throws
            static void Prefix(Feast __instance) => Safe(() =>
            {
                feasting = true;
                var piece = __instance.GetComponent<Piece>();
                SessionEvents.Add(Plugin.Events.AteFromFeastOf, (piece ? piece.GetCreator() : 0L) + "|" + Prefab(__instance));
            });
        }

        // Gear: whose crafted weapon, tool or armour did you put on?
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        static class Equip
        {
            static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result) => Safe(() =>
            {
                if (!__result || !Local(__instance) || item == null || string.IsNullOrEmpty(item.m_crafterName)) return;
                SessionEvents.Add(Plugin.Events.EquippedGearMadeBy, item.m_crafterName + "|" + (item.m_dropPrefab ? item.m_dropPrefab.name : item.m_shared.m_name));
            });
        }

        // Voyages: who else is aboard, and who holds the helm. Sampled every 10 s from Plugin.Update.
        static readonly GameField<Ship, System.Collections.Generic.List<Player>> shipPlayers = new GameField<Ship, System.Collections.Generic.List<Player>>("m_players");   // first use, not type load
        internal static void SampleVoyage(float seconds) => Safe(() =>
        {
            var me = Player.m_localPlayer; var ship = Ship.GetLocalShip();
            CargoHooks.SampleShip(me, ship);   // cargo carried: at the helm, what lies in the ship times the metres it moved
            SampleLed(me);                     // animals led: tamed animals that follow you, the metres they moved
            if (me == null || ship == null || !ship.IsPlayerInBoat(me)) return;
            foreach (var p in shipPlayers.Of(ship)) if (p != null && p != me) SessionEvents.Add(Plugin.Events.SailedWith, p.GetPlayerName(), seconds);
            var helm = ship.m_shipControlls ? ship.m_shipControlls.GetUser() : 0L;
            if (helm != 0L && helm != me.GetPlayerID())
                foreach (var p in Player.GetAllPlayers()) if (p.GetPlayerID() == helm) { SessionEvents.Add(Plugin.Events.SailedUnderHelmOf, p.GetPlayerName(), seconds); break; }
        });

        // Animals led (Drover, Long Lead): tamed animals that follow YOU and that this PC owns (the game runs an animal's AI only on its
        // owner's PC, and the follow target is only known there: labelled "while your PC hosted them"). Sampled with the voyages, every 10 s.
        static readonly LedTracker led = new LedTracker();
        internal static void SampleLed(Player me) => Safe(() =>
        {
            var followers = new System.Collections.Generic.List<LedTracker.Follower>();
            if (me != null)
                foreach (var c in Character.GetAllCharacters())
                {
                    if (c == null || c == me || c is Player || !c.IsTamed() || !c.IsOwner()) continue;
                    var ai = c.GetComponent<MonsterAI>();
                    var target = ai != null ? ai.GetFollowTarget() : null;
                    if (target == null || target != me.gameObject) continue;
                    var zdo = c.GetComponent<ZNetView>()?.GetZDO();
                    var p = c.transform.position;
                    followers.Add(new LedTracker.Follower { Id = zdo != null ? zdo.m_uid.ToString() : c.GetInstanceID().ToString(), Kind = Prefab(c), X = p.x, Y = p.y, Z = p.z });
                }
            led.Sample(Time.realtimeSinceStartup, followers, Plugin.Events.LedMeters);
            if (DevCheck.On) DevCheck.Led(me, System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(followers, f => f.Kind)), led.Total, led.BestMetres, led.BestKind);   // Dev.SelfCheck
            var ledger = Plugin.FeatsLedger;
            if (ledger != null && led.BestMetres > ledger.Count(LedTracker.BestKey)) ledger.NoteBest(LedTracker.BestKey, led.BestMetres, DateTime.UtcNow, Biome(), led.BestKind);
        });

        // Hauling: metres you pulled a cart, sampled with the voyages.
        static readonly System.Reflection.FieldInfo cartList = AccessTools.Field(typeof(Vagon), "m_instances");
        static System.Collections.Generic.List<Vagon> carts() => cartList?.GetValue(null) as System.Collections.Generic.List<Vagon> ?? new System.Collections.Generic.List<Vagon>();
        static Vector3 cartLast; static bool cartWas;
        internal static void SampleCart() => Safe(() =>
        {
            var me = Player.m_localPlayer; Vagon pulled = null;
            if (me != null) foreach (var v in carts()) if (v != null && v.IsAttached(me)) { pulled = v; break; }
            if (pulled == null) { cartWas = false; return; }
            var pos = pulled.transform.position;
            if (cartWas) { var d = Vector3.Distance(pos, cartLast); if (d < 200f) { SessionEvents.Add(Plugin.Events.CartMeters, Prefab(pulled), d); CargoHooks.CartMoved(pulled, d); } }
            cartLast = pos; cartWas = true;
        });

        // What you gathered from the world, exactly, per item (the game's own itemsPickedUp skips a pickup that merges into a
        // stack you already carry, so it is a floor). Every pickup goes through Humanoid.Pickup: auto-pickup, E on a drop,
        // drops from trees, rocks, pickables (Pickable.RPC_Pick spawns them in the world), loot, and a fish taken from the
        // water. Not here: chest transfers, crafting output, trader purchases (no world drop). Counted by how much the
        // carried amount of that item grew, so a partial pickup with a full inventory counts only what went in.
        class PickState { public string Item; public bool Held; public int Stack, Before; }
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup))]
        static class Pickup
        {
            static void Prefix(Humanoid __instance, GameObject go, out PickState __state)
            {
                __state = null;
                try
                {
                    if (!Local(__instance) || go == null) return;
                    var drop = go.GetComponent<ItemDrop>(); var data = drop != null ? drop.m_itemData : null;
                    var inv = __instance.GetInventory();
                    if (data?.m_shared == null || string.IsNullOrEmpty(data.m_shared.m_name) || inv == null) return;
                    __state = new PickState { Item = data.m_shared.m_name, Held = data.m_pickedUp, Stack = data.m_stack,
                                              Before = inv.CountItems(data.m_shared.m_name, -1, false) };
                }
                catch (Exception e) { __state = null; HookGuard.Fail(e, "ClientHooks.Pickup.Prefix"); }
            }
            static void Postfix(Humanoid __instance, PickState __state) => Safe(() =>
            {
                if (__state == null || __state.Held) return;
                var inv = __instance.GetInventory(); if (inv == null) return;
                var n = SessionEvents.PickedAmount(__state.Held, __state.Stack, __state.Before, inv.CountItems(__state.Item, -1, false));
                if (n > 0) SessionEvents.Add(Plugin.Events.PickedUp, __state.Item, n);
            });
        }

        // Skill practice: what you actually trained this session (the profile only keeps level and current progress).
        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        static class Practice
        {
            static void Postfix(Skills __instance, Skills.SkillType skillType, float factor) => Safe(() =>
            {
                if (Player.m_localPlayer != null && __instance == Player.m_localPlayer.GetSkills()) SessionEvents.Add(Plugin.Events.SkillPractice, skillType.ToString(), factor);
            });
        }
    }
}
