using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// A fellow's Deeds in Today, 7 days and 30 days (0.8.5, v085-groupdeeds). Battle shares damage per day by weapon kind and type (dayDealt,
    /// 0.8.3); the Deeds pages now share, per local day for the last 30 days, the numbers each page's hero and first bar show: one small part
    /// list per family ("dayDeeds"), nested per day so a live update carries only today's. Full day rows would add tens of KB per copy;
    /// these add about 1.2 KB packed for a typical month and 3 KB for a busy one, 0.6 KB of it the meals and 0.5 KB the voyages (test/DayDeedsTests.cs
    /// measures it). On the wire each
    /// family's part names stand once (DayDeedsNames) and a day holds only its amounts in that order (WriteDayDeeds, ReadDayDeeds): a part's
    /// name repeated on every day cost more than its number.
    ///
    /// Families (day -> family -> part -> amount, whole numbers, only what is above 0):
    ///   wood, ore   item token -> brought in (the wood and the stone and ore the pickup counted: Woodcutting's and Mining's bar)
    ///   built       hammer tab -> pieces built (Building's Category row; the tab as the sender's game data names it, "Other" for none)
    ///   ground      piece token -> strokes (Groundwork's bar); feast: feast piece -> set out (beside Cooking's hero)
    ///   dish        item token -> dishes made (Cooking's Dishes by kind)
    ///   plant       plant piece token -> planted (Farming's crops, by what they grow into)
    ///   fish        fish token -> caught on the line (Fishing's Fish caught)
    ///   game        the game's counters the heroes read (GameShared: gear crafted per kind, upgrades, picked, fish hooked and caught, tamed,
    ///               petted)
    ///   own         Hearthwoven's own counts beside a hero: trees felled and axe hits (Woodcutting)
    ///   voyage      sail: km sailed that day in metres (the game's DistanceSail, Voyages > Sailing's hero), helm: of it at the helm
    ///               (DistanceSailHelm, Sailing's bar); cargo: item token -> item-metres carried at the helm or pulling a cart (SessionEvents.
    ///               CargoMeters, Voyages > Cargo's items and total), so Together's Sailed and Cargo carried and Everyone's Sailing and Cargo have
    ///               a fellow's days (0.8.5, v085-together; the distances behind a cargo sum, CargoStretch, are not shared). A day with more
    ///               than DayDeedsParts item kinds adds the rest up in one part (DdCargoRest), so a fellow's total is their own book's. Its part names stand in "parts" on every copy that shares them, also when nothing was
    ///               sailed: a copy without them (the 0.8.5 test build) shares no voyage days, never "sailed nothing" (SharesVoyageDays)
    ///   ate, feastOf  the meals eaten as the record keeps them (SessionEvents.AteFoodMadeBy "cook|dish", AteFromFeastOf "placer id|feast"), so
    ///               Meals, the group's Meals and Cooking's Who enjoyed read them as they read the totals (MealsOf, FoodRows). Not AteFromFeastAt:
    ///               its feast ids made a new name for every feast (about 0.1 KB more in a busy month), so a fellow's feast serving in a day window
    ///               counts for whoever set the feast out (FeastBook's rule before 0.8), not for a teammate who made it
    /// A fellow's day window is a copy of their input whose tallies hold these parts (FellowDeedsWindow), so each page's own code draws its
    /// hero and first bar; what a page lists deeper (the pieces one by one, the hits per tree) is not shared and the page says so in one line
    /// (DeedsSharedLine). The window is the viewer's calendar, as Battle's (FellowDayRows): their day is their whole local day. Left out to stay
    /// small (measured on real evenings, about 20 parts each): the picks per crop (Farming's tiles show what was planted), fish that got away,
    /// commands given.
    /// </summary>
    public static partial class PanelModel
    {
        public const string DayDeedsKey = "dayDeeds";
        public const string DdWood = "wood", DdOre = "ore", DdBuilt = "built", DdGround = "ground", DdFeast = "feast", DdDish = "dish", DdPlant = "plant",
                            DdFish = "fish", DdGame = "game", DdOwn = "own", DdAte = "ate", DdFeastOf = "feastOf";
        /// <summary>0.8.5: the voyage family's parts (km sailed and at the helm, in metres) and the cargo family (item-metres per item): Together's
        /// Sailed and Cargo carried and Everyone's Sailing and Cargo on a fellow's days.</summary>
        public const string DdVoyage = "voyage", DdSail = "sail", DdHelm = "helm", DdCargo = "cargo";
        /// <summary>0.8.5 (review): a day's cargo past the DayDeedsParts largest items, added up in one part that no cap cuts, read back as one
        /// item, "Other items" (CargoOtherItems). A copy without it (the 0.8.5 test build) reads as before.</summary>
        public const string DdCargoRest = "~rest", CargoOtherItems = "Other items";
        /// <summary>The families of the meals eaten (Meals; test/DayDeedsTests.cs measures what they add).</summary>
        public static readonly string[] DdMeals = { DdAte, DdFeastOf };
        public const string DdFelled = "felled", DdChop = "chop";
        /// <summary>The game's counters a day share carries (day growth, as the day rows keep them).</summary>
        public static readonly string[] GameShared = { "CraftWeapon", "CraftArmor", "CraftTool", "CraftTrinket", "Upgrades", "HarvestCrop", "FishCaught", "FishHooked", "CreatureTamed", "TamedPetting", DayKillsPart };
        /// <summary>0.9.1 (Joost: "who was on yesterday and what happened"): the game's kill counter's growth per day (a count, no foe), so a fellow's
        /// Recent shows their foes defeated on Yesterday to 30 days. Its name stands first in the game family's names on every copy that shares it,
        /// also with nothing defeated (SharesDayKills): a copy without it (before 0.9.1) shares none, never "defeated nothing". About 15 bytes of
        /// JSON once per copy plus a few per day with kills (test-panel/YesterdayTests.cs measures it).</summary>
        public const string DayKillsPart = "EnemyKills";
        /// <summary>The most parts one family keeps for one day (the largest stay): a mod with hundreds of dishes cannot grow a copy.</summary>
        public const int DayDeedsParts = 24;
        /// <summary>The version that first shares the Deeds days (OlderLine names it when a fellow's copy has none).</summary>
        public const string NeedsDayDeeds = "0.8.5";
        /// <summary>The one key a copy's trees felled and axe hits stand under in a fellow's day window (no tree is named: only the totals are shared).</summary>
        const string SharedPart = "~shared";

        /// <summary>
        /// Your Deeds per local day for the last <paramref name="days"/> days (with the running session's unsaved part on today), in the families
        /// above: what the snapshot shares as "dayDeeds". <paramref name="kinds"/> carries the game data that sorts tokens (GatherKind, PieceKind,
        /// PieceTab, ItemKind), as your own pages sort them. An empty map when nothing was done; null without a day history.
        /// </summary>
        public static Dictionary<string, Dictionary<string, Dictionary<string, float>>> DayDeeds(DayHistory h, DateTime today, int days = 30, DayHistory.Row pending = null, PanelInput kinds = null)
        {
            if (h == null) return null;
            kinds = kinds ?? new PanelInput();
            var from = today.Date.AddDays(1 - days);
            var rows = h.Rows.Where(r => r.IsDay && r.Start >= from && r.Start <= today.Date).Select(r => (key: r.Period, row: r)).ToList();
            if (pending != null) rows.Add((DayHistory.DayKey(today), pending));
            var sum = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();
            foreach (var (key, r) in rows)
            {
                if (!sum.TryGetValue(key, out var day)) sum[key] = day = new Dictionary<string, Dictionary<string, double>>();
                void Put(string fam, string part, double v)
                {
                    if (v <= 0 || string.IsNullOrEmpty(part) || !Json.IsFinite(v)) return;
                    if (!day.TryGetValue(fam, out var d)) day[fam] = d = new Dictionary<string, double>();
                    d.TryGetValue(part, out var o); d[part] = o + v;
                }
                foreach (var kv in r.Events.PickedUp) { var g = GatherKind(kinds, kv.Key); if (g == "wood") Put(DdWood, kv.Key, kv.Value); else if (g == "mining") Put(DdOre, kv.Key, kv.Value); }
                foreach (var kv in r.Events.Made) if (ItemKind(kinds, kv.Key) == "food") Put(DdDish, kv.Key, kv.Value);
                foreach (var kv in r.Events.Planted) Put(DdPlant, kv.Key, kv.Value);
                Put(DdOwn, DdFelled, r.Events.Felled.Values.Sum(v => (double)v));
                Put(DdOwn, DdChop, r.Events.ChopHits.Values.Sum(v => (double)v));
                foreach (var kv in DayHistory.Family(r.Game, DayHistory.PlacedPrefix))
                    switch (PieceKind(kinds, kv.Key))
                    {
                        case "built": Put(DdBuilt, PieceTabValue(Ask(kinds.PieceTab, kv.Key)), kv.Value); break;
                        case "ground": Put(DdGround, kv.Key, kv.Value); break;
                        case "feast": Put(DdFeast, kv.Key, kv.Value); break;
                    }
                foreach (var kv in DayHistory.Family(r.Game, DayHistory.PickedPrefix))
                    if (kv.Key.StartsWith("$", StringComparison.Ordinal)) Put(DdFish, kv.Key, kv.Value);   // the game books a catch under the fish's name token (Fishing)
                foreach (var s in GameShared) if (r.Game.TryGetValue(s, out var v)) Put(DdGame, s, v);
                foreach (var kv in r.Events.AteFoodMadeBy) Put(DdAte, kv.Key, kv.Value);   // 0.8.5: the meals, as the record keeps them (Meals)
                foreach (var kv in r.Events.AteFromFeastOf) Put(DdFeastOf, kv.Key, kv.Value);
                if (r.Game.TryGetValue("DistanceSail", out var sail)) Put(DdVoyage, DdSail, sail);   // 0.8.5: Together's Sailed and Cargo carried, Everyone's Sailing and Cargo, on a fellow's days
                if (r.Game.TryGetValue("DistanceSailHelm", out var helm)) Put(DdVoyage, DdHelm, helm);
                foreach (var kv in r.Events.CargoMeters) Put(DdCargo, kv.Key, kv.Value);
            }
            var shared = new Dictionary<string, Dictionary<string, Dictionary<string, float>>>();
            foreach (var day in sum)
            {
                var fams = new Dictionary<string, Dictionary<string, float>>();
                foreach (var fam in day.Value)
                {
                    var parts = fam.Value.Select(kv => (kv.Key, v: Math.Round(kv.Value))).Where(x => x.v > 0).OrderByDescending(x => x.v).ThenBy(x => x.Key, StringComparer.Ordinal)
                                         .Take(DayDeedsParts).ToDictionary(x => x.Key, x => (float)x.v);
                    var rest = fam.Key == DdCargo ? Math.Round(fam.Value.Where(kv => !parts.ContainsKey(kv.Key)).Sum(kv => kv.Value)) : 0;
                    if (rest > 0) parts[DdCargoRest] = (float)rest;   // the cargo's total stays whole (DdCargoRest)
                    if (parts.Count > 0) fams[fam.Key] = parts;
                }
                if (fams.Count > 0) shared[day.Key] = fams;
            }
            return shared;
        }

        /// <summary>The key of the part names beside the days in "dayDeeds": family -> names, in the order a day's amounts follow.</summary>
        public const string DayDeedsNames = "parts";
        /// <summary>The most names one family keeps over the 30 days (the largest in all stay).</summary>
        public const int DayDeedsNamesMax = 96;

        /// <summary>
        /// "dayDeeds" as the snapshot writes it: {"parts":{"wood":["$item_wood","$item_finewood"]},"2026-10-10":{"wood":[120,0,30]}}. The names of
        /// each family once, in the order they first came (the oldest day first, so a name new today goes last and the earlier days keep theirs);
        /// each day the amounts in that order, 0 where the day has none, trailing zeros left out. A day with nothing is left out.
        /// </summary>
        public static void WriteDayDeeds(Json j, IDictionary<string, Dictionary<string, Dictionary<string, float>>> days)
        {
            if (days == null) return;
            var names = new Dictionary<string, List<string>>(); var totals = new Dictionary<string, double>();
            names[DdVoyage] = new List<string> { DdSail, DdHelm };   // always: the mark that this copy shares voyage and cargo days, also with nothing sailed (SharesVoyageDays)
            names[DdGame] = new List<string> { DayKillsPart };   // 0.9.1, always: the mark that this copy shares the foes defeated per day (SharesDayKills)
            foreach (var part in names[DdVoyage]) totals[DdVoyage + "|" + part] = 0;
            foreach (var day in days.OrderBy(d => d.Key, StringComparer.Ordinal))
                foreach (var fam in day.Value ?? new Dictionary<string, Dictionary<string, float>>())
                    foreach (var kv in (fam.Value ?? new Dictionary<string, float>()).OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    {
                        if (!names.TryGetValue(fam.Key, out var list)) names[fam.Key] = list = new List<string>();
                        if (!list.Contains(kv.Key)) list.Add(kv.Key);
                        totals.TryGetValue(fam.Key + "|" + kv.Key, out var t); totals[fam.Key + "|" + kv.Key] = t + kv.Value;
                    }
            foreach (var fam in names.Keys.ToList())   // a mod with hundreds of kinds: the largest in all stay, in their order
                if (names[fam].Count > DayDeedsNamesMax)
                {
                    var rest = names[fam].Contains(DdCargoRest);   // never cut: the cargo's rest
                    var keep = new HashSet<string>(names[fam].Where(n => n != DdCargoRest).OrderByDescending(n => totals[fam + "|" + n]).ThenBy(n => n, StringComparer.Ordinal).Take(DayDeedsNamesMax - (rest ? 1 : 0)));
                    if (rest) keep.Add(DdCargoRest);
                    names[fam] = names[fam].Where(keep.Contains).ToList();
                }
            j.Key(DayDeedsKey).Open();
            j.Key(DayDeedsNames).Open();
            foreach (var fam in names) j.Raw(fam.Key, "[" + string.Join(",", fam.Value.Select(Json.Q).ToArray()) + "]");
            j.Close();
            foreach (var day in days.OrderBy(d => d.Key, StringComparer.Ordinal))
            {
                var fams = (day.Value ?? new Dictionary<string, Dictionary<string, float>>()).Where(f => f.Value != null && names.ContainsKey(f.Key) && f.Value.Any(kv => kv.Value > 0)).ToList();
                if (fams.Count == 0) continue;
                j.Key(day.Key).Open();
                foreach (var fam in fams)
                {
                    var amounts = names[fam.Key].Select(n => fam.Value.TryGetValue(n, out var v) && v > 0 ? v : 0f).ToList();
                    while (amounts.Count > 0 && amounts[amounts.Count - 1] == 0) amounts.RemoveAt(amounts.Count - 1);
                    if (amounts.Count > 0) j.Raw(fam.Key, "[" + string.Join(",", amounts.Select(v => Json.F(v)).ToArray()) + "]");
                }
                j.Close();
            }
            j.Close();
        }

        /// <summary>A copy's "dayDeeds" read back (WriteDayDeeds), day -> family -> part -> amount; only real days, at most 40, a family's amounts
        /// matched to its names, only amounts above 0. null when the copy has none.</summary>
        public static Dictionary<string, Dictionary<string, Dictionary<string, float>>> ReadDayDeeds(Dictionary<string, object> o)
        {
            if (o == null) return null;
            var names = new Dictionary<string, List<string>>();
            if (o.TryGetValue(DayDeedsNames, out var n) && n is Dictionary<string, object> nd)
                foreach (var kv in nd.Take(16)) if (kv.Value is List<object> list) names[kv.Key] = list.Take(DayDeedsNamesMax).Select(x => x as string).ToList();
            var days = new Dictionary<string, Dictionary<string, Dictionary<string, float>>>();
            foreach (var day in o.Where(kv => kv.Value is Dictionary<string, object> && DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", Inv, DateTimeStyles.None, out _)).Take(40))
            {
                var fams = new Dictionary<string, Dictionary<string, float>>();
                foreach (var f in ((Dictionary<string, object>)day.Value).Take(16))
                {
                    if (!(f.Value is List<object> amounts) || !names.TryGetValue(f.Key, out var ks)) continue;
                    var parts = new Dictionary<string, float>();
                    for (int i = 0; i < amounts.Count && i < ks.Count; i++)
                        if (!string.IsNullOrEmpty(ks[i]) && amounts[i] is double v && v > 0 && Json.IsFinite((float)v)) parts[ks[i]] = (float)v;
                    if (parts.Count > 0) fams[f.Key] = parts;
                }
                days[day.Key] = fams;
            }
            return days;
        }

        /// <summary>0.8.5: a copy's "dayDeeds" names the voyage family (WriteDayDeeds always does): it shares km sailed and cargo per day, so a day
        /// without them is a day without sailing. false: a copy without it (the 0.8.5 test build, or older) shares no voyage days.</summary>
        public static bool SharesVoyageDays(Dictionary<string, object> o) =>
            o != null && o.TryGetValue(DayDeedsNames, out var n) && n is Dictionary<string, object> nd && nd.ContainsKey(DdVoyage);
        /// <summary>0.9.1: a copy's "dayDeeds" names the kill counter in its game family (WriteDayDeeds always does): a day without it is a day without
        /// foes defeated. false: a copy before 0.9.1 shares no foes defeated per day.</summary>
        public static bool SharesDayKills(Dictionary<string, object> o) =>
            o != null && o.TryGetValue(DayDeedsNames, out var n) && n is Dictionary<string, object> nd && nd.TryGetValue(DdGame, out var g) && g is List<object> gl && gl.Contains(DayKillsPart);

        /// <summary>A fellow's shared Deeds in a day window (their "dayDeeds"), family -> part -> amount over the window's days in the viewer's
        /// calendar (<paramref name="viewer"/>: your book, or the fellow's book read on this PC); null when their copy shares none (before 0.8.5).</summary>
        public static Dictionary<string, Dictionary<string, double>> FellowDeedsIn(PanelInput p, TimeWindow w, PanelInput viewer)
        {
            if (p?.DayDeeds == null || p.IsSelf || !IsDayWindow(w)) return null;
            DateTime from = DayFrom(viewer ?? p, w), to = DayTo(viewer ?? p, w);
            var sum = new Dictionary<string, Dictionary<string, double>>();
            foreach (var day in p.DayDeeds)
            {
                if (!DateTime.TryParseExact(day.Key, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var d) || d < from || d > to) continue;
                foreach (var fam in day.Value ?? new Dictionary<string, Dictionary<string, float>>())
                    foreach (var kv in fam.Value ?? new Dictionary<string, float>())
                    {
                        if (kv.Value <= 0) continue;
                        if (!sum.TryGetValue(fam.Key, out var s)) sum[fam.Key] = s = new Dictionary<string, double>();
                        s.TryGetValue(kv.Key, out var o); s[kv.Key] = o + kv.Value;
                    }
            }
            return sum;
        }

        /// <summary>
        /// A player's copy for km sailed, at the helm and cargo carried in a day window (Together's Sailed and Cargo carried, Everyone's Sailing and
        /// Cargo): yours from your day history (InWindow), a fellow's from the voyage family of their shared days (FellowDeedsWindow). null when the
        /// window cannot be told for them: <paramref name="gap"/> says why on their row, <paramref name="waits"/> is the older-version line when their
        /// Hearthwoven shares no voyage days yet (the 0.8.5 test build, or older), never read as "sailed nothing".
        /// </summary>
        internal static PanelInput VoyageDays(PanelInput p, TimeWindow w, PanelInput me, out string gap, out string waits)
        {
            gap = waits = null;
            if (p.IsSelf) { var own = InWindow(p, w); if (own == null) gap = "no days recorded yet" + GroupNotInSum; return own; }
            var c = p.DayVoyages ? FellowDeedsWindow(p, w, me) : null;
            if (c != null) return c;
            var older = p.DayDeeds != null || OlderThan(p, NeedsDayDeeds);   // a copy with Deeds days but no voyages is the 0.8.5 test build
            gap = (older ? "shares no days of this yet" : "shares no days") + GroupNotInSum;
            waits = older ? OlderLine(p, "their days fill in") : null;
            return null;
        }

        /// <summary>A fellow's Deeds days are open on their book (their copy shares them, FellowDeedsIn).</summary>
        public static bool FellowDeedsOpen(PanelInput input, TimeWindow w) => input != null && !input.IsSelf && IsDayWindow(w) && input.DayDeeds != null;

        /// <summary>
        /// A fellow's book in a day window for the Deeds pages: a copy whose tallies are the window's shared parts (FellowDeedsIn), built as InWindow
        /// builds your own, so each page's code draws its hero and first bar unchanged. Its DayView.Shared holds the parts, so a page leaves out
        /// the lists the parts cannot fill (DeedsSharedLine says which). null when their copy shares no Deeds days.
        /// </summary>
        public static PanelInput FellowDeedsWindow(PanelInput input, TimeWindow w, PanelInput viewer = null)
        {
            var s = FellowDeedsIn(input, w, viewer);
            var c = s == null ? null : SharedCopy(input, s, w, DayFrom(viewer ?? input, w), DayTo(viewer ?? input, w));
            if (c != null) c.Window.Whole = input;
            return c;
        }

        /// <summary>A fellow's copy whose tallies are shared Deeds parts <paramref name="s"/> (family -> part -> amount) of the days <paramref name="from"/>
        /// to <paramref name="to"/>: a day window's (FellowDeedsWindow), or one day's for the growth line (SharedGrowth).</summary>
        static PanelInput SharedCopy(PanelInput input, Dictionary<string, Dictionary<string, double>> s, TimeWindow w, DateTime from, DateTime to)
        {
            Dictionary<string, float> F(params string[] fams)
            {
                var d = new Dictionary<string, float>();
                foreach (var f in fams) if (s.TryGetValue(f, out var parts)) foreach (var kv in parts) d[kv.Key] = (float)kv.Value;
                return d;
            }
            var ev = new SessionEvents();
            foreach (var kv in F(DdWood, DdOre)) ev.PickedUp[kv.Key] = kv.Value;
            foreach (var kv in F(DdDish)) ev.Made[kv.Key] = kv.Value;
            foreach (var kv in F(DdPlant)) ev.Planted[kv.Key] = kv.Value;
            foreach (var kv in F(DdAte)) ev.AteFoodMadeBy[kv.Key] = kv.Value;
            foreach (var kv in F(DdFeastOf)) ev.AteFromFeastOf[kv.Key] = kv.Value;
            var own = F(DdOwn);
            if (own.TryGetValue(DdFelled, out var felled)) ev.Felled[SharedPart] = felled;
            if (own.TryGetValue(DdChop, out var chop)) ev.ChopHits[SharedPart] = chop;
            var c = input.ShallowCopy();
            c.Events = ev; c.DamageSinceInstall = new DamageTally(); c.BiomeSinceInstall = new BiomeTally(); c.BiomeFromUtc = null;
            c.Character = F(DdGame); c.PiecesPlaced = F(DdGround, DdFeast); c.Harvested = F(DdFish);
            if (s.TryGetValue(DdVoyage, out var voyage))   // 0.8.5: km sailed and at the helm, as the Voyages pages and Together read them
            {
                if (voyage.TryGetValue(DdSail, out var sail)) c.Character["DistanceSail"] = (float)sail;
                if (voyage.TryGetValue(DdHelm, out var helm)) c.Character["DistanceSailHelm"] = (float)helm;
            }
            foreach (var kv in F(DdCargo)) ev.CargoMeters[kv.Key == DdCargoRest ? CargoOtherItems : kv.Key] = kv.Value;   // the cargo carried, item by item (the rest as one)
            c.EnemyKills = null; c.ItemsPickedUp = new Dictionary<string, float>(ev.PickedUp); c.ItemsCrafted = new Dictionary<string, float>(ev.Made);
            c.Baseline = null; c.ExactAtBaseline = null; c.BaselineAt = null; c.Book = null;
            c.Window = new DayView { Window = w, From = from, To = to, Shared = s };
            return c;
        }

        /// <summary>
        /// A fellow's growth line beside a Deeds hero (GrowthLines: yours from your day history): each day from the first they share (at most
        /// GrowthDays back) to today, the page's own count of that day's shared parts (<paramref name="of"/>; Building the pieces per tab added up,
        /// the pieces themselves are not shared). null when their copy shares no Deeds days or under two days.
        /// </summary>
        static Block SharedGrowth(PanelInput input, string page, Func<PanelInput, double> of)
        {
            if (input?.DayDeeds == null || input.IsSelf) return null;
            var today = LocalToday(input);
            var shared = new Dictionary<DateTime, Dictionary<string, Dictionary<string, double>>>();
            foreach (var day in input.DayDeeds)
                if (DateTime.TryParseExact(day.Key, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var d) && d <= today)
                    shared[d] = (day.Value ?? new Dictionary<string, Dictionary<string, float>>()).ToDictionary(f => f.Key, f => f.Value.ToDictionary(kv => kv.Key, kv => (double)kv.Value));
            if (shared.Count == 0) return null;
            var back = today.AddDays(1 - GrowthDays); var first = shared.Keys.Min();
            var days = new List<DateTime>();
            for (var d = first < back ? back : first; d <= today; d = d.AddDays(1)) days.Add(d);
            if (days.Count < 2) return null;
            var values = days.Select(d =>
            {
                if (!shared.TryGetValue(d, out var s)) return 0;
                if (page == "building") return s.TryGetValue(DdBuilt, out var tabs) ? tabs.Values.Sum() : 0;
                return GrowthSafe(() => of(SharedCopy(input, s, TimeWindow.Today, d, d)));
            }).ToList();
            return SparkBlock(input, days, values);
        }

        /// <summary>
        /// Cooking's Who enjoyed in a day window (0.8.5): the book the window was cut from (DayView.Whole: its own records, so a dish's servings
        /// stay capped by what its maker made in all, MakerMade), its fellows each in the same window, from what they share per day: their meals
        /// by cook and dish, their feast servings by who set the feast out (no feast id in the day share, so FeastBook credits who set it out).
        /// Yourself among them (a fellow's book) from your day history. A fellow whose copy shares no days is left out (FoodDaysLines says so).
        /// null in a short window, or when the whole book is not known.
        /// </summary>
        public static PanelInput FoodBookIn(DayView v)
        {
            if (v == null || v.Short || v.Whole == null) return null;
            var book = v.Whole.ShallowCopy();
            book.Fellows = (v.Whole.Fellows ?? new List<PanelInput>()).Where(f => f != null)
                .Select(f => f.IsSelf ? DeedsWindow(f, v.Window) : FellowDeedsWindow(f, v.Window, v.Whole)).Where(f => f != null).ToList();
            return book;
        }

        /// <summary>
        /// What Cooking says in a day window about the fellows who enjoyed this player's food on All but share no days (FoodBookIn leaves them
        /// out): none of them shares days, the one line as before (NoDaysFood); else one line per such fellow whose Hearthwoven is older
        /// ("Tor's Hearthwoven is 0.8.3: what Tor ate of your food fills in once they update.").
        /// </summary>
        static List<string> FoodDaysLines(PanelInput all)
        {
            var fans = FoodRows(all).Select(r => r.fellow).ToList();
            var noDays = fans.Where(f => !f.IsSelf && f.DayDeeds == null).ToList();
            if (noDays.Count == 0) return new List<string>();
            if (noDays.Count == fans.Count) return new List<string> { NoDaysFood };
            return noDays.Where(f => OlderThan(f, NeedsDayDeeds)).Select(f => OlderLine(f, "what " + Name(f) + " ate of your food fills in")).ToList();
        }

        /// <summary>The parts of one family in a fellow's shared day window (DayView.Shared); empty when none.</summary>
        static Dictionary<string, double> SharedParts(PanelInput input, string fam) =>
            input?.Window?.Shared != null && input.Window.Shared.TryGetValue(fam, out var d) ? new Dictionary<string, double>(d) : new Dictionary<string, double>();

        /// <summary>Building in a fellow's shared day window: the pieces built and the Category row as one bar (the hammer's tabs, coloured as
        /// the filter's bar colours them); the pieces themselves are not shared.</summary>
        static void SharedBuilding(PanelInput input, PanelView view)
        {
            var tabs = SharedParts(input, DdBuilt);
            if (tabs.Values.Sum() <= 0) return;
            Add(view, Hero((N(tabs.Values.Sum()), Label1(tabs.Values.Sum(), "piece built", "pieces built"), SrcPc, null)));
            Add(view, SharedBar(Composition(SharedTabsTitle, tabs, k => k, k => (null, null), SrcPc, icon: k => null, note: "the hammer's tab"), "tab"));
        }
        public const string SharedTabsTitle = "By category", SharedKindsTitle = "By kind";

        /// <summary>Crafting's Kind row in a fellow's shared day window: the game's craft counters per kind, as one bar beside the hero they add up to.</summary>
        static Block SharedGearKinds(PanelInput input)
        {
            var kinds = GearKinds.Where(k => C(input, k.stat) > 0).ToDictionary(k => k.id, k => C(input, k.stat));
            return SharedBar(Composition(SharedKindsTitle, kinds, k => GearKinds.First(g => g.id == k).title, k => (null, null), SrcPc, icon: k => null), "kind");
        }

        /// <summary>A shared day's bar standing for a filter row (<paramref name="id"/>: the row's id, which the group's view reads): one part is
        /// no bar, its one row under the island's heading ("By category" over "Building 7"), as a fellow's one-kind bar is drawn (PanelModel.ListShown).</summary>
        static Block SharedBar(Block bar, string id)
        {
            if (bar == null) return null;
            bar.Id = id;
            if (bar.Items.Count == 1) bar.Tone = "single";
            return bar;
        }

        /// <summary>The plate's first line on a fellow's Deeds page in a window, whose numbers and from when (as Battle's PlateText): 10 min to 3 h
        /// "Edda, live", Session "Edda, since 21:40" (what reached this PC from them while you played), a day window and All whose copy and as
        /// of when (RecordedScope); null on your own book.</summary>
        public static string FellowDeedsScope(PanelInput input, TimeWindow w) =>
            input == null || input.IsSelf ? null : w == TimeWindow.Session ? Name(input) + ", " + SinceViewer(input) : IsShortWindow(w) ? Name(input) + ", " + GroupLive : RecordedScope(input);

        /// <summary>
        /// A fellow's Deeds page in a window, on the plate: whose numbers and since when (FellowDeedsScope); in a day window from their shared
        /// days, what those hold when the page lists more on All (DeedsSharedLine, left out when the window is empty) and that their day is their
        /// whole day (DayWindowLine, as Battle's). Nothing on your own book.
        /// </summary>
        static void FellowDayLines(PanelInput input, PanelInput day, string page, PanelView view, TimeWindow w)
        {
            if (input == null || input.IsSelf || day?.Window == null) return;
            var plate = PlateOf(view); if (plate == null) return;
            plate.Text = FellowDeedsScope(input, w);
            if (day.Window.Shared == null) return;
            var only = DeedsSharedLine(input, page);
            if (only != null && !plate.Items.Any(b => b.Kind == "empty")) plate.Items.Add(new Block { Kind = "note", Text = only });
            plate.Items.Add(new Block { Kind = "note", Text = DayWindowLine(w, Name(input)) });
        }

        /// <summary>
        /// "Edda's days are shared only for trees felled, axe hits and wood brought in." The one line of a fellow's Deeds page in a day window
        /// that says what the shared days hold, when the page lists more on All; null for a page whose day share is the whole page (Groundwork).
        /// 0.8.5: Voyages > Sailing and Cargo too (FellowVoyageLines), from the voyage family.
        /// </summary>
        public static string DeedsSharedLine(PanelInput input, string page)
        {
            string only;
            switch (page)
            {
                case "sailing": only = "km sailed and at the helm"; break;
                case "cargo": only = "cargo carried per item"; break;
                case "woodcutting": only = "trees felled, axe hits and wood brought in"; break;
                case "mining": only = "stone and ore brought in"; break;
                case "building": only = "pieces built per category"; break;
                case "crafting": only = "gear crafted per kind and upgrades"; break;
                case "cooking": only = "dishes cooked and feasts set out"; break;
                case "farming": only = "planted per crop and picked"; break;
                case "fishing": only = "fish hooked and caught"; break;
                case "taming": only = "tamed and petted"; break;
                default: return null;
            }
            return Name(input) + "'s days are shared only for " + only + ".";
        }
    }
}
