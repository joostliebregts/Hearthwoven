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
    ///
    /// Battle.DealtAfterArmour (off by default, a developer setting; ARMOUR-SCOPE.md section 7): your damage after the foe's resistances and
    /// armour, measured where the game applies them, on the PC that controls the foe. When that PC is yours it is recorded here; when it is a
    /// fellow player's with the setting on too, that PC sends it to you (one small message per hit). Off: none of these hooks is patched
    /// (Prepare), nothing is measured or sent. Read when the game starts.
    /// </summary>
    static class BattleHooks
    {
        static ConfigEntry<bool> dealtAfterArmour;
        /// <summary>Battle.DealtAfterArmour as the game started (the hooks are patched only then).</summary>
        internal static bool DealtAfterArmour => dealtAfterArmour != null && dealtAfterArmour.Value;
        internal const string DealtRpc = "Hearthwoven_DealtArmour";

        internal static void Bind(ConfigFile config)
        {
            dealtAfterArmour = config.Bind("Battle", "DealtAfterArmour", false,
                "Developer tool: measure the damage you deal after the foe's resistances and armor, on the PC that controls the foe (yours, or a fellow player's with this setting on too, which sends it to you). Takes effect at the next game start. Off for players.");
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
            rec.Dealt(DateTime.UtcNow, id, kind, target.m_name, ClientHooks.Biome(), hit.m_damage);
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
                if (a != null) { if (Foe(a)) rec.Received(DateTime.UtcNow, id, source, a.m_name, ClientHooks.Biome(), hit.m_damage); }
                else if (rec.Knows(id)) rec.Received(DateTime.UtcNow, id, null, null, ClientHooks.Biome(), hit.m_damage);   // no longer loaded: the kind as first seen
                return;
            }
            var slot = hit.m_hitType == HitData.HitType.Burning ? 0 : hit.m_hitType == HitData.HitType.Poisoned ? 1 : -1;
            if (slot < 0 || dot[slot].Id.IsNone || Time.time - dot[slot].At >= 30f) return;   // the same 30 s as ClientHooks.Source
            rec.Received(DateTime.UtcNow, dot[slot].Id, dot[slot].Kind, dot[slot].Token, ClientHooks.Biome(), hit.m_damage);
        }

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
                if (rec.FightOn && now >= nextNear) { nextNear = now + BattleRecorder.NearEvery; NearYou(rec, utc); }
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

        // ---------- Battle.DealtAfterArmour: your damage after the foe's resistances and armour, on the foe's PC ----------

        sealed class FoeMark { public HitData Hit; public Character Target; public bool Resisted; public HitData.DamageTypes Incoming, Before; }
        static FoeMark foeHit;   // a player's hit on a creature this PC controls, in flight inside RPC_Damage; null for every other hit
        static bool firstLogged;

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
                    Measured(__instance, hit, m);
                }
                catch (Exception e) { HookGuard.Fail(e, "BattleHooks.FoeAfterArmour.Prefix"); }
            }
        }

        /// <summary>A player's hit on a foe this PC controls, measured: kept here when it is yours, sent to that player's PC when it is a fellow player's.</summary>
        static void Measured(Character target, HitData hit, FoeMark m)
        {
            var attacker = hit.GetAttacker() as Player;
            if (attacker == null) return;
            var after = AfterArmour(m.Before, hit.m_damage, Game.m_worldLevel, Game.instance ? Game.instance.m_worldLevelEnemyBaseAC : 100);
            var kind = Prefab(target); var skill = hit.m_skill.ToString();
            if (attacker == Player.m_localPlayer) RecordDealt(kind, skill, m.Incoming, m.Before, after, "this PC");
            else SendDealt(hit.m_attacker.UserID, kind, skill, m.Incoming, m.Before, after);
        }

        /// <summary>
        /// The hit after the foe's armour. At ApplyDamage the game has already taken fire, spirit and poison out of the hit (they become burning
        /// and poison on the foe), after armour scaled every type by one ratio (HitData.DamageTypes.ApplyArmor). So: no world level, no armour
        /// (after = before); else the ratio the remaining types show, and only for a hit of fire, spirit or poison alone the game's own formula.
        /// </summary>
        internal static HitData.DamageTypes AfterArmour(HitData.DamageTypes before, HitData.DamageTypes now, int worldLevel, int baseAc)
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

        const int MaxDealtKeys = 3000;
        static void RecordDealt(string kind, string skill, HitData.DamageTypes incoming, HitData.DamageTypes before, HitData.DamageTypes after, string where)
        {
            if (Plugin.DealtArmour.Keys > MaxDealtKeys) return;   // bounded: a flood of invented kinds stops here
            if (Plugin.DealtArmour.Add(kind, skill, incoming, before, after)) Plugin.DealtArmourMinutes.Add(DateTime.UtcNow, incoming, before, after);
            if (!firstLogged) { firstLogged = true; Debug.Log("[Hearthwoven] Battle.DealtAfterArmour: first hit measured after the foe's armour (" + kind + ", measured on " + where + ")"); }
        }

        static void SendDealt(long peer, string kind, string skill, HitData.DamageTypes incoming, HitData.DamageTypes before, HitData.DamageTypes after)
        {
            if (peer == 0L || ZRoutedRpc.instance == null || Panel.SampleMode.On) return;
            var pkg = new ZPackage();
            pkg.Write(1); pkg.Write(kind ?? "?"); pkg.Write(skill ?? "?");
            Write(pkg, incoming); Write(pkg, before); Write(pkg, after);
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, DealtRpc, pkg);   // a PC without the handler (setting off, no Hearthwoven) ignores it quietly
        }

        static void Write(ZPackage p, HitData.DamageTypes d) { foreach (var v in ArmourTally.Of(d)) p.Write(v); }
        static HitData.DamageTypes Read(ZPackage p)
        {
            float R() { var v = p.ReadSingle(); return Json.IsFinite(v) && v > 0f && v < 1e5f ? v : 0f; }
            return new HitData.DamageTypes { m_blunt = R(), m_slash = R(), m_pierce = R(), m_fire = R(), m_frost = R(), m_lightning = R(), m_poison = R(), m_spirit = R() };
        }

        [HarmonyPatch(typeof(ZRoutedRpc), MethodType.Constructor, typeof(bool))]
        static class RegisterDealt
        {
            static bool Prepare() => DealtAfterArmour;
            static void Postfix(ZRoutedRpc __instance)
            {
                try { __instance.Register<ZPackage>(DealtRpc, OnDealt); }
                catch (Exception e) { HookGuard.Fail(e, "BattleHooks.RegisterDealt.Postfix"); }
            }
        }

        static void OnDealt(long sender, ZPackage pkg)
        {
            try
            {
                if (pkg == null || pkg.ReadInt() != 1) return;
                var kind = pkg.ReadString(); var skill = pkg.ReadString();
                if (string.IsNullOrEmpty(kind) || kind.Length > 64 || skill == null || skill.Length > 32) return;
                var i = Read(pkg); var b = Read(pkg); var a = Read(pkg);
                RecordDealt(kind, skill, i, b, a, "a fellow player's PC");
            }
            catch (Exception e) { HookGuard.Fail(e, "BattleHooks.OnDealt"); }
        }

        // ---------- the panel ----------

        /// <summary>The battle record into the panel's input (PanelUi.Gather): the recorder ticked to now, the book, the counts and the dates.</summary>
        internal static void Into(Panel.PanelInput input)
        {
            try
            {
                var rec = Plugin.Battle; Wire(rec); rec.Tick(DateTime.UtcNow);
                input.Battle = rec; input.FoeBook = Plugin.FoeBook; input.FoesSince = Plugin.FoesSince; input.FoesPending = Plugin.FoesPending(); input.FoesFromUtc = Plugin.FoesFromUtc;
                if (DealtAfterArmour) { input.DealtArmourSession = Plugin.DealtArmour; input.DealtArmourMinutes = Plugin.DealtArmourMinutes; }
            }
            catch (Exception e) { HookGuard.Fail(e, "BattleHooks.Into"); }
        }
    }
}
