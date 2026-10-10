using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// SAMPLE data (fictional) for Deeds > Recent (0.7): an own book's minute log (DeedLog) for this session, made from the same lists as the
    /// rest of the sample world so nothing can drift: what the session did IS today's row of the day history (SampleWorld.History), spread over
    /// the session's minutes, a little more of it towards the end (so the last ten minutes hold a handful of things). The kills per foe (which
    /// have no days) are today's kill count shared over the foes the character fought. Deterministic (seeded by the player id).
    /// The fellows' copies (B33): what reached Rowan's PC from them while Rowan played (Fellows: a trail whose growth is each one's shared session
    /// tally, most of it early, a little in the last ten minutes); Edda and Finch with live updates, Tor on full copies only.
    /// </summary>
    public static class RecentSample
    {
        /// <summary>The fellows Rowan's PC hears from with live updates (the others only every few minutes).</summary>
        public static readonly string[] Timed = { "Edda", "Finch" };

        /// <summary>Each fellow copy of <paramref name="self"/>'s book gets the trail Rowan's PC would have kept: the first copy 20 s after the
        /// session began (their totals less what they did this session), then 80 % of it 25 minutes ago, 95 % 5 minutes ago, all of it a minute ago.</summary>
        public static void Fellows(PanelInput self)
        {
            if (self?.Fellows == null || !self.SessionStartUtc.HasValue) return;
            foreach (var f in self.Fellows)
            {
                if (f == null || f.IsSelf || f.Trail != null) continue;
                var now = FellowTrail.Flat(f);
                var grew = new Dictionary<string, float>();
                foreach (var fam in (f.SessionOnly ?? new SessionEvents()).Named())
                    if (Array.IndexOf(DeedLog.EventFamilies, fam.Key) >= 0) foreach (var t in fam.Value) if (t.Value > 0) grew[fam.Key + "|" + t.Key] = t.Value;
                Dictionary<string, float> At(float part) => now.ToDictionary(kv => kv.Key, kv => kv.Value - (grew.TryGetValue(kv.Key, out var g) ? (float)Math.Round(g * (1 - part)) : 0f));   // whole things: a tree is felled or not
                var t0 = self.SessionStartUtc.Value.AddSeconds(20);
                var trail = new FellowTrail(At(0f), f.SharedSinceInstall, t0);
                foreach (var (minutesAgo, part) in new[] { (25, 0.8f), (5, 0.95f), (1, 1f) })
                {
                    var at = self.NowUtc.AddMinutes(-minutesAgo);
                    if (at > t0) trail.Add(At(part), at);
                }
                f.Trail = trail; f.Timed = Array.IndexOf(Timed, f.PlayerName) >= 0; f.OnThisSession = true; f.ViewerSessionStartUtc = self.SessionStartUtc;
            }
        }

        public static void Session(PanelInput p)
        {
            if (p == null || p.History == null || !p.IsSelf) return;
            var start = p.SessionStartUtc ?? p.NowUtc.AddHours(-3);
            var minutes = Math.Max(1, (int)(p.NowUtc - start).TotalMinutes);
            var today = PanelModel.LocalToday(p);
            var row = p.History.Sum(today, today);
            var rnd = new Random(1000 + (int)(p.PlayerId % 1000));
            var plan = new List<(int minute, Action<SessionEvents, Dictionary<string, float>> act)>();
            // a minute of the session, more often towards its end (1 - u^1.8: about a fifth of the chunks in the last ten minutes of three and a half hours)
            int Minute() => Math.Min(minutes - 1, (int)(minutes * (1 - Math.Pow(rnd.NextDouble(), 1.8))));
            void Spread(float amount, bool whole, Action<float, SessionEvents, Dictionary<string, float>> add)
            {
                if (amount <= 0) return;
                var chunks = whole ? (int)Math.Min(amount, 1 + rnd.Next(5)) : 1 + rnd.Next(4);
                var left = amount;
                for (int c = 0; c < chunks; c++)
                {
                    var part = c == chunks - 1 ? left : whole ? (float)Math.Max(1, Math.Floor(amount / chunks)) : amount / chunks;
                    if (part <= 0) break;
                    left -= part;
                    var at = part;
                    plan.Add((Minute(), (e, g) => add(at, e, g)));
                }
            }
            foreach (var fam in row.Events.Named().Where(f => Array.IndexOf(DeedLog.EventFamilies, f.Key) >= 0))
            {
                var name = fam.Key;
                foreach (var kv in fam.Value.ToList())
                {
                    var token = kv.Key;
                    Spread(kv.Value, name != DeedLog.Skills, (n, e, g) => SessionEvents.Add(e.Named().First(x => x.Key == name).Value, token, n));
                }
            }
            foreach (var kv in DayHistory.Family(row.Game, DayHistory.PlacedPrefix))
            {
                var token = DeedLog.Placed + "|" + kv.Key;
                Spread(kv.Value, true, (n, e, g) => SessionEvents.Add(g, token, n));
            }
            // the kills: today's count over the foes fought (bosses left out: a boss is a moment of its own, not an evening's tally)
            row.Game.TryGetValue("EnemyKills", out var kills);
            var foes = (p.EnemyKills ?? new Dictionary<string, float>()).Where(kv => kv.Value > 0 && !kv.Key.Contains("eikthyr") && !kv.Key.Contains("gdking") && !kv.Key.Contains("bonemass") && !kv.Key.Contains("dragon"))
                                                                         .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(5).ToList();
            if (kills > 0 && foes.Count > 0)
            {
                var total = foes.Sum(kv => (double)kv.Value); var given = 0;
                for (int i = 0; i < foes.Count; i++)
                {
                    var n = i == foes.Count - 1 ? (int)kills - given : (int)Math.Floor(kills * foes[i].Value / total);
                    if (n <= 0) continue;
                    given += n;
                    var token = DeedLog.Kills + "|" + foes[i].Key;
                    Spread(n, true, (a, e, g) => { SessionEvents.Add(g, token, a); SessionEvents.Add(g, DeedLog.Counters + "|EnemyKills", a); });
                }
            }
            foreach (var stat in new[] { "FishCaught", "CreatureTamed" })
                if (row.Game.TryGetValue(stat, out var v) && v > 0) { var key = DeedLog.Counters + "|" + stat; Spread(v, true, (a, e, g) => SessionEvents.Add(g, key, a)); }

            var events = new SessionEvents(); var game = new Dictionary<string, float>(); var log = new DeedLog();
            var byMinute = plan.GroupBy(x => x.minute).ToDictionary(x => x.Key, x => x.Select(y => y.act).ToList());
            for (int m = 0; m < minutes; m++)
            {
                log.Tick(start.AddMinutes(m).AddSeconds(30), events, () => game);   // books the minute that ended (the first call takes the mark)
                if (byMinute.TryGetValue(m, out var acts)) foreach (var a in acts) a(events, game);
            }
            log.Flush(p.NowUtc, events, game);   // the running minute, as the panel flushes before it reads
            p.Deeds = log;
        }
    }
}
