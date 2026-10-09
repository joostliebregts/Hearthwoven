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
                case "cooking": Cooking(input, view); return true;
                case "building": Building(input, state, view); return true;
                case "groundwork": Groundwork(input, view); return true;
                case "crafting": Crafting(input, state, view); return true;
                case "farming": Farming(input, view); return true;
                case "fishing": Fishing(input, view); return true;
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
        /// <summary>The scope of the Deeds overview, once, under the heading: every big number is the character's, the marked ones Hearthwoven's.</summary>
        public static string OverviewScope(PanelInput i) => i.IsSelf ? "Counts " + MadeLine(i) + "; \u201csince install\u201d: counted on this PC." : null;
        public const string Earned = "Earned", Unsung = "Unsung", EveryNameEarned = "Every name earned.";

        static void DeedsOverview(PanelInput input, PanelView view, PanelState state)
        {
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
            Card("woodcutting", "woodcutter", (TreesFelled(input), "tree felled", "trees felled", TreesFelledLayers(input) is var tl && tl.HasValue && tl.Value.faded <= 0 && tl.Value.solid > 0 ? SrcPc : SrcCharacter), (M(input, e => e.ChopHits), "axe hit", "axe hits", SrcPc));
            Card("mining", "miner", (BroughtInTotal(input, "mining"), "stone, ore brought in", "stone, ore brought in", BroughtInSrc(input, "mining")), (M(input, e => e.PickaxeHits), "hit", "hits", SrcPc));   // K6: one pickaxe-hits number (since install; "hits" fits the card line), brought in as on the Mining bar (K1)
            Card("farming", "farmer", (C(input, "HarvestCrop"), "crop picked", "crops picked", SrcCharacter), (PlantedTotals(input).Values.Sum(), "planted", "planted", SrcCharacter));
            Card("fishing", "fisher", (C(input, "FishCaught"), "fish caught", "fish caught", SrcCharacter), (C(input, "FishHooked"), "hooked", "hooked", SrcCharacter));
            Card("taming", "tamer", (C(input, "TamedPetting") + C(input, "TamedCommand"), "petted and commanded", "petted and commanded", SrcCharacter), (C(input, "CreatureTamed"), "tamed", "tamed", SrcCharacter));
            if (cards.Count > 0) earned.Add(new Block { Kind = "cards", Items = cards });
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
            Switch(view, state, "overview", "view", state != null && state.View.TryGetValue("Deeds/overview/view", out var shownView) && shownView == "unsung" ? UnsungCaption : OverviewScope(input), ("earned", Earned, earned),
                   // the switch always shows (Joost's live test: Unsung was not visible); every name earned: the view says so
                   ("unsung", Unsung, unsung.Count > 0 ? new List<Block> { new Block { Kind = "cards", Tone = "unsung", Items = unsung } }
                                                       : new List<Block> { new Block { Kind = "note", Text = EveryNameEarned } }));
            Plate(view, "ui:chapter-deeds", FellowScope(input));
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
            ["wallwarden"] = "Build and arm a defence.", ["defender"] = "Block your first blow.", ["fighter"] = "Fell your first foe.", ["bossbane"] = "Defeat a Forsaken.",
            ["tamer"] = "Pet or tame a creature.", ["fisher"] = "Catch your first fish.", ["trader"] = "Buy something from a trader.",
        };

        /// <summary>Servings of this player's food that fellow players enjoyed: meals and feast servings, the number the Cooking page's
        /// "Who enjoyed" head and Company > Food shared show too (one source, so the overview card never says 10 where the page says 13).</summary>
        public static double EnjoyedByFellows(PanelInput input)
        {
            return FoodRows(input).Sum(r => r.total);   // the numbers themselves (RESILIENCE-06 item 1: the shown "1 000" once closed the panel)
        }

        /// <summary>The mark of a brought-in number: this PC's when every item of it was counted since install, your character's otherwise.</summary>
        static string BroughtInSrc(PanelInput input, string kind) => BroughtIn(input, kind).Values.All(v => v.before <= 0) ? SrcPc : SrcCharacter;

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

        static void Cooking(PanelInput input, PanelView view)
        {
            view.Heading = "Cooking";
            var layers = CookedLayers(input);
            var made = DishesCooked(input);
            var fadedAll = layers?.Values.Sum(l => l.faded) ?? 0;
            // the feasts set out are the character's count: they share the hero line only while the dishes are the character's too
            var setOut = Placed(input, "feast").Values.Sum(); var feastBeside = setOut > 0 && made > 0 && !(layers != null && fadedAll <= 0);
            var hero = Hero((made > 0 ? N(made) : null, Label1(made, "dish cooked", "dishes cooked"), SrcCharacter, null),
                            (feastBeside ? N(setOut) : null, Label1(setOut, "feast set out", "feasts set out"), SrcCharacter, null));
            // every dish counted on this PC since install: the page's numbers are this PC's, not the character's
            var src = layers != null && fadedAll <= 0 ? SrcPc : SrcCharacter;
            if (hero != null && layers != null)
            {
                double solid = layers.Values.Sum(l => l.solid);
                if (fadedAll > 0 && solid > 0) { hero.Faded = N(fadedAll); hero.Solid = N(solid); }
                else if (fadedAll <= 0) { hero.Src = SrcPc; hero.Source = TagOfSrc(SrcPc); }
            }
            // the page has a hero, a bar, a ledger and the fellows' bars: the number at the second-number size and the bar thin (as
            // Woodcutting), so it fits the plate without scrolling at the default scale
            if (hero != null) hero.Tone = hero.Items != null && hero.Items.Count > 0 ? Compact : ZoneSecond;
            Add(view, hero);
            // dishes by kind (cook-A): one bar, the food's own colours from its icons; with a baseline each kind in its two layers,
            // faded = the game's count before install, solid = made since (as Woodcutting's wood bar)
            var dishes = layers == null ? Crafted(input, "food") : layers.ToDictionary(kv => kv.Key, kv => kv.Value.faded + kv.Value.solid);
            var bar = Composition(DishesByKind, dishes, k => Who(input, k), k => (null, null), src, tint: ItemTint(input));
            if (bar != null) bar.Tone = Thin;
            if (bar != null && layers != null)
            {
                foreach (var part in bar.Items)
                {
                    if (!layers.TryGetValue(part.Id, out var l) || l.faded + l.solid <= 0) continue;
                    part.Fraction2 = (float)(l.faded / (l.faded + l.solid));
                    if (l.faded <= 0 && src != SrcPc) { part.Src = SrcPc; part.Source = TagOfSrc(SrcPc); }   // made since install, in a bar of older counts
                }
                if (fadedAll > 0) bar.Note = FadedKey;
            }
            Add(view, bar);
            // without the two layers the headline is the game's own counters and the bar is its per-dish craft counts: the game books
            // them differently (a mod's food it cannot name, brews among the dishes), so they can differ. Say so under the headline
            // instead of hiding it (polish-06's honest headline, on cooking-06's bar).
            if (hero != null && bar != null && layers == null)
            {
                var kinds = Math.Round(dishes.Values.Sum());
                if (made > 0 && kinds != Math.Round(made)) hero.Note = "the dishes add up to " + N(kinds) + "; the headline is " + (input.IsSelf ? "your" : Name(input) + "'s") + " character's own count";
            }
            // the kitchen ledger (cook-B): where it was cooked, dishes as chips, and the one line on what is not counted apart
            var ledger = KitchenLedger(input, dishes, src);
            Under(view.Blocks, Section(WhereCooked), ledger);
            if (ledger == null && !input.IsSelf && made > 0) view.Blocks.Add(new Block { Kind = "note", Text = Name(input) + "'s shared copy does not say where it was cooked." });
            if (setOut > 0 && !feastBeside) view.Blocks.Add(new Block { Kind = "stat", Value = N(setOut), Title = Label1(setOut, "feast set out", "feasts set out"), Source = TagCharacter });
            // who enjoyed it (cook-B): one bar per fellow in their player colour, the count at the end; alphabetical, never a ranking
            var axis = FoodAxis(input);
            var whose = input.IsSelf ? "your" : Name(input) + "'s";
            if (axis != null)
            {
                var fans = FoodRows(input).Select(r => new KeyValuePair<string, double>(r.fellow.PlayerName, r.total)).ToList();   // the numbers, not the shown text
                var whoHead = Section("Who enjoyed " + whose + " food", fans.Sum(f => f.Value), SrcFellows); whoHead.Text = input.IsSelf ? TheirScope(FellowsOf(input).Where(f => !f.IsSelf).All(f => f.SharedSinceInstall)) : null;   // whose record: their last session
                Under(view.Blocks, whoHead, Ranking(fans, k => k, k => "person:" + k, SrcFellows, top: int.MaxValue, keepOrder: true));
            }
            else if (made > 0) view.Blocks.Add(new Block { Kind = "note", Text = "When a fellow player enjoys " + whose + " food, they show up here." });
            SkillStrip(input, view, SkillHeading, new[] { "Cooking" });
            Plate(view, "title:cook", FellowScope(input));
        }

        /// <summary>
        /// How many of a dish this player made in all (both Cooking layers), keyed as eaters record it (the item prefab,
        /// "CookedMeat"; a token works too); null = not known (no baseline, a fellow's copy, an item the game data does not name).
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
                return v(at) + Math.Max(0, v(input.Events.Made) - v(already));
            };
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
        /// what this player made of each dish where that is known (CapToMade); alphabetical.</summary>
        public static List<(string who, Dictionary<string, double> dishes)> MealsEnjoyed(PanelInput input)
        {
            var eaten = new List<(string who, Dictionary<string, double> dishes)>();
            foreach (var f in FellowsOf(input).OrderBy(f => f.PlayerName, StringComparer.OrdinalIgnoreCase))
            {
                var meals = new Dictionary<string, double>();
                foreach (var kv in f.Events.AteFoodMadeBy) { var p = Split2(kv.Key); if (SameName(p[0], input.PlayerName)) Bump(meals, p[1], kv.Value); }
                eaten.Add((f.PlayerName, meals));
            }
            return CapToMade(eaten, MadeOf(input));
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
        /// "Who enjoyed" head sum them.</summary>
        static List<(PanelInput fellow, Dictionary<string, double> meals, Dictionary<string, double> feasts, double total)> FoodRows(PanelInput input)
        {
            var rows = new List<(PanelInput, Dictionary<string, double>, Dictionary<string, double>, double)>();
            var enjoyed = MealsEnjoyed(input);
            int at = 0;
            foreach (var f in FellowsOf(input).OrderBy(f => f.PlayerName, StringComparer.OrdinalIgnoreCase))
            {
                var meals = enjoyed[at++].dishes; var feasts = new Dictionary<string, double>();
                foreach (var kv in f.Events.AteFromFeastOf)
                {
                    var p = Split2(kv.Key);
                    if (long.TryParse(p[0], NumberStyles.Integer, Inv, out var id) && id != 0 && id == input.PlayerId) Bump(feasts, p[1], kv.Value);
                }
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
            view.Heading = "Building";
            Func<string, string> named = k => Who(input, k);
            var built = Placed(input, "built");
            // the twin (Deeds twins): the game's placed-pieces counter is complete, so since install = it now minus its baseline
            var layers = CounterLayers(input, PlacedBaseline, input.PiecesPlaced, k => PieceKind(input, k) == "built");
            FacetResult filter = null;
            // every piece as a tile with the game's own picture, most first, the full list (Joost 2026-10-08: a grid like the other lists)
            if (built.Count > 0)
            {
                // the pieces as a filter bar (Category, Main material: FacetModel.cs, BuildingFacets.cs) when the game data names them, then the pieces that pass it
                filter = PieceFilter(input, state, built);
                var grid = filter != null ? ItemGrid(filter.Shown.Select(i => (i.Key, i.Weight, 0.0)), named, k => "piece:" + k, SrcCharacter) : ItemGrid(built, named, k => "piece:" + k, SrcCharacter);
                var twin = false; if (grid != null && layers != null) LayerTiles(grid, layers);   // the tiles say "since install" themselves: no key above them
                // the number is the page (reviewer: "866 pieces built is only a small heading"): the hero, with what it counts said beside it
                Add(view, Hero((N(built.Values.Sum()), Label1(built.Values.Sum(), "piece built", "pieces built"), SrcCharacter, input.IsSelf ? PlacementNote : "Placements: a piece moved counts again.")));
                if (filter == null) { if (grid != null) { view.Blocks.Add(Section("Every piece")); if (twin) view.Blocks.Add(TwinKey()); view.Blocks.Add(grid); } }
                else { view.Blocks.Add(filter.Bar); if (twin) view.Blocks.Add(TwinKey()); view.Blocks.Add(grid ?? new Block { Kind = "empty", Title = "Nothing for this choice" }); }
            }
            else { var b = BuiltCount(input); if (b > 0) Add(view, Hero((N(b), Label1(b, "piece built", "pieces built"), SrcCharacter, null))); }
            var repaired = M(input, e => e.Repairs);
            if (layers == null)
            {
                if (repaired > 0)
                    Under(view.Blocks, Section("Pieces repaired", repaired, SrcPc), Ranking(D(input.Events.Repairs), named, k => "piece:" + k, SrcPc, top: PieceTop, columns: 2));
            }
            else
            {
                // since install on this PC: the pieces placed since (the game's counter minus its baseline) and the repairs (counted exactly here).
                // With a filter chosen the block follows it (fix3: the header said "2 pieces" while this block still counted all 217) and says so
                Began(view, input, PlacedBaseline);
                var on = filter != null && FilterOn(state, BuildFilter);
                bool keep(string k) => !on || filter.Pass(PieceFacetItem(input, k, 0));
                var since = SinceOnly(layers).Where(kv => keep(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
                var fixes = D(input.Events?.Repairs).Where(kv => keep(kv.Key)).ToList();
                var fixed_ = on ? fixes.Sum(kv => kv.Value) : repaired;
                var hero = SinceHero((since.Values.Sum(), "piece built", "pieces built"), (fixed_, "piece repaired", "pieces repaired"));
                if (hero != null && on) hero.Note = FollowsFilter;
                Add(view, hero);
                Under(view.Blocks, Section("Pieces built"), Ranking(since, named, k => "piece:" + k, SrcPc, top: PieceTop, columns: 2));
                if (fixed_ > 0) Under(view.Blocks, Section("Pieces repaired"), Ranking(fixes, named, k => "piece:" + k, SrcPc, top: PieceTop, columns: 2));
                if (!view.Blocks.Any(b => ZoneOf(b) == SrcPc)) Add(view, on ? SinceNothingFor() : SinceInstallEmpty(input, null, null));
            }
            Plate(view, "title:builder", FellowScope(input));
        }

        // a filtered since-install block with nothing in it: it says it is the choice that is empty, not the counting
        // (a note with the pc mark, not an "empty" block: the zone is not empty, the page's whole count since install is there; only this choice has none)
        static Block SinceNothingFor() => new Block { Kind = "note", Text = "Nothing for this choice since install.", Src = SrcPc, Source = TagOfSrc(SrcPc) };

        // a grid's tiles in their two layers where an item has both (the game's count before install faded, since install solid), the legend chip once
        // returns true when some tile has both (the caller then puts the key, FadedKeyTwin, above the grid)
        static bool LayerTiles(Block grid, Dictionary<string, (double before, double since)> layers)
        {
            // zones-wording: the tile's big number is the whole; under it the part since install in words ("80 since install"), never "440 + 80"
            var any = false;
            foreach (var i in grid.Items ?? new List<Block>())
                if (layers.TryGetValue(i.Id, out var l) && l.before > 0 && l.since > 0) { i.Value2 = N(l.since); i.Text = SinceWord; any = true; }
            return any;
        }
        static Block TwinKey() => new Block { Kind = "note", Text = FadedKeyTwin };

        // Groundwork has its own page right after Building (Joost, play-test: the long piece list pushed it out of sight): raising,
        // lowering and levelling the ground, paths, paved roads, cultivating; the game's counter per kind, one bar with a pattern each
        // (materials per piece are not recorded, so no material bar)
        static void Groundwork(PanelInput input, PanelView view)
        {
            view.Heading = "Groundwork";
            var bar = Composition("Groundwork", Placed(input, "ground"), k => Who(input, k), GroundOf, SrcCharacter, icon: k => "piece:" + k);
            // the twin: the game's counter minus its baseline. The bar in its two layers (a part's faded share = placed before install)
            var layers = CounterLayers(input, PlacedBaseline, input.PiecesPlaced, k => PieceKind(input, k) == "ground");
            if (bar != null)
            {
                Add(view, Hero((bar.Value, "groundwork strokes", SrcCharacter, null)));   // the total is the hero's number, so the bar's head stays empty
                bar.Title = null; bar.Value = null;
                if (layers != null)
                {
                    foreach (var part in bar.Items) if (layers.TryGetValue(part.Id, out var l) && l.before + l.since > 0) part.Fraction2 = (float)(l.before / (l.before + l.since));
                    if (bar.Items.Any(p => p.Fraction2 > 0)) bar.Note = FadedKey;
                }
                view.Blocks.Add(bar);
            }
            if (layers != null)
            {
                Began(view, input, PlacedBaseline);
                Add(view, SinceHero((layers.Values.Sum(l => l.since), "groundwork stroke", "groundwork strokes")));
                Add(view, Ranking(SinceOnly(layers), k => Who(input, k), k => "piece:" + k, SrcPc, top: PieceTop, columns: 2));
                if (!view.Blocks.Any(b => ZoneOf(b) == SrcPc)) Add(view, SinceInstallEmpty(input, null, null));
            }
            Plate(view, "vocab:ground-lower", FellowScope(input));
        }

        // ---------- Crafting: gear crafted and upgrades (hero), the gear filter (Kind, Main material) with its bars and item grid, fellows who put your gear to good use ----------

        // the game's craft counters per kind (InventoryGui.DoCrafting) with the item types they count: the filter's Kind row (CraftingFacets.cs)
        static readonly (string stat, string title, string colour, string[] types, string fallback)[] GearKinds =
        {
            ("CraftWeapon", "Weapons", "#8c6e9a", new[] { "OneHandedWeapon", "TwoHandedWeapon", "TwoHandedWeaponLeft", "Bow", "Hands" }, "vocab:weapon-melee"),
            ("CraftArmor", "Armour", "#7d9a6a", new[] { "Chest", "Helmet", "Legs", "Shoulder", "Shield" }, null),
            ("CraftTool", "Tools", "#b59a5a", new[] { "Tool" }, null),
            ("CraftTrinket", "Trinkets", "#a8707a", new[] { "Trinket" }, null),
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
            view.Heading = "Crafting";
            double gear = GearKinds.Sum(k => C(input, k.stat)), up = C(input, "Upgrades");
            Add(view, Hero((gear > 0 ? N(gear) : null, "gear crafted", SrcCharacter, null), (up > 0 ? N(up) : null, Label1(up, "upgrade made", "upgrades made"), SrcCharacter, null)));
            // the gear as a filter bar (Kind, Main material: FacetModel.cs, CraftingFacets.cs), then the gear that passes it as the item grid
            // the twin: the game's craft counters are complete (a batch of arrows counts once, as the game books it), so since install = now minus baseline
            var items = CounterLayers(input, CraftedBaseline, input.ItemsCrafted, k => ItemKind(input, k) == "gear");
            var filter = GearFilter(input, state, Crafted(input, "gear"));
            if (filter != null)
            {
                view.Blocks.Add(filter.Bar);
                var grid = ItemGrid(filter.Shown.Select(i => (i.Key, i.Weight, 0.0)), k => Who(input, k), k => "item:" + k, SrcCharacter);
                var twin = false; if (grid != null && items != null) LayerTiles(grid, items);
                if (twin) view.Blocks.Add(TwinKey());
                view.Blocks.Add(grid ?? new Block { Kind = "empty", Title = "Nothing for this choice" });
            }
            Under(view.Blocks, Section("Put to good use by"), GearPutToGoodUse(input));
            if (items != null || HasBaseline(input, LocalTotals.StatsKind))
            {
                Began(view, input, LocalTotals.StatsKind, CraftedBaseline);
                double since(string stat) => StatLayers(input, stat)?.since ?? 0;
                // with a filter chosen the gear follows it (the per-item counters are there); upgrades are counted without a kind, so they stay out and the line says so.
                // Without per-item counters the block cannot follow: it says plainly that it shows all
                var on = filter != null && FilterOn(state, CraftFilter);
                Block hero;
                if (on && items != null)
                    hero = SinceHero((items.Where(kv => filter.Pass(GearFacetItem(input, kv.Key, 0))).Sum(kv => kv.Value.since), "gear crafted", "gear crafted"));
                else hero = SinceHero((GearKinds.Sum(k => since(k.stat)), "gear crafted", "gear crafted"), (since("Upgrades"), "upgrade made", "upgrades made"));
                if (hero != null && on) hero.Note = items != null ? FollowsFilter + " Upgrades are not counted by kind." : "Shows all gear, not the filter above.";
                Add(view, hero);
                // the kinds and the items made since install are the tiles' own solid numbers ("5 + 1"): a second list of them did not fit the plate
                if (!view.Blocks.Any(b => ZoneOf(b) == SrcPc)) Add(view, on && items != null ? SinceNothingFor() : SinceInstallEmpty(input, null, null));
            }
            Plate(view, "title:smith", FellowScope(input));
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
                people.Add(Person(f.PlayerName, items.Select(k => new Block { Kind = "item", Icon = "item:" + k, Title = Who(input, k), Src = SrcFellows, Source = TagFellows })));
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
            var layers = PlantedLayers(input);
            return layers == null ? Placed(input, "planted") : layers.ToDictionary(kv => kv.Key, kv => kv.Value.faded + kv.Value.solid);
        }

        static void Farming(PanelInput input, PanelView view)
        {
            view.Heading = "Farming";
            var layers = PlantedLayers(input);
            var planted = PlantedTotals(input);
            double sown = planted.Values.Sum(), picked = C(input, "HarvestCrop");
            // the twin (Deeds twins): planted since install is Hearthwoven's own exact count (PlantEasily grids included), picked
            // since install is the game's counter minus its baseline (complete: one per plant, anyone's field or wild). A character
            // that started with Hearthwoven has no planted before install: its planting is the ember zone's alone
            var twin = layers != null;
            double plantedSince = twin ? layers.Values.Sum(l => l.solid) : 0;
            var allSince = twin && plantedSince > 0 && layers.Values.All(l => l.faded <= 0);
            var hero = Hero((sown > 0 && !allSince ? N(sown) : null, "planted", SrcCharacter, null), (picked > 0 ? N(picked) : null, "picked", SrcCharacter, null));
            // the hero keeps the sum (two layers at hero size do not fit beside picked; the tiles carry them)
            Add(view, hero);

            // per crop: what you planted of it and what you picked of its item (m_pickableStats); trees are not crops
            var crops = new Dictionary<string, double>();
            var notCrops = new Dictionary<string, double>();
            var cropLayers = new Dictionary<string, (double faded, double solid)>();
            foreach (var kv in planted)
            {
                var c = CropOf(input, kv.Key); var key = c ?? kv.Key;
                if (c != null) Bump(crops, c, kv.Value); else Bump(notCrops, kv.Key, kv.Value);
                if (layers != null && layers.TryGetValue(kv.Key, out var l)) { cropLayers.TryGetValue(key, out var o); cropLayers[key] = (o.faded + l.faded, o.solid + l.solid); }
            }
            // a tile's planted number in its two layers (only when it has both; the tile's big number is the game's picked)
            void Layered(Block grid) { foreach (var i in grid?.Items ?? new List<Block>()) if (cropLayers.TryGetValue(i.Id, out var l) && l.faded > 0 && l.solid > 0) { i.Faded = N(l.faded); i.Solid = N(l.solid); } }
            // every crop you planted: picked big and planted small on its tile (m_pickableStats has the picks per item)
            double Got(string crop) => input.Harvested != null && input.Harvested.TryGetValue(crop, out var v) ? v : 0;
            var cropPicks = CounterLayers(input, LocalTotals.PickablesKind, input.Harvested, k => !k.StartsWith("$"));   // picked per crop in two layers; null = no baseline
            var cropGrid = ItemGrid(crops.Select(kv => (kv.Key, Got(kv.Key), kv.Value)), k => Who(input, k), k => "item:" + k, SrcCharacter, "planted");
            Layered(cropGrid);
            // the grid keeps its tiles; each crop tile carries two thin paired bars, planted (its faded share = counted before
            // Hearthwoven) and picked (its faded share = picked before install), each with its number, on the crop's own scale (the longer of the two fills the bar: one
            // crop of 128,450 picks would leave every other crop's bars as slivers): how much came back, at a glance
            if (cropGrid != null)
            {
                cropGrid.Kind = "cropgrid"; cropGrid.Text = PickedScope;
                foreach (var i in cropGrid.Items)
                {
                    crops.TryGetValue(i.Id, out var sown1); var got = Got(i.Id); var max = Math.Max(1, Math.Max(sown1, got));
                    cropLayers.TryGetValue(i.Id, out var l);
                    i.Kind = "crop";
                    var pickedPair = new Block { Kind = "pair", Tone = PickedWord, Title = PickedWord, Value = N(got), Fraction = (float)(got / max), Colour = PickedColour, Src = SrcCharacter, Source = TagOfSrc(SrcCharacter) };
                    if (cropPicks != null && cropPicks.TryGetValue(i.Id, out var pl) && pl.before + pl.since > 0)
                    {
                        pickedPair.Fraction2 = (float)(pl.before / (pl.before + pl.since));
                        if (pl.before > 0 && pl.since > 0) { pickedPair.Faded = N(pl.before); pickedPair.Solid = N(pl.since); }
                    }
                    i.Items = new List<Block> {
                        new Block { Kind = "pair", Tone = PlantedWord, Title = PlantedWord, Value = N(sown1), Fraction = (float)(sown1 / max), Colour = PlantedColour, Src = i.Src, Source = i.Source,
                                    Fraction2 = i.Faded != null ? (float)(l.faded / (l.faded + l.solid)) : 0, Faded = i.Faded, Solid = i.Solid },
                        pickedPair };
                }
                if (cropGrid.Items.Any(i => i.Faded != null || i.Items.Any(p => p.Fraction2 > 0))) cropGrid.Note = FadedKey;
            }
            // fix4: the crops picked add up to the hero's number ON THE LIST: what you picked of plants you never planted (wild ones, a fellow's field) is the list's last tile,
            // "Other plants", so 155 + 71 + 66 + 20 reads as the 312 picked; the berries, mushrooms, honey and sap are other counters and say so under their own heading
            var otherPlants = picked - crops.Keys.Sum(Got);
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
            var trees = Strip(notCrops.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, Who(input, kv.Key), kv.Value, "piece:" + kv.Key)), SrcCharacter);
            Layered(trees);
            Under(view.Blocks, Section("Also planted"), trees);
            Under(view.Blocks, Section(AlsoHarvestedTitle), Strip(AlsoHarvested.Select(a => (a.stat, a.title, C(input, a.stat), a.icon)), SrcCharacter));
            if (twin)
            {
                // since install, this PC: planted (counted exactly), picked (the game's counter minus its baseline) and the crops picked
                // since (what else was harvested stays with the character: the page has to fit the plate)
                Began(view, input, PlacedBaseline, LocalTotals.StatsKind, LocalTotals.PickablesKind);
                double since(string stat) => StatLayers(input, stat)?.since ?? 0;
                Add(view, SinceHero((plantedSince, "planted", "planted"), (since("HarvestCrop"), "picked", "picked")));
                var wanted = SinceOnly(cropPicks).Where(kv => crops.Count == 0 || crops.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
                Under(view.Blocks, Section("Crops picked"), Ranking(wanted, k => Who(input, k), k => "item:" + k, SrcPc, top: PieceTop, columns: 2));
                if (!view.Blocks.Any(b => ZoneOf(b) == SrcPc)) Add(view, SinceInstallEmpty(input, null, null));
            }
            Plate(view, "title:farmer", FellowScope(input));
        }

        // ---------- Fishing: hooked, caught, lost and caught by quality | caught per kind ----------

        public const string PlacementNote = "Placements: a piece you move counts again.";
        public const string FishHeldTitle = "Fish picked up";
        /// <summary>live-polish (Joost's book in game: 20 hooked, 8 got away, 0 caught, yet fish in his hands): the game books a catch only when
        /// the line is reeled in (FishingFloat.Catch: FishCaught and the fish's record); a fish grabbed by hand, taken from a trap or handed over
        /// is booked only as picked up (m_itemPickupStats, by its first holder).</summary>
        public const string FishHeldNote = "Every fish that came into your hands: reeled in, grabbed by hand, from a trap or from a fellow player. Only a reeled-in fish counts as caught.";

        static void Fishing(PanelInput input, PanelView view)
        {
            view.Heading = "Fishing";
            var hcl = new[] { ("FishHooked", "Hooked", "#6f8fa8"), ("FishCaught", "Caught", "#e8a948"), ("FishLost", "Got away", "#b8432e") };
            var left = new List<Block>();
            // the game's three counters, each booked at its own moment (FishingFloat.cs): the bite (hooked), the line reeled in (caught), the
            // fish lost for want of stamina (got away). They need not add up and no remainder is named (fish-held, Joost + Codex: 20 hooked and
            // 8 got away do not prove 12 of anything). Beside a bite, a counter at 0 stays: "Caught 0" says no fish was reeled in
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
            // the twin (Deeds twins): the game's fishing counters are complete, so since install = each now minus its baseline; the fish
            // per kind come from the picked-plants record, in two layers on the character's tiles
            var perKind = CounterLayers(input, LocalTotals.PickablesKind, input.Harvested, k => k.StartsWith("$"));
            // the kinds the game names, each from the same catch record (the line reeled in); their sum is the title, no remainder tile (fish-held)
            var named = kinds.Values.Sum(); var caughtAll = C(input, "FishCaught");
            var fishGrid = ItemGrid(kinds, k => Who(input, k), k => "item:" + k, SrcCharacter);
            if (fishGrid != null && perKind != null) LayerTiles(fishGrid, perKind);   // the tiles say "since install" themselves
            Under(right, Section("Fish caught", named, SrcCharacter), fishGrid);   // the total in the title (r4deeds-fishing)
            // the fish picked up (the game's first-holder record of fish items), shown when it holds more than the catches: fish that came in
            // another way than the line (live-polish). Never added to caught
            var held = (input.ItemsPickedUp ?? new Dictionary<string, float>()).Where(kv => kv.Key.StartsWith("$animal_fish", StringComparison.Ordinal) && kv.Value > 0)
                                                                              .ToDictionary(kv => kv.Key, kv => (double)kv.Value);
            if (held.Values.Sum() > Math.Max(named, caughtAll))
            {
                Under(right, Section(FishHeldTitle, held.Values.Sum(), SrcCharacter), ItemGrid(held, k => Who(input, k), k => "item:" + k, SrcCharacter));
                right.Add(new Block { Kind = "note", Text = FishHeldNote });
            }
            Columns(view, left, right);
            if (HasBaseline(input, LocalTotals.StatsKind))
            {
                Began(view, input, LocalTotals.StatsKind, LocalTotals.PickablesKind);
                double since(string stat) => StatLayers(input, stat)?.since ?? 0;
                Add(view, SinceHero((since("FishCaught"), "fish caught", "fish caught"), (since("FishHooked"), "hooked", "hooked"), (since("FishLost"), "got away", "got away")));
                if (!view.Blocks.Any(b => ZoneOf(b) == SrcPc)) Add(view, SinceInstallEmpty(input, null, null));
            }
            SkillStrip(input, view, SkillHeading, new[] { "Fishing" });
            Plate(view, "title:fisher", FellowScope(input));
        }

        // ---------- Taming: the game's three counters (no count per kind of creature exists) and the title band ----------

        static void Taming(PanelInput input, PanelView view)
        {
            view.Heading = "Taming";
            Add(view, Counts(new[] { ("CreatureTamed", "Tamed", "vocab:tame-tamed"), ("TamedPetting", "Petted", "vocab:tame-petted"), ("TamedCommand", "Commands given", "vocab:tame-command") }
                                 .Select(s => (s.Item1, s.Item2, C(input, s.Item1), s.Item3, (string)null)), SrcCharacter));   // Codex's care pictures (no game icon exists)
            var care = view.Blocks.LastOrDefault(b => b.Kind == "counts"); if (care != null) care.Tone = "framed";   // thin single frames, each picture in its own framed square
            if (C(input, "CreatureTamed") > 0) view.Blocks.Add(new Block { Kind = "note", Text = TamedNote });   // honest: the game books a tame to the creature's owner
            // the twin (Deeds twins): petting and commands are complete counters, so since install = each now minus its baseline;
            // tamed is the owner's alone (TamedNote), so it has no twin
            var twin = HasBaseline(input, LocalTotals.StatsKind);
            if (twin)
            {
                Began(view, input, LocalTotals.StatsKind);
                double since(string stat) => StatLayers(input, stat)?.since ?? 0;
                Add(view, SinceHero((since("TamedPetting"), "petted", "petted"), (since("TamedCommand"), "command given", "commands given")));
            }
            LedGroup(view, input);    // 0.6: animals led (Chapters/LedModel.cs)
            // 0.6: born in your care (Chapters/CargoModel.cs: counted on this PC since install) and born near you (the server's book): fix3-rest puts them side by side
            // as one compact pair in the since-install zone (below the fold, the server's block was never seen); each keeps its own source mark
            var careCol = Stretch(v => BornGroup(v, input)); var nearCol = Stretch(v => BornNearGroup(v, input));
            if (careCol.Count > 0 && nearCol.Count > 0) view.Blocks.Add(new Block { Kind = "columns", Tone = PairTone, Items = new List<Block> { new Block { Kind = "column", Items = careCol }, new Block { Kind = "column", Items = nearCol } } });
            else { view.Blocks.AddRange(careCol); view.Blocks.AddRange(nearCol); }
            if (twin && !view.Blocks.Any(b => ZoneOf(b) == SrcPc)) Add(view, SinceInstallEmpty(input, null, null));
            Plate(view, "title:tamer", FellowScope(input));
        }
    }
}
