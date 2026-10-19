using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 Deeds > Crafting, the "Upgrades per item" island (work/hearthwoven-0.9/DESIGN.md section 2, board Crafting; Joost 18 Oct "Agreed."): the
    /// upgrades recorded on this PC (record 1, UpgradeLog.PerItem: the newest 2 000 whole, older ones folded per item, so the count per item stays
    /// exact) in Woodcutting's per-tree row form (a ranking under its section, "Recorded from 2 November · this PC"), each row the item's picture
    /// and name, the four-part weakness meter of Battle > Foes reused as a level meter (filled to the highest level recorded out of the item's
    /// max quality, the game's m_maxQuality), "now level 3 of 4" and the count on the right. Then one line for what came before:
    /// "8 more upgrades came before 2 November: the game counts them, not which item." (the game's Upgrades counter minus the recorded ones; no
    /// line when that is 0 or less, so nothing false is said). Your own book in All only: a fellow's copy holds no upgrades (not recorded on that
    /// book), and the game's counter has no per-item days. Block shape: Kind "ranking", Tone LevelTone, Items: Id token, Icon, Title, Value the
    /// count, Fraction the meter's fill (level / max), Text "now level 3 of 4", Level the level, Count the max quality (0 when not known): the meter is LevelMeterOf(Level, Count).
    /// </summary>
    public static partial class PanelModel
    {
        public const string UpgradesTitle = "Upgrades per item", LevelTone = "levels", UpgradeOtherTitle = "Other items";
        /// <summary>The level meter's parts (the weakness meter's four, BattleUi.BattleMeter): an item of another max quality fills its share of them.</summary>
        public const int LevelMeterParts = 4;
        /// <summary>The island's line with no upgrade recorded yet (its empty state, under the "Recorded from" line).</summary>
        public const string UpgradesNoneYet = "Each upgrade you make from now on shows here, with its item.";

        /// <summary>"now level 3 of 4"; "now level 3" when the item's max quality is not known.</summary>
        public static string LevelWords(int level, int max) => max > 0 ? "now level " + level + " of " + max : "now level " + level;

        /// <summary>The level meter's fill: the level out of the max quality; not known: the level out of four parts (never more than full).</summary>
        public static float LevelFill(int level, int max) => level <= 0 ? 0f : (float)Math.Min(1.0, level / (double)(max > 0 ? max : Math.Max(LevelMeterParts, level)));

        /// <summary>The most parts a level meter draws (one per level): an item of a higher max quality (a mod's) fills four parts by its share instead.</summary>
        public const int LevelMeterMostParts = 10;

        /// <summary>
        /// The level meter's parts and how many of them are filled (Joost on the Wood Shield, max 3: "four parts filled 2.5"): one part per level of the
        /// item's max quality, the level's parts filled whole (level 2 of 3: two of three); a max not known, or above LevelMeterMostParts, four parts
        /// filled by the level's share. One rule for Crafting's Upgrades per item and the deed feed's upgrade lines.
        /// </summary>
        public static (int parts, float filled) LevelMeterOf(int level, int max)
        {
            if (max > 0 && max <= LevelMeterMostParts) return (max, Math.Max(0, Math.Min(level, max)));
            return (LevelMeterParts, LevelFill(level, max) * LevelMeterParts);
        }

        /// <summary>"8 more upgrades came before 2 November: the game counts them, not which item." (more: rows stand above it); null when n is 0 or less.</summary>
        public static string UpgradesBeforeLine(double n, string date, bool more) =>
            n < 1 ? null : N(n) + (more ? " more " : " ") + Label1(n, "upgrade", "upgrades") + " came before " + date + ": the game counts them, not which item.";

        static int MaxQualityOf(PanelInput input, string token) { try { return Math.Max(0, input.MaxQuality?.Invoke(token) ?? 0); } catch { return 0; } }

        /// <summary>The Upgrades per item island's blocks (section, rows, the line on what came before), or none: a fellow's book, a window, no record, nothing at all.</summary>
        public static List<Block> UpgradesIsland(PanelInput input)
        {
            var blocks = new List<Block>();
            if (input == null || !input.IsSelf || input.Window != null || input.Upgrades == null) return blocks;
            var log = input.Upgrades;
            var from = input.UpgradesFrom ?? (log.FromUtc > DateTime.MinValue ? log.FromUtc : (DateTime?)null);
            var recorded = log.Total; var game = C(input, "Upgrades");
            if (recorded == 0 && game <= 0) return blocks;   // no upgrade at all: no island (the hero says nothing either)
            var head = new Block { Kind = "section", Title = UpgradesTitle, Value = recorded > 0 ? N(recorded) : null, Src = SrcPc, Source = TagOfSrc(SrcPc) };
            DateFrom(head, from);
            if (recorded == 0) head.Text = RecordedFromLine(input, from);   // no number to label: the head says from when in words (never a 0)
            blocks.Add(head);
            var date = from.HasValue ? RecordDate(input, from.Value) : null;
            if (recorded > 0)
            {
                var rows = new List<Block>();
                foreach (var f in log.PerItem().Where(f => f.Count > 0))
                {
                    if (f.Token == DeedLog.OtherToken) { rows.Add(new Block { Id = f.Token, Icon = "", Title = UpgradeOtherTitle, Value = N(f.Count), Src = SrcPc, Source = TagOfSrc(SrcPc) }); continue; }   // past the fold's cap: the rest as one row, no level
                    var max = MaxQualityOf(input, f.Token);
                    rows.Add(new Block
                    {
                        Id = f.Token, Icon = "item:" + f.Token, Title = Who(input, f.Token), Value = N(f.Count), Fraction = LevelFill(f.MaxLevel, max),
                        Text = LevelWords(f.MaxLevel, max), Level = f.MaxLevel, Count = max, Src = SrcPc, Source = TagOfSrc(SrcPc),   // the meter: LevelMeterOf(Level, Count)
                    });
                }
                // the other-items row last, the items most upgraded first (PerItem's order)
                rows = rows.Where(r => r.Id != DeedLog.OtherToken).Concat(rows.Where(r => r.Id == DeedLog.OtherToken)).ToList();
                var ranking = new Block { Kind = "ranking", Tone = LevelTone, Src = SrcPc, Source = TagOfSrc(SrcPc), Items = rows };
                DateFrom(ranking, from);
                blocks.Add(ranking);
            }
            else blocks.Add(new Block { Kind = "note", Text = UpgradesNoneYet });
            if (date != null)
            {
                var before = UpgradesBeforeLine(game - recorded, date, recorded > 0);
                if (before != null) blocks.Add(new Block { Kind = "note", Text = before });
            }
            return blocks;
        }

        // ---------- the two faults Joost saw in game on Crafting (18 Oct: "6 gear crafted · 11 upgrades made", three tiles without names, one grey bar) ----------

        /// <summary>
        /// Fault 2, the unlabelled grey By kind bar: a facet bar whose parts hold one kind only (a new character's gear all armor, say) was drawn as a
        /// bar of one part with no legend (BarForm.ListShown: one part, no list; its head "By kind 6" names the row, not the part), in that kind's colour
        /// (Armor's slate, which reads grey). Such a bar now stands as its one legend row under its heading, "By kind 6" over "Armor 6", as a fellow's
        /// one-kind shared bar already was (DeedsDays.SharedBar): Tone FacetSingleTone, drawn with no bar and its row.
        /// </summary>
        public const string FacetSingleTone = "single";
    }
}
