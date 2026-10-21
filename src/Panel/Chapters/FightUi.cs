using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The 0.9 fight pages, drawn (Chapters/FightPages.cs; Joost's picks A, C and D in work/hearthwoven-0.8.5/boss-fight, round 3's dealt and
    /// received on one screen): the head (the boss in its ring or the dungeon's door, the group's dealt and received in the hero's second form,
    /// the boss's health lane or the run's time line), the foe rows (two damage bars a row: dealt split by player, received split by type, each
    /// column one scale from a quarter along with its true length under it, numbers inside, hover on the parts), the players' table (the same bars
    /// in its cells, blocks, parries, deaths with the types of their last seconds, time inside) and Runs and bosses' rows (the day, the boss or
    /// the door, how it ended, the dealt bar by player, the › to its page). Fixed rows, every part placed once per fill; nothing per frame.
    /// Mirrored by fightHead, fightFoeRows, fightPlayers and fightList in preview/panel-preview.html, measure for measure (the Ft* constants).
    /// </summary>
    public partial class PanelUi
    {
        // the head: picture, name over land on the left, the numbers on the right on one baseline; under them the lane or the run's line
        // 0.9.10: FtNumLess, the numbers' step down when the name needs the room (the book's next number size, PanelLayout.Text.Card); FtNameGap,
        // the least air between the name and the first number
        const float FtTopH = 48, FtRing = 44, FtDoor = 46, FtNameX = 58, FtName = 26, FtNameLeast = 20, FtLand = 15, FtNum = 43, FtNumLess = 38, FtNameGap = 16, FtNumLabel = 17, FtNumGap = 14, FtRuleGap = 24,
                    FtLaneTop = 8, FtLaneH = 20, FtLanePad = 10, FtKeyLineH = 20;
        // the foe rows: the name column (picture, name, ×N), each column's number, its bar; the boss first, the foes around it indented
        const float FtWho = 190, FtWhoMax = 240, FtIndent = 22, FtPic = 22, FtBossPic = 26, FtNumMin = 44, FtColGap = 10, FtSep = 18, FtChev = 18, FtFoeGap = 4, FtSubIndent = 38;
        // the players' table: a fixed width per column, the name column takes the rest
        // 0.8.6 (Joost: "check the deaths"): every column's numbers end at one right edge, its word's; Deaths' number too, its types' bar before it
        // (FtDeathNum, FtDeathGap) and the skull only in the column's head
        const float FtPlayerRow = 24, FtCellNum = 52, FtCellBarMin = 80, FtCellBarMax = 110, FtCellStat = 62, FtCellStop = 56, FtCellDeaths = 96, FtDeathBar = 48, FtDeathNum = 24, FtDeathGap = 8, FtHeadIcon = 20, FtCellGap = 10, FtNameMin = 190;
        // Runs and bosses: the day, the picture and name over how it ended, the number, the bar
        const float FtDate = 60, FtListPic = 28, FtListWho = 236, FtListLine1 = 24, FtListLine2 = 20, FtListGap = 8, FtAddRow = 26;
        const float FtKeyGap = 14, FtSwatch = 10, FtBarH = PanelModel.DamageTrack + PanelModel.DamageInner + PanelModel.DamageTrue;
        static readonly Color FtBossBand = PanelWash.BossBand.Game, FtBossEdge = PanelWash.BossEdge.Game, FtTree = PanelWash.Tree.Game;   // the mirror's .ft-boss and .ft-tree as the game draws them (PanelWash.cs)

        internal static void FightBlock(RectTransform col, Block b, Func<string, Action> link)
        {
            switch (b.Kind)
            {
                case PanelModel.FightHeadKind: FtHead(col, b); break;   // 0.8.6: the fight's own head (0.8.5 called the feed's FightHead here: "A dungeon ·" and nothing else)
                case PanelModel.FightFoesKind: FightFoeRows(col, b, link); break;
                case PanelModel.FightPlayersKind: FightPlayers(col, b); break;
                case PanelModel.FightListKind: FightList(col, b, link); break;
            }
        }

        static List<Block> FtItems(Block b, string kind) => (b?.Items ?? new List<Block>()).Where(i => i.Kind == kind).ToList();
        static float FtWidth(TextMeshProUGUI t) => Mathf.Ceil(t.preferredWidth) + 2;

        // the boss's ring with its trophy filling the aperture (as the Battle overview's biome tiles: the trophy under, Codex's ring over it)
        static void FtBossRing(RectTransform parent, string icon, float x, float top, float size)
        {
            var holder = Node("Boss", parent); holder.Box(x, top, size, size);
            var trophy = PanelLook.Icon(icon);
            if (trophy)
            {
                var tr = Img(holder, "Trophy", trophy, Color.white).rectTransform; var s = size * 0.86f;
                tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(s, s);
            }
            VocabImg(holder, "Ring", "boss-ring", Color.white).rectTransform.Stretch();
        }

        // a damage bar at (x, top), the book's one bar (DamageRowsUi.DamageBar, no share line: the keys stand under the list): its parts' numbers
        // inside where they fit, the true length under it, hover on its parts (a part too narrow shows its number in a tag over it)
        static void FtBar(RectTransform row, List<Block> parts, float fraction, bool dealt, float x, float top, float w)
        {
            if (parts.Count == 0) return;
            var bar = Node("Bar", row); bar.Box(x, top, w, FtBarH);
            DamageBar(bar, PanelModel.BarFormOf(parts, true), null, fraction, dealt, 0, w, shares: false);
        }

        // ---------- the head (A, D) ----------

        static void FtHead(RectTransform col, Block b)
        {
            col = VStack(col, 0);   // its own rows, its own air (the island's spacing only round the block, as in the preview)
            var run = b.Tone == "run";
            var lane = FtItems(b, "lane").FirstOrDefault(); var line = FtItems(b, "runline").FirstOrDefault();
            var r = BtRow(col, FtTopH);
            if (run) BtIcon(r, b.Icon, PanelLook.Gold, 0, (FtTopH - FtDoor) / 2, FtDoor);
            else if (b.Tone == "group")   // the land's tile with its emblem, as the Battle overview's biome strip draws it
            {
                var tile = RoundedPart(r, "Land", Hex(b.Colour, PanelLook.Muted)); tile.rectTransform.Box(0, (FtTopH - FtRing) / 2, FtRing, FtRing);
                VocabImg(tile.transform, "Emblem", VocabName(b.Icon), Color.white).rectTransform.Box((FtRing - 30) / 2, (FtRing - 30) / 2, 30, 30);
            }
            else FtBossRing(r, b.Icon, 0, (FtTopH - FtRing) / 2, FtRing);
            // the numbers from the right: each its number in gold and its words on one baseline, a rule between two
            var nums = FtItems(b, "number");
            var placed = new List<(TextMeshProUGUI words, TextMeshProUGUI num, Image rule)>();
            for (int k = 0; k < nums.Count; k++)
                placed.Add((BtText(r, nums[k].Title, 0, 0, 400, LineBox(FtNumLabel), FtNumLabel, PanelLook.Text, TextAlignmentOptions.BottomLeft),
                            BtText(r, nums[k].Value, 0, 0, 400, LineBox(FtNum), FtNum, PanelLook.Gold, TextAlignmentOptions.BottomLeft, FontStyles.Bold),
                            k > 0 ? BtRect(r, "Rule", PanelLook.Rule, 0, FtTopH - 34, 1, 30) : null));
            // right to left on a row's foot (dy: the row under the name's when they stand apart); brief: each number's short words ("dealt" for
            // "dealt together"); size: the numbers' (FtNum, or FtNumLess a step down); returns where they start
            float Place(bool brief, float size, float dy)
            {
                float right = Column, numTop = dy + FtTopH - LineBox(size), labelTop = dy + FtTopH - LineBox(FtNumLabel) - Mathf.Round((size - FtNumLabel) * Descent);
                for (int k = nums.Count - 1; k >= 0; k--)
                {
                    var (words, num, rule) = placed[k];
                    words.text = brief && !string.IsNullOrEmpty(nums[k].Text) ? nums[k].Text : nums[k].Title;
                    num.fontSize = size;
                    var ww = FtWidth(words); right -= ww; words.rectTransform.Box(right, labelTop, ww, LineBox(FtNumLabel));
                    var nw = FtWidth(num); right -= FtNumGap + nw; num.rectTransform.Box(right, numTop, nw, LineBox(size));
                    if (rule) { right -= FtRuleGap; rule.rectTransform.Box(right, dy + FtTopH - 34, 1, 30); right -= FtRuleGap + 1; }
                }
                return right;
            }
            // the name over its land beside the numbers while the room they leave holds both, measured: a long name steps down (26 to 20); still too
            // wide, the numbers say their words short; then (0.9.10, Joost in game: "Fader" and "Ashlands" drawn over 58 118) the numbers step down
            // to the book's next size; still too wide, the name and its land keep the row and the numbers take a row of their own under it. Never
            // one over the other, and the name is cut only past the whole row.
            var title = BtText(r, b.Title, FtNameX, string.IsNullOrEmpty(b.Text) ? (FtTopH - 30) / 2 : 0, 100, 30, FtName, PanelLook.Text, style: FontStyles.Bold);   // no land known: the name alone, in the row's middle
            var sub = string.IsNullOrEmpty(b.Text) ? null : BtText(r, b.Text, FtNameX, 28, 100, 20, FtLand, PanelLook.Muted);
            float room = 0;
            bool Fits(float edge)   // the name, stepped down as far as it needs, and its land in the room left of edge
            {
                room = Mathf.Max(0f, edge - FtNameX - FtNameGap);
                title.fontSize = FtName; for (var size = FtName - 2; size >= FtNameLeast && FtWidth(title) > room; size -= 2) title.fontSize = size;
                return FtWidth(title) <= room && (sub == null || FtWidth(sub) <= room);
            }
            var hasBrief = nums.Any(n => !string.IsNullOrEmpty(n.Text));
            var beside = nums.Count == 0 ? Fits(Column) || true
                       : Fits(Place(false, FtNum, 0)) || (hasBrief && Fits(Place(true, FtNum, 0))) || Fits(Place(hasBrief, FtNumLess, 0));
            if (!beside)
            {
                Size(r, -1, 2 * FtTopH);
                Fits(Column + FtNameGap);   // the name's row to its end
                if (Place(false, FtNum, FtTopH) < 0 && (!hasBrief || Place(true, FtNum, FtTopH) < 0)) Place(hasBrief, FtNumLess, FtTopH);
            }
            title.rectTransform.sizeDelta = new Vector2(room, title.rectTransform.sizeDelta.y);
            if (sub != null) sub.rectTransform.sizeDelta = new Vector2(room, sub.rectTransform.sizeDelta.y);
            if (lane != null) FightLane(col, lane);
            if (line != null) FightRunLine(col, line);
        }

        // the boss's health as one lane in its land's colour: what is left filled ("64 % left" in it), what went in the last minute hatched after
        // it, the rest the faint track every damage bar has; the note in the empty part of the track, or under it when the track has no room
        static void FightLane(RectTransform col, Block lane)
        {
            Spacer(col, FtLaneTop);
            var r = BtRow(col, FtLaneH); float w = Column;
            var track = RoundedPart(r, "Lane", DrTrack); track.pixelsPerUnitMultiplier = DrRadiusScale; track.rectTransform.Box(0, 0, w, FtLaneH);
            var c = Hex(lane.Colour, PanelLook.Muted);
            float left = Mathf.Clamp01(lane.Fraction) * w, was = Mathf.Clamp01(lane.Fraction2) * w;
            var etaOut = !string.IsNullOrEmpty(lane.Note);   // 0.8.6: the time left at this pace: inside the fill after "64 % left" when it fits there, else after the note
            if (was > left + 1)
            {
                var took = VocabImg(r, "Took", "grain-hatch-diag", c); took.rectTransform.Box(left, 0, was - left, FtLaneH);
                if (!took.sprite) { took.enabled = true; took.sprite = null; took.color = new Color(c.r, c.g, c.b, 0.45f); }
            }
            if (left > 0)
            {
                var fill = RoundedPart(r, "Left", c); fill.pixelsPerUnitMultiplier = DrRadiusScale; fill.rectTransform.Box(0, 0, left, FtLaneH);
                if (!string.IsNullOrEmpty(lane.Value))
                {
                    var t = BtText(r, etaOut ? lane.Value + " · " + lane.Note : lane.Value, FtLanePad, 0, Mathf.Max(0, left - 2 * FtLanePad), FtLaneH, PanelLook.MinText, SegmentInk(c), style: FontStyles.Bold);
                    if (etaOut && FtWidth(t) <= left - 2 * FtLanePad) etaOut = false;
                    else if (etaOut) t.text = lane.Value;
                    if (FtWidth(t) > left - 2 * FtLanePad) t.gameObject.SetActive(false);   // too short a fill for its words: the lane's length says it
                }
            }
            if (lane.Tone == "won")
            {
                BtIcon(r, "vocab:boss-mark", PanelLook.Gold, FtLanePad, (FtLaneH - 18) / 2, 18);
                BtText(r, lane.Title, FtLanePad + 24, 0, 200, FtLaneH, PanelLook.MinText, PanelLook.Text);
            }
            if (string.IsNullOrEmpty(lane.Text)) return;
            var said = etaOut ? lane.Text + " · " + lane.Note : lane.Text;
            var swatch = lane.Tone == "live" && was > left + 1;
            var note = BtText(r, said, 0, 0, w, FtLaneH, PanelLook.MinText, PanelLook.Muted);
            float nw = FtWidth(note), lead = swatch ? 18 + 6 : 0, need = nw + lead;
            if (need + FtLanePad + 8 <= w - Mathf.Max(left, was))
            {
                note.rectTransform.Box(w - FtLanePad - nw, 0, nw, FtLaneH);
                if (swatch) FtHatchSwatch(r, c, w - FtLanePad - nw - lead, (FtLaneH - 12) / 2);
                return;
            }
            // no room in the track: the note under the lane, from its start
            note.gameObject.SetActive(false); Destroy(note.gameObject);
            var under = BtRow(col, FtKeyLineH);
            if (swatch) FtHatchSwatch(under, c, 0, (FtKeyLineH - 12) / 2);
            BtText(under, said, lead, 0, w - lead, FtKeyLineH, PanelLook.MinText, PanelLook.Muted);
        }

        static void FtHatchSwatch(RectTransform r, Color c, float x, float top)
        {
            var sw = VocabImg(r, "Swatch", "grain-hatch-diag", c); sw.rectTransform.Box(x, top, 18, 12);
            if (!sw.sprite) { sw.enabled = true; sw.color = new Color(c.r, c.g, c.b, 0.45f); }
        }

        // a run in to out as one line: each fight a burst in the land's colour, a boss fight a gold band with the crown, each fall the death mark;
        // under it the two ends and the key
        static void FightRunLine(RectTransform col, Block line)
        {
            Spacer(col, FtLaneTop);
            var r = BtRow(col, FtLaneH); float w = Column;
            var track = RoundedPart(r, "Run", DrTrack); track.pixelsPerUnitMultiplier = DrRadiusScale; track.rectTransform.Box(0, 0, w, FtLaneH);
            var c = Hex(line.Colour, PanelLook.Muted);
            var spans = FtItems(line, "span");
            foreach (var s in spans.Where(s => s.Tone == "boss"))
            {
                float x0 = Mathf.Clamp01(s.Fraction) * w, x1 = Mathf.Max(x0 + 24, Mathf.Clamp01(s.Fraction2) * w);
                RoundedPart(r, "Boss", FtBossBand).rectTransform.Box(x0, 0, x1 - x0, FtLaneH);
                BtHollow(r, FtBossEdge, x0, 0, x1 - x0, FtLaneH);
                BtIcon(r, "vocab:boss-mark", PanelLook.Gold, x0 + 4, (FtLaneH - 16) / 2, 16);
            }
            foreach (var s in spans.Where(s => s.Tone != "boss"))
            {
                float x0 = Mathf.Clamp01(s.Fraction) * w, x1 = Mathf.Max(x0 + 3, Mathf.Clamp01(s.Fraction2) * w);
                var burst = RoundedPart(r, "Fight", c); burst.pixelsPerUnitMultiplier = DrRadiusScale; burst.rectTransform.Box(x0, 4, x1 - x0, FtLaneH - 8);
            }
            foreach (var f in FtItems(line, "fall"))
                BtIcon(r, "vocab:death", PanelLook.Text, Mathf.Clamp(Mathf.Clamp01(f.Fraction) * w - 7, 0, w - 14), (FtLaneH - 14) / 2, 14);
            // the ends and the key under the line
            var k = BtRow(col, FtKeyLineH);
            BtText(k, line.Value, 0, 0, 120, FtKeyLineH, PanelLook.MinText, PanelLook.Muted);
            var end = BtText(k, line.Value2, w - 120, 0, 120, FtKeyLineH, PanelLook.MinText, PanelLook.Muted, TextAlignmentOptions.MidlineRight);
            var parts = new List<(string kind, string words)> { ("fight", line.Text) };
            if (!string.IsNullOrEmpty(line.Note)) parts.Add(("boss", line.Note));
            if (FtItems(line, "fall").Count > 0) parts.Add(("fall", PanelModel.FallWord));
            var labels = parts.Select(p => BtText(k, p.words, 0, 0, 300, FtKeyLineH, PanelLook.MinText, PanelLook.Muted)).ToList();
            float total = labels.Sum(l => FtWidth(l)) + parts.Count * 22 + (parts.Count - 1) * FtKeyGap, x = (w - total) / 2;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].kind == "fall") BtIcon(k, "vocab:death", PanelLook.Text, x + 2, (FtKeyLineH - 14) / 2, 14);
                else if (parts[i].kind == "boss") { RoundedPart(k, "Swatch", FtBossBand).rectTransform.Box(x, (FtKeyLineH - 12) / 2, 16, 12); BtHollow(k, FtBossEdge, x, (FtKeyLineH - 12) / 2, 16, 12); }
                else RoundedPart(k, "Swatch", c).rectTransform.Box(x, (FtKeyLineH - 12) / 2, 16, 12);
                x += 22; var lw = FtWidth(labels[i]); labels[i].rectTransform.Box(x, 0, lw, FtKeyLineH); x += lw + FtKeyGap;
            }
        }

        // ---------- the foe rows ----------

        // the columns of a list of two-bar rows: the name column (picture, name, ×N: the widest, FtWho to FtWhoMax), each column's number (its
        // widest), the two tracks sharing what is left (dealt the larger), the chevron's room
        struct FtCols { public float Who, NumD, NumR, XD, WD, XR, WR; }
        static FtCols FoeCols(RectTransform parent, List<Block> rows, IEnumerable<Block> subs)
        {
            var c = new FtCols();
            var m = Label(parent, "", DrName, PanelLook.Text); m.textWrappingMode = TextWrappingModes.NoWrap;
            float who = FtWho, nd = FtNumMin, nr = FtNumMin;
            foreach (var r in rows)
            {
                m.text = r.Title ?? ""; var x0 = r.Tone == "add" ? FtIndent : 0;
                who = Mathf.Max(who, x0 + (r.Tone == "boss" ? FtBossPic : FtPic) + 8 + FtWidth(m) + (string.IsNullOrEmpty(r.Value2) ? 0 : 8 + BadgeWidth(parent, r.Value2)));
            }
            foreach (var v in rows.Concat(subs).Select(r => r.Value)) if (!string.IsNullOrEmpty(v)) { m.text = v; nd = Mathf.Max(nd, FtWidth(m)); }
            foreach (var v in rows.Concat(subs).Select(r => r.Note)) if (!string.IsNullOrEmpty(v)) { m.text = v; nr = Mathf.Max(nr, FtWidth(m)); }
            m.gameObject.SetActive(false); Destroy(m.gameObject);
            c.Who = Mathf.Min(who, FtWhoMax); c.NumD = nd; c.NumR = nr;
            var room = Mathf.Max(160f, Column - c.Who - FtColGap - nd - FtColGap - FtSep - nr - FtColGap - FtChev);
            c.WD = Mathf.Round(room * 0.55f); c.WR = room - c.WD;
            c.XD = c.Who + FtColGap + nd + FtColGap; c.XR = c.XD + c.WD + FtSep + nr + FtColGap;
            // stacked (PanelModel.FightStacked): a block of one column gives it all the room after the name and its number
            bool hasD = rows.Concat(subs).Any(r => !string.IsNullOrEmpty(r.Value)), hasR = rows.Concat(subs).Any(r => !string.IsNullOrEmpty(r.Note));
            if (hasD && !hasR) { c.WD = Mathf.Max(80f, Column - c.XD - FtChev); c.WR = 0; }
            else if (hasR && !hasD) { c.XR = c.Who + FtColGap + nr + FtColGap; c.WR = Mathf.Max(80f, Column - c.XR - FtChev); c.WD = 0; }
            return c;
        }

        static void FightFoeRows(RectTransform col, Block b, Func<string, Action> link)
        {
            var rows = FtItems(b, "foe");
            if (rows.Count == 0) return;
            col = VStack(col, 0);
            var list = VStack(col, FtFoeGap);
            var g = FoeCols(list, rows, rows.SelectMany(r => FtItems(r, "sub")));
            foreach (var row in rows)
            {
                var holder = list;
                if (row.Selected)   // the open row and its players' rows are one item: one quiet band behind them (as the Foes table's open foe)
                {
                    holder = VStack(list, FtFoeGap);
                    var band = RoundedPart(holder, "Band", BtBand); band.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; band.rectTransform.Stretch();
                    band.rectTransform.offsetMin = new Vector2(-6, -4); band.rectTransform.offsetMax = new Vector2(6, 4);
                }
                var r = BtRow(holder, FtBarH);
                if (link != null && !string.IsNullOrEmpty(row.Text))
                {
                    var ground = RoundedPart(r, "Ground", Color.clear, raycast: true); ground.rectTransform.Stretch();
                    if (!row.Selected) { var lit = r.gameObject.AddComponent<TintOnHover>(); lit.Ground = ground; lit.Lit = BtRowHover; }
                    r.gameObject.AddComponent<Press>().Act = link(row.Text);
                    OpensMark(r, row.Selected, 2);
                }
                var x0 = row.Tone == "add" ? FtIndent : 0f;
                if (row.Tone == "boss") FtBossRing(r, row.Icon, x0, (PanelModel.DamageTrack - FtBossPic) / 2, FtBossPic);
                else BtMarker(r, row.Icon, x0, (PanelModel.DamageTrack - FtPic) / 2, FtPic);
                var nx = x0 + (row.Tone == "boss" ? FtBossPic : FtPic) + 8;
                var chip = string.IsNullOrEmpty(row.Value2) ? 0 : BadgeWidth(r, row.Value2) + 8;
                var name = BtText(r, row.Title, nx, 0, g.Who - nx - chip, PanelModel.DamageTrack, DrName, PanelLook.Text);
                var nw = Mathf.Min(FtWidth(name), g.Who - nx - chip); name.rectTransform.sizeDelta = new Vector2(nw, name.rectTransform.sizeDelta.y);
                if (chip > 0) Badge(r, row.Value2, nx + nw + 8, (PanelModel.DamageTrack - BadgeH) / 2);
                FtRowBars(r, row, g);
                foreach (var sub in FtItems(row, "sub"))
                {
                    var s = BtRow(holder, FtBarH);
                    BtMarker(s, sub.Icon, FtSubIndent, (PanelModel.DamageTrack - 20) / 2, 20);
                    BtText(s, sub.Title, FtSubIndent + 28, 0, g.Who - FtSubIndent - 28, PanelModel.DamageTrack, PanelLayout.Text.Body, PanelLook.Text);
                    FtRowBars(s, sub, g);
                }
            }
            FtKeys(col, FtItems(b, "dkey").FirstOrDefault(), FtItems(b, "rkey").FirstOrDefault(), g.XR - g.NumR - FtColGap);
            var weak = FtItems(b, PanelModel.WeakLineKind);
            if (weak.Count > 0)   // 0.9.4 draft (A): the named lines' groups start at one x, after the widest name
            {
                var m = Label(col, "", PanelLook.MinText, PanelLook.Text, style: FontStyles.Bold); m.textWrappingMode = TextWrappingModes.NoWrap; float names = 0;
                foreach (var line in weak) if (!string.IsNullOrEmpty(line.Title)) { m.text = line.Title; names = Mathf.Max(names, Mathf.Min(FtWidth(m), Column * FtWeakNameMax)); }
                m.gameObject.SetActive(false); Destroy(m.gameObject);
                foreach (var line in weak) FtWeakLine(col, line, names);
            }
        }

        // 0.9.4 draft (A, Chapters/FightWeakness.cs): a kind's weaknesses under the keys: the kinds' names when the fight has more than one (the groups
        // of every named line from one x, after the widest name), or the boss's ring; then per group its caption in small capitals (the keys' own)
        // and its types, on as many lines as it takes, a group never split, a next line under the first group. Each group its own form (Joost: "make
        // each resist, weakness, immune more visually distinct"; by shape, the type colours stay the types'): weak to a solid chip (the track's
        // ground, the swatch filled, the name bright, the multiplier in gold); resists an open one (no ground, the swatch an outline, quiet words);
        // immune a struck one (the outline crossed, the name struck through, faint)
        const float FtWeakGap = 16, FtWeakName = 12, FtWeakRing = 18, FtWeakNameMax = 0.4f, FtCaptionGap = 8, FtChipH = 18, FtChipPad = 4, FtChipIn = 3, FtChipGap = 4, FtCapSpacing = 8,
                    FtCross = 14, FtCrossLine = 2, FtCrossTurn = 45;
        static void FtWeakLine(RectTransform col, Block line, float names)
        {
            var r = BtRow(col, FtKeyLineH); float x = 0, start = 0;
            if (line.Tone == "boss") { FtBossRing(r, line.Icon, 0, (FtKeyLineH - FtWeakRing) / 2, FtWeakRing); x = start = FtWeakRing + FtWeakName; }   // a boss with foes around: its ring
            else if (!string.IsNullOrEmpty(line.Title))
            {
                var t = BtText(r, line.Title, 0, 0, Column, FtKeyLineH, PanelLook.MinText, PanelLook.Text, style: FontStyles.Bold);
                var tw = Mathf.Min(FtWidth(t), Column * FtWeakNameMax); t.rectTransform.sizeDelta = new Vector2(tw, t.rectTransform.sizeDelta.y);
                x = start = Mathf.Max(tw, names) + FtWeakName;
            }
            foreach (var g in FtItems(line, PanelModel.WeakKeyKind))
            {
                var probe = Node("Probe", r); var gw = FtWeakGroup(probe, g, 0); probe.gameObject.SetActive(false); Destroy(probe.gameObject);
                if (x > start && x + gw > Column) { r = BtRow(col, FtKeyLineH); x = start; }
                x = FtWeakGroup(r, g, x) + FtWeakGap;
            }
        }

        // one group: its caption, then a chip per type in the group's form; returns where it ends
        static float FtWeakGroup(RectTransform r, Block g, float x)
        {
            var cap = BtText(r, (g.Title ?? "").ToUpperInvariant(), x, 0, Column, FtKeyLineH, PanelLook.MinText, PanelLook.Muted); cap.characterSpacing = FtCapSpacing;
            var cw = FtWidth(cap); cap.rectTransform.sizeDelta = new Vector2(cw, cap.rectTransform.sizeDelta.y); x += cw + FtCaptionGap;
            bool weak = g.Tone == PanelModel.WeakTone, immune = g.Tone == PanelModel.ImmuneTone;
            float chipTop = (FtKeyLineH - FtChipH) / 2, swTop = (FtKeyLineH - FtSwatch) / 2;
            var entries = FtItems(g, "key");
            for (int k = 0; k < entries.Count; k++)
            {
                var e = entries[k]; var c = Hex(e.Colour, PanelLook.Muted); var x0 = x;
                var ground = weak ? RoundedPart(r, "Chip", DrTrack) : null;   // drawn first: under the swatch and words
                x += FtChipPad;
                if (weak) RoundedPart(r, "Swatch", c).rectTransform.Box(x, swTop, FtSwatch, FtSwatch);
                else BtHollow(r, c, x, swTop, FtSwatch, FtSwatch);
                if (immune) FtCrossMark(r, x, swTop);
                x += FtSwatch + FtChipIn;
                var n = BtText(r, e.Title, x, 0, Column, FtKeyLineH, PanelLook.MinText, weak ? PanelLook.Text : immune ? PanelLook.Faint : PanelLook.Muted, style: immune ? FontStyles.Strikethrough : FontStyles.Normal);
                var nw = FtWidth(n); n.rectTransform.sizeDelta = new Vector2(nw, n.rectTransform.sizeDelta.y); x += nw + FtChipIn;
                var m = BtText(r, e.Value, x, 0, Column, FtKeyLineH, PanelLook.MinText, weak ? PanelLook.Gold : PanelLook.Faint, style: weak ? FontStyles.Bold : FontStyles.Normal);
                var mw = FtWidth(m); m.rectTransform.sizeDelta = new Vector2(mw, m.rectTransform.sizeDelta.y); x += mw + FtChipPad;
                if (ground) { ground.pixelsPerUnitMultiplier = DrRadiusScale; ground.rectTransform.Box(x0, chipTop, x - x0, FtChipH); }
                if (k < entries.Count - 1) x += FtChipGap;
            }
            return x;
        }

        // immune: a line across the type's outline, corner to corner
        static void FtCrossMark(RectTransform r, float x, float top)
        {
            var holder = Node("Cross", r); holder.Box(x + (FtSwatch - FtCross) / 2, top + (FtSwatch - FtCross) / 2, FtCross, FtCross);
            var line = Img(holder, "Slash", null, PanelLook.Faint).rectTransform;
            line.anchorMin = line.anchorMax = line.pivot = Vector2.one / 2; line.anchoredPosition = Vector2.zero; line.sizeDelta = new Vector2(FtCross, FtCrossLine);
            line.localRotation = Quaternion.Euler(0, 0, FtCrossTurn);
        }


        // a row's two bars: what was dealt (gold number, its parts: the players, or a player's types), what was received (its parts: the types)
        static void FtRowBars(RectTransform r, Block row, FtCols g)
        {
            if (!string.IsNullOrEmpty(row.Value)) BtText(r, row.Value, g.Who + FtColGap, 0, g.NumD, PanelModel.DamageTrack, DrName, PanelLook.Gold, TextAlignmentOptions.MidlineRight);
            FtBar(r, FtItems(row, "part"), row.Fraction, true, g.XD, 0, g.WD);
            if (!string.IsNullOrEmpty(row.Note)) BtText(r, row.Note, g.XR - FtColGap - g.NumR, 0, g.NumR, PanelModel.DamageTrack, DrName, PanelLook.Text, TextAlignmentOptions.MidlineRight);
            FtBar(r, FtItems(row, "rpart"), row.Fraction2, false, g.XR, 0, g.WR);
        }

        // the keys under the rows: DEALT and the players in their colours from the left; RECEIVED and the types with their share from the received
        // column (or, too long for that, ending at the island's edge; still too long, on a line of its own)
        static void FtKeys(RectTransform col, Block dealt, Block received, float recvX)
        {
            if (dealt == null && received == null) return;
            Spacer(col, 2);
            var r = BtRow(col, FtKeyLineH);
            var dw = dealt == null ? 0 : FtKeyRun(r, dealt, 0);
            if (received == null) return;
            var probe = Node("Probe", r); var rw = FtKeyRun(probe, received, 0); probe.gameObject.SetActive(false); Destroy(probe.gameObject);
            var x = dealt == null ? 0 : Mathf.Max(recvX, dw + 24);
            if (x + rw > Column) x = Column - rw;
            if (x < dw + 24) { r = BtRow(col, FtKeyLineH); x = 0; }
            FtKeyRun(r, received, x);
        }

        // one key: its caption in small capitals, then swatch and words per entry; returns where it ends
        static float FtKeyRun(RectTransform r, Block key, float x)
        {
            if (!string.IsNullOrEmpty(key.Title))
            {
                var cap = BtText(r, key.Title.ToUpperInvariant(), x, 0, 200, FtKeyLineH, PanelLook.MinText, PanelLook.Muted); cap.characterSpacing = FtCapSpacing;
                var cw = FtWidth(cap); cap.rectTransform.sizeDelta = new Vector2(cw, cap.rectTransform.sizeDelta.y); x += cw + 10;
            }
            foreach (var e in FtItems(key, "key"))
            {
                var sw = RoundedPart(r, "Swatch", Hex(e.Colour, PanelLook.Muted)); sw.pixelsPerUnitMultiplier = 5f / 3f; sw.rectTransform.Box(x, (FtKeyLineH - FtSwatch) / 2, FtSwatch, FtSwatch);
                x += FtSwatch + 5;
                var t = BtText(r, e.Title, x, 0, 200, FtKeyLineH, PanelLook.MinText, PanelLook.Muted); var tw = FtWidth(t); t.rectTransform.sizeDelta = new Vector2(tw, t.rectTransform.sizeDelta.y);
                x += tw + FtKeyGap;
            }
            return x - FtKeyGap;
        }

        // ---------- the players' table ----------

        static float FtCellW(string id, float bar) => id == "dealt" || id == "received" ? FtCellNum + 8 + bar : id == "deaths" ? FtCellDeaths : id == "stopped" ? FtCellStop : FtCellStat;

        static void FightPlayers(RectTransform col, Block b)
        {
            col = VStack(col, 0);
            var cols = FtItems(b, "col");
            // 0.8.6: a column of numbers is at least as wide as its head (its word and its icon), so a head never reaches over the column before it
            // (Parries' icon sat beside Blocks' word)
            var m = Label(col, "", PanelLook.MinText, PanelLook.Muted); m.textWrappingMode = TextWrappingModes.NoWrap;
            float CellW(Block c, float barW) { if (c.Id == "dealt" || c.Id == "received") return FtCellW(c.Id, barW); m.text = c.Title ?? ""; return Mathf.Max(FtCellW(c.Id, barW), FtWidth(m) + (string.IsNullOrEmpty(c.Icon) ? 0 : FtHeadIcon)); }
            var fixedW = cols.Where(c => c.Id != "dealt" && c.Id != "received").Sum(c => CellW(c, 0)) + cols.Count * FtCellGap;
            var bars = cols.Count(c => c.Id == "dealt" || c.Id == "received");
            var bar = bars == 0 ? 0 : Mathf.Clamp((Column - FtNameMin - fixedW - bars * (FtCellNum + 8)) / bars, FtCellBarMin, FtCellBarMax);
            var widths = cols.Select(c => CellW(c, bar)).ToList();
            m.gameObject.SetActive(false); Destroy(m.gameObject);
            var nameW = Column - widths.Sum() - cols.Count * FtCellGap;
            var xs = new List<float>(); var x = nameW; foreach (var w in widths) { x += FtCellGap; xs.Add(x); x += w; }
            // the head: the caption and the dealt split's key, each column's word right-aligned over its number (an icon before it)
            var h = BtRow(col, FtKeyLineH);
            var cap = BtText(h, (b.Title ?? "").ToUpperInvariant(), 0, 0, nameW, FtKeyLineH, PanelLook.MinText, PanelLook.Muted); cap.characterSpacing = FtCapSpacing;
            float firstHead = Column;
            for (int k = 0; k < cols.Count; k++)
            {
                var c = cols[k]; var numEnd = xs[k] + (c.Id == "dealt" || c.Id == "received" ? FtCellNum : widths[k]);
                var t = BtText(h, c.Title, 0, 0, 120, FtKeyLineH, PanelLook.MinText, PanelLook.Muted, TextAlignmentOptions.MidlineRight); var tw = FtWidth(t);   // its right edge the numbers' (0.8.6)
                t.rectTransform.Box(numEnd - tw, 0, tw, FtKeyLineH);
                var start = numEnd - tw;
                if (!string.IsNullOrEmpty(c.Icon)) { BtIcon(h, c.Icon, PanelLook.Muted, numEnd - tw - FtHeadIcon, (FtKeyLineH - 16) / 2, 16); start -= FtHeadIcon; }
                firstHead = Mathf.Min(firstHead, start);
            }
            // the dealt split's key on the head line: after the caption where it fits before the first column's word, else in the caption's
            // place (the rows say they are the players), else on a line of its own under the rows
            var dkey = FtItems(b, "dkey").FirstOrDefault(); var keyUnder = false;
            if (dkey != null)
            {
                var probe = Node("Probe", h); var after = FtKeyRun(probe, dkey, FtWidth(cap) + 12); var alone = FtKeyRun(probe, dkey, 0); probe.gameObject.SetActive(false); Destroy(probe.gameObject);
                if (after + 12 <= firstHead) FtKeyRun(h, dkey, FtWidth(cap) + 12);
                else if (alone + 12 <= firstHead) { cap.gameObject.SetActive(false); FtKeyRun(h, dkey, 0); }
                else keyUnder = true;
            }
            foreach (var p in (b.Items ?? new List<Block>()).Where(i => i.Kind == "player" || i.Kind == "quiet"))
            {
                var r = BtRow(col, FtPlayerRow);
                BtMarker(r, p.Icon, 0, (FtPlayerRow - 20) / 2, 20);
                var name = BtText(r, p.Title, 30, 0, nameW - 30, FtPlayerRow, DrName, PanelLook.Text, style: FontStyles.Bold);
                var nw = Mathf.Min(FtWidth(name), nameW - 30); name.rectTransform.sizeDelta = new Vector2(nw, name.rectTransform.sizeDelta.y);
                if (!string.IsNullOrEmpty(p.Note)) BtText(r, p.Note, 30 + nw + 8, 0, Mathf.Max(0, nameW - 30 - nw - 8), FtPlayerRow, PanelLook.MinText, PanelLook.Faint, style: FontStyles.Italic);
                if (p.Kind == "quiet") { BtText(r, p.Text, xs.FirstOrDefault(), 0, Column - xs.FirstOrDefault(), FtPlayerRow, PanelLayout.Text.Body, PanelLook.Muted, style: FontStyles.Italic); continue; }
                float barTop = (FtPlayerRow - PanelModel.DamageTrack) / 2;
                for (int k = 0; k < cols.Count; k++)
                {
                    var id = cols[k].Id; var cx = xs[k];
                    if (id == "dealt" || id == "received")
                    {
                        var dealt = id == "dealt";
                        BtText(r, dealt ? p.Value : p.Value2, cx, barTop, FtCellNum, PanelModel.DamageTrack, DrName, dealt ? PanelLook.Gold : PanelLook.Text, TextAlignmentOptions.MidlineRight);
                        FtBar(r, FtItems(p, dealt ? "part" : "rpart"), dealt ? p.Fraction : p.Fraction2, dealt, cx + FtCellNum + 8, barTop, bar);
                    }
                    else if (id == "deaths")
                    {
                        // the number at the column's right edge in every row (0.8.5 put a fall's skull and number at the cell's left, a 0 at its right);
                        // the types of the fall's last seconds as a small bar before it
                        var d = FtItems(p, "deaths").FirstOrDefault(); var n = d?.Value ?? "0"; var end = cx + widths[k];
                        BtText(r, n, end - FtDeathNum, barTop, FtDeathNum, PanelModel.DamageTrack, DrName, n == "0" ? PanelLook.Faint : PanelLook.Text, TextAlignmentOptions.MidlineRight, n == "0" ? FontStyles.Normal : FontStyles.Bold);
                        if (n != "0") FtBar(r, FtItems(d, "rpart"), 1, false, end - FtDeathNum - FtDeathGap - FtDeathBar, barTop, FtDeathBar);
                    }
                    else
                    {
                        var v = FtItems(p, "stat").FirstOrDefault(s => s.Id == id)?.Value ?? "";
                        BtText(r, v, cx, barTop, widths[k], PanelModel.DamageTrack, DrName, v == "0" ? PanelLook.Faint : PanelLook.Text, TextAlignmentOptions.MidlineRight);
                    }
                }
            }
            if (keyUnder) { Spacer(col, 2); FtKeyRun(BtRow(col, FtKeyLineH), dkey, 0); }   // too long for the head line: under the rows
        }

        // ---------- Runs and bosses ----------

        static void FightList(RectTransform col, Block b, Func<string, Action> link)
        {
            col = VStack(col, 0);
            var rows = FtItems(b, "fight");
            // the caption: NOW or EARLIER, the total and its words, where the fight on now is; the players' key at the right
            var h = BtRow(col, FtKeyLineH);
            var cap = BtText(h, (b.Title ?? "").ToUpperInvariant(), 0, 0, 200, FtKeyLineH, PanelLook.MinText, PanelLook.Muted); cap.characterSpacing = FtCapSpacing;
            float x = FtWidth(cap) + 10;
            if (!string.IsNullOrEmpty(b.Value)) { var v = BtText(h, b.Value, x, 0, 120, FtKeyLineH, PanelLayout.Text.Small, PanelLook.Text, style: FontStyles.Bold); x += FtWidth(v) + 8; }
            if (!string.IsNullOrEmpty(b.Text)) { var t = BtText(h, b.Text, x, 0, 300, FtKeyLineH, PanelLook.MinText, PanelLook.Muted); x += FtWidth(t) + 12; }
            if (!string.IsNullOrEmpty(b.Note)) { BtIcon(h, PanelModel.RunsIcon, PanelLook.Muted, x, (FtKeyLineH - 16) / 2, 16); var n = BtText(h, b.Note, x + 22, 0, 400, FtKeyLineH, PanelLook.MinText, PanelLook.Muted); x += 22 + FtWidth(n) + 12; }
            var key = FtItems(b, "dkey").FirstOrDefault();
            if (key != null)
            {
                var probe = Node("Probe", h); var kw = FtKeyRun(probe, key, 0); probe.gameObject.SetActive(false); Destroy(probe.gameObject);
                if (Column - kw >= x) FtKeyRun(h, key, Column - kw);
            }
            Spacer(col, 6);
            // one scale for the list: each row's number and bar from the same x
            var m = Label(col, "", DrName, PanelLook.Text); m.textWrappingMode = TextWrappingModes.NoWrap; float num = FtNumMin;
            foreach (var v in rows.Concat(rows.SelectMany(r => FtItems(r, "fight"))).Concat(rows.Concat(rows.SelectMany(r => FtItems(r, "fight"))).SelectMany(r => FtItems(r, "foe"))).Select(r => r.Value)) if (!string.IsNullOrEmpty(v)) { m.text = v; num = Mathf.Max(num, FtWidth(m)); }
            m.gameObject.SetActive(false); Destroy(m.gameObject);
            float numX = FtDate + FtListWho + FtColGap, barX = numX + num + FtColGap, barW = Mathf.Max(80f, Column - barX - FtChev - FtColGap);
            var list = VStack(col, FtListGap);
            foreach (var row in rows)
            {
                FtListRow(list, row, false, numX, num, barX, barW, link); FtListAdds(list, row, false, numX, num, barX, barW);
                foreach (var n in FtItems(row, "fight")) { FtListRow(list, n, true, numX, num, barX, barW, link); FtListAdds(list, n, true, numX, num, barX, barW); }   // a boss fight on now in its run: its foes under it
            }
        }

        // the foes around the boss fight on now, a row each on the list's scale, under its row (under a run too: already right of the nested
        // boss's picture, and a step further in would cut "Seeker Brood ×17")
        static void FtListAdds(RectTransform list, Block row, bool nested, float numX, float num, float barX, float barW)
        {
            foreach (var add in FtItems(row, "foe"))
            {
                var r = BtRow(list, FtAddRow); var px = FtDate + FtListPic + 6;
                BtMarker(r, add.Icon, px, (PanelModel.DamageTrack - FtPic) / 2, FtPic);
                var nx = px + FtPic + 8; var chip = string.IsNullOrEmpty(add.Value2) ? 0 : BadgeWidth(r, add.Value2) + 8;
                var name = BtText(r, add.Title, nx, 0, FtDate + FtListWho - nx - chip, PanelModel.DamageTrack, DrName, PanelLook.Text);
                var nw = Mathf.Min(FtWidth(name), FtDate + FtListWho - nx - chip); name.rectTransform.sizeDelta = new Vector2(nw, name.rectTransform.sizeDelta.y);
                if (chip > 0) Badge(r, add.Value2, nx + nw + 8, (PanelModel.DamageTrack - BadgeH) / 2);
                BtText(r, add.Value, numX, 0, num, PanelModel.DamageTrack, DrName, PanelLook.Gold, TextAlignmentOptions.MidlineRight);
                FtBar(r, FtItems(add, "part"), add.Fraction, true, barX, 0, barW);
            }
        }

        // a row of the list: the day over the time, the picture, the name and how long over how it ended, the number, the bar by player, the ›
        static void FtListRow(RectTransform list, Block row, bool nested, float numX, float num, float barX, float barW, Func<string, Action> link)
        {
            var r = BtRow(list, FtListLine1 + FtListLine2);
            if (link != null && !string.IsNullOrEmpty(row.Id))
            {
                var ground = RoundedPart(r, "Ground", Color.clear, raycast: true); ground.rectTransform.Stretch();
                var lit = r.gameObject.AddComponent<TintOnHover>(); lit.Ground = ground; lit.Lit = BtRowHover;
                r.gameObject.AddComponent<Press>().Act = link(row.Id);
                OpensMark(r, false, 2);
            }
            var live = row.Selected;
            if (!string.IsNullOrEmpty(row.Value2)) BtText(r, row.Value2, 0, 0, FtDate - 4, FtListLine1, PanelLayout.Text.Small, live ? PanelLook.Gold : PanelLook.Text, style: FontStyles.Bold);
            if (!string.IsNullOrEmpty(row.Pill)) BtText(r, row.Pill, 0, string.IsNullOrEmpty(row.Value2) ? 0 : FtListLine1, FtDate - 4, string.IsNullOrEmpty(row.Value2) ? FtListLine1 : FtListLine2, PanelLook.MinText, PanelLook.Faint);
            var px = FtDate + (nested ? FtIndent : 0);
            if (nested)   // the run's line down to this row's picture
            {
                BtRect(r, "Tree", FtTree, FtDate + 8, -FtListGap - 4, 1, FtListLine1 / 2 + FtListGap + 4);
                BtRect(r, "Tree", FtTree, FtDate + 8, FtListLine1 / 2, FtIndent - 10, 1);
            }
            var pic = nested ? FtListPic - 4 : FtListPic;
            if (row.Tone == "run") BtIcon(r, row.Icon, PanelLook.Gold, px, (FtListLine1 - pic) / 2, pic);
            else FtBossRing(r, row.Icon, px, (FtListLine1 - pic) / 2, pic);
            var nx = px + pic + 8; var room = FtDate + FtListWho - nx;
            var name = BtText(r, row.Title, nx, 0, room, FtListLine1, DrName, PanelLook.Text);
            var nw = Mathf.Min(FtWidth(name), room); name.rectTransform.sizeDelta = new Vector2(nw, name.rectTransform.sizeDelta.y);
            if (!string.IsNullOrEmpty(row.Text)) BtText(r, row.Text, nx + nw + 8, 0, Mathf.Max(0, room - nw - 8), FtListLine1, PanelLook.MinText, PanelLook.Muted);
            float ox = nx;
            if (row.Tone != "run") { MarkImg(r, live ? PanelModel.PipOpen : row.Note == "Won" ? PanelModel.PipOn : PanelModel.PipOff, ox, FtListLine1 + (FtListLine2 - MarkPx) / 2, MarkPx); ox += MarkPx + 6; }
            BtText(r, row.Note, ox, FtListLine1, FtDate + FtListWho - ox, FtListLine2, PanelLook.MinText, row.Note == "Won" ? PanelLook.Text : PanelLook.Muted);
            BtText(r, row.Value, numX, 0, num, PanelModel.DamageTrack, DrName, PanelLook.Gold, TextAlignmentOptions.MidlineRight);
            FtBar(r, FtItems(row, "part"), row.Fraction, true, barX, 0, barW);
            var lane = FtItems(row, "lane").FirstOrDefault();
            if (lane != null) FtListLane(r, lane, barX, FtListLine1, barW);   // the fight on now: the boss's health in the row's second line, under its bar
        }

        // the boss's health as the fight page's lane, at (x, top) in a row (no note: its page says it)
        static void FtListLane(RectTransform r, Block lane, float x, float top, float w)
        {
            var track = RoundedPart(r, "Lane", DrTrack); track.pixelsPerUnitMultiplier = DrRadiusScale; track.rectTransform.Box(x, top, w, FtLaneH);
            var c = Hex(lane.Colour, PanelLook.Muted);
            float left = Mathf.Clamp01(lane.Fraction) * w, was = Mathf.Clamp01(lane.Fraction2) * w;
            if (was > left + 1) { var took = VocabImg(r, "Took", "grain-hatch-diag", c); took.rectTransform.Box(x + left, top, was - left, FtLaneH); if (!took.sprite) { took.enabled = true; took.color = new Color(c.r, c.g, c.b, 0.45f); } }
            if (left <= 0) return;
            var fill = RoundedPart(r, "Left", c); fill.pixelsPerUnitMultiplier = DrRadiusScale; fill.rectTransform.Box(x, top, left, FtLaneH);
            var t = BtText(r, lane.Value, x + FtLanePad, top, Mathf.Max(0, left - 2 * FtLanePad), FtLaneH, PanelLook.MinText, SegmentInk(c), style: FontStyles.Bold);
            if (FtWidth(t) > left - 2 * FtLanePad) t.gameObject.SetActive(false);
        }
    }
}
