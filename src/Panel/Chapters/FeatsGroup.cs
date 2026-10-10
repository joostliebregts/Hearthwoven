using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// A feat's veil (GROUP-FEATS.md 1.4): a biome's feat first shows a riddle name and line, its biome's emblem as its picture, and a rule that names
    /// no material; it lifts once anyone who shares has held one of <see cref="Lifts"/> (or the group already earned a tier of it).
    /// </summary>
    public class FeatVeil
    {
        public string Name, Honours, Unit;
        /// <summary>The caveat under the riddle (null: the feat's own caveat names no material and stays).</summary>
        public string Caveat;
        public Func<double, string> Rule, Brief;
        /// <summary>Item tokens ("$item_copperore"): held by anyone who shares, the veil lifts.</summary>
        public string[] Lifts;
        /// <summary>false: the feat's own number does not lift the veil (Iron for the Forge counts copper and tin ore too, so moving ore says nothing about iron).</summary>
        public bool ValueLifts = true;
    }

    /// <summary>
    /// The feats of the whole group (Hearthwoven 0.7, design work/hearthwoven-0.7/GROUP-FEATS.md, approved by Joost 2026-10-09: "build these, tweak names
    /// later"): one number for you and every fellow player who shares, each once, the same on every book, on Feats > Together. The groundwork:
    ///   - who counts (GroupMembers): you (or the fellow whose book it is) and everyone in the Fellows list, deduplicated by name as GroupBooks does;
    ///   - the past counts (decision 1): each member's character record before install plus Hearthwoven's exact count since, as the Together page does
    ///     (RecordAndSince); a fellow's copy has no baseline, so the larger of the two stands for both and nothing counts twice;
    ///   - every world counts together (decision 2): the character record is kept in every world, said once in each detail's Counted by (CountedGroupLine);
    ///   - the veil (decision 3): a riddle name until someone who shares has held the material (FeatVeil);
    ///   - the biome gate (decision 5): a biome's feats are not on the page until the group found that biome (GroupFound: each member's known biomes,
    ///     shared in the snapshot's "knownBiomes" key from 0.7, and for older copies the evidence they carry), with one quiet line while any wait;
    ///   - the tiers (decision 6): guesses until calibrated on the real shared copies (GROUP-FEATS.md section 5), one array per feat in GroupFeatDefs.
    /// Nothing here names a boss, a dungeon, a later biome or what something unlocks. The tier itself never drops: EvaluateFeats records it in the
    /// player's own ledger the moment it is crossed, and FeatTier keeps the higher of the record and the number (a fellow who stops sharing).
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>How a group feat is counted (its detail's "Counted by"), the same words on every book.</summary>
        public const string CountedGroup = "Everyone who shares";
        /// <summary>A group feat's "Counted by", said once in one plain line (decision 2, the character's own record counts in every world, LocalTotals: per
        /// character, across every world and server; B37, Joost 2026-10-10: "Everyone who shares" and "Counted across every world you play. What a fellow who
        /// does not share cooks is missed." said the same thing twice).</summary>
        public const string CountedGroupLine = "Everyone who shares, in every world {you} play";   // "they play" on a fellow's book (FeatWords): a book never says "you" about its owner
        /// <summary>The Together page's quiet line while a biome's feats are still hidden (no count, no names).</summary>
        /// <summary>On the feats that layer a record and an exact count (GROUP-FEATS-TIERS.md): your own book adds what was counted exactly since
        /// install on top of your record, a fellow's copy has no baseline and takes the larger, so the same group total can differ by a few per cent per book. Accepted, and said.</summary>
        public const string BooksDiffer = "Each player's book can show a slightly different total.";
        public const string LandsNotFound = "More wait in lands not yet found.";
        /// <summary>B37 (Joost: '"added to by"? what does that mean?'): the row under a group feat's detail, in the feat's own verb where it has one.</summary>
        public const string WhoHelped = "Who helped";

        /// <summary>The Together page's order: journey order (Black Forest, Swamp), the cross-cutting ones after, the variety feat last; never by size.</summary>
        static readonly string[] TogetherOrder = { "copperwed", "bogiron", "ironforge", "charcoal", "workshop", "wellfed", "meadhall", "clad", "manycrafts" };

        /// <summary>The crafting stations' own extensions (reference/item-names.json: workbench 4, forge 6, cauldron 6, black forge 5, galdr table 4, artisan table 1).
        /// A mod's own pieces (kiln vents, bellows of a smelter mod) are no crafting-station extension and stay out.</summary>
        public static readonly HashSet<string> StationExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "$piece_workbench_ext1", "$piece_workbench_ext2", "$piece_workbench_ext3", "$piece_workbench_ext4",
            "$piece_forge_ext1", "$piece_forge_ext2", "$piece_forge_ext3", "$piece_forge_ext4", "$piece_forge_ext5", "$piece_forge_ext6",
            "$piece_cauldron_ext1_spice", "$piece_cauldron_ext3_butchertable", "$piece_cauldron_ext4_pans", "$piece_cauldron_ext5_mortarandpestle",
            "$piece_cauldron_ext6_rollingpins", "$piece_cauldron_ext7_smoker",
            "$piece_blackforge_ext1", "$piece_blackforge_ext2", "$piece_blackforge_ext3", "$piece_blackforge_ext4", "$piece_blackforge_ext5",
            "$piece_magetable_ext", "$piece_magetable_ext2", "$piece_magetable_ext3", "$piece_magetable_ext4",
            "$piece_artisan_ext1",
        };

        /// <summary>A mead base, by the game's item keys (reference/item-names.json): every "$item_meadbase..." and the barley wine base.</summary>
        public static bool IsMeadBase(string token) => token != null && (token.StartsWith("$item_meadbase", StringComparison.Ordinal) || token.StartsWith("$item_barleywinebase", StringComparison.Ordinal));


        // ---------- the group's eight (GROUP-FEATS.md section 4); tiers calibrated on the live group's shared copies (GROUP-FEATS-TIERS.md, 2026-10-09), one array per feat: tune them here ----------

        static FeatDef[] GroupFeatDefs() => new[]
        {
            new FeatDef("copperwed", "Copper Wed to Tin", "smith", Chapter.Feats, FeatsTogetherId,
                "Copper and tin, smelted and joined into bronze: blades, tools and the first real armor.", new[] { 50.0, 250.0, 500.0 },
                t => "Together, make " + N(t) + " bronze.",
                i => GroupSum(i, p => RecordAndSince(p, "pickedUp", p.ItemsPickedUp, p.Events?.Made, "$item_bronze", LocalTotals.CraftedKind)),
                CountedGroup, BooksDiffer, exact: false, derived: true,
                brief: t => N(t) + " bronze made", unit: "bronze", group: true)
            {
                Biome = "BlackForest", Picture = "item:$item_bronze|title:smith",
                Part = p => RecordAndSince(p, "pickedUp", p.ItemsPickedUp, p.Events?.Made, "$item_bronze", LocalTotals.CraftedKind), Helped = "Who made it",
                Veil = new FeatVeil
                {
                    Name = "What the Deep Woods Hold", Honours = "Something in these woods is worth the fire.", Lifts = new[] { "$item_copperore", "$item_tinore" },
                    Rule = t => "Together, make " + N(t) + " of what these woods hold.", Brief = t => N(t) + " from the woods",
                },
            },
            new FeatDef("bogiron", "Bog Iron", "smith", Chapter.Feats, FeatsTogetherId,
                "Iron from the bog, smelted bar by bar: the backbone of every hall.", new[] { 150.0, 600.0, 2500.0 },
                t => "Together, take " + N(t) + " iron bars from the smelters.",
                i => GroupSum(i, p => RecordAndSince(p, "pickedUp", p.ItemsPickedUp, p.Events?.PickedUp, "$item_iron")),
                CountedGroup, "A bar counts once, for whoever takes it first. " + BooksDiffer, exact: false, derived: true,
                brief: t => N(t) + " iron bars", unit: "iron bars", group: true)
            {
                Biome = "Swamp", Picture = "item:$item_iron|title:smith",
                Part = p => RecordAndSince(p, "pickedUp", p.ItemsPickedUp, p.Events?.PickedUp, "$item_iron"), Helped = "Who smelted",
                Veil = new FeatVeil
                {
                    Name = "What the Bog Keeps", Honours = "The bog keeps something worth the forge.", Lifts = new[] { "$item_ironscrap" },
                    Caveat = "Each counts once, for whoever takes it first.",   // "a bar" would name the metal
                    Rule = t => "Together, bring home " + N(t) + " of what the bog keeps.", Brief = t => N(t) + " from the bog",
                },
            },
            // coal first held (GROUP-FEATS-TIERS.md): the character's record plus the exact pickups since, as Bog Iron. Kiln wood was hand-feeding only and
            // stayed 0 where Stoker's Chests feed the kilns (StokerHooks); coal taken out of a kiln counts however the kiln was fed
            new FeatDef("charcoal", "The Charcoal Burners", "smith", Chapter.Feats, FeatsTogetherId,
                "Wood burned slow to coal, so the smelters never go cold.", new[] { 500.0, 2500.0, 10000.0 },
                t => "Together, take " + N(t) + " coal.",
                i => GroupSum(i, Coal),
                CountedGroup, "Coal counts once, for whoever takes it first. " + BooksDiffer, exact: false, derived: true,
                brief: t => N(t) + " coal", unit: "coal", group: true)
            { Biome = "BlackForest", Picture = "item:$item_coal|title:smith", Part = Coal },
            // a set over everyone's character record (pieces placed), so an extension two players placed counts once
            new FeatDef("workshop", "The Workshop Grows", "builder", Chapter.Feats, FeatsTogetherId,
                "Every tool and rack added to the group's crafting stations.", new[] { 5.0, 12.0, 22.0 },
                t => "Together, add " + N(t) + " different extensions to the crafting stations.",
                i => GroupUnion(i, Extensions),
                CountedGroup, "An extension moved or taken down still counts.", exact: false, derived: true,
                brief: t => N(t) + " station extensions", unit: "extension|extensions", group: true)
            { Picture = "piece:$piece_workbench_ext1|title:builder", Part = p => Extensions(p).Count(), Helped = "Who built", Overlaps = true },   // a workbench extension: ungated, so never the forge's anvil (the Swamp's iron)
            // the Together page's "dishes cooked" per member (DishesCooked: the cooking counters before install plus the exact count since), so the two agree
            new FeatDef("wellfed", "The Hall Well Fed", "cook", Chapter.Feats, FeatsTogetherId,
                "Every dish cooked for the hall, from the first grilled meat to the finest pie.", new[] { 250.0, 1000.0, 3000.0 },
                t => "Together, cook " + N(t) + " dishes.",
                i => GroupSum(i, DishesCooked),
                CountedGroup, null, exact: false, derived: true,
                brief: t => N(t) + " dishes cooked", unit: "dish|dishes", group: true)
            { Part = DishesCooked, Helped = "Who cooked" },
            new FeatDef("meadhall", "The Mead Hall", "cook", Chapter.Feats, FeatsTogetherId,
                "Meads brewed for the hall: healing, warmth and courage in a bottle.", new[] { 10.0, 40.0, 100.0 },
                t => "Together, brew " + N(t) + " mead bases.",
                i => GroupSum(i, MeadBases),
                CountedGroup, "A mead base counts once, for whoever brewed it. " + BooksDiffer, exact: false, derived: true,
                brief: t => N(t) + " mead bases", unit: "mead base|mead bases", group: true)
            { Biome = "BlackForest", Picture = "item:$item_meadbasehealth|title:cook", Part = MeadBases, Helped = "Who brewed" },
            // the game's own armour counter (CraftArmor) is complete: it books every piece made in the crafting window, also after install, so it
            // stands alone (adding Hearthwoven's count since would count those twice; the Crafting page reads it the same way)
            new FeatDef("clad", "Clad for the Road", "smith", Chapter.Feats, FeatsTogetherId,
                "Armor made for the group, piece by piece.", new[] { 12.0, 40.0, 120.0 },
                t => "Together, craft " + N(t) + " pieces of armor.",
                i => GroupSum(i, p => C(p, "CraftArmor")),
                CountedGroup, "Helmets, chest and leg pieces and capes count, as the game counts armor; shields and upgrades do not.", exact: false, derived: true,
                brief: t => N(t) + " pieces of armor", unit: "armor piece|armor pieces", group: true)
            { Picture = "item:$item_helmet_leather|title:smith", Part = p => C(p, "CraftArmor"), Helped = "Who crafted" },
            // a set: each different thing once, whoever made it and however many (the record of items crafted, and since install what was made,
            // which adds the dishes a grill or oven books to its owner)
            new FeatDef("manycrafts", "Many Crafts, One Hall", "smith", Chapter.Feats, FeatsTogetherId,
                "Not one thing in bulk, but many: every different thing the group has made, cooked or brewed.", new[] { 40.0, 120.0, 250.0 },
                t => "Together, make " + N(t) + " different things.",
                i => GroupUnion(i, ThingsMade),
                CountedGroup, "Each different thing counts once, whoever made it and however many.", exact: false, derived: true,
                brief: t => N(t) + " different things", unit: "different things", group: true)
            { Picture = "title:manycrafts", Part = p => ThingsMade(p).Count(), Overlaps = true },   // Codex's own emblem (icons-0.7), not the smith's
        };

        // ---------- who counts and how, once each ----------

        /// <summary>You (or the fellow whose book it is) and every fellow player who shares, each once: the Fellows list deduplicated by who they are (MemberKey,
        /// the fellow key GroupShare keeps their copy under), never by character name, so two players called Tor both count and a stale second copy of
        /// one Tor does not (a fellow's page holds you among their Fellows, so the same people count on every book).</summary>
        public static List<PanelInput> GroupMembers(PanelInput i) =>
            i == null ? new List<PanelInput>() : new[] { i }.Concat((i.Fellows ?? new List<PanelInput>()).Where(f => f?.Events != null)).Where(p => p != null).GroupBy(MemberKey, StringComparer.Ordinal).Select(g => g.First()).ToList();

        /// <summary>Who a group member is: a fellow's copy by its fellow key (FellowIds.KeyOf: platform id and profile id), else the player id, else the name
        /// (FellowIds' own fallbacks, in the same forms).</summary>
        public static string MemberKey(PanelInput p) =>
            !string.IsNullOrEmpty(p?.FellowKey) ? p.FellowKey : (p?.PlayerId ?? 0) != 0 ? FellowIds.IdPrefix + p.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) : FellowIds.NamePrefix + (p?.PlayerName ?? "");

        static double GroupSum(PanelInput i, Func<PanelInput, double> part) => GroupMembers(i).Sum(p => { try { return Math.Max(0, part(p)); } catch { return 0; } });
        static double GroupUnion(PanelInput i, Func<PanelInput, IEnumerable<string>> keys) =>
            GroupMembers(i).SelectMany(p => { try { return keys(p) ?? Enumerable.Empty<string>(); } catch { return Enumerable.Empty<string>(); } }).Distinct(StringComparer.Ordinal).Count();
        /// <summary>Everyone who added to a group feat with their own part and its share of all the parts, largest first (B37, Joost 2026-10-10: "Who helped"
        /// with each one's share, as the group view does), then by name.</summary>
        public static List<(string name, double part, double share)> GroupShares(PanelInput i, Func<PanelInput, double> part)
        {
            var parts = GroupMembers(i).Select(p => (name: p.PlayerName ?? "", part: Safe(() => Math.Max(0, part(p))))).Where(x => x.name.Length > 0 && x.part > 0).ToList();
            var all = parts.Sum(x => x.part);
            return parts.Select(x => (x.name, x.part, share: all > 0 ? x.part / all : 0)).OrderByDescending(x => x.part).ThenBy(x => x.name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        static double Safe(Func<double> f) { try { return f(); } catch { return 0; } }

        /// <summary>
        /// One member's count of one item: the character's record before install (the baseline of <paramref name="kind"/>, taken when Hearthwoven first ran)
        /// plus Hearthwoven's exact count since (<paramref name="exact"/>, less what it had already counted when that baseline was taken: the
        /// <paramref name="exactKind"/> baseline's, when the exact count is of another kind, as bronze made is beside bronze held). A fellow's copy has no
        /// baseline: the larger of their record and their exact count stands for both (LocalTotals.Layers), so nothing is counted twice.
        /// </summary>
        static double RecordAndSince(PanelInput p, string kind, IDictionary<string, float> record, IDictionary<string, float> exact, string token, string exactKind = null)
        {
            double game = record != null && record.TryGetValue(token, out var g) ? g : 0;
            double now = exact != null && exact.TryGetValue(token, out var e) ? e : 0;
            if (p?.Baseline == null || !p.Baseline.TryGetValue(kind, out var stored) || stored == null) return Math.Max(game, now);
            stored.TryGetValue(token, out var before);
            float already = 0;
            if (p.ExactAtBaseline != null && p.ExactAtBaseline.TryGetValue(exactKind ?? kind, out var at) && at != null) at.TryGetValue(token, out already);
            return before + Math.Max(0, now - already);
        }

        static double Coal(PanelInput p) => RecordAndSince(p, "pickedUp", p.ItemsPickedUp, p.Events?.PickedUp, "$item_coal");
        static IEnumerable<string> Extensions(PanelInput p) => (p?.PiecesPlaced ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0 && StationExtensions.Contains(kv.Key)).Select(kv => kv.Key);
        static double MeadBases(PanelInput p)
        {
            var tokens = new HashSet<string>((p?.ItemsCrafted ?? new Dictionary<string, float>()).Keys.Concat(p?.Events?.Made.Keys ?? Enumerable.Empty<string>()).Where(IsMeadBase));
            return tokens.Sum(t => RecordAndSince(p, LocalTotals.CraftedKind, p.ItemsCrafted, p.Events?.Made, t));
        }
        static IEnumerable<string> ThingsMade(PanelInput p) =>
            (p?.ItemsCrafted ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).Select(kv => kv.Key)
                .Concat((p?.Events?.Made ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0).Select(kv => kv.Key));

        // ---------- the gate and the veil ----------

        /// <summary>
        /// The biomes the group found: each member's own record (KnownBiomes: yours from the game, a fellow's from their copy's "knownBiomes", 0.7 on),
        /// and for a copy without it the evidence it carries: damage or a death there (this session's log, the per-biome totals since install) or a feat
        /// earned there. Never who found it first.
        /// </summary>
        public static HashSet<string> GroupFound(PanelInput i)
        {
            var memo = featMemo;   // once per render (FeatMemo); callers only read the set
            if (memo != null && i != null && memo.Found.TryGetValue(i, out var known)) return known;
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in GroupMembers(i))
            {
                foreach (var b in FoundBiomes(p)) found.Add(b);
                foreach (var b in p.KnownBiomes ?? new string[0]) found.Add(b);   // a fellow's shared record: the gate only, never their pages (FoundBiomes)
                if (p.BiomeSinceInstall != null)
                {
                    foreach (var k in p.BiomeSinceInstall.Damage.Keys) found.Add(k.Split('|')[0]);
                    foreach (var kv in p.BiomeSinceInstall.Deaths) if (kv.Value > 0) found.Add(kv.Key);
                }
                if (p.Feats != null) foreach (var m in p.Feats.Earned.Values.SelectMany(l => l)) if (!string.IsNullOrEmpty(m.Biome)) found.Add(m.Biome);
            }
            found.Remove("None");
            if (memo != null && i != null) memo.Found[i] = found;
            return found;
        }

        /// <summary>A group feat is on the Together page when it has no biome, when the group found its biome, or once a tier of it is earned (an earned
        /// tier never hides again, also when the fellow who found the land stops sharing).</summary>
        public static bool GroupVisible(FeatDef d, PanelInput i, HashSet<string> found = null) =>
            !d.Group || d.Biome == null || (i?.Feats?.Tier(d.Id) ?? 0) > 0 || (found ?? GroupFound(i)).Contains(d.Biome);

        /// <summary>True while the feat's riddle stands: no one who shares has held its material yet, and no tier of it is earned.</summary>
        public static bool FeatVeiled(FeatDef d, PanelInput i)
        {
            if (d.Veil == null) return false;
            var memo = featMemo;   // once per feat per render (FeatMemo)
            if (memo != null && i != null && memo.Veiled.TryGetValue((i, d.Id), out var hit)) return hit;
            var veiled = VeiledNow(d, i);
            if (memo != null && i != null) memo.Veiled[(i, d.Id)] = veiled;
            return veiled;
        }

        static bool VeiledNow(FeatDef d, PanelInput i)
        {
            if (d.Veil == null || (i?.Feats?.Tier(d.Id) ?? 0) > 0 || (d.Veil.ValueLifts && FeatValue(d, i) > 0)) return false;   // someone already has the thing itself: its name is no secret
            foreach (var p in GroupMembers(i))
                foreach (var t in d.Veil.Lifts ?? new string[0])
                    if ((p.ItemsPickedUp != null && p.ItemsPickedUp.TryGetValue(t, out var a) && a > 0) || (p.Events != null && p.Events.PickedUp.TryGetValue(t, out var b) && b > 0)) return false;
            return true;
        }

        /// <summary>The group feats on the Together page, in their order; hidden: whether any gated feat still waits for its land.</summary>
        static List<FeatDef> TogetherFeats(PanelInput input, out bool hidden)
        {
            var found = GroupFound(input);
            var all = FeatDefs.Where(d => d.Group).ToList();
            var shown = all.Where(d => GroupVisible(d, input, found)).ToList();
            hidden = shown.Count < all.Count;
            int at(FeatDef d) { var k = Array.IndexOf(TogetherOrder, d.Id); return k < 0 ? TogetherOrder.Length : k; }
            return shown.OrderBy(at).ToList();
        }

        // what a card and its detail say, veiled or not
        public static string FeatTitle(FeatDef d, PanelInput i) => FeatVeiled(d, i) ? d.Veil.Name : d.Name;
        static string FeatHonours(FeatDef d, PanelInput i) => FeatVeiled(d, i) ? d.Veil.Honours : d.Honours;
        static string FeatPicture(FeatDef d, PanelInput i) => FeatVeiled(d, i) ? BiomeEmblem(d.Biome) : FeatIcon(d);
        static Func<double, string> FeatRule(FeatDef d, PanelInput i) => FeatVeiled(d, i) ? d.Veil.Rule : d.Rule;
        static Func<double, string> FeatBriefOf(FeatDef d, PanelInput i) => FeatVeiled(d, i) ? d.Veil.Brief : d.Brief;
        static string FeatUnit(FeatDef d, PanelInput i) => FeatVeiled(d, i) ? d.Veil.Unit : d.Unit;
        static string FeatCaveat(FeatDef d, PanelInput i) => FeatVeiled(d, i) && d.Veil.Caveat != null ? d.Veil.Caveat : d.Caveat;
    }
}
