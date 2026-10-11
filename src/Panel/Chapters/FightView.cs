using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven.Panel
{
    /// <summary>What a fight is: a group fight (no boss), a boss fight (live, or a finished attempt), or a dungeon run (in to out).</summary>
    public enum FightKind { Group, Boss, Run }

    /// <summary>
    /// How a fight or run stands, in the words the book can know (work/hearthwoven-0.8.5/boss-fight/NOTES.md round 2): Live (still on), Won (the
    /// boss died), Reset (the boss reset; FightView.Health is its health then: "Reset at 41 %"), GotAway (ended with the outcome not known: nobody
    /// of the group in range for a long while; Health is the last seen), Ended (a group fight or a run that is over). Never "lost".
    /// </summary>
    public enum FightOutcome { Live, Won, Reset, GotAway, Ended }

    /// <summary>
    /// 0.9 fight pages (work/hearthwoven-0.8.5/boss-fight, Joost's picks A, C and D): one fight, boss fight or run, as the pages draw it
    /// (Chapters/FightPages.cs: Battle > Last fight, Runs and bosses, a fight's or a run's own page). Pure data, no Unity: filled by the fight
    /// adapter from the players' shared fight blocks (v09-fights), by PanelSample for the previews; the pages read nothing else.
    /// Amounts are damage points, dealt before the foe's armor and received after the player's armor, keyed by the book's damage type ids
    /// (BattleRecorder.Types: "blunt", "slash", "pierce", "fire", "frost", "lightning", "poison", "spirit"; received may also hold "damage", raw).
    /// A player's totals are the sum of their parts in the foe rows (plus, received, what no creature dealt): the foe rows and the player rows of
    /// a page always add up.
    /// </summary>
    public sealed class FightView
    {
        /// <summary>The fight's key, the same on every PC: a boss fight the boss's FoeId plus its start, a run its id; the pages link by it.</summary>
        public string Id;
        public FightKind Kind;
        public FightOutcome Outcome = FightOutcome.Ended;
        /// <summary>A boss fight inside a run: the run's Id (Runs and bosses nests it under the run); null = a top-level row.</summary>
        public string ParentId;
        /// <summary>The boss's prefab ("SeekerQueen"); null for a group fight or a run. Its row is Foes' first (FightFoe.Boss).</summary>
        public string Boss;
        /// <summary>A run's dungeon by name ("Infested Mine"); null = not known (the page says the land and "dungeon").</summary>
        public string Place;
        /// <summary>The land it was in, as Heightmap.Biome names it ("Mistlands"); null = not known.</summary>
        public string Biome;
        /// <summary>Start: a boss fight's first hit on the boss, a group fight's first hit, a run's first group member in. End: the boss's death or
        /// reset, the fight's last hit, the last of the group out; while Live, the moment of the newest numbers.</summary>
        public DateTime StartUtc, EndUtc;
        /// <summary>A boss's health, 0..1: now (Live), when it reset (Reset), last seen (GotAway), 0 when Won; -1 = not known (the lane is left out).</summary>
        public float Health = -1;
        /// <summary>Live only: the boss's health a minute ago, 0..1 (the lane's hatched "down 8 % in the last minute"); -1 = not known.</summary>
        public float HealthMinuteAgo = -1;
        /// <summary>The foes fought, one row per kind: the boss first (Boss), then the foes fought around it; a run's all kinds it met.</summary>
        public readonly List<FightFoe> Foes = new List<FightFoe>();
        /// <summary>The players in it, in the order the page lists them: the book's owner first, then the group in join order. A fellow player who
        /// was there but whose copy holds no numbers of it has Has false.</summary>
        public readonly List<FightPlayer> Players = new List<FightPlayer>();
        /// <summary>A run's time line: the fights inside it in time order (a boss fight among them carries its FightId); the falls are the
        /// players' Deaths. Empty for a fight.</summary>
        public readonly List<FightSpan> Spans = new List<FightSpan>();

        public bool IsLive => Outcome == FightOutcome.Live;
        public TimeSpan Length => EndUtc > StartUtc ? EndUtc - StartUtc : TimeSpan.Zero;

        /// <summary>The boss's row, or null.</summary>
        public FightFoe BossFoe => Foes.FirstOrDefault(f => f.Boss);

        /// <summary>One player's damage dealt in it by type: their parts of every foe row.</summary>
        public Dictionary<string, double> DealtBy(string player) => Sum(Foes.Select(f => f.PartOf(player)?.Dealt));

        /// <summary>One player's damage received in it by type: their parts of every foe row, plus what no creature dealt (FightPlayer.ReceivedElse).</summary>
        public Dictionary<string, double> ReceivedBy(string player) =>
            Sum(Foes.Select(f => f.PartOf(player)?.Received).Concat(new[] { PlayerOf(player)?.ReceivedElse }));

        /// <summary>A player by name (case does not matter), or null.</summary>
        public FightPlayer PlayerOf(string name) => Players.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>The players whose numbers are in it (Has).</summary>
        public IEnumerable<FightPlayer> Counted => Players.Where(p => p.Has);

        public double DealtTotal => Counted.Sum(p => DealtBy(p.Name).Values.Sum());
        public double ReceivedTotal => Counted.Sum(p => ReceivedBy(p.Name).Values.Sum());

        internal static Dictionary<string, double> Sum(IEnumerable<Dictionary<string, double>> parts)
        {
            var d = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var p in parts) if (p != null) foreach (var kv in p) if (kv.Value > 0) { d.TryGetValue(kv.Key, out var o); d[kv.Key] = o + kv.Value; }
            return d;
        }
    }

    /// <summary>One foe kind in a fight (or the boss itself), with each player's part of it.</summary>
    public sealed class FightFoe
    {
        /// <summary>The creature's prefab ("Seeker"); its name and trophy come from the game's data (PanelInput.DisplayName, Foe).</summary>
        public string Prefab;
        /// <summary>The fight's boss: its row comes first and draws the boss ring.</summary>
        public bool Boss;
        /// <summary>Separate creatures of the kind fought (the "×N"; a boss is 1); how many of them were defeated (Last fight's "8 foes defeated of 9").</summary>
        public int Count = 1, Defeated;
        /// <summary>Each player's damage to and from this kind in this fight, by player name (case does not matter). A player missing here dealt
        /// and received nothing from it.</summary>
        public readonly Dictionary<string, FightPart> By = new Dictionary<string, FightPart>(StringComparer.OrdinalIgnoreCase);

        public FightPart PartOf(string player) => player != null && By.TryGetValue(player, out var p) ? p : null;
        public double DealtTotal => By.Values.Sum(p => p.Dealt.Values.Sum());
        public double ReceivedTotal => By.Values.Sum(p => p.Received.Values.Sum());
        /// <summary>The group's damage to it by type, and from it by type.</summary>
        public Dictionary<string, double> Dealt => FightView.Sum(By.Values.Select(p => p.Dealt));
        public Dictionary<string, double> Received => FightView.Sum(By.Values.Select(p => p.Received));
    }

    /// <summary>One player's part of one foe row: what they dealt to it and what it dealt them, by damage type.</summary>
    public sealed class FightPart
    {
        public readonly Dictionary<string, double> Dealt = new Dictionary<string, double>(StringComparer.Ordinal);
        public readonly Dictionary<string, double> Received = new Dictionary<string, double>(StringComparer.Ordinal);
    }

    /// <summary>One player in a fight: who, how fresh their numbers are, and what is theirs alone (blocks, parries, deaths, time).</summary>
    public sealed class FightPlayer
    {
        public string Name;
        /// <summary>The book's owner (on your own book "You").</summary>
        public bool You;
        /// <summary>Their numbers are in it; false: they were there, but their copy holds none of it (the row says so in words, the sums leave them out).</summary>
        public bool Has = true;
        /// <summary>0.8.5: a player without numbers (Has false) who shares: the fight adapter lists no one else without numbers (FightPages.NoPartLine
        /// says why their copy holds none of it); false with Has false: one who does not share (the fight sample's Ylva).</summary>
        public bool Shares;
        /// <summary>Their numbers are coming in now (the row's "live"); else AsOfUtc says when they are from ("14 s ago"); null = not known.</summary>
        public bool Live;
        public DateTime? AsOfUtc;
        /// <summary>A group fight: how long they were near the book's owner, seconds ("near you 12 s"); -1 = not known.</summary>
        public float NearSeconds = -1;
        /// <summary>Their own time in it, seconds: a run, their own in to out ("Finch 6:40 of 18:00"); a boss fight, in range of the boss; -1 = not known.</summary>
        public float InsideSeconds = -1;
        /// <summary>Hits they blocked and parried in it (a parry is a block too, counted once, as a parry).</summary>
        public int Blocks, Parries;
        /// <summary>Hits that landed on them in it (blocked ones are Blocks and Parries, not here); -1 = not known: a fellow's copy does not share
        /// it, nor does a fight kept from an earlier session. The "bring a shield" line weighs blocks against it (FightPages.cs BringLine).</summary>
        public int HitsReceived = -1;
        /// <summary>The damage their shield stopped in it (blocked and parried hits, what did not get through); -1 = not known (the players'
        /// table leaves its Stopped column out when nobody's is known).</summary>
        public double Stopped = -1;
        /// <summary>Damage they received from no creature in it (a fall, drowning), by type: in their Received, in no foe row.</summary>
        public readonly Dictionary<string, double> ReceivedElse = new Dictionary<string, double>(StringComparer.Ordinal);
        /// <summary>Their deaths in it, in time order.</summary>
        public readonly List<FightDeath> Deaths = new List<FightDeath>();
    }

    /// <summary>A player's death in a fight: when, what dealt the last hit, and the damage of its last seconds by type. A fellow's fall their
    /// copy no longer carries (it shares its last 10) is still a death: AtUtc default (not known) and Last empty, so it counts but shows no types.</summary>
    public sealed class FightDeath
    {
        public DateTime AtUtc;
        /// <summary>The creature prefab, or the game's hit type for a cause that is no creature ("Fall"); null = not known.</summary>
        public string By;
        /// <summary>The damage received in the last seconds before the fall, by type (as the Deaths page reads EventLog.Death.Last10s: the last 10 s).</summary>
        public readonly Dictionary<string, double> Last = new Dictionary<string, double>(StringComparer.Ordinal);
    }

    /// <summary>A stretch of fighting on a run's time line; a boss fight's span carries the fight's Id (drawn as the gold span with the crown).</summary>
    public sealed class FightSpan
    {
        public DateTime StartUtc, EndUtc;
        public string FightId;
    }

    /// <summary>
    /// The fights the book knows (PanelInput.Fights), newest first: group fights, boss fights (live or finished) and runs, each once. Last fight
    /// shows the newest that is not a run; Runs and bosses lists the runs and boss fights, a boss fight in a run under it. Null on the input = no
    /// fight adapter yet: Last fight draws the 0.8 page from this PC's own record.
    /// </summary>
    public sealed class FightBook
    {
        public readonly List<FightView> Fights = new List<FightView>();

        /// <summary>The newest fight, boss or not (never a run); null = none yet.</summary>
        public FightView Last => Fights.FirstOrDefault(f => f.Kind != FightKind.Run);

        /// <summary>The boss fight on now, or null: Battle's Last fight entry reads "Boss fight" with the live dot while there is one.</summary>
        public FightView LiveBoss => Fights.FirstOrDefault(f => f.Kind == FightKind.Boss && f.IsLive);

        public FightView ById(string id) => id == null ? null : Fights.FirstOrDefault(f => f.Id == id);
    }
}
