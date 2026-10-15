using Fleetwright.Map.Wakes;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Ships;
using Xunit.Abstractions;

namespace Fleetwright.Tests;

/// <summary>The wake bake (docs/visuals/wakes.md K1): the stem's crest up and the waves astern, Kelvin's angle at a low
/// Froude number narrowing at a high one (research §2.2), the crest at Noblesse's height, the same bake twice.</summary>
public class WakeBakeTests(ITestOutputHelper output)
{
    static readonly Lazy<WakeHull> Destroyer = new(() => Hull("destroyer"));
    static readonly Lazy<WakeHull> Battleship = new(() => Hull("dreadnought"));
    static readonly WakeBake.Scratch Scratch = new();
    static readonly object Lock = new();

    static WakeHull Hull(string name) =>
        WakeHull.Of(ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(Paths.Shipgen("designs", name + ".json")))));

    static WakeField Bake(WakeHull h, double froude)
    {
        lock (Lock)
            return WakeBake.Bake(h, froude * Math.Sqrt(9.81 * h.LengthM), Scratch);
    }

    /// <summary>The wake's half-angles astern, degrees, as slopes fitted along the columns 1.5 to 3 lengths behind the
    /// stem (a slope, so where the V's apex lies doesn't matter), on the starboard side outboard of the beam: where the
    /// water is highest or lowest (the visible angle, which narrows at high Froude), and the outer envelope, the
    /// furthest out that the waves reach a fifth of that (Kelvin's 19.47° at any speed).</summary>
    static (double Peak, double Envelope) Angles(WakeField f, WakeHull h)
    {
        double stem = h.X1, L = h.LengthM;
        var (d, yPeak, yEdge) = (new List<double>(), new List<double>(), new List<double>());
        for (int q = 0; q < WakeField.Width; q++)
        {
            double x = f.X0 + (q + 0.5) * f.TexelX, back = stem - x;
            if (back < 1.5 * L || back > 3 * L)
                continue;
            double best = 0, at = 0, edge = 0;
            for (int r = 0; r < WakeField.Height; r++)
            {
                double y = (r + 0.5) * f.TexelY, e = Math.Abs(f.Eta[r * WakeField.Width + q]);
                if (y >= h.BeamM && e > best)
                    (best, at) = (e, y);
            }
            for (int r = 0; r < WakeField.Height; r++)
            {
                double y = (r + 0.5) * f.TexelY;
                if (Math.Abs(f.Eta[r * WakeField.Width + q]) > 0.2 * best)
                    edge = y;
            }
            if (best > 0)
            {
                d.Add(back);
                yPeak.Add(at);
                yEdge.Add(edge);
            }
        }
        return (Math.Atan(Slope(d, yPeak)) * 180 / Math.PI, Math.Atan(Slope(d, yEdge)) * 180 / Math.PI);
    }

    static double Slope(List<double> x, List<double> y)
    {
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0;
        for (int i = 0; i < x.Count; i++)
            (sxy, sxx) = (sxy + (x[i] - mx) * (y[i] - my), sxx + (x[i] - mx) * (x[i] - mx));
        return sxy / sxx;
    }

    static (double Ahead, double Astern) Energy(WakeField f, WakeHull h)
    {
        double ahead = 0, astern = 0;
        int na = 0, nb = 0;
        for (int r = 0; r < WakeField.Height; r++)
            for (int q = 0; q < WakeField.Width; q++)
            {
                double x = f.X0 + (q + 0.5) * f.TexelX, e = f.Eta[r * WakeField.Width + q];
                if (x > h.X1 + 0.1 * h.LengthM)
                    (ahead, na) = (ahead + e * e, na + 1);
                else if (x < h.X0)
                    (astern, nb) = (astern + e * e, nb + 1);
            }
        return (ahead / Math.Max(na, 1), astern / Math.Max(nb, 1));
    }

    [Fact]
    public void TheHullsAreThoseOfTheHitboxModel()
    {
        var d = Destroyer.Value;
        output.WriteLine($"destroyer: L {d.LengthM:0.0} m, B {d.BeamM:0.00} m, T {d.DraughtM:0.00} m, entrance {d.EntranceDeg:0.0}°");
        var b = Battleship.Value;
        output.WriteLine($"dreadnought: L {b.LengthM:0.0} m, B {b.BeamM:0.00} m, T {b.DraughtM:0.00} m, entrance {b.EntranceDeg:0.0}°");
        Assert.InRange(d.LengthM, 60, 160);
        Assert.InRange(d.LengthM / d.BeamM, 7, 14);
        Assert.InRange(b.LengthM / b.BeamM, 5, 9);
        Assert.InRange(d.EntranceDeg, 6, 20);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.4)]
    [InlineData(0.6)]
    public void TheStemCrestIsUpAndTheWavesAstern(double froude)
    {
        var h = Destroyer.Value;
        var f = Bake(h, froude);
        var (ahead, astern) = Energy(f, h);
        double stemEta = 0;
        for (int r = 0; r < WakeField.Height; r++)
            for (int q = 0; q < WakeField.Width; q++)
            {
                double x = f.X0 + (q + 0.5) * f.TexelX;
                if (Math.Abs(x - h.X1) < 0.05 * h.LengthM)
                    stemEta = Math.Max(stemEta, f.Eta[r * WakeField.Width + q]);
            }
        output.WriteLine($"Fr {froude}: {f.SpeedMs / 0.5144:0.0} kn, Zb {f.Zb:0.00} m, stem crest {stemEta:0.00} m, mean η² ahead {ahead:0.0000} astern {astern:0.0000}; grid {f.GridX}x{f.GridY} at {f.GridDx:0.00} m, {f.BakeMs:0} ms");
        Assert.True(stemEta > 0.5 * f.Zb, "the stem's crest is up");
        Assert.True(astern > 20 * ahead, "the waves trail");
    }

    [Fact]
    public void KelvinsAngleAndItsNarrowingAtHighFroude()
    {
        var h = Destroyer.Value;
        var low = Angles(Bake(h, 0.25), h);
        var mid = Angles(Bake(h, 0.4), h);
        var high = Angles(Bake(h, 0.65), h);
        output.WriteLine($"the wake's angles (peak, envelope): Fr 0.25 {low.Peak:0.0}° {low.Envelope:0.0}°, 0.4 {mid.Peak:0.0}° {mid.Envelope:0.0}°, 0.65 {high.Peak:0.0}° {high.Envelope:0.0}°");
        Assert.InRange(low.Envelope, 15, 24);   // Kelvin's 19.47°
        Assert.True(high.Peak < low.Peak - 2, "the visible angle narrows at high Froude (Rabaud & Moisy; Darmon et al.)");
        Assert.InRange(high.Peak, 4, 15);       // about 0.16 / Fr rad, 14° at 0.65 (research §2.2)
    }

    [Fact]
    public void TheCrestIsNoblessesHeight()
    {
        var h = Battleship.Value;
        var f = Bake(h, 0.3);
        double crest = f.Eta.Max();
        output.WriteLine($"dreadnought Fr 0.3: Zb {f.Zb:0.00} m, highest water {crest:0.00} m");
        Assert.InRange(f.Zb, 1, 8);
        Assert.InRange(crest, 0.9 * f.Zb, 1.6 * f.Zb);
    }

    [Fact]
    public void TheSameBakeTwiceIsTheSame()
    {
        var h = Destroyer.Value;
        var a = Bake(h, 0.4).Grad2.ToArray();
        var b = Bake(h, 0.4).Grad2;
        Assert.Equal(a, b);
    }

    [Fact]
    public void BucketsStepByTheirRatioAndBracketTheSpeed()
    {
        for (double kn = 3; kn < 45; kn += 0.37)
        {
            double u = kn * WakeLibrary.KnotMs;
            var (b, toward) = WakeLibrary.BucketOf(u);
            Assert.InRange(toward, 0, 1);
            Assert.True(WakeLibrary.BucketSpeedMs(b) <= u + 1e-9 && u < WakeLibrary.BucketSpeedMs(b + 1) + 1e-9, $"{kn} kn in bucket {b}");
            Assert.Equal(WakeLibrary.BucketRatio, WakeLibrary.BucketSpeedMs(b + 1) / WakeLibrary.BucketSpeedMs(b), 9);
        }
        Assert.Equal((0, 0.0), WakeLibrary.BucketOf(1.0));   // under the first: the first, alone
        Assert.InRange(WakeLibrary.BucketOf(40 * WakeLibrary.KnotMs).Bucket, 30, 36);   // ~34 buckets to 40 kn
    }
}
