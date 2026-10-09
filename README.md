<!-- SCREENSHOT PLACEHOLDER (banner, not in the public docs/ yet): replace this comment with
[![Hearthwoven](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/hearthwoven-banner-woodcut.png)](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/hearthwoven-banner-woodcut.png) -->

# Your own way of playing

One player mines iron. Another sails it home. Someone else cooks for the next trip. Everyone plays Valheim in their own way.

Hearthwoven is a book of your deeds. See the food you cooked, the things you built and the damage you dealt. Press **H** to open it.

[Installation](#installation) · [Reference](#reference) · [Feedback](#feedback)

## Your deeds, at a glance

See your cooking, building and other deeds in one place, under titles such as Hearth Cook, Hallwright and Woodcutter. The overview also shows what you are known for: up to three of your feats. Open a deed to see what went into its total.

<!-- SCREENSHOT PLACEHOLDER (SHOTS.md S1): replace this comment with
[![Deeds: titles for different playstyles](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s1-deeds-overview.png)](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s1-deeds-overview.png)
*Deeds overview. Select any image to open it at full size.* -->

## Look closer

- **Cooking:** the dishes you cooked, where you cooked them and who enjoyed your food.
- **Building and crafting:** what you made, with filters for type and material.
- **Woodcutting and mining:** trees felled, axe and pickaxe hits, and the wood, stone and ore you brought in.
- **Taming:** young born in your care, young born near you and the animals you led.
- **Battle:** damage dealt and received, by biome, foe and damage type. See your blocks, parries and what killed you.
- **Company:** who enjoyed your food, who put your gear to good use and the cargo each of you carried.

Select a biome or a time period to see more detail. Time periods run from the last 10 minutes to everything since install.

<!-- SCREENSHOT PLACEHOLDER (SHOTS.md S4): replace this comment with
[![Battle: damage dealt and received in each biome](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s4-battle.png)](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s4-battle.png)
*Battle overview, since install, by biome.* -->

<details>
<summary>Feats: recognition for your deeds</summary>

Feats have their own chapter. A feat recognises a specific achievement: a rule, a number and a day. Open it to see the rule, your progress and when it was first recorded.

- **Heavy Keel:** steer a ship 2 km with a heavy load of metal or ore aboard.
- **Shield Wall:** hold blows on your shield with a fellow player nearby.
- **Drover:** lead tamed animals over a distance.
- **Kept the Fires:** put ore and fuel into smelters, kilns and furnaces.
- **Full Table:** have your food enjoyed by three fellow players who share their books.

Some feats have several levels, marked bronze, silver and gold. Each level has its own target. The targets may change after the first weeks of play.

**Earned** shows the feats you have. **Unsung** shows the feats still ahead, and says why each one is not earned yet. **Together** shows Iron for the Forge: the ore and metal the whole group brings home. It needs Hearthwoven 0.6.0 or newer on the server.

<!-- SCREENSHOT PLACEHOLDER (SHOTS.md S2): replace this comment with
[![Heavy Keel: an earned feat](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s2-feats.png)](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s2-feats.png)
*Feats, Earned, with Heavy Keel selected.* -->

</details>

<details>
<summary>More detail: woodcutting and cargo</summary>

### A deed, up close

See how many trees you felled, how many axe hits each tree took and which types of wood you brought in. On the Crafting and Building pages, you can filter by type and material.

<!-- SCREENSHOT PLACEHOLDER (SHOTS.md S3): replace this comment with
[![Woodcutting: trees felled and wood brought in](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s3-woodcutting.png)](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s3-woodcutting.png)
*Woodcutting.* -->

### Carry cargo together

See the cargo you carried while steering a ship or pulling a cart.

Cargo is measured in **item-km**: one item carried one kilometre. The distance is measured in straight sections, so winding routes can be undercounted. Company > Together shows each player's share side by side.

With Hearthwoven 0.6.0 or newer on the server, Cargo also shows what you loaded into a ship or cart and what you unloaded somewhere else. Chests do not count.

<!-- SCREENSHOT PLACEHOLDER (SHOTS.md S5): replace this comment with
[![Company: each player's share of the cargo](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s5-together-cargo.png)](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s5-together-cargo.png)
*Sample data. Rowan, Edda, Finch and Tor are fictional players.* -->

</details>

<details>
<summary>Food and gear shared with fellow players</summary>

See who enjoyed your cooking and put your gear to good use. Company > Fireside shows these connections around the hearth. Each count sits on its own thread, from the player who gave to the player who received.

<!-- SCREENSHOT PLACEHOLDER (SHOTS.md S6): replace this comment with
[![Company: food and gear shared between players](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s6-fireside.png)](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/s6-fireside.png)
*Sample data. Rowan, Edda, Finch and Tor are fictional players.* -->

</details>

## Installation

Install Hearthwoven with your mod manager, such as Gale or r2modman. Start the game, then press **H** to open your book.

Your own book works on any server. Hearthwoven is optional. Players without it can still join and play.

**Other mods' items show too.** Hearthwoven reads the game's lists of items, building pieces, creatures and skills. Content from other mods appears with its own names and icons. Hearthwoven does not change other mods. See **Tested with other mods** under Reference for the mods we use in testing.

### Play with friends

Install Hearthwoven on the server to share books. Players who install it share their book by default.

- **ShareWithGroup** controls sharing with other players. Turn it off to see only your own book and stop sharing it with them.
- **SendStats** controls statistics updates to the server, even when **ShareWithGroup** is off. Turn both settings off to stop new updates and sharing.
- Shared copies leave out death locations and your history of worlds played.
- The server also records who adds or removes items from chests and carts. This includes players without Hearthwoven.
- Cargo loaded and unloaded, young born near you and Iron for the Forge need Hearthwoven 0.6.0 or newer on the server. An older server works for everything else.

<details>
<summary>Server setup, saved data and removal</summary>

### Server setup

Put `Hearthwoven.dll` in the server's `BepInEx/plugins` folder. Use the same file as the client.

The server keeps shared statistics and chest and cart records in `BepInEx/Hearthwoven/`.

A world you host from the game works the same way: your own copy of Hearthwoven is then the server.

### Saved data

Your local totals are in `BepInEx/Hearthwoven/local/` on your PC: one file for each character, with a backup copy. If a file is damaged, Hearthwoven reads the backup.

### Remove Hearthwoven

Remove it in your mod manager. Hearthwoven does not change the world. To remove everything, also delete the `BepInEx/Hearthwoven` folder and the `BepInEx/config/com.joostliebregts.hearthwoven.cfg` file. An update keeps both.

</details>

## Reference

Open a section for controls, settings or more detail about the numbers.

<details>
<summary>All seven chapters</summary>

- **Deeds:** your titles and what you are known for, with pages for cooking, building, groundwork, crafting, woodcutting, mining, farming, fishing and taming.
- **Feats:** Earned, Unsung and Together. The feats you have, the feats still ahead and one feat for the whole group.
- **Company:** Fireside, Together, Food shared and Gear shared. Your fellow players, who enjoyed your cooking, who put your gear to good use and the cargo each of you carried.
- **Hall:** the shared household, including trader purchases and items put into smelters.
- **Battle:** Overview, Damage, Defence, Deaths and Foes. Damage dealt and received, blocks, parries and deaths. Filter by biome and choose a time period.
- **Voyages:** Sailing, Cargo, On foot and Maps. Distance on foot and at sea, time at the helm, cargo, your heaviest load and maps shared.
- **Skills:** a ladder for each skill. A glow marks skills you practised since you installed Hearthwoven. Woodcutting, Mining, Cooking, Fishing, On foot and Battle > Damage also show the skills that belong to them.

</details>

<details>
<summary>Keys</summary>

| Key | Action |
|---|---|
| **H** | Open or close the book. |
| **Q/E** | Go to the previous or next chapter. |
| **A/D**, or **Left/Right arrow** | Go to the previous or next chapter. On the Feats page, select a feat. |
| **W/S**, or **Up/Down arrow** | Move through the page list. |
| **F** | Change the view, or the time period on pages that have one. |
| **Tab** | Open or leave the filters on pages that have them. On Company > Together, change the time period under Damage dealt. |
| **T** | Open the About page. |
| **Backspace** | Return to the previous page. |
| **Esc** | Leave filters or About first; otherwise close the book. |

Inside filters, use **A/D** and **W/S** to move, **Enter** or **Space** to choose, and **Delete** to clear the filters. The book remembers your filters for each page and each character. While the book is open, **Tab** does not open the inventory.

</details>

<details>
<summary>Settings</summary>

Use the config editor in your mod manager, such as Gale or r2modman. Changes apply while the game runs.

| Section | Setting | Default | Action |
|---|---|---|---|
| Client | **SendStats** | On | Send new statistics updates to the server, even when ShareWithGroup is off. |
| Client | **IntervalMinutes** | 5 | Set the time between updates, in minutes. Updates also occur on spawn, logout and quit. |
| Client | **ShareWithGroup** | On | Share books with fellow players. When off, you see only your own book and they cannot see yours. |
| Panel | **Enabled** | On | Allow the hotkey to show the book. |
| Panel | **Hotkey** | H | Set the key to open and close the book. |
| Panel | **InfoKey** | T | Set the key for the About page. |
| Panel | **ViewKey** | F | Set the key to change the view. |
| Panel | **FilterKey** | Tab | Set the key to open and leave filters. |
| Panel | **Scale** | 1.0 | Set the book's size, from 0.8 to 1.3. Use 1.0 for 1920 × 1080. |

</details>

<details>
<summary>Sharing and server history</summary>

Players who install Hearthwoven can share their books through the server. You can see other players' books only while you share yours.

The **ShareWithGroup** setting is on by default. Turn it off to see only your own book and stop sharing it with other players.

**SendStats** controls new statistics updates to the server. **ShareWithGroup** controls whether fellow players can see your book. These settings work separately.

To stop new updates and sharing, turn both settings off. Turning off **SendStats** alone does not withdraw an earlier shared copy. An update already being sent can still finish.

When the server runs Hearthwoven, it saves the statistics sent to it. Turning these settings off does not delete its saved history. A server without Hearthwoven does not save this history.

What is shared: your counts and the feats you earned, with the day and biome, never a place. Shared copies leave out death locations and your history of worlds played. They still include the name of the current world.

Fellow players are told apart by their Steam or Xbox ID, so two players with the same name both appear. On a world you host from the game, you share and see fellow players the same way. In single player, you see only your own book.

The server records who puts items into chests or carts and who takes them out. It also records these actions for players without the mod. The Discord companion (coming later) will use these records to tell the story of your sessions.

</details>

<details>
<summary>Where the numbers come from</summary>

### Your character

Valheim keeps counters for trees felled, pieces built, dishes cooked, foes defeated and skill levels. These cover the character's history across worlds, subject to the gaps described below.

The book shows these values under **Your character**. A total also says how much of it came since install, for example: "410 trees felled in all · 30 of them since install".

### Since install

Hearthwoven records details that Valheim does not. These include damage by biome and foe, items brought in, axe and pickaxe hits, trader activity, smelter activity, cargo, animals led and shared food.

These counters start at zero when you install Hearthwoven. They add up across sessions for each character. The book shows them under **Since install**.

### Time periods

All Battle pages, Sailing, Cargo, Woodcutting, Mining and Company > Together have time periods: the last 10 minutes, 30 minutes, hour and 3 hours, this session, today, the last 7 days, the last 30 days and everything since install. The day periods use a day history on your PC. A period without history yet shows greyed. The book does not guess.

### Fellow players and the server

Other players' data comes from their own copy of Hearthwoven, shared through the server. On a fellow player's book, the time periods they do not share show greyed, with the reason.

With Hearthwoven 0.6.0 or newer, the server also counts what only it can see in full: cargo loaded and unloaded, young born near you and the group's Iron for the Forge. "Born near you" means near, not bred: the game does not record who fed the animals.

### Gaps in Valheim's counters

Hearthwoven adds its own counts for these cases, starting when you install it:

- **Work near a friend.** One player's PC runs each area of the world. Trees you fell and animals you tame in an area run by a friend's PC can be missing from your character's counters.
- **Item stacks.** Valheim does not count an item when you pick it up and add it to a stack you already carry.

Where this can affect a number, the book says so in one short line: "Earlier counts may be incomplete." The About page explains why.

</details>

<details>
<summary>Performance</summary>

Hearthwoven records extra data on top of what the game already counts, so it can affect performance. It reads and counts game activity. It does not change the world.

To keep the cost small, Hearthwoven does the following.

### On your PC

- It counts actions the game already performs, such as hits, pickups and crafting. Each count works on its own. If a game update or another mod breaks one, only that count stops.
- It checks the biome at most once a second. It checks ships, carts and the animals you lead every 10 seconds.
- It keeps one running total for each kind of count. The battle log combines fights older than about 3 hours into 10-minute steps.
- It builds the book the first time you open it. While the book is open, it updates the page every 2 seconds. While the book is closed, it only checks for the key.
- It sends a statistics update every five minutes, and on spawn, logout and quit. For our test character, an update is about 25 KB, or about 8 KB compressed. Updates are sent in small parts.

### On the server

- It checks chests and carts only when they change.
- It packs each fellow player's book once and sends it again only when it changes. With 8 players and the book open, this cut the traffic by 81%.
- It refuses an update over 600 KB and accepts at most one regular update per player every 20 seconds.

### Testing

We test on a dedicated server with the mods listed under **Tested with other mods**. Automated checks cover the counters, sharing and interface logic. A self-test runs inside Valheim on a copy of that server. It checks startup and the time spent on server operations. We also read the server's logs after a group evening.

The cost per frame on your PC is not measured yet.

</details>

<details>
<summary>Tested with other mods</summary>

We play with these mods on our server. We test new Hearthwoven versions with them on a copy of the server first.

Jotunn, AzuWearNTearPatches, Drop That!, ValheimArmory, OdinArchitect, MultiUserChest, AdventureBackpacks, BetterArchery, BottleShips, ClayBuildPieces, CoreWoodPieces, FineWoodFurnitures, RefinedStonePieces, SeedBed, SmelterUpgrades, XPortal, AdvancedTerrainModifiers, SailingModer, Instant Monster Drop, NetworkPerformanceSystem and Server Devcommands.

For MultiUserChest, Hearthwoven reads that mod's messages to find who moved an item. It does not change the mod.

</details>

<details>
<summary>One question, one form: how to read the book</summary>

Each question gets one form, the same on every page. A number shows the total. A bar shows its parts. A strip of biomes shows where. A ladder shows each skill. A connection shows who helped whom. Items, foes and skills carry the game's own icons.

<!-- SCREENSHOT PLACEHOLDER (design-language, not in the public docs/ yet): replace this comment with
[![Examples of the book's visual forms](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/design-language.png)](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/design-language.png)
*Design examples, not a game screenshot.* -->

</details>

<!-- SCREENSHOT PLACEHOLDER (SHOTS.md G1): replace this comment with
[![Book walkthrough](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/g1-book.gif)](https://raw.githubusercontent.com/joostliebregts/Hearthwoven/main/docs/screenshots/g1-book.gif)
*Sample data. Rowan, Edda, Finch and Tor are fictional players.* -->

## Coming later

A Discord companion that shows what happened on the server while you were away.

## Feedback

Something odd, a number that looks wrong, or an idea? Tell us on the GitHub issues page (link follows when the repository is public). Screenshots help.

## Thanks

Hearthwoven learned from other statistics mods. See the credits in the source repository (link follows when the repository is public).

AIL-4: idea, design and every decision by me; code, text and art made with AI.
