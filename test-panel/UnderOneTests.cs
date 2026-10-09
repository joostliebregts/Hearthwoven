// 0.6.5: an amount above 0 but below 1 says "under 1" (or "under 1 %"), never "0", which reads as nothing happened:
// a falling tree that did 0.4 damage, and a dodge that is 0.3 % of the practice.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven.Panel;

static class UnderOneTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var now = new DateTime(2026, 10, 7, 22, 30, 0, DateTimeKind.Utc);
        var rows = new List<PanelModel.DamageRow> { new PanelModel.DamageRow { Bucket = now, Biome = "Meadows", Dir = "taken", Other = "FallingTree", Cause = "Tree", Type = "blunt", Amount = 0.4f } };
        var sources = PanelModel.ReceivedSources(PanelSample.Evening(now), rows);
        var tree = sources?.Items?.FirstOrDefault();
        Check(tree != null && tree.Value == "under 1", "under 1: a source that took 0.4 damage shows \"under 1\", not \"0\"");
        Check(PanelModel.Share(0.3, 100) == "under 1 %" && PanelModel.Share(0, 100) == "0 %" && PanelModel.Share(42.4, 100) == "42 %" && PanelModel.NAtLeast(3) == "3",
              "under 1: a share of 0.3 of 100 shows \"under 1 %\"; zero and whole shares and amounts do not change");
        return fails;
    }
}
