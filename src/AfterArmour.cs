namespace Hearthwoven
{
    /// <summary>
    /// 0.8.5: your hit after the foe's resistances and armor, worked out on your PC at the moment you strike (ClientHooks.DamageDealt, through
    /// BattleHooks.AfterOf), with the game's own steps for a creature in Character.RPC_Damage: HitData.ApplyResistance (each type times its
    /// modifier, ×0.25 to ×2, Immune and Ignore 0) with the foe's modifiers (Character.GetDamageModifiers, its weak spot's when the hit struck
    /// one), then, in a world with a world level above 0, HitData.DamageTypes.ApplyArmor(world level × Game.m_worldLevelEnemyBaseAC) on the whole
    /// hit (one ratio for every type). Fire, poison and spirit as the hit sets them (the game turns them into burning and poison on the foe).
    ///
    /// Why here and not where the game applies them (Battle.DealtAfterArmour, BattleHooks): the foe's armor runs on the PC that controls the
    /// foe, often a fellow player's; measuring it there needs Hearthwoven on that PC and a message per hit, and stays blind to foes a PC without
    /// it controls. Worked out here it is there for every hit on every foe, at no cost but a few multiplications, never sent.
    /// Not counted, on either side of the pair: a sneak attack's bonus, a staggered foe's double damage, a foe's own block or shield, and the
    /// group-size and world damage settings (all applied by the controlling PC). Pure C# (no Unity calls), unit-tested.
    /// </summary>
    public static class AfterArmour
    {
        /// <summary>The game's factor for a damage modifier (HitData.ApplyModifier).</summary>
        public static float Factor(HitData.DamageModifier m)
        {
            switch (m)
            {
                case HitData.DamageModifier.SlightlyResistant: return 0.75f;
                case HitData.DamageModifier.Resistant: return 0.5f;
                case HitData.DamageModifier.VeryResistant: return 0.25f;
                case HitData.DamageModifier.SlightlyWeak: return 1.25f;
                case HitData.DamageModifier.Weak: return 1.5f;
                case HitData.DamageModifier.VeryWeak: return 2f;
                case HitData.DamageModifier.Immune: case HitData.DamageModifier.Ignore: return 0f;
                default: return 1f;
            }
        }

        /// <summary><paramref name="before"/> (your hit as you swung it) after a foe with <paramref name="mods"/>, in a world at
        /// <paramref name="worldLevel"/> (0: no foe armor) with the game's armor per level <paramref name="baseAc"/>. Raw damage (m_damage) as it was.</summary>
        public static HitData.DamageTypes Of(HitData.DamageTypes before, HitData.DamageModifiers mods, int worldLevel, int baseAc)
        {
            var a = before;
            a.m_blunt *= Factor(mods.m_blunt); a.m_slash *= Factor(mods.m_slash); a.m_pierce *= Factor(mods.m_pierce);
            a.m_chop *= Factor(mods.m_chop); a.m_pickaxe *= Factor(mods.m_pickaxe);
            a.m_fire *= Factor(mods.m_fire); a.m_frost *= Factor(mods.m_frost); a.m_lightning *= Factor(mods.m_lightning);
            a.m_poison *= Factor(mods.m_poison); a.m_spirit *= Factor(mods.m_spirit); a.m_nonPlayer *= Factor(mods.m_nonPlayer);
            if (worldLevel > 0) a.ApplyArmor(worldLevel * baseAc);   // the game's own method: nothing for an armor of 0 or a hit of nothing
            return a;
        }
    }
}
