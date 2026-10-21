using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// 0.9 Armor sets, made or worn (Joost: "we should collect when people have interacted with it, not just crafted it"): the armor pieces your
    /// character has put on, whoever made them, yes or no per item token (the game's m_shared.m_name, as the craft counts key them). Your equip
    /// (ClientHooks.Equip: the game's Humanoid.EquipItem on your own player; read only, fail-safe) adds a token; at most MaxTokens, one entry each,
    /// however often you put it on. Counting begins on the first load of a version that has it (FromUtc): what you wore before is not known, so the
    /// page says "worn counted from 20 October" and never shows it as not worn. Kept in the local totals (LocalTotals, top-level "armorWorn"), shared
    /// in the copy as the tokens worn that the copy does not already say were made (Snapshot "armorWorn"). Pure C#, tested in test-panel/ArmorWornTests.cs.
    /// </summary>
    public sealed class ArmorWornBook
    {
        public const int MaxTokens = 300, MaxTokenLength = 120;
        public const string Key = "armorWorn";

        public DateTime FromUtc = DateTime.MinValue;
        public readonly HashSet<string> Tokens = new HashSet<string>(StringComparer.Ordinal);

        public bool Begun => FromUtc > DateTime.MinValue;
        public bool IsEmpty => !Begun && Tokens.Count == 0;

        /// <summary>Begins counting at <paramref name="utc"/> (once); true when it began now.</summary>
        public bool Begin(DateTime utc)
        {
            if (Begun) return false;
            FromUtc = utc; return true;
        }

        /// <summary>One armor piece put on: its token, once. Nothing before counting began, nothing past the cap. True when it is new.</summary>
        public bool Note(string token)
        {
            if (!Begun || !Usable(token) || Tokens.Contains(token) || Tokens.Count >= MaxTokens) return false;
            return Tokens.Add(token);
        }

        static bool Usable(string token) => !string.IsNullOrEmpty(token) && token.Length < MaxTokenLength;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The local file's form: {"from":"2026-10-20T18:00:00Z","tokens":["$item_helmet_leather",..]}.</summary>
        public void WriteTo(Json j, string key = Key)
        {
            j.Key(key).Open();
            if (Begun) j.Str("from", FromUtc.ToString("o", Inv));
            j.Key("tokens").OpenArr();
            foreach (var t in Tokens.OrderBy(t => t, StringComparer.Ordinal).Take(MaxTokens)) j.Val(t);
            j.CloseArr();
            j.Close();
        }

        /// <summary>Reads what WriteTo wrote, bounded; an empty record when there is none.</summary>
        public static ArmorWornBook ReadFrom(Dictionary<string, object> o)
        {
            var b = new ArmorWornBook();
            if (o == null) return b;
            b.FromUtc = Time(MiniJson.Str(o, "from"));
            if (o.TryGetValue("tokens", out var ts) && ts is List<object> list)
                foreach (var t in list.OfType<string>()) { if (b.Tokens.Count >= MaxTokens) break; if (Usable(t)) b.Tokens.Add(t); }
            return b;
        }

        /// <summary>The shared form (Snapshot, key "armorWorn"): the day counting began and the tokens worn that <paramref name="made"/> does not hold
        /// (the copy's "armorMade" says those already), sorted: {"from":"2026-10-20","t":[..]}. Nothing before counting began.</summary>
        public static void WriteShared(Json j, IEnumerable<string> worn, DateTime? fromUtc, IEnumerable<string> made)
        {
            if (!fromUtc.HasValue || fromUtc.Value == DateTime.MinValue || worn == null) return;
            var skip = new HashSet<string>(made ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            j.Key(Key).Open().Str("from", fromUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd", Inv));
            j.Key("t").OpenArr();
            foreach (var t in worn.Where(t => Usable(t) && !skip.Contains(t)).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).Take(MaxTokens)) j.Val(t);
            j.CloseArr();
            j.Close();
        }

        /// <summary>A fellow's shared "armorWorn" read back: the tokens and the day counting began (UTC midnight); null when the copy has none (an older
        /// Hearthwoven, or counting not begun).</summary>
        public static HashSet<string> ReadShared(Dictionary<string, object> o, out DateTime? fromUtc)
        {
            fromUtc = null;
            if (o == null) return null;
            var from = DateTime.TryParseExact(MiniJson.Str(o, "from"), "yyyy-MM-dd", Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : DateTime.MinValue;
            if (from == DateTime.MinValue) return null;
            fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (o.TryGetValue("t", out var ts) && ts is List<object> list)
                foreach (var t in list.OfType<string>()) { if (set.Count >= MaxTokens) break; if (Usable(t)) set.Add(t); }
            return set;
        }

        static DateTime Time(string s) => DateTime.TryParse(s, Inv, DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : DateTime.MinValue;
    }
}
