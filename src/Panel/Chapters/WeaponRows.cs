using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// DPS per named weapon (0.8.6, Joost: "on your own stats you can also see at which skill level you tested it"): under each weapon kind of Battle > Damage >
    /// By weapon, one line per weapon, each with its damage, its DPS and the skill level you used it at ("Frostner level 3 · 42 DPS · Clubs 45", or "Clubs 38–45"
    /// when it changed in the window). The kind rows keep their totals. A friend's book shows THEIR By weapon the same way where their copy can answer (WeaponCellsIn):
    /// the short windows from their live minutes, Today, 7 days and 30 days from the day share. Never side by side: no weapon DPS on Everyone, in a players' table or
    /// in Compare (a compare draws rows of its own).
    ///
    /// 0.8.6 fold (Joost in game: "make Mistwalker a clickable thing with a visual indicator that it has levels, and just show Mistwalker at its current level and DPS
    /// with skill? Otherwise this list could be loooong"; his rule: the most recent DPS folded, the details open): a weapon used at several upgrade levels is ONE line,
    /// its newest (highest) level and that level's most recent DPS with the skill level it was at ("level 3 · 149 DPS ▲7 at 35 · 2 levels"), its damage the whole
    /// weapon's. It opens (the ›, a press, or the keys: WeaponKeys) to one line per upgrade level, as before the fold; a level line with several skill levels opens
    /// further. Each line opens on its own and stays open while the page, the book and the window stay (WeaponOpenKey, as Show older foes holds); a new page, book,
    /// window or opening of the book starts folded. No DPS yet (under SwingClock.Floor seconds of fighting): the slot is empty, never a dash (DpsAbout says when it shows).
    ///
    /// Blocks: a kind's list is one "wpnlist" (Id = the kind) after its dmgmix row; its Items are "wpn" lines: Title the weapon (or "level 3"), Value its damage,
    /// Value2 its DPS ("42 DPS", or null), Note its skill words (a folded weapon: "2 levels"), Change its change mark, Text the words before them ("level 3 · 42 DPS"),
    /// Tone "level" for an upgrade level under its weapon, Id the key it came from, Cursor the keys' cursor. A line drawn as "Text ▲7 · Note" (the mark a picture, as
    /// Compare's): the line's DPS is the MOST RECENT one, not the window's average (an average mixes old skill levels): the DPS at the highest skill level with at least
    /// SwingClock.Floor seconds of fighting in the window, with "at 40" after it when that level is under the highest (a folded weapon: always, so the line says the
    /// skill level of its DPS); the mark is the change since the lowest skill level that reached the floor (▲ 7 or ▼ 3 whole DPS; none when only one level reaches it
    /// or it did not change). No level reaches the floor: the window's own pace, or nothing, no mark.
    /// A line with two skill levels or more Opens: "wpnskill" rows after it, one per skill level, lowest first, the newest WeaponLevelsShown: Title "Swords 38", Value
    /// and Value2 its pace (empty under the floor), Fraction its pace on one scale (a skill level has its pace only, not its damage).
    /// </summary>
    public static partial class PanelModel
    {
        public const string WeaponListKind = "wpnlist", WeaponLineKind = "wpn", WeaponSkillKind = "wpnskill", WeaponLevelTone = "level", OtherWeapons = "Other weapons";
        /// <summary>0.8.6 (honest numbers): the line for the part of a kind's row its weapons do not hold (hits from before weapons were counted, the days before a
        /// Hearthwoven that counts them; hits on players, which no weapon line counts), so a kind's lines add up to its row.</summary>
        public const string NotByWeapon = "Not by weapon", NotByWeaponWords = "from before weapons were counted, or on players";
        /// <summary>The page's view key of the lines that are open (their Ids, OpenSep between), the key of the window they were opened in (a new one folds them),
        /// the keys' cursor (a line's Id) and the target a press on a line sends (Follow toggles that line).</summary>
        public const string WeaponOpenKey = "Battle/damage/weapon", WeaponOpenAtKey = "Battle/damage/weapon/at", WeaponCursorKey = "Battle/damage/weapon/cursor", WeaponToggleTarget = "weapon:";
        const char OpenSep = ';';
        public static string WeaponOpenLink(Block line) => WeaponToggleTarget + (line?.Id ?? "");
        /// <summary>The lines open on this page (none: an empty set).</summary>
        public static HashSet<string> WeaponOpen(PanelState s) =>
            s != null && s.View.TryGetValue(WeaponOpenKey, out var v) && !string.IsNullOrEmpty(v) ? new HashSet<string>(v.Split(OpenSep), StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        /// <summary>A press on a line (or Enter on the cursor's): it opens, or shuts when open; the other lines stay as they are.</summary>
        public static void ToggleWeapon(PanelState s, string id)
        {
            if (s == null || string.IsNullOrEmpty(id)) return;
            var open = WeaponOpen(s);
            if (!open.Remove(id)) open.Add(id);
            s.View[WeaponOpenKey] = string.Join(OpenSep.ToString(), open.OrderBy(x => x, StringComparer.Ordinal));
        }
        /// <summary>Another page or book, or the book opened again: every weapon folded, no cursor (PanelModel.Visited, PanelUi.Open).</summary>
        public static void FoldWeapons(PanelState s) { s.View.Remove(WeaponOpenKey); s.View.Remove(WeaponOpenAtKey); s.View.Remove(WeaponCursorKey); }
        /// <summary>The open lines for this window: kept while the book and the window stay, folded when either changes (the window's page itself: Visited).</summary>
        static HashSet<string> WeaponOpenIn(PanelState s, TimeWindow w)
        {
            if (s == null) return new HashSet<string>(StringComparer.Ordinal);
            var at = s.Player + "|" + s.Everyone + "|" + w;
            if (s.View.TryGetValue(WeaponOpenAtKey, out var was) && was != at) { s.View.Remove(WeaponOpenKey); s.View.Remove(WeaponCursorKey); }
            s.View[WeaponOpenAtKey] = at;
            return WeaponOpen(s);
        }
        /// <summary>The newest skill levels an open line shows.</summary>
        public const int WeaponLevelsShown = 12;
        /// <summary>At most this many named weapons under a kind; the rest add to "Other weapons".</summary>
        public const int WeaponsShown = 8;

        /// <summary>The weapons of a window as the book can answer them: your own book from the ledger (All, Session before the log has it, the days) or the log (10 min
        /// to 3 h, Session); a friend's from their live minutes (short windows and Session) or their day share (Today, 7 days, 30 days). null when this window cannot
        /// say (a friend's All; a copy from before 0.8.6, never read as "no weapons"; a day before the ledger).</summary>
        public static WeaponCells WeaponCellsIn(PanelInput input, TimeWindow w)
        {
            if (input == null) return null;
            if (input.IsSelf)
            {
                if (w == TimeWindow.SinceInstall || IsDayWindow(w) || w == TimeWindow.Session && input.Log == null) return DealtLedgerIn(input, w)?.Weapons;
                return input.Log == null ? null : WeaponsInLog(input, w);
            }
            if (IsDayWindow(w))
            {
                if (input.DayWeapons == null) return null;
                DateTime from = DayFrom(input, w), to = LocalToday(input);
                var sum = new WeaponCells();
                foreach (var day in input.DayWeapons)
                    if (DateTime.TryParseExact(day.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d >= from && d <= to) sum.Merge(day.Value);
                return sum;
            }
            return w == TimeWindow.SinceInstall || input.Log == null || !input.Log.WeaponsKept ? null : WeaponsInLog(input, w);
        }

        static WeaponCells WeaponsInLog(PanelInput input, TimeWindow w)
        {
            var from = SessionFrom(input);
            return input.Log.Weapons.Window(minute => InFilter(minute, "", w, "", input.NowUtc, input.Log.Span, from));
        }

        /// <summary>A pace for a line: "42 DPS", or "" while the weapon has under SwingClock.Floor seconds of fighting (the slot stays empty, never a dash).</summary>
        static string LinePace(double seconds, double swung) => SwingClock.Words(SwingClock.Pace(swung, seconds));

        /// <summary>A skill as the lines say it ("Swords", "Elemental Magic").</summary>
        public static string SkillTitle(PanelInput input, string skill) => SkillName(input, skill);

        static string WeaponTitle(PanelInput input, string prefab) =>
            prefab == WeaponCells.Fists ? "Fists" : GameName(input, prefab) ?? Prettify(prefab);

        /// <summary>
        /// The lists under the kind rows, one "wpnlist" per kind that has weapons in the window (null: nothing to show). <paramref name="shownKinds"/> are the kinds that
        /// have a row on the page: a list never stands without its row. A kind shows at most WeaponsShown named weapons by damage, the rest and what had no named
        /// weapon add to one "Other weapons" line; a weapon of several upgrade levels is one folded line (FoldedLine) unless <paramref name="open"/> holds its Id. 0.8.6 (honest numbers): with <paramref name="rowTotals"/> (each kind row's damage, from the same window's rows) the
        /// part of a row its weapons do not hold is one last line (NotByWeapon), so the lines add up to the row they sit under.
        /// </summary>
        public static Dictionary<string, Block> WeaponLists(PanelInput input, WeaponCells cells, IEnumerable<string> shownKinds, ICollection<string> open = null, IDictionary<string, double> rowTotals = null)
        {
            if (cells == null || cells.Count == 0) return null;
            var lists = new Dictionary<string, Block>();
            // kind -> weapon (prefab|skill) -> its levels
            var byKind = new Dictionary<string, Dictionary<string, List<(int q, WeaponCell c)>>>();
            var other = new Dictionary<string, WeaponCell>();
            foreach (var kv in cells.Map)
            {
                if (!(kv.Value.All > 0f) || !WeaponKey.Split(kv.Key, out var prefab, out var q, out var skill)) continue;
                var kind = DayHistory.WeaponKind(skill);
                if (prefab == WeaponKey.Other) { Fold(other, kind, kv.Value); continue; }
                if (!byKind.TryGetValue(kind, out var weapons)) byKind[kind] = weapons = new Dictionary<string, List<(int, WeaponCell)>>();
                var id = prefab + "|" + skill;
                if (!weapons.TryGetValue(id, out var levels)) weapons[id] = levels = new List<(int, WeaponCell)>();
                levels.Add((q, kv.Value));
            }
            foreach (var kind in shownKinds)
            {
                byKind.TryGetValue(kind, out var weapons); other.TryGetValue(kind, out var rest);
                var lines = new List<Block>();
                var ordered = (weapons ?? new Dictionary<string, List<(int q, WeaponCell c)>>())
                    .Select(kv => (id: kv.Key, levels: kv.Value.OrderBy(l => l.q).ToList(), all: kv.Value.Sum(l => (double)l.c.All)))
                    .OrderByDescending(x => x.all).ThenBy(x => x.id, StringComparer.Ordinal).ToList();
                foreach (var x in ordered.Skip(WeaponsShown)) foreach (var l in x.levels) { rest = rest ?? new WeaponCell(); rest.Merge(l.c); }
                foreach (var x in ordered.Take(WeaponsShown))
                {
                    var bar = x.id.IndexOf('|'); var prefab = x.id.Substring(0, bar); var skill = SkillName(input, x.id.Substring(bar + 1));
                    var name = WeaponTitle(input, prefab);
                    if (x.levels.Count == 1) { AddLine(lines, input, open, x.id, name, x.levels[0].c, skill, x.levels[0].q, false); continue; }
                    if (!FoldedLine(lines, open, x.id, name, x.levels, x.all, skill)) continue;
                    foreach (var l in x.levels) AddLine(lines, input, open, x.id + "|" + l.q, "level " + l.q.ToString(CultureInfo.InvariantCulture), l.c, skill, 0, true);
                }
                if (rest != null && rest.All > 0f) AddLine(lines, input, open, WeaponKey.Other, OtherWeapons, rest, null, 0, false, whole: true);
                if (lines.Count > 0 && rowTotals != null && rowTotals.TryGetValue(kind, out var row))
                {
                    var held = (rest != null ? (double)rest.All : 0) + ordered.Take(WeaponsShown).Sum(x => x.all);
                    if (row - held >= 0.5) lines.Add(new Block { Kind = WeaponLineKind, Id = NotByWeapon, Title = NotByWeapon, Value = NAtLeast(row - held), Text = NotByWeaponWords, Src = SrcPc, Source = TagMeasured });
                }
                if (lines.Count > 0) lists[kind] = new Block { Kind = WeaponListKind, Id = kind, Src = SrcPc, Source = TagMeasured, Items = lines };
            }
            return lists.Count > 0 ? lists : null;
        }

        static void Fold(Dictionary<string, WeaponCell> into, string kind, WeaponCell c) { if (!into.TryGetValue(kind, out var o)) into[kind] = o = new WeaponCell(); o.Merge(c); }

        /// <summary>A weapon of several upgrade levels as one line (the 0.8.6 fold): its newest level and that level's most recent DPS with the skill level it was at
        /// ("level 3 · 149 DPS at 35"), its change mark, "2 levels" after them, the whole weapon's damage; it Opens to its level lines. Returns whether it is open.</summary>
        static bool FoldedLine(List<Block> lines, ICollection<string> open, string id, string title, List<(int q, WeaponCell c)> levels, double all, string skill)
        {
            var (q, c) = levels[levels.Count - 1];   // the highest upgrade level: the newest (a weapon is upgraded, not downgraded)
            var t = c.Recent(SwingClock.Floor);
            var pace = t.Has ? SwingClock.Words(t.Dps) : LinePace(c.Sec, c.Swung);
            if (t.Has && c.HasLevel) pace += " at " + t.Level.ToString(CultureInfo.InvariantCulture);   // the skill level of this DPS (its name is the kind row's)
            var text = "level " + q.ToString(CultureInfo.InvariantCulture) + (pace.Length > 0 ? " · " + pace : "");
            var selected = open != null && open.Contains(id);
            lines.Add(new Block
            {
                Kind = WeaponLineKind, Id = id, Title = title, Value = NAtLeast(all), Value2 = pace.Length > 0 ? pace : null, Note = levels.Count.ToString(CultureInfo.InvariantCulture) + " levels",
                Change = ChangeMark(t), Src = SrcPc, Source = TagMeasured, Text = text, Opens = true, Selected = selected,
            });
            return selected;
        }

        /// <summary>The change mark of a most recent pace against the lowest skill level that reached the floor ("▲ 7"); null when none.</summary>
        static string ChangeMark(Trend t)
        {
            if (!t.Has || t.LowLevel == t.Level) return null;
            var d = Math.Round(t.Dps) - Math.Round(t.LowDps);
            return Math.Abs(d) >= 1 ? (d > 0 ? CompareUp : CompareDown) + Number(Math.Abs(d)) : null;
        }

        /// <summary>One line (and its skill levels when it is open): see the class words. <paramref name="whole"/>: a weapon's total over its levels or the other cell: the window's
        /// pace, no most recent one, no skill words, nothing to open.</summary>
        static void AddLine(List<Block> lines, PanelInput input, ICollection<string> open, string id, string title, WeaponCell c, string skill, int quality, bool level, bool whole = false)
        {
            var t = whole ? default(Trend) : c.Recent(SwingClock.Floor);
            var pace = t.Has ? SwingClock.Words(t.Dps) : LinePace(c.Sec, c.Swung);
            if (t.Has && c.HasLevel && t.Level < c.Hi) pace += " at " + t.Level.ToString(CultureInfo.InvariantCulture);   // the most recent pace is at a level under the highest (that one is under the floor)
            var skillWords = !whole && skill != null && c.HasLevel ? c.SkillWords(skill) : null;
            var text = quality > 0 ? "level " + quality.ToString(CultureInfo.InvariantCulture) + (pace.Length > 0 ? " · " + pace : "") : pace;
            var parts = whole ? new LevelPart[0] : c.SortedParts();
            var opens = parts.Length >= 2 && skill != null;
            var line = new Block
            {
                Kind = WeaponLineKind, Id = id, Title = title, Value = NAtLeast(c.All), Value2 = pace.Length > 0 ? pace : null, Note = skillWords, Change = ChangeMark(t), Tone = level ? WeaponLevelTone : null, Src = SrcPc, Source = TagMeasured,
                Text = text, Opens = opens, Selected = opens && open != null && open.Contains(id),
            };
            lines.Add(line);
            if (!line.Selected) return;
            var shown = parts.Skip(Math.Max(0, parts.Length - WeaponLevelsShown)).ToList();
            var top = shown.Select(p => p.Sec >= SwingClock.Floor && p.Swung > 0f ? p.Swung / p.Sec : 0.0).DefaultIfEmpty(0).Max();
            foreach (var p in shown)
            {
                var dps = p.Sec >= SwingClock.Floor && p.Swung > 0f ? p.Swung / p.Sec : 0.0; var words = LinePace(p.Sec, p.Swung);
                lines.Add(new Block
                {
                    Kind = WeaponSkillKind, Id = id + "|" + p.Level.ToString(CultureInfo.InvariantCulture), Title = skill + " " + p.Level.ToString(CultureInfo.InvariantCulture), Value = words, Value2 = words, Text = words,
                    Fraction = top > 0 ? (float)(dps / top) : 0f, Src = SrcPc, Source = TagMeasured,
                });
            }
        }

        // ---------- the keys (0.8.6 fold): as on Foes (Chapters/FoesKeys.cs), A/D (the arrows, the D-pad) move a cursor between the lines that open, Enter, Space or
        // the pad's A opens or shuts the cursor's line, Q/E still turn the chapters ----------

        /// <summary>The lines of By weapon that open, in page order; none on any other page or view.</summary>
        public static List<Block> VisibleWeaponLines(PanelView v)
        {
            var r = new List<Block>();
            if (v == null || v.ShowAbout || v.Active != Chapter.Battle || v.Page != "damage") return r;
            foreach (var b in Content(v)) if (b.Kind == WeaponListKind && b.Items != null) foreach (var i in b.Items) if (i.Kind == WeaponLineKind && i.Opens) r.Add(i);
            return r;
        }

        static string WeaponCursor(PanelState s) => s != null && s.View.TryGetValue(WeaponCursorKey, out var c) ? c : null;

        /// <summary>A/D: the cursor moves to the next line that opens (wraps); the first press puts it on the first. false when there is none.</summary>
        public static bool StepWeapon(PanelState s, PanelView v, int d)
        {
            var rows = VisibleWeaponLines(v);
            if (rows.Count == 0) return false;
            var at = rows.FindIndex(r => r.Id == WeaponCursor(s));
            s.View[WeaponCursorKey] = rows[at >= 0 ? ((at + d) % rows.Count + rows.Count) % rows.Count : 0].Id;
            return true;
        }

        /// <summary>Enter, Space or the pad's A: the cursor's line opens, or shuts when open (no cursor yet: the first line).</summary>
        public static bool ToggleWeaponCursor(PanelState s, PanelView v)
        {
            var rows = VisibleWeaponLines(v);
            if (rows.Count == 0) return false;
            var row = rows.FirstOrDefault(r => r.Id == WeaponCursor(s)) ?? rows[0];
            s.View[WeaponCursorKey] = row.Id;
            ToggleWeapon(s, row.Id);
            return true;
        }

        /// <summary>Build's last step on By weapon with lines that open: the cursor's line is marked (Cursor) and the key line says what A/D and Enter do.</summary>
        static void WeaponsFinish(PanelState state, PanelView view)
        {
            var rows = VisibleWeaponLines(view);
            if (rows.Count == 0) return;
            var cursor = WeaponCursor(state);
            if (!string.IsNullOrEmpty(cursor)) foreach (var r in rows) if (r.Id == cursor) r.Cursor = true;
            if (state.FilterRow >= 0) return;   // the filter focus has the keys (FacetModel.cs)
            var at = view.Keys.IndexOf("[Q/E·A/D] Chapter");
            if (at >= 0) view.Keys[at] = "[Q/E] Chapter";
            var page = view.Keys.IndexOf("[W/S] Page");
            view.Keys.InsertRange(page >= 0 ? page + 1 : view.Keys.Count, new[] { WeaponKeyLine, FoeOpenKeyLine });
        }

        public const string WeaponKeyLine = "[A/D] Weapon";

        /// <summary>Whether a window's weapon lines are answerable on this book at all (a friend's All and a copy from before 0.8.6 are not): the page then says so in one line.</summary>
        public static string WeaponsNotShared(PanelInput input, TimeWindow w) =>
            input == null || input.IsSelf || WeaponCellsIn(input, w) != null ? null :
            Name(input) + "'s copy shows weapons by level for the last 3 hours, Today, 7 days and 30 days.";
    }
}
