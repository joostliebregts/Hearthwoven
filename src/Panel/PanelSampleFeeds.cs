using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The sample's deed feeds (0.9, Chapters/DeedFeedModel.cs; work/hearthwoven-0.9/TRACK-feeds.md): Rowan's deed stretches of this session and of an
    /// evening the day before (DeedSession as the record track keeps them), and Edda's earlier sessions as the server lists and serves them, in the same
    /// blocks as her earlier fights (FeedBlock written and read as the server stores it). Fictional players only.
    /// </summary>
    public static class FeedSample
    {
        public const string LiveId = "c0ffee000001", YesterdayId = "c0ffee000002", EddaNearId = "e0dda0000001", EddaFarId = "e0dda0000002";

        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["$item_helmet_bronze"] = "Bronze Helmet", ["$item_mace_iron"] = "Iron Mace", ["$item_chest_bronze"] = "Bronze Plate Cuirass", ["$item_legs_bronze"] = "Bronze Plate Leggings",
            ["$item_bronze"] = "Bronze", ["$item_meadbasetasty"] = "Tasty Mead Base", ["$piece_woodbeam"] = "Wood Beam", ["$piece_woodroof"] = "Wood Roof",
            ["$piece_woodpole"] = "Wood Pole", ["$piece_feast_meadows"] = "Whole Roasted Meadow Boar", ["$piece_stonewall1x1"] = "Stone Wall 1x1", ["$item_queensjam"] = "Queen's Jam",
            ["Beech1"] = "Beech", ["Oak1"] = "Oak", ["FirTree"] = "Fir",
            ["$item_raspberries"] = "Raspberries", ["$item_mushroomcommon"] = "Mushroom", ["$item_feathers"] = "Feathers", ["$item_leatherscraps"] = "Leather Scraps",
        };

        static DeedStretch Stretch(string family, DateTime from, DateTime to, params (string key, float n)[] counts)
        {
            var s = new DeedStretch { Family = family, FromUtc = from, ToUtc = to };
            foreach (var (key, n) in counts) s.Counts[key] = n;
            return s;
        }

        static DeedMoment Made(string token, DateTime at, int n = 1) => new DeedMoment { Kind = DeedMoment.Made, Token = token, Utc = at, Count = n };
        static DeedMoment Up(string token, DateTime at, int from, int to) => new DeedMoment { Kind = DeedMoment.Upgraded, Token = token, Utc = at, From = from, To = to, Station = "forge", StationLevel = 3 };
        static DeedMoment First(string token, DateTime at) => new DeedMoment { Kind = DeedMoment.First, Token = token, Utc = at };
        static DeedMoment Feast(string token, DateTime at) => new DeedMoment { Kind = DeedMoment.Feast, Token = token, Utc = at };
        static DateTime M(DateTime t) => new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);

        /// <summary>Rowan's deed stretches: this session (crafting with two upgrades and a first, cooking with a feast set out, building, woodcutting) and an evening
        /// the day before (crafting early in it, its times kept in tens, mining and building later).</summary>
        public static List<DeedSession> Rowan(DateTime now, bool busy = false)
        {
            var n = M(now);
            var live = new DeedSession { World = "Sample", Id = LiveId, FromUtc = n.AddHours(busy ? -4 : -3), ToUtc = n, Live = true };
            var craft = Stretch(DeedStretches.Crafting, n.AddMinutes(-55), n.AddMinutes(-37), ("made|$item_axe_bronze", 2), ("made|$item_helmet_bronze", 1), ("made|$item_mace_iron", 1), ("made|$item_arrow_wood", 40), ("made|$item_bronze", 6));
            craft.Moments.AddRange(new[] { Up("$item_sword_iron", n.AddMinutes(-38).AddSeconds(10), 2, 3), Up("$item_sword_iron", n.AddMinutes(-45).AddSeconds(42), 1, 2),
                                           Made("$item_mace_iron", n.AddMinutes(-51)), First("$item_mace_iron", n.AddMinutes(-51)), Made("$item_helmet_bronze", n.AddMinutes(-54)), Made("$item_axe_bronze", n.AddMinutes(-55), 2) });
            var cook = Stretch(DeedStretches.Cooking, n.AddMinutes(-100), n.AddMinutes(-86), ("made|$item_carrotsoup", 4), ("made|$item_fishwraps", 4), ("placed|$piece_feast_meadows", 1));
            cook.Moments.Add(Feast("$piece_feast_meadows", n.AddMinutes(-88)));
            var build = Stretch(DeedStretches.Building, n.AddMinutes(-150), n.AddMinutes(-105), ("placed|$piece_woodwall", 40), ("placed|$piece_woodfloor2x2", 24), ("placed|$piece_woodbeam", 12),
                                ("placed|$piece_woodroof", 18), ("placed|$piece_woodpole", 6), ("recovered|$item_wood", 18));
            build.Moments.Add(First("$piece_woodroof", n.AddMinutes(-131)));
            var wood = Stretch(DeedStretches.Woodcutting, n.AddMinutes(-172), n.AddMinutes(-156), ("treesFelled|Beech1", 3), ("treesFelled|Oak1", 1), ("pickedUp|$item_wood", 46), ("pickedUp|$item_finewood", 3), ("chopHits|Beech1", 40), ("chopHits|Oak1", 22));
            live.Stretches.AddRange(new[] { craft, cook, build, wood });
            if (busy)
            {
                // 0.9 all-feed, a busy evening touching every family the deed log records: the sail over, what was brought in on the shore, a wolf
                // tamed, the skills practiced while crafting and fighting (the fights come from the sample's battle record)
                var sail = Stretch(DeedStretches.Sailing, n.AddMinutes(-236), n.AddMinutes(-204), ("counters|DistanceSail", 4200), ("counters|" + DeedLog.CargoTotal, 186000));
                var brought = Stretch(DeedStretches.BroughtIn, n.AddMinutes(-200), n.AddMinutes(-184), ("pickedUp|$item_raspberries", 18), ("pickedUp|$item_mushroomcommon", 9), ("pickedUp|$item_feathers", 6), ("pickedUp|$item_leatherscraps", 11));
                var tame = Stretch(DeedStretches.Taming, n.AddMinutes(-80), n.AddMinutes(-76), ("counters|CreatureTamed", 1));
                var practice = Stretch(DeedStretches.Practice, n.AddMinutes(-58), n.AddMinutes(-30), ("skillPractice|Swords", 3.2f), ("skillPractice|Blocking", 1.4f), ("skillPractice|Run", 0.8f), ("skillPractice|Jump", 0.2f));
                live.Stretches.AddRange(new[] { practice, tame, brought, sail });
            }

            // the morning before (local time: the sample's clock is UTC + 2), 3 h 40 min: the crafting in its first half hour lies in minutes kept in tens
            var b = n.AddMinutes(-870);
            var y = new DeedSession { World = "Sample", Id = YesterdayId, FromUtc = b, ToUtc = b.AddMinutes(220) };
            var yCraft = Stretch(DeedStretches.Crafting, b.AddMinutes(10), b.AddMinutes(40), ("made|$item_chest_bronze", 1), ("made|$item_legs_bronze", 1), ("made|$item_shield_wood", 2));
            yCraft.Moments.AddRange(new[] { Made("$item_legs_bronze", b.AddMinutes(30)), Made("$item_chest_bronze", b.AddMinutes(20)), First("$item_chest_bronze", b.AddMinutes(20)), Made("$item_shield_wood", b.AddMinutes(10), 2) });
            var yCraft2 = Stretch(DeedStretches.Crafting, b.AddMinutes(185), b.AddMinutes(192), ("made|$item_arrow_wood", 20));
            yCraft2.Moments.Add(Up("$item_axe_bronze", b.AddMinutes(189).AddSeconds(31), 1, 2));
            var yMine = Stretch(DeedStretches.Mining, b.AddMinutes(90), b.AddMinutes(140), ("pickedUp|$item_copperore", 34), ("pickedUp|$item_tinore", 12), ("pickedUp|$item_stone", 60), ("pickaxeHits|rock4_copper", 120));
            var yBuild = Stretch(DeedStretches.Building, b.AddMinutes(160), b.AddMinutes(180), ("placed|$piece_stonewall1x1", 14), ("placed|$piece_woodwall", 6));
            y.Stretches.AddRange(new[] { yCraft2, yBuild, yMine, yCraft });
            return new List<DeedSession> { live, y };
        }

        /// <summary>The sample's game data for the feeds: names of the feed's own items, every upgradable item's highest level (4).</summary>
        static void Data(PanelInput p)
        {
            var old = p.DisplayName;
            p.DisplayName = t => t != null && Names.TryGetValue(t, out var nm) ? nm : old?.Invoke(t);
            p.MaxQuality = t => t != null && t.StartsWith("$item_", StringComparison.Ordinal) && (t.Contains("sword") || t.Contains("axe") || t.Contains("mace") || t.Contains("helmet")) ? 4 : 0;
        }

        /// <summary>Rowan's book with the deed feeds' sample (<paramref name="state"/>: "yours", "busy" every family of a busy evening (0.9 all-feed), "empty"
        /// no stretch yet, "unread" not recorded yet).</summary>
        public static PanelInput Yours(PanelInput full, string state = "yours")
        {
            var p = full.ShallowCopy();
            Data(p);
            p.FirstsFrom = p.NowUtc.Date.AddDays(-6).AddHours(17); p.UpgradesFrom = p.FirstsFrom;
            p.DeedStretches = state == "unread" ? null : state == "empty" ? new List<DeedSession>() : Rowan(p.NowUtc, state == "busy");
            return p;
        }

        /// <summary>Edda's book (<paramref name="edda"/>, her copy): her sessions on the server with deed stretches, as this PC's list reads them: one two days back,
        /// fetched (crafting, cooking, farming), one five days back still on the server (<paramref name="farState"/>: null not asked, "loading" or "failed").
        /// <paramref name="state"/> "none": the server lists none of hers; "oldserver": a server before 0.9.</summary>
        public static PanelInput Edda(PanelInput edda, DateTime now, string farState = "loading", string state = "rows")
        {
            var p = edda.ShallowCopy();
            Data(p); p.ModVersion = "0.9.0";
            p.DeedsOnServer = state != "oldserver";
            p.BlockStates = new Dictionary<string, string>(); p.BlocksStamp = "deeds-" + state + "-" + farState;
            if (state != "rows") { p.DeedBlockRows = null; p.DeedBlockSessions = null; return p; }
            var day = M(now).Date.AddDays(-2);
            var s = new DeedSession { World = "", Id = EddaNearId, FromUtc = day.AddHours(17), ToUtc = day.AddHours(20).AddMinutes(30) };
            var c = Stretch(DeedStretches.Crafting, day.AddHours(19).AddMinutes(2), day.AddHours(19).AddMinutes(24), ("made|$item_helmet_bronze", 1), ("made|$item_arrow_wood", 60));
            c.Moments.AddRange(new[] { Up("$item_helmet_bronze", day.AddHours(19).AddMinutes(20).AddSeconds(5), 1, 2), Made("$item_helmet_bronze", day.AddHours(19).AddMinutes(4)) });
            var k = Stretch(DeedStretches.Cooking, day.AddHours(18).AddMinutes(10), day.AddHours(18).AddMinutes(30), ("made|$item_queensjam", 4), ("made|$item_carrotsoup", 2));
            var f = Stretch(DeedStretches.Farming, day.AddHours(17).AddMinutes(20), day.AddHours(17).AddMinutes(50), ("planted|$piece_sapling_carrot", 30), ("picked|Carrot", 24));
            s.Stretches.AddRange(new[] { c, k, f });
            var block = FeedBlock.Read(FeedBlock.Write(null, s));   // as the server stores and serves it
            var far = new BlockRow { Id = EddaFarId, FromUtc = day.AddDays(-3).AddHours(16), ToUtc = day.AddDays(-3).AddHours(19), Stretches = 5 };
            p.DeedBlockRows = new List<BlockRow> { block.Row, far };
            p.DeedBlockSessions = new Dictionary<string, DeedSession> { [block.Id] = block.Deeds };
            if (farState != null) p.BlockStates[far.Id] = farState;
            return p;
        }
    }
}
