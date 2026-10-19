using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hearthwoven
{
    /// <summary>
    /// Measured since install, per session, with WHEN and WHERE: damage in 10-minute buckets per biome, enemy, cause and
    /// damage type (dealt and taken), and every death with its biome, killer and the damage of its last 30 seconds (TimelineSeconds).
    /// Absolute per session like everything measured: a resend replaces, never adds. 0.8.2: its last minutes are kept on this PC for the next
    /// connection in the same world (KeptMinutes), which joins them under its own log for the short windows only. Pure C#, unit-tested.
    /// </summary>
    public class EventLog
    {
        public const int BucketMinutes = 1, MaxDeaths = 200, MaxBuckets = 4000;
        /// <summary>Buckets are one minute, so Battle's "last 10 minutes" is a ten-minute window (a bucket counts when any part of it lies
        /// in the window: at most a minute more). Buckets older than FoldAfterMinutes (past every window but This session) are folded
        /// into ten-minute buckets (Fold), which keeps the log, and what is shared, near the size it had.</summary>
        public const int FoldAfterMinutes = 190, FoldedMinutes = 10;
        /// <summary>The bucket length of the keys of a copy read from a fellow player (an older sender: 10, as its JSON says); this PC's own: BucketMinutes.</summary>
        public int Span = BucketMinutes;
        string lastFold = "";
        /// <summary>A death keeps the hits you received in its last TimelineSeconds, with their time, at most MaxTimelineHits
        /// (the latest); the queue of recent hits is bounded by MaxRecent.</summary>
        public const int TimelineSeconds = 30, MaxTimelineHits = 40, MaxRecent = 400;
        // "2026-10-07T20:10Z|Swamp|taken|Draugr|EnemyHit|slash" -> damage
        public readonly Dictionary<string, float> Damage = new Dictionary<string, float>();
        // same key without the type -> number of hits
        public readonly Dictionary<string, float> Hits = new Dictionary<string, float>();
        /// <summary>0.8.2 (Joost, on a fellow's book: "why do their blocks show in All only?"): your held blocks and parries per minute,
        /// "2026-10-07T20:10Z|block" and "...|parry" -> count, the same rule and the same hook as SessionEvents.Blocks and Parries (a block counts
        /// every held one, a parry is one of them), so every window can cut them: 10 min .. 3 h on your book, a fellow's Session at yours. Its own
        /// map, so no reader of Damage or Hits ever meets it; folded with them. Two keys a minute at most.</summary>
        public readonly Dictionary<string, float> Guard = new Dictionary<string, float>();
        public const string GuardBlock = "block", GuardParry = "parry";
        /// <summary>0.9 (armor-share; Joost: "add friends' armour view ... to 0.9"): the hits on your armor per minute and damage type, the armour ledger's two
        /// numbers without foe or cause: "2026-10-07T20:14Z|b|slash" -> what reached your armor, "...|a|slash" -> what got through (ArmourHooks, the same hit
        /// ArmourTally and ArmourLog book), so a fellow's Defense cuts their 10 min .. 3 h and Session the way it cuts Guard (FellowTrail.Joined, logBefore,
        /// SessionFrom). Its own map: no reader of Damage or Hits ever meets it, and an older Hearthwoven ignores the key. Folded, joined and cut with the
        /// rest; at most 16 keys a minute and MaxArmorKeys in all (past it the oldest minute goes and ArmorClipped says so). Totals only, never a place.</summary>
        public readonly Dictionary<string, float> Armor = new Dictionary<string, float>();
        public const string ArmorReached = "b", ArmorThrough = "a";
        /// <summary>The bound of Armor: a day-long session (190 one-minute buckets of at most 16 keys, then at most 96 keys an hour folded) stays under it.</summary>
        public const int MaxArmorKeys = 6000;
        /// <summary>Armor reached MaxArmorKeys and its oldest minutes went (the page says so).</summary>
        public bool ArmorClipped;
        /// <summary>The log keeps Armor (this PC's own; a fellow's copy whose measuredLog has "armor"). False: a Hearthwoven before 0.9, whose armor is
        /// not shared (never read as nothing stopped).</summary>
        public bool ArmorKept = true;
        /// <summary>0.8.5: your hits after the foe's resistances and armor (AfterArmour), under the same keys as their Damage ("...|dealt|Troll|Axes|slash"
        /// -> after), every key on a foe from AfterFrom on (a type the foe is immune to: 0). Its own map, so no reader of Damage ever meets it;
        /// folded, joined and cut with it. Hits on players have none (their armor is not known here: Measured says so). Written as "stopped"
        /// (WriteFields: what the foe's armor took off, only where it took or added something), read back into After for every measured key, so a
        /// measured key always has its entry; one without (its after could not be worked out) is read as not measured.</summary>
        public readonly Dictionary<string, float> After = new Dictionary<string, float>();
        /// <summary>0.8.6 (DPS, SwingClock): your fighting time per minute and weapon kind: "2026-10-07T20:14Z|melee|s" -> seconds of fighting, "...|melee|d" ->
        /// the damage swung in them (the hits after the first of each stretch). Its own map, so no reader of Damage or Hits ever meets it; folded, joined
        /// and cut with them. Three weapon kinds, two keys each: at most six keys a minute. An older log has none: its windows show no pace.</summary>
        public readonly Dictionary<string, float> Swing = new Dictionary<string, float>();
        /// <summary>0.8.6 (DPS per named weapon): per minute, weapon and upgrade level ("2026-10-07T20:14Z|SwordIron|3|Swords"): its seconds of fighting, the damage swung
        /// in them, all its damage and the lowest and highest level of its skill you had. Unlike <see cref="Swing"/> it is shared on purpose (Snapshot), so a
        /// fellow's By weapon shows their weapons in the short windows; at most WeaponCells.PerMinute named weapons a minute. Folded, joined and cut with the rest.</summary>
        public readonly WeaponCells Weapons = new WeaponCells(true);
        /// <summary>The log keeps Weapons (this PC's own; a fellow's copy whose measuredLog has "weapons"). False: a Hearthwoven before 0.8.6, whose weapons are not known (never read as none).</summary>
        public bool WeaponsKept = true;
        long swingMinute = -1; readonly string[] swingKeys = new string[SwingClock.Kinds.Length * 2];   // this minute's keys, made once a minute, not per hit
        /// <summary>0.8.5: the bucket from which every hit of yours on a foe has its After (the first such hit's ten minutes, as a folded bucket would
        /// say it); "" = none (a log of a Hearthwoven before 0.8.5). A dealt key is measured when its bucket is at or after it (Measured).</summary>
        public string AfterFrom = "";
        /// <summary>The log keeps Guard (this PC's own; a fellow's copy whose measuredLog has "guard"). False: a copy from before 0.8.2, whose
        /// blocks are one untimed tally of the session (SessionEvents).</summary>
        public bool GuardKept = true;
        public readonly List<Death> Deaths = new List<Death>();
        /// <summary>Damage and deaths folded per biome, kept up to date as hits and deaths come in: what BiomeTally.FromLog(this) gives for a log
        /// filled through AddDamage and AddDeath, and also the falls past MaxDeaths, which the list (and so FromLog) no longer holds (0.8.5). The full
        /// pass over every key stays as the reference, and for a log read from a snapshot.</summary>
        public readonly BiomeTally Biome = new BiomeTally();
        public int DeathsNotListed;   // beyond MaxDeaths: counted, never silently lost
        /// <summary>0.8.2: how often the log changed (a hit, a death, a block): the book joins your kept minutes under it (Plugin.LogWithEarlier)
        /// once per change, not on every refresh.</summary>
        public int Changes { get; private set; }
        readonly Queue<Recent> recent = new Queue<Recent>();

        public class Death
        {
            public DateTime Time; public string Biome, Killer, Cause; public float X, Z;
            public readonly Dictionary<string, float> Last10s = new Dictionary<string, float>();   // "source|cause|type" -> damage
            /// <summary>How the damage built up: every hit received in the last 30 s, oldest first, one entry per damage type
            /// of a hit (Ago = seconds before the death). Empty for a death recorded before this was kept.</summary>
            public readonly List<Hit> Timeline = new List<Hit>();
        }
        public class Hit { public float Ago, Amount; public string Source, Cause, Type; }
        struct Recent { public DateTime T; public string Source, Cause; public HitData.DamageTypes D; }

        public static string Bucket(DateTime utc)
        {
            var t = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / BucketMinutes * BucketMinutes, 0, DateTimeKind.Utc);
            return t.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) + "Z";
        }

        /// <summary>Folds the buckets older than FoldAfterMinutes into ten-minute buckets (the minute's last digit to 0), once per minute.</summary>
        void Fold(DateTime utc)
        {
            var now = Bucket(utc); if (now == lastFold) return;
            lastFold = now;
            var cut = Bucket(utc.AddMinutes(-FoldAfterMinutes));
            FoldKeys(Damage, cut); FoldKeys(Hits, cut); FoldKeys(Guard, cut); FoldKeys(After, cut); FoldKeys(Swing, cut); FoldKeys(Armor, cut); Weapons.Fold(cut);
        }

        /// <summary>0.8.5: a bucket ("2026-10-07T20:14Z", or a key that starts with one) whose hits on foes have their After: at or after AfterFrom.</summary>
        public bool Measured(string bucketOrKey) =>
            AfterFrom.Length >= 17 && bucketOrKey != null && bucketOrKey.Length >= 17 && string.CompareOrdinal(bucketOrKey, 0, AfterFrom, 0, 17) >= 0;

        /// <summary>0.8.5: whether a dealt row has its After: its bucket is Measured and its target is no player (a player's armor is not known here).</summary>
        public bool Measured(string bucket, string other) => Measured(bucket) && other != PlayerTarget;
        /// <summary>The name a hit on another player is booked under (ClientHooks: the target's prefab).</summary>
        public const string PlayerTarget = "Player";

        /// <summary>The ten minutes a bucket folds into ("2026-10-07T20:14Z" -> "2026-10-07T20:10Z").</summary>
        static string TenOf(string bucket) => bucket.Length >= 17 ? bucket.Substring(0, 15) + "0" + bucket.Substring(16) : bucket;

        /// <summary>0.8.6: a hit of yours on a foe that earned <paramref name="seconds"/> of fighting with <paramref name="kind"/> (SwingClock.Kinds index) and
        /// <paramref name="damage"/> swung in them (SwingClock.Hit gives the seconds; 0 for the first hit of a stretch, which adds nothing). Called with the
        /// hit's AddDamage, which has folded the log and counted the change already. No allocation except once a minute (the minute's keys).</summary>
        public void AddSwing(DateTime utc, int kind, float seconds, float damage)
        {
            if (!(seconds > 0f) || !(damage > 0f) || !Json.IsFinite(seconds) || !Json.IsFinite(damage)) return;
            var tick = utc.Ticks / TimeSpan.TicksPerMinute;
            if (tick != swingMinute)
            {
                swingMinute = tick;
                var minute = Bucket(utc);
                for (int k = 0; k < SwingClock.Kinds.Length; k++) { swingKeys[2 * k] = minute + "|" + SwingClock.SecondsKeys[k]; swingKeys[2 * k + 1] = minute + "|" + SwingClock.DamageKeys[k]; }
            }
            Bump(swingKeys[2 * kind], seconds); Bump(swingKeys[2 * kind + 1], damage);
            void Bump(string k, float v) { Swing.TryGetValue(k, out var o); Swing[k] = o + v; }
        }

        /// <summary>0.8.6: a hit of yours with a weapon (<paramref name="prefab"/> at <paramref name="quality"/>, hit on the skill <paramref name="skill"/> at <paramref name="level"/>):
        /// its damage, and when it ended a stretch's seconds those and the damage swung in them. Called with the hit's AddDamage (which folded the log). No allocation
        /// except a weapon's first hit in a minute.</summary>
        public void AddWeapon(DateTime utc, string prefab, int quality, string skill, int level, float seconds, float swung, float all) =>
            Weapons.Hit(utc, prefab, quality, skill, level, seconds, swung, all, WeaponCells.PerMinute);

        /// <summary>0.9 (armor-share): one hit on your armor as ArmourHooks books it in the armour ledger: per battle type what reached your armor
        /// (<paramref name="before"/>) and what got through (<paramref name="after"/>), under this minute. Nothing for a hit that reached nothing.</summary>
        public void AddArmor(DateTime utc, HitData.DamageTypes before, HitData.DamageTypes after)
        {
            var b = ArmourTally.Of(before); var a = ArmourTally.Of(after);
            bool any = false;
            for (int k = 0; k < b.Length; k++) if (b[k] > 0 && Json.IsFinite(b[k])) any = true;
            if (!any) return;
            Fold(utc); Changes++;
            var minute = Bucket(utc);
            for (int k = 0; k < ArmourTally.Types.Length; k++)
            {
                if (b[k] > 0 && Json.IsFinite(b[k])) BumpArmor(minute + "|" + ArmorReached + "|" + ArmourTally.Types[k], b[k]);
                if (a[k] > 0 && Json.IsFinite(a[k])) BumpArmor(minute + "|" + ArmorThrough + "|" + ArmourTally.Types[k], a[k]);
            }
        }

        // a key of Armor: a new key past MaxArmorKeys first drops the oldest minute (the short windows and Session want the newest)
        void BumpArmor(string k, float v)
        {
            if (!Armor.ContainsKey(k) && Armor.Count >= MaxArmorKeys) DropOldestArmor();
            Armor.TryGetValue(k, out var o); Armor[k] = o + v;
        }

        void DropOldestArmor()
        {
            string oldest = null;
            foreach (var key in Armor.Keys) if (key.Length >= 17 && (oldest == null || string.CompareOrdinal(key, 0, oldest, 0, 17) < 0)) oldest = key;
            if (oldest == null) return;
            var gone = new List<string>();
            foreach (var key in Armor.Keys) if (string.CompareOrdinal(key, 0, oldest, 0, 17) == 0) gone.Add(key);
            foreach (var key in gone) Armor.Remove(key);
            ArmorClipped = true;
        }

        /// <summary>One held block (ClientHooks.Block, where SessionEvents.Blocks counts it); <paramref name="parry"/>: it was a parry too.</summary>
        public void AddGuard(DateTime utc, bool parry)
        {
            Fold(utc); Changes++;
            var minute = Bucket(utc);
            Bump(minute + "|" + GuardBlock);
            if (parry) Bump(minute + "|" + GuardParry);
            void Bump(string k) { Guard.TryGetValue(k, out var o); Guard[k] = o + 1; }
        }

        internal static void FoldKeys(Dictionary<string, float> d, string cut)   // ArmourLog folds its buckets the same way
        {
            List<KeyValuePair<string, float>> move = null;
            foreach (var kv in d)
            {
                var k = kv.Key;   // "2026-10-07T20:14Z|...": the minute's last digit is at 15
                if (k.Length > 16 && k[15] != '0' && string.CompareOrdinal(k, 0, cut, 0, 17) < 0) (move ?? (move = new List<KeyValuePair<string, float>>())).Add(kv);
            }
            if (move == null) return;
            foreach (var kv in move)
            {
                d.Remove(kv.Key);
                var folded = kv.Key.Substring(0, 15) + "0" + kv.Key.Substring(16);
                d.TryGetValue(folded, out var o); d[folded] = o + kv.Value;
            }
        }

        /// <summary>A hit: yours on <paramref name="other"/> (<paramref name="dealt"/>) or on you. <paramref name="after"/> (0.8.5, your hit on a foe):
        /// the same hit after the foe's resistances and armor (AfterArmour), kept under the same keys (AddAfter); null: none (a hit on a player).</summary>
        public void AddDamage(DateTime utc, string biome, bool dealt, string other, string cause, HitData.DamageTypes d, HitData.DamageTypes? after = null)
        {
            if (!dealt && DamageTally.TotalOf(d) <= 0f) return;   // a hit fully absorbed (a ward) is no hit received, as in DamageTally (review 0.6.5: the windows still counted it)
            Fold(utc); Changes++;
            var head = Bucket(utc) + "|" + (biome ?? "None") + "|" + (dealt ? "dealt" : "taken") + "|" + (other ?? "?") + "|" + (cause ?? "?");
            if (!Hits.ContainsKey(head) && Hits.Count >= MaxBuckets) head = Bucket(utc) + "|" + (biome ?? "None") + "|" + (dealt ? "dealt" : "taken") + "|other|other";
            Hits.TryGetValue(head, out var h); Hits[head] = h + 1;
            var biomeName = biome ?? "None";
            Each(d, (type, v) => { var k = head + "|" + type; Damage.TryGetValue(k, out var o); Damage[k] = o + v; Biome.AddDamage(biomeName, dealt, type, v); });
            if (dealt && after.HasValue) AfterOf(head, utc, d, after.Value);
            if (!dealt)
            {
                recent.Enqueue(new Recent { T = utc, Source = other, Cause = cause, D = d });
                while (recent.Count > 0 && ((utc - recent.Peek().T).TotalSeconds > TimelineSeconds || recent.Count > MaxRecent)) recent.Dequeue();
            }
        }

        /// <summary>0.8.5: the after of a hit of yours already added (AddDamage with the same <paramref name="utc"/>, biome, foe, skill and
        /// <paramref name="before"/>), under its keys. ClientHooks adds it once the game has found the weak spot the hit struck.</summary>
        public void AddAfter(DateTime utc, string biome, string other, string cause, HitData.DamageTypes before, HitData.DamageTypes after)
        {
            var head = Bucket(utc) + "|" + (biome ?? "None") + "|dealt|" + (other ?? "?") + "|" + (cause ?? "?");
            if (!Hits.ContainsKey(head)) head = Bucket(utc) + "|" + (biome ?? "None") + "|dealt|other|other";   // AddDamage put it under the cap's key
            Changes++;
            AfterOf(head, utc, before, after);
        }

        // every type of the before gets its after (an immune one adds 0); from the first one on, the log's hits on foes all have theirs
        void AfterOf(string head, DateTime utc, HitData.DamageTypes before, HitData.DamageTypes after)
        {
            if (AfterFrom.Length == 0) AfterFrom = TenOf(Bucket(utc));
            Each(before, (type, v) => { var k = head + "|" + type; After.TryGetValue(k, out var o); After[k] = o + Math.Max(0f, TypeOf(after, type)); });
        }

        public Death AddDeath(DateTime utc, string biome, float x, float z)
        {
            var death = new Death { Time = utc, Biome = biome ?? "None", X = x, Z = z };
            Recent last = default; bool any = false;
            var hits = new List<Recent>();
            foreach (var r in recent)
            {
                if ((utc - r.T).TotalSeconds > TimelineSeconds) continue;
                hits.Add(r);
                if ((utc - r.T).TotalSeconds > 10) continue;
                Each(r.D, (type, v) => { var k = r.Source + "|" + r.Cause + "|" + type; death.Last10s.TryGetValue(k, out var o); death.Last10s[k] = o + v; });
                last = r; any = true;
            }
            for (int i = Math.Max(0, hits.Count - MaxTimelineHits); i < hits.Count; i++)
            {
                var r = hits[i]; var ago = (float)Math.Max(0, (utc - r.T).TotalSeconds);
                Each(r.D, (type, v) => death.Timeline.Add(new Hit { Ago = ago, Source = r.Source ?? "?", Cause = r.Cause ?? "?", Type = type, Amount = v }));
            }
            death.Killer = any ? last.Source : "unknown";
            death.Cause = any ? last.Cause : "unknown";
            recent.Clear(); Changes++;
            if (Deaths.Count < MaxDeaths) Deaths.Add(death); else DeathsNotListed++;
            Biome.AddDeath(death.Biome);   // 0.8.5: a fall past the list's cap keeps its place (it was counted, LocalTotals' Deaths, with no biome)
            return death;
        }

        /// <summary>
        /// 0.8.2 (J): a fellow's earlier connections' log put under their latest copy's (FellowTrail.Joined): every bucket summed per key, the
        /// deaths of both in time order. A new log; the two stay as they are. Span: the wider of the two (a bucket counts when any part of it lies
        /// in a window); GuardKept: the latest copy's (its version decides whether blocks are timed).
        /// </summary>
        public static EventLog Join(EventLog earlier, EventLog later)
        {
            if (earlier == null) return later;
            if (later == null) return earlier;
            // 0.8.5: After from the earlier part's start when it has one, else from the later's (the earlier's minutes, older, stay unmeasured)
            var j = new EventLog { Span = Math.Max(earlier.Span, later.Span), GuardKept = later.GuardKept, ArmorKept = earlier.ArmorKept && later.ArmorKept, ArmorClipped = earlier.ArmorClipped || later.ArmorClipped, DeathsNotListed = earlier.DeathsNotListed + later.DeathsNotListed,
                                   AfterFrom = earlier.AfterFrom.Length > 0 ? earlier.AfterFrom : later.AfterFrom, WeaponsKept = later.WeaponsKept };
            foreach (var log in new[] { earlier, later })
            {
                Add(log.Damage, j.Damage); Add(log.Hits, j.Hits); Add(log.Guard, j.Guard); Add(log.After, j.After); Add(log.Swing, j.Swing); Add(log.Armor, j.Armor); j.Weapons.Merge(log.Weapons);
                j.Deaths.AddRange(log.Deaths);
            }
            j.Deaths.Sort((a, b) => a.Time.CompareTo(b.Time));
            return j;
            void Add(Dictionary<string, float> from, Dictionary<string, float> to) { foreach (var kv in from) { to.TryGetValue(kv.Key, out var o); to[kv.Key] = o + kv.Value; } }
        }

        /// <summary>
        /// 0.8.2: the part of the log from <paramref name="fromUtc"/> to <paramref name="untilUtc"/> (KeptMinutes, FellowTrail.Joined): a new log
        /// with the one-minute buckets that start at or after fromUtc and end at or before untilUtc, and the deaths in between. fromUtc on a whole
        /// ten minutes never cuts a folded bucket in two. The log stays as it is.
        /// </summary>
        public EventLog Between(DateTime fromUtc, DateTime untilUtc)
        {
            var b = new EventLog { Span = Span, GuardKept = GuardKept, AfterFrom = AfterFrom, WeaponsKept = WeaponsKept, ArmorKept = ArmorKept, ArmorClipped = ArmorClipped };
            string lo = Bucket(fromUtc), hi = untilUtc == DateTime.MaxValue ? null : Bucket(untilUtc);
            KeysBetween(Damage, b.Damage, lo, hi); KeysBetween(Hits, b.Hits, lo, hi); KeysBetween(Guard, b.Guard, lo, hi); KeysBetween(After, b.After, lo, hi); KeysBetween(Swing, b.Swing, lo, hi); KeysBetween(Armor, b.Armor, lo, hi);
            foreach (var kv in Weapons.Between(lo, hi).Map) b.Weapons.Map[kv.Key] = kv.Value;
            foreach (var d in Deaths) if (d.Time >= fromUtc && d.Time < untilUtc) b.Deaths.Add(d);
            return b;
        }

        /// <summary>The keys of <paramref name="from"/> whose minute ("2026-10-07T20:14Z|...") is at or after <paramref name="lo"/> and before
        /// <paramref name="hi"/> (null: no end), added into <paramref name="to"/>: EventLog's, DeedLog's and ArmourLog's keys alike.</summary>
        internal static void KeysBetween(IDictionary<string, float> from, IDictionary<string, float> to, string lo, string hi)
        {
            foreach (var kv in from)
            {
                var k = kv.Key;
                if (k.Length < 17 || string.CompareOrdinal(k, 0, lo, 0, 17) < 0 || (hi != null && string.CompareOrdinal(k, 0, hi, 0, 17) >= 0)) continue;
                to.TryGetValue(k, out var o); to[k] = o + kv.Value;
            }
        }

        static void Each(HitData.DamageTypes d, Action<string, float> f)
        {
            void A(string k, float v) { if (v != 0f && Json.IsFinite(v)) f(k, v); }
            A("blunt", d.m_blunt); A("slash", d.m_slash); A("pierce", d.m_pierce); A("chop", d.m_chop); A("pickaxe", d.m_pickaxe);
            A("fire", d.m_fire); A("frost", d.m_frost); A("lightning", d.m_lightning); A("poison", d.m_poison); A("spirit", d.m_spirit); A("damage", d.m_damage);
        }

        // 0.9: Armor to tenths, nothing that rounds to 0
        IEnumerable<KeyValuePair<string, float>> ArmorRounded()
        {
            foreach (var kv in Armor) { var v = (float)Math.Round(kv.Value, 1); if (v > 0) yield return new KeyValuePair<string, float>(kv.Key, v); }
        }

        /// <summary>0.9: a shared "armor" map read with the intake's care: only "minute|b|type" and "minute|a|type" keys of the eight battle types with
        /// a finite amount above 0, at most MaxArmorKeys (the rest is said as clipped). null: the sender keeps none (ArmorKept false).</summary>
        void ReadArmor(Dictionary<string, object> armor)
        {
            ArmorKept = armor != null;
            if (armor == null) return;
            var all = new Dictionary<string, float>(); MiniJson.Into(armor, all);
            foreach (var kv in all)
            {
                var p = kv.Key.Split('|');
                if (p.Length != 3 || p[0].Length != 17 || (p[1] != ArmorReached && p[1] != ArmorThrough) || Array.IndexOf(ArmourTally.Types, p[2]) < 0) continue;
                if (!(kv.Value > 0) || !Json.IsFinite(kv.Value)) continue;
                if (Armor.Count >= MaxArmorKeys) { ArmorClipped = true; break; }
                Armor[kv.Key] = kv.Value;
            }
        }

        // what the foe's armor took off each measured key (a weakness: less than 0), only where it is not nothing: a key it left as it was is not written
        IEnumerable<KeyValuePair<string, float>> Stopped()
        {
            foreach (var kv in After)
                if (Damage.TryGetValue(kv.Key, out var d) && Math.Abs(d - kv.Value) >= 0.0005f) yield return new KeyValuePair<string, float>(kv.Key, d - kv.Value);
        }

        // one type of a hit by the name Each gives it
        static float TypeOf(HitData.DamageTypes d, string type)
        {
            switch (type)
            {
                case "blunt": return d.m_blunt; case "slash": return d.m_slash; case "pierce": return d.m_pierce; case "chop": return d.m_chop; case "pickaxe": return d.m_pickaxe;
                case "fire": return d.m_fire; case "frost": return d.m_frost; case "lightning": return d.m_lightning; case "poison": return d.m_poison; case "spirit": return d.m_spirit;
                default: return d.m_damage;
            }
        }

        /// <param name="swing">0.8.6: the fighting time per minute and weapon kind. Your PC keeps it (the local file, KeptMinutes); the shared copy leaves it out.</param>
        public void WriteTo(Json j, string key = "measuredLog", bool swing = true) { j.Key(key).Open(); WriteFields(j, swing); j.Close(); }

        /// <summary>The log's fields inside an object the caller opened (WriteTo; KeptMinutes adds its own beside them).</summary>
        internal void WriteFields(Json j, bool swing = true)
        {
            j.Num("bucketMinutes", BucketMinutes).Num("deathsNotListed", DeathsNotListed).Dict("damage", Damage).Dict("hits", Hits).Dict("guard", Guard);
            if (swing && Swing.Count > 0) j.Dict("swing", Swing);   // 0.8.6: an older Hearthwoven ignores the key
            if (WeaponsKept) Weapons.WriteTimed(j, "weapons");   // 0.8.6: shared on purpose; written also empty, so a reader can tell "no weapons" from an older Hearthwoven
            if (ArmorKept) { j.Dict("armor", ArmorRounded()); if (ArmorClipped) j.Num("armorClipped", 1); }   // 0.9 (armor-share): written also empty, so a reader can tell "nothing reached their armor" from an older Hearthwoven; an older reader ignores the key
            if (AfterFrom.Length > 0) j.Str("afterFrom", AfterFrom).Dict("stopped", Stopped());   // 0.8.5: the after as what was stopped, nothing where nothing was
            j.Key("deaths").OpenArr();
            foreach (var d in Deaths)
            {
                j.Open().Str("t", d.Time.ToString("o", CultureInfo.InvariantCulture)).Str("biome", d.Biome).Str("killer", d.Killer).Str("cause", d.Cause)
                 .Num("x", d.X).Num("z", d.Z).Dict("last10s", d.Last10s);
                j.Key("timeline").OpenArr();
                foreach (var h in d.Timeline) j.Open().Num("ago", Math.Round(h.Ago, 2)).Str("source", h.Source).Str("cause", h.Cause).Str("type", h.Type).Num("amount", h.Amount).Close();
                j.CloseArr().Close();
            }
            j.CloseArr();
        }

        /// <summary>Reads what WriteTo wrote (a fellow's "measuredLog", your kept minutes) into this log; null: nothing, as an older sender's
        /// copy without a log (ten-minute buckets). A shared copy's deaths carry no position (0).</summary>
        public EventLog ReadFrom(Dictionary<string, object> log)
        {
            MiniJson.Into(MiniJson.Obj(log, "damage"), Damage); MiniJson.Into(MiniJson.Obj(log, "hits"), Hits);
            var wk = MiniJson.Obj(log, "weapons"); WeaponsKept = wk != null;   // 0.8.6: the weapons per minute (an older sender keeps none: not "no weapons")
            if (wk != null) foreach (var kv in WeaponCells.ReadTimed(wk).Map) Weapons.Map[kv.Key] = kv.Value;
            MiniJson.Into(MiniJson.Obj(log, "swing"), Swing);   // 0.8.6: your fighting time per minute and weapon kind (an older sender keeps none)
            MiniJson.Into(MiniJson.Obj(log, "guard"), Guard); GuardKept = MiniJson.Obj(log, "guard") != null;   // 0.8.2: blocks and parries per minute (an older sender keeps none)
            ReadArmor(MiniJson.Obj(log, "armor")); if (MiniJson.Num(log, "armorClipped") > 0) ArmorClipped = true;   // 0.9 (armor-share): the hits on their armor per minute (an older sender keeps none)
            AfterFrom = MiniJson.Str(log, "afterFrom"); if (AfterFrom.Length < 17) AfterFrom = "";   // 0.8.5: your hits after the foe's armor (an older sender keeps none)
            if (AfterFrom.Length > 0)
            {
                var stopped = new Dictionary<string, float>(); MiniJson.Into(MiniJson.Obj(log, "stopped"), stopped);
                foreach (var kv in Damage)
                {
                    var p = kv.Key.Split('|');   // time|biome|dir|foe|cause|type
                    if (p.Length >= 6 && p[2] == "dealt" && Measured(p[0], p[3])) After[kv.Key] = kv.Value - (stopped.TryGetValue(kv.Key, out var st) ? st : 0f);
                }
            }
            Span = log != null && MiniJson.Num(log, "bucketMinutes") >= 1 ? (int)MiniJson.Num(log, "bucketMinutes") : 10;   // an older sender kept ten-minute buckets
            if (log != null && log.TryGetValue("deaths", out var ds) && ds is List<object> deaths)
                foreach (var o in deaths)
                {
                    if (!(o is Dictionary<string, object> e) || !DateTime.TryParse(MiniJson.Str(e, "t"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)) continue;
                    var d = new Death { Time = t.ToUniversalTime(), Biome = MiniJson.Str(e, "biome", "None"), Killer = MiniJson.Str(e, "killer", "unknown"), Cause = MiniJson.Str(e, "cause", "unknown"),
                                        X = (float)MiniJson.Num(e, "x"), Z = (float)MiniJson.Num(e, "z") };
                    MiniJson.Into(MiniJson.Obj(e, "last10s"), d.Last10s);
                    if (e.TryGetValue("timeline", out var tl) && tl is List<object> hits)   // an older snapshot has none: the totals stay
                        foreach (var h in hits)
                            if (h is Dictionary<string, object> x)
                                d.Timeline.Add(new Hit { Ago = (float)MiniJson.Num(x, "ago"), Amount = (float)MiniJson.Num(x, "amount"), Source = MiniJson.Str(x, "source", "?"), Cause = MiniJson.Str(x, "cause", "?"), Type = MiniJson.Str(x, "type", "damage") });
                    Deaths.Add(d);
                }
            return this;
        }
    }
}
