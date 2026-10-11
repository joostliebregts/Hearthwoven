// What a page says, wherever it says it (0.8.5 test cleanup, work/hearthwoven-0.8.5/TEST-AUDIT.md part 5). A check of meaning asks
// whether the page tells a fact (a number, a name, a date, the promise of a sentence), not which slot holds it: the scope line, the
// heading's window, the strip's note, the plate's line, the About box or a greyed chip's reason. Moving a sentence between them is a
// layout change and breaks no test; dropping or changing the fact does. Every test file sees these through the global using.
global using static PageWords;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven.Panel;

static class PageWords
{
    static IEnumerable<Block> Every(IEnumerable<Block> bs) => (bs ?? Enumerable.Empty<Block>()).SelectMany(b => new[] { b }.Concat(Every(b.Items)).Concat(b.Chip != null ? Every(new[] { b.Chip }) : Enumerable.Empty<Block>()));

    /// <summary>Every text the page carries, each place once: PanelModel.AllText (the player-visible strings) plus the labels drawn beside
    /// them (the heading's window and "Recorded from" label, a number's "from 1 October") and every greyed chip's reason (shown on hover).</summary>
    public static List<string> Texts(PanelView v)
    {
        if (v == null) return new List<string>();
        var reasons = new[] { v.Chapters, v.List, v.Badges, v.Toggle, v.Windows, v.Biomes, v.Players }.SelectMany(cs => cs)
            .Where(c => !(c.Id == v.WindowTip && v.Windows.Contains(c))).Select(c => c.Why);   // AllText already holds the pressed window chip's reason
        var labels = Every(v.Blocks).Concat(v.AboutNumbers != null ? Every(new[] { v.AboutNumbers }) : Enumerable.Empty<Block>()).Select(b => b.RecordedFrom);
        return PanelModel.AllText(v).Concat(new[] { v.HeadingWindow, v.HeadingRecordedFrom, v.Compare?.Why }).Concat(reasons).Concat(labels)
            .Where(s => !string.IsNullOrEmpty(s)).ToList();
    }

    /// <summary>The page says this, in any of its texts.</summary>
    public static bool Says(PanelView v, string text) => Texts(v).Any(t => t.Contains(text));
    /// <summary>One text of the page holds all of these together (a name and its date on one line, a number with its unit).</summary>
    public static bool Says(PanelView v, params string[] parts) => Texts(v).Any(t => parts.All(t.Contains));
    /// <summary>How many of the page's texts hold this (a line said once, not on two slots).</summary>
    public static int TimesSaid(PanelView v, string text) => Texts(v).Count(t => t.Contains(text));
}
