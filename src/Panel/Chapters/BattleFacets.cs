using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Battle's filters (FILTERING-06.md): the reusable filter bar (FacetModel.cs) on the three pages whose data really carries
    /// the dimensions. One FacetItem is one distinct (biome, foe, weapon skill, damage type) of your hits with its damage as
    /// weight, so a chip's count is damage dealt, before the foe's armour, and the page's blocks are rebuilt from the rows that pass.
    ///   Overview: the biome tiles ARE the Biome row (inline filter bar: no chip row, the tiles carry the choice and the cursor),
    ///             counted over the window's log (or the per-biome totals since install).
    ///   Damage:   Biome and Foe rows beside the existing grid. Biome only where the rows have one (the session log; the
    ///             since-install tally has none, and one short line says so). Foe: the six that took most of your damage, said as such ("Top foes").
    ///   Foes:     Weapon (the skill, grouped), Damage type and Kin (the creature's faction from the game), since install only:
    ///             the tally has no biome and no time.
    /// The time window stays a scope row in the heading; the facets count inside it. A row with fewer than two choices is not
    /// offered (nothing to narrow). Chosen chips live in PanelState.Facets under the page's filter id.
    /// </summary>
    public static partial class PanelModel
    {
        public const string BattleOverviewFilter = "Battle/overview", BattleDamageFilter = "Battle/damage", BattleFoesFilter = "Battle/foes";
        public const string NothingForChoice = "Nothing for this choice";

        static int BiomeRank(string b) { var i = Array.FindIndex(BiomeTiles, t => t.key == b); return i < 0 ? 99 : i; }

        /// <summary>"in the Swamp", "in the Swamp and Plains", "in 3 biomes": what the chosen biomes are called in a legend; null = none chosen.</summary>
        public static string ScopeOfSet(ICollection<string> biomes)
        {
            if (biomes == null || biomes.Count == 0) return null;
            var list = biomes.OrderBy(BiomeRank).ToList();
            return list.Count == 1 ? ScopeOf(list[0]) : list.Count == 2 ? "in the " + BiomeName(list[0]) + " and " + BiomeName(list[1]) : "in " + list.Count + " biomes";
        }

        // ---------- the generic part: filter your hits ----------

        /// <summary>What a Battle filter leaves: the bar (null when no row has two choices) and the hits that pass every row.</summary>
        public sealed class RowFilter { public Block Bar; public List<DamageRow> Rows = new List<DamageRow>(); public double Total; public bool Narrowed; }

        /// <summary>
        /// Builds the filter bar over your hits (dealt rows). values: the facet values of one hit (facet id -> option id); the
        /// hits are folded to their distinct combinations first, so a chip's count is the damage of exactly those.
        /// none: the applied line with nothing chosen. say: the result line from (damage that passes, damage in all).
        /// </summary>
        static RowFilter FilterRows(PanelState state, string filter, IList<FacetDef> defs, List<DamageRow> dealt, Func<DamageRow, Dictionary<string, string>> values,
                                    string none, Func<double, double, string> say, Func<string, string, string> labelOf)
        {
            var total = dealt.Sum(r => (double)r.Amount);
            if (defs.Count == 0) return new RowFilter { Rows = dealt, Total = total };
            // a choice made in another window that has no chip here (a foe not fought in this window) stays as a chip with no count:
            // the page says "Nothing for this choice" and the chip is there to unchoose, never a filter you cannot see
            foreach (var d in defs)
                foreach (var id in Chosen(state, filter, d.Id).Where(id => d.Options.All(o => o.Id != id)).ToList())
                    d.Options.Add(new FacetOption { Id = id, Label = labelOf(d.Id, id) });
            var folded = dealt.GroupBy(r => (r.Biome ?? "", r.Other ?? "", r.Cause ?? "", r.Type ?? ""))
                              .Select(g => new DamageRow { Biome = g.Key.Item1, Other = g.Key.Item2, Cause = g.Key.Item3, Type = g.Key.Item4, Dir = "dealt", Amount = g.Sum(x => x.Amount) }).ToList();
            var items = new List<FacetItem>();
            for (int i = 0; i < folded.Count; i++)
            {
                var it = new FacetItem { Key = i.ToString(Inv), Weight = folded[i].Amount };
                foreach (var kv in values(folded[i])) it.Values[kv.Key] = kv.Value;
                items.Add(it);
            }
            var res = Facets(state, filter, defs, items, "dealt", SrcPc, "hit", "hits", none);
            var rows = res.Shown.Select(it => folded[int.Parse(it.Key, Inv)]).ToList();
            var passed = rows.Sum(r => (double)r.Amount);
            var line = say(passed, total);
            res.Bar.Text = line; res.Bar.Tone = "compact";
            var applied = res.Bar.Items.First(b => b.Kind == "applied"); applied.Text = line;
            return new RowFilter { Bar = res.Bar, Rows = rows, Total = total, Narrowed = defs.Any(d => Chosen(state, filter, d.Id).Count > 0) };
        }

        static string DamageSaid(double passed, double total) => (passed < total ? NAtLeast(passed) + " of " + NAtLeast(total) : NAtLeast(total)) + " damage dealt";

        // ---------- Overview: the biome tiles are the filter ----------

        /// <summary>
        /// The overview's filter bar over its biome strip: the tiles are the Biome row. The strip carries the filter id (Id),
        /// the chosen tiles (Selected) and the keys' cursor (Note "cursor"); the returned bar is inline (Tone "inline": only
        /// the applied line is drawn, under the strip). A tile with nothing in the window is a dimmed chip that cannot be chosen.
        /// </summary>
        static Block BiomeFilterBar(PanelState state, string filter, Block strip, List<DamageRow> rows, List<EventLog.Death> deaths)
        {
            var tiles = (strip?.Items ?? new List<Block>()).Where(t => t.Kind == "biome").ToList();
            // a strip without a single number (a first evening) is left out of the page (ZonesModel), and so is its filter
            if (tiles.Count == 0 || !tiles.Any(t => (t.Value ?? "").Any(char.IsDigit) || (t.Value2 ?? "").Any(char.IsDigit) || t.Count > 0)) return null;
            var defs = new List<FacetDef> { new FacetDef { Id = "biome", Title = "Biome", Bar = false, Options = tiles.Select(t => new FacetOption { Id = t.Id, Label = t.Title, Colour = t.Colour }).ToList() } };
            var items = tiles.Select(t =>
            {
                var weight = rows.Where(r => r.Biome == t.Id && (r.Dir == "taken" || (r.Dir == "dealt" && r.Amount > 0 && BattleTypes.Contains(r.Type)))).Sum(r => (double)r.Amount) + deaths.Count(d => d.Biome == t.Id);
                var it = new FacetItem { Key = t.Id, Weight = weight };
                it.Values["biome"] = t.Id;
                return it;
            }).ToList();
            var res = Facets(state, filter, defs, items, "damage", SrcPc, "biome", "biomes", "No filter: all biomes · press a tile to narrow");
            var chosen = Chosen(state, filter, "biome");
            var line = chosen.Count == 0 ? "" : chosen.Count + " of " + tiles.Count + " biomes";
            res.Bar.Text = line; res.Bar.Items.First(b => b.Kind == "applied").Text = line;
            res.Bar.Tone = "inline";
            strip.Id = filter;
            var row = res.Bar.Items.First(b => b.Kind == "facet");
            foreach (var chip in row.Items)
            {
                var tile = tiles.First(t => t.Id == chip.Id);
                tile.Selected = chip.Selected;
                if (chip.Tone == "cursor") tile.Note = "cursor";
            }
            return res.Bar;
        }

        static List<DamageRow> InBiomes(List<DamageRow> rows, ICollection<string> chosen) => chosen.Count == 0 ? rows : rows.Where(r => chosen.Contains(r.Biome)).ToList();
        static List<EventLog.Death> InBiomes(List<EventLog.Death> deaths, ICollection<string> chosen) => chosen.Count == 0 ? deaths : deaths.Where(d => chosen.Contains(d.Biome)).ToList();

        // ---------- Damage and Deaths: the same Biome row, in the page (fix3: no heading chip, one pattern on all three pages) ----------

        public const string BattleDeathsFilter = "Battle/deaths";
        /// <summary>Damage since install keeps no biome on its rows (the weapon is not folded per biome): the Biome line says so instead of a dim, dead control.</summary>
        public const string DamageAllBiomeNote = "Not kept per biome since install · choose a time window to narrow by biome";

        /// <summary>The Biome row both pages share: every biome the character found, in journey order with the Ocean last (the strip's order and colours).</summary>
        static FacetDef BiomeRow(PanelInput input)
        {
            var found = new HashSet<string>(FoundBiomes(input));
            return new FacetDef { Id = "biome", Title = "Biome", Bar = false,
                                  Options = BiomeTiles.Where(t => found.Contains(t.key)).Select(t => new FacetOption { Id = t.key, Label = BiomeName(t.key), Colour = t.colour }).ToList() };
        }

        // the bar's own words: the header says Biome (not "Filter"), and with nothing chosen it says what shows
        static Block BiomeBar(Block bar)
        {
            bar.Title = "Biome";
            bar.Items.First(b => b.Kind == "applied").Value = "All biomes";
            return bar;
        }

        /// <summary>The line that stands where the Biome row would, for a page whose rows carry no biome (Damage since install): same place, same words, no key.</summary>
        static Block BiomeLine(string line) =>
            new Block { Kind = "filterbar", Id = BattleDamageFilter, Tone = "inline", Title = "Biome", Items = new List<Block> { new Block { Kind = "applied", Title = line, Items = new List<Block>() } } };

        /// <summary>Damage's Biome row over your hits: a chip per found biome with the damage dealt there; null bar when fewer than two biomes are found.</summary>
        static RowFilter DamageBiomeFilter(PanelInput input, PanelState state, List<DamageRow> rows)
        {
            var dealt = DealtRows(rows);
            var def = BiomeRow(input);
            if (def.Options.Count < 2) return new RowFilter { Rows = dealt, Total = dealt.Sum(r => (double)r.Amount) };
            var rf = FilterRows(state, BattleDamageFilter, new List<FacetDef> { def }, dealt, r => new Dictionary<string, string> { ["biome"] = r.Biome ?? "" },
                                "All biomes", DamageSaid, (facet, id) => BiomeName(id));
            BiomeBar(rf.Bar);
            return rf;
        }

        static string FallsSaid(int shown, int all) => (shown < all ? shown + " of " + all : all.ToString(Inv)) + (all == 1 ? " fall" : " falls");

        /// <summary>Deaths' Biome row: a chip per found biome with the falls there (the strip's source: the per-biome tally since install, else the window's log).
        /// The returned bar is null when fewer than two biomes are found; the strip keeps every biome, the list narrows to the chosen ones.</summary>
        static Block DeathsBiomeBar(PanelInput input, PanelState state, List<EventLog.Death> source)
        {
            var def = BiomeRow(input);
            foreach (var id in Chosen(state, BattleDeathsFilter, "biome").Where(id => def.Options.All(o => o.Id != id)).ToList()) def.Options.Add(new FacetOption { Id = id, Label = BiomeName(id) });
            if (def.Options.Count < 2 || (source.Count == 0 && Chosen(state, BattleDeathsFilter, "biome").Count == 0)) return null;   // nothing to narrow
            var items = source.Select((d, i) => { var it = new FacetItem { Key = i.ToString(Inv), Weight = 1 }; it.Values["biome"] = d.Biome ?? ""; return it; }).ToList();
            var res = Facets(state, BattleDeathsFilter, new List<FacetDef> { def }, items, "falls", SrcPc, "fall", "falls", "All biomes");
            var line = FallsSaid(res.Shown.Count, source.Count);
            res.Bar.Text = line; res.Bar.Tone = "compact"; res.Bar.Items.First(b => b.Kind == "applied").Text = line;
            return BiomeBar(res.Bar);
        }
    }
}
