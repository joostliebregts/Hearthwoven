using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9.5 Mark read (Joost, 19 Oct: "we need a read all feature for the feats"): one action in the strip of Earned and Together, in your own book,
    /// while that page has New cards: "Mark 34 read". It marks every earned tier seen on both sides at once (Earned's and Together's: the Feats tab's dot
    /// covers both), so every New mark, the list's dots and the tab's dot go together; the ledger keeps it as it keeps a card opened (FeatsLedger.SeenTiers,
    /// saved with the local totals). The number it says is what it clears: the New feats of both pages. The page's line then leaves out its own count
    /// ("· 34 new", "3 new feats."): the count moved into the action, so the line and the search field still fit the strip. Not while a search is on
    /// (the line says the matches then), never on Unsung, Titles or a fellow's book (nothing is New there). Its key (Panel.MarkReadKey, R) is in the key
    /// line only while the action is offered, and does nothing while the search field has the keys (FeatSearchInput.Typing).
    /// The action rides on the search field's block (Kind "featsearch": Link FeatsReadTarget, Value its words); FeatSearchUi draws it left of the field.
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>The action's target (PanelUi.LinkTo marks everything read on it) and its words.</summary>
        public const string FeatsReadTarget = "Feats/markread", FeatsReadWord = "Mark read";
        /// <summary>The action's words: "Mark 34 read" (the New feats it clears, on Earned and Together together).</summary>
        public static string FeatsReadChip(int n) => "Mark " + N(n) + " read";

        /// <summary>How many New feats Mark read clears: your own and the group's (0 without the per-tier record or in a fellow's book).</summary>
        public static int FeatsUnread(PanelInput input, PanelState state = null) =>
            NewFeats(input, false).Concat(NewFeats(input, true)).Count(id => state == null || id != state.FeatOpen);   // the card opened now is seen after the build (FeatsSeen)

        /// <summary>Whether Earned or Together offers Mark read: your own book with the per-tier record, this page with a New card, no search on.</summary>
        public static bool FeatsReadOffered(PanelInput input, string page, PanelState state, int freshHere) =>
            freshHere > 0 && NewSide(input, page).HasValue && !FeatSearching(state, page);

        /// <summary>Mark read: every earned tier of both sides seen (FeatsLedger.MarkSeen, the old counts following). True when one was New.</summary>
        public static bool MarkFeatsRead(FeatsLedger ledger)
        {
            if (ledger?.SeenTiers == null) return false;
            var any = ledger.UnseenOf(IsGroupFeat, false) + ledger.UnseenOf(IsGroupFeat, true) > 0;
            if (any) ledger.MarkSeen(IsGroupFeat);
            return any;
        }

        /// <summary>True when the page on screen offers Mark read (its search field's block carries the action).</summary>
        public static bool FeatsReadOn(PanelView v) => FeatSearchBlock(v)?.Link == FeatsReadTarget;

        /// <summary>The read key pressed (PanelUi.FeatsKeys): marks everything read when the page offers it and the search field does not have the keys.
        /// True when the press was taken (the page is then drawn again).</summary>
        public static bool FeatsReadKey(PanelView v, FeatsLedger ledger) => !FeatSearchInput.Typing && FeatsReadOn(v) && MarkFeatsRead(ledger);

        /// <summary>FeatsChapter's step after the search's: the action on the field's block, and its key in the key line (FeatsFinish places it).</summary>
        static void FeatsReadFinish(PanelInput input, string page, PanelView view, PanelState state, bool offered)
        {
            if (!offered) return;
            var field = FeatSearchBlock(view);
            if (field == null) return;
            field.Link = FeatsReadTarget; field.Value = FeatsReadChip(FeatsUnread(input, state));
        }

        /// <summary>The key line's entry for Mark read ("[R] Mark read"), or null when the page does not offer it or no key is set.</summary>
        static string FeatsReadKeyLine(PanelView view, PanelState state) =>
            FeatsReadOn(view) && !string.IsNullOrEmpty(state?.ReadKey) ? "[" + state.ReadKey + "] " + FeatsReadWord : null;
    }
}
