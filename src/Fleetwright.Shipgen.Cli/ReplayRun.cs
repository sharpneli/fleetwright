using System.Diagnostics;
using System.Reflection;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen replay FILE [--to HH:MM[:SS]]</c>: a replay (src/Fleetwright.Sim.Missions/Replay.cs) played headless
/// on the land, as the game played it: what it holds, then its world made again and its commands played to the end of the
/// record (or a time of day); at a clean end, the world's hash beside the recorded one. The way to tell a tester's bug
/// reproduces before opening it in the game (<c>-replay=FILE@HH:MM</c>).</summary>
static class ReplayRun
{
    public static int Run(string path, string landPath, string? to, bool land)
    {
        var r = Replay.Read(path);
        string build = typeof(ReplayRun).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        Console.WriteLine($"{Path.GetFileName(path)}: {r.ScenarioName}, recorded {r.Recorded} by {r.Build}" + (r.Build != build ? $" (this is {build})" : ""));
        Console.WriteLine($"  {r.Files.Count} scenario file(s){(r.Setup != null ? " and a setup" : "")}, {r.Designs.Count} designs, seed {r.Seed}, " +
                          $"{r.Commands.Count} commands ({r.Placed} the placing's), {r.Start.ToUtc():yyyy-MM-dd HH:mm:ss} to {r.Reached.ToUtc():HH:mm:ss}");
        if (r.Cut > 0)
            Console.WriteLine($"  cut short at line {r.Cut} (a crash?)");
        if (r.Error is { } error)
            Console.WriteLine($"  the sim stopped on: {error.Split('\n')[0]}");
        var until = r.Reached;
        if (to != null)
        {
            if (!TimeSpan.TryParse(to, System.Globalization.CultureInfo.InvariantCulture, out var t))
            {
                Console.Error.WriteLine($"--to {to}: want HH:MM[:SS]");
                return 2;
            }
            var day = r.Start.ToUtc().Date + t;
            until = WorldTime.FromUtc(day < r.Start.ToUtc() ? day.AddDays(1) : day);
        }

        var clock = Stopwatch.StartNew();
        var shore = land ? Shore.Build(Land.Load(landPath)) : null;
        using var registry = new ShipClassRegistry(0);
        string scratch = Path.Combine(Path.GetTempPath(), "fleetwright-replay", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Rebuilt rb;
        try
        {
            rb = r.Rebuild(registry, shore, scratch);
        }
        catch (ReplayException x)
        {
            Console.Error.WriteLine($"  can't be made again: {x.Message}");
            return 1;
        }
        Console.WriteLine($"  made again in {clock.Elapsed.TotalSeconds:F1} s: {rb.Start.Ships} ships in {rb.Start.Groups} groups");
        clock.Restart();
        int next;
        try
        {
            next = r.Play(rb.World, r.Placed, until);
        }
        catch (ReplayException x)
        {
            Console.Error.WriteLine($"  can't be played: {x.Message}");
            return 1;
        }
        var w = rb.World;
        Console.WriteLine($"  played {next - r.Placed} commands to {w.Time.ToUtc():HH:mm:ss} in {clock.Elapsed.TotalSeconds:F1} s; {w.CombatLog.Count} in the combat log");
        ScenarioRun.Sides(w, rb.Ai, rb.Scenario);
        if (r.End is { } end && end.Time == w.Time)
        {
            ulong hash = w.ComputeHash();
            bool same = hash == end.Hash;
            Console.WriteLine($"  the end's hash: {(same ? "the same as recorded" : $"DIFFERENT ({hash:x16}, recorded {end.Hash:x16})")}");
            return same ? 0 : 1;
        }
        Console.WriteLine(r.End is null ? "  no recorded end to check against" : "  stopped before the recorded end: nothing to check");
        return 0;
    }
}
