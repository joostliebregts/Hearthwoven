using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Damage per second in three spots (0.8.6, Joost: DPS only where it helps you play, never a live ranking of players): each weapon on Battle >
    /// Damage > By weapon, the group's pace on a boss fight, and your own pace in the head line of a fight (Last fight, the Feed). Never on Everyone
    /// and never in a table or row of players. Seconds of fighting, not wall clock: SwingClock says how they are counted. Your own book only: a
    /// fellow's book shows none (their fighting time is not in what they share, except the boss fight's group seconds, which is the group's).
    ///
    /// 0.8.6 per named weapon (Chapters/WeaponRows.cs): under each kind a line per weapon and per upgrade level, with its DPS and your skill level then.
    ///
    /// Blocks: a By weapon row (dmgmix) carries its pace in Value2, the chip after its name ("42 DPS"; none under the floor, as its weapon lines). A Feed fight
    /// carries it in Rate (its head line, beside dealt and received). A fight page's head has it as a third number beside dealt and received ("38",
    /// "your DPS"; a boss fight "85", "DPS together"), and a live boss fight's lane adds the time left at this pace (its Note).
    /// </summary>
    public static partial class PanelModel
    {
        public const string DpsAboutTitle = "DPS (damage per second)";
        public const string DpsDash = "– DPS";   // under SwingClock.Floor seconds of fighting
        public const string DpsWeapon = "weapon", DpsFight = "fight", DpsBoss = "boss";
        public const string BossPaceWord = "together";

        /// <summary>The fighting seconds and the damage swung in them per weapon kind ("melee", "bow", "magic") in a window, or null when this book
        /// has none to give: a fellow's book, a biome choice (the fighting time has no biome), a day window before the ledger. All, Session and
        /// the days come from the dealt ledger, 10 min to 3 h from the log's minutes; the damage is the one earned in those seconds.</summary>
        public static Dictionary<string, (double seconds, double damage)> WeaponSwing(PanelInput input, TimeWindow w)
        {
            if (input == null) return null;
            var r = SwingClock.Kinds.ToDictionary(k => k, k => (seconds: 0.0, damage: 0.0));
            if (!input.IsSelf)   // a friend's book: their kind rows from the weapons their copy shares (WeaponCellsIn), as the lines under them read them
            {
                var theirs = WeaponCellsIn(input, w);
                if (theirs == null) return null;
                foreach (var kv in theirs.Map)
                    if (WeaponKey.Split(kv.Key, out _, out _, out var skill)) { var k = SwingClock.Kinds[SwingClock.KindOf(skill)]; var (s, d) = r[k]; r[k] = (s + kv.Value.Sec, d + kv.Value.Swung); }
                return r;
            }
            if (w == TimeWindow.SinceInstall || IsDayWindow(w) || w == TimeWindow.Session && input.Log == null)
            {
                var t = DealtLedgerIn(input, w);
                if (t == null) return null;
                foreach (var k in SwingClock.Kinds) { var (s, d) = t.SwingOf(k); r[k] = (s, d); }
                return r;
            }
            if (input.Log == null) return null;
            var from = SessionFrom(input);
            foreach (var kv in input.Log.Swing)
            {
                var p = kv.Key.Split('|');   // minute|kind|s or d
                if (p.Length != 3 || !r.ContainsKey(p[1]) || !InFilter(p[0], "", w, "", input.NowUtc, input.Log.Span, from)) continue;
                var (s, d) = r[p[1]];
                r[p[1]] = p[2] == "s" ? (s + kv.Value, d) : (s, d + kv.Value);
            }
            return r;
        }

        /// <summary>The By weapon rows with their pace in Value2 (the chip after the name): "42 DPS", or no chip while the weapon has fewer than
        /// SwingClock.Floor seconds in the window. Nothing at all when the window has no fighting time recorded (before this was counted, a biome
        /// choice, a fellow's book). Returns whether any chip was set (the page then explains DPS once, DpsAbout).</summary>
        public static bool WeaponPaces(PanelInput input, TimeWindow w, IEnumerable<Block> mixes, bool narrowed)
        {
            if (narrowed) return false;
            var swing = WeaponSwing(input, w);
            if (swing == null || swing.Values.All(v => v.seconds <= 0)) return false;
            foreach (var m in mixes)
            {
                if (!swing.TryGetValue(m.Id ?? "", out var v)) continue;
                var words = SwingClock.Words(SwingClock.Pace(v.damage, v.seconds));
                m.Value2 = words.Length > 0 ? words : null;   // 0.8.6 fold (Joost: no "- DPS"): no chip until the kind has SwingClock.Floor seconds of fighting
            }
            return true;
        }

        /// <summary>The words of the DPS box in About these numbers: what the number is, how the seconds are counted and why that gap, and (All only,
        /// rule W.1) the day it counts from.</summary>
        public static string DpsAbout(PanelInput input, TimeWindow w, string scope = DpsWeapon, bool live = false)
        {
            // the day a count began is said on All and on a day window that reaches back before it (the weapons' lines and the pace start with the first 0.8.6 evening)
            var from = input != null && input.IsSelf && (w == TimeWindow.SinceInstall || IsDayWindow(w) && input.SwingFromUtc.HasValue && Local(input, input.SwingFromUtc.Value).Date > DayFrom(input, w)) ? input.SwingFromUtc : null;
            return (scope == DpsBoss ? "The number is the damage of the group to the boss for each second that the boss was under attack, not for each second on the clock. "
                  : scope == DpsFight ? (input == null || input.IsSelf ? "The number is the damage you swung for each second that you were fighting" : "The number is the damage " + Name(input) + " swung for each second that " + Name(input) + " was fighting") + ", with any weapon, not for each second on the clock. "   // 0.8.6: a friend's Last fight, theirs
                  : "The number is the damage you swung for each second of fighting, not for each second on the clock. ") +
                   "A stretch of hits runs from its first hit to its last. A pause of more than " + (int)SwingClock.Gap.TotalSeconds + " seconds ends it and does not count: a heavy swing takes 2 or 3 seconds, and a drawn bow about 3. " +
                   "The first hit of a stretch starts the clock, so its damage is left out. " +
                   (scope == DpsWeapon ? "Each kind of weapon has its own clock. A weapon is counted by name and upgrade level; the skill level is yours when you swung it (the lowest to the highest, when it changed). " : "") +
                   (scope == DpsWeapon ? "A weapon's DPS shows after " + (int)SwingClock.Floor + " seconds of fighting with that weapon; until then its place stays empty. A weapon used at several upgrade levels is one line, at its newest level; press it to see each level. "
                                       : "With less than " + (int)SwingClock.Floor + " seconds of fighting the number is a dash. ") + "The damage is before the armor of the foe." +
                   (scope == DpsBoss && live ? " At this pace is the time left if the boss keeps losing health as it did in the last minute." : "") +
                   (from.HasValue ? " Counted from " + RecordDate(input, from.Value) + "." : "");
        }

        /// <summary>A fight's own pace, "38 DPS": your fighting seconds and the damage swung in them. "" while nothing is recorded (a fight from before
        /// this was counted, a single hit); a dash under the floor.</summary>
        public static string FightPace(double seconds, double damage) =>
            !(seconds > 0) ? "" : SwingClock.Words(SwingClock.Pace(damage, seconds)) is var w && w.Length > 0 ? w : DpsDash;

        /// <summary>A boss fight's group pace: the group's damage to the boss over the seconds it was under attack ("85 DPS"); "" when not known, a dash under the floor.</summary>
        public static string BossPace(FightView f)
        {
            if (f == null || f.Kind != FightKind.Boss) return "";
            return FightPace(f.SwingSeconds, f.Foes.Where(x => x.Boss).Sum(x => x.DealtTotal));
        }

        /// <summary>A live boss fight's time left at the pace of the last minute, from its health lane ("1:40"): the health left divided by what it lost in
        /// the last minute (about 60 s, the lane's own span). "" when it lost under 1 % (no pace to go by) or the time is over 30 minutes.</summary>
        public static string TimeLeftAtPace(double left, double minuteAgo)
        {
            var down = minuteAgo - left;
            if (!(left > 0) || down < 0.01) return "";
            var seconds = (int)Math.Round(left * 60.0 / down);
            if (seconds > 30 * 60) return "";
            return (seconds / 60).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
