// 0.10 Dev.LiveReload (src/LiveReload.cs): Hearthwoven reloaded in the running game by ScriptEngine. The old copy saves every store as at a
// logout and the new copy reads them back under a new session id, as after a reconnect. Here, an evening of one connection with five
// reloads, the way Plugin.Leave (SaveLocal) and Plugin.JoinRunning (LoadLocal) do it, through the real file each time: the since-install
// totals, Today's row, the armour and foe ledgers, the feats (counts, tiers, seen state) and the meal combinations neither lose a part nor
// count one twice, and the minutes before a reload come back once for the short windows. The control shows why the new copy takes a new
// session id: carrying the old one with stores that start empty replaces the session with a smaller one. Then the note the copies pass
// (the book's place), and the guard that silences a retired copy's hooks. Last, towards the server (Joost, 20 Oct: no session upload on a dev
// reload): five reloads and a logout put exactly one session on the server, all pieces in it, while every piece stays on this PC. Fictional
// ids and names; every file in a fresh temp folder.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hearthwoven;
using Hearthwoven.Panel;

static class LiveReloadTests
{
    const long Id = 3141592653L;
    const string Name = "Rowan";
    static readonly DateTime Evening = new DateTime(2026, 10, 20, 19, 0, 0);   // local

    static SessionEvents Ev(float chops) { var e = new SessionEvents(); SessionEvents.Add(e.ChopHits, "Beech1", chops); return e; }
    static DamageTally Dmg(float slash) { var d = new DamageTally(); d.Dealt["Troll|Swords|slash"] = slash; d.HitsDealt = (int)(slash / 10); return d; }
    static ArmourTally Arm(int hits) => new ArmourTally { Hits = hits };
    static FoeCounts Foes(int fought) { var f = new FoeCounts(); f.Kinds["$enemy_troll"] = new FoeCounts.Count { Fought = fought, Defeated = fought }; return f; }
    static float Chops(SessionEvents e) => e != null && e.ChopHits.TryGetValue("Beech1", out var v) ? v : 0;
    static float Slash(DamageTally d) => d != null && d.Dealt.TryGetValue("Troll|Swords|slash", out var v) ? v : 0;

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var dir = Path.Combine(Path.GetTempPath(), "hw-live-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var path = LocalTotals.PathFor(dir, Id, Name);
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        // ---------- an evening: six parts of play, a reload after each of the first five ----------
        var chops = new float[] { 5, 7, 0, 3, 11, 2 };
        var slash = new float[] { 100, 40, 0, 250, 10, 60 };
        var hits = new[] { 4, 0, 1, 9, 2, 3 };
        var fought = new[] { 1, 2, 0, 3, 1, 1 };
        var start = new LocalTotals { PlayerId = Id, Name = Name };
        start.Combos.FromUtc = Evening.ToUniversalTime().AddDays(-3);
        start.Feats.MigrateSeen(_ => false);   // as Plugin.LoadLocal does on the first load with 0.9: the seen tiers recorded from then
        start.Save(path, Evening.ToUniversalTime());

        var t = LocalTotals.Load(path, Id, out var problem);
        string session = "s0";
        bool beforeExact = problem == null, pendingClean = true;
        for (int i = 0; i < chops.Length; i++)
        {
            var at = Evening.AddMinutes(10 * i + 9);
            // this copy's stores start at zero (a new copy, or the connection's first); the records write into the loaded totals at once
            SessionEvents ev = Ev(chops[i]); DamageTally dmg = Dmg(slash[i]); ArmourTally arm = Arm(hits[i]); FoeCounts foes = Foes(fought[i]);
            t.Feats.Add("fishCaught", 1);
            t.Combos.Add(new[] { "Bread", "HoneyGlazedChicken" }, 30, 40, 0, at.Date);
            if (i == 2) { t.Feats.Earn("angler", 1, new FeatMoment { Utc = at.ToUniversalTime() }); t.Feats.MarkTierSeen("angler", 1, _ => false); }   // earned and looked at
            if (i == 3) t.Feats.Earn("angler", 2, new FeatMoment { Utc = at.ToUniversalTime() });   // earned, not looked at yet
            // Today's live part (PendingDay) holds this part only: the earlier parts are in the history already
            var live = t.Pending(session, dmg, ev, new BiomeTally(), null, at);
            if (Chops(live.Events) != chops[i] || Slash(live.Damage) != slash[i]) pendingClean = false;
            // the old copy's Leave: SaveLocal as at a logout
            t.Record(session, dmg, ev, null, null, at);
            t.Armour.Record(session, arm, at);
            t.Foes.Record(session, foes, at);
            t.Save(path, at.ToUniversalTime());
            // the new copy's JoinRunning: the file read back, a new session id
            t = LocalTotals.Load(path, Id, out problem);
            session = "s" + (i + 1);
            float wantChops = chops.Take(i + 1).Sum(), wantSlash = slash.Take(i + 1).Sum();
            if (problem != null || Chops(t.EventsBefore(session)) != wantChops || Slash(t.DamageBefore(session)) != wantSlash ||
                t.Armour.BeforeSession(session).Hits != hits.Take(i + 1).Sum() || t.Foes.BeforeSession(session).Total(c => c.Fought) != fought.Take(i + 1).Sum())
                beforeExact = false;
        }
        float allChops = chops.Sum(), allSlash = slash.Sum();
        var today = t.History.Rows.FirstOrDefault(r => r.Period == DayHistory.DayKey(Evening.Date));
        Check(beforeExact, "LR after each of the five reloads the new copy reads back every part saved so far, once: since-install chops, damage, armour hits and foes fought");
        Check(Chops(t.EventsBefore("next")) == allChops && Slash(t.DamageBefore("next")) == allSlash && t.Armour.BeforeSession("next").Hits == hits.Sum() &&
              t.Foes.BeforeSession("next").Total(c => c.Fought) == fought.Sum() && t.Sessions == chops.Length - 1 && t.LastSession == "s5",
              "LR the evening's end: all six parts once (" + Chops(t.EventsBefore("next")) + " of " + allChops + " chops, " + Slash(t.DamageBefore("next")) + " of " + allSlash + " damage), six sessions in the file");
        Check(today != null && Chops(today.Events) == allChops && Slash(today.Damage) == allSlash && t.History.Rows.Count == 1,
              "LR Today's row holds the six parts once (" + (today == null ? 0 : Chops(today.Events)) + " chops), nothing doubled by a reload");
        Check(pendingClean, "LR Today's live part (PendingDay) after a reload holds only what the new copy counted");
        Check(t.Feats.Count("fishCaught") == chops.Length && t.Feats.Tier("angler") == 2 && t.Feats.IsSeen("angler", 1) && !t.Feats.IsSeen("angler", 2) &&
              t.Combos.Total == chops.Length && t.Combos.Combos.Count == 1,
              "LR the records carry on across the reloads: feat counts (" + t.Feats.Count("fishCaught") + "), the tiers earned with their seen state (one seen, one new), the meal combinations (" + t.Combos.Total + ")");

        // ---------- the control: carrying the old session id with stores that start empty loses the session ----------
        var carried = LocalTotals.Load(path, Id, out _);
        carried.Record("s5", Dmg(5), Ev(1), null, null, Evening.AddHours(1));   // what an empty copy counted in its first seconds, under the old id
        Check(Chops(carried.EventsBefore("next")) == allChops - chops[5] + 1 && Slash(carried.DamageBefore("next")) == allSlash - slash[5] + 5,
              "LR control: the same session id carried on with empty stores replaces the session's " + chops[5] + " chops by 1 (why a reload takes a new id)");

        // ---------- the short windows: the minutes before a reload come back once ----------
        const string world = "Midgard-271828";
        var reload = new DateTime(2026, 10, 20, 18, 40, 30, DateTimeKind.Utc);
        var before = new EventLog();
        before.AddDamage(reload.AddMinutes(-30), "Plains", true, "Fuling", "Swords", new HitData.DamageTypes { m_slash = 80 });
        before.AddDamage(reload.AddMinutes(-3), "Plains", true, "Fuling", "Swords", new HitData.DamageTypes { m_slash = 20 });
        var keptPath = KeptMinutes.PathFor(path);
        var take = KeptMinutes.Take(reload, before, null); take.World = world; take.PlayerId = Id.ToString(); take.Name = Name; take.Save(keptPath, reload);
        var kept = KeptMinutes.Load(keptPath, world, Id, Name, reload.AddSeconds(2), reload, out var keptProblem);
        var after = new EventLog(); after.AddDamage(reload.AddMinutes(1), "Plains", true, "Fuling", "Swords", new HitData.DamageTypes { m_slash = 15 });
        var joined = EventLog.Join(kept?.Log, after);
        float Dealt(EventLog l) => l == null ? 0 : l.Damage.Where(kv => kv.Key.Contains("|dealt|")).Sum(kv => kv.Value);
        Check(keptProblem == null && Dealt(kept?.Log) == 100 && Dealt(joined) == 115 && Dealt(after) == 15,
              "LR the short windows: the 100 dealt before the reload come back as kept minutes, joined with the new copy's 15 to 115; This session holds the 15");

        // ---------- the note: the book's place, through the JSON the copies pass ----------
        var s = new PanelState { Chapter = Chapter.Feats, Window = TimeWindow.LastHour, WindowPicked = true, Player = "Tor" };
        s.Page[Chapter.Feats] = "earned"; s.Page[Chapter.Deeds] = "meals"; s.View["Deeds/mealplan/meal"] = "Bread|Sausages"; s.FeatSel = "angler"; s.FeatSelAt = "earned";
        var note = new LiveReload.Note { Session = "abc", LastSend = 812.5f };
        note.Keep(s, true);
        var back = LiveReload.Note.FromJson(note.ToJson());
        var s2 = new PanelState(); back?.Into(s2); var openedWith = s2.FeatSel; back?.AfterOpen(s2);
        Check(back != null && back.Session == "abc" && back.LastSend == 812.5f && back.BookOpen && s2.Chapter == Chapter.Feats && s2.Window == TimeWindow.LastHour && s2.WindowPicked &&
              s2.Player == "Tor" && s2.Page[Chapter.Feats] == "earned" && s2.Page[Chapter.Deeds] == "meals" && s2.View["Deeds/mealplan/meal"] == "Bread|Sausages" &&
              openedWith == "" && s2.FeatSel == "angler" && s2.FeatSelAt == "earned",
              "LR note: the open book's chapter, pages, window, whose book, every view (the Meal plan's meal) and, once it opened, the feat card reach the new copy");
        // the scroll offsets (0.9.11, Joost: the reloaded book went back to the top): the column, the list and the feats grid by its path, back as they were,
        // then clamped to the room the new build's page has
        var scrolled = new LiveReload.Note(); scrolled.Scrolls[LiveReload.Note.PageScroll] = 412.5f; scrolled.Scrolls[LiveReload.Note.ListScroll] = 64f; scrolled.Scrolls["Island/Feats/View"] = 230.25f;
        var sb = LiveReload.Note.FromJson(scrolled.ToJson());
        Check(sb != null && sb.Scrolls.Count == 3 && sb.Scrolls[LiveReload.Note.PageScroll] == 412.5f && sb.Scrolls[LiveReload.Note.ListScroll] == 64f && Math.Abs(sb.Scrolls["Island/Feats/View"] - 230.25f) <= 0.051f &&
              LiveReload.Note.FromJson(new LiveReload.Note().ToJson()).Scrolls.Count == 0 && LiveReload.Note.FromJson("{\"scrolls\":{\"~page\":\"x\",\"~list\":5}}").Scrolls.Count == 1,
              "LR note: the scroll offsets of the column, the list and the feats grid come back (to a tenth of a pixel); none, or one that is not a number: none");
        Check(LiveReload.Note.ClampScroll(412.5f, 900f) == 412.5f && LiveReload.Note.ClampScroll(412.5f, 300f) == 300f && LiveReload.Note.ClampScroll(-8f, 300f) == 0f &&
              LiveReload.Note.ClampScroll(50f, 0f) == 0f && LiveReload.Note.ClampScroll(float.NaN, 300f) == 0f,
              "LR note: an offset is clamped to the new page's room: as far as it was, or the bottom when the page got shorter, the top when it no longer scrolls");
        var odd = LiveReload.Note.FromJson("{\"chapter\":\"Gone\",\"window\":\"Fortnight\",\"pages\":{\"Gone\":\"x\",\"Battle\":\"foes\"}}");
        var s3 = new PanelState(); odd?.Into(s3);
        Check(odd != null && s3.Chapter == Chapter.Deeds && s3.Window == TimeWindow.Session && s3.Page.Count == 1 && s3.Page[Chapter.Battle] == "foes" &&
              LiveReload.Note.FromJson(null) == null && LiveReload.Note.FromJson("not json") == null,
              "LR note: a chapter or window this build no longer has is left as it was; no note or a broken one: none");

        // ---------- a retired copy runs no hook (HookGuard.Run), and players never see any of it ----------
        var retired = typeof(LiveReload).GetField("Retired", BindingFlags.NonPublic | BindingFlags.Static);
        bool ran = false, ranAfter = false, valueAfter = true;
        HookGuard.Run(() => ran = true);
        retired.SetValue(null, true);
        try { HookGuard.Run(() => ranAfter = true); valueAfter = HookGuard.Run(() => true, false); }
        finally { retired.SetValue(null, false); }
        Check(ran && !ranAfter && !valueAfter, "LR a copy that handed over runs no hook of its own (a prefix gets its fallback), the next one does");
        Check(!(bool)typeof(LiveReload).GetProperty("On", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null),
              "LR off unless Dev.LiveReload is set: without the config entry (a player, a test) nothing of it runs");

        fails += OneUpload(dir);
        try { Directory.Delete(dir, true); } catch { }
        return fails;
    }

    // ---------- towards the server: five reloads, then a logout: one session goes up, with every piece in it ----------

    const string World = "Midgard-271828";
    static string Hex(int n) => (0x5b1d00000000L + n * 7919L).ToString("x12");

    /// <summary>One piece of the evening: a copy's recorder (continuing the numbers of the copy before it, as Plugin.JoinRunning does), two fights of
    /// two foes, kept as Plugin.SaveKeptFights keeps it.</summary>
    static KeptSession Piece(BattleRecorder rec, string id, string of, DateTime from)
    {
        var t = from.AddMinutes(1);
        for (int f = 0; f < 2; f++)
        {
            for (int k = 0; k < 2; k++)
            {
                var foe = new FoeId(-77, (uint)(from.Ticks / TimeSpan.TicksPerSecond % 100000) * 10 + (uint)(f * 2 + k));
                rec.Dealt(t, foe, "Greydwarf", "$enemy_greydwarf", "BlackForest", new HitData.DamageTypes { m_slash = 30 });
                rec.Killed(t.AddSeconds(1), "$enemy_greydwarf", 1); rec.Destroyed(t.AddSeconds(1.5), foe);
                t = t.AddSeconds(5);
            }
            t = t.AddMinutes(2);   // past FightGap: the next foes are the next fight
        }
        rec.Tick(t);
        var s = KeptFights.Take(World, id, from, from.AddMinutes(9), rec, Name, _ => true, null);
        s.Of = of;
        return s;
    }

    static DeedSession Deeds(string id, string of, DateTime from)
    {
        var d = new DeedSession { World = World, Id = id, Of = of, FromUtc = from, ToUtc = from.AddMinutes(9) };
        var st = new DeedStretch { Family = DeedStretches.Woodcutting, FromUtc = from.AddMinutes(2), ToUtc = from.AddMinutes(6) };
        st.Counts[DeedLog.PickedUp + "|Wood"] = 12;
        d.Stretches.Add(st);
        return d;
    }

    static int OneUpload(string dir)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var totals = LocalTotals.PathFor(dir, Id, Name);
        string fightsPath = KeptFights.PathFor(totals), deedsPath = KeptDeeds.PathFor(totals);
        var start = new DateTime(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);
        var connection = Hex(0);   // the connection's first session id: what the server's feeds will know it by (Plugin.FeedId)
        var fights = new KeptFights { PlayerId = Id.ToString(), Name = Name }; var deeds = new KeptDeeds { PlayerId = Id.ToString(), Name = Name };
        int[] numbers = null; bool runningStaysHome = true;
        for (int i = 0; i <= 5; i++)
        {
            var id = Hex(i); var of = i == 0 ? "" : connection; var from = start.AddMinutes(10 * i);
            var rec = new BattleRecorder(); rec.ContinueAfter(numbers);
            fights.Record(Piece(rec, id, of, from), from.AddMinutes(9));
            deeds.Record(Deeds(id, of, from), from.AddMinutes(9));
            numbers = rec.Numbers;
            // the old copy's save, then the new copy reads the files back
            fights.Save(fightsPath, from.AddMinutes(9)); deeds.Save(deedsPath, from.AddMinutes(9));
            fights = KeptFights.Load(fightsPath, Id, Name, out _); deeds = KeptDeeds.Load(deedsPath, Id, Name, out _);
            // while the connection runs, the upload tick sees none of its pieces
            var now = from.AddMinutes(9.5);
            var during = Hearthwoven.Uploads.Plan(Hearthwoven.Uploads.JoinPieces(fights.Earlier(World, id, now), connection),
                                                          Hearthwoven.Uploads.JoinPieces(deeds.Earlier(World, id, now), connection), new HashSet<string>(), new HashSet<string>(), now);
            if (during.Count != 0) runningStaysHome = false;
        }
        Check(runningStaysHome, "LR up: while the connection runs (five reloads), none of its pieces is planned for the server");

        // the logout ends the connection; the next one (its own id) uploads the earlier sessions
        var next = Hex(99); var later = start.AddHours(2);
        var earlierFights = fights.Earlier(World, next, later); var earlierDeeds = deeds.Earlier(World, next, later);
        var plan = Hearthwoven.Uploads.Plan(Hearthwoven.Uploads.JoinPieces(earlierFights, next), Hearthwoven.Uploads.JoinPieces(earlierDeeds, next),
                                                    new HashSet<string>(), new HashSet<string>(), later);
        int localFights = earlierFights.Sum(s => s.Fights.Count);
        var packed = plan.Count == 1 ? Hearthwoven.Uploads.Pack(plan[0], out _) : null;
        var block = packed == null ? null : FeedBlock.Read(Transport.Unpack(packed));
        var blockNumbers = block?.Feed?.Fights.Select(f => f.Number).ToList() ?? new List<int>();
        Check(earlierFights.Count == 6 && localFights == 12 && earlierDeeds.Count == 6 && earlierDeeds.Sum(d => d.Stretches.Count) == 6,
              "LR up: nothing lost on this PC: six pieces kept with their 12 fights and 6 deed stretches, read back through the files");
        Check(plan.Count == 1 && plan[0].Id == connection && block != null && block.Id == connection && block.Feed.Fights.Count == 12 &&
              blockNumbers.Distinct().Count() == 12 && block.Stretches == 6 && block.FromUtc == start && block.ToUtc == start.AddMinutes(59),
              "LR up: after the logout exactly one session goes up, under the connection's id, with all 12 fights (numbers 1.." + (blockNumbers.Count == 0 ? 0 : blockNumbers.Max()) + ", none twice) and 6 stretches, from the first piece's start to the last one's end");

        // a whole session (no reload) is planned as it always was; a copy that did not continue the numbers keeps the newest of a clash
        var plain = new KeptSession { World = World, Id = Hex(50), FromUtc = start, ToUtc = start.AddMinutes(5) };
        var same = Hearthwoven.Uploads.JoinPieces(new[] { plain }, next);
        var r1 = new BattleRecorder(); var r2 = new BattleRecorder();
        var clash = Hearthwoven.Uploads.JoinPieces(new[] { Piece(r1, Hex(60), "", start), Piece(r2, Hex(61), Hex(60), start.AddMinutes(10)) }, next);
        Check(same.Count == 1 && ReferenceEquals(same[0], plain) && clash.Count == 1 && clash[0].Fights.Count == 2 && clash[0].Fights.All(f => f.StartUtc >= start.AddMinutes(10)),
              "LR up: a session without reloads goes up unchanged; pieces whose numbers clash (no note) keep the newer fights, never two under one number");
        var note = LiveReload.Note.FromJson(new LiveReload.Note { Session = Hex(5), Connection = connection, Numbers = new[] { 12, 24, 1, 0 } }.ToJson());
        Check(note != null && note.Connection == connection && note.Numbers != null && note.Numbers.SequenceEqual(new[] { 12, 24, 1, 0 }),
              "LR up: the note carries the connection and the recorder's numbers to the new copy");
        return fails;
    }
}
