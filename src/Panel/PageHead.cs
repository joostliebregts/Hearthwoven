using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8 layout D+ (work/hearthwoven-0.8/density-feedback/FEEDBACK.md, items 13 and 13b and part 4): every page on a plate has one head with
    /// fixed slots. The heading row: the page's name and the time chips. The strip (Kind "featband", the title strip): the page's feats and
    /// titles on the left, its view switch (with its caption) and "About these numbers" on the right (on a fellow's book: whose copy and when,
    /// StripNote). Then the hero, then the body as islands
    /// (Islands.cs). There is no slot for loose text above the hero: an explanation goes into About these numbers or into the hover reason of the
    /// chip it explains (WindowWhy). SettleHead, Build's last step, decides this once for every page; LooseText says what still breaks it (the
    /// panel test runs it over every preview page, so a new page cannot break the rule unnoticed).
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>About these numbers: the labels of the lines that moved there from the top of the plate.</summary>
        public const string AboutComparedLabel = "Compared", AboutFightLabel = "In this fight", AboutPlayersLabel = "Players", AboutEarlierLabel = "Earlier counts", AboutGrowthLabel = "Growth line";

        /// <summary>About these numbers' line for the hero's growth line: "One column per day: since 3 Oct."</summary>
        public static string GrowthAbout(string span) => "One column per day: " + span + ".";

        /// <summary>
        /// Build's last step on a page with a plate: the plate's lines go to their slots (a window chip's reason, About these numbers, or away
        /// where the rows already say it), text above the hero joins About these numbers, the strip leads the plate whenever the page has
        /// feats, titles, a view switch or About these numbers, and a group page (Everyone) has no growth line (fellow players share no days).
        /// The Feats chapter's full plate keeps its line: it has no hero, the line is what the page is.
        /// </summary>
        static PanelView SettleHead(PanelInput input, PanelState state, PanelView view)
        {
            var plate = PlateOf(view);
            if (plate == null || view.ShowAbout) return view;
            input = input ?? new PanelInput(); state = state ?? new PanelState();
            var items = plate.Items ?? (plate.Items = new List<Block>());
            var moved = new List<Block>();   // lines for About these numbers
            if (plate.Tone != PlateFull && !string.IsNullOrEmpty(plate.Text))
            {
                foreach (var line in plate.Text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)) Route(line);
                plate.Text = null;
            }
            // a text block between the strip and the hero: About these numbers, under "Additional details"
            var stream = HeadStream(plate);
            var hero = stream.FindIndex(x => x.block.Kind == "hero");
            for (int k = 0; k < hero; k++)
                if (IsLooseText(stream[k].block)) { moved.Add(AboutLine(DetailsLabel, stream[k].block.Text)); stream[k].list.Remove(stream[k].block); }
            // the view switch's caption rides in the strip's left slot; when the strip holds feats or titles there, it explains the numbers in the box
            var top = TopSwitch(plate);
            var titled = items.FirstOrDefault(b => b.Kind == "featband") is Block bandNow && (BandFeats(bandNow).Count > 0 || BandTitles(bandNow).Count > 0);
            if (top != null && titled && !string.IsNullOrEmpty(top.Title)) { moved.Add(AboutLine(DetailsLabel, Cap(top.Title) + ".")); top.Title = null; }
            // "Earlier counts may be incomplete." comes off the page and lives only in About these numbers (Joost on the board: no "i" mark, no hover)
            if (ClearIncomplete(items) && input.IsSelf) moved.Add(AboutLine(AboutEarlierLabel, EarlierIncomplete));   // a fellow's book has no box (hard case 6): there it simply goes
            // the growth line shows its columns only, beside the hero's numbers (no row of its own): its span goes to About and to its hover
            var spark = Shown(items).Where(b => b.Kind == "hero").SelectMany(h => h.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == SparkKind && i.Tone != OffTone);
            // (a group page has none, below; a page without the box keeps it in the hover only: a window's page has no box, rule W.1)
            if (spark != null && !view.EveryoneOn && view.AboutNumbers != null && !string.IsNullOrEmpty(spark.Title)) moved.Add(AboutLine(AboutGrowthLabel, GrowthAbout(spark.Title)));
            if (moved.Count > 0) MoveToAbout(state, view, moved);

            // the strip leads (an empty one when the page has only a switch or About these numbers to carry), About these numbers right after it
            var band = items.FirstOrDefault(b => b.Kind == "featband");
            var about = items.FirstOrDefault(b => b.Kind == "aboutnumbers");
            if (band != null || about != null || TopSwitch(plate) != null || !string.IsNullOrEmpty(view.StripNote))
            {
                if (band == null) band = new Block { Kind = "featband", Items = new List<Block>() };
                items.Remove(band); items.Insert(0, band);
                if (about != null) { items.Remove(about); items.Insert(1, about); }
            }
            // a group page: fellow players share no days, so a growth line would show only your own (FEEDBACK 13)
            if (view.EveryoneOn) foreach (var b in Content(view)) b.Items?.RemoveAll(i => i.Kind == SparkKind);   // anywhere on the page: a hero's, a legend's
            HeadingTotals(plate, view.Active == Chapter.Deeds);
            // each greyed window chip its own reason: why they are all greyed, then a day window's own day when it works later (0.8.1 review 3:
            // with 7 days pressed, the greyed 30 days said "7 days works from ..."; before a press the day chips said nothing). A chip the page
            // already gave its reason keeps it (the battle feed's days and All: what the feed holds, Chapters/BattleFeedModel.cs)
            foreach (var c in view.Windows.Where(c => c.Disabled && string.IsNullOrEmpty(c.Why)))
            {
                var own = c.Waits && Enum.TryParse(c.Id, out TimeWindow w) ? WaitLine(input, w) : null;
                c.Why = string.IsNullOrEmpty(view.WindowWhy) ? own : own == null ? view.WindowWhy : view.WindowWhy + " " + own;
            }
            return view;

            void Route(string line)
            {
                // why the window chips are greyed (a fellow's copy, the group, a day window before the history): the greyed chips' hover reason
                if (line == WaitLine(input, state.Window)) { view.WindowTip = state.Window.ToString(); return; }   // a greyed day chip was pressed: its own reason shows under it now (B17)
                if (line == GroupDaysLine || line == DeedsWindowsLine(input) || line == RecentWindowsLine(input) || line == SharedWindowsLine(input))
                {
                    view.WindowWhy = string.IsNullOrEmpty(view.WindowWhy) ? line : view.WindowWhy + " " + line;
                    return;
                }
                // the group's line: each player's row already says how fresh their numbers are
                if (view.EveryoneOn && line.StartsWith("You and ", StringComparison.Ordinal))
                {
                    if (!Shown(plate.Items).Any(b => b.Kind == GroupRowsKind)) moved.Add(AboutLine(AboutPlayersLabel, line));   // a view without the rows (By weapon): About says it
                    return;
                }
                // whose copy and when (a fellow's book): at the strip's right end, where your own book has its About button (a fellow's copy has
                // no box: it carries no dates, hard case 6)
                if (!input.IsSelf) { view.StripNote = string.IsNullOrEmpty(view.StripNote) ? line : view.StripNote + " · " + line; return; }
                var label = view.Comparing && line.Contains(", against ") ? AboutComparedLabel
                          : view.Active == Chapter.Battle && view.Page == LastFightPage ? AboutFightLabel : DetailsLabel;
                moved.Add(AboutLine(label, line));
            }
        }

        // one island heading style, each with its total (FEEDBACK part 4: "By type" had none, "DISHES BY KIND 166" had): a bar's heading says what
        // its parts add up to, the group's rows what the players' numbers do; on a Deeds page a ranking's or a ledger's heading too (counts of
        // one kind there). A number with a unit or words in it ("37.6 km") leaves the total out rather than add unlike things.
        static void HeadingTotals(Block plate, bool deeds)
        {
            void Walk(List<Block> list)
            {
                for (int k = 0; k < (list?.Count ?? 0); k++)
                {
                    var b = list[k]; if (b == null) continue;
                    var next = k + 1 < list.Count ? list[k + 1] : null;
                    if (string.IsNullOrEmpty(b.Value) && !string.IsNullOrEmpty(b.Title))
                    {
                        var of = b.Kind == "section" ? next : b;   // the total's source: what it adds up
                        if (b.Kind == "composition" && b.Tone != "single") b.Value = SumOfCounts((b.Items ?? new List<Block>()).Where(p => p.Fraction > 0));
                        else if (b.Kind == GroupRowsKind) b.Value = SumOfCounts((b.Items ?? new List<Block>()).Where(r => r.Kind == GroupMemberKind));
                        else if (deeds && b.Kind == "section" && next != null && (next.Kind == "ranking" || next.Kind == "ledger") && next.Items?.Count > 1) b.Value = SumOfCounts(next.Items);
                        if (!string.IsNullOrEmpty(b.Value) && of != null) { b.Src = b.Src ?? of.Src ?? of.Items?.FirstOrDefault(i => i.Src != null)?.Src; b.Source = b.Source ?? of.Source ?? of.Items?.FirstOrDefault(i => i.Source != null)?.Source; }
                    }
                    if (IsBox(b) || b.Kind == "filterbar") Walk(b.Items);
                }
            }
            Walk(plate.Items);
        }

        // the blocks the page shows, in reading order: a switch opens into its chosen view only (Content opens every view)
        static IEnumerable<Block> Shown(IEnumerable<Block> blocks)
        {
            foreach (var b in blocks ?? Enumerable.Empty<Block>())
            {
                yield return b;
                var inner = b.Kind == "switch" ? (b.Items ?? new List<Block>()).Where(v => v.Selected).SelectMany(v => v.Items ?? new List<Block>()) : IsBox(b) ? b.Items : null;
                if (inner != null) foreach (var x in Shown(inner)) yield return x;
            }
        }

        // every "Earlier counts may be incomplete." on the page, a note of its own or a number's qualifier, taken off; true when there was one
        static bool ClearIncomplete(List<Block> blocks)
        {
            var any = false;
            void Walk(List<Block> bs)
            {
                if (bs == null) return;
                any |= bs.RemoveAll(b => b != null && b.Kind == "note" && b.Text == EarlierIncomplete) > 0;
                foreach (var b in bs.Where(b => b != null))
                {
                    if (b.Note == EarlierIncomplete) { b.Note = null; any = true; }
                    if (b.Text == EarlierIncomplete) { b.Text = null; any = true; }
                    Walk(b.Items);
                }
            }
            Walk(blocks);
            return any;
        }

        static Block AboutLine(string label, string text) => new Block { Kind = "aboutline", Title = label, Text = text };

        // the lines join the page's box, first; a page without one gets it (its button in the strip, the numbers key in the key line)
        static void MoveToAbout(PanelState state, PanelView view, List<Block> lines)
        {
            if (view.AboutNumbers == null)
            {
                view.AboutNumbers = new Block { Kind = "aboutnumbers", Id = NumbersTarget, Title = AboutNumbersTitle, Items = lines };
                PlaceAboutNumbers(state, view);
                NumbersKeyLine(state, view);
                return;
            }
            view.AboutNumbers.Items = (view.AboutNumbers.Items ?? new List<Block>()).Concat(lines).ToList();   // the page's own lines first
            var shown = (PlateOf(view)?.Items ?? view.Blocks).FirstOrDefault(b => b.Kind == "aboutnumbers");
            if (shown != null && shown.Items != null) shown.Items = view.AboutNumbers.Items;   // open: it shows them now
        }

        // the plate's blocks in reading order as the islands see them: the top switch's chosen view opened in its place; each with the list it is in
        static List<(Block block, List<Block> list)> HeadStream(Block plate)
        {
            var all = new List<(Block, List<Block>)>();
            var top = TopSwitch(plate);
            foreach (var b in plate.Items ?? new List<Block>())
            {
                all.Add((b, plate.Items));
                if (b == top) foreach (var v in (b.Items ?? new List<Block>()).Where(v => v.Selected && v.Items != null)) foreach (var x in v.Items) all.Add((x, v.Items));
            }
            return all;
        }

        /// <summary>About these numbers' sentences, one per line ("" when the page has none): where a line from the top of the plate went.</summary>
        public static string AboutText(PanelView v) => string.Join("\n", (v?.AboutNumbers?.Items ?? new List<Block>()).Select(l => l.Text));

        /// <summary>A block that is only a sentence (a note or plain line), not the faded key's chip nor a tag drawn on a number.</summary>
        public static bool IsLooseText(Block b) => b != null && b.Kind == "note" && b.Tone != "tag" && b.Tone != "hint" && !string.IsNullOrEmpty(b.Text) && b.Text != FadedKey && b.Text != FadedKeyTwin;

        /// <summary>
        /// The header-space rule's check (FEEDBACK 13b): every text that sits between the strip and the hero, the plate's own line included (the
        /// Feats chapter's full plate leads with its line and has no hero). Empty on a page that keeps the rule.
        /// </summary>
        public static List<string> LooseText(PanelView v)
        {
            var plate = PlateOf(v);
            var loose = new List<string>();
            if (plate == null || v.ShowAbout || plate.Tone == PlateFull) return loose;
            if (!string.IsNullOrEmpty(plate.Text)) loose.Add("the plate's line: " + plate.Text);
            var stream = HeadStream(plate);
            var hero = stream.FindIndex(x => x.block.Kind == "hero");
            for (int k = 0; k < hero; k++) if (IsLooseText(stream[k].block)) loose.Add(stream[k].block.Text);
            // a sentence inside the hero's row (its number's qualifier ending in a full stop, "Earlier counts may be incomplete."): loose too
            foreach (var h in Shown(plate.Items).Where(b => b.Kind == "hero"))
                foreach (var n in new[] { h }.Concat(h.Items ?? new List<Block>()).Where(n => n.Kind != SparkKind))
                    if (!string.IsNullOrEmpty(n.Note) && n.Note.TrimEnd().EndsWith(".", StringComparison.Ordinal)) loose.Add("in the hero: " + n.Note);
            return loose;
        }
    }
}
