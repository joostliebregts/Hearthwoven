using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// Developer tool for the in-game test, off unless Dev.SelfCheck is true: one "HW-CHECK &lt;STATUS&gt; &lt;area&gt;: ..." line in the
    /// BepInEx log per thing that has never run in the real game (CheckBook keeps the books). Two halves:
    /// - while playing: the measuring points call in here (cargo read at the helm and the cart, the voyage, animals led, births, the
    ///   filter key, the last snapshot at logout) and each logs once what it saw;
    /// - on the self-check key (Dev.SelfCheckKey, F12): a report of the static checks (hooks, icons, fonts, keys, the server's book)
    ///   and the runtime checks seen so far (WAIT: not yet, with what to do), then a walk through every page of the real panel
    ///   (Panel/PanelCheck.cs) and "HW-CHECK DONE".
    /// Only reads and logs. With Dev.SelfCheck off every entry point returns at once: nothing changes for players.
    /// </summary>
    static class DevCheck
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyCode> Key;
        static BepInEx.Logging.ManualLogSource log;
        internal static readonly CheckBook Book = new CheckBook(l => { try { log?.LogInfo(l); } catch { } });
        internal static readonly PerfMeter Perf = new PerfMeter();   // what Hearthwoven costs per frame (PerfMeter.cs), only while On

        internal static bool On { get { try { return Enabled != null && Enabled.Value; } catch { return false; } } }

        internal static void Bind(ConfigFile config, BepInEx.Logging.ManualLogSource logger)
        {
            log = logger;
            Enabled = config.Bind("Dev", "SelfCheck", false, "Developer tool for the in-game test: logs one 'HW-CHECK PASS/FAIL/WARN/INFO/WAIT <area>: ...' line per runtime check (cargo read at the helm and at the cart, the voyage, animals led, births, the filter key, the last snapshot at logout) and writes a full report, with a walk through every page of the panel, on the self-check key. Only reads and logs; nothing in the game or in your stats changes. Off for players.");
            Key = config.Bind("Dev", "SelfCheckKey", KeyCode.F12, "Key that writes the self-check report (only with SelfCheck on). F12: F1 to F11 belong to the console, the group's mods and the panel snapshots; in Steam F12 also takes a screenshot, which does no harm.");
        }

        static void Safe(Action a) { try { a(); } catch (Exception e) { try { log?.LogWarning("self-check: " + e.Message); } catch { } } }
        static string M(double v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        // ---------- cargo at the helm and at the cart (CargoHooks) ----------

        internal static void NotAtHelm(Player me, Ship ship) => Safe(() =>
        {
            if (me != null && ship != null && ship.IsPlayerInBoat(me))
                Book.Once("INFO", "cargo-ship", "aboard " + Utils.GetPrefabName(ship.gameObject) + " but not holding the helm: cargo is counted only for the one at the helm");
        });

        internal static void Ship(string prefab, int boxes, IDictionary<string, int> aboard, double metres, int load) => Safe(() =>
        {
            if (boxes == 0) { Book.Once("FAIL", "cargo-ship", "no Container found on ship " + prefab + " (GetComponentsInChildren<Container>)"); return; }
            if (aboard == null) { Book.Once("FAIL", "cargo-ship", "container found on ship " + prefab + " but its inventory is not loaded on this PC"); return; }
            int items = aboard.Values.Sum();
            if (items == 0) { Book.Once("INFO", "cargo-ship", "container found on ship " + prefab + " (" + boxes + "), empty: put items in to test the count"); return; }
            Book.Once("PASS", "cargo-ship", "container found on ship " + prefab + " with " + items + " items (" + aboard.Count + " kinds, " + load + " ore/metal), moved " + M(metres) +
                " m in this 10 s sample: +" + M(items * metres) + " item-m (cargo this session: " + M(Plugin.Events.CargoMeters.Values.Sum()) + " item-m)");
        });

        internal static void Cart(string prefab, IDictionary<string, int> aboard, double metres) => Safe(() =>
        {
            if (aboard == null) { Book.Once("FAIL", "cargo-cart", "the inventory of cart " + prefab + " is not loaded on this PC"); return; }
            int items = aboard.Values.Sum();
            if (items == 0) { Book.Once("INFO", "cargo-cart", "pulling " + prefab + ", its box is empty: put items in to test the count"); return; }
            Book.Once("PASS", "cargo-cart", "container read on cart " + prefab + ": " + items + " items (" + aboard.Count + " kinds), moved " + M(metres) + " m: +" + M(items * metres) +
                " item-m (cart metres this session: " + M(Plugin.Events.CartMeters.Values.Sum()) + ")");
        });

        static int keelMilestone;
        internal static void Keel(bool active, double loadedMetres, int best, double ledgerBest) => Safe(() =>
        {
            if (!active) { keelMilestone = 0; return; }
            var mark = (int)(loadedMetres / 500);
            if (mark > keelMilestone)
            {
                keelMilestone = mark;
                Book.Say("INFO", "keel", "voyage on: " + M(mark * 500) + " m sailed with ore/metal aboard (best load over 2 km so far: " + best + ")");
            }
            if (best <= 0) return;
            if (ledgerBest < 0) { Book.Once("WARN", "keel", "2 km reached with " + best + " ore/metal aboard, but the feats ledger is not loaded (local totals?)"); return; }
            if (ledgerBest < best) { Book.Once("FAIL", "keel", "best load " + best + " over 2 km, but the ledger keeps " + M(ledgerBest)); return; }
            Book.Once("PASS", "keel", "2 km sailed with " + best + " ore/metal aboard; the ledger keeps cargoBestVoyage = " + M(ledgerBest) + (best >= 100 ? " (Heavy Keel: 100 reached)" : " (Heavy Keel needs 100)"));
            if (best >= 100) Book.Once("PASS", "keel", "Heavy Keel's 100 ore/metal over 2 km reached in one voyage", "100");
        });

        // ---------- animals led (ClientHooks.SampleLed) ----------

        static int ledMilestone;
        internal static void Led(Player me, IList<string> followingKinds, double total, double best, string bestKind) => Safe(() =>
        {
            if (me == null) return;
            var myName = me.GetPlayerName();
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c is Player || !c.IsTamed()) continue;
                var zdo = c.GetComponent<ZNetView>()?.GetZDO();
                var follows = zdo != null ? zdo.GetString(ZDOVars.s_follow, "") : "";
                var kind = Utils.GetPrefabName(c.gameObject);
                if (follows == myName && !c.IsOwner())
                    Book.Once("INFO", "led", kind + " follows you but another PC hosts it (owner " + zdo.GetOwner() + "): not counted on this PC, the owner trap the panel names", kind);
                if (c.IsOwner())
                {
                    var target = c.GetComponent<MonsterAI>()?.GetFollowTarget();
                    var aiMe = target != null && target == me.gameObject;
                    if (aiMe != (follows == myName))
                        Book.Once("WARN", "led", kind + ": the ZDO says it follows '" + follows + "', MonsterAI.GetFollowTarget says " + (target ? target.name : "nobody"), kind + "|mismatch");
                }
            }
            if (followingKinds.Count > 0)
                Book.Once("PASS", "led", "follow target read for " + string.Join(", ", followingKinds.Distinct().ToArray()) + " (this PC hosts it): led " + M(total) + " m so far, best lead " + M(best) + " m" + (bestKind != null ? " (" + bestKind + ")" : ""));
            var mark = (int)(total / 500);
            if (mark > ledMilestone) { ledMilestone = mark; Book.Say("INFO", "led", M(mark * 500) + " m led this session, best single lead " + M(best) + " m (Long Lead needs 2,000)"); }
        });

        // ---------- born in your care (CargoHooks) ----------

        internal static void Hook(string area, string text) => Safe(() => Book.Once("PASS", area, text));

        internal static void Tamed(string prefab, bool tamed, string birth, double metres, bool counted) => Safe(() =>
        {
            if (birth == null) { if (tamed) Book.Once("INFO", "born", "SetTamed on " + prefab + " outside a birth (a wild one tamed): not counted as born, as intended", prefab); return; }
            if (counted) Book.Once("PASS", "born", prefab + " born inside " + birth + ", " + M(metres) + " m from you: counted (born in your care this session: " + M(Plugin.Events.BornInCare.Values.Sum()) + ")", prefab);
            else if (metres > Cargo.CareRange) Book.Once("INFO", "born", prefab + " born inside " + birth + " " + M(metres) + " m away (over " + Cargo.CareRange + " m): not counted, as intended", prefab + "|far");
            else Book.Once("WARN", "born", prefab + " seen inside " + birth + " (tamed " + tamed + ", " + M(metres) + " m) but not counted", prefab + "|not");
        });

        // ---------- the last snapshot at logout or quit (Plugin.SendNow) ----------

        internal static void Final(string status, string reason, string text) => Safe(() => Book.Say(status, "snapshot-final", reason + ": " + text));

        // ---------- the filter key (Panel/PanelCheck.cs) ----------

        static float keyAt = -1f; static string keyPage, keyName; static bool keyInventory, keyRadial;
        internal static void FilterKey(string key, string page) => Safe(() => { keyAt = Time.unscaledTime; keyPage = page; keyName = key; keyInventory = keyRadial = false; });

        /// <summary>Every frame from Plugin.Update: for a second after the filter key, did the inventory or a radial menu open as well?</summary>
        internal static void Tick() => Safe(() =>
        {
            if (keyAt < 0f) return;
            if (InventoryGui.IsVisible()) keyInventory = true;
            if (Hud.InRadial()) keyRadial = true;
            if (Time.unscaledTime - keyAt < 1f) return;
            keyAt = -1f;
            var focus = Panel.PanelUi.Instance != null ? Panel.PanelUi.Instance.FocusNow() : "?";
            if (keyInventory || keyRadial) Book.Say("FAIL", "filter-key", keyName + " on " + keyPage + " also opened " + (keyInventory ? "the inventory" : "Valheim's radial menu"));
            else Book.Once("PASS", "filter-key", keyName + " on " + keyPage + ": filter focus " + focus + "; no inventory, no radial menu", keyPage);
        });

        // ---------- what Hearthwoven costs per frame (PerfMeter.cs) ----------
        // Every caller checks On first: with Dev.SelfCheck off no clock is read and nothing is summed.

        [ThreadStatic] static int hookDepth;   // a hook body that runs another one (a sample calling a DevCheck line) is timed once

        /// <summary>Start of a client hook body (the Safe wrappers in ClientHooks, CargoHooks, FeatsHooks); call only when On.
        /// Returns the start timestamp, or -1 when nested inside another timed body; pass it to HookEnd in a finally.</summary>
        internal static long HookStart() => hookDepth++ > 0 ? -1L : PerfMeter.Now;

        internal static void HookEnd(long start)
        {
            hookDepth--;
            if (start > 0) Perf.AddHook(PerfMeter.Now - start);
        }

        // ---------- sync bytes (0.7, SYNC-DESIGN.md item 4): what went out and came in per minute, full copies vs live updates ----------

        static int fullOut, liveOut, fullIn, liveIn; static long fullOutBytes, liveOutBytes, fullInBytes, liveInBytes;
        static float nextSync;

        /// <summary>One full copy (packed bytes) or live update sent.</summary>
        internal static void SyncOut(bool live, int bytes) { if (live) { liveOut++; liveOutBytes += bytes; } else { fullOut++; fullOutBytes += bytes; } }
        /// <summary>One fellow's full copy (packed bytes, once whole) or live update received.</summary>
        internal static void SyncIn(bool live, int bytes) { if (live) { liveIn++; liveInBytes += bytes; } else { fullIn++; fullInBytes += bytes; } }

        static string KB(long b) => (b / 1024.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " KB";

        /// <summary>Once a minute, when anything moved: one line with the counts and packed bytes of each kind.</summary>
        internal static void SyncTick()
        {
            try
            {
                var now = Time.realtimeSinceStartup;
                if (now < nextSync) return;
                nextSync = now + 60f;
                if (fullOut + liveOut + fullIn + liveIn > 0)
                    Book.Say("INFO", "sync", $"last minute sent {fullOut} full copies ({KB(fullOutBytes)} packed) and {liveOut} live updates ({KB(liveOutBytes)}); " +
                                             $"received {fullIn} fellow copies ({KB(fullInBytes)}) and {liveIn} live updates ({KB(liveInBytes)})" + (GroupShare.ServerLive ? "" : "; this server keeps no live updates"));
                fullOut = liveOut = fullIn = liveIn = 0; fullOutBytes = liveOutBytes = fullInBytes = liveInBytes = 0;
            }
            catch (Exception e) { try { log?.LogWarning("self-check sync: " + e.Message); } catch { } }
        }

        /// <summary>Every frame from Plugin.Update (only when On): closes the frame and, once a minute of real time, logs the window.</summary>
        internal static void PerfTick()
        {
            try
            {
                var ui = Panel.PanelUi.Instance;
                if (!Perf.EndFrame(Time.unscaledDeltaTime, ui != null && ui.IsOpen, AllocClock.Collections())) return;   // 0.8: with the garbage collections
                Book.Say("INFO", "perf", Perf.Line());
                Perf.Reset();
            }
            catch (Exception e) { try { log?.LogWarning("self-check perf: " + e.Message); } catch { } Perf.Reset(); }
        }

        // ---------- the report (the self-check key; Panel/PanelCheck.cs runs the page walk after it) ----------

        static readonly string[] OreIcons = { "CopperOre", "TinOre", "SilverOre", "IronScrap", "Stone" };

        internal static void Report() => Safe(() =>
        {
            Book.ResetCounts();
            log?.LogInfo(CheckBook.Prefix + " START Hearthwoven " + Plugin.Version + " self-check" + (Panel.SampleMode.On ? " (Dev.SampleData on: the pages show the fictional sample)" : ""));
            // hooks
            var hooks = new[] {
                new KeyValuePair<Type, string>(typeof(Procreation), "Procreate"), new KeyValuePair<Type, string>(typeof(EggGrow), "GrowUpdate"),
                new KeyValuePair<Type, string>(typeof(Character), "SetTamed"), new KeyValuePair<Type, string>(typeof(Game), "Shutdown"),
                new KeyValuePair<Type, string>(typeof(Game), "Logout"), new KeyValuePair<Type, string>(typeof(Player), "OnSpawned"),
                new KeyValuePair<Type, string>(typeof(ZNet), "Awake"), new KeyValuePair<Type, string>(typeof(Hud), "Awake"),
                new KeyValuePair<Type, string>(typeof(PlayerController), "TakeInput"), new KeyValuePair<Type, string>(typeof(InventoryGui), "Show"),
                new KeyValuePair<Type, string>(typeof(ZDO), "Deserialize") };
            var missing = new List<string>();
            foreach (var h in hooks)
            {
                bool ok = false;
                try { var m = AccessTools.Method(h.Key, h.Value); var info = m == null ? null : Harmony.GetPatchInfo(m); ok = info != null && info.Owners.Contains(Plugin.Guid); } catch { }
                if (!ok) missing.Add(h.Key.Name + "." + h.Value);
            }
            Book.Say(missing.Count == 0 ? "PASS" : "FAIL", "hooks", Plugin.HooksOk + " hook classes active, " + Plugin.HooksFailed + " skipped; " +
                (missing.Count == 0 ? "births, shutdown, logout, input block and inventory guard all attached" : "not attached: " + string.Join(", ", missing.ToArray())));
            // the game's icons by name
            foreach (var name in OreIcons)
            {
                var s = Panel.PanelLook.Icon("item:" + name);
                if (s) Book.Say("PASS", "icons", name + " found (sprite '" + s.name + "')");
                else Book.Say("FAIL", "icons", name + " not found in ObjectDB (the panel falls back to its own picture)");
            }
            var sprites = Panel.PanelProbe.MissingSprites();
            Book.Say(sprites.Count == 0 ? "PASS" : "FAIL", "sprites", sprites.Count == 0 ? "every vocabulary sprite decodes" : "do not load: " + string.Join(", ", sprites.ToArray()));
            // fonts
            Panel.PanelLook.Resolve();
            Book.Say(Panel.PanelLook.Body ? "PASS" : "FAIL", "fonts", "body '" + (Panel.PanelLook.Body ? Panel.PanelLook.Body.name : "none") + "', titles '" + (Panel.PanelLook.Title ? Panel.PanelLook.Title.name : "none") +
                "', digits '" + (Panel.PanelLook.Plain ? Panel.PanelLook.Plain.name : "none") + "'; text floor " + Panel.PanelLook.MinText + " px");
            // keys: other mods' configs and the game's own bindings
            Panel.PanelUi.KeyReport();
            // the server's book: what reached this PC, and on a hosting PC the book itself
            var own = GroupShare.OwnBook;
            if (own != null) Book.Say("PASS", "server-book", "received from the server: you sent " + M(own.SentTotal) + ", delivered " + M(own.DeliveredTotal) + " item-m, born near you " + M(own.BornTotal) + "; books for " + GroupShare.Books.Count + " fellows");
            else Book.Say("INFO", "server-book", "nothing received for you (a server before 0.6 sends none; a 0.6 server sends it with the group list every 5 minutes once it has cargo or a birth for you; " + GroupShare.Books.Count + " fellows' books)");
            if (ZNet.instance != null && ZNet.instance.IsServer())
            {
                var b = ServerBookHooks.Book;
                var root = Path.Combine(BepInEx.Paths.BepInExRootPath, "Hearthwoven");
                var file = Directory.Exists(root) ? Directory.GetFiles(root, "server-book-*.json").FirstOrDefault() : null;
                Book.Say(b != null && file != null ? "PASS" : "FAIL", "server-book", "this PC hosts the world: book " + (b != null ? "loaded (" + b.Carriers + " ships and carts, " + b.Players + " players)" : "missing") + ", file " + (file != null ? Path.GetFileName(file) : "not written yet (it is saved with the world)"));
            }
            // this session's counts
            var ledger = Plugin.FeatsLedger;
            Book.Say("INFO", "counts", "this session: cargo " + M(Plugin.Events.CargoMeters.Values.Sum()) + " item-m, cart " + M(Plugin.Events.CartMeters.Values.Sum()) + " m, led " + M(Plugin.Events.LedMeters.Values.Sum()) +
                " m, born in your care " + M(Plugin.Events.BornInCare.Values.Sum()) + "; ledger: best voyage load " + (ledger != null ? M(ledger.Count(CargoVoyage.BestKey)) : "?") + ", best lead " + (ledger != null ? M(ledger.Count(LedTracker.BestKey)) : "?") + " m");
            // the frame cost: a full minute logs itself (and shows in the summary below); before that, what the window holds so far
            if (!Book.Seen("perf") && Perf.Frames > 0) log?.LogInfo(CheckBook.Line("INFO", "perf", "window not full yet: " + Perf.Line()));
            // what the runtime checks have seen so far
            var summary = Book.Summary();
            for (int i = 0; i < summary.Count; i++) log?.LogInfo(CheckBook.Line(summary[i].Key, CheckBook.Expected[i].Key, summary[i].Value));
        });

        internal static void Done(string pages) => Safe(() =>
        {
            var waits = Book.Summary().Count(s => s.Key == "WAIT");
            log?.LogInfo(CheckBook.Prefix + " DONE " + Book.Totals() + "; runtime checks still waiting: " + waits + "; " + pages);
        });
    }
}
