using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Deeds chapter, the approved round-2/4 pages (work/hearthwoven-visual-vocabulary/proto/gallery.html PAGES): Overview
    /// (r4over-deeds-a), Cooking (r4deeds-cooking), Building (r2-deeds-building), Crafting (r4deeds-crafting), Farming
    /// (r4deeds-farming), Fishing (r4deeds-fishing) and Taming (r4over-taming), each on the plate with its hero and columns
    /// where the prototype has them. Woodcutting and Mining stay in PanelModel.Deeds.
    /// Only what the mod has: the character's counters (since you were made), fellow players' shared records, and what
    /// Hearthwoven counted on this PC; materials per piece and a per-kind tame count do not exist, so they are not shown.
    /// New kinds (general, any chapter may use them): itemgrid, ranking, counts, strip, grades, band, people. The food axis is
    /// the shared kind "axis" (contract with ch-company, renderer in Chapters/AxisUi.cs).
    /// </summary>
    public static partial class PanelModel
    {
        public const string MealColour = "#e8a948", FeastColour = "#b8434a";
        public const int CookScale = 16, RankTop = 8, PieceTop = 6;

        /// <summary>The pages this file owns; true when it built the page.</summary>
        static bool DeedsPage(PanelInput input, string page, PanelView view, PanelState state)
        {
            switch (page)
            {
                case "overview": DeedsOverview(input, view, state); return true;
                case "cooking": Cooking(input, state, view); return true;
                case "building": Building(input, state, view); return true;
                case "groundwork": Groundwork(input, view); return true;
                case "crafting": Crafting(input, state, view); return true;
                case "farming": Farming(input, view); return true;
                case "fishing": Fishing(input, state, view); return true;
                case "taming": Taming(input, view); return true;
                default: return false;
            }
        }

        // ---------- the general kinds ----------

        /// <summary>
        /// "What did I make, catch, harvest?" (Joost 2026-10-08, from the live game): every item as a small tile, the game's
        /// own picture, the count big, the name small under it, most first, the FULL list (the page scrolls with its soft
        /// fade when it does not fit). A tile may carry a second, quieter number (Value2, its word in Text: "1,406 planted").
        /// </summary>
        public static Block ItemGrid(IEnumerable<(string key, double value, double value2)> items, Func<string, string> label, Func<string, string> icon, string src, string value2Label = null)
        {
            var list = items.Where(x => x.value > 0 || x.value2 > 0).OrderByDescending(x => x.value).ThenByDescending(x => x.value2).ThenBy(x => x.key, StringComparer.Ordinal).ToList();
            if (list.Count == 0) return null;
            var tag = TagOfSrc(src);
            return new Block
            {
                Kind = "itemgrid", Src = src, Source = tag,
                Items = list.Select(x => new Block
                {
                    Kind = "item", Id = x.key, Icon = icon(x.key), Title = label(x.key), Value = N(x.value), Src = src, Source = tag,
                    Value2 = value2Label != null && x.value2 > 0 ? N(x.value2) : null, Text = value2Label != null && x.value2 > 0 ? value2Label : null,
                }).ToList(),
            };
        }

        /// <summary>The item grid's compact tile (fix2 6): at least this wide, this far apart; as many per line as the column
        /// holds (four across the plate, two in a half column), at most six.</summary>
        public const float ItemTileMinWidth = 168, ItemTileGap = 8;
        public static int ItemTilesPerLine(float column) => Math.Max(1, Math.Min(6, (int)Math.Floor((column + ItemTileGap) / (ItemTileMinWidth + ItemTileGap))));

        /// <summary>The item grid of one count per item.</summary>
        public static Block ItemGrid(IDictionary<string, double> items, Func<string, string> label, Func<string, string> icon, string src) =>
            ItemGrid(items.Select(kv => (kv.Key, kv.Value, 0.0)), label, icon, src);

        /// <summary>
        /// "What most?": one row per thing, picture, name, a bar on one scale, the number. Fraction = value / scale (scale =
        /// the largest value unless given). Colour per row (null: amber). Columns 2 sets the rows side by side. Title and
        /// Value are an optional head ("Pieces built" 866); on a plate the pages put that in a section instead.
        /// </summary>
        public static Block Ranking(IEnumerable<KeyValuePair<string, double>> items, Func<string, string> label, Func<string, string> icon, string src,
                                    string title = null, bool total = false, int top = RankTop, double scale = 0, Func<string, string> colour = null,
                                    bool keepOrder = false, int columns = 1)
        {
            var list = items.Where(kv => kv.Value > 0);
            if (!keepOrder) list = list.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal);
            var rows = list.ToList();
            if (rows.Count == 0) return null;
            var tag = TagOfSrc(src);
            var max = scale > 0 ? scale : rows.Max(kv => kv.Value);
            return new Block
            {
                Kind = "ranking", Title = title, Value = total ? N(rows.Sum(kv => kv.Value)) : null, Src = src, Source = tag, Columns = columns,
                Items = rows.Take(top).Select(kv => new Block
                {
                    Id = kv.Key, Icon = icon?.Invoke(kv.Key) ?? "", Title = label(kv.Key), Value = N(kv.Value), Fraction = (float)Math.Min(1, kv.Value / max),
                    Colour = colour?.Invoke(kv.Key), Src = src, Source = tag,
                }).ToList(),
            };
        }

        /// <summary>One tile per kind: picture, big number, name. When every tile has a Colour (its bottom edge), the proportional
        /// bar of the kinds runs under the tiles (Fraction = share).</summary>
        static Block Counts(IEnumerable<(string id, string title, double value, string icon, string colour)> parts, string src)
        {
            var list = parts.Where(p => p.value > 0).ToList();
            if (list.Count == 0) return null;
            var total = list.Sum(p => p.value); var tag = TagOfSrc(src);
            return new Block
            {
                Kind = "counts", Src = src, Source = tag,
                Items = list.Select(p => new Block { Id = p.id, Title = p.title, Value = N(p.value), Icon = p.icon ?? "", Colour = p.colour, Fraction = (float)(p.value / total), Src = src, Source = tag }).ToList(),
            };
        }

        static Block Strip(IEnumerable<(string id, string title, double value, string icon)> parts, string src)
        {
            var list = parts.Where(p => p.value > 0).ToList();
            if (list.Count == 0) return null;
            var tag = TagOfSrc(src);
            return new Block { Kind = "strip", Src = src, Source = tag, Items = list.Select(p => new Block { Id = p.id, Title = p.title, Value = N(p.value), Icon = p.icon ?? "", Src = src, Source = tag }).ToList() };
        }

        static Block Person(string name, IEnumerable<Block> items) => new Block { Kind = "person", Id = name, Title = name, Icon = "person:" + name, Items = items.ToList() };

        // a plate section with its total on the head line (vocab.css .sect + .tot)
        static Block Section(string title, double total, string src) => new Block { Kind = "section", Title = title, Value = total > 0 ? N(total) : null, Src = total > 0 ? src : null, Source = total > 0 ? TagOfSrc(src) : null };

        // a section and the block under it, or nothing when there is no block
        static void Under(List<Block> into, Block head, Block b) { if (b == null) return; into.Add(head); into.Add(b); }

        static string Label1(double n, string one, string many) => Math.Round(n) == 1 ? one : many;

        // ---------- Overview: one card per deed (r4over-deeds-a), then the names earned in the other chapters ----------

        public const string TamedNote = EarlierIncomplete;   // the game books a tame to the creature's owner (About says why)
        public const string MoreDeeds = "More deeds appear as you play.", UnsungCaption = "Not yet earned: what earns each name.";
        public const int DeedCardCount = 8;   // the deeds with a card (Mender, repairs, has none)
        public const string OwnerFootnote = "Trees felled and tamed: the game books them to the PC that hosts the area.";
        public const string WhoEnjoyedScope = "From each fellow's last shared session.";
        public const string Earned = "Earned", Unsung = "Unsung", EveryNameEarned = "Every name earned.";

        static void DeedsOverview(PanelInput input, PanelView view, PanelState state)
        {
            view.Recorded = true;   // 0.7 (REDESIGN-RULES.md part 5, G4): no zones, no scope line; the box below says what this PC counted
            view.Scope = RecordedScope(input);   // a fellow's book: whose and when, without "since install" (rule E; Deeds() sets the old one before this)
            view.Heading = "Deeds";
            var cards = new List<Block>();
            var earned = new List<Block>(); var carded = new HashSet<string>();   // a deed with a card counts as earned here
            // a card: the deed's title on top, its first number large with a SHORT label, the next number on one small line;
            // a click opens the deed's page. A deed without any number has no card.
            void Card(string page, string id, params (double v, string one, string many, string src)[] numbers)
            {
                var have = numbers.Where(n => n.v > 0).ToList();
                if (have.Count == 0) return;
                var t = SagaTitles.First(x => x.Id == id); carded.Add(id);
                var m = have[0];
                cards.Add(new Block
                {
                    Kind = "card", Id = "Deeds/" + page, Icon = "title:" + id, Title = t.Title, Value = N(m.v), Text = Label1(m.v, m.one, m.many), Src = m.src, Source = TagOfSrc(m.src),
                    Items = have.Skip(1).Take(1).Select(n => new Block { Kind = "number", Value = N(n.v), Title = Label1(n.v, n.one, n.many), Src = n.src, Source = TagOfSrc(n.src) }).ToList(),
                });
            }
            Card("cooking", "cook", (DishesCooked(input), "dish cooked", "dishes cooked", SrcCharacter), (EnjoyedByFellows(input), "enjoyed by fellows", "enjoyed by fellows", SrcFellows));
            Card("building", "builder", (BuiltCount(input), "piece built", "pieces built", SrcCharacter), (Placed(input, "ground").Values.Sum(), "groundwork stroke", "groundwork strokes", SrcCharacter));
            Card("crafting", "smith", (GearKinds.Sum(k => C(input, k.stat)), "gear crafted", "gear crafted", SrcCharacter), (C(input, "Upgrades"), "upgrade made", "upgrades made", SrcCharacter));
            // 0.7 (hard case 13): the card keeps its first number and a second class A, B or D one; the class C line (axe hits, pickaxe hits) goes
            Card("woodcutting", "woodcutter", (TreesFelled(input), "tree felled", "trees felled", SrcCharacter));
            Card("mining", "miner", (BroughtInTotal(input, "mining"), "stone, ore brought in", "stone, ore brought in", SrcCharacter));
            Card("farming", "farmer", (C(input, "HarvestCrop"), "crop picked", "crops picked", SrcCharacter), (PlantedTotals(input).Values.Sum(), "planted", "planted", SrcCharacter));
            Card("fishing", "fisher", (C(input, "FishCaught"), "fish caught", "fish caught", SrcCharacter), (C(input, "FishHooked"), "hooked", "hooked", SrcCharacter));
            Card("taming", "tamer", (C(input, "TamedPetting") + C(input, "TamedCommand"), "petted and commanded", "petted and commanded", SrcCharacter), (C(input, "CreatureTamed"), "tamed", "tamed", SrcCharacter));
            if (cards.Count > 0) earned.Add(new Block { Kind = "cards", Items = cards });
            // "Earlier counts may be incomplete." once on the page (rule A.3, J5): a card's game count from before Hearthwoven (trees felled, stone and ore:
            // the earlier part is above 0, or there is no baseline) or a tame (rule D.2, TamedNote)
            var treesLayers = TreesFelledLayers(input);
            bool gap = (TreesFelled(input) > 0 && (treesLayers == null || treesLayers.Value.faded > 0))
                    || (BroughtInTotal(input, "mining") > 0 && (!HasPickupBaseline(input) || BroughtIn(input, "mining").Values.Sum(v => v.before) > 0))
                    || C(input, "CreatureTamed") > 0;
            if (gap && cards.Count > 0) earned.Add(new Block { Kind = "note", Text = EarlierIncomplete });
            if (cards.Count > 0 && cards.Count < DeedCardCount) earned.Add(new Block { Kind = "note", Text = input.IsSelf ? MoreDeeds : "More deeds appear as " + Name(input) + " plays." });
            if (cards.Count > 0 && (C(input, "Tree") > 0 || C(input, "CreatureTamed") > 0)) earned.Add(new Block { Kind = "note", Text = OwnerFootnote });

            // the other chapters' titles in one line: emblem, name, its chapter quiet after it (no numbers here: they live on
            // the owner pages)
            var other = Titles(input).Where(t => t.Chapter != Chapter.Deeds)
                                     .Select(t => new Block { Id = t.Chapter + "/" + t.Page, Icon = "title:" + t.Id, Title = t.Title, Value = "", Text = ChapterRow.First(c => c.id == t.Chapter).label }).ToList();
            if (other.Count > 0) { earned.Add(Section("Other titles")); earned.Add(new Block { Kind = "strip", Items = other }); }
            if (earned.Count == 0) earned.Add(Empty(NotYetRecorded, (input.IsSelf ? "Your" : Name(input) + "'s") + " deeds show up here as you play."));
            var knownFor = FeatsKnownFor(input);   // "Known for": up to three feats, above the cards (Chapters/FeatsModel.cs)
            if (knownFor != null) earned.Insert(0, knownFor);
            // the names not earned yet (Joost 2026-10-08): the same card, dimmed, with the one line that says what earns it (the
            // title's own description); no number and no progress, the mod has no threshold to measure against
            var have = new HashSet<string>(Titles(input).Select(t => t.Id).Concat(carded));
            var unsung = SagaTitles.Where(t => !have.Contains(t.Id))
                                   .Select(t => new Block { Kind = "card", Tone = "unsung", Id = t.Chapter + "/" + t.Page, Icon = "title:" + t.Id, Title = t.Title, Text = Voice(input, FirstStep(t)) }).ToList();
            Switch(view, state, "overview", "view", state != null && state.View.TryGetValue("Deeds/overview/view", out var shownView) && shownView == "unsung" ? UnsungCaption : null, ("earned", Earned, earned),
                   // the switch always shows (Joost's live test: Unsung was not visible); every name earned: the view says so
                   ("unsung", Unsung, unsung.Count > 0 ? new List<Block> { new Block { Kind = "cards", Tone = "unsung", Items = unsung } }
                                                       : new List<Block> { new Block { Kind = "note", Text = EveryNameEarned } }));
            // the box (rule part 1, Overview): its own book only; the lines are the page's own (SOURCE-MATRIX, About these numbers, Deeds overview)
            AboutNumbers(view, input, StartOf(input, LocalTotals.StatsKind),
                         "The game's own counts. Trees, stone and ore can be lower than what you did.",
                         "Hearthwoven also counted every tree, pickup, planting and dish on this PC.",
                         "Tames and trees felled in an area another player's PC hosted go to that player.");
            Plate(view, "ui:chapter-deeds", RecordedScope(input));
        }

        /// <summary>What earns a name that is not earned yet: the first step, in plain words (Joost, reviewer 2026-10-09: "a category is not a first step").</summary>
        public static string FirstStep(SagaTitle t)
        {
            string step;
            return FirstSteps.TryGetValue(t.Id, out step) ? step : t.Descriptor;
        }
        static readonly Dictionary<string, string> FirstSteps = new Dictionary<string, string>
        {
            ["explorer"] = "Travel on foot, or find a place.", ["woodcutter"] = "Fell your first tree.", ["miner"] = "Break your first stone or ore.", ["farmer"] = "Plant and pick your first crop.",
            ["cook"] = "Cook your first dish.", ["smith"] = "Craft your first piece of gear.", ["hauler"] = "Pull a cart home.", ["sailor"] = "Take the helm of a ship.",
            ["mapmaker"] = "Share your map at the table.", ["builder"] = "Raise your first piece with the hammer.", ["mender"] = "Repair a piece with the hammer.",
            ["wallwarden"] = "Build and arm a defense.", ["defender"] = "Block your first blow.", ["fighter"] = "Fell your first foe.", ["bossbane"] = "Defeat a Forsaken.",
            ["tamer"] = "Pet or tame a creature.", ["fisher"] = "Catch your first fish.", ["trader"] = "Buy something from a trader.",
        };

        /// <summary>Servings of this player's food that fellow players enjoyed: meals and feast servings, the number the Cooking page's
        /// "Who enjoyed" head and Company > Food shared show too (one source, so the overview card never says 10 where the page says 13).</summary>
        public static double EnjoyedByFellows(PanelInput input)
        {
            return FoodRows(input).Sum(r => r.total);   // the numbers themselves (RESILIENCE-06 item 1: the shown "1 000" once closed the panel)
        }

        // ---------- Cooking: dishes cooked (hero); per dish | who enjoyed your food (one-sided axis) ----------

        public const string CraftedBaseline = "itemsCrafted";   // LocalTotals.Baseline kind: the game's m_itemCraftStats when Hearthwoven first ran

        /// <summary>
        /// Dishes and brews per item token in two layers (SOURCES.md: the game books a cooking station's dish to the station's
        /// OWNER, so your page missed what you cooked on a fellow's grill and counted what they cooked on yours): faded = the
        /// game's own craft counter when Hearthwoven first ran for this character, solid = every dish you made since, counted
        /// on your PC (ClientHooks: taken off any cooking station by you, or made in the crafting window). The game counter's
        /// later growth is never added. null = no baseline (a fellow's shared copy, local totals not loaded): the page then
        /// shows the game's counter alone, as before.
        /// </summary>
        public static Dictionary<string, (double faded, double solid)> CookedLayers(PanelInput input)
        {
            if (!input.IsSelf || input.Events == null || input.Baseline == null || !input.Baseline.TryGetValue(CraftedBaseline, out var at) || at == null) return null;
            Dictionary<string, float> already = null;
            input.ExactAtBaseline?.TryGetValue(CraftedBaseline, out already);
            var d = new Dictionary<string, (double faded, double solid)>();
            foreach (var kv in at) if (kv.Value > 0 && ItemKind(input, kv.Key) == "food") d[kv.Key] = (kv.Value, 0);
            foreach (var kv in input.Events.Made)
            {
                var solid = kv.Value - (already != null && already.TryGetValue(kv.Key, out var x) ? x : 0);
                if (solid <= 0 || ItemKind(input, kv.Key) != "food") continue;
                d.TryGetValue(kv.Key, out var l); d[kv.Key] = (l.faded, l.solid + solid);
            }
            return d;
        }

        /// <summary>Dishes cooked: both layers added up, or the game's counters (CraftFood + CraftGrill) without a baseline.</summary>
        public static double DishesCooked(PanelInput input)
        {
            if (input.Window != null) return Crafted(input, "food").Values.Sum();   // a day window: the dishes you made those days (DeedsWindow)
            var layers = CookedLayers(input);
            return layers == null ? C(input, "CraftFood") + C(input, "CraftGrill") : layers.Values.Sum(l => l.faded + l.solid);
        }

        public const string DishesByKind = "Dishes by kind", WhereCooked = "Where it was cooked", TableRow = "Cauldron and prep table", GrillRow = "Cooking stations and oven";
        public const string TableLine = "made in the crafting menu", GrillLine = "booked to whoever takes it off";
        public const string StationsNote = "Cooking station, iron cooking station and oven are not counted apart.";

        // a dish a cooking station hands out (the Grill row), by the game's own data; unknown = made in the crafting menu
        static bool AtStation(PanelInput input, string token) { try { return input.StationDish?.Invoke(token) ?? false; } catch { return false; } }

        /// <summary>
        /// The kitchen ledger (cook-B, Joost 2026-10-09): one row per kind of cooking the game counts apart, the cauldron and prep
        /// table (CraftFood) and the grill (CraftGrill, booked to whoever takes the dish off), each with its dishes as chips
        /// (picture, count, name) and its total on the right. The kind of kitchen comes from the game's data per dish
        /// (<see cref="PanelInput.StationDish"/>: what a CookingStation hands out), so the rows add up to the dish bar. The shared
        /// "ledger" kind; a row carries Text (its source line), Value (its total) and Note (the word after it).
        /// </summary>
        public static Block KitchenLedger(PanelInput input, IDictionary<string, double> dishes, string src)
        {
            var tag = TagOfSrc(src); var rows = new List<Block>();
            foreach (var (id, title, line, station) in new[] { ("table", TableRow, TableLine, false), ("grill", GrillRow, GrillLine, true) })
            {
                var mine = dishes.Where(kv => kv.Value > 0 && AtStation(input, kv.Key) == station).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
                if (mine.Count == 0) continue;
                var sum = mine.Sum(kv => kv.Value);
                rows.Add(new Block
                {
                    Kind = "row", Id = id, Title = title, Text = line, Value = N(sum), Note = Label1(sum, "dish", "dishes"), Src = src, Source = tag,
                    Items = mine.Select(kv => new Block { Kind = "chip", Id = kv.Key, Icon = "item:" + kv.Key, Title = Who(input, kv.Key), Value = N(kv.Value), Src = src, Source = tag }).ToList(),
                });
            }
            if (rows.Count == 0) return null;
            return new Block { Kind = "ledger", Src = src, Source = tag, Items = rows, Note = rows.Any(r => r.Id == "grill") ? StationsNote : null };
        }

        static void Cooking(PanelInput input, PanelState state, PanelView view)
        {
            view.Recorded = true;   // 0.7 (REDESIGN-RULES.md part 3): one recorded total, no zones; the day windows come through here too (W.1)
            view.Heading = "Cooking";
            view.Scope = RecordedScope(input);   // a fellow's copy: whose and when, with no install wording (rule E); replaces the page scope FellowScope
            var layers = CookedLayers(input);
            var made = DishesCooked(input);
            var windowed = input.Window != null;   // a day window: the dishes you made those days, counted here (no EI inside a window, rule W.1)
            var beforeAll = layers?.Values.Sum(l => l.faded) ?? 0;
            var setOut = Placed(input, "feast").Values.Sum();   // rule B: the feasts set out are the game's own counter, always beside the hero
            // rule A (dishes cooked): the total is the character's whole count, SrcCharacter also when nothing is before; "Earlier counts may be
            // incomplete." in the hero's own slot when the game's earlier part is above 0 or there is no baseline (a fellow's copy)
            var earlier = !windowed && made > 0 && (layers == null || beforeAll > 0) ? EarlierIncomplete : null;
            var hero = Hero((made > 0 ? N(made) : null, Label1(made, "dish cooked", "dishes cooked"), windowed ? SrcPc : SrcCharacter, earlier),
                            (setOut > 0 ? N(setOut) : null, Label1(setOut, "feast set out", "feasts set out"), SrcCharacter, null));
            // 0.8 layout D+: one hero form on every page (Woodcutting's); the compact and second-size Cooking heroes are gone
            Add(view, hero);
            SkillBeside(view, input, "Cooking");   // rule K: the game's cooking skill in the hero's row
            var src = windowed ? SrcPc : SrcCharacter;
            // dishes by kind (rule A.2): each kind's sum, one bar, the food's own colours from its icons; no faded shares, no key: the hero says it
            var allDishes = layers == null ? Crafted(input, "food") : layers.ToDictionary(kv => kv.Key, kv => kv.Value.faded + kv.Value.solid);
            // the dishes as a filter bar (Type, Main boost: CookingFacets.cs), then the bar and the ledger of the dishes that pass it (0.6.5)
            var filter = DishFilter(input, state, allDishes, src);
            var on = filter != null && FilterOn(state, CookFilter);
            var dishes = filter == null ? allDishes : filter.Shown.ToDictionary(i => i.Key, i => i.Weight);
            if (filter != null) view.Blocks.Add(filter.Bar);
            var bar = Composition(DishesByKind, dishes, k => Who(input, k), k => (null, null), src, tint: ItemTint(input));
            if (bar != null) bar.Tone = Thin;
            Add(view, bar);
            if (on && bar == null) view.Blocks.Add(new Block { Kind = "empty", Title = "Nothing for this choice" });
            // without the two layers the headline is the game's own counters and the bar is its per-dish craft counts: the game books
            // them differently (a mod's food it cannot name, brews among the dishes), so they can differ. Say so under the bar
            // instead of hiding it (polish-06's honest headline, on cooking-06's bar). Against every dish, whatever the filter shows
            if (hero != null && bar != null && layers == null)
            {
                var kinds = Math.Round(allDishes.Values.Sum());
                if (made > 0 && kinds != Math.Round(made)) view.Blocks.Add(new Block { Kind = "note", Text = "the dishes add up to " + N(kinds) + "; the headline is " + (input.IsSelf ? "your" : Name(input) + "'s") + " character's own count" });
            }
            // the kitchen ledger (cook-B): where it was cooked, dishes as chips, and the one line on what is not counted apart
            var ledger = KitchenLedger(input, dishes, src);
            Under(view.Blocks, Section(WhereCooked), ledger);
            if (ledger == null && !input.IsSelf && made > 0) view.Blocks.Add(new Block { Kind = "note", Text = Name(input) + "'s shared copy does not say where it was cooked." });
            // who enjoyed it (cook-B): one bar per fellow in their player colour, the count at the end; alphabetical, never a ranking
            var axis = input.Window != null ? null : FoodAxis(input);   // a day window: fellows' records have no days (DeedsDayLines says so)
            var whose = input.IsSelf ? "your" : Name(input) + "'s";
            if (axis != null)
            {
                var rows = FoodRows(input);
                var fans = rows.Select(r => new KeyValuePair<string, double>(r.fellow.PlayerName, r.total)).ToList();   // the numbers, not the shown text
                // rule E: one total only when every row has the same period words; a list whose rows differ says its own words on each row
                var sinceAll = rows.All(r => r.fellow.SharedSinceInstall); var uniform = sinceAll || rows.All(r => !r.fellow.SharedSinceInstall);
                var whoHead = Section("Who enjoyed " + whose + " food", uniform ? fans.Sum(f => f.Value) : 0, SrcFellows);
                if (input.IsSelf && uniform && rows.Count > 0)   // whose record: their last session, or each player's PC (one caption for the list)
                    whoHead.Text = FellowCaption(rows.Select(r => (Name(r.fellow), r.fellow.SharedSinceInstall)));
                var who = Ranking(fans, k => k, k => "person:" + k, SrcFellows, top: int.MaxValue, keepOrder: true);
                if (who != null && input.IsSelf && !uniform)
                    foreach (var row in who.Items) { var f = rows.First(r => r.fellow.PlayerName == row.Id).fellow; row.RecordedFrom = FellowWords(Name(f), f.SharedSinceInstall); }
                Under(view.Blocks, whoHead, who);
                // the servings are counted on the fellows' PCs, by dish: they stay whole under a filter, and say so (as Crafting's upgrades)
                if (on) view.Blocks.Add(new Block { Kind = "note", Text = EnjoyedWhole(whose), Src = SrcFellows, Source = TagFellows });
            }
            else if (made > 0 && input.Window == null) view.Blocks.Add(new Block { Kind = "note", Text = "When a fellow player enjoys " + whose + " food, they show up here." });
            // About these numbers (own book, all time, with a baseline): the game's own count before the split, what Hearthwoven counted after it
            if (layers != null && input.Window == null)
            {
                var exactSince = layers.Values.Sum(l => l.solid);
                AboutNumbers(view, input, StartOf(input, LocalTotals.CraftedKind),
                    "The game's own count: " + N(layers.Values.Sum(l => l.faded)) + " dishes. A dish from a cooking station went to the station's owner, so yours on a fellow's grill may be missing.",
                    "Hearthwoven counted every dish you made or took off a station, on this PC: " + N(exactSince) + " so far.",
                    "Who enjoyed your food is counted on each fellow player's PC.");
            }
            Plate(view, "title:cook", RecordedScope(input));
        }

        /// <summary>
        /// How many of a dish this player made in all (both Cooking layers), keyed as eaters record it (the item prefab,
        /// "CookedMeat"; a token works too); null = not known (no baseline, a fellow's copy, an item the game data does not name).
        /// Both layers count crafts and eaters count servings, so each craft counts what its recipe makes (YieldOf: 4 sausages).
        /// </summary>
        public static Func<string, double?> MadeOf(PanelInput input)
        {
            if (!input.IsSelf || input.Events == null || input.Baseline == null || !input.Baseline.TryGetValue(CraftedBaseline, out var at) || at == null) return null;
            Dictionary<string, float> already = null;
            input.ExactAtBaseline?.TryGetValue(CraftedBaseline, out already);
            return dish =>
            {
                var token = dish != null && dish.StartsWith("$") ? dish : Ask(input.ItemToken, dish);
                if (string.IsNullOrEmpty(token)) return null;
                double v(IDictionary<string, float> d) => d != null && d.TryGetValue(token, out var x) ? x : 0;
                return (v(at) + Math.Max(0, v(input.Events.Made) - v(already))) * YieldOf(input, token);
            };
        }

        /// <summary>What one craft of an item makes (RecipeYield, this PC's game data); one when not known or no recipe (a grill hands out one at a time).</summary>
        static double YieldOf(PanelInput input, string token)
        {
            try { var n = input.RecipeYield?.Invoke(token) ?? 0; return n > 1 ? n : 1; } catch { return 1; }
        }

        /// <summary>
        /// Servings of one maker's food, per eater and dish, never more of a dish than the maker made of it (<paramref name="made"/>;
        /// null or a null answer = not known, nothing capped). The eaters' PCs record the maker the game wrote on the food,
        /// and a cooking station writes its OWNER there whoever cooked it: a grill owner who never cooked a dish is credited
        /// for it. Over the cap the servings are shared out in proportion (largest remainders first, then by name).
        /// </summary>
        public static List<(string who, Dictionary<string, double> dishes)> CapToMade(IList<(string who, Dictionary<string, double> dishes)> eaten, Func<string, double?> made)
        {
            var result = eaten.Select(e => (e.who, dishes: new Dictionary<string, double>(e.dishes))).ToList();
            if (made == null) return result;
            foreach (var dish in result.SelectMany(e => e.dishes.Keys).Distinct().ToList())
            {
                var cap = made(dish);
                double total = result.Sum(e => e.dishes.TryGetValue(dish, out var n) ? n : 0);
                if (cap == null || total <= cap.Value) continue;
                var limit = Math.Max(0, Math.Floor(cap.Value));
                var shares = result.Select((e, k) => (k, e.who, exact: (e.dishes.TryGetValue(dish, out var n) ? n : 0) * limit / total)).ToList();
                var given = shares.ToDictionary(s => s.k, s => Math.Floor(s.exact));
                var left = limit - given.Values.Sum();
                foreach (var s in shares.OrderByDescending(s => s.exact - Math.Floor(s.exact)).ThenBy(s => s.who, StringComparer.OrdinalIgnoreCase))
                { if (left <= 0) break; if (s.exact - Math.Floor(s.exact) <= 0) continue; given[s.k]++; left--; }
                for (int k = 0; k < result.Count; k++)
                {
                    if (!result[k].dishes.ContainsKey(dish)) continue;
                    if (given[k] > 0) result[k].dishes[dish] = given[k]; else result[k].dishes.Remove(dish);
                }
            }
            return result;
        }

        /// <summary>The meals each fellow who shares enjoyed of this player's food (their record, by maker name), capped by
        /// what this player made of each dish where that is known (CapToMade, MakerMade); alphabetical.</summary>
        public static List<(string who, Dictionary<string, double> dishes)> MealsEnjoyed(PanelInput input)
        {
            var eaten = new List<(string who, Dictionary<string, double> dishes)>();
            foreach (var f in FellowsOf(input).OrderBy(f => f.PlayerName, StringComparer.OrdinalIgnoreCase))
            {
                var meals = new Dictionary<string, double>();
                foreach (var kv in f.Events.AteFoodMadeBy) { var p = Split2(kv.Key); if (SameName(p[0], input.PlayerName)) Bump(meals, p[1], kv.Value); }
                eaten.Add((f.PlayerName, meals));
            }
            return CapToMade(eaten, MakerMade(input));   // as Fireside caps it (a fellow's book too, when their copy runs since install)
        }

        /// <summary>
        /// Who enjoyed this player's food (recorded on the eaters' PCs): the shared "axis" kind, one-sided, this player on the
        /// axis and one row per fellow (alphabetical, never ranked), meals and feast servings in one bar on one scale (at
        /// least 16, as Company > Food shared), the dishes as chips. Meals never more of a dish than this player made of it
        /// where that is known (MealsEnjoyed). null when nobody who shares has enjoyed any.
        /// </summary>
        public static Block FoodAxis(PanelInput input)
        {
            var rows = new List<Block>(); var totals = new List<double>();
            foreach (var (f, meals, feasts, t) in FoodRows(input))
            {
                var fs = feasts.Values.Sum();
                Block Chip(KeyValuePair<string, double> kv) => new Block { Kind = "chip", Icon = "item:" + kv.Key, Title = Who(input, kv.Key), Value = "× " + N(kv.Value), Src = SrcFellows, Source = TagFellows };
                rows.Add(new Block
                {
                    Kind = "row", Id = f.PlayerName, Title = f.PlayerName, Icon = "person:" + f.PlayerName, Value = N(t), Fraction2 = (float)(fs / t), Src = SrcFellows, Source = TagFellows,
                    Items = meals.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(Chip)
                                 .Concat(feasts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(Chip)).ToList(),
                });
                totals.Add(t);
            }
            if (rows.Count == 0) return null;
            var scale = Math.Max(CookScale, totals.Max());
            for (int k = 0; k < rows.Count; k++) rows[k].Fraction = (float)(totals[k] / scale);
            var me = input.IsSelf ? "You" : Name(input);
            return new Block { Kind = "axis", Title = me, Icon = "person:" + (string.IsNullOrEmpty(input.PlayerName) ? me : input.PlayerName), Value = N(scale), Src = SrcFellows, Source = TagFellows, Items = rows };
        }

        /// <summary>The food axis's numbers: per fellow who enjoyed this player's food (alphabetical), the meals and feast servings
        /// per dish and their total; fellows with none are left out. FoodAxis draws them, the overview card and the Cooking page's
        /// "Who enjoyed" head sum them. Feast servings follow Fireside's rule (FeastParts): this player's when they made the feast (0.8: the
        /// one who set it out says so) or set it out with its maker unknown.</summary>
        static List<(PanelInput fellow, Dictionary<string, double> meals, Dictionary<string, double> feasts, double total)> FoodRows(PanelInput input)
        {
            var rows = new List<(PanelInput, Dictionary<string, double>, Dictionary<string, double>, double)>();
            var enjoyed = MealsEnjoyed(input);
            var people = FiresidePeople(input); var owner = people[0].Name;
            var all = FeastParts(input, people);
            int at = 0;
            foreach (var f in FellowsOf(input).OrderBy(f => f.PlayerName, StringComparer.OrdinalIgnoreCase))
            {
                var meals = enjoyed[at++].dishes; var feasts = new Dictionary<string, double>();
                foreach (var x in all) if (x.Eater == f && SameName(x.Maker, owner)) Bump(feasts, x.Feast, x.N);
                var t = meals.Values.Sum() + feasts.Values.Sum();
                if (t > 0) rows.Add((f, meals, feasts, t));
            }
            return rows;
        }

        // ---------- Building: pieces built (with their pictures), groundwork with a pattern per kind, repairs ----------

        // the groundwork patterns: ready fills (src/Panel/vocab/grain-ground-*.png), one per kind as in r2-deeds-building
        static readonly Dictionary<string, (string colour, string pattern)> GroundPatterns = new Dictionary<string, (string, string)>
        {
            // the colour is the fill's base as the sprite is authored (earthy and readable on the dark plate: soil brown, raised-earth
            // ochre, paving grey; Joost, live test: the first fills vanished), the pattern sits on it in the sprite
            ["$piece_levelground"] = ("#705032", "vocab:grain-ground-level"), ["$piece_lowerground"] = ("#925c3a", "vocab:grain-ground-lower"),
            ["$piece_raise"] = ("#b07e2e", "vocab:grain-ground-raise"), ["$piece_path"] = ("#9e8056", "vocab:grain-ground-path"),
            ["$piece_pavedroad"] = ("#7e7c76", "vocab:grain-ground-paved"), ["$piece_cultivate"] = ("#586834", "vocab:grain-ground-cultivate"),
            ["$piece_replant"] = ("#64863e", "vocab:grain-ground-replant"),
        };
        // a tool's variants (a mod's precision, square or path versions) share their base's pattern, a little lighter, so the
        // legend reads by meaning (Joost 2026-10-08): raise ^^^, lower vvv (raise mirrored), level lines, paved, path, cultivate
        static readonly (string part, string kind)[] GroundFamilies =
        {
            ("lower", "$piece_lowerground"), ("raise", "$piece_raise"), ("level", "$piece_levelground"), ("paved", "$piece_pavedroad"),
            ("path", "$piece_path"), ("cultivat", "$piece_cultivate"), ("replant", "$piece_replant"),
        };

        public static (string colour, string pattern) GroundOf(string token)
        {
            if (token != null && GroundPatterns.TryGetValue(token, out var l)) return l;
            var t = (token ?? "").ToLowerInvariant();
            foreach (var f in GroundFamilies)
                if (t.Contains(f.part)) { var b = GroundPatterns[f.kind]; return (Lighter(b.colour, 0.25), b.pattern); }
            return (null, null);
        }

        static string Lighter(string hex, double by)
        {
            int c(int i) => Convert.ToInt32(hex.Substring(i, 2), 16);
            string m(int v) => ((int)Math.Round(v + (255 - v) * by)).ToString("x2");
            return "#" + m(c(1)) + m(c(3)) + m(c(5));
        }

        static void Building(PanelInput input, PanelState state, PanelView view)
        {
            view.Recorded = true;   // 0.7 rule B (game complete): the game's placed-pieces counter is the number; no twins, no zones
            view.Heading = "Building";
            view.Scope = RecordedScope(input);   // a fellow's copy: whose and when, with no install wording (rule E); replaces the page scope FellowScope
            Func<string, string> named = k => Who(input, k);
            var built = Placed(input, "built");
            // the game's counts before Hearthwoven's first run and the placements since (the box's numbers only; the page shows the game's counter)
            var layers = CounterLayers(input, PlacedBaseline, input.PiecesPlaced, k => PieceKind(input, k) == "built");
            FacetResult filter = null;
            // every piece as a tile with the game's own picture, most first, the full list (Joost 2026-10-08: a grid like the other lists)
            if (built.Count > 0)
            {
                // the pieces as a filter bar (Category, Main material: FacetModel.cs, BuildingFacets.cs) when the game data names them, then the pieces that pass it
                filter = PieceFilter(input, state, built);
                var grid = filter != null ? ItemGrid(filter.Shown.Select(i => (i.Key, i.Weight, 0.0)), named, k => "piece:" + k, SrcCharacter) : ItemGrid(built, named, k => "piece:" + k, SrcCharacter);
                // the number is the page (reviewer: "866 pieces built is only a small heading"): the hero; the placement note lives in the box (own book)
                Add(view, Hero((N(built.Values.Sum()), Label1(built.Values.Sum(), "piece built", "pieces built"), SrcCharacter, input.IsSelf ? null : "Placements: a piece moved counts again.")));
                // the sort control over the list (SortModel.cs): Most, A-Z, By category (the hammer's tab, the filter's own row); it sorts what the filter leaves
                var sortKey = SortKey(state, "building");
                if (filter == null) { if (grid != null) { view.Blocks.Add(Section("Every piece")); view.Blocks.AddRange(Sorted(state, sortKey, grid, built.Count)); } }
                else { view.Blocks.Add(filter.Bar); view.Blocks.AddRange(Sorted(state, sortKey, grid, built.Count, GroupsOfFacet(filter, "tab"))); if (grid == null) view.Blocks.Add(new Block { Kind = "empty", Title = "Nothing for this choice" }); }
            }
            else { var b = BuiltCount(input); if (b > 0) Add(view, Hero((N(b), Label1(b, "piece built", "pieces built"), SrcCharacter, null))); }
            // rule C (repairs, Hearthwoven only): its own section with its own total, "Recorded from ... · this PC" by the automatic placement; a
            // section with nothing in it is left out. With a filter chosen (or in a day window) the repairs follow it, and say so
            var on = filter != null && FilterOn(state, BuildFilter) && (layers != null || input.Window != null);
            var fixes = D(input.Events?.Repairs).Where(kv => !on || filter.Pass(PieceFacetItem(input, kv.Key, 0))).ToList();
            if (fixes.Sum(kv => kv.Value) > 0)
                Under(view.Blocks, Section("Pieces repaired", fixes.Sum(kv => kv.Value), SrcPc), Ranking(fixes, named, k => "piece:" + k, SrcPc, top: PieceTop, columns: 2));
            view.Blocks.AddRange(RecoveredBlocks(input));
            // base defences (B28: moved here from Battle > Defence, they are built pieces): the character's counts, All only (a high-water mark)
            if (input.Window == null) { var defences = BaseDefences(input); if (defences != null) { view.Blocks.Add(Section(BaseDefencesTitle)); view.Blocks.Add(defences); } }
            // About these numbers (own book, all time, with a baseline): the game's count before the split, what it counted after (rule B.4), repairs (rule C)
            if (layers != null && input.Window == null)
            {
                double before = layers.Values.Sum(l => l.before), since = layers.Values.Sum(l => l.since);
                AboutNumbers(view, input, StartOf(input, LocalTotals.PlacedKind),
                    "The game's own count of placements: " + N(before) + ".",
                    since > 0 ? "Still the game's own count: " + N(since) + " more." : "Still the game's own count: nothing more.",
                    "Every placement counts, also a piece you moved or tore down and placed again. Repairs are counted by Hearthwoven " + FromShort(input, StartOf(input, null)) + ". " + RecoveredAbout);
            }
            Plate(view, "title:builder", RecordedScope(input));
        }

        // Materials recovered (0.8, v08-salvage): the section's title, and what Building's About box says of it (the limits included)
        public const string RecoveredTitle = "Materials recovered";
        public const string RecoveredAbout = "Materials recovered are what you picked up from a piece that came down, taken down with the hammer or broken: " +
                                             "they do not count as brought in on Woodcutting or Mining. Ruins, and pieces that came down while you were away, still count as brought in.";

        /// <summary>
        /// Materials recovered (0.8, v08-salvage): what you picked up from a piece that came down (taken down with the hammer, broken), counted
        /// apart from what you brought in from the world (SessionEvents.Recovered, SalvageWatch), so a wall taken down never counts its stone
        /// twice on Mining. Its own section with its total, recorded from the first load of the version that counts it (StartRecovered); one
        /// kind as one tile with its picture, more as the bar form (BAR-FORM.md: the items' own colours, wood in its grain). Nothing when nothing was recovered.
        /// </summary>
        static List<Block> RecoveredBlocks(PanelInput input)
        {
            var parts = D(input.Events?.Recovered).Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);   // item token -> amount
            var blocks = new List<Block>();
            if (parts.Count == 0) return blocks;
            var from = StartOf(input, LocalTotals.StartRecovered);
            Func<string, string> named = k => Who(input, k);
            Block body;
            if (parts.Count == 1) body = ItemGrid(parts, named, k => "item:" + k, SrcPc);
            else
            {
                var wood = WoodLookOf(input);   // a wood part wears its grain as on Woodcutting; stone and the rest their own colour as on Mining
                body = Composition(null, parts, named, k => GatherKind(input, k) == "wood" ? wood(k) : (null, null), SrcPc, tint: ItemTint(input));
                body.Value = null;   // the section carries the total
            }
            var head = Section(RecoveredTitle, parts.Values.Sum(), SrcPc);
            DateFrom(head, from); DateFrom(body, from);
            blocks.Add(head); blocks.Add(body);
            return blocks;
        }


        // Groundwork has its own page right after Building (Joost, play-test: the long piece list pushed it out of sight): raising,
        // lowering and levelling the ground, paths, paved roads, cultivating; the game's counter per kind, one bar with a pattern each
        // (materials per piece are not recorded, so no material bar)
        static void Groundwork(PanelInput input, PanelView view)
        {
            view.Recorded = true;   // 0.7 rule B (game complete): the game's counter is the number
            view.Heading = "Groundwork";
            view.Scope = RecordedScope(input);   // a fellow's copy: whose and when, with no install wording (rule E); replaces the page scope FellowScope
            var bar = Composition("Groundwork", Placed(input, "ground"), k => Who(input, k), GroundOf, SrcCharacter, icon: k => "piece:" + k);
            // the game's counter before Hearthwoven's first run and the strokes since: the box's numbers only
            var layers = CounterLayers(input, PlacedBaseline, input.PiecesPlaced, k => PieceKind(input, k) == "ground");
            if (bar != null)
            {
                Add(view, Hero((bar.Value, "groundwork strokes", SrcCharacter, null)));   // the total is the hero's number, so the bar's head stays empty
                bar.Title = null; bar.Value = null;
                view.Blocks.Add(bar);
            }
            if (layers != null && input.Window == null)
            {
                double before = layers.Values.Sum(l => l.before), since = layers.Values.Sum(l => l.since);
                AboutNumbers(view, input, StartOf(input, LocalTotals.PlacedKind),
                    "The game's own count of strokes: " + N(before) + ".",
                    since > 0 ? "Still the game's own count: " + N(since) + " more." : "Still the game's own count: nothing more.",
                    "A stroke is one use of the hoe, cultivator or pickaxe on the ground, not an area.");
            }
            Plate(view, "vocab:ground-lower", RecordedScope(input));
        }

        // ---------- Crafting: gear crafted and upgrades (hero), the gear filter (Kind, Main material) with its bars and item grid, fellows who put your gear to good use ----------

        // the game's craft counters per kind (InventoryGui.DoCrafting) with the item types they count: the filter's Kind row (CraftingFacets.cs).
        // A shield is a weapon to the game (CraftWeapon), a belt or other utility item a tool (CraftTool); armour is helmet, chest, legs and cape.
        // id = the Kind chip's id, saved in a player's filter choices: "armour" keeps its 0.7 spelling while the label reads "Armor".
        static readonly (string stat, string title, string[] types, string fallback, string id)[] GearKinds =
        {
            ("CraftWeapon", "Weapons", new[] { "OneHandedWeapon", "TwoHandedWeapon", "TwoHandedWeaponLeft", "Bow", "Shield", "Hands" }, "vocab:weapon-melee", "weapons"),
            ("CraftArmor", "Armor", new[] { "Chest", "Helmet", "Legs", "Shoulder" }, null, "armour"),
            ("CraftTool", "Tools", new[] { "Tool", "Utility" }, null, "tools"),
            ("CraftTrinket", "Trinkets", new[] { "Trinket" }, null, "trinkets"),
        };

        /// <summary>The tile's picture: the item of that kind this player crafted most (the game's own sprite), else a stand-in.</summary>
        static string GearPicture(PanelInput input, string[] types, string fallback)
        {
            var best = Crafted(input, "gear").Where(kv => types.Contains(Ask(input.ItemType, kv.Key) ?? "")).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                                             .Select(kv => kv.Key).FirstOrDefault();
            return best != null ? "item:" + best : fallback ?? "";
        }

        static void Crafting(PanelInput input, PanelState state, PanelView view)
        {
            view.Recorded = true;   // 0.7 rule B (game complete): the game's craft counters are the numbers; no twins, no zones
            view.Heading = "Crafting";
            view.Scope = RecordedScope(input);   // a fellow's copy: whose and when, with no install wording (rule E); replaces the page scope FellowScope
            // a short window (DeedsShort): the game's craft counters have no minutes; the gear you made those minutes is the same booking (DeedsWindow)
            var shortW = input.Window != null && input.Window.Short;
            double gear = shortW ? Crafted(input, "gear").Values.Sum() : GearKinds.Sum(k => C(input, k.stat)), up = C(input, "Upgrades");
            Add(view, Hero((gear > 0 ? N(gear) : null, "gear crafted", shortW ? SrcPc : SrcCharacter, null), (up > 0 ? N(up) : null, Label1(up, "upgrade made", "upgrades made"), SrcCharacter, null)));
            SkillBeside(view, input, "Crafting");   // rule K: the game's crafting skill in the hero's row
            // the gear as a filter bar (Kind, Main material: FacetModel.cs, CraftingFacets.cs), then the gear that passes it as the item grid
            var items = CounterLayers(input, CraftedBaseline, input.ItemsCrafted, k => ItemKind(input, k) == "gear");   // the box's numbers only
            var gearMade = Crafted(input, "gear");
            var filter = GearFilter(input, state, gearMade);
            if (filter != null)
            {
                view.Blocks.Add(filter.Bar);
                var grid = ItemGrid(filter.Shown.Select(i => (i.Key, i.Weight, 0.0)), k => Who(input, k), k => "item:" + k, input.Window != null ? SrcPc : SrcCharacter);   // a day window: the gear you made those days, counted here
                // the sort control (SortModel.cs): Most, A-Z, By kind (the filter's own row), over what the filter leaves
                view.Blocks.AddRange(Sorted(state, SortKey(state, "crafting"), grid, gearMade.Count, GroupsOfFacet(filter, "kind")));
                if (grid == null) view.Blocks.Add(new Block { Kind = "empty", Title = "Nothing for this choice" });
            }
            if (input.Window == null) Under(view.Blocks, Section("Put to good use by"), GearPutToGoodUse(input));   // a day window: fellows' records have no days (DeedsDayLines)
            // About these numbers (own book, all time, with the game's stats baseline): the game's own count before the split, what it counted after;
            // when the per-item baseline has another date, each date says its own sentence (hard case 4)
            if (input.Window == null && HasBaseline(input, LocalTotals.StatsKind))
            {
                var statsFrom = StartOf(input, LocalTotals.StatsKind); var itemsFrom = StartOf(input, LocalTotals.CraftedKind);
                double gearBefore = GearKinds.Sum(k => StatLayers(input, k.stat)?.before ?? 0), gearSince = GearKinds.Sum(k => StatLayers(input, k.stat)?.since ?? 0);
                double upBefore = StatLayers(input, "Upgrades")?.before ?? 0, upSince = StatLayers(input, "Upgrades")?.since ?? 0;
                string stillGame = "Still the game's own count: " + N(gearSince) + " more gear and " + N(upSince) + " more upgrades.";
                string from = stillGame;
                if (items != null && itemsFrom.HasValue && statsFrom.HasValue && RecordDate(input, itemsFrom.Value) != RecordDate(input, statsFrom.Value))
                    from = "From " + RecordDate(input, statsFrom.Value) + ": " + stillGame + " From " + RecordDate(input, itemsFrom.Value) + ": still the game's own count of each item: " + N(items.Values.Sum(v => v.since)) + " more.";
                AboutNumbers(view, input, statsFrom,
                    "The game's own count: " + N(gearBefore) + " pieces of gear, " + N(upBefore) + " upgrades.",
                    from,
                    "A batch (a stack of arrows) counts once, as the game books it.");
            }
            Plate(view, "title:smith", RecordedScope(input));
        }

        /// <summary>Gear this player made that fellow players equipped (recorded on their PCs): one line per fellow,
        /// alphabetical, the items by name and picture (no counts: equipping is not a deed to tally).</summary>
        static Block GearPutToGoodUse(PanelInput input)
        {
            var people = new List<Block>();
            foreach (var f in FellowsOf(input).OrderBy(f => f.PlayerName, StringComparer.OrdinalIgnoreCase))
            {
                var items = f.Events.EquippedGearMadeBy.Select(kv => Split2(kv.Key)).Where(p => SameName(p[0], input.PlayerName)).Select(p => p[1]).Distinct()
                             .OrderBy(k => Who(input, k), StringComparer.OrdinalIgnoreCase).ToList();
                if (items.Count == 0) continue;
                var person = Person(f.PlayerName, items.Select(k => new Block { Kind = "item", Icon = "item:" + k, Title = Who(input, k), Src = SrcFellows, Source = TagFellows }));
                person.RecordedFrom = f.IsSelf ? null : FellowWords(Name(f), f.SharedSinceInstall);   // rule E: each row says its own period words
                people.Add(person);
            }
            return people.Count == 0 ? null : new Block { Kind = "people", Items = people, Src = SrcFellows, Source = TagFellows };
        }

        // ---------- Farming: planted and harvested (hero), per crop (two bars touching, numbers left), also harvested ----------

        static readonly (string stat, string title, string icon)[] AlsoHarvested =
        {
            ("HarvestBerry", "Berries", "item:Raspberry"), ("HarvestMushroom", "Mushrooms", "item:Mushroom"), ("HarvestVine", "Vineberries", "item:Vineberry"),
            ("BeesHarvested", "Honey", "item:Honey"), ("SapHarvested", "Sap", "item:Sap"),
        };

        /// <summary>A planted piece's harvest (the grown plant's pickable item, PanelInput.CropOf); null = not a crop (a tree).</summary>
        static string CropOf(PanelInput i, string piece) => Ask(i.CropOf, piece);

        public const string PickedNote = "Picked includes wild plants and fellows' fields.";
        /// <summary>fix4: the crop list's last tile (what was picked of plants you never planted) and the heading of the harvests the game counts apart from "picked".</summary>
        public const string SlimTone = "slim";   // a crop entry with picked alone: one slim row under the tiles
        public const string OtherPlants = "Other plants", OtherPlantsId = "OtherPlants", OtherPlantsIcon = "title:farmer", AlsoHarvestedTitle = "Also harvested, counted apart";
        /// <summary>Farming's crop grid (diff-05, Joost 2026-10-08): the legend words and colours of the paired bars in every
        /// crop tile (planted tan, picked green, as r4deeds-farming), and what picked includes, said once in the legend.</summary>
        public const string PlantedWord = "planted", PickedWord = "picked", PickedScope = "includes wild plants and fellows' fields",
                            PlantedColour = "#b8a07a", PickedColour = "#9fbf5c";
        /// <summary>The legend chip of a bar or number with a faded part (diff-05: a small chip at the end of the legend, not a
        /// sentence by the heading); About > What it reads says the rest. PanelUi draws a block's Note equal to it as the chip.</summary>
        public const string FadedKey = "faded = before install";
        /// <summary>The key of a grid whose tiles carry both layers ("440 + 80"): both words once, as a chip above the grid (Joost 2026-10-09: the legend sat below what it explains).</summary>
        public const string FadedKeyTwin = "faded = before install \u00b7 solid = since install";
        public const string FollowsFilter = "Follows the filter above.";
        /// <summary>The words a layered number says at its parts (zones-wording): "114 before install + 52 since install".</summary>
        public const string BeforeWord = "before install", SinceWord = "since install";
        public const string PlacedBaseline = "piecesPlaced";   // LocalTotals.Baseline kind: the game's m_piecesPlacedStats when Hearthwoven first ran

        /// <summary>
        /// Plantings per plant piece in two layers (live doubt 1, Joost 2026-10-08 "Flax 16 planted / 1,694 harvested"): faded =
        /// the game's own planted counter when Hearthwoven first ran for this character (it books one per click, so a
        /// PlantEasily grid of 171 counts 1), solid = every plant Hearthwoven counted since (ClientHooks.Planting, one per
        /// plant). The game counter's later growth is never added. null = no baseline (a fellow's shared copy, local totals
        /// not loaded): the page then shows the game's counter alone, as before.
        /// </summary>
        public static Dictionary<string, (double faded, double solid)> PlantedLayers(PanelInput input)
        {
            if (!input.IsSelf || input.Events == null || input.Baseline == null || !input.Baseline.TryGetValue(PlacedBaseline, out var at) || at == null) return null;
            var d = new Dictionary<string, (double faded, double solid)>();
            foreach (var kv in at) if (kv.Value > 0 && PieceKind(input, kv.Key) == "planted") d[kv.Key] = (kv.Value, 0);
            foreach (var kv in input.Events.Planted) if (kv.Value > 0) { d.TryGetValue(kv.Key, out var l); d[kv.Key] = (l.faded, l.solid + kv.Value); }
            return d;
        }

        /// <summary>Planted per plant piece: both layers added up, or the game's counter when there is no baseline.</summary>
        public static Dictionary<string, double> PlantedTotals(PanelInput input)
        {
            if (input.Window != null) return D(input.Events?.Planted).Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);   // a day window: every plant you put in the ground those days
            var layers = PlantedLayers(input);
            return layers == null ? Placed(input, "planted") : layers.ToDictionary(kv => kv.Key, kv => kv.Value.faded + kv.Value.solid);
        }

        static void Farming(PanelInput input, PanelView view)
        {
            view.Recorded = true; view.Scope = RecordedScope(input);   // 0.7 (REDESIGN-RULES.md part 5, G3): no zones, no "since install"; the labels and the box are placed by RecordedModel.cs
            view.Heading = "Farming";
            var layers = PlantedLayers(input);
            var planted = PlantedTotals(input);
            double sown = planted.Values.Sum(), picked = C(input, "HarvestCrop");
            // rule A, planted: the game's count when Hearthwoven first ran plus every plant counted since, one number (the sum). "Earlier counts may
            // be incomplete." when that earlier count is above 0 or there is no baseline (a fellow's copy); none in a day window (rule W.1)
            double plantedBefore = layers?.Values.Sum(l => l.faded) ?? 0;
            double plantedSince = D(input.Events?.Planted).Where(kv => kv.Value > 0).Sum(kv => kv.Value);
            bool earlier = input.Window == null && (layers == null || plantedBefore > 0);
            var plantSrc = input.Window != null ? SrcPc : SrcCharacter;   // a day window: planted is counted here, picked is the game's counter's growth
            // rule B, picked: the game's counter now (SrcCharacter); rule K: the Farming skill rides in the hero's row
            Add(view, Hero((sown > 0 ? N(sown) : null, "planted", plantSrc, earlier ? EarlierIncomplete : null), (picked > 0 ? N(picked) : null, "picked", SrcCharacter, null)));
            SkillBeside(view, input, "Farming");

            // per crop: what you planted of it and what you picked of its item (m_pickableStats); trees are not crops
            var crops = new Dictionary<string, double>();
            var notCrops = new Dictionary<string, double>();
            foreach (var kv in planted)
            {
                var c = CropOf(input, kv.Key);
                if (c != null) Bump(crops, c, kv.Value); else Bump(notCrops, kv.Key, kv.Value);
            }
            // every crop you planted: picked big and planted small on its tile (m_pickableStats has the picks per item)
            double Got(string crop) => input.Harvested != null && input.Harvested.TryGetValue(crop, out var v) ? v : 0;
            var cropGrid = ItemGrid(crops.Select(kv => (kv.Key, Got(kv.Key), kv.Value)), k => Who(input, k), k => "item:" + k, SrcCharacter, "planted");
            // each crop tile carries two thin paired bars on the crop's own scale: planted (the sum, rule A) and picked (the game's counter, rule B)
            if (cropGrid != null)
            {
                cropGrid.Kind = "cropgrid"; cropGrid.Text = PickedScope;
                foreach (var i in cropGrid.Items)
                {
                    crops.TryGetValue(i.Id, out var sown1); var got = Got(i.Id); var max = Math.Max(1, Math.Max(sown1, got));
                    i.Kind = "crop";
                    i.Items = new List<Block> {
                        new Block { Kind = "pair", Tone = PlantedWord, Title = PlantedWord, Value = N(sown1), Fraction = (float)(sown1 / max), Colour = PlantedColour, Src = input.Window != null ? plantSrc : i.Src, Source = input.Window != null ? TagOfSrc(plantSrc) : i.Source },
                        new Block { Kind = "pair", Tone = PickedWord, Title = PickedWord, Value = N(got), Fraction = (float)(got / max), Colour = PickedColour, Src = SrcCharacter, Source = TagOfSrc(SrcCharacter) } };
                }
            }
            // fix4: the crops picked add up to the hero's number ON THE LIST: what you picked of plants you never planted (wild ones, a fellow's field) is the list's last tile,
            // "Other plants", so 155 + 71 + 66 + 20 reads as the 312 picked; the berries, mushrooms, honey and sap are other counters and say so under their own heading
            // a day window that begins before the picks per crop were kept (KeptWhole): the remainder would be days, not wild plants, so no tile
            var otherPlants = KeptWhole(input, DayHistory.PickedPrefix) ? picked - crops.Keys.Sum(Got) : 0;
            if (otherPlants > 0)
            {
                if (cropGrid == null) cropGrid = new Block { Kind = "cropgrid", Text = PickedScope, Src = SrcCharacter, Source = TagOfSrc(SrcCharacter), Items = new List<Block>() };
                cropGrid.Items.Add(new Block
                {
                    Kind = "crop", Tone = SlimTone, Id = OtherPlantsId, Icon = OtherPlantsIcon, Title = OtherPlants, Value = N(otherPlants), Src = SrcCharacter, Source = TagOfSrc(SrcCharacter),
                    Items = new List<Block> { new Block { Kind = "pair", Tone = PickedWord, Title = PickedWord, Value = N(otherPlants), Fraction = 1f, Colour = PickedColour, Src = SrcCharacter, Source = TagOfSrc(SrcCharacter) } },
                });
            }
            Under(view.Blocks, Section("Crops"), cropGrid);
            Under(view.Blocks, Section("Also planted"), Strip(notCrops.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, Who(input, kv.Key), kv.Value, "piece:" + kv.Key)), plantSrc));
            Under(view.Blocks, Section(AlsoHarvestedTitle), Strip(AlsoHarvested.Select(a => (a.stat, a.title, C(input, a.stat), a.icon)), SrcCharacter));

            // About these numbers (REDESIGN-RULES.md part 4, hard case 4): planted is the game's count before its split date and Hearthwoven's
            // count since; picked is the game's own counter from its own date. Two dates: one sentence per date, earliest first. No planted
            // baseline (a fellow's copy, totals not loaded): no box.
            if (layers != null && input.Window == null)
            {
                var plantedFrom = StartOf(input, LocalTotals.PlacedKind); var pickedFrom = StartOf(input, LocalTotals.StatsKind);
                var pickedSince = StatLayers(input, "HarvestCrop")?.since;
                string Plants(double n) => N(n) + " " + Label1(n, "plant", "plants");
                string plantedLine = "Hearthwoven counted every plant you put in the ground, " + Plants(plantedSince) + " so far.";
                string from;
                if (!pickedSince.HasValue) from = plantedLine;
                else if (plantedFrom == pickedFrom) from = plantedLine + " Picked is still the game's own count, " + N(pickedSince.Value) + " more.";
                else
                {
                    string When(DateTime? d) => d.HasValue ? RecordDate(input, d.Value) : "install";
                    var parts = new[] { (plantedFrom, "From " + When(plantedFrom) + ": " + plantedLine),
                                        (pickedFrom, "From " + When(pickedFrom) + ": picked is still the game's own count, " + N(pickedSince.Value) + " more.") };
                    from = string.Join(" ", parts.OrderBy(x => x.Item1 ?? DateTime.MinValue).Select(x => x.Item2));
                }
                var before = "The game's own count: " + Plants(plantedBefore) + ". It booked one planting per click, so a row planted at once by a mod may count as one.";
                AboutNumbers(view, input, plantedFrom, before, from, "Picked counts every plant you picked: your fields, fellow players' fields and wild plants.");
            }
            Plate(view, "title:farmer", RecordedScope(input));
        }

        // ---------- Fishing: hooked, caught, lost and caught by quality | caught per kind ----------

        public const string PlacementNote = "Placements: a piece you move counts again.";
        public const string FishHeldTitle = "Fish picked up";
        /// <summary>live-polish (Joost's book in game: 20 hooked, 8 got away, 0 caught, yet fish in his hands): the game books a catch only when
        /// the line is reeled in (FishingFloat.Catch: FishCaught and the fish's record); a fish grabbed by hand, taken from a trap or handed over
        /// is booked only as picked up (m_itemPickupStats, by its first holder).</summary>
        public const string FishHeldNote = "Every fish that came into your hands: reeled in, grabbed by hand, from a trap or from a fellow player. Only a reeled-in fish counts as caught.";

        static void Fishing(PanelInput input, PanelState state, PanelView view)
        {
            view.Recorded = true; view.Scope = RecordedScope(input);   // 0.7 (G3): no zones, no "since install"; the box (rule B) is placed by RecordedModel.cs
            view.Heading = "Fishing";
            // rule B: the game's three counters, each booked at its own moment (FishingFloat.cs): the bite (hooked), the line reeled in (caught), the
            // fish lost for want of stamina (got away). The hero holds caught and hooked (a real 0 caught stays "0"); rule K: the skill rides beside it
            double caughtNow = C(input, "FishCaught"), hookedNow = C(input, "FishHooked");
            if (caughtNow > 0 || hookedNow > 0) Add(view, Hero((N(caughtNow), Label1(caughtNow, "fish caught", "fish caught"), SrcCharacter, null), (hookedNow > 0 ? N(hookedNow) : null, "hooked", SrcCharacter, null)));
            SkillBeside(view, input, "Fishing");
            var hcl = new[] { ("FishHooked", "Hooked", "#6f8fa8"), ("FishCaught", "Caught", "#e8a948"), ("FishLost", "Got away", "#b8432e") };
            var left = new List<Block>();
            // They need not add up and no remainder is named (fish-held, Joost + Codex: 20 hooked and 8 got away do not prove 12 of anything). Beside a bite,
            // a counter at 0 stays: "Caught 0" says no fish was reeled in
            var hookRows = hcl.Select(x => new KeyValuePair<string, double>(x.Item2, C(input, x.Item1))).ToList();
            var caughtRows = Ranking(hookRows, k => k, null, SrcCharacter, keepOrder: true, colour: k => hcl.First(x => x.Item2 == k).Item3);
            if (caughtRows != null && C(input, "FishHooked") > 0)
                caughtRows.Items = hcl.Select(x => caughtRows.Items.FirstOrDefault(i => i.Id == x.Item2)
                                                   ?? new Block { Id = x.Item2, Title = x.Item2, Value = "0", Icon = "", Colour = x.Item3, Src = SrcCharacter, Source = TagCharacter }).ToList();
            if (caughtRows != null) left.Add(caughtRows);

            // fish caught on the line, per kind: m_pickableStats books a catch under the fish's name token ("$animal_fish1"),
            // every other pickable under a prefab name, so the tokens are the fish
            var kinds = (input.Harvested ?? new Dictionary<string, float>()).Where(kv => kv.Key.StartsWith("$") && kv.Value > 0)
                                                                            .ToDictionary(kv => kv.Key, kv => (double)kv.Value);
            var most = kinds.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).FirstOrDefault();
            var tiers = Enumerable.Range(1, 6).Select(q => C(input, "FishCaughtTier" + q)).ToList();
            var top = tiers.FindLastIndex(v => v > 0);
            if (top >= 0)
                Under(left, Section("Caught by quality, 6 best"), new Block
                {
                    Kind = "grades", Src = SrcCharacter, Source = TagCharacter,
                    // the fish you caught most as the picture, larger with each quality (one ready sprite, sized once)
                    Items = tiers.Take(top + 1).Select((v, q) => new Block { Id = (q + 1).ToString(Inv), Title = (q + 1).ToString(Inv), Value = N(v), Icon = most != null ? "item:" + most : "item:Fish1", Src = SrcCharacter, Source = TagCharacter }).ToList(),
                });
            var right = new List<Block>();
            // the kinds the game names, each from the same catch record (the line reeled in); their sum is the title, no remainder tile (fish-held)
            var named = kinds.Values.Sum(); var caughtAll = C(input, "FishCaught");
            var fishGrid = ItemGrid(kinds, k => Who(input, k), k => "item:" + k, SrcCharacter);
            if (fishGrid != null) { right.Add(Section("Fish caught", named, SrcCharacter)); right.AddRange(Sorted(state, SortKey(state, "fishing"), fishGrid, fishGrid.Items.Count)); }   // the total in the title (r4deeds-fishing); Most or A-Z (SortModel.cs)
            // the fish picked up (the game's first-holder record of fish items), shown when it holds more than the catches: fish that came in
            // another way than the line (live-polish). Never added to caught. Rule A (hard case 10): before Hearthwoven first ran plus every fish counted since
            var held = FishBroughtIn(input);
            var heldTotal = held.Values.Sum(v => v.before + v.exact);
            if (heldTotal > Math.Max(named, caughtAll))
            {
                var heldSrc = input.Window != null ? SrcPc : SrcCharacter;   // a day window: what came into your hands those days, counted here
                var heldHead = Section(FishHeldTitle, heldTotal, heldSrc);
                // "Earlier counts may be incomplete." in the section's own slot when an earlier part is above 0 or there is no pickup baseline (rule A.3)
                if (input.Window == null && (!HasPickupBaseline(input) || held.Values.Any(v => v.before > 0))) heldHead.Text = EarlierIncomplete;
                Under(right, heldHead, ItemGrid(held.ToDictionary(kv => kv.Key, kv => kv.Value.before + kv.Value.exact), k => Who(input, k), k => "item:" + k, heldSrc));
                right.Add(new Block { Kind = "note", Text = FishHeldNote });
            }
            Columns(view, left, right);
            // About these numbers (rule B, hard case 4 needs no second date here: the caught counter is the only one this box speaks of)
            var caughtLayers = StatLayers(input, "FishCaught");
            if (caughtLayers.HasValue && input.Window == null)
            {
                var (before, since) = caughtLayers.Value;
                AboutNumbers(view, input, StartOf(input, LocalTotals.StatsKind),
                    "The game's own count: " + N(before) + " " + Label1(before, "fish caught", "fish caught") + ".",
                    "Still the game's own count: " + N(since) + " more " + Label1(since, "fish caught", "fish caught") + ".",
                    "Only a fish reeled in counts as caught. A fish grabbed by hand, from a trap or from a fellow player is only picked up.");
            }
            Plate(view, "title:fisher", RecordedScope(input));
        }

        /// <summary>
        /// The fish picked up in rule A form (REDESIGN-RULES.md, hard case 10): BroughtIn's arithmetic over the fish tokens only ("$animal_fish*"),
        /// so GatherKind stays as it is. Before = the game's pickup counter when Hearthwoven first ran (LocalTotals.Baseline "pickedUp"); exact =
        /// what Hearthwoven counted since, less what it had counted by then (ExactAtBaseline). Without a baseline: the game's counter alone.
        /// </summary>
        static Dictionary<string, (double before, double exact)> FishBroughtIn(PanelInput i)
        {
            bool fish(string k) => k.StartsWith("$animal_fish", StringComparison.Ordinal);
            var r = new Dictionary<string, (double before, double exact)>();
            Dictionary<string, float> stored = null;
            var known = i?.Baseline != null && i.Baseline.TryGetValue("pickedUp", out stored) && stored != null;
            var exact = Only(i?.Events?.PickedUp, fish);
            Dictionary<string, float> already = null;
            if (known && i.ExactAtBaseline != null && i.ExactAtBaseline.TryGetValue("pickedUp", out already) && already != null)
                foreach (var k in exact.Keys.ToList()) if (already.TryGetValue(k, out var a)) exact[k] = Math.Max(0, exact[k] - a);
            var keys = new HashSet<string>(exact.Keys);
            foreach (var kv in (known ? stored : i?.ItemsPickedUp) ?? new Dictionary<string, float>())
                if (kv.Value > 0 && fish(kv.Key)) keys.Add(kv.Key);
            foreach (var k in keys)
            {
                exact.TryGetValue(k, out var e);
                double before;
                if (known) before = stored.TryGetValue(k, out var s) ? s : 0;
                else before = Math.Max(0, (i.ItemsPickedUp != null && i.ItemsPickedUp.TryGetValue(k, out var c) ? c : 0) - e);
                if (before + e > 0) r[k] = (before, e);
            }
            return r;
        }

        static void Taming(PanelInput input, PanelView view)
        {
            view.Recorded = true; view.Scope = RecordedScope(input);   // 0.7 (G3): no zones, no "since install"
            view.Heading = "Taming";
            // rule B: the game's counters, complete (no count per kind of creature exists); tamed is the owner's alone (rule D, TamedNote)
            Add(view, Counts(new[] { ("CreatureTamed", "Tamed", "vocab:tame-tamed"), ("TamedPetting", "Petted", "vocab:tame-petted"), ("TamedCommand", "Commands given", "vocab:tame-command") }
                                 .Select(s => (s.Item1, s.Item2, C(input, s.Item1), s.Item3, (string)null)), SrcCharacter));   // Codex's care pictures (no game icon exists)
            var care = view.Blocks.LastOrDefault(b => b.Kind == "counts"); if (care != null) care.Tone = "framed";   // thin single frames, each picture in its own framed square
            if (C(input, "CreatureTamed") > 0) view.Blocks.Add(new Block { Kind = "note", Text = TamedNote });   // honest: the game books a tame to the creature's owner
            LedGroup(view, input);    // 0.6: animals led (Chapters/LedModel.cs), class C6: its own group date
            // 0.6: born in your care (Chapters/CargoModel.cs: counted on this PC since its date) and born near you (the server's book): side by side as one
            // compact pair, each keeps its own source mark and its own label
            var careCol = Stretch(v => BornGroup(v, input)); var nearCol = Stretch(v => BornNearGroup(v, input));
            if (careCol.Count > 0 && nearCol.Count > 0) view.Blocks.Add(new Block { Kind = "columns", Tone = PairTone, Items = new List<Block> { new Block { Kind = "column", Items = careCol }, new Block { Kind = "column", Items = nearCol } } });
            else { view.Blocks.AddRange(careCol); view.Blocks.AddRange(nearCol); }
            // About these numbers (rule B): the game's own counts of tames, pets and commands before the stats baseline's date; the led and born groups
            // say their own dates on their blocks. No baseline of the stats kind (a fellow's copy, totals not loaded): no box.
            if (input.Window == null && HasBaseline(input, LocalTotals.StatsKind))
                AboutNumbers(view, input, StartOf(input, LocalTotals.StatsKind),
                    "The game's own count of tames, pets and commands. A tame goes to the player whose PC hosted the animal.",
                    "Hearthwoven also counts animals you lead and young born in your care.",
                    "Born near you is counted by the server; near is not bred.");
            Plate(view, "title:tamer", RecordedScope(input));
        }
        // ---------- a day window on the Deeds pages (0.7, HISTORY-06.md) ----------

        /// <summary>
        /// The window's copy of the input for a Deeds page (InWindow): the game's counters, pieces placed and plants picked grew this much
        /// those days, and the items you made (ItemsCrafted) are what Hearthwoven counted you making (SessionEvents.Made: crafted, or taken off
        /// a cooking station by you; for gear the same booking as the game's own craft counter). null when the window cannot be shown.
        /// </summary>
        public static PanelInput DeedsWindow(PanelInput input, TimeWindow w)
        {
            var c = InWindow(input, w);
            if (c != null) c.ItemsCrafted = new Dictionary<string, float>(c.Events.Made);
            return c;
        }

        /// <summary>
        /// The short window's copy of the input for a Deeds page (10 min .. Session; RecentOf over this session's DeedLog), built as InWindow builds
        /// a day window's so each page's own code renders it unchanged: Events holds the DeedLog's SessionEvents families (picked up, made, planted,
        /// felled, hits, skill practice) and nothing else, the game's counters (Character) only the ones counted per minute (DeedLog.CounterStats),
        /// PiecesPlaced and Harvested the per-token counters' growth, ItemsPickedUp and ItemsCrafted the pickups and the items made. What has no
        /// minutes is absent, never its all-time value: the page leaves it out and DeedsDayLines says so. null when the window cannot be shown
        /// (no DeedLog yet: <paramref name="why"/> says it for the player).
        /// </summary>
        public static PanelInput DeedsShort(PanelInput input, TimeWindow w, out string why)
        {
            why = null;
            var r = RecentOf(input, w);
            if (!r.Shown) { why = r.Why; return null; }
            var ev = new SessionEvents();
            foreach (var fam in ev.Named())
                if (Array.IndexOf(DeedLog.EventFamilies, fam.Key) >= 0 && r.Grew.TryGetValue(fam.Key, out var d)) foreach (var kv in d) fam.Value[kv.Key] = kv.Value;
            Dictionary<string, float> Game(string fam) => r.NotRecorded.Contains(fam) ? null : r.Grew.TryGetValue(fam, out var d) ? new Dictionary<string, float>(d) : new Dictionary<string, float>();
            var c = input.ShallowCopy();
            c.Events = ev; c.DamageSinceInstall = new DamageTally(); c.BiomeSinceInstall = new BiomeTally(); c.BiomeFromUtc = null;
            c.Character = Game(DeedLog.Counters); c.PiecesPlaced = Game(DeedLog.Placed); c.Harvested = Game(DeedLog.Picked);
            c.EnemyKills = null;   // the kills per foe: no Deeds page reads them
            c.ItemsPickedUp = new Dictionary<string, float>(ev.PickedUp); c.ItemsCrafted = new Dictionary<string, float>(ev.Made);
            c.Baseline = null; c.ExactAtBaseline = null; c.BaselineAt = null; c.Book = null;
            var today = LocalToday(input);
            c.Window = new DayView { Window = w, From = today, To = today, Clipped = r.Clipped };
            return c;
        }

        /// <summary>The lines of a Deeds page in a short window for a number it shows in Today and longer (the game's counters kept per day, not per minute).</summary>
        public const string NoMinutesRepairs = "Repairs show in Today and longer windows.",
                            NoMinutesUpgrades = "Upgrades show in Today and longer windows.",
                            NoMinutesPicked = "Total picked, berries, mushrooms, honey and sap show in Today and longer windows.",
                            NoMinutesHooked = "Hooked, got away and caught by quality show in Today and longer windows.";

        /// <summary>Whether a day window holds every day of a counter (a stat name) or family (DayHistory prefix) the history began keeping
        /// later (DayHistory.Began); outside a window always true.</summary>
        public static bool KeptWhole(PanelInput input, string counter) =>
            input?.Window == null || input.Window.Short || input.History == null || input.Window.From >= input.History.KeptFrom(counter, input.Window.To);

        /// <summary>"Pieces placed are counted per day from 10 Oct: this window counts them from then." The line of a page whose window
        /// begins before a counter it reads was kept per day.</summary>
        public static string KeptFromLine(string what, DateTime from, DateTime today) => what + " counted per day from " + ShortDate(from, today) + ": this window counts them from then.";
        public const string NoDaysFood = "Fellow players' records have no days: choose All to see who enjoyed your food.",
                            NoDaysGear = "Fellow players' records have no days: choose All to see who put your gear to good use.",
                            NoDaysTaming = "Born near you (the server's book) and your longest lead have no days: choose All to see them.";

        /// <summary>
        /// What a Deeds page in a day window says beside its numbers (on the plate, after them): what it left out because it has no days
        /// (only when All has it: fellows' records, the server's book, a record), from when a counter it reads is kept per day, that a very
        /// full day kept only its largest kinds, and the window's empty line when nothing was counted.
        /// </summary>
        static void DeedsDayLines(PanelInput all, PanelInput day, string page, PanelView view, TimeWindow w)
        {
            var plate = PlateOf(view); if (plate == null) return;
            var items = plate.Items;
            bool Numbers(IEnumerable<Block> bs) => (bs ?? Enumerable.Empty<Block>()).Any(b => b != null && b.Kind != "ladders" && b.Kind != "empty" && ((b.Src == SrcPc || b.Src == SrcCharacter) || Numbers(b.Items)));
            if (!Numbers(items)) { items.RemoveAll(b => b.Kind == "empty"); items.Insert(0, DayEmpty(day, w)); }
            void Line(string text) => items.Add(new Block { Kind = "note", Text = text });
            var today = day.Window.To;
            void Kept(string counter, string what)
            {
                if (!KeptWhole(day, counter)) Line(KeptFromLine(what, day.History.KeptFrom(counter, today), today));
            }
            if (day.Window.Short)
                switch (page)   // what has no minutes, said once when All has it; the fellows' records as in a day window
                {
                    case "cooking": if (FoodAxis(all) != null) Line(NoDaysFood); break;
                    case "building": if (D(all.Events?.Repairs).Sum(kv => kv.Value) > 0) Line(NoMinutesRepairs); break;
                    case "crafting": if (C(all, "Upgrades") > 0) Line(NoMinutesUpgrades); if (GearPutToGoodUse(all) != null) Line(NoDaysGear); break;
                    case "farming": if (C(all, "HarvestCrop") > 0 || AlsoHarvested.Any(a => C(all, a.stat) > 0)) Line(NoMinutesPicked); break;
                    case "fishing": if (C(all, "FishHooked") > 0 || C(all, "FishLost") > 0) Line(NoMinutesHooked); break;
                }
            else switch (page)
            {
                case "cooking":
                    if (FoodAxis(all) != null) Line(NoDaysFood);
                    if (Placed(all, "feast").Values.Sum() > 0) Kept(DayHistory.PlacedPrefix, "Feasts set out are");
                    break;
                case "building": Kept(DayHistory.PlacedPrefix, "Pieces built are"); break;
                case "groundwork": Kept(DayHistory.PlacedPrefix, "Groundwork strokes are"); break;
                case "crafting": if (GearPutToGoodUse(all) != null) Line(NoDaysGear); break;
                case "farming": Kept(DayHistory.PickedPrefix, "Picks per crop are"); break;
                case "fishing": Kept(DayHistory.PickedPrefix, "Fish per kind are"); break;
                case "taming":
                    if ((all.Book?.BornNear ?? new Dictionary<string, double>()).Any(kv => kv.Value > 0) || (LedTotal(all) > 0 && BestOf(all, LedTracker.BestKey) != null)) Line(NoDaysTaming);
                    Kept("CreatureTamed", "Creatures tamed are");
                    break;
            }
            if (day.Window.Clipped) Line(ClippedLineOf(day.Window));
        }
    }
}
