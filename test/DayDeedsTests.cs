// 0.8.5 (v085-groupdeeds): fellows share their Deeds per day ("dayDeeds", PanelModel.DayDeeds), per family the parts each Deeds page's hero and
// first bar show, nested per day. The bytes a typical and a busy month add to a realistic shared copy; a live update of a busy evening passes the
// 0.8.1 server's own checks in one message and carries only today's parts; a copy reads back what was sent, an older one has none, a hostile one
// only real days. Fictional players and evenings.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class DayDeedsTests
{
    static readonly DateTime Today = new DateTime(2026, 10, 10, 21, 0, 0);   // local evenings

    // the game data that sorts the tokens, as a player's own pages have it (wood and ore by the vanilla lists, the piece kinds by name)
    static readonly string[] Dishes = { "$item_cookedmeat", "$item_queensjam", "$item_carrotsoup", "$item_deerstew", "$item_sausages", "$item_turnipstew", "$item_shocklatesmoothie", "$item_blackSoup",
                                        "$item_serpentstew", "$item_fishcooked", "$item_onionsoup", "$item_breadbaked", "$item_lox_pie", "$item_wolfjerky" };
    const string NewDish = "$item_mushroomomelette";   // a dish first cooked tonight: its name joins the copy's names in a live update
    static readonly string[] Gear = { "$item_knife_flint", "$item_bow_fine", "$item_arrow_fire", "$item_helmet_bronze", "$item_chest_iron" };
    static readonly string[] Wood = { "$item_wood", "$item_finewood", "$item_roundlog", "$item_elderbark", "$item_yggdrasilwood" };
    static readonly string[] Ores = { "$item_stone", "$item_copperore", "$item_tinore", "$item_ironscrap", "$item_silverore", "$item_blackmetalscrap", "$item_crystal" };
    static readonly string[] Other = { "$item_resin", "$item_feathers", "$item_raspberries", "$item_thistle", "$item_leatherscraps", "$item_trophy_boar" };
    static readonly Dictionary<string, string> Tabs = new Dictionary<string, string>();
    static readonly string[] Ground = { "$piece_levelground", "$piece_pavedroad", "$piece_cultivate", "$piece_raise", "$piece_path" };
    static readonly Dictionary<string, string> Crops = new Dictionary<string, string>
    {
        ["$piece_sapling_carrot"] = "Carrot", ["$piece_sapling_turnip"] = "Turnip", ["$piece_sapling_barley"] = "Barley", ["$piece_sapling_flax"] = "Flax", ["$piece_sapling_onion"] = "Onion",
    };
    static readonly string[] Cargo = { "$item_wood", "$item_stone", "$item_ironscrap", "$item_coal", "$item_copperore", "$item_tin", "$item_finewood", "$item_blackmetalscrap", "$item_silverore", "$item_flametal_ore" };
    static readonly string[] Fish = { "$animal_fish1", "$animal_fish2", "$animal_fish4", "$animal_fish5", "$animal_fish8" };
    // the meals as the record keeps them: the cook the game writes on the food ("unknown" for berries and the like) and the dish's prefab
    static readonly string[] Cooks = { "Rowan", "Edda", "Tor", "Finch", "unknown" };
    static readonly string[] Eaten = { "CookedMeat", "CarrotSoup", "QueensJam", "Sausages", "DeerStew", "TurnipStew", "SerpentStew", "FishWraps", "Raspberry", "Blueberries", "Honey",
                                       "MushroomOmelette", "MagicallyStuffedShroom", "MeatPlatter", "HoneyGlazedChicken", "FishAndBread" };

    static PanelInput Kinds()
    {
        var tabs = new[] { "Building", "Furniture", "Crafting", "Misc", "Stonecutter", "Heat" };
        for (int i = 0; i < 60; i++) Tabs["$piece_kind" + i] = tabs[i % tabs.Length];
        return new PanelInput
        {
            IsSelf = true,
            ItemKind = t => Dishes.Contains(t) || t == NewDish ? "food" : Gear.Contains(t) ? "gear" : "other",
            PieceTab = t => t != null && Tabs.TryGetValue(t, out var tab) ? tab : null,
        };
    }

    enum Kind { Typical, Busy, Worst }

    // one evening's deeds as a day row holds them: what Hearthwoven counted (events) and the game's counters' growth (placed and picked per
    // token, the counters). Busy has the shape of real evenings of the group (three days of a player's own day history, read on their PC in
    // October 2026): wood of three kinds and trees felled, a little stone and ore, ten pieces over four tabs, a few dishes, two to four crops
    // planted, now and then a piece of gear or a fish. Typical: two or three of those an evening. Worst: everything, most kinds, every day
    static DayHistory.Row Evening(Random rnd, DateTime day, Kind kind)
    {
        var r = new DayHistory.Row { Period = DayHistory.DayKey(day) };
        var ev = r.Events;
        bool worst = kind == Kind.Worst;
        void Pick(string[] from, int kinds, int lo, int hi) { foreach (var t in from.OrderBy(_ => rnd.Next()).Take(kinds)) SessionEvents.Add(ev.PickedUp, t, rnd.Next(lo, hi)); }
        void Game(string key, int lo, int hi) => SessionEvents.Add(r.Game, key, rnd.Next(lo, hi));
        bool Does(int typical, int busy) => rnd.Next(10) < (worst ? 10 : kind == Kind.Busy ? busy : typical);
        int Of(int typical, int busy, int most) => worst ? most : kind == Kind.Busy ? busy : typical;
        Pick(Other, Of(2, 5, 6), 1, 40);   // what the day also picked up (not shared: not wood or ore)
        if (Does(5, 8)) { Pick(Wood, Of(2, 3, 5), 20, 700); SessionEvents.Add(ev.Felled, "Oak1", rnd.Next(2, 30)); SessionEvents.Add(ev.ChopHits, "Oak1", rnd.Next(30, 400)); }
        if (Does(4, 7)) { Pick(Ores, Of(2, 2, 6), 3, 2100); SessionEvents.Add(ev.PickaxeHits, "rock4_copper", rnd.Next(20, 300)); }
        if (Does(4, 6)) foreach (var t in Tabs.Keys.OrderBy(_ => rnd.Next()).Take(Of(6, 10, 40))) Game(DayHistory.PlacedPrefix + t, 1, 30);
        if (Does(3, 4)) foreach (var t in Ground.OrderBy(_ => rnd.Next()).Take(Of(1, 2, 5))) Game(DayHistory.PlacedPrefix + t, 5, 250);
        if (Does(4, 6)) { foreach (var t in Dishes.OrderBy(_ => rnd.Next()).Take(Of(3, 4, 10))) SessionEvents.Add(ev.Made, t, rnd.Next(1, 12)); if (rnd.Next(worst ? 1 : 6) == 0) Game(DayHistory.PlacedPrefix + "$piece_feast_mistlands", 1, 3); }
        if (Does(3, 4)) foreach (var t in Gear.OrderBy(_ => rnd.Next()).Take(Of(1, 2, 5))) { SessionEvents.Add(ev.Made, t, rnd.Next(1, 4)); Game(rnd.Next(2) == 0 ? "CraftWeapon" : "CraftArmor", 1, 3); Game("Upgrades", 0, 3); }
        if (Does(3, 5))
        {
            foreach (var p in Crops.Keys.OrderBy(_ => rnd.Next()).Take(Of(2, 3, 5))) { SessionEvents.Add(ev.Planted, p, rnd.Next(10, 1200)); Game(DayHistory.PickedPrefix + Crops[p], 5, 800); }
            Game(DayHistory.PickedPrefix + "Raspberry", 1, 30); Game("HarvestCrop", 7, 1500);
        }
        if (Does(1, 2)) { foreach (var f in Fish.OrderBy(_ => rnd.Next()).Take(Of(1, 2, 4))) Game(DayHistory.PickedPrefix + f, 1, 6); Game("FishHooked", 3, 12); Game("FishCaught", 1, 8); Game("FishLost", 0, 4); }
        if (Does(1, 1)) { Game("CreatureTamed", 1, 3); Game("TamedPetting", 1, 20); Game("TamedCommand", 1, 9); }
        // the meals: about four kinds an evening, mostly the player's own cooking and a fellow's, now and then a feast (three real evenings: 4 or 5 kinds)
        if (Does(7, 10)) foreach (var dish in Eaten.OrderBy(_ => rnd.Next()).Take(Of(3, 4, 8))) SessionEvents.Add(ev.AteFoodMadeBy, Cooks[rnd.Next(worst ? Cooks.Length : 3)] + "|" + dish, rnd.Next(1, 6));
        if (Does(2, 4)) { var placer = new[] { "880001", "880002" }[rnd.Next(2)]; var feast = new[] { "FeastMeadows", "FeastBlackforest", "FeastMistlands" }[rnd.Next(3)];
                          var n = rnd.Next(1, 6); SessionEvents.Add(ev.AteFromFeastOf, placer + "|" + feast, n); SessionEvents.Add(ev.AteFromFeastAt, placer + "|" + feast + "|" + placer + ":" + rnd.Next(10, 99), n); }
        foreach (var s in new[] { "DistanceWalk", "DistanceRun", "Jumps", "MineHits", "Tree" }) Game(s, 10, 3000);   // counters no Deeds day share reads
        // 0.8.5 (v085-together): the voyage days, km sailed (part at the helm) and the cargo per item, an evening with a ship or a cart now and then;
        // their own random, so the deeds above stay the evenings they were
        var vr = new Random(day.DayOfYear * 7 + (int)kind);
        if (vr.Next(10) < (worst ? 10 : kind == Kind.Busy ? 5 : 3))
        {
            SessionEvents.Add(r.Game, "DistanceSail", vr.Next(400, 12000)); SessionEvents.Add(r.Game, "DistanceSailHelm", vr.Next(0, 6000));
            foreach (var t in Cargo.OrderBy(_ => vr.Next()).Take(worst ? 10 : kind == Kind.Busy ? 4 : 2)) SessionEvents.Add(ev.CargoMeters, t, vr.Next(2000, 900000));
        }
        return r;
    }

    // a month of play: three evenings a week (typical) or every day (busy, worst), each saved as it ends
    static DayHistory Month(Kind kind, int seed)
    {
        var rnd = new Random(seed); var h = new DayHistory { From = Today.Date.AddDays(-34) };
        for (var d = Today.AddDays(-34); d.Date < Today.Date; d = d.AddDays(1))
        {
            if (kind == Kind.Typical && d.DayOfWeek != DayOfWeek.Tuesday && d.DayOfWeek != DayOfWeek.Friday && d.DayOfWeek != DayOfWeek.Saturday) continue;
            h.Rows.Add(Evening(rnd, d, kind));
        }
        return h;
    }

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
        var kinds = Kinds();
        var typical = Month(Kind.Typical, 3); var busy = Month(Kind.Busy, 5); var worst = Month(Kind.Worst, 7);
        var tonight = Evening(new Random(9), Today, Kind.Busy);   // today's running session, not saved yet: the live part

        // ---------- the bytes: a realistic shared copy (as DayTypesTests builds it) with and without the Deeds days ----------
        var rnd = new Random(11);
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats();
        foreach (PlayerStatType t in Enum.GetValues(typeof(PlayerStatType))) stats[0][t] = (float)Math.Round(rnd.NextDouble() * 5000, 1);
        for (int i = 0; i < 251; i++) stats[0].m_itemPickupStats["$item_kind" + i] = rnd.Next(1, 900);
        for (int i = 0; i < 99; i++) stats[0].m_itemCraftStats["$item_made" + i] = rnd.Next(1, 60);
        for (int i = 0; i < 385; i++) stats[0].m_piecesPlacedStats["$piece_kind" + i] = rnd.Next(1, 400);
        var skills = Enumerable.Range(0, 28).Select(i => new Snapshot.SkillInfo { Name = "Skill" + i, Level = (float)Math.Round(rnd.NextDouble() * 70, 3), Accumulator = (float)Math.Round(rnd.NextDouble() * 50, 2) }).ToList();
        var dmg = new DamageTally(); var log = new EventLog(); var ev = new SessionEvents();
        var t0 = new DateTime(2026, 10, 10, 19, 0, 0, DateTimeKind.Utc);
        var hit = new HitData.DamageTypes { m_slash = 18.5f, m_fire = 4f };
        for (int m = 0; m < 90; m++) { log.AddDamage(t0.AddMinutes(m), "BlackForest", true, "Greydwarf", "Swords", hit); dmg.AddDealt("Greydwarf", "Swords", hit); }
        // meals false: the share without its meal families (what the meals cost is the difference)
        string Full(DayHistory h, DayHistory.Row pend, bool deeds, string copyId = "s1-1", string[] leaveOut = null) =>
            Snapshot.Build("0.8.5", 4242, "Edda", stats, skills, "Midgard", dmg, "s1", ev, log, true, ev, dmg, null, null, null,
                           h?.DealtByDay(Today, 30, pend), new[] { "Meadows", "BlackForest" }, copyId, dayDealt: h?.DayDealt(Today, 30, pend),
                           dayDeeds: !deeds ? null : leaveOut == null ? PanelModel.DayDeeds(h, Today, 30, pend, kinds)
                                     : PanelModel.DayDeeds(h, Today, 30, pend, kinds).ToDictionary(d => d.Key, d => d.Value.Where(f => !leaveOut.Contains(f.Key)).ToDictionary(f => f.Key, f => f.Value)));
        var voyageFams = new[] { PanelModel.DdVoyage, PanelModel.DdCargo };
        int Packed(string json) => Transport.Pack(GroupShare.SharedCopy(json)).Length;
        int baseTyp = Packed(Full(typical, null, false)), withTyp = Packed(Full(typical, null, true));
        int baseBusy = Packed(Full(busy, tonight, false)), withBusy = Packed(Full(busy, tonight, true));
        int baseWorst = Packed(Full(worst, null, false)), withWorst = Packed(Full(worst, null, true));
        // 0.8.5: the voyages (km sailed, at the helm, the cargo per item) measured on their own; the meals beside the deeds without them, as before
        int voyBusy = withBusy - Packed(Full(busy, tonight, true, leaveOut: voyageFams)), voyTyp = withTyp - Packed(Full(typical, null, true, leaveOut: voyageFams));
        int mealsTyp = withTyp - voyTyp - Packed(Full(typical, null, true, leaveOut: PanelModel.DdMeals.Concat(voyageFams).ToArray())),
            mealsBusy = withBusy - voyBusy - Packed(Full(busy, tonight, true, leaveOut: PanelModel.DdMeals.Concat(voyageFams).ToArray()));
        int Raw(string json) => MiniJson.Write(MiniJson.Obj(MiniJson.Parse(json) as Dictionary<string, object>, PanelModel.DayDeedsKey)).Length;
        int Parts(DayHistory h, DayHistory.Row pend) => PanelModel.DayDeeds(h, Today, 30, pend, kinds).Sum(d => d.Value.Sum(f => f.Value.Count));
        var clock = Stopwatch.StartNew(); for (int i = 0; i < 20; i++) PanelModel.DayDeeds(busy, Today, 30, tonight, kinds); var ms = clock.Elapsed.TotalMilliseconds / 20;
        System.Console.WriteLine($"INFO share: a realistic shared copy is {baseTyp} bytes packed; dayDeeds adds {withTyp - baseTyp} bytes packed for a typical month ({PanelModel.DayDeeds(typical, Today, 30, null, kinds).Count} days played, " +
                                 $"{Parts(typical, null)} parts, {Raw(Full(typical, null, true))} bytes of JSON) and {withBusy - baseBusy} for a busy one ({PanelModel.DayDeeds(busy, Today, 30, tonight, kinds).Count} days, " +
                                 $"{Parts(busy, tonight)} parts, {Raw(Full(busy, tonight, true))} bytes of JSON); the worst month {withWorst - baseWorst} ({Parts(worst, null)} parts); " +
                                 $"of these the meals (cook and dish, feast servings by who set the feast out) are {mealsTyp} typical, {mealsBusy} busy, the voyages (km sailed, at the helm, cargo per item) {voyBusy} busy; " +
                                 $"working it out for the busy month takes {ms:0.00} ms");
        Check(withTyp - baseTyp <= 1024 + 512 && withBusy - baseBusy - mealsBusy - voyBusy <= 2048 && mealsBusy <= 614 && voyBusy <= 614 && withWorst - baseWorst <= 6 * 1024,
              "DD bytes: the Deeds days add at most 1.5 KB packed to a realistic shared copy for a typical month (+" + (withTyp - baseTyp) + "); a busy one, a real evening's deeds every day, " +
              "at most 2 KB for the deeds (+" + (withBusy - baseBusy - mealsBusy - voyBusy) + "), at most 0.6 KB for its meals per cook and dish and its feast servings (+" + mealsBusy + ") and at most " +
              "0.6 KB for its voyages, km sailed and the cargo per item (+" + voyBusy + "); at most 6 KB for the worst, most kinds of everything every day (+" + (withWorst - baseWorst) + ")");

        // ---------- the live update: two minutes of a busy evening through the 0.8.1 server's checks, one message ----------
        var baseShared = GroupShare.SharedCopy(Full(busy, tonight, true));   // the last full copy holds the evening so far
        var grow = new DayHistory.Row { Period = DayHistory.DayKey(Today.Date) }; grow.AddAll(tonight);
        var made = new List<LiveDelta.Made>(); var refused = new List<string>();
        for (int step = 1; step <= 12; step++)   // every 10 s: more wood and stone, a piece or two, a dish, a crop planted and picked
        {
            SessionEvents.Add(grow.Events.PickedUp, "$item_wood", 12); SessionEvents.Add(grow.Events.PickedUp, "$item_stone", 9); SessionEvents.Add(grow.Events.Felled, "Birch1", 1);
            SessionEvents.Add(grow.Events.ChopHits, "Birch1", 6); SessionEvents.Add(grow.Game, DayHistory.PlacedPrefix + "$piece_kind" + (step % 7), 2);
            SessionEvents.Add(grow.Events.Made, step < 6 ? Dishes[step % Dishes.Length] : NewDish, 1); SessionEvents.Add(grow.Events.Planted, "$piece_sapling_carrot", 5);
            SessionEvents.Add(grow.Game, DayHistory.PickedPrefix + "Carrot", 3); SessionEvents.Add(grow.Game, "HarvestCrop", 3);
            var now = GroupShare.SharedCopy(Full(busy, grow, true));
            var m = LiveDelta.Make(baseShared, now, "s1-1", step); made.Add(m);
            if (m.Text == null || !ServerIntake.IsJsonObject(m.Text) || !LiveDelta.Read(m.Text, out _, out _) || LiveDelta.Private(m.Text) || LiveDelta.Identity(m.Text) || m.TooBig)
                refused.Add("step " + step + (m.TooBig ? " too big" : " refused"));
        }
        var last = made.Last();
        var paths = (MiniJson.Obj(MiniJson.Parse(last.Text) as Dictionary<string, object>, "set") ?? new Dictionary<string, object>()).Keys.Where(k => k.StartsWith("/" + PanelModel.DayDeedsKey + "/")).ToList();
        var today = paths.Where(p => p.StartsWith("/" + PanelModel.DayDeedsKey + "/" + DayHistory.DayKey(Today.Date))).ToList();
        var names = paths.Where(p => p.StartsWith("/" + PanelModel.DayDeedsKey + "/" + PanelModel.DayDeedsNames + "/")).ToList();
        var applied = LiveDelta.Apply(FellowIds.WithPlatform(baseShared, "76561198000000042"), last.Text);
        var read = applied == null ? null : PanelInput.FromSnapshot(applied);
        var sent = PanelModel.DayDeeds(busy, Today, 30, grow, kinds);
        bool Same(Dictionary<string, Dictionary<string, Dictionary<string, float>>> a, Dictionary<string, Dictionary<string, Dictionary<string, float>>> b) =>
            a != null && b != null && a.Count == b.Count && a.All(d => b.TryGetValue(d.Key, out var x) && d.Value.Count == x.Count &&
                d.Value.All(f => x.TryGetValue(f.Key, out var y) && f.Value.Count == y.Count && f.Value.All(p => y.TryGetValue(p.Key, out var v) && v == p.Value)));
        System.Console.WriteLine("  live: the update since the last full copy, two minutes into a busy evening: " + string.Join(", ", made.Select(x => x.Packed?.Length ?? 0)) + " bytes packed (limit " + LiveDelta.MaxPacked + "); its dayDeeds paths: " + paths.Count);
        Check(refused.Count == 0 && made.Max(x => x.Packed?.Length ?? 0) <= LiveDelta.MaxPacked && today.Count > 0 && names.Count == 1 && today.Count + names.Count == paths.Count && read != null && Same(read.DayDeeds, sent),
              "DD live: each update of a busy evening is one message the 0.8.1 server keeps (its JSON object, base and seq, nothing private, no identity key; at most " + made.Max(x => x.Packed?.Length ?? 0) +
              " bytes packed); it carries only today's amounts (" + today.Count + " paths) and the part names new today (" + names.Count + "), and base + the latest update reads back the parts as sent" +
              (refused.Count > 0 ? ": " + string.Join(", ", refused) : ""));

        // ---------- a copy reads back the parts; nothing done is an empty share, an older copy has none, a hostile one only real days ----------
        var copy = PanelInput.FromSnapshot(GroupShare.SharedCopy(Full(typical, null, true)));
        var quiet = PanelInput.FromSnapshot(GroupShare.SharedCopy(Full(new DayHistory(), null, true)));
        var older = PanelInput.FromSnapshot(GroupShare.SharedCopy(Full(typical, null, false)));
        var hostile = PanelInput.FromSnapshot("{\"name\":\"Tor\",\"dayDeeds\":{\"parts\":{\"wood\":[\"$item_wood\",\"$item_finewood\",\"$item_roundlog\",7],\"own\":\"felled\"},\"nonsense\":{\"wood\":[5]}," +
                                              "\"2026-10-10\":{\"wood\":[-3,\"x\",4,9,9],\"own\":[7],\"ore\":[2]},\"2026-10-09\":7}}");
        Check(Same(copy?.DayDeeds, PanelModel.DayDeeds(typical, Today, 30, null, kinds)) && quiet?.DayDeeds != null && quiet.DayDeeds.Count == 0 && older != null && older.DayDeeds == null &&
              hostile?.DayDeeds != null && hostile.DayDeeds.Keys.SequenceEqual(new[] { "2026-10-10" }) && hostile.DayDeeds["2026-10-10"].Keys.SequenceEqual(new[] { "wood" }) &&
              hostile.DayDeeds["2026-10-10"]["wood"].Keys.SequenceEqual(new[] { "$item_roundlog" }),
              "DD read: a copy reads back every day's parts; a player with a day history and nothing done shares an empty map (not \"older\"); a copy without the key (before 0.8.5) has none; " +
              "only real days, amounts above 0 that have a name, and nothing past a family's names are read");
        return fails;
    }
}
