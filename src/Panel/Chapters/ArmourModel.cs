using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Battle > Defence's armour switch (0.7, work/hearthwoven-0.7/ARMOUR-SCOPE.md sections 3-4; review fix: a like-for-like pair): "Received from"
    /// gets a visible view switch, Received (the page as it was, the default: everything that hurt you, falls, drowning, cold, smoke and ticks
    /// too) and Your armour (the armour step alone). Your armour reads the armour ledger (ArmourTally.cs) for BOTH its numbers, before and after
    /// armour, the same hits; never the damage tally, so it is never summed with or set against Received from. 0.8.1 (Joost: the damage-row
    /// grammar): a damage row per type, then per foe, on one scale for the page; a type's part is as wide as what reached your armor, filled in its
    /// colour = what got through, the rest an outline in its colour = what your armor stopped (PanelModel.DamageStoppedTone). The outline is not
    /// faded: faded means "the game's count from before install".
    /// Yourself only in 0.7 to 0.8.6 (a fellow shared no armour numbers). 0.9 (armor-share; Joost, 18 Oct: "Yes, add friends' armour view (What hurt
    /// you / What your armor stopped on a friend's Defense page) to 0.9"): a fellow's copy shares their ledger per damage type since it began
    /// ("armorSince") and per minute in their log (EventLog.Armor), so their Defense gets the same switch, drawn by the same drawer, worded as their
    /// PC's record ("What their armor stopped", "Recorded from 6 October · their PC", their own About lines); All is their whole ledger, Session and
    /// 10 min .. 3 h are cut at your session's start as their blocks are (SessionFrom). Types only: their foes stay on their PC, so no By foe. A copy
    /// from before 0.9 shares none: the view says so in one line (FellowArmourOlder), never an empty or zero bar.
    /// </summary>
    public static partial class PanelModel
    {
        // B27 (Joost: "Received / Your armour" was unclear): the chips say what each view shows, in the words of the page ("What hurt you",
        // VOCABULARY.md's allowed heading for damage received after your armour)
        public const string ReceivedView = "What hurt you", ArmourViewLabel = "What your armor stopped";
        public const string ArmourStopped = "stopped by your armor", ArmourByType = "Before and after your armor", ArmourByFoe = "By foe";
        // the key over the type rows (BattleUi.ArmourKey): a whole part, its filled piece, its outlined piece
        public const string ArmourKeyWhole = "reached your armor", ArmourKeyThrough = "got through", ArmourKeyStopped = "stopped by your armor";
        /// <summary>The word after a share ("82 % stopped", the view's one bar) and after a hidden stopped number on hover ("6 stopped").</summary>
        public const string ArmourStoppedWord = "stopped";
        /// <summary>The view's one bar (Kind "armorbar"): the page's quiet ink, as the key's swatches.</summary>
        public const string ArmourTotalColour = "#b9a688";
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

        // 0.9 (armor-share): the same view on a fellow's book, in their words (Received from already says "after their armor" there)
        public const string FellowReceivedView = "What hurt them", FellowArmourViewLabel = "What their armor stopped";
        public const string FellowArmourStopped = "stopped by their armor", FellowArmourByType = "Before and after their armor";
        public const string FellowArmourKeyWhole = "reached their armor", FellowArmourKeyStopped = "stopped by their armor";
        /// <summary>The type block's Tone on a fellow's book: the key under the bar in their words (BattleUi.ArmourKey; "key" on your own).</summary>
        public const string ArmourKeyTone = "key", FellowArmourKeyTone = "keytheir";
        /// <summary>A fellow's copy from before 0.9 shares no armor: the view's one line (never an empty or zero bar).</summary>
        public const string FellowArmourOlder = "Their version doesn't share this yet.";
        /// <summary>A fellow's log reached EventLog.MaxArmorKeys and dropped its oldest armor minutes (a day-long session).</summary>
        public const string FellowArmourClipped = "A very long session kept only its newest armor minutes: the oldest are missing here.";

        /// <summary>0.9 (armor-share): "About these numbers" for the view on a fellow's book: what their PC recorded and shared, never "Complete for you".</summary>
        public static readonly (string title, string text)[] FellowArmourAbout =
        {
            ("From", "Recorded on their PC from the day their Hearthwoven first recorded armor. Hits before that were never measured."),
            ("What it is", "Each hit as it reached their armor (after a block, their ward and their resistances) and as it left it. Only the armor step."),
            ("Not here", "Falls, drowning, cold and smoke never meet armor. A hit their ward or a block stopped completely is not here either."),
            ("Burning and poison", "Counted as the hit sets them. Their ticks can come to less, so after armor here can be more than in What hurt them."),
            (FellowReceivedView, "Everything that hurt them, counted later: falls, drowning, cold and smoke too, after the world's damage setting and with the ticks. This view is only the armor step."),
            ("Windows", "All is everything their PC recorded. This session and 10 min to 3 h count from when you started playing this time, as the rest of their page."),
            ("Shared", "Their PC shares the totals per damage type, not which foe hit them or where: By foe is on their own book."),
            ("Gaps", "Sessions they played with an older Hearthwoven are missing here: it did not record their armor."),
        };

        /// <summary>The armour ledger in a window (null: not known). Yourself: All: everything recorded on this PC; Session: this session; a day: the
        /// book's day rows plus the running session's unsaved part; 10 min .. 3 h: this session's minute buckets (no source). 0.9, a fellow's book: All
        /// their shared ledger (ArmourSince); Session and 10 min .. 3 h their log's armor minutes from your session's start (FellowArmourIn); no days
        /// (their copy shares none); null too for a copy from before 0.9.</summary>
        public static ArmourTally ArmourIn(PanelInput input, TimeWindow w)
        {
            if (input == null) return null;
            if (!input.IsSelf)
            {
                if (input.ArmourSince == null || IsDayWindow(w)) return null;
                return w == TimeWindow.SinceInstall ? input.ArmourSince : FellowArmourIn(input.Log, w, input.NowUtc, SessionFrom(input));
            }
            if (w == TimeWindow.SinceInstall) return input.ArmourSince ?? input.ArmourSession;
            if (w == TimeWindow.Session) return input.ArmourSession;
            if (IsDayWindow(w))
            {
                var t = input.ArmourBook != null ? input.ArmourBook.Sum(DayFrom(input, w), DayTo(input, w)) : new ArmourTally();
                if (HoldsToday(w)) t.AddAll(input.ArmourBook != null ? input.ArmourPending : input.ArmourSession);   // 0.9.1: never in Yesterday
                return t;
            }
            return input.ArmourMinutes?.Since(Cutoff(w, input.NowUtc));
        }

        /// <summary>0.9 (armor-share): a fellow's armor minutes in a window (EventLog.Armor, a minute each), cut as their blocks are (GuardIn): a minute
        /// counts when any part of it lies in the window, Session from <paramref name="sessionFrom"/> (your session's start). A tally keyed "||type".
        /// Null when the log keeps no armor minutes (a copy from before 0.9).</summary>
        public static ArmourTally FellowArmourIn(EventLog log, TimeWindow w, DateTime nowUtc, DateTime? sessionFrom)
        {
            if (log == null || !log.ArmorKept) return null;
            var t = new ArmourTally();
            foreach (var kv in log.Armor)
            {
                var p = LogKeys.Of(kv.Key).P;   // minute|b|type or minute|a|type
                if (p.Length < 3 || !(kv.Value > 0) || !InFilter(p[0], "", w, "", nowUtc, log.Span, sessionFrom)) continue;
                var into = p[1] == EventLog.ArmorReached ? t.Before : p[1] == EventLog.ArmorThrough ? t.After : null;
                if (into == null) continue;
                var key = "||" + p[2]; into.TryGetValue(key, out var o); into[key] = o + kv.Value;
            }
            return t;
        }

        /// <summary>"Recorded from 9 October · this PC" (RecordedFromLine, the same words as every 0.7 label): the armour ledger's own start (not the install); before the totals load, the session's start.
        /// 0.9, a fellow's book: their ledger's start on their PC, "Recorded from 6 October · their PC" ("Recorded on their PC" when their copy names none).</summary>
        public static string ArmourFromLine(PanelInput input)
        {
            if (input != null && !input.IsSelf) return input.ArmourFromUtc.HasValue ? "Recorded from " + RecordDate(input, input.ArmourFromUtc.Value) + " · their PC" : "Recorded on their PC";
            var from = input.ArmourFromUtc ?? input.SessionStartUtc ?? input.NowUtc;
            return RecordedFromLine(input, from);
        }

        static string TypeOf(string key) => key.Substring(key.LastIndexOf('|') + 1);
        static string SourceOf(string key) => key.Substring(0, key.IndexOf('|') < 0 ? 0 : key.IndexOf('|'));

        /// <summary>The Your armour view's blocks for a window (null on a fellow's book): the type block, which opens with the view's one bar (0.8.1,
        /// Joost: the summary as one bar, so By foe shows on the first screen; Kind "armorbar": what reached your armor, filled by what got through,
        /// "82 % stopped" as its label) and has a damage row per damage type; then one row per foe (when the window has sources); the rows on one
        /// scale, each line the share stopped per type; its About these numbers box says how it relates to Received (ArmourNumbers). A window without hits on your armour says so, with the recorded-from date.</summary>
        public static List<Block> ArmourView(PanelInput input, TimeWindow w)
        {
            var t = ArmourIn(input, w);
            if (t == null) return null;
            var from = ArmourFromLine(input);
            var before = ArmourTally.Total(t.Before); var after = Math.Min(before, ArmourTally.Total(t.After));
            if (before <= 0) return new List<Block> { ArmourEmpty(input, w) };
            var stopped = before - after;
            var blocks = new List<Block>();
            var all = Shown(new List<double> { after, stopped });   // got through, stopped: they add up to what reached your armor
            var bar = new Block
            {
                Kind = "armorbar", Title = input.IsSelf ? ArmourStopped : FellowArmourStopped, Value = StoppedShare(all[1], all[0]), Text = ArmourStoppedWord, Src = SrcPc, Source = TagMeasured,
                Items = new List<Block> { new Block { Kind = "part", Id = "all", Title = "", Colour = ArmourTotalColour, Tone = DamageStoppedTone, Value = all[0], Value2 = all[1],
                                                      Fraction = 1, Fraction2 = (float)(after / before), Text = StoppedShare(all[1], all[0]) } },
            };
            // per damage type, and per foe when the window knows who hit you (the minute buckets keep no source): a foe's types in the book's order,
            // each with what reached your armor and what got through
            // 0.8.5: a foe by the game's name (FoeGroup): the ranged Draugr's hits are the Draugr's
            double Of(Dictionary<string, float> d, string source, string ty) => d.Where(kv => TypeOf(kv.Key) == ty && (source == null || FoeGroup(input, SourceOf(kv.Key)) == source)).Sum(kv => (double)kv.Value);
            var byType = BattleTypes.Select(ty => (ty, b: Of(t.Before, null, ty), a: Of(t.After, null, ty))).Where(x => x.b > 0).OrderByDescending(x => x.b).ToList();
            var foes = t.Before.Keys.Select(SourceOf).Where(s => s.Length > 0).Select(s => FoeGroup(input, s)).Distinct()
                .Select(s => (s, types: BattleTypes.Select(ty => (ty, before: Of(t.Before, s, ty), after: Of(t.After, s, ty))).Where(x => x.before > 0 || x.after > 0).ToList()))
                .Select(x => (x.s, x.types, b: x.types.Sum(y => y.before))).Where(x => x.b > 0)
                .OrderByDescending(x => x.b).ThenBy(x => x.s, StringComparer.Ordinal).Take(RowTop).ToList();
            // one scale for the page (the damage-row grammar): the largest row of either list fills the track
            var max = byType.Select(x => x.b).Concat(foes.Select(x => x.types.Sum(y => Math.Max(y.before, y.after)))).Max();
            blocks.Add(new Block
            {
                Kind = "armour", Title = input.IsSelf ? ArmourByType : FellowArmourByType, Text = from, Tone = input.IsSelf ? ArmourKeyTone : FellowArmourKeyTone, Src = SrcPc, Source = TagMeasured,
                Items = new[] { bar }.Concat(byType.Select(x => ArmourRow(x.ty, DamageIcon(x.ty), TypeName(x.ty), DamageColour(x.ty), "type", max, new List<(string, double, double)> { (x.ty, x.b, x.a) }))).ToList(),
            });
            if (foes.Count > 0)
                blocks.Add(new Block
                {
                    Kind = "armour", Title = ArmourByFoe, Src = SrcPc, Source = TagMeasured,
                    Items = foes.Select(x => ArmourRow(x.s, FoeIcon(input, x.s), Who(input, x.s), null, null, max, x.types)).ToList(),
                });
            else if (input.IsSelf && IsShortWindow(w) && w != TimeWindow.Session) blocks.Add(new Block { Kind = "note", Text = ArmourPerFoe });   // a fellow shares no foes: their About says so
            if (!input.IsSelf && w != TimeWindow.SinceInstall && input.Log != null && input.Log.ArmorClipped) blocks.Add(new Block { Kind = "note", Text = FellowArmourClipped });
            // All: these numbers are the armour ledger's, which starts at its own date (not the install): the label says that date (rule A/C, DateFrom)
            if (w == TimeWindow.SinceInstall) foreach (var b in blocks) DateFrom(b, input.IsSelf ? input.ArmourFromUtc ?? input.SessionStartUtc ?? input.NowUtc : input.ArmourFromUtc);
            return blocks;
        }

        /// <summary>B27: an empty Your armour says why (armour is counted from its own date, 0.7 on: right after installing there is nothing)
        /// and when it fills: "Your armour has stopped nothing in the last 30 minutes" / "Counted from 9 October, when this version of
        /// Hearthwoven first ran. A foe's hit on your armour shows up here."</summary>
        public static Block ArmourEmpty(PanelInput input, TimeWindow w)
        {
            var when = w == TimeWindow.SinceInstall ? " yet" : w == TimeWindow.Session ? " this session" : " " + InWindowWords(w);
            if (!input.IsSelf)   // 0.9 (armor-share): their PC's record, in their words
                return Empty("Their armor has stopped nothing" + when,
                             (input.ArmourFromUtc.HasValue ? "Counted on their PC from " + RecordDate(input, input.ArmourFromUtc.Value) : "Counted on their PC") + ". A foe's hit on their armor shows up here.");
            var from = input.ArmourFromUtc ?? input.SessionStartUtc ?? input.NowUtc;
            return Empty("Your armor has stopped nothing" + when,
                         "Counted from " + RecordDate(input, from) + (input.ArmourFromUtc.HasValue ? ", when this version of Hearthwoven first ran" : "") + ". A foe's hit on your armor shows up here.");
        }

        /// <summary>A damage row of the view (Kind "armourrow", the id saved state knows): its number is what reached your armor (the bar's total,
        /// as every damage row's number), Fraction that against the page's largest row. A part per damage type (PanelModel.DamageStoppedTone) as
        /// wide as what reached your armor, filled by what got through (Value, Fraction2 of the part), the rest what your armor stopped (Value2).
        /// The shown amounts are rounded together (Shown), so a part's two numbers add up and the parts add up to the row. Its line is the share
        /// stopped only ("Poison 20 %", Text; 0.8.1, Joost: on this view the share matters more than the amount, and the numbers are in the bar),
        /// so "my armor stops blunt better than pierce" reads at a glance; unlike the other damage rows, whose % is the part's share of the row.</summary>
        static Block ArmourRow(string id, string icon, string title, string colour, string tone, double max, List<(string ty, double before, double after)> types)
        {
            var reached = types.Select(x => Math.Max(x.before, x.after)).ToList();
            var got = types.Select((x, k) => Math.Min(Math.Max(0, x.after), reached[k])).ToList();
            var shown = Shown(types.SelectMany((x, k) => new[] { got[k], reached[k] - got[k] }).ToList());
            var parts = types.Select((x, k) =>
            {
                string through = shown[2 * k], stopped = shown[2 * k + 1];
                return new Block
                {
                    Kind = "part", Id = x.ty, Icon = DamageIcon(x.ty), Title = TypeName(x.ty), Colour = DamageColour(x.ty), Tone = DamageStoppedTone,
                    Value = through, Value2 = stopped, Fraction = (float)reached[k], Fraction2 = reached[k] > 0 ? (float)(got[k] / reached[k]) : 0,
                    Text = StoppedShare(stopped, through),
                };
            }).ToList();
            var total = reached.Sum();
            return new Block
            {
                Kind = "armourrow", Id = id, Icon = icon, Title = title, Colour = colour, Tone = tone, Value = NAtLeast(total),
                Fraction = max > 0 ? (float)(total / max) : 0, Src = SrcPc, Source = TagMeasured, Items = parts,
            };
        }

        /// <summary>What your armor stopped of what reached it, in whole percent ("73 %"), from the two shown amounts (stopped, got through) so it
        /// agrees with the numbers in the bar: never 100 % while something got through, "&lt;1 %" for a little, never 0 % for a real amount.</summary>
        static string StoppedShare(string stopped, string through)
        {
            double s = ShownWhole(stopped), r = s + ShownWhole(through);
            var p = r > 0 ? Math.Round(100 * s / r) : 0;
            if (p >= 100 && (through ?? "0") != "0") p = 99;
            else if (p <= 0 && (stopped ?? "0") != "0") return UnderOnePercent;
            return N(p) + " %";
        }

        // a shown amount ("1 040", "<1": nothing whole) as a number
        static double ShownWhole(string v) => v == LessThanOne || string.IsNullOrEmpty(v) ? 0 : ParseCount(v);

        /// <summary>
        /// Defence's received block becomes a view switch (Battle calls this after BattleDefense): Received = the "Received from" block exactly as
        /// it was, Your armour = ArmourView. Nothing changes when the page has no Received from (nothing received in the window). 0.9 (armor-share): on a
        /// fellow's book too, with their chips ("What hurt them" / "What their armor stopped"); a copy from before 0.9 (or a log without armor minutes)
        /// shows the one line FellowArmourOlder in the view, never an empty or zero bar; a day window (their copy shares no armor days) keeps the page as it was.
        /// </summary>
        static void ArmourSwitch(PanelInput input, PanelView view, PanelState state, string page, TimeWindow w)
        {
            if (input == null || state == null) return;
            if (!input.IsSelf && IsDayWindow(w)) return;
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
            if (armour == null && input.IsSelf) return;
            var shared = armour != null;
            if (!shared) armour = new List<Block> { new Block { Kind = "note", Text = FellowArmourOlder } };   // 0.9: an older friend's copy, said once
            var tmp = new PanelView();
            Switch(tmp, state, page, "view", null, (ReceivedId, input.IsSelf ? ReceivedView : FellowReceivedView, new List<Block> { holder[at] }),
                                                   (ArmourId, input.IsSelf ? ArmourViewLabel : FellowArmourViewLabel, armour));
            holder.RemoveAt(at); holder.InsertRange(at, tmp.Blocks);
            // Your armour has its own "About these numbers" in every window (G5: the chapter's box, All only, speaks of other numbers; review fix:
            // the lines that say how it relates to Received are in reach wherever the view is); a fellow's view that shares none keeps the page's box
            if (shared && tmp.Blocks.FirstOrDefault(b => b.Kind == "switch")?.Items?.FirstOrDefault(x => x.Selected)?.Id == ArmourId) ArmourNumbers(input, view);
        }

        /// <summary>The switch's view ids (state.View["Battle/defense/view"]).</summary>
        public const string ReceivedId = "received", ArmourId = "armour";

        /// <summary>The Your armour view's "About these numbers": the ArmourAbout lines, the first one under the ledger's own date ("From 9 October").
        /// 0.9, a fellow's book: FellowArmourAbout (what their PC recorded and shares; never "Complete for you"), under their ledger's date.</summary>
        static void ArmourNumbers(PanelInput input, PanelView view)
        {
            var from = input.IsSelf ? input.ArmourFromUtc ?? input.SessionStartUtc : input.ArmourFromUtc;
            view.AboutNumbers = new Block
            {
                Kind = "aboutnumbers", Id = NumbersTarget, Title = AboutNumbersTitle,
                // a fellow's box stays undated, as every box on their book (their ledger's date is on the view's own line, "Recorded from 6 October · their PC")
                Items = (input.IsSelf ? ArmourAbout : FellowArmourAbout).Select((l, k) => new Block { Kind = "aboutline", Title = k == 0 && from.HasValue && input.IsSelf ? FromLabel(input, from.Value) : l.title, Text = l.text }).ToList(),
            };
        }
    }
}
