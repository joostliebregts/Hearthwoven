using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    public enum Chapter { Deeds, Company, Stores, Battle, Voyages, Skills, Feats }   // Feats last in the enum (its number stays put); its tab sits after Deeds (ChapterRow, StepChapter)
    /// <summary>Battle's time windows, shortest first; SinceInstall is the total this PC folded (LocalTotals), which has no biome and no time.</summary>
    /// <summary>One window set for the panel (HISTORY-06.md): the session's event log (10 min .. Session), the day history (Today, 7 days,
    /// 30 days: local calendar days) and All (since install). Chosen by name (Enum.Parse), never stored as a number.</summary>
    public enum TimeWindow { LastTenMinutes, LastThirtyMinutes, LastHour, LastThreeHours, Session, Today, SevenDays, ThirtyDays, SinceInstall }

    public class PanelState
    {
        public Chapter Chapter = Chapter.Deeds;
        public readonly Dictionary<Chapter, string> Page = new Dictionary<Chapter, string>();   // chosen left-list entry per chapter
        public TimeWindow Window = TimeWindow.Session;      // the chosen time window of every page that has window chips (WindowsOf, HISTORY-06.md)
        /// <summary>The player chose a window (a chip, or the view key cycling them). Until then the Deeds pages show All: the book's default
        /// Session is Battle's, and a Deeds page opened for the first time shows the whole record (PanelModel.Deeds).</summary>
        public bool WindowPicked;
        /// <summary>A greyed day window that was pressed (B17) holds only on the page where it was pressed: WaitAt is that page, LastWorking the
        /// last chosen window that was open, and leaving the page (or closing the book) returns Window to it (PanelModel.SettleWait). The same
        /// for Together's window switch: WaitView is its id, LastWorkingView its last open choice per switch.</summary>
        public TimeWindow LastWorking = TimeWindow.Session;
        public PanelPlace WaitAt;
        public string WaitView;
        public readonly Dictionary<string, string> LastWorkingView = new Dictionary<string, string>();
        public string Player = "";                          // "" = yourself; otherwise a fellow player's name
        /// <summary>The Everyone chip is on (0.8, Chapters/EveryoneModel.cs): every page with a group view shows the group's numbers; with Player
        /// "" (you). One flag for the whole book, kept between sessions (PanelPrefs): it answers "whose book" like the names beside it.</summary>
        public bool Everyone;
        public bool TheyReceived = true;                    // Company: true = "They enjoyed your food", false = "You enjoyed their food"
        public bool ShowAbout;                              // the one "About Hearthwoven" page is open (InfoKey)
        public string Hotkey = "H";
        public string InfoKey = "T";                        // opens and closes the About page; T: bound by no mod in the group's client profile (I opens AdventureBackpacks)
        public string AboutPage = "counts";                 // the About page's own list: How it counts, What it reads, Sharing (PanelModel.AboutList)
        /// <summary>The chosen view of each view switch, by the switch's Id ("Skills/overview/view"), so every page keeps its
        /// own choice while the panel lives (chapter and page choices are kept the same way).</summary>
        public readonly Dictionary<string, string> View = new Dictionary<string, string>();
        public string FilterKey = "K";                      // enters and leaves the filter focus on a page with a filter bar (FacetModel.cs); K: Panel.FilterKey (G clashed with ZenDragon's radial menu; Tab closes the book)
        /// <summary>The chips chosen in each facet row of a filter bar, by "filter id|facet id", in the order they were chosen
        /// (FacetModel.cs). Every page keeps its own while the panel lives.</summary>
        public readonly Dictionary<string, List<string>> Facets = new Dictionary<string, List<string>>();
        public int FilterRow = -1, FilterCursor;            // the filter focus: the row the keys act on (-1 = not in the focus) and the chip under the cursor
        /// <summary>The filter bars opened by a click on their header (FacetModel.cs), by filter id; a bar is collapsed to one line otherwise, and open while the focus is in.</summary>
        public readonly HashSet<string> OpenFilters = new HashSet<string>();
        public string ViewKey = "F";                        // flips the page's view switch; F: bound by no mod in the group's client profile, nor by the panel
        /// <summary>The page's "About these numbers" box is open (the numbers key, or its button); kept across pages until pressed again or the panel closes.</summary>
        public bool ShowNumbers;
        public string NumbersKey = "Y";                     // shows and hides "About these numbers" (Panel.NumbersKey); Y: no game default, no group mod binds it
        public string BookKey = "B";                        // 0.8: steps whose book the page shows, as the player row's chips do (Panel.BookKey; StepBook)
        public string FeatSel = "";                         // the Feats chapter: the feat the detail area shows (mouse hover, A/D); "" = the first card of the view
        public string PageOf(Chapter c) => Page.TryGetValue(c, out var p) ? p : null;
        public readonly List<PanelPlace> History = new List<PanelPlace>();   // the back stack while the panel is open (PanelNav.cs)
        public PanelPlace Here;                                              // the page shown now
        public bool GoingBack;                                               // the next Visited is the Back itself: no push
        /// <summary>Since you were away (Chapters/AwayModel.cs): the session whose first opening already decided whether the book opens there by
        /// itself; where the book was before it did (LeaveAway puts it back), and Company's page then.</summary>
        public string AwayDecidedFor;
        public PanelPlace AwayReturn;
        public string AwayCompanyBefore;
        /// <summary>0.8 Compare periods (Chapters/CompareModel.cs): the Compare chip is on, on every page that offers it (kept in PanelPrefs).
        /// CompareTip: the greyed chip's reason is showing (the preview's picture of the pointer on it; in game the pointer shows it).</summary>
        public bool Compare, CompareTip;
    }

    /// <summary>
    /// One thing to draw. Kinds: headline, stat, tiles, bars, rows, titles, thread, section, empty, note, and the visual
    /// vocabulary: composition (Value = total; Items = parts with Fraction = share, Colour, Pattern), biomes (Items = one tile
    /// per biome with evidence, journey order, Ocean last and apart), ladders (Items = groups Fight/Gather/Move/Make/Other,
    /// each with ladder items) and ladder (one skill, large).
    /// Page layout (slice 3, the prototypes' vocab.css .hrow/.plate/.hero2, r4battle's view switch, r4over's deed cards):
    /// plate (the page on the dark recessed plate: Title and Icon make the heading row, Pill and PillIcon its pill on the
    /// right, Text one quiet line at the top, Items the page's blocks), hero (Value = the big number, Title its label, Note
    /// a qualifier; Items = further numbers on the right, Kind "number"), columns (Items = Kind "column", each with Items =
    /// blocks, side by side), switch (Id = its key in PanelState.View, Title an optional caption left of the chips; Items =
    /// Kind "view" with Id and Title, the chosen one Selected and the only one with Items) and cards (Items = Kind "card":
    /// Icon + Title on top, Value the big number, Text its label, Items the small lines under it, Id the page a click opens).
    /// Icons are references the UI resolves to sprites: "item:Bread" or "item:$item_bread" (the game's item sprite),
    /// "piece:Cart", "skill:Blocking", "status:poison", "title:cook" (Hearthwoven art), "person:Edda"; anything that does
    /// not resolve shows its label's initial.
    /// </summary>
    public class Block
    {
        public string Kind, Id, Icon, Title, Value, Text, Note, Tone;
        /// <summary>Where the numbers come from, machine-readable for the source marks: "character" (the game's own counters
        /// since the character was made), "measured" (Hearthwoven on this PC, or the player's own shared record), "fellows"
        /// (recorded on fellow players' PCs). Items inherit their block's source.</summary>
        public string Source;
        public float Fraction;                              // bars: value / largest value on the same scale
        public bool Selected;
        public List<Block> Items;

        // ---------- the visual vocabulary (contract, 2026-10-08; work/hearthwoven-visual-vocabulary/VOCABULARY.md) ----------
        /// <summary>Where a number comes from, as data: "character" (the game's counters, kept with the character), "pc"
        /// (Hearthwoven on this PC since install), "fellows" (fellow players' PCs). Set on every block and item that shows a
        /// number (TagSources fills it from Source); null = no number. It replaces the old source sentences ("since this
        /// character was made"). The panel draws no icon for it (Joost 2026-10-08): only "pc" shows, as the "since install"
        /// label placed by SinceInstall.</summary>
        public string Src;
        /// <summary>composition: the segment colour and its key swatch; biomes: the tile colour. "#rrggbb".</summary>
        public string Colour;
        /// <summary>composition: a fill sprite tiled over the segment and its swatch ("vocab:grain-wood"); null = flat colour.</summary>
        public string Pattern;
        /// <summary>biomes: the received bar (hangs under the tile), 0..1 on the block's shared scale. Fraction is the dealt bar (rises).</summary>
        public float Fraction2;
        /// <summary>biomes: received, formatted (Value is dealt); on the strip block itself Value and Value2 are the window's
        /// totals its legend shows (slice 3). ladder: the next level ("39").</summary>
        public string Value2;
        /// <summary>biomes: deaths in this biome (the death mark with this count; 0 = none).</summary>
        public int Count;
        /// <summary>ladder, ladders: level now, whole levels 0..100.</summary>
        public float Level;
        /// <summary>ladder, ladders: progress to the next level, 0..1; -1 = not known (draw no progress then).</summary>
        public float Progress = -1;
        /// <summary>view (Together's window chips): greyed (Tone OffTone), but a day window of your own book that works later, so pressing it
        /// chooses it and one line says when (B17, WaitLine); the keys still skip it.</summary>
        public bool Waits;
        /// <summary>ladder, ladders: practised since install on this PC (the soft glow at the climber).</summary>
        public bool Practised;
        /// <summary>filterbar: the key that enters its focus ("Tab"), drawn as a small keycap beside the rows; null = none.</summary>
        public string KeyCap;
        /// <summary>filterbar: the chip rows (and the linked bars) are shown: the focus is in, or the header was opened. false = collapsed to its header line.</summary>
        public bool Open;
        /// <summary>ranking: rows side by side (2 = two columns); 0 or 1 = one column.</summary>
        public int Columns;
        /// <summary>plate: the pill on the right of the heading row (the page's title badges, "Woodcutter") and its icon; null = none.</summary>
        public string Pill, PillIcon;

        // ---------- 0.7 redesign (work/hearthwoven-0.7/REDESIGN-RULES.md; RecordedModel.cs) ----------
        /// <summary>A Src "pc" number's start when it is not the install (a 0.6 counter group, a later baseline); null = StartOf(input, null).
        /// PlaceRecordedFrom dates its label from this.</summary>
        public DateTime? From;
        /// <summary>The label drawn after this heading or number: "Recorded from 8 October · this PC" on a section, block or page part,
        /// "from 8 October" after a single number. Set by PlaceRecordedFrom only (a page with view.Recorded).</summary>
        public string RecordedFrom;
        /// <summary>Value is NotRecorded ("Not recorded"): the counter did not run for this period or person. Drawn muted, italic, at the
        /// label's size, never gold; never "0".</summary>
        public bool Unrecorded;

        // ---------- 0.8 Compare periods (Chapters/CompareModel.cs) ----------
        /// <summary>While comparing: the number the period before held, as shown ("86"), and the change mark ("▲ 12 %", "new", "same"); on a hero
        /// number and a cap's total BeforeLabel says what the period before is ("the 7 days before"). On a compare row (Kind "row" of
        /// "comparerows") Value is now, Before the period before, Fraction and Fraction2 their lengths on the block's one scale.</summary>
        public string Before, Change, BeforeLabel;
        /// <summary>switch: a chip that sits after its chips, not one of its views (Compare beside Together's window chips): Selected = on, Tone
        /// OffTone = greyed with Text its reason, Open = the reason's tag is showing.</summary>
        public Block Chip;
    }

    public class Choice { public string Id, Label, Icon; public bool Selected, Disabled; public bool Dot; /* a gold dot: something new to see (earned feats not opened yet) */ public bool Waits; /* a greyed day window of your own book that works later: pressable, its WaitLine says when (B17) */ public string Why; /* a greyed window chip's own reason, under it on hover (PageHead.cs) */ }   // Disabled: drawn dim, does nothing (the biome chip where a page has no biomes)

    public class PanelView
    {
        public string Title = "Hearthwoven", Owner, Scope, Heading, ShareNote, ListTitle;
        public string HeadingSource;                        // source tag of a number in the heading (see Block.Source)
        public string HeadingSrc;                           // the heading's source mark: "character" | "pc" | "fellows" (see Block.Src)
        public Chapter Active;
        public string Page;
        public bool HasFilters, ShowAbout;
        /// <summary>A windowed Battle page: the chosen window in full ("last 30 minutes"), said after the heading because the chips say it short (10 min, 1 h, All).</summary>
        public string HeadingWindow;
        /// <summary>The window this page shows (WindowChips): the chosen one, or All when the page does not offer it; null = no window chips.</summary>
        public TimeWindow? ShownWindow;
        /// <summary>The page shows a chosen time window of the logs, not counts since install (Company's Together, Damage dealt): no "since install" label.</summary>
        public bool Windowed;
        public readonly List<Choice> Chapters = new List<Choice>(), List = new List<Choice>(), Badges = new List<Choice>(), Toggle = new List<Choice>(),
                                     Windows = new List<Choice>(), Biomes = new List<Choice>(), Players = new List<Choice>();
        /// <summary>A 0.7 page (REDESIGN-RULES.md): no zones, no "since install" label; PlaceRecordedFrom dates the numbers counted on
        /// this PC instead. Each page sets it when it migrates, so pages move one at a time.</summary>
        public bool Recorded;
        /// <summary>The page's "About these numbers" box (Kind "aboutnumbers", RecordedModel.AboutNumbers); null = the page has none.</summary>
        public Block AboutNumbers;
        /// <summary>The label after the page heading when every number on the page was counted on this PC ("Recorded from 8 October · this PC").</summary>
        public string HeadingRecordedFrom;
        public readonly List<Block> Blocks = new List<Block>();
        public readonly List<string> Keys = new List<string>();
        public readonly Dictionary<string, int> PersonColors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // name -> palette index
        /// <summary>0.8 (Chapters/EveryoneModel.cs): the Everyone chip shows at the end of the player row (EveryoneChip; sharing on), the page
        /// shows the group (EveryoneOn), or the chip is greyed here with its second line and its reason (EveryoneSub, EveryoneWhy).</summary>
        public bool EveryoneChip, EveryoneOn;
        public string EveryoneSub, EveryoneWhy;
        /// <summary>The greyed chip's press: the page it opens with Everyone on ("Battle/damage"); null = nowhere (nobody else shares yet).</summary>
        public string EveryoneTo;
        /// <summary>0.8: the Compare chip after the window chips (Chapters/CompareModel.cs); null = the page offers none. Disabled = greyed, with
        /// CompareWhy its reason (the tag on hover; CompareTip: shown now). Comparing: the page is merged with the period before.</summary>
        public Choice Compare;
        public string CompareWhy;
        public bool CompareTip, Comparing;
        /// <summary>0.8 layout D+ (PageHead.cs): why all the greyed window chips are greyed (a fellow's copy, the group); each greyed chip's own reason
        /// (Choice.Why: this, then a day window's own day before the history) shows under it while the pointer is on it; WindowTip: the chip whose
        /// reason shows now, the one just pressed (B17's line, no longer on the plate).</summary>
        public string WindowWhy, WindowTip;
        /// <summary>0.8 layout D+ (PageHead.cs): a fellow's book says whose copy it is and when at the strip's right end ("Edda, as of 8 Oct 00:28").</summary>
        public string StripNote;
    }

    /// <summary>
    /// The Hearthwoven panel as a view model: six chapters (Deeds, Company, Stores, Battle, Voyages, Skills), each with a
    /// left list and one page per entry, per the lead decision in work/branding-kritterstats/coordination/CLAUDE-REPLY.md.
    /// One owner page per metric; titles appear as badges on their owner page and as shortcuts in Deeds > Overview.
    /// Every value carries its source: "since this character was made" (the game's profile) or "measured" (Hearthwoven,
    /// this session). Pure C# (no Unity calls), unit-tested in test-panel/. Player-visible text has no em-dashes.
    /// </summary>
    public static partial class PanelModel
    {
        public const string SourceCharacter = "since this character was made";
        public const string SourceSession = "measured this session";
        public const string SourceFellows = "measured on their PCs";
        public const string NotYetRecorded = "Nothing yet";
        public const string CompanyEmpty = "Cook for each other and share gear: it shows up here.";
        /// <summary>The empty state of a section that only counts since install (Joost 2026-10-08): short, with the next step.</summary>
        public const string NothingYet = "Nothing yet";
        public const string NoDeaths = "No deaths yet";
        public const int RowTop = 8, TileTop = 8, DeathTop = 5, HintTop = 2;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------- names ----------

        static readonly string[] BiomeOrder = { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Ocean", "Mistlands", "AshLands", "DeepNorth", "None" };
        public static string BiomeName(string b)
        {
            switch (b)
            {
                case "BlackForest": return "Black Forest";
                case "Mountain": return "Mountains";
                case "AshLands": return "Ashlands";
                case "DeepNorth": return "Deep North";
                case "None": case "": case null: return "Unknown land";
                default: return b;
            }
        }

        static readonly string[] TypeOrder = { "slash", "pierce", "blunt", "chop", "pickaxe", "fire", "frost", "lightning", "poison", "spirit", "damage" };
        public static string TypeName(string t) => t == "damage" ? "Raw" : t.Length == 0 ? t : char.ToUpperInvariant(t[0]) + t.Substring(1);

        // Damage without an attacker: the game's hit type stands in for the source.
        static string CauseName(string cause, bool self = true)
        {
            switch (cause)
            {
                case "Fall": return "A fall";
                case "Drowning": return "Drowning";
                case "Burning": return "Burning";
                case "Freezing": return "Freezing";
                case "Poisoned": return "Poison";
                case "Smoke": return "Smoke";
                case "Water": return "Water";
                case "EdgeOfWorld": return "The edge of the world";
                case "Impact": return "An impact";
                case "Cart": return "A cart";
                case "Tree": return "A falling tree";
                case "Self": return self ? "Yourself" : "Themselves";
                case "Structural": return "Falling timber";
                case "Turret": return "A turret";
                case "Boat": return "A boat";
                case "Stalagtite": return "A stalactite";
                case "Catapult": return "A catapult";
                case "CinderFire": return "Cinder fire";
                case "AshlandsOcean": return "The boiling sea";
                case "AshlandsLava": return "Lava";
                case "Incinerator": return "The incinerator";
                case "DrawBridge": return "A drawbridge";
                case "unknown": case "Undefined": case "?": case "": case null: return "Unknown";
                default: return null;
            }
        }

        /// <summary>A readable name when the game has none to give: "$item_fish_wraps" -> "Fish wraps", "BlobElite" -> "Blob Elite", "Beech1" -> "Beech".</summary>
        public static string Prettify(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return "Unknown";
            var s = prefab.Replace("(Clone)", "").Trim();
            if (s.StartsWith("$")) { var u = s.IndexOf('_'); s = u > 0 ? s.Substring(u + 1) : s.Substring(1); }
            s = s.Replace('_', ' ').TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ' ');
            var b = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && char.IsLower(s[i - 1])) b.Append(' ');
                b.Append(s[i]);
            }
            var r = b.ToString().Trim();
            return r.Length == 0 ? prefab : char.ToUpperInvariant(r[0]) + r.Substring(1);
        }

        // ---------- what a token is: the game data first (PanelInput.*Kind), vanilla names as the fallback ----------

        public const string TagCharacter = "character", TagMeasured = "measured", TagFellows = "fellows";

        /// <summary>
        /// The game's own craft switch (InventoryGui.DoCrafting): consumables are food unless the name says bait; weapons,
        /// shields, armour, tools, utility and trinkets are gear (CraftWeapon/Armor/Tool/Trinket); the rest is "other".
        /// Mirroring it keeps the Crafting headline and its list on the same items. null = not known.
        /// </summary>
        public static string ItemKindOf(string itemType, string token)
        {
            switch (itemType)
            {
                case null: case "": return null;
                case "Consumable": return (token ?? "").ToLowerInvariant().Contains("bait") ? "other" : "food";
                case "OneHandedWeapon": case "Bow": case "Shield": case "Hands": case "TwoHandedWeapon": case "TwoHandedWeaponLeft":
                case "Helmet": case "Chest": case "Legs": case "Shoulder": case "Trinket": case "Utility": case "Tool":
                    return "gear";
                default: return "other";
            }
        }

        /// <summary>A placed piece: hoe and cultivator work (TerrainOp) is groundwork, seeds and saplings (Plant) are planted,
        /// a feast (Feast) is set out; everything else is built.</summary>
        public static string PieceKindOf(bool terrainOp, bool plant, bool feast) => terrainOp ? "ground" : plant ? "planted" : feast ? "feast" : "built";

        /// <summary>
        /// The kinds of one tool's pieces (its piece table, in order). A table that both plants (a seed or sapling with a Plant
        /// component) and works the ground (a TerrainOp) is the cultivator, or a mod's tool like it: everything on it is planted or
        /// groundwork, also what has no Plant component. 0.6.5, Joost's book: PlantEverything puts berry bushes, dandelions,
        /// mushrooms, small trees and debris on the cultivator as pickables without one, so they stood under pieces built and
        /// their seeds and flowers ("Beech Seeds", "Dandelion") as building materials. Any other table: PieceKindOf per piece.
        /// Review 0.6.5: one Plant and one TerrainOp alone made a mod's all-in-one tool "the cultivator" and every building on it
        /// planted; the table now also needs most of its other pieces (not groundwork, not a feast) to be plantings or pickables.
        /// </summary>
        public static string[] PieceKindsOfTable(IList<(bool terrainOp, bool plant, bool feast, bool pickable)> pieces)
        {
            var list = pieces ?? new List<(bool terrainOp, bool plant, bool feast, bool pickable)>();
            var rest = list.Where(p => !p.terrainOp && !p.feast).ToList();
            var growing = rest.Count(p => p.plant || p.pickable);
            var planting = list.Any(p => p.plant) && list.Any(p => p.terrainOp) && growing > rest.Count - growing;
            return list.Select(p => PieceKindOf(p.terrainOp, p.plant || (planting && !p.feast), p.feast)).ToArray();
        }

        /// <summary>
        /// What a prefab's drops count as (B12, Joost's 0.5 test: Coal and Iron showed up as mined). "wood": what a felled log
        /// (TreeLog) splits into. "mining": what a rock, ore vein or pile drops (MineRock, MineRock5, or a Destructible with
        /// drops) that a pickaxe works on (its damage modifiers leave the pickaxe effective; for a Destructible the axe must have
        /// no effect, or every crate would count). Anything else (a standing tree's resin and cones, a crate, a pot): null, not gathered. Mod-agnostic: it reads
        /// the components and modifiers, never names.
        /// </summary>
        public static string GatherKindOf(string component, string chop, string pickaxe)
        {
            bool none(string m) => m == "Immune" || m == "Ignore";
            if (component == "TreeLog") return "wood";
            if ((component == "MineRock" || component == "MineRock5") && !none(pickaxe)) return "mining";   // rocks and veins: the pickaxe works on them
            if (component == "Destructible" && !none(pickaxe) && none(chop)) return "mining";   // a pile only a pickaxe breaks (a crate breaks to anything)
            return null;
        }

        /// <summary>What a pickaxe digs up that is neither stone nor ore (Withered Bone from muddy scrap piles, Chitin from the
        /// Leviathan): its own legend under the Mining bar, never in the stone and ore total (0.6.5, seen on a real character).</summary>
        public const string PickaxeFinds = "pickaxe-finds";
        /// <summary>The heading of that legend.</summary>
        public const string PickaxeFindsTitle = "Other pickaxe finds";

        /// <summary>
        /// An item's gathered kind from the game data (GameData): <paramref name="tableKind"/> is GatherKindOf of the first table
        /// that drops it. A smelter's output is never gathered (B12). A pickaxe drop that a creature drops too is left out
        /// (0.6.5: a real Mining bar held 123 Leather Scraps beside 5 Scrap Iron; boars drop the scraps, and the pickup count
        /// says only the item, never where it lay, so no share of it can be called mining). What a smelter takes in is ore
        /// ("mining"); any other pickaxe drop is a find of its own (PickaxeFinds). The vanilla lists in GatherKind come first.
        /// </summary>
        public static string GatheredItemKind(string tableKind, bool refined, bool creatureDrops, bool smelted)
        {
            if (refined || tableKind == null) return null;
            if (tableKind != "mining") return tableKind;
            if (creatureDrops) return null;
            return smelted ? "mining" : PickaxeFinds;
        }

        // vanilla tokens, used only when the game data gives no answer (PanelUi derives these from drop tables and components)
        static readonly HashSet<string> VanillaGround = new HashSet<string> { "$piece_levelground", "$piece_lowerground", "$piece_raise", "$piece_path", "$piece_pavedroad", "$piece_cultivate", "$piece_replant" };
        static readonly HashSet<string> VanillaWood = new HashSet<string> { "$item_wood", "$item_finewood", "$item_roundlog", "$item_elderbark", "$item_yggdrasilwood", "$item_blackwood", "$item_frostwood" };
        static readonly HashSet<string> VanillaMining = new HashSet<string> { "$item_stone", "$item_copperore", "$item_tinore", "$item_ironscrap", "$item_ironore", "$item_silverore",
            "$item_blackmetalscrap", "$item_obsidian", "$item_crystal", "$item_blackmarble", "$item_flametalore", "$item_grausten", "$item_copperscrap", "$item_bronzescrap" };

        static string Ask(Func<string, string> f, string token) { try { return f?.Invoke(token); } catch { return null; } }
        static string ItemKind(PanelInput i, string token) => Ask(i.ItemKind, token) ?? "other";   // unknown: kept out of gear and food
        // gathering: the vanilla names first (a root or a fossil can drop wood or marble through either component), the game data for mods' items
        static string GatherKind(PanelInput i, string token) => VanillaWood.Contains(token) ? "wood" : VanillaMining.Contains(token) ? "mining" : Ask(i.GatherKind, token);   // game data: GatherKindOf, refined items left out (GameData)
        static string PieceKind(PanelInput i, string token) => Ask(i.PieceKind, token) ?? PieceKindByName(token);
        /// <summary>
        /// A placed piece the game data does not know (a removed mod's piece, the sample's names under Dev.SampleData; fix2 4,
        /// Joost in game: "Beech Sapling" and "Feast meadows" stood under pieces built, without a picture): its kind by its
        /// name token, so plantings stay on Farming and feasts on Cooking (B8); anything else is built.
        /// </summary>
        public static string PieceKindByName(string token)
        {
            if (VanillaGround.Contains(token ?? "")) return "ground";
            var low = (token ?? "").ToLowerInvariant();
            if (low.Contains("sapling") || low.Contains("seed")) return "planted";
            if (low.Contains("feast")) return "feast";
            return "built";
        }

        static Dictionary<string, double> Only(IDictionary<string, float> d, Func<string, bool> keep)
        {
            var r = new Dictionary<string, double>();
            foreach (var kv in d ?? new Dictionary<string, float>()) if (kv.Value > 0 && keep(kv.Key)) r[kv.Key] = kv.Value;
            return r;
        }
        public static Dictionary<string, double> Crafted(PanelInput i, string kind) => Only(i.ItemsCrafted, k => ItemKind(i, k) == kind);
        public static Dictionary<string, double> Placed(PanelInput i, string kind) => Only(i.PiecesPlaced, k => PieceKind(i, k) == kind);
        public static Dictionary<string, double> PickedUp(PanelInput i, string kind) => Only(i.ItemsPickedUp, k => GatherKind(i, k) == kind);
        /// <summary>Picked up per item, measured exactly by Hearthwoven (ClientHooks.Pickup): since install for yourself.</summary>
        public static Dictionary<string, double> PickedUpMeasured(PanelInput i, string kind) => Only(i.Events?.PickedUp, k => GatherKind(i, k) == kind);

        /// <summary>
        /// Brought in per item (K1): Before = the game's pickup counter when Hearthwoven first ran for this character
        /// (LocalTotals.Baseline, drawn faded), Exact = what Hearthwoven counted exactly since install (drawn solid); the
        /// total is both. The game counter's growth after install is never added (it misses every pickup onto a stack you
        /// carry). An install from before the baseline: what it had counted exactly by then (ExactAtBaseline) is left out of
        /// the solid part, since the baseline partly holds it already. Without a stored baseline (a fellow's copy, totals not loaded) Before = the game counter minus the exact
        /// count, at least 0, so nothing counts twice.
        /// </summary>
        public static Dictionary<string, (double before, double exact)> BroughtIn(PanelInput i, string kind)
        {
            var r = new Dictionary<string, (double before, double exact)>();
            Dictionary<string, float> stored = null;
            var known = i?.Baseline != null && i.Baseline.TryGetValue("pickedUp", out stored) && stored != null;
            var exact = PickedUpMeasured(i, kind);
            Dictionary<string, float> already = null;
            if (known && i.ExactAtBaseline != null && i.ExactAtBaseline.TryGetValue("pickedUp", out already) && already != null)
                foreach (var k in exact.Keys.ToList()) if (already.TryGetValue(k, out var a)) exact[k] = Math.Max(0, exact[k] - a);
            var keys = new HashSet<string>(exact.Keys);
            foreach (var kv in (known ? stored : i.ItemsPickedUp) ?? new Dictionary<string, float>())
                if (kv.Value > 0 && GatherKind(i, kv.Key) == kind) keys.Add(kv.Key);
            foreach (var k in keys)
            {
                exact.TryGetValue(k, out var e);
                double before;
                if (known) before = stored.TryGetValue(k, out var s) ? s : 0;
                else before = Math.Max(0, (i.ItemsPickedUp != null && i.ItemsPickedUp.TryGetValue(k, out var c) ? c : 0) - e);
                if (before + e > 0) r[k] = (before, e);
            }
            return r;
        }
        /// <summary>The game's pickup counter was stored when Hearthwoven first ran (LocalTotals.Baseline "pickedUp"); false: a fellow's copy, totals not loaded.</summary>
        static bool HasPickupBaseline(PanelInput i) => i?.Baseline != null && i.Baseline.TryGetValue("pickedUp", out var stored) && stored != null;
        /// <summary>Brought in, the whole kind: the same total as the page's bar (Deeds card, Together).</summary>
        public static double BroughtInTotal(PanelInput i, string kind) => BroughtIn(i, kind).Values.Sum(v => v.before + v.exact);

        /// <summary>The pickup gap, said short beside a brought-in total: the game counts an item only when it takes a new slot, so what
        /// joined a stack you already carried was never counted (Player.OnInventoryChanged). Only the part counted by the game (faded).</summary>
        /// Said on the page as the one quiet line EarlierIncomplete (Joost 2026-10-09: the mechanism sentences were unreadable);
        /// About > What it reads explains stacks and areas.
        public static string PickupNote(PanelInput i, string kind) => EarlierIncomplete;
        /// <summary>The one line a page says where the game's own (earlier) count can fall short: pickups onto a carried stack,
        /// work in an area a fellow player's PC hosts. The why is on About > What it reads, once.</summary>
        public const string EarlierIncomplete = "Earlier counts may be incomplete.";
    
        /// <summary>The brought-in bar: one composition, each part its item's total, its Fraction2 the faded share (counted
        /// before Hearthwoven); the note says what faded means, only when something is faded. Nothing faded: all counted on
        /// this PC since install.</summary>
        static Block BroughtInBar(string title, PanelInput input, string kind, Func<string, string> label, Func<string, (string colour, string pattern)> look, bool recorded = false)
        {
            var parts = BroughtIn(input, kind);
            var faded = parts.Values.Any(v => v.before > 0);
            if (recorded)   // 0.7 rule A: the sum per part, your character's whole count; no faded shares, no key; "Earlier counts may be incomplete." beside the total when an earlier part is above 0 or there is no baseline
            {
                var r = Composition(title, parts.ToDictionary(kv => kv.Key, kv => kv.Value.before + kv.Value.exact), label, look, SrcCharacter, tint: ItemTint(input));
                if (r == null) return null;
                if (faded || !HasPickupBaseline(input)) r.Text = PickupNote(input, kind);
                if (!input.IsSelf && r.Items.Count == 1) r.Tone = "single";
                return r;
            }
            var b = Composition(title, parts.ToDictionary(kv => kv.Key, kv => kv.Value.before + kv.Value.exact), label, look, faded ? SrcCharacter : SrcPc, tint: ItemTint(input));
            if (b == null) return null;
            foreach (var part in b.Items)
            {
                var ids = part.Id == FoldId ? part.Items.Select(x => x.Id).ToList() : new List<string> { part.Id };   // the folded part: its kinds together
                double before = ids.Sum(id => parts[id].before), all = ids.Sum(id => parts[id].before + parts[id].exact);
                part.Fraction2 = (float)(before / all);
            }
            if (faded) b.Note = FadedKey;   // the chip at the end of the legend
            if (faded) b.Text = PickupNote(input, kind);   // the pickup gap (SOURCES.md), beside the total
            if (!input.IsSelf && b.Items.Count == 1) b.Tone = "single";   // one kind at 100 % is no bar: the legend alone (a fellow's copy)
            return b;
        }

        /// <summary>
        /// What the pickaxe dug up besides stone and ore (PickaxeFinds: Withered Bone from muddy scrap piles, Chitin from the
        /// Leviathan), under the Mining bar as a legend of its own with its own total: the stone and ore total above stays
        /// what its label says, and the finds are still there to see (0.6.5). No bar: two or three small counts on a bar of
        /// their own would read as the same scale as the stone. Null when there is nothing.
        /// </summary>
        static Block PickaxeFindsKey(PanelInput input, Func<string, string> named)
        {
            var b = BroughtInBar(PickaxeFindsTitle, input, PickaxeFinds, named, k => (null, null), recorded: true);   // 0.7 rule A: the sum per item, no faded share
            if (b == null) return null;
            b.Tone = "single"; b.Note = null; b.Text = null;   // the legend alone; the bar above carries "Earlier counts may be incomplete."
            return b;
        }

        // the tree kinds by the words in their prefab names (a tree, its logs and log halves: "Beech1", "beech_log_half"),
        // most specific first
        static readonly (string word, string name)[] TreeKinds =
        {
            ("yggashoot", "Yggdrasil shoot"), ("swamptree", "Ancient tree"), ("beech", "Beech"), ("birch", "Birch"), ("oak", "Oak"), ("pine", "Pine"), ("fir", "Fir"),
        };
        /// <summary>A tree's plain kind: its logs and log halves fold into it ("FirTree_log_half" -> "Fir"); any other tree
        /// (a mod's) by its name without the log, stub and number suffixes.</summary>
        public static string TreeKind(PanelInput i, string prefab)
        {
            var low = (prefab ?? "").ToLowerInvariant();
            foreach (var t in TreeKinds) if (low.Contains(t.word)) return t.name;
            var bare = System.Text.RegularExpressions.Regex.Replace(prefab ?? "", "(?i)_(log|stub).*$", "");
            bare = System.Text.RegularExpressions.Regex.Replace(bare, "[_0-9]+$", "");
            return Who(i, bare.Length > 0 ? bare : prefab);
        }
        // Codex's tree and rock pictures (src/Panel/vocab/tree-*.png, rock-*.png; authored colour, drawn white) per tree kind
        // and per rock; a kind without its own picture (a mod's tree) keeps an empty place so the names stay in one column
        static readonly (string kind, string sprite)[] TreeSprites =
        {
            ("Yggdrasil shoot", "tree-yggdrasil"), ("Ancient tree", "tree-ancient"), ("Beech", "tree-beech"), ("Birch", "tree-birch"),
            ("Oak", "tree-oak"), ("Pine", "tree-pine"), ("Fir", "tree-fir"),
        };
        /// <summary>The picture of a tree kind (TreeKind's name): "vocab:tree-beech"; "" for a kind without one.</summary>
        public static string TreeIcon(string kind) { foreach (var t in TreeSprites) if (t.kind == kind) return "vocab:" + t.sprite; return ""; }
        static readonly (string word, string sprite)[] RockSprites =
        {
            ("copper", "item:CopperOre|vocab:rock-copper"), ("tin", "item:TinOre|vocab:rock-tin"), ("silver", "item:SilverOre|vocab:rock-silver"),
            ("scrap", "item:IronScrap|vocab:rock-scrap"), ("mudpile", "item:IronScrap|vocab:rock-scrap"),
            ("obsidian", "item:Obsidian|vocab:rock-stone"), ("flametal", "item:FlametalOreNew|item:FlametalOre|vocab:rock-stone"), ("meteorite", "item:FlametalOre|vocab:rock-stone"),
            ("blackmarble", "item:BlackMarble|vocab:rock-stone"), ("leviathan", "item:Chitin|vocab:rock-stone"),
            ("rock", "item:Stone|vocab:rock-stone"), ("stone", "item:Stone|vocab:rock-stone"),
        };
        /// <summary>The picture of a rock, vein or pile by its prefab ("rock4_copper" -> rock-copper, "MudPile" -> rock-scrap, any
        /// other rock -> rock-stone); "" when the name says nothing the set has a picture for. Every deposit shows the game's own
        /// ore icon (copper, tin, silver, iron scrap from the mud pile, stone) when the item exists at runtime (PanelLook.Icon: "a|b" = first that resolves), our sketch otherwise.</summary>
        public static string RockIcon(string prefab)
        {
            var low = (prefab ?? "").ToLowerInvariant();
            foreach (var r in RockSprites) if (low.Contains(r.word)) return r.sprite.Contains(":") ? r.sprite : "vocab:" + r.sprite;
            return "";
        }
        /// <summary>Axe hits per tree kind (since install, exact), logs and log halves folded into their tree.</summary>
        public static Dictionary<string, double> TreeHits(PanelInput i)
        {
            var r = new Dictionary<string, double>();
            foreach (var kv in i?.Events?.ChopHits ?? new Dictionary<string, float>())
            {
                if (kv.Value <= 0) continue;
                var k = TreeKind(i, kv.Key);
                r[k] = (r.TryGetValue(k, out var v) ? v : 0) + kv.Value;
            }
            return r;
        }

        /// <summary>Trees felled per tree kind, counted by Hearthwoven (ClientHooks.Felling): since install for yourself.</summary>
        public static Dictionary<string, double> TreesFelledByKind(PanelInput i)
        {
            var r = new Dictionary<string, double>();
            foreach (var kv in i?.Events?.Felled ?? new Dictionary<string, float>())
            {
                if (kv.Value <= 0) continue;
                var k = TreeKind(i, kv.Key);
                r[k] = (r.TryGetValue(k, out var v) ? v : 0) + kv.Value;
            }
            return r;
        }
        public const string TreesBaseline = "treesFelled";   // LocalTotals.Baseline kind: the game's trees-felled counter at first run
        /// <summary>
        /// Trees felled in two layers (T3): faded = the game's trees-felled counter when Hearthwoven first ran for this
        /// character (it misses trees felled where a fellow's PC hosts the area), solid = every tree Hearthwoven counted since
        /// (ClientHooks.Felling, wherever you felled it; nothing of it is in the baseline). The game counter's later growth is
        /// never added. null = no baseline (a fellow's shared copy, local totals not loaded): the game's counter alone.
        /// </summary>
        public static (double faded, double solid)? TreesFelledLayers(PanelInput i)
        {
            if (i == null || !i.IsSelf || i.Events == null || i.Baseline == null || !i.Baseline.TryGetValue(TreesBaseline, out var at) || at == null) return null;
            return (at.TryGetValue("Tree", out var f) ? f : 0, i.Events.Felled.Values.Sum(v => (double)v));
        }
        /// <summary>Trees felled: both layers added up, or the game's counter without a baseline.</summary>
        public static double TreesFelled(PanelInput i) { var l = TreesFelledLayers(i); return l.HasValue ? l.Value.faded + l.Value.solid : C(i, "Tree"); }
        /// <summary>Said under the game's trees-felled counter when it stands alone (no baseline): it misses trees.</summary>
        public const string TreesMissedBefore = EarlierIncomplete;
        public static string TreesMissed(PanelInput i) => EarlierIncomplete;

        static string Who(PanelInput input, string source)
        {
            var cause = CauseName(source, input.IsSelf);
            if (cause != null) return cause;
            // the game's own display name (PanelUi: the item's shared name token through the game's localization); a
            // readable fallback only when the game gives none, never a raw token
            var name = input.DisplayName?.Invoke(source);
            return string.IsNullOrEmpty(name) || name.StartsWith("$") ? Prettify(source) : name;
        }


        // ---------- the measured event log: filters, deaths, hints ----------

        public class DamageRow { public DateTime Bucket; public string Biome, Dir, Other, Cause, Type; public float Amount; }

        public static IEnumerable<string> BiomesSeen(EventLog log)
        {
            if (log == null) return Enumerable.Empty<string>();
            var seen = new HashSet<string>();
            foreach (var k in log.Damage.Keys) { var p = LogKeys.Of(k).P; if (p.Length >= 2) seen.Add(p[1]); }   // 0.8: each key split once (LogKeys.cs)
            foreach (var d in log.Deaths) seen.Add(d.Biome);
            return seen.OrderBy(b => { var i = Array.IndexOf(BiomeOrder, b); return i < 0 ? 99 : i; }).ThenBy(b => b, StringComparer.Ordinal);
        }

        static bool TryBucket(string iso, out DateTime utc) => LogKeys.Minute(iso, out utc);   // 0.8: each minute string parsed once (LogKeys.cs)

        /// <summary>Start of the window, or null for the whole session.</summary>
        public static DateTime? Cutoff(TimeWindow w, DateTime nowUtc) =>
            w == TimeWindow.LastTenMinutes ? nowUtc.AddMinutes(-10) : w == TimeWindow.LastThirtyMinutes ? nowUtc.AddMinutes(-30) :
            w == TimeWindow.LastHour ? nowUtc.AddHours(-1) : w == TimeWindow.LastThreeHours ? nowUtc.AddHours(-3) : (DateTime?)null;

        /// <summary>
        /// B33 on every page with windows (Joost 2026-10-10: "Session" is YOUR session's span): on a fellow's book their Session counts from
        /// when you started playing this time (their log's rows are timed, so what they did before you came in is left out); null on your own
        /// book, or when the start of your session is not known.
        /// </summary>
        public static DateTime? SessionFrom(PanelInput input) => input != null && !input.IsSelf ? input.ViewerSessionStartUtc : null;

        /// <summary>A fellow who did not play during your session (B33): not seen in the world this connection, or their latest record is older
        /// than your session's start. Their Session then says so ("Edda has not played since 21:40."), never as if they had been there.</summary>
        public static bool FellowOffThisSession(PanelInput input) =>
            input != null && !input.IsSelf && (input.OnThisSession == false ||
            (input.ViewerSessionStartUtc.HasValue && input.LastRecordedUtc.HasValue && input.LastRecordedUtc.Value.AddMinutes(SpanOf(input)) <= input.ViewerSessionStartUtc.Value));

        /// <summary>Damage rows inside the window and biome. A bucket (one minute; ten for an older sender) counts when any part of it lies in the window.
        /// sessionFrom: where Session starts (SessionFrom: a fellow's book counts from your session's start); null = the whole log.</summary>
        public static List<DamageRow> Damage(EventLog log, TimeWindow w, string biome, DateTime nowUtc, DateTime? sessionFrom = null)
        {
            var rows = new List<DamageRow>();
            if (log == null) return rows;
            foreach (var kv in log.Damage)
            {
                var p = LogKeys.Of(kv.Key).P;   // 0.8: each key split once (LogKeys.cs); its minute parsed once too (TryBucket)
                if (p.Length < 6 || !InFilter(p[0], p[1], w, biome, nowUtc, log.Span, sessionFrom)) continue;
                TryBucket(p[0], out var t);
                rows.Add(new DamageRow { Bucket = t, Biome = p[1], Dir = p[2], Other = p[3], Cause = p[4], Type = p[5], Amount = kv.Value });
            }
            return rows;
        }

        static bool InFilter(string bucketIso, string rowBiome, TimeWindow w, string biome, DateTime nowUtc, int span, DateTime? sessionFrom = null)
        {
            if (!TryBucket(bucketIso, out var t)) return false;
            var cutoff = Cutoff(w, nowUtc) ?? (w == TimeWindow.Session ? sessionFrom : null);
            if (cutoff.HasValue && t.AddMinutes(span) <= cutoff.Value) return false;
            return string.IsNullOrEmpty(biome) || rowBiome == biome;
        }

        public static List<EventLog.Death> Deaths(EventLog log, TimeWindow w, string biome, DateTime nowUtc, DateTime? sessionFrom = null)
        {
            if (log == null) return new List<EventLog.Death>();
            var cutoff = Cutoff(w, nowUtc) ?? (w == TimeWindow.Session ? sessionFrom : null);
            return log.Deaths.Where(d => (!cutoff.HasValue || d.Time >= cutoff.Value) && (string.IsNullOrEmpty(biome) || d.Biome == biome))
                             .OrderByDescending(d => d.Time).ToList();
        }

        /// <summary>The damage type that hurt most in the last 10 seconds before a death ("" if nothing was recorded).</summary>
        public static string DominantType(EventLog.Death d)
        {
            var byType = new Dictionary<string, float>();
            foreach (var kv in d.Last10s) { var t = kv.Key.Substring(kv.Key.LastIndexOf('|') + 1); byType.TryGetValue(t, out var o); byType[t] = o + kv.Value; }
            return byType.Count == 0 ? "" : byType.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;
        }

        // Elemental damage that a mead or wine in the game itself resists. Physical damage gets no advice: armour, block
        // and dodge all help, so a hint would be guesswork.
        static string Remedy(string type)
        {
            switch (type)
            {
                case "poison": return "Poison resistance mead";
                case "fire": return "Fire resistance barley wine";
                case "frost": return "Frost resistance mead";
                default: return null;
            }
        }
        static string ElementOf(EventLog.Death d)
        {
            if (d.Cause == "Poisoned") return "poison";
            if (d.Cause == "Burning") return "fire";
            if (d.Cause == "Freezing") return "frost";
            return DominantType(d);
        }

        public const float HintShare = 1f / 3f;   // an element is worth a hint at a third of the damage taken in a biome

        /// <summary>
        /// "Before you set out": per biome, an element behind your falls there (any death), or an element that made up at
        /// least a third of the damage you took there. Deaths first. Only remedies the game has.
        /// </summary>
        public static List<string> Hints(List<DamageRow> rows, List<EventLog.Death> deaths)
        {
            var hints = new List<string>(); var done = new HashSet<string>();
            foreach (var g in deaths.GroupBy(d => d.Biome))
            {
                foreach (var e in g.GroupBy(ElementOf).Where(e => Remedy(e.Key) != null).OrderByDescending(e => e.Count()))
                {
                    if (!done.Add(g.Key + "|" + e.Key)) continue;
                    int n = e.Count(), all = g.Count();
                    var which = all == 1 ? "the death" : n == all ? (all == 2 ? "both deaths" : "all " + all + " deaths") : n + " of " + all + " deaths";
                    hints.Add(BiomeName(g.Key) + ": " + TypeName(e.Key).ToLowerInvariant() + " was behind " + which + " there. Bring " + Remedy(e.Key) + ".");
                }
            }
            foreach (var g in rows.Where(r => r.Dir == "taken").GroupBy(r => r.Biome))
            {
                var total = g.Sum(r => r.Amount); if (total <= 0) continue;
                foreach (var t in g.GroupBy(r => r.Type).Select(t => new { t.Key, Share = t.Sum(r => r.Amount) / total }).OrderByDescending(t => t.Share))
                {
                    if (t.Share < HintShare || Remedy(t.Key) == null || !done.Add(g.Key + "|" + t.Key)) continue;
                    hints.Add(BiomeName(g.Key) + ": " + TypeName(t.Key).ToLowerInvariant() + " was " + Math.Round(t.Share * 100) + "% of the damage received there. " + Remedy(t.Key) + " helps.");
                }
            }
            return hints;
        }

        /// <summary>Per biome, the killer that took you most often there (ties: the latest).</summary>
        public static Dictionary<string, string> MostCommonKiller(List<EventLog.Death> deaths) =>
            deaths.GroupBy(d => d.Biome).ToDictionary(g => g.Key,
                g => g.GroupBy(d => d.Killer).OrderByDescending(k => k.Count()).ThenByDescending(k => k.Max(d => d.Time)).First().Key);

        static IEnumerable<KeyValuePair<string, float>> Sum(IEnumerable<DamageRow> rows, Func<DamageRow, string> key) =>
            rows.GroupBy(key).Select(g => new KeyValuePair<string, float>(g.Key, g.Sum(r => r.Amount)));

        /// <summary>The window as a page says it for whose book it is: a fellow player's Session is their last shared session (the copy can be days old), never "this session".</summary>
        public static string WindowLabelFor(PanelInput i, TimeWindow w) => i != null && !i.IsSelf && w == TimeWindow.Session ? "Last shared session" : WindowLabel(w);
        public static string WindowLabel(TimeWindow w) =>
            w == TimeWindow.LastTenMinutes ? "Last 10 minutes" : w == TimeWindow.LastThirtyMinutes ? "Last 30 minutes" : w == TimeWindow.LastHour ? "Last hour" :
            w == TimeWindow.LastThreeHours ? "Last 3 hours" : w == TimeWindow.Session ? "This session" : w == TimeWindow.Today ? "Today" :
            w == TimeWindow.SevenDays ? "Last 7 days" : w == TimeWindow.ThirtyDays ? "Last 30 days" : "All";   // 0.7 rule W.2: All (the page's own dates say when)
        /// <summary>The window as a chip says it (six chips must fit the heading row); the plate's own line carries the full name.</summary>
        public static string WindowShort(TimeWindow w) =>
            w == TimeWindow.LastTenMinutes ? "10 min" : w == TimeWindow.LastThirtyMinutes ? "30 min" : w == TimeWindow.LastHour ? "1 h" :
            w == TimeWindow.LastThreeHours ? "3 h" : w == TimeWindow.Session ? "Session" : w == TimeWindow.Today ? "Today" :
            w == TimeWindow.SevenDays ? "7 days" : w == TimeWindow.ThirtyDays ? "30 days" : "All";
        /// <summary>How long a fellow player's newest bucket ran (their copy's span; an unknown copy: this build's).</summary>
        static int SpanOf(PanelInput i) => i?.Log != null && i.Log.Span >= 1 ? i.Log.Span : EventLog.BucketMinutes;

        /// <summary>
        /// Every count the panel shows: whole, thousands apart by a no-break space ("4 180"), never a comma or a point, which
        /// read as a decimal mark to half the group (Joost 2026-10-08); decimals keep a point ("43.8 km").
        /// </summary>
        public static string Number(double v) => Math.Round(v).ToString("#,0", Grouped);
        public const string ThousandsGap = "\u00A0";
        /// <summary>
        /// The one reader for a count as text (RESILIENCE-06 item 1): whatever the culture, never an exception. Thousands marks are
        /// dropped: any space (the panel's no-break space, a narrow no-break U+202F, a thin U+2009, a figure space, a plain one), a
        /// comma, an apostrophe; a point is the decimal mark; U+2212 is a minus. Unreadable, empty, NaN or infinite: 0. Prefer the
        /// number itself where it exists; this is for the few places that only have the shown text.
        /// </summary>
        public static double ParseCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var b = new System.Text.StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c) || c == ',' || c == '\'' || c == '\u2019' || c == '\u066C') continue;
                b.Append(c == '\u2212' ? '-' : c);
            }
            return double.TryParse(b.ToString(), NumberStyles.Float, Inv, out var v) && !double.IsNaN(v) && !double.IsInfinity(v) ? v : 0;
        }
        static readonly NumberFormatInfo Grouped = new NumberFormatInfo { NumberGroupSeparator = ThousandsGap, NumberDecimalSeparator = ".", NumberGroupSizes = new[] { 3 } };
        static string N(double v) => Number(v);
        // a real amount between 0 and 1 (0.4 damage) rounds to "0", which reads as nothing happened: say so instead
        public static string NAtLeast(double v) => v > 0 && v < 1 ? LessThanOne : N(v);
        /// <summary>An amount above 0 that rounds to nothing (B26, Joost: "under 1" read odd beside "A falling tree"; 0.8.1, Joost: "&lt;1", a number like the others).</summary>
        public const string LessThanOne = "<1";
        /// <summary>Words that must stay on one line ("received blunt less than 1" wrapped in game as "less than" / "1"): every space a no-break space.</summary>
        public static string NoBreak(string s) => s?.Replace(' ', '\u00A0');
        static string Km(double meters) { var km = meters / 1000.0; return (km < 100 ? km.ToString("0.0", Inv) : Number(km)) + " km"; }
        static string Plural(double n, string one, string many) => N(n) + " " + (Math.Round(n) == 1 ? one : many);
        static string About(double seconds) => seconds < 90 ? "about " + N(seconds) + " seconds" : "about " + N(seconds / 60) + " minutes";
        static DateTime Local(PanelInput input, DateTime utc) => input.ToLocal != null ? input.ToLocal(utc) : utc.ToLocalTime();
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        static bool SameName(string a, string b) => !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        static string[] Split2(string key) { var i = key.IndexOf('|'); return i < 0 ? new[] { key, "" } : new[] { key.Substring(0, i), key.Substring(i + 1) }; }
        static void Bump(IDictionary<string, double> d, string k, double v) { d.TryGetValue(k, out var o); d[k] = o + v; }

        // Wording follows whose saga it is: "you" for yourself, the name for a fellow player.
        static string Name(PanelInput i) => string.IsNullOrEmpty(i.PlayerName) ? "this player" : i.PlayerName;
        /// <summary>"after your armour" for yourself, "after their armour" for a fellow player; "What hurt you" / "What hurt Tor".</summary>
        public static string AfterOf(PanelInput i) => i == null || i.IsSelf ? AfterResistance : AfterTheirs;
        public static string WhatHurt(PanelInput i) => i == null || i.IsSelf ? WhatHurtYou : "What hurt " + Name(i);
        static string Subject(PanelInput i) => i.IsSelf ? "you" : Name(i);
        static string Possessive(PanelInput i) => i.IsSelf ? "your" : Name(i) + "'s";
        public const string SourceTheirLast = "measured in last shared";
        public static string MeasuredOf(PanelInput i) => i.IsSelf ? SourceSession : SourceTheirLast;

        // The page scope of a fellow's book is RecordedScope (RecordedModel.cs): whose copy it is and when it is from.
        // the one "as of" form (fix3-rest): "as of 8 Oct 15:54", the moment of their latest record
        public static string AsOf(PanelInput i) => "as of " + Local(i, i.LastRecordedUtc.Value).ToString("d MMM HH:mm", Inv);

        // A fellow's data is their latest shared copy, which can be days old: always say so, with the date.
        static string SessionScope(PanelInput i)
        {
            if (!i.IsSelf && i.Cached && i.ReceivedUtc.HasValue) return "last shared " + Local(i, i.ReceivedUtc.Value).ToString("d MMM HH:mm", Inv);   // B23: from this PC's cache until the server's fresh copy comes
            if (!i.IsSelf) return "last shared" + (i.LastRecordedUtc.HasValue ? ", " + Local(i, i.LastRecordedUtc.Value).ToString("d MMM HH:mm", Inv) : "");
            return i.SessionStartUtc.HasValue ? "this session, since " + Local(i, i.SessionStartUtc.Value).ToString("HH:mm", Inv) : "this session";
        }

        static double C(PanelInput input, string stat) => input.Character != null && input.Character.TryGetValue(stat, out var v) ? v : 0;
        static double M(PanelInput input, Func<SessionEvents, Dictionary<string, float>> pick) => input.Events == null ? 0 : pick(input.Events).Values.Sum();
        static IEnumerable<PanelInput> FellowsOf(PanelInput i) => (i.Fellows ?? new List<PanelInput>()).Where(f => f?.Events != null && !SameName(f.PlayerName, i.PlayerName));

        // ---------- titles: one table, owner page per title ----------

        public const string Measured = "M", Profile = "P", Fellows = "F";

        public class SagaLine
        {
            public readonly string Source; public readonly Func<PanelInput, double> Amount; public readonly Func<double, PanelInput, string> Text;
            public SagaLine(string source, Func<PanelInput, double> amount, Func<double, string> text) : this(source, amount, (v, _) => text(v)) { }
            public SagaLine(string source, Func<PanelInput, double> amount, Func<double, PanelInput, string> text) { Source = source; Amount = amount; Text = text; }
            /// <summary>A line that earns the title but shows no number (Text null): the game's own counter keeps a title
            /// earned while the page shows Hearthwoven's exact count instead.</summary>
            public static SagaLine EarnOnly(string source, Func<PanelInput, double> amount) => new SagaLine(source, amount, (Func<double, PanelInput, string>)null);
        }

        public class SagaTitle
        {
            public readonly string Id, Title, Descriptor; public readonly Chapter Chapter; public readonly string Page; public readonly SagaLine[] Lines;
            public SagaTitle(string id, string title, string descriptor, Chapter chapter, string page, params SagaLine[] lines)
            { Id = id; Title = title; Descriptor = descriptor; Chapter = chapter; Page = page; Lines = lines; }
        }

        /// <summary>
        /// The titles, in the order of report/kstats.py SAGA. Woodcutter, Mapmaker, Mender, Wallwarden and Bossbane are
        /// working names (Codex will rename and draw them); renaming is one line here. Waymate stays out until whose grave
        /// you opened is measured.
        /// </summary>
        public static readonly SagaTitle[] SagaTitles =
        {
            new SagaTitle("explorer", "Trailfinder", "Paths traveled, places found", Chapter.Voyages, "onfoot",
                new SagaLine(Profile, i => C(i, "DistanceTraveled") > 0 ? C(i, "DistanceTraveled") : C(i, "DistanceWalk") + C(i, "DistanceRun"), v => Km(v) + " traveled"),
                new SagaLine(Profile, i => C(i, "TreasureBuriedFound") + C(i, "TreasureDungeonFound") + C(i, "TreasureLocationFound"), v => Plural(v, "treasure found", "treasures found"))),
            new SagaTitle("woodcutter", "Woodcutter", "Trees felled, logs split", Chapter.Deeds, "woodcutting",
                new SagaLine(Measured, i => M(i, e => e.ChopHits), v => Plural(v, "axe hit", "axe hits")),
                new SagaLine(Profile, TreesFelled, v => Plural(v, "tree felled", "trees felled")),
                SagaLine.EarnOnly(Profile, i => C(i, "Tree"))),
            new SagaTitle("miner", "Stonebreaker", "Stone and ore broken", Chapter.Deeds, "mining",
                // one pickaxe-hits number shown (K6): Hearthwoven's, since install and exact (the game's MineHits only counts in
                // an area your PC hosts); MineHits still earns the title, it is never taken away
                new SagaLine(Measured, i => M(i, e => e.PickaxeHits), v => Plural(v, "pickaxe hit", "pickaxe hits")),
                SagaLine.EarnOnly(Profile, i => C(i, "MineHits"))),
            new SagaTitle("farmer", "Fieldkeeper", "Crops, berries and honey harvested", Chapter.Deeds, "farming",
                new SagaLine(Profile, i => C(i, "HarvestCrop") + C(i, "HarvestBerry") + C(i, "HarvestMushroom") + C(i, "HarvestVine"), v => Plural(v, "harvest", "harvests")),
                new SagaLine(Profile, i => C(i, "BeesHarvested") + C(i, "SapHarvested"), v => Plural(v, "honey and sap harvest", "honey and sap harvests"))),
            new SagaTitle("cook", "Hearth Cook", "Food made, food others enjoyed", Chapter.Deeds, "cooking",
                new SagaLine(Fellows, FedOthers, (v, i) => (v == 1 ? "1 time" : N(v) + " times") + " someone enjoyed food " + Subject(i) + " made"),
                new SagaLine(Profile, i => C(i, "CraftFood") + C(i, "CraftGrill"), v => Plural(v, "dish cooked", "dishes cooked"))),
            new SagaTitle("smith", "Forgekeeper", "Gear crafted and improved", Chapter.Deeds, "crafting",
                // the game's gear counters: the same item types as the "Gear made most" list (ItemKindOf); upgrades on their own line
                new SagaLine(Profile, i => C(i, "CraftWeapon") + C(i, "CraftArmor") + C(i, "CraftTool") + C(i, "CraftTrinket"), v => Plural(v, "piece of gear made", "pieces of gear made")),
                new SagaLine(Profile, i => C(i, "Upgrades"), v => Plural(v, "upgrade", "upgrades")),
                // only what the player put in: a Stoker's Chest's feeding is nobody's (Hall > Smelters shows it apart); "by hand" says so where chests feed
                new SagaLine(Measured, i => M(i, e => e.SmelterAdded), (v, i) => N(v) + " ore and fuel into smelters" + (ChestsFeed(i) ? " by hand" : ""))),
            new SagaTitle("hauler", "Storekeeper", "Carts pulled home", Chapter.Stores, "carts",
                new SagaLine(Measured, i => M(i, e => e.CartMeters), v => N(v) + " m pulling a cart")),
            new SagaTitle("sailor", "Helmskeeper", "Distance at the helm", Chapter.Voyages, "sailing",
                new SagaLine(Profile, i => C(i, "DistanceSailHelm"), v => Km(v) + " at the helm")),
            new SagaTitle("mapmaker", "Mapmaker", "Your map shared at the table", Chapter.Voyages, "maps",
                new SagaLine(Measured, i => M(i, e => e.MapShared), v => v == 1 ? "Map shared once at the table" : "Map shared " + N(v) + " times at the table")),
            new SagaTitle("builder", "Hallwright", "Pieces raised with the hammer", Chapter.Deeds, "building",
                // the pieces in "Pieces built most" (groundwork, plantings and feasts left out); hammer counters only without the list
                new SagaLine(Profile, BuiltCount, v => Plural(v, "piece built", "pieces built"))),
            new SagaTitle("mender", "Mender", "Repairs that keep the hall standing", Chapter.Deeds, "building",
                new SagaLine(Measured, i => M(i, e => e.Repairs), v => Plural(v, "repair with the hammer", "repairs with the hammer"))),
            new SagaTitle("wallwarden", "Wallwarden", "Defenses built, armed and loaded", Chapter.Deeds, "building",   // B28: with the base defences it counts
                new SagaLine(Profile, i => C(i, "BuildClusterDefense") + C(i, "TrapArmed") + C(i, "TurretAmmoAdded"), v => N(v) + " defenses built, armed or loaded")),   // short: it is also the title's reason in the strip on its page (B18)
            new SagaTitle("defender", "Shieldbearer", "Blocks and well-timed parries", Chapter.Battle, "defense",
                // one reason with the Defence tile's split (review 0.6.5: the reason said the game's blocks, which include the parries, beside a
                // tile that says them apart): "196 blocks · 58 parries", a zero part left out
                new SagaLine(Measured, i => i.Events?.Blocks ?? 0, (v, i) => GuardSplit(i.Events.Blocks, i.Events.Parries))),
            new SagaTitle("fighter", "Battlehand", "Foes fought and felled", Chapter.Battle, "foes",
                new SagaLine(Profile, i => C(i, "EnemyKills"), v => Plural(v, "foe defeated", "foes defeated"))),
            new SagaTitle("bossbane", "Bossbane", "Forsaken faced and felled", Chapter.Battle, "foes",
                new SagaLine(Profile, i => C(i, "BossKills"), v => Plural(v, "boss fight won", "boss fights won"))),
            new SagaTitle("tamer", "Beastkeeper", "Creatures tamed, petted and led", Chapter.Deeds, "taming",
                new SagaLine(Profile, i => { var t = C(i, "TamedPetting") + C(i, "TamedCommand"); return t > 0 ? t : C(i, "CreatureTamed"); },
                             v => v == 1 ? "Cared for a creature once" : "Cared for a creature " + N(v) + " times")),
            new SagaTitle("fisher", "Tidecatcher", "Fish caught on the line", Chapter.Deeds, "fishing",
                new SagaLine(Profile, i => C(i, "FishCaught"), v => Plural(v, "fish caught", "fish caught"))),
            new SagaTitle("trader", "Far Trader", "Goods bought, coins spent", Chapter.Stores, "trader",
                new SagaLine(Measured, i => M(i, e => e.Spent), v => Plural(v, "coin spent", "coins spent"))),
        };

        public class TitleRow
        {
            public string Id, Title, Descriptor; public Chapter Chapter; public string Page;
            public readonly List<KeyValuePair<string, string>> Lines = new List<KeyValuePair<string, string>>();   // value, source
        }

        public static string SourceOf(PanelInput i, string source) => source == Profile ? SourceCharacter : source == Fellows ? SourceFellows : MeasuredOf(i);

        public static double BuiltCount(PanelInput i)
        {
            var built = Placed(i, "built").Values.Sum();
            return built > 0 ? built : C(i, "BuiltPiecesNoDebt") > 0 ? C(i, "BuiltPiecesNoDebt") : C(i, "BuiltPieces");
        }

        /// <summary>Blocks and parries as Defence's tile says them: the game's held blocks include the parries, so "blocks" are the ones that
        /// were not parries ("196 blocks · 58 parries"); a zero part is left out.</summary>
        public static string GuardSplit(double blocks, double parries)
        {
            var plain = Math.Max(0, blocks - parries);
            var parts = new List<string>();
            if (plain > 0) parts.Add(Plural(plain, "block", "blocks"));
            if (parries > 0) parts.Add(Plural(parries, "parry", "parries"));
            return string.Join(" · ", parts.ToArray());
        }

        /// <summary>Titles with evidence, in table order (no ranking). A zero line is left out, never shown as "0".</summary>
        public static List<TitleRow> Titles(PanelInput input)
        {
            var rows = new List<TitleRow>();
            foreach (var t in SagaTitles)
            {
                var row = new TitleRow { Id = t.Id, Title = t.Title, Descriptor = input.IsSelf || !t.Descriptor.StartsWith("Your ") ? t.Descriptor : Name(input) + "'s " + t.Descriptor.Substring(5), Chapter = t.Chapter, Page = t.Page };
                var earned = false;
                foreach (var l in t.Lines)
                {
                    double v; try { v = l.Amount(input); } catch { v = 0; }
                    if (v <= 0) continue;
                    earned = true;
                    if (l.Text != null) row.Lines.Add(new KeyValuePair<string, string>(l.Text(v, input), SourceOf(input, l.Source)));   // an earn-only line shows nothing
                }
                if (earned) rows.Add(row);
            }
            return rows;
        }

        /// <summary>Times fellow players who share ate food this player made (recorded on the eater's PC).</summary>
        public static double FedOthers(PanelInput input) =>
            FellowsOf(input).Sum(f => f.Events.AteFoodMadeBy.Where(kv => SameName(Split2(kv.Key)[0], input.PlayerName)).Sum(kv => (double)kv.Value));

        // ---------- chapters and their left lists ----------

        static readonly (Chapter id, string label, string icon)[] ChapterRow =
        {
            // the UI kit's chapter icons (original Hearthwoven art by Codex Finn)
            (Chapter.Deeds, "Deeds", "ui:chapter-deeds"), (Chapter.Feats, "Feats", FeatsIcon), (Chapter.Company, "Company", "ui:chapter-company"), (Chapter.Stores, "Hall", "ui:chapter-stores"),
            (Chapter.Battle, "Battle", "ui:chapter-battle"), (Chapter.Voyages, "Voyages", "ui:chapter-voyages"), (Chapter.Skills, "Skills", "ui:chapter-skills"),
        };

        static readonly (string id, string label, string icon)[] DeedsList =
        {
            (RecentPageId, RecentTitle, RecentIcon),   // 0.7: what grew lately, yours and the group's (Chapters/RecentPage.cs); first, the book still opens on the Overview
            ("overview", "Overview", ListOverview), ("cooking", "Cooking", "title:cook"), (MealsPageId, MealsLabel, MealsIcon), ("building", "Building", "title:builder"), ("groundwork", "Groundwork", "vocab:ground-lower"), ("crafting", "Crafting", "title:smith"),
            ("woodcutting", "Woodcutting", "title:woodcutter"), ("mining", "Mining", "title:miner"), ("farming", "Farming", "title:farmer"),
            ("fishing", "Fishing", "title:fisher"), ("taming", "Taming", "title:tamer"),
        };
        static readonly (string id, string label, string icon)[] StoresList =
            { ("overview", "Overview", ListOverview), ("trader", "Trader", "title:trader"), ("smelters", "Smelters", "vocab:list-smelters") };   // chest and cart records are server-only: not here
        static readonly (string id, string label, string icon)[] BattleList =
            { ("overview", "Overview", ListOverview), (LastFightPage, LastFightLabel, LastFightIcon), (FeedPage, FeedLabel, FeedIcon),   // 0.8: the last fight and the feed (Chapters/BattleFeedModel.cs)
              ("damage", "Damage", "vocab:list-damage"), ("defense", "Defense", "vocab:block-mark"), ("deaths", "Deaths", "vocab:death"), ("foes", "Foes", "vocab:list-foes") };
        static readonly (string id, string label, string icon)[] VoyagesList =
            { ("overview", "Overview", ListOverview), ("sailing", "Sailing", "vocab:list-sailing"), ("cargo", "Cargo", "vocab:cargo-mark"), ("onfoot", "On foot", "title:explorer"), ("maps", "Maps", "vocab:compass-home") };
        // every left-list entry carries a line icon (ADDENDUM-6); a sprite not shipped yet draws no icon, never an error
        public const string ListOverview = "vocab:list-overview";
        static readonly Dictionary<string, string> CompanyIcons = new Dictionary<string, string>
        {
            ["fireside"] = "vocab:hearth-fire", ["together"] = "vocab:list-together", ["food"] = "vocab:list-food-shared", ["gear"] = "vocab:list-gear-shared",
        };

        /// <summary>The left list of a chapter. Company: one equal row per companion, alphabetical. Skills: one row per skill.</summary>
        public static List<Choice> ListOf(PanelInput input, Chapter c)
        {
            IEnumerable<(string id, string label, string icon)> items;
            switch (c)
            {
                case Chapter.Deeds: items = DeedsList; break;
                case Chapter.Stores: items = StoresList; break;
                case Chapter.Battle: items = BattleList; break;
                case Chapter.Voyages: items = VoyagesList; break;
                case Chapter.Feats: items = FeatsList; break;   // Earned, Unsung (Chapters/FeatsModel.cs)
                case Chapter.Company:
                    items = CompanyList.Select(x => (x.id, x.label, CompanyIcons.TryGetValue(x.id, out var ic) ? ic : ""));   // Fireside, Together, Food shared, Gear shared (Chapters/CompanyModel.cs)
                    if (input == null || input.IsSelf) items = new[] { (AwayPage, AwayLabel, AwayIcon) }.Concat(items);   // Since you were away first: told to you, so your own book only (Chapters/AwayModel.cs)
                    break;
                default:
                    items = new[] { ("overview", "Overview", ListOverview) }.Concat(SkillNames(input).Select(s => (s, SkillName(input, s), "skill:" + s)));
                    break;
            }
            // 0.8: a fellow's copy carries no feed: the feed and the last fight stay in the list, greyed, and their page says why
            return items.Select(x => new Choice { Id = x.id, Label = x.label, Icon = x.icon, Disabled = c == Chapter.Battle && input != null && !input.IsSelf && (x.id == FeedPage || x.id == LastFightPage) }).ToList();
        }

        /// <summary>"your" on your own page, "their" on a fellow player's book (sample-one: a fellow's book never speaks to the reader about them).</summary>
        public static string Their(PanelInput i) => i == null || i.IsSelf ? "your" : "their";
        /// <summary>A line written to the reader ("Plant and pick your first crop.") said about a fellow player on their book.</summary>
        public static string Voice(PanelInput i, string text) => i == null || i.IsSelf || text == null ? text : text.Replace("your ", "their ").Replace(" you ", " they ");

        static string SkillName(PanelInput input, string skill)
        {
            var n = input.DisplayName?.Invoke("$skill_" + skill.ToLowerInvariant());
            return string.IsNullOrEmpty(n) || n.StartsWith("$") ? Prettify(skill) : n;
        }

        static IEnumerable<string> SkillNames(PanelInput input)
        {
            var all = new HashSet<string>(input.SkillLevels?.Where(kv => kv.Value > 0).Select(kv => kv.Key) ?? Enumerable.Empty<string>());
            foreach (var kv in input.Events?.SkillPractice ?? new Dictionary<string, float>()) if (kv.Value > 0) all.Add(kv.Key);
            return all.OrderBy(s => SkillName(input, s), StringComparer.OrdinalIgnoreCase);
        }

        // ---------- the panel ----------

        public static PanelView Build(PanelInput input, PanelState state) => WithFeatMemo(() => SettleHead(input, state, CompareOrPlain(input, state, BuildView(input, state))));   // L6: feat numbers once per render (FeatsModel.FeatMemo); 0.8: Compare periods (CompareModel.cs), then the page head (PageHead.cs)

        static PanelView BuildView(PanelInput input, PanelState state)
        {
            input = input ?? new PanelInput();
            state = state ?? new PanelState();
            // fix4: a fellow's Battle book holds their last shared session (and, when they share it, since install): the other windows
            // (10 min .. 3 h) measure against your clock, not theirs, so they are not offered and the book reads the window it can show
            var asked = state;
            var view = new PanelView { Active = state.Chapter, Owner = string.IsNullOrEmpty(input.PlayerName) ? "You" : input.PlayerName, ShowAbout = state.ShowAbout };
            foreach (var c in ChapterRow) view.Chapters.Add(new Choice { Id = c.id.ToString(), Label = c.label, Icon = c.icon, Selected = c.id == state.Chapter && !state.ShowAbout });
            view.ListTitle = state.ShowAbout ? "About" : state.Chapter == Chapter.Company ? "Company" : ChapterRow.First(c => c.id == state.Chapter).label;
            if (state.ShowAbout) view.List.AddRange(AboutList.Select(x => new Choice { Id = x.id, Label = x.label }));
            else view.List.AddRange(ListOf(input, state.Chapter));
            var page = state.ShowAbout ? state.AboutPage : state.PageOf(state.Chapter);
            if (view.List.Count > 0 && !view.List.Any(l => l.Id == page))   // Deeds opens on its Overview (Recent sits above it); Company still opens on Fireside (Since you were away is chosen, or opens by itself)
                page = !state.ShowAbout && state.Chapter == Chapter.Deeds ? "overview"
                     : state.Chapter == Chapter.Company && !state.ShowAbout && view.List.Count > 1 && view.List[0].Id == AwayPage ? view.List[1].Id : view.List[0].Id;
            view.Page = page;
            var place = new PanelPlace { Chapter = state.Chapter, Page = page, Player = state.Player ?? "", About = state.ShowAbout };
            SettleWait(state, place, input);   // a greyed day window pressed on another page: back to the last window that worked (B17)
            if (state.Chapter == Chapter.Battle && !state.ShowAbout && !input.IsSelf)
            {
                var wanted = state.Window == TimeWindow.SinceInstall && input.SharedSinceInstall ? TimeWindow.SinceInstall : TimeWindow.Session;
                if (wanted != state.Window) { state = (PanelState)CopyOf.Invoke(state, null); state.Window = wanted; }
            }
            foreach (var l in view.List) l.Selected = l.Id == page;
            var titles = Titles(input);
            foreach (var t in titles.Where(t => !state.ShowAbout && t.Chapter == state.Chapter && t.Page == page))
                view.Badges.Add(new Choice { Id = t.Id, Label = t.Title, Icon = "title:" + t.Id });

            // 0.8: where the Everyone chip works (Chapters/EveryoneModel.cs); on About, the page it was opened from
            var under = state.ShowAbout ? state.PageOf(state.Chapter) ?? (state.Chapter == Chapter.Deeds ? "overview" : ListOf(input, state.Chapter).FirstOrDefault()?.Id) : page;
            var everyoneOff = EveryoneOff(state.Chapter, under, state.ShowAbout, state.PageOf(Chapter.Deeds));
            view.EveryoneSub = everyoneOff?.sub; view.EveryoneWhy = everyoneOff?.why; view.EveryoneTo = everyoneOff?.to;
            if (state.ShowAbout) About(input, page, view);
            else if (ShowsEveryone(input, state, page)) EveryonePage(input, page, state, view);   // the group's page: the total, a row per player, the group's own bar
            else switch (state.Chapter)
            {
                case Chapter.Deeds:
                    Deeds(input, page, titles, view, state);
                    // a fellow's Deeds page keeps every window chip, greyed but All (as Battle): the plate's second line says why
                    if (!input.IsSelf && view.Windows.Count > 0 && view.Windows.Any(c => c.Disabled) && PlateOf(view) is Block deedsPlate)
                        deedsPlate.Text = (string.IsNullOrEmpty(deedsPlate.Text) ? "" : deedsPlate.Text + "\n") + (page == RecentPageId ? RecentWindowsLine(input) : DeedsWindowsLine(input));   // Recent: its own reason (B33)
                    break;
                case Chapter.Company: Company(input, page, state, view); break;
                case Chapter.Stores: Stores(input, page, view); break;
                case Chapter.Battle: Battle(input, page, state, view); break;
                case Chapter.Voyages: Voyages(input, page, view, state); break;
                case Chapter.Feats: FeatsChapter(input, page, view, state); break;   // Chapters/FeatsModel.cs
                default: Skills(input, page, state, view); break;
            }
            if (view.Blocks.Count == 0) view.Blocks.Add(new Block { Kind = "empty", Title = NotYetRecorded });
            // the chips say the window short (10 min, 7 days, All): the heading says it in full ("Defence, last 7 days"); Battle says it on
            // every window, the day-window pages only when it is not All (there All is the page as it always was)
            // a fellow's last shared session is said on the plate's first line with its date ("Tor, last shared session, 8 Oct 00:10"): the heading
            // stays the page's name, so it is not cut by the nine chips
            if (view.HasFilters && view.ShownWindow.HasValue && (state.Chapter == Chapter.Battle || view.ShownWindow != TimeWindow.SinceInstall) && (input.IsSelf || view.ShownWindow != TimeWindow.Session)
                && !(view.Recorded && view.ShownWindow == TimeWindow.SinceInstall)   // a 0.7 page on All: the automatic "Recorded from" label speaks instead (rule W.2)
                && !view.EveryoneOn)   // 0.8: the "Everyone" tag stands before the title and the chosen chip says the window (PICKS: the heading never truncates)
                view.HeadingWindow = WindowLabelFor(input, view.ShownWindow.Value).ToLowerInvariant();
            if (view.HasFilters && view.ShownWindow.HasValue && view.ShownWindow != TimeWindow.SinceInstall && state.Chapter != Chapter.Battle) view.Windowed = true;   // a window, not since install: no "since install" label
            // a plate's title badges go in the strip at its top with their reason (FeatsFinish, B18), no longer in the heading row's pill
            var plate = PlateOf(view);
            // a greyed day window that was pressed (B17): the page shows All, one line on the plate says from when the chosen window works
            var waitLine = view.HasFilters && view.Windows.Any(c => c.Waits && c.Id == state.Window.ToString()) ? WaitLine(input, state.Window) : null;
            if (asked.WaitView != null && asked.WaitAt == null) asked.WaitAt = place;   // Together's greyed day chip: its line holds on this page only
            if (waitLine != null && plate != null) plate.Text = string.IsNullOrEmpty(plate.Text) ? waitLine : plate.Text + "\n" + waitLine;
            TagSources(view);
            // a 0.7 page (view.Recorded, RecordedModel.cs) dates what this PC counted; the others keep "since install" until they migrate
            if (view.Recorded) PlaceRecordedFrom(input, view, state.Chapter);

            // every key that works on this page, short (Joost's live test: the line was incomplete); the arrow keys work too,
            // the README and About say so, the footer stays short
            view.Keys.Add("[Q/E·A/D] Chapter");
            if (view.List.Count > 1) view.Keys.Add("[W/S] Page");
            if (view.Toggle.Count > 0) view.Keys.Add("[A/D] Direction");
            var together = TogetherOf(view);
            if (!state.ShowAbout && together != null && TogetherCategories(together).Count > 1)   // fix4-rest: Together's chips have their keys: the view key flips the category, the filter key the window under Damage dealt
            {
                if (!string.IsNullOrEmpty(state.ViewKey)) view.Keys.Add("[" + state.ViewKey + "] Category");
                if (!string.IsNullOrEmpty(state.FilterKey) && TogetherWindowSwitch(together) != null) view.Keys.Add("[" + state.FilterKey + "] Window");
            }
            else if (!state.ShowAbout && !string.IsNullOrEmpty(state.ViewKey) && (SwitchOf(view) != null || (view.HasFilters && view.Windows.Count(w => !w.Disabled) > 1))) view.Keys.Add("[" + state.ViewKey + "] " + (SwitchOf(view) != null ? "View" : "Window"));
            // fix4 (Deeds): the view key's cap sits at the switch it turns (Earned / Unsung), as the filter key's cap sits at the filter
            if (!state.ShowAbout && !string.IsNullOrEmpty(state.ViewKey) && view.Active == Chapter.Deeds && SwitchOf(view) is Block viewSwitch) viewSwitch.KeyCap = state.ViewKey;
            if (!state.ShowAbout && !string.IsNullOrEmpty(state.FilterKey) && FacetRows(view).Count > 0) view.Keys.Add("[" + state.FilterKey + "] Filter");
            // T opens the About page; there, T or Esc goes back to the page and the hotkey still closes the panel
            if (!state.ShowAbout) view.Keys.Add("[Backspace] Back");   // to the page you came from
            var info = string.IsNullOrEmpty(state.InfoKey) ? null : state.InfoKey;
            if (state.ShowAbout) view.Keys.Add("[" + (info == null ? "Esc" : info + "/Esc") + "] Back");
            else if (info != null) view.Keys.Add("[" + info + "] About");
            NumbersKeyLine(state, view);   // "[Y] About these numbers", right before the About key, on a page that has the box (RecordedModel.cs)
            // the close keys: the hotkey, Tab (unless Tab is the filter key: PanelUi.TabCloses), and Esc on the pages (About goes back with Esc)
            var closeKeys = new List<string>();
            if (!string.IsNullOrEmpty(state.Hotkey)) closeKeys.Add(state.Hotkey);
            if (state.FilterKey != "Tab") closeKeys.Add("Tab");
            if (!state.ShowAbout) closeKeys.Add("Esc");
            if (closeKeys.Count > 0) view.Keys.Add("[" + string.Join("/", closeKeys) + "] Close");
            if (view.Recorded) OneIncompletePerPage(PlateOf(view)?.Items ?? view.Blocks);   // "Earlier counts may be incomplete." once per page (RecordedModel.cs)
            FeatsFinish(input, state, view);   // the feat band on an owner page, the gold dots, the Feats page's keys (Chapters/FeatsModel.cs)
            FoesFinish(state, view);   // 0.8: the Foes page's cursor and its keys (Chapters/FoesKeys.cs)
            GrowthLine(input, view);   // 0.8: the growth line from the day history beside the page's hero (Chapters/GrowthLines.cs)
            PlaceAboutNumbers(state, view);   // the page's "About these numbers" button, and its box when open, under the hero (RecordedModel.cs)
            ColorPeople(view);
            FilterFocusFix(state, view);   // the filter focus (FacetModel.cs)
            if (!ReferenceEquals(asked, state)) { asked.FilterRow = state.FilterRow; asked.FilterCursor = state.FilterCursor; }   // the fellow's window copy: only the focus is the caller's
            return view;
        }

        static Block Stat(string icon, string value, string title, string text, string note) => new Block { Kind = "stat", Icon = icon, Value = value, Title = title, Text = text, Note = note };

        /// <summary>The source tag a shown source label stands for (null: the label says nothing about the source).</summary>
        public static string TagOf(string note)
        {
            if (note == SourceCharacter) return TagCharacter;
            if (note == SourceFellows) return TagFellows;
            if (note == SourceSession || note == SourceTheirLast || note == "measured on your PC" || note == AfterResistance || note == AfterTheirs || note == BeforeResistance) return TagMeasured;
            return null;
        }
        public const string AfterResistance = "after your armor", AfterTheirs = "after their armor", BeforeResistance = "before the foe's armor";

        // every block whose label names a source gets the machine-readable tag; blocks set explicitly keep theirs
        // and every row inside a block carries its block's tag, so a source mark can sit on any single number
        static void TagSources(PanelView view)
        {
            void Down(Block b, string parent)
            {
                if (b.Source == null) b.Source = TagOf(b.Note) ?? parent;
                if (b.Src == null) b.Src = SrcOf(b.Source);
                if (IsSourceSentence(b.Note)) b.Note = null;   // the mark says it now (Joost 2026-10-08)
                foreach (var i in b.Items ?? new List<Block>()) Down(i, b.Source);
            }
            foreach (var b in view.Blocks) Down(b, null);
            view.HeadingSrc = SrcOf(view.HeadingSource);
        }

        public const string SrcCharacter = "character", SrcPc = "pc", SrcFellows = "fellows";
        /// <summary>0.6: counted by the server you play on (cargo loaded and unloaded, born near). Neither your character's nor this PC's,
        /// so its blocks stay outside the two zones, with their own line saying the server counted them.</summary>
        public const string SrcServer = "server", TagServer = "server";
        /// <summary>The source mark for a source tag: character -> tally, measured -> flame (this PC), fellows -> two shields.</summary>
        public static string SrcOf(string tag) => tag == TagCharacter ? SrcCharacter : tag == TagMeasured ? SrcPc : tag == TagFellows ? SrcFellows : tag == TagServer ? SrcServer : null;
        static string TagOfSrc(string src) => src == SrcCharacter ? TagCharacter : src == SrcPc ? TagMeasured : src == SrcFellows ? TagFellows : src == SrcServer ? TagServer : null;
        /// <summary>A note that only says where the number comes from: the source mark replaces it. Notes that say anything
        /// else ("before the foe's armour", "after your armour") stay.</summary>
        public static bool IsSourceSentence(string note) =>
            note == SourceCharacter || note == SourceSession || note == SourceTheirLast || note == SourceFellows || note == "measured on your PC";

        /// <summary>The one label a number Hearthwoven counted on this PC gets (VOCABULARY.md word list, 2026-10-08).</summary>
        public const string SinceInstallLabel = "since install";

        // the source marks of a block and of everything under it that shows a number
        static IEnumerable<string> Srcs(Block b) => new[] { b.Src }.Concat((b.Items ?? new List<Block>()).SelectMany(Srcs)).Where(s => s != null);
        static bool AllPc(Block b) { var s = Srcs(b).ToList(); return s.Count > 0 && s.All(x => x == SrcPc); }

        static Block Section(string title) => new Block { Kind = "section", Title = title };
        /// <summary>"in the Swamp": the biome a Battle overview is narrowed to, said in the legend and over what hurt you (null: all biomes).</summary>
        public static string ScopeOf(string biome) => string.IsNullOrEmpty(biome) ? null : "in the " + BiomeName(biome);
        public const string BlocksLink = "Blocks and parries";   // Defence has the window set now (HISTORY-06): the link no longer names a scope

        /// <summary>A Battle window with nothing in it yet: says the window (the page heading's own) and, in one line, where more is (B19: one calm empty state, no "since install").</summary>
        static Block WindowEmpty(PanelInput input, TimeWindow window)
        {
            var session = window == TimeWindow.Session;
            var b = Empty(session ? NothingYet + " this session" : window == TimeWindow.Today ? "Nothing today" : "Nothing in the " + WindowLabel(window).ToLowerInvariant(),
                          input.IsSelf ? (session ? "Fight something and it fills up." : DayEmptyLine)
                                       : "Choose Session to see " + Name(input) + "'s last session.");
            b.Tone = SinceInstallTone; return b;
        }
        static Block Empty(string title, string text = null) => new Block { Kind = "empty", Title = title, Text = text };
        // a section that only counts since install, still empty: "Nothing yet" and one line with the next step
        // ("Hearthwoven counts battles from the moment you installed it. Keep playing: ... shows up here."); a fellow's copy
        // says whose install it is and leaves out the advice
        // one empty state everywhere (WORDING.md, tweak a): "Nothing yet" and the same short line that keeps the next step; the
        // Tone marks it as a since-install count for the zones (the title alone no longer tells: every empty says Nothing yet)
        public const string OtherModsTitle = "Other mods' items show too",
                            OtherModsLine = "It reads the game's own lists, so modded items, pieces and creatures appear with their own name and icon.";
        public const string EmptyLine = "Hearthwoven counts this from install. Keep playing and it fills up.";
        static Block SinceInstallEmpty(PanelInput input, string counts, string next)
        {
            var b = Empty(NothingYet, input.IsSelf ? EmptyLine : "Hearthwoven counts this from install on " + Name(input) + "'s PC.");
            b.Tone = SinceInstallTone; return b;
        }
        public const string SinceInstallTone = "install";
        const string BattleNext = "the damage you deal and receive shows up";

        // the title's own lines on its owner page, each with its source
        static void TitleLines(PanelView view, List<TitleRow> titles, string id)
        {
            var t = titles.FirstOrDefault(x => x.Id == id);
            if (t == null) return;
            foreach (var l in t.Lines) view.Blocks.Add(Stat("", "", l.Key, null, l.Value));
        }

        static Block Rows(IEnumerable<KeyValuePair<string, double>> items, Func<string, string> label, Func<string, string> icon, string note, int top = RowTop, bool byName = false, bool keepOrder = false)
        {
            var list = items.Where(kv => kv.Value > 0);
            if (!keepOrder) list = byName ? list.OrderBy(kv => label(kv.Key), StringComparer.OrdinalIgnoreCase) : list.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal);
            var rows = list.Take(top).Select(kv => new Block { Icon = icon(kv.Key), Title = label(kv.Key), Value = N(kv.Value) }).ToList();
            return rows.Count == 0 ? null : new Block { Kind = "rows", Items = rows, Note = note };
        }

        static Block Tiles(IEnumerable<KeyValuePair<string, double>> items, PanelInput input, string note, bool counts = true)
        {
            var tiles = items.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(TileTop)
                             .Select(kv => new Block { Icon = "item:" + kv.Key, Title = Who(input, kv.Key), Value = counts ? N(kv.Value) : "" }).ToList();
            return tiles.Count == 0 ? null : new Block { Kind = "tiles", Items = tiles, Note = note };
        }

        static void Add(PanelView view, Block b) { if (b != null) view.Blocks.Add(b); }
        // a section with its rows, or nothing at all when there are no rows
        static void Group(PanelView view, string title, Block rows) { if (rows == null) return; view.Blocks.Add(Section(title)); view.Blocks.Add(rows); }
        public const int GatherTop = 12;

        public const string AtLeast = "at least";   // never shown (Joost 2026-10-08): only the tests use it, to keep it out
        static IEnumerable<KeyValuePair<string, double>> D(IDictionary<string, float> d) => (d ?? new Dictionary<string, float>()).Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value));
        static IEnumerable<KeyValuePair<string, double>> Counters(PanelInput i, params (string stat, string label)[] stats) =>
            stats.Select(s => new KeyValuePair<string, double>(s.label, C(i, s.stat)));

        // ---------- About (InfoKey): how Hearthwoven works, one page instead of a line on every page ----------

        public const string AboutHeading = "How Hearthwoven counts";
        public static readonly (string id, string label)[] AboutList = { ("counts", "How it counts"), ("reads", "What it reads"), ("sharing", "Sharing") };
        /// <summary>The words of "What it reads" and "Sharing" (trust-05's honest text, short; "fellow players", never "friend").</summary>
        public static readonly (string label, string text)[] AboutRows =
        {
            ("Your character's own count", "The game's counters, kept with your character in every world, also from before Hearthwoven: trees felled, pieces built, crafts, foes defeated. They can miss some work: what you do where a fellow player's PC hosts the area, and items you pick up onto a stack you already carry."),
            ("Hearthwoven on this PC", "Counts from the day you installed it: damage, blocks and parries, repairs, smelting, carts, whose food you enjoyed, and exactly what you brought in. Only these start at install. Battle's windows look back from now. Some counts began in a later version: their page says from when."),
            ("Feats", "A moment worth telling, earned by doing it: a rule, a number and a date, never a rank. Unsung ones are not earned yet. A title is earned by its own count; there is no combined score."),
            ("Your armor", "Battle > Defense, What your armor stopped: each hit as it reached your armor and as it left it, on this PC from the day this version first ran. Falls, drowning and cold never meet armor; What hurt you counts them too."),
            // fix4-rest (review: the page opened with Titles and the one thing that matters sat in the third row): what is shared, how to switch it, what stays private, where it goes
            ("What fellow players see", "Fellow players who run Hearthwoven see your counts in their own book: deeds, fights, voyages, skills, feats and the lands you found. You see theirs the same way."),
            ("Turn sharing on or off", "Set ShareWithGroup in your mod manager's config editor (Gale or r2modman). It is on by default, and a change works while the game runs. Off: you see only your own book, and nobody sees yours."),
            ("What stays private", "Where you died and which worlds you played are left out of what fellow players see. Hearthwoven only reads the game and never changes your world."),
            ("Where your counts go", "To the server you play on, when it runs Hearthwoven; a server without it keeps nothing. Switching sharing off stops fellow players seeing your counts, not the server keeping them. Remove Hearthwoven.dll to uninstall it."),
        };
        // 0.7: no "Faded" row (no faded parts are left on a page; REDESIGN-RULES.md part 5, G8, SOURCE-MATRIX row 136)
        static readonly string[] ReadsRows = { "Your character's own count", "Hearthwoven on this PC", "Feats", "Your armor" }, SharingRows = { "What fellow players see", "Turn sharing on or off", "What stays private", "Where your counts go" };
        // each row of What it reads and Sharing carries the mark of the card it belongs to on How it counts (fix-rest: a wall of text had no scan structure)
        static readonly Dictionary<string, string> AboutMarks = new Dictionary<string, string>
        {
            ["Your character's own count"] = "vocab:src-stone", ["Hearthwoven on this PC"] = "vocab:src-hearth", ["Feats"] = FeatsIcon, ["Your armor"] = "vocab:block-mark",
            ["What fellow players see"] = "vocab:src-fellows", ["Turn sharing on or off"] = "vocab:promise-optional", ["What stays private"] = "vocab:promise-world", ["Where your counts go"] = "vocab:about-info",
        };

        /// <summary>
        /// About (vocab-about), on its own list. How it counts: the three sources as cards (origins: Icon, Title, Value = since
        /// when, Text = examples, Tone = the source), the "Since when" timeline (sincewhen: one row per source from the
        /// character's making to install to now; Text, Value, Value2 = the captions under it, no dates the mod does not know)
        /// and the four promises in one row (promises: Icon, Title). What it reads and Sharing: the honest words as rows.
        /// </summary>
        static void About(PanelInput input, string page, PanelView view)
        {
            view.Heading = page == "reads" ? "What it reads" : page == "sharing" ? "Sharing" : AboutHeading;
            if (page == "reads" || page == "sharing")
            {
                // readrows: one row per statement, its mark, its title and its words
                view.Blocks.Add(new Block { Kind = "readrows", Items = AboutRows.Where(r => (page == "reads" ? ReadsRows : SharingRows).Contains(r.label))
                    .Select(r => new Block { Kind = "readrow", Icon = AboutMarks[r.label], Title = r.label, Text = r.text }).ToList() });
                Plate(view, "vocab:about-info");
                return;
            }
            var installedOn = StartOf(input, null);   // 0.7 date words: "from 1 October", never "since you installed it"
            view.Blocks.Add(new Block { Kind = "origins", Items = new List<Block> {
                new Block { Kind = "origin", Icon = "vocab:src-stone", Title = "Your character's own count", Value = MadeLine(input), Text = "goes with your character to every world · trees, kills, skills", Tone = SrcCharacter },
                new Block { Kind = "origin", Icon = "vocab:src-hearth", Title = "Hearthwoven on this PC", Value = installedOn.HasValue ? "from " + RecordDate(input, installedOn.Value) : "from the day you added it", Text = "damage · axe hits · whose food you enjoyed", Tone = SrcPc },
                new Block { Kind = "origin", Icon = "vocab:src-fellows", Title = "Your fellow players' PCs", Value = "when they share too", Text = "who put your gear to good use", Tone = SrcFellows } } });
            // 0.7: the dates the mod knows (the character's making, the install), in the player's words; a fellow's book carries neither
            var installed = StartOf(input, null);
            var madeCaption = (string.IsNullOrEmpty(input.PlayerName) ? "Your character" : input.PlayerName) + " made" +
                              (input.IsSelf && input.CharacterMade.HasValue && input.CharacterMade.Value.Date != NoCreationDate ? " " + RecordDate(input, input.CharacterMade.Value) : "");   // NoCreationDate: the game's fixed date for old profiles (RecordedModel)
            view.Blocks.Add(new Block { Kind = "sincewhen", Title = "Since when", Text = madeCaption,
                Value = installed.HasValue ? "installed " + RecordDate(input, installed.Value) : "Hearthwoven installed", Value2 = "now", Items = new List<Block> {
                new Block { Kind = "span", Icon = "vocab:src-stone", Title = "Your character", Tone = SrcCharacter },
                new Block { Kind = "span", Icon = "vocab:src-hearth", Title = "This PC", Tone = SrcPc },
                new Block { Kind = "span", Icon = "vocab:src-fellows", Title = "Fellow players", Tone = SrcFellows, Text = "as they last shared it" } } });
            view.Blocks.Add(new Block { Kind = "promises", Items = new List<Block> {
                new Block { Kind = "promise", Icon = "vocab:promise-reads", Title = "Only reads" },
                new Block { Kind = "promise", Icon = "vocab:promise-world", Title = "Your world stays untouched" },
                new Block { Kind = "promise", Icon = "vocab:promise-optional", Title = "Optional for every player" },
                // a true line, not "Works with any mod" (README check for 0.5.0): it reads the game's own lists
                new Block { Kind = "promise", Icon = "vocab:promise-anymod", Title = OtherModsTitle, Text = OtherModsLine } } });
            Plate(view, "vocab:about-info");
        }

        // ---------- Deeds ----------

        static void Deeds(PanelInput input, string page, List<TitleRow> titles, PanelView view, PanelState state)
        {
            view.Scope = RecordedScope(input);   // a fellow's book: whose and when (rule E); each page sets it again
            Func<string, string> named = k => Who(input, k);
            // the day windows (HISTORY-06.md; 0.7: every page but the Overview): a page in a day window reads the window's copy of the input
            // (DeedsWindow), so its own code, filters included, works on the window's rows
            if (page == RecentPageId) { RecentPage(input, view, state); return; }   // Deeds > Recent (Chapters/RecentPage.cs): its own chips and views
            if (page == MealsPageId) { Meals(input, view, state); return; }   // 0.8.1 Deeds > Meals (Chapters/MealsModel.cs): its own chips and its group view
            var offered = WindowsOf(Chapter.Deeds, page);
            // 0.7: the short windows (10 min .. Session) come from this session's DeedLog (DeedsShort), built into the same kind of copy, so the
            // pages and their filters need nothing new. Your own book only: a fellow's copy has no minutes (greyed there, DeedsWindowOpen).
            // A Deeds page shows All until a window is chosen: the book's default Session is Battle's (PanelState.WindowPicked)
            // a fellow's copy has no minutes and no days: their chips stay in the row, greyed (WindowChips), as on Battle (DeedsWindowsLine says why)
            var chosen = state != null && state.Window == TimeWindow.Session && !state.WindowPicked ? TimeWindow.SinceInstall : state?.Window;
            var w = offered != null && state != null ? WindowChips(input, state, view, offered, chosen, DeedsWindowOpen) : TimeWindow.SinceInstall;
            string notShort = null;
            var day = IsDayWindow(w) ? DeedsWindow(input, w) : IsShortWindow(w) ? DeedsShort(input, w, out notShort) : null;
            if (notShort != null)   // no DeedLog yet: the window says why, never the page's all-time numbers
            {
                view.Recorded = true; view.Heading = DeedsList.First(p => p.id == page).label;
                view.Blocks.Add(Empty(NothingYet + (w == TimeWindow.Session ? " this session" : " in the " + WindowLabel(w).ToLowerInvariant()), notShort));
                Plate(view, DeedsList.First(p => p.id == page).icon);
                return;
            }
            if (day != null && (page == "woodcutting" || page == "mining")) { DeedsDay(day, page, view, w); return; }
            if (DeedsPage(day ?? input, page, view, state)) { if (day != null) DeedsDayLines(input, day, page, view, w); return; }   // Overview, Cooking, Building, Groundwork, Crafting, Farming, Fishing, Taming: Chapters/DeedsModel.cs
            // Woodcutting and Mining (the composition pages of slice 1) stay here
            switch (page)
            {
                case "woodcutting":
                    // 0.7 (REDESIGN-RULES.md part 3, the worked page): one recorded total with its breakdown, no zones. The hero is the trees
                    // felled (rule A: the game's count when Hearthwoven first ran plus every tree counted since) with the axe hits beside it
                    // (rule C: this PC only, "from 1 October"), the Wood Cutting skill on the right (rule K); the brought-in bar (rule A, the sum
                    // per wood); the per-tree lists in their own sections, "Recorded from 1 October · this PC" (hard case 1); the About box
                    view.Recorded = true;
                    view.Heading = "Woodcutting";
                    view.Scope = RecordedScope(input);   // a fellow's copy: whose and when, without "since install" (rule E)
                    {
                        var layers = TreesFelledLayers(input);
                        double felled = TreesFelled(input), hits = M(input, e => e.ChopHits);
                        Add(view, Hero((felled > 0 ? N(felled) : null, Label1(felled, "tree felled", "trees felled"), SrcCharacter, layers == null || layers.Value.faded > 0 ? EarlierIncomplete : null),
                                       (hits > 0 ? N(hits) : null, Label1(hits, "axe hit", "axe hits"), SrcPc, null)));
                        SkillBeside(view, input, "WoodCutting");
                        Add(view, BroughtInBar("Wood brought in", input, "wood", named, WoodLookOf(input), recorded: true));
                        TreesFelledStrip(view, input);
                        // r2-refine-woodcutting (approved): each tree kind's picture, name, a bar on one scale and the number, two columns
                        if (hits > 0) Group(view, "Axe hits per tree", Ranking(TreeHits(input), k => k, TreeIcon, SrcPc, top: GatherTop, columns: 2));
                        if (!input.IsSelf && view.Blocks.Count <= 2) view.Blocks.Add(new Block { Kind = "note", Text = Name(input) + "'s shared copy holds only this." });
                        WoodcuttingNumbers(view, input, layers);
                    }
                    Plate(view, "title:woodcutter", RecordedScope(input));
                    return;
                case "mining":
                    // 0.7 (REDESIGN-RULES.md part 1, rules A, C, K and the box; the Woodcutting pattern): the hero is the stone and ore brought in
                    // (rule A: the game's count when Hearthwoven first ran plus every piece counted since), the Pickaxes skill on the right (rule K);
                    // the bar under it is the sum per ore, "Earlier counts may be incomplete." once beneath it; the other pickaxe finds stay outside
                    // that total; the pickaxe hits (rule C: this PC only, "from 1 October") beside the hero as on Woodcutting (the hero row wraps when it
                    // is full, HeroLines), their per-rock list in its own section; About these numbers
                    view.Recorded = true;
                    view.Heading = "Mining";
                    view.Scope = RecordedScope(input);   // a fellow's copy: whose and when (rule E)
                    {
                        var brought = BroughtInBar("Stone and ore brought in", input, "mining", named, k => (null, null), recorded: true);   // each ore in its own icon's colour
                        var pick = BroughtIn(input, "mining");
                        double pickaxeHits = M(input, e => e.PickaxeHits);
                        bool earlier = pick.Values.Any(v => v.before > 0) || !HasPickupBaseline(input);
                        // the hero is the stone and ore total, the pickaxe hits beside it, the skill on the right (rule K); the row wraps when it is full
                        Add(view, Hero((brought?.Value, "stone and ore brought in", SrcCharacter, null), (pickaxeHits > 0 ? N(pickaxeHits) : null, Label1(pickaxeHits, "pickaxe hit", "pickaxe hits"), SrcPc, null)));
                        SkillBeside(view, input, "Pickaxes");
                        if (brought != null) { brought.Title = null; brought.Value = null; brought.Text = null; Add(view, brought); }
                        if (earlier && brought != null) view.Blocks.Add(new Block { Kind = "note", Text = EarlierIncomplete });   // rule A.3: beside the bar, where the worked page's one line stands
                        Add(view, PickaxeFindsKey(input, named));
                        var rocks = pickaxeHits > 0 ? Ranking(D(input.Events.PickaxeHits), named, RockIcon, SrcPc, top: GatherTop, columns: 2) : null;
                        if (rocks != null) Group(view, "Pickaxe hits per rock", rocks);
                        MiningNumbers(view, input);
                    }
                    Plate(view, "title:miner", RecordedScope(input));
                    return;
            }
        }

        /// <summary>
        /// Woodcutting or Mining in a day window (HISTORY-06.md), from the window's copy of the input: what Hearthwoven counted those days
        /// (brought in, trees felled, hits), all exact, nothing faded; no title numbers (a title is earned by its lifetime count).
        /// </summary>
        static void DeedsDay(PanelInput src, string page, PanelView view, TimeWindow w)
        {
            Func<string, string> named = k => Who(src, k);
            if (page == "woodcutting")
            {
                view.Recorded = true;   // 0.7: no zones; the window's name is the label (rule W.1), the skill beside the hero (rule K.5)
                view.Heading = "Woodcutting";
                double felled = TreesFelledByKind(src).Values.Sum(), hits = M(src, e => e.ChopHits);
                Add(view, Hero((felled > 0 ? N(felled) : null, felled == 1 ? "tree felled" : "trees felled", SrcPc, null), (hits > 0 ? N(hits) : null, hits == 1 ? "axe hit" : "axe hits", SrcPc, null)));
                SkillBeside(view, src, "WoodCutting");
                Add(view, BroughtInBar("Wood brought in", src, "wood", named, WoodLookOf(src)));
                TreesFelledStrip(view, src);
                if (hits > 0) Group(view, "Axe hits per tree", Ranking(TreeHits(src), k => k, TreeIcon, SrcPc, top: GatherTop, columns: 2));
                if (view.Blocks.Count == 0) view.Blocks.Add(DayEmpty(src, w));
                if (src.Window.Clipped) view.Blocks.Add(new Block { Kind = "note", Text = ClippedLineOf(src.Window) });
                Plate(view, "title:woodcutter");
                return;
            }
            view.Recorded = true;   // 0.7: no zones; the window's name is the label (rule W.1), the skill beside the hero (rule K.5)
            view.Heading = "Mining";
            var brought = BroughtInBar("Stone and ore brought in", src, "mining", named, k => (null, null), recorded: true);
            double rockHits = M(src, e => e.PickaxeHits);
            if (brought != null) { Add(view, Hero((brought.Value, "stone and ore brought in", SrcPc, null))); brought.Title = null; brought.Value = null; brought.Text = null; Add(view, brought); }
            SkillBeside(view, src, "Pickaxes");
            Add(view, PickaxeFindsKey(src, named));
            if (rockHits > 0) Group(view, "Pickaxe hits per rock", Ranking(D(src.Events.PickaxeHits), named, RockIcon, SrcPc, top: GatherTop, columns: 2));
            if (view.Blocks.Count == 0) view.Blocks.Add(DayEmpty(src, w));
            if (src.Window.Clipped) view.Blocks.Add(new Block { Kind = "note", Text = ClippedLineOf(src.Window) });
            Plate(view, "title:miner");
        }

        // trees felled per tree kind, counted by Hearthwoven since install (rows, like the axe hits), in a section of its own with its
        // own total: never the breakdown of the hero's trees felled, which also holds the game's count from before (hard case 1)
        static void TreesFelledStrip(PanelView view, PanelInput input)
        {
            var rows = Ranking(TreesFelledByKind(input), k => k, TreeIcon, SrcPc, top: GatherTop, columns: 2);   // as the axe hits: picture, bar, number
            if (rows == null) return;
            // the layered whole first (your character's count: the stone zone), then the heading and the rows (counted on this PC:
            // the ember zone keeps its heading with them)
            // 0.7: the hero carries "Earlier counts may be incomplete." for the trees felled (rule A.3); this list is Hearthwoven's own, its own section
            view.Blocks.Add(Section("Trees felled per tree", TreesFelledByKind(input).Values.Sum(), SrcPc));
            view.Blocks.Add(rows);
        }

        /// <summary>
        /// Woodcutting's "About these numbers" (REDESIGN-RULES.md part 3, row 8): before the split date the game's own counts (trees felled,
        /// wood picked up) with what they missed; from it what Hearthwoven counted exactly; the per-tree lists and the axe hits only from the
        /// install. The trees' date is the trees-felled baseline's (StartOf), the wood's the pickup baseline's: when they differ, each half
        /// says its own date (hard case 4). Own book only; nothing without a baseline of either.
        /// </summary>
        static void WoodcuttingNumbers(PanelView view, PanelInput input, (double faded, double solid)? layers)
        {
            if (!input.IsSelf || (layers == null && !HasPickupBaseline(input))) return;
            var wood = BroughtIn(input, "wood");
            double treesBefore = layers?.faded ?? 0, treesSince = layers?.solid ?? 0, woodBefore = wood.Values.Sum(v => v.before), woodSince = wood.Values.Sum(v => v.exact);
            var treesFrom = StartOf(input, TreesBaseline); var woodFrom = StartOf(input, "pickedUp");
            string Trees(double n) => N(n) + " " + Label1(n, "tree", "trees");
            string Wood(double n) => N(n) + " wood";
            const string Gap = "It missed wood that went onto a stack you were already carrying, and trees felled where another player's PC ran the area, so the real numbers are higher.";
            string before, from;
            if (treesFrom == woodFrom)
            {
                before = "The game's own count: " + Trees(treesBefore) + ", " + Wood(woodBefore) + ". " + Gap;
                from = "Hearthwoven counted every tree you felled and every piece you picked up, on this PC: " + Trees(treesSince) + ", " + Wood(woodSince) + " so far.";
            }
            else   // hard case 4: one sentence per date, earliest first
            {
                string When(DateTime? d) => d.HasValue ? RecordDate(input, d.Value) : "install";
                before = "The game's own count: " + Trees(treesBefore) + " before " + When(treesFrom) + ", " + Wood(woodBefore) + " before " + When(woodFrom) + ". " + Gap;
                var parts = new[] { (treesFrom, "From " + When(treesFrom) + ": Hearthwoven counted every tree you felled, " + Trees(treesSince) + " so far."),
                                    (woodFrom, "From " + When(woodFrom) + ": every piece you picked up, " + Wood(woodSince) + " so far.") };
                from = string.Join(" ", parts.OrderBy(x => x.Item1 ?? DateTime.MinValue).Select(x => x.Item2));
            }
            var install = StartOf(input, null);
            AboutNumbers(view, input, treesFrom, before, from, Sentences(install.HasValue ? "Trees per kind and axe hits are only known from " + RecordDate(input, install.Value) + "." : null,
                                                                  RecoveredElsewhere(input, "wood from pieces that came down counts")));
        }

        /// <summary>Woodcutting's and Mining's About box (0.8): from when what a piece dropped counts on Building (Materials recovered), not here.</summary>
        static string RecoveredElsewhere(PanelInput input, string what)
        {
            var from = StartOf(input, LocalTotals.StartRecovered);
            return from.HasValue ? "From " + RecordDate(input, from.Value) + ", " + what + " on Building, not here." : null;
        }
        static string Sentences(params string[] sentences) { var s = string.Join(" ", sentences.Where(x => !string.IsNullOrEmpty(x))); return s.Length > 0 ? s : null; }

        /// <summary>
        /// Mining's "About these numbers" (REDESIGN-RULES.md part 1, box template A; SOURCE-MATRIX "About these numbers, Mining"): before the
        /// pickup baseline's date the game's own pickup count with what it missed; from that date what Hearthwoven counted on this PC; the
        /// pickaxe hits only from the install. Own book only; nothing without the pickup baseline.
        /// </summary>
        static void MiningNumbers(PanelView view, PanelInput input)
        {
            if (!input.IsSelf || !HasPickupBaseline(input)) return;
            var pick = BroughtIn(input, "mining");
            double before = pick.Values.Sum(v => v.before), since = pick.Values.Sum(v => v.exact);
            var install = StartOf(input, null);
            AboutNumbers(view, input, StartOf(input, "pickedUp"),
                "The game's own count: " + N(before) + ". It missed stone and ore that went onto a stack you were already carrying, so you brought in more.",
                "Hearthwoven counted every piece you picked up, on this PC: " + N(since) + " so far.",
                Sentences(install.HasValue ? "Pickaxe hits are only known from " + RecordDate(input, install.Value) + ". The game's own pickaxe count leaves out rocks in an area another player's PC hosted, so the book shows Hearthwoven's." : null,
                     RecoveredElsewhere(input, "stone and ore from pieces that came down count")));
        }

        // ---------- Company: src/Panel/Chapters/CompanyModel.cs ----------

        // ---------- Stores (the Hall chapter): Chapters/VoyagesHallModel.cs ----------

        // ---------- Battle ----------

        static readonly string[] Elemental = { "fire", "frost", "poison", "lightning", "spirit" };
        static string TypeIcon(string type) => Elemental.Contains(type) ? "status:" + type : "";

        static Block Bars(IEnumerable<KeyValuePair<string, float>> items, Func<string, string> label, Func<string, string> tone, Func<string, string> icon, string note, string selected = null, int top = RowTop)
        {
            var list = items.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(top).ToList();
            if (list.Count == 0) return null;
            var max = NiceMax(list[0].Value);   // one zero-based scale for the whole block, to a round number
            return new Block
            {
                Kind = "bars", Note = note, Value = N(max),
                Items = list.Select(kv => new Block { Title = label(kv.Key), Value = N(kv.Value), Fraction = kv.Value / max, Tone = tone(kv.Key), Icon = icon(kv.Key), Selected = selected != null && kv.Key == selected }).ToList(),
            };
        }

        /// <summary>The end of the bar scale: the smallest round number (1, 1.5, 2, 2.5, 3, 4, 5, 6 or 8 times a power of ten) that holds the largest value.</summary>
        public static float NiceMax(float v)
        {
            if (v <= 0) return 1;
            var p = (float)Math.Pow(10, Math.Floor(Math.Log10(v)));
            foreach (var m in new[] { 1f, 1.5f, 2f, 2.5f, 3f, 4f, 5f, 6f, 8f, 10f }) if (m * p >= v - 1e-3f) return m * p;
            return 10 * p;
        }

        static void Battle(PanelInput input, string page, PanelState state, PanelView view)
        {
            // 0.7 (REDESIGN-RULES.md part 1, group G5): every Battle page is a recorded page (no zones, dated numbers, one earlier-counts line)
            view.Recorded = true;
            // 0.8: the feed and the last fight are this session's, on your own book (Chapters/BattleFeedModel.cs): no window chips
            if (page == FeedPage) { view.Scope = input.IsSelf ? "This session · your fights" : "This session"; BattleFeedView(input, view, state); return; }
            if (page == LastFightPage) { view.Scope = input.IsSelf ? "This session · your last fight" : "This session"; BattleLastFight(input, view, state); return; }
            // every Battle page has the one window set (HISTORY-06.md): the log's short windows, the day history's days, All. A day window
            // reads a copy of the input whose totals are the window's rows (InWindow), so it takes the since-install path of each page
            var windowed = page == "overview" || page == "damage" || page == "deaths" || page == "foes" || page == "defense";
            var w = windowed ? WindowChips(input, state, view, AllWindows) : state.Window;
            if (w == TimeWindow.SinceInstall) BattleNumbers(input, view);   // "About these numbers" on All only (BattleModel.cs)
            var day = IsDayWindow(w);
            var src = day ? InWindow(input, w) ?? input : input;
            // Since install (and a day window) is the total this PC folded: damage by foe, cause and type, with no biome and no time, and
            // deaths only counted; so no biome choice on Damage there, and the deaths listed are the ones of this session's log
            var since = w == TimeWindow.SinceInstall || day;
            // the overview keeps its biome strip and biome choice on All and on a day when the per-biome totals are there (BiomeTally)
            var perBiome = since && page == "overview" && src.BiomeSinceInstall != null && !src.BiomeSinceInstall.Empty;
            // the biome is a filter bar choice on the three pages with biomes (BattleFacets.cs: the Overview tiles, the Biome row of Damage and
            // Deaths), never a heading chip, so the window chips keep their place on every page. Damage since install has no biome on its rows
            // (the weapon is not folded per biome): no choice there, one line says so
            var filterId = page == "overview" ? BattleOverviewFilter : page == "damage" ? BattleDamageFilter : page == "deaths" ? BattleDeathsFilter : null;
            var chosen = new HashSet<string>(filterId == null || (page == "damage" && since) ? new List<string>() : Chosen(state, filterId, "biome"));
            var now = input.NowUtc;
            var rows = since ? DamageSinceInstallRows(src) : Damage(input.Log, w, "", now, SessionFrom(input));   // B33: a fellow's Session from your session's start
            var taken = rows.Where(r => r.Dir == "taken").ToList();
            var deaths = Deaths(input.Log, since ? TimeWindow.Session : w, "", now, since ? null : SessionFrom(input));
            if (day) { var start = WindowStartUtc(input, w); deaths = deaths.Where(d => !start.HasValue || d.Time >= start.Value).ToList(); }   // the falls of this session that lie in the day window
            if (windowed)
            {
                // a fellow's book: their last session, and since install when they share it; the other windows stay in the row, greyed
                // (Joost 2026-10-09: shown, not dropped), and the plate's second line says why (SharedWindowsLine)
                // All has no window word (rule W.2: the page's own dates say when); a fellow's All says whose copy and as of when (rule E)
                view.Scope = (chosen.Count > 0 ? string.Join(" and ", chosen.OrderBy(BiomeRank).Select(BiomeName)) + " combat" : "All biomes") + " · all foes" +
                             (w == TimeWindow.SinceInstall || (!input.IsSelf && w == TimeWindow.Session) ? "" : " · " + WindowLabel(w).ToLowerInvariant()) +   // a fellow's Session is said once below
                             (input.IsSelf ? "" : " · " + (w == TimeWindow.SinceInstall ? RecordedScope(input)
                                 : w == TimeWindow.Session && SessionFrom(input).HasValue ? Name(input) + ", " + SinceViewer(input) : Name(input) + ", " + SessionScope(input)));   // B33: their Session is your session's span
            }
            else view.Scope = RecordedScope(input);
            // B33: a fellow who did not play during your session shows no numbers in Session, never as if they had been there
            if (windowed && w == TimeWindow.Session && SessionFrom(input).HasValue && FellowOffThisSession(input))
            {
                view.Heading = view.List.FirstOrDefault(l => l.Selected)?.Label ?? "Battle";
                view.Blocks.Add(Empty(NothingYet + " this session", FellowNotOn(input)));
                return;
            }
            // a fellow's copy can be older than the window: say when it is from, never "nothing happened"
            var cutoff = Cutoff(w, now);
            if (windowed && !input.IsSelf && cutoff.HasValue && (!input.LastRecordedUtc.HasValue || input.LastRecordedUtc.Value.AddMinutes(SpanOf(input)) <= cutoff.Value))
            {
                view.Heading = input.LastRecordedUtc.HasValue ? Name(input) + " last shared " + Local(input, input.LastRecordedUtc.Value).ToString("d MMM HH:mm", Inv) : "No record from " + Name(input) + " yet";
                view.Blocks.Add(Empty(w == TimeWindow.Session ? "Nothing this session" : "Nothing in the " + WindowLabel(w).ToLowerInvariant(), "Choose This session to see " + Name(input) + "'s last session."));
                return;
            }
            // ch-battle (Chapters/BattleModel.cs): Damage, Defense, Deaths, Foes from the approved prototypes
            if (page == "damage") { BattleDamage(src, view, state, page, rows, w); return; }
            if (page == "defense") { BattleDefense(input, src, view, w); ArmourSwitch(input, view, state, page, w); return; }   // 0.7: Received / Your armour (Chapters/ArmourModel.cs)
            if (page == "deaths") { BattleDeaths(input, src, view, state, deaths, w); return; }
            if (page == "foes") { BattleFoes(input, src, view, state, page, w); return; }
            switch (page)
            {
                case "overview":
                    {
                        if (since) { BattleOverviewSince(src, view, state, rows, perBiome, w); return; }
                        // r2-refine-battle: the biome strip leads, its legend carries the window's two totals (no hero: the
                        // tiles already hold the numbers); then what hurt you and the game's hit counters side by side. The
                        // window and biome choices sit in the heading row.
                        // the biome tiles are the filter (BattleFacets.cs): the strip keeps every found biome and shows the chosen ones, the rest narrows
                        var strip = BiomeStrip(input, rows, deaths, chosen);
                        var bar = BiomeFilterBar(state, BattleOverviewFilter, strip, rows, deaths);
                        rows = InBiomes(rows, chosen); deaths = InBiomes(deaths, chosen); taken = rows.Where(r => r.Dir == "taken").ToList();
                        var dealt = DealtRows(rows).Sum(r => r.Amount); var total = taken.Sum(r => r.Amount);   // the same dealt as the Damage page (no tool damage)
                        view.Heading = OverviewHeading;
                        var nothing = dealt == 0 && total == 0 && deaths.Count == 0;
                        if (dealt == 0 && total == 0) view.Blocks.Add(WindowEmpty(input, w));
                        if (strip != null) { strip.Value = dealt > 0 ? N(dealt) : null; strip.Value2 = total > 0 ? N(total) : null; strip.Text = AfterOf(input); strip.Title = ScopeOfSet(chosen); }
                        Add(view, strip);
                        Add(view, bar);
                        // denser (Joost 2026-10-08: the overview fits without scrolling): what hurt you and how you died on the left,
                        // the way to Defense on the right; no divider row. The hit counts are lifetime numbers, not this window's:
                        // they live on Foes (hits on foes, layered) and Defense (hits received), SOURCES.md Battle "H"
                        Columns(view, Stretch(v => { Add(v, Composition(WhatHurt(input) + (ScopeOfSet(chosen) == null ? "" : " " + ScopeOfSet(chosen)), ReceivedByType(taken), TypeName, Look(DamageLook), SrcPc, t => "damage:" + t, AfterOf(input))); if (!nothing) DeathSummary(v, input, deaths); }),
                                      Stretch(v =>
                                      {
                                          // blocks are counted since install, not per biome or window: they live on Defense only
                                          if ((input.Events?.Blocks ?? 0) > 0) v.Blocks.Add(new Block { Kind = "link", Icon = "item:ShieldWood", Title = BlocksLink, Id = "Battle/defense" });
                                      }));
                        // dealt and received per biome live on the biome strip above (dealt only before the foe's armour: after
                        // is not measurable on the attacker's PC, owner trap, FEEDBACK C3)
                        // labelled as advice (review 4: "Bring Poison resistance mead" read as data)
                        foreach (var h in Hints(rows, deaths).Take(HintTop)) view.Blocks.Add(new Block { Kind = "note", Tone = "hint", Text = TipWord + h, Source = TagMeasured });
                        // the window and biome are the heading row's choices; a fellow's copy also says whose it is, on the plate
                        Plate(view, "ui:chapter-battle", PlateText(input, windowed: true));
                        return;
                    }
            }
        }

        static string DeathCause(string k)
        {
            switch (k)
            {
                case "EnemyHit": return "Foes";
                case "PlayerHit": return "Other players";
                case "Undefined": return "Unknown";
                default: return CauseName(k) ?? Prettify(k);
            }
        }

        // deaths in the window: count by damage type, never pinned on an attacker when a damage-over-time ended it
        static void DeathSummary(PanelView view, PanelInput input, List<EventLog.Death> deaths)
        {
            if (deaths.Count == 0) { view.Blocks.Add(new Block { Kind = "note", Text = NoDeaths }); return; }   // one quiet line, not a second headline
            Add(view, DeathRows(input, deaths));   // one aligned row per killer and damage type (Chapters/BattleModel.cs)
        }

        // ---------- Voyages: Chapters/VoyagesHallModel.cs ----------

        // ---------- Skills ----------

        static void Skills(PanelInput input, string page, PanelState state, PanelView view)
        {
            // 0.7 (REDESIGN-RULES.md part 5, G8): the levels are the game's own now (class D); the practice is Hearthwoven's count from the
            // install (class C: "from 8 October" after each share, the practised view's sections "Recorded from ..."); no zones
            view.Recorded = true;
            view.Scope = RecordedScope(input);
            var levels = (input.SkillLevels ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => Math.Floor((double)kv.Value));
            var practised = (input.Events?.SkillPractice ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => (double)kv.Value);
            if (string.IsNullOrEmpty(page) || page == "overview")
            {
                view.Heading = "Skills";
                if (levels.Count == 0 && practised.Count == 0) { view.Blocks.Add(Empty(NotYetRecorded)); return; }
                var most = PracticeShares(input);
                // r2-skills-overview: the ladders fill the plate; what was practised since install is the second view
                Switch(view, state, "overview", "view", null,
                       ("levels", "Levels", Stretch(v => { Add(v, HighestSkill(input)); var lad = Ladders(input); if (lad != null) lad.Text = PractisedKeyText(input); Add(v, lad); })),   // every skill's level now (the old "Levels now" list repeated it)
                       ("practised", "Practiced", most));
                SkillsNumbers(view, input);
                Plate(view, "ui:chapter-skills", RecordedScope(input));
                return;
            }
            view.Heading = SkillName(input, page);
            Add(view, Ladder(input, page));   // the level and the practice since install, once each (no stats repeating them)
            SkillsNumbers(view, input);
            Plate(view, "skill:" + page, RecordedScope(input));   // on the plate like every page (the first in-game snapshots: the ladder sat on the bright world)
        }

        /// <summary>The Skills page's "About these numbers" box (SOURCE-MATRIX "About these numbers, Skills"): the levels are the game's own; the
        /// practice is counted here from the install (StartOf null). Own book only (AboutNumbers); nothing on an empty page.</summary>
        static void SkillsNumbers(PanelView view, PanelInput input)
        {
            if (view.Blocks.Count == 0) return;
            AboutNumbers(view, input, StartOf(input, null), "Not recorded: the game keeps only your level now.",
                         "Hearthwoven counted how much you practiced each skill.", "Levels are the game's own, without food or potion bonuses.");
        }

        /// <summary>The key of the glowing ladders (PanelUi "practised ...: N of M skills"): "practised from 8 October" (hard case 14), "practised on Tor's PC" for a fellow.</summary>
        public static string PractisedKeyText(PanelInput input) => "practiced " + FromShort(input, StartOf(input, null));

        // ---------- the visual vocabulary: composition, biomes, ladders (VOCABULARY.md) ----------

        public const string OverviewHeading = "Battle";
        public const string DealtLabel = "damage dealt", DealtQualifier = BeforeResistance, ReceivedLabel = "damage received", ReceivedQualifier = AfterResistance,
                            DiedHere = "died here", BossDefeated = "boss defeated", WhatHurtYou = "What hurt you", PracticeGained = "of your practice", PracticeHeroLabel = "of your practice went into", PracticeWhere = "Where your practice went",
                            PracticeExplained = "Practice is what the game credits each time a skill is used. Each figure is one skill's share of it.",
                            PractisedKey = "practiced", LevelWord = "level", ProgressTo = "To level";   // the fallback only: the ladders' key carries its date (PractisedKeyText, hard case 14)

        // colour and fill sprite per part (the prototype's colours; the grain sprites are Codex's kit). A part without a look
        // takes the next colour of a neutral palette: recognition only, never a meaning.
        // Wood (Addendum 5, Joost in game 2026-10-08): a neutral grain sprite per wood kind, tinted (multiplied) by the item's own
        // icon colour; any other wood (a mod's) the generic grain. The colours here are Codex's icon samples, standing in
        // only where the game is absent (tests, the preview).
        static readonly Dictionary<string, (string colour, string pattern)> WoodLook = new Dictionary<string, (string, string)>
        {
            ["$item_wood"] = ("#6a5135", "vocab:grain-wood-n"), ["$item_finewood"] = ("#bc9264", "vocab:grain-finewood-n"),
            ["$item_roundlog"] = ("#886140", "vocab:grain-corewood-n"), ["$item_elderbark"] = ("#67664c", "vocab:grain-ancientbark-n"),
            ["$item_yggdrasilwood"] = ("#936b4c", null),
        };
        public const string GenericGrain = "vocab:grain-generic-n";
        /// <summary>A wood part: its icon's own colour (the cached sample) over its neutral grain, the generic grain for any other wood.</summary>
        static Func<string, (string colour, string pattern)> WoodLookOf(PanelInput input)
        {
            var tint = ItemTint(input);
            return k => { WoodLook.TryGetValue(k, out var l); return (tint(k) ?? l.colour, l.pattern ?? GenericGrain); };
        }
        /// <summary>
        /// The item's own colour, as its icon shows it (Joost 2026-10-08: coal near black): in game PanelLook.IconColour reads
        /// the icon once per item (any mod's item too); without the game (tests, the preview) this table stands in for the
        /// sample items, approximating their icons. An item neither knows takes the neutral palette.
        /// </summary>
        static readonly Dictionary<string, string> ItemColourFallback = new Dictionary<string, string>
        {
            ["$item_flametalore"] = "#a8482a", ["$item_flint"] = "#6c6a62", ["$item_resin"] = "#a8641c",
        };
        /// <summary>
        /// Well-known vanilla materials whose icon average misleads (diff-05, Joost 2026-10-08: copper ore read olive, tin ore
        /// dark slate, stone near black): their approved colour wins over the icon sample. Every other item, a mod's too,
        /// keeps its icon's own colour.
        /// </summary>
        public static readonly Dictionary<string, string> MaterialColour = new Dictionary<string, string>
        {
            ["$item_copperore"] = "#c8783a", ["$item_tinore"] = "#d8dcdd", ["$item_ironscrap"] = "#8e4a2a", ["$item_iron"] = "#9a5636",
            ["$item_stone"] = "#a9abae", ["$item_coal"] = "#2a2624", ["$item_wood"] = "#9a6d42", ["$item_finewood"] = "#e2d2a4",
            ["$item_roundlog"] = "#b8703c", ["$item_elderbark"] = "#7e8a6c", ["$item_silver"] = "#c9ced6", ["$item_silverore"] = "#b8bfc8",
            ["$item_blackmetalscrap"] = "#4f5a52", ["$item_blackmetal"] = "#56615a", ["$item_obsidian"] = "#3a2a48",
        };
        static Func<string, string> ItemTint(PanelInput input) => k =>
        {
            if (MaterialColour.TryGetValue(k, out var m)) return m;
            var c = input?.ItemColour?.Invoke(k);
            return !string.IsNullOrEmpty(c) ? c : ItemColourFallback.TryGetValue(k, out var f) ? f : null;
        };
        static readonly Dictionary<string, (string colour, string pattern)> GroundLook = new Dictionary<string, (string, string)>
        {
            ["$piece_levelground"] = ("#3c3125", null), ["$piece_lowerground"] = ("#2a2218", null), ["$piece_raise"] = ("#4d4030", null),
            ["$piece_path"] = ("#4a3d2c", null), ["$piece_pavedroad"] = ("#5a5048", null), ["$piece_cultivate"] = ("#2c3a1c", null), ["$piece_replant"] = ("#3a4a24", null),
        };
        /// <summary>The damage palette (Joost 2026-10-08): physical = neutral metals, elements in their genre colours.</summary>
        public static readonly Dictionary<string, (string colour, string pattern)> DamageLook = new Dictionary<string, (string, string)>
        {
            ["blunt"] = ("#6b7076", null), ["slash"] = ("#aab6c2", null), ["pierce"] = ("#e8dcbc", null), ["fire"] = ("#e2552a", null),
            ["frost"] = ("#2f9bff", null), ["lightning"] = ("#f2cf2e", null), ["poison"] = ("#3ccf6e", null), ["spirit"] = ("#9b6cf0", null),
        };
        static readonly string[] Neutral = { "#8e8070", "#6f7f8a", "#a08a5a", "#7a6a8a", "#5f7a5a", "#8a6a5a" };
        static Func<string, (string colour, string pattern)> Look(Dictionary<string, (string colour, string pattern)> d) => k => d.TryGetValue(k, out var l) ? l : (null, null);

        /// <summary>
        /// "What is it made of?": one proportional bar, a part per kind, largest first; Value = the total, each part's
        /// Fraction = its share (the shares sum to 1). A part's colour: the block's own look (damage palette, groundwork,
        /// wood grain), then tint (an item's own icon colour), then the abstract categories' colour by name (FacetModel.BarColours,
        /// 0.7: the same category the same colour on every page, apart from the others on its bar, never one shared neutral). Past BarMaxParts the smallest kinds
        /// fold into one "Other (n kinds)" part (Id FoldId, no picture) whose Items are the folded parts, so a caller that
        /// rewrites the parts' numbers can sum them. null when there is nothing to show.
        /// </summary>
        public static Block Composition(string title, IDictionary<string, double> parts, Func<string, string> label, Func<string, (string colour, string pattern)> look,
                                        string src, Func<string, string> icon = null, string note = null, Func<string, string> tint = null)
        {
            var list = (parts ?? new Dictionary<string, double>()).Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            if (list.Count == 0) return null;
            var total = list.Sum(kv => kv.Value); var tag = TagOfSrc(src);
            var fold = list.Count > BarMaxParts;
            var shown = fold ? list.Take(BarMaxParts - 1).ToList() : list;
            // an approved look and an item's own colour stay (C1, diff-05: the legend shows the item's picture beside it); a part with
            // neither takes the bars' palette, apart from the others, never one shared neutral (0.6.5)
            var colours = BarColours(shown.Select(kv => { var own = look(kv.Key).colour ?? tint?.Invoke(kv.Key); return (kv.Key, label(kv.Key), own, own != null); }).ToList(), fold);
            Block Part(KeyValuePair<string, double> kv) => new Block
            {
                Id = kv.Key, Icon = icon != null ? icon(kv.Key) : "item:" + kv.Key, Title = label(kv.Key), Value = N(kv.Value), Fraction = (float)(kv.Value / total),
                Colour = colours.TryGetValue(kv.Key, out var c) ? c : null, Pattern = look(kv.Key).pattern, Src = src, Source = tag,
            };
            var items = shown.Select(Part).ToList();
            if (fold)
            {
                var rest = list.Skip(BarMaxParts - 1).ToList(); var n = rest.Sum(kv => kv.Value);
                items.Add(new Block { Id = FoldId, Title = FoldLabel(rest.Count), Value = N(n), Fraction = (float)(n / total), Colour = BarOtherColour, Src = src, Source = tag, Items = rest.Select(Part).ToList() });
            }
            return new Block { Kind = "composition", Title = title, Value = N(total), Note = note, Src = src, Source = tag, Items = items };
        }

        // the journey (Meadows to Deep North), the sea apart at the end; tile colours from the approved strip
        static readonly (string key, string colour)[] BiomeTiles =
        {
            ("Meadows", "#7fa04a"), ("BlackForest", "#3f6b4a"), ("Swamp", "#6b6a3a"), ("Mountain", "#55657a"), ("Plains", "#c9a64a"),
            ("Mistlands", "#7b6a8e"), ("AshLands", "#b0442c"), ("DeepNorth", "#4f7a90"), ("Ocean", "#3d6f96"),
        };
        // the game's bosses, the biome they rule and their trophy; the game's kill counter (since you were made) says defeated
        static readonly (string token, string biome, string trophy, string name)[] Bosses =
        {
            ("$enemy_eikthyr", "Meadows", "TrophyEikthyr", "Eikthyr"), ("$enemy_gdking", "BlackForest", "TrophyTheElder", "The Elder"),
            ("$enemy_bonemass", "Swamp", "TrophyBonemass", "Bonemass"), ("$enemy_dragon", "Mountain", "TrophyDragonQueen", "Moder"),
            ("$enemy_goblinking", "Plains", "TrophyGoblinKing", "Yagluth"), ("$enemy_seekerqueen", "Mistlands", "TrophySeekerQueen", "The Queen"),
            ("$enemy_fader", "AshLands", "TrophyFader", "Fader"),
        };
        public static string BiomeEmblem(string key) => "vocab:biome-" + BiomeName(key).ToLowerInvariant().Replace(" ", "");

        /// <summary>
        /// The biomes the character found, for the biome choice, the strips and the Maps tiles: on your own book the game's own record
        /// (Player.m_knownBiome, the set behind "new biome discovered") plus any biome with evidence in the log; on a fellow's book only
        /// the evidence their copy carries, as before 0.7. A fellow's shared "knownBiomes" opens the group feats' gate only (GroupFound):
        /// it never names a land on their pages that the group did not reach through evidence there (spoiler-safe). Journey order, Ocean last.
        /// </summary>
        public static List<string> FoundBiomes(PanelInput input)
        {
            var found = new HashSet<string>(BiomesSeen(input?.Log));
            if (input != null && input.IsSelf) foreach (var k in input.KnownBiomes ?? new string[0]) found.Add(k);
            return found.OrderBy(b => { var i = Array.FindIndex(BiomeTiles, t => t.key == b); return i < 0 ? 99 : i; }).ThenBy(b => b, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// "Where?": one tile per biome the character found (the game's record, KnownBiomes) or with evidence that you were
        /// there (damage either way or a death in the window, or its boss defeated), in journey order with the Ocean last.
        /// Undiscovered biomes are left out, so nothing is fogged or guessed. Dealt rises (Fraction), received hangs (Fraction2) on
        /// the prototype's scale: received shares dealt's unit unless that would make its bars taller than their band.
        /// </summary>
        public static Block BiomeStrip(PanelInput input, List<DamageRow> rows, List<EventLog.Death> deaths, string selected) =>
            BiomeStrip(input, rows, deaths, string.IsNullOrEmpty(selected) ? new string[0] : new[] { selected });

        /// <summary>The strip with any number of tiles chosen (the overview's Biome filter: Selected on each chosen tile).</summary>
        public static Block BiomeStrip(PanelInput input, List<DamageRow> rows, List<EventLog.Death> deaths, ICollection<string> chosen)
        {
            var dealt = Sum(DealtRows(rows), r => r.Biome).ToDictionary(kv => kv.Key, kv => (double)kv.Value);   // the eight types: tool damage (chop, pickaxe) is never Battle, as on Damage
            var recv = Sum(rows.Where(r => r.Dir == "taken"), r => r.Biome).ToDictionary(kv => kv.Key, kv => (double)kv.Value);
            var dead = deaths.GroupBy(d => d.Biome).ToDictionary(g => g.Key, g => g.Count());
            double maxUp = Math.Max(1, dealt.Values.DefaultIfEmpty(0).Max()), maxDown = Math.Max(1, recv.Values.DefaultIfEmpty(0).Max());
            var scaleDown = Math.Max(maxDown, maxUp / 3);
            var found = new HashSet<string>(FoundBiomes(input));
            var tiles = new List<Block>();
            foreach (var t in BiomeTiles)
            {
                dealt.TryGetValue(t.key, out var dv); recv.TryGetValue(t.key, out var rv); dead.TryGetValue(t.key, out var dn);
                var bosses = new List<Block>();
                foreach (var b in Bosses)
                {
                    if (b.biome != t.key || input.EnemyKills == null || !input.EnemyKills.TryGetValue(b.token, out var kills) || kills <= 0) continue;
                    var shown = input.DisplayName?.Invoke(b.token);
                    bosses.Add(new Block { Kind = "boss", Id = b.token, Icon = "item:" + b.trophy, Title = string.IsNullOrEmpty(shown) || shown.StartsWith("$") ? b.name : shown, Src = SrcCharacter, Source = TagCharacter });
                }
                var known = found.Contains(t.key);   // one source (FoundBiomes): the game's record plus evidence
                if (!known && dv <= 0 && rv <= 0 && dn == 0 && bosses.Count == 0) continue;
                tiles.Add(new Block
                {
                    Kind = "biome", Id = t.key, Title = BiomeName(t.key), Icon = BiomeEmblem(t.key), Colour = t.colour,
                    Tone = t.key == "Meadows" || t.key == "Plains" ? "dark-text" : "light-text",
                    Value = dv > 0 ? N(dv) : "", Fraction = (float)(dv / maxUp), Value2 = rv > 0 ? N(rv) : "", Fraction2 = (float)(rv / scaleDown), Count = dn,
                    Selected = chosen != null && chosen.Contains(t.key), Src = SrcPc, Source = TagMeasured, Items = bosses.Count > 0 ? bosses : null,
                });
            }
            return tiles.Count == 0 ? null : new Block { Kind = "biomes", Items = tiles, Src = SrcPc, Source = TagMeasured };
        }

        /// <summary>Damage received per damage type, tool damage (chop, pickaxe) left out.</summary>
        public static Dictionary<string, double> ReceivedByType(IEnumerable<DamageRow> rows) =>
            Sum(rows.Where(r => r.Dir == "taken" && r.Type != "chop" && r.Type != "pickaxe"), r => r.Type).Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => (double)kv.Value);

        // the skill groups of the ladders overview, in the prototype's order; any other skill (mods) goes under Other
        static readonly (string group, string[] skills)[] SkillGroups =
        {
            ("Fight", new[] { "Swords", "Knives", "Clubs", "Polearms", "Spears", "Blocking", "Axes", "Bows", "Crossbows", "ElementalMagic", "BloodMagic", "Unarmed" }),
            ("Gather", new[] { "Pickaxes", "WoodCutting", "Fishing", "Farming" }),
            ("Move", new[] { "Jump", "Sneak", "Run", "Swim", "Dodge", "Ride" }),   // every vanilla Skills.SkillType has a group
            ("Make", new[] { "Cooking", "Crafting" }),
        };
        static readonly Dictionary<string, (string page, string label)> SkillDeed = new Dictionary<string, (string, string)>
        {
            ["WoodCutting"] = ("Deeds/woodcutting", "Woodcutting"), ["Pickaxes"] = ("Deeds/mining", "Mining"), ["Cooking"] = ("Deeds/cooking", "Cooking"),
            ["Crafting"] = ("Deeds/crafting", "Crafting"), ["Farming"] = ("Deeds/farming", "Farming"), ["Fishing"] = ("Deeds/fishing", "Fishing"),
        };

        /// <summary>The game's progress to the next level from a shared snapshot (level, accumulator), as Skills.Skill.GetLevelPercentage computes it.</summary>
        public static float ProgressOf(float level, float accumulator)
        {
            var need = Math.Pow(Math.Floor(level) + 1, 1.5) * 0.5 + 0.5;
            return (float)Math.Max(0, Math.Min(1, accumulator / need));
        }

        /// <summary>"Which skill is my best?": the highest level as the page's hero (fix-rest: the Levels view had no focal point). Two or more skills only.</summary>
        static Block HighestSkill(PanelInput input)
        {
            var have = (input.SkillLevels ?? new Dictionary<string, float>()).Where(kv => kv.Value >= 1).Select(kv => (key: kv.Key, level: Math.Floor((double)kv.Value))).ToList();
            if (have.Count < 2) return null;
            var best = have.OrderByDescending(x => x.level).ThenBy(x => SkillName(input, x.key), StringComparer.OrdinalIgnoreCase).First();
            var hero = Hero((best.level.ToString("0", Inv), SkillName(input, best.key), SrcCharacter, Their(input) + " highest skill"));
            return hero;   // 0.8 layout D+: one hero form on every page (Woodcutting's); the compact one is gone
        }

        /// <summary>Practice since install as a share (Skills > Practised, the skill page). The game's raise factors have no unit a player
        /// knows (SOURCES: "14.5" meant nothing), so the raw sum is never shown: each skill's part of ALL the practice the game credited
        /// since install, in per cent, and its rank. The shares of the skills shown add up to 100.</summary>
        public static string Share(double part, double whole)
        {
            if (whole <= 0) return "0 %";
            var pct = Math.Round(100 * part / whole);
            return part > 0 && pct == 0 ? LessThanOne + " %" : pct.ToString("0", Inv) + " %";   // a real share below 0.5 % would read as nothing
        }

        static double TotalPractice(PanelInput input) => (input.Events?.SkillPractice ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).Sum(kv => (double)kv.Value);

        static List<KeyValuePair<string, double>> PracticeOrder(PanelInput input) =>
            (input.Events?.SkillPractice ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value))
                .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();

        static string Ordinal(int n) => n + (n % 100 >= 11 && n % 100 <= 13 ? "th" : (n % 10) == 1 ? "st" : (n % 10) == 2 ? "nd" : (n % 10) == 3 ? "rd" : "th");

        /// <summary>"Where did my practice go?": the top skill as the hero, then one bar per skill, each as long as its share of ALL the practice (the whole track is 100 %).</summary>
        static List<Block> PracticeShares(PanelInput input)
        {
            var order = PracticeOrder(input); var blocks = new List<Block>();
            if (order.Count == 0) return blocks;
            var total = order.Sum(kv => kv.Value); var top = order[0];
            var hero = Hero((Share(top.Value, total), (input.IsSelf ? PracticeHeroLabel : "of their practice went into") + " " + SkillName(input, top.Key), SrcPc, null));
            if (hero != null) blocks.Add(hero);
            blocks.Add(Section(input.IsSelf ? PracticeWhere : "Where their practice went"));
            blocks.Add(new Block
            {
                Kind = "ranking", Src = SrcPc, Source = TagOfSrc(SrcPc), Columns = 1,
                Items = order.Select(kv => new Block { Id = kv.Key, Icon = "skill:" + kv.Key, Title = SkillName(input, kv.Key), Value = Share(kv.Value, total), Fraction = (float)(kv.Value / total), Src = SrcPc, Source = TagOfSrc(SrcPc) }).ToList(),   // the share itself (fix2-rest): the track is 100 %, so 42 % no longer fills it
            });
            blocks.Add(new Block { Kind = "note", Text = PracticeExplained });
            return blocks;
        }

        static double Practice(PanelInput input, string skill) => input.Events != null && input.Events.SkillPractice.TryGetValue(skill, out var p) && p > 0 ? p : 0;

        public const string SkillHeading = "Skill", WeaponSkillsHeading = "Weapon skills", SkillStripTone = "strip";

        /// <summary>
        /// The skill that belongs to a page's deed, small at the bottom of its plate (Joost: the skill next to the deed it belongs
        /// to, as Voyages > On foot shows Jump, Run and Swim): the same ladder as Skills > Overview, level and progress, only the
        /// skills your character has, and only on a page that already has something on it.
        /// </summary>
        static void SkillStrip(PanelInput input, PanelView view, string heading, IEnumerable<string> skills, string scope = null)
        {
            if (view.Blocks.Count == 0) return;
            var have = new HashSet<string>(SkillNames(input));
            var items = skills.Where(have.Contains).Select(k => LadderOf(input, k, "ladder")).ToList();
            if (items.Count == 0) return;
            view.Blocks.Add(Tagged(new Block { Kind = "ladders", Tone = SkillStripTone, Items = new List<Block> { new Block { Kind = "group", Title = heading, Text = scope, Items = items } } }, SrcCharacter));   // scope: what the levels are, said beside the heading (Battle)
        }

        /// <summary>A page's own skill strip (no scope line: Woodcutting's Wood Cutting, not Battle's weapon skills): drawn as a small chip, never as a band.</summary>
        public static bool IsHeadStrip(Block b) => b.Kind == "ladders" && b.Tone == SkillStripTone && (b.Items ?? new List<Block>()).All(g => string.IsNullOrEmpty(g.Text));
        /// <summary>The skills a page shows as chips: its head strips' skills (the plate's blocks).</summary>
        public static IEnumerable<Block> HeadSkills(Block plate) =>
            (plate?.Items ?? new List<Block>()).Where(IsHeadStrip).SelectMany(b => b.Items.SelectMany(g => g.Items ?? new List<Block>()));
        /// <summary>
        /// Where the page's own skill chips go: in the heading row (right, before the pill), or, when that row holds the window chips, on one
        /// line at the top of the plate. Joost in game 0.6.2 (2026-10-09): Woodcutting and Mining have window chips, and their skill was drawn
        /// nowhere (the heading row drew only the windows, the plate skipped the head strip).
        /// </summary>
        public static bool HeadSkillsOnPlate(PanelView v) => v != null && v.HasFilters && HeadSkills(PlateOf(v)).Any();

        static Block LadderOf(PanelInput input, string skill, string kind)
        {
            double level = 0;
            if (input.SkillLevels != null && input.SkillLevels.TryGetValue(skill, out var l)) level = Math.Floor((double)l);
            float progress = -1;
            if (level < 100 && input.SkillProgress != null && input.SkillProgress.TryGetValue(skill, out var pr)) progress = Math.Max(0f, Math.Min(1f, pr));
            return new Block
            {
                Kind = kind, Id = skill, Icon = "skill:" + skill, Title = SkillName(input, skill), Value = level.ToString("0", Inv), Level = (float)level,
                Progress = progress, Practised = Practice(input, skill) > 0, Src = SrcCharacter, Source = TagCharacter,
            };
        }

        /// <summary>"How far along?": one ladder per skill (the skills in the left list), grouped Fight, Gather, Move, Make, Other.</summary>
        public static Block Ladders(PanelInput input)
        {
            var have = new HashSet<string>(SkillNames(input));
            var groups = new List<Block>();
            foreach (var g in SkillGroups)
            {
                var items = g.skills.Where(have.Contains).Select(k => LadderOf(input, k, "ladder")).ToList();
                if (items.Count > 0) groups.Add(new Block { Kind = "group", Title = g.group, Items = items });
            }
            var known = new HashSet<string>(SkillGroups.SelectMany(g => g.skills));
            var other = have.Where(k => !known.Contains(k)).OrderBy(k => SkillName(input, k), StringComparer.OrdinalIgnoreCase).Select(k => LadderOf(input, k, "ladder")).ToList();
            if (other.Count > 0) groups.Add(new Block { Kind = "group", Title = "Other", Items = other });
            return groups.Count == 0 ? null : new Block { Kind = "ladders", Items = groups, Src = SrcCharacter, Source = TagCharacter };
        }

        /// <summary>"Where am I on this skill?": one large ladder; practice since install and the deed page as child items.</summary>
        public static Block Ladder(PanelInput input, string skill)
        {
            var b = LadderOf(input, skill, "ladder"); var practice = Practice(input, skill);
            if (b.Level <= 0 && practice <= 0 && b.Progress <= 0) return null;
            if (b.Level < 100) b.Value2 = (b.Level + 1).ToString("0", Inv);
            b.Items = new List<Block>();
            if (practice > 0)
            {
                // the share of all practice since install and the rank among the skills practised (the raw sum has no unit a player knows)
                var order = PracticeOrder(input); var rank = order.FindIndex(kv => kv.Key == skill) + 1;
                b.Items.Add(new Block { Kind = "practice", Title = input.IsSelf ? PracticeGained : "of their practice", Value = Share(practice, TotalPractice(input)), Src = SrcPc, Source = TagMeasured,
                                        Note = order.Count < 2 ? (input.IsSelf ? "the only skill you practiced" : "the only skill practiced") : rank == 1 ? "the most of any skill" : Ordinal(rank) + " of " + order.Count + " skills" });
            }
            if (SkillDeed.TryGetValue(skill, out var deed)) b.Items.Add(new Block { Kind = "link", Id = deed.page, Title = deed.label });
            return b;
        }

        // ---------- page layout: plate, hero, columns, switch, cards (slice 3; the prototypes' vocab.css .hrow/.plate/.hero2/
        // .cols2, r4battle's view switch, r4over's deed cards). Layout only: every number inside keeps its own Src. ----------

        /// <summary>Layout boxes hold blocks of the page (plate, columns, column, switch, view); their Items are not parts of one number.</summary>
        public static bool IsBox(Block b) => b != null && (b.Kind == "plate" || b.Kind == "columns" || b.Kind == "column" || b.Kind == "switch" || b.Kind == "view" || b.Kind == "zone");

        /// <summary>The page's plate when the page sits on one (it is then the page's only block), else null.</summary>
        public static Block PlateOf(PanelView v) => v != null && v.Blocks.Count == 1 && v.Blocks[0].Kind == "plate" ? v.Blocks[0] : null;

        /// <summary>Every block of the page in reading order, the layout boxes included and opened (a switch: its chosen view only).</summary>
        public static List<Block> Content(PanelView v)
        {
            var all = new List<Block>();
            void Open(IEnumerable<Block> bs) { foreach (var b in bs ?? Enumerable.Empty<Block>()) { all.Add(b); if (IsBox(b)) Open(b.Items); } }
            Open(v?.Blocks);
            return all;
        }

        /// <summary>
        /// The page on the dark recessed plate (vocab.css .plate): the heading row carries the page title with its icon, on the
        /// list title's line, and the plate starts where the list starts; everything the page built so far moves inside.
        /// text: one quiet line at the top of the plate (a fellow player's copy says whose it is and when). Build adds the pill.
        /// </summary>
        static void Plate(PanelView view, string icon, string text = null)
        {
            var items = new List<Block>(view.Blocks);
            if (items.Count == 0) items.Add(Empty(NotYetRecorded));
            view.Blocks.Clear();
            view.Blocks.Add(new Block { Kind = "plate", Title = view.Heading, Icon = icon, Text = text, Items = items });
        }

        /// <summary>
        /// "How much?", large (vocab.css .hero2): the first number big with its label (and a quiet qualifier, note); any
        /// further number rides on the right, smaller (Items, Kind "number"). Each keeps its own Src, so "since install"
        /// lands only on numbers counted on this PC. Numbers without a value drop out; none gives null.
        /// </summary>
        public static Block Hero(params (string value, string label, string src, string note)[] numbers)
        {
            var list = (numbers ?? new (string, string, string, string)[0]).Where(n => !string.IsNullOrEmpty(n.value)).ToList();
            if (list.Count == 0) return null;
            Block Num(string kind, (string value, string label, string src, string note) n) =>
                new Block { Kind = kind, Value = n.value, Title = n.label, Note = n.note, Src = n.src, Source = TagOfSrc(n.src) };
            var hero = Num("hero", list[0]);
            if (list.Count > 1) hero.Items = list.Skip(1).Select(n => Num("number", n)).ToList();
            return hero;
        }

        /// <summary>"410 trees felled" -> ("410", "trees felled"); a line that does not start with a number -> (null, line).</summary>
        public static (string value, string label) SplitNumber(string line)
        {
            var m = System.Text.RegularExpressions.Regex.Match(line ?? "", @"^(\d[\d,. ]*) +(.+)$");
            return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : (null, line);
        }

        // a title's lines, your character's count first (it leads on the page and on a card), each split into number and label
        static List<(string value, string label, string src)> TitleNumbers(TitleRow t) =>
            t.Lines.Select((l, i) => (l, i)).OrderBy(x => x.l.Value == SourceCharacter ? 0 : 1).ThenBy(x => x.i)
             .Select(x => { var s = SplitNumber(x.l.Key); return (s.value, s.label, SrcOf(TagOf(x.l.Value))); }).ToList();

        /// <summary>Stretches of blocks side by side (vocab.css .cols2, the hits grid). Empty stretches drop out; a single one
        /// stands inline, without the box.</summary>
        static void Columns(PanelView view, params List<Block>[] columns)
        {
            var cols = columns.Where(c => c != null && c.Count > 0).ToList();
            if (cols.Count == 0) return;
            if (cols.Count == 1) { view.Blocks.AddRange(cols[0]); return; }
            view.Blocks.Add(new Block { Kind = "columns", Items = cols.Select(c => new Block { Kind = "column", Items = c }).ToList() });
        }

        // a stretch of blocks built with the page helpers (Add, Group, ...), for a column or a view
        static List<Block> Stretch(Action<PanelView> build) { var v = new PanelView(); build(v); return v.Blocks; }

        public const string ViewTarget = "view:";

        /// <summary>
        /// A view switch (r4battle's chips, top right): named views of the same page, one shown. Only the chosen view carries
        /// its blocks. The choice lives in PanelState.View under the switch's Id (chapter/page/name), so every page keeps its
        /// own; a chip's click or the view key changes it. Views without blocks drop out; one left over stands inline.
        /// </summary>
        static void Switch(PanelView view, PanelState state, string page, string name, string caption, params (string id, string label, List<Block> blocks)[] views)
        {
            var have = views.Where(x => x.blocks != null && x.blocks.Count > 0).ToList();
            if (have.Count == 0) return;
            if (have.Count == 1) { view.Blocks.AddRange(have[0].blocks); return; }
            var key = state.Chapter + "/" + page + "/" + name;
            var pick = state.View.TryGetValue(key, out var p) && have.Any(x => x.id == p) ? p : have[0].id;
            view.Blocks.Add(new Block
            {
                Kind = "switch", Id = key, Title = caption,
                Items = have.Select(x => new Block { Kind = "view", Id = x.id, Title = x.label, Selected = x.id == pick, Items = x.id == pick ? x.blocks : null }).ToList(),
            });
        }

        /// <summary>The click target of a switch's chip.</summary>
        public static string ViewLink(Block sw, Block view) => ViewTarget + sw.Id + "=" + view.Id;

        /// <summary>Follows a click: "view:&lt;switch&gt;=&lt;view&gt;" picks that view; anything else jumps to a page ("Battle/defense").</summary>
        public static void Follow(PanelState s, string target)
        {
            if (FollowFacet(s, target)) return;   // a filter bar's chip, token, bar part or Clear all (FacetModel.cs)
            if (target == NumbersTarget) { s.ShowNumbers = !s.ShowNumbers; return; }   // the "About these numbers" button (RecordedModel.cs)
            if (target == CompareTarget) { s.Compare = !s.Compare; s.CompareTip = false; return; }   // the Compare chip (CompareModel.cs)
            if (target != null && target.StartsWith(ViewTarget, StringComparison.Ordinal))
            {
                var t = target.Substring(ViewTarget.Length); var eq = t.LastIndexOf('=');
                if (eq > 0) s.View[t.Substring(0, eq)] = t.Substring(eq + 1);
                return;
            }
            Jump(s, target);
        }

        /// <summary>The page's view switch: its first switch box, else the one Together carries under its category chips.</summary>
        public static Block SwitchOf(PanelView v) =>
            Content(v).FirstOrDefault(b => b.Kind == "switch") ?? Content(v).Where(b => b.Kind == "together").SelectMany(b => b.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == "switch");

        /// <summary>The view key: the page's first switch moves to its next view (wraps). false when the page has none.</summary>
        /// <summary>Company > Together's block (its category chips and, under Damage dealt, the window chips), or null on any other page.</summary>
        public static Block TogetherOf(PanelView v) => Content(v).FirstOrDefault(b => b.Kind == "together");
        static List<Block> TogetherCategories(Block together) => (together.Items ?? new List<Block>()).Where(c => c.Kind == "category").ToList();
        static Block TogetherWindowSwitch(Block together) => (together.Items ?? new List<Block>()).FirstOrDefault(c => c.Kind == "switch" && c.Items != null && c.Items.Count > 1);

        public static bool StepView(PanelState s, PanelView v, int d)
        {
            // fix4-rest: on Together the view key flips the category chips (the window chips have the filter key, FilterKeyPressed)
            var tg = TogetherOf(v);
            if (tg != null && TogetherCategories(tg).Count > 1)
            {
                var cats = TogetherCategories(tg); var cat = Math.Max(0, cats.FindIndex(c => c.Selected)); var cn = cats.Count;
                s.View[tg.Id] = cats[((cat + d) % cn + cn) % cn].Id;
                return true;
            }
            var sw = SwitchOf(v);
            var open = v?.Windows.Where(x => !x.Disabled).ToList() ?? new List<Choice>();   // a greyed window (a fellow's book) is skipped
            if (sw == null && v != null && v.HasFilters && open.Count > 1)   // a Battle page without a switch: the view key cycles its time windows
            {
                var at = Math.Max(0, open.FindIndex(x => x.Selected)); var count = open.Count;
                s.Window = (TimeWindow)Enum.Parse(typeof(TimeWindow), open[((at + d) % count + count) % count].Id); s.WindowPicked = true;
                return true;
            }
            if (sw == null || sw.Items == null || sw.Items.Count < 2) return false;
            var i = Math.Max(0, sw.Items.FindIndex(x => x.Selected)); var n = sw.Items.Count;
            for (int k = 1; k <= n; k++) { var next = sw.Items[((i + d * k) % n + n) % n]; if (next.Tone != OffTone) { s.View[sw.Id] = next.Id; break; } }   // a greyed view is skipped
            return true;
        }

        /// <summary>
        /// Deeds overview A (r4over-deeds-a): one card per earned title of a chapter, with a number: icon and title on top,
        /// the big number and its label (your character's count first), the next line small under it (Items, Kind "number") when it is
        /// a class A, B or D number (0.7, hard case 13: a class C count such as axe hits, counted on this PC, drops off the card);
        /// a click opens the owner page (Id). Titles whose lines have no leading number are left out. Not on the Deeds
        /// overview yet: there the titles stay shortcuts without numbers (the owner rule) until Joost decides.
        /// </summary>
        public static Block DeedCards(List<TitleRow> titles, Chapter chapter = Chapter.Deeds)
        {
            var cards = new List<Block>();
            foreach (var t in (titles ?? new List<TitleRow>()).Where(x => x.Chapter == chapter))
            {
                var numbers = TitleNumbers(t).Where(n => n.value != null).ToList();
                if (numbers.Count == 0) continue;
                var main = numbers[0];
                var next = numbers.Skip(1).Where(n => n.src != SrcPc).Take(1).Select(n => new Block { Kind = "number", Value = n.value, Title = n.label, Src = n.src, Source = TagOfSrc(n.src) }).ToList();
                cards.Add(new Block
                {
                    Kind = "card", Id = t.Chapter + "/" + t.Page, Icon = "title:" + t.Id, Title = t.Title, Value = main.value, Text = main.label, Src = main.src, Source = TagOfSrc(main.src),
                    Items = next.Count > 0 ? next : null,
                });
            }
            return cards.Count == 0 ? null : new Block { Kind = "cards", Items = cards };
        }

        // ---------- navigation (keys) ----------

        public static void StepChapter(PanelState s, int d)
        {
            // the tab row's order (Feats sits after Deeds), not the enum's
            var n = ChapterRow.Length; var at = Math.Max(0, Array.FindIndex(ChapterRow, c => c.id == s.Chapter));
            s.Chapter = ChapterRow[((at + d) % n + n) % n].id;
        }

        /// <summary>How much shorter the plate gets on a short page (B12 screenshot note: the lower half of the plate stayed
        /// empty): it ends where its content ends, padding included; a page taller than the room keeps the full plate and scrolls.</summary>
        public static float PlateCut(float room, float content) => (float)Math.Max(0, Math.Floor(room - content));

        /// <summary>
        /// A composition bar's kinds along the bar, 0..1 (fix2 3): each kind from..to, the next kind starting exactly where it
        /// ends (the separator sits on that boundary and nowhere else), each at least minShare wide; fadedTo = where its part
        /// counted before Hearthwoven (fraction2 of it, drawn faded first) ends inside the kind, from when nothing is faded.
        /// </summary>
        public static (float from, float to, float fadedTo)[] BarParts(float[] fractions, float[] faded, float minShare)
        {
            var shares = fractions.Select(f => Math.Max(f, minShare)).ToArray(); var sum = shares.Sum();
            var r = new (float, float, float)[shares.Length]; float x = 0;
            for (int k = 0; k < shares.Length; k++)
            {
                var w = sum > 0 ? shares[k] / sum : 0; var fd = Math.Max(0, Math.Min(1, faded != null && k < faded.Length ? faded[k] : 0));
                var to = k == shares.Length - 1 ? 1f : x + w;
                r[k] = (x, to, x + (to - x) * fd); x = to;
            }
            return r;
        }

        /// <summary>The plate's height on screen: its content's, never more than the room (a taller page scrolls inside it).</summary>
        public static float PlateHeight(float room, float content) => room - PlateCut(room, content);

        /// <summary>What a page's content needs (fix2 1, Joost 2026-10-08: Woodcutting, Mining and Farming were cut short and
        /// could not scroll): the layout's own height, or, when something drawn reaches lower (drawn = from the content's top
        /// edge to the bottom of its lowest picture or text), that plus the bottom padding. Never less than the layout.</summary>
        public static float PlateNeed(float layout, float drawn, float padBottom) => Math.Max(layout, drawn > 0 ? drawn + padBottom : 0);

        /// <summary>Panel.Scale, kept between 0.8 and 1.3 (1.0 fits a 1920 x 1080 screen).</summary>
        public static float PanelScale(float v) => float.IsNaN(v) ? 1f : Math.Max(0.8f, Math.Min(1.3f, v));

        /// <summary>
        /// Labels under a bar's segments (Voyages: "37.6 km at the helm", "6.1 km as passenger"), each under its own segment's
        /// start, never over another: a label that would touch the previous one moves right of it; when the last one would run
        /// past the column, the row slides back left only as far as the gaps allow. starts and widths in pixels, same order.
        /// </summary>
        public static float[] LabelPositions(float[] starts, float[] widths, float column, float gap)
        {
            var n = starts.Length; var p = new float[n];
            for (int i = 0; i < n; i++) p[i] = i == 0 ? Math.Max(0, starts[0]) : Math.Max(starts[i], p[i - 1] + widths[i - 1] + gap);
            for (int i = n - 1; i >= 0; i--)
            {
                var limit = i == n - 1 ? column - widths[i] : p[i + 1] - gap - widths[i];
                p[i] = Math.Max(0, Math.Min(p[i], limit));
            }
            return p;
        }

        /// <summary>Live settings: a change seen sets the reload time <paramref name="delay"/> ahead (each further change moves it
        /// on: a save writes in bursts); true once, when that time has come.</summary>
        public static bool ConfigDue(ref float due, bool changed, float now, float delay)
        {
            if (changed) due = now + delay;
            if (due <= 0 || now < due) return false;
            due = 0; return true;
        }

        /// <summary>The left list's row height and gap (chrome A2): 40 px rows 8 apart; when the entries do not fit the room,
        /// the gap closes to 4 px first, then the rows go to 36, 32 and 28 px.</summary>
        public static (float row, float gap) ListRows(int count, float room)
        {
            float Need(float row, float gap) => count * row + Math.Max(0, count - 1) * gap + 4;
            if (Need(40, 8) <= room) return (40, 8);
            if (Need(40, 4) <= room) return (40, 4);
            if (Need(36, 4) <= room) return (36, 4);
            if (Need(32, 3) <= room) return (32, 3);   // fix3-rest: Skills has fourteen entries beside Overview; they fit in the room with the S keycap under the last row
            return (28, 3);   // longer still (a modded skill): the smallest rows, and the list scrolls with its fade
        }

        public const float MinOverflow = 12f;
        /// <summary>How far a list or column can scroll. Under 12 px of overflow it does not scroll at all: a sliver of range
        /// only made the page jump on the next wheel notch (B9).</summary>
        public static float ScrollRoom(float contentHeight, float viewHeight)
        {
            var over = contentHeight - viewHeight;
            return over < MinOverflow ? 0f : over;
        }

        /// <summary>
        /// Where a grid that scrolls inside its page (the Feats and Titles cards) stands once the page is drawn again (B31: after scrolling down, a hover
        /// near the top threw the list back to the top). kept = where the player left it on this same page, null on a page just opened. A page just opened
        /// shows the chosen card's row, scrolled only as far as it needs. The same page drawn again (an update arrived, a hover chose a card) stays where
        /// it was; only a choice that has to be on screen (follow: A/D moved it, or the page is new) scrolls, and only as far as its row needs.
        /// </summary>
        public static float GridScroll(float? kept, bool follow, float rowTop, float rowBottom, float view, float max)
        {
            max = Math.Max(0f, max);
            float Clamp(float y) => Math.Min(Math.Max(y, 0f), max);
            if (kept == null) return Clamp(rowBottom - view);
            var at = Clamp(kept.Value);
            if (!follow) return at;
            if (rowTop < at) return Clamp(rowTop);
            if (rowBottom > at + view) return Clamp(rowBottom - view);
            return at;
        }

        /// <summary>W/S: move through the left list of the current chapter (wraps).</summary>
        public static void StepList(PanelState s, PanelView v, int d)
        {
            if (v.List.Count == 0) return;
            var i = Math.Max(0, v.List.FindIndex(c => c.Selected));
            var to = v.List[((i + d) % v.List.Count + v.List.Count) % v.List.Count].Id;
            if (v.ShowAbout) s.AboutPage = to; else s.Page[s.Chapter] = to;
        }

        /// <summary>Jump from a Deeds > Overview title to its owner page ("Battle/defense").</summary>
        public static void Jump(PanelState s, string target)
        {
            var p = (target ?? "").Split('/');
            if ((p.Length == 2 || p.Length == 3) && Enum.TryParse(p[0], out Chapter c)) { s.Chapter = c; s.Page[c] = p[1]; }
            if (p.Length == 3 && p[0] == Chapter.Feats.ToString()) s.FeatSel = p[2];   // "Feats/titles/wallwarden": that card chosen (a title in a page's strip)
        }

        public const int Palette = 8;

        /// <summary>
        /// A colour per person that no one else in the group shares (up to eight people): by name, so it stays the same
        /// across screens. Recognition only, never an order of worth.
        /// </summary>
        /// <summary>The one player palette: eight colours, handed out in order (PersonColors). Every player colour reads it:
        /// shields, chips, the fellow-book chip, Together's bars and dots, the crew bars, and the HTML preview (through the dump).</summary>
        /// "Nordic earth" (Joost 2026-10-08, proto/share-pA.png); slot 0 is always the local player (You).
        public static readonly string[] PlayerPalette = { "#d6ccb3", "#9b4a50", "#5f86a0", "#94a56c", "#c4923f", "#8f7599", "#4d7a69", "#80807c" };
        //                                                  birch       lingonberry fjord      lichen     ochre      heather    pine       iron
        /// <summary>Text on a segment of this palette slot: the ink that reads better on it (fix2-rest: light text on fjord, heather and iron was 3.1 to 3.3:1, dark is 4.6 to 4.8:1):
        /// dark on birch, fjord, lichen, ochre, heather and iron, light on lingonberry and pine (pine wants the brighter ink, see CompanyUi.ShareLight).</summary>
        public static bool DarkTextOn(int slot) => slot != 1 && slot != 6;

        /// <summary>A colour slot per person, stable by name: the local player (you) always slot 0 (birch), the others
        /// alphabetically from slot 1; without a known you, alphabetically from slot 0.</summary>
        public static Dictionary<string, int> PersonColors(IEnumerable<string> names, string you = null)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); int k = 0;
            var mine = !string.IsNullOrEmpty(you);
            if (mine) map[you] = 0;
            foreach (var n in (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n) && !(mine && SameName(n, you))).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                map[n] = mine ? 1 + k++ % (Palette - 1) : k++ % Palette;
            return map;
        }

        static void ColorPeople(PanelView v)
        {
            IEnumerable<string> People(IEnumerable<Choice> cs) => cs.Where(c => c.Icon != null && c.Icon.StartsWith("person:")).Select(c => c.Icon.Substring(7));
            IEnumerable<string> InBlocks(IEnumerable<Block> bs) => bs.SelectMany(b => (b.Icon != null && b.Icon.StartsWith("person:") ? new[] { b.Icon.Substring(7) } : new string[0]).Concat(InBlocks(b.Items ?? new List<Block>())));
            v.PersonColors.Clear();
            // you: the switcher's first chip (yourself), else the book's owner (your own book before the switcher is added)
            var you = v.Players.Count > 0 && v.Players[0].Id == "" && v.Players[0].Icon != null && v.Players[0].Icon.StartsWith("person:") ? v.Players[0].Icon.Substring(7) : v.Owner;
            foreach (var kv in PersonColors(People(v.Players).Concat(People(v.List)).Concat(InBlocks(v.Blocks)).Concat(new[] { v.Owner }), you)) v.PersonColors[kv.Key] = kv.Value;
        }

        // ---------- the player switcher ----------

        /// <summary>fix4-rest (review: two small lines wedged by the corner ornament, no way to find out how): the header's notice says in one line what off means
        /// and, on a second, where the how is written: About > Sharing, with the key that opens it.</summary>
        public const string ShareOffNote = "Sharing is off: you see only your own book, and nobody sees yours.";
        public static string ShareOffHow(PanelView view)
        {
            var about = (view?.Keys ?? new List<string>()).FirstOrDefault(k => k.EndsWith("] About", StringComparison.Ordinal));
            return about == null ? "About > Sharing says how to turn it on" : about.Substring(0, about.IndexOf(']') + 1) + " About > Sharing: how to turn it on";
        }
        public const string ShareWaitingNote = "Fellow players appear here once they share too.";
        /// <summary>Singleplayer: the one line where fellow players would be (RESILIENCE-06 E1).</summary>
        public const string SoloNote = "Playing alone: fellow players appear here when you play on a server or a hosted world where they share too.";
        /// <summary>Together with sharing off: the line under your own number (the header already says sharing is off).</summary>
        public const string TogetherAloneOff = "Fellow players appear beside you once sharing is on.";

        /// <summary>0.7 (SYNC-DESIGN.md item 1): a fellow player on the game's list whose first shared copy has not arrived yet (JoinWatch):
        /// the chip's second line, and the header's note when nobody else shows yet (the note has room only then).</summary>
        public const string JoinedLine = "just joined", JoiningNote = "Their stats follow once they share too.";

        /// <summary>You first, then fellow players who share, by name, then (0.7) who just joined: a dimmed chip that does nothing, no numbers,
        /// until their copy arrives and it becomes their normal chip. Sharing off: no switcher, one line on how to turn it on.</summary>
        public static void AddPlayers(PanelView view, string me, IEnumerable<string> others, string selected, bool sharing, bool solo = false, IEnumerable<string> joining = null, string bookKey = null)
        {
            view.Players.Clear();
            if (!sharing || solo)
            {
                var line = solo ? SoloNote : ShareOffNote + "\n" + ShareOffHow(view);   // singleplayer: nobody can share, whatever the setting
                view.ShareNote = line;
                // Together alone says it under your number; the other Company pages already say what to do in their empty state: nothing is said twice
                var plate = PlateOf(view);
                var alone = plate?.Items?.FirstOrDefault(b => b.Kind == "note" && b.Text == TogetherAlone);
                if (view.Active == Chapter.Company && alone != null) alone.Text = solo ? SoloNote : TogetherAloneOff;
                else if (solo && view.Active == Chapter.Company && plate?.Items != null && !plate.Items.Any(b => b.Text == SoloNote)) plate.Items.Insert(0, new Block { Kind = "note", Text = SoloNote });   // singleplayer: the one line stands in the page (res-fellows)
                return;
            }
            var list = (others ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n) && !SameName(n, me))
                                                             .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            var pick = list.Contains(selected ?? "", StringComparer.OrdinalIgnoreCase) ? selected : "";
            view.Players.Add(new Choice { Id = "", Label = string.IsNullOrEmpty(me) ? "You" : me, Icon = "person:" + me, Selected = string.IsNullOrEmpty(pick) });
            foreach (var n in list) view.Players.Add(new Choice { Id = n, Label = n, Icon = "person:" + n, Selected = SameName(n, pick) });
            var join = (joining ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n) && !SameName(n, me) && !list.Contains(n, StringComparer.OrdinalIgnoreCase))
                                                             .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var n in join) view.Players.Add(new Choice { Id = n, Label = n, Icon = "person:" + n, Disabled = true });
            view.ShareNote = list.Count > 0 ? null : join.Count > 0 ? JoiningNote : ShareWaitingNote;
            // 0.8: the Everyone chip, always the row's last entry; on, no name is chosen (the row is the colour key for the group's bars); nobody
            // else yet: greyed with its reason (Chapters/EveryoneModel.cs)
            view.EveryoneChip = true;
            if (list.Count == 0) { view.EveryoneOn = false; view.EveryoneSub = EveryoneAloneSub; view.EveryoneWhy = EveryoneAloneWhy; view.EveryoneTo = null; }
            if (view.EveryoneOn) foreach (var c in view.Players) c.Selected = false;
            BookKeyLine(view, bookKey);
            ColorPeople(view);
        }

        // ---------- 0.8: the book key ([B] Book, the pad's Y): the player row by keys and controller ----------

        public const string BookWord = "Book";

        /// <summary>The books the key steps through, in the row's order: you, the fellow players (not one that is just joining), then Everyone
        /// unless its chip is greyed with nowhere to go (nobody else shares yet). Index Players.Count stands for Everyone.</summary>
        static bool BookTarget(PanelView view, int k) =>
            k < view.Players.Count ? !view.Players[k].Disabled : view.EveryoneChip && (view.EveryoneOn || string.IsNullOrEmpty(view.EveryoneWhy) || !string.IsNullOrEmpty(view.EveryoneTo));

        /// <summary>The book key in the key line, before Back (KeyLine leaves it out right after the scroll hint when the line is full); only
        /// when the row holds a second book to go to and the page is not About.</summary>
        static void BookKeyLine(PanelView view, string bookKey)
        {
            if (string.IsNullOrEmpty(bookKey) || view.ShowAbout || Enumerable.Range(0, view.Players.Count + 1).Count(k => BookTarget(view, k)) < 2) return;
            var key = "[" + bookKey + "] " + BookWord;
            var at = view.Keys.IndexOf("[Backspace] Back");
            if (at < 0) at = view.Keys.FindIndex(k => k.EndsWith("] Close", StringComparison.Ordinal));
            if (at < 0) view.Keys.Add(key); else view.Keys.Insert(at, key);
        }

        /// <summary>
        /// The book key ([B], the pad's Y): the next book in the player row, wrapping, skipping a chip that does nothing, doing what its click does
        /// (a name: that book, Everyone off; Everyone: the group's page, no name chosen; Everyone greyed here: the nearest page where it works,
        /// EveryoneJump). False when the row has no other book to go to.
        /// </summary>
        public static bool StepBook(PanelState state, PanelView view)
        {
            if (state == null || view == null || view.Players.Count == 0) return false;
            int n = view.Players.Count + 1, at = view.EveryoneOn ? view.Players.Count : view.Players.FindIndex(c => c.Selected);
            for (int step = 1; step <= n; step++)
            {
                var k = (at + step + n) % n;
                if (k == at || !BookTarget(view, k)) continue;
                if (k < view.Players.Count) { state.Player = view.Players[k].Id; state.Everyone = false; }
                else if (!view.EveryoneOn && !string.IsNullOrEmpty(view.EveryoneWhy)) EveryoneJump(state, view);
                else { state.Everyone = true; state.Player = ""; }
                return true;
            }
            return false;
        }

        // ---------- JSON (for the static preview) ----------

        /// <summary>The view as JSON: the preview's data, and in game the 2 s refresh's "did anything change" test. <paramref name="islands"/>: also
        /// the plate's island plan, for a dump the preview draws (the test-panel's, the in-game snapshot); the change test leaves it out, since
        /// the plan is a pure function of the blocks already in the text (0.8.1 review 8).</summary>
        public static string ToJson(PanelView v, bool islands = false)
        {
            var j = new Json().Open().Str("title", v.Title).Str("owner", v.Owner).Str("listTitle", v.ListTitle ?? "").Str("scope", v.Scope ?? "").Str("heading", v.Heading ?? "").Str("headingWindow", v.HeadingWindow ?? "").Str("headingSource", v.HeadingSource ?? "").Str("headingSrc", v.HeadingSrc ?? "")
                .Num("showAbout", v.ShowAbout ? 1 : 0).Str("shareNote", v.ShareNote ?? "")
                .Str("active", v.Active.ToString()).Str("page", v.Page ?? "").Num("hasFilters", v.HasFilters ? 1 : 0);
            if (!string.IsNullOrEmpty(v.HeadingRecordedFrom)) j.Str("headingRecordedFrom", v.HeadingRecordedFrom);
            if (v.EveryoneChip) j.Num("everyoneChip", 1).Num("everyoneOn", v.EveryoneOn ? 1 : 0).Str("everyoneSub", v.EveryoneSub ?? "").Str("everyoneWhy", v.EveryoneWhy ?? "").Str("everyoneTo", v.EveryoneTo ?? "");   // 0.8: written only with the chip, so the older data stays as it was
            void Choices(string k, List<Choice> cs)
            {
                j.Key(k).OpenArr();
                foreach (var c in cs)
                {
                    j.Open().Str("id", c.Id ?? "").Str("label", c.Label ?? "").Str("icon", c.Icon ?? "").Num("selected", c.Selected ? 1 : 0).Num("dot", c.Dot ? 1 : 0).Num("disabled", c.Disabled ? 1 : 0);
                    if (!string.IsNullOrEmpty(c.Why)) j.Str("why", c.Why);   // a greyed window chip's own reason (PageHead.cs), written only when set
                    j.Close();
                }
                j.CloseArr();
            }
            Choices("chapters", v.Chapters); Choices("list", v.List); Choices("badges", v.Badges); Choices("toggle", v.Toggle);
            Choices("windows", v.Windows); Choices("biomes", v.Biomes); Choices("players", v.Players);
            if (!string.IsNullOrEmpty(v.WindowWhy) || !string.IsNullOrEmpty(v.WindowTip)) j.Str("windowWhy", v.WindowWhy ?? "").Str("windowTip", v.WindowTip ?? "");   // 0.8 layout D+ (PageHead.cs), written only when set
            if (!string.IsNullOrEmpty(v.StripNote)) j.Str("stripNote", v.StripNote);
            if (v.Compare != null) { j.Key("compare").Open().Str("label", v.Compare.Label ?? "").Num("selected", v.Compare.Selected ? 1 : 0).Num("disabled", v.Compare.Disabled ? 1 : 0).Str("why", v.CompareWhy ?? "").Num("tip", v.CompareTip ? 1 : 0).Close(); }   // 0.8, written only when offered
            j.Key("keys").OpenArr(); foreach (var k in v.Keys) j.Open().Str("text", k).Close(); j.CloseArr();
            j.Dict("personColors", v.PersonColors.Select(kv => new KeyValuePair<string, float>(kv.Key, kv.Value + 1)));   // +1: Json.Dict leaves zeros out
            j.Key("palette").OpenArr(); foreach (var c in PlayerPalette) j.Open().Str("c", c).Close(); j.CloseArr();   // the preview reads the same table
            void B(Block b)
            {
                j.Open().Str("kind", b.Kind ?? "").Str("id", b.Id ?? "").Str("icon", b.Icon ?? "").Str("title", b.Title ?? "").Str("value", b.Value ?? "").Str("text", b.Text ?? "")
                 .Str("note", b.Note ?? "").Str("tone", b.Tone ?? "").Str("source", b.Source ?? "").Num("fraction", b.Fraction).Num("selected", b.Selected ? 1 : 0)
                 .Str("src", b.Src ?? "").Str("colour", b.Colour ?? "").Str("pattern", b.Pattern ?? "").Num("fraction2", b.Fraction2).Str("value2", b.Value2 ?? "")
                 .Num("count", b.Count).Num("level", b.Level).Num("progress", b.Progress).Num("practised", b.Practised ? 1 : 0).Str("pill", b.Pill ?? "").Str("pillIcon", b.PillIcon ?? "");
                j.Num("columns", b.Columns);
                j.Str("keyCap", b.KeyCap ?? "").Num("open", b.Open ? 1 : 0);
                if (!string.IsNullOrEmpty(b.RecordedFrom)) j.Str("recordedFrom", b.RecordedFrom);   // 0.7 (RecordedModel.cs): written only when set, so the older pages' data stays as it was
                if (b.Unrecorded) j.Num("unrecorded", 1);
                if (b.Before != null) j.Str("before", b.Before).Str("change", b.Change ?? "").Str("beforeLabel", b.BeforeLabel ?? "");   // 0.8 Compare: written only when set
                if (b.Chip != null) { j.Key("chip"); B(b.Chip); }
                j.Key("items").OpenArr(); foreach (var i in b.Items ?? new List<Block>()) B(i); j.CloseArr();
                if (islands && b.Kind == "plate" && ReferenceEquals(b, PlateOf(v))) IslandsJson(j, b);   // 0.8 layout D+: the page's islands (Islands.cs), dumps only
                j.Close();
            }
            j.Key("blocks").OpenArr(); foreach (var b in v.Blocks) B(b); j.CloseArr();
            return j.Close().ToString();
        }

        /// <summary>Every player-visible string in the view (for the no-em-dash check and localisation review).</summary>
        public static IEnumerable<string> AllText(PanelView v)
        {
            IEnumerable<string> Of(Block b) => new[] { b.Title, b.Value, b.Value2, b.Text, b.Note, b.Pill, b.Before, b.Change, b.BeforeLabel }.Concat((b.Items ?? new List<Block>()).SelectMany(Of)).Concat(b.Chip != null ? Of(b.Chip) : Enumerable.Empty<string>());
            return new[] { v.Title, v.Owner, v.Scope, v.Heading, v.ShareNote, v.EveryoneSub, v.EveryoneWhy, v.WindowWhy, v.StripNote,   // 0.8 layout D+: the window chips' reason, the strip's note (PageHead.cs)
                           v.Windows.FirstOrDefault(c => c.Id == v.WindowTip)?.Why }   // the pressed chip's own reason, shown now (the others only on hover)
                .Concat(new[] { v.Chapters, v.List, v.Badges, v.Toggle, v.Windows, v.Biomes, v.Players }.SelectMany(cs => cs.Select(c => c.Label)))
                .Concat(v.Keys).Concat(v.Blocks.SelectMany(Of)).Concat(CompareText(v))
                .Concat(v.AboutNumbers != null && !Content(v).Any(b => b.Kind == "aboutnumbers" && b.Items != null && b.Items.Count > 0) ? Of(v.AboutNumbers) : Enumerable.Empty<string>())   // About these numbers while closed: its lines are the page's words too (open, the page holds them)
                .Where(s => !string.IsNullOrEmpty(s));
        }
    }
}
