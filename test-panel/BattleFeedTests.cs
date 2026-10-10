// The battle pages of 0.8 (src/Panel/Chapters/BattleFeedModel.cs) on the sample world: the "×N" shows only where the window has a count (never
// "×0"), the battle feed's view is kept between sessions (PanelPrefs), and the last fight holds the fellow players near you for 10 s or more.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class BattleFeedTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var world = PanelSample.Full(now);
        Block Find(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
        PanelState Damage(TimeWindow w, Action<PanelState> more = null) { var s = new PanelState { Chapter = Chapter.Battle, Window = w, Page = { [Chapter.Battle] = "damage" } }; s.View["Battle/damage/view"] = "foe"; more?.Invoke(s); return s; }
        IEnumerable<Block> All(Block b) => new[] { b }.Concat((b?.Items ?? new List<Block>()).SelectMany(All));

        // ---------- the "×N" only where the window has a count ----------
        var session = Find(PanelModel.Build(world, Damage(TimeWindow.Session)), "dealtfoes");
        var counts = PanelModel.FoeCountsIn(world, TimeWindow.Session);
        var shown = session?.Items.Where(i => i.Kind == "source" && i.Id != PanelModel.FoldId).ToList() ?? new List<Block>();
        var right = shown.All(i => i.Value2 == PanelModel.Badge(counts.Get(i.Id)?.Separate ?? 0)) && shown.Count(i => i.Value2 != null) >= 3;
        // a biome chosen: the damage narrows, the counts are not kept per biome: no badge, no marks, one line says why
        var swamp = PanelModel.Build(world, Damage(TimeWindow.Session, s => s.Facets[PanelModel.BattleDamageFilter + "|biome"] = new List<string> { "Swamp" }));
        var narrowed = Find(swamp, "dealtfoes");
        // a fellow's short window carries no counts: their foes without a badge; and nowhere a "×0"
        var edda = FullDump.Books(world).First(b => b.who == "Edda").book;
        var fellowHour = PanelModel.DealtFoes(edda, PanelModel.Damage(edda.Log, TimeWindow.LastHour, "", edda.NowUtc), TimeWindow.LastHour);   // their copy has Session and All only
        var everyX = PanelModel.AllWindows.SelectMany(w => All(Find(PanelModel.Build(world, Damage(w)), "dealtfoes"))).Concat(All(Find(PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.SinceInstall, Page = { [Chapter.Battle] = "defense" } }), "sources")))
                            .Where(b => b != null).Select(b => b.Value2).Where(v => v != null).ToList();
        Check(session != null && right && narrowed != null && narrowed.Items.All(i => i.Value2 == null && (i.Items ?? new List<Block>()).All(p => p.Kind != "pips")) &&
              PanelModel.Content(swamp).Any(b => b.Kind == "note" && b.Text == PanelModel.ByFoeNoBiome) &&
              fellowHour != null && fellowHour.Items.All(i => i.Value2 == null) && PanelModel.Badge(0) == null && everyX.Count > 0 && everyX.All(v => v.StartsWith("×") && v != "×0"),
              "BF by foe: the x N is the window's count of separate foes (" + string.Join(", ", shown.Where(i => i.Value2 != null).Select(i => i.Title + " " + i.Value2)) + "); none where a biome is chosen (one line says why) or a fellow's window has no count; never x 0");

        // REVIEW-08 #2: a fellow's Session counts are one tally of their whole connection, while on their book Session starts at yours: the counts
        // show only when their log lies inside your session; when they played before you came in, no x N and no marks, and one line says so
        {
            var tor = FullDump.Books(world).First(b => b.who == "Tor").book; var keep = tor.ViewerSessionStartUtc;
            PanelView TorPage(string pg, DateTime yours)
            {
                tor.ViewerSessionStartUtc = yours;
                var s = pg == "damage" ? Damage(TimeWindow.Session) : new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Session, Page = { [Chapter.Battle] = pg } };
                s.Player = "Tor"; return PanelModel.Build(tor, s);
            }
            bool Line(PanelView v) => PanelModel.AllText(v).Contains(PanelModel.FellowCountsAll(tor));
            List<Block> Badged(Block b) => (b?.Items ?? new List<Block>()).Where(i => i.Kind == "source" && (i.Value2 != null || (i.Items ?? new List<Block>()).Any(p => p.Kind == "pips"))).ToList();
            try
            {
                var first = tor.FirstRecordedUtc.Value; var mid = first + TimeSpan.FromTicks((tor.LastRecordedUtc.Value - first).Ticks / 2);
                var inside = TorPage("damage", first.AddMinutes(-1)); var earlier = TorPage("damage", mid);
                var defInside = TorPage("defense", first.AddMinutes(-1)); var defBefore = TorPage("defense", mid);
                Check(Badged(Find(inside, "dealtfoes")).Count > 0 && !Line(inside) && Find(earlier, "dealtfoes") != null && Badged(Find(earlier, "dealtfoes")).Count == 0 && Line(earlier) &&
                      Badged(Find(defInside, "sources")).Count > 0 && !Line(defInside) && Find(defBefore, "sources") != null && Badged(Find(defBefore, "sources")).Count == 0 && Line(defBefore),
                      "BF REVIEW-08 #2: Tor's Session x N and marks show when Tor's log lies inside your session; when Tor played before you came in, none on By foe or Defence, and one line: " + PanelModel.FellowCountsAll(tor));
            }
            finally { tor.ViewerSessionStartUtc = keep; }
        }

        // REVIEW-08 #9: the feed keeps the newest 200 foes; a long evening (fights of 150, 40 and 30 foes) cuts into the first fight: it is left out
        // rather than shown with part of its foes, and the note says the older foes are no longer kept instead of counting them as this session
        {
            var ring = new BattleRecorder(); var at = new DateTime(2026, 10, 10, 18, 0, 0, DateTimeKind.Utc); uint id = 1;
            foreach (var size in new[] { 150, 40, 30 })
            {
                for (int i = 0; i < size; i++) { ring.Dealt(at, new FoeId(7, id++), "Greydwarf", "$enemy_greydwarf", "BlackForest", new HitData.DamageTypes { m_slash = 20 }); at = at.AddSeconds(1); }
                at = at.AddMinutes(3); ring.Tick(at);
            }
            var ringFights = ring.Fights();
            var ringNote = PanelModel.FeedBlock(new PanelInput { Battle = ring, NowUtc = at }, ringFights, PanelModel.FeedLog)?.Note ?? "";
            Check(ringFights.Count == 2 && ringFights.Sum(f => f.Foes) == 70 && ring.FeedLeftOut == 150 && ringNote.Contains("Older fights (150 foes) are no longer kept: the book keeps your newest " + BattleRecorder.MaxFeed + " foes.") && !ringNote.Contains("this session"),
                  "BF REVIEW-08 #9: past " + BattleRecorder.MaxFeed + " foes the cut first fight is left out and the note says the older foes are no longer kept: " + ringNote);
        }

        // ---------- 0.8.1: the feed's window and filter (Joost: "Battle feed, why can't I filter this?") ----------
        // his evening: a Gjall early on, then 48 Roots. The page draws the newest 40 foes, so the Gjall has dropped off; the recorder still keeps
        // it (200 foes), and the cap counts after the filter, so choosing Gjall finds that fight again
        {
            var gr = new BattleRecorder(); var t = now.AddMinutes(-50); uint fid = 100;
            gr.Dealt(t, new FoeId(9, fid++), "Gjall", "$enemy_gjall", "Mistlands", new HitData.DamageTypes { m_pierce = 300 }); t = t.AddMinutes(3); gr.Tick(t);
            for (int i = 0; i < 48; i++) { gr.Dealt(t, new FoeId(9, fid++), "Root", "$enemy_root", "Mistlands", new HitData.DamageTypes { m_fire = 30 }); t = t.AddSeconds(2); }
            gr.Tick(now);
            var book = world.ShallowCopy(); book.Battle = gr;
            PanelView Feed(Action<PanelState> more = null) { var s = new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = PanelModel.FeedPage } }; more?.Invoke(s); return PanelModel.Build(book, s); }
            IEnumerable<Block> Entries(PanelView v) => (Find(v, "feed")?.Items ?? new List<Block>()).SelectMany(f => f.Items ?? new List<Block>()).Where(e => e.Kind == "entry");
            var plain = Feed(); var gjall = Feed(s => s.Facets[PanelModel.FeedFilter + "|foe"] = new List<string> { "Gjall" });
            var plainNote = Find(plain, "feed")?.Note ?? ""; var bar = Find(gjall, "filterbar"); var fight = Find(gjall, "feed")?.Items.FirstOrDefault();
            Check(Entries(plain).Count() == PanelModel.FeedTop && Entries(plain).All(e => e.Id == "Root") && plainNote.StartsWith("8 more foes in this fight and 1 earlier fight this session (1 foe) not shown") && Find(plain, "feed").Items[0].Text.EndsWith("40 of 48 foes") &&
                  Entries(gjall).Select(e => e.Id).SequenceEqual(new[] { "Gjall" }) && bar != null && bar.Text == "1 of 49 foes" && fight?.Text == "under a minute · 1 foe" &&
                  bar.Items.First(r => r.Kind == "facet" && r.Id == "foe").Items.Select(c => c.Id).SequenceEqual(new[] { "Root", "Gjall" }),
                  "BF 0.8.1 filter: the cap of " + PanelModel.FeedTop + " counts after the filter: 48 Roots hide the Gjall, Foe = Gjall finds its fight (" + bar?.Text + "); the Foe row most foes first; unfiltered note: " + plainNote);
            // a fight whose foes only partly pass shows those foes, "1 of 5 foes", and their damage (its heading adds up with its rows): the sample's Troll
            var troll = PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = PanelModel.FeedPage }, Facets = { [PanelModel.FeedFilter + "|foe"] = new List<string> { "Troll" } } });
            var trollFights = Find(troll, "feed")?.Items ?? new List<Block>();
            var mixed = trollFights.FirstOrDefault(f => (f.Text ?? "").Contains(" of "));
            var mixedEntries = mixed?.Items.Where(e => e.Kind == "entry").ToList() ?? new List<Block>();
            Check(trollFights.Count == 2 && trollFights.All(f => f.Items.Where(e => e.Kind == "entry").All(e => e.Id == "Troll")) && mixed != null && mixed.Text.EndsWith("1 of 5 foes") &&
                  mixedEntries.Count == 1 && mixed.Value == mixedEntries[0].Value,
                  "BF 0.8.1 filter: a fight shows only its foes that pass, its heading \"" + mixed?.Text + "\" and its damage theirs (" + mixed?.Value + " = " + mixedEntries.FirstOrDefault()?.Value + ")");
            // the window: 10 min shows only the foes hit in the last 10 minutes; the day windows and All are greyed, each with its reason; the title says it
            var ten = PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.LastTenMinutes, WindowPicked = true, Page = { [Chapter.Battle] = PanelModel.FeedPage } });
            var feedFights = PanelModel.BattleFeed(world); var cut = now.AddMinutes(-10);
            var tenFoes = Find(ten, "feed")?.Items.Sum(f => f.Items.Count(e => e.Kind == "entry")) ?? 0;
            var greyed = ten.Windows.Where(c => c.Disabled).ToList();
            Check(tenFoes == feedFights.Sum(f => f.Entries.Count(e => e.EndUtc >= cut)) && tenFoes > 0 && tenFoes < feedFights.Sum(f => f.Foes) && ten.Heading + ", " + ten.HeadingWindow == "Battle feed, last 10 minutes" &&
                  greyed.Select(c => c.Id).SequenceEqual(new[] { "Today", "SevenDays", "ThirtyDays", "SinceInstall" }) && greyed.Take(3).All(c => c.Why == PanelModel.FeedDaysWhy) && greyed[3].Why == PanelModel.FeedAllWhy,
                  "BF 0.8.1 window: 10 min shows " + tenFoes + " foes (those hit since " + cut.ToString("HH:mm") + "), the title \"" + ten.Heading + ", " + ten.HeadingWindow + "\", the days and All greyed with their own reason");
            // the window and the filter kept between sessions: the window the feed showed is noted and written with the view; a fresh game (no window
            // chosen yet) opens the feed on it while the other pages keep Session; 7 days chosen elsewhere shows Session here
            var played = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.LastHour, WindowPicked = true, Page = { [Chapter.Battle] = PanelModel.FeedPage } };
            played.Facets[PanelModel.FeedFilter + "|biome"] = new List<string> { "Swamp" };
            PanelModel.Build(world, played);
            var later = new PanelState(); PanelPrefs.Apply(PanelPrefs.ToJson(played), later);
            later.Chapter = Chapter.Battle; later.Page[Chapter.Battle] = PanelModel.FeedPage;
            var reopened = PanelModel.Build(world, later);
            later.Page[Chapter.Battle] = "damage"; var damage = PanelModel.Build(world, later);
            var week = PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.SevenDays, WindowPicked = true, Page = { [Chapter.Battle] = PanelModel.FeedPage } });
            Check(reopened.ShownWindow == TimeWindow.LastHour && reopened.HeadingWindow == "last hour" && PanelModel.Chosen(later, PanelModel.FeedFilter, "biome").SequenceEqual(new[] { "Swamp" }) &&
                  (Find(reopened, "feed")?.Items ?? new List<Block>()).SelectMany(f => f.Items).Where(e => e.Kind == "entry").All(e => e.Id == "Leech" || e.Id == "Draugr" || e.Id == "Blob") &&
                  damage.ShownWindow == TimeWindow.Session && week.ShownWindow == TimeWindow.Session && week.Windows.First(c => c.Id == "Session").Selected,
                  "BF 0.8.1: the feed's window (1 h) and Biome choice are kept between sessions and reopen the feed on them; Damage keeps Session; 7 days chosen elsewhere shows Session on the feed");
            // review 0.8.1 batch 6: 1 h on the feed, then 7 days chosen on Deeds and the feed opened again: it keeps showing 1 h and keeps it noted,
            // so the next session opens on 1 h, not Session (the 7 days overwrote it before)
            var evening = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.LastHour, WindowPicked = true, Page = { [Chapter.Battle] = PanelModel.FeedPage } };
            PanelModel.Build(world, evening);
            evening.Chapter = Chapter.Deeds; evening.Window = TimeWindow.SevenDays; PanelModel.Build(world, evening);
            evening.Chapter = Chapter.Battle; var feedAfterDeeds = PanelModel.Build(world, evening);
            var morning = new PanelState(); PanelPrefs.Apply(PanelPrefs.ToJson(evening), morning);
            morning.Chapter = Chapter.Battle; morning.Page[Chapter.Battle] = PanelModel.FeedPage;
            var nextSession = PanelModel.Build(world, morning);
            Check(feedAfterDeeds.ShownWindow == TimeWindow.LastHour && evening.View[PanelModel.FeedWindowKey] == nameof(TimeWindow.LastHour) && nextSession.ShownWindow == TimeWindow.LastHour,
                  "BF 0.8.1 review batch 6: 1 h on the feed, then 7 days chosen on Deeds: the feed still shows 1 h (" + feedAfterDeeds.ShownWindow + ") and the next session opens on it (" + nextSession.ShownWindow + "), not Session");
            // review 0.8.1 batch 6: the last fight shown has no gap (the fight before it is outside the window, the filter or the cap); between two
            // fights shown the gap stays: the quiet time when they follow each other, else the time and how many fights lie between
            List<string> Gaps(PanelView v) => (Find(v, "feed")?.Items ?? new List<Block>()).Select(f => f.Items.FirstOrDefault(x => x.Kind == "gap")?.Text).ToList();
            var hour = PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.LastHour, WindowPicked = true, Page = { [Chapter.Battle] = PanelModel.FeedPage } });
            var draugr = PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = PanelModel.FeedPage }, Facets = { [PanelModel.FeedFilter + "|foe"] = new List<string> { "Draugr" } } });
            var hourGaps = Gaps(hour); var draugrGaps = Gaps(draugr); var trollGaps = Gaps(troll);
            Check(hourGaps.Count > 1 && hourGaps.Last() == null && hourGaps.Take(hourGaps.Count - 1).All(g => g != null && g.EndsWith(" without a fight")) &&
                  trollGaps.Count == 2 && trollGaps[0] != null && trollGaps[0].EndsWith(" without a fight") && trollGaps[1] == null &&
                  draugrGaps.Last() == null && draugrGaps.Any(g => g != null && g.Contains(" between not shown")),
                  "BF 0.8.1 review batch 6: no gap under the last fight shown (1 h, Troll, Draugr); between fights shown the quiet time, or the time with the fights between (" + draugrGaps.FirstOrDefault(g => g != null && g.Contains("between")) + ")");
            // nothing in the window, or nothing for the choice: its own empty line, the bar stays to unchoose
            var none = PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = PanelModel.FeedPage }, Facets = { [PanelModel.FeedFilter + "|biome"] = new List<string> { "Swamp" }, [PanelModel.FeedFilter + "|foe"] = new List<string> { "Troll" } } });
            Check(Find(none, "empty")?.Title == PanelModel.NothingForChoice && Find(none, "filterbar") != null && Find(none, "feed") == null,
                  "BF 0.8.1: a choice with no foe (Swamp and Troll) says " + PanelModel.NothingForChoice + " and keeps the bar to clear it");
        }

        // ---------- the feed's view, kept between sessions ----------
        var st = new PanelState(); st.View[PanelModel.FeedViewKey] = PanelModel.FeedCards; st.View["Battle/damage/view"] = "type";   // only the feed's is kept
        var json = PanelPrefs.ToJson(st);
        var back = new PanelState(); var read = PanelPrefs.Apply(json, back);
        var page = PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = PanelModel.FeedPage }, View = { [PanelModel.FeedViewKey] = back.View.TryGetValue(PanelModel.FeedViewKey, out var v) ? v : "" } });
        var feed = Find(page, "feed");
        var old = new PanelState(); PanelPrefs.Apply("{\"version\":1,\"facets\":{},\"open\":[]}", old);
        var odd = new PanelState(); PanelPrefs.Apply("{\"version\":1,\"facets\":{},\"open\":[],\"views\":{\"Battle/damage/view\":\"type\"}}", odd);
        Check(read && back.View.TryGetValue(PanelModel.FeedViewKey, out var kept) && kept == PanelModel.FeedCards && !back.View.ContainsKey("Battle/damage/view") && feed?.Tone == PanelModel.FeedCards &&
              PanelPrefs.ToJson(new PanelState()) == "{\"version\":1,\"facets\":{},\"open\":[]}" && !old.View.ContainsKey(PanelModel.FeedViewKey) && !odd.View.ContainsKey("Battle/damage/view"),
              "BF feed: the chosen view (Cards) is written to the panel prefs and read back, the page opens on it; a file without one reads and writes as before; no other view is kept");

        // ---------- the last fight: who was near you ----------
        var t0 = new DateTime(2026, 10, 10, 20, 0, 0, DateTimeKind.Utc);
        var rec = new BattleRecorder();
        var d = new HitData.DamageTypes { m_slash = 20 };
        rec.Near(t0, "Edda", 2);   // no fight on: nothing
        rec.Dealt(t0, new FoeId(1, 1), "Draugr", "$enemy_draugr", "Swamp", d);
        for (int i = 1; i <= 4; i++) { rec.Dealt(t0.AddSeconds(2 * i), new FoeId(1, 1), "Draugr", "$enemy_draugr", "Swamp", d); rec.Near(t0.AddSeconds(2 * i), "Edda", 2); }
        var before = rec.Fights()[0].With(); var at8 = rec.Version;
        rec.Near(t0.AddSeconds(10), "Edda", 2); rec.Near(t0.AddSeconds(10), "Finch", 2);
        var after = rec.Fights()[0].With();
        rec.Tick(t0.AddMinutes(3)); rec.Dealt(t0.AddMinutes(3), new FoeId(1, 2), "Boar", "$enemy_boar", "Meadows", d);   // the next fight: nobody near yet
        var next = rec.Fights()[0].With();
        // REVIEW-08 #7: a player who comes near after the last hit (the boar fell, Edda walks by 20 s later) is not in the fight, however long
        var quiet = new BattleRecorder(); quiet.Dealt(t0, new FoeId(1, 3), "Boar", "$enemy_boar", "Meadows", d);
        for (int i = 10; i <= 40; i += 2) quiet.Near(t0.AddSeconds(i + 10), "Edda", 2);
        var passerBy = quiet.Fights()[0].With();
        // on the page: the sample's last fight holds Edda and Tor (near for more than 10 s), not Finch (a moment only)
        var last = PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = PanelModel.LastFightPage } });
        var rows = Find(last, "grouprows");
        Check(before.Count == 0 && after.SequenceEqual(new[] { "Edda" }) && rec.Version > at8 && next.Count == 0 && rec.Fights()[1].NearFor("Edda") == 10 && passerBy.Count == 0 &&
              rows != null && rows.Items.Select(r => r.Title).SequenceEqual(new[] { "You", "Edda", "Tor" }) && PanelModel.AboutText(last).Contains("Finch was elsewhere"),
              "BF last fight: a fellow player is in the fight after 10 s near you (8 s: not yet), the page changes then; a moment near (Finch), near only after the last hit (REVIEW-08 #7) or the next fight starts without them; the page's rows are you, Edda and Tor");
        // REVIEW-08 #6: Edda reconnected after the fight: her copy is fresh, but her log starts with the new connection, so it holds no minute of
        // the fight: her row shows without numbers (never "0 · 0 %") and she is left out of "together"
        {
            var lastFight = PanelModel.BattleFeed(world, 1)[0]; var eddaBook = FullDump.Books(world).First(b => b.who == "Edda").book; var keepFirst = eddaBook.FirstRecordedUtc;
            var stayed = PanelModel.PartOf(eddaBook, lastFight).Has;
            try
            {
                eddaBook.FirstRecordedUtc = lastFight.EndUtc.AddMinutes(2);
                var rejoined = Find(PanelModel.Build(world, new PanelState { Chapter = Chapter.Battle, Page = { [Chapter.Battle] = PanelModel.LastFightPage } }), "grouprows")?.Items.FirstOrDefault(r => r.Id == "Edda");
                Check(stayed && !PanelModel.PartOf(eddaBook, lastFight).Has && rejoined?.Kind == PanelModel.GroupQuietKind && rejoined.Text == PanelModel.NoPartWords,
                      "BF REVIEW-08 #6: a fellow who reconnected after the fight (log from after its end) shows without numbers on Last fight (" + rejoined?.Kind + "), not as 0 %");
            }
            finally { eddaBook.FirstRecordedUtc = keepFirst; }
        }
        return fails;
    }
}
