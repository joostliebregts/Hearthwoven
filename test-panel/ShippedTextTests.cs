using System.IO;
using System.Linq;

// 0.9.8 (Joost: "why is my name in the changelog?"): the texts that ship to players (the CHANGELOG in the zip and on GitHub, the
// Thunderstore README) name no person: test builds read "test build, not released", design calls carry no name.
static class ShippedTextTests
{
    public static int Run()
    {
        var root = System.AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "src", "Plugin.cs"))) root = Path.GetDirectoryName(root);
        if (root == null) { System.Console.WriteLine("FAIL shipped text: src not found"); return 1; }
        int fails = 0;
        foreach (var rel in new[] { "CHANGELOG.md", Path.Combine("thunderstore", "README.md"), Path.Combine("thunderstore", "old-name", "README.md") })
        {
            var f = Path.Combine(root, rel); if (!File.Exists(f)) continue;
            var hits = File.ReadAllLines(f).Select((l, i) => (l, i)).Where(x => x.l.Contains("Joost")).ToList();
            if (hits.Count > 0) { fails++; System.Console.WriteLine("FAIL shipped text: " + rel + " names a person on line " + (hits[0].i + 1)); }
        }
        if (fails == 0) System.Console.WriteLine("PASS shipped text: no person named in the CHANGELOG or the Thunderstore READMEs");
        return fails;
    }
}
