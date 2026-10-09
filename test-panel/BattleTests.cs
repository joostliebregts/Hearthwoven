// Tests for the Battle chapter pages (src/Panel/Chapters/BattleModel.cs): Damage (by type, by weapon), Foes (by foe, by
// damage type), Defense, Deaths. A fictional evening close to the approved prototypes (proto/r4battle*.png,
// r2-voyages-deaths.png). The foes' modifiers and the arrows here are a small SAMPLE standing in for the game's own data
// (in game BattleGame reads Character.m_damageModifiers, the trophy drop and every arrow from the prefabs at runtime).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static partial class Program
{
    static HitData.DamageTypes DT(params (string type, float v)[] parts)
    {
        var d = new HitData.DamageTypes();
        foreach (var (t, v) in parts)
            switch (t)
            {
                case "blunt": d.m_blunt += v; break; case "slash": d.m_slash += v; break; case "pierce": d.m_pierce += v; break;
                case "fire": d.m_fire += v; break; case "frost": d.m_frost += v; break; case "lightning": d.m_lightning += v; break;
                case "poison": d.m_poison += v; break; case "spirit": d.m_spirit += v; break; case "chop": d.m_chop += v; break;
            }
        return d;
    }

    static readonly Dictionary<string, string> BattleNames = PanelSample.BattleNames;   // the Battle sample lives in src/Panel/PanelSample.cs

    internal static PanelModel.FoeData SampleFoe(string prefab) => PanelSample.SampleFoe(prefab);

    internal static IList<PanelModel.ArrowData> SampleArrows() => PanelSample.SampleArrows();

    /// <summary>A Battle evening close to the prototypes: melee, bow and magic across the eight types, six sources of hurt,
    /// three falls today (14:31 Black Forest, 15:02 and 15:40 Swamp, local time), 312 blocks of which 58 parries.</summary>
    internal static PanelInput BattleSample() => PanelSample.Battle();

    internal static void BattleChecks(PanelInput plain, Action<bool, string> Check)
    {
        var rich = BattleSample();
        PanelView Page(PanelInput i, string page, Action<PanelState> more = null)
        {
            var st = new PanelState { Chapter = Chapter.Battle }; st.Page[Chapter.Battle] = page; more?.Invoke(st);
            return PanelModel.Build(i, st);
        }
        // Foes and Defence on All (HISTORY-06: they have the one window set now, and the default window is This session): the checks below are about since install
        PanelView PageAll(PanelInput i, string page, Action<PanelState> more = null) => Page(i, page, st => { st.Window = TimeWindow.SinceInstall; more?.Invoke(st); });
        Block Of(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
        // the layered lifetime line by its title (zones: a page's own numbered heading also leads its zone as a hero)
        // (the foes defeated lead the Foes line, the hits follow it as its numbers)
        Block Life(PanelView v, string title) => PanelModel.Content(v).SelectMany(b => new[] { b }.Concat(b.Items ?? new List<Block>())).FirstOrDefault(b => (b.Kind == "hero" || b.Kind == "number") && b.Title == title && (b.Faded != null || b.Src == "character" || b.Items?.Any(i => i.Title == "hits on other players") == true));
        bool Near(double a, double b) => Math.Abs(a - b) < 1e-3;

        // ---------- the small rules ----------
        Check(PanelModel.WeaponOf("Bows") == "bow" && PanelModel.WeaponOf("Crossbows") == "bow" && PanelModel.WeaponOf("ElementalMagic") == "magic" && PanelModel.WeaponOf("BloodMagic") == "magic" &&
              PanelModel.WeaponOf("Swords") == "melee" && PanelModel.WeaponOf("Unarmed") == "melee" && PanelModel.WeaponOf("None") == "melee",
              "B weapon kind from the game's skill: bows and crossbows, the staffs' magic, everything else melee");
        Check(PanelModel.Relation("VeryWeak") == PanelModel.Weakness && PanelModel.Relation("SlightlyResistant") == PanelModel.Resistance && PanelModel.Relation("Ignore") == PanelModel.Immunity &&
              PanelModel.Relation("Normal") == null && PanelModel.Multiplier("Weak") == 1.5f && PanelModel.Multiplier("Immune") == 0f && PanelModel.Multiplier("Normal") == 1f,
              "B the game's modifiers: weakness, resistance, immunity; normal is none (its damage factors as the game applies them)");
        var arrows = SampleArrows();
        Check(PanelModel.BestArrow(SampleFoe("Troll"), arrows.Take(4).ToList())?.Prefab == "ArrowNeedle" && PanelModel.BestArrow(SampleFoe("Greydwarf"), arrows.Take(4).ToList())?.Prefab == "ArrowFrost",
              "B best arrow: the game's arrow damage after the foe's modifiers (Troll: needle 93 over frost 91; Greydwarf: frost)");
        Check(PanelModel.BestArrow(SampleFoe("Troll"), arrows.Take(1).ToList()) == null && PanelModel.BestArrow(null, arrows) == null &&
              PanelModel.BestArrow(SampleFoe("Boar"), new List<PanelModel.ArrowData> { arrows[0], new PanelModel.ArrowData { Prefab = "Twin", Token = "$twin", Damage = { ["pierce"] = 22 } } }) == null,
              "B best arrow: none with fewer than two kinds, an unknown foe or a tie");
        Check(PanelModel.KnownArrows(rich).Select(a => a.Prefab).SequenceEqual(new[] { "ArrowWood", "ArrowFire", "ArrowFrost", "ArrowNeedle", "ArrowPoison" }),
              "B a fellow's copy (no recipes): only arrows made or brought in count (silver never held: left out)");
        var knows = Program.BattleSample(); knows.RecipeKnown = t => PanelSample.SampleRecipes.Contains(t);
        Check(PanelModel.KnownArrows(knows).Any(a => a.Prefab == "ArrowCarapace") && !PanelModel.KnownArrows(knows).Any(a => a.Prefab == "ArrowSilver") &&
              PanelModel.BestArrow(SampleFoe("Troll"), PanelModel.KnownArrows(knows))?.Prefab == "ArrowCarapace",
              "B best arrow: among every arrow whose recipe your character knows, not only those made (Troll: Carapace over Needle)");

        // ---------- Damage ----------
        var dmg = Page(rich, "damage");
        var sw = Of(dmg, "switch"); var grid = Of(Page(rich, "damage", s => s.View["Battle/damage/view"] = "type"), "damagegrid");
        Check(PanelModel.PlateOf(dmg) != null && sw != null && sw.Id == "Battle/damage/view" && sw.Items.Select(v => v.Id + (v.Selected ? "*" : "")).SequenceEqual(new[] { "weapon*", "type" }) &&
              sw.Title == "damage dealt, before the foe's armour" && sw.Items.Select(v => v.Title).SequenceEqual(new[] { "By weapon", "By type" }) && Of(dmg, "dmgmix") != null && Of(dmg, "damagegrid") == null,
              "B damage: on the plate, the shared view switch By weapon (default, Joost 2026-10-09) / By type, its caption what the numbers are");
        var weapons = grid.Items[0].Items; var types = grid.Items.Skip(1).ToList();
        var dealt = PanelModel.DealtRows(PanelModel.Damage(rich.Log, TimeWindow.Session, "", rich.NowUtc));
        Check(weapons.Select(w => w.Title + "=" + w.Value).SequenceEqual(new[] { "Melee=1\u00A0630", "Bow=360", "Magic=290" }) && grid.Value == "2\u00A0280" && grid.Title == "All types" && grid.Items[0].Title == "Total",
              "B by type: weapon columns with their totals, all types together (" + grid.Value + ")");
        Check(types.Select(x => x.Id).SequenceEqual(new[] { "blunt", "slash", "pierce", "fire", "frost", "lightning", "poison", "spirit" }) && types.All(x => x.Icon == "vocab:dmg-" + x.Id) &&
              types.Single(x => x.Id == "slash").Colour == "#aab6c2" && types.Single(x => x.Id == "pierce").Items.Select(c => c.Value).SequenceEqual(new[] { "330", "190", "" }),
              "B by type: the eight types in the palette's order with Codex's icons and colours; an empty cell has no number");
        Check(types.SelectMany(x => x.Items).Max(c => c.Fraction) == 1f && Near(types.Single(x => x.Id == "slash").Items[0].Fraction, 630.0 / 630) && Near(types.Single(x => x.Id == "fire").Items[2].Fraction, 120.0 / 630),
              "B by type: one bar scale for the whole grid, the largest cell full");
        Check(!dealt.Any(r => r.Type == "chop") && PanelModel.AllText(dmg).All(s => !s.Contains("Chop")), "B dealt: tool damage (chop) never in Battle");
        var byWeapon = Page(rich, "damage", s => s.View["Battle/damage/view"] = "weapon");
        var mixes = PanelModel.Content(byWeapon).Where(b => b.Kind == "dmgmix").ToList();
        Check(mixes.Select(m => m.Title + "=" + m.Value).SequenceEqual(new[] { "Melee=1\u00A0630", "Bow=360", "Magic=290" }) && mixes.All(m => Near(m.Items.Sum(p => p.Fraction), 1)) &&
              mixes[2].Items.Select(p => p.Id).SequenceEqual(new[] { "fire", "frost", "lightning", "spirit" }) && mixes[0].Icon == "vocab:weapon-melee" && Of(byWeapon, "damagegrid") == null,
              "B by weapon: one composition bar per weapon kind, parts in type order summing to the whole, only the chosen view's blocks");
        Check(Page(plain, "damage", s => PanelModel.ToggleFacet(s, PanelModel.BattleDamageFilter, "biome", "Swamp")).Heading == "Damage dealt" && Of(Page(plain, "damage", s => { PanelModel.ToggleFacet(s, PanelModel.BattleDamageFilter, "biome", "Swamp"); s.View["Battle/damage/view"] = "type"; }), "damagegrid").Value == "434" && Page(plain, "damage").HasFilters,
              "B damage keeps the window choices and narrows by the Biome row (Swamp: 434 dealt)");

        // ---------- Foes ----------
        var foes = PageAll(rich, "foes");
        var table = Of(foes, "foetable");
        Check(foes.Heading == "Foes" && PanelModel.Content(foes).Any(b => b.Kind == "hero" && b.Value == "214" && b.Title == "foes defeated") && foes.HasFilters && Of(foes, "switch").Items.Select(v => v.Title).SequenceEqual(new[] { "By foe", "By damage type" }),
              "B foes on All: the lifetime count in the heading, the window chips (HISTORY-06), the view switch By foe (default) / By damage type");
        Check(table.Items.Select(r => r.Title + "=" + r.Value).SequenceEqual(new[] { "Troll=520", "Greydwarf=450", "Draugr=440", "Greydwarf Brute=270", "Boar=220", "Neck=190", "Blob=160", "Leech=30" }) &&
              table.Items[0].Fraction == 1f && table.Items[0].Icon == "item:TrophyFrostTroll",
              "B by foe: one row per foe struck, most dealt first, its trophy from the game data");
        var troll = table.Items[0].Items;
        Check(troll.First().Kind == "arrow" && troll.First().Title == "Needle Arrow" && troll.First().Icon == "item:ArrowNeedle" &&
              troll.Where(c => c.Kind == "mod").Select(c => c.Id + ":" + c.Tone).SequenceEqual(new[] { "blunt:resistance", "slash:", "pierce:weakness", "fire:", "frost:", "lightning:", "poison:", "spirit:immunity" }),
              "B by foe: the best arrow you have, and the eight cells: weakness, resistance, immunity, normal");
        var byType = PageAll(rich, "foes", s => s.View["Battle/foes/view"] = "type");
        var ft = Of(byType, "foetypes");
        string Chips(string type, string tone) => string.Join(",", ft.Items.Single(r => r.Id == type).Items.Where(c => c.Tone == tone).Select(c => c.Title));
        Check(Chips("fire", "strong") == "Greydwarf,Greydwarf Brute,Neck" && Chips("fire", "weak") == "Draugr,Blob" && Chips("fire", "none") == "Leech" &&
              Chips("spirit", "none") == "Troll,Greydwarf,Greydwarf Brute,Boar,Neck,Leech" && ft.Items.Single(r => r.Id == "pierce").Value == "520",
              "B by damage type: strong against = their weakness, weak against = their resistance, no effect on = their immunity; the type's dealt");
        Check(PanelModel.StrongAgainst == "Takes more dmg from" && PanelModel.WeakAgainst == "Takes less dmg from" && PanelModel.NoEffectOn == "No dmg from" &&
              PanelModel.WeaknessKey == "takes more dmg from" && PanelModel.ImmunityKey == "no dmg from" && ft.Note.EndsWith("A row reads: " + Chips(ft.Items.First(r => r.Items.Any(c => c.Tone == "strong")).Id, "strong").Split(',')[0] + " takes more dmg from " + ft.Items.First(r => r.Items.Any(c => c.Tone == "strong")).Title + "."),
              "B foes wording (Joost 2026-10-09): takes more / less / no dmg from, in both views; the type view reads one real row as a sentence");
        var noGame = PageAll(plain, "foes");
        Check(Of(noGame, "switch") == null && Of(noGame, "foetable").Items.Select(r => r.Title).First() == "Draugr" && Of(noGame, "foetable").Items.All(r => r.Items.Count == 0) && Of(noGame, "foetypes") == null,
              "B foes without the game's creature data: dealt only, no cells, no arrow, no By damage type view (nothing guessed)");

        // ---------- Defense ----------
        var def = PageAll(rich, "defense");
        var guard = Of(def, "guard"); var src = Of(def, "sources");
        Check(def.Heading == "Defence" && guard.Value == "254" && guard.Title == "blocks" && guard.Value2 == "58" && guard.Text == "parries" && guard.Fraction == 0 && guard.Note == null,
              "B defense: two numbers side by side, 254 blocks (the 312 held blocks that were not parries) and 58 parries, no shared bar; one line says it is counted since install only");
        Check(src.Text == "after your armour" && src.Items.Select(s => s.Title + "=" + s.Value).SequenceEqual(new[] { "Troll=171", "Draugr=160", "Leech=100", "Greydwarf=39", "Blob=30", "Neck=14" }) &&
              src.Items[1].Items.Select(p => p.Id + "=" + p.Value).SequenceEqual(new[] { "slash=91", "pierce=17", "poison=52" }) && src.Items[2].Icon == "item:TrophyLeech" &&
              Near(src.Items[1].Items.Sum(p => p.Fraction), 1) && Near(src.Items[1].Fraction, 160.0 / 171),
              "B defense: damage received by source, most first, stacked by type in palette order, after your armour");
        Check(Of(def, "rows")?.Items.Any(i => i.Title == PanelModel.MostDefences && i.Title == "Most defences standing at once" && i.Value == "12") == true, "B defense: base defences stay (your character's count)");

        // ---------- Deaths ----------
        var dp = Page(rich, "deaths");
        var strip = Of(dp, "deathstrip"); var list = Of(dp, "deaths");
        Check(dp.Heading == "Deaths" && strip.Items.Select(t => t.Title + ":" + t.Count).SequenceEqual(new[] { "Meadows:0", "Black Forest:1", "Swamp:2" }) &&
              Near(strip.Items[2].Fraction, 2.8) && strip.Items[2].Items.Select(d => d.Value).SequenceEqual(new[] { "8 Oct 15:40", "8 Oct 15:02" }),
              "B where you fell: the found biomes in journey order, wider where you fell more, the time of each fall");
        Check(list.Items.Select(d => d.Title + "|" + d.Text + "|" + d.Value).SequenceEqual(new[] { "Leech|8 Oct 15:40 · Swamp|100", "Draugr|8 Oct 15:02 · Swamp|126", "Troll|8 Oct 14:31 · Black Forest|171" }) &&
              list.Items[1].Items.Select(p => p.Id + "=" + p.Value).SequenceEqual(new[] { "slash=74", "poison=52" }) && list.Items[0].Icon == "item:TrophyLeech" && list.Text == "after your armour",
              "B deaths: per fall the killer's trophy, time and biome, and the last 10 seconds by type (totals)");
        Check(list.Title == "Last 30 seconds" && list.Items.All(d => d.Tone == "timeline" && d.Note == null) && list.Items[1].Items.All(h => h.Kind == "hit") &&
              list.Items[1].Items.Select(h => h.Text + ":" + h.Value + "@" + h.Fraction.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).SequenceEqual(new[] { "Draugr:74@0.80", "Draugr:52@0.90" }) &&
              Near(list.Items[0].Items[0].Fraction2, 88.0 / 90) && Near(list.Items[2].Items[0].Fraction2, 1),
              "Deaths timeline: each death's last 30 s, hit by hit where it fell in time (6 s and 3 s before = 0.80, 0.90), as wide as it hurt (the largest hit = 1)");
        // an older record (no timeline): its last 10 s as totals, labelled 10 s; a page with both says neither
        var old = BattleSample(); old.Log.Deaths[0].Timeline.Clear();
        var oldList = Of(Page(old, "deaths"), "deaths"); var oldTroll = oldList.Items.Single(d => d.Id == "Troll");
        Check(oldList.Title == "Before each death" && oldTroll.Tone == null && oldTroll.Note == "last 10 seconds" && oldList.Items.Where(d => d.Tone == "timeline").All(d => d.Note == "last 30 seconds") && oldTroll.Items.Select(p => p.Kind + ":" + p.Id + "=" + p.Value).SequenceEqual(new[] { "part:blunt=171" }) &&
              oldList.Items.Count(d => d.Tone == "timeline") == 2,
              "Deaths timeline: an older death keeps its last 10 s totals strip, labelled 10 s; the caption says neither when both kinds are listed");
        foreach (var d in old.Log.Deaths) d.Timeline.Clear();
        Check(Of(Page(old, "deaths"), "deaths").Title == "Last 10 seconds", "Deaths timeline: only older records: the caption stays Last 10 seconds");
        var json = new Json().Open(); rich.Log.WriteTo(json); var back = PanelInput.FromSnapshot(json.Close().ToString().Replace("{\"measuredLog\"", "{\"name\":\"Rowan\",\"measuredLog\""));
        Check(back != null && back.Log.Deaths.Count == 3 && back.Log.Deaths.Single(d => d.Killer == "Draugr").Timeline.Select(h => h.Ago + "|" + h.Type + "|" + h.Amount).SequenceEqual(new[] { "6|slash|74", "3|poison|52" }),
              "Deaths timeline: the sequence travels in the snapshot and is read back (a fellow's copy draws it too)");
        var ovRows = Of(Page(rich, "overview", s => s.Window = TimeWindow.Session), "deathrows");
        Check(ovRows.Items.Select(d => d.Icon + "|" + d.Value + " " + d.Title + "|" + d.Text).SequenceEqual(new[] { "item:TrophyLeech|1 death from poison|8 Oct 15:40", "item:TrophyDraugr|1 death from slash|8 Oct 15:02", "item:TrophyFrostTroll|1 death from blunt|8 Oct 14:31" }) &&
              ovRows.Items.All(d => d.Src == "pc" && !string.IsNullOrEmpty(d.Icon)),
              "Battle deaths: every row the same layout, the killer's trophy, how many from which type, when");
        rich.NowUtc = rich.NowUtc.AddDays(1);
        Check(Of(Page(rich, "deaths"), "deaths").Items[0].Text == "8 Oct 15:40 · Swamp", "B deaths: an earlier day carries its date");

        // ---------- the windows: 10 and 30 minutes, and since install (Joost, play-test) ----------
        rich.DamageSinceInstall = PanelSample.SinceInstall(rich.Log);
        var sinceDealt = PanelModel.DealtRows(PanelModel.DamageSinceInstallRows(rich)).Sum(r => (double)r.Amount);
        var ovAll = Page(rich, "overview", s => { s.Window = TimeWindow.SinceInstall; });
        var hero = Of(ovAll, "hero");
        Check(ovAll.HasFilters && ovAll.Windows.Last().Selected && ovAll.Windows.Last().Label == "All" && ovAll.Biomes.Count == 0 && ovAll.HeadingWindow == "since install" &&
              Of(ovAll, "biomes") == null && hero != null && hero.Value == PanelModel.Number(sinceDealt) && Of(ovAll, "composition")?.Value == PanelModel.Number(PanelModel.DamageSinceInstallRows(rich).Where(r => r.Dir == "taken").Sum(r => (double)r.Amount)) &&
              Of(ovAll, "composition")?.Title == PanelModel.WhatHurtYou && PanelModel.Content(ovAll).Any(b => b.Kind == "note" && b.Text == PanelModel.BattleAllNote),
              "windows: All on the overview = everything this PC folded: the totals, what hurt you, the lifetime deaths; no biome strip or biome choice (per biome is not stored), and one line says so");
        var ovBiome = Page(rich, "overview", s => { s.Window = TimeWindow.Session; PanelModel.ToggleFacet(s, PanelModel.BattleOverviewFilter, "biome", "Swamp"); });
        Check(Of(ovBiome, "biomes") != null && ovBiome.Biomes.Count == 0 && Of(ovBiome, "biomes").Items.Any(b => b.Selected && b.Id == "Swamp") && PanelModel.FilterOf(ovBiome) != null && !PanelModel.Content(ovBiome).Any(b => b.Text == PanelModel.BattleAllNote),
              "windows: any window but All keeps the biome strip, and its tiles are the biome choice");
        var dmgAll = Page(rich, "damage", s => { s.Window = TimeWindow.SinceInstall; PanelModel.ToggleFacet(s, PanelModel.BattleDamageFilter, "biome", "Swamp"); s.View["Battle/damage/view"] = "type"; });
        Check(Of(dmgAll, "damagegrid") != null && Of(dmgAll, "damagegrid").Value == PanelModel.Number(sinceDealt) && dmgAll.Biomes.Count == 0 && dmgAll.HeadingWindow == "since install" && dmgAll.Heading == "Damage dealt" &&
              PanelModel.Content(dmgAll).Single(b => b.Kind == "filterbar").Items.Single().Title == PanelModel.DamageAllBiomeNote && !PanelModel.Content(dmgAll).Any(b => b.Kind == "filterbar" && b.Items.Any(i => i.Kind == "facet")),
              "windows: Damage dealt on All reads the since-install totals, whatever biome was chosen before (it has none); one line where the Biome row would be says so, and no dimmed chip");
        var deathsAll = Page(rich, "deaths", s => s.Window = TimeWindow.SinceInstall);
        Check(Of(deathsAll, "deathstrip") != null && deathsAll.Biomes.Count == 0 && Of(deathsAll, "deaths") is Block dlAll && dlAll.Count >= dlAll.Items.Count && !PanelModel.Content(deathsAll).Any(b => b.Kind == "note" && b.Text == PanelModel.DeathsAllNote) &&
              PanelModel.DeathsCaption(3, 4) == "Listed: this session's 3 of 4 falls, newest first" && PanelModel.DeathsCaption(3) == "3 falls, newest first" && PanelModel.DeathsCaption(3, 3) == "3 falls, newest first",
              "windows: Deaths on All lists this session's falls under a heading that says how many of the counted falls it lists (3 of 4), with no separate caveat line");
        var tenMin = Page(rich, "overview", s => s.Window = TimeWindow.LastTenMinutes);
        Check(tenMin.Windows.Select(w => w.Id + (w.Selected ? "*" : "")).SequenceEqual(new[] { "LastTenMinutes*", "LastThirtyMinutes", "LastHour", "LastThreeHours", "Session", "Today", "SevenDays", "ThirtyDays", "SinceInstall" }) &&
              tenMin.HeadingWindow == "last 10 minutes" && tenMin.Scope.Contains("last 10 minutes"),
              "windows: Last 10 minutes on the overview: its chip lit, the plate's heading in full");
        var keyPage = new PanelState { Chapter = Chapter.Battle }; keyPage.Page[Chapter.Battle] = "overview";
        var keyView = PanelModel.Build(rich, keyPage);
        Check(PanelModel.StepView(keyPage, keyView, 1) && keyPage.Window == TimeWindow.SinceInstall && PanelModel.StepView(keyPage, PanelModel.Build(rich, keyPage), 1) && keyPage.Window == TimeWindow.LastTenMinutes && keyView.Keys.Contains("[F] Window"),
              "windows: the view key cycles the windows on a page without a switch (Session > All > 10 min), and the footer says Window");
        var keyDamage = new PanelState { Chapter = Chapter.Battle }; keyDamage.Page[Chapter.Battle] = "damage";
        Check(PanelModel.StepView(keyDamage, PanelModel.Build(rich, keyDamage), 1) && keyDamage.View["Battle/damage/view"] == "type" && keyDamage.Window == TimeWindow.Session,
              "windows: where a page has a view switch (Damage), the view key keeps flipping that");

        // ---------- All keeps the biome strip when the per-biome totals are there (BiomeTally, folded in LocalTotals) ----------
        var richBio = BattleSample(); richBio.DamageSinceInstall = PanelSample.SinceInstall(richBio.Log); richBio.BiomeSinceInstall = PanelSample.BiomeSinceInstall(richBio.Log);
        var bioRows = PanelModel.BiomeRows(richBio.BiomeSinceInstall);
        var allTotal = PanelModel.DealtRows(PanelModel.DamageSinceInstallRows(richBio)).Sum(r => (double)r.Amount);
        var ovBio = Page(richBio, "overview", s => { s.Window = TimeWindow.SinceInstall; PanelModel.ToggleFacet(s, PanelModel.BattleOverviewFilter, "biome", "Swamp"); });
        var tiles = Of(ovBio, "biomes");
        var swampDealt = PanelModel.DealtRows(bioRows.Where(r => r.Biome == "Swamp").ToList()).Sum(r => (double)r.Amount);
        Check(tiles != null && tiles.Items.Single(t => t.Id == "Swamp").Selected && tiles.Items.Single(t => t.Id == "Swamp").Count == 2 && tiles.Items.Single(t => t.Id == "BlackForest").Count == 2 &&   // the session's fall and one on an earlier evening
              
              tiles.Value == PanelModel.Number(swampDealt) && PanelModel.FilterOf(ovBio) != null && !PanelModel.Content(ovBio).Any(b => b.Text == PanelModel.BattleAllNote) &&
              Of(ovBio, "hero")?.Title != PanelModel.DealtLabel,
              "windows: All with the per-biome totals keeps the biome strip, its death marks and the biome choice (Swamp lit: its dealt in the legend)");
        var ovBioAll = Page(richBio, "overview", s => s.Window = TimeWindow.SinceInstall);
        Check(Of(ovBioAll, "biomes").Value == PanelModel.Number(allTotal) && Of(ovBioAll, "biomes").Items.Single(t => t.Id == "BlackForest").Value == PanelModel.Number(PanelModel.DealtRows(bioRows.Where(r => r.Biome == "BlackForest").ToList()).Sum(r => (double)r.Amount)) &&
              Of(ovBioAll, "composition").Value == PanelModel.Number(PanelModel.DamageSinceInstallRows(richBio).Where(r => r.Dir == "taken").Sum(r => (double)r.Amount)),
              "windows: All, no biome chosen: the complete totals in the legend and in what hurt you; the tiles carry each biome's share");
        Check(Of(Page(richBio, "damage", s => { s.Window = TimeWindow.SinceInstall; PanelModel.ToggleFacet(s, PanelModel.BattleDamageFilter, "biome", "Swamp"); s.View["Battle/damage/view"] = "type"; }), "damagegrid").Value == PanelModel.Number(allTotal) && Page(richBio, "damage", s => s.Window = TimeWindow.SinceInstall).Biomes.Count == 0,
              "windows: Damage dealt on All stays biome-less (the weapon is not folded per biome)");
        richBio.BiomeFromUtc = richBio.NowUtc.AddDays(-1);
        Check(PanelModel.Content(Page(richBio, "overview", s => s.Window = TimeWindow.SinceInstall)).Any(b => b.Kind == "note" && b.Text == PanelModel.BiomeFromNote(richBio, richBio.BiomeFromUtc.Value)) && PanelModel.BiomeFromNote(richBio, richBio.BiomeFromUtc.Value).StartsWith("Per biome counted from 7 Oct"),
              "windows: a file from before per-biome counting says from when (\"Per biome counted from 7 Oct; the totals from install.\")");
        var snapBio = PanelInput.FromSnapshot(Snapshot.Build("0.5.1", 1, "Edda", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", null, "s", new SessionEvents(), richBio.Log, true, null, richBio.DamageSinceInstall, richBio.BiomeSinceInstall, richBio.BiomeFromUtc));
        Check(snapBio != null && snapBio.BiomeSinceInstall != null && snapBio.BiomeSinceInstall.Damage.Count == richBio.BiomeSinceInstall.Damage.Count && snapBio.BiomeSinceInstall.Deaths["Swamp"] == 2f && snapBio.BiomeFromUtc.HasValue &&
              PanelInput.FromSnapshot(Snapshot.Build("0.5.0", 1, "Old", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", null, "s", new SessionEvents(), richBio.Log, true)).BiomeSinceInstall == null,
              "windows: a fellow's newer copy carries the per-biome totals (their All overview keeps its strip); an older copy has none and says so");

        // ---------- every Battle page: sources, words ----------
        var all = new[] { "damage", "foes", "defense", "deaths" }.SelectMany(p => new[] { Page(rich, p), Page(plain, p) }).Concat(new[] { byWeapon, byType }).ToList();
        string unmarked = null;
        foreach (var v in all) foreach (var b in PanelModel.Content(v)) Walk(b);
        void Walk(Block b) { if ((b.Value ?? "").Any(char.IsDigit) && b.Src == null) unmarked ??= b.Kind + ": " + b.Value; foreach (var i in b.Items ?? new List<Block>()) Walk(i); }
        Check(unmarked == null, "B every number on the Battle pages carries its Src" + (unmarked != null ? ": " + unmarked : ""));
        var text = all.SelectMany(PanelModel.AllText).ToList();
        var bad = text.FirstOrDefault(s => s.Contains('—') || s.Contains('–') || System.Text.RegularExpressions.Regex.IsMatch(s, @"\b(enemy|enemies|damage taken|weak to|DPS)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        Check(bad == null, "B words: foe, received, no dashes, never 'weak to'" + (bad != null ? ": " + bad : ""));
        int Labels(PanelView v) => Zoned.Labels(v);   // the labels, and each "Since install" zone (ZonesTests.cs)
        Check(new[] { Page(rich, "damage"), byWeapon, Page(rich, "deaths") }.All(v => Labels(v) == 0) && Labels(PageAll(rich, "defense")) == 1 &&
              Labels(foes) == 1 && Zoned.Says(foes, Of(foes, "foetable")) && Labels(byType) == 1 && Zoned.Says(byType, Of(byType, "foetypes")),
              "B since install: none on the windowed pages (Damage, Deaths); once on Defense (after its heading) and once on each Foes view");

        // ---------- K2 (SOURCES.md fix 1): Foes and Defense read the since-install damage, the windowed pages the session log ----------
        var since = BattleSample();
        var before = new DamageTally();
        before.AddDealt("Troll", "Spears", DT(("pierce", 2000))); before.AddDealt("Troll", "Axes", DT(("chop", 90))); before.AddDealt("Wolf", "Swords", DT(("slash", 800)));
        before.AddTaken("Wolf", "EnemyHit", DT(("slash", 300))); before.AddTaken("Troll", "EnemyHit", DT(("blunt", 100)));
        since.DamageSinceInstall = before;
        var sFoesV = PageAll(since, "foes"); var sFoes = Of(sFoesV, "foetable"); var sDef = PageAll(since, "defense"); var sSrc = Of(sDef, "sources");
        Check(sFoes.Items.Select(r => r.Title + "=" + r.Value).SequenceEqual(new[] { "Troll=2\u00A0000", "Wolf=800" }) && Zoned.Says(sFoesV, sFoes),
              "K2 foes: By foe is the damage since install (earlier sessions too), tool damage left out, labelled since install");
        Check(Of(PageAll(since, "foes", s => s.View["Battle/foes/view"] = "type"), "foetypes").Items.Single(r => r.Id == "pierce").Value == "2\u00A0000",
              "K2 foes: By damage type reads the same since-install damage");
        Check(sSrc.Items.Select(s => s.Title + "=" + s.Value).SequenceEqual(new[] { "Wolf=300", "Troll=100" }) && Zoned.Says(sDef, sSrc) && Of(sDef, "guard").Value == "254",
              "K2 defense: damage received by source is the damage since install; blocks stay");
        Check(Of(Page(since, "damage", s => s.View["Battle/damage/view"] = "type"), "damagegrid").Value == Of(Page(rich, "damage", s => s.View["Battle/damage/view"] = "type"), "damagegrid").Value,
              "K2 the windowed pages (Damage) keep reading this session's log");
        since.DamageSinceInstall = new DamageTally();
        Check(Of(PageAll(since, "foes"), "foetable") == null && Of(PageAll(since, "defense"), "sources") == null,
              "K2 nothing dealt or received since install: no table (the session log is not used when the since-install damage is known)");

        // ---------- Battle layer (K1 for hits and deaths): the game's counter when Hearthwoven first ran + counted since ----------
        Dictionary<string, Dictionary<string, float>> K(params (string k, float v)[] kv) => new Dictionary<string, Dictionary<string, float>> { ["battle"] = kv.ToDictionary(x => x.k, x => x.v) };
        bool FadedNoted(PanelView v) => PanelModel.Content(v).Any(b => b.Kind == "note" && b.Text == PanelModel.FadedKey);
        var lay = BattleSample();
        lay.Character["EnemyHits"] = 5600; lay.Character["Deaths"] = 41; lay.Character["HitsTakenEnemies"] = 1300; lay.Character["HitsTakenPlayers"] = 2;
        lay.Baseline = K(("EnemyHits", 5000), ("Deaths", 40)); lay.ExactAtBaseline = K();
        SessionEvents.Add(lay.Events.Battle, "EnemyHits", 120); SessionEvents.Add(lay.Events.Battle, "PlayerHits", 2); SessionEvents.Add(lay.Events.Battle, "Deaths", 3);
        var lf = PageAll(lay, "foes"); var lh = Life(lf, "hits on foes");
        Check(PanelModel.Content(lf).Count(b => b.Kind == "note" && b.Text == "faded = before install") == 1 && !PanelModel.AllText(lf).Any(t => t.StartsWith("Faded:")),
              "Battle layer foes: the faded key is the legend chip \"faded = before install\", once, not the sentence \"Faded: before Hearthwoven, ...\"");
        Check(lh != null && lh.Value == "5\u00A0120" && lh.Faded == "5\u00A0000" && lh.Solid == "120" && lh.Title == "hits on foes" && lh.Src == "character" &&
              PanelModel.Content(lf).FirstOrDefault(x => x.Kind == "hero" && x.Title == "hits on other players") is Block lp && lp.Value == "2" && lp.Faded == null && lp.Src == "pc" && Zoned.Says(lf, lp) && FadedNoted(lf),   // zones: this PC's number leads the ember zone
              "Battle layer foes: hits on foes = the game's EnemyHits at first run (5\u00A0000 faded) + every hit counted since (120 solid), its later 5\u00A0600 never added; hits on other players all counted on this PC");
        var ld = Page(lay, "deaths"); var ldh = Of(ld, "hero");
        Check(ldh != null && ldh.Value == "43" && ldh.Faded == "40" && ldh.Solid == "3" && ldh.Title == "deaths" && ldh.Src == "character" &&
              PanelModel.Content(ld).Select(b => b.Kind).Where(k => k != "plate" && k != "zone").Take(3).SequenceEqual(new[] { "hero", "note", "deathstrip" }) && Of(ld, "deaths").Items.Count == 3,
              "Battle layer deaths: one lifetime line first (the game's Deaths at first run faded + recorded since install solid), then the window's strip and list as before");
        var dRows = PanelModel.Content(PageAll(lay, "defense")).SkipWhile(b => !(b.Kind == "section" && b.Title == "Hits received")).Skip(1).FirstOrDefault();
        Check(dRows != null && dRows.Items.Select(i => i.Title + "=" + i.Value).SequenceEqual(new[] { "From foes=1\u00A0300", "From other players=2" }) && dRows.Src == "character" && !dRows.SinceInstall,
              "Battle hits received: on Defense, the game's own count stands alone (complete: counted on your own character), your character's");
        var lo = Page(lay, "overview");
        Check(!PanelModel.Content(lo).Any(b => b.Kind == "hero" || (b.Kind == "section" && b.Title == "Hits")) && !PanelModel.AllText(lo).Any(t => t.Contains("5\u00A0120") || t.Contains("5\u00A0600") || t.Contains("1\u00A0300")),
              "Battle: no lifetime count under the window chips");
        // an install from before the baseline: 100 hits and 3 deaths counted exactly by then, which the game's count already holds
        lay.ExactAtBaseline = K(("EnemyHits", 100), ("Deaths", 3));
        var oh = Life(PageAll(lay, "foes"), "hits on foes"); var od = Of(Page(lay, "deaths"), "hero");
        Check(oh.Value == "5\u00A0020" && oh.Faded == "5\u00A0000" && oh.Solid == "20" && od.Value == "40" && od.Faded == null && od.Src == "character" && !FadedNoted(Page(lay, "deaths")),
              "Battle layer, no double count: an older install's 100 hits and 3 deaths from before the baseline are in the game's count (5\u00A0020 and 40, not 5\u00A0120 and 43)");
        // a fellow's copy (no baseline): the game counter minus what was counted exactly, at least 0, so the total is the game's
        lay.Baseline = null; lay.ExactAtBaseline = null;
        var fh = Life(PageAll(lay, "foes"), "hits on foes"); var fd = Of(Page(lay, "deaths"), "hero");
        Check(fh.Value == "5\u00A0600" && fh.Faded == "5\u00A0480" && fh.Solid == "120" && fd.Value == "41" && fd.Faded == "38" && fd.Solid == "3",
              "Battle layer without a baseline: faded = the game counter minus the exact count, the total stays the game's (5\u00A0600, 41), never doubled");
        // a character that started with Hearthwoven: everything counted on this PC, one plain number, no faded note
        lay.Baseline = K(); lay.ExactAtBaseline = K(); lay.Character["EnemyHits"] = 90; lay.Character["Deaths"] = 0;
        var nh = PanelModel.Content(PageAll(lay, "foes")).FirstOrDefault(b => b.Kind == "hero" && b.Title == "hits on foes");
        Check(nh.Value == "120" && nh.Faded == null && nh.Src == "pc" && !FadedNoted(PageAll(lay, "foes")) && Of(Page(lay, "deaths"), "hero").Value == "3",
              "Battle layer: a character that started with Hearthwoven shows every hit counted on this PC (120, more than the game's 90), plain, Src this PC");
        var none = BattleSample();
        Check(!PanelModel.Content(PageAll(none, "foes")).Any(b => b.Kind == "hero" && b.Title == "hits on foes") && !PanelModel.Content(Page(none, "deaths")).Any(b => b.Kind == "hero" && b.Src == "character"), "Battle layer: no hits and no deaths anywhere: no lifetime line");
    }
}
