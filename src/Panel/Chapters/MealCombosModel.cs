using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 Deeds > Meals, the "Your combinations" island (Joost in game, 18 Oct: "show the food combinations that we had in game. So it shows you like
    /// okay, so you're going hard on stamina and this is your best combination that you've used the most"): the foods you had on together at each meal
    /// (Hearthwoven.MealCombos, recorded on this PC from 0.9 on, never shared), in the damage rows of Meals by dish (the "sources" form,
    /// Chapters/DamageRowsUi.cs, received: quiet ink), one row per combination, most eaten first. A row's picture is its dishes' item icons side by side
    /// (Tone ComboTone, Items Kind ComboDishKind; the names on the line under them), its number the meals, its bar the game's food values summed over
    /// the combination (0.9.2, Joost in game: "this is silly in percentages.. We need real numbers"): one scale for the list, the biggest total the
    /// full track and no floor, so a stronger combination is a longer bar and each part is as long as its value; the parts in the main-boost colours
    /// Cooking's filter uses (BarForm.CategoryColour: health, stamina, eitr), the line under it their values in game units ("Health 160", "Stamina
    /// 117"), the thin line under the bar the meals against the most eaten row's (the number's own length). The caption's words say where the top combinations lean ("Going hard on stamina: 3 of your top 5 lean on stamina."), worked out
    /// from the same food values (DishBoostOf per combination): about foods, never a score. Past CombosTop rows the rest fold into one row. Your own
    /// book only, in All and the day windows (the record keeps the last 40 days per day); with no meal in the window no island (the page's own empty
    /// line speaks). Not recorded on this book yet, or nothing recorded: the island's head with the house sentences.
    /// </summary>
    public static partial class PanelModel
    {
        public const string CombosTitle = "Your combinations", ComboTone = "combo", ComboDishKind = "dish";
        /// <summary>The combination rows a window shows; past this the rest fold into one "n other combinations" row.</summary>
        public const int CombosTop = 8;
        /// <summary>How many of the top combinations the focus line reads.</summary>
        public const int CombosFocusTop = 5;
        public const string CombosNoneYet = "The foods you have on together show here from your next meal.";
        public const string CombosNotRecordedTitle = "Not recorded yet";
        public const string CombosNotRecordedLine = "The foods you have on together are counted on this PC from your next meal.";

        public const string OtherCombosTitle = "Other combinations";
        public static string OtherCombos(int n) => n + (n == 1 ? " other combination" : " other combinations");

        /// <summary>The boosts a combination's bar is split by, in their fixed order, with their words.</summary>
        static readonly (string id, string label)[] ComboBoosts = { ("health", "Health"), ("stamina", "Stamina"), ("eitr", "Eitr") };

        /// <summary>
        /// The caption's words on where the top combinations lean, from the game's food values summed over each (DishBoostOf: the biggest of health,
        /// stamina and eitr, balanced when the second is close): a lean most of them share, "Going hard on stamina: 3 of your top 5 lean on stamina.";
        /// none most share, "A mix: of your top 4, 2 lean on health, 1 on stamina and 1 is balanced."; one combination, "Your combination leans on
        /// stamina."; null when no food value is known. Plain words about foods, never a score.
        /// </summary>
        public static string CombosFocus(IList<(float health, float stamina, float eitr)> top)
        {
            var leans = (top ?? new List<(float, float, float)>()).Select(x => DishBoostOf(x.health, x.stamina, x.eitr)).Where(l => l != null).ToList();
            if (leans.Count == 0) return null;
            int n = leans.Count;
            string[] order = { "health", "stamina", "eitr", "balanced" };
            var groups = leans.GroupBy(l => l).Select(g => (lean: g.Key, k: g.Count())).OrderByDescending(g => g.k).ThenBy(g => Array.IndexOf(order, g.lean)).ToList();
            string Says(string lean, int k, bool first) => lean == "balanced" ? (k == 1 ? "1 is balanced" : k + " are balanced")
                                                                              : k + (first ? (k == 1 ? " leans on " : " lean on ") : " on ") + lean;
            if (n == 1) return groups[0].lean == "balanced" ? "Your combination is balanced." : "Your combination leans on " + groups[0].lean + ".";
            var (top1, k1) = groups[0];
            if (k1 * 2 > n)
            {
                var head = top1 == "balanced" ? "Keeping it balanced" : "Going hard on " + top1;
                var who = k1 == n ? "all your top " + n : k1 + " of your top " + n;
                var verb = top1 == "balanced" ? (k1 == 1 ? " is balanced" : " are balanced") : (k1 == 1 ? " leans on " : " lean on ") + top1;
                return head + ": " + who + verb + ".";
            }
            var parts = groups.OrderBy(g => Array.IndexOf(order, g.lean)).ToList();
            var said = new List<string>(); var leanSaid = false;
            foreach (var g in parts) { said.Add(Says(g.lean, g.k, !leanSaid && g.lean != "balanced")); if (g.lean != "balanced") leanSaid = true; }
            var list = said.Count == 1 ? said[0] : string.Join(", ", said.Take(said.Count - 1)) + " and " + said[said.Count - 1];
            return "A mix: of your top " + n + ", " + list + ".";
        }

        /// <summary>A combination's bar: one part per boost it has, as long as its value (its share of the summed food values; the row's Fraction puts
        /// the whole on the list's one scale), in the boost's colour. Its value in game units (ComboValue) is the line's words ("Health 160", Text) and
        /// what pointing at the part says (Value); no number inside (Tone ComboTone: the number in front is meals). Empty when no food value is known.</summary>
        public static List<Block> ComboParts(float health, float stamina, float eitr)
        {
            var v = new[] { health, stamina, eitr }; var sum = v.Where(x => x > 0).Sum();
            var parts = new List<Block>();
            if (sum <= 0) return parts;
            for (int k = 0; k < ComboBoosts.Length; k++)
                if (v[k] > 0) parts.Add(new Block { Kind = "part", Id = ComboBoosts[k].id, Title = ComboBoosts[k].label, Colour = CategoryColour(ComboBoosts[k].id), Fraction = (float)(v[k] / sum),
                                                    Value = ComboValue(v[k]), Text = ComboValue(v[k]), Tone = ComboTone });
            return parts;
        }

        /// <summary>A food value as the game's food tooltip says it: whole game units (the tooltip prints the item's m_food as it is, whole in the game's
        /// foods, and your current maximum as ToString("0"), rounded half away from zero).</summary>
        public static string ComboValue(double v) => N(Math.Round(Math.Max(0, v), MidpointRounding.AwayFromZero));

        /// <summary>A combination's total: its food values summed (what its bar's length stands for).</summary>
        public static double ComboTotal(MealCombo c) => c == null ? 0 : Math.Max(0, c.Health) + Math.Max(0, c.Stamina) + Math.Max(0, c.Eitr);

        /// <summary>
        /// The island's blocks on Meals (your own book): its head ("Your combinations", "Recorded from 18 October · this PC"), then the rows with the
        /// focus line, or the house sentences. <paramref name="day"/>: the day window's copy of the input (its Window says the local days), null in All.
        /// <paramref name="meals"/>: the meals the page counts in the window; none: no island.
        /// </summary>
        public static List<Block> CombosIsland(PanelInput input, PanelInput day, TimeWindow w, double meals)
        {
            var blocks = new List<Block>();
            if (input == null || !input.IsSelf || meals <= 0) return blocks;
            var rec = input.MealCombos;
            var head = new Block { Kind = "section", Title = CombosTitle };
            blocks.Add(head);
            if (rec == null || !rec.Begun)
            {
                blocks.Add(Empty(CombosNotRecordedTitle, CombosNotRecordedLine));
                return blocks;
            }
            // the island says its own start in its head (as Crafting's Upgrades per item does with nothing to label): its blocks carry no source
            // tag, so the page's own "Recorded from" (the meals, from the install) stays one line in the heading, never said twice
            var window = day?.Window;
            if (window == null) head.Text = RecordedFromLine(input, rec.FromUtc);
            var counts = window != null ? rec.Between(window.From, window.To)
                                        : rec.Combos.Values.Where(c => c.Count > 0).ToDictionary(c => c.Key, c => c.Count, StringComparer.Ordinal);
            var today = LocalToday(input);
            if (window != null)   // a window reaching back before the record's day counts: from when it counts them
            {
                var kept = rec.DaysFrom(today, t => Local(input, t));
                if (window.From < kept) head.Note = KeptFromLine("Combinations", kept, today);
            }
            if (counts.Count == 0)
            {
                blocks.Add(Empty(window == null ? NothingFrom(input, rec.FromUtc) : "Nothing " + InWindowWords(w), CombosNoneYet));
                if (head.Note != null) blocks.Add(new Block { Kind = "note", Text = head.Note });
                head.Note = null;
                return blocks;
            }
            var all = counts.Select(kv => (key: kv.Key, n: kv.Value, c: rec.Combos.TryGetValue(kv.Key, out var c) ? c : null))
                            .OrderBy(x => x.key == DeedLog.OtherToken ? 1 : 0).ThenByDescending(x => x.n).ThenBy(x => x.key, StringComparer.Ordinal).ToList();
            var named = all.Where(x => x.key != DeedLog.OtherToken && x.c != null).ToList();
            var shown = named.Count > CombosTop || named.Count < all.Count ? named.Take(CombosTop - 1).ToList() : named;
            var rest = all.Except(shown).ToList();
            var max = Math.Max(shown.Select(x => x.n).DefaultIfEmpty(0).Max(), rest.Sum(x => x.n));
            // the bars' one scale: the biggest total shown is the full track (0.9.2: real numbers, a stronger combination a longer bar)
            var most = shown.Select(x => ComboTotal(x.c)).DefaultIfEmpty(0).Max();
            string tag = null, src = null;   // see the head: no source tag on the island's numbers
            var rows = shown.Select(x => new Block
            {
                Kind = "source", Id = x.key, Tone = ComboTone, Title = string.Join(", ", x.c.Foods.Select(f => DishTitle(input, f))), Icon = x.c.Foods.Length > 0 ? "item:" + x.c.Foods[0] : "",
                Value = N(x.n), Fraction = most > 0 ? (float)(ComboTotal(x.c) / most) : 0, Fraction2 = (float)(x.n / (double)max), Src = src, Source = tag,
                Link = HasMealPlan(input) && HasFoods(input) ? PlanLoadLink(x.c.Foods) : null,   // 0.9.2: a click loads it into Plan a meal (0.9.5: opens Meal plan with its foods in the slots, Chapters/MealPlanModel.cs)
                Items = x.c.Foods.Select(f => new Block { Kind = ComboDishKind, Id = f, Icon = "item:" + f, Title = DishTitle(input, f) })
                         .Concat(ComboParts(x.c.Health, x.c.Stamina, x.c.Eitr)).ToList(),
            }).ToList();
            if (rest.Count > 0)
            {
                var folded = rest.Sum(x => x.n);
                // the record's own rest (past its 200) holds combinations it did not name: no number then, never one too few. Its meals only: no
                // bar, as the folded combinations have no one food value to put on the totals' scale (0.9.2)
                var title = rest.Any(x => x.key == DeedLog.OtherToken) ? OtherCombosTitle : OtherCombos(rest.Count);
                rows.Add(new Block
                {
                    Kind = "source", Id = FoldId, Title = title, Icon = "", Tone = DamagePlayerTone, Value = N(folded), Fraction = 0, Fraction2 = (float)(folded / (double)max), Src = src, Source = tag,
                    Items = new List<Block>(),
                });
            }
            var focus = CombosFocus(named.Take(CombosFocusTop).Select(x => (x.c.Health, x.c.Stamina, x.c.Eitr)).ToList());
            blocks.Add(new Block { Kind = "sources", Tone = ComboTone, Title = "", Text = focus, Items = rows });
            if (head.Note != null) { blocks.Add(new Block { Kind = "note", Text = head.Note }); head.Note = null; }
            return blocks;
        }
    }
}
