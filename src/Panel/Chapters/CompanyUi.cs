using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Company's own forms (work/hearthwoven-visual-vocabulary/proto: vocab.js hearth + giftRow, r3-company.js Together,
    /// r4over.js Gear shared): giving (players around the hearth, one thin flat line per gift along its FireLayout curve, its width and
    /// arrowhead by the count, 0.7), together (category chips,
    /// the group's total as one bar coloured per player, the players x categories dot matrix) and madeby (their gear, your
    /// gear: maker, an arrow, one tile per kind). Every picture is a ready sprite; geometry is worked out once per fill.
    /// </summary>
    public partial class PanelUi
    {
        static readonly Color GearThread = new Color(0.722f, 0.737f, 0.769f), FoodThread = new Color(0.910f, 0.663f, 0.282f);   // #b8bcc4, #e8a948 (proto)
        static Color ThreadTint(Block g, bool faint) { var c = g.Tone == "gear" ? GearThread : FoodThread; c.a = faint ? 0.45f : 0.92f; return c; }
        static Color PersonTint(string name) => PanelLook.PersonColor(personColors.TryGetValue(name ?? "", out var i) ? i : 0);

        static void Caption(RectTransform parent, string text, float x, float top, float w)
        {
            var h = Label(parent, text, PanelLayout.Text.Floor, PanelLook.Muted, style: FontStyles.UpperCase, align: TextAlignmentOptions.MidlineLeft);
            h.characterSpacing = 14; h.textWrappingMode = TextWrappingModes.NoWrap; h.rectTransform.Box(x, top, w, 20);
        }

        // ----- giving: from whom to whom? -----

        const float FireH = 400, FireR = 140, ShieldD = 40, GiftRowH = 40, GiftPerson = 88, LegendH = 30, CountPx = 19, CountCompactPx = 16, BeadSpread = 68;
        static float FireW => Mathf.Min(360f, Column * 0.49f);   // the fire on the left, the list beside it
        /// <summary>The hearth's middle from the drawing's top: under the legend, in the middle of the fire's room.</summary>
        const float FireCy = LegendH + FireH / 2 + 2;
        /// <summary>The wait line under the hearth: its gap below the hearth, its inset from the fire's sides, its room (two lines).</summary>
        const float FireWaitGap = 10, FireWaitInset = 8, FireWaitH = 44;

        // ----- 0.9: the drawing on its way (Joost in game on 0.8.91: "when I open the company tab for the first time ... it sort of freezes the game") -----

        // the plan the page waits for and its line under the hearth (the step it shows); the drawing ahead, once per opening, from your own book
        static FireLayout.Pending fireWait; static TextMeshProUGUI fireWaitLine; static int fireWaitShown;
        static bool fireAheadWanted; PanelInput fireAheadSelf;

        static void FireWaitLine(RectTransform root, FireLayout.Pending wait, float cx, float top)
        {
            var step = wait?.Stage ?? 1;
            var line = Label(root, PanelModel.FireWaitWords(step), PanelLook.MinText, PanelLook.Muted, style: FontStyles.Italic, align: TextAlignmentOptions.Top);
            line.rectTransform.Box(cx - FireW / 2 + FireWaitInset, top, FireW - 2 * FireWaitInset, FireWaitH);
            fireWait = wait; fireWaitLine = line; fireWaitShown = step;
        }

        /// <summary>Every frame while the book is open: the drawing ahead once per opening; the wait line's step when it changes, and the page drawn
        /// again (forced: its model did not change) once the plan is kept. Nothing else is drawn again.</summary>
        void FireTick()
        {
            if (fireAheadWanted && fireAheadSelf != null) { fireAheadWanted = false; FireAhead(fireAheadSelf); }
            if (fireWait == null) return;
            if (!fireWaitLine) { fireWait = null; fireWaitLine = null; return; }   // its page was drawn away
            if (fireWait.Done) { fireWait = null; fireWaitLine = null; Render(true); return; }
            var step = fireWait.Stage;
            if (step == fireWaitShown) return;
            fireWaitShown = step; fireWaitLine.text = PanelModel.FireWaitWords(step);
        }

        /// <summary>Fireside's plan for your own book, asked of the worker the frame after the book opens (on another chapter: Company's own drawing
        /// asks for itself), so that a later turn to Company finds it ready. One model build on this frame (about a millisecond); the geometry off it.</summary>
        void FireAhead(PanelInput self)
        {
            if (view == null || view.Active == Chapter.Company || self.Fellows == null || self.Fellows.Count == 0) return;
            try
            {
                var st = new PanelState { Chapter = Chapter.Company, Player = "" }; st.Page[Chapter.Company] = "fireside";
                var giving = PanelModel.Content(PanelModel.Build(self, st)).FirstOrDefault(x => x.Kind == "giving");
                if (giving != null) FireLayout.Request(giving, FireW / 2, FireCy, out _);
            }
            catch (Exception e) { Failed("Fireside ahead", e); }
        }
        static float ListX => FireW + 22;

        internal static void Giving(RectTransform col, Block b)
        {
            var items = b.Items ?? new List<Block>();
            var players = items.Where(i => i.Kind == "player").ToList();
            var gifts = items.Where(i => i.Kind == "gift").ToList();
            if (players.Count == 0) return;
            var mine = gifts.Where(g => g.Selected).ToList(); var others = gifts.Where(g => !g.Selected).ToList();
            var root = Node("Fireside", col);
            // the list's words measured as drawn (one probe label): the name columns and each list line (GiftWraps)
            var probe = Label(root, "", PanelLayout.Text.Body, PanelLook.Text); probe.textWrappingMode = TextWrappingModes.NoWrap;
            float Measure(string t, float size, FontStyles style) { probe.fontSize = Mathf.Max(size, PanelLook.MinText); probe.fontStyle = style; return Mathf.Ceil(probe.GetPreferredValues(t ?? "").x); }
            float w = Column - ListX;
            // the name columns as wide as the longest name needs (Joost in game: a long name was cut on a row with room to spare), at least the old 88 px,
            // at most what leaves the item line its 200 px; only then a name is cut
            float longest = 0;
            foreach (var p in players) longest = Mathf.Max(longest, Measure(p.Text ?? p.Title ?? p.Id, PanelLayout.Text.Body, FontStyles.Normal));
            var personW = Mathf.Clamp(longest + 38, GiftPerson, Mathf.Max(GiftPerson, (w - 200) / 2));
            IEnumerable<Block> LinesOf(Block g) => g.Items != null && g.Items.Count > 0 ? g.Items : new List<Block> { g };   // a gift with meals and feast servings: a list line each
            float RowH(Block g, Block line) => GiftRowH + (GiftWraps(g, line, w - 2 * personW, Measure) ? GiftWrapH : 0);
            float ScopeH(string v) => string.IsNullOrEmpty(v) ? 0 : GiftScopeH;
            float Rows(List<Block> gs) => gs.Sum(g => LinesOf(g).Sum(l => RowH(g, l)));
            var listH = (mine.Count > 0 ? 24 + ScopeH(b.Value) + Rows(mine) : 0) + (others.Count > 0 ? 30 + ScopeH(b.Value2) + Rows(others) : 0);
            Size(root, -1, LegendH + Mathf.Max(FireH, listH));

            // the circle: players at even angles from the top (the owner first), the fire in the middle (FireLayout.cs: y up, origin top left)
            float cx = FireW / 2, cy = FireCy;
            // 0.9: the drawing's geometry is worked out on a worker the first time (FireLayout.Request); until then the hearth stands with the wait
            // line under it and the list beside it, and FireTick draws the page again once the plan is kept
            var plan = FireLayout.Request(b, cx, cy, out var wait);
            var hearthPx = (float)FireLayout.Hearth;   // the threads keep FireLayout.Keep from its middle
            var fire = VocabImg(root, "Hearth", "hearth-fire", Color.white).rectTransform; fire.Box(cx - hearthPx / 2, cy - hearthPx / 2, hearthPx, hearthPx);
            if (plan == null) FireWaitLine(root, wait, cx, cy + hearthPx / 2 + FireWaitGap);
            else FireDrawing();
            // the fire's threads, counts, shields and names from its worked-out plan
            void FireDrawing()
            {
                var pos = plan.Pos.ToDictionary(kv => kv.Key, kv => new Vector2((float)kv.Value.x, (float)kv.Value.y), StringComparer.OrdinalIgnoreCase);
                var layer = Node("Threads", root); layer.Stretch();
                var beads = Node("Beads", root); beads.Stretch();
                foreach (var kv in plan.Geos) Thread(layer, kv.Key, kv.Value);
                var pills = plan.Pills;   // the counts of your gifts: a pill on each thread, put where it touches nothing else (FireLayout.PlacePills); none left off in the sample world
                foreach (var p in pills) if (!p.Dropped) CountPill(beads, p);
                foreach (var p in players)
                {
                    var at = pos[p.Id];
                    var shield = Marker(root, p.Icon, ShieldD, layout: false); shield.Box(at.x - ShieldD / 2, -at.y - ShieldD / 2, ShieldD, ShieldD);
                    var below = -at.y > cy + 10;
                    var nr = plan.NameRect[p.Id]; var nx = (float)((nr.X0 + nr.X1) / 2);   // pushed outward for a player on the side (FireLayout: the threads leave inward)
                    var name = Label(root, p.Title, PanelLayout.Text.Body, p.Selected ? PanelLook.Gold : PanelLook.Text, style: FontStyles.Bold, align: below ? TextAlignmentOptions.Top : TextAlignmentOptions.Bottom);
                    name.textWrappingMode = TextWrappingModes.NoWrap; name.rectTransform.Box(nx - 70, below ? -at.y + ShieldD / 2 + 2 : -at.y - ShieldD / 2 - 24, 140, 22);
                }
            }

            // the list beside the fire: between the owner and the others, then around the fire; each list says its scope once in its caption (fix4-rest)
            // each section says its source once, on a quiet line under its caption (Value, Value2); a row adds words only where it differs
            float y = LegendH;
            void Lines(Block g) { foreach (var line in LinesOf(g)) { GiftRow(root, g, line, players, ListX, y, w, personW, Measure); y += RowH(g, line); } }
            if (mine.Count > 0) { Caption(root, b.Title, ListX, y, w); y += 24; y = Scope(root, b.Value, ListX, y, w); foreach (var g in mine) Lines(g); }
            if (others.Count > 0) { y += 4; Caption(root, b.Text + ", fainter", ListX, y, w); y += 26; y = Scope(root, b.Value2, ListX, y, w); foreach (var g in others) Lines(g); }
            UnityEngine.Object.Destroy(probe.gameObject);
            // the legend above the drawing: which thread is food, which is gear, where the arrow points, what a faint thread is
            var key = Node("Legend", root); key.Box(0, 0, Column, 24);
            Layout(key.gameObject.AddComponent<HorizontalLayoutGroup>(), 7, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(b.Note)) { Label(key, b.Note, PanelLayout.Text.Floor, PanelLook.Muted, style: FontStyles.Italic).textWrappingMode = TextWrappingModes.NoWrap; Size(Node("Gap", key), 14, 1); }
            Size(Fill(key, "Food", FoodThread), 22, 4); Label(key, PanelModel.LegendFood, PanelLayout.Text.Floor, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            Size(Node("Gap", key), 14, 1);
            Size(Fill(key, "Gear", GearThread), 22, 4); Label(key, PanelModel.LegendGear, PanelLayout.Text.Floor, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            Size(Node("Gap", key), 14, 1);
            Size(VocabImg(key, "Arrow", "thread-arrow", PanelLook.Muted), 12, 12); Label(key, PanelModel.LegendArrow, PanelLayout.Text.Floor, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            if (gifts.Any(g => g.Items != null && g.Items.Any(l => l.Tone == PanelModel.TeamworkTone)))   // a feast one made and another set out (0.8): its mark, once
            {
                Size(Node("Gap", key), 14, 1); TeamworkMark(key); Label(key, PanelModel.LegendTeamwork, PanelLayout.Text.Floor, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        /// <summary>The teamwork mark (0.8, B22): the Together page's two-people line sprite in the list gold, small and calm.</summary>
        internal static void TeamworkMark(RectTransform parent) => Size(VocabImg(parent, "Teamwork", "list-together", PanelLook.Gold), TeamworkPx, TeamworkPx);
        const float TeamworkPx = 16;

        // the arrow's line, from the row's top (its head 19 .. 31; the rule at 39); a wrapped line's words, GiftWrapH above; the room between a line's parts
        const float GiftScopeH = 20, GiftItemPx = 24, GiftArrowY = 25, GiftWrapH = 18, GiftGap = 6, GiftWordsH = 24;
        static float Scope(RectTransform root, string text, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(text)) return y;
            var t = Label(root, text, PanelLayout.Text.Floor, PanelLook.Faint, style: FontStyles.Italic, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.Box(x, y - 4, w, 18);
            return y + GiftScopeH;
        }

        // one gift as a thin flat line along its curve in its lane: the pair's corridor bends gently away from the fire, each way on its right, so no
        // thread runs along or crosses another where it need not (the geometry: FireLayout.Build); width by the count, the arrowhead 3 times the
        // width, its tip on the receiver's rim (FireLayout.StrokeOf, 0.7 variant A), both one mesh so a faint thread never darkens where line and head meet
        static void Thread(RectTransform layer, Block g, FireLayout.Geo geo)
        {
            var st = FireLayout.StrokeOf(geo, g.Fraction);
            var line = Node("Thread", layer); line.Stretch(); line.pivot = new Vector2(0, 1);   // local (0, 0) = the drawing's top left, y up: FireLayout's own coordinates
            var stroke = line.gameObject.AddComponent<ThreadStroke>(); stroke.raycastTarget = false; stroke.color = PanelWash.Toward(ThreadTint(g, !g.Selected), PanelWash.Island);   // the mirror's thread at its opacity on the island, as the game draws it
            Vector2 V((double x, double y) p) => new Vector2((float)p.x, (float)p.y);
            stroke.Set(st.Line.Select(V).ToArray(), (float)st.Width, V(st.Tip), V(st.Left), V(st.Right));
            // a thread between two fellow players stays a faint thread: its count is in the list under "Among fellow players" (fix3-rest)
        }

        // the count of one gift: plain digits (19 px: Joost could not read 12 px in game) on a dark pill lying on its own thread, where FireLayout put it;
        // 0.8.5 (Joost: "unclear which thread a number belongs to"): a CountRing px ring in its thread's tone, gold for food, gray for gear
        const float CountRing = 2;
        static void CountPill(RectTransform beads, FireLayout.Pill p)
        {
            var ring = Kit(beads, "CountRing", "meter-track"); ring.color = ThreadTint(p.Gift, false);
            var rr = ring.rectTransform; rr.anchorMin = rr.anchorMax = new Vector2(0, 1); rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = new Vector2((float)p.Width, (float)p.H); rr.anchoredPosition = new Vector2((float)p.At.x, (float)p.At.y);
            var pill = Kit(ring.transform, "CountPill", "meter-track"); pill.color = new Color(0.03f, 0.025f, 0.02f, 0.97f);
            var r = pill.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(CountRing, CountRing); r.offsetMax = new Vector2(-CountRing, -CountRing);
            var t = Label(ring.transform, p.Text, p.Compact ? CountCompactPx : CountPx, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.Center, face: PanelLook.Plain);
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch();
        }

        // maker -> the item and its count over a short thread, the grateful verb under it -> the one who enjoyed it
        // line: the gift itself, or one of its lines (meals, feast servings: "set out a Mountains feast · × 3 enjoyed", teamwork: "Tor set it out · × 3 enjoyed")
        // 0.8.5 (Joost in game, four players: "feas× 5"): the line's parts are measured and placed one after another, each at its own width (a layout group
        // squeezed the words under the count when the line was wider than its room); a line wider than its room between the two names puts its words (the
        // feast or the other hand, the row's own scope) on a line of their own above the count, and the row is GiftWrapH taller
        static string GiftWords(Block g, Block line) => line != g && !string.IsNullOrEmpty(line.Title) ? line.Title : null;
        static bool GiftWraps(Block g, Block line, float room, Func<string, float, FontStyles, float> measure)
        {
            var words = GiftWords(g, line); if (words == null && string.IsNullOrEmpty(g.Value2)) return false;
            var parts = new List<float> { GiftItemPx, measure("× " + line.Value, PanelLayout.Text.Small, FontStyles.Bold), measure(line.Note, PanelLayout.Text.Floor, FontStyles.Normal) };
            if (line.Tone == PanelModel.TeamworkTone) parts.Add(TeamworkPx);
            if (words != null) parts.Add(measure(words + " ·", PanelLayout.Text.Floor, FontStyles.Normal));
            if (!string.IsNullOrEmpty(g.Value2)) parts.Add(measure(g.Value2, PanelLayout.Text.Floor, FontStyles.Italic));
            return parts.Sum() + GiftGap * (parts.Count - 1) > room;
        }

        static void GiftRow(RectTransform root, Block g, Block line, List<Block> players, float x, float top, float w, float personW, Func<string, float, FontStyles, float> measure)
        {
            string Who(string id) { var p = players.FirstOrDefault(x2 => x2.Id == id); return p?.Text ?? p?.Title ?? id; }   // the list's short name ("You")
            var tint = ThreadTint(g, false);
            float mx = x + personW, mw = w - 2 * personW;
            var wrap = GiftWraps(g, line, mw, measure); var dy = wrap ? GiftWrapH : 0;
            void Person(string id, float px)
            {
                var m = Marker(root, "person:" + id, 22, layout: false); m.Box(px, top + dy + GiftArrowY - 11, 22, 22);   // the names at both ends of the arrow, on its line
                var t = Label(root, Who(id), PanelLayout.Text.Body, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
                t.rectTransform.Box(px + 30, top + dy + GiftArrowY - 12, personW - 32, 24);
            }
            Person(g.Title, x);
            // one line ABOVE the arrow (Joost 2026-10-09): the item (1.5 times the old 16 px), "× 3" and the verb side by side; nothing under the arrow
            var parts = new List<(float w, Action<float, float> draw)>();   // each part: its width, and how it draws at (x, the line's middle)
            void Text(string text, float size, Color color, FontStyles style)
            {
                var tw = measure(text, size, style);
                parts.Add((tw, (px, mid) => { var t = Label(root, text, size, color, style: style, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Box(px, mid - GiftWordsH / 2, tw + 2, GiftWordsH); }));
            }
            void Team() => parts.Add((TeamworkPx, (px, mid) => VocabImg(root, "Teamwork", "list-together", PanelLook.Gold).rectTransform.Box(px, mid - TeamworkPx / 2, TeamworkPx, TeamworkPx)));   // "Tor set it out": one made it, another set it out
            void Lay(float mid) { var total = parts.Sum(p => p.w) + GiftGap * (parts.Count - 1); var at = mx + (mw - total) / 2; foreach (var p in parts) { p.draw(at, mid); at += p.w + GiftGap; } parts.Clear(); }
            var words = GiftWords(g, line); var scope = string.IsNullOrEmpty(g.Value2) ? null : g.Value2;   // scope: only where it differs from the section: their last session
            if (wrap)
            {
                if (line.Tone == PanelModel.TeamworkTone) Team();
                if (words != null) Text(words, PanelLayout.Text.Floor, PanelLook.Muted, FontStyles.Normal);
                if (scope != null) Text(scope, PanelLayout.Text.Floor, PanelLook.Faint, FontStyles.Italic);
                Lay(top - 1 + GiftWrapH / 2);
            }
            parts.Add((GiftItemPx, (px, mid) => Marker(root, line.Icon, GiftItemPx, layout: false).Box(px, mid - GiftItemPx / 2, GiftItemPx, GiftItemPx)));
            if (!wrap && line.Tone == PanelModel.TeamworkTone) Team();
            if (!wrap && words != null) Text(words + " ·", PanelLayout.Text.Floor, PanelLook.Muted, FontStyles.Normal);
            Text("× " + line.Value, PanelLayout.Text.Small, PanelLook.Text, FontStyles.Bold);
            Text(line.Note, PanelLayout.Text.Floor, PanelLook.Muted, FontStyles.Normal);
            if (!wrap && scope != null) Text(scope, PanelLayout.Text.Floor, PanelLook.Faint, FontStyles.Italic);
            Lay(top + dy - 1 + GiftItemPx / 2);
            Fill(root, "Line", tint).rectTransform.Box(mx + 4, top + dy + GiftArrowY - 1, mw - 18, 2);
            var arrow = VocabImg(root, "Arrow", "thread-arrow", tint).rectTransform; arrow.Box(mx + mw - 16, top + dy + GiftArrowY - 6, 12, 12);
            Person(g.Text, x + w - personW + 6);
            Img(root, "Rule", null, PanelLook.Rule).rectTransform.Box(x, top + dy + GiftRowH - 1, w, 1);   // B32: 8 px of air between the arrow and the row's rule
        }

        // ----- together: the group's whole, per category; never a ranking or a combined score -----

        const float ChipGap = 4;
        // the share rows: the category's name column (its icon, then its name), the gap to the bars
        const float ShareLabelW = 190, ShareLabelGap = 12, ShareIcon = 16;


        internal static void Together(RectTransform col, Block b, Func<string, Action> link)
        {
            var cats = (b.Items ?? new List<Block>()).Where(c => c.Kind == "category").ToList();
            if (cats.Count == 0) return;
            var pick = cats.FirstOrDefault(c => c.Selected) ?? cats[0];

            // the chips: one per category, the chosen one lit; a click shows that category large
            var lines = VStack(col, 6); lines.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            RectTransform line = null; float used = 0;
            foreach (var c in cats)
            {
                // the plate's chip (as a view switch draws it), wrapped onto a second line when the row is full
                var at = cats.IndexOf(pick); Block Step(int d) => cats[((at + d) % cats.Count + cats.Count) % cats.Count];   // left: this one (the chosen: next), right: previous
                var cw = Chip(lines, c.Title, null, c == pick, link?.Invoke(PanelModel.ViewLink(b, c == pick ? Step(1) : c)), 28, back: link?.Invoke(PanelModel.ViewLink(b, Step(-1))));
                var chip = lines.GetChild(lines.childCount - 1);
                if (line == null || used + ChipGap + cw > Column) { line = Line(lines, ChipGap); used = 0; } else used += ChipGap;   // 4 px apart: all seven categories (Cargo carried too) share the first line
                chip.SetParent(line, false); used += cw;
            }

            // the window chips Battle uses, under the category chips, for every category (0.8.5): a window the chosen one cannot answer is greyed
            var windows = (b.Items ?? new List<Block>()).FirstOrDefault(c => c.Kind == "switch");
            if (windows != null) Switch(col, windows, link, (r, x) => { });

            // the chosen category large: the group's total, one bar coloured per player, the legend under it
            var parts = (pick.Items ?? new List<Block>()).Where(p => p.Fraction > 0).ToList();
            TogetherHero(col, pick);
            if (pick.Before != null) HeroBeforeLine(col, pick);   // 0.8 Compare: the period before on a line of its own (the hero's words are long here)
            if (parts.Count < 2) { TogetherScope(col, b, pick, link); return; }   // alone: the number is the page (with its scope and keys); the bar and the matrix of one are noise
            // the book's one bar form (BarFormUi.cs) in the players' colours: the bar, and the list names each player with their number and share
            var compared = (b.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == PanelModel.CompareRowsKind && i.Id == pick.Id);   // 0.8 Compare: a row per player, now beside the period before
            if (compared != null) CompareRows(col, compared);
            else BarWithList(col, parts, false, new BarLook { Colour = p => PersonTint(p.Id) });
            TogetherScope(col, b, pick, link);   // since when the category counts, the keys that flip the chips (fix4-rest) and About these numbers

            // each player's share (V1, Joost 2026-10-08, proto/share-pA.png): one 100 % bar per category in player colours, You first then
            // the legend's order; the category's gold icon before its name; the chosen category's row outlined gold; the players' legend once,
            // top right. 0.8.5 in the damage grammar (Joost: one bar grammar across the book; Together on one screen with three fellows): 16 px
            // bars 3 px apart, the parts rounded 3 px apart, each part's share inside it as digits (BarDigits: 13 px, 12 to fit; the caption
            // says %), no track (a whole bar covers it); pointing at a part says "Edda 17 %" on the page's one tag (no object per part)
            Spacer(col, 4);
            var people = (cats[0].Items ?? new List<Block>()).ToList();
            var head = Node("ShareHead", col); Size(head, -1, 22);
            Caption(head, b.Text, 0, 0, 300);
            var legend = Node("Legend", head); legend.anchorMin = legend.anchorMax = legend.pivot = new Vector2(1, 0.5f); legend.anchoredPosition = Vector2.zero; legend.sizeDelta = new Vector2(Column - 300, 22);
            Layout(legend.gameObject.AddComponent<HorizontalLayoutGroup>(), 14, TextAnchor.MiddleRight);
            foreach (var p in people)
            {
                var k = Line(legend, 5);
                Marker(k, p.Icon, 18);
                var nm = Label(k, p.Title, PanelLayout.Text.Floor, PanelLook.Text, style: FontStyles.Bold); nm.textWrappingMode = TextWrappingModes.NoWrap;   // the name in the text colour: the shield carries the player's colour
            }
            float RowH = PanelModel.DamageBar, RowGap = PanelModel.DamageGap;
            // every row follows the chosen window (0.8.5: sailed and cargo too, from the minutes and the shared days)
            var rows = Node("Shares", col); Size(rows, -1, cats.Count * (RowH + RowGap) - RowGap);
            float barX = ShareLabelW + ShareLabelGap, barW = Column - barX, span = barW + PanelModel.DamageGap;
            for (int c = 0; c < cats.Count; c++)
            {
                var cat = cats[c]; var on = cat == pick; var y = c * (RowH + RowGap);
                if (PanelLook.Icon(cat.Icon) != null) { var ic = Marker(rows, cat.Icon, ShareIcon, layout: false); ic.Box(0, y + (RowH - ShareIcon) / 2, ShareIcon, ShareIcon); if (cat.Icon.StartsWith("vocab:")) foreach (var im in ic.GetComponentsInChildren<Image>()) im.color = PanelLook.Gold; }
                BtText(rows, cat.Title, ShareIcon + 6, y, ShareLabelW - ShareIcon - 8, RowH, PanelLayout.Text.Floor, on ? PanelLook.Gold : PanelLook.Muted, style: on ? FontStyles.Bold : FontStyles.Normal);
                var bar = Node("Bar", rows); bar.Box(barX, y, barW, RowH);
                var shown = people.Select(p => (p, part: cat.Items?.FirstOrDefault(q => q.Id == p.Id))).Where(x => x.part != null && x.part.Fraction > 0).ToList();
                if (shown.Count == 0) continue;
                var pct = PanelModel.WholeShares(shown.Select(x => (double)Mathf.Clamp01(x.part.Fraction)).ToList());
                var spans = PanelModel.BarParts(shown.Select(x => Mathf.Clamp01(x.part.Fraction)).ToArray(), null, (PanelModel.DamagePartMin + PanelModel.DamageGap) / span);
                var hover = bar.gameObject.AddComponent<BarHover>(); hover.Init(shown.Count);
                var words = new string[shown.Count]; var mid = new float[shown.Count];
                for (int k = 0; k < shown.Count; k++)
                {
                    var tint = PersonTint(shown[k].p.Id); var share = pct[k] == 0 ? PanelModel.LessThanOne : pct[k].ToString();
                    var seg = RoundedPart(bar, "Part", tint, raycast: true); seg.pixelsPerUnitMultiplier = DrRadiusScale;
                    var w = Mathf.Max(1f, (spans[k].to - spans[k].from) * span - PanelModel.DamageGap);
                    seg.rectTransform.Box(spans[k].from * span, 0, w, RowH); mid[k] = spans[k].from * span + w / 2;
                    BarDigits(seg.transform, share, w, SegmentInk(tint));   // its share inside where it fits
                    words[k] = shown[k].p.Title + " " + share + " %";
                    hover.Parts[k] = seg.rectTransform; var hp = seg.gameObject.AddComponent<HoverPart>(); hp.Bar = hover; hp.Index = k;
                }
                HoverRing(bar, hover);
                hover.TagOver(PageTag(rows), bar, words, mid, 0, barW);
                if (on) Edge(bar, PanelLook.Gold);   // the chosen category
            }
        }

        // the chosen category's hero in the shared hero's look; its qualifier ("before the foe's armor") goes on a quiet line of its own under it when
        // the number, its words and the qualifier are wider than the column (0.8.5, Joost in game: "...brought in togethercounted the first time ..."
        // ran into the label and the plate's edge), never squeezed into the words
        const float TogetherQualifierH = 20;
        // 0.8.5 (Joost's 4K screenshots, about 135 px of air over the hero): TMP gives the hero's number its font's whole line box, the mirror crops it
        // (.hn .num line-height .84), so in game the number stood about 15 mirror px lower under the window chips than in the preview. The hero line
        // gives back that empty top (negative top padding: the row is shorter, every part keeps its baseline, the digits' empty ascent reaches up)
        const float TogetherHeroTrim = 0.2f;   // of the hero size: ~13 px at 67, the difference measured on the screenshot (47.webp: 29 against 14 mirror px)
        static RectTransform TogetherHeroLine(RectTransform col)
        {
            var line = Line(col, 0, TextAnchor.LowerLeft);
            line.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, -Mathf.RoundToInt(HeroSize * TogetherHeroTrim), 0);
            return line;
        }
        static void TogetherHero(RectTransform col, Block pick)
        {
            var line = TogetherHeroLine(col);
            HeroNumber(line, new Block { Value = pick.Value, Title = pick.Text }, HeroSize, HeroLabel, HeroSize);
            if (string.IsNullOrEmpty(pick.Note)) return;
            var q = Label(col, pick.Note, PanelLayout.Text.Floor, PanelLook.Faint); q.textWrappingMode = TextWrappingModes.NoWrap;
            LayoutRebuilder.ForceRebuildLayoutImmediate(line);
            var beside = LayoutUtility.GetPreferredWidth(line) + PanelLayout.Hero.WordsGap + Mathf.Ceil(q.GetPreferredValues(pick.Note).x);
            if (beside > Column) { Size(q.rectTransform, -1, TogetherQualifierH); return; }   // under the hero, its own line
            q.gameObject.SetActive(false); Destroy(q.gameObject);   // inactive at once: the layout leaves it out this frame
            line.gameObject.SetActive(false); Destroy(line.gameObject);
            HeroNumber(TogetherHeroLine(col), new Block { Value = pick.Value, Title = pick.Text, Note = pick.Note }, HeroSize, HeroLabel, HeroSize);   // beside the words, as before
        }

        // the line under Together's numbers (fix4-rest, review: no scope on the chip, no key shown for the chips): since when this category counts, on the left;
        // on the right the keys that act here, each as a quiet keycap with what it flips (the view key the category, the filter key the window), then (0.8.5,
        // Joost in game: "perhaps also put 'about these numbers' here?") About these numbers with its key, so the plate spends no row on it above the hero;
        // open, its box right under this line. Too wide (Cargo carried's long scope): the button says "Numbers" (as the top row's, SwitchRow), then the keys
        // lose their words (the key line at the foot says them too); only when that is not enough the scope gets a line of its own
        static float CapW(string key) => key.Length > 1 ? 14 + 8 * key.Length : 22;   // a keycap for a named key ("Tab") is wider than one for a letter
        const float KeyRowGap = 8, KeyRowH = 24;   // the About button's chip as tall as the line (the page fits one screen with three fellows)
        static void TogetherScope(RectTransform col, Block together, Block pick, Func<string, Action> link)
        {
            var windows = (together.Items ?? new List<Block>()).FirstOrDefault(c => c.Kind == "switch");
            var catKey = together.KeyCap; var winKey = windows?.KeyCap; var about = headAbout;
            if (string.IsNullOrEmpty(pick.Value2) && string.IsNullOrEmpty(catKey) && string.IsNullOrEmpty(winKey) && about == null) return;
            var scopeRow = Line(col, KeyRowGap); Size(scopeRow, -1, KeyRowH);   // its own line only when the keys leave it no room (below)
            var row = Line(col, KeyRowGap); Size(row, -1, KeyRowH);
            TextMeshProUGUI scope = null;
            if (!string.IsNullOrEmpty(pick.Value2)) { scope = Label(row, pick.Value2, PanelLayout.Text.Small, PanelLook.Muted); scope.textWrappingMode = TextWrappingModes.NoWrap; }
            Size(Node("Gap", row), 0, 1).flexibleWidth = 1;
            var words = new List<GameObject>();
            void Key(string key, string what) { Size(Keycap(row, key), CapW(key), 22); var w = Label(row, what, PanelLayout.Text.Small, PanelLook.Muted); w.textWrappingMode = TextWrappingModes.NoWrap; words.Add(w.gameObject); }
            if (!string.IsNullOrEmpty(catKey)) Key(catKey, "category");
            if (!string.IsNullOrEmpty(winKey)) { Size(Node("Gap", row), 10, 1); Key(winKey, "window"); }
            RectTransform button = null;
            if (about != null) { Size(Node("Gap", row), 10, 1); button = Line(row, KeyRowGap); AboutButton(button, about, link, height: KeyRowH); }
            float Wide() { LayoutRebuilder.ForceRebuildLayoutImmediate(row); return LayoutUtility.GetPreferredWidth(row); }
            if (button != null && Wide() > Column)
            {
                button.gameObject.SetActive(false); Destroy(button.gameObject);
                AboutButton(Line(row, KeyRowGap), about, link, PanelModel.AboutShort, KeyRowH);
            }
            void Words(bool on) { foreach (var w in words) w.SetActive(on); }
            if (words.Count > 0 && Wide() > Column) Words(false);
            if (scope != null && Wide() > Column) { Words(true); scope.transform.SetParent(scopeRow, false); }
            else { scopeRow.gameObject.SetActive(false); Destroy(scopeRow.gameObject); }
            if (about != null) AboutBox(col, about);   // open: the box under the line that holds its button
        }

        // ----- madeby: their gear, your gear; maker -> one tile per kind (left) and your gear -> who put it to good use -----

        const float GearTile = 76, GearGap = 6, GearPerson = 110, GearArrow = 34, GearNameH = 18;   // fix4-rest: the tiles 76 px (they were 56: the lower half of the plate stood empty)

        static int GearPerLine(float w) => Mathf.Max(1, Mathf.FloorToInt((w - GearPerson - GearArrow + GearGap) / (GearTile + GearGap)));
        static float GearRowHeight(Block r, float w) => Mathf.CeilToInt(Mathf.Max(1, (r.Items ?? new List<Block>()).Count(t => t.Kind == "tile")) / (float)GearPerLine(w)) * (GearTile + GearGap) + 14 + GearNameH;

        internal static void MadeBy(RectTransform col, Block b)
        {
            var rows = (b.Items ?? new List<Block>()).Where(r => r.Kind == "row").ToList();
            if (rows.Count == 0) return;
            float half = Column / 2, w = half - 20;
            var left = rows.Where(r => r.Tone == "left").ToList(); var right = rows.Where(r => r.Tone != "left").ToList();
            float hl = left.Sum(r => GearRowHeight(r, w)), hr = right.Sum(r => GearRowHeight(r, w));
            var height = 28 + Mathf.Max(hl, hr, GearTile) + (string.IsNullOrEmpty(b.Note) ? 0 : 24);
            var root = Node("Gear", col); Size(root, -1, height);
            Caption(root, b.Title, 0, 0, w); Caption(root, b.Text, half + 20, 0, w);
            if (!string.IsNullOrEmpty(b.Note)) { var nl = Label(root, b.Note, PanelLayout.Text.Floor, PanelLook.Muted, style: FontStyles.Italic, align: TextAlignmentOptions.MidlineLeft); nl.textWrappingMode = TextWrappingModes.NoWrap; nl.rectTransform.Box(0, height - 22, Column, 20); }
            Img(root, "Split", null, PanelLook.Rule).rectTransform.Box(half - 1, 0, 2, height - 6);
            float y = 28; foreach (var r in left) { GearRow(root, r, 0, y, w, true); y += GearRowHeight(r, w); }
            y = 28; foreach (var r in right) { GearRow(root, r, half + 20, y, w, false); y += GearRowHeight(r, w); }
        }

        // left: the maker, an arrow, the gear they made; right: the gear you made, an arrow, the one who put it to good use
        static void GearRow(RectTransform root, Block r, float x, float top, float w, bool makerFirst)
        {
            var tiles = (r.Items ?? new List<Block>()).Where(t => t.Kind == "tile").ToList();
            var per = GearPerLine(w);
            // your gear: the tiles, then the arrow and the one who put it to good use right after the first line of tiles
            var shown = Mathf.Min(tiles.Count, per); var after = x + shown * (GearTile + GearGap) - GearGap;
            float px = makerFirst ? x : after + GearArrow, ax = makerFirst ? x + GearPerson : after, tx = makerFirst ? x + GearPerson + GearArrow : x;
            var m = Marker(root, r.Icon, 26, layout: false); m.Box(px, top + (GearTile - 26) / 2, 26, 26);
            var n = Label(root, r.Title, PanelLayout.Text.Body, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
            n.rectTransform.Box(px + 34, top, GearPerson - 36, GearTile);
            Fill(root, "Line", GearThread).rectTransform.Box(ax + 2, top + GearTile / 2 - 1, GearArrow - 16, 2);
            VocabImg(root, "Arrow", "thread-arrow", GearThread).rectTransform.Box(ax + GearArrow - 18, top + GearTile / 2 - 6, 12, 12);
            for (int k = 0; k < tiles.Count; k++)
            {
                int line = k / per, at = k % per;
                var slot = Kit(root, "Slot", "slot").rectTransform; slot.Box(tx + at * (GearTile + GearGap), top + line * (GearTile + GearGap), GearTile, GearTile);
                var sprite = PanelLook.Icon(tiles[k].Icon);
                if (sprite) { var i = Img(slot, "Item", sprite, Color.white).rectTransform; i.Stretch(); var inset = GearTile * 14f / 128f; i.offsetMin = new Vector2(inset, inset); i.offsetMax = new Vector2(-inset, -inset); }
                if (!string.IsNullOrEmpty(tiles[k].Value))   // the count: a dark pill on the tile's lower right corner
                {
                    var pill = Kit(slot, "Count", "meter-track"); pill.color = new Color(0.03f, 0.025f, 0.02f, 0.97f);
                    var cw = Mathf.Max(26f, 12f + 9.5f * ("× " + tiles[k].Value).Length);
                    var ct = Label(pill.transform, "× " + tiles[k].Value, PanelLayout.Text.Small, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.Center); ct.textWrappingMode = TextWrappingModes.NoWrap; ct.rectTransform.Stretch();
                    var pr = pill.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(1, 0); pr.pivot = new Vector2(1, 0); pr.sizeDelta = new Vector2(cw, 22); pr.anchoredPosition = new Vector2(-3, 3);
                }
            }
            // the names of the kinds, one line under the tiles (a tile alone does not say what it is)
            var names = Label(root, string.Join(" \u00b7 ", tiles.Select(t => t.Title)), PanelLayout.Text.Floor, PanelLook.Muted, align: TextAlignmentOptions.TopLeft);
            names.textWrappingMode = TextWrappingModes.NoWrap; names.overflowMode = TextOverflowModes.Ellipsis;
            names.rectTransform.Box(tx, top + Mathf.CeilToInt(Mathf.Max(1, tiles.Count) / (float)per) * (GearTile + GearGap) - GearGap + 3, makerFirst ? w - GearPerson - GearArrow : w, GearNameH);
            Img(root, "Rule", null, PanelLook.Rule).rectTransform.Box(x, top + GearRowHeight(r, w) - 7, w, 1);
        }
    }

    /// <summary>One Fireside thread as a single mesh: the line as a mitred strip of the given width along its points, and the arrowhead triangle
    /// (FireLayout.StrokeOf). Points are in the RectTransform's local space with the pivot at the drawing's top left.</summary>
    sealed class ThreadStroke : MaskableGraphic
    {
        Vector2[] pts = new Vector2[0]; float width; Vector2 tip, left, right;
        public void Set(Vector2[] line, float w, Vector2 t, Vector2 l, Vector2 r) { pts = line; width = w; tip = t; left = l; right = r; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var c = (Color32)color; int n = pts.Length;
            for (int i = 0; n >= 2 && i < n; i++)
            {
                Vector2 Nrm(Vector2 a, Vector2 b) { var d = (b - a).normalized; return new Vector2(-d.y, d.x); }
                var nA = i > 0 ? Nrm(pts[i - 1], pts[i]) : Nrm(pts[i], pts[i + 1]); var nB = i + 1 < n ? Nrm(pts[i], pts[i + 1]) : nA;
                var m = (nA + nB).normalized; var k = Mathf.Max(0.5f, Vector2.Dot(m, nA));   // the mitre: half the width across the bend
                var o = m * (width / 2 / k);
                vh.AddVert(pts[i] + o, c, Vector2.zero); vh.AddVert(pts[i] - o, c, Vector2.zero);
                if (i > 0) { int b = 2 * i; vh.AddTriangle(b - 2, b - 1, b); vh.AddTriangle(b - 1, b + 1, b); }
            }
            int h = vh.currentVertCount; vh.AddVert(tip, c, Vector2.zero); vh.AddVert(left, c, Vector2.zero); vh.AddVert(right, c, Vector2.zero); vh.AddTriangle(h, h + 1, h + 2);
        }
    }
}
