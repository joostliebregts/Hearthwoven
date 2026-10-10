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
    /// r4over.js Gear shared): giving (players around the hearth, one thin flat line per gift along its FireLayout arc, its width and
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
            var h = Label(parent, text, 14, PanelLook.Muted, style: FontStyles.UpperCase, align: TextAlignmentOptions.MidlineLeft);
            h.characterSpacing = 14; h.textWrappingMode = TextWrappingModes.NoWrap; h.rectTransform.Box(x, top, w, 20);
        }

        // ----- giving: from whom to whom? -----

        const float FireH = 400, FireR = 140, ShieldD = 40, GiftRowH = 40, GiftPerson = 88, LegendH = 30, CountPx = 19, CountCompactPx = 16, BeadSpread = 68;
        static float FireW => Mathf.Min(360f, Column * 0.49f);   // the fire on the left, the list beside it
        static float ListX => FireW + 22;

        internal static void Giving(RectTransform col, Block b)
        {
            var items = b.Items ?? new List<Block>();
            var players = items.Where(i => i.Kind == "player").ToList();
            var gifts = items.Where(i => i.Kind == "gift").ToList();
            if (players.Count == 0) return;
            var mine = gifts.Where(g => g.Selected).ToList(); var others = gifts.Where(g => !g.Selected).ToList();
            float ScopeH(string v) => string.IsNullOrEmpty(v) ? 0 : GiftScopeH;
            int Rows(List<Block> gs) => gs.Sum(g => Math.Max(1, g.Items?.Count ?? 0));   // a gift with meals and feast servings: a list line each
            var listH = (mine.Count > 0 ? 24 + ScopeH(b.Value) + Rows(mine) * GiftRowH : 0) + (others.Count > 0 ? 30 + ScopeH(b.Value2) + Rows(others) * GiftRowH : 0);
            var root = Node("Fireside", col); Size(root, -1, LegendH + Mathf.Max(FireH, listH));

            // the circle: players at even angles from the top (the owner first), the fire in the middle (FireLayout.cs: y up, origin top left)
            float cx = FireW / 2, cy = LegendH + FireH / 2 + 2;
            var plan = FireLayout.Build(b, cx, cy);
            var pos = plan.Pos.ToDictionary(kv => kv.Key, kv => new Vector2((float)kv.Value.x, (float)kv.Value.y), StringComparer.OrdinalIgnoreCase);
            var hearthPx = (float)FireLayout.Hearth;   // the threads keep FireLayout.Keep from its middle
            var fire = VocabImg(root, "Hearth", "hearth-fire", Color.white).rectTransform; fire.Box(cx - hearthPx / 2, cy - hearthPx / 2, hearthPx, hearthPx);
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
                var name = Label(root, p.Title, 16, p.Selected ? PanelLook.Gold : PanelLook.Text, style: FontStyles.Bold, align: below ? TextAlignmentOptions.Top : TextAlignmentOptions.Bottom);
                name.textWrappingMode = TextWrappingModes.NoWrap; name.rectTransform.Box(nx - 70, below ? -at.y + ShieldD / 2 + 2 : -at.y - ShieldD / 2 - 24, 140, 22);
            }

            // the list beside the fire: between the owner and the others, then around the fire; each list says its scope once in its caption (fix4-rest)
            float y = LegendH, w = Column - ListX;
            // each section says its source once, on a quiet line under its caption (Value, Value2); a row adds words only where it differs
            // the name columns as wide as the longest name needs (Joost in game: a long name was cut on a row with room to spare), at least the old 88 px,
            // at most what leaves the item line its 200 px; only then a name is cut
            float longest = 0;
            foreach (var p in players)
            {
                var probe = Label(root, p.Text ?? p.Title ?? p.Id, 16, PanelLook.Text); longest = Mathf.Max(longest, Mathf.Ceil(probe.preferredWidth)); UnityEngine.Object.Destroy(probe.gameObject);
            }
            var personW = Mathf.Clamp(longest + 38, GiftPerson, Mathf.Max(GiftPerson, (w - 200) / 2));
            void Lines(Block g) { foreach (var line in g.Items != null && g.Items.Count > 0 ? g.Items : new List<Block> { g }) { GiftRow(root, g, line, players, ListX, y, w, personW); y += GiftRowH; } }
            if (mine.Count > 0) { Caption(root, b.Title, ListX, y, w); y += 24; y = Scope(root, b.Value, ListX, y, w); foreach (var g in mine) Lines(g); }
            if (others.Count > 0) { y += 4; Caption(root, b.Text + ", fainter", ListX, y, w); y += 26; y = Scope(root, b.Value2, ListX, y, w); foreach (var g in others) Lines(g); }
            // the legend above the drawing: which thread is food, which is gear, where the arrow points, what a faint thread is
            var key = Node("Legend", root); key.Box(0, 0, Column, 24);
            Layout(key.gameObject.AddComponent<HorizontalLayoutGroup>(), 7, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(b.Note)) { Label(key, b.Note, 14, PanelLook.Muted, style: FontStyles.Italic).textWrappingMode = TextWrappingModes.NoWrap; Size(Node("Gap", key), 14, 1); }
            Size(Fill(key, "Food", FoodThread), 22, 4); Label(key, PanelModel.LegendFood, 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            Size(Node("Gap", key), 14, 1);
            Size(Fill(key, "Gear", GearThread), 22, 4); Label(key, PanelModel.LegendGear, 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            Size(Node("Gap", key), 14, 1);
            Size(VocabImg(key, "Arrow", "thread-arrow", PanelLook.Muted), 12, 12); Label(key, PanelModel.LegendArrow, 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            if (gifts.Any(g => g.Items != null && g.Items.Any(l => l.Tone == PanelModel.TeamworkTone)))   // a feast one made and another set out (0.8): its mark, once
            {
                Size(Node("Gap", key), 14, 1); TeamworkMark(key); Label(key, PanelModel.LegendTeamwork, 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        /// <summary>The teamwork mark (0.8, B22): the Together page's two-people line sprite in the list gold, small and calm.</summary>
        internal static void TeamworkMark(RectTransform parent) => Size(VocabImg(parent, "Teamwork", "list-together", PanelLook.Gold), 16, 16);

        const float GiftScopeH = 20, GiftItemPx = 24, GiftArrowY = 25;   // the arrow's line, from the row's top (its head 19 .. 31; the rule at 39)
        static float Scope(RectTransform root, string text, float x, float y, float w)
        {
            if (string.IsNullOrEmpty(text)) return y;
            var t = Label(root, text, 14, PanelLook.Faint, style: FontStyles.Italic, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.Box(x, y - 4, w, 18);
            return y + GiftScopeH;
        }

        // one gift as a thin flat line along its arc in its lane: the pair's bundle bends gently away from the fire, the arc and side chosen so no
        // thread runs along or crosses another where it need not (the geometry: FireLayout.Build); width by the count, the arrowhead 3 times the
        // width (FireLayout.StrokeOf, 0.7 variant A), both one mesh so a faint thread never darkens where line and head meet
        static void Thread(RectTransform layer, Block g, FireLayout.Geo geo)
        {
            var st = FireLayout.StrokeOf(geo, g.Fraction);
            var line = Node("Thread", layer); line.Stretch(); line.pivot = new Vector2(0, 1);   // local (0, 0) = the drawing's top left, y up: FireLayout's own coordinates
            var stroke = line.gameObject.AddComponent<ThreadStroke>(); stroke.raycastTarget = false; stroke.color = ThreadTint(g, !g.Selected);
            Vector2 V((double x, double y) p) => new Vector2((float)p.x, (float)p.y);
            stroke.Set(st.Line.Select(V).ToArray(), (float)st.Width, V(st.Tip), V(st.Left), V(st.Right));
            // a thread between two fellow players stays a faint thread: its count is in the list under "Among fellow players" (fix3-rest)
        }

        // the count of one gift: plain digits (19 px: Joost could not read 12 px in game) on a dark pill lying on its own thread, where FireLayout put it
        static void CountPill(RectTransform beads, FireLayout.Pill p)
        {
            var pill = Kit(beads, "CountPill", "meter-track"); pill.color = new Color(0.03f, 0.025f, 0.02f, 0.97f);
            var t = Label(pill.transform, p.Text, p.Compact ? CountCompactPx : CountPx, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.Center, face: PanelLook.Plain);
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.Stretch();
            var r = pill.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2((float)p.Width, (float)p.H); r.anchoredPosition = new Vector2((float)p.At.x, (float)p.At.y);
        }

        // maker -> the item and its count over a short thread, the grateful verb under it -> the one who enjoyed it
        // line: the gift itself, or one of its lines (meals, feast servings: "set out a Mountains feast · × 3 enjoyed", teamwork: "Tor set it out · × 3 enjoyed")
        static void GiftRow(RectTransform root, Block g, Block line, List<Block> players, float x, float top, float w, float personW)
        {
            string Who(string id) { var p = players.FirstOrDefault(x2 => x2.Id == id); return p?.Text ?? p?.Title ?? id; }   // the list's short name ("You")
            var tint = ThreadTint(g, false);
            void Person(string id, float px)
            {
                var m = Marker(root, "person:" + id, 22, layout: false); m.Box(px, top + GiftArrowY - 11, 22, 22);   // the names at both ends of the arrow, on its line
                var t = Label(root, Who(id), 16, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
                t.rectTransform.Box(px + 30, top + GiftArrowY - 12, personW - 32, 24);
            }
            Person(g.Title, x);
            float mx = x + personW, mw = w - 2 * personW;
            // one line ABOVE the arrow (Joost 2026-10-09): the item (1.5 times the old 16 px), "× 3" and the verb side by side; nothing under the arrow
            var said = Node("Item", root); said.Box(mx, top - 1, mw, GiftItemPx);
            Layout(said.gameObject.AddComponent<HorizontalLayoutGroup>(), 6, TextAnchor.MiddleCenter);
            Marker(said, line.Icon, GiftItemPx);
            if (line.Tone == PanelModel.TeamworkTone) TeamworkMark(said);   // "Tor set it out": one made it, another set it out
            if (line != g && !string.IsNullOrEmpty(line.Title)) Label(said, line.Title + " ·", 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            Label(said, "× " + line.Value, 15, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            Label(said, line.Note, 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            if (!string.IsNullOrEmpty(g.Value2)) { var sc = Label(said, g.Value2, 14, PanelLook.Faint, style: FontStyles.Italic); sc.textWrappingMode = TextWrappingModes.NoWrap; }   // only where it differs from the section: their last session
            Fill(root, "Line", tint).rectTransform.Box(mx + 4, top + GiftArrowY - 1, mw - 18, 2);
            var arrow = VocabImg(root, "Arrow", "thread-arrow", tint).rectTransform; arrow.Box(mx + mw - 16, top + GiftArrowY - 6, 12, 12);
            Person(g.Text, x + w - personW + 6);
            Img(root, "Rule", null, PanelLook.Rule).rectTransform.Box(x, top + GiftRowH - 1, w, 1);   // B32: 8 px of air between the arrow and the row's rule
        }

        // ----- together: the group's whole, per category; never a ranking or a combined score -----

        const float ChipGap = 4;
        static readonly Color ShareLight = new Color(1f, 0.973f, 0.902f);   // light ink on a share segment: brighter than LightInk so pine reads at 4.7:1


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

            // Damage dealt: the chips Battle uses, under the category chips (since install, last hour, last 3 hours, this session)
            var windows = (b.Items ?? new List<Block>()).FirstOrDefault(c => c.Kind == "switch");
            if (windows != null) Switch(col, windows, link, (r, x) => { });
            else if (cats.Any(c => c.Id == "dealt")) Spacer(col, 28);   // the window row is reserved: choosing Damage dealt moves nothing

            // the chosen category large: the group's total, one bar coloured per player, the legend under it
            var parts = (pick.Items ?? new List<Block>()).Where(p => p.Fraction > 0).ToList();
            HeroNumber(Line(col, 0, TextAnchor.LowerLeft), new Block { Value = pick.Value, Title = pick.Text, Note = pick.Note }, HeroSize, HeroLabel);   // the shared hero's look
            if (pick.Before != null) HeroBeforeLine(col, pick);   // 0.8 Compare: the period before on a line of its own (the hero's words are long here)
            if (parts.Count < 2) { TogetherScope(col, b, pick); return; }   // alone: the number is the page (with its scope and keys); the bar and the matrix of one are noise
            // the book's one bar form (BarFormUi.cs) in the players' colours: the bar, and the list names each player with their number and share
            var compared = (b.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == PanelModel.CompareRowsKind && i.Id == pick.Id);   // 0.8 Compare: a row per player, now beside the period before
            if (compared != null) CompareRows(col, compared);
            else BarWithList(col, parts, false, new BarLook { Colour = p => PersonTint(p.Id) });
            TogetherScope(col, b, pick);   // since when the category counts, and the keys that flip the chips (fix4-rest)

            // each player's share (V1, Joost 2026-10-08, proto/share-pA.png): one 100 % bar per category in player colours, You
            // first then the legend's order; the share inside a segment wide enough; the category's gold icon before its name; the
            // chosen category's row outlined gold; the players' legend once, top right
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
                var nm = Label(k, p.Title, 14, PanelLook.Text, style: FontStyles.Bold); nm.textWrappingMode = TextWrappingModes.NoWrap;   // the name in the text colour: the shield carries the player's colour
            }
            const float RowH = 22, LabelW = 190, BarH = 20, CapH = 22;
            // windowed (Damage dealt in a time window): the row that follows the window first, then a caption and the rows that do not, dimmed (they count since install)
            var windowed = b.Tone == PanelModel.WindowedTone && !string.IsNullOrEmpty(b.Note);
            var order = windowed ? cats.Where(c => c == pick).Concat(cats.Where(c => c != pick)).ToList() : cats;
            var rows = Node("Shares", col); Size(rows, -1, cats.Count * (RowH + 1) + (windowed ? CapH : 0));
            var barW = Column - LabelW - 12;
            for (int c = 0; c < order.Count; c++)
            {
                var cat = order[c]; var on = cat == pick; var y = c * (RowH + 1) + (windowed && c > 0 ? CapH : 0);
                if (windowed && c == 1) Caption(rows, b.Note, 0, y - CapH + 3, Column);
                var dim = windowed && !on;
                if (PanelLook.Icon(cat.Icon) != null) { var ic = Marker(rows, cat.Icon, 18, layout: false); ic.Box(0, y + 3, 18, 18); if (cat.Icon.StartsWith("vocab:")) foreach (var im in ic.GetComponentsInChildren<Image>()) im.color = PanelLook.Gold; }
                var l = Label(rows, cat.Title, 14, on ? PanelLook.Gold : dim ? PanelLook.Faint : PanelLook.Muted, style: on ? FontStyles.Bold : FontStyles.Normal, align: TextAlignmentOptions.MidlineLeft);
                l.rectTransform.Box(24, y, LabelW - 26, RowH); l.textWrappingMode = TextWrappingModes.NoWrap; l.overflowMode = TextOverflowModes.Ellipsis;
                var track = Img(rows, "Bar", null, new Color(0, 0, 0, 0.35f)).rectTransform; track.Box(LabelW + 12, y + 1, barW, BarH);
                if (dim) { var cg = track.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0.5f; }   // the rows outside the window: dimmed
                float at = 0;
                foreach (var p in people)
                {
                    var part = cat.Items?.FirstOrDefault(q => q.Id == p.Id);
                    var share = part == null ? 0f : Mathf.Clamp01(part.Fraction);
                    if (share <= 0) continue;
                    var w = barW * share;
                    var seg = Img(track, "Part", null, PersonTint(p.Id)).rectTransform; seg.Box(at, 0, w, BarH);
                    if (at > 0) Img(track, "Cut", null, new Color(0, 0, 0, 0.55f)).rectTransform.Box(at, 0, 1, BarH);
                    var pct = Mathf.RoundToInt(share * 100) + "%";
                    if (w >= 10 + 8 * pct.Length)   // every segment the label fits in carries its share (a 5 % sliver too), at the 13 px floor
                    {
                        var slot = personColors.TryGetValue(p.Id ?? "", out var si) ? si : 0;
                        var t = Label(seg, pct, PanelLook.MinText, PanelModel.DarkTextOn(slot) ? DarkInk : ShareLight, style: FontStyles.Bold, align: TextAlignmentOptions.Center);
                        t.rectTransform.Stretch(); t.textWrappingMode = TextWrappingModes.NoWrap;
                    }
                    at += w;
                }
                if (on) Edge(track, PanelLook.Gold);   // the chosen category
            }
        }

        // the line under Together's numbers (fix4-rest, review: no scope on the chip, no key shown for the chips): since when this category counts, on the left;
        // on the right the keys that act here, each as a quiet keycap with what it flips (the view key the category, the filter key the window under Damage dealt)
        static float CapW(string key) => key.Length > 1 ? 14 + 8 * key.Length : 22;   // a keycap for a named key ("Tab") is wider than one for a letter
        static void TogetherScope(RectTransform col, Block together, Block pick)
        {
            var windows = (together.Items ?? new List<Block>()).FirstOrDefault(c => c.Kind == "switch");
            var catKey = together.KeyCap; var winKey = windows?.KeyCap;
            if (string.IsNullOrEmpty(pick.Value2) && string.IsNullOrEmpty(catKey) && string.IsNullOrEmpty(winKey)) return;
            var row = Line(col, 8); Size(row, -1, 24);
            if (!string.IsNullOrEmpty(pick.Value2)) Label(row, pick.Value2, 15, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            Size(Node("Gap", row), 0, 1).flexibleWidth = 1;
            if (!string.IsNullOrEmpty(catKey)) { Size(Keycap(row, catKey), CapW(catKey), 22); Label(row, "category", 15, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap; }
            if (!string.IsNullOrEmpty(winKey)) { Size(Node("Gap", row), 10, 1); Size(Keycap(row, winKey), CapW(winKey), 22); Label(row, "window", 15, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap; }
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
            if (!string.IsNullOrEmpty(b.Note)) { var nl = Label(root, b.Note, 14, PanelLook.Muted, style: FontStyles.Italic, align: TextAlignmentOptions.MidlineLeft); nl.textWrappingMode = TextWrappingModes.NoWrap; nl.rectTransform.Box(0, height - 22, Column, 20); }
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
            var n = Label(root, r.Title, 16, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); n.textWrappingMode = TextWrappingModes.NoWrap; n.overflowMode = TextOverflowModes.Ellipsis;
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
                    var ct = Label(pill.transform, "× " + tiles[k].Value, 15, PanelLook.Text, style: FontStyles.Bold, align: TextAlignmentOptions.Center); ct.textWrappingMode = TextWrappingModes.NoWrap; ct.rectTransform.Stretch();
                    var pr = pill.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(1, 0); pr.pivot = new Vector2(1, 0); pr.sizeDelta = new Vector2(cw, 22); pr.anchoredPosition = new Vector2(-3, 3);
                }
            }
            // the names of the kinds, one line under the tiles (a tile alone does not say what it is)
            var names = Label(root, string.Join(" \u00b7 ", tiles.Select(t => t.Title)), 14, PanelLook.Muted, align: TextAlignmentOptions.TopLeft);
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
