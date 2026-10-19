// 0.8.2 (H; Joost on a friend's Battle > Foes: "Why are there no 10 min and 30 min on my friend's stats? Makes no sense to me."): while a fellow's
// copy is live (their live updates come and the latest is under PanelModel.LiveSeconds old), 10 min to 3 h open on their Battle pages and in
// Everyone's Battle rows, cut from their minute log counted back from now; a copy that is not live keeps them greyed and says why in plain words.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class FellowWindowsTests
{
    public static int Run(DateTime now)
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var world = PanelSample.Full(now);
        var books = FullDump.Books(world).ToDictionary(b => b.who, b => b.book);
        var edda = books["Edda"]; var tor = books["Tor"];
        PanelView Battle(PanelInput book, string page, TimeWindow w, bool everyone = false)
        {
            var s = new PanelState { Chapter = Chapter.Battle, Player = book.IsSelf ? "" : book.PlayerName, Window = w, WindowPicked = true, Everyone = everyone };
            if (page != null) s.Page[Chapter.Battle] = page;
            return PanelModel.Build(book, s);
        }
        double Lead(string v) => PanelModel.ParseCount(v);

        // ---------- a live fellow: 10 min is open and cut from their minute log ----------
        var cut = now.AddMinutes(-10);
        var minutes = edda.Log.Damage.Where(kv => kv.Key.Split('|')[2] == "dealt" && Array.IndexOf(BattleRecorder.Types, kv.Key.Split('|')[5]) >= 0).Select(kv =>
            (t: DateTime.ParseExact(kv.Key.Split('|')[0], "yyyy-MM-dd'T'HH:mm'Z'", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal), v: (double)kv.Value)).ToList();
        var inTen = minutes.Where(m => m.t.AddMinutes(edda.Log.Span) > cut).Sum(m => m.v); var all = minutes.Sum(m => m.v);
        var damage = Battle(edda, "damage", TimeWindow.LastTenMinutes);
        var shown = PanelModel.Content(damage).Where(b => b.Kind == "dmgmix").Distinct().Sum(b => Lead(b.Value));   // By weapon: every weapon's row
        var guard = PanelModel.GuardIn(edda.Log, TimeWindow.LastTenMinutes, now);
        var defenseView = Battle(edda, "defense", TimeWindow.LastTenMinutes);
        var defense = PanelModel.Content(defenseView).FirstOrDefault(b => b.Kind == "guard");
        Check(PanelModel.FellowLive(edda) && damage.ShownWindow == TimeWindow.LastTenMinutes && !damage.Windows.First(c => c.Id == "LastTenMinutes").Disabled &&
              inTen > 0 && inTen < all && Math.Abs(shown - inTen) < 0.5 && Says(damage, "Edda", "live") &&
              guard != null && (guard.Blocks == 0 ? defense == null : defense != null && Lead(defense.Value) + Lead(defense.Value2) == guard.Blocks) &&
              !damage.Windows.Any(c => c.Disabled) &&   // 0.9: her Damage opens her days too (DayTypesPanelTests)
              defenseView.Windows.Where(c => c.Disabled).Select(c => c.Id).SequenceEqual(new[] { "Today", "Yesterday", "ThreeDays", "SevenDays", "ThirtyDays" }) && Says(defenseView, "Edda", "only for damage dealt"),
              "FW live: Edda's copy is live, so her 10 min is open and cut from her minute log counted back from now (" + shown + " of her " + all +
              " dealt this session); Defense's blocks from her minutes too (" + guard?.Blocks + "); Damage greys nothing, Defense only the days: " + defenseView.WindowWhy);

        // Everyone's Battle rows in 10 min: a live fellow in the sum (0 when quiet), one who is not live left out with why
        var group = Battle(world, "damage", TimeWindow.LastTenMinutes, everyone: true);
        var rows = PanelModel.Content(group).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind)?.Items ?? new List<Block>();
        var eddaRow = rows.FirstOrDefault(r => r.Id == "Edda"); var torRow = rows.FirstOrDefault(r => r.Id == "Tor");
        Check(eddaRow?.Kind == PanelModel.GroupMemberKind && Math.Abs(Lead(eddaRow.Value) - inTen) < 0.5 && torRow != null && torRow.Kind != PanelModel.GroupMemberKind && (torRow.Text ?? "").Contains("every few minutes"),
              "FW Everyone: in 10 min Edda (live) counts " + eddaRow?.Value + " from her minute log; Tor, whose copies are not live, is left out: " + torRow?.Text);

        // ---------- a copy that is not live: the short windows stay greyed, and the line says why ----------
        var gone = edda.ShallowCopy(); gone.Trail = null; gone.ReceivedUtc = now.AddMinutes(-5);   // her last update came five minutes ago: she logged out
        var goneView = Battle(gone, "foes", TimeWindow.LastTenMinutes);
        var torView = Battle(tor, "foes", TimeWindow.LastThirtyMinutes);
        var shortIds = new[] { "LastTenMinutes", "LastThirtyMinutes", "LastHour", "LastThreeHours" };
        var goneRows = PanelModel.Content(Battle(world, "damage", TimeWindow.LastTenMinutes, everyone: true)).FirstOrDefault(b => b.Kind == PanelModel.GroupRowsKind);
        Check(!PanelModel.FellowLive(gone) && goneView.ShownWindow == TimeWindow.Session && shortIds.All(id => goneView.Windows.First(c => c.Id == id).Disabled) &&
              Says(goneView, "Edda", "not playing") && Says(goneView, "only for damage dealt") &&
              torView.ShownWindow == TimeWindow.Session && shortIds.All(id => torView.Windows.First(c => c.Id == id).Disabled) && Says(torView, PanelModel.FellowShortWhy(tor)) && Says(torView, "only for damage dealt") &&
              PanelModel.FellowShortWhy(new PanelInput { IsSelf = false, PlayerName = "Ylva", ModVersion = "0.6.5", ServerLive = true }) is string ylvaWhy && ylvaWhy.Contains("Ylva") && ylvaWhy.Contains("0.6.5") && ylvaWhy.Contains("update"),
              "FW not live: Edda's last update five minutes old greys 10 min to 3 h (Foes shows Session): \"" + goneView.WindowWhy + "\"; Tor's copies never live: \"" + torView.WindowWhy + "\"");
        return fails;
    }
}
