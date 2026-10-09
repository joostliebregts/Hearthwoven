using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>One chip of a facet row: its id (kept in PanelState.Facets), the words on it, and the colour of its part in the
    /// row's linked bar (null = the row has no bar, or this part takes the neutral colour).</summary>
    public sealed class FacetOption { public string Id, Label, Colour; }

    /// <summary>
    /// One facet row of a filter bar. Title "Kind", Sub the honest line under it ("by main material": how the page knows),
    /// Options in their fixed order (zero chips stay, dimmed, so the layout never jumps). Bar: the row also gets a slim linked
    /// bar over the same parts and colours, titled BarTitle ("By kind").
    /// </summary>
    public sealed class FacetDef
    {
        public string Id, Title, Sub, BarTitle;
        public bool Bar = true;
        public List<FacetOption> Options = new List<FacetOption>();
    }

    /// <summary>One thing the filter bar narrows (a crafted item): its key, its weight in the page's unit (how often it was
    /// crafted) and its value in each facet (facet id -> option id; a missing value belongs to no chip of that row).</summary>
    public sealed class FacetItem
    {
        public string Key; public double Weight;
        public Dictionary<string, string> Values = new Dictionary<string, string>();
    }

    /// <summary>What a filter bar leaves: the block to draw, and the items that pass every row.</summary>
    public sealed class FacetResult
    {
        public Block Bar; public List<FacetItem> Shown = new List<FacetItem>();
        /// <summary>True when an item (its Values set) passes every chosen chip: what the since-install block of a filtered page follows.</summary>
        public Func<FacetItem, bool> Pass;
    }

    /// <summary>
    /// The reusable filter bar (design: work/hearthwoven-visual-vocabulary/FILTERING-06.md, prototype craft-G). One component
    /// for any page that narrows a list by what the game's own data says about each entry:
    ///   - chip rows, one per facet, with live counts: a chip's count is what the list would show if that chip were added, given
    ///     the chips chosen in the OTHER rows (OR within a row, AND across rows); a chip with no count is dimmed, stays in its
    ///     place and cannot be chosen (a chip that is chosen can always be unchosen);
    ///   - the chosen chips as removable tokens, the result line ("2 items · 6 crafted") and Clear all;
    ///   - a slim linked bar per row that has colours, filtered by the other rows and not by its own (the crossfilter rule: its
    ///     chosen part is outlined, the rest stays visible so you can choose another);
    ///   - keys: the filter key (default Tab) enters a focus on the rows; there A/D or the arrows move along a row, W/S between
    ///     rows, Enter or Space chooses, Delete clears all, Esc or the filter key leaves. The mouse needs no focus;
    ///   - collapsed by default (Open false): one line (the header: "Filter", the key's cap, the chosen tokens or "all", the
    ///     result line, Clear all, Show filters), so the page's list stays above the fold. The rows and the linked bars open
    ///     with the filter key (the focus) or a click on the header, and close again with Esc, the key or the header's
    ///     Hide filters. While collapsed the linked bars stay only if they fit above the fold with the list's first two
    ///     rows (their Tone is "hidden" when they do not); their legends carry the colour's name only: the chips carry the counts.
    /// Block shape: Kind "filterbar" (Id = the filter's id, Text = the result line, KeyCap = the key, Open = rows shown), Items =
    /// "facet" (Id, Title, Note = its honest line, Tone "focus" while the keys act on it; Items = "chip": Id, Title, Value =
    /// count, Selected, Tone "zero" | "cursor"), one "applied" (Title = what shows with no filter on an inline bar, Value =
    /// "all", Text = the result line, Items = "token": Id = "facet|option", Title, Text = the row), and one "facetbar" per
    /// row with colours (Id = facet, Title, Note, Tone "hidden" while collapsed and too tall, Items = "part": Id, Title,
    /// Value, Fraction, Colour, Selected, Tone "zero").
    /// </summary>
    public static partial class PanelModel
    {
        public const string FacetTarget = "facet:", FacetClearTarget = "facetclear:", FacetOpenTarget = "facetopen:";

        /// <summary>The room a collapsed filter bar's linked bars are measured against (px, 1080p, at the panel's scale 1): the
        /// plate's viewport, what the page above the list takes (hero, source line), the header, and the list's first two rows.</summary>
        public const double FilterViewport = 470, FilterAbove = 140, FilterHeader = 40, FilterListRows = 124;

        static string FacetKey(string filter, string facet) => filter + "|" + facet;

        /// <summary>True while the filter's rows and bars are shown: the focus is in or the header was opened by a click.</summary>
        public static bool FilterIsOpen(PanelState s, string filter) => s != null && (s.FilterRow >= 0 || (filter != null && s.OpenFilters.Contains(filter)));

        /// <summary>The header's click: opens the rows and bars, or closes them (and leaves the focus).</summary>
        public static void ToggleFilterOpen(PanelState s, string filter)
        {
            if (FilterIsOpen(s, filter)) { s.OpenFilters.Remove(filter); s.FilterRow = -1; }
            else s.OpenFilters.Add(filter);
        }

        /// <summary>The click target of the header's Show filters / Hide filters.</summary>
        public static string FacetOpenLink(string filter) => FacetOpenTarget + filter;

        // the lines a linked bar's legend takes: names only, wrapped at the column (14 px text, an 18 px swatch, gaps)
        static int LegendLines(FacetDef d, double column = 840)
        {
            double used = 0; var lines = 1;
            foreach (var o in d.Options)
            {
                var w = 22 + 6.8 * (o.Label ?? "").Length;
                if (used > 0 && used + 12 + w > column) { lines++; used = 0; }
                used += (used > 0 ? 12 : 0) + w;
            }
            return lines;
        }

        // do the linked bars fit above the fold with the list's first two rows? (5 px gaps; head 18, track 13, a 16 px legend line)
        static bool BarsFit(IList<FacetDef> defs, double above)
        {
            var h = above + FilterHeader + FilterListRows;
            foreach (var d in defs.Where(x => x.Bar)) h += 18 + 5 + 22 + 5 + LegendLines(d) * 16 + 5;   // fix4: the bar 22 high with its edge (was 13)
            return h <= FilterViewport;
        }

        /// <summary>True when any chip of this filter is chosen (the page is narrowed).</summary>
        public static bool FilterOn(PanelState s, string filter) =>
            s != null && s.Facets.Any(kv => kv.Key.StartsWith(filter + "|", StringComparison.Ordinal) && kv.Value != null && kv.Value.Count > 0);

        /// <summary>The chips chosen in a row, in the order they were chosen.</summary>
        public static List<string> Chosen(PanelState s, string filter, string facet) =>
            s != null && s.Facets.TryGetValue(FacetKey(filter, facet), out var l) ? l : new List<string>();

        /// <summary>Chooses or unchooses a chip.</summary>
        public static void ToggleFacet(PanelState s, string filter, string facet, string option)
        {
            var key = FacetKey(filter, facet);
            if (!s.Facets.TryGetValue(key, out var l)) s.Facets[key] = l = new List<string>();
            if (!l.Remove(option)) l.Add(option);
            if (l.Count == 0) s.Facets.Remove(key);
        }

        /// <summary>Clear all: every chip of this filter bar unchosen.</summary>
        public static void ClearFacets(PanelState s, string filter)
        {
            foreach (var k in s.Facets.Keys.Where(k => k.StartsWith(filter + "|", StringComparison.Ordinal)).ToList()) s.Facets.Remove(k);
        }

        /// <summary>The click target of a chip, a token or a bar part.</summary>
        public static string FacetLink(string filter, string facet, string option) => FacetTarget + FacetKey(filter, facet) + "|" + option;

        /// <summary>
        /// The pattern of a linked bar's part (fix4, rubric 4: nothing is told by colour alone): the part's place in its row (a row's options keep
        /// their order, so a part always keeps its pattern) picks one of seven marks, solid first; the bar's segment and the legend's swatch both
        /// carry it, and a segment wide enough also says its name (FacetUi). Ready sprites src/Panel/vocab/grain-hatch-*.png, tiled over the colour.
        /// </summary>
        public static readonly string[] FacetPatterns = { null, "vocab:grain-hatch-diag", "vocab:grain-hatch-vert", "vocab:grain-hatch-horiz", "vocab:grain-hatch-dots", "vocab:grain-hatch-check", "vocab:grain-hatch-cross" };
        public static string FacetPatternOf(int part) => FacetPatterns[((part % FacetPatterns.Length) + FacetPatterns.Length) % FacetPatterns.Length];

        /// <summary>A click on a filter target (chip, token, bar part, Clear all); false for any other target.</summary>
        public static bool FollowFacet(PanelState s, string target)
        {
            if (target == null) return false;
            if (target.StartsWith(FacetClearTarget, StringComparison.Ordinal)) { ClearFacets(s, target.Substring(FacetClearTarget.Length)); return true; }
            if (target.StartsWith(FacetOpenTarget, StringComparison.Ordinal)) { ToggleFilterOpen(s, target.Substring(FacetOpenTarget.Length)); return true; }
            if (!target.StartsWith(FacetTarget, StringComparison.Ordinal)) return false;
            var p = target.Substring(FacetTarget.Length).Split('|');
            if (p.Length == 3) ToggleFacet(s, p[0], p[1], p[2]);
            return true;
        }

        /// <summary>
        /// Builds a filter bar over items. unit: the page's own word for the weight ("crafted"); src: the source mark of those
        /// numbers; noun1/nounN: what one entry is called ("item", "items"). The focus marks (FilterRow, FilterCursor) come
        /// from the state, so the keys and the picture agree. above: what the page puts above the list in px (the fold test of
        /// the collapsed bars).
        /// </summary>
        public static FacetResult Facets(PanelState state, string filter, IList<FacetDef> defs, IList<FacetItem> items, string unit, string src,
                                         string noun1 = "item", string nounN = "items", string empty = "No filter: every item", double above = FilterAbove)
        {
            state = state ?? new PanelState();
            bool Has(FacetItem it, FacetDef d, string opt) => it.Values.TryGetValue(d.Id, out var v) && v == opt;
            bool Passes(FacetItem it, FacetDef except)
            {
                foreach (var d in defs)
                {
                    if (d == except) continue;
                    var chosen = Chosen(state, filter, d.Id);
                    if (chosen.Count > 0 && !(it.Values.TryGetValue(d.Id, out var v) && chosen.Contains(v))) return false;
                }
                return true;
            }
            double CountOf(FacetDef d, string opt) => items.Where(it => Has(it, d, opt) && Passes(it, d)).Sum(it => it.Weight);
            var shown = items.Where(it => Passes(it, null)).ToList();
            var bar = new Block { Kind = "filterbar", Id = filter, KeyCap = string.IsNullOrEmpty(state.FilterKey) ? null : state.FilterKey, Src = src, Source = TagOfSrc(src), Items = new List<Block>() };
            bar.Open = FilterIsOpen(state, filter);

            // the chip rows
            var rowCursor = state.FilterRow >= 0 ? Math.Min(state.FilterRow, defs.Count - 1) : -1;
            var rowIndex = 0;
            foreach (var d in defs)
            {
                var chosen = Chosen(state, filter, d.Id);
                var row = new Block { Kind = "facet", Id = d.Id, Title = d.Title, Note = d.Sub, Tone = rowIndex == rowCursor ? "focus" : null, Items = new List<Block>() };
                foreach (var o in d.Options)
                {
                    var n = CountOf(d, o.Id); var on = chosen.Contains(o.Id);
                    row.Items.Add(new Block { Kind = "chip", Id = o.Id, Title = o.Label, Value = N(n), Colour = o.Colour, Selected = on, Tone = n <= 0 && !on ? "zero" : null, Fraction = (float)n });
                }
                if (rowIndex == rowCursor && row.Items.Count > 0)
                {
                    var at = Math.Min(Math.Max(0, state.FilterCursor), row.Items.Count - 1);
                    if (row.Items[at].Tone == "zero") at = NextChip(row, at, 1);
                    if (at >= 0 && row.Items[at].Tone != "zero") row.Items[at].Tone = "cursor";
                }
                bar.Items.Add(row);
                rowIndex++;
            }

            // the chosen chips as tokens, and the result line
            var applied = new Block { Kind = "applied", Title = empty, Value = "all", Items = new List<Block>() };
            foreach (var d in defs)
                foreach (var id in Chosen(state, filter, d.Id))
                {
                    var o = d.Options.FirstOrDefault(x => x.Id == id);
                    if (o != null) applied.Items.Add(new Block { Kind = "token", Id = d.Id + "|" + id, Title = o.Label, Text = d.Title });
                }
            var total = shown.Sum(it => it.Weight);
            applied.Text = shown.Count.ToString(Inv) + " " + (shown.Count == 1 ? noun1 : nounN) + " · " + N(total) + " " + unit;
            bar.Text = applied.Text;
            bar.Items.Add(applied);

            // the linked bars: each row's parts counted over what the OTHER rows leave, the chosen part outlined
            var barsShown = bar.Open || BarsFit(defs, above);
            foreach (var d in defs.Where(x => x.Bar))
            {
                var chosen = Chosen(state, filter, d.Id);
                var counts = d.Options.Select(o => (o, n: CountOf(d, o.Id))).ToList();
                var sum = counts.Sum(c => c.n);
                var title = d.BarTitle ?? d.Title;
                // the honest line stays under the row; on the bar it only stays when the bar's title does not already say it
                var note = !string.IsNullOrEmpty(d.Sub) && title.IndexOf(d.Sub, StringComparison.OrdinalIgnoreCase) >= 0 ? null : d.Sub;
                var fb = new Block { Kind = "facetbar", Id = d.Id, Title = title, Note = note, Tone = barsShown ? null : "hidden", Items = new List<Block>() };
                var at = 0;
                foreach (var (o, n) in counts)
                    fb.Items.Add(new Block { Kind = "part", Id = o.Id, Title = o.Label, Value = N(n), Colour = o.Colour, Pattern = FacetPatternOf(at++), Fraction = sum > 0 ? (float)(n / sum) : 0, Selected = chosen.Contains(o.Id), Tone = n <= 0 ? "zero" : null });
                bar.Items.Add(fb);
            }
            return new FacetResult { Bar = bar, Shown = shown, Pass = it => Passes(it, null) };
        }

        // ---------- the filter focus (keys) ----------

        /// <summary>The page's filter bar, if it has one (looked for in the layout boxes too).</summary>
        public static Block FilterOf(PanelView v) => Content(v).FirstOrDefault(b => b.Kind == "filterbar");

        /// <summary>B16: the words on a filter bar's chips, chosen tokens and bar parts that are only a number ("10", a category
        /// the enum has no name for); empty when every label is words. The tests and the in-game self-check (facet-number) ask.</summary>
        public static List<string> FacetNumberLabels(PanelView v) => v == null ? new List<string>() : FacetNumberLabels(Content(v));

        /// <summary>The same over blocks: every filter bar among them, its rows, tokens and bars.</summary>
        public static List<string> FacetNumberLabels(IEnumerable<Block> blocks)
        {
            var found = new List<string>();
            void Walk(Block b, bool inBar)
            {
                if (b == null) return;
                inBar = inBar || b.Kind == "filterbar";
                if (inBar && (b.Kind == "chip" || b.Kind == "token" || b.Kind == "part") && BareNumber(b.Title)) found.Add(b.Title);
                foreach (var c in b.Items ?? new List<Block>()) Walk(c, inBar);
            }
            foreach (var b in blocks ?? Enumerable.Empty<Block>()) Walk(b, false);
            return found;
        }

        static List<Block> FacetRows(PanelView v) => FilterOf(v)?.Items?.Where(b => b.Kind == "facet").ToList() ?? new List<Block>();

        // the next chip that can be chosen in a direction (wraps); -1 when the row has none
        static int NextChip(Block row, int from, int d)
        {
            var n = row.Items.Count;
            for (int k = 1; k <= n; k++)
            {
                var i = (((from + d * k) % n) + n) % n;
                if (row.Items[i].Tone != "zero") return i;
            }
            return -1;
        }

        static int FirstChip(Block row)
        {
            var i = row.Items.FindIndex(c => c.Selected);
            return i >= 0 ? i : Math.Max(0, row.Items.FindIndex(c => c.Tone != "zero"));
        }

        /// <summary>The filter key: from the collapsed line it opens the rows and enters the focus on the first one; in the focus,
        /// or with the rows opened by a click, it closes them again. false when the page has no filter bar.</summary>
        public static bool FilterKeyPressed(PanelState s, PanelView v)
        {
            var rows = FacetRows(v);
            if (rows.Count == 0)
            {
                // fix4-rest: Company > Together has no filter bar; the filter key steps the window chips that show under Damage dealt
                var tg = TogetherOf(v); var win = tg == null ? null : TogetherWindowSwitch(tg);
                if (win == null) return false;
                var at = Math.Max(0, win.Items.FindIndex(x => x.Selected)); var n = win.Items.Count;
                for (int k = 1; k <= n; k++) { var next = win.Items[(at + k) % n]; if (next.Tone != OffTone) { s.View[win.Id] = next.Id; break; } }   // a greyed window is skipped
                return true;
            }
            if (FilterIsOpen(s, FilterOf(v)?.Id)) { s.FilterRow = -1; s.OpenFilters.Remove(FilterOf(v).Id); return true; }
            s.FilterRow = 0; s.FilterCursor = FirstChip(rows[0]);
            s.OpenFilters.Add(FilterOf(v).Id);   // opened by the key: it stays open when the focus is left by Q/E or Backspace (remembered, PanelPrefs.cs)
            return true;
        }

        /// <summary>True while Esc has something to close here: the focus is in, or this page's filter was opened by a click.</summary>
        public static bool FilterAnyOpen(PanelState s, PanelView v) => s != null && (s.FilterRow >= 0 || (v != null && FilterOf(v) is Block f && s.OpenFilters.Contains(f.Id)));

        /// <summary>Esc: leaves the focus and collapses the filter. true when there was something to leave.</summary>
        public static bool FilterLeave(PanelState s, PanelView v = null)
        {
            // with the page: only its own bar closes (zones-wording: every page keeps its own open or shut, remembered across sessions)
            var id = v != null ? FilterOf(v)?.Id : null;
            if (id != null) { var had = s.FilterRow >= 0 || s.OpenFilters.Contains(id); s.FilterRow = -1; s.OpenFilters.Remove(id); return had; }
            var was = s.FilterRow >= 0 || s.OpenFilters.Count > 0; s.FilterRow = -1; s.OpenFilters.Clear(); return was;
        }

        /// <summary>A/D (and the arrows) in the focus: along the row to the next chip that can be chosen (wraps).</summary>
        public static bool FilterMove(PanelState s, PanelView v, int d)
        {
            var rows = FacetRows(v);
            if (s.FilterRow < 0 || s.FilterRow >= rows.Count) return false;
            var row = rows[s.FilterRow]; var at = Math.Min(Math.Max(0, s.FilterCursor), row.Items.Count - 1);
            var next = NextChip(row, at, d);
            if (next >= 0) s.FilterCursor = next;
            return true;
        }

        /// <summary>W/S in the focus: to the row above or below (wraps); the cursor starts on its first chosen or first available chip.</summary>
        public static bool FilterRowStep(PanelState s, PanelView v, int d)
        {
            var rows = FacetRows(v);
            if (s.FilterRow < 0 || rows.Count == 0) return false;
            s.FilterRow = (((s.FilterRow + d) % rows.Count) + rows.Count) % rows.Count;
            s.FilterCursor = FirstChip(rows[s.FilterRow]);
            return true;
        }

        /// <summary>Enter or Space in the focus: chooses or unchooses the chip under the cursor.</summary>
        public static bool FilterToggle(PanelState s, PanelView v)
        {
            var f = FilterOf(v); var rows = FacetRows(v);
            if (f == null || s.FilterRow < 0 || s.FilterRow >= rows.Count) return false;
            var row = rows[s.FilterRow]; var at = Math.Min(Math.Max(0, s.FilterCursor), row.Items.Count - 1);
            if (at < 0 || row.Items[at].Tone == "zero") return true;
            ToggleFacet(s, f.Id, row.Id, row.Items[at].Id);
            return true;
        }

        /// <summary>Delete in the focus: Clear all.</summary>
        public static bool FilterClear(PanelState s, PanelView v)
        {
            var f = FilterOf(v);
            if (f == null || s.FilterRow < 0) return false;
            ClearFacets(s, f.Id);
            return true;
        }

        /// <summary>Build's last word on the focus: gone on a page without a filter bar; the footer says what the keys do in it.</summary>
        static void FilterFocusFix(PanelState state, PanelView view)
        {
            if (view.ShowAbout || FacetRows(view).Count == 0) { state.FilterRow = -1; return; }
            if (state.FilterRow < 0) return;
            view.Keys.Clear();
            view.Keys.AddRange(new[] { "[A/D] Move", "[W/S] Row", "[Enter] Choose", "[Delete] Clear all", "[" + state.FilterKey + "/Esc] Leave" });
        }
    }
}
