using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Hearthwoven
{
    /// <summary>
    /// The server's daily logs (chests-, births-, damage-routed- and players/received-&lt;yyyyMMdd&gt;.jsonl). Joost (2026-10-09):
    /// logs are never deleted; old days may be compressed in place. Compress turns a day older than N days into
    /// &lt;name&gt;.jsonl.gz: written to a temp file, flushed to disk, read back and compared byte for byte with the original, and only
    /// then is the original removed. Anything unexpected leaves the original as it is. Readers go through Files and ReadLines,
    /// which read a day from .jsonl or .jsonl.gz alike (the plain file wins when both exist). Pure C# (no Unity calls).
    /// </summary>
    public static class DailyLogs
    {
        public const int MinDays = 2;   // today and yesterday are never touched (a writer may still hold yesterday's file)
        /// <summary>Held while compressing and while a reader replays the logs, so a day is never removed mid-read.</summary>
        public static readonly object Gate = new object();
        static readonly Regex Daily = new Regex(@"^(chests|births|damage-routed|received)-(\d{8})\.jsonl(\.gz)?$", RegexOptions.Compiled);
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>The day stamp in a daily log's name (invariant, so a server locale never changes file names).</summary>
        public static string Day(DateTime utc) => utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        /// <summary>The files of one kind ("chests") in <paramref name="dir"/>, oldest day first, one per day (.jsonl before .jsonl.gz).</summary>
        public static List<string> Files(string dir, string kind)
        {
            if (!Directory.Exists(dir)) return new List<string>();
            var byDay = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var f in Directory.GetFiles(dir, kind + "-*.jsonl*"))
            {
                var m = Daily.Match(Path.GetFileName(f));
                if (!m.Success || m.Groups[1].Value != kind) continue;
                var day = m.Groups[2].Value;
                if (!byDay.TryGetValue(day, out var had) || had.EndsWith(".gz", StringComparison.Ordinal)) byDay[day] = f;
            }
            return byDay.Values.ToList();
        }

        /// <summary>The lines of a daily log, plain or gzipped.</summary>
        public static IEnumerable<string> ReadLines(string path)
        {
            if (!path.EndsWith(".gz", StringComparison.Ordinal)) { foreach (var l in File.ReadLines(path, Encoding.UTF8)) yield return l; yield break; }
            using (var fs = File.OpenRead(path))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            using (var r = new StreamReader(gz, Encoding.UTF8))
            {
                string line;
                while ((line = r.ReadLine()) != null) yield return line;
            }
        }

        public class Report { public int Compressed, Kept, Failed; public long BytesBefore, BytesAfter; public readonly List<string> Problems = new List<string>(); }

        /// <summary>
        /// Compresses every daily log in <paramref name="dirs"/> whose day is at least <paramref name="afterDays"/> (at least MinDays)
        /// before <paramref name="todayUtc"/>. 0 or less = off. Never removes a file unless its .gz reads back identical.
        /// </summary>
        public static Report Compress(IEnumerable<string> dirs, DateTime todayUtc, int afterDays)
        {
            var rep = new Report();
            if (afterDays <= 0) return rep;
            var cutoff = Day(todayUtc.Date.AddDays(-Math.Max(MinDays, afterDays)));
            lock (Gate)
                foreach (var dir in dirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.GetFiles(dir, "*.jsonl").OrderBy(x => x, StringComparer.Ordinal))
                    {
                        var m = Daily.Match(Path.GetFileName(f));
                        if (!m.Success || m.Groups[3].Success || string.CompareOrdinal(m.Groups[2].Value, cutoff) > 0) continue;
                        try { One(f, rep); }
                        catch (Exception e) { rep.Failed++; rep.Problems.Add(Path.GetFileName(f) + ": " + e.Message); TryDelete(f + ".gz.tmp"); }
                    }
                }
            return rep;
        }

        static void One(string f, Report rep)
        {
            string gz = f + ".gz", tmp = gz + ".tmp";
            long before = new FileInfo(f).Length;
            if (File.Exists(gz))
            {
                // an earlier run stopped between writing the .gz and removing the original: remove it only when they agree
                if (SameContent(f, gz)) { File.Delete(f); rep.Compressed++; rep.BytesBefore += before; rep.BytesAfter += new FileInfo(gz).Length; }
                else { rep.Kept++; rep.Problems.Add(Path.GetFileName(f) + ": a different " + Path.GetFileName(gz) + " exists; both kept"); }
                return;
            }
            var stamp = File.GetLastWriteTimeUtc(f);
            using (var src = File.OpenRead(f))
            using (var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var z = new GZipStream(dst, CompressionLevel.Optimal, true)) src.CopyTo(z);
                dst.Flush(true);
            }
            if (!SameContent(f, tmp)) { TryDelete(tmp); rep.Failed++; rep.Problems.Add(Path.GetFileName(f) + ": the compressed copy did not read back the same; original kept"); return; }
            File.Move(tmp, gz);
            File.SetLastWriteTimeUtc(gz, stamp);
            File.Delete(f);
            rep.Compressed++; rep.BytesBefore += before; rep.BytesAfter += new FileInfo(gz).Length;
        }

        /// <summary>True when the gzip file <paramref name="gzPath"/> unpacks to exactly the bytes of <paramref name="plain"/>.</summary>
        static bool SameContent(string plain, string gzPath)
        {
            try
            {
                using (var a = File.OpenRead(plain))
                using (var g = File.OpenRead(gzPath))
                using (var b = new GZipStream(g, CompressionMode.Decompress))
                {
                    var x = new byte[65536]; var y = new byte[65536];
                    while (true)
                    {
                        int n = Fill(a, x), m = Fill(b, y);
                        if (n != m) return false;
                        if (n == 0) return true;
                        for (int i = 0; i < n; i++) if (x[i] != y[i]) return false;
                    }
                }
            }
            catch { return false; }
        }

        static int Fill(Stream s, byte[] buf)
        {
            int total = 0, n;
            while (total < buf.Length && (n = s.Read(buf, total, buf.Length - total)) > 0) total += n;
            return total;
        }

        static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
    }
}
