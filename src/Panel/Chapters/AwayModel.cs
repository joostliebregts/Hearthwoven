using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Company > Since you were away (0.7, Joost 2026-10-09: "when you open your log book after being away: hey, since you last played, this
    /// has happened"). A visual page (variant C, "Group totals first"): how long you were away and since when; the group's totals over that
    /// time as big numbers (what grew in each fellow player's record since your PC last saw it, RecentModel.SinceLastTimeOfFellow); one slim
    /// row per fellow, alphabetical: their share of those totals as a bar (each total counted equally) with the per cent, and what of yours
    /// they enjoyed or put to good use (FellowMarks.StoryFamilies) as item pictures with x N. Only what grew is shown (never "0"); what a copy
    /// cannot tell is left out; a fellow seen for the first time and one with nothing new get a quiet one-line row.
    ///
    /// It opens by itself (OpenAway): the first time the book opens in a session after you were away at least Panel.WhileAwayHours, once per
    /// session, unless Panel.ShowWhileAway is off. Closing the book while still on it (LeaveAway) puts the book back where it was, so the next
    /// opening is the usual one. Reachable any time as the first entry of Company's list (your own book only: it is told to you).
    /// </summary>
    public static partial class PanelModel
    {
        public const string AwayPage = "away", AwayLabel = "While away", AwayHeading = "Since you were away", AwayIcon = "vocab:list-away";
        /// <summary>The one quiet line on what the page can and cannot know.</summary>
        public const string AwayLimits = "Told from what each fellow player last shared. Your own PC records nothing while you are away.";

        // ---------- opening by itself ----------

        /// <summary>
        /// Called when the book opens. The first opening of a session (<paramref name="session"/>) decides once: when you were away at least
        /// <paramref name="hours"/> (AwayFor) and <paramref name="on"/>, the book turns to Since you were away and remembers where it was
        /// (LeaveAway puts it back). Returns whether it turned. Later openings of the same session never turn.
        /// </summary>
        public static bool OpenAway(PanelState s, PanelInput self, bool on, double hours, string session)
        {
            if (s == null || string.IsNullOrEmpty(session) || s.AwayDecidedFor == session) return false;
            var away = AwayFor(self);
            if (away == null && self?.PreviousSessionEndUtc != null) return false;   // the session start not seen yet: decide at a later opening
            s.AwayDecidedFor = session;
            if (!on || away == null || away.Value.TotalHours < Math.Max(0, hours)) return false;
            s.AwayReturn = new PanelPlace { Chapter = s.Chapter, Page = s.PageOf(s.Chapter), Player = s.Player ?? "", About = s.ShowAbout };
            s.AwayCompanyBefore = s.PageOf(Chapter.Company);
            s.Chapter = Chapter.Company; s.Page[Chapter.Company] = AwayPage; s.Player = ""; s.ShowAbout = false;
            return true;
        }

        /// <summary>When the book closes: if it opened itself on Since you were away and is still there, it goes back to where it was, so the
        /// next opening is the usual one. A player who went elsewhere keeps that.</summary>
        public static void LeaveAway(PanelState s)
        {
            if (s?.AwayReturn == null) return;
            var back = s.AwayReturn; s.AwayReturn = null;
            if (s.Chapter != Chapter.Company || s.PageOf(Chapter.Company) != AwayPage || !string.IsNullOrEmpty(s.Player) || s.ShowAbout) return;
            void Put(Chapter c, string page) { if (page == null) s.Page.Remove(c); else s.Page[c] = page; }
            Put(Chapter.Company, s.AwayCompanyBefore);   // Company's own page as it was
            Put(back.Chapter, back.Page);
            s.Chapter = back.Chapter; s.Player = back.Player;   // About is not reopened: the book always reopens on a page
        }

        // ---------- the page (Joost 2026-10-09, variant C "Group totals first": away-prototypes/index.html) ----------
        // The text story was too wordy ("we have to make that visual"): one header line, the group's totals as big numbers, then one slim row
        // per fellow (alphabetical, never ranked): their share of those totals as a bar on one shared scale, the share in per cent, and what of
        // yours they enjoyed or put to good use as item pictures with x N. Labels only; the one quiet limits line at the foot.

        public const string AwayTotalsKind = "awaytotals", AwayRowsKind = "awayrows", AwayFellowKind = "fellow", AwayQuietKind = "quiet";
        public const string AwayForYou = "For you";
        public const string AwayFirstLine = "Started sharing: their part shows next time.", AwayNoTotalsLine = "Nothing in these totals";
        public const string AwayFirstSession = "Your first session on this PC";
        public const string AwayFirstSessionLine = "From your next session on, this page shows what your fellow players did while you were away.";

        /// <summary>One of the group's totals: its id, how it is counted, its picture and its colour. The colour is fixed per total (the hero's
        /// underline and the same part in every fellow's bar), never by rank or size (BAR-FORM.md: the later restyle changes only the shape).</summary>
        sealed class AwayTotal
        {
            public string Id, One, Many, Icon, Colour; public Func<AwayDeeds, double> Of;
            public string Label(double n) => Math.Round(n) == 1 ? One : Many;
        }

        static readonly AwayTotal[] AwayTotals =
        {
            new AwayTotal { Id = "built", One = "piece built", Many = "pieces built", Icon = "vocab:away-hero-built", Colour = "#7f9bab", Of = a => a.Built },
            new AwayTotal { Id = "foes", One = "foe defeated", Many = "foes defeated", Icon = "vocab:weapon-melee", Colour = "#c4553b", Of = a => a.Foes.Values.Sum() },
            new AwayTotal { Id = "mined", One = "stone and ore", Many = "stone and ore", Icon = "vocab:away-hero-stone-ore", Colour = "#a39d92", Of = a => a.Mined },
            new AwayTotal { Id = "wood", One = "wood brought in", Many = "wood brought in", Icon = "vocab:away-hero-wood", Colour = "#b98a52", Of = a => a.Wood },
            new AwayTotal { Id = "dishes", One = "dish cooked", Many = "dishes cooked", Icon = "vocab:away-hero-dishes", Colour = "#d9a441", Of = a => a.Dishes.Values.Sum() },
        };

        static readonly string[] CountWords = { "", "", "two", "three", "four", "five" };
        /// <summary>The rows' caption: "Share of the five totals, each counted equally" (the prototype's header), "Share of this total" for one.</summary>
        public static string AwayShareCaption(int totals) => totals <= 1 ? "Share of this total" : "Share of the " + (totals < CountWords.Length ? CountWords[totals] : N(totals)) + " totals, each counted equally";

        /// <summary>"You were away 3 days, since Thursday 21:40": how long, and when your last session here ended, in your own time (rc1
        /// play-test: "1 hour away · since 10 Oct" did not say it plainly). Without the length: "Your last session ended yesterday 21:40".</summary>
        static string AwayHeadLine(PanelInput input)
        {
            if (input.PreviousSessionEndUtc == null) return AwayFirstSession;
            var since = AwaySince(Local(input, input.PreviousSessionEndUtc.Value), Local(input, input.NowUtc));
            var away = AwayFor(input);
            return away.HasValue ? "You were away " + AwayLength(away.Value) + ", since " + since : "Your last session ended " + since;
        }

        /// <summary>When your last session ended, as people say it (both in local time): "11:05 today", "yesterday 21:40", the weekday within
        /// the week ("Thursday 21:40"), further back the date ("28 Sep"; the year only when it is not this one).</summary>
        public static string AwaySince(DateTime ended, DateTime now)
        {
            var days = (now.Date - ended.Date).Days; var clock = ended.ToString("HH:mm", Inv);
            if (days <= 0) return clock + " today";
            if (days == 1) return "yesterday " + clock;
            if (days < 7) return ended.ToString("dddd", Inv) + " " + clock;
            return ShortDate(ended, now);
        }

        /// <summary>One fellow as the page tells them: first seen, their deeds (null when nothing can be told yet), what of yours they used.</summary>
        sealed class AwayFellow
        {
            public PanelInput Who; public bool First, Restarted; public AwayDeeds Deeds; public DateTime? From, OlderFrom;
            public List<(string icon, string title, double n)> ForYou = new List<(string, string, double)>();
        }

        static AwayFellow AwayFellowOf(PanelInput viewer, PanelInput f)
        {
            var r = new AwayFellow { Who = f };
            var v = SinceLastTimeOfFellow(f);
            if (!v.Shown) { r.First = true; return r; }
            r.Deeds = DeedsOf(viewer, v);
            var gifts = StoryGrowth(f, out var giftsRestarted);
            r.Restarted = v.Restarted.Count > 0 || giftsRestarted;
            r.From = v.FromUtc;
            // their copy is clearly older than your own last session (they were not online then): its date goes under their name
            if (v.FromUtc.HasValue && viewer.PreviousSessionEndUtc.HasValue && v.FromUtc.Value < viewer.PreviousSessionEndUtc.Value.AddHours(-12)) r.OlderFrom = v.FromUtc;
            r.ForYou = ForYou(viewer, f, gifts);
            return r;
        }

        static void Away(PanelInput input, PanelView view)
        {
            view.Heading = AwayHeading; view.Recorded = true;   // labels only: no zones
            var fellows = FellowsOf(input).GroupBy(f => f.PlayerName, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                                          .OrderBy(f => f.PlayerName, StringComparer.OrdinalIgnoreCase).ToList();   // alphabetical, never ranked by size
            var told = fellows.Select(f => AwayFellowOf(input, f)).ToList();
            var withDeeds = told.Where(t => t.Deeds != null).ToList();
            // the group's totals over the away time: a total none of them grew is left out (never a 0)
            var sums = AwayTotals.Select(t => (t, sum: withDeeds.Sum(x => Math.Max(0, t.Of(x.Deeds))))).Where(x => Whole(x.sum)).ToList();
            var head = new Block
            {
                Kind = AwayTotalsKind, Title = AwayHeadLine(input), Src = SrcFellows, Source = TagFellows,
                Items = sums.Select(x => new Block { Id = x.t.Id, Icon = x.t.Icon, Value = N(x.sum), Title = x.t.Label(x.sum), Colour = x.t.Colour, Src = SrcFellows, Source = TagFellows }).ToList(),
            };
            if (input.PreviousSessionEndUtc == null) head.Text = AwayFirstSessionLine;
            view.Blocks.Add(head);
            if (fellows.Count == 0)
            {
                view.Blocks.Add(new Block { Kind = "note", Text = input.Solo ? SoloNote : ShareWaitingNote });
                Plate(view, AwayIcon);
                return;
            }

            // each fellow's share: per total their part of the group's, each total counted equally (1 / totals), so all rows add up to 100 %
            var k = sums.Count;
            var rows = new List<Block>();
            var shares = new Dictionary<AwayFellow, List<Block>>();
            foreach (var t in told.Where(t => t.Deeds != null))
                shares[t] = sums.Select(x => (x, n: Math.Max(0, x.t.Of(t.Deeds)))).Where(p => p.n > 0)
                                .Select(p => new Block { Id = p.x.t.Id, Colour = p.x.t.Colour, Title = p.x.t.Label(p.n), Value = N(p.n), Fraction = (float)(p.n / p.x.sum / k), Src = SrcFellows, Source = TagFellows }).ToList();
            var pct = AwayPercents(told.Select(t => shares.TryGetValue(t, out var ps) ? ps.Sum(p => (double)p.Fraction) : 0).ToList());
            for (int i = 0; i < told.Count; i++)
            {
                var t = told[i]; var name = t.Who.PlayerName;
                var row = new Block { Kind = AwayFellowKind, Id = name, Icon = "person:" + name, Title = name, Src = SrcFellows, Source = TagFellows };
                if (t.OlderFrom.HasValue) row.Note = "from " + Local(input, t.OlderFrom.Value).ToString("d MMM", Inv);
                var parts = shares.TryGetValue(t, out var ps) ? ps : new List<Block>();
                if (parts.Count > 0) { row.Items = parts; row.Value = PercentText(pct[i], ps.Sum(p => (double)p.Fraction)); }
                else
                {
                    row.Kind = AwayQuietKind; row.Items = new List<Block>();
                    row.Text = t.First ? AwayFirstLine
                             : t.Deeds.Any || t.ForYou.Count > 0 ? AwayNoTotalsLine
                             : t.From.HasValue ? "Nothing new since " + Local(input, t.From.Value).ToString("d MMM", Inv) : "Nothing new yet";
                }
                foreach (var g in t.ForYou) row.Items.Add(new Block { Kind = "chip", Id = g.icon, Icon = g.icon, Title = g.title, Value = "×" + N(g.n), Src = SrcFellows, Source = TagFellows });
                rows.Add(row);
            }
            view.Blocks.Add(new Block
            {
                Kind = AwayRowsKind, Title = k > 0 && rows.Any(r => r.Kind == AwayFellowKind) ? AwayShareCaption(k) : null,
                Text = rows.Any(r => r.Items.Any(x => x.Kind == "chip")) ? AwayForYou : null, Items = rows,
            });
            foreach (var t in told.Where(t => t.Restarted))
                view.Blocks.Add(new Block { Kind = "note", Text = "Some of " + t.Who.PlayerName + "'s counts started again from zero (a new install or another PC), so those are left out." });
            view.Blocks.Add(new Block { Kind = "note", Text = AwayLimits });
            Plate(view, AwayIcon);
        }

        /// <summary>Whole per cents for shares that add up to 1 (largest remainder, so they add up to 100); a share above 0 never shows 0: it
        /// takes its 1 from the largest. Shares of 0 stay 0 (those fellows get a quiet row, no number).</summary>
        /// <summary>A whole per cent as text: a real share that rounds to 0 says "&lt;1 %", never 0 (REVIEW-07 #9, as the bar lists).</summary>
        public static string PercentText(int pct, double share) => pct == 0 && share > 0 ? UnderOnePercent : pct.ToString("0", Inv) + " %";

        public static List<int> AwayPercents(IList<double> shares)
        {
            var total = shares.Sum();
            var exact = shares.Select(s => total > 0 ? 100 * s / total : 0).ToList();
            var whole = exact.Select(e => (int)Math.Floor(e)).ToList();
            var left = total > 0 ? 100 - whole.Sum() : 0;
            foreach (var i in Enumerable.Range(0, exact.Count).OrderByDescending(i => exact[i] - whole[i]).ThenBy(i => i).Take(Math.Max(0, left))) whole[i]++;
            for (int i = 0; i < whole.Count; i++)
                if (shares[i] > 0 && whole[i] == 0) { var big = Enumerable.Range(0, whole.Count).OrderByDescending(j => whole[j]).First(); if (whole[big] > 1) { whole[big]--; whole[i] = 1; } }
            return whole;
        }

        /// <summary>"3 days", "30 hours", "40 minutes": the longest unit that still says it in a whole number people use.</summary>
        public static string AwayLength(TimeSpan t)
        {
            if (t.TotalHours >= 48) return Plural(Math.Round(t.TotalDays), "day", "days");
            if (t.TotalMinutes >= 60) return Plural(Math.Round(t.TotalHours), "hour", "hours");
            return Plural(Math.Max(1, Math.Round(t.TotalMinutes)), "minute", "minutes");
        }

        /// <summary>What one fellow did, as the page counts it (whole numbers above 0 only).</summary>
        sealed class AwayDeeds
        {
            public double Trees, Wood, Mined, Built, Ground, Planted, Harvested, Gear, Feasts, Fish, Tamed, Bosses;
            public readonly Dictionary<string, double> Dishes = new Dictionary<string, double>(), Foes = new Dictionary<string, double>();
            public bool Any => Trees + Wood + Mined + Built + Ground + Planted + Harvested + Gear + Feasts + Fish + Tamed + Dishes.Values.Sum() + Foes.Values.Sum() >= 1;
        }

        static double Sum(PanelModel.RecentView v, string family, Func<string, bool> keep = null) =>
            v.Grew.TryGetValue(family, out var d) ? d.Where(kv => keep == null || keep(kv.Key)).Sum(kv => (double)kv.Value) : 0;

        static AwayDeeds DeedsOf(PanelInput viewer, RecentView v)
        {
            var a = new AwayDeeds
            {
                Trees = Sum(v, DeedLog.Felled),
                Wood = Sum(v, DeedLog.PickedUp, t => GatherKind(viewer, t) == "wood"),
                Mined = Sum(v, DeedLog.PickedUp, t => GatherKind(viewer, t) == "mining"),
                Built = Sum(v, DeedLog.Placed, t => PieceKind(viewer, t) == "built"),
                Ground = Sum(v, DeedLog.Placed, t => PieceKind(viewer, t) == "ground"),
                Feasts = Sum(v, DeedLog.Placed, t => PieceKind(viewer, t) == "feast"),
                Planted = Sum(v, DeedLog.Planted),
                Harvested = Sum(v, DeedLog.Picked, t => !t.StartsWith("$", StringComparison.Ordinal)),
                Gear = Sum(v, DeedLog.Made, t => ItemKind(viewer, t) == "gear"),
                Fish = Sum(v, DeedLog.Counters, t => t == "FishCaught"),
                Tamed = Sum(v, DeedLog.Counters, t => t == "CreatureTamed"),
                Bosses = Sum(v, DeedLog.Counters, t => t == "BossKills"),
            };
            if (v.Grew.TryGetValue(DeedLog.Made, out var made)) foreach (var kv in made) if (ItemKind(viewer, kv.Key) == "food") Bump(a.Dishes, kv.Key, kv.Value);
            if (v.Grew.TryGetValue(DeedLog.Kills, out var kills)) foreach (var kv in kills) Bump(a.Foes, kv.Key, kv.Value);
            return a;
        }

        static bool Whole(double n) => Math.Round(n) >= 1;

        /// <summary>What grew in a fellow's StoryFamilies since your PC's mark (family -> token -> growth above 0); a family whose total went down
        /// is left out (<paramref name="restarted"/>). Empty when either side has no since-install totals or the mark predates the story families.</summary>
        static Dictionary<string, Dictionary<string, double>> StoryGrowth(PanelInput f, out bool restarted)
        {
            restarted = false;
            var grew = new Dictionary<string, Dictionary<string, double>>();
            var mark = f?.SeenBefore;
            if (mark == null || !mark.Story || !mark.SinceInstall || !f.SharedSinceInstall) return grew;
            foreach (var kv in FellowMarks.Story(f))
            {
                var prefix = kv.Key + "|";
                var was = mark.Values.Where(m => m.Key.StartsWith(prefix, StringComparison.Ordinal)).ToDictionary(m => m.Key.Substring(prefix.Length), m => (double)m.Value);
                if (kv.Value.Values.Sum(x => (double)x) < was.Values.Sum() - 0.001) { restarted = true; continue; }
                var clipped = was.Count >= FellowMarks.MaxKeysPerFamily;
                foreach (var t in kv.Value)
                {
                    if (!was.TryGetValue(t.Key, out var b) && clipped) continue;
                    if (t.Value - b > 0) { if (!grew.TryGetValue(kv.Key, out var d)) grew[kv.Key] = d = new Dictionary<string, double>(); d[t.Key] = t.Value - b; }
                }
            }
            return grew;
        }

        /// <summary>
        /// Grateful use, from the receiver's record (the fellow whose row it is): YOUR food they enjoyed (servings, from your meals and your
        /// feasts) and YOUR gear they put to good use (times), as at most two pictures with x N (the prototype's bowl x 4, axe x 1). One kind
        /// only: that item's own picture; several: the book's food-shared or gear-shared mark with the sum, so the count is never read as
        /// that many of one item.
        /// </summary>
        static List<(string icon, string title, double n)> ForYou(PanelInput viewer, PanelInput f, Dictionary<string, Dictionary<string, double>> grew)
        {
            var food = new Dictionary<string, double>(); var gear = new Dictionary<string, double>();
            bool Yours(string who) => SameName(who, viewer.PlayerName) && !SameName(who, f.PlayerName);
            void Into(Dictionary<string, double> to, string item, double n) { if (n > 0 && !string.IsNullOrEmpty(item)) Bump(to, item, n); }
            if (grew.TryGetValue("ateFoodMadeBy", out var meals)) foreach (var kv in meals) { var p = Split2(kv.Key); if (Yours(p[0])) Into(food, p[1], kv.Value); }
            // feast servings by Fireside's rule (FeastParts, 0.8): yours when you made the feast (whoever set it out), or set it out with its maker unknown
            if (grew.TryGetValue("ateFromFeastOf", out var feasts) && !SameName(viewer.PlayerName, f.PlayerName))
            {
                grew.TryGetValue("ateFromFeastAt", out var at);
                var you = FiresidePeople(viewer)[0].Name;
                foreach (var part in FeastPartsGrown(viewer, f, feasts, at)) if (SameName(part.Maker, you)) Into(food, part.Feast, part.N);
            }
            if (grew.TryGetValue("equippedGearMadeBy", out var worn)) foreach (var kv in worn) { var p = Split2(kv.Key); if (Yours(p[0])) Into(gear, p[1], kv.Value); }
            var r = new List<(string, string, double)>();
            void Add(Dictionary<string, double> d, string mark, string title)
            {
                var n = d.Values.Sum(); var kinds = d.Where(kv => kv.Value > 0).ToList();
                if (!Whole(n)) return;
                r.Add(kinds.Count == 1 ? ("item:" + kinds[0].Key, Who(viewer, kinds[0].Key), n) : (mark, title, n));
            }
            Add(food, CompanyIcons["food"], "Your food");
            Add(gear, CompanyIcons["gear"], "Your gear");
            return r;
        }
    }
}
