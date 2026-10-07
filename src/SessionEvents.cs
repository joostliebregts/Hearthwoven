using System.Collections.Generic;

namespace Hearthwoven
{
    /// <summary>
    /// Measured since Hearthwoven was installed (this session), on the player's own PC. Absolute session totals,
    /// so a resend replaces and never adds. Keys are "a|b" strings to keep the JSON flat.
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
        public int Blocks, Parries;

        public static void Add(Dictionary<string, float> d, string key, float v = 1f)
        {
            if (string.IsNullOrEmpty(key) || v == 0f) return;
            d.TryGetValue(key, out var o); d[key] = o + v;
        }

        public void WriteTo(Json j)
        {
            j.Key("measuredThisSession").Open()
                .Num("blocks", Blocks).Num("parries", Parries)
                .Dict("ateFoodMadeBy", AteFoodMadeBy).Dict("ateFromFeastOf", AteFromFeastOf)
                .Dict("bought", Bought).Dict("coinsSpent", Spent).Dict("smelterAdded", SmelterAdded)
                .Dict("pickaxeHits", PickaxeHits).Dict("skillPractice", SkillPractice)
                .Dict("equippedGearMadeBy", EquippedGearMadeBy).Dict("sailedWith", SailedWith).Dict("sailedUnderHelmOf", SailedUnderHelmOf)
                .Dict("chopHits", ChopHits).Dict("repairs", Repairs).Dict("mapShared", MapShared).Dict("cartMeters", CartMeters)
                .Close();
        }
    }
}
