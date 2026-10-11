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
        return fails;
    }
}
