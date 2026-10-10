using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The sort control of a page's item list (0.7, Joost 2026-10-09: "a sort by amount and alphabet ... or by category as another
    /// way to view the things"). One shared control: a row of pills over the list, "Sort  Most · A-Z · By category".
    ///   Most        = the list's own order (most first): the default, today's order.
    ///   A-Z         = by the name the player sees (the game's localised name, never the prefab id), ties by id.
    ///   By category = grouped by a facet of the page's filter bar (Building: the hammer's tab; Crafting: the kind), each group under
    ///                 a small heading with its colour and total, the groups in the order of their totals, most first inside a group.
    ///                 Only where the page has such a facet.
    /// Sorting only reorders: every item, every number and the filter's result stay as they were (the list sorted is the filtered list).
    /// 0.8 (Joost 2026-10-10): a click on the chosen pill flips it: Most to Least (the list's own order turned round), A-Z to Z-A, and back;
    /// By category stays as it is. The pill says the direction it sorts in ("Least", "Z-A"): words, no arrow glyph the game font may lack.
    /// The choice lives in PanelState.View under the control's Id ("Deeds/building/sort"), like a view switch, so every page keeps its
    /// own while the book is open; a pill's click is a view link (Follow). Block shape: Kind "sort", Id = the state key, Title = the
    /// caption ("Sort"), Items = Kind "view" (Id most|least, az|za, group; Title, Selected).
    /// </summary>
    public static partial class PanelModel
    {
        public const string SortMost = "most", SortAz = "az", SortGroup = "group", SortLeast = "least", SortZa = "za";
        public const string SortCaption = "Sort", SortMostLabel = "Most", SortAzLabel = "A-Z", SortLeastLabel = "Least", SortZaLabel = "Z-A";
        /// <summary>A list shorter than this gets no sort control (two or three tiles need none).</summary>
        public const int SortMinItems = 4;

        static readonly CompareInfo NameOrder = CultureInfo.InvariantCulture.CompareInfo;

        /// <summary>The state key of a page's sort control: "Deeds/building/sort".</summary>
        public static string SortKey(PanelState state, string page, string list = null) => (state?.Chapter ?? Chapter.Deeds) + "/" + page + "/sort" + (list == null ? "" : "/" + list);

        /// <summary>The chosen order of a sort control; Most when nothing (or an order this list does not offer) was chosen.</summary>
        public static string SortChoice(PanelState state, string key, bool grouped) =>
            state != null && state.View.TryGetValue(key, out var v) && (v == SortAz || v == SortZa || v == SortLeast || (v == SortGroup && grouped)) ? v : SortMost;

        /// <summary>What a click on the chosen pill picks: Most and Least, A-Z and Z-A turn into each other; By category stays.</summary>
        public static string SortFlip(string order) =>
            order == SortMost ? SortLeast : order == SortLeast ? SortMost : order == SortAz ? SortZa : order == SortZa ? SortAz : order;

        /// <summary>A pill's click (a view link): another pill picks its order, the chosen one flips (SortFlip).</summary>
        public static string SortLink(Block control, Block pill) => ViewLink(control, pill.Selected ? new Block { Id = SortFlip(pill.Id) } : pill);

        /// <summary>A grouping for the sort control: the word on its pill ("By category"), and each item's group (null = "Other"),
        /// with the group's label, its colour (or null) and its place among equal totals.</summary>
        public sealed class SortGroups
        {
            public string Label;
            public Func<Block, (string id, string label, string colour)> Of;
            /// <summary>The weight of an item in its group's total (the page's own number, not the shown text); null = the shown number.</summary>
            public Func<Block, double> Weight;
        }

        /// <summary>The grouping of a filter bar's facet row: the item's value in that row, the chip's words and colour, weighted by the
        /// filter's own numbers. null when the page has no filter or the row does not exist.</summary>
        public static SortGroups GroupsOfFacet(FacetResult filter, string facet)
        {
            var row = filter?.Bar?.Items?.FirstOrDefault(b => b.Kind == "facet" && b.Id == facet);
            if (row == null) return null;
            var bar = filter.Bar.Items.FirstOrDefault(b => b.Kind == "facetbar" && b.Id == facet);
            var chips = (row.Items ?? new List<Block>()).ToDictionary(c => c.Id, c => c);
            // a group wears its part's colour on the bar (0.6.5: bar colours are planned per bar, the chips carry none); a kind folded
            // into "Other (n kinds)" wears the fold's colour
            var parts = (bar?.Items ?? new List<Block>()).Where(b => b.Kind == "part" && b.Id != null).GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First().Colour);
            var foldColour = (bar?.Items ?? new List<Block>()).LastOrDefault(b => b.Kind == "part")?.Colour;
            string ColourOf(string id, string own) => id != null && parts.TryGetValue(id, out var pc) && !string.IsNullOrEmpty(pc) ? pc : !string.IsNullOrEmpty(own) ? own : foldColour;
            var byKey = filter.Shown.GroupBy(i => i.Key).ToDictionary(g => g.Key, g => g.First());
            return new SortGroups
            {
                Label = bar?.Title ?? "By " + (row.Title ?? "group").ToLowerInvariant(),
                Of = item =>
                {
                    string v = null;
                    if (item?.Id != null && byKey.TryGetValue(item.Id, out var fi)) fi.Values.TryGetValue(facet, out v);
                    if (v != null && chips.TryGetValue(v, out var c)) return (v, c.Title, ColourOf(v, c.Colour));
                    return (MaterialOther, MaterialOther, ColourOf(MaterialOther, null));
                },
                Weight = item => item?.Id != null && byKey.TryGetValue(item.Id, out var fi) ? fi.Weight : ParseCount(item?.Value),
            };
        }

        /// <summary>
        /// The sort control and the list in the chosen order, as the blocks to put on the page where the list was: the control (when
        /// the list is long enough: <paramref name="count"/>, the unfiltered length, so the control never comes and goes with a
        /// filter), then the list sorted, or grouped: per group its heading and its own list (a copy of the block with that group's
        /// items). The list's items are only reordered. A null list (nothing for this choice) keeps the control and adds nothing.
        /// </summary>
        public static List<Block> Sorted(PanelState state, string key, Block list, int count, SortGroups groups = null)
        {
            var result = new List<Block>();
            if (count < SortMinItems) { if (list != null) result.Add(list); return result; }
            var pick = SortChoice(state, key, groups != null);
            var options = new List<(string id, string label)> { pick == SortLeast ? (SortLeast, SortLeastLabel) : (SortMost, SortMostLabel), pick == SortZa ? (SortZa, SortZaLabel) : (SortAz, SortAzLabel) };
            if (groups != null) options.Add((SortGroup, groups.Label));
            result.Add(new Block { Kind = "sort", Id = key, Title = SortCaption, Items = options.Select(o => new Block { Kind = "view", Id = o.id, Title = o.label, Selected = o.id == pick }).ToList() });
            if (list == null) return result;
            var items = list.Items ?? new List<Block>();
            if (pick == SortAz || pick == SortZa) { var named = ByName(items); if (pick == SortZa) named.Reverse(); list.Items = named; result.Add(list); return result; }
            if (pick == SortLeast) { var turned = new List<Block>(items); turned.Reverse(); list.Items = turned; result.Add(list); return result; }
            if (pick != SortGroup) { result.Add(list); return result; }

            // grouped: the groups in the order of their totals (then by name), each in the list's own order (most first)
            var weight = groups.Weight ?? (b => ParseCount(b.Value));
            var parts = items.Select((b, at) => (b, at, g: groups.Of(b))).GroupBy(x => x.g.id)
                             .Select(g => (id: g.Key, label: g.First().g.label, colour: g.First().g.colour, total: g.Sum(x => weight(x.b)), items: g.OrderBy(x => x.at).Select(x => x.b).ToList()))
                             .OrderByDescending(g => g.total).ThenBy(g => g.label, StringComparer.OrdinalIgnoreCase).ToList();
            for (int k = 0; k < parts.Count; k++)
            {
                var p = parts[k];
                var head = Section(p.label, p.total, list.Src); head.Colour = p.colour; head.Tone = SortGroup;
                var shell = Shell(list, p.items);
                if (k < parts.Count - 1) shell.Note = null;   // a note under the list (fuel, a key) once, under the last group
                result.Add(head); result.Add(shell);
            }
            return result;
        }

        /// <summary>A-Z: by the name the player sees, case and accents read as the invariant culture does, ties by id.</summary>
        public static List<Block> ByName(IEnumerable<Block> items) =>
            (items ?? Enumerable.Empty<Block>()).OrderBy(b => b.Title ?? "", Comparer<string>.Create((x, y) => NameOrder.Compare(x, y, CompareOptions.IgnoreCase)))
                                                .ThenBy(b => b.Id ?? "", StringComparer.Ordinal).ToList();

        /// <summary>The sort control of a page, if it has one.</summary>
        public static Block SortOf(PanelView v) => v == null ? null : Content(v).FirstOrDefault(b => b.Kind == "sort");
    }
}
