using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>One upgrade you made (0.9, record 1): the item (its token, "$item_sword_iron"), its level before and after, the crafting station
    /// it was made at (prefab name, "forge"; "" when none) and that station's level then, and when (UTC).</summary>
    public sealed class UpgradeMoment
    {
        public string Token = "", Station = "";
        public int From, To, StationLevel;
        public DateTime Utc;
    }

    /// <summary>The upgrades of one item that are older than the newest UpgradeLog.MaxRecent, folded into one line (0.9): the highest level they
    /// reached, how many, and the first and last day (UTC dates).</summary>
    public sealed class UpgradeFold
    {
        public string Token = "";
        public int MaxLevel, Count;
        public DateTime First, Last;
    }

    /// <summary>
    /// 0.9, record 1 (work/hearthwoven-0.9/DESIGN.md section 1): every upgrade you made on this PC, from the day 0.9 is installed. The newest
    /// MaxRecent are kept whole (Recent, newest first); an older one folds into one line per item (Folded: highest level, count, first and last day),
    /// so the count per item stays exact however long someone plays. Kept in the local totals file (LocalTotals, top-level "upgrades"), never shared
    /// in this form. FromUtc: when recording began (the panel's "Recorded from"). Pure C#, tested in test/RecordTests.cs.
    /// </summary>
    public sealed class UpgradeLog
    {
        public const int MaxRecent = 2000, MaxFolded = 2000;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public DateTime FromUtc = DateTime.MinValue;
        /// <summary>Newest first.</summary>
        public readonly List<UpgradeMoment> Recent = new List<UpgradeMoment>();
        /// <summary>One line per item, the items whose oldest upgrades fell past MaxRecent.</summary>
        public readonly List<UpgradeFold> Folded = new List<UpgradeFold>();

        public bool IsEmpty => FromUtc == DateTime.MinValue && Recent.Count == 0 && Folded.Count == 0;
        /// <summary>Every upgrade recorded (whole and folded).</summary>
        public int Total => Recent.Count + Folded.Sum(f => f.Count);

        /// <summary>One upgrade more (newest first), then the oldest past MaxRecent folded per item.</summary>
        public void Add(UpgradeMoment m)
        {
            if (m == null || string.IsNullOrEmpty(m.Token) || m.To <= m.From) return;
            int at = 0;
            while (at < Recent.Count && Recent[at].Utc > m.Utc) at++;   // in time order even if a clock stepped back
            Recent.Insert(at, m);
            while (Recent.Count > MaxRecent) { Fold(Recent[Recent.Count - 1]); Recent.RemoveAt(Recent.Count - 1); }
        }

        void Fold(UpgradeMoment m)
        {
            var day = m.Utc.Date;
            var f = Folded.FirstOrDefault(x => x.Token == m.Token);
            if (f == null)
            {
                if (Folded.Count >= MaxFolded - 1) f = Folded.FirstOrDefault(x => x.Token == DeedLog.OtherToken) ?? AddFold(DeedLog.OtherToken, day);   // past the cap: one line for the rest, in the last slot, so it is written and read back with the others (review fix)
                else f = AddFold(m.Token, day);
            }
            f.Count++; f.MaxLevel = Math.Max(f.MaxLevel, m.To);
            if (day < f.First) f.First = day;
            if (day > f.Last) f.Last = day;
        }
        UpgradeFold AddFold(string token, DateTime day) { var f = new UpgradeFold { Token = token, First = day, Last = day }; Folded.Add(f); return f; }

        /// <summary>Per item, everything recorded (whole and folded): the highest level reached, how many upgrades, first and last day (UTC dates).
        /// Most upgrades first, then by token.</summary>
        public List<UpgradeFold> PerItem()
        {
            var d = new Dictionary<string, UpgradeFold>();
            UpgradeFold Of(string t, DateTime day) { if (!d.TryGetValue(t, out var f)) d[t] = f = new UpgradeFold { Token = t, First = day, Last = day }; return f; }
            foreach (var f in Folded)
            {
                var x = Of(f.Token, f.First); x.Count += f.Count; x.MaxLevel = Math.Max(x.MaxLevel, f.MaxLevel);
                if (f.First < x.First) x.First = f.First;
                if (f.Last > x.Last) x.Last = f.Last;
            }
            foreach (var m in Recent)
            {
                var x = Of(m.Token, m.Utc.Date); x.Count++; x.MaxLevel = Math.Max(x.MaxLevel, m.To);
                if (m.Utc.Date < x.First) x.First = m.Utc.Date;
                if (m.Utc.Date > x.Last) x.Last = m.Utc.Date;
            }
            return d.Values.OrderByDescending(f => f.Count).ThenBy(f => f.Token, StringComparer.Ordinal).ToList();
        }

        /// <summary>The upgrades from <paramref name="fromUtc"/> up to (not including) <paramref name="toUtc"/>, newest first.</summary>
        public List<UpgradeMoment> Between(DateTime fromUtc, DateTime toUtc) => Recent.Where(m => m.Utc >= fromUtc && m.Utc < toUtc).ToList();

        // ---------- the file (LocalTotals, top-level "upgrades") ----------

        internal static string Iso(DateTime d) => d.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);
        internal static string Day(DateTime d) => d.ToString("yyyy-MM-dd", Inv);
        internal static DateTime Time(string s) => DateTime.TryParse(s, Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : DateTime.MinValue;

        /// <summary>{"from":"..","recent":[{"t":token,"a":from,"b":to,"s":station,"l":level,"u":utc}],"folded":[{"t":token,"max":n,"n":count,"first":day,"last":day}]}</summary>
        public void WriteTo(Json j, string key = "upgrades")
        {
            j.Key(key).Open();
            if (FromUtc > DateTime.MinValue) j.Str("from", FromUtc.ToString("o", Inv));
            j.Key("recent").OpenArr();
            foreach (var m in Recent.Take(MaxRecent))
            {
                j.Open().Str("t", m.Token).Num("a", m.From).Num("b", m.To);
                if (!string.IsNullOrEmpty(m.Station)) j.Str("s", m.Station);
                if (m.StationLevel > 0) j.Num("l", m.StationLevel);
                j.Str("u", Iso(m.Utc)).Close();
            }
            j.CloseArr();
            j.Key("folded").OpenArr();
            foreach (var f in Folded.Take(MaxFolded)) j.Open().Str("t", f.Token).Num("max", f.MaxLevel).Num("n", f.Count).Str("first", Day(f.First)).Str("last", Day(f.Last)).Close();
            j.CloseArr();
            j.Close();
        }

        /// <summary>Reads what WriteTo wrote, bounded (MaxRecent whole, MaxFolded lines; anything unreadable skipped). null: an empty log.</summary>
        public static UpgradeLog ReadFrom(Dictionary<string, object> o)
        {
            var l = new UpgradeLog();
            if (o == null) return l;
            l.FromUtc = Time(MiniJson.Str(o, "from"));
            if (o.TryGetValue("recent", out var r) && r is List<object> recent)
                foreach (var e in recent.OfType<Dictionary<string, object>>().Take(MaxRecent))
                {
                    var m = new UpgradeMoment { Token = MiniJson.Str(e, "t"), From = (int)MiniJson.Num(e, "a"), To = (int)MiniJson.Num(e, "b"), Station = MiniJson.Str(e, "s"), StationLevel = (int)MiniJson.Num(e, "l"), Utc = Time(MiniJson.Str(e, "u")) };
                    if (m.Token.Length > 0 && m.To > m.From && m.Utc > DateTime.MinValue) l.Recent.Add(m);
                }
            l.Recent.Sort((a, b) => b.Utc.CompareTo(a.Utc));
            if (o.TryGetValue("folded", out var fo) && fo is List<object> folded)
                foreach (var e in folded.OfType<Dictionary<string, object>>().Take(MaxFolded))
                {
                    var f = new UpgradeFold { Token = MiniJson.Str(e, "t"), MaxLevel = (int)MiniJson.Num(e, "max"), Count = (int)MiniJson.Num(e, "n"), First = Time(MiniJson.Str(e, "first")).Date, Last = Time(MiniJson.Str(e, "last")).Date };
                    if (f.Token.Length > 0 && f.Count > 0 && l.Folded.All(x => x.Token != f.Token)) l.Folded.Add(f);
                }
            return l;
        }
    }

    /// <summary>
    /// The upgrade hook's rule (UpgradeHooks on InventoryGui.DoCrafting), as a pure function. The prefix notes the item being upgraded (the very
    /// ItemData) and its level; the game then takes that item out and puts a new one of the new level in its slot. The postfix asks:
    /// the old item still in the bag: nothing happened (a check failed, nothing was crafted); the slot empty or holding another item: the
    /// upgrade broke it (an upgrader station's break); the same item at a level no higher than before: an upgrader's failure (a level lost).
    /// Only a higher level in the slot is an upgrade; its level is the new one.
    /// </summary>
    public static class UpgradeJudge
    {
        public static int? NewLevel(bool oldStillHeld, string tokenInSlot, int levelInSlot, string token, int from)
        {
            if (oldStillHeld || string.IsNullOrEmpty(token) || tokenInSlot != token) return null;
            return levelInSlot > from ? levelInSlot : (int?)null;
        }
    }
}
