using System;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Where the panel joins the game. Picked up by the per-type patch loop in Plugin.Awake, so a game update that breaks
    /// one of these only switches the panel off, never the measurements.
    /// </summary>
    static class PanelHooks
    {
        // The in-game HUD exists only on a player's PC; a dedicated server runs headless and never gets a panel.
        [HarmonyPatch(typeof(Hud), "Awake")]
        static class AttachToHud
        {
            static void Postfix(Hud __instance)
            {
                try
                {
                    if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
                    if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Plugin.Guid, out var info) || info.Instance == null) return;
                    PanelUi.BindConfig(info.Instance.Config);
                    if (!__instance.GetComponent<PanelUi>()) __instance.gameObject.AddComponent<PanelUi>();   // goes away with the HUD
                }
                catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel not attached: " + e.Message); }
            }
        }

        // While the panel is open the character takes no input at all, the way chat, the console and sign text block it:
        // W/S/A/D, Q and E are the panel's own keys then, so they must not walk, autorun, stand you up or steer a ship.
        // (The trader-window trick alone only stops movement with a gamepad: PlayerController.TakeInput.)
        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        static class NoMovement
        {
            static bool Prefix(ref bool __result) { if (!PanelUi.Blocking) return true; __result = false; return false; }
        }

        [HarmonyPatch(typeof(Player), "TakeInput")]
        static class NoActions
        {
            static bool Prefix(ref bool __result) { if (!PanelUi.Blocking) return true; __result = false; return false; }
        }

        // ...and reports itself as the trader's window for what the input block does not reach: the mouse wheel zooming
        // the camera, the hotbar, and the Escape that closes the panel also opening the main menu. Side effect: a trader
        // standing next to you skips its idle lines while the panel is open (Trader.RandomTalk).
        [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.IsVisible))]
        static class CountAsWindow
        {
            static void Postfix(ref bool __result) { if (!__result && PanelUi.Blocking) __result = true; }
        }

        // "This session" (one connection) starts at the first spawn after the measurements were reset for a new connection.
        [HarmonyPatch(typeof(Player), "OnSpawned")]
        static class SessionStart
        {
            static SessionEvents seen;
            static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || ReferenceEquals(seen, Plugin.Events)) return;
                seen = Plugin.Events;
                PanelUi.SessionStart = DateTime.UtcNow;
            }
        }
    }
}
