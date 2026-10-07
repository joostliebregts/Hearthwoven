using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    public enum Chapter { Deeds, Company, Stores, Battle, Voyages, Skills }
    public enum TimeWindow { LastHour, LastThreeHours, Session }

    public class PanelState
    {
        public Chapter Chapter = Chapter.Deeds;
        public readonly Dictionary<Chapter, string> Page = new Dictionary<Chapter, string>();   // chosen left-list entry per chapter
        public TimeWindow Window = TimeWindow.Session;      // Battle only: filters the measured event log
        public string Biome = "";                           // Battle only: "" = all biomes; otherwise Heightmap.Biome name
        public string Player = "";                          // "" = yourself; otherwise a fellow player's name
        public bool TheyReceived = true;                    // Company: true = "They ate your food", false = "You ate their food"
        public bool ShowHow;                                // "How this was counted" unfolded
        public string Hotkey = "H";
        public string PageOf(Chapter c) => Page.TryGetValue(c, out var p) ? p : null;
    }

    /// <summary>
    /// One thing to draw. Kinds: headline, stat, tiles, bars, rows, titles, thread, section, empty, note.
    /// Icons are references the UI resolves to sprites: "item:Bread" or "item:$item_bread" (the game's item sprite),
    /// "piece:Cart", "skill:Blocking", "status:poison", "title:cook" (Hearthwoven art), "person:Edda"; anything that does
    /// not resolve shows its label's initial.
    /// </summary>
    public class Block
    {
        public string Kind, Id, Icon, Title, Value, Text, Note, Tone;
        public float Fraction;                              // bars: value / largest value on the same scale
        public bool Selected;
        public List<Block> Items;
    }

    public class Choice { public string Id, Label, Icon; public bool Selected; }

    public class PanelView
    {
        public string Title = "Hearthwoven", Owner, Scope, Heading, HowCounted, ShareNote, ListTitle;
        public Chapter Active;
        public string Page;
        public bool HasFilters, ShowHow;
        public readonly List<Choice> Chapters = new List<Choice>(), List = new List<Choice>(), Badges = new List<Choice>(), Toggle = new List<Choice>(),
                                     Windows = new List<Choice>(), Biomes = new List<Choice>(), Players = new List<Choice>();
        public readonly List<Block> Blocks = new List<Block>();
        public readonly List<string> Keys = new List<string>();
        public readonly Dictionary<string, int> PersonColors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // name -> palette index
    }

    /// <summary>
    /// The Hearthwoven panel as a view model: six chapters (Deeds, Company, Stores, Battle, Voyages, Skills), each with a
    /// left list and one page per entry, per the lead decision in work/branding-kritterstats/coordination/CLAUDE-REPLY.md.
    /// One owner page per metric; titles appear as badges on their owner page and as shortcuts in Deeds > Overview.
    /// Every value carries its source: "since this character was made" (the game's profile) or "measured" (Hearthwoven,
    /// this session). Pure C# (no Unity calls), unit-tested in test-panel/. Player-visible text has no em-dashes.
    /// </summary>
    public static class PanelModel
    {
        public const string SourceCharacter = "since this character was made";
        public const string SourceSession = "measured this session";
        public const string SourceFellows = "measured on their PCs";
        public const string NotYetRecorded = "Not yet recorded";
        public const string CompanyEmpty = "Shared deeds will appear here as they are recorded.";
        public const string ChestsOnServer = "Chest records live on the server; see the web page.";
        public const string NoDeaths = "No deaths recorded";
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
        static string CauseName(string cause)
        {
            switch (cause)
            {
                case "Fall": return "A fall";
                case "Drowning": return "Drowning";
                case "Burning": return "Burning";
                case "Freezing": return "The cold";
                case "Poisoned": return "Poison";
                case "Smoke": return "Smoke";
                case "Water": return "Water";
                case "EdgeOfWorld": return "The edge of the world";
                case "Impact": return "An impact";
                case "Cart": return "A cart";
                case "Tree": return "A falling tree";
                case "Self": return "Your own hand";
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
                case "unknown": case "Undefined": case "?": case "": case null: return "Something unseen";
                default: return null;
            }
        }

        /// <summary>A readable name when the game has none to give: "$item_fish_wraps" -> "Fish wraps", "BlobElite" -> "Blob Elite", "Beech1" -> "Beech".</summary>
        public static string Prettify(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return "Something unseen";
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

        static string Who(PanelInput input, string source)
        {
            var cause = CauseName(source);
            if (cause != null) return cause;
            var name = input.DisplayName?.Invoke(source);
            return string.IsNullOrEmpty(name) ? Prettify(source) : name;
        }


        // ---------- the measured event log: filters, deaths, hints ----------

        public class DamageRow { public DateTime Bucket; public string Biome, Dir, Other, Cause, Type; public float Amount; }

        public static IEnumerable<string> BiomesSeen(EventLog log)
        {
            if (log == null) return Enumerable.Empty<string>();
            var seen = new HashSet<string>(log.Damage.Keys.Select(k => k.Split('|')).Where(p => p.Length >= 2).Select(p => p[1]));
            foreach (var d in log.Deaths) seen.Add(d.Biome);
            return seen.OrderBy(b => { var i = Array.IndexOf(BiomeOrder, b); return i < 0 ? 99 : i; }).ThenBy(b => b, StringComparer.Ordinal);
        }

        static bool TryBucket(string iso, out DateTime utc) =>
            DateTime.TryParseExact(iso, "yyyy-MM-dd'T'HH:mm'Z'", Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out utc);

        /// <summary>Start of the window, or null for the whole session.</summary>
        public static DateTime? Cutoff(TimeWindow w, DateTime nowUtc) =>
            w == TimeWindow.LastHour ? nowUtc.AddHours(-1) : w == TimeWindow.LastThreeHours ? nowUtc.AddHours(-3) : (DateTime?)null;

        /// <summary>Damage rows inside the window and biome. A 10-minute bucket counts when any part of it lies in the window.</summary>
        public static List<DamageRow> Damage(EventLog log, TimeWindow w, string biome, DateTime nowUtc)
        {
            var rows = new List<DamageRow>();
            if (log == null) return rows;
            foreach (var kv in log.Damage)
            {
                var p = kv.Key.Split('|');
                if (p.Length < 6 || !InFilter(p[0], p[1], w, biome, nowUtc)) continue;
                TryBucket(p[0], out var t);
                rows.Add(new DamageRow { Bucket = t, Biome = p[1], Dir = p[2], Other = p[3], Cause = p[4], Type = p[5], Amount = kv.Value });
            }
            return rows;
        }

        static bool InFilter(string bucketIso, string rowBiome, TimeWindow w, string biome, DateTime nowUtc)
        {
            if (!TryBucket(bucketIso, out var t)) return false;
            var cutoff = Cutoff(w, nowUtc);
            if (cutoff.HasValue && t.AddMinutes(EventLog.BucketMinutes) <= cutoff.Value) return false;
            return string.IsNullOrEmpty(biome) || rowBiome == biome;
        }

        public static List<EventLog.Death> Deaths(EventLog log, TimeWindow w, string biome, DateTime nowUtc)
        {
            if (log == null) return new List<EventLog.Death>();
            var cutoff = Cutoff(w, nowUtc);
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
                    hints.Add(BiomeName(g.Key) + ": " + TypeName(t.Key).ToLowerInvariant() + " was " + Math.Round(t.Share * 100) + "% of the damage taken there. " + Remedy(t.Key) + " helps.");
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

        public static string WindowLabel(TimeWindow w) =>
            w == TimeWindow.LastHour ? "Last hour" : w == TimeWindow.LastThreeHours ? "Last three hours" : "This session";

        static string N(double v) => Math.Round(v).ToString("#,0", Inv);
        static string Km(double meters) { var km = meters / 1000.0; return (km < 100 ? km.ToString("0.0", Inv) : km.ToString("#,0", Inv)) + " km"; }
        static string Plural(double n, string one, string many) => N(n) + " " + (Math.Round(n) == 1 ? one : many);
        static string About(double seconds) => seconds < 90 ? "about " + N(seconds) + " seconds" : "about " + N(seconds / 60) + " minutes";
        static DateTime Local(PanelInput input, DateTime utc) => input.ToLocal != null ? input.ToLocal(utc) : utc.ToLocalTime();
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        static bool SameName(string a, string b) => !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        static string[] Split2(string key) { var i = key.IndexOf('|'); return i < 0 ? new[] { key, "" } : new[] { key.Substring(0, i), key.Substring(i + 1) }; }
        static void Bump(IDictionary<string, double> d, string k, double v) { d.TryGetValue(k, out var o); d[k] = o + v; }

        // Wording follows whose saga it is: "you" for yourself, the name for a fellow player.
        static string Name(PanelInput i) => string.IsNullOrEmpty(i.PlayerName) ? "this player" : i.PlayerName;
        static string Subject(PanelInput i) => i.IsSelf ? "you" : Name(i);
        static string Possessive(PanelInput i) => i.IsSelf ? "your" : Name(i) + "'s";
        public const string SourceTheirLast = "measured in their last session";
        public static string MeasuredOf(PanelInput i) => i.IsSelf ? SourceSession : SourceTheirLast;

        // A fellow's data is their latest shared copy, which can be days old: always say so, with the date.
        static string SessionScope(PanelInput i)
        {
            if (!i.IsSelf) return "their last session" + (i.LastRecordedUtc.HasValue ? ", " + Local(i, i.LastRecordedUtc.Value).ToString("d MMM HH:mm", Inv) : "");
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
            new SagaTitle("explorer", "Trailfinder", "Paths travelled, places found", Chapter.Voyages, "onfoot",
                new SagaLine(Profile, i => C(i, "DistanceTraveled") > 0 ? C(i, "DistanceTraveled") : C(i, "DistanceWalk") + C(i, "DistanceRun"), v => Km(v) + " travelled"),
                new SagaLine(Profile, i => C(i, "TreasureBuriedFound") + C(i, "TreasureDungeonFound") + C(i, "TreasureLocationFound"), v => Plural(v, "treasure found", "treasures found"))),
            new SagaTitle("woodcutter", "Woodcutter", "Trees felled, logs split", Chapter.Deeds, "woodcutting",
                new SagaLine(Measured, i => M(i, e => e.ChopHits), v => Plural(v, "axe hit on trees and logs", "axe hits on trees and logs")),
                new SagaLine(Profile, i => C(i, "Tree"), v => Plural(v, "tree felled", "trees felled"))),
            new SagaTitle("miner", "Stonebreaker", "Stone and ore broken", Chapter.Deeds, "mining",
                new SagaLine(Measured, i => M(i, e => e.PickaxeHits), v => Plural(v, "pickaxe hit", "pickaxe hits")),
                new SagaLine(Profile, i => C(i, "MineHits"), v => Plural(v, "pickaxe hit", "pickaxe hits"))),
            new SagaTitle("farmer", "Fieldkeeper", "Seeds planted, harvest gathered", Chapter.Deeds, "farming",
                new SagaLine(Profile, i => C(i, "HarvestCrop") + C(i, "HarvestBerry") + C(i, "HarvestMushroom") + C(i, "HarvestVine"), v => Plural(v, "harvest", "harvests")),
                new SagaLine(Profile, i => C(i, "BeesHarvested") + C(i, "SapHarvested"), v => Plural(v, "honey and sap harvest", "honey and sap harvests"))),
            new SagaTitle("cook", "Hearth Cook", "Food made, food others ate", Chapter.Deeds, "cooking",
                new SagaLine(Fellows, FedOthers, v => Plural(v, "meal eaten by companions", "meals eaten by companions")),
                new SagaLine(Profile, i => C(i, "CraftFood") + C(i, "CraftGrill"), v => Plural(v, "dish cooked or grilled", "dishes cooked or grilled"))),
            new SagaTitle("smith", "Forgekeeper", "Gear crafted and improved", Chapter.Deeds, "crafting",
                new SagaLine(Profile, i => { var g = C(i, "CraftWeapon") + C(i, "CraftArmor") + C(i, "CraftTool") + C(i, "Upgrades"); return g > 0 ? g : C(i, "Crafts"); },
                             v => Plural(v, "thing crafted or upgraded", "things crafted or upgraded")),
                new SagaLine(Measured, i => M(i, e => e.SmelterAdded), v => N(v) + " ore and fuel into smelters")),
            new SagaTitle("hauler", "Storekeeper", "Supplies stowed in chests and carts", Chapter.Stores, "carts",
                new SagaLine(Measured, i => M(i, e => e.CartMeters), v => N(v) + " m pulling a cart")),
            new SagaTitle("sailor", "Helmskeeper", "Distance at the helm", Chapter.Voyages, "sailing",
                new SagaLine(Profile, i => C(i, "DistanceSailHelm"), v => Km(v) + " at the helm")),
            new SagaTitle("mapmaker", "Mapmaker", "Your map shared with the group", Chapter.Voyages, "maps",
                new SagaLine(Measured, i => M(i, e => e.MapShared), v => v == 1 ? "Map shared once at the table" : "Map shared " + N(v) + " times at the table")),
            new SagaTitle("builder", "Hallwright", "Pieces raised with the hammer", Chapter.Deeds, "building",
                new SagaLine(Profile, i => C(i, "BuiltPiecesNoDebt") > 0 ? C(i, "BuiltPiecesNoDebt") : C(i, "Builds"), v => Plural(v, "piece built", "pieces built"))),
            new SagaTitle("mender", "Mender", "Repairs that keep the hall standing", Chapter.Deeds, "building",
                new SagaLine(Measured, i => M(i, e => e.Repairs), v => Plural(v, "repair with the hammer", "repairs with the hammer"))),
            new SagaTitle("wallwarden", "Wallwarden", "Defences built, armed and loaded", Chapter.Battle, "defense",
                new SagaLine(Profile, i => C(i, "BuildClusterDefense") + C(i, "TrapArmed") + C(i, "TurretAmmoAdded"), v => N(v) + " defences built, traps armed or turrets loaded")),
            new SagaTitle("defender", "Shieldbearer", "Blocks and well-timed parries", Chapter.Battle, "defense",
                new SagaLine(Measured, i => i.Events?.Blocks ?? 0, v => Plural(v, "block", "blocks")),
                new SagaLine(Measured, i => i.Events?.Parries ?? 0, v => Plural(v, "parry", "parries"))),
            new SagaTitle("fighter", "Battlehand", "Blows landed, foes fought", Chapter.Battle, "foes",
                new SagaLine(Profile, i => C(i, "EnemyKills"), v => Plural(v, "foe defeated", "foes defeated"))),
            new SagaTitle("bossbane", "Bossbane", "Forsaken faced and felled", Chapter.Battle, "foes",
                new SagaLine(Profile, i => C(i, "BossKills"), v => Plural(v, "boss fight won", "boss fights won"))),
            new SagaTitle("tamer", "Beastkeeper", "Creatures petted, named and led", Chapter.Deeds, "taming",
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

        /// <summary>Titles with evidence, in table order (no ranking). A zero line is left out, never shown as "0".</summary>
        public static List<TitleRow> Titles(PanelInput input)
        {
            var rows = new List<TitleRow>();
            foreach (var t in SagaTitles)
            {
                var row = new TitleRow { Id = t.Id, Title = t.Title, Descriptor = t.Descriptor, Chapter = t.Chapter, Page = t.Page };
                foreach (var l in t.Lines)
                {
                    double v; try { v = l.Amount(input); } catch { v = 0; }
                    if (v > 0) row.Lines.Add(new KeyValuePair<string, string>(l.Text(v, input), SourceOf(input, l.Source)));
                }
                if (row.Lines.Count > 0) rows.Add(row);
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
            (Chapter.Deeds, "Deeds", "ui:chapter-deeds"), (Chapter.Company, "Company", "ui:chapter-company"), (Chapter.Stores, "Stores", "ui:chapter-stores"),
            (Chapter.Battle, "Battle", "ui:chapter-battle"), (Chapter.Voyages, "Voyages", "ui:chapter-voyages"), (Chapter.Skills, "Skills", "ui:chapter-skills"),
        };

        static readonly (string id, string label, string icon)[] DeedsList =
        {
            ("overview", "Overview", ""), ("cooking", "Cooking", "title:cook"), ("building", "Building", "title:builder"), ("crafting", "Crafting", "title:smith"),
            ("woodcutting", "Woodcutting", "title:woodcutter"), ("mining", "Mining", "title:miner"), ("farming", "Farming", "title:farmer"),
            ("fishing", "Fishing", "title:fisher"), ("taming", "Taming", "title:tamer"),
        };
        static readonly (string id, string label, string icon)[] StoresList =
            { ("stocked", "Stocked", "piece:piece_chest_wood"), ("taken", "Taken", "piece:piece_chest_wood"), ("carts", "Carts", "piece:Cart"), ("trader", "Trader", "item:Coins") };
        static readonly (string id, string label, string icon)[] BattleList =
            { ("overview", "Overview", ""), ("damage", "Damage", ""), ("defense", "Defense", ""), ("deaths", "Deaths", ""), ("foes", "Foes", "") };
        static readonly (string id, string label, string icon)[] VoyagesList =
            { ("sailing", "Sailing", ""), ("onfoot", "On foot", ""), ("maps", "Maps", "") };

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
                case Chapter.Company:
                    items = CompanyNames(input).Select(n => (n, !input.IsSelf && SameName(n, input.ViewerName) ? n + " (you)" : n, "person:" + n));
                    break;
                default:
                    items = SkillNames(input).Select(s => (s, SkillName(input, s), "skill:" + s));
                    break;
            }
            return items.Select(x => new Choice { Id = x.id, Label = x.label, Icon = x.icon }).ToList();
        }

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

        public static PanelView Build(PanelInput input, PanelState state)
        {
            input = input ?? new PanelInput();
            state = state ?? new PanelState();
            var view = new PanelView { Active = state.Chapter, Owner = string.IsNullOrEmpty(input.PlayerName) ? "You" : input.PlayerName, ShowHow = state.ShowHow };
            foreach (var c in ChapterRow) view.Chapters.Add(new Choice { Id = c.id.ToString(), Label = c.label, Icon = c.icon, Selected = c.id == state.Chapter });
            view.ListTitle = state.Chapter == Chapter.Company ? "Fireside company" : ChapterRow.First(c => c.id == state.Chapter).label;
            view.List.AddRange(ListOf(input, state.Chapter));
            var page = state.PageOf(state.Chapter);
            if (view.List.Count > 0 && !view.List.Any(l => l.Id == page)) page = view.List[0].Id;
            view.Page = page;
            foreach (var l in view.List) l.Selected = l.Id == page;
            var titles = Titles(input);
            foreach (var t in titles.Where(t => t.Chapter == state.Chapter && t.Page == page))
                view.Badges.Add(new Choice { Id = t.Id, Label = t.Title, Icon = "title:" + t.Id });

            switch (state.Chapter)
            {
                case Chapter.Deeds: Deeds(input, page, titles, view); break;
                case Chapter.Company: Company(input, page, state, view); break;
                case Chapter.Stores: Stores(input, page, view); break;
                case Chapter.Battle: Battle(input, page, state, view); break;
                case Chapter.Voyages: Voyages(input, page, view); break;
                default: Skills(input, page, view); break;
            }
            if (view.Blocks.Count == 0) view.Blocks.Add(new Block { Kind = "empty", Title = NotYetRecorded });

            view.Keys.Add("[Q/E] Chapter");
            if (view.List.Count > 1) view.Keys.Add("[W/S] " + (state.Chapter == Chapter.Company ? "Companion" : "List"));
            if (view.Toggle.Count > 0) view.Keys.Add("[A/D] Direction");
            if (!string.IsNullOrEmpty(view.HowCounted)) view.Keys.Add("[I] How this was counted");
            view.Keys.Add("[" + (string.IsNullOrEmpty(state.Hotkey) ? "Esc" : state.Hotkey + "/Esc") + "] Close");
            ColorPeople(view);
            return view;
        }

        static Block Stat(string icon, string value, string title, string text, string note) => new Block { Kind = "stat", Icon = icon, Value = value, Title = title, Text = text, Note = note };
        static Block Section(string title) => new Block { Kind = "section", Title = title };
        static Block Empty(string title, string text = null) => new Block { Kind = "empty", Title = title, Text = text };

        // the title's own lines on its owner page, each with its source
        static void TitleLines(PanelView view, List<TitleRow> titles, string id)
        {
            var t = titles.FirstOrDefault(x => x.Id == id);
            if (t == null) return;
            foreach (var l in t.Lines) view.Blocks.Add(Stat("", "", l.Key, null, l.Value));
        }

        static Block Rows(IEnumerable<KeyValuePair<string, double>> items, Func<string, string> label, Func<string, string> icon, string note, int top = RowTop, bool byName = false)
        {
            var list = items.Where(kv => kv.Value > 0);
            list = byName ? list.OrderBy(kv => label(kv.Key), StringComparer.OrdinalIgnoreCase) : list.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal);
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
        static IEnumerable<KeyValuePair<string, double>> D(IDictionary<string, float> d) => (d ?? new Dictionary<string, float>()).Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value));
        static IEnumerable<KeyValuePair<string, double>> Counters(PanelInput i, params (string stat, string label)[] stats) =>
            stats.Select(s => new KeyValuePair<string, double>(s.label, C(i, s.stat)));

        // ---------- Deeds ----------

        static void Deeds(PanelInput input, string page, List<TitleRow> titles, PanelView view)
        {
            var who = Name(input);
            view.Scope = who + " · " + SourceCharacter + " and " + SessionScope(input);
            view.HowCounted = "Since this character was made: the game's own counters. Measured: what Hearthwoven saw on " + (input.IsSelf ? "this PC" : who + "'s PC") +
                              " since installation, " + SessionScope(input) + ". Measured on their PCs: what fellow players who share recorded. Each title has its own count; there is no combined score.";
            switch (page)
            {
                case "overview":
                    view.Heading = "Names earned";
                    if (titles.Count == 0) { view.Blocks.Add(Empty(NotYetRecorded, (input.IsSelf ? "Your" : who + "'s") + " deeds will appear here as they are recorded.")); return; }
                    view.Blocks.Add(new Block
                    {
                        Kind = "titles",
                        // shortcuts only: the numbers live on the owner page the title jumps to
                        Items = titles.Select(t => new Block { Icon = "title:" + t.Id, Title = t.Title, Value = t.Descriptor, Id = t.Chapter + "/" + t.Page }).ToList(),
                    });
                    return;
                case "cooking":
                    {
                        var foods = new Dictionary<string, double>(); var eaters = new Dictionary<string, double>(); double feasts = 0;
                        foreach (var f in FellowsOf(input))
                        {
                            foreach (var kv in f.Events.AteFoodMadeBy)
                            {
                                var p = Split2(kv.Key);
                                if (!SameName(p[0], input.PlayerName)) continue;
                                Bump(foods, p[1], kv.Value); Bump(eaters, f.PlayerName, kv.Value);
                            }
                            foreach (var kv in f.Events.AteFromFeastOf)
                                if (long.TryParse(Split2(kv.Key)[0], NumberStyles.Integer, Inv, out var id) && id != 0 && id == input.PlayerId) feasts += kv.Value;
                        }
                        var fed = foods.Values.Sum();
                        if (fed > 0 || feasts > 0) view.Scope = who + " · companions' last sessions and " + SourceCharacter;
                        var made = C(input, "CraftFood") + C(input, "CraftGrill");
                        view.Heading = fed > 0 ? Plural(fed, "meal eaten by companions", "meals eaten by companions") : made > 0 ? Plural(made, "dish cooked or grilled", "dishes cooked or grilled") : "Cooking";
                        Add(view, Tiles(foods, input, SourceFellows));
                        if (eaters.Count > 0)
                        {
                            view.Blocks.Add(Section("Who ate " + (input.IsSelf ? "your" : who + "'s") + " food"));
                            Add(view, Rows(eaters, n => n, n => "person:" + n, SourceFellows, byName: true));
                        }
                        if (feasts > 0) view.Blocks.Add(Stat("", N(feasts), feasts == 1 ? "serving from " + Possessive(input) + " feast" : "servings from " + Possessive(input) + " feasts", null, SourceFellows));
                        if (fed > 0 && made > 0) view.Blocks.Add(Stat("", N(made), made == 1 ? "dish cooked or grilled" : "dishes cooked or grilled", null, SourceCharacter));
                        if (fed == 0 && made > 0) view.Blocks.Add(new Block { Kind = "note", Text = "Meals eaten by companions appear once a companion who shares eats food " + Subject(input) + " made." });
                        return;
                    }
                case "building":
                    view.Heading = "Building";
                    TitleLines(view, titles, "builder"); TitleLines(view, titles, "mender");
                    if (input.PiecesPlaced != null && input.PiecesPlaced.Count > 0)
                    {
                        view.Blocks.Add(Section("Pieces placed most"));
                        Add(view, Rows(D(input.PiecesPlaced), k => Who(input, k), k => "", SourceCharacter));
                    }
                    if (M(input, e => e.Repairs) > 0) { view.Blocks.Add(Section("Repaired")); Add(view, Rows(D(input.Events.Repairs), k => Who(input, k), k => "piece:" + k, MeasuredOf(input))); }
                    return;
                case "crafting":
                    {
                        view.Heading = "Crafting";
                        TitleLines(view, titles, "smith");
                        var smelted = new Dictionary<string, double>();
                        foreach (var kv in input.Events?.SmelterAdded ?? new Dictionary<string, float>()) { var item = Split2(kv.Key)[1]; if (item != "fuel") Bump(smelted, item, kv.Value); }
                        if (smelted.Count > 0) { view.Blocks.Add(Section("Into the smelters")); Add(view, Tiles(smelted, input, MeasuredOf(input))); }
                        var worn = new Dictionary<string, double>();
                        foreach (var f in FellowsOf(input))
                            foreach (var kv in f.Events.EquippedGearMadeBy) { var p = Split2(kv.Key); if (SameName(p[0], input.PlayerName)) Bump(worn, p[1], 1); }
                        if (worn.Count > 0) { view.Blocks.Add(Section("Gear companions equipped")); Add(view, Tiles(worn, input, SourceFellows, counts: false)); }
                        if (input.ItemsCrafted != null && input.ItemsCrafted.Count > 0)
                        {
                            view.Blocks.Add(Section("Crafted most"));
                            Add(view, Rows(D(input.ItemsCrafted), k => Who(input, k), k => "item:" + k, SourceCharacter));
                        }
                        return;
                    }
                case "woodcutting":
                    view.Heading = "Woodcutting";
                    TitleLines(view, titles, "woodcutter");
                    if (M(input, e => e.ChopHits) > 0) { view.Blocks.Add(Section("Axe hits per tree")); Add(view, Rows(D(input.Events.ChopHits), k => Who(input, k), k => "", MeasuredOf(input))); }
                    return;
                case "mining":
                    view.Heading = "Mining";
                    TitleLines(view, titles, "miner");
                    if (M(input, e => e.PickaxeHits) > 0) { view.Blocks.Add(Section("Pickaxe hits per rock")); Add(view, Rows(D(input.Events.PickaxeHits), k => Who(input, k), k => "", MeasuredOf(input))); }
                    return;
                case "farming":
                    view.Heading = "Farming";
                    Add(view, Rows(Counters(input, ("HarvestCrop", "Crops"), ("HarvestBerry", "Berries"), ("HarvestMushroom", "Mushrooms"), ("HarvestVine", "Vines"),
                                                   ("BeesHarvested", "Honey"), ("SapHarvested", "Sap")), k => k, k => "", SourceCharacter));
                    return;
                case "fishing":
                    view.Heading = "Fishing";
                    Add(view, Rows(Counters(input, ("FishCaught", "Caught"), ("FishHooked", "Hooked"), ("FishLost", "Lost")), k => k, k => "", SourceCharacter, byName: false));
                    return;
                default:   // taming
                    view.Heading = "Taming";
                    Add(view, Rows(Counters(input, ("TamedPetting", "Petted"), ("TamedCommand", "Led or told to stay"), ("CreatureTamed", "Tamed")), k => k, k => "", SourceCharacter));
                    return;
            }
        }

        // ---------- Company ----------

        /// <summary>
        /// One person linked to the saga's owner, both ways: what the owner did with their food, gear and feasts (recorded
        /// on the owner's PC) and what they did with the owner's (recorded on their PC, only if they share). Plus voyages.
        /// </summary>
        public class Companion
        {
            public string Name; public bool Shares;
            public double AteTheirFood, AteTheirFeast, SailedSeconds, HelmSeconds;   // the owner received
            public double AteOwnersFood, AteOwnersFeast;                             // they received
            public readonly SortedDictionary<string, double> Foods = new SortedDictionary<string, double>(), Items = new SortedDictionary<string, double>(),
                                                             OwnersFoods = new SortedDictionary<string, double>(), OwnersItems = new SortedDictionary<string, double>();
            public double Food => AteTheirFood + AteTheirFeast + AteOwnersFood + AteOwnersFeast;
            public int Kinds => (Food > 0 ? 1 : 0) + (Items.Count + OwnersItems.Count > 0 ? 1 : 0) + (SailedSeconds + HelmSeconds > 0 ? 1 : 0);
        }

        /// <summary>
        /// Everyone linked to the saga's owner, by name (alphabetical; never ranked). Fellow players who share are listed
        /// even without links yet. Leaves out the owner and unknown makers.
        /// </summary>
        public static List<Companion> Companions(PanelInput input, out int unnamedFeasts)
        {
            unnamedFeasts = 0;
            var people = new Dictionary<string, Companion>(StringComparer.OrdinalIgnoreCase);
            var me = input.PlayerName ?? "";
            Companion Get(string name)
            {
                if (string.IsNullOrEmpty(name) || name == "unknown" || SameName(name, me)) return null;
                if (!people.TryGetValue(name, out var c)) people[name] = c = new Companion { Name = name };
                return c;
            }
            var names = new Dictionary<long, string>();
            if (input.PlayerNames != null) foreach (var kv in input.PlayerNames) names[kv.Key] = kv.Value;
            foreach (var f in input.Fellows ?? new List<PanelInput>()) if (f != null && f.PlayerId != 0 && !string.IsNullOrEmpty(f.PlayerName)) names[f.PlayerId] = f.PlayerName;

            var ev = input.Events;
            if (ev != null)
            {
                foreach (var kv in ev.AteFoodMadeBy) { var p = Split2(kv.Key); var c = Get(p[0]); if (c == null) continue; c.AteTheirFood += kv.Value; Bump(c.Foods, p[1], kv.Value); }
                foreach (var kv in ev.AteFromFeastOf)
                {
                    long.TryParse(Split2(kv.Key)[0], NumberStyles.Integer, Inv, out var id);
                    string name = null;
                    if (id != 0) names.TryGetValue(id, out name);
                    if (name == null) { unnamedFeasts += (int)kv.Value; continue; }
                    var c = Get(name); if (c != null) c.AteTheirFeast += kv.Value;
                }
                foreach (var kv in ev.EquippedGearMadeBy) { var p = Split2(kv.Key); var c = Get(p[0]); if (c != null) Bump(c.Items, p[1], kv.Value); }
                foreach (var kv in ev.SailedWith) { var c = Get(kv.Key); if (c != null) c.SailedSeconds += kv.Value; }
                foreach (var kv in ev.SailedUnderHelmOf) { var c = Get(kv.Key); if (c != null) c.HelmSeconds += kv.Value; }
            }
            foreach (var f in input.Fellows ?? new List<PanelInput>())
            {
                if (f == null) continue;
                var c = Get(f.PlayerName); if (c == null) continue;
                c.Shares = true;
                if (f.Events == null) continue;
                foreach (var kv in f.Events.AteFoodMadeBy) { var p = Split2(kv.Key); if (SameName(p[0], me)) { c.AteOwnersFood += kv.Value; Bump(c.OwnersFoods, p[1], kv.Value); } }
                foreach (var kv in f.Events.AteFromFeastOf)
                    if (long.TryParse(Split2(kv.Key)[0], NumberStyles.Integer, Inv, out var id) && id != 0 && id == input.PlayerId) c.AteOwnersFeast += kv.Value;
                foreach (var kv in f.Events.EquippedGearMadeBy) { var p = Split2(kv.Key); if (SameName(p[0], me)) Bump(c.OwnersItems, p[1], kv.Value); }
            }
            return people.Values.Where(c => c.Shares || c.Kinds > 0).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static IEnumerable<string> CompanyNames(PanelInput input) => Companions(input, out _).Select(c => c.Name);

        static void Company(PanelInput input, string page, PanelState state, PanelView view)
        {
            var all = Companions(input, out var unnamedFeasts);
            var owner = input.IsSelf ? "you" : Name(input);
            view.HowCounted = "Each link is recorded on the PC of the one who ate, equipped or sailed. " + Cap(Possessive(input)) + " side: " + (input.IsSelf ? "this PC" : Name(input) + "'s PC") +
                              ", " + SessionScope(input) + ". Their side: their PC, latest session, only for players who share. Grilled food carries the name of whoever owns the grill. " +
                              "Equipping counts each time gear is put on, so gear is shown by name. Time aboard is checked every 10 seconds.";
            var c = all.FirstOrDefault(x => x.Name == page);
            if (c == null)
            {
                view.Heading = "Fireside company";
                view.Scope = Name(input) + " · " + SessionScope(input);
                view.Blocks.Add(Empty(NotYetRecorded, CompanyEmpty));
                return;
            }
            var viewer = !input.IsSelf && SameName(c.Name, input.ViewerName);
            var them = viewer ? "you" : c.Name;
            view.Heading = "Shared with " + (viewer ? "you" : c.Name);
            var theirCopy = (input.Fellows ?? new List<PanelInput>()).FirstOrDefault(f => f != null && SameName(f.PlayerName, c.Name));
            // "they ate" comes from their record (their last session, or yours when they are you); "you ate" from the owner's
            string theirScope = theirCopy == null ? "no record from " + c.Name + " yet" : theirCopy.IsSelf ? SessionScope(theirCopy) : c.Name + "'s " + SessionScope(theirCopy).Substring("their ".Length);
            view.Scope = Name(input) + " and " + c.Name + " · " + (state.TheyReceived ? theirScope : SessionScope(input));
            var theirSource = viewer ? "measured on your PC" : SourceFellows;
            view.Toggle.Add(new Choice { Id = "they", Label = Cap(input.IsSelf ? "they" : them) + " ate " + Possessive(input) + " food", Selected = state.TheyReceived });
            view.Toggle.Add(new Choice { Id = "you", Label = Cap(owner) + " ate " + (input.IsSelf ? "their" : viewer ? "your" : c.Name + "'s") + " food", Selected = !state.TheyReceived });
            if (state.TheyReceived)
            {
                if (!c.Shares) { view.Blocks.Add(Empty("No record from " + c.Name + " yet.")); }
                else
                {
                    Thread(view, input, c.OwnersFoods, "Food " + owner + " made", Cap(them) + " ate", viewer ? input.ViewerName : c.Name, theirSource);
                    if (c.AteOwnersFeast > 0) view.Blocks.Add(Stat("", N(c.AteOwnersFeast), (c.AteOwnersFeast == 1 ? "serving" : "servings") + " from " + Possessive(input) + " feast", null, theirSource));
                    if (c.OwnersItems.Count > 0) { view.Blocks.Add(Section(Cap(them) + " equipped gear " + owner + " made")); Add(view, Tiles(c.OwnersItems, input, theirSource, counts: false)); }
                    if (c.OwnersFoods.Count == 0 && c.AteOwnersFeast == 0 && c.OwnersItems.Count == 0) view.Blocks.Add(Empty(NotYetRecorded));
                }
            }
            else
            {
                Thread(view, input, c.Foods, "Food " + them + " made", Cap(owner) + " ate", Name(input), MeasuredOf(input));
                if (c.AteTheirFeast > 0) view.Blocks.Add(Stat("", N(c.AteTheirFeast), (c.AteTheirFeast == 1 ? "serving" : "servings") + " from " + (viewer ? "your" : c.Name + "'s") + " feast", null, MeasuredOf(input)));
                if (c.Items.Count > 0) { view.Blocks.Add(Section(Cap(owner) + " equipped gear " + them + " made")); Add(view, Tiles(c.Items, input, MeasuredOf(input), counts: false)); }
                if (c.HelmSeconds > 0) view.Blocks.Add(Stat("", "", Cap(them) + " held the helm for " + About(c.HelmSeconds), null, MeasuredOf(input)));
                if (c.Foods.Count == 0 && c.AteTheirFeast == 0 && c.Items.Count == 0 && c.HelmSeconds == 0) view.Blocks.Add(Empty(NotYetRecorded));
            }
            if (c.SailedSeconds > 0) view.Blocks.Add(Stat("piece:Karve", "", "Sailed together for " + About(c.SailedSeconds), null, MeasuredOf(input)));
            if (unnamedFeasts > 0 && !state.TheyReceived)
                view.Blocks.Add(new Block { Kind = "note", Text = Plural(unnamedFeasts, "feast serving", "feast servings") + " from a cook whose name is not known here." });
        }

        // food -> eater: each food in its slot with the native sprite, the count over the woven thread, the eater's shield
        static void Thread(PanelView view, PanelInput input, IDictionary<string, double> foods, string maker, string eater, string eaterName, string source)
        {
            if (foods.Count == 0) return;
            view.Blocks.Add(new Block
            {
                Kind = "thread", Title = maker, Text = eater, Icon = "person:" + eaterName, Note = source,
                Items = foods.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(TileTop)
                             .Select(kv => new Block { Icon = "item:" + kv.Key, Title = Who(input, kv.Key), Value = N(kv.Value) }).ToList(),
            });
        }

        // ---------- Stores ----------

        static void Stores(PanelInput input, string page, PanelView view)
        {
            view.Scope = Name(input) + " · " + SessionScope(input);
            view.HowCounted = "Carts: metres pulled, checked every 10 seconds. Trader: what was bought and the coins paid. Measured on " + (input.IsSelf ? "this PC" : Name(input) + "'s PC") + ".";
            switch (page)
            {
                case "stocked": view.Heading = "Stocked"; view.Blocks.Add(Empty(ChestsOnServer)); return;
                case "taken": view.Heading = "Taken"; view.Blocks.Add(Empty(ChestsOnServer)); return;
                case "carts":
                    {
                        var m = M(input, e => e.CartMeters);
                        view.Heading = m > 0 ? N(m) + " m pulling a cart" : "Carts";
                        if (m == 0) view.Blocks.Add(Empty(NotYetRecorded));
                        else Add(view, Rows(D(input.Events.CartMeters), k => Who(input, k), k => "piece:" + k, MeasuredOf(input)));
                        return;
                    }
                default:
                    {
                        var coins = M(input, e => e.Spent);
                        view.Heading = coins > 0 ? Plural(coins, "coin spent", "coins spent") : "Trader";
                        var bought = new Dictionary<string, double>();
                        foreach (var kv in input.Events?.Bought ?? new Dictionary<string, float>()) Bump(bought, Split2(kv.Key)[1], kv.Value);
                        Add(view, Tiles(bought, input, MeasuredOf(input)));
                        if (coins > 0) Add(view, Rows(D(input.Events.Spent), k => Who(input, k), k => "", MeasuredOf(input)));
                        if (coins == 0 && bought.Count == 0) view.Blocks.Add(Empty(NotYetRecorded));
                        return;
                    }
            }
        }

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
            var biome = state.Biome ?? "";
            var where = string.IsNullOrEmpty(biome) ? "All biomes" : BiomeName(biome);
            var now = input.NowUtc;
            var rows = Damage(input.Log, state.Window, biome, now);
            var taken = rows.Where(r => r.Dir == "taken").ToList();
            var deaths = Deaths(input.Log, state.Window, biome, now);
            var windowed = page == "overview" || page == "damage" || page == "deaths";
            view.HasFilters = windowed;
            if (windowed)
            {
                foreach (TimeWindow w in Enum.GetValues(typeof(TimeWindow)))
                    view.Windows.Add(new Choice { Id = w.ToString(), Label = WindowLabel(w), Selected = w == state.Window });
                view.Biomes.Add(new Choice { Id = "", Label = "All biomes", Selected = string.IsNullOrEmpty(biome) });
                foreach (var b in BiomesSeen(input.Log)) view.Biomes.Add(new Choice { Id = b, Label = BiomeName(b), Selected = b == biome });
                view.Scope = (string.IsNullOrEmpty(biome) ? "All biomes" : BiomeName(biome) + " combat") + " · all enemies · " + WindowLabel(state.Window).ToLowerInvariant() +
                             (input.IsSelf ? "" : " · " + Name(input) + ", " + SessionScope(input));
            }
            else view.Scope = Name(input) + " · " + SourceCharacter + " and " + SessionScope(input);
            view.HowCounted = "Damage dealt: before the target's resistances. Damage received: after armour and resistances. The time filter counts in 10-minute steps, so a window can reach " +
                              "up to 10 minutes further back. A death's cause is the last hit before it; its damage type is what hurt most in its last 10 seconds. " +
                              "Blocks include parries; a parry follows the game's own rule. Measured on " + (input.IsSelf ? "this PC" : Name(input) + "'s PC") + ", " + SessionScope(input) + ".";
            // a fellow's copy can be older than the window: say when it is from, never "nothing happened"
            var cutoff = Cutoff(state.Window, now);
            if (windowed && !input.IsSelf && cutoff.HasValue && (!input.LastRecordedUtc.HasValue || input.LastRecordedUtc.Value.AddMinutes(EventLog.BucketMinutes) <= cutoff.Value))
            {
                view.Heading = input.LastRecordedUtc.HasValue ? Name(input) + "'s last record is from " + Local(input, input.LastRecordedUtc.Value).ToString("d MMM HH:mm", Inv) : "No record from " + Name(input) + " yet";
                view.Blocks.Add(Empty("Nothing from this window", "Choose This session to see " + Name(input) + "'s last session."));
                return;
            }
            switch (page)
            {
                case "overview":
                    {
                        var total = taken.Sum(r => r.Amount);
                        view.Heading = total > 0 ? N(total) + " damage points received" : "No damage received";
                        Add(view, Bars(Sum(taken, r => r.Type), TypeName, t => t, TypeIcon, "after resistance"));
                        view.Blocks.Add(new Block { Kind = "divider" });
                        DeathSummary(view, input, deaths);
                        // blocks are counted per session, not per biome or window: they live on Defense only
                        if ((input.Events?.Blocks ?? 0) > 0) view.Blocks.Add(new Block { Kind = "link", Icon = "item:ShieldWood", Title = "Blocks and parries: see Defense", Id = "Battle/defense" });
                        foreach (var h in Hints(rows, deaths).Take(HintTop)) view.Blocks.Add(new Block { Kind = "note", Tone = "hint", Text = h });
                        return;
                    }
                case "damage":
                    {
                        var all = Damage(input.Log, state.Window, "", now);   // biome bars always compare every biome
                        var dealt = all.Where(r => r.Dir == "dealt").Sum(r => r.Amount);
                        view.Heading = dealt > 0 ? N(dealt) + " damage dealt" : "Damage";
                        view.Blocks.Add(Section("Dealt, by biome"));
                        Add(view, Bars(Sum(all.Where(r => r.Dir == "dealt"), r => r.Biome), BiomeName, _ => "ember", _ => "", "before the target's resistance", biome));
                        view.Blocks.Add(Section("Foes struck · " + where));
                        Add(view, Rows(Sum(rows.Where(r => r.Dir == "dealt"), r => r.Other).Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value)), k => Who(input, k), k => "", MeasuredOf(input)));
                        view.Blocks.Add(Section("What hurt most · " + where));
                        Add(view, Rows(Sum(taken, r => r.Other).Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value)), k => Who(input, k), k => "", "after resistance"));
                        if (dealt == 0 && taken.Count == 0) view.Blocks.Add(Empty(NotYetRecorded));
                        return;
                    }
                case "defense":
                    {
                        var ev = input.Events;
                        view.Heading = (ev?.Blocks ?? 0) > 0 ? Plural(ev.Blocks, "successful block", "successful blocks") : "Defense";
                        if ((ev?.Blocks ?? 0) > 0) view.Blocks.Add(Stat("item:ShieldWood", N(ev.Blocks), ev.Blocks == 1 ? "successful block" : "successful blocks", "including " + Plural(ev.Parries, "parry", "parries"), MeasuredOf(input)));
                        var defences = Rows(Counters(input, ("BuildClusterDefense", "Defences built"), ("TrapArmed", "Traps armed"), ("TurretAmmoAdded", "Turrets loaded")), k => k, k => "", SourceCharacter, byName: false);
                        if (defences != null) { view.Blocks.Add(Section("Base defences")); view.Blocks.Add(defences); }
                        return;
                    }
                case "deaths":
                    {
                        view.Heading = deaths.Count == 0 ? NoDeaths : Plural(deaths.Count, "death", "deaths");
                        foreach (var d in deaths.Take(DeathTop))
                        {
                            var type = DominantType(d);
                            view.Blocks.Add(Stat(TypeIcon(type), "", type.Length > 0 ? TypeName(type) + " · " + Who(input, d.Killer) : Who(input, d.Killer),
                                                 BiomeName(d.Biome) + " · " + Local(input, d.Time).ToString("d MMM HH:mm", Inv), null));
                        }
                        foreach (var h in Hints(rows, deaths).Take(HintTop)) view.Blocks.Add(new Block { Kind = "note", Tone = "hint", Text = h });
                        var lifetime = input.Character?.Where(kv => kv.Key.StartsWith("DeathBy", StringComparison.Ordinal) && kv.Value > 0)
                                            .Select(kv => new KeyValuePair<string, double>(kv.Key.Substring(7), kv.Value)).ToList();
                        if (lifetime != null && lifetime.Count > 0)
                        {
                            view.Blocks.Add(Section("All deaths by cause"));
                            Add(view, Rows(lifetime, k => DeathCause(k), k => "", SourceCharacter));
                        }
                        return;
                    }
                default:   // foes
                    {
                        var kills = C(input, "EnemyKills");
                        view.Heading = kills > 0 ? Plural(kills, "foe defeated", "foes defeated") : "Foes";
                        if (C(input, "BossKills") > 0) view.Blocks.Add(Stat("", N(C(input, "BossKills")), C(input, "BossKills") == 1 ? "boss fight won" : "boss fights won", null, SourceCharacter));
                        if (input.EnemyKills != null && input.EnemyKills.Count > 0)
                        {
                            view.Blocks.Add(Section("Defeated most"));
                            Add(view, Rows(D(input.EnemyKills), k => Who(input, k), k => "", SourceCharacter));
                        }
                        if (kills == 0 && (input.EnemyKills == null || input.EnemyKills.Count == 0)) view.Blocks.Add(Empty(NotYetRecorded));
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
            if (deaths.Count == 0) { view.Blocks.Add(Stat("", "", NoDeaths, null, null)); return; }
            foreach (var g in deaths.GroupBy(d => DominantType(d)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
            {
                var label = (g.Count() == 1 ? "death" : "deaths") + (g.Key.Length > 0 ? " from " + TypeName(g.Key).ToLowerInvariant() : "");
                view.Blocks.Add(Stat(TypeIcon(g.Key), N(g.Count()), label, string.Join("  ·  ", g.Take(3).Select(d => Local(input, d.Time).ToString("d MMM HH:mm", Inv)).ToArray()), null));
            }
        }

        // ---------- Voyages ----------

        static void Voyages(PanelInput input, string page, PanelView view)
        {
            view.Scope = Name(input) + " · " + SourceCharacter + " and " + SessionScope(input);
            view.HowCounted = "Distances come from the game's own counters since this character was made. Time aboard with others is measured, checked every 10 seconds.";
            switch (page)
            {
                case "sailing":
                    {
                        view.Heading = C(input, "DistanceSail") > 0 ? Km(C(input, "DistanceSail")) + " sailed" : "Sailing";
                        if (C(input, "DistanceSailHelm") > 0) view.Blocks.Add(Stat("", "", Km(C(input, "DistanceSailHelm")) + " at the helm", null, SourceCharacter));
                        var ev = input.Events;
                        if (ev != null && ev.SailedWith.Count > 0)
                        {
                            view.Blocks.Add(Section("Sailed with"));
                            view.Blocks.Add(new Block { Kind = "rows", Note = MeasuredOf(input), Items = ev.SailedWith.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                                .Select(kv => new Block { Icon = "person:" + kv.Key, Title = kv.Key, Value = About(kv.Value) }).ToList() });
                        }
                        if (ev != null && ev.SailedUnderHelmOf.Count > 0)
                        {
                            view.Blocks.Add(Section("Under the helm of"));
                            view.Blocks.Add(new Block { Kind = "rows", Note = MeasuredOf(input), Items = ev.SailedUnderHelmOf.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                                .Select(kv => new Block { Icon = "person:" + kv.Key, Title = kv.Key, Value = About(kv.Value) }).ToList() });
                        }
                        return;
                    }
                case "onfoot":
                    view.Heading = C(input, "DistanceTraveled") > 0 ? Km(C(input, "DistanceTraveled")) + " travelled" : "On foot";
                    Add(view, new Block
                    {
                        Kind = "rows", Note = SourceCharacter,
                        Items = new[] { ("DistanceWalk", "Walked"), ("DistanceRun", "Ran") }.Where(s => C(input, s.Item1) > 0)
                                .Select(s => new Block { Title = s.Item2, Value = Km(C(input, s.Item1)) }).ToList(),
                    });
                    Add(view, Rows(Counters(input, ("TreasureBuriedFound", "Buried treasure found"), ("TreasureDungeonFound", "Dungeon treasure found"),
                                                   ("TreasureLocationFound", "Treasure found at places"), ("PortalDungeonIn", "Dungeons entered")), k => k, k => "", SourceCharacter));
                    view.Blocks.RemoveAll(b => b.Kind == "rows" && (b.Items == null || b.Items.Count == 0));
                    return;
                default:   // maps
                    {
                        var m = M(input, e => e.MapShared);
                        view.Heading = m > 0 ? (m == 1 ? "Map shared once at the table" : "Map shared " + N(m) + " times at the table") : "Maps";
                        if (m == 0) view.Blocks.Add(Empty(NotYetRecorded));
                        else view.Blocks.Add(Stat("piece:piece_cartographytable", N(m), m == 1 ? "map shared" : "maps shared", null, MeasuredOf(input)));
                        return;
                    }
            }
        }

        // ---------- Skills ----------

        static void Skills(PanelInput input, string page, PanelView view)
        {
            view.Scope = Name(input) + " · level now and " + SessionScope(input);
            view.HowCounted = "Level: the skill as it stands now. Practice: the game's own raise amounts added up this session; not a contribution score.";
            if (string.IsNullOrEmpty(page)) { view.Heading = "Skills"; view.Blocks.Add(Empty(NotYetRecorded)); return; }
            view.Heading = SkillName(input, page);
            double level = 0, practice = 0;
            if (input.SkillLevels != null && input.SkillLevels.TryGetValue(page, out var l)) level = l;
            if (input.Events != null && input.Events.SkillPractice.TryGetValue(page, out var p)) practice = p;
            if (level > 0) view.Blocks.Add(Stat("skill:" + page, Math.Floor(level).ToString("0", Inv), "level", null, "now"));
            if (practice > 0) view.Blocks.Add(Stat("", practice.ToString("0.0", Inv), "practice", null, MeasuredOf(input)));
        }

        // ---------- navigation (keys) ----------

        public static void StepChapter(PanelState s, int d)
        {
            var n = Enum.GetValues(typeof(Chapter)).Length;
            s.Chapter = (Chapter)(((int)s.Chapter + d + n) % n);
        }

        /// <summary>W/S: move through the left list of the current chapter (wraps).</summary>
        public static void StepList(PanelState s, PanelView v, int d)
        {
            if (v.List.Count == 0) return;
            var i = Math.Max(0, v.List.FindIndex(c => c.Selected));
            s.Page[s.Chapter] = v.List[((i + d) % v.List.Count + v.List.Count) % v.List.Count].Id;
        }

        /// <summary>Jump from a Deeds > Overview title to its owner page ("Battle/defense").</summary>
        public static void Jump(PanelState s, string target)
        {
            var p = (target ?? "").Split('/');
            if (p.Length == 2 && Enum.TryParse(p[0], out Chapter c)) { s.Chapter = c; s.Page[c] = p[1]; }
        }

        public const int Palette = 8;

        /// <summary>
        /// A colour per person that no one else in the group shares (up to eight people): by name, so it stays the same
        /// across screens. Recognition only, never an order of worth.
        /// </summary>
        public static Dictionary<string, int> PersonColors(IEnumerable<string> names)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); int k = 0;
            foreach (var n in (names ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                map[n] = k++ % Palette;
            return map;
        }

        static void ColorPeople(PanelView v)
        {
            IEnumerable<string> People(IEnumerable<Choice> cs) => cs.Where(c => c.Icon != null && c.Icon.StartsWith("person:")).Select(c => c.Icon.Substring(7));
            IEnumerable<string> InBlocks(IEnumerable<Block> bs) => bs.SelectMany(b => (b.Icon != null && b.Icon.StartsWith("person:") ? new[] { b.Icon.Substring(7) } : new string[0]).Concat(InBlocks(b.Items ?? new List<Block>())));
            v.PersonColors.Clear();
            foreach (var kv in PersonColors(People(v.Players).Concat(People(v.List)).Concat(InBlocks(v.Blocks)).Concat(new[] { v.Owner }))) v.PersonColors[kv.Key] = kv.Value;
        }

        // ---------- the player switcher ----------

        public const string ShareOffNote = "You keep your stats to yourself, so you see only your own. Turn on ShareWithGroup in the mod settings to see your fellow players.";
        public const string ShareWaitingNote = "Fellow players appear here once they share too.";

        /// <summary>You first, then fellow players who share, by name. Sharing off: no switcher, one line on how to turn it on.</summary>
        public static void AddPlayers(PanelView view, string me, IEnumerable<string> others, string selected, bool sharing)
        {
            view.Players.Clear();
            if (!sharing) { view.ShareNote = ShareOffNote; return; }
            var list = (others ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n) && !SameName(n, me))
                                                             .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            var pick = list.Contains(selected ?? "", StringComparer.OrdinalIgnoreCase) ? selected : "";
            view.Players.Add(new Choice { Id = "", Label = string.IsNullOrEmpty(me) ? "You" : me, Icon = "person:" + me, Selected = string.IsNullOrEmpty(pick) });
            foreach (var n in list) view.Players.Add(new Choice { Id = n, Label = n, Icon = "person:" + n, Selected = SameName(n, pick) });
            view.ShareNote = list.Count == 0 ? ShareWaitingNote : null;
            ColorPeople(view);
        }

        // ---------- JSON (for the static preview) ----------

        public static string ToJson(PanelView v)
        {
            var j = new Json().Open().Str("title", v.Title).Str("owner", v.Owner).Str("listTitle", v.ListTitle ?? "").Str("scope", v.Scope ?? "").Str("heading", v.Heading ?? "")
                .Str("howCounted", v.HowCounted ?? "").Num("showHow", v.ShowHow ? 1 : 0).Str("shareNote", v.ShareNote ?? "")
                .Str("active", v.Active.ToString()).Str("page", v.Page ?? "").Num("hasFilters", v.HasFilters ? 1 : 0);
            void Choices(string k, List<Choice> cs)
            {
                j.Key(k).OpenArr();
                foreach (var c in cs) j.Open().Str("id", c.Id ?? "").Str("label", c.Label ?? "").Str("icon", c.Icon ?? "").Num("selected", c.Selected ? 1 : 0).Close();
                j.CloseArr();
            }
            Choices("chapters", v.Chapters); Choices("list", v.List); Choices("badges", v.Badges); Choices("toggle", v.Toggle);
            Choices("windows", v.Windows); Choices("biomes", v.Biomes); Choices("players", v.Players);
            j.Key("keys").OpenArr(); foreach (var k in v.Keys) j.Open().Str("text", k).Close(); j.CloseArr();
            j.Dict("personColors", v.PersonColors.Select(kv => new KeyValuePair<string, float>(kv.Key, kv.Value + 1)));   // +1: Json.Dict leaves zeros out
            void B(Block b)
            {
                j.Open().Str("kind", b.Kind ?? "").Str("id", b.Id ?? "").Str("icon", b.Icon ?? "").Str("title", b.Title ?? "").Str("value", b.Value ?? "").Str("text", b.Text ?? "")
                 .Str("note", b.Note ?? "").Str("tone", b.Tone ?? "").Num("fraction", b.Fraction).Num("selected", b.Selected ? 1 : 0);
                j.Key("items").OpenArr(); foreach (var i in b.Items ?? new List<Block>()) B(i); j.CloseArr();
                j.Close();
            }
            j.Key("blocks").OpenArr(); foreach (var b in v.Blocks) B(b); j.CloseArr();
            return j.Close().ToString();
        }

        /// <summary>Every player-visible string in the view (for the no-em-dash check and localisation review).</summary>
        public static IEnumerable<string> AllText(PanelView v)
        {
            IEnumerable<string> Of(Block b) => new[] { b.Title, b.Value, b.Text, b.Note }.Concat((b.Items ?? new List<Block>()).SelectMany(Of));
            return new[] { v.Title, v.Owner, v.Scope, v.Heading, v.HowCounted, v.ShareNote }
                .Concat(new[] { v.Chapters, v.List, v.Badges, v.Toggle, v.Windows, v.Biomes, v.Players }.SelectMany(cs => cs.Select(c => c.Label)))
                .Concat(v.Keys).Concat(v.Blocks.SelectMany(Of))
                .Where(s => !string.IsNullOrEmpty(s));
        }
    }
}
