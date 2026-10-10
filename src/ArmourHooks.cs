using System;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// How much your armour helped (0.7, work/hearthwoven-0.7/ARMOUR-SCOPE.md section 2): a hit on your own character measured at the game's
    /// own steps in Character.RPC_Damage, which runs on your PC because your PC owns your character (every world, every group). No formula is
    /// re-run, so a mod that changes armour is captured as it acts.
    ///   A. prefix on Character.RPC_Damage marks the local player's hit (a finalizer puts the previous mark back, so a hit that throws or
    ///      returns early never leaves one behind);
    ///   B. prefix and postfix on HitData.ApplyResistance, only for the marked hit: before and after your resistances (= before armour; a
    ///      shield's own modifiers in BlockAttack call it first on the same hit, the main call comes later and wins);
    ///   C. prefix on Player.DamageArmorDurability, which the game calls right after ApplyArmor: after armour.
    /// HitData.ApplyArmor itself is a one-line forwarder Mono may inline (a patch that never fires); B and C are not inlining candidates.
    /// Only reads; records into Plugin.Armour and Plugin.ArmourMinutes, never into the damage tally, the event log or what is shared.
    /// </summary>
    static class ArmourHooks
    {
        sealed class Mark { public HitData Hit; public bool Resisted; public HitData.DamageTypes Incoming, Before; }
        static Mark pending;   // the local player's hit in flight inside RPC_Damage; null for every other hit

        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        static class MarkHit
        {
            static void Prefix(Character __instance, HitData hit, out object __state)
            {
                __state = null;
                try { __state = pending; pending =hit != null && __instance != null && __instance == Player.m_localPlayer ? new Mark { Hit = hit } : null; }
                catch (Exception e) { HookGuard.Fail(e, "ArmourHooks.MarkHit.Prefix"); }
            }
            static void Finalizer(object __state) { try { pending = __state as Mark; } catch (Exception e) { HookGuard.Fail(e, "ArmourHooks.MarkHit.Finalizer"); } }
        }

        // runs for every hit on anything this PC owns (and our own TreeFalls probe on a clone): the reference check first, no closure
        [HarmonyPatch(typeof(HitData), nameof(HitData.ApplyResistance))]
        static class Resist
        {
            static void Prefix(HitData __instance)
            {
                try { var p = pending; if (p == null || !ReferenceEquals(__instance, p.Hit)) return; p.Incoming = __instance.m_damage; }
                catch (Exception e) { HookGuard.Fail(e, "ArmourHooks.Resist.Prefix"); }
            }
            static void Postfix(HitData __instance)
            {
                try { var p = pending; if (p == null || !ReferenceEquals(__instance, p.Hit)) return; p.Before = __instance.m_damage; p.Resisted = true; }
                catch (Exception e) { HookGuard.Fail(e, "ArmourHooks.Resist.Postfix"); }
            }
        }

        [HarmonyPatch(typeof(Player), "DamageArmorDurability")]
        static class AfterArmour
        {
            static void Prefix(Player __instance, HitData hit)
            {
                try
                {
                    var p = pending;
                    if (p == null || !p.Resisted || !ReferenceEquals(hit, p.Hit) || __instance == null || __instance != Player.m_localPlayer) return;
                    p.Resisted = false;   // one record per hit
                    var source = ClientHooks.Source(hit); var cause = hit.m_hitType.ToString();
                    var kept = Plugin.Armour.Add(source, cause, p.Incoming, p.Before, hit.m_damage);
                    if (kept) Plugin.ArmourMinutes.Add(DateTime.UtcNow, p.Incoming, p.Before, hit.m_damage);
                    if (DevCheck.On) ArmourCheck.Record(source, cause, p.Incoming, p.Before, hit.m_damage, __instance.GetBodyArmor(), __instance.IsBlocking(), kept);   // Dev.SelfCheck
                }
                catch (Exception e) { HookGuard.Fail(e, "ArmourHooks.AfterArmour.Prefix"); }
            }
        }

        // The methods whose replacement by another mod would leave the armour view empty or wrong. Logged once per game run, at the first spawn
        // (every mod has patched by then): only the other mods' Harmony ids, so a player's log says at once whether one changes this step.
        static bool ownersLogged;

        /// <summary>The other mods that patch each watched method, "Character.RPC_Damage: other.mod.id"; "none" when no other mod does.</summary>
        internal static string Owners()
        {
            var parts = new System.Collections.Generic.List<string>();
            var watched = new[] { (typeof(Character), "RPC_Damage"), (typeof(HitData), "ApplyResistance"), (typeof(HitData), "ApplyArmor"), (typeof(Player), "GetBodyArmor"), (typeof(Player), "DamageArmorDurability") };
            foreach (var (type, name) in watched)
            {
                foreach (var m in AccessTools.GetDeclaredMethods(type))
                {
                    if (m.Name != name) continue;
                    var info = Harmony.GetPatchInfo(m);
                    if (info == null) continue;
                    var others = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(System.Linq.Enumerable.Distinct(info.Owners), o => o != Plugin.Guid));
                    if (others.Length > 0) parts.Add(type.Name + "." + name + ": " + string.Join(", ", others));
                }
            }
            return parts.Count == 0 ? "none" : string.Join("; ", parts.ToArray());
        }

        [HarmonyPatch(typeof(Player), "OnSpawned")]
        static class LogOwners
        {
            static void Postfix(Player __instance)
            {
                try
                {
                    if (ownersLogged || __instance == null || __instance != Player.m_localPlayer) return;
                    ownersLogged = true;
                    var line = Owners();
                    Debug.Log("[Hearthwoven] armour: other mods that patch the damage and armour steps: " + line);
                    if (DevCheck.On) ArmourCheck.Owners(line, Game.m_localDamgeTakenRate);
                }
                catch (Exception e) { HookGuard.Fail(e, "ArmourHooks.LogOwners.Postfix"); }
            }
        }
    }

    /// <summary>
    /// The in-game check of the armour view (Dev.SelfCheck only; every call returns at once with it off): one "HW-CHECK ... armour:" line per
    /// recorded hit (the first 60 of a session), with what to compare in the game. ARMOUR-SCOPE.md "In-game check": the red number above you
    /// equals "after armour" for a blow without fire, spirit or poison; no armour worn gives before = after; a block lowers "before armour"; a
    /// ward leaves nothing to record; the patch owners and the world's damage-taken rate are logged once at the first spawn.
    /// </summary>
    static class ArmourCheck
    {
        const int MaxLines = 60;
        static int lines;
        static string F(float v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        static float Sum(HitData.DamageTypes t) { float s = 0; foreach (var v in ArmourTally.Of(t)) if (v > 0) s += v; return s; }

        internal static void Record(string source, string cause, HitData.DamageTypes incoming, HitData.DamageTypes before, HitData.DamageTypes after, float armour, bool blocking, bool kept)
        {
            try
            {
                var b = Sum(before); var a = Sum(after);
                if (!kept) { DevCheck.Book.Say("INFO", "armour", source + ": nothing left before your armour (a ward or a parry took it all): nothing recorded, as intended"); return; }
                var formula = armour > 0 ? HitData.DamageTypes.ApplyArmor(b, armour) : b;
                var red = a - Math.Max(0, after.m_fire) - Math.Max(0, after.m_spirit) - Math.Max(0, after.m_poison);
                if (Math.Abs(formula - a) > 0.5f) DevCheck.Book.Once("WARN", "armour", "after armour " + F(a) + " differs from the game's formula " + F(formula) + " for armour " + F(armour) + ": another mod changes the armour step (measured values are kept as they are)");
                else if (armour <= 0 && Math.Abs(b - a) < 0.05f) DevCheck.Book.Once("PASS", "armour", "no armour worn: before = after (" + F(a) + "), as the game's rule says", "noarmour");
                else DevCheck.Book.Once("PASS", "armour", "a hit on your armour recorded: after armour matches the game's formula (" + F(a) + " for armour " + F(armour) + ")");
                if (lines >= MaxLines) return;
                lines++;
                DevCheck.Book.Say("INFO", "armour", source + " " + cause + (blocking ? " (shield up)" : "") + ": before resistances " + F(Sum(incoming)) + ", before armour " + F(b) + ", after armour " + F(a) +
                    " (armour " + F(armour) + "; the game's formula gives " + F(formula) + "); the red number above you should read " + F(red) + (red < a ? " (fire, spirit and poison come as ticks)" : "") +
                    "; this session: " + Plugin.Armour.Hits + " hits, " + F((float)ArmourTally.Total(Plugin.Armour.Before)) + " before armour, " + F((float)ArmourTally.Total(Plugin.Armour.After)) + " after");
            }
            catch (Exception e) { HookGuard.Fail(e, "ArmourCheck.Record"); }
        }

        internal static void Owners(string line, float damageTakenRate)
        {
            try
            {
                lines = 0;
                DevCheck.Book.Say(line == "none" ? "PASS" : "WARN", "armour-mods", "other mods that patch RPC_Damage, ApplyResistance, ApplyArmor, GetBodyArmor or DamageArmorDurability: " + line);
                DevCheck.Book.Say(Math.Abs(damageTakenRate - 1f) < 0.001f ? "INFO" : "WARN", "armour-mods", "the world's damage-taken rate is " + damageTakenRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
                    (Math.Abs(damageTakenRate - 1f) < 0.001f ? ": Received from and the armour view's after armour agree for physical hits" : ": Received from differs from after armour by that factor for physical hits"));
            }
            catch (Exception e) { HookGuard.Fail(e, "ArmourCheck.Owners"); }
        }
    }
}
