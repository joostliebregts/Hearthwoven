// The group feats (Hearthwoven 0.7, work/hearthwoven-0.7/GROUP-FEATS.md): only the checks that earn their place. The gate hides a feat until the group
// found its land (a fellow's land counts, also from a copy without "knownBiomes"); the riddle stands until someone who shares held the material; one sum
// for the group with two fellows sharing the same things, per kind of count, never twice; a tier sticks after a fellow stops sharing; and "knownBiomes"
// travels whole through the path a server keeps and relays a copy by. Called from Program.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class GroupFeatsTests
{
    static readonly DateTime Now = new DateTime(2026, 10, 9, 18, 0, 0, DateTimeKind.Utc);

    static PanelView Together(PanelInput i, string sel = "") => PanelModel.Build(i, new PanelState { Chapter = Chapter.Feats, FeatSel = sel, Page = { [Chapter.Feats] = PanelModel.FeatsTogetherId } });
    static List<Block> Cards(PanelView v) => PanelModel.VisibleFeats(v);

    /// <summary>A fellow's copy as the game sends it (Snapshot.Build, the server's shared copy, read back), with what the test gives them.</summary>
    static PanelInput Copy(string name, long id, Action<PlayerProfile.PlayerStats> record = null, Action<SessionEvents> since = null, IEnumerable<string> biomes = null)
    {
        var stats = new PlayerProfile.PlayerStats[1]; stats[0] = new PlayerProfile.PlayerStats(); stats[0][PlayerStatType.Jumps] = 1; record?.Invoke(stats[0]);   // a record with nothing in it is not sent
        var ev = new SessionEvents(); since?.Invoke(ev);
        var json = Snapshot.Build("0.7.0", id, name, stats, new Snapshot.SkillInfo[0], "w", new DamageTally(), "s-" + id, ev, new EventLog(), true, ev, feats: new FeatsLedger(), knownBiomes: biomes);
        var c = PanelInput.FromSnapshot(GroupShare.SharedCopy(json)); c.NowUtc = Now; return c;
    }

    static PanelInput Self(params PanelInput[] fellows)
    {
        var me = new PanelInput
        {
            PlayerName = "Rowan", PlayerId = 11, IsSelf = true, NowUtc = Now, InstalledUtc = Now.AddDays(-5), Events = new SessionEvents(), Log = new EventLog(), Session = new DamageTally(),
            Character = new Dictionary<string, float>(), ItemsCrafted = new Dictionary<string, float>(), ItemsPickedUp = new Dictionary<string, float>(), PiecesPlaced = new Dictionary<string, float>(),
            KnownBiomes = new List<string> { "Meadows", "BlackForest" }, Feats = new FeatsLedger { Primed = true }, Fellows = fellows.ToList(),
        };
        foreach (var f in fellows) f.Fellows = new[] { me }.Concat(fellows.Where(x => x != f)).ToList();
        return me;
    }

    public static int Run()
    {
        int fails = 0;
        void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

        // ---------- the gate: a land's feats wait until someone who shares found it ----------
        {
            var early = FeatsSample.EarlyGroup(Now);
            var page = Together(early);
            var ids = Cards(page).Select(c => c.Id).ToList();
            Check(!ids.Contains("bogiron") && !ids.Contains("ironforge") && ids.Contains("copperwed") && ids.Contains("charcoal") && ids.Contains("meadhall") && ids.Contains("wellfed") &&
                  PanelModel.PlateOf(page).Text == PanelModel.TogetherDefinition + " " + PanelModel.LandsNotFound,
                  "gate: the Meadows and the Black Forest found: their feats show, the Swamp's do not (Bog Iron, Iron for the Forge), and one quiet line says more wait: " + string.Join(", ", ids));
            var texts = string.Join(" ", PanelModel.Content(page).SelectMany(b => new[] { b }.Concat(b.Items ?? new List<Block>()).Concat((b.Items ?? new List<Block>()).SelectMany(x => x.Items ?? new List<Block>())))
                                     .SelectMany(b => new[] { b.Title, b.Text, b.Note, b.Value, b.Icon })).ToLowerInvariant();
            Check(!texts.Contains("bog") && !texts.Contains("swamp") && !texts.Contains("iron") && !texts.Contains("forge") && !texts.Contains("anvil"),
                  "gate: nothing on the page, words or icons, names the land not found, its metal or the forge it feeds (the Workshop shows a workbench extension)");
            // Edda finds the Swamp: her copy says so, and the gate opens on Rowan's book
            var edda = early.Fellows[0]; edda.KnownBiomes = new List<string> { "Meadows", "BlackForest", "Swamp" };
            var opened = Together(early);
            Check(Cards(opened).Any(c => c.Id == "bogiron") && Cards(opened).Any(c => c.Id == "ironforge") && PanelModel.PlateOf(opened).Text == PanelModel.TogetherDefinition,
                  "gate: a fellow who shares found the Swamp: its feats show on your book too, and the line is gone");
            // an older copy (0.6) carries no knownBiomes: the evidence it does carry opens the gate (damage there since install)
            edda.KnownBiomes = null; edda.BiomeSinceInstall = new BiomeTally(); edda.BiomeSinceInstall.AddDamage("Swamp", true, "slash", 40);
            Check(Cards(Together(early)).Any(c => c.Id == "bogiron"), "gate: a copy from before 0.7 (no lands in it): a fight in the Swamp it shares opens the gate");
        }

        // ---------- the veil: a riddle name until someone who shares has held the material ----------
        {
            var early = FeatsSample.EarlyGroup(Now);
            var card = Cards(Together(early, "copperwed")).Single(c => c.Id == "copperwed");
            var d = card.Items[0];
            var said = string.Join(" ", new[] { card.Title, card.Text, card.Note, d.Title, d.Text }.Concat(d.Items.Select(x => x.Title + " " + x.Text))).ToLowerInvariant();
            Check(card.Title == "What the Deep Woods Hold" && d.Text == "Something in these woods is worth the fire." && card.Icon == "vocab:biome-blackforest" &&
                  !said.Contains("copper") && !said.Contains("tin ") && !said.Contains("bronze"),
                  "veil: the Black Forest found but no copper or tin held: its riddle name, line and land's emblem, and nothing names the metal: " + card.Title + " | " + said);
            early.Fellows[0].ItemsPickedUp["$item_tinore"] = 3;   // Edda brings home a little tin
            var lifted = Cards(Together(early, "copperwed")).Single(c => c.Id == "copperwed");
            Check(lifted.Title == "Copper Wed to Tin" && lifted.Items[0].Items.First(x => x.Kind == "rule").Title == "Together, make 50 bronze." && lifted.Icon.StartsWith("item:$item_bronze"),
                  "veil: a fellow who shares held tin: the real name, line, rule and the game's own picture");
            var veils = PanelModel.FeatDefs.Where(f => f.Veil != null).ToList();
            Check(veils.Select(f => f.Id).OrderBy(x => x).SequenceEqual(new[] { "bogiron", "copperwed", "ironforge" }) &&
                  veils.All(f => new[] { f.Veil.Name, f.Veil.Honours, f.Veil.Rule(f.Tiers[0]), f.Veil.Brief(f.Tiers[0]), f.Veil.Caveat ?? f.Caveat }.All(t => !new[] { "copper", "tin ", "bronze", "iron", "scrap" }.Any(w => t.ToLowerInvariant().Contains(w)))),
                  "veil: every riddle (name, line, rule, tile line, caveat) names no material");
        }

        // ---------- review fix: the same veil for both of the Swamp's iron feats, their caveats and pictures included; the ungated Workshop shows no anvil ----------
        {
            var tor = Copy("Tor", 88, s => { s[PlayerStatType.CraftFood] = 1; });
            var me = Self(tor); me.KnownBiomes.Add("Swamp");   // the Swamp found, no iron held by anyone who shares
            string Said(Block c) => string.Join(" ", new[] { c.Title, c.Text, c.Note, c.Icon }.Concat(c.Items.SelectMany(x => new[] { x.Title, x.Text, x.Icon }.Concat((x.Items ?? new List<Block>()).SelectMany(y => new[] { y.Title, y.Text, y.Icon }))))).ToLowerInvariant();
            Block Card(PanelInput i, string id) => Cards(Together(i, id)).Single(c => c.Id == id);
            var bog = Card(me, "bogiron"); var forge = Card(me, "ironforge"); var shop = Card(me, "workshop");
            Check(bog.Title == "What the Bog Keeps" && forge.Title == "Ore for the Forge" && bog.Icon == "vocab:biome-swamp" && forge.Icon == "vocab:biome-swamp" && bog.Items[0].Icon == "vocab:biome-swamp" && forge.Items[0].Icon == "vocab:biome-swamp" &&
                  !new[] { Said(bog), Said(forge) }.Any(t => t.Contains("iron") || t.Contains("bar ") || t.Contains("bars") || t.Contains("scrap") || t.Contains("anvil")),
                  "veil: the Swamp found, no iron held: Bog Iron and Iron for the Forge both keep their riddle, the Swamp's emblem as picture, and no word or icon (caveat included) names the metal: " + Said(bog) + " || " + Said(forge));
            Check(!Said(shop).Contains("anvil") && !Said(shop).Contains("forge") && !Said(shop).Contains("iron"),
                  "veil: The Workshop Grows (not gated) uses a workbench extension's picture and no anvil, forge or iron in its words or icons: " + Said(shop));
            tor.ItemsPickedUp["$item_ironscrap"] = 2;   // Tor carries home some scrap
            Check(Card(me, "bogiron").Title == "Bog Iron" && Card(me, "ironforge").Title == "Iron for the Forge", "veil: once someone who shares held iron, both Swamp feats lift together");
        }

        // ---------- one sum: you and every fellow who shares, each once, per kind of count ----------
        {
            // the record and since install (Bog Iron): your baseline 40 + 120 taken since; Tor's copy holds 100 bars in his record, 30 of them counted since install
            // by his Hearthwoven (inside the record, not on top of it); Finch has only an exact count (50). Tor appears twice in the list (a stale copy): once.
            PanelInput tor = Copy("Tor", 88, s => s.m_itemPickupStats["$item_iron"] = 100, e => SessionEvents.Add(e.PickedUp, "$item_iron", 30));
            PanelInput finch = Copy("Finch", 99, null, e => SessionEvents.Add(e.PickedUp, "$item_iron", 50));
            var me = Self(tor, finch, Copy("Tor", 88, s => s.m_itemPickupStats["$item_iron"] = 100, e => SessionEvents.Add(e.PickedUp, "$item_iron", 30)));
            me.KnownBiomes.Add("Swamp");
            me.ItemsPickedUp["$item_iron"] = 160; SessionEvents.Add(me.Events.PickedUp, "$item_iron", 120);
            me.Baseline = new Dictionary<string, Dictionary<string, float>> { ["pickedUp"] = new Dictionary<string, float> { ["$item_iron"] = 40 } };
            var iron = PanelModel.FeatById("bogiron");
            Check(PanelModel.FeatValue(iron, me) == 160 + 100 + 50 && PanelModel.FeatValue(iron, tor) == 310,
                  "sum (record and since install): Bog Iron is 40 + 120 (yours) + 100 (Tor) + 50 (Finch) = 310, Tor once, his own count not on top of his record; the same on Tor's book: " + PanelModel.FeatValue(iron, me));
            var ironDetail = Cards(Together(me, "bogiron")).Single(c => c.Id == "bogiron").Items[0];
            var helped = ironDetail.Items.Single(x => x.Kind == "crew");
            Check(string.Join(", ", helped.Items.Select(x => x.Title + " " + x.Value + " · " + x.Note)) == "Rowan 160 · 52 %, Tor 100 · 32 %, Finch 50 · 16 %" && helped.Title == "Who smelted" && helped.Tone == "share" &&
                  ironDetail.Items.Single(x => x.Kind == "counted").Text == "Everyone who shares, in every world you play" && !ironDetail.Items.Single(x => x.Kind == "caveat").Text.Contains("every world") &&
                  ironDetail.Items.Single(x => x.Kind == "progress").Title == "310 of 600 iron bars",
                  "sum (B37): the detail says who smelted, each with their part and share, largest first (the shares add up to 100), and the counting once, in one line: " +
                  string.Join(", ", helped.Items.Select(x => x.Title + " " + x.Value + " · " + x.Note)));
            // The Charcoal Burners counts coal taken, as Bog Iron counts bars: what Stoker's Chests put in a kiln is no one's, the coal that comes out is
            SessionEvents.Add(me.Events.ChestFed, "charcoal_kiln|Wood", 500); SessionEvents.Add(finch.Events.PickedUp, "$item_coal", 25);
            Check(PanelModel.FeatValue(PanelModel.FeatById("charcoal"), me) == 25, "sum: The Charcoal Burners is coal taken (Finch's 25), not wood a Stoker's Chest fed the kiln");
            // a set (The Workshop Grows, Many Crafts): the chopping block all three placed, and the bread all three made, count once
            foreach (var p in new[] { me, tor, finch }) { p.PiecesPlaced["$piece_workbench_ext1"] = 1; p.ItemsCrafted["$item_bread"] = 4; }
            tor.PiecesPlaced["$piece_forge_ext1"] = 1; finch.ItemsCrafted["$item_arrow_wood"] = 20;
            Check(PanelModel.FeatValue(PanelModel.FeatById("workshop"), me) == 2 && PanelModel.FeatValue(PanelModel.FeatById("manycrafts"), me) == 2,
                  "sum (a set): the same extension or the same thing made by three players counts once (2 extensions, 2 different things)");
            // the game's counters (The Hall Well Fed, Clad for the Road) per player, Tor once
            me.Character["CraftFood"] = 100; tor.Character["CraftFood"] = 30; finch.Character["CraftArmor"] = 2; tor.Character["CraftArmor"] = 3;
            Check(PanelModel.FeatValue(PanelModel.FeatById("wellfed"), me) == 130 && PanelModel.FeatValue(PanelModel.FeatById("clad"), me) == 5,
                  "sum (the game's counters): 100 + 30 dishes, 3 + 2 pieces of armour, Tor's second copy not again");
        }

        // ---------- a tier sticks after a fellow stops sharing ----------
        {
            var tor = Copy("Tor", 88, s => { s[PlayerStatType.CraftFood] = 200; }, null, new[] { "Meadows", "BlackForest", "Swamp" });
            tor.ItemsPickedUp = new Dictionary<string, float> { ["$item_ironscrap"] = 40, ["$item_iron"] = 150 };
            var me = Self(tor); me.Character["CraftFood"] = 60;
            var fresh = PanelModel.EvaluateFeats(me, Now, "Meadows", null);
            Check(fresh.Any(f => f.feat.Id == "wellfed" && f.tier == 1) && fresh.Any(f => f.feat.Id == "bogiron" && f.tier == 1) && me.Feats.Moment("wellfed").Value.Noticed,
                  "sticks: with Tor sharing, The Hall Well Fed I (260 dishes) and Bog Iron I (his 150 bars, the Swamp he found) are noticed in your own ledger");
            me.Fellows.Clear();   // Tor stops sharing (or his copy ages out after 14 days)
            var page = Together(me, "bogiron");
            var bog = Cards(page).SingleOrDefault(c => c.Id == "bogiron");
            Check(PanelModel.FeatTier(PanelModel.FeatById("wellfed"), me) == 1 && bog != null && bog.Level == 1 && bog.Title == "Bog Iron" && bog.Tone == PanelModel.FeatTone,
                  "sticks: Tor gone, the tiers stay earned, and Bog Iron stays on the page under its own name though the Swamp and the scrap were his");
        }

        // ---------- the lands found travel in the snapshot, whole, through the copy a server keeps and relays ----------
        {
            var json = Snapshot.Build("0.7.0", 5, "Edda", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", new DamageTally(), knownBiomes: new[] { "Meadows", "BlackForest", "Swamp" });
            var kept = GroupShare.SharedCopy(json);   // what the server writes to players/<id>.share.json and serves as the file's text (GroupServe)
            var back = PanelInput.FromSnapshot(kept);
            Check(ServerIntake.IsJsonObject(json) && kept.Contains("\"knownBiomes\":[\"Meadows\",\"BlackForest\",\"Swamp\"]") && back.KnownBiomes.SequenceEqual(new[] { "Meadows", "BlackForest", "Swamp" }) &&
                  PanelInput.FromSnapshot(Snapshot.Build("0.6.0", 5, "Old", new PlayerProfile.PlayerStats[0], new Snapshot.SkillInfo[0], "w", new DamageTally())).KnownBiomes == null,
                  "snapshot: \"knownBiomes\" passes the server's intake check, stays in the shared copy and is read back; an older copy has none (null: the gate uses its evidence)");
        }
        // ---------- review fix: each person once by who they are (the fellow key), never by character name ----------
        {
            PanelInput Tor(long id, float bars) => Copy("Tor", id, s => s.m_itemPickupStats["$item_iron"] = bars);
            var me = Self(Tor(88, 100), Tor(89, 40), Tor(88, 100));   // two different players called Tor, and a stale second copy of the first
            me.KnownBiomes.Add("Swamp");
            var torsBook = me.Fellows[1];   // the second Tor's book: you and the first Tor are in their list
            Check(PanelModel.FeatValue(PanelModel.FeatById("bogiron"), me) == 140 && PanelModel.GroupMembers(me).Count == 3 && PanelModel.FeatValue(PanelModel.FeatById("bogiron"), torsBook) == 140,
                  "members: two players both called Tor both count (100 + 40 bars), the stale copy of the first once; the same on the second Tor's book: " + PanelModel.FeatValue(PanelModel.FeatById("bogiron"), me));
        }

        // ---------- review fix: shields are not armour (the game books a crafted shield as CraftWeapon, InventoryGui.DoCrafting) ----------
        {
            var me = Self(); me.Character["CraftArmor"] = 5; me.Character["CraftWeapon"] = 4;
            var clad = PanelModel.FeatById("clad");
            Check(PanelModel.FeatValue(clad, me) == 5 && clad.Caveat.Contains("shields and upgrades do not") && !clad.Caveat.Contains("Shields and capes count"),
                  "clad: Clad for the Road counts the game's armour counter only, and its caveat says shields do not count: " + clad.Caveat);
        }

        // ---------- review fix: a fellow's shared knownBiomes opens the gate only, never their Maps tiles, Battle's biome row or strips ----------
        {
            var edda = Copy("Edda", 55, null, null, new[] { "Meadows", "BlackForest", "Swamp" });   // her copy says she found the Swamp, no trace of it otherwise
            var me = Self(edda);
            var tiles = PanelModel.BiomeTilesFound(edda)?.Items.Select(t => t.Id).ToList() ?? new List<string>();
            var strip = PanelModel.BiomeStrip(edda, new List<PanelModel.DamageRow>(), new List<EventLog.Death>(), (string)null)?.Items.Select(t => t.Id).ToList() ?? new List<string>();
            Check(PanelModel.GroupFound(me).Contains("Swamp") && Cards(Together(me)).Any(c => c.Id == "bogiron") &&
                  !PanelModel.FoundBiomes(edda).Contains("Swamp") && !tiles.Contains("Swamp") && !strip.Contains("Swamp") && PanelModel.FoundBiomes(me).SequenceEqual(new[] { "Meadows", "BlackForest" }),
                  "knownBiomes: Edda's shared Swamp opens the group feats' gate, but her Maps tiles, biome row and strips name no Swamp (only her copy's evidence, as before 0.7); yours stay your own record");
        }

        // ---------- review fix: a group tier's gold dot sits on the Feats tab and Together, never on Earned; only opening Together clears it ----------
        {
            var tor = Copy("Tor", 88, s => { s[PlayerStatType.CraftFood] = 200; });
            var me = Self(tor); me.Character["CraftFood"] = 60;
            var fresh = PanelModel.EvaluateFeats(me, Now, "Meadows", null);
            PanelState At(string page) => new PanelState { Chapter = Chapter.Feats, Page = { [Chapter.Feats] = page } };
            bool Dot(PanelView v, string id) => v.List.Single(l => l.Id == id).Dot;
            var deeds = PanelModel.Build(me, new PanelState { Chapter = Chapter.Deeds });
            var earned = PanelModel.Build(me, At(PanelModel.FeatsPageId));
            var crossed = fresh.Any(f => f.feat.Id == "wellfed") && fresh.All(f => f.feat.Group);
            Check(crossed && deeds.Chapters.Single(c => c.Id == "Feats").Dot && Dot(earned, PanelModel.FeatsTogetherId) && !Dot(earned, PanelModel.FeatsPageId),
                  "dot: a group tier crossed (The Hall Well Fed I) puts the gold dot on the Feats tab and on Together, not on Earned");
            PanelModel.FeatsSeen(me, At(PanelModel.FeatsPageId));   // Earned opened
            var still = PanelModel.Build(me, At(PanelModel.FeatsUnsungId));
            Check(Dot(still, PanelModel.FeatsTogetherId) && PanelModel.Build(me, new PanelState { Chapter = Chapter.Deeds }).Chapters.Single(c => c.Id == "Feats").Dot,
                  "dot: opening Earned does not clear a group tier's dot");
            var onTogether = Together(me);
            PanelModel.FeatsSeen(me, At(PanelModel.FeatsTogetherId));   // Together opened
            var after = PanelModel.Build(me, At(PanelModel.FeatsPageId));
            Check(!Dot(onTogether, PanelModel.FeatsTogetherId) && !after.List.Any(l => l.Dot) && !PanelModel.Build(me, new PanelState { Chapter = Chapter.Deeds }).Chapters.Any(c => c.Dot),
                  "dot: Together answers its own dot, and opening it clears the dot from the tab and Together");
        }

        return fails;
    }
}
