using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Hearthwoven.Panel;

namespace Hearthwoven
{
    /// <summary>
    /// 0.10 Dev.LiveReload (off for players): the book iterated in the running game with ScriptEngine, which loads each new build from
    /// BepInEx/scripts and destroys the old copy at the end of that frame (tools/valheim-mods/LIVE-MODDING.md, README "Live reload").
    /// With it off nothing here runs and the mod behaves as before.
    /// On this PC a reload ends the old copy's session the way a new connection does (INTEGRITY P3), so nothing is lost or counted twice:
    /// - the old copy (Plugin.Leave) saves every store as at a logout (SaveLocal: the totals with the feats, meals, armor worn, ledgers and
    ///   day history; the kept minutes, fights and deed stretches; the fellow marks; the book's choices), closes the book and destroys
    ///   everything it drew, takes its routed RPCs out of the game's table, unpatches its own hooks and leaves a note in the process (the
    ///   AppDomain: each copy has statics of its own, the domain is shared);
    /// - the new copy patches nothing until that note is there (two copies' hooks would count one hit twice), then reads it all back from
    ///   the files under a new session id, the minutes before the reload as kept ones (Plugin.LoadKept), and opens the book where it was.
    /// "This session" starts again at the reload, as after a reconnect; the windows, Today, the days, All and the records carry on.
    /// Towards the server the connection stays one (Joost, 20 Oct: no session upload on a dev reload): no last copy is sent, the new copy
    /// carries the connection's first id (Plugin.FeedId) and continues the fight numbers, and the pieces go up as one session once the
    /// connection is over (Uploads.JoinPieces): the server keeps a player's newest 60 sessions, which reloads must not use up.
    /// (Carrying the same session id on would not be exact: the battle record, the log's land fold and the deed minutes cannot be read back
    /// whole, and a save of a smaller session under the same id replaces the larger one: LiveReloadTests shows it.)
    /// </summary>
    public static class LiveReload
    {
        internal static ConfigEntry<bool> Enabled;
        internal static bool On { get { try { return Enabled != null && Enabled.Value; } catch { return false; } } }
        /// <summary>This copy has handed over: a hook or RPC handler of it the game still calls does nothing (HookGuard.Run, Guard).</summary>
        internal static volatile bool Retired;
        /// <summary>This copy may run its hooks: no older copy was running, or its note came (or MaxWait passed).</summary>
        internal static bool Settled { get; private set; }

        const string AliveSlot = "Hearthwoven.LiveReload.alive", NoteSlot = "Hearthwoven.LiveReload.note";
        /// <summary>How long a new copy waits for the old one's note before it starts without (the old one did not hand over).</summary>
        internal const float MaxWait = 10f;
        static readonly string token = Guid.NewGuid().ToString("N");
        static float waitSince = -1f;
        internal static bool Waiting => waitSince >= 0f;

        internal static void Bind(ConfigFile config) =>
            Enabled = config.Bind("Dev", "LiveReload", false, "Developer tool: Hearthwoven may be reloaded in the running game by BepInEx ScriptEngine (a new build in BepInEx/scripts). On: a reload saves everything first, hands over to the new copy and opens the book where it was; 'This session' starts again at each reload, as after a reconnect. Set it before the first reload. Off for players.");

        /// <summary>Its own Harmony id per copy: with one fixed id the old copy's unpatch would take the new copy's hooks too.</summary>
        internal static string HarmonyId(string guid) => On ? guid + "." + token : guid;

        /// <summary>Plugin.Awake, before the hooks: true when an older copy still runs (a ScriptEngine reload); the hooks wait for its note then.</summary>
        internal static bool Arrive(float now)
        {
            if (!On) { Settled = true; return false; }
            var older = AppDomain.CurrentDomain.GetData(AliveSlot) as string;
            AppDomain.CurrentDomain.SetData(AliveSlot, token);
            if (string.IsNullOrEmpty(older)) { Settled = true; return false; }
            waitSince = now;
            return true;
        }

        /// <summary>Plugin.Update while waiting: true once this copy may start, with the old copy's note (null: none came within MaxWait).</summary>
        internal static bool Settle(float now, out Note note)
        {
            note = null;
            if (AppDomain.CurrentDomain.GetData(NoteSlot) == null && now - waitSince < MaxWait) return false;
            note = TakeNote();
            waitSince = -1f; Settled = true;
            return true;
        }

        /// <summary>The note in the process, taken (null: none). Also for a copy that found no older one running: had the old copy gone before
        /// this one woke, its note is there already.</summary>
        internal static Note TakeNote()
        {
            var text = AppDomain.CurrentDomain.GetData(NoteSlot) as string;
            AppDomain.CurrentDomain.SetData(NoteSlot, null);
            return Note.FromJson(text);
        }

        /// <summary>The old copy, last: retired, the note for the new one, and its alive mark gone (unless a newer copy already set its own).
        /// A copy that never settled leaves the note slot alone: the note it was waiting for is the next copy's.</summary>
        internal static void HandOver(Note note)
        {
            Retired = true;
            if (note != null) AppDomain.CurrentDomain.SetData(NoteSlot, note.ToJson());
            if (AppDomain.CurrentDomain.GetData(AliveSlot) as string == token) AppDomain.CurrentDomain.SetData(AliveSlot, null);
        }

        /// <summary>A routed RPC handler that does nothing once this copy retired (for one TakeBack could not remove); off: the handler itself.</summary>
        internal static Action<long, T> Guard<T>(Action<long, T> handler) => !On ? handler : (sender, value) => { if (!Retired) handler(sender, value); };

        /// <summary>The old copy: its routed RPCs out of the game's table (ZRoutedRpc.m_functions), so the new copy can register its own
        /// (Register throws on a name the table has). How many went; -1: the table is not where this game keeps it (they stay, retired).</summary>
        internal static int TakeBack(ZRoutedRpc rpc, IEnumerable<string> names)
        {
            if (rpc == null) return 0;
            if (!(AccessTools.Field(typeof(ZRoutedRpc), "m_functions")?.GetValue(rpc) is System.Collections.IDictionary table)) return -1;
            int n = 0;
            foreach (var name in names) { var hash = name.GetStableHashCode(); if (table.Contains(hash)) { table.Remove(hash); n++; } }
            return n;
        }

        /// <summary>What the old copy hands the new one: its session (for the log), when its last copy went out (the new one's first keeps the
        /// server's 20 s gap, Plugin.FirstSendAt) and where the book was, with every view chosen while it lived (the Meal plan's meal among them;
        /// the saved choices, PanelPrefs, keep only a few), the feat card chosen and how far each scroll area was scrolled (Joost in game, 0.9.11:
        /// "I scrolled a little bit down and then when you reload it, it went to the top of the page"). JSON in a string: the copies share no types
        /// of their own.</summary>
        public sealed class Note
        {
            public string Session = "", Connection = "";
            public float LastSend = -100f;
            /// <summary>The old copy's BattleRecorder.Numbers: the new one continues after them.</summary>
            public int[] Numbers;
            public bool BookOpen, WindowPicked;
            public string Chapter = "", Window = "", Player = "", FeatSel = "", FeatSelAt = "";
            public readonly Dictionary<string, string> Pages = new Dictionary<string, string>(), Views = new Dictionary<string, string>();
            /// <summary>Each scroll area's offset from its top (px): the page's column (PageScroll), the left list (ListScroll), and an area inside the
            /// page (the feats grid) by its path under the column, as the book keeps it across a redraw (PanelUi.ScrollPath).</summary>
            public readonly Dictionary<string, float> Scrolls = new Dictionary<string, float>();
            public const string PageScroll = "~page", ListScroll = "~list";

            /// <summary>An offset given back to content that may be shorter now (a new build drew it): within 0 and its room (PanelModel.ScrollRoom).</summary>
            public static float ClampScroll(float y, float room) => room <= 0f || !(y > 0f) ? 0f : Math.Min(y, room);

            public string ToJson()
            {
                var j = new Json().Open().Str("session", Session).Str("connection", Connection).Num("lastSend", LastSend).Num("open", BookOpen ? 1 : 0)
                    .Str("chapter", Chapter).Str("window", Window).Num("picked", WindowPicked ? 1 : 0).Str("player", Player).Str("featSel", FeatSel).Str("featSelAt", FeatSelAt);
                j.Key("pages").Open(); foreach (var kv in Pages) j.Str(kv.Key, kv.Value ?? ""); j.Close();
                j.Key("views").Open(); foreach (var kv in Views) j.Str(kv.Key, kv.Value ?? ""); j.Close();
                j.Key("scrolls").Open(); foreach (var kv in Scrolls) j.Num(kv.Key, Math.Round(kv.Value, 1)); j.Close();
                if (Numbers != null) { j.Key("numbers").OpenArr(); foreach (var x in Numbers) j.Val(x.ToString(CultureInfo.InvariantCulture)); j.CloseArr(); }
                return j.Close().ToString();
            }

            /// <summary>null: no note, or not one this version reads.</summary>
            public static Note FromJson(string json)
            {
                if (string.IsNullOrEmpty(json) || !(MiniJson.Parse(json) is Dictionary<string, object> o)) return null;
                var n = new Note
                {
                    Session = MiniJson.Str(o, "session"), Connection = MiniJson.Str(o, "connection"), LastSend = (float)MiniJson.Num(o, "lastSend"), BookOpen = MiniJson.Num(o, "open") == 1,
                    Chapter = MiniJson.Str(o, "chapter"), Window = MiniJson.Str(o, "window"), WindowPicked = MiniJson.Num(o, "picked") == 1, Player = MiniJson.Str(o, "player"),
                    FeatSel = MiniJson.Str(o, "featSel"), FeatSelAt = MiniJson.Str(o, "featSelAt"),
                };
                if (MiniJson.Obj(o, "pages") is Dictionary<string, object> pages) foreach (var kv in pages) if (kv.Value is string p) n.Pages[kv.Key] = p;
                if (MiniJson.Obj(o, "views") is Dictionary<string, object> views) foreach (var kv in views) if (kv.Value is string v) n.Views[kv.Key] = v;
                if (MiniJson.Obj(o, "scrolls") is Dictionary<string, object> scrolls)
                    foreach (var kv in scrolls) if (kv.Value is double y && !double.IsNaN(y) && !double.IsInfinity(y)) n.Scrolls[kv.Key] = (float)y;
                if (o.TryGetValue("numbers", out var nums) && nums is List<object> list)
                    n.Numbers = list.Select(x => int.TryParse(x as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0).ToArray();
                return n;
            }

            /// <summary>The book's place: the chapter, each chapter's page, the time window and whose book (the filters, views and Everyone are
            /// the book's saved choices, PanelPrefs).</summary>
            public void Keep(PanelState s, bool open)
            {
                BookOpen = open; Chapter = s.Chapter.ToString(); Window = s.Window.ToString(); WindowPicked = s.WindowPicked; Player = s.Player ?? "";
                Pages.Clear(); foreach (var kv in s.Page) Pages[kv.Key.ToString()] = kv.Value;
                Views.Clear(); foreach (var kv in s.View) Views[kv.Key] = kv.Value;
                FeatSel = s.FeatSel ?? ""; FeatSelAt = s.FeatSelAt ?? "";
            }

            /// <summary>Back into a new book's state; a name this build no longer has is left as it was.</summary>
            public void Into(PanelState s)
            {
                if (Enum.TryParse<Chapter>(Chapter, out var c)) s.Chapter = c;
                if (Enum.TryParse<TimeWindow>(Window, out var w)) { s.Window = w; s.WindowPicked = WindowPicked; }
                s.Player = Player ?? "";
                foreach (var kv in Pages) if (Enum.TryParse<Chapter>(kv.Key, out var pc)) s.Page[pc] = kv.Value;
                foreach (var kv in Views) s.View[kv.Key] = kv.Value;
            }

            /// <summary>After the new book opened (PanelUi.Open starts with no feat card chosen): the card the old one showed.</summary>
            public void AfterOpen(PanelState s) { s.FeatSel = FeatSel ?? ""; s.FeatSelAt = string.IsNullOrEmpty(FeatSelAt) ? null : FeatSelAt; }
        }
    }
}
