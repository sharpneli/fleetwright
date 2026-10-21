using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>The smoke field's model (docs/battle/smoke.md Z0): the erf, the line integral against quadrature (sloped,
/// curved, near the sea), the research's single trail, the column, merging, Taylor's and Briggs' limits, the air's frame,
/// and the funnels' facts on the shipped designs.</summary>
public class SmokeModelTests(ITestOutputHelper output)
{
    static readonly SmokeAir Neutral = SmokeAir.Of(new WeatherHere(25_000, 225, 8));
    static readonly Vec3d Here = Earth.Position(56.5, 4);
    const double K = 7.0 / 6;

    static ShipClass Class(string name) => Fleet.Get(name).Class!;

    /// <summary>A puff of given σs and A at a height, <paramref name="east"/> and <paramref name="north"/> m from here.</summary>
    static PuffState Puff(double east, double north, double z, double sh, double sz, double a) =>
        new((Here + Earth.East(Here) * east + Earth.North(Here) * north).Normalized() * Earth.Radius, z, sh * sh, sz * sz, a, 0);

    /// <summary>The line's τ by brute force: the puff's and its image's density summed along the line's own curve.</summary>
    static double Quadrature(in SmokeLine l, in PuffState p, double toM, int steps = 200_000)
    {
        double sum = 0, ds = l.Length / steps, norm = p.Area / (Math.Pow(2 * Math.PI, 1.5) * p.SigmaH2 * Math.Sqrt(p.SigmaZ2));
        for (int i = 0; i < steps; i++)
        {
            double s = (i + 0.5) * ds;
            var at = l.From + l.Along * s;
            double h = l.HeightAt(s, toM);
            var rel = p.Centre - at;
            double r2 = rel.LengthSquared - Math.Pow(Vec3d.Dot(rel, at.Normalized()), 2);
            double lo = h - p.Z, hi = h + p.Z;
            sum += norm * Math.Exp(-r2 / (2 * p.SigmaH2)) * (Math.Exp(-lo * lo / (2 * p.SigmaZ2)) + Math.Exp(-hi * hi / (2 * p.SigmaZ2))) * ds;
        }
        return sum;
    }

    [Fact]
    public void Erf_matches_the_tables()
    {
        (double X, double Erf)[] table = [(0, 0), (0.1, 0.1124629160), (0.5, 0.5204998778), (1, 0.8427007929), (1.5, 0.9661051465),
            (2, 0.9953222650), (3, 0.9999779095)];
        foreach (var (x, e) in table)
        {
            Assert.InRange(SmokeModel.Erf(x) - e, -2e-7, 2e-7);
            Assert.Equal(-SmokeModel.Erf(x), SmokeModel.Erf(-x), 12);
        }
        Assert.Equal(1, SmokeModel.Erf(10));
        Assert.Equal(-1, SmokeModel.Erf(-10));
    }

    [Theory]
    [InlineData(30, 30, 0, 40, 30, 20)]       // level, through the puff
    [InlineData(35, 12, 15, 40, 60, 25)]      // sloping down, passing to one side
    [InlineData(20, 20, -40, 25, 120, 60)]    // a big old puff low on the sea: the image counts
    [InlineData(30, 10, 0, 6, 15, 8)]         // a small young puff near the line's height
    public void A_line_through_a_puff_is_the_quadrature(double eyeM, double toM, double missM, double z, double sh, double sz)
    {
        var p = Puff(4000, missM, z, sh, sz, 500);
        var l = new SmokeLine(Here, Here + Earth.East(Here) * 9000, eyeM, toM, K);
        double q = Quadrature(l, p, toM), t = SmokeModel.TauLine(l, p);
        Assert.True(q > 1e-4, $"the case sees smoke ({q})");
        Assert.InRange(t / q, 0.995, 1.005);
    }

    [Fact]
    public void The_curvature_lowers_a_long_line_through_the_smoke()
    {
        // 30 km at 25 m both ends: mid-line the curve puts the line ~16 m lower, into a puff at 10 m that a straight line
        // would skim over
        var p = Puff(15_000, 0, 10, 60, 6, 2000);
        var l = new SmokeLine(Here, Here + Earth.East(Here) * 30_000, 25, 25, K);
        Assert.InRange(l.HeightAt(15_000, 25), 8, 11);
        double t = SmokeModel.TauLine(l, p);
        Assert.InRange(t / Quadrature(l, p, 25), 0.99, 1.01);
        var flat = Puff(15_000, 0, 10 + 25 - l.HeightAt(15_000, 25), 60, 6, 2000);
        Assert.True(SmokeModel.TauLine(l, flat) < 0.1 * t, "without the curve the line clears it");
    }

    [Fact]
    public void Bands_agree_with_their_own_lines()
    {
        var l = new SmokeLine(Here, Here + Earth.East(Here) * 12_000, 30, 20, K);
        double[] bands = [5, 15, 30];
        Span<double> tau = stackalloc double[3];
        double[] each = new double[3];
        for (int i = 0; i < 40; i++)
        {
            var p = Puff(300 * i, 25 * (i % 7) - 75, 10 + i, 20 + 3 * i, 10 + 2 * i, 300);
            SmokeModel.TauBands(l, p, bands, tau);
            for (int j = 0; j < 3; j++)
                each[j] += SmokeModel.TauLine(l, p, bands[j]);
        }
        for (int j = 0; j < 3; j++)
            Assert.InRange(tau[j] / each[j], 0.995, 1.005);
    }

    /// <summary>A straight trail of one age, its puffs every σ_h: the research's line source.</summary>
    static (double Column, double Across, double Along) Trail(double lambda, double sh, double sz, double z = 1000)
    {
        double spacing = sh, a = lambda * spacing;
        int n = (int)(40 * sh / spacing);
        var east = Earth.East(Here);
        var cross = new SmokeLine(Here + Earth.North(Here) * 3000, Here - Earth.North(Here) * 3000, z, z, 1e9);
        var axis = new SmokeLine(Here - east * 250, Here + east * 250, z, z, 1e9);
        double col = 0, across = 0, along = 0;
        for (int i = 0; i < n; i++)
        {
            var p = Puff((i - n / 2) * spacing, 0, z, sh, sz, a);
            col += SmokeModel.TauColumn(p, Here);
            across += SmokeModel.TauLine(cross, p);
            along += SmokeModel.TauLine(axis, p);
        }
        return (col, across, along);
    }

    [Theory]
    [InlineData(15, 10, 4.0, 0.003, 80)]
    [InlineData(30, 20, 2.0, 0.05, 20)]
    [InlineData(80, 40, 0.75, 0.22, 3.7)]
    public void The_researchs_single_trail(double sh, double sz, double column, double across, double along)
    {
        // research/vfx/unified-smoke-system.md "Calibrating funnel smoke": Ȧ = 1,500 m²/s at 10 m/s, the sea far below.
        // The table's figures are the line source's closed forms, rounded; the puffs laid every σ give them back
        double lambda = 1500 / 10.0;
        var (c, x, a) = Trail(lambda, sh, sz);
        Assert.InRange(c / (lambda / (Math.Sqrt(2 * Math.PI) * sh)), 0.995, 1.005);
        Assert.InRange(x / (lambda / (Math.Sqrt(2 * Math.PI) * sz)), 0.995, 1.005);
        Assert.InRange(a / (lambda * 500 / (2 * Math.PI * sh * sz)), 0.98, 1.02);
        Assert.InRange(c / column, 0.97, 1.03);
        Assert.InRange(Math.Exp(-x) / across, 0.8, 1.25);
        Assert.InRange(a / along, 0.97, 1.03);
    }

    [Fact]
    public void A_column_holds_the_puffs_smoke_once()
    {
        // on the sea, half the Gaussian is under it: the image folds it back, and the column's τ integrates to A
        var p = Puff(0, 0, 0, 50, 40, 1000);
        double sum = 0, step = 5;
        for (double x = -300; x <= 300; x += step)
            for (double y = -300; y <= 300; y += step)
                sum += SmokeModel.TauColumn(p, Here + Earth.East(Here) * x + Earth.North(Here) * y) * step * step;
        Assert.InRange(sum / 1000, 0.99, 1.01);
        // and a level line at sea level sees twice what it would without the sea
        var l = new SmokeLine(Here - Earth.East(Here) * 2000, Here + Earth.East(Here) * 2000, 0, 0, 1e9);
        Assert.InRange(SmokeModel.TauLine(l, p) / (1000 / (2 * Math.PI * 50 * 40)) / 2, 0.99, 1.01);
    }

    [Fact]
    public void Taylors_spread_grows_linearly_then_diffusively()
    {
        double sigma = 0.5, tl = 20;
        Assert.InRange(SmokeModel.Taylor(0.1, sigma, tl) / Math.Pow(sigma * 0.1, 2), 0.99, 1.0);
        double late = 20_000, k = sigma * sigma * tl;
        Assert.InRange(SmokeModel.Taylor(late, sigma, tl) / (2 * k * (late - tl)), 0.999, 1.001);
        Assert.Equal(0, SmokeModel.Taylor(0, sigma, tl));
        // neutral air at 8 m/s: the research's K_h ≈ 5 m²/s
        Assert.InRange(Neutral.SigmaV * Neutral.SigmaV * Neutral.LagrangianH, 4, 7);
    }

    [Fact]
    public void Stable_air_keeps_smoke_thin_and_unstable_air_spreads_it()
    {
        var stable = SmokeAir.Of(new WeatherHere(25_000, 225, 4, 3));
        var unstable = SmokeAir.Of(new WeatherHere(25_000, 225, 4, -3));
        var neutral = SmokeAir.Of(new WeatherHere(25_000, 225, 4));
        Assert.True(stable.Kz(50) < neutral.Kz(50) && neutral.Kz(50) < unstable.Kz(50));
        Assert.True(stable.Stability(50) > 0 && neutral.Stability(50) == 0 && unstable.Stability(50) == 0);
        Assert.True(unstable.SigmaV > neutral.SigmaV);
    }

    [Fact]
    public void Briggs_rise_and_downwash()
    {
        // F = 100 m⁴/s³ in a 10 m/s wind: the neutral final rise 38.7·100^0.6/10 ≈ 61 m, at x_f = 119 F^0.4 ≈ 750 m (75 s)
        var (rise, s) = SmokeModel.Briggs(100, 10, 0);
        Assert.InRange(rise, 60, 62);
        Assert.InRange(s, 65, 85);
        Assert.InRange(SmokeModel.RiseShare(1e-3) / Math.Pow(1e-3, 2.0 / 3), 0.99, 1.01);
        Assert.InRange(SmokeModel.RiseShare(50), 0.999, 1);
        // stable air caps it: 2.6 (F/(U s))^⅓ at s = 1e-3, ≈ 56 m
        Assert.InRange(SmokeModel.Briggs(100, 10, 1e-3).RiseM, 55, 57);
        // twice the wind, half the rise
        Assert.InRange(SmokeModel.Briggs(100, 20, 0).RiseM / rise, 0.499, 0.501);
        Assert.Equal(0, SmokeModel.Downwash(15, 10, 1.5));
        Assert.InRange(SmokeModel.Downwash(5, 10, 1.5), 2.99, 3.01);
    }

    [Fact]
    public void A_merge_keeps_the_smoke_its_centre_and_its_moments()
    {
        var frame = AirFrame.Identity;
        double now = 600;
        var a = SmokeModel.Born(Here, 100, frame, 20, 40, 60, 10, 8, 300, 0, SmokeTint.Coal);
        var b = SmokeModel.Born(Here + Earth.East(Here) * 30, 160, frame, 20, 40, 60, 10, 8, 200, 0, SmokeTint.Coal);
        var sa = SmokeModel.State(a, now, frame, Neutral);
        var sb = SmokeModel.State(b, now, frame, Neutral);
        var m = SmokeModel.Merge(a, sa, b, sb, now, frame, Neutral);
        var sm = SmokeModel.State(m, now, frame, Neutral);
        Assert.InRange(sm.Area, 499.9, 500.1);
        var centre = (sa.Centre * 0.6 + sb.Centre * 0.4).Normalized() * Earth.Radius;
        Assert.True((sm.Centre - centre).Length < 0.01);
        double h2 = 0.6 * (sa.SigmaH2 + 0.5 * (sa.Centre - centre).LengthSquared) + 0.4 * (sb.SigmaH2 + 0.5 * (sb.Centre - centre).LengthSquared);
        Assert.InRange(sm.SigmaH2 / h2, 0.999, 1.001);
        double z = 0.6 * sa.Z + 0.4 * sb.Z, z2 = 0.6 * (sa.SigmaZ2 + Math.Pow(sa.Z - z, 2)) + 0.4 * (sb.SigmaZ2 + Math.Pow(sb.Z - z, 2));
        Assert.InRange(sm.Z - z, -0.01, 0.01);
        Assert.InRange(sm.SigmaZ2 / z2, 0.999, 1.001);
        // and it carries on spreading
        Assert.True(SmokeModel.State(m, now + 600, frame, Neutral).SigmaH2 > sm.SigmaH2 * 1.2);
    }

    [Fact]
    public void The_air_carries_a_puff_with_the_wind()
    {
        var frame = AirFrame.Identity;
        var p = SmokeModel.Born(Here, 0, frame, 20, 0, 1, 10, 10, 100, 0, SmokeTint.Oil);
        for (int i = 0; i < 3600; i++)
            frame.Advance(Here, 45, 10, 1);   // toward the north-east, 10 m/s, an hour
        var c = SmokeModel.State(p, 3600, frame, Neutral).Centre;
        Assert.InRange(Earth.Distance(Here, c), 35_990, 36_010);
        Assert.InRange(Earth.Bearing(Here, c), 44.5, 45.5);
        var w = Earth.Position(57, 5);
        Assert.True((frame.ToWorld(frame.ToAir(w)) - w).Length < 1e-3);
    }

    [Fact]
    public void The_shipped_designs_funnels_share_their_plant()
    {
        foreach (var b in Fleet.Designs)
        {
            var c = b.Class!;
            if (c.Funnels.Length == 0)
                continue;
            double share = 0;
            foreach (ref readonly var f in c.Funnels)
            {
                share += f.Share;
                Assert.True(f.ExitAreaM2 > 0, $"{c}: a funnel with no exit");
            }
            Assert.InRange(share, 0.999, 1.001);
            Assert.True(c.Smoke.M2PerKg > 0 && c.Smoke.GasM3PerKg > 0 && c.Smoke.GasTempK > 300, $"{c}: no smoke facts");
        }
    }

    [Fact]
    public void More_power_more_smoke_and_coal_smokes_more_than_oil()
    {
        var coal = Class("dreadnought");
        var oil = Class("super_dreadnought");
        Assert.Equal(Fuel.Coal, coal.Smoke.Fuel);
        Assert.Equal(Fuel.Oil, oil.Smoke.Fuel);
        double last = 0;
        foreach (double kn in new[] { 8.0, 12, 16, 20 })
        {
            double rate = SmokeSources.Funnel(coal, 0, kn, 10, Neutral).RateM2s;
            Assert.True(rate > last);
            last = rate;
        }
        double Ship(ShipClass c) => Enumerable.Range(0, c.Funnels.Length).Sum(i => SmokeSources.Funnel(c, i, c.Motion.MaxSpeedKn, 10, Neutral).RateM2s);
        Assert.True(Ship(coal) > 2 * Ship(oil), $"coal {Ship(coal):0} against oil {Ship(oil):0} m²/s");
        // a cruising ship's slow gas comes down into the funnel's lee
        Assert.True(SmokeSources.Funnel(coal, 0, 10, 10, Neutral).DownwashM > SmokeSources.Funnel(coal, 0, coal.Motion.MaxSpeedKn, 10, Neutral).DownwashM);
    }

    [Fact]
    public void The_model_allocates_nothing()
    {
        var frame = AirFrame.Identity;
        var p = SmokeModel.Born(Here, 0, frame, 20, 40, 60, 10, 8, 300, 0, SmokeTint.Coal);
        var l = new SmokeLine(Here, Here + Earth.East(Here) * 9000, 30, 20, K);
        Span<double> tau = stackalloc double[3];
        ReadOnlySpan<double> bands = Bands;
        var c = Class("dreadnought");
        double sink = SmokeModel.TauLine(l, SmokeModel.State(p, 100, frame, Neutral)) + SmokeSources.Funnel(c, 0, 15, 10, Neutral).RateM2s;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            var s = SmokeModel.State(p, 100 + i, frame, Neutral);
            sink += SmokeModel.TauLine(l, s) + SmokeModel.TauColumn(s, Here);
            SmokeModel.TauBands(l, s, bands, tau);
            sink += SmokeModel.State(SmokeModel.Merge(p, s, p, s, 100 + i, frame, Neutral), 100 + i, frame, Neutral).Area;
            sink += SmokeSources.Funnel(c, 0, 15, 10, Neutral).RateM2s;
            frame.Advance(Here, 45, 10, 1);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.NotEqual(0, sink);
    }

    static readonly double[] Bands = [5, 15, 30];

    [Fact]
    public void A_puff_on_a_line_costs_tens_of_nanoseconds()
    {
        // gate 1's base number (docs/battle/smoke.md Z2): puffs near a 12 km line, a third of them culled cheaply
        var l = new SmokeLine(Here, Here + Earth.East(Here) * 12_000, 30, 20, K);
        var puffs = new PuffState[3000];
        for (int i = 0; i < puffs.Length; i++)
            puffs[i] = Puff(4 * i, 40 * (i % 9) - 160, 20 + i % 50, 20 + i % 80, 15 + i % 40, 300);
        double sink = 0;
        for (int i = 0; i < puffs.Length; i++)
            sink += SmokeModel.TauLine(l, puffs[i]);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int n = 0;
        while (watch.ElapsedMilliseconds < 200)
        {
            for (int i = 0; i < puffs.Length; i++)
                sink += SmokeModel.TauLine(l, puffs[i]);
            n += puffs.Length;
        }
        double ns = watch.Elapsed.TotalMilliseconds * 1e6 / n;
        output.WriteLine($"{ns:F1} ns a puff on a line (τ {sink:G3})");
        Assert.True(ns < 2000, $"{ns:F0} ns a puff");
    }
}
