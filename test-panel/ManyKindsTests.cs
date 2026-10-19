// 0.8.1 (Joost's PC, Deeds > Building: 2 630 objects, 863 texts, 194 ms a rebuild): a long-played character with a mod-heavy hammer has
// hundreds of kinds, and the sample world has a few dozen, so the previews never showed a list without its fold. This builds every page
// and view of a world the size of his (400 piece kinds on 30 hammer tabs, 150 dishes, 150 kinds of gear, 300 kinds picked up, 200 cargo
// items), your own book and the group's, and fails when any block draws more than ManyMax rows, tiles, chips or legend entries, or a page
// more than PanelModel.ItemPageMax item tiles (folded: Show all draws the full list on request). Called from Program.cs; World() is also the
// preview scenarios deeds-building-manykinds (folded, By category, Show all).
using System;
using System.Collections.Generic;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

static class ManyKindsTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

    /// <summary>The most entries one block may draw: rows, tiles, chips or a legend's entries.</summary>
    public const int ManyMax = 60;

    /// <summary>The sample world with Rowan's book grown to a long-played character's size: every kind invented, sample names only.</summary>
    public static PanelInput World(DateTime now)
    {
        var w = PanelSample.Full(now);
        string Piece(int k) => "$piece_many" + k.ToString("000");
        string Dish(int k) => "$item_manydish" + k.ToString("000");
        string Gear(int k) => "$item_manygear" + k.ToString("000");
        string Found(int k) => "$item_manyfound" + k.ToString("000");
        string[] dishTypes = { "meal", "grilled", "baked", "feast", "uncooked", "meadbase" }, boosts = { "health", "stamina", "eitr", "balanced" };
        string[] gearTypes = { "OneHandedWeapon", "TwoHandedWeapon", "Bow", "Shield", "Chest", "Helmet", "Legs", "Shoulder", "Tool", "Utility", "Trinket" };
        string[] materials = { "Wood", "Fine wood", "Core wood", "Stone", "Iron", "Copper", "Black marble", "Grausten", "Flametal", "Bronze", "Obsidian", "Crystal", "Silver", "Tin", "Bone", "Hide", "Resin", "Ancient bark", "Yggdrasil wood", "Sap" };
        w.PiecesPlaced = new Dictionary<string, float>(w.PiecesPlaced ?? new Dictionary<string, float>());
        w.ItemsCrafted = new Dictionary<string, float>(w.ItemsCrafted ?? new Dictionary<string, float>());
        w.ItemsPickedUp = new Dictionary<string, float>(w.ItemsPickedUp ?? new Dictionary<string, float>());
        for (int k = 0; k < 400; k++) w.PiecesPlaced[Piece(k)] = 400 - k;
        for (int k = 0; k < 150; k++) w.ItemsCrafted[Dish(k)] = 150 - k;
        for (int k = 0; k < 150; k++) w.ItemsCrafted[Gear(k)] = 1 + k % 7;
        for (int k = 0; k < 300; k++) w.ItemsPickedUp[Found(k)] = 300 - k;
        if (w.Events?.CargoMeters != null && w.Events.CargoStretch != null)
            for (int k = 0; k < 200; k++) { w.Events.CargoMeters[Found(k)] = 20000 - 90 * k; w.Events.CargoStretch[Found(k)] = 400; }
        int At(string t) => int.Parse(t.Substring(t.Length - 3));
        var name = w.DisplayName; var kind = w.ItemKind; var station = w.StationDish; var dishType = w.DishType; var dishBoost = w.DishBoost;
        var itemType = w.ItemType; var mainMaterial = w.MainMaterial; var pieceKind = w.PieceKind; var pieceTab = w.PieceTab; var pieceMaterial = w.PieceMaterial;
        bool Many(string t, string head) => t != null && t.StartsWith(head, StringComparison.Ordinal);
        w.DisplayName = t => Many(t, "$piece_many") ? "Carved piece " + At(t) : Many(t, "$item_manydish") ? "Stew no. " + At(t) : Many(t, "$item_manygear") ? "Old blade " + At(t) : Many(t, "$item_manyfound") ? "Odd find " + At(t) : name?.Invoke(t);
        w.ItemKind = t => Many(t, "$item_manydish") ? "food" : Many(t, "$item_manygear") ? "gear" : Many(t, "$item_manyfound") ? "other" : kind?.Invoke(t);
        w.StationDish = t => Many(t, "$item_manydish") ? At(t) % 2 == 0 : station != null && station(t);
        w.DishType = t => Many(t, "$item_manydish") ? dishTypes[At(t) % dishTypes.Length] : dishType?.Invoke(t);
        w.DishBoost = t => Many(t, "$item_manydish") ? boosts[At(t) % boosts.Length] : dishBoost?.Invoke(t);
        w.ItemType = t => Many(t, "$item_manygear") ? gearTypes[At(t) % gearTypes.Length] : itemType?.Invoke(t);
        w.MainMaterial = t => Many(t, "$item_manygear") ? materials[At(t) % materials.Length] : mainMaterial?.Invoke(t);
        w.PieceKind = t => Many(t, "$piece_many") ? "built" : pieceKind?.Invoke(t);
        w.PieceTab = t => Many(t, "$piece_many") ? "Mod tab " + (At(t) % 30) : pieceTab?.Invoke(t);   // a mod-heavy hammer: 30 tabs
        w.PieceMaterial = t => Many(t, "$piece_many") ? materials[At(t) % materials.Length] : pieceMaterial?.Invoke(t);
        return w;
    }

    // what a block draws, as PanelUi and the preview draw it: a bar's legend through the one bar form (it folds past BarMaxParts), else its items
    static int Drawn(Block b) => b?.Items == null || b.Id == PanelModel.FoldId ? 0 : b.Kind == "composition" || b.Kind == "facetbar" ? PanelModel.BarFormOf(b.Items).Rows.Count : b.Items.Count;

    // a block and the blocks inside it; a folded part's kinds ride along for its callers (the group's bar, Cargo's numbers) and are not drawn (Drawn)
    static IEnumerable<Block> Nested(Block b) => new[] { b }.Concat(b.Id == PanelModel.FoldId ? Enumerable.Empty<Block>() : (b.Items ?? new List<Block>()).Where(i => i != null).SelectMany(Nested));

    // every page and view of a book: each list entry, each view of its switch, each side of its toggle, each sort order
    static IEnumerable<(string name, PanelState state)> Pages(PanelInput book, bool everyone)
    {
        foreach (Chapter c in Enum.GetValues(typeof(Chapter)))
        {
            var first = PanelModel.Build(book, new PanelState { Chapter = c, Everyone = everyone });
            foreach (var page in first.List.Count > 0 ? first.List.Select(l => l.Id).ToList() : new List<string> { null })
            {
                PanelState At() { var s = new PanelState { Chapter = c, Everyone = everyone }; if (page != null) s.Page[c] = page; return s; }
                var probe = At(); var v = PanelModel.Build(book, probe);
                if (everyone && !v.EveryoneOn) continue;
                var name = (everyone ? "Everyone/" : "") + c + (page != null ? "/" + page : "");
                yield return (name, probe);
                var sw = PanelModel.Content(v).FirstOrDefault(b => b.Kind == "switch" && b.Items != null && b.Items.Count > 1);
                if (sw != null) foreach (var o in sw.Items) { var s = At(); s.View[sw.Id] = o.Id; yield return (name + "/" + o.Id, s); }
                foreach (var t in v.Toggle.Skip(1)) { var s = At(); s.TheyReceived = t.Id == "they"; yield return (name + "/" + t.Id, s); }
                var sort = PanelModel.SortOf(v);
                if (sort != null) foreach (var o in sort.Items.Skip(1)) { var s = At(); s.View[sort.Id] = o.Id; yield return (name + "/sort-" + o.Id, s); }
            }
        }
    }

    public static int Run(DateTime now)
    {
        fails = 0;
        var world = World(now);
        var over = new List<string>(); var tilesOver = new List<string>(); int pages = 0, worst = 0; string worstAt = "";
        foreach (var everyone in new[] { false, true })
            foreach (var (name, state) in Pages(world, everyone))
            {
                var v = PanelModel.Build(world, state); pages++;
                foreach (var b in PanelModel.Content(v).Where(b => !PanelModel.IsBox(b)).SelectMany(Nested))
                {
                    var n = Drawn(b);
                    if (n > worst) { worst = n; worstAt = name + " " + b.Kind; }
                    if (n > ManyMax && !(b.Kind == "feats" && n <= PanelModel.FeatDefs.Length)) over.Add(name + ": " + b.Kind + " '" + (b.Title ?? b.Id) + "' draws " + n);   // 0.9.1: a feats grid holds at most the feat table (it scrolls)
                }
                var tiles = PanelModel.Content(v).Where(b => b.Kind == "itemgrid").Sum(b => b.Items?.Count ?? 0);
                if (tiles > PanelModel.ItemPageMax) tilesOver.Add(name + ": " + tiles + " tiles");
            }
        Check(pages > 40 && over.Count == 0,
              "many kinds: on " + pages + " pages and views of a long-played book (400 piece kinds on 30 tabs, 150 dishes, 150 kinds of gear, 300 finds, 200 cargo items; yours and Everyone's) no block draws more than " +
              ManyMax + " rows, tiles, chips or legend entries (most: " + worst + ", " + worstAt + ")" + (over.Count > 0 ? ": " + string.Join(" | ", over.Take(12)) : ""));
        Check(tilesOver.Count == 0, "many kinds: no page draws more than " + PanelModel.ItemPageMax + " item tiles, its grids' rest folded" + (tilesOver.Count > 0 ? ": " + string.Join(" | ", tilesOver.Take(12)) : ""));

        // the fold itself on Building: the most built first, then the Show all button with the tiles it holds back
        var built = PanelModel.Build(world, Deeds("building"));
        var grid = PanelModel.Content(built).FirstOrDefault(b => b.Kind == "itemgrid");
        var fold = grid?.Items.LastOrDefault();
        var total = PanelModel.ParseCount(PanelModel.Content(built).First(b => b.Kind == "hero").Value);
        var kinds = world.PiecesPlaced.Where(kv => kv.Value > 0 && world.PieceKind(kv.Key) == "built").ToList();
        Check(grid != null && grid.Items.Count == PanelModel.ItemGridMax && fold.Id == PanelModel.ShowAllTarget && fold.Title == "Show all " + PanelModel.Number(kinds.Count) + " pieces" &&
              fold.Value == "+" + PanelModel.Number(kinds.Count - (PanelModel.ItemGridMax - 1)) && string.IsNullOrEmpty(fold.Icon) && PanelModel.ParseCount(grid.Items[0].Value) == kinds.Max(kv => kv.Value),
              "many kinds, Building: " + PanelModel.ItemGridMax + " tiles, the most built first, the last the button \"" + fold?.Value + " " + fold?.Title + "\"");
        // By category: the tabs past the bar's eight fold into one Other group, as the bar does; each group's grid is short
        var byTab = Deeds("building"); var sortKey = PanelModel.SortOf(built).Id; byTab.View[sortKey] = PanelModel.SortGroup;
        var grouped = PanelModel.Build(world, byTab);
        var heads = PanelModel.Content(grouped).Where(b => b.Kind == "section" && b.Tone == PanelModel.SortGroup).ToList();
        var grids = PanelModel.Content(grouped).Where(b => b.Kind == "itemgrid").ToList();
        var tabs = kinds.Select(kv => world.PieceTab(kv.Key) ?? "").Distinct().Count();   // the hammer tabs built on
        Check(heads.Count == PanelModel.BarMaxParts && heads.Last().Title == PanelModel.FoldLabel(tabs - (PanelModel.BarMaxParts - 1)) && grids.Count == heads.Count &&
              Math.Abs(heads.Sum(h => PanelModel.ParseCount(h.Value)) - total) < 0.5 && grids.All(g => g.Items.Last().Id == PanelModel.ShowAllTarget),
              "many kinds, Building by category: " + heads.Count + " groups (the last \"" + heads.LastOrDefault()?.Title + "\") that add up to the " + total + " pieces built, " +
              grids.Sum(g => g.Items.Count) + " tiles, each group ending on Show all");

        // Show all (Joost's call: the full list stays reachable): pressed, every tile and every group where they stand; another page, or the
        // book opened again, starts folded; never saved with the player's choices
        var st = Deeds("building");
        PanelModel.Follow(st, fold.Id);
        var every = PanelModel.Build(world, st);
        var everyTiles = PanelModel.Content(every).Where(b => b.Kind == "itemgrid").SelectMany(b => b.Items).ToList();
        st.View[sortKey] = PanelModel.SortGroup;
        var everyGroup = PanelModel.Build(world, st);
        var everyHeads = PanelModel.Content(everyGroup).Count(b => b.Kind == "section" && b.Tone == PanelModel.SortGroup);
        var shownAll = st.ShowAll; var saved = PanelPrefs.ToJson(st); st.ShowAll = null; var savedFolded = PanelPrefs.ToJson(st); st.ShowAll = shownAll;
        st.View.Remove(sortKey); st.Page[Chapter.Deeds] = "cooking"; PanelModel.Visited(st, Chapter.Deeds, "cooking");
        st.Page[Chapter.Deeds] = "building"; PanelModel.Visited(st, Chapter.Deeds, "building");
        var again = PanelModel.Content(PanelModel.Build(world, st)).Where(b => b.Kind == "itemgrid").SelectMany(b => b.Items).ToList();
        var fellow = Deeds("building"); PanelModel.Follow(fellow, PanelModel.ShowAllTarget); fellow.Player = "Edda";   // another book is another visit
        Check(everyTiles.Count == kinds.Count && everyTiles.All(i => i.Id != PanelModel.ShowAllTarget) && everyHeads == tabs &&
              again.Count == PanelModel.ItemGridMax && again.Last().Id == PanelModel.ShowAllTarget && !PanelModel.ShowingAll(fellow) && shownAll != null && saved == savedFolded,
              "many kinds, Show all: every one of the " + everyTiles.Count + " pieces and all " + everyHeads + " tabs By category; back from Cooking it starts folded (" + again.Count +
              " tiles), another player's book too, and the choice is never saved");
        // a short list keeps every tile: the sample's own Building has no button
        var sample = PanelModel.Build(PanelSample.Full(now), Deeds("building"));
        Check(PanelModel.Content(sample).Where(b => b.Kind == "itemgrid").SelectMany(b => b.Items).All(i => i.Id != PanelModel.ShowAllTarget), "many kinds: the sample's short Building list keeps every tile, no Show all");
        return fails;
    }

    static PanelState Deeds(string page) { var s = new PanelState { Chapter = Chapter.Deeds }; s.Page[Chapter.Deeds] = page; return s; }
}
