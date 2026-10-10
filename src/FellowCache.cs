using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Hearthwoven
{
    /// <summary>
    /// The fellows' last copies on this PC (0.7, FEEDBACK B23: "I should have his files"): after logging in the book shows each fellow at once,
    /// as they last shared it, until the server's fresh copy replaces it (~10 s after spawn). Only while you share (switching sharing off
    /// deletes them all, GroupShare.ForgetCache); a fellow the server no longer lists (they stopped sharing) is deleted when its list arrives.
    ///
    /// Where: BepInEx/Hearthwoven/local/fellows/&lt;world&gt;/&lt;fellow&gt;.json, never outside the mod's folder. &lt;world&gt; = the world's name
    /// and id as the server gave them (ScopeKey), so two servers never mix; &lt;fellow&gt; = the fellow key the server and GroupShare use
    /// (FellowIds.KeyOf), made safe for a file name (FileName). Written whole (AtomicFile) when a full copy arrives, never per live update;
    /// at most MaxFellows per world, none older than MaxAgeDays (the server's own limit). The file: one header line
    /// {"version":1,"key":"..","received":"ISO"}, then the shared copy as the server sent it (no death positions, no worlds).
    /// Pure C# with files: tested without the game (FellowTests).
    /// </summary>
    public static class FellowCache
    {
        public const int Version = 1, MaxFellows = 40, MaxAgeDays = 14;

        public class Entry { public string Key, Json; public DateTime ReceivedUtc; }

        /// <summary>The folder of all cached fellows under the mod's folder (BepInEx/Hearthwoven).</summary>
        public static string Root(string hearthwovenDir) => Path.Combine(Path.Combine(hearthwovenDir, "local"), "fellows");

        /// <summary>One world's folder name: its name (letters, digits, '-' and '_' only, at most 40) and its id, e.g. "Midgard-123456789".</summary>
        public static string ScopeKey(string worldName, long worldUid) => Safe(worldName, 40) + "-" + worldUid.ToString(CultureInfo.InvariantCulture);

        /// <summary>A fellow key as a file name: its safe characters plus a short hash of the whole key (two keys never share a file).</summary>
        public static string FileName(string key) => Safe(key, 60) + "-" + Hash(key ?? "") + ".json";

        static string Safe(string s, int max)
        {
            var sb = new StringBuilder();
            foreach (var c in s ?? "") { if (sb.Length >= max) break; sb.Append(c < 128 && (char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '_'); }
            return sb.Length == 0 ? "_" : sb.ToString();
        }

        // FNV-1a, 32 bits: stable across runs and machines (string.GetHashCode is not)
        static string Hash(string s)
        {
            uint h = 2166136261;
            foreach (var b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 16777619; }
            return h.ToString("x8", CultureInfo.InvariantCulture);
        }

        /// <summary>Keeps one fellow's copy in <paramref name="dir"/> (the world's folder), received at <paramref name="receivedUtc"/>.</summary>
        public static void Save(string dir, string key, string json, DateTime receivedUtc)
        {
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(json)) return;
            var head = new Json().Open().Num("version", Version).Str("key", key).Str("received", receivedUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)).Close().ToString();
            AtomicFile.Write(Path.Combine(dir, FileName(key)), head + "\n" + json);
        }

        /// <summary>The cached copies of one world, newest first, at most MaxFellows, none older than MaxAgeDays; an unreadable file is skipped.</summary>
        public static List<Entry> Load(string dir, DateTime utcNow)
        {
            var list = new List<Entry>();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return list;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    // usable = a header line AND a whole JSON object after it: a copy cut after its header falls back to the .bak
                    var text = AtomicFile.ReadWithBackup(file, t => { var cut = t.IndexOf('\n'); return cut > 0 && ServerIntake.IsJsonObject(t.Substring(cut + 1)); }, out _);
                    var nl = text?.IndexOf('\n') ?? -1;
                    if (nl <= 0) continue;
                    var head = text.Substring(0, nl); var json = text.Substring(nl + 1);
                    var key = Transport.Field(head, "key");
                    if (key.Length == 0 || FellowIds.KeyOf(json) != key) continue;   // the copy must be who the file says
                    if (!DateTime.TryParse(Transport.Field(head, "received"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)) continue;
                    at = at.ToUniversalTime();
                    if ((utcNow - at).TotalDays > MaxAgeDays) { Remove(dir, key); continue; }
                    list.Add(new Entry { Key = key, Json = json, ReceivedUtc = at });
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            return list.OrderByDescending(e => e.ReceivedUtc).Take(MaxFellows).ToList();
        }

        /// <summary>Deletes one fellow's copy (and its backup) from the world's folder.</summary>
        public static void Remove(string dir, string key)
        {
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(key)) return;
            var file = Path.Combine(dir, FileName(key));
            foreach (var f in new[] { file, file + AtomicFile.BackupSuffix, file + AtomicFile.TempSuffix }) if (File.Exists(f)) File.Delete(f);
        }

        /// <summary>Deletes every cached copy not in <paramref name="keep"/> (the server's list of who shares now).</summary>
        public static void KeepOnly(string dir, ICollection<string> keep)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            var files = new HashSet<string>(keep.Select(FileName), StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                var name = Path.GetFileName(file);
                if (files.Contains(name)) continue;
                foreach (var f in new[] { file, file + AtomicFile.BackupSuffix, file + AtomicFile.TempSuffix }) if (File.Exists(f)) File.Delete(f);
            }
        }

        /// <summary>Deletes every cached copy of every world (you stopped sharing: you keep nobody's), and the fellow marks next to each
        /// character's totals (local/&lt;id&gt;-&lt;name&gt;.fellows.json: their running totals, REVIEW-07 #5).</summary>
        public static void ForgetAll(string root)
        {
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) Directory.Delete(root, true);
            var local = string.IsNullOrEmpty(root) ? null : Path.GetDirectoryName(root);
            if (local != null && Directory.Exists(local))
                foreach (var f in Directory.GetFiles(local, "*.fellows.json*")) File.Delete(f);   // the marks, their backup and a half-written temp
        }

        /// <summary>Ages out every world's cached copies (REVIEW-07 #5: not only the world you load): older than MaxAgeDays goes, an empty
        /// world folder goes. Run off the main thread at spawn.</summary>
        public static void SweepAll(string root, DateTime utcNow)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
            foreach (var dir in Directory.GetDirectories(root))
            {
                try
                {
                    Load(dir, utcNow);   // deletes what is too old as it reads
                    if (Directory.GetFiles(dir).Length == 0) Directory.Delete(dir);
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
