using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// One feat: a notable thing done, with a rule, a number to reach and a moment (ACHIEVEMENTS-06). Layered under the titles,
    /// never merged with them: a title says what kind of thing someone does, a feat a moment worth telling. One table, one
    /// place to tune every threshold (<see cref="PanelModel.FeatDefs"/>). Never ranked, never compared, no popup.
    /// </summary>
    public class FeatDef
    {
        public readonly string Id, Name, Family, Honours, Source, Caveat, Needs;
        public readonly Chapter Chapter; public readonly string Page;
        /// <summary>The numbers that earn the tiers, in order (one entry = a one-off feat; never more than three).</summary>
        public readonly double[] Tiers;
        /// <summary>The rule for one tier's number ("Carry 5 000 item-km ...").</summary>
        public readonly Func<double, string> Rule;
        /// <summary>The player's number now; null = Hearthwoven does not count this yet (its data key is missing): reads 0, Unsung.</summary>
        public readonly Func<PanelInput, double?> Value;
        /// <summary>The number is exact on this PC or in the character's record, so the player's own page may show "3 400 of 5 000".</summary>
        public readonly bool Exact;
        /// <summary>Worked out from fellow players' copies: the moment is "noticed", the latest it can have happened.</summary>
        public readonly bool Derived;
        /// <summary>Already over the line when Hearthwoven first counted: the moment is "before install" (a character's own record).</summary>
        public readonly bool Retro;
        /// <summary>The rule in a few words for a tile ("10 000 damage stopped"): the next tier's number and what it counts, about 22 characters at most.
        /// The full rule (<see cref="Rule"/>) stays in the detail area. "{your}" is said for whose book it is.</summary>
        public readonly Func<double, string> Brief;
        /// <summary>What the feat's number counts, "one|many" where the noun changes ("grave|graves"); said after every progress ("8 750 of 10 000 damage").</summary>
        public readonly string Unit;
        /// <summary>The number is a distance or a duration: its progress shows one decimal ("1.6 of 5 km").</summary>
        public readonly bool Decimals;
        /// <summary>A feat of the whole group (Iron for the Forge): one number for everyone who shares, on the Together page, never in a player's own Earned or Unsung, never in Known for.</summary>
        public readonly bool Group;
        /// <summary>A group feat's land (Heightmap.Biome name, "Swamp"): not on the Together page until the group found it (GroupVisible); null = no gate.</summary>
        public string Biome;
        /// <summary>The picture when it is not the family's emblem ("item:$item_bronze|title:smith": the game's own icon, the emblem when it has none).</summary>
        public string Picture;
        /// <summary>A group feat's riddle until someone who shares has held its material (FeatsGroup.cs); null = always its own name.</summary>
        public FeatVeil Veil;
        /// <summary>A group feat: one member's own part of the number (B37: "Who helped", each with their share); null = Iron for the Forge's crew from the
        /// server's book ("Carried by", names only). Helped: the row's heading in the feat's own verb ("Who cooked"). Overlaps: the number is a set (each
        /// different thing once), so members' parts overlap: their own counts, no per cent.</summary>
        public Func<PanelInput, double> Part; public string Helped = PanelModel.WhoHelped; public bool Overlaps;

        public FeatDef(string id, string name, string family, Chapter chapter, string page, string honours, double[] tiers, Func<double, string> rule,
                       Func<PanelInput, double?> value, string source, string caveat, bool exact = true, bool derived = false, bool retro = false, string needs = null,
                       Func<double, string> brief = null, string unit = null, bool decimals = false, bool group = false)
        {
            Brief = brief; Unit = unit; Decimals = decimals; Group = group;
            Id = id; Name = name; Family = family; Chapter = chapter; Page = page; Honours = honours; Tiers = tiers; Rule = rule; Value = value;
            Source = source; Caveat = caveat; Exact = exact; Derived = derived; Retro = retro; Needs = needs;
        }
    }

    /// <summary>
    /// The Feats page and its parts (design: work/hearthwoven-visual-vocabulary/ACHIEVEMENTS-06.md). Block kinds:
    ///   feats      the grid on a page of the Feats chapter (Earned, Unsung, Together); Items = Kind "feat" cards: Id, Icon (the family's title emblem), Title (name),
    ///              Level (tiers earned), Count (tiers in all), Tone "earned" | "unsung" | "waiting" (not counted yet: Text says so), Text (the moment, one small line),
    ///              Selected (the one the detail area shows), Items = [its detail].
    ///   featdetail the one fixed detail area: Icon, Title, Value ("II of III"), Text (what it honours), Tone, Items = Kind
    ///              "rule" (Title; Value = the tier's numeral; Selected = reached), "progress" (Title "3 400 of 5 000", Fraction),
    ///              "counted" (Text), "caveat" (Text), "moment" (Text; Note = a quiet "(or earlier)"), "crew" (a group feat: Title "Who cooked", Tone "share" when the parts add up; Items = who, Icon "person:Name", Value their part, Note "66 %", Fraction).
    ///              Every card carries its own, so a hover can fill the area.
    ///   Colour     on a card, the detail, a rule and a Known for / band item: the tier's colour (TierColour: I bronze, II silver, III and a one-off gold).
    ///   knownfor   Deeds > Overview: "Known for" and up to three feats (Items: Icon, Title, Value = numeral, Id = feat id); Id "Feats/earned".
    ///   featband   a thin band on a feat's owner page (Items as knownfor, Text = the moment); Id "Feats/earned".
    /// </summary>
    public static partial class PanelModel
    {
        // ---------- the table: every feat and every threshold (tune here) ----------

        public const string FeatsPageId = "earned", FeatsUnsungId = "unsung", FeatsTogetherId = "together", FeatsLink = "Feats/earned";
        public const string FeatsIcon = "vocab:list-feats", FeatsUnsungIcon = "vocab:list-unsung", FeatTone = "earned", FeatUnsung = "unsung";
        public const string FeatsTogetherWord = "Together", FeatsTogetherHeading = "Feats of the group";
        /// <summary>The Feats chapter's left list (its own chapter, Joost 2026-10-09): your feats earned, the ones not earned yet, the group's.</summary>
        static readonly (string id, string label, string icon)[] FeatsList =
            { (FeatsPageId, "Earned", FeatsIcon), (FeatsUnsungId, "Unsung", FeatsUnsungIcon), (FeatsTogetherId, FeatsTogetherWord, "vocab:list-together"), (TitlesPageId, TitlesHeading, TitlesIcon) };
        /// <summary>The Titles page (B18, Joost 2026-10-09: titles and feats stay two systems, explained): every title, the ones held first with why, then the
        /// ones not held yet with what earns them. A title in a page's strip opens it here ("Feats/titles/wallwarden"). The list icon: the laurel ring (no line icon
        /// for titles exists yet).</summary>
        public const string TitlesPageId = "titles", TitlesLink = "Feats/titles", TitlesIcon = "vocab:list-titles", TitlesHeading = "Titles", TitleKind = "title";
        public const string TitlesDefinition = "A title names a kind of work you do, earned the first time you do it. A feat is a moment you reach.";
        public const string TitleHeld = "Held", TitleNotHeld = "Not held yet", TitlePageWord = "Its page: ";
        /// <summary>The tiers' colours, Valheim metals (Joost: colour-code I, II, III): bronze, silver, gold. A one-off feat is earned in full: gold. Never a player's
        /// or a damage colour; always with its numeral or notches, never colour alone.</summary>
        public static readonly string[] TierColours = { "", "#c27a4a", "#c9d2da", "#f0c862" };
        public static string TierColour(int level, int count) => level <= 0 ? null : count <= 1 || level >= 3 ? TierColours[3] : TierColours[Math.Min(level, 3)];
        public const string CountedCharacter = "Your character", CountedPc = "Recorded on this PC", CountedFellows = "Fellow players who share", CountedServer = "The server, from its cargo book";
        public const string KnownForTitle = "Known for", FeatsHeading = "Feats", FeatsNone = "No feats earned yet.", FeatsAllEarned = "Every feat earned.";
        public const string NotCountedYet = "Hearthwoven does not count this yet.";
        /// <summary>Why an Unsung feat reads 0 (feats-real, Joost's own book showed "0 of 16" with no reason): the card's second small line. A feat counted
        /// on this PC counts from the day Hearthwoven was installed; one worked out from fellows needs fellows who share; the group's cargo needs the server's book.</summary>
        public const string NeedsFellows = "no fellow shares yet", NeedsServer = "needs the server on 0.6",
                            NeedsServerCaveat = "Needs the server on Hearthwoven 0.6: its cargo book counts this. A server before 0.6 sends nothing.";
        /// <summary>One line on the Feats page (in the switch's caption row) saying what a feat is and what Unsung means: plain, no ranking.</summary>
        public const string FeatsDefinition = "A feat is a moment worth telling, earned by doing it.", UnsungDefinition = "Not earned yet. Each one says what earns it.",
                            TogetherDefinition = "Earned by the whole group. Everyone who shares adds to them.";
        /// <summary>The strip's title part may say each title's reason when the line stays about this wide (px, an estimate on the safe side: 15 px names,
        /// 14 px italic reasons); wider, the names stand alone and still open the Titles page.</summary>
        public const float StripTitleRoom = 780;
        /// <summary>A feat whose data Hearthwoven does not keep yet: its card and detail carry this tone (neither earned nor earnable), its card the short line, its detail "Counted by: not yet".</summary>
        public const string FeatWaitingTone = "waiting", NotCountedShort = "not counted yet", CountedNotYet = "no one yet", FeatLabel = "Feat", TitleWord = "Title", TitlesWord = "Titles";   // the titles part of the strip at a page's top
        public static readonly string[] Numerals = { "", "I", "II", "III" };
        public const int KnownForTop = 3;
        /// <summary>The height of the feats' grid area (px): up to this much shows, more rows scroll inside it with the soft fade, so the one detail area under it is always
        /// whole and at the same place (fix3: Finch's 13 feats pushed the detail under the fold). Four whole rows of the tiles in FeatsUi (fix4: 4 x 52 + 3 x 6, so the 16 feats
        /// of four across show with no row cut); a narrower plate or more feats scroll, with the fade and <see cref="FeatsMore"/> at the foot.</summary>
        public const float FeatsGridRoom = 226;   // the least room the grid gets; on the Feats chapter it takes all the plate leaves above the detail area (FeatsUi)
        /// <summary>The cue at the foot of a feats grid that has rows below (fix4): that there is more, and how to reach it.</summary>
        public const string FeatsMore = "More below · wheel or A/D";   // the Titles page uses the same grid (B18)

        // a fellow player's cooking, gear and voyages are the only things a feat is worked out from besides your own counts
        static double Hours(double seconds) => seconds / 3600.0;


        /// <summary>A counter the hooks keep in the ledger (the shield hooks) or another branch keeps in the measured tallies under its
        /// JSON name ("cargoMeters", "bornInCare"): the ledger counter when there is one, else the sum of that tally (a fellow's copy
        /// carries it too), else null (not counted yet: the feat waits).</summary>
        static double? FeatKey(PanelInput i, string name)
        {
            if (i?.Feats != null && i.IsSelf && i.Feats.Counts.TryGetValue(name, out var c)) return c;
            if (i?.Feats != null && i.Feats.Bests.TryGetValue(name, out var best)) return best.Value;   // a fellow's shared best (cargoBestVoyage, ledBestMeters)
            // counted by this PC (or by a fellow's that shares the bests) but none set yet: 0, not "does not count this yet"
            if (i?.Feats != null && (name == CargoVoyage.BestKey || name == LedTracker.BestKey) && (i.IsSelf || i.Feats.BestsShared)) return 0;
            if (i?.Events != null)
                foreach (var kv in i.Events.Named()) if (kv.Key == name) return kv.Value.Values.Sum(v => (double)v);
            return null;
        }

        /// <summary>A hook counter of this PC (the ledger): yours only; a fellow's page shows what they shared, never their counters.</summary>
        static double? FeatCount(PanelInput i, string name) => i != null && i.IsSelf && i.Feats != null ? i.Feats.Count(name) : 0;

        static double DistinctFellows(PanelInput input, Func<SessionEvents, bool> by) => FellowsOf(input).Count(f => by(f.Events));

        /// <summary>The picture of a feat: Codex's own line pictures for the ones that have one (Waymate, the young, the lead rope), else its family's title emblem.</summary>
        public static string FeatIcon(FeatDef d)
        {
            if (d.Picture != null) return d.Picture;
            switch (d.Id)
            {
                case "waymate": return "vocab:feat-waymate";
                case "born": return "vocab:young-creature";
                case "drover": case "longlead": return "vocab:lead-rope";
                case "ironforge": return "vocab:cargo-mark";
                default: return "title:" + d.Family;
            }
        }

        public static readonly FeatDef[] FeatDefs = new FeatDef[]
        {
            // ----- Hearth Cook -----
            new FeatDef("fulltable", "Full Table", "cook", Chapter.Deeds, "cooking", "A table the whole group sat at.", new[] { 3.0 },
                t => "Food {you} made was enjoyed by " + N(t) + " different fellow players.",
                i => DistinctFellows(i, e => e.AteFoodMadeBy.Any(kv => kv.Value > 0 && SameName(Split2(kv.Key)[0], i.PlayerName))),
                CountedFellows, "Only fellow players who share are seen. A dish from a fellow's grill may carry their name.", exact: false, derived: true,
                brief: t => N(t) + " fellows enjoyed it", unit: "fellow players"),
            new FeatDef("feastgiver", "Feast-Giver", "cook", Chapter.Deeds, "cooking", "Feasts set out for everyone.", new[] { 20.0 },
                t => "Fellow players enjoyed " + N(t) + " servings from feasts {you} set out.",
                i => FellowsOf(i).Sum(f => f.Events.AteFromFeastOf.Where(kv => i.PlayerId != 0 && Split2(kv.Key)[0] == i.PlayerId.ToString(Inv)).Sum(kv => (double)kv.Value)),
                CountedFellows, "Only fellow players who share are seen.", exact: false, derived: true,
                brief: t => N(t) + " feast servings", unit: "servings"),
            // ----- Forgekeeper -----
            new FeatDef("arms", "Arms for the Hall", "smith", Chapter.Deeds, "crafting", "Gear that fellow players wear.", new[] { 3.0 },
                t => "Gear {you} made was put on by " + N(t) + " different fellow players.",
                i => DistinctFellows(i, e => e.EquippedGearMadeBy.Any(kv => kv.Value > 0 && SameName(Split2(kv.Key)[0], i.PlayerName))),
                CountedFellows, "Only fellow players who share are seen. It counts putting gear on, not wearing it.", exact: false, derived: true,
                brief: t => N(t) + " fellows wear it", unit: "fellow players"),
            new FeatDef("keptfires", "Kept the Fires", "smith", Chapter.Stores, "smelters", "The smelters never went cold.", new[] { 1000.0, 5000.0, 20000.0 },
                t => "Put " + N(t) + " ore and fuel into smelters, kilns and furnaces.",
                i => M(i, e => e.SmelterAdded),
                CountedPc, "A feeder chest that fills a smelter by itself does not count.", brief: t => N(t) + " ore and fuel", unit: "ore and fuel"),
            // ----- Storekeeper (the hauler's) -----
            new FeatDef("oreroad", "Ore Road", "hauler", Chapter.Voyages, "cargo", "Carrying the iron home.", new[] { 500.0, 5000.0, 25000.0 },
                t => "Carry " + N(t) + " item-km of cargo while {you} steer or pull a cart.",
                i => FeatKey(i, "cargoMeters") / 1000.0,
                CountedPc, "It counts who steers or pulls, not who loaded. The distance is a straight line between samples, so a little short.", needs: "cargoMeters",
                brief: t => N(t) + " item-km hauled", unit: "item-km", decimals: true),
            new FeatDef("heavykeel", "Heavy Keel", "hauler", Chapter.Stores, "overview", "A deep keel under a heavy load.", new[] { 100.0 },
                t => "Steer 2 km with " + N(t) + " metal or ore items aboard.",
                i => FeatKey(i, "cargoBestVoyage"),
                CountedPc, "It counts who steers, not who loaded.", needs: "cargoBestVoyage",
                brief: t => N(t) + " items on 2 km", unit: "items"),
            // ----- Helmskeeper -----
            new FeatDef("ferryman", "Ferryman", "sailor", Chapter.Voyages, "sailing", "Taking fellow players where they needed to go.", new[] { 1.0, 5.0, 20.0 },
                t => "Fellow players spent " + Plural(t, "hour", "hours") + " aboard while {you} held the helm.",
                i => Hours(FellowsOf(i).Sum(f => f.Events.SailedUnderHelmOf.Where(kv => SameName(kv.Key, i.PlayerName)).Sum(kv => (double)kv.Value))),
                CountedFellows, "Only fellow players who share are seen. It counts time, not distance.", exact: false, derived: true,
                brief: t => Plural(t, "hour", "hours") + " at {your} helm", unit: "hours", decimals: true),
            // ----- Shieldbearer -----
            new FeatDef("shieldwall", "Shield Wall", "defender", Chapter.Battle, "defense", "Standing in front of the others.", new[] { 250.0, 1000.0, 5000.0 },
                t => "Hold " + N(t) + " hits on {your} shield with a fellow player within " + FeatsCounters.NearMetres.ToString("0", Inv) + " m.",
                i => FeatCount(i, FeatsCounters.BlocksNear),
                CountedPc, "Held hits only. A fellow player without Hearthwoven counts as near.", brief: t => N(t) + " hits held near", unit: "hits"),
            new FeatDef("stoodfast", "Stood Fast", "defender", Chapter.Battle, "defense", "Taking the blows so others can fight.", new[] { 10000.0 },
                t => "Stop " + N(t) + " damage with {your} shield.",
                i => FeatCount(i, FeatsCounters.Stopped),
                CountedPc, "It counts damage before {your} armor.", brief: t => N(t) + " damage stopped", unit: "damage"),
            new FeatDef("unbroken", "Unbroken", "defender", Chapter.Battle, "defense", "Not one blow got through.", new[] { 10.0 },
                t => "Parry " + N(t) + " blows in a row before a hit lands on {them}.",
                i => FeatCount(i, FeatsCounters.BestStreak),
                CountedPc, "Burning and poison do not break the run.", brief: t => N(t) + " parries in a row", unit: "parries"),
            new FeatDef("turnedblades", "Turned Blades", "defender", Chapter.Battle, "defense", "Turning blows aside.", new[] { 100.0, 500.0, 2000.0 },
                t => "Parry " + N(t) + " blows.",
                i => i.Events == null ? 0 : i.Events.Parries,
                CountedPc, "A parry follows the game's rule. Tower shields never parry.", brief: t => N(t) + " parries", unit: "parries"),
            new FeatDef("forsaken", "Turned the Forsaken", "defender", Chapter.Battle, "defense", "Turning aside a Forsaken's blow.", new[] { 1.0 },
                t => "Parry a blow from a Forsaken.",
                i => FeatCount(i, FeatsCounters.BossParries),
                CountedPc, null, brief: t => "Parry a Forsaken", unit: "Forsaken parry|Forsaken parries"),
            // ----- Beastkeeper -----
            new FeatDef("drover", "Drover", "tamer", Chapter.Deeds, "taming", "Bringing the herd home.", new[] { 1.0, 5.0, 20.0 },
                t => "Lead tamed animals for " + N(t) + " km in all.",
                i => FeatKey(i, "ledMeters") / 1000.0,
                CountedPc, "It counts animals that follow {them}, matched by {your} name.", needs: "ledMeters",
                brief: t => N(t) + " km of animals led", unit: "km", decimals: true),
            new FeatDef("longlead", "Long Lead", "tamer", Chapter.Deeds, "taming", "One long walk with a creature behind {them}.", new[] { 2.0 },
                t => "Lead one animal " + N(t) + " km in one go.",
                i => FeatKey(i, "ledBestMeters") / 1000.0,
                CountedPc, "It counts an animal that follows {them}, matched by {your} name.", needs: "ledBestMeters",
                brief: t => N(t) + " km in one go", unit: "km", decimals: true),
            new FeatDef("born", "Born in Your Care", "tamer", Chapter.Deeds, "taming", "Young ones born under a steady hand.", new[] { 5.0, 25.0, 100.0 },
                t => N(t) + " tamed young born while {your} PC hosted their parents.",
                i => FeatKey(i, "bornInCare"),
                CountedPc, "Only births {your} PC hosted. The game does not record who fed them.", needs: "bornInCare",
                brief: t => N(t) + " young born", unit: "young born"),
            // ----- Waymate (no title yet: the helper's emblem stands in) -----
            new FeatDef("waymate", "Waymate", "helper", Chapter.Battle, "deaths", "Going back for a fellow player's things.", new[] { 3.0 },
                t => "Open a fellow player's grave " + N(t) + " times.",
                i => C(i, "TombstonesOpenedOther"),
                CountedCharacter, "Opening a grave does not prove the things went back.", retro: true,
                brief: t => N(t) + " graves opened", unit: "grave|graves"),
            // ----- the group's (Together) -----
            new FeatDef("ironforge", "Iron for the Forge", "hauler", Chapter.Feats, FeatsTogetherId, "Bringing the ore home, all of us.", new[] { 10000.0 },
                t => "Together, move " + N(t) + " item-km of ore and metal by ship or cart.",
                GroupOre, CountedServer, "Counted when ore or metal comes off a ship or cart. Only fellow players who share are seen.", exact: false, derived: true, needs: "cargoDelivered",
                brief: t => N(t) + " item-km moved", unit: "item-km", decimals: true, group: true)
                {
                    Biome = "Swamp",   // 0.7 decision 4: it names iron, so it waits for the Swamp like Bog Iron (name kept)
                    // and, like Bog Iron, its name stays a riddle until someone who shares held iron (review fix: the Swamp found is not iron found);
                    // its number counts copper and tin ore too, so moving ore does not lift it
                    Veil = new FeatVeil
                    {
                        Name = "Ore for the Forge", Honours = "Bringing the ore home, all of us.", Lifts = new[] { "$item_ironscrap", "$item_iron" }, ValueLifts = false,
                        Rule = t => "Together, move " + N(t) + " item-km of ore and metal by ship or cart.", Brief = t => N(t) + " item-km moved", Unit = "item-km",
                    },
                },
        }.Concat(GroupFeatDefs()).ToArray();   // the group's eight (FeatsGroup.cs)

        /// <summary>The group's ore and metal moved by ship or cart (item-km): what the server's book booked as unloaded, summed over you and every fellow player who shares
        /// (each haul once: from where it was loaded to where it came off). null when no one's book is there (a server before 0.6): the feat waits.</summary>
        static double? GroupOre(PanelInput i)
        {
            var books = GroupBooks(i).Select(x => x.book).ToList();
            if (books.Count == 0) return null;
            return books.Sum(b => b.Delivered.Where(kv => CargoVoyage.MetalOre.Contains(kv.Key)).Sum(kv => kv.Value)) / 1000.0;
        }
        static List<(string name, ServerBook.Shared book)> GroupBooks(PanelInput i) =>
            GroupMembers(i).Where(p => p.Book != null).Select(p => (p.PlayerName ?? "", p.Book)).ToList();   // each person once, by who they are (MemberKey), not by name
        /// <summary>Who loaded or unloaded ore or metal for the group, by name (no numbers each: no comparison, and who gets the credit is Joost's call).</summary>
        public static List<string> GroupCrew(PanelInput i) =>
            GroupBooks(i).Where(x => x.book.Sent.Concat(x.book.Delivered).Any(kv => kv.Value > 0 && CargoVoyage.MetalOre.Contains(kv.Key)))
                .Select(x => x.name).Where(n => n.Length > 0).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        public static FeatDef FeatById(string id) => FeatDefs.FirstOrDefault(d => d.Id == id);
        /// <summary>True for a group feat's id (Together's): its gold dot sits on Together, never on Earned (FeatsLedger.UnseenOf).</summary>
        public static bool IsGroupFeat(string id) => FeatById(id)?.Group == true;

        /// <summary>The panel's feats seen once their page is on screen in your own book: Earned marks your own tiers seen, Together the group's
        /// (each page answers its own dot; opening Earned leaves a group tier's dot on Together).</summary>
        public static void FeatsSeen(PanelInput self, PanelState state)
        {
            if (self?.Feats == null || !self.IsSelf || state == null || state.ShowAbout || state.Chapter != Chapter.Feats) return;
            var page = state.PageOf(Chapter.Feats) ?? FeatsPageId;
            if (page == FeatsPageId) self.Feats.MarkSeenOf(IsGroupFeat, false);
            else if (page == FeatsTogetherId) self.Feats.MarkSeenOf(IsGroupFeat, true);
        }

        // ---------- the numbers ----------

        /// <summary>
        /// L6 (review): one render asks a feat's number and veil many times (the card, its detail, the hint, the title, the picture, the rule, the unit,
        /// the caveat) and a group feat's number walks every member's copy. Build opens this memo for its own run (by reference: the inputs of one
        /// render), so each is worked out once per feat and the group's lands (GroupFound) once per page. Outside Build nothing is kept.
        /// </summary>
        sealed class FeatMemo
        {
            public readonly Dictionary<(PanelInput, string), (double? value, bool threw)> Raw = new Dictionary<(PanelInput, string), (double?, bool)>();
            public readonly Dictionary<(PanelInput, string), bool> Veiled = new Dictionary<(PanelInput, string), bool>();
            public readonly Dictionary<PanelInput, HashSet<string>> Found = new Dictionary<PanelInput, HashSet<string>>();
        }
        [ThreadStatic] static FeatMemo featMemo;

        /// <summary>Runs one render with the feat memo open (nested calls share the outer one).</summary>
        static T WithFeatMemo<T>(Func<T> render)
        {
            if (featMemo != null) return render();
            featMemo = new FeatMemo();
            try { return render(); } finally { featMemo = null; }
        }

        static (double? value, bool threw) FeatRaw(FeatDef d, PanelInput i)
        {
            var m = featMemo;
            if (m != null && i != null && m.Raw.TryGetValue((i, d.Id), out var hit)) return hit;
            (double?, bool) r;
            try { r = (d.Value(i), false); } catch { r = (null, true); }
            if (m != null && i != null) m.Raw[(i, d.Id)] = r;
            return r;
        }

        /// <summary>The player's number for a feat now (0 when it is not counted yet).</summary>
        public static double FeatValue(FeatDef d, PanelInput i)
        {
            var r = FeatRaw(d, i);
            return r.threw ? 0 : Math.Max(0, r.value ?? 0);
        }

        /// <summary>True when the feat's data key is not there (a counter another part of Hearthwoven will keep): nothing to show but the rule.</summary>
        public static bool FeatWaiting(FeatDef d, PanelInput i)
        {
            if (d.Needs == null) return false;
            var r = FeatRaw(d, i);
            return r.threw || r.value == null;
        }

        /// <summary>How many of a feat's tiers a number reaches.</summary>
        public static int FeatTierOf(FeatDef d, double value) => d.Tiers.Count(t => value >= t);

        /// <summary>The tiers a player has: the record of earned tiers (the ledger: moments seen on their PC, or shared by a fellow) or
        /// what their numbers say now, whichever is higher.</summary>
        public static int FeatTier(FeatDef d, PanelInput i) => Math.Max(i?.Feats?.Tier(d.Id) ?? 0, FeatTierOf(d, FeatValue(d, i)));

        public static string Numeral(int tier) => tier >= 1 && tier < Numerals.Length ? Numerals[tier] : "";

        // ---------- noticing a feat (Evaluate, called on the player's PC) ----------

        /// <summary>
        /// The player's own feats against their numbers now: every tier reached and not yet in the ledger is recorded with its moment
        /// (the date and the biome under their feet; a derived feat is "noticed"; a character's old record already over the line at
        /// the very first look is "before install"). Returns what was earned just now. Nothing for a fellow's copy or without a ledger.
        /// </summary>
        public static List<(FeatDef feat, int tier)> EvaluateFeats(PanelInput self, DateTime nowUtc, string biome, string place)
        {
            var fresh = new List<(FeatDef, int)>();
            var ledger = self?.Feats;
            if (self == null || ledger == null || !self.IsSelf) return fresh;
            var first = !ledger.Primed;
            HashSet<string> found = null;
            foreach (var d in FeatDefs)
            {
                // a group feat of a land the group has not found is not noticed yet (no dot for a feat the page does not show)
                if (d.Group && d.Biome != null && !GroupVisible(d, self, found = found ?? GroupFound(self))) continue;
                var tier = FeatTierOf(d, FeatValue(d, self));
                while (ledger.Tier(d.Id) < tier)
                {
                    // derived from fellows' copies, or Hearthwoven's own count met for the first time at the very first look (it kept no
                    // moments before this): the latest it can have happened is now, "by 9 Oct". A character's own old record already over
                    // the line at the first look: before Hearthwoven counted. Otherwise Hearthwoven saw it cross: the date and the biome.
                    // a Before moment keeps the day of that first look too: when the feats came later than the install (0.6 on a PC that ran 0.5),
                    // the record may have crossed the line in between, so the words say "by that day" instead of "before install" (FeatMomentWords)
                    var m = d.Derived || (first && !d.Retro) ? new FeatMoment { Utc = nowUtc, Noticed = true }
                          : first ? new FeatMoment { Before = true, Utc = nowUtc }
                          : new FeatMoment { Utc = nowUtc, Biome = string.IsNullOrEmpty(biome) || biome == "None" ? null : biome, Place = place };
                    var next = ledger.Tier(d.Id) + 1;
                    if (!ledger.Earn(d.Id, next, m)) break;
                    fresh.Add((d, next));
                }
            }
            ledger.Primed = true;
            return fresh;
        }

        // ---------- words ----------

        static string MomentDay(PanelInput i, FeatMoment m, bool year) => Local(i, m.Utc).ToString(year ? "d MMM yyyy" : "d MMM", Inv);
        // the year only when it is not this year's (short, Joost 2026-10-09: "Earned 7 Oct")
        static bool OtherYear(PanelInput i, FeatMoment m) => i.NowUtc > DateTime.MinValue && Local(i, m.Utc).Year != Local(i, i.NowUtc).Year;
        /// <summary>The quiet tail of a moment first seen on a day (worked out from fellows' copies, or met at Hearthwoven's first look): it may have happened before.</summary>
        public const string OrEarlier = "(or earlier)";

        /// <summary>A character's old record first looked at after the install day (the feats came with a later version): it crossed the line before that look,
        /// not necessarily before the install, so it is said as a day first seen ("9 Oct or earlier"), never "before install".</summary>
        static bool BeforeIsLate(PanelInput i, FeatMoment m) =>
            m.Before && m.Utc > DateTime.MinValue && i.InstalledUtc.HasValue && Local(i, m.Utc).Date > Local(i, i.InstalledUtc.Value).Date;

        /// <summary>The one small line on a card: "12 Oct · Swamp", "before install", "9 Oct or earlier" ("" when nothing is known).</summary>
        public static string FeatMomentLine(PanelInput i, FeatMoment m, DateTime? since = null)
        {
            if (m.Before && !BeforeIsLate(i, m)) return BeforeWhen(i, since ?? StartOf(i, null));
            if (m.Utc <= DateTime.MinValue) return "";
            if (m.Noticed || m.Before) return MomentDay(i, m, false) + " or earlier";
            var biome = string.IsNullOrEmpty(m.Biome) || m.Biome == "None" ? null : BiomeName(m.Biome);
            return MomentDay(i, m, false) + (biome != null ? " · " + biome : "");
        }

        /// <summary>The detail's moment, short and honest (Joost 2026-10-09): "Earned 12 Oct in the Swamp"; a day first seen: "Earned 7 Oct" with the quiet note
        /// "(or earlier)"; a character's old record: "Earned before 8 Oct" with "(before install)". The year only when it is not this one.</summary>
        public static (string text, string note) FeatMomentWords(PanelInput i, FeatMoment m, DateTime? since = null)
        {
            if (m.Before && !BeforeIsLate(i, m))
            {
                var from = since ?? StartOf(i, null);
                return ("Earned " + BeforeWhen(i, from), from.HasValue && i.IsSelf ? "(Hearthwoven counts from " + RecordDate(i, from.Value) + ")" : null);
            }
            if (m.Utc <= DateTime.MinValue) return ("Earned, date not known", null);
            if (m.Noticed || m.Before) return ("Earned " + MomentDay(i, m, OtherYear(i, m)), OrEarlier);
            var biome = string.IsNullOrEmpty(m.Biome) || m.Biome == "None" ? null : BiomeName(m.Biome);
            return ("Earned " + MomentDay(i, m, OtherYear(i, m)) + (biome != null ? " in the " + biome : "") + (string.IsNullOrEmpty(m.Place) ? "" : ", " + m.Place), null);
        }
        /// <summary>"before 8 October": a character's old record, before the day its count began; without a date: "before the count began".</summary>
        static string BeforeWhen(PanelInput i, DateTime? from) => from.HasValue ? "before " + RecordDate(i, from.Value) : "before the count began";

        /// <summary>The moment as one line (its text and its quiet note), for tests and plain text.</summary>
        public static string FeatMomentSentence(PanelInput i, FeatMoment m) { var w = FeatMomentWords(i, m); return w.note == null ? w.text : w.text + " " + w.note; }

        // ---------- the page ----------

        // the source tag of a feat's numbers (ISC-A-2: every number carries one): where the feat is counted
        static string FeatTag(FeatDef d) => d.Source == CountedCharacter ? TagCharacter : d.Source == CountedFellows || d.Source == CountedGroup ? TagFellows : d.Source == CountedServer ? TagServer : TagMeasured;

        /// <summary>A rule or a caveat said for whose book it is: "you" and "your" on your own, "they", "their" and "them" on a fellow player's.</summary>
        public static string FeatWords(PanelInput i, string text) =>
            text == null ? null : (i == null || i.IsSelf ? text.Replace("{you}", "you").Replace("{your}", "your").Replace("{them}", "you") : text.Replace("{you}", "they").Replace("{your}", "their").Replace("{them}", "them"));

        /// <summary>How a feat is counted: "Your character" / "Recorded from 1 October · this PC" on your own page, "Tor's character" /
        /// "Recorded on Tor's PC" on a fellow's.</summary>
        public static string FeatCounted(PanelInput i, FeatDef d) =>
            d.Source == CountedGroup ? FeatWords(i, CountedGroupLine) :
            i != null && d.Source == CountedPc ? FeatsPcLine(i, CountFrom(i, d.Id)) :
            i == null || i.IsSelf || d.Source == CountedFellows || d.Source == CountedServer || d.Source == CountedGroup ? d.Source : d.Source == CountedCharacter ? Name(i) + "'s character" : FeatsPcLine(i, null);

        // a fellow's feats ledger travels whole in every copy (not only their last session), so a feat counted on their PC says whose PC,
        // also on an older sender's copy (RecordedFromLine says "as Tor last shared it" there, which is right for the session's numbers)
        static string FeatsPcLine(PanelInput i, DateTime? from) => i != null && !i.IsSelf ? FellowWords(Name(i), true) : RecordedFromLine(i, from);
        static string FeatsPcShort(PanelInput i, DateTime? from) => i != null && !i.IsSelf ? "on " + Name(i) + "'s PC" : FromShort(i, from);

        /// <summary>The 0.6 group's feats (Feats chapter row 132): their counts on this PC began with the feats group's own start (LocalTotals.StartFeats),
        /// not with the install (0.7 hard case 15). Every other feat counted on this PC began with the install.</summary>
        static readonly HashSet<string> SinceV06 = new HashSet<string> { "shieldwall", "stoodfast", "unbroken", "forsaken", "oreroad", "heavykeel", "drover", "longlead", "born" };

        /// <summary>When a feat's count on this PC began (0.7 hard case 15): the 0.6 feats from the feats group's start, the others from the install; null for a fellow's book.</summary>
        public static DateTime? CountFrom(PanelInput i, string featId) => StartOf(i, featId != null && SinceV06.Contains(featId) ? LocalTotals.StartFeats : null);

        /// <summary>The block kinds of the Feats system (they carry no number of their own beyond what their items tag).</summary>
        public static bool IsFeatKind(string kind) => kind == "feats" || kind == "featdetail" || kind == "knownfor" || kind == "featband";

        /// <summary>"8 750 of 10 000 damage", "1.6 of 5 km": every progress line names its unit; a distance or a duration shows one decimal, cut not rounded (never ahead of the truth).</summary>
        public static string FeatProgress(FeatDef d, double value, double next, PanelInput i = null)
        {
            var have = FeatHave(d, value);
            var unit = (i == null ? d.Unit : FeatUnit(d, i)) ?? "";
            var bar = unit.IndexOf('|');
            if (bar >= 0) unit = Math.Round(next) == 1 ? unit.Substring(0, bar) : unit.Substring(bar + 1);
            return have + " of " + N(next) + (unit.Length > 0 ? " " + unit : "");
        }

        /// <summary>The number reached, as a progress says it: one decimal for a distance or a duration, cut not rounded (never ahead of the truth).</summary>
        static string FeatHave(FeatDef d, double value)
        {
            var now = d.Decimals ? Math.Floor(value * 10 + 1e-9) / 10 : Math.Floor(value);
            return d.Decimals && Math.Abs(now - Math.Round(now)) > 1e-9 ? now.ToString("0.0", Inv) : N(Math.Round(now));
        }

        /// <summary>
        /// Why a feat not earned reads what it reads (the card's second small line, feats-real): counted on this PC, it counts from the install day ("counting
        /// since 8 Oct", or "340 so far · since 8 Oct"); worked out from fellows, it needs fellows who share; the group's cargo needs the server on 0.6; the
        /// character's own record says how far it is. Never a number that is not there; a fellow's book shows no counting of theirs.
        /// </summary>
        public static string FeatHint(PanelInput i, FeatDef d, bool waiting)
        {
            if (i == null) return null;
            if (d.Source == CountedServer) return waiting ? NeedsServer : null;
            if (waiting) return null;
            var value = FeatValue(d, i);
            if (d.Source == CountedGroup) return value > 0 ? FeatHave(d, value) + " so far" : "none yet";   // the group's number, the same on every book
            if (d.Source == CountedFellows)
                return !i.IsSelf ? null : !FellowsOf(i).Any() ? NeedsFellows : value > 0 ? FeatHave(d, value) + " so far" : "none yet from fellows";
            var have = i.IsSelf && d.Exact && value > 0 ? FeatHave(d, value) + " so far" : null;
            if (d.Source == CountedPc)
            {
                if (!i.IsSelf) return null;
                // hard case 15: the install, or the 0.6 feats' start; the short month, the card's second line is narrow ("counting from 8 Oct")
                var from = CountFrom(i, d.Id);
                if (!from.HasValue) return have ?? "counted on this PC";
                var since = Local(i, from.Value).ToString("d MMM", Inv);
                return have != null ? have + " · from " + since : "counting from " + since;
            }
            return have ?? (i.IsSelf ? "0 so far" : null);
        }

        /// <summary>
        /// The tier in words, revealed one step at a time (Joost 2026-10-09: no row of three marks before the first is earned): an Unsung feat with tiers
        /// "Tier I at 1 000"; an earned one "Tier I · next: II at 5 000"; all tiers "Tier III · every tier earned". A one-off feat: "Earned" or nothing.
        /// </summary>
        public static string FeatTierLine(FeatDef d, int tier)
        {
            if (d.Tiers.Length <= 1) return tier > 0 ? Earned : "";
            if (tier <= 0) return "Tier I at " + N(d.Tiers[0]);
            if (tier >= d.Tiers.Length) return "Tier " + Numeral(tier) + " · every tier earned";
            return "Tier " + Numeral(tier) + " · next: " + Numeral(tier + 1) + " at " + N(d.Tiers[tier]);
        }

        /// <summary>An earned card's second small line when a tier is still ahead, with what the number counts (B30, Joost: "next: II at 250" said
        /// 250 of what?): "next: II at 250 bronze made", in the feat's own few words (its brief; a veiled feat's names no material). Only the next tier.</summary>
        public static string FeatNextLine(FeatDef d, int tier, PanelInput input = null)
        {
            if (d.Tiers.Length <= 1 || tier <= 0 || tier >= d.Tiers.Length) return null;
            var brief = input != null ? FeatBriefOf(d, input) : d.Brief;
            return "next: " + Numeral(tier + 1) + " at " + (brief != null ? FeatWords(input, brief(d.Tiers[tier])) : N(d.Tiers[tier]));
        }

        /// <summary>The same line shorter, for a card too narrow for the feat's own words (FeatsUi.FeatCard measures; one line, never wrapped): the number
        /// and its unit only, "next: III at 25 000 item-km".</summary>
        public static string FeatNextShort(FeatDef d, int tier, PanelInput input = null)
        {
            if (FeatNextLine(d, tier, input) == null) return null;
            var unit = (input == null ? d.Unit : FeatUnit(d, input)) ?? "";
            var bar = unit.IndexOf('|');
            if (bar >= 0) unit = Math.Round(d.Tiers[tier]) == 1 ? unit.Substring(0, bar) : unit.Substring(bar + 1);
            return "next: " + Numeral(tier + 1) + " at " + N(d.Tiers[tier]) + (unit.Length > 0 ? " " + unit : "");
        }

        /// <summary>The tile's own line of an unsung feat: the next tier's rule in a few words ("10 000 damage stopped").</summary>
        static string FeatBrief(PanelInput input, FeatDef d, int tier) =>
            FeatBriefOf(d, input) is Func<double, string> brief && tier < d.Tiers.Length ? FeatWords(input, brief(d.Tiers[tier])) : null;

        static Block FeatDetail(PanelInput input, FeatDef d, int tier)
        {
            var many = d.Tiers.Length > 1;
            var earned = tier > 0;
            var waiting = FeatWaiting(d, input);
            var value = FeatValue(d, input);
            var detail = new Block
            {
                Kind = "featdetail", Id = d.Id, Icon = FeatPicture(d, input), Title = FeatTitle(d, input), Text = FeatWords(input, FeatHonours(d, input)), Tone = earned ? FeatTone : waiting ? FeatWaitingTone : FeatUnsung, Colour = TierColour(tier, d.Tiers.Length), Source = FeatTag(d),
                Value = FeatTierLine(d, tier),
                Level = tier, Count = d.Tiers.Length, Items = new List<Block>(),
            };
            // every tier (B30, Joost wanted to see that tier III exists; the card's marks show them all): the reached ones lit in their colours, the later
            // ones greyed. A veiled feat keeps its riddle: the tiers reached and only the next one (Joost 2026-10-09)
            var rule = FeatRule(d, input);   // a veiled feat's rule names no material
            var shown = FeatVeiled(d, input) ? Math.Min(d.Tiers.Length, tier + 1) : d.Tiers.Length;
            for (int k = 0; k < shown; k++)
                detail.Items.Add(new Block { Kind = "rule", Title = FeatWords(input, rule(d.Tiers[k])), Value = many ? Numeral(k + 1) : "", Selected = k < tier, Source = FeatTag(d), Colour = k < tier ? TierColour(k + 1, d.Tiers.Length) : null });
            // your progress: your own page, exact numbers only, and only while a tier is still ahead; a group feat's number is the group's, the same on every book
            if ((input.IsSelf && d.Exact || d.Group) && !waiting && tier < d.Tiers.Length)
            {
                var next = d.Tiers[tier];
                detail.Items.Add(new Block { Kind = "progress", Title = FeatProgress(d, value, next, input), Fraction = (float)Math.Max(0, Math.Min(1, value / next)), Source = FeatTag(d) });
            }
            detail.Items.Add(new Block { Kind = "counted", Text = waiting && !earned ? CountedNotYet : FeatCounted(input, d) });
            if (waiting) detail.Items.Add(new Block { Kind = "caveat", Text = d.Source == CountedServer ? NeedsServerCaveat : NotCountedYet });
            else if (!string.IsNullOrEmpty(FeatCaveat(d, input))) detail.Items.Add(new Block { Kind = "caveat", Text = FeatWords(input, FeatCaveat(d, input)) });   // a veiled feat's caveat names no material
            if (d.Group && !waiting && d.Part != null)   // B37: who helped, each with their part and share, largest first, in their own colour
            {
                var shares = GroupShares(input, d.Part);
                var pct = AwayPercents(shares.Select(x => x.share).ToList());   // whole per cents that add up to 100, as the group view's
                if (shares.Count > 0) detail.Items.Add(new Block { Kind = "crew", Title = d.Helped, Tone = d.Overlaps ? null : "share",
                    Items = shares.Select((x, k) => new Block { Icon = "person:" + x.name, Title = x.name, Id = x.name, Value = FeatHave(d, x.part), Note = d.Overlaps ? null : PercentText(pct[k], x.share), Fraction = (float)x.share }).ToList() });
            }
            else if (d.Group && !waiting)   // who carried (Iron for the Forge): their shields, by name (the server's book keeps no part each)
            {
                var crew = GroupCrew(input);
                if (crew.Count > 0) detail.Items.Add(new Block { Kind = "crew", Title = "Carried by", Items = crew.Select(n => new Block { Icon = "person:" + n, Title = n }).ToList() });
            }
            var moment = input.Feats?.Moment(d.Id);
            if (earned)
            {
                var (text, note) = moment.HasValue ? FeatMomentWords(input, moment.Value, CountFrom(input, d.Id)) : ("Earned, date not known", null);
                // a feat with tiers says which tier the moment is of: "Tier II earned 9 Oct in the Swamp"
                var of = moment.HasValue && (input.Feats?.Tier(d.Id) ?? 0) > 0 ? input.Feats.Tier(d.Id) : tier;   // the moment is the ledger's last tier's
                if (many && text.StartsWith("Earned", StringComparison.Ordinal)) text = "Tier " + Numeral(of) + " earned" + text.Substring("Earned".Length);
                detail.Items.Add(new Block { Kind = "moment", Text = text, Note = note });
            }
            return detail;
        }

        static Block FeatCard(PanelInput input, FeatDef d, string selected)
        {
            var tier = FeatTier(d, input);
            var moment = tier > 0 ? input.Feats?.Moment(d.Id) : null;
            var waiting = tier == 0 && FeatWaiting(d, input);   // its data is not kept yet: the card says so and looks it (never earnable-looking)
            return new Block
            {
                Kind = "feat", Id = d.Id, Icon = FeatPicture(d, input), Title = FeatTitle(d, input), Level = tier, Count = d.Tiers.Length, Colour = TierColour(tier, d.Tiers.Length), Source = FeatTag(d),
                Tone = tier > 0 ? FeatTone : waiting ? FeatWaitingTone : FeatUnsung,
                Text = moment.HasValue ? FeatMomentLine(input, moment.Value, CountFrom(input, d.Id)) : waiting ? (d.Source == CountedServer ? NeedsServer : NotCountedShort) : tier == 0 ? FeatBrief(input, d, 0) : null,
                // the second small line: why an Unsung feat reads what it reads, or on an earned one the next tier only
                Note = tier > 0 ? FeatNextLine(d, tier, input) : waiting ? null : FeatHint(input, d, waiting),
                Value = tier > 0 ? FeatNextShort(d, tier, input) : null,   // the next line shorter, where the card is too narrow for it
                Selected = d.Id == selected, Items = new List<Block> { FeatDetail(input, d, tier) },
            };
        }

        /// <summary>The cards of one page, each feat once, in the table's order (the title families' order, never by size or rarity): your earned feats, your unsung
        /// ones, or the group's (earned or not).</summary>
        static List<Block> FeatCards(PanelInput input, string page, string selected)
        {
            var defs = page == FeatsTogetherId ? TogetherFeats(input, out _)
                     : FeatDefs.Where(d => !d.Group && (FeatTier(d, input) > 0) == (page == FeatsPageId)).ToList();
            var pick = defs.Any(d => d.Id == selected) ? selected : defs.FirstOrDefault()?.Id;   // the first card is selected on open: the detail is never empty
            return defs.Select(d => FeatCard(input, d, pick)).ToList();
        }

        /// <summary>Your own feats (not the group's): how many there are to earn.</summary>
        public static int OwnFeatCount => FeatDefs.Count(d => !d.Group);

        /// <summary>
        /// The Feats chapter (Joost 2026-10-09: its own chapter, not a page of Deeds): Earned, Unsung and Together in the left list, each a grid of cards over the one
        /// fixed detail area. The plate takes the whole room (Tone "full", as long pages like Cooking do), the grid the height the detail area leaves.
        /// </summary>
        static void FeatsChapter(PanelInput input, string page, PanelView view, PanelState state)
        {
            if (page == TitlesPageId) { TitlesPage(input, view, state); return; }
            var together = page == FeatsTogetherId; var earnedPage = page == FeatsPageId;
            view.Heading = together ? FeatsTogetherHeading : earnedPage ? "Earned feats" : "Unsung feats";
            var cards = FeatCards(input, page, state.FeatSel);
            if (cards.Count == 0) view.Blocks.Add(new Block { Kind = "note", Text = earnedPage ? FeatsNone : FeatsAllEarned });
            else
            {
                view.Blocks.Add(new Block { Kind = "feats", Items = cards, Tone = together ? FeatsTogetherWord.ToLowerInvariant() : earnedPage ? FeatTone : FeatUnsung });
                view.Blocks.Add(cards.First(c => c.Selected).Items[0]);
            }
            var have = FeatDefs.Count(d => !d.Group && FeatTier(d, input) > 0);
            var hidden = false; if (together) TogetherFeats(input, out hidden);
            var line = together ? TogetherDefinition + (hidden ? " " + LandsNotFound : "") : (earnedPage ? FeatsDefinition : UnsungDefinition) + " " + N(have) + " of " + N(OwnFeatCount) + " earned.";
            Plate(view, together ? "vocab:list-together" : earnedPage ? FeatsIcon : FeatsUnsungIcon, RecordedScope(input) ?? line);   // a fellow's book: "Tor, as of 8 Oct" (rule E), never "since install"
            PlateOf(view).Tone = PlateFull;
        }
        /// <summary>A plate that keeps the whole room even when its blocks are shorter (the Feats chapter: the grid grows into it, the detail area sits at the foot).</summary>
        public const string PlateFull = "full";

        /// <summary>true when the page is one of the Feats chapter's.</summary>
        public static bool OnFeatsPage(PanelView v) => v != null && !v.ShowAbout && v.Active == Chapter.Feats;

        /// <summary>The cards of the view on screen (the Feats page's grid), in order.</summary>
        public static List<Block> VisibleFeats(PanelView v) => Content(v).FirstOrDefault(b => b.Kind == "feats")?.Items ?? new List<Block>();

        /// <summary>A/D and the D-pad on the Feats page: the selection moves to the next card (wraps from the last to the first).
        /// false when the page has no cards.</summary>
        public static bool StepFeat(PanelState s, PanelView v, int d)
        {
            var cards = VisibleFeats(v);
            if (cards.Count == 0) return false;
            var at = Math.Max(0, cards.FindIndex(c => c.Selected));
            s.FeatSel = cards[((at + d) % cards.Count + cards.Count) % cards.Count].Id;
            return true;
        }

        // ---------- Known for (Deeds > Overview) and the band on an owner page ----------

        static List<(FeatDef d, int tier, FeatMoment? m)> EarnedFeats(PanelInput input) =>
            FeatDefs.Where(d => !d.Group).Select(d => (d, tier: FeatTier(d, input), m: input.Feats?.Moment(d.Id))).Where(x => x.tier > 0).ToList();

        /// <summary>Up to three feats someone is known for: the highest tier first, then the most recent (no comparison with anyone else).
        /// null when nothing is earned.</summary>
        public static Block FeatsKnownFor(PanelInput input)
        {
            var top = EarnedFeats(input).OrderByDescending(x => x.tier).ThenByDescending(x => x.m.HasValue && x.m.Value.Utc > DateTime.MinValue ? x.m.Value.Utc : DateTime.MinValue)
                                        .Take(KnownForTop).ToList();
            if (top.Count == 0) return null;
            return new Block
            {
                Kind = "knownfor", Id = FeatsLink, Title = KnownForTitle,
                Items = top.Select(x => new Block { Id = x.d.Id, Icon = FeatIcon(x.d), Title = x.d.Name, Value = x.d.Tiers.Length > 1 ? Numeral(x.tier) : "", Level = x.tier, Count = x.d.Tiers.Length, Colour = TierColour(x.tier, x.d.Tiers.Length) }).ToList(),
            };
        }

        /// <summary>The band under the heading of a feat's owner page: the feats earned that belong to this page, tier and moment.</summary>
        public static Block FeatBand(PanelInput input, Chapter chapter, string page)
        {
            var mine = EarnedFeats(input).Where(x => x.d.Chapter == chapter && x.d.Page == page).ToList();
            if (mine.Count == 0) return null;
            return new Block
            {
                Kind = "featband", Id = FeatsLink, Title = FeatsHeading,
                Items = mine.Select(x => new Block
                {
                    Id = x.d.Id, Icon = FeatIcon(x.d), Title = x.d.Name, Value = x.d.Tiers.Length > 1 ? Numeral(x.tier) : "", Level = x.tier, Count = x.d.Tiers.Length, Colour = TierColour(x.tier, x.d.Tiers.Length),
                    Text = x.m.HasValue ? FeatMomentLine(input, x.m.Value, CountFrom(input, x.d.Id)) : "", Note = FeatLabel,   // the small "Feat" tag the chip carries on an owner page
                }).ToList(),
            };
        }

        // Build's last steps: the band at the top of the page's plate (outside the zones), the gold dots, the keys of the Feats page
        static void FeatsFinish(PanelInput input, PanelState state, PanelView view)
        {
            if (view.ShowAbout) return;
            if (view.Active != Chapter.Feats && !view.EveryoneOn)   // 0.8: the group's page carries no one player's feats or titles (EveryoneModel.cs)
            {
                var band = FeatBand(input, view.Active, view.Page);
                var plate = PlateOf(view);
                // fix4: the page's feat and its titles are ONE compact strip at the top of the plate. B18: every page's titles ride in it (no title pill in the
                // heading row any more, where window chips hid it), each with its reason, each opening the Titles page. A title is all-time: in a time window
                // (10 min .. 30 days, Session) the strip leaves the titles out, so nothing suggests one was earned in that window; All shows them.
                var windowed = view.HasFilters && view.ShownWindow.HasValue && view.ShownWindow != TimeWindow.SinceInstall;
                var titles = plate == null || windowed ? new List<Block>() : StripTitles(input, view);
                if (titles.Count > 0)
                {
                    band = band ?? new Block { Kind = "featband", Id = FeatsLink, Title = FeatsHeading, Items = new List<Block>() };
                    band.Text = titles.Count == 1 ? TitleWord : TitlesWord;
                    band.Items.AddRange(titles);
                }
                if (band != null) { if (plate != null) plate.Items.Insert(0, band); else view.Blocks.Insert(0, band); }
            }
            // the page that answers the dot carries none (fix4: the Feats page you were reading kept its own "new" dot); opening Earned marks your own
            // tiers seen, opening Together the group's (FeatsSeen): a group tier's dot sits on the tab and on Together, never on Earned
            if (input.IsSelf && input.Feats != null)
            {
                var onPage = OnFeatsPage(view) ? view.Page : null;
                var own = onPage != FeatsPageId && input.Feats.UnseenOf(IsGroupFeat, false) > 0;
                var group = onPage != FeatsTogetherId && input.Feats.UnseenOf(IsGroupFeat, true) > 0;
                var tab = view.Chapters.FirstOrDefault(c => c.Id == Chapter.Feats.ToString());
                if (tab != null && view.Active != Chapter.Feats && (own || group)) tab.Dot = true;
                if (view.Active == Chapter.Feats)
                    foreach (var entry in view.List)
                        if ((own && entry.Id == FeatsPageId) || (group && entry.Id == FeatsTogetherId)) entry.Dot = true;
            }
            if (OnFeatsPage(view))
            {
                // A/D choose the feat here; Q/E still turn the chapters
                var at = view.Keys.IndexOf("[Q/E·A/D] Chapter");
                if (at >= 0) view.Keys[at] = "[Q/E] Chapter";
                var page = view.Keys.IndexOf("[W/S] Page");
                view.Keys.Insert(page >= 0 ? page + 1 : view.Keys.Count, view.Page == TitlesPageId ? "[A/D] Title" : "[A/D] Feat");
                FeatsLabelPc(input, view.Blocks);
            }
        }

        // ---------- titles: the Titles page and the strip on a title's own page (B18) ----------

        /// <summary>The feat items of a strip (its titles left out).</summary>
        public static List<Block> BandFeats(Block band) => (band?.Items ?? new List<Block>()).Where(i => i.Kind != TitleKind).ToList();
        /// <summary>The title items of a strip: Title = the name, Text = its reason ("3 defences built, armed or loaded") or null when the line has no room,
        /// Id = the Titles page with that title chosen.</summary>
        public static List<Block> BandTitles(Block band) => (band?.Items ?? new List<Block>()).Where(i => i.Kind == TitleKind).ToList();

        // a title's reasons, your character's count first (as on its page and its card)
        static List<KeyValuePair<string, string>> Reasons(TitleRow t) => t.Lines.Select((l, k) => (l, k)).OrderBy(x => x.l.Value == SourceCharacter ? 0 : 1).ThenBy(x => x.k).Select(x => x.l).ToList();

        /// <summary>"+2 more": the end of a strip whose titles do not all fit; it opens the Titles page as a title does (review 0.6.5: they were dropped silently).</summary>
        public static string MoreTitles(int n) => "+" + N(n) + " more";

        /// <summary>The page's titles for its strip: the badges' titles, each with its first reason while the line has room (else names only).</summary>
        static List<Block> StripTitles(PanelInput input, PanelView view)
        {
            var rows = Titles(input).Where(t => view.Badges.Any(b => b.Id == t.Id)).ToList();
            var items = rows.Select(t =>
            {
                var first = Reasons(t).FirstOrDefault();
                var tag = first.Key == null ? null : TagOf(first.Value);
                return new Block { Kind = TitleKind, Id = TitlesLink + "/" + t.Id, Icon = "title:" + t.Id, Title = t.Title, Text = first.Key, Source = tag, Src = SrcOf(tag) };
            }).ToList();
            // the width on the safe side: the tag, then per title its emblem, name, reason and the gap
            var wide = 70 + items.Sum(i => 28 + i.Title.Length * 8.5f + (i.Text == null ? 0 : 10 + i.Text.Length * 7.2f) + 18);
            if (wide > StripTitleRoom) foreach (var i in items) { i.Text = null; i.Source = i.Src = null; }
            return items;
        }

        // how a title's line is counted, in the feats' words ("Your character", "Recorded from 1 October · this PC"; "Tor's character" on Tor's book)
        static string TitleCounted(PanelInput i, string source)
        {
            var d = source == Profile ? CountedCharacter : source == Fellows ? CountedFellows : CountedPc;
            if (d == CountedPc && i != null) return RecordedFromLine(i, StartOf(i, null));
            return i == null || i.IsSelf || d == CountedFellows ? d : d == CountedCharacter ? Name(i) + "'s character" : RecordedFromLine(i, null);
        }

        /// <summary>
        /// Feats > Titles: every title on cards like the feats' (the same grid and detail area), the ones held first in the table's order with their reason,
        /// then the ones not held yet, greyed, with what earns them (as Unsung does for feats and Deeds > Unsung for these names). The plate's line says
        /// what a title is next to a feat. A fellow's book: their titles, the same way.
        /// </summary>
        static void TitlesPage(PanelInput input, PanelView view, PanelState state)
        {
            view.Heading = TitlesHeading;
            var held = Titles(input);
            var order = held.Select(r => SagaTitles.First(t => t.Id == r.Id)).Concat(SagaTitles.Where(t => held.All(r => r.Id != t.Id))).ToList();
            var pick = order.Any(t => t.Id == state.FeatSel) ? state.FeatSel : order[0].Id;   // a strip's title opens with that card chosen
            var cards = order.Select(t => TitleCard(input, t, held.FirstOrDefault(r => r.Id == t.Id), pick)).ToList();
            view.Blocks.Add(new Block { Kind = "feats", Items = cards, Tone = TitlesPageId });
            view.Blocks.Add(cards.First(c => c.Selected).Items[0]);
            Plate(view, TitlesIcon, RecordedScope(input) ?? TitlesDefinition + " " + N(held.Count) + " of " + N(SagaTitles.Length) + " held.");
            PlateOf(view).Tone = PlateFull;
        }

        static Block TitleCard(PanelInput input, SagaTitle t, TitleRow row, string selected)
        {
            var first = row == null ? default : Reasons(row).FirstOrDefault();
            var tag = first.Key == null ? null : TagOf(first.Value);
            return new Block
            {
                Kind = "feat", Id = t.Id, Icon = "title:" + t.Id, Title = t.Title, Level = row != null ? 1 : 0, Count = 1, Colour = row != null ? TierColour(1, 1) : null,
                Tone = row != null ? FeatTone : FeatUnsung, Source = tag,
                // held: why (its first reason; when only an earn-only count holds it, HeldBefore); not held: the first step that earns it
                Text = row != null ? first.Key ?? HeldBefore(input, row) : Voice(input, FirstStep(t)),
                Selected = t.Id == selected, Items = new List<Block> { TitleDetail(input, t, row) },
            };
        }

        /// <summary>The reason of a title only an earn-only count holds (Stonebreaker by the game's own pickaxe count while Hearthwoven counted none: K6
        /// shows no number from it): the work and when, "Stone and ore broken before 9 October", as the other cards say their count; a fellow's book
        /// (no date): the work alone.</summary>
        public static string HeldBefore(PanelInput input, TitleRow row)
        {
            var from = StartOf(input, null);
            return from.HasValue ? row.Descriptor + " before " + RecordDate(input, from.Value) : row.Descriptor;
        }

        static Block TitleDetail(PanelInput input, SagaTitle t, TitleRow row)
        {
            var held = row != null;
            var detail = new Block
            {
                Kind = "featdetail", Id = t.Id, Icon = "title:" + t.Id, Title = t.Title, Text = row?.Descriptor ?? (input.IsSelf || !t.Descriptor.StartsWith("Your ") ? t.Descriptor : Name(input) + "'s " + t.Descriptor.Substring(5)), Tone = held ? FeatTone : FeatUnsung,
                Colour = held ? TierColour(1, 1) : null, Value = held ? TitleHeld : TitleNotHeld, Level = held ? 1 : 0, Count = 1, Items = new List<Block>(),
            };
            if (held && row.Lines.Count == 0) detail.Items.Add(new Block { Kind = "rule", Title = HeldBefore(input, row), Value = "", Selected = true, Colour = TierColour(1, 1) });   // only an earn-only count holds it
            else if (held) foreach (var l in Reasons(row)) detail.Items.Add(new Block { Kind = "rule", Title = l.Key, Value = "", Selected = true, Source = TagOf(l.Value), Colour = TierColour(1, 1) });
            else detail.Items.Add(new Block { Kind = "rule", Title = Voice(input, FirstStep(t)), Value = "" });
            detail.Items.Add(new Block { Kind = "counted", Text = string.Join(" · ", t.Lines.Select(l => TitleCounted(input, l.Source)).Distinct().ToArray()) });
            var chapter = ChapterRow.First(c => c.id == t.Chapter).label;
            var page = ListOf(input, t.Chapter).FirstOrDefault(c => c.Id == t.Page)?.Label;
            detail.Items.Add(new Block { Kind = "moment", Text = TitlePageWord + chapter + (page == null ? "" : " · " + page) });
            return detail;
        }

        // a number counted on this PC is labelled as such (the data flag the page rules read; the detail also says "Counted by ... this PC" in words)
        static void FeatsLabelPc(PanelInput input, IEnumerable<Block> blocks, string feat = null)
        {
            foreach (var b in blocks ?? Enumerable.Empty<Block>())
            {
                var id = (b.Kind == "feat" || b.Kind == "featdetail") && FeatById(b.Id) != null ? b.Id : feat;   // the feat the number belongs to
                if (b.Src == SrcPc && (b.Title ?? "").Any(char.IsDigit)) { b.RecordedFrom = FeatsPcShort(input, CountFrom(input, id)); }
                FeatsLabelPc(input, b.Items, id);
            }
        }
    }
}
