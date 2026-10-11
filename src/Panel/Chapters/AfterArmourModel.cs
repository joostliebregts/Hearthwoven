using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8.5, Battle > Damage (By weapon, By type, By foe) and Foes: what your hits did after the foe's resistances and armor, in the bars that
    /// show what you swung (AfterArmour.cs works it out at each hit). One rule, the one Defense's What your armor stopped reads by: a bar is what
    /// you swung, as before; in each part the filled piece is what got through, the outlined piece what the foe's armor and resistances stopped
    /// (DamageStoppedTone). A foe weak to a type takes more than you swung: that part is all filled and its number is what got through. Hits from
    /// before the after was recorded (a window reaching back past the ledger's start, an older Hearthwoven's minutes) are the part's faded piece,
    /// first, with no number: they were never split. The row's line ends with its share that got through ("73 % got through").
    /// Where the after comes from per window: 10 min .. Session the log's After (shared with fellows, so their short windows have it too), All and
    /// the day windows the dealt ledger (DealtLedgerIn, your own book only).
    /// </summary>
    public static partial class PanelModel
    {
        public const string ThroughWords = "got through";
        /// <summary>The strip caption of Damage and Foes when the window's hits carry their after (DealtQualifier without it).</summary>
        public const string DealtAfterCaption = "before and after the foe's armor";   // the heading says damage dealt; a fellow's freshness needs the room
        /// <summary>The faded piece's words on hover where no date is said (rule W.1: every window but All; a fellow's book; an older Hearthwoven's minutes).</summary>
        public const string NotSplitWords = "from before this was counted";

        /// <summary>The About these numbers line of Damage and Foes (once, with the strip caption: the two places that say what before and after are).</summary>
        public const string AfterAboutTitle = "After the foe's armor";   // never "Before ..." nor "From ...": those titles are a box's dated lines (rule W.1)
        /// <summary>Its words; the date the after is counted from on All only (rule W.1: a shorter window dates nothing).</summary>
        public static string AfterAbout(PanelInput input, TimeWindow w)
        {
            var from = w == TimeWindow.SinceInstall && input != null && input.IsSelf ? input.DealtArmourFromUtc : null;
            return "Each bar is the damage you swung, before the armor of the foe. The filled piece got through its armor and resistances. The outlined piece is what they stopped. " +
                   "A foe that is weak to a type takes more than you swung: that piece is all filled, and its number is higher. " +
                   (from.HasValue ? "Counted from " + RecordDate(input, from.Value) + ": a faded piece is damage from before then. " : "A faded piece is damage from before this was counted. ") +
                   "Hearthwoven works it out on your PC at each hit, with the numbers of the game for that foe. A sneak attack or a hit on a staggered foe does more damage; neither number counts that.";
        }

        /// <summary>
        /// The dealt ledger in a window, your own book only (null on a fellow's and for 10 min .. 3 h, whose rows come from the log with its After):
        /// All everything recorded on this PC, Session this session, a day window the book's day rows plus the running session's unsaved part.
        /// </summary>
        public static ArmourTally DealtLedgerIn(PanelInput input, TimeWindow w)
        {
            if (input == null || !input.IsSelf) return null;
            if (w == TimeWindow.SinceInstall) return input.DealtArmourSince ?? input.DealtArmourSession;
            if (w == TimeWindow.Session) return input.DealtArmourSession;
            if (!IsDayWindow(w)) return null;
            var t = input.DealtArmourBook != null ? input.DealtArmourBook.Sum(DayFrom(input, w), LocalToday(input)) : new ArmourTally();
            t.AddAll(input.DealtArmourBook != null ? input.DealtArmourPending : input.DealtArmourSession);
            return t;
        }

        /// <summary>Damage you dealt with its after: what you swung (Before), the part of it whose after is known (Measured) and that after (After).</summary>
        public struct DealtAmount
        {
            public double Before, Measured, After;
            public bool Split => Measured > 0;
            /// <summary>What was stopped of the measured part (none for a weakness).</summary>
            public double Stopped => Math.Max(0, Measured - After);
            /// <summary>The part from before the after was recorded: not split.</summary>
            public double Earlier => Math.Max(0, Before - Measured);
            public static DealtAmount operator +(DealtAmount a, DealtAmount b) => new DealtAmount { Before = a.Before + b.Before, Measured = a.Measured + b.Measured, After = a.After + b.After };
        }

        /// <summary>The rows added up.</summary>
        public static DealtAmount DealtOf(IEnumerable<DamageRow> rows)
        {
            var s = new DealtAmount();
            foreach (var r in rows ?? Enumerable.Empty<DamageRow>()) s += DealtOf(r);
            return s;
        }
        /// <summary>One row's.</summary>
        public static DealtAmount DealtOf(DamageRow r) =>
            r == null ? default : new DealtAmount { Before = r.Amount, Measured = Math.Min(r.Amount, r.Measured), After = r.Measured > 0 ? Math.Max(0, r.After) : 0 };

        /// <summary>Adds a row's to a key's (one pass beside a page's own sums, no grouping: 0.8.1's allocations per refresh).</summary>
        static void BumpDealt(Dictionary<string, DealtAmount> d, string key, DamageRow r) { d.TryGetValue(key, out var o); d[key] = o + DealtOf(r); }

        /// <summary>
        /// A damage part (its Value what you swung, its Fraction its share of the row) drawn by the after rule when some of it is measured: Tone
        /// DamageStoppedTone, Whole its Value as it was (what you swung, said first on hover), Value what got through (in the fill), Value2 what was
        /// stopped (in the outline; the two shown so they add up), Fraction2 the fill's share of the part, Earlier the faded piece's share before
        /// it (Note its words on hover, "300 from before 12 October"). The line keeps the part's share of the row (Text null). Nothing measured:
        /// the part as it was.
        /// </summary>
        public static Block AfterPart(Block part, DealtAmount a, string earlierWords)
        {
            if (part == null || !a.Split || !(a.Before > 0)) return part;
            var (through, stopped) = ShownTwo(a.After, a.Stopped);
            part.Tone = DamageStoppedTone; part.Whole = part.Value ?? NAtLeast(a.Before); part.Value = through; part.Value2 = a.Stopped > 0 ? stopped : null; part.Text = null;
            part.Earlier = (float)(a.Earlier / a.Before);
            part.Fraction2 = (float)(Math.Min(a.After, a.Measured) / a.Before);
            part.Note = a.Earlier > 0 ? NAtLeast(a.Earlier) + " " + earlierWords : null;
            return part;
        }

        /// <summary>Shown for two amounts (what got through, what was stopped) without its lists and sorting: each rounded down, the rest of their
        /// rounded sum to the larger remainder first, "&lt;1" for an amount above 0 that gets nothing. One call per part on every refresh.</summary>
        static (string, string) ShownTwo(double a, double b)
        {
            a = Math.Max(0, a); b = Math.Max(0, b);
            double fa = Math.Floor(a), fb = Math.Floor(b); var left = (long)Math.Round(a + b) - (long)(fa + fb);
            bool aFirst = a - fa >= b - fb;
            for (int k = 0; k < 2 && left > 0; k++)
            {
                if ((k == 0) == aFirst) { if (a - fa > 0) { fa++; left--; } }
                else if (b - fb > 0) { fb++; left--; }
            }
            string S(double whole, double v) => whole > 0 ? N(whole) : v > 0 ? LessThanOne : N(0);
            return (S(fa, a), S(fb, b));
        }

        /// <summary>A part's numbers in words, for a tag ("490 got through, 180 stopped, 300 from before 12 October"; Foes' Dealt bar).</summary>
        public static string DealtTagWords(Block part) =>
            part == null ? null : string.Join(", ", new[] { part.Value + " " + ThroughWords, part.Value2 != null ? part.Value2 + " " + ArmourStoppedWord : null, part.Note }.Where(x => x != null));

        /// <summary>The row's closing words on its line: its share that got through ("73 % got through"; a weakness "112 % got through"), from the
        /// shown amounts so it agrees with the numbers in the bar: never 100 % while something was stopped, "&lt;1 %" for a little. null: nothing measured.</summary>
        public static string ThroughLine(DealtAmount a)
        {
            if (!a.Split) return null;
            var p = Math.Round(100 * a.After / a.Measured);
            if (p >= 100 && a.Stopped >= 0.5) p = 99;
            var share = p <= 0 && a.After > 0 ? UnderOnePercent : N(p) + " %";
            return share + " " + ThroughWords;
        }

        /// <summary>The faded piece's words for a window: on All "from before 12 October" (the dealt ledger's start), else NotSplitWords.</summary>
        public static string EarlierWords(PanelInput input, TimeWindow w) =>
            w == TimeWindow.SinceInstall && input != null && input.IsSelf && input.DealtArmourFromUtc != null ? "from before " + RecordDate(input, input.DealtArmourFromUtc.Value) : NotSplitWords;

        /// <summary>Whether any dealt row of the window has its after: the page then says before and after (DealtAfterCaption) and explains it once.</summary>
        public static bool AnySplit(IEnumerable<DamageRow> rows) => (rows ?? Enumerable.Empty<DamageRow>()).Any(r => r.Dir == "dealt" && r.Measured > 0);
    }
}
