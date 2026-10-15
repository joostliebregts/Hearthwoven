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
    ///   On the page (0.8.1, FeedBlocks): one feed block per fight, each its own island, with Kind "feedline" between them (Tone "gap", Text the
    ///   quiet time) and after the last (Tone "note", Text what is left out), outside the islands.
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
        public static readonly string[] RememberedViews = { FeedViewKey, FeedWindowKey, RunsWindowKey };
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
            // per foe (0.8.5: one row per name the game gives, FoeGroup) its damage by type and (0.8.5) each type's after (AfterPart), in one pass
            var byFoe = new Dictionary<string, (Dictionary<string, double> types, Dictionary<string, DealtAmount> after)>();
            foreach (var r in dealt)
            {
                var foe = FoeGroup(input, r.Other) ?? "";
                if (!byFoe.TryGetValue(foe, out var f)) byFoe[foe] = f = (new Dictionary<string, double>(), new Dictionary<string, DealtAmount>());
                Bump(f.types, r.Type, r.Amount); BumpDealt(f.after, r.Type, r);
            }
            var all = byFoe.Select(kv => (key: kv.Key, total: kv.Value.types.Values.Sum(), types: kv.Value.types, after: kv.Value.after))
                           .Where(x => x.total > 0).OrderByDescending(x => x.total).ThenBy(x => x.key, StringComparer.Ordinal).ToList();
            if (all.Count == 0) return null;
            var counts = counted ? FoeCountsIn(input, w) : null;
            var fold = all.Count > RowTop;
            var shown = fold ? all.Take(RowTop - 1).ToList() : all;
            if (fold)
            {
                var rest = all.Skip(RowTop - 1).ToList(); var types = new Dictionary<string, double>(); var after = new Dictionary<string, DealtAmount>();
                foreach (var r in rest) { foreach (var kv in r.types) Bump(types, kv.Key, kv.Value); foreach (var kv in r.after) after[kv.Key] = (after.TryGetValue(kv.Key, out var o) ? o : default) + kv.Value; }
                shown.Add((FoldId, rest.Sum(r => r.total), types, after));
            }
            var max = shown.Max(x => x.total); var restCount = all.Count - (RowTop - 1);
            var foes = new List<Block>();
            foreach (var s in shown)
            {
                var c = s.key == FoldId ? null : counts?.Get(s.key);
                var parts = Parts(s.types, s.total);
                foreach (var p in parts) if (s.after.TryGetValue(p.Id, out var a)) AfterPart(p, a);
                var pips = c == null ? null : Pips(c.Defeated, c.GotAway, c.Fighting);
                if (pips != null) parts.Add(pips);
                foes.Add(SourcePicture(input, new Block
                {
                    Kind = "source", Id = s.key, Title = s.key == FoldId ? OthersLabel(restCount) : Who(input, s.key), Value = NAtLeast(s.total), Fraction = (float)(s.total / max),
                    Value2 = c == null ? null : Badge(c.Separate), Src = SrcPc, Source = TagMeasured, Items = parts,
                    Text = ThroughLine(s.after.Values.Aggregate(new DealtAmount(), (x, y) => x + y)),   // 0.8.5: the line's closing words
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

        /// <summary>A fight's day against today, both in the viewer's time, said after its clock in the feed's head: "" today, " yesterday", " on Thursday"
        /// (2 to 6 days back), " on 3 Oct" (older). 0.8.6: far down the feed the head said only "Fight at 20:46", and the day was lost.</summary>
        public static string FightDayOf(DateTime fightDay, DateTime today)
        {
            var days = (today.Date - fightDay.Date).Days;
            return days <= 0 ? "" : days == 1 ? " yesterday" : days < 7 ? " on " + fightDay.ToString("dddd", Inv) : " on " + fightDay.ToString("d MMM", Inv);
        }

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

        // ---------- 0.8.5: fights of earlier sessions (KeptFights) ----------

        /// <summary>Your fights of earlier sessions in this world (PanelInput.KeptSessions), newest first: the feed lists them under this session's;
        /// none on a fellow's book (their copy carries this session's feed only).</summary>
        public static List<FoeFight> KeptFightsOf(PanelInput input) =>
            input != null && input.IsSelf && input.KeptSessions != null ? input.KeptSessions.SelectMany(s => s.Fights).ToList() : new List<FoeFight>();

        /// <summary>A fight that ended before this session began: yours kept from an earlier session, or a fellow's from before you came in.</summary>
        public static bool BeforeThisSession(PanelInput input, DateTime endUtc)
        {
            var from = input == null ? null : input.IsSelf ? input.SessionStartUtc : input.ViewerSessionStartUtc;
            return from.HasValue && endUtc < from.Value;
        }

        /// <summary>How long ago in plain words (the book's time words, AwayLength): "45 minutes ago", "3 hours ago", "2 days ago".</summary>
        public static string AgoWords(TimeSpan t) => AwayLength(t) + " ago";

        /// <summary>A moment as people say it, in your own time: "at 18:05" (today), "yesterday 21:12", "Thursday 21:12" (within the week), "28 Sep 21:12".</summary>
        static string DayClock(PanelInput input, DateTime utc)
        {
            var d = Local(input, utc); var today = Local(input, input.NowUtc).Date; var days = (today - d.Date).Days; var clock = d.ToString("HH:mm", Inv);
            return days <= 0 ? "at " + clock : days == 1 ? "yesterday " + clock : days < 7 ? d.ToString("dddd", Inv) + " " + clock : ShortDate(d, today) + " " + clock;
        }

        /// <summary>
        /// When Last fight's fight was, for its head (Joost: "show your last fight, but indicate that it was x hours/days ago"): one of this session
        /// as before, the time of day first ("21:12 · 4 min · 5 min ago"); one that ended before this session began how long ago first, in plain
        /// words, then when ("3 hours ago, at 18:05 · 4 min", "2 days ago, Thursday 21:12 · 4 min").
        /// </summary>
        public static string FightWhen(PanelInput input, DateTime startUtc, DateTime endUtc, string length) =>
            BeforeThisSession(input, endUtc) ? AgoWords(input.NowUtc - endUtc) + ", " + DayClock(input, startUtc) + " · " + length
                                             : Clock(input, startUtc, false) + " · " + length + " · " + Ago(input.NowUtc - endUtc);

        /// <summary>The feed's quiet line over an earlier session's fights: its day and its span in your own time, "Yesterday, 20:05 to 23:40",
        /// "Earlier today, 14:10 to 15:02", "Thursday, 20:05 to 23:40", "28 Sep, 20:05 to 23:40".</summary>
        public static string SessionWords(PanelInput input, KeptSession s)
        {
            var from = s.FromUtc > DateTime.MinValue ? s.FromUtc : s.Fights.Select(f => f.StartUtc).DefaultIfEmpty(s.ToUtc).Min();
            var a = Local(input, from); var b = Local(input, s.ToUtc); var today = Local(input, input.NowUtc).Date; var days = (today - a.Date).Days;
            var day = days <= 0 ? "Earlier today" : days == 1 ? "Yesterday" : days < 7 ? a.ToString("dddd", Inv) : ShortDate(a, today);
            return day + ", " + a.ToString("HH:mm", Inv) + " to " + b.ToString("HH:mm", Inv);
        }

        public const string KeptAboutTitle = "Earlier sessions";
        public static readonly string KeptAbout = "This PC keeps your newest " + KeptFights.MaxFoes + " foes of earlier sessions in this world, up to " + KeptFights.MaxAge.Days +
                                                  " days back, and the parts of the fellow players who shared those fights, as their copies showed them then.";

        static string Names(IList<string> names) => names.Count == 0 ? "" : names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1).ToArray()) + " and " + names[names.Count - 1];

        /// <summary>A name as a fellow's book says it (0.8.2): the viewer's own marked "Rowan (you)", as Company marks it (a fellow's book never says
        /// "you" about anyone); every other name as it is.</summary>
        static string Person(PanelInput input, string name) => !input.IsSelf && SameName(name, input.ViewerName) ? name + " (you)" : name;
        static bool IsViewer(PanelInput input, string name) => !input.IsSelf && SameName(name, input.ViewerName);

        /// <summary>The names in words, the viewer first on a fellow's book ("Rowan (you)"), then the players their copy does not name (FeedShare:
        /// "another player", "2 other players") and those the game counted without a name (Others); "" for nobody.</summary>
        static string WithWords(PanelInput input, IEnumerable<string> names, int outside, int others = 0)
        {
            var list = (names ?? FoeEncounter.NoNames).OrderBy(n => IsViewer(input, n) ? 0 : 1).Select(n => Person(input, n)).ToList();
            return list.Count == 0 && outside == 0 && others == 0 ? "" : FoeEncounter.Names(list, others, outside);
        }

        /// <summary>A foe's outcome as the book says it: your own ("defeated by you"), or on a fellow's book theirs ("defeated by Edda", "defeated with
        /// Rowan (you) and another player").</summary>
        static string OutcomeOf(PanelInput input, FoeEncounter e)
        {
            if (input.IsSelf) return e.OutcomeText;
            if (e.Outcome == FoeOutcome.DefeatedByYou) return "defeated by " + Name(input);
            if (e.Outcome == FoeOutcome.DefeatedWith) return "defeated with " + FoeEncounter.Names(e.With.OrderBy(n => IsViewer(input, n) ? 0 : 1).Select(n => Person(input, n)), e.Others, e.Outside);
            return e.OutcomeText;
        }

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
            var with = WithWords(input, f.With(), f.Outside);
            var part = entries.Count < f.Foes;
            double dealt = part ? entries.Sum(e => (double)e.DealtTotal) : f.DealtTotal, received = part ? entries.Sum(e => (double)e.ReceivedTotal) : f.ReceivedTotal;
            var b = new Block
            {
                Kind = "fight", Id = f.Number.ToString(Inv), Title = "Fight at " + Clock(input, f.StartUtc, false) + FightDayOf(Local(input, f.StartUtc), Local(input, input.NowUtc)),
                Text = (f.Open ? "still on" : FightLength(f.Length)) + " · " + (part ? N(entries.Count) + " of " : "") + N(f.Foes) + (f.Foes == 1 ? " foe" : " foes"),
                Note = with.Length > 0 ? "with " + with : null, Value = dealt > 0 ? NAtLeast(dealt) : null, Value2 = received > 0 ? NAtLeast(received) : null, Count = f.Foes, Items = new List<Block>(),
            };
            if (input.IsSelf && !part) { var pace = FightPace(f.SwingSeconds, f.SwingDamage); if (pace.Length > 0 && pace != DpsDash) b.Rate = pace; }   // 0.8.6: your pace in the fight (not when a filter shows only part of its foes: the fighting time is the fight's); under the floor no dash in the head
            foreach (var e in entries)
            {
                var parts = TypeParts(input, e.Dealt, FeedDealt); parts.AddRange(TypeParts(input, e.Received, FeedReceived));
                b.Items.Add(new Block
                {
                    Kind = "entry", Id = e.Kind, Icon = FoeIcon(input, e.Kind), Title = Who(input, e.Kind), Text = Clock(input, e.EndUtc, true), Tone = OutcomeTone(e), Note = OutcomeOf(input, e),
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
            /// <summary>0.8.5: how many times Show older foes was pressed (PanelState.FeedMore): the pages drawn after the first.</summary>
            public int More;
            /// <summary>0.8.5: a line after what is left out (your Session: where the earlier sessions' fights show); null: none.</summary>
            public string After;
            /// <summary>0.8.6 (a friend's older feed): the friend's sessions in this window that are not on this PC yet; null: none (your own feed, a short window, nothing missing).</summary>
            public FeedServer Server;
            /// <summary>0.8.6: what FeedBlock drew: the foes shown, the foes held on this PC that pass, and the presses that drew foes (FeedFetch reads them).</summary>
            public int Drawn, Total, Used;
            public bool InWindow(FoeEncounter e) => !From.HasValue || e.EndUtc >= From.Value;
            public bool Shows(FoeEncounter e) => InWindow(e) && (Pass == null || Pass(e));
        }

        /// <summary>
        /// The battle feed (Joost: Log, Cards and Timeline over the same fights, the choice remembered): your fights this session, newest first, each
        /// foe with the time, the outcome in the book's words and both directions per damage type (dealt before the foe's armour, received after
        /// yours). <paramref name="pick"/> (0.8.1): only the foes in the window that pass the filter, each fight with those of its foes (its heading
        /// says "2 of 6 foes"). The first page draws at most FeedTop foes, counted after the window and the filter: the newest fights whole while
        /// they fit (the newest alone cut at FeedTop). 0.8.5 (Joost in game: "I'm not sure that these are forty. Can we paginate it?"): each press of
        /// Show older foes (pick.More) adds a page, the next fights whole (a cut one completed first) until at least FeedTop more foes are drawn, so
        /// a fight is never cut in half; the pages before stay as they were. Title: the button's words while foes are left (null: none left); Note:
        /// how many foes and fights are shown and how many are left, so the two add up to the window's. Null when no foe is left to show.
        /// </summary>
        /// <summary>0.8.6: a friend's sessions the server holds that this PC has not fetched, in the window: Missing newest first, Next the first of them (the
        /// one the next press needs), State its state ("loading", "failed" or null), UpTo whether their foe counts are an upper bound (a filter is on, or the
        /// first one starts before the window). Index and Of: where Next is among the window's sessions on the server (Of counts the ones here too); Waited: the seconds Next has
        /// been on its way when the input says (PanelInput.BlockWaited), else -1 (the book counts them on the line itself, PanelUi.FetchTick).</summary>
        public sealed class FeedServer { public List<BlockRow> Missing; public BlockRow Next; public string State; public bool UpTo; public int Index = 1, Of = 1, Waited = -1; }

        public static Block FeedBlock(PanelInput input, List<FoeFight> fights, string view, FeedPick pick = null)
        {
            if (fights == null || fights.Count == 0) return null;
            var narrowed = pick != null && (pick.From.HasValue || pick.Filtered);
            var matched = new List<(FoeFight f, List<FoeEncounter> e)>();
            foreach (var f in fights) { var e = pick == null ? f.Entries : f.Entries.Where(pick.Shows).ToList(); if (e.Count > 0) matched.Add((f, e)); }
            if (matched.Count == 0) return null;
            var total = matched.Sum(m => m.e.Count);
            var shown = new List<(FoeFight f, List<FoeEncounter> e)>(); int n = 0;
            foreach (var (f, e) in matched)
            {
                if (n >= FeedTop || (shown.Count > 0 && n + e.Count > FeedTop)) break;
                var take = e.Take(FeedTop - n).ToList(); shown.Add((f, take)); n += take.Count;
            }
            int used = 0;
            for (int k = 0; k < (pick?.More ?? 0) && n < total; k++) { n = FeedPageOn(matched, shown, n); used++; }
            if (pick != null) { pick.Drawn = n; pick.Total = total; pick.Used = used; }
            int nextFoes = 0;   // what the next press adds (the button says it)
            if (n < total) nextFoes = FeedPageOn(matched, new List<(FoeFight f, List<FoeEncounter> e)>(shown), n) - n;
            var largest = shown.SelectMany(x => x.e).Select(e => (double)e.DealtTotal).DefaultIfEmpty(0).Max();
            var items = new List<Block>();
            for (int i = 0; i < shown.Count; i++)
            {
                var fb = FightBlock(input, shown[i].f, shown[i].e, largest);
                // 0.8.5: over the first fight shown of an earlier session, its day and span ("Yesterday, 20:05 to 23:40"), in place of a quiet time
                var kept = shown[i].f.Kept;
                if (kept != null && (i == 0 || shown[i - 1].f.Kept != kept)) fb.Items.Insert(0, new Block { Kind = "session", Text = SessionWords(input, kept) });
                // the quiet time between this fight and the next one shown below it; when the window or the filter hides fights between them,
                // the time and how many lie between. The last fight shown has none: the fight before it is not on the page (the window, the
                // filter or the cap leaves it out; review 0.8.1 batch 6: its gap pointed out of the window, at the foot of the Timeline)
                if (i + 1 < shown.Count && shown[i + 1].f.Kept == kept)
                {
                    int at = fights.IndexOf(shown[i].f), below = fights.IndexOf(shown[i + 1].f);
                    var quiet = shown[i].f.StartUtc - shown[i + 1].f.EndUtc;
                    fb.Items.Add(new Block { Kind = "gap", Text = below > at + 1 ? GapPast(quiet, below - at - 1) : QuietTime(quiet) });
                }
                items.Add(fb);
            }
            var leftFights = matched.Count - shown.Count;
            // REVIEW-08 #9: foes of this session the book no longer keeps (0.8.2: or a fellow's copy does not carry); said where the window reaches
            // back past the oldest fight it keeps (0.8.5: not where earlier sessions' fights go on below it)
            var keptOn = fights.Any(f => f.Kept != null);
            var friend = input != null && !input.IsSelf;
            var older = keptOn && !friend ? 0 : FeedLeftOut(input);   // 0.8.6: a friend's copy still says what it left out, with their sessions from the server after it
            var copyEnd = (friend ? fights.LastOrDefault(f => f.Kept == null) : null) ?? fights[fights.Count - 1];
            if (pick?.From != null && copyEnd.StartUtc < pick.From.Value) older = 0;
            var server = pick?.Server;
            var that = pick != null && pick.Filtered ? " that match" : "";
            var where = that.Length > 0 ? that : narrowed || keptOn || server != null ? " in this window" : older > 0 ? "" : " this session";
            // only the last fight shown can be cut (the first page's newest fight alone past FeedTop): its foes left out, then the earlier fights
            var cut = matched[shown.Count - 1].e.Count - shown[shown.Count - 1].e.Count; var earlier = total - n - cut;
            string note = null;
            if (n < total || (pick?.More ?? 0) > 0 || server != null)
            {
                note = n < total
                    ? "Shown: " + N(shown.Count) + (shown.Count == 1 ? " fight, " : " fights, ") + N(n) + " of " + N(total) + " foes" + where + "."
                    : "Shown: all " + (shown.Count == 1 ? "1 fight, " : N(shown.Count) + " fights, ") + N(n) + (n == 1 ? " foe" : " foes") + where + ".";
                var parts = new List<string>();
                if (cut > 0) parts.Add(N(cut) + " more " + (cut == 1 ? "foe" : "foes") + " in this fight");
                if (leftFights > 0) parts.Add(N(leftFights) + (leftFights == 1 ? " earlier fight" : " earlier fights") + " (" + N(earlier) + (earlier == 1 ? " foe" : " foes") + ")");
                if (server != null) parts.Add(ServerCount(server));
                if (parts.Count > 0) note += " Not shown: " + Names(parts) + ".";
            }
            if (older > 0) note = (note == null ? "" : note + " ") + "Older fights (" + N(older) + (older == 1 ? " foe" : " foes") + (input == null || input.IsSelf
                ? ") are no longer kept: the book keeps your newest " + BattleRecorder.MaxFeed + " foes."
                : ") are not in " + Name(input) + "'s copy: it carries the newest " + FeedShare.MaxFoes + " foes.");
            if (!string.IsNullOrEmpty(pick?.After)) note = (note == null ? "" : note + " ") + pick.After;
            if (n >= total && server != null) items.Add(ServerLine(input, server));   // the next session from the server: its button, or what it is doing
            return new Block { Kind = "feed", Tone = view, Title = n < total ? FeedMoreWords(nextFoes) : null, Note = note, Count = matched.Count, Items = items };
        }

        /// <summary>0.8.6: a friend's sessions on the server not on this PC yet, as the count line names them: "2 earlier sessions on the server (up to 420 foes)".</summary>
        static string ServerCount(FeedServer s)
        {
            var foes = s.Missing.Sum(r => r.Foes);
            return N(s.Missing.Count) + (s.Missing.Count == 1 ? " earlier session" : " earlier sessions") + " on the server (" + (s.UpTo ? "up to " : "") + N(foes) + (foes == 1 ? " foe" : " foes") + ")";
        }

        /// <summary>0.8.6: Try again after a session did not arrive (its id in the line's Value).</summary>
        public const string FeedFetchTarget = "feed:fetch", ShowOlderWords = "Show older foes", TryAgainWords = "Try again";

        /// <summary>Which session a line speaks of: "Edda's session of Thursday, 20:05 to 23:40" (the day word in small letters after "of" where it is a word, not a name).</summary>
        static string SessionOfWords(PanelInput input, BlockRow r)
        {
            var w = SessionWords(input, new KeptSession { FromUtc = r.FromUtc, ToUtc = r.ToUtc });
            if (w.StartsWith("Earlier today", StringComparison.Ordinal) || w.StartsWith("Yesterday", StringComparison.Ordinal)) w = char.ToLowerInvariant(w[0]) + w.Substring(1);
            return Name(input) + "'s session of " + w;
        }

        // the end of the feed once every foe held on this PC is drawn and the friend's next session is on the server (FeedBlocks turns it into the last lines)
        static Block ServerLine(PanelInput input, FeedServer s)
        {
            var of = SessionOfWords(input, s.Next);
            if (s.State == "loading") return new Block { Kind = "serverline", Tone = "loading", Text = FetchWaited(FetchingWords(of, s.Index, s.Of), s.Waited), Value = s.Next.Id };
            if (s.State == "failed") return new Block { Kind = "serverline", Tone = FeedMoreTone, Title = TryAgainWords, Text = of + " did not arrive.", NoteLink = FeedFetchTarget, Value = s.Next.Id };
            return new Block { Kind = "serverline", Tone = FeedMoreTone, Title = ShowOlderWords, NoteLink = FeedMoreTarget };
        }

        /// <summary>0.8.6 (test build 5, Joost: "the loading took a while ... We should have something of a progress indicator there"): the line while a friend's session is on its
        /// way, "Getting Edda's session of Sunday, 20:25 to 22:30 from the server · 1 of 2" (the count only when the window holds more than one of their sessions on the server).</summary>
        public static string FetchingWords(string of, int index, int count) => "Getting " + of + " from the server" + (count > 1 ? " · " + N(index) + " of " + N(count) : "");
        /// <summary>The same line with the seconds it has been on its way ("... · 6 s"); <paramref name="seconds"/> below 0: as it is. The book sets only that line's text with
        /// it, once a second (PanelUi.FetchTick), so the page is not built again for a clock.</summary>
        public static string FetchWaited(string text, int seconds) => seconds < 0 ? text : text + " · " + N(seconds) + " s";

        /// <summary>0.8.5: the feed's button at its end, a press adds the next page (PanelState.FeedMore; Enter, Space or the pad's A too).</summary>
        public const string FeedMoreTarget = "feed:more", FeedMoreKeyLine = "[Enter] Older foes";
        /// <summary>The button's words: what one press adds ("Show 52 older foes").</summary>
        public static string FeedMoreWords(int foes) => "Show " + N(foes) + " older " + (foes == 1 ? "foe" : "foes");

        /// <summary>The feed's Show older foes line on this page, or null (none left, or another page).</summary>
        public static Block FeedMoreLine(PanelView view) =>
            view == null || view.ShowAbout || view.Active != Chapter.Battle || view.Page != FeedPage ? null : Content(view).FirstOrDefault(b => b.Kind == FeedLineKind && b.Tone == FeedMoreTone);

        /// <summary>The key line: Enter (or Space, the pad's A) shows older foes while the button is on the page, after the page keys.</summary>
        static void FeedFinish(PanelState state, PanelView view)
        {
            if (state.FilterRow >= 0 || FeedMoreLine(view) == null) return;   // the filter focus has Enter (FacetModel.cs)
            var page = view.Keys.IndexOf("[W/S] Page");
            view.Keys.Insert(page >= 0 ? page + 1 : view.Keys.Count, FeedMoreKeyLine);
        }

        /// <summary>The feed's blocks on a page (the chosen view of its switch, or the blocks themselves when it has one view), or null.</summary>
        static List<Block> FeedListOf(PanelView v)
        {
            var plate = PlateOf(v); var items = plate?.Items ?? v?.Blocks;
            if (items == null) return null;
            foreach (var b in items)
            {
                if (b?.Kind == "switch") { var chosen = (b.Items ?? new List<Block>()).FirstOrDefault(x => x.Selected); if (chosen?.Items != null && chosen.Items.Any(x => x?.Kind == "feed")) return chosen.Items; }
                if (b?.Kind == "feed") return items;
            }
            return null;
        }

        /// <summary>
        /// 0.8.5: Show older foes draws only the new page (PanelUi.AppendFeed), never the page again, when that is all that changed: the page
        /// <paramref name="after"/> is <paramref name="before"/> (drawn as <paramref name="beforeJson"/>) with its last line, the button, replaced by
        /// further fights and lines, every block above it the same (the same scale: a bar's fraction is in its block), and nothing else on the page
        /// changed but the key line. Returns the index in Islands(PlateOf(after)) of the first island to draw, or -1 when the page must be drawn
        /// whole (a fight came in, the scale grew, the first page's cut fight was completed, another page).
        /// </summary>
        public static int FeedAppendAt(PanelView before, string beforeJson, PanelView after)
        {
            if (before == null || after == null || beforeJson == null || after.ShowAbout || after.Active != Chapter.Battle || after.Page != FeedPage || before.Page != FeedPage) return -1;
            var was = FeedListOf(before); var now = FeedListOf(after);
            if (was == null || now == null || was.Count == 0 || now.Count < was.Count || was[was.Count - 1].Kind != FeedLineKind || was[was.Count - 1].Tone != FeedMoreTone) return -1;
            var kept = was.Count - 1;
            // the rest of the page: the same with the old blocks in the feed's place and the old key line
            var nowItems = now.ToList(); var nowKeys = after.Keys.ToList(); bool same;
            try { now.Clear(); now.AddRange(was); after.Keys.Clear(); after.Keys.AddRange(before.Keys); same = ToJson(after) == beforeJson; }
            finally { now.Clear(); now.AddRange(nowItems); after.Keys.Clear(); after.Keys.AddRange(nowKeys); }
            if (!same) return -1;
            string Of(List<Block> list) { var v = new PanelView(); v.Blocks.AddRange(list.Take(kept)); return ToJson(v); }
            if (Of(was) != Of(now)) return -1;
            var plan = Islands(PlateOf(after));
            var at = plan.FindIndex(i => i.Parts.Count > 0 && ReferenceEquals(i.Parts[0].Block, now[kept]));
            // the islands from there on are exactly the new blocks, in order (nothing of the page follows the feed)
            if (at < 0 || plan.Count - at != now.Count - kept) return -1;
            for (int k = at; k < plan.Count; k++) if (plan[k].Parts.Count != 1 || !ReferenceEquals(plan[k].Parts[0].Block, now[kept + k - at])) return -1;
            return at;
        }

        /// <summary>One more page of the feed onto <paramref name="shown"/> (the fights drawn so far, newest first; <paramref name="n"/> their foes):
        /// the last one completed if the first page cut it, then the next fights whole while fewer than FeedTop foes were added. Returns the foes
        /// drawn after it.</summary>
        static int FeedPageOn(List<(FoeFight f, List<FoeEncounter> e)> matched, List<(FoeFight f, List<FoeEncounter> e)> shown, int n)
        {
            var limit = n + FeedTop;
            if (shown.Count > 0 && shown[shown.Count - 1].e.Count < matched[shown.Count - 1].e.Count)
            {
                var whole = matched[shown.Count - 1]; n += whole.e.Count - shown[shown.Count - 1].e.Count; shown[shown.Count - 1] = whole;
            }
            while (shown.Count < matched.Count && n < limit) { var next = matched[shown.Count]; shown.Add(next); n += next.e.Count; }
            return n;
        }

        /// <summary>The kind of a quiet line between the feed's fight islands, outside them: Tone "gap" the quiet time to the fight below,
        /// Tone "note" (after the last) what is left out.</summary>
        public const string FeedLineKind = "feedline", FeedMoreTone = "more";

        /// <summary>
        /// The feed as the page lays it out (0.8.1, Joost: every fight its own card): FeedBlock split into one "feed" block per fight (the view in
        /// Tone, the one fight in Items), each its own island (Islands.cs); between two of them a FeedLineKind line "gap" with the quiet time, and
        /// after the last one a FeedLineKind line "note" with what is left out, both outside the islands; 0.8.5: over an earlier session's first
        /// fight a FeedLineKind line "session" with its day and span; 0.8.5: while foes are left, the last line is Tone FeedMoreTone with the
        /// Show older foes button (Title, NoteLink FeedMoreTarget) before what is shown and left (Text). Empty when FeedBlock is null.
        /// </summary>
        public static List<Block> FeedBlocks(Block feed)
        {
            var list = new List<Block>();
            if (feed?.Items == null) return list;
            var server = feed.Items.FirstOrDefault(x => x.Kind == "serverline");
            foreach (var f in feed.Items.Where(x => x.Kind != "serverline"))
            {
                var gap = (f.Items ?? new List<Block>()).FirstOrDefault(x => x.Kind == "gap");
                var session = (f.Items ?? new List<Block>()).FirstOrDefault(x => x.Kind == "session");
                if (gap != null || session != null) f.Items = f.Items.Where(x => x != gap && x != session).ToList();
                if (session != null) list.Add(new Block { Kind = FeedLineKind, Tone = "session", Text = session.Text });
                list.Add(new Block { Kind = "feed", Tone = feed.Tone, Count = 1, Items = new List<Block> { f } });
                if (gap != null) list.Add(new Block { Kind = FeedLineKind, Tone = "gap", Text = gap.Text });
            }
            // 0.8.5: while foes are left, the line holds the Show older foes button (Title) before its words
            if (!string.IsNullOrEmpty(feed.Title)) list.Add(new Block { Kind = FeedLineKind, Tone = FeedMoreTone, Title = feed.Title, Text = feed.Note, NoteLink = FeedMoreTarget });
            // 0.8.6: every foe held here is drawn and the friend's next session is on the server: its button with the words (Show older foes), or the words alone
            // (the count line, then "Getting ..." while it comes, or then the failure and Try again)
            else if (server != null && server.Tone == FeedMoreTone && server.NoteLink == FeedMoreTarget) list.Add(new Block { Kind = FeedLineKind, Tone = FeedMoreTone, Title = server.Title, Text = feed.Note, NoteLink = server.NoteLink });
            else if (server != null)
            {
                if (!string.IsNullOrEmpty(feed.Note)) list.Add(new Block { Kind = FeedLineKind, Tone = "note", Text = feed.Note });
                list.Add(new Block { Kind = FeedLineKind, Tone = server.Tone, Title = server.Title, Text = server.Text, NoteLink = server.NoteLink, Value = server.Value });
            }
            else if (!string.IsNullOrEmpty(feed.Note)) list.Add(new Block { Kind = FeedLineKind, Tone = "note", Text = feed.Note });
            return list;
        }

        /// <summary>Every entry of every fight on the page (its blocks as Content gives them), newest first, over all the feed blocks FeedBlocks
        /// made. The Log measures its name, outcome and received columns over these once per page (PanelUi.FeedPageColumns, the mirror's
        /// feedPageEntries), not per fight island, so every bar of the feed starts at one x on one track (REVIEW-081, 0.8.2 pass: a fight where
        /// nothing hurt you had no received column and a track about 160 px longer).</summary>
        public static List<Block> FeedPageEntries(IEnumerable<Block> content) =>
            (content ?? Enumerable.Empty<Block>()).Where(b => b?.Kind == "feed").SelectMany(b => b.Items ?? new List<Block>()).Where(f => f?.Kind == "fight")
                .SelectMany(f => f.Items ?? new List<Block>()).Where(e => e?.Kind == "entry").ToList();

        // the feed's blocks, kept while the recorder has not changed (BattleRecorder.Version) and the view and the choice are the same: the book
        // refreshes every 2 s, the feed changes only with a hit, a kill, a fight's end, or (a short window) a foe that leaves the window, which
        // the key's count of foes in it notes. The blocks carry no Source, so the build's later passes leave them as they are
        // (0.8.2: a fellow's shared feed is one object per copy received, read by whose book it is and who is looking)
        // (0.8.5: and the earlier sessions' fights it lists, which change only with a new session)
        static object feedRec, feedKept; static int feedAt = -1; static string feedView, feedKey, feedWho; static Func<DateTime, DateTime> feedLocal; static List<Block> feedBlocks;
        static int feedDrawn, feedTotal, feedUsed;
        static List<Block> FeedOf(PanelInput input, List<FoeFight> fights, string view, FeedPick pick)
        {
            object rec = input.IsSelf ? (object)input.Battle : input.SharedFeed;
            var at = input.IsSelf ? input.Battle?.Version ?? -1 : 0; var who = input.IsSelf ? "" : input.PlayerName + "|" + input.ViewerName;
            var key = pick.Key + "|" + pick.More + "|" + input.BlocksStamp;   // 0.8.5: and the pages shown; 0.8.6: and a friend's sessions from the server (what is here, what comes)
            if (rec != null && ReferenceEquals(rec, feedRec) && ReferenceEquals(input.KeptSessions, feedKept) && at == feedAt && view == feedView && key == feedKey && who == feedWho && feedLocal == input.ToLocal && feedBlocks != null)
            { pick.Drawn = feedDrawn; pick.Total = feedTotal; pick.Used = feedUsed; return feedBlocks; }
            var b = FeedBlocks(FeedBlock(input, fights, view, pick));
            feedRec = rec; feedKept = input.KeptSessions; feedAt = at; feedView = view; feedKey = key; feedWho = who; feedLocal = input.ToLocal; feedBlocks = b;
            feedDrawn = pick.Drawn; feedTotal = pick.Total; feedUsed = pick.Used;
            return b;
        }

        // ---------- the feed's window and filter (0.8.1) ----------

        /// <summary>The windows a fellow's feed can show: the short ones and Session (their copy holds this session's fights, no days, no earlier
        /// session). Your own feed shows every window (0.8.5): the days and All reach back into the fights kept from earlier sessions.</summary>
        public static bool FeedWindowOpen(TimeWindow w) => IsShortWindow(w);
        static bool FeedWindowOpen(PanelInput input, TimeWindow w) => (input != null && input.IsSelf) || FeedWindowOpen(w) || (FriendHasBlocks(input) && (IsDayWindow(w) || w == TimeWindow.SinceInstall));

        /// <summary>0.8.6: a fellow's book whose server lists sessions of theirs (PanelInput.BlockRows): their days and All open, the sessions coming one at a time.</summary>
        public static bool FriendHasBlocks(PanelInput input) => input != null && !input.IsSelf && input.BlockRows != null && input.BlockRows.Count > 0;
        public const string FeedDaysWhy = "The feed holds this session's fights only, not days. Session shows every fight it holds.",
                            FeedAllWhy = "The feed holds this session's fights only. Damage and Foes count earlier sessions.";
        /// <summary>0.8.2: a fellow's Session with no fight since you came in (their earlier fights lie before it).</summary>
        public static string FeedBeforeYouLine(PanelInput i) => "Session holds the fights " + SinceViewer(i) + ". Choose 3 h for " + Name(i) + "'s fights before that.";
        public const string FeedLongerLine = "Choose a longer window, or Session for every fight the feed holds.",
                            FeedLongerKept = "Choose a longer window: All shows every fight the book keeps.",
                            FeedClearLine = "Clear the filter to see every fight in this window.";
        /// <summary>The feed's empty title for a window, the window said in words: "No fight today", "No fight this session", "No fight in the last 30 min" (0.8.6:
        /// "No fight in the today" read oddly); All has no window to name.</summary>
        public static string NoFightTitle(TimeWindow w) =>
            w == TimeWindow.SinceInstall ? "No fight recorded" : w == TimeWindow.Today || w == TimeWindow.Session ? "No fight " + RunsWindowWords(w) : "No fight in " + RunsWindowWords(w);
        public const string FeedFilterAboutTitle = "Window and filter";
        public static readonly string FeedFilterAbout = "The window, Biome and Foe show only the foes that pass them: a fight then says how many of its foes show, and its damage is theirs. The newest fights that pass are drawn whole, up to " +
                                                        FeedTop + " foes. At the end of the feed, Show older foes draws the next fights, about " + FeedTop + " foes for each press, and the line beside it says how many are shown and how many are left.";

        /// <summary>0.8.5: your Session with no fight yet while earlier sessions' fights are kept: when the last one was, and the window that shows it.</summary>
        public static string FeedEarlierLine(PanelInput input, DateTime lastEndUtc) =>
            "Your last fight was " + AgoWords(input.NowUtc - lastEndUtc) + ". Choose " + WindowShort(FeedWindowHolding(input, lastEndUtc, AllWindows[0])) + " or a longer window to see it.";

        /// <summary>Where the feed's window starts (null: no start): the short windows their cut, the days their first local midnight, a fellow's
        /// Session yours (B33); your Session holds this session's fights only, so it needs no cut, and All none.</summary>
        static DateTime? FeedFrom(PanelInput input, TimeWindow w) =>
            IsDayWindow(w) ? WindowStartUtc(input, w) : Cutoff(w, input.NowUtc) ?? (w == TimeWindow.Session && !input.IsSelf ? SessionFrom(input) : null);

        /// <summary>The first window from <paramref name="from"/> on (in the chips' order) that shows a fight which ended at <paramref name="endUtc"/>:
        /// never Session (it holds this session's fights only); All always.</summary>
        static TimeWindow FeedWindowHolding(PanelInput input, DateTime endUtc, TimeWindow from)
        {
            foreach (var w in AllWindows.SkipWhile(x => x != from))
                if (w != TimeWindow.Session && (!(FeedFrom(input, w) is DateTime start) || endUtc >= start)) return w;
            return TimeWindow.SinceInstall;
        }

        /// <summary>
        /// The feed's window chips: the book's one window set (Damage's chips); on a fellow's book the days and All greyed with their own reason.
        /// The window shown: the book's chosen one when it is a window the feed can show and it was chosen this game (PanelState.WindowPicked); else
        /// the one the feed last showed (FeedWindowKey, kept between sessions as its view is), else Session. Only a feed window chosen this game is
        /// noted (review 0.8.1 batch 6: 7 days chosen on Deeds overwrote the kept 1 h, and the next session opened on Session). 0.8.5: your own feed
        /// with no fight yet this session and no window chosen this game opens on the first window from that one on that shows your last fight
        /// (after a restart the feed kept Session, which holds none yet); the window kept stays as it was.
        /// </summary>
        static TimeWindow FeedWindowChips(PanelInput input, PanelState state, PanelView view, int liveFights, List<FoeFight> earlier)
        {
            var chosen = state.WindowPicked && FeedWindowOpen(input, state.Window);
            TimeWindow? kept = state.View.TryGetValue(FeedWindowKey, out var k) && Enum.TryParse(k, out TimeWindow kw) && Enum.IsDefined(typeof(TimeWindow), kw) && FeedWindowOpen(input, kw) ? kw : (TimeWindow?)null;
            var shown = chosen ? state.Window : kept ?? (FeedWindowOpen(input, state.Window) ? state.Window : TimeWindow.Session);
            if (!chosen && liveFights == 0 && earlier.Count > 0) shown = FeedWindowHolding(input, earlier[0].EndUtc, shown);
            view.HasFilters = true; view.ShownWindow = shown;
            foreach (var w in AllWindows)
                view.Windows.Add(new Choice { Id = w.ToString(), Label = WindowShort(w), Selected = w == shown, Disabled = !FeedWindowOpen(input, w), Why = FeedWindowOpen(input, w) ? null : IsDayWindow(w) ? FeedDaysWhy : FeedAllWhy });
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
            // 0.8.5: a chip per foe by the game's name (FoeGroup); a foe chosen before (one kind of a name) is that name's chip
            var picked = Chosen(state, FeedFilter, "foe");
            for (int i = picked.Count - 1; i >= 0; i--) { var g = FoeGroup(input, picked[i]); if (g == picked[i]) continue; if (picked.Contains(g)) picked.RemoveAt(i); else picked[i] = g; }
            var foe = new FacetDef { Id = "foe", Title = "Foe", Bar = false,
                                     Options = inWindow.GroupBy(e => FoeGroup(input, e.Kind)).OrderByDescending(g => g.Count()).ThenBy(g => Who(input, g.Key), StringComparer.Ordinal).Select(g => new FacetOption { Id = g.Key, Label = Who(input, g.Key) }).ToList() };
            foreach (var id in Chosen(state, FeedFilter, "foe").Where(id => foe.Options.All(o => o.Id != id)).ToList()) foe.Options.Add(new FacetOption { Id = id, Label = Who(input, id) });
            var defs = new[] { biome, foe }.Where(d => d.Options.Count >= 2 || Chosen(state, FeedFilter, d.Id).Count > 0).ToList();
            if (defs.Count == 0) return (null, false, null);
            var items = inWindow.Select((e, i) => { var it = new FacetItem { Key = i.ToString(Inv), Weight = 1 }; it.Values["biome"] = e.Biome ?? ""; it.Values["foe"] = FoeGroup(input, e.Kind) ?? ""; return it; }).ToList();
            var res = Facets(state, FeedFilter, defs, items, "foes", SrcPc, "foe", "foes", "All foes");
            var line = (res.Shown.Count < items.Count ? N(res.Shown.Count) + " of " : "") + N(items.Count) + (items.Count == 1 ? " foe" : " foes");
            res.Bar.Text = line; res.Bar.Tone = "compact";
            var applied = res.Bar.Items.First(b => b.Kind == "applied"); applied.Text = line; applied.Value = "All foes";
            var shown = new HashSet<FoeEncounter>(res.Shown.Select(it => inWindow[int.Parse(it.Key, Inv)]));
            return (res.Bar, defs.Any(d => Chosen(state, FeedFilter, d.Id).Count > 0), shown.Contains);
        }

        /// <summary>0.8.2: a fellow's copy from before 0.8.2 carries no feed: their Feed and Last fight say so, in the one line of an older copy (OlderLine).</summary>
        public static string FeedNotShared(PanelInput i) => OlderLine(i);
        public const string NoFightYet = "No fight yet this session", NoFightText = "Your fights show up here as you play, newest first.";
        public const string LastFightEmptyText = "After a fight, this page shows it: your part and the part of the fellow players near you.";
        public const string EarlierFights = "Earlier fights";
        public static readonly (string title, string text)[] FeedAbout =
        {
            ("The feed", "Your fights, newest first: this session's, then your earlier sessions' in this world. A fight ends after a minute without hits. Session shows this session's only. This PC keeps your newest " +
                         KeptFights.MaxFoes + " foes of earlier sessions, up to " + KeptFights.MaxAge.Days + " days back. Fellow players who share see this session's newest " + FeedShare.MaxFoes + " foes; anyone who does not share is another player there."),
            ("What became of a foe", "Defeated by you: the game counted the kill for you alone. Defeated with: fellow players hit it too. Survived: it still stood when the fight ended. Got away: the game took it away without a kill."),
            ("Damage", "Dealt is before the foe's armor, as on Damage. Received is after your armor, as on Defense."),
        };

        /// <summary>The About lines of the feed for whose book it is: yours (FeedAbout), or a fellow's as their copy shares it (0.8.2).</summary>
        public static (string title, string text)[] FeedAboutOf(PanelInput input)
        {
            if (input == null || input.IsSelf) return FeedAbout;
            var n = Name(input);
            var sessions = FriendHasBlocks(input) ? " Earlier sessions, up to " + KeptFights.MaxAge.Days + " days back, come from the server one at a time as you show older foes. The filter counts the foes on this PC." : "";
            return new[]
            {
                ("The feed", n + "'s fights, newest first, as " + n + "'s copy last shared them: the newest " + FeedShare.MaxFoes + " foes of the session. Session holds the fights " + SinceViewer(input) + ". A player who does not share shows as another player." + sessions),
                ("What became of a foe", "Defeated by " + n + ": the game counted the kill for " + n + " alone. Defeated with: other players hit it too. Survived: it still stood when the fight ended. Got away: the game took it away without a kill."),
                ("Damage", "Dealt is before the foe's armor, as on Damage. Received is after " + n + "'s armor, as on Defense."),
            };
        }

        static void BattleFeedView(PanelInput input, PanelView view, PanelState state)
        {
            view.Heading = FeedHeading + ", this session";
            // 0.8.2 (Joost on a fellow's book: "why no feed? I want that in"): a fellow's feed as their copy shares it; a copy from before 0.8.2 has none
            if (!input.IsSelf && input.SharedFeed == null) { view.Heading = FeedHeading; view.Blocks.Add(Empty(Name(input) + "'s feed is not shared", FeedNotShared(input))); Plate(view, "ui:chapter-battle"); return; }
            var live = BattleFeed(input) ?? new List<FoeFight>(); var earlier = KeptFightsOf(input);   // 0.8.5: and your earlier sessions' (KeptFights)
            var blocks = FriendHasBlocks(input);   // 0.8.6: a friend's earlier sessions on the server
            if (live.Count == 0 && earlier.Count == 0 && !blocks) { view.Blocks.Add(Empty(NoFightYet, input.IsSelf ? NoFightText : Name(input) + "'s fights show up here as " + Name(input) + " plays, newest first.")); Plate(view, "ui:chapter-battle"); return; }
            // 0.8.1: the window chips (the heading follows: "Battle feed, last 30 minutes") and the Biome and Foe rows, Damage's pattern
            view.Heading = FeedHeading;
            var w = FeedWindowChips(input, state, view, live.Count, earlier);
            view.Scope = WindowLabel(w) + " · " + (input.IsSelf ? "your fights" : Name(input) + "'s fights");
            // Session is this session's fights (0.8.2: yours with no cut); the other windows reach back into the earlier sessions' (0.8.5). B33: a
            // fellow's Session counts from when you came in (SessionFrom), as their Deaths and Received from do; the short windows are their own times
            var from = FeedFrom(input, w);
            var fights = w == TimeWindow.Session ? live : live.Concat(earlier).ToList();
            List<BlockRow> missing = null;
            if (blocks && (IsDayWindow(w) || w == TimeWindow.SinceInstall)) fights = live.Concat(FriendFights(input, from, out missing)).ToList();   // their sessions that are here, newest first; the first one that is not stops the list
            var inWindow = fights.SelectMany(f => f.Entries).Where(e => !from.HasValue || e.EndUtc >= from.Value).ToList();
            var (bar, narrowed, pass) = FeedFilterBar(input, state, inWindow);
            var feedPick = new FeedPick { From = from, Pass = narrowed ? pass : null, Filtered = narrowed };
            var choice = narrowed ? string.Join(",", Chosen(state, FeedFilter, "biome").ToArray()) + "|" + string.Join(",", Chosen(state, FeedFilter, "foe").ToArray()) : "";
            feedPick.Key = w + "|" + inWindow.Count + "|" + choice;
            // 0.8.5: Show older foes holds while the window and the filter stay (a new page or book resets it: PanelModel.Visited)
            var moreAt = state.Player + "|" + state.Everyone + "|" + w + "|" + choice;
            if (state.FeedMoreAt != null && state.FeedMoreAt != moreAt) state.FeedMore = 0;
            state.FeedMoreAt = moreAt;
            feedPick.More = state.FeedMore;
            if (missing != null && missing.Count > 0)
            {
                string st = null; input.BlockStates?.TryGetValue(missing[0].Id, out st);
                var of = input.BlockRows.Count(r => !from.HasValue || r.ToUtc >= from.Value);   // her sessions on the server in the window: "1 of 2" while one comes
                if (input.BlockWaited == null || !input.BlockWaited.TryGetValue(missing[0].Id, out var waited)) waited = -1;
                feedPick.Server = new FeedServer { Missing = missing, Next = missing[0], State = st, UpTo = narrowed || (from.HasValue && missing[0].FromUtc < from.Value), Of = of, Index = of - missing.Count + 1, Waited = waited };
            }
            if (w == TimeWindow.Session && blocks) feedPick.After = "Choose " + WindowShort(TimeWindow.ThirtyDays) + " or " + WindowShort(TimeWindow.SinceInstall) + " for " + Name(input) + "'s earlier sessions.";
            if (w == TimeWindow.Session && earlier.Count > 0) feedPick.After = "Session shows this session's fights only. Choose " + WindowShort(FeedWindowHolding(input, earlier[0].EndUtc, TimeWindow.Today)) + " for your earlier ones.";
            var shown = fights.Select(f => f.Entries.Count(feedPick.Shows)).Where(c => c > 0).ToList();
            AboutLines(view, FeedAboutOf(input));
            AboutLines(view, (FeedFilterAboutTitle, FeedFilterAbout));
            if (bar != null && (shown.Count > 0 || narrowed)) Add(view, bar);   // the bar stays on an empty choice: it is how you unchoose
            if (shown.Count == 0 && feedPick.Server != null)
            {
                // 0.8.6 (live, the first real evening: a friend's 7 days read "No fight ... Choose a longer window, or Session" and never asked): nothing of theirs on this PC
                // in the window, but the server holds sessions of theirs in it. The newest is asked for now (FeedFetch), so the page says it is coming, or Try again
                var server = feedPick.Server;
                view.Blocks.Add(narrowed ? Empty(NothingForChoice, "None of the foes on this PC match. Not shown: " + ServerCount(server) + ".")
                                         : Empty(Name(input) + "'s earlier sessions are on the server", "Not shown: " + ServerCount(server) + "."));
                var line = ServerLine(input, new FeedServer { Missing = server.Missing, Next = server.Next, State = server.State ?? "loading", UpTo = server.UpTo, Index = server.Index, Of = server.Of, Waited = server.Waited });
                view.Blocks.Add(new Block { Kind = FeedLineKind, Tone = line.Tone, Title = line.Title, Text = line.Text, NoteLink = line.NoteLink, Value = line.Value });
                Plate(view, "ui:chapter-battle");
                FeedFetch(state, view, feedPick);
                return;
            }
            if (shown.Count == 0)
            {
                view.Blocks.Add(narrowed ? Empty(NothingForChoice, FeedClearLine)
                    : w == TimeWindow.Session && from.HasValue ? Empty("No fight " + SinceViewer(input), FeedBeforeYouLine(input))   // a fellow's Session: none since you came in
                    : w == TimeWindow.Session && earlier.Count > 0 ? Empty(NoFightYet, FeedEarlierLine(input, earlier[0].EndUtc))   // 0.8.5: yours, right after a restart
                    : blocks && live.Count == 0 && w != TimeWindow.Session && w != TimeWindow.SinceInstall   // 0.8.6: a friend's sessions on the server all lie before this window (their Session is empty too)
                        ? Empty(NoFightTitle(w), "Choose " + WindowShort(FeedWindowHolding(input, input.BlockRows[0].ToUtc, AllWindows.SkipWhile(x => x != w).ElementAt(1))) + " for " + Name(input) + "'s earlier sessions.")
                    : Empty(NoFightTitle(w), input.IsSelf ? FeedLongerKept : FeedLongerLine));
                Plate(view, "ui:chapter-battle");
                return;
            }
            // only the chosen view is built; the others need a chip, not their content (Switch attaches the chosen one's blocks only)
            var pick = state.View.TryGetValue(FeedViewKey, out var p) && FeedViews.Any(v => v.id == p) ? p : FeedLog;
            var chip = new List<Block> { new Block { Kind = "note" } };
            Switch(view, state, FeedPage, "view", FightsCaption(shown.Count, shown.Sum()), FeedViews.Select(v => (v.id, v.label, v.id == pick ? new List<Block>(FeedOf(input, fights, v.id, feedPick)) : chip)).ToArray());
            Plate(view, "ui:chapter-battle");
            if (feedPick.Server != null) FeedFetch(state, view, feedPick);
        }

        /// <summary>0.8.6: a friend's sessions that are here, newest first, as feed fights (each tied to its session, so its first fight carries the session line), for
        /// the window starting at <paramref name="from"/> (null: all); stops at the first session that is not here yet: <paramref name="missing"/> is it and every
        /// one after it in the window (a page never skips a session).</summary>
        static List<FoeFight> FriendFights(PanelInput input, DateTime? from, out List<BlockRow> missing)
        {
            missing = new List<BlockRow>(); var fights = new List<FoeFight>();
            var rows = input.BlockRows.Where(r => !from.HasValue || r.ToUtc >= from.Value).ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                if (input.BlockSessions != null && input.BlockSessions.TryGetValue(rows[i].Id, out var s)) fights.AddRange(s.Fights);
                else { missing = rows.Skip(i).ToList(); break; }
            }
            return fights;
        }

        /// <summary>0.8.6, after the feed is built: presses never pile up (once every foe held here is drawn, one press may wait for the next session; more do nothing),
        /// and the session to fetch (PanelView.FetchBlock): the next one when it is not asked yet and either the next page would reach it (fewer than FeedTop held foes
        /// are left, so it is fetched a page ahead and the press is usually instant) or a press is waiting on it.</summary>
        static void FeedFetch(PanelState state, PanelView view, FeedPick pick)
        {
            if (pick.Drawn >= pick.Total && state.FeedMore > pick.Used + 1) state.FeedMore = pick.Used + 1;
            var waiting = state.FeedMore > pick.Used;
            if (pick.Server.State == null && (pick.Total - pick.Drawn < FeedTop || waiting)) view.FetchBlock = pick.Server.Next.Id;
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
        /// starts with the new connection). 0.8.2, a fellow's fight on their book: your own part from your own log the same way (it is live, so
        /// always known).
        /// </summary>
        public static FightPart PartOf(PanelInput fellow, FoeFight f)
        {
            var part = new FightPart { Name = fellow?.PlayerName };
            if (fellow?.Log == null) return part;
            if (!fellow.IsSelf && (!fellow.LastRecordedUtc.HasValue || fellow.LastRecordedUtc.Value.AddMinutes(fellow.Log.Span) <= f.StartUtc)) return part;
            if (!fellow.IsSelf && fellow.FirstRecordedUtc.HasValue && fellow.FirstRecordedUtc.Value > f.EndUtc) return part;
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

        /// <summary>The parts of a fight: the book's owner first (you; on a fellow's book, 0.8.2, them), then the players in it (FoeFight.With) who share,
        /// in join order of the group (the book's Fellows list; on a fellow's book that holds you).</summary>
        public static List<FightPart> FightParts(PanelInput input, FoeFight f)
        {
            var you = new FightPart { Name = input.PlayerName, You = true, Has = true };
            for (int t = 0; t < BattleRecorder.Types.Length; t++) you.Dealt[t] = f.Dealt[t];
            you.Received = f.ReceivedTotal;
            var parts = new List<FightPart> { you };
            var with = f.With();
            var sharers = (input.Fellows ?? new List<PanelInput>()).Where(x => x != null && x != input && !SameName(x.PlayerName, input.PlayerName) && f.Kept == null).ToList();   // 0.8.5: a kept fight's minutes are in no copy now
            foreach (var fellow in sharers.Where(x => with.Any(n => SameName(n, x.PlayerName))))
            {
                var p = PartOf(fellow, f);
                p.NearSeconds = f.Near == null ? 0 : f.Near.Where(kv => SameName(kv.Key, fellow.PlayerName)).Sum(kv => kv.Value);
                parts.Add(p);
            }
            return parts;
        }

        public const string NearYouTitle = "Near you", YourPartTitle = "Your part", FellowsPartTitle = "Fellow players' parts";
        public const string NearYouAbout = "Players within 40 m of you for 10 seconds or more during the fight, or who hit a foe of it with you. Counted on this PC; fellow players who share see it, and anyone who does not share is another player there.";
        public const string YourPartAbout = "Every hit you dealt and received in this fight, as the feed shows it. Damage dealt is before the foe's armor, damage received after your armor.";
        /// <summary>0.8.2: the About lines of Last fight for whose book it is (a fellow's: as their copy shares it).</summary>
        static (string title, string text) NearAbout(PanelInput i) => i.IsSelf ? (NearYouTitle, NearYouAbout)
            : ("Near " + Name(i), "Players within 40 m of " + Name(i) + " for 10 seconds or more during the fight, or who hit a foe of it with " + Name(i) + ". Counted on " + Name(i) + "'s PC; a player who does not share shows as another player.");
        static (string title, string text) PartAbout(PanelInput i) => i.IsSelf ? (YourPartTitle, YourPartAbout)
            : (Name(i) + "'s part", "Every hit " + Name(i) + " dealt and received in this fight, as " + Name(i) + "'s feed shows it. Damage dealt is before the foe's armor, damage received after " + Name(i) + "'s armor.");
        public const string FellowsPartAbout = "Fellow players' parts come from the minutes their shared copies hold, so up to a minute at each end of the fight can fall in or out. A fellow player whose copy holds no minute of this fight shows without numbers, and is left out of the sums.";
        public const string FellowsNoneAbout = "Only who was near you: no fellow player's shared copy holds minutes of this fight, so the numbers are yours alone.";
        public const string NoPartWords = "no shared minutes of this fight yet";

        static void BattleLastFight(PanelInput input, PanelView view, PanelState state)
        {
            view.Heading = LastFightLabel;
            // 0.8.2 ("same for last fight"): a fellow's last fight as their copy shares it; a copy from before 0.8.2 has none
            if (!input.IsSelf && input.SharedFeed == null) { view.Blocks.Add(Empty(Name(input) + "'s last fight is not shared", FeedNotShared(input))); Plate(view, "ui:chapter-battle"); return; }
            var f = BattleFeed(input, 1)?.FirstOrDefault() ?? KeptFightsOf(input).FirstOrDefault();   // 0.8.5: none this session yet, your last one kept from before
            if (f == null) { view.Blocks.Add(Empty(NoFightYet, input.IsSelf ? LastFightEmptyText : "After a fight, this page shows it: " + Name(input) + "'s part and the part of the fellow players near " + Name(input) + ".")); if (ServerLine(input) is Block server) view.Blocks.Add(server); Plate(view, "ui:chapter-battle"); return; }   // 0.8.6: none of yours, but the server has a fight
            if (f.Kept != null) { view.Scope = "An earlier session · your last fight"; AboutLines(view, (KeptAboutTitle, KeptAbout)); }
            var parts = FightParts(input, f);
            var counted = parts.Where(p => p.Has).ToList(); var fellows = parts.Where(p => !p.You).ToList(); var together = counted.Count > 1;
            var dealt = counted.Sum(p => p.DealtTotal);
            // the foes you fought in it, per kind (a foe that only hit you is in the received line, not fought)
            var fought = f.Entries.Where(e => e.DealtTotal > 0 || e.Defeated).ToList();
            var defeated = fought.Count(e => e.Defeated);
            view.Blocks.Add(new Block { Kind = "note", Text = DealtLabel + ", " + DealtQualifier });   // what the numbers are, over them (the prototype's caption)
            view.Blocks.Add(Hero((NAtLeast(dealt), together ? "damage dealt together" : DealtLabel, null, null), (fought.Count > 0 ? N(defeated) : null, defeated == 1 ? "foe defeated" : "foes defeated", null, "of " + N(fought.Count))));
            AboutLines(view, NearAbout(input), PartAbout(input), (FellowsPartTitle, fellows.Count == 0 ? null : together ? FellowsPartAbout : input.IsSelf ? FellowsNoneAbout : "Only who was near " + Name(input) + ": no other player's minutes of this fight are here, so the numbers are " + Name(input) + "'s alone."));
            if (fellows.Count > 0)
            {
                var shares = WholeShares(counted.Select(p => p.DealtTotal).ToList());
                var rows = new List<Block>();
                foreach (var p in parts)
                {
                    var k = counted.IndexOf(p);
                    var note = p.You ? (input.IsSelf ? "this PC" : Name(input) + "'s PC") : p.NearSeconds >= BattleRecorder.NearSeconds ? "near " + Subject(input) + " " + (p.NearSeconds < 60 ? (int)p.NearSeconds + " s" : FightLength(TimeSpan.FromSeconds(p.NearSeconds))) : "hit the same foes";
                    var title = p.You ? (input.IsSelf ? "You" : Name(input)) : Person(input, p.Name);   // on a fellow's book the viewer's row is "Rowan (you)"
                    rows.Add(p.Has
                        ? new Block { Kind = GroupMemberKind, Id = p.Name, Icon = "person:" + p.Name, Title = title, Note = note, Value = NAtLeast(p.DealtTotal), Value2 = shares[k] == 0 && p.DealtTotal > 0 ? UnderOnePercent : shares[k] + " %", Fraction = dealt > 0 ? (float)(p.DealtTotal / dealt) : 0, Colour = "person:" + p.Name,
                                      Items = DamageMix(Enumerable.Range(0, BattleRecorder.Types.Length).Select(t => new KeyValuePair<string, double>(BattleRecorder.Types[t], p.Dealt[t]))) }   // 0.8: the line under their bar
                        : new Block { Kind = GroupQuietKind, Id = p.Name, Icon = "person:" + p.Name, Title = title, Note = note, Text = NoPartWords });
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
                var kinds = fought.GroupBy(e => FoeGroup(input, e.Kind)).Select(g =>   // 0.8.5: a row per foe by the game's name
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
                view.Blocks.Add(new Block { Kind = "fightfoes", Title = "Foes " + Subject(input) + " fought", Value = N(fought.Count), Text = together ? (input.IsSelf ? YourDamageToEach : Name(input) + "'s damage to each") : null, Items = items });
                if (most > 0) AboutLines(view, (DamageBarsAboutTitle, DamageBarsAbout));
            }
            // what the group received, after each one's own armour
            var received = counted.Where(p => p.Received > 0).ToList();
            if (received.Count > 0)
                view.Blocks.Add(new Block
                {
                    Kind = "fightreceived", Value = NAtLeast(received.Sum(p => p.Received)), Title = together ? "damage received together" : "damage received", Text = together ? "after each player's armor" : AfterOf(input),
                    Items = together ? received.Select(p => new Block { Kind = "player", Id = p.Name, Title = p.You ? Subject(input) : Person(input, p.Name), Value = NAtLeast(p.Received), Colour = "person:" + p.Name }).ToList() : new List<Block>(),
                });
            Plate(view, "ui:chapter-battle", NearLine(input, f, parts));
            var plate = view.Blocks[0];
            plate.Pill = f.Open ? Clock(input, f.StartUtc, false) + " · still on" : FightWhen(input, f.StartUtc, f.EndUtc, FightLength(f.Length));
            plate.Items.Insert(0, new Block { Kind = "headlink", Id = "Battle/" + FeedPage, Title = EarlierFights, Icon = FeedIcon });
        }

        /// <summary>
        /// The plate's first line: who was in the fight with you ("Near you in this fight: Edda and Tor."), players near you who do not share,
        /// and the fellow players who share but were not in it ("Finch was elsewhere, so not in it."). 0.8.2, a fellow's book: who was with them
        /// ("Near Edda in this fight: Rowan (you) and another player."), the players their copy does not name as another player.
        /// </summary>
        public static string NearLine(PanelInput input, FoeFight f, List<FightPart> parts)
        {
            var with = f.With();
            var sharing = (input.Fellows ?? new List<PanelInput>()).Where(x => x != null && x != input && !SameName(x.PlayerName, input.PlayerName)).Select(x => x.PlayerName).ToList();
            var inIt = with.Where(n => sharing.Any(s => SameName(s, n))).ToList();
            var notSharing = with.Where(n => !sharing.Any(s => SameName(s, n)) && !SameName(n, input.PlayerName)).ToList();
            var elsewhere = f.Kept != null ? new List<string>() : sharing.Where(s => !with.Any(n => SameName(n, s))).ToList();   // 0.8.5: who shares now says nothing about an earlier session
            var near = WithWords(input, inIt.Concat(notSharing), f.Outside);
            var line = near.Length > 0 ? "Near " + Subject(input) + " in this fight: " + near + "." : "Nobody was near " + Subject(input) + " in this fight.";
            var apart = notSharing.Count + f.Outside;
            if (apart > 0) line += " " + Cap(WithWords(input, notSharing, f.Outside)) + (apart == 1 ? " does not share, so is not in the numbers." : " do not share, so are not in the numbers.");
            var away = WithWords(input, elsewhere, 0);
            if (elsewhere.Count > 0 && inIt.Count > 0) line += " " + Cap(away) + (elsewhere.Count == 1 ? " was" : " were") + " elsewhere, so not in it.";
            return line;
        }
    }
}
