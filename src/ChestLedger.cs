using System;
using System.Collections.Generic;

namespace Hearthwoven
{
    /// <summary>
    /// Pure bookkeeping for containers (no Unity calls), so every integrity rule is unit-tested outside the game.
    /// Rules: mods/Hearthwoven/INTEGRITY.md. In short: the diff between the server's own previous copy and the new copy is
    /// the truth about WHAT changed (never missed, never counted twice); attribution says WHO did it.
    /// </summary>
    public static class ChestLedger
    {
        public struct Slot
        {
            public int X, Y, Prefab, Quality, Stack;
            public string Maker;
            /// <summary>Same item = same prefab, same maker, same quality. Moving or splitting stacks inside a chest keeps the sum per key.</summary>
            public string Key => Prefab + "|" + (Maker ?? "") + "|" + Quality;
        }

        /// <summary>A request from a player who is NOT the container's manager (MultiUserChest), seen as it passes the server.</summary>
        public class Claim
        {
            public long Peer;
            public string Kind;        // "add", "remove", "consume", "drop"
            public int X = -1, Y = -1; // slot in the container (remove / consume / drop, or the target of a swap)
            public string Key;         // item key (add, or a remove whose item is known from the response)
            public int Amount;
            public int RequestId = -1; // MUC's request id (add / remove); -1 = none (consume / drop)
            public bool Swap;          // the item an add may push out of an occupied slot (until the response says)
            public double T;           // seconds, server clock
        }

        public class Event
        {
            public long Peer;
            public string Key, Action, Via;
            public int Count;
        }

        /// <summary>
        /// Inventory bytes in the game's own format (Inventory.Save, item version 108+). Empty input: empty list.
        /// An older format (still loadable by the game) returns null: we cannot read it, so it is never diffed (INTEGRITY C11).
        /// </summary>
        public static List<Slot> Parse(byte[] bytes)
        {
            var list = new List<Slot>();
            if (bytes == null || bytes.Length < 6) return list;
            var p = new ZPackage(bytes);
            var ver = (Version.Item)p.ReadInt();
            if (ver < Version.Item.Smaller) return null;
            int n = p.ReadUShort();
            for (int k = 0; k < n; k++)
            {
                var s = new Slot();
                p.ReadInt();                       // durability
                s.X = p.ReadByte(); s.Y = p.ReadByte();
                p.ReadByte();                      // world level
                byte b = p.ReadByte();
                s.Quality = (b & 4) != 0 ? p.ReadUShort() : 1;
                s.Stack = (b & 8) == 0 ? 1 : p.ReadUShort();
                if ((b & 0x10) != 0) p.ReadInt();  // variant
                s.Maker = "";
                if ((b & 0x20) != 0) { p.ReadLong(); s.Maker = p.ReadString(); }
                s.Prefab = (b & 0x40) != 0 ? p.ReadInt() : 0;
                int custom = (b & 0x80) != 0 ? p.ReadNumItems() : 0;
                for (int c = 0; c < custom; c++) { p.ReadString(); p.ReadString(); }
                if (ver >= Version.Item.ChunksNCheats || ver == Version.Item.AbandonedDN) p.ReadByte();
                if (s.Prefab != 0) list.Add(s);
            }
            return list;
        }

        public static Dictionary<string, int> Count(List<Slot> slots)
        {
            var d = new Dictionary<string, int>();
            foreach (var s in slots) { d.TryGetValue(s.Key, out var o); d[s.Key] = o + s.Stack; }
            return d;
        }

        /// <summary>after - before, per key; positive = put in, negative = taken out.</summary>
        public static Dictionary<string, int> Diff(Dictionary<string, int> before, Dictionary<string, int> after)
        {
            var d = new Dictionary<string, int>();
            foreach (var kv in after) { before.TryGetValue(kv.Key, out var b); if (kv.Value != b) d[kv.Key] = kv.Value - b; }
            foreach (var kv in before) if (!after.ContainsKey(kv.Key)) d[kv.Key] = -kv.Value;
            return d;
        }

        /// <summary>
        /// Splits one container change into events.
        /// kind: "chest", "cart", "ship", "grave" (tombstone), "crate" (cargo of a sunk ship or broken cart), "feeder"
        ///       (a chest that a mod empties by itself, e.g. SmelterUpgrades' Stoker's chest).
        /// before: the server's previous copy; null = unreadable (old item format), then nothing is counted.
        /// firstCopy: the server never had an item list for this object before. playerBuilt: it has a creator.
        /// inUse: someone has it open (the game's InUse flag). claims: shared-chest requests, oldest first; matched ones are removed.
        /// sender: the PC that sent the new copy; owner: the container's manager.
        /// </summary>
        public static List<Event> Attribute(List<Slot> before, List<Slot> after, string kind, bool firstCopy, bool playerBuilt,
                                            List<Claim> claims, long sender, long owner, bool inUse = true)
        {
            var events = new List<Event>();
            if (before == null || after == null) return events;
            var diff = Diff(Count(before), Count(after));
            if (diff.Count == 0) return events;

            // A world chest (dungeon, camp) whose loot appears: not a gift. A crate's first copy is the cargo it was made from.
            if (firstCopy && !playerBuilt && kind != "grave")
            {
                foreach (var kv in diff) if (kv.Value > 0) events.Add(new Event { Peer = sender, Key = kv.Key, Action = kind == "crate" ? "salvage-drop" : "loot-spawned", Via = "sender", Count = kv.Value });
                return events;
            }

            // Shared-chest requests (MultiUserChest) first: they name exactly who moved what.
            if (claims != null)
            {
                var slotAt = new Dictionary<long, Slot>();
                foreach (var s in before) slotAt[((long)s.X << 32) | (uint)s.Y] = s;
                for (int i = 0; i < claims.Count; i++)
                {
                    var c = claims[i];
                    int sign = c.Kind == "add" ? +1 : -1;
                    string key = c.Key;
                    if (key == null)
                    {
                        if (!slotAt.TryGetValue(((long)c.X << 32) | (uint)c.Y, out var s)) continue;
                        key = s.Key;
                    }
                    if (!diff.TryGetValue(key, out var left) || Math.Sign(left) != sign) continue;
                    int m = Math.Min(Math.Abs(left), c.Amount);
                    if (m <= 0) continue;
                    events.Add(new Event { Peer = c.Peer, Key = key, Action = Act(kind, sign), Via = "shared-chest", Count = m });
                    left -= sign * m;
                    if (left == 0) diff.Remove(key); else diff[key] = left;
                    claims.RemoveAt(i--);
                }
            }

            // The rest: whoever's PC sent the change (vanilla: the one who has the chest open).
            foreach (var kv in diff)
            {
                int sign = Math.Sign(kv.Value);
                string action = Act(kind, sign), via = sender == owner ? "sender" : "sender-not-owner";
                if (!inUse)
                {
                    via = "unattended";   // nobody had it open: a mod or automation, still on this PC
                    if (kind == "feeder" && sign < 0) action = "auto-feed";
                }
                events.Add(new Event { Peer = sender, Key = kv.Key, Action = action, Via = via, Count = Math.Abs(kv.Value) });
            }
            return events;
        }

        static string Act(string kind, int sign)
        {
            switch (kind)
            {
                case "grave": return sign > 0 ? "grave-drop" : "grave-take";
                case "crate": return sign > 0 ? "salvage-drop" : "salvage-take";
                default: return sign > 0 ? "put" : "take";
            }
        }

        /// <summary>Drops claims that never matched a change and got no answer (the manager left).</summary>
        public static void Expire(List<Claim> claims, double now, double maxAge = 15.0) => claims.RemoveAll(c => now - c.T > maxAge);

        /// <summary>Reads a MultiUserChest request package (MUC 0.6.x formats). Empty for anything else or an unreadable package.</summary>
        public static List<Claim> ReadMucRequest(string method, ZPackage request, long peer, double now)
        {
            var list = new List<Claim>();
            try
            {
                switch (method)
                {
                    case "MUC_RequestItemAdd":
                    {
                        int id = request.ReadInt(); var to = request.ReadVector2i();
                        var add = ReadItem(request, peer, now, out _);
                        bool allowSwitch = request.ReadBool();
                        if (add == null) break;
                        add.RequestId = id; list.Add(add);
                        // dropping onto an occupied slot may push that item out to the requester (MUC CanStack, allowSwitch)
                        if (allowSwitch && to.x >= 0 && to.y >= 0)
                            list.Add(new Claim { Peer = peer, Kind = "remove", X = to.x, Y = to.y, Amount = int.MaxValue, RequestId = id, Swap = true, T = now });
                        break;
                    }
                    case "MUC_RequestItemRemove":
                    {
                        int id = request.ReadInt(); var from = request.ReadVector2i(); request.ReadVector2i(); int amount = request.ReadInt();
                        list.Add(new Claim { Peer = peer, Kind = "remove", X = from.x, Y = from.y, Amount = amount, RequestId = id, T = now });
                        var swapIn = ReadItem(request, peer, now, out _);   // an item swapped back into the chest
                        if (swapIn != null) { swapIn.RequestId = id; swapIn.Swap = true; list.Add(swapIn); }
                        break;
                    }
                    case "MUC_RequestItemConsume":
                    {
                        int x = request.ReadInt(), y = request.ReadInt();
                        list.Add(new Claim { Peer = peer, Kind = "consume", X = x, Y = y, Amount = 1, T = now });
                        break;
                    }
                    case "MUC_RequestItemDrop":
                    {
                        var slot = request.ReadVector2i(); int amount = request.ReadInt();
                        list.Add(new Claim { Peer = peer, Kind = "drop", X = slot.x, Y = slot.y, Amount = amount, T = now });
                        break;
                    }
                }
            }
            catch (Exception) { }
            return list;
        }

        /// <summary>
        /// The manager's answer to a request (it passes the server too): a failed request is removed, a partial one gets
        /// the amount that really moved, and a swap is confirmed with the item that really came out (INTEGRITY C8).
        /// requester = the answer's target peer. Returns true when the package was a MUC response.
        /// </summary>
        public static bool ApplyMucResponse(string method, ZPackage resp, long requester, List<Claim> claims)
        {
            if (claims == null) return false;
            try
            {
                switch (method)
                {
                    case "MUC_RequestItemAddResponse":
                    {
                        int id = resp.ReadInt(); resp.ReadVector2i(); bool ok = resp.ReadBool(); int amount = resp.ReadInt();
                        var back = ReadItem(resp, requester, 0, out _);
                        var add = claims.Find(c => c.Peer == requester && c.RequestId == id && c.Kind == "add");
                        var swap = claims.Find(c => c.Peer == requester && c.RequestId == id && c.Swap);
                        if (!ok || amount <= 0) { claims.Remove(add); claims.Remove(swap); return true; }
                        if (add != null) add.Amount = amount;
                        // what came back is either the rest of the dragged stack or the chest's own item (a real take)
                        if (swap != null)
                        {
                            if (back != null && (add == null || back.Key != add.Key)) { swap.Key = back.Key; swap.Amount = back.Amount; swap.Swap = false; }
                            else claims.Remove(swap);
                        }
                        return true;
                    }
                    case "MUC_RequestItemRemoveResponse":
                    {
                        int id = resp.ReadInt(); bool ok = resp.ReadBool(); int amount = resp.ReadInt(); bool switched = resp.ReadBool();
                        var rem = claims.Find(c => c.Peer == requester && c.RequestId == id && c.Kind == "remove");
                        var swapIn = claims.Find(c => c.Peer == requester && c.RequestId == id && c.Kind == "add" && c.Swap);
                        if (!ok || amount <= 0) { claims.Remove(rem); claims.Remove(swapIn); return true; }
                        if (rem != null) rem.Amount = amount;
                        if (swapIn != null && !switched) claims.Remove(swapIn);
                        return true;
                    }
                    case "MUC_RequestItemConsumeResponse":
                    {
                        ReadItem(resp, requester, 0, out _); bool ok = resp.ReadBool(); int amount = resp.ReadInt();
                        Settle(claims, requester, "consume", ok, amount);
                        return true;
                    }
                    case "MUC_RequestItemDropResponse":
                    {
                        ReadItem(resp, requester, 0, out _); resp.ReadZDOID(); bool ok = resp.ReadBool(); int amount = resp.ReadInt();
                        Settle(claims, requester, "drop", ok, amount);
                        return true;
                    }
                }
            }
            catch (Exception) { }
            return false;
        }

        static void Settle(List<Claim> claims, long requester, string kind, bool ok, int amount)
        {
            var c = claims.Find(x => x.Peer == requester && x.Kind == kind);   // oldest open one: these have no request id
            if (c == null) return;
            if (!ok || amount <= 0) claims.Remove(c); else c.Amount = amount;
        }

        /// <summary>
        /// MUC's item format (InventoryHelper.WriteItemToPackage): bool present, prefab, stack, durability, pos, quality,
        /// variant, crafter id, crafter name, world level, picked up, cheated, custom data. Read to the end so what follows lines up.
        /// The game keeps a maker only with a crafter id, so a name without one is ignored (as in the chest's own bytes).
        /// </summary>
        static Claim ReadItem(ZPackage p, long peer, double now, out bool present)
        {
            present = p.ReadBool();
            if (!present) return null;
            var prefab = p.ReadString(); int stack = p.ReadInt(); p.ReadSingle(); p.ReadVector2i();
            int quality = p.ReadInt(); p.ReadInt(); long crafterId = p.ReadLong(); var maker = p.ReadString();
            p.ReadInt(); p.ReadBool(); p.ReadBool();
            int custom = p.ReadInt();
            for (int i = 0; i < custom; i++) { p.ReadString(); p.ReadString(); }
            if (crafterId == 0) maker = "";
            return new Claim { Peer = peer, Kind = "add", Key = StringExtensionMethods.GetStableHashCode(prefab) + "|" + maker + "|" + quality, Amount = stack, T = now };
        }
    }
}
