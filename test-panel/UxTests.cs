// 0.8 UX polish (work/hearthwoven-0.8, track v08-ux): the matching skill beside its component (Chapters/SkillsBeside.cs) and the growth lines
// from the day history on the heroes (Chapters/GrowthLines.cs). The hover and the Foes keys' UI are Unity-side (only the book's raycaster rank is
// pinned here, rc2); the Foes keys' model is in BattleTests.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class UxTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Page(PanelInput i, Chapter c, string page, Action<PanelState> more = null) { var s = new PanelState { Chapter = c }; if (page != null) s.Page[c] = page; more?.Invoke(s); return PanelModel.Build(i, s); }
        Block Of(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
        string Names(Block b) => string.Join(", ", PanelModel.SkillsOf(b).Select(s => s.Title));

        // ---------- the skill beside its component ----------
        var world = PanelSample.Full(now);
        // 0.8 damage rows (Joost: By weapon, By type and By foe in one grammar): a weapon's row carries no skills of its own; the page's Weapon skills
        // strip, the same on all three views, lists the weapons' skills, and on All each weapon keeps its one "Recorded from ... · this PC" line
        var byWeapon = Page(world, Chapter.Battle, "damage");
        var mixes = PanelModel.Content(byWeapon).Where(b => b.Kind == "dmgmix").ToList();
        var skillStrip = PanelModel.Content(byWeapon).FirstOrDefault(b => b.Kind == "ladders")?.Items?.FirstOrDefault()?.Items?.Select(l => l.Title).ToList() ?? new List<string>();
        var bandsOk = mixes.Count >= 2 && mixes.All(m => PanelModel.SkillsOf(m).Count == 0) && new[] { "Clubs", "Swords", "Spears", "Bows" }.All(skillStrip.Contains);
        var allBands = PanelModel.Content(Page(world, Chapter.Battle, "damage", s => s.Window = TimeWindow.SinceInstall)).Where(b => b.Kind == "dmgmix").ToList();
        var labelsOk = allBands.Count >= 2 && allBands.All(m => (m.RecordedFrom ?? "").StartsWith("Recorded from") && m.Items.All(p => string.IsNullOrEmpty(p.RecordedFrom)));
        var guard = Of(Page(world, Chapter.Battle, "defense", s => s.Window = TimeWindow.SinceInstall), "guard");
        var guardOk = guard != null && Names(guard) == "Blocking";
        // a mod's sailing skill: keyed by the game's number for it, named "Sailing" by the mod; the game itself has none (the world shows none)
        var modded = PanelSample.Full(now);
        modded.SkillLevels = new Dictionary<string, float>(modded.SkillLevels) { ["1294012935"] = 17f };
        { var dn = modded.DisplayName; modded.DisplayName = k => k == "$skill_1294012935" ? "Sailing" : dn?.Invoke(k); }
        var sailHero = Of(Page(modded, Chapter.Voyages, "sailing"), "hero");
        var sailOk = PanelModel.SkillOf(sailHero)?.Title == "Sailing" && PanelModel.SkillOf(sailHero).Value == "17" && PanelModel.SkillOf(Of(Page(world, Chapter.Voyages, "sailing"), "hero")) == null;
        Check(bandsOk && labelsOk && guardOk && sailOk,
              "skills beside (0.8): a weapon's damage row has no skills of its own, the page's Weapon skills strip lists them (" + string.Join(", ", skillStrip) +
              "), each weapon its one \"Recorded from\" line; Defence's blocks the Blocking skill; Sailing a mod's sailing skill found by its name, none without one");

        // ---------- growth lines from the day history ----------
        Block Spark(PanelView v) => PanelModel.SparkOf(Of(v, "hero"));
        double Last(Block sp, int n) => sp.Items.Skip(Math.Max(0, sp.Items.Count - n)).Sum(d => PanelModel.ParseCount(d.Value));
        var adds = new List<string>(); var today = PanelModel.LocalToday(world);
        foreach (var page in PanelModel.GrowthPages)
        {
            var sp = Spark(Page(world, Chapter.Deeds, page));
            var week = PanelModel.ParseCount(Of(Page(world, Chapter.Deeds, page, s => s.Window = TimeWindow.SevenDays), "hero")?.Value);
            var day = PanelModel.ParseCount(Of(Page(world, Chapter.Deeds, page, s => s.Window = TimeWindow.Today), "hero")?.Value);
            if (sp == null || sp.Items.Count < 7 || Last(sp, 7) != week || PanelModel.ParseCount(sp.Items.Last().Value) != day || sp.Items.Last().Id != DayHistory.DayKey(today)) adds.Add(page + (sp == null ? " none" : " " + Last(sp, 7) + " vs " + week));
        }
        var strip = PanelModel.SparkOf(Of(Page(world, Chapter.Battle, null), "biomes"));
        var dealtWeek = PanelModel.ParseCount(Of(Page(world, Chapter.Battle, null, s => s.Window = TimeWindow.SevenDays), "biomes")?.Value);
        if (strip == null || Last(strip, 7) != dealtWeek) adds.Add("battle " + (strip == null ? "none" : Last(strip, 7) + " vs " + dealtWeek));
        Check(adds.Count == 0, "growth lines (0.8): on the " + PanelModel.GrowthPages.Count() + " Deeds pages and Battle's damage dealt, a column per day whose last seven add up to the 7 days window and whose last is Today" + (adds.Count > 0 ? ": " + string.Join(", ", adds) : ""));
        // short history: the days the history holds only, from the counter's first kept day; under two days no line; a fellow's Deeds page greyed
        var young = PanelSample.Full(now); young.History.From = today.AddDays(-2); young.History.Rows.RemoveAll(r => r.Start < young.History.From);
        young.History.Began[DayHistory.PlacedPrefix] = today.AddDays(-1);
        var wood = Spark(Page(young, Chapter.Deeds, "woodcutting")); var built = Spark(Page(young, Chapter.Deeds, "building"));
        var fresh = PanelSample.Full(now); fresh.History.From = today;
        var edda = FullDump.Books(PanelSample.Full(now)).Single(b => b.who == "Edda").book;
        var fellowDeeds = Spark(Page(edda, Chapter.Deeds, "woodcutting"));
        var fellowDealt = PanelModel.SparkOf(Of(Page(edda, Chapter.Battle, null), "biomes"));
        Check(wood != null && wood.Items.Count == 3 && wood.Items[0].Id == DayHistory.DayKey(today.AddDays(-2)) && wood.Title == "since " + PanelModel.ShortDate(today.AddDays(-2), today) &&
              built != null && built.Items.Count == 2 && built.Items[0].Id == DayHistory.DayKey(today.AddDays(-1)) &&
              Spark(Page(fresh, Chapter.Deeds, "woodcutting")) == null &&
              fellowDeeds != null && fellowDeeds.Tone == PanelModel.OffTone && fellowDeeds.Title == PanelModel.NoDaysShared && (fellowDeeds.Items == null || fellowDeeds.Items.Count == 0) &&
              (edda.DealtByDay == null || edda.DealtByDay.Count == 0 ? fellowDealt == null : fellowDealt != null && fellowDealt.Tone == null),
              "growth lines (0.8): only the days the history holds (from the counter's first kept day: pieces from when they were kept), no line under two days, a fellow's Deeds page greyed \"no days shared\", their damage dealt from what they share");
        // ---------- the book key (0.8 integration): [B] and the pad's Y step the player row as its chips' clicks do ----------
        {
            string Step(PanelState st)
            {
                var v = PanelModel.Build(world, st);
                PanelModel.AddPlayers(v, "Rowan", new[] { "Edda", "Tor" }, st.Player, true, joining: new[] { "Ylva" }, bookKey: "B");
                return PanelModel.StepBook(st, v) ? (st.Everyone ? "Everyone" : st.Player == "" ? "You" : st.Player) : "-";
            }
            var woodSt = new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = "woodcutting" } };
            var walk = string.Join(" ", Enumerable.Range(0, 4).Select(_ => Step(woodSt)));   // from you: Edda, Tor, Everyone, you again (Ylva is just joining: skipped)
            var skills = new PanelState { Chapter = Chapter.Skills }; skills.Player = "Tor";
            var noGroup = Step(skills);   // Everyone is greyed on Skills: the key lands on it and opens Deeds' nearest group page, Everyone on (0.8)
            var landed = skills.Chapter + "/" + skills.PageOf(skills.Chapter);
            var line = PanelModel.Build(world, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = "woodcutting" } });
            PanelModel.AddPlayers(line, "Rowan", new[] { "Edda" }, "", true, bookKey: "B");
            var alone = PanelModel.Build(world, new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = "woodcutting" } });
            PanelModel.AddPlayers(alone, "Rowan", new string[0], "", true, bookKey: "B");
            var keys = line.Keys.ToList(); var book = keys.IndexOf("[B] Book");
            var full = PanelModel.KeyLine(keys.Concat(new[] { PanelModel.ScrollKey }), l => l.Length <= string.Join(PanelModel.KeyGap, keys).Length - 1);
            Check(walk == "Edda Tor Everyone You" && noGroup == "Everyone" && landed == "Deeds/cooking" && book >= 0 && keys[book + 1] == "[Backspace] Back" && !alone.Keys.Any(k => k.EndsWith("] Book")) &&
                  !full.Contains("Scroll") && !full.Contains("] Book") && full.Contains("[Backspace] Back"),
                  "book key (0.8): [B] steps you, the fellow players and Everyone (" + walk + "), skipping one just joining; on Everyone greyed it opens the nearest page where it works (" + noGroup + ", " + landed + "); in the key line before Back, left out right after the scroll hint when the line is full, absent with nobody else to go to");
        }
        // ---------- the hover's blind spot (rc1 play-test): the game's HUD canvas, drawn above the book, was asked for the pointer first ----------
        {
            // a canvas's sorting order is a 16-bit number, so a raycaster that reports more than that is asked before every canvas's own
            var type = typeof(PanelUi).Assembly.GetType("Hearthwoven.Panel.PanelRaycaster");
            var rank = type == null ? 0 : (int)type.GetProperty("sortOrderPriority").GetValue(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type));
            var src = AppContext.BaseDirectory;
            while (src != null && !System.IO.File.Exists(System.IO.Path.Combine(src, "src", "Plugin.cs"))) src = System.IO.Path.GetDirectoryName(src);
            var ui = src == null ? "" : System.IO.File.ReadAllText(System.IO.Path.Combine(src, "src", "Panel", "PanelUi.cs"));
            Check(rank > short.MaxValue && ui.Contains("root.AddComponent<PanelRaycaster>();") && !ui.Contains("AddComponent<GraphicRaycaster>()"),
                  "hover (0.8 rc2): the book's canvas takes the pointer before any game canvas drawn above it (the HUD's emptied hover text sat over the plate's centre), so every bar part lights");
        }
        return fails;
    }
}
