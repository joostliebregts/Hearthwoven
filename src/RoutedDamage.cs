namespace Hearthwoven
{
    /// <summary>Reads a routed RPC package without consuming it, and pulls out the HitData of an RPC_Damage.</summary>
    public static class RoutedDamage
    {
        public static readonly int DamageHash = StringExtensionMethods.GetStableHashCode("RPC_Damage");

        /// <summary>Returns null when the package is not an RPC_Damage. Never modifies <paramref name="pkg"/>.</summary>
        public static HitData TryRead(ZPackage pkg, out ZRoutedRpc.RoutedRPCData data)
        {
            data = null;
            var copy = new ZPackage(pkg.GetArray());
            var d = new ZRoutedRpc.RoutedRPCData();
            d.Deserialize(copy);
            data = d;   // every routed message, so other watchers (shared chests) can read it too
            if (d.m_methodHash != DamageHash) return null;
            d.m_parameters.SetPos(0);
            var hit = new HitData();
            var p = d.m_parameters;
            hit.Deserialize(ref p);
            return hit;
        }

        public static void FillDamage(DamageRecord r, HitData.DamageTypes dt)
        {
            void Add(string k, float v) { if (v != 0f) r.Damage[k] = v; }
            Add("damage", dt.m_damage); Add("blunt", dt.m_blunt); Add("slash", dt.m_slash); Add("pierce", dt.m_pierce);
            Add("chop", dt.m_chop); Add("pickaxe", dt.m_pickaxe); Add("fire", dt.m_fire); Add("frost", dt.m_frost);
            Add("lightning", dt.m_lightning); Add("poison", dt.m_poison); Add("spirit", dt.m_spirit);
        }
    }
}
