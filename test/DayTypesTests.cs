// 0.9 (Joost's option A, "Today works like everything else"): fellows share their damage dealt per day split by weapon kind and type
// ("dayDealt", DayHistory.DayDealt) beside the day totals ("dealtByDay"), so the group's By weapon and By type work on Today, 7 days and
// 30 days. A day's parts add up to its total exactly; the bytes a typical and a busy month add to a realistic shared copy; a live update
// that carries today's growth passes the 0.8.1 server's own checks in one message; a copy reads back what was sent, and an older one has
// none. Fictional players and fights.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class DayTypesTests
{
    static readonly DateTime Today = new DateTime(2026, 10, 10, 21, 0, 0);   // local evenings

    enum Kind { Typical, Busy, Worst }

    // one evening's fights: per foe, the skill the hit was booked on and its damage per type
    static DamageTally Evening(Random rnd, Kind kind)
    {
        var d = new DamageTally();
        void Hit(string foe, string skill, string type, int lo, int hi) => d.Dealt[foe + "|" + skill + "|" + type] = (float)Math.Round(rnd.Next(lo, hi) + rnd.NextDouble(), 2);
        if (kind == Kind.Typical)
        {
            // a typical evening: a sword and a bow, now and then fire arrows, a club or a frost staff, a few foe kinds
            foreach (var foe in new[] { "Greydwarf", "Troll", "Draugr" }.Take(rnd.Next(1, 4))) { Hit(foe, "Swords", "slash", 200, 2500); if (rnd.Next(2) == 0) Hit(foe, "Bows", "pierce", 100, 1500); }
            if (rnd.Next(3) == 0) Hit("Troll", "Bows", "fire", 80, 600);
            if (rnd.Next(4) == 0) Hit("Skeleton", "Clubs", "blunt", 100, 900);
            if (rnd.Next(5) == 0) Hit("Wolf", "ElementalMagic", "frost", 100, 800);
            d.Dealt["Beech1|WoodCutting|chop"] = 900;   // tool damage: never battle, never shared
            return d;
        }
        if (kind == Kind.Busy)
        {
            // a busy day in the Mistlands: a frost-and-spirit mace and a sword, a bow with three kinds of arrows, a staff most days; thousands each
            foreach (var foe in new[] { "Seeker", "Seeker Soldier", "Gjall", "Tick", "Dvergr" }.Take(rnd.Next(2, 6)))
            {
                Hit(foe, "Clubs", "blunt", 2000, 30000); Hit(foe, "Clubs", "frost", 500, 8000); Hit(foe, "Clubs", "spirit", 500, 8000);
                if (rnd.Next(2) == 0) Hit(foe, "Swords", "slash", 2000, 25000);
                Hit(foe, "Bows", "pierce", 1000, 20000);
                foreach (var arrow in new[] { "fire", "frost", "poison", "spirit" }.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4))) Hit(foe, "Bows", arrow, 300, 6000);
                if (rnd.Next(3) > 0) Hit(foe, "ElementalMagic", new[] { "fire", "frost", "lightning" }[rnd.Next(3)], 1000, 15000);
            }
            return d;
        }
        // the worst a month can bring (a bound, no one plays it): every weapon kind with every one of the eight types every day, large numbers
        foreach (var skill in new[] { "Swords", "Bows", "ElementalMagic", "Crossbows", "BloodMagic", "Clubs" })
            foreach (var type in PanelModel.BattleTypes) Hit("Seeker Soldier", skill, type, 1000, 140000);
        return d;
    }

    // a month of play: three evenings a week (typical) or every day (busy, worst), each evening saved as it ends
    static DayHistory Month(Kind kind, int seed)
    {
        var rnd = new Random(seed); var h = new DayHistory();
        for (var d = Today.AddDays(-34); d <= Today; d = d.AddDays(1))
        {
            if (kind == Kind.Typical && d.DayOfWeek != DayOfWeek.Tuesday && d.DayOfWeek != DayOfWeek.Friday && d.DayOfWeek != DayOfWeek.Saturday && d != Today) continue;
            h.Add(d, null, Evening(rnd, kind), null, null);
        }
        return h;
    }

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- a day's parts add up to that day's total, today with the running session's unsaved part ----------
        var typical = Month(Kind.Typical, 3); var busy = Month(Kind.Busy, 5); var worst = Month(Kind.Worst, 7);
        var pending = new DayHistory.Row { Period = DayHistory.DayKey(Today.Date) };
        pending.Damage.Dealt["Neck|Crossbows|pierce"] = 333.33f; pending.Damage.Dealt["Neck|Unarmed|blunt"] = 12.4f; pending.Damage.Dealt["Neck|Axes|chop"] = 50;
        var bad = new List<string>();
        foreach (var (name, h, pend) in new[] { ("typical", typical, (DayHistory.Row)null), ("typical+live", typical, pending), ("busy", busy, null), ("busy+live", busy, pending), ("worst", worst, null) })
        {
            var totals = h.DealtByDay(Today, 30, pend); var parts = h.DayDealt(Today, 30, pend);
            if (!totals.Keys.OrderBy(k => k).SequenceEqual(parts.Keys.OrderBy(k => k))) bad.Add(name + ": days " + totals.Count + " vs " + parts.Count);
            foreach (var kv in totals)
                if (!parts.TryGetValue(kv.Key, out var p) || Math.Abs(p.Values.Sum() - kv.Value) > 0.001 || p.Values.Any(v => v <= 0 || v != Math.Floor(v)) ||
                    p.Keys.Any(k => !PanelModel.WeaponKinds.Contains(k.Split('|')[0]) || !PanelModel.BattleTypes.Contains(k.Split('|')[1])))
                    bad.Add(name + " " + kv.Key + ": total " + kv.Value + ", parts " + (p == null ? "none" : string.Join(",", p.Select(x => x.Key + "=" + x.Value))));
        }
        var todayParts = typical.DayDealt(Today, 30, pending)[DayHistory.DayKey(Today.Date)];
        var bowsToday = typical.Rows.Last().Damage.Dealt.Where(kv => kv.Key.Split('|')[1] == "Bows" && kv.Key.EndsWith("|pierce")).Sum(kv => kv.Value) + 333.33;
        Check(bad.Count == 0 && todayParts.TryGetValue("bow|pierce", out var bp) && Math.Abs(bp - bowsToday) <= 1 && todayParts.ContainsKey("melee|blunt") && !todayParts.Keys.Any(k => k.EndsWith("|chop")),
              "DT sums: each shared day's parts (weapon kind and type, whole numbers) add up to that day's dealtByDay exactly, a typical, a busy and the worst month, " +
              "today with the live part (crossbow hits are bow, a fist is melee, tool damage is never shared)" + (bad.Count > 0 ? ": " + string.Join("; ", bad.Take(4)) : ""));

        // ---------- the bytes: a realistic shared copy (the live server's size, as LiveSyncTests) with and without the parts ----------
        var rnd = new Random(11);
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats();
        foreach (PlayerStatType t in Enum.GetValues(typeof(PlayerStatType))) stats[0][t] = (float)Math.Round(rnd.NextDouble() * 5000, 1);
        for (int i = 0; i < 251; i++) stats[0].m_itemPickupStats["$item_kind" + i] = rnd.Next(1, 900);
        for (int i = 0; i < 99; i++) stats[0].m_itemCraftStats["$item_made" + i] = rnd.Next(1, 60);
        for (int i = 0; i < 385; i++) stats[0].m_piecesPlacedStats["$piece_kind" + i] = rnd.Next(1, 400);
        var skills = Enumerable.Range(0, 28).Select(i => new Snapshot.SkillInfo { Name = "Skill" + i, Level = (float)Math.Round(rnd.NextDouble() * 70, 3), Accumulator = (float)Math.Round(rnd.NextDouble() * 50, 2) }).ToList();
        var dmg = new DamageTally(); var log = new EventLog(); var ev = new SessionEvents();
        var t0 = new DateTime(2026, 10, 10, 19, 0, 0, DateTimeKind.Utc);
        var hit = new HitData.DamageTypes { m_slash = 18.5f, m_fire = 4f };
        for (int m = 0; m < 90; m++) { log.AddDamage(t0.AddMinutes(m), "BlackForest", true, "Greydwarf", "Swords", hit); dmg.AddDealt("Greydwarf", "Swords", hit); }
        string Full(DayHistory h, DayHistory.Row pend, bool parts, string copyId = "s1-1") =>
            Snapshot.Build("0.9.0", 4242, "Edda", stats, skills, "Midgard", dmg, "s1", ev, log, true, ev, dmg, null, null, null,
                           h?.DealtByDay(Today, 30, pend), new[] { "Meadows", "BlackForest" }, copyId, dayDealt: parts ? h?.DayDealt(Today, 30, pend) : null);
        int Packed(string json) => Transport.Pack(GroupShare.SharedCopy(json)).Length;
        int baseTyp = Packed(Full(typical, null, false)), withTyp = Packed(Full(typical, null, true));
        int baseBusy = Packed(Full(busy, null, false)), withBusy = Packed(Full(busy, null, true));
        int baseWorst = Packed(Full(worst, null, false)), withWorst = Packed(Full(worst, null, true));
        var typJson = Full(typical, null, true); var busyJson = Full(busy, null, true);
        int Raw(string json) => MiniJson.Write(MiniJson.Obj(MiniJson.Parse(json) as Dictionary<string, object>, "dayDealt")).Length;
        System.Console.WriteLine($"INFO share: a realistic shared copy is {baseTyp} bytes packed; dayDealt adds {withTyp - baseTyp} bytes packed for a typical month ({typical.DayDealt(Today).Count} days played, " +
                                 $"{typical.DayDealt(Today).Sum(d => d.Value.Count)} parts, {Raw(typJson)} bytes of JSON) and {withBusy - baseBusy} for a busy one ({busy.DayDealt(Today).Count} days, " +
                                 $"{busy.DayDealt(Today).Sum(d => d.Value.Count)} parts, {Raw(busyJson)} bytes of JSON); the worst month (all 24 parts every day) {withWorst - baseWorst}; " +
                                 $"dealtByDay itself is {Packed(Full(busy, null, false)) - Packed(Full(null, null, false))} bytes packed (busy)");
        Check(withTyp - baseTyp <= 1024 && withBusy - baseBusy <= 2048 && withWorst - baseWorst <= 5 * 1024,
              "DT bytes: the parts add at most 1 KB packed to a realistic shared copy for a typical month (+" + (withTyp - baseTyp) + "), at most 2 KB for a busy one, every day in the Mistlands with three weapons (+" +
              (withBusy - baseBusy) + "), and at most 5 KB for the worst, every weapon kind with every type every day (+" + (withWorst - baseWorst) + ")");

        // ---------- the live update: today's growth through the 0.8.1 server's checks, one message ----------
        var baseFull = Full(worst, null, true); var baseShared = GroupShare.SharedCopy(baseFull);   // the worst month: the largest copy to update
        var grow = new DayHistory.Row { Period = DayHistory.DayKey(Today.Date) };
        var made = new List<LiveDelta.Made>(); var refused = new List<string>();
        for (int step = 1; step <= 12; step++)   // two minutes of a busy fight: every 10 s more of every kind and type
        {
            foreach (var skill in new[] { "Swords", "Bows", "ElementalMagic" }) foreach (var type in PanelModel.BattleTypes)
            { var k = "Seeker|" + skill + "|" + type; grow.Damage.Dealt.TryGetValue(k, out var o); grow.Damage.Dealt[k] = o + 377.7f; }
            var now = GroupShare.SharedCopy(Full(worst, grow, true));
            var m = LiveDelta.Make(baseShared, now, "s1-1", step); made.Add(m);
            if (m.Text == null || !ServerIntake.IsJsonObject(m.Text) || !LiveDelta.Read(m.Text, out _, out _) || LiveDelta.Private(m.Text) || LiveDelta.Identity(m.Text) || m.TooBig)
                refused.Add("step " + step + (m.TooBig ? " too big" : " refused"));
        }
        var last = made.Last(); var todayKey = DayHistory.DayKey(Today.Date);
        var paths = (MiniJson.Obj(MiniJson.Parse(last.Text) as Dictionary<string, object>, "set") ?? new Dictionary<string, object>()).Keys.Where(k => k.StartsWith("/dayDealt/")).ToList();
        var applied = LiveDelta.Apply(FellowIds.WithPlatform(baseShared, "76561198000000042"), last.Text);
        var read = applied == null ? null : PanelInput.FromSnapshot(applied);
        var sent = worst.DayDealt(Today, 30, grow);
        bool SameParts(Dictionary<string, Dictionary<string, float>> a, Dictionary<string, Dictionary<string, float>> b) =>
            a != null && b != null && a.Count == b.Count && a.All(d => b.TryGetValue(d.Key, out var x) && d.Value.Count == x.Count && d.Value.All(p => x.TryGetValue(p.Key, out var v) && v == p.Value));
        System.Console.WriteLine("  live: the update since the last full copy, two minutes into a busy fight on the worst month: " + string.Join(", ", made.Select(x => x.Packed?.Length ?? 0)) + " bytes packed (limit " + LiveDelta.MaxPacked + "); its dayDealt paths: " + paths.Count);
        Check(refused.Count == 0 && made.Max(x => x.Packed?.Length ?? 0) <= LiveDelta.MaxPacked && paths.Count > 0 && paths.All(p => p.StartsWith("/dayDealt/" + todayKey + "/")) && read != null && SameParts(read.DayDealt, sent),
              "DT live: each update of a busy fight is one message the 0.8.1 server keeps (its JSON object, base and seq, nothing private, no identity key; at most " + made.Max(x => x.Packed?.Length ?? 0) +
              " bytes packed); it carries only today's parts, and base + the latest update reads back the parts as sent" + (refused.Count > 0 ? ": " + string.Join(", ", refused) : ""));

        // ---------- a copy reads back the parts; nothing dealt is an empty split, an older copy has none ----------
        var copy = PanelInput.FromSnapshot(GroupShare.SharedCopy(typJson));
        var quiet = PanelInput.FromSnapshot(GroupShare.SharedCopy(Full(new DayHistory(), null, true)));
        var older = PanelInput.FromSnapshot(GroupShare.SharedCopy(Full(typical, null, false)));
        var hostile = PanelInput.FromSnapshot("{\"name\":\"Tor\",\"dayDealt\":{\"nonsense\":{\"melee|slash\":5},\"2026-10-10\":{\"melee|slash\":1e99,\"bow|pierce\":\"x\"},\"2026-10-09\":7}}");
        Check(SameParts(copy?.DayDealt, typical.DayDealt(Today)) && quiet?.DayDealt != null && quiet.DayDealt.Count == 0 && older != null && older.DayDealt == null && older.DealtByDay != null &&
              hostile?.DayDealt != null && hostile.DayDealt.Keys.SequenceEqual(new[] { "2026-10-10" }) && !hostile.DayDealt["2026-10-10"].ContainsKey("bow|pierce"),
              "DT read: a copy reads back every day's parts; a player with a day history and nothing dealt shares an empty split (not \"older\"); a copy without the key (before 0.9) has none, its day totals stay; only real days are read");
        return fails;
    }
}
