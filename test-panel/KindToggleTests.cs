// 0.9.5 the kind toggle (Joost in game, on Battle > Damage > By weapon: "Can we get a toggle on 'Melee' and 'Bow' etc.."): each weapon kind's row shuts or opens
// the weapon lines under it (Chapters/WeaponRows.cs, KindRows); open by default, kept between sessions in the panel prefs, per kind, the same on a friend's book.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class KindToggleTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    static PanelState Page(TimeWindow w, Action<PanelState> more = null, bool everyone = false)
    {
        var s = new PanelState { Chapter = Chapter.Battle, Window = w, WindowPicked = true, Everyone = everyone, Page = { [Chapter.Battle] = "damage" } };
        more?.Invoke(s); return s;
    }
    static List<Block> Kinds(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == "dmgmix").ToList();
    static List<Block> Lists(PanelView v) => PanelModel.Content(v).Where(b => b.Kind == PanelModel.WeaponListKind).ToList();
    // what a kind row shows: its total, DPS chip, closing words, bar and legend (each part's title, value, share and colour)
    static string Shown(Block m) => m.Id + "|" + m.Title + "|" + m.Value + "|" + m.Value2 + "|" + m.Text + "|" + m.Fraction.ToString("R") + "|" +
        string.Join(";", (m.Items ?? new List<Block>()).Select(p => p.Id + "=" + p.Value + "/" + p.Fraction.ToString("R") + "/" + p.Colour + "/" + p.Value2 + "/" + p.Text));

    public static int Run(DateTime now)
    {
        fails = 0;
        var book = WeaponScenario.Longer(PanelSample.Full(now));
        var w = TimeWindow.SevenDays;

        // default: every kind with weapon lines toggles and is open, its lines under it as before
        var open = PanelModel.Build(book, Page(w));
        var kinds = Kinds(open); var lists = Lists(open);
        Check(kinds.Count >= 2 && lists.Count >= 2 && kinds.All(k => k.Tone == PanelModel.KindTone) && lists.All(l => kinds.Any(k => k.Id == l.Id && k.Opens && k.Selected && k.Link == PanelModel.KindToggleLink(k.Id))),
              "kind toggle: by default every kind row with weapon lines (" + string.Join(", ", kinds.Where(k => k.Opens).Select(k => k.Title)) + ") is open, a click (" + kinds[0].Link + ") and the opens mark, its " + lists.Count + " lists under the rows as before");

        // a click on Bow shuts it: its lines go, the other kinds keep theirs; the rows' totals, chips, bars and legends do not change
        var bow = kinds.FirstOrDefault(k => k.Id == "bow" && k.Opens) ?? kinds.First(k => k.Opens);
        var s = Page(w); PanelModel.Follow(s, bow.Link);
        var shut = PanelModel.Build(book, s);
        var shutKinds = Kinds(shut); var shutLists = Lists(shut);
        var same = kinds.Select(Shown).SequenceEqual(shutKinds.Select(Shown));
        bool Top(Block b) => ((b.Title ?? "") + (b.Text ?? "")).Contains("the most of any weapon");
        var top = PanelModel.Content(open).FirstOrDefault(Top); var topShut = PanelModel.Content(shut).FirstOrDefault(Top);
        Check(!PanelModel.KindIsOpen(s, bow.Id) && shutLists.All(l => l.Id != bow.Id) && shutLists.Count == lists.Count - 1 && !shutKinds.First(k => k.Id == bow.Id).Selected &&
              shutKinds.Where(k => k.Id != bow.Id && k.Opens).All(k => k.Selected) && same && top != null && topShut != null && topShut.Value == top.Value,
              "kind toggle: a click on " + bow.Title + " shuts it (" + PanelModel.KindShutKey + " = \"" + s.View[PanelModel.KindShutKey] + "\"): its weapon lines go, the other kinds keep theirs; every kind row's total, DPS chip, bar and legend the same (" + same + "), the top line too");

        // all shut, then open again: nothing under the rows, then everything back, a weapon line opened before shutting still open
        var lineId = lists.SelectMany(l => l.Items).First(i => i.Opens).Id;
        var a = Page(w, x => x.View[PanelModel.WeaponOpenKey] = lineId);
        foreach (var k in kinds.Where(k => k.Opens)) PanelModel.Follow(a, k.Link);
        var allShut = PanelModel.Build(book, a);
        var allSame = kinds.Select(Shown).SequenceEqual(Kinds(allShut).Select(Shown));
        foreach (var k in kinds.Where(k => k.Opens)) PanelModel.Follow(a, k.Link);
        var back = PanelModel.Build(book, a);
        var reopened = Lists(back).SelectMany(l => l.Items).FirstOrDefault(i => i.Id == lineId);
        Check(Lists(allShut).Count == 0 && allSame && !PanelModel.Content(allShut).Any(b => b.Kind == PanelModel.WeaponLineKind) && !a.View.ContainsKey(PanelModel.KindShutKey) &&
              Lists(back).Count == lists.Count && reopened != null && reopened.Selected && PanelModel.AllText(back).Count() > PanelModel.AllText(allShut).Count(),
              "kind toggle: every kind shut leaves the kind rows alone (totals and legends the same: " + allSame + "); opened again every list is back, the weapon line opened before (" + lineId + ") still open, and no key left");

        // kept between sessions: the shut kinds are in the panel prefs and read back; a state without them reads as all open
        var played = Page(w); PanelModel.ToggleKind(played, "bow"); PanelModel.ToggleKind(played, "melee");
        var json = PanelPrefs.ToJson(played);
        var later = new PanelState(); var read = PanelPrefs.Apply(json, later);
        var fresh = new PanelState(); PanelPrefs.Apply(PanelPrefs.ToJson(new PanelState()), fresh);
        Check(read && json.Contains("\"" + PanelModel.KindShutKey + "\":\"bow;melee\"") && !PanelModel.KindIsOpen(later, "bow") && !PanelModel.KindIsOpen(later, "melee") && PanelModel.KindIsOpen(later, "magic") &&
              PanelModel.KindIsOpen(fresh, "bow") && !PanelPrefs.ToJson(new PanelState()).Contains(PanelModel.KindShutKey),
              "kind toggle: the shut kinds go into the panel prefs per kind (" + json.Substring(json.IndexOf("\"views\"", StringComparison.Ordinal)) + ") and come back after a restart; none shut writes nothing, all open");

        // the shut kinds survive another page, another window and the book opened again (only the lines' own open state folds)
        var kept = Page(w); PanelModel.ToggleKind(kept, "bow");
        PanelModel.Visited(kept, Chapter.Battle, "foes"); PanelModel.FoldWeapons(kept); kept.Page[Chapter.Battle] = "damage"; kept.Window = TimeWindow.Session; PanelModel.Build(book, kept);
        Check(!PanelModel.KindIsOpen(kept, "bow"), "kind toggle: a shut kind stays shut on another page, another window and the book opened again");

        // the keys: A/D visit the kind rows too, Enter on one shuts or opens it, the key line says which
        var ks = Page(w); var v0 = PanelModel.Build(book, ks); var stops = PanelModel.WeaponStops(v0);
        PanelModel.StepWeapon(ks, v0, 1); var v1 = PanelModel.Build(book, ks); var cur = PanelModel.WeaponStops(v1).FirstOrDefault(b => b.Cursor);
        var keysOpen = v1.Keys.ToList();
        PanelModel.ToggleWeaponCursor(ks, v1); var v2 = PanelModel.Build(book, ks); var cur2 = PanelModel.WeaponStops(v2).FirstOrDefault(b => b.Cursor);
        Check(stops.Count > kinds.Count(k => k.Opens) && stops[0].Kind == "dmgmix" && cur != null && cur.Kind == "dmgmix" && cur.Id == stops[0].Id && keysOpen.Contains(PanelModel.KindHideKeyLine) && keysOpen.Contains(PanelModel.WeaponKeyLine) &&
              cur2 != null && cur2.Kind == "dmgmix" && !cur2.Selected && !PanelModel.KindIsOpen(ks, cur2.Id) && v2.Keys.Contains(PanelModel.KindShowKeyLine) && !Lists(v2).Any(l => l.Id == cur2.Id) &&
              PanelModel.ToJson(v1).Contains("\"cursor\":1") && stops.Where(b => b.Kind == "dmgmix").Select(b => b.Id).SequenceEqual(kinds.Where(k => k.Opens).Select(k => k.Id)),
              "kind toggle by keys: A/D visit the " + stops.Count(b => b.Kind == "dmgmix") + " kind rows and the lines that open (" + stops.Count + " stops), the first press on " + cur?.Title + " (\"" + PanelModel.KindHideKeyLine + "\"); Enter shuts it (\"" + PanelModel.KindShowKeyLine + "\")");

        // a friend's book: the same toggle, the same shut set
        var edda = WeaponScenario.Longer(SampleWorld.Make(now).copies["Edda"]);
        var eOpen = PanelModel.Build(edda, Page(w)); var eKinds = Kinds(eOpen).Where(k => k.Opens).ToList();
        var es = Page(w); if (eKinds.Count > 0) PanelModel.Follow(es, eKinds[0].Link);
        var eShut = PanelModel.Build(edda, es);
        Check(eKinds.Count >= 1 && !Lists(eShut).Any(l => l.Id == eKinds[0].Id) && Lists(eShut).Count == Lists(eOpen).Count - 1 && Kinds(eOpen).Select(Shown).SequenceEqual(Kinds(eShut).Select(Shown)),
              "kind toggle on a friend's book: Edda's " + (eKinds.Count > 0 ? eKinds[0].Title : "?") + " shuts the same way, her rows unchanged");

        // where no weapon lines stand (Everyone, a compare) the kind rows do not toggle and keep their old form
        var ev = PanelModel.Build(book, Page(w, x => x.View["Battle/damage/group"] = "weapon", everyone: true)); var cmp = PanelModel.Build(book, Page(w, x => x.Compare = true));
        Check(Kinds(ev).Count > 0 && Kinds(ev).All(k => !k.Opens && k.Link == null && k.Tone != PanelModel.KindTone) && PanelModel.WeaponStops(ev).Count == 0 && Kinds(cmp).All(k => !k.Opens),
              "kind toggle: Everyone's By weapon and a compare (no weapon lines) draw their kind rows as before, nothing to toggle");
        return fails;
    }
}
