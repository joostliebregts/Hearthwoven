using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// The bookkeeping of the in-game self-check (Dev.SelfCheck, DevCheck.cs), pure C# so the tests run it without the game.
    /// Every check is one log line "HW-CHECK &lt;STATUS&gt; &lt;area&gt;: &lt;what was seen&gt;", easy to grep in LogOutput.log.
    /// Statuses: PASS (seen working), FAIL (seen broken), WARN (works, but something to look at), INFO (context, no verdict),
    /// WAIT (not seen yet: the report says what to do in the game to see it). Runtime checks that fire while playing (the
    /// cargo read at the helm, an animal following you, a birth) log once per area and status, never every sample.
    /// </summary>
    public class CheckBook
    {
        public const string Prefix = "HW-CHECK";
        public static readonly string[] Order = { "FAIL", "WARN", "PASS", "INFO", "WAIT" };

        readonly Action<string> sink;
        readonly HashSet<string> said = new HashSet<string>();
        readonly Dictionary<string, Dictionary<string, string>> seen = new Dictionary<string, Dictionary<string, string>>();   // area -> status -> last text
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public CheckBook(Action<string> sink) { this.sink = sink ?? (_ => { }); }

        /// <summary>The runtime checks the report asks after, with what to do in the game to trigger each one.</summary>
        public static readonly KeyValuePair<string, string>[] Expected =
        {
            new KeyValuePair<string, string>("cargo-ship", "hold the helm of a ship with items in its hold and sail"),
            new KeyValuePair<string, string>("keel", "sail 2 km at the helm with 100 or more ore or metal aboard (Heavy Keel)"),
            new KeyValuePair<string, string>("cargo-cart", "pull a cart with items in it"),
            new KeyValuePair<string, string>("led", "let a tamed animal follow you (E on it) and walk a few hundred metres"),
            new KeyValuePair<string, string>("procreate-hook", "stand near tamed animals that can breed (boars, wolves, lox)"),
            new KeyValuePair<string, string>("egg-hook", "stand near a fertilised egg lying by a fire"),
            new KeyValuePair<string, string>("born", "see a tamed young born or hatched within 40 m of you"),
            new KeyValuePair<string, string>("filter-key", "open the panel on Deeds > Crafting (or Building, Battle) and press the filter key"),
            new KeyValuePair<string, string>("snapshot-final", "log out to the main menu (the line comes at logout)"),
            new KeyValuePair<string, string>("perf", "play a minute, open the book (H) and page through it (one 'perf' line a minute: the panel, page refresh and hook cost per frame)"),
        };

        public static string Line(string status, string area, string text) => Prefix + " " + status + " " + area + ": " + text;

        /// <summary>Logs a line and remembers it for the summary.</summary>
        public void Say(string status, string area, string text)
        {
            if (!seen.TryGetValue(area, out var byStatus)) seen[area] = byStatus = new Dictionary<string, string>();
            byStatus[status] = text;
            counts.TryGetValue(status, out var n); counts[status] = n + 1;
            sink(Line(status, area, text));
        }

        /// <summary>Logs a line only the first time for this area and status (or this <paramref name="key"/>); true when it was logged.</summary>
        public bool Once(string status, string area, string text, string key = null)
        {
            if (!said.Add(area + "|" + status + "|" + (key ?? ""))) return false;
            Say(status, area, text);
            return true;
        }

        /// <summary>Was anything logged for this area (with this status, when given)?</summary>
        public bool Seen(string area, string status = null) => seen.TryGetValue(area, out var s) && (status == null || s.ContainsKey(status));

        /// <summary>The verdict of an area: PASS when it was ever seen working, else the worst seen, else WAIT.</summary>
        public string StatusOf(string area)
        {
            if (!seen.TryGetValue(area, out var s) || s.Count == 0) return "WAIT";
            if (s.ContainsKey("PASS")) return "PASS";
            return Order.First(o => s.ContainsKey(o));
        }

        /// <summary>The summary lines for every expected runtime check, in the order of Expected (not logged; the caller says them).</summary>
        public List<KeyValuePair<string, string>> Summary()
        {
            var lines = new List<KeyValuePair<string, string>>();
            foreach (var e in Expected)
            {
                var status = StatusOf(e.Key);
                if (status == "WAIT") { lines.Add(new KeyValuePair<string, string>("WAIT", "not seen yet: " + e.Value)); continue; }
                var s = seen[e.Key];
                var text = "seen: " + s[status];
                if (status == "PASS" && s.ContainsKey("FAIL")) text += " (also a FAIL earlier: " + s["FAIL"] + ")";
                lines.Add(new KeyValuePair<string, string>(status, text));
            }
            return lines;
        }

        /// <summary>
        /// Settings in one BepInEx .cfg file (its <paramref name="lines"/>) bound to <paramref name="key"/> ("Tab", "G"), as
        /// "&lt;file&gt; [section] Name = value". A KeyCode is saved as its name, a KeyboardShortcut as "LeftShift + G": the last part
        /// is the key. Comments and descriptions (#) are skipped. A combination with modifiers is listed too, marked as such.
        /// </summary>
        public static List<string> KeyClashes(string file, IEnumerable<string> lines, string key)
        {
            var found = new List<string>();
            if (lines == null || string.IsNullOrEmpty(key)) return found;
            var section = "";
            foreach (var raw in lines)
            {
                var line = (raw ?? "").Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2); continue; }
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var name = line.Substring(0, eq).Trim(); var value = line.Substring(eq + 1).Trim();
                var parts = value.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                if (parts.Length == 0 || !string.Equals(parts[parts.Length - 1], key, StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(file + " [" + section + "] " + name + " = " + value + (parts.Length > 1 ? " (with a modifier)" : ""));
            }
            return found;
        }

        /// <summary>How many lines of each status were logged since the last ResetCounts.</summary>
        public int Count(string status) => counts.TryGetValue(status, out var n) ? n : 0;
        public void ResetCounts() => counts.Clear();
        public string Totals() => string.Join(" ", Order.Select(o => o.ToLowerInvariant() + "=" + Count(o)).ToArray());
    }
}
