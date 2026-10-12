using System.Numerics;
using Fleetwright.Map;
using Fleetwright.Sim.Core;
using Xunit.Abstractions;

namespace Fleetwright.Tests;

/// <summary>Zooming in, from the globe to 10 px/m: the land's fill, checked pixel by pixel against the land it was built
/// from, and the levels of detail and grid only ever getting finer. This caught clipping by planes through the
/// Earth's centre filling the wrong side of Afro-Eurasia and the Americas (rings over a hemisphere) whenever the
/// screen was covered.</summary>
public class LodSweepTests(ITestOutputHelper output)
{
    const double W = 1600, H = 900;
    static Land L(string r) => Land.Load(Path.Combine(Paths.Root, "Content", "World", $"land-{r}.bin"));
    static readonly Land Coarse = L("110m"), Medium = L("50m"), Fine = L("10m");

    /// <summary>The winding number at a pixel, from the fill's triangles: what the GPU's coverage pass counts.</summary>
    internal static int Winding(ReadOnlySpan<Vector2> fill, double x, double y)
    {
        var p = new Vector2((float)(x / W * 2 - 1), (float)(1 - y / H * 2));
        int w = 0;
        for (int i = 0; i + 2 < fill.Length; i += 3)
        {
            var (a, b, c) = (fill[i], fill[i + 1], fill[i + 2]);
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            if (d1 > 0 && d2 > 0 && d3 > 0) w++;
            else if (d1 < 0 && d2 < 0 && d3 < 0) w--;
        }
        return w;
    }

    static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;

    /// <summary>Zooms in from the globe to 10 px/m at a cursor, and at every step checks the fill against the land it
    /// was built from: under each of a grid of probe pixels, filled exactly where that level's IsLand says land. Pixels
    /// within a few pixels of a coast are left out (the fill is exact to the projected polygon, the probe to the
    /// sphere's).</summary>
    [Theory]
    [InlineData(56.0, 3.0, 800, 450)]     // the North Sea, at the centre
    [InlineData(56.0, 3.0, 1100, 300)]    // the same, the cursor off centre
    [InlineData(41.0, 15.0, 800, 450)]    // the Mediterranean
    [InlineData(-34.0, 18.5, 800, 450)]   // the Cape
    [InlineData(70.0, 25.0, 800, 450)]    // the North Cape
    public void Fill_matches_the_land_at_every_zoom(double lat, double lon, double cx, double cy)
    {
        var view = new LandView(Coarse, Medium, Fine);
        var lines = new MapLines();
        var cam = MapCamera.At(lat, lon, 12_000);
        int bad = 0, checkedPx = 0, lastLevel = 0;
        double lastSpacing = double.MaxValue;
        var report = new List<string>();
        for (int k = 0; k <= 45; k++)
        {
            if (k > 0)
                cam.ZoomAt(1 / 1.3, cx, cy, W, H);
            var v = new MapView(cam, W, H);
            lines.Begin(W, H);
            view.Build(v, lines);
            var level = view.LevelFor(v);
            int levelIndex = level == Coarse ? 0 : level == Medium ? 1 : 2;
            double spacing = Graticule.Spacing(cam.MetresPerPixel);
            Assert.True(levelIndex >= lastLevel, $"step {k}: the level went back to {Name(level)}");
            Assert.True(spacing <= lastSpacing, $"step {k}: the grid went back to {Graticule.Label(spacing)}");
            (lastLevel, lastSpacing) = (levelIndex, spacing);
            int stepBad = 0;
            for (double y = 30; y < H; y += 120)
                for (double x = 30; x < W; x += 120)
                {
                    if (!v.Pick(x, y, out var p) || NearCoast(v, level, x, y))
                        continue;
                    checkedPx++;
                    bool filled = Winding(view.Fill, x, y) != 0, land = level.IsLand(p);
                    if (filled != land)
                    {
                        stepBad++;
                        var (pl, po) = Earth.LatLon(p);
                        output.WriteLine($"  wrong at ({x}, {y}) = {pl:F6}, {po:F6}: filled {filled}, land {land}, winding {Winding(view.Fill, x, y)}");
                    }
                }
            if (stepBad > 0)
                report.Add($"step {k}, {cam.MetresPerPixel:F1} m/px, {Name(level)}: {stepBad} probes wrong");
            bad += stepBad;
        }
        foreach (var r in report)
            output.WriteLine(r);
        Assert.True(bad == 0, $"{bad} of {checkedPx} probes wrong: " + string.Join("; ", report.Take(20)));
    }

    /// <summary>Whether a coast passes within a few pixels: land and sea differ among the probe's neighbours.</summary>
    static bool NearCoast(in MapView v, Land level, double x, double y)
    {
        const double R = 3;
        bool? first = null;
        foreach (var (dx, dy) in new[] { (0.0, 0.0), (R, 0.0), (-R, 0.0), (0.0, R), (0.0, -R) })
        {
            if (!v.Pick(x + dx, y + dy, out var q))
                return true;
            bool l = level.IsLand(q);
            first ??= l;
            if (l != first)
                return true;
        }
        return false;
    }

    static string Name(Land l) => l == Coarse ? "110m" : l == Medium ? "50m" : "10m";
}
