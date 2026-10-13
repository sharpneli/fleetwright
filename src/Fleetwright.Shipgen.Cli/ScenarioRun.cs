using System.Diagnostics;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Shipgen.Cli;

/// <summary><c>shipgen scenario</c>: a scenario run headless at full tilt (docs/battle/plan.md step 2.7, the Jutland
/// test): how long its classes take to build (no art: that's the app's), the land and the placing, then game minutes
/// on the sim's own thread's work alone, with the ships on the fine tick and on rails, the speed it could run at, the
/// share of the sim thread a given speed would take, and what it allocated. With --replay, the command log is run again
/// on a fresh world and the hashes compared.</summary>
static class ScenarioRun
{
    public static int Run(string path, string settingsPath, string landPath, string designsPath, double minutes, int threads,
        int every, double atSpeed, bool land, bool replay, int[]? watch = null, double viewMpp = 0, double worldSpeed = 0)
    {
        var settings = SimSettings.Load(settingsPath);
        var total = Stopwatch.StartNew();
        var s = Scenario.Load(path);
        Console.WriteLine($"{s.Name}: {s.ShipCount} ships, {s.DesignNames().Count} designs, {s.Start}");

        // the land on a thread of its own, as the app does, while the classes build
        var shoreTask = land ? Task.Run(() => Shore.Build(Land.Load(landPath))) : Task.FromResult<Shore>(null!);

        var sw = Stopwatch.StartNew();
        using var registry = new ShipClassRegistry(threads);
        var classes = ScenarioLoader.RequestClasses(s, registry, [designsPath]);
        registry.WaitAll();
        double build = sw.Elapsed.TotalSeconds;
        int failed = classes.Values.Count(id => registry.Get(id) == null);
        Console.WriteLine($"classes: {classes.Count} built in {build:F2} s on {(threads > 0 ? threads : Math.Clamp(Environment.ProcessorCount / 2, 1, 4))} threads" +
                          (failed > 0 ? $", {failed} failed" : ""));
        foreach (var (name, id) in classes)
            if (registry.ErrorOf(id) is { } err)
                Console.WriteLine($"  {name}: {err}");

        sw.Restart();
        var shore = shoreTask.Result;
        double shoreWait = sw.Elapsed.TotalSeconds;
        sw.Restart();
        var w = new World(s.Start, s.Seed, settings, registry, land ? shore : null);
        var ai = new AiSystem(w);
        var start = ScenarioLoader.Apply(s, w, ai, classes);
        if (worldSpeed > 0)
            w.Apply(Command.SetSpeed(worldSpeed));   // the speed asked: the rails' rules read it (snaps from 10,000x)
        if (viewMpp > 0)
            w.Apply(Command.SetViewScale(viewMpp));   // as the app's map tells it (the rails' one display rule)
        double place = sw.Elapsed.TotalSeconds;
        Console.WriteLine($"land: {(land ? $"waited {shoreWait:F2} s more" : "none (a world of water)")}; placed {start.Ships} ships in " +
                          $"{start.Groups} groups in {place * 1000:F0} ms; ready {total.Elapsed.TotalSeconds:F2} s after the start");
        foreach (var x in start.Warnings)
            Console.WriteLine($"  {x}");

        // game minutes, each timed on this thread (the sim thread's work: no host, no snapshots); the probes from here
        var probes0 = new long[Probes.Capacity];
        var calls0 = new long[Probes.Capacity];
        Array.Copy(w.Probes.Ticks, probes0, Probes.Capacity);
        Array.Copy(w.Probes.Calls, calls0, Probes.Capacity);
        w.Probes.ResetMax();
        Console.WriteLine();
        Console.WriteLine("  game time   fine  rails   x real (this stretch)   allocated   gen0/1/2");
        var store = w.Ships.Store;
        var snap = new Snapshot();
        long alloc0 = GC.GetAllocatedBytesForCurrentThread();
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        double run = 0, worst = 0;
        var stretch = Stopwatch.StartNew();
        long stretchMs = 0;
        for (int m = 1; m <= minutes; m++)
        {
            sw.Restart();
            w.AdvanceTo(w.Time + 60_000);
            double t = sw.Elapsed.TotalSeconds;
            run += t;
            worst = Math.Max(worst, t);
            stretchMs += 60_000;
            if (m % every == 0 || m == (int)minutes)
            {
                double x = stretchMs / 1000.0 / stretch.Elapsed.TotalSeconds;
                Console.WriteLine($"  {w.Time.ToString()[11..19]}  {store.FineCount,5}  {store.Count - store.FineCount,5}   {x,12:F0}x            " +
                                  $"{(GC.GetAllocatedBytesForCurrentThread() - alloc0) / 1024.0,8:F0} KB   " +
                                  $"{GC.CollectionCount(0) - g0}/{GC.CollectionCount(1) - g1}/{GC.CollectionCount(2) - g2}");
                stretch.Restart();
                stretchMs = 0;
                if (watch != null)
                    w.Fill(snap);
                foreach (int e in watch ?? [])
                {
                    int j = snap.Ships.IndexOf(e);
                    if (j >= 0)
                    {
                        var (sp, _) = snap.Ships.PoseAt(j, snap.Time.Ms);
                        var (sla, slo) = Earth.LatLon(sp);
                        Console.WriteLine($"    #{e} in the snapshot: {sla:F4}, {slo:F4} (pose of {snap.Ships.PoseMs[j] - snap.Time.Ms} ms)");
                    }
                    int k = store.SlotOf(e);
                    if (k < 0)
                    {
                        Console.WriteLine($"    #{e}: gone");
                        continue;
                    }
                    var sh = store[k];
                    var (la, lo) = Earth.LatLon(sh.Position);
                    var g = w.Groups.Of(e);
                    Console.WriteLine($"    #{e} {start.ShipNames.GetValueOrDefault(e)}: {la:F4}, {lo:F4}, heading {sh.HeadingDeg:F1}, " +
                                      $"{sh.Speed / Fleetwright.Sim.Combat.Handling.MsPerKnot:F1} kn, helm {sh.Helm} {sh.CourseOrderDeg:F0}° {sh.SpeedOrderKn:F1} kn, " +
                                      $"{sh.Tier.ToString().ToLowerInvariant()}, {g?.Name ?? "no group"}");
                }
            }
        }
        double game = minutes * 60;
        double speed = game / run;
        Console.WriteLine();
        Console.WriteLine($"{minutes:F0} game minutes in {run:F2} s: {speed:F0}x at full tilt, the slowest minute {60 / worst:F0}x; " +
                          $"{run / (game * 1000 / settings.ShipTickMs) * 1e6:F1} µs a fine tick");
        Console.WriteLine($"the world's state hash at {w.Time}: {w.ComputeHash():X16}");
        Console.WriteLine($"at {atSpeed:G}x the sim thread is {atSpeed / speed:P1} busy (the target: at most 25% at 10x, docs/battle/plan.md 2.7)");
        Console.WriteLine("where the time went (share of the run, longest call):");
        for (int i = 0; i < w.Probes.Count; i++)
        {
            double ms = Probes.Ms(w.Probes.Ticks[i] - probes0[i]);
            if (ms > 0)
                Console.WriteLine($"  {w.Probes.Names[i],-26} {ms / 1000 / run,7:P1}  {ms / 1000,7:F2} s  {(w.Probes.Calls[i] - calls0[i]) / game,9:F1} calls a game second, " +
                                  $"{ms * 1000 / Math.Max(w.Probes.Calls[i] - calls0[i], 1),7:F2} µs each   (longest {Probes.Ms(w.Probes.Max[i]):F2} ms)");
        }

        if (replay)
        {
            sw.Restart();
            var r = new World(s.Start, s.Seed, settings, registry, land ? shore : null);
            _ = new AiSystem(r);
            foreach (var c in w.CommandLog)
            {
                r.AdvanceTo(c.At);
                r.Apply(c.Command);
            }
            r.AdvanceTo(w.Time);
            bool same = r.ComputeHash() == w.ComputeHash();
            Console.WriteLine($"replay of {w.CommandLog.Count} commands: {(same ? "the same hash" : "A DIFFERENT HASH")} ({sw.Elapsed.TotalSeconds:F2} s)");
            if (!same)
                return 1;
        }
        return failed > 0 ? 1 : 0;
    }
}
