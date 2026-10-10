// 0.6 panel tests for the server's book (src/Panel/Chapters/CargoModel.cs): Voyages > Sailing's Cargo loaded and unloaded beside
// Cargo carried, Deeds > Taming's Born near beside Born in your care. Built on the sample evening (fictional Rowan, Edda, Tor).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class ServerBookPanelTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));
        PanelView Page(PanelInput who, Chapter c, string page) { var st = new PanelState { Chapter = c }; st.Page[c] = page; return PanelModel.Build(who, st); }
        bool InZone(PanelView v, Func<Block, bool> what) => Every(PanelModel.Content(v)).Where(b => b.Kind == "zone").Any(z => Every(z.Items ?? new List<Block>()).Any(what));

        // ---------- Voyages > Sailing ----------
        var me = PanelSample.Voyager(PanelSample.Evening(now));
        var sailing = Page(me, Chapter.Voyages, "cargo");
        var all = Every(PanelModel.Content(sailing)).ToList();
        var hero = all.FirstOrDefault(b => b.Kind == "hero" && b.Title == "item-km loaded");
        var second = all.FirstOrDefault(b => b.Kind == "hero" && b.Title == "item-km unloaded");   // fix3-rest: each number heads its own column, over its bar
        Check(all.Any(b => b.Kind == "section" && b.Title == "Cargo loaded and unloaded" && b.RecordedFrom != null) && hero != null && hero.Value == "1 434" && hero.Src == "server" &&
              second != null && second.Title == "item-km unloaded" && second.Value == "133" && second.Src == "server",
              "server book: Cargo shows Cargo loaded and unloaded (its own section, 0.7), two numbers side by side (1 434 item-km loaded, 133 item-km unloaded), never summed, recorded by the server");
        Check(all.Any(b => b.Kind == "section" && b.Title == "Cargo carried"), "server book: Cargo carried at the helm stays beside it (this PC's own count)");
        var serverBars = all.Where(b => b.Kind == "composition" && b.Src == "server").Distinct().ToList();   // fix3-rest: the hero over each bar says which is which
        var sentBar = serverBars.ElementAtOrDefault(0);
        var delivBar = serverBars.ElementAtOrDefault(1);
        Check(sentBar != null && delivBar != null && sentBar.Items.Select(i => i.Id).SequenceEqual(new[] { "$item_ironscrap", "$item_wood" }) && sentBar.Items[0].Title == "Iron Scrap" &&
              sentBar.Items[0].Value == "1 430" && delivBar.Items.Select(i => i.Id).SequenceEqual(new[] { "$item_ironscrap", "$item_wood" }) && delivBar.Items[1].Value == "49",
              "server book: per item, a thin bar for Sent and one for Delivered, in the unit of the larger number");
        var line = all.FirstOrDefault(b => b.Kind == "note" && b.Text == PanelModel.ServerCargoLine(me));
        Check(line != null && line.Text.StartsWith("Loaded: you put it into a ship or cart. Unloaded: you took it out.") && line.Text.Contains("server saw") && line.Text.EndsWith("in a straight line."),
              "server book: one honest line: what loaded and unloaded mean, that only cargo the server saw counts, and that it is a straight line");
        var why = all.FindIndex(b => b.Kind == "note" && b.Text == PanelModel.ServerVersusCarried(me));
        Check(why > all.IndexOf(line) && PanelModel.ServerVersusCarried(me).Contains("whoever steered") && PanelModel.ServerVersusCarried(me).Contains("more than Cargo carried") && PanelModel.ServerVersusCarried(me).EndsWith("only while you steered or pulled."),
              "live-polish server book: one more line under it says why loaded can be more than Cargo carried (wherever it went, whoever steered; carried only at your helm or cart)");
        // 0.7 (rule S, G7): the server's book is a section of its own with the server's label; no zone on a 0.7 page
        var serverSection = Every(PanelModel.Content(sailing)).FirstOrDefault(b => b.Kind == "section" && b.Title == "Cargo loaded and unloaded");
        var order = Every(PanelModel.Content(sailing)).ToList();
        Check(serverSection != null && serverSection.RecordedFrom != null && serverSection.RecordedFrom.StartsWith("Recorded by the server from ") && !order.Any(b => b.Kind == "zone") &&
              order.Any(b => b.Src == "server") && order.FindIndex(b => b.Kind == "section" && b.Title == "Cargo carried") < order.IndexOf(serverSection),
              "server book: the server's numbers are their own section, labelled \"" + serverSection?.RecordedFrom + "\", after Cargo carried; no zone");

        var edda = PanelSample.Fellows(now).First(f => f.PlayerName == "Edda"); edda.ViewerName = "Rowan";
        var eddaAll = Every(PanelModel.Content(Page(edda, Chapter.Voyages, "cargo"))).ToList();
        var eddaHero = eddaAll.FirstOrDefault(b => b.Kind == "hero" && b.Title == "item-km unloaded");
        Check(eddaHero != null && eddaHero.Value == "1 425" && (eddaHero.Items == null || eddaHero.Items.Count == 0) && !eddaAll.Any(b => b.Title == "item-km loaded") && eddaAll.Where(b => b.Kind == "composition" && b.Src == "server").Distinct().Count() == 1 &&
              eddaAll.Any(b => b.Kind == "note" && b.Text.StartsWith("Loaded: Edda put it into a ship or cart.")),
              "server book: a fellow's book: only what she has (1 425 item-km unloaded, nothing sent shown), the line in her name");

        var noBook = PanelSample.Voyager(PanelSample.Evening(now)); noBook.Book = null;
        var noAll = Every(PanelModel.Content(Page(noBook, Chapter.Voyages, "cargo"))).ToList();
        Check(!noAll.Any(b => b.Title == "Cargo loaded and unloaded" || b.Title == "item-km loaded") && noAll.Any(b => b.Title == "Cargo carried"),
              "server book: none (a server before 0.6, or you do not share): nothing shown, Cargo carried as before (no ghosts)");

        // ---------- Deeds > Taming ----------
        var deeds = PanelSample.DeedsRich(PanelSample.Evening(now));
        var taming = Page(deeds, Chapter.Deeds, "taming");
        var t = Every(PanelModel.Content(taming)).ToList();
        var near = t.FirstOrDefault(b => b.Kind == "section" && b.Title == "Born near you");
        var grid = near == null ? null : t.Skip(t.IndexOf(near)).FirstOrDefault(b => b.Kind == "strip");
        Check(near != null && near.Value == "5" && near.Src == "server" && grid != null && grid.Items.Select(i => i.Id + "=" + i.Value).SequenceEqual(new[] { "Boar_piggy=4", "Wolf_cub=1" }),
              "born near: Taming shows Born near you (5: four piglets, a wolf cub), counted by the server");
        Check(t.Any(b => b.Kind == "section" && b.Title == "Born in your care") && t.Any(b => b.Kind == "note" && b.Text == "while your PC hosted them"),
              "born near: Born in your care (this PC) stays beside it, with its own line");
        Check(t.Any(b => b.Kind == "note" && b.Text == "tamed young that appeared within 40 m of you, seen by the server · near you, not bred by you") && Every(PanelModel.Content(taming)).Any(p => p.Tone == PanelModel.PairTone && Every(p.Items).Any(b => b.Src == "server") && Every(p.Items).Any(b => b.Title == "Born in your care")),
              "born near: the line says near, not bred, and who saw it; it stands beside Born in your care as one pair (fix3-rest), each with its own source mark");
        var tor = PanelSample.Fellows(now).First(f => f.PlayerName == "Tor"); tor.ViewerName = "Rowan";
        var torT = Every(PanelModel.Content(Page(tor, Chapter.Deeds, "taming"))).ToList();
        Check(torT.Any(b => b.Kind == "section" && b.Title == "Born near Tor" && b.Value == "3") && torT.Any(b => b.Kind == "note" && b.Text == "tamed young that appeared within 40 m of Tor, seen by the server · near them, not bred by them"),
              "born near: a fellow's book says whose (never \"you\")");
        var noNear = PanelSample.DeedsRich(PanelSample.Evening(now)); noNear.Book = null;
        Check(!Every(PanelModel.Content(Page(noNear, Chapter.Deeds, "taming"))).Any(b => b.Title == "Born near you"), "born near: none, nothing shown");

        return fails;
    }
}
