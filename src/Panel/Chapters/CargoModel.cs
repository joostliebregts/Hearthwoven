using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.6, two things only this PC can see (work/hearthwoven-visual-vocabulary/FEASIBILITY-06.md):
    /// Cargo carried (Voyages > Cargo, and Company > Together as a category): item-metres of what lay in the ship you
    /// steered or the cart you pulled, per item (SessionEvents.CargoMeters, Cargo.Add); and Born in your care (Deeds >
    /// Taming): tamed animals born or hatched within 40 m of you while your PC hosted them (SessionEvents.BornInCare).
    /// Both are Hearthwoven's own counts since install (Src "pc"). Block kinds: hero, composition, itemgrid, note, and 0.8.5's sum.
    /// </summary>
    public static partial class PanelModel
    {
        public const string CargoCarried = "Cargo carried", AverageLoad = "Usually aboard", BornTitle = "Born in your care";
        public const string ItemMetresUnit = "item-meters", ItemKmUnit = "item-km";

        /// <summary>0.8.5 (Joost: say what counts, in one line): what Cargo carried counts, in the section's own line. The 10 s samples and the
        /// teleport that counts nothing stay in Cargo.cs; the straight line is said once, in the sum's caption.</summary>
        public static string CargoCounts(PanelInput input) =>
            "Counts what is in the ship's or cart's own hold, only while " + (input.IsSelf ? "you steer or pull it." : Name(input) + " steers or pulls it.");
        /// <summary>The same line on Everyone's Cargo, for every player's own count.</summary>
        public const string CargoCountsGroup = "Counts what is in the ship's or cart's own hold, only while one of you steers or pulls it.";

        /// <summary>What the unit is, the sum's caption first: "item-km: items aboard × km, on a straight line." (the line alone when there is no sum).
        /// The book's one explanation of item-km: Company > Together's cargo chip says it in these words too (0.8.5).</summary>
        public static string CargoUnitLine(double total) => CargoUnit(total) + ": items aboard × " + (total >= 1000 ? "km" : "meters") + ", on a straight line.";

        /// <summary>0.8.5 the block kind of the small sum under a cargo bar (VoyagesHallUi.CargoSum, the preview's cargoSum).</summary>
        public const string SumKind = "sum";
        public const string SumLoadIcon = "vocab:cargo-mark", SumDistanceIcon = "vocab:list-sailing";

        /// <summary>
        /// 0.8.5 item-km as a small sum (Joost's wish, variants A and C of proto/5-item-km, combined): "[crate] 100 × [ship] 4.1 km = 410
        /// item-km" for one item, from the player's own numbers. The load is how many of the item were aboard on average (Cargo.AverageAboard:
        /// its item-metres divided by the distance it travelled with some of it aboard), the distance is that distance, the result is its part
        /// of the bar; load × distance is the item-metres exactly, so the sum holds before rounding. It is an average over the km, never one
        /// trip: the mod keeps no trips. Where the rounding of the load (a whole number) and the distance (as Km rounds it) would make the sum
        /// read wrong (100 × 3.7 km beside 369), the distance gets a decimal more, then the load, until the sum as shown holds. The two terms;
        /// null without a distance (a copy from before the distances were kept): never guessed.
        /// </summary>
        static List<Block> SumTerms(double itemMetres, double stretch, double total)
        {
            if (!(itemMetres > 0) || !(stretch > 0) || !(total > 0)) return null;
            var km = total >= 1000; var distance = km ? stretch / 1000 : stretch; var first = km && distance < 100 ? 1 : 0;
            var avg = Cargo.AverageAboard(itemMetres, stretch);
            var result = CargoNumber(itemMetres, total);
            string load = null, far = null;
            foreach (var (a, d) in new[] { (0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2), (2, 2) })
            {
                load = Fixed(avg, a); far = Fixed(distance, first + d);
                if (CargoNumber(ParseCount(load) * ParseCount(far) * (km ? 1000 : 1), total) == result) break;
            }
            return new List<Block> { new Block { Kind = "term", Icon = SumLoadIcon, Value = load, Src = SrcPc, Source = TagMeasured },
                                     new Block { Kind = "term", Icon = SumDistanceIcon, Value = far + (km ? " km" : " m"), Src = SrcPc, Source = TagMeasured } };
        }

        /// <summary>A: the one sum under the bar, for the item carried most, its caption saying what the unit is and whose item it is
        /// (<paramref name="most"/>: "the item you carried most"). null without a distance.</summary>
        public static Block CargoSum(string id, string item, double itemMetres, double stretch, double total, string most)
        {
            var terms = SumTerms(itemMetres, stretch, total);
            if (terms == null) return null;
            return new Block
            {
                Kind = SumKind, Id = id, Title = CargoUnit(total), Value = CargoNumber(itemMetres, total), Src = SrcPc, Source = TagMeasured, Items = terms,
                Text = CargoUnitLine(total) + " For " + item + ", " + most + ", " + terms[0].Value + " aboard on average:",
            };
        }

        /// <summary>0.8.5 C: Usually aboard as sums, the block kind (VoyagesHallUi.CargoSumRows, the preview's cargoSumRows) and its closing word.</summary>
        public const string SumRowsKind = "sumrows", SumRowKind = "sumrow", SumTogether = "together";

        /// <summary>
        /// C: Usually aboard as a sum per item, so the reader sees item-km add up: a row per item, largest first as the bar has them, in the
        /// bar's colour, its picture and name, load × distance = its part (SumTerms; an item without a distance gives its part alone). Compact:
        /// as many rows as a half island holds (IslandHalfRows), the rest in one "Other (n kinds)" row with their part together (five items: five
        /// rows, an Other row for one would save nothing). The block's Value is the hero's total ("together 640 item-km"), so the rows add up
        /// to it (each rounded as the bar's legend is). null when no item has a distance (a copy from before the distances were kept).
        /// </summary>
        static Block CargoSumRows(PanelInput input, Block bar, double total)
        {
            var items = CargoItems(input);
            var stretch = input.Events?.CargoStretch ?? new Dictionary<string, float>();
            var shown = items.Count > IslandHalfRows + 1 ? items.Take(IslandHalfRows).ToList() : items;
            var rows = new List<Block>();
            foreach (var kv in shown)
            {
                stretch.TryGetValue(kv.Key, out var s);
                rows.Add(new Block { Kind = SumRowKind, Id = kv.Key, Icon = "item:" + kv.Key, Title = Who(input, kv.Key), Colour = bar?.Items?.FirstOrDefault(p => p.Id == kv.Key)?.Colour,
                                     Value = CargoNumber(kv.Value, total), Items = SumTerms(kv.Value, s, total), Src = SrcPc, Source = TagMeasured });
            }
            if (!rows.Any(r => r.Items != null)) return null;
            var rest = items.Skip(shown.Count).ToList();
            if (rest.Count > 0)
                rows.Add(new Block { Kind = SumRowKind, Id = FoldId, Title = FoldLabel(rest.Count), Colour = BarOtherColour, Value = CargoNumber(rest.Sum(kv => kv.Value), total), Src = SrcPc, Source = TagMeasured });
            return new Block { Kind = SumRowsKind, Title = CargoUnit(total), Value = CargoNumber(total, total), Text = SumTogether, Items = rows, Src = SrcPc, Source = TagMeasured };
        }

        /// <summary>A number with <paramref name="decimals"/> places, grouped as every count is ("1 234.5").</summary>
        static string Fixed(double v, int decimals) => decimals <= 0 ? N(v) : Math.Round(v, decimals).ToString("#,0." + new string('0', decimals), Grouped);

        // ---------- cargo ----------

        /// <summary>An item's colour as the cargo bar, its key and its tiles draw it (fix2-rest: Coal's own tint is near black, which vanishes on the dark plate):
        /// a near-black tint (below a luminance of 0.05) is mixed toward a warm grey until it reads against the plate (about 3:1), any other is left as it is.</summary>
        public static string Legible(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 7 || hex[0] != '#') return hex;
            double Ch(int at) { var v = Convert.ToInt32(hex.Substring(at, 2), 16) / 255.0; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
            double lum = 0.2126 * Ch(1) + 0.7152 * Ch(3) + 0.0722 * Ch(5);
            if (lum >= 0.05) return hex;
            int Mix(int at) => (int)Math.Round(Convert.ToInt32(hex.Substring(at, 2), 16) * 0.35 + Convert.ToInt32(CargoDarkTo.Substring(at, 2), 16) * 0.65);
            return "#" + Mix(1).ToString("x2") + Mix(3).ToString("x2") + Mix(5).ToString("x2");
        }
        const string CargoDarkTo = "#8a8278";

        /// <summary>One player's cargo: item-metres over every item (what they carried at the helm or pulling a cart).</summary>
        public static double CargoItemMetres(PanelInput p) => p?.Events == null ? 0 : p.Events.CargoMeters.Where(kv => kv.Value > 0).Sum(kv => (double)kv.Value);

        /// <summary>The unit a total reads in: item-metres up to 1 000, item-km above (no k or M: the word list keeps km as the one abbreviation).</summary>
        public static string CargoUnit(double total) => total >= 1000 ? ItemKmUnit : ItemMetresUnit;

        /// <summary>A value of item-metres in the unit chosen for <paramref name="scale"/> (the total it belongs to), number only: "2 070", "60", "0.3", "840".</summary>
        public static string CargoNumber(double itemMetres, double scale)
        {
            if (scale < 1000) return N(itemMetres);
            var km = itemMetres / 1000.0;
            if (scale >= 100000 && km >= 1) return N(Math.Round(km));   // fix4-rest: "1 430 Iron Scrap" beside "4 Wood", not "4.0": whole km once the total is 100 or more
            return km < 10 ? km.ToString("0.0", Inv) : N(km);
        }

        /// <summary>"2 070 item-km" / "840 item-metres": a value with its unit, the unit of <paramref name="scale"/>.</summary>
        public static string CargoText(double itemMetres, double scale) => CargoNumber(itemMetres, scale) + " " + CargoUnit(scale);

        static string Distance(double meters) => meters < 1000 ? N(meters) + " m" : Km(meters);

        /// <summary>What lay aboard, by item: item-metres, largest first (ties by token).</summary>
        static List<KeyValuePair<string, double>> CargoItems(PanelInput input) =>
            (input.Events?.CargoMeters ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value))
                .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();

        /// <summary>
        /// "What did I carry?": one composition by item, a part per item in its share of the item-metres (a part's Value = its
        /// item-metres in the total's unit, the bar's own Value the total, which the page's hero says too). null = nothing carried.
        /// </summary>
        public static Block CargoComposition(PanelInput input)
        {
            var items = CargoItems(input);
            if (items.Count == 0) return null;
            var total = items.Sum(kv => kv.Value);
            var b = Composition(null, items.ToDictionary(kv => kv.Key, kv => kv.Value), k => Who(input, k), Look(new Dictionary<string, (string colour, string pattern)>()), SrcPc, tint: ItemTint(input));
            if (b == null) return null;
            b.Value = CargoNumber(total, total);
            double Of(string id) => items.First(kv => kv.Key == id).Value;
            foreach (var part in b.Items) { part.Value = CargoNumber(part.Id == FoldId ? part.Items.Sum(x => Of(x.Id)) : Of(part.Id), total); part.Colour = Legible(part.Colour); }
            return b;
        }

        /// <summary>
        /// The Cargo carried section of Voyages > Cargo: the item-metres as the hero with the bar by item and, under it, the one sum for the item
        /// carried most (or the unit's line alone); beside them Usually aboard as a sum per item that adds up to the hero (CargoSumRows) and,
        /// when the ledger holds one, Heavy Keel's heaviest load as a tile, named as a record. Nothing when nothing was carried (no ghosts).
        /// </summary>
        static void CargoGroup(PanelView view, PanelInput input, bool dayWindow = false)
        {
            var total = CargoItemMetres(input);
            var keel = dayWindow ? null : HeavyKeelTile(input);   // a record, not a count of the window
            if (total <= 0 && keel == null) return;
            if (total <= 0) { view.Blocks.Add(Section(HeaviestLoad)); view.Blocks.Add(new Block { Kind = "itemgrid", Src = SrcPc, Source = TagMeasured, Items = new List<Block> { keel } }); return; }   // a best without the metres behind it: the record alone
            view.Blocks.Add(new Block { Kind = "section", Title = CargoCarried, Text = CargoCounts(input) });   // 0.8.5: what counts, in the section's own line
            var bar = CargoComposition(input);
            var rows = CargoSumRows(input, bar, total);
            // Heavy Keel's heaviest load, a record: under the sums one compact line (as the longest lead on Taming), not a tile that adds a row of
            // its own; without the sums the tile, the column's one block
            var keelBlock = keel == null ? null : rows != null ? new Block { Kind = "stat", Tone = Compact, Id = keel.Id, Icon = keel.Icon, Value = keel.Value, Title = keel.Title, Text = keel.Value2, Src = keel.Src, Source = keel.Source }
                                                               : new Block { Kind = "itemgrid", Src = SrcPc, Source = TagMeasured, Items = new List<Block> { keel } };
            // 0.8.5 A: the sum under the bar, for the item carried most (largest first); without its distance, the unit's line alone
            var top = CargoItems(input).First();
            float stretch = 0; input.Events?.CargoStretch?.TryGetValue(top.Key, out stretch);
            var sum = CargoSum(top.Key, Who(input, top.Key), top.Value, stretch, total, "the item " + (input.IsSelf ? "you" : Name(input)) + " carried most");
            var hero = Hero((CargoNumber(total, total), CargoUnit(total), SrcPc, null));
            // 0.7 rule C6: the cargo counter group's own start (LocalTotals.StartCargo), on every block this PC counted
            DateCargo(input, hero); DateCargo(input, bar); DateCargo(input, rows); DateCargo(input, sum); DateCargo(input, keelBlock);
            if (bar != null) bar.Tone = Thin;
            Columns(view,
                Stretch(v => { Add(v, hero); if (bar != null) { bar.Title = null; bar.Value = null; v.Blocks.Add(bar); } v.Blocks.Add(sum ?? new Block { Kind = "note", Text = CargoUnitLine(total) }); }),   // the unit and its sum under the bar they explain: above the fold with it
                Stretch(v => { if (rows != null) { v.Blocks.Add(Section(AverageLoad)); v.Blocks.Add(rows); } Add(v, keelBlock); }));
        }

        /// <summary>Rule C6 (0.7): every block this PC counted in a cargo group carries the cargo group's own start, so its label says "from 8 October".</summary>
        static void DateCargo(PanelInput input, Block b)
        {
            if (b == null) return;
            if (b.Src == SrcPc) b.From = StartOf(input, LocalTotals.StartCargo);
            foreach (var i in b.Items ?? new List<Block>()) DateCargo(input, i);
        }

        // ---------- cargo loaded and unloaded (the server's book) ----------

        public const string ServerCargoTitle = "Cargo loaded and unloaded";
        /// <summary>The one honest line under the two numbers: what each word means, whose, what the server counted and that it is a straight line.</summary>
        public static string ServerCargoLine(PanelInput input)
        {
            var who = input.IsSelf ? "you" : Name(input);
            return "Loaded: " + who + " put it into a ship or cart. Unloaded: " + who + " took it out. Only cargo the server saw go in and come out, in a straight line.";
        }
        /// <summary>live-polish (review 5: "1 434 item-km loaded" above "640 carried" without a reason): the server counts wherever the cargo
        /// travelled and whoever steered, Cargo carried only while this player held the helm or pulled the cart.</summary>
        public static string ServerVersusCarried(PanelInput input) =>
            "It counts wherever the cargo went and whoever steered, so it can be more than Cargo carried, which counts only while " + (input.IsSelf ? "you" : Name(input)) + " steered or pulled.";
        /// <summary>The label of the server's cargo book (rule S, 0.7): the server counts for itself, from the first cargo it saw: "Recorded by the server from 8 October".</summary>
        public static string ServerScope(PanelInput input, ServerBook.Shared book) =>
            "Recorded by the server" + (book != null && book.CargoFrom.HasValue ? " from " + RecordDate(input, book.CargoFrom.Value) : "");

        /// <summary>One table of the server's book as a thin bar by item, in the unit of <paramref name="scale"/>; null when empty.</summary>
        static Block ServerItemBar(PanelInput input, string title, Dictionary<string, double> items, double scale)
        {
            var parts = (items ?? new Dictionary<string, double>()).Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (parts.Count == 0) return null;
            var b = Composition(title, parts, k => Who(input, k), Look(new Dictionary<string, (string colour, string pattern)>()), SrcServer, tint: ItemTint(input));
            if (b == null) return null;
            b.Value = CargoNumber(parts.Values.Sum(), scale); b.Tone = Thin;
            foreach (var part in b.Items)
            {
                var v = part.Id == FoldId ? part.Items.Sum(x => parts.TryGetValue(x.Id, out var f) ? f : 0) : parts.TryGetValue(part.Id, out var one) ? one : -1;   // the folded part: its kinds together
                if (v >= 0) part.Value = CargoNumber(v, scale);
                part.Colour = Legible(part.Colour);
            }
            return b;
        }

        /// <summary>
        /// Voyages > Cargo, after Cargo carried (0.6, the server's book; FEASIBILITY-06 option A): what you loaded into a ship or cart
        /// that someone took out elsewhere (loaded) and what you took out that someone loaded elsewhere (unloaded), item-km per item,
        /// never summed. fix4-rest: its own part, whose heading says who counted and since when; the server counted it, apart from your character's
        /// and this PC's numbers. Nothing when the server has nothing for you (no ghosts). 0.7: a section with the server's label, no zone.
        /// </summary>
        static void ServerCargoGroup(PanelView view, PanelInput input)
        {
            var book = input.Book;
            if (book == null) return;
            double sent = book.SentTotal, delivered = book.DeliveredTotal, scale = Math.Max(sent, delivered);
            if (scale <= 0) return;
            // fix3-rest: each side is its own column, its number over its bar (the bar's own head repeated the number and cost a line)
            Block Side(double total, string word)
            {
                if (total <= 0) return null;
                var h = Hero((CargoNumber(total, scale), CargoUnit(scale) + " " + word, SrcServer, null));
                return h;
            }
            var inside = new PanelView();
            Columns(inside, Stretch(v => { Add(v, Side(sent, "loaded")); var bar = ServerItemBar(input, null, book.Sent, scale); if (bar != null) { bar.Value = null; Add(v, bar); } }),
                            Stretch(v => { Add(v, Side(delivered, "unloaded")); var bar = ServerItemBar(input, null, book.Delivered, scale); if (bar != null) { bar.Value = null; Add(v, bar); } }));
            inside.Blocks.Add(new Block { Kind = "note", Text = ServerCargoLine(input) });
            if (CargoItemMetres(input) > 0) inside.Blocks.Add(new Block { Kind = "note", Text = ServerVersusCarried(input) });   // why loaded can be more than carried
            view.Blocks.Add(new Block { Kind = "section", Title = ServerCargoTitle, RecordedFrom = ServerScope(input, book) });   // 0.7 rule S: the server's own label, no zone
            view.Blocks.AddRange(inside.Blocks);
        }

        /// <summary>
        /// Voyages > Cargo (fix3-rest: on Sailing the cargo sat below the fold, where the review never saw it): what was aboard the ship or cart you
        /// steered or pulled (this PC), Heavy Keel's heaviest load, and what you loaded and unloaded (the server's book), each with its one honest line.
        /// Sailing keeps the voyages, the route and the crew.
        /// </summary>
        static void CargoPage(PanelInput input, PanelView view, TimeWindow w = TimeWindow.SinceInstall, bool serverBook = false)
        {
            view.Heading = "Cargo";
            var day = IsDayWindow(w);
            CargoGroup(view, input, day);   // carried at the helm or pulling a cart (this PC), Heavy Keel's heaviest load as a tile beside the average loads: first in the since-install zone, above the fold
            if (!day) ServerCargoGroup(view, input);  // loaded and unloaded, the server's book: its own zone, outside the others
            // a day window leaves out what has no days, and says so once: the server's book (and Heavy Keel, a record, not a count); on an
            // empty window that line is the empty state's own (B19: one calm empty state, not two lines of advice under each other)
            var noDaysLine = day && serverBook;
            if (view.Blocks.Count == 0)
            {
                var none = day ? DayEmpty(input, w) : NothingHere(input, StartOf(input, LocalTotals.StartCargo));   // rule C6.3
                if (noDaysLine) { none.Text = NoDaysServerBook; noDaysLine = false; }
                view.Blocks.Add(none);
            }
            if (noDaysLine) view.Blocks.Add(new Block { Kind = "note", Text = NoDaysServerBook });
            Plate(view, "vocab:cargo-mark", RecordedScope(input));
        }

        // ---------- born near (the server's book) ----------

        public const string BornNearTitle = "Born near you";
        /// <summary>The line under Born near: what counted, who saw it, and that near is not bred.</summary>
        public static string BornNearLine(PanelInput input) =>
            "tamed young that appeared within 40 m of " + (input.IsSelf ? "you" : Name(input)) + ", seen by the server · near " + (input.IsSelf ? "you" : "them") + ", not bred by " + (input.IsSelf ? "you" : "them");

        /// <summary>Deeds > Taming, after Born in your care (0.6, the server's book; FEASIBILITY-06 section 2): every tamed young animal the
        /// server saw appear within 40 m of you, whoever's PC hosted it. Nothing when there is none.</summary>
        static void BornNearGroup(PanelView view, PanelInput input)
        {
            var born = (input.Book?.BornNear ?? new Dictionary<string, double>()).Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (born.Count == 0) return;
            // 0.7 rule S: the server's book is labelled by its own start (BornFrom), "Recorded by the server from 8 October"; no date: "Recorded by the server"
            var since = input.Book?.BornFrom;
            view.Blocks.Add(new Block { Kind = "section", Title = input.IsSelf ? BornNearTitle : "Born near " + Name(input), Icon = "vocab:young-creature", Value = N(born.Values.Sum()), Src = SrcServer, Source = TagServer,
                                        RecordedFrom = since.HasValue ? "Recorded by the server from " + RecordDate(input, since.Value) : "Recorded by the server" });
            view.Blocks.Add(BornStrip(input, born, SrcServer));   // fix4: one strip, not a grid of tiles: the page's since-install block fits the plate with both groups whole
            view.Blocks.Add(new Block { Kind = "note", Text = BornNearLine(input) });
        }

        // ---------- born in your care ----------

        // a young animal's adult, for its picture: the trophy the adult drops
        static readonly string[] YoungSuffixes = { "_piggy", "_cub", "_calf", "_chick", "_baby", "_young" };
        static string AdultOf(string young)
        {
            foreach (var s in YoungSuffixes) if ((young ?? "").EndsWith(s, StringComparison.OrdinalIgnoreCase)) return young.Substring(0, young.Length - s.Length);
            return young;
        }
        static string BornIcon(PanelInput input, string creature)
        {
            var t = TrophyOf(input, AdultOf(creature));
            if (t.Length == 0) t = TrophyOf(input, creature);
            return t.Length > 0 ? t : "vocab:tame-tamed";
        }

        public static double BornTotal(PanelInput p) => p?.Events == null ? 0 : p.Events.BornInCare.Where(kv => kv.Value > 0).Sum(kv => (double)kv.Value);

        /// <summary>The line under the tiles, said from whose PC it was: counted only while that PC hosted the animals (the game runs breeding on the PC that owns them).</summary>
        public static string BornLine(PanelInput input) => "while " + (input.IsSelf ? "your" : Name(input) + "'s") + " PC hosted them";

        /// <summary>"Born in your care": a section with the total, a tile per kind of creature (count, name), the honest line. Nothing when none were counted.</summary>
        static void BornGroup(PanelView view, PanelInput input)
        {
            var born = (input.Events?.BornInCare ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => (double)kv.Value);
            if (born.Count == 0) return;
            var title = input.IsSelf ? BornTitle : "Born in " + Name(input) + "'s care";
            // 0.7 rule C6: the group's own start (StartBorn); every number in the strip carries it, so the label says that date
            var from = StartOf(input, LocalTotals.StartBorn);
            view.Blocks.Add(new Block { Kind = "section", Title = title, Icon = "vocab:young-creature", Value = N(born.Values.Sum()), Src = SrcPc, Source = TagMeasured, From = from });
            var strip = BornStrip(input, born, SrcPc);
            if (strip != null) { strip.From = from; foreach (var item in strip.Items) item.From = from; }
            view.Blocks.Add(strip);
            view.Blocks.Add(new Block { Kind = "note", Text = BornLine(input) });
        }

        /// <summary>The young born, one entry per kind of creature (its grown animal's picture, the count, the name), most first.</summary>
        static Block BornStrip(PanelInput input, Dictionary<string, double> born, string src) =>
            Strip(born.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (kv.Key, Who(input, kv.Key), kv.Value, BornIcon(input, kv.Key))), src);
    }
}
