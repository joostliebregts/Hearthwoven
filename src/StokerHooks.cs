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
    /// Only that one PC feeds a chest: the first player near it, or the last who opened it (the game moves a chest to whoever opens it). A
    /// host on a Hearthwoven before 0.8.0, or none, counts nothing, and before 0.8.5 no PC counted anything (Taken read the amount after the
    /// game had counted it down to 0; Joost's evening, 2026-10-11). So since 0.8.5 the server counts every chest's feeding once (ChestWatch's
    /// "auto-feed" lines, ServerBook.Fed) and the book shows that; this PC's count is what the book shows from a server before 0.8.5 (the
    /// sum over the group on Everyone).
    /// A soft dependency: without the mod, or when its methods are renamed, Prepare says no and nothing is patched (the mod is never referenced
    /// at build time). Never on a dedicated server.
    /// </summary>
    static class StokerHooks
    {
        public const string ModGuid = "zl.smelterupgrades", FeederType = "SmelterUpgrades.SmelterFeeder";

        // the feeding call in progress on this PC: the station, its prefab and fuel item's prefab ("smelter", "Coal"), and whether it is fuel;
        // cleared by each Finalizer and by the one take it counts
        static Smelter station; static string stationName, fuelName; static bool fuel;

        // a feeding call begins (FeedFuel's or FeedOre's prefix): the names read here, so the take below touches no game object to count
        static void Begin(Smelter s, bool isFuel)
        {
            station = s; fuel = isFuel;
            stationName = s ? Utils.GetPrefabName(s.gameObject) : null;
            fuelName = s && s.m_fuelItem ? s.m_fuelItem.gameObject.name : null;
        }

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
            else if (feeder != null) log?.LogWarning("Stoker's Chests (" + ModGuid + ") found, but not " + FeederType + ".FeedFuel/FeedOre(Smelter, Inventory) (another version?): this PC does not count their feeding; a server on Hearthwoven 0.8.5 or later still does");
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
            static void Prefix(Smelter __0) => HookGuard.Run(() => Begin(__0, true));
            static void Finalizer() { try { station = null; } catch (Exception e) { HookGuard.Fail(e, "StokerHooks.FeedFuel.Finalizer"); } }
        }

        [HarmonyPatch()]
        static class FeedOre
        {
            static bool Prepare() => Found("FeedOre");
            static MethodBase TargetMethod() => Feed("FeedOre");
            static void Prefix(Smelter __0) => HookGuard.Run(() => Begin(__0, false));
            static void Finalizer() { try { station = null; } catch (Exception e) { HookGuard.Fail(e, "StokerHooks.FeedOre.Finalizer"); } }
        }

        // the chest gives up the one item it just sent: one feed, counted once (the feeder takes one per call, after the RPC went out).
        // The amount is read before the game's RemoveItem runs: it counts its own "amount" argument down to 0 as it removes, and a postfix
        // reads arguments as the method left them, so 0.8.0 up to the 0.8.5 test build read 0 and counted no feed at all (Joost's evening, 2026-10-11: the
        // server's chest log shows 879 items fed from his PC on 10 October, his book none).
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
        static class Taken
        {
            static bool Prepare() => Feed("FeedFuel") != null || Feed("FeedOre") != null;
            static void Prefix(int amount, out int __state)
            {
                __state = 0;
                try { if ((object)station != null) __state = amount; }   // only during a feeding call: every other RemoveItem costs one comparison
                catch (Exception e) { HookGuard.Fail(e, "StokerHooks.Taken.Prefix"); }
            }
            static void Postfix(string name, int __state) => HookGuard.Run(() => Took(name, __state));
        }

        static void Took(string name, int amount)
        {
            var s = station;
            if ((object)s == null) return;
            station = null;
            if (amount <= 0 || Dedicated()) return;
            var item = fuel ? "fuel" : OreOf(s, name) ?? name;
            SessionEvents.Add(Plugin.Events.ChestFed, stationName + "|" + item, amount);
            if (DevCheck.On) DevCheck.Hook("stoker-hook", "a Stoker's Chest fed " + stationName + " from this PC (it hosts the chest), counted once");
            // a world hosted on this PC: its own chest updates never come from a peer, so ChestWatch never sees this feed; the host books it
            // into the server's count itself (a peer's feeding still reaches ChestWatch: one count either way)
            if (Hosting()) ServerBookHooks.Fed(fuel ? fuelName : OreOf(s, name), amount);
        }

        /// <summary>Where this PC runs: a dedicated server (never feeds), or the host of a world (books its own feeding for the server). The
        /// tests swap them: the game's network class cannot load without the game.</summary>
        internal static Func<bool> Dedicated = () => ZNet.instance != null && ZNet.instance.IsDedicated(), Hosting = () => ZNet.instance != null && ZNet.instance.IsServer();

        // an ore's prefab name ("CopperOre", as SmelterAdded keys it) from the item name the chest gave ("$item_copperore"), by the station's own conversions
        static string OreOf(Smelter s, string token)
        {
            if ((object)s == null || s.m_conversion == null) return null;
            foreach (var c in s.m_conversion)
                if (c != null && c.m_from != null && c.m_from.m_itemData?.m_shared?.m_name == token) return c.m_from.gameObject.name;
            return null;
        }
    }
}
