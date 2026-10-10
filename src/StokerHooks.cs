using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// The Stoker's Chest of the OverDrive-SmelterUpgrades mod (GUID "zl.smelterupgrades", read from its 1.1.5 client DLL; reference/mods/
    /// OverDrive-SmelterUpgrades.md). Its SmelterUpgrades.SmelterFeeder.Tick runs every few seconds only on the PC that owns the chest
    /// (ZNetView.IsOwner) and, for each kiln, smelter or blast furnace in range, calls FeedFuel(Smelter, Inventory) and FeedOre(Smelter, Inventory).
    /// Those send the game's own "RPC_AddFuel" / "RPC_AddOre" straight to the station (run by the station's owner) and then take one from the
    /// chest with Inventory.RemoveItem(name, 1, -1, true). The game's hand path, Smelter.OnAddOre / OnAddFuel (ClientHooks.SmeltOre/SmeltFuel),
    /// never runs, so this feeding was never in SmelterAdded.
    /// Counted here as nobody's own: Events.ChestFed on the PC that hosted the chest, "station|ore prefab" or "station|fuel" like SmelterAdded.
    /// A soft dependency: without the mod, or when its methods are renamed, Prepare says no and nothing is patched (the mod is never referenced
    /// at build time). Never on a dedicated server.
    /// </summary>
    static class StokerHooks
    {
        public const string ModGuid = "zl.smelterupgrades", FeederType = "SmelterUpgrades.SmelterFeeder";

        // the feeding call in progress on this PC: the station and whether it is fuel; cleared by each Finalizer and by the one take it counts
        static Smelter station; static bool fuel;

        /// <summary>A hook of this class: patched on the first frame (Patch), not in Plugin.Awake. BepInEx loads each plugin's assembly only when
        /// that plugin's turn comes, so at Hearthwoven's Awake the smelter mod may not be loaded yet; a dependency attribute would fix the order but
        /// Hearthwoven carries none (test N2).</summary>
        internal static bool Later(Type t) => t != null && t.DeclaringType == typeof(StokerHooks);

        /// <summary>Patches this class's hooks once every plugin is loaded. Without the mod each one's Prepare says no and nothing is patched.</summary>
        internal static void Patch(Harmony harmony, BepInEx.Logging.ManualLogSource log)
        {
            if (harmony == null) return;
            foreach (var t in typeof(StokerHooks).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (t.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try { harmony.CreateClassProcessor(t).Patch(); }
                catch (Exception e) { log?.LogWarning("hook " + t.Name + " skipped: " + (e.InnerException ?? e).Message); }
            }
            if (Panel.PanelModel.StokersChests) log?.LogInfo("Stoker's Chests (" + ModGuid + ") found: their feeding is counted apart, as nobody's own");
        }

        /// <summary>The feeder's method, or null when the mod (or that method) is not there.</summary>
        static MethodBase Feed(string name)
        {
            try
            {
                if (!looked) { looked = true; feeder = AccessTools.TypeByName(FeederType); }   // one search of every loaded assembly, not one per hook
                return feeder == null ? null : AccessTools.DeclaredMethod(feeder, name, new[] { typeof(Smelter), typeof(Inventory) });
            }
            catch { return null; }
        }
        static Type feeder; static bool looked;

        static bool Found(string name)
        {
            var ok = Feed(name) != null;
            if (ok) Panel.PanelModel.StokersChests = true;   // the mod is enforced on the server: every fellow here has the chests too
            return ok;
        }

        [HarmonyPatch()]
        static class FeedFuel
        {
            static bool Prepare() => Found("FeedFuel");
            static MethodBase TargetMethod() => Feed("FeedFuel");
            static void Prefix(Smelter __0) => HookGuard.Run(() => { station = __0; fuel = true; });
            static void Finalizer() { try { station = null; } catch (Exception e) { HookGuard.Fail(e, "StokerHooks.FeedFuel.Finalizer"); } }
        }

        [HarmonyPatch()]
        static class FeedOre
        {
            static bool Prepare() => Found("FeedOre");
            static MethodBase TargetMethod() => Feed("FeedOre");
            static void Prefix(Smelter __0) => HookGuard.Run(() => { station = __0; fuel = false; });
            static void Finalizer() { try { station = null; } catch (Exception e) { HookGuard.Fail(e, "StokerHooks.FeedOre.Finalizer"); } }
        }

        // the chest gives up the one item it just sent: one feed, counted once (the feeder takes one per call, after the RPC went out)
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
        static class Taken
        {
            static bool Prepare() => Feed("FeedFuel") != null || Feed("FeedOre") != null;
            static void Postfix(string name, int amount) => HookGuard.Run(() =>
            {
                var s = station;
                if (s == null) return;
                station = null;
                if (amount <= 0 || (ZNet.instance != null && ZNet.instance.IsDedicated())) return;
                var item = fuel ? "fuel" : OreOf(s, name) ?? name;
                SessionEvents.Add(Plugin.Events.ChestFed, Utils.GetPrefabName(s.gameObject) + "|" + item);
            });
        }

        // an ore's prefab name ("CopperOre", as SmelterAdded keys it) from the item name the chest gave ("$item_copperore"), by the station's own conversions
        static string OreOf(Smelter s, string token)
        {
            if (s.m_conversion == null) return null;
            foreach (var c in s.m_conversion)
                if (c != null && c.m_from != null && c.m_from.m_itemData?.m_shared?.m_name == token) return c.m_from.gameObject.name;
            return null;
        }
    }
}
