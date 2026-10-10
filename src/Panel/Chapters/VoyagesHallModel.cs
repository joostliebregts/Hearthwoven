using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Voyages (Overview, Sailing, On foot, Maps) and Hall (Overview, Trader, Smelters): the approved round-2 and round-4
    /// pages (work/hearthwoven-visual-vocabulary/proto r2-voyages.js, r4voy.js). New block kinds, general enough for other
    /// chapters: journey (one line by km, legs of segments), crew (a bar per fellow player), compass (four arms from home),
    /// biometiles (the biomes your character found). Only what the mod has: chest and cart records are server-only and are
    /// not drawn; the crew bars cannot say whose helm (the mod keeps time with each player and time under each helm apart).
    /// </summary>
    public static partial class PanelModel
    {
        public const string HallOverview = "Hall", JourneyTitle = "Journey", SailedWithTitle = "Sailed with",
                            HomeAway = "Home and away", UnderEachHelm = "Under the helm of", SeaRoute = "Sea route",
                            WalkRun = "Walking and running", MovementSkills = "Movement skills", Found = "Finds",
                            FarEdge = "Time far out", FarEdgeLine = "beyond 10 km from the center", BiomesFound = "Biomes found", CoinsSpent = "Trader",
                            IntoSmelters = "Smelters", BoughtPerTrader = "Bought, per trader", PerStation = "Put in, per station";

        // ---------- formats ----------

        static string KmNumber(double meters) { var k = Km(meters); return k.Substring(0, k.Length - 3); }   // "48.2 km" -> "48.2"

        /// <summary>Time as the word list writes it: "21 min", "6 hours 10 min" (no abbreviations except km and min), "under 1 min".</summary>
        public static string Minutes(double seconds)
        {
            if (seconds <= 0) return "0 min";
            if (seconds < 60) return "under 1 min";
            var m = (long)Math.Round(seconds / 60.0);
            if (m < 60) return m + " min";
            var h = m / 60; var rest = m % 60;
            return h + (h == 1 ? " hour" : " hours") + (rest > 0 ? " " + rest + " min" : "");
        }

        static Block Tagged(Block b, string src) { b.Src = src; b.Source = TagOfSrc(src); return b; }

        /// <summary>The empty state of a page or section this PC counts (rule C.3): "Nothing from 8 October", one line, no number to mark; its tag says whose count it is.</summary>
        static Block NothingHere(PanelInput input, DateTime? from) { var b = Empty(NothingFrom(input, from)); b.Source = TagMeasured; return b; }

        // ---------- Voyages ----------

        static void Voyages(PanelInput input, string page, PanelView view, PanelState state = null)
        {
            view.Recorded = true;   // 0.7 (REDESIGN-RULES.md, group G7): no zones; the dated labels are automatic
            view.Scope = RecordedScope(input);
            // Sailing and Cargo have the day windows (HISTORY-06.md): Today, 7 days, 30 days from the day history, and All. A day window
            // reads the window's copy of the input (InWindow): km from the game counters' growth those days, crew and cargo from the rows
            var offered = WindowsOf(Chapter.Voyages, page);
            var w = offered != null && state != null ? WindowChips(input, state, view, offered) : TimeWindow.SinceInstall;
            var src = IsDayWindow(w) ? InWindow(input, w) ?? input : input;
            if (!IsDayWindow(w)) VoyagesAboutNumbers(input, view);   // the box: All only (a day window has none, rule W.1)
            switch (page)
            {
                case "sailing": Sailing(src, view, w); return;
                case "cargo": CargoPage(src, view, w, input.Book != null); return;
                case "onfoot": OnFoot(input, view); return;
                case "maps": Maps(input, view); return;
                default: VoyagesOverview(input, view); return;
            }
        }

        /// <summary>
        /// About these numbers, Voyages (SOURCE-MATRIX): before the game's stats baseline the game's own distances, finds and biomes; from it
        /// what this PC counted (who you sailed with, under whose helm, maps shared, cargo carried). Cargo carried has its own date when its
        /// counter group started later (hard case 4): the From line says so.
        /// </summary>
        static void VoyagesAboutNumbers(PanelInput input, PanelView view)
        {
            var stats = StartOf(input, LocalTotals.StatsKind); var cargo = StartOf(input, LocalTotals.StartCargo);
            var from = cargo.HasValue && stats.HasValue && RecordDate(input, cargo.Value) != RecordDate(input, stats.Value)
                ? "Hearthwoven also counted who you sailed with, under whose helm and maps shared, on this PC. Cargo carried is counted from " + RecordDate(input, cargo.Value) + "."
                : "Hearthwoven also counted who you sailed with, under whose helm, maps shared and cargo carried, on this PC.";
            AboutNumbers(view, input, stats, "The game's own count of distances, finds and biomes.", from,
                         "Cargo is a straight line between samples, so the real figure is higher. Cargo loaded and unloaded is counted by the server.");
        }

        // the journey's colours (r2-voyages.css): foot path, the helm in amber, the sea as passenger
        // Colour rules (fix-rest, Joost: the player palette is for players): a thing that is not a player never takes a player's colour
        // (the passenger's old blue was Finch's fjord) and one colour never means two things on a page (the foot path's brown was also "away").
        // Passenger: sea teal, between the palette's fjord blue and pine green; away from home: night indigo, darker than any player. Home: hearth ember (fix3-rest: the cream it had is Rowan's birch on the Together page).
        public const string FootColour = "#6b5232", HelmColour = "#e8a948", PassengerColour = "#2f9a9a", WalkColour = "#6b5232", RunColour = "#8c6a3e",
                            HomeColour = "#c2603a", AwayColour = "#46506e";

        static Block Segment(string id, string title, double meters, double of, string colour, string tone) =>
            new Block { Kind = "segment", Id = id, Title = title, Value = Km(meters), Fraction = (float)(meters / of), Colour = colour, Tone = tone };

        // at the helm and as passenger: passenger = sailed minus at the helm, the game's own rule (GAME-METRICS)
        static List<Block> SeaSegments(PanelInput input)
        {
            double sail = C(input, "DistanceSail"), helm = Math.Min(sail, C(input, "DistanceSailHelm")), pass = sail - helm;
            var segs = new List<Block>();
            if (sail <= 0) return segs;
            if (helm > 0) segs.Add(Segment("helm", "at the helm", helm, sail, HelmColour, "solid"));
            if (pass > 0) segs.Add(Segment("passenger", "as passenger", pass, sail, PassengerColour, "crest"));
            return segs;
        }

        /// <summary>"How far did I travel?": one journey line by km, on foot and sailed (at the helm, as passenger); each leg
        /// is as long as its share of the whole. Your character's own counts.</summary>
        public static Block Journey(PanelInput input)
        {
            double foot = C(input, "DistanceWalk") + C(input, "DistanceRun"), sail = C(input, "DistanceSail"), total = foot + sail;
            if (total <= 0) return null;
            var legs = new List<Block>();
            if (foot > 0) legs.Add(new Block { Kind = "leg", Id = "foot", Icon = "title:explorer", Value = KmNumber(foot), Title = "km on foot", Fraction = (float)(foot / total),
                                               Items = new List<Block> { Segment("foot", "", foot, foot, FootColour, "dots") } });
            if (sail > 0) legs.Add(new Block { Kind = "leg", Id = "sail", Icon = "piece:Karve", Value = KmNumber(sail), Title = "km sailed", Fraction = (float)(sail / total), Items = SeaSegments(input) });
            return Tagged(new Block { Kind = "journey", Items = legs }, SrcCharacter);
        }

        /// <summary>"With whom did I sail?": a bar of minutes aboard the same ship per fellow player, alphabetical (never a
        /// ranking), measured on this PC. Fraction = share of the longest. Items stay empty: whose helm per fellow is not recorded.</summary>
        public static Block Crew(PanelInput input)
        {
            var with = (input.Events?.SailedWith ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0 && !SameName(kv.Key, input.PlayerName)).ToList();
            if (with.Count == 0) return null;
            var max = with.Max(kv => kv.Value);
            // rule C (0.7): counted on this PC since the install; a fellow's copy of their last session only says so (rule E), the label says the rest
            return Tagged(new Block
            {
                Kind = "crew", Note = input.IsSelf || input.SharedSinceInstall ? null : "as " + Name(input) + " last shared it",
                Items = with.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                            .Select(kv => new Block { Kind = "person", Id = kv.Key, Icon = "person:" + kv.Key, Title = kv.Key, Value = Minutes(kv.Value), Fraction = kv.Value / max }).ToList(),
            }, SrcPc);
        }

        public const string UnderHelmNote = "fellow players' helms only";   // your own helm is the km "at the helm" (the game does not tell the mod your helm minutes)

        /// <summary>Minutes under each fellow player's helm, as a composition in their colours (the game does not tell the
        /// mod your own helm time aboard: that is the km at the helm).</summary>
        static Block UnderHelm(PanelInput input)
        {
            var under = (input.Events?.SailedUnderHelmOf ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            if (under.Count == 0) return null;
            var total = under.Sum(kv => (double)kv.Value);
            return Tagged(new Block
            {
                Kind = "composition", Title = UnderEachHelm, Note = UnderHelmNote,
                Items = under.Select(kv => new Block { Id = kv.Key, Icon = "person:" + kv.Key, Title = kv.Key, Value = Minutes(kv.Value), Fraction = (float)(kv.Value / total), Colour = "person:" + kv.Key }).ToList(),
            }, SrcPc);
        }

        /// <summary>The words of "Home and away" (fix-rest): the game books TimeInBase only while you are safe at home (Player.IsSafeInHome: the Resting effect,
        /// in shelter, by a fire, in a base) and everything else as TimeOutOfBase.
        /// So "in base" over-claimed "time at home": the bar says "resting at home" and says what counts.</summary>
        public const string RestingAtHome = "resting at home", AwayFromHome = "away from home", HomeCondition = "home: resting in shelter, by a fire, in your base";

        /// <summary>Home and away: the game's own time safe at home (sheltered, near a fire in a base) and out of it.</summary>
        static Block HomeAndAway(PanelInput input)
        {
            double home = C(input, "TimeInBase"), away = C(input, "TimeOutOfBase"), total = home + away;
            if (total <= 0) return null;
            var parts = new List<Block>();
            if (home > 0) parts.Add(new Block { Id = "home", Icon = "vocab:home-mark", Title = RestingAtHome, Value = Minutes(home), Fraction = (float)(home / total), Colour = HomeColour });
            if (away > 0) parts.Add(new Block { Id = "away", Title = AwayFromHome, Value = Minutes(away), Fraction = (float)(away / total), Colour = AwayColour });   // no picture: the explorer mark is the foot path's
            return Tagged(new Block { Kind = "composition", Title = HomeAway, Items = parts, Note = input.IsSelf ? HomeCondition : HomeCondition.Replace("your base", "their base") }, SrcCharacter);
        }

        // r2-voyages-voyages: the journey across the plate, then the crew and home and away side by side
        static void VoyagesOverview(PanelInput input, PanelView view)
        {
            view.Heading = "Voyages";
            var journey = Journey(input);
            if (journey != null) { view.Blocks.Add(Section(JourneyTitle)); view.Blocks.Add(journey); }
            // fix4-rest (review: three bands of one weight): the journey is the page; home and away is one thin line under it, the crew the quiet strip last
            var home = HomeAndAway(input); if (home != null) { home.Tone = Thin; view.Blocks.Add(home); }
            Group(view, SailedWithTitle, Crew(input));
            Plate(view, "ui:chapter-voyages", RecordedScope(input));
        }

        // r4voy-sailing: km sailed as the hero, the sea route, then the crew beside the helms and the leviathans (the cargo: Voyages > Cargo)
        static void Sailing(PanelInput input, PanelView view, TimeWindow w = TimeWindow.SinceInstall)
        {
            view.Heading = "Sailing";
            var sail = C(input, "DistanceSail");
            // a leviathan sinks back under the sea once it has been mined enough (Leviathan.m_leaveStat); the game counts it. Its number rides
            // beside the km in the hero (fix-rest: its own line pushed the crew off the fold)
            var sunk = C(input, "LeviathanSink") + C(input, "LavaLeviathanSink");
            Add(view, Hero((sail > 0 ? KmNumber(sail) : null, "km sailed", SrcCharacter, null), (sunk > 0 ? N(sunk) : null, sunk == 1 ? "leviathan sunk" : "leviathans sunk", SrcCharacter, null)));
            SkillBeside(view, input, SkillKeyNamed(input, SailingSkillName));   // 0.8: a mod's sailing skill in the hero's row, when the character has one (the game has none)
            var segs = SeaSegments(input);
            if (segs.Count > 0)
            {
                view.Blocks.Add(Tagged(new Block { Kind = "journey", Items = new List<Block> { new Block { Kind = "leg", Id = "sail", Fraction = 1, Items = segs } } }, SrcCharacter));
            }
            // fix3-rest: the cargo has its own page (CargoPage: below the fold of Sailing it was never seen); Sailing keeps the voyages, the route and the crew
            Columns(view, Stretch(v => Group(v, SailedWithTitle, Crew(input))),
                          Stretch(v => Add(v, UnderHelm(input))));
            if (IsDayWindow(w) && view.Blocks.All(b => b.Kind == "columns" && (b.Items ?? new List<Block>()).All(c => (c.Items ?? new List<Block>()).Count == 0))) { view.Blocks.Clear(); view.Blocks.Add(DayEmpty(input, w)); }
            Plate(view, "ui:chapter-voyages", RecordedScope(input));
        }

        static readonly string[] MoveSkills = { "Jump", "Run", "Swim" };

        // r4voy-onfoot: the hero, walking and running, jumps and the air on the left; the movement skills' ladders on the right
        static void OnFoot(PanelInput input, PanelView view)
        {
            view.Heading = "On foot";
            double walk = C(input, "DistanceWalk"), run = C(input, "DistanceRun"), foot = walk + run;
            var left = Stretch(v =>
            {
                if (foot > 0)
                {
                    Add(v, Hero((KmNumber(foot), "km on foot", SrcCharacter, null)));
                    var segs = new List<Block>();
                    if (walk > 0) segs.Add(Segment("walk", "walking", walk, foot, WalkColour, "dots"));
                    if (run > 0) segs.Add(Segment("run", "running", run, foot, RunColour, "dash"));
                    v.Blocks.Add(Section(WalkRun));
                    v.Blocks.Add(Tagged(new Block { Kind = "journey", Note = "swimming and riding are not counted", Items = new List<Block> { new Block { Kind = "leg", Id = "foot", Fraction = 1, Items = segs } } }, SrcCharacter));
                }
                var air = new List<Block>();
                var jumps = C(input, "Jumps");
                if (jumps > 0) air.Add(new Block { Title = jumps == 1 ? "Jump made" : "Jumps made", Value = N(jumps) });   // no mark: move-air belongs to the air
                // the distance flown is NOT inside the km on foot (that is walking + running only): the row says so
                if (C(input, "DistanceAir") > 0) air.Add(new Block { Title = "In the air, not in the km above", Icon = "vocab:move-air", Value = Km(C(input, "DistanceAir")) });
                if (air.Count > 0) v.Blocks.Add(new Block { Kind = "rows", Items = air, Note = SourceCharacter });   // its two rows say it: no heading
            });
            // the movement skills beside the distance (Joost's wish: the skill next to the deed it belongs to); the same
            // ladders as Skills > Overview, only Jump, Run and Swim, only those your character has
            var have = new HashSet<string>(SkillNames(input));
            var skills = MoveSkills.Where(have.Contains).Select(k => LadderOf(input, k, "ladder")).ToList();
            var right = Stretch(v => { if (skills.Count > 0) v.Blocks.Add(Tagged(new Block { Kind = "ladders", Text = PractisedKeyText(input), Items = new List<Block> { new Block { Kind = "group", Title = MovementSkills, Items = skills } } }, SrcCharacter)); });   // the key's own date (PractisedKeyText)
            Columns(view, left, right);
            Plate(view, "title:explorer", RecordedScope(input));
        }

        // the far edge: the game counts seconds beyond 10350 m on each axis (Player.UpdateStats). Its x-axis names are
        // mirrored (x below -10350, the west on the map, is booked as ExploreEast; GAME-METRICS), so East and West swap here.
        static readonly (string id, string label, string stat)[] Edges =
            { ("north", "North", "ExploreNorth"), ("east", "East", "ExploreWest"), ("south", "South", "ExploreSouth"), ("west", "West", "ExploreEast") };

        /// <summary>"Where did I go to the edge of the world?": four arms from home, length = time beyond that edge.</summary>
        public static Block Compass(PanelInput input)
        {
            var arms = Edges.Select(e => (e, s: C(input, e.stat))).ToList();
            var max = arms.Max(a => a.s);
            if (max <= 0) return null;
            return Tagged(new Block
            {
                Kind = "compass",
                Items = arms.Select(a => new Block { Kind = "arm", Id = a.e.id, Title = a.e.label, Value = Minutes(a.s), Fraction = (float)(a.s / max) }).ToList(),
            }, SrcCharacter);
        }

        /// <summary>"6 of 9": found out of the biomes the game defines (the journey's nine, the Ocean included).</summary>
        public static string BiomesOf(int found) => found + " of " + BiomeTiles.Length;

        /// <summary>"Which biomes did I find?": one tile per biome found (FoundBiomes), in journey order with the Ocean last. Your own book:
        /// the game's own record plus evidence. A fellow's book: only the evidence their copy carries (damage or a death there); their shared
        /// "knownBiomes" only opens the group feats' gate, so a fellow's Maps never names a land the copy shows no trace of. Not found: not shown.</summary>
        public static Block BiomeTilesFound(PanelInput input)
        {
            var known = new HashSet<string>(FoundBiomes(input), StringComparer.OrdinalIgnoreCase);   // one source with Battle: the game's record plus evidence
            var tiles = BiomeTiles.Where(t => known.Contains(t.key)).Select(t => new Block
            {
                Kind = "biome", Id = t.key, Title = BiomeName(t.key), Icon = BiomeEmblem(t.key), Colour = t.colour,
                Tone = t.key == "Meadows" || t.key == "Plains" ? "dark-text" : "light-text",
            }).ToList();
            return tiles.Count == 0 ? null : new Block { Kind = "biometiles", Items = tiles };
        }

        static void Maps(PanelInput input, PanelView view)
        {
            view.Heading = "Maps";
            // r4voy-maps (B with A's compass): the found tiles beside the compass, the biomes found across the plate
            // (as rows: the 160 px cards made the found column twice the compass's height; rows keep both in view)
            var found = FoundStats.Select(s => (s.label, s.mark, n: C(input, s.stat))).Where(s => s.n > 0)
                                  .Select(s => Tagged(new Block { Title = s.label, Icon = s.mark, Value = N(s.n) }, SrcCharacter)).ToList();
            var shared = M(input, e => e.MapShared);
            if (shared > 0) found.Add(Tagged(new Block { Title = shared == 1 ? "Map shared" : "Maps shared", Icon = "vocab:maps-shared", Value = N(shared) }, SrcPc));
            var compass = Compass(input);
            Columns(view, Stretch(v => { if (found.Count > 0) { v.Blocks.Add(Section(Found)); v.Blocks.Add(new Block { Kind = "rows", Items = found }); } }),
                          Stretch(v => { if (compass != null) { var far = Section(FarEdge); far.Text = FarEdgeLine; v.Blocks.Add(far); v.Blocks.Add(compass); } }));
            var bios = BiomeTilesFound(input);
            // how many of the game's biomes (Joost: "6 of 9"), from the same found-biomes record as the tiles
            if (bios != null) { view.Blocks.Add(new Block { Kind = "section", Title = BiomesFound, Value = BiomesOf(bios.Items.Count), Src = SrcCharacter, Source = TagCharacter }); view.Blocks.Add(bios); }
            Plate(view, "title:mapmaker", RecordedScope(input));
        }

        // your character's finds, in the prototype's order (the game's own counters)
        static readonly (string stat, string label, string mark)[] FoundStats =   // Codex's marks (the game has no icons for these)
        {
            ("TreasureBuriedFound", "Buried treasures", "vocab:find-buried"), ("TreasureDungeonFound", "Dungeon treasures", "vocab:find-dungeon"),
            ("TreasureLocationFound", "Other treasures", "vocab:find-location"),
            ("PortalDungeonIn", "Dungeons entered", "vocab:find-dungeon"), ("PortalsUsed", "Portal trips", "vocab:portal-mark"),
        };

        // ---------- Hall (the chapter enum keeps its old name, Stores) ----------

        static void Stores(PanelInput input, string page, PanelView view)
        {
            view.Recorded = true;   // 0.7 (G7): every Hall page counts this PC's own numbers since the install: one label, no zones
            view.Scope = RecordedScope(input);
            AboutNumbers(view, input, StartOf(input, null), "Not recorded: the game keeps no count of trades or smelting.",
                         "Hearthwoven counted what you bought and put in, on this PC.",
                         "The game does not name the fuel; each station's usual fuel is shown. What a Stoker's Chest feeds is shown apart, never as yours.");
            switch (page)
            {
                case "trader": TraderPage(input, view); return;
                case "smelters": SmeltersPage(input, view); return;
                default: HallOverviewPage(input, view); return;
            }
        }

        // the traders' colours in the prototype; any other trader takes the neutral palette
        static string TraderColour(string trader)
        {
            var t = (trader ?? "").ToLowerInvariant();
            return t.Contains("haldor") ? "#e8a948" : t.Contains("hildir") ? HildirColour : t.Contains("bogwitch") ? "#4a9aa0" : null;
        }

        /// <summary>A trader's colour as the coins bar draws it: Haldor gold, Hildir rose, the Bog Witch sea-teal (none a player colour), any other trader
        /// the next neutral in the bar's order (most coins first).</summary>
        static Func<string, string> TraderColours(PanelInput input)
        {
            var order = (input.Events?.Spent ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key)
                        .Concat(Purchases(input).Select(p => p.trader).Distinct().OrderBy(t => t, StringComparer.Ordinal)).Distinct().ToList();
            var map = new Dictionary<string, string>(); int spare = 0;
            foreach (var t in order) map[t] = TraderColour(t) ?? Neutral[spare++ % Neutral.Length];
            return t => t != null && map.TryGetValue(t, out var c) ? c : TraderColour(t);
        }

        /// <summary>Coins spent per trader, as one composition (Value = all coins).</summary>
        public static Block CoinsByTrader(PanelInput input)
        {
            var spent = (input.Events?.Spent ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            if (spent.Count == 0) return null;
            var total = spent.Sum(kv => (double)kv.Value); var colours = TraderColours(input);
            return Tagged(new Block
            {
                Kind = "composition", Title = CoinsSpent, Value = N(total),
                Items = spent.Select(kv => new Block { Id = kv.Key, Title = Who(input, kv.Key), Value = N(kv.Value), Fraction = (float)(kv.Value / total), Colour = colours(kv.Key) }).ToList(),
            }, SrcPc);
        }

        // "trader|item" -> amount bought
        static List<(string trader, string item, double n)> Purchases(PanelInput input) =>
            (input.Events?.Bought ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).Select(kv => { var p = Split2(kv.Key); return (p[0], p[1], (double)kv.Value); }).ToList();

        // the ledger: one row per item, the most bought first; the trader under the name when you bought from more than one
        static Block Ledger(PanelInput input, IEnumerable<(string trader, string item, double n)> bought, bool traderLine, int top)
        {
            // the itemgrid kind (ch-deeds, Joost 2026-10-08: what you made, caught or bought as item tiles, the full list); its
            // renderer arrives with the ch-deeds merge
            // each item edged in its trader's colour, the trader you bought most of it from (DESIGN-DIFF Hall: the bar colour
            // per trader linked each bought item to its trader)
            var colours = TraderColours(input);
            var rows = bought.GroupBy(b => b.item).Select(g => (item: g.Key, n: g.Sum(b => b.n), trader: g.GroupBy(b => b.trader).OrderByDescending(t => t.Sum(b => b.n)).ThenBy(t => t.Key, StringComparer.Ordinal).First().Key))
                             .OrderByDescending(r => r.n).ThenBy(r => r.item, StringComparer.Ordinal).Take(top)
                             .Select(r => new Block { Kind = "item", Id = r.item, Icon = "item:" + r.item, Title = Who(input, r.item), Value = N(r.n), Colour = colours(r.trader) }).ToList();
            return rows.Count == 0 ? null : Tagged(new Block { Kind = "itemgrid", Items = rows }, SrcPc);
        }

        // a composition under its own section and hero: the total is the hero's number, so the bar's head stays empty
        // (no number twice on a page)
        // compact (Trader, Smelters; fix2 7, Joost: a huge hero and a big bar left the page empty): the hero on one modest line,
        // the bar right under it at reduced height
        static void HeroAndBar(PanelView v, string section, Block comp, string one, string many, bool compact = false)
        {
            if (comp == null) return;
            var total = comp.Value; var n = ParseCount(total);
            if (section != null) v.Blocks.Add(Section(section));
            var hero = Hero((total, n == 1 ? one : many, SrcPc, null));
            Add(v, hero);
            comp.Title = null; comp.Value = null;
            if (compact) comp.Tone = Thin;
            v.Blocks.Add(comp);
        }

        /// <summary>Block.Tone of a hero drawn on one modest line, and of a composition bar at reduced height (fix2 7).</summary>
        public const string Compact = "compact", Thin = "thin";

        /// <summary>
        /// The ledger kind (fix2 7, the dense ledger of r4voy-hall-a Joost approved): one compact row per station or trader,
        /// its name on the left (Title, its colour in Colour), then its items as inline chips (Kind "chip": picture, count,
        /// name; Colour = the thin edge, Tone "fuel" = marked as fuel) flowing to the right and wrapping.
        /// </summary>
        static Block LedgerRows(IEnumerable<Block> rows)
        {
            var list = rows.Where(r => r.Items != null && r.Items.Count > 0).ToList();
            return list.Count == 0 ? null : Tagged(new Block { Kind = "ledger", Items = list }, SrcPc);
        }
        static Block Chip(PanelInput input, string item, double n, string colour, bool fuel) => Tagged(new Block
        {
            Kind = "chip", Id = item, Icon = item == FuelKey ? "" : "item:" + item, Title = item == FuelKey ? "Fuel" : Who(input, item), Value = N(n),
            Colour = colour, Tone = fuel ? FuelTone : null,
        }, SrcPc);

        // r4voy-hall-a: coins at the trader and the ledger on the left, the smelters on the right
        static void HallOverviewPage(PanelInput input, PanelView view)
        {
            view.Heading = HallOverview;
            var bought = Purchases(input);
            Columns(view,
                Stretch(v =>
                {
                    HeroAndBar(v, CoinsSpent, CoinsByTrader(input), "coin spent", "coins spent", compact: true);   // fix3-rest: modest heroes, so the item tiles are above the fold under the feat band
                    if (bought.Count > 0) Group(v, Plural(bought.Sum(b => b.n), "item bought", "items bought"), Ledger(input, bought, false, int.MaxValue));
                }),
                Stretch(v =>
                {
                    HeroAndBar(v, IntoSmelters, SmelterComposition(input), "item put in", "items put in", compact: true);
                    Add(v, SmelterRows(input, Smelted(input), int.MaxValue));   // the item tiles say it: no "by item" heading
                }));
            if (PanelModel.Content(view).All(b => IsBox(b))) { view.Blocks.Clear(); Add(view, NothingHere(input, StartOf(input, null))); }
            Plate(view, "ui:chapter-stores", RecordedScope(input));
        }

        static void TraderPage(PanelInput input, PanelView view)
        {
            view.Heading = "Trader";
            HeroAndBar(view, null, CoinsByTrader(input), "coin spent", "coins spent", compact: true);
            var bought = Purchases(input); var colours = TraderColours(input);
            // one row per trader, in the bar's order (most coins first), what you bought there as chips in its colour
            var spent = input.Events?.Spent ?? new Dictionary<string, float>();
            var traders = bought.Select(b => b.trader).Distinct()
                                .OrderByDescending(t => spent.TryGetValue(t, out var c) ? c : 0).ThenBy(t => Who(input, t), StringComparer.OrdinalIgnoreCase).ToList();
            var ledger = LedgerRows(traders.Select(t => new Block
            {
                Kind = "row", Id = t, Title = Who(input, t), Colour = colours(t),
                Items = bought.Where(b => b.trader == t).GroupBy(b => b.item).Select(g => (item: g.Key, n: g.Sum(b => b.n)))
                              .OrderByDescending(r => r.n).ThenBy(r => r.item, StringComparer.Ordinal).Select(r => Chip(input, r.item, r.n, colours(t), false)).ToList(),
            }));
            if (ledger != null) { view.Blocks.Add(Section(BoughtPerTrader)); view.Blocks.Add(ledger); }
            if (view.Blocks.Count == 0) Add(view, NothingHere(input, StartOf(input, null)));   // rule C.3: one empty state, its date
            Plate(view, "ui:chapter-stores", RecordedScope(input));
        }

        // smelters, kilns, furnaces, refineries, the windmill and the spinning wheel all take ore or a raw good; the game
        // tells the mod only "fuel" for their fuel, so the fuel item is the vanilla one for its station (unknown: "Fuel")
        static readonly HashSet<string> OrePrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "CopperOre", "TinOre", "IronScrap", "IronOre", "SilverOre", "BlackMetalScrap", "FlametalOre", "FlametalOreNew", "CopperScrap", "BronzeScrap" };
        static string FuelOf(string station)
        {
            switch ((station ?? "").ToLowerInvariant())
            {
                case "smelter": case "blastfurnace": return "Coal";
                case "eitrrefinery": return "Sap";
                default: return null;
            }
        }
        const string FuelKey = "fuel";

        // (station, item or fuel prefab, is fuel, amount): what was put in by hand, or (chests) what a Stoker's Chest put in
        static List<(string station, string item, bool fuel, double n)> Smelted(PanelInput input, bool chests = false) =>
            ((chests ? input.Events?.ChestFed : input.Events?.SmelterAdded) ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).Select(kv =>
            {
                var p = Split2(kv.Key); var fuel = p[1] == FuelKey;
                return (p[0], fuel ? FuelOf(p[0]) ?? FuelKey : p[1], fuel, (double)kv.Value);
            }).ToList();

        /// <summary>The smelter colours (fix2 7, DESIGN-DIFF Hall: "Wood" and "Fuel" were two near-identical greys): ore in
        /// copper, fuel in one distinct coal grey (coal in the smelter, sap in the refinery; no blue: Hildir keeps hers), any other good (wood in the kiln,
        /// barley in the windmill) in its item's own icon colour.</summary>
        public const string OreColour = "#b8703a", FuelColour = "#6e6862", HildirColour = "#b8708c", FuelTone = "fuel";
        static readonly Dictionary<string, string> GoodColourFallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["Wood"] = "#b89a68", ["Barley"] = "#c8b060", ["Flax"] = "#8aa0b8", ["Sap"] = "#9a8c4a" };

        // a smelted good's part of the bar ("Ore", "Fuel" or the good itself) and its colour
        static string SmeltPart((string station, string item, bool fuel, double n) s) => s.fuel ? "Fuel" : OrePrefabs.Contains(s.item) ? "Ore" : s.item;
        static Func<string, string> SmeltColours(PanelInput input)
        {
            var tint = ItemTint(input); var spare = new Dictionary<string, string>();
            return part =>
            {
                if (part == "Ore") return OreColour;
                if (part == "Fuel") return FuelColour;
                var c = tint(Ask(input.ItemToken, part) ?? part) ?? (GoodColourFallback.TryGetValue(part, out var f) ? f : null);
                if (c != null) return c;
                if (!spare.TryGetValue(part, out var n)) spare[part] = n = Neutral[spare.Count % Neutral.Length];
                return n;
            };
        }

        /// <summary>What went into the smelters, by kind: ore, fuel, and any other good on its own (wood in the kiln, barley in the windmill).</summary>
        public static Block SmelterComposition(PanelInput input)
        {
            var smelted = Smelted(input);
            if (smelted.Count == 0) return null;
            var parts = new Dictionary<string, double>();
            foreach (var s in smelted) Bump(parts, SmeltPart(s), s.n);
            var total = parts.Values.Sum(); var colour = SmeltColours(input);
            return Tagged(new Block
            {
                Kind = "composition", Title = IntoSmelters, Value = N(total),
                Items = parts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => new Block
                {
                    Id = kv.Key, Title = kv.Key == "Ore" || kv.Key == "Fuel" ? kv.Key : Who(input, kv.Key), Value = N(kv.Value), Fraction = (float)(kv.Value / total),
                    Colour = colour(kv.Key),
                }).ToList(),
            }, SrcPc);
        }

        // the items put in, most first, each edged in its part's colour; the fuel marked as fuel (Coal 27 here is the bar's Fuel 27)
        static IEnumerable<(string item, bool fuel, double n, string colour)> SmeltItems(PanelInput input, IEnumerable<(string station, string item, bool fuel, double n)> smelted)
        {
            var colour = SmeltColours(input);
            return smelted.GroupBy(s => (s.item, s.fuel)).Select(g => (g.Key.item, g.Key.fuel, n: g.Sum(s => s.n), colour: colour(SmeltPart(g.First()))))
                          .OrderByDescending(r => r.n).ThenBy(r => r.item, StringComparer.Ordinal);
        }

        static Block SmelterRows(PanelInput input, IEnumerable<(string station, string item, bool fuel, double n)> smelted, int top)
        {
            var rows = SmeltItems(input, smelted).Take(top)
                              .Select(r => new Block { Kind = "item", Id = r.item, Icon = r.item == FuelKey ? "" : "item:" + r.item, Title = r.item == FuelKey ? "Fuel" : Who(input, r.item), Value = N(r.n),
                                                       Colour = r.colour, Tone = r.fuel ? FuelTone : null }).ToList();
            return rows.Count == 0 ? null : Tagged(new Block { Kind = "itemgrid", Items = rows, Note = rows.Any(r => r.Tone == FuelTone) ? FuelNote : null }, SrcPc);
        }

        /// <summary>The caveat under fuel (fix-rest, review: the fuel pictures were a guess, unflagged): the game tells the mod only that
        /// something went in as fuel, so the picture is the station's usual fuel (FuelOf).</summary>
        public const string FuelNote = "The game does not name the fuel: each station's usual fuel is shown.";

        /// <summary>True when this game has the Stoker's Chest (StokerHooks found the OverDrive-SmelterUpgrades mod on this PC). The mod is enforced
        /// on the server, so every fellow's book here has the chests too.</summary>
        public static bool StokersChests;
        /// <summary>Stoker's Chests feed the smelters here: the mod is on this PC, or the book counted a chest's feeding.</summary>
        public static bool ChestsFeed(PanelInput input) => StokersChests || (input?.Events?.ChestFed.Values.Any(v => v > 0) ?? false);
        public const string FedByChests = "Fed by Stoker's Chests", ChestsNoneYet = "Nothing fed by a Stoker's Chest yet. Its feeding shows here, apart from yours.";
        /// <summary>Under the chests' part: nobody's own work, and only what this PC (or the fellow's) hosted.</summary>
        public static string ChestNote(PanelInput input) => "Nobody's own work: what the chests put in while " + (input.IsSelf ? "your" : Name(input) + "'s") + " PC hosted them.";

        static void SmeltersPage(PanelInput input, PanelView view)
        {
            view.Heading = "Smelters";
            HeroAndBar(view, null, SmelterComposition(input), "item put in", "items put in", compact: true);
            var smelted = Smelted(input);
            // one row per station (its game name, alphabetical), what went in as chips in their part's colour, fuel marked
            var ledger = LedgerRows(smelted.Select(s => s.station).Distinct().OrderBy(s => Who(input, s), StringComparer.OrdinalIgnoreCase).Select(station => new Block
            {
                Kind = "row", Id = station, Title = Who(input, station),
                Items = SmeltItems(input, smelted.Where(s => s.station == station)).Select(r => Chip(input, r.item, r.n, r.colour, r.fuel)).ToList(),
            }));
            if (ledger != null && smelted.Any(s => s.fuel)) ledger.Note = FuelNote;
            if (ledger != null) { view.Blocks.Add(Section(PerStation)); view.Blocks.Add(ledger); }
            // a Stoker's Chest's feeding (StokerHooks): its own part, never in "items put in" (a chest's feeding is nobody's)
            var chest = SmelterRows(input, Smelted(input, chests: true), int.MaxValue);
            if (chest != null) chest.Note = ChestNote(input);
            if (view.Blocks.Count == 0 && chest == null) Add(view, NothingHere(input, StartOf(input, null)));
            if (chest != null) { view.Blocks.Add(Section(FedByChests)); view.Blocks.Add(chest); }
            else if (ChestsFeed(input)) Add(view, Empty(ChestsNoneYet));
            Plate(view, "ui:chapter-stores", RecordedScope(input));
        }
    }
}
