# Hearthwoven

A Valheim mod that shows how everyone in your group contributes, each in their own way.

My friends and I play on a modded server. One of us builds for hours, another keeps the smelters running, someone
else cooks for everyone and hardly ever fights. The game counts a lot of this, but you never see it, and you never
see who ate your food or sailed under your helm. Hearthwoven makes that visible: in game, and on a page you can
open on your phone.

**Status: early.** Built and tested on a test server with 23 other mods. It has not been played on a real evening
yet. Expect rough edges, and please tell me about them in the issues.

## What you see

Press **H** in game. Six chapters:

- **Deeds**: what you make or gather: cooking, building, crafting, woodcutting, mining, farming, fishing, taming.
- **Company**: the people your evening crosses with. Who ate food you made, whose gear you equipped, who you
  sailed with. Both directions, no ranking.
- **Stores**: chests, carts and the trader.
- **Battle**: damage taken and dealt per biome, blocks and parries, deaths with what hit you in the last ten
  seconds, and a hint for next time ("poison was behind both deaths in the swamp: bring Poison Resistance Mead").
- **Voyages**: sailing, at the helm, on foot, maps shared at the cartography table.
- **Skills**: levels, and what you practised this session.

Keys: Q/E chapter, W/S list, A/D direction, I for how something was counted, H or Esc to close.

## What it counts, and how honestly

Every number says where it comes from: the game's own counters for your character, or what Hearthwoven measured
since you installed it. Older history is never presented as measured. The rules for "nothing counted twice,
nothing missed" are in [INTEGRITY.md](INTEGRITY.md), each with a test. An independent review tried to break them;
what it found is fixed and tested, apart from a few accepted risks listed there.

Some examples: dungeon loot is not counted as someone stocking a chest, a tombstone is not a gift to the group, and
a chest that a mod empties by itself (like a smelter feeder) does not count as your take. With MultiUserChest, the
mod reads that mod's own messages to see who moved what. It only reads; it never changes another mod.

## Sharing

You see your own stats. If you also want to see your friends' stats, you share yours: one setting,
`ShareWithGroup`, on by default. Turn it off and nobody sees yours, and you see only your own. Shared copies leave
out where you died and which worlds you played.

## Install

- **Server**: put `Hearthwoven.dll` in `BepInEx/plugins/`. It works without any player having the mod: it then
  records chests, carts and ships on its own.
- **Players**: optional, any time. Without the mod you notice nothing and you are never kicked. With it you get the
  panel and your measured stats.
- Not a dependency of anything, no Jotunn, no ServerSync, no version check. Removing it leaves nothing behind in
  your world.

The companion (the phone page and a daily picture for Discord) is a separate tool and comes later, as one command
that installs itself as a scheduled job.

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
