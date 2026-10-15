using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The sea's spectrum (docs/visuals/sea.md W1) against research/vfx/ocean-surface-research.md: the heights of
/// its Beaufort table, the realised variance of the uploaded bins against the spectrum's (a factor-2 error once hid
/// there), waves travelling downwind (the lab once had them going upwind), Cox and Munk's slopes, and phases that stay
/// when the state changes.</summary>
public class SeaSpectrumTests
{
    const int N = 128;

    static SeaState Wind(double ms, double fromDeg = 0, double fetchM = 150_000, double swellHs = 0) =>
        new(ms, fromDeg, fetchM, swellHs, 12, fromDeg + 50);

    // research §2.2: Bft 4 fully developed, Bft 5 and 8 fetch-limited at 150 km
    [Theory]
    [InlineData(7.9, 1.40, true)]
    [InlineData(10.7, 2.43, false)]
    [InlineData(20.7, 5.11, false)]
    public void Heights_follow_the_research_table(double windMs, double hs, bool developed)
    {
        var s = new SeaSpectrum(Wind(windMs));
        Assert.Equal(hs, s.HsTarget, hs * 0.04);
        Assert.Equal(developed, s.Developed);
    }

    [Theory]
    [InlineData(7.9, 1.0)]
    [InlineData(20.7, 1.0)]
    [InlineData(10.7, 0)]
    public void The_uploaded_bins_hold_the_spectrums_variance(double windMs, double swellHs)
    {
        var s = new SeaSpectrum(Wind(windMs, 250, swellHs: swellHs));
        var buf = new float[N * N * 4];
        double var = 0, realised = 0;
        for (int c = 0; c < SeaSpectrum.Cascades; c++)
        {
            var st = s.BuildH0(c, N, buf);
            var += st.Variance;
            for (int i = 0; i < N * N; i++)   // E|h0|² = P/2 for each half of the pair, so Σ|h0(k)|² + |h0(-k)|² ≈ ΣP
                realised += buf[i * 4] * buf[i * 4] + buf[i * 4 + 1] * buf[i * 4 + 1]
                    + buf[i * 4 + 2] * buf[i * 4 + 2] + buf[i * 4 + 3] * buf[i * 4 + 3];
        }
        Assert.Equal(s.HsTarget, 4 * Math.Sqrt(var), s.HsTarget * 0.1);   // the discrete spectrum covers the waves that matter
        Assert.Equal(var, realised, var * 0.15);                            // and the draws carry it
    }

    [Fact]
    public void Waves_travel_downwind()
    {
        // wind from the north: the wind sea's energy is in wavenumbers pointing south
        var s = new SeaSpectrum(Wind(10.7, fromDeg: 0));
        var buf = new float[N * N * 4];
        double dk = 2 * Math.PI / SeaSpectrum.TileM[1], south = 0, east = 0, all = 0;
        s.BuildH0(1, N, buf);
        for (int m = 0; m < N; m++)
            for (int j = 0; j < N; j++)
            {
                int i = (m * N + j) * 4;
                double e = buf[i] * buf[i] + buf[i + 1] * buf[i + 1];   // h0(k): the term that travels along +k
                double kx = (j < N / 2 ? j : j - N) * dk, ky = (m < N / 2 ? m : m - N) * dk, k = Math.Sqrt(kx * kx + ky * ky);
                if (k == 0)
                    continue;
                south += -ky / k * e;
                east += kx / k * e;
                all += e;
            }
        Assert.True(south / all > 0.6, $"south share {south / all:0.00}");
        Assert.True(Math.Abs(east / all) < 0.1, $"east share {east / all:0.00}");
        Assert.Equal(-Math.PI / 2, s.WindAngle, 1e-9);
    }

    [Fact]
    public void Cox_and_Munk_slopes_turn_with_the_wind()
    {
        double u = 10;
        var (x, y) = new SeaSpectrum(Wind(u, fromDeg: 0)).CoxMunkMss;   // along y: upwind
        Assert.Equal(0.003 + 0.00192 * u, x, 1e-9);
        Assert.Equal(0.00316 * u, y, 1e-9);
        var (x2, y2) = new SeaSpectrum(Wind(u, fromDeg: 270)).CoxMunkMss;   // along x
        Assert.Equal(0.00316 * u, x2, 1e-9);
        Assert.Equal(0.003 + 0.00192 * u, y2, 1e-9);
        Assert.Equal(0.003 + 0.00512 * u, x + y, 0.0005);   // the split's sum, 0.00508 U, against the fitted total
    }

    [Fact]
    public void A_new_state_keeps_the_phases_and_a_build_allocates_nothing()
    {
        var a = new float[N * N * 4];
        var b = new float[N * N * 4];
        new SeaSpectrum(Wind(7.9, 250)).BuildH0(1, N, a);
        var s = new SeaSpectrum(Wind(12, 250));
        s.BuildH0(1, N, b);
        int both = 0;
        for (int i = 0; i < N * N; i++)
        {
            double ra = a[i * 4], ia = a[i * 4 + 1], rb = b[i * 4], ib = b[i * 4 + 1];
            if (ra * ra + ia * ia < 1e-20 || rb * rb + ib * ib < 1e-20)
                continue;
            both++;
            Assert.Equal(Math.Atan2(ia, ra), Math.Atan2(ib, rb), 1e-4);
        }
        Assert.True(both > 1000);
        s.BuildH0(1, N, b);
        long before = GC.GetAllocatedBytesForCurrentThread();
        s.BuildH0(1, N, b);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
