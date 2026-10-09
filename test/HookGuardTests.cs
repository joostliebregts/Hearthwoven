// RESILIENCE-06 item 6: one guard for every Harmony hook (src/HookGuard.cs). Tests the guard itself (never throws, logs once per hook
// per session, counts the rest, one summary line at the session's end), then reads the IL of EVERY patch method in the mod and fails
// when one could let an exception reach the game: each must hand its body to HookGuard.Run, or keep every call, field access and
// cast inside a try whose catch calls HookGuard.Fail (a helper it calls counts when that helper is guarded the same way). Last, every
// patch method is called with empty arguments outside the game, where nearly every game call throws: nothing may escape.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Hearthwoven;

static class HookGuardTests
{
    static int fails;
    static void Check(bool ok, string what) { System.Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static string Here([CallerFilePath] string path = "") => path;

    class Fixture { public int Value; }

    public static int Run()
    {
        fails = 0;
        var said = new List<string>();
        var keepSink = HookGuard.Sink;
        HookGuard.Sink = said.Add;
        try
        {
            // ---------- the guard ----------
            HookGuard.EndSession(); said.Clear();
            Exception escaped = null;
            try { for (int i = 0; i < 5; i++) HookGuard.Run(() => throw new InvalidOperationException("boom " + i)); } catch (Exception e) { escaped = e; }
            var name = said.Count == 1 ? said[0] : "";
            Check(escaped == null && said.Count == 1 && name.Contains("HookGuardTests.Run") && name.Contains("InvalidOperationException: boom 0") && HookGuard.Failures("HookGuardTests.Run") == 5,
                  "guard: five failures of one hook, nothing reaches the caller, one log line naming the hook and the first reason, the other four counted (" + name + ")");
            HookGuard.Fail(new TypeInitializationException("X", new MissingFieldException("Humanoid", "m_blockTimer")), "Some.Hook.Prefix");
            Check(said.Count == 2 && said[1].Contains("Some.Hook.Prefix") && said[1].Contains("MissingFieldException"), "guard: a second hook logs its own first failure, with the cause under a type-init wrapper");
            Check(HookGuard.Run(() => true, false) && !HookGuard.Run<bool>(() => throw new Exception("x"), false) && HookGuard.Run<bool>(() => throw new Exception("x"), true),
                  "guard: a value-returning body gives its value, or the caller's fallback when it throws");
            var summary = HookGuard.Summary();
            HookGuard.EndSession();
            Check(summary != null && said.Count == 3 && said[2].Contains("HookGuardTests.Run x7") && said[2].Contains("Some.Hook.Prefix x1") && HookGuard.Summary() == null && HookGuard.Failures("Some.Hook.Prefix") == 0,
                  "guard: the session's end logs one line with every failed hook and its count, then the counts start over (" + (said.Count > 2 ? said[2] : "") + ")");
            HookGuard.Run(() => throw new Exception("again"));
            Check(said.Count == 4, "guard: in the next session a failing hook is logged once again");
            HookGuard.Sink = m => throw new Exception("the log itself broke");
            escaped = null;
            try { HookGuard.Fail(new Exception("y"), "Other.Hook"); HookGuard.EndSession(); } catch (Exception e) { escaped = e; }
            Check(escaped == null, "guard: a failing log never throws out of the guard");
            HookGuard.Sink = said.Add;

            var missing = new GameField<Fixture, int>("m_renamedByAGameUpdate");
            Exception first = null, second = null;
            try { missing.Of(new Fixture()); } catch (Exception e) { first = e; }
            try { missing.Of(new Fixture()); } catch (Exception e) { second = e; }
            Check(first != null && second is MissingFieldException,   // (a field that is there is read by Harmony, which needs the game's MonoMod: not here)
                  "GameField: a field that is gone throws when read (inside a guarded hook), not when the class loads, and keeps saying so");

            // ---------- every patch method, by its IL ----------
            var asm = typeof(HookGuard).Assembly;
            Type[] types;
            try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            var patchClasses = types.Where(t => t.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch")).ToList();
            var hookNames = new HashSet<string> { "Prefix", "Postfix", "Finalizer" };
            var methods = patchClasses.SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                                                       .Where(m => hookNames.Contains(m.Name) || m.GetCustomAttributesData().Any(a => a.AttributeType.FullName is "HarmonyLib.HarmonyPrefix" or "HarmonyLib.HarmonyPostfix" or "HarmonyLib.HarmonyFinalizer"))).ToList();
            var srcDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here()), "..", "src"));
            var declared = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories).Sum(f => File.ReadAllLines(f).Count(l => l.TrimStart().StartsWith("[HarmonyPatch(")));
            Check(patchClasses.Count == declared && methods.Count >= declared, "hooks: every [HarmonyPatch] class in the source is found by reflection (" + patchClasses.Count + " of " + declared + "), " + methods.Count + " patch methods");
            var unguarded = new List<string>();
            foreach (var m in methods)
            {
                var why = Unguarded(m, 0, new HashSet<MethodBase>());
                if (why != null) unguarded.Add(m.DeclaringType.Name + "." + m.Name + ": " + why);
            }
            Check(unguarded.Count == 0 && methods.Count > 0, "hooks: every patch method is guarded (HookGuard.Run, or try/catch with HookGuard.Fail around every call and field access)" +
                                                             (unguarded.Count > 0 ? ": " + string.Join("; ", unguarded) : ""));

            // ---------- every patch method, called outside the game ----------
            var leaked = new List<string>(); int called = 0, notHere = 0;
            foreach (var m in methods)
            {
                object[] args;
                try { args = m.GetParameters().Select(p => { var t = p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType; return t.IsValueType ? Activator.CreateInstance(t) : null; }).ToArray(); }
                catch (Exception) { notHere++; continue; }   // a parameter type needs a game assembly that is not in lib/ (the IL check above still covers it)
                try { called++; m.Invoke(null, args); }
                catch (TargetInvocationException e) when (e.InnerException is FileNotFoundException) { called--; notHere++; }   // the method itself cannot compile here (a game assembly not in lib/)
                catch (TargetInvocationException e) { leaked.Add(m.DeclaringType.Name + "." + m.Name + ": " + e.InnerException?.GetType().Name + " " + e.InnerException?.Message); }
            }
            Check(leaked.Count == 0 && called >= methods.Count * 3 / 4, "hooks: " + called + " patch methods called with empty arguments outside the game (where game and Unity calls throw; " + notHere +
                                     " need a game assembly not here), none lets an exception out" + (leaked.Count > 0 ? ": " + string.Join("; ", leaked.Take(5)) : ""));
            HookGuard.EndSession();
        }
        finally { HookGuard.Sink = keepSink; }
        return fails;
    }

    // ---------- a small IL reader ----------

    static readonly OpCode[] One = new OpCode[0x100], Two = new OpCode[0x100];
    static HookGuardTests()
    {
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!(f.GetValue(null) is OpCode o)) continue;
            var v = (ushort)o.Value;
            if (v < 0x100) One[v] = o; else if ((v & 0xff00) == 0xfe00) Two[v & 0xff] = o;
        }
    }

    struct Ins { public int At; public OpCode Op; public int Token; }

    static List<Ins> Read(byte[] il)
    {
        var list = new List<Ins>();
        for (int i = 0; i < il.Length;)
        {
            var at = i; OpCode op = il[i] == 0xfe ? Two[il[i + 1]] : One[il[i]]; i += il[at] == 0xfe ? 2 : 1;
            int token = 0;
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: i += 1; break;
                case OperandType.InlineVar: i += 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: i += 8; break;
                case OperandType.InlineSwitch: { var n = BitConverter.ToInt32(il, i); i += 4 + 4 * n; break; }
                case OperandType.InlineMethod: case OperandType.InlineField: case OperandType.InlineType: case OperandType.InlineTok: case OperandType.InlineString: case OperandType.InlineSig:
                    token = BitConverter.ToInt32(il, i); i += 4; break;
                default: i += 4; break;   // InlineBrTarget, InlineI, ShortInlineR
            }
            list.Add(new Ins { At = at, Op = op, Token = token });
        }
        return list;
    }

    static bool Generated(Type t) { for (; t != null; t = t.DeclaringType) if (t.IsDefined(typeof(CompilerGeneratedAttribute), false)) return true; return false; }

    // opcodes that cannot throw on their own (loads and stores of arguments and locals, constants, branches, returns, out-parameter stores)
    static readonly HashSet<string> Harmless = new HashSet<string>
    {
        "nop", "ret", "dup", "pop", "ldnull", "ldstr", "ldftn", "ldtoken", "initobj", "box", "ceq", "cgt", "cgt.un", "clt", "clt.un", "endfinally",
    };
    static bool IsHarmless(OpCode op)
    {
        var n = op.Name;
        return Harmless.Contains(n) || n.StartsWith("ldarg") || n.StartsWith("starg") || n.StartsWith("ldloc") || n.StartsWith("stloc") || n.StartsWith("ldc.")
            || n.StartsWith("br") || n.StartsWith("leave") || n.StartsWith("stind.") || n.StartsWith("ldind.") || n is "beq" or "beq.s" or "bne.un" or "bne.un.s"
            || n.StartsWith("bge") || n.StartsWith("bgt") || n.StartsWith("ble") || n.StartsWith("blt") || n.StartsWith("conv.") && !n.Contains("ovf");
    }

    static bool IsGuard(MethodBase mb) => mb?.DeclaringType == typeof(HookGuard) && (mb.Name == "Run" || mb.Name == "Fail" || mb.Name == "EndSession");

    /// <summary>null when the method is guarded; otherwise what is not.</summary>
    static string Unguarded(MethodBase m, int depth, HashSet<MethodBase> seen)
    {
        if (!seen.Add(m)) return null;
        var body = m.GetMethodBody();
        if (body == null) return "no body";
        var il = body.GetILAsByteArray();
        var ins = Read(il);
        var module = m.Module;
        Type[] targs = m.DeclaringType != null && m.DeclaringType.IsGenericType ? m.DeclaringType.GetGenericArguments() : null;
        Type[] margs = m.IsGenericMethod ? m.GetGenericArguments() : null;
        MethodBase Method(int token) { try { return module.ResolveMethod(token, targs, margs); } catch { return null; } }
        FieldInfo Field(int token) { try { return module.ResolveField(token, targs, margs); } catch { return null; } }

        // the try ranges whose catch takes every exception and reports it to the guard
        var guarded = new List<(int from, int to)>();
        foreach (var c in body.ExceptionHandlingClauses)
        {
            if (c.Flags != ExceptionHandlingClauseOptions.Clause || (c.CatchType != typeof(Exception) && c.CatchType != typeof(object))) continue;
            var reports = ins.Any(x => x.At >= c.HandlerOffset && x.At < c.HandlerOffset + c.HandlerLength && (x.Op == OpCodes.Call || x.Op == OpCodes.Callvirt) && IsGuard(Method(x.Token)));
            if (reports) guarded.Add((c.TryOffset, c.TryOffset + c.TryLength));
        }
        foreach (var x in ins)
        {
            if (guarded.Any(g => x.At >= g.from && x.At < g.to) || IsHarmless(x.Op)) continue;
            if (x.Op == OpCodes.Call || x.Op == OpCodes.Callvirt || x.Op == OpCodes.Newobj)
            {
                var mb = Method(x.Token);
                if (mb == null) return x.Op.Name + " of an unresolved method at IL_" + x.At.ToString("x4");
                if (IsGuard(mb)) continue;
                if (x.Op == OpCodes.Newobj && (typeof(Delegate).IsAssignableFrom(mb.DeclaringType) || Generated(mb.DeclaringType))) continue;   // the closure handed to HookGuard.Run
                if (mb.DeclaringType?.Assembly == m.Module.Assembly && !mb.IsConstructor && depth < 3 && Unguarded(mb, depth + 1, seen) == null) continue;   // a guarded helper (Safe, Pick, Chop)
                return x.Op.Name + " " + mb.DeclaringType?.Name + "." + mb.Name + " outside a guard at IL_" + x.At.ToString("x4");
            }
            if (x.Op == OpCodes.Ldfld || x.Op == OpCodes.Stfld || x.Op == OpCodes.Ldflda || x.Op == OpCodes.Ldsfld || x.Op == OpCodes.Stsfld || x.Op == OpCodes.Ldsflda)
            {
                var f = Field(x.Token);
                if (f != null && Generated(f.DeclaringType)) continue;   // the closure's own fields and the compiler's cached lambdas
                return x.Op.Name + " " + f?.DeclaringType?.Name + "." + f?.Name + " outside a guard at IL_" + x.At.ToString("x4");
            }
            return x.Op.Name + " outside a guard at IL_" + x.At.ToString("x4");
        }
        return null;
    }
}
