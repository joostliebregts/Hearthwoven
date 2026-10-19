// 0.9.2 feat search (Chapters/FeatSearch.cs, FeatSearchUi.cs; Joost: "Yes, add feat search (title and description, across Earned, Unsung and
// Together)"): the matching rules (name, what it honours, every tier's rule, the brief, the family; case and diacritics; a veiled feat only on its
// riddle), the one grid across the pages in the book's order with each card's page tag, the count line, and the field counting as typing.
// Called from Program.cs; the previews (feats-search-*) come from DumpShots.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FeatSearchTests
{
    static PanelState At(string page, string query, string player = "")
    {
        var s = new PanelState { Chapter = Chapter.Feats, Player = player, Page = { [Chapter.Feats] = page } };
        PanelModel.SetFeatQuery(s, query);
        return s;
    }
    static PanelState Chosen(PanelState s, string id) { s.FeatSel = id; return s; }
    static List<Block> Cards(PanelView v) => PanelModel.VisibleFeats(v);
    static List<string> Ids(PanelView v) => Cards(v).Select(c => c.Id).ToList();
    static int Rank(string page) => page == PanelModel.FeatsPageId ? 0 : page == PanelModel.FeatsUnsungId ? 1 : 2;

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var (full, books) = FeatsTests.World();
        FeatDef Def(string id) => PanelModel.FeatById(id);

        // ---------- folding: case and diacritics ----------
        Check(PanelModel.FoldText("Sigrún ÅSA Æsir Þór Ðel Straße Œuvre Łodz") == "sigrun asa aesir thor del strasse oeuvre lodz",
              "fold: lower case, marks dropped, and the letters that do not split spelled out (ø o, æ ae, þ th, ð d, ß ss, œ oe, ł l): '" + PanelModel.FoldText("Sigrún ÅSA Æsir Þór Ðel Straße Œuvre Łodz") + "'");
        Check(PanelModel.FoldText("Feast-Giver") == "feast giver" && PanelModel.FoldText("Fisher's  catch") == "fishers catch" && PanelModel.FoldText("Put 20 000 ore") == "put 20000 ore" && PanelModel.FoldText("5 000 item-km") == "5000 item km",
              "fold: a mark between words is a space, an apostrophe goes, a number's thousands gap goes (\"5 000\" finds \"5000\")");
        Check(PanelModel.FeatTerms("  Iron   FORGE ").SequenceEqual(new[] { "iron", "forge" }) && PanelModel.FeatTerms("  ").Length == 0 && PanelModel.FeatTerms(null).Length == 0,
              "terms: the query's words, folded; blank is no search");

        // ---------- what a feat is found by ----------
        Check(PanelModel.FeatMatches(Def("feastgiver"), full, "FEAST") && PanelModel.FeatMatches(Def("feastgiver"), full, "fëast-gïver"), "match: the name, any case, any accents (\"fëast-gïver\" finds Feast-Giver)");
        Check(PanelModel.FeatMatches(Def("fulltable"), full, "whole group sat"), "match: what it honours (Full Table: 'A table the whole group sat at.')");
        Check(PanelModel.FeatMatches(Def("keptfires"), full, "20000 smelters") && PanelModel.FeatMatches(Def("keptfires"), full, "20 000"), "match: a later tier's rule (Kept the Fires III: 'Put 20 000 ore and fuel into smelters')");
        Check(PanelModel.FeatMatches(Def("stoodfast"), full, "damage stopped"), "match: the brief (Stood Fast: '10 000 damage stopped')");
        Check(PanelModel.FeatMatches(Def("arms"), full, "forgekeeper") && !PanelModel.FeatMatches(Def("shieldwall"), full, "forgekeeper"), "match: the family's name (Forgekeeper finds Arms for the Hall, not Shield Wall)");
        Check(PanelModel.FeatMatches(Def("feastgiver"), full, "feast servings") && !PanelModel.FeatMatches(Def("feastgiver"), full, "feast iron"), "match: every word of the query must be there");
        Check(!PanelModel.FeatSearchText(Def("shieldwall"), full).Contains("{"), "match: the page's own words, never a placeholder ('{you}' said as 'you')");

        // ---------- a veiled feat: only what its card shows ----------
        var early = FeatsSample.EarlyGroup(FeatsTests.Now);
        var copper = Def("copperwed");
        Check(PanelModel.FeatVeiled(copper, early), "veiled: Copper Wed to Tin is under its riddle in the early group (no copper or tin held yet)");
        Check(!PanelModel.FeatMatches(copper, early, "copper") && !PanelModel.FeatMatches(copper, early, "bronze") && !PanelModel.FeatMatches(copper, early, "tin"),
              "veiled: its hidden name and rule are never found ('copper', 'bronze', 'tin')");
        Check(PanelModel.FeatMatches(copper, early, "deep woods") && PanelModel.FeatMatches(copper, early, "worth the fire"), "veiled: its riddle is found ('What the Deep Woods Hold', what it honours)");
        var earlyHits = PanelModel.FeatSearchHits(early, "copper");
        Check(earlyHits.All(h => h.feat.Id != "copperwed"), "veiled: a search for 'copper' on the early group's book leaves Copper Wed to Tin out (" + earlyHits.Count + " other hits)");
        Check(PanelModel.FeatSearchHits(early, "bog").All(h => h.feat.Id != "bogiron"), "unfound land: a group feat of a land the group has not found (the Swamp's) is on no page and never found");
        Check(!PanelModel.FeatVeiled(copper, full) && PanelModel.FeatSearchHits(full, "copper").Any(h => h.feat.Id == "copperwed"), "veil lifted: once the group held copper, 'copper' finds it");

        // ---------- one grid across the pages ----------
        var q = "iron";
        var hits = PanelModel.FeatSearchHits(full, q);
        var pages = hits.Select(h => h.page).Distinct().ToList();
        Check(pages.Count == 3, "grid: '" + q + "' finds feats on all three pages (" + string.Join(", ", hits.GroupBy(h => h.page).Select(g => g.Key + " " + g.Count())) + ")");
        Check(hits.Select(h => Rank(h.page)).Zip(hits.Select(h => Rank(h.page)).Skip(1), (a, b) => a <= b).All(x => x), "grid: Earned first, then Unsung, then Together");
        var ownOrder = PanelModel.FeatDefs.Where(d => !d.Group).Select(d => d.Id).ToList();
        var earnedIds = hits.Where(h => h.page == PanelModel.FeatsPageId).Select(h => ownOrder.IndexOf(h.feat.Id)).ToList();
        Check(earnedIds.Zip(earnedIds.Skip(1), (a, b) => a < b).All(x => x), "grid: within a page the table's order");
        Check(hits.All(h => h.page == (h.feat.Group ? PanelModel.FeatsTogetherId : PanelModel.FeatTier(h.feat, full) > 0 ? PanelModel.FeatsPageId : PanelModel.FeatsUnsungId)),
              "grid: each feat on the page it stands on (the group's on Together, a tier earned on Earned, the rest Unsung)");
        var fromEarned = PanelModel.Build(full, At("earned", q)); var fromUnsung = PanelModel.Build(full, At("unsung", q)); var fromTogether = PanelModel.Build(full, At("together", q));
        Check(Ids(fromEarned).SequenceEqual(hits.Select(h => h.feat.Id)) && Ids(fromUnsung).SequenceEqual(Ids(fromEarned)) && Ids(fromTogether).SequenceEqual(Ids(fromEarned)),
              "grid: the same " + Ids(fromEarned).Count + " cards from Earned, Unsung or Together, in the hits' order");
        var field = PanelModel.FeatSearchBlock(fromUnsung);
        Check(field != null && field.Text == q && field.Items.Select(t => t.Id).SequenceEqual(Ids(fromUnsung)) && field.Items.All(t => t.Title == PanelModel.FeatPageWord(t.Tone))
              && field.Items.Select(t => t.Tone).SequenceEqual(hits.Select(h => h.page)) && new[] { "Earned", "Unsung", "Together" }.All(w => field.Items.Any(t => t.Title == w)),
              "tags: one per card, in the grid's order, each its page's word (Earned, Unsung, Together)");
        Check(field.Title == hits.Count + " feats match '" + q + "'" && PanelModel.FeatsIntro(fromUnsung) == field.Title && fromUnsung.Heading == PanelModel.FeatsHeading,
              "count: \"" + field.Title + "\" says the count where the page's line was, the heading is 'Feats'");
        Check(!Cards(fromUnsung).Any(c => c.Selected) && !PanelModel.Content(fromUnsung).Any(b => b.Kind == "featdetail"), "grid: as a page, no card chosen and no detail area until one is chosen");
        var kept = At("together", q); kept.FeatSel = hits.Last().feat.Id; var keptView = PanelModel.Build(full, kept);
        Check(Cards(keptView).Single(c => c.Selected).Id == hits.Last().feat.Id && PanelModel.Content(keptView).Count(b => b.Kind == "featdetail") == 1, "grid: a feat chosen before stays chosen (its detail shown) while the search finds it");
        var gone = At("together", q); gone.FeatSel = "shieldwall";
        Check(!Cards(PanelModel.Build(full, gone)).Any(c => c.Selected) && gone.FeatSel == "", "grid: a feat chosen before that the search does not find is let go");
        Check(PanelModel.FeatSearchLine(1, "iron") == "1 feat matches 'iron'" && PanelModel.FeatSearchLine(7, " iron ") == "7 feats match 'iron'", "count: one feat, many feats");

        // ---------- no match, and clearing ----------
        var none = PanelModel.Build(full, At("earned", "xyzzy"));
        Check(Cards(none).Count == 0 && PanelModel.FeatsIntro(none) == "No feat matches 'xyzzy'" && PanelModel.FeatSearchBlock(none)?.Title == "No feat matches 'xyzzy'"
              && !PanelModel.Content(none).Any(b => b.Kind == "note" && (b.Text == PanelModel.FeatsNone || b.Text == PanelModel.FeatsAllEarned)),
              "no match: \"No feat matches 'xyzzy'\" and no grid (the page's own empty note goes)");
        foreach (var page in new[] { "earned", "unsung", "together" })
        {
            var plain = PanelModel.Build(full, At(page, null)); var blank = PanelModel.Build(full, At(page, "   "));
            var off = PanelModel.FeatSearchBlock(plain);
            Check(off != null && string.IsNullOrEmpty(off.Text) && off.Title == null && off.Items.Count == 0 && Ids(blank).SequenceEqual(Ids(plain)) && PanelModel.FeatsIntro(blank) == PanelModel.FeatsIntro(plain)
                  && plain.Heading != PanelModel.FeatsHeading,
                  "clear (" + page + "): no query or a blank one is the page itself, its field empty (" + Ids(plain).Count + " cards, its own line)");
        }
        var cleared = At("unsung", q); PanelModel.SetFeatQuery(cleared, "");
        Check(!cleared.View.ContainsKey(PanelModel.FeatSearchKey) && Ids(PanelModel.Build(full, cleared)).SequenceEqual(Ids(PanelModel.Build(full, At("unsung", null)))), "clear: an empty query gives the page back");
        Check(PanelModel.FeatSearchBlock(PanelModel.Build(full, At("titles", null))) == null && Ids(PanelModel.Build(full, At("titles", "iron"))).SequenceEqual(Ids(PanelModel.Build(full, At("titles", null)))),
              "titles: the Titles page has no field and never filters");
        Check(!PanelModel.RememberedViews.Contains(PanelModel.FeatSearchKey), "prefs: the query is never saved with the character's choices");
        var longQ = new string('a', PanelModel.FeatSearchMax + 10); var st = new PanelState(); PanelModel.SetFeatQuery(st, longQ);
        Check(PanelModel.FeatQuery(st).Length == PanelModel.FeatSearchMax, "query: at most " + PanelModel.FeatSearchMax + " letters");

        // ---------- a fellow's book ----------
        var tor = books["Tor"];
        var torView = PanelModel.Build(tor, At("earned", "fellow", "Tor"));
        Check(Cards(torView).Count > 0 && Cards(torView).All(c => !(c.Title ?? "").Contains("{")), "fellow: the search works on a fellow's book in their words (" + Cards(torView).Count + " found)");

        // ---------- the field counts as typing ----------
        var was = (FeatSearchInput.FocusedLastFrame, FeatSearchInput.FocusedNow);
        try
        {
            FeatSearchInput.FocusedLastFrame = false; FeatSearchInput.FocusedNow = null;
            var idle = FeatSearchInput.Typing;
            FeatSearchInput.FocusedNow = () => true; var now = FeatSearchInput.Typing;
            FeatSearchInput.FocusedNow = () => false; FeatSearchInput.FocusedLastFrame = true; var last = FeatSearchInput.Typing;
            FeatSearchInput.FocusedLastFrame = false; FeatSearchInput.FocusedNow = () => throw new InvalidOperationException("gone"); var broken = FeatSearchInput.Typing;
            Check(!idle && now && last && !broken, "typing: true while the field has the keys, now or at the end of the last frame (the Esc or Enter that ends it is still the field's); false when idle or when the field is gone");
        }
        finally { FeatSearchInput.FocusedLastFrame = was.Item1; FeatSearchInput.FocusedNow = was.Item2; }
        Check(FeatSearchInput.KeyAct(true, false) == FeatSearchInput.Act.Clear && FeatSearchInput.KeyAct(false, true) == FeatSearchInput.Act.Keep && FeatSearchInput.KeyAct(false, false) == FeatSearchInput.Act.Type
              && FeatSearchInput.KeyAct(true, true) == FeatSearchInput.Act.Clear,
              "keys: Esc clears and leaves, Enter keeps and leaves, every other key is typing");
        return fails;
    }

    /// <summary>The previews (test-panel --dump): a query with hits on all three pages, one with none, and a veiled feat that its hidden name does not find
    /// (and its riddle does).</summary>
    public static string DumpShots(string[] group, DateTime at)
    {
        var (full, _) = FeatsTests.World(at);
        var early = FeatsSample.EarlyGroup(at);
        var shots = new (string name, PanelInput inp, PanelState st)[]
        {
            ("feats-search-hits", full, At("earned", "iron")),
            ("feats-search-none", full, At("unsung", "xyzzy")),
            ("feats-search-veiled", early, At("together", "copper")),
            ("feats-search-riddle", early, Chosen(At("together", "make"), "copperwed")),   // the riddle chosen: its detail names no material either
        };
        return string.Join(",\n", shots.Select(s =>
        {
            var v = PanelModel.Build(s.inp, s.st);
            PanelModel.AddPlayers(v, s.inp == early ? s.inp.PlayerName : "Rowan", s.inp == early ? new[] { s.inp.PlayerName } : group, s.st.Player, true);
            return "  \"" + s.name + "\": " + PanelModel.ToJson(v, islands: true);
        }));
    }
}
