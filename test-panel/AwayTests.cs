// Company > Since you were away (0.7, Chapters/AwayModel.cs, variant C "Group totals first"): the page opens by itself once per session after a
// long break; the group's totals and each fellow's row never show 0, rows are alphabetical, the shares add up to 100 %, what a copy cannot
// tell is left out, a first-seen or quiet fellow gets a one-line row and a count that restarted is left out. The scenario builders (Scenario)
// are also the previews' (Program.cs --dump). Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hearthwoven;
using Hearthwoven.Panel;

static class AwayTests
{
    /// <summary>
    /// The sample world (PanelSample.Full) after a break of <paramref name="awayHours"/>: your previous session ended that long before this one
    /// began; each fellow in <paramref name="who"/> as your PC last saw them then (their mark: every running total at <paramref name="kept"/> of
    /// today's, so the rest grew while you were away), except <paramref name="firstTime"/>, whom your PC never saw before.
    /// </summary>
    public static PanelInput Scenario(DateTime now, double awayHours, string[] who, double kept, string firstTime = null)
    {
        var w = PanelSample.Full(now);
        w.PreviousSessionEndUtc = w.SessionStartUtc.Value.AddHours(-awayHours);
        var grew = w.Fellows.ToDictionary(f => f.PlayerName, f => 1 - kept);
        w.Fellows = w.Fellows.Where(f => who.Contains(f.PlayerName)).ToList();
        // 0.8.5 (While away on one screen with five fellows): a name the sample has no book for is a fresh copy of one of the sample's three,
        // renamed, that grew a share of what that one grew (Ylva as Edda at half, Bryn as Tor at a third, Sigrid as Finch at two thirds)
        var spare = who.Where(n => !grew.ContainsKey(n)).ToList();
        if (spare.Count > 0)
        {
            var copies = SampleWorld.Make(now).copies;
            foreach (var n in spare)
            {
                var (from, part) = Extra.TryGetValue(n, out var e) ? e : (SampleWorld.Edda, 0.5);
                var f = copies[from]; f.PlayerName = n; grew[n] = (1 - kept) * part;
                w.Fellows.Add(f); copies = SampleWorld.Make(now).copies;   // the next one from a fresh world: no copy is shared
            }
        }
        foreach (var f in w.Fellows)
        {
            f.ReceivedUtc = now.AddMinutes(-3);
            if (f.PlayerName == firstTime) { f.SeenBefore = null; continue; }
            var m = FellowMarks.Of(f, w.PreviousSessionEndUtc.Value.AddMinutes(-20));
            foreach (var k in m.Values.Keys.ToList()) m.Values[k] = (float)Math.Floor(m.Values[k] * (1 - grew[f.PlayerName]));
            f.SeenBefore = m;
        }
        return w;
    }

    /// <summary>The sample's further names (BRIEF: sample names only) and whose record each is a copy of, with the share of that one's growth.</summary>
    static readonly Dictionary<string, (string from, double part)> Extra = new Dictionary<string, (string, double)>
    {
        [SampleWorld.Ylva] = (SampleWorld.Edda, 0.5), ["Bryn"] = (SampleWorld.Tor, 1 / 3.0), ["Sigrid"] = (SampleWorld.Finch, 2 / 3.0),
    };
    /// <summary>Five fellows, the most While away shows on one screen; six, the first that scrolls.</summary>
    public static readonly string[] Five = { SampleWorld.Edda, SampleWorld.Finch, SampleWorld.Tor, SampleWorld.Ylva, "Bryn" }, Six = Five.Concat(new[] { "Sigrid" }).ToArray();

    static PanelState AwayState() => new PanelState { Chapter = Chapter.Company, Page = { [Chapter.Company] = PanelModel.AwayPage } };

    static List<Block> Page(PanelInput input) => PanelModel.Content(PanelModel.Build(input, AwayState())).ToList();
    /// <summary>The header block: its Title is the header line, its Items the group's totals.</summary>
    public static Block Totals(PanelInput input) => Page(input).Single(b => b.Kind == PanelModel.AwayTotalsKind);
    /// <summary>One row per fellow (Kind fellow: a bar; quiet: one line).</summary>
    public static List<Block> Rows(PanelInput input) => Page(input).Where(b => b.Kind == PanelModel.AwayRowsKind).SelectMany(b => b.Items).ToList();
    static IEnumerable<Block> Parts(Block row) => (row.Items ?? new List<Block>()).Where(p => p.Kind != "chip");
    static IEnumerable<Block> Chips(Block row) => (row.Items ?? new List<Block>()).Where(p => p.Kind == "chip");
    static IEnumerable<string> Words(Block b) => new[] { b.Title, b.Value, b.Text, b.Note }.Concat((b.Items ?? new List<Block>()).SelectMany(Words)).Where(w => !string.IsNullOrEmpty(w));

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- opens by itself: once per session, at or past the threshold, not when switched off ----------
        var start = now.AddMinutes(-5);
        PanelInput Self(double awayHours) => new PanelInput { SessionStartUtc = start, PreviousSessionEndUtc = start.AddHours(-awayHours) };
        var s = new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = "deaths" } };
        var opened = PanelModel.OpenAway(s, Self(30), true, 24, "s1");
        var onAway = s.Chapter == Chapter.Company && s.PageOf(Chapter.Company) == PanelModel.AwayPage;
        PanelModel.LeaveAway(s);   // closed while still on it
        var backThere = s.Chapter == Chapter.Battle && s.PageOf(Chapter.Battle) == "deaths" && s.PageOf(Chapter.Company) == null;
        var again = PanelModel.OpenAway(s, Self(30), true, 24, "s1");
        Check(opened && onAway && backThere && !again && s.Chapter == Chapter.Battle,
              "away opens: after 30 h away (threshold 24) the first opening turns to Since you were away; closing there puts the book back on Battle > Deaths; the next opening of the session stays");
        var s2 = new PanelState(); PanelModel.OpenAway(s2, Self(30), true, 24, "s2"); s2.Page[Chapter.Company] = "food"; PanelModel.LeaveAway(s2);
        Check(s2.Chapter == Chapter.Company && s2.PageOf(Chapter.Company) == "food", "away opens: a player who went to another page keeps it when the book closes");
        Check(!PanelModel.OpenAway(new PanelState(), Self(30), false, 24, "s3") && !PanelModel.OpenAway(new PanelState(), Self(20), true, 24, "s4") &&
              !PanelModel.OpenAway(new PanelState(), new PanelInput { SessionStartUtc = start }, true, 0, "s5") && PanelModel.OpenAway(new PanelState(), Self(24), true, 24, "s6"),
              "away opens: not when ShowWhileAway is off, not after 20 h (threshold 24), not in the first session on this PC; exactly at the threshold it does");
        var s7 = new PanelState(); var early = PanelModel.OpenAway(s7, new PanelInput { PreviousSessionEndUtc = start.AddDays(-2) }, true, 24, "s7");
        Check(!early && PanelModel.OpenAway(s7, Self(48), true, 24, "s7"), "away opens: an opening before the session start is known decides nothing yet; the next one does");

        // ---------- the page: header, the group's totals, one row per fellow ----------
        var three = Scenario(now, 72, new[] { "Edda", "Finch", "Tor" }, 0.8);
        var totals = Totals(three); var rows = Rows(three);
        var dump = totals.Title + " | " + string.Join(", ", totals.Items.Select(t => t.Value + " " + t.Title)) + "\n" +
                   string.Join("\n", rows.Select(r => r.Title + " " + (r.Value ?? r.Text) + ": " + string.Join(", ", Parts(r).Select(p => p.Id + " " + p.Value)) + " | for you " + string.Join(", ", Chips(r).Select(c => c.Title + " " + c.Value))));
        Check(Regex.IsMatch(totals.Title, @"^You were away 3 days, since [A-Z][a-z]+day \d{2}:\d{2}$") && totals.Items.Select(t => t.Id).SequenceEqual(new[] { "built", "foes", "mined", "wood", "dishes" }),
              "away page: 'You were away 3 days, since <weekday> <time>', then the group's five totals in their fixed order: " + totals.Title);
        // rc2 (Joost on rc1: "1 hour away · since 10 Oct" is unclear): when your last session ended, as people say it, in your own time
        var at = new DateTime(2026, 10, 10, 12, 40, 0);   // a Saturday
        var said = new[] { at.AddMinutes(-95), at.AddHours(-15), at.AddDays(-2), at.AddDays(-12), new DateTime(2025, 12, 30, 20, 5, 0) }.Select(d => PanelModel.AwaySince(d, at)).ToList();
        Check(said.SequenceEqual(new[] { "11:05 today", "yesterday 21:40", "Thursday 12:40", "28 Sep", "30 Dec 2025" }),
              "away page: since when in plain words: the time today, yesterday, the weekday within the week, the date further back: " + string.Join(" | ", said));
        Check(rows.Select(r => r.Title).SequenceEqual(new[] { "Edda", "Finch", "Tor" }) && rows.All(r => r.Kind == PanelModel.AwayFellowKind),
              "away page: one bar row per fellow, alphabetical (never by size)");
        var pcts = rows.Select(r => int.Parse(r.Value.Replace(" %", ""))).ToList();
        var shares = rows.Select(r => Parts(r).Sum(p => (double)p.Fraction)).ToList();
        Check(pcts.Sum() == 100 && pcts.All(p => p > 0) && Math.Abs(shares.Sum() - 1) < 0.001,
              "away page: the fellows' shares (each total weighs the same) add up to 100 % and to the whole track: " + string.Join(", ", pcts));
        // 0.8.5 the damage-row grammar: each part says its fellow's share of that one total in the line under the bar ("built 35 %"), and a
        // total's shares over the fellows add up to 100; the rows share one scale (the largest share fills the track); five fellows the same
        foreach (var (name, page) in new[] { ("three", rows), ("five", Rows(Scenario(now, 72, Five, 0.8))) })
        {
            var parts = page.SelectMany(r => Parts(r).Select(p => (row: r, p))).ToList();
            var byTotal = parts.GroupBy(x => x.p.Id).Select(g => (id: g.Key, sum: g.Sum(x => int.Parse(x.p.Text.Replace(" %", ""))))).ToList();
            var words = parts.Select(x => PanelModel.DamageLineWords(PanelModel.BarFormOf(Parts(x.row).ToList(), true).Rows.Single(r => r.Part == x.p))).ToList();
            Check(page.Count == (name == "five" ? 5 : 3) && byTotal.All(t => t.sum == 100) && words.All(w => Regex.IsMatch(w, @"^(built|foes|stone|wood|dishes) \d+ %$")) &&
                  page.Max(r => r.Fraction) == 1 && page.All(r => r.Fraction > 0 && r.Fraction <= 1) && page.Sum(r => int.Parse(r.Value.Replace(" %", ""))) == 100 &&
                  // About these numbers says it: the percent before a bar is the average of the percents under it (a total not added to is 0)
                  page.All(r => Math.Abs(int.Parse(r.Value.Replace(" %", "")) - Parts(r).Sum(p => int.Parse(p.Text.Replace(" %", ""))) / (double)Totals(Scenario(now, 72, name == "five" ? Five : new[] { "Edda", "Finch", "Tor" }, 0.8)).Items.Count) <= 1),
                  "away page, " + name + " fellows: a part's line says that fellow's share of its total (" + words.First() + "), each total's shares add up to 100 (" +
                  string.Join(", ", byTotal.Select(t => t.id + " " + t.sum)) + "), the rows on one scale (the largest full), a row's percent the average of its line's");
        }
        var colourOf = totals.Items.ToDictionary(t => t.Id, t => t.Colour);
        Check(rows.SelectMany(Parts).All(p => colourOf.TryGetValue(p.Id, out var c) && c == p.Colour) && colourOf.Values.Distinct().Count() == colourOf.Count,
              "away page: each total keeps one colour, on its hero line and in every fellow's bar");
        Check(!Words(totals).Concat(rows.SelectMany(Words)).Any(w => Regex.IsMatch(w, @"(^|[^\d.,])0([^\d.,]|$)")), "away page: never a 0 anywhere on the page");
        // grateful use: your food and gear they used, from their own record (another fellow's things are not "for you"); one kind shows its own
        // picture, several the food-shared or gear-shared mark with the sum (the prototype's bowl x 4, axe x 1)
        string You(Block r) => string.Join(", ", Chips(r).Select(c => c.Title + " " + c.Value));
        // (Edda's ×6 holds one serving of the Meadows feast you made and Tor set out: a teamwork feast is its maker's, 0.8)
        Check(You(rows[0]) == "Your food ×6, Bronze Axe ×1" && You(rows[1]) == "Your food ×5, Your gear ×2" && You(rows[2]) == "Your food ×6, Wood Shield ×1" &&   // 0.8.5: they ate more of your soup and wraps (the sample's Today)
              rows[0].Items.Single(c => c.Title == "Bronze Axe").Icon.StartsWith("item:") && Page(three).Single(b => b.Kind == PanelModel.AwayRowsKind).Text == PanelModel.AwayForYou,
              "away page: For you counts your food and gear they enjoyed or put to good use, never another fellow's: " + string.Join(" | ", rows.Select(You)));

        // an older copy (no since-install totals): its Hearthwoven deeds are not recorded and simply left out; the game's own counters still tell
        var older = Scenario(now, 72, new[] { "Tor" }, 0.8);
        var tor = older.Fellows[0]; tor.SharedSinceInstall = false; tor.SeenBefore.SinceInstall = false;
        var olderTotals = Totals(older); var olderRow = Rows(older).Single();
        Check(!olderTotals.Items.Any(t => t.Id == "wood" || t.Id == "mined" || t.Id == "dishes") && olderTotals.Items.Any(t => t.Id == "foes") && Parts(olderRow).Any(p => p.Id == "foes") && !Words(olderTotals).Concat(Words(olderRow)).Any(w => w.Contains("Not recorded")),
              "away page: an older copy's measured deeds are left out (no wood, stone or dishes total, no 'Not recorded'); its foes defeated (the game's own counters) still count: " + string.Join(", ", olderTotals.Items.Select(t => t.Id)));

        // a count that started again from zero on their side: left out, said once, never negative
        var restarted = Scenario(now, 72, new[] { "Edda" }, 0.8);
        var mark = restarted.Fellows[0].SeenBefore;
        foreach (var k in mark.Values.Keys.Where(k => k.StartsWith(DeedLog.PickedUp + "|")).ToList()) mark.Values[k] *= 10;
        var rTotals = Totals(restarted);
        Check(!rTotals.Items.Any(t => t.Id == "wood" || t.Id == "mined") && Page(restarted).Count(b => b.Kind == "note" && b.Text.Contains("started again from zero")) == 1 && !rTotals.Items.Any(t => t.Value.Contains("-")),
              "away page: a count that went down (a reinstall) is left out with one line, never negative");

        // first time a fellow shares: a quiet row; the other told as usual
        var first = Scenario(now, 30, new[] { "Edda", "Tor" }, 0.95, firstTime: "Tor");
        var firstRows = Rows(first);
        Check(Totals(first).Title is string awayFor && awayFor.Contains("30 hours") && awayFor.Contains("since") && firstRows.Select(r => r.Title).SequenceEqual(new[] { "Edda", "Tor" }) &&
              firstRows[1].Kind == PanelModel.AwayQuietKind && firstRows[1].Text == PanelModel.AwayFirstLine && !Parts(firstRows[1]).Any() && firstRows[1].Value == null &&
              firstRows[0].Kind == PanelModel.AwayFellowKind && firstRows[0].Value == "100 %" && firstRows[0].Tone == PanelModel.DamageNoLineTone && rows.All(r => r.Tone == null),
              "away page: a fellow seen for the first time gets the quiet first-time row, no bar, no per cent; the other has the whole share, and their bar no line of 100 % entries (with three fellows every bar keeps its line)");

        // nothing grew: a quiet row with the date; no totals, so no 0
        var still = Scenario(now, 72, new[] { "Finch" }, 1.0);
        var stillRow = Rows(still).Single();
        Check(stillRow.Kind == PanelModel.AwayQuietKind && stillRow.Text.Contains("Nothing new") && Totals(still).Items.Count == 0 && Page(still).Single(b => b.Kind == PanelModel.AwayRowsKind).Title == null,
              "away page: a fellow whose record did not grow: one quiet line 'Nothing new since <date>', no totals and no share caption");

        // your own book only: the entry is not on a fellow's Company list; Company still opens on Fireside
        var list = PanelModel.Build(three, new PanelState { Chapter = Chapter.Company });
        var theirs = PanelModel.Build(three.Fellows[0], new PanelState { Chapter = Chapter.Company, Player = three.Fellows[0].PlayerName });
        Check(list.List[0].Id == PanelModel.AwayPage && list.Page == "fireside" && !theirs.List.Any(l => l.Id == PanelModel.AwayPage),
              "away entry: first in your Company list, Company still opens on Fireside, not on a fellow's book");
        System.Console.WriteLine("  page (3 days):\n    " + dump.Replace("\n", "\n    "));
        return fails;
    }
}
