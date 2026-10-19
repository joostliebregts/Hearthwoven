// 0.9.1 a drawn picture for every feat (Joost: "creative designs for the icons ... for all of these"): every feat id has its scene's SVG source,
// its sprite (112 px) with its manifest entry, the cards and the detail carry it (a veiled feat its land's emblem), and the mirror draws it
// (panel-preview.html's FEATSCENE, multiplied by the gold). 0.9.2: look 1 gone, its small 72 px sprites are not shipped any more (none left, no
// manifest entry, render.mjs draws none). Program.cs sets PanelModel.SceneExists from the files first.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hearthwoven.Panel;

static class FeatScenesTests
{
    /// <summary>The mod's src/Panel folder, found from the test's folder (null when not there).</summary>
    public static string PanelDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "src", "Panel", "vocab"))) dir = Path.GetDirectoryName(dir);
        return dir == null ? null : Path.Combine(dir, "src", "Panel");
    }

    /// <summary>The game's lookup (PanelUi: the manifest's names), from the shipped files: a sprite exists when its PNG and its manifest entry do.</summary>
    public static Func<string, bool> FromFiles()
    {
        var panel = PanelDir(); if (panel == null) return null;
        var vocab = Path.Combine(panel, "vocab"); var manifest = File.ReadAllText(Path.Combine(vocab, "kit-additions.json"));
        return name => File.Exists(Path.Combine(vocab, name + ".png")) && manifest.Contains("\"" + name + ".png\"");
    }

    static (int w, int h) PngSize(string path)
    {
        var b = File.ReadAllBytes(path);
        int Be(int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
        return b.Length > 24 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G' ? (Be(16), Be(20)) : (0, 0);
    }

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var panel = PanelDir();
        Check(panel != null, "scenes: found src/Panel from the test's folder");
        if (panel == null) return fails;
        var vocab = Path.Combine(panel, "vocab"); var src = Path.Combine(panel, "vocab-src", "feats");
        var manifest = File.ReadAllText(Path.Combine(vocab, "kit-additions.json"));
        var ids = PanelModel.FeatDefs.Select(d => d.Id).ToList();

        // every feat: its source, its sprite at its size, its manifest entry; no small sprite left (0.9.2)
        var noSvg = ids.Where(i => !File.Exists(Path.Combine(src, i + ".svg"))).ToList();
        Check(noSvg.Count == 0 && File.Exists(Path.Combine(src, "render.mjs")), "scenes: every feat (" + ids.Count + ") has its drawing in src/Panel/vocab-src/feats, with render.mjs" + (noSvg.Count > 0 ? ": missing " + string.Join(", ", noSvg) : ""));
        var bad = new List<string>();
        foreach (var i in ids)
        {
            var name = PanelModel.FeatSceneSprite + i; var file = Path.Combine(vocab, name + ".png");
            if (!File.Exists(file) || PngSize(file) != (112, 112) || !manifest.Contains("\"" + name + ".png\"")) bad.Add(name);
        }
        Check(bad.Count == 0, "scenes: every feat has feat-scene-<id>.png (112 px) with its kit-additions.json entry" + (bad.Count > 0 ? ": " + string.Join(", ", bad.Take(8)) : ""));
        var orphans = Directory.GetFiles(vocab, PanelModel.FeatSceneSprite + "*.png").Select(Path.GetFileNameWithoutExtension)
            .Where(n => !ids.Any(i => n == PanelModel.FeatSceneSprite + i)).ToList();
        Check(orphans.Count == 0, "scenes: no scene without its feat, and no small (look 1) render shipped" + (orphans.Count > 0 ? ": " + string.Join(", ", orphans.Take(8)) : ""));
        Check(!manifest.Contains("-small.png\"") && !File.ReadAllText(Path.Combine(src, "render.mjs")).Contains("-small.png"),
              "scenes: the manifest has no small scene's entry and render.mjs draws none (0.9.2)");
        var svgs = ids.Where(i => File.Exists(Path.Combine(src, i + ".svg"))).Select(i => File.ReadAllText(Path.Combine(src, i + ".svg"))).ToList();
        Check(svgs.All(s => s.Contains("viewBox=\"0 0 72 72\"") && s.Contains("stroke=\"#fff\"") && !s.Contains("<text") && !s.Contains("\r")) && svgs.Distinct().Count() == svgs.Count,
              "scenes: each drawing a white line on the 72 grid, no text, LF only, no two alike");

        // the cards and the detail carry the feat's own scene; a veiled feat keeps its emblem
        var (full, _) = FeatsTests.World();
        var has = PanelModel.SceneExists ?? FromFiles();
        var keep = PanelModel.SceneExists; PanelModel.SceneExists = has;
        try
        {
            foreach (var page in new[] { PanelModel.FeatsPageId, PanelModel.FeatsUnsungId, PanelModel.FeatsTogetherId })
                {
                    var s = new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = page } };
                    var v = PanelModel.Build(full, s);
                    var cards = PanelModel.Content(v).Where(b => b.Kind == "feats").SelectMany(b => b.Items ?? new List<Block>()).Where(c => PanelModel.FeatById(c.Id) != null).ToList();
                    string Want(Block c) => "vocab:" + PanelModel.FeatSceneSprite + c.Id;
                    var veiled = cards.Where(c => PanelModel.FeatVeiled(PanelModel.FeatById(c.Id), full)).ToList();
                    var open = cards.Except(veiled).ToList();
                    var wrong = open.Where(c => c.Icon != Want(c) || (c.Items?.FirstOrDefault() is Block dt && dt.Icon != Want(c))).Select(c => c.Id).ToList();
                    Check(open.Count > 0 && wrong.Count == 0 && veiled.All(c => c.Icon == null || !c.Icon.Contains(PanelModel.FeatSceneSprite)),
                          "scenes: " + page + ": every card (" + open.Count + ") and its detail show its feat's own scene; veiled ones (" + veiled.Count + ") keep their emblem" + (wrong.Count > 0 ? ": wrong " + string.Join(", ", wrong.Take(6)) : ""));
                    if (page == PanelModel.FeatsPageId)
                        Check(PanelModel.ToJson(v).Contains("\"vocab:" + PanelModel.FeatSceneSprite), "mirror: the page's JSON carries the scenes for the preview");
                }
            Check(PanelModel.FeatScene("drover", "vocab:lead-rope", null) == "vocab:lead-rope" && PanelModel.FeatScene("nosuchfeat", "title:smith", has) == "title:smith",
                  "scenes: without a drawn scene a card keeps its emblem");
        }
        finally { PanelModel.SceneExists = keep; }
        var lookSrc = File.ReadAllText(Path.Combine(panel, "PanelLook.cs"));
        Check(Regex.IsMatch(lookSrc, @"bool GoldLine\(string icon\)[^;]*StartsWith\(""vocab:"" \+ PanelModel\.FeatSceneSprite"), "scenes: the game multiplies them by the feat gold (PanelLook.GoldLine takes every vocab:feat-scene-)");

        // the mirror: panel-preview.html's FEATSCENE takes every scene and multiplies it by the gold
        var html = File.ReadAllText(Path.Combine(panel, "preview", "panel-preview.html"));
        var m = Regex.Match(html, @"const FEATSCENE = /(.+?)/;");
        var rx = m.Success ? new Regex(m.Groups[1].Value) : null;
        Check(rx != null && ids.All(i => rx.IsMatch("vocab:feat-scene-" + i)) && !rx.IsMatch("vocab:feat-waymate") &&
              Regex.IsMatch(html, @"FEATSCENE\.test\(icon\)\) return [^\n]*filter:url\(#goldmul\)") && html.Contains("<filter id=\"goldmul\""),
              "mirror: panel-preview.html draws every feat's scene (FEATSCENE) multiplied by the gold, from ../vocab/");
        return fails;
    }
}
