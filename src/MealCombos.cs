using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>One combination of foods you had on together (0.9, Meals' "Your combinations"): the foods (item prefabs, sorted, joined by '+'),
    /// how many meals left you with exactly these on, the first and last local day, the game's food values summed over the foods (as they were
    /// last eaten), and the meals per local day of the last MealCombos.KeepDays days (the page's day windows).</summary>
    public sealed class MealCombo
    {
        public string Key = "";
        public int Count;
        public DateTime First, Last;
        public float Health, Stamina, Eitr;
        public readonly SortedDictionary<DateTime, int> Days = new SortedDictionary<DateTime, int>();

        /// <summary>The foods (item prefabs) of this combination; none for the folded rest (DeedLog.OtherToken).</summary>
        public string[] Foods => Key == DeedLog.OtherToken || Key.Length == 0 ? new string[0] : Key.Split(MealCombos.Joiner);
    }

    /// <summary>
    /// 0.9 (Joost in game, 18 Oct: "show the food combinations that we had in game ... you're going hard on stamina and this is your best combination
    /// that you've used the most"): each time you eat (ClientHooks.Eat, after the game took the food), the foods you then have on (Player.GetFoods,
    /// one to three) as one combination. Per combination the count, first and last local day, the game's food values summed over its foods, and the
    /// meals per local day of the last KeepDays days, so Meals' Today, 7 days and 30 days read it. Bounded: at most MaxCombos combinations; a new
    /// one past that counts in one "other combinations" line (DeedLog.OtherToken), so the total stays exact. Your own PC only, never shared; kept in
    /// the local totals file (LocalTotals, top-level "mealCombos"), from the day 0.9 is installed (FromUtc). Pure C#, tested in test/RecordTests.cs (MC).
    /// </summary>
    public sealed class MealCombos
    {
        public const int MaxCombos = 200, KeepDays = 40, MaxFoods = 3;
        public const char Joiner = '+';

        public DateTime FromUtc = DateTime.MinValue;
        public readonly Dictionary<string, MealCombo> Combos = new Dictionary<string, MealCombo>(StringComparer.Ordinal);

        public bool IsEmpty => FromUtc == DateTime.MinValue && Combos.Count == 0;
        public bool Begun => FromUtc > DateTime.MinValue;
        /// <summary>Every meal recorded (whole and folded).</summary>
        public int Total => Combos.Values.Sum(c => c.Count);

        /// <summary>The key of a set of foods: the names sorted (ordinal), each once, at most MaxFoods; a '+' in a name becomes '_'. null: no food.</summary>
        public static string KeyOf(IEnumerable<string> foods)
        {
            var names = (foods ?? Enumerable.Empty<string>()).Where(f => !string.IsNullOrEmpty(f)).Select(f => f.Replace(Joiner, '_'))
                                                              .Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal).Take(MaxFoods).ToList();
            return names.Count == 0 ? null : string.Join(Joiner.ToString(), names);
        }

        /// <summary>One meal more: the foods you now have on, their food values summed, the local day it was eaten. False when there is no food.</summary>
        public bool Add(IEnumerable<string> foods, float health, float stamina, float eitr, DateTime localDay)
        {
            var key = KeyOf(foods);
            if (key == null) return false;
            var day = localDay.Date;
            if (!Combos.TryGetValue(key, out var c))
            {
                if (Combos.Count >= MaxCombos - 1)   // past the cap: one line for the rest, in the last slot (written and read back with the others)
                {
                    key = DeedLog.OtherToken;
                    if (!Combos.TryGetValue(key, out c)) Combos[key] = c = new MealCombo { Key = key, First = day, Last = day };
                }
                else Combos[key] = c = new MealCombo { Key = key, First = day, Last = day };
            }
            c.Count++;
            if (day < c.First) c.First = day;
            if (day > c.Last) c.Last = day;
            c.Days[day] = (c.Days.TryGetValue(day, out var n) ? n : 0) + 1;
            if (key != DeedLog.OtherToken) { c.Health = Clean(health); c.Stamina = Clean(stamina); c.Eitr = Clean(eitr); }
            foreach (var old in c.Days.Keys.Where(d => d <= day.AddDays(-KeepDays)).ToList()) c.Days.Remove(old);
            return true;
        }

        static float Clean(float v) => float.IsNaN(v) || float.IsInfinity(v) || v < 0 ? 0 : v;

        /// <summary>The meals per combination from local day <paramref name="from"/> to <paramref name="to"/> (both included); only those with a meal.</summary>
        public Dictionary<string, int> Between(DateTime from, DateTime to)
        {
            var d = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in Combos.Values)
            {
                var n = c.Days.Where(kv => kv.Key >= from.Date && kv.Key <= to.Date).Sum(kv => kv.Value);
                if (n > 0) d[c.Key] = n;
            }
            return d;
        }

        /// <summary>The first local day the day counts reach back to for <paramref name="today"/>: the record's first day, or KeepDays back.</summary>
        public DateTime DaysFrom(DateTime today, Func<DateTime, DateTime> toLocal)
        {
            var kept = today.Date.AddDays(1 - KeepDays);
            var from = Begun ? (toLocal != null ? toLocal(FromUtc) : FromUtc).Date : kept;
            return from > kept ? from : kept;
        }

        // ---------- the file (LocalTotals, top-level "mealCombos") ----------

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>{"from":"..","combos":[{"k":"Bread+CarrotSoup","n":7,"first":"2026-10-18","last":"2026-10-20","h":38,"s":115,"e":0,"days":{"2026-10-20":2}}]}</summary>
        public void WriteTo(Json j, string key = "mealCombos")
        {
            j.Key(key).Open();
            if (Begun) j.Str("from", FromUtc.ToString("o", Inv));
            j.Key("combos").OpenArr();
            foreach (var c in Combos.Values.Take(MaxCombos))
            {
                j.Open().Str("k", c.Key).Num("n", c.Count).Str("first", UpgradeLog.Day(c.First)).Str("last", UpgradeLog.Day(c.Last));
                if (c.Health > 0) j.Num("h", c.Health);
                if (c.Stamina > 0) j.Num("s", c.Stamina);
                if (c.Eitr > 0) j.Num("e", c.Eitr);
                j.Key("days").Open();
                foreach (var kv in c.Days.Reverse().Take(KeepDays)) j.Num(UpgradeLog.Day(kv.Key), kv.Value);
                j.Close().Close();
            }
            j.CloseArr();
            j.Close();
        }

        /// <summary>Reads what WriteTo wrote, bounded (MaxCombos combinations, KeepDays days each; anything unreadable skipped). null: an empty record.</summary>
        public static MealCombos ReadFrom(Dictionary<string, object> o)
        {
            var m = new MealCombos();
            if (o == null) return m;
            m.FromUtc = UpgradeLog.Time(MiniJson.Str(o, "from"));
            if (o.TryGetValue("combos", out var cs) && cs is List<object> list)
                foreach (var e in list.OfType<Dictionary<string, object>>())
                {
                    if (m.Combos.Count >= MaxCombos) break;
                    var key = MiniJson.Str(e, "k"); var n = (int)Math.Min(int.MaxValue, Math.Max(0, MiniJson.Num(e, "n")));
                    if (key.Length == 0 || n <= 0 || m.Combos.ContainsKey(key)) continue;
                    if (key != DeedLog.OtherToken && KeyOf(key.Split(Joiner)) != key) continue;   // not a key this writer makes
                    var first = UpgradeLog.Time(MiniJson.Str(e, "first")).Date; var last = UpgradeLog.Time(MiniJson.Str(e, "last")).Date;
                    if (first == DateTime.MinValue.Date || last < first) continue;
                    var c = new MealCombo { Key = key, Count = n, First = first, Last = last, Health = Clean((float)MiniJson.Num(e, "h")), Stamina = Clean((float)MiniJson.Num(e, "s")), Eitr = Clean((float)MiniJson.Num(e, "e")) };
                    var days = MiniJson.Obj(e, "days");
                    if (days != null)
                        foreach (var kv in days)
                        {
                            if (c.Days.Count >= KeepDays) break;
                            var d = UpgradeLog.Time(kv.Key); var k = kv.Value is double x ? (int)Math.Min(int.MaxValue, Math.Max(0, x)) : 0;
                            if (d > DateTime.MinValue && k > 0) c.Days[d.Date] = k;
                        }
                    m.Combos[key] = c;
                }
            return m;
        }
    }
}
