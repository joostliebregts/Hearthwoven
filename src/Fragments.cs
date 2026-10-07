using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// Splits a packed snapshot into fragments under 4 KiB and reassembles them on the server. Big single messages
    /// stalled multiplayer for seconds in another mod (ValheimSagas, see README credits).
    /// </summary>
    public static class Fragments
    {
        public const int Size = 3000;

        public static List<byte[]> Split(byte[] data)
        {
            var parts = new List<byte[]>();
            for (int i = 0; i < data.Length; i += Size) parts.Add(data.Skip(i).Take(System.Math.Min(Size, data.Length - i)).ToArray());
            if (parts.Count == 0) parts.Add(new byte[0]);
            return parts;
        }

        /// <summary>Server side: one buffer per (sender, message id). Returns the whole payload when the last part arrives.</summary>
        public class Assembler
        {
            readonly Dictionary<string, byte[][]> pending = new Dictionary<string, byte[][]>();
            public byte[] Add(long sender, string messageId, int index, int count, byte[] part)
            {
                if (count <= 0 || count > 200 || index < 0 || index >= count) { Rejected++; return null; }
                var key = sender + "/" + messageId;
                if (!pending.TryGetValue(key, out var parts) || parts.Length != count) pending[key] = parts = new byte[count][];
                parts[index] = part;
                if (parts.Any(p => p == null)) return null;
                pending.Remove(key);
                return parts.SelectMany(p => p).ToArray();
            }
            public int Pending => pending.Count;
            public int Rejected;   // parts of a message too large to accept; the server logs it (never silent)
        }
    }
}
