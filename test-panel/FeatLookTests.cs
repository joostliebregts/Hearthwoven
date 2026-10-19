// 0.9 feat look (work/hearthwoven-0.9/TRACK-featlook.md, Chapters/FeatLook.cs): the families' colours against VOCABULARY.md's rule (printed), the
// cards dressed for the look, the mirror's JSON, (Joost in game, 18 Oct) a hover never chooses a feat: only a click or A/D, and 0.9.2 (Joost: "Let's
// stick with Look 2 and remove look 1"; "show me the full list before I select anything"): one look, no chips, no config entry, the page's line across
// the strip; no card chosen when a page opens, a click on the chosen card or Backspace closes it again; the More below cue over the grid's foot.
// Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FeatLookTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        string F(double d) => d.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        // ---------- the families' colours (VOCABULARY.md: no tone darker than about CIELAB L 45, each pair at least CIEDE2000 15 apart; apart from the tiers) ----------
        var fams = PanelModel.FeatFamilies;
        var tiers = PanelModel.TierColours.Skip(1).ToArray();
        var (full0, _) = FeatsTests.World();
        System.Console.WriteLine("  family colours (CIELAB L, CIEDE2000 to bronze / silver / gold):");
        foreach (var f in fams)
            System.Console.WriteLine("    " + f.id.PadRight(9) + f.colour + "  L " + F(PanelModel.Lightness(f.colour)) + "  " + string.Join(" / ", tiers.Select(t => F(PanelModel.ColourDistance(f.colour, t)))));
        var pairs = fams.SelectMany((a, i) => fams.Skip(i + 1).Select(b => (a: a.id, b: b.id, d: PanelModel.ColourDistance(a.colour, b.colour)))).OrderBy(p => p.d).ToList();
        System.Console.WriteLine("  closest pairs: " + string.Join(", ", pairs.Take(4).Select(p => p.a + "/" + p.b + " " + F(p.d))));
        Check(fams.All(f => PanelModel.Lightness(f.colour) >= 45), "families: no colour darker than CIELAB L 45 (darkest " + F(fams.Min(f => PanelModel.Lightness(f.colour))) + ")");
        Check(pairs[0].d >= 15, "families: every pair at least CIEDE2000 15 apart (closest " + pairs[0].a + "/" + pairs[0].b + " " + F(pairs[0].d) + ")");
        var nearTier = fams.SelectMany(f => tiers.Select(t => PanelModel.ColourDistance(f.colour, t))).Min();
        Check(nearTier >= 15, "families: each at least CIEDE2000 15 from bronze, silver and gold (closest " + F(nearTier) + ")");
        Check(fams.Select(f => f.id).Distinct().Count() == fams.Length && PanelModel.FeatDefs.All(d => fams.Any(f => f.id == d.Family)),
              "families: one colour each, and every feat's family has one (" + fams.Length + ")");
        Check(fams.All(f => f.hatch.StartsWith("vocab:grain-hatch-", StringComparison.Ordinal)), "families: each ground is one of the vocabulary's grain-hatch tiles");

        // ---------- legibility (0.8.93, Joost in game): text keeps 4.5:1 on its ground, the family's ground never under the text ----------
        {
            var inks = PanelModel.FeatLookInks.Concat(tiers).Concat(fams.Select(f => f.colour)).ToList();
            var worst = inks.SelectMany(i => PanelModel.FeatLookTextGrounds.Select(g => (i, g, c: PanelModel.Contrast(i, g)))).OrderBy(x => x.c).First();
            System.Console.WriteLine("  text on the card's ground: lowest " + worst.i + " on " + worst.g + " " + F(worst.c) + ":1");
            Check(worst.c >= 4.5, "legibility: every ink of the looks (text, muted, faint, gold, accent, tiers, families) at least 4.5:1 on the card's ground (lowest " + worst.i + " " + F(worst.c) + ":1)");
            var stops = FeatLookShape.GroundStops;
            Check(FeatLookShape.Ramp(stops, 0.8f) >= 1f && FeatLookShape.Ramp(stops, 1f) >= 1f && FeatLookShape.Ramp(FeatLookShape.WashStops, 0.6f) <= 0f,
                  "legibility: the family's ground fades across the card as board 9's (the card's own ground from 80 %), its wash by 60 %");
            // 0.9.1 (Joost: the full-card pattern, legible in game): the worst pixel under the text (a hatch line of the family's colour at full coverage,
            // after the fade and the band at the text column's left edge) against every ink, for every family, the card and the detail, at the defaults
            var cardW = (float)System.Math.Floor((PanelLayout.Plate.Column - 2 * PanelLayout.Feats.ColumnGap) / 3);
            var cardX = (PanelLayout.FeatLook.IconLeftLarge + PanelLayout.FeatLook.Edge + PanelLayout.FeatLook.IconLarge + PanelLayout.FeatLook.IconGap) / cardW;
            var detailX = 84f / PanelLayout.FeatLook.DetailGroundW;
            var under = fams.SelectMany(f => new[] { cardX, detailX }.Select(x => (f.id, x, bg: PanelModel.FeatUnderText(f.colour, FeatLookShape.PatternDefault, FeatLookShape.BackingDefault, x)))).ToList();
            var worstUnder = under.SelectMany(u => PanelModel.FeatLookInks.Select(i => (u.id, u.x, u.bg, i, c: PanelModel.Contrast(i, u.bg)))).OrderBy(x => x.c).First();
            System.Console.WriteLine("  text over the pattern (defaults: strength " + FeatLookShape.PatternDefault + ", " + FeatLookShape.BackingDefault + "): lowest " + worstUnder.i + " on " + worstUnder.bg +
                                     " (" + worstUnder.id + " at " + F(worstUnder.x * 100) + " %) " + F(worstUnder.c) + ":1; a line shows at most " + F(FeatLookShape.UnderText(FeatLookShape.PatternDefault, true, cardX) * 100) + " % under a card's text");
            Check(worstUnder.c >= 4.5, "legibility: every ink at least 4.5:1 on the worst pixel under the text, every family, the card and the detail, at the defaults (lowest " + F(worstUnder.c) + ":1)");
            var bare = fams.SelectMany(f => PanelModel.FeatLookInks.Select(i => PanelModel.Contrast(i, PanelModel.FeatUnderText(f.colour, FeatLookShape.PatternDefault, FeatBacking.None, cardX)))).Min();
            System.Console.WriteLine("  the same without the band (TextBacking None or Shadow): lowest " + F(bare) + ":1 (the shade on the letters is not counted)");
            Check(FeatLookShape.Veil(0.7f) > 0.9f && FeatLookShape.Ink(0.67f) < 0.45f && System.Math.Abs(FeatLookShape.HatchLine(100) - 0.67f) < 0.01f && FeatLookShape.HatchLayerTint(0) == 0f && FeatLookShape.HatchLayerTint(100) <= 1f,
                  "game: a dark layer is drawn darker and a light one fainter than the mirror's alpha (linear blending: 0.7 -> " + F(FeatLookShape.Veil(0.7f)) + ", 0.67 -> " + F(FeatLookShape.Ink(0.67f)) + "); strength 0 draws no hatch");
            var root0 = AppContext.BaseDirectory; while (root0 != null && !System.IO.File.Exists(System.IO.Path.Combine(root0, "src", "Plugin.cs"))) root0 = System.IO.Path.GetDirectoryName(root0);
            var html0 = System.IO.File.ReadAllText(System.IO.Path.Combine(root0, "src", "Panel", "preview", "panel-preview.html"));
            Check(html0.Contains("rgba(12,12,10,.25) 22%, rgba(12,12,10,.85) 58%, rgb(12,12,10) 80%") && html0.Contains("const LK_STRENGTH = " + FeatLookShape.PatternDefault + ", LK_TILE = " + FeatLookShape.TileAlpha.ToString(System.Globalization.CultureInfo.InvariantCulture)) &&
                  html0.Contains("rgba(12,12,10," + FeatLookShape.Backing.ToString(".0#", System.Globalization.CultureInfo.InvariantCulture).TrimStart('0') + ") var(--feat-look-backing-in)") && html0.Contains("text-shadow"),
                  "mirror: the same fade, strength, band and shade as the game's defaults");
            // 0.8.96 (Joost in game, 0.8.95: Unsung cards showed no words): a card's stack of one-line texts and its bar fits the card's least height in
            // line boxes (PanelLayout.LineBox), so Unity's layout never squeezes a text under its font's line, where TMP's ellipsis drops it whole
            float Lb(float size) => PanelLayout.LineBox(size);
            var stack = Lb(PanelLayout.Text.Small) + Lb(PanelLayout.Text.Floor) + PanelLayout.FeatLook.BarH + PanelLayout.FeatLook.BarTop;
            Check(stack <= PanelLayout.FeatLook.CardMin, "fit: a card's lines fit its least height in line boxes (" + F(stack) + " of " + F(PanelLayout.FeatLook.CardMin) + " px)");
            // the feat's own name reads first on every card (Joost: "The turned blades, the shield wall, the stood fast, like that should be the name");
            // the family word stays only in the detail area, over the name
            var root = AppContext.BaseDirectory;
            while (root != null && !System.IO.File.Exists(System.IO.Path.Combine(root, "src", "Plugin.cs"))) root = System.IO.Path.GetDirectoryName(root);
            if (root == null) Check(false, "names: source folder not found");
            else
            {
                var ui = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "src", "Panel", "Chapters", "FeatLookUi.cs"));
                var html = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "src", "Panel", "preview", "panel-preview.html"));
                var at = ui.IndexOf("static void FeatLookCard(", StringComparison.Ordinal); var end = at < 0 ? -1 : ui.IndexOf("\n        }", at, StringComparison.Ordinal);
                var card = at < 0 || end < 0 ? "" : System.Text.RegularExpressions.Regex.Replace(ui.Substring(at, end - at), @"//[^\n]*", "");
                var mirror = System.Text.RegularExpressions.Regex.Match(html, @"function lookCard\([\s\S]*?\n}").Value;
                var firstText = System.Text.RegularExpressions.Regex.Match(card, @"\b(?:Rich)?Label\(stack,\s*([^,]+),").Groups[1].Value;
                Check(card.Length > 0 && !card.Contains("c.Family") && firstText == "\"feats\"" && card.Contains("LkLine(name") && card.Contains("LkLine(line") &&
                      mirror.Length > 0 && !mirror.Contains("lkfam") && mirror.Contains("<div class=\"lkt\"><div class=\"fn\">") && ui.Contains("static void FeatLookFamilyLine("),
                      "names: the card leads with the feat's name, no family word on it (game and mirror), each line in its line box; the detail keeps the family line");
                // 0.9.2: look 1 is gone from the game and the mirror (no frame, no badges, no small scene, no chips, no config entry)
                var feats = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "src", "Panel", "Chapters", "FeatsUi.cs")) + ui + System.IO.File.ReadAllText(System.IO.Path.Combine(root, "src", "Panel", "PanelUi.cs"));
                Check(!feats.Contains("TierFrame(") && !feats.Contains("TierBadges(") && !feats.Contains("\"FeatLook\"") && !feats.Contains("SyncFeatLook") &&
                      !html.Contains("lkFrame") && !html.Contains("class=\"lkb\"") && !html.Contains("look === 1"),
                      "look 1 gone: no tier frame, no badges, no Panel.FeatLook entry and no look sync in the game; no frame or badges in the mirror");
            }
            Check(PanelModel.Islands(PanelModel.PlateOf(PanelModel.Build(full0, new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = PanelModel.FeatsPageId } })))
                      .SelectMany(i => i.Parts).All(p => p.Block.Kind != "featband"),
                  "strip: a Feats page draws its strip once (the full plate's plan leaves it out), so the page's line in it shows once");
        }

        // ---------- one look, the page's line in the strip (0.9.2) ----------
        var (full, _) = FeatsTests.World();
        PanelState At(string page) => new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = page } };
        foreach (var page in new[] { PanelModel.FeatsPageId, PanelModel.FeatsUnsungId, PanelModel.FeatsTogetherId })
        {
            var v = PanelModel.Build(full, At(page));
            var cap = PanelModel.Content(v).FirstOrDefault(b => b.Kind == "switch" && b.Id == PanelModel.FeatsIntroKey);
            Check(cap != null && cap.Title == PanelModel.FeatsIntro(v) && PanelModel.PlateOf(v).Text == null && (cap.Items ?? new List<Block>()).Count == 0 && PanelModel.TopSwitch(PanelModel.PlateOf(v)) == cap,
                  "strip: " + page + "'s line rides in the strip, no Look chips beside it (" + cap?.Title?.Length + " letters, at most " + PanelModel.FeatsIntroMax + ")");
            Check(PanelModel.SwitchOf(v) == null && !v.Keys.Any(k => k.Contains("View")), "strip: " + page + ": the view key has nothing to flip (no [F] View on the Feats chapter)");
            var g = PanelModel.Content(v).First(b => b.Kind == "feats");
            Check(g.Look == PanelModel.FeatLookOn && g.Items.All(c => c.Family != null && c.Ground != null && c.Pattern != null && !string.IsNullOrEmpty(c.Line) && c.Items[0].Look == PanelModel.FeatLookOn && c.Items[0].Family == c.Family),
                  "cards: " + page + "'s cards carry the family, its colour and hatch, one short line, and their details the look");
            Check(PanelModel.ToJson(v).Contains("\"look\":" + PanelModel.FeatLookOn) && PanelModel.ToJson(v).Contains("\"family\":\""), "mirror: " + page + " writes the look and the family for the preview");
        }
        {
            var v = PanelModel.Build(full, At(PanelModel.FeatsPageId));
            var kept = PanelModel.VisibleFeats(v).FirstOrDefault(c => c.Id == "keptfires");
            Check(kept == null || (kept.Family == "Forgekeeper" && kept.Ground == PanelModel.FeatFamily("smith").colour), "cards: Kept the Fires is the Forgekeeper's, in its colour");
            var earned = PanelModel.VisibleFeats(v).Where(c => c.Tone == PanelModel.FeatTone).ToList();
            Check(earned.Count > 0 && earned.All(c => c.Line.Length <= 30), "cards: the short line stays short (at most 30 letters; longest \"" + earned.OrderByDescending(c => c.Line.Length).First().Line + "\")");
            var titles = PanelModel.Build(full, At(PanelModel.TitlesPageId));
            Check(PanelModel.Content(titles).First(b => b.Kind == "feats").Look == 0 && PanelModel.SwitchOf(titles)?.Id != PanelModel.FeatsIntroKey, "titles: the Titles page keeps its cards");
            Check(PanelModel.FeatScene("drover", "vocab:lead-rope", null) == "vocab:lead-rope" && PanelModel.FeatScene("drover", "x", n => n == "feat-scene-drover") == "vocab:feat-scene-drover",
                  "cards: a feat's large picture is its woodcut scene once drawn (vocab:feat-scene-<id>), else its emblem");
        }

        // ---------- 0.9.2 the full list first: no card chosen when a page opens, a click opens one, a click on it or Backspace closes it ----------
        foreach (var page in new[] { PanelModel.FeatsPageId, PanelModel.FeatsUnsungId, PanelModel.FeatsTogetherId })
        {
            var s = At(page); var v = PanelModel.Build(full, s);
            var cards = PanelModel.VisibleFeats(v);
            if (cards.Count < 2) { Check(false, "choose: " + page + " has two cards to try"); continue; }
            Check(!cards.Any(c => c.Selected) && !PanelModel.Content(v).Any(b => b.Kind == "featdetail") && s.FeatSel == "",
                  "open: " + page + " opens with no card chosen and no detail area (the whole grid)");
            var hovered = PanelModel.FeatPointer(s, cards[1].Id, click: false);
            Check(!hovered && !PanelModel.VisibleFeats(PanelModel.Build(full, s)).Any(c => c.Selected), "pointer: on " + page + " a hover chooses nothing");
            var clicked = PanelModel.FeatPointer(s, cards[1].Id, click: true);
            var now = PanelModel.Build(full, s);
            Check(clicked && PanelModel.VisibleFeats(now).Single(c => c.Selected).Id == cards[1].Id && PanelModel.Content(now).First(b => b.Kind == "featdetail").Id == cards[1].Id,
                  "click: on " + page + " a click chooses the card (" + cards[1].Id + ") and the detail area comes in under the grid");
            var other = cards[0].Id;
            var hover2 = PanelModel.FeatPointer(s, other, click: false);
            Check(!hover2 && PanelModel.VisibleFeats(PanelModel.Build(full, s)).Single(c => c.Selected).Id == cards[1].Id, "pointer: on " + page + " a hover over another card leaves the chosen one and the detail as they are");
            var again = PanelModel.FeatPointer(s, cards[1].Id, click: true);
            var closed = PanelModel.Build(full, s);
            Check(again && !PanelModel.VisibleFeats(closed).Any(c => c.Selected) && !PanelModel.Content(closed).Any(b => b.Kind == "featdetail"),
                  "click: on " + page + " a click on the chosen card closes it: the full list again");
            Check(!PanelModel.CloseFeat(s, closed), "backspace: on " + page + " with no card chosen Backspace is not taken (it goes back as before)");
            PanelModel.StepFeat(s, closed, 1);
            var stepped = PanelModel.Build(full, s);
            Check(PanelModel.VisibleFeats(stepped).Single(c => c.Selected).Id == cards[0].Id && PanelModel.Content(stepped).Any(b => b.Kind == "featdetail"),
                  "keys: on " + page + " the first D chooses the first card and opens its detail");
            Check(PanelModel.CloseFeat(s, stepped) && !PanelModel.VisibleFeats(PanelModel.Build(full, s)).Any(c => c.Selected), "backspace: on " + page + " Backspace closes the chosen card first");
        }
        {
            // the choice lasts while its page is on screen; a page opened again (another page, then back) opens with none
            var s = At(PanelModel.FeatsPageId);
            var v = PanelModel.Build(full, s); PanelModel.Visited(s, v.Active, v.Page);
            PanelModel.FeatPointer(s, "drover", click: true);
            var same = PanelModel.Build(full, s); PanelModel.Visited(s, same.Active, same.Page);
            Check(PanelModel.VisibleFeats(same).Single(c => c.Selected).Id == "drover", "keep: the chosen card stays while its page is drawn again (a refresh, an update)");
            s.Page[Chapter.Feats] = PanelModel.FeatsUnsungId;
            var away = PanelModel.Build(full, s); PanelModel.Visited(s, away.Active, away.Page);
            s.Page[Chapter.Feats] = PanelModel.FeatsPageId;
            var back = PanelModel.Build(full, s);
            Check(!PanelModel.VisibleFeats(away).Any(c => c.Selected) && !PanelModel.VisibleFeats(back).Any(c => c.Selected) && !PanelModel.Content(back).Any(b => b.Kind == "featdetail"),
                  "keep: Earned opened again after Unsung opens with no card chosen (\"show me the full list before I select anything\")");
            var titles = PanelModel.Build(full, At(PanelModel.TitlesPageId));
            Check(PanelModel.VisibleFeats(titles).Count(c => c.Selected) == 1 && PanelModel.Content(titles).Any(b => b.Kind == "featdetail"), "titles: the Titles page keeps one title chosen and its detail");
            var ts = At(PanelModel.TitlesPageId); var chosen = PanelModel.VisibleFeats(PanelModel.Build(full, ts)).Single(c => c.Selected).Id; ts.FeatSel = chosen;
            Check(!PanelModel.FeatPointer(ts, chosen, click: true) && ts.FeatSel == chosen, "titles: a click on the chosen title keeps it (no empty detail on Titles)");
        }

        // ---------- 0.9.2 the More below cue over the grid's foot (Joost: "can we not make that a full horizontal bar that overlays the items?") ----------
        {
            var root = AppContext.BaseDirectory; while (root != null && !System.IO.File.Exists(System.IO.Path.Combine(root, "src", "Plugin.cs"))) root = System.IO.Path.GetDirectoryName(root);
            var ui = root == null ? "" : System.IO.File.ReadAllText(System.IO.Path.Combine(root, "src", "Panel", "Chapters", "FeatsUi.cs"));
            var html = root == null ? "" : System.IO.File.ReadAllText(System.IO.Path.Combine(root, "src", "Panel", "preview", "panel-preview.html"));
            var at = ui.IndexOf("static RectTransform FeatMoreCue(", StringComparison.Ordinal); var cue = at < 0 ? "" : ui.Substring(at, Math.Max(0, ui.IndexOf("\n        }", at, StringComparison.Ordinal) - at));
            Check(cue.Contains("raycastTarget = false") && cue.Contains("PanelLook.CueFade") && cue.Contains("FeatLookShade(words") && !ui.Contains("FeatCueStrip") && ui.Contains("view.offsetMin = Vector2.zero"),
                  "cue: a band over the grid's foot (the grid keeps its whole height), its fade drawn as the game must (CueFade), its words on the cards' text shade, catching no click");
            Check(FeatLookShape.Ramp(FeatLookShape.CueStops, 0f) == 0f && Math.Abs(FeatLookShape.Ramp(FeatLookShape.CueStops, 1f) - FeatLookShape.CueGround) < 1e-4 && Math.Abs(FeatLookShape.Ramp(FeatLookShape.CueStops, 1f - (PanelLayout.Feats.CueTextBottom + PanelLayout.LineBox(PanelLayout.Text.Floor)) / PanelLayout.Feats.CueBand) - FeatLookShape.CueGround) < 0.05f && FeatLookShape.CueGround <= 0.85f &&
                  FeatLookShape.Veil(FeatLookShape.CueGround) > FeatLookShape.CueGround,
                  "cue: the card's ground at " + F(FeatLookShape.CueGround) + " under the words, fading to nothing at the top (the game draws it darker, " + F(FeatLookShape.Veil(FeatLookShape.CueGround)) + ", to show the same)");
            Check(html.Contains("rgba(9,9,7,0) 0%, rgba(9,9,7,.85) 50%, rgba(9,9,7,.85) 100%") && html.Contains("height:var(--feats-cue-band)") && !html.Contains(".fgrid.scrolls { margin-bottom") &&
                  System.Text.RegularExpressions.Regex.IsMatch(html, @"\.fcue \{[^}]*pointer-events:none"),
                  "mirror: the same band over the grid's foot, the same stops, no strip of its own, no pointer events");
            var gold = PanelModel.Contrast(PanelModel.FeatLookInks[3], "#" + string.Concat(new[] { 0xe9, 0xdc, 0xc4 }.Select(c => ((int)Math.Round(9 + (1 - FeatLookShape.CueGround) * (c - 9))).ToString("x2"))));
            Check(gold >= 4.5, "cue: its gold words at least 4.5:1 where the brightest card text lies under the band's foot (" + F(gold) + ":1)");
        }

        // ---------- 0.9.3 the chosen card (Joost in game: "'currently selected' is way too subtle. or not even active. Let's make selected way more clear.") ----------
        // The chosen card had only the keys' focus ring, hidden by any mouse use: a card chosen by a click showed nothing. Now a gold frame over
        // everything on the card, a warm lift over the pattern and the band, its name in gold, the same for a click and A/D; a hover stays its 1 px line.
        {
            var root = AppContext.BaseDirectory; while (root != null && !System.IO.File.Exists(System.IO.Path.Combine(root, "src", "Plugin.cs"))) root = System.IO.Path.GetDirectoryName(root);
            string Read(params string[] p) => root == null ? "" : System.IO.File.ReadAllText(System.IO.Path.Combine(new[] { root, "src", "Panel" }.Concat(p).ToArray()));
            string Body(string text, string head) { var at = text.IndexOf(head, StringComparison.Ordinal); var end = at < 0 ? -1 : text.IndexOf("\n        }", at, StringComparison.Ordinal); return at < 0 || end < 0 ? "" : text.Substring(at, end - at); }
            var look = Read("Chapters", "FeatLookUi.cs"); var feats = Read("Chapters", "FeatsUi.cs"); var html = Read("preview", "panel-preview.html");
            var lk = Body(look, "static void FeatLookCard("); var plain = Body(feats, "static void FeatCard("); var chosen = Body(feats, "static void FeatChosen(");
            int Pos(string text, string what) => text.IndexOf(what, StringComparison.Ordinal);
            Check(lk.Length > 0 && Pos(lk, "FamilyGround(zone") < Pos(lk, "TextBand(") && Pos(lk, "TextBand(") < Pos(lk, "var liftAt = card.transform.childCount") && Pos(lk, "var liftAt") < Pos(lk, "\"FamilyEdge\"") &&
                  Pos(lk, "TierBar(stack") < Pos(lk, "FeatChosen(hover, c.Selected)") && plain.Contains("FeatChosen(hover, c.Selected)") && !lk.Contains("FocusRing(") && !plain.Contains("FocusRing(") && !feats.Contains("c.Ring") &&
                  chosen.Contains("PanelLook.RoundedEdge, PanelLook.Gold") && chosen.Contains("SetAsLastSibling()") && chosen.Contains("PanelWash.FeatChosen.Game") && chosen.Contains("SetSiblingIndex(") &&
                  chosen.Contains("PanelLook.Gold : h.NameInk") && Body(feats, "internal static void FeatSelect(").Contains("FeatChosen(c, c == h)"),
                  "chosen: the card's gold frame is drawn last (over the pattern, the band, the icon and the text), its lift over the pattern and the band and under the rest, its name in gold; no keys-only focus ring; a moved choice moves the look");
            Check(Pos(html, ".fcard .fframe {") > 0 && System.Text.RegularExpressions.Regex.IsMatch(html, @"\.fcard \.fframe \{[^}]*border:var\(--feats-chosen-rim\) solid var\(--gold\)[^}]*border-radius:var\(--feats-chosen-round\)[^}]*z-index:3") &&
                  System.Text.RegularExpressions.Regex.IsMatch(html, @"\.fcard\.sel \.fn[^{]*\{ color:var\(--gold\); \}") && html.Contains("${lkGround(c)}${c.selected ? `<i class=\"fsel\"></i>` : \"\"}<i class=\"lke\"></i>") &&
                  html.Contains("${bar}</div>${c.selected ? `<i class=\"fframe\"></i>` : \"\"}</div>`") && html.Contains("${notches}${c.selected ? `<i class=\"fframe\"></i>` : \"\"}</div>`") &&
                  !System.Text.RegularExpressions.Regex.IsMatch(html, @"body\.kbd [^{]*\.fcard\.sel") && html.Contains("outline:1px solid rgba(232,194,122,.45)"),
                  "mirror: the chosen card's lift after its ground, its gold frame (2 px, rounded 5) last, its name in gold, whatever the keys; a hover keeps its 1 px line at 45 %");
            var fams1 = PanelModel.FeatFamilies;
            var cardW = (float)Math.Floor((PanelLayout.Plate.Column - 2 * PanelLayout.Feats.ColumnGap) / 3);
            var cardX = (PanelLayout.FeatLook.IconLeftLarge + PanelLayout.FeatLook.Edge + PanelLayout.FeatLook.IconLarge + PanelLayout.FeatLook.IconGap) / cardW;
            string Lift(string hex) { var c = PanelWash.Over(PanelWash.FeatChosen.Mirror, PanelWash.Rgba("rgba(" + Convert.ToInt32(hex.Substring(1, 2), 16) + "," + Convert.ToInt32(hex.Substring(3, 2), 16) + "," + Convert.ToInt32(hex.Substring(5, 2), 16) + ",1)"));
                                      return "#" + string.Concat(new[] { c.r, c.g, c.b }.Select(v => ((int)Math.Round(v * 255)).ToString("x2"))); }
            var grounds = fams1.Select(f => Lift(PanelModel.FeatUnderText(f.colour, FeatLookShape.PatternDefault, FeatLookShape.BackingDefault, cardX))).Concat(PanelModel.FeatLookTextGrounds.Select(Lift)).ToList();
            var low = grounds.SelectMany(g => PanelModel.FeatLookInks.Select(i => (i, g, c: PanelModel.Contrast(i, g)))).OrderBy(x => x.c).First();
            var gold = grounds.Min(g => PanelModel.Contrast(PanelModel.FeatLookInks[3], g));
            Check(low.c >= 4.5 && gold >= 7, "chosen: every ink at least 4.5:1 on the lifted ground under the text, every family (lowest " + low.i + " on " + low.g + " " + F(low.c) + ":1), the gold name " + F(gold) + ":1");
            foreach (var page in new[] { PanelModel.FeatsPageId, PanelModel.FeatsUnsungId, PanelModel.FeatsTogetherId })
            {
                var s = At(page); var cards = PanelModel.VisibleFeats(PanelModel.Build(full, s));
                if (cards.Count < 2) continue;
                PanelModel.FeatPointer(s, cards[1].Id, click: true); var byClick = PanelModel.VisibleFeats(PanelModel.Build(full, s)).Where(c => c.Selected).Select(c => c.Id).ToList();
                var k = At(page); PanelModel.StepFeat(k, PanelModel.Build(full, k), 1); PanelModel.StepFeat(k, PanelModel.Build(full, k), 1);
                var byKeys = PanelModel.VisibleFeats(PanelModel.Build(full, k)).Where(c => c.Selected).Select(c => c.Id).ToList();
                Check(byClick.Count == 1 && byKeys.Count == 1 && byClick[0] == byKeys[0] && byClick[0] == cards[1].Id,
                      "chosen: on " + page + " one card at most carries the chosen look, the same card by a click or by D D (" + string.Join(",", byClick) + " / " + string.Join(",", byKeys) + ")");
            }
        }

        // ---------- a hover never chooses: only a click or A/D (Joost in game, 18 Oct) ----------
        {
            var s = At(PanelModel.TitlesPageId); var v = PanelModel.Build(full, s);
            var cards = PanelModel.VisibleFeats(v);
            var chosen = cards.First(c => c.Selected).Id; var other = cards.First(c => !c.Selected).Id;
            var hovered = PanelModel.FeatPointer(s, other, click: false);
            Check(!hovered && PanelModel.VisibleFeats(PanelModel.Build(full, s)).First(c => c.Selected).Id == chosen, "pointer: on Titles a hover over another card leaves the chosen one and the detail as they are");
            var clicked = PanelModel.FeatPointer(s, other, click: true);
            var now = PanelModel.Build(full, s);
            Check(clicked && PanelModel.VisibleFeats(now).First(c => c.Selected).Id == other && PanelModel.Content(now).First(b => b.Kind == "featdetail").Id == other, "pointer: on Titles a click chooses the card and the detail shows it");
            var before = s.FeatSel; PanelModel.StepFeat(s, now, 1);
            Check(s.FeatSel != before, "pointer: on Titles A/D still move the choice");
        }
        return fails;
    }
}
