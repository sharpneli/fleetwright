using System.Diagnostics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen shore`: the land index's build and queries, measured (docs/world/terrain-plan.md T1): build time
/// and memory, IsLand against Land.IsLand at random points, and what each query costs in open water and near a coast.</summary>
static class ShoreBench
{
    public static int Run(string landPath, int points)
    {
        var sw = Stopwatch.StartNew();
        var land = Land.Load(landPath);
        Console.WriteLine($"land: {land.RingCount} rings, {land.PointCount} points, loaded in {sw.ElapsedMilliseconds} ms");
        Shore shore = null!;
        for (int run = 0; run < 3; run++)
        {
            long before = GC.GetTotalAllocatedBytes(true);
            sw.Restart();
            shore = Shore.Build(land);
            Console.WriteLine($"build {run + 1}: {sw.ElapsedMilliseconds} ms, {(GC.GetTotalAllocatedBytes(true) - before) / 1e6:F0} MB allocated");
        }
        Console.WriteLine($"index: {shore.CoastCells} coast cells, {shore.Bytes / 1e6:F1} MB kept");

        var rng = new Random(1);
        var uniform = new Vec3d[points];
        for (int k = 0; k < points; k++)
            uniform[k] = RandomUnit(rng);
        // near the coast: a random ring point, moved up to 5 km in a random direction
        var near = new Vec3d[points];
        for (int k = 0; k < points; k++)
        {
            int r = rng.Next(land.RingCount);
            var ring = land.Ring(r);
            var p = ring[rng.Next(ring.Length)];
            near[k] = Earth.Forward(p, rng.NextDouble() * 360) * (rng.NextDouble() * 5000 / Earth.Radius) + p;
        }

        int checks = Math.Min(points, 2000), wrong = 0;
        sw.Restart();
        for (int k = 0; k < checks; k++)
        {
            if (land.IsLand(uniform[k]) != shore.IsLand(uniform[k]))
                wrong++;
            if (land.IsLand(near[k]) != shore.IsLand(near[k]))
                wrong++;
        }
        Console.WriteLine($"IsLand against Land.IsLand: {wrong} of {2 * checks} differ ({sw.Elapsed.TotalMilliseconds / (2 * checks):F2} ms a pair)");

        Time("IsLand, anywhere", uniform, p => shore.IsLand(p) ? 1 : 0);
        Time("IsLand, near the coast", near, p => shore.IsLand(p) ? 1 : 0);
        Time("Clearance 1 km, near the coast", near, p => shore.Clearance(p, 1000, out _));
        Time("Clearance 50 km, near the coast", near, p => shore.Clearance(p, 50_000, out _));
        Time("FirstContact 200 km at 200 m, anywhere at sea", uniform,
            p => shore.IsLand(p) ? 0 : shore.FirstContact(p, Earth.Forward(p, 37), 200_000, 200));
        Time("FirstContact 20 km at 200 m, near the coast", near,
            p => shore.FirstContact(p, Earth.Forward(p, 37), 20_000, 200));
        Time("FirstContactRhumb 200 km at 200 m, anywhere at sea", uniform,
            p => shore.IsLand(p) || Math.Abs(p.Z) > 0.98 ? 0 : shore.FirstContactRhumb(p, 37, 200_000, 200));
        Time("FirstContactRhumb 20 km at 200 m, near the coast", near,
            p => Math.Abs(p.Z) > 0.98 ? 0 : shore.FirstContactRhumb(p, 37, 20_000, 200));
        return 0;
    }

    static void Time(string what, Vec3d[] at, Func<Vec3d, double> query)
    {
        double sink = 0;
        foreach (var p in at.AsSpan(0, Math.Min(at.Length, 1000)))
            sink += query(p);   // warm up
        long alloc = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        foreach (var p in at)
            sink += query(p);
        double us = sw.Elapsed.TotalMilliseconds * 1000 / at.Length;
        alloc = GC.GetAllocatedBytesForCurrentThread() - alloc;
        Console.WriteLine($"{what}: {us:F2} µs ({alloc} bytes allocated){(sink == 1e300 ? "" : "")}");
    }

    static Vec3d RandomUnit(Random rng)
    {
        double z = 2 * rng.NextDouble() - 1, lon = 2 * Math.PI * rng.NextDouble(), c = Math.Sqrt(1 - z * z);
        return new Vec3d(c * Math.Cos(lon), c * Math.Sin(lon), z);
    }
}
