# Hearthwoven

Your own book of deeds in Valheim. Press **H** and see what your viking did: trees felled, ore carried home, food
cooked, foes fought, seas crossed. If your friends play with it too, you also see how each of you helps the group,
each in your own way.

## For you

Hearthwoven adds a book to the game. It has six chapters:

- **Deeds**: cooking, building, crafting, woodcutting, mining, farming, fishing, taming.
- **Company**: the friends you play with. Who ate the food you made. Whose gear you wear. Who sailed with you.
- **Stores**: chests, carts and the trader.
- **Battle**: damage dealt and taken per biome and per foe, blocks and parries, and what killed you.
- **Voyages**: sailing, time at the helm, distance on foot, maps you shared.
- **Skills**: every skill by level, and what you practised this session.

Each playstyle earns its own title: Woodcutter, Stonebreaker, Hearth Cook, Hallwright and more. There is no total
score and no ranking. A builder and a fighter both matter.

Keys: **H** open and close, **Q/E** chapter, **W/S** list, **A/D** direction, **T** about, **Esc** back.

## Where the numbers come from

Hearthwoven uses three sources. The About page in the book (press T) explains them too.

| Source | What | Since |
|---|---|---|
| Your character | The game itself keeps many counters: trees felled, pieces built, crafts, kills, skills. | The day you made your character. Full from the first minute. |
| Hearthwoven on your PC | What the game does not keep: damage per biome and foe, the cause of each death, exactly what you picked up, who ate your food. | The moment you install it. |
| The server | Who put what into which chest or cart, and who took it out. | The moment the server installs it. Works for every player, also without the mod. |

## For your group

On a server with friends, everyone plays differently. One builds for hours. One keeps the smelters running. One
cooks for everyone and hardly fights. Hearthwoven makes that visible.

- Players who install it share their book with the server. You see your friends' books, and they see yours.
  You see theirs only while you share yours. You can turn sharing off (`ShareWithGroup`). Shared copies leave out
  where you died and which worlds you played.
- The server keeps every session, so the history grows with every evening you play.
- The chest log works for all players, also for friends who do not install the mod.

## Install

- **Player**: install it with your mod manager (Gale, r2modman). It is optional: you can play on any server with it,
  and a server with it does not need you to have it.
- **Server**: put the same `Hearthwoven.dll` in the server's `BepInEx/plugins` folder. The server then stores the
  shared books and the chest log in `BepInEx/Hearthwoven/`.
- **Remove**: delete the DLL. Hearthwoven never changes your world.

## Tested with

We play with Hearthwoven on our own server with these mods, and test new versions against them on a copy of that server first:
Jotunn, AzuWearNTearPatches, Drop That!, ValheimArmory, OdinArchitect, MultiUserChest, AdventureBackpacks,
BetterArchery, BottleShips, ClayBuildPieces, CoreWoodPieces, FineWoodFurnitures, RefinedStonePieces, SeedBed,
SmelterUpgrades, XPortal, AdvancedTerrainModifiers, SailingModer, Instant Monster Drop, NetworkPerformanceSystem and
Server Devcommands. With MultiUserChest, Hearthwoven reads that mod's messages to see who moved what. It only reads;
it never changes another mod.

## Coming later

A companion tool that posts a daily picture of your group's evening to Discord, and a page you can open on your
phone. It will install with one command.

## Feedback

Something odd, a number that looks wrong, or an idea? Tell us on the
[GitHub issues page](https://github.com/joostliebregts/Hearthwoven/issues). Screenshots help a lot.

## Thanks

Ideas and hard lessons from other mods shaped this one:
[CrewStats and DamageMeter](https://thunderstore.io/c/valheim/p/Bagr/) (Bagr),
[DudeWhatAreMyStats](https://thunderstore.io/c/valheim/p/DeathMonger/DudeWhatAreMyStats/) (DeathMonger),
[ValheimSagas](https://thunderstore.io/c/valheim/p/Pendulum/ValheimSagas/) (Pendulum),
[CombatStats](https://thunderstore.io/c/valheim/p/Muindor/CombatStats/) (Muindor),
[GsValheimStatsClient](https://thunderstore.io/c/valheim/p/Proudlock_Technology/GsValheimStatsClient/) (Proudlock Technology),
[StatCreditFix](https://thunderstore.io/c/valheim/p/Qua8ion/StatCreditFix/) (Qua8ion),
[Almanac](https://thunderstore.io/c/valheim/p/RustyMods/Almanac/) (RustyMods),
[VikingStoryteller](https://thunderstore.io/c/valheim/p/JacobsValheim/VikingStoryteller/) (JacobsValheim),
[Skald](https://github.com/casmith/skald) (casmith) and
[valheim-save-research](https://github.com/Erhuangjing/valheim-save-research) (Erhuangjing).

## Building

Needs the .NET SDK and the game's own assemblies (from your Valheim install, not in this repo): see
[lib/README.md](lib/README.md). Then `dotnet build -c Release`, and `dotnet run --project test/KsTest.csproj` for
the tests.

Made with a lot of help from AI (Claude and Codex), steered and played by me.

## License

MIT
