using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthwoven
{
    /// <summary>
    /// Server: which fellows' copies a group request needs (OPTIMISE-06 item 1), pure C# so the tests run it without the game.
    /// Each share file is read, gzipped and split once per version (its write time and length) and kept packed; a requester gets
    /// only the copies that changed since it was last served, and every copy again once FullEvery seconds have passed (covers a
    /// fragment lost on the way). The names of all current sharers go out with every answer, as before, so a player who stopped
    /// sharing still disappears. Nothing in the messages changes: an older client just receives fewer copies, and keeps the ones
    /// it has (GroupShare.Group). Bounded: one packed copy per share file, one served list per connected requester.
    /// </summary>
    public class GroupServe
    {
        public const float FullEvery = 300f;

        /// <summary>A share file as it is on disk now: path, the player key (file name), and its version stamp.</summary>
        public struct Source { public string Path, Key, Stamp; }

        /// <summary>One packed copy: the name inside it, its fragments and the message id they go out under.</summary>
        public class Copy { public string Path, Key, Name, Stamp, MsgId, Fellow; public List<byte[]> Parts; }

        /// <summary>Names, file keys and fellow keys (FellowIds, "" without a fellow function) line up: one entry per current sharer.</summary>
        public class Answer { public readonly List<string> Names = new List<string>(), Keys = new List<string>(), Fellows = new List<string>(); public readonly List<Copy> Send = new List<Copy>(); }

        readonly Dictionary<string, Copy> cache = new Dictionary<string, Copy>();
        readonly Dictionary<long, Dictionary<string, string>> served = new Dictionary<long, Dictionary<string, string>>();
        readonly Dictionary<long, float> fullAt = new Dictionary<long, float>();
        /// <summary>How many copies were read and packed (for the tests and the measurement): once per new version.</summary>
        public int Packed { get; private set; }

        /// <summary>
        /// The answer to one request from <paramref name="peer"/> at <paramref name="now"/> (seconds): <paramref name="sources"/> are the
        /// current sharers except the requester. <paramref name="read"/> reads a file's text, <paramref name="pack"/> turns text into
        /// fragments, <paramref name="name"/> finds the name in a copy, <paramref name="msgId"/> names a new packed copy.
        /// </summary>
        public Answer Plan(long peer, IList<Source> sources, float now, Func<string, string> read, Func<string, List<byte[]>> pack,
                           Func<string, string> name, Func<string, string> msgId, Func<string, string> fellow = null)
        {
            var answer = new Answer();
            if (!served.TryGetValue(peer, out var had) || !fullAt.TryGetValue(peer, out var at) || now - at >= FullEvery || now < at)
            {
                served[peer] = had = new Dictionary<string, string>();   // first answer, or the full resend
                fullAt[peer] = now;
            }
            foreach (var s in sources)
            {
                if (!cache.TryGetValue(s.Path, out var copy) || copy.Stamp != s.Stamp)
                {
                    var text = read(s.Path);
                    if (text == null) continue;   // gone between the listing and the read
                    cache[s.Path] = copy = new Copy { Path = s.Path, Key = s.Key, Stamp = s.Stamp, Name = name(text), Parts = pack(text), MsgId = msgId(text), Fellow = fellow?.Invoke(text) ?? "" };
                    Packed++;
                }
                answer.Names.Add(copy.Name); answer.Keys.Add(copy.Key); answer.Fellows.Add(copy.Fellow ?? "");
                if (had.TryGetValue(s.Path, out var stamp) && stamp == copy.Stamp) continue;   // this requester has this version already
                answer.Send.Add(copy);
                had[s.Path] = copy.Stamp;
            }
            // what is no longer shared is forgotten, so the cache never holds more than the share files there are
            if (cache.Count > sources.Count)
            {
                var live = new HashSet<string>(sources.Select(s => s.Path));
                foreach (var gone in cache.Keys.Where(p => !live.Contains(p)).ToList()) if (!IsAnyoneElses(gone, peer)) cache.Remove(gone);
            }
            return answer;
        }

        // the requester's own file is left out of its sources, but is another requester's fellow: keep its packed copy
        bool IsAnyoneElses(string path, long peer) => served.Any(kv => kv.Key != peer && kv.Value.ContainsKey(path));

        /// <summary>This requester's next answer sends every copy again (it switched sharing off and its copies were cleared).</summary>
        public void Forget(long peer) { served.Remove(peer); fullAt.Remove(peer); }

        /// <summary>Drops the served lists of requesters that are no longer connected.</summary>
        public void Prune(Func<long, bool> connected)
        {
            foreach (var peer in served.Keys.Where(p => !connected(p)).ToList()) Forget(peer);
        }

        public int Requesters => served.Count;
        public int Cached => cache.Count;
    }
}
