using System.Collections.Generic;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// SAMPLE data (fictional Rowan, Edda, Finch and Tor) for the 0.6 pages: cargo carried (Voyages > Sailing, Company >
    /// Together) and born in your care (Deeds > Taming). Kept out of PanelSample so that file stays as it is; PanelSample
    /// calls Cargo and Born once per player. Values are assigned, never added, so calling twice on the same events
    /// changes nothing. Item-metres and metres travelled with the item aboard, as Cargo.Add would have summed them.
    /// </summary>
    public static class CargoSample
    {
        // the game's English names, the stand-in for PanelUi's Localized outside the game
        static readonly Dictionary<string, string> Named = new Dictionary<string, string>
        {
            ["$item_ironscrap"] = "Iron Scrap", ["$item_coal"] = "Coal", ["Boar_piggy"] = "Piglet", ["Wolf_cub"] = "Wolf Cub", ["Lox_Calf"] = "Lox Calf", ["Chicken"] = "Chicken",
        };

        static void Register() { foreach (var kv in Named) if (!PanelSample.Names.ContainsKey(kv.Key)) PanelSample.Names[kv.Key] = kv.Value; }

        static void Set(SessionEvents ev, string item, float itemMetres, float stretch) { ev.CargoMeters[item] = itemMetres; ev.CargoStretch[item] = stretch; }

        /// <summary>What the sample player carried at the helm or pulling a cart; nothing for a player the sample has no haul for.</summary>
        public static void Cargo(SessionEvents ev, string player)
        {
            Register();
            switch (player)
            {
                case "Rowan":   // the iron haul: about 550 scrap aboard over 2.6 km, the cart's wood, stone by ship
                    Set(ev, "$item_ironscrap", 1430000f, 2600f); Set(ev, "$item_wood", 410000f, 4100f); Set(ev, "$item_stone", 170000f, 1700f); Set(ev, "$item_coal", 60000f, 1200f);
                    break;
                case "Edda": Set(ev, "$item_ironscrap", 1900000f, 3000f); Set(ev, "$item_wood", 300000f, 2000f); break;
                case "Tor": Set(ev, "$item_wood", 150000f, 1500f); Set(ev, "$item_stone", 90000f, 900f); break;
            }
        }

        /// <summary>The sample player's part of the server's book (0.6): cargo loaded and unloaded (item-metres per item token) and the
        /// tamed young born near them. Rowan loads iron that Edda unloads; Tor loads and unloads his own. null: none.</summary>
        public static ServerBook.Shared Book(string player)
        {
            Register();
            var b = new ServerBook.Shared { CargoFrom = new System.DateTime(2026, 10, 8, 19, 52, 0, System.DateTimeKind.Utc), BornFrom = new System.DateTime(2026, 10, 9, 12, 0, 0, System.DateTimeKind.Utc) };
            switch (player)
            {
                case "Rowan": b.Sent["$item_ironscrap"] = 1430000; b.Sent["$item_wood"] = 4000; b.Delivered["$item_ironscrap"] = 84000; b.Delivered["$item_wood"] = 49000; b.BornNear["Boar_piggy"] = 4; b.BornNear["Wolf_cub"] = 1; break;
                case "Edda": b.Delivered["$item_ironscrap"] = 1425000; break;
                case "Tor": b.Sent["$item_ironscrap"] = 214000; b.Delivered["$item_ironscrap"] = 442000; b.BornNear["Boar_piggy"] = 3; break;
                default: return null;
            }
            return b;
        }

        /// <summary>The animals born or hatched near the sample player while their PC hosted them.</summary>
        public static void Born(SessionEvents ev, string player)
        {
            Register();
            switch (player)
            {
                case "Rowan": ev.BornInCare["Boar_piggy"] = 5f; ev.BornInCare["Wolf_cub"] = 2f; ev.BornInCare["Lox_Calf"] = 1f; ev.BornInCare["Chicken"] = 3f; break;
                case "Tor": ev.BornInCare["Boar_piggy"] = 3f; break;
            }
        }
    }
}
