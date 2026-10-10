using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// What the game's own data says about the tokens in the character's counters, so the panel groups them the way the
    /// game does, mods included. Read once, after ObjectDB and ZNetScene are loaded:
    /// - items: the item type (the same switch the game uses for CraftWeapon/CraftFood..., PanelModel.ItemKindOf);
    /// - gathering: what felled logs split into is wood, what a pickaxe-only rock, ore vein or pile drops is mining
    ///   (PanelModel.GatherKindOf); what any smelter, kiln or refinery makes (Coal, Iron, ...) is never gathered, even when
    ///   some drop table also holds it (B12). A pickaxe drop a creature drops too (Leather Scraps: boars and piles) is not
    ///   mining, and one no smelter takes in (Withered Bone, Chitin) is a find of its own (0.6.5, PanelModel.GatheredItemKind).
    ///   The standing tree's own drops are left out: they are resin, cones and seeds,
    ///   not wood. PanelModel checks the vanilla names first;
    /// - pieces: TerrainOp/TerrainModifier = groundwork, Plant = planted, Feast = feast, the rest built;
    /// - piece categories (B16): the hammer tab's label from the piece tables, else the enum's name, else, for a mod's category
    ///   (an enum value past the vanilla ones, whose ToString is only the number), the name its mod gave it: the enum as Jotunn
    ///   and similar libraries extend Enum.GetNames/GetValues, or Jotunn's own book of categories by reflection (no reference to
    ///   Jotunn). Looked up when asked, so a tab Jotunn adds once the hammer is used is found too; a category nothing names is
    ///   null ("Other"), looked for again after a few seconds. None of it ever throws.
    /// Reads only. An unknown token gets null: PanelModel keeps such an item out of gear and food, leaves such a pickup out
    /// of wood and mining, and counts such a piece as built unless it carries a vanilla groundwork name.
    /// </summary>
    static class GameData
    {
        static readonly Dictionary<string, string> items = new Dictionary<string, string>(), gather = new Dictionary<string, string>(), pieces = new Dictionary<string, string>();
        static readonly Dictionary<string, string> crops = new Dictionary<string, string>(), types = new Dictionary<string, string>();   // Deeds: Farming, Crafting
        static readonly Dictionary<string, string> tokens = new Dictionary<string, string>();   // Deeds: Cooking (item prefab -> token)
        static readonly Dictionary<string, int> yields = new Dictionary<string, int>();   // Deeds: Cooking's cap (item token -> what one craft makes, the most any recipe gives)
        static readonly HashSet<string> stationDishes = new HashSet<string>();   // Deeds: Cooking (what cooking stations hand out, by item token)
        static readonly HashSet<string> grillDishes = new HashSet<string>(), ovenDishes = new HashSet<string>(), stationInputs = new HashSet<string>(), fermenterInputs = new HashSet<string>(), feastItems = new HashSet<string>();   // Deeds: Cooking's filter (Type)
        static readonly Dictionary<string, string> boosts = new Dictionary<string, string>();   // Deeds: Cooking's filter (Main boost: item token -> its biggest food value)
        static readonly Dictionary<string, string> materials = new Dictionary<string, string>();   // Deeds: Crafting (item token -> main material)
        static readonly Dictionary<string, string> pieceMats = new Dictionary<string, string>();   // Deeds: Building (piece token -> main material)
        static readonly Dictionary<string, int> pieceCats = new Dictionary<string, int>();   // Deeds: Building (piece token -> Piece.m_category as its number)
        static readonly Dictionary<int, string> tabLabels = new Dictionary<int, string>(), catNames = new Dictionary<int, string>();   // category -> the hammer tab's label; category -> its words, once found (B16)
        static readonly Dictionary<int, int> catTried = new Dictionary<int, int>();   // category -> when nothing named it last (Environment.TickCount)
        static readonly HashSet<int> catLogged = new HashSet<int>();
        static bool jotunnLooked; static Type jotunnPieces;
        static readonly Dictionary<string, Sprite> pieceIcons = new Dictionary<string, Sprite>();
        static bool ready;

        public static string ItemKind(string token) { Ensure(); return token != null && items.TryGetValue(token, out var k) ? k : null; }
        public static string GatherKind(string token) { Ensure(); return token != null && gather.TryGetValue(token, out var k) ? k : null; }
        public static string PieceKind(string token) { Ensure(); return token != null && pieces.TryGetValue(token, out var k) ? k : null; }
        /// <summary>A planted piece's harvest: the item prefab its grown plant's Pickable yields ("Barley"); null = not a crop.</summary>
        public static string CropOf(string token) { Ensure(); return token != null && crops.TryGetValue(token, out var k) ? k : null; }
        /// <summary>The game's item type of an item token ("OneHandedWeapon"); null = not known.</summary>
        public static string ItemType(string token) { Ensure(); return token != null && types.TryGetValue(token, out var k) ? k : null; }
        /// <summary>True when a cooking station (grill, iron cooking station, oven, any mod's CookingStation) hands this dish out: the game books it as CraftGrill, to whoever takes it off. False = made in the crafting window, or not known.</summary>
        public static bool StationDish(string token) { Ensure(); return token != null && stationDishes.Contains(token); }
        /// <summary>A food's type for Cooking's filter (PanelModel.DishTypeOf): set out as a feast, handed out by a grill or an oven (a
        /// cooking station that burns fuel), put onto a cooking station, taken by a fermenter, or a meal with food value; null = not known.</summary>
        public static string DishType(string token)
        {
            Ensure();
            if (token == null || !items.ContainsKey(token)) return null;
            return PanelModel.DishTypeOf(feastItems.Contains(token), grillDishes.Contains(token), ovenDishes.Contains(token), stationInputs.Contains(token), fermenterInputs.Contains(token), boosts.ContainsKey(token));
        }
        /// <summary>A food's biggest food value ("health", "stamina", "eitr", "balanced": PanelModel.DishBoostOf); null = no food value or not known.</summary>
        public static string DishBoost(string token) { Ensure(); return token != null && boosts.TryGetValue(token, out var k) ? k : null; }
        /// <summary>An item's main material from its recipe ("Bronze"): the ingredient that best marks the tier (PanelModel.MainMaterial); null = no recipe or no ingredient that marks a tier.</summary>
        public static string MainMaterial(string token) { Ensure(); return token != null && materials.TryGetValue(token, out var k) ? k : null; }
        /// <summary>The hammer tab a piece sits on ("Furniture"): the label its piece table gives that category, else the category's own name, else the name a mod gave it (B16); null = not known (Piece.m_category), counted as "Other".</summary>
        public static string PieceTab(string token)
        {
            Ensure();
            if (token == null || !pieceCats.TryGetValue(token, out var cat)) return null;
            return CategoryName(cat);
        }

        /// <summary>B16: a piece category in words (PanelModel.PieceCategoryName), remembered once found; null when nothing names it yet.</summary>
        static string CategoryName(int cat)
        {
            try
            {
                if (catNames.TryGetValue(cat, out var known)) return known;
                var now = Environment.TickCount;
                if (catTried.TryGetValue(cat, out var at) && unchecked(now - at) < 5000) return null;   // nothing named it a moment ago: look again later, not every frame
                var text = ((Piece.PieceCategory)cat).ToString();
                var modded = PanelModel.BareNumber(text);   // the enum has no name for it: a mod's category
                if (modded) LearnLiveTabs();   // the tables' labels as they are now: Jotunn adds a mod's tab once the hammer is used
                tabLabels.TryGetValue(cat, out var label);
                var name = PanelModel.PieceCategoryName(text, label, modded ? ModCategoryName(cat) : null);
                if (name != null) catNames[cat] = name; else catTried[cat] = now;
                if (modded && catLogged.Add(cat))
                    Debug.Log("[Hearthwoven] piece category " + cat + ": " + (name != null ? "\"" + name + "\"" : "no name found yet, counted as Other"));
                return name;
            }
            catch { return null; }
        }

        /// <summary>The labels a piece table gives its categories (the hammer's tabs), translated; the first label of each category stays.</summary>
        static void LearnTabs(PieceTable table)
        {
            if (table == null || table.m_categories == null) return;
            for (int i = 0; i < table.m_categories.Count; i++)
            {
                var label = table.m_categoryLabels != null && i < table.m_categoryLabels.Count ? table.m_categoryLabels[i] : null;
                var key = (int)table.m_categories[i];
                if (!string.IsNullOrWhiteSpace(label) && !tabLabels.ContainsKey(key)) tabLabels[key] = LocalizedName(label);
            }
        }

        /// <summary>Every tool's piece table as it is now: labels Jotunn added after the first read (the hammer in hand shares its table with the item's data).</summary>
        static void LearnLiveTabs()
        {
            try
            {
                if (!ObjectDB.instance || ObjectDB.instance.m_items == null) return;
                foreach (var go in ObjectDB.instance.m_items)
                {
                    var table = go ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
                    if (table) LearnTabs(table);
                }
            }
            catch { }
        }

        /// <summary>
        /// The name a mod gave a category the vanilla enum does not have; null when none is found. First the enum as the category
        /// managers extend it (Jotunn patches Enum.GetNames and Enum.GetValues for Piece.PieceCategory; ToString it does not, which
        /// is where the bare numbers came from), then Jotunn's own book of its categories by reflection, read only, never created.
        /// </summary>
        static string ModCategoryName(int cat)
        {
            try
            {
                var values = Enum.GetValues(typeof(Piece.PieceCategory)); var names = Enum.GetNames(typeof(Piece.PieceCategory));
                if (values != null && names != null && values.Length == names.Length)
                    for (int i = 0; i < values.Length; i++)
                        if (Convert.ToInt32(values.GetValue(i)) == cat && !string.IsNullOrWhiteSpace(names[i]) && !PanelModel.BareNumber(names[i])) return names[i];
            }
            catch { }
            try
            {
                if (!jotunnLooked)
                {
                    jotunnLooked = true;
                    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try { if (a.GetName().Name == "Jotunn") { jotunnPieces = a.GetType("Jotunn.Managers.PieceManager", false); break; } } catch { }
                    }
                }
                if (jotunnPieces == null) return null;
                const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var manager = jotunnPieces.GetField("_instance", any)?.GetValue(null);   // the manager as Jotunn made it; never created here
                if (manager == null) return null;
                var books = new List<IDictionary>();
                foreach (var f in new[] { "PieceCategories", "OtherPieceCategories" })
                    if (jotunnPieces.GetField(f, any)?.GetValue(manager) is IDictionary d) books.Add(d);   // name -> category
                if (jotunnPieces.GetMethod("GetPieceCategoriesMap", any, null, Type.EmptyTypes, null)?.Invoke(manager, null) is IDictionary map) books.Add(map);   // category -> name
                foreach (var book in books)
                    foreach (DictionaryEntry e in book)
                    {
                        var name = e.Key as string ?? e.Value as string; var value = e.Key is string ? e.Value : e.Key;
                        if (name != null && value != null && Convert.ToInt32(value) == cat && !PanelModel.BareNumber(name)) return name;
                    }
            }
            catch { }
            return null;
        }
        /// <summary>A piece's main material from its build resources ("Fine wood"), judged by PanelModel.PieceMainMaterial; null = no resource marks one.</summary>
        public static string PieceMaterial(string token) { Ensure(); return token != null && pieceMats.TryGetValue(token, out var k) ? k : null; }
        /// <summary>An item prefab's token ("CookedMeat" -> "$item_cookedmeat"): what eaters record against what the craft counter books.</summary>
        public static string ItemToken(string prefab) { Ensure(); return prefab != null && tokens.TryGetValue(prefab, out var k) ? k : null; }
        /// <summary>How many of an item one craft puts in the bag (Recipe.m_amount: 4 sausages); the most when several recipes make it, 0 = no recipe known.
        /// The game's craft counter books one per craft, eaters one per serving (DeedsModel.MadeOf, CompanyModel.MakerMade).</summary>
        public static int RecipeYield(string token) { Ensure(); return token != null && yields.TryGetValue(token, out var n) ? n : 0; }
        /// <summary>The game's own sprite of a piece by its name token ("$piece_woodwall"), as the build menu shows it.</summary>
        public static Sprite PieceIcon(string token) { Ensure(); return token != null && pieceIcons.TryGetValue(token, out var s) ? s : null; }

        // the game's own word for a material the panel does not know by name (a mod's bar)
        static string LocalizedName(string token) { try { return Localization.instance != null ? Localization.instance.Localize(token) : token; } catch { return token; } }

        /// <summary>Reads the game data now if it is there and not read yet (PanelWarm: soon after spawn, so the book's first opening does not); true once read.</summary>
        public static bool Warm() { Ensure(); return ready; }

        static void Ensure()
        {
            if (ready || !ObjectDB.instance || ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0 || !ZNetScene.instance) return;
            ready = true;
            try
            {
                var pieceObjs = new Dictionary<string, Piece>();   // the first Piece of each name, for its build resources (judged once the smelters are known)
                void Piece(GameObject go, string tableKind = null)
                {
                    var p = go ? go.GetComponent<Piece>() : null;
                    if (!p || string.IsNullOrEmpty(p.m_name)) return;
                    if (!pieceCats.ContainsKey(p.m_name)) pieceCats[p.m_name] = (int)p.m_category;   // the number: a mod's category has no enum name (B16, CategoryName)
                    if (!pieceObjs.ContainsKey(p.m_name)) pieceObjs[p.m_name] = p;
                    if (go.GetComponent<Feast>() && p.m_resources != null)   // a feast is set out from its feast item (Cooking's filter: Feasts)
                        foreach (var req in p.m_resources) { var food = req != null && req.m_resItem ? req.m_resItem.m_itemData?.m_shared?.m_name : null; if (!string.IsNullOrEmpty(food)) feastItems.Add(food); }
                    var kind = tableKind ?? PanelModel.PieceKindOf(go.GetComponent<TerrainOp>() || go.GetComponent<TerrainModifier>(), go.GetComponent<Plant>(), go.GetComponent<Feast>());
                    if (!pieces.ContainsKey(p.m_name) || pieces[p.m_name] == "built") pieces[p.m_name] = kind;
                    if (p.m_icon && !pieceIcons.ContainsKey(p.m_name)) pieceIcons[p.m_name] = p.m_icon;
                    var plant = go.GetComponent<Plant>();
                    if (plant && plant.m_grownPrefabs != null && !crops.ContainsKey(p.m_name))
                        foreach (var grown in plant.m_grownPrefabs)
                        {
                            var pick = grown ? grown.GetComponent<Pickable>() : null;
                            if (pick && pick.m_itemPrefab) { crops[p.m_name] = pick.m_itemPrefab.name; break; }
                        }
                }
                foreach (var go in ObjectDB.instance.m_items)
                {
                    var shared = go ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                    if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;
                    if (!items.ContainsKey(shared.m_name)) items[shared.m_name] = PanelModel.ItemKindOf(shared.m_itemType.ToString(), shared.m_name);
                    if (!types.ContainsKey(shared.m_name)) types[shared.m_name] = shared.m_itemType.ToString();
                    if (!tokens.ContainsKey(go.name)) tokens[go.name] = shared.m_name;
                    if (!boosts.ContainsKey(shared.m_name)) { var boost = PanelModel.DishBoostOf(shared.m_food, shared.m_foodStamina, shared.m_foodEitr); if (boost != null) boosts[shared.m_name] = boost; }
                    LearnTabs(shared.m_buildPieces);
                    var table = shared.m_buildPieces?.m_pieces;   // hammer, hoe, cultivator, feast tray, mods' tools
                    if (table != null)
                    {
                        // the cultivator's pieces are plantings, a pickable without a Plant component too (PanelModel.PieceKindsOfTable, 0.6.5)
                        var kinds = PanelModel.PieceKindsOfTable(table.Select(g => (g != null && (g.GetComponent<TerrainOp>() != null || g.GetComponent<TerrainModifier>() != null), g != null && g.GetComponent<Plant>() != null, g != null && g.GetComponent<Feast>() != null, g != null && g.GetComponent<Pickable>() != null)).ToList());
                        for (int k = 0; k < table.Count; k++) Piece(table[k], kinds[k]);
                    }
                }
                var drops = new List<KeyValuePair<DropTable, string>>();
                var refined = new HashSet<string>();   // what smelters, kilns and refineries make
                var smelted = new HashSet<string>();   // what they take in: ore (0.6.5)
                var creature = new HashSet<string>();   // what any creature drops (CharacterDrop): a pickup of it can be a kill's loot (0.6.5)
                void Gather(DropTable table, string component, HitData.DamageModifiers mods)
                {
                    var kind = PanelModel.GatherKindOf(component, mods.m_chop.ToString(), mods.m_pickaxe.ToString());
                    if (kind != null && table != null) drops.Add(new KeyValuePair<DropTable, string>(table, kind));
                }
                foreach (var go in ZNetScene.instance.m_prefabs)
                {
                    if (!go) continue;
                    Piece(go);
                    var log = go.GetComponent<TreeLog>(); if (log) Gather(log.m_dropWhenDestroyed, "TreeLog", log.m_damages);
                    var rock = go.GetComponent<MineRock>(); if (rock) Gather(rock.m_dropItems, "MineRock", rock.m_damageModifiers);
                    var rock5 = go.GetComponent<MineRock5>(); if (rock5) Gather(rock5.m_dropItems, "MineRock5", rock5.m_damageModifiers);
                    var destructible = go.GetComponent<Destructible>(); var dropper = go.GetComponent<DropOnDestroyed>();
                    if (destructible && dropper) Gather(dropper.m_dropWhenDestroyed, "Destructible", destructible.m_damages);
                    var cooker = go.GetComponent<CookingStation>();
                    if (cooker && cooker.m_conversion != null)
                        foreach (var c in cooker.m_conversion)
                        {
                            var dish = c?.m_to?.m_itemData?.m_shared?.m_name; if (!string.IsNullOrEmpty(dish)) { stationDishes.Add(dish); (cooker.m_useFuel ? ovenDishes : grillDishes).Add(dish); }
                            var raw = c?.m_from?.m_itemData?.m_shared?.m_name; if (!string.IsNullOrEmpty(raw)) stationInputs.Add(raw);
                        }
                    var fermenter = go.GetComponent<Fermenter>();
                    if (fermenter && fermenter.m_conversion != null)
                        foreach (var c in fermenter.m_conversion) { var basis = c?.m_from?.m_itemData?.m_shared?.m_name; if (!string.IsNullOrEmpty(basis)) fermenterInputs.Add(basis); }
                    var smelter = go.GetComponent<Smelter>();
                    if (smelter && smelter.m_conversion != null)
                        foreach (var c in smelter.m_conversion)
                        {
                            var made = c?.m_to?.m_itemData?.m_shared?.m_name; if (!string.IsNullOrEmpty(made)) refined.Add(made);
                            var taken = c?.m_from?.m_itemData?.m_shared?.m_name; if (!string.IsNullOrEmpty(taken)) smelted.Add(taken);
                        }
                    var loot = go.GetComponent<CharacterDrop>();
                    if (loot && loot.m_drops != null)
                        foreach (var dr in loot.m_drops) { var n = dr?.m_prefab ? dr.m_prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name : null; if (!string.IsNullOrEmpty(n)) creature.Add(n); }
                }
                foreach (var d in drops)
                    foreach (var dd in d.Key?.m_drops ?? new List<DropTable.DropData>())
                    {
                        var shared = dd.m_item ? dd.m_item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                        if (shared == null || string.IsNullOrEmpty(shared.m_name) || gather.ContainsKey(shared.m_name)) continue;
                        var kind = PanelModel.GatheredItemKind(d.Value, refined.Contains(shared.m_name), creature.Contains(shared.m_name), smelted.Contains(shared.m_name));
                        if (kind != null) gather[shared.m_name] = kind;
                    }
                // main material: every recipe's ingredients (ObjectDB.m_recipes), judged by PanelModel.MainMaterial; a bar is an ingredient some smelter makes
                if (ObjectDB.instance.m_recipes != null)
                    foreach (var r in ObjectDB.instance.m_recipes)
                    {
                        var made = r && r.m_item ? r.m_item.m_itemData?.m_shared?.m_name : null;
                        if (!string.IsNullOrEmpty(made) && r.m_amount > 1 && (!yields.TryGetValue(made, out var y) || r.m_amount > y)) yields[made] = r.m_amount;
                        if (string.IsNullOrEmpty(made) || materials.ContainsKey(made) || r.m_resources == null) continue;
                        var recipe = new List<Ingredient>();
                        foreach (var req in r.m_resources)
                        {
                            var shared = req != null && req.m_resItem ? req.m_resItem.m_itemData?.m_shared : null;
                            if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;
                            recipe.Add(new Ingredient { Token = shared.m_name, Refined = refined.Contains(shared.m_name), Name = refined.Contains(shared.m_name) ? LocalizedName(shared.m_name) : null });
                        }
                        var m = PanelModel.MainMaterial(recipe);
                        if (m != null) materials[made] = m;
                    }
                // pieces: the same for what a piece costs to build (Piece.m_resources); a resource only counts as a new material when the game calls it one
                foreach (var kv in pieceObjs)
                {
                    var recipe = new List<PieceIngredient>();
                    foreach (var req in kv.Value.m_resources ?? new Piece.Requirement[0])
                    {
                        var shared = req != null && req.m_resItem ? req.m_resItem.m_itemData?.m_shared : null;
                        if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;
                        recipe.Add(new PieceIngredient { Token = shared.m_name, Amount = req.m_amount, Refined = refined.Contains(shared.m_name), Material = shared.m_itemType == ItemDrop.ItemData.ItemType.Material, Name = LocalizedName(shared.m_name) });
                    }
                    var m = PanelModel.PieceMainMaterial(recipe);
                    if (m != null) pieceMats[kv.Key] = m;
                }
                Debug.Log("[Hearthwoven] panel game data: " + items.Count + " items, " + pieces.Count + " pieces, " + gather.Count + " gathered kinds");
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] panel game data: " + e.Message); }
        }
    }
}
