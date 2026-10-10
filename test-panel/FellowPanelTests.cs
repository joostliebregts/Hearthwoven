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

        // 0.7 item 1: names right away. Tor was there when you arrived; Ylva joins at 20:00:00 and their copy comes at :25; Sam joins and never shares
        var t0 = new DateTime(2026, 10, 9, 20, 0, 0, DateTimeKind.Utc);
        var watch = new JoinWatch();
        watch.Update(new[] { "Tor" }, false, t0.AddSeconds(-30));                       // the list before you spawned: not this world's yet
        watch.Update(new[] { "Tor" }, true, t0.AddSeconds(-20));                        // you are in: whoever is here did not just join
        watch.Update(new[] { "Tor", "Ylva" }, true, t0);
        watch.Update(new[] { "Tor", "Ylva", "Sam" }, true, t0.AddSeconds(5));
        var early = watch.Joining(new string[0], "Rowan", t0.AddSeconds(10));
        var joinView = new PanelView();
        PanelModel.AddPlayers(joinView, "Rowan", new[] { "Tor" }, "", true, joining: early);
        var ylva = joinView.Players.FirstOrDefault(p => p.Label == "Ylva");
        var arrived = watch.Joining(new[] { "Ylva" }, "Rowan", t0.AddSeconds(25));
        var later = watch.Joining(new string[0], "Rowan", t0.AddSeconds(5 + JoinWatch.Window));
        // Sam (never shares) dies at +70 s: off the list 10 s, back: no new arrival; Sam leaves for good, comes back after 40 s: a new arrival
        watch.Update(new[] { "Tor", "Ylva", "Sam" }, true, t0.AddSeconds(68));             // the list comes every 2 s
        watch.Update(new[] { "Tor", "Ylva" }, true, t0.AddSeconds(70));
        var whileDead = watch.Joining(new[] { "Ylva" }, "Rowan", t0.AddSeconds(72));
        watch.Update(new[] { "Tor", "Ylva", "Sam" }, true, t0.AddSeconds(80));
        var respawned = watch.Joining(new[] { "Ylva" }, "Rowan", t0.AddSeconds(80));
        watch.Update(new[] { "Tor", "Ylva" }, true, t0.AddSeconds(90)); watch.Update(new[] { "Tor", "Ylva" }, true, t0.AddSeconds(125));
        watch.Update(new[] { "Tor", "Ylva", "Sam" }, true, t0.AddSeconds(130));
        var returned = watch.Joining(new[] { "Ylva" }, "Rowan", t0.AddSeconds(131));
        var arrivedView = new PanelView();
        PanelModel.AddPlayers(arrivedView, "Rowan", new[] { "Tor", "Ylva" }, "", true, joining: arrived);
        Check(early.SequenceEqual(new[] { "Ylva", "Sam" }) && ylva != null && ylva.Disabled && !ylva.Selected && joinView.Players.Last().Label == "Sam" &&
              joinView.ShareNote == null && joinView.PersonColors.ContainsKey("Ylva") &&
              arrived.SequenceEqual(new[] { "Sam" }) && arrivedView.Players.Single(p => p.Label == "Ylva").Disabled == false &&
              later.Count == 0 && AloneJoin().ShareNote == PanelModel.JoiningNote && whileDead.Count == 0 && respawned.Count == 0 && returned.SequenceEqual(new[] { "Sam" }),
              "joining: a fellow who just joined shows at once as a dimmed chip without a book (\"" + PanelModel.JoinedLine + "\"; alone with them the header adds \"" + PanelModel.JoiningNote + "\"); whoever was there before you is not joining; " +
              "once the copy is here it is the normal chip; nobody stays \"just joined\" past " + JoinWatch.Window + " s (a player who never shares drops out); " +
              "a death and respawn is no new arrival, leaving for over " + JoinWatch.Absence + " s and coming back is");
        return fails;
    }

    static PanelView AloneJoin() { var v = new PanelView(); PanelModel.AddPlayers(v, "Rowan", new string[0], "", true, joining: new[] { "Ylva" }); return v; }
}
