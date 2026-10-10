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

        /// <summary>Fireside's list says its source once per section (0.7 integration): every row is counted by the PC of who received it.</summary>
        public const string CountedByReceiver = "Counted by who received it";
        public const string FiresideHeading = "Fireside", FiresideLine = "who made what for whom", TogetherHeading = "Together", FoodHeading = "Food shared", GearHeading = "Gear shared";
        public const string PutToGoodUse = "put to good use", Enjoyed = "enjoyed";

        /// <summary>A feast is SET OUT, never made (B22: the data is the feast piece's placer, ClientHooks FeastEat): "set out a Mountains feast",
        /// "set out feasts" for several kinds. The feast's prefab names its land ("FeastMountains"); one we do not know is "a feast".</summary>
        public static string SetOutFeast(IEnumerable<string> feasts)
        {
            var kinds = (feasts ?? new string[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (kinds.Count != 1) return "set out feasts";
            var land = kinds[0] != null && kinds[0].StartsWith("Feast", StringComparison.OrdinalIgnoreCase) ? kinds[0].Substring(5) : "";
            switch (land.ToLowerInvariant())
            {
                case "meadows": land = "Meadows"; break; case "blackforest": land = "Black Forest"; break; case "swamps": case "swamp": land = "Swamp"; break;
                case "oceans": case "ocean": land = "Ocean"; break; case "mountains": case "mountain": land = "Mountains"; break; case "plains": land = "Plains"; break;
                case "mistlands": land = "Mistlands"; break; case "ashlands": land = "Ashlands"; break; default: land = null; break;
            }
            return land == null ? "set out a feast" : "set out a " + land + " feast";
        }

        // ---------- feasts: who made it, who set it out (0.8, B22; Joost: "Another community thing! Teamwork!") ----------

        /// <summary>A Fireside line (and its mark) for servings from a feast one player made and another set out.</summary>
        public const string TeamworkTone = "teamwork", LegendTeamwork = "teamwork";

        /// <summary>
        /// One part of an eater's feast servings, credited. Maker = who they count for, a person around the fire: the feast's crafter when the
        /// copy of the one who set it out names them (SetOutFeastMadeBy, joined to the eater's AteFromFeastAt by the feast's own id), else the
        /// one who set it out (an older copy, a feast set out before 0.8, a crafter who is not around the fire). Known = the crafter is on
        /// record. Other = the other hand of a teamwork feast, named on the line: who set it out (the maker made it), or, OtherMade, who made it
        /// while that player is not around the fire (the credit stays with the one who set it out); null = one player did both, or nobody knows.
        /// </summary>
        public sealed class FeastPart
        {
            public PanelInput Eater; public string Maker, Other, Feast; public bool Known, OtherMade; public double N;
            public bool Teamwork => Other != null;
            /// <summary>What the maker did, for the Food shared key: "made" (a teamwork feast someone else set out) or "set out".</summary>
            public bool MadeOnly => Known && Other != null && !OtherMade;
        }

        /// <summary>The player ids of the people around the fire, to their names (a feast names the one who set it out by id).</summary>
        static Dictionary<long, string> PeopleIds(PanelInput input, List<Fellow> people)
        {
            var byName = people.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var ids = new Dictionary<long, string>();
            if (input.PlayerNames != null) foreach (var kv in input.PlayerNames) if (byName.ContainsKey(kv.Value ?? "")) ids[kv.Key] = byName[kv.Value].Name;
            foreach (var p in people) if (p.Input.PlayerId != 0) ids[p.Input.PlayerId] = p.Name;
            return ids;
        }

        public static List<FeastPart> FeastParts(PanelInput input) => FeastParts(input, FiresidePeople(input));

        /// <summary>
        /// Every feast serving in the records of the owner and the fellow players who share, credited (FeastPart): one rule for Fireside,
        /// Food shared, Cooking's "Who enjoyed", the overview card and While away. A feast is joined to its maker by its own id, never by its
        /// kind, so a feast set out before 0.8 is never credited to whoever made a later one; those servings stay with the one who set it out.
        /// </summary>
        public static List<FeastPart> FeastParts(PanelInput input, List<Fellow> people)
        {
            var book = new FeastBook(input, people);
            var parts = new List<FeastPart>();
            foreach (var e in new[] { input }.Concat(FellowsOf(input)))
                if (e.Events != null) parts.AddRange(book.Credit(e, Doubles(e.Events.AteFromFeastOf), Doubles(e.Events.AteFromFeastAt)));
            return parts;
        }
        static IEnumerable<KeyValuePair<string, double>> Doubles(IDictionary<string, float> d) =>
            d == null ? Enumerable.Empty<KeyValuePair<string, double>>() : d.Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value));

        /// <summary>Who is around the fire (names, player ids) and who made the feasts they set out: feast id -> who set it out, their id,
        /// the crafter (their SetOutFeastMadeBy, "crafter|feast|feast id"). Credit applies FeastParts' rule to any pair of feast tallies.</summary>
        sealed class FeastBook
        {
            readonly Dictionary<string, Fellow> byName; readonly Dictionary<long, string> ids;
            readonly Dictionary<string, (string placer, long placerId, string crafter)> made = new Dictionary<string, (string, long, string)>();
            public FeastBook(PanelInput input, List<Fellow> people)
            {
                byName = people.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
                ids = PeopleIds(input, people);
                foreach (var p in people)
                    if (p.Input.Events != null)
                        foreach (var kv in p.Input.Events.SetOutFeastMadeBy)
                        {
                            int a = kv.Key.IndexOf('|'), b = kv.Key.LastIndexOf('|');
                            if (a > 0 && b > a) made[kv.Key.Substring(b + 1)] = (p.Name, p.Input.PlayerId, kv.Key.Substring(0, a));
                        }
            }

            /// <summary>One eater's feast servings, credited: <paramref name="ateOf"/> "placer id|feast" (AteFromFeastOf), <paramref name="ateAt"/>
            /// "placer id|feast|feast id" (AteFromFeastAt), both from the same record (a session, since install, or what grew in between).</summary>
            public List<FeastPart> Credit(PanelInput eater, IEnumerable<KeyValuePair<string, double>> ateOf, IEnumerable<KeyValuePair<string, double>> ateAt)
            {
                var parts = new List<FeastPart>();
                var room = new Dictionary<string, double>();   // "placer id|feast" -> servings (AteFromFeastOf): a known feast takes its servings from here, never more
                foreach (var kv in ateOf) if (kv.Value > 0) Bump(room, kv.Key, kv.Value);
                foreach (var kv in ateAt)
                {
                    int a = kv.Key.IndexOf('|'), b = kv.Key.LastIndexOf('|');
                    if (a <= 0 || b <= a || kv.Value <= 0 || !made.TryGetValue(kv.Key.Substring(b + 1), out var m)) continue;
                    if (!long.TryParse(kv.Key.Substring(0, a), NumberStyles.Integer, Inv, out var placerId) || placerId != m.placerId) continue;   // the same feast, set out by that same player
                    var key = kv.Key.Substring(0, b); var n = Math.Min(kv.Value, room.TryGetValue(key, out var r) ? r : 0);
                    if (n <= 0) continue;
                    room[key] = r - n;
                    var part = new FeastPart { Eater = eater, Known = true, Feast = kv.Key.Substring(a + 1, b - a - 1), N = n };
                    if (SameName(m.crafter, m.placer)) part.Maker = m.placer;                                           // made it and set it out
                    else if (byName.TryGetValue(m.crafter, out var c)) { part.Maker = c.Name; part.Other = m.placer; }  // teamwork: the crafter's, set out by another
                    else { part.Maker = m.placer; part.Other = m.crafter; part.OtherMade = true; }                     // teamwork, the crafter not around the fire
                    parts.Add(part);
                }
                foreach (var kv in room)   // the rest: the one who set it out (the piece's creator), as before 0.8
                {
                    var p = Split2(kv.Key);
                    if (kv.Value <= 0 || !long.TryParse(p[0], NumberStyles.Integer, Inv, out var id) || id == 0 || !ids.TryGetValue(id, out var placer)) continue;
                    parts.Add(new FeastPart { Eater = eater, Maker = placer, Feast = p[1], N = kv.Value });
                }
                return parts;
            }
        }

        /// <summary>One fellow's feast servings in what grew while you were away (AwayModel), credited by FeastParts' rule.</summary>
        internal static List<FeastPart> FeastPartsGrown(PanelInput viewer, PanelInput eater, IDictionary<string, double> ateOf, IDictionary<string, double> ateAt) =>
            new FeastBook(viewer, FiresidePeople(viewer)).Credit(eater, ateOf ?? new Dictionary<string, double>(), ateAt ?? new Dictionary<string, double>());

        /// <summary>How a name reads inside a line of this book: "you" for the book's own player, else the name.</summary>
        static Func<string, string> SayName(PanelInput input) => n => input.IsSelf && SameName(n, input.PlayerName) ? "you" : n;

        /// <summary>The servings of a gift's teamwork feasts, as one quiet line under a Food shared row: "Teamwork: 3 servings from a feast Tor
        /// set out"; null when none. Several feasts or several hands say so ("from feasts Tor and Finch set out").</summary>
        public static string TeamworkNote(IEnumerable<FeastPart> parts, Func<string, string> say)
        {
            var team = (parts ?? new FeastPart[0]).Where(p => p.Teamwork).ToList();
            if (team.Count == 0) return null;
            var said = team.GroupBy(p => p.OtherMade).OrderBy(g => g.Key).Select(g =>
            {
                var n = g.Sum(p => p.N);
                var hands = g.Select(p => say(p.Other)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                return N(n) + (n == 1 ? " serving" : " servings") + " from " + (g.Count() == 1 ? "a feast " : "feasts ") + JoinNames(hands) + (g.Key ? " made" : " set out");
            });
            return "Teamwork: " + string.Join(" · ", said.ToArray());
        }

        /// <summary>The axis key for the feast part of a food bar (AxisUi): the 0.7 words "from a feast they set out", true while every feast
        /// serving on the axis counts for the one who set it out; once a bar counts a feast its maker made and another set out (the teamwork
        /// notes say who), plainly "from a feast".</summary>
        public static string FeastKey(IEnumerable<FeastPart> parts) => (parts ?? new FeastPart[0]).Any(p => p.MadeOnly) ? "from a feast" : PanelUi.FeastServingKey;

        // ---------- 0.7 words (REDESIGN-RULES.md part 0, rule E): a fellow's copy says whose PC it is, or that it is only the session they last shared ----------

        /// <summary>One fellow's copy, on a row: "Recorded on Tor's PC", or "as Tor last shared it" when the copy is only their last session.</summary>
        public static string FellowWords(string name, bool sharedSince) => sharedSince ? "Recorded on " + name + "'s PC" : "as " + name + " last shared it";

        /// <summary>
        /// One caption for a list whose fellow copies agree (they say it once, in the caption); null when they differ, and each row says its own.
        /// Several fellows: "Recorded on each player's PC" / "as each player last shared it".
        /// </summary>
        public static string FellowCaption(IEnumerable<(string name, bool sharedSince)> copies)
        {
            var list = copies.ToList();
            if (list.Count == 0) return null;
            var since = list.Select(c => c.sharedSince).Distinct().ToList();
            if (since.Count != 1) return null;
            var names = list.Select(c => c.name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count == 1) return FellowWords(names[0], since[0]);
            return since[0] ? "Recorded on each player's PC" : "as each player last shared it";
        }

        /// <summary>The box's three lines on each Company page (the About these numbers box, REDESIGN-RULES.md part 1; the page texts of SOURCE-MATRIX.md).</summary>
        public const string BoxBefore = "Not recorded for food, gear and damage. Wood, stone, pieces and km are each character's own count.",
                            BoxFrom = "Hearthwoven counted what you enjoyed and put to good use on this PC; each fellow player's PC counts theirs.",
                            BoxDetails = "A fellow player's numbers are as they last shared them. Sailed counts one voyage once for each player aboard.";
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
            /// <summary>Food: the feast servings in Feasts, part by part (who set each feast out, FeastPart).</summary>
            public readonly List<FeastPart> FeastParts = new List<FeastPart>();
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
            var feasts = FeastParts(input, people);   // feast servings: the maker's when the one who set it out says who made it (0.8), else theirs
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
                foreach (var part in feasts)
                {
                    if (part.Eater != to.Input) continue;
                    var g = Get(part.Maker, to, "food"); if (g == null) continue;
                    Bump(g.Feasts, part.Feast, part.N); g.FeastParts.Add(part);
                }
                foreach (var kv in ev.EquippedGearMadeBy) { var p = Split2(kv.Key); var g = Get(p[0], to, "gear"); if (g != null && kv.Value > 0) g.Gear[p[1]] = kv.Value; }   // times put to good use: the count on each tile (Joost, review pack) and, summed, the gift's own count on Fireside
            }
            // every maker's food: never more of a dish than that maker made of it (the Cooking page's rule, so Fireside, Food shared and Cooking
            // tell one number: a cooking station writes its owner on the food whoever cooked it). Your own: your record and since (MadeOf); a
            // fellow's: their shared "made" when their copy runs since install (B22); otherwise not known, nothing capped. Feast servings are not capped.
            foreach (var maker in people)
            {
                var made = MakerMade(maker.Input);
                if (made == null) continue;
                var theirs = gifts.Values.Where(g => g.Kind == "food" && SameName(g.From, maker.Name)).ToList();
                if (theirs.Count == 0) continue;
                var capped = CapToMade(theirs.Select(g => (g.To, new Dictionary<string, double>(g.Meals))).ToList(), made);
                for (int k = 0; k < theirs.Count; k++) { theirs[k].Meals.Clear(); foreach (var kv in capped[k].dishes) theirs[k].Meals[kv.Key] = kv.Value; }
            }
            return gifts.Values.Where(g => g.N > 0).ToList();
        }

        /// <summary>What a maker made of a dish, for CapToMade: your own book's record and since (MadeOf); a fellow's, when their copy carries
        /// since-install totals, the larger of their game record of crafts and their since-install "made", times what one craft makes; else null (not known).</summary>
        static Func<string, double?> MakerMade(PanelInput p)
        {
            if (p == null) return null;
            if (p.IsSelf) return MadeOf(p);
            if (!p.SharedSinceInstall || p.Events == null) return null;
            return dish =>
            {
                var token = dish != null && dish.StartsWith("$") ? dish : Ask(p.ItemToken, dish);
                if (string.IsNullOrEmpty(token)) return null;
                // REVIEW-07 #1: both are lower bounds of how often they crafted it: the game's own crafted record (from before Hearthwoven too)
                // and what Hearthwoven counted since install; the larger one caps. Both count crafts, eaters count servings: REVIEW-08 #1, each
                // craft counts what its recipe makes (5 crafts of sausages are 20 sausages)
                double crafted = p.ItemsCrafted != null && p.ItemsCrafted.TryGetValue(token, out var c) ? c : 0, since = p.Events.Made.TryGetValue(token, out var x) ? x : 0;
                return Math.Max(crafted, since) * YieldOf(p, token);
            };
        }

        static void Company(PanelInput input, string page, PanelState state, PanelView view)
        {
            if (page == AwayPage) { Away(input, view); return; }   // Since you were away (Chapters/AwayModel.cs)
            view.Recorded = true;   // 0.7 (REDESIGN-RULES.md part 5, G6): no zones; the dates come from PlaceRecordedFrom
            view.Scope = RecordedScope(input);
            var people = FiresidePeople(input);
            var gifts = Gifts(input, people);
            switch (page)
            {
                case "together":
                    {
                        view.Heading = TogetherHeading;
                        var tb = Together(input, people, state, page); Add(view, tb);
                        view.Windowed = tb?.Tone == WindowedTone;   // a chosen window of the damage dealt: nothing "since install" about it
                        var wn = tb == null ? null : TogetherWindowNote(input, people, tb, state);
                        if (wn != null && tb.Tone != WindowedTone) view.Blocks.Insert(view.Blocks.IndexOf(tb), wn);   // the wait line alone (a pressed greyed day chip): at the top, where the other pages say it
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
            if (view.Blocks.Count == 0) view.Blocks.Add(Empty(NothingYet, EmptyOf(page)));   // one empty text per page (0.7: no "counts from install" line; the agreed texts stay)
            AboutNumbers(view, input, StartOf(input, null), BoxBefore, BoxFrom, BoxDetails);   // the page's "About these numbers" box (own book only; RecordedModel.cs)
            // every Company page on the plate (slice 3), its pill naming who shares or what is counted
            // Fireside's "who made what for whom" sits in the legend row of its drawing (Giving.Note)
            Plate(view, "ui:chapter-company", RecordedScope(input));
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
            var owner = people[0].Name; var max = Math.Max(1, gifts.Select(g => g.N).DefaultIfEmpty(0).Max()); var say = SayName(input);
            // Text: the list's short name, the plain name for everyone but the owner ("Rowan (you)" on a fellow's book ran into the row's line)
            var items = people.Select(p => new Block { Kind = "player", Id = p.Name, Title = p.Label, Text = p.Owner ? p.Label : p.Name, Icon = "person:" + p.Name, Selected = p.Owner }).ToList();
            foreach (var g in gifts.OrderBy(g => SameName(g.From, owner) || SameName(g.To, owner) ? 0 : 1).ThenBy(g => g.From, StringComparer.OrdinalIgnoreCase)
                                   .ThenBy(g => g.To, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Kind, StringComparer.Ordinal))
            {
                var all = new Dictionary<string, double>(g.Kind == "gear" ? g.Gear : g.Meals);
                if (g.Kind == "food") foreach (var kv in g.Feasts) Bump(all, kv.Key, kv.Value);
                items.Add(new Block
                {
                    Kind = "gift", Id = g.From + ">" + g.To, Title = g.From, Text = g.To, Tone = g.Kind, Icon = "item:" + Top(all), Value = N(g.N), Count = (int)Math.Round(g.N),
                    Fraction = (float)(g.N / max), Note = g.Kind == "gear" ? PutToGoodUse : Enjoyed, Selected = SameName(g.From, owner) || SameName(g.To, owner),
                    Src = g.Src, Source = TagOfSrc(g.Src), Value2 = g.Src == SrcFellows && !g.SharedSince ? FellowWords(g.To, false) : null,   // only where it differs from its section: "as Tor last shared it"
                    // feast servings get list lines of their own (B22): the maker SET OUT the feast ("set out a Mountains feast · × 3 enjoyed"), a teamwork
                    // feast names the other hand ("Tor set it out · × 3 enjoyed"); the meals keep "× 5 enjoyed"; one thread, one count for all
                    Items = g.Kind != "food" || g.Feasts.Values.Sum() <= 0 ? null : FoodLines(g, say),
                });
            }
            // each section says its source once, on a quiet line under its caption (the uppercase caption had no room for it): every row is counted by
            // the PC of who received it, yours from your date; a row whose receiver's copy is only their last session says so itself (Value2)
            string Scope(IEnumerable<Gift> part) { var l = part.ToList(); return l.Count == 0 ? null : CountedByReceiver + (input.IsSelf && l.Any(g => g.Src == SrcPc) ? " · yours " + FromShort(input, StartOf(input, null)) : ""); }
            bool Mine(Gift g) => SameName(g.From, owner) || SameName(g.To, owner);
            return new Block
            {
                Kind = "giving", Title = (input.IsSelf ? "You" : Name(input)) + " and fellow players", Text = "Among fellow players", Note = FiresideLine, Items = items,
                Value = Scope(gifts.Where(Mine)), Value2 = Scope(gifts.Where(g => !Mine(g))),
                Src = gifts.Count == 0 ? null : gifts.All(g => g.Src == SrcPc) ? SrcPc : SrcFellows, Source = gifts.Count == 0 ? null : gifts.All(g => g.Src == SrcPc) ? TagMeasured : TagFellows,
            };
        }

        /// <summary>A food gift's list lines on Fireside (B22, 0.8): the meals ("× 5 enjoyed"), the feasts its maker set out ("set out a Mountains
        /// feast · × 3 enjoyed"), and one line per other hand of a teamwork feast, marked (Tone TeamworkTone): "Tor set it out · × 3 enjoyed",
        /// or "Edda made it" when its maker is not around the fire. The thread runs from the maker to the one who enjoyed it; its count is the sum.</summary>
        static List<Block> FoodLines(Gift g, Func<string, string> say)
        {
            var lines = new List<Block>();
            if (g.Meals.Values.Sum() > 0) lines.Add(new Block { Kind = "giftline", Icon = "item:" + Top(g.Meals), Value = N(g.Meals.Values.Sum()), Note = Enjoyed });
            Dictionary<string, double> Of(IEnumerable<FeastPart> ps) { var d = new Dictionary<string, double>(); foreach (var p in ps) Bump(d, p.Feast, p.N); return d; }
            var own = Of(g.FeastParts.Where(p => !p.Teamwork));
            if (own.Count > 0) lines.Add(new Block { Kind = "giftline", Icon = "item:" + Top(own), Title = SetOutFeast(own.Keys), Value = N(own.Values.Sum()), Note = Enjoyed });
            foreach (var hand in g.FeastParts.Where(p => p.Teamwork).GroupBy(p => (who: say(p.Other), made: p.OtherMade))
                                  .OrderBy(h => h.Key.who, StringComparer.OrdinalIgnoreCase).ThenBy(h => h.Key.made))
            {
                var d = Of(hand);
                lines.Add(new Block { Kind = "giftline", Icon = "item:" + Top(d), Title = hand.Key.who + (hand.Key.made ? " made it" : " set it out"), Value = N(d.Values.Sum()), Note = Enjoyed, Tone = TeamworkTone });
            }
            return lines;
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
        /// Rule A.3 for one player's part of a class A category: an earlier part above 0, or no baseline to split it by (a fellow's copy, or
        /// the pickup counter not stored yet), is a known gap ("Earlier counts may be incomplete.").
        /// </summary>
        static bool HasGap(PanelInput p, string id)
        {
            if (!p.IsSelf) return CategoryValue(p, id) > 0;
            switch (id)
            {
                case "wood": return !HasPickupBaseline(p) || BroughtIn(p, "wood").Values.Sum(v => v.before) > 0;
                case "ore": return !HasPickupBaseline(p) || BroughtIn(p, "mining").Values.Sum(v => v.before) > 0;
                case "cooked": { var layers = CookedLayers(p); return layers == null || layers.Values.Sum(l => l.faded) > 0; }
                default: return false;
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
            if (state != null)   // B17 review: a greyed chip pressed holds on this page only (PanelModel.SettleWait); an open one is the one to return to
            {
                if (!Greyed(win)) state.LastWorkingView[wkey] = win.ToString();
                else if (own != null && DayOpensOn(own, win).HasValue) state.WaitView = wkey;
            }
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
                // rule E/A/C6 (0.7): a class A category (wood, ore, dishes) says Earlier counts may be incomplete. in its scope line when a part has a known gap;
                // cargo (C6) says the date Hearthwoven started counting it on this PC; built and sailed (game counters, complete) and Damage dealt (its window caption) say nothing here
                string scope = null;
                if (c.id == "cargo")
                {
                    scope = string.Join(" · ", new[] { RecordedFromLine(input, StartOf(input, "cargo")), CargoUnit(total) + ": " + CargoUnitWords(total), "straight line, so the real figure is higher" });
                }
                else if (c.id == "wood" || c.id == "ore" || c.id == "cooked")
                    scope = people.Any(p => HasGap(p.Input, c.id)) ? EarlierIncomplete : null;   // hard case 7: the category, once, in its scope line (no RF label on a class A number, rule A.4)
                else if (c.id != "dealt")   // pieces built and sailed: the game's own counters, complete; the matrix keeps their since-when line (rule B, #78 and #81)
                    scope = people.Count == 1 ? MadeLine(input) : "since each character was made";
                cats.Add(new Block
                {
                    Kind = "category", Id = c.id, Icon = TogetherIcon(c.id), Title = c.label, Note = c.note, Value2 = scope, From = c.id == "cargo" ? StartOf(input, "cargo") : null,
                    Text = inWindow ? c.phrase + ", " + WindowLabel(win).ToLowerInvariant() : people.Count == 1 ? c.phrase.Replace(" together", "") : c.phrase, Value = fmt(total),
                    Src = src ?? (people.All(p => p.Input.IsSelf) ? SrcPc : SrcFellows), Source = TagOfSrc(src ?? (people.All(p => p.Input.IsSelf) ? SrcPc : SrcFellows)),
                    Items = people.Select((p, k) =>
                    {
                        var s = src ?? (p.Input.IsSelf ? SrcPc : SrcFellows);
                        // Damage dealt: a fellow whose copy has no damage record at all says so (rule E.3), never 0
                        var noRecord = c.id == "dealt" && !inWindow && !p.Input.IsSelf && p.Input.DamageSinceInstall == null && p.Input.Session == null;
                        // a fellow's part says whose copy it is (rule E): Damage dealt in a window carries none; the total's caption says the rest
                        var words = !p.Input.IsSelf && !noRecord && (c.id == "cargo" || (c.id == "dealt" && !inWindow)) ? FellowWords(p.Name, p.Input.SharedSinceInstall) : null;
                        return new Block { Kind = "part", Id = p.Name, Icon = "person:" + p.Name, Title = p.Label, Value = noRecord ? NotRecorded : values[k] > 0 ? fmt(values[k]) : "", Unrecorded = noRecord,
                                           Fraction = total > 0 ? (float)(values[k] / total) : 0f, Src = s, Source = TagOfSrc(s), From = c.id == "cargo" ? StartOf(p.Input, "cargo") : null, RecordedFrom = words };
                    }).ToList(),
                });
            }
            cats.First(c => c.Id == pickId).Selected = true;
            var items = new List<Block>(cats);
            if (pickId == "dealt")   // the chips Battle uses, under the category chips: who dealt how much, since install or in a window
                items.Add(new Block
                {
                    Kind = "switch", Id = wkey, Title = !windowed && people.Count > 1 ? "All: " + (StartOf(input, null) is DateTime since ? "recorded from " + RecordDate(input, since) : "recorded on this PC") : TogetherWindowCaption,   // short: each fellow part says its own PC
                    Items = TogetherWindows.Select(x => new Block { Kind = "view", Id = x.ToString(), Title = WindowShort(x), Selected = x == win, Tone = Greyed(x) ? OffTone : null, Waits = Greyed(x) && DayOpensOn(own, x).HasValue }).ToList(),
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
        public const string WindowFellowSession = "Other players count from when you started playing this time.";
        /// <summary>The caption above the share rows that do not follow the chosen window (fix3-rest, review: one table mixed two scopes): Damage dealt first,
        /// in the window; under this line the rest, dimmed, and why they have no window (Joost: "since install" there read as a contradiction).</summary>
        public static string WindowRowsNote(List<Fellow> people) =>
            "No time windows for these: they are kept as totals";

        /// <summary>Damage dealt by one player in a window (the same rows, the same eight damage types, as Battle's Damage page).</summary>
        static double DealtInWindow(PanelInput p, TimeWindow w, PanelInput viewer)
        {
            // Since install is not read here: Together has the totals. B33: Session is your session's span for everyone (a fellow's log from when you began)
            if (!IsDayWindow(w)) return DealtRows(Damage(p.Log, w, "", viewer.NowUtc, p.IsSelf ? null : viewer.SessionStartUtc ?? p.ViewerSessionStartUtc)).Sum(r => (double)r.Amount);
            // a day window (HISTORY-06.md): your own day history; a fellow's shared damage dealt per day (their "dealtByDay", the last 30 days)
            if (p.IsSelf) { var c = InWindow(p, w); return c == null ? 0 : DealtRows(DamageSinceInstallRows(c)).Sum(r => (double)r.Amount); }
            if (p.DealtByDay == null) return 0;
            DateTime from = DayFrom(viewer, w), to = LocalToday(viewer);
            double sum = 0;
            foreach (var kv in p.DealtByDay)
                if (DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", Inv, System.Globalization.DateTimeStyles.None, out var d) && d >= from && d <= to && kv.Value > 0) sum += kv.Value;
            return sum;
        }

        // "Edda", "Edda and Tor", "Edda, Finch and Tor"; nobody: "" (Everyone in a window where nobody can be counted asked for the names of
        // an empty sum and threw, v08-fix-open)
        static string JoinNames(List<string> names) => names == null || names.Count == 0 ? "" : names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1).ToArray()) + " and " + names[names.Count - 1];

        /// <summary>One quiet line under a windowed Together, only when it tells something the bar cannot: a fellow player's latest
        /// shared record is older than the window (their copy may simply be out of date, so "nothing" would be a guess); nobody
        /// dealt anything in the window; or, in This session, whose session a fellow player's number is. Null otherwise.</summary>
        static Block TogetherWindowNote(PanelInput input, List<Fellow> people, Block together, PanelState state)
        {
            // a greyed day chip that was pressed (B17): All shows, and the one line says from when the chosen window works, as on every other page
            var sw0 = together.Items.FirstOrDefault(i => i.Kind == "switch");
            var asked = sw0 != null && state != null && state.View.TryGetValue(sw0.Id, out var askedId) ? sw0.Items.FirstOrDefault(v => v.Id == askedId && v.Waits) : null;
            var own = people.FirstOrDefault(p => p.Input.IsSelf)?.Input;
            if (asked != null && own != null) return new Block { Kind = "note", Text = WaitLine(own, (TimeWindow)Enum.Parse(typeof(TimeWindow), asked.Id)) };
            if (together.Tone != WindowedTone) return null;
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
                items.Add(new Block { Kind = "end", Tone = "left", Value = N(n), Title = (n == 1 ? "serving " : "servings ") + you + " enjoyed", Src = src, Source = TagOfSrc(src), Value2 = src == SrcFellows ? FellowWords(owner.Name, owner.Input.SharedSinceInstall) : null });
            }
            if (right.Count > 0)
            {
                var n = right.Sum(g => g.N); var src = right.All(g => g.Src == SrcPc) ? SrcPc : SrcFellows;
                items.Add(new Block { Kind = "end", Tone = "right", Value = N(n), Title = (n == 1 ? "serving" : "servings") + " of " + (you == "you" ? "yours" : you + "'s") + " enjoyed", Src = src, Source = TagOfSrc(src), Value2 = src == SrcFellows ? FellowCaption(right.Where(g => g.Src == SrcFellows).Select(g => (g.To, g.SharedSince))) ?? "Recorded on their PCs, or as they last shared it" : null });   // the axis draws one scope per side (AxisUi): mixed copies get this one line
            }
            var say = SayName(input);
            foreach (var p in people.Skip(1))
            {
                var mine = left.FirstOrDefault(g => SameName(g.From, p.Name)); var theirs = right.FirstOrDefault(g => SameName(g.To, p.Name));
                if (mine != null) items.Add(AxisRow(p, mine, "left", max, say));
                if (theirs != null) items.Add(AxisRow(p, theirs, "right", max, say));
            }
            return new Block { Kind = "axis", Value = N(max), Items = items, Src = items.All(i => i.Src == SrcPc) ? SrcPc : SrcFellows, Source = items.All(i => i.Src == SrcPc) ? TagMeasured : TagFellows,
                               Value2 = FeastKey(left.Concat(right).SelectMany(g => g.FeastParts)) };   // the axis key's feast words (AxisUi)
        }

        /// <summary>One side of a fellow's line on Food shared: Note = the teamwork feasts in it, one quiet line under the dishes (TeamworkNote).</summary>
        static Block AxisRow(Fellow p, Gift g, string side, double max, Func<string, string> say)
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
                Note = TeamworkNote(g.FeastParts, say),
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
            // rule E: a side's caption says its scope once (yours: Recorded from <date>; a fellow's copies: whose PC, or their last session)
            string Scope(string side)
            {
                var rows = items.Where(i => i.Tone == side).ToList();
                if (rows.Count == 0) return "";
                if (rows.All(r => r.Src == SrcPc)) return ", " + (StartOf(input, null) is DateTime since ? "Recorded from " + RecordDate(input, since) : RecordedFromLine(input, null));
                var words = rows.All(r => r.Src == SrcFellows) ? FellowCaption(rows.Select(r => (r.Id, sharedSince[r]))) : null;
                return words == null ? "" : ", " + words;
            }
            return new Block { Kind = "madeby", Title = "Their gear" + Scope("left"), Text = (input.IsSelf ? "Your gear" : Name(input) + "'s gear") + Scope("right"), Items = items, Note = GearCountNote };
        }
    }
}
