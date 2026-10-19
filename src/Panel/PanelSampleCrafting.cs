using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 crafting-history (Chapters/CraftingHistory.cs, Chapters/DeedHistory.cs): Rowan's book (SampleWorld) with the records the crafting-history pages
    /// read: upgrades recorded from 2 Oct (record 1) with the game's max quality per item, firsts from 2 Oct (record 3), and a day history that reaches
    /// back 35 days (so History draws a full month of days, a few empty, and Weeks waits for its date). Variants for the previews and tests:
    ///   "upgrades"        the Upgrades per item island: four recorded (Iron Sword to 3 of 4, Bronze Axe and Wood Shield), seven more before 2 Oct;
    ///   "upgrades-empty"  recording began an hour ago, nothing recorded yet: the island's empty state with the eleven of the game;
    ///   "onekind"         a new character whose six pieces of gear are all armor, eleven upgrades: Joost's in-game Crafting of 18 Oct (fault 2);
    ///   "history"         the long day history with the firsts (the ticks) and the upgrades.
    /// Fictional numbers. Only these scenarios read it: the shared sample world stays as it is.
    /// </summary>
    public static class CraftHistorySample
    {
        static readonly Dictionary<string, int> MaxQualities = new Dictionary<string, int>
        {
            ["$item_sword_iron"] = 4, ["$item_axe_bronze"] = 4, ["$item_shield_wood"] = 3, ["$item_bow"] = 4, ["$item_helmet_leather"] = 4, ["$item_chest_leather"] = 4,
            ["$item_legs_leather"] = 4, ["$item_hammer"] = 1, ["$item_hoe"] = 1, ["$item_cultivator"] = 1, ["$item_trinketbronzehealth"] = 1,
        };

        public static PanelInput World(DateTime now, string variant)
        {
            var p = PanelSample.Full(now);
            p.MaxQuality = t => t != null && MaxQualities.TryGetValue(t, out var q) ? q : 0;
            var from = new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc);
            switch (variant)
            {
                case "upgrades-empty":
                    p.UpgradesFrom = now.AddHours(-1); p.Upgrades = new UpgradeLog { FromUtc = now.AddHours(-1) };
                    return p;
                case "onekind":
                    {
                        // a new character (Joost's in-game Crafting, 18 Oct): six pieces of gear, all of them armor, eleven upgrades (the game's counters)
                        foreach (var k in p.ItemsCrafted.Keys.Where(k => p.ItemKind?.Invoke(k) == "gear").ToList()) p.ItemsCrafted.Remove(k);
                        p.ItemsCrafted["$item_helmet_leather"] = 2; p.ItemsCrafted["$item_chest_leather"] = 2; p.ItemsCrafted["$item_helmet_iron"] = 2;
                        foreach (var s in new[] { "CraftWeapon", "CraftTool", "CraftTrinket" }) p.Character[s] = 0;
                        p.Character["CraftArmor"] = 6; p.Character["Upgrades"] = 11;
                        p.Baseline = null; p.ExactAtBaseline = null;
                        p.UpgradesFrom = from; p.Upgrades = Log(from, ("$item_helmet_leather", 1, 2, 2), ("$item_chest_leather", 1, 2, 3));
                        return p;
                    }
            }
            p.UpgradesFrom = from;
            p.Upgrades = Log(from, ("$item_sword_iron", 1, 2, 3), ("$item_sword_iron", 2, 3, 5), ("$item_axe_bronze", 1, 2, 6), ("$item_shield_wood", 1, 2, 6));
            p.FirstsFrom = from;
            p.Firsts = new Dictionary<string, DateTime>
            {
                ["$item_trinketbronzehealth"] = new DateTime(2026, 10, 6), ["$item_cultivator"] = new DateTime(2026, 10, 4), ["$item_hoe"] = new DateTime(2026, 10, 7),
                ["$piece_sharpstakes"] = new DateTime(2026, 10, 5), ["$item_mod_spicedcider"] = new DateTime(2026, 10, 3),
            };
            if (variant == "history") LongHistory(p, now);
            return p;
        }

        // the upgrades: (item, from, to, day of October), each in the evening
        static UpgradeLog Log(DateTime from, params (string token, int a, int b, int day)[] ups)
        {
            var log = new UpgradeLog { FromUtc = from };
            foreach (var u in ups) log.Add(new UpgradeMoment { Token = u.token, From = u.a, To = u.b, Station = "forge", StationLevel = 2, Utc = new DateTime(2026, 10, u.day, 19, 30, 0, DateTimeKind.Utc) });
            return log;
        }

        /// <summary>The day history from 3 Sep (35 days to the sample's today, 7 Oct): the September days play like the sample's week, each a sample day's
        /// row again (every fifth day nothing, a quiet stretch in the middle), so History holds a month of days and Weeks opens on 8 Oct.</summary>
        static void LongHistory(PanelInput p, DateTime now)
        {
            var h = p.History; if (h == null) return;
            var today = PanelModel.LocalToday(p);
            var first = today.AddDays(1 - DayHistory.DayRows);
            var days = h.Rows.Where(r => r.IsDay).OrderBy(r => r.Period, StringComparer.Ordinal).ToList();
            if (days.Count == 0) return;
            var start = days[0].Start;
            var added = new List<DayHistory.Row>();
            int k = 0;
            for (var d = first; d < start; d = d.AddDays(1), k++)
            {
                if (k % 5 == 3 || (k >= 12 && k <= 15)) continue;   // a day without play, and four quiet days in the middle of the month
                var row = new DayHistory.Row { Period = DayHistory.DayKey(d) };
                row.AddAll(days[k % days.Count]);
                added.Add(row);
            }
            h.Rows.InsertRange(h.Rows.FindIndex(r => r.IsDay), added);
            h.From = first;
            VariedGear(h, first);
        }

        // the sample's own days all made one piece of gear (one a play day, PanelSampleWorld), so History drew every played day as tall as the next. Here
        // each day makes what a real evening might: 0 to 6 pieces, several days of two or three kinds (weapons, armor, tools, trinkets), the same
        // pieces in the day's made items so a day's own page agrees
        static readonly (int weapons, int armor, int tools, int trinkets)[] GearDays =
        {
            (2, 0, 1, 0), (1, 0, 0, 0), (0, 2, 2, 1), (0, 0, 0, 0), (4, 1, 0, 0), (0, 0, 1, 0), (3, 2, 1, 0), (1, 1, 0, 0), (0, 0, 0, 0), (2, 0, 0, 0),
            (0, 3, 0, 0), (1, 0, 1, 1), (0, 0, 0, 0), (0, 0, 0, 0), (0, 0, 0, 0), (0, 0, 0, 0), (2, 2, 2, 0), (0, 1, 0, 0), (5, 0, 0, 1), (1, 0, 0, 0),
            (0, 0, 3, 0), (2, 1, 0, 0), (0, 0, 0, 0), (1, 4, 0, 0), (0, 0, 1, 0), (3, 0, 0, 0), (0, 2, 0, 1), (1, 1, 1, 0), (0, 0, 0, 0), (2, 0, 0, 0),
            (0, 1, 2, 0), (1, 0, 0, 0), (3, 1, 0, 1), (2, 0, 1, 0), (1, 1, 0, 0),
        };
        static readonly (string stat, string token)[] GearTokens =
            { ("CraftWeapon", "$item_axe_bronze"), ("CraftArmor", "$item_helmet_leather"), ("CraftTool", "$item_hammer"), ("CraftTrinket", "$item_trinketbronzehealth") };
        static void VariedGear(DayHistory h, DateTime first)
        {
            var gearMade = new HashSet<string>(new[] { "$item_sword_iron", "$item_shield_wood", "$item_axe_bronze", "$item_bow", "$item_helmet_leather", "$item_hammer", "$item_hoe", "$item_cultivator", "$item_trinketbronzehealth" });
            var byDay = h.Rows.Where(r => r.IsDay).ToDictionary(r => r.Period, r => r);
            for (int k = 0; k < GearDays.Length; k++)
            {
                if (!byDay.TryGetValue(DayHistory.DayKey(first.AddDays(k)), out var row)) continue;   // a day without play stays empty
                foreach (var (stat, _) in GearTokens) row.Game.Remove(stat);
                foreach (var t in row.Events.Made.Keys.Where(gearMade.Contains).ToList()) row.Events.Made.Remove(t);
                var g = GearDays[k]; var n = new[] { g.weapons, g.armor, g.tools, g.trinkets };
                for (int i = 0; i < GearTokens.Length; i++)
                    if (n[i] > 0) { row.Game[GearTokens[i].stat] = n[i]; row.Events.Made[GearTokens[i].token] = n[i]; }
            }
        }
    }
}
