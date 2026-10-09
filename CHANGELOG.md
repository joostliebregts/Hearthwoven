# Changelog

Every tweak that reaches a PC or the server gets its own patch version (Joost, 2026-10-08), so the version in the
log line and in Gale always says which build runs.

## 0.6.5 (2026-10-09, client release)
From the first evening with the Thunderstore install. Client only: the server stays on 0.6.0.
- **Your skill is back** on Woodcutting and Mining. Pages with time windows had stopped drawing it (the heading row held the windows); it now sits at the top of the page ("Wood Cutting · level 34").
- **Titles explained.** Feats has a Titles page: what a title is (a kind of work you do) and what a feat is (a moment you reach), every title you hold with its reason, the others greyed with what earns them. On a page a title says why ("Wallwarden · 3 defences built, armed or loaded") and opens its entry; it shows once (it was drawn twice in game) and not inside a short time window.
- **Calmer time windows.** No standing "Day history since…" line on every page: press a greyed 7 days or 30 days and one line says from when it works. An empty window says one thing, not three.
- **Keys.** Tab closes the book, as it closes your inventory; the filter key moves to K. An existing config that still has the old default Tab is switched to K once; if you choose Tab again, it stays.
- **Cooking has filters**: Type (meals, grilled, baked; feasts and mead bases when made) and Main boost (health, stamina, eitr).
- **Bars you can read.** A filter or composition bar shows at most eight parts in clearly different colours, the rest as "Other (n kinds)"; "Misc" and "Misc." are one category; the cultivator's pieces from PlantEverything count as plantings, not as buildings with "Beech Seeds" as material.
- **Mining** counts stone and ore: Leather Scraps are out (boars drop them too), Withered Bone and Chitin show apart as "Other pickaxe finds".
- **Smaller:** an amount between 0 and 1 says "under 1" instead of 0; a hit your ward took completely is no hit received; deaths keep up to the last 30 seconds of damage (the docs said 10).
- **A greyed window stays where you pressed it.** The "7 days works from…" line shows on that page only; another page, another chapter or reopening the book goes back to the last window that worked (Battle keeps its Session). The same on Company > Together.
- **One block count on Defence.** Shieldbearer's reason splits blocks and parries as the numbers below it do ("254 blocks · 58 parries").
- **Cooking's Other** sits last in its bars, in the quiet colour, as on Crafting and Building.
- **A hit your ward took completely** is no hit received in the short windows either (10 min to Session).
- **Modded tools:** a tool that holds buildings with a seed and a hoe among them no longer counts every building as planted.
- **Titles that do not fit** the strip at a page's top end in "+2 more", which opens the Titles page.

## 0.6.2 (2026-10-09, client release)
- Feats cards and details no longer show raw text formatting codes. All formatted text now goes through one helper, and a self-check catches any slip.
- The Feat tag above a page is no longer cut off.
- Building filters name the categories that other mods add, or group them under Other; no more bare numbers.

## 0.6.1 (2026-10-09, client release)
The client build of the 0.6 line. It needs a server on **0.6.0 or newer** to show what only the server counts (cargo loaded and unloaded, born near you); without one, everything else works and those two are left out.
- **Time windows everywhere.** Defence, Deaths, Foes, Sailing, Cargo, Woodcutting, Mining and Company > Together carry the same nine window chips as Battle, backed by a daily history this PC keeps (`dealtByDay` goes with the shared copy, so fellow players' windows work too). A day that has no history yet is greyed, never guessed. Foes and Defence open on this session.
- **Shorter caveats.** The long sentences about stacks you carry, trees and hits near other players and tames booked to the owner are one quiet line, "Earlier counts may be incomplete.", said once per zone at its bottom when several of its stats are marked; the full explanation is in About > What it reads.
- **Fishing** is Hooked, Caught and Got away, with "Fish picked up" from your own values; no "Other" anywhere, and the sample's kinds add up to the fish caught.
- **Fireside:** each count is a pill on its own thread; gifts along one line run in their own lanes and threads across the fire bend to the side that crosses fewest others; gear is counted as times put to good use, the same number on Fireside and Gear shared.
- **Feats** are their own chapter after Deeds (Earned, Unsung, Together). Unsung cards say why (counting since a date, no fellow shares yet, needs server 0.6, 0 so far); tiers appear one step at a time; dates read "or earlier" where they are worked out from fellow players.
- **Smaller:** the logo in the header; a soft rounded focus ring on the chosen feat or filter; ledger names no longer overlap their second line (TMP line-height); Foes headers tilted in one row; Cargo says "loaded" and "unloaded" and "Usual load aboard"; Together says since when each chip counts; the filter key is Tab.
- **Safer:** every Harmony hook runs through one guard, counts are never parsed with the system's culture, your own book is written atomically with a `.bak`, one file per character, and a file from a newer version is refused instead of overwritten. Fellow players are told apart by platform id, so two players with one name both show.

## 0.6.1, details (review round 4)
- Deeds and Battle: the long caveat sentences (stacks you carry, trees and hits near other players, tames booked to the owner) are now one quiet line, "Earlier counts may be incomplete.", said once per zone at its bottom when several of its stats are marked; About > What it reads says why, in plain words.
- Voyages > Cargo: the unit is said once in plain words ("item-km: 1 item carried 1 km"), the average loads read "100 Wood, aboard over 4.1 km", whole km beside whole km ("4" not "4.0" next to "1 430"), Heavy Keel's heaviest load is a labelled tile in the same grid, and Cargo loaded and unloaded has a zone of its own that says who counted and since when ("counted by the server since 8 Oct").
- Company > Fireside: each count is a small pill lying on its own thread, placed so that it touches no other count, shield, name, arrow head or the hearth (FireLayout.cs); each list says its scope once in its caption.
- Company > Together: every chip says since when it counts; F flips the category, Tab the window under Damage dealt, with their keycaps under the numbers. Gear shared: larger tiles.
- Cargo "sent" and "delivered" are now "loaded" and "unloaded" in the panel (the saved and shared data keep their keys); the average loads read "Usual load aboard: 100 Wood, over 4.1 km in all"; Ore Road (feat) is shown on Voyages > Cargo instead of Hall > Overview.
- Company > Fireside: gifts along one line run in their own lanes and the threads across the fire bend to the side that crosses fewest others. Gear shared: a count on every tile (times put to good use). Together: the rows without time windows say why ("No time windows for these: they are kept as totals").
- Voyages > Overview: home and away is one thin line under the journey. Maps: "Time far out, beyond 10 km from the centre". About > What it reads is shorter.
- About > Sharing leads with what fellow players see, how to switch sharing (ShareWithGroup), what stays private and where the counts go; the sharing-off notice sits in the chip row and points to it.

## 0.6.0 (2026-10-09, server build; not published)
The first build of the 0.6 branch, made to run on the server; it also carries everything under Next and Unreleased (feats) below.
- The server keeps a book of what only it can see whole, and sends each player their part with the group list (only while you share, like everything else in the group):
  - Voyages > Sailing: **Cargo loaded and unloaded**. For every ship and cart the server follows what goes in and what comes out, first in, first out. When cargo comes out somewhere else, the straight line between where it went in and where it came out, times the count, is credited as "sent" to whoever loaded it and as "delivered" to whoever unloaded it (item-km per item; two numbers, never added together). Chests do not count (storage, not transport); a change with nobody at the cart or ship credits nobody. The book starts with the chest logs the server already has (8 Oct on our own server), so the iron haul of that evening is in it.
  - Deeds > Taming: **Born near you**. Every tamed young animal the server sees appear for the first time within 40 m of you, whoever's PC hosted it. Near, not bred: the game does not record who fed them. Wild young are not counted.
  - Both sit outside the two zones with their own line saying the server counted them; "Cargo carried" (at the helm or pulling, this PC) and "Born in your care" stay beside them. Company > Together keeps Cargo carried as its cargo category.
- Faster group sharing: the server packs each fellow's copy once and sends only the copies that changed (every copy again every 5 minutes). Measured with 8 fellows and the panel open for 10 minutes: 81% less traffic, server time per request from about 2.1 ms to 0.3 ms. Nothing changes in the messages; older Hearthwoven versions keep working on both sides.
- Server files: `BepInEx/Hearthwoven/server-book-<world>.json` (saved with every world save, so a restart without a save rolls it back with the world) and `births-<date>.jsonl` (one line per birth).

## Next (not released)
- The filter key is now **Tab** (Panel.FilterKey), not G: G opens the radial menu of ZenDragon Zen.ModLib. While the panel is open, Tab enters the filter and does not also open the inventory (only then; with the panel shut, Tab opens the inventory as always). At startup the log says `filter key: Tab, conflicts: none` or names each other mod setting bound to the same key.
- Developer tool **Dev.SelfCheck** (off for players): one `HW-CHECK PASS/FAIL/WARN/INFO/WAIT` line in the log per thing that has never run in the real game (cargo at the helm and the cart, the voyage, animals led, births, the filter key, the last snapshot at logout), and on F12 a report with a walk through every page (text floor, focus ring, icons, scale). Only reads and logs.
- Battle's time windows are finer: last 10 minutes, last 30 minutes, last hour, last 3 hours, this session, and All (everything this PC counted since install, per biome too: the overview keeps its biome strip, death marks and biome choice; Damage dealt and Deaths on All have no biome choice). The chips are short; the plate's heading names the window in full. The view key (F) cycles them.
- Your own book on this PC is safer: one file per character (player id and name), so two characters made from one copied character file no longer mix their counts; an older file moves to its character only when exactly one character fits it, never merged. Every save keeps the previous one as a `.bak`, and a damaged file falls back to it instead of starting from zero. One odd value (NaN) can no longer reset "since install". Data a newer Hearthwoven adds to the file survives a downgrade to this version; a file from a newer format is left alone.
- Company > Together, with Damage dealt chosen, has the same windows: who dealt how much in the last fight, for you and for fellow players (their last-shared session's log).

## Next: server resilience (RESILIENCE-06, server side; not released)
- Server files are written whole: the per-player JSON, every session copy, the share copies and the server book go to a temp file flushed to disk, then replace the old one, which stays as `.bak`. An unreadable book falls back to its `.bak`.
- Input limits for snapshots: parts and messages are bounded, half-sent messages are dropped after 2 minutes (no slow memory leak), at most one snapshot per player per 20 seconds, at most 4 MB unpacked (a gzip bomb stops there), only one well-formed JSON object is stored. Each problem is logged once per player.
- The cargo book is rebuilt per world: the chest log markers name the world, so a new world never starts with the old one's cargo.
- Old daily logs are compressed in place after 30 days (`Server.CompressLogsAfterDays`), never deleted; the server and the companion read the compressed days too.
- Routed damage logging (`Server.LogRoutedDamage`) is off by default (Joost). A server config that already has it on keeps it on until changed.

## Unreleased (feats)
Feats: a few notable things done, each with a rule, a number and a moment.
- Deeds > Feats (right after Overview): Earned and Unsung. Each card shows the family's emblem, a gold rim once earned, the tier, and the day and biome it was earned. One fixed area under the cards shows the rule of the feat you point at (the mouse, or A/D): what it honours, every tier with the reached ones lit, how far along you are (your own page, exact counts only), how it is counted and what it cannot see.
- Known for on Deeds > Overview (up to three), a band on the page a feat belongs to (Shield Wall on Battle > Defence), a gold dot until you have seen what you earned. No popup, no ranking, no comparison.
- First feats: Full Table, Feast-Giver, Arms for the Hall, Ferryman (worked out from fellow players' copies, so the day is "by"), Kept the Fires, Shield Wall, Stood Fast, Unbroken, Turned Blades, Turned the Forsaken, Waymate. Ore Road, Heavy Keel, Drover, Long Lead and Born in Your Care are in the table and say Hearthwoven does not count them yet.
- Heavy Keel, Drover and Long Lead now count. Heavy Keel: the most metal and ore (ores, scrap and bars) carried for 2 km in all in one voyage at the helm (a voyage ends after five minutes away from the helm or on another ship); Voyages > Sailing shows that best load with its day and biome. Drover and Long Lead: the metres tamed animals walked or sailed while following you, counted every ten seconds and only while your PC hosted them (a step over 100 m is a teleport and counts for nothing; a lead ends when the animal has not followed for a minute); Deeds > Taming shows the total and the longest lead. Fellow players see your best load and longest lead (number, day, biome, animal; never a place).
- Shared: which feats you earned, with day and biome. Not your counters, not where you were.
- Fixed: the Hall's Smelters and Cooking's food axis stopped working from 1 000 items (a count with a no-break space was read back with a comma).

## 0.5.0 (2026-10-08)
A new look for the whole book.
- Six chapters: Deeds, Company, Hall (was Stores), Battle, Voyages and Skills.
- One form per question, the same on every page: a big number for how much, one bar of parts for what it is made of, a biome strip for where, a ladder for each skill, threads around the hearth for who enjoyed what you made.
- The game's own icons for items, foes and skills.
- Since-install numbers now add up across sessions for each character and survive a restart (kept on your PC in `BepInEx/Hearthwoven/local/`). Battle also shows recent fights: last hour, three hours, this session.
- Other mods' items show too: items, pieces and creatures that other mods add appear with their own name and icon.
- Two kinds of number, told apart by place: a cool stone panel for your character's own counts (the game keeps them in every world) and a warm ember panel for what Hearthwoven counted since you installed it. Where a bar or number has a faded part, that part is from before you installed (the chip "faded = before install" says so).
- Woodcutting and Mining: wood, stone and ore brought in as one bar with each kind in its own colour; trees felled, axe hits and pickaxe hits per tree or rock, with the tree or rock pictured. Woodcutting fits without scrolling.
- Cooking counts the dishes you make yourself, whichever station you use; Farming shows planted and picked per crop; Fishing, Taming, Building, Groundwork (raise, lower, level, paths, cultivating; split off Building so the piece list cannot push it out of sight) and Crafting got pages of their own; items are compact tiles you can scroll.
- Battle: hits on foes and deaths over the whole of your play, how each death built up over its last 30 seconds, blocks and parries side by side. Foes shows what each foe is weak against and the best arrow you know for it.
- Hall: Trader and Smelters on their own pages, one row per trader or smelter with what you bought or put in. Company: Fireside, Together, Food shared and Gear shared.
- Shorter, plainer headings; every empty page says "Nothing yet" the same way; British spelling (Defence).
- About (T) explains where each number comes from and what "since install" means.
- The creature health bar hides while the panel is open. Changes to the settings (Panel.Scale, keys) apply while you play.
- Keys: H opens and closes, Q/E, A/D or the arrow keys change chapter, W/S moves through the list, F switches the view, T opens the About page, Backspace goes back.

## 0.2.1 (2026-10-08)
- Server: player stats are filed under the player's real id (the game never fills `ZNetPeer.m_playerID`; it stayed 0 for everyone). JSON written without a BOM.
- Panel: T opens one "About Hearthwoven" page (I opened the AdventureBackpacks backpack); per-page explanations removed.
- Panel: exact pickups per session (the game's own counter skips pickups that land on a stack you carry: shown as "at least").
- Panel: Crafting shows gear only, food under Cooking; Building splits build pieces from groundwork; Woodcutting and Mining show what you picked up per kind; Battle and Skills overviews.
- Panel: the game's hover text and crosshair hide while the panel is open; smooth scrolling, no scroll jump when content nearly fits, soft inset fades.
- Build targets net472 (net462 pulled in System.ValueTuple 4.0.2.0, which Valheim lacks: the panel crashed on open).

## 0.2.0 (2026-10-07)
- First Hearthwoven build: in-game panel, measured events, group sharing, server-side chest and cart log.
