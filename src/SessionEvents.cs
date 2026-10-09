using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// Measured by Hearthwoven on the player's own PC, one session (one connection). Absolute session totals, so a resend
    /// replaces and never adds. Keys are "a|b" strings to keep the JSON flat. The running total since install lives in
    /// LocalTotals (previous sessions + this one).
    /// </summary>
    public class SessionEvents
    {
        public readonly Dictionary<string, float> AteFoodMadeBy = new Dictionary<string, float>();   // "maker|food"
        public readonly Dictionary<string, float> AteFromFeastOf = new Dictionary<string, float>();  // "creatorId|feast"
        public readonly Dictionary<string, float> Bought = new Dictionary<string, float>();          // "trader|item" -> stack
        public readonly Dictionary<string, float> Spent = new Dictionary<string, float>();           // "trader" -> coins
        public readonly Dictionary<string, float> SmelterAdded = new Dictionary<string, float>();    // "station|item"
        public readonly Dictionary<string, float> PickaxeHits = new Dictionary<string, float>();     // "rock" -> hits
        public readonly Dictionary<string, float> SkillPractice = new Dictionary<string, float>();   // "skill" -> sum of raise factors
        public readonly Dictionary<string, float> EquippedGearMadeBy = new Dictionary<string, float>(); // "maker|item" -> times equipped
        public readonly Dictionary<string, float> SailedWith = new Dictionary<string, float>();      // "player" -> seconds aboard the same ship
        public readonly Dictionary<string, float> SailedUnderHelmOf = new Dictionary<string, float>(); // "player at the helm" -> seconds
        public readonly Dictionary<string, float> ChopHits = new Dictionary<string, float>();       // "tree or log" -> axe hits
        public readonly Dictionary<string, float> Repairs = new Dictionary<string, float>();        // "piece" -> repairs with the hammer
        public readonly Dictionary<string, float> MapShared = new Dictionary<string, float>();      // "map table" -> times you wrote your map to it
        public readonly Dictionary<string, float> CartMeters = new Dictionary<string, float>();     // "cart" -> metres pulled
        public readonly Dictionary<string, float> PickedUp = new Dictionary<string, float>();       // item token ("$item_wood") -> amount gathered from the world
        public readonly Dictionary<string, float> Planted = new Dictionary<string, float>();        // plant piece token ("$piece_sapling_flax") -> plants you put in the ground
        public readonly Dictionary<string, float> Made = new Dictionary<string, float>();           // item token ("$item_cookedmeat") -> items you made: crafted, or taken off a cooking station by you
        public readonly Dictionary<string, float> Felled = new Dictionary<string, float>();         // "tree" -> trees your axe felled (TreeFalls: the felling blow, then the tree destroyed)
        public readonly Dictionary<string, float> CargoMeters = new Dictionary<string, float>();    // item token -> item-metres carried at the helm or pulling a cart (Cargo.Add: amount aboard x metres moved)
        public readonly Dictionary<string, float> CargoStretch = new Dictionary<string, float>();   // item token -> metres travelled with some of it aboard (item-metres / this = the average amount aboard)
        public readonly Dictionary<string, float> LedMeters = new Dictionary<string, float>();      // creature prefab ("Wolf") -> metres tamed animals of that kind walked (or sailed) following you while your PC hosted them (LedTracker)
        public readonly Dictionary<string, float> BornInCare = new Dictionary<string, float>();     // creature prefab ("Boar_piggy") -> tamed animals born or hatched within 40 m of you while your PC hosted them
        /// <summary>Battle, counted from your own side (ClientHooks): "EnemyHits" / "PlayerHits" = your hits on a foe / another
        /// player as your PC sends them (the game's counters of the same name book only hits on what your PC owns, the owner
        /// trap), "Deaths" = your deaths. Keyed by the game's stat names so they layer on its counters (LocalTotals.Layers).</summary>
        public readonly Dictionary<string, float> Battle = new Dictionary<string, float>();
        public int Blocks, Parries;

        public static void Add(Dictionary<string, float> d, string key, float v = 1f)
        {
            if (string.IsNullOrEmpty(key) || v == 0f || !Json.IsFinite(v)) return;   // NaN/Infinity from a hook or a mod: ignored
            d.TryGetValue(key, out var o); d[key] = o + v;
        }

        /// <summary>
        /// One pickup, exactly (ClientHooks.Pickup): what the carried amount of that item grew by, at most the drop's
        /// stack. A drop that someone already held (your own dropped stack, a fellow's) counts 0: only fresh items from the
        /// world count, the same rule as the game's m_pickedUp flag, but without the game's gap (it skips a pickup that
        /// lands on a stack you already carry). Inventory full: only the part that went in. Refused: 0.
        /// </summary>
        public static int PickedAmount(bool alreadyHeld, int dropStack, int carriedBefore, int carriedAfter) =>
            alreadyHeld ? 0 : System.Math.Max(0, System.Math.Min(dropStack, carriedAfter - carriedBefore));

        /// <summary>
        /// One new piece got its creator (ClientHooks.Planting, on Piece.SetCreator): it counts as one plant you planted when
        /// it is a plant (a Plant component), it had no creator before and now names you. Every plant counts on its own, so a
        /// field placed in one go (PlantEasily's grid, any mod that plants many at once) books every plant, where the game's
        /// own counter (m_piecesPlacedStats, Player.TryPlacePiece) books only the one under the cursor. Returns whether it counted.
        /// </summary>
        public static bool CountPlanting(Dictionary<string, float> planted, string piece, bool isPlant, long creatorBefore, long creatorNow, long you)
        {
            if (planted == null || !isPlant || you == 0 || creatorBefore != 0 || creatorNow != you || string.IsNullOrEmpty(piece)) return false;
            Add(planted, piece); return true;
        }

        /// <summary>
        /// One booking of the game's item-craft counter (ClientHooks.MadeByYou, on PlayerProfile.IncrementStatItemCraft): it
        /// counts as made by you unless it is a cooking station handing out a dish (CookingStation.SpawnItem runs on the
        /// station OWNER's PC for whoever took the dish off, so the game books it to the owner; Hearthwoven counts that dish
        /// on the taker's PC instead, CountTakenOff). The crafting window (cauldron, food table, mead ketill, workbench) is
        /// already the crafter's own. Only the local player's profile. Returns whether it counted.
        /// </summary>
        public static bool CountMade(Dictionary<string, float> made, string item, float amount, bool stationSpawn, bool yourProfile)
        {
            if (made == null || stationSpawn || !yourProfile || string.IsNullOrEmpty(item) || amount <= 0) return false;
            Add(made, item, amount); return true;
        }

        /// <summary>
        /// You took a finished dish off a cooking station (ClientHooks.TakeOff: CookingStation.OnInteract on your PC, any
        /// station that is a CookingStation: grill, iron cooking station, oven, a mod's). <paramref name="rpcArgs"/> are what
        /// it sends to the owner with "RPC_RemoveDoneItem" (user position, amount): the amount includes the cooking-skill
        /// bonus. A burnt one (<paramref name="burnt"/>, the station's overcooked item) is no dish. Returns the amount counted.
        /// </summary>
        public static int CountTakenOff(Dictionary<string, float> made, string dish, bool burnt, object[] rpcArgs)
        {
            if (made == null || burnt || string.IsNullOrEmpty(dish)) return 0;
            var n = rpcArgs != null && rpcArgs.Length >= 2 && rpcArgs[1] is int a && a > 0 ? a : 1;
            Add(made, dish, n); return n;
        }

        /// <summary>Every per-kind tally under its JSON name, in snapshot order: one list for writing, reading and adding.</summary>
        public IEnumerable<KeyValuePair<string, Dictionary<string, float>>> Named()
        {
            KeyValuePair<string, Dictionary<string, float>> P(string k, Dictionary<string, float> d) => new KeyValuePair<string, Dictionary<string, float>>(k, d);
            yield return P("ateFoodMadeBy", AteFoodMadeBy); yield return P("ateFromFeastOf", AteFromFeastOf);
            yield return P("bought", Bought); yield return P("coinsSpent", Spent); yield return P("smelterAdded", SmelterAdded);
            yield return P("pickaxeHits", PickaxeHits); yield return P("skillPractice", SkillPractice);
            yield return P("equippedGearMadeBy", EquippedGearMadeBy); yield return P("sailedWith", SailedWith); yield return P("sailedUnderHelmOf", SailedUnderHelmOf);
            yield return P("chopHits", ChopHits); yield return P("repairs", Repairs); yield return P("mapShared", MapShared); yield return P("cartMeters", CartMeters);
            yield return P("pickedUp", PickedUp); yield return P("planted", Planted); yield return P("made", Made); yield return P("treesFelled", Felled);
            yield return P("battle", Battle);
            yield return P("cargoMeters", CargoMeters); yield return P("cargoStretch", CargoStretch); yield return P("bornInCare", BornInCare); yield return P("ledMeters", LedMeters);
        }

        public void WriteTo(Json j, string key = "measuredThisSession")
        {
            j.Key(key).Open().Num("blocks", Blocks).Num("parries", Parries);
            foreach (var kv in Named()) j.Dict(kv.Key, kv.Value);
            j.Close();
        }

        /// <summary>Fills this from what WriteTo wrote, parsed by MiniJson (null: nothing). Returns this.</summary>
        public SessionEvents ReadFrom(Dictionary<string, object> o)
        {
            if (o == null) return this;
            Blocks = (int)MiniJson.Num(o, "blocks"); Parries = (int)MiniJson.Num(o, "parries");
            foreach (var kv in Named()) MiniJson.Into(MiniJson.Obj(o, kv.Key), kv.Value);
            return this;
        }

        /// <summary>Adds another tally's counts to this one (null: nothing).</summary>
        public void AddAll(SessionEvents other)
        {
            if (other == null) return;
            Blocks += other.Blocks; Parries += other.Parries;
            var theirs = other.Named().ToList(); int i = 0;
            foreach (var mine in Named()) { foreach (var kv in theirs[i].Value) Add(mine.Value, kv.Key, kv.Value); i++; }
        }

        /// <summary>What <paramref name="now"/> holds beyond <paramref name="was"/> (null: nothing before), per key, never below zero: what
        /// one save added to a session (DayHistory). Both are absolute session tallies, so the difference is what happened in between.</summary>
        public static SessionEvents Minus(SessionEvents now, SessionEvents was)
        {
            var d = new SessionEvents();
            if (now == null) return d;
            d.Blocks = System.Math.Max(0, now.Blocks - (was?.Blocks ?? 0)); d.Parries = System.Math.Max(0, now.Parries - (was?.Parries ?? 0));
            var mine = now.Named().ToList(); var before = was?.Named().ToList(); int i = 0;
            foreach (var into in d.Named())
            {
                foreach (var kv in mine[i].Value)
                {
                    float w = 0; if (before != null) before[i].Value.TryGetValue(kv.Key, out w);
                    if (kv.Value - w > 0) Add(into.Value, kv.Key, kv.Value - w);
                }
                i++;
            }
            return d;
        }

        /// <summary>A new tally with the counts of all parts added up (nulls skipped); the parts stay unchanged.</summary>
        public static SessionEvents Sum(params SessionEvents[] parts)
        {
            var s = new SessionEvents();
            foreach (var p in parts) s.AddAll(p);
            return s;
        }
    }
}
