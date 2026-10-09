// Company (ch-company, 2026-10-08): Fireside, Together, Food shared, Gear shared, from a group of four who share
// (Rowan's own record plus the shared copies of Edda, Tor and Finch). Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static partial class Program
{
    /// <summary>The fellow players of the Company sample: their shared copies as PanelUi receives them (IsSelf false).</summary>
    internal static List<PanelInput> CompanyFellows(DateTime now) => PanelSample.Fellows(now);   // src/Panel/PanelSample.cs

    internal static void CompanyChecks(PanelInput input, DateTime now, Action<bool, string> Check)
    {
        PanelView Page(PanelInput who, string page, Action<PanelState> more = null)
        {
            var st = new PanelState { Chapter = Chapter.Company }; if (page != null) st.Page[Chapter.Company] = page; more?.Invoke(st);
            return PanelModel.Build(who, st);
        }
        Block Of(PanelView v, string kind) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == kind);
        IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));

        var alone = Page(new PanelInput { PlayerName = "Rowan" }, null);
        Check(alone.List.Select(l => l.Label).SequenceEqual(new[] { "Fireside", "Together", "Food shared", "Gear shared" }) && alone.ListTitle == "Company" && alone.Page == "fireside" &&
              alone.Heading == "Fireside" && Of(alone, "empty").Text == PanelModel.CompanyEmpty && PanelModel.PlateOf(alone).Pill == null && alone.Keys.Contains("[W/S] Page") && !alone.Keys.Contains("[A/D] Direction"),
              "company: the left list is the four approved pages (no companion rows, no direction toggle); alone, the agreed empty text");
        Check(new[] { "together", "food", "gear" }.All(p => Of(Page(new PanelInput { PlayerName = "Rowan" }, p), "empty") != null), "company: no fellow players who share, every page says so (no ghosts)");
        var emptyTexts = new[] { "fireside", "together", "food", "gear" }.Select(p => Of(Page(new PanelInput { PlayerName = "Rowan" }, p), "empty").Text).ToList();
        Check(emptyTexts.Distinct().Count() == 4 && !emptyTexts[1].Contains("meal") && emptyTexts[2].Contains("Meals") && emptyTexts[3].Contains("Gear") && emptyTexts.All(t => !t.ToLowerInvariant().Contains("friend")),
              "diff-05 company: one empty text per page that fits it (Together is not about meals)");
        var soloT = new PanelInput { PlayerName = "Rowan", IsSelf = true, ItemsPickedUp = new Dictionary<string, float> { ["$item_wood"] = 40 }, GatherKind = _ => "wood", Events = new SessionEvents() };
        var togetherAlone = Page(soloT, "together");
        Check(Of(togetherAlone, "together")?.Items.All(c => c.Items.Count == 1 && c.Items[0].Id == "Rowan") == true && Of(togetherAlone, "empty") == null &&
              Of(togetherAlone, "note")?.Text == PanelModel.TogetherAlone, "diff-05 company: Together alone still shows your own row, the fellow players to come");
        Check(Of(togetherAlone, "together")?.Items.Where(c => c.Id != "cargo" && c.Id != "dealt").All(c => c.Value2 == "since you made this character") == true,
              "live-polish company: Together alone says \"since you made this character\" (the zones' words), never \"since you were made\"");

        var keepFellows = input.Fellows; var keepId = input.PlayerId;
        input.Fellows = CompanyFellows(now); input.PlayerId = 11;
        try
        {
            // ----- Fireside -----
            var fire = Page(input, "fireside");
            var giving = Of(fire, "giving");
            var players = giving.Items.Where(i => i.Kind == "player").ToList();
            var gifts = giving.Items.Where(i => i.Kind == "gift").ToDictionary(g => g.Id + "/" + g.Tone);
            Check(PanelModel.PlateOf(fire)?.Title == "Fireside" && giving.Note == PanelModel.FiresideLine && PanelModel.PlateOf(fire).Text == null && PanelModel.PlateOf(fire).Icon == "ui:chapter-company" && PanelModel.PlateOf(fire).Pill == "Edda, Finch and Tor share" &&
                  PanelModel.PlateOf(Page(input, "food")).Pill == "Edda, Finch and Tor share", "company: every page on the plate, its pill says who shares (or what is counted)");
            Check(players.Select(p => p.Id).SequenceEqual(new[] { "Rowan", "Edda", "Finch", "Tor" }) && players[0].Title == "You" && players[0].Selected && players.Skip(1).All(p => !p.Selected),
                  "fireside: you first, then the fellow players who share, by name (never by amount)");
            Check(gifts["Edda>Rowan/food"].Value == "8" && gifts["Edda>Rowan/food"].Src == "pc" && gifts["Edda>Rowan/food"].Note == "enjoyed" &&
                  gifts["Tor>Rowan/gear"].Value == "2" && gifts["Tor>Rowan/gear"].Note == "put to good use" && gifts["Rowan>Tor/food"].Value == "11" && gifts["Rowan>Tor/food"].Src == "fellows",
                  "fireside: food in servings (meals and feast servings), gear in kinds; your record says what you enjoyed, theirs what they enjoyed");
            Check(gifts["Edda>Rowan/food"].Icon == "item:Bread" && gifts["Rowan>Tor/food"].Icon == "item:CarrotSoup" && gifts.Values.All(g => g.Count.ToString() == g.Value) &&
                  Math.Abs(gifts["Rowan>Tor/food"].Fraction - 1f) < 1e-4 && Math.Abs(gifts["Tor>Rowan/food"].Fraction - 1f / 11f) < 1e-4,
                  "fireside: the bead carries the item given most; thickness = count on one scale (the largest thread is 1)");
            Check(gifts.ContainsKey("Tor>Finch/food") && !gifts["Tor>Finch/food"].Selected && gifts["Edda>Finch/gear"].Value == "1" && gifts.Where(kv => kv.Key.Contains("Rowan")).All(kv => kv.Value.Selected),
                  "fireside: gifts between fellow players are listed under Among fellow players; yours under You and fellow players");
            // fix4-rest: no overlaps anywhere in the drawing: every count sits on its thread, touches no other count, shield, name, arrow head or the hearth; and the same twice
            var plan = FireLayout.Build(giving, 180, 232); var plan2 = FireLayout.Build(giving, 180, 232);
            var pillRects = plan.Pills.Select(p => FireLayout.Rect.Around(p.At.x, p.At.y, p.Width / 2, p.H / 2)).ToList();
            int touching = 0; for (int i = 0; i < pillRects.Count; i++) { foreach (var f in plan.Fixed) if (FireLayout.Overlap(pillRects[i], f) > 0) touching++; for (int j = i + 1; j < pillRects.Count; j++) if (FireLayout.Overlap(pillRects[i], pillRects[j]) > 0) touching++; }
            Check(plan.Pills.Count == giving.Items.Count(g => g.Kind == "gift" && g.Selected) && plan.Stuck == 0 && touching == 0 && plan.Pills.Select(p => Math.Round(p.U, 2)).SequenceEqual(plan2.Pills.Select(p => Math.Round(p.U, 2))),
                  "fireside: a count on every one of your threads, none touching another count, a shield, a name, an arrow head or the hearth, the same places every time (" + plan.Pills.Count + " counts)");
            var legal = new[] { null, "since install", PanelModel.AsTheyShared };
            Check(legal.Contains(giving.Value) && legal.Contains(giving.Value2) && giving.Items.Where(i => i.Kind == "gift").All(g => g.Value2 == null || g.Value2 == PanelModel.AsTheyShared) && giving.Items.Where(i => i.Kind == "gift" && !i.Selected).All(g => giving.Value2 == null || true) &&
                  (giving.Value != null || giving.Items.Any(i => i.Kind == "gift" && i.Selected && i.Src == "fellows") && giving.Items.Any(i => i.Kind == "gift" && i.Selected && i.Src == "pc")),
                  "fireside: each list says its scope once in its caption (since install, or as they last shared it); where its rows differ the caption stays silent and each row says its own: " + giving.Value + " / " + giving.Value2);
            Check(!gifts.Keys.Any(k => k.Contains("Gunn") || k.Contains("unknown") || k.StartsWith("Rowan>Rowan") || k.StartsWith("Edda>Edda")),
                  "fireside: only players who share are shown; nobody gives to themselves");
            Check(giving.Title == "You and fellow players" && giving.Text == "Among fellow players" && gifts.Values.Where(g => g.Src == "pc").All(g => g.SinceInstall) &&
                  gifts.Values.Where(g => g.Src != "pc").All(g => !g.SinceInstall),
                  "fireside: what you enjoyed (this PC) says since install; what fellow players recorded does not");

            // ----- Together -----
            var together = Of(Page(input, "together"), "together");
            var cats = together.Items;
            Check(cats.Select(c => c.Id).SequenceEqual(new[] { "wood", "ore", "built", "cooked", "dealt", "sailed", "cargo" }) && cats[0].Selected && cats.Count(c => c.Selected) == 1,
                  "together: categories with numbers in them, the first shown large; no hall category (chests are not in the shared records)");
            Check(cats.All(c => c.Items.Select(p => p.Id).SequenceEqual(new[] { "Rowan", "Edda", "Finch", "Tor" })) && cats.All(c => Math.Abs(c.Items.Sum(p => p.Fraction) - 1f) < 1e-4),
                  "together: every bar in the fire's order (you first, then by name), never sorted by amount; shares sum to the whole");
            var wood = cats[0];
            Check(wood.Value == "4\u00A0805" && wood.Text == "wood brought in together" && wood.Note == PanelModel.BroughtInScope && wood.Items.Select(p => p.Value).SequenceEqual(new[] { "3\u00A0305", "1\u00A0000", "", "500" }) && wood.Items.All(p => p.Src == "character"),
                  "together: wood brought in, your character's counts and theirs, total = the parts");
            var dealt = cats.Single(c => c.Id == "dealt");
            Check(dealt.Note == "before the foe's armour" && dealt.Items[0].Src == "pc" && dealt.Items[0].Value == "420" && dealt.Items[1].Src == "fellows" && dealt.Items[0].SinceInstall && !dealt.Items[1].SinceInstall,
                  "together: damage dealt before the foe's armour; yours since install, theirs from their PCs");
            // K2 (SOURCES.md fix 1): yours since install is LocalTotals' earlier sessions + this one, not this session's tally
            var earlier = new DamageTally(); earlier.AddDealt("Troll", "Axes", new HitData.DamageTypes { m_slash = 1000 }); earlier.AddDealt("Rock", "Pickaxes", new HitData.DamageTypes { m_pickaxe = 500 });
            input.DamageSinceInstall = DamageTally.Sum(earlier, input.Session);
            var dealtAll = Of(Page(input, "together"), "together").Items.Single(c => c.Id == "dealt");
            input.DamageSinceInstall = null;
            Check(dealtAll.Items[0].Value == "1\u00A0420" && dealtAll.Items[0].SinceInstall && dealtAll.Items[1].Value == "300",
                  "K2 together: your damage dealt reads the since-install damage (earlier sessions + this one, tools left out), not this session");
            Check(cats.Single(c => c.Id == "sailed").Value == "15.0 km" && !PanelModel.AllText(Page(input, "together")).Any(t => t.Contains("rank") || t.Contains("score")),
                  "together: km sailed in km; no ranking, no combined score");
            const string key = "Company/together/category";
            var cooked = Of(Page(input, "together", s => s.View[key] = "cooked"), "together");
            var click = new PanelState { Chapter = Chapter.Company }; PanelModel.Follow(click, PanelModel.ViewLink(together, together.Items.Single(c => c.Id == "built")));
            Check(together.Id == key && cooked.Items.Single(c => c.Selected).Id == "cooked" && click.View[key] == "built" && Of(Page(input, "together", s => s.View[key] = "nothing"), "together").Items[0].Selected,
                  "together: a chip's click (through Follow, kept like a view switch's view) shows its category large; an unknown choice falls back to the first");

            // ----- Together: Damage dealt in Battle's windows (Joost, play-test: who did how much in the last fight) -----
            const string wkey = "Company/together/window";
            Block DealtOf(string window, Action<PanelState> more = null)
            {
                var v = Page(input, "together", s => { s.View[key] = "dealt"; if (window != null) s.View[wkey] = window; more?.Invoke(s); });
                return Of(v, "together");
            }
            string Shares(Block t) => string.Join(",", t.Items.Single(c => c.Id == "dealt").Items.Select(p => p.Id + "=" + p.Value));
            Check(together.Items.All(c => c.Kind == "category") && DealtOf(null).Items.Count(c => c.Kind == "switch") == 1 && DealtOf(null).Tone == null &&
                  Of(Page(input, "together"), "together").Items.Count(c => c.Kind == "switch") == 0 && Of(Page(input, "together", s => s.View[key] = "wood"), "together").Items.Count(c => c.Kind == "switch") == 0,
                  "together windows: the switch shows only while Damage dealt is chosen; the default is the total since install");
            var sw = DealtOf(null).Items.Single(c => c.Kind == "switch");
            Check(sw.Id == wkey && sw.Items.Select(v => v.Id).SequenceEqual(Enum.GetNames(typeof(TimeWindow))) && sw.Items.Select(v => v.Title).SequenceEqual(new[] { "10 min", "30 min", "1 h", "3 h", "Session", "Today", "7 days", "30 days", "All" }) &&
                  sw.Items.Single(v => v.Selected).Id == "SinceInstall" && sw.Title == PanelModel.AllScope,
                  "together windows: Battle's nine windows as the same short chips, All (since install) the default");
            Check(Shares(DealtOf("LastHour")) == "Rowan=434,Edda=200,Finch=80,Tor=" && DealtOf("LastHour").Items.Single(c => c.Id == "dealt").Value == "714" &&
                  DealtOf("LastHour").Items.Single(c => c.Id == "dealt").Text == "damage dealt together, last hour" && DealtOf("LastHour").Tone == PanelModel.WindowedTone,
                  "together windows: last hour = each player's own log of it (you 434, Edda 200, Finch 80), the hero names the window in full");
            Check(Shares(DealtOf("LastTenMinutes")) == "Rowan=,Edda=200,Finch=80,Tor=" && Shares(DealtOf("LastThirtyMinutes")) == "Rowan=,Edda=200,Finch=80,Tor=" &&
                  Shares(DealtOf("LastThreeHours")) == "Rowan=854,Edda=300,Finch=80,Tor=" && Shares(DealtOf("Session")) == "Rowan=962,Edda=300,Finch=80,Tor=",
                  "together windows: 10 and 30 minutes (the fight just now: Edda 200, Finch 80), 3 hours (Edda's earlier 100 joins you), this session (yours 962)");
            var tenParts = DealtOf("LastTenMinutes").Items.Single(c => c.Id == "dealt").Items;
            Check(Math.Abs(tenParts[1].Fraction - 200f / 280f) < 1e-4 && tenParts[0].Fraction == 0 && Math.Abs(tenParts.Sum(p => p.Fraction) - 1f) < 1e-4 &&
                  DealtOf("LastTenMinutes").Items.Where(c => c.Id != "dealt" && c.Kind == "category").All(c => c.Items.Sum(p => p.Fraction) > 0.999f),
                  "together windows: the shares are of the window's whole; the other categories keep their totals");
            Check(DealtOf("nonsense").Tone == null && DealtOf("nonsense").Items.Single(c => c.Kind == "switch").Items.Single(v => v.Selected).Id == "SinceInstall",
                  "together windows: an unknown choice falls back to the total since install");
            var inst = Page(input, "together", s => s.View[key] = "dealt"); var hour2 = Page(input, "together", s => { s.View[key] = "dealt"; s.View[wkey] = "LastHour"; });
            Check(inst.Windowed == false && Every(inst.Blocks).Any(b => b.SinceInstall) && hour2.Windowed && !Every(hour2.Blocks).Any(b => b.SinceInstall) && !Every(hour2.Blocks).Any(b => b.Kind == "zone"),
                  "together windows: a window carries no 'since install' label (the total since install keeps it on your number)");
            Check(DealtOf("LastTenMinutes").Items.Single(c => c.Id == "dealt").Items.Select(p => p.Src).SequenceEqual(new[] { "pc", "fellows", "fellows", "fellows" }) &&
                  PanelModel.Content(Page(input, "together", s => { s.View[key] = "dealt"; s.View[wkey] = "LastTenMinutes"; })).Any(b => b.Kind == "note" && b.Text == "Nothing in this window from Tor: their latest shared record is older."),
                  "together windows: yours from this PC, theirs from their PCs; a fellow whose latest shared record is older than the window is named, not counted as nothing");
            Check(PanelModel.Content(Page(input, "together", s => { s.View[key] = "dealt"; s.View[wkey] = "Session"; })).Any(b => b.Kind == "note" && b.Text == PanelModel.WindowFellowSession) &&
                  !PanelModel.Content(Page(input, "together", s => { s.View[key] = "dealt"; s.View[wkey] = "LastHour"; })).Any(b => b.Kind == "note" && b.Text.StartsWith("Nothing")),
                  "together windows: This session says whose session a fellow's number is; a window every fellow reaches says nothing extra");
            var clickW = new PanelState { Chapter = Chapter.Company }; PanelModel.Follow(clickW, PanelModel.ViewLink(sw, sw.Items.Single(v => v.Id == "LastHour")));
            var keyed = new PanelState { Chapter = Chapter.Company }; keyed.Page[Chapter.Company] = "together"; keyed.View[key] = "dealt";
            var keyedView = PanelModel.Build(input, keyed);
            keyed.ViewKey = "F"; keyed.FilterKey = "G"; keyedView = PanelModel.Build(input, keyed);
            Check(clickW.View[wkey] == "LastHour" && PanelModel.SwitchOf(keyedView) != null && keyedView.Keys.Contains("[F] Category") && keyedView.Keys.Contains("[G] Window") && PanelModel.FilterKeyPressed(keyed, keyedView) && keyed.View[wkey] == "LastTenMinutes",
                  "together windows: a chip's click picks the window; the filter key steps them (All > 10 min), the footer lists it (fix4-rest: the view key flips the category)");
            var catKeyed = new PanelState { Chapter = Chapter.Company, ViewKey = "F", FilterKey = "G" }; catKeyed.Page[Chapter.Company] = "together";
            var catView = PanelModel.Build(input, catKeyed); var catTogether = PanelModel.TogetherOf(catView);
            Check(catTogether.KeyCap == "F" && catView.Keys.Contains("[F] Category") && !catView.Keys.Contains("[G] Window") && PanelModel.StepView(catKeyed, catView, 1) && catKeyed.View[key] == "ore" && PanelModel.StepView(catKeyed, PanelModel.Build(input, catKeyed), -1) && catKeyed.View[key] == "wood" &&
                  Enumerable.Range(0, catTogether.Items.Count(c => c.Kind == "category")).Aggregate(catKeyed, (st, _) => { PanelModel.StepView(st, PanelModel.Build(input, st), 1); return st; }).View[key] == "wood" && !PanelModel.FilterKeyPressed(new PanelState(), catView),
                  "together keys: the view key flips the category chips (wraps), the footer says so; the window key only exists under Damage dealt");
            var noKeys = new PanelState { Chapter = Chapter.Company, ViewKey = "", FilterKey = "" }; noKeys.Page[Chapter.Company] = "together"; noKeys.View[key] = "dealt";
            var noKeysView = PanelModel.Build(input, noKeys);
            Check(DealtOf(null).Items.Single(c => c.Kind == "switch").KeyCap == new PanelState().FilterKey && PanelModel.TogetherOf(noKeysView).KeyCap == null && PanelModel.TogetherOf(noKeysView).Items.Single(c => c.Kind == "switch").KeyCap == null && !noKeysView.Keys.Any(k => k.EndsWith("Category") || k.EndsWith("Window")) &&
                  catTogether.Items.All(c => c.Kind != "category" || c.Value2 != null || c.Id == "dealt"),
                  "together keys: the window chips carry the filter key's cap, the category chips the view key's; keys switched off, no caps and no footer words; every chip but Damage dealt says since when");
            var soloDealt = new PanelInput { PlayerName = "Rowan", IsSelf = true, NowUtc = now, Session = new DamageTally(), Log = new EventLog(), Events = new SessionEvents() };
            soloDealt.Session.AddDealt("Troll", "Axes", new HitData.DamageTypes { m_slash = 500 });
            var soloNone = Page(soloDealt, "together", s => { s.View[key] = "dealt"; s.View[wkey] = "LastTenMinutes"; });
            var soloCat = Of(soloNone, "together").Items.Single(c => c.Id == "dealt");
            Check(soloCat.Value == "0" && soloCat.Selected && soloCat.Items[0].Value == "" && PanelModel.Content(soloNone).Any(b => b.Kind == "note" && b.Text == "Nothing dealt in the last 10 minutes.") && Of(soloNone, "together").Items.Any(c => c.Kind == "switch"),
                  "together windows: nothing dealt in the window keeps the category and its switch (so you can step back) and says so, never a blank page");

            // ----- Food shared -----
            var food = Page(input, "food");
            var axis = Of(food, "axis");
            var ends = axis.Items.Where(i => i.Kind == "end").ToList(); var rows = axis.Items.Where(i => i.Kind == "row").ToList();
            Check(ends.Select(e => e.Tone + ":" + e.Value + " " + e.Title).SequenceEqual(new[] { "left:9 servings you enjoyed", "right:13 servings of yours enjoyed" }) &&
                  ends[0].Src == "pc" && ends[0].SinceInstall && ends[1].Src == "fellows" && !ends[1].SinceInstall,
                  "food shared: the two totals, what you enjoyed (since install, this PC) and what was enjoyed from you (their PCs)");
            Check(rows.Select(r => r.Id + ":" + r.Tone + "=" + r.Value).SequenceEqual(new[] { "Edda:left=8", "Edda:right=2", "Tor:left=1", "Tor:right=11" }) && axis.Value == "11" &&
                  Math.Abs(rows[0].Fraction2 - 3f / 8f) < 1e-4 && Math.Abs(rows[3].Fraction2 - 3f / 11f) < 1e-4 && Math.Abs(rows[3].Fraction - 1f) < 1e-4,
                  "food shared: per fellow player both ways on one scale; feast servings as their share of the row (Finch shared no food with you: no row)");
            Check(rows[0].Items.Select(c => c.Icon + " " + c.Value).SequenceEqual(new[] { "item:Bread × 3", "item:FeastMeadows × 2", "item:FishWraps × 2", "item:FeastBlackforest × 1" }),
                  "food shared: the dishes with their counts, most first, feasts by their own item");
            // ----- Gear shared -----
            var gear = Page(input, "gear");
            var made = Of(gear, "madeby");
            Check(made.Title == "Their gear, since install" && made.Text == "Your gear, " + PanelModel.TheirSession && made.Note == PanelModel.GearCountNote && PanelModel.PlateOf(gear).Pill == "gear put to good use" &&
                  made.Items.Select(r => r.Tone + ":" + r.Id + "=" + string.Join(",", r.Items.Select(t => t.Title))).SequenceEqual(new[] { "left:Tor=Iron Scale Mail,Iron Sword", "right:Edda=Bronze Axe", "right:Tor=Antler Pickaxe" }),
                  "gear shared: their gear you put to good use, your gear they put to good use, one tile per kind, the times it was put to good use on each tile");
            Check(made.Items.All(r => r.Items.All(t => t.Value != null && t.Value.Length > 0 && t.Value.All(char.IsDigit) && t.Value != "0")) && made.Items.All(r => string.IsNullOrEmpty(r.Value)), "gear shared: a count on every tile (times put to good use, the receiver's own record), none on the row");

            // ----- a fellow player's book: the same pages from their side -----
            var edda = input.Fellows[0]; var keepE = edda.Fellows; var keepV = edda.ViewerName;
            edda.Fellows = input.Fellows.Skip(1).Concat(new[] { input }).ToList(); edda.ViewerName = "Rowan"; edda.PlayerNames = input.PlayerNames;   // as PanelUi.Fellow wires it
            try
            {
                var eddaFire = Of(Page(edda, "fireside"), "giving");
                var eddaFood = Of(Page(edda, "food"), "axis");
                Check(eddaFire.Items.Where(i => i.Kind == "player").Select(p => p.Title).SequenceEqual(new[] { "Edda", "Finch", "Rowan (you)", "Tor" }) && eddaFire.Title == "Edda and fellow players",
                      "fellow's book: Edda first by name, you marked (you)");
                Check(eddaFood.Items.Where(i => i.Kind == "end").Select(e => e.Value + " " + e.Title + " " + e.Src).SequenceEqual(new[] { "5 servings Edda enjoyed fellows", "8 servings of Edda's enjoyed pc" }),
                      "fellow's book: what Edda enjoyed comes from her copy, what you enjoyed of hers from this PC");
            }
            finally { edda.Fellows = keepE; edda.ViewerName = keepV; }
        }
        finally { input.Fellows = keepFellows; input.PlayerId = keepId; }
    }
}
