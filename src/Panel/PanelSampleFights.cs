using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// The fight pages' sample (0.9, Chapters/FightPages.cs): fictional fights for Rowan's book with Edda, Finch and Tor, and Ylva who does not
    /// share. Not part of the one sample world yet (SampleWorld): the world's fights come from its battle record, and the fight adapter that turns
    /// a record into FightViews is built apart (v09-fights); until then the previews attach this book in named scenarios. Every number is a
    /// player's part of one foe kind; the totals are worked out from them (FightView), so every page adds up.
    ///   The Queen, live, in the Infested Citadel (Seeker, Seeker Brood, Seeker Soldier around her), inside a run that is on too.
    ///   A Greydwarf camp, a group fight (8 of 9 foes defeated).
    ///   Bonemass, an attempt that reset at 41 % (Ylva there, not sharing).
    ///   The Infested Mine, a run without a boss (4 deaths, Finch inside for 6:40 of 18:00).
    ///   An earlier visit to the Infested Citadel with an attempt on The Queen that reset (a boss fight nested in a run).
    /// </summary>
    public static class FightsSample
    {
        public const string Rowan = SampleWorld.Rowan, Edda = SampleWorld.Edda, Finch = SampleWorld.Finch, Tor = SampleWorld.Tor, Ylva = SampleWorld.Ylva;

        /// <summary>The game's English names of the creatures and drinks the fights use (the stand-in for the game's localization).</summary>
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["SeekerQueen"] = "The Queen", ["Seeker"] = "Seeker", ["SeekerBrood"] = "Seeker Brood", ["SeekerBrute"] = "Seeker Soldier", ["Tick"] = "Tick",
            ["Bonemass"] = "Bonemass", ["Blob"] = "Blob", ["BlobElite"] = "Oozer", ["Skeleton"] = "Skeleton",
            ["Greydwarf"] = "Greydwarf", ["Greydwarf_Elite"] = "Greydwarf Brute", ["Greydwarf_Shaman"] = "Greydwarf Shaman", ["Dragon"] = "Moder", ["Fader"] = "Lord Reto",
            ["$item_mead_poisonres"] = "Poison Resistance Mead", ["$item_barleywine"] = "Fire Resistance Barley Wine", ["$item_mead_frostres"] = "Frost Resistance Mead",
        };

        /// <summary>The creatures' trophies (the game's item prefabs); a creature without one (Seeker Brood, Oozer) shows the foe mark.</summary>
        public static readonly Dictionary<string, string> Trophies = new Dictionary<string, string>
        {
            ["SeekerQueen"] = "TrophySeekerQueen", ["Seeker"] = "TrophySeeker", ["SeekerBrute"] = "TrophySeekerBrute", ["Tick"] = "TrophyTick",
            ["Bonemass"] = "TrophyBonemass", ["Blob"] = "TrophyBlob", ["Skeleton"] = "TrophySkeleton",
            ["Greydwarf"] = "TrophyGreydwarf", ["Greydwarf_Elite"] = "TrophyGreydwarfBrute", ["Greydwarf_Shaman"] = "TrophyGreydwarfShaman", ["Dragon"] = "TrophyDragonQueen", ["Fader"] = "TrophyFader",
        };

        /// <summary>
        /// 0.9.4 draft (Chapters/FightWeakness.cs): SAMPLE damage modifiers for the fights' creatures, plausible, not the game's (in game they come
        /// from each creature's prefab, BattleGame.Foe). vw VeryWeak, w Weak, sw SlightlyWeak, sr SlightlyResistant, r Resistant, vr VeryResistant,
        /// i Immune; every other type Normal.
        /// </summary>
        public static readonly Dictionary<string, string> Mods = new Dictionary<string, string>
        {
            ["SeekerQueen"] = "fire:w pierce:r poison:i", ["Seeker"] = "fire:w poison:r", ["SeekerBrood"] = "fire:w poison:i", ["SeekerBrute"] = "frost:w pierce:r poison:r",
            ["Tick"] = "fire:w", ["Bonemass"] = "blunt:vw spirit:w slash:r pierce:r poison:i", ["Blob"] = "blunt:w frost:w lightning:w slash:r pierce:r fire:r poison:i",
            ["BlobElite"] = "blunt:w frost:w lightning:w slash:r pierce:r fire:r poison:i", ["Skeleton"] = "blunt:w spirit:w pierce:r poison:i",
            ["Greydwarf"] = "fire:w poison:r spirit:i", ["Greydwarf_Elite"] = "fire:w poison:r spirit:i", ["Greydwarf_Shaman"] = "fire:w poison:r spirit:i",
            ["Dragon"] = "pierce:w fire:w blunt:r frost:i",
            ["Fader"] = "frost:w spirit:w poison:r fire:i",   // SAMPLE, for the Lord Reto draft (WithReto), not the game's
        };

        /// <summary>A fight creature's sample data: its trophy and (0.9.4) its sample modifiers.</summary>
        public static PanelModel.FoeData FoeOf(string prefab)
        {
            if (prefab == null) return null;
            var hasTrophy = Trophies.TryGetValue(prefab, out var trophy);
            if (!hasTrophy && !Mods.ContainsKey(prefab)) return null;
            var d = new PanelModel.FoeData { Trophy = trophy };   // a creature without a trophy (Seeker Brood): the foe mark, its modifiers all the same
            if (!Mods.TryGetValue(prefab, out var mods)) return d;
            foreach (var t in PanelModel.BattleTypes) d.Modifiers[t] = "Normal";
            foreach (var m in mods.Split(' '))
            {
                var p = m.Split(':');
                d.Modifiers[p[0]] = p[1] == "vw" ? "VeryWeak" : p[1] == "w" ? "Weak" : p[1] == "sw" ? "SlightlyWeak" : p[1] == "sr" ? "SlightlyResistant" : p[1] == "r" ? "Resistant" : p[1] == "vr" ? "VeryResistant" : "Immune";
            }
            return d;
        }

        /// <summary>A book with the fights attached: the game's names and trophies for them, the rest of the book as it was.</summary>
        public static PanelInput With(PanelInput book, bool live)
        {
            var names = book.DisplayName; var foe = book.Foe;
            book.DisplayName = t => t != null && Names.TryGetValue(t, out var n) ? n : names?.Invoke(t);
            book.Foe = p => FoeOf(p) ?? foe?.Invoke(p);   // 0.9.4: with their sample modifiers
            book.Fights = Book(book.NowUtc, live, book.ToLocal);
            return book;
        }

        /// <summary>
        /// 0.9.4 draft (weaknesses, Chapters/FightWeakness.cs): a newer fight on a book With the fights (not live): Moder alone, won 4 minutes ago
        /// after 3 min 10 s by Rowan, Edda and Tor, one kind of foe: her line under the foe rows, all three forms (weak to pierce and fire,
        /// resists blunt, immune to frost).
        /// </summary>
        public static PanelInput WithSoloBoss(PanelInput book)
        {
            var now = book.NowUtc;
            var f = new FightView { Id = "moder-1", Kind = FightKind.Boss, Outcome = FightOutcome.Won, Boss = "Dragon", Biome = "Mountain", StartUtc = now.AddSeconds(-430), EndUtc = now.AddSeconds(-240), Health = 0f, SwingSeconds = 160 };
            var you = Player(f, Rowan, true); you.Blocks = 3; you.Stopped = 120; you.HitsReceived = 11;
            var edda = Player(f, Edda); edda.Blocks = 1; edda.Stopped = 40;
            var tor = Player(f, Tor); tor.Blocks = 6; tor.Parries = 2; tor.Stopped = 310;
            var m = Foe(f, "Dragon", defeated: 1, boss: true);
            Hit(m, Rowan, "pierce 1840 frost 260", "frost 220 pierce 60");
            Hit(m, Edda, "pierce 1210 poison 300", "frost 180");
            Hit(m, Tor, "blunt 980", "frost 260 slash 90");
            book.Fights.Fights.Insert(0, f);
            return book;
        }

        /// <summary>
        /// 0.9.4 draft (weaknesses): a newer group fight in the Swamp with two kinds of foe the game treats differently, Draugr (resists fire,
        /// immune to poison) and Blob (weak to blunt, frost and lightning; resists the rest; immune to poison): a weakness line each, named.
        /// </summary>
        public static PanelInput WithTwoKinds(PanelInput book)
        {
            var now = book.NowUtc;
            var f = new FightView { Id = "swamp-1", Kind = FightKind.Group, Outcome = FightOutcome.Ended, Biome = "Swamp", StartUtc = now.AddSeconds(-280), EndUtc = now.AddSeconds(-170) };
            var you = Player(f, Rowan, true); you.SwingSeconds = 38; you.SwingDamage = 690; you.Blocks = 4; you.Stopped = 90; you.HitsReceived = 10;
            var edda = Player(f, Edda); edda.NearSeconds = 8; edda.Blocks = 1; edda.Stopped = 20;
            var tor = Player(f, Tor); tor.NearSeconds = 14; tor.Blocks = 5; tor.Parries = 1; tor.Stopped = 150;
            var d = Foe(f, "Draugr", 4, 4);
            Hit(d, Rowan, "slash 420 fire 60", "slash 90 pierce 40"); Hit(d, Edda, "pierce 330 fire 90", "slash 60"); Hit(d, Tor, "blunt 280", "slash 110");
            var b = Foe(f, "Blob", 3, 3);
            Hit(b, Rowan, "slash 120", "poison 70"); Hit(b, Edda, "frost 140", "poison 40"); Hit(b, Tor, "blunt 210", "poison 90");
            book.Fights.Fights.Insert(0, f);
            return book;
        }

        /// <summary>
        /// 0.9.4 draft (weaknesses; Joost's screenshot, retold with the sample cast): a group fight in the Ashlands on now, 2 min 38 s in, against one
        /// Lord Reto (prefab Fader here, SAMPLE modifiers in Mods), already down: Rowan (you) 4 133 (blunt, lightning, a little spirit and pierce),
        /// Edda 3 568 (blunt, lightning), Tor 5 331 (blunt, poison), 13 032 together; only Edda was hit (337 pierce) and fell once; no blocks or
        /// parries; your DPS 107. The halves keep the group's types whole (lightning 3 162, pierce 306) with your 4 133.
        /// </summary>
        public static PanelInput WithReto(PanelInput book)
        {
            var now = book.NowUtc;
            var f = new FightView { Id = "reto-1", Kind = FightKind.Group, Outcome = FightOutcome.Live, Biome = "AshLands", StartUtc = now.AddSeconds(-158), EndUtc = now };
            var you = Player(f, Rowan, true); you.Live = true; you.SwingSeconds = 38.6f; you.SwingDamage = 4133; you.HitsReceived = 0; you.Stopped = 0;
            var edda = Player(f, Edda); edda.Live = true; edda.AsOfUtc = now.AddSeconds(-2); edda.NearSeconds = 6; edda.Stopped = 0;
            var tor = Player(f, Tor); tor.Live = true; tor.AsOfUtc = now.AddSeconds(-3); tor.NearSeconds = 4; tor.Stopped = 0;
            var r = Foe(f, "Fader", 1, 1);
            Hit(r, Rowan, "blunt 2122 lightning 1691.5 spirit 14 pierce 305.5");
            Hit(r, Edda, "blunt 2098 lightning 1470", "pierce 337");
            Hit(r, Tor, "blunt 4536 poison 795");
            Died(edda, now.AddSeconds(-40), "Fader", "pierce 120");
            book.Fights.Fights.Insert(0, f);
            return book;
        }

        /// <summary>0.9.4 draft (weaknesses): a book whose foe data carries no modifiers (the trophies only): the fight pages show no weakness at all.</summary>
        public static PanelInput WithoutModifiers(PanelInput book)
        {
            var foe = book.Foe;
            book.Foe = p => { var d = foe?.Invoke(p); return d == null ? null : new PanelModel.FoeData { Trophy = d.Trophy }; };
            return book;
        }

        /// <summary>
        /// 0.8.5: Brenna, one of Hildir's minibosses, through the real recorder and the fight adapter (not hand-made like the fights above): the game
        /// does not flag her a boss (every hit says boss: false) and the recorder knows her by name (BattleRecorder.IsBoss), so her fight is a boss
        /// fight. Rowan fights her for 2 min 40 s with three Skeletons and two Skeletons without a bow around her (one row: one name, FoeGroup) and
        /// wins. <paramref name="name"/>: her name as the game gives it (the tests and the preview pass it with the game's color tags, which no
        /// label may show: PanelRich.Untagged).
        /// </summary>
        public static PanelInput WithBrenna(PanelInput book, string name = "Brenna")
        {
            var now = book.NowUtc; var t0 = now.AddSeconds(-240);
            const string Brenna = "Skeleton_Hildir", BrennaToken = "$enemy_skeletonfire";
            var rec = new BattleRecorder();
            var at = new Spot(812.5f, 34.25f, -1460.75f);
            var boss = new FoeId(book.PlayerId, 9001);
            HitData.DamageTypes D(float blunt = 0, float slash = 0, float fire = 0) => new HitData.DamageTypes { m_blunt = blunt, m_slash = slash, m_fire = fire };
            for (int s = 0; s <= 160; s += 4)
            {
                rec.Dealt(t0.AddSeconds(s), boss, Brenna, BrennaToken, "BlackForest", D(blunt: 45), false, at);
                if (s % 10 == 0) rec.BossSeen(t0.AddSeconds(s + 1), boss, Brenna, BrennaToken, Math.Max(0f, 1f - s / 165f), at);
                if (s % 16 == 8) rec.Received(t0.AddSeconds(s + 2), boss, Brenna, BrennaToken, "BlackForest", D(fire: 28, slash: 12), false, at);
            }
            // her skeletons, near her: three with a bow, two without (the game names both "Skeleton")
            var adds = new[] { "Skeleton", "Skeleton", "Skeleton", "Skeleton_NoArcher", "Skeleton_NoArcher" };
            for (int i = 0; i < adds.Length; i++)
            {
                var id = new FoeId(book.PlayerId, (uint)(9100 + i)); var near = new Spot(at.X + 6 * i - 12, at.Y, at.Z + 4 * i - 8); var from = 20 + 25 * i;
                rec.Dealt(t0.AddSeconds(from), id, adds[i], "$enemy_skeleton", "BlackForest", D(slash: 38), false, near);
                rec.Received(t0.AddSeconds(from + 3), id, adds[i], "$enemy_skeleton", "BlackForest", D(slash: 11), false, near);
                rec.Dealt(t0.AddSeconds(from + 6), id, adds[i], "$enemy_skeleton", "BlackForest", D(blunt: 52), false, near);
                if (i == 4) continue;   // the last one got away
                rec.Killed(t0.AddSeconds(from + 7), "$enemy_skeleton", 1); rec.Destroyed(t0.AddSeconds(from + 7.5), id);
            }
            rec.Killed(t0.AddSeconds(164), BrennaToken, 1); rec.Destroyed(t0.AddSeconds(164.5), boss);
            rec.Tick(now);
            book.Fights = FightAdapter.Build(new List<FightSide> { FightSide.Of(rec, book.PlayerName) }, null, null, now);
            var names = book.DisplayName; var foe = book.Foe;
            book.DisplayName = t => t == Brenna || t == BrennaToken ? name : t == "Skeleton" || t == "Skeleton_NoArcher" ? "Skeleton" : names?.Invoke(t);
            book.Foe = p => p == Brenna ? new PanelModel.FoeData { Trophy = "TrophySkeletonHildir" } : p == "Skeleton" ? new PanelModel.FoeData { Trophy = "TrophySkeleton" } : foe?.Invoke(p);
            return book;
        }

        /// <summary>
        /// 0.8.6 (Joost's PC, 11 Oct: two saved runs listed as "A dungeon"): the runs a restart keeps (RunLog, "dungeonRuns" in the saved file), shaped
        /// as the PC's own record was: each with the place's game name ("Mistlands_DvergrTownEntrance2", "Crypt2") and no name token (the game has
        /// no label for these places), through the fight adapter with no game names at all. Rowan and Edda in an Infested Mine for 26 min (1 death),
        /// then Rowan in a Burial Chamber for 8 min, and a 34 s look into another Burial Chamber with nothing fought (kept, not listed).
        /// </summary>
        public static PanelInput WithSavedRuns(PanelInput book)
        {
            var now = book.NowUtc; string At(double minAgo) => now.AddMinutes(-minAgo).ToString("o", CultureInfo.InvariantCulture);
            string P(string name, bool me, string more) => "{\"n\":\"" + name + "\"" + (me ? ",\"me\":1" : "") + more + "}";
            const string RowanD = "\"d\":[0,4144,4599,1983,290,0,894,611],\"r\":[163,0,349]", EddaD = "\"d\":[0,4414,4018,0,3060,0,0,588],\"r\":[254,0,739]", CryptD = "\"d\":[0,880],\"r\":[120]";
            var json = "[" +
                "{\"k\":\"Mistlands_DvergrTownEntrance2\",\"s\":\"" + At(150) + "\",\"t\":1577,\"dd\":1,\"n\":89,\"p\":[" + P(Edda, false, ",\"in\":1224,\"dd\":1,\"fo\":69") + "," + P(Rowan, true, ",\"in\":1213,\"fo\":65") + "]," +
                    "\"f\":[{\"k\":\"Seeker\",\"n\":31,\"df\":30,\"p\":[" + P(Rowan, true, "," + RowanD) + "," + P(Edda, false, "," + EddaD) + "]}],\"sp\":[[16,53],[327,1347],[1436,1534]]}," +
                "{\"k\":\"Crypt3\",\"s\":\"" + At(115) + "\",\"t\":480,\"dd\":0,\"n\":12,\"p\":[" + P(Rowan, true, ",\"in\":480,\"fo\":12") + "]," +
                    "\"f\":[{\"k\":\"Skeleton\",\"n\":12,\"df\":12,\"p\":[" + P(Rowan, true, "," + CryptD) + "]}],\"sp\":[[20,400]]}," +
                "{\"k\":\"Crypt2\",\"s\":\"" + At(70) + "\",\"t\":34,\"dd\":0,\"n\":0,\"p\":[" + P(Rowan, true, ",\"in\":33") + "]}]";
            book.Fights = FightAdapter.Build(null, null, RunLog.ReadFrom(MiniJson.Parse(json)), now);
            var names = book.DisplayName; book.DisplayName = t => t != null && Names.TryGetValue(t, out var n) ? n : names?.Invoke(t);
            var foe = book.Foe; book.Foe = p => p != null && Trophies.TryGetValue(p, out var t) ? new PanelModel.FoeData { Trophy = t } : foe?.Invoke(p);
            return book;
        }

        /// <summary>
        /// 0.8.6: a fight the book's owner was not in, from a fellow's shared feed, newer than the owner's own newest (Last fight keeps the owner's;
        /// its quiet "Newer on the server" line points at this one, id "server-fight"): Edda alone with a Seeker, 3 minutes ago. boss: Finch and Tor
        /// on Eikthyr, live (id "server-boss").
        /// </summary>
        public static PanelInput WithServerFight(PanelInput book, bool boss = false)
        {
            var now = book.NowUtc;
            var f = boss
                ? new FightView { Id = "server-boss", Kind = FightKind.Boss, Outcome = FightOutcome.Live, Boss = "Eikthyr", Biome = "Meadows", StartUtc = now.AddMinutes(-4), EndUtc = now, Health = 0.7f, HealthMinuteAgo = 0.8f }
                : new FightView { Id = "server-fight", Kind = FightKind.Group, Outcome = FightOutcome.Ended, Biome = "Meadows", StartUtc = now.AddMinutes(-3), EndUtc = now.AddMinutes(-2) };
            var players = boss ? new[] { Finch, Tor } : new[] { Edda };
            foreach (var n in players) { var p = Player(f, n); p.Live = boss; p.AsOfUtc = now.AddSeconds(-5); }
            var x = Foe(f, boss ? "Eikthyr" : "Seeker", boss: boss, defeated: boss ? 0 : 1);
            foreach (var n in players) Hit(x, n, "pierce 300", "blunt 40");
            book.Fights.Fights.Insert(0, f);
            return book;
        }

        /// <summary>
        /// 0.8.6: a fellow's book on the PC of <paramref name="own"/>'s owner, as PanelUi opens it: their copy, with the others' copies and your own
        /// book (and its fights) among their Fellows. Their Battle pages read your fights seen by them (PanelModel.FriendFights).
        /// </summary>
        public static PanelInput Friend(PanelInput own, string name)
        {
            var f = own.Fellows.First(x => string.Equals(x.PlayerName, name, StringComparison.OrdinalIgnoreCase)).ShallowCopy();
            f.NowUtc = own.NowUtc; f.ViewerName = own.PlayerName; f.PlayerNames = own.PlayerNames; f.ViewerSessionStartUtc = own.SessionStartUtc;
            f.DisplayName = own.DisplayName; f.Foe = own.Foe;   // the fights' foes by the game's names and trophies, as on your book
            f.Fellows = own.Fellows.Where(x => !string.Equals(x.PlayerName, name, StringComparison.OrdinalIgnoreCase)).Concat(new[] { own }).ToList();
            return f;
        }

        // ---------- the builders ----------

        static Dictionary<string, double> Types(string spec)
        {
            var d = new Dictionary<string, double>(StringComparer.Ordinal);
            var w = (spec ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 1 < w.Length; i += 2) d[w[i]] = double.Parse(w[i + 1], CultureInfo.InvariantCulture);
            return d;
        }

        static FightFoe Foe(FightView f, string prefab, int count = 1, int defeated = 0, bool boss = false)
        {
            var x = f.Foes.FirstOrDefault(o => o.Prefab == prefab);
            if (x == null) { x = new FightFoe { Prefab = prefab, Boss = boss, Count = 0 }; f.Foes.Add(x); }
            x.Count += count; x.Defeated += defeated;
            return x;
        }

        /// <summary>One player's part of one foe kind: what they dealt it and what it dealt them ("slash 700 pierce 350").</summary>
        static void Hit(FightFoe x, string player, string dealt, string received = null)
        {
            if (!x.By.TryGetValue(player, out var p)) x.By[player] = p = new FightPart();
            foreach (var kv in Types(dealt)) { p.Dealt.TryGetValue(kv.Key, out var o); p.Dealt[kv.Key] = o + kv.Value; }
            foreach (var kv in Types(received)) { p.Received.TryGetValue(kv.Key, out var o); p.Received[kv.Key] = o + kv.Value; }
        }

        static FightPlayer Player(FightView f, string name, bool you = false)
        {
            var p = f.PlayerOf(name);
            if (p == null) { p = new FightPlayer { Name = name, You = you }; f.Players.Add(p); }
            return p;
        }

        static void Died(FightPlayer p, DateTime at, string by, string last)
        {
            var d = new FightDeath { AtUtc = at, By = by };
            foreach (var kv in Types(last)) d.Last[kv.Key] = kv.Value;
            p.Deaths.Add(d);
        }

        /// <summary>A run takes in a fight inside it: its foes and parts, and its players' blocks, parries and deaths.</summary>
        static void Into(FightView run, FightView fight)
        {
            foreach (var x in fight.Foes)
            {
                var y = Foe(run, x.Prefab, x.Count, x.Defeated, x.Boss);
                foreach (var kv in x.By)
                    Hit(y, kv.Key, string.Join(" ", kv.Value.Dealt.Select(t => t.Key + " " + t.Value.ToString(CultureInfo.InvariantCulture)).ToArray()),
                               string.Join(" ", kv.Value.Received.Select(t => t.Key + " " + t.Value.ToString(CultureInfo.InvariantCulture)).ToArray()));
            }
            foreach (var p in fight.Players)
            {
                var q = Player(run, p.Name, p.You); q.Has = p.Has; q.Live = p.Live; q.AsOfUtc = p.AsOfUtc;
                q.Blocks += p.Blocks; q.Parries += p.Parries; q.Deaths.AddRange(p.Deaths);
            }
            run.Spans.Add(new FightSpan { StartUtc = fight.StartUtc, EndUtc = fight.EndUtc, FightId = fight.Id });
        }

        // ---------- the fights ----------

        /// <summary>
        /// The fight book at <paramref name="now"/>. live: The Queen is being fought now, in a run of the Infested Citadel that is on too (Last fight
        /// reads "Boss fight"); otherwise the newest fight is the Greydwarf camp, 5 minutes ago.
        /// </summary>
        public static FightBook Book(DateTime now, bool live, Func<DateTime, DateTime> toLocal = null)
        {
            var local = toLocal ?? (t => t.AddHours(2));
            // a local evening time a number of days back, as UTC (the sample's PCs run at UTC+2)
            DateTime Evening(int daysBack, int hour, int minute) { var day = local(now).Date.AddDays(-daysBack).AddHours(hour).AddMinutes(minute); return now + (day - local(now)); }
            var book = new FightBook();
            if (live)
            {
                var queen = QueenNow(now);
                var citadel = new FightView { Id = "citadel-now", Kind = FightKind.Run, Outcome = FightOutcome.Live, Place = "Infested Citadel", Biome = "Mistlands", StartUtc = now.AddSeconds(-1085), EndUtc = now };
                Gate(citadel, citadel.StartUtc.AddSeconds(70), 110, now);
                Into(citadel, queen);
                foreach (var (name, inside) in new[] { (Rowan, 1085f), (Edda, 1085f), (Finch, 900f), (Tor, 1060f) }) citadel.PlayerOf(name).InsideSeconds = inside;
                book.Fights.Add(queen); book.Fights.Add(citadel);
            }
            book.Fights.Add(Camp(live ? now.AddMinutes(-50) : now.AddMinutes(-7), now));
            book.Fights.Add(Mine(Evening(1, 21, 2)));
            var queen2 = QueenReset(Evening(2, 21, 10));
            var citadel2 = new FightView { Id = "citadel-2", Kind = FightKind.Run, Outcome = FightOutcome.Ended, Place = "Infested Citadel", Biome = "Mistlands", StartUtc = Evening(2, 20, 58), EndUtc = Evening(2, 21, 24) };
            Gate(citadel2, citadel2.StartUtc.AddSeconds(50), 140, now, finch: false);
            Into(citadel2, queen2);
            foreach (var (name, inside) in new[] { (Rowan, 1560f), (Edda, 1560f), (Tor, 1500f) }) citadel2.PlayerOf(name).InsideSeconds = inside;
            book.Fights.Add(queen2); book.Fights.Add(citadel2);
            book.Fights.Add(BonemassReset(Evening(3, 21, 40)));
            book.Fights.Sort((a, b) => b.StartUtc.CompareTo(a.StartUtc));   // newest first
            return book;
        }

        /// <summary>The Queen, live: 5:12 in, 64 % left (72 % a minute ago), the four of the group; Finch fell once to the brood's poison.</summary>
        static FightView QueenNow(DateTime now)
        {
            var f = new FightView { Id = "queen-now", Kind = FightKind.Boss, Outcome = FightOutcome.Live, ParentId = "citadel-now", Boss = "SeekerQueen", Biome = "Mistlands",
                                    StartUtc = now.AddSeconds(-312), EndUtc = now, Health = 0.64f, HealthMinuteAgo = 0.72f, SwingSeconds = 241 };   // 0.8.6: under attack 241 of its 312 s
            var you = Player(f, Rowan, true); you.Live = true; you.Blocks = 5; you.Parries = 1; you.Stopped = 260; you.HitsReceived = 20; you.InsideSeconds = 312;
            var edda = Player(f, Edda); edda.Live = true; edda.AsOfUtc = now.AddSeconds(-2); edda.Blocks = 2; edda.Stopped = 90; edda.InsideSeconds = 312;
            var finch = Player(f, Finch); finch.Live = true; finch.AsOfUtc = now.AddSeconds(-3); finch.Stopped = 0; finch.InsideSeconds = 250;
            var tor = Player(f, Tor); tor.AsOfUtc = now.AddSeconds(-14); tor.Blocks = 10; tor.Parries = 3; tor.Stopped = 540; tor.InsideSeconds = 290;
            var q = Foe(f, "SeekerQueen", boss: true);
            Hit(q, Rowan, "slash 1050", "pierce 40 poison 160");
            Hit(q, Edda, "pierce 700 fire 260", "slash 40 pierce 60");
            Hit(q, Finch, "pierce 500 frost 280", "slash 40 pierce 100 poison 200");
            Hit(q, Tor, "blunt 430", "slash 60 pierce 90 poison 110");
            var s = Foe(f, "Seeker", 11, 9);
            Hit(s, Rowan, "slash 360", "slash 60"); Hit(s, Edda, "pierce 300", "pierce 60"); Hit(s, Finch, "pierce 240", "slash 80 pierce 40"); Hit(s, Tor, "blunt 180", "slash 80 pierce 20");
            var b = Foe(f, "SeekerBrood", 17, 15);
            Hit(b, Rowan, "slash 150", "poison 94"); Hit(b, Edda, "fire 120", "poison 60"); Hit(b, Finch, "frost 160", "pierce 30 poison 120"); Hit(b, Tor, "blunt 60", "pierce 40 poison 50");
            var w = Foe(f, "SeekerBrute", 4, 3);
            Hit(w, Rowan, "slash 120", "slash 56"); Hit(w, Edda, "pierce 90", "slash 40"); Hit(w, Finch, "pierce 70", "slash 30"); Hit(w, Tor, "blunt 110", "slash 70");
            Died(finch, now.AddSeconds(-130), "SeekerBrood", "poison 90 pierce 15");
            return f;
        }

        /// <summary>The Seekers at a citadel's gate before the boss (a fight of the run, not of the boss fight).</summary>
        static void Gate(FightView run, DateTime start, int seconds, DateTime now, bool finch = true)
        {
            var s = Foe(run, "Seeker", 5, 5);
            Hit(s, Rowan, "slash 220", "slash 30"); Hit(s, Edda, "pierce 180"); Hit(s, Tor, "blunt 120", "slash 50 pierce 10");
            if (finch) Hit(s, Finch, "pierce 150", "slash 20");
            var t = Foe(run, "Tick", 2, 2);
            Hit(t, Rowan, "slash 30"); Hit(t, Tor, "blunt 20", "pierce 15");
            foreach (var n in finch ? new[] { Rowan, Edda, Finch, Tor } : new[] { Rowan, Edda, Tor }) Player(run, n, n == Rowan);
            run.Spans.Add(new FightSpan { StartUtc = start, EndUtc = start.AddSeconds(seconds) });
        }

        /// <summary>The Greydwarf camp: two minutes, 8 of 9 foes defeated (a Shaman got away), the four near each other; Rowan, hit mostly by blunt,
        /// blocked once against nine hits landing (the "bring a shield" line). Only your own hits are known, as in the game (a copy does not share them).</summary>
        static FightView Camp(DateTime start, DateTime now)
        {
            var f = new FightView { Id = "camp", Kind = FightKind.Group, Outcome = FightOutcome.Ended, Biome = "BlackForest", StartUtc = start, EndUtc = start.AddMinutes(2) };
            var you = Player(f, Rowan, true); you.SwingSeconds = 41; you.SwingDamage = 455; you.Blocks = 1; you.Stopped = 20; you.HitsReceived = 9;   // 0.8.6: your fighting time, 41 s; one block against nine hits landing, blunt the most: the shield line
            var edda = Player(f, Edda); edda.NearSeconds = 12; edda.Blocks = 1; edda.Stopped = 30;
            edda.SwingSeconds = 32; edda.SwingDamage = 400;   // 0.8.6: her fighting time as her copy shares it (her Last fight on her book: 12 DPS)
            edda.Near = new Dictionary<string, float> { [Rowan] = 12, [Finch] = 16, [Tor] = 30 };   // who was near her, as her record counted it (her book's "near Edda 30 s")
            var finch = Player(f, Finch); finch.NearSeconds = 20; finch.Stopped = 0;
            var tor = Player(f, Tor); tor.NearSeconds = 12; tor.Blocks = 4; tor.Parries = 2; tor.Stopped = 160;
            var g = Foe(f, "Greydwarf", 6, 6);
            Hit(g, Rowan, "slash 260", "blunt 60 pierce 30"); Hit(g, Edda, "pierce 210", "blunt 40 pierce 20"); Hit(g, Finch, "pierce 180", "blunt 30"); Hit(g, Tor, "blunt 150", "blunt 44 pierce 24");
            var b = Foe(f, "Greydwarf_Elite", 1, 1);
            Hit(b, Rowan, "slash 90", "blunt 70"); Hit(b, Edda, "pierce 80 fire 30", "slash 20"); Hit(b, Finch, "pierce 70", "blunt 46 slash 30"); Hit(b, Tor, "blunt 130", "blunt 80");
            var s = Foe(f, "Greydwarf_Shaman", 2, 1);
            Hit(s, Rowan, "slash 120", "poison 30"); Hit(s, Edda, "pierce 80", "poison 50"); Hit(s, Finch, "pierce 150", "poison 44"); Hit(s, Tor, "blunt 90", "poison 122");
            return f;
        }

        /// <summary>Bonemass, an attempt that reset at 41 % after 6 min 20 s: Rowan, Edda and Tor, with Ylva there, who does not share; Edda's fall is
        /// past what her copy still holds (counted, its types not known).</summary>
        static FightView BonemassReset(DateTime start)
        {
            var f = new FightView { Id = "bonemass-1", Kind = FightKind.Boss, Outcome = FightOutcome.Reset, Boss = "Bonemass", Biome = "Swamp", StartUtc = start, EndUtc = start.AddSeconds(380), Health = 0.41f, SwingSeconds = 290 };
            var you = Player(f, Rowan, true); you.Blocks = 7; you.Parries = 1; you.Stopped = 350; you.HitsReceived = 18;
            var edda = Player(f, Edda); edda.Blocks = 2; edda.Stopped = 100;
            var tor = Player(f, Tor); tor.Blocks = 11; tor.Parries = 3; tor.Stopped = 550;
            Player(f, Ylva).Has = false;
            var m = Foe(f, "Bonemass", boss: true);
            Hit(m, Rowan, "slash 620", "blunt 120 poison 180"); Hit(m, Edda, "pierce 260 fire 120", "blunt 80 poison 120"); Hit(m, Tor, "blunt 1180", "blunt 260 poison 210");
            var b = Foe(f, "Blob", 9, 9);
            Hit(b, Rowan, "slash 120", "poison 60"); Hit(b, Edda, "pierce 90", "poison 40"); Hit(b, Tor, "blunt 60", "poison 30");
            var o = Foe(f, "BlobElite", 3, 2);
            Hit(o, Rowan, "slash 140", "poison 90"); Hit(o, Edda, "fire 60", "poison 30"); Hit(o, Tor, "blunt 100", "poison 50");
            var k = Foe(f, "Skeleton", 5, 5);
            Hit(k, Rowan, "slash 110", "slash 40"); Hit(k, Edda, "pierce 70", "slash 50"); Hit(k, Tor, "blunt 190", "slash 60");
            Died(you, start.AddSeconds(250), "Bonemass", "poison 70 blunt 40");
            edda.Deaths.Add(new FightDeath { By = "BlobElite" });   // three evenings back: older than the 10 falls her copy shares, so counted without time or types
            Died(tor, start.AddSeconds(200), "Bonemass", "blunt 90 poison 30");
            Died(tor, start.AddSeconds(360), "Bonemass", "poison 80 blunt 30");
            return f;
        }

        /// <summary>The Queen, two evenings ago: an attempt that reset at 41 % after 7 min 40 s, inside a run of the Infested Citadel (Finch was not there).</summary>
        static FightView QueenReset(DateTime start)
        {
            var f = new FightView { Id = "queen-2", Kind = FightKind.Boss, Outcome = FightOutcome.Reset, ParentId = "citadel-2", Boss = "SeekerQueen", Biome = "Mistlands",
                                    StartUtc = start, EndUtc = start.AddSeconds(460), Health = 0.41f, SwingSeconds = 352 };
            var you = Player(f, Rowan, true); you.Blocks = 7; you.Parries = 1; you.Stopped = 350; you.HitsReceived = 16;
            var edda = Player(f, Edda); edda.Blocks = 2; edda.Stopped = 100;
            var tor = Player(f, Tor); tor.Blocks = 11; tor.Parries = 3; tor.Stopped = 550;
            var q = Foe(f, "SeekerQueen", boss: true);
            Hit(q, Rowan, "slash 880", "pierce 70 poison 130"); Hit(q, Edda, "pierce 520 fire 180", "slash 40 pierce 50 poison 60"); Hit(q, Tor, "blunt 510", "slash 100 pierce 90 poison 160");
            var s = Foe(f, "Seeker", 13, 12);
            Hit(s, Rowan, "slash 300", "slash 60 pierce 40"); Hit(s, Edda, "pierce 240", "slash 50"); Hit(s, Tor, "blunt 150", "slash 100 pierce 60");
            var b = Foe(f, "SeekerBrood", 20, 18);
            Hit(b, Rowan, "slash 180", "pierce 20 poison 60"); Hit(b, Edda, "fire 140", "poison 70"); Hit(b, Tor, "blunt 70", "poison 100");
            Died(you, start.AddSeconds(330), "SeekerQueen", "poison 80 pierce 30");
            Died(edda, start.AddSeconds(390), "SeekerBrood", "poison 60");
            Died(tor, start.AddSeconds(180), "SeekerQueen", "pierce 70 poison 40");
            Died(tor, start.AddSeconds(420), "SeekerQueen", "poison 90");
            return f;
        }

        /// <summary>The Infested Mine: 18 minutes in and out, no boss, six fights, four falls; Finch came in late and left early (6:40 inside).</summary>
        static FightView Mine(DateTime start)
        {
            var f = new FightView { Id = "mine-1", Kind = FightKind.Run, Outcome = FightOutcome.Ended, Place = "Infested Mine", Biome = "Mistlands", StartUtc = start, EndUtc = start.AddMinutes(18) };
            var you = Player(f, Rowan, true); you.InsideSeconds = 1080;
            var edda = Player(f, Edda); edda.InsideSeconds = 1080;
            var tor = Player(f, Tor); tor.InsideSeconds = 1040;
            var finch = Player(f, Finch); finch.InsideSeconds = 400;
            var s = Foe(f, "Seeker", 22, 22);
            Hit(s, Rowan, "slash 520", "slash 150 pierce 60"); Hit(s, Edda, "pierce 400", "slash 80 pierce 40"); Hit(s, Finch, "pierce 360", "slash 40"); Hit(s, Tor, "blunt 260", "slash 190 pierce 70");
            var b = Foe(f, "SeekerBrood", 31, 31);
            Hit(b, Rowan, "slash 240", "poison 140"); Hit(b, Edda, "fire 190", "poison 110"); Hit(b, Finch, "frost 230", "poison 50"); Hit(b, Tor, "blunt 120", "poison 260");
            var t = Foe(f, "Tick", 6, 6);
            Hit(t, Rowan, "slash 60", "pierce 30"); Hit(t, Edda, "pierce 40", "pierce 20"); Hit(t, Finch, "pierce 20"); Hit(t, Tor, "blunt 60", "pierce 40");
            foreach (var (a, b2) in new[] { (60, 150), (300, 380), (500, 620), (720, 800), (900, 1010), (1150 - 120, 1060) }) f.Spans.Add(new FightSpan { StartUtc = start.AddSeconds(a), EndUtc = start.AddSeconds(b2) });
            Died(you, start.AddSeconds(560), "Seeker", "slash 90 pierce 20");
            Died(edda, start.AddSeconds(760), "SeekerBrood", "poison 70");
            Died(tor, start.AddSeconds(940), "Seeker", "slash 80");
            Died(tor, start.AddSeconds(1000), "SeekerBrood", "poison 90");
            return f;
        }
    }
}
