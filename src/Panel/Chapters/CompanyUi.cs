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
    /// r4over.js Gear shared): giving (players around the hearth, Codex's pre-baked thread arcs placed, rotated, scaled,
    /// mirrored and tinted once; thickness = count as stacked copies, never a stretched weave), together (category chips,
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
            var h = Label(parent, text, 13, PanelLook.Muted, style: FontStyles.UpperCase, align: TextAlignmentOptions.MidlineLeft);
            h.characterSpacing = 14; h.textWrappingMode = TextWrappingModes.NoWrap; h.rectTransform.Box(x, top, w, 20);
        }

        // ----- giving: from whom to whom? -----

        const float FireH = 400, FireR = 140, ShieldD = 40, GiftRowH = 32, GiftPerson = 88, LegendH = 30, CountPx = 19, CountCompactPx = 16, BeadSpread = 68;
        static float FireW => Mathf.Min(360f, Column * 0.49f);   // the fire on the left, the list beside it
        static float ListX => FireW + 22;
        // the three baked arcs (ui-language/README.md): 256 x 96, chord (8,8)-(248,8); their shape (a parabola, FireLayout.Arcs) and the end tangent are FireLayout's
        static readonly string[] ArcSprites = { "thread-arc-shallow", "thread-arc-medium", "thread-arc-deep" };

        internal static void Giving(RectTransform col, Block b)
        {
            var items = b.Items ?? new List<Block>();
            var players = items.Where(i => i.Kind == "player").ToList();
            var gifts = items.Where(i => i.Kind == "gift").ToList();
            if (players.Count == 0) return;
            var mine = gifts.Where(g => g.Selected).ToList(); var others = gifts.Where(g => !g.Selected).ToList();
            var listH = (mine.Count > 0 ? 24 + mine.Count * GiftRowH : 0) + (others.Count > 0 ? 30 + others.Count * GiftRowH : 0);
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
            if (mine.Count > 0) { Caption(root, b.Title + (string.IsNullOrEmpty(b.Value) ? "" : ", " + b.Value), ListX, y, w); y += 24; foreach (var g in mine) { GiftRow(root, g, players, ListX, y, w, string.IsNullOrEmpty(b.Value)); y += GiftRowH; } }
            if (others.Count > 0) { y += 4; Caption(root, b.Text + ", fainter" + (string.IsNullOrEmpty(b.Value2) ? "" : ", " + b.Value2), ListX, y, w); y += 26; foreach (var g in others) { GiftRow(root, g, players, ListX, y, w, string.IsNullOrEmpty(b.Value2)); y += GiftRowH; } }
            // the legend above the drawing: which thread is food, which is gear, where the arrow points, what a faint thread is
            var key = Node("Legend", root); key.Box(0, 0, Column, 24);
            Layout(key.gameObject.AddComponent<HorizontalLayoutGroup>(), 7, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(b.Note)) { Label(key, b.Note, 14, PanelLook.Muted, style: FontStyles.Italic).textWrappingMode = TextWrappingModes.NoWrap; Size(Node("Gap", key), 14, 1); }
            Size(Fill(key, "Food", FoodThread), 22, 4); Label(key, PanelModel.LegendFood, 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            Size(Node("Gap", key), 14, 1);
            Size(Fill(key, "Gear", GearThread), 22, 4); Label(key, PanelModel.LegendGear, 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            Size(Node("Gap", key), 14, 1);
            Size(VocabImg(key, "Arrow", "thread-arrow", PanelLook.Muted), 12, 12); Label(key, PanelModel.LegendArrow, 14, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
        }

        // one gift as a baked arc in its lane: the pair's bundle bends gently away from the fire, the arc and side chosen so no thread
        // runs along or crosses another where it need not (the geometry: FireLayout.Build)
        static void Thread(RectTransform layer, Block g, FireLayout.Geo geo)
        {
            var sprite = ArcSprites[geo.Arc]; var bendRight = geo.BendRight;
            var mid = new Vector2((float)geo.Mid.x, (float)geo.Mid.y); var dir = new Vector2((float)geo.Dir.x, (float)geo.Dir.y); var p1 = new Vector2((float)geo.P1.x, (float)geo.P1.y);
            var s = (float)geo.Scale; var angle = (float)geo.AngleDeg;
            var faint = !g.Selected; var tint = ThreadTint(g, faint);

            // the arc image, scaled uniformly; unmirrored its bend lies on the right of travel (y up), mirrored on the left
            var holder = Node("Thread", layer); holder.anchorMin = holder.anchorMax = new Vector2(0, 1); holder.pivot = new Vector2(0.5f, 0.5f);
            holder.anchoredPosition = mid; holder.sizeDelta = Vector2.zero; holder.localRotation = Quaternion.Euler(0, 0, angle);
            holder.localScale = new Vector3(s, bendRight ? s : -s, 1);
            var copies = 1 + Mathf.RoundToInt(2 * Mathf.Clamp01(g.Fraction));   // thickness = count: one, two or three cords side by side
            for (int k = 0; k < copies; k++)
            {
                var img = VocabImg(holder, "Cord", sprite, tint); img.preserveAspect = false;
                var r = img.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 1 - 8f / 96f);
                r.sizeDelta = new Vector2(256, 96); r.anchoredPosition = new Vector2(0, (k - (copies - 1) / 2f) * 2.2f / s);
            }
            // the arrow at the receiving end, along the arc's own end tangent (at most 18 px: the lanes beside it keep their own arrows clear)
            var endAngle = (float)geo.EndAngleDeg;
            var arrow = VocabImg(layer, "Arrow", "thread-arrow", tint).rectTransform;
            arrow.anchorMin = arrow.anchorMax = new Vector2(0, 1); arrow.pivot = new Vector2(22f / 24f, 0.5f);
            arrow.sizeDelta = new Vector2(12 + 2 * copies, 12 + 2 * copies); arrow.anchoredPosition = p1 + dir * 4; arrow.localRotation = Quaternion.Euler(0, 0, endAngle);
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
        static void GiftRow(RectTransform root, Block g, List<Block> players, float x, float top, float w, bool own)
        {
            string Who(string id) => players.FirstOrDefault(p => p.Id == id)?.Title ?? id;
            var tint = ThreadTint(g, false);
            void Person(string id, float px)
            {
                var m = Marker(root, "person:" + id, 22, layout: false); m.Box(px, top + 5, 22, 22);
                var t = Label(root, Who(id), 16, PanelLook.Text, align: TextAlignmentOptions.MidlineLeft); t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
                t.rectTransform.Box(px + 30, top + 4, GiftPerson - 32, 24);
            }
            Person(g.Title, x);
            float mx = x + GiftPerson, mw = w - 2 * GiftPerson;
            var said = Node("Item", root); said.Box(mx, top, mw, 16);
            Layout(said.gameObject.AddComponent<HorizontalLayoutGroup>(), 5, TextAnchor.MiddleCenter);
            Marker(said, g.Icon, 16);
            Label(said, "× " + g.Value, 15, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            Fill(root, "Line", tint).rectTransform.Box(mx + 4, top + 16, mw - 18, 2);
            var arrow = VocabImg(root, "Arrow", "thread-arrow", tint).rectTransform; arrow.Box(mx + mw - 16, top + 11, 12, 12);
            var verb = Node("Verb", root); verb.Box(mx, top + 17, mw, 14);
            Layout(verb.gameObject.AddComponent<HorizontalLayoutGroup>(), 6, TextAnchor.MiddleCenter);
            Label(verb, g.Note, 12, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            if (!own) { }   // the list's caption says the scope (since install, or as they last shared it)
            else if (g.SinceInstall) Since(verb, 11);
            else if (!string.IsNullOrEmpty(g.Value2)) { var sc = Label(verb, g.Value2, 12, PanelLook.Faint, style: FontStyles.Italic); sc.textWrappingMode = TextWrappingModes.NoWrap; }   // whose record: their last session
            Person(g.Text, x + w - GiftPerson + 6);
            Img(root, "Rule", null, PanelLook.Rule).rectTransform.Box(x, top + GiftRowH - 2, w, 1);
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
            if (parts.Count < 2) { TogetherScope(col, b, pick); return; }   // alone: the number is the page (with its scope and keys); the bar and the matrix of one are noise
            var bar = Kit(col, "Together", "meter-track"); Size(bar, -1, 26);
            var shares = parts.Select(p => Mathf.Max(p.Fraction, 4f / Column)).ToList(); var sum = shares.Sum(); float x = 0;
            for (int k = 0; k < parts.Count; k++)
            {
                var r = Fill(bar.transform, "Part", PersonTint(parts[k].Id)).rectTransform; var w = shares[k] / sum;
                r.anchorMin = new Vector2(x, 0); r.anchorMax = new Vector2(x + w, 1); r.pivot = new Vector2(0, 0.5f);
                r.offsetMin = new Vector2(k == 0 ? 3 : 1, 3); r.offsetMax = new Vector2(k == parts.Count - 1 ? -3 : -1, -3);
                // the name inside a segment wide enough for it (the key below names the small ones): colour never stands alone
                if (w * (Column - 6) >= 70)
                {
                    var slot = personColors.TryGetValue(parts[k].Id ?? "", out var pi) ? pi : 0;
                    var nm = Label(r, parts[k].Title, 14, PanelModel.DarkTextOn(slot) ? DarkInk : LightInk, style: FontStyles.Bold, align: TextAlignmentOptions.Center);
                    nm.rectTransform.Stretch(); nm.textWrappingMode = TextWrappingModes.NoWrap; nm.overflowMode = TextOverflowModes.Ellipsis;
                }
                x += w;
            }
            var key = Line(col, 22);
            foreach (var p in parts)
            {
                var e = Line(key, 7);
                Size(Fill(e, "Swatch", PersonTint(p.Id)), 14, 14);
                Label(e, p.Value, 22, PanelLook.Text, style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
                Label(e, p.Title, 15, PanelLook.Muted).textWrappingMode = TextWrappingModes.NoWrap;
                if (p.SinceInstall) Since(e, 13);
            }
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
                var l = Label(rows, cat.Title, 13, on ? PanelLook.Gold : dim ? PanelLook.Faint : PanelLook.Muted, style: on ? FontStyles.Bold : FontStyles.Normal, align: TextAlignmentOptions.MidlineLeft);
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
}
