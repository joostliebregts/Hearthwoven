using System;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Notices feats on the player's own PC: against their numbers now (the character's record, what Hearthwoven counted, and fellow
    /// players' copies for the derived feats) every tier reached and not yet in the ledger is recorded with its date and biome
    /// (PanelModel.EvaluateFeats). Runs about once a minute from Plugin.Update, and every render of the open panel, so a feat is
    /// there the moment the panel opens. The ledger is saved with the local totals and travels in the next snapshot. Dev.SampleData
    /// never writes to the real ledger.
    /// </summary>
    internal static class FeatsTracker
    {
        const float EverySeconds = 60f;
        static float next;

        /// <summary>From Plugin.Update, with the clock it keeps.</summary>
        internal static void Tick(float now)
        {
            if (now < next) return;
            next = now + EverySeconds;
            try
            {
                if (SampleMode.On || Plugin.FeatsLedger == null) return;
                var self = PanelUi.GatherSelf();
                self.Fellows = PanelUi.FellowsNow(self);
                Note(self);
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] feats tick: " + e.Message); }
        }

        /// <summary>Notes the feats of the player's own PanelInput (its Feats is the live ledger); true when something was earned just now.</summary>
        internal static bool Note(PanelInput self)
        {
            if (self == null || !self.IsSelf || self.Feats == null || SampleMode.On) return false;
            var fresh = PanelModel.EvaluateFeats(self, DateTime.UtcNow, ClientHooks.Biome(), FeatsHooks.Place());
            foreach (var f in fresh) Debug.Log("[Hearthwoven] feat earned: " + f.feat.Name + (f.feat.Tiers.Length > 1 ? " " + PanelModel.Numeral(f.tier) : ""));
            return fresh.Count > 0;
        }
    }
}
