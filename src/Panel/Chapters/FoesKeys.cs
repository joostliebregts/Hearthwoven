using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.8: the Foes page by keyboard and controller (0.7 was mouse only: a click opened a foe's ranking, the exact factor showed on hover).
    /// As on the Feats pages, A/D (the arrows, the D-pad) move a cursor between the foe rows and Q/E still turn the chapters; the key the
    /// filter focus already uses to choose (Enter, Space, the pad's A) opens or closes the cursor's foe. The cursor's row wears the focus
    /// ring and shows the game's factor under every damage type (what pointing at a cell shows). While a foe is open, moving the cursor
    /// moves the open ranking with it, so the rankings can be read one foe after another; Enter closes it.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>The state key of the foe under the keys' cursor (its prefab; "" or none = no cursor yet).</summary>
        public const string FoeCursorKey = "Battle/foes/cursor";
        /// <summary>The Note a foe row carries while the cursor is on it (the panel and the preview draw the focus ring and the factors).</summary>
        public const string CursorNote = "cursor";

        /// <summary>The foe rows of the page on screen (Foes, By foe), in order; none on any other page or view.</summary>
        public static List<Block> VisibleFoes(PanelView v)
        {
            if (v == null || v.ShowAbout || v.Active != Chapter.Battle || v.Page != "foes") return new List<Block>();
            return Content(v).FirstOrDefault(b => b.Kind == "foetable")?.Items?.Where(i => i.Kind == "foe").ToList() ?? new List<Block>();
        }

        /// <summary>The Foes page shows its foe rows: A/D and Enter are the cursor's there.</summary>
        public static bool OnFoesTable(PanelView v) => VisibleFoes(v).Count > 0;

        static string FoeCursor(PanelState s) => s != null && s.View.TryGetValue(FoeCursorKey, out var c) ? c : null;

        /// <summary>A/D on the Foes page: the cursor moves to the next foe (wraps); the first press puts it on the open foe, else the first.
        /// While a foe is open, its ranking moves with the cursor (a foe the game knows nothing about opens nothing). false when there are no rows.</summary>
        public static bool StepFoe(PanelState s, PanelView v, int d)
        {
            var rows = VisibleFoes(v);
            if (rows.Count == 0) return false;
            var at = rows.FindIndex(r => r.Id == FoeCursor(s));
            var open = rows.FindIndex(r => r.Selected);
            var to = at >= 0 ? ((at + d) % rows.Count + rows.Count) % rows.Count : open >= 0 ? open : 0;
            s.View[FoeCursorKey] = rows[to].Id;
            if (open >= 0) s.View[FoeOpenKey] = rows[to].Tone == "opens" ? rows[to].Id : "";
            return true;
        }

        /// <summary>Enter, Space or the pad's A on the Foes page: opens the cursor's foe, or closes it when it is open (no cursor yet: the open
        /// foe, else the first). A foe that cannot open (a fellow's book, a foe the game has no data for) only takes the cursor.</summary>
        public static bool ToggleFoe(PanelState s, PanelView v)
        {
            var rows = VisibleFoes(v);
            if (rows.Count == 0) return false;
            var row = rows.FirstOrDefault(r => r.Id == FoeCursor(s)) ?? rows.FirstOrDefault(r => r.Selected) ?? rows[0];
            s.View[FoeCursorKey] = row.Id;
            if (row.Tone == "opens") s.View[FoeOpenKey] = row.Selected ? "" : row.Id;
            return true;
        }

        /// <summary>Build's last step on the Foes page with rows: the cursor's row is marked (CursorNote), and the key line says what A/D and Enter
        /// do here ("[Q/E] Chapter", as on the Feats pages).</summary>
        static void FoesFinish(PanelState state, PanelView view)
        {
            var rows = VisibleFoes(view);
            if (rows.Count == 0) return;
            var cursor = FoeCursor(state);
            foreach (var r in rows) if (r.Id == cursor && !string.IsNullOrEmpty(cursor)) r.Note = CursorNote;
            if (state.FilterRow >= 0) return;   // the filter focus has the keys (FacetModel.cs)
            var at = view.Keys.IndexOf("[Q/E·A/D] Chapter");
            if (at >= 0) view.Keys[at] = "[Q/E] Chapter";
            var page = view.Keys.IndexOf("[W/S] Page");
            var keys = new List<string> { FoeKey };
            if (rows.Any(r => r.Tone == "opens")) keys.Add(FoeOpenKeyLine);
            view.Keys.InsertRange(page >= 0 ? page + 1 : view.Keys.Count, keys);
        }

        public const string FoeKey = "[A/D] Foe", FoeOpenKeyLine = "[Enter] Open";
    }
}
