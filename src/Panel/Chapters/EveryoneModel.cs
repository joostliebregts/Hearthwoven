using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The Everyone chip (0.8, work/hearthwoven-0.8/prototypes/PICKS.md section 1: chip C with group block G2; VOCABULARY.md "What did the
    /// group do?"). One chip at the end of the player row turns the page you are on into the group's page: the group's total first (hero),
    /// then one row per player in a fixed order (you first, then the others by name, as the player row reads: never a ranking), each a bar at
    /// their share of the whole, the number and the share in gold; then the page's own composition for the whole group.
    ///
    /// Every player's number is read from THEIR OWN page, built by the page's own code from their copy (as their book shows it), so a row is
    /// always the number that player's page says and the group's total is the sum of the rows shown (EveryoneTests). A fellow's copy is read
    /// with this PC's game data (names, kinds, hammer tabs), as PanelUi reads it for their book.
    ///
    /// Windows: All is each fellow's last shared totals; 10 min to Session come from what reached this PC from them while you played (their
    /// trail, B33), the short ones only when their copies come live; the day windows only for Battle's damage dealt (fellows share days for
    /// nothing else), so on every other page they are greyed with one line on the plate. A fellow whose copy cannot fill the window is named
    /// on their row and left out of the sum, and the hero says who is in it. Pages whose numbers do not add up grey the chip with a reason
    /// (EveryoneOff). The chip's state is one flag for the whole book (PanelState.Everyone), kept in PanelPrefs: it answers "whose book am I
    /// reading" like the names beside it, which also hold across pages.
    /// </summary>
    public static partial class PanelModel
    {
        public const string EveryoneLabel = "Everyone";
        public const string GroupRowsKind = "grouprows", GroupMemberKind = "member", GroupQuietKind = "quiet";
        public const string GroupShareCaption = "share", GroupThisPc = "this PC", GroupLive = "live", GroupAsOf = "as of ";
        /// <summary>The chip alone (nobody else shares yet): its second line and the reason on hover.</summary>
        public const string EveryoneAloneSub = "nobody else yet", EveryoneAloneWhy = "Nobody else shares yet. The group view fills in once fellow players share too.";
        /// <summary>The plate's line on a page whose day windows the group cannot fill (PICKS: fellows share days only for damage dealt).</summary>
        public const string GroupDaysLine = "Today, 7 days and 30 days: fellow players share days only for damage dealt.";
        public const string GroupNotInSum = ", so not in this sum";
        public const string GroupWeaponTypeDays = "By weapon and By type need every player's hits: fellow players share days only as totals.";
        public const string GroupAfterArmour = "after each player's armor";

        // ---------- where the chip works ----------

        /// <summary>Whether the chip shows the group on this page: one of GroupPages, or Deeds > Recent and Meals (their own group views, RecentPage.cs, MealsModel.cs).</summary>
        public static bool EveryoneWorks(Chapter c, string page) => GroupPageOf(c, page) != null || (c == Chapter.Deeds && (page == RecentPageId || page == MealsPageId));

        /// <summary>The pages of a chapter where the chip works, in its list's order (id, label); empty for Skills, Feats and Company.</summary>
        static List<(string id, string label)> EveryonePages(Chapter c)
        {
            var list = c == Chapter.Deeds ? DeedsList : c == Chapter.Battle ? BattleList : c == Chapter.Voyages ? VoyagesList : c == Chapter.Stores ? StoresList : null;
            return list == null ? new List<(string, string)>() : list.Where(x => EveryoneWorks(c, x.id)).Select(x => (x.id, x.label)).ToList();
        }

        /// <summary>The page nearest to <paramref name="page"/> in the chapter's list where the chip works: the next one down, else the nearest
        /// one up (the list's first when the page is not in it); null when the chapter has none.</summary>
        static string NearestEveryonePage(Chapter c, string page)
        {
            var list = c == Chapter.Deeds ? DeedsList : c == Chapter.Battle ? BattleList : c == Chapter.Voyages ? VoyagesList : c == Chapter.Stores ? StoresList : null;
            if (list == null) return null;
            var at = Math.Max(0, Array.FindIndex(list, x => x.id == page));
            for (int k = at; k < list.Length; k++) if (EveryoneWorks(c, list[k].id)) return list[k].id;
            for (int k = at - 1; k >= 0; k--) if (EveryoneWorks(c, list[k].id)) return list[k].id;
            return null;
        }

        /// <summary>
        /// The chip on this page (Joost 2026-10-10: say where it DOES work, in ASD-STE100-like plain words; a press takes you there). null = it
        /// shows the group here; else
        ///   its second line, where it works, short so the chip keeps about its old width and the player row stays put: two pages by name
        ///     ("on Damage, Defence"), Deeds "on the other pages", else the first and "and more" ("on Sailing and more"; a chapter without a
        ///     group view: "on Deeds and more");
        ///   its hover: "Press to open Damage for the whole group.", after the whole list when the second line shortened it ("The group view
        ///     is on Sailing, Cargo and On foot.");
        ///   the page the press opens with Everyone on ("Battle/damage"): the nearest page of this chapter where it works (the next one down the
        ///     list, else the nearest one up); from Skills, Feats, Company or About, the Deeds page you last had open (or the nearest to it);
        ///     About opened from a page where it works goes back to that page.
        /// </summary>
        public static (string sub, string why, string to)? EveryoneOff(Chapter c, string page, bool about, string deedsPage = null)
        {
            if (!about && EveryoneWorks(c, page)) return null;
            var here = EveryonePages(c);
            List<string> all; string sub, to, name;
            if (!about && here.Count > 0)
            {
                all = here.Select(x => x.label).ToList();
                sub = all.Count <= 2 ? "on " + string.Join(", ", all.ToArray()) : c == Chapter.Deeds ? EveryoneOtherPages : "on " + all[0] + EveryoneAndMore;
                var id = NearestEveryonePage(c, page);
                to = c + "/" + id; name = here.First(x => x.id == id).label;
            }
            else
            {
                all = ChapterRow.Where(x => EveryonePages(x.id).Count > 0).Select(x => x.label).ToList();   // in the tabs' order
                sub = "on " + all[0] + EveryoneAndMore;
                var back = about && EveryoneWorks(c, page) ? (c, page) : about && here.Count > 0 ? (c, NearestEveryonePage(c, page)) : (Chapter.Deeds, NearestEveryonePage(Chapter.Deeds, deedsPage ?? "overview"));
                to = back.Item1 + "/" + back.Item2;
                var label = EveryonePages(back.Item1).First(x => x.id == back.Item2).label;
                name = back.Item1 == c && !about ? label : ChapterRow.First(x => x.id == back.Item1).label + " > " + label;
            }
            var press = "Press to open " + name + " for the whole group.";
            return (sub, sub.EndsWith(EveryoneAndMore, StringComparison.Ordinal) ? "The group view is on " + JoinNames(all) + ". " + press : press, to);
        }
        public const string EveryoneOtherPages = "on the other pages", EveryoneAndMore = " and more";

        /// <summary>
        /// A press on the greyed chip, or the book key landing on it: the page it names opens with Everyone on, on your own book (About shut).
        /// False when the chip has nowhere to go (nobody else shares yet: then a press shows the reason).
        /// </summary>
        public static bool EveryoneJump(PanelState state, PanelView view)
        {
            if (state == null || string.IsNullOrEmpty(view?.EveryoneTo)) return false;
            state.ShowAbout = false; state.Player = ""; state.Everyone = true;
            Jump(state, view.EveryoneTo);
            return true;
        }

        /// <summary>Whether this build shows the group page: the chip is on, you read your own book, the page has a group view and somebody else shares.</summary>
        public static bool ShowsEveryone(PanelInput input, PanelState state, string page) =>
            state != null && state.Everyone && !state.ShowAbout && input != null && input.IsSelf && GroupPageOf(state.Chapter, page) != null && FellowsOf(input).Any();

        // ---------- what one player brings to a page ----------

        /// <summary>One part of a player's number (a wood kind, a hammer tab, a damage type), as their page's own bar shows it.</summary>
        sealed class GroupPart { public string Id, Title, Colour, Pattern, Icon; public double Value; }

        /// <summary>What one player brings to a group page in the window.</summary>
        sealed class GroupMeasure
        {
            /// <summary>false: their copy cannot fill this window (Words says why on their row); they are left out of the sum.</summary>
            public bool In = true;
            /// <summary>The row's words instead of a bar ("nothing built yet", "not on this session"); null = a bar.</summary>
            public string Words;
            public double Value, Second;
            /// <summary>The page's own composition of this player's number; null = their page has none.</summary>
            public List<GroupPart> Parts;
            /// <summary>Battle: the rows behind the number (By weapon, By type); null = a total only (a fellow's day).</summary>
            public List<DamageRow> Rows;
            /// <summary>All: a fellow copy without since-install totals, so their number is their last shared session (Battle; REVIEW-08 #12: Deeds,
            /// Voyages and the Hall too, which read the same copy).</summary>
            public bool LastSessionOnly;
        }

        /// <summary>One page's group view: what its number is called, its rows' caption, its own composition, and how a player's number is read.</summary>
        sealed class GroupPage
        {
            public Chapter Chapter; public string Page, Heading, Icon;
            public string One, Many, SecondOne, SecondMany;   // the hero's label (singular, plural), and the second number's
            public string ByPlayer, PartsTitle, PartsNote, None, Note;
            public bool Km, Days, DamageParts;
            public Func<double, double, string> Format;          // (value, the group's total) -> the number as shown; null = a count
            public Func<double, string> What;                    // the hero's label for this total; null = One/Many
            public Func<double, string> NoteOf;                  // a closing line that depends on the total (Cargo's unit); null = none
            public Func<PanelInput, PanelInput, TimeWindow, GroupMeasure> Of;   // (the player, read with your game data; you; the window)
        }

        static GroupPage GroupPageOf(Chapter c, string page) => page == null ? null : GroupPages.FirstOrDefault(g => g.Chapter == c && g.Page == page);

        static GroupPage[] groupPages;
        static GroupPage[] GroupPages => groupPages ?? (groupPages = MakeGroupPages());

        static GroupPage[] MakeGroupPages() => new[]
        {
            Deed("woodcutting", "Woodcutting", "title:woodcutter", "wood brought in", "wood brought in", "Wood brought in, by player", "Wood brought in together, by kind", "no wood brought in yet",
                 bs => (bs.FirstOrDefault(b => b.Kind == "composition" && b.Title == "Wood brought in") is Block c ? ParseCount(c.Value) : 0, HeroNum(bs, "tree felled", "trees felled"),
                        Parts(bs.FirstOrDefault(b => b.Kind == "composition" && b.Title == "Wood brought in"))),
                 "tree felled", "trees felled"),
            Deed("mining", "Mining", "title:miner", "stone and ore brought in", "stone and ore brought in", "Stone and ore brought in, by player", "Stone and ore brought in together, by kind", "no stone or ore yet",
                 bs => (HeroNum(bs, "stone and ore brought in"), 0, Parts(bs.FirstOrDefault(b => b.Kind == "composition" && string.IsNullOrEmpty(b.Title))))),
            Deed("building", "Building", "title:builder", "piece built", "pieces built", "Pieces built, by player", "By category, together", "nothing built yet",
                 bs => (HeroNum(bs, "piece built", "pieces built"), 0, FacetParts(bs, "tab")), partsNote: "the hammer's tab"),
            Deed("groundwork", "Groundwork", "vocab:ground-lower", "groundwork stroke", "groundwork strokes", "Groundwork strokes, by player", "Groundwork together, by kind", "no groundwork yet",
                 bs => (HeroNum(bs, "groundwork strokes"), 0, Parts(bs.FirstOrDefault(b => b.Kind == "composition")))),
            Deed("crafting", "Crafting", "title:smith", "gear crafted", "gear crafted", "Gear crafted, by player", "By kind, together", "no gear crafted yet",
                 bs => (HeroNum(bs, "gear crafted"), HeroNum(bs, "upgrade made", "upgrades made"), FacetParts(bs, "kind")), "upgrade made", "upgrades made"),
            Deed("cooking", "Cooking", "title:cook", "dish cooked", "dishes cooked", "Dishes cooked, by player", "Dishes cooked together, by kind", "nothing cooked yet",
                 bs => (HeroNum(bs, "dish cooked", "dishes cooked"), HeroNum(bs, "feast set out", "feasts set out"), Parts(bs.FirstOrDefault(b => b.Kind == "composition" && b.Title == DishesByKind))),
                 "feast set out", "feasts set out"),
            Deed("farming", "Farming", "title:farmer", "planted", "planted", "Planted, by player", "Planted together, by crop", "nothing planted yet",
                 bs => (HeroNum(bs, "planted"), HeroNum(bs, "picked"), PlantedParts(bs)), "picked", "picked"),
            Deed("fishing", "Fishing", "title:fisher", "fish caught", "fish caught", "Fish caught, by player", "Fish caught together, by kind", "no fish caught yet",
                 bs => (HeroNum(bs, "fish caught"), HeroNum(bs, "hooked"), Parts(AfterSection(bs, "Fish caught", "itemgrid"))), "hooked", "hooked"),
            Deed("taming", "Taming", "title:tamer", "creature tamed", "creatures tamed", "Creatures tamed, by player", null, "nothing tamed yet",
                 bs => (CountOf(bs, "CreatureTamed"), CountOf(bs, "TamedPetting"), null), "time petted", "times petted"),
            new GroupPage
            {
                Chapter = Chapter.Battle, Page = "damage", Heading = "Damage dealt", Icon = "ui:chapter-battle", One = "damage dealt", Many = "damage dealt",
                ByPlayer = "Damage dealt, by player", PartsTitle = "By type, together", None = "no damage dealt", Days = true, DamageParts = true,
                Of = (p, me, w) => BattleMeasure(p, me, w, true),
            },
            new GroupPage
            {
                Chapter = Chapter.Battle, Page = "defense", Heading = "Defense", Icon = "ui:chapter-battle", One = "damage received", Many = "damage received",
                ByPlayer = "Damage received, by player", PartsTitle = "By type, together", PartsNote = GroupAfterArmour, None = "nothing received", DamageParts = true,
                Of = (p, me, w) => BattleMeasure(p, me, w, false),
            },
            Voyage("sailing", "Sailing", "vocab:list-sailing", "km sailed", "Km sailed, by player", "Sailed together: at the helm and as passenger", "not sailed yet",
                   bs => (HeroNum(bs, "km sailed"), JourneyParts(bs, "sail")), "Sailed counts one voyage once for each player aboard."),
            new GroupPage
            {
                Chapter = Chapter.Voyages, Page = "cargo", Heading = "Cargo", Icon = "vocab:cargo-mark", One = "carried", Many = "carried",
                ByPlayer = "Cargo carried, by player", PartsTitle = "Cargo carried together, by item", None = "no cargo carried yet",
                Format = (v, total) => CargoNumber(v, total), What = total => CargoUnit(total) + " carried together",
                NoteOf = CargoNote,
                Of = (p, me, w) => CargoMeasure(p, me),
            },
            Voyage("onfoot", "On foot", "title:explorer", "km on foot", "Km on foot, by player", "On foot together: walking and running", "no km on foot yet",
                   bs => (HeroNum(bs, "km on foot"), JourneyParts(bs, "foot")), null),
            Hall("trader", "Trader", "title:trader", "coin spent", "coins spent", "Coins spent, by player", "Coins spent together, by trader", "no coins spent yet"),
            Hall("smelters", "Smelters", "vocab:list-smelters", "item put in", "items put in", "Put in by hand, by player", "Put in together, by kind", "nothing put in yet"),
        };

        // a Deeds page: each player's number from their own page in the window (DeedsPageOf), read by the page's own labels
        static GroupPage Deed(string page, string heading, string icon, string one, string many, string byPlayer, string partsTitle, string none,
                              Func<List<Block>, (double value, double second, List<GroupPart> parts)> read, string secondOne = null, string secondMany = null, string partsNote = null) =>
            new GroupPage
            {
                Chapter = Chapter.Deeds, Page = page, Heading = heading, Icon = icon, One = one, Many = many, SecondOne = secondOne, SecondMany = secondMany,
                ByPlayer = byPlayer, PartsTitle = partsTitle, PartsNote = partsNote, None = none,
                Of = (p, me, w) =>
                {
                    var v = DeedsPageOf(p, page, w, out var gap);
                    if (v == null) return new GroupMeasure { In = false, Words = gap };
                    var r = read(PageBlocks(v));
                    return new GroupMeasure { Value = r.value, Second = r.second, Parts = r.parts, LastSessionOnly = w == TimeWindow.SinceInstall && OlderCopy(p) };
                },
            };

        static GroupPage Voyage(string page, string heading, string icon, string label, string byPlayer, string partsTitle, string none,
                                Func<List<Block>, (double value, List<GroupPart> parts)> read, string note) =>
            new GroupPage
            {
                Chapter = Chapter.Voyages, Page = page, Heading = heading, Icon = icon, One = label, Many = label, ByPlayer = byPlayer, PartsTitle = partsTitle, None = none, Note = note, Km = true,
                Of = (p, me, w) =>
                {
                    var v = new PanelView { Active = Chapter.Voyages, Page = page };
                    Voyages(p, page, v, new PanelState { Chapter = Chapter.Voyages, Window = TimeWindow.SinceInstall });
                    var r = read(PageBlocks(v));
                    return new GroupMeasure { Value = r.value, Parts = r.parts, LastSessionOnly = OlderCopy(p) };
                },
            };

        static GroupPage Hall(string page, string heading, string icon, string one, string many, string byPlayer, string partsTitle, string none) =>
            new GroupPage
            {
                Chapter = Chapter.Stores, Page = page, Heading = heading, Icon = icon, One = one, Many = many, ByPlayer = byPlayer, PartsTitle = partsTitle, None = none,
                Of = (p, me, w) =>
                {
                    var v = new PanelView { Active = Chapter.Stores, Page = page };
                    Stores(p, page, v);
                    var bs = PageBlocks(v);
                    return new GroupMeasure { Value = HeroNum(bs, one, many), Parts = Parts(bs.FirstOrDefault(b => b.Kind == "composition")), LastSessionOnly = OlderCopy(p) };
                },
            };

        /// <summary>A fellow's copy from a Hearthwoven that shares no since-install totals: its pages are their last shared session.</summary>
        static bool OlderCopy(PanelInput p) => p != null && !p.IsSelf && !p.SharedSinceInstall;

        /// <summary>
        /// A player's own Deeds page in window <paramref name="w"/>, built by the page's own code from their copy (as Deeds builds yours, B33 for
        /// a fellow's Session and short windows: what reached this PC from them while you played); null when the window cannot be told for them,
        /// <paramref name="gap"/> then says why on their row.
        /// </summary>
        static PanelView DeedsPageOf(PanelInput p, string page, TimeWindow w, out string gap)
        {
            gap = null;
            var src = p;
            if (IsShortWindow(w))
            {
                src = DeedsShort(p, w, out _);
                if (src == null) { gap = ShortGap(p, w); return null; }
            }
            else if (IsDayWindow(w))
            {
                src = p.IsSelf ? DeedsWindow(p, w) : null;
                if (src == null) { gap = p.IsSelf ? "no days recorded yet" + GroupNotInSum : "shares no days" + GroupNotInSum; return null; }
            }
            var v = new PanelView { Active = Chapter.Deeds, Page = page };
            var st = new PanelState { Chapter = Chapter.Deeds, Window = TimeWindow.SinceInstall, WindowPicked = true };
            st.Page[Chapter.Deeds] = page;
            if (src.Window != null && (page == "woodcutting" || page == "mining")) DeedsDay(src, page, v, w);
            else if (!DeedsPage(src, page, v, st)) Deeds(src, page, new List<TitleRow>(), v, st);
            return v;
        }

        /// <summary>Why a short window holds nothing from this player (RecentOfFellow's reasons, said short on their row).</summary>
        static string ShortGap(PanelInput p, TimeWindow w)
        {
            if (p.IsSelf) return "recorded from your next session on" + GroupNotInSum;
            if (w != TimeWindow.Session && !p.Timed) return "updates only every few minutes" + GroupNotInSum;
            if (p.Trail == null) return "numbers still on their way" + GroupNotInSum;
            return RecentNotOn;   // they did not play during your session: nothing, said so
        }

        /// <summary>Battle's rows for one player in the window, as their own Battle page reads them (PanelModel.Battle): All the since-install tally
        /// (a fellow's last shared session when they share no totals), a day window your day history (a fellow: their damage dealt per day, no
        /// rows), 10 min to Session the session's log, a fellow's from the start of your session (B33).</summary>
        static GroupMeasure BattleMeasure(PanelInput p, PanelInput me, TimeWindow w, bool dealt)
        {
            var m = new GroupMeasure();
            List<DamageRow> rows;
            if (w == TimeWindow.SinceInstall)
            {
                if (!p.IsSelf && p.DamageSinceInstall == null)
                {
                    if (p.Session == null) return new GroupMeasure { In = false, Words = "shares no damage record" + GroupNotInSum };
                    var c = p.ShallowCopy(); c.DamageSinceInstall = p.Session; rows = DamageSinceInstallRows(c); m.LastSessionOnly = true;
                }
                else rows = DamageSinceInstallRows(p);
            }
            else if (IsDayWindow(w))
            {
                if (!p.IsSelf)
                {
                    if (!dealt) return new GroupMeasure { In = false, Words = "shares no days" + GroupNotInSum };
                    if (p.DealtByDay == null) return new GroupMeasure { In = false, Words = "shares no days yet" + GroupNotInSum };
                    m.Value = DealtInWindow(p, w, me);
                    return m;   // a total per day, no rows: By player only
                }
                var day = InWindow(p, w);
                if (day == null) return new GroupMeasure { In = false, Words = "no days recorded yet" + GroupNotInSum };
                rows = DamageSinceInstallRows(day);
            }
            else
            {
                if (!p.IsSelf)
                {
                    if (w == TimeWindow.Session && SessionFrom(p).HasValue && FellowOffThisSession(p)) return new GroupMeasure { Words = RecentNotOn, Rows = new List<DamageRow>() };
                    var cutoff = Cutoff(w, me.NowUtc);
                    if (cutoff.HasValue && (!p.LastRecordedUtc.HasValue || p.LastRecordedUtc.Value.AddMinutes(SpanOf(p)) <= cutoff.Value))
                        return new GroupMeasure { In = false, Words = "last shared before this window" + GroupNotInSum };
                }
                rows = Damage(p.Log, w, "", me.NowUtc, SessionFrom(p));
            }
            m.Rows = rows;
            if (dealt)
            {
                var d = DealtRows(rows);
                m.Value = d.Sum(r => (double)r.Amount);
                m.Parts = d.GroupBy(r => r.Type).Select(g => new GroupPart { Id = g.Key, Title = TypeName(g.Key), Icon = "damage:" + g.Key, Colour = DamageColour(g.Key), Value = g.Sum(r => (double)r.Amount) }).ToList();
            }
            else
            {
                var byType = ReceivedByType(rows);
                m.Value = byType.Values.Sum();
                m.Parts = byType.Select(kv => new GroupPart { Id = kv.Key, Title = TypeName(kv.Key), Icon = "damage:" + kv.Key, Colour = DamageColour(kv.Key), Value = kv.Value }).ToList();
            }
            return m;
        }

        // Cargo carried: item-metres per item, Hearthwoven's own count on each PC (CargoModel), in one unit for the whole group
        static GroupMeasure CargoMeasure(PanelInput p, PanelInput me) => new GroupMeasure
        {
            Value = CargoItemMetres(p),
            Parts = CargoItems(p).Select(kv => { var tint = ItemTint(me)(kv.Key); return new GroupPart { Id = kv.Key, Title = Who(me, kv.Key), Icon = "item:" + kv.Key, Colour = tint == null ? null : Legible(tint), Value = kv.Value }; }).ToList(),
        };

        // ---------- reading a player's page ----------

        // every block of the page in reading order, a hero's second numbers and a filter bar's linked bars opened too
        static List<Block> PageBlocks(PanelView v)
        {
            var all = new List<Block>();
            foreach (var b in Content(v))
            {
                all.Add(b);
                if (b.Kind == "hero" || b.Kind == "filterbar") all.AddRange(b.Items ?? new List<Block>());
            }
            return all;
        }

        // a number of the page by its label (a hero's, or a second number beside it): the page leaves out a number it has none of
        static double HeroNum(List<Block> bs, params string[] labels) =>
            bs.Where(b => (b.Kind == "hero" || b.Kind == "number") && labels.Contains(b.Title)).Select(b => GroupAmount(b.Value)).FirstOrDefault();

        static double CountOf(List<Block> bs, string id) =>
            bs.Where(b => b.Kind == "counts").SelectMany(b => b.Items ?? new List<Block>()).Where(i => i.Id == id).Select(i => GroupAmount(i.Value)).FirstOrDefault();

        static Block FacetBarOf(List<Block> bs, string id) => bs.FirstOrDefault(b => b.Kind == "facetbar" && b.Id == id);

        // a filter's bar, its folded "Other (n kinds)" opened: a facet bar's fold holds no kinds of its own, its row of chips has every kind with
        // its count (release-0.8.0, Joost's PC: a mod-heavy hammer folded Building's tabs, and the group's bar summed the fold's missing kinds)
        static List<GroupPart> FacetParts(List<Block> bs, string id) => Parts(FacetBarOf(bs, id), bs.FirstOrDefault(b => b.Kind == "facet" && b.Id == id));

        static Block AfterSection(List<Block> bs, string title, string kind)
        {
            var at = bs.FindIndex(b => b.Kind == "section" && b.Title == title);
            return at < 0 ? null : bs.Skip(at + 1).FirstOrDefault(b => b.Kind == kind);
        }

        /// <summary>A shown amount as a number: a count ("1 576", "21.5"), a distance ("6.6 km"), "&lt;1" (0). Its own name beside
        /// CompareModel.Amount, which reads base units (metres) and gives NaN for words.</summary>
        static double GroupAmount(string shown)
        {
            var s = (shown ?? "").Trim();
            if (s.Length == 0 || s == LessThanOne) return 0;
            if (s.EndsWith(" km", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 3);
            return ParseCount(s);
        }

        // a bar's parts (a composition, a facet bar, an item grid), the folded "Other (n kinds)" opened into its kinds
        static List<GroupPart> Parts(Block bar, Block chips = null)
        {
            if (bar?.Items == null) return null;
            var parts = new List<GroupPart>();
            void Add(Block p)
            {
                if (p.Id == FoldId && p.Items != null) { foreach (var x in p.Items) Add(x); return; }
                if (p.Id == FoldId && chips?.Items != null)   // a facet bar's fold: its kinds are the chips the bar does not show
                {
                    var onBar = new HashSet<string>(bar.Items.Where(x => x.Id != FoldId).Select(x => x.Id ?? ""));
                    foreach (var c in chips.Items) if (!onBar.Contains(c.Id ?? "")) Add(new Block { Id = c.Id, Title = c.Title, Value = c.Value });
                    return;
                }
                var v = GroupAmount(p.Value);
                if (v > 0) parts.Add(new GroupPart { Id = p.Id ?? p.Title, Title = p.Title, Colour = p.Colour, Pattern = p.Pattern, Icon = p.Icon, Value = v });
            }
            foreach (var p in bar.Items) Add(p);
            return parts;
        }

        // Farming: what was planted per crop (the crop grid's "planted" pairs) and of the saplings beside it (Also planted)
        static List<GroupPart> PlantedParts(List<Block> bs)
        {
            var grid = bs.FirstOrDefault(b => b.Kind == "cropgrid");
            if (grid == null) return null;
            var parts = new List<GroupPart>();
            foreach (var c in grid.Items ?? new List<Block>())
            {
                var n = (c.Items ?? new List<Block>()).Where(x => x.Title == "planted").Select(x => GroupAmount(x.Value)).FirstOrDefault();
                if (n > 0) parts.Add(new GroupPart { Id = c.Id, Title = c.Title, Icon = c.Icon, Colour = c.Colour, Value = n });
            }
            var also = AfterSection(bs, "Also planted", "strip");
            if (also != null) parts.AddRange(Parts(also) ?? new List<GroupPart>());
            return parts;
        }

        // Sailing, On foot: the journey leg's segments (at the helm, as passenger; walking, running) in km
        static List<GroupPart> JourneyParts(List<Block> bs, string leg)
        {
            var legs = bs.Where(b => b.Kind == "journey").SelectMany(b => b.Items ?? new List<Block>()).Where(l => l.Id == leg).ToList();
            if (legs.Count == 0) return null;
            var parts = new List<GroupPart>();
            foreach (var s in legs[0].Items ?? new List<Block>())
            {
                var v = GroupAmount(s.Value);
                if (v > 0) parts.Add(new GroupPart { Id = s.Id, Title = Cap(s.Title), Colour = s.Colour, Value = v });
            }
            return parts;
        }

        /// <summary>A fellow's copy read with this PC's game data (names, kinds, hammer tabs, dish types, crops, item colours), as PanelUi reads
        /// it for their book; what their copy carries itself stays.</summary>
        static PanelInput ReadWith(PanelInput p, PanelInput me)
        {
            if (p == null || p.IsSelf) return p;
            var c = p.ShallowCopy();
            c.NowUtc = me.NowUtc; c.ViewerName = me.PlayerName; c.PlayerNames = c.PlayerNames ?? me.PlayerNames; c.ToLocal = c.ToLocal ?? me.ToLocal;
            c.DisplayName = c.DisplayName ?? me.DisplayName; c.ItemKind = c.ItemKind ?? me.ItemKind; c.GatherKind = c.GatherKind ?? me.GatherKind; c.PieceKind = c.PieceKind ?? me.PieceKind;
            c.ItemColour = c.ItemColour ?? me.ItemColour; c.StationDish = c.StationDish ?? me.StationDish; c.CropOf = c.CropOf ?? me.CropOf; c.ItemType = c.ItemType ?? me.ItemType;
            c.MainMaterial = c.MainMaterial ?? me.MainMaterial; c.PieceTab = c.PieceTab ?? me.PieceTab; c.PieceMaterial = c.PieceMaterial ?? me.PieceMaterial;
            c.DishType = c.DishType ?? me.DishType; c.DishBoost = c.DishBoost ?? me.DishBoost; c.ItemToken = c.ItemToken ?? me.ItemToken; c.RecipeYield = c.RecipeYield ?? me.RecipeYield; c.Foe = c.Foe ?? me.Foe;
            c.ViewerSessionStartUtc = c.ViewerSessionStartUtc ?? me.SessionStartUtc;
            return c;
        }

        // ---------- the group page ----------

        /// <summary>When a fellow's numbers reached this PC: "live" (their live updates come and the latest is under a minute and a half old), else
        /// "as of 8 Oct 00:24"; your own "this PC".</summary>
        static string Freshness(PanelInput p, PanelInput me)
        {
            if (p.IsSelf) return GroupThisPc;
            var last = p.Trail?.LastUtc ?? p.ReceivedUtc;
            if (p.Timed && !p.Cached && last.HasValue && (me.NowUtc - last.Value).TotalSeconds <= 90) return GroupLive;
            var at = p.Cached ? p.ReceivedUtc : p.LastRecordedUtc ?? p.ReceivedUtc;
            return at.HasValue ? GroupAsOf + Local(me, at.Value).ToString("d MMM HH:mm", Inv) : "as last shared";
        }

        /// <summary>The note a group row draws beside the name (PanelUi.GroupRows, the preview's fitGroupNotes): the whole note when the name and it
        /// fit <paramref name="room"/>; else "as of 8 Oct 00:27" drops its "as of " ("8 Oct 00:27"), since a name cut to "Ed…" is worse than a
        /// shorter note; when that still does not fit, the name gives way. Widths as drawn, the gap between them left out of the room.</summary>
        public static string GroupNoteFit(string note, float nameW, Func<string, float> noteW, float room) =>
            string.IsNullOrEmpty(note) || !note.StartsWith(GroupAsOf, StringComparison.Ordinal) || nameW + noteW(note) <= room ? note : note.Substring(GroupAsOf.Length);

        /// <summary>The plate's first line (PICKS): "You and 3 fellow players, as each last shared: Edda and Finch live, Tor as of 8 Oct 00:24."</summary>
        static string GroupLine(List<(string name, string fresh)> fellows)
        {
            if (fellows.Count == 0) return null;
            if (fellows.Count == 1) return "You and " + fellows[0].name + " (" + fellows[0].fresh + ").";
            var live = fellows.Where(f => f.fresh == GroupLive).Select(f => f.name).ToList();
            var parts = new List<string>();
            if (live.Count > 0) parts.Add(JoinNames(live) + " live");
            parts.AddRange(fellows.Where(f => f.fresh != GroupLive).Select(f => f.name + " " + f.fresh));
            return "You and " + fellows.Count + " fellow players, as each last shared: " + string.Join(", ", parts.ToArray()) + ".";
        }

        /// <summary>The group's view of the page (PICKS G2): the plate's line, the hero, one row per player, the page's own composition for the
        /// group, the closing lines. Battle's Damage puts these under its By player view, beside By weapon and By type for the group's rows.</summary>
        static void EveryonePage(PanelInput input, string page, PanelState state, PanelView view)
        {
            var g = GroupPageOf(state.Chapter, page);
            view.Recorded = true; view.Heading = g.Heading; view.EveryoneOn = true;
            // the windows: those the page offers; one the group cannot fill stays in the row, greyed (a day window but on Damage dealt)
            var offered = WindowsOf(state.Chapter, page);
            var chosen = state.Chapter == Chapter.Deeds && state.Window == TimeWindow.Session && !state.WindowPicked ? TimeWindow.SinceInstall : state.Window;
            var w = offered == null ? TimeWindow.SinceInstall : WindowChips(input, state, view, offered, chosen, (i, x) => GroupWindowOpen(g, i, x));
            var greyDays = offered != null && offered.Any(IsDayWindow) && !g.Days;
            if (greyDays) foreach (var c in view.Windows.Where(c => IsDayWindow((TimeWindow)Enum.Parse(typeof(TimeWindow), c.Id)))) c.Waits = false;   // greyed for the group, not waiting for your day history

            var people = FiresidePeople(input);   // you first, then the others by name, as the player row reads: a fixed order, never a ranking
            var read = people.Select(p => ReadWith(p.Input, input)).ToList();
            var ms = read.Select(p => g.Of(p, input, w)).ToList();
            var inSum = Enumerable.Range(0, ms.Count).Where(k => ms[k].In).ToList();
            var total = inSum.Sum(k => ms[k].Value); var second = inSum.Sum(k => ms[k].Second);
            Func<double, string> fmt = v => g.Format != null ? g.Format(v, total) : g.Km ? KmText(v) : g.DamageParts ? NAtLeast(v) : N(v);
            var blocks = new List<Block>();

            // the hero: the group's total, and the page's second number together; when someone is left out, who is in it
            var left = inSum.Count < ms.Count;
            var label = g.What != null ? g.What(total) : (Math.Round(total) == 1 && !g.Km ? g.One : g.Many) + " together";
            var who = left ? JoinNames(inSum.Select(k => people[k].Input.IsSelf ? "you" : people[k].Name).ToList()) : null;
            if (total > 0)
                blocks.Add(Hero((fmt(total), label, SrcFellows, who),
                                (second > 0 && g.SecondMany != null ? N(second) : null, Math.Round(second) == 1 ? g.SecondOne : g.SecondMany, SrcFellows, null)));

            // one row per player: a bar at their share of the whole, the number, the share; words where there is nothing or no number to give
            var shares = WholeShares(inSum.Select(k => ms[k].Value).ToList());
            var battle = state.Chapter == Chapter.Battle && g.DamageParts;
            var rows = new List<Block>();
            for (int k = 0; k < ms.Count; k++)
            {
                var m = ms[k]; var p = people[k];
                var row = new Block { Kind = GroupMemberKind, Id = p.Name, Icon = "person:" + p.Name, Title = p.Input.IsSelf ? "You" : p.Name, Note = Freshness(read[k], input),
                                      Colour = "person:" + p.Name, Src = SrcFellows, Source = TagFellows };
                var at = inSum.IndexOf(k);
                if (m.In && m.Words == null && m.Value > 0 && total > 0)
                {
                    row.Value = fmt(m.Value); row.Fraction = (float)(m.Value / total);
                    row.Value2 = shares[at] == 0 ? UnderOnePercent : shares[at] + " %";
                }
                else { row.Kind = GroupQuietKind; row.Text = m.Words ?? g.None; }
                if (battle && row.Kind == GroupMemberKind && m.Parts != null) row.Items = DamageMix(m.Parts.Select(x => new KeyValuePair<string, double>(x.Id, x.Value)));   // 0.8: the line under their bar
                rows.Add(row);
            }
            // 0.8 (Joost: the group's Battle views in the damage rows' grammar): Damage's and Defence's rows by player drawn as damage rows
            blocks.Add(new Block { Kind = GroupRowsKind, Title = g.ByPlayer, Text = GroupShareCaption, Tone = !battle ? null : page == "damage" ? DamagePlayersDealt : DamagePlayersReceived, Items = rows, Src = SrcFellows, Source = TagFellows });

            // the page's own composition, for the whole group (the parts summed by kind; each kind keeps the look its page gives it)
            // Battle: a bar by type only when every player in the sum gave their hits (a fellow's day is a total, PICKS: day windows By player only)
            var typed = !g.DamageParts || inSum.All(k => ms[k].Rows != null);
            var bar = typed ? GroupComposition(g, inSum.Select(k => ms[k]).ToList(), fmt) : null;
            var unsplit = inSum.Where(k => ms[k].Value > 0 && (ms[k].Parts == null || ms[k].Parts.Count == 0) && (ms[k].Rows != null || !g.DamageParts)).Select(k => people[k].Input.IsSelf ? "Your" : people[k].Name + "'s").ToList();
            var notes = new List<string>();
            if (bar != null && unsplit.Count > 0) notes.Add(JoinNames(unsplit) + (unsplit.Count == 1 ? " number is" : " numbers are") + " not split by kind, so the bar leaves " + (unsplit.Count == 1 ? "it" : "them") + " out.");
            if (state.Chapter == Chapter.Battle && page == "damage")
                BattleGroupSwitch(view, state, page, blocks, bar, inSum.Select(k => (people[k].Name, rows[k].Title, ms[k])).ToList(), notes);
            else
            {
                if (bar != null) blocks.Add(bar);
                view.Blocks.AddRange(blocks);
            }
            // closing lines: whose Session, a fellow's last session on All, the page's own caveat
            var lastOnly = inSum.Where(k => ms[k].LastSessionOnly).Select(k => people[k].Name).ToList();
            if (lastOnly.Count > 0) notes.Add(JoinNames(lastOnly) + (lastOnly.Count == 1 ? " shares" : " share") + " no totals: their number is their last shared session.");
            if (w == TimeWindow.Session) notes.Add(WindowFellowSession);
            if (g.Note != null) notes.Add(g.Note);
            if (g.NoteOf != null && total > 0) notes.Add(g.NoteOf(total));
            foreach (var n in notes) view.Blocks.Add(new Block { Kind = "note", Text = n });

            var fresh = Enumerable.Range(0, people.Count).Where(k => !people[k].Input.IsSelf).Select(k => (people[k].Name, rows[k].Note)).ToList();
            Plate(view, g.Icon, string.Join("\n", new[] { GroupLine(fresh), greyDays ? GroupDaysLine : null }.Where(s => s != null).ToArray()));
            // every number here is the group's: fellow players' PCs (and yours), never "this PC" alone (no "Recorded from" label)
            foreach (var b in Content(view)) Fellowed(b);
        }

        static void Fellowed(Block b)
        {
            if (b == null) return;
            if (b.Src != null || b.Source != null) { b.Src = SrcFellows; b.Source = TagFellows; }
            b.RecordedFrom = null; b.From = null;
            foreach (var i in b.Items ?? new List<Block>()) Fellowed(i);
        }

        /// <summary>The windows the group view can show: All; the short ones as your own page; a day window only where fellows share days
        /// (damage dealt), from your day history.</summary>
        static bool GroupWindowOpen(GroupPage g, PanelInput me, TimeWindow w)
        {
            if (w == TimeWindow.SinceInstall) return true;
            if (IsDayWindow(w)) return g.Days && DayOpen(me, w);
            return g.Chapter == Chapter.Battle ? WindowShared(me, w) : DeedsWindowOpen(me, w);
        }

        static string KmText(double km) => km < 100 ? km.ToString("0.0", Inv) : N(km);

        /// <summary>The page's own bar for the whole group: every player's parts summed by kind (the look of the first who has the kind: yours when
        /// you have it), sorted and folded as every bar is (Composition); null when the page has no bar or nobody has parts.</summary>
        static Block GroupComposition(GroupPage g, List<GroupMeasure> ms, Func<double, string> fmt)
        {
            if (g.PartsTitle == null) return null;
            var sum = new Dictionary<string, double>(); var look = new Dictionary<string, GroupPart>();
            foreach (var m in ms)
                foreach (var p in m.Parts ?? new List<GroupPart>())
                {
                    if (p.Value <= 0 || string.IsNullOrEmpty(p.Id)) continue;
                    sum.TryGetValue(p.Id, out var o); sum[p.Id] = o + p.Value;
                    if (!look.ContainsKey(p.Id)) look[p.Id] = p;
                }
            if (sum.Count == 0) return null;
            // two players' bars may have handed one colour to two different kinds (each bar keeps its own parts apart): the larger kind keeps it,
            // the other takes the bars' palette (BarColours), so no two parts of the group's bar share a colour
            var colour = new Dictionary<string, string>(); var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in sum.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key))
            {
                var c = look[id].Colour;
                colour[id] = !string.IsNullOrEmpty(c) && used.Add(c) ? c : null;
            }
            var b = Composition(g.PartsTitle, sum, k => look[k].Title, k => (colour[k], look[k].Pattern), SrcFellows, k => look[k].Icon, g.PartsNote);
            if (b == null) return null;
            b.Value = null;   // the hero says the total; the head line names the bar
            // a member's fold that could not be opened stays one part of its own (no kinds inside it): read by its id, never as a fold
            foreach (var part in b.Items) part.Value = fmt(part.Id == FoldId && part.Items != null ? part.Items.Sum(x => sum.TryGetValue(x.Id, out var a) ? a : 0) : sum.TryGetValue(part.Id, out var one) ? one : 0);
            if (g.DamageParts) { b.Value = fmt(sum.Values.Sum()); }   // a damage-type bar carries its total in its head, as Battle's do
            if (b.Items.Count == 1) b.Tone = "single";
            return b;
        }

        /// <summary>Damage dealt with Everyone (PICKS: the view switch gains By player first): By player holds the hero, the rows and the group's
        /// damage by type; By weapon is the page's own view over every player's hits together; By type is the page's grid with the players across
        /// (group-battle b: damage types down, a column per player headed by their shield, totals right and under). A day window has only By
        /// player (fellows share damage dealt per day as a total).</summary>
        static void BattleGroupSwitch(PanelView view, PanelState state, string page, List<Block> byPlayer, Block bar, List<(string name, string label, GroupMeasure m)> players, List<string> notes)
        {
            if (bar != null) byPlayer.Add(bar);
            var allRows = players.All(p => p.m.Rows != null);
            var rows = players.Where(p => p.m.Rows != null).SelectMany(p => p.m.Rows).ToList();
            List<Block> byWeapon = null, byType = null;
            var grid = allRows ? PlayersGrid(players) : null;
            if (grid != null)
            {
                var mixes = DamageMixes(rows);
                var topType = grid.Items.Skip(1).OrderByDescending(t => ParseCount(t.Value)).First(); var topWeapon = mixes.OrderByDescending(m => ParseCount(m.Value)).First();
                byWeapon = new List<Block> { TopLine(topWeapon.Value, topWeapon.Title + " damage together, the most of any weapon", null) }; byWeapon.AddRange(mixes);
                byType = new List<Block> { TopLine(topType.Value, topType.Title + " damage together, the most of any type", null), grid };
            }
            else if (!allRows && players.Any(p => p.m.Value > 0)) notes.Add(GroupWeaponTypeDays);
            Switch(view, state, page, "group", DealtCaption, ("player", "By player", byPlayer), ("weapon", ByWeapon, byWeapon), ("type", ByType, byType));
        }

        /// <summary>By type with Everyone (group-battle b): the page's damage grid (DamageGrid) with a column per player instead of a weapon kind,
        /// each headed by their shield; one bar scale for the whole grid, totals right and under, a player with none of a type an empty cell.</summary>
        static Block PlayersGrid(List<(string name, string label, GroupMeasure m)> players)
        {
            var cell = new Dictionary<string, double>();
            foreach (var p in players) foreach (var r in DealtRows(p.m.Rows ?? new List<DamageRow>())) { var k = r.Type + "|" + p.name; cell.TryGetValue(k, out var o); cell[k] = o + r.Amount; }
            var max = cell.Values.DefaultIfEmpty(0).Max();
            if (max <= 0) return null;
            var items = new List<Block>
            {
                new Block
                {
                    Kind = "weapons", Title = TotalHead,
                    Items = players.Select(p => { var sum = BattleTypes.Sum(t => Get(cell, t + "|" + p.name)); return new Block { Kind = "weapon", Id = p.name, Title = p.label, Icon = "person:" + p.name, Colour = "person:" + p.name, Value = NAtLeast(sum), Tone = sum > 0 ? null : "idle", Src = SrcFellows, Source = TagFellows }; }).ToList(),
                },
            };
            foreach (var t in BattleTypes)
            {
                var total = players.Sum(p => Get(cell, t + "|" + p.name));
                if (total <= 0) continue;
                items.Add(new Block
                {
                    Kind = "dmgtype", Id = t, Title = TypeName(t), Icon = DamageIcon(t), Colour = DamageColour(t), Value = NAtLeast(total), Src = SrcFellows, Source = TagFellows,
                    Items = players.Select(p => { var v = Get(cell, t + "|" + p.name); return new Block { Kind = "cell", Id = p.name, Value = v > 0 ? NAtLeast(v) : "", Fraction = (float)(v / max), Colour = DamageColour(t) }; }).ToList(),
                });
            }
            return new Block { Kind = "damagegrid", Title = AllTypes, Value = NAtLeast(cell.Values.Sum()), Src = SrcFellows, Source = TagFellows, Items = items };
        }
    }
}
