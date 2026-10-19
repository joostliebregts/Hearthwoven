using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 fight pages (work/hearthwoven-0.8.5/boss-fight: Joost's picks A, C and D, round 3's "dealt and received on one screen"), built from the
    /// fights the book knows (FightView.cs, PanelInput.Fights). Pure C#. Drawn by Chapters/FightUi.cs, mirrored in preview/panel-preview.html.
    ///   Battle > Last fight: the newest fight, boss or not (a boss fight on now makes its list entry "Boss fight" with the live dot).
    ///   Battle > Runs and bosses: the fight on now on top, then runs and boss fights newest first, a boss fight in a run nested under it;
    ///     each row opens its page ("fight:" + its Id): a fight's page (A) or a run's (D). Backspace returns (PanelNav).
    /// Block kinds:
    /// - fighthead (A, D): Tone "boss" | "run", Title the boss or the dungeon, Text its land, Icon the trophy or the door; Items Kind "number" (the
    ///   group's dealt and received, Value and Title), then Kind "lane" (a boss: Fraction health left, Fraction2 a minute ago, Colour the land's,
    ///   Value "64 % left", Text the note, Tone live | reset | away | won) or Kind "runline" (a run: Value "went in 21:02", Value2 "left 21:20", Colour the
    ///   land's, Items Kind "span" (Fraction start, Fraction2 end of the run's length, Tone fight | boss, Title the boss) and Kind "fall" (Fraction),
    ///   Text, Note the key words).
    /// - fightfoerows: one row per foe kind, the boss first: Kind "foe" (Id prefab, Icon, Title, Value2 "×N", Tone boss | add | flat, Value dealt to it,
    ///   Fraction on the dealt column's scale, Note what it dealt, Fraction2 on the received column's scale, Id2 in Text: the open/close target,
    ///   Selected open; Items Kind "part" a player's part of the dealt (Colour person:), Kind "rpart" a damage type's part of the received, and,
    ///   open, Kind "sub" per player: their own two bars, by type). Then Kind "dkey" (the players) and Kind "rkey" (the types with their share).
    /// - fightplayers: the players' table. Items Kind "col" (Id dealt | received | blocks | parries | deaths | inside, Title, Icon), an optional
    ///   Kind "dkey" (a boss fight's dealt split: the boss, the foes around), then Kind "player" (Id, Icon, Title, Note how fresh, Value dealt and
    ///   Fraction, Value2 received and Fraction2, Items Kind "part" the dealt split, Kind "rpart" the received by type, Kind "stat" (Id, Value), Kind
    ///   "deaths" (Value, Items Kind "rpart" the types of the last seconds)) or Kind "quiet" (Text in words).
    /// - fightlist (C): Tone now | earlier, Title, Value, Text, Note (where the fight on now is); Items Kind "fight" (Id the page it opens, Tone boss |
    ///   run, Icon, Title, Text how long, Note how it ended, Value dealt, Fraction on the list's scale, Value2 the day, Pill the time, Selected live;
    ///   Items Kind "part" per player, Kind "lane" (the fight on now), Kind "foe" (its adds, on the same scale), Kind "fight" (nested: a boss fight
    ///   in the run)), then Kind "dkey".
    /// The strip's quiet slot (PanelView.StripNote) holds one line (BringLine) when one damage type with a mead or wine against it hit the book's owner hardest.
    /// </summary>
    public static partial class PanelModel
    {
        public const string RunsPage = "runs", RunsLabel = "Runs and bosses", RunsIcon = "vocab:find-dungeon";
        public const string BossFightLabel = "Boss fight", BossFightIcon = "vocab:boss-mark";
        /// <summary>A fight's or a run's own page: "fight:" + FightView.Id (the list entry it belongs to is Runs and bosses).</summary>
        public const string FightPagePrefix = "fight:";
        public const string FightHeadKind = "fighthead", FightFoesKind = "fightfoerows", FightPlayersKind = "fightplayers", FightListKind = "fightlist";
        public static bool IsFightKind(string kind) => kind == FightHeadKind || kind == FightFoesKind || kind == FightPlayersKind || kind == FightListKind;
        /// <summary>A run line's key word for the death mark.</summary>
        public const string FallWord = "fall";
        /// <summary>A boss fight's lane and a run's line take the land's colour; the foes around a boss the quiet one (FacetModel's Other).</summary>
        public const string AroundColour = BarOtherColour;

        public static string FightPageOf(FightView f) => FightPagePrefix + f.Id;
        public static bool IsFightPage(string page) => page != null && page.StartsWith(FightPagePrefix, StringComparison.Ordinal);

        /// <summary>The left-list entry a page belongs to: a fight's or a run's own page is Runs and bosses'.</summary>
        /// <summary>0.8.6: a group fight's page (a fight of the server you were not in, opened from Last fight's line) belongs to Last fight.</summary>
        public static string ListEntryOf(PanelInput input, Chapter c, string page) =>
            c == Chapter.Deeds && page == SetsPageId ? "crafting" :   // 0.9: Armor sets, opened from Crafting (Chapters/ArmorSets.cs)
            c == Chapter.Deeds && IsDeedFeedPage(page) ? DeedFeedFamilyOf(page) ?? RecentPageId :   // 0.9: a deed feed belongs to its page, the mixed one to Recent (Chapters/DeedFeedModel.cs)
            c == Chapter.Deeds && DeedSubParent(page) is string deedOf ? deedOf :   // 0.9: a deed's History and its days belong to the deed's page (Chapters/DeedHistory.cs)
            c == Chapter.Battle && IsDamageHistoryPage(page) ? "damage" :   // 0.9.5 draft: Damage history belongs to Damage (Chapters/DamageHistory.cs)
            c == Chapter.Battle && IsFightPage(page) ? (input?.Fights?.ById(page.Substring(FightPagePrefix.Length))?.Kind == FightKind.Group ? LastFightPage : RunsPage) : page;

        /// <summary>Where a fight's row leads: the fight Last fight holds opens there (its entry lit), any other its own page.</summary>
        public static string FightLink(PanelInput input, FightView f) => "Battle/" + (input?.Fights?.Last == f ? LastFightPage : FightPageOf(f));

        /// <summary>
        /// 0.8.6 (Joost in game: a friend's Last fight was the 0.8 page, "18 foes defeated of 18", where his own said 31 for the same fight): a friend's
        /// book reads the fights your PC joined (your own input among their Fellows), seen by them (FightBook.SeenBy), so one fight shows the same foes,
        /// dealt and received on every book. They are in none: null, and their Last fight keeps the 0.8 page from their copy. Set on each build
        /// (PanelModel.Build): your own book's fights change every refresh.
        /// </summary>
        public static void FriendFights(PanelInput input)
        {
            if (input == null || input.IsSelf) return;
            input.Fights = input.Fellows?.FirstOrDefault(x => x != null && x.IsSelf)?.Fights?.SeenBy(input.PlayerName);
        }

        /// <summary>The fight a page shows: Last fight's, or the one its id names; null when there is none.</summary>
        public static FightView FightOn(PanelInput input, string page) =>
            input?.Fights == null ? null : page == LastFightPage ? input.Fights.Last : IsFightPage(page) ? input.Fights.ById(page.Substring(FightPagePrefix.Length)) : null;

        /// <summary>Battle's list with the fight pages: Runs and bosses after Last fight when the book knows fights, and Last fight read "Boss fight"
        /// with the live dot while a boss fight is on (its page shows that fight).</summary>
        static void FightEntries(PanelInput input, List<Choice> list)
        {
            if (input?.Fights == null) return;
            var last = list.FindIndex(c => c.Id == LastFightPage);
            if (last < 0) return;
            var yours = input.Fights.Last;   // 0.8.6: your own newest fight; a fellow's boss fight on without you does not turn the entry
            if (yours != null && yours.Kind == FightKind.Boss && yours.IsLive) { list[last].Label = BossFightLabel; list[last].Icon = BossFightIcon; list[last].Dot = true; list[last].Disabled = false; }
            list.Insert(last + 1, new Choice { Id = RunsPage, Label = RunsLabel, Icon = RunsIcon });
        }

        // ---------- the words ----------

        /// <summary>"64 %": a health or a share in whole percent.</summary>
        public static string Pct(double f) => (int)Math.Round(Math.Max(0, Math.Min(1, f)) * 100) + " %";

        /// <summary>How a fight stands, in words the book can know (never "lost"): "Live, 64 % left", "Won", "Reset at 41 %", "Got away at 58 %";
        /// a run "Inside now" or "Left" with its deaths ("Left, 4 deaths", "Left, no deaths"; 0.8.6, Joost on "Out": "wat betekent out?"); a group
        /// fight "Live" or "Ended".</summary>
        public static string OutcomeWords(FightView f)
        {
            if (f == null) return "";
            string Health(string before) => f.Health >= 0 ? before + Pct(f.Health) : "";
            if (f.Kind == FightKind.Run)
            {
                var deaths = f.Players.Sum(p => p.Deaths.Count);
                return RunWord(f) + ", " + (deaths == 0 ? "no deaths" : deaths == 1 ? "1 death" : N(deaths) + " deaths");
            }
            switch (f.Outcome)
            {
                case FightOutcome.Live: return f.Kind == FightKind.Boss && f.Health >= 0 ? "Live, " + Pct(f.Health) + " left" : "Live";
                case FightOutcome.Won: return "Won";
                case FightOutcome.Reset: return "Reset" + Health(" at ");
                case FightOutcome.GotAway: return "Got away" + Health(" at ");
                default: return "Ended";
            }
        }

        /// <summary>The outcome's one word for a pill or a lane ("Live", "Won", "Reset", "Got away", "Inside now", "Left", "Ended").</summary>
        public static string OutcomeWord(FightView f) =>
            f.Kind == FightKind.Run ? RunWord(f) : f.Outcome == FightOutcome.GotAway ? "Got away" : f.Outcome.ToString();

        /// <summary>0.8.6: where a run stands: "Inside now" while the group is in, "Left" once the last one came out (the list, the pill and the
        /// time line's "left 21:20" say the same).</summary>
        static string RunWord(FightView f) => f.IsLive ? "Inside now" : "Left";

        /// <summary>A time in the fight on a clock: "5:12", "18:00", "1:02:05".</summary>
        public static string FightClock(TimeSpan t)
        {
            var s = (int)Math.Max(0, Math.Round(t.TotalSeconds));
            return s >= 3600 ? s / 3600 + ":" + (s / 60 % 60).ToString("00", Inv) + ":" + (s % 60).ToString("00", Inv) : s / 60 + ":" + (s % 60).ToString("00", Inv);
        }

        static string BossName(PanelInput input, FightView f) => f.Boss == null ? null : Who(input, f.Boss);
        static string RunName(PanelInput input, FightView f) => !string.IsNullOrEmpty(f.Place) ? f.Place : (f.Biome != null ? BiomeName(f.Biome) + " dungeon" : "A dungeon");
        static string FightName(PanelInput input, FightView f) => f.Kind == FightKind.Run ? RunName(input, f) : f.Kind == FightKind.Boss ? BossName(input, f) : "Fight at " + Clock(input, f.StartUtc, false);
        static string LandColour(string biome) { foreach (var t in BiomeTiles) if (string.Equals(t.key, biome, StringComparison.OrdinalIgnoreCase)) return t.colour; return "#9b8f80"; }
        static string BossTrophy(PanelInput input, string prefab)
        {
            var t = TrophyOf(input, prefab); if (t.Length == 0) t = TrophyOf(input, FoeGroup(input, prefab)); if (t.Length > 0) return t;   // 0.8.5: a kind without its own trophy shows its name's
            var name = Who(input, prefab);
            foreach (var b in Bosses) if (b.name == name) return "item:" + b.trophy;
            return FoeMark;
        }
        static string Day(PanelInput input, DateTime utc) => Local(input, utc).ToString("d MMM", Inv);

        /// <summary>How fresh a player's numbers are, under their name: "this PC" (yours), "live", "near you 12 s" (a group fight), "14 s ago".</summary>
        static string Freshness(PanelInput input, FightView f, FightPlayer p)
        {
            if (p.You) return input.IsSelf ? "this PC" : Name(input) + "'s PC";
            if (IsViewer(input, p.Name)) return null;   // 0.8.6, a friend's book: "Rowan (you)" leaves no room for "near Edda 12 s" in the name column (render-check)
            if (f.Kind == FightKind.Group && p.NearSeconds >= BattleRecorder.NearSeconds) return "near " + Subject(input) + " " + (p.NearSeconds < 60 ? (int)p.NearSeconds + " s" : FightLength(TimeSpan.FromSeconds(p.NearSeconds)));
            if (p.Live && f.IsLive) return "live";
            if (f.IsLive && p.AsOfUtc.HasValue) { var s = (input.NowUtc - p.AsOfUtc.Value).TotalSeconds; return s < 60 ? Math.Max(1, (int)s) + " s ago" : Ago(input.NowUtc - p.AsOfUtc.Value); }
            return null;
        }
        static string PlayerTitle(PanelInput input, FightPlayer p) => p.You ? (input.IsSelf ? "You" : Name(input)) : Person(input, p.Name);

        // ---------- bring next time ----------

        /// <summary>The hits a shield stops: blunt, slash, pierce.</summary>
        static readonly string[] Physical = { "blunt", "slash", "pierce" };
        /// <summary>Physical damage has to be at least this share of what hit you for the shield line (the lead's rule, 10 Oct).</summary>
        public const double ShieldShare = 0.25;
        /// <summary>
        /// "Blocked little": fewer than one block or parry for every ShieldHitsPerBlock hits that landed on you in the fight, and at least
        /// ShieldHitsLeast of them landed. Why: of what came at you, a shield in use takes a good part (the sample's Rowan, 6 against 20 landing at the
        /// Queen); one in five or fewer means it was mostly down or not carried, while physical hits, the ones a shield stops, kept landing. Under
        /// five landed hits the fight is too small to advise on.
        /// </summary>
        public const int ShieldHitsPerBlock = 5, ShieldHitsLeast = 5;

        /// <summary>
        /// The one quiet "bring next time" line (round 3; the lead's rules, 10 Oct): of what hit <paramref name="whom"/> in the fight, the bigger of
        ///   an element the game has a drink against (Remedy: poison, fire, frost) from a third of it (HintShare): "Poison was 62 % of what hit
        ///   you: bring Poison resistance mead.", and
        ///   physical damage (blunt, slash and pierce together) from a quarter (ShieldShare), only when whom blocked little (ShieldHitsPerBlock):
        ///   "Blunt was 50 % of what hit you: bring a shield." (the types together when no one of them leads).
        /// A tie goes to the drink. Lightning and spirit never: the game has nothing against them. <paramref name="guards"/> is whom's blocks and
        /// parries in the fight, <paramref name="hits"/> the hits that landed; hits negative = not known (a fellow's copy, a saved fight): no shield line.
        /// </summary>
        public static string BringLine(IDictionary<string, double> received, string whom, int guards, int hits)
        {
            if (received == null) return null;
            var sum = received.Values.Where(v => v > 0).Sum();
            if (sum <= 0) return null;
            string best = null; double bestShare = 0;
            foreach (var kv in received.Where(kv => kv.Value > 0 && Remedy(kv.Key) != null).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var share = kv.Value / sum;
                if (share >= HintShare && share > bestShare) { bestShare = share; best = TypeName(kv.Key) + " was " + Pct(share) + " of what hit " + whom + ": bring " + Remedy(kv.Key) + "."; }
            }
            var phys = Physical.Where(t => received.TryGetValue(t, out var v) && v > 0).OrderByDescending(t => received[t]).ThenBy(t => t, StringComparer.Ordinal).ToList();
            var physSum = phys.Sum(t => received[t]);
            var blockedLittle = hits >= ShieldHitsLeast && guards * ShieldHitsPerBlock < hits;
            if (blockedLittle && physSum / sum >= ShieldShare && physSum / sum > bestShare)
            {
                var leads = received[phys[0]] >= physSum * 2 / 3;   // one type leads: name it with its own share
                best = leads ? TypeName(phys[0]) + " was " + Pct(received[phys[0]] / sum) + " of what hit " + whom + ": bring a shield."
                             : Cap(Names(phys.Select(t => TypeName(t).ToLowerInvariant()).ToList())) + " were " + Pct(physSum / sum) + " of what hit " + whom + ": bring a shield.";
            }
            return best;
        }

        /// <summary>The line for the book's owner in a fight: what hit them, their blocks and parries, and the hits that landed (not known on a run,
        /// a saved fight or a fellow's copy: no shield line there).</summary>
        static string OwnerBring(PanelInput input, FightView f)
        {
            var me = f.Players.FirstOrDefault(p => p.You && p.Has);
            if (me == null) return null;
            return BringLine(f.ReceivedBy(me.Name), Subject(input), me.Blocks + me.Parries, f.Kind == FightKind.Run ? -1 : me.HitsReceived);
        }

        /// <summary>The book's order of players (the group rows' rule): the book's owner first, then by name, those without numbers last.</summary>
        static List<FightPlayer> InOrder(IEnumerable<FightPlayer> players) =>
            players.OrderByDescending(p => p.You).ThenByDescending(p => p.Has).ThenBy(p => p.Name ?? "", StringComparer.OrdinalIgnoreCase).ToList();

        // ---------- the pages ----------

        public static readonly (string title, string text) FightAboutBoss = ("The fight", "A boss fight starts at the first hit on the boss. Every foe the group fights near the boss belongs to it, and it ends when the boss dies or resets. Each player's numbers come from their own PC.");
        public static readonly (string title, string text) FightAboutGroup = ("The fight", "A fight ends after a minute without hits. Each player's numbers come from their own PC; a fellow player who does not share is not in them.");
        public static readonly (string title, string text) FightAboutRun = ("The run", "A run is one visit to a dungeon: from the first of the group in to the last one out. Each player's time inside is their own.");
        public const string FoeRowsAboutTitle = "Each foe row";
        public const string FoeRowsAbout = "Left: what the group dealt to that kind of foe, a part per player. Right: what those foes dealt the group, a part per damage type. Open a row for each player's own two bars.";
        /// <summary>The damage bars on a fight's page: two columns, each its own scale (DamageBarsAbout says one scale, true on a page of one column).</summary>
        public const string FightBarsAbout = "Dealt and received are two columns, each with its own scale, and each bar starts a quarter of the way along, so even a small one shows what it is made of. The thin line under a bar is its true length. A piece too narrow for its number shows it when you point at it.";
        public const string DealtReceivedAboutTitle = "Dealt and received";
        public const string DealtReceivedAbout = "Dealt is before the foe's armor, as on Damage. Received is after each player's armor, as on Defense.";
        public const string FallsAboutTitle = "Falls";
        public const string FallsAbout = "A fall shows the damage of its last seconds while a record still holds it: a fellow player's copy shares their last 10 falls, and a fight kept from an earlier session keeps only how many. Other falls count, without their types.";
        /// <summary>
        /// The foe rows' layout (round 3: Joost may still pick stacked, so the switch is this one constant): FightSide, the group's dealt and what
        /// hit back on one row; FightStacked, two islands of rows, dealt then received. FightLayoutKey in the state's views overrides it (the
        /// previews draw both).
        /// </summary>
        public const string FightSide = "side", FightStacked = "stacked", FightLayout = FightSide, FightLayoutKey = "Battle/fight/layout";
        public const string NoRunsYet = "No runs or boss fights yet", NoRunsText = "A dungeon visit or a boss fight shows up here: who dealt what, what hit whom, and how it ended.";
        public const string FightGone = "This fight is no longer kept", FightGoneText = "The book keeps the newest runs and boss fights.";

        /// <summary>Last fight, or a fight's or run's own page, from the fight the book knows (FightOn).</summary>
        static void FightPage(PanelInput input, PanelView view, PanelState state, string page, FightView f)
        {
            var asLast = page == LastFightPage;
            if (f.Kind == FightKind.Run) { RunPage(input, view, state, page, f); return; }
            var boss = f.Kind == FightKind.Boss;
            view.Heading = asLast ? (boss && f.IsLive ? BossFightLabel : LastFightLabel) : FightName(input, f) + ", " + Day(input, f.StartUtc) + (f.Kind == FightKind.Group ? "" : " " + Clock(input, f.StartUtc, false));   // 0.8.6: a group fight's name has its time already ("Fight at 13:46, 12 Oct")
            view.Scope = boss ? "Boss fight · " + BossName(input, f) : asLast ? "Last fight" : "Fight";
            view.Blocks.Add(FightHead(input, f));   // one head for every fight: the boss's ring, or a group fight's land with its foes defeated
            FightBody(input, view, state, page, f);
            if (!f.YouWereIn) view.StripNote = NotInFightOf(input);   // 0.8.6: a fight a fellow's feed holds, opened from Last fight's server line: the strip's quiet slot says so
            AboutLines(view, boss ? FightAboutBoss : FightAboutGroup, (FoeRowsAboutTitle, FoeRowsAbout), (DealtReceivedAboutTitle, DealtReceivedAbout), (DamageBarsAboutTitle, FightBarsAbout), (FallsAboutTitle, FallsUntyped(f) ? FallsAbout : null),
                             (WeakAboutTitle, ShowsWeakness(input, f) ? WeakAbout : null),   // 0.9.4 draft: what the foe is weak to (Chapters/FightWeakness.cs)
                             (KeptAboutTitle, asLast && !boss && BeforeThisSession(input, f.EndUtc) ? KeptAbout : null),   // 0.8.5: Last fight kept from an earlier session (a boss attempt: the BossLog's)
                             (DpsAboutTitle, PaceNumber(input, f) != null ? DpsAbout(input, TimeWindow.Session, boss ? DpsBoss : DpsFight, f.IsLive) : null));   // 0.8.6: what DPS is, once
            Plate(view, "ui:chapter-battle");
            var plate = view.Blocks[0];
            plate.Pill = f.IsLive ? FightingNow + " · " + Lapse(f.Length)   // 0.8.6 (Joost): "Fighting now · 8 s", "Fighting now · 1 min 12 s", not a clock "Live, 0:08"
                       : asLast ? FightWhen(input, f.StartUtc, f.EndUtc, boss ? Lapse(f.Length) : FightLength(f.Length))   // 0.8.5: "2 hours ago, at 22:31 · 2 min" from an earlier session
                       : OutcomeWord(f) + ", " + Lapse(f.Length);
            if (asLast && !boss) plate.Items.Insert(0, new Block { Kind = "headlink", Id = "Battle/" + FeedPage, Title = EarlierFights, Icon = FeedIcon });   // as 0.8: the feed holds the fights before it
            if (asLast && ServerLine(input) is Block server) plate.Items.Add(server);
        }

        public const string NotInFight = "You were not in this fight";
        /// <summary>0.8.6 (Joost): the pill of a fight on now, before how long it has run ("Fighting now · 1 min 12 s").</summary>
        public const string FightingNow = "Fighting now";
        /// <summary>"You were not in this fight"; on a friend's book "Edda was not in this fight".</summary>
        static string NotInFightOf(PanelInput input) => input.IsSelf ? NotInFight : Name(input) + " was not in this fight";

        /// <summary>
        /// 0.8.6: Last fight's one quiet line about what happened on the server without you: the group's newest fight, when it is newer than yours
        /// and you were not in it, "Newer on the server: Edda fought a Seeker at 13:46 (you were not in it)", a link to that fight's own page. The
        /// players as the book writes names ("Edda and Finch"), the foes as the pages name them (a boss by name, a group fight's kinds with ×N).
        /// On a friend's book the same from them ("(Edda was not in it)", you as "Rowan (you)"). Null when there is nothing newer.
        /// </summary>
        public static Block ServerLine(PanelInput input)
        {
            var f = input?.Fights?.NewerOnServer;
            if (f == null) return null;
            var who = f.Counted.Select(p => p.Name).ToList(); if (who.Count == 0) who = f.Players.Select(p => p.Name).ToList();
            var names = who.Count == 0 ? "Another player" : Names(who.Select(n => Person(input, n)).ToList());
            string foes;
            if (f.Kind == FightKind.Boss) { var boss = BossName(input, f) ?? "a boss"; foes = boss.StartsWith("The ", StringComparison.Ordinal) ? "the " + boss.Substring(4) : boss; }
            else
            {
                var kinds = f.Foes.OrderByDescending(x => x.Count).Select(x => x.Count > 1 ? Who(input, x.Prefab) + " ×" + x.Count : AOrAn(Who(input, x.Prefab))).ToList();
                foes = kinds.Count == 0 ? "a foe" : kinds.Count <= 3 ? Names(kinds) : Names(kinds.Take(3).ToList()) + " and " + (kinds.Count - 3) + " more";
            }
            var day = Local(input, f.StartUtc).Date != Local(input, input.NowUtc).Date ? ", " + Day(input, f.StartUtc) : "";
            var text = "Newer on the server: " + names + (f.IsLive ? (names.Contains(" and ") ? " are fighting " : " is fighting ") + foes : " fought " + foes + " at " + Clock(input, f.StartUtc, false) + day) + " (" + (input.IsSelf ? "you " + (f.IsLive ? "are" : "were") : Name(input) + (f.IsLive ? " is" : " was")) + " not in it)";
            return new Block { Kind = "link", Id = "Battle/" + FightPageOf(f), Title = text, Icon = f.Kind == FightKind.Boss ? BossFightIcon : FoeMark };
        }

        static string AOrAn(string name) => name.Length > 0 && "AEIOUaeiou".IndexOf(name[0]) >= 0 ? "an " + name : "a " + name;

        static void RunPage(PanelInput input, PanelView view, PanelState state, string page, FightView f)
        {
            var name = RunName(input, f);
            view.Heading = name + ", " + Day(input, f.StartUtc) + " " + Clock(input, f.StartUtc, false);
            view.Scope = "Run · " + name;
            view.Blocks.Add(FightHead(input, f));
            FightBody(input, view, state, page, f);
            AboutLines(view, FightAboutRun, (FoeRowsAboutTitle, FoeRowsAbout), (DealtReceivedAboutTitle, DealtReceivedAbout), (DamageBarsAboutTitle, FightBarsAbout), (FallsAboutTitle, FallsUntyped(f) ? FallsAbout : null),
                             (WeakAboutTitle, ShowsWeakness(input, f) ? WeakAbout : null));   // 0.9.4 draft (Chapters/FightWeakness.cs)
            Plate(view, "ui:chapter-battle");
            view.Blocks[0].Pill = RunWord(f) + (f.IsLive ? " · " + Lapse(f.Length) : " after " + FightLength(f.Length));   // 0.8.6: "Inside now · 18 min", as a fight's "Fighting now · 8 s"
        }

        // the body under the head: the foe rows (side by side, or stacked: dealt, then received), the players' table, and the bring line in the
        // strip's quiet slot (one line, no height of its own: PageHead.cs)
        static void FightBody(PanelInput input, PanelView view, PanelState state, string page, FightView f)
        {
            var layout = state != null && state.View.TryGetValue(FightLayoutKey, out var l) && l == FightStacked ? FightStacked : FightLayout;
            foreach (var part in layout == FightStacked ? new[] { "dealt", "received" } : new[] { FightSide })
            {
                var foes = FightFoeRows(input, state, page, f, part);
                if (foes != null) view.Blocks.Add(foes);
            }
            view.Blocks.Add(FightPlayers(input, f));
            Add(view, FightTypes(f));   // 0.8.6: the page's foot, under the fold
            view.StripNote = OwnerBring(input, f);
        }

        /// <summary>
        /// 0.8.6 (from the 0.8 Last fight; Joost: one fight page everywhere): what the group dealt, by damage type, as the 0.8 page drew it: "By type,
        /// together" with its total, one bar with the numbers in it, each type with its number and share under it, on the same numbers as the head
        /// (each counted player's parts of the foe rows). The one island at the foot of every fight's and run's page, as wide as the plate
        /// (FightTypesTone, Islands.cs): under the fold, so the head, the foe rows and the players still fit one screen (render-check), and the
        /// pinned strip stays on top while it scrolls into view.
        /// </summary>
        public const string FightTypesTone = "fighttypes";
        public static Block FightTypes(FightView f)
        {
            var counted = f.Counted.ToList();
            var b = Composition(counted.Count > 1 ? "By type, together" : "By type", FightView.Sum(counted.Select(p => f.DealtBy(p.Name))), TypeName, Look(DamageLook), null, t => "damage:" + t);
            if (b != null) b.Tone = FightTypesTone;
            return b;
        }

        /// <summary>A fall on the page without its types (FightDeath.Last empty): a fellow's their copy no longer holds, or any fall of a fight kept
        /// from an earlier session (saved as a count).</summary>
        static bool FallsUntyped(FightView f) => f.Players.Any(p => p.Has && p.Deaths.Any(d => d.Last.Count == 0));

        /// <summary>The head island of a boss fight (A) or a run (D): who or where, the group's dealt and received, then the boss's health lane or
        /// the run's time line.</summary>
        public static Block FightHead(PanelInput input, FightView f)
        {
            var run = f.Kind == FightKind.Run; var group = f.Kind == FightKind.Group;
            var colour = LandColour(f.Biome);
            int fought = f.Foes.Sum(x => x.Count), defeated = f.Foes.Sum(x => x.Defeated);
            var head = new Block
            {
                Kind = FightHeadKind, Tone = run ? "run" : group ? "group" : "boss", Id = f.Boss, Colour = colour,
                // a group fight has no name: its land, and how many of its foes fell ("8 of 9 foes defeated")
                Title = group ? (f.Biome != null ? BiomeName(f.Biome) : "A fight") : FightName(input, f),
                Text = group ? (fought == 0 ? null : defeated == fought ? (fought == 1 ? "foe defeated" : "all " + N(fought) + " foes defeated") : N(defeated) + " of " + N(fought) + (fought == 1 ? " foe defeated" : " foes defeated")) : f.Biome != null ? BiomeName(f.Biome) : null,
                Icon = run ? RunsIcon : group ? (f.Biome != null ? BiomeEmblem(f.Biome) : FoeMark) : BossTrophy(input, f.Boss),
                Items = new List<Block>
                {
                    new Block { Kind = "number", Value = NAtLeast(f.DealtTotal), Title = "dealt together", Text = "dealt" },   // Text: the words when the name needs the room
                    new Block { Kind = "number", Value = NAtLeast(f.ReceivedTotal), Title = "received together", Text = "received" },
                },
            };
            var pace = PaceNumber(input, f);   // 0.8.6: the group's pace on a boss, your own in a group fight: a third number
            if (pace != null) head.Items.Add(pace);
            if (!run && f.Health >= 0)
            {
                var won = f.Outcome == FightOutcome.Won;
                var left = won ? 0 : Math.Max(0, Math.Min(1, f.Health));
                var was = f.IsLive && f.HealthMinuteAgo > left ? Math.Min(1, f.HealthMinuteAgo) : 0;
                var down = was > left ? (int)Math.Round((was - left) * 100) : 0;
                var atPace = f.IsLive && was > left ? TimeLeftAtPace(left, was) : "";   // 0.8.6: the time left if the last minute goes on
                var note = f.IsLive ? (down > 0 ? "down " + down + " % in the last minute" : "no change in the last minute")
                         : f.Outcome == FightOutcome.Reset ? "the boss reset from here" : f.Outcome == FightOutcome.GotAway ? "last seen here" : null;
                head.Items.Add(new Block { Kind = "lane", Fraction = (float)left, Fraction2 = (float)was, Colour = colour, Value = won ? null : Pct(left) + " left", Text = note,
                                           Tone = won ? "won" : f.IsLive ? "live" : f.Outcome == FightOutcome.Reset ? "reset" : "away", Title = won ? "defeated" : null,
                                           Note = atPace.Length > 0 ? AtThisPace(atPace) : null });   // 0.8.6: the lane's Note, the time left at the last minute's pace (inside the fill, else after the note)
            }
            if (run)
            {
                var len = f.Length.TotalSeconds; float At(DateTime t) => len > 0 ? (float)Math.Max(0, Math.Min(1, (t - f.StartUtc).TotalSeconds / len)) : 0;
                var line = new Block { Kind = "runline", Colour = colour, Value = "went in " + Clock(input, f.StartUtc, false), Value2 = f.IsLive ? "now" : "left " + Clock(input, f.EndUtc, false), Text = "fighting", Items = new List<Block>() };
                foreach (var s in f.Spans.OrderBy(s => s.StartUtc))
                {
                    var inner = s.FightId == null ? null : input.Fights?.ById(s.FightId);
                    var bossSpan = inner != null && inner.Kind == FightKind.Boss;
                    line.Items.Add(new Block { Kind = "span", Fraction = At(s.StartUtc), Fraction2 = At(s.EndUtc), Tone = bossSpan ? "boss" : "fight", Title = bossSpan ? BossName(input, inner) : null });
                    if (bossSpan) line.Note = BossName(input, inner);
                }
                foreach (var d in f.Players.SelectMany(p => p.Deaths).Where(d => d.AtUtc != default(DateTime)).OrderBy(d => d.AtUtc)) line.Items.Add(new Block { Kind = "fall", Fraction = At(d.AtUtc) });   // a fall whose time is not known is counted, not placed
                head.Items.Add(line);
            }
            return head;
        }

        /// <summary>0.8.6: the head's pace number, the third beside dealt and received. A boss fight: the group's ("13", "DPS together"). A group fight: the book
        /// owner's ("38", "your DPS"; on a friend's book theirs, "38", "Edda's DPS", when their copy shares it). A dash under the floor. null: none (a run,
        /// nothing recorded). Never a player's pace on a boss page, never two players' side by side.</summary>
        static Block PaceNumber(PanelInput input, FightView f)
        {
            string words;
            if (f.Kind == FightKind.Boss) words = BossPace(f);
            else if (f.Kind == FightKind.Group && input != null) { var me = f.Players.FirstOrDefault(x => x.You); words = me == null ? "" : FightPace(me.SwingSeconds, me.SwingDamage); }   // a friend's copy without it (older): nothing
            else return null;
            if (words.Length == 0) return null;
            var whose = input.IsSelf ? "your DPS" : Name(input) + "'s DPS";   // a friend's book: theirs; its short words "DPS" when the land's line needs the room (beside "Edda's book")
            return new Block { Kind = "number", Value = words == DpsDash ? "\u2013" : words.Substring(0, words.Length - 4), Title = f.Kind == FightKind.Boss ? "DPS " + BossPaceWord : whose, Text = f.Kind == FightKind.Boss || !input.IsSelf ? "DPS" : whose };   // Text: the short words when the name needs the room (beside the group's dealt and received, "DPS" alone would read as the group's: yours says so)
        }
        public static string AtThisPace(string time) => "at this pace about " + time + " left";

        /// <summary>The foe rows (round 3): per kind, boss first, two bars on one row: what the group dealt to it, a part per player, and what it
        /// dealt the group, a part per damage type, each column on its own scale (each from a quarter along, its true length under it). An open
        /// row adds each player's own two bars, by type. The keys under the rows: the players, the types with their share of all received.</summary>
        public static Block FightFoeRows(PanelInput input, PanelState state, string page, FightView f, string cols = FightSide)
        {
            if (f.Foes.Count == 0) return null;
            bool dealtCol = cols != "received", receivedCol = cols != "dealt";   // stacked: one column a block, all the room for its bars
            var players = InOrder(f.Players.Where(p => p.Has));
            var foes = FoeRowsOf(input, f).OrderByDescending(x => x.Boss).ThenByDescending(x => x.DealtTotal).ThenBy(x => x.Prefab, StringComparer.Ordinal).ToList();   // 0.8.5: a row per foe by the game's name
            double maxD = foes.Max(x => x.DealtTotal), maxR = foes.Max(x => x.ReceivedTotal);
            var key = "Battle/" + page + "/open";
            var open = state != null && state.View.TryGetValue(key, out var o) ? o : null;
            var hasBoss = foes.Any(x => x.Boss);
            var rows = new List<Block>();
            foreach (var x in foes)
            {
                var parts = PlayerParts(players.Select(p => (p.Name, x.PartOf(p.Name)?.Dealt.Values.Sum() ?? 0)).ToList());
                var rparts = Parts(x.Received, x.ReceivedTotal); foreach (var r in rparts) r.Kind = "rpart";
                var row = new Block
                {
                    Kind = "foe", Id = x.Prefab, Icon = x.Boss ? BossTrophy(input, x.Prefab) : FoeIcon(input, x.Prefab), Title = Who(input, x.Prefab), Value2 = x.Boss || x.Count < 2 ? null : Badge(x.Count),
                    Tone = x.Boss ? "boss" : hasBoss ? "add" : "flat", Value = dealtCol && x.DealtTotal > 0 ? NAtLeast(x.DealtTotal) : null, Fraction = dealtCol && maxD > 0 ? (float)(x.DealtTotal / maxD) : 0,
                    Note = receivedCol && x.ReceivedTotal > 0 ? NAtLeast(x.ReceivedTotal) : null, Fraction2 = receivedCol && maxR > 0 ? (float)(x.ReceivedTotal / maxR) : 0,
                    Text = ViewTarget + key + "=" + (open == x.Prefab ? "" : x.Prefab), Selected = open == x.Prefab,
                    Items = (dealtCol ? parts : new List<Block>()).Concat(receivedCol ? rparts : new List<Block>()).ToList(),
                };
                if (row.Selected)
                {
                    var subs = players.Select(p => (p, part: x.PartOf(p.Name))).Where(s => s.part != null && (s.part.Dealt.Values.Sum() > 0 || s.part.Received.Values.Sum() > 0)).ToList();
                    double sd = subs.Select(s => s.part.Dealt.Values.Sum()).DefaultIfEmpty(0).Max(), sr = subs.Select(s => s.part.Received.Values.Sum()).DefaultIfEmpty(0).Max();
                    foreach (var (p, part) in subs)
                    {
                        double d = part.Dealt.Values.Sum(), r = part.Received.Values.Sum();
                        var sp = Parts(part.Dealt, d); var srp = Parts(part.Received, r); foreach (var q in srp) q.Kind = "rpart";
                        if ((dealtCol ? d : 0) + (receivedCol ? r : 0) <= 0) continue;
                        row.Items.Add(new Block { Kind = "sub", Id = p.Name, Icon = "person:" + p.Name, Title = PlayerTitle(input, p), Colour = "person:" + p.Name,
                                                  Value = dealtCol && d > 0 ? NAtLeast(d) : null, Fraction = dealtCol && sd > 0 ? (float)(d / sd) : 0, Note = receivedCol && r > 0 ? NAtLeast(r) : null, Fraction2 = receivedCol && sr > 0 ? (float)(r / sr) : 0,
                                                  Items = (dealtCol ? sp : new List<Block>()).Concat(receivedCol ? srp : new List<Block>()).ToList() });
                    }
                }
                rows.Add(row);
            }
            if (dealtCol) rows.Add(new Block { Kind = "dkey", Title = "Dealt", Items = players.Select(p => new Block { Kind = "key", Id = p.Name, Icon = "person:" + p.Name, Title = p.You && input.IsSelf ? input.PlayerName : p.Name, Colour = "person:" + p.Name }).ToList() });   // the icon: the page's colours count every player of it (ColorPeople)
            var all = FightView.Sum(f.Foes.Select(x => x.Received));
            if (receivedCol && all.Count > 0)
            {
                var sum = all.Values.Sum(); var types = ReceivedTypes.Where(all.ContainsKey).ToList();
                var pct = WholeShares(types.Select(t => all[t]).ToList());
                rows.Add(new Block { Kind = "rkey", Title = "Received", Items = types.Select((t, k) => new Block { Kind = "key", Id = t, Title = TypeName(t) + " " + pct[k] + " %", Colour = DamageColour(t), Fraction = (float)(all[t] / sum) }).ToList() });
            }
            if (receivedCol) rows.AddRange(WeakLines(input, foes, f.Kind == FightKind.Run));   // 0.9.4 draft (A): what each kind is weak to, under the keys (Chapters/FightWeakness.cs)
            return new Block { Kind = FightFoesKind, Id = key, Tone = cols, Items = rows };
        }

        // a bar split by player (their colours, the group's order), each part's number shown so the parts add up to the row's total
        static List<Block> PlayerParts(List<(string name, double v)> by)
        {
            var on = by.Where(b => b.v > 0).ToList();
            var total = on.Sum(b => b.v);
            var shown = Shown(on.Select(b => b.v).ToList());
            return on.Select((b, k) => new Block { Kind = "part", Id = b.name, Title = b.name, Value = shown[k], Colour = "person:" + b.name, Fraction = (float)(b.v / total) }).ToList();
        }

        /// <summary>The Hearthwoven that first shared the creatures' ids a group fight joins by (0.8.3): a fellow on an older one is never in its numbers.</summary>
        public const string NeedsFightIds = "0.8.3";
        public const string NotSharingLine = "does not share, so not in the numbers";
        public const string NoHitLine = "did not hit these foes";

        /// <summary>
        /// Why a player in a fight has no numbers in it, in words that are true of them (0.8.5; it said "does not share" also of players who do). The
        /// fight adapter lists a player without numbers only when they share but no record of theirs holds the fight (FightPlayer.Shares); a player
        /// who does not share is never named by it (the fight sample's Ylva is). For one who shares, in order: a fight kept from an earlier session
        /// (their copy held nothing of it then); no copy of theirs on this PC now; a Hearthwoven too old to join a fight (the older-version line);
        /// their newest copy came before the fight; their copy holds no fight yet; it no longer holds this one (it carries their newest
        /// FeedShare.MaxFoes foes); it starts after this fight (they reconnected); else their copy covers the time and holds no hit on these foes ("did not hit these foes").
        /// </summary>
        public static string NoPartLine(PanelInput input, FightView f, FightPlayer p)
        {
            var n = p.Name;
            if (!p.Shares) return NotSharingLine;
            if (BeforeThisSession(input, f.EndUtc)) return "shares, but " + n + "'s copy held nothing of this fight when it was kept";
            var fellow = input?.Fellows?.FirstOrDefault(x => x != null && SameName(x.PlayerName, n));
            if (fellow == null) return "shares, but no copy of " + n + "'s is on this PC now";
            var feed = fellow.SharedFeed;
            if (feed == null || OlderThan(fellow, NeedsFightIds) || (feed.Fights.Count > 0 && feed.Fights.All(x => x.Entries.All(e => e.Id.IsNone)))) return OlderLine(fellow, n + "'s part fills in");
            if (fellow.ReceivedUtc.HasValue && fellow.ReceivedUtc.Value < f.StartUtc) return "shares, but " + n + "'s newest copy came in before this fight";
            if (feed.Fights.Count == 0) return "shares, but " + n + "'s copy holds no fight yet";
            if (feed.LeftOut > 0 && feed.Fights.Min(x => x.StartUtc) > f.EndUtc) return "shares, but " + n + "'s copy no longer holds this fight";
            if (feed.T0 > f.EndUtc) return "shares, but " + n + "'s copy starts at " + Clock(input, feed.T0, false) + ", after this fight";
            return NoHitLine;   // 0.8.6 (Joost): plain words, the same on a friend's book
        }

        /// <summary>The players' table: what each dealt (a boss fight: against the boss and the foes around it; else by type) and received (by type),
        /// their blocks and parries, their deaths with the types of their last seconds; a run, their deaths and their own time inside.</summary>
        public static Block FightPlayers(PanelInput input, FightView f)
        {
            var run = f.Kind == FightKind.Run;
            var boss = BossRowOf(input, f);
            var cols = new List<Block>
            {
                new Block { Kind = "col", Id = "dealt", Title = "Dealt" }, new Block { Kind = "col", Id = "received", Title = "Received" },
            };
            if (!run) { cols.Add(new Block { Kind = "col", Id = "blocks", Title = "Blocks", Icon = "vocab:block-mark" }); cols.Add(new Block { Kind = "col", Id = "parries", Title = "Parries", Icon = "vocab:parry-spark" }); }
            var stopped = !run && f.Players.Any(p => p.Has && p.Stopped >= 0);   // what the shield stopped: only where somebody's is known
            if (stopped) cols.Add(new Block { Kind = "col", Id = "stopped", Title = "Stopped" });
            cols.Add(new Block { Kind = "col", Id = "deaths", Title = "Deaths", Icon = "vocab:death" });
            if (run) cols.Add(new Block { Kind = "col", Id = "inside", Title = "Time inside" });   // 0.8.6: "Inside" over "18:00" could read as a time of day
            var items = new List<Block>(cols);
            if (boss != null) items.Add(new Block { Kind = "dkey", Items = new List<Block> { new Block { Kind = "key", Id = "boss", Title = Who(input, boss.Prefab), Colour = LandColour(f.Biome) }, new Block { Kind = "key", Id = "around", Title = "foes around", Colour = AroundColour } } });
            var counted = f.Players.Where(p => p.Has).ToList();
            double maxD = counted.Select(p => f.DealtBy(p.Name).Values.Sum()).DefaultIfEmpty(0).Max(), maxR = counted.Select(p => f.ReceivedBy(p.Name).Values.Sum()).DefaultIfEmpty(0).Max();
            foreach (var p in InOrder(f.Players))   // the book's order: you, then by name (the data's own order is by damage)
            {
                var title = PlayerTitle(input, p); var note = Freshness(input, f, p);
                if (!p.Has) { items.Add(new Block { Kind = "quiet", Id = p.Name, Icon = "person:" + p.Name, Title = title, Note = note, Text = NoPartLine(input, f, p) }); continue; }
                var dealt = f.DealtBy(p.Name); var received = f.ReceivedBy(p.Name);
                double d = dealt.Values.Sum(), r = received.Values.Sum();
                var row = new Block { Kind = "player", Id = p.Name, Icon = "person:" + p.Name, Title = title, Note = note, Colour = "person:" + p.Name,
                                      Value = NAtLeast(d), Fraction = maxD > 0 ? (float)(d / maxD) : 0, Value2 = NAtLeast(r), Fraction2 = maxR > 0 ? (float)(r / maxR) : 0, Items = new List<Block>() };
                if (boss != null)
                {
                    var toBoss = boss.PartOf(p.Name)?.Dealt.Values.Sum() ?? 0;
                    var split = new List<(string, double)> { ("boss", toBoss), ("around", d - toBoss) }.Where(s => s.Item2 > 0).ToList();
                    var shown = Shown(split.Select(s => s.Item2).ToList());
                    for (int k = 0; k < split.Count; k++)
                        row.Items.Add(new Block { Kind = "part", Id = split[k].Item1, Title = split[k].Item1 == "boss" ? Who(input, boss.Prefab) : "foes around", Value = shown[k], Colour = split[k].Item1 == "boss" ? LandColour(f.Biome) : AroundColour, Fraction = (float)(split[k].Item2 / d) });
                }
                else row.Items.AddRange(Parts(dealt, d));
                var rp = Parts(received, r); foreach (var q in rp) q.Kind = "rpart"; row.Items.AddRange(rp);
                if (!run) { row.Items.Add(new Block { Kind = "stat", Id = "blocks", Value = N(p.Blocks) }); row.Items.Add(new Block { Kind = "stat", Id = "parries", Value = N(p.Parries) }); }
                if (stopped) row.Items.Add(new Block { Kind = "stat", Id = "stopped", Value = p.Stopped >= 0 ? NAtLeast(p.Stopped) : null });
                var last = FightView.Sum(p.Deaths.Select(x => x.Last)); var lastSum = last.Values.Sum();
                var dp = lastSum > 0 ? Parts(last, lastSum) : new List<Block>(); foreach (var q in dp) { q.Kind = "rpart"; q.Value = null; }   // its types only: the Deaths page keeps each fall second by second
                row.Items.Add(new Block { Kind = "deaths", Value = N(p.Deaths.Count), Items = dp });
                if (run) row.Items.Add(new Block { Kind = "stat", Id = "inside", Value = p.InsideSeconds >= 0 ? FightClock(TimeSpan.FromSeconds(p.InsideSeconds)) : null });
                items.Add(row);
            }
            return new Block { Kind = FightPlayersKind, Tone = run ? "run" : boss != null ? "boss" : "group", Title = "By player", Items = items };
        }

        // ---------- Runs and bosses (C) ----------

        /// <summary>0.8.6 (Joost: "how does one filter the runs and bosses by time?"): the window Runs and bosses last showed, kept between sessions as the feed's is.</summary>
        public const string RunsWindowKey = "Battle/runs/window";

        /// <summary>
        /// Where the Runs and bosses window starts (null: no start): the short windows their cut, the days their first local midnight, Session when this
        /// connection began (the book keeps earlier sessions' runs too), All none.
        /// </summary>
        static DateTime? RunsFrom(PanelInput input, TimeWindow w) =>
            w == TimeWindow.SinceInstall ? null : IsDayWindow(w) ? WindowStartUtc(input, w) : w == TimeWindow.Session ? SessionFrom(input) ?? input.SessionStartUtc : Cutoff(w, input.NowUtc);

        /// <summary>
        /// The runs and boss fights in a window that starts at <paramref name="from"/> (null: all): one is in when any part of it falls inside (it
        /// began before and ended inside counts; it ended before does not); a live one is always in; a boss fight in a run the book keeps follows its
        /// run, and a run holding a live boss fight is in.
        /// </summary>
        public static List<FightView> RunsInWindow(IEnumerable<FightView> fights, DateTime? from, DateTime? to = null)
        {
            var all = fights.ToList();
            bool Overlaps(FightView f) => (!from.HasValue || f.IsLive || f.EndUtc >= from.Value) && (!to.HasValue || f.StartUtc < to.Value);   // 0.9.1: Yesterday ends at midnight
            FightView RunOf(FightView f) => f.Kind == FightKind.Boss && f.ParentId != null ? all.FirstOrDefault(r => r.Kind == FightKind.Run && r.Id == f.ParentId) : null;
            bool In(FightView f)
            {
                if (f.Kind == FightKind.Run) return Overlaps(f) || all.Any(c => c.IsLive && c.ParentId == f.Id);
                var run = RunOf(f);
                return run != null ? In(run) : Overlaps(f);
            }
            return all.Where(In).ToList();
        }

        /// <summary>The words an empty window says it in: "the last 30 min", "today", "this session".</summary>
        static string RunsWindowWords(TimeWindow w) =>
            w == TimeWindow.LastTenMinutes ? "the last 10 min" : w == TimeWindow.LastThirtyMinutes ? "the last 30 min" : w == TimeWindow.LastHour ? "the last hour" :
            w == TimeWindow.LastThreeHours ? "the last 3 hours" : w == TimeWindow.Session ? "this session" : w == TimeWindow.Today ? "today" :
            w == TimeWindow.Yesterday ? "yesterday" : w == TimeWindow.ThreeDays ? "the last 3 days" :
            w == TimeWindow.SevenDays ? "the last 7 days" : "the last 30 days";

        /// <summary>
        /// The window chips of Runs and bosses (the book's one set, Damage's chips, every window open: the fights carry their own times). The window
        /// shown: the book's chosen one when it was chosen this game (PanelState.WindowPicked); else the one this page last showed (RunsWindowKey,
        /// kept between sessions); else All, the list as before.
        /// </summary>
        static TimeWindow RunsWindowChips(PanelState state, PanelView view)
        {
            var chosen = state.WindowPicked;
            TimeWindow? kept = state.View.TryGetValue(RunsWindowKey, out var k) && Enum.TryParse(k, out TimeWindow kw) && Enum.IsDefined(typeof(TimeWindow), kw) ? kw : (TimeWindow?)null;
            var shown = chosen ? state.Window : kept ?? TimeWindow.SinceInstall;
            view.HasFilters = true; view.ShownWindow = shown;
            foreach (var w in AllWindows) view.Windows.Add(new Choice { Id = w.ToString(), Label = WindowShort(w), Selected = w == shown });
            if (chosen) state.View[RunsWindowKey] = shown.ToString();
            return shown;
        }

        /// <summary>
        /// 0.8.6 (Joost's PC: "A dungeon 34 s · Out, no deaths · 0"): a run that is only a look inside: over, shorter than GlanceSeconds, and nothing
        /// dealt, received or fallen in it. Runs and bosses does not list it (it still happened: the record keeps it, and its page opens by its id).
        /// A minute: a look at the door and back out; anything fought, or a run on now, is listed however short.
        /// </summary>
        public const int GlanceSeconds = 60;
        public static bool Glance(FightView f) =>
            f.Kind == FightKind.Run && !f.IsLive && f.Length.TotalSeconds < GlanceSeconds && f.DealtTotal <= 0 && f.ReceivedTotal <= 0 && f.Players.All(p => p.Deaths.Count == 0);

        static void RunsView(PanelInput input, PanelView view, PanelState state)
        {
            view.Heading = RunsLabel;
            view.Scope = "Runs and boss fights, newest first";
            var book = input.Fights;
            var kept = book.Fights.Where(f => f.Kind == FightKind.Boss || (f.Kind == FightKind.Run && !Glance(f))).ToList();
            if (kept.Count == 0) { view.Blocks.Add(Empty(NoRunsYet, NoRunsText)); Plate(view, "ui:chapter-battle"); return; }
            var win = RunsWindowChips(state, view);
            if (win != TimeWindow.SinceInstall) view.Scope += " · " + WindowLabel(win).ToLowerInvariant();
            var listed = RunsInWindow(kept, RunsFrom(input, win), WindowEndUtc(input, win));
            if (listed.Count == 0)
            {
                // the newest outside the window: a run, or a boss fight not nested in a run
                var newest = kept.Where(f => !(f.Kind == FightKind.Boss && kept.Any(r => r.Kind == FightKind.Run && r.Id == f.ParentId))).OrderByDescending(f => f.EndUtc).First();
                view.Blocks.Add(Empty("Nothing " + (IsOneDay(win) || win == TimeWindow.Session ? "" : "in ") + RunsWindowWords(win), "Newest " + (newest.Kind == FightKind.Run ? "run" : "boss fight") + ": " + AgoWords(input.NowUtc - newest.EndUtc) + "."));
                Plate(view, "ui:chapter-battle");
                return;
            }
            var players = new List<string>();   // the book's order: you, then by name
            foreach (var p in InOrder(listed.SelectMany(f => f.Players).Where(p => p.Has))) if (!players.Any(n => SameName(n, p.Name))) players.Add(p.Name);
            Block Key() => new Block { Kind = "dkey", Items = players.Select(n => new Block { Kind = "key", Id = n, Icon = "person:" + n, Title = n, Colour = "person:" + n }).ToList() };   // the icon: a player of these fights who is not sharing now still gets a colour of their own (ColorPeople)
            // on now: the live boss fight (else a live run), its health lane under its bar, the foes around it under it on the same scale
            var now = book.LiveBoss ?? listed.FirstOrDefault(f => f.IsLive);
            // the run on now: the live boss fight's run, or the live run itself; it is Now's row (› to its page), the live boss fight under it
            var nowRun = now == null ? null : now.Kind == FightKind.Run ? now : now.ParentId != null ? listed.FirstOrDefault(r => r.Kind == FightKind.Run && r.Id == now.ParentId) : null;
            // a boss fight sits under its run wherever that run is a row (on now, or earlier); one whose run the book no longer keeps stands on its own
            bool RowRun(string id) => id != null && listed.Any(r => r.Kind == FightKind.Run && r.Id == id);
            var inside = listed.Where(f => f.Kind == FightKind.Boss && f != now && RowRun(f.ParentId)).ToList();
            List<FightView> Under(FightView run) => inside.Where(c => c.ParentId == run.Id).OrderBy(c => c.StartUtc).ToList();
            Block Nested(FightView c, double max) { var nested = ListRow(input, c, max, players); nested.Value2 = null; return nested; }   // the run's day says it: the time only
            void Nest(Block row, FightView run, double max) { foreach (var c in Under(run)) row.Items.Add(Nested(c, max)); }
            // the boss fight on now: the boss's own damage, its health lane in the row, the foes around it as rows under it on the same scale
            Block NowBoss(Block row, FightView b, double max)
            {
                var bossFoe = BossRowOf(input, b);
                if (bossFoe == null) return row;
                row.Value = NAtLeast(bossFoe.DealtTotal); row.Fraction = max > 0 ? (float)(bossFoe.DealtTotal / max) : 0;
                row.Items = PlayerParts(InOrder(b.Players.Where(p => p.Has)).Select(p => (p.Name, bossFoe.PartOf(p.Name)?.Dealt.Values.Sum() ?? 0)).ToList());
                if (b.Health >= 0) row.Items.Add(new Block { Kind = "lane", Fraction = Math.Max(0, Math.Min(1, b.Health)), Fraction2 = b.HealthMinuteAgo > b.Health ? Math.Min(1, b.HealthMinuteAgo) : 0, Colour = LandColour(b.Biome), Value = Pct(b.Health) + " left" });
                foreach (var x in FoeRowsOf(input, b).Where(x => !x.Boss).OrderByDescending(x => x.DealtTotal))
                    row.Items.Add(new Block { Kind = "foe", Id = x.Prefab, Icon = FoeIcon(input, x.Prefab), Title = Who(input, x.Prefab), Value2 = x.Count < 2 ? null : Badge(x.Count), Value = NAtLeast(x.DealtTotal), Fraction = max > 0 ? (float)(x.DealtTotal / max) : 0,
                                              Items = PlayerParts(InOrder(b.Players.Where(p => p.Has)).Select(p => (p.Name, x.PartOf(p.Name)?.Dealt.Values.Sum() ?? 0)).ToList()) });
                return row;
            }
            if (now != null)
            {
                var liveBoss = now.Kind == FightKind.Boss ? now : null;
                var all = new List<double> { (nowRun ?? now).DealtTotal };
                if (liveBoss != null) all.AddRange(FoeRowsOf(input, liveBoss).Select(x => x.DealtTotal));
                if (nowRun != null) all.AddRange(Under(nowRun).Select(c => c.DealtTotal));
                var max = all.Max();
                Block row;
                if (nowRun != null)
                {
                    row = ListRow(input, nowRun, max, players); row.Value2 = "Now"; row.Pill = null;
                    foreach (var c in Under(nowRun).Concat(liveBoss != null ? new[] { liveBoss } : new FightView[0]).OrderBy(c => c.StartUtc))
                        row.Items.Add(c == liveBoss ? NowBoss(Nested(c, max), c, max) : Nested(c, max));
                }
                else { row = NowBoss(ListRow(input, now, max, players), now, max); row.Value2 = "Now"; row.Pill = null; }
                view.Blocks.Add(new Block { Kind = FightListKind, Tone = "now", Title = "Now", Value = NAtLeast((nowRun ?? now).DealtTotal), Text = "damage dealt together", Items = new List<Block> { row, Key() } });
            }
            // earlier: runs and boss fights, newest first, each boss fight under its run; one scale for the list
            var top = listed.Where(f => f != now && f != nowRun && !inside.Contains(f)).ToList();
            if (top.Count > 0)
            {
                var max = top.Concat(top.SelectMany(Under)).Max(f => f.DealtTotal);
                var rows = new List<Block>();
                foreach (var f in top) { var row = ListRow(input, f, max, players); Nest(row, f, max); rows.Add(row); }
                rows.Add(Key());
                view.Blocks.Add(new Block { Kind = FightListKind, Tone = "earlier", Title = "Earlier", Text = "newest first", Items = rows });
            }
            AboutLines(view, ("This list", "Every dungeon run and boss fight the book knows, newest first. A boss fight inside a run sits under the run. A row opens the run or the fight. A visit under a minute with no fighting and no deaths is not listed."),
                             (DealtReceivedAboutTitle, DealtReceivedAbout), (DamageBarsAboutTitle, DamageBarsAbout));
            Plate(view, "ui:chapter-battle");
            int runs = listed.Count(f => f.Kind == FightKind.Run), bosses = listed.Count(f => f.Kind == FightKind.Boss);
            view.Blocks[0].Pill = string.Join(", ", new[] { runs > 0 ? N(runs) + (runs == 1 ? " run" : " runs") : null, bosses > 0 ? N(bosses) + (bosses == 1 ? " boss fight" : " boss fights") : null }.Where(s => s != null).ToArray());
        }

        // one row of the list: the day and time, the door or the boss, how long and how it ended, the damage dealt split by player (in the list's
        // one order, its key's, so a player's colour sits in the same place on every row), the page it opens
        static Block ListRow(PanelInput input, FightView f, double max, List<string> order)
        {
            int Rank(string n) { var i = order.FindIndex(o => SameName(o, n)); return i < 0 ? int.MaxValue : i; }
            var run = f.Kind == FightKind.Run;
            return new Block
            {
                Kind = "fight", Id = FightLink(input, f), Tone = run ? "run" : "boss", Icon = run ? RunsIcon : BossTrophy(input, f.Boss), Title = FightName(input, f),
                Text = f.IsLive ? FightClock(f.Length) : Lapse(f.Length), Note = OutcomeWords(f), Value = NAtLeast(f.DealtTotal), Fraction = max > 0 ? (float)(f.DealtTotal / max) : 0,
                Value2 = Day(input, f.StartUtc), Pill = Clock(input, f.StartUtc, false), Selected = f.IsLive,
                Items = PlayerParts(f.Players.Where(p => p.Has).OrderBy(p => Rank(p.Name)).Select(p => (p.Name, f.DealtBy(p.Name).Values.Sum())).ToList()),
            };
        }
    }
}
