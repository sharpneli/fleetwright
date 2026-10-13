using System.Diagnostics;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The hit query (docs/battle/fighting.md 4.1): the one tracer's crossings against the hull's own exact ray,
/// turrets at their bearings, a heeled ship hit where the heel says, a miss's fall, no allocation, and its cost.</summary>
public class HitQueryTests(ITestOutputHelper output)
{
    static readonly Vec3d North = new(0, 0, 1);

    /// <summary>A ship's frame on the equator at 0° E, heading north.</summary>
    static ShipFrame Frame() => ShipFrame.At(new Vec3d(Earth.Radius, 0, 0), North);

    /// <summary>A random falling segment at the ship: from 120 m out and up, down through a random point in its box.</summary>
    static (P3, P3) Shot(ShipClass c, RandomStream rng)
    {
        var b = c.Solids.Bounds;
        double tx = b.X0 + (b.X1 - b.X0) * rng.NextDouble(), ty = b.Y0 + (b.Y1 - b.Y0) * rng.NextDouble();
        double tz = c.Vertical.Keel + (b.Top - c.Vertical.Keel) * rng.NextDouble();
        double bearing = 2 * Math.PI * rng.NextDouble(), fall = double.DegreesToRadians(1 + 45 * rng.NextDouble());
        double ux = Math.Cos(bearing) * Math.Cos(fall), uy = Math.Sin(bearing) * Math.Cos(fall), uz = Math.Sin(fall);
        return (new P3(tx + 120 * ux, ty + 120 * uy, tz + 120 * uz), new P3(tx - 30 * ux, ty - 30 * uy, tz - 30 * uz));
    }

    [Fact]
    public void Hull_crossings_agree_with_the_hulls_own_ray()
    {
        int n = 0, close = 0, both = 0;
        var path = new HitPath();
        foreach (var name in new[] { "bismarck", "dreadnought", "destroyer", "mikasa", "yamato", "fleet_carrier" })
        {
            var c = Fleet.Get(name).Class!;
            var rng = RandomStream.FromSeed(11);
            for (int k = 0; k < 2000; k++)
            {
                var (p0, p1) = Shot(c, rng.Derive((ulong)k));
                ShipTracer.Trace(c, p0, p1, [], Waterplane.Level(c), path);
                double dx = p1.X - p0.X, dy = p1.Y - p0.Y, dz = p1.Z - p0.Z, len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                bool exact = c.Hull.Raycast(p0.X, p0.Y, p0.Z, dx / len, dy / len, dz / len, len, out var hit);
                double first = double.NaN;
                foreach (ref readonly var x in path.Crossings)
                    if (x.Kind == CrossingKind.HullIn)
                    {
                        first = x.T;
                        break;
                    }
                if (!exact && double.IsNaN(first))
                    continue;
                n++;
                if (exact && !double.IsNaN(first))
                {
                    both++;
                    close += Math.Abs(first - hit.T) < 0.01 ? 1 : 0;
                }
            }
        }
        output.WriteLine($"{n} rays met the hull: both found it {both}, within 1 cm {close}");
        // the walk can step over a sliver of hull thinner than its 5 cm step; the exact ray never misses it
        Assert.True(both >= 0.995 * n, $"{both} of {n} found by both");
        Assert.True(close >= 0.995 * both, $"{close} of {both} within 1 cm");
    }

    [Fact]
    public void A_turret_is_where_its_bearing_puts_it()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var g = c.Solids;
        int mount = 0;
        var m = c.Mounts[mount];
        var s = g.Solids[m.Component];
        // a point in the gunhouse at rest that it leaves when trained 90° off
        double z = (s.Box.Base + s.Box.Top) / 2;
        P3 p = default;
        for (double px = s.Box.X0; px <= s.Box.X1; px += 0.1)
            for (double py = s.Box.Y0; py <= s.Box.Y1; py += 0.1)
                if (g.Inside(s, px, py, z, m.RestDeg) && !g.Inside(s, px, py, z, m.RestDeg + 90))
                    (p, px) = (new P3(px, py, z), double.PositiveInfinity);
        Assert.True(g.Inside(s, p.X, p.Y, p.Z, m.RestDeg));
        Assert.False(g.Inside(s, p.X, p.Y, p.Z, m.RestDeg + 90));

        // straight down onto it: the roof at rest, nothing of that turret trained away
        var path = new HitPath();
        var bearings = new double[c.Mounts.Length];
        Array.Fill(bearings, double.NaN);
        bool Roof(double bearing)
        {
            bearings[mount] = bearing;
            ShipTracer.Trace(c, p with { Z = s.Box.Top + 5 }, p with { Z = s.Box.Base + 0.01 }, bearings, Waterplane.Level(c), path);
            foreach (ref readonly var x in path.Crossings)
                if (x.Kind == CrossingKind.PartIn && x.Index == m.Component)
                    return x.Plate == PlateKind.TurretRoof && x.Mm == m.RoofMm;
            return false;
        }
        Assert.True(Roof(m.RestDeg));
        Assert.False(Roof(m.RestDeg + 90));

        // from ahead of the barrels into the face, from behind into the rear
        PlateKind Wall(double fromDeg)
        {
            double f = double.DegreesToRadians(fromDeg);
            var aim = new P3(m.X, m.Y, z);
            bearings[mount] = m.RestDeg;
            ShipTracer.Trace(c, new P3(aim.X + 40 * Math.Cos(f), aim.Y + 40 * Math.Sin(f), z), aim, bearings, Waterplane.Level(c), path);
            foreach (ref readonly var x in path.Crossings)
                if (x.Kind == CrossingKind.PartIn && x.Index == m.Component)
                    return x.Plate;
            return PlateKind.None;
        }
        Assert.Equal(PlateKind.TurretFace, Wall(m.RestDeg));
        Assert.Equal(PlateKind.TurretRear, Wall(m.RestDeg + 180));
        Assert.Equal(PlateKind.TurretSide, Wall(m.RestDeg + 90));
    }

    [Fact]
    public void A_heeled_ship_is_hit_where_the_heel_says()
    {
        var c = Fleet.Get("bismarck").Class!;
        var f = Frame();
        double wl = c.Vertical.Waterline, heel = 10, phi = double.DegreesToRadians(heel);
        var heeled = new Attitude(0, 0, heel);

        // the frame both ways round, and the sea's surface on the waterplane
        var rng = RandomStream.FromSeed(3);
        foreach (var a in new[] { Attitude.Upright, heeled, new Attitude(1.5, -2, 7) })
        {
            var water = HitQuery.WaterOf(c, a);
            for (int k = 0; k < 50; k++)
            {
                var w = f.ToWorld(200 * rng.NextDouble() - 100, 60 * rng.NextDouble() - 30, 0);
                var p = HitQuery.ToShip(c, f, a, w);
                Assert.Equal(water.At(p.X, p.Y), p.Z, 9);
                Assert.True((HitQuery.ToWorld(c, f, a, p) - w).Length < 1e-6);
            }
        }

        // level shots from starboard at heights over the sea: upright the hull is hit h over the waterline; heeled to
        // starboard, the side has gone down, so it's hit higher on the hull: z = wl + (h + y sin φ) / cos φ
        var path = new HitPath();
        foreach (double h in new[] { -1.0, 1.0, 3.0 })
        {
            var w0 = f.ToWorld(0, 80, h);
            var w1 = f.ToWorld(0, 0, h);
            Assert.True(HitQuery.Trace(c, f, Attitude.Upright, w0, w1, [], path));
            var up = FirstHull(path);
            Assert.Equal(wl + h, up.Z, 3);
            Assert.True(HitQuery.Trace(c, f, heeled, w0, w1, [], path));
            var hp = FirstHull(path);
            Assert.InRange(hp.Z - (wl + (h + hp.Y * Math.Sin(phi)) / Math.Cos(phi)), -0.01, 0.01);
            Assert.True(hp.Z > up.Z + 2, $"heeled {hp.Z:F2} against upright {up.Z:F2}");
            // under the sea heeled: it came in through the water
            bool wet = false;
            foreach (ref readonly var x in path.Crossings)
                wet |= x.Kind == CrossingKind.WaterIn;
            Assert.Equal(h < 0, wet);
        }

        static P3 FirstHull(HitPath path)
        {
            foreach (ref readonly var x in path.Crossings)
                if (x.Kind == CrossingKind.HullIn)
                    return x.Point;
            throw new Xunit.Sdk.XunitException("no hull crossing");
        }
    }

    [Fact]
    public void A_miss_says_where_it_fell()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var f = Frame();
        var path = new HitPath();
        double halfBeam = 0;
        foreach (var p in c.Solids.Waterline)
            halfBeam = Math.Max(halfBeam, p.Y);
        // falling at 20° across the ship from port, landing 50 m short of it amidships
        double t = Math.Tan(double.DegreesToRadians(20));
        var land = f.ToWorld(0, -halfBeam - 50, 0);
        var w0 = f.ToWorld(0, -halfBeam - 50 - 100, 100 * t);
        var w1 = f.ToWorld(0, -halfBeam - 50 + 10, -10 * t);
        Assert.True(HitQuery.Trace(c, f, Attitude.Upright, w0, w1, [], path));
        Assert.False(path.HitShip);
        var fall = HitQuery.FallOf(c, path, HitQuery.WaterOf(c, Attitude.Upright));
        Assert.InRange(fall.Along, -50.1, -49.9);
        Assert.Equal(0, fall.Beside);
        Assert.True((HitQuery.ToWorld(c, f, Attitude.Upright, fall.Water) - land).Length < 0.01);

        // far from the ship: the broadphase turns it away
        Assert.False(HitQuery.Trace(c, f, Attitude.Upright, f.ToWorld(0, -2000, 300), f.ToWorld(0, -1500, -10), [], path));
        Assert.Equal(0, path.Count);
    }

    [Fact]
    public void Queries_allocate_nothing_and_paths_fit()
    {
        var f = Frame();
        var path = new HitPath();
        var classes = Fleet.Designs.Select(d => d.Class!).ToArray();
        void Work()
        {
            foreach (var c in classes)
            {
                var rng = RandomStream.FromSeed(5);
                for (int k = 0; k < 40; k++)
                {
                    var (p0, p1) = Shot(c, rng.Derive((ulong)k));
                    HitQuery.Trace(c, f, new Attitude(0.5, 1, 3), HitQuery.ToWorld(c, f, Attitude.Upright, p0), HitQuery.ToWorld(c, f, Attitude.Upright, p1), [],
                        path);
                    Assert.False(path.Overflowed);
                    HitQuery.FallOf(c, path, HitQuery.WaterOf(c, Attitude.Upright));
                }
            }
        }
        Work();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Work();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>A Jutland's worth: thousands of hits on a capital ship (fighting.md 4.10 expects ~8,000 heavy rounds and
    /// thousands of hits), timed. The bound is loose (Debug runs ~8x slower); the number is printed.</summary>
    [Fact]
    public void A_jutlands_worth_of_queries_timed()
    {
        var c = Fleet.Get("dreadnought").Class!;
        var f = Frame();
        var path = new HitPath();
        const int N = 8000;
        var segs = new (Vec3d, Vec3d)[N];
        var rng = RandomStream.FromSeed(9);
        for (int k = 0; k < N; k++)
        {
            var (p0, p1) = Shot(c, rng.Derive((ulong)k));
            segs[k] = (HitQuery.ToWorld(c, f, Attitude.Upright, p0), HitQuery.ToWorld(c, f, Attitude.Upright, p1));
        }
        HitQuery.Trace(c, f, Attitude.Upright, segs[0].Item1, segs[0].Item2, [], path);
        int hits = 0, crossings = 0;
        var sw = Stopwatch.StartNew();
        foreach (var (w0, w1) in segs)
        {
            HitQuery.Trace(c, f, Attitude.Upright, w0, w1, [], path);
            hits += path.HitShip ? 1 : 0;
            crossings += path.Count;
        }
        sw.Stop();
        double us = sw.Elapsed.TotalMicroseconds / N;
        output.WriteLine($"{N} queries at {c.Name}: {us:F1} µs each, {sw.ElapsedMilliseconds} ms in all; {hits} met the ship, " +
                         $"{(double)crossings / N:F1} crossings each");
        Assert.True(us < 5000, $"{us:F0} µs a query");
    }
}
