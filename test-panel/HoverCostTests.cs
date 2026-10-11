// 0.8.2 (Joost felt 0.8.1's speed in game; 4e72aff gave every part of every damage bar its name and number on hover, at one or two objects a
// part): what the damage bars' hover makes on a page, counted the way the builder makes it (PanelUi.DamageBar, GroupDealtBar, ArmourBar and
// PageTag; Unity does not run here, so this counts as Bench.cs counts the chrome), on the sample feed (Log and Cards), a long feed (100 more
// foes, its newest fights up to PanelModel.FeedTop foes drawn; also the preview battle-feed-100) and the Battle pages with damage bars. The
// guard: the hover makes no object per part, only the page's one tag (2 objects) and the armor bar's ring (1); and the builders' source keeps
// the count true: their per-part and per-entry loops make what 475797b's made (the part, its number, the entry's one label), the tag only in
// PageTag. Called from Program.cs; LongFeed is also the preview scenario battle-feed-100.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hearthwoven;
using Hearthwoven.Panel;

static class HoverCostTests
{
    /// <summary>The 0.8.1 bench's fit: about 58 µs a UI object in game (PanelReuse.cs).</summary>
    const double MsPerObject = 0.058;

    /// <summary>A page's damage bars as the builder draws them: parts, share entries keyed to a part (their words swap on hover), parts no entry
    /// keys (no share line, a player's line: the tag says their words), Foes' Dealt parts with Everyone (the tag in the total's place), armor bars.</summary>
    internal struct Bars { public int Bars_, Parts, Keyed, Tagged, Group, Armor; }

    /// <summary>The hover objects 4e72aff to d6c9fd4 made: a second label per keyed entry, a tag (its ground and label) per part no entry keys
    /// and per Foes' Dealt part, the armor bar's pointer target, ring and tag.</summary>
    internal static int Before(Bars b) => b.Keyed + 2 * (b.Tagged + b.Group) + 4 * b.Armor;

    /// <summary>The hover objects now: the page's one tag (its ground and label) when a part needs it, the armor bar's ring; nothing per part.</summary>
    internal static int After(Bars b) => (b.Tagged + b.Group + b.Armor > 0 ? 2 : 0) + b.Armor;

    /// <summary>A page's damage bars, found as the Battle chapter's builders find them (BattleUi.BattleBlock, BattleFeedUi, EveryoneUi.GroupRows).</summary>
    internal static Bars Count(PanelView v)
    {
        var c = new Bars();
        List<Block> Of(Block b, string kind) => (b.Items ?? new List<Block>()).Where(i => i.Kind == kind).ToList();
        void Row(Block row)   // PanelUi.DamageRow: its parts' bar, the share line keyed to them, or a player's line of their own (the tag)
        {
            var n = PanelModel.BarFormOf(Of(row, "part"), true).Rows.Count; if (n == 0) return;
            c.Bars_++; c.Parts += n;
            if (row.Tone == PanelModel.DamagePlayerTone) c.Tagged += n; else c.Keyed += n;
        }
        void Bar(List<Block> parts)   // PanelUi.DamageBar with no share line (the feed's Log and Cards, a ranking line)
        {
            var n = PanelModel.BarFormOf(parts, true).Rows.Count; if (n == 0) return;
            c.Bars_++; c.Parts += n; c.Tagged += n;
        }
        foreach (var b in PanelModel.Content(v))   // every block the page draws, its boxes opened (a switch: its chosen view)
            switch (b.Kind)
            {
                case "dmgmix": Row(b); break;
                case "sources": case "dealtfoes": foreach (var r in Of(b, "source")) Row(r); break;
                case "fightfoes": foreach (var r in Of(b, "foe")) Row(r); break;
                case "armour":
                    foreach (var r in Of(b, "armourrow")) Row(r);
                    foreach (var a in Of(b, "armorbar")) if (PanelModel.BarFormOf(Of(a, "part"), true).Rows.Count > 0) { c.Bars_++; c.Parts++; c.Armor++; }
                    break;
                case "feed":
                    if (b.Tone == PanelModel.FeedTimeline) break;   // the Timeline draws no damage bar
                    foreach (var e in Of(b, "fight").SelectMany(f => Of(f, "entry"))) Bar(Of(e, PanelModel.FeedDealt));
                    break;
                case "foetable":
                    foreach (var r in Of(b, "foe"))
                    {
                        var split = Of(r, "part").Count; if (split > 0) { c.Bars_++; c.Parts += split; c.Group += split; }   // GroupDealtBar
                        foreach (var rk in Of(r, "ranking"))
                        {
                            foreach (var who in Of(rk, PanelModel.WhoHitKind)) foreach (var s in Of(who, "source")) Row(s);
                            foreach (var line in Of(rk, "rankrow").SelectMany(x => Of(x, "rankgroup")).SelectMany(g => g.Items ?? new List<Block>())) Bar(Of(line, "part"));
                        }
                    }
                    break;
                case PanelModel.GroupRowsKind:
                    if (b.Tone == PanelModel.DamagePlayersDealt || b.Tone == PanelModel.DamagePlayersReceived) foreach (var r in PanelModel.DamagePlayerRows(b)) Row(r);
                    break;
            }
        return c;
    }

    /// <summary>A long feed: the sample world with 100 more foes in this session, 20 fights of five foe kinds 8 minutes apart, each foe hit with
    /// three damage types (a bar of three parts). The feed draws its newest fights whole up to PanelModel.FeedTop foes (36 of them, 34 with a bar).</summary>
    public static PanelInput LongFeed(DateTime now)
    {
        var w = PanelSample.Full(now);
        string[] kinds = { "Boar", "Draugr", "Greydwarf", "Neck", "Skeleton" };
        for (int f = 0; f < 20; f++)
            for (int i = 0; i < kinds.Length; i++)
                w.Log.AddDamage(now.AddMinutes(-2 - 8 * f), "BlackForest", true, kinds[i], "Swords", new HitData.DamageTypes { m_slash = 70 + 7 * i, m_fire = 30 + (5 * f) % 40, m_pierce = 12 + i });
        w.Session = SampleWorld.TallyOf(w.Log); w.DamageSinceInstall = PanelSample.SinceInstall(w.Log);
        BattleSample.Session(w, now, new[] { "Edda", "Tor" }, new[] { "Edda", "Tor", "Finch" });
        return w;
    }

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var world = PanelSample.Full(now); var longFeed = LongFeed(now);
        PanelState Battle(string page, Action<PanelState> more = null) { var s = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Session, Page = { [Chapter.Battle] = page } }; more?.Invoke(s); return s; }
        PanelState Feed(string view) => Battle(PanelModel.FeedPage, s => s.View[PanelModel.FeedViewKey] = view);
        var pages = new (string name, PanelInput world, PanelState st)[]
        {
            ("the sample feed, Log", world, Feed(PanelModel.FeedLog)),
            ("the sample feed, Cards", world, Feed(PanelModel.FeedCards)),
            ("the long feed, Log", longFeed, Feed(PanelModel.FeedLog)),
            ("the long feed, Cards", longFeed, Feed(PanelModel.FeedCards)),
            ("Damage by foe", world, Battle("damage", s => s.View["Battle/damage/view"] = "foe")),
            ("Damage by weapon", world, Battle("damage")),
            ("Defense", world, Battle("defense")),
            ("Defense, What your armor stopped", world, Battle("defense", s => s.View["Battle/defense/view"] = PanelModel.ArmourId)),
            ("Last fight", world, Battle(PanelModel.LastFightPage)),
            ("Foes, Draugr open", world, Battle("foes", s => s.View[PanelModel.FoeOpenKey] = "Draugr")),
            ("Everyone, Foes, Draugr open", world, Battle("foes", s => { s.Window = TimeWindow.LastHour; s.Everyone = true; s.View[PanelModel.FoeOpenKey] = "Draugr"; })),
            ("Everyone, Damage", world, Battle("damage", s => s.Everyone = true)),
        };
        var counted = pages.Select(p => (p.name, bars: Count(PanelModel.Build(p.world, p.st)))).ToList();
        string Line((string name, Bars bars) p) => p.name + ": " + p.bars.Parts + " parts in " + p.bars.Bars_ + " bars, " + Before(p.bars) + " hover objects before (4e72aff, about " +
                                                  (Before(p.bars) * MsPerObject).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms a drawing), " + After(p.bars) + " now";
        foreach (var p in counted) System.Console.WriteLine("     hover cost: " + Line(p));

        // the long feed is long: its newest fights whole up to FeedTop foes drawn, nearly each with a bar of its three types
        var log = counted.First(p => p.name == "the long feed, Log").bars;
        Check(log.Bars_ >= PanelModel.FeedTop * 3 / 4 && log.Parts >= 2 * log.Bars_,
              "hover cost: the long feed (100 more foes in the session) draws its newest fights up to " + PanelModel.FeedTop + " foes, " + log.Bars_ + " damage bars of " + log.Parts + " parts");
        // every kind of damage bar is on these pages, so the count covers them all
        Check(counted.Any(p => p.bars.Keyed > 0) && counted.Any(p => p.bars.Tagged > 0) && counted.Any(p => p.bars.Group > 0) && counted.Any(p => p.bars.Armor > 0),
              "hover cost: the pages hold every kind of damage bar: share lines (" + counted.Sum(p => p.bars.Keyed) + " entries), bars without one (" + counted.Sum(p => p.bars.Tagged) +
              " parts), Foes' Dealt with Everyone (" + counted.Sum(p => p.bars.Group) + " parts), the armor bar (" + counted.Sum(p => p.bars.Armor) + ")");
        // the guard: nothing per part. The page's one tag and the armor bar's ring, on every page, however many parts
        var heavy = counted.Where(p => After(p.bars) > 2 + p.bars.Armor || p.bars.Armor > 1).Select(p => p.name).ToList();
        Check(heavy.Count == 0 && After(log) == 2 && counted.Sum(p => Before(p.bars)) > 10 * counted.Sum(p => After(p.bars)),
              "hover cost: the damage bars' hover makes no object per part, at most the page's one tag (2) and the armor bar's ring (1): the long feed's Log " + Before(log) + " objects before (4e72aff), " +
              After(log) + " now; all " + counted.Count + " pages " + counted.Sum(p => Before(p.bars)) + " before, " + counted.Sum(p => After(p.bars)) + " now" + (heavy.Count > 0 ? "; over it: " + string.Join(", ", heavy) : ""));

        // ---------- the source keeps the count true ----------
        var src = AppContext.BaseDirectory;
        while (src != null && !System.IO.File.Exists(System.IO.Path.Combine(src, "src", "Plugin.cs"))) src = System.IO.Path.GetDirectoryName(src);
        if (src == null) { Check(false, "hover cost: source folder not found"); return fails; }
        string Read(params string[] p) => Regex.Replace(System.IO.File.ReadAllText(System.IO.Path.Combine(new[] { src, "src", "Panel" }.Concat(p).ToArray())), @"//[^\n]*", "");   // the code, not its comments
        var rows = Read("Chapters", "DamageRowsUi.cs"); var battle = Read("Chapters", "BattleUi.cs"); var hover = Read("HoverUi.cs");
        var panel = string.Join("\n", System.IO.Directory.GetFiles(System.IO.Path.Combine(src, "src", "Panel"), "*.cs", System.IO.SearchOption.AllDirectories).Select(f => Regex.Replace(System.IO.File.ReadAllText(f), @"//[^\n]*", "")));
        // a method's body by its signature, and the bodies of the loops in it (by brace)
        string Body(string code, string head)
        {
            var at = code.IndexOf(head, StringComparison.Ordinal); if (at < 0) return "";
            var open = code.IndexOf('{', at); int depth = 0;
            for (int i = open; i >= 0 && i < code.Length; i++) { if (code[i] == '{') depth++; else if (code[i] == '}' && --depth == 0) return code.Substring(open, i - open + 1); }
            return "";
        }
        List<string> Loops(string body)
        {
            var found = new List<string>();
            foreach (Match m in Regex.Matches(body, @"\bfor\s*\(int k = 0;[^)]*\)\s*\{")) found.Add(Body(body.Substring(m.Index), "for"));
            return found;
        }
        // what a loop body makes: the book's object-making helpers it calls, sorted ("Label RoundedPart ...")
        var makers = new Regex(@"\b(Node|Label|Img|RoundedPart|VocabImg|Kit|BtText|BtIcon|BtRect|BtHollow|BtMarker|Marker|BarDigits|StoppedPart|PageTag|HoverRing|Badge|Fill|Spacer|VStack|Row|Line)\s*\(|new\s+GameObject\s*\(");
        string Makes(string body) => string.Join(" ", makers.Matches(body).Cast<Match>().Select(m => m.Groups[1].Success ? m.Groups[1].Value : "GameObject").OrderBy(x => x, StringComparer.Ordinal));
        var bar = Body(rows, "static float DamageBar("); var barLoops = Loops(bar);
        var group = Body(battle, "static RectTransform GroupDealtBar("); var groupLoops = Loops(group);
        var armor = Body(battle, "static void ArmourBar(");
        // 475797b's per-part and per-entry objects: a part (with its number inside, or an outlined part's fill, edges and numbers), a share entry
        // (its node, its band, its swatch or weapon icon, its one label); Foes' Dealt a part. A per-part tag or a second label shows here
        var want = new[] { "BarDigits RoundedPart StoppedPart", "BtIcon Label Node RoundedPart RoundedPart" };
        var have = barLoops.Select(Makes).ToList();
        Check(have.Count == 2 && have[0] == want[0] && have[1] == want[1] && groupLoops.Count == 1 && Makes(groupLoops[0]) == "RoundedPart",
              "hover cost: DamageBar's loop per part makes \"" + (have.Count > 0 ? have[0] : "?") + "\", per share entry \"" + (have.Count > 1 ? have[1] : "?") + "\", GroupDealtBar's per part \"" +
              (groupLoops.Count > 0 ? Makes(groupLoops[0]) : "?") + "\": what 475797b's made (a part and its number, an entry's one label; no hover object per part)");
        var tips = Regex.Matches(panel, @"""Tip""").Count; var pageTag = Body(rows, "static HoverTag PageTag(");
        Check(tips == 1 && pageTag.Contains("\"Tip\"") && Regex.Matches(bar + group + armor, @"\bPageTag\(").Count == 3 && !Regex.IsMatch(panel, @"\bPartTag\(") &&
              !Regex.IsMatch(hover, @"GameObject\[\]") && hover.Contains("Names[k].text = OnWords[k]") && hover.Contains("Tag.Show(this, k)") &&
              Makes(armor).Split(' ').Count(x => x == "RoundedPart") == 2 && !Regex.IsMatch(armor, @"\bImg\("),
              "hover cost: the tag is made in one place (PanelUi.PageTag, once a page), the bars take it (DamageBar, GroupDealtBar, ArmourBar); BarHover holds no object per part, swaps the entry's own words; the armor bar is its own pointer target");
        return fails;
    }
}
