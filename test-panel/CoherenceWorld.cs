// One world, every identity (sample-one, 2026-10-09). SampleWorld (src/Panel/PanelSampleWorld.cs) is the fictional group the previews and Dev.SampleData
// show: Rowan, Edda, Finch and Tor. Independent reviewers failed pages because pages contradicted each other: Ore Road earned on the Hall but not on
// Feats, Kept the Fires 1 377 beside 127 put in, Rowan 0 % sailed beside voyages. These checks read the pages the way a player does and hold every number
// that two pages (or two books) both tell to the same value: overview = detail, Together = each player's own page, a fellow's copy = their own book,
// feats = rules(counters), the windows nest, sums = totals, the voyages add up the same way from either end, the skills practised are the things done.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Hearthwoven;
using Hearthwoven.Panel;

static partial class CoherenceTests
{
    static readonly Regex Leading = new Regex(@"[0-9][0-9   ,]*(\.[0-9]+)?");
    /// <summary>The first number in a shown string: "1 377" 1377, "21.5 km" 21.5, "640 item-km" 640, "42 %" 42; 0 when there is none.</summary>
    static double Lead(string s) { var m = Leading.Match(s ?? ""); return m.Success ? PanelModel.ParseCount(m.Value.Trim()) : 0; }

    static PanelView Show(PanelInput who, Chapter c, string page, Action<PanelState> more = null)
    {
        var st = new PanelState { Chapter = c, Player = who.IsSelf ? "" : who.PlayerName, Window = TimeWindow.SinceInstall }; if (page != null) st.Page[c] = page; more?.Invoke(st);
        return PanelModel.Build(who, st);
    }
    static Block First(PanelView v, Func<Block, bool> where) => All(v).FirstOrDefault(where);
    static double ValueOf(PanelView v, Func<Block, bool> where) => Lead(First(v, where)?.Value);

    /// <summary>
    /// Where a fellow's copy of a player MAY differ from the player's own book, by the way the data is shared and not by the sample (SOURCES.md): the copy has no install
    /// counters, so a number layered on one (planted: the game books one plant of a PlantEasily grid, Hearthwoven counts every plant; hits on foes: the game misses hits in
    /// a friend's area) shows the game's own count, and the copy carries no record of biomes found (the Maps page counts the ones fought in).
    /// </summary>
    static readonly string[] KnownToDiffer = { "#planted", "#hits on foes", "#Biomes found" };

    static readonly TimeWindow[] Windows = { TimeWindow.LastTenMinutes, TimeWindow.LastThirtyMinutes, TimeWindow.LastHour, TimeWindow.LastThreeHours, TimeWindow.Session, TimeWindow.SinceInstall };

    // ---------- the numbers a player's own book tells, by name ----------
    static double Wood(PanelInput b) => ValueOf(Show(b, Chapter.Deeds, "woodcutting"), x => x.Kind == "composition" && x.Title == "Wood brought in");
    static double Ore(PanelInput b) => ValueOf(Show(b, Chapter.Deeds, "mining"), x => x.Kind == "hero" && x.Title == "stone and ore brought in");
    static double Built(PanelInput b) => ValueOf(Show(b, Chapter.Deeds, "building"), x => x.Kind == "hero" && x.Title == "pieces built");
    static double Cooked(PanelInput b) => ValueOf(Show(b, Chapter.Deeds, "cooking"), x => x.Kind == "hero" && x.Title == "dishes cooked");
    static double Dealt(PanelInput b) => ValueOf(Show(b, Chapter.Battle, "damage", s => s.View["Battle/damage/view"] = "type"), x => x.Kind == "damagegrid" && x.Title == "All types");
    static double Sailed(PanelInput b) => ValueOf(Show(b, Chapter.Voyages, "sailing"), x => x.Kind == "hero" && x.Title == "km sailed");
    static double Cargo(PanelInput b) => ValueOf(Show(b, Chapter.Voyages, "cargo"), x => x.Kind == "hero" && x.Title == "item-km");

    static double PartOf(PanelView together, string category, string player) =>
        Lead(All(together).FirstOrDefault(b => b.Kind == "category" && b.Id == category)?.Items.FirstOrDefault(p => p.Id == player)?.Value);

    static Block Strip(PanelInput b, TimeWindow w) => First(Show(b, Chapter.Battle, null, st => st.Window = w), x => x.Kind == "biomes");

    static List<(string name, List<string> problems, int checks)> WorldChecks(Dictionary<string, PanelInput> owns, Dictionary<string, PanelInput> copies, DateTime now)
    {
        var results = new List<(string, List<string>, int)>();
        void G(string name, Action<Action<bool, string>> body)
        {
            var bad = new List<string>(); var count = 0;
            body((ok, what) => { count++; if (!ok) bad.Add(what); });
            results.Add((name, bad, count));
        }
        var names = SampleWorld.Everyone;
        var rowan = owns[SampleWorld.Rowan];
        double Pct(double a, double b) => b <= 0 ? 0 : a / b;

        // ---- the overview tells what the detail pages tell, in every book ----
        G("the Deeds overview's numbers are on the detail pages, in all four books", Is =>
        {
            foreach (var n in names)
            {
                var book = owns[n]; var over = Show(book, Chapter.Deeds, "overview");
                foreach (var card in All(over).Where(b => b.Kind == "card" && b.Tone == null))
                {
                    var page = card.Id.Substring("Deeds/".Length); var detail = Show(book, Chapter.Deeds, page);
                    bool Shows(string number) => All(detail).Any(b => b.Value == number || b.Value2 == number || (b.Items ?? new List<Block>()).Any(i => i.Value == number));
                    var taming = card.Title == "Beastkeeper" ? All(detail).Where(b => b.Kind == "counts").SelectMany(b => b.Items).Where(i => i.Title != "Tamed").Sum(i => Lead(i.Value)) : -1;
                    Is(Shows(card.Value) || taming == Lead(card.Value), n + ": overview " + card.Title + " " + card.Value + " is not on the " + page + " page");
                    var second = card.Items?.FirstOrDefault();
                    if (second != null && card.Title != "Hearth Cook") Is(Shows(second.Value) || All(Show(book, Chapter.Deeds, "groundwork")).Any(b => b.Value == second.Value), n + ": overview " + card.Title + " second number " + second.Value + " is on no page of its deed");
                }
            }
        });

        // ---- Together tells each player's own number, whoever looks ----
        G("Together shows every player's own number, the same in every book (wood, ore, built, cooked, dealt, sailed, cargo)", Is =>
        {
            var own = names.ToDictionary(n => n, n => new Dictionary<string, double>
            {
                ["wood"] = Wood(owns[n]), ["ore"] = Ore(owns[n]), ["built"] = Built(owns[n]), ["cooked"] = Cooked(owns[n]), ["dealt"] = Dealt(owns[n]), ["sailed"] = Sailed(owns[n]), ["cargo"] = Cargo(owns[n]),
            });
            foreach (var viewer in names)
            {
                var together = Show(owns[viewer], Chapter.Company, "together");
                foreach (var q in names)
                    foreach (var cat in own[q].Keys)
                    {
                        var shown = PartOf(together, cat, q);
                        Is(Math.Abs(shown - own[q][cat]) < 0.051, viewer + "'s Together says " + q + " " + cat + " " + shown + ", " + q + "'s own page says " + own[q][cat]);
                    }
            }
            // a player who has anything in a category has a share in it, and the shares add up to the whole
            foreach (var viewer in names)
            {
                var together = Show(owns[viewer], Chapter.Company, "together");
                foreach (var cat in All(together).Where(b => b.Kind == "category"))
                {
                    var parts = cat.Items.ToList();
                    Is(parts.Sum(p => p.Fraction) > 0.999 && parts.Sum(p => p.Fraction) < 1.001, viewer + ": Together " + cat.Id + " shares add to " + parts.Sum(p => p.Fraction));
                    foreach (var q in names) Is((Lead(parts.First(p => p.Id == q).Value) > 0) == (own[q][cat.Id] > 0), viewer + ": " + q + " has " + own[q][cat.Id] + " " + cat.Id + " but " + (Lead(parts.First(p => p.Id == q).Value) > 0 ? "a share" : "no share") + " in Together");
                }
            }
        });

        // ---- a fellow's copy tells what their own book tells ----
        G("a fellow's copy shows the numbers of their own book (every page of Deeds, Hall, Battle, Voyages, Skills)", Is =>
        {
            Dictionary<string, List<double>> Leaves(PanelView v)
            {
                var d = new Dictionary<string, List<double>>();
                foreach (var b in All(v))
                {
                    if (string.IsNullOrEmpty(b.Value) || !char.IsDigit(b.Value.Trim()[0])) continue;
                    var key = b.Kind + "#" + (b.Id ?? b.Title ?? "");
                    if (!d.ContainsKey(key)) d[key] = new List<double>();
                    d[key].Add(Lead(b.Value));
                }
                return d;
            }
            var pages = new[]
            {
                (Chapter.Deeds, new[] { "overview", "cooking", "building", "groundwork", "crafting", "woodcutting", "mining", "farming", "fishing", "taming" }),
                (Chapter.Stores, new[] { "overview", "trader", "smelters" }), (Chapter.Battle, new[] { "overview", "damage", "defense", "deaths", "foes" }),
                (Chapter.Voyages, new[] { "overview", "sailing", "cargo", "onfoot", "maps" }),
            };
            foreach (var q in names)
                foreach (var (ch, ids) in pages)
                    foreach (var id in ids)
                    {
                        var mine = Leaves(Show(owns[q], ch, id)); var theirs = Leaves(Show(copies[q], ch, id));
                        foreach (var kv in theirs)
                            if (mine.TryGetValue(kv.Key, out var m) && !KnownToDiffer.Any(k => kv.Key.EndsWith(k)))
                                Is(kv.Value.All(m.Contains), q + " " + ch + "/" + id + " " + kv.Key + ": copy " + string.Join("+", kv.Value) + " vs own " + string.Join("+", m));   // the own book has the since-install zone too: the copy's numbers are among its
                    }
        });

        // ---- the windows nest and a session is part of since install ----
        G("Battle's windows nest (10 min <= 30 min <= 1 h <= 3 h <= session <= since install) for dealt, received and falls, in every book", Is =>
        {
            foreach (var n in names)
            {
                var strips = Windows.Select(w => Strip(owns[n], w)).ToList();
                for (int k = 1; k < strips.Count; k++)
                {
                    Is(strips[k] != null || strips[k - 1] == null, n + ": " + Windows[k] + " has no strip but " + Windows[k - 1] + " has");
                    if (strips[k] == null || strips[k - 1] == null) continue;
                    Is(Lead(strips[k].Value) >= Lead(strips[k - 1].Value), n + ": dealt in " + Windows[k] + " " + strips[k].Value + " is below " + Windows[k - 1] + " " + strips[k - 1].Value);
                    Is(Lead(strips[k].Value2) >= Lead(strips[k - 1].Value2), n + ": received in " + Windows[k] + " " + strips[k].Value2 + " is below " + Windows[k - 1] + " " + strips[k - 1].Value2);
                    Is(strips[k].Items.Sum(t => t.Count) >= strips[k - 1].Items.Sum(t => t.Count), n + ": falls in " + Windows[k] + " are fewer than in " + Windows[k - 1]);
                    // per biome likewise
                    foreach (var t in strips[k - 1].Items.Where(t => !string.IsNullOrEmpty(t.Value)))
                        Is(Lead(strips[k].Items.First(x => x.Id == t.Id).Value) >= Lead(t.Value), n + ": " + t.Id + " dealt in " + Windows[k] + " is below " + Windows[k - 1]);
                }
                // sums = totals: the biome tiles add up to the strip's dealt and received
                foreach (var (w, s) in Windows.Zip(strips, (a, b) => (a, b)).Where(x => x.b != null))
                {
                    Is(Math.Abs(s.Items.Sum(t => Lead(t.Value)) - Lead(s.Value)) < 0.5, n + " " + w + ": the biome tiles' dealt add up to " + s.Items.Sum(t => Lead(t.Value)) + ", the strip says " + s.Value);
                    Is(Math.Abs(s.Items.Sum(t => Lead(t.Value2)) - Lead(s.Value2)) < 0.5, n + " " + w + ": the biome tiles' received add up to " + s.Items.Sum(t => Lead(t.Value2)) + ", the strip says " + s.Value2);
                }
            }
        });
        G("the day windows nest (today <= 7 days <= All) on every page that has them, the day rows add up to since install, and 30 days (before the history) shows All, greyed with its line", Is =>
        {
            var days = new[] { TimeWindow.Today, TimeWindow.SevenDays, TimeWindow.SinceInstall };
            PanelView In(PanelInput b, Chapter c, string page, TimeWindow w, Action<PanelState> more = null) => Show(b, c, page, st => { st.Window = w; more?.Invoke(st); });
            double Sum(Block blk) => blk == null ? 0 : blk.Items.Sum(r => Lead(r.Value));
            foreach (var n in names)
            {
                var b = owns[n];
                var metrics = new (string what, Func<TimeWindow, double> f)[]
                {
                    ("dealt on the strip", w => Lead(Strip(b, w)?.Value)), ("received on the strip", w => Lead(Strip(b, w)?.Value2)), ("falls on the strip", w => Strip(b, w)?.Items.Sum(t => t.Count) ?? 0),
                    ("dealt on Damage", w => ValueOf(In(b, Chapter.Battle, "damage", w, st => st.View["Battle/damage/view"] = "type"), x => x.Kind == "damagegrid" && x.Title == "All types")),
                    ("the foes' table", w => Sum(First(In(b, Chapter.Battle, "foes", w), x => x.Kind == "foetable"))),
                    ("received from (Defence)", w => Sum(First(In(b, Chapter.Battle, "defense", w), x => x.Kind == "sources"))),
                    ("blocks and parries", w => First(In(b, Chapter.Battle, "defense", w), x => x.Kind == "guard") is Block g ? Lead(g.Value) + Lead(g.Value2) : 0),
                    ("falls on Deaths", w => First(In(b, Chapter.Battle, "deaths", w), x => x.Kind == "deathstrip")?.Items.Sum(t => t.Count) ?? 0),
                    ("km sailed", w => ValueOf(In(b, Chapter.Voyages, "sailing", w), x => x.Kind == "hero" && x.Title == "km sailed")),
                    ("item-km of cargo", w => ValueOf(In(b, Chapter.Voyages, "cargo", w), x => x.Kind == "hero" && x.Title == "item-km")),
                    ("wood brought in", w => ValueOf(In(b, Chapter.Deeds, "woodcutting", w), x => x.Kind == "composition" && x.Title == "Wood brought in")),
                    ("stone and ore brought in", w => ValueOf(In(b, Chapter.Deeds, "mining", w), x => x.Kind == "hero" && x.Title == "stone and ore brought in")),
                    ("damage dealt on Together", w => PartOf(In(b, Chapter.Company, "together", TimeWindow.Session, st => { st.View["Company/together/category"] = "dealt"; st.View["Company/together/window"] = w.ToString(); }), "dealt", n)),
                };
                foreach (var (what, f) in metrics)
                {
                    var v = days.Select(f).ToList();
                    Is(v[0] <= v[1] + 0.5 && v[1] <= v[2] + 0.5, n + ": " + what + " today " + v[0] + ", 7 days " + v[1] + ", All " + v[2] + " do not nest");
                }
                // not empty by accident: the week holds something on the pages the sample fills for this player
                Is(Lead(Strip(b, TimeWindow.SevenDays)?.Value) > 0 || b.DamageSinceInstall == null || DayHistory.DealtOf(b.DamageSinceInstall) == 0, n + ": 7 days shows no damage dealt");
                // the day rows are what was counted since install (the sample's history begins at the install)
                var rows = b.History.Rows; double R(Func<DayHistory.Row, double> f) => rows.Sum(f);
                Is(Math.Abs(R(r => r.Events.ChopHits.Values.Sum()) - b.Events.ChopHits.Values.Sum()) < 0.5 && Math.Abs(R(r => r.Events.CargoMeters.Values.Sum()) - b.Events.CargoMeters.Values.Sum()) < 0.5 &&
                   R(r => r.Events.Blocks) == b.Events.Blocks && Math.Abs(R(r => r.Events.SailedWith.Values.Sum()) - b.Events.SailedWith.Values.Sum()) < 0.5,
                   n + ": the day rows do not add up to the since-install tallies");
                Is(b.DamageSinceInstall == null || Math.Abs(R(r => DayHistory.DealtOf(r.Damage)) - DayHistory.DealtOf(b.DamageSinceInstall)) < 0.5, n + ": the day rows' damage dealt is not the since-install damage");
                Is(b.BiomeSinceInstall == null || Math.Abs(R(r => r.Biome.Deaths.Values.Sum()) - b.BiomeSinceInstall.Deaths.Values.Sum()) < 0.5, n + ": the day rows' falls are not the since-install falls");
                Is(Math.Abs(R(r => r.Game.TryGetValue("DistanceSail", out var ds) ? ds : 0) / 1000 - SampleWorld.SailedSince(n)) < 0.05, n + ": the day rows' km sailed are not the voyages since install");
                // 30 days reaches before the history: greyed but pressable; chosen, the page shows All and one line says from when it works.
                // B17 (Joost 2026-10-09): never a standing "Day history since ..." line on a page whose chosen window works
                var thirty = In(b, Chapter.Voyages, "sailing", TimeWindow.ThirtyDays);
                var seven = In(b, Chapter.Voyages, "sailing", TimeWindow.SevenDays);
                var opens = PanelModel.ZoneDate(b.History.FirstDay(PanelModel.LocalToday(b)).AddDays(29), PanelModel.LocalToday(b));
                Is(thirty.ShownWindow == TimeWindow.SinceInstall && thirty.Windows.Single(c => c.Id == "ThirtyDays").Disabled && thirty.Windows.Single(c => c.Id == "ThirtyDays").Waits &&
                   !thirty.Windows.Single(c => c.Id == "SevenDays").Disabled && !thirty.Windows.Single(c => c.Id == "SevenDays").Waits &&
                   (PanelModel.PlateOf(thirty)?.Text ?? "").StartsWith("30 days works from " + opens + ": ") &&
                   !PanelModel.AllText(seven).Any(t => t.Contains("works from") || t.Contains("Day history")),
                   n + ": 30 days before the history is not greyed and pressable with its one line, or a line stands on a page whose window works (" + PanelModel.PlateOf(thirty)?.Text + ")");
            }
            // Together's window chips: a day window before the history is greyed the same way, with the same line, and shows All
            foreach (var n in names)
            {
                var tv = Show(owns[n], Chapter.Company, "together", st => { st.View["Company/together/category"] = "dealt"; st.View["Company/together/window"] = "ThirtyDays"; });
                var tsw = All(tv).FirstOrDefault(x => x.Kind == "switch" && x.Id == "Company/together/window");
                Is(tsw != null && tsw.Items.Single(x => x.Id == "ThirtyDays").Tone == PanelModel.OffTone && tsw.Items.Single(x => x.Id == "SevenDays").Tone == null && tsw.Items.Single(x => x.Selected).Id == "SinceInstall" &&
                   tsw.Items.Single(x => x.Id == "ThirtyDays").Waits && All(tv).Any(x => x.Kind == "note" && (x.Text ?? "").StartsWith("30 days works from ")), n + ": Together's 30 days is not greyed and pressable with its one line, or still chosen");
                var st2 = new PanelState { Chapter = Chapter.Company, Window = TimeWindow.SinceInstall }; st2.Page[Chapter.Company] = "together"; st2.View["Company/together/category"] = "dealt"; st2.View["Company/together/window"] = "SevenDays";
                PanelModel.FilterKeyPressed(st2, PanelModel.Build(owns[n], st2));
                Is(st2.View["Company/together/window"] == "SinceInstall", n + ": the window key stops on Together's greyed 30 days (" + st2.View["Company/together/window"] + ")");
            }
            // a fellow's copy has no day rows: its day chips are greyed and the page falls back
            foreach (var n in names)
            {
                var copy = copies[n];
                var v = Show(copy, Chapter.Battle, "foes", st => st.Window = TimeWindow.SevenDays);
                Is(v.Windows.Where(c => PanelModel.IsDayWindow((TimeWindow)Enum.Parse(typeof(TimeWindow), c.Id))).All(c => c.Disabled) && v.ShownWindow != TimeWindow.SevenDays, n + ": a fellow's copy offers day windows it cannot show");
            }
        });
        G("a session is part of since install: damage per foe, skill and type, per biome, the falls and the hits", Is =>
        {
            foreach (var n in names)
            {
                var b = owns[n]; var session = b.Session; var since = b.DamageSinceInstall;
                Is(since != null && session != null, n + ": no tallies");
                if (since == null || session == null) continue;
                foreach (var kv in session.Dealt) Is(since.Dealt.TryGetValue(kv.Key, out var v) && v >= kv.Value, n + ": session dealt " + kv.Key + " " + kv.Value + " is more than since install");
                foreach (var kv in session.Taken) Is(since.Taken.TryGetValue(kv.Key, out var v) && v >= kv.Value, n + ": session taken " + kv.Key + " " + kv.Value + " is more than since install");
                var logTally = SampleWorld.TallyOf(b.Log);
                Is(logTally.Dealt.Count == session.Dealt.Count && logTally.Dealt.All(kv => session.Dealt.TryGetValue(kv.Key, out var v) && Math.Abs(v - kv.Value) < 0.01), n + ": the session tally is not what the log holds");
                var bio = BiomeTally.FromLog(b.Log);
                foreach (var kv in bio.Damage) Is(b.BiomeSinceInstall.Damage.TryGetValue(kv.Key, out var v) && v >= kv.Value, n + ": biome damage " + kv.Key + " this session is more than since install");
                foreach (var kv in bio.Deaths) Is(b.BiomeSinceInstall.Deaths.TryGetValue(kv.Key, out var v) && v >= kv.Value, n + ": falls in " + kv.Key + " this session are more than since install");
                // the biome totals are the tally's, in both directions
                Is(Math.Abs(b.BiomeSinceInstall.Damage.Where(kv => kv.Key.Contains("|dealt|") && !kv.Key.EndsWith("|chop") && !kv.Key.EndsWith("|pickaxe")).Sum(kv => kv.Value) - since.Dealt.Where(kv => !kv.Key.EndsWith("|chop") && !kv.Key.EndsWith("|pickaxe")).Sum(kv => kv.Value)) < 0.5, n + ": the biome totals dealt are not the damage tally's");
                Is(Math.Abs(b.BiomeSinceInstall.Damage.Where(kv => kv.Key.Contains("|taken|")).Sum(kv => kv.Value) - since.Taken.Sum(kv => kv.Value)) < 0.5, n + ": the biome totals received are not the damage tally's");
                // the falls: the game's counter, the baseline and Hearthwoven's count, and the biomes
                var falls = b.BiomeSinceInstall.Deaths.Values.Sum(); b.Events.Battle.TryGetValue("Deaths", out var counted);
                Is(Math.Abs(falls - counted) < 0.01, n + ": " + falls + " falls by biome, " + counted + " counted");
                Is(b.Log.Deaths.Count <= falls, n + ": more falls in the log than since install");
                if (b.Baseline != null && b.Baseline.TryGetValue("battle", out var bb)) Is(Math.Abs(b.Character["Deaths"] - (bb["Deaths"] + counted)) < 0.01, n + ": deaths " + b.Character["Deaths"] + " is not " + bb["Deaths"] + " at install + " + counted + " since");
                else Is(!b.Character.ContainsKey("Deaths") ? counted == 0 : b.Character["Deaths"] >= counted, n + ": fewer deaths in the game's counter than counted since install");
                // every row of the session is inside the session, and not in the future
                var start = SampleWorld.SessionStart(now);
                foreach (var key in b.Log.Damage.Keys)
                {
                    var t = DateTime.ParseExact(key.Split('|')[0], "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    Is(t >= start.AddMinutes(-1) && t <= now, n + ": a fight at " + t.ToString("HH:mm") + " is outside the session");
                }
                foreach (var d in b.Log.Deaths) Is(d.Time >= start && d.Time <= now, n + ": a fall at " + d.Time.ToString("HH:mm") + " is outside the session");
            }
        });
        G("hits and kills: the game's counter is at the install counter and no more than Hearthwoven's count above it; the kills by foe add up", Is =>
        {
            foreach (var n in names)
            {
                var b = owns[n]; b.Events.Battle.TryGetValue("EnemyHits", out var counted);
                if (b.Baseline != null && b.Baseline.TryGetValue("battle", out var bb)) Is(b.Character["EnemyHits"] >= bb["EnemyHits"] && b.Character["EnemyHits"] <= bb["EnemyHits"] + counted, n + ": the game's hits " + b.Character["EnemyHits"] + " are outside " + bb["EnemyHits"] + " + up to " + counted);
                else Is(b.Character["EnemyHits"] >= counted, n + ": fewer hits in the game's counter than counted");
                Is(Math.Abs(b.EnemyKills.Values.Sum() - b.Character["EnemyKills"]) < 0.01, n + ": the kills by foe add up to " + b.EnemyKills.Values.Sum() + ", the game's count is " + b.Character["EnemyKills"]);
                var bosses = b.EnemyKills.Where(kv => kv.Key == "$enemy_eikthyr" || kv.Key == "$enemy_gdking" || kv.Key == "$enemy_bonemass" || kv.Key == "$enemy_dragon").Sum(kv => kv.Value);
                Is(!b.Character.ContainsKey("BossKills") ? bosses == 0 : Math.Abs(b.Character["BossKills"] - bosses) < 0.01, n + ": " + bosses + " bosses by name, BossKills is " + (b.Character.ContainsKey("BossKills") ? b.Character["BossKills"] : 0));
                // the Foes page's table adds up to the damage dealt
                var foes = First(Show(b, Chapter.Battle, "foes"), x => x.Kind == "foetable");
                Is(foes == null || Math.Abs(foes.Items.Sum(r => Lead(r.Value)) - Dealt(b)) < 0.5, n + ": the foes add up to " + (foes?.Items.Sum(r => Lead(r.Value))) + ", the damage dealt is " + Dealt(b));
                // what hurt you adds up to the sources received from, and both to the strip's received
                var hurt = First(Show(b, Chapter.Battle, null), x => x.Kind == "composition" && x.Title != null && x.Title.StartsWith("What hurt"));
                var sources = First(Show(b, Chapter.Battle, "defense"), x => x.Kind == "sources");
                if (hurt != null && sources != null) Is(Math.Abs(Lead(hurt.Value) - sources.Items.Sum(s => Lead(s.Value))) < 0.5, n + ": what hurt adds up to " + hurt.Value + ", the sources to " + sources.Items.Sum(s => Lead(s.Value)));
                if (hurt != null) Is(Math.Abs(Lead(hurt.Value) - Lead(Strip(b, TimeWindow.SinceInstall).Value2)) < 0.5, n + ": what hurt " + hurt.Value + " is not the strip's received " + Strip(b, TimeWindow.SinceInstall).Value2);
                // blocks: the Defence page's blocks plus parries are the blocks counted, and the shield hooks never count more than that
                var guard = First(Show(b, Chapter.Battle, "defense"), x => x.Kind == "guard");
                if (guard != null) Is(Math.Abs(Lead(guard.Value) + Lead(guard.Value2) - b.Events.Blocks) < 0.5 && Lead(guard.Value2) == b.Events.Parries, n + ": " + guard.Value + " blocks + " + guard.Value2 + " parries is not the " + b.Events.Blocks + " blocks counted");
                var near = b.Feats.Count(FeatsCounters.BlocksNear);
                Is(near <= b.Events.Blocks - b.Events.Parries, n + ": " + near + " hits held near a fellow is more than the " + (b.Events.Blocks - b.Events.Parries) + " held");
                Is(b.Feats.Count(FeatsCounters.Stopped) == 0 || (b.Feats.Count(FeatsCounters.Stopped) / Math.Max(1, b.Events.Blocks) is var per && per >= 5 && per <= 120), n + ": " + b.Feats.Count(FeatsCounters.Stopped) + " damage stopped by " + b.Events.Blocks + " blocks is not a believable " + (b.Feats.Count(FeatsCounters.Stopped) / Math.Max(1, b.Events.Blocks)) + " each");
                Is(b.Feats.Count(FeatsCounters.BestStreak) <= b.Events.Parries, n + ": a run of " + b.Feats.Count(FeatsCounters.BestStreak) + " parries is more than the " + b.Events.Parries + " parries counted");
            }
        });

        // ---- feats are the rules of the counters ----
        G("feats: the ledger is exactly what the rules make of the counters, in every own book and every copy; moments are after install", Is =>
        {
            var install = SampleWorld.InstalledAt(now);
            foreach (var n in names)
            {
                var own = owns[n]; var copy = copies[n];
                foreach (var d in PanelModel.FeatDefs)
                {
                    var derived = PanelModel.FeatTierOf(d, PanelModel.FeatValue(d, own));
                    Is(own.Feats.Tier(d.Id) == derived, n + ": " + d.Name + " is tier " + own.Feats.Tier(d.Id) + " in the ledger, the counters say " + derived);
                    Is(PanelModel.FeatTier(d, own) == derived, n + ": the page's tier for " + d.Name + " is not the rules'");
                    Is(copy.Feats.Tier(d.Id) == derived, n + ": the copy of " + d.Name + " is tier " + copy.Feats.Tier(d.Id) + ", their own book " + derived);
                    for (int t = 1; t <= own.Feats.Tier(d.Id); t++)
                    {
                        var m = own.Feats.Moment(d.Id, t).Value;
                        Is(m.Before ? d.Retro : (m.Utc >= install && m.Utc <= now), n + ": " + d.Name + " tier " + t + " has a moment outside the time since install (" + m.Utc.ToString("d MMM HH:mm") + ")");
                        Is(!m.Before || (d.Retro), n + ": " + d.Name + " is 'before install' but its counter starts at install");
                        Is(!m.Noticed || d.Derived, n + ": " + d.Name + " is 'noticed' but is counted on its own PC");
                    }
                }
                // the Feats page lists the earned and the unsung exactly, none twice, in both views
                var earnedIds = PanelModel.FeatDefs.Where(d => !d.Group && PanelModel.FeatTier(d, own) > 0).Select(d => d.Id).ToList();
                var earnedPage = PanelModel.VisibleFeats(Show(own, Chapter.Feats, "earned")).Select(c => c.Id).ToList();
                var unsungPage = PanelModel.VisibleFeats(Show(own, Chapter.Feats, "unsung")).Select(c => c.Id).ToList();
                Is(earnedPage.SequenceEqual(earnedIds), n + ": the Earned view lists " + string.Join(",", earnedPage) + ", the rules earn " + string.Join(",", earnedIds));
                Is(earnedPage.Concat(unsungPage).Distinct().Count() == PanelModel.OwnFeatCount && !earnedPage.Intersect(unsungPage).Any(), n + ": a feat is on both Earned and Unsung, or on neither");
                // Known for names only earned feats; each owner page's band holds exactly the earned feats of that page
                var known = First(Show(own, Chapter.Deeds, "overview"), x => x.Kind == "knownfor");
                Is((known?.Items ?? new List<Block>()).All(i => earnedIds.Contains(i.Id)) && (known != null) == earnedIds.Any(), n + ": Known for is not a subset of the earned feats");
                foreach (var (ch, page) in PanelModel.FeatDefs.Where(d => !d.Group).Select(d => (d.Chapter, d.Page)).Distinct())
                {
                    var band = First(Show(own, ch, page), x => x.Kind == "featband");
                    var want = PanelModel.FeatDefs.Where(d => d.Chapter == ch && d.Page == page && earnedIds.Contains(d.Id)).Select(d => d.Id).ToList();
                    Is((band == null ? new List<string>() : PanelModel.BandFeats(band).Select(i => i.Id).ToList()).SequenceEqual(want), n + ": the band on " + ch + "/" + page + " holds " + string.Join(",", band == null ? new string[0] : PanelModel.BandFeats(band).Select(i => i.Id)) + ", the earned feats of the page are " + string.Join(",", want));
                }
            }
        });
        G("feats: each progress line is a number another page tells (Kept the Fires = the smelters, Ore Road = the cargo, Born = the births, Drover and Long Lead = the leads, Shield Wall = the blocks)", Is =>
        {
            foreach (var n in names)
            {
                var own = owns[n];
                double Progress(string id) { var d = First(Show(own, Chapter.Feats, PanelModel.FeatTier(PanelModel.FeatById(id), own) > 0 ? "earned" : "unsung", st => st.FeatSel = id), x => x.Kind == "featdetail" && x.Id == id); return d == null ? -1 : Lead(d.Items.FirstOrDefault(i => i.Kind == "progress")?.Title); }
                var kept = PanelModel.FeatById("keptfires"); var tier = PanelModel.FeatTier(kept, own);
                var smelters = Show(own, Chapter.Stores, "smelters"); var put = ValueOf(smelters, x => x.Kind == "hero" && x.Title == "items put in");
                if (tier < 3) Is(Progress("keptfires") == put, n + ": Kept the Fires says " + Progress("keptfires") + ", the smelters took " + put);
                Is(PanelModel.FeatValue(kept, own) == put, n + ": the feat counts " + PanelModel.FeatValue(kept, own) + " into the smelters, the Hall says " + put);
                var ore = PanelModel.FeatById("oreroad"); var cargo = Cargo(own);
                if (PanelModel.FeatTier(ore, own) < 3) Is(Progress("oreroad") == Math.Floor(cargo), n + ": Ore Road says " + Progress("oreroad") + ", the Cargo page says " + cargo + " item-km");
                Is(Math.Abs(PanelModel.FeatValue(ore, own) - cargo) < 0.5, n + ": Ore Road counts " + PanelModel.FeatValue(ore, own) + ", Cargo " + cargo);
                var born = PanelModel.FeatById("born"); var births = ValueOf(Show(own, Chapter.Deeds, "taming"), x => x.Kind == "section" && x.Title == "Born in " + (own.IsSelf ? "your" : n + "'s") + " care");
                Is(PanelModel.FeatValue(born, own) == births || (births == 0 && PanelModel.FeatValue(born, own) == 0), n + ": Born in Your Care counts " + PanelModel.FeatValue(born, own) + ", Taming says " + births);
                var drover = PanelModel.FeatById("drover"); var ledKm = ValueOf(Show(own, Chapter.Deeds, "taming"), x => x.Kind == "section" && x.Title == "Animals led");
                Is(Math.Abs(PanelModel.FeatValue(drover, own) - ledKm) < 0.06, n + ": Drover counts " + PanelModel.FeatValue(drover, own) + " km, Taming says " + ledKm);
                var lead = PanelModel.FeatById("longlead"); var best = ValueOf(Show(own, Chapter.Deeds, "taming"), x => x.Kind == "stat" && x.Title == "longest lead");
                Is(Math.Abs(PanelModel.FeatValue(lead, own) - best) < 0.06, n + ": Long Lead counts " + PanelModel.FeatValue(lead, own) + " km, Taming says " + best);
                var keel = PanelModel.FeatById("heavykeel"); var aboard = ValueOf(Show(own, Chapter.Voyages, "cargo"), x => x.Kind == "item" && x.Id == "heavykeel");
                Is(PanelModel.FeatValue(keel, own) == aboard, n + ": Heavy Keel counts " + PanelModel.FeatValue(keel, own) + ", Cargo says " + aboard);
                var parries = PanelModel.FeatById("turnedblades");
                Is(PanelModel.FeatValue(parries, own) == own.Events.Parries, n + ": Turned Blades counts " + PanelModel.FeatValue(parries, own) + ", the parries counted are " + own.Events.Parries);
                var grave = PanelModel.FeatById("waymate");
                Is(PanelModel.FeatValue(grave, own) == (own.Character.TryGetValue("TombstonesOpenedOther", out var gv) ? gv : 0), n + ": Waymate does not count the graves in the character's record");
                // the table, the fires, the feasts and the gear: the fellows' own records
                var food = All(Show(own, Chapter.Company, "food")).Where(b => b.Kind == "row" && b.Tone == "right").ToList();
                Is(PanelModel.FeatValue(PanelModel.FeatById("fulltable"), own) == food.Count(r => Lead(r.Value) > 0), n + ": Full Table counts " + PanelModel.FeatValue(PanelModel.FeatById("fulltable"), own) + " fellows, Food shared has " + food.Count);
                var ferry = PanelModel.FeatValue(PanelModel.FeatById("ferryman"), own);
                var minutes = names.Where(o => o != n).Sum(o => ValueOfUnder(Show(owns[o], Chapter.Voyages, "sailing"), n));
                Is(Math.Abs(ferry - minutes / 60.0) < 0.02, n + ": Ferryman counts " + ferry + " hours, the others' Sailing pages put " + minutes + " minutes under their helm");
                var armsRows = All(Show(own, Chapter.Company, "gear")).Where(b => b.Kind == "row" && b.Tone == "right").ToList();
                Is(PanelModel.FeatValue(PanelModel.FeatById("arms"), own) == armsRows.Count, n + ": Arms for the Hall counts " + PanelModel.FeatValue(PanelModel.FeatById("arms"), own) + " fellows, Gear shared has " + armsRows.Count);
            }
        });

        // ---- the Hall adds up ----
        G("Hall: coins = the traders, items bought = the ledger, put in = the stations = the bar, and the overview is the two pages", Is =>
        {
            foreach (var n in names)
            {
                var b = owns[n]; var trader = Show(b, Chapter.Stores, "trader"); var smelters = Show(b, Chapter.Stores, "smelters"); var over = Show(b, Chapter.Stores, "overview");
                var coins = ValueOf(trader, x => x.Kind == "hero" && x.Title == "coins spent");
                Is(Math.Abs(coins - b.Events.Spent.Values.Sum()) < 0.5, n + ": " + coins + " coins shown, " + b.Events.Spent.Values.Sum() + " spent");
                var comp = First(trader, x => x.Kind == "composition");
                if (comp != null) Is(Math.Abs(comp.Items.Sum(i => Lead(i.Value)) - coins) < 0.5, n + ": the traders add up to " + comp.Items.Sum(i => Lead(i.Value)) + ", the coins are " + coins);
                var bought = b.Events.Bought.Values.Sum();
                var ledger = First(trader, x => x.Kind == "ledger");
                if (ledger != null) Is(Math.Abs(ledger.Items.SelectMany(r => r.Items).Sum(c => Lead(c.Value)) - bought) < 0.5, n + ": the ledger adds up to " + ledger.Items.SelectMany(r => r.Items).Sum(c => Lead(c.Value)) + " items, " + bought + " bought");
                var sub = First(over, x => x.Kind == "section" && x.Title != null && x.Title.EndsWith("items bought"));
                Is((sub == null) == (bought == 0) && (sub == null || Lead(sub.Title) == bought), n + ": the overview counts " + (sub?.Title) + ", " + bought + " were bought");
                var put = ValueOf(smelters, x => x.Kind == "hero" && x.Title == "items put in");
                Is(Math.Abs(put - b.Events.SmelterAdded.Values.Sum()) < 0.5, n + ": " + put + " put in, " + b.Events.SmelterAdded.Values.Sum() + " counted");
                var bar = First(smelters, x => x.Kind == "composition");
                if (bar != null) Is(Math.Abs(bar.Items.Sum(i => Lead(i.Value)) - put) < 0.5, n + ": the ore, fuel and wood add up to " + bar.Items.Sum(i => Lead(i.Value)) + ", the put-in is " + put);
                var stations = First(smelters, x => x.Kind == "ledger");
                if (stations != null) Is(Math.Abs(stations.Items.SelectMany(r => r.Items).Sum(c => Lead(c.Value)) - put) < 0.5, n + ": the stations add up to " + stations.Items.SelectMany(r => r.Items).Sum(c => Lead(c.Value)) + ", the put-in is " + put);
                Is(ValueOf(over, x => x.Kind == "hero" && x.Title == "items put in") == put && ValueOf(over, x => x.Kind == "hero" && x.Title == "coins spent") == coins, n + ": the Hall overview is not the Trader and Smelters pages");
                // every item bought was bought from a trader who was paid
                foreach (var kv in b.Events.Bought) Is(b.Events.Spent.TryGetValue(kv.Key.Split('|')[0], out var paid) && paid > 0, n + ": " + kv.Key + " was bought from a trader who was paid nothing");
            }
        });

        // ---- the voyages add up from either end ----
        G("Voyages: foot = walking + running, sailed = helm + passenger, since install is part of the character's, and A with B is B with A", Is =>
        {
            foreach (var n in names)
            {
                var b = owns[n]; double C(string k) => b.Character.TryGetValue(k, out var v) ? v : 0;
                Is(Math.Abs(C("DistanceSailHelm") - Math.Min(C("DistanceSail"), C("DistanceSailHelm"))) < 0.1, n + ": more km at the helm than sailed");
                Is(C("DistanceSail") >= SampleWorld.SailedSince(n) * 1000 - 1 && C("DistanceSailHelm") >= SampleWorld.HelmSince(n) * 1000 - 1, n + ": fewer km sailed in the character's record than on the voyages since install");
                Is(C("DistanceTraveled") >= C("DistanceWalk") + C("DistanceRun") + C("DistanceSail") + C("DistanceAir"), n + ": travelled less than walked, run, sailed and flown");
                var playtime = C("TimeInBase") + C("TimeOutOfBase");
                Is(playtime * 8 >= C("DistanceWalk") + C("DistanceRun") + C("DistanceSail"), n + ": " + playtime / 3600 + " hours is too little time for the distance");
                var sailing = Show(b, Chapter.Voyages, "sailing"); var over = Show(b, Chapter.Voyages, "overview");
                var segs = All(over).Where(x => x.Kind == "segment" && (x.Id == "helm" || x.Id == "passenger")).Sum(x => Lead(x.Value));
                Is(Math.Abs(segs - Lead(First(over, x => x.Kind == "leg" && x.Id == "sail")?.Value)) < 0.11, n + ": helm + passenger " + segs + " is not the km sailed");
                var foot = All(Show(b, Chapter.Voyages, "onfoot")).Where(x => x.Kind == "segment" && (x.Id == "walk" || x.Id == "run")).Sum(x => Lead(x.Value));
                Is(Math.Abs(foot - Lead(First(over, x => x.Kind == "leg" && x.Id == "foot")?.Value)) < 0.11, n + ": walking + running " + foot + " is not the km on foot");
                Is(Math.Abs(Sailed(b) * 1000 - C("DistanceSail")) < 60, n + ": the Sailing hero says " + Sailed(b) + " km, the character " + C("DistanceSail") / 1000);
                // the crew: only fellows who share, and symmetric with theirs
                foreach (var o in names.Where(o => o != n))
                {
                    b.Events.SailedWith.TryGetValue(o, out var mine); owns[o].Events.SailedWith.TryGetValue(n, out var theirs);
                    Is(Math.Abs(mine - theirs) < 1, n + " sailed with " + o + " " + mine / 60 + " min, " + o + " with " + n + " " + theirs / 60);
                    b.Events.SailedUnderHelmOf.TryGetValue(o, out var under); owns[o].Events.SailedUnderHelmOf.TryGetValue(n, out var over2);
                    Is(under + over2 <= mine + 1, n + " and " + o + ": " + (under + over2) / 60 + " min under each other's helm is more than the " + mine / 60 + " min together");
                    Is((mine > 0) == (SampleWorld.WithSeconds(n, o) > 0) && Math.Abs(mine - SampleWorld.WithSeconds(n, o)) < 1 && Math.Abs(under - SampleWorld.UnderSeconds(n, o)) < 1, n + " with " + o + ": not the voyages' minutes");
                    Is(mine <= 0 || Lead(First(sailing, x => x.Kind == "person" && x.Id == o)?.Value) > 0, n + " sailed with " + o + " but the crew does not show them");
                }
                // the cargo: the hero, the bar and the average load are one tally; carried only by someone who steered or pulled a cart
                var hero = Cargo(b); var bar = First(Show(b, Chapter.Voyages, "cargo"), x => x.Kind == "composition" && x.Items.Any(i => (i.Id ?? "").StartsWith("$item")));
                Is(Math.Abs(b.Events.CargoMeters.Values.Sum() / 1000.0 - hero) < 0.5, n + ": the cargo hero says " + hero + ", the tally " + b.Events.CargoMeters.Values.Sum() / 1000.0);
                if (bar != null) Is(Math.Abs(bar.Items.Sum(i => Lead(i.Value)) - hero) < 1.01, n + ": the cargo bar adds up to " + bar.Items.Sum(i => Lead(i.Value)) + ", the hero says " + hero);
                var helmM = SampleWorld.HelmSince(n) * 1000; b.Events.CartMeters.TryGetValue("Cart", out var cart);
                foreach (var kv in b.Events.CargoStretch) Is(kv.Value <= helmM + cart + 1, n + ": " + kv.Key + " was carried over " + kv.Value + " m but the helm and the cart moved " + (helmM + cart));
                Is(b.Events.CargoMeters.Keys.All(k => b.Events.CargoStretch.ContainsKey(k)) && b.Events.CargoMeters.Count > 0 == (hero > 0), n + ": cargo without a stretch, or a hero without cargo");
                // Heavy Keel's best load can not be more than the metal and ore the average load shows aboard
                var metal = b.Events.CargoMeters.Where(kv => CargoVoyage.MetalOre.Contains(kv.Key) && b.Events.CargoStretch[kv.Key] >= CargoVoyage.LineMetres).Sum(kv => kv.Value / b.Events.CargoStretch[kv.Key]);
                Is(b.Feats.Count(CargoVoyage.BestKey) <= metal + 1, n + ": the heaviest load " + b.Feats.Count(CargoVoyage.BestKey) + " is more than the " + metal + " metal and ore on average");
                Is(b.Feats.Count(CargoVoyage.BestKey) == 0 || SampleWorld.HelmSince(n) >= 2, n + ": a heaviest load over 2 km without 2 km at the helm");
                // the leads
                if (b.Feats.Bests.TryGetValue(LedTracker.BestKey, out var ledBest)) Is(ledBest.Value <= b.Events.LedMeters.Values.Sum() && b.Events.LedMeters.ContainsKey(ledBest.What), n + ": the longest lead " + ledBest.Value + " is more than all the leads or of an animal not led");
            }
        });

        // ---- the server's book: what it says about a player is what the pages show, and it fits the cargo the group hauled ----
        G("the server's book: loaded and unloaded cargo and born near are shown as the book says, in every book, and no more is sent or delivered than the group hauled", Is =>
        {
            foreach (var item in owns.Values.SelectMany(o => o.Book?.Sent.Keys.Concat(o.Book.Delivered.Keys) ?? Enumerable.Empty<string>()).Distinct())
            {
                var hauled = owns.Values.Sum(o => o.Events.CargoMeters.TryGetValue(item, out var m) ? m : 0);
                var sent = owns.Values.Sum(o => o.Book != null && o.Book.Sent.TryGetValue(item, out var v) ? v : 0); var delivered = owns.Values.Sum(o => o.Book != null && o.Book.Delivered.TryGetValue(item, out var v) ? v : 0);
                Is(sent <= hauled, item + ": " + sent / 1000 + " item-km loaded, the four of them hauled " + hauled / 1000);
                Is(delivered <= hauled, item + ": " + delivered / 1000 + " item-km unloaded, the four of them hauled " + hauled / 1000);
            }
            foreach (var n in names)
            {
                var own = owns[n]; var copy = copies[n];
                Is((own.Book == null) == (copy.Book == null), n + ": the copy and the book disagree on having a server book");
                if (own.Book == null) { Is(!All(Show(own, Chapter.Voyages, "cargo")).Any(b => b.Title == "item-km loaded" || b.Title == "item-km unloaded"), n + ": no server book but the Cargo page shows its numbers"); continue; }
                foreach (var who in new[] { own, copy })
                {
                    var sailing = Show(who, Chapter.Voyages, "cargo");
                    var sent = All(sailing).FirstOrDefault(b => b.Title == "item-km loaded"); var delivered = All(sailing).FirstOrDefault(b => b.Title == "item-km unloaded");
                    Is((own.Book.SentTotal > 0) == (sent != null) && (sent == null || Math.Abs(Lead(sent.Value) - own.Book.SentTotal / 1000.0) < 0.51), n + (who.IsSelf ? "" : " (copy)") + ": Cargo says " + sent?.Value + " item-km loaded, the book " + own.Book.SentTotal / 1000.0);
                    Is((own.Book.DeliveredTotal > 0) == (delivered != null) && (delivered == null || Math.Abs(Lead(delivered.Value) - own.Book.DeliveredTotal / 1000.0) < 0.51), n + (who.IsSelf ? "" : " (copy)") + ": Cargo says " + delivered?.Value + " item-km unloaded, the book " + own.Book.DeliveredTotal / 1000.0);
                    var born = First(Show(who, Chapter.Deeds, "taming"), b => b.Kind == "section" && b.Src == "server");
                    Is((own.Book.BornTotal > 0) == (born != null) && (born == null || Math.Abs(Lead(born.Value) - own.Book.BornTotal) < 0.01), n + (who.IsSelf ? "" : " (copy)") + ": Taming says " + born?.Value + " born near, the book " + own.Book.BornTotal);
                }
            }
        });

        // ---- the skills are the things done ----
        G("Skills: practised since install is what was done (hits, blocks, dishes, bites), the shares add up, a level is the same on every page", Is =>
        {
            foreach (var n in names)
            {
                var b = owns[n]; var ev = b.Events; double P(string k) => ev.SkillPractice.TryGetValue(k, out var v) ? v : 0;
                Is(Math.Abs(P("WoodCutting") - ev.ChopHits.Values.Sum()) < 0.01, n + ": WoodCutting practice " + P("WoodCutting") + " is not the " + ev.ChopHits.Values.Sum() + " axe hits");
                Is(Math.Abs(P("Pickaxes") - ev.PickaxeHits.Values.Sum()) < 0.01, n + ": Pickaxes practice " + P("Pickaxes") + " is not the " + ev.PickaxeHits.Values.Sum() + " pickaxe hits");
                Is(Math.Abs(P("Blocking") - ev.Blocks) < 0.01, n + ": Blocking practice " + P("Blocking") + " is not the " + ev.Blocks + " blocks");
                Is(Math.Abs(P("Cooking") - ev.Made.Where(kv => b.ItemKind(kv.Key) == "food").Sum(kv => kv.Value)) < 0.01, n + ": Cooking practice " + P("Cooking") + " is not the dishes made since install");
                var weapons = new[] { "Swords", "Knives", "Clubs", "Spears", "Axes", "Bows", "ElementalMagic", "Polearms", "Unarmed" };
                ev.Battle.TryGetValue("EnemyHits", out var hits);
                Is(Math.Abs(weapons.Sum(P) - hits) < 0.01, n + ": weapon practice " + weapons.Sum(P) + " is not the " + hits + " hits on foes");
                if (b.Baseline != null && b.Baseline.TryGetValue(LocalTotals.StatsKind, out var sb) && sb.ContainsKey("FishHooked")) Is(Math.Abs(P("Fishing") - (b.Character["FishHooked"] - sb["FishHooked"])) < 0.01, n + ": Fishing practice " + P("Fishing") + " is not the bites since install");
                Is(P("Jump") <= (b.Character.TryGetValue("Jumps", out var jumps) ? jumps : 0), n + ": more jump practice than jumps");
                // every skill practised is a skill the character has, and the shares in the page add up
                foreach (var kv in ev.SkillPractice.Where(kv => kv.Value > 0)) Is(b.SkillLevels.ContainsKey(kv.Key), n + ": practised " + kv.Key + " without a level");
                var practised = Show(b, Chapter.Skills, "overview", st => st.View["Skills/overview/view"] = "practised");
                var shares = All(practised).Where(x => x.Kind == "bar" || x.Kind == "ranking").SelectMany(x => x.Items ?? new List<Block>()).Where(x => (x.Value ?? "").Contains("%")).Sum(x => Lead(x.Value));
                Is(shares == 0 || Math.Abs(shares - 100) <= ev.SkillPractice.Count(kv => kv.Value > 0) * 0.5 + 0.5, n + ": the shares of practice add up to " + shares + " %");
                // a skill's level is one number on every page that shows it
                foreach (var kv in b.SkillLevels)
                {
                    var level = Math.Floor(kv.Value);
                    foreach (var (ch, page) in new[] { (Chapter.Skills, "overview"), (Chapter.Skills, kv.Key), (Chapter.Voyages, "onfoot"), (Chapter.Battle, "damage"), (Chapter.Deeds, "woodcutting"), (Chapter.Deeds, "mining"), (Chapter.Deeds, "fishing"), (Chapter.Deeds, "cooking") })
                    {
                        var v = Show(b, ch, page);
                        foreach (var l in All(v).Where(x => (x.Kind == "ladder") && x.Id == kv.Key)) Is(Math.Abs(l.Level - level) < 0.01, n + ": " + kv.Key + " is level " + l.Level + " on " + ch + "/" + page + ", " + level + " in the character");
                    }
                }
            }
        });

        // ---- the Deeds data adds up in every book ----
        G("Deeds: gear by kind = the gear grid, dishes by kind = dishes cooked, built = the pieces, picked = the crops, caught = the fish by kind, in every book", Is =>
        {
            foreach (var n in names)
            {
                var b = owns[n];
                double Cn(string k) => b.Character.TryGetValue(k, out var v) ? v : 0;
                var crafting = Show(b, Chapter.Deeds, "crafting"); var gearHero = ValueOf(crafting, x => x.Kind == "hero" && x.Title == "gear crafted");
                var grid = First(crafting, x => x.Kind == "itemgrid");
                Is(Math.Abs(gearHero - Cn("CraftWeapon") - Cn("CraftArmor") - Cn("CraftTool") - Cn("CraftTrinket")) < 0.01, n + ": gear crafted " + gearHero + " is not the four craft counters");
                Is(grid == null ? gearHero == 0 : Math.Abs(grid.Items.Sum(i => Lead(i.Value)) - gearHero) < 0.01, n + ": the gear grid adds up to " + (grid?.Items.Sum(i => Lead(i.Value))) + ", gear crafted is " + gearHero);
                // the kinds the grid counts are the kinds the counters count
                double Kind(params string[] types) => b.ItemsCrafted.Where(kv => b.ItemType(kv.Key) != null && types.Contains(b.ItemType(kv.Key))).Sum(kv => kv.Value);
                Is(Kind("OneHandedWeapon", "TwoHandedWeapon", "Bow", "Torch") == Cn("CraftWeapon") && Kind("Shield", "Helmet", "Chest", "Legs", "Shoulder") == Cn("CraftArmor") && Kind("Tool") == Cn("CraftTool") && Kind("Trinket") == Cn("CraftTrinket"),
                   n + ": the gear by item type does not add up to the craft counters");
                var cooking = Show(b, Chapter.Deeds, "cooking"); var dishes = Cooked(b);
                Is(Math.Abs(dishes - Cn("CraftFood") - Cn("CraftGrill")) < 0.01, n + ": dishes cooked " + dishes + " is not CraftFood + CraftGrill " + (Cn("CraftFood") + Cn("CraftGrill")));
                var kinds = First(cooking, x => x.Kind == "composition" && x.Title == "Dishes by kind");
                Is(kinds == null ? dishes == 0 : Math.Abs(kinds.Items.Sum(i => Lead(i.Value)) - dishes) < 0.01, n + ": the dishes by kind add up to " + (kinds?.Items.Sum(i => Lead(i.Value))) + ", dishes cooked is " + dishes);
                var ledger = First(cooking, x => x.Kind == "ledger");
                if (ledger != null) Is(Math.Abs(ledger.Items.Sum(r => Lead(r.Value)) - dishes) < 0.01, n + ": the kitchen ledger adds up to " + ledger.Items.Sum(r => Lead(r.Value)) + ", dishes cooked is " + dishes);
                var building = Show(b, Chapter.Deeds, "building"); var pieces = Built(b);
                var pieceGrid = First(building, x => x.Kind == "itemgrid");
                Is(pieceGrid == null ? pieces == 0 : Math.Abs(pieceGrid.Items.Sum(i => Lead(i.Value)) - pieces) < 0.01, n + ": the pieces add up to " + (pieceGrid?.Items.Sum(i => Lead(i.Value))) + ", built is " + pieces);
                var ground = ValueOf(Show(b, Chapter.Deeds, "groundwork"), x => x.Kind == "hero");
                Is(Math.Abs(ground - b.PiecesPlaced.Where(kv => PanelModel.PieceKindByName(kv.Key) == "ground").Sum(kv => kv.Value)) < 0.01, n + ": groundwork " + ground + " is not the ground pieces");
                var wood = Show(b, Chapter.Deeds, "woodcutting"); var felled = ValueOf(wood, x => x.Kind == "hero" && x.Title == "trees felled");
                Is(Math.Abs(felled - Cn("Tree")) < 0.01 || (b.Baseline != null && Math.Abs(felled - (b.Baseline[PanelModel.TreesBaseline]["Tree"] + b.Events.Felled.Values.Sum())) < 0.01), n + ": trees felled " + felled + " is not the game's counter nor the install counter + felled since");
                var perTree = First(wood, x => x.Kind == "ranking" && x.Items.Any() && x.Items.All(i => i.Value != null));
                Is(Math.Abs(ValueOf(wood, x => x.Kind == "hero" && x.Title == "axe hits") - b.Events.ChopHits.Values.Sum()) < 0.01, n + ": the axe hits are not the counted ones");
                Is(Math.Abs(ValueOf(Show(b, Chapter.Deeds, "mining"), x => x.Kind == "hero" && x.Title == "pickaxe hits") - b.Events.PickaxeHits.Values.Sum()) < 0.01 || b.Events.PickaxeHits.Count == 0, n + ": the pickaxe hits are not the counted ones");
                Is(b.Events.Felled.Values.Sum() <= Math.Max(Cn("Tree"), b.Baseline == null ? 0 : b.Baseline[PanelModel.TreesBaseline]["Tree"] + b.Events.Felled.Values.Sum()), n + ": more trees felled since install than in the character's record");
                // brought in: the game's counter now sits between the install counter and the install counter plus what Hearthwoven counted since; a fellow has no install counter
                foreach (var kv in b.ItemsPickedUp)
                {
                    b.Events.PickedUp.TryGetValue(kv.Key, out var exact);
                    if (b.Baseline != null && b.Baseline.TryGetValue("pickedUp", out var atInstall) && atInstall.ContainsKey(kv.Key)) { atInstall.TryGetValue(kv.Key, out var was); Is(kv.Value >= was - 0.01 && kv.Value <= was + exact + 0.01 || kv.Value == was, n + ": " + kv.Key + " counter " + kv.Value + " is outside [" + was + ", " + (was + exact) + "]"); }
                    else Is(kv.Value >= exact, n + ": " + kv.Key + " counted " + exact + " since install, the game's counter says " + kv.Value);
                }
                // food and gear that others enjoyed or put on were made by the maker, and not more than they made
                foreach (var eater in names.Where(o => o != n))
                    foreach (var kv in owns[eater].Events.AteFoodMadeBy)
                    {
                        var p = kv.Key.Split('|'); if (p[0] != n) continue;
                        var dishName = b.DisplayName(p[1]);
                        var made = b.ItemsCrafted.Where(m => b.DisplayName(m.Key) == dishName && b.ItemKind(m.Key) == "food").Sum(m => m.Value);
                        var eaten = names.Where(o => o != n).Sum(o => owns[o].Events.AteFoodMadeBy.TryGetValue(kv.Key, out var e) ? e : 0);
                        Is(made > 0 && eaten <= made, n + ": " + eaten + " servings of " + dishName + " eaten, " + made + " made");
                    }
                foreach (var wearer in names.Where(o => o != n))
                    foreach (var kv in owns[wearer].Events.EquippedGearMadeBy)
                    {
                        var p = kv.Key.Split('|'); if (p[0] != n) continue;
                        var itemName = b.DisplayName(p[1]);
                        Is(b.ItemsCrafted.Any(m => b.DisplayName(m.Key) == itemName && m.Value > 0), n + ": " + wearer + " put on " + itemName + " that " + n + " never crafted");
                    }
                foreach (var eater in names.Where(o => o != n))
                    foreach (var kv in owns[eater].Events.AteFromFeastOf)
                    {
                        var p = kv.Key.Split('|'); if (p[0] != owns[n].PlayerId.ToString()) continue;
                        var placed = b.PiecesPlaced.Where(m => m.Key.StartsWith("$piece_feast") && b.DisplayName(m.Key) == b.DisplayName(p[1])).Sum(m => m.Value);
                        Is(placed > 0 && kv.Value <= placed * 8, n + ": " + kv.Value + " servings of " + b.DisplayName(p[1]) + " from " + placed + " set out");
                    }
                // crops and fish (the existing identities, in every book that has them)
                var fishing = Show(b, Chapter.Deeds, "fishing"); var rank = First(fishing, x => x.Kind == "ranking" && x.Items.Any(i => i.Title == "Hooked"));
                if (rank != null) Is(Lead(rank.Items.First(i => i.Title == "Hooked").Value) >= Lead(rank.Items.First(i => i.Title == "Caught").Value) + Lead(rank.Items.First(i => i.Title == "Got away").Value) && rank.Items.Count == 3, n + ": hooked is at least caught + got away, and only the game's three fishing counters are rows");
            }
        });

        // ---- the Company tells one story of the food and the gear ----
        G("Company: the food, the feasts and the gear are one ledger on the Cooking and Crafting pages, Fireside, Food shared and Gear shared, in every book", Is =>
        {
            foreach (var n in names)
            {
                var b = owns[n];
                // what others enjoyed of n's food: Cooking page = Food shared's right end = Fireside's threads from n = the card
                var food = Show(b, Chapter.Company, "food");
                var right = All(food).Where(x => x.Kind == "end" && x.Tone == "right").Sum(x => Lead(x.Value)); var left = All(food).Where(x => x.Kind == "end" && x.Tone == "left").Sum(x => Lead(x.Value));
                var cookingHead = First(Show(b, Chapter.Deeds, "cooking"), x => x.Kind == "section" && x.Title != null && x.Title.StartsWith("Who enjoyed"));
                Is((cookingHead == null ? 0 : Lead(cookingHead.Value)) == right, n + ": Cooking says " + cookingHead?.Value + " enjoyed, Food shared " + right);
                var gifts = All(Show(b, Chapter.Company, "fireside")).Where(x => x.Kind == "gift" && x.Tone == "food").ToList();
                Is(gifts.Where(g => g.Title == n).Sum(g => Lead(g.Value)) == right && gifts.Where(g => g.Text == n).Sum(g => Lead(g.Value)) == left, n + ": Fireside's food threads are not Food shared's " + right + " and " + left);
                var card = First(Show(b, Chapter.Deeds, "overview"), x => x.Kind == "number" && x.Title == "enjoyed by fellows");
                Is((card == null ? 0 : Lead(card.Value)) == right, n + ": the Hearth Cook card says " + card?.Value + ", Food shared " + right);
                // what the others' own records say they ate of n's, summed from outside
                var ate = names.Where(o => o != n).Sum(o => owns[o].Events.AteFoodMadeBy.Where(kv => kv.Key.StartsWith(n + "|")).Sum(kv => kv.Value) + owns[o].Events.AteFromFeastOf.Where(kv => kv.Key.StartsWith(b.PlayerId + "|")).Sum(kv => kv.Value));
                Is(ate == right, n + ": the others' records say " + ate + " servings enjoyed, Food shared " + right);
                // gear
                var gear = Show(b, Chapter.Company, "gear");
                var theirs = All(gear).Where(x => x.Kind == "row" && x.Tone == "right").SelectMany(r => r.Items).Count();
                var crafting = First(Show(b, Chapter.Deeds, "crafting"), x => x.Kind == "people");
                Is((crafting == null ? 0 : crafting.Items.SelectMany(p => p.Items).Count()) == theirs, n + ": Crafting says " + (crafting?.Items.SelectMany(p => p.Items).Count() ?? 0) + " pieces put to good use, Gear shared " + theirs);
                // Fireside's gear threads and Gear shared's tiles: one count (times put to good use), each way, per fellow player
                var gearThreads = All(Show(b, Chapter.Company, "fireside")).Where(x => x.Kind == "gift" && x.Tone == "gear").ToList();
                foreach (var row in All(gear).Where(x => x.Kind == "row"))
                {
                    var tiles = row.Items.Sum(t => Lead(t.Value));
                    var thread = gearThreads.FirstOrDefault(g => row.Tone == "right" ? g.Title == n && g.Text == row.Id : g.Title == row.Id && g.Text == n);
                    Is(thread != null && Lead(thread.Value) == tiles, n + ": Fireside says " + thread?.Value + " gear " + (row.Tone == "right" ? "from " + n + " to " + row.Id : "from " + row.Id + " to " + n) + ", Gear shared's tiles add up to " + tiles);
                }
                Is(gearThreads.Where(g => g.Title == n || g.Text == n).Count() == All(gear).Count(x => x.Kind == "row"), n + ": Fireside has " + gearThreads.Count(g => g.Title == n || g.Text == n) + " gear threads of " + n + "'s, Gear shared " + All(gear).Count(x => x.Kind == "row") + " rows");
            }
        });

        // ---- the sums of every composition ----
        G("every bar adds up to its total, on every page of every book", Is =>
        {
            foreach (var viewer in names)
                foreach (var (who, book) in new[] { (viewer, owns[viewer]) }.Concat(viewer == SampleWorld.Rowan ? names.Where(x => x != viewer).Select(x => (x + " (copy)", copies[x])) : Enumerable.Empty<(string, PanelInput)>()))
                    foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
                        foreach (var l in PanelModel.Build(book, new PanelState { Chapter = ch }).List)
                        {
                            var v = Show(book, ch, l.Id);
                            foreach (var c in All(v).Where(x => x.Kind == "composition" && !string.IsNullOrEmpty(x.Value) && x.Items != null && x.Items.Count > 0))
                            {
                                if (!c.Items.All(i => !string.IsNullOrEmpty(i.Value) && Regex.IsMatch(i.Value.Trim(), @"^[0-9   ,]+$")) || !Regex.IsMatch(c.Value.Trim(), @"^[0-9   ,]+$")) continue;
                                Is(Math.Abs(c.Items.Sum(i => Lead(i.Value)) - Lead(c.Value)) < 0.5, who + " " + ch + "/" + l.Id + ": " + c.Title + " adds up to " + c.Items.Sum(i => Lead(i.Value)) + ", it says " + c.Value);
                            }
                        }
        });

        // ---- a fellow's book never talks to the reader about them ----
        G("a fellow's book never says you or your about them (the viewer's own chip and the Company's '(you)' aside)", Is =>
        {
            var yous = new Regex(@"\b(you|your|yours|You|Your|Yours)\b");
            foreach (var q in names.Where(n => n != SampleWorld.Rowan))
            {
                var book = copies[q]; book.Fellows = owns.Where(kv => kv.Key != q).Select(kv => kv.Key == SampleWorld.Rowan ? kv.Value : copies[kv.Key]).ToList();
                foreach (var (name, v) in SampleTests.AllPages(book).Where(p => p.name != "about"))   // About is the viewer's own page (their settings), never a fellow's
                {
                    IEnumerable<string> Of(Block b) => new[] { b.Title, b.Value, b.Text, b.Note, b.Value2 }.Concat((b.Items ?? new List<Block>()).SelectMany(Of));
                    foreach (var t in v.Blocks.SelectMany(Of).Concat(new[] { v.Heading, v.Scope }).Where(s => !string.IsNullOrEmpty(s)).Distinct())
                        Is(!yous.IsMatch(t.Replace("(you)", "").Replace("Born in Your Care", "")), q + " " + name + ": \"" + t + "\"");
                }
            }
        });

        // ---- About says what the pages say ----
        G("About and the pages say the same about when each source starts: the character since it was made, this PC since install, never a character number marked since install", Is =>
        {
            foreach (var n in names)
            {
                var b = owns[n];
                var about = PanelModel.Build(b, new PanelState { ShowAbout = true });
                var origins = First(about, x => x.Kind == "origins");
                Is(origins.Items.First(o => o.Tone == PanelModel.SrcCharacter).Value == PanelModel.MadeLine(b), n + ": About's character card says " + origins.Items.First(o => o.Tone == PanelModel.SrcCharacter).Value);
                var madeOn = b.CharacterMade.Value.AddHours(2).ToString("d MMM", CultureInfo.InvariantCulture); var installedOn = b.InstalledUtc.Value.AddHours(2).ToString("d MMM", CultureInfo.InvariantCulture);
                foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
                    foreach (var l in PanelModel.Build(b, new PanelState { Chapter = ch }).List)
                    {
                        var v = Show(b, ch, l.Id);
                        foreach (var z in All(v).Where(x => x.Kind == "zone"))
                        {
                            var line = string.Join(" ", new[] { z.Title, z.Value, z.Value2, z.Text, z.Note }.Where(t => !string.IsNullOrEmpty(t)));
                            if (z.Id == "character") Is(line.Contains(PanelModel.MadeLine(b)) && line.Contains(madeOn), n + " " + ch + "/" + l.Id + ": the character zone says \"" + line + "\", made " + madeOn);
                            if (z.Id == "pc") Is(line.Contains("since " + installedOn) || !line.Contains("since 1") && !Regex.IsMatch(line, @"since \d"), n + " " + ch + "/" + l.Id + ": the install zone says \"" + line + "\", installed " + installedOn);
                        }
                        foreach (var x in All(v).Where(x => x.Src == PanelModel.SrcCharacter)) Is(!x.SinceInstall, n + " " + ch + "/" + l.Id + ": a number from the character's record (" + x.Title + " " + x.Value + ") is marked since install");
                    }
            }
        });

        // ---- the clock ----
        G("the clock: the character was made before Hearthwoven was installed, before now; fellows' last records are within the session", Is =>
        {
            foreach (var n in names)
            {
                var b = owns[n];
                Is(b.CharacterMade < b.InstalledUtc && b.InstalledUtc < now && b.SessionStartUtc > b.InstalledUtc, n + ": made " + b.CharacterMade + ", installed " + b.InstalledUtc + ", session " + b.SessionStartUtc);
                var c = copies[n]; Is(!c.LastRecordedUtc.HasValue || (c.LastRecordedUtc.Value <= now && c.LastRecordedUtc.Value >= b.SessionStartUtc), n + ": the copy's last record " + c.LastRecordedUtc + " is outside the session");
            }
        });
        return results;
    }

    /// <summary>The minutes this page's book spent under a player's helm (the "Under the helm of" bar), or 0.</summary>
    static double ValueOfUnder(PanelView sailing, string helm)
    {
        var bar = First(sailing, x => x.Kind == "composition" && x.Title == "Under the helm of");
        var part = bar?.Items.FirstOrDefault(i => i.Id == helm);
        if (part == null) return 0;
        var m = Regex.Match(part.Value, @"(?:(\d+) hours?)?\s*(?:(\d+) min)?");
        return (m.Groups[1].Success ? double.Parse(m.Groups[1].Value) * 60 : 0) + (m.Groups[2].Success ? double.Parse(m.Groups[2].Value) : 0);
    }

    static int RunWorld()
    {
        var now = new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc);
        var (owns, copies) = SampleWorld.Make(now);
        foreach (var (name, problems, checks) in WorldChecks(owns, copies, now))
        {
            if (problems.Count > 0) foreach (var p in problems.Distinct().Take(40)) System.Console.WriteLine("  problem: " + p);
            Check(problems.Count == 0 && checks > 0, "world: " + name + " (" + checks + " checks" + (problems.Count > 0 ? ", " + problems.Distinct().Count() + " problems" : "") + ")");
        }
        // the checks catch what they are for: each break below must be found by the group named
        void Breaks(string what, string group, Action<Dictionary<string, PanelInput>, Dictionary<string, PanelInput>> mutate)
        {
            var (o, c) = SampleWorld.Make(now); mutate(o, c);
            var hit = WorldChecks(o, c, now).Where(r => r.name.StartsWith(group) && r.problems.Count > 0).ToList();
            Check(hit.Count > 0, "world breaks: " + what + " is found (" + (hit.Count > 0 ? hit[0].problems.Count + " problems in '" + group + "'" : "not found") + ")");
        }
        Breaks("Edda sailed 5 minutes longer with Rowan than Rowan with her", "Voyages", (o, c) => o["Edda"].Events.SailedWith["Rowan"] += 300);
        Breaks("a feat tier added by hand (Ore Road III for Rowan)", "feats: the ledger", (o, c) => o["Rowan"].Feats.Earn("oreroad", 3, new FeatMoment { Utc = now.AddDays(-1) }));
        Breaks("a fight in the last minutes that is not in the damage since install", "a session is part", (o, c) => o["Tor"].Log.AddDamage(now.AddMinutes(-3), "Swamp", true, "Draugr", "Clubs", new HitData.DamageTypes { m_blunt = 500 }));
        Breaks("a nineteenth Blocking practice nobody did", "Skills", (o, c) => SessionEvents.Add(o["Tor"].Events.SkillPractice, "Blocking", 10));
        Breaks("Edda's shared copy says more dishes than her book", "Together", (o, c) => c["Edda"].Character["CraftFood"] += 5);
        Breaks("a copy that lost a finished feat", "feats: the ledger", (o, c) => c["Tor"].Feats.Earned.Remove("shieldwall"));
        Breaks("a meal eaten that nobody made", "Deeds: gear by kind", (o, c) => SessionEvents.Add(o["Finch"].Events.AteFoodMadeBy, "Edda|Bread", 99));
        Breaks("something bought from a trader who was paid nothing", "Hall", (o, c) => o["Rowan"].Events.Spent.Remove("Hildir"));
        Breaks("Edda delivered more iron than the four of them hauled", "the server's book", (o, c) => o["Edda"].Book.Delivered["$item_ironscrap"] = 9000000);
        Breaks("a copy that shows more born near than the book", "the server's book", (o, c) => c["Tor"].Book = new ServerBook.Shared { BornFrom = o["Tor"].Book.BornFrom, CargoFrom = o["Tor"].Book.CargoFrom });
        Breaks("cargo carried over more metres than the helm and the cart moved", "Voyages", (o, c) => o["Tor"].Events.CargoStretch["$item_wood"] = 90000);
        return 0;
    }
}
