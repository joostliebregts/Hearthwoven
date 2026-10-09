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
    /// Both are Hearthwoven's own counts since install (Src "pc"). No new block kinds: hero, composition, itemgrid, note.
    /// </summary>
    public static partial class PanelModel
    {
        public const string CargoCarried = "Cargo carried", AverageLoad = "Usually aboard", BornTitle = "Born in your care";
        /// <summary>The honest line under Cargo carried: whose cart or ship, and that a winding route is longer than the straight line counted.</summary>
        public const string CargoLine = "at the helm or pulling a cart · straight line, so the real figure is higher";
        /// <summary>fix4-rest: the one line under the Cargo carried bar says first what the unit is in plain words, then the honest line ("item-km: 1 item carried 1 km · at the helm ...").</summary>
        public static string CargoNote(double total) => CargoUnit(total) + ": " + CargoUnitWords(total) + " · " + CargoLine;
        public const string ItemMetresUnit = "item-metres", ItemKmUnit = "item-km";

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

        /// <summary>What the unit means, said once under the number it measures (fix2-rest, the review: "item-km" is jargon): one item carried one km (or one metre).</summary>
        public static string CargoUnitWords(double total) => total >= 1000 ? "1 item carried 1 km" : "1 item carried 1 m";

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
            foreach (var part in b.Items) { part.Value = CargoNumber(items.First(kv => kv.Key == part.Id).Value, total); part.Colour = Legible(part.Colour); }
            return b;
        }

        /// <summary>
        /// Per item, how much was aboard on average and over how far (item-metres / the distance travelled with some of it aboard):
        /// "550 · Iron Scrap · 2.6 km aboard". The big number is the average load, never a count of trips or items moved, which the
        /// mod cannot know. null = no distances (a copy from before the distances were kept).
        /// </summary>
        /// <summary>The line under an average load (live-polish, review 5: "over 4.1 km in all" read as a second, unrelated number): how far
        /// that item was carried in all, said as a verb, "carried 4.1 km".</summary>
        public static string LoadLine(double metres) => "carried " + Distance(metres);

        public static Block CargoLoads(PanelInput input)
        {
            var stretch = input.Events?.CargoStretch ?? new Dictionary<string, float>();
            var tint = ItemTint(input);
            var tiles = new List<Block>();
            foreach (var kv in CargoItems(input))
            {
                stretch.TryGetValue(kv.Key, out var s);
                var avg = Cargo.AverageAboard(kv.Value, s);
                if (avg <= 0) continue;
                tiles.Add(new Block { Kind = "item", Id = kv.Key, Icon = "item:" + kv.Key, Title = Who(input, kv.Key), Value = N(avg), Value2 = LoadLine(s), Src = SrcPc, Source = TagMeasured, Colour = Legible(tint(kv.Key)) });   // live-polish (review 5): read with the heading as one sentence, "Usually aboard: 100 Wood, carried 4.1 km"
                if (tiles.Count == TileTop) break;
            }
            return tiles.Count == 0 ? null : new Block { Kind = "itemgrid", Src = SrcPc, Source = TagMeasured, Items = tiles };
        }

        /// <summary>
        /// The Cargo carried section of Voyages > Cargo: the item-metres as the hero with the bar by item beside the average loads (and, when
        /// the ledger holds one, Heavy Keel's heaviest load as one more tile of the same grid, named as a record), and one honest line that
        /// says first what the unit is. Nothing when nothing was carried (no ghosts).
        /// </summary>
        static void CargoGroup(PanelView view, PanelInput input, bool dayWindow = false)
        {
            var total = CargoItemMetres(input);
            var keel = dayWindow ? null : HeavyKeelTile(input);   // a record, not a count of the window
            if (total <= 0 && keel == null) return;
            if (total <= 0) { view.Blocks.Add(Section(HeaviestLoad)); view.Blocks.Add(new Block { Kind = "itemgrid", Src = SrcPc, Source = TagMeasured, Items = new List<Block> { keel } }); return; }   // a best without the metres behind it: the record alone
            view.Blocks.Add(Section(CargoCarried));
            var bar = CargoComposition(input);
            var loads = CargoLoads(input);
            var averages = loads != null;
            if (keel != null) { if (loads == null) loads = new Block { Kind = "itemgrid", Src = SrcPc, Source = TagMeasured, Items = new List<Block>() }; loads.Items.Add(keel); }
            var hero = Hero((CargoNumber(total, total), CargoUnit(total), SrcPc, null));
            if (hero != null) hero.Tone = Compact;   // one modest line: the cargo sits above the fold (fix2-rest), the page's hero is the km sailed
            if (bar != null) bar.Tone = Thin;
            Columns(view,
                Stretch(v => { Add(v, hero); if (bar != null) { bar.Title = null; bar.Value = null; v.Blocks.Add(bar); } v.Blocks.Add(new Block { Kind = "note", Text = CargoNote(total) }); }),   // the unit and the honest line under the bar they qualify: above the fold with it
                Stretch(v => { if (loads != null) { if (averages) v.Blocks.Add(Section(AverageLoad)); v.Blocks.Add(loads); } }));
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
        /// <summary>The scope of the server's cargo book (fix4-rest): the server counts for itself, from the first cargo it saw ("counted by the server since 8 Oct").</summary>
        public static string ServerScope(PanelInput input, ServerBook.Shared book) =>
            "counted by the server" + (book != null && book.CargoFrom.HasValue ? " since " + ZoneDate(Local(input, book.CargoFrom.Value), Local(input, input.NowUtc)) : "");

        /// <summary>One table of the server's book as a thin bar by item, in the unit of <paramref name="scale"/>; null when empty.</summary>
        static Block ServerItemBar(PanelInput input, string title, Dictionary<string, double> items, double scale)
        {
            var parts = (items ?? new Dictionary<string, double>()).Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (parts.Count == 0) return null;
            var b = Composition(title, parts, k => Who(input, k), Look(new Dictionary<string, (string colour, string pattern)>()), SrcServer, tint: ItemTint(input));
            if (b == null) return null;
            b.Value = CargoNumber(parts.Values.Sum(), scale); b.Tone = Thin;
            foreach (var part in b.Items) { if (parts.TryGetValue(part.Id, out var v)) part.Value = CargoNumber(v, scale); part.Colour = Legible(part.Colour); }
            return b;
        }

        /// <summary>
        /// Voyages > Cargo, after Cargo carried (0.6, the server's book; FEASIBILITY-06 option A): what you loaded into a ship or cart
        /// that someone took out elsewhere (loaded) and what you took out that someone loaded elsewhere (unloaded), item-km per item,
        /// never summed. fix4-rest: its own zone (Id "server") whose heading says who counted and since when; outside your character's and this PC's
        /// zones, since the server counted it. Nothing when the server has nothing for you (no ghosts).
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
                var h = Hero((CargoNumber(total, scale), CargoUnit(scale) + " " + word, SrcServer, null)); if (h != null) h.Tone = Compact;
                return h;
            }
            var inside = new PanelView();
            Columns(inside, Stretch(v => { Add(v, Side(sent, "loaded")); var bar = ServerItemBar(input, null, book.Sent, scale); if (bar != null) { bar.Value = null; Add(v, bar); } }),
                            Stretch(v => { Add(v, Side(delivered, "unloaded")); var bar = ServerItemBar(input, null, book.Delivered, scale); if (bar != null) { bar.Value = null; Add(v, bar); } }));
            inside.Blocks.Add(new Block { Kind = "note", Text = ServerCargoLine(input) });
            if (CargoItemMetres(input) > 0) inside.Blocks.Add(new Block { Kind = "note", Text = ServerVersusCarried(input) });   // why loaded can be more than carried
            view.Blocks.Add(new Block { Kind = "zone", Id = SrcServer, Title = ServerCargoTitle, Text = ServerScope(input, book), Items = inside.Blocks });
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
            if (view.Blocks.Count == 0) view.Blocks.Add(day ? DayEmpty(input, w) : SinceInstallEmpty(input, "cargo", "what you carry in ships and carts shows up"));
            // a day window leaves out what has no days, and says so once: the server's book (and Heavy Keel, a record, not a count)
            if (day && serverBook) view.Blocks.Add(new Block { Kind = "note", Text = NoDaysServerBook });
            Plate(view, "vocab:cargo-mark", FellowScope(input));
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
            view.Blocks.Add(new Block { Kind = "section", Title = input.IsSelf ? BornNearTitle : "Born near " + Name(input), Icon = "vocab:young-creature", Value = N(born.Values.Sum()), Src = SrcServer, Source = TagServer });
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
            view.Blocks.Add(new Block { Kind = "section", Title = title, Icon = "vocab:young-creature", Value = N(born.Values.Sum()), Src = SrcPc, Source = TagMeasured });
            view.Blocks.Add(BornStrip(input, born, SrcPc));
            view.Blocks.Add(new Block { Kind = "note", Text = BornLine(input) });
        }

        /// <summary>The young born, one entry per kind of creature (its grown animal's picture, the count, the name), most first.</summary>
        static Block BornStrip(PanelInput input, Dictionary<string, double> born, string src) =>
            Strip(born.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (kv.Key, Who(input, kv.Key), kv.Value, BornIcon(input, kv.Key))), src);
    }
}
