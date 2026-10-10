using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>One row of a bar's list, and the bar's part for it: a part as the model gave it (every part its own row since B39), or
    /// the "Other (n kinds)" row past BarMaxParts rows.</summary>
    public sealed class BarRow
    {
        /// <summary>The part itself; for a row folded here past BarMaxParts a part of its own (Id FoldId).</summary>
        public Block Part;
        public string Title, Value, Colour;
        /// <summary>The part's share of the bar (0..1) and its share in whole percent (largest remainder: the list adds up to 100).</summary>
        public float Fraction; public int Percent;
        /// <summary>The share as the list shows it: a real part that rounds to 0 says "&lt;1 %" (never 0 for a real amount; the others still add to 100).</summary>
        public string Share => Percent == 0 && Fraction > 0 ? PanelModel.UnderOnePercent : Percent + " %";
        public bool Other, Selected;
    }

    /// <summary>A bar with its list (BAR-FORM.md): the rows in the order bar and list show them (the parts, then Other last), whether the parts carry their numbers (damage-type bars only), whether it has a list at all (not for one part:
    /// the head line already says its name and total), and the list's columns (two for more than four rows, filled top to bottom:
    /// rows [0, Split) on the left, [Split, n) on the right; a damage-type list from three rows, half and half: 2 x 2 for four).</summary>
    public sealed class BarForm
    {
        public List<BarRow> Rows = new List<BarRow>();
        public bool Numbers, List; public int Columns = 1, Split;
    }

    public static partial class PanelModel
    {
        /// <summary>0.8 layout D+ (FEEDBACK 1, Joost: "as flat and wide as possible"): a list takes up to this many columns side by side, as many
        /// as fit its island, filled top to bottom (BarListSplit); fewer rows, so a lower legend.</summary>
        public const int BarListColumns = 3;
        public const string UnderOnePercent = "<1 %";

        /// <summary>The one bar form's measures (px at scale 1, PanelUi and the preview alike): the bar (0.8 layout D+: 22, the Hall's thin one
        /// too; a wood grain or groundwork pattern keeps its taller 24, BarFormTall), the gap between the parts, the air between bar and list, a
        /// list row and the gap between rows, the gap between the columns, the gap between a row's cells, and the air after the whole
        /// bar-with-list (none: it sits in an island, whose padding is the air).</summary>
        public const float BarFormBar = 22, BarFormThin = 22, BarFormTall = 24, BarFormGap = 4, BarFormToList = 10, BarFormRow = 22, BarFormRowGap = 6, BarFormColumnGap = 36, BarFormCellGap = 8, BarFormAfter = 0;

        /// <summary>A bar's height: a part with a wood grain or groundwork pattern keeps the bar tall (Joost, FEEDBACK 3: Woodcutting and Groundwork).</summary>
        public static float BarHeightOf(IEnumerable<Block> parts, bool thin) =>
            (parts ?? Enumerable.Empty<Block>()).Any(p => p != null && p.Fraction > 0 && !string.IsNullOrEmpty(p.Pattern) && p.Pattern.StartsWith("vocab:grain-", StringComparison.Ordinal)) ? BarFormTall : thin ? BarFormThin : BarFormBar;

        /// <summary>How tall a bar with its list stands (its head line when it has one): what a page's fold test counts.</summary>
        public static double BarFormHeight(int rows, bool head) => (head ? 18 + 6 : 0) + BarFormBar + (rows > 1 ? BarFormToList + BarListLines(rows) * (BarFormRow + BarFormRowGap) - BarFormRowGap : 0) + BarFormAfter;

        /// <summary>0.8 layout D+ (Joost, after step A): one legend grid per page. Every legend on a page uses the same equal columns across a
        /// wide island's width, so the islands' legends line up column for column: LegendColumnsMax of them when every legend's columns fit, else
        /// one fewer for the whole page (LegendPage), never mixed. An entry stays compact in its column (name at the 14 px floor on every legend of the
        /// book, the number right-aligned in its own small column, the share); the room left over is the space between columns. LegendGap: the least
        /// space between two columns (Joost: Cooking's By type on one row, Dishes by kind two rows of four).</summary>
        public const int LegendColumnsMax = 4;
        public const float LegendGap = 10;

        /// <summary>The columns a legend gets and their width, for a page grid of <paramref name="page"/> columns over a wide island's inner
        /// width <paramref name="wide"/>: a wide island takes the grid itself; a narrower column (a half island) as many as nearly fit at the
        /// grid's width, sharing its own width evenly (half of four: two).</summary>
        public static (int columns, float width) LegendGrid(int page, float column, float wide)
        {
            page = Math.Max(1, page);
            var cw = (wide - (page - 1) * LegendGap) / page;
            if (column >= wide - 1) return (page, cw);
            var k = Math.Max(1, (int)Math.Round((column + LegendGap) / (cw + LegendGap)));
            return (k, (column - (k - 1) * LegendGap) / k);
        }

        /// <summary>The page's legend columns: the most (up to LegendColumnsMax) at which every legend fits, each given as its column's width
        /// and its widest column when cut into k columns (need(k), measured by the renderer: the game's text, the preview's).</summary>
        public static int LegendPage(IEnumerable<(float column, Func<int, float> need)> legends, float wide)
        {
            var all = (legends ?? Enumerable.Empty<(float, Func<int, float>)>()).ToList();
            for (int n = LegendColumnsMax; n > 1; n--)
                if (all.All(l => { var (k, w) = LegendGrid(n, l.column, wide); return l.need(k) <= w + 0.5f; })) return n;
            return 1;
        }

        /// <summary>The rows in each of a list's columns when it takes this many: filled top to bottom, the first columns the fuller ones.</summary>
        public static int BarListSplit(int rows, int columns) => columns <= 1 ? rows : (rows + columns - 1) / columns;

        /// <summary>The bar's rest: the folded "Other (n kinds)" or a facet's own Other. Quiet grey, always last.</summary>
        public static bool IsOtherPart(Block p) => p != null && (p.Id == FoldId || IsOtherOption(p.Id));

        /// <summary>Damage-type bars (Battle: a weapon's damage by type, what hurt you by type, Defence's foes by type) carry each part's number inside it where
        /// it fits (Joost 2026-10-09); every other bar carries no numbers.</summary>
        public static bool NumbersOnBar(Block b)
        {
            if (b == null) return false;
            if (b.Kind == "dmgmix") return true;
            var parts = (b.Items ?? new List<Block>()).Where(p => p.Fraction > 0 && !IsOtherPart(p)).ToList();
            return b.Kind == "composition" && parts.Count > 0 && parts.All(p => p.Icon != null && (p.Icon.StartsWith("damage:", StringComparison.Ordinal) || p.Icon.StartsWith("vocab:dmg-", StringComparison.Ordinal)));
        }

        /// <summary>Whole percentages of the values that add up to 100 (largest remainder; ties to the earlier one). All zero: zeros.</summary>
        public static int[] WholeShares(IList<double> values)
        {
            var total = values.Sum(v => Math.Max(0, v));
            var res = new int[values.Count];
            if (total <= 0) return res;
            var raw = values.Select(v => Math.Max(0, v) * 100.0 / total).ToArray();
            for (int k = 0; k < raw.Length; k++) res[k] = (int)Math.Floor(raw[k]);
            var rest = 100 - res.Sum();
            foreach (var k in Enumerable.Range(0, raw.Length).OrderByDescending(k => raw[k] - res[k]).ThenBy(k => k).Take(Math.Max(0, rest))) res[k]++;
            return res;
        }

        // the sum of the folded parts' numbers when every one is a count ("272", "1 040", "<1"); a unit ("37.6 km") leaves the
        // Other row without a number. A part that says "<1" adds nothing (the shown amounts add up to the shown total, Shown), and a
        // sum of only such parts says "<1" itself: a real part always shows a number (Lox's poison read "1 %" alone)
        static string SumOfCounts(IEnumerable<Block> parts)
        {
            double sum = 0; var any = false;
            foreach (var p in parts)
            {
                var v = (p.Value ?? "").Trim();
                if (v == LessThanOne) { any = true; continue; }
                if (v.Length == 0 || v.Any(c => char.IsLetter(c))) return null;
                sum += ParseCount(v); any = true;
            }
            return any && sum < 1 ? LessThanOne : N(sum);
        }

        /// <summary>
        /// The one bar form's rows for a bar's parts (in the model's order, largest first): parts with no share are left out; every part has
        /// its own row (B39, Joost 2026-10-10: no "Small parts" row; a tiny part keeps its own sliver in the bar, at least 6 px, BarParts), the
        /// Other part ("Other", "Other (n kinds)") last. Past BarMaxParts rows the smallest parts and the Other part fold into one
        /// "Other (n kinds)" row (the 0.6.5 rule; the models fold first, this only catches a bar they left long, Battle's nine damage types).
        /// Percent by largest remainder over every part, so the list adds up to 100.
        /// numbers: the parts carry their numbers (NumbersOnBar). The list: up to BarListColumns columns (0.8 layout D+, FEEDBACK 1; a renderer
        /// takes fewer where they do not fit), filled top to bottom; four rows read as a 2 x 2 grid (B34). pairs: kept for its callers, the rule is one.
        /// </summary>
        public static BarForm BarFormOf(IList<Block> parts, bool numbers = false, bool pairs = false)
        {
            var form = new BarForm { Numbers = numbers };
            var shown = (parts ?? new List<Block>()).Where(p => p != null && p.Fraction > 0).ToList();
            if (shown.Count == 0) return form;
            var sum = shown.Sum(p => (double)p.Fraction);
            var pct = WholeShares(shown.Select(p => (double)p.Fraction).ToList());
            var named = new List<BarRow>(); var others = new List<BarRow>();
            for (int k = 0; k < shown.Count; k++)
            {
                var p = shown[k];
                var row = new BarRow { Part = p, Title = p.Title, Value = p.Value, Colour = p.Colour, Fraction = (float)(p.Fraction / sum), Percent = pct[k], Selected = p.Selected, Other = IsOtherPart(p) };
                (row.Other ? others : named).Add(row);
            }
            if (named.Count + others.Count > BarMaxParts)
            {
                var rest = named.Skip(BarMaxParts - 1).Concat(others).ToList();
                named = named.Take(BarMaxParts - 1).ToList();
                var title = FoldLabel(rest.Count); var value = SumOfCounts(rest.Select(r => r.Part)); var share = rest.Sum(r => r.Fraction);
                others = new List<BarRow> { new BarRow
                {
                    Part = new Block { Kind = "part", Id = FoldId, Title = title, Value = value, Colour = BarOtherColour, Fraction = share },
                    Title = title, Value = value, Colour = BarOtherColour, Fraction = share, Percent = rest.Sum(r => r.Percent), Other = true,
                } };
            }
            form.Rows.AddRange(named);
            form.Rows.AddRange(others);
            form.List = form.Rows.Count > 1;
            var n = form.Rows.Count;
            form.Columns = Math.Max(1, Math.Min(BarListColumns, n));
            form.Split = BarListSplit(n, form.Columns);   // the first column's rows (3 + 3 + 2 for eight, 2 + 2 for four)
            return form;
        }

        /// <summary>The rows a bar's list takes (the left column when it flows into two; none for one part): what the filter's fold test counts.</summary>
        public static int BarListLines(int rows) => rows <= 1 ? 0 : BarListSplit(rows, Math.Min(BarListColumns, rows));

        /// <summary>A bar's length as a share of the full width: rows of the same kind share one scale (BAR-FORM.md: Defence's bar per
        /// foe, Battle's bar per weapon), so a block with Fraction between 0 and 1 (its total against the largest row) is that long;
        /// any other bar runs the full width.</summary>
        public static float BarLength(Block b) => b != null && b.Fraction > 0 && b.Fraction < 1 ? b.Fraction : 1;

        // ---------- the colours of abstract categories (BAR-FORM.md: D's family, a fixed colour per name) ----------

        /// <summary>
        /// The fixed colour of a known abstract category (a hammer tab, a dish type, a main boost, a gear kind): its place in
        /// BarPalette, keyed by the name as near-duplicates share it (SameLabel). Within one family (one bar) every name has its own
        /// colour; a mod's category takes a stable hash of its name instead (CategoryColour).
        /// </summary>
        static readonly Dictionary<string, int> CategoryColours = new Dictionary<string, int>
        {
            // the hammer's tabs (v4: Building slate)
            ["building"] = 0, ["stonecutter"] = 3, ["furniture"] = 7, ["crafting"] = 5, ["misc"] = 9,
            // dish types (v4: Meals umber and Grilled slate lifted; Baked brick moved off Health's red, its sister bar on Cooking)
            ["meals"] = 10, ["baked"] = 8, ["grilled"] = 5, ["feasts"] = 4, ["uncooked"] = 6, ["mead bases"] = 9,
            // main boosts: the game's food colours in the family's tones (health red, stamina yellow, eitr blue)
            ["health"] = 7, ["stamina"] = 1, ["eitr"] = 0, ["balanced"] = 3,
            // gear kinds
            ["weapons"] = 7, ["armor"] = 0, ["tools"] = 3, ["trinkets"] = 4,
            // the weapon kinds, as parts of a damage type's bar (0.8 Battle > Damage, By type): clay, teal and plum, apart from every damage colour
            ["melee"] = 10, ["bow"] = 9, ["magic"] = 8,
        };

        /// <summary>Where a category's colour starts in BarPalette: its fixed place for a known name, else a stable hash of the name
        /// (FNV-1a over its SameLabel form, so it is the same on every page, window and PC).</summary>
        public static int CategoryIndex(string name)
        {
            var key = SameLabel(name);
            if (CategoryColours.TryGetValue(key, out var i)) return i;
            uint h = 2166136261;
            foreach (var c in key) { h ^= c; h *= 16777619; }
            return (int)(h % (uint)BarPalette.Length);
        }

        /// <summary>A category's own colour (before any step on a crowded bar).</summary>
        public static string CategoryColour(string name) => BarPalette[CategoryIndex(name)];

        static bool KnownCategory(string name) => CategoryColours.ContainsKey(SameLabel(name));
    }
}
