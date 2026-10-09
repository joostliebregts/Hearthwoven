using System.Collections.Generic;

namespace Hearthwoven.Panel
{
    /// <summary>One place in the panel: a chapter's page, whose page it is (a fellow player's or yours), or the About page.</summary>
    public sealed class PanelPlace
    {
        public Chapter Chapter; public string Page, Player; public bool About;
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
            if (s.Here != null && !s.Here.Same(here) && !s.GoingBack)
            {
                s.History.Add(s.Here);
                if (s.History.Count > HistoryMax) s.History.RemoveAt(0);
            }
            s.Here = here; s.GoingBack = false;
        }

        /// <summary>Backspace: back to the page you came from. False when there is nowhere to go back to.</summary>
        public static bool Back(PanelState s)
        {
            if (s.History.Count == 0) return false;
            var to = s.History[s.History.Count - 1];
            s.History.RemoveAt(s.History.Count - 1);
            s.Chapter = to.Chapter;
            if (to.About) { if (to.Page != null) s.AboutPage = to.Page; }
            else if (to.Page != null) s.Page[to.Chapter] = to.Page;
            s.Player = to.Player; s.ShowAbout = to.About;
            s.GoingBack = true;
            return true;
        }

        /// <summary>A fresh history when the panel opens: Backspace never leads into an earlier opening.</summary>
        public static void ForgetHistory(PanelState s) { s.History.Clear(); s.Here = null; s.GoingBack = false; EndWait(s); }   // and a greyed window pressed last time is let go (B17)
    }
}
