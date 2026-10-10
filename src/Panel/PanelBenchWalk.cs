using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>A page Dev.Bench draws (PanelBench.cs): a chapter as it opens (Probe), a page as it stands, or a page's view by name.</summary>
    public sealed class BenchShot { public Chapter Chapter; public bool About, Probe; public string Page, Switch, View, Name; public bool? They; }

    /// <summary>
    /// Dev.Bench's first round, pure, so the panel tests hold it to the page plan (0.8.1). The pages are found while they are drawn instead of
    /// all at once beforehand: the first in-game run built every page's model in one frame for its plan (the 386 ms frame it then blamed on
    /// Deeds > Recent), which also ran every page's code before its first visit. Each chapter is drawn as it opens: what it shows names it and
    /// queues the chapter's other pages in list order; each page is drawn as it stands and queues its other views. The names are the page
    /// plan's (PanelSnapshot.Plan): "deeds-overview-earned".
    /// </summary>
    public static class PanelBenchWalk
    {
        /// <summary>Round 1's start: every chapter as it opens, then About.</summary>
        public static List<BenchShot> Start(HashSet<string> names)
        {
            var todo = new List<BenchShot>();
            foreach (Chapter c in Enum.GetValues(typeof(Chapter))) todo.Add(new BenchShot { Chapter = c, Probe = true });
            todo.Add(new BenchShot { About = true, Name = Unique(names, "about") });
            return todo;
        }

        /// <summary>The panel's state for a shot, as the page walk sets it before drawing (PanelSnapshot.Apply): your own book.</summary>
        public static void Apply(BenchShot shot, PanelState state)
        {
            state.Player = ""; state.ShowAbout = shot.About;
            if (shot.About) return;
            state.Chapter = shot.Chapter;
            if (shot.Page != null) state.Page[shot.Chapter] = shot.Page;
            if (shot.Switch != null) state.View[shot.Switch] = shot.View;
            if (shot.They.HasValue) state.TheyReceived = shot.They.Value;
        }

        /// <summary>
        /// What a drawn page teaches (v: the view on screen): the full shot it was, named (the page's switch or toggle as it stood), with the
        /// page's other views queued right after it in <paramref name="todo"/>; a chapter's first drawing also queues the chapter's other pages.
        /// done: the pages drawn or queued as they stand ("Deeds/cooking"). About is its own shot: nothing to learn.
        /// </summary>
        public static BenchShot Learn(BenchShot drawn, PanelView v, List<BenchShot> todo, int at, HashSet<string> names, HashSet<string> done)
        {
            if (drawn.About) return drawn;
            var c = drawn.Chapter;
            var page = drawn.Probe ? (v.List.Count > 0 ? v.Page : null) : drawn.Page;
            var baseName = Safe(c.ToString()) + (page != null ? "-" + Safe(page) : "");
            var queue = new List<BenchShot>();
            BenchShot full;
            var sw = PanelModel.Content(v).FirstOrDefault(b => b.Kind == "switch" && b.Items != null && b.Items.Count > 1);
            if (drawn.Switch != null || drawn.They.HasValue) full = drawn;   // a view asked for by name
            else if (sw != null)
            {
                var on = sw.Items.FirstOrDefault(x => x.Selected) ?? sw.Items[0];
                full = new BenchShot { Chapter = c, Page = page, Switch = sw.Id, View = on.Id, Name = Unique(names, baseName + "-" + Safe(on.Id)) };
                foreach (var o in sw.Items) if (o != on) queue.Add(new BenchShot { Chapter = c, Page = page, Switch = sw.Id, View = o.Id, Name = Unique(names, baseName + "-" + Safe(o.Id)) });
            }
            else if (v.Toggle.Count > 1)
            {
                var on = v.Toggle.FirstOrDefault(x => x.Selected) ?? v.Toggle[0];
                full = new BenchShot { Chapter = c, Page = page, They = on.Id == "they", View = on.Id, Name = Unique(names, baseName + "-" + Safe(on.Id)) };
                foreach (var t in v.Toggle) if (t != on) queue.Add(new BenchShot { Chapter = c, Page = page, They = t.Id == "they", View = t.Id, Name = Unique(names, baseName + "-" + Safe(t.Id)) });
            }
            else full = new BenchShot { Chapter = c, Page = page, Name = Unique(names, baseName) };
            done.Add(c + "/" + (page ?? ""));
            if (drawn.Probe)
                foreach (var l in v.List)
                    if (done.Add(c + "/" + l.Id)) queue.Add(new BenchShot { Chapter = c, Page = l.Id });   // drawn as it stands, then learnt from
            todo.InsertRange(at + 1, queue);
            return full;
        }

        /// <summary>A file-safe name part: lower case, letters and digits, the rest a single dash (the page walk's and the snapshots' names).</summary>
        public static string Safe(string s)
        {
            var chars = (s ?? "").ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            var t = new string(chars).Trim('-');
            while (t.Contains("--")) t = t.Replace("--", "-");
            return t.Length == 0 ? "page" : t.Length > 48 ? t.Substring(0, 48) : t;
        }

        /// <summary>The name, or name-2, name-3 when it is taken.</summary>
        public static string Unique(HashSet<string> names, string name)
        {
            var n = name; for (int i = 2; !names.Add(n); i++) n = name + "-" + i;
            return n;
        }
    }
}
