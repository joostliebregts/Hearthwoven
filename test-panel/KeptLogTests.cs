// 0.8.2 (Joost, right after restarting the game to install a build: his own Battle > Damage in 3 h said "Nothing in the last 3 hours. Choose a
// longer window, or All for everything." though he had fought all evening, and Everyone > Damage in 3 h showed "You: no damage dealt"): every
// connection started its minute log from zero. Now the last minutes are kept on the PC (KeptMinutes) and the next connection in the same world
// reads its log with them under it (Plugin.LogWithEarlier, PanelInput.LogSessionFromUtc), and a fellow's copy carries theirs ("logBefore",
// FellowTrail.Joined). Here, through the file as the game does it: the short windows reach back, Session stays the new connection's, and a
// fellow's kept minutes count once whether or not this PC saw their earlier connection. Fictional names.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class KeptLogTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        HitData.DamageTypes D(float slash) => new HitData.DamageTypes { m_slash = slash };
        var dir = Path.Combine(Path.GetTempPath(), "hw-keptlog-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        const string world = "Midgard-123456789";

        // ---------- your evening: a Seeker 40 min ago (500 dealt, 80 received, one block), then the game restarted 5 min ago ----------
        var restart = now.AddMinutes(-5); var connected = restart.AddSeconds(40);
        var evening = new EventLog();
        evening.AddDamage(now.AddMinutes(-40), "Mistlands", true, "Seeker", "Swords", D(500));
        evening.AddDamage(now.AddMinutes(-40), "Mistlands", false, "Seeker", "EnemyHit", D(80));
        evening.AddGuard(now.AddMinutes(-40), false);
        var path = KeptMinutes.PathFor(LocalTotals.PathFor(dir, 7, "Rowan"));
        var taken = KeptMinutes.Take(restart, evening, null); taken.World = world; taken.PlayerId = "7"; taken.Name = "Rowan"; taken.Save(path, restart);
        var kept = KeptMinutes.Load(path, world, 7, "Rowan", now, connected, out _);

        PanelInput Self(bool withKept, List<PanelInput> fellows = null)
        {
            var thisSession = new EventLog();   // the new connection has fought nothing yet
            var self = new PanelInput { IsSelf = true, PlayerName = "Rowan", NowUtc = now, SessionStartUtc = connected.AddMinutes(1), ToLocal = t => t.AddHours(2), PlayerNames = new Dictionary<long, string>(),
                                        Log = withKept ? EventLog.Join(kept.Log, thisSession) : thisSession, LogSessionFromUtc = withKept ? connected : (DateTime?)null,
                                        Session = new DamageTally(), Events = new SessionEvents(), SessionOnly = new SessionEvents(), DamageSinceInstall = new DamageTally() };
            self.Fellows = fellows ?? new List<PanelInput>();
            return self;
        }
        PanelView Battle(PanelInput i, string page, TimeWindow w, bool everyone = false, string player = "") =>
            PanelModel.Build(i, new PanelState { Chapter = Chapter.Battle, Window = w, WindowPicked = true, Everyone = everyone, Player = player, Page = { [Chapter.Battle] = page }, View = { ["Battle/damage/view"] = "foe" } });
        bool Seeker(PanelView v) => PanelModel.AllText(v).Any(t => t.Contains("Seeker"));
        string Empty(PanelView v) => PanelModel.Content(v).FirstOrDefault(b => b.Kind == "empty")?.Title;

        // ---------- the bug as it was, then your book with the kept minutes: 1 h and 3 h reach back, Session and 10 min do not ----------
        var before = Battle(Self(false), "damage", TimeWindow.LastThreeHours);
        var self = Self(true);
        var hour = Battle(self, "damage", TimeWindow.LastHour); var three = Battle(self, "damage", TimeWindow.LastThreeHours);
        var session = Battle(self, "damage", TimeWindow.Session); var ten = Battle(self, "damage", TimeWindow.LastTenMinutes);
        Check(Empty(before) == "Nothing in the last 3 hours" && Seeker(hour) && Seeker(three) && Empty(three) == null && !Seeker(session) && Empty(session) == PanelModel.NothingYet + " this session" && !Seeker(ten) &&
              PanelModel.Damage(self.Log, TimeWindow.LastThreeHours, "", now, PanelModel.SessionFrom(self)).Where(r => r.Dir == "dealt").Sum(r => r.Amount) == 500,
              "K own book: before, 3 h after a restart said \"" + Empty(before) + "\"; now 1 h and 3 h show the Seeker fought 40 min ago (500 dealt), Session says \"" + Empty(session) + "\" and 10 min shows nothing");
        var defense3 = PanelModel.Content(Battle(self, "defense", TimeWindow.LastThreeHours)).FirstOrDefault(b => b.Kind == "guard");
        var defenseS = PanelModel.Content(Battle(self, "defense", TimeWindow.Session)).FirstOrDefault(b => b.Kind == "guard");
        Check(defense3?.Value == "1" && defenseS == null, "K own book, Defense: the block before the restart counts in 3 h (" + defense3?.Value + "), not in Session");

        // ---------- a fellow, Ylva: the same evening on her PC, her copy after her restart carries her kept minutes ----------
        var ylvaKept = new KeptMinutes { FromUtc = KeptMinutes.From(now), Log = evening.Between(KeptMinutes.From(now), connected) };
        string Copy(string session, (DateTime, string, float)[] fights, bool withKept) =>
            RejoinTests.Copy(session, "0.8.2", fights).Replace(",\"measuredSinceInstall\"", withKept ? ",\"" + KeptMinutes.SharedKey + "\":" + ylvaKept.SharedJson() + ",\"measuredSinceInstall\"" : ",\"measuredSinceInstall\"");
        var y1 = Copy("y1", new[] { (now.AddMinutes(-40), "Seeker", 500f) }, false); var y2 = Copy("y2", new (DateTime, string, float)[0], true);
        var key = FellowIds.KeyOf(y2);
        PanelInput Ylva(FellowTrails trails, DateTime viewerStart)
        {
            var c = trails.Of(key).Joined(FellowCopies.Of(key, y2));
            c.PlayerName = "Ylva"; c.NowUtc = now; c.ToLocal = t => t.AddHours(2); c.PlayerNames = new Dictionary<long, string>(); c.ViewerName = "Rowan";
            c.Trail = trails.Of(key); c.Timed = true; c.ServerLive = true; c.OnThisSession = true; c.ViewerSessionStartUtc = viewerStart; c.ReceivedUtc = now.AddSeconds(-5);
            return c;
        }
        float Dealt(PanelInput p, TimeWindow w) => PanelModel.Damage(p.Log, w, "", now, PanelModel.SessionFrom(p)).Where(r => r.Dir == "dealt").Sum(r => r.Amount);
        // you came in after her restart: this PC never saw her earlier connection, her copy brings it
        var late = new FellowTrails(); late.Add(key, y2, now.AddSeconds(-5));
        var afterHer = Ylva(late, now.AddMinutes(-3));
        // you played through her restart: this PC received her earlier connection too, and her kept minutes cover the same fight
        var through = new FellowTrails(); through.Add(key, y1, now.AddMinutes(-20)); through.Add(key, y2, now.AddSeconds(-5));
        var acrossHers = Ylva(through, now.AddMinutes(-60));
        afterHer.Fellows = new List<PanelInput> { Self(true) };
        var ylvaThree = Battle(afterHer, "damage", TimeWindow.LastThreeHours, player: "Ylva");
        Check(Dealt(afterHer, TimeWindow.LastThreeHours) == 500 && Dealt(afterHer, TimeWindow.Session) == 0 && Seeker(ylvaThree) &&
              Dealt(acrossHers, TimeWindow.LastThreeHours) == 500 && Dealt(acrossHers, TimeWindow.Session) == 500 && FellowCopies.Of(key, y2).Log.Damage.Count == 0,
              "K a fellow's restart: her 3 h shows her Seeker from before it (" + Dealt(afterHer, TimeWindow.LastThreeHours) + ") though your PC never saw it, not in your Session that began after; " +
              "seen both ways (your PC got her earlier copy too) it counts once (" + Dealt(acrossHers, TimeWindow.LastThreeHours) + "), in your Session as well; her shared copy keeps its own log");

        // ---------- Everyone > Damage in 3 h: "You" has your damage again, beside Ylva's ----------
        var group = Self(true, new List<PanelInput> { afterHer }); afterHer.Fellows = new List<PanelInput> { group };
        var rows = PanelModel.Content(Battle(group, "damage", TimeWindow.LastThreeHours, everyone: true)).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>();
        var you = rows.FirstOrDefault(r => r.Title == "You"); var her = rows.FirstOrDefault(r => r.Id == "Ylva");
        Check(you?.Kind == PanelModel.GroupMemberKind && you.Value == "500" && her?.Value == "500",
              "K Everyone > Damage in 3 h: You " + (you?.Value ?? you?.Text ?? "missing") + " (was \"no damage dealt\"), Ylva " + (her?.Value ?? her?.Text ?? "missing"));

        try { Directory.Delete(dir, true); } catch { }
        return fails;
    }
}
