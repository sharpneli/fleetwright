// alloctop X.nettrace [-n N] [--focus Method [--callers]]
// Sums a gc-verbose trace's GC AllocationTick samples (one per ~100 KB allocated, with its stack) by the innermost
// Fleetwright frame, by allocated type, and by both. --focus splits what a method's stacks allocate by its direct
// callees (the frame below it), or with --callers by its direct callers. Writes X.etlx beside the trace.
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: alloctop X.nettrace [-n N] [--focus Method [--callers]]");
    return 1;
}
var path = args[0];
int n = 30;
string? focus = null;
bool byCaller = false;
for (int i = 1; i < args.Length; i++)
{
    if (args[i] == "-n")
        n = int.Parse(args[++i]);
    else if (args[i] == "--focus")
        focus = args[++i];
    else if (args[i] == "--callers")
        byCaller = true;
}

using var log = new TraceLog(TraceLog.CreateFromEventPipeDataFile(path));
var byFrame = new Dictionary<string, double>();
var byType = new Dictionary<string, double>();
var byPair = new Dictionary<string, double>();
var around = new Dictionary<string, double>();
double total = 0;

void Add(Dictionary<string, double> d, string key, double mb) => d[key] = d.GetValueOrDefault(key) + mb;

foreach (var ev in log.Events)
{
    if (ev is not GCAllocationTickTraceData a)
        continue;
    double mb = a.AllocationAmount64 / 1e6;
    total += mb;
    string type = a.TypeName ?? "?";
    // the stack, innermost first
    var chain = new List<string>();
    for (var s = ev.CallStack(); s != null; s = s.Caller)
        if (!string.IsNullOrEmpty(s.CodeAddress.FullMethodName))
            chain.Add(s.CodeAddress.ModuleName + "!" + s.CodeAddress.FullMethodName);
    if (focus != null)
    {
        int k = chain.FindIndex(m => m.Contains(focus));
        if (k < 0)
            continue;
        string next = byCaller ? (k + 1 < chain.Count ? chain[k + 1] : "(root)") : (k > 0 ? chain[k - 1] : "(self)");
        Add(around, $"{next}  <{type}>", mb);
        continue;
    }
    string frame = chain.FirstOrDefault(m => m.Contains("Fleetwright")) ?? "(no Fleetwright frame)";
    Add(byFrame, frame, mb);
    Add(byType, type, mb);
    Add(byPair, $"{frame}  <{type}>", mb);
}

Console.WriteLine($"total sampled {total:F0} MB");

void Top(string title, Dictionary<string, double> d)
{
    Console.WriteLine($"== {title}");
    foreach (var (k, v) in d.OrderByDescending(p => p.Value).Take(n))
        Console.WriteLine($"{v,9:F1}  {(k.Length > 190 ? k[..190] : k)}");
}

if (focus != null)
    Top($"{(byCaller ? "direct callers" : "direct callees")} of {focus} (MB, type)", around);
else
{
    Top("by innermost Fleetwright frame (MB)", byFrame);
    Top("by type (MB)", byType);
    Top("by frame and type (MB)", byPair);
}
return 0;
