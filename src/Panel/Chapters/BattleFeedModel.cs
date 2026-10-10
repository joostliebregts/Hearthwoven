using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The battle pages of 0.8 (work/hearthwoven-0.8/BACKLOG.md "Battle detail", Joost's choices from battle-prototypes/index.html; Last fight:
    /// prototypes/PICKS.md section 2, pick A), built on the battle record (BattleRecModel.cs, src/BattleRecord.cs). Pure C#.
    ///
    /// Block kinds (drawn by Chapters/BattleFeedUi.cs and BattleUi.BattleSources, mirrored in preview/panel-preview.html):
    /// - dealtfoes (Damage, By foe): the "sources" form of Defence for damage dealt. Items: an optional Kind "pipkey" first (Items Kind "pip"
    ///   with Tone defeated|away|fighting and Title its word, the tones present), then Kind "source" per foe kind (Id prefab, Icon, Title, Value =
    ///   dealt, Fraction on one scale, Value2 = the "×N" badge or null, Items = Kind "part" per damage type and an optional Kind "pips").
    /// - pips: Items Kind "pip" (Tone, Count) when the kind has at most PipMax foes, else Text in words ("412 defeated · 88 got away").
    /// - feed (Battle feed): Tone = the view (log | cards | timeline), Note = what is not shown, Items = Kind "fight" newest first (Id its number,
    ///   Title "Fight at 21:08", Text "4 min · 6 foes", Note "with Edda and Tor", Value dealt, Value2 received, Items = Kind "entry" newest first
    ///   (Id prefab, Icon its trophy, Title the foe, Text the time "21:12:20", Tone you|with|away|fighting, Note the outcome words, Fraction its
    ///   dealt against the largest entry shown, Items = Kind "dealt" and Kind "received" per type (Id, Title the type in small letters, Value,
    ///   Colour, Fraction = share of its direction)), then an optional Kind "gap" (Text "2 min 40 s without a fight") before the next fight).
    /// - grouprows (Last fight): the Everyone chip's group rows (EveryoneModel.cs GroupRowsKind, drawn by EveryoneUi.GroupRows): Title the
    ///   caption, Text "share", Items = GroupMemberKind (Id name, Icon person:, Title "You" or the name, Note, Value, Value2 = share "46 %",
    ///   Fraction = value / group total, Colour person:) or, without numbers, GroupQuietKind with Text in words.
    /// - fightfoes (Last fight): Title, Value, Text ("your damage to each" when fellow players are in the numbers), Items = the pipkey and Kind
    ///   "foe" per kind (Id, Icon, Title, Value = your damage to it, Value2 badge, Fraction on one scale, Items = Kind "part" per damage type and
    ///   the pips block): drawn as the damage rows (DamageRowsUi.cs).
    /// - fightreceived (Last fight): Value the total, Title its words, Text "after ... armour", Items = Kind "player" (Title, Value, Colour).
    /// - headlink (plate item): a chip in the heading row that opens a page (Id = "Battle/feed", Title, Icon).
    /// </summary>
    public static partial class PanelModel
    {
        public const string FeedPage = "feed", LastFightPage = "lastfight";
        public const string FeedLabel = "Feed", LastFightLabel = "Last fight", FeedHeading = "Battle feed";
        public const string FeedIcon = "vocab:list-feed", LastFightIcon = "vocab:list-lastfight";
        /// <summary>The feed's view switch (state.View key) and its three views (Joost 2026-10-09: all three, the choice remembered).</summary>
        public const string FeedViewKey = "Battle/feed/view", FeedLog = "log", FeedCards = "cards", FeedTimeline = "timeline";
        public static readonly (string id, string label)[] FeedViews = { (FeedLog, "Log"), (FeedCards, "Cards"), (FeedTimeline, "Timeline") };
        /// <summary>0.8.1 (Joost in game: "Battle feed, why can't I filter this?"): the feed's filter bar (Biome and Foe rows, the pattern of
        /// Damage's Biome row, BattleFacets.cs) and the window it last showed (FeedWindowChips), kept between sessions as its view is.</summary>
        public const string FeedFilter = "Battle/feed", FeedWindowKey = "Battle/feed/window";
        /// <summary>The views kept between sessions (PanelPrefs): the feed's view and window (Joost); every other switch starts at its first view, as before.</summary>
        public static readonly string[] RememberedViews = { FeedViewKey, FeedWindowKey };
        /// <summary>At most this many foes drawn in the feed (the newest that pass the window and the filter; whole fights while they fit): a page
        /// of 200 would cost seconds to lay out. The recorder keeps BattleRecorder.MaxFeed, so a filter reaches further back than this.</summary>
        public const int FeedTop = 40;
        public const string DealtTo = "Dealt to", PipDefeated = "defeated", PipAway = "got away", PipFighting = "still fighting";
        public const string PipOn = "defeated", PipOff = "away", PipOpen = "fighting";
        /// <summary>A kind with more foes than this shows its outcomes in words, not one mark per foe (two rows of 15 fill the name column).</summary>
        public const int PipMax = 30;
        public const string FeedDealt = "dealt", FeedReceived = "received";
        public const string ByFoeNoBiome = "Counts per foe are kept for every biome together, so a biome choice shows no counts.";

        /// <summary>"×14"; null for none (never "×0").</summary>
        public static string Badge(int n) => n > 0 ? "×" + N(n) : null;

        // ---------- Damage > By foe, and the "×N" on Defence ----------

        /// <summary>The outcome marks of one foe kind: a filled mark per foe defeated (alone or with fellow players), a ring per foe that got away,
        /// a gold ring per foe still in the fight; in words past PipMax. Null when there is nothing to mark.</summary>
        public static Block Pips(int defeated, int away, int fighting)
        {
            if (defeated + away + fighting <= 0) return null;
            if (defeated + away + fighting > PipMax)
            {
                var words = new List<string>();
                if (defeated > 0) words.Add(N(defeated) + " " + PipDefeated);
                if (away > 0) words.Add(N(away) + " " + PipAway);
                if (fighting > 0) words.Add(N(fighting) + " " + PipFighting);
                return new Block { Kind = "pips", Text = string.Join(" · ", words.ToArray()) };
            }
            var items = new List<Block>();
            if (defeated > 0) items.Add(new Block { Kind = "pip", Tone = PipOn, Count = defeated });
            if (away > 0) items.Add(new Block { Kind = "pip", Tone = PipOff, Count = away });
            if (fighting > 0) items.Add(new Block { Kind = "pip", Tone = PipOpen, Count = fighting });
            return new Block { Kind = "pips", Items = items };
        }

        /// <summary>The one-line key over a list with marks: only the marks it uses ("defeated", "got away", "still fighting").</summary>
        static Block PipKey(IEnumerable<Block> pipsBlocks)
        {
            var tones = new HashSet<string>(pipsBlocks.Where(p => p?.Items != null).SelectMany(p => p.Items).Select(i => i.Tone));
            var items = new List<Block>();
            foreach (var (tone, word) in new[] { (PipOn, PipDefeated), (PipOff, PipAway), (PipOpen, PipFighting) })
                if (tones.Contains(tone)) items.Add(new Block { Kind = "pip", Tone = tone, Title = word });
            return items.Count == 0 ? null : new Block { Kind = "pipkey", Items = items };
        }

        /// <summary>
        /// "By foe" (Joost's A + C; 0.8 drawn as the damage rows, DamageRowsUi.cs): one row per foe kind you dealt damage to in the window, most first, the name with its "×N" (separate foes
        /// of the kind you fought; no badge where the window has no counts or the count is 0), the outcome marks under the name, the damage by type on
        /// one scale across foes, the total. The damage is the page's own rows (the same as By weapon and By type); the counts are the window's
        /// (FoeCountsIn). <paramref name="counted"/> false (a biome chosen: counts are not kept per biome): no badges, no marks.
        /// </summary>
        public static Block DealtFoes(PanelInput input, IEnumerable<DamageRow> rows, TimeWindow w, bool counted = true)
        {
            var dealt = DealtRows(rows);
            var all = dealt.GroupBy(r => r.Other).Select(g => (key: g.Key, total: g.Sum(r => (double)r.Amount), types: g.GroupBy(r => r.Type).ToDictionary(x => x.Key, x => x.Sum(r => (double)r.Amount))))
                           .Where(x => x.total > 0).OrderByDescending(x => x.total).ThenBy(x => x.key, StringComparer.Ordinal).ToList();
            if (all.Count == 0) return null;
            var counts = counted ? FoeCountsIn(input, w) : null;
            var fold = all.Count > RowTop;
            var shown = fold ? all.Take(RowTop - 1).ToList() : all;
            if (fold)
            {
                var rest = all.Skip(RowTop - 1).ToList(); var types = new Dictionary<string, double>();
                foreach (var r in rest) foreach (var kv in r.types) Bump(types, kv.Key, kv.Value);
                shown.Add((FoldId, rest.Sum(r => r.total), types));
            }
            var max = shown.Max(x => x.total); var restCount = all.Count - (RowTop - 1);
            var foes = new List<Block>();
            foreach (var s in shown)
            {
                var c = s.key == FoldId ? null : counts?.Get(s.key);
                var parts = Parts(s.types, s.total);
                var pips = c == null ? null : Pips(c.Defeated, c.GotAway, c.Fighting);
                if (pips != null) parts.Add(pips);
                foes.Add(SourcePicture(input, new Block
                {
                    Kind = "source", Id = s.key, Title = s.key == FoldId ? OthersLabel(restCount) : Who(input, s.key), Value = NAtLeast(s.total), Fraction = (float)(s.total / max),
                    Value2 = c == null ? null : Badge(c.Separate), Src = SrcPc, Source = TagMeasured, Items = parts,
                }, s.types));
            }
            var key = PipKey(foes.Select(f => f.Items.FirstOrDefault(i => i.Kind == "pips")));
            if (key != null) foes.Insert(0, key);
            return new Block { Kind = "dealtfoes", Title = DealtTo, Text = counts != null && foes.Any(f => f.Value2 != null) ? CountsFrom(input, w) : null, Src = SrcPc, Source = TagMeasured, Items = foes };
        }

        /// <summary>Defence's "×N" (Joost: the same badge after the foe's name): separate foes of the kind that hit you in the window; the caption
        /// says from when the counts run where the window reaches back before that (CountsFrom).</summary>
        static void HitYouBadges(PanelInput input, Block sources, TimeWindow w)
        {
            if (sources?.Items == null) return;
            var counts = FoeCountsIn(input, w);
            if (counts == null) return;
            foreach (var s in sources.Items) if (s.Kind == "source" && s.Id != FoldId) s.Value2 = Badge(counts.Get(s.Id)?.HitYou ?? 0);
            var from = sources.Items.Any(s => s.Value2 != null) ? CountsFrom(input, w) : null;
            if (from != null) sources.Text = string.IsNullOrEmpty(sources.Text) ? from : sources.Text + " · " + from;
        }

        /// <summary>"counts from 8 October": the ×N of a window that reaches back before the foes were first counted (All, a day window that starts
        /// earlier), where the damage beside it runs longer (battlerec HANDBACK: one recorded total per number, the later one dated). Null otherwise.</summary>
        public static string CountsFrom(PanelInput input, TimeWindow w)
        {
            var from = FoesFrom(input);
            if (!from.HasValue || (w != TimeWindow.SinceInstall && !IsDayWindow(w))) return null;
            var start = w == TimeWindow.SinceInstall ? StartOf(input, null) ?? input.InstalledUtc : WindowStartUtc(input, w);
            if (start.HasValue && start.Value >= from.Value.AddMinutes(-1)) return null;
            return "counts from " + RecordDate(input, from.Value);
        }

        // what the marks and the badge mean, in the page's About these numbers box (no legend beyond the one-line key)
        public const string ByFoeAboutTitle = "×N and the marks", DefenceBadgeTitle = "×N after a foe";
        public const string ByFoeAbout = "×N: how many separate foes of that kind you fought in this window (a troll hit fifty times is one). A filled mark is a foe you defeated, alone or with fellow players; a ring one that got away; a gold ring one still in the fight. No ×N where this window has no count.";
        public const string DefenceBadgeAbout = "How many separate foes of that kind hit you in this window. Causes that are no creature (a fall, smoke) have none.";

        /// <summary>Adds lines to the page's About these numbers box (made when the page has none: a window other than All).</summary>
        static void AboutLines(PanelView view, params (string title, string text)[] lines)
        {
            var add = lines.Where(l => !string.IsNullOrEmpty(l.text)).Select(l => new Block { Kind = "aboutline", Title = l.title, Text = l.text }).ToList();
            if (add.Count == 0) return;
            if (view.AboutNumbers != null) { view.AboutNumbers.Items = (view.AboutNumbers.Items ?? new List<Block>()).Concat(add).ToList(); return; }
            view.AboutNumbers = new Block { Kind = "aboutnumbers", Id = NumbersTarget, Title = AboutNumbersTitle, Items = add };
        }

        // ---------- the feed and the last fight (own pages, this session, your own book) ----------

        static string Clock(PanelInput input, DateTime utc, bool seconds) => Local(input, utc).ToString(seconds ? "HH:mm:ss" : "HH:mm", Inv);

        /// <summary>A fight's length: "under a minute", "4 min", "1 h 5 min".</summary>
        public static string FightLength(TimeSpan t)
        {
            if (t.TotalSeconds < 60) return "under a minute";
            var m = (int)Math.Round(t.TotalMinutes);
            return m < 60 ? m + " min" : m / 60 + " h" + (m % 60 > 0 ? " " + m % 60 + " min" : "");
        }

        /// <summary>The quiet time between two fights: "2 min 40 s without a fight" (seconds under ten minutes), "25 min", "1 h 10 min".</summary>
        public static string QuietTime(TimeSpan t) => Lapse(t) + " without a fight";

        // a stretch of time: "40 s", "2 min 40 s" (seconds under ten minutes), "25 min", "1 h 10 min"
        static string Lapse(TimeSpan t)
        {
            var s = (int)Math.Max(0, Math.Round(t.TotalSeconds));
            return s < 60 ? s + " s" : s < 600 ? s / 60 + " min" + (s % 60 > 0 ? " " + s % 60 + " s" : "") : FightLength(t);
        }

        /// <summary>The time to the next fight down the list when the window or the filter hides fights between them: "25 min, 2 fights between not shown".</summary>
        public static string GapPast(TimeSpan t, int hidden) => Lapse(t) + ", " + N(hidden) + (hidden == 1 ? " fight" : " fights") + " between not shown";

        /// <summary>How long ago: "just now", "6 min ago", "2 h ago".</summary>
        public static string Ago(TimeSpan t)
        {
            if (t.TotalMinutes < 1) return "just now";
            if (t.TotalMinutes < 60) return (int)t.TotalMinutes + " min ago";
            return (int)t.TotalHours + " h ago";
        }

        static string Names(IList<string> names) => names.Count == 0 ? "" : names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1).ToArray()) + " and " + names[names.Count - 1];

        static string OutcomeTone(FoeEncounter e) =>
            e.Outcome == FoeOutcome.DefeatedByYou ? "you" : e.Outcome == FoeOutcome.DefeatedWith ? "with" : e.Outcome == FoeOutcome.GotAway ? "away" : "fighting";

        static List<Block> TypeParts(PanelInput input, float[] amounts, string kind)
        {
            var list = new List<Block>(); double sum = 0;
            for (int t = 0; t < BattleRecorder.Types.Length; t++) if (amounts[t] > 0) sum += amounts[t];
            if (sum <= 0) return list;
            var used = Enumerable.Range(0, BattleRecorder.Types.Length).Where(t => amounts[t] > 0).ToList();
            var shown = Shown(used.Select(t => (double)amounts[t]).ToList());
            for (int k = 0; k < used.Count; k++)
            {
                var type = BattleRecorder.Types[used[k]];
                list.Add(new Block { Kind = kind, Id = type, Title = TypeName(type).ToLowerInvariant(), Icon = DamageIcon(type), Value = shown[k], Colour = DamageColour(type), Fraction = (float)(amounts[used[k]] / sum) });
            }
            return list;
        }

        // a fight's heading: its time, length and foes, its damage both ways. Part of its foes shown (the window, the filter, or the cap cut into
        // it): "2 of 6 foes", and the damage is theirs, so the heading adds up with the rows under it (the fight's length and company stay its own)
        static Block FightBlock(PanelInput input, FoeFight f, IList<FoeEncounter> entries, double largest)
        {
            var with = f.With();
            var part = entries.Count < f.Foes;
            double dealt = part ? entries.Sum(e => (double)e.DealtTotal) : f.DealtTotal, received = part ? entries.Sum(e => (double)e.ReceivedTotal) : f.ReceivedTotal;
            var b = new Block
            {
                Kind = "fight", Id = f.Number.ToString(Inv), Title = "Fight at " + Clock(input, f.StartUtc, false),
                Text = (f.Open ? "still on" : FightLength(f.Length)) + " · " + (part ? N(entries.Count) + " of " : "") + N(f.Foes) + (f.Foes == 1 ? " foe" : " foes"),
                Note = with.Count > 0 ? "with " + Names(with) : null, Value = dealt > 0 ? NAtLeast(dealt) : null, Value2 = received > 0 ? NAtLeast(received) : null, Count = f.Foes, Items = new List<Block>(),
            };
            foreach (var e in entries)
            {
                var parts = TypeParts(input, e.Dealt, FeedDealt); parts.AddRange(TypeParts(input, e.Received, FeedReceived));
                b.Items.Add(new Block
                {
                    Kind = "entry", Id = e.Kind, Icon = FoeIcon(input, e.Kind), Title = Who(input, e.Kind), Text = Clock(input, e.EndUtc, true), Tone = OutcomeTone(e), Note = e.OutcomeText,
                    Value = NAtLeast(e.DealtTotal), Value2 = NAtLeast(e.ReceivedTotal), Fraction = largest > 0 ? (float)(e.DealtTotal / largest) : 0, Items = parts,
                });
            }
            return b;
        }

        /// <summary>What the feed shows of the session's fights: the window's start (null: all of them) and the filter bar's choice (Pass: a foe that
        /// passes the Biome and Foe rows; null: every foe). Key: the choice in words, for FeedOf's reuse.</summary>
        public sealed class FeedPick
        {
            public DateTime? From; public Func<FoeEncounter, bool> Pass; public bool Filtered; public string Key = "";
            public bool InWindow(FoeEncounter e) => !From.HasValue || e.EndUtc >= From.Value;
            public bool Shows(FoeEncounter e) => InWindow(e) && (Pass == null || Pass(e));
        }

        /// <summary>
        /// The battle feed (Joost: Log, Cards and Timeline over the same fights, the choice remembered): your fights this session, newest first, each
        /// foe with the time, the outcome in the book's words and both directions per damage type (dealt before the foe's armour, received after
        /// yours). <paramref name="pick"/> (0.8.1): only the foes in the window that pass the filter, each fight with those of its foes (its heading
        /// says "2 of 6 foes"). At most FeedTop foes drawn, counted after the window and the filter: the newest fights whole while they fit; Note says
        /// what is left out. Null when no foe is left to show.
        /// </summary>
        public static Block FeedBlock(PanelInput input, List<FoeFight> fights, string view, FeedPick pick = null)
        {
            if (fights == null || fights.Count == 0) return null;
            var narrowed = pick != null && (pick.From.HasValue || pick.Filtered);
            var matched = new List<(FoeFight f, List<FoeEncounter> e)>();
            foreach (var f in fights) { var e = pick == null ? f.Entries : f.Entries.Where(pick.Shows).ToList(); if (e.Count > 0) matched.Add((f, e)); }
            if (matched.Count == 0) return null;
            var shown = new List<(FoeFight f, List<FoeEncounter> e)>(); int n = 0;
            foreach (var (f, e) in matched)
            {
                if (n >= FeedTop || (shown.Count > 0 && n + e.Count > FeedTop)) break;
                var take = e.Take(FeedTop - n).ToList(); shown.Add((f, take)); n += take.Count;
            }
            var largest = shown.SelectMany(x => x.e).Select(e => (double)e.DealtTotal).DefaultIfEmpty(0).Max();
            var items = new List<Block>();
            for (int i = 0; i < shown.Count; i++)
            {
                var fb = FightBlock(input, shown[i].f, shown[i].e, largest);
                // the quiet time between this fight and the next one shown below it; when the window or the filter hides fights between them,
                // the time and how many lie between. The last fight shown has none: the fight before it is not on the page (the window, the
                // filter or the cap leaves it out; review 0.8.1 batch 6: its gap pointed out of the window, at the foot of the Timeline)
                if (i + 1 < shown.Count)
                {
                    int at = fights.IndexOf(shown[i].f), below = fights.IndexOf(shown[i + 1].f);
                    var quiet = shown[i].f.StartUtc - shown[i + 1].f.EndUtc;
                    fb.Items.Add(new Block { Kind = "gap", Text = below > at + 1 ? GapPast(quiet, below - at - 1) : QuietTime(quiet) });
                }
                items.Add(fb);
            }
            var leftFights = matched.Count - shown.Count; var leftFoes = matched.Sum(m => m.e.Count) - n;
            // REVIEW-08 #9: foes of this session the book no longer keeps; said where the window reaches back past the oldest fight it keeps
            var older = input?.Battle?.FeedLeftOut ?? 0;
            if (pick?.From != null && fights[fights.Count - 1].StartUtc < pick.From.Value) older = 0;
            var that = pick != null && pick.Filtered ? " that match" : "";
            var where = that.Length > 0 ? that : narrowed ? " in this window" : older > 0 ? "" : " this session";
            // the cap cuts only into the first fight shown (the others fit whole or not at all): its foes left out, then the earlier fights
            var cut = shown.Count == 1 ? matched[0].e.Count - shown[0].e.Count : 0; var earlier = leftFoes - cut;
            var parts = new List<string>();
            if (cut > 0) parts.Add(N(cut) + " more " + (cut == 1 ? "foe" : "foes") + that + " in this fight");
            if (leftFights > 0) parts.Add(N(leftFights) + (leftFights == 1 ? " earlier fight" : " earlier fights") + where + " (" + N(earlier) + (earlier == 1 ? " foe" : " foes") + ")");
            var note = parts.Count == 0 ? null : string.Join(" and ", parts.ToArray()) + " not shown: the book shows the newest " + FeedTop + " foes" + that + ".";
            if (older > 0) note = (note == null ? "" : note + " ") + "Older fights (" + N(older) + (older == 1 ? " foe" : " foes") + ") are no longer kept: the book keeps your newest " + BattleRecorder.MaxFeed + " foes.";
            return new Block { Kind = "feed", Tone = view, Note = note, Count = matched.Count, Items = items };
        }

        // the feed's blocks, kept while the recorder has not changed (BattleRecorder.Version) and the view and the choice are the same: the book
        // refreshes every 2 s, the feed changes only with a hit, a kill, a fight's end, or (a short window) a foe that leaves the window, which
        // the key's count of foes in it notes. The blocks carry no Source, so the build's later passes leave them as they are
        static BattleRecorder feedRec; static int feedAt = -1; static string feedView, feedKey; static Func<DateTime, DateTime> feedLocal; static Block feedBlock;
        static Block FeedOf(PanelInput input, List<FoeFight> fights, string view, FeedPick pick)
        {
            var rec = input.Battle;
            if (rec != null && ReferenceEquals(rec, feedRec) && rec.Version == feedAt && view == feedView && pick.Key == feedKey && feedLocal == input.ToLocal && feedBlock != null) return feedBlock;
            var b = FeedBlock(input, fights, view, pick);
            feedRec = rec; feedAt = rec?.Version ?? -1; feedView = view; feedKey = pick.Key; feedLocal = input.ToLocal; feedBlock = b;
            return b;
        }

        // ---------- the feed's window and filter (0.8.1) ----------

        /// <summary>The windows the feed can show: the short ones and Session (the recorder holds this session's fights, no days, no earlier session).</summary>
        public static bool FeedWindowOpen(TimeWindow w) => IsShortWindow(w);
        public const string FeedDaysWhy = "The feed holds this session's fights only, not days. Session shows every fight it holds.",
                            FeedAllWhy = "The feed holds this session's fights only. Damage and Foes count earlier sessions.";
        public const string NoFightIn = "No fight in the ", FeedLongerLine = "Choose a longer window, or Session for every fight the feed holds.",
                            FeedClearLine = "Clear the filter to see every fight in this window.";
        public const string FeedFilterAboutTitle = "Window and filter";
        public const string FeedFilterAbout = "The window, Biome and Foe show only the foes that pass them: a fight then says how many of its foes show, and its damage is theirs. The newest 40 that pass are drawn.";

        /// <summary>
        /// The feed's window chips: the book's one window set (Damage's chips), the days and All greyed with their own reason. The window shown:
        /// the book's chosen one when it is a window the feed can show and it was chosen this game (PanelState.WindowPicked); else the one the feed
        /// last showed (FeedWindowKey, kept between sessions as its view is), else Session. Only a feed window chosen this game is noted (review
        /// 0.8.1 batch 6: 7 days chosen on Deeds overwrote the kept 1 h, and the next session opened on Session).
        /// </summary>
        static TimeWindow FeedWindowChips(PanelState state, PanelView view)
        {
            var chosen = state.WindowPicked && FeedWindowOpen(state.Window);
            TimeWindow? kept = state.View.TryGetValue(FeedWindowKey, out var k) && Enum.TryParse(k, out TimeWindow kw) && Enum.IsDefined(typeof(TimeWindow), kw) && FeedWindowOpen(kw) ? kw : (TimeWindow?)null;
            var shown = chosen ? state.Window : kept ?? (FeedWindowOpen(state.Window) ? state.Window : TimeWindow.Session);
            view.HasFilters = true; view.ShownWindow = shown;
            foreach (var w in AllWindows)
                view.Windows.Add(new Choice { Id = w.ToString(), Label = WindowShort(w), Selected = w == shown, Disabled = !FeedWindowOpen(w), Why = FeedWindowOpen(w) ? null : IsDayWindow(w) ? FeedDaysWhy : FeedAllWhy });
            if (chosen) state.View[FeedWindowKey] = shown.ToString();
            return shown;
        }

        /// <summary>
        /// The feed's filter bar over the foes in the window (one item per foe, weight 1, so a chip counts foes): Biome (every biome found, in
        /// journey order, as Damage's row; a biome of a fight the character's record has not marked found is added) and Foe (the kinds in the
        /// window, most foes first). A row needs two choices, or a chosen one (a choice from another window stays as a chip to unchoose). No bar when
        /// no row is offered; pass: the foes that pass every row.
        /// </summary>
        static (Block bar, bool narrowed, Func<FoeEncounter, bool> pass) FeedFilterBar(PanelInput input, PanelState state, List<FoeEncounter> inWindow)
        {
            var biome = BiomeRow(input);
            foreach (var b in inWindow.Select(e => e.Biome).Concat(Chosen(state, FeedFilter, "biome")).Distinct().ToList())
                if (!string.IsNullOrEmpty(b) && biome.Options.All(o => o.Id != b)) biome.Options.Add(new FacetOption { Id = b, Label = BiomeName(b) });
            var foe = new FacetDef { Id = "foe", Title = "Foe", Bar = false,
                                     Options = inWindow.GroupBy(e => e.Kind).OrderByDescending(g => g.Count()).ThenBy(g => Who(input, g.Key), StringComparer.Ordinal).Select(g => new FacetOption { Id = g.Key, Label = Who(input, g.Key) }).ToList() };
            foreach (var id in Chosen(state, FeedFilter, "foe").Where(id => foe.Options.All(o => o.Id != id)).ToList()) foe.Options.Add(new FacetOption { Id = id, Label = Who(input, id) });
            var defs = new[] { biome, foe }.Where(d => d.Options.Count >= 2 || Chosen(state, FeedFilter, d.Id).Count > 0).ToList();
            if (defs.Count == 0) return (null, false, null);
            var items = inWindow.Select((e, i) => { var it = new FacetItem { Key = i.ToString(Inv), Weight = 1 }; it.Values["biome"] = e.Biome ?? ""; it.Values["foe"] = e.Kind ?? ""; return it; }).ToList();
            var res = Facets(state, FeedFilter, defs, items, "foes", SrcPc, "foe", "foes", "All foes");
            var line = (res.Shown.Count < items.Count ? N(res.Shown.Count) + " of " : "") + N(items.Count) + (items.Count == 1 ? " foe" : " foes");
            res.Bar.Text = line; res.Bar.Tone = "compact";
            var applied = res.Bar.Items.First(b => b.Kind == "applied"); applied.Text = line; applied.Value = "All foes";
            var shown = new HashSet<FoeEncounter>(res.Shown.Select(it => inWindow[int.Parse(it.Key, Inv)]));
            return (res.Bar, defs.Any(d => Chosen(state, FeedFilter, d.Id).Count > 0), shown.Contains);
        }

        public const string FeedOwnOnly = "Each player's feed stays on their own PC: it is never shared.";
        public const string NoFightYet = "No fight yet this session", NoFightText = "Your fights show up here as you play, newest first.";
        public const string LastFightEmptyText = "After a fight, this page shows it: your part and the part of the fellow players near you.";
        public const string EarlierFights = "Earlier fights";
        public static readonly (string title, string text)[] FeedAbout =
        {
            ("The feed", "Your fights this session, newest first. A fight ends after a minute without hits. Kept on this PC for this session; fellow players never see it."),
            ("What became of a foe", "Defeated by you: the game counted the kill for you alone. Defeated with: fellow players hit it too. Survived: it still stood when the fight ended. Got away: the game took it away without a kill."),
            ("Damage", "Dealt is before the foe's armor, as on Damage. Received is after your armor, as on Defense."),
        };

        static void BattleFeedView(PanelInput input, PanelView view, PanelState state)
        {
            view.Heading = FeedHeading + ", this session";
            if (!input.IsSelf) { view.Heading = FeedHeading; view.Blocks.Add(Empty(Name(input) + "'s feed is on " + Name(input) + "'s PC", FeedOwnOnly)); Plate(view, "ui:chapter-battle"); return; }
            var fights = BattleFeed(input);
            if (fights == null || fights.Count == 0) { view.Blocks.Add(Empty(NoFightYet, NoFightText)); Plate(view, "ui:chapter-battle"); return; }
            // 0.8.1: the window chips (the heading follows: "Battle feed, last 30 minutes") and the Biome and Foe rows, Damage's pattern
            view.Heading = FeedHeading;
            var w = FeedWindowChips(state, view);
            view.Scope = WindowLabel(w) + " · your fights";
            var from = Cutoff(w, input.NowUtc);
            var inWindow = fights.SelectMany(f => f.Entries).Where(e => !from.HasValue || e.EndUtc >= from.Value).ToList();
            var (bar, narrowed, pass) = FeedFilterBar(input, state, inWindow);
            var feedPick = new FeedPick { From = from, Pass = narrowed ? pass : null, Filtered = narrowed };
            feedPick.Key = w + "|" + inWindow.Count + "|" + (narrowed ? string.Join(",", Chosen(state, FeedFilter, "biome").ToArray()) + "|" + string.Join(",", Chosen(state, FeedFilter, "foe").ToArray()) : "");
            var shown = fights.Select(f => f.Entries.Count(feedPick.Shows)).Where(c => c > 0).ToList();
            AboutLines(view, FeedAbout);
            AboutLines(view, (FeedFilterAboutTitle, FeedFilterAbout));
            if (bar != null && (shown.Count > 0 || narrowed)) Add(view, bar);   // the bar stays on an empty choice: it is how you unchoose
            if (shown.Count == 0)
            {
                view.Blocks.Add(narrowed ? Empty(NothingForChoice, FeedClearLine) : Empty(NoFightIn + WindowLabel(w).ToLowerInvariant(), FeedLongerLine));
                Plate(view, "ui:chapter-battle");
                return;
            }
            // only the chosen view is built; the others need a chip, not their content (Switch attaches the chosen one's blocks only)
            var pick = state.View.TryGetValue(FeedViewKey, out var p) && FeedViews.Any(v => v.id == p) ? p : FeedLog;
            var chip = new List<Block> { new Block { Kind = "note" } };
            Switch(view, state, FeedPage, "view", FightsCaption(shown.Count, shown.Sum()), FeedViews.Select(v => (v.id, v.label, v.id == pick ? new List<Block> { FeedOf(input, fights, v.id, feedPick) } : chip)).ToArray());
            Plate(view, "ui:chapter-battle");
        }

        static string FightsCaption(int fights, int foes) =>
            N(fights) + (fights == 1 ? " fight, " : " fights, ") + N(foes) + (foes == 1 ? " foe" : " foes") + ", newest first";

        // ---------- Last fight (PICKS.md section 2, pick A) ----------

        /// <summary>One player's part of a fight: damage dealt per type (BattleRecorder.Types order) and received; Has false when it is not known.</summary>
        public sealed class FightPart
        {
            public string Name; public bool You, Has; public float NearSeconds;
            public readonly double[] Dealt = new double[BattleRecorder.Types.Length];
            public double Received;
            public double DealtTotal => Dealt.Sum();
        }

        /// <summary>
        /// A fellow player's part of one of your fights, from the minutes their shared copy holds (their own damage log, 1-minute buckets, as
        /// Together's windows read it): every bucket that overlaps the fight. Not known (Has false) when their copy holds no log, or no minute from
        /// the fight's start on (it was shared before the fight), or none up to its end (REVIEW-08 #6: they reconnected after it, and their log
        /// starts with the new connection).
        /// </summary>
        public static FightPart PartOf(PanelInput fellow, FoeFight f)
        {
            var part = new FightPart { Name = fellow?.PlayerName };
            if (fellow?.Log == null || !fellow.LastRecordedUtc.HasValue || fellow.LastRecordedUtc.Value.AddMinutes(fellow.Log.Span) <= f.StartUtc) return part;
            if (fellow.FirstRecordedUtc.HasValue && fellow.FirstRecordedUtc.Value > f.EndUtc) return part;
            part.Has = true;
            foreach (var r in Damage(fellow.Log, TimeWindow.Session, "", f.EndUtc))
            {
                if (r.Bucket.AddMinutes(fellow.Log.Span) <= f.StartUtc || r.Bucket > f.EndUtc || r.Amount <= 0) continue;
                var t = Array.IndexOf(BattleRecorder.Types, r.Type);
                if (r.Dir == "dealt" && t >= 0) part.Dealt[t] += r.Amount;
                else if (r.Dir == "taken" && ReceivedTypes.Contains(r.Type)) part.Received += r.Amount;
            }
            return part;
        }

        /// <summary>The parts of a fight: you first, then the fellow players in it (FoeFight.With) in join order of the group (your Fellows list).</summary>
        public static List<FightPart> FightParts(PanelInput input, FoeFight f)
        {
            var you = new FightPart { Name = input.PlayerName, You = true, Has = true };
            for (int t = 0; t < BattleRecorder.Types.Length; t++) you.Dealt[t] = f.Dealt[t];
            you.Received = f.ReceivedTotal;
            var parts = new List<FightPart> { you };
            var with = f.With();
            var sharers = (input.Fellows ?? new List<PanelInput>()).Where(x => x != null && x != input && !SameName(x.PlayerName, input.PlayerName)).ToList();
            foreach (var fellow in sharers.Where(x => with.Any(n => SameName(n, x.PlayerName))))
            {
                var p = PartOf(fellow, f);
                p.NearSeconds = f.Near == null ? 0 : f.Near.Where(kv => SameName(kv.Key, fellow.PlayerName)).Sum(kv => kv.Value);
                parts.Add(p);
            }
            return parts;
        }

        public const string NearYouTitle = "Near you", YourPartTitle = "Your part", FellowsPartTitle = "Fellow players' parts";
        public const string NearYouAbout = "Players within 40 m of you for 10 seconds or more during the fight, or who hit a foe of it with you. Counted on this PC; nothing new is shared.";
        public const string YourPartAbout = "Every hit you dealt and received in this fight, as the feed shows it. Damage dealt is before the foe's armor, damage received after your armor.";
        public const string FellowsPartAbout = "Fellow players' parts come from the minutes their shared copies hold, so up to a minute at each end of the fight can fall in or out. A fellow player whose copy holds no minute of this fight shows without numbers, and is left out of the sums.";
        public const string FellowsNoneAbout = "Only who was near you: no fellow player's shared copy holds minutes of this fight, so the numbers are yours alone.";
        public const string NoPartWords = "no shared minutes of this fight yet";

        static void BattleLastFight(PanelInput input, PanelView view, PanelState state)
        {
            view.Heading = LastFightLabel;
            if (!input.IsSelf) { view.Blocks.Add(Empty(Name(input) + "'s last fight is on " + Name(input) + "'s PC", "Each player's fights stay on their own PC: they are never shared.")); Plate(view, "ui:chapter-battle"); return; }
            var f = BattleFeed(input, 1)?.FirstOrDefault();
            if (f == null) { view.Blocks.Add(Empty(NoFightYet, LastFightEmptyText)); Plate(view, "ui:chapter-battle"); return; }
            var parts = FightParts(input, f);
            var counted = parts.Where(p => p.Has).ToList(); var fellows = parts.Where(p => !p.You).ToList(); var together = counted.Count > 1;
            var dealt = counted.Sum(p => p.DealtTotal);
            // the foes you fought in it, per kind (a foe that only hit you is in the received line, not fought)
            var fought = f.Entries.Where(e => e.DealtTotal > 0 || e.Defeated).ToList();
            var defeated = fought.Count(e => e.Defeated);
            view.Blocks.Add(new Block { Kind = "note", Text = DealtLabel + ", " + DealtQualifier });   // what the numbers are, over them (the prototype's caption)
            view.Blocks.Add(Hero((NAtLeast(dealt), together ? "damage dealt together" : DealtLabel, null, null), (fought.Count > 0 ? N(defeated) : null, defeated == 1 ? "foe defeated" : "foes defeated", null, "of " + N(fought.Count))));
            AboutLines(view, (NearYouTitle, NearYouAbout), (YourPartTitle, YourPartAbout), (FellowsPartTitle, fellows.Count == 0 ? null : together ? FellowsPartAbout : FellowsNoneAbout));
            if (fellows.Count > 0)
            {
                var shares = WholeShares(counted.Select(p => p.DealtTotal).ToList());
                var rows = new List<Block>();
                foreach (var p in parts)
                {
                    var k = counted.IndexOf(p);
                    var note = p.You ? "this PC" : p.NearSeconds >= BattleRecorder.NearSeconds ? "near you " + (p.NearSeconds < 60 ? (int)p.NearSeconds + " s" : FightLength(TimeSpan.FromSeconds(p.NearSeconds))) : "hit the same foes";
                    rows.Add(p.Has
                        ? new Block { Kind = GroupMemberKind, Id = p.Name, Icon = "person:" + p.Name, Title = p.You ? "You" : p.Name, Note = note, Value = NAtLeast(p.DealtTotal), Value2 = shares[k] == 0 && p.DealtTotal > 0 ? UnderOnePercent : shares[k] + " %", Fraction = dealt > 0 ? (float)(p.DealtTotal / dealt) : 0, Colour = "person:" + p.Name,
                                      Items = DamageMix(Enumerable.Range(0, BattleRecorder.Types.Length).Select(t => new KeyValuePair<string, double>(BattleRecorder.Types[t], p.Dealt[t]))) }   // 0.8: the line under their bar
                        : new Block { Kind = GroupQuietKind, Id = p.Name, Icon = "person:" + p.Name, Title = p.Name, Note = note, Text = NoPartWords });
                }
                view.Blocks.Add(new Block { Kind = GroupRowsKind, Title = "Damage dealt, by player", Text = GroupShareCaption, Tone = DamagePlayersDealt, Items = rows });   // the Everyone chip's rows, drawn as damage rows (0.8, EveryoneUi.GroupRows)
            }
            // by type, together: the one bar form with the numbers inside (a damage-type bar)
            var byType = new Dictionary<string, double>();
            foreach (var p in counted) for (int t = 0; t < BattleRecorder.Types.Length; t++) if (p.Dealt[t] > 0) Bump(byType, BattleRecorder.Types[t], p.Dealt[t]);
            Add(view, Composition(together ? "By type, together" : "By type", byType, TypeName, Look(DamageLook), null, t => "damage:" + t));
            // the foes: per kind its "×N", the marks (the Damage-by-foe pieces) and, 0.8 (Joost's damage bars), your damage to it by type in the
            // damage rows: most damage first, one scale for the fight. Your own only: this PC does not know a fellow player's damage per foe
            if (fought.Count > 0)
            {
                var kinds = fought.GroupBy(e => e.Kind).Select(g =>
                {
                    var types = new Dictionary<string, double>();
                    foreach (var e in g) for (int t = 0; t < BattleRecorder.Types.Length; t++) if (e.Dealt[t] > 0) Bump(types, BattleRecorder.Types[t], e.Dealt[t]);
                    return (kind: g.Key, n: g.Count(), d: g.Count(e => e.Defeated), a: g.Count(e => e.Outcome == FoeOutcome.GotAway), o: g.Count(e => e.Outcome == FoeOutcome.Fighting), dealt: types.Values.Sum(), types);
                }).OrderByDescending(x => x.dealt).ThenByDescending(x => x.n).ThenBy(x => x.kind, StringComparer.Ordinal).ToList();
                var most = kinds.Max(x => x.dealt);
                var items = kinds.Select(x =>
                {
                    var parts = x.dealt > 0 ? Parts(x.types, x.dealt) : new List<Block>();
                    var pips = Pips(x.d, x.a, x.o); if (pips != null) parts.Add(pips);
                    return new Block { Kind = "foe", Id = x.kind, Icon = FoeIcon(input, x.kind), Title = Who(input, x.kind), Value = x.dealt > 0 ? NAtLeast(x.dealt) : null, Value2 = Badge(x.n), Fraction = most > 0 ? (float)(x.dealt / most) : 0, Items = parts };
                }).ToList();
                var key = PipKey(items.Select(i => i.Items.FirstOrDefault(p => p.Kind == "pips")));
                if (key != null) items.Insert(0, key);
                view.Blocks.Add(new Block { Kind = "fightfoes", Title = "Foes you fought", Value = N(fought.Count), Text = together ? YourDamageToEach : null, Items = items });
                if (most > 0) AboutLines(view, (DamageBarsAboutTitle, DamageBarsAbout));
            }
            // what the group received, after each one's own armour
            var received = counted.Where(p => p.Received > 0).ToList();
            if (received.Count > 0)
                view.Blocks.Add(new Block
                {
                    Kind = "fightreceived", Value = NAtLeast(received.Sum(p => p.Received)), Title = together ? "damage received together" : "damage received", Text = together ? "after each player's armor" : AfterOf(input),
                    Items = together ? received.Select(p => new Block { Kind = "player", Id = p.Name, Title = p.You ? "you" : p.Name, Value = NAtLeast(p.Received), Colour = "person:" + p.Name }).ToList() : new List<Block>(),
                });
            Plate(view, "ui:chapter-battle", NearLine(input, f, parts));
            var plate = view.Blocks[0];
            plate.Pill = Clock(input, f.StartUtc, false) + " · " + (f.Open ? "still on" : FightLength(f.Length)) + (f.Open ? "" : " · " + Ago(input.NowUtc - f.EndUtc));
            plate.Items.Insert(0, new Block { Kind = "headlink", Id = "Battle/" + FeedPage, Title = EarlierFights, Icon = FeedIcon });
        }

        /// <summary>
        /// The plate's first line: who was in the fight with you ("Near you in this fight: Edda and Tor."), players near you who do not share,
        /// and the fellow players who share but were not in it ("Finch was elsewhere, so not in it.").
        /// </summary>
        public static string NearLine(PanelInput input, FoeFight f, List<FightPart> parts)
        {
            var with = f.With();
            var sharing = (input.Fellows ?? new List<PanelInput>()).Where(x => x != null && x != input && !SameName(x.PlayerName, input.PlayerName)).Select(x => x.PlayerName).ToList();
            var inIt = with.Where(n => sharing.Any(s => SameName(s, n))).ToList();
            var notSharing = with.Where(n => !sharing.Any(s => SameName(s, n)) && !SameName(n, input.PlayerName)).ToList();
            var elsewhere = sharing.Where(s => !with.Any(n => SameName(n, s))).ToList();
            var near = inIt.Concat(notSharing).ToList();
            var line = near.Count > 0 ? "Near you in this fight: " + Names(near) + "." : "Nobody was near you in this fight.";
            if (notSharing.Count > 0) line += " " + Names(notSharing) + (notSharing.Count == 1 ? " does not share, so is not in the numbers." : " do not share, so are not in the numbers.");
            if (elsewhere.Count > 0 && inIt.Count > 0) line += " " + Names(elsewhere) + (elsewhere.Count == 1 ? " was" : " were") + " elsewhere, so not in it.";
            return line;
        }
    }
}
