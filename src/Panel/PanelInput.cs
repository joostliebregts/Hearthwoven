using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Everything the panel reads about one player. Built either from the live objects on this PC (PanelUi) or from a
    /// fellow player's shared snapshot (FromSnapshot), so every chapter renders the same way for anyone.
    /// Pure C#: filled by hand in the tests.
    /// </summary>
    public class PanelInput
    {
        public string PlayerName = "";
        public long PlayerId;                               // the game's player id (feasts name their creator by it)
        public DateTime NowUtc = DateTime.UtcNow;
        public DateTime? SessionStartUtc;                   // first spawn of this connection (a session); null = not known
        public bool IsSelf = true;                          // false: a fellow player's shared snapshot
        public string ViewerName;                           // who is looking (marked "(you)" in someone else's company)
        public DateTime? LastRecordedUtc;                   // fellow player: newest moment in their shared log

        // since this character was made (the game's own profile, slot 0)
        public IDictionary<string, float> Character;        // PlayerStatType name -> value; null = no profile
        public IDictionary<string, float> ItemsCrafted;     // item token ("$item_bread") -> count
        public IDictionary<string, float> PiecesPlaced;     // piece token ("$piece_woodwall") -> count
        public IDictionary<string, float> EnemyKills;       // enemy token ("$enemy_troll") -> kills
        public IDictionary<string, float> SkillLevels;      // skill name ("Blocking") -> level now
        public IDictionary<string, float> ItemsPickedUp;    // item token ("$item_wood") -> amount, first time anyone held it (any source)

        // what the game data says about a token, derived at runtime by PanelUi (item type, drop tables, piece components).
        // null (or a null answer) = unknown; PanelModel then falls back to the vanilla names it knows or keeps it apart.
        public Func<string, string> ItemKind;               // item token -> "gear" | "food" | "other"
        public Func<string, string> GatherKind;             // item token -> "wood" (dropped by trees and logs) | "mining" (by rocks)
        public Func<string, string> PieceKind;              // piece token -> "built" | "ground" | "planted" | "feast"

        // measured by Hearthwoven since installation, this session
        public DamageTally Session;
        public SessionEvents Events;
        public EventLog Log;

        public List<PanelInput> Fellows;                    // everyone else who shares (and you, when looking at a fellow player)
        public IDictionary<long, string> PlayerNames;       // player id -> name, for feast creators (players nearby)
        public Func<string, string> DisplayName;            // prefab or $token -> localized name; null = prettified
        public Func<DateTime, DateTime> ToLocal;            // null = the PC's own time zone

        /// <summary>
        /// A fellow player's shared snapshot (Snapshot.Build output, received through GroupShare). Rebuilds the mod's own
        /// recorder objects from the JSON; null if it cannot be read. Shared copies carry no death positions.
        /// </summary>
        public static PanelInput FromSnapshot(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) return null;
            var input = new PanelInput { IsSelf = false, PlayerName = Str(root, "name"), PlayerId = (long)Num(root, "playerId"), Session = new DamageTally(), Events = new SessionEvents(), Log = new EventLog() };
            if (root.TryGetValue("stats", out var st) && st is List<object> slots && slots.Count > 0 && slots[0] is Dictionary<string, object> slot0)
            {
                input.Character = Dict(Obj(slot0, "counters"));
                input.ItemsCrafted = Dict(Obj(slot0, "itemsCrafted"));
                input.PiecesPlaced = Dict(Obj(slot0, "piecesPlaced"));
                input.ItemsPickedUp = Dict(Obj(slot0, "itemsPickedUp"));
                if (slot0.TryGetValue("enemyKills", out var ek) && ek is List<object> tables && tables.Count > 0 && tables[0] is Dictionary<string, object> t0)
                    input.EnemyKills = Dict(Obj(t0, "kills"));
            }
            if (root.TryGetValue("skills", out var sk) && sk is List<object> skills)
            {
                input.SkillLevels = new Dictionary<string, float>();
                foreach (var s in skills.OfType<Dictionary<string, object>>()) if (Str(s, "skill").Length > 0) input.SkillLevels[Str(s, "skill")] = (float)Num(s, "level");
            }
            var dmg = Obj(root, "damageThisSession");
            Into(Obj(dmg, "dealt"), input.Session.Dealt); Into(Obj(dmg, "taken"), input.Session.Taken);
            input.Session.HitsDealt = (int)Num(dmg, "hitsDealt"); input.Session.HitsTaken = (int)Num(dmg, "hitsTaken");
            var ev = Obj(root, "measuredThisSession"); var e = input.Events;
            e.Blocks = (int)Num(ev, "blocks"); e.Parries = (int)Num(ev, "parries");
            Into(Obj(ev, "ateFoodMadeBy"), e.AteFoodMadeBy); Into(Obj(ev, "ateFromFeastOf"), e.AteFromFeastOf); Into(Obj(ev, "bought"), e.Bought);
            Into(Obj(ev, "coinsSpent"), e.Spent); Into(Obj(ev, "smelterAdded"), e.SmelterAdded); Into(Obj(ev, "pickaxeHits"), e.PickaxeHits);
            Into(Obj(ev, "skillPractice"), e.SkillPractice); Into(Obj(ev, "equippedGearMadeBy"), e.EquippedGearMadeBy);
            Into(Obj(ev, "sailedWith"), e.SailedWith); Into(Obj(ev, "sailedUnderHelmOf"), e.SailedUnderHelmOf);
            Into(Obj(ev, "chopHits"), e.ChopHits); Into(Obj(ev, "repairs"), e.Repairs); Into(Obj(ev, "mapShared"), e.MapShared); Into(Obj(ev, "cartMeters"), e.CartMeters);
            Into(Obj(ev, "pickedUp"), e.PickedUp);
            var log = Obj(root, "measuredLog");
            Into(Obj(log, "damage"), input.Log.Damage); Into(Obj(log, "hits"), input.Log.Hits);
            DateTime? last = null;
            foreach (var k in input.Log.Damage.Keys)
                if (DateTime.TryParseExact(k.Split('|')[0], "yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var b) && (last == null || b > last)) last = b;
            if (log != null && log.TryGetValue("deaths", out var ds) && ds is List<object> deaths)
                foreach (var o in deaths.OfType<Dictionary<string, object>>())
                {
                    if (!DateTime.TryParse(Str(o, "t"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)) continue;
                    var d = new EventLog.Death { Time = t.ToUniversalTime(), Biome = Str(o, "biome", "None"), Killer = Str(o, "killer", "unknown"), Cause = Str(o, "cause", "unknown") };
                    Into(Obj(o, "last10s"), d.Last10s);
                    input.Log.Deaths.Add(d);
                    if (last == null || d.Time > last) last = d.Time;
                }
            input.LastRecordedUtc = last;
            return input;
        }

        static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
        static double Num(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v is double x ? x : 0;
        static string Str(Dictionary<string, object> d, string k, string otherwise = "") => d != null && d.TryGetValue(k, out var v) && v is string x ? x : otherwise;
        static Dictionary<string, float> Dict(Dictionary<string, object> from) { if (from == null) return null; var d = new Dictionary<string, float>(); Into(from, d); return d; }
        static void Into(Dictionary<string, object> from, IDictionary<string, float> to)
        {
            if (from == null) return;
            foreach (var kv in from) if (kv.Value is double v) to[kv.Key] = (float)v;
        }
    }
}
