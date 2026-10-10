// One story for every number (fix-deeds, 2026-10-09): the reviewer failed pages on a sample that said 410 trees on the overview and
// 411 on Woodcutting, 2 134 stone and ore beside 1 812, a "1 h" window above "All", 402 fish hooked beside 348 caught and got away, 312
// crops picked beside a crop list of 165. The sample players see in screenshots (PanelSample.Full) must never contradict itself, and
// where real data can (the game's counters and Hearthwoven's own count differ), the page says so in a row of its own.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static partial class CoherenceTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static double Num(string s) => string.IsNullOrWhiteSpace(s) ? 0 : double.Parse(s.Replace(" ", "").Replace(",", "").Replace(" ", ""), CultureInfo.InvariantCulture);

    static PanelView Page(PanelInput who, Chapter c, string page, Action<PanelState> more = null)
    {
        var st = new PanelState { Chapter = c, Page = { [c] = page } }; more?.Invoke(st);
        return PanelModel.Build(who, st);
    }
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
    static List<Block> All(PanelView v) => Every(v.Blocks).ToList();
    static bool Shows(PanelView v, string number) => All(v).Any(b => b.Value == number || b.Value2 == number);

    /// <summary>Every contradiction found in a sample (empty = coherent). The sample is Rowan's book with fellows wired as PanelUi does.</summary>
    internal static List<string> Problems(PanelInput full)
    {
        var bad = new List<string>();
        void Is(bool ok, string what) { if (!ok) bad.Add(what); }
        var over = Page(full, Chapter.Deeds, "overview");
        var cards = All(over).Where(b => b.Kind == "card" && b.Tone == null).ToList();
        Block Card(string title) => cards.First(c => c.Title == title);
        // ---- the overview's numbers are the detail pages' numbers ----
        foreach (var (title, page) in new[] { ("Hearth Cook", "cooking"), ("Hallwright", "building"), ("Forgekeeper", "crafting"), ("Woodcutter", "woodcutting"),
                                              ("Stonebreaker", "mining"), ("Fieldkeeper", "farming"), ("Tidecatcher", "fishing"), ("Beastkeeper", "taming") })
        {
            var card = Card(title); var detail = Page(full, Chapter.Deeds, page); var second0 = card.Items?.FirstOrDefault();
            Is(Shows(detail, card.Value) || (title == "Beastkeeper" && Num(card.Value) == All(detail).Where(b => b.Kind == "counts").SelectMany(b => b.Items).Where(i => i.Title != "Tamed").Sum(i => Num(i.Value))), "overview " + title + " " + card.Value + " is not on the " + page + " page");
            var second = card.Items?.FirstOrDefault();
            if (second != null && title != "Hearth Cook") Is(Shows(title == "Hallwright" ? Page(full, Chapter.Deeds, "groundwork") : detail, second.Value), "overview " + title + " second number " + second.Value + " is not on the " + page + " page");
        }
        // ---- the windows nest on Together damage: 10 min <= 30 min <= 1 h <= 3 h <= session <= all, for the group and for every player ----
        var windows = new[] { "LastTenMinutes", "LastThirtyMinutes", "LastHour", "LastThreeHours", "Session", "SinceInstall" };
        var totals = windows.Select(w => Every(Page(full, Chapter.Company, "together", st => { st.View["Company/together/category"] = "dealt"; st.View["Company/together/window"] = w; }).Blocks)
                                      .First(b => b.Kind == "category" && b.Id == "dealt")).ToList();
        for (int k = 1; k < totals.Count; k++)
        {
            Is(Num(totals[k].Value) >= Num(totals[k - 1].Value), "damage together: " + windows[k] + " " + totals[k].Value + " is below " + windows[k - 1] + " " + totals[k - 1].Value);
            foreach (var p in totals[k].Items) Is(Num(p.Value) >= Num(totals[k - 1].Items.First(x => x.Id == p.Id).Value), "damage together: " + p.Id + " has less in " + windows[k] + " than in " + windows[k - 1]);
        }
        // ---- Together tells the pages' numbers ----
        var together = Page(full, Chapter.Company, "together");
        double YouIn(string cat) => Num(Every(together.Blocks).First(b => b.Kind == "category" && b.Id == cat).Items.First(p => p.Id == full.PlayerName).Value);
        var cooking = Page(full, Chapter.Deeds, "cooking"); var building = Page(full, Chapter.Deeds, "building");
        Is(All(cooking).Any(b => Num(b.Value) == YouIn("cooked")), "Together dishes cooked " + YouIn("cooked") + " is not the Cooking page's");
        Is(All(building).Any(b => Num(b.Value) == YouIn("built")), "Together pieces built " + YouIn("built") + " is not the Building page's");
        Is(All(Page(full, Chapter.Deeds, "woodcutting")).Any(b => b.Kind == "composition" && Num(b.Value) == YouIn("wood")), "Together wood " + YouIn("wood") + " is not the Woodcutting bar's");
        Is(All(Page(full, Chapter.Deeds, "mining")).Any(b => b.Kind == "hero" && b.Title == "stone and ore brought in" && Num(b.Value) == YouIn("ore")), "Together stone and ore " + YouIn("ore") + " is not the Mining hero's");
        // ---- the food: the card, the Cooking page, Fireside and Food shared say one number ----
        var food = Page(full, Chapter.Company, "food");
        var ends = All(food).Where(b => b.Kind == "end").ToList();
        double fellowsEnjoyed = Num(ends.First(e => e.Tone == "right").Value);
        Is(Num(Card("Hearth Cook").Items.First().Value) == fellowsEnjoyed, "the Hearth Cook card says " + Card("Hearth Cook").Items.First().Value + " enjoyed, Food shared says " + fellowsEnjoyed);
        var who = All(cooking).FirstOrDefault(b => b.Kind == "section" && (b.Title ?? "").StartsWith("Who enjoyed"));
        Is(who != null && Num(who.Value) == fellowsEnjoyed, "Cooking 'Who enjoyed your food' " + who?.Value + " is not Food shared's " + fellowsEnjoyed);
        var fire = All(Page(full, Chapter.Company, "fireside")).Where(b => b.Kind == "gift" && b.Tone == "food").ToList();
        Is(Num(ends.First(e => e.Tone == "left").Value) == fire.Where(g => g.Text == full.PlayerName).Sum(g => Num(g.Value)) && fellowsEnjoyed == fire.Where(g => g.Title == full.PlayerName).Sum(g => Num(g.Value)), "Fireside's food threads are not Food shared's totals");
        // a fellow's book says what Rowan's says: what Edda made and Rowan enjoyed
        var edda = full.Fellows.First(f => f.PlayerName == "Edda"); var keepViewer = (edda.NowUtc, edda.ViewerName, edda.PlayerNames, edda.Fellows);
        edda.NowUtc = full.NowUtc; edda.ViewerName = full.PlayerName; edda.PlayerNames = full.PlayerNames; edda.Fellows = full.Fellows.Where(f => f != edda).Concat(new[] { full }).ToList();
        var eddaBook = Page(edda, Chapter.Company, "food");
        var rowanRow = All(eddaBook).FirstOrDefault(b => b.Kind == "row" && b.Tone == "right" && (b.Title ?? "").StartsWith(full.PlayerName));
        var toYou = fire.Where(g => g.Title == "Edda" && g.Text == full.PlayerName).Sum(g => Num(g.Value));
        Is(rowanRow != null && Num(rowanRow.Value) == toYou, "Edda's book says " + rowanRow?.Value + " of her food enjoyed by " + full.PlayerName + ", Fireside says " + toYou);
        edda.NowUtc = keepViewer.NowUtc; edda.ViewerName = keepViewer.ViewerName; edda.PlayerNames = keepViewer.PlayerNames; edda.Fellows = keepViewer.Fellows;
        // ---- fishing: hooked = caught + got away + the rest the page names; the fish by kind are the fish caught; quality adds up ----
        var fishing = Page(full, Chapter.Deeds, "fishing"); var rank = All(fishing).First(b => b.Kind == "ranking" && b.Items.Any(i => i.Title == "Hooked"));
        double Row(string t) => Num(rank.Items.FirstOrDefault(i => i.Title == t)?.Value);
        Is(Row("Hooked") >= Row("Caught") + Row("Got away") && rank.Items.All(i => i.Title == "Hooked" || i.Title == "Caught" || i.Title == "Got away"), "fishing: hooked " + Row("Hooked") + " is under caught " + Row("Caught") + " + got away " + Row("Got away") + ", or a row other than the game's three counters");
        var kinds = All(fishing).First(b => b.Kind == "itemgrid");
        Is(kinds.Items.Sum(i => Num(i.Value)) == Row("Caught"), "fishing: the fish listed add up to " + kinds.Items.Sum(i => Num(i.Value)) + ", the fish caught are " + Row("Caught"));
        var grades = All(fishing).First(b => b.Kind == "grades");
        Is(grades.Items.Sum(i => Num(i.Value)) == Row("Caught"), "fishing: the quality tiers add up to " + grades.Items.Sum(i => Num(i.Value)) + ", the fish caught are " + Row("Caught"));
        // ---- farming: the crop tiles, Other plants among them (fix4: one list), add up to the crops picked ----
        var farming = Page(full, Chapter.Deeds, "farming"); var hero = All(farming).First(b => b.Kind == "hero");
        double picked = Num(new[] { hero }.Concat(hero.Items ?? new List<Block>()).First(n => n.Title == "picked").Value);
        var tiles = All(farming).Where(b => b.Kind == "crop").ToList();
        double listed = tiles.Sum(t => Num(t.Items.First(p => p.Tone == PanelModel.PickedWord).Value));
        var other = tiles.FirstOrDefault(b => b.Id == PanelModel.OtherPlantsId);
        Is(listed == picked, "farming: the crop tiles (" + listed + ", Other plants " + other?.Value + " among them) are not the " + picked + " picked");
        Is(!All(farming).Any(b => b.Kind == "strip" && (b.Items ?? new List<Block>()).Any(i => i.Title == PanelModel.OtherPlants)), "farming: Other plants is in the crop list, not in the strip of other harvests");
        foreach (var t in tiles) Is(Num(t.Items.First(p => p.Tone == PanelModel.PickedWord).Value) <= picked, "farming: " + t.Title + " picked is more than all crops picked");
        // ---- the baselines are the game's counters at install: none above the counter now ----
        void Under(string what, IDictionary<string, float> baseline, IDictionary<string, float> now)
        {
            foreach (var kv in baseline ?? new Dictionary<string, float>())
            {
                float n = 0; if (now != null) now.TryGetValue(kv.Key, out n);
                Is(kv.Value <= n, "baseline " + what + "/" + kv.Key + " " + kv.Value + " is above the counter now " + n);
            }
        }
        Under("stats", full.Baseline[LocalTotals.StatsKind], full.Character); Under("pickables", full.Baseline[LocalTotals.PickablesKind], full.Harvested);
        Under("placed", full.Baseline[LocalTotals.PlacedKind], full.PiecesPlaced); Under("crafted", full.Baseline[LocalTotals.CraftedKind], full.ItemsCrafted);
        Under("pickedUp", full.Baseline["pickedUp"], full.ItemsPickedUp); Under("treesFelled", full.Baseline[PanelModel.TreesBaseline], full.Character);
        return bad;
    }

    public static int Run()
    {
        fails = 0;
        var now = new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc);
        var full = PanelSample.Full(now);
        var bad = Problems(full);
        Check(bad.Count == 0, "coherence: the sample every screenshot is built on never contradicts itself (overview = detail pages, windows nest, fish, crops, food, baselines)" + (bad.Count > 0 ? ": " + string.Join(" | ", bad) : ""));

        // the stress sample (a long-played character): the crops picked add up to no more than all crops picked
        var big = DeedsTests.Big(PanelSample.Evening(now));
        var crops = big.Harvested.Where(kv => !kv.Key.StartsWith("$") && kv.Key != "Flint").Sum(kv => (double)kv.Value);
        Check(crops <= big.Character["HarvestCrop"], "coherence: the stress sample's crops picked (" + crops + ") are within all crops picked (" + big.Character["HarvestCrop"] + ")");

        // the checker catches what it is meant to: a sample with the old contradictions fails
        var broken = PanelSample.Full(now); broken.Character["FishCaughtTier1"] += 10; broken.DamageSinceInstall = new DamageTally();
        var found = Problems(broken);
        Check(found.Any(p => p.StartsWith("fishing: the quality")) && found.Any(p => p.StartsWith("damage together")), "coherence: the checker flags a sample whose quality tiers or windows do not add up (" + found.Count + " found)");
        RunWorld();   // the whole sample world: every identity between pages and books (CoherenceWorld.cs)
        // ---------- text: nothing under 13 px at 1080p, the quiet colours readable on every ground (rubric 4: 4.5:1) ----------
        var src = AppContext.BaseDirectory;
        while (src != null && !System.IO.File.Exists(System.IO.Path.Combine(src, "src", "Plugin.cs"))) src = System.IO.Path.GetDirectoryName(src);
        if (src == null) Check(false, "text: source folder not found");
        else
        {
            string Read(params string[] p) => System.IO.File.ReadAllText(System.IO.Path.Combine(new[] { src }.Concat(p).ToArray()));
            var ui = Read("src", "Panel", "PanelUi.cs"); var look = Read("src", "Panel", "PanelLook.cs"); var html = Read("src", "Panel", "preview", "panel-preview.html");
            Check(ui.Contains("t.fontSize = Mathf.Max(size, PanelLook.MinText);") && System.Text.RegularExpressions.Regex.IsMatch(look, @"public const float MinText = 1[3-9]f;"),
                  "text: every Label is at least PanelLook.MinText (13 px at 1080p), the one shared floor");
            var small = System.Text.RegularExpressions.Regex.Matches(html, @"font-size:(\d+(?:\.\d+)?)px").Cast<System.Text.RegularExpressions.Match>().Where(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) < 13).Select(m => m.Value).Distinct().ToList();
            Check(small.Count == 0, "text: the preview draws nothing under 13 px" + (small.Count > 0 ? ": " + string.Join(", ", small) : ""));
            double Lum(string hex) { double L(int i) { var c = Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); } return 0.2126 * L(1) + 0.7152 * L(3) + 0.0722 * L(5); }
            double Ratio(string a, string b) { var x = Lum(a); var y = Lum(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }
            string Hex(string name) => System.Text.RegularExpressions.Regex.Match(look, name + @" = Hex\(""(#[0-9a-fA-F]{6})""\)").Groups[1].Value;
            // the grounds as drawn (sampled from the kit's art): the plate, your character's stone zone, the ember zone
            var grounds = new[] { ("plate", "#0a0908"), ("stone zone", "#33373c"), ("ember zone", "#261709") };
            var worst = grounds.SelectMany(g => new[] { "Text", "Muted", "Faint" }.Select(n => (n, ground: g.Item1, ratio: Ratio(Hex(n), g.Item2)))).OrderBy(x => x.ratio).First();
            Check(worst.ratio >= 4.5, "text: body, muted and faint text keep 4.5:1 on the plate and both zones (worst: " + worst.n + " on " + worst.ground + " " + worst.ratio.ToString("0.0", CultureInfo.InvariantCulture) + ":1)");
            // live-polish (Cooking's kitchen ledger in game: the station names were missing): an ellipsis line whose box is lower than its
            // font's line (about 1.16 em in the game's serif) is dropped whole by TMP. Every Size(OneLine(Label(.., size, ..)), w, h) with a
            // fixed h keeps h at least 1.2 x the drawn size (the MinText floor applied)
            var chapterSrc = System.IO.Directory.GetFiles(System.IO.Path.Combine(src, "src", "Panel"), "*.cs", System.IO.SearchOption.AllDirectories).Select(f => System.IO.File.ReadAllText(f)).ToList();
            var minText = double.Parse(System.Text.RegularExpressions.Regex.Match(look, @"MinText = (\d+(?:\.\d+)?)f").Groups[1].Value, CultureInfo.InvariantCulture);
            var lowBoxes = chapterSrc.SelectMany(s => System.Text.RegularExpressions.Regex.Matches(s, @"Size\(OneLine\(Label\([^,]+,[^,]+,\s*(\d+(?:\.\d+)?|PanelLook\.MinText)\b[^;]*?\)\),\s*[^,;]+,\s*(\d+(?:\.\d+)?)\)").Cast<System.Text.RegularExpressions.Match>())
                .Where(m => double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) < 1.2 * Math.Max(minText, m.Groups[1].Value == "PanelLook.MinText" ? minText : double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)))
                .Select(m => m.Value.Length > 90 ? m.Value.Substring(0, 90) : m.Value).ToList();
            // live-polish: the keyboard focus is one soft rounded gold ring (the vocab sprite, sliced), drawn by one helper everywhere, shown only after
            // a key press (focus-visible): a click, the wheel and a mouse move hide it, opening the panel starts without it; the bridge does the same
            var all = string.Join(" ", chapterSrc); var manifest = Read("src", "Panel", "vocab", "kit-additions.json");
            var ringEntry = System.Text.RegularExpressions.Regex.Match(manifest, @"""focus-ring\.png""\s*:\s*\{[^}]*""border""\s*:\s*\[\s*14\s*,\s*14\s*,\s*14\s*,\s*14\s*\]");
            Check(ringEntry.Success && System.IO.File.Exists(System.IO.Path.Combine(src, "src", "Panel", "vocab", "focus-ring.png")) &&
                  System.Text.RegularExpressions.Regex.Matches(all, @"FocusRing\(").Count >= 4 && !all.Contains("Brackets(") && !all.Contains("Ring(img.rectTransform, PanelLook.Focus)") &&
                  all.Contains("if (e != null) PanelUi.SetKeyFocus(false);") && all.Contains("if (!Mathf.Approximately(d, 0f)) SetKeyFocus(false);") && all.Contains("KeyFocus = false; pointerKnown = false;") &&
                  html.Contains("body.kbd .fcard.sel::after, body.kbd .fb-chip.cur::after, body.kbd .b2-col.cur .b2-tile::after") && html.Contains("-webkit-mask-box-image:url(../vocab/focus-ring.png) 14 / 14px") && !html.Contains(".fb-chip.cur { box-shadow"),
                  "focus: one soft rounded ring (vocab focus-ring, sliced 14) on chips, biome tiles and feat cards, shown only after a key press, in the panel and the bridge");
            // 0.8.1 (Joost sailing: the rudder, the wind, the hotbar and another mod's clock drew over the book): the book's canvas is on top, every
            // canvas of its own (the reason tags) sits relative to it, never at a fixed order the book would cover, and the game's own windows close it
            var fixedOrders = chapterSrc.SelectMany(s => System.Text.RegularExpressions.Regex.Matches(s, @"sortingOrder\s*=\s*\d+").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value)).ToList();
            Check(fixedOrders.Count == 0 && System.Text.RegularExpressions.Regex.IsMatch(ui, @"const int BookOrder = [1-3]\d{4};") && ui.Contains("canvas.sortingOrder = BookOrder;") &&
                  ui.Contains("if (GameOnTop()) { Close(); return; }") && ui.Contains("static bool CanOpen() => !GameOnTop()"),
                  "on top: the book's canvas is above the HUD, its tags sit relative to it (fixed orders: " + (fixedOrders.Count == 0 ? "none" : string.Join(", ", fixedOrders)) + "), the game's own windows close it and keep it shut");
            // 0.8.1 (reviewer, a BLOCKER in game): our own patch makes the trader's IsVisible true while the book is open, so GameOnTop took the book for
            // a trader and closed it on the next frame. What decides whether the book gives way (CanOpen, GameOnTop) never calls a method this mod
            // patches. Harmony does not run here, so the source says it: every [HarmonyPatch(typeof(T), "M")] against every call in those two methods
            // (a call on a variable counts when any patched method has its name)
            var patchedSrc = string.Join("\n", System.IO.Directory.GetFiles(System.IO.Path.Combine(src, "src"), "*.cs", System.IO.SearchOption.AllDirectories).Select(System.IO.File.ReadAllText));
            var patched = System.Text.RegularExpressions.Regex.Matches(patchedSrc, @"\[HarmonyPatch\(typeof\((\w+)\),\s*(?:nameof\((?:\w+\.)?(\w+)\)|""(\w+)"")").Cast<System.Text.RegularExpressions.Match>()
                .Select(m => (type: m.Groups[1].Value, method: m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value)).ToList();
            var from = ui.IndexOf("static bool CanOpen()", StringComparison.Ordinal); var gate = ui.IndexOf("static bool GameOnTop()", StringComparison.Ordinal);
            var end = gate < 0 ? -1 : ui.IndexOf("\n        }", gate, StringComparison.Ordinal);
            var decides = from < 0 || end < 0 ? "" : System.Text.RegularExpressions.Regex.Replace(ui.Substring(from, end - from), @"//[^\n]*", "");   // the code, not its comments
            var throughPatch = System.Text.RegularExpressions.Regex.Matches(decides, @"\b([A-Za-z_]\w*)(?:\.instance)?\.(\w+)\s*\(").Cast<System.Text.RegularExpressions.Match>()
                .Where(m => char.IsUpper(m.Groups[1].Value[0]) ? patched.Contains((m.Groups[1].Value, m.Groups[2].Value)) : patched.Any(p => p.method == m.Groups[2].Value))
                .Select(m => m.Value.TrimEnd('(')).Distinct().ToList();
            Check(patched.Count > 20 && patched.Contains(("StoreGui", "IsVisible")) && decides.Contains("Menu.IsVisible()") && throughPatch.Count == 0,
                  "on top: CanOpen and GameOnTop read the game's windows, never through a method this mod patches (" + patched.Count + " patched; through a patch: " + (throughPatch.Count == 0 ? "none" : string.Join(", ", throughPatch)) + ")");
            Check(lowBoxes.Count == 0 && chapterSrc.Any(s => s.Contains("static float LineBox(")), "text: no one-line label sits in a box lower than its font's line (TMP's ellipsis would drop it whole)" + (lowBoxes.Count > 0 ? ": " + string.Join(" | ", lowBoxes) : ""));
        }
        return fails;
    }
}
