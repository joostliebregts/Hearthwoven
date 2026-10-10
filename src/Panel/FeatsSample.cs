using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The feats of the fictional sample (Rowan with Edda, Finch and Tor), a mix of earned and unsung, added on top of
    /// PanelSample.Full for the tests, the previews and Dev.SampleData. Rowan: Ferryman I (Edda sailed under his helm: derived),
    /// Waymate (his character's record), Shield Wall I, Unbroken and Kept the Fires I (his own counts); Full Table and Arms for the
    /// Hall one fellow short, the rest unsung, the cargo and herd feats waiting for their data. Fellows carry what they shared.
    /// Pure C#; nothing here touches a file or the game.
    /// </summary>
    public static class FeatsSample
    {
        static FeatMoment At(DateTime now, double daysAgo, string biome, string place = null) => new FeatMoment { Utc = now.AddDays(-daysAgo), Biome = biome, Place = place };

        /// <summary>Adds the sample's feats to <paramref name="full"/> (PanelSample.Full) and to its fellows.</summary>
        public static void Apply(PanelInput full, DateTime now)
        {
            // Rowan's own counts: the shield hooks' counters, and a smelter feed that is over the first line
            var mine = new FeatsLedger { Primed = true };
            mine.Counts[FeatsCounters.BlocksNear] = 310; mine.Counts[FeatsCounters.Stopped] = 4180; mine.Counts[FeatsCounters.BestStreak] = 11;
            SessionEvents.Add(full.Events.SmelterAdded, "blastfurnace|IronOre", 1250);
            full.Character["TombstonesOpenedOther"] = 4;
            mine.Earn("waymate", 1, new FeatMoment { Before = true });
            mine.Earn("keptfires", 1, At(now, 2, "Mountain"));
            mine.Earn("shieldwall", 1, At(now, 4, "BlackForest"));
            mine.Earn("unbroken", 1, At(now, 1, "Swamp"));
            mine.Earn("ferryman", 1, new FeatMoment { Utc = now.AddDays(-3), Noticed = true });
            LedSample.Bests(mine, "Rowan", now);   // Heavy Keel's best load and Long Lead's longest lead (the notes the hooks make)
            mine.Earn("heavykeel", 1, At(now, 3, "Ocean", "aboard"));
            mine.Earn("drover", 2, At(now, 4, "Meadows")); mine.Earn("longlead", 1, At(now, 5, "Meadows"));
            mine.Seen = 7;   // two earned tiers not seen yet: the gold dots
            if (!full.InstalledUtc.HasValue) full.InstalledUtc = now.Date.AddDays(-6).AddHours(16);   // the zones' install date, as Evening sets it
            full.Feats = mine;
            // coal taken out of the kilns (The Charcoal Burners counts coal first held, GROUP-FEATS-TIERS.md): the character's record plus this session's
            Coal(full, 640, 35);


            foreach (var f in full.Fellows ?? new List<PanelInput>())
            {
                var theirs = new FeatsLedger();
                switch (f.PlayerName)
                {
                    case "Edda":
                        theirs.Earn("oreroad", 2, At(now, 5, "Ocean")); theirs.Earn("feastgiver", 1, At(now, 3, "Meadows")); theirs.Earn("keptfires", 1, At(now, 2, "Mountain"));
                        SessionEvents.Add(f.Events.SailedUnderHelmOf, "Rowan", 9000);   // 2.5 hours under Rowan's helm: his Ferryman I
                        break;
                    case "Tor":
                        theirs.Earn("shieldwall", 2, At(now, 4, "BlackForest")); theirs.Earn("stoodfast", 1, At(now, 3, "Swamp")); theirs.Earn("unbroken", 1, At(now, 2, "Plains"));
                        theirs.Earn("turnedblades", 1, At(now, 6, "Meadows"));
                        break;
                    case "Finch":
                        theirs.Earn("ferryman", 1, new FeatMoment { Utc = now.AddDays(-2), Noticed = true }); theirs.Earn("arms", 1, new FeatMoment { Utc = now.AddDays(-1), Noticed = true });
                        theirs.Earn("longlead", 1, At(now, 5, "Meadows")); LedSample.Bests(theirs, "Finch", now);
                        break;
                }
                Coal(f, f.PlayerName == "Edda" ? 420 : f.PlayerName == "Tor" ? 180 : 90, 0);
                theirs.BestsShared = true;   // they run this version: it shares the bests behind Heavy Keel and Long Lead (an empty "featBests" when none yet)
                f.Feats = theirs;
            }
        }

        static void Coal(PanelInput p, float record, int session)
        {
            if (p.ItemsPickedUp == null) p.ItemsPickedUp = new Dictionary<string, float>();
            p.ItemsPickedUp.TryGetValue("$item_coal", out var had); p.ItemsPickedUp["$item_coal"] = had + record + session;
            if (session > 0 && p.Events != null) SessionEvents.Add(p.Events.PickedUp, "$item_coal", session);
        }

        /// <summary>
        /// A group early in its world (0.7 group feats: the gate and the veil): Rowan with Edda, the Meadows and the Black Forest found, the Swamp not yet,
        /// no copper or tin held. Feats > Together then shows the Black Forest's feats (Copper Wed to Tin still under its riddle), not the Swamp's, and the
        /// quiet line that more wait; their dishes together earn The Hall Well Fed I. Edda's book comes through the snapshot as the game sends it (with the
        /// lands she found). Rowan's ledger is what EvaluateFeats notices on his PC.
        /// </summary>
        public static PanelInput EarlyGroup(DateTime now)
        {
            var installed = now.Date.AddDays(-4).AddHours(18);
            var edda = new PanelInput
            {
                PlayerName = "Edda", PlayerId = 77, IsSelf = true, NowUtc = now, InstalledUtc = installed, Events = new SessionEvents(), Log = new EventLog(), Session = new DamageTally(),
                Character = new Dictionary<string, float> { ["CraftFood"] = 70, ["CraftGrill"] = 12, ["CraftArmor"] = 3 },
                ItemsCrafted = new Dictionary<string, float> { ["$item_bread"] = 30, ["$item_helmet_leather"] = 1, ["$item_chest_leather"] = 1, ["$item_legs_leather"] = 1 },
                PiecesPlaced = new Dictionary<string, float> { ["$piece_workbench_ext1"] = 1, ["$piece_workbench_ext2"] = 1 },
                ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 640, ["$item_stone"] = 300, ["$item_coal"] = 120 },
                KnownBiomes = new List<string> { "Meadows", "BlackForest" }, Feats = new FeatsLedger { Primed = true },
            };
            SessionEvents.Add(edda.Events.Made, "$item_bread", 10); SessionEvents.Add(edda.Events.SmelterAdded, "charcoal_kiln|Wood", 40);
            var rowan = new PanelInput
            {
                PlayerName = "Rowan", PlayerId = 11, IsSelf = true, NowUtc = now, InstalledUtc = installed, ToLocal = t => t.AddHours(2),
                Events = new SessionEvents(), Log = new EventLog(), Session = new DamageTally(), PlayerNames = new Dictionary<long, string> { [77] = "Edda" },
                Character = new Dictionary<string, float> { ["CraftFood"] = 160, ["CraftGrill"] = 40, ["CraftArmor"] = 4 },
                ItemsCrafted = new Dictionary<string, float> { ["$item_fishwraps"] = 50, ["$item_cookedmeat"] = 40, ["$item_helmet_leather"] = 1, ["$item_shield_wood"] = 2, ["$item_axe_flint"] = 1 },
                PiecesPlaced = new Dictionary<string, float> { ["$piece_workbench_ext1"] = 1 },
                ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 1200, ["$item_stone"] = 500, ["$item_flint"] = 60, ["$item_coal"] = 260 },
                KnownBiomes = new List<string> { "Meadows", "BlackForest", "Ocean" }, Feats = new FeatsLedger(),
            };
            SessionEvents.Add(rowan.Events.SmelterAdded, "charcoal_kiln|Wood", 80);
            // Edda's book as Rowan's PC receives it: her snapshot, read back
            var stats = new PlayerProfile.PlayerStats[1]; var s = new PlayerProfile.PlayerStats(); stats[0] = s;
            foreach (var kv in edda.Character) s[(PlayerStatType)Enum.Parse(typeof(PlayerStatType), kv.Key)] = kv.Value;
            foreach (var kv in edda.ItemsCrafted) s.m_itemCraftStats[kv.Key] = kv.Value;
            foreach (var kv in edda.PiecesPlaced) s.m_piecesPlacedStats[kv.Key] = kv.Value;
            foreach (var kv in edda.ItemsPickedUp) s.m_itemPickupStats[kv.Key] = kv.Value;
            var copy = PanelInput.FromSnapshot(GroupShare.SharedCopy(Snapshot.Build("0.7.0", edda.PlayerId, edda.PlayerName, stats, new Snapshot.SkillInfo[0], "Midgard", edda.Session, "s1",
                                                                                    edda.Events, edda.Log, true, edda.Events, feats: edda.Feats, knownBiomes: edda.KnownBiomes)));
            copy.NowUtc = now; copy.ViewerName = "Rowan"; copy.ToLocal = rowan.ToLocal;
            rowan.Fellows = new List<PanelInput> { copy };
            copy.Fellows = new List<PanelInput> { rowan };
            PanelModel.EvaluateFeats(rowan, now.AddDays(-1), "BlackForest", null);   // the first look, yesterday: the group's dishes were already over the first line
            rowan.Feats.MarkSeen(PanelModel.IsGroupFeat);
            return rowan;
        }
    }
}
