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

        // While the panel is open the game treats it like the trader's window: cursor free, no walking, looking or hotbar.
        [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.IsVisible))]
        static class CountAsWindow
        {
            static void Postfix(ref bool __result) { if (!__result && PanelUi.Blocking) __result = true; }
        }

        // "This session" (one connection) starts at the first spawn after the measurements were reset (Plugin.OnLogout makes new ones).
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
