using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// The game side of the battle recorder (0.8, BattleRecord.cs). Only reads: every hook reports to Plugin.Battle and changes nothing.
    ///   - your hit on a foe and a foe's hit on you come from ClientHooks' damage hooks (Dealt, Received, DotFrom: one call each);
    ///   - who hit a creature: its own network record ("Attackers" count and one flag per player name, set by the PC that controls it in
    ///     Character.RPC_Damage), read on your PC when its count grew and when it is destroyed;
    ///   - the kill: Game.RPC_RegisterKill, the game's own kill message to every player who hit the foe (KillCredit);
    ///   - the creature's network object destroyed (ZDOMan.HandleDestroyedZDO, which every PC runs: FoeGone);
    ///   - once a second (Tick, from Plugin.Update): a fight ends after a minute without hits, waiting kills and destroys are settled.
    /// Players and tamed creatures are never foes here (they stay in the damage tally as before).
    /// 0.9 (work/hearthwoven-0.8.5/fights/DESIGN.md): each hit also says whether the foe is a boss (Character.IsBoss, the same flag that gives it the
    /// game's boss bar, EnemyHud) and where it stood (Spot: the arena rule, on this PC only); held blocks and parries come from FeatsHooks' block
    /// hook, your deaths from ClientHooks' death hook; once a second Watch reads whether you are in an interior and which (dungeon runs) and the
    /// bosses loaded here (their health, for boss fights a fellow started).
    ///
    /// 0.8.5: your damage after the foe's resistances and armor is worked out at each hit on your PC (AfterOf, AfterArmour.cs) and kept beside it
    /// (the log's After, the dealt ledger). Battle.DealtAfterArmour (off by default, a developer setting) is now its check: where the game applies
    /// them, on a foe this PC controls, it compares the game's own numbers with the ones worked out at your hit and logs the first CheckLines (0.8
    /// recorded them here and had a fellow's PC send its own per hit: blind to foes a PC without the setting controls, ARMOUR-SCOPE.md section 7).
    /// Off: none of its hooks is patched (Prepare), nothing is measured. Read when the game starts.
    /// </summary>
    static class BattleHooks
    {
        static ConfigEntry<bool> dealtAfterArmour;
        /// <summary>Battle.DealtAfterArmour as the game started (the check's hooks are patched only then).</summary>
        internal static bool DealtAfterArmour => dealtAfterArmour != null && dealtAfterArmour.Value;

        internal static void Bind(ConfigFile config)
        {
            dealtAfterArmour = config.Bind("Battle", "DealtAfterArmour", false,
                "Developer tool: checks the damage after the foe's resistances and armor that Hearthwoven works out at each of your hits against the game's own numbers, on foes your PC controls (play alone for the most). Logs the first " + CheckLines + " hits. Takes effect at the next game start. Off for players.");
        }

        /// <summary>
        /// 0.8.5: your hit on <paramref name="target"/> (<paramref name="before"/>: as you swung it) after the foe's resistances and armor, worked out on
        /// your PC (AfterArmour.Of) with the game's own data: its damage modifiers (Character.GetDamageModifiers: the creature's, the weak spot's when
        /// the hit struck one, hit.m_weakSpot as Character.Damage set it, and the effects on it this PC knows), the world level and its armor per
        /// level. null for a player (their armor is not known here). One boxed copy of the modifiers (GetDamageModifiers, as the game makes per hit)
        /// and a few multiplications; nothing is sent.
        /// </summary>
        internal static HitData.DamageTypes? AfterOf(Character target, HitData hit, HitData.DamageTypes before)
        {
            if (hit == null || target == null || target is Player) return null;
            var spots = target.m_weakSpots; var i = hit.m_weakSpot;
            var spot = spots != null && i >= 0 && i < spots.Length ? spots[i] : null;   // Character.GetWeakSpot
            var after = AfterArmour.Of(before, target.GetDamageModifiers(spot), Game.m_worldLevel, Game.instance ? Game.instance.m_worldLevelEnemyBaseAC : 100);
            if (DealtAfterArmour) Compare(target, before, after);
            return after;
        }

        static FoeId Of(ZDOID z) => new FoeId(z.UserID, z.ID);
        static string Prefab(Component c) => c == null ? "?" : Utils.GetPrefabName(c.gameObject);
        static readonly Func<FoeId, bool> loaded = id => ZNetScene.instance == null || ZNetScene.instance.FindInstance(new ZDOID(id.User, id.Id)) != null;
        static bool Foe(Character c) => c != null && !(c is Player) && !c.IsTamed();

        // ---------- the recorder's inputs (called inside ClientHooks' guarded hooks) ----------

        /// <summary>Your hit on a creature (ClientHooks.DamageDealt), <paramref name="kind"/> its prefab as that hook already has it.</summary>
        internal static void Dealt(Character target, string kind, HitData hit)
        {
            if (hit == null || !Foe(target)) return;
            var z = target.GetZDOID(); if (z.IsNone()) return;
            var id = Of(z); var rec = Plugin.Battle; Wire(rec);
            rec.Dealt(DateTime.UtcNow, id, kind, target.m_name, ClientHooks.Biome(), hit.m_damage, target.IsBoss(), SpotOf(target));
            ReadAttackers(ZDOMan.instance?.GetZDO(z), id, rec);
        }

        /// <summary>A hit on you after your armour (ClientHooks.DamageTaken), <paramref name="source"/> as that hook names it (the attacker's prefab,
        /// or for a burning or poison tick the one who set it).</summary>
        internal static void Received(HitData hit, string source)
        {
            if (hit == null) return;
            var rec = Plugin.Battle;
            if (!hit.m_attacker.IsNone())
            {
                var id = Of(hit.m_attacker);
                var a = hit.GetAttacker();
                if (a != null) { if (Foe(a)) rec.Received(DateTime.UtcNow, id, source, a.m_name, ClientHooks.Biome(), hit.m_damage, a.IsBoss(), SpotOf(a)); }
                else if (rec.Knows(id)) rec.Received(DateTime.UtcNow, id, null, null, ClientHooks.Biome(), hit.m_damage);   // no longer loaded: the kind as first seen
                return;
            }
            var slot = hit.m_hitType == HitData.HitType.Burning ? 0 : hit.m_hitType == HitData.HitType.Poisoned ? 1 : -1;
            if (slot < 0 || dot[slot].Id.IsNone || Time.time - dot[slot].At >= 30f) return;   // the same 30 s as ClientHooks.Source
            rec.Received(DateTime.UtcNow, dot[slot].Id, dot[slot].Kind, dot[slot].Token, ClientHooks.Biome(), hit.m_damage);
        }

        /// <summary>0.9: where a creature stands, for the arena rule (this PC only, never shared or saved).</summary>
        internal static Spot SpotOf(Component c)
        {
            if (c == null) return default;
            var p = c.transform.position;
            return new Spot(p.x, p.y, p.z);
        }

        /// <summary>0.9: a block of yours that held (FeatsHooks.Block), against the foe whose hit it was; a parry by the game's rule.</summary>
        internal static void Blocked(HitData hit, Character attacker, bool parry)
        {
            if (hit == null || hit.m_attacker.IsNone() || (attacker != null && !Foe(attacker))) return;
            Plugin.Battle.Blocked(DateTime.UtcNow, Of(hit.m_attacker), parry);
        }

        /// <summary>0.9: you died (ClientHooks.Died): counted in the running fight, every open boss fight and the dungeon run you are in.</summary>
        internal static void Died() => Plugin.Battle.Died(DateTime.UtcNow);

        // who set you burning (fire, spirit) or poisoned you: the ticks carry no attacker (ClientHooks.DotSource keeps the prefab, this the creature)
        struct Dot { public FoeId Id; public string Kind, Token; public float At; }
        static readonly Dot[] dot = new Dot[2];
        internal static void DotFrom(HitData hit, Character attacker)
        {
            if (hit == null || !Foe(attacker)) return;
            var d = new Dot { Id = Of(hit.m_attacker), Kind = Prefab(attacker), Token = attacker.m_name, At = Time.time };
            if (hit.m_damage.m_fire > 0f || hit.m_damage.m_spirit > 0f) dot[0] = d;
            if (hit.m_damage.m_poison > 0f) dot[1] = d;
        }

        /// <summary>The creature's record of who hit it, when its count grew since the recorder last saw it (names cost a string per player).</summary>
        static void ReadAttackers(ZDO zdo, FoeId id, BattleRecorder rec)
        {
            if (zdo == null) return;
            var n = zdo.GetInt(ZDOVars.s_attackers);
            if (n <= 0 || n <= rec.AttackersSeen(id)) return;
            rec.Attackers(id, n, NamesOn(zdo));
        }

        /// <summary>The fellow players the record names (Character.RPC_Damage sets the flag "Attackers" hash + player name), not you.</summary>
        static string[] NamesOn(ZDO zdo)
        {
            var list = ZNet.instance?.GetPlayerList(); if (list == null) return FoeEncounter.NoNames;
            var me = Player.m_localPlayer ? Player.m_localPlayer.GetPlayerName() : null;
            List<string> names = null;
            foreach (var p in list)
            {
                if (string.IsNullOrEmpty(p.m_name) || p.m_name == me) continue;
                if (zdo.GetBool(ZDOVars.s_attackers + p.m_name)) (names ?? (names = new List<string>())).Add(p.m_name);
            }
            return names == null ? FoeEncounter.NoNames : names.ToArray();
        }

        // Dev.SelfCheck: one line per settled foe (the first 40 of a session), what the record says and how it knew, to hold against the game
        static int checkLines;
        static readonly Action<FoeEncounter, string> say = (e, how) =>
        {
            try
            {
                if (!DevCheck.On || checkLines >= 40) return;
                checkLines++;
                DevCheck.Book.Say("INFO", "battle", e.Kind + " (" + e.Biome + ") " + e.OutcomeText + ": " + how + "; you dealt " + e.DealtTotal.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " in " + e.HitsDealt +
                    " hits, received " + e.ReceivedTotal.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "; fight " + e.Fight + " (the game's kill counter should grow by one for each defeated foe)");
            }
            catch (Exception x) { HookGuard.Fail(x, "BattleHooks.Say"); }
        };
        static BattleRecorder wired;
        static void Wire(BattleRecorder rec) { if (rec != wired) { wired = rec; checkLines = 0; } rec.Loaded = loaded; rec.Settled = DevCheck.On ? say : null; }

        static float nextTick, nextNear;
        /// <summary>From Plugin.Update while playing: one comparison per frame, the recorder's Tick once a second, and while a fight is on who is
        /// near you every BattleRecorder.NearEvery seconds (NearYou).</summary>
        internal static void Tick(float now)
        {
            if (now < nextTick) return;
            nextTick = now + 1f;
            try
            {
                var rec = Plugin.Battle; Wire(rec); var utc = DateTime.UtcNow; rec.Tick(utc);
                if (!rec.FightOn) FeatsCounters.OnFightOver(Plugin.FeatsLedger);   // 0.9 Held the Line: the fight's run counts once it is over
                if (rec.FightOn && now >= nextNear) { nextNear = now + BattleRecorder.NearEvery; NearYou(rec, utc); }
                if (!Panel.SampleMode.On) Watch(rec, utc);
            }
            catch (Exception e) { HookGuard.Fail(e, "BattleHooks.Tick"); }
        }

        /// <summary>
        /// Last fight (0.8): the players loaded near you (Player.GetAllPlayers, the game's own list on this PC) within BattleRecorder.NearMetres,
        /// each credited NearEvery seconds in the running fight. Reads positions only; nothing is written to the world or shared. No allocation:
        /// the game's list, squared distances, the names the game already holds.
        /// </summary>
        static void NearYou(BattleRecorder rec, DateTime utc)
        {
            var me = Player.m_localPlayer; if (me == null) return;
            var at = me.transform.position; const float reach = BattleRecorder.NearMetres * BattleRecorder.NearMetres;
            var all = Player.GetAllPlayers(); if (all == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p == null || p == me || (p.transform.position - at).sqrMagnitude > reach) continue;
                rec.Near(utc, p.GetPlayerName(), BattleRecorder.NearEvery);
            }
        }

        /// <summary>
        /// 0.9, once a second. Dungeon runs: you are in an interior by the game's own rule (Character.InInterior: y above 3000, where Location.Awake
        /// builds every dungeon, 5000 m above its entrance); which one is the location of that zone (Location.GetZoneLocation, the game's own lookup
        /// for a point inside), named by its prefab ("Crypt2") and its own label when it has one (m_discoverLabel, "$location_dvergrboss"), looked
        /// up only when the zone changes. Boss fights: every boss loaded here
        /// (Character.GetAllCharacters, the game's list on this PC; IsBoss, 0.8.5: or one of Hildir's minibosses, BattleRecorder.IsBoss), its health
        /// left (GetHealthPercentage: the creature's network record, synced to every PC) and place. Reads only; no allocation but the place name once
        /// per interior.
        /// </summary>
        static void Watch(BattleRecorder rec, DateTime utc)
        {
            var me = Player.m_localPlayer; if (me == null) return;
            var at = me.transform.position;
            if (Character.InInterior(at))
            {
                var zone = Spot.ZoneOf(at.x, at.z);
                if (placeKind == null || zone != placeZone)
                {
                    placeZone = zone;
                    var loc = Location.GetZoneLocation(at);
                    placeKind = loc != null ? Utils.GetPrefabName(loc.gameObject) : "Interior";
                    placeToken = loc != null ? loc.m_discoverLabel : null;   // the label the game itself uses for a found place (Player.UpdateBiome)
                }
                rec.Inside(utc, placeKind, zone, placeToken);
            }
            else { placeKind = null; rec.Inside(utc, null, 0); }
            var all = Character.GetAllCharacters(); if (all == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !BattleRecorder.IsBoss(c.IsBoss(), null, c.m_name) || c.IsDead() || !Foe(c)) continue;   // 0.8.5: Hildir's minibosses by their token, flagged or not
                var z = c.GetZDOID(); if (z.IsNone()) continue;
                rec.BossSeen(utc, Of(z), Prefab(c), c.m_name, c.GetHealthPercentage(), SpotOf(c));
            }
        }
        static string placeKind, placeToken; static long placeZone;

        // ---------- the game's kill message and the destroyed creature ----------

        /// <summary>Game.RPC_RegisterKill: the PC that controls a dying creature sends it to every player who hit it (the game's own kill counter
        /// counts it for each), with how many players hit it. It arrives here for a kill on any PC, and directly when this PC controlled the foe.</summary>
        [HarmonyPatch(typeof(Game), nameof(Game.RPC_RegisterKill))]
        static class KillCredit
        {
            static void Prefix(string enemyName, int attackers)
            {
                try { Plugin.Battle.Killed(DateTime.UtcNow, enemyName, attackers); }
                catch (Exception e) { HookGuard.Fail(e, "BattleHooks.KillCredit.Prefix"); }
            }
        }

        /// <summary>Every destroyed network object in the world passes here: one dictionary lookup, then only a creature the recorder knows.</summary>
        [HarmonyPatch(typeof(ZDOMan), "HandleDestroyedZDO")]
        static class FoeGone
        {
            static void Prefix(ZDOID uid)
            {
                try
                {
                    var rec = Plugin.Battle; var id = Of(uid);
                    if (!rec.Knows(id)) return;
                    ReadAttackers(ZDOMan.instance?.GetZDO(uid), id, rec);   // the record as this PC last had it (still there in the prefix)
                    rec.Destroyed(DateTime.UtcNow, id);
                }
                catch (Exception e) { HookGuard.Fail(e, "BattleHooks.FoeGone.Prefix"); }
            }
        }

        // ---------- Battle.DealtAfterArmour: the check of AfterOf where the game applies the foe's resistances and armor ----------

        internal const int CheckLines = 30;
        struct Seen { public int Frame; public ZDOID Foe; public HitData.DamageTypes Incoming, After; }
        static Seen seen;   // your last hit on a foe this PC controls as the game worked it out (inside Character.Damage, before AfterOf runs on it)
        sealed class FoeMark { public HitData Hit; public Character Target; public bool Resisted; public HitData.DamageTypes Incoming, Before; }
        static FoeMark foeHit;   // a player's hit on a creature this PC controls, in flight inside RPC_Damage; null for every other hit
        static int checkedHits;

        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        static class MarkFoeHit
        {
            static bool Prepare() => DealtAfterArmour;
            static void Prefix(Character __instance, HitData hit, out object __state)
            {
                __state = null;
                try
                {
                    __state = foeHit;
                    foeHit = hit != null && __instance != null && !(__instance is Player) && !hit.m_attacker.IsNone() && __instance.IsOwner() ? new FoeMark { Hit = hit, Target = __instance } : null;
                }
                catch (Exception e) { HookGuard.Fail(e, "BattleHooks.MarkFoeHit.Prefix"); }
            }
            static void Finalizer(object __state) { try { foeHit = __state as FoeMark; } catch (Exception e) { HookGuard.Fail(e, "BattleHooks.MarkFoeHit.Finalizer"); } }
        }

        // before and after the foe's resistances (a blocking foe's shield calls it first on the same hit; the main call comes later and wins)
        [HarmonyPatch(typeof(HitData), nameof(HitData.ApplyResistance))]
        static class FoeResist
        {
            static bool Prepare() => DealtAfterArmour;
            static void Prefix(HitData __instance)
            {
                try { var m = foeHit; if (m == null || !ReferenceEquals(__instance, m.Hit)) return; m.Incoming = __instance.m_damage; }
                catch (Exception e) { HookGuard.Fail(e, "BattleHooks.FoeResist.Prefix"); }
            }
            static void Postfix(HitData __instance)
            {
                try { var m = foeHit; if (m == null || !ReferenceEquals(__instance, m.Hit)) return; m.Before = __instance.m_damage; m.Resisted = true; }
                catch (Exception e) { HookGuard.Fail(e, "BattleHooks.FoeResist.Postfix"); }
            }
        }

        // after the world-level armour (Character.RPC_Damage: ApplyArmor when the world level is above 0), just before the foe's health drops
        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        static class FoeAfterArmour
        {
            static bool Prepare() => DealtAfterArmour;
            static void Prefix(Character __instance, HitData hit)
            {
                try
                {
                    var m = foeHit;
                    if (m == null || !m.Resisted || !ReferenceEquals(hit, m.Hit) || __instance != m.Target) return;
                    m.Resisted = false;   // one record per hit
                    Check(__instance, hit, m);
                }
                catch (Exception e) { HookGuard.Fail(e, "BattleHooks.FoeAfterArmour.Prefix"); }
            }
        }

        /// <summary>Your hit on a foe this PC controls, as the game worked it out: kept for AfterOf, which runs on the same hit right after (the game
        /// handles a hit on a foe this PC controls inside Character.Damage, in the same frame).</summary>
        static void Check(Character target, HitData hit, FoeMark m)
        {
            if (checkedHits >= CheckLines || !(hit.GetAttacker() is Player p) || p != Player.m_localPlayer) return;
            seen = new Seen { Frame = Time.frameCount, Foe = target.GetZDOID(), Incoming = m.Incoming,
                              After = MeasuredAfter(m.Before, hit.m_damage, Game.m_worldLevel, Game.instance ? Game.instance.m_worldLevelEnemyBaseAC : 100) };
        }

        /// <summary>AfterOf's numbers for a hit against the game's for the same hit (the same frame, the same foe): each type's share left after the
        /// foe (game: after / what reached its resistances; Hearthwoven: after / before). A sneak attack or a staggered foe raises both of the game's
        /// numbers alike, so the shares still agree. One line per hit for the first CheckLines.</summary>
        static void Compare(Character target, HitData.DamageTypes before, HitData.DamageTypes after)
        {
            var s = seen;
            if (checkedHits >= CheckLines || s.Frame != Time.frameCount || s.Foe != target.GetZDOID()) return;
            seen = default;
            float[] i = ArmourTally.Of(s.Incoming), g = ArmourTally.Of(s.After), b = ArmourTally.Of(before), a = ArmourTally.Of(after);
            var parts = new List<string>(); var same = true;
            string F(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            for (int k = 0; k < ArmourTally.Types.Length; k++)
            {
                if (!(b[k] > 0) && !(i[k] > 0)) continue;
                double mine = b[k] > 0 ? a[k] / b[k] : 0, theirs = i[k] > 0 ? g[k] / i[k] : 0;
                var differs = Math.Abs(mine - theirs) > 0.01; if (differs) same = false;
                parts.Add(ArmourTally.Types[k] + " " + F(mine) + (differs ? " (game " + F(theirs) + ")" : ""));
            }
            if (parts.Count == 0) return;
            checkedHits++;
            Debug.Log("[Hearthwoven] Battle.DealtAfterArmour check " + checkedHits + "/" + CheckLines + ": " + Prefab(target) + ", share left after the foe: " + string.Join(", ", parts.ToArray()) + (same ? " (same as the game)" : " DIFFERS"));
        }

        /// <summary>
        /// The hit after the foe's armour as the game holds it at ApplyDamage: it has already taken fire, spirit and poison out of the hit (they become
        /// burning and poison on the foe), after armour scaled every type by one ratio (HitData.DamageTypes.ApplyArmor). So: no world level, no armour
        /// (after = before); else the ratio the remaining types show, and only for a hit of fire, spirit or poison alone the game's own formula.
        /// </summary>
        internal static HitData.DamageTypes MeasuredAfter(HitData.DamageTypes before, HitData.DamageTypes now, int worldLevel, int baseAc)
        {
            var a = now;
            float ratio = 1f;
            if (worldLevel > 0)
            {
                float rest = before.m_blunt + before.m_slash + before.m_pierce + before.m_frost + before.m_lightning;
                float restAfter = now.m_blunt + now.m_slash + now.m_pierce + now.m_frost + now.m_lightning;
                float dots = before.m_fire + before.m_poison + before.m_spirit;
                if (rest > 0f) ratio = restAfter / rest;
                else if (dots > 0f) ratio = HitData.DamageTypes.ApplyArmor(dots, worldLevel * baseAc) / dots;
            }
            a.m_fire = before.m_fire * ratio; a.m_poison = before.m_poison * ratio; a.m_spirit = before.m_spirit * ratio;
            return a;
        }

        // ---------- the panel ----------

        /// <summary>The battle record into the panel's input (PanelUi.Gather): the recorder ticked to now, the book, the counts and the dates.</summary>
        internal static void Into(Panel.PanelInput input)
        {
            try
            {
                var rec = Plugin.Battle; Wire(rec); rec.Tick(DateTime.UtcNow);
                input.Battle = rec; input.BossLog = Plugin.BossLog; input.FoeBook = Plugin.FoeBook; input.FoesSince = Plugin.FoesSince; input.FoesPending = Plugin.FoesPending(); input.FoesFromUtc = Plugin.FoesFromUtc;
                input.KeptSessions = Plugin.EarlierFights;   // 0.8.5: the fights of earlier sessions (KeptFights), for the feed
                // 0.8.5: your damage after the foe's armor, every window (the log's After serves 10 min .. Session)
                input.DealtArmourSession = Plugin.DealtArmour; input.DealtArmourSince = Plugin.DealtArmourSince; input.DealtArmourBook = Plugin.DealtArmourBook;
                input.DealtArmourPending = Plugin.DealtArmourPending(); input.DealtArmourFromUtc = Plugin.DealtArmourFromUtc; input.SwingFromUtc = Plugin.SwingFromUtc;
            }
            catch (Exception e) { HookGuard.Fail(e, "BattleHooks.Into"); }
            // 0.9: the fight pages' book (FightAdapter): this session's group fights, boss fights and runs from every record, then the saved ones
            // (0.8.5: and the kept group fights of earlier sessions); none at all: null, and Last fight keeps its 0.8 page
            try
            {
                var book = FightAdapter.Build(Plugin.FightSides(), Plugin.BossLog, Plugin.RunLog, DateTime.UtcNow, PlaceName, Plugin.EarlierFights);
                input.Fights = book.Fights.Count > 0 ? book : null;
            }
            catch (Exception e) { input.Fights = null; HookGuard.Fail(e, "BattleHooks.Into fights"); }
        }

        /// <summary>A dungeon's name in the player's language from the game's own label for it ("$location_dvergrboss": "Infested Citadel"); null:
        /// the game has none for it (the page names the land and "dungeon").</summary>
        static string PlaceName(string kind, string token)
        {
            if (string.IsNullOrEmpty(token) || Localization.instance == null) return null;
            var name = Panel.PanelRich.Untagged(Localization.instance.Localize(token));   // 0.8.5: a game name without its color tags
            return string.IsNullOrEmpty(name) || name.StartsWith("$") || name.StartsWith("[") ? null : name;
        }
    }
}
