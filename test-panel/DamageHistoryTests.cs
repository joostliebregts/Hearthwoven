// Tests for the 0.9.5 draft Damage history (src/Panel/Chapters/DamageHistory.cs): Battle > Damage's History, the Deeds History form on damage dealt per
// weapon kind. The hook on By weapon's one answer, the day bucketing, the kinds adding up to By weapon's rows for the same days, nothing drawn before the
// records begin (the history's first day, the fighting time's), DPS per day where seconds exist, a friend's shared days, none on Everyone. On the sample
// of src/Panel/PanelSampleDamage.cs. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hearthwoven;
using Hearthwoven.Panel;

static class DamageHistoryTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelState Damage(TimeWindow w, string who = null) { var s = new PanelState { Chapter = Chapter.Battle, Window = w, WindowPicked = true, Player = who }; s.Page[Chapter.Battle] = "damage"; return s; }
    static List<Block> All(PanelView v) => PanelModel.Content(v);
    static double Num(string v) { var d = Regex.Replace(v ?? "", "[^0-9.]", ""); return double.TryParse(d, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ? x : -1; }
    static Block Hook(PanelView v) => All(v).Where(b => b.Kind == "stat").SelectMany(b => b.Items ?? new List<Block>()).FirstOrDefault(i => i.Kind == PanelModel.SparkKind && i.Id == "Battle/" + PanelModel.DamageHistoryPage);

    /// <summary>Damage opened in a window, then its History followed, as a click does.</summary>
    static PanelState Open(PanelInput i, TimeWindow w, string who = null, Action<PanelState> more = null)
    {
        var s = Damage(w, who); var v0 = PanelModel.Build(i, s); PanelModel.Visited(s, v0.Active, v0.Page);
        PanelModel.Follow(s, "Battle/" + PanelModel.DamageHistoryPage);
        more?.Invoke(s);
        return s;
    }
    static List<Block> Charts(PanelView v) => All(v).Where(b => b.Kind == PanelModel.SparkKind && b.Tone == PanelModel.SparkLargeTone).ToList();
    static Block Mix(PanelView v, string kind) => All(v).FirstOrDefault(b => b.Kind == "dmgmix" && b.Id == kind);

    public static int Run(DateTime now)
    {
        fails = 0;
        var young = DamageHistorySample.World(now, "young");
        var month = DamageHistorySample.World(now, "month");
        var todayY = PanelModel.LocalToday(young); var todayM = PanelModel.LocalToday(month);

        // ---------- the hook ----------
        var dmg = PanelModel.Build(young, Damage(TimeWindow.SevenDays));
        var hook = Hook(dmg);
        Check(hook != null && hook.Items != null && hook.Items.Count >= 2, "hook: By weapon's one answer carries the damage growth line with the History link (" + hook?.Items?.Count + " days)");
        var group = PanelModel.Build(young, new PanelState { Chapter = Chapter.Battle, Window = TimeWindow.Session, Everyone = true }.Also(s => s.Page[Chapter.Battle] = "damage"));
        Check(Hook(group) == null, "hook: never on Everyone (no per-player columns; DPS never stands in a row of players)");

        // ---------- the page, 7 days of history (the sample's own week) ----------
        var sy = Open(young, TimeWindow.SevenDays);
        var vy = PanelModel.Build(young, sy);
        Check(vy.Heading == PanelModel.DamageHistoryHeading && vy.Page == PanelModel.DamageHistoryPage && vy.Back != null, "page: \"Damage history\" opens with a Back button (" + vy.Heading + ", " + vy.Page + ")");
        Check(vy.List.Any(l => l.Id == "damage" && l.Selected) || vy.List.Count == 0 || PanelModel.ListEntryOf(young, Chapter.Battle, PanelModel.DamageHistoryPage) == "damage", "page: the left list lights Damage");
        var cy = Charts(vy).FirstOrDefault();
        var firstY = young.History.FirstDay(todayY);
        Check(cy != null && cy.Items.Count == (todayY - firstY).Days + 1 && cy.Items[0].Id == DayHistory.DayKey(firstY) && cy.Items.Last().Id == DayHistory.DayKey(todayY),
              "days: one column per day from the history's first day to today, none before it (" + cy?.Items?.Count + " columns from " + cy?.Items?.FirstOrDefault()?.Id + ")");
        var sizes = All(vy).FirstOrDefault(b => b.Kind == "switch" && (b.Id ?? "").EndsWith("/period"));
        var weeks = sizes?.Items?.FirstOrDefault(i => i.Id == "weeks");
        Check(weeks != null && weeks.Tone == PanelModel.OffTone && (weeks.Title ?? "").StartsWith("Weeks from "), "days: Weeks greyed with the day it opens (" + weeks?.Title + ")");
        // the kinds add up to By weapon's rows for the same days (the history spans 7 days: 7 days on Damage)
        var key = All(vy).FirstOrDefault(b => b.Kind == "composition");
        var dmg7 = PanelModel.Build(young, Damage(TimeWindow.SevenDays));
        var sums = new List<string>();
        var agree = key != null;
        foreach (var k in new[] { "melee", "bow", "magic" })
        {
            var part = key?.Items?.FirstOrDefault(i => i.Id == k); var mix = Mix(dmg7, k);
            if (part == null && mix == null) continue;
            var a = Num(part?.Value); var b = Num(mix?.Value);
            sums.Add(k + " " + a + "/" + b);
            if (Math.Abs(a - b) > 1) agree = false;
        }
        Check(agree && sums.Count >= 2, "kinds: the key's kinds are By weapon's rows for the same 7 days (" + string.Join(", ", sums) + ")");
        var colTotal = cy?.Items?.Sum(i => Num(i.Value)) ?? 0;
        Check(key != null && Math.Abs(colTotal - Num(key.Value)) <= cy.Items.Count, "kinds: the columns add up to the key's total (" + colTotal + " / " + key?.Value + ")");
        var stacked = cy?.Items?.Where(i => i.Tone != "zero").ToList() ?? new List<Block>();
        Check(stacked.Count > 0 && stacked.All(i => i.Items != null && Math.Abs(i.Items.Sum(p => p.Fraction) - 1) < 1e-3 && i.Items.All(p => p.Colour == PanelModel.CategoryColour(PanelModel.WeaponName(p.Id)))),
              "kinds: each day split by kind in By type's kind colours, the parts making the whole column");

        // ---------- a month of history, fighting time from 12 days back ----------
        var sm = Open(month, TimeWindow.SevenDays);
        var vm = PanelModel.Build(month, sm);
        var cm = Charts(vm).FirstOrDefault();
        Check(cm != null && cm.Items.Count == DamageHistorySample.MonthDays, "month: thirty columns, the history's thirty days (" + cm?.Items?.Count + ")");
        var gaps = cm?.Items?.Zip(cm.Items.Skip(1), (a, b) => (DateTime.Parse(b.Id) - DateTime.Parse(a.Id)).Days).All(d => d == 1) ?? false;
        Check(gaps, "month: the columns are consecutive days (a day without play is a flat mark, never left out)");
        Check(cm != null && cm.Items.Any(i => i.Tone == "zero" && (i.Title ?? "").Contains("no damage dealt")), "month: a day without play is a flat mark saying so");
        var hover = cm?.Items?.LastOrDefault(i => i.Tone != "zero" && (i.Title ?? "").Contains("DPS"));
        Check(hover != null && hover.Title.Contains("Melee ") && hover.Title.Contains(" damage · "), "month: a column's hover says each kind's damage and DPS that day (" + hover?.Title + ")");

        // Bow only: its damage per day in the bow's colour, its DPS per day on the same columns
        var sb = Open(month, TimeWindow.SevenDays, null, s => s.View["Battle/" + PanelModel.DamageHistoryPage + "/kind"] = "bow");
        var vb = PanelModel.Build(month, sb);
        var cb = Charts(vb);
        Check(cb.Count == 2 && cb[0].Items.Count == cb[1].Items.Count && cb[0].Items.Select(i => i.Id).SequenceEqual(cb[1].Items.Select(i => i.Id)),
              "bow: two charts, its damage and its DPS, on the same columns (" + cb.Count + ")");
        var swingFrom = PanelModel.LocalToday(month).AddDays(1 - DamageHistorySample.SwingDays);
        var before = cb.Count == 2 ? cb[1].Items.Where(i => DateTime.Parse(i.Id) < swingFrom).ToList() : new List<Block>();
        Check(before.Count > 0 && before.All(i => i.Tone == PanelModel.SparkNoneTone && (i.Title ?? "").Contains("not recorded")),
              "bow DPS: no column before fighting time was recorded, its hover says so (not recorded is not zero; " + before.Count + " days)");
        var bowHead = All(vb).FirstOrDefault(b => b.Kind == "section" && b.Title == "Bow DPS per day");
        Check(bowHead != null && (bowHead.RecordedFrom ?? "").Contains("Recorded from"), "bow DPS: its section says when fighting time was recorded from (" + bowHead?.RecordedFrom + ")");
        // DPS per day where seconds exist: the ledger's own day row
        var book = month.DealtArmourBook;
        var checkedDays = 0; var dpsOk = cb.Count == 2;
        foreach (var col in cb.Count == 2 ? cb[1].Items.Where(i => i.Tone == null) : Enumerable.Empty<Block>())
        {
            var t = book.Days.Where(r => r.Period == col.Id).Select(r => r.Tally).FirstOrDefault(); if (t == null) continue;
            var (s, d) = t.SwingOf("bow");
            var want = SwingClock.Words(SwingClock.Pace(d, s)).Replace(" DPS", "");
            if (col.Value != want) dpsOk = false;
            checkedDays++;
        }
        Check(dpsOk && checkedDays >= 3, "bow DPS: each day's number is the ledger's damage swung over its fighting seconds (" + checkedDays + " days checked)");
        var bowTotal = Num(All(vb).FirstOrDefault(b => b.Kind == "section" && b.Title == "Bow damage per day")?.Value);
        var bowKey = Num(All(vm).FirstOrDefault(b => b.Kind == "composition")?.Items?.FirstOrDefault(i => i.Id == "bow")?.Value);
        Check(bowTotal > 0 && Math.Abs(bowTotal - bowKey) <= 1, "bow: its total is the bow's part of All weapons' key (" + bowTotal + " / " + bowKey + ")");

        // ---------- a friend's book: the days their copy shares ----------
        var books = month.Fellows.ToDictionary(f => f.PlayerName, f => f);
        foreach (var f in month.Fellows) { f.NowUtc = month.NowUtc; f.ViewerName = month.PlayerName; f.PlayerNames = month.PlayerNames; f.Fellows = month.Fellows.Where(x => x != f).Concat(new[] { month }).ToList(); }
        var edda = books["Edda"];
        var eddaDmg = PanelModel.Build(edda, Damage(TimeWindow.SevenDays, "Edda"));
        Check(Hook(eddaDmg) != null, "friend: Edda's Damage carries the History link (her copy splits its days)");
        var ve = PanelModel.Build(edda, Open(edda, TimeWindow.SevenDays, "Edda"));
        var ce = Charts(ve).FirstOrDefault();
        Check(ce != null && ce.Items.Count == PanelModel.SharedDays, "friend: thirty columns, the days her copy shares (" + ce?.Items?.Count + ")");
        var eWeeks = All(ve).FirstOrDefault(b => b.Kind == "switch" && (b.Id ?? "").EndsWith("/period"))?.Items?.FirstOrDefault(i => i.Id == "weeks");
        Check(eWeeks != null && eWeeks.Tone == PanelModel.OffTone && eWeeks.Title == "Weeks, not shared", "friend: Weeks greyed, not shared (" + eWeeks?.Title + ")");
        var veBow = PanelModel.Build(edda, Open(edda, TimeWindow.SevenDays, "Edda", s => s.View["Battle/" + PanelModel.DamageHistoryPage + "/kind"] = "bow"));
        var eDps = Charts(veBow).Skip(1).FirstOrDefault();
        Check(eDps != null && eDps.Items.Count(i => i.Tone == null) == edda.DayWeapons.Count && eDps.Items.Where(i => i.Tone == null).All(i => edda.DayWeapons.ContainsKey(i.Id)) && eDps.Items.Take(PanelModel.SharedDays - 14).All(i => i.Tone == PanelModel.SparkNoneTone),
              "friend: her bow DPS on the fourteen days her weapons are shared, the earlier days without a column (" + eDps?.Items?.Count(i => i.Tone == null) + ")");
        var nothing = PanelSample.Full(now).Fellows.First(f => f.PlayerName == "Finch"); nothing.DayDealt = null;
        Check(!PanelModel.DamageHistoryOpen(nothing), "friend: a copy that shares no split has no Damage history");

        return fails;
    }

    static T Also<T>(this T x, Action<T> f) { f(x); return x; }
}
