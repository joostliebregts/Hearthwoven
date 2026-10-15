// 0.8.6 honest numbers over every preview page (INTEGRITY.md: a page never shows parts bigger than their whole):
// - By weapon: no weapon line is bigger than the kind row it sits under, and the lines never add up to more than it (the same window);
// - a damage part by the after rule: what you swung of it (Whole) = what got through + what was stopped; a part whose foe was weak to it took
//   more than you swung: its counted part (Counted) = Whole, it got through more, and it says "(weak to it)";
// - the split into got through and stopped shows only when armor was counted for ALL the damage of its window: a page with a plain dealt bar
//   (no after rule) carries the one line that says where the split is known, and a page with split bars carries no such line;
// - a share above 100 % got through is by design (a weakness), but it says why where it shows ("(weak foes)", "(weak to Fire)").
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hearthwoven.Panel;

static class HonestNumbers
{
    static readonly Regex Through = new Regex(@"(\d[\d\s  ]*) % " + PanelModel.ThroughWords + @"( \(weak)?");

    /// <summary>What breaks the rules on one page, in words (empty: nothing).</summary>
    public static List<string> Off(PanelView v)
    {
        var off = new List<string>();
        double C(string s) => PanelModel.ParseCount(s);
        void Part(Block p)
        {
            if (p.Whole == null || p.Tone != PanelModel.DamageStoppedTone) return;
            double whole = C(p.Whole), got = C(p.Value), stopped = C(p.Value2);
            var words = p.Title + " " + p.Whole + ": " + PanelModel.DealtTagWords(p);
            if (p.Counted == null && Math.Abs(got + stopped - whole) > 2) off.Add("\"" + words + "\" does not add up to " + p.Whole);
            if (p.Counted != null && (Math.Abs(C(p.Counted) - whole) > 2 || !(got > C(p.Counted)) || !words.Contains("(" + PanelModel.WeakToIt + ")")))
                off.Add("\"" + words + "\": a weak part's counted part must be what you swung (" + p.Whole + "), it got through more, and it says why");
        }
        void Walk(List<Block> list)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.Kind == PanelModel.WeaponListKind && i > 0 && list[i - 1].Kind == "dmgmix" && list[i - 1].Id == b.Id)
                {
                    var row = list[i - 1]; var whole = C(row.Value);
                    var top = b.Items.Where(x => x.Kind == PanelModel.WeaponLineKind && x.Tone != PanelModel.WeaponLevelTone).ToList();
                    foreach (var x in top.Where(x => C(x.Value) > whole))
                        off.Add(x.Title + " " + x.Value + " is more than the " + row.Title + " row it sits under (" + row.Value + ")");
                    var sum = top.Sum(x => C(x.Value));
                    if (sum > whole + top.Count) off.Add(row.Title + "'s weapon lines add up to " + sum + ", more than its row (" + row.Value + ")");
                }
                Part(b);
                Walk(b.Items);
            }
        }
        Walk(v.Blocks);
        // a window with damage from before armor was counted draws no split (no part by the after rule) and says where the split is known; a split
        // window says nothing of the kind: the two never meet on one page
        var all = new List<Block>(); void Flat(List<Block> l) { foreach (var b in l ?? new List<Block>()) { all.Add(b); Flat(b.Items); } } Flat(v.Blocks);
        var split = all.Any(b => b.Kind == "part" && b.Tone == PanelModel.DamageStoppedTone && b.Whole != null) || all.Any(b => b.Kind == "foe" && b.Fraction2 > 0 && b.Text != null && b.Text.Contains(PanelModel.ThroughWords));
        var said = all.Where(b => b.Kind == "note" && b.Text != null && (b.Text.StartsWith("Armor counted from") || b.Text == PanelModel.NotAllCountedLine)).ToList();
        if (split && said.Count > 0) off.Add("a split bar and \"" + said[0].Text + "\" on one page");
        if (said.Any(n => n.Text.StartsWith("Armor counted from") && !new Regex(@"^Armor counted from \d+ \w+(?: \d{4})?: choose (Today|Session|7 days|30 days|10 min|30 min|1 h|3 h) or shorter to see what got through\.$").IsMatch(n.Text)))
            off.Add("the counted-from line reads \"" + said.First(n => n.Text.StartsWith("Armor counted from")).Text + "\"");
        foreach (Match m in Through.Matches(PanelModel.ToJson(v)))
            if (C(m.Groups[1].Value) > 100 && !m.Groups[2].Success) off.Add("\"" + m.Value + "\" without why (a weakness)");
        return off;
    }
}
