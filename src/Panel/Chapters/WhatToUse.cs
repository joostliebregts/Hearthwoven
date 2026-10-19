using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 arrows track, "What to use" (board 7, work/hearthwoven-0.9/sketch/project/WhatToUse.dc.html; Joost 18 Oct: "it doesn't show me which
    /// foes", "I still want to see like this arrow does the most damage to this foe", "there should be like some filters or tabs", and the main page
    /// not overloaded). A third view on Battle > Foes (after By foe and By damage type), and the same view on Last fight over that fight's foes.
    ///
    ///   tabs        a switch (Id "Battle/&lt;page&gt;/usetab"): Arrows · Bolts · Weapons
    ///   sets        a switch under it ("Battle/&lt;page&gt;/useset"): You know (owned, made, can craft) · You can make (owned or made, or can craft
    ///               with every recipe material brought in during the window in the land where you fought these foes: Gathered, FoughtIn) · All. Its default:
    ///               You can make when it holds two items or more, else You know.
    ///   cards       Kind "cards", Tone SayTone each (PanelUi.Cards: a title and a sentence): up to three, worked out from the table as board 7's
    ///               prototype does (UseCards): in the quiver / in the crossbow / in hand, as backup, for big game.
    ///   table       Kind UseTableKind (a variant of the foe table: the foes as columns): Items Kind "usefoe" per column (the 7 foes you dealt most
    ///               damage to that the game knows: Id prefab, Icon trophy, Title, Selected = its detail open), then Kind "userow" per item (the most
    ///               damage over these foes first, at most UseRows: Id, Icon, Title, Text = status, Tone "unknown" when not known yet, Note = its
    ///               materials, Items = Kind "part" per damage type (Id, Title = the type, Value, Colour) and Kind "usecell" per column (Id the foe,
    ///               Value = Expected: base damage times the foe's multipliers, Fraction = against the table's highest cell (one length scale),
    ///               Tone UseBest on the column's highest (a tie: both; drawn with a gold edge) or UseLow under three quarters of the item's own
    ///               damage (resisted), Items = Kind "part"
    ///               per damage type after the foe's multiplier, in the type's colour, Fraction its share). A column's Items: Kind "weak" per type
    ///               the foe takes more damage from (a multiplier above 1), the type's colour and icon. Title = the head of the name column, Note = the italic line under it, Text = how many foes are left out.
    ///   detail      a click on a column opens that foe's ranking (FoeRanking: Best and Worst arrows, bolts, weapons) under the table (Kind
    ///               UseDetailKind, its Title the foe's name; "Battle/&lt;page&gt;/useopen"). By foe and its open foe stay as they are.
    /// Your own book only (the gear and the gathering are yours). Pure C#: test-panel/WhatToUseTests.cs.
    /// </summary>
    public static partial class PanelModel
    {
        public const string UseView = "use", UseLabel = "What to use";
        public const string UseTableKind = "usetable", UseDetailKind = "usedetail", SayTone = "say", UseBest = "best", UseLow = "low";
        public const string UseKnown = "known", UseMake = "make", UseAll = "all";
        /// <summary>The most foe columns and item rows the table shows.</summary>
        public const int UseFoes = 7, UseRows = 10;
        /// <summary>A cell under this share of the item's own damage is quiet: the foe resists it.</summary>
        public const double UseResisted = 0.75;
        public const string UseAboutTitle = "What to use",
                            UseAbout = "Each foe you hit is a column, the 7 you hit most; each cell is the item's base damage times that foe's multipliers (upgrades and skill " +
                                       "are not counted), its bar what that damage is made of, on one scale for the table. A gold edge is the most against that foe, a tie " +
                                       "on both; a quiet cell is resisted. You can make: what you own or made, or can craft with every material brought in during this " +
                                       "window in the land where you fought these foes (most of your damage there; this PC notes your land each minute, days and All keep " +
                                       "none, so there all you brought in counts). The cards on top say it in words. Click a foe's column for its own best and worst.";

        static readonly (string id, string label, string one, string many)[] UseTabs = { ("arrow", "Arrows", "arrow", "arrows"), ("bolt", "Bolts", "bolt", "bolts"), ("weapon", "Weapons", "weapon", "weapons") };

        /// <summary>The click on a column's head: opens its foe's detail under the table, or shuts it when open.</summary>
        public static string UseOpenLink(Block table, Block column) => ViewTarget + table.Id + "=" + (column != null && !column.Selected ? column.Id : "");

        /// <summary>Foes' What to use over the window's rows (the foes and your damage to each, as the By foe table groups them).</summary>
        public static List<Block> WhatToUseForFoes(PanelInput input, PanelInput src, PanelState state, string page, List<DamageRow> rows, TimeWindow w)
        {
            if (input == null || !input.IsSelf) return null;
            var foes = DealtRows(rows).GroupBy(r => FoeGroup(input, r.Other)).Select(g => (foe: g.Key, dealt: g.Sum(r => (double)r.Amount))).ToList();
            return WhatToUse(input, state, page, foes, GatheredIn(input, src, w, rows));
        }

        /// <summary>
        /// The view's blocks (its tab switch, which holds the set switch, which holds the cards, the table and the open foe's detail), over
        /// <paramref name="foes"/> (a foe's prefab and your damage dealt to it). null on a fellow's book, without a foe the game knows, or without gear data.
        /// </summary>
        public static List<Block> WhatToUse(PanelInput input, PanelState state, string page, IEnumerable<(string foe, double dealt)> foes, Gathered gathered)
        {
            if (input == null || !input.IsSelf || foes == null) return null;
            var all = foes.Where(f => f.dealt > 0).GroupBy(f => f.foe).Select(g => (foe: g.Key, dealt: g.Sum(x => x.dealt), data: FoeOf(input, g.Key)))
                          .Where(f => f.data != null).OrderByDescending(f => f.dealt).ThenBy(f => f.foe, StringComparer.Ordinal).ToList();
            if (all.Count == 0) return null;
            var cols = all.Take(UseFoes).ToList(); var leftOut = all.Count - cols.Count;
            IList<ArrowData> Read(Func<IList<ArrowData>> f) { try { return f?.Invoke() ?? new List<ArrowData>(); } catch { return new List<ArrowData>(); } }
            var gear = Read(input.Gear);
            var lists = new Dictionary<string, List<ArrowData>>
            {
                ["arrow"] = Read(input.Arrows).Where(a => a.Kind == "arrow").ToList(),
                ["bolt"] = gear.Where(g => g.Kind == "bolt").ToList(),
                ["weapon"] = gear.Where(g => g.Kind == "weapon" && (g.Recipe || (g.Cost != null && g.Cost.Count > 0) || GearStatus(input, g) != GearNotKnown)).ToList(),   // a creature's copy of a weapon is no weapon of yours
            };
            if (lists.Values.All(l => l.Count == 0)) return null;
            string Key(string name) => state.Chapter + "/" + page + "/" + name;
            state.View.TryGetValue(Key("useopen"), out var open);
            var openFoe = cols.Any(c => c.foe == open) ? open : null;
            var sum = cols.Sum(c => c.dealt);
            double Weighted(ArrowData a) => cols.Sum(c => c.dealt * Expected(c.data, a)) / sum;
            bool Brought(ArrowData a) => a.Cost != null && a.Cost.Count > 0 && gathered?.Items != null && a.Cost.All(c => Has(gathered.Items, c.Token));

            var tabViews = new List<Block>();
            var tabPick = state.View.TryGetValue(Key("usetab"), out var tp) && UseTabs.Any(t => t.id == tp && lists[t.id].Count > 0) ? tp : UseTabs.First(t => lists[t.id].Count > 0).id;
            foreach (var tab in UseTabs)
            {
                if (lists[tab.id].Count == 0) continue;
                var items = OneLinePerItem(input, lists[tab.id]).Select(x => (x.g, x.name, status: GearStatus(input, x.g), score: Weighted(x.g)))
                                                               .OrderByDescending(x => x.score).ThenBy(x => x.name, StringComparer.Ordinal).ToList();
                var known = items.Where(x => x.status != GearNotKnown).ToList();
                var make = known.Where(x => x.status == GearOwned || x.status == GearMade || (x.status == GearCanCraft && Brought(x.g))).ToList();
                var setDefault = make.Count >= 2 ? UseMake : UseKnown;
                var setPick = state.View.TryGetValue(Key("useset"), out var sp) && (sp == UseKnown || sp == UseMake || sp == UseAll) ? sp : setDefault;
                var setViews = new List<Block>();
                foreach (var (id, label, list) in new[] { (UseKnown, "You know", known), (UseMake, "You can make", make), (UseAll, "All", items) })
                {
                    var chosen = tab.id == tabPick && id == setPick;
                    var blocks = chosen ? UseSet(input, tab, id, list.Select(x => (x.g, x.name, x.status, x.score)).ToList(), known.Count, make.Count, items.Count, cols, leftOut, openFoe, Key("useopen"), gathered) : null;
                    setViews.Add(new Block { Kind = "view", Id = id, Title = label, Selected = id == setPick, Items = blocks });
                }
                var setCaption = "";
                var setSwitch = new Block { Kind = "switch", Id = Key("useset"), Title = setCaption, Items = setViews };
                tabViews.Add(new Block { Kind = "view", Id = tab.id, Title = tab.label, Selected = tab.id == tabPick, Items = tab.id == tabPick ? new List<Block> { setSwitch } : null });
            }
            var against = cols.Count == 1 ? "Against the foe you hit" : "Against the " + cols.Count + " foes you hit" + (leftOut > 0 ? " most" : "");
            return new List<Block> { new Block { Kind = "switch", Id = Key("usetab"), Title = against, Items = tabViews, Tone = page == LastFightPage ? InPageTone : null } };
        }

        // one set of one tab: the cards, the table, the open foe's detail
        static List<Block> UseSet(PanelInput input, (string id, string label, string one, string many) tab, string set, List<(ArrowData g, string name, string status, double score)> rows,
                                  int known, int make, int all, List<(string foe, double dealt, FoeData data)> cols, int leftOut, string openFoe, string openKey, Gathered gathered)
        {
            var blocks = new List<Block>();
            if (rows.Count == 0)
            {
                blocks.Add(new Block { Kind = "note", Text = set == UseMake ? "Nothing you can make from what you brought in" + LandWords(gathered) + ": choose You know or All." : "None known yet: choose All." });
                return blocks;
            }
            var cards = UseCards(input, tab, rows, cols);
            if (cards.Count > 0) blocks.Add(new Block { Kind = "cards", Src = SrcPc, Items = cards });
            var shown = rows.Take(UseRows).ToList();
            var colMax = cols.Select(c => shown.Max(r => Expected(c.data, r.g))).ToList();
            var tableMax = colMax.DefaultIfEmpty(0).Max();   // one length scale for every cell's bar
            var items = new List<Block>();
            foreach (var c in cols)
                items.Add(new Block { Kind = "usefoe", Id = c.foe, Icon = TrophyOf(input, c.foe) is string t && t.Length > 0 ? t : FoeMark, Title = Who(input, c.foe), Selected = c.foe == openFoe, Src = SrcPc,
                                      // its weaknesses as small type swatches (the foe table's colours and icons): the types it takes more damage from
                                      Items = BattleTypes.Where(ty => Multiplier(c.data.Modifiers.TryGetValue(ty, out var m) ? m : "Normal") > 1f)
                                                         .Select(ty => new Block { Kind = "weak", Id = ty, Title = TypeName(ty), Icon = DamageIcon(ty), Colour = DamageColour(ty) }).ToList() });
            foreach (var r in shown)
            {
                var row = new Block { Kind = "userow", Id = r.g.Prefab, Icon = "item:" + r.g.Prefab, Title = r.name, Text = r.status, Tone = r.status == GearNotKnown ? "unknown" : null, Src = SrcPc,
                                      Note = Materials(input, r.g), Items = new List<Block>() };
                foreach (var t in BattleTypes.Where(t => r.g.Damage.TryGetValue(t, out var d) && d > 0))
                    row.Items.Add(new Block { Kind = "part", Id = t, Title = t, Value = N(Math.Round(r.g.Damage[t], MidpointRounding.AwayFromZero)), Colour = DamageColour(t), Src = SrcPc });
                for (int k = 0; k < cols.Count; k++)
                {
                    var v = Expected(cols[k].data, r.g); var max = colMax[k];
                    var data = cols[k].data;
                    double Part(string t) => r.g.Damage.TryGetValue(t, out var d) ? d * Multiplier(data.Modifiers.TryGetValue(t, out var m) ? m : "Normal") : 0;   // as Expected adds it up
                    row.Items.Add(new Block
                    {
                        Kind = "usecell", Id = cols[k].foe, Value = N(Math.Round(v, MidpointRounding.AwayFromZero)), Fraction = tableMax > 0 ? (float)(v / tableMax) : 0, Src = SrcPc,
                        Tone = shown.Count > 1 && max > 0 && v > max - 1e-3 ? UseBest : v < BattleTypes.Sum(t => r.g.Damage.TryGetValue(t, out var d) ? d : 0) * UseResisted ? UseLow : null,
                        // its small bar: a part per damage type after this foe's multiplier, in the type's colour (Fraction = its share of the cell)
                        Items = BattleTypes.Where(t => Part(t) > 0).Select(t => new Block { Kind = "part", Id = t, Title = TypeName(t), Colour = DamageColour(t), Src = SrcPc,
                                                                                            Value = N(Math.Round(Part(t), MidpointRounding.AwayFromZero)), Fraction = v > 0 ? (float)(Part(t) / v) : 0 }).ToList(),
                    });
                }
                items.Add(row);
            }
            var what = rows.Count + " " + (rows.Count == 1 ? tab.one : tab.many) + (set == UseMake ? " you can make from what you brought in" + LandWords(gathered) : set == UseKnown ? " you know" : " in the game") +
                       (all > rows.Count ? " · " + (all - rows.Count) + " more under All" : "");
            if (rows.Count > shown.Count) what += " · the " + shown.Count + " that do the most here shown";
            blocks.Add(new Block
            {
                Kind = UseTableKind, Id = openKey, Src = SrcPc, Source = TagMeasured, Title = char.ToUpperInvariant(tab.one[0]) + tab.one.Substring(1) + ", then each foe you hit",
                Note = what + " · gold edge = the most against that foe · the bar: what the damage is made of",
                Text = leftOut > 0 ? leftOut + (leftOut == 1 ? " more foe you hit is" : " more foes you hit are") + " left out: these are the " + cols.Count + " you hit most." : null,
                Items = items,
            });
            if (openFoe != null)
            {
                var c = cols.First(x => x.foe == openFoe);
                var detail = FoeRanking(input, c.data);
                detail.Kind = UseDetailKind; detail.Id = c.foe; detail.Title = Who(input, c.foe); detail.Text = "best and worst against it";
                blocks.Add(detail);
            }
            return blocks;
        }

        static string LandWords(Gathered g) => g == null ? "" : !string.IsNullOrEmpty(g.Land) ? ", in the " + BiomeName(g.Land) + ", where you fought these" : g.Items == null ? "" : ", " + g.When;

        /// <summary>A recipe as the row says it: "8 Wood · 4 Obsidian · 2 Feathers"; null when no recipe makes it.</summary>
        static string Materials(PanelInput input, ArrowData a) =>
            a.Cost == null || a.Cost.Count == 0 ? null : string.Join(" · ", a.Cost.Select(c => N(c.Amount) + " " + (string.IsNullOrEmpty(c.Name) ? Who(input, c.Token) : c.Name)).ToArray());

        /// <summary>
        /// The cards over the table (Tone SayTone), as board 7's prototype works them out (a win = the column's highest cell, a tie a win for both):
        /// the item with the most wins ("in the quiver", bolts "in the crossbow", weapons "in hand": "Best or tied best against A, B and C."); a
        /// backup, the item that wins most where that one does not ("as backup": "Beats X on A and B."); then for big game the next item that wins
        /// where neither does ("Best against A."). Equal counts: the order of the rows (most damage over these foes, each counted by your damage to
        /// it, first). From the whole set, not only the rows shown; none with fewer than two items.
        /// </summary>
        static List<Block> UseCards(PanelInput input, (string id, string label, string one, string many) tab, List<(ArrowData g, string name, string status, double score)> rows,
                                    List<(string foe, double dealt, FoeData data)> cols)
        {
            var cards = new List<Block>();
            if (rows.Count < 2) return cards;
            var cell = rows.Select(r => cols.Select(c => Math.Round(Expected(c.data, r.g), MidpointRounding.AwayFromZero)).ToList()).ToList();
            var best = Enumerable.Range(0, cols.Count).Select(k => cell.Max(row => row[k])).ToList();
            var wins = cell.Select(row => Enumerable.Range(0, cols.Count).Select(k => best[k] > 0 && row[k] == best[k]).ToList()).ToList();
            var order = Enumerable.Range(0, rows.Count).OrderByDescending(i => wins[i].Count(x => x)).ThenBy(i => i).ToList();
            // names in words, up to three named, past that two and the rest counted, so a card's sentence fits it: "A, B and 3 more"
            string FoeList(IEnumerable<int> ks) { var n = ks.Select(k => Who(input, cols[k].foe)).ToList(); return n.Count > 3 ? string.Join(", ", n.Take(2).ToArray()) + " and " + (n.Count - 2) + " more" : n.Count > 1 ? string.Join(", ", n.Take(n.Count - 1).ToArray()) + " and " + n[n.Count - 1] : n.FirstOrDefault() ?? ""; }
            Block Card(int i, string title, string text) => new Block { Kind = "card", Tone = SayTone, Title = rows[i].name + " " + title, Text = text, Src = SrcPc };
            var top = order[0];
            if (!wins[top].Any(x => x)) return cards;
            var noun = tab.id == "weapon" ? "in hand" : tab.id == "bolt" ? "in the crossbow" : "in the quiver";
            cards.Add(Card(top, noun, "Best or tied best against " + FoeList(Enumerable.Range(0, cols.Count).Where(k => wins[top][k])) + "."));
            var lost = Enumerable.Range(0, cols.Count).Where(k => !wins[top][k]).ToList();
            int Where(int i, IEnumerable<int> ks) => ks.Count(k => wins[i][k]);
            var others = order.Where(i => i != top).ToList();
            if (others.Count == 0) return cards;
            var backup = others.OrderByDescending(i => Where(i, lost)).ThenBy(i => order.IndexOf(i)).First();
            var beats = lost.Where(k => wins[backup][k]).ToList();
            if (beats.Count > 0) cards.Add(Card(backup, "as backup", "Beats " + rows[top].name + " on " + FoeList(beats) + "."));
            var open = lost.Where(k => !wins[backup][k]).ToList();
            var rest = others.Where(i => i != backup && Where(i, open) > 0).Cast<int?>().FirstOrDefault();
            if (rest.HasValue) cards.Add(Card(rest.Value, "for big game", "Best against " + FoeList(open.Where(k => wins[rest.Value][k])) + "."));
            return cards;
        }
    }
}
