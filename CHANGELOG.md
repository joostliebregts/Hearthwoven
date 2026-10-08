# Changelog

Every tweak that reaches a PC or the server gets its own patch version (Joost, 2026-10-08), so the version in the
log line and in Gale always says which build runs.

## 0.2.1 (2026-10-08)
- Server: player stats are filed under the player's real id (the game never fills `ZNetPeer.m_playerID`; it stayed 0 for everyone). JSON written without a BOM.
- Panel: T opens one "About Hearthwoven" page (I opened the AdventureBackpacks backpack); per-page explanations removed.
- Panel: exact pickups per session (the game's own counter skips pickups that land on a stack you carry: shown as "at least").
- Panel: Crafting shows gear only, food under Cooking; Building splits build pieces from groundwork; Woodcutting and Mining show what you picked up per kind; Battle and Skills overviews.
- Panel: the game's hover text and crosshair hide while the panel is open; smooth scrolling, no scroll jump when content nearly fits, soft inset fades.
- Build targets net472 (net462 pulled in System.ValueTuple 4.0.2.0, which Valheim lacks: the panel crashed on open).

## 0.2.0 (2026-10-07)
- First Hearthwoven build: in-game panel, measured events, group sharing, server-side chest and cart log.
