// RESILIENCE-06 item 1: a count read back from text is culture-proof and never throws (PanelModel.ParseCount), and Cooking's
// "enjoyed by fellows" is summed from the numbers, not the shown text: the shown "1 000" (a no-break space) once closed the panel
// on its default page, Deeds > Overview. Plus a source guard: no culture-bound number parse anywhere in the mod.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Hearthwoven;
using Hearthwoven.Panel;

static class ParseCountTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static string Here([CallerFilePath] string path = "") => path;
    static IEnumerable<Block> All(IEnumerable<Block> blocks) => (blocks ?? Enumerable.Empty<Block>()).SelectMany(b => new[] { b }.Concat(All(b.Items)));

    public static int Run(PanelInput input, PanelInput edda)
    {
        fails = 0;

        // ---------- the reader ----------
        var cases = new (string text, double want)[]
        {
            ("1 000", 1000), ("1 000", 1000), ("1 000", 1000), ("1 000", 1000), ("1 000", 1000), ("1,000", 1000), ("1'000", 1000),
            ("12 345 678", 12345678), ("43.8", 43.8), ("0", 0), ("−5", -5), ("-5", -5), (" 7 ", 7),
            (PanelModel.Number(4180), 4180), (PanelModel.Number(1234567), 1234567),
            ("", 0), (null, 0), ("abc", 0), ("NaN", 0), ("Infinity", 0), ("-Infinity", 0), ("1e400", 0),
        };
        var keep = CultureInfo.CurrentCulture;
        var wrong = new List<string>();
        try
        {
            foreach (var culture in new[] { "en-US", "de-DE", "fr-FR", "sv-SE", "nb-NO", "ru-RU", "ar-SA" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                foreach (var (text, want) in cases)
                {
                    double got;
                    try { got = PanelModel.ParseCount(text); } catch (Exception e) { wrong.Add(culture + " '" + text + "' threw " + e.GetType().Name); continue; }
                    if (Math.Abs(got - want) > 1e-9) wrong.Add(culture + " '" + text + "' -> " + got + ", not " + want);
                }
                if (PanelModel.ParseCount(PanelModel.Number(98765)) != 98765) wrong.Add(culture + ": Number and ParseCount do not round-trip");
            }
        }
        finally { CultureInfo.CurrentCulture = keep; }
        Check(wrong.Count == 0, "ParseCount: no-break, narrow, thin, figure and plain spaces, commas and apostrophes as thousands marks, a point as decimal, U+2212 as minus; " +
                                "empty, text, NaN and infinity read 0; never throws; the same under seven cultures" + (wrong.Count > 0 ? ": " + string.Join("; ", wrong.Take(4)) : ""));

        // ---------- Cooking from the numbers: a fellow who enjoyed this cook's food 1 234 times ----------
        var keepF = input.Fellows; var keepE = edda.Fellows;
        try
        {
            input.Fellows = new List<PanelInput> { edda }; edda.Fellows = new List<PanelInput> { input };
            var rich = DeedsTests.Rich(input);
            if (rich.PlayerId == 0) rich.PlayerId = 4242;
            var before = PanelModel.EnjoyedByFellows(rich);
            var feasts = new SessionEvents();
            SessionEvents.Add(feasts.AteFromFeastOf, rich.PlayerId.ToString(CultureInfo.InvariantCulture) + "|$item_feastmeadows", 1234);
            rich.Fellows = (rich.Fellows ?? new List<PanelInput>()).Concat(new[] { new PanelInput { PlayerName = "Ulf", IsSelf = false, Events = feasts } }).ToList();
            var total = PanelModel.EnjoyedByFellows(rich);
            Check(total == before + 1234 && total >= 1000, "cooking: enjoyed by fellows is the sum of the numbers, " + before + " + 1234 = " + total + " (over 1 000, where the shown text has a no-break space)");

            PanelView overview = null, cooking = null; string err = null;
            try
            {
                overview = PanelModel.Build(rich, new PanelState());   // the default page: Deeds > Overview
                cooking = PanelModel.Build(rich, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = "cooking" } });
            }
            catch (Exception e) { err = e.GetType().Name + ": " + e.Message; }
            var card = overview == null ? null : All(PanelModel.Content(overview)).FirstOrDefault(b => b.Kind == "card" && b.Id == "Deeds/cooking");
            Check(err == null && card != null && card.Items.Any(n => n.Value == PanelModel.Number(total) && n.Title == "enjoyed by fellows"),
                  "overview: the panel opens on Deeds > Overview with 1 000+ servings enjoyed, the cook's card says " + PanelModel.Number(total) + " enjoyed by fellows" + (err != null ? " (threw " + err + ")" : "") +
                  (card == null || !card.Items.Any(n => n.Title == "enjoyed by fellows") ? " [cards: " + string.Join(" | ", (overview == null ? new List<Block>() : All(PanelModel.Content(overview)).Where(b => b.Kind == "card").ToList()).Select(b => b.Id + "=" + b.Value + " " + b.Text + " / " + string.Join(",", (b.Items ?? new List<Block>()).Select(n => n.Value + " " + n.Title)))) + "]" : ""));
            var head = cooking == null ? null : PanelModel.Content(cooking).FirstOrDefault(b => b.Kind == "section" && (b.Title ?? "").StartsWith("Who enjoyed"));
            Check(head != null && head.Value == PanelModel.Number(total), "cooking: the 'Who enjoyed your food' head carries the same " + PanelModel.Number(total) + " (one source)");
        }
        finally { input.Fellows = keepF; edda.Fellows = keepE; }

        // ---------- source guard ----------
        var src = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here()), "..", "src"));
        var parse = new Regex(@"\b(double|float|decimal|int|long|short|uint|ulong|DateTime)\.(Try)?Parse\(");
        var bad = new List<string>();
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "MiniJson.cs") continue;   // the JSON reader: its own grammar, already invariant
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                if (l.Contains("AllowThousands")) bad.Add(Path.GetFileName(file) + ":" + (i + 1) + " AllowThousands");
                else if (parse.IsMatch(l) && !Regex.IsMatch(l, @"\bInv\b|InvariantCulture")) bad.Add(Path.GetFileName(file) + ":" + (i + 1));
            }
        }
        Check(bad.Count == 0 && Directory.Exists(src), "source: every number or date parse in the mod names the invariant culture, and no AllowThousands parse of shown text (use PanelModel.ParseCount)" +
                                                        (bad.Count > 0 ? ": " + string.Join(", ", bad) : ""));
        return fails;
    }
}
