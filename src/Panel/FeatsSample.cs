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
                theirs.BestsShared = true;   // they run this version: it shares the bests behind Heavy Keel and Long Lead (an empty "featBests" when none yet)
                f.Feats = theirs;
            }
        }
    }
}
