// 0.9 deed feeds (work/hearthwoven-0.9/TRACK-feeds.md; src/Panel/Chapters/DeedFeedModel.cs): a Feed page for every Deeds page with a growth line and one
// mixed feed from Recent, in the Battle feed's own blocks. Here: the Feed chip on every such page (and only there), the page opened from it (Back, the
// page lit), a stretch as an island (its head, its moment lines with the level meter and the count, its counts by kind), the session line and the quiet
// gap, times kept in tens said so, the windows, the filter, Show older, the mixed feed in time order, the not-recorded and empty states, and a friend's
// feed from the server (here, on its way, did not arrive, none listed, an older server). Fictional players only (PanelSampleFeeds.cs).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class DeedFeedTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var world = PanelSample.Full(now);
        var yours = FeedSample.Yours(world);
        PanelView Build(PanelInput inp, PanelState s) => PanelModel.Build(inp, s);
        PanelState Deeds(string page, Action<PanelState> more = null) { var s = new PanelState { Chapter = Chapter.Deeds }; s.Page[Chapter.Deeds] = page; more?.Invoke(s); return s; }
        // a feed opened from its page's chip, as the book does it (so Back shows)
        (PanelView v, PanelState s) Open(PanelInput inp, string page, TimeWindow w, Action<PanelState> more = null, string who = null)
        {
            var s = Deeds(page, x => { x.Window = w; x.WindowPicked = true; x.Player = who; });
            var v0 = Build(inp, s); PanelModel.Visited(s, v0.Active, v0.Page);
            var chip = PanelModel.PlateOf(v0)?.Items?.FirstOrDefault(b => b.Kind == "headlink" && b.Title == PanelModel.DeedFeedChip);
            if (chip != null) PanelModel.Follow(s, chip.Id);
            more?.Invoke(s);
            var v = Build(inp, s); PanelModel.Visited(s, v.Active, v.Page);
            return (v, s);
        }
        List<Block> Islands(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "feed").SelectMany(b => b.Items ?? new List<Block>()).Where(b => b.Kind == "fight").ToList();
        List<string> Rows(PanelView v) => (PanelModel.Content(v).FirstOrDefault(b => b.Kind == "filterbar")?.Items ?? new List<Block>()).Where(b => b.Kind == "facet").Select(b => b.Title).ToList();
        List<Block> Lines(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == PanelModel.FeedLineKind).ToList();

        // ---------- the Feed chip ----------
        var growth = PanelModel.GrowthPages.ToList();
        var chips = growth.Select(p => (p, chip: PanelModel.PlateOf(Build(yours, Deeds(p)))?.Items?.FirstOrDefault(b => b.Kind == "headlink"))).ToList();
        Check(growth.Count == 8 && chips.All(c => c.chip != null && c.chip.Title == "Feed" && c.chip.Id == "Deeds/feed-" + c.p && c.chip.Icon == PanelModel.FeedIcon),
              "chip: every Deeds page with a growth line has the Feed chip first in its heading row (" + string.Join(", ", growth) + ")");
        var recentChip = PanelModel.PlateOf(Build(yours, Deeds(PanelModel.RecentPageId)))?.Items?.FirstOrDefault(b => b.Kind == "headlink");
        Check(recentChip?.Id == "Deeds/" + PanelModel.DeedFeedAllPage, "chip: Recent's Feed chip opens the mixed feed");
        var craftItems = PanelModel.PlateOf(Build(yours, Deeds("crafting")))?.Items?.Where(b => b.Kind == "headlink").Select(b => b.Title).ToList();
        Check(craftItems != null && craftItems.SequenceEqual(new[] { "Feed", PanelModel.SetsChip }), "chip: Crafting's heading row says Feed, then Sets (DESIGN.md section 2)");
        Check(new[] { "overview", "taming", PanelModel.MealsPageId }.All(p => PanelModel.PlateOf(Build(yours, Deeds(p)))?.Items?.Any(b => b.Kind == "headlink" && b.Title == "Feed") != true) &&
              PanelModel.PlateOf(Build(yours, Deeds(PanelModel.RecentPageId, s => s.Everyone = true)))?.Items?.Any(b => b.Kind == "headlink") != true,
              "chip: no Feed chip on the Overview, Taming, Meals or the group's Recent (no growth line, no feed)");
        var eddaBook = FeedSample.Edda(FullDump.Books(world).First(b => b.who == SampleWorld.Edda).book, now);
        Check(PanelModel.PlateOf(Build(eddaBook, Deeds("crafting", s => s.Player = SampleWorld.Edda)))?.Items?.Any(b => b.Kind == "headlink" && b.Id == "Deeds/feed-crafting") == true,
              "chip: a friend's Deeds page has it too (their feed from the server)");

        // ---------- your crafting feed, 7 days ----------
        var (feed, fs) = Open(yours, "crafting", TimeWindow.SevenDays);
        var isl = Islands(feed);
        Check(feed.Page == "feed-crafting" && feed.Back == PanelModel.BackLabel && feed.List.First(l => l.Selected).Id == "crafting" && feed.Heading == "Crafting feed" && feed.HeadingWindow == "last 7 days" && feed.Compare == null,
              "page: opened from the chip, with Back, Crafting lit in the list, \"Crafting feed, last 7 days\", no Compare (a feed adds nothing up)");
        Check(isl.Count == 3 && isl.All(i => i.Title.StartsWith("Crafting at ", StringComparison.Ordinal)) && PanelModel.SwitchOf(feed) != null && PanelModel.SwitchOf(feed).Title == "3 stretches, newest first",
              "page: one island per stretch in the window (this session's and the morning before's), the Log, Cards and Timeline switch over them");
        var first = isl[0]; var lines = first.Items.Where(b => b.Kind == "entry").ToList();
        Check(first.Text == "18 min · 50 made · 2 upgrades" && lines.Count == 5 && lines.Select(e => e.Tone).SequenceEqual(new[] { "upgraded", "upgraded", "made", "made", "made" }),
              "island: \"Crafting at 23:35 · 18 min · 50 made · 2 upgrades\"; a line only for each moment, newest first: the ring for an upgrade, the filled mark for gear made: " + first.Text);
        var up = lines[0]; var mace = lines[2];
        Check(up.Title == "Iron Sword" && up.Note == "upgraded to level 3" && up.Level == 3 && up.Count == 4 && up.Value == "level 3 of 4" && up.Icon == "item:$item_sword_iron" &&
              mace.Note == "made, first recorded" && mace.Value == "×1" && lines[4].Value == "×2",
              "lines: an upgrade carries its level for the four-part meter out of the item's highest (level 3 of 4); gear made its count; a first said on its made line");
        var counts = first.Items.Where(b => b.Kind == "counts").ToList();
        Check(counts.Count == 1 && counts[0].Title == "Made" && counts[0].Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "Wood Arrow=40", "Bronze=6" }),
              "counts: the rest of the stretch by kind (what has a line of its own is not counted again)");
        var feedLines = Lines(feed);
        var session = feedLines.FirstOrDefault(l => l.Tone == "session");
        Check(session != null && session.Text.StartsWith("Yesterday, 10:00 to 13:40", StringComparison.Ordinal), "session: the morning before under its own line, its day and span: " + session?.Text);
        var folded = isl.Last();
        Check(folded.Title.Contains("at about 10:10") && folded.Text.EndsWith("times to the nearest 10 min", StringComparison.Ordinal),
              "times: a stretch in minutes kept in tens says its time \"about\" and that its times are to the nearest 10 minutes: " + folded.Title + " · " + folded.Text);
        var gap = feedLines.FirstOrDefault(l => l.Tone == "gap");
        Check(gap != null && gap.Text == "2 h 25 min without crafting", "gap: the quiet time between two stretches of one session: " + gap?.Text);
        Check(PanelModel.IsFeedView(feed) && !PanelModel.AllText(feed).Any(t => t != null && (t.Contains("foe") || t.Contains("$item"))),
              "words: a deed feed says stretches and deeds, never foes, and no raw token");

        // ---------- windows ----------
        var (sess, _) = Open(yours, "crafting", TimeWindow.Session);
        Check(Islands(sess).Count == 1 && Lines(sess).Any(l => (l.Text ?? "").Contains("Session shows this session's stretches only. Choose ")),
              "Session: this session's stretch only, and one line on where the earlier ones show");
        var noLive = FeedSample.Yours(world); noLive.DeedStretches = noLive.DeedStretches.Where(s => !s.Live).ToList();
        var (opened, _) = Open(noLive, "crafting", TimeWindow.Session, s => s.WindowPicked = false);
        Check(opened.ShownWindow != TimeWindow.Session && Islands(opened).Count == 2, "windows: nothing this session and no window chosen: the feed opens on the first window that holds your last stretch (" + opened.ShownWindow + ")");

        // ---------- the filter ----------
        var (sword, _) = Open(yours, "crafting", TimeWindow.SevenDays, s => PanelModel.ToggleFacet(s, "Deeds/feed-crafting", "kind", "$item_sword_iron"));
        var swordIsl = Islands(sword);
        Check(swordIsl.Count == 1 && swordIsl[0].Items.Count(e => e.Kind == "entry") == 2 && swordIsl[0].Text.EndsWith("1 of 6 kinds", StringComparison.Ordinal) && !swordIsl[0].Items.Any(e => e.Kind == "counts"),
              "filter: Kind on the Iron Sword: only the stretch with it, only its lines, and the island says how much of it shows: " + swordIsl.FirstOrDefault()?.Text);
        var bar = PanelModel.Content(feed).FirstOrDefault(b => b.Kind == "filterbar");
        Check(bar != null && bar.Id == "Deeds/feed-crafting" && Rows(feed).SequenceEqual(new[] { "Kind", "Main material" }),
              "filter: Crafting's feed filters by kind and by main material (Crafting's own facets)");

        // ---------- Show older ----------
        var many = FeedSample.Yours(world);
        var longS = new DeedSession { World = "Sample", Id = "c0ffee000099", FromUtc = now.AddDays(-3), ToUtc = now.AddDays(-2) };
        for (int i = 0; i < 28; i++) { var st = new DeedStretch { Family = DeedStretches.Mining, FromUtc = longS.FromUtc.AddMinutes(i * 40), ToUtc = longS.FromUtc.AddMinutes(i * 40 + 15) }; st.Counts["pickedUp|$item_stone"] = 10 + i; longS.Stretches.Add(st); }
        many.DeedStretches = many.DeedStretches.Concat(new[] { longS }).ToList();
        var (p1, ps) = Open(many, "mining", TimeWindow.SinceInstall);
        var more1 = PanelModel.FeedMoreLine(p1);
        Check(Islands(p1).Count == PanelModel.DeedFeedTop && more1 != null && more1.Title == "Show 12 older stretches" && more1.Text.StartsWith("Shown: 12 of 29 stretches in this window. Not shown: 17 earlier stretches.", StringComparison.Ordinal),
              "older: the first page draws the newest " + PanelModel.DeedFeedTop + " stretches; the button says what one press adds, the line what is shown and left: " + more1?.Text);
        PanelModel.Follow(ps, PanelModel.FeedMoreTarget); var p2 = Build(many, ps);
        PanelModel.Follow(ps, PanelModel.FeedMoreTarget); var p3 = Build(many, ps);
        Check(Islands(p2).Count == 24 && Islands(p3).Count == 29 && PanelModel.FeedMoreLine(p3) == null && Lines(p3).Any(l => l.Tone == "note" && l.Text.StartsWith("Shown: all 29 stretches", StringComparison.Ordinal)) &&
              p2.Keys.Contains(PanelModel.DeedMoreKeyLine),
              "older: each press adds the next " + PanelModel.DeedFeedTop + " (Enter does too); at the end no button, the count line says all are shown");
        var json1 = PanelModel.ToJson(p1);
        PanelModel.Follow(ps, "Deeds/mining"); var back = Build(many, ps); PanelModel.Visited(ps, back.Active, back.Page); PanelModel.Follow(ps, "Deeds/feed-mining"); var p4 = Build(many, ps);
        Check(Islands(p4).Count == PanelModel.DeedFeedTop, "older: another page and back: the feed starts at its first page again");
        Check(PanelModel.FeedAppendAt(p1, json1, p2) >= 0, "older: a press draws only the new page (FeedAppendAt finds where it starts), as the Battle feed's");

        // ---------- the mixed feed ----------
        // its deed stretches first, on a book without fights (the fights are the all-feed's checks below)
        var calm = yours.ShallowCopy(); calm.Battle = null; calm.KeptSessions = null;
        var (mixed, _) = Open(calm, PanelModel.RecentPageId, TimeWindow.SevenDays);
        var mixedIsl = Islands(mixed);
        Check(mixed.Page == PanelModel.DeedFeedAllPage && mixed.List.First(l => l.Selected).Id == PanelModel.RecentPageId && mixed.Heading == "Deeds feed" &&
              mixedIsl.Select(i => i.Title.Substring(0, i.Title.IndexOf(" at ", StringComparison.Ordinal))).SequenceEqual(new[] { "Crafting", "Cooking", "Building", "Woodcutting", "Crafting", "Building", "Mining", "Crafting" }),
              "mixed: from Recent, every deed of the window in time order, each island named by its deed: " + string.Join(", ", mixedIsl.Select(i => i.Title)));
        Check(Lines(mixed).Any(l => l.Tone == "gap" && l.Text.EndsWith("without a deed", StringComparison.Ordinal)) && Rows(mixed).SequenceEqual(new[] { "Deed" }),
              "mixed: its quiet times say \"without a deed\"; its filter is by deed");
        var cook = mixedIsl[1];
        Check(cook.Text == "14 min · 8 dishes cooked · 1 feast set out" && cook.Items.Any(e => e.Kind == "entry" && e.Note == "set out" && e.Tone == "made"),
              "mixed: a feast set out has its line in the cooking stretch: " + cook.Text);
        var build = mixedIsl[2];
        Check(build.Items.Where(b => b.Kind == "counts").Select(b => b.Title).SequenceEqual(new[] { "Built", "Recovered from pieces that came down" }) && build.Items.Any(e => e.Kind == "entry" && e.Note == "placed, first recorded"),
              "mixed: building's pieces counted by kind first, the materials recovered after; a first piece placed has its line");
        var wood = mixedIsl[3];
        Check(wood.Text == "16 min · 4 trees felled · 49 wood brought in · 62 axe hits" && wood.Items.Where(b => b.Kind == "counts").Select(b => b.Title).SequenceEqual(new[] { "Trees felled", "Brought in" }) &&
              wood.Items.First(b => b.Kind == "counts").Items.Select(i => i.Title).SequenceEqual(new[] { "Beech", "Oak" }),
              "mixed: woodcutting's head counts trees, wood and axe hits apart (never added across kinds); trees by their kind: " + wood.Text);

        // ---------- 0.9 all-feed: every deed recorded, in the mixed feed ----------
        // every family the deed log records has its words and a family the mixed feed shows (a family added to DeedLog without them fails here)
        var labelled = PanelModel.DeedFeedFamilies.Concat(PanelModel.DeedFeedMoreFamilies).ToDictionary(f => f.id, f => f.label);
        var unmapped = DeedLog.AllFamilies.SelectMany(f => f == DeedLog.Counters ? DeedLog.CounterStats.Select(t => (f, t)) : new[] { (f, (string)null) })
            .Where(x => { var k = PanelModel.DeedFeedKindOf(x.Item1, x.Item2); return k == null || string.IsNullOrEmpty(k.Value.words) || !labelled.TryGetValue(k.Value.family, out var l) || string.IsNullOrEmpty(l); })
            .Select(x => x.Item1 + (x.Item2 == null ? "" : "|" + x.Item2)).ToList();
        Check(unmapped.Count == 0 && DeedLog.AllFamilies.Length == 12 && DeedLog.CounterStats.Length == 6,
              "all-feed: every family the deed log records maps to a feed label (" + DeedLog.AllFamilies.Length + " families, " + DeedLog.CounterStats.Length + " counters; without: " + (unmapped.Count == 0 ? "none" : string.Join(", ", unmapped)) + ")");
        Check(PanelModel.DeedFeedKindOf(DeedLog.ChopHits).Value.family == DeedStretches.Woodcutting && PanelModel.DeedFeedKindOf(DeedLog.PickaxeHits).Value.family == DeedStretches.Mining &&
              PanelModel.DeedFeedKindOf(DeedLog.Kills).Value.family == PanelModel.DeedFeedFights && !PanelModel.DeedFeedMoreFamilies.Any(f => f.id.Contains("hit")),
              "all-feed: hit counts stay inside their deed's stretch (axe hits Woodcutting, pickaxe hits Mining), never a family of their own; kills are the fights");
        var busy = FeedSample.Yours(world, "busy");
        var (all, allState) = Open(busy, PanelModel.RecentPageId, TimeWindow.Session, s => s.FeedMore = 9);
        var allIsl = Islands(all);
        string Deed(Block b) => b.Title.Substring(0, b.Title.IndexOf(" at ", StringComparison.Ordinal));
        var deedsSeen = allIsl.Select(Deed).Distinct().ToList();
        Check(new[] { "Crafting", "Cooking", "Building", "Woodcutting", "Taming", "Skills", "Brought in", "Sailing", "Fight" }.All(deedsSeen.Contains),
              "all-feed: a busy evening's mixed feed has a stretch for every recorded family, each with its own name, and the fights: " + string.Join(", ", deedsSeen));
        Check(allIsl.Count(i => i.NoteLink == null) == 8,
              "all-feed: this session's 8 stretches, between its fights");
        Block Of(string deed) => allIsl.First(i => Deed(i) == deed);
        var sail = Of("Sailing"); var tame = Of("Taming"); var skills = Of("Skills"); var brought = Of("Brought in");
        Check(sail.Text == "32 min · 4.2 km sailed · 186 item-km carried" && tame.Text == "4 min · 1 tamed" && sail.Icon == "title:sailor" && tame.Icon == "title:tamer" &&
              !sail.Items.Any() && !tame.Items.Any(),
              "all-feed: sailing says km sailed and the cargo carried, taming the creatures tamed, each in its own words and picture: " + sail.Text + " | " + tame.Text);
        var practiced = skills.Items.FirstOrDefault(b => b.Kind == "counts");
        Check(skills.Text == "28 min · 4 skills practiced" && practiced?.Title == "Practiced" && practiced.Items.Count == 4 && practiced.Items.All(i => i.Value == "+" && i.Icon.StartsWith("skill:", StringComparison.Ordinal)) &&
              practiced.Items[0].Id == "Swords",
              "all-feed: skill practice names the skills that rose, practiced as a glow word (a \"+\"), never levels or the practice's own units: " + skills.Text);
        var broughtIn = brought.Items.FirstOrDefault(b => b.Kind == "counts");
        Check(brought.Text.StartsWith("16 min · 44 items", StringComparison.Ordinal) && broughtIn?.Title == "Brought in" && broughtIn.Items.Select(i => i.Title).SequenceEqual(new[] { "Raspberries", "Leather Scraps", "Mushroom", "Feathers" }),
              "all-feed: what was brought in that is neither wood nor stone and ore, \"brought in\" in the house words, by kind: " + brought.Text);
        var fight = allIsl.First(i => i.NoteLink != null);
        Check(fight.Title.StartsWith("Fight at ", StringComparison.Ordinal) && fight.NoteLink == PanelModel.BattleFeedTarget && fight.Note == PanelModel.DeedFightLink &&
              fight.Items.Count == 0 && fight.Value == null && fight.Value2 == null && System.Text.RegularExpressions.Regex.IsMatch(fight.Text, @"^(under a minute|\d+ min)( · \d+ foes?)?$"),
              "all-feed: a fight is one line (when, how long, how many foes) that opens the Battle feed; none of the Battle feed's own lines, no damage: " + fight.Title + " · " + fight.Text);
        var toBattle = new PanelState { Chapter = Chapter.Deeds }; toBattle.Page[Chapter.Deeds] = PanelModel.DeedFeedAllPage; PanelModel.Follow(toBattle, fight.NoteLink);
        Check(toBattle.Chapter == Chapter.Battle && toBattle.Page[Chapter.Battle] == PanelModel.FeedPage, "all-feed: the fight line's link opens Battle's feed");
        var deedRow = (PanelModel.Content(all).FirstOrDefault(b => b.Kind == "filterbar")?.Items ?? new List<Block>()).FirstOrDefault(b => b.Kind == "facet" && b.Id == "deed");
        var options = deedRow?.Items?.Select(c => c.Title).ToList() ?? new List<string>();
        Check(new[] { "Crafting", "Cooking", "Building", "Woodcutting", "Taming", "Skills", "Brought in", "Sailing", "Fights" }.All(options.Contains) && !options.Contains("Mining"),
              "all-feed: the Deed filter lists every family present in the window, the fights too (not one that is absent): " + string.Join(", ", options));
        var (picked, _) = Open(busy, PanelModel.RecentPageId, TimeWindow.Session, s => { PanelModel.ToggleFacet(s, "Deeds/feed-all", "deed", PanelModel.DeedFeedFights); PanelModel.ToggleFacet(s, "Deeds/feed-all", "deed", DeedStretches.Taming); s.FeedMore = 9; });
        var pickedIsl = Islands(picked);
        Check(pickedIsl.Count > 1 && pickedIsl.All(i => i.NoteLink != null || Deed(i) == "Taming") && pickedIsl.Count(i => Deed(i) == "Taming") == 1,
              "all-feed: the Deed filter on Fights and Taming shows those only, whole: " + string.Join(", ", pickedIsl.Select(Deed).Distinct()));
        var allText = PanelModel.AllText(all).Where(t => t != null).ToList();
        Check(allText.Count(t => t.Contains(PanelModel.DeedFeedNotByMinute)) == 1 && PanelModel.DeedFeedNotByMinute.StartsWith(PanelModel.NotRecorded + " ", StringComparison.Ordinal) &&
              PanelModel.DeedFeedNotByMinute.Contains("meals eaten") && PanelModel.DeedFeedNotByMinute.Contains("petted") && PanelModel.DeedFeedNotByMinute.Contains("helm"),
              "all-feed: what no minute holds (meals eaten, creatures petted, at the helm or as a passenger) is said once, in the house sentence \"Not recorded\"");
        Check(System.Text.RegularExpressions.Regex.IsMatch(PanelModel.SwitchOf(all)?.Title ?? "", @"^8 stretches · \d+ fights$"),
              "all-feed: the caption counts stretches and fights apart: " + PanelModel.SwitchOf(all)?.Title);
        var (craftBusy, _) = Open(busy, "crafting", TimeWindow.Session);
        Check(Islands(craftBusy).Count > 0 && Islands(craftBusy).All(i => Deed(i) == "Crafting" && i.NoteLink == null) && !Rows(craftBusy).Contains("Deed"),
              "all-feed: a family's own feed keeps its own stretches only (no fight lines, no other deeds), its filter as before");

        // ---------- not recorded, empty ----------
        var (unread, _) = Open(FeedSample.Yours(world, "unread"), "crafting", TimeWindow.Session);
        var (empty, _) = Open(FeedSample.Yours(world, "empty"), "crafting", TimeWindow.Session);
        var unreadEmpty = PanelModel.Content(unread).FirstOrDefault(b => b.Kind == "empty"); var emptyEmpty = PanelModel.Content(empty).FirstOrDefault(b => b.Kind == "empty");
        Check(unreadEmpty?.Title == PanelModel.NotRecorded && unreadEmpty.Text == PanelModel.DeedFeedNotRecorded && emptyEmpty?.Title == "No crafting yet" &&
              !PanelModel.AllText(unread).Concat(PanelModel.AllText(empty)).Any(t => t == "0") && emptyEmpty.Text.EndsWith(" · this PC.", StringComparison.Ordinal) && emptyEmpty.Text.Contains("Recorded from ") &&
              Lines(feed).Last().Text.EndsWith("Recorded from " + PanelModel.RecordDate(yours, yours.FirstsFrom.Value) + " · this PC.", StringComparison.Ordinal),
              "states: before the records are read \"Not recorded\" (never 0); nothing yet: what will show; the feed's last line and the empty state say from when it is recorded on this PC");

        // ---------- a friend's feed from the server ----------
        var edda = FullDump.Books(world).First(b => b.who == SampleWorld.Edda).book;
        var (ef, _) = Open(FeedSample.Edda(edda, now), "crafting", TimeWindow.SinceInstall, null, SampleWorld.Edda);
        var efIsl = Islands(ef); var efLast = Lines(ef).LastOrDefault();
        Check(efIsl.Count == 1 && efIsl[0].Text == "22 min · 61 made · 1 upgrade" && Lines(ef).First().Tone == "session" &&
              efLast?.Tone == "loading" && efLast.Text.StartsWith("Getting Edda's session of ", StringComparison.Ordinal) && efLast.Text.EndsWith(" from the server · 2 of 2", StringComparison.Ordinal) && ef.FetchBlock == null,
              "friend: Edda's session from the server under its line, her next session on its way (its line counts where it is)");
        Check(ef.Windows.Where(w => w.Disabled).Select(w => w.Id).SequenceEqual(new[] { "LastTenMinutes", "LastThirtyMinutes", "LastHour", "LastThreeHours", "Session" }) && ef.Windows.All(w => !w.Disabled || w.Why == PanelModel.DeedFeedRunningWhy) &&
              ef.ShownWindow == TimeWindow.SinceInstall,
              "friend: their days and All open; Session and the short windows greyed with why (their running session comes once it ends)");
        var (asking, _) = Open(FeedSample.Edda(edda, now, null), "crafting", TimeWindow.SinceInstall, null, SampleWorld.Edda);
        Check(asking.FetchBlock == FeedSample.EddaFarId && PanelModel.FeedMoreLine(asking)?.Title == PanelModel.DeedShowOlder,
              "friend: the next session not asked yet: the page asks for it (FetchBlock) and offers Show older");
        var (failed, _) = Open(FeedSample.Edda(edda, now, "failed"), "crafting", TimeWindow.SinceInstall, null, SampleWorld.Edda);
        var tryAgain = PanelModel.FeedMoreLine(failed);
        Check(tryAgain?.Title == PanelModel.TryAgainWords && tryAgain.NoteLink == PanelModel.FeedFetchTarget && tryAgain.Value == FeedSample.EddaFarId && tryAgain.Text.EndsWith(" did not arrive.", StringComparison.Ordinal),
              "friend: a session that did not arrive is said, with Try again (never skipped)");
        var (none, _) = Open(FeedSample.Edda(edda, now, null, "none"), "crafting", TimeWindow.SinceInstall, null, SampleWorld.Edda);
        var (oldServer, _) = Open(FeedSample.Edda(edda, now, null, "oldserver"), "crafting", TimeWindow.SinceInstall, null, SampleWorld.Edda);
        Check(PanelModel.Content(none).FirstOrDefault(b => b.Kind == "empty")?.Title == "No crafting from Edda yet" &&
              PanelModel.Content(oldServer).FirstOrDefault(b => b.Kind == "empty")?.Text == "This server keeps no deed feeds: it needs Hearthwoven 0.9 or later.",
              "friend: none listed yet: when hers come; a server before 0.9: it keeps no deed feeds");
        var (efMixed, _) = Open(FeedSample.Edda(edda, now), PanelModel.RecentPageId, TimeWindow.SinceInstall, null, SampleWorld.Edda);
        Check(Islands(efMixed).Select(i => i.Title.Split(' ')[0]).SequenceEqual(new[] { "Crafting", "Cooking", "Farming" }), "friend: her mixed feed, every deed of her session in time order");
        return fails;
    }
}
