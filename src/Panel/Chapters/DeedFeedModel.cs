using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 deed feeds (work/hearthwoven-0.9/DESIGN.md section 2 "Feeds", TRACK-feeds.md): a Feed page for every Deeds page with a growth line
    /// (Crafting, Cooking, Building, Groundwork, Farming, Fishing, Woodcutting, Mining) and one mixed feed opened from Deeds > Recent, every deed in time
    /// order. Built from the deed stretches (TRACK-record.md: DeedSession / DeedStretch / DeedMoment), drawn as the Battle feed is: the same "feed" block
    /// per island and "feedline" lines between (BattleFeedModel.FeedBlocks), its Log, Cards and Timeline, its filter bar, quiet gaps, session lines and
    /// Show older. Pure C#.
    ///
    /// An island is a stretch, the minutes a deed grew until 10 quiet minutes passed: Kind "fight" (the feed's island kind) with Title "Crafting at 21:05",
    /// Text its length and what it did by kind ("18 min · 4 made · 2 upgrades"); its lines only for moments (Kind "entry": gear made, an upgrade, a feast
    /// set out, a first), Tone "made" (a filled mark, in Battle's outcome-mark place) or "upgraded" (a ring); an upgrade carries its level for the four-part
    /// level meter (Level the level reached, Count the item's highest level, 0 when not known), a made line its count (Value "×2"). The rest of the stretch
    /// as counts by kind: Kind "counts" (Title the kind's words, Items Kind "item": Icon, Title, Value). Older minutes are kept in tens (EventLog's fold),
    /// so their times are said to the nearest 10 minutes and the island says so.
    ///
    /// Your feed: this session's stretches (PanelInput.DeedStretches, the Live one) and the earlier sessions kept on this PC. A friend's: their closed
    /// sessions from the server, in the same blocks as their earlier fights (FeedBlocks.cs; PanelInput.DeedBlockRows and DeedBlockSessions), one at a time.
    /// </summary>
    public static partial class PanelModel
    {
        public const string DeedFeedPrefix = "feed-", DeedFeedAllPage = "feed-all", DeedFeedChip = "Feed", NeedsDeedFeeds = "0.9.0";
        /// <summary>The stretches one page of a deed feed draws; each Show older adds as many again.</summary>
        public const int DeedFeedTop = 12;

        /// <summary>The deed families with a feed: the Deeds page, its name, the words of its quiet time ("without crafting") and its plate's picture.</summary>
        public static readonly (string id, string label, string doing, string icon)[] DeedFeedFamilies =
        {
            (DeedStretches.Crafting, "Crafting", "crafting", "title:smith"), (DeedStretches.Cooking, "Cooking", "cooking", "title:cook"),
            (DeedStretches.Building, "Building", "building", "title:builder"), (DeedStretches.Groundwork, "Groundwork", "groundwork", "vocab:ground-lower"),
            (DeedStretches.Farming, "Farming", "farming", "title:farmer"), (DeedStretches.Fishing, "Fishing", "fishing", "title:fisher"),
            (DeedStretches.Woodcutting, "Woodcutting", "woodcutting", "title:woodcutter"), (DeedStretches.Mining, "Mining", "mining", "title:miner"),
        };

        /// <summary>
        /// 0.9 all-feed (Joost: "I think we should have a deed of every deed that's being recorded. Cause then you're like, okay, so that's being
        /// recorded, that's good to know."): the families the mixed feed shows beside the eight, none with a page feed of its own: creatures tamed,
        /// skills practiced, what you brought in that is neither wood nor stone and ore, sailing (km sailed and cargo carried), and your fights, one
        /// line each that opens the Battle feed (DeedFeedFights; never the Battle feed's own lines).
        /// </summary>
        public static readonly (string id, string label, string doing, string icon)[] DeedFeedMoreFamilies =
        {
            (DeedStretches.Taming, "Taming", "taming", "title:tamer"), (DeedStretches.Practice, "Skills", "skill practice", "ui:chapter-skills"),
            (DeedStretches.BroughtIn, "Brought in", "bringing in", "title:explorer"), (DeedStretches.Sailing, "Sailing", "sailing", "title:sailor"),
            (DeedFeedFights, "Fights", "fighting", "ui:chapter-battle"),
        };
        /// <summary>The mixed feed's family of a fight line (no stretch: one line per fight of the Battle record).</summary>
        public const string DeedFeedFights = "fights";
        /// <summary>A fight line's link words (they open the Battle feed, BattleFeedTarget).</summary>
        public const string DeedFightLink = "In the Battle feed";
        public static readonly string BattleFeedTarget = Chapter.Battle + "/" + FeedPage;

        /// <summary>
        /// Every family DeedLog records, by the words the mixed feed says it in: (the stretch family it shows in, its words). A deed log family
        /// (DeedLog.AllFamilies) by its name; the game's counters (DeedLog.CounterStats) as their token. Hits stay inside their deed's stretch
        /// (axe hits in Woodcutting, pickaxe hits in Mining); kills and the kill counters are the fights; the fish counter is Fishing's fish.
        /// DeedFeedTests checks that every family DeedLog records has words here, so a family added to the log without them fails the test.
        /// </summary>
        public static (string family, string words)? DeedFeedKindOf(string deedFamily, string token = null)
        {
            switch (deedFamily == DeedLog.Counters ? DeedLog.Counters + "|" + token : deedFamily)
            {
                case DeedLog.PickedUp: return (DeedStretches.BroughtIn, "Brought in");
                case DeedLog.Made: return (DeedStretches.Crafting, "Made");
                case DeedLog.Planted: return (DeedStretches.Farming, "Planted");
                case DeedLog.Felled: return (DeedStretches.Woodcutting, "Trees felled");
                case DeedLog.ChopHits: return (DeedStretches.Woodcutting, "axe hits");
                case DeedLog.PickaxeHits: return (DeedStretches.Mining, "pickaxe hits");
                case DeedLog.Skills: return (DeedStretches.Practice, "Practiced");
                case DeedLog.Recovered: return (DeedStretches.Building, "Recovered from pieces that came down");
                case DeedLog.Placed: return (DeedStretches.Building, "Built");
                case DeedLog.Picked: return (DeedStretches.Farming, "Picked");
                case DeedLog.Kills: return (DeedFeedFights, "foes");
                case DeedLog.Counters + "|EnemyKills": case DeedLog.Counters + "|BossKills": return (DeedFeedFights, "foes");
                case DeedLog.Counters + "|FishCaught": return (DeedStretches.Fishing, "Fish");
                case DeedLog.Counters + "|CreatureTamed": return (DeedStretches.Taming, "tamed");
                case DeedLog.Counters + "|DistanceSail": return (DeedStretches.Sailing, "sailed");
                case DeedLog.Counters + "|" + DeedLog.CargoTotal: return (DeedStretches.Sailing, "carried");
                default: return null;
            }
        }

        /// <summary>The deeds no minute holds, said once under the mixed feed (the house sentence: "Not recorded", never 0).</summary>
        public static readonly string DeedFeedNotByMinute = NotRecorded + " by the minute: meals eaten, creatures petted, and whether you sailed at the helm or as a passenger.";

        public static string DeedFeedPageOf(string family) => family == null ? DeedFeedAllPage : DeedFeedPrefix + family;
        public static bool IsDeedFeedPage(string page) => page != null && (page == DeedFeedAllPage || (page.StartsWith(DeedFeedPrefix, StringComparison.Ordinal) && DeedFeedFamilies.Any(f => DeedFeedPrefix + f.id == page)));
        /// <summary>The family a feed page shows; null for the mixed feed (or another page).</summary>
        public static string DeedFeedFamilyOf(string page) => IsDeedFeedPage(page) && page != DeedFeedAllPage ? page.Substring(DeedFeedPrefix.Length) : null;
        /// <summary>A deed feed's filter bar (its Id is the filter key "Deeds/feed-..."): its own island, as the Battle feed's (Islands.cs).</summary>
        public static bool IsDeedFeedFilter(string id) => id != null && id.StartsWith("Deeds/" + DeedFeedPrefix, StringComparison.Ordinal);
        /// <summary>A page that is a feed (Battle's or a deed's): its Show older line, keys and drawing are the feed's.</summary>
        public static bool IsFeedView(PanelView v) => v != null && !v.ShowAbout && ((v.Active == Chapter.Battle && v.Page == FeedPage) || (v.Active == Chapter.Deeds && IsDeedFeedPage(v.Page)));

        static (string id, string label, string doing, string icon) FamilyRow(string family) => DeedFeedFamilies.Concat(DeedFeedMoreFamilies).FirstOrDefault(f => f.id == family);

        /// <summary>The words of one deed kind in a stretch of a family: the kind's head words (one, many; null: not in the head) and its counts line's title
        /// (null: no counts line: its moments speak, or hits per tree are no reader's question).</summary>
        static (string one, string many, string strip) KindWords(string family, string kind)
        {
            switch (family + "|" + kind)
            {
                case "crafting|made": return ("made", "made", "Made");
                case "cooking|made": return ("dish cooked", "dishes cooked", "Cooked");
                case "cooking|placed": return ("feast set out", "feasts set out", null);
                case "building|placed": return ("piece built", "pieces built", "Built");
                case "building|recovered": return (null, null, "Recovered from pieces that came down");
                case "groundwork|placed": return ("groundwork stroke", "groundwork strokes", "Groundwork");
                case "farming|planted": return ("planted", "planted", "Planted");
                case "farming|picked": return ("picked", "picked", "Picked");
                case "fishing|picked": return ("fish", "fish", "Fish");
                case "woodcutting|treesFelled": return ("tree felled", "trees felled", "Trees felled");
                case "woodcutting|pickedUp": return ("wood brought in", "wood brought in", "Brought in");
                case "woodcutting|chopHits": return ("axe hit", "axe hits", null);
                case "mining|pickedUp": return ("stone and ore brought in", "stone and ore brought in", "Brought in");
                case "mining|pickaxeHits": return ("pickaxe hit", "pickaxe hits", null);
                case "practice|skillPractice": return (null, null, "Practiced");   // the head counts the skills (practice has no unit a player reads)
                case "broughtin|pickedUp": return ("item", "items", "Brought in");
                default: return (null, null, null);
            }
        }

        /// <summary>The order a family's deed kinds are said in, its own deed first: trees felled before the wood they gave, pieces built before what was recovered.</summary>
        static int KindOrder(string family, string kind)
        {
            string[] order;
            switch (family)
            {
                case DeedStretches.Cooking: order = new[] { DeedLog.Made, DeedLog.Placed }; break;
                case DeedStretches.Building: order = new[] { DeedLog.Placed, DeedLog.Recovered }; break;
                case DeedStretches.Farming: order = new[] { DeedLog.Planted, DeedLog.Picked }; break;
                case DeedStretches.Woodcutting: order = new[] { DeedLog.Felled, DeedLog.PickedUp, DeedLog.ChopHits }; break;
                case DeedStretches.Mining: order = new[] { DeedLog.PickedUp, DeedLog.PickaxeHits }; break;
                case DeedStretches.BroughtIn: order = new[] { DeedLog.PickedUp }; break;
                case DeedStretches.Practice: order = new[] { DeedLog.Skills }; break;
                case DeedStretches.Taming: case DeedStretches.Sailing: order = new[] { DeedLog.Counters }; break;
                default: order = new[] { DeedLog.Made, DeedLog.Placed }; break;
            }
            var i = Array.IndexOf(order, kind);
            return i >= 0 ? i : order.Length + Array.IndexOf(DeedLog.AllFamilies, kind);
        }

        // ---------- the Feed chip on every Deeds page with a growth line (DESIGN.md section 4: the heading-row plan, owned by this track) ----------

        /// <summary>The heading row's Feed chip (a headlink, as Battle's), with the feed's own picture: it opens the page's feed (Recent: the mixed feed).</summary>
        public static Block DeedFeedLink(string family) => new Block { Kind = "headlink", Id = "Deeds/" + DeedFeedPageOf(family), Title = DeedFeedChip, Icon = FeedIcon };

        /// <summary>A headlink that rides in the strip under the heading (the feat and About strip), not in the heading row: every Deeds page's.</summary>
        public const string StripLinkTone = "strip";

        /// <summary>The headlinks a plate's strip carries (Tone StripLinkTone), in their order.</summary>
        public static List<Block> StripLinks(Block plate) => (plate?.Items ?? new List<Block>()).Where(b => b?.Kind == "headlink" && b.Tone == StripLinkTone).ToList();

        /// <summary>
        /// Build's step: the Feed chip first among the page's links on every Deeds page with a growth line and on Recent (your book and a friend's; never the
        /// group's view or About). The heading-row plan (DESIGN.md section 4, owned by this track): with the Feed chip beside nine window chips and Compare,
        /// render-check cut the heading on Crafting, Woodcutting, Groundwork and Building in a day window, so every Deeds page carries all its links
        /// (Feed, and Crafting's Sets) in the strip under the heading, at its right end before the views and About these numbers, the same way on every page.
        /// </summary>
        static void DeedFeedLinks(PanelInput input, PanelView view)
        {
            if (view == null || view.ShowAbout || view.Active != Chapter.Deeds || input == null) return;
            var plate = PlateOf(view);
            if (plate == null) return;
            var family = view.Page == RecentPageId ? null : GrowthPages.Contains(view.Page) ? view.Page : "";
            if (family != "" && !view.EveryoneOn && !(plate.Items ?? new List<Block>()).Any(b => b.Kind == "headlink" && b.Id == "Deeds/" + DeedFeedPageOf(family)))
                (plate.Items ?? (plate.Items = new List<Block>())).Insert(0, DeedFeedLink(family));
            foreach (var b in (plate.Items ?? new List<Block>()).Where(b => b?.Kind == "headlink")) b.Tone = StripLinkTone;
        }

        // ---------- the page ----------

        public const string DeedFeedAboutTitle = "The feed", DeedFeedTimesTitle = "Times", DeedFeedFilterTitle = "Window and filter";
        public const string DeedFeedTimes = "Minutes older than about three hours are kept in tens of minutes, so those times are said to the nearest 10 minutes; such an island says so.";
        public static readonly string DeedFeedFilterAbout = "The window and the filter show only the stretches and kinds that pass them. The newest " + DeedFeedTop + " stretches are drawn; Show older draws the next " + DeedFeedTop + ", and the line beside it says how many are shown and how many are left.";
        public const string DeedFeedNotRecorded = "This book has not read your records yet: the feed shows once you are in a world.";
        public const string DeedFeedRunningWhy = "The running session comes here once it ends, from the server.";

        /// <summary>The feed's About lines for whose book it is.</summary>
        static (string title, string text)[] DeedFeedAboutOf(PanelInput input, string family)
        {
            var doing = family == null ? "deeds" : FamilyRow(family).doing;
            var moments = family == null || family == DeedStretches.Crafting || family == DeedStretches.Cooking
                ? " A line for each moment: gear made (a filled mark), an upgrade with its level (a ring and the level meter), a feast set out, a first you made or placed; the rest is counted by kind under it." : " A line for each first you placed or made; the rest is counted by kind under it.";
            if (family == null) moments += " Every deed recorded by the minute has its stretches here: crafting, cooking, building, groundwork, farming, fishing, woodcutting, mining, taming, skill practice, what you brought in and sailing; each fight is one line that opens the Battle feed.";
            if (input == null || input.IsSelf)
                return new[]
                {
                    (DeedFeedAboutTitle, "Your " + doing + ", newest first. Each island is a stretch: the minutes it grew, until 10 quiet minutes passed." + moments +
                                         " This session's stretches, then your earlier sessions in this world, kept on this PC up to " + KeptDeeds.MaxAge.Days + " days back. Fellow players who share see your sessions once they end."),
                    (DeedFeedTimesTitle, DeedFeedTimes),
                };
            var n = Name(input);
            return new[]
            {
                (DeedFeedAboutTitle, n + "'s " + doing + ", newest first, from " + n + "'s sessions on the server: each session comes once it ends, up to " + KeptDeeds.MaxAge.Days + " days back, one at a time as you show older. Each island is a stretch: the minutes it grew, until 10 quiet minutes passed." + moments),
                (DeedFeedTimesTitle, DeedFeedTimes),
            };
        }

        /// <summary>One stretch on its way to the page: the stretch, its session (null: this session's, live), its place in its session's list.</summary>
        sealed class FeedStretch { public DeedStretch S; public DeedSession Session; public bool Live; public FoeFight Fight; }

        /// <summary>
        /// 0.9 all-feed: the fights the mixed feed lines up with the stretches, as stretches of the family DeedFeedFights (Fight set, no counts): yours
        /// this session (the Battle record) and of the earlier sessions kept on this PC; a friend's of their sessions that are here (the same blocks as
        /// their deed stretches). Each under the deed session of the same id, so a session line holds both.
        /// </summary>
        static List<FeedStretch> FightStretches(IEnumerable<FoeFight> fights, DeedSession session, bool live) =>
            (fights ?? Enumerable.Empty<FoeFight>()).Where(f => f != null && f.EndUtc >= f.StartUtc)
                .Select(f => new FeedStretch { S = new DeedStretch { Family = DeedFeedFights, FromUtc = f.StartUtc, ToUtc = f.EndUtc }, Session = session, Live = live, Fight = f }).ToList();

        static List<FeedStretch> YourFights(PanelInput input, List<DeedSession> sessions)
        {
            var list = new List<FeedStretch>();
            var live = sessions.FirstOrDefault(s => s.Live) ?? new DeedSession { Id = "", FromUtc = input.SessionStartUtc ?? input.NowUtc, ToUtc = input.NowUtc, Live = true };
            list.AddRange(FightStretches(BattleFeed(input), live, true));
            foreach (var k in input.KeptSessions ?? new List<KeptSession>())
            {
                if (k == null) continue;
                var s = sessions.FirstOrDefault(x => !x.Live && k.Id.Length > 0 && x.Id == k.Id) ?? new DeedSession { World = k.World, Id = k.Id, FromUtc = k.FromUtc, ToUtc = k.ToUtc };
                list.AddRange(FightStretches(k.Fights, s, false));
            }
            return list;
        }

        static void DeedFeedPage(PanelInput input, PanelView view, PanelState state, string page)
        {
            var family = DeedFeedFamilyOf(page);
            var row = FamilyRow(family);
            var label = family == null ? RecentTitle : row.label;
            var icon = family == null ? RecentIcon : row.icon;
            view.Heading = (family == null ? "Deeds" : label) + " feed";
            view.Scope = input.IsSelf ? null : Name(input) + "'s sessions from the server";
            AboutLines(view, DeedFeedAboutOf(input, family));
            AboutLines(view, (DeedFeedFilterTitle, DeedFeedFilterAbout));
            if (input.IsSelf) DeedFeedYours(input, view, state, page, family, icon);
            else DeedFeedFriend(input, view, state, page, family, icon);
        }

        /// <summary>The date your deed feeds count from (the 0.9 records' start on this PC), or null.</summary>
        static DateTime? DeedFeedFrom(PanelInput input) => input.FirstsFrom ?? input.UpgradesFrom;
        static string DeedFeedRecorded(PanelInput input) => DeedFeedFrom(input) is DateTime f ? "Recorded from " + RecordDate(input, f) + " · this PC" : null;

        static bool OfFamily(DeedStretch s, string family) => s != null && (family == null ? FamilyRow(s.Family).id != null : s.Family == family && DeedFeedFamilies.Any(f => f.id == family));

        static void DeedFeedYours(PanelInput input, PanelView view, PanelState state, string page, string family, string icon)
        {
            var doing = family == null ? "deeds" : FamilyRow(family).doing;
            if (input.DeedStretches == null) { view.Blocks.Add(Empty(NotRecorded, DeedFeedNotRecorded)); Plate(view, icon); return; }
            var all = new List<FeedStretch>();
            foreach (var s in input.DeedStretches.Where(x => x != null))
                foreach (var st in s.Stretches.Where(x => OfFamily(x, family))) all.Add(new FeedStretch { S = st, Session = s, Live = s.Live });
            if (family == null) all.AddRange(YourFights(input, input.DeedStretches.Where(x => x != null).ToList()));   // 0.9 all-feed: a line per fight
            all = all.OrderByDescending(x => x.S.ToUtc).ThenBy(x => x.S.Family, StringComparer.Ordinal).ToList();
            var recorded = DeedFeedRecorded(input);
            if (all.Count == 0)
            {
                view.Blocks.Add(Empty(family == null ? "No deeds yet" : "No " + doing + " yet", "Your " + doing + " shows up here as you play, newest first: every stretch of minutes you spend on it, closed by 10 quiet minutes." + (recorded != null ? " " + recorded + "." : "")));
                Plate(view, icon);
                return;
            }
            var live = all.Where(x => x.Live).ToList();
            var w = DeedFeedChips(input, state, view, page, true, live.Count > 0 ? (DateTime?)null : all[0].S.ToUtc);
            view.Scope = WindowLabel(w) + " · your " + doing;
            var from = FeedFrom(input, w); var to = WindowEndUtc(input, w);   // 0.9.1: Yesterday ends at this morning's midnight
            var inWindow = w == TimeWindow.Session ? live : all.Where(x => (!from.HasValue || x.S.ToUtc >= from.Value) && (!to.HasValue || x.S.FromUtc < to.Value)).ToList();
            DeedFeedList(input, view, state, page, family, icon, inWindow, w, from, null, recorded,
                         w == TimeWindow.Session && all.Count > live.Count ? "Session shows this session's stretches only. Choose " + WindowShort(FeedWindowHolding(input, all.First(x => !x.Live).S.ToUtc, TimeWindow.Today)) + " for your earlier ones." : null,
                         live.Count == 0 && w == TimeWindow.Session ? "Your last " + (family == null ? "deed" : doing) + " was " + AgoWords(input.NowUtc - all[0].S.ToUtc) + ". Choose " + WindowShort(FeedWindowHolding(input, all[0].S.ToUtc, AllWindows[0])) + " or a longer window to see it." : null);
        }

        static void DeedFeedFriend(PanelInput input, PanelView view, PanelState state, string page, string family, string icon)
        {
            var n = Name(input);
            var doing = family == null ? "deeds" : FamilyRow(family).doing;
            var older = OlderThan(input, NeedsDeedFeeds);
            var rows = input.DeedBlockRows ?? new List<BlockRow>();
            if (rows.Count == 0)
            {
                view.Blocks.Add(older ? Empty(n + "'s " + doing + " feed is not shared", OlderLine(input, n + "'s deed feeds fill"))
                    : !input.DeedsOnServer ? Empty(n + "'s " + doing + " feed is not here", "This server keeps no deed feeds: it needs Hearthwoven 0.9 or later.")
                    : Empty("No " + doing + " from " + n + " yet", n + "'s sessions come here from the server once they end, up to " + KeptDeeds.MaxAge.Days + " days back. " + DeedFeedRunningWhy));
                Plate(view, icon);
                return;
            }
            var w = DeedFeedChips(input, state, view, page, false, null);
            view.Scope = WindowLabel(w) + " · " + n + "'s " + doing;
            var from = FeedFrom(input, w); var to = WindowEndUtc(input, w);
            // their sessions in the window that are here, newest first; the first one that is not stops the list (a page never skips a session)
            var inRows = rows.Where(r => (!from.HasValue || r.ToUtc >= from.Value) && (!to.HasValue || r.FromUtc < to.Value)).ToList();
            var here = new List<FeedStretch>(); var missing = new List<BlockRow>();
            for (int i = 0; i < inRows.Count; i++)
            {
                if (input.DeedBlockSessions != null && input.DeedBlockSessions.TryGetValue(inRows[i].Id, out var s) && s != null)
                {
                    var mine = s.Stretches.Where(x => OfFamily(x, family)).Select(x => new FeedStretch { S = x, Session = s }).ToList();
                    if (family == null && input.BlockSessions != null && input.BlockSessions.TryGetValue(inRows[i].Id, out var k) && k != null) mine.AddRange(FightStretches(k.Fights, s, false));   // 0.9 all-feed: their fights of the same block
                    here.AddRange(mine.Where(x => (!from.HasValue || x.S.ToUtc >= from.Value) && (!to.HasValue || x.S.FromUtc < to.Value)).OrderByDescending(x => x.S.ToUtc));
                }
                else { missing = inRows.Skip(i).ToList(); break; }
            }
            FeedServer server = null;
            if (missing.Count > 0)
            {
                string st = null; input.BlockStates?.TryGetValue(missing[0].Id, out st);
                if (input.BlockWaited == null || !input.BlockWaited.TryGetValue(missing[0].Id, out var waited)) waited = -1;
                server = new FeedServer { Missing = missing, Next = missing[0], State = st, UpTo = true, Of = inRows.Count, Index = inRows.Count - missing.Count + 1, Waited = waited };
            }
            DeedFeedList(input, view, state, page, family, icon, here, w, from, server, null, null, null);
        }

        /// <summary>The window chips of a deed feed (the book's one window set). Yours: every window; Session holds this session's stretches. A friend's: the
        /// days and All (their sessions from the server); Session and the short windows greyed, with why. The window shown: the one chosen this game when the
        /// feed can show it, else Session (yours, while this session has a stretch) or the first window holding your newest stretch (<paramref name="newest"/>), else All.</summary>
        static TimeWindow DeedFeedChips(PanelInput input, PanelState state, PanelView view, string page, bool yours, DateTime? newest)
        {
            bool Open(TimeWindow w) => yours || IsDayWindow(w) || w == TimeWindow.SinceInstall;
            var shown = state.WindowPicked && Open(state.Window) ? state.Window
                      : !yours ? TimeWindow.SinceInstall
                      : newest.HasValue ? FeedWindowHolding(input, newest.Value, AllWindows[0]) : TimeWindow.Session;
            view.HasFilters = true; view.ShownWindow = shown;
            foreach (var w in AllWindows)
                view.Windows.Add(new Choice { Id = w.ToString(), Label = WindowShort(w), Selected = w == shown, Disabled = !Open(w), Why = Open(w) ? null : DeedFeedRunningWhy });
            return shown;
        }

        /// <summary>
        /// The feed of <paramref name="stretches"/> (newest first, the window's), with the filter bar over their kinds (a family's: Kind, and Crafting's Main
        /// material; the mixed feed's: Deed), paged by DeedFeedTop, the islands with their lines and counts, the gaps and session lines between, the count
        /// line, Show older, and a friend's next session on the server (<paramref name="server"/>).
        /// </summary>
        static void DeedFeedList(PanelInput input, PanelView view, PanelState state, string page, string family, string icon, List<FeedStretch> stretches, TimeWindow w,
                                 DateTime? from, FeedServer server, string recorded, string after, string sessionEmpty)
        {
            var doing = family == null ? "deeds" : FamilyRow(family).doing;
            var filter = "Deeds/" + page;
            // the filter bar: one item per moment and per counted kind in the window (weight 1, so a chip counts kinds)
            var items = new List<(FeedStretch st, string family, string token)>();
            foreach (var x in stretches)
            {
                var n = items.Count;
                foreach (var t in x.S.Moments.Select(m => m.Token).Concat(x.S.Counts.Keys.Where(k => KindWords(x.S.Family, DeedStretch.KindOf(k)).strip != null).Select(DeedStretch.TokenOf)).Distinct())
                    items.Add((x, x.S.Family, t));
                if (family == null && items.Count == n) items.Add((x, x.S.Family, WholeDeed));   // 0.9 all-feed: a fight, or a stretch said in one number (tamed, sailed), is one kind
            }
            var defs = new List<FacetDef>();
            if (family == null)
                defs.Add(new FacetDef { Id = "deed", Title = "Deed", Bar = false, Options = DeedFeedFamilies.Concat(DeedFeedMoreFamilies).Where(f => stretches.Any(s => s.S.Family == f.id) || Chosen(state, filter, "deed").Contains(f.id)).Select(f => new FacetOption { Id = f.id, Label = f.label }).ToList() });
            else
            {
                var kinds = items.Where(i => i.token != DeedLog.OtherToken).GroupBy(i => i.token).OrderByDescending(g => g.Count()).ThenBy(g => KindName(input, family, g.Key), StringComparer.Ordinal)
                                 .Select(g => new FacetOption { Id = g.Key, Label = KindName(input, family, g.Key) }).ToList();
                foreach (var id in Chosen(state, filter, "kind").Where(id => kinds.All(o => o.Id != id)).ToList()) kinds.Add(new FacetOption { Id = id, Label = KindName(input, family, id) });
                defs.Add(new FacetDef { Id = "kind", Title = "Kind", Bar = false, Options = kinds });
                if (family == DeedStretches.Crafting)
                {
                    var mats = items.Select(i => Ask(input.MainMaterial, i.token) ?? MaterialOther).Concat(Chosen(state, filter, "material")).Distinct()
                                    .OrderBy(m => { var r = Array.IndexOf(MaterialTiers, m); return r < 0 ? 99 : r; }).ThenBy(m => m, StringComparer.OrdinalIgnoreCase).ToList();
                    defs.Add(new FacetDef { Id = "material", Title = "Main material", Sub = "by main material", Bar = false, Options = mats.Select(m => new FacetOption { Id = m, Label = m, Colour = GearMaterialColour(m) }).ToList() });
                }
            }
            defs = defs.Where(d => d.Options.Count >= 2 || Chosen(state, filter, d.Id).Count > 0).ToList();
            Func<string, string, bool> pass = null; var narrowed = false;
            if (defs.Count > 0 && items.Count > 0)
            {
                var facetItems = items.Select((it, k) =>
                {
                    var f = new FacetItem { Key = k.ToString(Inv), Weight = 1 };
                    f.Values["deed"] = it.family; f.Values["kind"] = it.token; f.Values["material"] = Ask(input.MainMaterial, it.token) ?? MaterialOther;
                    return f;
                }).ToList();
                var res = Facets(state, filter, defs, facetItems, "kinds", input.IsSelf ? SrcPc : SrcFellows, "kind", "kinds", family == null ? "All deeds" : "All kinds");
                var line = (res.Shown.Count < facetItems.Count ? N(res.Shown.Count) + " of " : "") + N(facetItems.Count) + (facetItems.Count == 1 ? " kind" : " kinds");
                res.Bar.Text = line; res.Bar.Tone = "compact";
                var applied = res.Bar.Items.FirstOrDefault(b => b.Kind == "applied"); if (applied != null) { applied.Text = line; applied.Value = family == null ? "All deeds" : "All kinds"; }
                narrowed = defs.Any(d => Chosen(state, filter, d.Id).Count > 0);
                var ok = new HashSet<string>(res.Shown.Select(it => { var i = items[int.Parse(it.Key, Inv)]; return i.family + "|" + i.token; }), StringComparer.Ordinal);
                if (narrowed)
                {
                    var deeds = new HashSet<string>(Chosen(state, filter, "deed"), StringComparer.Ordinal);
                    pass = family == null ? (Func<string, string, bool>)((fam, token) => deeds.Contains(fam)) : (fam, token) => ok.Contains(fam + "|" + token);   // the mixed feed filters by deed: a chosen deed's stretches whole
                }
                if (stretches.Count > 0 || narrowed) view.Blocks.Add(res.Bar);
            }
            // the stretches that pass (each with what of it passes)
            var shown = stretches.Where(x => pass == null || (x.Fight != null && pass(x.S.Family, WholeDeed)) || x.S.Moments.Any(m => pass(x.S.Family, m.Token)) || x.S.Counts.Keys.Any(k => pass(x.S.Family, DeedStretch.TokenOf(k)))).ToList();
            if (shown.Count == 0)
            {
                view.Blocks.Add(narrowed ? Empty(NothingForChoice, "Clear the filter to see every stretch in this window.")
                    : server != null ? Empty(Name(input) + "'s earlier sessions are on the server", "Not shown: " + DeedServerCount(server) + ".")
                    : !string.IsNullOrEmpty(sessionEmpty) ? Empty("No " + doing + " yet this session", sessionEmpty)
                    : Empty(w == TimeWindow.SinceInstall ? "No " + doing + " recorded" : "No " + doing + " " + (IsOneDay(w) || w == TimeWindow.Session ? RunsWindowWords(w) : "in " + RunsWindowWords(w)),
                            input.IsSelf ? "Choose a longer window: All shows every stretch kept on this PC." : "Choose All for " + Name(input) + "'s earlier sessions."));
                if (server != null && !narrowed)
                {
                    var line = DeedServerLine(input, new FeedServer { Missing = server.Missing, Next = server.Next, State = server.State ?? "loading", UpTo = server.UpTo, Index = server.Index, Of = server.Of, Waited = server.Waited });
                    view.Blocks.Add(new Block { Kind = FeedLineKind, Tone = line.Tone, Title = line.Title, Text = line.Text, NoteLink = line.NoteLink, Value = line.Value });
                    if (server.State == null) view.FetchBlock = server.Next.Id;
                }
                Plate(view, icon, recorded);
                return;
            }
            // pages: DeedFeedTop stretches, each press of Show older the next DeedFeedTop (they hold while the window and the filter stay)
            var choice = string.Join(",", Chosen(state, filter, "deed").Concat(Chosen(state, filter, "kind")).Concat(Chosen(state, filter, "material")).ToArray());
            var moreAt = state.Player + "|" + page + "|" + w + "|" + choice;
            if (state.FeedMoreAt != null && state.FeedMoreAt != moreAt) state.FeedMore = 0;
            state.FeedMoreAt = moreAt;
            var drawn = Math.Min(shown.Count, DeedFeedTop * (1 + Math.Max(0, state.FeedMore)));
            var usedPresses = (Math.Max(drawn, 1) - 1) / DeedFeedTop;
            if (server != null && drawn >= shown.Count && state.FeedMore > usedPresses + 1) state.FeedMore = usedPresses + 1;   // presses never pile up: one may wait for the next session
            var pick = state.View.TryGetValue("Deeds/" + page + "/view", out var p) && FeedViews.Any(v => v.id == p) ? p : FeedLog;
            var feed = DeedFeedBlock(input, family, shown, drawn, pick, pass, stretches.Count, narrowed, server, after, recorded);
            if (server != null && server.State == null && (shown.Count - drawn < DeedFeedTop || state.FeedMore > usedPresses)) view.FetchBlock = server.Next.Id;   // a page ahead, or a press waits on it
            var chip = new List<Block> { new Block { Kind = "note" } };
            var moments = shown.Take(drawn).Sum(x => x.S.Moments.Count(m => pass == null || pass(x.S.Family, m.Token)));
            Switch(view, state, page, "view", StretchesCaption(shown.Count(x => x.Fight == null), moments, shown.Count(x => x.Fight != null)), FeedViews.Select(v => (v.id, v.label, v.id == pick ? FeedBlocks(feed) : chip)).ToArray());
            Plate(view, icon);
        }

        /// <summary>The view switch's caption: "3 stretches, newest first"; with fights "8 stretches · 12 fights" (short: the strip also holds the views, About these numbers and Back).</summary>
        static string StretchesCaption(int stretches, int moments, int fights = 0) =>
            fights == 0 ? N(stretches) + (stretches == 1 ? " stretch" : " stretches") + ", newest first"
                        : (stretches > 0 ? N(stretches) + (stretches == 1 ? " stretch" : " stretches") + " · " : "") + N(fights) + (fights == 1 ? " fight" : " fights");   // 0.9 all-feed: the two apart, short (the strip holds the views too)
        /// <summary>The filter's token for a deed said whole (a fight, a stretch said in one number): the mixed feed's kind of it.</summary>
        const string WholeDeed = "~deed";

        /// <summary>A kind's name in a family's feed: a tree by its kind ("Beech"), anything else by the game's name.</summary>
        static string KindName(PanelInput input, string family, string token) =>
            token == DeedLog.OtherToken ? "other kinds" : family == DeedStretches.Woodcutting && (Ask(input.ItemKind, token) == null && !token.StartsWith("$", StringComparison.Ordinal)) ? TreeKind(input, token) : Who(input, token);

        /// <summary>The picture of a counted kind: a piece for what was placed or planted, a tree for one felled, else the item.</summary>
        static string KindIcon(PanelInput input, string kind, string token)
        {
            switch (kind)
            {
                case DeedLog.Placed: case DeedLog.Planted: return "piece:" + token;
                case DeedLog.Felled: return TreeIcon(TreeKind(input, token));
                default: return "item:" + token;
            }
        }

        /// <summary>Whether a time of a session lies in its minutes kept in tens (EventLog's fold: older than FoldAfterMinutes at the session's last save, on a whole ten minutes).</summary>
        public static bool FoldedTime(DateTime utc, DateTime sessionEndUtc) =>
            utc < sessionEndUtc.AddMinutes(-EventLog.FoldAfterMinutes) && utc.Minute % EventLog.FoldedMinutes == 0 && utc.Second == 0;

        /// <summary>The feed block: one island per stretch drawn (Kind "fight"), the session line over an earlier session's first one, the quiet time to the
        /// next one below; Title the Show older words while stretches are left; Note what is shown and left; a friend's next session as the server line.</summary>
        static Block DeedFeedBlock(PanelInput input, string family, List<FeedStretch> shown, int drawn, string view, Func<string, string, bool> pass, int inWindow, bool narrowed,
                                   FeedServer server, string after, string recorded = null)
        {
            var items = new List<Block>();
            var doing = family == null ? "a deed" : FamilyRow(family).doing;
            for (int i = 0; i < drawn; i++)
            {
                var x = shown[i];
                var b = StretchBlock(input, x, pass, family == null);
                if (!x.Live && (i == 0 || shown[i - 1].Session != x.Session)) b.Items.Insert(0, new Block { Kind = "session", Text = SessionWords(input, new KeptSession { FromUtc = x.Session.FromUtc, ToUtc = x.Session.ToUtc }) });
                if (i + 1 < drawn && shown[i + 1].Session == x.Session)
                {
                    var quiet = x.S.FromUtc - shown[i + 1].S.ToUtc;
                    if (quiet.TotalMinutes >= 1) b.Items.Add(new Block { Kind = "gap", Text = Lapse(quiet) + " without " + doing });
                }
                items.Add(b);
            }
            var left = shown.Count - drawn;
            string note = null;
            if (left > 0 || drawn > DeedFeedTop || server != null || narrowed)
            {
                var where = narrowed ? " that match" : " in this window";
                note = left > 0 ? "Shown: " + N(drawn) + " of " + N(shown.Count) + " stretches" + where + "." : "Shown: all " + (drawn == 1 ? "1 stretch" : N(drawn) + " stretches") + where + ".";
                var parts = new List<string>();
                if (left > 0) parts.Add(N(left) + (left == 1 ? " earlier stretch" : " earlier stretches"));
                if (server != null) parts.Add(DeedServerCount(server));
                if (parts.Count > 0) note += " Not shown: " + Names(parts) + ".";
            }
            if (!string.IsNullOrEmpty(after)) note = (note == null ? "" : note + " ") + after;
            if (!string.IsNullOrEmpty(recorded)) note = (note == null ? "" : note + " ") + recorded + ".";   // rule 2: what 0.9 counts says from when, on this PC
            if (family == null) note = (note == null ? "" : note + " ") + DeedFeedNotByMinute;   // 0.9 all-feed: what no minute holds, said once
            if (left == 0 && server != null) items.Add(DeedServerLine(input, server));
            return new Block { Kind = "feed", Tone = view, Title = left > 0 ? DeedMoreWords(Math.Min(left, DeedFeedTop)) : null, Note = note, Count = drawn, Items = items };
        }

        /// <summary>The Show older button's words: "Show 12 older stretches".</summary>
        public static string DeedMoreWords(int stretches) => "Show " + N(stretches) + " older " + (stretches == 1 ? "stretch" : "stretches");
        public const string DeedShowOlder = "Show older";

        /// <summary>A friend's sessions on the server not on this PC yet, as the count line names them: "2 earlier sessions on the server".</summary>
        static string DeedServerCount(FeedServer s) => N(s.Missing.Count) + (s.Missing.Count == 1 ? " earlier session" : " earlier sessions") + " on the server";

        // the end of a friend's deed feed once every stretch here is drawn and their next session is on the server (FeedBlocks turns it into the last lines)
        static Block DeedServerLine(PanelInput input, FeedServer s)
        {
            var of = SessionOfWords(input, s.Next);
            if (s.State == "loading") return new Block { Kind = "serverline", Tone = "loading", Text = FetchWaited(FetchingWords(of, s.Index, s.Of), s.Waited), Value = s.Next.Id };
            if (s.State == "failed") return new Block { Kind = "serverline", Tone = FeedMoreTone, Title = TryAgainWords, Text = of + " did not arrive.", NoteLink = FeedFetchTarget, Value = s.Next.Id };
            return new Block { Kind = "serverline", Tone = FeedMoreTone, Title = DeedShowOlder, NoteLink = FeedMoreTarget };
        }

        /// <summary>
        /// One stretch as the feed's island: its head ("Crafting at 21:05" and its day when not today; "18 min · 4 made · 2 upgrades"), its moments newest first
        /// (a made line with its first folded in, an upgrade with its level, a feast set out, a first), then its counts by kind (what has no line). With a
        /// filter on, only what passes, and the head says so. A stretch whose start lies in minutes kept in tens says its time "about" and why.
        /// </summary>
        static Block StretchBlock(PanelInput input, FeedStretch x, Func<string, string, bool> pass, bool mixed)
        {
            if (x.Fight != null) return FightLine(input, x.Fight);
            var s = x.S; var fam = s.Family; var row = FamilyRow(fam);
            var end = x.Live ? input.NowUtc : x.Session?.ToUtc ?? s.ToUtc;
            var folded = FoldedTime(s.FromUtc, end);
            bool Ok(string token) => pass == null || pass(fam, token);
            var firsts = new HashSet<string>(s.Moments.Where(m => m.Kind == DeedMoment.First).Select(m => m.Token), StringComparer.Ordinal);
            var lined = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<Block>();
            foreach (var m in s.Moments.Where(m => Ok(m.Token)))
            {
                var e = new Block { Kind = "entry", Id = m.Token, Text = Clock(input, m.Utc, false), Title = Who(input, m.Token) };
                switch (m.Kind)
                {
                    case DeedMoment.Made:
                        e.Icon = "item:" + m.Token; e.Tone = "made"; e.Note = firsts.Contains(m.Token) ? "made, first recorded" : "made"; e.Value = "×" + N(m.Count);
                        lined.Add(DeedLog.Made + "|" + m.Token); break;
                    case DeedMoment.Upgraded:
                        var max = Math.Max(0, input.MaxQuality == null ? 0 : SafeInt(() => input.MaxQuality(m.Token)));
                        e.Icon = "item:" + m.Token; e.Tone = "upgraded"; e.Note = "upgraded to level " + N(m.To); e.Level = m.To; e.Count = max;
                        e.Value = "level " + N(m.To) + (max > 0 ? " of " + N(max) : ""); break;
                    case DeedMoment.Feast:
                        e.Icon = "piece:" + m.Token; e.Tone = "made"; e.Note = "set out"; e.Value = "×" + N(m.Count);
                        lined.Add(DeedLog.Placed + "|" + m.Token); break;
                    case DeedMoment.First:
                        if (s.Moments.Any(o => o.Kind == DeedMoment.Made && o.Token == m.Token)) continue;   // said on its made line
                        var piece = s.Counts.Keys.Any(k => DeedStretch.TokenOf(k) == m.Token && (DeedStretch.KindOf(k) == DeedLog.Placed || DeedStretch.KindOf(k) == DeedLog.Planted));
                        e.Icon = (piece ? "piece:" : "item:") + m.Token; e.Tone = "made"; e.Note = piece ? "placed, first recorded" : "first recorded"; break;
                    default: continue;
                }
                entries.Add(e);
            }
            // what it did by kind: the head's words and the counts lines (what has a line of its own is not counted again)
            var heads = new List<string>(); var counts = new List<Block>();
            // the kinds of the stretch the filter reads (its moments' and its counted kinds' tokens, as the filter bar lists them)
            var kinds = new HashSet<string>(s.Moments.Select(m => m.Token).Concat(s.Counts.Keys.Where(k => KindWords(fam, DeedStretch.KindOf(k)).strip != null).Select(DeedStretch.TokenOf)), StringComparer.Ordinal);
            int kindsAll = kinds.Count, kindsShown = kinds.Count(Ok);
            foreach (var kind in s.Counts.Keys.Select(DeedStretch.KindOf).Distinct().OrderBy(k => KindOrder(fam, k)))   // the page's own deed first (built before recovered, trees before wood)
            {
                if (kind == DeedLog.Counters) { heads.AddRange(CounterWords(s, Ok)); continue; }   // 0.9 all-feed: tamed, km sailed, cargo carried: one number each
                if (kind == DeedLog.Skills)
                {
                    // the skills that rose, a "+" each (Recent's chips: practice has no unit a player reads; the Skills pages show the levels)
                    var rose = s.Counts.Where(kv => DeedStretch.KindOf(kv.Key) == kind && kv.Value > 0 && DeedStretch.TokenOf(kv.Key) != DeedLog.OtherToken && Ok(DeedStretch.TokenOf(kv.Key)))
                                       .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
                    if (rose.Count == 0) continue;
                    heads.Add(N(rose.Count) + (rose.Count == 1 ? " skill practiced" : " skills practiced"));
                    var chips = rose.Take(DeedCountsTop).Select(kv => { var t = DeedStretch.TokenOf(kv.Key); return new Block { Kind = "item", Id = t, Icon = "skill:" + t, Title = SkillName(input, t), Value = "+" }; }).ToList();
                    counts.Add(new Block { Kind = "counts", Title = KindWords(fam, kind).strip, Items = chips, Note = rose.Count > DeedCountsTop ? "+" + N(rose.Count - DeedCountsTop) + " more" : null });
                    continue;
                }
                var words = KindWords(fam, kind);
                var keys = s.Counts.Where(kv => DeedStretch.KindOf(kv.Key) == kind).ToList();
                var ok = keys.Where(kv => Ok(DeedStretch.TokenOf(kv.Key))).ToList();
                var sum = ok.Sum(kv => (double)kv.Value);
                if (sum > 0 && words.one != null) heads.Add(N(sum) + " " + Label1(sum, words.one, words.many));
                if (words.strip == null) continue;
                var rest = ok.Where(kv => !lined.Contains(kv.Key)).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
                if (rest.Count == 0) continue;
                var top = rest.Take(DeedCountsTop).Select(kv =>
                {
                    var t = DeedStretch.TokenOf(kv.Key);
                    return t == DeedLog.OtherToken ? new Block { Kind = "item", Title = "other kinds", Value = NAtLeast(kv.Value) }
                                                   : new Block { Kind = "item", Id = t, Icon = KindIcon(input, kind, t), Title = KindName(input, fam, t), Value = NAtLeast(kv.Value) };
                }).ToList();
                counts.Add(new Block { Kind = "counts", Title = words.strip, Items = top, Note = rest.Count > DeedCountsTop ? "+" + N(rest.Count - DeedCountsTop) + " more" : null });
            }
            var ups = s.Moments.Count(m => m.Kind == DeedMoment.Upgraded && Ok(m.Token));
            if (ups > 0) heads.Add(N(ups) + (ups == 1 ? " upgrade" : " upgrades"));
            var text = FightLength(s.ToUtc - s.FromUtc) + (heads.Count > 0 ? " · " + string.Join(" · ", heads.ToArray()) : "");
            if (pass != null && kindsShown < kindsAll) text += " · " + N(kindsShown) + " of " + N(kindsAll) + " kinds";
            if (folded) text += " · times to the nearest 10 min";
            if (s.MomentsLeft > 0 && pass == null) text += " · " + N(s.MomentsLeft) + " more " + (s.MomentsLeft == 1 ? "moment" : "moments") + " not kept";
            var title = (row.label ?? "Deeds") + " at " + (folded ? "about " : "") + Clock(input, s.FromUtc, false) + FightDayOf(Local(input, s.FromUtc), Local(input, input.NowUtc));
            var block = new Block { Kind = "fight", Id = fam + "@" + s.FromUtc.Ticks.ToString(Inv), Title = title, Text = text, Icon = row.icon, Items = new List<Block>() };
            block.Items.AddRange(entries);
            block.Items.AddRange(counts);
            return block;
        }

        /// <summary>The kinds one counts line names before "+N more".</summary>
        public const int DeedCountsTop = 8;

        /// <summary>A stretch's game counters in its head, one number each in its own words (0.9 all-feed): "2 tamed", "4.2 km sailed", "640 item-km carried".</summary>
        static IEnumerable<string> CounterWords(DeedStretch s, Func<string, bool> ok)
        {
            double V(string token) => s.Counts.TryGetValue(DeedLog.Counters + "|" + token, out var v) && ok(token) ? v : 0;
            var tamed = V("CreatureTamed"); if (tamed > 0) yield return N(tamed) + " tamed";
            var sail = V("DistanceSail"); if (sail > 0) yield return Km(sail) + " sailed";
            var cargo = V(DeedLog.CargoTotal); if (cargo > 0) yield return CargoText(cargo, cargo) + " carried";
        }

        /// <summary>
        /// 0.9 all-feed: a fight in the mixed feed, one line: "Fight at 21:40 · 2 min · 3 foes", and at its right end the link that opens the Battle feed
        /// (Note DeedFightLink, NoteLink BattleFeedTarget; drawn by BattleFeedUi.FightHead in dealt's place). Never the Battle feed's own lines: no foes, no damage.
        /// </summary>
        static Block FightLine(PanelInput input, FoeFight f)
        {
            var foes = f.Foes;
            return new Block
            {
                Kind = "fight", Id = DeedFeedFights + "@" + f.StartUtc.Ticks.ToString(Inv), Icon = FamilyRow(DeedFeedFights).icon,
                Title = "Fight at " + Clock(input, f.StartUtc, false) + FightDayOf(Local(input, f.StartUtc), Local(input, input.NowUtc)),
                Text = FightLength(f.Length) + (foes > 0 ? " · " + N(foes) + (foes == 1 ? " foe" : " foes") : ""),
                Note = DeedFightLink, NoteLink = BattleFeedTarget, Items = new List<Block>(),
            };
        }

        static int SafeInt(Func<int> f) { try { return f(); } catch { return 0; } }
    }
}
