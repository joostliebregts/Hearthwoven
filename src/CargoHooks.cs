using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// Client-side measuring points of 0.6 that read the game and call Cargo (the arithmetic, tested without the game).
    /// Each one only reads, never changes a result, and only counts the local player. Read from the game's own code
    /// (decompiled 2026-10-09): Container keeps its inventory current on every PC that has it loaded (CheckForChanges
    /// every second reloads it when the network object's revision changed); Vagon.m_container is the cart's box; a ship's
    /// box is a Container on a child object of the ship; Procreation.Procreate and EggGrow.GrowUpdate run on the PC that
    /// owns the animal or egg and call SetTamed on the newborn; Growup.GrowUpdate (a young animal growing up) is another
    /// method and is never counted.
    /// </summary>
    static class CargoHooks
    {
        static void Safe(Action a) => HookGuard.Run(a);   // RESILIENCE-06 item 6: one guard for every hook

        // ---------- cargo ----------

        static bool unreadableLogged;

        /// <summary>
        /// What lies in these containers now, item token -> amount. null = the inventory could not be read (the container
        /// exists but its inventory is not there): the sample is skipped and the reason is logged once. No containers: empty.
        /// </summary>
        internal static Dictionary<string, int> Aboard(IEnumerable<Container> boxes, string what)
        {
            var aboard = new Dictionary<string, int>();
            foreach (var box in boxes)
            {
                if (box == null) continue;
                var inventory = box.GetInventory();
                if (inventory == null)
                {
                    if (!unreadableLogged) { unreadableLogged = true; Debug.LogWarning("[Hearthwoven] cargo not counted: the inventory of " + what + " is not loaded on this PC (logged once)"); }
                    return null;
                }
                foreach (var item in inventory.GetAllItems())
                {
                    var token = item?.m_shared?.m_name;
                    if (!string.IsNullOrEmpty(token) && item.m_stack > 0) { aboard.TryGetValue(token, out var n); aboard[token] = n + item.m_stack; }
                }
            }
            return aboard;
        }

        // The ship you steer: the metres it moved since the last sample, times what lies in its box. Called with the voyage sample.
        static Ship shipOf; static Vector3 shipLast;
        // Heavy Keel: the voyage at the helm (CargoVoyage), its best load kept in the feats ledger the moment it beats the one before
        static readonly CargoVoyage voyage = new CargoVoyage();
        internal static void SampleShip(Player me, Ship ship) => Safe(() =>
        {
            var now = Time.realtimeSinceStartup;
            var holdsHelm = me != null && ship != null && ship.IsPlayerInBoat(me) && ship.m_shipControlls != null && ship.m_shipControlls.GetUser() == me.GetPlayerID();
            if (!holdsHelm) { if (DevCheck.On) DevCheck.NotAtHelm(me, ship); shipOf = null; voyage.Step(now, null, 0, 0); return; }
            var pos = ship.transform.position;
            int load = 0; double metres = 0;
            if (shipOf == ship)
            {
                var boxes = ship.GetComponentsInChildren<Container>();
                var aboard = Aboard(boxes, Utils.GetPrefabName(ship.gameObject));
                if (aboard != null)
                {
                    metres = Vector3.Distance(pos, shipLast);
                    Cargo.Add(Plugin.Events.CargoMeters, Plugin.Events.CargoStretch, aboard, metres);
                    load = CargoVoyage.MetalOreCount(aboard);
                }
                if (DevCheck.On) DevCheck.Ship(Utils.GetPrefabName(ship.gameObject), boxes.Length, aboard, metres, load);   // Dev.SelfCheck
            }
            voyage.Step(now, ship, load, metres);
            var ledger = Plugin.FeatsLedger;
            if (ledger != null && voyage.Best > ledger.Count(CargoVoyage.BestKey)) ledger.NoteBest(CargoVoyage.BestKey, voyage.Best, DateTime.UtcNow, ClientHooks.Biome());
            if (DevCheck.On) DevCheck.Keel(voyage.Active, voyage.LoadedMetres, voyage.Best, ledger != null ? ledger.Count(CargoVoyage.BestKey) : -1);   // Dev.SelfCheck
            shipOf = ship; shipLast = pos;
        });

        // The cart you pull: the same step the cart metres use, times what lies in its box.
        internal static void CartMoved(Vagon cart, float metres) => Safe(() =>
        {
            if (cart == null) return;
            var aboard = Aboard(new[] { cart.m_container }, Utils.GetPrefabName(cart.gameObject));
            if (aboard != null) Cargo.Add(Plugin.Events.CargoMeters, Plugin.Events.CargoStretch, aboard, metres);
            if (DevCheck.On) DevCheck.Cart(Utils.GetPrefabName(cart.gameObject), aboard, metres);   // Dev.SelfCheck
        });

        // ---------- born in your care ----------

        static int birthDepth;   // > 0 inside Procreation.Procreate or EggGrow.GrowUpdate
        static string birthVia;  // which of the two (Dev.SelfCheck says it)

        [HarmonyPatch(typeof(Procreation), "Procreate")]
        static class Procreate
        {
            static void Prefix() { try { birthDepth++; birthVia = "Procreation.Procreate"; if (DevCheck.On) DevCheck.Hook("procreate-hook", "Procreation.Procreate is called on this PC (the birth hook fires)"); } catch (Exception e) { HookGuard.Fail(e, "CargoHooks.Procreate.Prefix"); } }
            static void Finalizer() { try { if (birthDepth > 0) birthDepth--; } catch (Exception e) { HookGuard.Fail(e, "CargoHooks.Procreate.Finalizer"); } }
        }

        [HarmonyPatch(typeof(EggGrow), "GrowUpdate")]
        static class Hatch
        {
            static void Prefix() { try { birthDepth++; birthVia = "EggGrow.GrowUpdate"; if (DevCheck.On) DevCheck.Hook("egg-hook", "EggGrow.GrowUpdate is called on this PC (the hatch hook fires)"); } catch (Exception e) { HookGuard.Fail(e, "CargoHooks.Hatch.Prefix"); } }
            static void Finalizer() { try { if (birthDepth > 0) birthDepth--; } catch (Exception e) { HookGuard.Fail(e, "CargoHooks.Hatch.Finalizer"); } }
        }

        // The newborn is tamed by the game's own call, inside one of the two above: counted once, if it is near you.
        [HarmonyPatch(typeof(Character), nameof(Character.SetTamed))]
        static class NewbornTamed
        {
            static void Postfix(Character __instance, bool tamed) => Safe(() =>
            {
                var me = Player.m_localPlayer;
                if (me == null || __instance == null || __instance == me) return;
                var creature = Utils.GetPrefabName(__instance.gameObject);
                if (birthDepth <= 0) { if (DevCheck.On) DevCheck.Tamed(creature, tamed, null, 0, false); return; }
                var metres = Vector3.Distance(me.transform.position, __instance.transform.position);
                var counted = Cargo.CountBirth(Plugin.Events.BornInCare, creature, tamed, true, metres);
                if (DevCheck.On) DevCheck.Tamed(creature, tamed, birthVia, metres, counted);   // Dev.SelfCheck
            });
        }
    }
}
