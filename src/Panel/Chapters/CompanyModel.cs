using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Company (work/hearthwoven-visual-vocabulary, gallery PAGES "Company"): Fireside (players around the hearth, threads
    /// from maker to the one who enjoyed or put to good use, thickness = count), Together (the group's whole per category,
    /// one bar coloured per player and the players x categories dot matrix; never a ranking or a combined score), Food
    /// shared (the axis: what you enjoyed from each fellow player left, what they enjoyed from you right) and Gear shared
    /// (their gear, your gear: maker, the gear as tiles, the one who put it to good use). Only players who share are shown:
    /// the saga's owner and the fellow players whose shared record arrived. Sailed together lives in Voyages.
    /// </summary>
    public static partial class PanelModel
    {
        static readonly (string id, string label)[] CompanyList =
            { ("fireside", "Fireside"), ("together", "Together"), ("food", "Food shared"), ("gear", "Gear shared") };

        /// <summary>A Together category's gold icon (V1): the deed's emblem, or the list icon of its page.</summary>
        static string TogetherIcon(string id) => id == "wood" ? "title:woodcutter" : id == "ore" ? "title:miner" : id == "built" ? "title:builder" :
                                                 id == "cooked" ? "title:cook" : id == "dealt" ? "vocab:list-damage" : id == "sailed" ? "vocab:list-sailing" : id == "cargo" ? "title:hauler" : null;

        public const string FiresideHeading = "Fireside", FiresideLine = "who made what for whom", TogetherHeading = "Together", FoodHeading = "Food shared", GearHeading = "Gear shared";
        public const string PutToGoodUse = "put to good use", Enjoyed = "enjoyed";
        /// <summary>The scope of a number read from a fellow player's shared copy: the session they last shared (SOURCES.md). Yours says "since install".</summary>
        public const string TheirSession = "their last session";
        /// <summary>fix4-rest: the Fireside's scope for a fellow's row whose shared copy is only their last session ("as they last shared it", the words the About page uses).</summary>
        public const string AsTheyShared = "as they last shared it";
        public const string AllScope = "All: yours since install, fellow players' last shared session.";
        /// <summary>"All" says what each side is: when every fellow's copy carries its since-install totals (SharedSinceInstall: damage and events since install, SOURCES.md fix 3) both sides are since install.</summary>
        public const string AllScopeSince = "All: since install, yours and fellow players'.";
        /// <summary>The scope of a number read from fellow players' shared copies: "since install" when their copy carries the since-install totals, else the session they last shared (an older sender).</summary>
        public static string TheirScope(bool sinceInstall) => sinceInstall ? SinceInstallLabel : TheirSession;
        public const string BroughtInScope = "counted the first time in a player's hands";
        public const string GearKey = "gear", FoodKey = "food";
        public const string GearPill = "gear put to good use";
        /// <summary>Under the heading of Gear shared: what the number on a tile counts (the times the gear was put to good use).</summary>
        public const string GearCountNote = "the number on a tile: times put to good use";
        /// <summary>The legend above the Fireside drawing (Joost, play-test): what the two threads and the arrow mean.</summary>
        public const string LegendFood = "food thread", LegendGear = "gear thread", LegendArrow = "the arrow points to who received it", LegendFaint = "fainter: between fellow players";
        /// <summary>One empty text per Company page (diff-05: Together is not about meals). Fireside keeps the agreed CompanyEmpty.</summary>
        public const string TogetherEmpty = "What you and your fellow players do side by side shows up here: wood and ore brought in, pieces built, dishes cooked, damage dealt, km sailed.",
                            TogetherAlone = "Fellow players appear beside you once they share too.",
                            FoodEmpty = "Meals and feast servings your fellow players enjoy of your food, and you of theirs, show up here once they share too.",
                            GearEmpty = "Gear you made that fellow players put to good use, and theirs that you wear, shows up here once they share too.";
        static string EmptyOf(string page) => page == "together" ? TogetherEmpty : page == "food" ? FoodEmpty : page == "gear" ? GearEmpty : CompanyEmpty;
        static string CountsOf(string page) => page == "together" ? "deeds done together" : page == "gear" ? "shared gear" : "shared meals";
        public const string NoGifts = "Threads appear here once a fellow player enjoys food or puts gear to good use that another made.";

        /// <summary>One person around the fire: the saga's owner or a fellow player who shares.</summary>
        public class Fellow
        {
            public string Name, Label; public bool Owner; public PanelInput Input;
        }

        /// <summary>What one player made and another enjoyed or put to good use, recorded on the receiver's PC.</summary>
        public class Gift
        {
            public string From, To, Kind, Src;   // Kind "food" | "gear"; Src of the receiver's record
            public bool SharedSince;             // the receiver's copy carries their since-install totals (a fellow's record is then since install, not their last session)
            public readonly Dictionary<string, double> Meals = new Dictionary<string, double>(), Feasts = new Dictionary<string, double>(), Gear = new Dictionary<string, double>();
            /// <summary>Food: servings (meals and feast servings); gear: times put to good use, the sum of Gear shared's tiles (fireside-route, 2026-10-09: Fireside
            /// said "1 put to good use" for an axe Gear shared showed as Bronze Axe x2; one unit on both pages, CoherenceWorld checks it).</summary>
            public double N => Kind == "food" ? Meals.Values.Sum() + Feasts.Values.Sum() : Gear.Values.Sum();
        }

        /// <summary>The owner first, then the fellow players who share, by name (never by amount).</summary>
        public static List<Fellow> FiresidePeople(PanelInput input)
        {
            var me = new Fellow { Name = Name(input), Label = input.IsSelf ? "You" : Name(input), Owner = true, Input = input };
            var rest = FellowsOf(input).GroupBy(f => f.PlayerName, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .Select(f => new Fellow { Name = f.PlayerName, Label = f.IsSelf && !input.IsSelf ? f.PlayerName + " (you)" : f.PlayerName, Input = f })
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
            return new[] { me }.Concat(rest).ToList();
        }

        /// <summary>
        /// Every gift between the people around the fire, from each receiver's own record (the owner's, and each fellow
        /// player's shared copy). Makers who do not share (or are unknown) are left out; nobody gives to themselves.
        /// </summary>
        public static List<Gift> Gifts(PanelInput input, List<Fellow> people)
        {
            var byName = people.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var ids = new Dictionary<long, string>();
            if (input.PlayerNames != null) foreach (var kv in input.PlayerNames) if (byName.ContainsKey(kv.Value ?? "")) ids[kv.Key] = byName[kv.Value].Name;
            foreach (var p in people) if (p.Input.PlayerId != 0) ids[p.Input.PlayerId] = p.Name;
            var gifts = new Dictionary<string, Gift>();
            Gift Get(string from, Fellow to, string kind)
            {
                if (string.IsNullOrEmpty(from) || !byName.TryGetValue(from, out var maker) || SameName(maker.Name, to.Name)) return null;
                var key = maker.Name + ">" + to.Name + ">" + kind;
                if (!gifts.TryGetValue(key, out var g)) gifts[key] = g = new Gift { From = maker.Name, To = to.Name, Kind = kind, Src = to.Input.IsSelf ? SrcPc : SrcFellows, SharedSince = !to.Input.IsSelf && to.Input.SharedSinceInstall };
                return g;
            }
            foreach (var to in people)
            {
                var ev = to.Input.Events; if (ev == null) continue;
                foreach (var kv in ev.AteFoodMadeBy) { var p = Split2(kv.Key); var g = Get(p[0], to, "food"); if (g != null && kv.Value > 0) Bump(g.Meals, p[1], kv.Value); }
                foreach (var kv in ev.AteFromFeastOf)
                {
                    var p = Split2(kv.Key);
                    if (!long.TryParse(p[0], NumberStyles.Integer, Inv, out var id) || id == 0 || !ids.TryGetValue(id, out var maker)) continue;
                    var g = Get(maker, to, "food"); if (g != null && kv.Value > 0) Bump(g.Feasts, p[1], kv.Value);
                }
                foreach (var kv in ev.EquippedGearMadeBy) { var p = Split2(kv.Key); var g = Get(p[0], to, "gear"); if (g != null && kv.Value > 0) g.Gear[p[1]] = kv.Value; }   // times put to good use: the count on each tile (Joost, review pack) and, summed, the gift's own count on Fireside
            }
            // the owner's own food: never more of a dish than the owner made of it (the Cooking page's rule, so Fireside, Food shared and
            // Cooking tell one number: a cooking station writes its owner on the food whoever cooked it)
            var owner = people[0];
            if (owner.Input.IsSelf)
            {
                var mine = gifts.Values.Where(g => g.Kind == "food" && SameName(g.From, owner.Name)).ToList();
                if (mine.Count > 0)
                {
                    var capped = CapToMade(mine.Select(g => (g.To, new Dictionary<string, double>(g.Meals))).ToList(), MadeOf(owner.Input));
                    for (int k = 0; k < mine.Count; k++) { mine[k].Meals.Clear(); foreach (var kv in capped[k].dishes) mine[k].Meals[kv.Key] = kv.Value; }
                }
            }
            return gifts.Values.Where(g => g.N > 0).ToList();
        }

        static void Company(PanelInput input, string page, PanelState state, PanelView view)
        {
            view.Scope = FellowScope(input);
            var people = FiresidePeople(input);
            var gifts = Gifts(input, people);
            switch (page)
            {
                case "together":
                    {
                        view.Heading = TogetherHeading;
                        var tb = Together(input, people, state, page); Add(view, tb);
                        view.Windowed = tb?.Tone == WindowedTone;   // a chosen window of the damage dealt: nothing "since install" about it
                        var wn = tb == null ? null : TogetherWindowNote(input, people, tb);
                        if (wn != null && tb.Tone != WindowedTone) view.Blocks.Insert(view.Blocks.IndexOf(tb), wn);   // the history line alone: at the top, where the other pages say it
                        else if (wn != null) view.Blocks.Add(wn);
                    }
                    if (people.Count == 1 && view.Blocks.Count > 0 && !input.Solo) view.Blocks.Add(new Block { Kind = "note", Text = TogetherAlone });   // your own total, the others still to come (singleplayer: AddPlayers' one line)
                    break;
                case "food": view.Heading = FoodHeading; Add(view, FoodAxis(input, people, gifts)); break;
                case "gear":
                    {
                        view.Heading = GearHeading;
                        Add(view, GearMadeBy(input, people, gifts));
                        break;
                    }
                default:
                    view.Heading = FiresideHeading;
                    if (people.Count > 1) Add(view, Giving(input, people, gifts));
                    if (people.Count > 1 && gifts.Count == 0) view.Blocks.Add(new Block { Kind = "note", Text = NoGifts });
                    break;
            }
            if (view.Blocks.Count == 0) view.Blocks.Add(input.IsSelf ? Empty(NothingYet, EmptyOf(page)) : SinceInstallEmpty(input, CountsOf(page), ""));   // one empty text per page
            // every Company page on the plate (slice 3), its pill naming who shares or what is counted
            // Fireside says what it is in one quiet line under the heading (Joost asked what "Fireside company" meant)
            string line = null;   // Fireside's "who made what for whom" sits in the legend row of its drawing (Giving.Note)
            Plate(view, "ui:chapter-company", string.Join(" · ", new[] { line, FellowScope(input) }.Where(x => !string.IsNullOrEmpty(x)).ToArray()) is var t && t.Length > 0 ? t : null);
            var others = people.Skip(1).Select(p => p.Name).ToList();
            PlateOf(view).Pill = page == "gear" ? GearPill : others.Count == 0 ? null :
                others.Count == 1 ? others[0] + " shares" : string.Join(", ", others.Take(others.Count - 1).ToArray()) + " and " + others[others.Count - 1] + " share";
        }

        static string Top(IDictionary<string, double> d) => d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).FirstOrDefault();

        /// <summary>
        /// "From whom to whom?" (kind giving): Items = the people (Kind "player", the owner first and Selected) and the gifts
        /// (Kind "gift": Title maker, Text receiver, Tone food | gear, Icon the item given most, Value and Count the count,
        /// Fraction = count / the largest count (the thread's thickness), Note the grateful verb, Selected = the owner is
        /// one end). Title and Text caption the two halves of the list beside the fire.
        /// </summary>
        public static Block Giving(PanelInput input, List<Fellow> people, List<Gift> gifts)
        {
            var owner = people[0].Name; var max = Math.Max(1, gifts.Select(g => g.N).DefaultIfEmpty(0).Max());
            var items = people.Select(p => new Block { Kind = "player", Id = p.Name, Title = p.Label, Icon = "person:" + p.Name, Selected = p.Owner }).ToList();
            foreach (var g in gifts.OrderBy(g => SameName(g.From, owner) || SameName(g.To, owner) ? 0 : 1).ThenBy(g => g.From, StringComparer.OrdinalIgnoreCase)
                                   .ThenBy(g => g.To, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Kind, StringComparer.Ordinal))
            {
                var all = new Dictionary<string, double>(g.Kind == "gear" ? g.Gear : g.Meals);
                if (g.Kind == "food") foreach (var kv in g.Feasts) Bump(all, kv.Key, kv.Value);
                items.Add(new Block
                {
                    Kind = "gift", Id = g.From + ">" + g.To, Title = g.From, Text = g.To, Tone = g.Kind, Icon = "item:" + Top(all), Value = N(g.N), Count = (int)Math.Round(g.N),
                    Fraction = (float)(g.N / max), Note = g.Kind == "gear" ? PutToGoodUse : Enjoyed, Selected = SameName(g.From, owner) || SameName(g.To, owner),
                    Src = g.Src, Source = TagOfSrc(g.Src), Value2 = g.Src == SrcFellows && !g.SharedSince ? AsTheyShared : null,   // a row says its own scope only where the copy is not since install (the caption says the rest)
                });
            }
            // fix4-rest (review: "since install" on every row, and a fellow's row said it though their copy might be only their last session): each list says its scope
            // once, in its caption (Value for yours, Value2 for the one among fellow players); a list whose rows differ leaves it null and each row says its own
            string ScopeOf(Gift g) => g.Src == SrcPc || g.SharedSince ? SinceInstallLabel : AsTheyShared;
            string Uniform(IEnumerable<Gift> list) { var l = list.Select(ScopeOf).Distinct().ToList(); return l.Count == 1 ? l[0] : null; }
            return new Block
            {
                Kind = "giving", Title = (input.IsSelf ? "You" : Name(input)) + " and fellow players", Text = "Among fellow players", Note = FiresideLine, Items = items,
                Value = Uniform(gifts.Where(g => SameName(g.From, owner) || SameName(g.To, owner))), Value2 = Uniform(gifts.Where(g => !(SameName(g.From, owner) || SameName(g.To, owner)))),
                Src = gifts.Count == 0 ? null : gifts.All(g => g.Src == SrcPc) ? SrcPc : SrcFellows, Source = gifts.Count == 0 ? null : gifts.All(g => g.Src == SrcPc) ? TagMeasured : TagFellows,
            };
        }

        // ---------- Together ----------

        /// <summary>The group's categories: the game's own counters (kept with each character), plus damage dealt (each
        /// player's PC). Chests are not in the shared records, so "Stowed in the hall" is not a category here.</summary>
        static readonly (string id, string label, string phrase, string note)[] Categories =
        {
            ("wood", "Wood brought in", "wood brought in together", BroughtInScope),   // the row icons: TogetherIcon
            ("ore", "Stone and ore brought in", "stone and ore brought in together", BroughtInScope),
            ("built", "Pieces built", "pieces built together", null), ("cooked", "Dishes cooked", "dishes cooked together", null),
            ("dealt", "Damage dealt", "damage dealt together", BeforeResistance), ("sailed", "Sailed", "sailed together", null),
            ("cargo", "Cargo carried", "carried together", null),   // 0.6: item-metres at the helm or pulling a cart, Hearthwoven's own count (CargoModel.cs)
        };

        static double CategoryValue(PanelInput p, string id)
        {
            switch (id)
            {
                case "wood": return BroughtInTotal(p, "wood");   // the same total as the Woodcutting bar (K1)
                case "ore": return BroughtInTotal(p, "mining");
                case "built": return BuiltCount(p);
                case "cooked": return DishesCooked(p);   // the same number as the Cooking page (its two layers, or the game's counters without a baseline)
                case "dealt": var dmg = p.DamageSinceInstall ?? p.Session; return dmg == null ? 0 : dmg.Dealt.Where(kv => !kv.Key.EndsWith("|chop") && !kv.Key.EndsWith("|pickaxe")).Sum(kv => (double)kv.Value);
                case "cargo": return CargoItemMetres(p);
                default: return C(p, "DistanceSail");
            }
        }

        /// <summary>
        /// "Together" (kind together): Items = the categories with anything in them (Kind "category": Id, Title the chip,
        /// Text the phrase after the total, Value the group's total, Note a qualifier, Selected = the one shown large), each
        /// with Items = one part per player in the fire's order (Kind "part": Id name, Icon person, Title label, Value,
        /// Fraction = share of the group, Src). Text = the matrix heading, Note = its key. No ranking, no combined score.
        /// </summary>
        public static Block Together(PanelInput input, List<Fellow> people, PanelState state, string page)
        {
            // the chosen category lives where a view switch keeps its view (PanelState.View, "Company/together/category"),
            // so a chip's click goes through Follow like any switch; the chips wrap, which the switch's top-right row cannot
            var key = Chapter.Company + "/" + page + "/category";
            var chosen = state != null && state.View.TryGetValue(key, out var v) ? v : null;
            if (people.Count < 1) return null;   // alone: your own row (diff-05), the fellow players join it once they share
            // Damage dealt alone has time windows (below): the totals since install decide which categories have anything in them
            var installValues = Categories.ToDictionary(c => c.id, c => people.Select(p => CategoryValue(p.Input, c.id)).ToList());
            var present = Categories.Where(c => installValues[c.id].Sum() > 0).Select(c => c.id).ToList();
            if (present.Count == 0) return null;
            var pickId = chosen != null && present.Contains(chosen) ? chosen : present[0];
            var wkey = Chapter.Company + "/" + page + "/" + WindowSwitch;
            var winId = state != null && state.View.TryGetValue(wkey, out var wv) ? wv : null;
            var win = TimeWindow.SinceInstall;   // the total since install is the default
            if (winId != null && Enum.TryParse(winId, out TimeWindow chosenWindow) && Enum.IsDefined(typeof(TimeWindow), chosenWindow)) win = chosenWindow;
            // a day window your own day history does not reach yet is greyed as everywhere else (HISTORY-06.md), and shows All
            var own = people.FirstOrDefault(p => p.Input.IsSelf)?.Input;
            bool Greyed(TimeWindow w) => IsDayWindow(w) && (own == null || !DayOpen(own, w));
            if (Greyed(win)) win = TimeWindow.SinceInstall;
            var windowed = pickId == "dealt" && win != TimeWindow.SinceInstall;   // the chosen window, on the damage dealt only
            var cats = new List<Block>();
            foreach (var c in Categories)
            {
                if (!present.Contains(c.id)) continue;
                var inWindow = windowed && c.id == "dealt";
                var values = inWindow ? people.Select(p => DealtInWindow(p.Input, win, input)).ToList() : installValues[c.id];
                var total = values.Sum();
                var src = c.id == "dealt" || c.id == "cargo" ? null : SrcCharacter;   // dealt and cargo are Hearthwoven's own counts on each PC
                Func<double, string> fmt = v2 => c.id == "sailed" ? Km(v2) : c.id == "cargo" ? CargoText(v2, total) : N(v2);
                // fix4-rest (review: no scope on the chip): one quiet line under the numbers says since when each category counts; Damage dealt has its window caption
                string scope = null;
                if (c.id == "cargo")
                    scope = (people.Where(p => !p.Input.IsSelf).All(p => p.Input.SharedSinceInstall) ? SinceInstallLabel : SinceInstallLabel + ", a fellow player's older copy: " + TheirSession) + " · " + CargoUnit(total) + ": " + CargoUnitWords(total) + " · straight line, so the real figure is higher";
                else if (c.id != "dealt")
                    scope = people.Count == 1 ? MadeLine(input) : "since each character was made";   // live-polish (review 5): "since you made this character", as the zones say
                cats.Add(new Block
                {
                    Kind = "category", Id = c.id, Icon = TogetherIcon(c.id), Title = c.label, Note = c.note, Value2 = scope,
                    Text = inWindow ? c.phrase + ", " + WindowLabel(win).ToLowerInvariant() : people.Count == 1 ? c.phrase.Replace(" together", "") : c.phrase, Value = fmt(total),
                    Src = src ?? (people.All(p => p.Input.IsSelf) ? SrcPc : SrcFellows), Source = TagOfSrc(src ?? (people.All(p => p.Input.IsSelf) ? SrcPc : SrcFellows)),
                    Items = people.Select((p, k) =>
                    {
                        var s = src ?? (p.Input.IsSelf ? SrcPc : SrcFellows);
                        return new Block { Kind = "part", Id = p.Name, Icon = "person:" + p.Name, Title = p.Label, Value = values[k] > 0 ? fmt(values[k]) : "", Fraction = total > 0 ? (float)(values[k] / total) : 0f, Src = s, Source = TagOfSrc(s) };
                    }).ToList(),
                });
            }
            cats.First(c => c.Id == pickId).Selected = true;
            var items = new List<Block>(cats);
            if (pickId == "dealt")   // the chips Battle uses, under the category chips: who dealt how much, since install or in a window
                items.Add(new Block
                {
                    Kind = "switch", Id = wkey, Title = !windowed && people.Count > 1 ? (people.Where(p => !p.Input.IsSelf).All(p => p.Input.SharedSinceInstall) ? AllScopeSince : AllScope) : TogetherWindowCaption,
                    Items = TogetherWindows.Select(x => new Block { Kind = "view", Id = x.ToString(), Title = WindowShort(x), Selected = x == win, Tone = Greyed(x) ? OffTone : null }).ToList(),
                });
            // the keys that act here (fix4-rest, review: the chips showed no key): the view key flips the category, the filter key the window
            if (items.Any(i => i.Kind == "switch") && !string.IsNullOrEmpty(state?.FilterKey)) items.First(i => i.Kind == "switch").KeyCap = state.FilterKey;
            return new Block { Kind = "together", Id = key, Text = "Each player's share", Items = items, Tone = windowed ? WindowedTone : null, Note = windowed ? WindowRowsNote(people) : null, KeyCap = cats.Count > 1 && !string.IsNullOrEmpty(state?.ViewKey) ? state.ViewKey : null };
        }

        // ---------- Together: Damage dealt in Battle's time windows ----------

        /// <summary>The switch under Together's category chips while Damage dealt is chosen: Battle's windows (the same set, the same short
        /// chips), the total since install the default. Everyone's windows come from the same place, each player's own log of the session
        /// they shared (the numbers Battle reads), so a fellow player's last hour is as real as yours.</summary>
        public static readonly TimeWindow[] TogetherWindows = (TimeWindow[])Enum.GetValues(typeof(TimeWindow));
        public const string WindowSwitch = "window", WindowedTone = "windowed", TogetherWindowCaption = "Damage dealt";
        public const string WindowFellowSession = "A fellow player's session is the one they last shared.";
        /// <summary>The caption above the share rows that do not follow the chosen window (fix3-rest, review: one table mixed two scopes): Damage dealt first,
        /// in the window; under this line the rest, dimmed, and why they have no window (Joost: "since install" there read as a contradiction).</summary>
        public static string WindowRowsNote(List<Fellow> people) =>
            "No time windows for these: they are kept as totals";

        /// <summary>Damage dealt by one player in a window (the same rows, the same eight damage types, as Battle's Damage page).</summary>
        static double DealtInWindow(PanelInput p, TimeWindow w, PanelInput viewer)
        {
            if (!IsDayWindow(w)) return DealtRows(Damage(p.Log, w, "", viewer.NowUtc)).Sum(r => (double)r.Amount);   // Since install is not read here: Together has the totals
            // a day window (HISTORY-06.md): your own day history; a fellow's shared damage dealt per day (their "dealtByDay", the last 30 days)
            if (p.IsSelf) { var c = InWindow(p, w); return c == null ? 0 : DealtRows(DamageSinceInstallRows(c)).Sum(r => (double)r.Amount); }
            if (p.DealtByDay == null) return 0;
            DateTime from = DayFrom(viewer, w), to = LocalToday(viewer);
            double sum = 0;
            foreach (var kv in p.DealtByDay)
                if (DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", Inv, System.Globalization.DateTimeStyles.None, out var d) && d >= from && d <= to && kv.Value > 0) sum += kv.Value;
            return sum;
        }

        static string JoinNames(List<string> names) => names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1).ToArray()) + " and " + names[names.Count - 1];

        /// <summary>One quiet line under a windowed Together, only when it tells something the bar cannot: a fellow player's latest
        /// shared record is older than the window (their copy may simply be out of date, so "nothing" would be a guess); nobody
        /// dealt anything in the window; or, in This session, whose session a fellow player's number is. Null otherwise.</summary>
        static Block TogetherWindowNote(PanelInput input, List<Fellow> people, Block together)
        {
            // greyed day chips: the same one line as on every other page ("Day history since 1 Oct: 30 days opens on 30 Oct.")
            var greyed = together.Items.FirstOrDefault(i => i.Kind == "switch")?.Items.Where(v => v.Tone == OffTone).Select(v => (TimeWindow)Enum.Parse(typeof(TimeWindow), v.Id)).ToList();
            var own = people.FirstOrDefault(p => p.Input.IsSelf)?.Input;
            var historyLine = greyed != null && greyed.Count > 0 ? (own != null ? HistoryLine(own, greyed) : null) : null;   // as on every page: no line without a day history (totals not loaded)
            if (together.Tone != WindowedTone) return historyLine == null ? null : new Block { Kind = "note", Text = historyLine };
            var sw = together.Items.First(i => i.Kind == "switch"); var w = (TimeWindow)Enum.Parse(typeof(TimeWindow), sw.Items.First(y => y.Selected).Id);
            var dealt = together.Items.First(i => i.Kind == "category" && i.Id == "dealt");
            var others = people.Where(p => !p.Input.IsSelf).ToList();
            var cutoff = Cutoff(w, input.NowUtc);
            var stale = cutoff.HasValue
                ? others.Where(p => !p.Input.LastRecordedUtc.HasValue || p.Input.LastRecordedUtc.Value.AddMinutes(SpanOf(p.Input)) <= cutoff.Value).Select(p => p.Name).ToList()
                : new List<string>();
            string text = null;
            if (IsDayWindow(w))
            {
                // the day windows: yours from the day history, a fellow's from what they share per day
                var noDays = others.Where(p => p.Input.DealtByDay == null).Select(p => p.Name).ToList();
                if (noDays.Count > 0) text = JoinNames(noDays) + (noDays.Count == 1 ? " shares" : " share") + " no days yet: their Hearthwoven is older.";
            }
            if (historyLine != null) text = text == null ? historyLine : historyLine + " " + text;
            if (text != null) { }
            else if (stale.Count > 0) text = "Nothing in this window from " + JoinNames(stale) + ": their latest shared record is older.";
            else if (dealt.Items.All(p => p.Fraction <= 0)) text = w == TimeWindow.Session ? "Nothing dealt this session." : "Nothing dealt in the " + WindowLabel(w).ToLowerInvariant() + ".";
            else if (w == TimeWindow.Session && others.Count > 0) text = WindowFellowSession;
            return text == null ? null : new Block { Kind = "note", Text = text };
        }

        // ---------- Food shared ----------

        /// <summary>
        /// "Food shared" as the axis: per fellow player, left what the owner enjoyed of their food (the owner's record), right
        /// what they enjoyed of the owner's (their record). Meals and feast servings, dish by dish. Only fellows with food
        /// either way, by name.
        /// </summary>
        public static Block FoodAxis(PanelInput input, List<Fellow> people, List<Gift> gifts)
        {
            var owner = people[0];
            var food = gifts.Where(g => g.Kind == "food").ToList();
            var left = food.Where(g => SameName(g.To, owner.Name)).ToList(); var right = food.Where(g => SameName(g.From, owner.Name)).ToList();
            if (left.Count + right.Count == 0) return null;
            var max = Math.Max(1, left.Concat(right).Max(g => g.N));
            var you = input.IsSelf ? "you" : Name(input);
            var items = new List<Block>();
            if (left.Count > 0)
            {
                var n = left.Sum(g => g.N); var src = owner.Input.IsSelf ? SrcPc : SrcFellows;
                items.Add(new Block { Kind = "end", Tone = "left", Value = N(n), Title = (n == 1 ? "serving " : "servings ") + you + " enjoyed", Src = src, Source = TagOfSrc(src), Value2 = src == SrcFellows ? TheirScope(owner.Input.SharedSinceInstall) : null });
            }
            if (right.Count > 0)
            {
                var n = right.Sum(g => g.N); var src = right.All(g => g.Src == SrcPc) ? SrcPc : SrcFellows;
                items.Add(new Block { Kind = "end", Tone = "right", Value = N(n), Title = (n == 1 ? "serving" : "servings") + " of " + (you == "you" ? "yours" : you + "'s") + " enjoyed", Src = src, Source = TagOfSrc(src), Value2 = src == SrcFellows ? TheirScope(right.Where(g => g.Src == SrcFellows).All(g => g.SharedSince)) : null });
            }
            foreach (var p in people.Skip(1))
            {
                var mine = left.FirstOrDefault(g => SameName(g.From, p.Name)); var theirs = right.FirstOrDefault(g => SameName(g.To, p.Name));
                if (mine != null) items.Add(AxisRow(p, mine, "left", max));
                if (theirs != null) items.Add(AxisRow(p, theirs, "right", max));
            }
            return new Block { Kind = "axis", Value = N(max), Items = items, Src = items.All(i => i.Src == SrcPc) ? SrcPc : SrcFellows, Source = items.All(i => i.Src == SrcPc) ? TagMeasured : TagFellows };
        }

        static Block AxisRow(Fellow p, Gift g, string side, double max)
        {
            var chips = g.Meals.Concat(g.Feasts).GroupBy(kv => kv.Key).Select(x => new KeyValuePair<string, double>(x.Key, x.Sum(kv => kv.Value)))
                .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(TileTop)
                .Select(kv => new Block { Kind = "chip", Icon = "item:" + kv.Key, Value = "× " + N(kv.Value), Src = g.Src, Source = TagOfSrc(g.Src) }).ToList();
            var feast = g.Feasts.Values.Sum();
            return new Block
            {
                Kind = "row", Id = p.Name, Title = p.Label, Icon = "person:" + p.Name, Tone = side, Value = N(g.N), Fraction = (float)(g.N / max),
                Fraction2 = g.N > 0 ? (float)(feast / g.N) : 0, Src = g.Src, Source = TagOfSrc(g.Src), Items = chips,
                Count = (int)Math.Round(g.Meals.Values.Sum()), Level = (int)Math.Round(feast),   // the counts inside the bar: meals, feast servings
            };
        }

        // ---------- Gear shared ----------

        /// <summary>
        /// "Gear shared" (kind madeby): Title = the left column's heading (gear fellow players made that the owner put to
        /// good use), Text = the right column's (gear the owner made that they put to good use); Items = Kind "row" (Tone
        /// left | right, Id and Icon the fellow player, Title their label), each with Items = one tile per kind of gear
        /// (Kind "tile": Icon item, Title its name; no counts). Note = the key.
        /// </summary>
        public static Block GearMadeBy(PanelInput input, List<Fellow> people, List<Gift> gifts)
        {
            var owner = people[0];
            var gear = gifts.Where(g => g.Kind == "gear").ToList();
            var items = new List<Block>(); var sharedSince = new Dictionary<Block, bool>();
            foreach (var side in new[] { "left", "right" })
                foreach (var p in people.Skip(1))
                {
                    var g = gear.FirstOrDefault(x => side == "left" ? SameName(x.From, p.Name) && SameName(x.To, owner.Name) : SameName(x.From, owner.Name) && SameName(x.To, p.Name));
                    if (g == null) continue;
                    var row = new Block
                    {
                        Kind = "row", Tone = side, Id = p.Name, Icon = "person:" + p.Name, Title = p.Label, Src = g.Src, Source = TagOfSrc(g.Src),
                        Items = g.Gear.Keys.OrderBy(k => Who(input, k), StringComparer.OrdinalIgnoreCase).Select(k => new Block { Kind = "tile", Icon = "item:" + k, Title = Who(input, k), Value = N(g.Gear[k]), Src = g.Src, Source = TagOfSrc(g.Src) }).ToList(),
                    };
                    sharedSince[row] = g.SharedSince; items.Add(row);
                }
            if (items.Count == 0) return null;
            string Scope(string side) { var rows = items.Where(i => i.Tone == side).ToList(); return rows.Count == 0 ? "" : rows.All(r => r.Src == SrcPc) ? ", " + SinceInstallLabel : ", " + TheirScope(rows.Where(r => r.Src == SrcFellows).All(r => sharedSince[r])); }
            return new Block { Kind = "madeby", Title = "Their gear" + Scope("left"), Text = (input.IsSelf ? "Your gear" : Name(input) + "'s gear") + Scope("right"), Items = items, Note = GearCountNote };
        }
    }
}
