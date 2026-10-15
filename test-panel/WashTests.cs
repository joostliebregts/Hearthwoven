// 0.8.5 the washes (src/Panel/PanelWash.cs). The game draws the book in linear colour space, the mirror in sRGB, so a translucent colour
// copied from the mirror draws another colour in game: the damage rows' track, rgba(255,240,210,.05), showed (67, 63, 54) on an island where the
// mirror shows (32, 29, 25) (Joost, 0.8.3: the loud band under every bar). One check for the class: every wash in PanelWash.All is its twin's
// colour in preview/panel-preview.html and draws in game what the mirror draws on its ground, the colours drawn per row (a player's, a part's)
// the same through PanelWash.Toward, and no drawing file writes out a translucent colour that draws more than Levels off on the page or an
// island (a wash goes through PanelWash; what stays is listed below with its reason). The game's side as Unity draws it: the colour as bytes
// (a UI vertex colour is a Color32), turned linear, mixed with the ground, turned back to sRGB.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hearthwoven.Panel;
using UnityEngine;

static class WashTests
{
    const int Levels = 2;

    static double Lin(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    static double Srgb(double v) => v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
    static double Byte(float v) => Math.Round(Math.Clamp(v, 0f, 1f) * 255) / 255;

    /// <summary>What the game draws: <paramref name="c"/> over <paramref name="ground"/>, 0..255 per channel.</summary>
    static int[] Game(Color c, Color ground)
    {
        var a = Byte(c.a);
        return Enumerable.Range(0, 3).Select(i => (int)Math.Round(255 * Srgb(a * Lin(Byte(c[i])) + (1 - a) * Lin(Byte(ground[i]))))).ToArray();
    }

    /// <summary>What the mirror draws: <paramref name="c"/> over <paramref name="ground"/>, mixed as they are.</summary>
    static int[] Mirror(Color c, Color ground) =>
        Enumerable.Range(0, 3).Select(i => (int)Math.Round(255 * (c.a * Byte(c[i]) + (1 - c.a) * Byte(ground[i])))).ToArray();

    static int Off(int[] a, int[] b) => Enumerable.Range(0, 3).Max(i => Math.Abs(a[i] - b[i]));
    static string Rgb(int[] v) => "(" + string.Join(", ", v) + ")";
    static Color With(Color c, float a) { c.a = a; return c; }
    static Color Hex(string h) => new Color(Convert.ToInt32(h.Substring(1, 2), 16) / 255f, Convert.ToInt32(h.Substring(3, 2), 16) / 255f, Convert.ToInt32(h.Substring(5, 2), 16) / 255f, 1f);

    /// <summary>Translucent colours a drawing file writes out that are no wash: each must still be found (a stale entry fails), each with its reason.</summary>
    static readonly (string file, string colour, string why)[] NotWashes =
    {
        ("PanelUi.cs", "new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.9f)", "the focus ring's tint: the ring sprite's own alpha draws its glow"),
        ("PanelUi.cs", "new Color(PanelLook.Gold.r, PanelLook.Gold.g, PanelLook.Gold.b, 0.6f)", "a key cap's letter: text, dimmed (the mirror's .kc color); a ground is a wash, dimmed text is Joost's call"),
        ("Chapters/FeatsUi.cs", "new Color(0.62f, 0.6f, 0.58f, 0.55f)", "a feat's emblem not earned: an icon, dimmed"),
        ("Chapters/FeatsUi.cs", "new Color(0.62f, 0.6f, 0.58f, 0.28f)", "a feat's emblem not counted yet: an icon, dimmed"),
    };

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var src = AppContext.BaseDirectory;
        while (src != null && !File.Exists(Path.Combine(src, "src", "Plugin.cs"))) src = Path.GetDirectoryName(src);
        var panel = Path.Combine(src, "src", "Panel");
        var html = File.ReadAllLines(Path.Combine(panel, "preview", "panel-preview.html"));

        var off = new List<string>();
        foreach (var w in PanelWash.All)
        {
            var name = typeof(PanelWash).GetFields().First(f => f.GetValue(null) == (object)w).Name;
            if (!html.Any(l => l.Contains(w.Twin) && l.Contains(w.Css))) off.Add(name + ": no line of the mirror holds " + w.Twin + " with " + w.Css);
            var game = Game(w.Game, w.Ground); var mirror = Mirror(w.Mirror, w.Ground);
            if (Off(game, mirror) > Levels) off.Add(name + " " + w.Css + " on " + Rgb(Mirror(w.Ground, w.Ground)) + ": the mirror " + Rgb(mirror) + ", the game " + Rgb(game));
        }
        // drawn per row: a part's veil (BarFormUi), a player's book chip (PanelUi.BookChip), the period before's trace (CompareUi), a crop's faded part
        // (CropsUi), a thread (CompanyUi), a voyage's ticks and crests (VoyagesHallUi), each with a sample colour and its ground
        var veil = PanelWash.Rgba("rgba(9,7,5,.45)");
        var drawn = new (string what, Color mirror, Color ground)[]
        {
            ("veil on Slash", veil, Hex("#b7c4d6")), ("veil on Pierce", veil, Hex("#ece4c8")), ("veil on Blunt", veil, Hex("#6e7480")), ("veil on Poison", veil, Hex("#3ccf6e")),
            ("Edda's book chip", new Color(0.71f, 0.29f, 0.33f, 0.22f), PanelWash.Head), ("the trace of Wood", new Color(0.56f, 0.33f, 0.2f, 0.55f), PanelWash.Island),
            ("a faded crop", new Color(0.55f, 0.75f, 0.35f, 0.55f), Hex("#2b2117")), ("a faint gear thread", With(Hex("#b8bcc4"), 0.45f), PanelWash.Island),
            ("a food thread", With(Hex("#e8a948"), 0.92f), PanelWash.Island), ("a foot path's ticks", PanelWash.Rgba("rgba(242,227,181,.85)"), Hex("#8a7a52")),
            ("the sea's crests", PanelWash.Rgba("rgba(219,237,255,.8)"), Hex("#3f6f9a")),
        };
        foreach (var (what, mirror, ground) in drawn)
        {
            var game = Game(PanelWash.Toward(mirror, ground), ground); var look = Mirror(mirror, ground);
            if (Off(game, look) > Levels) off.Add(what + ": the mirror " + Rgb(look) + ", the game " + Rgb(game));
        }

        // a translucent colour written out in a drawing file: its game look against the mirror's on the page and on an island
        var named = Regex.Matches(File.ReadAllText(Path.Combine(panel, "PanelLook.cs")), @"\b(\w+) = Hex\(""(#[0-9a-fA-F]{6})""\)").Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => Hex(g.First().Groups[2].Value));
        var written = new Regex(@"new Color\(\s*(?:(?<r>\d*\.?\d+)f?\s*,\s*(?<g>\d*\.?\d+)f?\s*,\s*(?<b>\d*\.?\d+)f?|PanelLook\.(?<n>\w+)\.r\s*,\s*PanelLook\.\k<n>\.g\s*,\s*PanelLook\.\k<n>\.b)\s*,\s*(?<a>\d*\.?\d+)f?\s*\)");
        var loud = new List<string>(); var kept = new HashSet<(string, string)>();
        foreach (var f in Directory.GetFiles(panel, "*.cs", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != "PanelWash.cs").OrderBy(f => f, StringComparer.Ordinal))
        {
            var file = Path.GetRelativePath(panel, f).Replace('\\', '/');
            var code = LayoutTests.Code(File.ReadAllText(f));
            foreach (Match m in written.Matches(code))
            {
                var a = float.Parse(m.Groups["a"].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (a <= 0 || a >= 1 || code.Substring(0, m.Index).TrimEnd().EndsWith("PanelWash.Toward(", StringComparison.Ordinal)) continue;   // the mirror's colour, turned for the game
                Color c;
                if (m.Groups["n"].Success) { if (!named.TryGetValue(m.Groups["n"].Value, out c)) continue; c.a = a; }
                else c = new Color(float.Parse(m.Groups["r"].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(m.Groups["g"].Value, System.Globalization.CultureInfo.InvariantCulture),
                                   float.Parse(m.Groups["b"].Value, System.Globalization.CultureInfo.InvariantCulture), a);
                var worst = new[] { PanelWash.Page, PanelWash.Island }.Max(g => Off(Game(c, g), Mirror(c, g)));
                if (worst <= Levels) continue;
                if (NotWashes.Any(x => x.file == file && x.colour == m.Value)) { kept.Add((file, m.Value)); continue; }
                loud.Add(file + " " + m.Value + " (" + worst + " levels)");
            }
        }
        var stale = NotWashes.Where(x => !kept.Contains((x.file, x.colour))).Select(x => x.file + " " + x.colour).ToList();

        Check(off.Count == 0 && loud.Count == 0 && stale.Count == 0,
              "washes: the game draws every wash as the mirror does, within " + Levels + " levels (" + PanelWash.All.Length + " in PanelWash, each its mirror twin's colour, and " + drawn.Length +
              " drawn per row); no drawing file writes a translucent colour out that draws otherwise (" + NotWashes.Length + " that are no wash, listed)" +
              (off.Count > 0 ? "; off: " + string.Join("; ", off) : "") + (loud.Count > 0 ? "; written out, through PanelWash instead: " + string.Join("; ", loud) : "") +
              (stale.Count > 0 ? "; listed but not found: " + string.Join("; ", stale) : ""));

        // 0.8.6 the rounded sprites (PanelLook.RoundedSprite, RoundedShape): an outline's inside is clear, so the part shows the track under it as the
        // mirror's outlined part does (box-shadow inset, no fill); a filled one's inside is whole; the rim is solid. The damage parts' outline
        // (DamageEdge, rim 5 at radius 5) left its sliced inside at half alpha, which the game stretched over every stopped piece (Joost, 0.8.5: the
        // stopped piece "slightly too similar in color" to the got-through fill, its number hard to read)
        var sprites = Regex.Matches(LayoutTests.Code(File.ReadAllText(Path.Combine(panel, "PanelLook.cs"))), @"\bRoundedSprite\((true|false)(?:\s*,\s*(\d*\.?\d+)f?)?\)").Cast<Match>()
            .Select(m => (edge: m.Groups[1].Value == "true", rim: m.Groups[2].Success ? float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) : 2f)).Distinct().ToList();
        int A(int x, int y, bool edge, float rim) => (int)Math.Round(255 * Math.Clamp(RoundedShape.Alpha(x, y, edge, rim), 0f, 1f));
        var shapes = new List<string>();
        foreach (var (edge, rim) in sprites)
        {
            var name = edge ? "the outline, rim " + rim : "the filled shape";
            var inside = Enumerable.Range(RoundedShape.Border, RoundedShape.Size - 2 * RoundedShape.Border).SelectMany(x => Enumerable.Range(RoundedShape.Border, RoundedShape.Size - 2 * RoundedShape.Border).Select(y => A(x, y, edge, rim))).Distinct().ToList();
            if (inside.Any(a => a != (edge ? 0 : 255))) shapes.Add(name + ": its sliced inside at alpha " + string.Join(", ", inside) + " of 255");
            var mid = RoundedShape.Size / 2;
            var rimA = Enumerable.Range(0, edge ? (int)rim : mid).SelectMany(i => new[] { A(mid, i, edge, rim), A(i, mid, edge, rim), A(mid, RoundedShape.Size - 1 - i, edge, rim), A(RoundedShape.Size - 1 - i, mid, edge, rim) }).Distinct().ToList();
            if (rimA.Any(a => a != 255)) shapes.Add(name + ": its rim at alpha " + string.Join(", ", rimA) + " of 255");
        }
        Check(sprites.Count >= 4 && shapes.Count == 0, "rounded sprites: an outline's sliced inside is clear (the track shows, as the mirror's outlined part), a filled one's whole, the rim solid (" +
              sprites.Count + " made by RoundedSprite)" + (shapes.Count > 0 ? "; off: " + string.Join("; ", shapes) : ""));
        return fails;
    }
}
