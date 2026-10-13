using System.Diagnostics;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The ballistic tables (docs/battle/fighting.md 4.2a): within a metre of direct integration at the fall, the
/// research's 16"/50 times of flight and angles of fall, and shells along a meridian and the equator landing where
/// direct integration on the turning sphere puts them.</summary>
public class BallisticsTests(ITestOutputHelper output)
{
    /// <summary>The research's 16"/50 Mk 7 (fire_control_ref.py GUNS): 1,225 kg at 762 m/s, 38,720 m at 45°.</summary>
    static readonly BallisticKey Mk7 = new(406, 1225, 762, FitFormFactor(406, 1225, 762, 45, 38720), 45);

    /// <summary>A WWI gun at its low elevations: the placeholder 12"/45 of the Great War.</summary>
    static readonly BallisticKey Twelve = new(305, 420.8, 775, 1.5, 18);

    static readonly Lazy<BallisticTable> Mk7Table = new(() => new BallisticTable(Mk7));
    static readonly Lazy<BallisticTable> TwelveTable = new(() => new BallisticTable(Twelve));

    /// <summary>A flight integrated directly in its plane from a muzzle h0 over the sea, finely: its fall's arc, time and
    /// angle.</summary>
    static (double S, double T, double FallDeg) Direct(in BallisticKey key, double elevationDeg, double speed, double h0, double dt = 0.002)
    {
        var st = Ballistics.Start(elevationDeg, key.MuzzleVelocityMs * speed);
        st.Y = h0;
        double k = Ballistics.DragK(key), t = 0;
        var prev = st;
        while (true)
        {
            var next = Ballistics.Step(st, k, dt);
            var (_, hn, _, _) = Ballistics.Polar(next);
            if (hn < 0 && t > 0)
            {
                // the last step, finely, to the sea
                var (sa, ha, _, _) = Ballistics.Polar(st);
                double lo = 0, hi = dt;
                for (int i = 0; i < 60; i++)
                {
                    double m = (lo + hi) / 2;
                    if (Ballistics.Polar(Ballistics.Step(st, k, m)).H > 0)
                        lo = m;
                    else
                        hi = m;
                }
                var at = Ballistics.Step(st, k, (lo + hi) / 2);
                var (s, _, sd, hd) = Ballistics.Polar(at);
                return (s, t + (lo + hi) / 2, double.RadiansToDegrees(Math.Atan2(-hd, sd)));
            }
            (prev, st, t) = (st, next, t + dt);
        }
    }

    /// <summary>The form factor that throws a gun's shell to its sourced range at its highest elevation (as
    /// fire_control_ref.py's Gun.fit), on the sphere.</summary>
    static double FitFormFactor(double mm, double kg, double ms, double elevDeg, double range)
    {
        double lo = 0.3, hi = 3.0;
        for (int i = 0; i < 30; i++)
        {
            double mid = (lo + hi) / 2;
            if (Direct(new BallisticKey(mm, kg, ms, mid, elevDeg), elevDeg, 1, 0, 0.05).S > range)
                lo = mid;
            else
                hi = mid;
        }
        return (lo + hi) / 2;
    }

    [Fact]
    public void The_table_is_within_a_metre_of_direct_integration_at_the_fall()
    {
        var rng = RandomStream.FromSeed(4);
        double worst = 0, worstDense = 0, worstH0 = 0;
        foreach (var (key, table) in new[] { (Mk7, Mk7Table.Value), (Twelve, TwelveTable.Value) })
            for (int k = 0; k < 40; k++)
            {
                double e = 0.5 + (key.MaxElevationDeg - 1) * rng.NextDouble(), v = 0.97 + 0.06 * rng.NextDouble();
                var (s, t, _) = Direct(key, e, v, 0);
                var f = table.Fall(e, 0, v);
                worst = Math.Max(worst, Math.Abs(f.Range - s));
                Assert.InRange(f.Tof - t, -0.01, 0.01);
                // in other air: the same launch with the drag scaled, against the table's density dimension
                double dens = 0.96 + 0.08 * rng.NextDouble();
                var (sd, _, _) = Direct(key with { FormFactor = key.FormFactor * dens }, e, v, 0);
                double td = table.Descends(e, v, 0, 0) is var _ ? DescendsIn(table, e, v, dens) : 0;
                double sTable = table.At(e, v, dens, td).S;
                worstDense = Math.Max(worstDense, Math.Abs(sTable - sd));
                // from a muzzle 5-25 m up: the model (a flight from the sea in air thinner by the muzzle's height)
                // against a true flight from up there
                double h0 = 5 + 20 * rng.NextDouble();
                var (s0, _, _) = Direct(key, e, v, h0);
                worstH0 = Math.Max(worstH0, Math.Abs(table.Fall(e, h0, v).Range - s0));
            }
        output.WriteLine($"the table against direct integration at the fall: worst {worst:F3} m, {worstDense:F3} m in other air; " +
                         $"the muzzle-height model {worstH0:F3} m from a true flight from 5-25 m up");
        Assert.True(worst < 1, $"{worst:F2} m");
        Assert.True(worstDense < 1, $"{worstDense:F2} m in other air");
        Assert.True(worstH0 < 3, $"{worstH0:F2} m from a muzzle up there");
    }

    /// <summary>When the table's flight in air of a density comes down to the sea.</summary>
    static double DescendsIn(BallisticTable t, double e, double v, double density)
    {
        double lo = 0, hi = 0;
        for (double tt = 0.5; tt < 400; tt += 0.5)
        {
            var pt = t.At(e, v, density, tt);
            double h = pt.H, hd = pt.HDot;
            if (hd < 0 && h <= 0)
            {
                (lo, hi) = (tt - 0.5, tt);
                break;
            }
        }
        for (int i = 0; i < 60; i++)
        {
            double m = (lo + hi) / 2;
            double h = t.At(e, v, density, m).H;
            if (h > 0)
                lo = m;
            else
                hi = m;
        }
        return (lo + hi) / 2;
    }

    [Fact]
    public void The_research_16in_50_flies_as_sourced()
    {
        // fire-control-research.md §5.1 (navweaps): times of flight at 10-30 kyd, angles of fall at 10-30 kyd
        var t = Mk7Table.Value;
        output.WriteLine($"16\"/50 Mk 7 form factor on the sphere {Mk7.FormFactor:F3}; table {t.Bytes / 1024} KB");
        foreach (var (kyd, tof, fall) in new[] { (10, 13.2, 5.0), (15, 21.0, double.NaN), (20, 29.6, 14.9), (25, 39.3, double.NaN), (30, 50.3, 28.3) })
        {
            double range = kyd * 914.4, e = t.ElevationFor(range, 0);
            var f = t.Fall(e, 0);
            output.WriteLine($"{kyd} kyd: elevation {e:F2}°, time of flight {f.Tof:F1} s (sourced {tof}), fall {f.FallDeg:F1}° (sourced {fall}), " +
                             $"striking {f.StrikeMs:F0} m/s");
            // the research's own model: times within a few per cent, falls within about 1° (fire-control-research.md §5.1)
            Assert.InRange(f.Tof / tof, 0.96, 1.04);
            if (!double.IsNaN(fall))
                Assert.InRange(f.FallDeg - fall, -1, 1);
        }
    }

    [Fact]
    public void Shells_land_where_the_turning_sphere_puts_them()
    {
        var t = Mk7Table.Value;
        foreach (var (name, lat, lon, heading) in new[] { ("north along a meridian", 50.0, 0.0, 0.0), ("east along the equator", 0.0, 30.0, 90.0),
                     ("west at 60° N", 60.0, -20.0, 270.0) })
        {
            double h0 = 12, e = 15;
            var up = Earth.Up(lat, lon);
            var muzzle = up * (Earth.Radius + h0);
            var fwd = Earth.Forward(muzzle, heading);
            // direct, in the Earth's own turning frame: gravity, drag on the air's speed (the air turns with it), Coriolis
            double k = Ballistics.DragK(Mk7), dt = 0.002;
            var p = muzzle;
            var v = (fwd * Math.Cos(double.DegreesToRadians(e)) + up * Math.Sin(double.DegreesToRadians(e))) * Mk7.MuzzleVelocityMs;
            double time = 0;
            (Vec3d, Vec3d) Rate(Vec3d x, Vec3d u)
            {
                double r = x.Length, sp = u.Length;
                var (rho, a) = Ballistics.Air(r - Earth.Radius);
                var acc = x * (-Ballistics.G * Earth.Radius * Earth.Radius / (r * r * r)) - u * (k * rho * Ballistics.Cd(sp / a) * sp)
                          - Vec3d.Cross(Ballistics.OmegaVector, u) * 2;
                return (u, acc);
            }
            while (true)
            {
                var (a1, b1) = Rate(p, v);
                var (a2, b2) = Rate(p + a1 * (dt / 2), v + b1 * (dt / 2));
                var (a3, b3) = Rate(p + a2 * (dt / 2), v + b2 * (dt / 2));
                var (a4, b4) = Rate(p + a3 * dt, v + b3 * dt);
                var pn = p + (a1 + a2 * 2 + a3 * 2 + a4) * (dt / 6);
                var vn = v + (b1 + b2 * 2 + b3 * 2 + b4) * (dt / 6);
                if (pn.Length < Earth.Radius && time > 1)
                {
                    double f = (p.Length - Earth.Radius) / (p.Length - pn.Length);
                    p += (pn - p) * f;
                    time += dt * f;
                    break;
                }
                (p, v, time) = (pn, vn, time + dt);
            }
            double tt = t.Reaches(muzzle, fwd, e, 1, 0);
            var q = t.Position(muzzle, fwd, e, 1, tt, out _);
            double miss = Earth.Distance(q, p);
            // how far the Earth's turning moved it: against the same flight on a still sphere
            double still = t.Descends(e, 1, h0, 0);
            double s = t.At(e, 1, BallisticTable.DensityAt(h0), still).S;
            var noTurn = Earth.Position(0, 0) * 0 + (up * Math.Cos(s / Earth.Radius) + fwd * Math.Sin(s / Earth.Radius)) * Earth.Radius;
            output.WriteLine($"{name}: lands {Earth.Distance(muzzle, p) / 914.4:F2} kyd out, moved {Earth.Distance(noTurn, p):F1} m by the turning; " +
                             $"the table's fall {miss:F2} m from the turning sphere's, time {tt:F2} s against {time:F2} s");
            // the first order: the across-track part is exact (its damping is the drag's across the track); along it, where
            // drag brakes harder and a lifted shell meets thinner air, it's within a few per cent of the turning's effect
            Assert.True(miss < 1 + 0.04 * Earth.Distance(noTurn, p), $"{name}: {miss:F2} m");
            Assert.InRange(tt - time, -0.02, 0.02);
        }
    }

    [Fact]
    public void A_table_builds_quickly_and_a_lookup_is_cheap()
    {
        var sw = Stopwatch.StartNew();
        var t = new BallisticTable(Twelve);
        double build = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        double sink = 0;
        for (int i = 0; i < 100_000; i++)
        {
            double s = t.At(5 + i % 10, 1, 1, (i % 300) * 0.1).S;
            sink += s;
        }
        double ns = sw.Elapsed.TotalNanoseconds / 100_000;
        // fire control's question (4.2b), many times a second: the elevation for a range
        sw.Restart();
        for (int i = 0; i < 10_000; i++)
            sink += t.ElevationFor(3000 + i % 13_000, 5 + i % 20, 0.98 + i % 5 * 0.01);
        double elevationNs = sw.Elapsed.TotalNanoseconds / 10_000;
        output.WriteLine($"12\"/45 table (18°): built in {build:F0} ms, {t.Bytes / 1024} KB; a lookup {ns:F0} ns, the elevation for a range {elevationNs:F0} ns");
        Assert.NotEqual(0, sink);
        Assert.True(elevationNs < 50_000, $"{elevationNs:F0} ns for an elevation");
        long before = GC.GetAllocatedBytesForCurrentThread();
        t.At(7, 1.01, 0.999, 12.3);
        t.Fall(7, 10);
        t.ElevationFor(12_000, 15, 1.01);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void The_range_table_answers_as_the_flights_do()
    {
        // the range table's falls against the flights' own (the table's trajectory brought down to the sea, finely), and
        // the elevation it gives throws to the range asked. From 2 km: closer, a shell all but level falls as √h0 and the
        // table's 5 m heights are coarse for it (under 2 km, up to ~50 m in range, which at its fall of under 1° is under
        // half a metre of height at a target)
        var t = TwelveTable.Value;
        var rng = RandomStream.FromSeed(9);
        double worst = 0, worstTof = 0, worstBack = 0;
        for (int k = 0, n = 0; n < 200; k++)
        {
            double e = -1 + 18.5 * rng.NextDouble(), h0 = 30 * rng.NextDouble(), v = 0.97 + 0.06 * rng.NextDouble();
            double td = t.Descends(e, v, h0, 0);
            var p = t.At(e, v, BallisticTable.DensityAt(h0), td);
            if (p.S < 2000)
                continue;
            n++;
            var f = t.Fall(e, h0, v);
            worst = Math.Max(worst, Math.Abs(f.Range - p.S));
            worstTof = Math.Max(worstTof, Math.Abs(f.Tof - td));
            double range = 2000 + 16_000 * rng.NextDouble();
            double el = t.ElevationFor(range, h0, v);
            if (!double.IsNaN(el))
                worstBack = Math.Max(worstBack, Math.Abs(t.Fall(el, h0, v).Range - range));
        }
        output.WriteLine($"the range table against the flights: worst {worst:F3} m, {worstTof * 1000:F1} ms; an elevation's fall {worstBack:F4} m off its range");
        Assert.True(worst < 1, $"{worst:F2} m");
        Assert.True(worstTof < 0.01, $"{worstTof:F3} s");
        Assert.True(worstBack < 0.01, $"{worstBack:F3} m");
    }
}
