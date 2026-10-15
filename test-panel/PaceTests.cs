// 0.8.6 damage per second in three spots (Chapters/PaceModel.cs): each weapon on By weapon, the group's pace on a boss fight, your own pace in a
// fight's head line. Joost's rule: never a live comparison of players, so no DPS on Everyone, in a players' table or in Compare. A fellow's own book shows
// THEIR By weapon (their weapons, upgrade levels, DPS and skill levels: Chapters/WeaponRows.cs), never their fights.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

/// <summary>0.8.6 (DPS per named weapon): the sample world with a long evening of fighting. The sample's own days are short (a level has under 5 s of fighting, so most lines
/// show the dash or fall back to the level before): here every number of damage you dealt is <c>factor</c> times as big and every fighting time as long, the kind rows'
/// sources with the weapon cells (the log and its after, the session, the days, the ledgers, a friend's day share), so a pace and a change mark show and the paces stay
/// as they were. 0.8.6 honest numbers: before, only the weapon cells grew and the lines under a kind row came to five times the row (Iron Sword 6 360 under Melee 4 160).</summary>
static class WeaponScenario
{
    static void Scale(WeaponCells cells, float f)
    {
        foreach (var c in cells.Map.Values)
        {
            c.Sec *= f; c.Swung *= f; c.All *= f;
            for (int i = 0; i < c.PartCount; i++) { c.Parts[i].Sec *= f; c.Parts[i].Swung *= f; }
        }
    }

    public static PanelInput Longer(PanelInput p, float factor = 12f)
    {
        var maps = new HashSet<Dictionary<string, float>>(); var cells = new HashSet<WeaponCells>();
        void Times(Dictionary<string, float> d, Func<string, bool> which = null)
        {
            if (d == null || !maps.Add(d)) return;   // each map once (a tally may stand in two places)
            foreach (var k in d.Keys.ToList()) if (which == null || which(k)) d[k] *= factor;
        }
        bool DealtKey(string k) { var parts = k.Split('|'); return parts.Length >= 6 && parts[2] == "dealt"; }   // time|biome|dir|foe|cause|type
        foreach (var log in new[] { p.Log, p.LogBefore }.Where(l => l != null))
        {
            Times(log.Damage, DealtKey); Times(log.After); Times(log.Swing);
            if (cells.Add(log.Weapons)) Scale(log.Weapons, factor);
        }
        Times(p.Session?.Dealt); Times(p.DamageSinceInstall?.Dealt); Times(p.Pending?.Damage.Dealt);
        if (p.History != null) foreach (var r in p.History.Rows) Times(r.Damage.Dealt);
        if (p.DayDealt != null) foreach (var d in p.DayDealt.Values) Times(d);   // a friend's copy: their days
        if (p.DayWeapons != null) foreach (var w in p.DayWeapons.Values) if (cells.Add(w)) Scale(w, factor);
        var ledgers = new[] { p.DealtArmourSession, p.DealtArmourSince, p.DealtArmourPending }.ToList();
        if (p.DealtArmourBook != null) ledgers.AddRange(new[] { p.DealtArmourBook.Previous, p.DealtArmourBook.Last }.Concat(p.DealtArmourBook.Days.Select(d => d.Tally)));
        foreach (var t in ledgers.Where(t => t != null))
        {
            Times(t.Before); Times(t.After); Times(t.Swing);
            if (cells.Add(t.Weapons)) Scale(t.Weapons, factor);
        }
        return p;
    }
}

static class PaceTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        bool Dps(string s) => s != null && s.Contains("DPS");
        bool AnyDps(PanelView v) => PanelModel.AllText(v).Any(Dps) || PanelModel.ToJson(v).Contains("DPS");
        PanelState Page(string page, TimeWindow w, bool everyone = false, Action<PanelState> more = null)
        {
            var s = new PanelState { Chapter = Chapter.Battle, Window = w, WindowPicked = true, Everyone = everyone, Page = { [Chapter.Battle] = page } };
            more?.Invoke(s); return s;
        }
        IEnumerable<Block> All(Block b) => new[] { b }.Concat((b.Items ?? new List<Block>()).SelectMany(All));
        var world = PanelSample.Full(now);

        // spot 1: each weapon's pace beside its total on your own book, in the windows that have fighting time recorded; a dash under the floor
        var allWeapons = PanelModel.Content(PanelModel.Build(world, Page("damage", TimeWindow.SinceInstall))).Where(b => b.Kind == "dmgmix").ToList();
        var ledger = world.DealtArmourSince;
        var expected = allWeapons.Select(b => SwingClock.Words(SwingClock.Pace(ledger.SwingOf(b.Id).damage, ledger.SwingOf(b.Id).seconds))).ToList();
        var shortRows = PanelModel.Content(PanelModel.Build(world, Page("damage", TimeWindow.LastTenMinutes))).Where(b => b.Kind == "dmgmix").ToList();
        Check(allWeapons.Count == 3 && allWeapons.Select(b => b.Value2).SequenceEqual(expected) && expected.All(e => e.EndsWith(" DPS")) && shortRows.All(b => b.Value2 == null),
              "By weapon on All: each of the three weapons has its pace beside its total (" + string.Join(", ", allWeapons.Select(b => b.Title + " " + b.Value2)) + "), the ledger's damage over its fighting seconds; a short window with under 5 s of fighting no chip at all (no dash)");

        // spots 2 and 3: the boss fight's group pace and what is left at the last minute's pace (live), your own pace in a group fight's head and the Feed
        var live = FightsSample.With(PanelSample.Full(now), true); var after = FightsSample.With(PanelSample.Full(now), false);
        var boss = PanelModel.Build(live, Page(PanelModel.LastFightPage, TimeWindow.Session)); var group = PanelModel.Build(after, Page(PanelModel.LastFightPage, TimeWindow.Session));
        var bossHead = PanelModel.Content(boss).First(b => b.Kind == PanelModel.FightHeadKind); var groupHead = PanelModel.Content(group).First(b => b.Kind == PanelModel.FightHeadKind);
        var lane = bossHead.Items.First(i => i.Kind == "lane");
        var bossNum = bossHead.Items.Where(i => i.Kind == "number").ToList(); var groupNum = groupHead.Items.Where(i => i.Kind == "number").ToList();
        var bossDamage = live.Fights.LiveBoss.Foes.Where(f => f.Boss).Sum(f => f.DealtTotal);
        var feed = PanelModel.Content(PanelModel.Build(world, Page(PanelModel.FeedPage, TimeWindow.Session))).SelectMany(All).Where(b => b.Kind == "fight").ToList();
        Check(bossNum.Count == 3 && bossNum[2].Value + " DPS" == SwingClock.Words(bossDamage / live.Fights.LiveBoss.SwingSeconds) && bossNum[2].Title == "DPS together" && lane.Note == "at this pace about 8:00 left" &&
              groupNum.Count == 3 && groupNum[2].Value == "11" && groupNum[2].Title == "your DPS" && feed.Count > 0 && feed.Any(f => f.Rate != null && f.Rate.EndsWith(" DPS")) && PanelModel.TimeLeftAtPace(0.5, 0.505) == "" && PanelModel.TimeLeftAtPace(0.5, 0.6) == "5:00",
              "the boss fight's head has the group's pace as its third number (" + bossNum[2].Value + " " + bossNum[2].Title + "), the live lane the time left at the last minute's pace (\"" + lane.Note + "\"); the group fight has yours (" + groupNum[2].Value + " " + groupNum[2].Title + "); the Feed's fights have it beside dealt and received (" + feed.Count(f => f.Rate != null) + " of " + feed.Count + ")");

        // never a ranking: Everyone (every page and window), the players' tables of the fight pages, a fellow's own book and the compare rows carry no DPS
        var everyone = new List<string>(); int shown = 0;
        foreach (var page in new[] { "overview", "damage", "defense", "foes", PanelModel.LastFightPage, PanelModel.FeedPage })
            foreach (var w in new[] { TimeWindow.LastTenMinutes, TimeWindow.Session, TimeWindow.Today, TimeWindow.SevenDays, TimeWindow.SinceInstall })
            {
                var v = PanelModel.Build(world, Page(page, w, everyone: true));   // a page with no group view (the Feed, Last fight) stays your own, with your own pace
                if (v.EveryoneOn) { shown++; if (AnyDps(v)) everyone.Add(page + "/" + w); }
            }
        var tables = new List<string>();
        foreach (var book in new[] { live, after })
            foreach (var page in book.Fights.Fights.Where(f => f.Kind != FightKind.Run).Select(f => PanelModel.FightPageOf(f)).Concat(new[] { PanelModel.LastFightPage }))
                foreach (var b in PanelModel.Content(PanelModel.Build(book, Page(page, TimeWindow.Session))).Where(b => b.Kind == PanelModel.FightPlayersKind || b.Kind == PanelModel.FightFoesKind))
                    if (All(b).Any(x => Dps(x.Title) || Dps(x.Value) || Dps(x.Value2) || Dps(x.Text) || Dps(x.Note) || Dps(x.Rate))) tables.Add(page);
        var edda = world.Fellows.First(f => f.PlayerName == "Edda"); var fellow = new List<string>();
        foreach (var page in new[] { PanelModel.LastFightPage, PanelModel.FeedPage })   // a fellow's By weapon has its own pace (checked below); their fights and Feed never do
            foreach (var w in new[] { TimeWindow.Session, TimeWindow.SinceInstall })
                if (AnyDps(PanelModel.Build(edda, Page(page, w)))) fellow.Add(page + "/" + w);
        bool WeaponRows(PanelView v) => PanelModel.Content(v).SelectMany(All).Any(b => b.Kind == PanelModel.WeaponListKind || b.Kind == PanelModel.WeaponLineKind || b.Kind == PanelModel.WeaponSkillKind);
        var weaponsElsewhere = new List<string>();   // the new rows (weapon, upgrade level, DPS, skill level) on Everyone in every window, in a fight's players' table, in Compare
        foreach (var page in new[] { "overview", "damage", "defense", "foes" })
            foreach (var w in new[] { TimeWindow.LastTenMinutes, TimeWindow.Session, TimeWindow.Today, TimeWindow.SevenDays, TimeWindow.ThirtyDays, TimeWindow.SinceInstall })
                if (WeaponRows(PanelModel.Build(world, Page(page, w, everyone: true)))) weaponsElsewhere.Add("everyone " + page + "/" + w);
        foreach (var book in new[] { live, after })
            foreach (var page in book.Fights.Fights.Where(f => f.Kind != FightKind.Run).Select(f => PanelModel.FightPageOf(f)).Concat(new[] { PanelModel.LastFightPage }))
                if (WeaponRows(PanelModel.Build(book, Page(page, TimeWindow.Session)))) weaponsElsewhere.Add("fight " + page);
        foreach (var w in new[] { TimeWindow.SevenDays, TimeWindow.ThirtyDays })
            if (WeaponRows(PanelModel.Build(world, Page("damage", w, more: s => s.Compare = true)))) weaponsElsewhere.Add("compare " + w);
        var compare = PanelModel.Build(world, Page("damage", TimeWindow.SevenDays, more: s => s.Compare = true));
        Check(shown > 10 && everyone.Count == 0 && tables.Count == 0 && fellow.Count == 0 && !AnyDps(compare) && weaponsElsewhere.Count == 0,
              "no DPS where players would be compared: Everyone (" + shown + " pages in every window), the players' tables of " + (live.Fights.Fights.Count + after.Fights.Fights.Count) + " fight pages' rows, Edda's own book, Compare" +
              (everyone.Count + tables.Count + fellow.Count + weaponsElsewhere.Count > 0 ? " - FOUND on " + string.Join(", ", everyone.Concat(tables).Concat(fellow).Concat(weaponsElsewhere)) : "") + "; no weapon rows (weapon, level, DPS, skill) there either");

        // under the floor: a Feed fight with less than SwingClock.Floor seconds of your fighting has no pace in its head, not even a dash (the dash stays on By weapon and the fight pages)
        var under = PanelSample.Full(now);
        foreach (var f in under.Battle.Fights()) f.SwingSeconds = 2;
        var floorFights = PanelModel.Content(PanelModel.Build(under, Page(PanelModel.FeedPage, TimeWindow.Session))).SelectMany(All).Where(b => b.Kind == "fight").ToList();
        Check(floorFights.Count > 0 && floorFights.All(f => f.Rate == null && !Dps(f.Title) && !Dps(f.Text) && !Dps(f.Value) && !Dps(f.Value2) && !Dps(f.Note)),
              "a Feed fight under the floor has no DPS in its head (" + floorFights.Count + " fights, dealt and received only)");

        // ---------- per named weapon: under each kind a line per weapon and per upgrade level; its most recent pace, a change mark, skill levels that open; a friend's book shows THEIR rows the same way ----------
        // 0.8.6 fold: a weapon of several upgrade levels is one line until opened; open holds the Ids of the open lines (";" between)
        List<Block> Lists(PanelInput p, TimeWindow w, string open = null) => PanelModel.Content(PanelModel.Build(p, Page("damage", w, more: open == null ? null : (Action<PanelState>)(s => s.View[PanelModel.WeaponOpenKey] = open)))).Where(b => b.Kind == PanelModel.WeaponListKind).ToList();
        // the rule worked out again from a cell's skill levels: the pace at the highest level with at least 5 s of fighting ("at 41" when that is under the highest level), the change since the
        // lowest such level (whole DPS, rounded as shown), else the window's own pace with the dash rule
        (string pace, string change, string skill) Expect(WeaponCell c, string skill)
        {
            var reach = c.SortedParts().Where(x => x.Sec >= 5 && x.Swung > 0).ToList(); string pace, change = null;
            if (reach.Count == 0) pace = c.Sec >= 5 && c.Swung > 0 ? SwingClock.Words(c.Swung / c.Sec) : "";   // no DPS yet: nothing, never a dash
            else
            {
                var top = reach.Last(); var low = reach.First(); pace = SwingClock.Words(top.Swung / top.Sec) + (top.Level < c.Hi ? " at " + top.Level : "");
                var d = Math.Round(top.Swung / top.Sec) - Math.Round(low.Swung / low.Sec);
                if (low.Level != top.Level && Math.Abs(d) >= 1) change = (d > 0 ? "\u25B2 " : "\u25BC ") + Math.Abs(d);
            }
            return (pace, change, skill + " " + c.Lo + (c.Lo == c.Hi ? "" : "\u2013" + c.Hi));
        }
        bool LineIs(Block line, WeaponCell c, string skill, int quality)
        {
            var e = Expect(c, skill);
            return line != null && line.Change == e.change && line.Note == e.skill && line.Value2 == (e.pace.Length > 0 ? e.pace : null) && line.Text == (quality > 0 ? "level " + quality + (e.pace.Length > 0 ? " \u00B7 " : "") : "") + e.pace;
        }
        // a folded weapon (several upgrade levels): its newest level, that level's most recent pace "at" its skill level, the level's change mark, "N levels"
        bool FoldIs(Block line, WeaponCell newest, int quality, int levels)
        {
            var e = Expect(newest, ""); var reach = newest.SortedParts().Where(x => x.Sec >= 5 && x.Swung > 0).ToList();
            var pace = reach.Count > 0 ? SwingClock.Words(reach.Last().Swung / reach.Last().Sec) + " at " + reach.Last().Level : e.pace;
            return line != null && line.Opens && line.Tone == null && line.Note == levels + " levels" && line.Change == e.change && line.Value2 == (pace.Length > 0 ? pace : null) &&
                   line.Text == "level " + quality + (pace.Length > 0 ? " \u00B7 " + pace : "");
        }
        // every line that opens opened, folded weapons first (their level lines then open too)
        string OpenAll(PanelInput p, TimeWindow w)
        {
            var open = new HashSet<string>();
            for (int k = 0; k < 3; k++) foreach (var i in Lists(p, w, string.Join(";", open)).SelectMany(l => l.Items).Where(i => i.Opens)) open.Add(i.Id);
            return string.Join(";", open.OrderBy(x => x, StringComparer.Ordinal));
        }
        var lists = Lists(world, TimeWindow.Today); var cells = PanelModel.WeaponCellsIn(world, TimeWindow.Today);
        var folded = lists.FirstOrDefault(l => l.Id == "melee")?.Items ?? new List<Block>(); var ironFolded = folded.FirstOrDefault(l => l.Title == "Iron Sword");
        var melee = Lists(world, TimeWindow.Today, "SwordIron|Swords").FirstOrDefault(l => l.Id == "melee")?.Items ?? new List<Block>();
        var iron = melee.FindIndex(l => l.Title == "Iron Sword");   // two upgrade levels in the sample: folded one line; open, the weapon then a line for each level
        var level1 = iron >= 0 && iron + 1 < melee.Count ? melee[iron + 1] : null; var level2 = iron >= 0 && iron + 2 < melee.Count ? melee[iron + 2] : null;
        WeaponCell s1 = cells.Map["SwordIron|1|Swords"], s2 = cells.Map["SwordIron|2|Swords"], frost = cells.Map["MaceSilver|3|Clubs"];
        var frostner = melee.FirstOrDefault(l => l.Title == "Frostner");
        Check(lists.Count >= 2 && iron >= 0 && level1?.Title == "level 1" && level2?.Title == "level 2" && level2.Tone == PanelModel.WeaponLevelTone && melee[iron].Tone == null && melee[iron].Opens && melee[iron].Selected &&
              FoldIs(ironFolded, s2, 2, 2) && !ironFolded.Selected && !folded.Any(l => l.Tone == PanelModel.WeaponLevelTone) && ironFolded.Value == melee[iron].Value &&
              LineIs(level1, s1, "Swords", 0) && LineIs(level2, s2, "Swords", 0) && Math.Abs(PanelModel.ParseCount(melee[iron].Value) - PanelModel.ParseCount(level1.Value) - PanelModel.ParseCount(level2.Value)) <= 1 &&
              LineIs(frostner, frost, "Clubs", 3) && PanelModel.AllText(PanelModel.Build(world, Page("damage", TimeWindow.Today))).Any(t => t.Contains("upgrade level")),
              "your By weapon in Today: under Melee the Iron Sword folded to one line at its newest level (\"" + ironFolded?.Text + " · " + ironFolded?.Note + "\"), open a line for each of its two upgrade levels (\"" + level2?.Text + " · " + level2?.Note + "\"), a weapon with one level on one line (\"" + frostner?.Text + " · " + frostner?.Note + "\"), nothing where a day has under 5 s, and the About box says what is counted");

        // a long evening (the sample's weapons 12 times as long): the most recent pace, the fallback to the level before, the change mark and the skill levels that open
        var longer = WeaponScenario.Longer(PanelSample.Full(now)); var longCells = PanelModel.WeaponCellsIn(longer, TimeWindow.SevenDays);
        var foldedLists = Lists(longer, TimeWindow.SevenDays); var openAll = OpenAll(longer, TimeWindow.SevenDays);
        var longLists = Lists(longer, TimeWindow.SevenDays, openAll); var all7 = longLists.SelectMany(l => l.Items).Where(i => i.Kind == PanelModel.WeaponLineKind).ToList();
        var spear = all7.First(l => l.Id == "SpearBronze|Spears"); var spearCell = longCells.Map["SpearBronze|2|Spears"];
        var fallback = all7.First(l => l.Id == "SwordIron|Swords|1"); var fallbackCell = longCells.Map["SwordIron|1|Swords"];
        var checkedLines = 0; var wrong = new List<string>();
        foreach (var l in foldedLists) foreach (var i in l.Items.Where(i => i.Note != null && i.Note.EndsWith(" levels")))   // every folded weapon: its newest level
        {
            var key = i.Id.Split('|'); var qs = longCells.Map.Keys.Where(k => k.StartsWith(key[0] + "|") && k.EndsWith("|" + key[1])).Select(k => int.Parse(k.Split('|')[1])).OrderBy(q => q).ToList();
            if (!FoldIs(i, longCells.Map[key[0] + "|" + qs.Last() + "|" + key[1]], qs.Last(), qs.Count)) wrong.Add(i.Id); checkedLines++;
        }
        foreach (var l in longLists) foreach (var i in l.Items.Where(i => i.Kind == PanelModel.WeaponLineKind && i.Note != null && !i.Note.EndsWith(" levels")))   // every line with a skill range: weapon + level, single or under a weapon
        {
            var key = i.Id.Split('|'); var q = key.Length > 2 ? int.Parse(key[2]) : longCells.Map.Keys.Where(k => k.StartsWith(key[0] + "|") && k.EndsWith("|" + key[1])).Select(k => int.Parse(k.Split('|')[1])).First();
            if (!LineIs(i, longCells.Map[key[0] + "|" + q + "|" + key[1]], PanelModel.SkillTitle(longer, key[1]), key.Length > 2 ? 0 : q)) wrong.Add(i.Id); checkedLines++;
        }
        var open = PanelModel.Content(PanelModel.Build(longer, Page("damage", TimeWindow.SevenDays, more: s => s.View[PanelModel.WeaponOpenKey] = "SpearBronze|Spears"))).Where(b => b.Kind == PanelModel.WeaponListKind).SelectMany(l => l.Items).ToList();
        var openAt = open.FindIndex(i => i.Id == "SpearBronze|Spears"); var skillRows = open.Skip(openAt + 1).TakeWhile(i => i.Kind == PanelModel.WeaponSkillKind).ToList();
        var spearParts = spearCell.SortedParts();
        double PartDps(LevelPart x) => x.Sec >= 5 && x.Swung > 0 ? x.Swung / x.Sec : 0; var top = spearParts.Select(PartDps).Max();
        var closedAt = Lists(longer, TimeWindow.SevenDays).SelectMany(l => l.Items).ToList();
        var toggled = new PanelState(); PanelModel.Follow(toggled, PanelModel.WeaponOpenLink(spear)); var afterOpen = PanelModel.WeaponOpen(toggled).SetEquals(new[] { "SpearBronze|Spears" });
        PanelModel.Follow(toggled, PanelModel.WeaponOpenLink(fallback)); var both = PanelModel.WeaponOpen(toggled).Count == 2;   // each line opens on its own
        PanelModel.Follow(toggled, PanelModel.WeaponOpenLink(spear)); var shut = PanelModel.WeaponOpen(toggled).SetEquals(new[] { fallback.Id });
        Check(wrong.Count == 0 && checkedLines >= 10 && spear.Change != null && spear.Change.StartsWith("\u25B2 ") && fallback.Value2.Contains(" at ") && fallbackCell.SortedParts().Last().Sec < 5 &&
              spear.Opens && spear.Selected && !closedAt.First(l => l.Id == "SpearBronze|Spears").Selected && !all7.First(l => l.Id == "SwordIron|Swords|2").Opens && !all7.First(l => l.Id == "KnifeFlint|Knives").Opens &&
              openAt >= 0 && open[openAt].Selected && skillRows.Count == spearParts.Length && skillRows.Select(r => r.Title).SequenceEqual(spearParts.Select(x => "Spears " + x.Level)) &&
              skillRows.Select(r => r.Fraction).Zip(spearParts, (f, x) => Math.Abs(f - (top > 0 ? PartDps(x) / top : 0)) < 0.001).All(b => b) && skillRows.Any(r => Math.Abs(r.Fraction - 1) < 0.001) &&
              skillRows.Zip(spearParts, (r, x) => r.Value2 == (x.Sec >= 5 ? SwingClock.Words(x.Swung / x.Sec) : "")).All(b => b) && !closedAt.Any(i => i.Kind == PanelModel.WeaponSkillKind) &&
              afterOpen && both && shut,
              "a long evening: the line's pace is the most recent one (the level before when the newest is under 5 s: \"" + fallback.Value2 + "\"), a change mark against the lowest level that reached the floor (Bronze Spear \"" + spear.Change + "\"), " + checkedLines +
              " lines equal the rule worked out again (folded weapons at their newest level); a line with several skill levels opens (the Spear's " + skillRows.Count + " levels, a bar each on one scale, empty under the floor) and shuts again, each line on its own, one level opens nothing" + (wrong.Count > 0 ? " - WRONG: " + string.Join(", ", wrong) : ""));

        var (owns, copies) = SampleWorld.Make(now);
        string Rows(PanelInput p, TimeWindow w, string open = null) => string.Join("\n", PanelModel.Content(PanelModel.Build(p, Page("damage", w, more: open == null ? null : (Action<PanelState>)(s => s.View[PanelModel.WeaponOpenKey] = open)))).Where(b => b.Kind == PanelModel.WeaponListKind)
                                                    .SelectMany(l => l.Items.Select(i => l.Id + "|" + i.Kind + "|" + i.Id + "|" + i.Title + "|" + i.Value + "|" + i.Value2 + "|" + i.Note + "|" + i.Change + "|" + i.Opens + "|" + i.Selected + "|" + i.Fraction.ToString("0.###") + "|" + i.Text)));
        var same = new List<string>(); var skipped = new List<string>(); int compared = 0, openCompared = 0;
        void CompareFriends(Dictionary<string, PanelInput> theirOwns, Dictionary<string, PanelInput> theirCopies, string tag)
        {
            foreach (var who in new[] { "Edda", "Tor", "Finch" })
                foreach (var w in new[] { TimeWindow.LastTenMinutes, TimeWindow.LastHour, TimeWindow.Today, TimeWindow.SevenDays, TimeWindow.ThirtyDays })
                {
                    if (PanelModel.Build(theirCopies[who], Page("damage", w)).ShownWindow != w) { skipped.Add(who + "/" + w); continue; }   // a window their book does not offer (Tor played before you came in)
                    var theirs = Rows(theirCopies[who], w); var own = Rows(theirOwns[who], w); compared++;
                    if (theirs.Length == 0 || theirs != own) same.Add(tag + who + "/" + w + (theirs.Length == 0 ? " (none)" : ""));
                    var all = OpenAll(theirOwns[who], w);   // and with every line open (folded weapons and skill levels)
                    if (all.Length > 0) { openCompared++; if (Rows(theirCopies[who], w, all) != Rows(theirOwns[who], w, all) || Rows(theirCopies[who], w, all).Split('\n').Length <= own.Split('\n').Length) same.Add(tag + who + "/" + w + " open " + all); }
                }
        }
        CompareFriends(owns, copies, "");
        var longOwns = owns.ToDictionary(kv => kv.Key, kv => WeaponScenario.Longer(kv.Value)); var longCopies = copies.ToDictionary(kv => kv.Key, kv => WeaponScenario.Longer(kv.Value));   // each copy as read, its weapons as long
        CompareFriends(longOwns, longCopies, "long ");
        var tor10 = Lists(copies["Tor"], TimeWindow.LastTenMinutes); var torAll = PanelModel.Build(copies["Tor"], Page("damage", TimeWindow.SinceInstall));
        var olderTor = SampleWorld.Share(owns["Tor"], now, "0.8.3"); var olderToday = PanelModel.Build(olderTor, Page("damage", TimeWindow.Today));
        var theirKinds = PanelModel.Content(PanelModel.Build(copies["Tor"], Page("damage", TimeWindow.Today))).Where(b => b.Kind == "dmgmix").ToList();
        var marks = longCopies.Values.Select(c => PanelModel.Content(PanelModel.Build(c, Page("damage", TimeWindow.SevenDays))).SelectMany(b => b.Items ?? new List<Block>()).Count(i => i.Change != null)).Sum();
        Check(same.Count == 0 && compared >= 22 && openCompared >= 12 && marks > 0 && skipped.All(x => x.StartsWith("Tor/Last")) && tor10.Count > 0 && !PanelModel.Content(torAll).Any(b => b.Kind == PanelModel.WeaponListKind) && PanelModel.AllText(torAll).Any(t => t.Contains("copy shows weapons by level")) &&
              !PanelModel.Content(olderToday).Any(b => b.Kind == PanelModel.WeaponListKind) && PanelModel.AllText(olderToday).Any(t => t.Contains("copy shows weapons by level")) &&
              theirKinds.Count > 0 && theirKinds.All(k => k.Value2 != null && k.Value2.EndsWith("DPS")),
              "a friend's By weapon shows their weapons, levels, most recent DPS, change marks and skill levels as their own book does, closed and with a line open, in 10 min, 1 h, Today, 7 days and 30 days (" + compared + " windows of three friends compared, " + openCompared + " with a line open, every row equal, on the sample and on a long evening with " + marks + " change marks" +
              (skipped.Count > 0 ? "; " + string.Join(", ", skipped) + " is not a window of their book" : "") + ")" + (same.Count > 0 ? " - DIFFERENT in " + string.Join(", ", same) : "") + "; their All and a copy from before 0.8.6 show none and say so in one line, and their kind rows keep their pace from the same cells");

        // 0.8.6 fold (Joost in game, All: "Mistwalker 1 711 · 144 DPS", under it always "level 1 195 · – DPS · Swords 35" and "level 3 1 516 · 149 DPS · Swords 35"): a weapon used
        // at two upgrade levels is ONE line at its newest level with that level's DPS at its skill level, "2 levels" and the › (it opens); pressed, its two levels; no dash anywhere
        var mist = PanelSample.Full(now); var mistCells = (PanelModel.DealtLedgerIn(mist, TimeWindow.SinceInstall)).Weapons;
        var lv1 = new WeaponCell(); lv1.Add(35, 3f, 190f, 195f, WeaponCells.MergeLevels);   // 3 s of fighting: under the floor, no DPS yet
        var lv3 = new WeaponCell(); lv3.Add(35, 10f, 1490f, 1516f, WeaponCells.MergeLevels);   // 149 DPS at Swords 35
        mistCells.Map[WeaponKey.Make("SwordMistwalker", 1, "Swords")] = lv1; mistCells.Map[WeaponKey.Make("SwordMistwalker", 3, "Swords")] = lv3;
        var mistShut = Lists(mist, TimeWindow.SinceInstall).First(l => l.Id == "melee").Items; var mw = mistShut.FindIndex(i => i.Id == "SwordMistwalker|Swords");
        var mistOpen = Lists(mist, TimeWindow.SinceInstall, "SwordMistwalker|Swords").First(l => l.Id == "melee").Items; var mo = mistOpen.FindIndex(i => i.Id == "SwordMistwalker|Swords");
        var mwLevels = mo >= 0 ? mistOpen.Skip(mo + 1).TakeWhile(i => i.Tone == PanelModel.WeaponLevelTone).ToList() : new List<Block>();
        Check(mw >= 0 && mistShut[mw].Text == "level 3 \u00B7 149 DPS at 35" && mistShut[mw].Note == "2 levels" && mistShut[mw].Opens && !mistShut[mw].Selected && PanelModel.ParseCount(mistShut[mw].Value) == 1711 &&
              (mw + 1 >= mistShut.Count || mistShut[mw + 1].Tone != PanelModel.WeaponLevelTone) && mo >= 0 && mistOpen[mo].Selected && mwLevels.Count == 2 &&
              mwLevels[0].Title == "level 1" && mwLevels[0].Text == "" && mwLevels[0].Value2 == null && mwLevels[0].Note == "Swords 35" && mwLevels[1].Title == "level 3" && mwLevels[1].Text == "149 DPS" && mwLevels[1].Note == "Swords 35",
              "a weapon with two upgrade levels is one line (\"Mistwalker " + (mw >= 0 ? mistShut[mw].Value + " · " + mistShut[mw].Text + " · " + mistShut[mw].Note : "missing") + "\", the ›) and opens to its two levels (\"" +
              string.Join("\", \"", mwLevels.Select(l => l.Title + " " + l.Value + " · " + (l.Text.Length > 0 ? l.Text + " · " : "") + l.Note)) + "\"); the level with under 5 s of fighting shows no DPS, not a dash");

        // no "- DPS" anywhere on By weapon: the kind rows' chips, the weapon lines folded and every line open, the skill levels, in every window of your book, the long evening's and a friend's
        var dashes = new List<string>(); int swept = 0;
        foreach (var (who, book) in new[] { ("you", world), ("long", longer), ("mist", mist), ("Edda", copies["Edda"]), ("Edda long", longCopies["Edda"]) })
            foreach (var w in new[] { TimeWindow.LastTenMinutes, TimeWindow.LastHour, TimeWindow.Session, TimeWindow.Today, TimeWindow.SevenDays, TimeWindow.ThirtyDays, TimeWindow.SinceInstall })
                foreach (var openIds in new[] { null, OpenAll(book, w) })
                {
                    var v = PanelModel.Build(book, Page("damage", w, more: openIds == null ? null : (Action<PanelState>)(s => s.View[PanelModel.WeaponOpenKey] = openIds)));
                    if (!PanelModel.Content(v).Any(b => b.Kind == PanelModel.WeaponListKind)) continue;
                    swept++;
                    if (PanelModel.AllText(v).Any(t => t.Contains(PanelModel.DpsDash) || t.Contains("\u2013 DPS") || t == "\u2013")) dashes.Add(who + "/" + w + (openIds == null ? "" : " open"));
                }
        Check(swept >= 20 && dashes.Count == 0, "no dash DPS on By weapon: " + swept + " pages (yours, a long evening, Edda's copy; folded and every line open) say nothing where a weapon has no DPS yet" + (dashes.Count > 0 ? " - DASH on " + string.Join(", ", dashes) : ""));

        // the keys, as on Foes: A/D move a cursor over the lines that open (the focus ring on it), Enter opens or shuts the cursor's line, the key line says so; another page or book folds them all
        var ks = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.SinceInstall, WindowPicked = true, Page = { [Chapter.Battle] = "damage" } };
        var kv0 = PanelModel.Build(mist, ks); var firstOpens = PanelModel.VisibleWeaponLines(kv0);
        var stepped = PanelModel.StepWeapon(ks, kv0, 1); var kv1 = PanelModel.Build(mist, ks);
        var cursorLine = PanelModel.VisibleWeaponLines(kv1).FirstOrDefault(i => i.Cursor);
        PanelModel.ToggleWeaponCursor(ks, kv1); var kv2 = PanelModel.Build(mist, ks); var opened = PanelModel.VisibleWeaponLines(kv2).FirstOrDefault(i => i.Cursor);
        var kept = PanelModel.WeaponOpen(ks).Count == 1;
        PanelModel.Visited(ks, Chapter.Battle, "damage"); PanelModel.Visited(ks, Chapter.Battle, "foes"); var foldedAway = PanelModel.WeaponOpen(ks).Count == 0;
        ks.Page[Chapter.Battle] = "damage"; PanelModel.ToggleWeapon(ks, firstOpens.Count > 0 ? firstOpens[0].Id : "x"); PanelModel.Build(mist, ks);
        ks.Window = TimeWindow.Session; PanelModel.Build(mist, ks); var foldedWindow = PanelModel.WeaponOpen(ks).Count == 0;
        Check(firstOpens.Count >= 2 && stepped && cursorLine != null && cursorLine.Id == firstOpens[0].Id && opened != null && opened.Selected && kept && foldedAway && foldedWindow &&
              kv1.Keys.Contains(PanelModel.WeaponKeyLine) && kv1.Keys.Contains(PanelModel.FoeOpenKeyLine) && kv1.Keys.Contains("[Q/E] Chapter") && PanelModel.ToJson(kv1).Contains("\"cursor\":1"),
              "By weapon by keys: A/D put the cursor on the first of " + firstOpens.Count + " lines that open (" + cursorLine?.Title + "), Enter opens it, the key line says \"" + PanelModel.WeaponKeyLine + "\" and \"" + PanelModel.FoeOpenKeyLine + "\"; another page and another window fold every weapon again");
        return fails;
    }
}
