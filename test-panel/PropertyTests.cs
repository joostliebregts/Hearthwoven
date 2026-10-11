// Rules that hold on every page (0.8.5 test cleanup, work/hearthwoven-0.8.5/TEST-AUDIT.md parts 4 and 6). Each is one check over every
// page of every book the sample shows (Rowan's own and each fellow's copy as PanelUi wires them), each view of a switch, in every window
// chip the page offers, in place of the same rule restated page by page. Each rule is proven by seeded breaks: one real page of the walk is
// rebuilt with the fault the rule is for, and the rule has to find it.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hearthwoven;
using Hearthwoven.Panel;

static class PropertyTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => (bs ?? Enumerable.Empty<Block>()).SelectMany(b => new[] { b }.Concat(Every(b.Items)));

    internal record Page(string Who, bool Own, string Name, TimeWindow? Window, PanelView View, Func<PanelView> Rebuild);

    /// <summary>Every page of every book of the sample, each view of a switch, in every window chip the page offers (greyed ones left out).</summary>
    internal static List<Page> Walk(DateTime now)
    {
        var pages = new List<Page>();
        foreach (var (who, book) in FullDump.Books(PanelSample.Full(now)))
        {
            var player = who == SampleWorld.Rowan ? "" : who;
            foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
                foreach (var l in PanelModel.Build(book, new PanelState { Chapter = ch, Player = player }).List)
                {
                    PanelState At(TimeWindow? w, string sw = null, string view = null)
                    {
                        var st = new PanelState { Chapter = ch, Player = player, Page = { [ch] = l.Id } };
                        if (w.HasValue) { st.Window = w.Value; st.WindowPicked = true; }   // as a chip press: a Deeds page shows All until one is picked
                        if (sw != null) st.View[sw] = view;
                        return st;
                    }
                    var first = PanelModel.Build(book, At(null));
                    var windows = first.Windows.Count == 0 ? new TimeWindow?[] { null } : first.Windows.Where(c => !c.Disabled).Select(c => (TimeWindow?)Enum.Parse<TimeWindow>(c.Id)).ToArray();
                    foreach (var w in windows)
                    {
                        var v = PanelModel.Build(book, At(w));
                        var sw = PanelModel.Content(v).FirstOrDefault(b => b.Kind == "switch" && b.Items != null && b.Items.Count > 1);
                        if (sw == null) pages.Add(new Page(who, player == "", ch + "/" + l.Id, v.ShownWindow, v, () => PanelModel.Build(book, At(w))));
                        else foreach (var o in sw.Items)
                                pages.Add(new Page(who, player == "", ch + "/" + l.Id + "/" + o.Id, v.ShownWindow, PanelModel.Build(book, At(w, sw.Id, o.Id)), () => PanelModel.Build(book, At(w, sw.Id, o.Id))));
                    }
                }
        }
        return pages;
    }

    /// <summary>The pages whose box explains the page itself, the same in every window and in a fellow's book (its forms, the feed, the
    /// windows): Battle's pages but the Overview, and Deeds > Recent. Every other page's box is about All's numbers and their dates.</summary>
    static bool BoxExplainsPage(Page p) => p.Name.StartsWith("Battle/") && !p.Name.StartsWith("Battle/overview") || p.Name == "Deeds/recent";
    static bool HasBox(PanelView v) => v.AboutNumbers != null || PanelModel.Content(v).Any(b => b.Kind == "aboutnumbers");
    static IEnumerable<string> DatedLines(PanelView v) =>
        (v.AboutNumbers?.Items ?? new List<Block>()).Where(l => (l.Title ?? "").StartsWith("Before ") || (l.Title ?? "").StartsWith("From ")).Select(l => l.Title);
    static IEnumerable<string> NumberLabels(PanelView v) => new[] { v.HeadingRecordedFrom }.Concat(Every(v.Blocks).Select(b => b.RecordedFrom)).Where(s => !string.IsNullOrEmpty(s));
    /// <summary>A label that dates a number from when it was counted on a PC ("Recorded from 1 October · this PC", "from 1 October"); a
    /// fellow's own label says only where ("Recorded on Edda's PC", rule E).</summary>
    static readonly Regex DateLabel = new Regex(@"Recorded from|this PC|^from \d");

    // ---------- rule W.1 (REDESIGN-RULES.md): a short or day window carries no dates, no "Earlier counts" and none of All's box ----------
    internal static IEnumerable<string> ShortWindow(Page p)
    {
        if (!p.Own || p.Window == null || p.Window == TimeWindow.SinceInstall) yield break;
        var v = p.View;
        if (NumberLabels(v).Any()) yield return "a number labelled \"" + NumberLabels(v).First() + "\"";
        // Battle > Deaths: its lifetime deaths (the game's count with what was counted since) stand in every window, with their note
        if (Says(v, PanelModel.EarlierIncomplete) && p.Name != "Battle/deaths") yield return "\"" + PanelModel.EarlierIncomplete + "\"";
        if (HasBox(v) && !BoxExplainsPage(p)) yield return "an About these numbers box";
        // Your armor's box says from when the armor is counted, in every window (ArmourPanelTests)
        if (DatedLines(v).Any() && p.Name != "Battle/defense/armour") yield return "a dated box line (" + string.Join(", ", DatedLines(v)) + ")";
    }

    // ---------- a fellow's book (hard case 6): their copy carries no dates; never "since install"; it says whose it is ----------
    /// <summary>The page's own lines (not the chips nor their reasons): heading, scope, the strip's note, the blocks and the box.</summary>
    static IEnumerable<string> OwnLines(PanelView v) => new[] { v.Heading, v.Scope, v.StripNote, v.ShareNote, v.HeadingWindow }
        .Concat(Every(v.Blocks).Concat(Every(v.AboutNumbers?.Items)).SelectMany(b => new[] { b.Title, b.Value, b.Value2, b.Text, b.Note })).Where(s => !string.IsNullOrEmpty(s));
    internal static IEnumerable<string> Fellow(Page p)
    {
        if (p.Own) yield break;
        var v = p.View;
        if (NumberLabels(v).Any(DateLabel.IsMatch)) yield return "a number dated \"" + NumberLabels(v).First(DateLabel.IsMatch) + "\"";
        if (DatedLines(v).Any()) yield return "a dated box line (" + string.Join(", ", DatedLines(v)) + ")";
        if (Says(v, PanelModel.EarlierIncomplete)) yield return "\"" + PanelModel.EarlierIncomplete + "\" (your own box's line)";
        if (HasBox(v) && !BoxExplainsPage(p)) yield return "an About these numbers box";
        if (Says(v, "since install")) yield return "\"since install\"";
        if (!OwnLines(v).Any(t => t.Contains(p.Who))) yield return "nothing on the page but the chips says whose book it is";
    }

    // ---------- parts add up to their whole, and shares to 1, on every bar and total (TEST-AUDIT.md gap 4) ----------
    static readonly Regex Plain = new Regex(@"^[0-9   ,]+(\.[0-9]+)?$");
    /// <summary>The blocks whose numbered parts make their whole: the bars (composition, the filter's facet bars, damage mixes, Together's
    /// categories, the source bars, a group's rows and mixes) and the totals over parts (biome tiles, damage grids and cells, a fight's
    /// foes, a gift's lines, a death's hits, a fight's received rows, the cooking ledger's rows of dishes).</summary>
    static readonly HashSet<string> Summed = new HashSet<string> { "composition", "facetbar", "dmgmix", "category", "source", "grouprows", "member", "biomes", "damagegrid", "dmgtype", "fight", "gift", "death", "fightreceived", "row" };
    /// <summary>The bars whose parts carry their share of the whole (Fraction).</summary>
    static readonly HashSet<string> Shared = new HashSet<string> { "composition", "facetbar", "dmgmix", "category", "source", "grouprows", "member", "journey", "leg" };
    internal static IEnumerable<string> Sums(Page p)
    {
        foreach (var b in Every(p.View.Blocks).Where(b => b.Items != null && b.Items.Count > 1))
        {
            if (Summed.Contains(b.Kind) && !string.IsNullOrEmpty(b.Value) && Plain.IsMatch(b.Value.Trim()) && (b.Kind != "row" || b.Items.All(i => i.Kind == "chip")))
            {
                // 0.8.5: a damage part by the after rule holds what got through; what it adds to its whole is what was swung (Whole)
                string Of(Block i) => i.Whole ?? i.Value;
                var parts = b.Items.Where(i => !string.IsNullOrEmpty(Of(i))).ToList();
                if (parts.Count >= 2 && parts.All(i => Plain.IsMatch(Of(i).Trim())))
                {
                    double sum = parts.Sum(i => PanelModel.ParseCount(Of(i).Trim())), whole = PanelModel.ParseCount(b.Value.Trim());
                    if (Math.Abs(sum - whole) > 0.5 * parts.Count) yield return b.Kind + " \"" + b.Title + "\": its parts add up to " + sum + ", it says " + b.Value;
                }
            }
            if (Shared.Contains(b.Kind) && b.Items.Any(i => i.Fraction > 0))
            {
                var share = b.Items.Sum(i => (double)i.Fraction);
                if (Math.Abs(share - 1) > 0.01) yield return b.Kind + " \"" + b.Title + "\": its shares add up to " + share.ToString("0.###");
            }
        }
    }

    // ---------- "Earlier counts may be incomplete." on your own book: once a page at most, in its About box only (0.8 layout D+) ----------
    /// <summary>Joost on the board: no "i" mark beside a number, no hover; the line comes off the page and lives only in About these numbers,
    /// said once for the page however many numbers it is about (0.8.5: Company > Together's category chips too).</summary>
    internal static IEnumerable<string> EarlierOnce(Page p)
    {
        if (!p.Own) yield break;
        var v = p.View; var times = TimesSaid(v, PanelModel.EarlierIncomplete);
        if (times > 1) yield return "\"" + PanelModel.EarlierIncomplete + "\" said " + times + " times";
        var inBox = (v.AboutNumbers?.Items ?? new List<Block>()).Count(l => (l.Text ?? "").Contains(PanelModel.EarlierIncomplete));
        if (times > inBox) yield return "\"" + PanelModel.EarlierIncomplete + "\" on the page, outside the About box";
    }

    public static int Run(DateTime now)
    {
        fails = 0;
        var pages = Walk(now);
        var rules = new (string name, Func<Page, bool> applies, Func<Page, IEnumerable<string>> faults)[]
        {
            ("rule W.1: a short or day window of your own book dates no number, says no \"Earlier counts\" and shows none of All's box",
                p => p.Own && p.Window != null && p.Window != TimeWindow.SinceInstall, ShortWindow),
            ("a fellow's book: no dates, no \"Earlier counts\", no box of All's numbers, never \"since install\", and the page says whose book it is",
                p => !p.Own, Fellow),
            ("parts add up to their whole and shares to 1, on every bar and total", p => true, Sums),
            ("your own book: \"Earlier counts may be incomplete.\" once a page at most, in the About box only, never beside a number", p => p.Own, EarlierOnce),
        };
        foreach (var (name, applies, faults) in rules)
        {
            var bad = pages.SelectMany(p => faults(p).Select(f => p.Who + " " + p.Name + (p.Window != null ? " (" + p.Window + ")" : "") + ": " + f)).ToList();
            foreach (var b in bad.Take(20)) System.Console.WriteLine("  problem: " + b);
            Check(bad.Count == 0 && pages.Count(applies) > 50, "every page, " + name + " (" + pages.Count(applies) + " pages" + (bad.Count > 0 ? ", " + bad.Count + " problems" : "") + ")");
        }

        // the rules catch what they are for: a real page of the walk rebuilt with the fault, and the rule names it
        void Breaks(string what, int rule, Func<Page, bool> which, Action<PanelView> tamper)
        {
            var page = pages.FirstOrDefault(which);
            if (page == null) { Check(false, "page rules, seeded break: " + what + " (no such page in the walk)"); return; }
            var v = page.Rebuild(); tamper(v);
            var found = rules[rule].faults(page with { View = v }).ToList();
            Check(found.Count > 0, "page rules, seeded break: " + what + " is found" + (found.Count > 0 ? " (" + found[0] + ")" : " (not found)"));
        }
        Block First(PanelView v, Func<Block, bool> where) => Every(v.Blocks).First(where);
        Breaks("Farming in 7 days keeps All's box", 0, p => p.Own && p.Name == "Deeds/farming" && p.Window == TimeWindow.SevenDays,
            v => v.AboutNumbers = new Block { Kind = "aboutnumbers", Items = new List<Block> { new Block { Kind = "aboutline", Title = "Before 1 October", Text = "The game's own count." } } });
        Breaks("Woodcutting's hero in 7 days dated \"Recorded from\"", 0, p => p.Own && p.Name == "Deeds/woodcutting" && p.Window == TimeWindow.SevenDays,
            v => First(v, b => b.Kind == "hero").RecordedFrom = "Recorded from 1 October · this PC");
        Breaks("Mining's hero this session noting \"Earlier counts may be incomplete.\"", 0, p => p.Own && p.Name == "Deeds/mining" && p.Window == TimeWindow.Session,
            v => First(v, b => b.Kind == "hero").Note = PanelModel.EarlierIncomplete);
        Breaks("Edda's Cooking with a dated box", 1, p => p.Who == SampleWorld.Edda && p.Name == "Deeds/cooking",
            v => v.AboutNumbers = new Block { Kind = "aboutnumbers", Items = new List<Block> { new Block { Kind = "aboutline", Title = "From 3 October", Text = "Hearthwoven counted every dish on this PC." } } });
        Breaks("Tor's Foes scope saying \"since install\"", 1, p => p.Who == SampleWorld.Tor && p.Name.StartsWith("Battle/foes"),
            v => v.Scope = "Tor, since install");
        Breaks("Finch's Woodcutting no longer naming Finch", 1, p => p.Who == SampleWorld.Finch && p.Name == "Deeds/woodcutting",
            v => { v.Scope = v.StripNote = v.ShareNote = null; foreach (var b in Every(v.Blocks)) { b.Title = b.Title?.Replace(SampleWorld.Finch, "x"); b.Text = b.Text?.Replace(SampleWorld.Finch, "x"); b.Note = b.Note?.Replace(SampleWorld.Finch, "x"); } });
        Breaks("a part of Woodcutting's wood bar more than its whole allows", 2, p => p.Own && p.Name == "Deeds/woodcutting" && p.Window == TimeWindow.SinceInstall,
            v => { var c = First(v, b => b.Kind == "composition" && b.Items.Count > 1); c.Items[0].Value = PanelModel.Number(PanelModel.ParseCount(c.Items[0].Value) + c.Items.Count); });
        Breaks("a filter bar's shares over 1", 2, p => p.Own && Every(p.View.Blocks).Any(b => b.Kind == "facetbar" && b.Items.Count > 1 && b.Items.Any(i => i.Fraction > 0)),
            v => First(v, b => b.Kind == "facetbar" && b.Items.Count > 1 && b.Items.Any(i => i.Fraction > 0)).Items[0].Fraction += 0.2f);
        Breaks("Mining's hero (All) noting \"Earlier counts may be incomplete.\" beside its number", 3, p => p.Own && p.Name == "Deeds/mining" && p.Window == TimeWindow.SinceInstall,
            v => First(v, b => b.Kind == "hero").Note = PanelModel.EarlierIncomplete);
        return fails;
    }
}
