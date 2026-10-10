using System;
using System.IO;
using System.Text;

namespace Hearthwoven
{
    /// <summary>
    /// Crash-safe file writes with one backup copy (RESILIENCE-06 item 3). Write: the text goes to "&lt;path&gt;.tmp", is
    /// flushed through to the disk (FileStream.Flush(true)), then File.Replace swaps it in and keeps the file it replaced as
    /// "&lt;path&gt;.bak" (one copy, renewed on every write). A crash or power loss mid-write leaves the old file or the new
    /// one, never half of one; should the disk still lose the new file's data, the .bak holds the save before it.
    /// ReadWithBackup: the main file when it is there and usable, else the .bak (fromBackup says which). A main file that
    /// exists but cannot be opened (locked, no rights) throws: the caller must not save over what it could not read.
    /// The .bak is the last GOOD copy (0.8): a blank main file is never rotated into it, and a loader that had to fall back to the
    /// .bak sets the broken main aside (SetAside) before the next write, so a broken file can never push the good copy out.
    /// UTF-8 without BOM. Pure C# (no Unity calls): shared by the client's local files and the server's JSON files.
    /// </summary>
    public static class AtomicFile
    {
        public const string TempSuffix = ".tmp", BackupSuffix = ".bak";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Writes <paramref name="text"/> to <paramref name="path"/> atomically, keeping the previous file as .bak.</summary>
        public static void Write(string path, string text)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + TempSuffix, bak = path + BackupSuffix;
            var bytes = Utf8.GetBytes(text ?? "");
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);   // the data reaches the disk before the rename does
            }
            if (!File.Exists(path)) { File.Move(tmp, path); return; }
            // 0.8: the .bak is the previous GOOD copy. A main file that is blank (empty or zero-filled after a power loss) is never
            // rotated into it: the new file replaces it and the .bak stays as it is
            var keepBak = File.Exists(bak) && LooksBlank(path);
            try { File.Replace(tmp, path, keepBak ? null : bak, true); }
            catch (Exception e) when (e is PlatformNotSupportedException || e is IOException)
            {
                // a file system without an atomic replace (or Replace failed half way): copy instead, keeping the backup first
                if (File.Exists(path) && !keepBak) File.Copy(path, bak, true);
                File.Copy(tmp, path, true);
                File.Delete(tmp);
            }
        }

        /// <summary>True when the file holds nothing but NUL bytes or whitespace in its first 4 KB (a zero-length or zero-filled file
        /// after a power loss); false when it cannot be read. Cheap: one small read, done only when a .bak exists to protect.</summary>
        static bool LooksBlank(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var buf = new byte[(int)Math.Min(4096, fs.Length)];
                    int n = fs.Read(buf, 0, buf.Length);
                    for (int i = 0; i < n; i++) if (buf[i] != 0 && buf[i] != (byte)' ' && buf[i] != (byte)'\n' && buf[i] != (byte)'\r' && buf[i] != (byte)'\t') return false;
                    return true;
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// Sets a file that could not be read aside: renamed to "&lt;path&gt;.&lt;yyyyMMddHHmmss&gt;.unreadable" (never over an earlier one),
        /// never deleted, so a person can still look at it and the next Write does not turn it into the .bak (which keeps the last good
        /// copy). Returns the new name, or null when there was no file or it could not be moved.
        /// </summary>
        public static string SetAside(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                var aside = path + "." + stamp + ".unreadable";
                for (int n = 2; File.Exists(aside); n++) aside = path + "." + stamp + "-" + n + ".unreadable";
                File.Move(path, aside);
                return aside;
            }
            catch { return null; }
        }

        /// <summary>The text of <paramref name="path"/>, or of its .bak when the main file is missing, blank (a zero-filled
        /// file after a power loss) or fails <paramref name="usable"/> (null = any non-blank text). Neither usable: the main
        /// file's text as it is (null when there is none), and fromBackup false.</summary>
        public static string ReadWithBackup(string path, out bool fromBackup) => ReadWithBackup(path, null, out fromBackup);

        public static string ReadWithBackup(string path, Func<string, bool> usable, out bool fromBackup)
        {
            fromBackup = false;
            var main = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;   // throws when it exists but cannot be opened
            if (Usable(main, usable)) return main;
            string bak = null;
            try { if (File.Exists(path + BackupSuffix)) bak = File.ReadAllText(path + BackupSuffix, Encoding.UTF8); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            if (Usable(bak, usable)) { fromBackup = true; return bak; }
            return main;
        }

        static bool Usable(string text, Func<string, bool> usable)
        {
            if (text == null || Blank(text)) return false;
            try { return usable == null || usable(text); } catch { return false; }
        }

        /// <summary>True for text with nothing in it but whitespace, NUL bytes or a BOM.</summary>
        public static bool Blank(string text)
        {
            foreach (var c in text ?? "") if (c != '\0' && c != '﻿' && !char.IsWhiteSpace(c)) return false;
            return true;
        }
    }
}
