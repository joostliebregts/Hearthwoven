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

        /// <summary>
        /// Server side (and the client for group replies): one buffer per (sender, message id). Returns the whole payload when the
        /// last part arrives. Bounded (RESILIENCE-06 A1): a part over Size bytes or a message over MaxParts parts is refused; a
        /// message still incomplete MaxAgeSeconds after its first part is dropped (a client that replaced an unsent snapshot,
        /// or lost its link mid-send, never completes it); at most MaxOpenPerSender incomplete messages per sender (the oldest goes).
        /// </summary>
        public class Assembler
        {
            public const int MaxParts = 200, MaxOpenPerSender = 4;
            public const double MaxAgeSeconds = 120;
            class Open { public byte[][] Parts; public long Sender; public double First; }
            readonly Dictionary<string, Open> pending = new Dictionary<string, Open>();
            static readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            /// <summary>Seconds, for the expiry; the tests set their own clock.</summary>
            public System.Func<double> Clock = () => watch.Elapsed.TotalSeconds;

            public byte[] Add(long sender, string messageId, int index, int count, byte[] part)
            {
                if (count <= 0 || count > MaxParts || index < 0 || index >= count || part == null || part.Length > Size) { Rejected++; return null; }
                var now = Clock();
                Expire(now);
                var key = sender + "/" + messageId;
                if (!pending.TryGetValue(key, out var open) || open.Parts.Length != count)
                {
                    if (open == null)
                    {
                        var mine = pending.Where(kv => kv.Value.Sender == sender).OrderBy(kv => kv.Value.First).ToList();
                        for (int i = 0; i <= mine.Count - MaxOpenPerSender; i++) { pending.Remove(mine[i].Key); Dropped++; }
                    }
                    pending[key] = open = new Open { Parts = new byte[count][], Sender = sender, First = now };
                }
                open.Parts[index] = part;
                if (open.Parts.Any(p => p == null)) return null;
                pending.Remove(key);
                return open.Parts.SelectMany(p => p).ToArray();
            }

            void Expire(double now)
            {
                if (pending.Count == 0) return;
                foreach (var gone in pending.Where(kv => now - kv.Value.First > MaxAgeSeconds || now < kv.Value.First).Select(kv => kv.Key).ToList()) { pending.Remove(gone); Expired++; }
            }

            public int Pending => pending.Count;
            public int Rejected;   // parts out of range or too large; the server logs it (never silent)
            public int Expired;    // incomplete messages dropped after MaxAgeSeconds
            public int Dropped;    // incomplete messages dropped because the sender opened more than MaxOpenPerSender
        }
    }
}
