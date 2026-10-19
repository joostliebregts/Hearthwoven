using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9.2 feat search (Joost: "how hard would it be to be able to search in this? Search title and description." then "Yes, add feat search (title and
    /// description, across Earned, Unsung and Together)"): whether the Feats pages' search field has the keys. Kept apart from Unity so the game's hooks
    /// (FeatSearchUi's patches on Chat.HasFocus and Minimap.InTextInput) and PanelUi.Typing read one answer, and the tests can set it.
    /// </summary>
    public static class FeatSearchInput
    {
        /// <summary>The field had the keys at the end of the last frame (FeatSearchUi.FeatSearchLate): every key of this frame is the field's, also the
        /// Esc or Enter that ends it (the field may let go of the keys before the game's own windows read them in this frame).</summary>
        public static bool FocusedLastFrame;
        /// <summary>The field has the keys now (set by FeatSearchUi; null outside the game).</summary>
        public static Func<bool> FocusedNow;

        /// <summary>True while the search field has the keys (this frame or the last): the panel's keys and the game's input stay off, as for chat.</summary>
        public static bool Typing
        {
            get
            {
                if (FocusedLastFrame) return true;
                try { return FocusedNow != null && FocusedNow(); } catch { return false; }
            }
        }

        /// <summary>What a key does while the field has the keys: Esc clears the search and leaves the field, Enter keeps it and leaves; anything else is
        /// the field's own (typing). The book never closes on these.</summary>
        public enum Act { Type, Clear, Keep }
        public static Act KeyAct(bool escape, bool enter) => escape ? Act.Clear : enter ? Act.Keep : Act.Type;
    }

    /// <summary>
    /// 0.9.2 feat search: a query typed in the Feats pages' search field filters live, case- and diacritic-insensitive (Sigrún, Åsa, Æsir), on what a
    /// card and its detail say: the feat's name, what it honours, the rule of every tier, its brief and its family's name. While a query is on, Earned,
    /// Unsung and Together are one grid, in the book's order (your earned feats, the unsung ones, the group's), each card tagged with its page; clearing
    /// the query gives the page back. A veiled feat (a land's riddle until someone who shares held its material) matches only on what its card shows:
    /// its riddle, never its hidden name or rule; a feat of a land not found is not on any page and matches nothing.
    /// The query lives in PanelState.View under <see cref="FeatSearchKey"/> (not saved with the prefs: RememberedViews does not list it).
    /// </summary>
    public static partial class PanelModel
    {
        public const string FeatSearchKey = "Feats/search", FeatSearchKind = "featsearch";
        public const string FeatSearchPlaceholder = "Search feats";
        /// <summary>The longest query the field takes (letters).</summary>
        public const int FeatSearchMax = 40;

        /// <summary>The query as typed ("" when none).</summary>
        public static string FeatQuery(PanelState s) => s != null && s.View.TryGetValue(FeatSearchKey, out var q) && q != null ? q : "";

        /// <summary>Sets the query (empty or blank: the search is off and the page is the page again).</summary>
        public static void SetFeatQuery(PanelState s, string query)
        {
            if (s == null) return;
            if (string.IsNullOrWhiteSpace(query)) s.View.Remove(FeatSearchKey);
            else s.View[FeatSearchKey] = query.Length > FeatSearchMax ? query.Substring(0, FeatSearchMax) : query;
        }

        /// <summary>The pages the search field stands on and searches across: Earned, Unsung and Together (not Titles).</summary>
        public static bool FeatSearchPage(string page) => page == FeatsPageId || page == FeatsUnsungId || page == FeatsTogetherId;

        /// <summary>True when the page shows the search's grid: a Feats page with a query of at least one word.</summary>
        public static bool FeatSearching(PanelState s, string page) => FeatSearchPage(page) && FeatTerms(FeatQuery(s)).Length > 0;

        /// <summary>A page's word on a card of the search's grid ("Earned", "Unsung", "Together"), as the left list says it.</summary>
        public static string FeatPageWord(string page) => FeatsList.FirstOrDefault(e => e.id == page).label ?? page;

        // ---------- folding: case and diacritics ----------

        /// <summary>
        /// Text as the search compares it: lower case, accents and other marks dropped (é e, å a), the letters that do not split into a base and a mark
        /// spelled out (ø o, æ ae, œ oe, ß ss, ð d, þ th, ł l), apostrophes dropped ("Fisher's" fishers), every other mark between words a space, and a
        /// space inside a number gone ("5 000" 5000, so "5000" finds it).
        /// </summary>
        public static string FoldText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var raw in s.Normalize(NormalizationForm.FormD))
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(raw);
                if (cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.EnclosingMark) continue;
                var c = char.ToLowerInvariant(raw);
                switch (c)
                {
                    case 'ø': sb.Append('o'); continue;
                    case 'æ': sb.Append("ae"); continue;
                    case 'œ': sb.Append("oe"); continue;
                    case 'ß': sb.Append("ss"); continue;
                    case 'ð': case 'đ': sb.Append('d'); continue;
                    case 'þ': sb.Append("th"); continue;
                    case 'ł': sb.Append('l'); continue;
                    case 'ı': sb.Append('i'); continue;
                    case '\'': case '’': case '‘': continue;
                }
                sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
            // a space between two digits is a number's thousands gap ("5 000"): gone; runs of spaces one
            var t = sb.ToString(); var o = new StringBuilder(t.Length);
            for (int k = 0; k < t.Length; k++)
            {
                if (t[k] == ' ')
                {
                    if (o.Length == 0 || o[o.Length - 1] == ' ') continue;
                    var next = k + 1; while (next < t.Length && t[next] == ' ') next++;
                    if (char.IsDigit(o[o.Length - 1]) && next < t.Length && char.IsDigit(t[next])) continue;
                }
                o.Append(t[k]);
            }
            return o.ToString().Trim();
        }

        /// <summary>The query's words, folded (FoldText): a card matches when its text holds every one of them.</summary>
        public static string[] FeatTerms(string query) => FoldText(query).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // ---------- what a feat is searched on ----------

        /// <summary>
        /// The text one feat is searched on, folded: what its card and detail show in this book. Its name, what it honours, its family's name, and the rule
        /// and the brief of every tier. A veiled feat: its riddle's name, honours, rule and brief, and only the tiers its detail shows (those reached and the
        /// next), never its hidden name or text. Words for whose book it is ("you", "they") as the page says them.
        /// </summary>
        public static string FeatSearchText(FeatDef d, PanelInput input)
        {
            var parts = new List<string>();
            void Add(Func<string> part) { try { var p = part(); if (!string.IsNullOrEmpty(p)) parts.Add(p); } catch { } }   // a feat's words that throw are left out, never the page
            Add(() => FeatTitle(d, input));
            Add(() => FeatWords(input, FeatHonours(d, input)));
            Add(() => FeatFamilyName(d.Family));
            var veiled = false; try { veiled = FeatVeiled(d, input); } catch { veiled = true; }   // in doubt, the riddle
            var shown = d.Tiers.Length;
            if (veiled) { var tier = 0; try { tier = FeatTier(d, input); } catch { } shown = Math.Min(d.Tiers.Length, tier + 1); }
            Func<double, string> rule = null, brief = null;
            try { rule = FeatRule(d, input); brief = FeatBriefOf(d, input); } catch { }
            for (int k = 0; k < shown; k++)
            {
                var at = d.Tiers[k];
                if (rule != null) Add(() => FeatWords(input, rule(at)));
                if (brief != null) Add(() => FeatWords(input, brief(at)));
            }
            return FoldText(string.Join(" \n ", parts));
        }

        /// <summary>True when every word of the query is in the feat's searched text (FeatSearchText).</summary>
        public static bool FeatMatches(FeatDef d, PanelInput input, string query)
        {
            var terms = FeatTerms(query);
            if (terms.Length == 0) return false;
            var text = FeatSearchText(d, input);
            return terms.All(t => text.Contains(t));
        }

        /// <summary>
        /// Every feat the query finds across the three pages, each with the page its card stands on, in the book's order: Earned (your feats with a tier,
        /// in the table's order), then Unsung (the ones without), then Together (the group's, in Together's order). Only the feats a page shows: a land's
        /// own feat or a group feat of a land the group has not found is on no page and is never found.
        /// </summary>
        public static List<(FeatDef feat, string page)> FeatSearchHits(PanelInput input, string query)
        {
            var hits = new List<(FeatDef, string)>();
            if (input == null || FeatTerms(query).Length == 0) return hits;
            return WithFeatMemo(() =>
            {
                var own = FeatDefs.Where(d => !d.Group && OwnVisible(d, input)).ToList();
                foreach (var d in own.Where(d => FeatTier(d, input) > 0)) if (FeatMatches(d, input, query)) hits.Add((d, FeatsPageId));
                foreach (var d in own.Where(d => FeatTier(d, input) == 0)) if (FeatMatches(d, input, query)) hits.Add((d, FeatsUnsungId));
                foreach (var d in TogetherFeats(input, out _)) if (FeatMatches(d, input, query)) hits.Add((d, FeatsTogetherId));
                return hits;
            });
        }

        /// <summary>
        /// The search's grid: one card per feat found (FeatSearchHits), as its own page draws it. As a page (0.9.2), no card is chosen until one is (a
        /// click, A/D): a card chosen before stays chosen while the search finds it, else none and no detail area. A New card keeps its mark (your own book).
        /// </summary>
        static List<Block> FeatSearchCards(PanelInput input, PanelState state)
        {
            var hits = FeatSearchHits(input, FeatQuery(state));
            var pick = hits.Any(h => h.feat.Id == state.FeatSel) ? state.FeatSel : null;
            state.FeatSel = pick ?? "";
            var fresh = new HashSet<string>();
            if (input != null && input.IsSelf && input.Feats?.SeenTiers != null) { fresh.UnionWith(NewFeats(input, false)); fresh.UnionWith(NewFeats(input, true)); }
            return hits.Select(h => { var c = FeatCard(input, h.feat, pick); c.New = h.feat.Id != state.FeatOpen && fresh.Contains(h.feat.Id); return c; }).ToList();
        }

        /// <summary>The count line while a query is on: "7 feats match 'iron'", "1 feat matches 'iron'", "No feat matches 'xyz'".</summary>
        public static string FeatSearchLine(int n, string query)
        {
            var q = "'" + (query ?? "").Trim() + "'";
            return n <= 0 ? "No feat matches " + q : n == 1 ? "1 feat matches " + q : N(n) + " feats match " + q;
        }

        /// <summary>The search field's block on a Feats page (null on any other page).</summary>
        public static Block FeatSearchBlock(PanelView v) => PlateOf(v)?.Items?.FirstOrDefault(b => b.Kind == FeatSearchKind);

        /// <summary>
        /// The search on a Feats page (FeatsChapter's last step): the field's block at the plate's top (Kind "featsearch": Text the query, Title the count
        /// line or null; while a query is on, Items one per card in the grid's order: Id the feat, Tone its page, Title the page's word, the tag on its card).
        /// While a query is on, the page's line (wherever FeatsIntro finds it) says the count, the heading is "Feats", and the page's empty note goes (the
        /// count line says there is no match).
        /// </summary>
        static void FeatSearchFinish(PanelInput input, string page, PanelView view, PanelState state)
        {
            if (!FeatSearchPage(page)) return;
            var plate = PlateOf(view); if (plate == null) return;
            if (plate.Items == null) plate.Items = new List<Block>();
            var query = FeatQuery(state);
            var field = new Block { Kind = FeatSearchKind, Id = FeatSearchKey, Text = query, Items = new List<Block>() };
            if (FeatTerms(query).Length > 0)
            {
                var cards = plate.Items.FirstOrDefault(b => b.Kind == "feats")?.Items ?? new List<Block>();
                foreach (var c in cards)
                {
                    var d = FeatById(c.Id); if (d == null) continue;
                    var on = d.Group ? FeatsTogetherId : c.Level > 0 ? FeatsPageId : FeatsUnsungId;
                    field.Items.Add(new Block { Kind = "tag", Id = c.Id, Tone = on, Title = FeatPageWord(on) });
                }
                field.Title = FeatSearchLine(cards.Count, query);
                view.Heading = FeatsHeading;
                // the count where the page's line is: the plate's own line, or the block that carries it (FeatsIntro)
                var intro = FeatsIntro(view);
                var carrier = intro == null || plate.Text == intro ? null : plate.Items.FirstOrDefault(b => b.Kind == "switch" && b.Title == intro);
                if (carrier != null) carrier.Title = field.Title;
                else { plate.Text = field.Title; FeatsIntroInStrip(plate); }   // no match: the line in the strip still, so the field stays where it was
                plate.Items.RemoveAll(b => b.Kind == "note" && (b.Text == FeatsNone || b.Text == FeatsAllEarned));
            }
            plate.Items.Insert(0, field);
        }
    }
}
