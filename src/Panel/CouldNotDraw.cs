using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The page the book shows when a page throws while it is built or drawn (PanelUi.Draw): the chapter tabs, the chapter's list with the
    /// page chosen, the page's name and one plain line, with the player row (the Everyone chip on, so one press shows your own numbers).
    /// The book always opens and the player can go elsewhere. 0.8 play-test (Joost, 2026-10-10): Deeds > Crafting with Everyone threw on every
    /// open; the book shut in the same frame and the log held one line without its stack. Made from the state alone (the input when there is
    /// one): nothing here throws, whatever the state.
    /// </summary>
    public static partial class PanelModel
    {
        public const string CouldNotDrawTitle = "This page could not be drawn",
                            CouldNotDrawText = "Choose another page or chapter: the rest of the book works. What went wrong is in the game's log.",
                            CouldNotDrawEveryone = "Everyone is on: press Everyone to see your own numbers here.";

        public static PanelView CouldNotDraw(PanelInput input, PanelState state)
        {
            state = state ?? new PanelState();
            var v = new PanelView { Active = state.Chapter, ShowAbout = state.ShowAbout, Owner = string.IsNullOrEmpty(input?.PlayerName) ? "You" : input.PlayerName };
            foreach (var c in ChapterRow) v.Chapters.Add(new Choice { Id = c.id.ToString(), Label = c.label, Icon = c.icon, Selected = c.id == state.Chapter && !state.ShowAbout });
            v.ListTitle = state.ShowAbout ? "About" : ChapterRow.FirstOrDefault(c => c.id == state.Chapter).label ?? state.Chapter.ToString();
            try
            {
                if (state.ShowAbout) v.List.AddRange(AboutList.Select(x => new Choice { Id = x.id, Label = x.label }));
                else v.List.AddRange(ListOf(input ?? new PanelInput(), state.Chapter));
            }
            catch { v.List.Clear(); }   // a list that cannot be read (Skills from a broken input): the tabs still lead elsewhere
            var page = state.ShowAbout ? state.AboutPage : state.PageOf(state.Chapter);
            if (v.List.Count > 0 && !v.List.Any(l => l.Id == page)) page = v.List[0].Id;
            v.Page = page;
            foreach (var l in v.List) l.Selected = l.Id == page;
            v.Heading = v.List.FirstOrDefault(l => l.Selected)?.Label ?? v.ListTitle;
            v.EveryoneOn = state.Everyone && !state.ShowAbout && string.IsNullOrEmpty(state.Player);   // the chip stays on, so pressing it turns the group's view off
            v.Blocks.Add(new Block { Kind = "empty", Title = CouldNotDrawTitle, Text = CouldNotDrawText + (v.EveryoneOn ? "\n" + CouldNotDrawEveryone : "") });
            v.Keys.Add("[Q/E·A/D] Chapter");
            if (v.List.Count > 1) v.Keys.Add("[W/S] Page");
            if (!state.ShowAbout) v.Keys.Add("[Backspace] Back");
            var closeKeys = new List<string>();
            if (!string.IsNullOrEmpty(state.Hotkey)) closeKeys.Add(state.Hotkey);
            if (state.FilterKey != "Tab") closeKeys.Add("Tab");
            closeKeys.Add("Esc");
            v.Keys.Add("[" + string.Join("/", closeKeys) + "] Close");
            return v;
        }

        /// <summary>The book closed on an error outside a page's drawing (its keys, a click, the frame): the next opening starts on your own Deeds >
        /// Overview with Everyone off and no filter focus, so the same state cannot shut the book again on every opening (the 0.8 play-test's latch).</summary>
        public static void ToSafePage(PanelState s)
        {
            if (s == null) return;
            s.ShowAbout = false; s.Player = ""; s.Everyone = false; s.FilterRow = -1;
            s.Chapter = Chapter.Deeds; s.Page[Chapter.Deeds] = "overview";
        }
    }
}
