// 0.8.2 (Joost in game, The Mead Hall's "Who brewed" ended in "+3 more": "I want to see everyone, of course I want to see everyone, not plus three
// more ... for all of the feats of the group and everywhere else where it makes sense"): no list of people says "+N more" or folds people into an
// Other part, on any page of a group of seven, Everyone on or off; a bar of ten people keeps ten parts. A list of things may still fold, but its
// "+N more" is a click that shows the rest (Block.NoteLink, the book's Show all), never dead words. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hearthwoven.Panel;

static class EveryoneShownTests
{
    static readonly Regex Fold = new Regex(@"^\+[\d  ]+ more$|^Other \(\d+ kinds?\)$");
    static bool Person(Block b) => PanelModel.IsPersonPart(b);
    static IEnumerable<Block> Walk(IEnumerable<Block> bs) { foreach (var b in bs ?? Enumerable.Empty<Block>()) { if (b == null) continue; yield return b; foreach (var c in Walk(b.Items)) yield return c; if (b.Chip != null) yield return b.Chip; } }

    /// <summary>Every page of a book, with Everyone off and on, and every view of a page's switch.</summary>
    static IEnumerable<(string name, PanelState st)> States(PanelInput who)
    {
        foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
            foreach (var l in PanelModel.Build(who, new PanelState { Chapter = ch }).List)
                foreach (var everyone in new[] { false, true })
                {
                    var st = new PanelState { Chapter = ch, Page = { [ch] = l.Id }, Everyone = everyone };
                    yield return (ch + "/" + l.Id + (everyone ? "/everyone" : ""), st);
                    foreach (var sw in PanelModel.Content(PanelModel.Build(who, st)).Where(b => b.Kind == "switch" && b.Items != null))
                        foreach (var o in sw.Items.Where(x => !x.Selected))
                            yield return (ch + "/" + l.Id + "/" + o.Id + (everyone ? "/everyone" : ""), new PanelState { Chapter = ch, Page = { [ch] = l.Id }, Everyone = everyone, View = { [sw.Id] = o.Id } });
                }
    }

    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- every page of a group of seven: people are never folded, a fold of things is never dead ----------
        var seven = FeatsTests.Brewers(now, FeatsTests.SevenMore);
        var cut = new List<string>(); var dead = new List<string>(); int lists = 0, folds = 0, pages = 0;
        foreach (var (name, st) in States(seven))
        {
            var v = PanelModel.Build(seven, st); pages++;
            foreach (var b in Walk(v.Blocks))
            {
                if (Fold.IsMatch(b.Note ?? "")) { folds++; if (string.IsNullOrEmpty(b.NoteLink)) dead.Add(name + ": " + b.Kind + " '" + b.Note + "'"); }
                var people = (b.Items ?? new List<Block>()).Where(Person).ToList();
                if (people.Count == 0) continue;
                lists++;
                // the list itself says nothing like "+3 more", no row stands in for the people left out, and a bar of them keeps every one
                var stand = new[] { b.Title, b.Value, b.Text }.Concat(b.Items.Where(i => !Person(i)).SelectMany(i => new[] { i.Title, i.Value, i.Note })).FirstOrDefault(t => Fold.IsMatch(t ?? ""));
                if (stand != null) cut.Add(name + ": " + b.Kind + " '" + (b.Title ?? b.Id) + "' says '" + stand + "'");
                if (people.Any(p => p.Fraction > 0) && PanelModel.BarFormOf(b.Items).Rows.Any(r => r.Part?.Id == PanelModel.FoldId)) cut.Add(name + ": " + b.Kind + " '" + (b.Title ?? b.Id) + "' folds people into Other");
            }
        }
        foreach (var c in cut.Concat(dead).Take(12)) System.Console.WriteLine("  " + c);
        Check(cut.Count == 0 && lists > 0, "everyone (0.8.2): on " + pages + " pages of a group of seven (Everyone on and off), " + lists + " lists of people, none says \"+N more\" or folds a person into Other");
        Check(dead.Count == 0 && folds > 0, "everyone (0.8.2): every \"+N more\" of a list of things on those pages (" + folds + ") is a click that shows the rest (NoteLink), none dead words");

        // ---------- the click: Recent's folds show every gain on this visit ----------
        {
            var st = new PanelState { Chapter = Chapter.Deeds, Page = { [Chapter.Deeds] = PanelModel.RecentPageId }, Everyone = true };
            var folded = PanelModel.Build(seven, st);
            var more = Walk(folded.Blocks).FirstOrDefault(b => Fold.IsMatch(b.Note ?? "") && b.NoteLink != null);
            var before = Walk(folded.Blocks).Count(b => b.Kind == "item");
            if (more != null) PanelModel.Follow(st, more.NoteLink);
            var all = PanelModel.Build(seven, st);
            Check(more != null && !Walk(all.Blocks).Any(b => Fold.IsMatch(b.Note ?? "")) && Walk(all.Blocks).Count(b => b.Kind == "item") > before,
                  "everyone (0.8.2): Recent with Everyone, a click on a player's \"" + more?.Note + "\" shows every gain on the page (" + before + " gains, then " + Walk(all.Blocks).Count(b => b.Kind == "item") + ")");
        }

        // ---------- ten people in one bar: ten parts, never "Other (2 kinds)" (the eight-part fold is for things) ----------
        {
            var names = new[] { "Rowan", "Edda", "Finch", "Tor", "Ylva", "Bryn", "Sigrid", "Rowan Two", "Edda Two", "Finch Two" };
            var parts = names.Select((n, k) => new Block { Kind = "part", Id = n, Title = n, Colour = "person:" + n, Value = (10 - k).ToString(), Fraction = (10 - k) / 55f }).ToList();
            var things = names.Select((n, k) => new Block { Kind = "part", Id = "$item_" + k, Title = "Thing " + k, Colour = "#806040", Value = (10 - k).ToString(), Fraction = (10 - k) / 55f }).ToList();
            var form = PanelModel.BarFormOf(parts);
            Check(form.Rows.Count == 10 && form.Rows.Select(r => r.Title).SequenceEqual(names) && PanelModel.BarFormOf(things).Rows.Count == PanelModel.BarMaxParts,
                  "everyone (0.8.2): a bar of ten people keeps all ten parts and rows; a bar of ten things still folds to " + PanelModel.BarMaxParts);
        }
        return fails;
    }
}
