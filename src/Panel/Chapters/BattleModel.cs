using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Battle chapter pages (ch-battle; approved prototypes work/hearthwoven-visual-vocabulary/proto r4battle-damage-type,
    /// r4battle-damage-weapon, r4battle-foes-foe, r4battle-foes-type, r4battle-defense, r2-voyages-deaths). Overview stays
    /// with the shared page layout (vf-layout). Pure C#: the game's creature and arrow data arrive through PanelInput.Foe and
    /// PanelInput.Arrows (read at runtime by BattleGame in Chapters/BattleUi.cs; the tests pass a small sample).
    ///
    /// Block kinds (all chapter-specific; numbers carry Src "pc" = counted on this PC since install, as the log is):
    /// - damagegrid (Damage, By type): Value = all types together, Title = the foot row's label ("All types"). Items[0] Kind
    ///   "weapons" (Title = the total column's head, Items = Kind "weapon": Id melee|bow|magic, Title, Icon vocab:weapon-*,
    ///   Value = that column's total); then one Kind "dmgtype" per damage type with damage (Id, Title, Icon vocab:dmg-*, Colour,
    ///   Value = the row total, Items = Kind "cell" per weapon in the head's order, Value "" when none, Fraction on one scale for
    ///   the whole grid, the largest cell = 1).
    /// - dmgmix (Damage, By weapon): one per weapon kind, the composition shape: Id, Icon vocab:weapon-*, Title, Value = total,
    ///   Items = parts in the palette's type order (Id, Icon vocab:dmg-*, Title, Value, Fraction = share, Colour).
    /// - foetable (Foes, By foe): Items = Kind "foe" (Id prefab, Icon = its trophy, Title, Value = dealt, Fraction on the
    ///   block's scale; Items = an optional Kind "arrow" (Icon, Title: the best arrow you have against it, from the game's
    ///   arrow damage and the foe's modifiers) and, when the game knows the foe, eight Kind "mod" cells in type order (Id,
    ///   Icon, Colour, Tone weakness|resistance|immunity|"" = normal)).
    /// - foetypes (Foes, By damage type): Items = Kind "dmgtype" (Id, Title, Icon, Colour, Value = dealt of that type) with
    ///   Items = Kind "foe" chips, Tone strong (the foe's weakness) | weak (its resistance) | none (its immunity).
    /// - guard (Defense): two numbers side by side, each with its own mark (Joost 2026-10-08: a parry reads as its own skill):
    ///   Value = blocks that were not parries, Title its word; Value2 = parries, Text its word. No shared bar; Note = the page's
    ///   window's scope is the heading's (HISTORY-06.md: Defence has the one window set).
    /// - sources (Defense): Title, Text = "after your armour", Items = Kind "source" (Id, Icon = trophy, Title, Value,
    ///   Fraction on the block's scale, Items = Kind "part" per type: Id, Title, Value, Fraction = share, Colour).
    /// - deathrows (Overview, the window's deaths): Items = Kind "death" per killer and damage type, most first (Id killer,
    ///   Icon = its trophy, else the type's icon, else the death mark; Colour = the type's colour for a type icon; Value =
    ///   how many, Title "death(s) from <type>", Text = the times, newest first, at most three). Every row the same layout.
    /// - deathstrip (Deaths): Title, Items = Kind "biome" per found biome in journey order, Ocean last (Id, Title, Colour,
    ///   Tone ink, Count = deaths there, Fraction = width weight 1 + 0.9 x deaths, Items = Kind "death" with Value = time).
    /// - deaths (Deaths): Title "Last 30 s" (every death has its timeline), "Last 10 seconds" (none has) or "Before each
    ///   death" (mixed), Text "after your armour", Items = Kind "death" per death, newest first (Id killer, Icon its trophy,
    ///   Title, Text "time · biome", Value = total, Note = "last 30 s" | "last 10 s"). A death with its timeline: Tone
    ///   "timeline", Items = Kind "hit" per hit and damage type, oldest first (Id type, Icon, Title = the type's name, Text =
    ///   who hit, Value, Colour, Fraction = when, 0 = 30 s before the death .. 1 = the death, Fraction2 = amount / the largest
    ///   hit in the block). An older death: Items = Kind "part" per type as above (its last 10 s as totals).
    /// Each page sits on the plate (vf-layout's Plate); Damage and Foes carry vf-layout's view switch (Switch, name "view":
    /// By weapon / By type, By foe / By damage type, the first the default), its caption what the numbers are.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>A creature as the game defines it: its damage modifier per type (HitData.DamageModifier names, "Weak",
        /// "Resistant", "Immune", ...) and the prefab of its trophy (null = none).</summary>
        public class FoeData
        {
            public readonly Dictionary<string, string> Modifiers = new Dictionary<string, string>();
            public string Trophy;
        }

        /// <summary>An arrow as the game defines it: prefab, item token ("$item_arrow_frost") and its damage per type.</summary>
        public class ArrowData
        {
            public string Prefab, Token;
            public readonly Dictionary<string, float> Damage = new Dictionary<string, float>();
        }

        /// <summary>The eight damage types in the palette's order (VOCABULARY.md); tool damage (chop, pickaxe) is never shown in Battle.</summary>
        public static readonly string[] BattleTypes = { "blunt", "slash", "pierce", "fire", "frost", "lightning", "poison", "spirit" };
        // received damage may also be raw (falls, the edge of the world): kept, after the eight, in its neutral colour
        static readonly string[] ReceivedTypes = BattleTypes.Concat(new[] { "damage" }).ToArray();
        public static readonly string[] WeaponKinds = { "melee", "bow", "magic" };
        public const string Weakness = "weakness", Resistance = "resistance", Immunity = "immunity";
        public const string WeaknessLabel = "Weakness", ResistanceLabel = "Resistance", ImmunityLabel = "Immunity";
        // what the three looks of a cell mean, said once above the table (the gold cell was unexplained below the fold)
        // the game counts the most defence pieces you had standing at once (GAME-METRICS 1d), not how many you built (SOURCES.md)
        public const string MostDefences = "Most defences standing at once";
        // Joost 2026-10-09: the relation in his words, the same in both Foes views ("dmg", VOCABULARY.md): the cell or the column says
        // what the foe takes from the type; a cell also carries a mark (+ / - / the stamp), so it never reads as a gold "dealt" bar
        public const string WeaknessKey = "takes more dmg from", ResistanceKey = "takes less dmg from", ImmunityKey = "no dmg from";
        public const string FoeTypesNote = "From the game, for each foe you have struck.";
        public const string FoeHitsNote = EarlierIncomplete;   // the game's own count (faded) misses hits on foes another player's PC controls (About says why)
        // the three columns say what the foe's side of the type is, in the words of the key on the By foe view (fix3: "Weak against" read as bad for you)
        public const string StrongAgainst = "Takes more dmg from", WeakAgainst = "Takes less dmg from", NoEffectOn = "No dmg from", TypeDealtHead = "Damage type";
        // the columns read from the type to the foe, the words from the foe to the type: one real row of the table, said as a sentence
        public static string FoeTypesExample(string foe, string key, string type) => "A row reads: " + foe + " " + key + " " + type + ".";
        public const string FoeHead = "Foe", DealtHead = "Dealt", BestArrowHead = "Best arrow", TotalHead = "Total", AllTypes = "All types";
        public const string ByType = "By type", ByWeapon = "By weapon", ByFoe = "By foe", ByDamageType = "By damage type";
        public const string ReceivedBySource = "Received from";
        public const string WiderNote = "wider where you fell more";
        public const string WhereYouFell = "Where you fell", LastTenSeconds = "Last 10 seconds", LastTenShort = "last 10 seconds", ReceivedWord = "received";
        public const string LastThirty = "Last 30 seconds", LastThirtyShort = "last 30 seconds", BeforeEachDeath = "Before each death";
        public const int FoeTop = 10, BattleDeathTop = 10;
        const string RawColour = "#9b8f80";

        static Block TopLine(string value, string title, string text) => new Block { Kind = "stat", Value = value, Title = title, Text = text, Src = SrcPc, Source = TagMeasured };
        public const string WeaponSkillsScope = "the weapons of this window, your levels now";
        public static string WeaponName(string kind) => kind == "bow" ? "Bow" : kind == "magic" ? "Magic" : "Melee";

        /// <summary>The weapon kind of a hit, from the skill the game booked it on: bows and crossbows are "bow", the staffs'
        /// elemental and blood magic "magic", everything else (swords, axes, clubs, spears, fists, tools) "melee".</summary>
        public static string WeaponOf(string skill)
        {
            switch (skill)
            {
                case "Bows": case "Crossbows": return "bow";
                case "ElementalMagic": case "BloodMagic": return "magic";
                default: return "melee";
            }
        }

        /// <summary>A foe's relation to a damage type, from the game's modifier: weakness, resistance, immunity, or null (normal).</summary>
        public static string Relation(string modifier)
        {
            switch (modifier)
            {
                case "Weak": case "VeryWeak": case "SlightlyWeak": return Weakness;
                case "Resistant": case "VeryResistant": case "SlightlyResistant": return Resistance;
                case "Immune": case "Ignore": return Immunity;
                default: return null;
            }
        }

        /// <summary>The game's damage factor for a modifier (HitData.DamageModifiers.ApplyIfNotImmune).</summary>
        public static float Multiplier(string modifier)
        {
            switch (modifier)
            {
                case "VeryWeak": return 2f;
                case "Weak": return 1.5f;
                case "SlightlyWeak": return 1.25f;
                case "SlightlyResistant": return 0.75f;
                case "Resistant": return 0.5f;
                case "VeryResistant": return 0.25f;
                case "Immune": case "Ignore": return 0f;
                default: return 1f;
            }
        }

        static string DamageColour(string type) => DamageLook.TryGetValue(type, out var l) ? l.colour : RawColour;
        static string DamageIcon(string type) => BattleTypes.Contains(type) ? "vocab:dmg-" + type : "";
        static FoeData FoeOf(PanelInput input, string prefab)
        {
            try { return string.IsNullOrEmpty(prefab) ? null : input.Foe?.Invoke(prefab); } catch { return null; }
        }
        static string TrophyOf(PanelInput input, string prefab) { var f = FoeOf(input, prefab); return string.IsNullOrEmpty(f?.Trophy) ? "" : "item:" + f.Trophy; }
        static double Get(Dictionary<string, double> d, string k) => d.TryGetValue(k, out var v) ? v : 0;
        static bool Has(IDictionary<string, float> d, string k) => d != null && k != null && d.TryGetValue(k, out var v) && v > 0;

        /// <summary>Your hits on foes, the eight damage types only, before the foe's armour.</summary>
        public static List<DamageRow> DealtRows(IEnumerable<DamageRow> rows) => rows.Where(r => r.Dir == "dealt" && r.Amount > 0 && BattleTypes.Contains(r.Type)).ToList();

        // the switch's caption, left of the chips (r4battle's top row): what every number on the page is
        static string DealtCaption => DealtLabel + ", " + DealtQualifier;
        // a fellow player's copy says whose it is and when it is from, on the plate (as the Battle overview does)
        // Foes and Defense (not windowed) read the damage since install when the copy carries it; the windowed pages their last session
        // a windowed page (Overview, Damage, Deaths) adds the line that says why the other windows are greyed
        static string PlateText(PanelInput input, bool sinceInstall = false, bool windowed = false)
        {
            if (input.IsSelf) return null;
            var line = sinceInstall && input.SharedSinceInstall ? FellowScope(input) : Name(input) + ", last shared session" + (input.LastRecordedUtc.HasValue ? ", " + Local(input, input.LastRecordedUtc.Value).ToString("d MMM HH:mm", Inv) : "");
            return windowed ? line + "\n" + SharedWindowsLine(input) : line;
        }

        /// <summary>Whether a fellow's copy can show a window: their last session, and since install when they share it. The 10 min .. 3 h
        /// windows count back from your clock, not theirs, so a copy cannot answer them (fix4). Your own book shows every window.</summary>
        public static bool WindowShared(PanelInput input, TimeWindow w) => input.IsSelf || w == TimeWindow.Session || (w == TimeWindow.SinceInstall && input.SharedSinceInstall);

        /// <summary>Why a fellow's window chips are greyed, in one line (Joost 2026-10-09: "Tor shares his last session only").</summary>
        public static string SharedWindowsLine(PanelInput input) => Name(input) + (input.SharedSinceInstall ? " shares the last session and since install only" : " shares the last session only");

        public const string TipWord = "Tip: ";
        public const string BattleAllNote = "Per biome and each fall are kept for the session only. Choose This session or a shorter window for them.",
                            DeathsAllNote = "No falls to list from this session. Earlier ones are only counted.";

        /// <summary>"Per biome counted from 9 Oct: the totals from install." (a file from before per-biome counting)</summary>
        public static string BiomeFromNote(PanelInput input, DateTime fromUtc) => "Per biome counted from " + ZoneDate(Local(input, fromUtc), Local(input, input.NowUtc)) + "; the totals from install.";

        /// <summary>The folded per-biome damage as rows (no foe, no time; tool damage left out, as the totals leave it).</summary>
        public static List<DamageRow> BiomeRows(BiomeTally t)
        {
            var rows = new List<DamageRow>();
            foreach (var kv in t.Damage)
            {
                var p = kv.Key.Split('|');   // biome|dir|type
                if (p.Length < 3 || kv.Value <= 0 || p[2] == "chop" || p[2] == "pickaxe") continue;
                rows.Add(new DamageRow { Biome = p[0], Dir = p[1], Other = "", Cause = "", Type = p[2], Amount = kv.Value });
            }
            return rows;
        }
        /// <summary>One death per fall counted per biome: no time, no killer (the strip counts them per biome).</summary>
        static List<EventLog.Death> BiomeDeaths(BiomeTally t)
        {
            var deaths = new List<EventLog.Death>();
            foreach (var kv in t.Deaths) for (int i = 0; i < (int)Math.Round(kv.Value); i++) deaths.Add(new EventLog.Death { Biome = kv.Key });
            return deaths;
        }

        /// <summary>Since install on the overview: the totals (what this PC folded), what hurt you by type and the lifetime deaths. With the
        /// per-biome totals (BiomeTally: yours, or a fellow's newer copy) the biome strip and the biome choice stay; without them (an older
        /// copy) the strip is not drawn and one line says what stays per session. A file from before per-biome counting says from when.</summary>
        static void BattleOverviewSince(PanelInput input, PanelView view, PanelState state, List<DamageRow> rows, bool perBiome, TimeWindow w = TimeWindow.SinceInstall)
        {
            var day = IsDayWindow(w);   // a day window: input is the window's copy (InWindow), its rows the window's
            view.Heading = OverviewHeading;
            var byBiome = perBiome ? BiomeRows(input.BiomeSinceInstall) : null;
            var chosen = new HashSet<string>(perBiome ? Chosen(state, BattleOverviewFilter, "biome") : new List<string>());   // the biome tiles are the filter (BattleFacets.cs)
            var shown = perBiome ? InBiomes(byBiome, chosen) : rows;   // no biome chosen: the complete totals
            var dealt = DealtRows(shown).Sum(r => (double)r.Amount); var taken = shown.Where(r => r.Dir == "taken").ToList(); var total = taken.Sum(r => (double)r.Amount);
            if (dealt == 0 && total == 0 && !perBiome) view.Blocks.Add(day ? WindowEmpty(input, w) : SinceInstallEmpty(input, "battles", BattleNext));
            else
            {
                if (perBiome)
                {
                    var fell = BiomeDeaths(input.BiomeSinceInstall);
                    var strip = BiomeStrip(input, byBiome, fell, chosen);
                    var bar = BiomeFilterBar(state, BattleOverviewFilter, strip, byBiome, fell);
                    if (strip != null) { strip.Value = dealt > 0 ? N(dealt) : null; strip.Value2 = total > 0 ? N(total) : null; strip.Text = AfterOf(input); strip.Title = ScopeOfSet(chosen); }
                    Add(view, strip);
                    Add(view, bar);
                }
                else Add(view, Hero((N(dealt), DealtLabel, SrcPc, DealtQualifier)));   // what hurt you carries its own total just below
                Columns(view, Stretch(v =>
                {
                    Add(v, Composition(WhatHurt(input) + (ScopeOfSet(chosen) == null ? "" : " " + ScopeOfSet(chosen)), ReceivedByType(taken), TypeName, Look(DamageLook), SrcPc, t => "damage:" + t, AfterOf(input)));
                    if (!perBiome) LifetimeHero(v, input, ("Deaths", DeathWord, DeathsWord));   // with the strip the deaths are its marks per biome; the lifetime line lives on Deaths
                }),
                Stretch(v => { if ((input.Events?.Blocks ?? 0) > 0) v.Blocks.Add(new Block { Kind = "link", Icon = "item:ShieldWood", Title = BlocksLink, Id = "Battle/defense" }); }));
                if (!perBiome && !day) view.Blocks.Add(new Block { Kind = "note", Text = BattleAllNote });
                else if (input.BiomeFromUtc.HasValue) view.Blocks.Add(new Block { Kind = "note", Text = BiomeFromNote(input, input.BiomeFromUtc.Value) });
            }
            Plate(view, "ui:chapter-battle", PlateText(input, true, windowed: true));
        }

        /// <summary>"By type": damage types down, weapon kinds across, each cell a bar on one scale for the whole grid, totals right and under.</summary>
        public static Block DamageGrid(IEnumerable<DamageRow> dealtRows)
        {
            var dealt = DealtRows(dealtRows);
            var cell = dealt.GroupBy(r => r.Type + "|" + WeaponOf(r.Cause)).ToDictionary(g => g.Key, g => g.Sum(r => (double)r.Amount));
            var max = cell.Values.DefaultIfEmpty(0).Max();
            if (max <= 0) return null;
            var weapons = WeaponKinds.ToList();   // all three kinds always: the columns stay where they are in every window (one with nothing is dim)
            var items = new List<Block>
            {
                new Block
                {
                    Kind = "weapons", Title = TotalHead,
                    Items = weapons.Select(w => { var sum = BattleTypes.Sum(t => Get(cell, t + "|" + w)); return new Block { Kind = "weapon", Id = w, Title = WeaponName(w), Icon = "vocab:weapon-" + w, Value = N(sum), Tone = sum > 0 ? null : "idle", Src = SrcPc, Source = TagMeasured }; }).ToList(),
                },
            };
            foreach (var t in BattleTypes)
            {
                var total = weapons.Sum(w => Get(cell, t + "|" + w));
                if (total <= 0) continue;
                items.Add(new Block
                {
                    Kind = "dmgtype", Id = t, Title = TypeName(t), Icon = DamageIcon(t), Colour = DamageColour(t), Value = N(total), Src = SrcPc, Source = TagMeasured,
                    Items = weapons.Select(w => { var v = Get(cell, t + "|" + w); return new Block { Kind = "cell", Id = w, Value = v > 0 ? N(v) : "", Fraction = (float)(v / max), Colour = DamageColour(t) }; }).ToList(),
                });
            }
            return new Block { Kind = "damagegrid", Title = AllTypes, Value = N(cell.Values.Sum()), Src = SrcPc, Source = TagMeasured, Items = items };
        }

        /// <summary>"By weapon": one proportional bar per weapon kind, split into the damage types it dealt.</summary>
        public static List<Block> DamageMixes(IEnumerable<DamageRow> dealtRows)
        {
            var dealt = DealtRows(dealtRows);
            var mixes = new List<Block>();
            var largest = WeaponKinds.Select(w => dealt.Where(r => WeaponOf(r.Cause) == w).Sum(r => (double)r.Amount)).DefaultIfEmpty(0).Max();
            foreach (var w in WeaponKinds)
            {
                var byType = dealt.Where(r => WeaponOf(r.Cause) == w).GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Sum(r => (double)r.Amount));
                var total = byType.Values.Sum();
                if (total <= 0) continue;
                mixes.Add(new Block
                {
                    Kind = "dmgmix", Id = w, Icon = "vocab:weapon-" + w, Title = WeaponName(w), Value = N(total), Fraction = (float)(total / largest), Src = SrcPc, Source = TagMeasured,
                    Items = BattleTypes.Where(t => Get(byType, t) > 0).Select(t => new Block
                    {
                        Kind = "part", Id = t, Icon = DamageIcon(t), Title = TypeName(t), Value = N(Get(byType, t)), Fraction = (float)(Get(byType, t) / total), Colour = DamageColour(t),
                    }).ToList(),
                });
            }
            return mixes;
        }

        static void BattleDamage(PanelInput input, PanelView view, PanelState state, string page, List<DamageRow> rows, TimeWindow w)
        {
            view.Heading = "Damage dealt"; view.HeadingSource = null;
            // the Biome row (the same pattern as the Overview's tiles and Deaths): the hits of the window narrow to the chosen biomes; since install the
            // rows carry no biome, so the same line stands where the row would, saying why
            var since = w == TimeWindow.SinceInstall || IsDayWindow(w);   // the totals and the day rows carry no biome on a hit
            var biomeRf = since ? null : DamageBiomeFilter(input, state, rows);
            if (biomeRf != null && biomeRf.Bar != null) rows = biomeRf.Rows;
            var grid = DamageGrid(rows);
            if (grid != null || (biomeRf != null && biomeRf.Narrowed)) Add(view, since ? (BiomeRow(input).Options.Count >= 2 ? BiomeLine(IsDayWindow(w) ? DamageDayBiomeNote : DamageAllBiomeNote) : null) : biomeRf.Bar);   // the bar stays on an empty choice: it is how you unchoose
            if (grid == null) view.Blocks.Add(biomeRf != null && biomeRf.Narrowed ? Empty(NothingForChoice, "Clear the biome choice to see all your damage in this window.") : w == TimeWindow.SinceInstall ? SinceInstallEmpty(input, "battles", BattleNext) : WindowEmpty(input, w));   // vf-fix1's empty state
            else
            {
                var mixes = DamageMixes(rows);
                // the one answer first: the type and the weapon that did the most
                var topType = grid.Items.Skip(1).OrderByDescending(t => ParseCount(t.Value)).First(); var topWeapon = mixes.OrderByDescending(m => ParseCount(m.Value)).First();
                var byType = new List<Block> { TopLine(topType.Value, topType.Title + " damage, the most of any type", null), grid };
                var byWeapon = new List<Block> { TopLine(topWeapon.Value, topWeapon.Title + " damage, the most of any weapon", null) }; byWeapon.AddRange(mixes);
                // Joost 2026-10-09: By weapon is the default (first), By type the second view
                Switch(view, state, page, "view", DealtCaption, ("weapon", ByWeapon, byWeapon), ("type", ByType, byType));
            }
            // the weapon skills these hits were booked on (the hit's skill is the row's cause), in the ladders' Fight order
            var used = new HashSet<string>(rows.Where(r => r.Dir == "dealt" && r.Amount > 0 && BattleTypes.Contains(r.Type)).Select(r => r.Cause));
            SkillStrip(input, view, WeaponSkillsHeading, SkillGroups[0].skills.Where(used.Contains), input.IsSelf ? WeaponSkillsScope : "the weapons of this window, " + Name(input) + "'s levels now");
            Plate(view, "ui:chapter-battle", PlateText(input, w == TimeWindow.SinceInstall, windowed: true));
        }

        public const string DamageDayBiomeNote = "Not kept per biome by day · choose 10 min to Session to narrow by biome";

        // ---------- Foes ----------

        /// <summary>Foes and Defense are not windowed: the damage since install (LocalTotals, SOURCES.md fix 1), one row per
        /// (foe or source, skill or cause, type), no time or biome. Not known (an older fellow's copy): the session log as before.</summary>
        public static List<DamageRow> DamageSinceInstallRows(PanelInput input)
        {
            var d = input.DamageSinceInstall;
            if (d == null) return Damage(input.Log, TimeWindow.Session, "", input.NowUtc);
            var rows = new List<DamageRow>();
            foreach (var (dir, map) in new[] { ("dealt", d.Dealt), ("taken", d.Taken) })
                foreach (var kv in map)
                {
                    var p = kv.Key.Split('|');
                    if (p.Length < 3) continue;
                    rows.Add(new DamageRow { Biome = "", Dir = dir, Other = p[0], Cause = p[1], Type = p[2], Amount = kv.Value });
                }
            return rows;
        }

        /// <summary>The best arrow you have against a foe: among the arrows your character can make (their recipe known; a fellow's
        /// copy, which carries no recipes: the arrows made or brought in) (at least two
        /// kinds, else there is no choice to show), the one whose damage, after the foe's own modifiers, is highest. A tie
        /// names none. The bow's own damage adds the same to every arrow, so it does not change the order.</summary>
        public static ArrowData BestArrow(FoeData foe, IList<ArrowData> known)
        {
            if (foe == null || known == null || known.Count < 2) return null;
            double Score(ArrowData a) => a.Damage.Sum(kv => kv.Value * Multiplier(foe.Modifiers.TryGetValue(kv.Key, out var m) ? m : "Normal"));
            var ranked = known.Select(a => (a, s: Score(a))).OrderByDescending(x => x.s).ThenBy(x => x.a.Prefab, StringComparer.Ordinal).ToList();
            return ranked[0].s > ranked[1].s + 1e-3 ? ranked[0].a : null;
        }

        /// <summary>The arrows open to your character, as the game defines them (damage from the game at runtime, so a game update or a
        /// mod's arrows follow): every arrow whose recipe your character knows (Joost: Needle showed while Carapace was known); without
        /// the recipe record (a fellow's copy), the arrows made or brought in (the game's own counters).</summary>
        public static List<ArrowData> KnownArrows(PanelInput input)
        {
            IList<ArrowData> all = null;
            try { all = input.Arrows?.Invoke(); } catch { all = null; }
            if (input.RecipeKnown != null)
                return (all ?? new List<ArrowData>()).Where(a => { try { return input.RecipeKnown(a.Token); } catch { return false; } }).ToList();
            return (all ?? new List<ArrowData>()).Where(a => Has(input.ItemsCrafted, a.Token) || Has(input.ItemsPickedUp, a.Token)).ToList();
        }

        /// <summary>"By foe": one row per foe you struck, most damage first, its trophy, the dealt bar, your best arrow and
        /// its modifier per damage type, read from the game (no modifiers known: no cells).</summary>
        public static Block FoeTable(PanelInput input, IEnumerable<DamageRow> dealtRows)
        {
            var byFoe = DealtRows(dealtRows).GroupBy(r => r.Other).Select(g => (key: g.Key, v: g.Sum(r => (double)r.Amount)))
                                            .OrderByDescending(x => x.v).ThenBy(x => x.key, StringComparer.Ordinal).Take(FoeTop).ToList();
            if (byFoe.Count == 0) return null;
            var max = byFoe[0].v; var arrows = KnownArrows(input);
            var rows = new List<Block>();
            foreach (var (key, v) in byFoe)
            {
                var foe = FoeOf(input, key);
                var cells = new List<Block>();
                var best = BestArrow(foe, arrows);
                if (best != null) cells.Add(new Block { Kind = "arrow", Id = best.Prefab, Icon = "item:" + best.Prefab, Title = Who(input, best.Prefab) });
                if (foe != null)
                    foreach (var t in BattleTypes)
                        cells.Add(new Block { Kind = "mod", Id = t, Icon = DamageIcon(t), Colour = DamageColour(t), Title = TypeName(t), Tone = Relation(foe.Modifiers.TryGetValue(t, out var m) ? m : null) ?? "" });
                rows.Add(new Block { Kind = "foe", Id = key, Icon = TrophyOf(input, key), Title = Who(input, key), Value = N(v), Fraction = (float)(v / max), Src = SrcPc, Source = TagMeasured, Items = cells });
            }
            return new Block { Kind = "foetable", Src = SrcPc, Source = TagMeasured, Items = rows };
        }

        /// <summary>"By damage type": per type, the foes it is strong against (their weakness), weak against (their
        /// resistance) and has no effect on (their immunity), among the foes you struck. Null when the game knows none of them.</summary>
        public static Block FoeTypes(PanelInput input, IEnumerable<DamageRow> dealtRows)
        {
            var dealt = DealtRows(dealtRows);
            var foes = dealt.GroupBy(r => r.Other).Select(g => (key: g.Key, v: g.Sum(r => (double)r.Amount)))
                            .OrderByDescending(x => x.v).ThenBy(x => x.key, StringComparer.Ordinal).Take(FoeTop)
                            .Select(x => (x.key, data: FoeOf(input, x.key))).Where(x => x.data != null).ToList();
            if (foes.Count == 0) return null;
            var byType = dealt.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Sum(r => (double)r.Amount));
            var rows = new List<Block>();
            foreach (var t in BattleTypes)
            {
                var chips = new List<Block>();
                foreach (var (tone, rel) in new[] { ("strong", Weakness), ("weak", Resistance), ("none", Immunity) })
                    foreach (var f in foes.Where(f => Relation(f.data.Modifiers.TryGetValue(t, out var m) ? m : null) == rel))
                        chips.Add(new Block { Kind = "foe", Id = f.key, Title = Who(input, f.key), Tone = tone });
                var total = Get(byType, t);
                if (total <= 0 && chips.Count == 0) continue;
                rows.Add(new Block { Kind = "dmgtype", Id = t, Title = TypeName(t), Icon = DamageIcon(t), Colour = DamageColour(t), Value = total > 0 ? N(total) : "", Src = SrcPc, Source = TagMeasured, Items = chips });
            }
            if (rows.Count == 0) return null;
            // one real row said as a sentence, the first weakness (else resistance, else immunity) in the table's order
            var ex = new[] { ("strong", WeaknessKey), ("weak", ResistanceKey), ("none", ImmunityKey) }
                .Select(k => (k.Item2, row: rows.FirstOrDefault(r => r.Items.Any(c => c.Tone == k.Item1)), k.Item1)).FirstOrDefault(x => x.row != null);
            var note = (input.IsSelf ? FoeTypesNote : FoeTypesNote.Replace("you have", "they have")) + (ex.row == null ? "" : " " + FoeTypesExample(ex.row.Items.First(c => c.Tone == ex.Item3).Title, ex.Item1, ex.row.Title));
            return new Block { Kind = "foetypes", Note = note, Src = SrcPc, Source = TagMeasured, Items = rows };
        }

        // ---------- lifetime counts in two layers (K1): the game's counter when Hearthwoven first ran + counted since ----------

        /// <summary>A battle count in two layers (LocalTotals.Layers): before = the game's counter <paramref name="stat"/>
        /// ("EnemyHits", "PlayerHits", "Deaths") when Hearthwoven first ran for this character, exact = what Hearthwoven
        /// counted of it since install (SessionEvents.Battle, from your own side: every hit, also on a foe another PC owns).</summary>
        public static (double before, double exact) BattleLayers(PanelInput input, string stat)
        {
            double exact = input?.Events?.Battle != null && input.Events.Battle.TryGetValue(stat, out var e) ? e : 0;
            return LocalTotals.Layers(input?.Baseline, input?.ExactAtBaseline, LocalTotals.BattleKind, stat, exact, input == null ? 0 : C(input, stat));
        }

        /// <summary>The lifetime numbers as a hero, each its total with the two layers (Faded/Solid, only when it has both);
        /// Src "character" when part of it is the game's count, "pc" when all of it was counted since install. Nothing added
        /// when all are 0. A note under it says what faded means, only when a number is drawn in layers.</summary>
        static Block LifetimeHero(PanelView view, PanelInput input, params (string stat, string one, string many)[] stats)
        {
            var layers = stats.Select(s => (s, l: BattleLayers(input, s.stat))).Where(x => x.l.before + x.l.exact > 0).ToList();
            if (layers.Count == 0) return null;
            var hero = Hero(layers.Select(x => { var t = x.l.before + x.l.exact; return (N(t), t == 1 ? x.s.one : x.s.many, x.l.before > 0 ? SrcCharacter : SrcPc, (string)null); }).ToArray());
            var numbers = new[] { hero }.Concat(hero.Items ?? new List<Block>()).ToList();
            for (int k = 0; k < layers.Count; k++)
                if (layers[k].l.before > 0 && layers[k].l.exact > 0) { numbers[k].Faded = N(layers[k].l.before); numbers[k].Solid = N(layers[k].l.exact); numbers[k].FadedTag = true; }
            view.Blocks.Add(hero);
            // the key "faded = before install" sits right under the faded part of the number (Block.FadedTag); the note stays on the page, Tone "tag" (the renderers skip it)
            if (numbers.Any(n => n.Faded != null)) view.Blocks.Add(new Block { Kind = "note", Text = FadedKey, Tone = "tag" });
            return hero;
        }

        public const string HitsOnFoes = "hits on foes", HitOnFoes = "hit on foes", HitsOnPlayers = "hits on other players", HitOnPlayers = "hit on other players";
        public const string HitsReceived = "Hits received", FromFoes = "From foes", FromPlayers = "From other players", DeathWord = "death", DeathsWord = "deaths";

        /// <summary>
        /// Foes in the chosen window (HISTORY-06.md). All: the lifetime line (foes defeated, your character's count; your hits in two layers)
        /// and the damage since install. A day: the window's copy (<paramref name="src"/>): foes defeated by the game counter's growth those
        /// days, your hits as Hearthwoven counted them, the window's damage rows. 10 min .. Session: the session's log (foe, weapon skill and
        /// type per hit); foes defeated has no times there, so only your hits in the window lead.
        /// </summary>
        static void BattleFoes(PanelInput input, PanelInput src, PanelView view, PanelState state, string page, TimeWindow w)
        {
            view.Heading = "Foes"; view.HeadingSource = null;
            var at = view.Blocks.Count;
            List<DamageRow> rows;
            if (w == TimeWindow.SinceInstall)
            {
                var kills = C(input, "EnemyKills");
                // your hits, lifetime: the game's EnemyHits when Hearthwoven first ran (faded) + every hit counted since (solid);
                // the game's own counter misses hits on foes another player's PC owns (owner trap, GAME-METRICS 0.4). One compact line:
                // the foes defeated lead, the hits follow (the table is what the page is for)
                var hits = LifetimeHero(view, input, ("EnemyHits", HitOnFoes, HitsOnFoes), ("PlayerHits", HitOnPlayers, HitsOnPlayers));
                if (kills > 0)
                {
                    var lead = new Block { Kind = "hero", Value = N(kills), Title = kills == 1 ? "foe defeated" : "foes defeated", Src = SrcCharacter, Source = TagCharacter, Tone = Compact };
                    if (hits != null) { var rest = new List<Block>(hits.Items ?? new List<Block>()); hits.Kind = "number"; hits.Items = null; rest.Insert(0, hits); lead.Items = rest; view.Blocks.RemoveAt(at); }
                    view.Blocks.Insert(at, lead);
                    if (hits == null) lead.Tone = Compact;
                }
                else if (hits != null) hits.Tone = Compact;
                if (hits != null && hits.Faded != null)   // the faded part is the game's own count: say what it misses, beside its key
                {
                    var key = view.Blocks.FindIndex(b => b.Kind == "note" && b.Text == FadedKey);
                    view.Blocks.Insert(key >= 0 ? key + 1 : view.Blocks.Count, new Block { Kind = "note", Text = FoeHitsNote });
                }
                rows = DamageSinceInstallRows(input);
            }
            else
            {
                // the window's own numbers: never a lifetime layer (nothing is faded in a window)
                var day = IsDayWindow(w);
                var kills = day ? C(src, "EnemyKills") : 0;   // the game's kill counter has days, not minutes
                double hitsOnFoes = day ? (src.Events?.Battle != null && src.Events.Battle.TryGetValue("EnemyHits", out var eh) ? eh : 0) : HitsIn(input.Log, w, input.NowUtc, true);
                var numbers = new List<(string value, string label, string src, string note)>();
                if (kills > 0) numbers.Add((N(kills), kills == 1 ? "foe defeated" : "foes defeated", SrcCharacter, (string)null));
                if (hitsOnFoes > 0) numbers.Add((N(hitsOnFoes), hitsOnFoes == 1 ? HitOnFoes : HitsOnFoes, SrcPc, (string)null));
                if (numbers.Count > 0) { var hero = Hero(numbers.ToArray()); if (hero != null) { hero.Tone = Compact; view.Blocks.Add(hero); } }
                rows = day ? DamageSinceInstallRows(src) : Damage(input.Log, w, "", input.NowUtc);
            }
            var table = FoeTable(input, rows);
            if (table == null) view.Blocks.Add(w == TimeWindow.SinceInstall ? SinceInstallEmpty(input, "battles", BattleNext) : WindowEmpty(input, w));
            else
            {
                var types = FoeTypes(input, rows);
                Switch(view, state, page, "view", DealtCaption, ("foe", ByFoe, new List<Block> { table }), ("type", ByDamageType, types == null ? null : new List<Block> { types }));
            }
            if (src.Window != null && src.Window.Clipped) view.Blocks.Add(new Block { Kind = "note", Text = ClippedLine });
            Plate(view, "ui:chapter-battle", PlateText(input, w == TimeWindow.SinceInstall && input.DamageSinceInstall != null, windowed: true));
        }

        // ---------- Defense ----------

        /// <summary>Damage received per source, most first, stacked by damage type (tool damage left out), after your armour.</summary>
        public static Block ReceivedSources(PanelInput input, IEnumerable<DamageRow> rows)
        {
            var taken = rows.Where(r => r.Dir == "taken" && r.Amount > 0 && ReceivedTypes.Contains(r.Type)).ToList();
            var bySource = taken.GroupBy(r => r.Other).Select(g => (key: g.Key, total: g.Sum(r => (double)r.Amount), types: g.GroupBy(r => r.Type).ToDictionary(x => x.Key, x => x.Sum(r => (double)r.Amount))))
                                .OrderByDescending(x => x.total).ThenBy(x => x.key, StringComparer.Ordinal).Take(RowTop).ToList();
            if (bySource.Count == 0) return null;
            var max = bySource[0].total;
            return new Block
            {
                Kind = "sources", Title = ReceivedBySource, Text = AfterOf(input), Src = SrcPc, Source = TagMeasured,
                Items = bySource.Select(s => new Block
                {
                    Kind = "source", Id = s.key, Icon = TrophyOf(input, s.key), Title = Who(input, s.key), Value = N(s.total), Fraction = (float)(s.total / max), Src = SrcPc, Source = TagMeasured,
                    Items = Parts(s.types, s.total),
                }).ToList(),
            };
        }

        // the parts of one total by damage type, in the palette's order (raw damage last)
        static List<Block> Parts(Dictionary<string, double> byType, double total) =>
            ReceivedTypes.Where(t => Get(byType, t) > 0).Select(t => new Block
            {
                Kind = "part", Id = t, Icon = DamageIcon(t), Title = TypeName(t), Value = N(Get(byType, t)), Fraction = (float)(Get(byType, t) / total), Colour = DamageColour(t),
            }).ToList();

        /// <summary>
        /// Defence in the chosen window (HISTORY-06.md). Blocks and parries: All (since install), a day (the window's rows) and Session (this
        /// session's own tally); 10 min .. 3 h have none (the block hook keeps no times), one line says so. Received from: the totals, the
        /// day rows, or the session's log in the window. Hits received (your character's count): All and the days. Base defences: All only
        /// ("most defences standing at once" is a high-water mark, not a count a window can split).
        /// </summary>
        static void BattleDefense(PanelInput input, PanelInput src, PanelView view, TimeWindow w)
        {
            var day = IsDayWindow(w);
            var ev = w == TimeWindow.SinceInstall ? input.Events : day ? src.Events : w == TimeWindow.Session ? input.SessionOnly : null;
            var received = w == TimeWindow.SinceInstall ? DamageSinceInstallRows(input) : day ? DamageSinceInstallRows(src) : Damage(input.Log, w, "", input.NowUtc);
            view.Heading = "Defence"; view.HeadingSource = (ev?.Blocks ?? 0) > 0 || received.Count > 0 ? TagMeasured : null;
            if ((ev?.Blocks ?? 0) > 0)
                view.Blocks.Add(new Block
                {
                    // the game's held blocks include the parries: the blocks number is the ones that were not parries
                    Kind = "guard", Value = N(Math.Max(0, ev.Blocks - ev.Parries)), Title = Math.Max(0, ev.Blocks - ev.Parries) == 1 ? "block" : "blocks",
                    Value2 = N(ev.Parries), Text = ev.Parries == 1 ? "parry" : "parries", Src = SrcPc, Source = TagMeasured,
                });
            else if (ev == null && w != TimeWindow.SinceInstall) view.Blocks.Add(new Block { Kind = "note", Text = NoDaysBlocks });
            Add(view, ReceivedSources(input, received));
            if (received.Count == 0 && (ev?.Blocks ?? 0) == 0 && w != TimeWindow.SinceInstall) view.Blocks.Add(WindowEmpty(input, w));
            // hits received: the game counts them on your own character (Character.ApplyDamage), complete, so its counter stands alone
            // (no layer); in a day window its growth those days
            if (w == TimeWindow.SinceInstall || day)
                Group(view, HitsReceived, Rows(Counters(day ? src : input, ("HitsTakenEnemies", FromFoes), ("HitsTakenPlayers", FromPlayers)), k => k, k => "", SourceCharacter, keepOrder: true));
            if (w == TimeWindow.SinceInstall)
            {
                var defences = Rows(Counters(input, ("BuildClusterDefense", MostDefences), ("TrapArmed", "Traps armed"), ("TurretAmmoAdded", "Turrets loaded")), k => k, k => k == MostDefences || k == "Traps armed" ? "vocab:defence-built" : "", SourceCharacter, byName: false);
                if (defences != null) { view.Blocks.Add(Section("Base defences")); view.Blocks.Add(defences); }
            }
            Plate(view, "ui:chapter-battle", PlateText(input, w == TimeWindow.SinceInstall && input.DamageSinceInstall != null, windowed: true));
        }

        // ---------- Deaths ----------

        // one format everywhere, always dated ("8 Oct 00:10"): "today 00:10" read as a different kind of time than Tor's page and the death rows after midnight
        static string DeathTime(PanelInput input, DateTime utc) => Local(input, utc).ToString("d MMM HH:mm", Inv);

        /// <summary>"Where you fell": the biomes found (FoundBiomes: the game's record plus any damage or death on record), in journey order with the Ocean last;
        /// a tile is wider where you fell more (1 + 0.9 per death), with a death mark and the time per fall above it.</summary>
        public static Block DeathStrip(PanelInput input, List<EventLog.Death> deaths, ICollection<string> chosen = null)
        {
            var seen = new HashSet<string>(FoundBiomes(input));   // one source with the Battle strip and Maps: the game's record plus evidence
            foreach (var d in deaths) seen.Add(d.Biome);
            var tiles = new List<Block>();
            foreach (var t in BiomeTiles)
            {
                if (!seen.Contains(t.key)) continue;
                var here = deaths.Where(d => d.Biome == t.key).ToList();
                tiles.Add(new Block
                {
                    Kind = "biome", Id = t.key, Title = BiomeName(t.key), Colour = t.colour, Tone = t.key == "Meadows" || t.key == "Plains" ? "dark-text" : "light-text",
                    Count = here.Count, Fraction = 1f + 0.9f * here.Count, Src = SrcPc, Source = TagMeasured,
                    Selected = chosen != null && chosen.Contains(t.key),   // the Biome row's choice: the renderer lights it and steps the others back, as on the overview
                    // a fall counted per biome only (since install) has no time: it is in Count, not listed
                    Items = here.Where(d => d.Time != default(DateTime)).Select(d => new Block { Kind = "death", Value = DeathTime(input, d.Time), Src = SrcPc, Source = TagMeasured }).ToList(),
                });
            }
            return tiles.Count == 0 ? null : new Block { Kind = "deathstrip", Title = input.IsSelf ? WhereYouFell : "Where " + Name(input) + " fell", Note = input.IsSelf ? WiderNote : "wider where " + Name(input) + " fell more", Src = SrcPc, Source = TagMeasured, Items = tiles };
        }

        /// <summary>The list's caption: how many falls and in what order ("3 falls, newest first"), so a list that scrolls says how long it is.</summary>
        public static string DeathsCaption(int falls, int of = 0) => of > falls ? "Listed: this session's " + falls + " of " + of + " falls, newest first" : falls + (falls == 1 ? " fall" : " falls") + ", newest first";
        /// <summary>What each bar shows, quietly after the caption: "last 30 seconds before each fall, after your armour".</summary>
        public static string DeathsQualifier(Block list)
        {
            var title = (list.Title ?? "").ToLowerInvariant().Replace("death", "fall");
            return (title.StartsWith("last", StringComparison.Ordinal) ? title + " before each fall" : title) + ", " + list.Text;
        }

        /// <summary>Per death, newest first: the killer (its trophy), when and where, and the last 10 seconds as totals by type.</summary>
        public static Block DeathList(PanelInput input, List<EventLog.Death> deaths)
        {
            if (deaths.Count == 0) return null;
            var items = new List<Block>();
            var shown = deaths.Take(BattleDeathTop).ToList();
            List<EventLog.Hit> Hits(EventLog.Death d) => d.Timeline.Where(h => h.Amount > 0 && ReceivedTypes.Contains(h.Type)).OrderByDescending(h => h.Ago).ToList();
            var largest = shown.SelectMany(Hits).Select(h => (double)h.Amount).DefaultIfEmpty(0).Max();
            foreach (var d in shown)
            {
                if (d.Timeline.Count > 0)
                {
                    // how the damage built up over the last 30 s: each hit where it fell in time, as wide as it hurt
                    var hits = Hits(d); var sum = hits.Sum(h => (double)h.Amount);
                    items.Add(new Block
                    {
                        Kind = "death", Tone = "timeline", Id = d.Killer, Icon = TrophyOf(input, d.Killer), Title = Who(input, d.Killer), Text = DeathTime(input, d.Time) + " · " + BiomeName(d.Biome),
                        Value = sum > 0 ? N(sum) : "", Note = LastThirtyShort, Src = SrcPc, Source = TagMeasured,
                        Items = hits.Select(h => new Block
                        {
                            Kind = "hit", Id = h.Type, Icon = DamageIcon(h.Type), Title = TypeName(h.Type), Text = Who(input, h.Source), Value = N(h.Amount), Colour = DamageColour(h.Type),
                            Fraction = Math.Max(0f, Math.Min(1f, 1f - h.Ago / EventLog.TimelineSeconds)), Fraction2 = largest > 0 ? (float)(h.Amount / largest) : 0,
                        }).ToList(),
                    });
                    continue;
                }
                var byType = new Dictionary<string, double>();
                foreach (var kv in d.Last10s) { var t = kv.Key.Substring(kv.Key.LastIndexOf('|') + 1); if (ReceivedTypes.Contains(t) && kv.Value > 0) Bump(byType, t, kv.Value); }
                var total = byType.Values.Sum();
                items.Add(new Block
                {
                    Kind = "death", Id = d.Killer, Icon = TrophyOf(input, d.Killer), Title = Who(input, d.Killer), Text = DeathTime(input, d.Time) + " · " + BiomeName(d.Biome),
                    Value = total > 0 ? N(total) : "", Note = LastTenShort, Src = SrcPc, Source = TagMeasured, Items = total > 0 ? Parts(byType, total) : new List<Block>(),
                });
            }
            var timelines = items.Count(i => i.Tone == "timeline");
            if (timelines == 0 || timelines == items.Count) foreach (var i in items) i.Note = null;   // the caption says which; a mixed list keeps the words under each bar
            var title = timelines == items.Count ? LastThirty : timelines == 0 ? LastTenSeconds : BeforeEachDeath;
            return new Block { Kind = "deaths", Title = title, Text = AfterOf(input), Src = SrcPc, Source = TagMeasured, Items = items };
        }

        /// <summary>The overview's deaths in the window: one row per killer and damage type (DominantType: never pinned on an
        /// attacker when a damage-over-time ended it, the killer is whoever set it on you), most first, then newest.</summary>
        public static Block DeathRows(PanelInput input, List<EventLog.Death> deaths)
        {
            var groups = deaths.GroupBy(d => (killer: d.Killer ?? "unknown", type: DominantType(d)))
                               .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Max(d => d.Time)).Take(DeathTop).ToList();
            if (groups.Count == 0) return null;
            var items = groups.Select(g =>
            {
                var type = g.Key.type; var trophy = TrophyOf(input, g.Key.killer); var typeIcon = DamageIcon(type);
                return new Block
                {
                    Kind = "death", Id = g.Key.killer, Icon = trophy.Length > 0 ? trophy : typeIcon.Length > 0 ? typeIcon : "vocab:death",
                    Colour = trophy.Length == 0 && typeIcon.Length > 0 ? DamageColour(type) : null, Value = N(g.Count()),
                    Title = (g.Count() == 1 ? DeathWord : DeathsWord) + (type.Length > 0 ? " from " + TypeName(type).ToLowerInvariant() : ""),
                    Text = string.Join("  ·  ", g.OrderByDescending(d => d.Time).Take(3).Select(d => DeathTime(input, d.Time)).ToArray()), Src = SrcPc, Source = TagMeasured,
                };
            }).ToList();
            return new Block { Kind = "deathrows", Src = SrcPc, Source = TagMeasured, Items = items };
        }

        static void BattleDeaths(PanelInput input, PanelInput src, PanelView view, PanelState state, List<EventLog.Death> deaths, TimeWindow w)
        {
            // All and a day window count the falls per biome (the totals, the day rows: src); the list is this session's log in the window
            var since = w == TimeWindow.SinceInstall || IsDayWindow(w);
            view.Heading = deaths.Count == 0 && !(since && src.BiomeSinceInstall != null && src.BiomeSinceInstall.Deaths.Count > 0) ? NoDeaths : "Deaths"; view.HeadingSource = null;   // the strip and the list carry the counts
            // one quiet lifetime line, not this window's: the game's Deaths when Hearthwoven first ran (faded) + every death
            // recorded since install (solid); the strip and the list below stay the chosen window of this session's log
            if (!IsDayWindow(w)) LifetimeHero(view, input, ("Deaths", DeathWord, DeathsWord));   // a day window: the strip counts that window's falls; a lifetime line there would sit under the window's name
            // since install the strip counts every fall per biome (BiomeTally, no times); the list can only be this session's, and says so above it
            var counted = since && src.BiomeSinceInstall != null && !src.BiomeSinceInstall.Empty && src.BiomeSinceInstall.Deaths.Count > 0 ? BiomeDeaths(src.BiomeSinceInstall) : null;
            // the Biome row (the same pattern as the Overview's tiles and Damage): the strip keeps every biome with the chosen ones lit, the list narrows to them
            var strip = counted ?? deaths;
            var biomeBar = DeathsBiomeBar(input, state, strip);
            var chosen = biomeBar == null ? new List<string>() : Chosen(state, BattleDeathsFilter, "biome");
            Add(view, DeathStrip(input, strip, chosen));
            Add(view, biomeBar);   // under the strip, as on the overview
            var listed = InBiomes(deaths, chosen);
            // fix4: the list's own heading says it ("Listed: this session's 3 of 4 falls"): Count = every fall counted in the chosen biomes
            var of = since && counted != null ? InBiomes(strip, chosen).Count : listed.Count;
            var list = DeathList(input, listed);
            if (since && list == null && of > 0) view.Blocks.Add(new Block { Kind = "note", Text = DeathsAllNote });   // nothing to list this session: the falls are only counted
            if (chosen.Count > 0 && strip.Count > 0 && !InBiomes(strip, chosen).Any()) view.Blocks.Add(new Block { Kind = "note", Text = "No falls in " + string.Join(" and ", chosen.OrderBy(BiomeRank).Select(BiomeName)) });
            if (list != null) list.Count = of;
            Add(view, list);
            Plate(view, "ui:chapter-battle", PlateText(input, w == TimeWindow.SinceInstall, windowed: true));
        }
    }
}
