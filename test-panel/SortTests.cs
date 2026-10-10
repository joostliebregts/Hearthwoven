// Tests for the sort control (src/Panel/SortModel.cs): Most, A-Z and By category over a page's item list, with a filter on.
// Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class SortTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelView Show(PanelInput i, PanelState st, string page) { st.Chapter = Chapter.Deeds; st.Page[Chapter.Deeds] = page; return PanelModel.Build(i, st); }
    static List<Block> Tiles(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "itemgrid").SelectMany(b => b.Items).ToList();
    static string Line(Block i) => i.Id + "|" + i.Title + "|" + i.Value + "|" + i.Value2 + "|" + i.Text;
    // every number on the page that is not a sort group's own heading
    static string Numbers(PanelView v) => string.Join(";", PanelModel.Content(v).SelectMany(b => new[] { b }.Concat(b.Items ?? new List<Block>()))
                                                       .Where(b => b.Kind != "item" && b.Kind != "itemgrid" && !(b.Kind == "section" && b.Tone == PanelModel.SortGroup))
                                                       .Select(b => b.Kind + ":" + b.Value + "/" + b.Value2 + "/" + b.Text).OrderBy(x => x, StringComparer.Ordinal));

    public static int Run(DateTime now)
    {
        fails = 0;
        var input = PanelSample.Full(now);

        // ---------- sorting keeps every item and every number; the choice is a click on the pill, and it sorts what the filter leaves ----------
        foreach (var (page, filter, facet, option) in new[] { ("building", PanelModel.BuildFilter, "material", "Wood"), ("crafting", PanelModel.CraftFilter, "kind", "weapons") })
        {
            var st = new PanelState(); PanelModel.ToggleFacet(st, filter, facet, option);
            var most = Show(input, st, page);
            var control = PanelModel.SortOf(most);
            var baseline = Tiles(most).Select(Line).ToList();
            var ok = control != null && baseline.Count >= 2 && control.Items.Select(o => o.Id).SequenceEqual(new[] { PanelModel.SortMost, PanelModel.SortAz, PanelModel.SortGroup }) && control.Items[0].Selected;
            foreach (var o in control?.Items ?? new List<Block>())
            {
                PanelModel.Follow(st, PanelModel.ViewLink(control, o));
                var v = Show(input, st, page);
                var tiles = Tiles(v).Select(Line).ToList();
                ok &= PanelModel.SortOf(v).Items.Single(x => x.Selected).Id == o.Id
                      && tiles.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(baseline.OrderBy(x => x, StringComparer.Ordinal))
                      && Numbers(v) == Numbers(most) && PanelModel.FilterOf(v).Text == PanelModel.FilterOf(most).Text;
            }
            Check(ok, "sort " + page + ": Most, A-Z and By " + (page == "building" ? "category" : "kind") + " over the filtered list (" + baseline.Count + " tiles) keep every item and every number, the filter's result line too; a click on a pill picks the order");
        }

        // ---------- 0.8 (Joost 2026-10-10): a click on the chosen pill flips it, Most to Least and A-Z to Z-A and back; By category stays ----------
        {
            var st = new PanelState();
            List<string> Titles() => Tiles(Show(input, st, "building")).Select(t => t.Title).ToList();
            string Pills() => string.Join(" ", PanelModel.SortOf(Show(input, st, "building")).Items.Select(o => (o.Selected ? "*" : "") + o.Title));
            void Click(int at) { var c = PanelModel.SortOf(Show(input, st, "building")); PanelModel.Follow(st, PanelModel.SortLink(c, c.Items[at])); }
            var most = Titles();
            Click(0); var least = Titles(); var leastPills = Pills();
            Click(0); var back = Titles();
            Click(1); var az = Titles(); Click(1); var za = Titles(); var zaPills = Pills();
            Click(2); Click(2); var grouped = Pills();
            Check(most.Count > 4 && least.SequenceEqual(Enumerable.Reverse(most)) && back.SequenceEqual(most) && za.SequenceEqual(Enumerable.Reverse(az)) &&
                  leastPills == "*Least A-Z By category" && zaPills == "Most *Z-A By category" && grouped == "Most A-Z *By category",
                  "sort flip: the chosen pill clicked again turns the list round (" + leastPills + "; " + zaPills + "), a second click turns it back; By category stays (" + grouped + ")");
        }

        // ---------- A-Z goes by the name the player sees, not the prefab id ----------
        {
            var st = new PanelState(); st.View[PanelModel.SortKey(new PanelState { Chapter = Chapter.Deeds }, "building")] = PanelModel.SortAz;
            var tiles = Tiles(Show(input, st, "building"));
            var byName = tiles.Select(t => t.Title).ToList();
            var expected = byName.OrderBy(t => t, Comparer<string>.Create((a, b) => CultureInfo.InvariantCulture.CompareInfo.Compare(a, b, CompareOptions.IgnoreCase))).ToList();
            var byId = tiles.OrderBy(t => t.Id, StringComparer.Ordinal).Select(t => t.Title).ToList();
            var mixed = PanelModel.ByName(new[] { new Block { Id = "$a", Title = "wood wall" }, new Block { Id = "$b", Title = "Ägir stone" }, new Block { Id = "$c", Title = "Bench" } }).Select(b => b.Title);
            Check(tiles.Count > 4 && byName.SequenceEqual(expected) && !byName.SequenceEqual(byId) && mixed.SequenceEqual(new[] { "Ägir stone", "Bench", "wood wall" }),
                  "sort A-Z: Building's pieces by their shown names (" + string.Join(", ", byName.Take(4)) + " ...), not by prefab id; case and accents read as letters (Ägir before Bench)");
        }

        // ---------- By category: one group per hammer tab, the groups by their totals, most first inside each ----------
        {
            var st = new PanelState(); st.View["Deeds/building/sort"] = PanelModel.SortGroup;
            var v = Show(input, st, "building");
            var blocks = PanelModel.Content(v);
            var heads = blocks.Where(b => b.Kind == "section" && b.Tone == PanelModel.SortGroup).ToList();
            var ok = heads.Count >= 2;
            double Num(string s) => PanelModel.ParseCount(s);
            for (int k = 0; k < heads.Count && ok; k++)
            {
                var grid = blocks[blocks.IndexOf(heads[k]) + 1];
                var values = grid.Items.Select(i => Num(i.Value)).ToList();
                ok &= grid.Kind == "itemgrid" && Num(heads[k].Value) == values.Sum()                                  // its total is its tiles
                      && values.SequenceEqual(values.OrderByDescending(x => x))                                       // most first inside
                      && (k == 0 || Num(heads[k - 1].Value) >= Num(heads[k].Value))                                   // the groups by their totals
                      && grid.Items.All(i => PanelModel.SameLabel(input.PieceTab?.Invoke(i.Id) ?? PanelModel.MaterialOther) == PanelModel.SameLabel(heads[k].Title)) // each piece under its own tab ("Misc." counts with "Misc", 0.6.5 bars)
                      && !string.IsNullOrEmpty(heads[k].Colour);
            }
            string Why() { var w = new List<string>(); for (int k = 0; k < heads.Count; k++) { var g = blocks[blocks.IndexOf(heads[k]) + 1]; var vs = g.Items?.Select(i => Num(i.Value)).ToList() ?? new List<double>();
                if (g.Kind != "itemgrid") w.Add(heads[k].Title + ": next is " + g.Kind); else { if (Num(heads[k].Value) != vs.Sum()) w.Add(heads[k].Title + ": total " + heads[k].Value + " vs " + vs.Sum());
                if (!vs.SequenceEqual(vs.OrderByDescending(x => x))) w.Add(heads[k].Title + ": order"); if (k > 0 && Num(heads[k - 1].Value) < Num(heads[k].Value)) w.Add(heads[k].Title + ": group order");
                foreach (var i in g.Items) if (PanelModel.SameLabel(input.PieceTab?.Invoke(i.Id) ?? PanelModel.MaterialOther) != PanelModel.SameLabel(heads[k].Title)) w.Add(i.Id + " tab " + input.PieceTab?.Invoke(i.Id) + " under " + heads[k].Title);
                if (string.IsNullOrEmpty(heads[k].Colour)) w.Add(heads[k].Title + ": no colour"); } } return string.Join("; ", w); }
            Check(ok, (ok ? "" : "[" + Why() + "] ") + "sort By category: Building in groups by the hammer's tab (" + string.Join(", ", heads.Select(h => h.Title + " " + h.Value)) + "), the largest group first, each with its colour and total, most first inside");
        }
        return fails;
    }
}
