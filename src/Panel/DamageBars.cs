using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    public static partial class PanelModel
    {
        // ---------- the damage rows (0.8, Joost's battle plate settings, work/hearthwoven-0.8/density-feedback/DAMAGE-BARS.md) ----------
        // Every list of damage bars in the Battle chapter (Damage's three views: By weapon, By type, By foe; Defence's Received from; Last
        // fight's foes; the feed's cards take the scale only): the rows of a page share one scale, a bar's zero sits at DamageFloor of its
        // track, and a thin line under it shows the true length from zero. Drawn by PanelUi.DamageRows (Chapters/DamageRowsUi.cs), mirrored by dmgRows() in the preview.

        /// <summary>Where a damage bar's zero sits on its track (Joost 2026-10-10: "zero at 25 %"): the smallest foe's bar still has room
        /// for its parts and their numbers; the true-length line under it keeps the real proportion.</summary>
        public const float DamageFloor = 0.25f;

        /// <summary>The damage rows' measures (px at scale 1, PanelUi and the preview alike; Joost's condensed settings, 13:25 and 13:40): the bar
        /// (16: a bold 12 or 13 px number inside with 2 px above and below, PanelLook.MinBarDigits) and its faint track (2 px round it), the
        /// corner radius of both, the gap between parts, a part's least width, the true-length line, the air from bar to line and from line to
        /// the share line, the share line's height, and the air between two rows.</summary>
        public const float DamageBar = 16, DamageTrack = 20, DamageRadius = 2, DamageGap = 3, DamagePartMin = 4, DamageTrue = 3, DamageInner = 1, DamageShareLine = 18, DamageFoeGap = 6;

        /// <summary>A damage bar's length as a share of its track: DamageFloor plus the rest by its total against the page's largest
        /// (<paramref name="fraction"/>, 0..1): the largest fills the track, a foe of almost nothing stands at the floor.</summary>
        public static float DamageBarWidth(float fraction) => DamageFloor + (1 - DamageFloor) * ClampShare(fraction);

        /// <summary>The true-length line under a damage bar, as a share of the track: its total against the page's largest, from zero.</summary>
        public static float DamageTrueWidth(float fraction) => ClampShare(fraction);

        static float ClampShare(float v) => float.IsNaN(v) ? 0 : Math.Max(0f, Math.Min(1f, v));

        /// <summary>
        /// By type as damage rows (0.8, Joost: By weapon, By type and By foe in one grammar): the damage grid's types, each a row on one scale (its
        /// total against the largest type's) in the book's type order, its bar split by the grid's columns: the weapon kinds in their fixed category
        /// colours, or with Everyone the players in theirs (the head's Colour), each part with the column's name and icon. Read from the grid's own
        /// cells (their Fraction is the amount over the grid's largest cell, so a type's parts and totals keep their proportions exactly); rows of
        /// Kind "source", Tone "type" (the picture is the type's swatch with its icon), parts of Kind "part".
        /// </summary>
        public static List<Block> DamageTypeRows(Block grid)
        {
            var items = grid?.Items ?? new List<Block>();
            var cols = items.FirstOrDefault(i => i.Kind == "weapons")?.Items ?? new List<Block>();
            var types = items.Where(i => i.Kind == "dmgtype").ToList();
            var sums = types.Select(t => (t.Items ?? new List<Block>()).Sum(c => (double)Math.Max(0f, c.Fraction))).ToList();
            var max = sums.DefaultIfEmpty(0).Max();
            var rows = new List<Block>();
            for (int k = 0; k < types.Count; k++)
            {
                var t = types[k]; var cells = t.Items ?? new List<Block>(); var parts = new List<Block>();
                for (int c = 0; c < cells.Count && c < cols.Count; c++)
                    if (cells[c].Fraction > 0)
                        parts.Add(new Block { Kind = "part", Id = cols[c].Id, Title = cols[c].Title, Icon = cols[c].Icon, Colour = cols[c].Colour, Value = cells[c].Value, Fraction = (float)(cells[c].Fraction / sums[k]) });
                rows.Add(new Block { Kind = "source", Id = t.Id, Title = t.Title, Icon = t.Icon, Colour = t.Colour, Tone = "type", Value = t.Value, Fraction = max > 0 ? (float)(sums[k] / max) : 0, Src = t.Src, Source = t.Source, Items = parts });
            }
            return rows;
        }

        /// <summary>Battle's rows by player drawn as damage rows (the group rows' Tone: Everyone's Damage and Defence, Last fight): each player's bar in
        /// their colour on one scale, their share of the group inside it, their damage by type as the line under it where their copy has types.</summary>
        public const string DamagePlayersDealt = "dmgdealt", DamagePlayersReceived = "dmgreceived";
        /// <summary>A player's damage row (DamagePlayerRows): its line is their damage by type (its "mix"), never its one part.</summary>
        public const string DamagePlayerTone = "player";

        /// <summary>A player's damage by type, the line under their bar (Kind "mix", the book's type order; Fraction = share of their damage).</summary>
        public static List<Block> DamageMix(IEnumerable<KeyValuePair<string, double>> byType)
        {
            var d = new Dictionary<string, double>();
            foreach (var kv in byType ?? Enumerable.Empty<KeyValuePair<string, double>>()) if (kv.Value > 0 && !string.IsNullOrEmpty(kv.Key)) { d.TryGetValue(kv.Key, out var o); d[kv.Key] = o + kv.Value; }
            var sum = d.Values.Sum();
            if (sum <= 0) return new List<Block>();
            return ReceivedTypes.Where(d.ContainsKey).Concat(d.Keys.Where(k => !ReceivedTypes.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
                                .Select(t => new Block { Kind = "mix", Id = t, Title = TypeName(t), Colour = DamageColour(t), Value = NAtLeast(d[t]), Fraction = (float)(d[t] / sum) }).ToList();
        }

        /// <summary>The group rows (GroupRowsKind) as damage rows (Tone DamagePlayerTone): a player with a number one part in their colour with their share of the group
        /// (Value2) as its number, on one scale (their total against the largest player's), their "mix" line kept; a player without one their
        /// words (Text) where the bar would be. The freshness ("live", "this PC") stays as the row's Note, under the name.</summary>
        public static List<Block> DamagePlayerRows(Block group)
        {
            var rows = (group?.Items ?? new List<Block>()).Where(r => r.Kind == GroupMemberKind || r.Kind == GroupQuietKind).ToList();
            var max = rows.Where(r => r.Kind == GroupMemberKind).Select(r => r.Fraction).DefaultIfEmpty(0).Max();
            return rows.Select(r => r.Kind == GroupMemberKind
                ? new Block
                {
                    Kind = "source", Id = r.Id, Icon = r.Icon, Title = r.Title, Note = r.Note, Colour = r.Colour, Value = r.Value, Fraction = max > 0 ? r.Fraction / max : 0, Src = r.Src, Source = r.Source, Tone = DamagePlayerTone,
                    Items = new[] { new Block { Kind = "part", Id = r.Id, Title = r.Title, Value = r.Value2, Colour = r.Colour, Fraction = 1 } }.Concat((r.Items ?? new List<Block>()).Where(i => i.Kind == "mix")).ToList(),
                }
                : new Block { Kind = "source", Id = r.Id, Icon = r.Icon, Title = r.Title, Note = r.Note, Text = r.Text, Items = new List<Block>() }).ToList();
        }

        /// <summary>The widest number a list of rows on one scale holds, for its number column, worked out from the first of them (By weapon's rows
        /// are blocks of their own, drawn one at a time; the renderer keeps the width for the run, REVIEW-081 #6): the largest total, at most this
        /// row's shown amount plus the half it may have been rounded down by (under 1 for "&lt;1") over its Fraction, with every digit an 8,
        /// so it never comes out a digit short of the largest.</summary>
        public static string DamageWidestNumber(Block row)
        {
            if (row == null || string.IsNullOrEmpty(row.Value)) return "";
            var less = row.Value == LessThanOne; var v = less ? 1 : ParseCount(row.Value);
            var largest = row.Fraction > 0 && row.Fraction < 1 ? (v + (less ? 0 : 0.5)) / row.Fraction : v;
            var s = Number(largest).ToCharArray();
            for (int i = 0; i < s.Length; i++) if (char.IsDigit(s[i])) s[i] = '8';
            return new string(s);
        }

        /// <summary>The damage rows' line in About these numbers (Damage's three views, Defence, Last fight): the floor, the true line, the hover.</summary>
        public const string DamageBarsAboutTitle = "The damage bars";
        public const string DamageBarsAbout = "All rows here share one scale, but each bar starts a quarter of the way along, so even a small one shows what it is made of. The thin line under a bar is its true length. A piece too narrow for its number shows it when you point at it.";

        /// <summary>Last fight's foes, when fellow players are in the numbers: the rows are your own damage (the hero above is the group's).</summary>
        public const string YourDamageToEach = "your damage to each";
    }
}
