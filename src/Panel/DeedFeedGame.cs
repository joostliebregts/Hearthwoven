using System.Collections.Generic;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// 0.9 deed feeds: what the feeds read from the game's data (any mod's items too), read only, never written: an item's highest level
    /// (ItemDrop m_maxQuality), for the four-part level meter of an upgrade (Chapters/DeedFeedUi.cs). Read once, when the game's item list is
    /// there, and again only when it has grown (a mod that adds items later).
    /// </summary>
    static class FeedGameData
    {
        static readonly Dictionary<string, int> maxQuality = new Dictionary<string, int>();
        static int readCount = -1;

        /// <summary>An item token ("$item_sword_iron") -> its highest level (4 for most gear); 0 = not known.</summary>
        public static int MaxQuality(string token)
        {
            try
            {
                var db = ObjectDB.instance;
                if (db && db.m_items != null && db.m_items.Count != readCount)
                {
                    maxQuality.Clear();
                    foreach (var go in db.m_items)
                    {
                        var shared = go ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                        if (shared != null && !string.IsNullOrEmpty(shared.m_name) && shared.m_maxQuality > 0 && !maxQuality.ContainsKey(shared.m_name)) maxQuality[shared.m_name] = shared.m_maxQuality;
                    }
                    readCount = db.m_items.Count;
                }
                return token != null && maxQuality.TryGetValue(token, out var q) ? q : 0;
            }
            catch { return 0; }
        }
    }
}
