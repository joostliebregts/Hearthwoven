using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The group's feats, batch 3 (0.9.2, work/hearthwoven-0.9/FEAT-CATALOG-2.md "Together, batch 3"; Joost: "add a batch of Together feats"). Each one is
    /// summed or gathered over everyone who shares, each person once (GroupMembers), from what every shared copy already carries: the character's
    /// record (counters, pieces placed, items picked up, foes felled), Hearthwoven's counts since install ("measuredSinceInstall") and the lands found
    /// ("knownBiomes"). No new key in the copy, no server change. Never who did most: a set counts each different thing once, a sum is the group's.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>Said on the feats a fellow's copy adds to only through Hearthwoven's own count (since they installed it): an older Hearthwoven that does
        /// not share that count adds nothing until they update.</summary>
        public const string SinceTheyInstalled = "Counted by Hearthwoven since each player installed it; a fellow on an older version adds nothing until they update.";

        /// <summary>Every ship the game names as one ("$ship_raft", "$ship_karve", "$ship_longship", ...): a piece placed with the hammer.</summary>
        public static bool IsShip(string token) => token != null && token.StartsWith("$ship_", StringComparison.Ordinal);
        /// <summary>A plant put in the ground (crops and tree saplings alike): the game's piece tokens "$piece_sapling_...".</summary>
        public static bool IsPlanting(string token) => token != null && token.StartsWith("$piece_sapling_", StringComparison.Ordinal);

        static double Placed(PanelInput p, Func<string, bool> which) => (p?.PiecesPlaced ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0 && which(kv.Key)).Sum(kv => (double)kv.Value);
        static double PickedSum(PanelInput p, IEnumerable<string> tokens) => tokens.Sum(t => RecordAndSince(p, "pickedUp", p?.ItemsPickedUp, p?.Events?.PickedUp, t));

        /// <summary>The gear one member put on that another player made, each maker and item once ("maker|item"), never their own make.</summary>
        static IEnumerable<string> GearFromOthers(PanelInput p) => (p?.Events?.EquippedGearMadeBy ?? new Dictionary<string, float>())
            .Where(kv => kv.Value > 0 && Split2(kv.Key)[0].Length > 0 && Split2(kv.Key)[1].Length > 0 && !SameName(Split2(kv.Key)[0], p.PlayerName)).Select(kv => kv.Key);

        /// <summary>The feasts one member ate from, by the feast's own id (AteFromFeastAt "creatorId|feast|feast id": the same id on every PC).</summary>
        static IEnumerable<string> FeastsEatenFrom(PanelInput p) => (p?.Events?.AteFromFeastAt ?? new Dictionary<string, float>())
            .Where(kv => kv.Value > 0).Select(kv => kv.Key.Split('|')).Where(a => a.Length >= 3 && a[2].Length > 0).Select(a => a[2]).Distinct(StringComparer.Ordinal);

        /// <summary>Different feasts that two or more who share ate from, each feast once.</summary>
        static double FeastsShared(PanelInput i) =>
            GroupMembers(i).SelectMany(p => { try { return FeastsEatenFrom(p).ToList(); } catch { return new List<string>(); } })
                .GroupBy(id => id, StringComparer.Ordinal).Count(g => g.Count() >= 2);

        /// <summary>The Forsaken one member had a hand in felling (the game credits everyone who struck one: Character.OnDeath's attackers, RPC_RegisterKill).</summary>
        static IEnumerable<string> BossesFelled(PanelInput p) => Bosses.Where(b => p?.EnemyKills != null && p.EnemyKills.TryGetValue(b.token, out var n) && n > 0).Select(b => b.token);
        static double BossesFelledTogether(PanelInput i) =>
            GroupMembers(i).SelectMany(p => { try { return BossesFelled(p).ToList(); } catch { return new List<string>(); } })
                .GroupBy(t => t, StringComparer.Ordinal).Count(g => g.Count() >= 2);

        static IEnumerable<string> PowersCalled(PanelInput p) => PowerStats.Where(s => C(p, s) > 0);
        static IEnumerable<string> Plantings(PanelInput p) =>
            (p?.PiecesPlaced ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0 && IsPlanting(kv.Key)).Select(kv => kv.Key)
                .Concat((p?.Events?.Planted ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0 && IsPlanting(kv.Key)).Select(kv => kv.Key));
        /// <summary>The lands the group found (GroupFound: each member's record, the evidence an older copy carries), the sea among them as the game keeps it.</summary>
        static double LandsTogether(PanelInput i) => GroupFound(i).Count(b => !string.IsNullOrEmpty(b));

        /// <summary>A batch-3 feat counts only with two or more who share (as One Hearth): alone, its number is 0 and the card says no fellow shares yet
        /// (FeatHint). "Together" is a group's, never one player's own total under another name.</summary>
        static double? WithTwo(PanelInput i, Func<PanelInput, double> f) => GroupMembers(i).Count >= 2 ? f(i) : 0;
        /// <summary>The batch's feats (WithTwo): alone, the card's hint says no fellow shares yet rather than "none yet".</summary>
        public static readonly HashSet<string> NeedsTwo = new HashSet<string>(StringComparer.Ordinal)
            { "quarry", "woodpile", "fleet", "doorways", "garden", "everyland", "oldpowers", "felledtogether", "sharedtable", "longtable", "handtohand", "onemap" };

        static IEnumerable<FeatDef> GroupFeatDefs3() => new[]
        {
            // ----- hauled together: the record and the exact count since, as Bog Iron -----
            new FeatDef("quarry", "The Quarry", "miner", Chapter.Feats, FeatsTogetherId, "Every wall the group raised began as stone someone carried home.", new[] { 5000.0, 20000.0, 75000.0 },
                t => "Together, bring in " + N(t) + " stone.",
                i => WithTwo(i, g => GroupSum(g, p => PickedSum(p, StoneTokens))),
                CountedGroup, "Stone of every land counts, once, for whoever takes it first; stone from pieces taken down never counts. " + BooksDiffer, exact: false, derived: true,
                brief: t => N(t) + " stone hauled", unit: "stone", group: true)
            { Picture = "vocab:rock-stone", Part = p => PickedSum(p, StoneTokens), Helped = "Who quarried" },
            new FeatDef("woodpile", "One Woodpile", "woodcutter", Chapter.Feats, FeatsTogetherId, "One stack by the hall, fed by every axe.", new[] { 10000.0, 50000.0, 200000.0 },
                t => "Together, bring in " + N(t) + " wood.",
                i => WithTwo(i, g => GroupSum(g, p => PickedSum(p, WoodTokens))),
                CountedGroup, "Wood of every kind counts, once, for whoever takes it first; wood from pieces taken down never counts. " + BooksDiffer, exact: false, derived: true,
                brief: t => N(t) + " wood hauled", unit: "wood", group: true)
            { Picture = "vocab:tree-oak", Part = p => PickedSum(p, WoodTokens), Helped = "Who brought it in" },
            // ----- built together: the character's record of pieces placed (every version shares it) -----
            new FeatDef("fleet", "The Fleet", "sailor", Chapter.Feats, FeatsTogetherId, "Hulls laid down one by one, until the shore was crowded.", new[] { 2.0, 5.0, 12.0 },
                t => "Together, build " + N(t) + " ships.",
                i => WithTwo(i, g => GroupSum(g, p => Placed(p, IsShip))),
                CountedGroup, "A ship taken apart or lost at sea still counts, as the game's record keeps it.", exact: false, derived: true,
                brief: t => N(t) + " ships built", unit: "ship|ships", group: true)
            { Picture = "vocab:list-sailing", Part = p => Placed(p, IsShip), Helped = "Who built" },
            new FeatDef("doorways", "Doorways Home", "explorer", Chapter.Feats, FeatsTogetherId, "However far anyone wandered, a door stood open back to the hall.", new[] { 4.0, 12.0, 30.0 },
                t => "Together, raise " + N(t) + " portals.",
                i => WithTwo(i, g => GroupSum(g, p => Placed(p, PortalTokens.Contains))),
                CountedGroup, "A portal moved or taken down still counts, as the game's record keeps it.", exact: false, derived: true,
                brief: t => N(t) + " portals raised", unit: "portal|portals", group: true)
            { Picture = "vocab:portal-mark", Part = p => Placed(p, PortalTokens.Contains), Helped = "Who raised them" },
            new FeatDef("garden", "The Common Garden", "farmer", Chapter.Feats, FeatsTogetherId, "Rows and saplings of every kind, planted by whoever passed.", new[] { 4.0, 8.0, 14.0 },
                t => "Together, plant " + N(t) + " different kinds of crop or tree.",
                i => WithTwo(i, g => GroupUnion(g, Plantings)),
                CountedGroup, "Each kind counts once, whoever planted it and however many.", exact: false, derived: true,
                brief: t => N(t) + " kinds planted", unit: "kind|kinds", group: true)
            { Part = p => Plantings(p).Distinct(StringComparer.Ordinal).Count(), Helped = "Who planted", Overlaps = true },
            // ----- the lands and the Forsaken: sets, each thing once -----
            new FeatDef("everyland", "Lands Between Us", "explorer", Chapter.Feats, FeatsTogetherId, "No one walked them all, but together the map filled in.", new[] { 3.0, 5.0, 8.0 },
                t => "Together, find " + N(t) + " lands.",
                i => WithTwo(i, g => LandsTogether(g)),
                CountedGroup, "Each land counts once, whoever found it. The sea counts, as the game keeps it. A copy from before 0.7 adds the lands it shows a fight or a death in.", exact: false, derived: true,
                brief: t => N(t) + " lands found", unit: "land|lands", group: true)
            { Picture = "vocab:compass-rose", Part = LandsWalked, Helped = "Who walked them", Overlaps = true },
            new FeatDef("oldpowers", "Old Powers Answer", "bossbane", Chapter.Feats, FeatsTogetherId, "Each gift of the Forsaken, called on by someone in the hall.", new[] { 2.0, 4.0, 7.0 },
                t => "Together, call on " + N(t) + " different Forsaken powers.",
                i => WithTwo(i, g => GroupUnion(g, PowersCalled)),
                CountedGroup, "Each power counts once, whoever called on it. Setting a power at a stone does not count.", exact: false, derived: true,
                brief: t => N(t) + " powers called on", unit: "power|powers", group: true)
            { Picture = "vocab:boss-ring", Part = p => PowersCalled(p).Count(), Helped = "Who called on them", Overlaps = true },
            new FeatDef("felledtogether", "Felled as One", "bossbane", Chapter.Feats, FeatsTogetherId, "No old power fell to one hand alone.", new[] { 1.0, 3.0, 6.0 },
                t => t <= 1 ? "Fell a Forsaken with two or more who share striking it." : "Fell " + N(t) + " different Forsaken, two or more who share striking each.",
                i => WithTwo(i, g => BossesFelledTogether(g)),
                CountedGroup, "The game credits everyone who struck a Forsaken; it counts once two who share were credited, in one fight or two.", exact: false, derived: true,
                brief: t => N(t) + " Forsaken felled", unit: "Forsaken", group: true)
            { Picture = "vocab:boss-ring", Part = p => BossesFelled(p).Count(), Helped = "Who struck them", Overlaps = true },
            // ----- given and shared: Hearthwoven's own counts since each player installed it -----
            new FeatDef("sharedtable", "The Shared Table", "cook", Chapter.Feats, FeatsTogetherId, "Nobody in the hall lived on their own cooking alone.", new[] { 50.0, 250.0, 1000.0 },
                t => "Together, enjoy " + N(t) + " dishes another player made.",
                i => WithTwo(i, g => GroupSum(g, DishesFromOthers)),
                CountedGroup, SinceTheyInstalled, exact: false, derived: true,
                brief: t => N(t) + " dishes shared", unit: "dish|dishes", group: true)
            { Picture = "vocab:list-food-shared", Part = DishesFromOthers, Helped = "Who was fed" },
            new FeatDef("longtable", "The Long Table", "cook", Chapter.Feats, FeatsTogetherId, "One feast set out, and the whole hall pulling up a seat.", new[] { 1.0, 5.0, 15.0 },
                t => t <= 1 ? "Share a feast: two or more who share enjoy it." : "Share " + N(t) + " feasts, each enjoyed by two or more who share.",
                i => WithTwo(i, g => FeastsShared(g)),
                CountedGroup, "A feast counts once two who share have enjoyed it. Recorded from Hearthwoven 0.8; a fellow on an older version adds nothing until they update.", exact: false, derived: true,
                brief: t => t <= 1 ? "1 feast shared" : N(t) + " feasts shared", unit: "feast|feasts", group: true)
            { Picture = "item:$item_feastmeadows|title:cook", Part = p => FeastsEatenFrom(p).Count(), Helped = "Who feasted", Overlaps = true },
            new FeatDef("handtohand", "Hand to Hand", "helper", Chapter.Feats, FeatsTogetherId, "Gear made by one, carried into the fight by another.", new[] { 5.0, 20.0, 60.0 },
                t => "Together, put on " + N(t) + " pieces of gear another player made.",
                i => WithTwo(i, g => GroupSum(g, p => GearFromOthers(p).Count())),
                CountedGroup, "Each player counts a piece once per maker, however often they put it on. " + SinceTheyInstalled, exact: false, derived: true,
                brief: t => N(t) + " pieces passed on", unit: "piece|pieces", group: true)
            { Picture = "vocab:list-gear-shared", Part = p => GearFromOthers(p).Count(), Helped = "Who wore them" },
            // every member who shares has written their map to a table; two or more (as One Hearth: never "everyone", only those who share)
            new FeatDef("onemap", "One Map", "mapmaker", Chapter.Feats, FeatsTogetherId, "Everyone who shares has added what they saw to the hall's map.", new[] { 1.0 },
                t => "Every player who shares has written their map to a map table.",
                i => { var m = GroupMembers(i); return m.Count >= 2 && m.All(p => M(p, e => e.MapShared) > 0) ? 1 : 0; },
                CountedGroup, "Every fellow player who shares, never everyone: it needs two or more who share. " + SinceTheyInstalled, exact: false, derived: true,
                brief: t => "Each added to the map", unit: "", group: true)
            { Part = p => M(p, e => e.MapShared), Helped = "Maps written", Overlaps = true },
        };
    }
}
