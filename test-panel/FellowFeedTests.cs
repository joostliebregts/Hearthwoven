// 0.8.2 (Joost on a fellow's book: "why no feed? I want that in", "same for last fight"): a fellow's Battle > Feed and Last fight, read from the
// copy the sample world sends (Snapshot and back), say what the sender's own pages say about the same fights, in the fellow's words: their name
// where the sender reads "you", "Rowan (you)" for the one looking, and "another player" for whoever does not share. A copy from before 0.8.2
// says why there is no feed; a fellow's Session counts from your session's start, as their other pages do (B33).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FellowFeedTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var (owns, copies) = SampleWorld.Make(now);
        var own = owns[SampleWorld.Edda]; var copy = copies[SampleWorld.Edda];
        copy.Fellows = owns.Where(kv => kv.Key != SampleWorld.Edda).Select(kv => kv.Key == SampleWorld.Rowan ? kv.Value : copies[kv.Key]).ToList();   // as PanelUi wires a fellow's book
        PanelView Page(PanelInput book, string page, Action<PanelState> more = null)
        {
            var s = new PanelState { Chapter = Chapter.Battle, Player = book.IsSelf ? "" : book.PlayerName, Page = { [Chapter.Battle] = page } }; more?.Invoke(s);
            return PanelModel.Build(book, s);
        }
        List<Block> Fights(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "feed").SelectMany(b => b.Items ?? new List<Block>()).Where(f => f.Kind == "fight").ToList();
        string Numbers(Block b) => b.Title + "|" + b.Text + "|" + b.Value + "|" + b.Value2 + "|" + b.Tone;

        // ---------- the feed: the same fights, foes, times and damage as on Edda's own PC; the words are hers to Rowan ----------
        var mine = Fights(Page(own, PanelModel.FeedPage)); var theirs = Fights(Page(copy, PanelModel.FeedPage));
        var sameNumbers = mine.Select(Numbers).SequenceEqual(theirs.Select(Numbers)) &&
                          mine.SelectMany(f => f.Items.Where(e => e.Kind == "entry")).Select(Numbers).SequenceEqual(theirs.SelectMany(f => f.Items.Where(e => e.Kind == "entry")).Select(Numbers));
        var ownNotes = mine.Select(f => f.Note).Concat(mine.SelectMany(f => f.Items.Where(e => e.Kind == "entry")).Select(e => e.Note)).ToList();
        var copyNotes = theirs.Select(f => f.Note).Concat(theirs.SelectMany(f => f.Items.Where(e => e.Kind == "entry")).Select(e => e.Note)).ToList();
        string Fellow(string ownWords) => ownWords?.Replace("defeated by you", "defeated by Edda").Replace("Rowan and Ylva", "Rowan (you) and another player").Replace("with Rowan", "with Rowan (you)").Replace("(you) (you)", "(you)");
        Check(mine.Count >= 2 && sameNumbers && ownNotes.Contains("with Rowan and Ylva") && copyNotes.Contains("with Rowan (you) and another player") &&
              ownNotes.Select(Fellow).SequenceEqual(copyNotes) && !PanelModel.AllText(Page(copy, PanelModel.FeedPage)).Any(t => t.Contains("Ylva")),
              "FF feed: Edda's feed on Rowan's PC has her " + mine.Count + " fights with the same foes, times and damage as her own; \"defeated by you\" reads \"defeated by Edda\", " +
              "\"with Rowan and Ylva\" reads \"" + copyNotes.FirstOrDefault(n => n != null && n.Contains("another")) + "\": Ylva does not share, so her name never reaches Rowan");

        // ---------- the last fight: Edda's part, Rowan's from his own log, Ylva counted, never named ----------
        var last = Page(copy, PanelModel.LastFightPage); var lastOwn = Page(own, PanelModel.LastFightPage);
        var rows = PanelModel.Content(last).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>();
        var near = PanelModel.AboutText(last);
        var foes = PanelModel.Content(last).FirstOrDefault(b => b.Kind == "fightfoes"); var foesOwn = PanelModel.Content(lastOwn).FirstOrDefault(b => b.Kind == "fightfoes");
        Check(rows.Select(r => r.Title).SequenceEqual(new[] { "Edda", "Rowan (you)" }) && rows[0].Note == "Edda's PC" && rows[1].Note.StartsWith("near Edda ") &&
              near.Contains("Near Edda in this fight: Rowan (you) and another player. Another player does not share, so is not in the numbers.") && !near.Contains("Ylva") &&
              foes?.Title == "Foes Edda fought" && foes.Items.Where(i => i.Kind == "foe").Select(Numbers).SequenceEqual(foesOwn.Items.Where(i => i.Kind == "foe").Select(Numbers)),
              "FF last fight: Edda's last fight on Rowan's PC: her part and Rowan's (from his own log, \"" + rows.ElementAtOrDefault(1)?.Note + "\"), her foes as on her PC; " + near.Split('\n').FirstOrDefault(l => l.StartsWith("Near ")));

        // ---------- a copy from before 0.8.2: no feed, and the reason ----------
        var older = copy.ShallowCopy(); older.SharedFeed = null;
        var oldFeed = PanelModel.Content(Page(older, PanelModel.FeedPage)).FirstOrDefault(b => b.Kind == "empty"); var oldLast = PanelModel.Content(Page(older, PanelModel.LastFightPage)).FirstOrDefault(b => b.Kind == "empty");
        bool Greyed(PanelInput book) => Page(book, PanelModel.FeedPage).List.Where(l => l.Id == PanelModel.FeedPage || l.Id == PanelModel.LastFightPage).All(l => l.Disabled);
        bool Open(PanelInput book) => Page(book, PanelModel.FeedPage).List.Where(l => l.Id == PanelModel.FeedPage || l.Id == PanelModel.LastFightPage).All(l => !l.Disabled);
        Check(oldFeed?.Title == "Edda's feed is not shared" && oldFeed.Text == PanelModel.FeedNotShared(older) && oldLast?.Title == "Edda's last fight is not shared" && oldLast.Text == PanelModel.FeedNotShared(older) &&
              Greyed(older) && Open(copy),
              "FF older copy: Feed and Last fight greyed in the list, their pages say \"" + oldFeed?.Title + "\": " + oldFeed?.Text + " (a copy that shares its feed: both open)");

        // ---------- B33: a fellow's Session counts from your session's start; the short windows are their own times ----------
        var keep = copy.ViewerSessionStartUtc;
        try
        {
            var all = PanelModel.BattleFeed(copy); var newest = all[0]; var oldest = all[all.Count - 1];
            copy.ViewerSessionStartUtc = oldest.EndUtc.AddSeconds(30);
            var session = Fights(Page(copy, PanelModel.FeedPage, s => { s.Window = TimeWindow.Session; s.WindowPicked = true; }));
            copy.ViewerSessionStartUtc = newest.EndUtc.AddMinutes(5);
            var none = PanelModel.Content(Page(copy, PanelModel.FeedPage, s => { s.Window = TimeWindow.Session; s.WindowPicked = true; })).FirstOrDefault(b => b.Kind == "empty");
            var hours = Fights(Page(copy, PanelModel.FeedPage, s => { s.Window = TimeWindow.LastThreeHours; s.WindowPicked = true; }));
            Check(session.Count == all.Count - 1 && session.All(f => f.Id != oldest.Number.ToString()) && none != null && none.Title.StartsWith("No fight since ") && none.Text == PanelModel.FeedBeforeYouLine(copy) &&
                  hours.Count == all.Count(f => f.EndUtc >= copy.NowUtc.AddHours(-3)) && hours.Count > 0,
                  "FF Session: Edda's Session holds her fights since Rowan's session began (" + session.Count + " of " + all.Count + "); none since then says \"" + none?.Title + "\" and how to see them (3 h: " + hours.Count + ")");
        }
        finally { copy.ViewerSessionStartUtc = keep; }

        // ---------- 0.8.6 (a friend's older feed, work/hearthwoven-0.8.6/DESIGN-feed-blocks.md): Edda's earlier sessions from the server ----------
        // Her server lists two earlier sessions: Thursday's (150 foes, fetched) and Tuesday's (120 foes, not here yet). Her days and All open; her copy's fights come first,
        // then Thursday's whole fights under a line with its day and span, and the count line names Tuesday's as a session on the server, never as foes shown.
        var older6 = copy.ShallowCopy();
        var thursday = SampleSessions.Give(older6, now, 150); var tuesday = older6.BlockRows.Last();
        string Words(BlockRow r) => PanelModel.SessionWords(older6, new KeptSession { FromUtc = r.FromUtc, ToUtc = r.ToUtc });
        var copyFoes = PanelModel.BattleFeed(older6).Sum(f => f.Foes);
        PanelView AllAt(PanelInput book, int more, Action<PanelState> also = null) => Page(book, PanelModel.FeedPage, s => { s.Window = TimeWindow.SinceInstall; s.WindowPicked = true; s.FeedMore = more; also?.Invoke(s); });
        List<Block> Lines(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == PanelModel.FeedLineKind).ToList();
        int Foes(PanelView v) => Fights(v).SelectMany(f => f.Items).Count(e => e.Kind == "entry");
        string Last(PanelView v) => Lines(v).LastOrDefault()?.Text;

        // FBP windows: without the server's list the days and All stay greyed with their reasons; with it they open; Session never draws a block
        var plain6 = Page(copy, PanelModel.FeedPage).Windows.Where(w => w.Id == nameof(TimeWindow.Today) || w.Id == nameof(TimeWindow.SinceInstall)).ToList();
        var open6 = Page(older6, PanelModel.FeedPage).Windows;
        var sessionPage = Page(older6, PanelModel.FeedPage, st => { st.Window = TimeWindow.Session; st.WindowPicked = true; });
        Check(plain6.Count == 2 && plain6.All(w => w.Disabled) && plain6[0].Why == PanelModel.FeedDaysWhy && plain6[1].Why == PanelModel.FeedAllWhy &&
              open6.Where(w => w.Id == nameof(TimeWindow.Today) || w.Id == nameof(TimeWindow.SevenDays) || w.Id == nameof(TimeWindow.ThirtyDays) || w.Id == nameof(TimeWindow.SinceInstall)).All(w => !w.Disabled) &&
              Fights(sessionPage).Count == PanelModel.BattleFeed(older6).Count && !Lines(sessionPage).Any(l => l.Tone == "session") &&
              PanelModel.AllText(sessionPage).Any(t => t.Contains("Choose 30 days or All for Edda's earlier sessions.")) && PanelModel.AllText(Page(older6, PanelModel.FeedPage)).Any(t => t.Contains("come from the server one at a time")),
              "FBP windows: without the server's list Edda's days and All stay greyed with their reasons; with it they open; her Session still shows her running session only and says where her earlier ones are");

        // FBP pages: her copy first (" + copyFoes + " foes), then Thursday's whole fights under its own line; the count line names Tuesday's as a session on the server; Tuesday is
        // asked for once fewer than a page of held foes is left, and not before
        int firstSession = -1, firstFetch = -1, allDrawn = -1;
        for (int m = 0; m <= 12 && (firstSession < 0 || firstFetch < 0 || allDrawn < 0); m++)
        {
            var v = AllAt(older6, m);
            if (firstSession < 0 && Lines(v).Any(l => l.Tone == "session" && l.Text == Words(thursday.Row))) firstSession = m;
            if (firstFetch < 0 && v.FetchBlock != null) firstFetch = m;
            if (allDrawn < 0 && PanelModel.FeedMoreLine(v)?.Title == PanelModel.ShowOlderWords) allDrawn = m;
        }
        var page0 = AllAt(older6, 0); var pageEnd = AllAt(older6, allDrawn);
        var fetchAt = AllAt(older6, firstFetch); var beforeFetch = firstFetch > 0 ? AllAt(older6, firstFetch - 1) : null;
        var endLine = Lines(pageEnd).LastOrDefault(); var fightsEnd = Fights(pageEnd);
        Check(firstSession >= 0 && firstFetch >= 0 && allDrawn >= firstFetch && fetchAt.FetchBlock == tuesday.Id && (beforeFetch == null || beforeFetch.FetchBlock == null) && AllAt(older6, 0).FetchBlock == (copyFoes + thursday.Foes - Foes(page0) < PanelModel.FeedTop ? tuesday.Id : null) &&
              Foes(pageEnd) == copyFoes + thursday.Foes && endLine.Tone == PanelModel.FeedMoreTone && endLine.NoteLink == PanelModel.FeedMoreTarget && endLine.Text.StartsWith("Shown: all ") && endLine.Text.Contains(" in this window.") &&
              endLine.Text.EndsWith("Not shown: 1 earlier session on the server (120 foes).") && Lines(pageEnd).Count(l => l.Tone == "session") == 1 &&
              fightsEnd.Count > PanelModel.BattleFeed(older6).Count && !PanelModel.AllText(pageEnd).Any(t => t.Contains("Ylva")),
              "FBP pages: Edda's All draws her copy's " + copyFoes + " foes, then Thursday's (" + thursday.Foes + ") under \"" + Words(thursday.Row) + "\" from press " + firstSession + "; Tuesday's session is asked for from press " + firstFetch +
              " (a page ahead) and the count line says \"Not shown: 1 earlier session on the server (120 foes).\" when every foe held here is drawn (press " + allDrawn + ")");

        // FBP count: a filter makes the session's foes an upper bound ("up to"); the foes on this PC are counted exactly
        var filtered = AllAt(older6, 12, st => { st.Facets[PanelModel.FeedFilter + "|foe"] = new List<string> { thursday.Feed.Fights[0].Entries[0].Kind }; st.OpenFilters.Add(PanelModel.FeedFilter); });
        var filteredNote = Lines(filtered).Select(l => l.Text).LastOrDefault(t => t != null && t.Contains("Not shown")) ?? "";
        Check(filteredNote.Contains("Not shown: ") && filteredNote.Contains(" earlier session on the server (up to 120 foes).") && filteredNote.Contains(" that match."),
              "FBP count: with a foe filter on the sessions on the server are \"up to\" (\"" + filteredNote + "\"), the foes shown are the ones on this PC");

        // FBP states: Tuesday on its way: no button, its words, and presses that came before it did not pile up; failed: Try again
        var loading = older6.ShallowCopy(); loading.BlockStates = new Dictionary<string, string> { [tuesday.Id] = "loading" }; loading.BlocksStamp = "loading";
        var failedBook = older6.ShallowCopy(); failedBook.BlockStates = new Dictionary<string, string> { [tuesday.Id] = "failed" }; failedBook.BlocksStamp = "failed";
        var pressed = new PanelState { Chapter = Chapter.Battle, Player = "Edda", Window = TimeWindow.SinceInstall, WindowPicked = true, FeedMore = 25, Page = { [Chapter.Battle] = PanelModel.FeedPage } };
        var loadingView = PanelModel.Build(loading, pressed);
        var failedView = AllAt(failedBook, allDrawn);
        var failedLine = Lines(failedView).LastOrDefault();
        var settled = new PanelState { Chapter = Chapter.Battle, Player = "Edda", Window = TimeWindow.SinceInstall, WindowPicked = true, FeedMore = 25, Page = { [Chapter.Battle] = PanelModel.FeedPage } };
        PanelModel.Build(older6, settled);
        // its line counts where it is among her sessions in the window (Thursday is here, Tuesday is the second) and, once the book says how long it has been on its way, the seconds
        var waited = loading.ShallowCopy(); waited.BlockWaited = new Dictionary<string, int> { [tuesday.Id] = 6 };
        var waitedLine = Lines(PanelModel.Build(waited, new PanelState { Chapter = Chapter.Battle, Player = "Edda", Window = TimeWindow.SinceInstall, WindowPicked = true, FeedMore = 25, Page = { [Chapter.Battle] = PanelModel.FeedPage } })).LastOrDefault();
        var getting = "Getting Edda's session of " + Words(tuesday) + " from the server · 2 of 2";
        Check(Lines(loadingView).LastOrDefault()?.Tone == "loading" && Last(loadingView) == getting && Lines(loadingView).Last().Value == tuesday.Id && waitedLine?.Text == getting + " · 6 s" &&
              PanelModel.FetchWaited(Last(loadingView), 6) == waitedLine.Text && PanelModel.FetchWaited(Last(loadingView), -1) == getting && PanelModel.FeedMoreLine(loadingView) == null && loadingView.FetchBlock == null &&
              pressed.FeedMore == allDrawn + 1 && settled.FeedMore == allDrawn + 1 && Lines(loadingView).Any(l => l.Tone == "note" && l.Text.Contains("Not shown: 1 earlier session on the server (120 foes).")) &&
              failedLine?.Tone == PanelModel.FeedMoreTone && failedLine.Title == PanelModel.TryAgainWords && failedLine.NoteLink == PanelModel.FeedFetchTarget && failedLine.Value == tuesday.Id &&
              failedLine.Text == "Edda's session of " + Words(tuesday) + " did not arrive." && failedView.FetchBlock == null && PanelModel.FeedMoreLine(failedView)?.Value == tuesday.Id,
              "FBP states: Tuesday's session on its way reads \"" + Last(loadingView) + "\", \"" + waitedLine?.Text + "\" after 6 s (the book updates only that line's text), with no button and 25 presses held at " + pressed.FeedMore + "; one that did not arrive reads \"" + failedLine?.Text + "\" with \"" + failedLine?.Title + "\" on " + failedLine?.NoteLink);

        // FBP nothing held (0.8.6 live, first real evening): Edda has not fought this session and none of her sessions is on this PC yet. Her 7 days is not empty: it asks the server
        // for her newest session in the window and says it is coming, never "Choose a longer window, or Session" (her Session is empty too); one that did not arrive offers Try again;
        // a window her sessions do not reach names the window that does
        var quiet = copy.ShallowCopy(); quiet.SharedFeed = new FeedShare();
        var near6 = SampleSessions.Give(quiet, now, 150); quiet.BlockSessions = new Dictionary<string, KeptSession>(); quiet.BlocksStamp = "quiet";
        PanelView Window(PanelInput book, TimeWindow w) => Page(book, PanelModel.FeedPage, s => { s.Window = w; s.WindowPicked = true; });
        var week = Window(quiet, TimeWindow.SevenDays);
        var quietFailed = quiet.ShallowCopy(); quietFailed.BlockStates = new Dictionary<string, string> { [near6.Id] = "failed" }; quietFailed.BlocksStamp = "quiet-failed";
        var weekFailed = Window(quietFailed, TimeWindow.SevenDays); var weekFailedLine = Lines(weekFailed).LastOrDefault();
        var today = Window(quiet, TimeWindow.Today); var todayEmpty = PanelModel.Content(today).FirstOrDefault(b => b.Kind == "empty");
        bool SaysSession(PanelView v) => PanelModel.AllText(v).Any(t => t.Contains("Session for every fight"));
                // All holds both of her sessions, neither here: the first one is "1 of 2"; 7 days holds one, so no count
        var allQuiet = Window(quiet, TimeWindow.SinceInstall);
        var quietWaited = quiet.ShallowCopy(); quietWaited.BlockStates = new Dictionary<string, string> { [near6.Id] = "loading" }; quietWaited.BlockWaited = new Dictionary<string, int> { [near6.Id] = 12 }; quietWaited.BlocksStamp = "quiet-waited";
        Check(Last(allQuiet) == "Getting Edda's session of " + Words(near6.Row) + " from the server · 1 of 2" && allQuiet.FetchBlock == near6.Id &&
              Last(Window(quietWaited, TimeWindow.SinceInstall)) == "Getting Edda's session of " + Words(near6.Row) + " from the server · 1 of 2 · 12 s" &&
              Last(Window(quietWaited, TimeWindow.SevenDays)) == "Getting Edda's session of " + Words(near6.Row) + " from the server · 12 s",
              "FBP progress: Edda's All with both sessions on the server reads \"" + Last(allQuiet) + "\"; 12 s on: \"" + Last(Window(quietWaited, TimeWindow.SinceInstall)) + "\"; her 7 days (one session) has no count");
        Check(week.ShownWindow == TimeWindow.SevenDays && week.FetchBlock == near6.Id && !SaysSession(week) && Last(week) == "Getting Edda's session of " + Words(near6.Row) + " from the server" &&
              PanelModel.AllText(week).Any(t => t.Contains("1 earlier session on the server (150 foes)")) &&
              weekFailedLine?.Title == PanelModel.TryAgainWords && weekFailedLine.NoteLink == PanelModel.FeedFetchTarget && weekFailedLine.Value == near6.Id && weekFailed.FetchBlock == null && !SaysSession(weekFailed) &&
              todayEmpty != null && !SaysSession(today) && todayEmpty.Text == "Choose 7 days for Edda's earlier sessions." && today.FetchBlock == null,
              "FBP nothing held: Edda's 7 days with no fight this session asks for " + week.FetchBlock + " and reads \"" + Last(week) + "\"; failed: \"" + weekFailedLine?.Title + "\" on " + weekFailedLine?.NoteLink +
              "; Today reads \"" + todayEmpty?.Title + ": " + todayEmpty?.Text + "\"");
        return fails;
    }
}
