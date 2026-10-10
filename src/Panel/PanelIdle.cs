using System;
using System.Collections.Generic;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The open book's per-frame work without allocations (0.8 performance pass; target: 0 bytes per frame with the book open and idle).
    /// Before, every frame walked the whole page for its filter bar (FilterOf: a new list of every block, LINQ) and worked the key line
    /// out again (LINQ, a joined string, a TMP layout pass), while nothing had changed. Both now follow the drawn view: the filter bar is
    /// looked up once per view, the key line only when the page's keys, the wheel key or the line's width change.
    /// </summary>
    public partial class PanelUi
    {
        PanelView filterOf; Block filterBar;
        readonly KeyLineCache keyLine = new KeyLineCache();

        /// <summary>The drawn view's filter bar (FilterOf once per view; a new view comes with every Render).</summary>
        Block FilterNow()
        {
            if (!ReferenceEquals(view, filterOf)) { filterOf = view; filterBar = view != null ? PanelModel.FilterOf(view) : null; }
            return filterBar;
        }

        // the key line with the wheel key while a list has more below it (LateFrame); no LINQ, no closure, no new string while nothing changed
        void KeyLineNow()
        {
            var more = false;
            for (int i = 0; i < scrollers.Count && !more; i++) { var s = scrollers[i]; more = s.Rect && s.Rect.content && Room(s) > 0f; }
            var want = keyLine.Line(baseKeys, more, keys.rectTransform.rect.width, keysFit ?? (keysFit = KeysFit));
            if (!ReferenceEquals(keys.text, want) && keys.text != want) keys.text = want;
        }
        Func<string, bool> keysFit;
    }

    /// <summary>
    /// The key line, worked out again only when what it shows can change: the page's keys (a new list with every drawn page), the wheel key
    /// (a list has more below it, or no longer) or the line's width (the sample tag takes room). Pure: the caller measures (fits).
    /// </summary>
    public sealed class KeyLineCache
    {
        List<string> keys; bool more; float width = float.NaN; string line;
        /// <summary>How often the line was worked out (the tests' check that an idle frame does not).</summary>
        public int Computed { get; private set; }

        public string Line(List<string> baseKeys, bool more, float width, Func<string, bool> fits)
        {
            if (line != null && ReferenceEquals(baseKeys, keys) && more == this.more && width.Equals(this.width)) return line;
            keys = baseKeys; this.more = more; this.width = width; Computed++;
            if (!more) return line = PanelModel.KeyLine(baseKeys, fits);
            var withWheel = new List<string>(baseKeys ?? new List<string>()) { PanelModel.ScrollKey };
            return line = PanelModel.KeyLine(withWheel, fits);
        }
    }
}
