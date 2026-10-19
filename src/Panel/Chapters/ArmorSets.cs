using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Hearthwoven.Panel
{
    /// <summary>One ingredient of an armor piece's recipe as the game gives it (Recipe.m_resources at quality 1).</summary>
    public sealed class ArmorCost
    {
        public string Token;      // "$item_bronze"
        public string Name;       // its shown name ("Bronze"); null = the token, prettified
        public int Amount;
        public bool Refined;      // a smelter, kiln or furnace makes it (a bar)
    }

    /// <summary>One armor piece from the game's ObjectDB (GameData.ArmorPieces): a helmet, chest, legs or cape, any mod's too.</summary>
    public sealed class ArmorPiece
    {
        public string Token, Prefab, Name;
        /// <summary>The game's item type: Helmet, Chest, Legs or Shoulder.</summary>
        public string Slot;
        /// <summary>m_setName ("" = none), m_setSize, and the shown name of the set's status effect (null = none or not named).</summary>
        public string SetName = "", SetEffect;
        public int SetSize;
        /// <summary>The recipe that makes it: its station's prefab ("forge"; null = made by hand), the station's shown name, m_minStationLevel.</summary>
        public string Station, StationName;
        public int StationLevel;
        public bool HasRecipe;
        public List<ArmorCost> Recipe = new List<ArmorCost>();
        /// <summary>A piece the game hardens from a cast (the Deep North's gold armor): the cast's token, whose recipe and craft count stand for this piece; null = none.</summary>
        public string MadeAs;
    }

    /// <summary>A set: the game's own (m_setName) or the pieces Hearthwoven puts together (main ingredient + station), in its land.</summary>
    public sealed class ArmorSet
    {
        public string Id, Name;
        /// <summary>The main ingredient's name in small letters ("deer hide"), shown beside a name Hearthwoven gave; null when the name is the game's or the material's own.</summary>
        public string Material;
        public string MainToken;
        public bool GameNamed, HearthNamed;
        /// <summary>The land, as an index of PanelModel.ArmorLands (biome order); ArmorLands.Length = a land Hearthwoven cannot place.</summary>
        public int Land;
        public List<ArmorPiece> Pieces = new List<ArmorPiece>();
    }

    /// <summary>
    /// Deeds > Crafting > Armor sets (0.9, work/hearthwoven-0.9/DESIGN.md section 2): every armor set the game knows, by land in biome order, one
    /// band per set with its pieces as item tiles. Built at runtime from the game's own data, so a mod's armor joins in:
    ///   a set   = the game's set name (m_setName) when the pieces have one, else the pieces that share their main ingredient (the ingredient of the
    ///             latest land, then the highest material: Bronze for a Bronze Helmet, Linen Thread for a Padded Cuirass) and their crafting station.
    ///             The station's level is not part of it: a helmet often asks a lower level than its chest (Iron, Padded, the Drake Helmet), and
    ///             splitting there would make two sets of one. A cape alone in its group joins the set of its land with the same material.
    ///   a land  = the latest land among its ingredients (vanilla names), else its material's tier, else its station; Hearthwoven's own name for
    ///             the set of that land's metal (Deerstalker's Garb ... Emberforged) where the game gives none; a mod's set keeps its material's name.
    ///   made    = the game's own count of that item made (m_itemCraftStats: the character's whole life); a cast piece by its cast.
    /// A set the character knows no recipe of is not shown (Player.IsRecipeKnown); one line counts them. Pure C#: tested in test-panel.
    /// </summary>
    public static partial class PanelModel
    {
        public const string SetsPageId = "sets", SetsLabel = "Armor sets", SetsIcon = "title:smith";
        public const string SetBandTone = "setband", NotMadeTone = "notmade", PersonTileTone = "person";
        public const string SetFull = "made in full", LandFull = "every set made";
        public const string NeedsArmorMade = "0.9.0", ArmorMadeKey = "armorMade";

        /// <summary>The lands in journey order (Heightmap.Biome's shown names); a set Hearthwoven cannot place goes after them, as "Other lands".</summary>
        public static readonly string[] ArmorLands = { "Meadows", "Black Forest", "Swamp", "Mountains", "Plains", "Mistlands", "Ashlands", "Deep North" };
        public const string OtherLands = "Other lands";
        public static string LandName(int land) => land >= 0 && land < ArmorLands.Length ? ArmorLands[land] : OtherLands;

        static readonly string[] ArmorSlots = { "Helmet", "Chest", "Legs", "Shoulder" };
        /// <summary>An item type that is an armor piece (helmet, chest, legs, cape).</summary>
        public static bool IsArmorSlot(string itemType) => itemType != null && Array.IndexOf(ArmorSlots, itemType) >= 0;

        // the land an ingredient comes from, by its vanilla token; the latest land first, so "trollhide" is the Black Forest and not the Meadows' "hide".
        // Bone fragments, feathers, coal and the like turn up in many lands and mark none.
        static readonly (int land, string[] keys)[] IngredientLands =
        {
            (7, new[] { "bloodgold", "moose", "seal", "elaking", "nornathread", "frozenbranch", "timberwood", "liquidfrost", "mould" }),
            (6, new[] { "flametal", "askhide", "asksvin", "morgen", "celestial", "charred", "ashwood", "grausten", "ceramic", "bonemaw", "sulfur" }),
            (5, new[] { "carapace", "eitr", "scalehide", "mandible", "yggdrasil", "jute", "softtissue", "bilebag", "royaljelly", "blackcore", "wisp", "gjall", "seeker" }),
            (4, new[] { "blackmetal", "linen", "lox", "needle", "barley", "flax", "vile", "writhan" }),
            (3, new[] { "silver", "wolf", "fenris", "fenring", "drake", "freezegland", "obsidian", "crystal", "ulv", "cultist" }),
            (2, new[] { "iron", "guck", "chain", "ancientbark", "elderbark", "witheredbone", "bloodbag", "entrails", "ooze", "abomination" }),
            (1, new[] { "bronze", "troll", "finewood", "roundlog", "greydwarfeye", "surtling", "ancientseed", "bear" }),
            (0, new[] { "deerhide", "leatherscraps", "flint", "resin", "boar", "neck", "raspberr" }),
        };
        static readonly (int land, string token)[] IngredientLandsExact = { (2, "$item_root"), (1, "$item_copper"), (1, "$item_tin"), (4, "$item_tar"), (0, "$item_wood"), (0, "$item_stone") };

        /// <summary>The land an ingredient token comes from (an index of ArmorLands), or -1 when its name marks none.</summary>
        public static int LandOfIngredient(string token)
        {
            var t = (token ?? "").ToLowerInvariant();
            if (t.Length == 0) return -1;
            foreach (var e in IngredientLandsExact) if (t == e.token) return e.land;
            foreach (var e in IngredientLands) foreach (var k in e.keys) if (t.Contains(k)) return e.land;
            return -1;
        }

        // a material tier's land, when no ingredient names one (a mod's piece of a vanilla bar); a mod's own bar has none
        static int LandOfMaterial(string material)
        {
            switch (material)
            {
                case "Wood": case "Leather": return 0;
                case "Bronze": return 1;
                case "Iron": return 2;
                case "Silver": return 3;
                case "Black metal": return 4;
                case "Carapace": return 5;
                case "Flametal": return 6;
                default: return -1;
            }
        }

        // a vanilla station's land, the last guess: the workbench the Meadows, the forge the Black Forest, the black forge and the galdr table the Mistlands
        static int LandOfStation(string station)
        {
            var s = (station ?? "").ToLowerInvariant();
            if (s.Contains("blackforge") || s.Contains("magetable")) return 5;
            if (s.Contains("forge")) return 1;
            if (s.Contains("workbench")) return 0;
            return -1;
        }

        /// <summary>The ingredient that marks a piece: a mod's own bar first (no vanilla name knows it), then the one of the latest land, then the
        /// highest material tier, then the largest amount; null = no recipe.</summary>
        public static ArmorCost MainCost(ArmorPiece p)
        {
            ArmorCost best = null; var key = (mod: -1, land: -2, rank: -2, amount: -1);
            foreach (var c in p?.Recipe ?? new List<ArmorCost>())
            {
                if (c == null || string.IsNullOrEmpty(c.Token)) continue;
                var land = LandOfIngredient(c.Token); var rank = CostRank(c);
                var k = (mod: land < 0 && rank >= MaterialTiers.Length ? 1 : 0, land, rank, amount: c.Amount);
                var better = k.mod != key.mod ? k.mod > key.mod : k.land != key.land ? k.land > key.land : k.rank != key.rank ? k.rank > key.rank
                           : k.amount != key.amount ? k.amount > key.amount : best != null && string.CompareOrdinal(c.Token, best.Token) < 0;
                if (better) { best = c; key = k; }
            }
            return best;
        }

        // an ingredient's material tier; a refined good of a known land that is no tier (Refined Eitr, Linen Thread: a refinery and a spinning wheel
        // are smelters to the game) ranks with no material, so only a mod's own bar ranks above the vanilla metals
        static int CostRank(ArmorCost c)
        {
            var (material, rank) = MaterialOfIngredient(new Ingredient { Token = c.Token, Refined = c.Refined, Name = c.Name });
            return LandOfIngredient(c.Token) >= 0 && Array.IndexOf(MaterialTiers, material) < 0 ? -1 : rank;
        }

        /// <summary>A piece's land: the latest of its ingredients, else its main material's, else its station's; ArmorLands.Length when none says.</summary>
        public static int LandOfPiece(ArmorPiece p)
        {
            var land = (p?.Recipe ?? new List<ArmorCost>()).Select(c => LandOfIngredient(c?.Token)).DefaultIfEmpty(-1).Max();
            if (land < 0) { var m = MainCost(p); land = m == null ? -1 : LandOfMaterial(MaterialOfIngredient(new Ingredient { Token = m.Token, Refined = m.Refined, Name = m.Name }).material); }
            if (land < 0) land = LandOfStation(p?.Station);
            return land < 0 ? ArmorLands.Length : land;
        }

        // Hearthwoven's names for the set of a land's own metal where the game gives none (Joost's go on the sketch, 18 Oct 2026): by the main
        // ingredient and its land, given to the one set with the most pieces there
        static readonly (string key, int land, string name)[] HearthNames =
        {
            ("deerhide", 0, "Deerstalker's Garb"), ("bronze", 1, "Bronze-Clad"), ("iron", 2, "Cryptbreaker's Mail"), ("silver", 3, "Frostfang"),
            ("linen", 4, "Plainswarden"), ("carapace", 5, "Seeker-Shell"), ("flametal", 6, "Emberforged"),
        };

        /// <summary>The name the game gives a set: its bonus's shown name, else its set name made readable ("troll" -> "Troll").</summary>
        static string GameSetName(ArmorPiece p)
        {
            if (!string.IsNullOrWhiteSpace(p.SetEffect) && !p.SetEffect.StartsWith("$", StringComparison.Ordinal)) return p.SetEffect.Trim();
            var n = (p.SetName ?? "").Replace('_', ' ').Trim();
            return n.Length == 0 ? null : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(n);
        }

        static string CostName(ArmorCost c) => !string.IsNullOrWhiteSpace(c?.Name) && !c.Name.StartsWith("$", StringComparison.Ordinal) ? c.Name.Trim() : Prettify((c?.Token ?? "").Replace("$item_", ""));

        /// <summary>Every armor set of the game's data, by land and then by the station level of its first piece; pieces without a recipe left out.</summary>
        public static List<ArmorSet> ArmorSetsOf(IEnumerable<ArmorPiece> pieces)
        {
            var all = (pieces ?? Enumerable.Empty<ArmorPiece>()).Where(p => p != null && !string.IsNullOrEmpty(p.Token) && p.HasRecipe && IsArmorSlot(p.Slot))
                                                                 .GroupBy(p => p.Token).Select(g => g.First()).ToList();
            var groups = new List<ArmorSet>();
            foreach (var g in all.Where(p => !string.IsNullOrWhiteSpace(p.SetName)).GroupBy(p => p.SetName.Trim().ToLowerInvariant()))
            {
                var first = g.OrderBy(p => p.Token, StringComparer.Ordinal).First();
                groups.Add(new ArmorSet { Id = "set:" + g.Key, Name = GameSetName(first), GameNamed = true, Pieces = g.ToList() });
            }
            foreach (var g in all.Where(p => string.IsNullOrWhiteSpace(p.SetName)).GroupBy(p => (MainCost(p)?.Token ?? p.Token) + "|" + (p.Station ?? "")))
                groups.Add(new ArmorSet { Id = "mat:" + g.Key, Pieces = g.ToList() });
            foreach (var s in groups)
            {
                s.Pieces = s.Pieces.OrderBy(p => Array.IndexOf(ArmorSlots, p.Slot)).ThenBy(p => p.Name ?? p.Token, StringComparer.OrdinalIgnoreCase).ToList();
                s.Land = s.Pieces.Max(LandOfPiece);
                s.MainToken = s.Pieces.Select(MainCost).FirstOrDefault(c => c != null)?.Token;
            }
            // a cape alone in its group joins the set of its land made of the same material (the Wolf Fur Cape to the wolf armor), the largest first
            string MaterialOf(ArmorSet s) { var c = s.Pieces.Select(MainCost).FirstOrDefault(x => x != null); return c == null ? null : MaterialOfIngredient(new Ingredient { Token = c.Token, Refined = c.Refined, Name = c.Name }).material; }
            foreach (var cape in groups.Where(s => !s.GameNamed && s.Pieces.All(p => p.Slot == "Shoulder")).ToList())
            {
                var m = MaterialOf(cape); if (m == null) continue;
                var to = groups.Where(s => s != cape && !s.GameNamed && s.Land == cape.Land && s.Pieces.Any(p => p.Slot != "Shoulder") && MaterialOf(s) == m)
                               .OrderByDescending(s => s.Pieces.Count).ThenBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault();
                if (to == null) continue;
                to.Pieces.AddRange(cape.Pieces); groups.Remove(cape);
            }
            // names: the game's; Hearthwoven's for its land's metal; else the main ingredient's name. Two sets of one land with the same name: the material says which
            foreach (var s in groups.Where(s => !s.GameNamed))
            {
                var main = s.Pieces.Select(MainCost).FirstOrDefault(c => c != null);
                s.Name = main != null ? CostName(main) : s.Pieces[0].Name ?? Prettify(s.Pieces[0].Token);
            }
            foreach (var h in HearthNames)
            {
                var s = groups.Where(x => !x.GameNamed && x.Land == h.land && (x.MainToken ?? "").ToLowerInvariant().Contains(h.key))
                              .OrderByDescending(x => x.Pieces.Count).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                if (s == null) continue;
                s.Material = s.Name.ToLowerInvariant(); s.Name = h.name; s.HearthNamed = true;
            }
            foreach (var same in groups.GroupBy(s => s.Land + "|" + s.Name).Where(g => g.Count() > 1))
                foreach (var s in same.Where(x => !x.HearthNamed && !x.GameNamed && x.Pieces.Count > 0))
                    s.Name = s.Name + " (" + (s.Pieces[0].StationName ?? Prettify(s.Pieces[0].Station ?? "by hand")) + ")";
            foreach (var s in groups.Where(s => s.Pieces.Count > 0))
                foreach (var p in s.Pieces.Where(p => string.IsNullOrEmpty(p.Name))) p.Name = Prettify(p.Token);
            return groups.Where(s => s.Pieces.Count > 0)
                         .OrderBy(s => s.Land).ThenBy(s => s.Pieces.Min(p => p.StationLevel)).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ---------- made, known ----------

        /// <summary>How many of this piece the character made (the game's own count; a cast piece by its cast); 0 = none or not known.</summary>
        public static double MadeCount(IDictionary<string, float> crafted, ArmorPiece p)
        {
            if (crafted == null || p == null) return 0;
            double n = crafted.TryGetValue(p.Token, out var a) ? a : 0;
            if (!string.IsNullOrEmpty(p.MadeAs) && crafted.TryGetValue(p.MadeAs, out var b)) n += b;
            return n;
        }

        /// <summary>Whether this book made the piece: your own and a fellow's by their shared yes/no (ArmorMade), else by the counts this book carries.</summary>
        static bool Made(PanelInput i, ArmorPiece p) =>
            i?.ArmorMade != null && !i.IsSelf ? i.ArmorMade.Contains(p.Token) || (!string.IsNullOrEmpty(p.MadeAs) && i.ArmorMade.Contains(p.MadeAs)) : MadeCount(i?.ItemsCrafted, p) > 0;

        static bool Known(PanelInput i, ArmorPiece p)
        {
            if (i?.RecipeKnown == null) return false;
            try { return i.RecipeKnown(p.Token) || (!string.IsNullOrEmpty(p.MadeAs) && i.RecipeKnown(p.MadeAs)); } catch { return false; }
        }

        /// <summary>The armor tokens a character made, for their shared copy ("armorMade", yes or no per piece): every token the game counts as made
        /// whose item type is an armor piece, by name, at most <paramref name="max"/>. null = the game data is not read yet (nothing to say).</summary>
        public static List<string> ArmorMadeOf(IDictionary<string, float> crafted, Func<string, string> itemType, int max = 300)
        {
            if (crafted == null || itemType == null) return null;
            return crafted.Where(kv => kv.Value > 0 && !string.IsNullOrEmpty(kv.Key) && IsArmorSlot(Ask(itemType, kv.Key))).Select(kv => kv.Key)
                          .OrderBy(k => k, StringComparer.Ordinal).Take(max).ToList();
        }

        static List<ArmorSet> SetsOf(PanelInput i)
        {
            if (i?.ArmorPieces == null) return null;
            try { return ArmorSetsOf(i.ArmorPieces()); } catch { return null; }
        }

        /// <summary>A set made in full on this book: every piece made.</summary>
        static bool Full(PanelInput i, ArmorSet s) => s.Pieces.Count > 0 && s.Pieces.All(p => Made(i, p));

        /// <summary>The armor feat's number: the lands whose every set this book made in full (a land with no set does not count); null = no game data.</summary>
        public static double? LandsArmored(PanelInput i)
        {
            var sets = SetsOf(i);
            if (sets == null || (!i.IsSelf && i.ArmorMade == null && i.ItemsCrafted == null)) return null;
            return sets.Where(s => s.Land < ArmorLands.Length).GroupBy(s => s.Land).Count(g => g.All(s => Full(i, s)));
        }

        /// <summary>The tiers of the feat: one land, half the lands, every land (of the eight that have armor in the game).</summary>
        public static readonly double[] ArmorFeatTiers = { 1.0, Math.Ceiling(ArmorLands.Length / 2.0), ArmorLands.Length };

        // ---------- the page ----------

        public const string SetsWaitOne = "1 more set waits in lands further on. It appears once you know the recipe for one of its pieces.";
        static string SetsWait(int n) => n == 1 ? SetsWaitOne : N(n) + " more sets wait in lands further on. Each appears once you know the recipe for one of its pieces.";
        public const string SetsNoneKnown = "No armor set yet";
        public const string SetsNoneKnownLine = "A set appears here once you know the recipe for one of its pieces.";
        public const string SetsAboutMade = "A piece counts as made once your character has made it, as the game counts it for your whole character, and a set is made in full when every piece is.";
        public const string SetsAboutGroups = "Where the game names no set, the pieces that share a main material and a crafting station are one, and Hearthwoven names the set of each land's own metal, its material beside the name.";
        public const string SetsFellowLine = "Only the sets {name} began are shown: {their} copy does not say which recipes {they} know.";

        /// <summary>Deeds > Crafting > Armor sets: the hero (sets made in full, begun), then a land per island, a set per band with its pieces; or the
        /// group's sets (the Everyone chip), who made each in full.</summary>
        static void ArmorSetsPage(PanelInput input, PanelState state, PanelView view)
        {
            view.Recorded = true;
            view.Heading = SetsLabel;
            view.Scope = RecordedScope(input);
            var sets = SetsOf(input) ?? new List<ArmorSet>();
            if (state != null && state.Everyone && input.IsSelf && FellowsOf(input).Any()) { ArmorSetsGroup(input, view, sets); return; }
            bool Shown(ArmorSet s) => input.IsSelf ? s.Pieces.Any(p => Known(input, p) || Made(input, p)) : s.Pieces.Any(p => Made(input, p));
            var shown = sets.Where(Shown).ToList();
            var full = shown.Count(s => Full(input, s)); var begun = shown.Count(s => !Full(input, s) && s.Pieces.Any(p => Made(input, p)));
            Add(view, Hero((full > 0 ? N(full) : null, Label1(full, "set made in full", "sets made in full"), SrcCharacter, null),
                           (begun > 0 ? N(begun) : null, "begun", SrcCharacter, null)));
            if (shown.Count == 0) view.Blocks.Add(Empty(input.IsSelf ? SetsNoneKnown : "No armor set begun yet", input.IsSelf ? SetsNoneKnownLine : null));
            foreach (var land in shown.GroupBy(s => s.Land).OrderBy(g => g.Key))
            {
                var all = sets.Where(s => s.Land == land.Key).ToList();
                view.Blocks.Add(new Block { Kind = "section", Title = LandName(land.Key), Value = all.All(s => Full(input, s)) ? LandFull : null });
                foreach (var s in land) view.Blocks.Add(SetBand(input, s));
            }
            var waiting = sets.Count - shown.Count;
            if (input.IsSelf && waiting > 0) view.Blocks.Add(new Block { Kind = "note", Text = SetsWait(waiting) });
            if (!input.IsSelf) view.Blocks.Add(new Block { Kind = "note", Text = SetsFellowLine.Replace("{name}", Name(input)).Replace("{their}", "their").Replace("{they}", "they") });
            AboutNumbers(view, input, null, null, null, SetsAboutMade + " " + SetsAboutGroups);
            Plate(view, SetsIcon, RecordedScope(input));
        }

        /// <summary>One set as a band (the item grid's set variant): its name, the material small beside a name Hearthwoven gave, "2 of 3 made" or
        /// "made in full"; a tile per piece: made = the game's count, not made = the dashed tile with the recipe's materials under the name.</summary>
        static Block SetBand(PanelInput input, ArmorSet s)
        {
            var made = s.Pieces.Count(p => Made(input, p));
            return new Block
            {
                Kind = "itemgrid", Tone = SetBandTone, Id = s.Id, Title = s.Name, Text = s.Material, Selected = made == s.Pieces.Count,
                Value = made == s.Pieces.Count ? SetFull : N(made) + " of " + N(s.Pieces.Count) + " made", Src = SrcCharacter, Source = TagOfSrc(SrcCharacter),
                Items = s.Pieces.Select(p => PieceTile(input, p)).ToList(),
            };
        }

        static Block PieceTile(PanelInput input, ArmorPiece p)
        {
            var n = MadeCount(input.ItemsCrafted, p);
            if (Made(input, p)) return new Block { Kind = "item", Id = p.Token, Icon = "item:" + p.Token, Title = p.Name, Value = n > 0 ? N(n) : SetMadeWord, Src = SrcCharacter, Source = TagOfSrc(SrcCharacter) };
            return new Block
            {
                Kind = "item", Id = p.Token, Icon = "item:" + p.Token, Title = p.Name, Tone = NotMadeTone,
                Items = p.Recipe.Where(c => c != null && c.Amount > 0).Take(4).Select(c => new Block { Kind = "cost", Id = c.Token, Icon = "item:" + c.Token, Title = CostName(c), Value = N(c.Amount) }).ToList(),
            };
        }
        /// <summary>A fellow's piece they made where their copy says yes but no count: the tile says so in place of a number.</summary>
        public const string SetMadeWord = "made";

        public const string SetsGroupNobody = "nobody yet";
        public const string SetsGroupLine = "Each set shows who made every piece of it, in the group's order: you first, then by name.";

        /// <summary>The group's sets (the Everyone chip): per land and set, the players who made that set in full, in the fixed order (you first, then
        /// by name), never a count per player. A fellow whose copy does not say which armor they made is named once, below.</summary>
        static void ArmorSetsGroup(PanelInput input, PanelView view, List<ArmorSet> sets)
        {
            view.EveryoneOn = true;
            var people = FiresidePeople(input);
            bool Tells(PanelInput p) => p.IsSelf || p.ArmorMade != null;
            var shown = sets.Where(s => s.Pieces.Any(p => Known(input, p)) || people.Any(f => Tells(f.Input) && s.Pieces.Any(x => Made(f.Input, x)))).ToList();
            var together = shown.Count(s => people.Any(f => Tells(f.Input) && Full(f.Input, s)));
            Add(view, Hero((together > 0 ? N(together) : null, Label1(together, "set made in full in the group", "sets made in full in the group"), SrcFellows, null)));
            if (shown.Count == 0) view.Blocks.Add(Empty(SetsNoneKnown, SetsNoneKnownLine));
            foreach (var land in shown.GroupBy(s => s.Land).OrderBy(g => g.Key))
            {
                view.Blocks.Add(new Block { Kind = "section", Title = LandName(land.Key) });
                foreach (var s in land)
                {
                    var who = people.Where(f => Tells(f.Input) && Full(f.Input, s)).ToList();
                    view.Blocks.Add(new Block
                    {
                        Kind = "itemgrid", Tone = SetBandTone, Id = s.Id, Title = s.Name, Text = s.Material, Value = who.Count == 0 ? SetsGroupNobody : null, Src = SrcFellows, Source = TagOfSrc(SrcFellows),
                        Items = who.Select(f => new Block { Kind = "item", Id = f.Name, Icon = "person:" + f.Name, Colour = "person:" + f.Name, Tone = PersonTileTone, Value = f.Label, Title = Freshness(f.Input, input) }).ToList(),
                    });
                }
            }
            var waiting = sets.Count - shown.Count;
            if (waiting > 0) view.Blocks.Add(new Block { Kind = "note", Text = SetsWait(waiting) });
            foreach (var f in people.Where(f => !Tells(f.Input))) view.Blocks.Add(new Block { Kind = "note", Text = OlderLine(f.Input, "the sets " + f.Name + " made fill in") });
            view.AboutNumbers = new Block
            {
                Kind = "aboutnumbers", Id = NumbersTarget, Title = AboutNumbersTitle,
                Items = new List<Block> { new Block { Kind = "aboutline", Title = DetailsLabel, Text = SetsGroupLine + " Each player shares only which armor pieces they made, yes or no." } },
            };
            Plate(view, SetsIcon, null);
        }

        /// <summary>The Crafting page's heading-row chip that opens the sets (your own book).
        /// Short ("Sets", the helmet beside it): the heading row also holds nine window chips and Compare, and the page's heading must stay whole in it.</summary>
        public const string SetsChip = "Sets";
        static Block SetsLink() => new Block { Kind = "headlink", Id = "Deeds/" + SetsPageId, Title = SetsChip, Icon = "item:$item_helmet_bronze|vocab:list-gear-shared" };

        // ---------- the dev dump (hearthwoven_armorsets) ----------

        /// <summary>Every armor item of the game's data as tab-separated lines, so the grouping can be checked in game: token, name, slot, game set
        /// name, set size, set bonus, recipe station and level, main material, main ingredient, the land and the set it lands in. Items without a
        /// recipe say so (they are never shown).</summary>
        public static string ArmorDump(IEnumerable<ArmorPiece> pieces)
        {
            var list = (pieces ?? Enumerable.Empty<ArmorPiece>()).Where(p => p != null).ToList();
            var sets = ArmorSetsOf(list);
            var setOf = new Dictionary<string, ArmorSet>();
            foreach (var s in sets) foreach (var p in s.Pieces) setOf[p.Token] = s;
            var b = new StringBuilder();
            b.Append("token\tprefab\tname\tslot\tgame set name\tset size\tset bonus\tstation\tstation level\tmade as\tmain material\tmain ingredient\tland\tHearthwoven set\tset pieces\n");
            foreach (var p in list.OrderBy(p => setOf.TryGetValue(p.Token ?? "", out var s) ? sets.IndexOf(s) : int.MaxValue).ThenBy(p => p.Token, StringComparer.Ordinal))
            {
                var main = MainCost(p);
                setOf.TryGetValue(p.Token ?? "", out var set);
                var material = main == null ? "" : MaterialOfIngredient(new Ingredient { Token = main.Token, Refined = main.Refined, Name = main.Name }).material ?? "";
                b.Append(string.Join("\t", new[]
                {
                    p.Token, p.Prefab, p.Name, p.Slot, p.SetName, p.SetSize.ToString(CultureInfo.InvariantCulture), p.SetEffect ?? "",
                    p.HasRecipe ? p.Station ?? "(by hand)" : "(no recipe)", p.HasRecipe ? p.StationLevel.ToString(CultureInfo.InvariantCulture) : "", p.MadeAs ?? "",
                    material, main == null ? "" : main.Token + " (" + CostName(main) + ")", set == null ? "" : LandName(set.Land),
                    set == null ? "(not in a set: no recipe)" : set.Name + (set.Material != null ? " · " + set.Material : "") + (set.GameNamed ? " [game]" : set.HearthNamed ? " [Hearthwoven]" : ""),
                    set == null ? "" : set.Pieces.Count.ToString(CultureInfo.InvariantCulture),
                }.Select(x => (x ?? "").Replace('\t', ' ').Replace('\n', ' '))));
                b.Append('\n');
            }
            b.Append("# ").Append(list.Count).Append(" armor items, ").Append(sets.Count).Append(" sets in ").Append(sets.Select(s => s.Land).Distinct().Count()).Append(" lands\n");
            return b.ToString();
        }
    }
}
