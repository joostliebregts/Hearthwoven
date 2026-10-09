// The in-game self-check's bookkeeping (src/CheckBook.cs, Dev.SelfCheck) and the filter key: Tab by default, the keycap and
// footer follow it, and the config scan that names another mod's setting bound to the same key.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class SelfCheckTests
{
    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- the lines ----------
        var lines = new List<string>();
        var book = new CheckBook(lines.Add);
        Check(CheckBook.Line("PASS", "cargo-ship", "x") == "HW-CHECK PASS cargo-ship: x", "self-check: one greppable line, HW-CHECK <STATUS> <area>: text");
        Check(book.Once("PASS", "led", "Wolf") && !book.Once("PASS", "led", "Wolf again") && lines.Count == 1, "self-check: a runtime check logs once per area and status, never every sample");
        Check(book.Once("INFO", "led", "Boar elsewhere", "Boar") && book.Once("INFO", "led", "Lox elsewhere", "Lox") && !book.Once("INFO", "led", "Boar again", "Boar"), "self-check: a key keeps one line per creature");
        book.Say("FAIL", "cargo-ship", "inventory not loaded"); book.Say("PASS", "cargo-ship", "120 items");
        Check(book.StatusOf("cargo-ship") == "PASS" && book.StatusOf("born") == "WAIT" && book.StatusOf("led") == "PASS", "self-check: PASS when ever seen working, WAIT when never seen");
        var summary = book.Summary();
        var ship = summary[Array.FindIndex(CheckBook.Expected, e => e.Key == "cargo-ship")];
        var born = summary[Array.FindIndex(CheckBook.Expected, e => e.Key == "born")];
        Check(summary.Count == CheckBook.Expected.Length && ship.Key == "PASS" && ship.Value.Contains("120 items") && ship.Value.Contains("also a FAIL earlier") &&
              born.Key == "WAIT" && born.Value.StartsWith("not seen yet: "), "self-check: the summary names every runtime check, with what to do for the ones not seen");
        book.ResetCounts(); book.Say("WARN", "keys", "x"); book.Say("PASS", "icons", "y");
        Check(book.Totals() == "fail=0 warn=1 pass=1 info=0 wait=0", "self-check: totals count this report's lines: " + book.Totals());
        Check(CheckBook.Expected.Select(e => e.Key).Distinct().Count() == CheckBook.Expected.Length, "self-check: every expected area once");

        // ---------- the filter key: Tab, and a clash in another mod's config ----------
        Check(new PanelState().FilterKey == "Tab", "filter key: Tab by default (G is ZenDragon's radial menu)");
        var cfg = new[] { "## Settings file", "[General]", "", "## Opens the radial", "# Setting type: KeyCode", "# Default value: G", "Keyboard Radial Menu = G",
                          "[Other]", "Toggle = LeftShift + G", "Name = Gandalf", "Inventory = Tab", "# Tab = G" };
        var g = CheckBook.KeyClashes("ZenDragon.Zen.ModLib.cfg", cfg, "G");
        Check(g.Count == 2 && g[0] == "ZenDragon.Zen.ModLib.cfg [General] Keyboard Radial Menu = G" && g[1].EndsWith("(with a modifier)") && g[1].Contains("[Other] Toggle"),
              "filter key: the scan finds a KeyCode and a shortcut bound to G, never a word or a comment: " + string.Join(" | ", g));
        Check(CheckBook.KeyClashes("a.cfg", cfg, "Tab").Count == 1 && CheckBook.KeyClashes("a.cfg", cfg, "K").Count == 0 && CheckBook.KeyClashes("a.cfg", null, "G").Count == 0,
              "filter key: Tab found once, K not at all, no file no clash");
        return fails;
    }
}
