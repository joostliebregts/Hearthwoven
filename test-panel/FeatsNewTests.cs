// 0.9 New feats (work/hearthwoven-0.9/DESIGN.md section 2, TRACK-feats-new.md): the New mark on an earned tier not opened yet, on Earned and on
// Together; new ones first, newest first; opening a card (a click, A/D) marks it seen; the intro line's count; the tab dot while any is left, read
// from the per-tier record; the first 0.9 start from the old count (FeatsLedger.MigrateSeen). 0.9.2 (Joost: "show me the full list before I select
// anything. Only show when I click to select something"): a page opens with no card chosen, never on the newest New feat by itself, so every New
// mark waits until its own card is chosen. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FeatsNewTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        var (full, books) = FeatsTests.World();
        PanelState At(string page) => new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = page } };
        // one build as PanelUi.DrawPage does it: the build, then the card it shows opened (PanelModel.FeatsSeen)
        PanelView Draw(PanelInput i, PanelState s) { var v = PanelModel.Build(i, s); PanelModel.FeatsSeen(i, s); return v; }
        List<Block> Cards(PanelView v) => PanelModel.VisibleFeats(v);
        string Intro(PanelView v) => PanelModel.FeatsIntro(v) ?? "";   // the line rides in the strip (FeatLook.cs)
        string Read(PanelView v) => PanelModel.FeatSearchBlock(v)?.Value ?? "";   // 0.9.5: the count rides in Mark read ("Mark 2 read", FeatsReadAll.cs)
        bool TabDot(PanelInput i) => PanelModel.Build(i, new PanelState { Chapter = Chapter.Deeds }).Chapters.Single(c => c.Id == "Feats").Dot;

        // ---------- the record is not there: nothing is New, the 0.8.6 pages as they were ----------
        {
            var v = PanelModel.Build(full, At(PanelModel.FeatsPageId));
            Check(full.Feats.SeenTiers == null && !Cards(v).Any(c => c.New) && !Intro(v).Contains(" new") && !PanelModel.ToJson(v).Contains("\"new\":1"),
                  "new: a ledger without the per-tier record (SeenTiers null) marks no card New and counts none");
        }

        // ---------- order, no card opened by itself, the count, a click and A/D ----------
        {
            var three = FeatsSample.WithNew(full, false, 3);
            var expect = PanelModel.NewFeats(three, false);
            var tiers = three.Feats.UnseenTiers(PanelModel.IsGroupFeat, false);   // before the page opens one
            var s = At(PanelModel.FeatsPageId);
            var v = Draw(three, s);
            var cards = Cards(v);
            Check(expect.Count >= 3 && cards.Take(expect.Count).Select(c => c.Id).SequenceEqual(expect),
                  "order: the New feats come first, newest first (" + string.Join(", ", expect) + "), then the rest");
            Check(tiers.Count > 0 && expect[0] == tiers[0].id, "order: the first is the feat of the newest unopened tier");
            Check(!cards.Any(c => c.Selected) && s.FeatSel == "" && cards.Take(expect.Count).All(c => c.New) && cards.Skip(expect.Count).All(c => !c.New) && !PanelModel.Content(v).Any(b => b.Kind == "featdetail"),
                  "open (0.9.2): the page opens with no card chosen, every New card keeps its mark (none is opened by the page itself)");
            Check(Intro(v).EndsWith(" earned.", StringComparison.Ordinal) && Read(v) == PanelModel.FeatsReadChip(expect.Count),
                  "count: how many New cards wait, in the strip's Mark read (0.9.5: the line's count moved into it; \"" + Read(v) + "\")");
            Check(expect.All(id => !three.Feats.IsSeen(id, three.Feats.Tier(id))), "open: after the build nothing is seen: no card was chosen");
            Check(PanelModel.ToJson(v).Contains("\"new\":1"), "mirror: a New card is written with \"new\":1 for the preview");

            // the same visit drawn again: nothing jumps
            var again = Draw(three, s);
            Check(Cards(again).Take(expect.Count).Select(c => c.Id).SequenceEqual(expect) && Cards(again).Count(c => c.New) == expect.Count,
                  "visit: drawn again, the order holds and the count stays");

            // a hover chooses nothing: the card keeps its New mark and stays unopened
            var hovered = PanelModel.FeatPointer(s, expect[1], click: false);
            var swept = Draw(three, s);
            Check(!hovered && !three.Feats.IsSeen(expect[1], three.Feats.Tier(expect[1])) && Cards(swept).Single(c => c.Id == expect[1]).New && !Cards(swept).Any(c => c.Selected),
                  "hover: a hovered New card keeps its mark and stays unopened (a mouse sweep clears nothing)");
            // a click opens it: the detail area, its tiers seen, its mark gone, the count down by one
            PanelModel.OpenFeat(s, expect[0]);   // what FeatsUi.FeatClick does
            var clicked = Draw(three, s);
            Check(three.Feats.IsSeen(expect[0], three.Feats.Tier(expect[0])) && !Cards(clicked).Single(c => c.Id == expect[0]).New && Cards(clicked).Single(c => c.Selected).Id == expect[0] &&
                  Cards(clicked).Count(c => c.New) == expect.Count - 1 && Read(clicked) == PanelModel.FeatsReadChip(expect.Count - 1),
                  "click: a clicked New card is chosen and opened: its tiers seen, its mark gone, the count down by one (\"" + Read(clicked) + "\")");
            Check(Cards(clicked).Take(expect.Count).Select(c => c.Id).SequenceEqual(expect), "visit: the opened card stays where it was (the visit's order holds)");
            // A/D to the next card opens it: its mark goes, the count goes down by one
            PanelModel.StepFeat(s, clicked, 1);
            var stepped = Draw(three, s);
            Check(s.FeatSel == expect[1] && !Cards(stepped)[1].New && Cards(stepped).Count(c => c.New) == expect.Count - 2 && three.Feats.IsSeen(expect[1], three.Feats.Tier(expect[1])),
                  "open: A/D to the next New card opens it: its mark goes and the count drops");
            Check(expect.Count - 2 == 0 ? Read(stepped) == "" && !Intro(stepped).Contains(" new") : Read(stepped) == PanelModel.FeatsReadChip(expect.Count - 2),
                  "count: Mark read's count follows (\"" + Read(stepped) + "\")");
            // closing the chosen card (a click on it, Backspace) opens nothing more
            Check(PanelModel.CloseFeat(s, stepped) && !Cards(Draw(three, s)).Any(c => c.Selected) && Cards(Draw(three, s)).Count(c => c.New) == expect.Count - 2,
                  "close: the chosen card closed, the rest keep their marks");

            // the tab dot stays while any New is left, and goes with the last
            var left = PanelModel.NewFeats(three, false);
            Check(three.Feats.UnseenOf(PanelModel.IsGroupFeat, false) == three.Feats.UnseenTiers(PanelModel.IsGroupFeat, false).Count && left.Count <= three.Feats.UnseenOf(PanelModel.IsGroupFeat, false),
                  "dot: the old count (UnseenOf) is read from the per-tier record");
            Check(left.Count > 0 == TabDot(three), "dot: the Feats tab keeps its gold dot while a New feat is left (" + left.Count + " left)");
            var unsung = PanelModel.Build(three, At(PanelModel.FeatsUnsungId));
            Check(left.Count > 0 == unsung.List.Single(l => l.Id == PanelModel.FeatsPageId).Dot, "dot: from Unsung, the Earned entry keeps its dot while one is left");

            // a new visit: the New ones left first, none chosen
            PanelModel.EndFeatsVisit(s); s.FeatSel = "";
            var back = Draw(three, s);
            Check(left.Count == 0 || (Cards(back)[0].Id == left[0] && Cards(back)[0].New && !Cards(back).Any(c => c.Selected)),
                  "visit: a new visit puts the New feats left first, with their marks, none chosen");
            foreach (var c in Cards(back)) PanelModel.FeatOpened(three.Feats, c.Id, false);   // every card opened
            Check(three.Feats.UnseenOf(PanelModel.IsGroupFeat, false) == 0 && !TabDot(three) && three.Feats.Seen == three.Feats.TiersEarnedOf(PanelModel.IsGroupFeat, false),
                  "dot: every card opened, the tab's dot is gone and the old count (Seen) follows the record");
            Check(!PanelModel.FeatOpened(three.Feats, "ironforge", false) && !PanelModel.FeatOpened(three.Feats, "nosuchfeat"),
                  "open: a feat of the other side, or one the table does not have, is not opened from this page");
        }

        // ---------- a tier earned while the page is open: it comes first, the opened card stays where it is ----------
        {
            var one = FeatsSample.WithNew(full, false, 1);
            var s = At(PanelModel.FeatsPageId);
            var v = Draw(one, s);
            PanelModel.OpenFeat(s, Cards(v)[0].Id); Draw(one, s);   // a click opens the New one
            var opened = s.FeatSel;
            var later = PanelModel.FeatDefs.First(d => !d.Group && one.Feats.Tier(d.Id) > 0 && d.Tiers.Length > one.Feats.Tier(d.Id) && d.Id != opened);
            one.Feats.Earn(later.Id, one.Feats.Tier(later.Id) + 1, new FeatMoment { Utc = FeatsTests.Now.AddMinutes(5), Biome = "Meadows" });
            var after = Draw(one, s);
            Check(Cards(after)[0].Id == later.Id && Cards(after)[0].New && s.FeatSel == opened && Cards(after).Single(c => c.Selected).Id == opened,
                  "visit: " + later.Name + " earned during the visit comes first with its mark; the card opened stays chosen");
        }

        // ---------- Together: the group's tiers, New first ----------
        {
            var group = FeatsSample.WithNew(full, true, 3);
            var expect = PanelModel.NewFeats(group, true);
            var s = At(PanelModel.FeatsTogetherId);
            var v = Draw(group, s);
            Check(expect.Count >= 3 && Cards(v).Take(expect.Count).Select(c => c.Id).SequenceEqual(expect) && !Cards(v).Any(c => c.Selected) && Cards(v).Count(c => c.New) == expect.Count,
                  "together: the group's New feats first, newest first, each with its mark, none chosen (" + string.Join(", ", expect) + ")");
            Check(Read(v) == PanelModel.FeatsReadChip(expect.Count) && !Intro(v).Contains(" new feat"), "together: Mark read counts them (\"" + Read(v) + "\"; the line \"" + Intro(v) + "\")");
            var earned = PanelModel.Build(group, At(PanelModel.FeatsPageId));
            Check(!Cards(earned).Any(c => c.New) && earned.List.Single(l => l.Id == PanelModel.FeatsTogetherId).Dot,
                  "together: a group tier is never New on Earned; Together keeps its dot");
            Check(expect.All(id => !group.Feats.IsSeen(id, group.Feats.Tier(id))), "together: opening the page opens none of them");
        }

        // ---------- 0.9.1 Together parity (Joost: "if you also get new feats on the together page, we need to have that same indicator there"): every
        // part of Earned's New treatment on Together: the card's mark, the Together entry's dot, the tab's dot, cleared by a click, A/D, and a group tier
        // earned during the visit coming first ----------
        {
            var group = FeatsSample.WithNew(full, true, 3);
            var expect = PanelModel.NewFeats(group, true);
            var s = At(PanelModel.FeatsTogetherId);
            var v = Draw(group, s);
            var grid = PanelModel.Content(v).First(b => b.Kind == "feats");
            Check(grid.Look == PanelModel.FeatLookOn && expect.Count >= 3 && Cards(v).Take(expect.Count).All(c => c.New) && PanelModel.ToJson(v).Contains("\"new\":1"),
                  "together: the New cards carry the mark on the look's cards, and the mirror writes it");
            Check(PanelModel.PlateOf(v).Text == null && PanelModel.FeatsIntro(v).Length <= PanelModel.FeatsIntroMax && Read(v) == PanelModel.FeatsReadChip(expect.Count),
                  "together: Mark read counts them and the line rides in the strip as on Earned (no row of its own; " + PanelModel.FeatsIntro(v).Length + " letters)");
            Check(TabDot(group) && group.Feats.UnseenOf(PanelModel.IsGroupFeat, false) == 0, "together: a group tier alone puts the gold dot on the Feats tab");
            var unsung = PanelModel.Build(group, At(PanelModel.FeatsUnsungId));
            Check(unsung.List.Single(l => l.Id == PanelModel.FeatsTogetherId).Dot && !unsung.List.Single(l => l.Id == PanelModel.FeatsPageId).Dot,
                  "together: from Unsung the Together entry has the dot, Earned none");
            Check(!v.List.Single(l => l.Id == PanelModel.FeatsTogetherId).Dot, "together: the page you are on carries no dot of its own");
            PanelModel.OpenFeat(s, expect[2]);   // a click (FeatsUi.FeatClick)
            var clicked = Draw(group, s);
            PanelModel.OpenFeat(s, expect[0]);
            clicked = Draw(group, s);
            Check(group.Feats.IsSeen(expect[2], group.Feats.Tier(expect[2])) && group.Feats.IsSeen(expect[0], group.Feats.Tier(expect[0])) && !Cards(clicked).Single(c => c.Id == expect[2]).New &&
                  Cards(clicked).Count(c => c.New) == expect.Count - 2,
                  "together: a click opens a New card: its tiers seen, its mark gone");
            PanelModel.StepFeat(s, clicked, 1);   // A/D from the first onto the last New one
            var stepped = Draw(group, s);
            Check(s.FeatSel == expect[1] && group.Feats.IsSeen(expect[1], group.Feats.Tier(expect[1])) && !Cards(stepped).Any(c => c.New) && !TabDot(group),
                  "together: A/D opens the last New card; no mark and no tab dot are left");
            var later = PanelModel.FeatDefs.FirstOrDefault(d => d.Group && group.Feats.Tier(d.Id) > 0 && d.Tiers.Length > group.Feats.Tier(d.Id) && d.Id != s.FeatSel);
            if (later == null) Check(false, "together: a group feat with a tier left to earn");
            else
            {
                group.Feats.Earn(later.Id, group.Feats.Tier(later.Id) + 1, new FeatMoment { Utc = FeatsTests.Now.AddMinutes(5), Noticed = true });   // as FeatsTracker notes a group tier
                var after = Draw(group, s);
                Check(Cards(after)[0].Id == later.Id && Cards(after)[0].New && Cards(after).Single(c => c.Selected).Id == s.FeatSel,
                      "together: " + later.Name + " earned during the visit comes first with its mark; the chosen card stays");
                var elsewhere = PanelModel.Build(group, new PanelState { Chapter = Chapter.Battle });
                Check(elsewhere.Chapters.Single(c => c.Id == "Feats").Dot, "together: that new group tier lights the Feats tab from another chapter");
            }
        }

        // ---------- a fellow's book: never New ----------
        {
            var tor = books["Tor"].ShallowCopy();
            tor.Feats = FeatsSample.WithNew(books["Tor"], false, 3).Feats;   // even a copy that carried a record
            var s = new PanelState { Chapter = Chapter.Feats, Player = "Tor", Page = { [Chapter.Feats] = PanelModel.FeatsPageId } };
            var v = Draw(tor, s);
            Check(!tor.IsSelf && !Cards(v).Any(c => c.New) && !Intro(v).Contains(" new") && s.FeatsVisit == null && tor.Feats.UnseenTiers(PanelModel.IsGroupFeat, false).Count == Math.Min(3, tor.Feats.TiersEarnedOf(PanelModel.IsGroupFeat, false)),
                  "fellow: Tor's book shows no New mark and no count, and opening it marks nothing");
        }

        // ---------- the first 0.9 start: the old count says how many, the dates say which ----------
        {
            var now = FeatsTests.Now;
            var l = new FeatsLedger { Primed = true };
            l.Earn("keptfires", 1, new FeatMoment { Utc = now.AddDays(-5), Biome = "Meadows" });
            l.Earn("shieldwall", 1, new FeatMoment { Utc = now.AddDays(-1), Biome = "BlackForest" });
            l.Earn("unbroken", 1, new FeatMoment { Utc = now.AddDays(-3), Biome = "Swamp" });
            l.Earn("waymate", 1, new FeatMoment { Before = true, Utc = now.AddDays(-9) });
            l.Seen = 2;   // 0.8.6 saw two of four: two were not seen
            var countedBefore = l.UnseenOf(PanelModel.IsGroupFeat, false);
            Check(l.MigrateSeen(PanelModel.IsGroupFeat) && countedBefore == 2 && l.UnseenOf(PanelModel.IsGroupFeat, false) == 2,
                  "migration: the old count's 2 unseen become 2 New tiers (the dot reads the same before and after)");
            var self = full.ShallowCopy(); self.Feats = l; self.FeatsSeen = l.SeenTiers;
            Check(PanelModel.NewFeats(self, false).SequenceEqual(new[] { "shieldwall", "unbroken" }), "migration: the two most recently earned dated tiers are New, newest first; Before and older ones seen");
            var s = At(PanelModel.FeatsPageId);
            var v = Draw(self, s);
            Check(Cards(v)[0].Id == "shieldwall" && Cards(v)[0].New && !Cards(v)[0].Selected && Cards(v)[1].Id == "unbroken" && Cards(v)[1].New && Read(v) == PanelModel.FeatsReadChip(2),
                  "migration: the first visit shows Shield Wall then Unbroken first, both with their marks, none chosen (\"" + Read(v) + "\")");
            var nodates = new FeatsLedger { Primed = true };
            nodates.Earn("waymate", 1, new FeatMoment { Before = true });
            nodates.Earn("ferryman", 1, new FeatMoment { Noticed = true, Utc = now });
            nodates.Seen = 0;
            nodates.MigrateSeen(PanelModel.IsGroupFeat);
            Check(nodates.UnseenOf(PanelModel.IsGroupFeat, false) == 0, "migration: no earned dates (before install, noticed): nothing New, and the dot goes");
        }
        return fails;
    }
}
