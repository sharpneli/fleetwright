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
    static readonly LandMesh CoarseMesh = LandMesh.Bake(Coarse), MediumMesh = LandMesh.Bake(Medium);

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
            var mesh = levelIndex == 0 ? CoarseMesh : levelIndex == 1 ? MediumMesh : null;
            var bins = mesh != null ? Bin(mesh, v) : null;
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
                    // the baked level too, where it's drawn: its fans projected, away from the limb
                    if (bins != null && !NearLimb(v, x, y))
                    {
                        checkedPx++;
                        int w = Winding(bins, x, y);
                        if ((w != 0) != land)
                        {
                            stepBad++;
                            var (pl, po) = Earth.LatLon(p);
                            output.WriteLine($"  baked wrong at ({x}, {y}) = {pl:F6}, {po:F6}: winding {w}, land {land}");
                        }
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

    /// <summary>A baked level's fans projected through the view (triangles with a vertex beyond the horizon left out,
    /// as the GPU's depth test hides them away from the limb), in NDC, binned by a coarse screen grid.</summary>
    static List<Vector2>[] Bin(LandMesh mesh, in MapView v)
    {
        const int Cols = 16, Rows = 9;
        var bins = new List<Vector2>[Cols * Rows];
        for (int b = 0; b < bins.Length; b++)
            bins[b] = [];
        var f = mesh.Fill;
        Span<Vector2> q = stackalloc Vector2[3];
        for (int t = 0; t + 2 < f.Length; t += 3)
        {
            bool ok = true;
            for (int k = 0; k < 3 && ok; k++)
            {
                ok = v.Project(new Vec3d(f[t + k].X, f[t + k].Y, f[t + k].Z), out double x, out double y);
                q[k] = new Vector2((float)x, (float)y);
            }
            if (!ok)
                continue;
            float x0 = MathF.Min(q[0].X, MathF.Min(q[1].X, q[2].X)), x1 = MathF.Max(q[0].X, MathF.Max(q[1].X, q[2].X));
            float y0 = MathF.Min(q[0].Y, MathF.Min(q[1].Y, q[2].Y)), y1 = MathF.Max(q[0].Y, MathF.Max(q[1].Y, q[2].Y));
            if (x1 < 0 || y1 < 0 || x0 > W || y0 > H)
                continue;
            int c0 = Math.Clamp((int)(x0 / W * Cols), 0, Cols - 1), c1 = Math.Clamp((int)(x1 / W * Cols), 0, Cols - 1);
            int r0 = Math.Clamp((int)(y0 / H * Rows), 0, Rows - 1), r1 = Math.Clamp((int)(y1 / H * Rows), 0, Rows - 1);
            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                    foreach (var p in q)
                        bins[r * Cols + c].Add(new Vector2((float)(p.X / W * 2 - 1), (float)(1 - p.Y / H * 2)));
        }
        return bins;
    }

    static int Winding(List<Vector2>[] bins, double x, double y)
    {
        int c = Math.Clamp((int)(x / W * 16), 0, 15), r = Math.Clamp((int)(y / H * 9), 0, 8);
        return Winding(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(bins[r * 16 + c]), x, y);
    }

    /// <summary>Whether the globe's limb is within 30 px.</summary>
    static bool NearLimb(in MapView v, double x, double y)
    {
        const double R = 30;
        return !v.Pick(x - R, y, out _) || !v.Pick(x + R, y, out _) || !v.Pick(x, y - R, out _) || !v.Pick(x, y + R, out _);
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
