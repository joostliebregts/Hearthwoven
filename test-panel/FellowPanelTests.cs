// Fellow identity on the panel (RESILIENCE-06 item 8, E1): two fellows with one name both get a chip and their own colour,
// and singleplayer shows one clear line where the fellow players would be. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FellowPanelTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // the labels as PanelUi gets them from GroupShare.Fellows: you are Rowan, two fellows are also called Rowan
        var ids = new FellowIds();
        string Copy(string name, long id, string platform) => FellowIds.WithPlatform(GroupShare.SharedCopy(Snapshot.Build("0.6.0", id, name, new PlayerProfile.PlayerStats[0],
            new Snapshot.SkillInfo[0], "w", new DamageTally(), "s", null, null, true)), platform);
        var kA = ids.Receive(Copy("Rowan", 2, "76561198000000002")); var kB = ids.Receive(Copy("Rowan", 3, "Xbox_2535400000000003")); ids.Receive(Copy("Edda", 4, "76561198000000004"));
        var labels = ids.Labelled("Rowan").Select(kv => kv.Value).ToList();
        var v = new PanelView();
        PanelModel.AddPlayers(v, "Rowan", labels, "Rowan (3)", true);
        Check(v.Players.Select(p => p.Label).SequenceEqual(new[] { "Rowan", "Edda", "Rowan (2)", "Rowan (3)" }) && v.Players.Single(p => p.Selected).Id == "Rowan (3)" &&
              ids.KeyOfLabel("Rowan (3)", "Rowan") == kB && ids.KeyOfLabel("Rowan (2)", "Rowan") == kA,
              "same name: you and both fellow Rowans each get a chip (Rowan (2), Rowan (3)); the chosen chip leads to that one person's copy");
        Check(v.PersonColors.TryGetValue("Rowan", out var c0) && v.PersonColors.TryGetValue("Rowan (2)", out var c2) && v.PersonColors.TryGetValue("Rowan (3)", out var c3) &&
              new[] { c0, c2, c3 }.Distinct().Count() == 3, "same name: three people called Rowan, three colours");

        // a fellow called like you is no longer hidden: its label differs from your name (PanelModel's own-name filter)
        var you = new PanelInput { PlayerName = "Rowan", IsSelf = true, Events = new SessionEvents(), ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 40 }, GatherKind = _ => "wood" };
        var twin = PanelInput.FromSnapshot(Copy("Rowan", 2, "76561198000000002")); twin.PlayerName = "Rowan (2)"; twin.GatherKind = _ => "wood"; twin.ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 5 }; twin.Events.PickedUp["$item_wood"] = 5;
        you.Fellows = new List<PanelInput> { twin };
        var together = PanelModel.Build(you, new PanelState { Chapter = Chapter.Company, Page = { [Chapter.Company] = "together" } });
        var ids2 = PanelModel.Content(together).SelectMany(b => new[] { b }.Concat(b.Items ?? new List<Block>())).SelectMany(b => new[] { b }.Concat(b.Items ?? new List<Block>())).Select(b => b.Id).ToList();
        Check(ids2.Contains("Rowan (2)") && ids2.Contains("Rowan"), "same name: Together shows you and the other Rowan side by side");

        // singleplayer: one line where the fellow players would be, whatever the sharing setting
        foreach (var sharing in new[] { true, false })
        {
            var solo = new PanelView();
            PanelModel.AddPlayers(solo, "Rowan", new string[0], "", sharing, solo: true);
            Check(solo.Players.Count == 0 && solo.ShareNote == PanelModel.SoloNote, "solo: singleplayer says once that fellow players appear on a server or hosted world (sharing " + (sharing ? "on" : "off") + ")");
        }
        var soloInput = new PanelInput { PlayerName = "Rowan", IsSelf = true, Solo = true, ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 40 }, GatherKind = _ => "wood", Events = new SessionEvents() };
        var soloView = PanelModel.Build(soloInput, new PanelState { Chapter = Chapter.Company, Page = { [Chapter.Company] = "together" } });
        PanelModel.AddPlayers(soloView, "Rowan", new string[0], "", true, solo: true);
        var notes = PanelModel.Content(soloView).Where(b => b.Kind == "note").Select(b => b.Text).ToList();
        Check(notes.Count(t => t == PanelModel.SoloNote) == 1 && !notes.Contains(PanelModel.TogetherAlone), "solo: Together carries the one solo line, not \"once they share too\"");
        var hosted = new PanelView();
        PanelModel.AddPlayers(hosted, "Rowan", new string[0], "", true);
        Check(hosted.ShareNote == PanelModel.ShareWaitingNote, "solo: a hosted or dedicated world with nobody else yet keeps the waiting line");
        return fails;
    }
}
