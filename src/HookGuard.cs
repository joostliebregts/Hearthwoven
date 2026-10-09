using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Hearthwoven
{
    /// <summary>
    /// The one guard around every Harmony prefix, postfix and finalizer (RESILIENCE-06 item 6). A hook that throws never reaches
    /// the game: the game's method runs exactly as it would without the mod (a prefix that could skip it lets it run, a result
    /// stays as the game set it). The first failure of each hook in a session is logged with its reason; later ones are only
    /// counted, and the counts are logged in one line when the session ends (logout or quit), then start over.
    /// Every patch method either hands its body to <see cref="Run(Action)"/> or wraps it in try/catch with <see cref="Fail"/>
    /// in the catch; test/HookGuardTests.cs reads the IL of every patch method and fails the build's tests otherwise.
    /// </summary>
    public static class HookGuard
    {
        /// <summary>Where the guard writes: the game's log; tests swap it.</summary>
        public static Action<string> Sink = m => Debug.LogWarning(m);

        static readonly Dictionary<string, int> failures = new Dictionary<string, int>();
        static readonly object gate = new object();

        /// <summary>Runs a hook's body; an exception is counted and logged once, never passed on.</summary>
        public static void Run(Action body)
        {
            var start = DevCheck.On ? DevCheck.HookStart() : 0L;   // Dev.SelfCheck: the hook cost per frame (PerfMeter); nothing when off
            try { body(); }
            catch (Exception e) { Fail(e, NameOf(body)); }
            finally { if (start != 0L) DevCheck.HookEnd(start); }
        }

        /// <summary>Runs a hook's body that returns a value (a prefix's "run the game's method?"); on an exception: the fallback.</summary>
        public static T Run<T>(Func<T> body, T fallback)
        {
            var start = DevCheck.On ? DevCheck.HookStart() : 0L;
            try { return body(); }
            catch (Exception e) { Fail(e, NameOf(body)); return fallback; }
            finally { if (start != 0L) DevCheck.HookEnd(start); }
        }

        /// <summary>A hook's catch: counts the failure; logs it the first time this session.</summary>
        public static void Fail(Exception e, string hook)
        {
            try
            {
                int n;
                lock (gate) { failures.TryGetValue(hook ?? "?", out n); failures[hook ?? "?"] = ++n; }
                if (n == 1) Sink?.Invoke("[Hearthwoven] hook " + hook + " failed and is skipped; the game goes on as without the mod. Further failures this session are only counted: " + Reason(e));
            }
            catch { }   // the guard itself never throws
        }

        /// <summary>How often a hook failed this session (0: never).</summary>
        public static int Failures(string hook) { lock (gate) return failures.TryGetValue(hook ?? "?", out var n) ? n : 0; }

        /// <summary>Every hook that failed this session with its count ("ClientHooks.Block.Prefix x3, ..."); null when none did.</summary>
        public static string Summary()
        {
            lock (gate) return failures.Count == 0 ? null : string.Join(", ", failures.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + " x" + kv.Value).ToArray());
        }

        /// <summary>The session ends (logout, quit): one line with the counts, if any hook failed; then a new session logs afresh.</summary>
        public static void EndSession()
        {
            try
            {
                string line;
                lock (gate) { line = Summary(); failures.Clear(); }
                if (line != null) Sink?.Invoke("[Hearthwoven] hooks that failed this session: " + line);
            }
            catch { }
        }

        static string Reason(Exception e)
        {
            var inner = e;
            while ((inner is TypeInitializationException || inner is TargetInvocationException) && inner.InnerException != null) inner = inner.InnerException;
            var at = (inner.StackTrace ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            return inner.GetType().Name + ": " + inner.Message + (at != null ? " (" + at + ")" : "");
        }

        /// <summary>A body's hook name from the compiler's lambda ("ClientHooks.Block.Postfix"): the enclosing method and its class.</summary>
        internal static string NameOf(Delegate body)
        {
            try
            {
                var m = body?.Method; if (m == null) return "?";
                var name = m.Name;
                if (name.StartsWith("<")) { var end = name.IndexOf('>'); if (end > 1) name = name.Substring(1, end - 1); }
                var t = m.DeclaringType;
                while (t != null && t.IsDefined(typeof(CompilerGeneratedAttribute), false)) t = t.DeclaringType;
                if (t == null) return name;
                var path = t.FullName ?? t.Name;
                if (!string.IsNullOrEmpty(t.Namespace) && path.StartsWith(t.Namespace + ".")) path = path.Substring(t.Namespace.Length + 1);
                return path.Replace('+', '.') + "." + name;
            }
            catch { return "?"; }
        }
    }

    /// <summary>
    /// A game field read through Harmony, looked up on first use instead of in a static initializer: a field a game update renamed
    /// then fails inside one guarded hook (counted, logged once), never at type load where it would take the whole class down.
    /// </summary>
    public sealed class GameField<T, F> where T : class
    {
        readonly string name; AccessTools.FieldRef<T, F> read; bool tried;
        public GameField(string name) { this.name = name; }
        public F Of(T obj)
        {
            if (!tried) { tried = true; read = AccessTools.FieldRefAccess<T, F>(name); }
            if (read == null) throw new MissingFieldException(typeof(T).Name, name);
            return read(obj);
        }
    }
}
