using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>One island of a plate (0.8 layout D+): a visual group with a faint outline, as wide as the plate or half of it.</summary>
    public sealed class Island
    {
        /// <summary>PanelModel.IslandWide, IslandHalf (two of them side by side) or IslandPlain (a row without an outline: a link, an open About box).</summary>
        public string Span;
        public readonly List<IslandPart> Parts = new List<IslandPart>();
    }

    /// <summary>A block an island draws, and where it sits under the plate ("3", "5.1.0": Items indexes from the plate), so the preview finds the
    /// same block in its data.</summary>
    public sealed class IslandPart
    {
        public Block Block;
        public string Path;
        /// <summary>A people block's one person (Recent's group: an island per player); -1 = the whole block.</summary>
        public int Only = -1;
        /// <summary>A view switch that is not the strip's: its row only (its chosen view's blocks follow as their own parts).</summary>
        public bool Row;
    }

    /// <summary>
    /// 0.8 layout D+ (work/hearthwoven-0.8/density-feedback/FEEDBACK.md part 4; the picked prototype density-prototypes/variant-D): the plate's
    /// body as a calm grid of islands. Each visual group is one island: a bar with its heading and legend, a ranking, a list, the hero. A heading
    /// (section) or a sort row joins the block under it, a sentence (note) the island above it; a sorted list keeps its group headings in one
    /// island; a filter is the head line of the island it filters (its bars in it, or the next one); a page's two columns are two half islands; Recent's
    /// group has an island per player. A short legend (four parts or fewer, no pattern) and a ranking of four rows or fewer are half islands;
    /// a run of them pairs up, an odd one in the middle of the page spans the plate (a player's island stays a half). No divider lines: the
    /// islands set the groups apart.
    /// PanelUi draws these, the preview reads the same plan from the dump (PanelModel.ToJson, "islands").
    /// </summary>
    public static partial class PanelModel
    {
        public const string IslandWide = "wide", IslandHalf = "half", IslandPlain = "plain";
        /// <summary>The most legend rows (parts) or ranking rows a half island holds.</summary>
        public const int IslandHalfRows = 4;

        /// <summary>The islands of a page's plate, in reading order (the strip and the heading row's blocks are not in them).</summary>
        public static List<Island> Islands(Block plate)
        {
            var islands = new List<Island>();
            if (plate == null) return islands;
            if (plate.Tone == PlateFull)   // the Feats chapter: its grid takes the whole room the detail area leaves (FeatsUi.FeatsRoom), as it was
            {
                var all = new Island { Span = IslandPlain };
                for (int k = 0; k < (plate.Items?.Count ?? 0); k++) all.Parts.Add(new IslandPart { Block = plate.Items[k], Path = k.ToString() });
                islands.Add(all); return islands;
            }
            var strip = (plate.Items ?? new List<Block>()).Any(b => b.Kind == "featband") ? TopSwitch(plate) : null;   // its chips ride in the strip
            var head = new List<IslandPart>();
            Island sorted = null; var joinNext = false;   // a sorted list: its group headings and their grids stay in the island of its sort row

            Island Put(string span, params IslandPart[] parts)
            {
                var island = new Island { Span = span };
                island.Parts.AddRange(head); island.Parts.AddRange(parts);
                sorted = head.Any(p => p.Block.Kind == "sort") ? island : null; joinNext = false;
                head.Clear(); islands.Add(island);
                return island;
            }
            void Walk(List<Block> items, string at)
            {
                for (int k = 0; k < (items?.Count ?? 0); k++)
                {
                    var b = items[k]; if (b == null) continue;
                    var part = new IslandPart { Block = b, Path = at + k };
                    if (sorted != null && (joinNext || (b.Kind == "section" && b.Tone == SortGroup)))
                    {
                        sorted.Parts.Add(part); joinNext = b.Kind == "section"; continue;
                    }
                    sorted = null;
                    switch (b.Kind)
                    {
                        case "featband": case "headlink": case "divider": continue;   // the strip, the heading row's chip; no divider lines (Joost, part 4)
                        case "ladders" when IsHeadStrip(b): continue;   // the page's own skill: a chip in the heading row
                        case "aboutnumbers":   // its button rides in the strip; open, its box, a row of its own
                            if (b.Open) { var box = new Island { Span = IslandPlain }; box.Parts.Add(part); islands.Add(box); }
                            continue;
                        case "section": case "sort": head.Add(part); continue;
                        case "filterbar":   // the head line of what it filters: the island of its bars, or with none shown the next island
                            if ((b.Items ?? new List<Block>()).Any(x => x.Kind == "facetbar" && x.Tone != "hidden") || b.Open) Put(IslandWide, part); else head.Add(part);
                            continue;
                        case "switch":
                            if (b != strip) Put(IslandPlain, new IslandPart { Block = b, Path = part.Path, Row = true });
                            var views = b.Items ?? new List<Block>();
                            for (int v = 0; v < views.Count; v++) if (views[v].Selected) Walk(views[v].Items, part.Path + "." + v + ".");
                            continue;
                        case "people":
                            if (head.Count > 0 || (b.Items?.Count ?? 0) < 2) { Put(IslandWide, part); continue; }
                            for (int p = 0; p < b.Items.Count; p++) Put(IslandHalf, new IslandPart { Block = b, Path = part.Path, Only = p });
                            continue;
                        case "columns":
                            {
                                var cols = Enumerable.Range(0, b.Items?.Count ?? 0).Where(c => b.Items[c].Items != null && b.Items[c].Items.Count > 0).ToList();
                                if (cols.Count != 2) { Put(IslandWide, part); continue; }
                                if (head.Count > 0) Put(IslandPlain);   // a heading over both columns stays a heading over the pair
                                foreach (var c in cols)
                                {
                                    var island = Put(IslandHalf);
                                    for (int j = 0; j < b.Items[c].Items.Count; j++) island.Parts.Add(new IslandPart { Block = b.Items[c].Items[j], Path = part.Path + "." + c + "." + j });
                                }
                                continue;
                            }
                        case "note":
                            {
                                var last = islands.LastOrDefault();
                                if (head.Count == 0 && b.Tone != "hint" && last != null && last.Span == IslandWide) last.Parts.Add(part);   // its sentence ends the island above (under a pair it speaks for both: a row)
                                else Put(head.Count > 0 ? IslandWide : IslandPlain, part);
                                continue;
                            }
                        case "empty": case "link": Put(head.Count > 0 ? IslandWide : IslandPlain, part); continue;
                        case "dmgmix":   // 0.8 damage rows: By weapon's weapons are one list in one island, as By foe's foes and By type's types
                            {
                                var last = islands.LastOrDefault();
                                if (head.Count == 0 && last != null && last.Span == IslandWide && last.Parts.Count > 0 && last.Parts[last.Parts.Count - 1].Block.Kind == "dmgmix") last.Parts.Add(part);
                                else Put(IslandWide, part);
                                continue;
                            }
                        case "composition": Put(HalfLegend(b.Items) ? IslandHalf : IslandWide, part); continue;
                        case "ranking": Put((b.Items?.Count ?? 0) <= IslandHalfRows ? IslandHalf : IslandWide, part); continue;
                        default: Put(IslandWide, part); continue;
                    }
                }
            }
            Walk(plate.Items, "");
            if (head.Count > 0) Put(IslandPlain);
            // half islands pair up; an odd one in the middle of the page spans the plate, the last one may stand alone; a player's island never
            // spans it (Recent's group of three: one player drawn twice as wide as the others would rank them, 0.8.1 review 5), it stays a half
            for (int i = 0; i < islands.Count;)
            {
                if (islands[i].Span != IslandHalf) { i++; continue; }
                var j = i; while (j < islands.Count && islands[j].Span == IslandHalf) j++;
                if ((j - i) % 2 == 1 && j < islands.Count && !islands[j - 1].Parts.Any(p => p.Only >= 0)) islands[j - 1].Span = IslandWide;
                i = j;
            }
            return islands;
        }

        /// <summary>A legend short enough for a half island: four parts or fewer, none patterned (the grain and groundwork bars want the width).</summary>
        public static bool HalfLegend(List<Block> parts)
        {
            var shown = (parts ?? new List<Block>()).Where(p => p != null && p.Fraction > 0).ToList();
            return shown.Count <= IslandHalfRows && !shown.Any(p => !string.IsNullOrEmpty(p.Pattern));
        }

        /// <summary>The block a part draws: a people block with only its one person, else the block itself.</summary>
        public static Block PartBlock(IslandPart p) =>
            p.Only >= 0 && p.Block.Items != null && p.Only < p.Block.Items.Count ? new Block { Kind = p.Block.Kind, Tone = IslandPersonTone, Src = p.Block.Src, Source = p.Block.Source, Items = new List<Block> { p.Block.Items[p.Only] } } : p.Block;

        /// <summary>A people block cut to one person (PartBlock): drawn as that player's island, the shield and name over their items.</summary>
        public const string IslandPersonTone = "island";

        // the plan in the dump (ToJson): the islands and their parts by path, for the preview
        static void IslandsJson(Json j, Block plate)
        {
            j.Key("islands").OpenArr();
            foreach (var island in Islands(plate))
            {
                j.Open().Str("span", island.Span).Key("parts").OpenArr();
                foreach (var p in island.Parts) { j.Open().Str("path", p.Path); if (p.Only >= 0) j.Num("only", p.Only); if (p.Row) j.Num("row", 1); j.Close(); }
                j.CloseArr().Close();
            }
            j.CloseArr();
        }
    }
}
