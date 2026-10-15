using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The sample's older sessions of a friend (0.8.6, a friend's older feed, FeedBlocks.cs): what the server lists for Edda besides her running copy, and one of
    /// them fetched. Built the way a real one is: her recorder's fights, cut as her PC keeps them (KeptFights.Take) and written and read as the server stores them
    /// (FeedBlock). Fictional players only: Edda, with Rowan, Tor and Finch who share beside her.
    /// </summary>
    public static class SampleSessions
    {
        static readonly string[][] Kinds =
        {
            new[] { "Greydwarf", "BlackForest" }, new[] { "Greydwarf_Shaman", "BlackForest" }, new[] { "Troll", "BlackForest" }, new[] { "Draugr", "Swamp" },
            new[] { "Draugr_Elite", "Swamp" }, new[] { "Wraith", "Swamp" }, new[] { "Skeleton", "Mountain" }, new[] { "Blob", "Swamp" },
        };

        /// <summary>One evening of <paramref name="fights"/> fights of <paramref name="perFight"/> foes that ended at <paramref name="end"/>, as the server holds it: a block of
        /// <paramref name="who"/> (fights a few minutes apart, a foe kind or two each, Rowan and Tor near in each).</summary>
        public static FeedBlock Block(string id, string who, DateTime end, int fights, int perFight)
        {
            var rec = new BattleRecorder();
            var start = end.AddMinutes(-(fights * (perFight * 0.2 + 3) + 5)); var t = start; uint seq = 0;
            for (int f = 0; f < fights; f++)
            {
                var kind = Kinds[(f + id.Length) % Kinds.Length];
                for (int k = 0; k < perFight; k++)
                {
                    var foe = new FoeId(-800, ++seq); var kn = (k % 4 == 3) ? Kinds[(f + k) % Kinds.Length] : kind;
                    var token = "$enemy_" + kn[0].ToLowerInvariant();
                    var dmg = new HitData.DamageTypes { m_slash = 30 + 7 * ((f + k) % 5), m_pierce = k % 2 == 0 ? 12 : 0 };
                    rec.Dealt(t, foe, kn[0], token, kn[1], dmg); rec.Dealt(t.AddSeconds(2), foe, kn[0], token, kn[1], dmg);
                    if (k % 3 == 0) rec.Received(t.AddSeconds(3), foe, kn[0], token, kn[1], new HitData.DamageTypes { m_blunt = 9 + f % 4 });
                    if ((f + k) % 7 != 6) { rec.Killed(t.AddSeconds(4), token, 1); rec.Destroyed(t.AddSeconds(4.5), foe); }
                    t = t.AddSeconds(12);
                }
                rec.Near(t.AddSeconds(-20), "Rowan", 12); rec.Near(t.AddSeconds(-10), "Tor", 12);
                t = t.AddMinutes(3);
            }
            rec.Tick(t);
            var shares = new HashSet<string>(new[] { "Rowan", "Tor", "Finch", who }, StringComparer.OrdinalIgnoreCase);
            return FeedBlock.Read(FeedBlock.Write(KeptFights.Take("Sample", id, start, end, rec, who, shares.Contains, null)));
        }

        /// <summary>Gives <paramref name="friend"/> (Edda's book) the server's list of her earlier sessions: one three days back and fetched (<paramref name="foes"/> foes, in
        /// fights of five), one nine days back that is not here yet (120 foes), as the book of someone whose server keeps blocks reads them. Returns the fetched session's block.</summary>
        public static FeedBlock Give(PanelInput friend, DateTime now, int foes = 60)
        {
            var near = Block("5be1c0d9a4f2", friend.PlayerName, now.AddDays(-3).AddHours(-2), Math.Max(1, foes / 5), 5);
            var far = new BlockRow { Id = "3f9a0c1e2b7d", FromUtc = now.AddDays(-9).AddHours(-5), ToUtc = now.AddDays(-9).AddHours(-1), Foes = 120 };
            friend.BlockRows = new List<BlockRow> { near.Row, far };
            friend.BlockSessions = new Dictionary<string, KeptSession> { [near.Id] = near.Session };
            friend.BlockStates = new Dictionary<string, string>();
            friend.BlocksStamp = "sample";
            near.Session.Sides[0].Name = friend.PlayerName;
            return near;
        }
    }
}
