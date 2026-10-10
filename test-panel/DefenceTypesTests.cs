// ISC-22 (Joost 2026-10-09, in game on a fellow's Defence: "you can't tell what the amount of damage was from the different types"):
// Battle > Defence > Received from gives per foe the amount per damage type, as Damage does. The amounts per foe add up to the foe's
// total (largest remainders, so 10.5 + 10.5 shows 11 + 10 = 21, not 10 + 10), a real amount below 1 says "<1" (B26) and nothing says
// "0" or "Not recorded"; past the top rows the rest sum into one row; a fellow's shared copy has the same structure; Your armour's
// foe rows carry the per-type amounts too (what reached you of what hit your armour).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class DefenceTypesTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        double Count(string v) => v == PanelModel.LessThanOne ? 0 : PanelModel.ParseCount(v);
        bool Plain(string v) => !string.IsNullOrEmpty(v) && v != "0" && v != PanelModel.NotRecorded;
        // every foe: parts by type with their amounts, none 0 or Not recorded, adding up to the foe's shown total
        bool Adds(Block sources) => sources != null && sources.Items.Count > 0 && sources.Items.All(f => f.Kind == "source" && f.Items.Count > 0 &&
            f.Items.All(p => Plain(p.Value) && p.Colour != null && p.Icon != null && p.Title != null) && Math.Abs(f.Items.Sum(p => Count(p.Value)) - Count(f.Value)) < 0.01);

        // ten sources, the amounts in halves: the parts must add up to the shown total, the rest of the top sum into one row
        var rows = new List<PanelModel.DamageRow>();
        void Taken(string who, string type, float amount) => rows.Add(new PanelModel.DamageRow { Bucket = now, Biome = "Swamp", Dir = "taken", Other = who, Cause = "EnemyHit", Type = type, Amount = amount });
        Taken("Lox", "blunt", 52.5f); Taken("Lox", "frost", 52.5f); Taken("Lox", "poison", 0.4f);
        for (int k = 0; k < 9; k++) { Taken("Foe" + k, "slash", 10.5f + k); Taken("Foe" + k, "pierce", 10.5f); }
        var input = PanelSample.Evening(now);
        var s = PanelModel.ReceivedSources(input, rows);
        var lox = s?.Items.FirstOrDefault(f => f.Id == "Lox");
        var rest = s?.Items.LastOrDefault();
        var restWho = rows.GroupBy(r => r.Other).OrderByDescending(g => g.Sum(r => r.Amount)).Skip(PanelModel.RowTop - 1).ToList();
        Check(Adds(s) && lox != null && lox.Value == "105" && lox.Items.Select(p => p.Id + "=" + p.Value).SequenceEqual(new[] { "blunt=53", "frost=52", "poison=<1" }) &&
              s.Items.Count == PanelModel.RowTop && rest.Id == PanelModel.FoldId && rest.Title == PanelModel.OthersLabel(restWho.Count) && restWho.Count == 3 &&
              Math.Abs(Count(rest.Value) - Math.Round(restWho.Sum(g => g.Sum(r => (double)r.Amount)))) < 0.01,
              "defence types: per foe the amount per damage type (Lox 105 = 53 blunt + 52 frost + <1 poison), never 0; past the top " + PanelModel.RowTop + " rows the rest is one row \"" + rest?.Title + "\"");

        // the one bar form (BAR-FORM.md): the foes on one scale (each total's share of the largest), a damage-type bar (numbers inside where they fit)
        var foeRows = s.Items.Where(f => f.Kind == "source").ToList();
        Check(lox.Fraction == 1 && foeRows.Count(f => f.Fraction == 1) == 1 && foeRows.All(f => f.Fraction > 0 && f.Fraction <= 1 && PanelModel.BarLength(f) == f.Fraction) &&
              foeRows.Where(f => f.Id != PanelModel.FoldId).All(f => Math.Abs(f.Fraction - Count(f.Value) / Count(lox.Value)) < 0.02) &&
              PanelModel.NumbersOnBar(new Block { Kind = "composition", Items = lox.Items }),
              "defence types: every foe's bar on one scale (Lox, the largest, full length; the others by their share of it), a damage-type bar with its numbers inside");
        // a part's number always shows (B39: every part its own row): Lox's poison says "<1" and "<1 %", never only its share
        var loxPoison = PanelModel.BarFormOf(lox.Items, true, true).Rows.FirstOrDefault(r => r.Part.Id == "poison");
        Check(loxPoison != null && loxPoison.Value == PanelModel.LessThanOne && loxPoison.Share == PanelModel.UnderOnePercent, "defence types: Lox's poison has its own row, \"" + loxPoison?.Value + "\" · \"" + loxPoison?.Share + "\"");
        // a fellow's shared copy (Snapshot -> SharedCopy -> FromSnapshot): the same structure on their Defence
        var since = new DamageTally();
        since.AddTaken("Lox", "EnemyHit", new HitData.DamageTypes { m_blunt = 1471, m_frost = 784 }); since.AddTaken("Greyling", "EnemyHit", new HitData.DamageTypes { m_slash = 12.5f, m_poison = 3.5f });
        var json = GroupShare.SharedCopy(Snapshot.Build("0.7.0", 88, "Tor", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", new DamageTally(), "s", new SessionEvents(), new EventLog(), true,
                                                         eventsSinceInstall: new SessionEvents(), damageSinceInstall: since));
        var tor = PanelInput.FromSnapshot(json); tor.NowUtc = now; tor.DisplayName = k => k;
        var st = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.SinceInstall }; st.Page[Chapter.Battle] = "defense";
        var fellow = PanelModel.Content(PanelModel.Build(tor, st)).FirstOrDefault(b => b.Kind == "sources");
        Check(!tor.IsSelf && Adds(fellow) && fellow.Items.Select(f => f.Title + "=" + f.Value + ":" + string.Join(",", f.Items.Select(p => p.Id + p.Value))).SequenceEqual(new[] { "Lox=2 255:blunt1 471,frost784", "Greyling=16:slash13,poison3" }),
              "defence types: a fellow's shared copy shows the same per-type amounts per foe (Lox 1 471 blunt + 784 frost)");

        // B25 (Joost's Defence in game: Bat and Smoke had no picture beside Lox's trophy): every source has one. A creature its trophy, else the foe
        // mark; a cause that is no creature the swatch of the damage type it did most (raw damage: the swatch alone)
        var hurt = new List<PanelModel.DamageRow>();
        void Hurt(string who, string type, float amount) => hurt.Add(new PanelModel.DamageRow { Bucket = now, Biome = "Plains", Dir = "taken", Other = who, Cause = "EnemyHit", Type = type, Amount = amount });
        Hurt("Lox", "blunt", 105); Hurt("Bat", "slash", 14); Hurt("Smoke", "damage", 12); Hurt("Tree", "blunt", 0.4f);
        var joost = PanelSample.Evening(now); joost.Foe = p => p == "Lox" ? new PanelModel.FoeData { Trophy = "TrophyLox" } : p == "Bat" ? new PanelModel.FoeData() : null;
        var pics = PanelModel.ReceivedSources(joost, hurt).Items.ToDictionary(f => f.Id);
        Check(pics["Lox"].Icon == "item:TrophyLox" && pics["Bat"].Icon == PanelModel.FoeMark && pics["Smoke"].Tone == "type" && pics["Smoke"].Icon == "" && !string.IsNullOrEmpty(pics["Smoke"].Colour) &&
              pics["Tree"].Tone == "type" && pics["Tree"].Icon == "vocab:dmg-blunt" && pics["Tree"].Value == "<1",
              "defence (B25): every source has a picture: Lox its trophy, Bat the foe mark, Smoke and a falling tree the swatch of their damage type");

        // Your armour (own book): each foe row's types carry what reached you of what hit your armour, adding up to the row
        var world = PanelSample.Full(now);
        var bst = new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Session }; bst.Page[Chapter.Battle] = "defense"; bst.View["Battle/defense/view"] = PanelModel.ArmourId;
        var foes = PanelModel.Content(PanelModel.Build(world, bst)).FirstOrDefault(b => b.Kind == "armour" && b.Title == PanelModel.ArmourByFoe);
        Check(foes != null && foes.Items.Count > 0 && foes.Items.All(f => f.Items.Count > 0 && f.Items.All(p => p.Title != null && p.Value != null && p.Value2 != null && p.Value2 != "0") &&
              Math.Abs(f.Items.Sum(p => Count(p.Value)) - Count(f.Value)) < 0.01 && Math.Abs(f.Items.Sum(p => Count(p.Value2)) - Count(f.Value2)) < 0.01),
              "defence types: Your armour's foe rows give per damage type what reached you of what hit your armour, adding up to the row");
        return fails;
    }
}
