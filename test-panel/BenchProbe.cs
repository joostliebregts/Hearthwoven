// The bench's allocation probe (0.8 performance pass): test-panel -- --bench <dir> --probe <page prefix> builds each matching page 40 times
// and prints what it allocated by type (the runtime's GCAllocationTick samples, about one per 100 KB), so a hot spot can be found by its
// garbage (DamageRow, String, a LINQ iterator) before anything is changed. .NET only; a sample, not an exact count.
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using Hearthwoven;
using Hearthwoven.Panel;

sealed class AllocListener : EventListener
{
    public readonly Dictionary<string, long> ByType = new Dictionary<string, long>();
    public bool On;
    protected override void OnEventSourceCreated(EventSource s)
    {
        if (s.Name == "Microsoft-Windows-DotNETRuntime") EnableEvents(s, EventLevel.Verbose, (EventKeywords)0x1);
    }
    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (!On || e.EventName == null || !e.EventName.StartsWith("GCAllocationTick")) return;
        var i = e.PayloadNames.IndexOf("TypeName"); var a = e.PayloadNames.IndexOf("AllocationAmount64");
        var t = i >= 0 ? e.Payload[i] as string : "?"; long amt = a >= 0 ? Convert.ToInt64(e.Payload[a]) : 100000;
        lock (ByType) { ByType.TryGetValue(t ?? "?", out var o); ByType[t ?? "?"] = o + amt; }
    }
}

static class AllocProbe
{
    public static void Run(PanelInput book, PanelState st, string label, int n, Func<PanelInput, PanelState, string> refresh)
    {
        using var l = new AllocListener();
        System.Threading.Thread.Sleep(200);
        l.On = true;
        for (int i = 0; i < n; i++) refresh(book, st);
        System.Threading.Thread.Sleep(500);
        l.On = false;
        System.Console.WriteLine("== " + label);
        lock (l.ByType) foreach (var kv in l.ByType.OrderByDescending(kv => kv.Value).Take(14)) System.Console.WriteLine("   " + (kv.Value / 1024 / n).ToString().PadLeft(7) + " KB/build  " + kv.Key);
    }
}
