using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 developer tool (Armor sets, Chapters/ArmorSets.cs): the console command <c>hearthwoven_armorsets</c> writes every armor item of the
    /// game's data, with the set Hearthwoven puts it in, to <c>BepInEx/Hearthwoven/local/armor-sets.tsv</c> (tab-separated: token, name, slot,
    /// the game's set name and size, set bonus, recipe station and level, main material and ingredient, land, Hearthwoven's set), so the grouping
    /// can be checked in game. Reads only; no cheat, no devcommands needed; registered with the game's own console when it starts (Terminal.InitTerminal).
    /// </summary>
    [HarmonyPatch(typeof(Terminal), "InitTerminal")]
    static class ArmorSetsCommand
    {
        public const string Command = "hearthwoven_armorsets", FileName = "armor-sets.tsv";
        static bool added;

        static void Postfix() => HookGuard.Run(() =>
        {
            if (added) return;
            added = true;
            new Terminal.ConsoleCommand(Command, "Hearthwoven: write every armor item and the set it belongs to (BepInEx/Hearthwoven/local/" + FileName + ")", args =>
            {
                try
                {
                    var pieces = GameData.ArmorPieces();
                    if (pieces == null) { args.Context?.AddString("Hearthwoven: the game data is not read yet. Try again in a world."); return; }
                    var dir = Path.Combine(Paths.BepInExRootPath, Path.Combine("Hearthwoven", "local"));
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, FileName);
                    File.WriteAllText(path, PanelModel.ArmorDump(pieces));
                    args.Context?.AddString("Hearthwoven: " + pieces.Count + " armor items written to " + path);
                    Debug.Log("[Hearthwoven] armor sets written to " + path);
                }
                catch (Exception e) { args.Context?.AddString("Hearthwoven: could not write the armor sets: " + e.Message); }
            });
        });
    }
}
