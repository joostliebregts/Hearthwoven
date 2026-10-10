using System.Collections.Generic;
using System.Linq;
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
    ///
    /// One profile id, two people (0.8, RESILIENCE item 2): the profile id is the character's, not the account's. A character file
    /// copied to a friend's PC, or played from a Steam and an Xbox account, brings the same id from two platform accounts. The files
    /// of an id belong to the account that wrote them first (its platform id is in players/&lt;id&gt;.json, or in the host's own
    /// &lt;id&gt;.share.json); another account with that id is keyed "&lt;id&gt;~&lt;platform id&gt;" (KeyFor), so two people never share
    /// one players file, shared copy or book entry. Platform ids are compared as FellowIds.PlatformOf gives them, so the same Steam
    /// account on a Steam server (a bare number) and on a crossplay server ("Steam_…") is one account. Unknown on either side: the id.
    /// </summary>
    public static class PeerIdentity
    {
        /// <summary>No BOM: Python's json.load fails on a BOM unless it reads with utf-8-sig.</summary>
        public static readonly Encoding Utf8 = new UTF8Encoding(false);

        static readonly Dictionary<long, long> boundProfileId = new Dictionary<long, long>();   // peer uid -> snapshot playerId
        static readonly HashSet<long> warned = new HashSet<long>();
        static readonly Dictionary<long, KeyValuePair<long, string>> keyed = new Dictionary<long, KeyValuePair<long, string>>();   // peer uid -> (id, file key)
        static readonly HashSet<long> clashWarned = new HashSet<long>();

        /// <summary>The file key of profile id <paramref name="id"/> sent from <paramref name="platform"/> when the id's files belong to
        /// <paramref name="ownerPlatform"/> (null or "": nobody yet): the id itself, or "&lt;id&gt;~&lt;platform&gt;" for a second account.</summary>
        public static string KeyFor(long id, string platform, string ownerPlatform)
        {
            var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string mine = FellowIds.PlatformOf(platform), owner = FellowIds.PlatformOf(ownerPlatform);
            if (mine.Length == 0 || owner.Length == 0 || mine == owner) return key;
            var safe = new string(mine.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').Take(60).ToArray());
            return safe.Length > 0 ? key + "~" + safe : key;
        }

        /// <summary>The platform id the files of profile id <paramref name="id"/> under <paramref name="playersDir"/> belong to: the envelope's
        /// "platform" of players/&lt;id&gt;.json (read from its first 4 KB), else the "platformId" of &lt;id&gt;.share.json; "" when neither says.</summary>
        public static string OwnerOf(string playersDir, long id)
        {
            var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var owner = Head(System.IO.Path.Combine(playersDir, key + ".json"), "platform");
            return owner.Length > 0 ? owner : Head(System.IO.Path.Combine(playersDir, key + ".share.json"), FellowIds.PlatformField);
        }

        static string Head(string file, string field)
        {
            try
            {
                if (!System.IO.File.Exists(file)) return "";
                using (var r = new System.IO.StreamReader(file, Utf8))
                {
                    var buf = new char[4096]; int n = r.Read(buf, 0, buf.Length);
                    return Transport.Field(new string(buf, 0, n), field);
                }
            }
            catch { return ""; }
        }

        /// <summary>Where the server keeps players' files (BepInEx/Hearthwoven/players); tests point it elsewhere.</summary>
        internal static string PlayersDir = null;

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
            if (!id.HasValue) return KeyOf(id, platform, peer.m_uid);
            if (keyed.TryGetValue(peer.m_uid, out var k) && k.Key == id.Value) return k.Value;   // once per connection: the owner is read from disk once
            string key;
            try
            {
                var dir = PlayersDir ?? System.IO.Path.Combine(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "Hearthwoven"), "players");
                key = KeyFor(id.Value, platform, OwnerOf(dir, id.Value));
            }
            catch { key = KeyOf(id, platform, peer.m_uid); }
            if (keyed.Count > 512) { keyed.Clear(); clashWarned.Clear(); }   // bounded: a long-running server forgets old connections (read again when needed)
            keyed[peer.m_uid] = new KeyValuePair<long, string>(id.Value, key);
            if (key.IndexOf('~') > 0 && clashWarned.Add(peer.m_uid))
                Debug.LogWarning("[Hearthwoven] player " + peer.m_playerName + " (" + platform + ") has the character id of another account's files; kept apart as " + key);
            return key;
        }
    }
}
