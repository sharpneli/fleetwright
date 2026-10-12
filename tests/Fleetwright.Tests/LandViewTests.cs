using System.Diagnostics;
using Fleetwright.Map;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Tests;

/// <summary>The land as the map builds it each frame: no garbage once its buffers have grown (the times are printed,
/// not asserted: frame cost is measured in the game with -frametimes), and the fill lands where the land is.</summary>
public class LandViewTests(ITestOutputHelper output)
{
    const double W = 1600, H = 900;

    static readonly LandView View = new(Load("110m"), Load("50m"), Load("10m"));

    static Land Load(string res) => Land.Load(Path.Combine(Paths.Root, "Content", "World", $"land-{res}.bin"));

    // the globe, a theatre, operations off Orkney, tactical in Scapa Flow, a ship over Hoy
    static readonly MapCamera[] Cameras =
    [
        MapCamera.At(56.7, 5.8, 12_000), MapCamera.At(56, 3, 2500), MapCamera.At(58.9, -3.1, 150),
        MapCamera.At(58.85, -3.15, 15), MapCamera.At(58.88, -3.3, 0.1),
    ];

    [Fact]
    public void Building_allocates_nothing()
    {
        var lines = new MapLines();
        foreach (var cam in Cameras)
        {
            lines.Begin(W, H);
            View.Build(new MapView(cam, W, H), lines);
        }
        var ms = new double[Cameras.Length];
        var counts = new (int Fill, int Coast)[Cameras.Length];
        var sw = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int c = 0; c < Cameras.Length; c++)
        {
            var v = new MapView(Cameras[c], W, H);
            sw.Restart();
            const int N = 10;
            for (int k = 0; k < N; k++)
            {
                lines.Begin(W, H);
                View.Build(v, lines);
            }
            ms[c] = sw.Elapsed.TotalMilliseconds / N;
            counts[c] = (View.Fill.Length / 3, lines.Vertices.Length / 6);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        for (int c = 0; c < Cameras.Length; c++)
            output.WriteLine($"{Cameras[c].MetresPerPixel,8} m/px: {ms[c],6:F2} ms, {counts[c].Fill} fill triangles, {counts[c].Coast} coast segments");
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Over_land_the_view_is_filled_and_over_sea_it_is_not()
    {
        var lines = new MapLines();
        // over Hoy at 10 px/m the whole screen is land, and no coast is in view (nor a walk along the cap drawn as one)
        lines.Begin(W, H);
        View.Build(new MapView(MapCamera.At(58.88, -3.3, 0.1), W, H), lines);
        foreach (var (x, y) in Probes)
            Assert.NotEqual(0, LodSweepTests.Winding(View.Fill, x, y));
        Assert.Equal(0, lines.Vertices.Length);
        // over the Jutland bank at 10 px/m: open sea everywhere
        lines.Begin(W, H);
        View.Build(new MapView(MapCamera.At(56.7, 5.8, 0.1), W, H), lines);
        foreach (var (x, y) in Probes)
            Assert.Equal(0, LodSweepTests.Winding(View.Fill, x, y));
    }

    [Fact]
    public void The_level_shows_while_its_segments_are_short_on_screen()
    {
        // by the zoom alone, never the window's size; the chosen level's median segment at most MaxSegmentPx, and the
        // next coarser one's over it
        Land[] levels = [Load("110m"), Load("50m"), Load("10m")];
        var view = new LandView(levels[0], levels[1], levels[2]);
        for (double mpp = 20_000; mpp > 0.1; mpp /= 1.05)
        {
            var level = view.LevelFor(mpp);
            Assert.Same(level, view.LevelFor(new MapView(MapCamera.At(0, 0, mpp), 800, 600)));
            Assert.Same(level, view.LevelFor(new MapView(MapCamera.At(0, 0, mpp), 3840, 2160)));
            int i = Array.IndexOf(levels, level);
            Assert.True(i == 2 || level.MedianSegmentMetres / mpp <= LandView.MaxSegmentPx);
            Assert.True(i == 0 || levels[i - 1].MedianSegmentMetres / mpp > LandView.MaxSegmentPx);
        }
    }

    static readonly (double X, double Y)[] Probes = [(800, 450), (5, 5), (1595, 5), (5, 895), (1595, 895), (400, 700)];
}
