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
                catch (Exception e) { HookGuard.Fail(e, "PanelHooks.AttachToHud.Postfix"); }   // the panel stays off
            }
        }

        // While the panel is open the character takes no input at all, the way chat, the console and sign text block it:
        // W/S/A/D, Q and E are the panel's own keys then, so they must not walk, autorun, stand you up or steer a ship.
        // (The trader-window trick alone only stops movement with a gamepad: PlayerController.TakeInput.)
        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        static class NoMovement
        {
            // every frame: no closure; on an error the game's input runs as always (true), its result untouched
            static bool Prefix(ref bool __result) { try { if (!PanelUi.Blocking) return true; __result = false; return false; } catch (Exception e) { HookGuard.Fail(e, "PanelHooks.NoMovement.Prefix"); return true; } }
        }

        [HarmonyPatch(typeof(Player), "TakeInput")]
        static class NoActions
        {
            static bool Prefix(ref bool __result) { try { if (!PanelUi.Blocking) return true; __result = false; return false; } catch (Exception e) { HookGuard.Fail(e, "PanelHooks.NoActions.Prefix"); return true; } }
        }

        // ...and reports itself as the trader's window for what the input block does not reach: the mouse wheel zooming
        // the camera, the hotbar, and the Escape that closes the panel also opening the main menu. Side effect: a trader
        // standing next to you skips its idle lines while the panel is open (Trader.RandomTalk).
        [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.IsVisible))]
        static class CountAsWindow
        {
            static void Postfix(ref bool __result) { try { if (!__result && PanelUi.Blocking) __result = true; } catch (Exception e) { HookGuard.Fail(e, "PanelHooks.CountAsWindow.Postfix"); } }
        }

        // The game writes the hover text of whatever is under the crosshair ("Spice Rack", "[E] Open") every frame, on the
        // HUD canvas that draws above the panel. While the panel is open: no hover text, no piece health or author card, no
        // crosshair. Nothing to restore by hand: the next frame after closing the game writes them again; only the
        // crosshair image, which the game never re-enables, is switched back on here.
        [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
        static class NoHoverText
        {
            static bool hid;
            static void Postfix(Hud __instance)
            {
                try
                {
                    if (PanelUi.Blocking)
                    {
                        if (__instance.m_hoverName) __instance.m_hoverName.text = "";
                        if (__instance.m_pieceHealthRoot) __instance.m_pieceHealthRoot.gameObject.SetActive(false);
                        if (__instance.m_hoveredPieceAuthorWindow) __instance.m_hoveredPieceAuthorWindow.SetActive(false);
                        if (__instance.m_crosshair && __instance.m_crosshair.enabled) { __instance.m_crosshair.enabled = false; hid = true; }
                    }
                    else if (hid) { if (__instance.m_crosshair) __instance.m_crosshair.enabled = true; hid = false; }
                }
                catch (Exception e) { HookGuard.Fail(e, "PanelHooks.NoHoverText.Postfix"); }
            }
        }

        // The creature hud (a hen's name and health bar over the creature you look at, a boss bar) draws on top of the open
        // panel (playtest B5). The game sets its root active every frame (EnemyHud.LateUpdate: shown unless the player hid the
        // HUD); while the panel is open it stays off, and the next frame after closing the game shows it again by itself.
        [HarmonyPatch(typeof(EnemyHud), "LateUpdate")]
        static class NoCreatureHud
        {
            static void Postfix(EnemyHud __instance) { try { if (PanelUi.Blocking && __instance && __instance.m_hudRoot) __instance.m_hudRoot.SetActive(false); } catch (Exception e) { HookGuard.Fail(e, "PanelHooks.NoCreatureHud.Postfix"); } }
        }

        // Tab is the game's inventory key and closes the open book (PanelUi.Update); the filter key (K by default) is a second key for the
        // filters. While the panel is open, a press of either does not also open the inventory (the book would close and the inventory
        // would open on one press). Only then: with the panel shut, or on any other
        // key or button, InventoryGui.Show runs as always; a chest (Show with a container) is never touched.
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        static class FilterKeyNotInventory
        {
            static bool Prefix(Container container)   // on an error: the inventory opens as always
            {
                try { return container != null || !PanelUi.FilterKeyHeld(); }
                catch (Exception e) { HookGuard.Fail(e, "PanelHooks.FilterKeyNotInventory.Prefix"); return true; }
            }
        }

        // "This session" (one connection) starts at the first spawn after the measurements were reset for a new connection.
        [HarmonyPatch(typeof(Player), "OnSpawned")]
        static class SessionStart
        {
            static SessionEvents seen;
            static void Postfix(Player __instance) => HookGuard.Run(() =>
            {
                if (__instance != Player.m_localPlayer || ReferenceEquals(seen, Plugin.Events)) return;
                seen = Plugin.Events;
                PanelUi.SessionStart = DateTime.UtcNow;
            });
        }
    }
}
