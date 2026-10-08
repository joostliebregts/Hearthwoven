using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// Server: which player a connected peer is, for file names (players/&lt;key&gt;.json, &lt;key&gt;.share.json) and the chest log.
    ///
    /// ZNetPeer.m_playerID is 0 on a dedicated server: the server registers the "PlayerID" RPC (ZNet.cs:1135) and its
    /// handler would set it (ZNet.cs:2152-2158), but nothing in the game ever invokes "PlayerID", so it stays at its
    /// default (ZNetPeer.cs:23). Found live on 2026-10-08: Joost's snapshot was stored as players/0.json.
    ///
    /// What the server does know: the peer's character ZDO (RPC_CharacterID, ZNet.cs:2131-2141), and on that ZDO the
    /// "playerID" field the client writes on spawn from its profile (Player.SetPlayerID, Player.cs:743-750, called from
    /// PlayerProfile.cs:201). That is the same id as the snapshot's playerId. Order used:
    ///   1. peer.m_playerID, if a future game version starts sending it;
    ///   2. the playerID on the peer's character ZDO;
    ///   3. the playerId in the last snapshot this peer sent (bound to the sending peer, Bind);
    ///   4. the platform id ("<platform>_<id>"), made file-safe, with one warning; never "0".
    /// </summary>
    public static class PeerIdentity
    {
        /// <summary>No BOM: Python's json.load fails on a BOM unless it reads with utf-8-sig.</summary>
        public static readonly Encoding Utf8 = new UTF8Encoding(false);

        static readonly Dictionary<long, long> boundProfileId = new Dictionary<long, long>();   // peer uid -> snapshot playerId
        static readonly HashSet<long> warned = new HashSet<long>();

        /// <summary>The first id that is not 0, in the order above; null when there is none.</summary>
        public static long? IdOf(long peerPlayerId, long zdoPlayerId, long snapshotPlayerId) =>
            peerPlayerId != 0 ? peerPlayerId : zdoPlayerId != 0 ? zdoPlayerId : snapshotPlayerId != 0 ? snapshotPlayerId : (long?)null;

        /// <summary>The file key: the player id, else the platform id made file-safe, else "peer-&lt;uid&gt;". Never "0" or "".</summary>
        public static string KeyOf(long? id, string platform, long peerUid)
        {
            if (id.HasValue) return id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var safe = Transport.SafeName(platform);
            return safe.Length > 0 ? safe : "peer-" + Transport.SafeName(peerUid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>The top-level "playerId" number of a snapshot written by Snapshot.Build (0 if absent).</summary>
        public static long SnapshotPlayerId(string json)
        {
            const string key = "\"playerId\":";
            var i = json?.IndexOf(key, System.StringComparison.Ordinal) ?? -1;
            if (i < 0) return 0;
            i += key.Length; var j = i;
            if (j < json.Length && json[j] == '-') j++;
            while (j < json.Length && char.IsDigit(json[j])) j++;
            return long.TryParse(json.Substring(i, j - i), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        /// <summary>A snapshot arrived from this peer: remember the id it says it is (used when the character ZDO has none yet).</summary>
        internal static void Bind(long peerUid, long snapshotPlayerId) { if (snapshotPlayerId != 0) boundProfileId[peerUid] = snapshotPlayerId; }

        internal static long? Id(ZNetPeer peer)
        {
            if (peer == null) return null;
            long zdoId = 0;
            try
            {
                if (!peer.m_characterID.IsNone()) zdoId = ZDOMan.instance?.GetZDO(peer.m_characterID)?.GetLong(ZDOVars.s_playerID, 0L) ?? 0L;
            }
            catch { zdoId = 0; }
            boundProfileId.TryGetValue(peer.m_uid, out var bound);
            return IdOf(peer.m_playerID, zdoId, bound);
        }

        internal static string Key(ZNetPeer peer)
        {
            if (peer == null) return null;
            var id = Id(peer);
            string platform = null;
            try { platform = peer.m_socket?.GetHostName(); } catch { }
            if (!id.HasValue && warned.Add(peer.m_uid))
                Debug.LogWarning("[Hearthwoven] no player id for peer " + peer.m_playerName + " (" + platform + "); files keyed by the platform id until one arrives");
            return KeyOf(id, platform, peer.m_uid);
        }
    }
}
