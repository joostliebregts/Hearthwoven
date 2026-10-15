using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hearthwoven
{
    /// <summary>
    /// Fighting time for damage per second (0.8.6, DPS in three spots: By weapon, the boss fight's group pace, a fight's own pace). It is seconds of
    /// fighting, not wall clock: from the first to the last hit of a stretch, where a gap of more than <see cref="Gap"/> between two hits ends the
    /// stretch (the time of the gap is not counted). The first hit of a stretch starts the clock and counts for no time, so its damage is left
    /// out too (<see cref="Pace"/> pairs the seconds with the damage of the hits that earned them): a rate worked out from N hits is the damage of
    /// N - 1 swings over the time between them. Pure C#, tested (test/SwingTests.cs).
    ///
    /// The gap, 4 seconds, is picked from the game's attack speeds as I know them (not read from the game's files): a one-handed swing takes about
    /// a second and a heavy two-hander (sledge, battleaxe, greatsword) about 2 to 3 seconds from one hit to the next, a bow at a low skill takes
    /// about 3 seconds to draw. 4 seconds holds the slowest normal rhythm in one stretch; a pause to heal, eat, run or block through a
    /// phase (usually 5 seconds or more) ends it.
    /// </summary>
    public sealed class SwingClock
    {
        public static readonly TimeSpan Gap = TimeSpan.FromSeconds(4);
        /// <summary>Under this many seconds of fighting a pace is not shown (a dash): two or three swings say little about a rate.</summary>
        public const float Floor = 5f;
        public static readonly string[] Kinds = { "melee", "bow", "magic" };

        readonly DateTime[] last = new DateTime[Kinds.Length];

        /// <summary>The index of a weapon kind (Kinds); a skill passes through DayHistory.WeaponKind.</summary>
        public static int KindOf(string skill)
        {
            var k = DayHistory.WeaponKind(skill);
            return k == "bow" ? 1 : k == "magic" ? 2 : 0;
        }

        /// <summary>
        /// The seconds a hit of this weapon kind adds at <paramref name="utc"/>: the time since this kind's previous hit when that was within Gap,
        /// else 0 (it starts a stretch). No allocation. A clock that went back, or the same instant, adds nothing.
        /// </summary>
        public float Hit(int kind, DateTime utc)
        {
            var s = Step(last[kind], utc); last[kind] = utc;
            return s;
        }

        /// <summary>The time from <paramref name="prev"/> to <paramref name="now"/> in seconds when it is positive and within Gap, else 0 (also when there was no previous).</summary>
        public static float Step(DateTime prev, DateTime now)
        {
            if (prev == default(DateTime) || now <= prev) return 0f;
            var d = now - prev;
            return d <= Gap ? (float)d.TotalSeconds : 0f;
        }

        /// <summary>What a hit swung: the eight battle types, as the Damage page's bars count it (tool damage is never battle).</summary>
        public static float SwungOf(HitData.DamageTypes t)
        {
            float s = 0f;
            void A(float v) { if (v > 0f && Json.IsFinite(v)) s += v; }
            A(t.m_blunt); A(t.m_slash); A(t.m_pierce); A(t.m_fire); A(t.m_frost); A(t.m_lightning); A(t.m_poison); A(t.m_spirit);
            return s;
        }

        /// <summary>Damage per second, or -1 (show a dash) when the seconds are under Floor.</summary>
        public static double Pace(double damage, double seconds) => seconds >= Floor && damage > 0 ? damage / seconds : -1;

        /// <summary>"42 DPS" (a whole number; "0.8 DPS" under 10 keeps a tenth); "" for no pace.</summary>
        public static string Words(double pace)
        {
            if (!(pace >= 0)) return "";
            return (pace < 10 ? Math.Round(pace, 1).ToString("0.#", CultureInfo.InvariantCulture) : Math.Round(pace).ToString("0", CultureInfo.InvariantCulture)) + " DPS";
        }

        /// <summary>The keys of a weapon kind's seconds and damage in a keyed map ("melee|s", "melee|d"), made once: a hit looks them up and allocates nothing.</summary>
        public static readonly string[] SecondsKeys = { "melee|s", "bow|s", "magic|s" }, DamageKeys = { "melee|d", "bow|d", "magic|d" };
    }
}
