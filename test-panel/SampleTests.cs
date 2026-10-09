// Dev.SampleData (src/Panel/PanelSample.cs, PanelSampleUi.cs): the fictional sample the game shows for screenshots fills every
// page of every chapter without any real data, carries both kinds of number (your character's and since install), names only
// Rowan, Edda, Finch and Tor, and while it is on nothing is written or sent (LocalTotals, share state, group request).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Hearthwoven;
using Hearthwoven.Panel;

static class SampleTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => bs.SelectMany(b => new[] { b }.Concat(Every(b.Items ?? new List<Block>())));

    /// <summary>Every page of every chapter as the snapshot tool plans them: each view of a page's switch, both sides of a toggle.</summary>
    internal static List<(string name, PanelView view)> AllPages(PanelInput who)
    {
        var all = new List<(string, PanelView)>();
        foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
            foreach (var l in PanelModel.Build(who, new PanelState { Chapter = ch }).List)
            {
                var st = new PanelState { Chapter = ch, Page = { [ch] = l.Id } };
                var v = PanelModel.Build(who, st);
                var sw = PanelModel.Content(v).FirstOrDefault(b => b.Kind == "switch" && b.Items != null && b.Items.Count > 1);
                if (sw != null)
                    foreach (var o in sw.Items) { var s2 = new PanelState { Chapter = ch, Page = { [ch] = l.Id }, View = { [sw.Id] = o.Id } }; all.Add((ch + "/" + l.Id + "/" + o.Id, PanelModel.Build(who, s2))); }
                else if (v.Toggle.Count > 1)
                    foreach (var t in v.Toggle) all.Add((ch + "/" + l.Id + "/" + t.Id, PanelModel.Build(who, new PanelState { Chapter = ch, Page = { [ch] = l.Id }, TheyReceived = t.Id == "they" })));
                else all.Add((ch + "/" + l.Id, v));
            }
        all.Add(("about", PanelModel.Build(who, new PanelState { ShowAbout = true })));
        return all;
    }

    public static int Run()
    {
        fails = 0;
        var now = new DateTime(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc);
        var full = PanelSample.Full(now);
        var pages = AllPages(full);

        // ---------- every page filled, from the sample alone ----------
        var emptyPage = pages.Where(p => p.view.Blocks.Count == 0 || PanelModel.Content(p.view).Any(b => b.Kind == "empty")).Select(p => p.name).ToList();
        Check(emptyPage.Count == 0, "sample: every page of every chapter has content, no empty state (" + pages.Count + " pages)" + (emptyPage.Count > 0 ? ": " + string.Join(", ", emptyPage) : ""));
        var chapters = Enum.GetValues(typeof(Chapter)).Cast<Chapter>().ToList();
        Check(chapters.All(c => pages.Count(p => p.view.Active == c && !p.view.ShowAbout) >= 2), "sample: every chapter has its pages: " +
              string.Join(", ", chapters.Select(c => c + " " + pages.Count(p => p.view.Active == c && !p.view.ShowAbout))));
        bool json = true;
        foreach (var p in pages) try { JsonDocument.Parse(PanelModel.ToJson(p.view)); } catch { json = false; }
        Check(json, "sample: every page's model is valid JSON (what the snapshot tool writes beside each PNG)");
        Check(PanelModel.Content(pages.Single(p => p.name == "Battle/overview").view).Any(b => b.Kind == "biomes" && b.Items.Count >= 4) &&
              PanelModel.Content(pages.First(p => p.name.StartsWith("Battle/foes/")).view).Any(b => b.Kind == "foetable" && b.Items.Any(r => r.Items.Count > 0)),
              "sample: Battle has the biome strip with its found biomes and the foe table with the creature cells (the stand-in creature data)");

        // ---------- both kinds of number: your character's (stone) and since install on this PC (hearth) ----------
        string Kinds(Chapter c) => string.Join("+", pages.Where(p => p.view.Active == c && !p.view.ShowAbout).SelectMany(p => Every(p.view.Blocks))
            .Where(b => (b.Value ?? "").Any(char.IsDigit)).Select(b => b.Src).Where(s => s == "character" || s == "pc").Distinct().OrderBy(s => s));
        Check(new[] { Chapter.Deeds, Chapter.Battle, Chapter.Voyages, Chapter.Skills, Chapter.Company }.All(c => Kinds(c) == "character+pc") && Kinds(Chapter.Stores) == "pc",
              "sample: Deeds, Company, Battle, Voyages and Skills carry both your character's numbers and Hearthwoven's since install; the Hall (only Hearthwoven counts it) since install: " +
              string.Join(", ", chapters.Select(c => c + "=" + Kinds(c))));

        // ---------- the people: Rowan with Edda, Finch and Tor, nobody else ----------
        var v0 = PanelModel.Build(full, new PanelState());
        PanelModel.AddPlayers(v0, full.PlayerName, full.Fellows.Select(f => f.PlayerName), "", true);
        Check(full.PlayerName == "Rowan" && v0.Players.Select(p => p.Label).SequenceEqual(new[] { "Rowan", "Edda", "Finch", "Tor" }) && PanelSample.Players.SequenceEqual(new[] { "Rowan", "Edda", "Finch", "Tor" }),
              "sample: the switcher shows Rowan, then Edda, Finch and Tor");
        var text = pages.SelectMany(p => PanelModel.AllText(p.view)).ToList();
        Check(!text.Any(t => t.Contains("Gunn")) && text.Any(t => t.Contains("Edda")) && text.Any(t => t.Contains("Finch")) && text.Any(t => t.Contains("Tor")),
              "sample: the fellows show up by name; a name outside the four (Gunn, who does not share) never does");
        foreach (var f in full.Fellows) { f.Fellows = full.Fellows.Where(x => x != f).Concat(new[] { full }).ToList(); }
        Check(full.Fellows.All(f => PanelModel.Build(f, new PanelState { Chapter = Chapter.Company }).Blocks.Count > 0), "sample: each fellow's book opens too (the switcher in sample mode)");
        foreach (var f in full.Fellows) f.Fellows = null;
        Check(PanelModel.ToJson(PanelModel.Build(PanelSample.Full(now), new PanelState { Chapter = Chapter.Battle })) == PanelModel.ToJson(PanelModel.Build(PanelSample.Full(now), new PanelState { Chapter = Chapter.Battle })),
              "sample: Full builds the same sample every time (fresh objects, nothing carried over)");

        // ---------- the in-game path: PanelUi.Render's wiring on the sample source, render after render ----------
        // Render: self = Gather(); self.Fellows = new List(); subject = Fellow(state.Player); fellows = FellowsOf(self) (the
        // source's names through Fellow); self.Fellows = fellows; AddPlayers(GroupNames()). Joost's first in-game run showed
        // the Company empty and only Rowan in the chips: the fellows were read back from self.Fellows after it was cleared.
        var source = new SampleSource(now);
        PanelView RenderLike(string player, Chapter ch, string page)
        {
            var self = source.Self;
            self.Fellows = new List<PanelInput>();
            var subject = source.Fellow(player, self);
            var fellows = source.Names.Select(n => source.Fellow(n, self)).Where(f => f != null).ToList();
            self.Fellows = fellows;
            if (subject != null) subject.Fellows = fellows.Where(f => f != subject).Concat(new[] { self }).ToList();
            var st = new PanelState { Chapter = ch, Player = player ?? "" }; if (page != null) st.Page[ch] = page;
            var v = PanelModel.Build(subject ?? self, st);
            PanelModel.AddPlayers(v, self.PlayerName, source.Names, st.Player, true);
            return v;
        }
        var companyPages = new[] { "fireside", "together", "food", "gear" };
        var firstRound = companyPages.Select(p => RenderLike("", Chapter.Company, p)).ToList();
        var secondRound = companyPages.Select(p => RenderLike("", Chapter.Company, p)).ToList();   // Render runs every 2 s
        Check(firstRound.Concat(secondRound).All(v => !PanelModel.Content(v).Any(b => b.Kind == "empty") && v.Players.Select(p => p.Label).SequenceEqual(new[] { "Rowan", "Edda", "Finch", "Tor" }) && v.ShareNote == null),
              "in-game path: render after render, every Company page is filled and the chips show Rowan, Edda, Finch and Tor (self.Fellows cleared each time)");
        var asEdda = RenderLike("Edda", Chapter.Company, "fireside");
        Check(asEdda.Players.Single(p => p.Selected).Label == "Edda" && !PanelModel.Content(asEdda).Any(b => b.Kind == "empty") && RenderLike("", Chapter.Company, "fireside").Players[0].Selected,
              "in-game path: a fellow's chip opens their book, and back to Rowan keeps the Company filled");
        Check(source.Fellow("Gunn", source.Self) == null && source.Fellows.Count == 3, "in-game path: only the sample's three fellows answer; anyone else (a real name) is nobody");

        // ---------- while it is on, nothing is written or sent ----------
        var dir = Path.Combine(Path.GetTempPath(), "hw-sample-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(dir);
        var keep = SampleMode.Check;
        try
        {
            var path = LocalTotals.PathFor(dir, 1234567890L);
            var totals = LocalTotals.Load(path, 1234567890L, out _);
            totals.Record("S1", full.Session, full.Events);
            SampleMode.Check = () => true;
            Check(SampleMode.On, "sample mode: on when Dev.SampleData says so");
            totals.Save(path, now);
            Check(!File.Exists(path) && Directory.GetFiles(dir).Length == 0, "sample mode: the local since-install totals are not saved (no file, no temp file)");
            var broken = Path.Combine(dir, "broken.json"); File.WriteAllText(broken, "not json");
            Check(LocalTotals.Load(broken, 1L, out var problem) == null && File.Exists(broken) && Directory.GetFiles(dir).Length == 1 && problem.Contains("Dev.SampleData"),
                  "sample mode: an unreadable totals file is left where it is (not set aside), and nothing is saved over it");
            Check(SampleMode.Quieted.Contains("local totals"), "sample mode: what was held back is logged once per kind: " + string.Join(", ", SampleMode.Quieted));
            // the network paths need the running game (ZNet, Splatform), so their gates are checked in the source: each is the
            // method's first statement, before anything is built, queued or sent
            var src = AppContext.BaseDirectory;
            while (src != null && !File.Exists(Path.Combine(src, "src", "Plugin.cs"))) src = Path.GetDirectoryName(src);
            string Body(string file, string head)
            {
                var s = File.ReadAllText(Path.Combine(src, "src", file)); var i = s.IndexOf(head, StringComparison.Ordinal);
                if (i < 0) return "";
                var open = s.IndexOf('{', i); var tryAt = s.IndexOf("try", open, StringComparison.Ordinal); var from = tryAt >= 0 && tryAt - open < 40 ? s.IndexOf('{', tryAt) : open;
                return s.Substring(from + 1, Math.Min(400, s.Length - from - 1)).TrimStart();
            }
            var gates = new[] { ("Plugin.cs", "internal static void SendNow(", "SampleMode.Quiet(\"your stats snapshot\")"), ("GroupShare.cs", "public static void SendShareState()", "SampleMode.Quiet(\"share state\")"),
                                ("GroupShare.cs", "public static void Request()", "SampleMode.Quiet(\"group request\")"), ("LocalTotals.cs", "public void Save(", "SampleMode.Quiet(\"local totals\")") };
            var ungated = src == null ? "src not found" : string.Join(", ", gates.Where(g => !Body(g.Item1, g.Item2).StartsWith("if (Panel." + g.Item3)).Select(g => g.Item1 + " " + g.Item2));
            var outbox = src == null ? "" : File.ReadAllLines(Path.Combine(src, "src", "Plugin.cs")).FirstOrDefault(l => l.Contains("InvokeRoutedRPC(RpcName, Outbox.Dequeue()); nextFragment"));
            Check(ungated == "" && outbox != null && outbox.Contains("!Panel.SampleMode.On"),
                  "sample mode: the stats snapshot, the share state, the group request and the local totals each stop first thing, and nothing already queued is sent" + (ungated != "" ? ": " + ungated : ""));
            var before = PanelModel.ToJson(PanelModel.Build(full, new PanelState { Chapter = Chapter.Deeds }));
            var again = AllPages(full).Count;
            Check(again == pages.Count && PanelModel.ToJson(PanelModel.Build(full, new PanelState { Chapter = Chapter.Deeds })) == before && Directory.GetFiles(dir).Length == 1,
                  "sample mode: building every page changes nothing and writes nothing");
            SampleMode.Check = () => false;
            totals.Save(path, now);
            Check(File.Exists(path) && !SampleMode.On, "sample mode off: saving works again (the real data was only held back, never changed)");
        }
        finally { SampleMode.Check = keep; try { Directory.Delete(dir, true); } catch { } }
        return fails;
    }
}
