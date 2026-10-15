using System.Collections.Generic;

namespace Hearthwoven.Panel
{
    /// <summary>One place in the panel: a chapter's page, whose page it is (a fellow player's or yours), or the About page.</summary>
    public sealed class PanelPlace
    {
        public Chapter Chapter; public string Page, Player; public bool About;
        /// <summary>0.8.6: reached by opening it from the page before (a row, a card, a link: PanelModel.Jump), not by the list, a tab or a key;
        /// such a page shows the Back button. Not part of Same: it says how the page was reached, not which page it is.</summary>
        public bool Opened;
        public bool Same(PanelPlace o) => o != null && o.Chapter == Chapter && o.Page == Page && o.Player == Player && o.About == About;
    }

    public static partial class PanelModel
    {
        public const int HistoryMax = 40;

        /// <summary>Note the page now shown (Chapter, its resolved Page, Player, About). When it differs from the last one, the
        /// last one goes on the back stack, so Backspace returns to the page you came from, however you left it (list, tab,
        /// chapter key, a card or link jump, a player chip). Going back itself does not push.</summary>
        public static void Visited(PanelState s, Chapter chapter, string page)
        {
            var here = new PanelPlace { Chapter = chapter, Page = page, Player = s.Player ?? "", About = s.ShowAbout };
            here.Opened = OpenedAt(s, here);
            if (s.Here != null && !s.Here.Same(here)) { s.FeedMore = 0; FoldWeapons(s); }   // 0.8.5: another page or book: the feed starts at its first page again; 0.8.6: every weapon folded
            if (s.Here != null && !s.Here.Same(here) && !s.GoingBack)
            {
                s.History.Add(s.Here);
                if (s.History.Count > HistoryMax) s.History.RemoveAt(0);
            }
            s.Here = here; s.GoingBack = false; s.Opening = false; s.BackOpened = false;
            if (s.ShowAll != null && s.ShowAll != ShowAllKey(s)) s.ShowAll = null;   // 0.8.1: Show all lasts one visit of its page (DeedsModel.cs)
        }

        /// <summary>Backspace: back to the page you came from. False when there is nowhere to go back to.</summary>
        public static bool Back(PanelState s)
        {
            if (s.History.Count == 0) return false;
            var to = s.History[s.History.Count - 1];
            s.History.RemoveAt(s.History.Count - 1);
            s.BackOpened = to.Opened;   // the page gone back to keeps how it was reached: opened from a page, its own Back leads there next
            s.Chapter = to.Chapter;
            if (to.About) { if (to.Page != null) s.AboutPage = to.Page; }
            else if (to.Page != null) s.Page[to.Chapter] = to.Page;
            s.Player = to.Player; s.ShowAbout = to.About;
            s.GoingBack = true;
            return true;
        }

        /// <summary>A fresh history when the panel opens: Backspace never leads into an earlier opening.</summary>
        public static void ForgetHistory(PanelState s) { s.History.Clear(); s.Here = null; s.GoingBack = false; s.Opening = false; s.BackOpened = false; EndWait(s); }

        /// <summary>The Back button's words (0.8.6, Joost on a run page: "How does one get back to the previous view?"), and its key.</summary>
        public const string BackLabel = "Back", BackKeyCap = "Backspace";

        // how the page about to be noted was reached: the same page again keeps its own way (a window, a view, a refresh), a page gone back to
        // the way it was reached then, any other page opened when a Jump led there from the page before
        static bool OpenedAt(PanelState s, PanelPlace here) =>
            s.Here != null && s.Here.Same(here) ? s.Here.Opened : s.GoingBack ? s.BackOpened : s.Opening && s.Here != null;

        /// <summary>
        /// 0.8.6: the page shows the Back button at the strip's left end: it was opened from another page (a run or a boss fight from Runs and
        /// bosses, Defense from the Overview, a title from the strip, the Feed from Last fight) and Backspace has that page to go back to. Its
        /// press is Back, as Backspace's is. A page reached by the list, a tab, a key or a player chip has none (the list is its way back),
        /// nor has About. Build asks this before Visited notes the page, so it reads the step Visited is about to take.
        /// </summary>
        public static bool BackShown(PanelState s, PanelPlace here) =>
            s != null && here != null && !here.About && OpenedAt(s, here) && (s.History.Count > 0 || (s.Here != null && !s.Here.Same(here) && !s.GoingBack));   // and a greyed window pressed last time is let go (B17)
    }
}
