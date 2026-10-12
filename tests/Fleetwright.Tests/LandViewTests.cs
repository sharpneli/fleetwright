using System.Diagnostics;
using Fleetwright.Map;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Tests;

/// <summary>The land as the map builds it each frame: no garbage once its buffers have grown, quick enough at every
/// band, and the fill lands where the land is.</summary>
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
    public void Building_allocates_nothing_and_is_quick()
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
        Assert.All(ms, t => Assert.True(t < 4, $"{t:F2} ms"));
    }

    [Fact]
    public void Over_land_the_view_is_filled_and_over_sea_it_is_not()
    {
        var lines = new MapLines();
        // over Hoy at 10 px/m the whole screen is land: one clipped ring, the screen's quad
        lines.Begin(W, H);
        View.Build(new MapView(MapCamera.At(58.88, -3.3, 0.1), W, H), lines);
        Assert.True(View.Fill.Length >= 6);
        Assert.Equal(0, lines.Vertices.Length);   // no coast in view, and no joins drawn as coast
        // over the Jutland bank at 10 px/m: open sea, nothing to fill
        lines.Begin(W, H);
        View.Build(new MapView(MapCamera.At(56.7, 5.8, 0.1), W, H), lines);
        Assert.Equal(0, View.Fill.Length);
    }
}
