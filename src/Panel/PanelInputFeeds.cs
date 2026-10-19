using System;
using System.Collections.Generic;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 deed feeds (work/hearthwoven-0.9/TRACK-feeds.md; the model is Chapters/DeedFeedModel.cs): what a book needs beyond the records of
    /// TRACK-record.md. Your own deed stretches stay in DeedStretches; a friend's earlier sessions come from the server in their session blocks
    /// (FeedBlocks.cs), the same blocks that carry their earlier fights.
    /// </summary>
    public partial class PanelInput
    {
        // MaxQuality (the item's highest level) lives in PanelInput.cs; both 0.9 tracks read the one field
        /// <summary>A friend's book: their closed sessions with deed stretches that the server lists (BlockRow.Stretches set), newest first, the one their
        /// running copy is left out; null on your own book and where none is listed.</summary>
        public List<BlockRow> DeedBlockRows;
        /// <summary>A friend's book: the deed stretches of those sessions fetched so far (id -> session); their states are in BlockStates, the same ids.</summary>
        public Dictionary<string, DeedSession> DeedBlockSessions;
        /// <summary>A friend's book: this server keeps deed feeds (its list carried the deeds tag, GroupShare.DeedsOn); false on a server before 0.9.</summary>
        public bool DeedsOnServer;
    }
}
