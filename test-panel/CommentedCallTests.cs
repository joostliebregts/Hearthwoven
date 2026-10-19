using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// 0.9.8: a drawing call that ended up inside a trailing comment (MealPlanUi.PlanMark's dot.rectTransform.Box) left a gold mark at its default
// size, a large block over the Meal plan tiles in game; the HTML mirror draws its own and stayed right. This check fails on any Panel UI line
// whose trailing comment holds a positioning call.
static class CommentedCallTests
{
    public static int Run()
    {
        int fails = 0;
        var root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "src", "Plugin.cs"))) root = Path.GetDirectoryName(root);
        if (root == null) { System.Console.WriteLine("FAIL commented call: src not found"); return 1; }
        var srcDir = Path.Combine(root, "src", "Panel");
        var call = new Regex(@"//.*\w+\.(rectTransform\.)?(Box|Stretch|Inside)\s*\(\s*[\w.+\-*/ ]+,", RegexOptions.Compiled);
        foreach (var f in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            var n = 0;
            foreach (var line in File.ReadLines(f))
            {
                n++;
                var i = line.IndexOf("//", StringComparison.Ordinal);
                if (i < 0 || line.TrimStart().StartsWith("//") || line.TrimStart().StartsWith("///")) continue;
                if (call.IsMatch(line.Substring(i)))
                {
                    System.Console.WriteLine("FAIL commented call: " + Path.GetFileName(f) + ":" + n + " a drawing call sits inside a trailing comment");
                    fails++;
                }
            }
        }
        if (fails == 0) System.Console.WriteLine("PASS commented call: no positioning call hidden in a trailing comment");
        return fails;
    }
}
