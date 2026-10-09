// Codex's last sprites (integrate-05, ADDENDUM-4): where they are wired, and that every "vocab:" picture any page asks for
// is a file in src/Panel/vocab/ with its kit-additions.json entry (embedded into the DLL), so no page asks for a missing one.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hearthwoven.Panel;

static class MarksTests
{
    public static int Run(PanelInput voyager, IEnumerable<(string name, PanelInput inp)> samples)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        PanelView Show(Chapter c, string page) => PanelModel.Build(voyager, new PanelState { Chapter = c, Page = { [c] = page } });
        Block First(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);

        var air = First(Show(Chapter.Voyages, "onfoot"), "rows");
        Check(air.Items.Single(r => r.Title.StartsWith("In the air")).Icon == "vocab:move-air" && string.IsNullOrEmpty(air.Items.Single(r => r.Title.StartsWith("Jumps")).Icon),
              "marks: On foot, in the air carries Codex's move-air");
        var found = First(Show(Chapter.Voyages, "maps"), "rows");
        Check(found.Items.Where(r => r.Title.Contains("treasure") || r.Title.Contains("ungeon") || r.Title.Contains("ortal")).All(r => r.Icon != null && r.Icon.StartsWith("vocab:find-") || r.Icon == "vocab:portal-mark") &&
              found.Items.Any(r => r.Icon == "vocab:find-buried"), "marks: Maps, what you found carries the find and portal marks");

        var maps = Show(Chapter.Voyages, "maps");
        var head = PanelModel.Content(maps).FirstOrDefault(b => b.Kind == "section" && b.Title == "Biomes found");
        Check(head != null && head.Value == PanelModel.BiomesOf(First(maps, "biometiles").Items.Count) && PanelModel.BiomesOf(6) == "6 of 9",
              "maps: Biomes found says how many of the game's nine (from the found-biomes record)");

        // every vocab picture any page asks for is shipped
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "src", "Panel", "vocab"))) dir = Path.GetDirectoryName(dir);
        Check(dir != null, "marks: found src/Panel/vocab from the test's folder");
        if (dir == null) return fails;
        var vocab = Path.Combine(dir, "src", "Panel", "vocab");
        var manifest = File.ReadAllText(Path.Combine(vocab, "kit-additions.json"));
        IEnumerable<string> Refs(Block b) => new[] { b.Icon, b.Pattern }.Concat((b.Items ?? new List<Block>()).SelectMany(Refs));
        var asked = PanelTextTests.Pages(samples).SelectMany(p => p.v.Blocks.SelectMany(Refs)).Where(r => r != null && r.StartsWith("vocab:")).Select(r => r.Substring(6)).Distinct().ToList();
        var missing = asked.Where(n => !File.Exists(Path.Combine(vocab, n + ".png")) || !manifest.Contains("\"" + n + ".png\"")).ToList();
        Check(asked.Count > 0 && missing.Count == 0, "marks: every vocab picture a page asks for (" + asked.Count + ") is in src/Panel/vocab with its manifest entry" + (missing.Count > 0 ? ": missing " + string.Join(", ", missing) : ""));
        var addendum4 = new[] { "tame-tamed", "tame-petted", "tame-command", "move-air", "find-buried", "find-dungeon", "find-location", "portal-mark", "ground-lower", "boss-mark" };
        Check(addendum4.All(n => File.Exists(Path.Combine(vocab, n + ".png")) && manifest.Contains("\"" + n + ".png\"")), "marks: Codex's ten last sprites are copied and registered");
        return fails;
    }
}
