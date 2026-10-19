// 0.9.5 Mark read (Chapters/FeatsReadAll.cs; Joost, 19 Oct: "we need a read all feature for the feats"): one action in the strip of Earned and Together
// while that page has New cards, "Mark N read", that clears every New mark on both pages at once (the list's dots and the tab's dot with them), kept
// in the ledger as an opened card is (it round-trips through the totals file); gone when nothing is New; its key in the key line only while it is
// offered, and never while the search field has the keys. Called from Program.cs.
using System;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FeatsReadAllTests
{
    /// <summary>A copy of <paramref name="book"/> with <paramref name="own"/> of your own tiers and <paramref name="group"/> of the group's New (FeatsSample.WithNew on both sides).</summary>
    public static PanelInput Both(PanelInput book, int own, int group)
    {
        var copy = FeatsSample.WithNew(book, false, own);
        copy.Feats.SeenTiers.IntersectWith(FeatsSample.WithNew(book, true, group).Feats.SeenTiers);
        return copy;
    }

    /// <summary>A copy with everything New marked read (Mark read pressed), for the preview after it.</summary>
    public static PanelInput Read(PanelInput book) { PanelModel.MarkFeatsRead(book.Feats); return book; }

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        var (full, books) = FeatsTests.World();
        PanelState At(string page) => new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = page } };
        PanelView Draw(PanelInput i, PanelState s) { var v = PanelModel.Build(i, s); PanelModel.FeatsSeen(i, s); return v; }
        string Intro(PanelView v) => PanelModel.FeatsIntro(v) ?? "";
        Block Field(PanelView v) => PanelModel.FeatSearchBlock(v);
        bool TabDot(PanelInput i) => PanelModel.Build(i, new PanelState { Chapter = Chapter.Deeds }).Chapters.Single(c => c.Id == "Feats").Dot;
        bool ListDot(PanelView v, string id) => v.List.Single(l => l.Id == id).Dot;
        string Key(PanelView v) => v.Keys.FirstOrDefault(k => k.EndsWith("] " + PanelModel.FeatsReadWord, StringComparison.Ordinal));

        // ---------- offered: Earned with New cards, the count moved from the line into the action, its key in the key line ----------
        {
            var both = Both(full, 3, 2);
            var own = PanelModel.NewFeats(both, false).Count; var group = PanelModel.NewFeats(both, true).Count;
            var v = Draw(both, At(PanelModel.FeatsPageId));
            Check(own == 3 && group == 2 && PanelModel.FeatsReadOn(v) && Field(v).Link == PanelModel.FeatsReadTarget && Field(v).Value == PanelModel.FeatsReadChip(own + group) && Field(v).Value == "Mark 5 read",
                  "offered: Earned with New cards carries the action, saying what it clears on both pages (\"" + Field(v)?.Value + "\")");
            Check(Intro(v).EndsWith(" earned.", StringComparison.Ordinal) && !Intro(v).Contains(" new"),
                  "offered: the line leaves out its own count, which moved into the action (\"" + Intro(v) + "\")");
            Check(Key(v) == "[R] " + PanelModel.FeatsReadWord && v.Keys.IndexOf(Key(v)) == v.Keys.IndexOf("[A/D] Feat") + 1,
                  "offered: its key is in the key line, after [A/D] Feat (" + string.Join(" ", v.Keys) + ")");
            Check(PanelModel.ToJson(v).Contains("\"link\":\"" + PanelModel.FeatsReadTarget + "\""), "mirror: the action is written for the preview");
            var t = Draw(both, At(PanelModel.FeatsTogetherId));
            Check(PanelModel.FeatsReadOn(t) && Field(t).Value == "Mark 5 read" && !Intro(t).Contains(" new feat"),
                  "offered: Together with New cards carries it too, the same count, its line without \"new feats\" (\"" + Intro(t) + "\")");
        }

        // ---------- not offered: Unsung, Titles, a page with nothing New, a fellow's book, a ledger without the record, a search on ----------
        {
            var both = Both(full, 3, 0);
            Check(!PanelModel.FeatsReadOn(Draw(both, At(PanelModel.FeatsUnsungId))) && !PanelModel.FeatsReadOn(Draw(both, At(PanelModel.TitlesPageId))),
                  "hidden: never on Unsung or Titles");
            var t = Draw(both, At(PanelModel.FeatsTogetherId));
            Check(!PanelModel.FeatsReadOn(t) && Key(t) == null, "hidden: Together with nothing New of its own offers nothing (Earned's New stay Earned's to clear)");
            var none = Draw(FeatsSample.WithNew(full, false, 0), At(PanelModel.FeatsPageId));
            Check(!PanelModel.FeatsReadOn(none) && Key(none) == null && string.IsNullOrEmpty(Field(none).Link), "hidden: nothing New, no action and no key");
            Check(!PanelModel.FeatsReadOn(Draw(full, At(PanelModel.FeatsPageId))), "hidden: a ledger without the per-tier record (nothing is New)");
            var tor = books["Tor"].ShallowCopy(); tor.Feats = FeatsSample.WithNew(books["Tor"], false, 3).Feats;
            Check(!PanelModel.FeatsReadOn(Draw(tor, new PanelState { Chapter = Chapter.Feats, Player = "Tor", Page = { [Chapter.Feats] = PanelModel.FeatsPageId } })), "hidden: a fellow's book");
            var s = At(PanelModel.FeatsPageId); PanelModel.SetFeatQuery(s, "the");
            var searching = Draw(both, s);
            Check(!PanelModel.FeatsReadOn(searching) && Key(searching) == null, "hidden: while a search is on (the line says the matches)");
        }

        // ---------- pressed: every New mark on both pages goes, the counts, the list's dots and the tab's dot with them; the action goes ----------
        {
            var both = Both(full, 3, 2);
            var s = At(PanelModel.FeatsPageId);
            var before = Draw(both, s);
            Check(TabDot(both) && ListDot(before, PanelModel.FeatsTogetherId) && PanelModel.VisibleFeats(before).Count(c => c.New) == 3, "before: three New cards, Together's dot, the tab's dot");
            Check(PanelModel.MarkFeatsRead(both.Feats), "pressed: Mark read reports that something was New");
            var after = Draw(both, s);
            Check(!PanelModel.VisibleFeats(after).Any(c => c.New) && both.Feats.UnseenOf(PanelModel.IsGroupFeat, false) == 0 && both.Feats.UnseenOf(PanelModel.IsGroupFeat, true) == 0,
                  "pressed: no New card is left on Earned, and none of either side is unseen");
            var together = Draw(both, At(PanelModel.FeatsTogetherId));
            Check(!PanelModel.VisibleFeats(together).Any(c => c.New) && !ListDot(after, PanelModel.FeatsTogetherId) && !ListDot(together, PanelModel.FeatsPageId) && !TabDot(both),
                  "pressed: Together's New cards are cleared too; the list's dots and the Feats tab's dot are gone");
            Check(!PanelModel.FeatsReadOn(after) && Key(after) == null && !Intro(after).Contains(" new"), "pressed: the action and its key are gone (it cannot be pressed again)");
            Check(both.Feats.Seen == both.Feats.TiersEarnedOf(PanelModel.IsGroupFeat, false) && both.Feats.SeenGroup == both.Feats.TiersEarnedOf(PanelModel.IsGroupFeat, true),
                  "pressed: the old counts follow (an older Hearthwoven's dot agrees)");
            Check(!PanelModel.MarkFeatsRead(both.Feats) && !PanelModel.MarkFeatsRead(full.Feats) && !PanelModel.MarkFeatsRead(null), "again: nothing left to mark, no record, or no ledger: nothing happens");
        }

        // ---------- kept: the marks stay read after the book is saved and opened again ----------
        {
            var both = Both(full, 3, 2);
            PanelModel.MarkFeatsRead(both.Feats);
            var back = LocalTotals.FromJson(new LocalTotals { PlayerId = 1, Name = "Rowan", Feats = both.Feats }.ToJson(FeatsTests.Now)).Feats;
            var reopened = both.ShallowCopy(); reopened.Feats = back; reopened.FeatsSeen = back.SeenTiers;
            var v = Draw(reopened, At(PanelModel.FeatsPageId));
            Check(back.SeenTiers != null && back.UnseenOf(PanelModel.IsGroupFeat, false) == 0 && back.UnseenOf(PanelModel.IsGroupFeat, true) == 0 && !PanelModel.VisibleFeats(v).Any(c => c.New) && !PanelModel.FeatsReadOn(v) && !TabDot(reopened),
                  "kept: read through the totals file and opened again, nothing is New, no action, no dot");
        }

        // ---------- the key: marks everything read where offered, never while the search field has the keys ----------
        {
            var was = (FeatSearchInput.FocusedLastFrame, FeatSearchInput.FocusedNow);
            try
            {
                var both = Both(full, 3, 2);
                var v = Draw(both, At(PanelModel.FeatsPageId));
                FeatSearchInput.FocusedLastFrame = false; FeatSearchInput.FocusedNow = () => true;   // typing in the search field
                var typing = PanelModel.FeatsReadKey(v, both.Feats);
                Check(!typing && PanelModel.NewFeats(both, false).Count == 3 && PanelModel.NewFeats(both, true).Count == 2, "key: while the search field has the keys, R marks nothing (it types)");
                FeatSearchInput.FocusedNow = () => false;
                var pressed = PanelModel.FeatsReadKey(v, both.Feats);
                Check(pressed && PanelModel.FeatsUnread(both) == 0, "key: with the field left, R marks everything read");
                var none = Draw(both, At(PanelModel.FeatsPageId));
                Check(!PanelModel.FeatsReadKey(none, both.Feats), "key: where nothing is offered, R does nothing");
            }
            finally { FeatSearchInput.FocusedLastFrame = was.Item1; FeatSearchInput.FocusedNow = was.Item2; }
        }
        return fails;
    }
}
