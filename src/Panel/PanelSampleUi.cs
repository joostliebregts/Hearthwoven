using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Developer tool, off unless Dev.SampleData is true: the panel shows the fictional sample (PanelSample.Full: Rowan with
    /// Edda, Finch and Tor, every chapter filled) instead of the real player's data, for Thunderstore screenshots. The game's
    /// own lookups (names, icons, creature and item data) still come from the game, so the pages draw exactly as in play.
    /// Nothing is saved or shared meanwhile (SampleMode gates LocalTotals, the snapshot, the share state and the group
    /// request); the real data stays untouched. The footer says "Sample data" while it is on, so a screenshot is marked.
    /// Combines with Dev.PanelSnapshots: one F11 press photographs every page of the sample.
    /// Hooked into PanelUi in: BindConfig, Gather, Fellow, Render (fellows and switcher), Build and Fill (the footer tag);
    /// and PanelSnapshot.Model (fellows).
    /// </summary>
    public partial class PanelUi
    {
        internal static ConfigEntry<bool> SampleData;
        static SampleSource sample;
        TextMeshProUGUI sampleTag;

        static void BindSampleConfig(ConfigFile config)
        {
            SampleData = config.Bind("Dev", "SampleData", false, "Developer tool for screenshots: the panel shows a fictional sample (players Rowan, Edda, Finch and Tor, every page filled) instead of your own data. While on, nothing is saved or shared and your real data stays untouched; the footer says Sample data. Off for players.");
            SampleMode.Check = () => SampleData != null && SampleData.Value;
            SampleMode.Log = s => Debug.Log(s);
            SampleData.SettingChanged += (_, __) => sample = null;
        }

        /// <summary>The sample as yourself, built once per switch-on at the real clock, with the game's own lookups.</summary>
        static PanelInput SampleSelf() => Sample().Self;

        // the fellows live in the source's own list: Render empties self.Fellows before it fills it again
        static SampleSource Sample()
        {
            if (sample != null) return sample;
            var s = new SampleSource(DateTime.UtcNow);
            Lookups(s.Self);
            foreach (var f in s.Fellows) Lookups(f);
            Debug.Log("[Hearthwoven] Dev.SampleData: the panel shows the fictional sample (" + string.Join(", ", PanelSample.Players) + ")");
            return sample = s;
        }

        static void Lookups(PanelInput i)
        {
            var fallback = i.DisplayName;
            i.DisplayName = k => Localized(k) ?? fallback?.Invoke(k);
            i.ToLocal = null;   // the PC's own time zone, as for real data
            i.ItemKind = GameData.ItemKind; i.GatherKind = GameData.GatherKind; i.PieceKind = GameData.PieceKind; i.ItemColour = PanelLook.IconColour;
            i.Foe = BattleGame.Foe; i.Arrows = BattleGame.Arrows;
            i.CropOf = GameData.CropOf; i.ItemType = GameData.ItemType; i.StationDish = GameData.StationDish; i.MainMaterial = GameData.MainMaterial; var tab = i.PieceTab; var material = i.PieceMaterial; i.PieceTab = t => GameData.PieceTab(t) ?? tab?.Invoke(t); i.PieceMaterial = t => GameData.PieceMaterial(t) ?? material?.Invoke(t);   // the game does not know the sample's pieces: the sample's own tabs and materials stand in
        }

        static PanelInput SampleFellow(string name, PanelInput self) => Sample().Fellow(name, self);

        /// <summary>Everyone else whose stats the panel shows: the sample's fellows, or the real group when you share.</summary>
        List<PanelInput> FellowsOf(PanelInput self)
        {
            var names = GroupNames(self.PlayerName);
            return names.Select(n => Fellow(n, self)).Where(f => f != null).ToList();
        }

        // the real group by label (GroupShare.Fellows): a name shown twice comes back as "Rowan (2)"
        static IEnumerable<string> GroupNames(string self) =>
            SampleMode.On ? Sample().Names : GroupShare.Sharing() ? GroupShare.Fellows.Labelled(self).Select(kv => kv.Value) : Enumerable.Empty<string>();

        static bool SharingShown() => SampleMode.On || GroupShare.Sharing();

        // ---------- the footer tag ----------

        void BuildSampleTag()
        {
            sampleTag = Label(frame, "Sample data", 15, PanelLook.Faint, align: TextAlignmentOptions.MidlineRight, style: FontStyles.Italic);
            sampleTag.rectTransform.Bottom(-64, 22, 160, 30, right: true);   // inside the frame, in the footer row (B13: +64 put it outside, over the knot)
            sampleTag.textWrappingMode = TextWrappingModes.NoWrap;
            sampleTag.enabled = false;
        }

        // the key line gives the tag its room on the right while it shows
        void ShowSampleTag()
        {
            if (!sampleTag) return;
            sampleTag.enabled = SampleMode.On;
            if (keys) keys.rectTransform.sizeDelta = new Vector2(W - 128 - (sampleTag.enabled ? 176 : 0), 30);
        }
    }
}
