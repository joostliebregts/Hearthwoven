using System;
using System.Collections.Generic;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// SAMPLE data (fictional Rowan and Finch) for the animals led (Deeds > Taming) and for the bests behind Heavy Keel and Long Lead
    /// (Voyages > Sailing, Deeds > Taming). The led metres are what LedTracker would have summed into SessionEvents.LedMeters; the bests
    /// are what the hooks note in the feats ledger. Values are assigned, never added, so calling twice changes nothing.
    /// </summary>
    public static class LedSample
    {
        static readonly Dictionary<string, string> Named = new Dictionary<string, string> { ["Wolf"] = "Wolf", ["Boar"] = "Boar", ["Lox"] = "Lox" };
        static void Register() { foreach (var kv in Named) if (!PanelSample.Names.ContainsKey(kv.Key)) PanelSample.Names[kv.Key] = kv.Value; }

        /// <summary>The metres tamed animals followed the sample player: Rowan walked a wolf and a boar home and sailed a lox; Finch drove his boars.</summary>
        public static void Led(SessionEvents ev, string player)
        {
            Register();
            switch (player)
            {
                case "Rowan": ev.LedMeters["Wolf"] = 3900f; ev.LedMeters["Boar"] = 2100f; ev.LedMeters["Lox"] = 800f; break;
                case "Finch": ev.LedMeters["Boar"] = 3100f; break;
            }
        }

        /// <summary>The notes in the feats ledger: Rowan's heaviest voyage (140 iron scrap and bars aboard over 2 km) and longest lead (2.3 km, a wolf).</summary>
        public static void Bests(FeatsLedger l, string player, DateTime now)
        {
            if (l == null) return;
            switch (player)
            {
                case "Rowan":
                    l.NoteBest(CargoVoyage.BestKey, 140, now.AddDays(-3), "Ocean");
                    l.NoteBest(LedTracker.BestKey, 2300, now.AddDays(-5), "Meadows", "Wolf");
                    break;
                case "Finch": l.NoteBest(LedTracker.BestKey, 2400, now.AddDays(-5), "Meadows", "Boar"); break;
            }
        }
    }
}
