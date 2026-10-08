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
        static void Safe(Action a) { try { a(); } catch (Exception e) { Debug.LogWarning("[Hearthwoven] " + e.Message); } }

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
            });
        }

        // Blocks and parries, by the game's own rule (Humanoid.BlockAttack): a parry needs a shield that can parry
        // (m_timedBlockBonus > 1, so tower shields never do), a raise within 0.25 s, and a block that held (not staggered).
        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        static class Block
        {
            static readonly AccessTools.FieldRef<Humanoid, float> timer = AccessTools.FieldRefAccess<Humanoid, float>("m_blockTimer");
            static readonly System.Reflection.MethodInfo getBlocker = AccessTools.Method(typeof(Humanoid), "GetCurrentBlocker");
            static void Prefix(Humanoid __instance, out bool __state)
            {
                __state = false;
                if (!Local(__instance)) return;
                var blocker = getBlocker?.Invoke(__instance, null) as ItemDrop.ItemData;
                float t = timer(__instance);
                __state = blocker != null && blocker.m_shared.m_timedBlockBonus > 1f && t != -1f && t < 0.25f;
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
        static class ChopTree { static void Prefix(TreeBase __instance, HitData hit) => Chop(__instance, hit); }
        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
        static class ChopLog { static void Prefix(TreeLog __instance, HitData hit) => Chop(__instance, hit); }
        static void Chop(Component tree, HitData hit) => Safe(() =>
        {
            if (hit != null && hit.m_damage.m_chop > 0f && Local(hit.GetAttacker()))
                SessionEvents.Add(Plugin.Events.ChopHits, Prefab(tree));
        });

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
            static void Prefix(Player __instance) { repairing = __instance == Player.m_localPlayer; }
            static void Postfix() { repairing = false; }
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
            static void Finalizer() { feasting = false; }   // runs even if the game's method throws
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
        static readonly AccessTools.FieldRef<Ship, System.Collections.Generic.List<Player>> shipPlayers =
            AccessTools.FieldRefAccess<Ship, System.Collections.Generic.List<Player>>("m_players");
        internal static void SampleVoyage(float seconds) => Safe(() =>
        {
            var me = Player.m_localPlayer; var ship = Ship.GetLocalShip();
            if (me == null || ship == null || !ship.IsPlayerInBoat(me)) return;
            foreach (var p in shipPlayers(ship)) if (p != null && p != me) SessionEvents.Add(Plugin.Events.SailedWith, p.GetPlayerName(), seconds);
            var helm = ship.m_shipControlls ? ship.m_shipControlls.GetUser() : 0L;
            if (helm != 0L && helm != me.GetPlayerID())
                foreach (var p in Player.GetAllPlayers()) if (p.GetPlayerID() == helm) { SessionEvents.Add(Plugin.Events.SailedUnderHelmOf, p.GetPlayerName(), seconds); break; }
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
            if (cartWas) { var d = Vector3.Distance(pos, cartLast); if (d < 200f) SessionEvents.Add(Plugin.Events.CartMeters, Prefab(pulled), d); }
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
                catch (Exception e) { __state = null; Debug.LogWarning("[Hearthwoven] " + e.Message); }
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
