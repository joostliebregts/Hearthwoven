using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Zones (design B, Joost 2026-10-08; work/hearthwoven-visual-vocabulary/proto/sinceB.*): two kinds of numbers on one
    /// page, told apart by place and material instead of a label beside each number. A page layout box, Kind "zone":
    ///   Id "character": the cool stone zone, the game's own counters. Title "Your character", Text "since you made this character, 15 Sep".
    ///   Id "pc": the warm ember zone, Hearthwoven's counts on this PC. Title "Since install", Text "this PC", Note the one
    ///   line under the heading ("Counting since you installed Hearthwoven on 8 Oct: keep playing and this fills up.").
    ///   Tone "empty": the ember zone has no number yet (the dimmed variant, same line).
    /// Items = the page's blocks of that kind, in their own order. Stone always comes first, then ember, then whatever
    /// belongs to neither (fellow players' numbers, blocks without a number of their own), so the place on the page says
    /// which kind of number it is. A page with one kind gets that zone only; a page with neither (Company, About) none.
    ///
    /// Automatic, from the Src every block already carries (new pages need no extra code): each block goes to the zone of
    /// its Src; a block without a number goes with the block it belongs to (a section heading with what follows it, a
    /// divider, link, note or hint with what came before). A few blocks carry numbers of both kinds and are split:
    /// a hero's further number of the other kind becomes that zone's hero, a skill's practice under its ladder likewise,
    /// the bosses on the biome strip (the character's kill counter) leave the strip for a row of boss marks in the stone
    /// zone, columns split by column, a deed card keeps only its own kind's line (the other line lives on the card's page).
    /// A heading with a number of its own (Foes: "860 foes defeated") becomes the hero of its zone; the heading keeps the
    /// page's name. Inside a zone no number carries the "since install" label: the zone says it (Block.SinceInstall off).
    /// </summary>
    public static partial class PanelModel
    {
        /// <summary>Tone of a columns box whose two parts stay together in the since-install zone though one is the server's book (Taming: born in your care beside born near you).</summary>
        public const string PairTone = "pair";
        public const string ZoneStone = "Your character", ZoneEmber = "Since install", ZoneThisPc = "this PC";
        public const string ZoneEmpty = "empty", ZoneSecond = "second", BossesDefeated = "Bosses defeated";
        /// <summary>Tone of a zone drawn tight (Woodcutting, which has the most to say: the stone zone's hero at the second-number size,
        /// less padding and spacing, the ember zone's line folded into its heading, the per-tree rows three across).</summary>
        public const string ZoneTight = "tight";

        /// <summary>"Counting since you installed Hearthwoven on 8 Oct: keep playing and this fills up." (no date known: without "on ...").</summary>
        /// <summary>A zone that already has numbers says only "Counting since you installed Hearthwoven on 8 Oct."; the fill-up clause is for an empty zone.</summary>
        public static string ZoneLine(string installed, bool empty = true) =>
            "Counting since you installed Hearthwoven" + (string.IsNullOrEmpty(installed) ? "" : " on " + installed) + (empty ? ": keep playing and this fills up." : ".");

        /// <summary>The line of an ember zone whose counts began later than the install (Deeds twins: a counter Hearthwoven first read
        /// when this version started): "Counting since 9 Oct, when Hearthwoven started these counts."; empty: with the fill-up clause.</summary>
        public static string ZoneLineBegan(string date, bool empty = true) =>
            "Counting since " + date + ", when Hearthwoven started these counts" + (empty ? ": keep playing and this fills up." : ".");

        /// <summary>A date as the zones say it: "8 Oct", with the year when it is not this year's.</summary>
        public static string ZoneDate(DateTime day, DateTime today) => day.ToString(day.Year == today.Year ? "d MMM" : "d MMM yyyy", Inv);

        // the game gives profiles from before its stats (2021) this fixed date instead of a real one: not a date to show
        static readonly DateTime NoCreationDate = new DateTime(2021, 2, 2);

        /// <summary>The zones of a page (Build calls this last). Changes nothing on a page without your character's or this PC's numbers.</summary>
        public static void Zones(PanelInput input, PanelState state, PanelView view)
        {
            if (view == null || view.ShowAbout) return;
            if (view.Active == Chapter.Company) return;   // the group's chapter: your numbers and fellow players' side by side, no zones
            if (view.Active == Chapter.Deeds && view.Page == "overview") return;   // the deed cards keep both lines with their own small label (integrate-05)
            if (view.Active == Chapter.Feats) return;   // the feats of all sources side by side, each saying how it is counted
            input = input ?? new PanelInput(); state = state ?? new PanelState();
            var plate = PlateOf(view);
            var list = plate != null ? plate.Items : view.Blocks;
            if (list == null) return;
            var work = new List<Block>(list);

            // a heading that is a number of one kind ("860 foes defeated"): that number leads its zone as the hero
            Block headHero = null;
            if (view.HeadingSrc == SrcCharacter || view.HeadingSrc == SrcPc)
            {
                var (value, label) = SplitNumber(view.Heading);
                if (value != null) headHero = new Block { Kind = "hero", Value = value, Title = label, Src = view.HeadingSrc, Source = TagOfSrc(view.HeadingSrc) };
            }
            if (headHero != null) work.Insert(0, headHero);

            var (stone, ember, rest) = Distribute(work);
            if (stone.Count == 0 && ember.Count == 0) return;

            if (headHero != null)
            {
                var name = view.List.FirstOrDefault(l => l.Selected)?.Label;   // the page's own name ("Foes")
                view.Heading = string.IsNullOrEmpty(name) ? Cap(headHero.Title) : name;
                view.HeadingSource = null; view.HeadingSrc = null;
                if (plate != null) { plate.Title = view.Heading; plate.Source = null; plate.Src = null; }
            }

            var zoned = new List<Block>();
            if (stone.Count > 0) zoned.Add(StoneZone(input, stone, view));
            if (ember.Count > 0) zoned.Add(EmberZone(input, state, view, ember));
            if (view.Active == Chapter.Deeds && stone.Count > 0 && ember.Count > 0) LinkTwins(stone, ember);
            zoned.AddRange(rest);
            // fix4: Defence answers "how well did I hold?" with blocks and parries, which this PC counts: that zone leads (the stone zone's
            // hit counts were the first thing on the page and the answer came third); on every other page stone still comes first
            if (view.Active == Chapter.Battle && view.Page == "defense" && stone.Count > 0 && ember.Count > 0) { var first = zoned.FindIndex(z => z.Kind == "zone" && z.Id == SrcPc); if (first > 0) { var z = zoned[first]; zoned.RemoveAt(first); zoned.Insert(0, z); } }
            // the tight pages must fit the plate; so must the Battle overview (the strip is tall) and Foes (the table is what the page is for)
            if ((view.Active == Chapter.Deeds && TightPages.Contains(view.Page)) || (view.Active == Chapter.Voyages && view.Page == "maps") || (view.Active == Chapter.Battle && (view.Page == "overview" || view.Page == "foes" || view.Page == "damage" || view.Page == "defense" || view.Page == "deaths"))) foreach (var z in zoned.Where(z => z.Kind == "zone" && z.Tone != ZoneEmpty)) Tighten(z, view.Active == Chapter.Deeds && (view.Page == "building" || view.Page == "mining") ? 2 : 3);
            // fix3-rest: a fellow's book on Voyages, Hall and Skills says what each zone counts in the zone's own line, with the one "as of" moment,
            // instead of a line above the plate that said "since install" over a zone that counts since the character was made
            if (!input.IsSelf && plate != null && input.LastRecordedUtc.HasValue && (view.Active == Chapter.Voyages || view.Active == Chapter.Stores || view.Active == Chapter.Skills))
            {
                foreach (var zn in zoned.Where(z => z.Kind == "zone")) zn.Text = string.IsNullOrEmpty(zn.Text) ? AsOf(input) : zn.Text + " · " + AsOf(input);
                plate.Text = null;
            }
            foreach (var z in zoned.Where(z => z.Kind == "zone")) OneIncompleteLine(z.Items);
            foreach (var z in zoned.Where(z => z.Kind == "zone")) Unlabel(z.Items);
            view.HeadingSinceInstall = false;
            list.Clear(); list.AddRange(zoned);
        }

        /// <summary>The Deeds pages (and Voyages > Maps, fix-rest: its found list, compass and biomes plus the map shared at the table must all fit above the fold) whose zones are drawn tight (Woodcutting first; the twins that would not fit the plate otherwise).</summary>
        public static readonly HashSet<string> TightPages = new HashSet<string> { "woodcutting", "mining", "building", "crafting", "farming", "fishing", "taming" };

        // Woodcutting must fit the plate without scrolling at the default scale (Joost, in game): its zones sit closer together
        static void Tighten(Block zone, int maxColumns = 3)   // maxColumns: Building's piece names are long ("Wood Floor 2x2"), two across keep them whole
        {
            zone.Tone = ZoneTight;
            // a line that says the counts began after the install ("Counting since 6 Oct, when Hearthwoven started these counts.") stays: it is news
            if (zone.Id == SrcPc && !string.IsNullOrEmpty(zone.Note) && zone.Note.StartsWith("Counting since you installed", StringComparison.Ordinal))
            {
                // "Counting since you installed Hearthwoven on 8 Oct: keep playing ..." becomes the heading's "this PC, since 8 Oct"
                var at = zone.Note.IndexOf(" on ", StringComparison.Ordinal); var end = zone.Note.IndexOfAny(new[] { ':', '.' }, Math.Max(0, at));
                if (at >= 0 && end > at) zone.Text += ", since " + zone.Note.Substring(at + 4, end - at - 4);
                zone.Note = null;
            }
            foreach (var x in zone.Items ?? new List<Block>())
            {
                if (x.Kind == "hero" && x.Tone == null) x.Tone = x.Items != null && x.Items.Count > 0 ? Compact : ZoneSecond;   // the stone zone's number at the lifted number's size (several numbers: the compact line, which keeps them all)
                if (x.Kind == "composition" && x.Tone == null) x.Tone = Thin;   // the bar at the reduced height the Hall uses
                if (x.Kind == "ranking") { x.Tone = ZoneTight; x.Columns = Math.Max(x.Columns, Math.Min(maxColumns, x.Items?.Count ?? 0)); }
            }
        }

        /// <summary>When the character's own counts start, said plainly (Joost 2026-10-09: "since Rowan was made" read oddly): "since you made
        /// this character" in your own book, "since Tor made this character" in a fellow's.</summary>
        public static string MadeLine(PanelInput input) =>
            input != null && input.IsSelf ? "since you made this character" : "since " + (string.IsNullOrEmpty(input?.PlayerName) ? "the player" : input.PlayerName) + " made this character";

        /// <summary>Several stats in one zone that each carry EarlierIncomplete (Woodcutting: wood brought in and trees felled): the
        /// line is said once, as the zone's last block, and covers them all (Joost 2026-10-09). One stat keeps its line where it is.</summary>
        public static void OneIncompleteLine(List<Block> items)
        {
            if (items == null) return;
            int Count(IEnumerable<Block> bs) => (bs ?? Enumerable.Empty<Block>()).Sum(b => b == null ? 0 : (b.Text == EarlierIncomplete ? 1 : 0) + (b.Note == EarlierIncomplete ? 1 : 0) + Count(b.Items));
            if (Count(items) <= 1) return;
            void Clear(List<Block> bs)
            {
                if (bs == null) return;
                bs.RemoveAll(b => b != null && b.Kind == "note" && b.Text == EarlierIncomplete);
                foreach (var b in bs.Where(b => b != null)) { if (b.Text == EarlierIncomplete) b.Text = null; if (b.Note == EarlierIncomplete) b.Note = null; Clear(b.Items); }
            }
            Clear(items);
            items.Add(new Block { Kind = "note", Text = EarlierIncomplete });
        }

        static Block StoneZone(PanelInput input, List<Block> items, PanelView view = null)
        {
            // a day window (HISTORY-06.md): your character's counters grew this much in the window, not since the character was made
            if (view != null && view.ShownWindow.HasValue && IsDayWindow(view.ShownWindow.Value))
                return new Block { Kind = "zone", Id = SrcCharacter, Title = input.IsSelf ? ZoneStone : Name(input) + "'s character", Text = "the game's own count, " + WindowLabel(view.ShownWindow.Value).ToLowerInvariant(), Items = items };
            var made = input.CharacterMade.HasValue && input.CharacterMade.Value.Date != NoCreationDate && input.CharacterMade.Value > DateTime.MinValue
                ? ", " + ZoneDate(input.CharacterMade.Value, Local(input, input.NowUtc)) : "";
            return new Block
            {
                Kind = "zone", Id = SrcCharacter, Title = input.IsSelf ? ZoneStone : Name(input) + "'s character",
                Text = MadeLine(input) + made, Items = items,
            };
        }

        static Block EmberZone(PanelInput input, PanelState state, PanelView view, List<Block> items)
        {
            // Battle's windowed pages count the chosen window of this session's log, not since install: the zone says the window
            var windowed = view.HasFilters && view.ShownWindow.HasValue && (view.Active == Chapter.Battle || view.ShownWindow != TimeWindow.SinceInstall);
            string installed = null;
            if (input.InstalledUtc.HasValue) installed = ZoneDate(Local(input, input.InstalledUtc.Value), Local(input, input.NowUtc));
            var empty = !items.Any(b => b.Kind != "empty" && Srcs(b).Any());
            var line = input.IsSelf && !windowed ? ZoneLine(installed, empty) : null;
            // a page whose counts began after the install (a counter baselined when this version first ran): the line says since when
            if (line != null && view.EmberFrom.HasValue && input.InstalledUtc.HasValue && view.EmberFrom.Value > input.InstalledUtc.Value.AddDays(1))
                line = ZoneLineBegan(ZoneDate(Local(input, view.EmberFrom.Value), Local(input, input.NowUtc)), empty);
            // nothing yet: the zone's line already says how it fills up, so the empty block keeps only its title
            if (empty && line != null) foreach (var b in items.Where(b => b.Kind == "empty")) b.Text = null;
            return new Block
            {
                Kind = "zone", Id = SrcPc, Title = windowed ? WindowLabelFor(input, view.ShownWindow.Value) : ZoneEmber,
                Text = input.IsSelf ? ZoneThisPc : Name(input) + "'s PC",
                Note = line,
                Tone = empty ? ZoneEmpty : null, Items = items,
            };
        }

        /// <summary>The words under a character total whose since-install part is counted in the ember zone (Joost 2026-10-09: "is since install
        /// part of the total, or must I add it?"): "in all · 30 of them since install".</summary>
        public static string PartLine(string since) => "in all · " + since + " of them since install";

        static string Unit(string title)
        {
            var words = (title ?? "").Trim().ToLowerInvariant().Split(' ');
            for (int k = 0; k < words.Length; k++)
            {
                var w = words[k];
                if (w.EndsWith("shes") || w.EndsWith("ches") || w.EndsWith("xes")) w = w.Substring(0, w.Length - 2);
                else if (w.EndsWith("s") && !w.EndsWith("ss") && w.Length > 3) w = w.Substring(0, w.Length - 1);
                words[k] = w;
            }
            return string.Join(" ", words);
        }
        static double Count(string v) { var d = new string((v ?? "").Where(char.IsDigit).ToArray()); return d.Length == 0 ? 0 : double.Parse(d, Inv); }

        /// <summary>
        /// Deeds twins: a character total (stone zone hero number, or a section's total) whose unit the ember zone's hero counts too gets the
        /// line "in all · 30 of them since install", so nobody adds the two up. Only where the since number is a real part (smaller, above 0) and
        /// does not follow a filter the total ignores. A number that already says it (the model's own Woodcutting and Mining line) stays as it is.
        /// </summary>
        static void LinkTwins(List<Block> stone, List<Block> ember)
        {
            var since = new Dictionary<string, Block>();
            foreach (var h in ember.Where(b => b.Kind == "hero"))
                foreach (var n in new[] { h }.Concat(h.Items ?? new List<Block>()))
                    if (!string.IsNullOrEmpty(n.Value) && (n.Note == null || n.Note.IndexOf("filter", StringComparison.OrdinalIgnoreCase) < 0) && !since.ContainsKey(Unit(n.Title))) since[Unit(n.Title)] = n;
            if (since.Count == 0) return;
            string PartOf(string title, string total)
            {
                if (!since.TryGetValue(Unit(title), out var e)) return null;
                double all = Count(total), part = Count(e.Value);
                return part > 0 && part < all ? PartLine(e.Value) : null;
            }
            void Walk(IEnumerable<Block> blocks)
            {
                foreach (var b in blocks ?? Enumerable.Empty<Block>())
                {
                    if (b.Kind == "hero")
                        foreach (var n in new[] { b }.Concat(b.Items ?? new List<Block>()))
                        {
                            if (n.Faded != null || (n.Note ?? "").StartsWith("in all", StringComparison.Ordinal)) continue;
                            var line = PartOf(n.Title, n.Value);
                            if (line != null) n.Note = string.IsNullOrEmpty(n.Note) ? line : line + " · " + n.Note;
                        }
                    else if (b.Kind == "section" && !string.IsNullOrEmpty(b.Value) && string.IsNullOrEmpty(b.Text)) b.Text = PartOf(b.Title, b.Value);
                    else if (b.Kind == "counts") foreach (var t in b.Items ?? new List<Block>()) { if (string.IsNullOrEmpty(t.Note) && PartOf(t.Title, t.Value) != null) t.Note = since[Unit(t.Title)].Value + " " + SinceWord; }   // a tile says it short, as the item tiles do: "7 since install"
                    else if (b.Kind == "columns" || b.Kind == "column" || b.Kind == "switch" || b.Kind == "view") Walk(b.Items);
                }
            }
            Walk(stone);
        }

        static void Unlabel(IEnumerable<Block> blocks)
        {
            foreach (var b in blocks ?? Enumerable.Empty<Block>()) { b.SinceInstall = false; Unlabel(b.Items); }
        }

        /// <summary>The zone a block belongs to by its numbers: "character", "pc", "fellows" (stays outside the zones), or
        /// null for a block without a number of its own (it goes with its neighbours).</summary>
        public static string ZoneOf(Block b)
        {
            if (b == null) return null;
            if (b.Kind == "zone") return b.Id;
            if (b.Kind == "empty") return b.Tone == SinceInstallTone ? SrcPc : null;   // a since-install section with nothing yet
            if (b.Kind == "biometiles") return SrcCharacter;   // the biomes found: the character's own record (KnownBiomes), no number to carry a mark
            var s = Srcs(b).ToList();
            if (s.Count == 0) return null;
            if (s.Contains(SrcFellows)) return SrcFellows;
            if (b.Src != null && !IsBox(b)) return b.Src;   // a layout box can inherit a mark from its page; its parts decide
            // no number of its own (a layout box, a list): what most of its parts are, each part by its own kind
            var parts = (b.Items ?? new List<Block>()).Select(ZoneOf).Where(z => z != null).ToList();
            return parts.Count == 0 ? null : parts.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;
        }

        // a page's blocks into stone, ember and the rest, splitting the few blocks that carry both kinds
        static (List<Block> stone, List<Block> ember, List<Block> rest) Distribute(List<Block> blocks)
        {
            var entries = new List<(Block b, string zone)>();
            foreach (var b in blocks) entries.AddRange(Expand(b));
            // blocks without a number of their own: a section heading goes with what follows it, anything else with what came before
            var zones = entries.Select(e => e.zone).ToArray();
            for (int i = 0; i < entries.Count; i++)
            {
                if (zones[i] != null) continue;
                string before = null, after = null;
                for (int k = i - 1; k >= 0 && before == null; k--) before = entries[k].zone;
                for (int k = i + 1; k < entries.Count && after == null; k++) after = entries[k].zone;
                zones[i] = entries[i].b.Kind == "section" ? after ?? before : before ?? after;
            }
            List<Block> Of(Func<string, bool> keep) => Tidy(entries.Where((e, i) => keep(zones[i])).Select(e => e.b).ToList());
            return (Of(z => z == SrcCharacter), Of(z => z == SrcPc), Of(z => z != SrcCharacter && z != SrcPc));
        }

        // no divider at a zone's top or bottom, none twice in a row (the zone frames separate now)
        static List<Block> Tidy(List<Block> l)
        {
            var r = new List<Block>();
            foreach (var b in l) if (!(b.Kind == "divider" && (r.Count == 0 || r[r.Count - 1].Kind == "divider"))) r.Add(b);
            while (r.Count > 0 && r[r.Count - 1].Kind == "divider") r.RemoveAt(r.Count - 1);
            return r;
        }

        // one block as (block, zone) entries; a block with numbers of both kinds comes out as one entry per kind
        static IEnumerable<(Block, string)> Expand(Block b)
        {
            var other = new Func<string, string>(z => z == SrcCharacter ? SrcPc : z == SrcPc ? SrcCharacter : null);
            switch (b.Kind)
            {
                case "columns":
                    {
                        if (b.Tone == PairTone) { yield return (b, SrcPc); yield break; }   // a pair: the server's book beside this PC's count (Taming), each part keeping its own source mark
                        var parts = (b.Items ?? new List<Block>()).Select(c => Distribute(c.Items ?? new List<Block>())).ToList();
                        var used = new[] { parts.Any(p => p.stone.Count > 0), parts.Any(p => p.ember.Count > 0), parts.Any(p => p.rest.Count > 0) };
                        if (used.Count(u => u) <= 1)
                        {
                            yield return (b, used[0] ? SrcCharacter : used[1] ? SrcPc : parts.SelectMany(p => p.rest).Select(ZoneOf).FirstOrDefault(z => z != null));   // columns of the server's own numbers stay outside the zones (fix3-rest), not with the zone before
                            yield break;
                        }
                        foreach (var x in ColumnsOf(parts.Select(p => p.stone))) yield return (x, SrcCharacter);
                        foreach (var x in ColumnsOf(parts.Select(p => p.ember))) yield return (x, SrcPc);
                        foreach (var x in ColumnsOf(parts.Select(p => p.rest))) yield return (x, SrcFellows);
                        yield break;
                    }
                case "switch":
                    {
                        // only the chosen view carries blocks: they are sorted the same way. One kind: the switch (its chips)
                        // stands in that zone. Both kinds: the chips stay with the stone part, the ember part follows in its zone
                        var chosen = (b.Items ?? new List<Block>()).FirstOrDefault(v => v.Selected && v.Items != null);
                        if (chosen == null) { yield return (b, ZoneOf(b)); yield break; }
                        var (s, e, r) = Distribute(chosen.Items);
                        if (s.Count > 0 && e.Count > 0)
                        {
                            chosen.Items = s; yield return (b, SrcCharacter);
                            foreach (var x in e) yield return (x, SrcPc);
                            foreach (var x in r) yield return (x, SrcFellows);
                            yield break;
                        }
                        if (s.Count == 0 && e.Count == 0) { chosen.Items = r; yield return (b, r.Count > 0 ? SrcFellows : null); yield break; }
                        chosen.Items = s.Count > 0 ? s : e;
                        yield return (b, s.Count > 0 ? SrcCharacter : SrcPc);
                        foreach (var x in r) yield return (x, SrcFellows);
                        yield break;
                    }
                case "hero":
                    {
                        var lifted = Lift(b, n => n.Kind == "number" && other(b.Src) != null && n.Src == other(b.Src));
                        yield return (b, ZoneOf(b));
                        if (lifted.Count > 0)
                        {
                            var h = AsHero(lifted[0]);
                            if (lifted.Count > 1) h.Items = lifted.Skip(1).ToList();
                            yield return (h, h.Src);
                        }
                        yield break;
                    }
                case "ladders":
                    // the skill beside a page's deed: it ends the plate, under both zones, and belongs to neither
                    yield return (b, b.Tone == SkillStripTone ? SrcFellows : ZoneOf(b));
                    yield break;
                case "ladder":
                    {
                        var lifted = Lift(b, n => n.Kind == "practice" && b.Src == SrcCharacter && n.Src == SrcPc);
                        yield return (b, ZoneOf(b));
                        foreach (var n in lifted) yield return (AsHero(n), n.Src);
                        yield break;
                    }
                case "biomes":
                    {
                        // the bosses are the character's kill counter: a row of boss marks in the stone zone; the strip keeps
                        // this PC's numbers, and without any (a first evening) it is left out: no bars, no tiles, no empty slots
                        var bosses = new List<Block>();
                        foreach (var t in b.Items ?? new List<Block>())
                        {
                            var mine = (t.Items ?? new List<Block>()).Where(x => x.Kind == "boss" && x.Src == SrcCharacter).ToList();
                            if (mine.Count == 0) continue;
                            bosses.AddRange(mine);
                            t.Items = t.Items.Except(mine).ToList();
                            if (t.Items.Count == 0) t.Items = null;
                        }
                        if (bosses.Count > 0) yield return (new Block { Kind = "bosses", Title = BossesDefeated, Items = bosses, Src = SrcCharacter, Source = TagCharacter }, SrcCharacter);
                        var numbers = (b.Items ?? new List<Block>()).Any(t => (t.Value ?? "").Any(char.IsDigit) || (t.Value2 ?? "").Any(char.IsDigit) || t.Count > 0);
                        if (numbers) yield return (b, ZoneOf(b));
                        yield break;
                    }
                case "cards":
                    // a deed card in a zone keeps the lines of its own kind; the other line is on the card's own page, in its zone
                    foreach (var c in b.Items ?? new List<Block>())
                        if (c.Items != null && (c.Src == SrcCharacter || c.Src == SrcPc))
                        {
                            c.Items = c.Items.Where(x => x.Src == null || x.Src == c.Src).ToList();
                            if (c.Items.Count == 0) c.Items = null;
                        }
                    yield return (b, ZoneOf(b));
                    yield break;
                case "rows": case "itemgrid": case "counts": case "tiles": case "bars": case "ranking":
                    {
                        // a list whose rows are of both kinds (Maps: what the character found, and the map shared at the table):
                        // the same list twice, each with its own rows, in its own zone
                        var rows = b.Items ?? new List<Block>();
                        var ch = rows.Where(i => ZoneOf(i) == SrcCharacter).ToList(); var pc = rows.Where(i => ZoneOf(i) == SrcPc).ToList();
                        if (b.Src == null && ch.Count > 0 && pc.Count > 0 && ch.Count + pc.Count == rows.Count)
                        {
                            yield return (Shell(b, ch), SrcCharacter);
                            yield return (Shell(b, pc), SrcPc);
                            yield break;
                        }
                        yield return (b, ZoneOf(b));
                        yield break;
                    }
                default:
                    yield return (b, ZoneOf(b));
                    yield break;
            }
        }

        static readonly System.Reflection.MethodInfo CopyOf = typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        // the same block with other items (every other field as it was)
        static Block Shell(Block b, List<Block> items) { var c = (Block)CopyOf.Invoke(b, null); c.Items = items; return c; }

        static List<Block> Lift(Block b, Func<Block, bool> take)
        {
            var lifted = (b.Items ?? new List<Block>()).Where(take).ToList();
            if (lifted.Count > 0) { b.Items = b.Items.Except(lifted).ToList(); if (b.Items.Count == 0) b.Items = null; }
            return lifted;
        }

        // a number lifted out of another block leads its zone at the size it had there (the hero's second number size)
        static Block AsHero(Block n) => new Block { Kind = "hero", Value = n.Value, Title = n.Title, Note = n.Note, Src = n.Src, Source = n.Source ?? TagOfSrc(n.Src), Tone = ZoneSecond };

        // the non-empty stretches side by side again; one left over stands inline, without the box
        static IEnumerable<Block> ColumnsOf(IEnumerable<List<Block>> stretches)
        {
            var cols = stretches.Where(s => s.Count > 0).ToList();
            if (cols.Count == 0) return Enumerable.Empty<Block>();
            if (cols.Count == 1) return cols[0];
            return new[] { new Block { Kind = "columns", Items = cols.Select(c => new Block { Kind = "column", Items = c }).ToList() } };
        }
    }
}
