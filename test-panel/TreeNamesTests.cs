// Woodcutting's lists per tree (0.8.2, the same class as Mining's rocks): a tree kind the book has no word for (the Ashlands' Scorched
// Tree, a mod's tree) read as its prefab ("Ashlands Tree"), and its logs and log halves ("AshlandsTreeLog1", "AshlandsTreeLogHalf1") were
// rows of their own. Now they fold into their tree by the game data (TreeBase.m_logPrefab, TreeLog.m_subLogPrefab), named as the game
// names the tree. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class TreeNamesTests
{
    // as GameData.Trees reads the Ashlands' trees: every Scorched Tree drops AshlandsTreeLog1, which splits into AshlandsTreeLogHalf1;
    // the trees carry the hover text $prop_ashlandstree, the logs $prop_treelog ("Log")
    static readonly Dictionary<string, string> TreeOf = new Dictionary<string, string>
    {
        ["AshlandsTree1"] = "AshlandsTree1", ["AshlandsTree3"] = "AshlandsTree3", ["AshlandsTree6_big"] = "AshlandsTree6_big",
        ["AshlandsTreeLog1"] = "AshlandsTree1", ["AshlandsTreeLogHalf1"] = "AshlandsTree1",
    };
    static string Name(string p) => p.StartsWith("AshlandsTreeLog") ? "Log" : p.StartsWith("AshlandsTree") ? "Scorched Tree" : null;

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelInput Chopped(bool game)
        {
            var i = new PanelInput { IsSelf = true, PlayerName = "Tor", Events = new SessionEvents(), Character = new Dictionary<string, float>() };
            foreach (var (k, v) in new[] { ("AshlandsTree1", 6f), ("AshlandsTree3", 2f), ("AshlandsTree6_big", 4f), ("AshlandsTreeLog1", 5f), ("AshlandsTreeLogHalf1", 9f), ("Beech1", 3f) })
                SessionEvents.Add(i.Events.ChopHits, k, v);
            if (game) { i.DisplayName = Name; i.TreeOf = p => TreeOf.TryGetValue(p, out var t) ? t : null; }
            return i;
        }
        var hits = PanelModel.TreeHits(Chopped(true));
        Check(hits.Count == 2 && hits.TryGetValue("Scorched Tree", out var s) && s == 26 && hits["Beech"] == 3,
              "trees: a tree the book has no word for is named as the game names it (Scorched Tree), its kinds, logs and log halves one row (26)");
        var bare = PanelModel.TreeHits(Chopped(false));
        Check(bare.TryGetValue("Ashlands Tree", out var a) && a == 22 && bare["Beech"] == 3,
              "trees: without game data a log or log half without an underscore (AshlandsTreeLogHalf1) still folds into its tree by name");
        return fails;
    }
}
