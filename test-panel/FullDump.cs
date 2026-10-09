// The whole sample world as plain text, one page after another (test-panel --fulltext <file>): every chapter and page of Rowan's book and
// of each fellow's book, read from PanelSample.Full, so the numbers can be read side by side and checked against each other.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FullDump
{
    static void Out(System.Text.StringBuilder sb, Block b, string indent)
    {
        sb.Append(indent).Append(b.Kind).Append(b.Source != null ? " {" + b.Source + "}" : "").Append(": ").Append(string.Join(" | ", new[] { b.Id != null ? "#" + b.Id : null, b.Title, b.Value, b.Value2, b.Text, b.Note }.Where(x => !string.IsNullOrEmpty(x)))).Append('\n');
        foreach (var i in b.Items ?? new List<Block>()) Out(sb, i, indent + "    ");
    }

    /// <summary>The books of the sample world as PanelUi wires them: Rowan, then Edda, Tor and Finch with the group around them.</summary>
    public static List<(string who, PanelInput book)> Books(PanelInput full)
    {
        var books = new List<(string, PanelInput)> { ("Rowan", full) };
        foreach (var f in full.Fellows)
        {
            f.NowUtc = full.NowUtc; f.ViewerName = full.PlayerName; f.PlayerNames = full.PlayerNames;
            f.Fellows = full.Fellows.Where(x => x != f).Concat(new[] { full }).ToList();
            books.Add((f.PlayerName, f));
        }
        return books;
    }

    public static string Text(DateTime now)
    {
        var full = PanelSample.Full(now);
        var sb = new System.Text.StringBuilder();
        foreach (var (who, book) in Books(full))
        {
            sb.Append("################ BOOK: ").Append(who).Append('\n');
            var player = who == "Rowan" ? "" : who;
            foreach (Chapter ch in Enum.GetValues(typeof(Chapter)))
            {
                var first = PanelModel.Build(book, new PanelState { Chapter = ch, Player = player });
                foreach (var l in first.List)
                {
                    var st = new PanelState { Chapter = ch, Player = player, Window = TimeWindow.SinceInstall, Page = { [ch] = l.Id } };
                    var v = PanelModel.Build(book, st);
                    sb.Append("=== ").Append(who).Append(" / ").Append(ch).Append(" / ").Append(l.Label).Append('\n')
                      .Append("heading: ").Append(v.Heading).Append(v.HeadingSource != null ? " {" + v.HeadingSource + "}" : "").Append('\n');
                    foreach (var b in v.Blocks) Out(sb, b, "  ");
                }
            }
        }
        foreach (var (id, label) in PanelModel.AboutList)
        {
            var v = PanelModel.Build(full, new PanelState { ShowAbout = true, AboutPage = id });
            sb.Append("=== About / ").Append(label).Append((char)10).Append("heading: ").Append(v.Heading).Append((char)10);
            foreach (var b in v.Blocks) Out(sb, b, "  ");
        }
        return sb.ToString();
    }
}
