using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Deeds > Recent (0.7, Joost 2026-10-09: "last 10 minutes, 30 minutes, this session: what have I actually collected? So you can see the
    /// stats growing rather than having to search for what changed. For your personal stuff, but also for the group"). The page reads only
    /// RecentModel.cs (RecentOf, RecentOfFellow); it counts nothing itself.
    ///
    /// Chips: RecentWindows, Battle's whole set (10 min .. All, B33); a window this book cannot show (a day before the history, a fellow's days)
    /// is greyed and shows Session while the choice stays for the other pages. 0.8 (Joost 2026-10-10: "make them one thing"): the Everyone
    /// chip turns the page into the group's (it had its own You / The group switch before): off, your own gains (or the fellow's on their book);
    /// on, the group.
    ///   You: what grew in the window, family by family in a fixed order (Brought in, Made, Built, Groundwork, Planted, Trees felled, Foes
    ///   defeated), each a strip of picture + "+N" + name, biggest first, the top RecentTop and "+N more"; the skills practised as small
    ///   "+" chips. A family the window cannot tell is said once in plain words (never 0). Nothing grew: one calm line.
    ///   The group (Everyone on): one row per player who shares, you first, then the others by name (the Everyone pages' order: never a
    ///   ranking of people), each with up to GroupTop gains (GroupGains: every family that grew, then the next largest), and "+N more"
    ///   when more grew. A fellow counts only inside YOUR session (B33): what reached this PC from them while you played; a window that
    ///   cannot be told for them says "Not recorded" (or "not on this session"), muted, and RecentOfFellow's Why line stands once under the
    ///   rows (the header-space rule, FEEDBACK 13b: no loose text above the numbers).
    /// A window is its own label (rule W.1): no "Recorded from" inside it (Build sets Windowed); the heading carries the window.
    /// </summary>
    public static partial class PanelModel
    {
        public const string RecentPageId = "recent", RecentTitle = "Recent", RecentIcon = "vocab:list-recent";
        /// <summary>The largest kinds a family shows before "+N more"; the gains per player in The group (0.8: 6, two rows of three; was 4 on one
        /// row, cut off in game).</summary>
        public const int RecentTop = 8, GroupTop = 6;
        /// <summary>The group's people block (Tone): drawn as a grid of gains per player (RecentUi.cs), not one line.</summary>
        public const string RecentGroupTone = "recent";
        public const string RecentNothingNew = "Nothing new yet";
        /// <summary>A row of The group whose window grew nothing (true: the window was recorded and is empty).</summary>
        public const string RecentRowNothing = "nothing new";
        /// <summary>Said once under The group's rows on your own book: what a fellow's row is (B33: only what they did while you played; All: their shared totals).</summary>
        public const string RecentFellowsLine = "Other players: what they did while you were playing too.", RecentFellowsAllLine = "Other players: their totals as they last shared them.";
        /// <summary>Why a fellow's Recent greys some chips, on the plate (as DeedsWindowsLine on the other Deeds pages; B33).</summary>
        public static string RecentWindowsLine(PanelInput input) =>
            input.Timed ? Name(input) + "'s days are not shared" : Name(input) + "'s numbers arrive only every few minutes, and days are not shared";
        /// <summary>About these numbers on Recent (B29, Joost 2026-10-10: the old lines were not understood): labels and lines for a player.</summary>
        public const string AboutYouLabel = "You", AboutOthersLabel = "Other players",
                            AboutYou = "10 min to Session: what you did since you started playing this time. Today to 30 days: the days saved on this computer. All: everything since you installed Hearthwoven.",
                            AboutOthers = "Only what they did while you were playing too, as it reached you. 10 min to 3 h need a server that sends live updates. Their days are not shared; All shows their totals.";
        public const string RecentFoesNoDays = "Which foes is kept for this session only: 7 days counts them.";

        /// <summary>The families of the You view, in reading order: (id, heading). The ids are RecentModel's families or a part of one
        /// (DeedLog.Placed split by PieceKind into built and groundwork; the plants put in the ground come from DeedLog.Planted).</summary>
        static readonly (string id, string title)[] RecentFamilies =
        {
            ("broughtIn", "Brought in"), ("made", "Made"), ("built", "Built"), ("ground", "Groundwork"), ("planted", "Planted"),
            ("felled", "Trees felled"), ("foes", "Foes defeated"),
        };

        static void RecentPage(PanelInput input, PanelView view, PanelState state)
        {
            view.Recorded = true;   // a 0.7 page: no zones, no "since install"; inside a window nothing is dated (rule W.1)
            view.Heading = RecentTitle;
            var w = RecentChips(input, state, view);
            // a fellow's Recent: whose, and (B33) that it is what reached you while you played; All: their copy's scope (rule E)
            view.Scope = input.IsSelf ? null : w == TimeWindow.SinceInstall ? RecordedScope(input) : input.Cached ? CachedScope(input) : w == TimeWindow.Session ? Name(input) + ", " + SinceViewer(input) : Name(input);
            // the Everyone chip on, your own book, somebody else shares: the group (as ShowsEveryone for the group pages); else this book's gains
            var fellows = FellowsOf(input).ToList();
            var groupOn = state.Everyone && input.IsSelf && fellows.Count > 0;
            if (groupOn)
            {
                view.EveryoneOn = true;
                var group = new List<PanelInput> { input }.Concat(fellows.OrderBy(p => Name(p), StringComparer.OrdinalIgnoreCase)).ToList();   // you first, then by name
                RecentGroupBlocks(input, group, w, view);
            }
            else RecentYouBlocks(input, w, view);
            if (input.IsSelf)   // its own labels (RecordedModel's "On this PC" does not fit a page about you and the others); the others' only when they show
            {
                var lines = new List<Block> { new Block { Kind = "aboutline", Title = AboutYouLabel, Text = AboutYou } };
                if (groupOn) lines.Add(new Block { Kind = "aboutline", Title = AboutOthersLabel, Text = AboutOthers });
                view.AboutNumbers = new Block { Kind = "aboutnumbers", Id = NumbersTarget, Title = AboutNumbersTitle, Items = lines };
            }
            Plate(view, RecentIcon);
        }

        /// <summary>The chips of Recent and the window shown: the chosen one when Recent offers it and this book can show it, else Session.
        /// A fellow's book greys what their copies cannot tell (RecentOpen: their days always; the short windows without live updates).</summary>
        static TimeWindow RecentChips(PanelInput input, PanelState state, PanelView view)
        {
            var shown = RecentWindows.Contains(state.Window) && RecentOpen(input, state.Window) ? state.Window : TimeWindow.Session;
            view.HasFilters = true; view.ShownWindow = shown;
            foreach (var w in RecentWindows)
                view.Windows.Add(new Choice { Id = w.ToString(), Label = WindowShort(w), Selected = w == shown, Disabled = !RecentOpen(input, w), Waits = DayOpensOn(input, w).HasValue });
            return shown;
        }

        // "+12", "+1 040"
        static string Plus(double v) => "+" + N(v);

        /// <summary>
        /// The parts of one window's growth the page shows, per RecentFamilies id: token (or tree kind) -> amount, only what grew, OtherToken
        /// left out (it is counted, but holds no name: Clipped says so). Kills per foe come from DeedLog.Kills; when a window has none per
        /// foe (a day window), the game's kill counter's growth stands under foes as one token (KillsTotal).
        /// </summary>
        public static Dictionary<string, Dictionary<string, double>> RecentParts(PanelInput input, RecentView r)
        {
            var parts = RecentFamilies.ToDictionary(f => f.id, f => new Dictionary<string, double>());
            void Put(string fam, string key, double v) { if (v <= 0 || key == DeedLog.OtherToken) return; var d = parts[fam]; d.TryGetValue(key, out var o); d[key] = o + v; }
            IEnumerable<KeyValuePair<string, float>> Of(string fam) => r.Grew.TryGetValue(fam, out var d) ? d : Enumerable.Empty<KeyValuePair<string, float>>();
            foreach (var kv in Of(DeedLog.PickedUp)) Put("broughtIn", kv.Key, kv.Value);
            foreach (var kv in Of(DeedLog.Made)) Put("made", kv.Key, kv.Value);
            foreach (var kv in Of(DeedLog.Placed))
            {
                var kind = PieceKind(input, kv.Key);
                if (kind == "ground") Put("ground", kv.Key, kv.Value);
                else if (kind != "planted") Put("built", kv.Key, kv.Value);   // plants: DeedLog.Planted below (the same plants, counted once)
            }
            foreach (var kv in Of(DeedLog.Planted)) Put("planted", kv.Key, kv.Value);
            foreach (var kv in Of(DeedLog.Felled)) Put("felled", TreeKind(input, kv.Key), kv.Value);
            foreach (var kv in Of(DeedLog.Kills)) Put("foes", kv.Key, kv.Value);
            if (parts["foes"].Count == 0 && r.NotRecorded.Contains(DeedLog.Kills) && r.Grew.TryGetValue(DeedLog.Counters, out var c) && c.TryGetValue("EnemyKills", out var k)) Put("foes", KillsTotal, k);
            return parts;
        }
        /// <summary>The foes token of a window that counts kills but not per foe (a day window).</summary>
        public const string KillsTotal = "~kills";

        // the picture of one part
        static string RecentIconOf(PanelInput input, string fam, string key)
        {
            switch (fam)
            {
                case "built": case "ground": case "planted": return "piece:" + key;
                case "felled": return TreeIcon(key);
                case "foes":
                    if (key == KillsTotal) return "vocab:list-foes";
                    var boss = Bosses.FirstOrDefault(b => b.token == key);
                    if (boss.trophy != null) return "item:" + boss.trophy;
                    var bare = key.StartsWith("$enemy_", StringComparison.Ordinal) ? key.Substring(7) : key;
                    return bare.Length == 0 ? "" : TrophyOf(input, char.ToUpperInvariant(bare[0]) + bare.Substring(1));
                default: return "item:" + key;
            }
        }
        static string RecentNameOf(PanelInput input, string fam, string key) =>
            fam == "felled" ? key : fam == "foes" && key == KillsTotal ? "foes" : Who(input, key);

        /// <summary>The You view: the families that grew, each a section with its sum and a strip of its largest kinds.</summary>
        static void RecentYouBlocks(PanelInput input, TimeWindow w, PanelView v)
        {
            var r = RecentOf(input, w);
            if (!r.Shown) { v.Blocks.Add(Empty(r.Why)); return; }
            var parts = RecentParts(input, r);
            var skills = r.Grew.TryGetValue(DeedLog.Skills, out var sk) ? sk.Where(kv => kv.Value > 0 && kv.Key != DeedLog.OtherToken).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList() : new List<KeyValuePair<string, float>>();
            if (parts.Values.All(d => d.Count == 0) && skills.Count == 0)
            {
                v.Blocks.Add(Empty(RecentNothingIn(input, w), w == TimeWindow.SinceInstall ? null : RecentLonger));
                RecentNotRecorded(input, r, v);
                return;
            }
            var src = input.IsSelf ? SrcPc : SrcFellows;
            foreach (var (id, title) in RecentFamilies)
            {
                var d = parts[id];
                if (d.Count == 0) continue;
                var list = d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
                v.Blocks.Add(new Block { Kind = "section", Title = title, Value = Plus(list.Sum(kv => kv.Value)), Src = src, Source = TagOfSrc(src) });
                v.Blocks.Add(new Block
                {
                    Kind = "strip", Src = src, Source = TagOfSrc(src),
                    Items = list.Take(RecentTop).Select(kv => new Block { Kind = "item", Id = kv.Key, Icon = RecentIconOf(input, id, kv.Key), Title = RecentNameOf(input, id, kv.Key), Value = Plus(kv.Value), Src = src, Source = TagOfSrc(src) }).ToList(),
                    Note = list.Count > RecentTop ? RecentMore(list.Count - RecentTop) : id == "foes" && list.Any(kv => kv.Key == KillsTotal) ? RecentFoesNoDays : null,
                });
            }
            if (skills.Count > 0)
            {
                v.Blocks.Add(new Block { Kind = "section", Title = "Practiced" });
                // small "+" chips: practice has no unit a player reads (the Skills pages show the levels), so the chip says only that it grew
                v.Blocks.Add(new Block { Kind = "strip", Items = skills.Take(RecentTop).Select(kv => new Block { Kind = "item", Id = Chapter.Skills + "/" + kv.Key, Icon = "skill:" + kv.Key, Title = SkillName(input, kv.Key), Value = "+" }).ToList(),
                                         Note = skills.Count > RecentTop ? RecentMore(skills.Count - RecentTop) : null });
            }
            if (r.Clipped) v.Blocks.Add(new Block { Kind = "note", Text = RecentClippedLine });
            RecentNotRecorded(input, r, v);
        }

        public const string RecentLonger = "Choose a longer window to look further back.";
        public const string RecentClippedLine = "A very busy stretch kept only its largest kinds: a few small ones are left out here.";
        public static string RecentMore(int n) => "+" + n + " more";

        /// <summary>"Nothing new in the last 10 minutes" / "this session" / "the last 7 days".</summary>
        public static string RecentNothingIn(PanelInput input, TimeWindow w) =>
            "Nothing new " + (input.IsSelf ? "" : "from " + Name(input) + " ") + (w == TimeWindow.Session ? (input.IsSelf ? "this session" : SinceViewer(input)) : w == TimeWindow.SinceInstall ? "yet" : w == TimeWindow.Today ? "today" : "in the " + WindowLabel(w).ToLowerInvariant());

        /// <summary>
        /// The families a window cannot tell, said once in plain words, never as 0 (REDESIGN-RULES "Not recorded"): a fellow's shared session
        /// holds no game counters (pieces built, foes defeated). Kills per foe in a day window is said on the foes strip (RecentFoesNoDays),
        /// and only when kills grew; a window whose kill counter did not grow truly holds none.
        /// </summary>
        static void RecentNotRecorded(PanelInput input, RecentView r, PanelView v)
        {
            var words = new List<string>();
            if (r.NotRecorded.Contains(DeedLog.Placed)) words.Add("pieces built");
            if (r.NotRecorded.Contains(DeedLog.Counters) || (r.NotRecorded.Contains(DeedLog.Kills) && !r.Grew.ContainsKey(DeedLog.Counters) && !IsDayWindow(r.Window ?? TimeWindow.Session))) words.Add("foes defeated");
            if (words.Count == 0) return;
            v.Blocks.Add(new Block { Kind = "note", Text = NotRecorded + " in " + (input.IsSelf ? "this window" : Name(input) + "'s shared totals") + ": " + string.Join(" and ", words) + "." });
        }

        /// <summary>
        /// The group view: one row per player, you first, then the others by name, with their gains (picture, "+N", name; GroupGains) and
        /// "+N more" (Note) when more grew. A fellow in a window they do not share: "Not recorded" (muted, never 0) and their Why line once
        /// under the rows, never above the numbers (FEEDBACK 13b).
        /// </summary>
        static void RecentGroupBlocks(PanelInput input, List<PanelInput> group, TimeWindow w, PanelView v)
        {
            var rows = new List<Block>(); var why = new List<string>(); var fellowsShown = false;
            foreach (var p in group)
            {
                var r = RecentOf(p, w);
                var row = new Block { Kind = "person", Id = Name(p), Title = p.IsSelf ? "You" : Name(p), Icon = "person:" + Name(p), Items = new List<Block>() };
                if (!r.Shown)
                {
                    row.Items.Add(new Block { Kind = "item", Title = r.Row ?? NotRecorded, Unrecorded = true });
                    if (r.Row == null && !string.IsNullOrEmpty(r.Why) && !why.Contains(r.Why)) why.Add(r.Why);   // a row that says it itself needs no line
                }
                else
                {
                    if (!p.IsSelf) fellowsShown = true;
                    var src = p.IsSelf ? SrcPc : SrcFellows;
                    var (gains, more) = GroupGains(RecentParts(p, r));
                    foreach (var (id, key, amount) in gains)
                        row.Items.Add(new Block { Kind = "item", Id = key, Icon = RecentIconOf(p, id, key), Title = RecentNameOf(p, id, key), Value = Plus(amount), Src = src, Source = TagOfSrc(src) });
                    if (more > 0) row.Note = RecentMore(more);
                    if (row.Items.Count == 0) row.Items.Add(new Block { Kind = "item", Title = RecentRowNothing, Tone = "quiet" });
                }
                rows.Add(row);
            }
            v.Blocks.Add(new Block { Kind = "people", Tone = RecentGroupTone, Items = rows });
            foreach (var line in why) v.Blocks.Add(new Block { Kind = "note", Text = line });
            if (fellowsShown && input.IsSelf) v.Blocks.Add(new Block { Kind = "note", Text = w == TimeWindow.SinceInstall ? RecentFellowsAllLine : RecentFellowsLine });
        }

        /// <summary>
        /// A player's gains on The group: the largest of each family first, so every family that grew shows (Brought in, Made, ...), then the
        /// next largest of any family, at most GroupTop; shown in the families' order, largest first inside one. more = the gains left out.
        /// </summary>
        static (List<(string fam, string key, double amount)> shown, int more) GroupGains(Dictionary<string, Dictionary<string, double>> parts)
        {
            var all = RecentFamilies.SelectMany((f, at) => parts[f.id].OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                                                                      .Select((kv, rank) => (at, rank, fam: f.id, key: kv.Key, amount: kv.Value))).ToList();
            var chosen = all.Where(x => x.rank == 0).Concat(all.Where(x => x.rank > 0).OrderByDescending(x => x.amount).ThenBy(x => x.at).ThenBy(x => x.rank)).Take(GroupTop).ToList();
            return (chosen.OrderBy(x => x.at).ThenBy(x => x.rank).Select(x => (x.fam, x.key, x.amount)).ToList(), all.Count - chosen.Count);
        }
    }
}
