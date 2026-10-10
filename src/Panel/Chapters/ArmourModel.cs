using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Battle > Defence's armour switch (0.7, work/hearthwoven-0.7/ARMOUR-SCOPE.md sections 3-4; review fix: a like-for-like pair): "Received from"
    /// gets a visible view switch, Received (the page as it was, the default: everything that hurt you, falls, drowning, cold, smoke and ticks
    /// too) and Your armour (the armour step alone). Your armour reads the armour ledger (ArmourTally.cs) for BOTH its numbers, before and after
    /// armour, the same hits; never the damage tally, so it is never summed with or set against Received from. Each damage type is one bar on one scale: solid = what reached you, hollow (the type's colour as an outline) =
    /// what your armour stopped, together = what hit your armour. Hollow is not faded: faded means "the game's count from before install".
    /// Yourself only in 0.7 (a fellow shares no armour numbers): on a fellow's book the switch is absent and the page is as before.
    /// </summary>
    public static partial class PanelModel
    {
        // B27 (Joost: "Received / Your armour" was unclear): the chips say what each view shows, in the words of the page ("What hurt you",
        // VOCABULARY.md's allowed heading for damage received after your armour)
        public const string ReceivedView = "What hurt you", ArmourViewLabel = "What your armor stopped";
        public const string ArmourStopped = "stopped by your armor", ArmourByType = "Before and after your armor", ArmourByFoe = "By foe";
        public const string ArmourKeyReached = "reached you", ArmourKeyStopped = "stopped by your armor";
        public const string ArmourPerFoe = "Per foe: choose This session or longer.";

        /// <summary>"About these numbers" for the Your armour view, in every window (page-specific; the About page carries the short row).</summary>
        public static readonly (string title, string text)[] ArmourAbout =
        {
            ("From", "Recorded on this PC from the day this version first ran. Hits before that were never measured, and your character's own counters keep no armor numbers."),
            ("What it is", "Each hit as it reached your armor (after a block, your ward and your resistances) and as it left it. Only the armor step."),
            ("Not here", "Falls, drowning, cold and smoke never meet your armor. A hit your ward or a block stopped completely is not here either."),
            ("Burning and poison", "Counted as the hit sets them. Their ticks can come to less (a weaker poison does not stack), so after armor here can be more than in Received."),
            (ReceivedView, "Everything that hurt you, counted later: falls, drowning, cold and smoke too, after the world's damage setting and with the ticks. This view is only the armor step."),
            ("Gaps", "Sessions played with an older Hearthwoven are missing here: it did not record your armor."),
            ("Complete for you", "Your armor always works on your own PC, so this is complete for your character in every world and group."),
        };

        /// <summary>The armour ledger in a window, yourself only (null: a fellow's book). All: everything recorded on this PC; Session: this
        /// session; a day: the book's day rows plus the running session's unsaved part; 10 min .. 3 h: this session's minute buckets (no source).</summary>
        public static ArmourTally ArmourIn(PanelInput input, TimeWindow w)
        {
            if (input == null || !input.IsSelf) return null;
            if (w == TimeWindow.SinceInstall) return input.ArmourSince ?? input.ArmourSession;
            if (w == TimeWindow.Session) return input.ArmourSession;
            if (IsDayWindow(w))
            {
                var t = input.ArmourBook != null ? input.ArmourBook.Sum(DayFrom(input, w), LocalToday(input)) : new ArmourTally();
                t.AddAll(input.ArmourBook != null ? input.ArmourPending : input.ArmourSession);
                return t;
            }
            return input.ArmourMinutes?.Since(Cutoff(w, input.NowUtc));
        }

        /// <summary>"Recorded from 9 October · this PC" (RecordedFromLine, the same words as every 0.7 label): the armour ledger's own start (not the install); before the totals load, the session's start.</summary>
        public static string ArmourFromLine(PanelInput input)
        {
            var from = input.ArmourFromUtc ?? input.SessionStartUtc ?? input.NowUtc;
            return RecordedFromLine(input, from);
        }

        static string TypeOf(string key) => key.Substring(key.LastIndexOf('|') + 1);
        static string SourceOf(string key) => key.Substring(0, key.IndexOf('|') < 0 ? 0 : key.IndexOf('|'));

        /// <summary>The Your armour view's blocks for a window (null on a fellow's book): the headline, the bar per damage type, the bar per foe
        /// (when the window has sources); its About these numbers box says how it relates to Received (ArmourNumbers). A window without hits on your armour says so, with the recorded-from date.</summary>
        public static List<Block> ArmourView(PanelInput input, TimeWindow w)
        {
            var t = ArmourIn(input, w);
            if (t == null) return null;
            var from = ArmourFromLine(input);
            var before = ArmourTally.Total(t.Before); var after = Math.Min(before, ArmourTally.Total(t.After));
            if (before <= 0) return new List<Block> { ArmourEmpty(input, w) };
            var stopped = before - after;
            var blocks = new List<Block>
            {
                new Block { Kind = "stat", Value = N(stopped), Title = ArmourStopped, Text = "of " + N(before) + " that reached your armor · " + N(Math.Round(100 * stopped / before)) + "%", Src = SrcPc, Source = TagMeasured },
            };
            // per damage type, on one scale
            var byType = BattleTypes.Select(ty => (ty, b: t.Before.Where(kv => TypeOf(kv.Key) == ty).Sum(kv => (double)kv.Value), a: t.After.Where(kv => TypeOf(kv.Key) == ty).Sum(kv => (double)kv.Value)))
                                    .Where(x => x.b > 0).OrderByDescending(x => x.b).ToList();
            var max = byType.Max(x => x.b);
            blocks.Add(new Block
            {
                Kind = "armour", Title = ArmourByType, Text = from, Tone = "key", Src = SrcPc, Source = TagMeasured,
                Items = byType.Select(x => ArmourRow(x.ty, DamageIcon(x.ty), TypeName(x.ty), DamageColour(x.ty), x.b, Math.Min(x.b, x.a), max,
                                                      new List<Block> { new Block { Kind = "part", Id = x.ty, Colour = DamageColour(x.ty), Fraction = 1f } })).ToList(),
            });
            // per foe, when the window knows who hit you (the minute buckets keep no source)
            var foes = t.Before.Keys.Select(SourceOf).Where(s => s.Length > 0).Distinct()
                .Select(s => (s, b: t.Before.Where(kv => SourceOf(kv.Key) == s).Sum(kv => (double)kv.Value),
                                 types: t.After.Where(kv => SourceOf(kv.Key) == s).GroupBy(kv => TypeOf(kv.Key)).ToDictionary(g => g.Key, g => g.Sum(kv => (double)kv.Value)),
                                 typesBefore: t.Before.Where(kv => SourceOf(kv.Key) == s).GroupBy(kv => TypeOf(kv.Key)).ToDictionary(g => g.Key, g => g.Sum(kv => (double)kv.Value))))
                .Where(x => x.b > 0).OrderByDescending(x => x.b).ThenBy(x => x.s, StringComparer.Ordinal).Take(RowTop).ToList();
            if (foes.Count > 0)
            {
                var fmax = foes.Max(x => x.b);
                blocks.Add(new Block
                {
                    Kind = "armour", Title = ArmourByFoe, Src = SrcPc, Source = TagMeasured,
                    Items = foes.Select(x =>
                    {
                        var a = Math.Min(x.b, x.types.Values.Sum());
                        // ISC-22: per damage type what your armour stopped of what hit it (Value of Value2), as the hero reads (REVIEW-07 #3: one
                        // meaning for "X of Y" on the page), each list adding up to the row's numbers; a type that got through whole reads "0 of 12"
                        var used = BattleTypes.Where(ty => Get(x.typesBefore, ty) > 0 || Get(x.types, ty) > 0).ToList();
                        var stoppedBy = Shown(used.Select(ty => Math.Max(0, Get(x.typesBefore, ty) - Get(x.types, ty))).ToList());
                        var before = Shown(used.Select(ty => Math.Max(Get(x.typesBefore, ty), Get(x.types, ty))).ToList());
                        var parts = used.Select((ty, k) => new Block
                        {
                            Kind = "part", Id = ty, Icon = DamageIcon(ty), Title = TypeName(ty), Colour = DamageColour(ty), Value = stoppedBy[k], Value2 = before[k],
                            Fraction = a > 0 ? (float)(Get(x.types, ty) / a) : 0,
                        }).ToList();
                        return ArmourRow(x.s, FoeIcon(input, x.s), Who(input, x.s), ArmourOutline, x.b, a, fmax, parts);
                    }).ToList(),
                });
            }
            else if (IsShortWindow(w) && w != TimeWindow.Session) blocks.Add(new Block { Kind = "note", Text = ArmourPerFoe });
            // All: these numbers are the armour ledger's, which starts at its own date (not the install): the label says that date (rule A/C, DateFrom)
            if (w == TimeWindow.SinceInstall) foreach (var b in blocks) DateFrom(b, input.ArmourFromUtc ?? input.SessionStartUtc ?? input.NowUtc);
            return blocks;
        }

        /// <summary>B27: an empty Your armour says why (armour is counted from its own date, 0.7 on: right after installing there is nothing)
        /// and when it fills: "Your armour has stopped nothing in the last 30 minutes" / "Counted from 9 October, when this version of
        /// Hearthwoven first ran. A foe's hit on your armour shows up here."</summary>
        public static Block ArmourEmpty(PanelInput input, TimeWindow w)
        {
            var when = w == TimeWindow.SinceInstall ? " yet" : w == TimeWindow.Today ? " today" : w == TimeWindow.Session ? " this session" : " in the " + WindowLabel(w).ToLowerInvariant();
            var from = input.ArmourFromUtc ?? input.SessionStartUtc ?? input.NowUtc;
            return Empty("Your armor has stopped nothing" + when,
                         "Counted from " + RecordDate(input, from) + (input.ArmourFromUtc.HasValue ? ", when this version of Hearthwoven first ran" : "") + ". A foe's hit on your armor shows up here.");
        }

        /// <summary>The outline of the stopped part on a foe's row (its solid part is stacked by type): the page's quiet ink.</summary>
        public const string ArmourOutline = "#b3aea4";

        static Block ArmourRow(string id, string icon, string title, string colour, double before, double after, double max, List<Block> parts) => new Block
        {
            // the number is what your armour stopped of what hit it ("124 of 295"), as the hero above ("296 stopped by your armour, of 868"):
            // one meaning for "X of Y" on the page (REVIEW-07 #3); the bar still shows both, solid = reached you, hollow = stopped
            Kind = "armourrow", Id = id, Icon = icon, Title = title, Colour = colour, Value = N(before - after), Value2 = N(before),
            Fraction = max > 0 ? (float)(after / max) : 0, Fraction2 = max > 0 ? (float)(before / max) : 0, Src = SrcPc, Source = TagMeasured, Items = parts,
        };

        /// <summary>
        /// Defence's received block becomes a view switch (Battle calls this after BattleDefense): Received = the "Received from" block exactly as
        /// it was, Your armour = ArmourView. Nothing changes when the page has no Received from (nothing received in the window) or on a fellow's book.
        /// </summary>
        static void ArmourSwitch(PanelInput input, PanelView view, PanelState state, string page, TimeWindow w)
        {
            if (input == null || !input.IsSelf || state == null) return;
            List<Block> holder = null; int at = -1;
            void Find(List<Block> list)
            {
                if (list == null || holder != null) return;
                var i = list.FindIndex(b => b.Kind == "sources");
                if (i >= 0) { holder = list; at = i; return; }
                foreach (var b in list) Find(b.Items);
            }
            Find(view.Blocks);
            if (holder == null) return;
            var armour = ArmourView(input, w);
            if (armour == null) return;
            var tmp = new PanelView();
            Switch(tmp, state, page, "view", null, (ReceivedId, ReceivedView, new List<Block> { holder[at] }), (ArmourId, ArmourViewLabel, armour));
            holder.RemoveAt(at); holder.InsertRange(at, tmp.Blocks);
            // Your armour has its own "About these numbers" in every window (G5: the chapter's box, All only, speaks of other numbers; review fix:
            // the lines that say how it relates to Received are in reach wherever the view is)
            if (tmp.Blocks.FirstOrDefault(b => b.Kind == "switch")?.Items?.FirstOrDefault(x => x.Selected)?.Id == ArmourId) ArmourNumbers(input, view);
        }

        /// <summary>The switch's view ids (state.View["Battle/defense/view"]).</summary>
        public const string ReceivedId = "received", ArmourId = "armour";

        /// <summary>The Your armour view's "About these numbers": the ArmourAbout lines, the first one under the ledger's own date ("From 9 October").
        /// Own book only (a fellow's book has no armour view).</summary>
        static void ArmourNumbers(PanelInput input, PanelView view)
        {
            var from = input.ArmourFromUtc ?? input.SessionStartUtc;
            view.AboutNumbers = new Block
            {
                Kind = "aboutnumbers", Id = NumbersTarget, Title = AboutNumbersTitle,
                Items = ArmourAbout.Select((l, k) => new Block { Kind = "aboutline", Title = k == 0 && from.HasValue ? FromLabel(input, from.Value) : l.title, Text = l.text }).ToList(),
            };
        }
    }
}
