using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The 0.7 redesign's shared model (work/hearthwoven-0.7/REDESIGN-RULES.md, part 2): one recorded total with its breakdown, no
    /// zones. A page opts in with view.Recorded = true; Build then dates the numbers Hearthwoven counted on this PC with
    /// PlaceRecordedFrom ("Recorded from 8 October · this PC", "from 8 October") instead of the zones and the "since install" label,
    /// says "Earlier counts may be incomplete." once per page, and offers the page's "About these numbers" box (AboutNumbers) on the
    /// numbers key and its button. Deeds pages show their game skill in the hero's row (SkillBeside, rule K).
    /// Pages migrate one at a time: a page without view.Recorded is drawn exactly as before.
    /// </summary>
    public static partial class PanelModel
    {
        public const string NotRecorded = "Not recorded", AboutNumbersTitle = "About these numbers", DetailsLabel = "Additional details";
        /// <summary>The key line's short word for the box (the button says it in full): the long one made the key line wrap once the page scrolls.</summary>
        public const string NumbersKeyWord = "Numbers";
        /// <summary>The click target of the "About these numbers" button (Follow flips PanelState.ShowNumbers).</summary>
        public const string NumbersTarget = "numbers:toggle";

        // ---------- dates and words (part 0) ----------

        /// <summary>The fixed date the game gives profiles from before its stats (2021) instead of a real one: never a date to show (About's
        /// "made" date; the zones read it too while they last).</summary>
        static readonly DateTime NoCreationDate = new DateTime(2021, 2, 2);

        /// <summary>"8 October", or "8 October 2025" when it is not this year; the player's local date.</summary>
        public static string RecordDate(PanelInput i, DateTime utc)
        {
            i = i ?? new PanelInput();
            var d = Local(i, utc); var now = Local(i, i.NowUtc);
            return d.ToString(d.Year == now.Year ? "d MMMM" : "d MMMM yyyy", Inv);
        }

        /// <summary>
        /// When a part of a page began on this PC: the counter group's own start (Starts: cargo, led, born, feats), else the baseline date
        /// of that kind (BaselineAt: "stats", "pickables", "treesFelled", ...), else the install. null = not known: a fellow's copy (it
        /// carries no dates), or no install date. Never read InstalledUtc directly for a page's date (hard case 3).
        /// </summary>
        public static DateTime? StartOf(PanelInput i, string kindOrGroup)
        {
            if (i == null || !i.IsSelf) return null;
            if (kindOrGroup != null)
            {
                if (i.Starts != null && i.Starts.TryGetValue(kindOrGroup, out var g)) return g;
                if (i.BaselineAt != null && i.BaselineAt.TryGetValue(kindOrGroup, out var b)) return b;
            }
            return i.InstalledUtc;
        }

        /// <summary>The label of a section, block or page whose numbers are all Hearthwoven's: "Recorded from 8 October · this PC"; no date known:
        /// "Recorded on this PC"; a fellow's book: "Recorded on Tor's PC" (their copy has no dates), or "as Tor last shared it" when their copy
        /// holds only their last session (part 0, rule E; the same words as Company's FellowWords).</summary>
        public static string RecordedFromLine(PanelInput i, DateTime? fromUtc) =>
            i != null && !i.IsSelf ? FellowWords(Name(i), i.SharedSinceInstall) : fromUtc.HasValue ? "Recorded from " + RecordDate(i, fromUtc.Value) + " · this PC" : "Recorded on this PC";

        /// <summary>After one such number among others: "from 8 October"; no date: "on this PC"; a fellow's book: "on Tor's PC", or "as Tor last
        /// shared it" when their copy holds only their last session.</summary>
        public static string FromShort(PanelInput i, DateTime? fromUtc) =>
            i != null && !i.IsSelf ? (i.SharedSinceInstall ? "on " + Name(i) + "'s PC" : "as " + Name(i) + " last shared it") : fromUtc.HasValue ? "from " + RecordDate(i, fromUtc.Value) : "on this PC";

        /// <summary>The empty state of a page or hero that only Hearthwoven counts: "Nothing from 8 October"; a fellow or no date: "Nothing yet".</summary>
        public static string NothingFrom(PanelInput i, DateTime? fromUtc) =>
            i != null && i.IsSelf && fromUtc.HasValue ? "Nothing from " + RecordDate(i, fromUtc.Value) : NothingYet;

        /// <summary>The plate's line on a fellow's book of a 0.7 page (rule E): whose copy and when, without "since install": "Edda, as of 8 Oct
        /// 00:24"; a copy of their last session only keeps "Edda, last shared, 8 Oct 00:05". Your own book: none. Replaces FellowScope on a migrated page.</summary>
        public static string RecordedScope(PanelInput i) =>
            i == null || i.IsSelf ? null : i.Cached ? CachedScope(i) : Name(i) + (i.SharedSinceInstall ? (i.LastRecordedUtc.HasValue ? ", " + AsOf(i) : "") : ", " + SessionScope(i));

        /// <summary>B23: a fellow shown from this PC's cache until the server's fresh copy comes: "as Edda last shared it, 8 Oct 21:40" (when it reached this PC).</summary>
        public static string CachedScope(PanelInput i) =>
            "as " + Name(i) + " last shared it" + (i.ReceivedUtc.HasValue ? ", " + Local(i, i.ReceivedUtc.Value).ToString("d MMM HH:mm", Inv) : "");

        public static string BeforeLabel(PanelInput i, DateTime d) => "Before " + RecordDate(i, d);
        public static string FromLabel(PanelInput i, DateTime d) => "From " + RecordDate(i, d);

        // ---------- the labels (part 2, step 4): the stretches of a page, with dates ----------

        /// <summary>
        /// Where "Recorded from ..." goes on a 0.7 page, once per place it is true: after the page heading when every number on the page was
        /// counted on this PC (one date); after a section heading when its whole section was (one date); otherwise after the block when all
        /// its numbers were ("Recorded from ..."), or after each single number that was ("from 8 October"). Each number's date is its
        /// Block.From, else StartOf(input, null); a stretch with numbers of different dates is labelled block by block. Your character's
        /// counts and fellow players' carry nothing. Skips: About, a windowed page, a Battle window other than All, Feats.
        /// </summary>
        public static void PlaceRecordedFrom(PanelInput input, PanelView view, Chapter chapter)
        {
            if (view.ShowAbout || view.Windowed) return;
            if (chapter == Chapter.Battle && view.HasFilters && view.ShownWindow.HasValue && view.ShownWindow.Value != TimeWindow.SinceInstall) return;
            if (chapter == Chapter.Feats) return;
            var numbered = view.Blocks.Where(b => Srcs(b).Any()).ToList();
            if (numbered.Count == 0) return;
            var hasHeading = !string.IsNullOrEmpty(view.Heading);
            if (hasHeading && numbered.All(AllPc) && !Content(view).Any(b => b.Kind == "switch"))
            {
                var dates = numbered.SelectMany(b => PcDates(input, b)).Distinct().ToList();
                if (dates.Count <= 1) { view.HeadingRecordedFrom = RecordedFromLine(input, dates.FirstOrDefault()); return; }
            }
            var headingPc = hasHeading && view.HeadingSrc == SrcPc;
            if (headingPc) view.HeadingRecordedFrom = RecordedFromLine(input, StartOf(input, null));
            var leading = true;
            PlaceRecorded(input, view.Blocks, headingPc, ref leading);
            SaidOnce(view.Blocks);
        }

        // a hero's "from 1 October" right above a section that says "Recorded from 1 October · this PC" is said twice (Skills > Practised): the
        // section's label stays, the hero's goes
        static void SaidOnce(List<Block> blocks)
        {
            if (blocks == null) return;
            for (int i = 0; i + 1 < blocks.Count; i++)
            {
                var hero = blocks[i]; var next = blocks[i + 1];
                if (hero?.Kind == "hero" && next?.Kind == "section" && !string.IsNullOrEmpty(hero.RecordedFrom) && !string.IsNullOrEmpty(next.RecordedFrom) &&
                    next.RecordedFrom.IndexOf(hero.RecordedFrom, StringComparison.Ordinal) >= 0) hero.RecordedFrom = null;
            }
            foreach (var b in blocks) if (b != null) SaidOnce(b.Items);
        }

        // the dates of the numbers this PC counted inside a block (its own and its items')
        static IEnumerable<DateTime?> PcDates(PanelInput input, Block b) =>
            (b.Src == SrcPc ? new[] { b.From ?? StartOf(input, null) } : Enumerable.Empty<DateTime?>())
            .Concat((b.Items ?? new List<Block>()).SelectMany(i => PcDates(input, i)));

        static void PlaceRecorded(PanelInput input, List<Block> blocks, bool headingSince, ref bool leading)
        {
            for (int i = 0; i < blocks.Count;)
            {
                var b = blocks[i];
                if (b.Kind == "divider") { leading = false; i++; continue; }
                if (IsBox(b))
                {
                    if (b.Kind == "columns") leading = false;
                    if (b.Kind == "columns" || b.Kind == "switch") foreach (var c in b.Items ?? new List<Block>()) { var own = false; PlaceRecorded(input, c.Items ?? new List<Block>(), headingSince, ref own); }
                    else PlaceRecorded(input, b.Items ?? new List<Block>(), headingSince, ref leading);
                    i++; continue;
                }
                var head = b.Kind == "section" ? b : null;
                if (head != null) leading = false;
                int from = head != null ? i + 1 : i, end = from;
                while (end < blocks.Count && blocks[end].Kind != "section" && blocks[end].Kind != "divider" && !IsBox(blocks[end])) end++;
                var stretch = blocks.GetRange(from, end - from).Where(x => Srcs(x).Any()).ToList();
                var whole = stretch.Count > 0 && stretch.All(AllPc);
                var dates = stretch.SelectMany(x => PcDates(input, x)).Distinct().ToList();
                if (head != null && stretch.Count == 0 && head.Src == SrcPc && (head.Value ?? "").Any(char.IsDigit))   // a section that is only its total (Animals led with no tile under it)
                    head.RecordedFrom = RecordedFromLine(input, head.From ?? StartOf(input, null));
                else if (whole && head != null && dates.Count <= 1) head.RecordedFrom = RecordedFromLine(input, dates.FirstOrDefault());
                else if (!(whole && leading && headingSince && dates.Count <= 1)) foreach (var x in stretch) LabelRecorded(input, x);
                i = end;
            }
        }

        // a block whose numbers were all counted on this PC (one date): "Recorded from ..." after it, or "from ..." when it is one number;
        // else each such number inside it. 0.8: a skill at the block's head (Kind "skill", Chapters/SkillsBeside.cs) is the character's level beside
        // its counts, not one of them: it does not split a weapon band's one line into a "from 1 October" after every damage type
        static void LabelRecorded(PanelInput input, Block b)
        {
            var dates = PcDates(input, b).Distinct().ToList();
            var counts = b.Items?.Where(x => x.Kind != "skill").ToList();
            var single = counts == null || !counts.Any(x => Srcs(x).Any());
            var srcs = new[] { b.Src }.Concat((counts ?? new List<Block>()).SelectMany(Srcs)).Where(s => s != null).ToList();
            if (srcs.Count > 0 && srcs.All(s => s == SrcPc) && dates.Count == 1) { b.RecordedFrom = single ? FromShort(input, dates[0]) : RecordedFromLine(input, dates[0]); return; }
            if (b.Src == SrcPc && ((b.Value ?? "").Any(char.IsDigit) || (b.Kind == "stat" && (b.Title ?? "").Any(char.IsDigit)))) b.RecordedFrom = FromShort(input, b.From ?? StartOf(input, null));
            foreach (var i in b.Items ?? new List<Block>()) LabelRecorded(input, i);
        }

        /// <summary>A short date: "8 Oct", with the year when it is not this year's (the day windows' lines).</summary>
        public static string ShortDate(DateTime day, DateTime today) => day.ToString(day.Year == today.Year ? "d MMM" : "d MMM yyyy", Inv);

        /// <summary>When the character's own counts start, said plainly (Joost 2026-10-09: "since Rowan was made" read oddly): "since you made
        /// this character" in your own book, "since Tor made this character" in a fellow's.</summary>
        public static string MadeLine(PanelInput input) =>
            input != null && input.IsSelf ? "since you made this character" : "since " + (string.IsNullOrEmpty(input?.PlayerName) ? "the player" : input.PlayerName) + " made this character";

        // a shallow copy of a block with other items (a sorted list, a narrowed section)
        static readonly System.Reflection.MethodInfo CopyOf = typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        static Block Shell(Block b, List<Block> items) { var c = (Block)CopyOf.Invoke(b, null); c.Items = items; return c; }

        /// <summary>Block tones a few pages still set: a columns box whose two parts belong together (Taming: born in your care beside born
        /// near you), a hero's second number size (Cooking), a ranking drawn tight (DeedsUi).</summary>
        public const string PairTone = "pair", HeroSecondTone = "second", TightTone = "tight";

        // ---------- "Earlier counts may be incomplete." once per page (Joost 2026-10-09, J5) ----------

        /// <summary>
        /// A 0.7 page says EarlierIncomplete once (the 2026-10-09 decision, OneIncompleteLine): one holder keeps it where it is; with several
        /// (Woodcutting: trees felled and wood brought in) every one is cleared and the one line stands right after the last block that
        /// carried it, beside the numbers it is about (not at the page's end, under counts it does not touch).
        /// </summary>
        public static void OneIncompletePerPage(List<Block> items)
        {
            if (items == null) return;
            int Count(Block b) => b == null ? 0 : (b.Text == EarlierIncomplete ? 1 : 0) + (b.Note == EarlierIncomplete ? 1 : 0) + (b.Items ?? new List<Block>()).Sum(Count);
            if (items.Sum(Count) <= 1) return;
            var last = items.FindLastIndex(b => Count(b) > 0);
            var at = last + 1 - items.Take(last + 1).Count(b => b != null && b.Kind == "note" && b.Text == EarlierIncomplete);
            void Clear(List<Block> bs)
            {
                if (bs == null) return;
                bs.RemoveAll(b => b != null && b.Kind == "note" && b.Text == EarlierIncomplete);
                foreach (var b in bs.Where(b => b != null)) { if (b.Text == EarlierIncomplete) b.Text = null; if (b.Note == EarlierIncomplete) b.Note = null; Clear(b.Items); }
            }
            Clear(items);
            items.Insert(Math.Min(Math.Max(0, at), items.Count), new Block { Kind = "note", Text = EarlierIncomplete });
        }

        // ---------- About these numbers (part 2, step 5) ----------

        /// <summary>
        /// The page's box: Kind "aboutnumbers", Title AboutNumbersTitle, Items Kind "aboutline" (Title = the label, Text = the sentence):
        /// "Before 8 October", "From 8 October", "Additional details", in that order. A null or empty sentence drops its line; all three
        /// empty: no box. Own book only (a fellow's copy carries no dates, hard case 6). splitUtc null (no date known): "Earlier" and
        /// "On this PC". Build puts it under the page's hero when the numbers key or its button opens it.
        /// </summary>
        public static void AboutNumbers(PanelView view, PanelInput i, DateTime? splitUtc, string before, string from, string details)
        {
            if (view == null || i == null || !i.IsSelf) return;
            var lines = new List<Block>();
            void Line(string label, string text) { if (!string.IsNullOrEmpty(text)) lines.Add(new Block { Kind = "aboutline", Title = label, Text = text }); }
            Line(splitUtc.HasValue ? BeforeLabel(i, splitUtc.Value) : "Earlier", before);
            Line(splitUtc.HasValue ? FromLabel(i, splitUtc.Value) : "On this PC", from);
            Line(DetailsLabel, details);
            view.AboutNumbers = lines.Count == 0 ? null : new Block { Kind = "aboutnumbers", Id = NumbersTarget, Title = AboutNumbersTitle, Items = lines };
        }

        /// <summary>
        /// Build's last step on a page with a box: the box's button goes right under the first hero (or first on the plate), with the
        /// numbers key as its keycap; open (the key, or a click on the button) it shows its lines there. Kept closed: the button alone.
        /// </summary>
        static void PlaceAboutNumbers(PanelState state, PanelView view)
        {
            var box = view.AboutNumbers;
            if (box == null || view.ShowAbout) return;
            var list = PlateOf(view)?.Items ?? view.Blocks;
            if (list.Any(b => b.Kind == "aboutnumbers")) return;
            // the page holds the button and, only while open, the lines (view.AboutNumbers keeps them all for the tests and the key line)
            var shown = new Block { Kind = box.Kind, Id = box.Id, Title = box.Title, Open = state.ShowNumbers, Selected = state.ShowNumbers,
                                    KeyCap = string.IsNullOrEmpty(state.NumbersKey) ? null : state.NumbersKey, Items = state.ShowNumbers ? box.Items : null };
            list.Insert(list.FindIndex(b => b.Kind == "hero") + 1, shown);
        }

        /// <summary>The numbers key in the key line, right before the About key: only on a page that has the box.</summary>
        static void NumbersKeyLine(PanelState state, PanelView view)
        {
            if (view.AboutNumbers == null || state.ShowAbout || string.IsNullOrEmpty(state.NumbersKey)) return;
            var about = string.IsNullOrEmpty(state.InfoKey) ? -1 : view.Keys.IndexOf("[" + state.InfoKey + "] About");
            var key = "[" + state.NumbersKey + "] " + NumbersKeyWord;
            if (about >= 0) view.Keys.Insert(about, key); else view.Keys.Add(key);
        }

        // 0.8: Page Up and Page Down and the pad's right stick scroll the page too (Chapters/FoesKeysUi.cs: PageKeys), unlisted like the arrow keys:
        // "[Wheel/PgDn] Scroll" was too wide for most Deeds pages' key lines, which then left the scroll hint out altogether
        /// <summary>Shown in the key line only while a page has more below it (the soft fade says where; this says how). PanelUi adds it.</summary>
        public const string ScrollKey = "[Wheel] Scroll";
        /// <summary>The keys the key line may leave out when it is too wide, the least needed first: the wheel (the fade already shows there is
        /// more, and the wheel is how every list scrolls), then (0.8) the book key (the player row's chips do the same with a click), then Back (the page you came from is one click on its list), then (0.8) the Foes page's
        /// Enter (a click on a foe opens it too, and the key the filter focus chooses with does the same here). Every other key stays.</summary>
        public static readonly string[] KeysToDrop = { ScrollKey, "] " + BookWord, "[Backspace] Back", FoeOpenKeyLine };   // "] Book": the book key, whatever its key (0.8)
        public const string KeyGap = "      ";

        /// <summary>
        /// The key line (MERGE-NOTES G2/G6: Cooking with [Y] and [K], Company > Together with [Y] and [F] ran past the line): the keys six spaces
        /// apart; while fits says the line is too wide, it leaves out the next of KeysToDrop that it holds. PanelUi measures the drawn text
        /// (TMP's preferred width against the key line's width), the preview its rendered width; the panel test passes its character estimate.
        /// </summary>
        public static string KeyLine(IEnumerable<string> keys, Func<string, bool> fits)
        {
            var list = (keys ?? Enumerable.Empty<string>()).Where(k => !string.IsNullOrEmpty(k)).ToList();
            var line = string.Join(KeyGap, list);
            foreach (var drop in KeysToDrop)
            {
                if (fits == null || fits(line)) break;
                var at = list.FindIndex(k => k == drop || (drop.StartsWith("] ", StringComparison.Ordinal) && k.EndsWith(drop, StringComparison.Ordinal)));
                if (at >= 0) { list.RemoveAt(at); line = string.Join(KeyGap, list); }
            }
            return line;
        }

        // ---------- the deed's skill beside the hero (rule K) ----------

        /// <summary>
        /// The page's game skill in the hero's row, on the right, as the hero's last item: Kind "skill", Id "Skills/&lt;skill&gt;" (a click opens
        /// its page), Icon "skill:&lt;skill&gt;", Title its name, Value the level, Progress to the next level (LadderOf). Nothing when the page
        /// has no hero or the character lacks the skill (level 0, no practice). Call it right after the page's hero is added.
        /// </summary>
        static void SkillBeside(PanelView view, PanelInput input, string skill)
        {
            var hero = view.Blocks.LastOrDefault(b => b.Kind == "hero");
            if (hero == null || !SkillNames(input).Contains(skill)) return;
            var s = LadderOf(input, skill, "skill");
            if (s.Level <= 0 && s.Progress <= 0 && Practice(input, skill) <= 0) return;
            s.Id = Chapter.Skills + "/" + skill;
            (hero.Items ?? (hero.Items = new List<Block>())).Add(s);
        }

        /// <summary>The hero's own skill (SkillBeside), or null.</summary>
        public static Block SkillOf(Block hero) => hero?.Items?.FirstOrDefault(i => i.Kind == "skill");

        /// <summary>The least room kept between the hero's last number and the skill on its right (HeroLines).</summary>
        public const float HeroSkillGap = 24f;

        /// <summary>
        /// The hero row's one wrap rule (0.7, MERGE-NOTES G1/G3: "Farming lev" was clipped at the plate edge). PanelUi and the preview
        /// measure the drawn parts and ask this where each goes: the hero's number first; each second number follows on the same line
        /// (after its rule, sep wide) while it fits the column, else it starts the next line (no rule before it there); the skill keeps
        /// to the right of the line the last number ends on when it fits there (at least HeroSkillGap from it), else it takes the next
        /// line, on the right. Nothing is ever clipped. widths: the hero's number, then each second number, label included; skill: its
        /// width, 0 = none. Returns the line of each number, in order, then the skill's (when there is one).
        /// </summary>
        public static int[] HeroLines(IList<float> widths, float sep, float skill, float column)
        {
            var n = widths?.Count ?? 0;
            var lines = new int[n + (skill > 0 ? 1 : 0)];
            int line = 0; float x = n > 0 ? widths[0] : 0;
            for (int i = 1; i < n; i++)
            {
                if (x + sep + widths[i] <= column) x += sep + widths[i];
                else { line++; x = widths[i]; }
                lines[i] = line;
            }
            if (skill > 0) lines[n] = n == 0 || x + HeroSkillGap + skill <= column ? line : line + 1;
            return lines;
        }

        // ---------- compact page tops (0.7, Joost in game, Deeds > Farming: the skill under the hero "uses too much vertical height"; then, 2026-10-09:
        // the skill stays at the right end of the hero's row, HeroLines puts it there, and the About button rides in the title strip) ----------


        /// <summary>The page's "About these numbers" button when the plate also has a title strip (featband) to carry it, else null.</summary>
        public static Block StripAbout(Block plate)
        {
            var items = plate?.Items ?? new List<Block>();
            return items.Any(b => b.Kind == "featband") ? items.FirstOrDefault(b => b.Kind == "aboutnumbers") : null;
        }

        /// <summary>The page's view switch that shares the top row (B38): the plate's first switch; null = none.</summary>
        public static Block TopSwitch(Block plate) => (plate?.Items ?? new List<Block>()).FirstOrDefault(b => b.Kind == "switch");

        /// <summary>The About button's short label when the top row is full (the key line already says "[Y] Numbers").</summary>
        public const string AboutShort = "Numbers";

        /// <summary>The room between the strip's titles and what rides at its right end (StripFit).</summary>
        public const float StripAboutGap = 18f;

        /// <summary>
        /// B38 (Joost: "try to get all top items into one row max"): the plate's top items share ONE row: the title strip on the left, on its
        /// right the view switch with its key and the About button with Y. What rides on the right is reserved first, so the strip's titles
        /// end in "+N more" before it. lead: what the strip must still show (its feat part, or the Titles tag and room for one title); full,
        /// shortW: the right part with "About these numbers" and with "Numbers"; note: the feat's moment ("3 Oct · Black Forest"); inner: the
        /// strip's inside width. First the About label shortens, then the strip leaves out its note; it never wraps into a second row.
        /// </summary>
        public static (bool shortAbout, bool dropNote) StripFit(float lead, float full, float shortW, float note, float inner)
        {
            if (lead + StripAboutGap + full <= inner) return (false, false);
            if (lead + StripAboutGap + shortW <= inner) return (true, false);
            return (true, note > 0);
        }
    }
}
