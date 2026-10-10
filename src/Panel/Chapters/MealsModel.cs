using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Deeds > Meals (0.8.1; Joost 2026-10-10: "I want to see what you ate, but also what anyone else ate"): the eater's side of Cooking, read
    /// from what each PC already records (ClientHooks.Eat: SessionEvents.AteFoodMadeBy "cook|dish"; feast servings in AteFromFeastOf and
    /// AteFromFeastAt). A player's own book (yours, or a fellow's) has no other players on it (Joost): the hero is the meals eaten with the
    /// dishes tried beside it, under it one damage row per dish (the "sources" form of Chapters/DamageRowsUi.cs, received: quiet ink), most
    /// eaten first, each bar one part in the dish's own colour (Cooking's item colours), no cooks and no legend. The Everyone chip: the meals
    /// eaten together and those cooked by another player, then two views: By dish (the default), one row per dish for the whole group split by
    /// who cooked it in the person colours, Gathered (food the game names no cook on) in the quiet grey; and By player, one row per player
    /// (you first, then by name, never a ranking) split by whose cooking they ate, so who fed whom is one glance. All only: fellow players
    /// share no days. A feast is a dish of its own, its servings credited to who made it as Fireside credits them (FeastBook).
    /// Meals count where they were eaten and are never capped by what a cook made (Cooking caps its "Who enjoyed" that way): this page says
    /// what the eater ate. Drawn and mirrored by the damage rows as they are; this file only builds the blocks.
    /// </summary>
    public static partial class PanelModel
    {
        public const string MealsPageId = "meals", MealsLabel = "Meals", MealsIcon = "vocab:away-hero-dishes";
        /// <summary>The cook ClientHooks.Eat writes when the food names none, and how a bar names it.</summary>
        public const string NoCook = "unknown", NoCookTitle = "Gathered";
        /// <summary>A cook who is not around the fire (FiresidePeople: a visitor, a friend without Hearthwoven, whoever set out a feast and does not
        /// share): a cook, but not one to name or colour (REVIEW-081 batch 5, as Fireside's Gifts leave them out), so the person colours stay
        /// Cooking's and the palette never wraps for them.</summary>
        public const string OutsideCook = "~outside", OutsideCookTitle = "Another player";
        /// <summary>The dish rows a page shows; past this the smallest fold into one row ("4 other dishes"), so the rows add up to every meal.</summary>
        public const int MealsTop = 12;
        public const string MealsByDish = "Meals by dish", MealsByPlayer = "Meals by player", MealsSplit = "split by who cooked it", MealsSplitGroup = "split by whose cooking it was";
        /// <summary>The Everyone views (PanelState.View "Deeds/meals/group"): By dish first, the default.</summary>
        public const string MealsGroupSwitch = "group", MealsDishView = "dish", MealsPlayerView = "player", ByDishLabel = "By dish", ByPlayerLabel = "By player";
        public const string NothingEaten = "nothing eaten yet", CookedByAnother = "cooked by another player";
        public const string MealsEmptyLine = "What you eat shows up here, dish by dish.";
        public const string MealsAboutBefore = "Not counted: Hearthwoven counts meals from the day it was installed.";
        public const string MealsAboutDetails = "Every meal counts once: food from your bag, food you picked, found or gathered, such as berries, and each serving from a feast.";
        /// <summary>About these numbers with Everyone on: how the cook is known, what Gathered is, whose PC counts.</summary>
        public const string MealsCookLabel = "Who cooked it", MealsGatheredLabel = "Gathered", MealsPlayersLabel = "Players";
        public const string MealsCookAbout = "The cook is the name the game writes on the food; a dish from a cooking station names the station's owner. " +
                                             "A feast counts for the one who made it. A cook who does not share with the group, or a feast set out by one, shows as Another player.";
        public const string MealsGatheredAbout = "Food with no cook on it: picked, found or gathered, such as berries, mushrooms or honey.";
        public const string MealsPlayersAbout = "Each player's meals are counted on their own PC, as they last shared them.";

        public static string MealsFrom(double n) => "Hearthwoven counted every meal you enjoyed on this PC, from your bag and from feasts: " + N(n) + " so far.";
        public static string OtherDishes(int n) => n + (n == 1 ? " other dish" : " other dishes");

        /// <summary>
        /// What one player ate, from their own record: dish (the item prefab, "CookedMeat"; a feast's prefab) -> cook -> servings. A meal's cook is
        /// the name the game wrote on the food (NoCook when none); a feast serving's is its maker by Fireside's rule (FeastBook: the crafter when
        /// the one who set it out says so, else who set it out). A cook who is not around the fire counts as OutsideCook. <paramref name="book"/>:
        /// the book being read, all time, whose people name the feasts' makers (a feast set out before a day window still finds its maker).
        /// A cook's name is spelled as the people around the fire spell it.
        /// </summary>
        public static Dictionary<string, Dictionary<string, double>> MealsOf(PanelInput eater, PanelInput book)
        {
            var dishes = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
            var ev = eater?.Events;
            if (ev == null) return dishes;
            book = book ?? eater;
            var people = FiresidePeople(book);
            void Put(string dish, string cook, double n)
            {
                if (n <= 0 || string.IsNullOrEmpty(dish)) return;
                cook = string.IsNullOrEmpty(cook) || SameName(cook, NoCook) ? NoCook : cook == OutsideCook ? OutsideCook : people.FirstOrDefault(p => SameName(p.Name, cook))?.Name ?? OutsideCook;
                if (!dishes.TryGetValue(dish, out var by)) dishes[dish] = by = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                Bump(by, cook, n);
            }
            foreach (var kv in ev.AteFoodMadeBy) { var p = Split2(kv.Key); Put(p[1], p[0], kv.Value); }
            // feast servings: the credited part by its maker, the rest (set out by someone not around the fire) as OutsideCook
            var feasts = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var kv in ev.AteFromFeastOf) if (kv.Value > 0) Bump(feasts, Split2(kv.Key)[1], kv.Value);
            foreach (var part in new FeastBook(book, people).Credit(eater, Doubles(ev.AteFromFeastOf), Doubles(ev.AteFromFeastAt)))
            {
                Put(part.Feast, part.Maker, part.N);
                if (feasts.ContainsKey(part.Feast)) feasts[part.Feast] -= part.N;
            }
            foreach (var kv in feasts) Put(kv.Key, OutsideCook, Math.Round(kv.Value, 3));
            return dishes;
        }

        /// <summary>Every meal in a player's record (meals and feast servings): what the hero counts.</summary>
        public static double MealsEaten(Dictionary<string, Dictionary<string, double>> dishes) => dishes.Values.Sum(d => d.Values.Sum());

        /// <summary>The cooks of a bar in one order on every bar of the page: the book's owner first, then the others by name, then Another player, Gathered last.</summary>
        static List<string> CookOrder(IEnumerable<string> cooks, string owner) =>
            cooks.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => SameName(c, NoCook) ? 3 : c == OutsideCook ? 2 : SameName(c, owner) ? 0 : 1).ThenBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>A bar's parts, one per cook (Kind "part": the person's colour, Gathered and Another player in the bar's quiet grey), each with its servings inside.</summary>
        public static List<Block> CookParts(IDictionary<string, double> byCook, string owner, Func<string, string> title)
        {
            var total = byCook.Values.Where(v => v > 0).Sum();
            if (total <= 0) return new List<Block>();
            return CookOrder(byCook.Where(kv => kv.Value > 0).Select(kv => kv.Key), owner).Select(c => SameName(c, NoCook) || c == OutsideCook
                ? new Block { Kind = "part", Id = c == OutsideCook ? OutsideCook : NoCook, Title = c == OutsideCook ? OutsideCookTitle : NoCookTitle, Colour = BarOtherColour, Value = N(byCook[c]), Fraction = (float)(byCook[c] / total) }
                : new Block { Kind = "part", Id = c, Title = title(c), Icon = "person:" + c, Colour = "person:" + c, Value = N(byCook[c]), Fraction = (float)(byCook[c] / total) }).ToList();
        }

        /// <summary>
        /// The dish rows (damage rows, the "sources" form): one per dish, most eaten first, its total on the page's one scale. byCook (Everyone):
        /// the bar split by cook (CookParts); else (a player's own book) one part in the dish's own colour, no number inside (the count stands in
        /// front) and no line of shares under it (Tone DamagePlayerTone with no "mix": the damage row draws no line). Past MealsTop rows the
        /// smallest fold into one "n other dishes" row, their cooks summed. null: nothing eaten.
        /// </summary>
        public static Block MealsByDishRows(PanelInput input, Dictionary<string, Dictionary<string, double>> dishes, bool byCook, string owner, Func<string, string> cookTitle, string src)
        {
            var all = dishes.Select(kv => (dish: kv.Key, cooks: kv.Value, total: kv.Value.Values.Sum())).Where(x => x.total > 0)
                            .OrderByDescending(x => x.total).ThenBy(x => x.dish, StringComparer.Ordinal).ToList();
            if (all.Count == 0) return null;
            var shown = all.Count > MealsTop ? all.Take(MealsTop - 1).ToList() : all;
            if (all.Count > MealsTop)
            {
                var rest = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var x in all.Skip(MealsTop - 1)) foreach (var kv in x.cooks) Bump(rest, kv.Key, kv.Value);
                shown.Add((FoldId, rest, rest.Values.Sum()));
            }
            var max = shown.Max(x => x.total); var tag = TagOfSrc(src); var folded = all.Count - (MealsTop - 1);
            // a dish's own colour as Cooking gives it (its icon's colour, kept apart from the others on the page; a palette colour by name without one)
            var tint = ItemTint(input);
            var colours = byCook ? null : BarColours(shown.Where(x => x.dish != FoldId).Select(x => (x.dish, DishTitle(input, x.dish), tint(x.dish.StartsWith("$") ? x.dish : Ask(input.ItemToken, x.dish) ?? x.dish), false)).ToList(), shown.Any(x => x.dish == FoldId));
            return new Block
            {
                Kind = "sources", Title = MealsByDish, Text = byCook ? MealsSplit : null, Src = src, Source = tag,
                Items = shown.Select(x =>
                {
                    var title = x.dish == FoldId ? OtherDishes(folded) : DishTitle(input, x.dish);
                    return new Block
                    {
                        Kind = "source", Id = x.dish, Title = title, Icon = x.dish == FoldId ? "" : "item:" + x.dish, Tone = byCook ? null : DamagePlayerTone,
                        Value = N(x.total), Fraction = (float)(x.total / max), Src = src, Source = tag,
                        Items = byCook ? CookParts(x.cooks, owner, cookTitle)
                                       : new List<Block> { new Block { Kind = "part", Id = x.dish, Title = title, Colour = x.dish == FoldId ? BarOtherColour : colours[x.dish], Fraction = 1 } },
                    };
                }).ToList(),
            };
        }

        /// <summary>A dish row's name: the game's name, but a feast by its land ("Meadows feast", as Fireside says "set out a Meadows feast"): the
        /// game's feast names ("Whole Roasted Meadow Boar") do not fit the damage rows' name column. A feast of a land the book does not know keeps its name.</summary>
        static string DishTitle(PanelInput input, string dish)
        {
            var said = SetOutFeast(new[] { dish }); const string a = "set out a ";
            return said.StartsWith(a, StringComparison.Ordinal) && said != a + "feast" ? Cap(said.Substring(a.Length)) : Who(input, dish);
        }

        /// <summary>Deeds > Meals: the window chips (the day windows and All, as every Deeds page without minutes), then this book's meals, or the
        /// group's with the Everyone chip on your own book.</summary>
        static void Meals(PanelInput input, PanelView view, PanelState state)
        {
            view.Recorded = true;   // a 0.7 page: one recorded total, no zones
            view.Heading = MealsLabel;
            view.Scope = RecordedScope(input);   // a fellow's copy: whose and when (rule E)
            var group = state != null && state.Everyone && input.IsSelf && FellowsOf(input).Any();
            // a Deeds page shows All until a window is chosen (PanelState.WindowPicked); the group has no days (fellows share none): All only
            var chosen = state != null && state.Window == TimeWindow.Session && !state.WindowPicked ? TimeWindow.SinceInstall : state?.Window;
            Func<PanelInput, TimeWindow, bool> open = group ? (i, x) => x == TimeWindow.SinceInstall : (Func<PanelInput, TimeWindow, bool>)DeedsWindowOpen;
            var w = state == null ? TimeWindow.SinceInstall : WindowChips(input, state, view, WindowsOf(Chapter.Deeds, MealsPageId) ?? DayAndAll, chosen, open);
            if (group) { MealsGroup(input, view, state); return; }

            var day = IsDayWindow(w) ? DeedsWindow(input, w) : null;
            var src = day ?? input;
            var dishes = MealsOf(src, input);
            var total = MealsEaten(dishes);
            var tried = dishes.Count(d => d.Value.Values.Sum() > 0);
            Add(view, Hero((total > 0 ? N(total) : null, Label1(total, "meal eaten", "meals eaten"), SrcPc, null),
                           (tried > 0 ? N(tried) : null, Label1(tried, "dish tried", "dishes tried"), SrcPc, null)));
            Add(view, MealsByDishRows(src, dishes, false, input.PlayerName, null, SrcPc));   // no other players on a player's own book (Joost)
            if (total <= 0 && day == null) view.Blocks.Add(Empty(NothingFrom(input, StartOf(input, null)), Voice(input, MealsEmptyLine)));
            if (day == null) AboutNumbers(view, input, StartOf(input, null), MealsAboutBefore, MealsFrom(total), MealsAboutDetails);   // your own book (a fellow's copy has no dates)
            Plate(view, MealsIcon, RecordedScope(input));
            if (day != null) DeedsDayLines(input, day, MealsPageId, view, w);   // nothing in the window, a very full day
        }

        /// <summary>
        /// The group's Meals (the Everyone chip): the meals eaten together with those cooked by another player beside them, then the view switch
        /// (as Battle's By player / By weapon): By dish, the default, one row per dish for the whole group split by who cooked it; By player, one row
        /// per player (you first, then the others by name: a fixed order, never a ranking), split by whose cooking they ate, with how fresh their
        /// numbers are under the name. Each player's meals from their own record as their own book reads it (MealsOf).
        /// </summary>
        static void MealsGroup(PanelInput input, PanelView view, PanelState state)
        {
            view.EveryoneOn = true;
            foreach (var c in view.Windows.Where(c => c.Id != TimeWindow.SinceInstall.ToString())) c.Waits = false;   // greyed for the group, not waiting for your day history
            var people = FiresidePeople(input);
            var meals = people.Select(p => MealsOf(p.Input, input)).ToList();
            var totals = meals.Select(MealsEaten).ToList();
            var max = totals.DefaultIfEmpty(0).Max(); var all = totals.Sum();
            double fromAnother = 0;
            var rows = new List<Block>();
            for (int k = 0; k < people.Count; k++)
            {
                var p = people[k];
                var byCook = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in meals[k].Values) foreach (var kv in d) Bump(byCook, kv.Key, kv.Value);
                fromAnother += byCook.Where(kv => !SameName(kv.Key, NoCook) && !SameName(kv.Key, p.Name)).Sum(kv => kv.Value);
                var row = new Block { Kind = "source", Id = p.Name, Title = p.Input.IsSelf ? "You" : p.Name, Icon = "person:" + p.Name, Note = Freshness(p.Input, input), Src = SrcFellows, Source = TagFellows };
                if (totals[k] > 0) { row.Value = N(totals[k]); row.Fraction = (float)(totals[k] / max); row.Items = CookParts(byCook, input.PlayerName, c => SameName(c, input.PlayerName) ? "You" : c); }
                else { row.Text = NothingEaten; row.Items = new List<Block>(); }
                rows.Add(row);
            }
            Add(view, Hero((all > 0 ? N(all) : null, Label1(all, "meal eaten together", "meals eaten together"), SrcFellows, null),
                           (fromAnother > 0 ? N(fromAnother) : null, CookedByAnother, SrcFellows, null)));
            var together = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
            foreach (var m in meals)
                foreach (var d in m)
                {
                    if (!together.TryGetValue(d.Key, out var by)) together[d.Key] = by = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in d.Value) Bump(by, kv.Key, kv.Value);
                }
            var byDish = MealsByDishRows(input, together, true, input.PlayerName, c => SameName(c, input.PlayerName) ? "You" : c, SrcFellows);
            Switch(view, state, MealsPageId, MealsGroupSwitch, null,
                   (MealsDishView, ByDishLabel, byDish == null ? null : new List<Block> { byDish }),
                   (MealsPlayerView, ByPlayerLabel, new List<Block> { new Block { Kind = "sources", Title = MealsByPlayer, Text = MealsSplitGroup, Src = SrcFellows, Source = TagFellows, Items = rows } }));
            var lastOnly = people.Where(p => !p.Input.IsSelf && !p.Input.SharedSinceInstall).Select(p => p.Name).ToList();
            if (lastOnly.Count > 0) view.Blocks.Add(new Block { Kind = "note", Text = JoinNames(lastOnly) + (lastOnly.Count == 1 ? " shares" : " share") + " no totals: their number is their last shared session." });
            view.AboutNumbers = new Block
            {
                Kind = "aboutnumbers", Id = NumbersTarget, Title = AboutNumbersTitle,
                Items = new List<Block> { new Block { Kind = "aboutline", Title = MealsCookLabel, Text = MealsCookAbout }, new Block { Kind = "aboutline", Title = MealsGatheredLabel, Text = MealsGatheredAbout },
                                          new Block { Kind = "aboutline", Title = MealsPlayersLabel, Text = MealsPlayersAbout } },
            };
            Plate(view, MealsIcon, GroupDaysLine);   // the greyed day chips' reason (PageHead); each player's row says how fresh their numbers are
        }
    }
}
