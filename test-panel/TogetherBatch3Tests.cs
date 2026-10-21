// 0.9.2 Together, batch 3 (Chapters/FeatsGroup3.cs, work/hearthwoven-0.9/FEAT-CATALOG-2.md "Together, batch 3"): each new group feat from a group
// built by hand (you, Edda and Tor who share, and a stale second copy of Edda that must not count twice) that also holds what it must not count,
// its tiers at their lines (one step under: not earned; on it: earned); the same number on a fellow's book; nothing alone; a fellow on an older
// Hearthwoven adds nothing where the copy needs Hearthwoven's own count, and the note says so; the sample shows some earned and some partway; the
// ledger's room. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class TogetherBatch3Tests
{
    static readonly DateTime Now = new DateTime(2026, 10, 19, 20, 0, 0, DateTimeKind.Utc);
    public static readonly string[] Ids = { "quarry", "woodpile", "fleet", "doorways", "garden", "everyland", "oldpowers", "felledtogether", "sharedtable", "longtable", "handtohand", "onemap" };
    /// <summary>The feats a fellow's copy adds to only through Hearthwoven's own count since install (an older version shares none of it).</summary>
    static readonly string[] SinceInstall = { "sharedtable", "longtable", "handtohand", "onemap" };

    static PanelInput Book(string name, long id, bool self) => new PanelInput
    {
        PlayerName = name, PlayerId = id, IsSelf = self, NowUtc = Now, ModVersion = self ? null : "0.9.2", SharedSinceInstall = !self, Events = new SessionEvents(),
        Feats = new FeatsLedger { Primed = self }, Character = new Dictionary<string, float>(), ItemsPickedUp = new Dictionary<string, float>(), PiecesPlaced = new Dictionary<string, float>(),
        EnemyKills = new Dictionary<string, float>(), KnownBiomes = new List<string>(), Fellows = new List<PanelInput>(),
    };

    /// <summary>Rowan's book with fellows (each fellow's book lists the others and Rowan, as their copies do).</summary>
    static PanelInput Group(PanelInput me, params PanelInput[] fellows)
    {
        me.Fellows = fellows.ToList();
        foreach (var f in fellows) f.Fellows = new[] { me }.Concat(fellows.Where(x => x != f)).ToList();
        return me;
    }

    static readonly string[] Lands = { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "Ocean" };
    static readonly string[] Powers = { "UsePowerEikthyr", "UsePowerElder", "UsePowerBonemass", "UsePowerModer", "UsePowerYagluth", "UsePowerQueen", "UsePowerAshlands" };
    static readonly string[] Bosses = { "$enemy_eikthyr", "$enemy_gdking", "$enemy_bonemass", "$enemy_dragon", "$enemy_goblinking", "$enemy_seekerqueen", "$enemy_fader" };

    /// <summary>
    /// A group whose feat reads exactly <c>v</c>: Rowan holds part, Edda the rest (a set: one thing they both hold), Tor only what must not count, and a
    /// stale second copy of Edda (her id) carries what she does. <c>fill(book, mine, eddas)</c> gives each their part.
    /// </summary>
    static PanelInput Make(double v, Action<PanelInput, PanelInput, PanelInput, int, int> fill)
    {
        var me = Book("Rowan", 11, true); var edda = Book("Edda", 77, false); var tor = Book("Tor", 88, false); var stale = Book("Edda", 77, false);
        int a = (int)Math.Floor(v / 2), b = (int)Math.Round(v) - a;
        fill(me, edda, tor, a, b); fill(Book("Rowan", 11, true), stale, Book("Tor", 88, false), a, b);   // the stale copy: Edda's part again, nothing else
        return Group(me, edda, tor, stale);
    }

    static void Add(IDictionary<string, float> d, string k, float v) { d.TryGetValue(k, out var o); d[k] = o + v; }

    /// <summary>Each feat: its group for a value, the step its number moves by.</summary>
    static readonly (string id, Func<double, PanelInput> make, double step)[] Feats =
    {
        ("quarry", v => Make(v, (me, e, tor, a, b) =>
        {
            if (a >= 0) { me.ItemsPickedUp["$item_stone"] = a; me.ItemsPickedUp["$item_copperore"] = 900; }
            e.ItemsPickedUp["$item_grausten"] = b; Add(e.Events.PickedUp, "$item_grausten", b / 2f);   // her exact count lies inside her record: once
            tor.ItemsPickedUp["$item_wood"] = 40; Add(tor.Events.Recovered, "$item_stone", 300);       // stone from pieces taken down never counts
        }), 1),
        ("woodpile", v => Make(v, (me, e, tor, a, b) =>
        {
            if (a >= 0) { me.ItemsPickedUp["$item_wood"] = a; me.ItemsPickedUp["$item_stone"] = 70; }
            e.ItemsPickedUp["$item_finewood"] = b; tor.ItemsPickedUp["$item_resin"] = 30; Add(tor.Events.Recovered, "$item_wood", 200);
        }), 1),
        ("fleet", v => Make(v, (me, e, tor, a, b) =>
        {
            if (a >= 0) { me.PiecesPlaced["$ship_karve"] = a; me.PiecesPlaced["$tool_cart"] = 4; }
            e.PiecesPlaced["$ship_longship"] = b; tor.PiecesPlaced["$piece_woodwall"] = 60;
        }), 1),
        ("doorways", v => Make(v, (me, e, tor, a, b) =>
        {
            if (a >= 0) { me.PiecesPlaced["$piece_portal"] = a; me.PiecesPlaced["$piece_woodwall"] = 9; }
            e.PiecesPlaced["$piece_portal_stone"] = b; tor.PiecesPlaced["$piece_workbench"] = 2;
        }), 1),
        ("garden", v => Make(v, (me, e, tor, a, b) =>
        {
            if (a >= 0) for (int k = 0; k < a; k++) me.PiecesPlaced["$piece_sapling_" + k] = 12;
            for (int k = Math.Max(0, a - 1); k < Math.Max(0, a - 1) + b + (a > 0 ? 1 : 0); k++) Add(e.Events.Planted, "$piece_sapling_" + k, 3);   // one kind Rowan planted too
            tor.PiecesPlaced["$piece_woodwall"] = 5; Add(tor.Events.Planted, "$piece_woodwall", 1);
        }), 1),
        ("everyland", v => Make(v, (me, e, tor, a, b) =>
        {
            if (a >= 0) me.KnownBiomes = Lands.Take(a).Concat(new[] { "None" }).ToList();
            e.KnownBiomes = Lands.Skip(Math.Max(0, a - 1)).Take(b + (a > 0 ? 1 : 0)).ToList();   // one land Rowan found too
        }), 1),
        ("oldpowers", v => Make(v, (me, e, tor, a, b) =>
        {
            if (a >= 0) { for (int k = 0; k < a; k++) me.Character[Powers[k]] = 2; me.Character["SetPowerQueen"] = 1; }
            foreach (var p in Powers.Skip(Math.Max(0, a - 1)).Take(b + (a > 0 ? 1 : 0))) e.Character[p] = 1;
            tor.Character["SetPowerAshlands"] = 1; tor.Character["UseGuardianPower"] = 4;   // set at a stone, or the guardian power counter: not a power called on
        }), 1),
        ("felledtogether", v => Make(v, (me, e, tor, a, b) =>
        {
            int n = (int)Math.Round(v);
            if (a >= 0) { for (int k = 0; k < n; k++) me.EnemyKills[Bosses[k]] = 1; me.EnemyKills["$enemy_troll"] = 30; }
            for (int k = 0; k < n; k++) e.EnemyKills[Bosses[k]] = 2;
            if (n < Bosses.Length) e.EnemyKills[Bosses[n]] = 1;   // one Edda felled without anyone else who shares: once, even with her stale copy
            if (n + 1 < Bosses.Length) tor.EnemyKills[Bosses[n + 1]] = 1;
        }), 1),
        ("sharedtable", v => Make(v, (me, e, tor, a, b) =>
        {
            if (a >= 0) { Add(me.Events.AteFoodMadeBy, "Edda|Bread", a); Add(me.Events.AteFoodMadeBy, "Rowan|FishWraps", 9); }
            Add(e.Events.AteFoodMadeBy, "Gunn|CookedMeat", b); Add(e.Events.AteFoodMadeBy, "Edda|Bread", 5);   // a cook who does not share still fed her; her own bread does not count
        }), 1),
        ("longtable", v => Make(v, (me, e, tor, a, b) =>
        {
            int n = (int)Math.Round(v);
            for (int k = 0; k < n; k++) { if (a >= 0) Add(me.Events.AteFromFeastAt, "11|FeastMeadows|1:" + k, 2); Add(tor.Events.AteFromFeastAt, "11|FeastMeadows|1:" + k, 1); }
            Add(e.Events.AteFromFeastAt, "77|FeastBlackforest|9:9", 4);   // a feast only Edda enjoyed (her stale copy too): not shared
            if (a >= 0) Add(me.Events.AteFromFeastOf, "11|FeastMeadows", 5);
        }), 1),
        ("handtohand", v => Make(v, (me, e, tor, a, b) =>
        {
            if (a >= 0) { for (int k = 0; k < a; k++) Add(me.Events.EquippedGearMadeBy, "Edda|Item" + k, 3); Add(me.Events.EquippedGearMadeBy, "Rowan|AxeBronze", 4); }
            for (int k = 0; k < b; k++) Add(e.Events.EquippedGearMadeBy, "Tor|Item" + k, 1);   // put on once or many times: once
            Add(e.Events.EquippedGearMadeBy, "Edda|ShieldWood", 2);
        }), 1),
        ("onemap", v => Make(v, (me, e, tor, a, b) =>
        {
            if (a >= 0) Add(me.Events.MapShared, "piece_cartographytable", 2);
            Add(e.Events.MapShared, "piece_cartographytable", 1);
            if (v >= 1) Add(tor.Events.MapShared, "piece_cartographytable", 1);
        }), 1),
    };

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- every new feat: the group's number from the copies, each person once, its tiers at their lines ----------
        foreach (var t in Feats)
        {
            var d = PanelModel.FeatById(t.id);
            if (d == null || !d.Group) { Check(false, "together batch 3: " + t.id + " is a group feat in the table"); continue; }
            var bad = new List<string>();
            for (int k = 0; k < d.Tiers.Length; k++)
            {
                var at = t.make(d.Tiers[k]); var under = t.make(d.Tiers[k] - t.step);
                var vAt = PanelModel.FeatValue(d, at); var vUnder = PanelModel.FeatValue(d, under);
                if (Math.Abs(vAt - d.Tiers[k]) > 1e-6) bad.Add("reads " + vAt + " for " + d.Tiers[k]);
                if (PanelModel.FeatTierOf(d, vAt) != k + 1) bad.Add("tier " + (k + 1) + " not earned at " + d.Tiers[k]);
                if (PanelModel.FeatTierOf(d, vUnder) != k) bad.Add("tier " + (k + 1) + " earned at " + vUnder);
                var edda = at.Fellows[0];
                if (Math.Abs(PanelModel.FeatValue(d, edda) - vAt) > 1e-6) bad.Add("Edda's book reads " + PanelModel.FeatValue(d, edda) + ", Rowan's " + vAt);
            }
            var none = t.make(0);
            if (PanelModel.FeatValue(d, none) != 0) bad.Add("an empty group reads " + PanelModel.FeatValue(d, none));
            Check(bad.Count == 0, "together batch 3: " + d.Name + " sums everyone who shares, each once (Edda's stale second copy adds nothing), tiers " +
                  string.Join(" / ", d.Tiers.Select(PanelModel.Number)) + " at their lines, the same on a fellow's book" + (bad.Count > 0 ? ": " + string.Join("; ", bad) : ""));
        }
        Check(Ids.All(id => PanelModel.FeatById(id)?.Group == true) && Feats.Select(f => f.id).OrderBy(x => x).SequenceEqual(Ids.OrderBy(x => x)) && PanelModel.FeatDefs.Count(d => d.Group) == 16 + Ids.Length + 1,   // + 1: Clad as One (ArmorWornTests)
              "together batch 3: " + Ids.Length + " group feats added, each tested here (" + PanelModel.FeatDefs.Count(d => d.Group) + " Together, " + PanelModel.FeatDefs.Length + " feats in all)");

        // ---------- the room: the ledger keeps every feat, 16 or more spare ----------
        {
            Check(PanelModel.FeatDefs.Length + 16 <= FeatsLedger.MaxFeats && FeatsLedger.MaxFeats == 160,
                  "together batch 3: the ledger holds 160 feats (was 128): " + PanelModel.FeatDefs.Length + " in the table, " + (FeatsLedger.MaxFeats - PanelModel.FeatDefs.Length) + " spare");
            var l = new FeatsLedger { Primed = true };
            foreach (var d in PanelModel.FeatDefs) l.Earn(d.Id, 1, new FeatMoment { Utc = Now });
            var j = new Json().Open(); l.WriteTo(j); l.WriteShared(j, "shared"); j.Close();
            var root = MiniJson.Parse(j.ToString()) as Dictionary<string, object>;
            var back = FeatsLedger.ReadFrom(MiniJson.Obj(root, "feats")); var theirs = FeatsLedger.ReadShared(MiniJson.Obj(root, "shared"));
            Check(PanelModel.FeatDefs.All(d => back.Tier(d.Id) == 1 && theirs.Tier(d.Id) == 1),
                  "together batch 3: a ledger with every one of the " + PanelModel.FeatDefs.Length + " feats earned survives its save and load, and the shared copy");
        }

        // ---------- alone: a Together feat needs two or more who share ----------
        {
            var bad = new List<string>();
            foreach (var t in Feats)
            {
                var d = PanelModel.FeatById(t.id); var g = t.make(d.Tiers[d.Tiers.Length - 1]);
                var me = g; me.Fellows = new List<PanelInput>();
                if (PanelModel.FeatValue(d, me) != 0) bad.Add(t.id + " reads " + PanelModel.FeatValue(d, me));
                if (PanelModel.FeatHint(me, d, false) != PanelModel.NeedsFellows) bad.Add(t.id + " hint " + PanelModel.FeatHint(me, d, false));
            }
            Check(bad.Count == 0, "together batch 3: alone (no fellow who shares) each reads 0 and its card says no fellow shares yet, never one player's own total" + (bad.Count > 0 ? ": " + string.Join("; ", bad) : ""));
        }

        // ---------- a fellow on an older Hearthwoven ----------
        {
            var bad = new List<string>();
            foreach (var t in Feats)
            {
                var d = PanelModel.FeatById(t.id);
                var g = t.make(d.Tiers[0]);
                var others = g.Fellows.Where(f => f.PlayerName != "Tor").ToArray();
                var before = PanelModel.FeatValue(d, Group(g, others));
                // Tor on 0.5: a copy with his character record but none of Hearthwoven's counts since install (his session's measured tallies only, empty here)
                var old = Book("Tor", 88, false); old.ModVersion = "0.5.0"; old.SharedSinceInstall = false;
                old.ItemsPickedUp["$item_stone"] = 10; old.ItemsPickedUp["$item_wood"] = 10; old.PiecesPlaced["$ship_raft"] = 1; old.PiecesPlaced["$piece_portal"] = 1;
                old.PiecesPlaced["$piece_sapling_old"] = 1; old.KnownBiomes = new List<string> { "DeepNorth" }; old.Character["UsePowerAshlands"] = 1;
                var withOld = Group(g, others.Concat(new[] { old }).ToArray());
                var after = PanelModel.FeatValue(d, withOld);
                var counted = after - before;
                if (SinceInstall.Contains(t.id))
                {
                    if (t.id == "onemap") { if (after != 0) bad.Add("onemap earned with an older fellow who shares no map"); }
                    else if (Math.Abs(counted) > 1e-6) bad.Add(t.id + " moved by " + counted);
                    if ((d.Caveat ?? "").IndexOf("older version adds nothing", StringComparison.Ordinal) < 0) bad.Add(t.id + " caveat does not say it: " + d.Caveat);
                }
                else if (counted <= 0 && t.id != "felledtogether") bad.Add(t.id + ": the older copy's record should still count (" + counted + ")");
            }
            Check(bad.Count == 0, "together batch 3: a fellow on Hearthwoven 0.5 adds nothing to the feats that need Hearthwoven's own count (The Shared Table, The Long Table, Hand to Hand, One Map) and their note says so; " +
                  "their character record still counts for the rest (stone, wood, ships, portals, plantings, lands, powers)" + (bad.Count > 0 ? ": " + string.Join("; ", bad) : ""));
        }

        // ---------- on the page, in their order, and pictures that name no later land ----------
        {
            var g = Feats.First(f => f.id == "quarry").make(20000);
            var page = PanelModel.Build(g, new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = PanelModel.FeatsTogetherId } });
            var ids = PanelModel.VisibleFeats(page).Select(c => c.Id).ToList();
            Check(Ids.All(ids.Contains) && ids.IndexOf("quarry") < ids.IndexOf("onemap") && ids.Last() == "manycrafts" && Ids.All(id => PanelModel.FeatById(id).Biome == null),
                  "together batch 3: all twelve on the Together page (no land gate), in the table's order, the variety feat still last: " + string.Join(", ", ids));
            var said = string.Join(" ", Ids.Select(PanelModel.FeatById).SelectMany(d => new[] { d.Name, d.Honours, d.Caveat, PanelModel.FeatIcon(d) }.Concat(d.Tiers.Select(d.Rule)).Concat(d.Tiers.Select(d.Brief)))).ToLowerInvariant();
            Check(!new[] { "black marble", "grausten", "drakkar", "yggdrasil", "ashwood", "eikthyr", "elder", "bonemass", "moder", "yagluth", "queen", "fader", "mistlands", "ashlands", "deep north", "friend", " ate ", "first to", " most" }.Any(w => said.Contains(w)),
                  "together batch 3: no word names a later land's stone, wood or boss, no friend, no race or most");
        }

        // ---------- the sample (previews): some earned, some partway ----------
        {
            var full = PanelSample.Full(Now);
            var rows = Ids.Select(id => { var d = PanelModel.FeatById(id); var v = PanelModel.FeatValue(d, full); return (id, v, tier: PanelModel.FeatTierOf(d, v), first: d.Tiers[0]); }).ToList();
            System.Console.WriteLine("INFO together batch 3 sample: " + string.Join(", ", rows.Select(r => r.id + " " + r.v + " (" + r.tier + ")")));
            Check(rows.Count(r => r.tier > 0) >= 4 && rows.Count(r => r.tier == 0 && r.v > 0) >= 3,
                  "together batch 3: the sample group shows some earned (" + rows.Count(r => r.tier > 0) + ") and some partway (" + rows.Count(r => r.tier == 0 && r.v > 0) + ")");
        }
        return fails;
    }
}
