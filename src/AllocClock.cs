using System;
using System.Reflection;

namespace Hearthwoven
{
    /// <summary>
    /// How many bytes have been allocated, as well as this runtime can say (0.8 performance pass: PerfMeter and Dev.Bench). Tried in
    /// order, once, each checked against a 64 KB test allocation before it is trusted:
    /// 1. GC.GetAllocatedBytesForCurrentThread (exact, this thread only; .NET has it, the game's Mono may have it but count 0);
    /// 2. GC.GetTotalAllocatedBytes (every thread, never goes down);
    /// 3. GC.GetTotalMemory(false): the heap in use, which drops when a collection runs, so a span with a collection in it is not
    ///    counted (Span.Bytes says -1) and small allocations show in steps (the collector hands out memory in blocks).
    /// Looked up by reflection, so the net472 build compiles and an older runtime simply falls back. Never throws.
    /// </summary>
    public static class AllocClock
    {
        static readonly Func<long> read;
        /// <summary>What Now() reads (for the bench file and the perf line).</summary>
        public static readonly string Source;
        /// <summary>true for 1 and 2 (counters that only grow), false for the heap-in-use fallback.</summary>
        public static readonly bool Exact;

        static AllocClock()
        {
            try
            {
                var f = Find("GetAllocatedBytesForCurrentThread", Type.EmptyTypes, null);
                if (Works(f)) { read = f; Source = "GC.GetAllocatedBytesForCurrentThread"; Exact = true; return; }
                f = Find("GetTotalAllocatedBytes", new[] { typeof(bool) }, false);
                if (Works(f)) { read = f; Source = "GC.GetTotalAllocatedBytes"; Exact = true; return; }
            }
            catch { }
            read = () => GC.GetTotalMemory(false);
            Source = "GC.GetTotalMemory (heap in use; spans with a collection left out)";
            Exact = false;
        }

        static Func<long> Find(string name, Type[] args, object arg)
        {
            var m = typeof(GC).GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (m == null || m.ReturnType != typeof(long)) return null;
            if (args.Length == 0) return (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), m);
            var one = (Func<bool, long>)Delegate.CreateDelegate(typeof(Func<bool, long>), m);
            var flag = (bool)arg;
            return () => one(flag);
        }

        static byte[] probe;
        static bool Works(Func<long> f)
        {
            if (f == null) return false;
            try
            {
                var before = f();
                probe = new byte[64 * 1024];
                var after = f();
                probe = null;
                return after - before >= 64 * 1024;
            }
            catch { return false; }
        }

        /// <summary>The counter now (bytes); 0 when it cannot be read.</summary>
        public static long Now() { try { return read(); } catch { return 0; } }

        /// <summary>The garbage collections so far (generation 0 counts every collection, also on the game's Boehm collector).</summary>
        public static int Collections() { try { return GC.CollectionCount(0); } catch { return 0; } }

        /// <summary>A measured stretch: Start, the work, then Bytes.</summary>
        public struct Span
        {
            long at; int gc;
            public static Span Start() => new Span { at = Now(), gc = Collections() };
            /// <summary>Bytes allocated since Start; -1 when it cannot be told (the heap fallback saw a collection, or went down).</summary>
            public long Bytes()
            {
                var d = Now() - at;
                if (Exact) return d < 0 ? 0 : d;
                return Collections() != gc || d < 0 ? -1 : d;
            }
        }
    }
}
