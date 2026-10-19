using System;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// The small hooks the feats need (ACHIEVEMENTS-06 section 3.3-3.4), in their own file: each only reads and only counts the local
    /// player, adds to the FeatsLedger's counters through FeatsCounters (plain C#, tested) and never changes a result. Patched one by
    /// one like every hook: if a game update breaks one, only the feats that need it stop counting.
    /// </summary>
    static class FeatsHooks
    {
        static bool Local(Character c) => c != null && c == Player.m_localPlayer;
        static void Safe(Action a) => HookGuard.Run(a);   // RESILIENCE-06 item 6: one guard for every hook

        /// <summary>A coarse place for the moment of a feat ("aboard" on a ship); null on foot. Never a position.</summary>
        internal static string Place()
        {
            try { var me = Player.m_localPlayer; var ship = Ship.GetLocalShip(); return me != null && ship != null && ship.IsPlayerInBoat(me) ? "aboard" : null; }
            catch { return null; }
        }

        // a fellow player within 15 m of you; players without Hearthwoven count too (positions sync to every PC)
        static bool FellowNear(Player me)
        {
            if (me == null) return false;
            foreach (var p in Player.GetAllPlayers())
                if (p != null && p != me && Vector3.Distance(p.transform.position, me.transform.position) <= FeatsCounters.NearMetres) return true;
            return false;
        }

        class Held { public float Before; public bool Parry; }

        // Shield Wall, Stood Fast, Unbroken, Turned the Forsaken: one look at every block that held (Humanoid.BlockAttack runs on the
        // defender's own client). Prefix: the blockable damage before the block and whether it is a parry by the game's own rule (a shield
        // that can parry, a raise within 0.25 s). Postfix: the block held and the guard was not broken; what the shield stopped is the
        // blockable damage before minus after (before your armour).
        [HarmonyPatch(typeof(Humanoid), "BlockAttack")]
        static class Block
        {
            static readonly GameField<Humanoid, float> timer = new GameField<Humanoid, float>("m_blockTimer");   // looked up on first use, never at type load
            static readonly System.Reflection.MethodInfo getBlocker = AccessTools.Method(typeof(Humanoid), "GetCurrentBlocker");
            static void Prefix(Humanoid __instance, HitData hit, out Held __state)
            {
                __state = null;
                try
                {
                    if (!Local(__instance) || hit == null) return;
                    var blocker = getBlocker?.Invoke(__instance, null) as ItemDrop.ItemData;
                    float t = timer.Of(__instance);
                    __state = new Held { Before = hit.GetTotalBlockableDamage(), Parry = blocker != null && blocker.m_shared.m_timedBlockBonus > 1f && t != -1f && t < 0.25f };
                }
                catch (Exception e) { __state = null; HookGuard.Fail(e, "FeatsHooks.Block.Prefix"); }
            }
            static void Postfix(Humanoid __instance, HitData hit, Character attacker, bool __result, Held __state) => Safe(() =>
            {
                if (__state == null || !__result || __instance.IsStaggering()) return;   // guard broken: not a held block
                FeatsCounters.OnBlock(Plugin.FeatsLedger, __state.Parry, attacker != null && attacker.IsBoss(), FellowNear(Player.m_localPlayer),
                                      Mathf.Max(0f, __state.Before - hit.GetTotalBlockableDamage()));
                BattleHooks.Blocked(hit, attacker, __state.Parry);   // 0.9: on the foe's encounter in the battle record
            });
        }

        // 0.9 Before the Altar: the altar answered your offering. OfferingBowl.RPC_BossSpawnInitiated is sent by the altar's owner to the PC of the
        // player who offered, only once the boss is coming (RPC_SpawnBoss, CanSpawnBoss): read only, it counts and changes nothing.
        [HarmonyPatch(typeof(OfferingBowl), "RPC_BossSpawnInitiated")]
        static class AltarAnswered
        {
            static void Postfix() => Safe(() => FeatsCounters.OnSummoned(Plugin.FeatsLedger));
        }

        /// <summary>0.9 Mender of Others: a repair of yours (ClientHooks.Repaired) on a piece someone else built (Piece.GetCreator: the builder's player id).</summary>
        internal static void Repaired(WearNTear piece) => Safe(() =>
        {
            var me = Player.m_localPlayer; if (piece == null || me == null) return;
            var p = piece.GetComponent<Piece>();
            FeatsCounters.OnRepaired(Plugin.FeatsLedger, p != null ? p.GetCreator() : 0L, me.GetPlayerID());
        });

        /// <summary>0.9 Raid Breaker: your hit on a foe (ClientHooks.DamageDealt) while the game's raid is on around you. A client knows the raid as its
        /// RandEventSystem's random event, made active while you stand in its area (RandEventSystem.FixedUpdate); a boss's forced event is not a raid.</summary>
        internal static void RaidHit(Character target) => Safe(() =>
        {
            if (target == null || target is Player || target.IsTamed()) return;
            var rs = RandEventSystem.instance; if (rs == null) return;
            var ev = rs.GetActiveEvent(); if (ev == null || ev != rs.GetCurrentRandomEvent()) return;
            FeatsCounters.OnRaidHit(Plugin.FeatsLedger, FeatsCounters.RaidKey(ev.m_name, ev.m_pos.x, ev.m_pos.z));
        });

        // 0.9.1 feats, batch 2: one look every 10 s (ClientHooks.SampleVoyage): at the helm in a storm (Through the Storm), the land under your feet (One
        // Long Road), how far from the world's middle (Far Horizon: the distance only, never where), the metres ridden in a saddle (Saddled). Read only.
        static Vector3 rideLast; static bool rideWas;
        internal static void SampleWorld(Player me, Ship ship, float seconds) => Safe(() =>
        {
            var ledger = Plugin.FeatsLedger;
            if (me == null) { FeatsCounters.OnStormSample(ledger, false, false, seconds); rideWas = false; return; }
            var aboard = ship != null && ship.IsPlayerInBoat(me);
            var helm = aboard && ship.m_shipControlls ? ship.m_shipControlls.GetUser() : 0L;
            var env = EnvMan.instance != null ? EnvMan.instance.GetCurrentEnvironment() : null;
            FeatsCounters.OnStormSample(ledger, aboard && helm != 0L && helm == me.GetPlayerID(), FeatsCounters.IsStorm(env != null ? env.m_name : null), seconds);
            FeatsCounters.OnLand(ledger, ClientHooks.Biome());
            var pos = me.transform.position;
            FeatsCounters.OnFar(ledger, pos.x, pos.z);
            var saddle = me.GetDoodadController() as Sadle;
            if (saddle == null || !saddle.IsValid()) { rideWas = false; return; }
            if (rideWas) FeatsCounters.OnRide(ledger, Vector3.Distance(pos, rideLast));
            rideLast = pos; rideWas = true;
        });

        // A hit that lands on you ends the parry run (Unbroken). After resistances and armour, so a block that let nothing through does not;
        // burning and poison carry no attacker and do not.
        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        static class Hurt
        {
            static void Postfix(Character __instance, HitData hit) => Safe(() =>
            {
                if (hit == null || !Local(__instance)) return;
                FeatsCounters.OnHurt(hit.GetAttacker() != null, hit.GetTotalDamage());
            });
        }
    }
}
