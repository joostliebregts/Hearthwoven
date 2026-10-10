using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The source numbers behind two feats, one small piece each (Chapters/FeatsModel.cs has the feats themselves):
    /// Heavy Keel's best load (Voyages > Cargo: the most metal and ore carried over 2 km in one voyage at the helm,
    /// CargoVoyage) and the animals led (Deeds > Taming: the metres tamed animals walked or sailed following you,
    /// SessionEvents.LedMeters, and the longest single lead, LedTracker). Both are Hearthwoven's own counts since install on this PC
    /// (Src "pc"); a fellow's page shows what they shared. No new block kinds: section, stat, note.
    /// </summary>
    public static partial class PanelModel
    {
        public const string LedTitle = "Animals led", LongestLead = "longest lead", MetalAboard = "metal and ore aboard";

        /// <summary>A best behind a feat (the ledger's own, or a fellow's shared one); null = none.</summary>
        public static BestMark? BestOf(PanelInput i, string key) =>
            i?.Feats != null && i.Feats.Bests.TryGetValue(key, out var b) && b.Value > 0 ? b : (BestMark?)null;

        /// <summary>The metres tamed animals walked or sailed following the player (the sum over animals).</summary>
        public static double LedTotal(PanelInput p) => p?.Events == null ? 0 : p.Events.LedMeters.Where(kv => kv.Value > 0).Sum(kv => (double)kv.Value);

        /// <summary>The line under the led animals, said from whose PC it was: an animal's follow target is only known on the PC that hosts it.</summary>
        public static string LedLine(PanelInput input) => "straight line, " + BornLine(input);

        // the led group's own start (rule C6) as the moment's "since": a best mark is never from before it, the date is passed so no line can fall back to the install
        static string BestMoment(PanelInput input, BestMark b) => FeatMomentLine(input, new FeatMoment { Utc = b.Utc, Biome = b.Biome }, StartOf(input, LocalTotals.StartLed));

        public const string HeaviestLoad = "Heaviest load";
        /// <summary>
        /// Voyages > Cargo, one more tile in the grid beside Cargo carried: Heavy Keel's best load named as a record (fix4-rest: it was a stray
        /// line with no label): "500 · Heaviest load · metal and ore, 2 km". Null without a best (no ghosts).
        /// </summary>
        static Block HeavyKeelTile(PanelInput input)
        {
            var best = BestOf(input, CargoVoyage.BestKey);
            if (best == null) return null;
            return new Block { Kind = "item", Id = "heavykeel", Icon = "item:$item_iron", Title = HeaviestLoad, Value = N(best.Value.Value), Value2 = "metal and ore, " + N(CargoVoyage.LineMetres / 1000) + " km", Src = SrcPc, Source = TagMeasured,
                               From = StartOf(input, LocalTotals.StartCargo) };   // 0.7 rule C6: the cargo group's own start
        }

        /// <summary>
        /// Deeds > Taming: the animals led. A section with the total (km or m), the longest lead as one tile (the animal's own picture),
        /// and the honest line. Nothing when no animal was led (no ghosts).
        /// </summary>
        static void LedGroup(PanelView view, PanelInput input)
        {
            var total = LedTotal(input);
            if (total <= 0) return;
            var from = StartOf(input, LocalTotals.StartLed);   // 0.7 rule C6: the led group's own start (its label says "Recorded from ...")
            view.Blocks.Add(new Block { Kind = "section", Title = LedTitle, Icon = "vocab:lead-rope", Value = Distance(total), Src = SrcPc, Source = TagMeasured, Text = LedLine(input), From = from });   // PlaceRecorded labels it, also with no tile under it   // fix4: the honest line rides on the heading (one line less: Taming fits the plate)
            var best = input.Window != null ? null : BestOf(input, LedTracker.BestKey);   // a day window: the longest lead is a record, not a day count (DeedsDayLines)
            if (best != null)
            {
                var when = BestMoment(input, best.Value);
                var kind = best.Value.What;
                var text = string.Join(" · ", new[] { string.IsNullOrEmpty(kind) ? null : Who(input, kind), when.Length > 0 ? when : null }.Where(s => s != null));
                var tile = Tagged(Stat(string.IsNullOrEmpty(kind) ? "vocab:tame-tamed" : BornIcon(input, kind), Distance(best.Value.Value), LongestLead, text.Length > 0 ? text : null, null), SrcPc);
                tile.From = from;
                tile.Tone = Compact;   // fix4: one line as Heavy Keel's tile on Cargo, so Taming's since-install block fits the plate and both born groups show whole
                view.Blocks.Add(tile);
            }
        }
    }
}
