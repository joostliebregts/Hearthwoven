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
                BattleHooks.DotFrom(hit, a);   // 0.8: and which creature, for the battle feed
            });
        }
        internal static string Source(HitData hit)   // ArmourHooks names the source the same way
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
            static void Prefix(Character __instance, HitData hit, out DealtHit __state)
            {
                DealtHit d = null;
                Safe(() =>
                {
                    if (hit != null && Local(hit.GetAttacker()) && !Local(__instance))
                    {
                        var target = Prefab(__instance); var skill = GameNames.Of(hit.m_skill);   // the same text as ToString, worked out once per skill
                        Plugin.Session.AddDealt(target, skill, hit.m_damage);
                        // every hit, also on a foe another PC owns (the game's EnemyHits/PlayerHits miss those: owner trap)
                        SessionEvents.Add(Plugin.Events.Battle, __instance is Player ? "PlayerHits" : "EnemyHits");
                        var utc = DateTime.UtcNow; var biome = Biome();
                        Plugin.Log.AddDamage(utc, biome, true, target, skill, hit.m_damage);
                        BattleHooks.Dealt(__instance, target, hit);   // 0.8: this foe as its own creature (count per kind, battle feed)
                        if (!(__instance is Player)) Swung(utc, hit.m_skill, skill, hit.m_damage);
                        if (!(__instance is Player)) FeatsHooks.RaidHit(__instance);   // 0.9 Raid Breaker (its own guard)
                        if (!(__instance is Player)) d = new DealtHit { Utc = utc, Biome = biome, Target = target, Skill = skill, Before = hit.m_damage };   // its after in the postfix
                    }
                });
                __state = d;
            }

            // 0.8.5: the same hit after the foe's resistances and armor, worked out on this PC (BattleHooks.AfterOf) once the game has found the weak spot
            // it struck (Character.Damage sets hit.m_weakSpot; the foe's own PC gets a copy of the hit, so this one is as you swung it): beside its
            // before in the log and in the dealt ledger. A player has none: their armor is not known on this PC.
            static void Postfix(Character __instance, HitData hit, DealtHit __state) => Safe(() =>
            {
                if (__state == null) return;
                var after = BattleHooks.AfterOf(__instance, hit, __state.Before);
                if (!after.HasValue) return;
                Plugin.Log.AddAfter(__state.Utc, __state.Biome, __state.Target, __state.Skill, __state.Before, after.Value);
                Plugin.DealtArmour.Add(__state.Target, __state.Skill, default, __state.Before, after.Value);   // the dealt ledger: before and after of the same hits
            });
        }
        // 0.8.6 (DPS): your fighting time with this weapon kind: the time since its previous hit when within SwingClock.Gap, with the damage of this hit;
        // the first hit of a stretch adds nothing. The log (the short windows) and the dealt ledger (Session, the days, All) get the same two numbers.
        // And, per named weapon (DPS per named weapon): the weapon you hold, its upgrade level and your level in its skill, with every hit's damage.
        static void Swung(DateTime utc, Skills.SkillType type, string skill, HitData.DamageTypes d)
        {
            var kind = SwingClock.KindOf(skill); var secs = Plugin.Swing.Hit(kind, utc);
            var dmg = SwingClock.SwungOf(d);
            if (secs > 0f)
            {
                Plugin.Log.AddSwing(utc, kind, secs, dmg);
                Plugin.DealtArmour.AddSwing(kind, secs, dmg);
                Plugin.SwingBegan(utc);
            }
            if (!(dmg > 0f)) return;
            var swung = secs > 0f ? dmg : 0f; var weapon = HeldWeapon(type, out var quality, out var level);
            Plugin.Log.AddWeapon(utc, weapon, quality, skill, level, secs, swung, dmg);
            Plugin.DealtArmour.AddWeapon(weapon, quality, skill, level, secs, swung, dmg);
        }

        // The weapon in your hand: its prefab name and upgrade level, worked out once per item (a Unity name is a new string each time it is read, a hit must
        // allocate nothing). Null when what you hold is not the weapon of the skill the hit was booked on (an arrow that lands after you switched, a thrown spear):
        // the hit falls into the skill's "other" cell. Your level in the skill is the one the skills page shows.
        static ItemDrop.ItemData heldItem; static int heldQuality = -1; static string heldPrefab;
        static string HeldWeapon(Skills.SkillType type, out int quality, out int level)
        {
            quality = 0; level = 0;
            var p = Player.m_localPlayer; if (p == null) return null;
            var skills = p.GetSkills(); if (skills != null) level = Math.Max(0, (int)Math.Floor(skills.GetSkillLevel(type)));
            var item = p.GetCurrentWeapon();
            if (item?.m_shared == null || item.m_shared.m_skillType != type) return null;
            if (!ReferenceEquals(item, heldItem) || item.m_quality != heldQuality)
            {
                heldItem = item; heldQuality = item.m_quality;
                heldPrefab = item.m_dropPrefab ? string.Intern(item.m_dropPrefab.name) : type == Skills.SkillType.Unarmed ? WeaponCells.Fists : null;   // your fists are no item in the world
            }
            quality = heldQuality;
            return heldPrefab;
        }
        /// <summary>A hit of yours on a creature between DamageDealt's prefix (where its before is kept) and its postfix (its after).</summary>
        sealed class DealtHit { public DateTime Utc; public string Biome, Target, Skill; public HitData.DamageTypes Before; }

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
                BattleHooks.Received(hit, source);   // 0.8: which creature hit you (battle feed)
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
                BattleHooks.Died();   // 0.9: in the running fight, boss fight and dungeon run
                FeatsCounters.OnFell();   // 0.9 Held the Line: this fight does not hold
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
                Plugin.Log.AddGuard(DateTime.UtcNow, __state);   // 0.8.2: the same block with its minute (Battle > Defense's short windows)
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
                // a plant, or anything the cultivator places (PlantEverything's bushes and pickables have no Plant component; GameData knows them as planted)
                SessionEvents.CountPlanting(Plugin.Events.Planted, __instance.m_name, __instance.GetComponent<Plant>() != null || Panel.GameData.PieceKind(__instance.m_name) == "planted", __state, __instance.GetCreator(), you);
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
                    FeatsCounters.OnTakenOff(Plugin.FeatsLedger, takingBurnt);   // 0.9 Steady at the Grill: a burnt one ends the run
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
                if (__result && repairing) { SessionEvents.Add(Plugin.Events.Repairs, Prefab(__instance)); FeatsHooks.Repaired(__instance); }   // 0.9 Mender of Others
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
                if (!__result || !Local(__instance) || item == null) return;
                Combination(__instance);   // 0.9: the foods you now have on, a feast's serving among them (Meals' Your combinations)
                if (feasting) return;   // a feast bite is counted as a feast only
                var food = item.m_dropPrefab ? item.m_dropPrefab.name : item.m_shared.m_name;
                var maker = string.IsNullOrEmpty(item.m_crafterName) ? "unknown" : item.m_crafterName;
                SessionEvents.Add(Plugin.Events.AteFoodMadeBy, maker + "|" + food);
            });
        }

        /// <summary>0.9 (Meals' Your combinations, MealCombos): after the game took a food, the foods you have on (Player.GetFoods, one to three, each
        /// by its item prefab as the game names it, Food.m_name) and the game's food values of those items summed (m_food, m_foodStamina,
        /// m_foodEitr). Read only; guarded by the hook's Safe.</summary>
        static void Combination(Player p)
        {
            var foods = p.GetFoods();
            if (foods == null || foods.Count == 0) return;
            var names = new System.Collections.Generic.List<string>(foods.Count);
            float h = 0, st = 0, e = 0;
            foreach (var f in foods)
            {
                if (f == null || string.IsNullOrEmpty(f.m_name)) continue;
                names.Add(f.m_name);
                var shared = f.m_item?.m_shared;
                if (shared != null) { h += shared.m_food; st += shared.m_foodStamina; e += shared.m_foodEitr; }
            }
            Plugin.AteCombination(names, h, st, e);
        }

        // Feasts: who ate from whose placed feast (the piece's creator: the one who SET IT OUT), and from which feast (its ZDO id, the same on
        // every PC), so the feast's maker can be found in the placer's own record (SetOutFeastMadeBy, 0.8).
        static bool feasting;
        [HarmonyPatch(typeof(Feast), "RPC_EatConfirmation")]
        static class FeastEat
        {
            static void Finalizer() { try { feasting = false; } catch (Exception e) { HookGuard.Fail(e, "ClientHooks.FeastEat.Finalizer"); } }   // runs even if the game's method throws
            static void Prefix(Feast __instance) => Safe(() =>
            {
                feasting = true;
                var piece = __instance.GetComponent<Piece>();
                var key = (piece ? piece.GetCreator() : 0L) + "|" + Prefab(__instance);
                SessionEvents.Add(Plugin.Events.AteFromFeastOf, key);
                var zdo = __instance.GetComponent<ZNetView>()?.GetZDO();
                if (zdo != null) SessionEvents.Add(Plugin.Events.AteFromFeastAt, key + "|" + zdo.m_uid);
            });
        }

        // Feasts set out (0.8, B22): who made the feast you set out. Player.PlacePiece names YOU (the placer) as the piece's creator, and the
        // feast item's crafter is gone once it is placed, so it is read here from the item the game is about to take from your bag
        // (Player.UpdatePlacement takes it after TryPlacePiece returns: ConsumeResources, Inventory.RemoveItem, the first matching stack at
        // your world level). The new feast's ZDO id joins it to whoever eats from it (FeastEat). Read only: nothing is written to the world.
        static bool placingFeast;   // inside Player.PlacePiece for the local player, placing a feast
        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        static class SetOut
        {
            static void Prefix(Player __instance, Piece piece)
            {
                try { placingFeast = __instance != null && __instance == Player.m_localPlayer && piece != null && piece.GetComponent<Feast>() != null; }
                catch (Exception e) { HookGuard.Fail(e, "ClientHooks.SetOut.Prefix"); }   // the finalizer clears it
            }
            static void Finalizer() { try { placingFeast = false; } catch (Exception e) { HookGuard.Fail(e, "ClientHooks.SetOut.Finalizer"); } }   // runs even if the game's method throws
        }
        [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
        static class SetOutFeast
        {
            static void Postfix(Piece __instance) => Safe(() =>
            {
                if (!placingFeast || __instance == null) return;
                var me = Player.m_localPlayer; if (me == null || __instance.GetCreator() != me.GetPlayerID()) return;
                placingFeast = false;   // one feast per placement
                var zdo = __instance.GetComponent<ZNetView>()?.GetZDO(); if (zdo == null) return;
                var crafter = FeastCrafter(me, __instance);
                if (!string.IsNullOrEmpty(crafter)) SessionEvents.Add(Plugin.Events.SetOutFeastMadeBy, crafter + "|" + Prefab(__instance) + "|" + zdo.m_uid);
            });
        }

        /// <summary>The crafter of the feast item the game takes from your bag for this piece: the first stack of it at your world level, in the
        /// bag's order (Inventory.RemoveItem). Null when nothing is taken (the no-cost cheat, the world's no-crafting-cost key) or none is there;
        /// "" for an item nobody crafted.</summary>
        static string FeastCrafter(Player me, Piece piece)
        {
            if (me.NoCostCheat() || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))) return null;
            var bag = me.GetInventory(); if (bag == null || piece.m_resources == null) return null;
            foreach (var req in piece.m_resources)
            {
                var name = req != null && req.m_resItem ? req.m_resItem.m_itemData?.m_shared?.m_name : null;
                if (string.IsNullOrEmpty(name) || req.GetAmount(0) <= 0) continue;
                foreach (var item in bag.GetAllItems())
                    if (item?.m_shared != null && item.m_shared.m_name == name && item.m_worldLevel >= Game.m_worldLevel) return item.m_crafterName ?? "";
            }
            return null;
        }

        // Gear: whose crafted weapon, tool or armour did you put on? 0.9 Armor sets, made or worn: and every armor piece you put on, whoever made it
        // (its token once, ArmorWornBook)
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        static class Equip
        {
            static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result) => Safe(() =>
            {
                if (!__result || !Local(__instance) || item == null) return;
                if (item.m_shared != null && Panel.PanelModel.IsArmorSlot(item.m_shared.m_itemType.ToString())) Plugin.Wore(item.m_shared.m_name);
                if (string.IsNullOrEmpty(item.m_crafterName)) return;
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
            SampleCrew(me, ship, seconds);     // 0.9 Whole Crew: at the helm with a full crew
            FeatsHooks.SampleWorld(me, ship, seconds);   // 0.9.1: storm at the helm, lands this session, how far out, the saddle (its own guard)
            if (me == null || ship == null || !ship.IsPlayerInBoat(me)) return;
            foreach (var p in shipPlayers.Of(ship)) if (p != null && p != me) SessionEvents.Add(Plugin.Events.SailedWith, p.GetPlayerName(), seconds);
            var helm = ship.m_shipControlls ? ship.m_shipControlls.GetUser() : 0L;
            if (helm != 0L && helm != me.GetPlayerID())
                foreach (var p in Player.GetAllPlayers()) if (p.GetPlayerID() == helm) { SessionEvents.Add(Plugin.Events.SailedUnderHelmOf, p.GetPlayerName(), seconds); break; }
        });

        static void SampleCrew(Player me, Ship ship, float seconds) => Safe(() =>
        {
            var aboard = me != null && ship != null && ship.IsPlayerInBoat(me);
            var helm = aboard && ship.m_shipControlls ? ship.m_shipControlls.GetUser() : 0L;
            int others = 0;
            if (aboard) foreach (var p in shipPlayers.Of(ship)) if (p != null && p != me) others++;
            FeatsCounters.OnCrewSample(Plugin.FeatsLedger, aboard && helm != 0L && helm == me.GetPlayerID(), others, seconds);
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
        // 0.8: a piece's materials (what it dropped when it came down: SalvageHooks) are recovered, never brought in.
        class PickState { public string Item; public bool Held, FromPiece; public int Stack, Before; }
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
                                              Before = inv.CountItems(data.m_shared.m_name, -1, false), FromPiece = !data.m_pickedUp && SalvageHooks.FromPiece(go) };
                }
                catch (Exception e) { __state = null; HookGuard.Fail(e, "ClientHooks.Pickup.Prefix"); }
            }
            static void Postfix(Humanoid __instance, PickState __state) => Safe(() =>
            {
                if (__state == null || __state.Held) return;
                var inv = __instance.GetInventory(); if (inv == null) return;
                SessionEvents.CountPickup(Plugin.Events, __state.Item, __state.Held, __state.FromPiece, __state.Stack, __state.Before, inv.CountItems(__state.Item, -1, false));
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
